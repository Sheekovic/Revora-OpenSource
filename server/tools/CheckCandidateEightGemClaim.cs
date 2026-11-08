using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle
{
    static bool eightGemClaimFixture;
    static ConquerItem EightGemGear(GameState p,byte slot){
        var info=ConquerItemInformation.BaseInformations.Values.Where(i=>(int)ItemHandler.GetPositionFromID(i.ID)==slot && i.ID%10==5 && i.Level<=15 && (i.Class==0 || i.Class>=20 && i.Class<=25) && i.Gender==0).OrderBy(i=>i.Level).ThenBy(i=>i.ID).First();
        var item=new ConquerItem(true){ID=info.ID,Bound=true,StackSize=1,Durability=info.Durability,MaximDurability=info.Durability,SocketOne=Revora.Game.Enums.Gem.EmptySocket,SocketTwo=Revora.Game.Enums.Gem.EmptySocket};
        Require(p.Inventory.Add(item,Revora.Game.Enums.ItemUse.CreateAndAdd));return item;
    }
    static void EightGemClaimProcess(bool reload){
        eightGemClaimFixture=true;const uint uid=4294912101;
        if((bool)Sql("SELECT to_regclass('revora_eight_gem_probe') IS NULL")){
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==0);
            nextUid=uid;Player(Kernel.Maps[1002]);Sql("UPDATE entities SET level=130,reborn=2,class=25,strength=500,agility=500,conquerpoints=500000 WHERE uid=@uid","uid",(long)uid);
            Sql("CREATE TABLE revora_eight_gem_probe(snapshot text);INSERT INTO revora_eight_gem_probe VALUES(NULL)");
        }
        nextUid=uid;var p=Player(Kernel.Maps[1002],true);ChiTable.Load(p);WayofHeroes.Load(p);p.LoadItemStats();
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if(Sql("SELECT snapshot FROM revora_eight_gem_probe")==DBNull.Value){
            Require(!reload && p.Inventory.Count==0);byte[] slots={1,2,3,8};
            uint gemType=EShopFile.Shops[2888].Items.Where(id=>id>=700001 && id<=700073 && id%10==3 && ConquerItemInformation.BaseInformations.ContainsKey(id) && ConquerItemInformation.BaseInformations[id].ConquerPointsWorth>0 && Enum.IsDefined(typeof(Revora.Game.Enums.Gem),(byte)(id%1000))).OrderBy(id=>ConquerItemInformation.BaseInformations[id].ConquerPointsWorth).First();
            foreach(byte slot in slots){
                var gear=EightGemGear(p,slot);RetainedUpgradeUsage(p,gear.UID,slot,ItemUsage.Mode.EquipItem);Require(p.Equipment.TryGetItem(slot).UID==gear.UID);
                for(ushort socket=1;socket<=2;socket++){
                    string before=StoneClaimSnapshot(p);NobilityClaimPacket(p,707);Require(before==StoneClaimSnapshot(p));
                    CareerBuy(p,gemType);var gem=p.Inventory.Objects.Single();StoneClaimEmbed(p,gear,gem,socket);
                }
            }
            var boots=p.Equipment.TryGetItem(8);RetainedUpgradeUsage(p,boots.UID,8,ItemUsage.Mode.UnequipItem);
            string bag=StoneClaimSnapshot(p);NobilityClaimPacket(p,707);Require(bag==StoneClaimSnapshot(p));
            RetainedUpgradeUsage(p,boots.UID,8,ItemUsage.Mode.EquipItem);Require(GeneralData.SwitchEquipment(p,true));
            string inactive=StoneClaimSnapshot(p);NobilityClaimPacket(p,707);Require(inactive==StoneClaimSnapshot(p));
            Require(GeneralData.SwitchEquipment(p,false));System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,707));
            var reward=p.Inventory.Objects.Single();Require(reward.ID==700072 && reward.Bound && reward.StackSize==1);
            var ring=EightGemGear(p,6);StoneClaimEmbed(p,ring,reward,1);Require((byte)ring.SocketOne==72 && !p.Inventory.ContainsUID(reward.UID));
            RetainedUpgradeUsage(p,ring.UID,6,ItemUsage.Mode.EquipItem);Require(p.Equipment.TryGetItem(6).UID==ring.UID);
            Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_eight_gem_probe SET snapshot=@snapshot","snapshot",StoneClaimSnapshot(p));
        }
        Require(p.Inventory.Count==0 && (byte)p.Equipment.TryGetItem(6).SocketOne==72);
        Require((long)Sql("SELECT count(*) FROM revora_gem_embeddings WHERE uid=@uid","uid",(long)uid)==9);
        Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=707 AND NOT legacy","uid",(long)uid)==1);
        string exact=StoneClaimSnapshot(p);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_eight_gem_probe")));
        NobilityClaimPacket(p,707);Require(exact==StoneClaimSnapshot(p) && !PirateFenced(p));
        System.Console.WriteLine("[career-process] career-eight-gem-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Eight gem claim did not close.");
    }
}
