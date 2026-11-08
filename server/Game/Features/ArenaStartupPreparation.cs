using System;
using Revora.Database;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Server.Run calls this after catalog loading, before workers, caches and listeners.
    internal static class ArenaStartupPreparation
    {
        private static readonly object Gate = new object();
        internal static bool Prepared { get; private set; }
        private static void RequireCold()
        {
            if (Server.Thread != null || Server.AuthServer != null || Server.GameServer != null
                || Kernel.GamePool.Count != 0 || Kernel.DisconnectPool.Count != 0
                || Qualifier.ArenaStatistics.Count != 0 || TeamQualifier.ArenaStatistics.Count != 0
                || Qualifier.QualifierList.Groups.Count != 0 || TeamQualifier.QualifierList.Groups.Count != 0)
                throw new InvalidOperationException("Joint Arena preparation requires startup before workers, listeners, sessions, groups and caches.");
        }
        internal static void Prepare(int batchLimit)
        {
            lock (Gate)
            {
                Prepared = false;
                if (batchLimit < 1 || batchLimit > 100) throw new ArgumentOutOfRangeException("batchLimit");
                RequireCold();
                if (ConquerItemInformation.BaseInformations == null)
                    throw new InvalidOperationException("Joint Arena preparation requires the item catalog before startup recovery.");
                ArenaBots.Initialize();
                ArenaResultCoordinator.Initialize();
                ArenaAdminBalances.Initialize();
                TeamArenaResultStatistics.Initialize();
                TeamArenaAdmissions.Initialize();
                ArenaAdmissions.Initialize();
                ArenaAdmissions.PrepareStartup(batchLimit);
                for(int pass=0;pass<batchLimit;pass++) {
                    if(TeamArenaAdmissions.AbandonInterrupted(128)<128)break;
                }
                if(TeamArenaAdmissions.HasInterrupted())throw new InvalidOperationException("Team admission recovery left unfinished reservations.");
                // Both modes forbid overlapping ownership at capture. Do not guess an order
                // for contradictory evidence from an older writer or damaged database.
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open(); using (var q = Q.Command(c,null,@"SELECT EXISTS(
SELECT 1 FROM revora_arena_matches m JOIN revora_team_arena_intents i ON
(m.player1 IN(i.winner,i.loser) OR m.player2 IN(i.winner,i.loser)
 OR EXISTS(SELECT 1 FROM revora_team_arena_intent_statistics s WHERE s.run=i.id AND s.uid IN(m.player1,m.player2)))
WHERE m.state='active'
AND NOT EXISTS(SELECT 1 FROM revora_arena_abandoned a WHERE a.run=m.id)
AND NOT EXISTS(SELECT 1 FROM revora_team_arena_results r WHERE r.id=i.id))"))
                        if (Equals(q.ExecuteScalar(),true)) throw new InvalidOperationException("Joint Arena preparation found overlapping unsettled ownership.");
                }
                TeamArenaStartupRecovery.PrepareStartup(batchLimit);
                ArenaStartupRecovery.PrepareStartup(batchLimit);
                RequireCold();
                Prepared = TeamArenaStartupRecovery.Prepared && ArenaStartupRecovery.Prepared;
                if (!Prepared) throw new InvalidOperationException("Both Arena modes must finish startup preparation.");
            }
        }
    }
}
