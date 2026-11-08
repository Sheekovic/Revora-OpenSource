using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static bool lotteryClaimFixture;
    static string LotteryClaimSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object(
'base',@snapshot::json,'draws',(SELECT json_agg(d ORDER BY id) FROM revora_lottery_draws d WHERE uid=@uid),
'payments',(SELECT json_agg(s ORDER BY s.draw_id,s.step) FROM revora_lottery_steps s JOIN revora_lottery_draws d ON d.id=s.draw_id WHERE d.uid=@uid))::text",
        "snapshot",PromotionClaimSnapshot(p),"uid",(long)p.Entity.UID));}
    static void LotteryClaimProcess(bool reload){
        lotteryClaimFixture=true;const uint uid=4294911501;
        if((bool)Sql("SELECT to_regclass('revora_lottery_claim_probe') IS NULL")){
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==0);
            nextUid=uid;Player(Kernel.Maps[1002]);Sql("UPDATE entities SET level=110,reborn=0,conquerpoints=100000 WHERE uid=@uid","uid",(long)uid);
            Sql("CREATE TABLE revora_lottery_claim_probe(snapshot text);INSERT INTO revora_lottery_claim_probe VALUES(NULL)");
        }
        nextUid=uid;var p=Player(Kernel.Maps[1002],true);ChiTable.Load(p);WayofHeroes.Load(p);
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if(Sql("SELECT snapshot FROM revora_lottery_claim_probe")==DBNull.Value){Require(!reload);
            var magnolia=Feature("MagnoliaQuest");var clock=magnolia.GetField("Clock",Hidden);var bonus=magnolia.GetField("BonusRoll",Hidden);var roll=Feature("LotteryDraws").GetField("Roll",Hidden);
            object oldClock=clock.GetValue(null),oldBonus=bonus.GetValue(null),oldRoll=roll.GetValue(null);
            try{DateTime now=new DateTime(2037,2,1,20,0,0,DateTimeKind.Utc);clock.SetValue(null,(Func<DateTime>)(()=>now));bonus.SetValue(null,(Func<int,int>)(max=>max==100?0:6));roll.SetValue(null,(Func<int,int>)(max=>0));
                string before=LotteryClaimSnapshot(p);NobilityClaimPacket(p,306);Require(before==LotteryClaimSnapshot(p));
                if(!p.Inventory.Contains(711504,3)){Require(p.Inventory.Count==0);
                HeartTalk(p,79882,5);Require(p.Entity.MapID==1806);HeartTalk(p,44100,1);uint cp=p.Entity.ConquerPoints;HeartTalk(p,44100,4);
                Require(p.Entity.ConquerPoints==cp-Config.DailyMagnoliaSuperSeedCp);var seed=p.Inventory.Objects.Single(i=>i.ID==729310);p.Entity.X=98;p.Entity.Y=111;SoulClaimUse(p,seed);
                Require(p.Inventory.Contains(710212,1));HeartTalk(p,44100,2);HeartTalk(p,923,2);
                Require(!p.Inventory.Contains(710212,1) && p.Inventory.Objects.Where(i=>i.ID==711504).Sum(i=>Math.Max(1,(int)i.StackSize))==3);
                }
                p.Entity.Teleport(1002,400,350);HeartTalk(p,923,1);Require(p.Entity.MapID==700);string entered=LotteryClaimSnapshot(p);NobilityClaimPacket(p,306);Require(entered==LotteryClaimSnapshot(p));
                HeartTalk(p,925,1);Require(!p.Inventory.Contains(711504,1));
                Require((long)Sql("SELECT count(*) FROM revora_lottery_draws WHERE uid=@uid AND NOT claimed","uid",(long)uid)==1);
                string pending=LotteryClaimSnapshot(p);NobilityClaimPacket(p,306);Require(pending==LotteryClaimSnapshot(p));
                var frame=new byte[14];Writer.WriteUInt16(6,0,frame);Writer.WriteUInt16(1314,2,frame);Writer.WriteUInt64(PacketHandler.ClientSeal,6,frame);PacketHandler.HandlePacket(frame,p);
                Require((long)Sql("SELECT count(*) FROM revora_lottery_draws WHERE uid=@uid AND claimed","uid",(long)uid)==1);
                string claimed=LotteryClaimSnapshot(p);PacketHandler.HandlePacket(frame,p);Require(claimed==LotteryClaimSnapshot(p));
                var beforeItems=p.Inventory.Objects.Select(i=>i.UID).ToArray();System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,306));
                var balls=p.Inventory.Objects.Where(i=>!beforeItems.Contains(i.UID)).ToArray();Require(balls.Length==3 && balls.All(i=>i.ID==723700 && i.Bound && i.StackSize==1));
                foreach(var ball in balls){ulong experience=p.Entity.Experience;byte level=p.Entity.Level;SoulClaimUse(p,ball);Require(p.Entity.Level>level || p.Entity.Experience>experience);}
            }finally{clock.SetValue(null,oldClock);bonus.SetValue(null,oldBonus);roll.SetValue(null,oldRoll);}
            Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_lottery_claim_probe SET snapshot=@snapshot","snapshot",LotteryClaimSnapshot(p));
        }
        Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=306 AND NOT legacy","uid",(long)uid)==1);
        Require(Convert.ToInt32(Sql("SELECT used FROM revora_exp_ball_days WHERE uid=@uid","uid",(long)uid))==3);
        string exact=LotteryClaimSnapshot(p);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_lottery_claim_probe")));
        NobilityClaimPacket(p,306);Require(exact==LotteryClaimSnapshot(p) && !PirateFenced(p));
        System.Console.WriteLine("[career-process] career-lottery-claim-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Lottery claim did not close.");
    }
}
