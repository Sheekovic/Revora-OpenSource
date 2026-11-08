using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshMagnoliaSnapshot(GameState p) { return Convert.ToString(Sql(@"SELECT json_build_object('prior',@prior::json,'flowers',(SELECT json_agg(r ORDER BY day) FROM revora_magnolia_runs r WHERE uid=@uid),'bonuses',(SELECT json_agg(r ORDER BY day) FROM revora_magnolia_bonuses r WHERE uid=@uid))::text","prior",FreshSpookSnapshot(p),"uid",(long)p.Entity.UID)); }
    static void FreshMagnoliaSave(GameState p,string stage) { Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_magnolia_probe SET stage=@stage,snapshot=@snapshot","stage",stage,"snapshot",FreshMagnoliaSnapshot(p)); }
    static void FreshMagnoliaProcess(string phase) {
        var p=FreshLoadSaved();EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();DialyQuestTable.Load(p);Call(Feature("MagnoliaQuest"),"Refresh",p);
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_fresh_magnolia_probe') IS NULL")) {
            Require(phase.EndsWith("seed") && FreshSpookSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_spook_probe")) && (long)Sql("SELECT count(*) FROM revora_magnolia_runs WHERE uid=@uid","uid",(long)p.Entity.UID)==0);
            Sql("CREATE TABLE revora_fresh_magnolia_probe(stage text,snapshot text);INSERT INTO revora_fresh_magnolia_probe VALUES('new',@snapshot)","snapshot",FreshMagnoliaSnapshot(p));
        }
        Require(FreshMagnoliaSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_magnolia_probe")));
        string stage=Convert.ToString(Sql("SELECT stage FROM revora_fresh_magnolia_probe"));
        if(phase.EndsWith("seed")) {
            Require(stage=="new");p.Entity.Teleport(1002,Config.DailyEnvoyX,Config.DailyEnvoyY);HeartTalk(p,79882,5);Require(p.Entity.MapID==1806);uint cp=p.Entity.ConquerPoints;
            HeartTalk(p,44100,1);HeartTalk(p,44100,4);Require(!PirateFenced(p) && p.Entity.ConquerPoints==cp-Config.DailyMagnoliaSuperSeedCp && p.Inventory.Objects.Count(i=>i.ID==Config.DailyMagnoliaSuperSeed)==1);
            string before=FreshMagnoliaSnapshot(p);HeartTalk(p,44100,4);Require(before==FreshMagnoliaSnapshot(p));FreshMagnoliaSave(p,"ready");
        } else if(phase.EndsWith("plant")) {
            Require(stage=="ready");var seed=p.Inventory.Objects.Single(i=>i.ID==Config.DailyMagnoliaSuperSeed);uint cp=p.Entity.ConquerPoints,rvp=p.Entity.RevoraPoint;ushort study=p.Entity.SubClasses.StudyPoints;var prior=p.Inventory.Objects.Select(i=>i.UID).ToArray();
            p.Entity.X=98;p.Entity.Y=111;ItemHandler.UseItem(seed,p);Require(!PirateFenced(p) && !p.Inventory.ContainsUID(seed.UID) && p.Entity.ConquerPoints==cp+Config.DailyQuestCp && p.Entity.RevoraPoint==rvp+Config.DailyQuestRevoraPoints && p.Entity.SubClasses.StudyPoints==study+300);
            int roll=Convert.ToInt32(Sql("SELECT roll FROM revora_magnolia_bonuses WHERE uid=@uid","uid",(long)p.Entity.UID));uint bonus=Convert.ToUInt32(Sql("SELECT item_type FROM revora_magnolia_bonuses WHERE uid=@uid","uid",(long)p.Entity.UID));Require(roll>=0 && roll<100 && ((roll<20)==(bonus!=0)));
            if(bonus!=0)Require(new uint[]{1088000,720027,720578,721259,754009,756009,710212,720957}.Contains(bonus));
            var rewards=p.Inventory.Objects.Where(i=>!prior.Contains(i.UID)).ToArray();Require(rewards.Length==(bonus==0?2:3) && rewards.Any(i=>i.ID==Config.DailyQuestChiToken) && rewards.Any(i=>i.ID==4030101 && i.Bound) && (bonus==0 || rewards.Count(i=>i.ID==bonus)==1));
            Require((long)Sql("SELECT count(*) FROM revora_magnolia_runs WHERE uid=@uid AND completed AND planted_seed=issued_seed AND fee=@fee AND rune_uid IS NOT NULL AND soul_uid IS NULL","uid",(long)p.Entity.UID,"fee",(long)Config.DailyMagnoliaSuperSeedCp)==1);FreshMagnoliaSave(p,"paid");
            string before=FreshMagnoliaSnapshot(p);ItemHandler.UseItem(seed,p);Require(before==FreshMagnoliaSnapshot(p));System.Console.WriteLine("[fresh-magnolia] bonus-roll="+roll+" bonus-item="+bonus);
        } else Require(phase.EndsWith("reload") && stage=="paid");
        if(!phase.EndsWith("seed")) {HeartTalk(p,44100,0);string before=FreshMagnoliaSnapshot(p);HeartTalk(p,44100,1);HeartTalk(p,44100,4);Require(before==FreshMagnoliaSnapshot(p));FreshMagnoliaSave(p,"paid");}
        Require(FreshMagnoliaSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_magnolia_probe")));
        System.Console.WriteLine("[career-process] "+phase+"-passed uid="+p.Entity.UID+" cps="+p.Entity.ConquerPoints+" rvp="+p.Entity.RevoraPoint+" study="+p.Entity.SubClasses.StudyPoints);
        Server.CommandsAI("@close");throw new InvalidOperationException("Fresh Magnolia did not close.");
    }
}
