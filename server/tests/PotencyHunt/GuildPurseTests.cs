using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Rank=Revora.Game.Enums.GuildMemberRank;

internal static partial class PotencyHuntTests
{
    static readonly Type GuildPurses=typeof(Entity).Assembly.GetType("Revora.Game.Features.GuildWarPurses");
    static GameState PurseMember(GameState leader,Rank rank) {
        var p=GuildClaimant(Kernel.Maps[1002],leader.Guild,false);p.AsMember.Rank=rank;p.Entity.GuildRank=(ushort)rank;
        if(!EntityTable.SaveEntity(p))throw new Exception("Purse rank save failed");return p;
    }
    static void PurseField(GameState p){p.Entity.MapID=1038;p.Entity.X=(ushort)(GuildWar.Pole.X+1);p.Entity.Y=GuildWar.Pole.Y;}
    static void PurseTown(GameState p){p.Entity.MapID=1002;p.Entity.X=317;p.Entity.Y=322;}
    static long PurseRows(GameState p,bool claimed=false){return Scalar("SELECT count(*) FROM revora_guild_purses WHERE uid=@uid"+(claimed?" AND claimed_at IS NOT NULL":""),p.Entity.UID);}
    static void CollectPurse(GameState p){GuildPrize(p,88);}
    static readonly Type SuperPurses=typeof(Entity).Assembly.GetType("Revora.Game.Features.SuperGuildWarPurses");
    static long SuperPurseRows(GameState p,bool paid=false){return Scalar("SELECT count(*) FROM revora_super_guild_purses WHERE uid=@uid"+(paid?" AND claimed_at IS NOT NULL":""),p.Entity.UID);}
    static void SuperCollect(GameState p){Call(SuperPurses,null,"Claim",p);}
    static void VerifyEliteGuildCombat(string runtime) {
        var oldVars=Server.Vars;var oldPole=EliteGuildWar.Poles;var oldKeeper=EliteGuildWar.PoleKeeper;var oldScores=EliteGuildWar.Scores;
        bool oldWar=EliteGuildWar.IsWar,oldFirst=EliteGuildWar.FirstRound;var oldStart=EliteGuildWar.StartTime;
        try {
            Server.Vars=new VariableVault();EliteGuildWar.IsWar=false;EliteGuildWar.PoleKeeper=null;
            ushort terrain=checked((ushort)Scalar("SELECT mapdoc FROM public.maps WHERE id=@uid",2071));
            Map previous;Kernel.Maps.TryGetValue(terrain,out previous);MapsTable.MapInformation info;bool hadInfo=MapsTable.MapInformations.TryGetValue(terrain,out info);
            var field=World(runtime,terrain);field.ID=2071;field.BaseID=terrain;Kernel.Maps[2071]=field;
            if(terrain!=2071){if(previous!=null)Kernel.Maps[terrain]=previous;else Kernel.Maps.Remove(terrain);if(hadInfo)MapsTable.MapInformations[terrain]=info;else MapsTable.MapInformations.Remove(terrain);}
            MapsTable.MapInformations[2071]=new MapsTable.MapInformation{ID=2071,BaseID=terrain,Status=2};
            Query("INSERT INTO sobnpcs SELECT * FROM public.sobnpcs WHERE id=88816 AND mapid=2071 ON CONFLICT DO NOTHING");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT cellx,celly,lookface,type,life,maxlife,name FROM sobnpcs WHERE id=88816 AND mapid=2071",c))
            using(var r=q.ExecuteReader()){if(!r.Read())throw new Exception("Installed Elite Guild pole missing.");field.AddNpc(new SobNpcSpawn{UID=88816,MapID=2071,
                X=Convert.ToUInt16(r[0]),Y=Convert.ToUInt16(r[1]),Mesh=Convert.ToUInt16(r[2]),Type=(Enums.NpcType)Convert.ToByte(r[3]),Hitpoints=Convert.ToUInt32(r[4]),MaxHitpoints=Convert.ToUInt32(r[5]),Name=Convert.ToString(r[6])});}}
            LoadEliteEnvoy(runtime);EliteGuildWar.EliteGwint();var p=PrizeLeader(896);p.BindWorldMap(2071);p.Entity.X=(ushort)(EliteGuildWar.Poles.X+1);p.Entity.Y=EliteGuildWar.Poles.Y;
            uint hp=EliteGuildWar.Poles.Hitpoints;p.Guild.EWarScore=987;
            GuildHit(p,EliteGuildWar.Poles,uint.MaxValue);
            Check(EliteGuildWar.Poles.Hitpoints==hp && EliteGuildWar.PoleKeeper==null,"inactive Elite Guild pole cannot be captured through the damage handler");
            EliteGuildWar.Start();hp=EliteGuildWar.Poles.Hitpoints;
            Check(hp>100 && p.Guild.EWarScore==987,"Elite Guild start resets its pole without clearing shared guild scores");
            var eliteType=typeof(EliteGuildWar);var savedStart=EliteGuildWar.StartTime;
            var savedBattle=(Guid)eliteType.GetField("battleId",Hidden).GetValue(null);
            EliteGuildWar.IsWar=false;eliteType.GetField("pendingStartId",Hidden).SetValue(null,savedBattle);EliteGuildWar.Start();
            Check(EliteGuildWar.IsWar && EliteGuildWar.StartTime==savedStart && Scalar("SELECT count(*) FROM revora_elite_guild_battles WHERE state='active'",0)==1,
                "retry after a lost Elite Guild start projection reuses the saved battle and start time");
            p.Fake=true;GuildHit(p,EliteGuildWar.Poles,uint.MaxValue);p.Fake=false;
            var member=p.AsMember;p.AsMember=null;GuildHit(p,EliteGuildWar.Poles,uint.MaxValue);p.AsMember=member;
            ushort x=p.Entity.X,y=p.Entity.Y;p.Entity.X=0;p.Entity.Y=0;GuildHit(p,EliteGuildWar.Poles,uint.MaxValue);p.Entity.X=x;p.Entity.Y=y;
            var forged=new SobNpcSpawn{UID=88816,MapID=2071,X=EliteGuildWar.Poles.X,Y=EliteGuildWar.Poles.Y,Hitpoints=hp,MaxHitpoints=hp};
            GuildHit(p,forged,uint.MaxValue);
            Check(EliteGuildWar.Poles.Hitpoints==hp && forged.Hitpoints==hp && EliteGuildWar.FirstRound,
                "fake sessions, stale members, remote attackers and replacement pole objects cannot earn Elite Guild credit");
            GuildHit(p,EliteGuildWar.Poles,100);var started=EliteGuildWar.StartTime;
            Parallel.For(0,12,n=>EliteGuildWar.Start());
            Check(EliteGuildWar.Poles.Hitpoints==hp-100 && EliteGuildWar.StartTime==started && p.Guild.EWarScore==987,
                "one ordinary hit applies once and repeated starts retain the active Elite Guild round");
            long wins=Scalar("SELECT wins FROM guilds WHERE id=@uid",p.Guild.ID);
            Parallel.For(0,12,n=>GuildHit(p,EliteGuildWar.Poles,uint.MaxValue));
            Check(EliteGuildWar.PoleKeeper==p.Guild && !EliteGuildWar.FirstRound && EliteGuildWar.Poles.Hitpoints==EliteGuildWar.Poles.MaxHitpoints
                && Scalar("SELECT wins FROM guilds WHERE id=@uid",p.Guild.ID)==wins+1,
                "concurrent lethal hits capture once and do not damage the newly reset Elite Guild pole");
            Check(Scalar("SELECT count(*) FROM revora_elite_guild_captures WHERE guild_id=@uid",p.Guild.ID)==1,
                "Elite Guild capture and guild statistics have one durable receipt");
            eliteType.GetField("captures",Hidden).SetValue(null,0);eliteType.GetField("pendingCapture",Hidden).SetValue(null,1);
            EliteGuildWar.PoleKeeper=null;EliteGuildWar.FirstRound=true;EliteGuildWar.Poles.Hitpoints=0;p.Guild.Wins=(uint)wins;
            EliteGuildWar.SendScores();
            Check(EliteGuildWar.PoleKeeper==p.Guild && EliteGuildWar.Poles.Hitpoints==hp && p.Guild.Wins==wins+1
                && Scalar("SELECT count(*) FROM revora_elite_guild_captures WHERE guild_id=@uid",p.Guild.ID)==1,
                "lost Elite Guild capture projection reconstructs keeper, HP and saved guild statistics without replay");
            Query("CREATE FUNCTION suppress_elite_end() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF NEW.state=''won'' THEN RETURN NULL; END IF; RETURN NEW; END';CREATE TRIGGER suppress_elite_end BEFORE UPDATE ON revora_elite_guild_battles FOR EACH ROW EXECUTE FUNCTION suppress_elite_end()");
            bool rejected=false;try{EliteGuildWar.End();}catch(InvalidOperationException){rejected=true;}finally{Query("DROP TRIGGER suppress_elite_end ON revora_elite_guild_battles;DROP FUNCTION suppress_elite_end()");}
            Check(rejected && EliteGuildWar.IsWar && Scalar("SELECT count(*) FROM revora_elite_guild_battles WHERE state='won'",0)==0,
                "suppressed Elite Guild result cannot end the battle or arm a prize");
            Parallel.For(0,12,n=>EliteGuildWar.End());EliteGuildWar.Claim=false;EliteGuildWar.End();
            Check(!EliteGuildWar.IsWar && !EliteGuildWar.Claim,"inactive Elite Guild ending cannot reopen the legacy claim flag");
            uint frozenSoul=checked((uint)Scalar("SELECT soul_type FROM revora_elite_guild_battles WHERE keeper=@uid AND state='won'",p.Guild.ID));
            EliteGuildWar.IsWar=true;EliteGuildWar.End();
            Check(!EliteGuildWar.IsWar && Scalar("SELECT soul_type FROM revora_elite_guild_battles WHERE keeper=@uid AND state='won'",p.Guild.ID)==frozenSoul,
                "repeated saved Elite Guild settlement retains its originally frozen soul");
            VerifyEliteGuildPrize(p,runtime);
            VerifyEliteCaptureRetry();
            p.BindWorldMap(1002);
        }finally{Server.Vars=oldVars;EliteGuildWar.Poles=oldPole;EliteGuildWar.PoleKeeper=oldKeeper;EliteGuildWar.Scores=oldScores;
            EliteGuildWar.IsWar=oldWar;EliteGuildWar.FirstRound=oldFirst;EliteGuildWar.StartTime=oldStart;}
    }
    static void VerifyEliteCaptureRetry() {
        var p=PrizeLeader(897);p.BindWorldMap(2071);p.Entity.X=(ushort)(EliteGuildWar.Poles.X+1);p.Entity.Y=EliteGuildWar.Poles.Y;
        EliteGuildWar.PoleKeeper=null;EliteGuildWar.Start();long wins=Scalar("SELECT wins FROM guilds WHERE id=@uid",p.Guild.ID);
        bool first=true;
        foreach(string fault in new[]{"revora_elite_guild_battles:UPDATE","revora_elite_guild_captures:INSERT","guilds:UPDATE","sobnpcs:UPDATE"}) {
            string table=fault.Split(':')[0],operation=fault.Split(':')[1];
            Query("CREATE FUNCTION suppress_elite_capture() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_elite_capture BEFORE "+operation+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_elite_capture()");
            bool rejected=false;
            try{if(first)GuildHit(p,EliteGuildWar.Poles,uint.MaxValue);else EliteGuildWar.FinishRound();}
            catch(InvalidOperationException){rejected=true;}
            finally{Query("DROP TRIGGER suppress_elite_capture ON "+table+";DROP FUNCTION suppress_elite_capture()");}
            first=false;
            Check(rejected && EliteGuildWar.IsWar && EliteGuildWar.Poles.Hitpoints==0 && EliteGuildWar.PoleKeeper==null
                && Scalar("SELECT count(*) FROM revora_elite_guild_captures WHERE guild_id=@uid",p.Guild.ID)==0
                && Scalar("SELECT wins FROM guilds WHERE id=@uid",p.Guild.ID)==wins
                && Scalar("SELECT count(*) FROM revora_elite_guild_battles WHERE state='active' AND captures=0 AND keeper=0",0)==1,
                "suppressed "+fault+" rolls back the whole Elite Guild capture while retaining lethal damage for retry");
        }
        EliteGuildWar.SendScores();
        Check(EliteGuildWar.PoleKeeper==p.Guild && EliteGuildWar.Poles.Hitpoints==EliteGuildWar.Poles.MaxHitpoints
            && Scalar("SELECT count(*) FROM revora_elite_guild_captures WHERE guild_id=@uid",p.Guild.ID)==1
            && Scalar("SELECT wins FROM guilds WHERE id=@uid",p.Guild.ID)==wins+1,
            "production score pulse retries a rolled-back lethal capture without requiring another player hit");
        EliteGuildWar.SendScores();EliteGuildWar.End();
        Check(Scalar("SELECT count(*) FROM revora_elite_guild_battles WHERE keeper=@uid AND state='won' AND captures=1 AND claimed_at IS NULL",p.Guild.ID)==1,
            "recovered Elite Guild capture settles once with an unpaid winning prize");
        p.BindWorldMap(1002);
    }
    static void EliteGuildCollect(GameState p) {p.ActiveNpc=115522;Npcs.GetDialog(new NpcRequest(true){NpcID=115522,OptionID=5},p);}
    static long ElitePaid(uint guild) {return Scalar("SELECT count(*) FROM revora_elite_guild_battles WHERE keeper=@uid AND claimed_at IS NOT NULL",guild);}
    static void LoadEliteEnvoy(string runtime) {
        ushort mapId,x,y,mesh;byte kind;
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT mapid,cellx,celly,lookface,type FROM public.npcs WHERE id=115522",c))
        using(var r=q.ExecuteReader()){if(!r.Read())throw new Exception("Installed Elite Guild envoy missing.");mapId=Convert.ToUInt16(r[0]);x=Convert.ToUInt16(r[1]);y=Convert.ToUInt16(r[2]);mesh=Convert.ToUInt16(r[3]);kind=Convert.ToByte(r[4]);}}
        Map map;if(!Kernel.Maps.TryGetValue(mapId,out map))map=World(runtime,mapId);
        if(!map.Npcs.ContainsKey(115522))map.AddNpc(new NpcSpawn{UID=115522,MapID=mapId,X=x,Y=y,Mesh=mesh,Type=(Enums.NpcType)kind});
    }
    static void VerifyEliteGuildPrize(GameState p,string runtime) {
        ushort mapId=1002,x=282,y=161;
        // Plaza restoration now validates both installed clan envoys as well as this desk.
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT id,mapid,cellx,celly,lookface,type FROM public.npcs WHERE id IN(101895,101897)",c))
        using(var r=q.ExecuteReader())while(r.Read()) {
            uint id=Convert.ToUInt32(r[0]);ushort sourceMap=Convert.ToUInt16(r[1]);Map map;
            if(!Kernel.Maps.TryGetValue(sourceMap,out map))map=World(runtime,sourceMap);
            if(!Kernel.Maps.Values.Any(m=>m.Npcs.ContainsKey(id)))map.AddNpc(new NpcSpawn{UID=id,MapID=sourceMap,X=Convert.ToUInt16(r[2]),Y=Convert.ToUInt16(r[3]),Mesh=Convert.ToUInt16(r[4]),Type=(Enums.NpcType)Convert.ToByte(r[5])});
        }}
        bool parked=false;Revora.Game.Features.TwinCityPlaza.Apply((id,map,px,py)=>{if(id==115522)parked=true;});
        Check(!parked && !Revora.Game.Features.TwinCityPlaza.IsClosed(115522) && Kernel.Maps[1002].Npcs[115522].X==282,
            "Elite Guild envoy is restored on ordinary Twin City terrain without re-parking or closing its dialogue");
        Action<GameState> near=player=>{player.BindWorldMap(mapId);player.Entity.X=x;player.Entity.Y=y;player.ActiveNpc=115522;};
        near(p);uint guild=p.Guild.ID,rvp=p.Entity.RevoraPoint;ulong gold=p.Entity.Money;
        uint soul=checked((uint)Scalar("SELECT soul_type FROM revora_elite_guild_battles WHERE keeper=@uid AND state='won'",guild));
        Check(ConquerItemInformation.BaseInformations[soul].PurificationLevel==7 && !p.Fake,"Elite Guild freezes a catalog-valid P7 for an ordinary winning guild");
        p.Entity.X=0;p.Entity.Y=0;EliteGuildCollect(p);near(p);p.Fake=true;EliteGuildCollect(p);p.Fake=false;
        var member=PurseMember(p,Rank.Member);near(member);EliteGuildCollect(member);
        Check(ElitePaid(guild)==0 && p.Entity.Money==gold && member.Inventory.Count==0,"remote, fake and nonleader Elite Guild claims cannot pay");
        for(int i=0;i<40;i++)Pack(p,1088001,1);EliteGuildCollect(p);
        Check(ElitePaid(guild)==0 && p.Entity.Money==gold && p.Entity.RevoraPoint==rvp,"full bag defers the whole Elite Guild prize");
        foreach(var item in p.Inventory.Objects.ToArray())DisRemove(p,item);
        p.Entity.RevoraPoint=uint.MaxValue;Query("UPDATE entities SET revorapoint=@rvp WHERE uid=@uid","rvp",(long)uint.MaxValue,"uid",(long)p.Entity.UID);EliteGuildCollect(p);
        Check(ElitePaid(guild)==0 && !Fenced(p),"Elite Guild RVP overflow leaves the prize pending");
        p.Entity.RevoraPoint=rvp;Query("UPDATE entities SET revorapoint=@rvp WHERE uid=@uid","rvp",(long)rvp,"uid",(long)p.Entity.UID);
        foreach(string fault in new[]{"entities:UPDATE","items:INSERT","status:INSERT","revora_elite_guild_battles:UPDATE"}) {
            string table=fault.Split(':')[0],operation=fault.Split(':')[1];
            Query("CREATE FUNCTION suppress_elite_claim() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_elite_claim BEFORE "+operation+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_elite_claim()");
            try{EliteGuildCollect(p);}finally{Query("DROP TRIGGER suppress_elite_claim ON "+table+";DROP FUNCTION suppress_elite_claim()");}
            Check(Fenced(p) && ElitePaid(guild)==0 && p.Inventory.Count==0 && p.Entity.Money==gold && p.Entity.RevoraPoint==rvp
                && Scalar("SELECT money FROM entities WHERE uid=@uid",p.Entity.UID)==(long)gold
                && Scalar("SELECT count(*) FROM items WHERE entityid=@uid",p.Entity.UID)==0
                && Scalar("SELECT count(*) FROM status WHERE entityid=@uid AND status=176",p.Entity.UID)==0,
                "suppressed "+fault+" rolls back Elite Guild currency, bound soul, effect and claim receipt");DemonReset(p);
        }
        // Restart reconstruction must not discard an earned guild-owned claim or trust mutable flags.
        typeof(EliteGuildWar).GetField("ledgerReady",Hidden).SetValue(null,false);EliteGuildWar.EliteGwint();
        Check(EliteGuildWar.PoleKeeper==p.Guild && ElitePaid(guild)==0,"Elite Guild ledger reload retains the winning identity and unpaid prize");
        EliteGuildWar.Claim=false;EliteGuildWar.KeeperID=0;
        TransferGuild(p,member.AsMember);EliteGuildCollect(p);
        Check(ElitePaid(guild)==0,"former leader cannot collect a guild-owned prize after leadership transfer");
        uint beforeRvp=member.Entity.RevoraPoint;ulong beforeGold=member.Entity.Money;
        Parallel.For(0,12,n=>EliteGuildCollect(member));EliteGuildCollect(member);
        Check(!Fenced(member) && ElitePaid(guild)==1 && member.Inventory.Count==1 && member.Entity.Money==beforeGold+500000000000UL && member.Entity.RevoraPoint==beforeRvp+200000,
            "current ordinary leader receives the frozen Elite Guild prize once despite concurrent requests and cleared legacy flags");
        var itemSoul=member.Inventory.Objects.Single();
        Check(itemSoul.ID==soul && itemSoul.Bound && itemSoul.Days==0 && member.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.JQ_dunpaichuantou),
            "Elite Guild claim projects its exact permanent bound P7 and existing indexed effect");
        Check(Scalar("SELECT claimed_by FROM revora_elite_guild_battles WHERE keeper=@uid AND state='won'",guild)==member.Entity.UID
            && Scalar("SELECT soul_uid FROM revora_elite_guild_battles WHERE keeper=@uid AND state='won'",guild)==itemSoul.UID,
            "Elite Guild receipt records the actual claiming leader and item identity");
        Check(EntityTable.SaveEntity(member),"Elite Guild recipient saves after atomic delivery");
        var reload=ReloadGold(member);ConquerItemTable.LoadItems(reload);reload.Entity.LoadTopStatus();near(reload);EliteGuildCollect(reload);
        Check(reload.Entity.Money==beforeGold+500000000000UL && reload.Entity.RevoraPoint==beforeRvp+200000 && reload.Inventory.Count==1
            && reload.Inventory.Objects.Single().UID==itemSoul.UID && reload.Inventory.Objects.Single().Bound && ElitePaid(guild)==1,
            "ordinary character and item reload retains Elite Guild currencies and bound soul without replay");
        near(reload);Npcs.GetDialog(new NpcRequest(true){NpcID=115522,OptionID=1},reload);
        Check(reload.Entity.MapID==1002,"ordinary Elite Guild envoy refuses admission outside the event");
        EliteGuildWar.Start();near(reload);Npcs.GetDialog(new NpcRequest(true){NpcID=115522,OptionID=1},reload);
        Check(reload.Entity.MapID==2071 && reload.Entity.X==47 && reload.Entity.Y==131,"ordinary Elite Guild envoy admits a player to the installed arena while active");
        // A process restart cancels unfinished battles without fabricating prizes.
        typeof(EliteGuildWar).GetField("ledgerReady",Hidden).SetValue(null,false);EliteGuildWar.EliteGwint();
        Check(!EliteGuildWar.IsWar && Scalar("SELECT count(*) FROM revora_elite_guild_battles WHERE state='cancelled'",0)==1 && ElitePaid(guild)==1,
            "Elite Guild startup cancels an interrupted battle and preserves the paid result");
    }
    static void VerifySuperGuildLifecycle(string runtime) {
        var priorVars=Server.Vars;var priorPole=SuperGuildWar.Pole;var priorLeft=SuperGuildWar.LeftGate;var priorRight=SuperGuildWar.RightGate;
        var priorKeeper=SuperGuildWar.PoleKeeper1;var priorScores=SuperGuildWar.Scores;bool priorWar=SuperGuildWar.IsWar,priorFirst=SuperGuildWar.FirstRound;
        var priorStart=SuperGuildWar.StartTime;var priorLast=SuperGuildWar.LastWin;var priorTop=SuperGuildWar.CurrentTopLeader;
        try{
            Server.Vars=new VariableVault();SuperGuildWar.IsWar=false;SuperGuildWar.PoleKeeper1=null;
            ushort baseId=checked((ushort)Scalar("SELECT mapdoc FROM public.maps WHERE id=@uid",1234));
            Map previousMap;Kernel.Maps.TryGetValue(baseId,out previousMap);var previousInfo=MapsTable.MapInformations[baseId];
            var map=World(runtime,baseId);map.ID=1234;map.BaseID=baseId;Kernel.Maps[1234]=map;
            if(previousMap!=null)Kernel.Maps[baseId]=previousMap;else Kernel.Maps.Remove(baseId);
            MapsTable.MapInformations[baseId]=previousInfo;
            MapsTable.MapInformations[1234]=new MapsTable.MapInformation{ID=1234,BaseID=baseId,Status=2};
            Query("INSERT INTO sobnpcs SELECT * FROM public.sobnpcs WHERE mapid=1234 ON CONFLICT DO NOTHING");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT id,cellx,celly,lookface,type,life,maxlife,name FROM sobnpcs WHERE id IN(811,516076,516077)",c))
            using(var r=q.ExecuteReader())while(r.Read())map.AddNpc(new SobNpcSpawn{UID=Convert.ToUInt32(r[0]),MapID=1234,X=Convert.ToUInt16(r[1]),Y=Convert.ToUInt16(r[2]),
                Mesh=Convert.ToUInt16(r[3]),Type=(Enums.NpcType)Convert.ToByte(r[4]),Hitpoints=Convert.ToUInt32(r[5]),MaxHitpoints=Convert.ToUInt32(r[6]),Name=Convert.ToString(r[7])});}
            SuperGuildWar.Initiate();var winner=PrizeLeader(892);winner.Entity.MapID=1234;winner.Entity.X=(ushort)(SuperGuildWar.Pole.X+1);winner.Entity.Y=SuperGuildWar.Pole.Y;
            var spouse=PurseMember(winner,Rank.LeaderSpouse);var deputy=PurseMember(winner,Rank.DeputyLeader);
            var deputySpouse=PurseMember(winner,Rank.DLeaderSpouse);var member=PurseMember(winner,Rank.Member);var fallen=PurseMember(winner,Rank.Member);
            var absent=PurseMember(winner,Rank.Member);var fake=PurseMember(winner,Rank.Member);var unknown=PurseMember(winner,(Rank)0);var outsider=PrizeLeader(893);
            var eligible=new[]{winner,spouse,deputy,deputySpouse,member,fallen};
            foreach(var p in eligible.Concat(new[]{fake,unknown,outsider})){p.Entity.MapID=1234;p.Entity.X=winner.Entity.X;p.Entity.Y=winner.Entity.Y;}
            fake.Fake=true;fallen.Entity.Hitpoints=0;
            uint cp=winner.Entity.ConquerPoints,rvp=winner.Entity.RevoraPoint;ulong gold=winner.Entity.Money;
            SuperGuildWar.End();Check(winner.Entity.ConquerPoints==cp && !SuperGuildWar.IsWar,"inactive Super Guild War cannot invoke its legacy payout");
            // SaveStart reached PostgreSQL, but model a reply lost before runtime activation.
            Call(typeof(SuperGuildWar),null,"SaveStart");
            var savedStart=typeof(SuperGuildWar).GetField("battleId",Hidden).GetValue(null);var savedTime=SuperGuildWar.StartTime;
            typeof(SuperGuildWar).GetField("pendingStartId",Hidden).SetValue(null,savedStart);
            typeof(SuperGuildWar).GetField("battleId",Hidden).SetValue(null,Guid.Empty);
            SuperGuildWar.Start();
            Check(Scalar("SELECT count(*) FROM revora_super_guild_battles WHERE state='active'",0)==1 && SuperGuildWar.IsWar
                && Equals(typeof(SuperGuildWar).GetField("battleId",Hidden).GetValue(null),savedStart) && SuperGuildWar.StartTime==savedTime,
                "retry after lost start installation reuses its durable UUID and original timestamp");
            uint poleBefore=SuperGuildWar.Pole.Hitpoints;
            long winsBefore=Scalar("SELECT wins FROM guilds WHERE id=@uid",winner.Guild.ID);
            Query("CREATE FUNCTION suppress_super_capture() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_super_capture BEFORE INSERT ON revora_super_guild_captures FOR EACH ROW EXECUTE FUNCTION suppress_super_capture()");
            bool failed=false;
            try{Call(typeof(SuperGuildWar),null,"HitPole",winner.Entity,SuperGuildWar.Pole,new Attack(true){AttackType=Attack.Melee},uint.MaxValue);}
            catch(InvalidOperationException){failed=true;}
            finally{Query("DROP TRIGGER suppress_super_capture ON revora_super_guild_captures;DROP FUNCTION suppress_super_capture()");}
            Check(failed && SuperGuildWar.Pole.Hitpoints==poleBefore && SuperGuildWar.FirstRound
                && Scalar("SELECT sum(captures) FROM revora_super_guild_battles",0)==0
                && Scalar("SELECT wins FROM guilds WHERE id=@uid",winner.Guild.ID)==winsBefore,
                "failed capture rolls back battle and statistics and restores lethal hit for retry");
            var actualMember=winner.AsMember;winner.AsMember=null;
            try{Call(typeof(SuperGuildWar),null,"HitPole",winner.Entity,SuperGuildWar.Pole,new Attack(true){AttackType=Attack.Melee},uint.MaxValue);}
            finally{winner.AsMember=actualMember;}
            Check(SuperGuildWar.Pole.Hitpoints==poleBefore && SuperGuildWar.FirstRound,"stale guild membership cannot capture the Super Guild pole");
            GuildHit(winner,SuperGuildWar.Pole,uint.MaxValue);
            Check(SuperGuildWar.Pole.Hitpoints==SuperGuildWar.Pole.MaxHitpoints,"lethal capture does not hit the newly reset pole a second time");
            GuildHit(winner,SuperGuildWar.Pole,uint.MaxValue);
            Check(Scalar("SELECT count(*) FROM revora_super_guild_captures WHERE guild_id=@uid AND damage>0",winner.Guild.ID)==1
                && Scalar("SELECT wins FROM guilds WHERE id=@uid",winner.Guild.ID)==winsBefore+1,
                "ordinary capture commits its receipt and guild statistics together");
            Check(SuperGuildWar.IsWar && !SuperGuildWar.FirstRound && SuperGuildWar.PoleKeeper1==winner.Guild,
                "ordinary Super Guild War fighter captures the installed pole through combat");
            // Model an acknowledged-by-PostgreSQL capture whose in-memory installation was lost.
            var captureCount=typeof(SuperGuildWar).GetField("captures",Hidden);
            var pendingCapture=typeof(SuperGuildWar).GetField("pendingCapture",Hidden);
            var recoveryTime=typeof(SuperGuildWar).GetField("nextCaptureRecovery",Hidden);
            captureCount.SetValue(null,0);pendingCapture.SetValue(null,1);SuperGuildWar.PoleKeeper1=null;SuperGuildWar.FirstRound=true;
            winner.Guild.Wins=checked((uint)winsBefore);SuperGuildWar.Pole.Hitpoints=1;
            Query("UPDATE revora_super_guild_captures SET guild_id=@wrong WHERE guild_id=@winner","wrong",(long)outsider.Guild.ID,"winner",(long)winner.Guild.ID);
            SuperGuildWar.End();
            Check(SuperGuildWar.IsWar && SuperGuildWar.PoleKeeper1==null && Scalar("SELECT count(*) FROM revora_super_guild_purses",0)==0,
                "mismatched capture receipt cannot finalize a stale or invented winner");
            Query("UPDATE revora_super_guild_captures SET guild_id=@winner WHERE guild_id=@wrong","wrong",(long)outsider.Guild.ID,"winner",(long)winner.Guild.ID);
            recoveryTime.SetValue(null,DateTime.MinValue);SuperGuildWar.SendScores();
            Check(SuperGuildWar.PoleKeeper1==winner.Guild && !SuperGuildWar.FirstRound && SuperGuildWar.Pole.Hitpoints==SuperGuildWar.Pole.MaxHitpoints
                && (int)captureCount.GetValue(null)==1 && (int)pendingCapture.GetValue(null)==0 && winner.Guild.Wins==winsBefore+1,
                "ordinary score tick recovers saved capture, defender, pole and exact statistics after lost installation");
            GuildHit(winner,SuperGuildWar.Pole,uint.MaxValue);SuperGuildWar.SendScores();
            Check(Scalar("SELECT count(*) FROM revora_super_guild_captures",0)==1 && Scalar("SELECT wins FROM guilds WHERE id=@uid",winner.Guild.ID)==winsBefore+1,
                "recovered capture and winner statistics are never incremented twice");
            var started=SuperGuildWar.StartTime;Parallel.For(0,8,n=>SuperGuildWar.Start());
            Check(!SuperGuildWar.FirstRound && SuperGuildWar.StartTime==started && SuperGuildWar.PoleKeeper1==winner.Guild,
                "repeated concurrent starts cannot reset an active Super Guild War capture");
            captureCount.SetValue(null,0);pendingCapture.SetValue(null,1);recoveryTime.SetValue(null,DateTime.MinValue);
            SuperGuildWar.PoleKeeper1=outsider.Guild;SuperGuildWar.FirstRound=true;
            Query("CREATE FUNCTION suppress_super_result() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.state='won' THEN RETURN NULL; END IF; RETURN NEW; END $$;CREATE TRIGGER suppress_super_result BEFORE UPDATE ON revora_super_guild_battles FOR EACH ROW EXECUTE FUNCTION suppress_super_result()");
            failed=false;
            try{SuperGuildWar.End();}catch(InvalidOperationException){failed=true;}
            finally{Query("DROP TRIGGER suppress_super_result ON revora_super_guild_battles;DROP FUNCTION suppress_super_result()");}
            Check(failed && SuperGuildWar.IsWar && winner.Entity.ConquerPoints==cp && SuperGuildWar.PoleKeeper1==winner.Guild && (int)captureCount.GetValue(null)==1,
                "ending recovers the committed keeper before snapshotting recipients; failed result remains retryable and unpaid");
            foreach(string fault in new[]{"recipient","seal"}) {
                string table=fault=="recipient"?"revora_super_guild_purses":"revora_super_guild_battles",op=fault=="recipient"?"INSERT":"UPDATE";
                string condition=fault=="recipient"?"NEW.uid="+deputy.Entity.UID:"NEW.purses_frozen";
                Query("CREATE FUNCTION suppress_super_freeze() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF "+condition+" THEN RETURN NULL; END IF; RETURN NEW; END $$;CREATE TRIGGER suppress_super_freeze BEFORE "+op+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_super_freeze()");
                failed=false;try{SuperGuildWar.End();}catch(InvalidOperationException){failed=true;}
                finally{Query("DROP TRIGGER suppress_super_freeze ON "+table+";DROP FUNCTION suppress_super_freeze()");}
                Check(failed && SuperGuildWar.IsWar && Scalar("SELECT count(*) FROM revora_super_guild_purses",0)==0
                    && Scalar("SELECT count(*) FROM revora_super_guild_battles WHERE state='active' AND NOT purses_frozen",0)==1,
                    "suppressed Super Guild War "+fault+" rolls back result and all preceding recipients");
            }
            Parallel.For(0,12,n=>SuperGuildWar.End());SuperGuildWar.End();
            Check(Scalar("SELECT count(*) FROM revora_super_guild_battles WHERE state='won' AND keeper=@uid AND captures=1 AND ended_at IS NOT NULL",winner.Guild.ID)==1,
                "concurrent endings freeze exactly one canonical terminal result");
            Check(!SuperGuildWar.IsWar && winner.Entity.ConquerPoints==cp && winner.Entity.RevoraPoint==rvp && winner.Entity.Money==gold
                && eligible.All(p=>SuperPurseRows(p)==1 && SuperPurseRows(p,true)==0),"Super Guild War freezes six ordinary purses without invoking legacy multi-write payouts");
            Check(new[]{absent,fake,unknown,outsider}.All(p=>SuperPurseRows(p)==0),"absent, fake, unknown-rank and losing guild players receive no Super Guild War purse");fake.Fake=false;
            VerifySuperPurseDelivery(eligible,cp,rvp,gold);
            SuperGuildWar.Start();Check(SuperGuildWar.IsWar && SuperGuildWar.FirstRound,"a later Super Guild War can start after the lifecycle guard is released");
            Call(typeof(SuperGuildWar),null,"InitializeLedger");
            Check(!SuperGuildWar.IsWar && Scalar("SELECT count(*) FROM revora_super_guild_battles WHERE state='cancelled' AND ended_at IS NOT NULL",0)==1
                && Scalar("SELECT count(*) FROM revora_super_guild_battles WHERE state='won'",0)==1 && SuperGuildWar.PoleKeeper1==winner.Guild,
                "startup recovery cancels interrupted battle and restores canonical previous winner from completed result");
            SuperGuildWar.End();
            Check(winner.Entity.ConquerPoints==cp+Config.SuperGuildWarCp,"recovered inactive event cannot replay completed atomic payout");
            winner.Entity.MapID=1002;
        }finally{SuperGuildWar.IsWar=priorWar;SuperGuildWar.Pole=priorPole;SuperGuildWar.LeftGate=priorLeft;SuperGuildWar.RightGate=priorRight;
            SuperGuildWar.PoleKeeper1=priorKeeper;SuperGuildWar.Scores=priorScores;SuperGuildWar.FirstRound=priorFirst;
            SuperGuildWar.StartTime=priorStart;SuperGuildWar.LastWin=priorLast;SuperGuildWar.CurrentTopLeader=priorTop;Server.Vars=priorVars;}
    }
    static void VerifySuperPurseDelivery(GameState[] eligible,uint cp,uint rvp,ulong gold) {
        var winner=eligible[0];var member=eligible[4];var fallen=eligible[5];
        foreach(var p in eligible)PurseTown(p);
        var soulTypes=eligible.ToDictionary(p=>p.Entity.UID,p=>checked((uint)Scalar("SELECT soul_type FROM revora_super_guild_purses WHERE uid=@uid",p.Entity.UID)));
        Check(soulTypes.Values.All(id=>ConquerItemInformation.BaseInformations[id].PurificationLevel==7),"Super Guild War freezes one valid P7 soul for every eligible rank");
        uint frozenCp=Config.SuperGuildWarCp,frozenRvp=Config.GuildWarLeaderRevoraPoints;ulong frozenGold=Config.SuperGuildWarGold;
        Config.SuperGuildWarCp=1;Config.SuperGuildWarGold=1;Config.GuildWarLeaderRevoraPoints=1;
        try {
            for(int i=0;i<35;i++)Pack(winner,1088001,1);SuperCollect(winner);
            Check(SuperPurseRows(winner,true)==0 && winner.Inventory.Count==35 && winner.Entity.ConquerPoints==cp,"Super Guild War six-item purse waits intact when only five slots are free");
            foreach(var item in winner.Inventory.Objects.ToArray())DisRemove(winner,item);
            foreach(string fault in new[]{"entities:UPDATE","items:INSERT","status:INSERT","revora_super_guild_purses:UPDATE"}) {
                string table=fault.Split(':')[0],op=fault.Split(':')[1];
                Query("CREATE FUNCTION suppress_super_pay() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_super_pay BEFORE "+op+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_super_pay()");
                try{SuperCollect(winner);}finally{Query("DROP TRIGGER suppress_super_pay ON "+table+";DROP FUNCTION suppress_super_pay()");}
                Check(Fenced(winner) && SuperPurseRows(winner,true)==0 && winner.Inventory.Count==0 && winner.Entity.ConquerPoints==cp && winner.Entity.RevoraPoint==rvp && winner.Entity.Money==gold
                    && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0",winner.Entity.UID)==0
                    && Scalar("SELECT count(*) FROM status WHERE entityid=@uid AND status IN(31,32,33)",winner.Entity.UID)==0,
                    "suppressed Super Guild War "+fault+" retains complete pending reward and rolls back all preceding writes");DemonReset(winner);
            }
            Query("UPDATE entities SET revorapoint=revorapoint+1 WHERE uid=@uid","uid",(long)winner.Entity.UID);SuperCollect(winner);
            Check(Fenced(winner) && SuperPurseRows(winner,true)==0,"stale RVP balance cannot be overwritten by Super Guild War delivery");
            Query("UPDATE entities SET revorapoint=@rvp WHERE uid=@uid","rvp",(long)rvp,"uid",(long)winner.Entity.UID);DemonReset(winner);
            Query("CREATE FUNCTION fail_super_soul() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF NEW.id BETWEEN 800000 AND 899999 THEN RAISE EXCEPTION ''fixture soul failure''; END IF; RETURN NEW; END';CREATE TRIGGER fail_super_soul BEFORE INSERT ON items FOR EACH ROW EXECUTE FUNCTION fail_super_soul()");
            try{SuperCollect(winner);}finally{Query("DROP TRIGGER fail_super_soul ON items;DROP FUNCTION fail_super_soul()");}
            Check(Fenced(winner) && SuperPurseRows(winner,true)==0 && winner.Inventory.Count==0
                && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",winner.Entity.UID)==cp
                && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0",winner.Entity.UID)==0,
                "failed Super Guild War P7 insertion rolls back currencies and preceding pack/tokens");DemonReset(winner);
            Call(typeof(Entity),winner.Entity,"ApplyCommittedConquerPoints",uint.MaxValue);
            Query("UPDATE entities SET conquerpoints=@cp WHERE uid=@uid","cp",(long)uint.MaxValue,"uid",(long)winner.Entity.UID);SuperCollect(winner);
            Check(!Fenced(winner) && SuperPurseRows(winner,true)==0 && winner.Inventory.Count==0,"Super Guild War currency overflow retains its entire pending purse");
            Call(typeof(Entity),winner.Entity,"ApplyCommittedConquerPoints",cp);Query("UPDATE entities SET conquerpoints=@cp WHERE uid=@uid","cp",(long)cp,"uid",(long)winner.Entity.UID);
            Parallel.For(0,12,n=>SuperCollect(winner));SuperCollect(winner);
            Check(!Fenced(winner) && SuperPurseRows(winner,true)==1 && winner.Inventory.Count==6 && winner.Entity.ConquerPoints==cp+frozenCp
                && winner.Entity.RevoraPoint==rvp+frozenRvp && winner.Entity.Money==gold+frozenGold,"concurrent delivery pays frozen Super Guild War currencies and six-item purse once");
        }finally{Config.SuperGuildWarCp=frozenCp;Config.SuperGuildWarGold=frozenGold;Config.GuildWarLeaderRevoraPoints=frozenRvp;}
        Check(winner.Inventory.Objects.Count(i=>i.ID==3302411 && i.Bound && i.Days==1)==1 && winner.Inventory.Objects.Count(i=>i.ID==723467 && i.Bound && i.Days==1)==4,
            "Super Guild War leader keeps one bound one-day pack and four bound one-day tokens");
        Check(winner.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.ConuqerSuperYellow) && Scalar("SELECT count(*) FROM status WHERE entityid=@uid AND status=32",winner.Entity.UID)==1,
            "Super Guild War leader honor persists and projects immediately");
        foreach(var p in eligible.Skip(1).Take(3)){SuperCollect(p);Check(SuperPurseRows(p,true)==1 && p.Inventory.Count==4 && p.Entity.ContainsFlag(
            p==eligible[1]?(ulong)FlagsUpdate.Flags.ConuqerSuperYellow:(ulong)FlagsUpdate.Flags.ConuqerSuperBlue),"Super Guild War officer receives atomic four-item purse and correct honor: "+p.AsMember.Rank);}
        TransferGuild(winner,member.AsMember);SuperCollect(member);
        Check(SuperPurseRows(member,true)==1 && member.Inventory.Count==2 && member.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.ConuqerSuperUnderBlue),
            "post-result promotion cannot change frozen Super Guild War member rewards or honor");
        Check(eligible.Take(5).All(p=>p.Inventory.Objects.Count(i=>i.ID==soulTypes[p.Entity.UID] && i.Bound && i.Days==0)==1),"Super Guild War souls retain frozen identity, permanent duration and binding");
        Check(EntityTable.SaveEntity(winner),"Super Guild War winner saves after atomic delivery");var reloaded=ReloadGold(winner);ConquerItemTable.LoadItems(reloaded);SuperCollect(reloaded);
        Check(reloaded.Entity.ConquerPoints==cp+frozenCp && reloaded.Entity.RevoraPoint==rvp+frozenRvp && reloaded.Entity.Money==gold+frozenGold
            && reloaded.Inventory.Count==6 && SuperPurseRows(reloaded,true)==1 && reloaded.Inventory.Objects.Count(i=>i.ID==soulTypes[winner.Entity.UID] && i.Bound && i.Days==0)==1,
            "ordinary character/item reload retains complete Super Guild War purse and rejects replay");
        Kernel.GamePool.Remove(fallen.Entity.UID);
        SuperPurses.GetField("initialized",Hidden).SetValue(null,false);Call(SuperPurses,null,"Initialize");
        Check(SuperPurseRows(fallen,true)==0,"purse migration/reinitialization preserves an offline recipient's pending entitlement");
        var returning=ReloadGold(fallen);ConquerItemTable.LoadItems(returning);
        var previousClock=SuperPurses.GetField("Clock",Hidden).GetValue(null);var delayed=DateTime.UtcNow.AddDays(8);
        SuperPurses.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>delayed));
        try {
        SuperPurses.GetField("nextPoll",Hidden).SetValue(null,DateTime.MinValue);SuperPurses.GetField("cursor",Hidden).SetValue(null,0L);Call(GuildPurses,null,"Tick");
        Check(!Fenced(returning) && SuperPurseRows(returning,true)==1 && returning.Inventory.Count==2,
            "ordinary reconnect delivers fallen member's pending Super Guild War purse through the production reward tick");
        Check(!returning.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.ConuqerSuperUnderBlue)
            && Scalar("SELECT count(*) FROM status WHERE entityid=@uid AND status IN(31,32,33)",returning.Entity.UID)==0,
            "delayed Super Guild War delivery cannot renew expired six-day honor");
        Check(returning.Inventory.Objects.Single(i=>i.ID==3302411).DayStamp==delayed.ToLocalTime()
            && returning.Inventory.Objects.Single(i=>i.ID==soulTypes[fallen.Entity.UID]).Days==0,
            "delayed pack receives a full claim-relative day while the bound P7 stays permanent");
        }finally{SuperPurses.GetField("Clock",Hidden).SetValue(null,previousClock);}
    }
    static void VerifyGuildPurses() {
        var leader=PrizeLeader(220);var spouse=PurseMember(leader,Rank.LeaderSpouse);var deputy=PurseMember(leader,Rank.DeputyLeader);
        var deputySpouse=PurseMember(leader,Rank.DLeaderSpouse);var member=PurseMember(leader,Rank.Member);var fallen=PurseMember(leader,Rank.Member);
        var absent=PurseMember(leader,Rank.Member);var fake=PurseMember(leader,Rank.Member);var unknown=PurseMember(leader,(Rank)0);
        var outsider=PrizeLeader(221);var eligible=new[]{leader,spouse,deputy,deputySpouse,member,fallen};
        foreach(var p in eligible.Concat(new[]{fake,unknown,outsider,absent})){p.Entity.MyAchievement=new Achievement(p.Entity);p.Entity.MyAchievement.Load("");}
        foreach(var p in eligible.Concat(new[]{fake,unknown,outsider}))PurseField(p);fake.Fake=true;fallen.Entity.Hitpoints=0;
        guildNow=new DateTime(2034,1,1,15,0,0,DateTimeKind.Local).ToUniversalTime();GuildWar.Start();GuildHit(leader,GuildWar.Pole,uint.MaxValue);
        Check(HasAchievement(leader,10232),"ordinary fully loaded fighter immediately projects accepted pillar credit");
        guildNow=guildNow.AddHours(1);var ended=guildNow;
        var projection=GuildSeason.GetField("active",Hidden).GetValue(null);
        projection.GetType().GetField("Captures",Instance).SetValue(projection,0);projection.GetType().GetField("Keeper",Instance).SetValue(projection,0u);GuildWar.PoleKeeper=null;
        GuildWar.End();Check(GuildWar.IsWar && PurseRows(leader)==0 && GuildWar.PoleKeeper==leader.Guild,
            "lost capture projection recovers and retries instead of sealing an empty recipient list");
        foreach(string fault in new[]{"recipient","seal"}) {
            string table=fault=="recipient"?"revora_guild_purses":"revora_guild_battles",operation=fault=="recipient"?"INSERT":"UPDATE";
            string condition=fault=="recipient"?"NEW.uid="+deputy.Entity.UID:"NEW.purses_frozen";
            Query("CREATE FUNCTION suppress_purse_snapshot() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF "+condition+" THEN RETURN NULL; END IF; RETURN NEW; END $$;CREATE TRIGGER suppress_purse_snapshot BEFORE "+operation+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_purse_snapshot()");
            try{GuildWar.End();}finally{Query("DROP TRIGGER suppress_purse_snapshot ON "+table+";DROP FUNCTION suppress_purse_snapshot()");}
            Check(GuildWar.IsWar && Scalar("SELECT count(*) FROM revora_guild_purses WHERE day='2034-01-01'",0)==0
                && Scalar("SELECT count(*) FROM revora_guild_battles WHERE day='2034-01-01' AND state='active' AND NOT purses_frozen",0)==1,
                "suppressed "+fault+" snapshot rolls back the entire battle result and all preceding purse entitlements");
        }
        GuildWar.End();Check(!GuildWar.IsWar && eligible.All(p=>PurseRows(p)==1) && eligible.All(p=>PurseRows(p,true)==0),"battle finalization seals all six ordinary eligible purses without paying through the old loop");
        Check(new[]{absent,fake,unknown,outsider}.All(p=>PurseRows(p)==0),"absent, fake, unrecognized-rank and losing-guild players receive no purse entitlement");fake.Fake=false;
        Check(Scalar("SELECT tokens FROM revora_guild_purses WHERE uid=@uid",leader.Entity.UID)==4
            && new[]{spouse,deputy,deputySpouse}.All(p=>Scalar("SELECT tokens FROM revora_guild_purses WHERE uid=@uid",p.Entity.UID)==2)
            && Scalar("SELECT tokens FROM revora_guild_purses WHERE uid=@uid",member.Entity.UID)==0,"leader, officers and members retain the established four/two/zero loose-token quantities");
        var soulTypes=eligible.ToDictionary(p=>p.Entity.UID,p=>checked((uint)Scalar("SELECT soul_type FROM revora_guild_purses WHERE uid=@uid",p.Entity.UID)));
        Check(soulTypes.Values.All(id=>ConquerItemInformation.BaseInformations.ContainsKey(id) && ConquerItemInformation.BaseInformations[id].PurificationLevel==7),
            "winning guild purses freeze one catalog-valid P7 soul per eligible recipient");
        // Model a pre-upgrade pending purse; migration assigns zero rather than inventing a new reward.
        Query("UPDATE revora_guild_purses SET soul_type=0 WHERE uid=@uid","uid",(long)fallen.Entity.UID);
        foreach(var p in eligible)PurseTown(p);fallen.Entity.Hitpoints=fallen.Entity.MaxHitpoints;
        uint wardrobe=StoredQuestGarment(leader);
        uint cp=Config.GuildWarCp,rvp=Config.GuildWarLeaderRevoraPoints;ulong gold=Config.GuildWarGold;long initialCp=leader.Entity.ConquerPoints,initialRvp=leader.Entity.RevoraPoint;decimal initialGold=leader.Entity.Money;
        Config.GuildWarCp=1;Config.GuildWarGold=1;Config.GuildWarLeaderRevoraPoints=1;
        try {
            for(int i=0;i<35;i++)Pack(leader,1088001,1);CollectPurse(leader);Check(PurseRows(leader,true)==0 && leader.Inventory.Count==35,"leader's six-item purse waits intact when only five bag slots are free");
            foreach(var item in leader.Inventory.Objects.ToArray())DisRemove(leader,item);
            foreach(string fault in new[]{"entities:UPDATE","items:INSERT","status:INSERT","revora_guild_purses:UPDATE"}) {
                string table=fault.Split(':')[0],operation=fault.Split(':')[1];
                Query("CREATE FUNCTION suppress_purse_delivery() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_purse_delivery BEFORE "+operation+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_purse_delivery()");
                try{CollectPurse(leader);}finally{Query("DROP TRIGGER suppress_purse_delivery ON "+table+";DROP FUNCTION suppress_purse_delivery()");}
                Check(Fenced(leader) && PurseRows(leader,true)==0 && leader.Inventory.Count==0 && leader.Entity.ConquerPoints==initialCp && leader.Entity.RevoraPoint==initialRvp && leader.Entity.Money==(ulong)initialGold
                    && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0",leader.Entity.UID)==0 && Scalar("SELECT count(*) FROM status WHERE entityid=@uid AND status IN(9,10)",leader.Entity.UID)==0,
                    "suppressed "+fault+" delivery retains currencies, all items, honor and the unclaimed purse");DemonReset(leader);
            }
            Query("UPDATE entities SET revorapoint=revorapoint+1 WHERE uid=@uid","uid",(long)leader.Entity.UID);CollectPurse(leader);
            Check(Fenced(leader) && PurseRows(leader,true)==0 && leader.Inventory.Count==0,"stale saved RVP purse cannot be overwritten by guild rewards");
            Query("UPDATE entities SET revorapoint=@rvp WHERE uid=@uid","rvp",initialRvp,"uid",(long)leader.Entity.UID);DemonReset(leader);
            Call(typeof(Entity),leader.Entity,"ApplyCommittedConquerPoints",uint.MaxValue);
            Query("UPDATE entities SET conquerpoints=@cp WHERE uid=@uid","cp",(long)uint.MaxValue,"uid",(long)leader.Entity.UID);CollectPurse(leader);
            Check(!Fenced(leader) && PurseRows(leader,true)==0 && leader.Inventory.Count==0,"CP overflow leaves the complete purse available without disconnecting its owner");
            Call(typeof(Entity),leader.Entity,"ApplyCommittedConquerPoints",(uint)initialCp);
            Query("UPDATE entities SET conquerpoints=@cp WHERE uid=@uid","cp",initialCp,"uid",(long)leader.Entity.UID);
            leader.Entity.RevoraPoint=uint.MaxValue;Query("UPDATE entities SET revorapoint=@rvp WHERE uid=@uid","rvp",(long)uint.MaxValue,"uid",(long)leader.Entity.UID);CollectPurse(leader);
            Check(!Fenced(leader) && PurseRows(leader,true)==0 && leader.Inventory.Count==0,"RVP overflow leaves the complete purse available");
            leader.Entity.RevoraPoint=(uint)initialRvp;Query("UPDATE entities SET revorapoint=@rvp WHERE uid=@uid","rvp",initialRvp,"uid",(long)leader.Entity.UID);
            Query("CREATE FUNCTION fail_purse_soul() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF NEW.id BETWEEN 800000 AND 899999 THEN RAISE EXCEPTION ''fixture soul insert''; END IF; RETURN NEW; END';CREATE TRIGGER fail_purse_soul BEFORE INSERT ON items FOR EACH ROW EXECUTE FUNCTION fail_purse_soul()");
            try{CollectPurse(leader);}finally{Query("DROP TRIGGER fail_purse_soul ON items;DROP FUNCTION fail_purse_soul()");}
            Check(Fenced(leader) && PurseRows(leader,true)==0 && leader.Inventory.Count==0
                && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",leader.Entity.UID)==initialCp
                && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0",leader.Entity.UID)==0,
                "failed P7 insertion rolls back the guild purse currencies and all preceding legacy items");DemonReset(leader);
            Parallel.For(0,12,i=>CollectPurse(leader));
            Check(!Fenced(leader) && PurseRows(leader,true)==1 && leader.Inventory.Count==6 && leader.Entity.ConquerPoints==initialCp+cp
                && leader.Entity.RevoraPoint==initialRvp+rvp && leader.Entity.Money==(ulong)(initialGold+gold),"duplicate leader collection pays the frozen CP/RVP/gold amounts once despite later configuration changes");
        }finally{Config.GuildWarCp=cp;Config.GuildWarGold=gold;Config.GuildWarLeaderRevoraPoints=rvp;}
        Check(HasAchievement(leader,10228) && !HasAchievement(absent,10228) && !HasAchievement(outsider,10228),"paid guild purse grants reward achievement only to its recipient");
        Check(Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10228",leader.Entity.UID)==1,"duplicate purse claims retain one reward achievement receipt");
        Check(leader.Inventory.Objects.Where(i=>i.ID==3302411 || i.ID==723467).All(i=>i.Bound && i.Days==1 && i.DayStamp.AddDays(1).ToUniversalTime()==ended.AddDays(1))
            && leader.Inventory.Objects.Count(i=>i.ID==3302411)==1 && leader.Inventory.Objects.Count(i=>i.ID==723467)==4,"leader receives the existing bound one-day pack and four one-day loose tokens");
        Check(leader.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.TopGuildLeader) && Scalar("SELECT count(*) FROM status WHERE entityid=@uid AND status=9",leader.Entity.UID)==1,
            "leader honor is saved with the purse and immediately projected through its existing flag");
        GuildPrize(leader,84);GuildPrize(leader,85);Check(PrizeCount(leader)==0,"local purse tokens do not become seasonal Gold Prize earning evidence");
        long laterHonor=(long)Kernel.ToDateTimeInt(ended.AddDays(10).ToLocalTime());
        Query("INSERT INTO status(entityid,status,time) VALUES(@uid,10,@until)","uid",(long)spouse.Entity.UID,"until",laterHonor);
        foreach(var p in new[]{spouse,deputy,deputySpouse}) {uint before=p.Entity.RevoraPoint;CollectPurse(p);Check(!Fenced(p) && PurseRows(p,true)==1 && p.Inventory.Count==4 && p.Entity.RevoraPoint==before+Config.GuildWarOfficerRevoraPoints,
            "officer receives one pack, two tokens, a bound P7 and the configured officer purse: "+p.AsMember.Rank);}
        Check(spouse.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.TopDeputyLeader) && !spouse.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.TopGuildLeader)
            && Scalar("SELECT time FROM status WHERE entityid=@uid AND status=10",spouse.Entity.UID)==laterHonor,
            "collecting an older purse preserves a newer guild honor and its expiry");
        TransferGuild(leader,member.AsMember);uint memberCp=member.Entity.ConquerPoints;CollectPurse(member);
        Check(!Fenced(member) && PurseRows(member,true)==1 && member.Inventory.Objects.Count(i=>i.ID==3302411)==1 && member.Inventory.Count==2 && member.Entity.ConquerPoints==memberCp+Config.GuildWarMemberCp,
            "leadership promotion after the result cannot upgrade a frozen member purse");
        Check(eligible.Where(p=>p!=fallen).All(p=>p.Inventory.Objects.Count(i=>i.ID==soulTypes[p.Entity.UID] && i.Bound && i.Days==0 && i.TimeLeftInMinutes==0)==1),
            "all eligible claimed ranks receive their exact frozen bound permanent P7 soul once");
        Check(EntityTable.SaveEntity(leader),"ordinary character save succeeds after atomic guild purse delivery");Query("DELETE FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10232","uid",(long)leader.Entity.UID);var reloaded=ReloadGold(leader);ConquerItemTable.LoadItems(reloaded);
        Check(Scalar("SELECT count(*) FROM items WHERE uid=@uid AND position=0 AND warehouse=0 AND inwardrobe=1",wardrobe)==1,"guild war purse and production reload preserve stored wardrobe design");
        Check(reloaded.Entity.ConquerPoints==initialCp+cp && reloaded.Entity.RevoraPoint==initialRvp+rvp && reloaded.Entity.Money==(ulong)(initialGold+gold)
            && reloaded.Inventory.Objects.Count(i=>i.ID==723467)==4 && PurseRows(reloaded,true)==1,"ordinary item and character reload retains the delivered purse without reopening it");
        Check(reloaded.Inventory.Objects.Count(i=>i.ID==soulTypes[leader.Entity.UID] && i.Bound && i.Days==0)==1,
            "earned guild P7 identity and permanent binding survive ordinary character/item reload");
        reloaded.Entity.MyAchievement.Send();Check(HasAchievement(reloaded,10232) && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10232",reloaded.Entity.UID)==1,"ordinary character reload recovers pillar credit from its accepted-hit receipt");
        reloaded.Entity.MyAchievement.Send();Check(HasAchievement(reloaded,10228),"guild reward achievement survives ordinary character reload");
        Kernel.GamePool.Remove(fallen.Entity.UID);guildNow=ended.AddDays(8);var returning=ReloadGold(fallen);ConquerItemTable.LoadItems(returning);
        GuildPurses.GetField("nextPoll",Hidden).SetValue(null,DateTime.MinValue);GuildPurses.GetField("cursor",Hidden).SetValue(null,0L);Call(GuildPurses,null,"Tick");
        Check(!Fenced(returning) && PurseRows(returning,true)==1 && returning.Inventory.Objects.Single().ID==3302411,
            "ordinary reconnect allows the automatic poll to deliver a previously offline or fallen member's earned purse");
        Check(HasAchievement(returning,10228),"automatic delivery immediately projects the guild reward achievement");
        Check(returning.Inventory.Objects.Single().DayStamp.AddDays(1).ToUniversalTime()==guildNow.AddDays(1)
            && !returning.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.TopDeputyLeader),"late purse items retain a useful delivery-day lifetime without extending expired six-day honors");
        var rewardFailure=PrizeLeader(229);rewardFailure.Entity.MyAchievement=new Achievement(rewardFailure.Entity);rewardFailure.Entity.MyAchievement.Load("");PurseField(rewardFailure);guildNow=new DateTime(2034,1,8,15,0,0,DateTimeKind.Local).ToUniversalTime();
        GuildWar.Start();GuildHit(rewardFailure,GuildWar.Pole,uint.MaxValue);guildNow=guildNow.AddHours(1);GuildWar.End();PurseTown(rewardFailure);
        Check(PurseRows(rewardFailure)==1 && !HasAchievement(rewardFailure,10228),"earned but undelivered guild purse does not count as receiving rewards");
        uint faultCp=rewardFailure.Entity.ConquerPoints,faultRvp=rewardFailure.Entity.RevoraPoint;ulong faultGold=rewardFailure.Entity.Money;
        Query("CREATE FUNCTION suppress_purse_achievement() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
        Query("CREATE TRIGGER suppress_purse_achievement BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW WHEN (NEW.achievement_id=10228) EXECUTE FUNCTION suppress_purse_achievement()");
        try{CollectPurse(rewardFailure);}finally{Query("DROP TRIGGER suppress_purse_achievement ON revora_achievement_evidence;DROP FUNCTION suppress_purse_achievement()");}
        Check(Fenced(rewardFailure) && PurseRows(rewardFailure,true)==0 && rewardFailure.Inventory.Count==0
            && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",rewardFailure.Entity.UID)==faultCp
            && Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",rewardFailure.Entity.UID)==faultRvp
            && Scalar("SELECT money FROM entities WHERE uid=@uid",rewardFailure.Entity.UID)==(long)faultGold,"failed guild achievement persistence rolls back payment, items and claim receipt");
        rewardFailure=ReloadGold(rewardFailure);ConquerItemTable.LoadItems(rewardFailure);PurseTown(rewardFailure);CollectPurse(rewardFailure);
        Check(!Fenced(rewardFailure) && PurseRows(rewardFailure,true)==1 && HasAchievement(rewardFailure,10228),"unpaid guild purse retries safely after a failed achievement write and reload");
        Query("DELETE FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10228","uid",(long)rewardFailure.Entity.UID);
        rewardFailure=ReloadGold(rewardFailure);rewardFailure.Entity.MyAchievement.Send();Check(HasAchievement(rewardFailure,10228) && PurseRows(rewardFailure,true)==1,"a preexisting paid guild purse restores missing achievement evidence without paying again");
        Call(GuildPurses,null,"SynchronizeHonor",reloaded,guildNow);Check(!reloaded.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.TopGuildLeader),"guild honor disappears at or after its fixed battle-relative expiry");

    }
}
