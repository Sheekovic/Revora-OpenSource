using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static bool promotionClaimFixture;
    static string PromotionClaimSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object(
'base',@snapshot::json,'profile',(SELECT json_build_array(class,level,experience,expballs) FROM entities WHERE uid=@uid),
'promotions',(SELECT json_agg(e ORDER BY achievement_id) FROM revora_achievement_evidence e WHERE uid=@uid AND source='class-promotion-v1'),
'allowance',(SELECT row_to_json(d) FROM revora_exp_ball_days d WHERE uid=@uid))::text",
        "snapshot",NobilityClaimSnapshot(p),"uid",(long)p.Entity.UID));}
    static void PromotionClaimProcess(bool reload){
        promotionClaimFixture=true;const uint first=4294910501;
        byte[] schools={10,20,40,50,60,70,80,100,160};uint[] trainers={10022,10001,400,4720,4271,4272,5061,10000,51580};
        bool fresh=(bool)Sql("SELECT to_regclass('revora_promotion_claim_probe') IS NULL");
        if(fresh){Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN 4294910501 AND 4294910509")==0);
            Sql("CREATE TABLE revora_promotion_claim_probe(uid bigint PRIMARY KEY,snapshot text)");
        }
        for(int n=0;n<schools.Length;n++){
            uint uid=first+(uint)n;
            if(fresh){nextUid=uid;Player(Kernel.Maps[1002]);Sql("UPDATE entities SET level=20,reborn=0,class=@class WHERE uid=@uid","class",(int)schools[n],"uid",(long)uid);
                Sql("INSERT INTO revora_promotion_claim_probe VALUES(@uid,NULL)","uid",(long)uid);}
            nextUid=uid;var p=Player(Kernel.Maps[1002],true);ChiTable.Load(p);WayofHeroes.Load(p);SkillTable.LoadSpells(p);p.LoadItemStats();
            Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
            if(Sql("SELECT snapshot FROM revora_promotion_claim_probe WHERE uid=@uid","uid",(long)uid)==DBNull.Value){
                Require(!reload && p.Inventory.Count==0 && p.Entity.Class==schools[n]);
                NobilityClaimPacket(p,105);Require(p.Inventory.Count==0);
                p.BindWorldMap(1004);HeartTalk(p,trainers[n],1);HeartTalk(p,trainers[n],schools[n]==100?(byte)254:schools[n]==160?(byte)2:(byte)3);
                Require(p.Entity.Class==schools[n]+1 && !PirateFenced(p));
                System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,105));var reward=p.Inventory.Objects.Single();
                Require(reward.ID==723700 && reward.Bound && reward.Plus==0 && reward.StackSize==1);
                ulong before=p.Entity.Experience;byte level=p.Entity.Level;SoulClaimUse(p,reward);
                Require(p.Entity.Level>level || p.Entity.Experience>before);
                Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_promotion_claim_probe SET snapshot=@snapshot WHERE uid=@uid","snapshot",PromotionClaimSnapshot(p),"uid",(long)uid);
            }
            Require(p.Entity.Class==schools[n]+1 && p.Inventory.Count==0);
            Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=105 AND NOT legacy","uid",(long)uid)==1);
            Require(Convert.ToInt32(Sql("SELECT used FROM revora_exp_ball_days WHERE uid=@uid","uid",(long)uid))==1);
            string exact=PromotionClaimSnapshot(p);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_promotion_claim_probe WHERE uid=@uid","uid",(long)uid)));
            NobilityClaimPacket(p,105);Require(exact==PromotionClaimSnapshot(p) && !PirateFenced(p));
        }
        System.Console.WriteLine("[career-process] career-promotion-claim-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Promotion claim did not close.");
    }
}
