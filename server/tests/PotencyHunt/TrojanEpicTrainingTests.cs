using System;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type TrojanTraining = typeof(Entity).Assembly.GetType("Revora.Game.Features.TrojanEpicTraining");
    static GameState TrojanStudent(Map home, byte level = 120) {
        var p = PromotionPlayer(home, 15, level, 10022); var npc = home.Npcs[10022]; var tile = FlowerTile(home, npc.X, npc.Y);
        p.Entity.X = tile.Item1; p.Entity.Y = tile.Item2; EntityTable.SaveEntity(p); return p;
    }
    static void TrojanLesson(GameState p) { TrojanTalk(p, 10022, 49); TrojanTalk(p, 10022, 84); }
    static void VerifyTrojanTraining(GameState earned, Map home) {
        TrojanTalk(earned, 10022, 84);
        Check(!earned.Spells.ContainsKey(11960), "unoffered trainer confirmation cannot learn an earned epic art");
        TrojanLesson(earned);
        Check(!Fenced(earned) && earned.Spells.ContainsKey(11960) && earned.Spells.ContainsKey(11980) && earned.Spells.ContainsKey(11990)
            && !earned.Spells.ContainsKey(11970), "one fully NPC-earned completion unlocks Breath Focus but not Super Cyclone");
        earned = ReloadBall(earned);
        Check(new ushort[] {11960,11980,11990}.All(id => earned.Spells.ContainsKey(id) && earned.Spells[id].Level == 0 && earned.Spells[id].PreviousLevel == 0)
            && Scalar("SELECT count(*) FROM revora_trojan_epic_training WHERE uid=@uid", earned.Entity.UID) == 3,
            "epic lessons persist with individual learning receipts and zero former proficiency");
        TrojanLesson(earned);
        Check(Scalar("SELECT count(*) FROM revora_trojan_epic_training WHERE uid=@uid", earned.Entity.UID) == 3, "replayed epic lessons cannot duplicate skill rows or receipts");
        var p = TrojanStudent(home); TrojanLesson(p);
        Check(!Fenced(p) && !p.Spells.ContainsKey(11960) && !p.Spells.ContainsKey(11970) && p.Spells.ContainsKey(11980) && p.Spells.ContainsKey(11990),
            "uncompleted Trojan learns weapon arts but cannot receive completion-locked arts");
        var young = TrojanStudent(home, 14); TrojanLesson(young);
        Check(!young.Spells.ContainsKey(11980) && !young.Spells.ContainsKey(11990), "catalog minimum levels prevent premature Trojan weapon lessons");
        young.Entity.Level = 15; EntityTable.SaveEntity(young); TrojanLesson(young);
        Check(young.Spells.ContainsKey(11990) && !young.Spells.ContainsKey(11980), "Mortal Strike unlocks at catalog level fifteen while Fatal Cross waits");
        young.Entity.Level = 40; EntityTable.SaveEntity(young); TrojanLesson(young);
        Check(young.Spells.ContainsKey(11980), "Fatal Cross unlocks at catalog level forty");
        // Synthetic boundary fixture: reward receipts represent previously granted/transferred weapons.
        Query("INSERT INTO revora_trojan_epic(uid,run,revision,stage,pulses,energy,completed,last_energy,updated_at) VALUES(@uid,6,1,14,0,90,6,@day,now())",
            "uid", (long)p.Entity.UID, "day", trojanNow.Date);
        for (int run = 1; run <= 6; run++) Query("INSERT INTO revora_trojan_epic_rewards(uid,run,item_uid,item_type,bound,created_at) VALUES(@uid,@run,@item,614439,false,now())",
            "uid", (long)p.Entity.UID, "run", run, "item", 88000000L + p.Entity.UID * 10L + run);
        TrojanLesson(p); Check(p.Spells.ContainsKey(11960) && !p.Spells.ContainsKey(11970), "six receipted completions still cannot teach Super Cyclone");
        Query("UPDATE revora_trojan_epic SET run=7,completed=7 WHERE uid=@uid", "uid", (long)p.Entity.UID);
        Query("INSERT INTO revora_trojan_epic_rewards(uid,run,item_uid,item_type,bound,created_at) VALUES(@uid,7,@item,614439,false,now())",
            "uid", (long)p.Entity.UID, "item", 88000000L + p.Entity.UID * 10L + 7);
        TrojanLesson(p); Check(!Fenced(p) && p.Spells.ContainsKey(11970), "seventh receipted completion unlocks Super Cyclone");
        var failed = TrojanStudent(home);
        Query("CREATE FUNCTION trojan_lesson_fail() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER trojan_lesson_fail BEFORE INSERT ON revora_trojan_epic_training FOR EACH ROW EXECUTE FUNCTION trojan_lesson_fail()");
        try { TrojanLesson(failed); } finally { Query("DROP TRIGGER trojan_lesson_fail ON revora_trojan_epic_training;DROP FUNCTION trojan_lesson_fail()"); }
        Check(Fenced(failed) && !failed.Spells.ContainsKey(11980) && Scalar("SELECT count(*) FROM skills WHERE entityid=@uid AND id IN(11980,11990)", failed.Entity.UID) == 0,
            "failed learning receipt rolls skill inserts back and requires character reload");
        var rules = typeof(Entity).Assembly.GetType("Revora.Game.Features.RebirthSkillRules");
        var second = typeof(Entity).Assembly.GetType("Revora.Game.Features.SecondRebirthSkills");
        var reincarnation = typeof(Entity).Assembly.GetType("Revora.Game.Features.PaidReincarnation");
        foreach (ushort id in new ushort[] {11980,11990}) { p.Spells[id].Level = 3; p.Spells[id].PreviousLevel = 5; p.Spells[id].Experience = 123; }
        foreach (byte next in new byte[] {11,21}) {
            Check(!((Spell[])Call(rules, null, "Plan", p, next)).Any(s => s.ID == 11980 || s.ID == 11990), "first rebirth drops both Trojan weapon arts instead of retaining proficiency: " + next);
            Check(!((Spell[])Call(second, null, "PlanHistory", p, (byte)15, (byte)15, next)).Any(s => s.ID == 11980 || s.ID == 11990), "second rebirth drops both Trojan weapon arts: " + next);
            p.Entity.SecondRebornClass = 15;
            Check(!((Spell[])Call(reincarnation, null, "Skills", p, next)).Any(s => s.ID == 11980 || s.ID == 11990), "paid reincarnation drops both Trojan weapon arts: " + next);
        }
        foreach (ushort id in new ushort[] {11980,11990}) {
            Check(!(bool)Call(TrojanTraining, null, "CanPerform", p, id)
                && !Revora.Game.Attacking.Handle.CanUseSpell(SpellTable.GetSpell(id, 0), p), "normal spell admission rejects epic weapon art without two active sabers: " + id);
        }
        RebirthGear(p, 4, 614439);
        Check(!(bool)Call(TrojanTraining, null, "CanPerform", p, (ushort)11980), "one Cross Saber does not satisfy Fatal Cross");
        RebirthGear(p, 5, 614439);
        Check((bool)Call(TrojanTraining, null, "CanPerform", p, (ushort)11980), "two distinct active Cross Sabers satisfy the weapon guard");
        p.AlternateEquipment = true;
        Check(!(bool)Call(TrojanTraining, null, "CanPerform", p, (ushort)11990), "inactive primary sabers cannot authorize alternate-set Mortal Strike");
        RebirthGear(p, 24, 614439); RebirthGear(p, 25, 614439);
        Check((bool)Call(TrojanTraining, null, "CanPerform", p, (ushort)11990), "two active alternate Cross Sabers satisfy the weapon guard");
        p.Entity.Class = 25;
        Check(!(bool)Call(TrojanTraining, null, "CanPerform", p, (ushort)11990), "non-Trojan cannot perform the Cross Saber art");
        p.Entity.Class = 15;
        TrojanTalk(earned, 10022, 50);
        Check(!Fenced(earned) && earned.Entity.MapID == 1002 && Revora.Kernel.GetDistance(earned.Entity.X, earned.Entity.Y, 425, 298) <= 6,
            "Trojan Star refers eligible players to the owned public entrance");
    }
}
