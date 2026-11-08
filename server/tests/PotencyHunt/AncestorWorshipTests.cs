using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type Worship=typeof(Entity).Assembly.GetType("Revora.Game.Features.AncestorWorship");
    static DateTime worshipNow=new DateTime(2034,6,15,19,0,0,DateTimeKind.Utc);
    static long WorshipClaims(GameState p){return Scalar("SELECT count(*) FROM revora_worship_claims WHERE uid=@uid AND NOT legacy",p.Entity.UID);}
    static GameState WorshipPlayer(Map town,byte level=135){
        var p=BallPlayer(town);p.Entity.Level=level;p.Entity.Vitality=100;p.LastResetTime=worshipNow.ToLocalTime();p.Entity.X=319;p.Entity.Y=307;
        DialyQuestTable.Load(p);if(!EntityTable.SaveEntity(p))throw new Exception("Worship profile setup failed.");return p;
    }
    static void WorshipTravel(GameState p){p.Entity.Teleport(1002,319,307);ExplorationNpc(p,79882,1);}
    static void WorshipNpc(GameState p,uint statue=98130,byte option=1){ExplorationNpc(p,statue,option);}
    static void WorshipWalk(GameState p,uint statue){
        var map=p.Map;var npc=map.Npcs[statue];int w=map.Floor.Bounds.Width,h=map.Floor.Bounds.Height;
        var seen=new System.Collections.Generic.HashSet<int>();var pending=new System.Collections.Generic.Queue<int>();
        int start=p.Entity.X+p.Entity.Y*w;seen.Add(start);pending.Enqueue(start);
        while(pending.Count>0){int at=pending.Dequeue(),x=at%w,y=at/w;
            if(Kernel.GetDistance((ushort)x,(ushort)y,npc.X,npc.Y)<=6){AdventureWalk(p,(ushort)x,(ushort)y);return;}
            for(int dx=-1;dx<=1;dx++)for(int dy=-1;dy<=1;dy++){
                int nx=x+dx,ny=y+dy;if(nx<0 || ny<0 || nx>=w || ny>=h || !map.Floor[(ushort)nx,(ushort)ny,MapObjectType.Player])continue;
                int next=nx+ny*w;if(seen.Add(next))pending.Enqueue(next);
            }
        }
        throw new Exception("No reachable interaction tile for installed worship statue "+statue);
    }
    static GameState ReloadWorship(GameState p){p=ReloadBall(p);DialyQuestTable.Load(p);Call(Worship,null,"Refresh",p);return p;}
    static void VerifyAncestorWorship(string runtime,Map town){
        VerifyFirstRebirth(runtime,town);Call(Worship,null,"Initialize");Call(Worship,null,"Initialize");
        Worship.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>worshipNow));
        MiningNpc(town,79882);var hall=World(runtime,3081);
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT id,mapid,cellx,celly,lookface FROM public.npcs WHERE id BETWEEN 98130 AND 98135 ORDER BY id",c))
        using(var r=q.ExecuteReader())while(r.Read()){
            Check(Convert.ToUInt16(r[1])==3081,"installed worship statue belongs to the Ancestral Hall");
            hall.AddNpc(new NpcSpawn{UID=Convert.ToUInt32(r[0]),MapID=3081,X=Convert.ToUInt16(r[2]),Y=Convert.ToUInt16(r[3]),Mesh=Convert.ToUInt16(r[4]),Type=Enums.NpcType.Talker});
        }}
        Check(hall.Npcs.Count==6,"all six installed ancestral statues are present");
        foreach(uint statue in hall.Npcs.Keys.OrderBy(id=>id)){
            var p=WorshipPlayer(town);ulong award=p.ExpBall;uint cp=p.Entity.ConquerPoints,rvp=p.Entity.RevoraPoint;ulong before=p.Entity.Experience;
            WorshipTravel(p);Check(!Fenced(p) && p.Entity.MapID==3081 && p.Account.State==AccountTable.AccountState.Player,"ordinary Envoy passage reaches the installed Ancestral Hall");
            WorshipWalk(p,statue);Parallel.For(0,8,i=>WorshipNpc(p,statue));
            var chi=p.Inventory.Objects.Single(i=>i.ID==Config.DailyQuestChiToken);
            var rune=p.Inventory.Objects.Single(i=>i.ID==4030101);
            Check(!Fenced(p) && WorshipClaims(p)==1 && !chi.Bound && rune.Bound && Scalar("SELECT rune_uid FROM revora_worship_claims WHERE uid=@uid",p.Entity.UID)==rune.UID && !p.Inventory.Objects.Any(i=>i.ID>=800000 && i.ID<900000)
                && p.Entity.ConquerPoints==cp+Config.DailyWorshipCp && p.Entity.RevoraPoint==rvp+Config.DailyWorshipRevoraPoints && p.Entity.HoringThe
                && (p.Entity.Experience!=before || p.Entity.Level!=135) && Scalar("SELECT experience FROM revora_worship_claims WHERE uid=@uid AND NOT legacy",p.Entity.UID)==(long)award,
                "earned statue "+statue+" grants the bound yellow rune, Chi Token, purse and EXP once under concurrent claims");
            byte level=p.Entity.Level;ulong experience=p.Entity.Experience;
            p=ReloadWorship(p);p.Entity.Teleport(3081,56,56);WorshipWalk(p,statue);WorshipNpc(p,statue);
            Check(!Fenced(p) && WorshipClaims(p)==1 && p.Inventory.Count==2 && p.Inventory.ContainsUID(chi.UID) && p.Inventory.Objects.Any(i=>i.UID==rune.UID && i.Bound)
                && p.Entity.Level==level && p.Entity.Experience==experience
                && p.Entity.ConquerPoints==cp+Config.DailyWorshipCp && p.Entity.RevoraPoint==rvp+Config.DailyWorshipRevoraPoints && p.Entity.HoringThe,
                "fresh item/character/daily reload retains the entire worship reward without a postclaim save");
            p.Entity.HoringThe=false;DialyQuestTable.Reset(p);WorshipNpc(p,statue);
            Check(WorshipClaims(p)==1 && p.Entity.HoringThe,"legacy daily reset cannot reopen a paid worship receipt");
        }
        foreach(string fault in new[]{"purse","profile","chi","rune","daily","receipt"}){
            var p=WorshipPlayer(town);WorshipTravel(p);WorshipWalk(p,98130);byte level=p.Entity.Level;ulong exp=p.Entity.Experience;
            string table=fault=="daily"?"dialyquests":fault=="receipt"?"revora_worship_claims":fault=="rune" || fault=="chi"?"items":"entities";
            string verb=table=="items" || fault=="receipt"?"INSERT":"UPDATE";
            string when=fault=="purse"?" WHEN(NEW.conquerpoints<>OLD.conquerpoints)":fault=="profile"?" WHEN(NEW.experience<>OLD.experience)":fault=="rune"?" WHEN(NEW.id=4030101)":fault=="chi"?" WHEN(NEW.id="+Config.DailyQuestChiToken+")":"";
            Query("CREATE FUNCTION suppress_worship() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_worship BEFORE "+verb+" ON "+table+" FOR EACH ROW"+when+" EXECUTE FUNCTION suppress_worship()");
            try{WorshipNpc(p);}finally{Query("DROP TRIGGER suppress_worship ON "+table+";DROP FUNCTION suppress_worship()");}
            Check(Fenced(p) && WorshipClaims(p)==0 && p.Inventory.Count==0 && !p.Entity.HoringThe,"suppressed "+fault+" rolls back all worship rewards");
            p=ReloadWorship(p);Check(!Fenced(p) && WorshipClaims(p)==0 && p.Inventory.Count==0 && p.Entity.ConquerPoints==0 && p.Entity.RevoraPoint==0
                && p.Entity.Level==level && p.Entity.Experience==exp && !p.Entity.HoringThe,"fresh reload retains unpaid worship after "+fault);
            WorshipTravel(p);WorshipWalk(p,98130);WorshipNpc(p);Check(!Fenced(p) && WorshipClaims(p)==1,"worship retries once after "+fault);
        }
        var rollover=WorshipPlayer(town);WorshipTravel(rollover);WorshipWalk(rollover,98130);WorshipNpc(rollover);
        var day=worshipNow;worshipNow=worshipNow.AddDays(1);Call(Worship,null,"Refresh",rollover);WorshipNpc(rollover);
        Check(!Fenced(rollover) && WorshipClaims(rollover)==2 && rollover.Inventory.Count==4,"next worship day earns a separate daily reward");
        worshipNow=day;WorshipNpc(rollover);Check(WorshipClaims(rollover)==2,"clock rollback cannot reopen an earlier worship day");
        var legacy=WorshipPlayer(town);legacy.Entity.HoringThe=true;DialyQuestTable.Save(legacy);Call(Worship,null,"Refresh",legacy);
        Check(!Fenced(legacy) && legacy.Entity.HoringThe && WorshipClaims(legacy)==0 && Scalar("SELECT count(*) FROM revora_worship_claims WHERE uid=@uid AND legacy",legacy.Entity.UID)==1,"persisted paid legacy day imports without retroactive gifts");
        worshipNow=day.AddDays(1);WorshipTravel(legacy);WorshipWalk(legacy,98130);WorshipNpc(legacy);
        Check(!Fenced(legacy) && WorshipClaims(legacy)==1,"legacy paid day can earn the next day's valuable reward");worshipNow=day;
        var full=WorshipPlayer(town);for(int i=0;i<40;i++)Pack(full,1088001,1);WorshipTravel(full);WorshipWalk(full,98130);WorshipNpc(full);
        Check(!Fenced(full) && WorshipClaims(full)==0 && !full.Entity.HoringThe,"full bag cannot partially pay worship");DisRemove(full,full.Inventory.Objects.First());WorshipNpc(full);
        Check(!Fenced(full) && WorshipClaims(full)==0 && full.Inventory.Count==39,"one free slot cannot partially pay the rune and Chi Token");
        DisRemove(full,full.Inventory.Objects.First());WorshipNpc(full);
        Check(!Fenced(full) && WorshipClaims(full)==1 && full.Inventory.Count==40,"two free slots admit the complete worship reward");
        foreach(string field in new[]{"conquerpoints","revorapoint","experience"}){
            var p=WorshipPlayer(town);WorshipTravel(p);WorshipWalk(p,98130);Query("UPDATE entities SET "+field+"="+field+"+1 WHERE uid=@uid","uid",(long)p.Entity.UID);WorshipNpc(p);
            Check(Fenced(p) && WorshipClaims(p)==0 && p.Inventory.Count==0,"worship refuses stale "+field+" without overwriting its durable value");
        }
        foreach(string field in new[]{"conquerpoints","revorapoint"}){
            var p=WorshipPlayer(town);Query("UPDATE entities SET "+field+"=@value WHERE uid=@uid","value",(long)uint.MaxValue,"uid",(long)p.Entity.UID);p=ReloadWorship(p);WorshipTravel(p);WorshipWalk(p,98130);WorshipNpc(p);
            Check(!Fenced(p) && WorshipClaims(p)==0 && !p.Entity.HoringThe,"worship refuses overflowing "+field);
        }
        var young=WorshipPlayer(town,49);WorshipTravel(young);Check(young.Entity.MapID==1002,"Envoy refuses under-level worship travel");young.Entity.Teleport(3081,56,56);WorshipWalk(young,98130);WorshipNpc(young);
        Check(!Fenced(young) && WorshipClaims(young)==0,"direct statue request also enforces worship level");
        var remote=WorshipPlayer(town);remote.Entity.X=10;remote.Entity.Y=10;ExplorationNpc(remote,79882,1);Check(remote.Entity.MapID==1002,"remote Envoy continuation cannot teleport to worship");
        remote.Entity.Teleport(3081,56,56);remote.Entity.X=1;remote.Entity.Y=1;WorshipNpc(remote);Check(!Fenced(remote) && WorshipClaims(remote)==0,"remote statue continuation cannot grant worship rewards");
        var capped=WorshipPlayer(town,Config.LevelCap);WorshipTravel(capped);WorshipWalk(capped,98130);WorshipNpc(capped);
        Check(!Fenced(capped) && WorshipClaims(capped)==1 && capped.Entity.Level==Config.LevelCap && Scalar("SELECT experience FROM revora_worship_claims WHERE uid=@uid",capped.Entity.UID)==0,"level-cap player receives valuable gifts without invented EXP");
        Call(Worship,null,"Initialize");Check(WorshipClaims(capped)==1,"reinitializing worship schema preserves paid receipts");
        var race=WorshipPlayer(town);WorshipTravel(race);WorshipWalk(race,98130);
        Parallel.For(0,12,i=>{if(i%3==0){if(!EntityTable.SaveEntity(race))throw new Exception("Worship autosave failed.");DialyQuestTable.Save(race);}else WorshipNpc(race);});
        race=ReloadWorship(race);Check(!Fenced(race) && WorshipClaims(race)==1 && race.Inventory.Count==2 && race.Entity.HoringThe,"separate autosave race retains one complete worship claim");
        var boundary=TimeZoneInfo.ConvertTimeToUtc(new DateTime(2034,6,16),TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"));
        worshipNow=boundary.AddTicks(-1);var midnight=WorshipPlayer(town);WorshipTravel(midnight);WorshipWalk(midnight,98130);WorshipNpc(midnight);
        worshipNow=boundary;WorshipNpc(midnight);Check(!Fenced(midnight) && WorshipClaims(midnight)==2,"Pacific midnight opens exactly the next worship day");worshipNow=day;
        var past=WorshipPlayer(town);past.LastResetTime=worshipNow.ToLocalTime().AddDays(-1);past.Entity.HoringThe=true;EntityTable.SaveEntity(past);DialyQuestTable.Save(past);
        WorshipTravel(past);WorshipWalk(past,98130);WorshipNpc(past);Check(!Fenced(past) && WorshipClaims(past)==1,"previous legacy reset date cannot suppress today's worship");
        var bag=WorshipPlayer(town);WorshipTravel(bag);WorshipWalk(bag,98130);var missing=Pack(bag,1088001,1);bag.Inventory.Remove(missing.UID,Enums.ItemUse.None,true);
        Check(SavedRune(missing,"id")==1088001 && bag.Inventory.Count==0,"stale worship bag fixture retains an unseen durable item");
        WorshipNpc(bag);Check(Fenced(bag) && WorshipClaims(bag)==0 && SavedRune(missing,"id")==1088001,"worship refuses stale bag ownership without minting gifts");
        foreach(bool reject in new[]{false,true}){
            var p=ArenaEntryPlayer(town,false);p.LastResetTime=worshipNow.ToLocalTime();DialyQuestTable.Load(p);EntityTable.SaveEntity(p);
            WorshipTravel(p);WorshipWalk(p,98130);
            if(reject)Query("CREATE FUNCTION suppress_worship_arena() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_worship_arena BEFORE INSERT ON arena FOR EACH ROW EXECUTE FUNCTION suppress_worship_arena()");
            try{WorshipNpc(p);}finally{if(reject)Query("DROP TRIGGER suppress_worship_arena ON arena;DROP FUNCTION suppress_worship_arena()");}
            if(reject){
                Check(Fenced(p) && WorshipClaims(p)==0 && p.Inventory.Count==0 && p.Entity.Level==69 && SkillRows(p)==0,"late Arena admission failure rolls back worship items, purse, level and class skill");
                p=ReloadWorship(p);WorshipTravel(p);WorshipWalk(p,98130);WorshipNpc(p);
            }
            Check(!Fenced(p) && WorshipClaims(p)==1 && p.Entity.Level>=70 && p.Spells.ContainsKey(1110) && p.Spells[1110].Available
                && Scalar("SELECT arenapoints FROM arena WHERE entityid=@uid",p.Entity.UID)==1000,"worship EXP commits the level-70 class skill and Arena admission");
            byte level=p.Entity.Level;ulong exp=p.Entity.Experience;p=ReloadWorship(p);
            Check(!Fenced(p) && p.Entity.Level==level && p.Entity.Experience==exp && p.Spells.ContainsKey(1110) && WorshipClaims(p)==1,"worship level effects survive fresh reload without backfilling a save");
        }
    }
}
