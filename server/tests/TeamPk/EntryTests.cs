using System;
using System.Collections.Concurrent;
using System.Runtime.Serialization;
using Revora.Game.ConquerStructures;
using Revora.Game.Features.Tournaments;
internal static class EntryTests {
    static int checks;
    static T Empty<T>() { return (T)FormatterServices.GetUninitializedObject(typeof(T)); }
    static void Check(bool value,string name) { if(!value)throw new InvalidOperationException(name);checks++; }
    static int Main() {
        var schedule=typeof(TeamElitePk.TeamTournament).GetMethod("IsOpeningTime",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        var opening=new DateTime(2026,9,26,18,56,0);
        Check((bool)schedule.Invoke(null,new object[]{opening}),"Saturday registration opens at 18:56");
        Check((bool)schedule.Invoke(null,new object[]{opening.AddSeconds(35)}),"delayed opening callback remains eligible through second 35");
        foreach(var closed in new[]{opening.AddSeconds(-1),opening.AddSeconds(36),opening.AddMinutes(1),opening.AddDays(1),opening.AddHours(1)})
            Check(!(bool)schedule.Invoke(null,new object[]{closed}),"registration does not open outside the scheduled window: "+closed);
        var skillSchedule=typeof(TeamElitePk.SkillTeamTournament).GetMethod("IsOpeningTime",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        var skillClose=typeof(TeamElitePk.SkillTeamTournament).GetMethod("RegistrationClose",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        var skillOpening=new DateTime(2026,9,30,19,45,0);
        foreach(var when in new[]{skillOpening,skillOpening.AddSeconds(35),skillOpening.AddMinutes(14).AddSeconds(59)}) {
            Check((bool)skillSchedule.Invoke(null,new object[]{when}),"Revora Wednesday registration accepts delayed callbacks: "+when);
            Check((DateTime)skillClose.Invoke(null,new object[]{when})==skillOpening.Date.AddHours(20),"delayed opening never extends registration past 20:00");
        }
        foreach(var when in new[]{skillOpening.AddTicks(-1),skillOpening.AddMinutes(15),skillOpening.AddDays(-1),skillOpening.AddDays(1)})
            Check(!(bool)skillSchedule.Invoke(null,new object[]{when}),"Revora Skill registration excludes other times: "+when);
        var duration=typeof(TeamElitePk).GetMethod("MatchMinutes",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        var remaining=typeof(TeamElitePk.Match).GetMethod("TimeLeftAt",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        var roundMinutes=typeof(TeamElitePk.Match).GetField("RoundMinutes",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        foreach(int matchStage in new[]{ElitePkStatus.States.GUI_Knockout,ElitePkStatus.States.GUI_Top8Qualifier,ElitePkStatus.States.GUI_Top4Qualifier,ElitePkStatus.States.GUI_Top2Qualifier,ElitePkStatus.States.GUI_Top3,ElitePkStatus.States.GUI_Top1}) {
            int expected=matchStage==ElitePkStatus.States.GUI_Knockout||matchStage==ElitePkStatus.States.GUI_Top8Qualifier?10:15;
            Check((int)duration.Invoke(null,new object[]{TeamElitePk.GamePackets.SkillElitePkBrackets,matchStage})==expected,"Skill match duration follows its frozen bracket matchStage");
            Check((int)duration.Invoke(null,new object[]{TeamElitePk.GamePackets.TeamElitePkBrackets,matchStage})==1,"regular Team PK timing remains unchanged");
            var clock=System.Time32.Now;var timed=Empty<TeamElitePk.Match>();timed.T_ID=TeamElitePk.GamePackets.SkillElitePkBrackets;
            timed.ImportTime=clock;timed.MatchStats=new TeamElitePk.FighterStats[2];roundMinutes.SetValue(timed,expected);
            Check((uint)remaining.Invoke(timed,new object[]{clock})==(uint)expected*60,"new Skill round displays the full duration");
            Check((uint)remaining.Invoke(timed,new object[]{clock.AddSeconds(expected*60-1)})==1,"Skill timeout waits until its final second");
            Check((uint)remaining.Invoke(timed,new object[]{clock.AddSeconds(expected*60)})==0,"Skill round expires at its fixed duration");
            Check((uint)remaining.Invoke(timed,new object[]{clock.AddSeconds(expected*60+20)})==0,"expired Skill clock cannot underflow");
            timed.MatchStats=new TeamElitePk.FighterStats[1];
            Check((uint)remaining.Invoke(timed,new object[]{clock})==0,"bye does not wait for a combat clock");
        }
        TeamElitePk.TeamTournament.Tournaments=null;TeamElitePk.TeamTournament.Opened=false;
        Check(!TeamElitePk.TeamTournament.Open(),"missing brackets cannot announce an opening");
        TeamElitePk.TeamTournament.Tournaments=new TeamElitePk[0];
        Check(!TeamElitePk.TeamTournament.Open(),"empty brackets cannot announce an opening");
        TeamElitePk.TeamTournament.Opened=true;
        Check(!TeamElitePk.TeamTournament.Open(),"already-open callback cannot repeat invitations");
        TeamElitePk.TeamTournament.Opened=false;
        TeamElitePk.SkillTeamTournament.Tournaments=null;TeamElitePk.SkillTeamTournament.Opened=false;
        Check(!TeamElitePk.SkillTeamTournament.Open(),"missing skill brackets cannot announce an opening");
        TeamElitePk.SkillTeamTournament.Tournaments=new TeamElitePk[0];
        Check(!TeamElitePk.SkillTeamTournament.Open(),"empty skill brackets cannot announce an opening");
        TeamElitePk.SkillTeamTournament.Opened=true;
        Check(!TeamElitePk.SkillTeamTournament.Open(),"already-open skill callback cannot repeat invitations");
        TeamElitePk.SkillTeamTournament.Opened=false;
        var team=Empty<Team>();team.UID=12;
        team.EliteMatch=Empty<TeamElitePk.Match>();team.EliteFighterStats=Empty<TeamElitePk.FighterStats>();
        var regular=Empty<TeamElitePk>();regular.Alive=true;regular.Teams=new ConcurrentDictionary<uint,Team>();regular.Teams[12]=team;
        TeamElitePk.TeamTournament.Tournaments=new[]{regular};
        TeamElitePk.SkillTeamTournament.Tournaments=null;
        Check(ElitePkStatus.InSkillTeam(0,team),"regular match remains recognized without skill tournament initialization");
        var skill=Empty<TeamElitePk>();skill.Teams=new ConcurrentDictionary<uint,Team>();
        TeamElitePk.SkillTeamTournament.Tournaments=new[]{skill};
        Check(ElitePkStatus.InSkillTeam(0,team),"inactive skill tournament cannot hide a regular match");
        regular.Alive=false;skill.Alive=true;skill.Teams[12]=team;
        Check(ElitePkStatus.InSkillTeam(0,team),"skill match remains recognized without an active regular tournament");
        var replacement=Empty<Team>();replacement.UID=12;replacement.EliteMatch=team.EliteMatch;replacement.EliteFighterStats=team.EliteFighterStats;
        Check(!ElitePkStatus.InSkillTeam(0,replacement),"matching UID does not confer another team's membership");
        Check(!ElitePkStatus.InSkillTeam(4,team) && !ElitePkStatus.InSkillTeam(1,team),"invalid or absent group rejected");
        Check(!ElitePkStatus.InSkillTeam(0,null),"null team rejected");
        team.EliteMatch=null;Check(!ElitePkStatus.InSkillTeam(0,team),"unmatched team rejected");
        team.EliteMatch=Empty<TeamElitePk.Match>();team.EliteFighterStats=null;
        Check(!ElitePkStatus.InSkillTeam(0,team),"missing fighter state rejected");
        var status=new ElitePkStatus();
        byte[] levels={1,99,100,119,120,129,130,255},groups={0,0,1,1,2,2,3,3};
        for(int i=0;i<levels.Length;i++)Check(team.GetEliteGroup(levels[i])==groups[i] && status.GetEliteGroup(levels[i])==groups[i],"entry and prize bracket at level "+levels[i]);
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        typeof(TeamElitePk).GetField("ledgerRun",flags).SetValue(regular,Guid.NewGuid());
        regular.Top8=new[]{new TeamElitePk.FighterStats(920001,"fixture",0,null)};
        var claimant=Empty<Revora.Client.GameState>();claimant.Entity=new Revora.Game.Entity(Revora.Game.EntityFlag.Player,false);claimant.Entity.UID=920001;
        byte rank,stage;
        Check(!TeamElitePk.TeamTournament.GetReward(claimant,out rank,out stage) && regular.Top8[0].RceiveReward==0,"new run cannot claim an intermediate ranking before saved final result");
        typeof(TeamElitePk).GetField("ledgerFinalized",flags).SetValue(regular,true);
        Check(TeamElitePk.TeamTournament.GetReward(claimant,out rank,out stage) && rank==1,"saved final run passes existing rank gate");
        VerifyTimeout(900,100,0);
        VerifyTimeout(100,900,1);
        VerifyTimeout(100,100,1);
        Console.WriteLine("Local Team PK entry checks passed: "+checks);return 0;
    }
    static void VerifyTimeout(uint first,uint second,int winningIndex) {
        Revora.Server.GamePool=new Revora.Client.GameState[0];
        ElitePKTournament.Tournaments=new[]{Empty<Revora.Game.ElitePK>()};
        var match=Empty<TeamElitePk.Match>();match.ID=100000;match.T_ID=TeamElitePk.GamePackets.TeamElitePkBrackets;
        match.Watchers=new ConcurrentDictionary<uint,Revora.Client.GameState>();
        match.ImportTime=System.Time32.Now.AddSeconds(-120);
        match.MatchStats=new TeamElitePk.FighterStats[2];
        for(int i=0;i<2;i++) {
            var p=new Revora.Client.GameState(null);p.Socket.Alive=false;
            p.Account=new Revora.Database.AccountTable(null);p.Account.State=Revora.Database.AccountTable.AccountState.Player;
            p.Entity=new Revora.Game.Entity(Revora.Game.EntityFlag.Player,false);p.Entity.SendUpdates=false;p.Entity.Owner=p;p.Entity.UID=(uint)(930000+i);
            var team=Empty<Team>();team.UID=(uint)(41+i);team.Lider=p;team.Members=new ConcurrentDictionary<uint,Team.Member>();
            team.Members[p.Entity.UID]=new Team.Member{entry=p,Lider=true};p.Team=team;
            team.EliteFighterStats=new TeamElitePk.FighterStats(p.Entity.UID,"fixture",0,team){Flag=TeamElitePk.FighterStats.StatusFlag.Fighting,Points=i==0?first:second};
            team.EliteMatch=match;match.MatchStats[i]=team.EliteFighterStats;
        }
        match.Export();
        Check(match.Done && match.Exported && match.Completed && match.MatchStats[winningIndex].Winner && match.MatchStats[1-winningIndex].Lost,"timeout keeps the higher score; equal scores retain the existing tie rule: "+first+"/"+second);
        match.Export();
        Check(match.MatchStats[winningIndex].Winner && match.MatchStats[1-winningIndex].Lost,"export replay preserves timeout placement");
    }

}
