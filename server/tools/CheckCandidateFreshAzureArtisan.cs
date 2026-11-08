using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshAzureArtisanSnapshot(GameState p) {return Convert.ToString(Sql(@"SELECT json_build_object('prior',@prior::json,'azure',(SELECT json_agg(r ORDER BY life) FROM revora_azure_pill r WHERE uid=@uid),'artisan',(SELECT json_agg(r ORDER BY life) FROM revora_artisan_feather r WHERE uid=@uid))::text","prior",FreshClassicSnapshot(p),"uid",(long)p.Entity.UID));}
    static void FreshAzureArtisanSave(GameState p,string stage,int kills,int hits) {Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_azure_artisan_probe SET stage=@stage,kills=@kills,hits=@hits,snapshot=@snapshot","stage",stage,"kills",kills,"hits",hits,"snapshot",FreshAzureArtisanSnapshot(p));}
    static void FreshAzureArtisanProcess(string phase) {
        var p=FreshLoadSaved();EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_fresh_azure_artisan_probe') IS NULL")) {
            Require(phase.EndsWith("earn") && FreshClassicSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_classic_probe")) && (long)Sql("SELECT (SELECT count(*) FROM revora_azure_pill WHERE uid=@uid)+(SELECT count(*) FROM revora_artisan_feather WHERE uid=@uid)","uid",(long)p.Entity.UID)==0);
            Sql("CREATE TABLE revora_fresh_azure_artisan_probe(stage text,kills integer,hits integer,snapshot text);INSERT INTO revora_fresh_azure_artisan_probe VALUES('new',0,0,@snapshot)","snapshot",FreshAzureArtisanSnapshot(p));
        }
        if(phase.EndsWith("inspect")) {
            System.IO.File.WriteAllText("fresh-azure-recovery-before.json",Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_azure_artisan_probe")));
            System.IO.File.WriteAllText("fresh-azure-recovery-current.json",FreshAzureArtisanSnapshot(p));
            System.Console.WriteLine("[fresh-azure-inspect] level="+p.Entity.Level+" map="+p.Entity.MapID+" weapon="+p.Equipment.TryGetItem(4).ID+" azure="+Sql("SELECT stage FROM revora_azure_pill WHERE uid=@uid AND life=2","uid",(long)p.Entity.UID)+" artisan="+Sql("SELECT stage FROM revora_artisan_feather WHERE uid=@uid AND life=2","uid",(long)p.Entity.UID));
            System.Console.WriteLine("[career-process] "+phase+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Fresh inspection did not close.");
        }
        if(phase.EndsWith("earn") && FreshAzureArtisanSnapshot(p)!=Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_azure_artisan_probe"))) {
            // One inspected interrupted fixture: never reset the earned quest or player rows.
            Func<string,string> digest=value=>{using(var sha=System.Security.Cryptography.SHA256.Create())return System.BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant();};
            Require(digest(Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_azure_artisan_probe")))=="c212946f2edbadc489a776756dc4c13cc768ca01965bbe8a3ddcbbf049d994c6" && digest(FreshAzureArtisanSnapshot(p))=="d752cb03f10536bea654bcc193ea1c1fd63577982e534334dbc0e2d9f725b270");
            Require((long)Sql("SELECT count(*) FROM revora_azure_pill WHERE uid=@uid AND life=2 AND stage=6 AND cardinality(rewards)=0","uid",(long)p.Entity.UID)==1 && (long)Sql("SELECT count(*) FROM revora_artisan_feather WHERE uid=@uid AND life=2 AND stage=3 AND feathers=79 AND cardinality(rewards)=0","uid",(long)p.Entity.UID)==1);
            Sql("UPDATE revora_fresh_azure_artisan_probe SET stage='artisan',snapshot=@snapshot WHERE stage='new'","snapshot",FreshAzureArtisanSnapshot(p));
            System.Console.WriteLine("[fresh-azure-artisan] recovered inspected progress; counters below cover remaining combat only");
        }
        Require(FreshAzureArtisanSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_azure_artisan_probe")));string stage=Convert.ToString(Sql("SELECT stage FROM revora_fresh_azure_artisan_probe"));int kills=Convert.ToInt32(Sql("SELECT kills FROM revora_fresh_azure_artisan_probe")),hits=Convert.ToInt32(Sql("SELECT hits FROM revora_fresh_azure_artisan_probe"));
        if(phase.EndsWith("earn")) {
            Require((stage=="new" || stage=="artisan") && p.Inventory.Count<=33);if(stage=="new"){FreshClassicTalk(p,1002,900071727,1);FreshClassicTalk(p,1011,900071728,1);FreshClassicTalk(p,1011,900071729,1);
            for(int i=0;i<100 && !(bool)Sql("SELECT eye>0 AND cardinality(meat)=3 FROM revora_azure_pill WHERE uid=@uid AND life=2","uid",(long)p.Entity.UID);i++)FreshClassicKill(p,1011,67,ref kills,ref hits);
            Require((bool)Sql("SELECT eye>0 AND cardinality(meat)=3 FROM revora_azure_pill WHERE uid=@uid AND life=2","uid",(long)p.Entity.UID));FreshClassicTalk(p,1002,900071730,1);FreshClassicTalk(p,1011,900071729,1);
            Require((long)Sql("SELECT count(*) FROM revora_azure_pill WHERE uid=@uid AND life=2 AND stage=4 AND ready_at IS NOT NULL","uid",(long)p.Entity.UID)==1);string before=FreshAzureArtisanSnapshot(p);FreshClassicTalk(p,1011,900071729,1);Require(before==FreshAzureArtisanSnapshot(p));
            var clock=Feature("AzurePillQuest").GetField("Clock",Hidden);object original=clock.GetValue(null);DateTime ready=DateTime.SpecifyKind(Convert.ToDateTime(Sql("SELECT ready_at AT TIME ZONE 'UTC' FROM revora_azure_pill WHERE uid=@uid AND life=2","uid",(long)p.Entity.UID)),DateTimeKind.Utc);
            try {clock.SetValue(null,(Func<DateTime>)(()=>ready.AddSeconds(1)));FreshClassicTalk(p,1011,900071729,1);FreshClassicTalk(p,1011,900071728,1);} finally {clock.SetValue(null,original);}
            Require((long)Sql("SELECT count(*) FROM revora_azure_pill WHERE uid=@uid AND life=2 AND stage=6 AND cardinality(rewards)=0","uid",(long)p.Entity.UID)==1);
            FreshClassicTalk(p,1002,900071727,71);FreshClassicTalk(p,1002,4466,1);FreshClassicTalk(p,1002,4467,1);}
            int feathers=Convert.ToInt32(Sql("SELECT feathers FROM revora_artisan_feather WHERE uid=@uid AND life=2","uid",(long)p.Entity.UID));for(int i=feathers;i<100;i++)FreshClassicKill(p,1002,1,ref kills,ref hits);Require((long)Sql("SELECT count(*) FROM revora_artisan_feather WHERE uid=@uid AND life=2 AND stage=3 AND feathers=100","uid",(long)p.Entity.UID)==1);
            FreshClassicTalk(p,1002,4467,1);for(int i=0;!p.Inventory.Contains(729976,1) && i<100;i++)FreshClassicKill(p,1020,2143,ref kills,ref hits);Require(p.Inventory.Contains(729976,1));FreshClassicTalk(p,1002,4467,1);FreshClassicTalk(p,1002,900071727,0);
            Require((long)Sql("SELECT count(*) FROM revora_artisan_feather WHERE uid=@uid AND life=2 AND stage=5 AND cardinality(rewards)=0","uid",(long)p.Entity.UID)==1);FreshAzureArtisanSave(p,"ready",kills,hits);
        } else if(phase.EndsWith("claim")) {
            Require(stage=="ready");var prior=p.Inventory.Objects.Select(i=>i.UID).ToArray();FreshClassicTalk(p,1002,900071727,1);FreshClassicTalk(p,1002,900071727,71);var added=p.Inventory.Objects.Where(i=>!prior.Contains(i.UID)).ToArray();Require(added.Length==4 && added.All(i=>i.Bound) && prior.All(uid=>p.Inventory.ContainsUID(uid)));
            Require((long)Sql("SELECT count(*) FROM revora_azure_pill WHERE uid=@uid AND life=2 AND stage=7 AND cardinality(rewards)=2","uid",(long)p.Entity.UID)==1 && (long)Sql("SELECT count(*) FROM revora_artisan_feather WHERE uid=@uid AND life=2 AND stage=6 AND cardinality(rewards)=2","uid",(long)p.Entity.UID)==1);FreshAzureArtisanSave(p,"paid",kills,hits);
        } else Require(phase.EndsWith("reload") && stage=="paid");
        if(!phase.EndsWith("earn")){
            var ids=((long[])Sql("SELECT rewards FROM revora_azure_pill WHERE uid=@uid AND life=2","uid",(long)p.Entity.UID)).Concat((long[])Sql("SELECT rewards FROM revora_artisan_feather WHERE uid=@uid AND life=2","uid",(long)p.Entity.UID)).ToArray();Require(ids.Length==4 && ids.Distinct().Count()==4);var rewards=p.Inventory.Objects.Where(i=>ids.Contains((long)i.UID)).ToArray();Require(rewards.Length==4 && rewards.All(i=>i.Bound) && rewards.Count(i=>i.ID==728238)==1 && rewards.Count(i=>i.ID==728239)==1 && rewards.Count(i=>ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)==2);
            string before=FreshAzureArtisanSnapshot(p);FreshClassicTalk(p,1002,900071727,1);FreshClassicTalk(p,1002,900071727,71);Require(before==FreshAzureArtisanSnapshot(p));
        }
        Require(FreshAzureArtisanSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_azure_artisan_probe")));System.Console.WriteLine("[career-process] "+phase+"-passed uid="+p.Entity.UID+" kills="+kills+" hits="+hits+" bag="+p.Inventory.Count);Server.CommandsAI("@close");throw new InvalidOperationException("Fresh Azure/Artisan did not close.");
    }
}
