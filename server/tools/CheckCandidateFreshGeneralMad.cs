using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshGeneralMadSnapshot(GameState p) {
        return Convert.ToString(Sql(@"SELECT json_build_object('prior',@prior::json,
'general',(SELECT json_agg(r ORDER BY life) FROM revora_soul_general r WHERE uid=@uid),
'phantoms',(SELECT json_agg(r ORDER BY id) FROM revora_soul_phantoms r WHERE uid=@uid),
'phantomKills',(SELECT json_agg(r ORDER BY attempt,body) FROM revora_soul_phantom_kills r WHERE attempt IN(SELECT id FROM revora_soul_phantoms WHERE uid=@uid)),
'mad',(SELECT json_agg(r ORDER BY life) FROM revora_mad_man r WHERE uid=@uid),
'toxins',(SELECT json_agg(r ORDER BY life,type) FROM revora_mad_toxins r WHERE uid=@uid),
'mixes',(SELECT json_agg(r ORDER BY id) FROM revora_mad_mixes r WHERE uid=@uid),
'trials',(SELECT json_agg(r ORDER BY id) FROM revora_mad_trials r WHERE uid=@uid),
'visits',(SELECT row_to_json(r) FROM revora_mad_visits r WHERE uid=@uid),
'powders',(SELECT json_agg(r ORDER BY attempt,colour) FROM revora_mad_powders r WHERE attempt IN(SELECT id FROM revora_mad_trials WHERE uid=@uid)))::text","prior",FreshMysticFranticSnapshot(p),"uid",(long)p.Entity.UID));
    }
    static void FreshGeneralMadSave(GameState p,string stage,int kills,int hits) {
        Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_general_mad_probe SET stage=@stage,kills=@kills,hits=@hits,snapshot=@snapshot","stage",stage,"kills",kills,"hits",hits,"snapshot",FreshGeneralMadSnapshot(p));
    }
    static void FreshGeneralMadProcess(string phase) {
        var p=FreshLoadSaved(true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_fresh_general_mad_probe') IS NULL")) {
            Require(phase.EndsWith("general") && FreshMysticFranticSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_mystic_frantic_probe")) && (long)Sql("SELECT (SELECT count(*) FROM revora_soul_general WHERE uid=@uid)+(SELECT count(*) FROM revora_mad_man WHERE uid=@uid)","uid",(long)p.Entity.UID)==0);
            Sql("CREATE TABLE revora_fresh_general_mad_probe(stage text,kills integer,hits integer,snapshot text);INSERT INTO revora_fresh_general_mad_probe VALUES('new',0,0,@snapshot)","snapshot",FreshGeneralMadSnapshot(p));
        }
        Require(FreshGeneralMadSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_general_mad_probe")));
        string stage=Convert.ToString(Sql("SELECT stage FROM revora_fresh_general_mad_probe"));int kills=Convert.ToInt32(Sql("SELECT kills FROM revora_fresh_general_mad_probe")),hits=Convert.ToInt32(Sql("SELECT hits FROM revora_fresh_general_mad_probe"));
        if(phase.EndsWith("general")) {
            Require(stage=="new" && p.Inventory.Count<=25);var phoenix=Kernel.Maps[1011];Require(phoenix.Entities.Values.Count(e=>e.MonsterInfo!=null && e.MonsterInfo.ID==106)==20 && (int)Call(Feature("SoulGeneralQuest"),"EnsureRockPopulation",phoenix)==0);
            FreshClassicTalk(p,1002,900071727,101);FreshClassicTalk(p,1020,4487,1);FreshClassicTalk(p,1002,4488,1);FreshClassicTalk(p,1020,4487,1);
            FreshClassicKill(p,1020,13,ref kills,ref hits);for(int i=0;i<10;i++)FreshClassicKill(p,1011,106,ref kills,ref hits);for(int i=0;i<10;i++)FreshClassicKill(p,1020,70,ref kills,ref hits);
            Require((long)Sql("SELECT count(*) FROM revora_soul_general WHERE uid=@uid AND life=2 AND stage=4 AND snake=1 AND rock=10 AND ape=10","uid",(long)p.Entity.UID)==1);
            FreshClassicTalk(p,1020,4487,1);FreshClassicTalk(p,1020,4489,1);FreshClassicTalk(p,1020,4487,1);FreshClassicTalk(p,1002,4488,1);FreshClassicTalk(p,1002,4490,1);FreshClassicTalk(p,1020,4487,1);
            FreshClassicKill(p,1020,9917510,ref kills,ref hits);System.Threading.Thread.Sleep(1100);Call(Feature("SoulGeneralQuest"),"Tick");Require(p.Inventory.Contains(721706,1));
            FreshClassicTalk(p,1002,4488,1);FreshClassicTalk(p,1002,900071727,0);
            Require((long)Sql("SELECT count(*) FROM revora_soul_general WHERE uid=@uid AND life=2 AND stage=11 AND cardinality(rewards)=0","uid",(long)p.Entity.UID)==1 && (long)Sql("SELECT count(*) FROM revora_soul_phantom_kills WHERE attempt IN(SELECT id FROM revora_soul_phantoms WHERE uid=@uid)","uid",(long)p.Entity.UID)==1);FreshGeneralMadSave(p,"general",kills,hits);
        } else if(phase.EndsWith("mad")) {
            Require(stage=="general");FreshClassicTalk(p,1002,900071727,111);FreshClassicTalk(p,1020,4468,1);FreshClassicTalk(p,1002,1611,1);
            for(int i=0;!p.Inventory.Contains(729978,1) && i<100;i++)FreshClassicKill(p,1020,13,ref kills,ref hits);Require(p.Inventory.Contains(729978,1));FreshClassicTalk(p,1002,1611,1);FreshClassicTalk(p,1002,4470,1);
            ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==729981),p);ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==729980),p);Require(p.Entity.MapID>=11000 && !PirateFenced(p));
            for(uint id=9917810;id<9917817;id++) {
                var stone=p.Map.Npcs[id];p.Entity.X=(ushort)(stone.X-2);p.Entity.Y=stone.Y;p.ActiveNpc=id;Npcs.GetDialog(new NpcRequest(true){NpcID=id,OptionID=0},p);Require(p.Inventory.Objects.Count(i=>i.ID==729983+id-9917810)==1);
                string once=FreshGeneralMadSnapshot(p);Npcs.GetDialog(new NpcRequest(true){NpcID=id,OptionID=0},p);Require(once==FreshGeneralMadSnapshot(p));
            }
            Require((long)Sql("SELECT count(*) FROM revora_mad_trials WHERE uid=@uid AND life=2 AND state=0 AND mask=127","uid",(long)p.Entity.UID)==1);ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==729983),p);Require(p.Entity.MapID==1020 && p.Inventory.Contains(729982,1) && !p.Inventory.Objects.Any(i=>i.ID>=729983 && i.ID<=729989));
            FreshClassicTalk(p,1020,4468,1);FreshClassicTalk(p,1002,900071727,0);
            Require((long)Sql("SELECT count(*) FROM revora_mad_man WHERE uid=@uid AND life=2 AND stage=8 AND cardinality(rewards)=0","uid",(long)p.Entity.UID)==1 && (long)Sql("SELECT count(*) FROM revora_mad_trials WHERE uid=@uid AND life=2 AND state=1 AND mask=127","uid",(long)p.Entity.UID)==1 && (long)Sql("SELECT count(*) FROM revora_mad_powders WHERE attempt IN(SELECT id FROM revora_mad_trials WHERE uid=@uid)","uid",(long)p.Entity.UID)==7);FreshGeneralMadSave(p,"ready",kills,hits);
        } else if(phase.EndsWith("claim")) {
            Require(stage=="ready");var prior=p.Inventory.Objects.Select(i=>i.UID).ToArray();FreshClassicTalk(p,1002,900071727,101);FreshClassicTalk(p,1002,900071727,111);var added=p.Inventory.Objects.Where(i=>!prior.Contains(i.UID)).ToArray();Require(added.Length==5 && added.All(i=>i.Bound) && prior.All(uid=>p.Inventory.ContainsUID(uid)));
            Require((long)Sql("SELECT count(*) FROM revora_soul_general WHERE uid=@uid AND life=2 AND stage=12 AND cardinality(rewards)=2","uid",(long)p.Entity.UID)==1 && (long)Sql("SELECT count(*) FROM revora_mad_man WHERE uid=@uid AND life=2 AND stage=9 AND cardinality(rewards)=3","uid",(long)p.Entity.UID)==1);FreshGeneralMadSave(p,"paid",kills,hits);
        } else Require(phase.EndsWith("reload") && stage=="paid");
        if(phase.EndsWith("claim") || phase.EndsWith("reload")) {
            var ids=((long[])Sql("SELECT rewards FROM revora_soul_general WHERE uid=@uid AND life=2","uid",(long)p.Entity.UID)).Concat((long[])Sql("SELECT rewards FROM revora_mad_man WHERE uid=@uid AND life=2","uid",(long)p.Entity.UID)).ToArray();Require(ids.Length==5 && ids.Distinct().Count()==5);var rewards=p.Inventory.Objects.Where(i=>ids.Contains((long)i.UID)).ToArray();Require(rewards.Length==5 && rewards.All(i=>i.Bound) && rewards.Count(i=>i.ID==728244)==1 && rewards.Count(i=>i.ID==728245)==1 && rewards.Count(i=>i.ID==723911)==1 && rewards.Count(i=>ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)==2);
            Require(p.Inventory.Objects.Count(i=>i.ID==729979)==1);string before=FreshGeneralMadSnapshot(p);FreshClassicTalk(p,1002,900071727,101);FreshClassicTalk(p,1002,900071727,111);Require(before==FreshGeneralMadSnapshot(p));
        }
        Require(FreshGeneralMadSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_general_mad_probe")) && !PirateFenced(p));System.Console.WriteLine("[career-process] "+phase+"-passed uid="+p.Entity.UID+" level="+p.Entity.Level+" kills="+kills+" hits="+hits+" bag="+p.Inventory.Count+" phoenix="+p.Warehouses.First(w=>(uint)w.Key==10012).Value.Count);Server.CommandsAI("@close");throw new InvalidOperationException("Fresh General/Mad did not close.");
    }
}
