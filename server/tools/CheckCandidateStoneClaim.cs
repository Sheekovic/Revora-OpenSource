using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static bool stoneClaimFixture;
    static string StoneClaimSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object(
'base',@snapshot::json,'embeddings',(SELECT json_agg(e ORDER BY id) FROM revora_gem_embeddings e WHERE uid=@uid),
'compositions',(SELECT json_agg(c ORDER BY id) FROM revora_plus_compositions c WHERE uid=@uid))::text",
        "snapshot",NobilityClaimSnapshot(p),"uid",(long)p.Entity.UID));}
    static void StoneClaimEmbed(GameState p,ConquerItem target,ConquerItem gem,ushort slot){
        var frame=new byte[32];Writer.WriteUInt16(24,0,frame);Writer.WriteUInt16(1027,2,frame);
        Writer.WriteUInt32(target.UID,12,frame);Writer.WriteUInt32(gem.UID,16,frame);Writer.WriteUInt16(slot,20,frame);
        Writer.WriteUInt64(PacketHandler.ClientSeal,24,frame);PacketHandler.HandlePacket(frame,p);
        Require(!p.Inventory.ContainsUID(gem.UID) && !PirateFenced(p));
    }
    static void StoneClaimProcess(bool reload){
        stoneClaimFixture=true;const uint uid=4294910001;
        if((bool)Sql("SELECT to_regclass('revora_stone_claim_probe') IS NULL")){
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==0);
            nextUid=uid;Player(Kernel.Maps[1002]);Sql("UPDATE entities SET level=140,reborn=2,conquerpoints=500000 WHERE uid=@uid","uid",(long)uid);
            Sql("CREATE TABLE revora_stone_claim_probe(snapshot text);INSERT INTO revora_stone_claim_probe VALUES(NULL)");
        }
        nextUid=uid;var p=Player(Kernel.Maps[1002],true);ChiTable.Load(p);WayofHeroes.Load(p);p.LoadItemStats();
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if(Sql("SELECT snapshot FROM revora_stone_claim_probe")==DBNull.Value){
            Require(!reload && p.Inventory.Count==0);
            // Supplied empty-socket boots; gems and reward are earned through ordinary handlers.
            ushort dur=ConquerItemInformation.BaseInformations[160249].Durability;
            var boots=new ConquerItem(true){ID=160249,Bound=true,StackSize=1,Durability=dur,MaximDurability=dur,
                SocketOne=Revora.Game.Enums.Gem.EmptySocket,SocketTwo=Revora.Game.Enums.Gem.EmptySocket};
            Require(p.Inventory.Add(boots,Revora.Game.Enums.ItemUse.CreateAndAdd));
            RetainedUpgradeUsage(p,boots.UID,8,ItemUsage.Mode.EquipItem);
            NobilityClaimPacket(p,603);Require(p.Inventory.Count==0);
            uint type=EShopFile.Shops[2888].Items.Where(id=>id>=700001 && id<=700073 && id%10==3
                && ConquerItemInformation.BaseInformations.ContainsKey(id) && ConquerItemInformation.BaseInformations[id].ConquerPointsWorth>0
                && Enum.IsDefined(typeof(Revora.Game.Enums.Gem),(byte)(id%1000))).OrderBy(id=>ConquerItemInformation.BaseInformations[id].ConquerPointsWorth).First();
            for(ushort slot=1;slot<=2;slot++){
                CareerBuy(p,type);var gem=p.Inventory.Objects.Single();Require(gem.ID==type);StoneClaimEmbed(p,boots,gem,slot);
                if(slot==1){NobilityClaimPacket(p,603);Require(p.Inventory.Count==0);}
            }
            RetainedUpgradeUsage(p,boots.UID,8,ItemUsage.Mode.UnequipItem);NobilityClaimPacket(p,603);
            Require(p.Inventory.Count==1 && p.Inventory.ContainsUID(boots.UID));
            RetainedUpgradeUsage(p,boots.UID,8,ItemUsage.Mode.EquipItem);
            System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,603));
            var stone=p.Inventory.Objects.Single();Require(stone.ID==730002 && stone.Plus==2 && stone.Bound && stone.StackSize==1);
            Require(Convert.ToInt32(Sql("SELECT plus FROM items WHERE uid=@uid","uid",(long)stone.UID))==2);
            var compose=RetainedBreedFrame(boots.UID,stone.UID);compose[4]=0;PacketHandler.HandlePacket(compose,p);
            Require(!PirateFenced(p) && !p.Inventory.ContainsUID(stone.UID) && boots.Plus==2 && boots.PlusProgress==0);
            string once=StoneClaimSnapshot(p);PacketHandler.HandlePacket(compose,p);NobilityClaimPacket(p,603);Require(once==StoneClaimSnapshot(p));
            Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_stone_claim_probe SET snapshot=@snapshot","snapshot",StoneClaimSnapshot(p));
        }
        var equipped=p.Equipment.TryGetItem(8);Require(equipped!=null && equipped.Plus==2 && equipped.PlusProgress==0 && equipped.Bound);
        Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=603 AND NOT legacy","uid",(long)uid)==1);
        Require((long)Sql("SELECT count(*) FROM revora_gem_embeddings WHERE uid=@uid","uid",(long)uid)==2);
        Require((long)Sql("SELECT count(*) FROM revora_plus_compositions WHERE uid=@uid AND before_plus=0 AND after_plus=2 AND after_progress=0","uid",(long)uid)==1);
        string exact=StoneClaimSnapshot(p);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_stone_claim_probe")));
        NobilityClaimPacket(p,603);Require(exact==StoneClaimSnapshot(p) && !PirateFenced(p));
        System.Console.WriteLine("[career-process] career-stone-claim-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Stone claim did not close.");
    }
}
