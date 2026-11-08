using System;
using System.Linq;
using System.Reflection;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshQuizSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object('career',@career::json,'entries',(SELECT json_agg(r ORDER BY run,uid) FROM revora_quiz_entries r WHERE run IN('2041040614','2041042014')),'answers',(SELECT json_agg(r ORDER BY run,uid,question) FROM revora_quiz_answers r WHERE run IN('2041040614','2041042014')),'results',(SELECT json_agg(r ORDER BY run,uid) FROM revora_quiz_results r WHERE run IN('2041040614','2041042014')),'run',(SELECT json_agg(r ORDER BY id) FROM revora_quiz_runs r WHERE id IN('2041040614','2041042014')))::text","career",FreshPurpleCombatSnapshot(p)));}
    static void FreshQuizJourney(string phase){
        bool earn=phase.EndsWith("earn"),claim=phase.EndsWith("claim");var p=FreshLoadSaved(true,true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);
        uint peerUid=Convert.ToUInt32(Sql("SELECT uid FROM revora_fresh_contributors WHERE slot=0"));var peer=FreshContributor(peerUid,Convert.ToString(Sql("SELECT name FROM revora_fresh_contributors WHERE slot=0")));Server.GamePool=new[]{p,peer};Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p) && !PirateFenced(peer));
        var feature=Feature("QuizShowEvent");DateTime start=new DateTime(2041,4,20,21,0,0,DateTimeKind.Utc),now=start.AddMinutes(11);feature.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        if(phase.EndsWith("inspect")){
            if((bool)Sql("SELECT to_regclass('revora_fresh_quiz_probe') IS NOT NULL")){System.IO.File.WriteAllText("fresh-quiz-stage-before.json",Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_quiz_probe")));System.IO.File.WriteAllText("fresh-quiz-stage-current.json",FreshQuizSnapshot(p));}
            Console.WriteLine("[fresh-quiz-owner] dead="+p.Entity.Dead+" peer-bag="+peer.Inventory.Count);
            Console.WriteLine("[fresh-quiz-peer] dead="+peer.Entity.Dead+" hp="+peer.Entity.Hitpoints+" player="+Call(Feature("QuestInventory"),"Player",peer,true)+" map="+peer.Entity.MapID);
            System.IO.File.WriteAllText("fresh-quiz-before.json",Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_purple_probe")));System.IO.File.WriteAllText("fresh-quiz-current.json",FreshPurpleCombatSnapshot(p));
            Console.WriteLine("[fresh-quiz-inspect] "+Sql("SELECT json_build_object('run',(SELECT row_to_json(r) FROM revora_quiz_runs r WHERE id='2041040614'),'entries',(SELECT json_agg(e) FROM revora_quiz_entries e WHERE run='2041040614'),'answers',(SELECT json_agg(x) FROM (SELECT uid,count(*) total,count(*) FILTER(WHERE correct) correct FROM revora_quiz_answers WHERE run='2041040614' GROUP BY uid) x),'results',(SELECT json_agg(r) FROM revora_quiz_results r WHERE run='2041040614'))::text"));
            Console.WriteLine("[career-process] "+phase+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Quiz inspect did not close.");
        }
        if(phase.EndsWith("recover")){
            Require((bool)Sql("SELECT to_regclass('revora_fresh_quiz_probe') IS NULL"));
            using(var sha=System.Security.Cryptography.SHA256.Create()){
                Func<string,string> hash=value=>System.BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant();
                Require(hash(Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_purple_probe")))=="ca45f7610a2fb43a89eb05bffc3ee201a2c06adb0ccc345be28b4e9b2c8d50dc");
                Require(hash(FreshPurpleCombatSnapshot(p))=="a12353ec313189b3c274fa5a9f69b5b68d305c1e638df4c3e32df751f066ecf2");
            }
            Require(peer.Entity.Dead && (long)Sql("SELECT count(*) FROM revora_quiz_entries WHERE run='2041040614'")==1 && (long)Sql("SELECT count(*) FROM revora_quiz_results WHERE run='2041040614' AND uid=@uid AND correct=20 AND answered=20 AND rank=1 AND prize=1 AND NOT claimed","uid",(long)p.Entity.UID)==1);
            var before=p.Inventory.Objects.Select(i=>i.UID).ToArray();HeartTalk(p,900000062,3);var added=p.Inventory.Objects.Where(i=>!before.Contains(i.UID)).ToArray();Require(added.Length==2 && added.Count(i=>i.ID==722136 && i.Bound)==1 && added.Count(i=>ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7 && i.Bound)==1 && before.All(id=>p.Inventory.ContainsUID(id)));
            string paid=FreshQuizSnapshot(p);HeartTalk(p,900000062,3);Require(paid==FreshQuizSnapshot(p));peer.Entity.DeathStamp=Time32.Now.AddSeconds(-20);GeneralData.Revive(new Data(true){dwParam=0},peer);Require(!peer.Entity.Dead && peer.Entity.Hitpoints>0);
            Require(EntityTable.SaveEntity(p) && EntityTable.SaveEntity(peer));Sql("CREATE TABLE revora_fresh_quiz_probe(stage int NOT NULL,snapshot text NOT NULL);INSERT INTO revora_fresh_quiz_probe VALUES(0,@snapshot)","snapshot",FreshQuizSnapshot(p));Console.WriteLine("[fresh-quiz] preserved-solo-result claimed-two-items peer-revived");Console.WriteLine("[career-process] "+phase+"-passed");Console.Out.Flush();Environment.Exit(0);return;
        }
        if(earn){
            Require(FreshQuizSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_quiz_probe WHERE stage=0")) && !p.Entity.Dead && !peer.Entity.Dead && peer.Inventory.Count<=37);
            now=start.AddMinutes(-4);
            FreshClassicTalk(p,1020,10028,0);var bank=p.Warehouses[Revora.Game.ConquerStructures.Warehouse.WarehouseID.ApeCity];int store=Math.Max(0,p.Inventory.Count-35);Require(bank.Count+store<=60);
            foreach(var item in p.Inventory.Objects.Where(i=>i.Days==0).Take(store).ToArray()){var frame=new byte[172];Revora.Network.Writer.WriteUInt16(164,0,frame);Revora.Network.Writer.WriteUInt16(1102,2,frame);Revora.Network.Writer.WriteUInt32(10028,4,frame);frame[12]=1;frame[13]=10;Revora.Network.Writer.WriteUInt32(item.UID,18,frame);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,164,frame);Warehouse.Process(frame,p);Require(!p.Inventory.ContainsUID(item.UID) && bank.ContainsUID(item.UID));}Require(p.Inventory.Count<=35);
            FreshClassicTalk(p,1002,900000062,0);FreshClassicTalk(peer,1002,900000062,0);HeartTalk(p,900000062,2);HeartTalk(peer,900000062,2);Require((long)Sql("SELECT count(*) FROM revora_quiz_entries WHERE run='2041042014' AND uid IN(@uid,@peer)","uid",(long)p.Entity.UID,"peer",(long)peerUid)==2);
            var questions=(Array)Feature("QuizQuestionsV1").GetField("All",Hidden).GetValue(null);var deck=(int[])Sql("SELECT deck FROM revora_quiz_runs WHERE id='2041042014'");Require(deck.Length==20);
            for(int index=0;index<20;index++){
                now=start.AddSeconds(index*30+2);var question=questions.GetValue(deck[index]);int correct=(int)question.GetType().GetField("Correct",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(question);
                if(index==0){HeartTalk(p,900000062,(byte)(10+correct));Require((long)Sql("SELECT count(*) FROM revora_quiz_answers WHERE run='2041042014'")==0);}
                HeartTalk(p,900000062,1);HeartTalk(p,900000062,(byte)(10+index*4+correct));string before=FreshQuizSnapshot(p);HeartTalk(p,900000062,(byte)(10+index*4+(correct+1)%4));Require(before==FreshQuizSnapshot(p));
                now=now.AddSeconds(2);HeartTalk(peer,900000062,1);HeartTalk(peer,900000062,(byte)(10+index*4+(index<10?correct:(correct+1)%4)));
            }
            now=start.AddMinutes(10);feature.GetField("next",Hidden).SetValue(null,DateTime.MinValue);Call(feature,"Tick");
            Require((long)Sql("SELECT count(*) FROM revora_quiz_results WHERE run='2041042014' AND NOT claimed AND ((uid=@uid AND correct=20 AND answered=20 AND rank=1 AND prize=3) OR (uid=@peer AND correct=10 AND answered=20 AND rank=2 AND prize=2))","uid",(long)p.Entity.UID,"peer",(long)peerUid)==2);
            Require(EntityTable.SaveEntity(p) && EntityTable.SaveEntity(peer));Sql("UPDATE revora_fresh_quiz_probe SET stage=1,snapshot=@snapshot","snapshot",FreshQuizSnapshot(p));
        }else{
            Require(FreshQuizSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_quiz_probe")));
            if(claim){
                Require((int)Sql("SELECT stage FROM revora_fresh_quiz_probe")==1);
                foreach(var who in new[]{p,peer}){
                    int balls=who==p?3:2;var prior=who.Inventory.Objects.Select(i=>i.UID).ToArray();uint cp=who.Entity.ConquerPoints,rp=who.Entity.RevoraPoint;ulong silver=who.Entity.Money;HeartTalk(who,900000062,3);var added=who.Inventory.Objects.Where(i=>!prior.Contains(i.UID)).ToArray();
                    Require(added.Length==balls+1 && added.Count(i=>i.ID==722136 && i.Bound)==balls && added.Count(i=>ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7 && i.Bound)==1 && prior.All(id=>who.Inventory.ContainsUID(id)) && who.Entity.ConquerPoints==cp && who.Entity.RevoraPoint==rp && who.Entity.Money==silver);
                }
                Require(EntityTable.SaveEntity(p) && EntityTable.SaveEntity(peer));Sql("UPDATE revora_fresh_quiz_probe SET stage=2,snapshot=@snapshot","snapshot",FreshQuizSnapshot(p));
            }else Require(phase.EndsWith("reload") && (int)Sql("SELECT stage FROM revora_fresh_quiz_probe")==2);
            string paid=FreshQuizSnapshot(p);HeartTalk(p,900000062,3);HeartTalk(peer,900000062,3);Call(feature,"Tick");Require(paid==FreshQuizSnapshot(p));
            foreach(var who in new[]{p,peer}){var ids=(long[])Sql("SELECT rewards FROM revora_quiz_results WHERE run='2041042014' AND uid=@uid AND claimed","uid",(long)who.Entity.UID);Require(ids.Length==(who==p?4:3) && ids.All(id=>who.Inventory.ContainsUID((uint)id)));}
        }
        foreach(int id in new[]{10120,11128})Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=@id AND source='quiz-local-v1'","uid",(long)p.Entity.UID,"id",id)==1);
        Require(FreshQuizSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_quiz_probe")));Console.WriteLine("[career-process] "+phase+"-passed");Console.Out.Flush();if(earn || claim){Environment.Exit(0);return;}Server.CommandsAI("@close");throw new InvalidOperationException("Fresh Quiz did not close.");
    }
}
