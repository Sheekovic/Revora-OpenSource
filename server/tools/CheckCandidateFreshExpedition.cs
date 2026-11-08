using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshExpeditionSnapshot(GameState p) {
        return Convert.ToString(Sql(@"SELECT json_build_object('prior',@prior::json,
'run',(SELECT json_agg(r ORDER BY life) FROM revora_desert_expedition r WHERE uid=@uid),
'materials',(SELECT json_agg(r ORDER BY item_uid) FROM revora_desert_expedition_materials r WHERE uid=@uid),
'recoveries',(SELECT json_agg(r ORDER BY old_uid) FROM revora_desert_expedition_recoveries r WHERE uid=@uid),
'traps',(SELECT json_agg(r ORDER BY life) FROM revora_desert_expedition_traps r WHERE uid=@uid),
'kills',(SELECT json_agg(r ORDER BY attempt) FROM revora_desert_expedition_encounter_kills r WHERE uid=@uid),
'payments',(SELECT json_agg(r ORDER BY life,until_at,paid_after) FROM revora_desert_expedition_pill_payments r WHERE uid=@uid))::text","prior",FreshFamilyTreasureSnapshot(p),"uid",(long)p.Entity.UID));
    }
    static void FreshExpeditionSave(GameState p,string stage,int kills,int hits) {
        Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_expedition_probe SET stage=@stage,kills=@kills,hits=@hits,snapshot=@snapshot","stage",stage,"kills",kills,"hits",hits,"snapshot",FreshExpeditionSnapshot(p));
    }
    static void FreshExpeditionProcess(string phase) {
        var p=FreshLoadSaved(true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_fresh_expedition_probe') IS NULL")) {
            Require(phase.EndsWith("earn") && FreshFamilyTreasureSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_family_treasure_probe")) && (long)Sql("SELECT count(*) FROM revora_desert_expedition WHERE uid=@uid","uid",(long)p.Entity.UID)==0);
            Sql("CREATE TABLE revora_fresh_expedition_probe(stage text,kills integer,hits integer,snapshot text);INSERT INTO revora_fresh_expedition_probe VALUES('new',0,0,@snapshot)","snapshot",FreshExpeditionSnapshot(p));
        }
        Require(FreshExpeditionSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_expedition_probe")));
        string stage=Convert.ToString(Sql("SELECT stage FROM revora_fresh_expedition_probe"));int kills=Convert.ToInt32(Sql("SELECT kills FROM revora_fresh_expedition_probe")),hits=Convert.ToInt32(Sql("SELECT hits FROM revora_fresh_expedition_probe"));
        if(phase.EndsWith("earn")) {
            Require(stage=="new" && p.Inventory.Count<=26);var clock=Feature("DesertExpeditionStore").GetField("Clock",Hidden);object original=clock.GetValue(null);DateTime now=DateTime.UtcNow;
            try {
                clock.SetValue(null,(Func<DateTime>)(()=>now));FreshClassicTalk(p,1002,900071727,181);FreshClassicTalk(p,1000,4702,1);p.Entity.Teleport(1001,173,329);uint health=p.Entity.Hitpoints;DateTime activated=now;
                ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==721920),p);
                for(int i=0;p.Inventory.Objects.Count(item=>item.ID==710259)<10 && i<200;i++){now=now.AddMilliseconds(200);FreshDesertHeroKill(p,1001,new uint[]{57,82},ref kills,ref hits);}
                Require(p.Inventory.Objects.Count(i=>i.ID==710259)==10);FreshClassicTalk(p,1000,4702,1);Require(p.Inventory.Objects.Count(i=>i.ID==710259)==10 && Convert.ToInt32(Sql("SELECT stage FROM revora_desert_expedition WHERE uid=@uid AND life=2","uid",(long)p.Entity.UID))==2);
                now=activated.AddSeconds(60);Call(Feature("DesertExpeditionQuest"),"Tick");Require(p.Entity.Hitpoints>0 && p.Entity.Hitpoints<health && Convert.ToInt32(Sql("SELECT pill_paid FROM revora_desert_expedition WHERE uid=@uid AND life=2","uid",(long)p.Entity.UID))==6 && Convert.ToInt32(Sql("SELECT sum(paid_after-paid_before) FROM revora_desert_expedition_pill_payments WHERE uid=@uid AND life=2","uid",(long)p.Entity.UID))==6);
                FreshClassicTalk(p,1000,4702,1);ushort[,] traps={{172,399},{102,292},{109,369}};
                for(int site=0;site<3;site++) {
                    p.Entity.Teleport(1001,traps[site,0],traps[site,1]);now=now.AddSeconds(2);ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==721921),p);
                    var bull=p.Map.Entities.Values.Single(b=>b.MonsterInfo!=null && b.MonsterInfo.ID==9918350 && !b.Dead);p.Entity.X=(ushort)(traps[site,0]-3);var until=DateTime.UtcNow.AddSeconds(3);
                    while(Kernel.GetDistance(bull.X,bull.Y,traps[site,0],traps[site,1])>1 && DateTime.UtcNow<until){bull.MonsterInfo.LastMove=new Time32(1);Call(Feature("DesertExpeditionEncounters"),"DriveOwned",p,bull,Time32.Now);System.Threading.Thread.Sleep(10);}
                    Require(Kernel.GetDistance(bull.X,bull.Y,traps[site,0],traps[site,1])<=1);FreshClassicKill(p,1001,9918350,ref kills,ref hits);Call(Feature("DesertExpeditionQuest"),"Tick");Require(p.Inventory.Objects.Count(i=>i.ID==710261)==site+1);
                }
                FreshClassicTalk(p,1000,4702,1);p.Entity.Teleport(1001,185,359);Revora.Game.Entity master=null;var deadline=DateTime.UtcNow.AddSeconds(3);
                do{now=now.AddSeconds(2);Call(Feature("DesertExpeditionQuest"),"Tick");master=p.Map.Entities.Values.SingleOrDefault(b=>b.MonsterInfo!=null && b.MonsterInfo.ID==9918351 && !b.Dead);if(master==null)System.Threading.Thread.Sleep(10);}while(master==null && DateTime.UtcNow<deadline);
                Require(master!=null);FreshClassicKill(p,1001,9918351,ref kills,ref hits);Call(Feature("DesertExpeditionQuest"),"Tick");Require(p.Inventory.Contains(710260,1));FreshClassicTalk(p,1000,4702,1);FreshClassicTalk(p,1002,900071727,0);
            } finally {clock.SetValue(null,original);}
            Require((long)Sql("SELECT count(*) FROM revora_desert_expedition WHERE uid=@uid AND life=2 AND stage=5 AND cardinality(rewards)=0","uid",(long)p.Entity.UID)==1 && (long)Sql("SELECT count(*) FROM revora_desert_expedition_encounter_kills WHERE uid=@uid AND life=2 AND won","uid",(long)p.Entity.UID)==4);FreshExpeditionSave(p,"ready",kills,hits);
        } else if(phase.EndsWith("claim")) {
            Require(stage=="ready");var prior=p.Inventory.Objects.Select(i=>i.UID).ToArray();FreshClassicTalk(p,1002,900071727,181);var added=p.Inventory.Objects.Where(i=>!prior.Contains(i.UID)).ToArray();Require(added.Length==3 && added.All(i=>i.Bound) && prior.All(uid=>p.Inventory.ContainsUID(uid)));
            Require((long)Sql("SELECT count(*) FROM revora_desert_expedition WHERE uid=@uid AND life=2 AND stage=6 AND cardinality(rewards)=3","uid",(long)p.Entity.UID)==1);FreshExpeditionSave(p,"paid",kills,hits);
        } else Require(phase.EndsWith("reload") && stage=="paid");
        if(phase.EndsWith("claim") || phase.EndsWith("reload")) {
            var ids=(long[])Sql("SELECT rewards FROM revora_desert_expedition WHERE uid=@uid AND life=2","uid",(long)p.Entity.UID);Require(ids.Length==3 && ids.Distinct().Count()==3);var rewards=p.Inventory.Objects.Where(i=>ids.Contains((long)i.UID)).ToArray();Require(rewards.Length==3 && rewards.All(i=>i.Bound) && rewards.Count(i=>i.ID==728250)==1 && rewards.Count(i=>i.ID==723911)==1 && rewards.Count(i=>ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)==1);
            string before=FreshExpeditionSnapshot(p);FreshClassicTalk(p,1002,900071727,181);Require(before==FreshExpeditionSnapshot(p));
        }
        Require(FreshExpeditionSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_expedition_probe")) && !PirateFenced(p));System.Console.WriteLine("[career-process] "+phase+"-passed uid="+p.Entity.UID+" level="+p.Entity.Level+" kills="+kills+" hits="+hits+" bag="+p.Inventory.Count);Server.CommandsAI("@close");throw new InvalidOperationException("Fresh Expedition did not close.");
    }
}
