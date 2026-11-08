using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshAncientSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object('prior',@prior::json,'trials',(SELECT json_agg(r ORDER BY id) FROM revora_ancient_trials r WHERE uid=@uid),'lives',(SELECT json_agg(l ORDER BY l.id) FROM revora_ancient_lives l JOIN revora_ancient_trials r ON r.id=l.trial WHERE r.uid=@uid),'collection',(SELECT row_to_json(r) FROM revora_ancient_collections r WHERE uid=@uid))::text","prior",FreshLabSnapshot(p),"uid",(long)p.Entity.UID));}
    static void FreshAncientSave(GameState p,string stage,int kills,int hits){Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_ancient_probe SET stage=@stage,kills=@kills,hits=@hits,snapshot=@snapshot","stage",stage,"kills",kills,"hits",hits,"snapshot",FreshAncientSnapshot(p));}
    static void FreshAncientProcess(string phase){
        var p=FreshLoadSaved(true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_fresh_ancient_probe') IS NULL")){
            Require(phase.EndsWith("earn") && FreshLabSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_lab_probe")) && (long)Sql("SELECT count(*) FROM revora_ancient_trials WHERE uid=@uid","uid",(long)p.Entity.UID)==0);Sql("CREATE TABLE revora_fresh_ancient_probe(stage text,kills integer,hits integer,snapshot text);INSERT INTO revora_fresh_ancient_probe VALUES('new',0,0,@snapshot)","snapshot",FreshAncientSnapshot(p));
        }
        if(FreshAncientSnapshot(p)!=Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_ancient_probe"))){
            Func<string,string> digest=value=>{using(var sha=System.Security.Cryptography.SHA256.Create())return System.BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant();};
            // Reviewed startup recovery retained one abandoned, undefeated trial and the island return only.
            Require(phase.EndsWith("earn") && digest(Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_ancient_probe")))=="c8a8d744fa388b34c3509d85eca8a37d9960ee7c702c50332db28df319f85092" && digest(FreshAncientSnapshot(p))=="8e956605b2695c2d19b59e039aa5f8f08f6d7cca59450202f6646a561992d3de");Sql("UPDATE revora_fresh_ancient_probe SET snapshot=@snapshot WHERE stage='new'","snapshot",FreshAncientSnapshot(p));
        }
        Require(FreshAncientSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_ancient_probe")));string stage=Convert.ToString(Sql("SELECT stage FROM revora_fresh_ancient_probe"));int kills=Convert.ToInt32(Sql("SELECT kills FROM revora_fresh_ancient_probe")),hits=Convert.ToInt32(Sql("SELECT hits FROM revora_fresh_ancient_probe"));
        if(phase.EndsWith("earn")){
            Require(stage=="new" && p.Entity.Class==25 && p.Inventory.Count<=39);FreshClassicTalk(p,1015,300010,0);HeartTalk(p,300010,1);Require(p.Entity.MapID==1052);HeartTalk(p,300011,0);string before=FreshAncientSnapshot(p);HeartTalk(p,300011,2);Require(before==FreshAncientSnapshot(p));HeartTalk(p,300011,1);Require(p.Entity.MapID>=11000);FreshClassicKill(p,p.Entity.MapID,9916501,ref kills,ref hits);Call(Feature("AncientDevilQuest"),"Tick");Require(p.Entity.MapID==1052 && (long)Sql("SELECT count(*) FROM revora_ancient_trials WHERE uid=@uid AND profession=25 AND state='victory' AND certificate_uid IS NULL","uid",(long)p.Entity.UID)==1);FreshAncientSave(p,"victory",kills,hits);
        }else if(phase.EndsWith("certificate")){
            Require(stage=="victory");var prior=p.Inventory.Objects.Select(i=>i.UID).ToArray();HeartTalk(p,300011,0);HeartTalk(p,300011,2);var added=p.Inventory.Objects.Where(i=>!prior.Contains(i.UID)).ToArray();Require(added.Length==1 && added[0].ID==710016 && added[0].Bound && prior.All(uid=>p.Inventory.ContainsUID(uid)));FreshAncientSave(p,"certificate",kills,hits);
        }else if(phase.EndsWith("exchange")){
            Require(stage=="certificate");var cert=p.Inventory.Objects.Single(i=>i.ID==710016);var prior=p.Inventory.Objects.Where(i=>i.UID!=cert.UID).Select(i=>i.UID).ToArray();HeartTalk(p,300011,0);HeartTalk(p,300011,3);var added=p.Inventory.Objects.Where(i=>!prior.Contains(i.UID)).ToArray();Require(added.Length==1 && added[0].ID==710011 && !added[0].Bound && !p.Inventory.ContainsUID(cert.UID) && prior.All(uid=>p.Inventory.ContainsUID(uid)));FreshAncientSave(p,"exchanged",kills,hits);
        }else Require(phase.EndsWith("reload") && stage=="exchanged");
        if(!phase.EndsWith("earn")){
            string exact=FreshAncientSnapshot(p);HeartTalk(p,300011,2);if(stage=="exchanged" || phase.EndsWith("exchange"))HeartTalk(p,300011,3);Require(exact==FreshAncientSnapshot(p));
            if(!phase.EndsWith("certificate"))Require((bool)Sql("SELECT EXISTS(SELECT 1 FROM revora_ancient_trials r JOIN items i ON i.uid=r.amulet_uid WHERE r.uid=@uid AND r.state='exchanged' AND i.entityid=r.uid AND i.id=710011 AND i.bound=0)","uid",(long)p.Entity.UID));
        }
        Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11020","uid",(long)p.Entity.UID)==0 && !PirateFenced(p) && FreshAncientSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_ancient_probe")));System.Console.WriteLine("[career-process] "+phase+"-passed uid="+p.Entity.UID+" kills="+kills+" hits="+hits+" bag="+p.Inventory.Count);Server.CommandsAI("@close");throw new InvalidOperationException("Fresh Ancient trial did not close.");
    }
}
