using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type Adventure = typeof(Entity).Assembly.GetType("Revora.Game.Features.AdventureTravel");
    static void AdventureNpc(GameState p, uint id, byte option = 1) {
        p.ActiveNpc = id; Npcs.GetDialog(new NpcRequest(true) { NpcID = id, OptionID = option }, p);
    }
    static void AdventureWalk(GameState p, ushort x, ushort y, bool packets=false) {
        var map = p.Map; int width = map.Floor.Bounds.Width, height = map.Floor.Bounds.Height;
        if (!map.Floor[p.Entity.X, p.Entity.Y, MapObjectType.Player] || !map.Floor[x, y, MapObjectType.Player]) throw new Exception("Adventure walk endpoint is blocked: " + map.ID + ":" + x + "," + y);
        int start = p.Entity.X + p.Entity.Y * width, end = x + y * width; var previous = new int[width * height]; previous[start] = -1;
        var queue = new Queue<int>(); queue.Enqueue(start);
        while (queue.Count > 0 && previous[end] == 0) {
            int cell = queue.Dequeue(), cx = cell % width, cy = cell / width;
            for (int d = 0; d < 8; d++) {
                int dx = Entity.XDir[d], dy = Entity.YDir[d], nx = cx + dx, ny = cy + dy;
                if (!map.Floor[nx, ny, MapObjectType.Player] || dx != 0 && dy != 0 && (!map.Floor[cx + dx, cy, MapObjectType.Player] || !map.Floor[cx, cy + dy, MapObjectType.Player])) continue;
                if(packets && (bool)Call(typeof(Calculations),null,"GuildGateBlocks",map.ID,(ushort)cx,(ushort)cy,(ushort)nx,(ushort)ny))continue;
                int next = nx + ny * width; if (previous[next] != 0) continue; previous[next] = cell + 1; queue.Enqueue(next);
            }
        }
        if (previous[end] == 0) throw new Exception("No ordinary walking path: " + map.ID + ":" + x + "," + y);
        var path = new Stack<int>(); for (int cell = end; previous[cell] != -1; cell = previous[cell] - 1) path.Push(cell);
        int steps = path.Count;
        while (path.Count > 0) {
            int cell = path.Pop(); ushort nx = (ushort)(cell % width), ny = (ushort)(cell / width);
            var direction=Kernel.GetAngle(p.Entity.X,p.Entity.Y,nx,ny);
            if(packets){var frame=new GroundMovement(true){UID=p.Entity.UID,MapID=p.Entity.MapID,Direction=direction,GroundMovementType=GroundMovement.Walk}.ToArray();
                Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);PacketHandler.HandlePacket(frame,p);}
            else if(!p.Entity.Move(direction,1))throw new Exception("Adventure ordinary movement rejected.");
            if (p.Entity.X != nx || p.Entity.Y != ny || Fenced(p))
                throw new Exception("Adventure ordinary movement failed.");
        }
        Check(p.Entity.X == x && p.Entity.Y == y, (packets?"sealed movement packets reach ":"ordinary server walking reaches ") + map.ID + " (" + x + "," + y + ") in " + steps + " valid steps");
    }
    static void AdventurePortal(GameState p, int index, ushort x, ushort y) {
        AdventureWalk(p, x, y); p.Map.Portals.Clear(); Call(typeof(Map), p.Map, "LoadPortals");
        var portal = p.Map.Portals[index]; var first = p.Map.Portals.First(i => Kernel.GetDistance(i.CurrentX, i.CurrentY, x, y) <= 4);
        if (!ReferenceEquals(first, portal)) throw new Exception("Adventure path selected a different nearby portal.");
        var bytes = new Data(true) { ID = Data.UsePortal, UID = p.Entity.UID, dwParam = (uint)(portal.CurrentX | portal.CurrentY << 16) }.ToArray();
        Writer.WriteUInt64(PacketHandler.ClientSeal, bytes.Length - 8, bytes); Data.Process(bytes, p);
        Check(!Fenced(p) && p.Entity.MapID == portal.DestinationMapID && p.Map.Floor[p.Entity.X, p.Entity.Y, MapObjectType.Player],
            "native portal action reaches a valid island landing: " + portal.CurrentMapID + "/" + index);
    }
    static void AdventureGo(GameState p, ushort destination) {
        p.Map.Portals.Clear(); Call(typeof(Map), p.Map, "LoadPortals");
        int index = p.Map.Portals.FindIndex(i => i.DestinationMapID == destination);
        if (index < 0) throw new Exception("Adventure connection is missing.");
        var portal = p.Map.Portals[index]; AdventurePortal(p, index, portal.CurrentX, portal.CurrentY);
    }
    static void AdventureNear(GameState p, uint id, bool packets=false) {
        var npc = p.Map.Npcs[id];
        for (int radius = 1; radius <= 6; radius++)
            for (int dx = -radius; dx <= radius; dx++)
                for (int dy = -radius; dy <= radius; dy++) {
                    int x = npc.X + dx, y = npc.Y + dy;
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius || !p.Map.Floor[x, y, MapObjectType.Player]) continue;
                    AdventureWalk(p, (ushort)x, (ushort)y,packets); return;
                }
        throw new Exception("No legal standing point beside adventure NPC " + id);
    }
    static void AdventureBegin(string runtime, Map town, GameState p) {
        // Model the exact pre-extension schema before production initialization.
        Query(@"CREATE TABLE IF NOT EXISTS revora_adventure_passages(
uid bigint PRIMARY KEY CHECK(uid>0),pending boolean NOT NULL,paid_at timestamptz NOT NULL DEFAULT now(),
trips bigint NOT NULL CHECK(trips>0),silver_before bigint NOT NULL CHECK(silver_before>=1000),
silver_after bigint NOT NULL CHECK(silver_after=silver_before-1000))");
        Query("INSERT INTO revora_adventure_passages(uid,pending,trips,silver_before,silver_after) VALUES(@uid,true,1,2000,1000)","uid",(long)uint.MaxValue-1);
        Call(Adventure, null, "Initialize"); Call(Adventure, null, "Initialize"); Call(Travel, null, "Initialize");
        Check(Scalar("SELECT count(*) FROM revora_adventure_passages WHERE uid=@uid AND pending AND trips=1 AND silver_before=2000 AND silver_after=1000 AND npc_id=9884 AND destination=1216",uint.MaxValue-1)==1,
            "idempotent migration preserves an old paid Adventure passage and its original conductor/destination");
        Query("DELETE FROM revora_adventure_passages WHERE uid=@uid","uid",(long)uint.MaxValue-1);
        var guild = World(runtime, 1038); var center = World(runtime, 1216); World(runtime, 1210); World(runtime, 1211);
        var far = TravelAlias(runtime, 1219, 1212); var island = Kernel.Maps[1212];
        MiningNpc(town, 380); MiningNpc(center, 300655); MiningNpc(island, 300653); MiningNpc(far, 300654);
        using (var c = DataHolder.MySqlConnection) { c.Open(); using (var q = new Npgsql.NpgsqlCommand("SELECT id,cellx,celly,lookface,type,name,life,maxlife FROM public.sobnpcs WHERE id=9884 AND mapid=1038", c))
        using (var r = q.ExecuteReader()) { if (!r.Read()) throw new Exception("Adventure transporter missing.");
            guild.AddNpc(new SobNpcSpawn { UID = 9884, MapID = 1038, X = Convert.ToUInt16(r[1]), Y = Convert.ToUInt16(r[2]), Mesh = Convert.ToUInt16(r[3]),
                Type = (Enums.NpcType)Convert.ToByte(r[4]), Name = r.GetString(5), Hitpoints = Convert.ToUInt32(r[6]), MaxHitpoints = Convert.ToUInt32(r[7]) }); } }
        AdventureGo(p, 1020); AdventureGo(p, 1011); AdventureGo(p, 1002); AdventureNear(p, 380); AdventureNpc(p, 380);
        Check(!Fenced(p) && p.Entity.MapID == 1038 && p.Entity.GuildID == 0 && p.Account.State == AccountTable.AccountState.Player,
            "installed Guild Controller admits an ordinary unguilded visitor to the public transport area");
        AdventureNear(p, 9884); AdventureNpc(p, 9884, 0); Parallel.For(0, 10, i => AdventureNpc(p, 9884));
        Check(!Fenced(p) && p.Entity.MapID == 1216 && p.Entity.Money == 100
            && Scalar("SELECT trips FROM revora_adventure_passages WHERE uid=@uid AND NOT pending", p.Entity.UID) == 1 && HasAchievement(p,10117),
            "installed guild transporter charges exactly one 1,000-silver fare for concurrent clicks");
        AdventureNear(p, 300655); AdventureNpc(p, 300655);
        Check(!Fenced(p) && p.Entity.MapID == 1210 && p.Entity.Money == 100, "Old Explorer opens the actual Adventure Islands route for free");
        AdventurePortal(p, 0, 4, 329); AdventurePortal(p, 0, 3, 400);
        Check(p.Entity.MapID == 1212 && p.Entity.X == 1016 && p.Entity.Y == 1293,
            "blocked installed island entry is corrected to the nearest legal landing");
        AdventurePortal(p, 19, 904, 1045); AdventurePortal(p, 15, 898, 895); AdventurePortal(p, 12, 865, 848);
        AdventurePortal(p, 0, 780, 837); AdventurePortal(p, 2, 583, 826); AdventureWalk(p, 517, 732);
        Check(HasAchievement(p, 10715) && HasAchievement(p, 10716) && HasAchievement(p, 10717),
            "accepted walking and island crossings earn the existing three distinct travel achievements");
    }
    static void AdventureReturn(GameState p) {
        AdventurePortal(p, 22, 478, 692); AdventurePortal(p, 24, 349, 515); AdventurePortal(p, 5, 394, 391); AdventurePortal(p, 6, 312, 175);
        AdventureNear(p, 300653); AdventureNpc(p, 300653);
        Check(!Fenced(p) && p.Entity.MapID == 1219 && p.Entity.X == 450 && p.Entity.Y == 269
            && p.Map.Floor[p.Entity.X, p.Entity.Y, MapObjectType.Player], "Grandpa's blocked fixed-alias landing is corrected while preserving logical map 1219");
        AdventureNear(p, 300654); AdventureNpc(p, 300654);
        Check(!Fenced(p) && p.Entity.MapID == 1002 && p.Entity.Money == 100 && HasAchievement(p, 10718),
            "Boatman returns the hunter and Clean Water to Twin City without an extra fare");
        AdventureNear(p, 59558);
    }
    static GameState AdventureVisitor(Map map, uint npc) {
        var p = TravelPlayer(map, (ushort)(map.Npcs[npc].X + 1), map.Npcs[npc].Y); p.ActiveNpc = npc;
        Collection(p); p.Entity.MyAchievement = new Achievement(p.Entity); p.Entity.MyAchievement.Load("");
        p.Entity.AdjustMoney(2000); EntityTable.UpdateMoney(p); return p;
    }
    static void VerifyAdventureGuards(string runtime, Map town) {
        var guild = Kernel.Maps[1038];
        foreach (string mode in new[] { "funds", "far", "trade", "booth", "dead", "fake", "unloaded", "stale", "map", "receipt", "purse", "missing-map", "wrong-floor" }) {
            var p = AdventureVisitor(guild, 9884); var destination = Kernel.Maps[1216]; ushort profile = destination.BaseID;
            if (mode == "funds") { p.Entity.AdjustMoney(-1001); EntityTable.UpdateMoney(p); }
            if (mode == "far") p.Entity.X += 10; if (mode == "trade") p.Trade.InTrade = true; if (mode == "booth") p.Booth = new Revora.Game.ConquerStructures.Booth();
            if (mode == "dead") p.Entity.Hitpoints = 0; if (mode == "fake") p.Fake = true; if (mode == "unloaded") p.Entity.FullyLoaded = false;
            if (mode == "stale") Query("UPDATE entities SET money=3000 WHERE uid=@uid", "uid", (long)p.Entity.UID);
            if (mode == "map") Query("UPDATE entities SET mapid=1002 WHERE uid=@uid", "uid", (long)p.Entity.UID);
            if (mode == "missing-map") Kernel.Maps.Remove(1216); if (mode == "wrong-floor") destination.BaseID = 1002;
            bool trigger = mode == "receipt" || mode == "purse"; string table = mode == "receipt" ? "revora_adventure_passages" : "entities";
            if (trigger) Query("CREATE FUNCTION suppress_adventure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_adventure BEFORE " + (mode == "receipt" ? "INSERT" : "UPDATE") + " ON " + table + " FOR EACH ROW EXECUTE FUNCTION suppress_adventure()");
            try { AdventureNpc(p, 9884); }
            finally { if (trigger) Query("DROP TRIGGER suppress_adventure ON " + table + ";DROP FUNCTION suppress_adventure()"); if (mode == "missing-map") Kernel.Maps[1216] = destination; destination.BaseID = profile; p.Booth = null; }
            Check(p.Entity.MapID == 1038 && Scalar("SELECT count(*) FROM revora_adventure_passages WHERE uid=@uid", p.Entity.UID) == 0
                && Scalar("SELECT money FROM entities WHERE uid=@uid", p.Entity.UID) == (mode == "funds" ? 999 : mode == "stale" ? 3000 : 2000),
                "invalid adventure admission preserves the complete purse: " + mode);
        }
        foreach (bool suppressArrival in new[] { false, true }) {
            var p = AdventureVisitor(guild, 9884);
            if (suppressArrival) Query("CREATE FUNCTION suppress_arrival() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_arrival BEFORE UPDATE ON revora_adventure_passages FOR EACH ROW EXECUTE FUNCTION suppress_arrival()");
            else Query("ALTER TABLE entities ADD CONSTRAINT reject_adventure CHECK(mapid<>1216) NOT VALID");
            try { AdventureNpc(p, 9884); }
            finally { if (suppressArrival) Query("DROP TRIGGER suppress_arrival ON revora_adventure_passages;DROP FUNCTION suppress_arrival()"); else Query("ALTER TABLE entities DROP CONSTRAINT reject_adventure"); }
            Check(Fenced(p) && p.Entity.MapID == 1038 && p.Entity.Money == 1000
                && Scalar("SELECT trips FROM revora_adventure_passages WHERE uid=@uid AND pending", p.Entity.UID) == 1,
                "failed or silently suppressed arrival retains exactly one resumable paid passage: " + suppressArrival);
            var fresh = ReloadGold(p); fresh.Entity.X = 349; fresh.Entity.Y = 320;
            Parallel.For(0, 8, i => AdventureNpc(fresh, 9884));
            Check(!Fenced(fresh) && fresh.Entity.MapID == 1216 && fresh.Entity.Money == 1000
                && Scalar("SELECT trips FROM revora_adventure_passages WHERE uid=@uid AND NOT pending", fresh.Entity.UID) == 1,
                "production reconnect resumes the same paid adventure journey once: " + suppressArrival);
            fresh.Entity.Teleport(1038, 349, 320); AdventureNpc(fresh, 9884);
            Check(!Fenced(fresh) && fresh.Entity.MapID == 1216 && fresh.Entity.Money == 0
                && Scalar("SELECT trips FROM revora_adventure_passages WHERE uid=@uid AND NOT pending", fresh.Entity.UID) == 2,
                "later intentional adventure trip pays normally against one bounded row: " + suppressArrival);
        }
        foreach (var row in new uint[][] { new uint[] { 1216, 300655 }, new uint[] { 1212, 300653 }, new uint[] { 1219, 300654 } }) {
            var p = AdventureVisitor(Kernel.Maps[(ushort)row[0]], row[1]); ushort x = p.Entity.X;
            p.Entity.X += 10; AdventureNpc(p, row[1]); p.Entity.X = x; p.Trade.InTrade = true; AdventureNpc(p, row[1]); p.Trade.InTrade = false;
            Npcs.GetDialog(new NpcRequest(true) { NpcID = row[1] + 1, OptionID = 1 }, p);
            Check(p.Entity.MapID == row[0] && p.Entity.Money == 2000, "free adventure NPC rejects remote, trading and mismatched requests: " + row[1]);
        }
        var portalMap = Kernel.Maps[1211]; portalMap.Portals.Clear(); Call(typeof(Map), portalMap, "LoadPortals");
        var visitor = TravelPlayer(portalMap, 3, 400); Collection(visitor); visitor.Entity.MyAchievement = new Achievement(visitor.Entity); visitor.Entity.MyAchievement.Load("");
        var target = Kernel.Maps[1212]; ushort old = target.BaseID; target.BaseID = 1002;
        try { GeneralData.UsePortal(new Data(true) { ID = Data.UsePortal, UID = visitor.Entity.UID }, visitor); } finally { target.BaseID = old; }
        Check(visitor.Entity.MapID == 1211 && !HasAchievement(visitor, 10717), "changed destination terrain cannot grant island access or its travel evidence");
        visitor.Trade.InTrade = true; GeneralData.UsePortal(new Data(true) { ID = Data.UsePortal, UID = visitor.Entity.UID }, visitor);
        Check(visitor.Entity.MapID == 1211, "active trading cannot enter an island portal");
        VerifyGuildTransporters(runtime,guild);
    }
    static void VerifyGuildTransporters(string runtime,Map guild) {
        foreach(var route in new[]{new[]{9885,1015},new[]{9886,1217},new[]{9887,1001}}) {
            uint npc=(uint)route[0];ushort target=(ushort)route[1];
            if(target==1217)TravelAlias(runtime,1217,1075);
            else if(!Kernel.Maps.ContainsKey(target))World(runtime,target);
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT cellx,celly,lookface,type,name,life,maxlife FROM public.sobnpcs WHERE id=@id AND mapid=1038",c)) {
                q.Parameters.AddWithValue("id",(int)npc);using(var r=q.ExecuteReader()) {
                    if(!r.Read())throw new Exception("Installed guild transporter missing.");
                    guild.AddNpc(new SobNpcSpawn{UID=npc,MapID=1038,X=Convert.ToUInt16(r[0]),Y=Convert.ToUInt16(r[1]),Mesh=Convert.ToUInt16(r[2]),Type=(Enums.NpcType)Convert.ToByte(r[3]),
                        Name=r.GetString(4),Hitpoints=Convert.ToUInt32(r[5]),MaxHitpoints=Convert.ToUInt32(r[6])});
                }
            }}
            var p=AdventureVisitor(guild,npc);AdventureNpc(p,npc,0);
            Check(!HasAchievement(p,10117),"guild conductor menu does not award a journey: "+npc);
            Parallel.For(0,10,n=>AdventureNpc(p,npc));
            Check(!Fenced(p) && p.Entity.MapID==target && p.Entity.Money==1000 && HasAchievement(p,10117)
                && Scalar("SELECT trips FROM revora_adventure_passages WHERE uid=@uid AND NOT pending",p.Entity.UID)==1,
                "installed guild conductor commits one paid trip and achievement: "+npc);
            p=ReloadGold(p);Call(Adventure,null,"RecoverAchievement",p);new TitleStorage().Login(p);
            Check(!Fenced(p) && p.Entity.MapID==target && p.Entity.Money==1000 && HasAchievement(p,10117),"guild route and award survive production reload: "+npc);
            foreach(string fault in new[]{"payment","arrival","achievement"}) {
                var f=AdventureVisitor(guild,npc);string table=fault=="payment"?"revora_adventure_passages":fault=="arrival"?"entities":"revora_achievement_evidence";
                string predicate=fault=="arrival"?"NEW.mapid<>OLD.mapid":fault=="achievement"?"NEW.achievement_id=10117":"TRUE";
                string verb=fault=="arrival"?"UPDATE":"INSERT";
                Query("CREATE FUNCTION suppress_guild_trip() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF "+predicate+" THEN RETURN NULL; END IF; RETURN NEW; END';CREATE TRIGGER suppress_guild_trip BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_guild_trip()");
                try{AdventureNpc(f,npc);}finally{Query("DROP TRIGGER suppress_guild_trip ON "+table+";DROP FUNCTION suppress_guild_trip()");}
                Check(Fenced(f) && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10117",f.Entity.UID)==0
                    && Scalar("SELECT money FROM entities WHERE uid=@uid",f.Entity.UID)==(fault=="payment"?2000:1000),
                    "failed guild "+fault+" retains exact payment and no false achievement: "+npc);
                f=ReloadGold(f);
                if(fault=="arrival") {
                    f.Entity.X=349;f.Entity.Y=320;AdventureNpc(f,9884);
                    Check(!Fenced(f) && f.Entity.MapID==1038 && f.Entity.Money==1000
                        && Scalar("SELECT trips FROM revora_adventure_passages WHERE uid=@uid AND pending",f.Entity.UID)==1,
                        "a different guild conductor cannot spend or redirect a pending paid trip: "+npc);
                }
                if(fault=="achievement")Call(Adventure,null,"RecoverAchievement",f);
                else {f.Entity.X=(ushort)(guild.Npcs[npc].X+1);f.Entity.Y=guild.Npcs[npc].Y;AdventureNpc(f,npc);}
                Call(Adventure,null,"RecoverAchievement",f);new TitleStorage().Login(f);
                Check(!Fenced(f) && f.Entity.MapID==target && f.Entity.Money==1000 && HasAchievement(f,10117)
                    && Scalar("SELECT trips FROM revora_adventure_passages WHERE uid=@uid AND NOT pending",f.Entity.UID)==1,
                    "guild journey recovers without charging twice after "+fault+": "+npc);
            }
        }
        var unearned=AdventureVisitor(guild,9884);Call(Adventure,null,"RecoverAchievement",unearned);
        Check(!HasAchievement(unearned,10117),"guild-map presence without a completed passage cannot earn portal credit");
    }
}
