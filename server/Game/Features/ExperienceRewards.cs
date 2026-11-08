using System;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Interfaces;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    internal static class ExperienceRewards
    {
        internal sealed class Effects
        {
            internal ISkill Skill;
            internal bool NewSkill;
            internal QualifierStatistic Arena, PreviousArena;
            internal bool NewArena;
            internal TeamQualifierStatistic TeamArena, PreviousTeamArena;
            internal bool NewTeamArena;
            internal MentorExperience.Credit MentorCredit;
        }
        // Caller owns the transaction, profile persistence and any consumable receipt.
        internal static Effects Persist(NpgsqlConnection c, NpgsqlTransaction t, GameState p, ExperienceRewardPlan plan)
        {
            if (t == null || !Monitor.IsEntered(p.ItemSyncRoot ?? p) || !Monitor.IsEntered(MentorExperience.Sync)
                || !QuestInventory.Player(p) || p.Spells == null)
                throw new InvalidOperationException("Experience effects are not ready.");
            SkillTable.LockOwner(c, t, p);
            var result = new Effects();
            if (plan.DefaultSkill != 0)
            {
                ISkill skill;
                bool exists = p.Spells.TryGetValue(plan.DefaultSkill, out skill);
                if (!exists) skill = new Spell(true) { ID = plan.DefaultSkill };
                if (skill == null) throw new InvalidOperationException("Level skill cache is invalid.");
                if (!skill.Available)
                {
                    SkillTable.Persist(c, t, p, skill);
                    result.Skill = skill; result.NewSkill = !exists;
                }
                else using (var q = QuestInventory.Command(c, t, "SELECT count(*) FROM skills WHERE entityid=@uid AND id=@id",
                    "uid", (long)p.Entity.UID, "id", (int)skill.ID))
                    if (Convert.ToInt64(q.ExecuteScalar()) != 1) throw new InvalidOperationException("Level skill row is missing or duplicated.");
            }
            if (plan.ReachedArenaLevel)
            {
                var old = p.ArenaStatistic;
                if (old == null || old.EntityID == 0)
                {
                    var arena = new QualifierStatistic(true) { EntityID = p.Entity.UID, Name = p.Entity.Name,
                        Level = (byte)plan.After.Values[0], Class = (byte)plan.After.Values[7], Model = p.Entity.Mesh,
                        ArenaPoints = ArenaTable.ArenaPointFill(70), LastArenaPointFill = DateTime.Now,
                        Status = QualifierStatistic.NotSignedUp };
                    if (QuestInventory.Execute(c, t, @"INSERT INTO arena(entityid,entityname,level,class,model,arenapoints,arenapointfill)
SELECT @uid,@name,@level,@class,@model,@points,@stamp WHERE NOT EXISTS(SELECT 1 FROM arena WHERE entityid=@uid)",
                        "uid", (long)p.Entity.UID, "name", arena.Name, "level", (int)arena.Level, "class", (int)arena.Class,
                        "model", (long)arena.Model, "points", (long)arena.ArenaPoints, "stamp", arena.LastArenaPointFill.Ticks) != 1)
                        throw new InvalidOperationException("Arena admission already exists.");
                    result.Arena = arena; result.PreviousArena = old; result.NewArena = true;
                }
                else if (old.EntityID != p.Entity.UID) throw new InvalidOperationException("Arena owner differs.");
                else if (p.Entity.Reborn == 0 && old.Level < 70 && old.ArenaPoints == 0)
                {
                    // Login creates a zero-point Arena row even for characters below 70.
                    var arena = new QualifierStatistic(true) { Level = (byte)plan.After.Values[0],
                        ArenaPoints = ArenaTable.ArenaPointFill(70), LastArenaPointFill = DateTime.Now };
                    if (QuestInventory.Execute(c, t, @"UPDATE arena SET level=@level,arenapoints=@points,arenapointfill=@stamp
WHERE entityid=@uid AND level<70 AND arenapoints=0", "uid", (long)p.Entity.UID, "level", (int)arena.Level,
                        "points", (long)arena.ArenaPoints, "stamp", arena.LastArenaPointFill.Ticks) != 1)
                        throw new InvalidOperationException("Arena admission preimage changed.");
                    result.Arena = arena; result.PreviousArena = old;
                }
                if (result.Arena != null) PersistTeamEnrollment(c, t, p, result);
            }
            result.MentorCredit = MentorExperience.Persist(c, t, p, plan);
            return result;
        }
        private static void PersistTeamEnrollment(NpgsqlConnection c, NpgsqlTransaction t, GameState p, Effects result)
        {
            var old = p.TeamArenaStatistic;
            if (old != null && (old.EntityID != 0 && old.EntityID != p.Entity.UID || old.ArenaPoints != 0))
                throw new InvalidOperationException("Initial Team Arena point mirror requires reload.");
            result.PreviousTeamArena = old;
            if (old == null || old.EntityID == 0)
            {
                var solo = result.NewArena ? result.Arena : result.PreviousArena;
                var team = new TeamQualifierStatistic(true) { EntityID = p.Entity.UID, Name = p.Entity.Name,
                    Level = result.Arena.Level, Class = p.Entity.Class, Model = p.Entity.Mesh,
                    ArenaPoints = result.Arena.ArenaPoints, CurrentHonor = solo == null ? 0 : solo.CurrentHonor,
                    HistoryHonor = solo == null ? 0 : solo.HistoryHonor, Status = TeamQualifierStatistic.NotSignedUp };
                if (QuestInventory.Execute(c,t,@"INSERT INTO teamarena(entityid,entityname,level,class,model,arenapoints,currenthonor,historyhonor)
SELECT @uid,@name,@level,@class,@model,@points,@honor,@history WHERE NOT EXISTS(SELECT 1 FROM teamarena WHERE entityid=@uid)",
                    "uid",(long)p.Entity.UID,"name",team.Name,"level",(int)team.Level,"class",(int)team.Class,
                    "model",(long)team.Model,"points",(long)team.ArenaPoints,"honor",(long)team.CurrentHonor,"history",(long)team.HistoryHonor)!=1)
                    throw new InvalidOperationException("Initial Team Arena row already exists.");
                result.TeamArena = team; result.NewTeamArena = true;
            }
            else
            {
                if (QuestInventory.Execute(c,t,@"UPDATE teamarena SET level=@level,arenapoints=@points
WHERE entityid=@uid AND COALESCE(arenapoints,0)=0", "uid",(long)p.Entity.UID,
                    "level",(int)result.Arena.Level,"points",(long)result.Arena.ArenaPoints)!=1)
                    throw new InvalidOperationException("Initial Team Arena point preimage changed.");
                result.TeamArena = old;
            }
        }
        internal static Effects Commit(GameState p, ExperienceRewardPlan plan)
        {
            lock (MentorExperience.Sync)
            try
            {
                Effects effects;
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open(); using (var t = c.BeginTransaction())
                    {
                        CharacterProgression.Persist(c, t, p, plan.After);
                        effects = Persist(c, t, p, plan);
                        ReincarnationRecovery.Consume(c, t, p, plan.Restored);
                        t.Commit();
                    }
                }
                ReincarnationRecovery.Committed(p, plan.Restored);
                MentorExperience.Apply(p, effects.MentorCredit);
                return effects;
            }
            catch (Exception ex) { QuestInventory.Fail(p, "experience-commit", ex); throw; }
        }
        internal static void ApplyCommitted(GameState p, Effects effects)
        {
            try
            {
                if (!Monitor.IsEntered(p.ItemSyncRoot ?? p) || !QuestInventory.Player(p) || effects == null || p.Spells == null)
                    throw new InvalidOperationException("Experience effects require reload.");
                if (effects.Skill != null)
                {
                    if (effects.NewSkill)
                    {
                        if (p.Spells.ContainsKey(effects.Skill.ID)) throw new InvalidOperationException("Committed skill cache changed.");
                        p.Spells.Add(effects.Skill.ID, effects.Skill);
                    }
                    else if (!ReferenceEquals(p.Spells[effects.Skill.ID], effects.Skill))
                        throw new InvalidOperationException("Committed skill cache changed.");
                    effects.Skill.Available = true;
                    effects.Skill.Send(p);
                }
                if (effects.Arena != null)
                {
                    if (!ReferenceEquals(p.ArenaStatistic, effects.PreviousArena)
                        || !ReferenceEquals(p.TeamArenaStatistic, effects.PreviousTeamArena)) throw new InvalidOperationException("Committed Arena cache changed.");
                    if (effects.NewArena)
                    {
                        if (!Game.Qualifier.ArenaStatistics.TryAdd(p.Entity.UID, effects.Arena))
                            throw new InvalidOperationException("Committed Arena admission requires reload.");
                        p.ArenaStatistic = effects.Arena;
                    }
                    else
                    {
                        p.ArenaStatistic.Level = effects.Arena.Level;
                        p.ArenaStatistic.LastArenaPointFill = effects.Arena.LastArenaPointFill;
                    }
                    if (effects.NewTeamArena)
                    {
                        if (!Game.TeamQualifier.ArenaStatistics.TryAdd(p.Entity.UID, effects.TeamArena))
                            throw new InvalidOperationException("Committed Team Arena admission requires reload.");
                        p.TeamArenaStatistic = effects.TeamArena;
                    }
                    p.TeamArenaStatistic.Level = effects.Arena.Level;
                    p.ArenaPoints = effects.Arena.ArenaPoints;
                    p.ArenaStatistic.Send(p); p.TeamArenaStatistic.Send(p);
                }
            }
            catch (Exception ex) {
                if (effects != null && effects.Arena != null) ArenaCacheRecovery.Fail(p, "experience-effects", ex);
                else QuestInventory.Fail(p, "experience-effects", ex);
                throw;
            }
        }
    }
}
