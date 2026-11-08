using System;
using Revora.Client;
using Revora.Database;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class ArenaAdminBalances
    {
        private static bool initialized;
        internal static void Initialize()
        {
            using (var c = DataHolder.MySqlConnection) {
                c.Open(); Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_arena_admin_balances(
id uuid PRIMARY KEY,uid bigint NOT NULL,operation varchar(16) NOT NULL CHECK(operation IN('set-points','add-honor')),
amount bigint NOT NULL CHECK(amount BETWEEN 0 AND 4294967295),
balance_before bigint NOT NULL,balance_after bigint NOT NULL CHECK(balance_after BETWEEN 0 AND 4294967295),
applied_at timestamptz NOT NULL);");
            }
            initialized = true;
        }
        private static bool Ready(GameState p)
        {
            return initialized && Q.Player(p,false) && p.Account != null
                && p.Account.State == AccountTable.AccountState.Administrador && !p.ShutdownStarted
                && !ArenaShutdownDrain.Stopping && !Kernel.DisconnectPool.ContainsKey(p.Entity.UID)
                && p.QualifierGroup == null && p.TeamQualifierGroup == null && p.WatchingGroup == null && p.TeamWatchingGroup == null
                && p.Trade != null && !p.Trade.InTrade && p.Booth == null
                && p.ArenaStatistic != null && p.TeamArenaStatistic != null
                && p.ArenaStatistic.EntityID == p.Entity.UID && p.TeamArenaStatistic.EntityID == p.Entity.UID
                && p.ArenaStatistic.PlayWith == null && p.TeamArenaStatistic.PlayWith == null
                && !Qualifier.PlayerList.ContainsKey(p.Entity.UID) && !Qualifier.WaitingPlayerList.ContainsKey(p.Entity.UID)
                && !TeamQualifier.PlayerList.ContainsKey(p.Entity.UID) && !TeamQualifier.WaitingPlayerList.ContainsKey(p.Entity.UID)
                && p.ArenaStatistic.ArenaPoints == p.TeamArenaStatistic.ArenaPoints
                && p.ArenaStatistic.CurrentHonor == p.TeamArenaStatistic.CurrentHonor
                && p.ArenaStatistic.HistoryHonor == p.TeamArenaStatistic.HistoryHonor;
        }
        internal static bool Apply(GameState p, bool honor, uint amount)
        {
            if (!Ready(p)) return false;
            lock (p.ItemSyncRoot) try {
                if (!Ready(p) || honor && amount > uint.MaxValue - p.CurrentHonor) return false;
                // Preserve legitimate legacy earnings; managed economic columns are not flushed.
                ArenaTable.SaveArenaStatistics(p.ArenaStatistic); TeamArenaTable.SaveArenaStatistics(p.TeamArenaStatistic);
                uint points=p.ArenaPoints, current=p.CurrentHonor, history=p.HistoryHonor;
                uint before=honor?current:points, after=honor?current+amount:amount;
                using (var c=DataHolder.MySqlConnection) {
                    c.Open(); using (var t=c.BeginTransaction()) {
                        SkillTable.LockOwner(c,t,p);
                        if (ArenaMatchLedger.Pending(c,t,p.Entity.UID) || TeamArenaResultStatistics.Pending(c,t,p.Entity.UID)) return false;
                        var args=new object[]{"uid",(long)p.Entity.UID,"honor",honor,"after",(long)after,
                            "points",(long)points,"current",(long)current,"history",(long)history};
                        if (Q.Execute(c,t,@"UPDATE arena SET arenapoints=CASE WHEN @honor THEN arenapoints ELSE @after END,
currenthonor=CASE WHEN @honor THEN @after ELSE currenthonor END
WHERE entityid=@uid AND COALESCE(arenapoints,0)=@points AND COALESCE(currenthonor,0)=@current AND COALESCE(historyhonor,0)=@history",args)!=1
                            || Q.Execute(c,t,@"UPDATE teamarena SET arenapoints=CASE WHEN @honor THEN arenapoints ELSE @after END,
currenthonor=CASE WHEN @honor THEN @after ELSE currenthonor END
WHERE entityid=@uid AND COALESCE(arenapoints,0)=@points AND COALESCE(currenthonor,0)=@current AND COALESCE(historyhonor,0)=@history",args)!=1)
                            throw new InvalidOperationException("Administrator Arena balance preimage changed.");
                        if (Q.Execute(c,t,@"INSERT INTO revora_arena_admin_balances(id,uid,operation,amount,balance_before,balance_after,applied_at)
VALUES(@id,@uid,@operation,@amount,@before,@after,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,
                            "operation",honor?"add-honor":"set-points","amount",(long)amount,"before",(long)before,"after",(long)after,"now",DateTime.UtcNow)!=1)
                            throw new InvalidOperationException("Administrator Arena audit was not saved.");
                        if (!Ready(p) || p.ArenaPoints!=points || p.CurrentHonor!=current || p.HistoryHonor!=history)
                            throw new InvalidOperationException("Administrator Arena owner changed.");
                        t.Commit();
                    }
                }
                if (honor) p.CurrentHonor=after; else p.ArenaPoints=after;
                p.ArenaStatistic.Send(p); p.TeamArenaStatistic.Send(p);
                return true;
            } catch (Exception ex) { ArenaCacheRecovery.Fail(p,"arena-admin-balance",ex); return false; }
        }
    }
}
