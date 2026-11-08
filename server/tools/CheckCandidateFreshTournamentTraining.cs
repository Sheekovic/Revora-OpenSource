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
    static string FreshTournamentTrainingSnapshot(GameState p,GameState[] players){return Convert.ToString(Sql("SELECT json_build_object('prior',@prior::json,'allowance',(SELECT row_to_json(r) FROM revora_exp_ball_days r WHERE uid=@peer),'progress',(SELECT json_build_object('level',level,'experience',experience,'strength',strength,'agility',agility,'vitality',vitality,'spirit',spirit,'attributes',atributes) FROM entities WHERE uid=@peer))::text","prior",FreshLmsSnapshot(p,players),"peer",(long)players[3].Entity.UID));}
    static void FreshTournamentTrainingSave(GameState p,GameState[] players){foreach(var who in players)Require(EntityTable.SaveEntity(who));Sql("UPDATE revora_fresh_tournament_training_probe SET snapshot=@snapshot","snapshot",FreshTournamentTrainingSnapshot(p,players));}
    static void FreshTournamentTraining(string phase){
        bool reload=phase.EndsWith("reload");var p=FreshLoadSaved(true,true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);var list=new System.Collections.Generic.List<GameState>();foreach(int slot in new[]{1,2,3,0}){uint uid=Convert.ToUInt32(Sql("SELECT uid FROM revora_fresh_contributors WHERE slot=@slot","slot",slot));list.Add(FreshContributor(uid,Convert.ToString(Sql("SELECT name FROM revora_fresh_contributors WHERE slot=@slot","slot",slot))));}list.Add(p);var players=list.ToArray();var peer=players[3];Server.GamePool=players;Require(players.All(w=>!w.Fake && w.Account.State==AccountTable.AccountState.Player && !PirateFenced(w) && !w.Entity.Dead));
        if((bool)Sql("SELECT to_regclass('revora_fresh_tournament_training_probe') IS NULL")){
            Require(!reload && FreshLmsSnapshot(p,players)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_lms_probe WHERE stage=2")) && peer.Entity.Level==70);
            Sql("CREATE TABLE revora_fresh_tournament_training_probe(balls int NOT NULL,spent bigint NOT NULL,capped int NOT NULL,done boolean NOT NULL,snapshot text);INSERT INTO revora_fresh_tournament_training_probe VALUES(0,0,0,false,NULL)");FreshTournamentTrainingSave(p,players);
        }
        if((bool)Sql("SELECT snapshot IS NULL AND balls=0 AND spent=0 AND capped=0 AND NOT done FROM revora_fresh_tournament_training_probe")){Require(!reload && peer.Entity.Level==70 && FreshLmsSnapshot(p,players)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_lms_probe WHERE stage=2")));FreshTournamentTrainingSave(p,players);}
        Require(FreshTournamentTrainingSnapshot(p,players)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_tournament_training_probe")));
        if(!reload){
            Require(!(bool)Sql("SELECT done FROM revora_fresh_tournament_training_probe"));int balls=(int)Sql("SELECT balls FROM revora_fresh_tournament_training_probe"),start=balls;DateTime day=new DateTime(2041,4,23,12,0,0,DateTimeKind.Local);Feature("ExperienceConsumables").GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>day));var timer=System.Diagnostics.Stopwatch.StartNew();
            while(peer.Entity.Level<130 && balls-start<80 && timer.Elapsed.TotalSeconds<210){
                day=new DateTime(2041,4,23,12,0,0,DateTimeKind.Local).AddDays(balls/10);peer.Entity.Teleport(1002,304,435);uint cp=peer.Entity.ConquerPoints;var ball=peer.Inventory.Objects.FirstOrDefault(i=>i.ID==722136 || i.ID==723700);if(ball==null){var old=peer.Inventory.Objects.Select(i=>i.UID).ToArray();CareerBuy(peer,723700);ball=peer.Inventory.Objects.Single(i=>!old.Contains(i.UID));}uint cost=cp-peer.Entity.ConquerPoints;Require(cost==0 || cost==ConquerItemInformation.BaseInformations[723700].ConquerPointsWorth);
                if(balls>0 && balls%10==0){day=day.AddDays(-1);byte level=peer.Entity.Level;ulong xp=peer.Entity.Experience;ushort stack=ball.StackSize;ItemHandler.UseItem(ball,peer);Require(peer.Entity.Level==level && peer.Entity.Experience==xp && peer.Inventory.ContainsUID(ball.UID) && ball.StackSize==stack);day=day.AddDays(1);Sql("UPDATE revora_fresh_tournament_training_probe SET capped=capped+1");}
                byte beforeLevel=peer.Entity.Level;ulong beforeXp=peer.Entity.Experience;ushort beforeStack=ball.StackSize;ItemHandler.UseItem(ball,peer);Require((beforeStack==1?!peer.Inventory.ContainsUID(ball.UID):ball.StackSize==beforeStack-1) && (peer.Entity.Level>beforeLevel || peer.Entity.Experience>beforeXp) && !PirateFenced(peer));balls++;
                Require((bool)Sql("SELECT used=@used AND day=@day FROM revora_exp_ball_days WHERE uid=@uid","used",balls%10==0?10:balls%10,"day",day.Year*10000+day.Month*100+day.Day,"uid",(long)peer.Entity.UID));Sql("UPDATE revora_fresh_tournament_training_probe SET balls=@balls,spent=spent+@cost,done=@done","balls",balls,"cost",(long)cost,"done",peer.Entity.Level>=130);FreshTournamentTrainingSave(p,players);
                if(beforeStack==1){string saved=FreshTournamentTrainingSnapshot(p,players);ItemHandler.UseItem(ball,peer);Require(saved==FreshTournamentTrainingSnapshot(p,players));}
                if(balls%10==0 || peer.Entity.Level>=130)Console.WriteLine("[fresh-tournament-training] balls="+balls+" level="+peer.Entity.Level+" spent="+Sql("SELECT spent FROM revora_fresh_tournament_training_probe"));
            }
            FreshTournamentTrainingSave(p,players);
        }else Require((bool)Sql("SELECT done FROM revora_fresh_tournament_training_probe") && peer.Entity.Level>=130 && (int)Call(Feature("LocalPkWar"),"Bracket",(int)peer.Entity.Level)==(int)Call(Feature("LocalPkWar"),"Bracket",(int)p.Entity.Level));
        Require(FreshTournamentTrainingSnapshot(p,players)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_tournament_training_probe")));Console.WriteLine("[career-process] "+phase+"-passed level="+peer.Entity.Level+" done="+Sql("SELECT done FROM revora_fresh_tournament_training_probe"));Console.Out.Flush();if(!reload){Environment.Exit(0);return;}Server.CommandsAI("@close");throw new InvalidOperationException("Fresh tournament training did not close.");
    }
}
