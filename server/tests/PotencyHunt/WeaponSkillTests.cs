using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type WeaponSkills = typeof(Entity).Assembly.GetType("Revora.Game.Features.WeaponSkillMastery");
    static long FixedReceipts(GameState p) { return Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND source='weapon-skill-fixed-v1'", p.Entity.UID); }
    static GameState SkillPlayer(Map map)
    {
        var p = StoredProgressionPlayer(map); Collection(p); p.Entity.MyAchievement = new Achievement(p.Entity);
        EvidenceRefresh(p); return p;
    }
    static Spell AlmostFixed(GameState p, ushort id)
    {
        byte level = (byte)(id == 1045 || id == 1046 ? 3 : 8);
        var skill = new Spell(true) { ID = id, Level = level, Experience = SpellTable.SpellInformations[id][level].NeedExperience - 1 };
        p.AddSpell(skill); SkillTable.SaveSpells(p); return skill;
    }
    static void VerifyWeaponSkillMastery(Map map)
    {
        SpellTable.Load();
        Call(EvidenceType, null, "Initialize");
        ushort[] skills = { 1045, 1046, 5030, 5040, 1300, 5020, 1260, 1290, 5010, 1250, 5050, 7000, 7010, 7020, 7030, 7040 };
        for (int i = 0; i < skills.Length; i++)
        {
            ushort id = skills[i]; int achievement = 10504 + i;
            var p = SkillPlayer(map); var skill = AlmostFixed(p, id); byte terminal = (byte)(skill.Level + 1);
            Check(!HasAchievement(p, achievement) && FixedReceipts(p) == 0, "pre-Fixed skill does not grant mastery " + id);
            p.IncreaseSpellExperience(1, id);
            Check(!Fenced(p) && skill.Level == terminal && skill.Available && skill.Experience == 0 && HasAchievement(p, achievement)
                && FixedReceipts(p) == 1 && Scalar("SELECT level FROM skills WHERE entityid=@uid", p.Entity.UID) == terminal,
                "ordinary final training update commits the correct skill and mastery receipt " + id);
            SkillTable.LoadSpells(p); p.IncreaseSpellExperience(uint.MaxValue, id); SkillTable.SaveSpells(p); EvidenceRefresh(p);
            Check(!Fenced(p) && p.Spells[id].Level == terminal && FixedReceipts(p) == 1 && HasAchievement(p, achievement),
                "fixed skill survives production loading and repeated training/saves without duplicate mastery " + id);
        }
        var pending = SkillPlayer(map); var unsaved = new Spell(true) { ID = 1045, Level = 4 }; pending.AddSpell(unsaved);
        EvidenceRefresh(pending);
        Check(!HasAchievement(pending, 10504) && FixedReceipts(pending) == 0,
            "an unsaved terminal cache value cannot create an observed mastery receipt");
        SkillTable.SaveSpells(pending); pending.RemoveSpell((ushort)1045); EvidenceRefresh(pending);
        Check(HasAchievement(pending, 10504) && FixedReceipts(pending) == 1 && SkillRows(pending) == 0,
            "earned mastery remains historical after the saved skill is forgotten");
        var batch = SkillPlayer(map); batch.AddSpell(new Spell(true) { ID = 1045, Level = 4 }); batch.AddSpell(new Spell(true) { ID = 1046, Level = 4 });
        Query("ALTER TABLE revora_achievement_evidence ADD CONSTRAINT fail_fixed_skill CHECK(achievement_id<>10505) NOT VALID");
        SkillTable.SaveSpells(batch);
        Check(Fenced(batch) && SkillRows(batch) == 0 && FixedReceipts(batch) == 0 && batch.Spells.Values.All(s => !s.Available),
            "later mastery failure rolls back the entire saved skill and achievement batch");
        Query("ALTER TABLE revora_achievement_evidence DROP CONSTRAINT fail_fixed_skill"); DemonReset(batch); SkillTable.SaveSpells(batch);
        Check(!Fenced(batch) && SkillRows(batch) == 2 && FixedReceipts(batch) == 2,
            "retry after a rolled-back mastery batch commits both exact saved skills and receipts");
        var fail = SkillPlayer(map); var old = AlmostFixed(fail, 5030);
        Query("ALTER TABLE revora_achievement_evidence ADD CONSTRAINT fail_fixed_training CHECK(achievement_id<>10506) NOT VALID");
        fail.IncreaseSpellExperience(1, 5030);
        Check(Fenced(fail) && FixedReceipts(fail) == 0 && Scalar("SELECT level FROM skills WHERE entityid=@uid", fail.Entity.UID) == 8,
            "failed terminal training receipt preserves the durable pre-Fixed skill and fences its unsaved cache");
        Query("ALTER TABLE revora_achievement_evidence DROP CONSTRAINT fail_fixed_training"); DemonReset(fail); SkillTable.LoadSpells(fail);
        Check(fail.Spells[5030].Level == 8 && !ReferenceEquals(old, fail.Spells[5030]), "production loader replaces the unsaved failed terminal skill");
        Parallel.Invoke(() => fail.IncreaseSpellExperience(1, 5030), () => { for (int n = 0; n < 12; n++) SkillTable.SaveSpells(fail); });
        Check(!Fenced(fail) && fail.Spells[5030].Level == 9 && FixedReceipts(fail) == 1,
            "concurrent recovery training and periodic saves cannot duplicate or lose the mastery");
        var excluded = SkillPlayer(map); var invalid = new Spell(true) { ID = 1045 };
        invalid.Buffer[16] = 5; // Bypass the packet model's normal level clamp to exercise malformed state.
        excluded.AddSpell(invalid);
        excluded.AddSpell(new Spell(true) { ID = 40101, Level = 4 }); SkillTable.SaveSpells(excluded);
        Check(FixedReceipts(excluded) == 0, "over-level and derived rune skills cannot satisfy the reviewed mastery identities");
        SkillTable.LoadSpells(excluded); SkillTable.SaveSpells(excluded);
        Check(!excluded.Spells.ContainsKey((ushort)1045) && FixedReceipts(excluded) == 0,
            "production loading cannot clamp an invalid saved level into earned mastery");
        var fake = SkillPlayer(map); fake.AddSpell(new Spell(true) { ID = 1045, Level = 4 }); fake.Fake = true; SkillTable.SaveSpells(fake);
        Check(FixedReceipts(fake) == 0 && SkillRows(fake) == 0, "fake adventurer cannot establish a player's saved skill mastery");
        var loading = SkillPlayer(map); loading.Entity.FullyLoaded = false; loading.AddSpell(new Spell(true) { ID = 1045, Level = 4 }); SkillTable.SaveSpells(loading);
        Check(FixedReceipts(loading) == 0, "incomplete login cannot publish a mastery achievement");
        loading.Entity.FullyLoaded = true; SkillTable.SaveSpells(loading);
        Check(FixedReceipts(loading) == 1, "fully loaded ordinary owner can recover mastery from its durable fixed skill");
        var definition = SpellTable.SpellInformations[1045][4]; uint needed = definition.NeedExperience;
        definition.NeedExperience = 1;
        try { Check(!(bool)Call(WeaponSkills, null, "Fixed", new Spell(true) { ID = 1045, Level = 4 }), "catalog that no longer has the reviewed terminal level cannot grant mastery"); }
        finally { definition.NeedExperience = needed; }
        VerifySkillTrainingArithmetic(map);
    }
    static void VerifySkillTrainingArithmetic(Map map)
    {
        Func<uint,uint,uint,uint,uint,uint,uint,uint,uint> gain = (a,b,c,d,e,f,g,h) => (uint)Call(WeaponSkills, null, "Accumulate", a,b,c,d,e,f,g,h);
        Check(gain(10, 100, 10000, 3, 2, 50, 4, 3) == 685,
            "skill EXP retains rate, gem, training divisor and VIP operation order");
        Check(gain(0, 1, 10000, 1, 1, 50, 2, 3) == 0,
            "skill EXP keeps existing integer rounding before later multipliers");
        Check(gain(uint.MaxValue - 1, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, 1, uint.MaxValue) == uint.MaxValue,
            "extreme skill rate and accumulated EXP products saturate at the earned threshold without wrapping");
        Check(gain(10, 5, 100, 0, 0, 0, 0, 1) == 15 && gain(10, 5, 0, 1, 1, 0, 1, 1) == 10,
            "zero rate/divisor defaults and terminal zero-threshold policy remain defined");
        var p = SkillPlayer(map); var skill = AlmostFixed(p, 1046);
        skill.Experience = 0; uint rate = Config.SpellExperienceRate, multiplier = Config.SpellExperienceRateMultiplier;
        try { Config.SpellExperienceRate = uint.MaxValue; Config.SpellExperienceRateMultiplier = uint.MaxValue; p.IncreaseSpellExperience(uint.MaxValue, 1046); }
        finally { Config.SpellExperienceRate = rate; Config.SpellExperienceRateMultiplier = multiplier; }
        Check(!Fenced(p) && skill.Level == 4 && FixedReceipts(p) == 1,
            "production training handles rates whose product exceeded the old uint calculation");
        var incomplete = SkillPlayer(map); var lower = AlmostFixed(incomplete, 1045); var last = SpellTable.SpellInformations[1045][4];
        SpellTable.SpellInformations[1045].Remove(4);
        try { incomplete.IncreaseSpellExperience(uint.MaxValue, 1045); }
        finally { SpellTable.SpellInformations[1045][4] = last; }
        Check(lower.Level == 3 && FixedReceipts(incomplete) == 0,
            "missing next skill definition cannot advance into an invalid catalog level");
    }
}
