using System;
using System.Threading;
using Revora.Database;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Participation is an accepted, successfully imported local Qualifier fight.
    // No queue, watcher, client score or administrator endpoint writes this receipt.
    internal static class ArenaParticipation
    {
        private static bool enabled;
        internal static bool Initialized {get{return enabled;}}
        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection) {c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_arena_participation(
uid bigint PRIMARY KEY CHECK(uid>0),opponent bigint NOT NULL CHECK(opponent>0 AND opponent<>uid),
match_id uuid NOT NULL,entered_at timestamptz NOT NULL DEFAULT now(),rule_version integer NOT NULL CHECK(rule_version=1));
CREATE TABLE IF NOT EXISTS revora_arena_daily_entries(
uid bigint NOT NULL CHECK(uid>0),run uuid NOT NULL,opponent bigint NOT NULL CHECK(opponent>0 AND opponent<>uid),
day varchar(8) NOT NULL,PRIMARY KEY(uid,run));
CREATE INDEX IF NOT EXISTS revora_arena_daily_entry_day ON revora_arena_daily_entries(uid,day);");}
            enabled=true;
        }
        internal static void RecordStart(Qualifier.QualifierList.QualifierGroup group) {
            if(!enabled || group==null)return;
            lock(group.ParticipationSync) {
                if(group.ParticipationRecorded || !group.StartedOnOwnedMap)return;
                if(group.RegisterLedgerOnEntry && (Qualifier.SignupWithdrawals.Pending(group.Player1) || Qualifier.SignupWithdrawals.Pending(group.Player2)))return;
                var a=group.Player1;var b=group.Player2;
                if(!ArenaActors.Player(a) || !ArenaActors.Player(b) || a.Entity.UID==b.Entity.UID || a.Entity.Level<40 || b.Entity.Level<40
                    || a.ArenaStatistic==null || b.ArenaStatistic==null || !a.ArenaStatistic.AcceptBox || !b.ArenaStatistic.AcceptBox
                    || a.ArenaState!=Qualifier.Fight || b.ArenaState!=Qualifier.Fight)return;
                try {
                    using(var c=DataHolder.MySqlConnection) {c.Open();using(var t=c.BeginTransaction()) {
                        if(group.RegisterLedgerOnEntry) {
                            if(!ArenaAdmissions.Initialized || !group.DurableAdmissionReserved
                                || !Monitor.IsEntered(a.ItemSyncRoot) || !Monitor.IsEntered(b.ItemSyncRoot)
                                || !ArenaMatchLedger.StartInTransaction(c,t,group))
                                throw new InvalidOperationException("Registered participation requires admitted locked owners.");
                        }
                        // Stable identity order also protects concurrent first matches. This
                        // changes no inventory or purse and takes no second player's item lock.
                        foreach(var p in a.Entity.UID<b.Entity.UID?new[]{a,b}:new[]{b,a}) {
                            var opponent=ReferenceEquals(p,a)?b:a;
                            Q.Execute(c,t,@"INSERT INTO revora_arena_participation(uid,opponent,match_id,rule_version)
VALUES(@uid,@opponent,@match,1) ON CONFLICT(uid) DO NOTHING","uid",(long)p.Entity.UID,"opponent",(long)opponent.Entity.UID,"match",group.ParticipationKey);
                            string key;
                            using(var q=Q.Command(c,t,"SELECT match_id FROM revora_arena_participation WHERE uid=@uid","uid",(long)p.Entity.UID))key=((Guid)q.ExecuteScalar()).ToString("N");
                            ProgressionEvidence.Complete(c,t,p.Entity.UID,11032,"arena-local-v1",key,1);
                            if(group.RegisterLedgerOnEntry) {
                                SkillTable.LockOwner(c,t,p);
                                string day;using(var q=Q.Command(c,t,"SELECT started_at AT TIME ZONE 'UTC' FROM revora_arena_matches WHERE id=@run","run",group.ParticipationKey))
                                    day=MagnoliaQuest.Day(DateTime.SpecifyKind((DateTime)q.ExecuteScalar(),DateTimeKind.Utc));
                                Q.Execute(c,t,"INSERT INTO revora_arena_daily_entries(uid,run,opponent,day) VALUES(@uid,@run,@opponent,@day) ON CONFLICT(uid,run) DO NOTHING",
                                    "uid",(long)p.Entity.UID,"run",group.ParticipationKey,"opponent",(long)opponent.Entity.UID,"day",day);
                                using(var q=Q.Command(c,t,"SELECT count(*) FROM revora_arena_daily_entries WHERE uid=@uid AND run=@run AND opponent=@opponent AND day=@day",
                                    "uid",(long)p.Entity.UID,"run",group.ParticipationKey,"opponent",(long)opponent.Entity.UID,"day",day))
                                    if(Convert.ToInt64(q.ExecuteScalar())!=1)throw new InvalidOperationException("Arena daily entry was not saved.");
                                using(var q=Q.Command(c,t,"SELECT count(*) FROM revora_arena_daily_entries WHERE uid=@uid AND day=@day","uid",(long)p.Entity.UID,"day",day))
                                    if(Convert.ToInt64(q.ExecuteScalar())>=20)ProgressionEvidence.Complete(c,t,p.Entity.UID,11111,"arena-twenty-entries",day,1);
                            }
                        }
                        if(group.RegisterLedgerOnEntry && (a.ShutdownStarted || b.ShutdownStarted
                            || Kernel.DisconnectPool.ContainsKey(a.Entity.UID) || Kernel.DisconnectPool.ContainsKey(b.Entity.UID)))
                            throw new InvalidOperationException("Arena entry lost an original session before commit.");
                        if(group.RegisterLedgerOnEntry && (Qualifier.SignupWithdrawals.Pending(a) || Qualifier.SignupWithdrawals.Pending(b)))return;
                        if(group.RegisterLedgerOnEntry && Q.Execute(c,t,"UPDATE revora_arena_matches SET entry_evidence_version=1 WHERE id=@run AND state='active'","run",group.ParticipationKey)!=1)
                            throw new InvalidOperationException("Arena entry evidence version was not saved.");
                        t.Commit();
                    }}
                    group.ParticipationRecorded=true;
                    if(!a.Fake)Q.Refresh(a);if(!b.Fake)Q.Refresh(b);
                } catch(Exception ex) {
                    if(group.RegisterLedgerOnEntry)group.EntryRecoveryRequired=true;
                    ArenaCacheRecovery.Fail(a,"arena-participation",ex);ArenaCacheRecovery.Fail(b,"arena-participation",ex);
                }
            }
        }
    }
}
