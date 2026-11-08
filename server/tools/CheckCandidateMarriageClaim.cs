using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle
{
    static bool marriageClaimFixture;
    static string MarriageClaimSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object(
'base',@snapshot::json,'spouse',(SELECT spouse FROM entities WHERE uid=@uid),
'evidence',(SELECT json_agg(e ORDER BY achievement_id,source,source_key) FROM revora_achievement_evidence e WHERE uid=@uid),
'uses',(SELECT json_agg(u ORDER BY id) FROM revora_quest_gift_openings u WHERE uid=@uid),
'flowers',(SELECT row_to_json(f) FROM revora_flower_balances f WHERE uid=@uid),
'deliveries',(SELECT json_agg(d ORDER BY id) FROM revora_flower_deliveries d WHERE sender=@uid OR recipient=@uid))::text",
        "snapshot",NobilityClaimSnapshot(p),"uid",(long)p.Entity.UID));}
    static void MarriageClaimAttack(GameState sender,GameState recipient,uint action){
        new Revora.Game.Attacking.Handle(new Attack(true){AttackType=action,Attacker=sender.Entity.UID,Attacked=recipient.Entity.UID},sender.Entity,null);
    }
    static void MarriageClaimProcess(bool reload){
        marriageClaimFixture=true;const uint uid=4294911901;
        if((bool)Sql("SELECT to_regclass('revora_marriage_claim_probe') IS NULL")){
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN @uid AND @last","uid",(long)uid,"last",(long)uid+1)==0);
            nextUid=uid;Player(Kernel.Maps[1002]);Player(Kernel.Maps[1002],false,2001);
            Sql("UPDATE entities SET level=130,reborn=2 WHERE uid BETWEEN @uid AND @last","uid",(long)uid,"last",(long)uid+1);
            Sql("CREATE TABLE revora_marriage_claim_probe(snapshot text,peer_snapshot text);INSERT INTO revora_marriage_claim_probe VALUES(NULL,NULL)");
        }
        nextUid=uid;var male=Player(Kernel.Maps[1002],true);var female=Player(Kernel.Maps[1002],true,2001);
        foreach(var p in new[]{male,female}){ChiTable.Load(p);WayofHeroes.Load(p);Call(Feature("FlowerDelivery"),"Load",p);Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));}
        if(Sql("SELECT snapshot FROM revora_marriage_claim_probe")==DBNull.Value){
            Require(!reload);male.Entity.Teleport(1002,400,350);female.Entity.Teleport(1002,400,351);
            foreach(var p in new[]{male,female}){string before=MarriageClaimSnapshot(p);NobilityClaimPacket(p,710);Require(before==MarriageClaimSnapshot(p));}
            MarriageClaimAttack(female,male,Attack.MarriageAccept);Require(male.Entity.Spouse=="None" && female.Entity.Spouse=="None");
            MarriageClaimAttack(male,female,Attack.MarriageRequest);
            foreach(var p in new[]{male,female}){string pending=MarriageClaimSnapshot(p);NobilityClaimPacket(p,710);Require(pending==MarriageClaimSnapshot(p));}
            MarriageClaimAttack(female,male,Attack.MarriageAccept);Require(male.Entity.Spouse==female.Entity.Name && female.Entity.Spouse==male.Entity.Name);
            foreach(var p in new[]{male,female}){
                System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,710));var pack=p.Inventory.Objects.Single();Require(pack.ID==3005134 && pack.Bound);
                uint cp=p.Entity.ConquerPoints,rvp=p.Entity.RevoraPoint;ulong silver=p.Entity.Money;uint packUid=pack.UID;
                System.Threading.Tasks.Parallel.For(0,8,_=>RetainedUpgradeUsage(p,packUid,0,ItemUsage.Mode.EquipItem));
                Require(p.Entity.ConquerPoints==cp+5000 && p.Entity.RevoraPoint==rvp && p.Entity.Money==silver);
                Require(p.Inventory.Count==2 && p.Inventory.Objects.All(i=>i.Bound && i.StackSize==1) && p.Inventory.Objects.Select(i=>i.ID).OrderBy(i=>i).SequenceEqual(new uint[]{751099,755099}));
                string once=MarriageClaimSnapshot(p);RetainedUpgradeUsage(p,packUid,0,ItemUsage.Mode.EquipItem);NobilityClaimPacket(p,710);Require(once==MarriageClaimSnapshot(p));
            }
            foreach(var sender in new[]{male,female}){
                bool boy=ReferenceEquals(sender,male);var recipient=boy?female:male;var gift=sender.Inventory.Objects.Single(i=>i.ID==(boy?751099u:755099u));
                var frame=new byte[24];Revora.Network.Writer.WriteUInt16(16,0,frame);Revora.Network.Writer.WriteUInt16(1150,2,frame);Revora.Network.Writer.WriteUInt32(boy?0u:1u,4,frame);
                Revora.Network.Writer.WriteUInt32(recipient.Entity.UID,8,frame);Revora.Network.Writer.WriteUInt32(gift.UID,12,frame);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,16,frame);
                Revora.Network.PacketHandler.HandlePacket(frame,sender);Require(!sender.Inventory.ContainsUID(gift.UID));
                string once=MarriageClaimSnapshot(sender);Revora.Network.PacketHandler.HandlePacket(frame,sender);Require(once==MarriageClaimSnapshot(sender));
            }
            Require(EntityTable.SaveEntity(male) && EntityTable.SaveEntity(female));
            Sql("UPDATE revora_marriage_claim_probe SET snapshot=@snapshot,peer_snapshot=@peer","snapshot",MarriageClaimSnapshot(male),"peer",MarriageClaimSnapshot(female));
        }
        foreach(var p in new[]{male,female}){
            Require(!PirateFenced(p) && p.Inventory.Count==1);
            Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=710 AND NOT legacy","uid",(long)p.Entity.UID)==1);
            Require((long)Sql("SELECT count(*) FROM revora_quest_gift_openings WHERE uid=@uid AND item_type=3005134 AND cps=5000 AND rvps=0 AND silver=0 AND cardinality(rewards)=2","uid",(long)p.Entity.UID)==1);
            Require((long)Sql("SELECT count(*) FROM revora_flower_deliveries WHERE sender=@uid AND amount=99 AND bound","uid",(long)p.Entity.UID)==1);
        }
        string exact=MarriageClaimSnapshot(male),peerExact=MarriageClaimSnapshot(female);
        Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_marriage_claim_probe")) && peerExact==Convert.ToString(Sql("SELECT peer_snapshot FROM revora_marriage_claim_probe")));
        MarriageClaimAttack(female,male,Attack.MarriageAccept);NobilityClaimPacket(male,710);NobilityClaimPacket(female,710);
        Require(exact==MarriageClaimSnapshot(male) && peerExact==MarriageClaimSnapshot(female));
        System.Console.WriteLine("[career-process] career-marriage-claim-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Marriage claim did not close.");
    }
}
