using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static bool compositionClaimFixture;
    static string CompositionClaimSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object(
'base',@snapshot::json,'targets',(SELECT json_agg(e ORDER BY receipt) FROM revora_plus_composition_targets e JOIN revora_plus_compositions c ON c.id=e.receipt WHERE c.uid=@uid),
'upgrades',(SELECT json_agg(u ORDER BY id) FROM revora_equipment_upgrades u WHERE uid=@uid))::text",
        "snapshot",StoneClaimSnapshot(p),"uid",(long)p.Entity.UID));}
    static void CompositionClaimCompose(GameState p,ConquerItem target,ConquerItem stone){
        var frame=RetainedBreedFrame(target.UID,stone.UID);frame[4]=0;PacketHandler.HandlePacket(frame,p);
        Require(!p.Inventory.ContainsUID(stone.UID) && !PirateFenced(p));
        string once=CompositionClaimSnapshot(p);PacketHandler.HandlePacket(frame,p);Require(once==CompositionClaimSnapshot(p));
    }
    static ConquerItem CompositionClaimStone(GameState p,byte plus){
        var stone=new ConquerItem(true){ID=730000u+plus,Plus=plus,Bound=true,StackSize=1};
        Require(p.Inventory.Add(stone,Revora.Game.Enums.ItemUse.CreateAndAdd));return stone;
    }
    static void CompositionClaimProcess(bool reload){
        compositionClaimFixture=true;const uint uid=4294910101;
        if((bool)Sql("SELECT to_regclass('revora_composition_claim_probe') IS NULL")){
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==0);
            nextUid=uid;Player(Kernel.Maps[1002]);Sql("UPDATE entities SET level=140,reborn=2 WHERE uid=@uid","uid",(long)uid);
            Sql("CREATE TABLE revora_composition_claim_probe(snapshot text);INSERT INTO revora_composition_claim_probe VALUES(NULL)");
        }
        nextUid=uid;var p=Player(Kernel.Maps[1002],true);ChiTable.Load(p);WayofHeroes.Load(p);p.LoadItemStats();
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if(Sql("SELECT snapshot FROM revora_composition_claim_probe")==DBNull.Value){
            Require(!reload);ConquerItem boots;
            bool started=(long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=403","uid",(long)uid)==1;
            if(!started){
                Require(p.Inventory.Count==0);
                uint type=ConquerItemInformation.BaseInformations.Values.Where(i=>i.ID/1000==160 && i.ID%10==9 && i.Level>=15).OrderBy(i=>i.Level).First().ID;
                boots=RefineryClaimGive(p,type);RetainedUpgradeUsage(p,boots.UID,8,ItemUsage.Mode.EquipItem);
                NobilityClaimPacket(p,403);NobilityClaimPacket(p,503);Require(p.Inventory.Count==0);
                // Supplied input stones; actual composition establishes all earning receipts.
                CompositionClaimCompose(p,boots,CompositionClaimStone(p,2));Require(boots.Plus==2 && boots.PlusProgress==0);
                byte old=boots.Plus;boots.Plus=12;NobilityClaimPacket(p,503);boots.Plus=old;Require(p.Inventory.Count==0);
                System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,403));
                var earned=p.Inventory.Objects.ToArray();Require(earned.Length==3 && earned.All(i=>i.ID==1088001 && i.Bound && i.Plus==0 && i.StackSize==1));
                RetainedUpgradeUsage(p,boots.UID,8,ItemUsage.Mode.UnequipItem);
            }else{
                // Resume only the two remaining Meteors after the cooldown assertion failure.
                boots=p.Inventory.Objects.Single(i=>i.ID/1000==160);
                Require(boots.Bound && boots.Plus==2 && boots.PlusProgress==0);
                Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid","uid",(long)uid)==1);
                Require((long)Sql("SELECT count(*) FROM revora_plus_compositions WHERE uid=@uid","uid",(long)uid)==1);
            }
            var meteors=p.Inventory.Objects.Where(i=>i.ID==1088001).ToArray();
            Require(meteors.All(i=>i.Bound && i.Plus==0 && i.StackSize==1) && meteors.Length+(long)Sql("SELECT count(*) FROM revora_equipment_upgrades WHERE uid=@uid AND operation=1 AND success","uid",(long)uid)==3);
            var roll=Feature("EquipmentUpgrades").GetField("Roll",Hidden);object oldRoll=roll.GetValue(null);
            var clock=Feature("EquipmentUpgrades").GetField("Clock",Hidden);object oldClock=clock.GetValue(null);DateTime upgradeNow=DateTime.UtcNow;
            try{clock.SetValue(null,(Func<DateTime>)(()=>upgradeNow));roll.SetValue(null,(Func<int,int>)(max=>0));foreach(var meteor in meteors){
                upgradeNow=upgradeNow.AddSeconds(2);byte level=ConquerItemInformation.BaseInformations[boots.ID].Level;
                RetainedUpgradeUsage(p,boots.UID,meteor.UID,ItemUsage.Mode.MeteorUpgrade);
                Require(!p.Inventory.ContainsUID(meteor.UID) && ConquerItemInformation.BaseInformations[boots.ID].Level>level && !PirateFenced(p));
                string once=CompositionClaimSnapshot(p);RetainedUpgradeUsage(p,boots.UID,meteor.UID,ItemUsage.Mode.MeteorUpgrade);Require(once==CompositionClaimSnapshot(p));
            }}finally{roll.SetValue(null,oldRoll);clock.SetValue(null,oldClock);}
            CompositionClaimCompose(p,boots,CompositionClaimStone(p,3));Require(boots.Plus==3 && boots.PlusProgress==40);
            NobilityClaimPacket(p,503);Require(p.Inventory.Count==1 && p.Inventory.ContainsUID(boots.UID));
            RetainedUpgradeUsage(p,boots.UID,8,ItemUsage.Mode.EquipItem);
            System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,503));var reward=p.Inventory.Objects.Single();
            Require(reward.ID==730002 && reward.Plus==2 && reward.Bound && reward.StackSize==1);
            CompositionClaimCompose(p,boots,reward);Require(boots.Plus==3 && boots.PlusProgress==80);
            Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_composition_claim_probe SET snapshot=@snapshot","snapshot",CompositionClaimSnapshot(p));
        }
        var worn=p.Equipment.TryGetItem(8);Require(worn!=null && worn.Plus==3 && worn.PlusProgress==80 && p.Inventory.Count==0);
        Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid IN(403,503) AND NOT legacy","uid",(long)uid)==2);
        Require((long)Sql("SELECT count(*) FROM revora_plus_composition_targets e JOIN revora_plus_compositions c ON c.id=e.receipt WHERE c.uid=@uid","uid",(long)uid)==3);
        Require((long)Sql("SELECT count(*) FROM revora_equipment_upgrades WHERE uid=@uid AND operation=1 AND success","uid",(long)uid)==3);
        string exact=CompositionClaimSnapshot(p);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_composition_claim_probe")));
        NobilityClaimPacket(p,403);NobilityClaimPacket(p,503);Require(exact==CompositionClaimSnapshot(p) && !PirateFenced(p));
        System.Console.WriteLine("[career-process] career-composition-claims-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Composition claims did not close.");
    }
}
