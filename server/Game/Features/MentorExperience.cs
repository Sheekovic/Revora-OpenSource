using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Game.ConquerStructures.Society;

namespace Revora.Game.Features
{
    internal static class MentorExperience
    {
        // Character owner first, then this lock. Never acquire another character's
        // owner lock while holding this one; mentor cache projections use this lock.
        internal static readonly object Sync = new object();
        internal const ulong VisibleCap = 50UL * 606;
        internal sealed class Credit
        {
            internal uint Mentor, Apprentice;
            internal string Name;
            internal uint Enrolled;
            internal ulong Actual, Total;
            internal ushort Plus, TotalPlus, Blessing, TotalBlessing;
        }
        internal static ushort ContributionView(long value)
        {
            if (value < 0) throw new InvalidOperationException("Negative mentor contribution requires repair.");
            return (ushort)Math.Min(ushort.MaxValue, value);
        }
        private static void Require(GameState p)
        {
            if (!Monitor.IsEntered(p.ItemSyncRoot ?? p) || !Monitor.IsEntered(Sync) || !QuestInventory.Player(p))
                throw new InvalidOperationException("Mentor EXP requires character and relationship ownership.");
        }
        internal static Credit Persist(NpgsqlConnection c, NpgsqlTransaction t, GameState p, ExperienceRewardPlan plan)
        {
            Require(p);
            if (t == null) throw new InvalidOperationException("Mentor EXP requires a caller-owned transaction.");
            if (!plan.CreditMentor || p.Mentor == null) return null;
            ulong amount = 0; foreach (var step in plan.Steps) amount = checked(amount + step.MentorExperience);
            if (amount == 0) return null;
            using (var q = QuestInventory.Command(c, t, @"UPDATE apprentice SET
actual_experience=COALESCE(actual_experience,0)+@amount,total_experience=COALESCE(total_experience,0)+@amount
WHERE apprenticeid=@uid AND mentorid=@mentor
RETURNING actual_experience,total_experience,actual_plus,total_plus,actual_heavenblessing,total_heavenblessing",
                "amount", checked((long)amount), "uid", (long)p.Entity.UID, "mentor", (long)p.Mentor.ID))
            using (var r = q.ExecuteReader())
            {
                if (!r.Read()) throw new InvalidOperationException("Mentor relationship changed.");
                var credit = new Credit { Mentor = p.Mentor.ID, Apprentice = p.Entity.UID, Name = p.Entity.Name,
                    Enrolled = p.Mentor.EnroleDate, Actual = Convert.ToUInt64(r.GetValue(0)), Total = Convert.ToUInt64(r.GetValue(1)),
                    Plus = r.IsDBNull(2) ? (ushort)0 : ContributionView(Convert.ToInt64(r.GetValue(2))),
                    TotalPlus = r.IsDBNull(3) ? (ushort)0 : ContributionView(Convert.ToInt64(r.GetValue(3))),
                    Blessing = r.IsDBNull(4) ? (ushort)0 : ContributionView(Convert.ToInt64(r.GetValue(4))),
                    TotalBlessing = r.IsDBNull(5) ? (ushort)0 : ContributionView(Convert.ToInt64(r.GetValue(5))) };
                if (r.Read()) throw new InvalidOperationException("Duplicate mentor relationship.");
                return credit;
            }
        }
        private static void SetExperience(Apprentice app, ulong actual, ulong total)
        { app.Actual_Experience = actual; app.Total_Experience = total; }
        internal static void RefreshPrize(GameState p)
        {
            ulong total = 0;
            if (p.Apprentices != null) foreach (var app in p.Apprentices.Values)
                total = Math.Min(VisibleCap, total + Math.Min(VisibleCap, app.Actual_Experience));
            p.PrizeExperience = Math.Min(VisibleCap, total + MentorDepartures.Experience(p.Entity.UID));
        }
        internal static void Apply(GameState p, Credit credit)
        {
            if (credit == null) return; Require(p);
            if (p.AsApprentice == null)
                p.AsApprentice = new Apprentice { ID = credit.Apprentice, Name = credit.Name, EnroleDate = credit.Enrolled,
                    Actual_Plus = credit.Plus, Total_Plus = credit.TotalPlus,
                    Actual_HeavenBlessing = credit.Blessing, Total_HeavenBlessing = credit.TotalBlessing };
            if (p.AsApprentice != null && p.AsApprentice.ID == credit.Apprentice)
                SetExperience(p.AsApprentice, credit.Actual, credit.Total);
            GameState mentor;
            if (Kernel.GamePool.TryGetValue(credit.Mentor, out mentor) && QuestInventory.Player(mentor, false))
            {
                if (mentor.Apprentices == null) mentor.Apprentices = new SafeDictionary<uint, Apprentice>();
                Apprentice app;
                if (!mentor.Apprentices.TryGetValue(credit.Apprentice, out app))
                {
                    app = new Apprentice { ID = credit.Apprentice, Name = credit.Name, EnroleDate = credit.Enrolled,
                        Actual_Plus = credit.Plus, Total_Plus = credit.TotalPlus,
                        Actual_HeavenBlessing = credit.Blessing, Total_HeavenBlessing = credit.TotalBlessing };
                    mentor.Apprentices.Add(app.ID, app);
                }
                SetExperience(app, credit.Actual, credit.Total); p.AsApprentice = app; RefreshPrize(mentor);
            }
        }
        internal static void AwardLegacy(GameState p, ExperienceRewardPlan plan)
        {
            if (p.Mentor == null || !plan.CreditMentor || plan.Steps.Length == 0) return;
            lock (Sync) try
            {
                Credit credit;
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open(); using (var t = c.BeginTransaction())
                    { credit = Persist(c, t, p, plan); t.Commit(); }
                }
                Apply(p, credit);
            }
            catch (Exception ex) { QuestInventory.Fail(p, "mentor-exp-credit", ex); throw; }
        }
        private sealed class Taken { internal uint Apprentice; internal Guid? Departure; internal ulong Before, Amount, Total; }
        internal static void Claim(GameState p)
        {
            Require(p);
            if (!CharacterProgression.Managed(p) || p.Entity.Level >= Config.LevelCap
                || p.Entity.ContainsFlag((ulong)Revora.Network.GamePackets.FlagsUpdate.Flags.Cursed)
                || Config.TrainingGroundBlocksExperience && p.Map.BaseID == 1039) return;
            try
            {
                var taken = new List<Taken>(); ulong units = 0;
                ExperienceRewardPlan plan; ExperienceRewards.Effects effects; ulong autoHunt;
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open(); using (var t = c.BeginTransaction())
                    {
                        SkillTable.LockOwner(c, t, p);
                        var seen = new HashSet<uint>();
                        using (var q = QuestInventory.Command(c, t, @"SELECT apprenticeid,COALESCE(actual_experience,0),COALESCE(total_experience,0)
FROM apprentice WHERE mentorid=@uid ORDER BY apprenticeid FOR UPDATE", "uid", (long)p.Entity.UID))
                        using (var r = q.ExecuteReader()) while (r.Read())
                        {
                            uint id = Convert.ToUInt32(r.GetValue(0));
                            if (!seen.Add(id)) throw new InvalidOperationException("Duplicate mentor apprentice.");
                            ulong before = Convert.ToUInt64(r.GetValue(1)), total = Convert.ToUInt64(r.GetValue(2));
                            ulong amount = Math.Min(before, VisibleCap - units); units += amount;
                            taken.Add(new Taken { Apprentice = id, Before = before, Amount = amount, Total = total });
                        }
                        using (var q = QuestInventory.Command(c, t, @"SELECT id,actual_experience,total_experience FROM revora_mentor_departures
WHERE mentor=@uid AND actual_experience>0 ORDER BY left_at,id LIMIT 64 FOR UPDATE", "uid", (long)p.Entity.UID))
                        using (var r = q.ExecuteReader()) while (r.Read())
                        {
                            ulong before = checked((ulong)r.GetInt64(1)), total = checked((ulong)r.GetInt64(2));
                            ulong amount = Math.Min(before, VisibleCap - units); units += amount;
                            taken.Add(new Taken { Departure = r.GetGuid(0), Before = before, Amount = amount, Total = total });
                        }
                        if (units == 0) { ApplyClaimBalances(p, taken); return; }
                        ulong award = ExperienceMath.Scale(p.ExpBall, units, 600);
                        plan = new ExperienceRewardPlan(CharacterProgression.Capture(p), award, Config.LevelCap,
                            Config.AttributePointsPerLevel, ReincarnationRecovery.Snapshot(p), false);
                        autoHunt = p.Entity.InAutoHunt ? checked(p.Entity.AutoHuntEXP + award / Math.Max(1u, Config.AutoHuntExperienceDivisor)) : p.Entity.AutoHuntEXP;
                        foreach (var row in taken)
                            if (row.Amount != 0)
                            if (row.Departure.HasValue)
                            {
                                if (QuestInventory.Execute(c, t, @"UPDATE revora_mentor_departures SET actual_experience=actual_experience-@amount
WHERE id=@id AND mentor=@uid AND actual_experience=@before", "id", row.Departure.Value, "uid", (long)p.Entity.UID,
                                    "amount", checked((long)row.Amount), "before", checked((long)row.Before)) != 1)
                                    throw new InvalidOperationException("Retained mentor EXP changed.");
                            }
                            else
                            if (QuestInventory.Execute(c, t, @"UPDATE apprentice SET actual_experience=actual_experience-@amount
WHERE mentorid=@uid AND apprenticeid=@app AND actual_experience=@before", "uid", (long)p.Entity.UID,
                                "app", (long)row.Apprentice, "before", checked((long)row.Before), "amount", checked((long)row.Amount)) != 1)
                                throw new InvalidOperationException("Mentor EXP balance changed.");
                        CharacterProgression.Persist(c, t, p, plan.After);
                        effects = ExperienceRewards.Persist(c, t, p, plan);
                        ReincarnationRecovery.Consume(c, t, p, plan.Restored); t.Commit();
                    }
                }
                ReincarnationRecovery.Committed(p, plan.Restored);
                ApplyClaimBalances(p, taken); p.ApplyExperienceReward(plan, effects, autoHunt, true);
            }
            catch (Exception ex) { QuestInventory.Fail(p, "mentor-exp-claim", ex); throw; }
        }
        private static void ApplyClaimBalances(GameState p, List<Taken> taken)
        {
                var present = new HashSet<uint>(taken.Where(row => !row.Departure.HasValue).Select(row => row.Apprentice));
                if (p.Apprentices != null) foreach (var app in p.Apprentices.Values)
                    if (!present.Contains(app.ID)) app.Actual_Experience = 0;
                foreach (var row in taken.Where(row => !row.Departure.HasValue))
                {
                    Apprentice app;
                    if (p.Apprentices != null && p.Apprentices.TryGetValue(row.Apprentice, out app))
                        SetExperience(app, row.Before - row.Amount, row.Total);
                    GameState child;
                    if (Kernel.GamePool.TryGetValue(row.Apprentice, out child) && child.Mentor != null && child.Mentor.ID == p.Entity.UID
                        && child.AsApprentice != null) SetExperience(child.AsApprentice, row.Before - row.Amount, row.Total);
                }
                ulong remaining = 0;
                foreach (var row in taken.Where(row => !row.Departure.HasValue))
                    remaining = Math.Min(VisibleCap, remaining + Math.Min(VisibleCap, row.Before - row.Amount));
                p.PrizeExperience = Math.Min(VisibleCap, remaining + MentorDepartures.Experience(p.Entity.UID));
        }
    }
}
