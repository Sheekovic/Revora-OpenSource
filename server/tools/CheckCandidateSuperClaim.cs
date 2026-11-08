using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static bool superClaimFixture;
    static string SuperClaimSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object(
'base',@snapshot::json,'promotion',@promotion::json)::text","snapshot",QualityClaimSnapshot(p),"promotion",PromotionClaimSnapshot(p)));}
    static void SuperClaimProcess(bool reload){
        superClaimFixture=true;const uint uid=4294910801;
        if((bool)Sql("SELECT to_regclass('revora_super_claim_probe') IS NULL")){
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==0);
            nextUid=uid;Player(Kernel.Maps[1002]);Sql("UPDATE entities SET level=110,reborn=0,class=24,strength=500,agility=500,conquerpoints=50000 WHERE uid=@uid","uid",(long)uid);
            Sql("CREATE TABLE revora_super_claim_probe(snapshot text);INSERT INTO revora_super_claim_probe VALUES(NULL)");
        }
        nextUid=uid;var p=Player(Kernel.Maps[1002],true);ChiTable.Load(p);WayofHeroes.Load(p);SkillTable.LoadProficiencies(p);p.LoadItemStats();byte[] slots={1,2,3,4,6,8};
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if(Sql("SELECT snapshot FROM revora_super_claim_probe")==DBNull.Value){
            Require(!reload && p.Inventory.Count==0);var catalog=ConquerItemInformation.BaseInformations;p.AddProficiency(new Proficiency(true){ID=410,Level=20});
            var gear=new ConquerItem[6];
            for(int n=0;n<slots.Length;n++){
                byte slot=slots[n];uint type=catalog.Values.Where(i=>(int)ItemHandler.GetPositionFromID(i.ID)==slot && i.ID%10==8 && i.Level<=15
                    && (i.Class==0 || i.Class>=20 && i.Class<=24) && i.Gender==0 && (slot!=4 || i.ID/1000==410) && catalog.ContainsKey(i.ID+1))
                    .OrderBy(i=>i.Level).ThenBy(i=>i.ID).First().ID;
                gear[n]=RefineryClaimGive(p,type);RetainedUpgradeUsage(p,gear[n].UID,slot,ItemUsage.Mode.EquipItem);Require(p.Equipment.TryGetItem(slot).UID==gear[n].UID);
            }
            NobilityClaimPacket(p,402);Require(p.Inventory.Count==0);
            var upgrades=Feature("EquipmentUpgrades");var roll=upgrades.GetField("Roll",Hidden);var clock=upgrades.GetField("Clock",Hidden);
            object oldRoll=roll.GetValue(null),oldClock=clock.GetValue(null);DateTime upgradeNow=DateTime.UtcNow;
            try{roll.SetValue(null,(Func<int,int>)(max=>0));clock.SetValue(null,(Func<DateTime>)(()=>upgradeNow));
                for(int n=0;n<gear.Length;n++){
                    var item=gear[n];RetainedUpgradeUsage(p,item.UID,slots[n],ItemUsage.Mode.UnequipItem);CareerBuy(p,1088000);
                    var ball=p.Inventory.Objects.Single(i=>i.ID==1088000);upgradeNow=upgradeNow.AddSeconds(2);
                    RetainedUpgradeUsage(p,item.UID,ball.UID,ItemUsage.Mode.DragonBallUpgrade);Require(item.ID%10==9 && !p.Inventory.ContainsUID(ball.UID));
                    if(n==gear.Length-1){NobilityClaimPacket(p,402);Require(p.Inventory.Count==1);}
                    RetainedUpgradeUsage(p,item.UID,slots[n],ItemUsage.Mode.EquipItem);Require(p.Equipment.TryGetItem(slots[n]).UID==item.UID);
                }
                System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,402));var box=p.Inventory.Objects.Single();
                Require(box.ID==721052 && box.Bound && box.Plus==0 && box.StackSize==1);
                p.BindWorldMap(1004);HeartTalk(p,10001,1);HeartTalk(p,10001,3);
                Require(p.Entity.Class==25 && !p.Inventory.ContainsUID(box.UID) && !PirateFenced(p));
                var prize=p.Inventory.Objects.Single();Require(prize.ID==1088000 && prize.Bound && prize.StackSize==1);
                string promoted=SuperClaimSnapshot(p);HeartTalk(p,10001,3);Require(promoted==SuperClaimSnapshot(p));
                uint spareType=catalog.Values.Where(i=>i.ID/1000==160 && i.ID%10==5 && i.Level<=15 && catalog.ContainsKey(i.ID+1)).OrderBy(i=>i.Level).First().ID;
                var spare=RefineryClaimGive(p,spareType);upgradeNow=upgradeNow.AddSeconds(2);
                RetainedUpgradeUsage(p,spare.UID,prize.UID,ItemUsage.Mode.DragonBallUpgrade);Require(spare.ID==spareType+1 && !p.Inventory.ContainsUID(prize.UID));
                string used=SuperClaimSnapshot(p);RetainedUpgradeUsage(p,spare.UID,prize.UID,ItemUsage.Mode.DragonBallUpgrade);Require(used==SuperClaimSnapshot(p));
            }finally{roll.SetValue(null,oldRoll);clock.SetValue(null,oldClock);}
            Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_super_claim_probe SET snapshot=@snapshot","snapshot",SuperClaimSnapshot(p));
        }
        Require(p.Entity.Class==25 && slots.All(slot=>p.Equipment.TryGetItem(slot)!=null && p.Equipment.TryGetItem(slot).ID%10==9) && p.Inventory.Count==1);
        Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=402 AND NOT legacy","uid",(long)uid)==1);
        Require((long)Sql("SELECT count(*) FROM revora_equipment_upgrades WHERE uid=@uid AND operation=3 AND success","uid",(long)uid)==7);
        Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND source='class-promotion-v1' AND source_key='class-25'","uid",(long)uid)==1);
        string exact=SuperClaimSnapshot(p);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_super_claim_probe")));
        NobilityClaimPacket(p,402);Require(exact==SuperClaimSnapshot(p) && !PirateFenced(p));
        System.Console.WriteLine("[career-process] career-super-claim-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Super claim did not close.");
    }
}
