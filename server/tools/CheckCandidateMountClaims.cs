using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static bool mountClaimFixture;
    static void MountClaimUpgrade(GameState p,ConquerItem boots,ConquerItem meteor){
        uint old=boots.ID;RetainedUpgradeUsage(p,boots.UID,meteor.UID,ItemUsage.Mode.MeteorUpgrade);
        Require(boots.ID!=old && !p.Inventory.ContainsUID(meteor.UID) && !PirateFenced(p));
        string exact=CompositionClaimSnapshot(p);RetainedUpgradeUsage(p,boots.UID,meteor.UID,ItemUsage.Mode.MeteorUpgrade);Require(exact==CompositionClaimSnapshot(p));
    }
    static void MountClaimProcess(bool reload){
        mountClaimFixture=true;const uint uid=4294910401;
        if((bool)Sql("SELECT to_regclass('revora_mount_claim_probe') IS NULL")){
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==0);
            nextUid=uid;Player(Kernel.Maps[1002]);Sql("UPDATE entities SET level=100,reborn=0,conquerpoints=50000 WHERE uid=@uid","uid",(long)uid);
            Sql("CREATE TABLE revora_mount_claim_probe(snapshot text);INSERT INTO revora_mount_claim_probe VALUES(NULL)");
        }
        nextUid=uid;var p=Player(Kernel.Maps[1002],true);ChiTable.Load(p);WayofHeroes.Load(p);p.LoadItemStats();
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if(Sql("SELECT snapshot FROM revora_mount_claim_probe")==DBNull.Value){
            Require(!reload && p.Inventory.Count==0);NobilityClaimPacket(p,205);NobilityClaimPacket(p,207);Require(p.Inventory.Count==0);
            uint type=ConquerItemInformation.BaseInformations.Values.Where(i=>i.ID/1000==160 && i.ID%10==9 && i.Level>=15).OrderBy(i=>i.Level).First().ID;
            var boots=RefineryClaimGive(p,type);CareerBuy(p,1088001);var meteor=p.Inventory.Objects.Single(i=>i.ID==1088001);
            var upgrades=Feature("EquipmentUpgrades");var roll=upgrades.GetField("Roll",Hidden);var clock=upgrades.GetField("Clock",Hidden);
            object oldRoll=roll.GetValue(null),oldClock=clock.GetValue(null);DateTime upgradeNow=DateTime.UtcNow;
            try{roll.SetValue(null,(Func<int,int>)(max=>0));clock.SetValue(null,(Func<DateTime>)(()=>upgradeNow));
                MountClaimUpgrade(p,boots,meteor);
                System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,205));
                var horse=p.Inventory.Objects.Single(i=>i.ID==300000);Require(horse.Bound && horse.Plus==0 && horse.StackSize==1);
                Require(p.Inventory.Count==2);NobilityClaimPacket(p,207);Require(p.Inventory.Count==2);
                RetainedUpgradeUsage(p,horse.UID,12,ItemUsage.Mode.EquipItem);Require(p.Equipment.TryGetItem(12).UID==horse.UID);
                Require(GeneralData.SwitchEquipment(p,true));System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,207));
                meteor=p.Inventory.Objects.Single(i=>i.ID==1088001);Require(meteor.Bound && meteor.Plus==0 && meteor.StackSize==1);
                upgradeNow=upgradeNow.AddSeconds(2);MountClaimUpgrade(p,boots,meteor);
                Require(GeneralData.SwitchEquipment(p,false));
            }finally{roll.SetValue(null,oldRoll);clock.SetValue(null,oldClock);}
            Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_mount_claim_probe SET snapshot=@snapshot","snapshot",CompositionClaimSnapshot(p));
        }
        var mount=p.Equipment.TryGetItem(12);Require(mount!=null && mount.ID==300000 && mount.Bound && mount.Plus==0 && p.Inventory.Count==1);
        Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid IN(205,207) AND NOT legacy","uid",(long)uid)==2);
        Require((long)Sql("SELECT count(*) FROM revora_equipment_upgrades WHERE uid=@uid AND operation=1 AND success","uid",(long)uid)==2);
        string exact=CompositionClaimSnapshot(p);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_mount_claim_probe")));
        NobilityClaimPacket(p,205);NobilityClaimPacket(p,207);Require(exact==CompositionClaimSnapshot(p) && !PirateFenced(p));
        System.Console.WriteLine("[career-process] career-mount-claims-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Mount claims did not close.");
    }
}
