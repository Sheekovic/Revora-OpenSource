using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.Features;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static long CourierClaims(GameState p){return Scalar("SELECT count(*) FROM revora_courier_claims WHERE uid=@uid",p.Entity.UID);}
    static void CourierNpc(GameState p,byte option){ExplorationNpc(p,CourierLoopSystem.NpcId,option);}
    static void CourierReturn(GameState p){p.Entity.Teleport(1002,320,303);CourierNpc(p,0);}
    static GameState ReloadCourier(GameState p){p=ReloadBall(p);EntityVariableTable.Load(p);return p;}
    static GameState CourierPlayer(Map town,byte life=0,byte vip=4,byte days=0,byte level=70){
        var p=BallPlayer(town);p.Entity.Reborn=life;p.Entity.Level=level;p.Entity.Vitality=100;p.Entity.VIPLevel=vip;p.Entity.VIPDays=days;p.Entity.VIPLevelDays=days;
        p.Entity.X=320;p.Entity.Y=303;p.Variables=new VariableVault();
        Query("INSERT INTO variablevault(entityid,data) VALUES(@uid,@data)","uid",(long)p.Entity.UID,"data",p.Variables.Serialize());
        if(!EntityTable.SaveEntity(p))throw new Exception("Courier fixture save failed.");
        CourierNpc(p,1);Check(!Fenced(p) && p.Account.State==AccountTable.AccountState.Player && (uint)p[CourierLoopSystem.StageKey]==1,"ordinary player accepts Courier Loop");
        return p;
    }
    static void ReadyCourier(GameState p){p[CourierLoopSystem.BanditKey]=Config.CourierLoopBanditKills;p[CourierLoopSystem.StageKey]=CourierLoopSystem.StageTurnIn;EntityVariableTable.Save(p);}
    static GameState EarnCourier(GameState p){
        CourierNpc(p,3);ExplorationNpc(p,CityGuardSystem.CommanderNpcId,CourierLoopSystem.CommanderOption);
        Check(!Fenced(p) && (uint)p[CourierLoopSystem.StageKey]==CourierLoopSystem.StageHunt,"Commander receives the accepted satchel report");
        CourierReturn(p);CourierNpc(p,4);Check(p.Entity.MapID==1011,"Cerys sends the courier to Phoenix Castle");
        var family=MonsterInformation.MonsterInformations.Values.First(m=>m.Name=="Bandit");
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT count(*) FROM public.monsterspawns WHERE mapid=1011 AND npctype=@id",c)){
            q.Parameters.AddWithValue("id",(long)family.ID);Check(Convert.ToInt64(q.ExecuteScalar())>0,"installed Phoenix Castle Bandits support the actual Courier route");}}
        for(int n=0;n<Config.CourierLoopBanditKills;n++){
            var mob=HuntMonster(p,"Bandit");mob.Hitpoints=mob.MaxHitpoints=100;mob.IsDropped=true;mob.ClearFlagsMobs();uint damage=mob.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,mob,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=mob.UID},ref damage,null);CourierLoopSystem.OnMonsterKill(p,mob);
            if(Fenced(p))throw new Exception("Courier hunt fenced its owner.");
            if(n==9){EntityTable.SaveEntity(p);p=ReloadCourier(p);p.BindWorldMap(1011);p.Entity.X=130;p.Entity.Y=250;
                Check((uint)p[CourierLoopSystem.BanditKey]==10,"Courier partial hunt survives fresh character/variable reload");}
        }
        Check((uint)p[CourierLoopSystem.BanditKey]==Config.CourierLoopBanditKills && (uint)p[CourierLoopSystem.StageKey]==CourierLoopSystem.StageClerk,"owned Bandit deaths finish the hunt exactly once");
        CourierReturn(p);CourierNpc(p,5);ExplorationNpc(p,CpShopClerkSystem.NpcId,CourierLoopSystem.ClerkOption);
        Check((uint)p[CourierLoopSystem.StageKey]==CourierLoopSystem.StageTurnIn,"PurseClerk records the earned satchel");
        p=ReloadCourier(p);CourierReturn(p);Check((uint)p[CourierLoopSystem.StageKey]==CourierLoopSystem.StageTurnIn,"clerk approval survives reconnect before payment");return p;
    }
    static void VerifyCourierLoop(string runtime,Map town){
        VerifyFirstRebirth(runtime,town);Query("CREATE TABLE IF NOT EXISTS variablevault(entityid bigint PRIMARY KEY,data bytea NOT NULL)");World(runtime,1011);
        CourierLoopSystem.Create();Call(typeof(CourierLoopSystem),null,"InitializeRewards");CpShopClerkSystem.Create();
        CityGuardSystem.SpawnTalker(CityGuardSystem.CommanderNpcId,1002,CityGuardSystem.CommanderX,CityGuardSystem.CommanderY,50000,"City Commander");
        foreach(byte life in new byte[]{0,1,2}){
            var p=EarnCourier(CourierPlayer(town,life));if(!EntityTable.SaveEntity(p))throw new Exception("Courier preclaim save failed.");
            uint cp=p.Entity.ConquerPoints,rvp=p.Entity.RevoraPoint;ulong gold=p.Entity.Money;
            Parallel.For(0,12,i=>CourierNpc(p,2));var soul=p.Inventory.Objects.Single();
            Check(!Fenced(p) && CourierClaims(p)==1 && soul.Bound && ConquerItemInformation.BaseInformations[soul.ID].PurificationLevel==7
                && p.Entity.ConquerPoints==cp+Config.CourierLoopCp && p.Entity.RevoraPoint==rvp+Config.CourierLoopRevoraPoints && p.Entity.Money==gold+Config.CourierLoopGold
                && p.Entity.Level==Config.CourierLoopPuff && p.Entity.Experience==0 && p.Entity.VIPLevel==6 && p.Entity.VIPDays==7 && p.Entity.VIPLevelDays==7,
                "earned Courier life "+life+" atomically pays one bound soul, purse, puff and VIP under concurrent claims");
            ushort[] stats={p.Entity.Strength,p.Entity.Agility,p.Entity.Vitality,p.Entity.Spirit,p.Entity.Atributes};
            p=ReloadCourier(p);CourierReturn(p);CourierNpc(p,1);CourierNpc(p,2);
            Check(!Fenced(p) && CourierClaims(p)==1 && p.Inventory.Count==1 && p.Inventory.Objects.Single().UID==soul.UID && p.Inventory.Objects.Single().Bound
                && p.Entity.ConquerPoints==cp+Config.CourierLoopCp && p.Entity.RevoraPoint==rvp+Config.CourierLoopRevoraPoints && p.Entity.Money==gold+Config.CourierLoopGold
                && p.Entity.Level==Config.CourierLoopPuff && p.Entity.Experience==0 && p.Entity.VIPLevel==6 && p.Entity.VIPDays==7 && p.Entity.VIPLevelDays==7
                && new ushort[]{p.Entity.Strength,p.Entity.Agility,p.Entity.Vitality,p.Entity.Spirit,p.Entity.Atributes}.SequenceEqual(stats),
                "fresh item/character/variable reload preserves entire earned Courier reward without a postclaim save: life "+life);
            Check(EntityTable.SaveEntity(p),"autosave accepts committed Courier VIP and progression preimages");
        }
        var racing=CourierPlayer(town);ReadyCourier(racing);
        Parallel.For(0,12,i=>{if(i%3==0){if(!EntityTable.SaveEntity(racing))throw new Exception("Concurrent Courier autosave failed.");}else CourierNpc(racing,2);});
        racing=ReloadCourier(racing);
        Check(!Fenced(racing) && CourierClaims(racing)==1 && racing.Inventory.Objects.Count(i=>i.ID>=800000 && i.ID<900000)==1,
            "separate Courier race fixture keeps one reward under concurrent autosave and claim");
        foreach(string fault in new[]{"purse","progression","vip","soul","variables","receipt"}){
            var p=CourierPlayer(town);ReadyCourier(p);string table=fault=="variables"?"variablevault":fault=="receipt"?"revora_courier_claims":fault=="soul"?"items":"entities";
            string verb=table=="items" || fault=="receipt"?"INSERT":"UPDATE";
            string when=fault=="progression"?" WHEN(NEW.level<>OLD.level)":fault=="vip"?" WHEN(NEW.viplevel<>OLD.viplevel)":"";
            Query("CREATE FUNCTION suppress_courier() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_courier BEFORE "+verb+" ON "+table+" FOR EACH ROW"+when+" EXECUTE FUNCTION suppress_courier()");
            try{CourierNpc(p,2);}finally{Query("DROP TRIGGER suppress_courier ON "+table+";DROP FUNCTION suppress_courier()");}
            Check(Fenced(p) && CourierClaims(p)==0 && p.Inventory.Count==0 && p.Entity.VIPLevel==4 && p.Entity.VIPDays==0 && p.Entity.Level==70,"failed "+fault+" rolls back Courier reward including VIP");
            p=ReloadCourier(p);CourierReturn(p);Check(!Fenced(p) && p.Entity.VIPLevel==4 && p.Entity.VIPDays==0 && p.Entity.Level==70 && p.Entity.ConquerPoints==0
                && p.Entity.RevoraPoint==0 && p.Entity.Money==0 && (uint)p[CourierLoopSystem.StageKey]==CourierLoopSystem.StageTurnIn,"reload retains unpaid Courier after "+fault);
            CourierNpc(p,2);Check(!Fenced(p) && CourierClaims(p)==1,"Courier retries once after "+fault);
        }
        foreach(byte[] vip in new[]{new byte[]{6,0,0},new byte[]{7,12,12},new byte[]{6,30,7},new byte[]{5,30,7}}){
            var p=CourierPlayer(town,0,vip[0],vip[1],135);ReadyCourier(p);CourierNpc(p,2);p=ReloadCourier(p);
            Check(!Fenced(p) && CourierClaims(p)==1 && p.Entity.VIPLevel==Math.Max((byte)6,vip[0]) && p.Entity.VIPDays==vip[1] && p.Entity.VIPLevelDays==vip[2]
                && p.Entity.Level==135,"Courier retains lifetime/higher/longer VIP entitlement and high level: "+vip[0]+"/"+vip[1]);
        }
        foreach(string field in new[]{"conquerpoints","revorapoint","money","experience","viplevel","vipdays","vipleveldays"}){
            var p=CourierPlayer(town);ReadyCourier(p);Query("UPDATE entities SET "+field+"="+(field=="vipdays"?"(vipdays::integer+1)::text":field+"+1")+" WHERE uid=@uid","uid",(long)p.Entity.UID);CourierNpc(p,2);
            Check(Fenced(p) && CourierClaims(p)==0 && p.Inventory.Count==0,"Courier rejects stale "+field+" without overwriting durable state");
            p=ReloadCourier(p);CourierReturn(p);CourierNpc(p,2);Check(!Fenced(p) && CourierClaims(p)==1,"Courier accepts reloaded "+field+" preimage");
        }
        var full=CourierPlayer(town);ReadyCourier(full);for(int i=0;i<40;i++)Pack(full,1088001,1);CourierNpc(full,2);
        Check(!Fenced(full) && CourierClaims(full)==0,"full bag preserves unpaid Courier reward");DisRemove(full,full.Inventory.Objects.First());CourierNpc(full,2);
        Check(!Fenced(full) && CourierClaims(full)==1 && full.Inventory.Count==40,"one free slot is sufficient for the entire Courier reward");
        full[CourierLoopSystem.DoneKey]=0u;full[CourierLoopSystem.LifeKey]=1u;ReadyCourier(full);CourierNpc(full,2);full=ReloadCourier(full);
        Check(!Fenced(full) && CourierClaims(full)==1 && (uint)full[CourierLoopSystem.DoneKey]==1,"Courier receipt repairs stale completion durably without repayment");
        var old=CourierPlayer(town);ReadyCourier(old);old[CourierLoopSystem.DoneKey]=1u;EntityVariableTable.Save(old);CourierNpc(old,2);
        Check(!Fenced(old) && CourierClaims(old)==0,"legacy Courier completion does not receive a second reward");
        foreach(uint npc in new[]{CourierLoopSystem.NpcId,CityGuardSystem.CommanderNpcId,CpShopClerkSystem.NpcId}){
            var p=CourierPlayer(town);p.Entity.X=10;p.Entity.Y=10;uint stage=(uint)p[CourierLoopSystem.StageKey];
            ExplorationNpc(p,npc,npc==CourierLoopSystem.NpcId?(byte)2:(byte)10);
            Check(!Fenced(p) && CourierClaims(p)==0 && (uint)p[CourierLoopSystem.StageKey]==stage,"remote Courier NPC continuation cannot advance or pay: "+npc);
        }
        foreach(string field in new[]{"conquerpoints","revorapoint","money"}){
            var p=CourierPlayer(town);ReadyCourier(p);Query("UPDATE entities SET "+field+"=@value WHERE uid=@uid","value",field=="money"?long.MaxValue:(long)uint.MaxValue,"uid",(long)p.Entity.UID);
            p=ReloadCourier(p);CourierReturn(p);CourierNpc(p,2);Check(!Fenced(p) && CourierClaims(p)==0,"Courier refuses overflowing "+field+" without payment");
        }
        var nullable=CourierPlayer(town);ReadyCourier(nullable);Query("UPDATE entities SET viplevel=NULL,vipdays=NULL,vipleveldays=NULL WHERE uid=@uid","uid",(long)nullable.Entity.UID);
        nullable=ReloadCourier(nullable);CourierReturn(nullable);CourierNpc(nullable,2);
        Check(!Fenced(nullable) && CourierClaims(nullable)==1 && nullable.Entity.VIPLevel==6 && nullable.Entity.VIPDays==7,"nullable stored VIP supports login's minimum level without inventing a saved preimage");
        var pending=CourierPlayer(town,0,6,10);pending.Entity.VIPDays=9;ReadyCourier(pending);CourierNpc(pending,2);pending=ReloadCourier(pending);
        Check(!Fenced(pending) && CourierClaims(pending)==1 && pending.Entity.VIPDays==9,"pending VIP expiry remains distinct from its stored preimage during claim");
        var staleSave=CourierPlayer(town);Query("UPDATE entities SET vipdays=9 WHERE uid=@uid","uid",(long)staleSave.Entity.UID);
        Check(!EntityTable.SaveEntity(staleSave) && Fenced(staleSave) && Scalar("SELECT vipdays FROM entities WHERE uid=@uid",staleSave.Entity.UID)==9,"autosave cannot overwrite an external VIP update");
        var stages=CourierPlayer(town);CourierNpc(stages,2);CourierNpc(stages,5);ExplorationNpc(stages,CpShopClerkSystem.NpcId,10);
        Check(!Fenced(stages) && CourierClaims(stages)==0 && (uint)stages[CourierLoopSystem.StageKey]==CourierLoopSystem.StageAccept,"clerk and claim cannot skip the Commander and hunt");
        stages.BindWorldMap(1011);stages.Entity.X=130;stages.Entity.Y=250;CourierLoopSystem.OnMonsterKill(stages,HuntMonster(stages,"Bandit"));
        Check((uint)stages[CourierLoopSystem.BanditKey]==0,"Bandits killed before Commander acceptance do not count");
        CourierReturn(stages);CourierNpc(stages,3);ExplorationNpc(stages,CityGuardSystem.CommanderNpcId,10);CourierReturn(stages);CourierNpc(stages,4);
        foreach(string invalid in new[]{"alive","removed","other-map","guard","companion","far","wrong-owner","wrong-family"}){
            var mob=HuntMonster(stages,"Bandit");if(invalid=="alive")mob.Hitpoints=100;if(invalid=="removed")stages.Map.Entities.Remove(mob.UID);
            if(invalid=="other-map")mob.MapID=1015;if(invalid=="guard")mob.MonsterInfo.Guard=true;if(invalid=="companion")mob.Companion=true;
            if(invalid=="far")mob.X=(ushort)(stages.Entity.X+40);if(invalid=="wrong-owner")mob.MonsterInfo.Owner=null;if(invalid=="wrong-family")mob.MonsterInfo.ID=uint.MaxValue;
            CourierLoopSystem.OnMonsterKill(stages,mob);Check(!Fenced(stages) && (uint)stages[CourierLoopSystem.BanditKey]==0,"Courier rejects invalid hunt source: "+invalid);
        }
        var corpse=HuntMonster(stages,"Bandit");CourierLoopSystem.OnMonsterKill(stages,corpse);CourierLoopSystem.OnMonsterKill(stages,corpse);
        Check((uint)stages[CourierLoopSystem.BanditKey]==1,"Courier corpse replay cannot inflate the hunt");
        corpse.Hitpoints=corpse.MaxHitpoints=100;corpse.ClearFlagsMobs();Call(typeof(CourierLoopSystem),null,"Revived",corpse);corpse.IsDropped=true;corpse.Die(stages.Entity);
        Check(!Fenced(stages) && (uint)stages[CourierLoopSystem.BanditKey]==2,"Courier counts the monster's next spawned life");
        CourierLoopSystem.OnReborn(stages);EntityVariableTable.Load(stages);
        Check((uint)stages[CourierLoopSystem.LifeKey]==0 && (uint)stages[CourierLoopSystem.StageKey]==0 && (uint)stages[CourierLoopSystem.BanditKey]==0,"Courier rebirth reset persists all unfinished progress together");
        var levelWrite=CourierPlayer(town);Query("UPDATE entities SET vipdays='11' WHERE uid=@uid","uid",(long)levelWrite.Entity.UID);
        bool refused=false;try{levelWrite.Entity.VIPLevel=5;}catch(InvalidOperationException){refused=true;}
        Check(refused && Fenced(levelWrite) && levelWrite.Entity.VIPLevel==4 && Scalar("SELECT viplevel FROM entities WHERE uid=@uid",levelWrite.Entity.UID)==4,
            "immediate VIP level writer refuses a stale entitlement before publishing a new level");
    }
}
