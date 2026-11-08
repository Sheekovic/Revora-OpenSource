using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static bool potionClaimFixture;
    static string PotionClaimSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object(
'base',@snapshot::json,'seconds',(SELECT doubleexptime FROM entities WHERE uid=@uid),
'potions',(SELECT json_agg(u ORDER BY id) FROM revora_exp_potion_uses u WHERE uid=@uid))::text",
        "snapshot",NobilityClaimSnapshot(p),"uid",(long)p.Entity.UID));}
    static void PotionClaimProcess(bool reload){
        potionClaimFixture=true;const uint uid=4294910201;
        if((bool)Sql("SELECT to_regclass('revora_potion_claim_probe') IS NULL")){
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==0);
            nextUid=uid;Player(Kernel.Maps[1002]);Sql("UPDATE entities SET level=50,reborn=0,conquerpoints=50000 WHERE uid=@uid","uid",(long)uid);
            Sql("CREATE TABLE revora_potion_claim_probe(snapshot text);INSERT INTO revora_potion_claim_probe VALUES(NULL)");
        }
        nextUid=uid;var p=Player(Kernel.Maps[1002],true);ChiTable.Load(p);WayofHeroes.Load(p);
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if(Sql("SELECT snapshot FROM revora_potion_claim_probe")==DBNull.Value){
            Require(!reload);
            bool claimed=(long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=206","uid",(long)uid)==1;
            if(!claimed){
            Require(p.Inventory.Count==0 && p.Entity.DoubleExperienceTime==0);
            // An unrelated timer grant must not qualify the potion-use milestone.
            p.Entity.DoubleExperienceTime=3600;NobilityClaimPacket(p,206);Require(p.Inventory.Count==0);
            p.Entity.DoubleExperienceTime=0;
            var shortPotion=RefineryClaimGive(p,722700);SoulClaimUse(p,shortPotion);
            Require(p.Entity.DoubleExperienceTime==600 && !p.Inventory.ContainsUID(shortPotion.UID));
            NobilityClaimPacket(p,206);Require(p.Inventory.Count==0);
            // Controlled expiry via the same durable balance setter used by countdowns.
            p.Entity.DoubleExperienceTime=0;CareerBuy(p,723017);var bought=p.Inventory.Objects.Single();
            Require(bought.ID==723017);SoulClaimUse(p,bought);
            Require(p.Entity.DoubleExperienceTime==3600 && p.Inventory.Count==0);
            System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,206));
            }else{
                Require(p.Entity.DoubleExperienceTime==3600);
                Require((long)Sql("SELECT count(*) FROM revora_exp_potion_uses WHERE uid=@uid AND item_type=723017","uid",(long)uid)==1);
                Require((long)Sql("SELECT count(*) FROM revora_exp_potion_uses WHERE uid=@uid AND item_type=722700","uid",(long)uid)==1);
            }
            var reward=p.Inventory.Objects.Single();Require(reward.ID==723017 && reward.Bound && reward.Plus==0 && reward.StackSize==1);
            string active=PotionClaimSnapshot(p);
            RetainedUpgradeUsage(p,reward.UID,0,ItemUsage.Mode.EquipItem);RetainedUpgradeUsage(p,reward.UID,0,ItemUsage.Mode.EquipItem);Require(active==PotionClaimSnapshot(p) && p.Inventory.ContainsUID(reward.UID));
            // Keep the reward while the bonus is active; expiry then allows one use.
            p.Entity.DoubleExperienceTime=0;SoulClaimUse(p,reward);
            Require(p.Entity.DoubleExperienceTime==3600 && p.Inventory.Count==0 && !PirateFenced(p));
            Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_potion_claim_probe SET snapshot=@snapshot","snapshot",PotionClaimSnapshot(p));
        }
        Require(p.Entity.DoubleExperienceTime==3600 && p.Inventory.Count==0);
        Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=206 AND NOT legacy","uid",(long)uid)==1);
        Require((long)Sql("SELECT count(*) FROM revora_exp_potion_uses WHERE uid=@uid AND item_type=723017 AND seconds=3600","uid",(long)uid)==2);
        Require((long)Sql("SELECT count(*) FROM revora_exp_potion_uses WHERE uid=@uid AND item_type=722700 AND seconds=600","uid",(long)uid)==1);
        string exact=PotionClaimSnapshot(p);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_potion_claim_probe")));
        NobilityClaimPacket(p,206);Require(exact==PotionClaimSnapshot(p) && !PirateFenced(p));
        System.Console.WriteLine("[career-process] career-potion-claim-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Potion claim did not close.");
    }
}
