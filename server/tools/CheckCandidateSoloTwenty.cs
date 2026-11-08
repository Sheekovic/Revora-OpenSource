using System;
using System.Linq;
using System.Reflection;
using Revora;
using Revora.Client;
using Revora.Game;
using Console=System.Console;

internal static partial class CheckCandidateArenaLifecycle
{
    static GameState[] SoloTwentyPlayers(bool fresh) {
        nextUid=FirstUid;
        if(fresh){Player(Kernel.Maps[1002]);Player(Kernel.Maps[1002]);nextUid=FirstUid;}
        var players=new[]{Player(Kernel.Maps[1002],true),Player(Kernel.Maps[1002],true)};
        foreach(var p in players){p.CalculateStatBonus();p.CalculateHPBonus();Require(p.Entity.MaxHitpoints>0);}
        return players;
    }
    static string SoloTwentySnapshot() {return Convert.ToString(Sql(@"SELECT json_build_object(
'arena',(SELECT json_agg(a ORDER BY entityid) FROM (SELECT entityid,arenapoints,todaywin,todaybattles,totalwin,totallose,currenthonor,historyhonor FROM arena WHERE entityid BETWEEN 4294903001 AND 4294903002) a),
'rewards',(SELECT json_agg(r ORDER BY uid,milestone) FROM revora_arena_daily_rewards r WHERE uid BETWEEN 4294903001 AND 4294903002),
'items',(SELECT json_agg(i ORDER BY uid) FROM (SELECT uid,id,entityid,bound,stacksize FROM items WHERE entityid BETWEEN 4294903001 AND 4294903002) i))::text"));}
    static void SoloTwentyProcess(bool capture) {
        var players=SoloTwentyPlayers(capture);var winner=players[0];var loser=players[1];
        var instance=BindingFlags.Instance|BindingFlags.NonPublic;
        if(capture) {
            Sql("CREATE TABLE revora_solo_twenty_probe(run uuid NOT NULL,fingerprint text)");
            for(int round=1;round<=19;round++)EarnRound(winner,loser,round);
            foreach(var p in players)Call(Feature("ArenaRewardDelivery"),"Tick",p);
            Require(winner.CurrentHonor==500 && winner.Inventory.Count==2 && loser.CurrentHonor==0 && loser.Inventory.Count==0);
            winner.ArenaState=loser.ArenaState=Qualifier.Fight;winner.ArenaStatistic.AcceptBox=loser.ArenaStatistic.AcceptBox=true;
            var group=new Qualifier.QualifierList.QualifierGroup(winner,loser);
            var owner=Activator.CreateInstance(Feature("ArenaDisconnectRetention"),instance,null,new object[]{4,null},null);
            Call(typeof(Qualifier),"ImportMatched",group,owner);Require(players.All(p=>ReferenceEquals(p.QualifierGroup,group)));
            Guid run=(Guid)group.GetType().GetField("ParticipationKey",instance).GetValue(group);
            Sql(@"INSERT INTO revora_solo_twenty_probe VALUES(@run,NULL);
CREATE FUNCTION solo_twenty_suppress() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;
CREATE TRIGGER solo_twenty_suppress BEFORE INSERT ON revora_arena_result_statistics FOR EACH ROW EXECUTE FUNCTION solo_twenty_suppress();","run",run);
            group.End(loser);Call(Feature("ArenaResultCoordinator"),"Complete",group);
            Require((bool)Sql(@"SELECT EXISTS(SELECT 1 FROM revora_arena_result_intents WHERE run=@run)
AND NOT EXISTS(SELECT 1 FROM revora_arena_result_commits WHERE run=@run)
AND (SELECT count(*) FROM revora_arena_result_statistics WHERE uid=4294903001)=19
AND NOT EXISTS(SELECT 1 FROM revora_arena_daily_rewards WHERE uid BETWEEN 4294903001 AND 4294903002 AND milestone='matches20')","run",run));
            Server.CommandsAI("@close");Require(players.All(p=>ReferenceEquals(p.QualifierGroup,group) && Kernel.GamePool.ContainsKey(p.Entity.UID)));
            Console.WriteLine("[solo-twenty] solo-twenty-capture-passed");Environment.Exit(0);
        }
        Require((bool)Sql(@"SELECT (SELECT count(*) FROM revora_arena_result_commits c JOIN revora_solo_twenty_probe p ON p.run=c.run)=1
AND (SELECT count(*) FROM revora_arena_result_statistics s JOIN revora_solo_twenty_probe p ON p.run=s.run)=2"));
        foreach(var p in players) {
            Call(Feature("ArenaRewardDelivery"),"Tick",p);Call(Feature("ArenaRewardDelivery"),"Tick",p);
            bool won=p==winner;uint milestones=won?2u:1u;
            Require(p.CurrentHonor==500*milestones && p.HistoryHonor==500*milestones && p.Inventory.Count==2*milestones);
            Require(p.ArenaStatistic.TodayWin==(won?20u:0u) && p.ArenaStatistic.TodayBattles==20);
            Require((long)Sql("SELECT count(*) FROM revora_arena_result_statistics WHERE uid=@uid","uid",(long)p.Entity.UID)==20);
            Require((long)Sql(@"SELECT count(*) FROM revora_arena_daily_rewards r
JOIN items e ON e.uid=r.item_uid AND e.entityid=r.uid AND e.id=723700
JOIN items b ON b.uid=r.rune_uid AND b.entityid=r.uid AND b.id=4030101 AND b.bound=1
WHERE r.uid=@uid AND r.honor=500 AND r.paid_at IS NOT NULL","uid",(long)p.Entity.UID)==milestones);
            Require(Convert.ToInt64(Sql("SELECT currenthonor FROM arena WHERE entityid=@uid","uid",(long)p.Entity.UID))==500*milestones
                && Convert.ToInt64(Sql("SELECT currenthonor FROM teamarena WHERE entityid=@uid","uid",(long)p.Entity.UID))==500*milestones);
            Require(!(bool)typeof(Entity).GetField("PersistenceRequiresReload",instance).GetValue(p.Entity));
        }
        var prior=Sql("SELECT fingerprint FROM revora_solo_twenty_probe");string snapshot=SoloTwentySnapshot();
        if(prior==DBNull.Value)Sql("UPDATE revora_solo_twenty_probe SET fingerprint=@snapshot","snapshot",snapshot);else Require((string)prior==snapshot);
        foreach(var p in players)Call(Feature("ArenaRewardDelivery"),"Tick",p);Require(SoloTwentySnapshot()==snapshot);
        Console.WriteLine("[solo-twenty] solo-twenty-verify-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Solo twentieth recovery did not close.");
    }
}
