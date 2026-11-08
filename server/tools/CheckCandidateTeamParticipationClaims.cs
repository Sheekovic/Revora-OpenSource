using System;
using System.Linq;
using System.Reflection;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game.Features.Tournaments;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static bool teamParticipationClaimFixture;
    static string TeamParticipationClaimSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object(
'base',@snapshot::json,'participation',(SELECT json_agg(json_build_array(run,uid,participated_at) ORDER BY run) FROM revora_local_team_pk_admissions WHERE uid=@uid))::text",
        "snapshot",PromotionClaimSnapshot(p),"uid",(long)p.Entity.UID));}
    static void TeamParticipationCancelled(bool reload){
        bool fresh=(bool)Sql("SELECT to_regclass('revora_team_participation_cancel_probe') IS NULL");
        if(fresh){Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN 4294911401 AND 4294911408")==0);
            Sql("CREATE TABLE revora_team_participation_cancel_probe(uid bigint PRIMARY KEY,snapshot text)");
            nextUid=4294911401;for(int n=0;n<8;n++)Player(Kernel.Maps[1002]);Sql("UPDATE entities SET level=130,reborn=0 WHERE uid BETWEEN 4294911401 AND 4294911408");}
        for(int kind=0;kind<2;kind++){
            nextUid=4294911401u+(uint)(kind*4);var players=new GameState[4];for(int n=0;n<4;n++){
                var p=players[n]=Player(Kernel.Maps[1002],true);ChiTable.Load(p);WayofHeroes.Load(p);p.LoadItemStats();}
            if(fresh){for(int n=0;n<4;n+=2){players[n].Team=new Revora.Game.ConquerStructures.Team(players[n]);players[n].Team.Add(players[n+1]);}
                var bracket=kind==0?TeamElitePk.TeamTournament.Tournaments[3]:TeamElitePk.SkillTeamTournament.Tournaments[3];
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;var bt=bracket.GetType();DateTime local=DateTime.Today.AddDays(21+kind);
                Require((bool)bt.GetMethod("SubscribeUntil",flags).Invoke(bracket,new object[]{local,local.AddHours(1)}));
                if(kind==0)TeamElitePk.TeamTournament.Opened=true;else TeamElitePk.SkillTeamTournament.Opened=true;
                foreach(var p in players){HeartTalk(p,kind==0?417171u:801717u,2);Require(p.Entity.MapID==bracket.WaitingArea.ID);}
                Require(bracket.Teams.Count==2);bt.GetField("StarTimer",flags).SetValue(bracket,DateTime.Now.AddMinutes(-2));
                bt.GetMethod("timerCallback",flags).Invoke(bracket,new object[]{0});Require(!bracket.Alive);
                foreach(var p in players){Require(p.Entity.MapID==1002 && EntityTable.SaveEntity(p));
                    Sql("INSERT INTO revora_team_participation_cancel_probe VALUES(@uid,@snapshot)","uid",(long)p.Entity.UID,"snapshot",TeamParticipationClaimSnapshot(p));}
            }
            foreach(var p in players){Require((long)Sql("SELECT count(*) FROM revora_local_team_pk_admissions WHERE uid=@uid AND participated_at IS NOT NULL","uid",(long)p.Entity.UID)==0);
                string before=TeamParticipationClaimSnapshot(p);Require(before==Convert.ToString(Sql("SELECT snapshot FROM revora_team_participation_cancel_probe WHERE uid=@uid","uid",(long)p.Entity.UID)));
                NobilityClaimPacket(p,406);NobilityClaimPacket(p,407);Require(before==TeamParticipationClaimSnapshot(p) && !PirateFenced(p));
                Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid","uid",(long)p.Entity.UID)==0);}
        }
        System.Console.WriteLine("[team-participation] cancelled-registration-refused");
    }
    static void TeamParticipationClaimProcess(bool reload){
        teamParticipationClaimFixture=true;TeamParticipationCancelled(reload);const uint first=4294911301;
        if((bool)Sql("SELECT to_regclass('revora_team_participation_claim_probe_v2') IS NULL")){
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN @uid AND @last","uid",(long)first,"last",(long)first+31)==0);
            nextUid=first;for(int n=0;n<32;n++)Player(Kernel.Maps[1002]);
            Sql("UPDATE entities SET level=130,reborn=0 WHERE uid BETWEEN @uid AND @last","uid",(long)first,"last",(long)first+31);
            Sql("CREATE TABLE revora_team_participation_claim_probe_v2(uid bigint PRIMARY KEY,snapshot text)");
        }
        for(int kind=0;kind<2;kind++){
            nextUid=first+(uint)(kind*16);var players=new GameState[16];for(int n=0;n<16;n++){
                var p=players[n]=Player(Kernel.Maps[1002],true);ChiTable.Load(p);WayofHeroes.Load(p);p.LoadItemStats();
                Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));}
            uint stage=(uint)(406+kind);bool captured=(long)Sql("SELECT count(*) FROM revora_team_participation_claim_probe_v2 WHERE uid=@uid","uid",(long)players[0].Entity.UID)==1;
            if(!captured){Require(!reload);
                foreach(var p in players){string before=TeamParticipationClaimSnapshot(p);NobilityClaimPacket(p,stage);Require(before==TeamParticipationClaimSnapshot(p));}
                for(int n=0;n<16;n+=2){players[n].Team=new Revora.Game.ConquerStructures.Team(players[n]);players[n].Team.Add(players[n+1]);}
                DateTime opening=DateTime.Today.AddDays(7).AddHours(19).AddMinutes(45);while(opening.DayOfWeek!=DayOfWeek.Wednesday)opening=opening.AddDays(1);
                if(kind==0){var fresh=TeamElitePk.TeamTournament.Tournaments[3];Require((bool)fresh.GetType().GetMethod("SubscribeUntil",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(fresh,new object[]{DateTime.Today.AddDays(2),DateTime.Today.AddDays(2).AddHours(1)}));TeamElitePk.TeamTournament.Opened=true;}
                else Require((bool)Call(typeof(TeamElitePk.SkillTeamTournament),"OpenAt",opening));
                var bracket=kind==0?TeamElitePk.TeamTournament.Tournaments[3]:TeamElitePk.SkillTeamTournament.Tournaments[3];
                foreach(var p in players){HeartTalk(p,kind==0?417171u:801717u,2);Require(p.Entity.MapID==bracket.WaitingArea.ID);
                    string admitted=TeamParticipationClaimSnapshot(p);NobilityClaimPacket(p,stage);Require(admitted==TeamParticipationClaimSnapshot(p));}
                Require(bracket.Teams.Count==8);var flags=BindingFlags.Instance|BindingFlags.NonPublic;var bt=bracket.GetType();
                bt.GetField("StarTimer",flags).SetValue(bracket,DateTime.Now.AddMinutes(-2));bt.GetField("pStamp",flags).SetValue(bracket,Time32.Now.AddSeconds(-90));
                bt.GetMethod("timerCallback",flags).Invoke(bracket,new object[]{0});
                foreach(var p in players){Require((long)Sql("SELECT count(*) FROM revora_local_team_pk_admissions a JOIN revora_local_team_pk_runs r USING(run) WHERE a.uid=@uid AND r.kind=@kind AND a.participated_at IS NOT NULL","uid",(long)p.Entity.UID,"kind",kind)==1);
                    var beforeItems=p.Inventory.Objects.Select(i=>i.UID).ToArray();System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,stage));
                    var ball=p.Inventory.Objects.Single(i=>!beforeItems.Contains(i.UID));Require(ball.ID==723700 && ball.Bound && ball.StackSize==1);
                    ulong experience=p.Entity.Experience;SoulClaimUse(p,ball);Require(p.Entity.Experience>experience);
                    string earned=TeamParticipationClaimSnapshot(p);NobilityClaimPacket(p,kind==0?407u:406u);Require(earned==TeamParticipationClaimSnapshot(p));}
                for(int pulse=0;pulse<80 && bracket.State!=ElitePkStatus.States.GUI_ReconstructTop;pulse++){
                    bt.GetField("StarTimer",flags).SetValue(bracket,DateTime.Now.AddMinutes(-2));bt.GetField("pStamp",flags).SetValue(bracket,Time32.Now.AddSeconds(-90));
                    var extended=(TeamElitePk.Match[])bt.GetField("ExtendedMatchArray",flags).GetValue(bracket);
                    foreach(var match in bracket.Matches.Values.Concat(extended??new TeamElitePk.Match[0]).Distinct().Where(m=>m.Inside && !m.Done))match.ImportTime=Time32.Now.AddSeconds(-1000);
                    bt.GetMethod("timerCallback",flags).Invoke(bracket,new object[]{0});}
                System.Console.WriteLine("[team-participation] kind="+kind+" terminal-state="+bracket.State);Require(bracket.State==ElitePkStatus.States.GUI_ReconstructTop);bracket.ConstructTop8=DateTime.Now.AddMinutes(-3);
                bt.GetMethod("timerCallback",flags).Invoke(bracket,new object[]{0});Require(!bracket.Alive);
                foreach(var p in players){Require(p.Entity.MapID==1002 && EntityTable.SaveEntity(p));
                    Sql("INSERT INTO revora_team_participation_claim_probe_v2 VALUES(@uid,@snapshot)","uid",(long)p.Entity.UID,"snapshot",TeamParticipationClaimSnapshot(p));}
            }
            foreach(var p in players){Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=@stage AND NOT legacy","uid",(long)p.Entity.UID,"stage",(int)stage)==1);
                Require(Convert.ToInt32(Sql("SELECT used FROM revora_exp_ball_days WHERE uid=@uid","uid",(long)p.Entity.UID))==1);
                string exact=TeamParticipationClaimSnapshot(p);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_team_participation_claim_probe_v2 WHERE uid=@uid","uid",(long)p.Entity.UID)));
                NobilityClaimPacket(p,406);NobilityClaimPacket(p,407);Require(exact==TeamParticipationClaimSnapshot(p) && !PirateFenced(p));}
        }
        System.Console.WriteLine("[career-process] career-team-participation-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Team participation claims did not close.");
    }
}
