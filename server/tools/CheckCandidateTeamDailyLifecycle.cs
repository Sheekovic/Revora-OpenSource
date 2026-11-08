using System;
using System.Linq;
using System.Reflection;
using Revora;
using Revora.Client;
using Revora.Game;
using Console=System.Console;

internal static partial class CheckCandidateArenaLifecycle
{
    static readonly BindingFlags TeamDailyInstance=BindingFlags.Instance|BindingFlags.NonPublic;
    static GameState[] TeamDailyPlayers(bool fresh) {
        nextUid=FirstUid+2;
        if(fresh){for(int n=0;n<3;n++)Player(Kernel.Maps[1002]);nextUid=FirstUid+2;}
        var players=new[]{Player(Kernel.Maps[1002],true),Player(Kernel.Maps[1002],true),Player(Kernel.Maps[1002],true)};
        foreach(var p in players){p.CalculateStatBonus();p.CalculateHPBonus();Require(p.Entity.MaxHitpoints>0);}
        players[0].Team=new Revora.Game.ConquerStructures.Team(players[0]);players[0].Team.Add(players[2]);
        players[1].Team=new Revora.Game.ConquerStructures.Team(players[1]);return players;
    }
    static void TeamDailyRound(GameState[] players,int round,bool interrupt,string phase) {
        var group=new TeamQualifier.QualifierList.QualifierGroup(players[0],players[1]);
        var owner=Activator.CreateInstance(Feature("TeamArenaDisconnectRetention"),TeamDailyInstance,null,new object[]{4,null},null);
        Call(typeof(TeamQualifier),"ImportMatched",group,owner);
        Require(players.All(p=>ReferenceEquals(p.TeamQualifierGroup,group)));
        Guid id=(Guid)group.GetType().GetField("ResultKey",TeamDailyInstance).GetValue(group);
        if(interrupt) {
            Sql("CREATE FUNCTION team_daily_suppress() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER team_daily_suppress BEFORE INSERT ON revora_team_arena_results FOR EACH ROW EXECUTE FUNCTION team_daily_suppress()");
            Sql("UPDATE revora_team_daily_process_probe SET pending=@run,round=@round","run",id,"round",round);
        }
        group.End(players[1]);
        if(interrupt) {
            Call(Feature("TeamArenaResultCoordinator"),"Complete",group);
            Require((bool)Sql(@"SELECT EXISTS(SELECT 1 FROM revora_team_arena_intents WHERE id=@run)
AND NOT EXISTS(SELECT 1 FROM revora_team_arena_results WHERE id=@run)
AND (SELECT count(*) FROM revora_team_arena_statistics WHERE uid=4294903003)=@count","run",id,"count",(long)(round-1)));
            Server.CommandsAI("@close");
            Require(players.All(p=>ReferenceEquals(p.TeamQualifierGroup,group) && Kernel.GamePool.ContainsKey(p.Entity.UID)));
            Console.WriteLine("[team-daily] "+phase+"-passed");Environment.Exit(0);
        }
        var limit=DateTime.UtcNow.AddSeconds(10);
        do {
            owner.GetType().GetMethod("Tick",TeamDailyInstance).Invoke(owner,new object[]{16});
            if(players.All(p=>p.TeamQualifierGroup==null && typeof(GameState).GetField("ArenaShutdownRetention",TeamDailyInstance).GetValue(p)==null))break;
            System.Threading.Thread.Sleep(50);
        }while(DateTime.UtcNow<limit);
        Require(players.All(p=>p.TeamQualifierGroup==null && p.Entity.MapID==1002));
        Require(players[0].TeamArenaStatistic.TodayWin==round && players[2].TeamArenaStatistic.TodayWin==round && players[1].TeamArenaStatistic.TodayWin==0);
        Console.WriteLine("[team-daily] earned-round="+round);
    }
    static void TeamDailyRewardsCheck(GameState[] players,int rounds) {
        Require((bool)Sql(@"SELECT EXISTS(SELECT 1 FROM revora_team_arena_results r JOIN revora_team_daily_process_probe p ON p.pending=r.id)
AND (SELECT count(*) FROM revora_team_arena_statistics s JOIN revora_team_daily_process_probe p ON p.pending=s.run)=3"));
        foreach(var p in players) {
            Call(Feature("ArenaRewardDelivery"),"Tick",p);Call(Feature("TeamArenaDailyRewards"),"Pay",p);Call(Feature("TeamArenaDailyRewards"),"Pay",p);
            bool won=p!=players[1];uint milestones=(won?1u:0u)+(rounds==20?1u:0u);
            Require(p.CurrentHonor==500*milestones && p.HistoryHonor==500*milestones && p.Inventory.Count==2*milestones);
            Require(p.TeamArenaStatistic.TodayWin==(won?(uint)rounds:0u) && p.TeamArenaStatistic.TodayBattles==rounds);
            Require((long)Sql("SELECT count(*) FROM revora_team_arena_statistics WHERE uid=@uid","uid",(long)p.Entity.UID)==rounds);
            Require((long)Sql(@"SELECT count(*) FROM revora_team_arena_daily_rewards r
JOIN items e ON e.uid=r.item_uid AND e.entityid=r.uid AND e.id=723700
JOIN items b ON b.uid=r.rune_uid AND b.entityid=r.uid AND b.id=4030101 AND b.bound=1
WHERE r.uid=@uid AND r.honor=500 AND r.paid_at IS NOT NULL","uid",(long)p.Entity.UID)==milestones);
            Require(Convert.ToInt64(Sql("SELECT currenthonor FROM arena WHERE entityid=@uid","uid",(long)p.Entity.UID))==500*milestones
                && Convert.ToInt64(Sql("SELECT currenthonor FROM teamarena WHERE entityid=@uid","uid",(long)p.Entity.UID))==500*milestones);
            Require(!(bool)typeof(Entity).GetField("PersistenceRequiresReload",TeamDailyInstance).GetValue(p.Entity));
        }
    }
    static string TeamDailySnapshot() {return Convert.ToString(Sql(@"SELECT json_build_object(
'arena',(SELECT json_agg(a ORDER BY entityid) FROM (SELECT entityid,arenapoints,todaywin,todaybattles,totalwin,totallose,currenthonor,historyhonor FROM arena WHERE entityid BETWEEN 4294903003 AND 4294903005) a),
 'team',(SELECT json_agg(a ORDER BY entityid) FROM (SELECT entityid,arenapoints,todaywin,todaybattles,totalwin,totallose,currenthonor,historyhonor FROM teamarena WHERE entityid BETWEEN 4294903003 AND 4294903005) a),
'rewards',(SELECT json_agg(r ORDER BY uid,milestone) FROM revora_team_arena_daily_rewards r WHERE uid BETWEEN 4294903003 AND 4294903005),
'items',(SELECT json_agg(i ORDER BY uid) FROM (SELECT uid,id,entityid,bound,stacksize FROM items WHERE entityid BETWEEN 4294903003 AND 4294903005) i))::text"));}
    static void TeamDailyProcess(string phase) {
        bool capture=phase=="team-daily-capture";
        var players=TeamDailyPlayers(capture);
        if(capture) {
            Sql("CREATE TABLE revora_team_daily_process_probe(pending uuid,round integer,fingerprint text)");
            Sql("INSERT INTO revora_team_daily_process_probe VALUES(NULL,0,NULL)");
            for(int round=1;round<=9;round++)TeamDailyRound(players,round,round==9,phase);
        } else if(phase=="team-daily-continue") {
            TeamDailyRewardsCheck(players,9);
            for(int round=10;round<=20;round++)TeamDailyRound(players,round,round==20,phase);
        } else {
            TeamDailyRewardsCheck(players,20);
            string snapshot=TeamDailySnapshot();var prior=Sql("SELECT fingerprint FROM revora_team_daily_process_probe");
            if(prior==DBNull.Value)Sql("UPDATE revora_team_daily_process_probe SET fingerprint=@snapshot","snapshot",snapshot);
            else Require(snapshot==(string)prior);
            foreach(var p in players)Call(Feature("ArenaRewardDelivery"),"Tick",p);
            Require(snapshot==TeamDailySnapshot());
            Console.WriteLine("[team-daily] team-daily-verify-passed");Server.CommandsAI("@close");
        }
        throw new InvalidOperationException("Team daily process did not close.");
    }
}
