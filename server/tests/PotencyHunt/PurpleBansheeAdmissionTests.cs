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
    static readonly Type PurpleAdmission=typeof(Entity).Assembly.GetType("Revora.Game.Features.PurpleBansheeAdmission");
    static object PurpleAdmit(GameState p,ConquerItem soul,Func<bool> context=null) {
        ushort x=0,y=0;bool found=false;
        for(int dx=-10;dx<=10 && !found;dx++)for(int dy=-10;dy<=10 && !found;dy++) {
            int tx=p.Entity.X+dx,ty=p.Entity.Y+dy;
            if(tx<0 || ty<0 || tx>ushort.MaxValue || ty>ushort.MaxValue)continue;
            if((bool)Call(PurpleAdmission,null,"Cell",p.Map,(ushort)tx,(ushort)ty)){x=(ushort)tx;y=(ushort)ty;found=true;}
        }
        if(!found)throw new Exception("Purple fixture has no summon cell.");
        lock(p.ItemSyncRoot)return Call(PurpleAdmission,null,"Admit",p,soul,p.Map,x,y,context??new Func<bool>(()=>true));
    }
    static void VerifyPurpleAdmissions(string runtime,Map town) {
        InitializeHousingFixture(runtime,town);Call(SwordAdmission,null,"Initialize");
        Call(PurpleAdmission,null,"Initialize");Call(PurpleAdmission,null,"Initialize");
        var now=new DateTime(2038,1,4,20,0,0,DateTimeKind.Utc);
        PurpleAdmission.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>now));
        try {
            var p=SwordHousePlayer(town);var soul=Pack(p,3004259,3);soul.Bound=true;
            Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)soul.UID);
            Check(PurpleAdmit(p,soul,()=>false)==null && soul.StackSize==3,
                "Purple Banshee admission requires the coordinator's live authorization");
            p.Entity.Level=99;EntityTable.SaveEntity(p);
            soul.Lock=1;Check(PurpleAdmit(p,soul)==null,"locked Banshee Spirit cannot authorize a summon");soul.Lock=0;
            object won=null;int admitted=0;
            Parallel.For(0,8,n=>{var run=PurpleAdmit(p,soul);if(run!=null){System.Threading.Interlocked.Increment(ref admitted);won=run;}});
            Guid id=(Guid)Field(won,"Id");
            Check(admitted==1 && soul.StackSize==2 && soul.Bound
                && Scalar("SELECT count(*) FROM revora_purple_banshee_runs WHERE uid=@uid AND state='active' AND before_stack=3 AND bound",p.Entity.UID)==1
                && ConquerItemTable.LoadItem(soul.UID).StackSize==2,
                "concurrent house admissions commit one daily attempt and consume one exact bound stack unit");
            Check(Scalar("SELECT map FROM revora_purple_banshee_runs WHERE uid=@uid",p.Entity.UID)==p.Entity.MapID
                && Scalar("SELECT terrain FROM revora_purple_banshee_runs WHERE uid=@uid",p.Entity.UID)==p.Map.BaseID
                && ((uint[])Field(won,"Party")).SequenceEqual(new[]{p.Entity.UID}),
                "admission freezes the registered house identity, terrain and owner party");
            p=ReloadBall(p);soul=p.Inventory.Objects.Single(i=>i.ID==3004259);Call(PurpleAdmission,null,"Initialize");
            Check(PurpleAdmit(p,soul)==null && soul.StackSize==2,"reload and schema initialization cannot reopen an active daily attempt");
            Check(!(bool)Call(PurpleAdmission,null,"Fail",p.Entity.UID+1,id)
                && (bool)Call(PurpleAdmission,null,"Fail",p.Entity.UID,id) && !(bool)Call(PurpleAdmission,null,"Fail",p.Entity.UID,id),
                "attempt failure requires exact owner/run and cannot close twice");
            Check(PurpleAdmit(p,soul)==null,"failed hunt still consumes today's attempt");
            now=now.AddDays(-1);Check(PurpleAdmit(p,soul)==null,"clock rollback cannot reopen Purple Banshee admission");
            now=now.AddDays(2);Check(PurpleAdmit(p,soul)!=null && soul.StackSize==1,"next Pacific day permits a fresh attempt after the previous failure");
            var outsider=HousePlayer(town);var foreign=Pack(outsider,3004259,1);
            outsider.Entity.MapID=p.Entity.MapID;outsider.Entity.X=p.Entity.X;outsider.Entity.Y=p.Entity.Y;
            Check(PurpleAdmit(outsider,foreign)==null && foreign.StackSize==1,"foreign house metadata cannot authorize another owner's Banshee Spirit");
            outsider.Entity.MapID=1002;
            foreach(string fault in new[]{"receipt","consume","context"}) {
                var f=SwordHousePlayer(town);var token=Pack(f,3004259,1);bool rejected=false;
                string table=fault=="receipt"?"revora_purple_banshee_runs":"items";
                string verb=fault=="receipt"?"INSERT":"DELETE";
                if(fault!="context")Query("CREATE FUNCTION suppress_purple_admission() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_purple_admission BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_purple_admission()");
                int calls=0;
                try{PurpleAdmit(f,token,fault=="context"?new Func<bool>(()=>++calls==1):null);}catch(InvalidOperationException){rejected=true;}
                finally{if(fault!="context")Query("DROP TRIGGER suppress_purple_admission ON "+table+";DROP FUNCTION suppress_purple_admission()");}
                Check(rejected && Scalar("SELECT count(*) FROM revora_purple_banshee_runs WHERE uid=@uid",f.Entity.UID)==0
                    && ConquerItemTable.LoadItem(token.UID).ID==3004259 && f.Inventory.Objects.Any(i=>i.UID==token.UID),
                    "failed Purple admission rolls back exact item and daily reservation: "+fault);
                Check(PurpleAdmit(f,token)!=null && !f.Inventory.Objects.Any(i=>i.UID==token.UID),"single Banshee Spirit can be admitted once after rollback: "+fault);
            }
            var rollover=SwordHousePlayer(town);var daily=Pack(rollover,3004259,1);int passes=0;
            Check(PurpleAdmit(rollover,daily,()=>{if(++passes==2)now=now.AddDays(1);return true;})==null
                && rollover.Inventory.ContainsUID(daily.UID) && ConquerItemTable.LoadItem(daily.UID).ID==3004259
                && Scalar("SELECT count(*) FROM revora_purple_banshee_runs WHERE uid=@uid",rollover.Entity.UID)==0,
                "daily rollover before commit rolls back admission and the exact item");
            var party=SwordHousePlayer(town);var mate=HousePlayer(town);mate.Entity.Level=1;
            mate.Entity.MapID=party.Entity.MapID;mate.Entity.X=party.Entity.X;mate.Entity.Y=party.Entity.Y;
            TeratoTeam(party,mate);var teamRun=PurpleAdmit(party,Pack(party,3004259,1));
            Check(((uint[])Field(teamRun,"Party")).OrderBy(v=>v).SequenceEqual(new[]{party.Entity.UID,mate.Entity.UID}.OrderBy(v=>v)),
                "Purple house party has no borrowed Sword Master level-100 gate");
            VerifyHouseBossExclusion(town);
        } finally {PurpleAdmission.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>DateTime.UtcNow));}
    }
    static void VerifyHouseBossExclusion(Map town) {
        foreach(bool purpleFirst in new[]{true,false}) {
            var p=SwordHousePlayer(town);var purple=Pack(p,3004259,1);var sword=Pack(p,720842,1);
            var run=purpleFirst?PurpleAdmit(p,purple):SwordAdmit(p,sword);
            Check(run!=null && (purpleFirst?SwordAdmit(p,sword):PurpleAdmit(p,purple))==null,
                "active house challenge refuses the other boss: "+purpleFirst);
            Check(p.Inventory.ContainsUID(purpleFirst?sword.UID:purple.UID),"refused second boss retains its exact summon item");
            Call(purpleFirst?PurpleAdmission:SwordAdmission,null,"Fail",p.Entity.UID,(Guid)Field(run,"Id"));
            Check((purpleFirst?SwordAdmit(p,sword):PurpleAdmit(p,purple))!=null,"terminal challenge allows a different daily boss");
        }
        var race=SwordHousePlayer(town);var a=Pack(race,3004259,1);var b=Pack(race,720842,1);int count=0;
        Parallel.Invoke(()=>{if(PurpleAdmit(race,a)!=null)System.Threading.Interlocked.Increment(ref count);},
            ()=>{if(SwordAdmit(race,b)!=null)System.Threading.Interlocked.Increment(ref count);});
        Check(count==1 && race.Inventory.Objects.Count(i=>i.ID==3004259 || i.ID==720842)==1,
            "simultaneous different boss admissions consume only one item");
        var upgrade=SwordHousePlayer(town);PurpleAdmit(upgrade,Pack(upgrade,3004259,1));
        upgrade.Entity.MapID=1002;AdventureNear(upgrade,30156);BuyHouseCert(upgrade,2);
        var permit=upgrade.Inventory.Objects.Single(i=>i.ID==721174);HouseNpc(upgrade,3);HouseNpc(upgrade,40);
        Check(!Fenced(upgrade) && Scalar("SELECT houselevel FROM house WHERE id=@uid",upgrade.Entity.UID)==1
            && upgrade.Inventory.ContainsUID(permit.UID),"active Purple admission prevents house upgrade without spending its permit");
    }
}
