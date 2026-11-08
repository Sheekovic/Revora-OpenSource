using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static void TrojanInstructor(GameState p) {
        var home = Revora.Kernel.Maps[1004]; var npc = home.Npcs[10022]; var tile = FlowerTile(home, npc.X, npc.Y);
        p.Entity.Teleport(1004, tile.Item1, tile.Item2); p.ActiveNpc = 10022; EntityTable.SaveEntity(p);
    }
    static void VerifyTrojanRebirth(string runtime, Map town) {
        // Reuse actual first-rite/material initialization, without repeating the unrelated
        // hundred-combination second/paid suites for this two-art regression.
        VerifyFirstRebirth(runtime, town);
        Call(SecondRite, null, "Initialize"); Call(PaidRite, null, "Initialize");
        SecondRite.GetField("Clock", Hidden).SetValue(null, (Func<DateTime>)(() => secondNow));
        PaidRite.GetField("Clock", Hidden).SetValue(null, (Func<DateTime>)(() => paidNow));
        Query("CREATE TABLE IF NOT EXISTS reincarnation(id bigserial PRIMARY KEY,uid bigint NOT NULL,level bigint NOT NULL,experience bigint NOT NULL)");
        var home = World(runtime, 1004);
        using (var c = DataHolder.MySqlConnection) { c.Open(); using (var q = new Npgsql.NpgsqlCommand("SELECT cellx,celly,lookface FROM public.npcs WHERE id=10022 AND mapid=1004", c))
        using (var r = q.ExecuteReader()) { if (!r.Read()) throw new Exception("Trojan Star catalog missing.");
            home.AddNpc(new NpcSpawn { UID = 10022, MapID = 1004, X = Convert.ToUInt16(r[0]), Y = Convert.ToUInt16(r[1]), Mesh = Convert.ToUInt16(r[2]), Type = Enums.NpcType.Talker }); }}
        TrojanCombatCatalog(); Call(TrojanQuest, null, "Initialize");
        ushort[] arts = {11980,11990};
        foreach (int rite in new[] {0,1,2}) foreach (byte next in new byte[] {11,21}) foreach (bool fail in new[] {false,true}) {
            var p = rite == 0 ? RebirthPlayer(town, 15) : rite == 1 ? SecondPlayer(town, 15, 15) : PaidPlayer(town, 11);
            if (rite == 2) p.Entity.Class = 15;
            TrojanInstructor(p); TrojanLesson(p);
            Check(!Fenced(p) && arts.All(id => p.Spells.ContainsKey(id)), "ordinary Trojan learns both weapon arts before rite " + rite);
            foreach (ushort id in arts) { var skill = p.Spells[id]; skill.Level = 3; skill.PreviousLevel = 5; skill.Experience = 123; }
            SkillTable.SaveSpells(p);
            var sereth = town.Npcs[59558]; p.Entity.Teleport(1002, (ushort)(sereth.X-2), (ushort)(sereth.Y-2)); p.ActiveNpc = 59558; EntityTable.SaveEntity(p);
            byte oldReborn = p.Entity.Reborn; uint cp = p.Entity.ConquerPoints;
            if (rite == 0) RebirthOffer(p, next); else if (rite == 1) SecondOffer(p, next); else PaidOffer(p);
            if (fail) Query("CREATE FUNCTION trojan_rite_fail() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER trojan_rite_fail BEFORE UPDATE ON entities FOR EACH ROW EXECUTE FUNCTION trojan_rite_fail()");
            try { if (rite == 0) StoneNpc(p, 214); else if (rite == 1) StoneNpc(p, 180); else PaidRequest(p, next); }
            finally { if (fail) Query("DROP TRIGGER trojan_rite_fail ON entities;DROP FUNCTION trojan_rite_fail()"); }
            if (fail) {
                Check(Fenced(p) && p.Entity.ConquerPoints == cp && arts.All(id => p.Spells.ContainsKey(id) && p.Spells[id].Experience == 123),
                    "failed Trojan rite preserves in-memory learned arts and currency: " + rite + "/" + next);
                p = ReloadBall(p);
                Check(p.Entity.Reborn == oldReborn && p.Entity.Class == 15 && arts.All(id => p.Spells[id].Level == 3 && p.Spells[id].PreviousLevel == 5 && p.Spells[id].Experience == 123),
                    "failed Trojan rite preserves saved former proficiency after reload: " + rite + "/" + next);
            } else {
                Check(!Fenced(p) && p.Entity.Class == next && arts.All(id => !p.Spells.ContainsKey(id)), "committed rite removes both Trojan weapon arts immediately: " + rite + "/" + next);
                p = ReloadBall(p);
                Check(arts.All(id => !p.Spells.ContainsKey(id)) && Scalar("SELECT count(*) FROM skills WHERE entityid=@uid AND id IN(11980,11990)", p.Entity.UID) == 0,
                    "cold reload cannot restore removed Trojan weapon proficiency: " + rite + "/" + next);
                TrojanInstructor(p); TrojanLesson(p);
                if (next == 11) {
                    Check(p.Spells.ContainsKey(11990) && p.Spells[11990].Level == 0 && p.Spells[11990].PreviousLevel == 0 && p.Spells[11990].Experience == 0 && !p.Spells.ContainsKey(11980),
                        "reborn level-fifteen Trojan relearns only Mortal Strike from zero: " + rite);
                    p.Entity.Atributes += (ushort)(25 * Config.AttributePointsPerLevel); p.Entity.Level = 40; EntityTable.SaveEntity(p); TrojanLesson(p);
                    p = ReloadBall(p);
                    Check(arts.All(id => p.Spells.ContainsKey(id) && p.Spells[id].Level == 0 && p.Spells[id].PreviousLevel == 0 && p.Spells[id].Experience == 0),
                        "relearned Trojan weapon arts persist without a previous-level jump: " + rite);
                } else Check(arts.All(id => !p.Spells.ContainsKey(id)), "reborn Warrior cannot relearn Trojan weapon arts: " + rite);
            }
        }
    }
}
