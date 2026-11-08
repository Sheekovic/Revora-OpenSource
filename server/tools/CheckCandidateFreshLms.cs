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
    static string FreshLmsSnapshot(GameState p,GameState[] players){return Convert.ToString(Sql(@"SELECT json_build_object('prior',@prior::json,'selected',(SELECT json_agg(r ORDER BY cycle) FROM revora_lms_selected_winners r WHERE cycle='204104221200'),'victories',(SELECT json_agg(r ORDER BY cycle) FROM revora_lms_victories r WHERE cycle='204104221200'),'offline',(SELECT json_agg(r ORDER BY cycle) FROM revora_lms_offline_prizes r WHERE cycle='204104221200'),'admissions',(SELECT json_agg(r ORDER BY uid) FROM revora_lms_admissions r WHERE cycle='204104221200'))::text","prior",FreshDuelSnapshot(p,players)));}
    static void FreshLmsJourney(string phase){
        bool earn=phase.EndsWith("earn"),claim=phase.EndsWith("claim");var p=FreshLoadSaved(true,true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);
        var list=new System.Collections.Generic.List<GameState>();foreach(int slot in new[]{1,2,3,0}){uint uid=Convert.ToUInt32(Sql("SELECT uid FROM revora_fresh_contributors WHERE slot=@slot","slot",slot));list.Add(FreshContributor(uid,Convert.ToString(Sql("SELECT name FROM revora_fresh_contributors WHERE slot=@slot","slot",slot))));}list.Add(p);var players=list.ToArray();Server.GamePool=players;Require(players.All(w=>!w.Fake && w.Account.State==AccountTable.AccountState.Player && !PirateFenced(w) && !w.Entity.Dead));
        var system=typeof(Revora.Game.Features.LastManStandingSystem);
        if(earn){
            Require(FreshDuelSnapshot(p,players)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_duel_probe WHERE stage=3")) && (bool)Sql("SELECT to_regclass('revora_fresh_lms_probe') IS NULL") && p.Inventory.Count<=39 && players.All(w=>(uint)w["lms-claim"]==0));
            system.GetField("CycleStart",Hidden).SetValue(null,DateTime.Now.AddMinutes(1));Revora.Game.Features.LastManStandingSystem.Tick();for(int n=0;n<players.Length;n++){players[n].Entity.PKMode=(Enums.PKMode)(n%3);FreshClassicTalk(players[n],1002,900000003,1);}
            Require(((System.Collections.Generic.HashSet<uint>)system.GetField("Registered",Hidden).GetValue(null)).Count==5);system.GetField("CycleStart",Hidden).SetValue(null,new DateTime(2041,4,22,12,0,0));Call(system,"StartFight");Require(players.All(w=>Revora.Game.Features.LastManStandingSystem.InEvent(w.Entity)));system.GetField("PeaceUntil",Hidden).SetValue(null,Time32.Now.AddSeconds(-1));p.CantAttack=Time32.Now.AddSeconds(-1);p.Attackable=true;
            int total=0;foreach(var loser in players.Where(w=>w!=p)){
                Require(Revora.Game.Features.LastManStandingSystem.InEvent(p.Entity) && Revora.Game.Features.LastManStandingSystem.InEvent(loser.Entity));loser.CantAttack=Time32.Now.AddSeconds(-1);loser.Attackable=true;p.Entity.X=loser.Entity.X;p.Entity.Y=(ushort)(loser.Entity.Y+1);p.Screen.Add(loser.Entity);int hits=0;var timer=System.Diagnostics.Stopwatch.StartNew();
                while(Revora.Game.Features.LastManStandingSystem.InEvent(loser.Entity) && hits<5000 && timer.Elapsed.TotalSeconds<60){p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);uint hp=loser.Entity.Hitpoints;Attack.HandleAttack(new Attack(true){Attacker=p.Entity.UID,Attacked=loser.Entity.UID,X=loser.Entity.X,Y=loser.Entity.Y,AttackType=Attack.Melee},p);hits++;if(hits==1)Require(loser.Entity.Hitpoints<hp || !Revora.Game.Features.LastManStandingSystem.InEvent(loser.Entity));}
                Require(!Revora.Game.Features.LastManStandingSystem.InEvent(loser.Entity) && loser.Entity.MapID==1002 && !p.Entity.Dead);total+=hits;Console.WriteLine("[fresh-lms] eliminated="+loser.Entity.UID+" calculated-hits="+hits);
            }
            Revora.Game.Features.LastManStandingSystem.Tick();Require((long)Sql("SELECT count(*) FROM revora_lms_victories WHERE cycle='204104221200' AND uid=@uid","uid",(long)p.Entity.UID)==1 && (uint)p["lms-claim"]==1);
            Require((long)Sql("SELECT count(*) FROM revora_lms_admissions WHERE cycle='204104221200' AND ended AND returned")==5);
            for(int n=0;n<players.Length;n++){var who=players[n];Require(who.Entity.MapID==1002 && who.Entity.PKMode==(Enums.PKMode)(n%3));if(who.Entity.Dead){who.Entity.DeathStamp=Time32.Now.AddSeconds(-20);GeneralData.Revive(new Data(true){dwParam=0},who);}Require(!who.Entity.Dead && who.Entity.MapID==1002 && (uint)who["lms-claim"]==(who==p?1u:0u));Require(EntityTable.SaveEntity(who));}
            Sql("CREATE TABLE revora_fresh_lms_probe(stage int NOT NULL,item bigint,snapshot text NOT NULL);INSERT INTO revora_fresh_lms_probe VALUES(1,NULL,@snapshot)","snapshot",FreshLmsSnapshot(p,players));Console.WriteLine("[fresh-lms] total-calculated-hits="+total);
        }else{
            Require(FreshLmsSnapshot(p,players)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_lms_probe")));foreach(var who in players)Revora.Game.Features.LastManStandingSystem.OnLogin(who);
            if(claim){
                Require((int)Sql("SELECT stage FROM revora_fresh_lms_probe")==1);uint cp=p.Entity.ConquerPoints,rp=p.Entity.RevoraPoint;ulong gold=p.Entity.Money;var prior=p.Inventory.Objects.Select(i=>i.UID).ToArray();HeartTalk(p,900000003,2);var added=p.Inventory.Objects.Where(i=>!prior.Contains(i.UID)).ToArray();Require(added.Length==1 && added[0].Bound && ConquerItemInformation.BaseInformations[added[0].ID].PurificationLevel==7 && prior.All(id=>p.Inventory.ContainsUID(id)) && p.Entity.ConquerPoints==cp+Revora.Game.Features.LastManStandingSystem.CpPrize && p.Entity.RevoraPoint==rp+Revora.Game.Features.LastManStandingSystem.RevoraPointPrize && p.Entity.Money==gold+Revora.Game.Features.LastManStandingSystem.GoldPrize && (uint)p["lms-claim"]==0);Sql("UPDATE revora_fresh_lms_probe SET item=@item","item",(long)added[0].UID);Console.WriteLine("[fresh-lms] cp="+(p.Entity.ConquerPoints-cp)+" rvp="+(p.Entity.RevoraPoint-rp)+" silver="+(p.Entity.Money-gold));
                foreach(var who in players){HeartTalk(who,900000003,0);Require(EntityTable.SaveEntity(who));}Sql("UPDATE revora_fresh_lms_probe SET stage=2,snapshot=@snapshot","snapshot",FreshLmsSnapshot(p,players));
            }else Require(phase.EndsWith("reload") && (int)Sql("SELECT stage FROM revora_fresh_lms_probe")==2);
            string paid=FreshLmsSnapshot(p,players);foreach(var who in players){HeartTalk(who,900000003,2);Require((uint)who["lms-claim"]==0);}Require(paid==FreshLmsSnapshot(p,players));uint item=Convert.ToUInt32(Sql("SELECT item FROM revora_fresh_lms_probe"));Require(p.Inventory.ContainsUID(item) && p.Inventory.Objects.Single(i=>i.UID==item).Bound);
            foreach(var who in players){var saved=(long[])Sql("SELECT coalesce(array_agg(uid ORDER BY uid),ARRAY[]::bigint[]) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND coalesce(inwardrobe,0)=0","uid",(long)who.Entity.UID);Require(saved.SequenceEqual(who.Inventory.Objects.Select(i=>(long)i.UID).OrderBy(id=>id)) && EntityTable.SaveEntity(who));}
            Require(FreshLmsSnapshot(p,players)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_lms_probe")));
        }
        Require(players.All(w=>!PirateFenced(w)));Console.WriteLine("[career-process] "+phase+"-passed");Console.Out.Flush();if(earn || claim){Environment.Exit(0);return;}Server.CommandsAI("@close");throw new InvalidOperationException("Fresh LMS did not close.");
    }
}
