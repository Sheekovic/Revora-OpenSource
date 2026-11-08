using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static bool sharedBonusFixture;
    static ConquerItem SharedBonusGive(GameState p,uint type){
        ushort dur=ConquerItemInformation.BaseInformations[type].Durability;
        var item=new ConquerItem(true){ID=type,Plus=3,Bound=true,StackSize=1,Durability=dur,MaximDurability=dur};
        Require(p.Inventory.Add(item,Revora.Game.Enums.ItemUse.CreateAndAdd));return item;
    }
    static void SharedBonusProcess(bool reload){
        sharedBonusFixture=true;const uint uid=4294910301;
        if((bool)Sql("SELECT to_regclass('revora_shared_bonus_probe') IS NULL")){
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==0);
            nextUid=uid;Player(Kernel.Maps[1002]);Sql("UPDATE entities SET level=140,reborn=2 WHERE uid=@uid","uid",(long)uid);
            Sql("CREATE TABLE revora_shared_bonus_probe(snapshot text);INSERT INTO revora_shared_bonus_probe VALUES(NULL)");
        }
        nextUid=uid;var p=Player(Kernel.Maps[1002],true);ChiTable.Load(p);WayofHeroes.Load(p);p.LoadItemStats();
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if(Sql("SELECT snapshot FROM revora_shared_bonus_probe")==DBNull.Value){
            Require(!reload && p.Inventory.Count==0);
            var boots=SharedBonusGive(p,160249);RetainedUpgradeUsage(p,boots.UID,8,ItemUsage.Mode.EquipItem);
            Require(p.Equipment.TryGetItem(8).UID==boots.UID && GeneralData.SwitchEquipment(p,true));
            NobilityClaimPacket(p,503);Require(p.Inventory.Count==0); // Inactive primary boots cannot qualify.
            var fan=SharedBonusGive(p,201009);NobilityClaimPacket(p,503);Require(p.Inventory.Count==1); // Bagged shared item cannot qualify.
            RetainedUpgradeUsage(p,fan.UID,10,ItemUsage.Mode.EquipItem);
            Require(p.AlternateEquipment && p.Equipment.TryGetItem(10).UID==fan.UID);
            System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,503));
            var reward=p.Inventory.Objects.Single();Require(reward.ID==730002 && reward.Plus==2 && reward.Bound && reward.StackSize==1);
            Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_shared_bonus_probe SET snapshot=@snapshot","snapshot",NobilityClaimSnapshot(p));
        }
        string exact=NobilityClaimSnapshot(p);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_shared_bonus_probe")));
        Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=503 AND NOT legacy","uid",(long)uid)==1);
        var saved=p.Inventory.Objects.Single();Require(saved.ID==730002 && saved.Plus==2 && saved.Bound && saved.StackSize==1);
        // Selected bank itself is not assumed persisted; explicitly switch through normal service.
        Require(GeneralData.SwitchEquipment(p,true));NobilityClaimPacket(p,503);
        Require(GeneralData.SwitchEquipment(p,false));NobilityClaimPacket(p,503);
        Require(exact==NobilityClaimSnapshot(p) && !PirateFenced(p));
        System.Console.WriteLine("[career-process] career-shared-bonus-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Shared bonus did not close.");
    }
}
