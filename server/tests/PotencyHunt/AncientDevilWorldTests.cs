using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.Attacking;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type AncientWorld=typeof(Entity).Assembly.GetType("Revora.Game.Features.AncientDevilWorld");
    static DateTime ancientNow=new DateTime(2033,1,1,0,0,0,DateTimeKind.Utc);
    static object AncientStart(GameState p) {lock(p.ItemSyncRoot)return Call(AncientWorld,null,"Start",p,300011u);}
    static void AncientLeave(object h) {var p=(GameState)AbyssField(h,"Player");lock(p.ItemSyncRoot)Call(AncientWorld,null,"Leave",h);}
    static Entity AncientBody(object h) {return (Entity)AbyssField(h,"Body");}
    static GameState AncientHunter(Map town,Map island) {
        var p=HousePlayer(town);p.Entity.Class=25;AncientAt(p,island);return p;
    }
    static uint AncientHit(Entity attacker,Entity target,uint amount) {
        var attack=new Attack(true){AttackType=Attack.Melee,Attacker=attacker.UID,Attacked=target.UID};
        Handle.ReceiveAttack(attacker,target,attack,ref amount,null);return amount;
    }
    static void VerifyAncientWorld(string runtime,Map town) {
        InitializeHousingFixture(runtime,town);
        var terrain=World(runtime,1082);DMaps.MapPaths[1082]="maps\\1082.map";
        var island=AncientMap(1052,terrain);
        island.AddNpc(new NpcSpawn{UID=300011,MapID=1052,X=229,Y=233,Mesh=2177,Type=Enums.NpcType.Talker});
        AncientWorld.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>ancientNow));
        Call(AncientWorld,null,"Initialize");
        var p=AncientHunter(town,island);var h=AncientStart(p);var body=AncientBody(h);ushort map=p.Entity.MapID;
        Check(map>=11000 && map<60000 && p.Map.BaseID==1082 && p.Map.Entities.Count==1 && p.Map.Npcs.Count==1 && p.Map.Npcs.ContainsKey(300016),
            "ordinary trial creates an owned island copy with a return guide and one class devil");
        Check(Scalar("SELECT mapid FROM entities WHERE uid=@uid",p.Entity.UID)==map,"trial travel persists private map identity");
        Check(body.MonsterInfo.Mesh==205 && body.MaxHitpoints==23588 && body.MinAttack==350 && body.MaxAttack==550
            && body.Defence==100 && Handle.CanAttack(p.Entity,body,null,true),"reviewed model and explicit combat values are attackable by owner");
        var outsider=AncientHunter(town,island);outsider.Entity.Teleport((ushort)1082,map,(ushort)216,(ushort)208);
        Check(outsider.Entity.MapID==1052,"outsider teleport cannot enter an owned trial");
        uint health=body.Hitpoints;
        Check(AncientHit(outsider.Entity,body,health)==0 && body.Hitpoints==health,"direct foreign damage is blocked before HP mutation");
        body.Die(outsider.Entity);Check(!body.Dead && AncientState(p,"hunting")==1,"foreign death callback cannot create entitlement");
        var copy=new Entity(EntityFlag.Monster,false){UID=body.UID,MapID=body.MapID,MonsterInfo=body.MonsterInfo.Copy(),Hitpoints=body.Hitpoints};
        copy.MonsterInfo.Owner=copy;copy.Die(p.Entity);
        Check(!body.Dead && AncientState(p,"hunting")==1,"counterfeit same UID and family has no encounter authority");
        p.Trade.InTrade=true;Check(AncientHit(p.Entity,body,health)==0,"trade suspends direct damage");p.Trade.InTrade=false;
        using(var ready=new ManualResetEventSlim())using(var release=new ManualResetEventSlim()) {
            var held=Task.Run(()=>{lock(p.ItemSyncRoot){ready.Set();release.Wait();}});ready.Wait();
            try{Check(AncientHit(p.Entity,body,health)==0 && !body.Dead,"busy owner lock defers damage without blocking");}
            finally{release.Set();held.Wait();}
        }
        uint partial=AncientHit(p.Entity,body,100);
        Check(partial>0 && body.Hitpoints==health-partial,"ordinary damage pipeline subtracts accepted partial hit");
        AncientHit(p.Entity,body,body.Hitpoints);
        Check(!Fenced(p) && body.Dead && AncientState(p,"victory")==1 && p.Map.FloorItems.Count==0,
            "lethal combat records pending certificate and suppresses unrelated legacy loot");
        body.Die(p.Entity);Check(AncientState(p,"victory")==1,"repeated corpse callback adds no victory");
        Call(AncientWorld,null,"Tick");
        Check(p.Entity.MapID==1052 && !Kernel.Maps.ContainsKey(map),"victory cleanup returns owner and releases private map");
        AncientAt(p,island);Guid id=AbyssId(AbyssField(h,"Run"));
        var cert=(ConquerItem)AncientWrite(p,"Claim",id);AncientApply(p,cert);
        var amulet=(ConquerItem)AncientWrite(p,"Exchange",id,cert.UID);AncientApply(p,amulet,cert.UID);
        p=ReloadBall(p);Check(!Fenced(p) && p.Inventory.Objects.Single(i=>i.ID==710011).UID==amulet.UID,
            "actual trial damage earns certificate and transferable amulet through production reload");
        foreach(string failure in new[]{"leave","timeout","disconnect","replacement","class-change","death"}) {
            var hunter=AncientHunter(town,island);var run=AncientStart(hunter);var mob=AncientBody(run);ushort privateMap=hunter.Entity.MapID;
            if(failure=="leave")AncientLeave(run);
            if(failure=="timeout")ancientNow=ancientNow.AddMinutes(21);
            if(failure=="disconnect")Kernel.GamePool.Remove(hunter.Entity.UID);
            if(failure=="replacement")ReloadBall(hunter);
            if(failure=="class-change")hunter.Entity.Class=15;
            if(failure=="death")hunter.Entity.Hitpoints=0;
            Call(AncientWorld,null,"Tick");
            Check(!Kernel.Maps.ContainsKey(privateMap) && AncientState(hunter,"abandoned")==1
                && Scalar("SELECT mapid FROM entities WHERE uid=@uid",hunter.Entity.UID)==1052,"unfinished trial cleanup: "+failure);
            if(failure=="replacement")Check(Kernel.GamePool[hunter.Entity.UID].Entity.MapID!=privateMap && Kernel.Maps.ContainsKey(Kernel.GamePool[hunter.Entity.UID].Entity.MapID),"production replacement session retains a valid public map when old trial is disposed");
            mob.Die(hunter.Entity);Check(AncientState(hunter,"victory")==0,"released life cannot award late victory: "+failure);
        }
        var retry=AncientHunter(town,island);var pending=AncientStart(retry);var enemy=AncientBody(pending);
        Query("CREATE FUNCTION suppress_ancient_world() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_ancient_world BEFORE UPDATE ON revora_ancient_trials FOR EACH ROW EXECUTE FUNCTION suppress_ancient_world()");
        try { enemy.Die(retry.Entity);Check(Fenced(retry) && !enemy.Dead && AncientState(retry,"hunting")==1,"failed death receipt fences player without killing unpaid encounter"); }
        finally {Query("DROP TRIGGER suppress_ancient_world ON revora_ancient_trials;DROP FUNCTION suppress_ancient_world()");}
        Call(AncientWorld,null,"Tick");Check(AncientState(retry,"abandoned")==1,"fenced failed encounter can be cleaned up without awarding");
        var failedEntry=AncientHunter(town,island);int mapsBefore=Kernel.Maps.Count;
        Query("CREATE FUNCTION suppress_ancient_bind() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_ancient_bind BEFORE INSERT ON revora_ancient_lives FOR EACH ROW EXECUTE FUNCTION suppress_ancient_bind()");
        try{Check(AbyssRejected(()=>AncientStart(failedEntry)),"failed encounter bind rejects admission");}
        finally{Query("DROP TRIGGER suppress_ancient_bind ON revora_ancient_lives;DROP FUNCTION suppress_ancient_bind()");}
        Check(Kernel.Maps.Count==mapsBefore && AncientState(failedEntry,"hunting")==0,"failed bind rolls back trial and releases unpublished scene");
        var failedTravel=AncientHunter(town,island);string path=DMaps.MapPaths[1082];DMaps.MapPaths.Remove(1082);
        try{Check(AbyssRejected(()=>AncientStart(failedTravel)),"failed teleport rejects trial entry");}
        finally{DMaps.MapPaths[1082]=path;}
        Check(failedTravel.Entity.MapID==1052 && AncientState(failedTravel,"abandoned")==1 && Kernel.Maps.Count==mapsBefore,
            "failed post-commit travel abandons the paid life and releases its scene");
        var clean=AncientHunter(town,island);AncientStart(clean);ushort cleanupMap=clean.Entity.MapID;
        ancientNow=ancientNow.AddMinutes(21);
        Query("CREATE FUNCTION suppress_ancient_cleanup() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_ancient_cleanup BEFORE UPDATE ON revora_ancient_trials FOR EACH ROW EXECUTE FUNCTION suppress_ancient_cleanup()");
        try{Call(AncientWorld,null,"Tick");Check(Fenced(clean) && Kernel.Maps.ContainsKey(cleanupMap) && AncientState(clean,"hunting")==1,
            "failed abandonment retains scene for cleanup retry instead of losing ownership");}
        finally{Query("DROP TRIGGER suppress_ancient_cleanup ON revora_ancient_trials;DROP FUNCTION suppress_ancient_cleanup()");}
        Call(AncientWorld,null,"Tick");Check(!Kernel.Maps.ContainsKey(cleanupMap) && AncientState(clean,"abandoned")==1,
            "cleanup retries successfully after failed write is removed");
        foreach(bool won in new[]{false,true}) {
            var crash=AncientHunter(town,island);var live=AncientStart(crash);var target=AncientBody(live);ushort privateMap=crash.Entity.MapID;
            if(won)AncientHit(crash.Entity,target,target.Hitpoints);
            Check(!Fenced(crash),"crash fixture has a valid saved trial");
            Kernel.GamePool.Remove(crash.Entity.UID);
            // Simulated process-memory loss: deliberately do not call Leave/Cancel.
            var registry=AncientWorld.GetField("Hunts",Hidden).GetValue(null);registry.GetType().GetMethod("Clear").Invoke(registry,null);
            Kernel.Maps[privateMap].Dispose();MapsTable.MapInformations.Remove(privateMap);
            Call(AncientWorld,null,"Initialize");
            Check(AncientState(crash,won?"victory":"abandoned")==1 && Scalar("SELECT mapid FROM entities WHERE uid=@uid",crash.Entity.UID)==1052,
                "startup recovery returns stale private location and preserves earned state: "+won);
            crash=ReloadBall(crash);AncientAt(crash,island);
            if(won) {
                var certificate=(ConquerItem)AncientWrite(crash,"Claim",AbyssId(AbyssField(live,"Run")));AncientApply(crash,certificate);
                Check(certificate!=null && certificate.ID==710016,"won certificate remains claimable after memory-loss recovery");
            } else {
                var restarted=AncientStart(crash);Check(AncientBody(restarted).MapID!=privateMap,"unfinished crash recovery permits a fresh private trial");AncientLeave(restarted);
            }
        }

    }
}
