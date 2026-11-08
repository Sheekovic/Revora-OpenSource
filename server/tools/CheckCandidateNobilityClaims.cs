using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static bool nobilityClaimFixture;
    static void NobilityClaimPacket(GameState p,uint stage){
        var frame=new byte[17];Revora.Network.Writer.WriteUInt16(9,0,frame);
        Revora.Network.Writer.WriteUInt16(2830,2,frame);frame[4]=1;
        Revora.Network.Writer.WriteUInt32(stage,5,frame);
        Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,9,frame);
        Revora.Network.PacketHandler.HandlePacket(frame,p);
    }
    static string NobilityClaimSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object(
'profile',(SELECT json_build_array(level,reborn,money,conquerpoints,boundcps) FROM entities WHERE uid=@uid),
'donation',(SELECT row_to_json(n) FROM nobility n WHERE entityuid=@uid),
'payments',(SELECT json_agg(n ORDER BY id) FROM revora_nobility_payments n WHERE uid=@uid),
'items',(SELECT json_agg(i ORDER BY uid) FROM items i WHERE entityid=@uid),
'chi',(SELECT row_to_json(c) FROM chi c WHERE uid=@uid),
'way',(SELECT row_to_json(w) FROM wayofheroes w WHERE uid=@uid),
'claims',(SELECT json_agg(r ORDER BY stage_uid) FROM revora_way_of_heroes_claims r WHERE uid=@uid),
'uses',(SELECT json_agg(r ORDER BY id) FROM revora_chi_item_uses r WHERE uid=@uid))::text","uid",(long)p.Entity.UID));}
    static void NobilityClaimProcess(bool reload){
        nobilityClaimFixture=true;
        if((bool)Sql("SELECT to_regclass('revora_nobility_claim_probe') IS NULL")){
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN 4294909301 AND 4294909303")==0);
            Sql("CREATE TABLE revora_nobility_claim_probe(uid bigint PRIMARY KEY,chi bigint NOT NULL,snapshot text)");
        }
        uint[] stages={806,907,1005},prizes={3005127,3005128,3005129},chi={1000,2000,3000};
        ulong[] limits={30000000,100000000,500000000};
        for(int n=0;n<3;n++){
            uint uid=4294909301u+(uint)n;bool exists=(long)Sql("SELECT count(*) FROM revora_nobility_claim_probe WHERE uid=@uid","uid",(long)uid)==1;
            if(!exists){Require(!reload);nextUid=uid;Player(Kernel.Maps[1002]);
                // Supplied starting qualification and silver, never a supplied donation or prize.
                Sql("UPDATE entities SET level=121,reborn=2,money=@money WHERE uid=@uid","money",(long)limits[n]+100,"uid",(long)uid);
            }
            nextUid=uid;var p=Player(Kernel.Maps[1002],true);ChiTable.Load(p);WayofHeroes.Load(p);
            Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
            if(!exists)Sql("INSERT INTO revora_nobility_claim_probe(uid,chi) VALUES(@uid,@chi)","uid",(long)uid,"chi",(long)p.ChiPoints);
            object snapshot=Sql("SELECT snapshot FROM revora_nobility_claim_probe WHERE uid=@uid","uid",(long)uid);
            if(snapshot==DBNull.Value){Require(!reload);
                Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid","uid",(long)uid)==0);
                // Forged runtime progress does not substitute for a saved payment.
                ulong original=p.NobilityInformation.Donation;p.NobilityInformation.Donation=limits[n];
                NobilityClaimPacket(p,stages[n]);p.NobilityInformation.Donation=original;
                Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid","uid",(long)uid)==0 && !PirateFenced(p));
                CareerNobilityDonate(p,limits[n]-1,0);
                Require(p.NobilityInformation.Donation==limits[n]-1 && p.Entity.Money==101);
                NobilityClaimPacket(p,stages[n]);
                Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid","uid",(long)uid)==0 && !PirateFenced(p));
                CareerNobilityDonate(p,1,0);
                Require(p.NobilityInformation.Donation==limits[n] && p.Entity.Money==100);
                System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,stages[n]));
                Require(!PirateFenced(p) && (long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND NOT legacy","uid",(long)uid)==1);
                var reward=p.Inventory.Objects.Single(i=>i.ID==prizes[n]);Require(reward.Bound && reward.Plus==0 && reward.StackSize==1);
                var use=new ItemUsage(true){UID=reward.UID,Action=ItemUsage.Mode.EquipItem}.ToArray();
                Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,use.Length-8,use);
                ItemUsage.Process(use,p);ItemUsage.Process(use,p);
                Require(!p.Inventory.ContainsUID(reward.UID) && !PirateFenced(p));
                Require(EntityTable.SaveEntity(p));
                Sql("UPDATE revora_nobility_claim_probe SET snapshot=@snapshot WHERE uid=@uid","snapshot",NobilityClaimSnapshot(p),"uid",(long)uid);
            }
            Require(p.ChiPoints==Convert.ToInt64(Sql("SELECT chi FROM revora_nobility_claim_probe WHERE uid=@uid","uid",(long)uid))+chi[n]);
            Require(p.NobilityInformation.Donation==limits[n] && p.Entity.Money==100);
            Require((long)Sql("SELECT count(*) FROM revora_nobility_payments WHERE uid=@uid","uid",(long)uid)==2);
            string exact=NobilityClaimSnapshot(p);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_nobility_claim_probe WHERE uid=@uid","uid",(long)uid)));
            NobilityClaimPacket(p,stages[n]);Require(!PirateFenced(p) && NobilityClaimSnapshot(p)==exact);
        }
        System.Console.WriteLine("[career-process] career-nobility-claims-"+(reload?"reload":"earn")+"-passed");
        Server.CommandsAI("@close");throw new InvalidOperationException("Nobility claim process did not close.");
    }
}
