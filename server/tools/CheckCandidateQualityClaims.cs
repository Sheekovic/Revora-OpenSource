using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static bool qualityClaimFixture;
    static string QualityClaimSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object(
'base',@snapshot::json,'fashion',(SELECT json_agg(u ORDER BY id) FROM revora_fashion_suit_pack_uses u WHERE uid=@uid))::text",
        "snapshot",CompositionClaimSnapshot(p),"uid",(long)p.Entity.UID));}
    static void QualityClaimProcess(bool reload){
        qualityClaimFixture=true;const uint uid=4294910701;
        if((bool)Sql("SELECT to_regclass('revora_quality_claim_probe') IS NULL")){
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==0);
            nextUid=uid;Player(Kernel.Maps[1002]);Sql("UPDATE entities SET level=140,reborn=0,class=25,strength=500,agility=500,conquerpoints=100000 WHERE uid=@uid","uid",(long)uid);
            Sql("CREATE TABLE revora_quality_claim_probe(snapshot text);INSERT INTO revora_quality_claim_probe VALUES(NULL)");
        }
        nextUid=uid;var p=Player(Kernel.Maps[1002],true);ChiTable.Load(p);WayofHeroes.Load(p);SkillTable.LoadProficiencies(p);p.LoadItemStats();
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));byte[] slots={1,2,3,4,6,8};
        if(Sql("SELECT snapshot FROM revora_quality_claim_probe")==DBNull.Value){
            Require(!reload && p.Inventory.Count==0);var catalog=ConquerItemInformation.BaseInformations;
            var gear=new ConquerItem[6];
            for(int n=0;n<slots.Length;n++){
                byte slot=slots[n];uint type=catalog.Values.Where(i=>(int)ItemHandler.GetPositionFromID(i.ID)==slot && i.ID%10==5 && i.Level<=15
                    && (i.Class==0 || i.Class>=20 && i.Class<=25) && i.Gender==0 && (slot!=4 || i.ID/1000==410)
                    && Enumerable.Range(1,3).All(k=>catalog.ContainsKey(i.ID+(uint)k))).OrderBy(i=>i.Level).ThenBy(i=>i.ID).First().ID;
                gear[n]=RefineryClaimGive(p,type);
            }
            p.AddProficiency(new Proficiency(true){ID=410,Level=20});
            var upgrades=Feature("EquipmentUpgrades");var roll=upgrades.GetField("Roll",Hidden);var clock=upgrades.GetField("Clock",Hidden);
            object oldRoll=roll.GetValue(null),oldClock=clock.GetValue(null);DateTime upgradeNow=DateTime.UtcNow;
            try{roll.SetValue(null,(Func<int,int>)(max=>0));clock.SetValue(null,(Func<DateTime>)(()=>upgradeNow));
                uint[] stages={102,202,302};
                for(int tier=0;tier<3;tier++){
                    uint stage=stages[tier],quality=6u+(uint)tier;
                    NobilityClaimPacket(p,stage);Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=@stage","uid",(long)uid,"stage",(int)stage)==0);
                    for(int n=0;n<gear.Length;n++){
                        var item=gear[n];if(item.Position!=0)RetainedUpgradeUsage(p,item.UID,slots[n],ItemUsage.Mode.UnequipItem);
                        CareerBuy(p,1088000);var ball=p.Inventory.Objects.Single(i=>i.ID==1088000);upgradeNow=upgradeNow.AddSeconds(2);
                        RetainedUpgradeUsage(p,item.UID,ball.UID,ItemUsage.Mode.DragonBallUpgrade);
                        Require(!p.Inventory.ContainsUID(ball.UID) && item.ID%10==quality && !PirateFenced(p));
                        if(n==gear.Length-1){NobilityClaimPacket(p,stage);Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=@stage","uid",(long)uid,"stage",(int)stage)==0);}
                        RetainedUpgradeUsage(p,item.UID,slots[n],ItemUsage.Mode.EquipItem);Require(p.Equipment.TryGetItem(slots[n]).UID==item.UID);
                    }
                    System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,stage));
                    if(tier<2){
                        var meteors=p.Inventory.Objects.ToArray();Require(meteors.Length==(tier==0?1:3) && meteors.All(i=>i.ID==1088001 && i.Bound && i.Plus==0 && i.StackSize==1));
                        var boots=gear[5];RetainedUpgradeUsage(p,boots.UID,8,ItemUsage.Mode.UnequipItem);
                        foreach(var meteor in meteors){upgradeNow=upgradeNow.AddSeconds(2);MountClaimUpgrade(p,boots,meteor);}
                        RetainedUpgradeUsage(p,boots.UID,8,ItemUsage.Mode.EquipItem);
                    }else{
                        var pack=p.Inventory.Objects.Single();Require(pack.ID==3001264 && pack.Bound && pack.Plus==0 && pack.StackSize==1);
                        SoulClaimUse(p,pack);var garment=p.Inventory.Objects.Single();Require(garment.Bound && garment.Days==7 && StorageItem.Garments[garment.ID].Stars==5);
                        RetainedUpgradeUsage(p,garment.UID,9,ItemUsage.Mode.EquipItem);Require(p.Equipment.TryGetItem(9).UID==garment.UID);
                    }
                }
            }finally{roll.SetValue(null,oldRoll);clock.SetValue(null,oldClock);}
            Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_quality_claim_probe SET snapshot=@snapshot","snapshot",QualityClaimSnapshot(p));
        }
        Require(slots.All(slot=>p.Equipment.TryGetItem(slot)!=null && p.Equipment.TryGetItem(slot).ID%10==8) && p.Inventory.Count==0);
        Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid IN(102,202,302) AND NOT legacy","uid",(long)uid)==3);
        Require((long)Sql("SELECT count(*) FROM revora_equipment_upgrades WHERE uid=@uid AND operation=3 AND success","uid",(long)uid)==18);
        Require((long)Sql("SELECT count(*) FROM revora_equipment_upgrades WHERE uid=@uid AND operation=1 AND success","uid",(long)uid)==4);
        string exact=QualityClaimSnapshot(p);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_quality_claim_probe")));
        foreach(uint stage in new uint[]{102,202,302})NobilityClaimPacket(p,stage);Require(exact==QualityClaimSnapshot(p) && !PirateFenced(p));
        System.Console.WriteLine("[career-process] career-quality-claims-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Quality claims did not close.");
    }
}
