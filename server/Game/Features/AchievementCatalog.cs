using System;
using System.Collections.Generic;
using System.Globalization;
using Revora.Client;

namespace Revora.Game.Features
{
    // Numeric interoperability facts only. See docs/research/WARDROBE_QUEST_EVENT_RESEARCH.md.
    internal static class AchievementCatalog
    {
        internal const int WordCount = 13;
        internal const int Count = 337;
        private static readonly uint[] Masks = {
            0xFFFFFAFFu, 0xEFF60000u, 0xFFFFFFFFu, 0xFBFFFFFFu, 0x1FFFFFFFu,
            0x533F3C70u, 0x07DFFF87u, 0xFFFDFBFFu, 0xFFFDFFFFu, 0xE42EBA1Fu,
            0xCFFFFDFFu, 0xFFFFFFFFu, 0x007FFFFFu
        };

        internal static bool TryPosition(int id, out int position)
        {
            position = -1;
            int group = id / 100 - 101, bit = id % 100 - 1;
            if (group < 0 || group >= WordCount || bit < 0 || bit >= 32
                || (Masks[group] & (1u << bit)) == 0) return false;
            position = group * 32 + bit;
            return true;
        }

        internal static bool Known(int id) { int position; return TryPosition(id, out position); }

        internal static int[] Ids()
        {
            var ids = new List<int>(Count);
            for (int group = 0; group < WordCount; group++)
                for (int bit = 0; bit < 32; bit++)
                    if ((Masks[group] & (1u << bit)) != 0) ids.Add((101 + group) * 100 + bit + 1);
            return ids.ToArray();
        }

        internal static uint[] Words(IEnumerable<int> ids)
        {
            var words = new uint[WordCount];
            foreach (int id in ids) {
                int position;
                if (!TryPosition(id, out position)) throw new ArgumentOutOfRangeException("ids");
                words[position / 32] |= 1u << (position % 32);
            }
            return words;
        }

        internal static int LegacyKnownCount(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return 0;
            string[] parts = raw.Split('#');
            int count = 0;
            for (int i = 0; i < WordCount && i < parts.Length; i++) {
                uint word;
                if (!uint.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out word)) continue;
                word &= Masks[i];
                while (word != 0) { word &= word - 1; count++; }
            }
            return count;
        }

        private static bool Mastered(GameState player, ushort id)
        {
            Revora.Interfaces.IProf proficiency;
            return player.Proficiencies != null && player.Proficiencies.TryGetValue(id, out proficiency)
                && proficiency != null && proficiency.ID == id && proficiency.Level >= 12 && proficiency.Level <= 20;
        }

        internal static int[] ObserveMasteries(GameState player)
        {
            var ids = new List<int>(34);
            if (player == null || player.Fake || player.Entity == null || !player.Entity.FullyLoaded
                || player.Entity.PersistenceRequiresReload) return ids.ToArray();
            // Numeric identities from the hash-reviewed 6711 WeaponSkillName.ini.
            if (Mastered(player, 0)) ids.Add(10321);
            if (Mastered(player, 420)) ids.Add(10322);
            if (Mastered(player, 410)) ids.Add(10323);
            if (Mastered(player, 460)) ids.Add(10324);
            if (Mastered(player, 540)) ids.Add(10325);
            if (Mastered(player, 580)) ids.Add(10326);
            if (Mastered(player, 560)) ids.Add(10327);
            if (Mastered(player, 490)) ids.Add(10328);
            if (Mastered(player, 561)) ids.Add(10329);
            if (Mastered(player, 510)) ids.Add(10330);
            if (Mastered(player, 530)) ids.Add(10331);
            if (Mastered(player, 430)) ids.Add(10332);
            if (Mastered(player, 450)) ids.Add(10401);
            if (Mastered(player, 480)) ids.Add(10402);
            if (Mastered(player, 481)) ids.Add(10403);
            if (Mastered(player, 440)) ids.Add(10404);
            if (Mastered(player, 500)) ids.Add(10405);
            if (Mastered(player, 610)) ids.Add(10406);
            if (Mastered(player, 601)) ids.Add(10407);
            if (Mastered(player, 421)) ids.Add(10408);
            if (Mastered(player, 900)) ids.Add(11132);
            if (Mastered(player, 611)) ids.Add(11210);
            if (Mastered(player, 612)) ids.Add(11211);
            if (Mastered(player, 511)) ids.Add(11212);
            if (Mastered(player, 613)) ids.Add(11213);
            if (Mastered(player, 614)) ids.Add(11214);
            if (Mastered(player, 616)) ids.Add(11215);
            if (Mastered(player, 617)) ids.Add(11221);
            if (Mastered(player, 620)) ids.Add(11222);
            if (Mastered(player, 622)) ids.Add(11223);
            if (Mastered(player, 624)) ids.Add(11302);
            if (Mastered(player, 626)) ids.Add(11303);
            if (Mastered(player, 671)) ids.Add(11322);
            if (Mastered(player, 670)) ids.Add(11323);
            return ids.ToArray();
        }

        // Preserve current attainment before a rite resets proficiency levels.
        // Use the existing observation identity so later refreshes cannot duplicate credit.
        internal static void PreserveMasteries(Npgsql.NpgsqlConnection c, Npgsql.NpgsqlTransaction t, GameState player)
        {
            if (t == null || player == null || !System.Threading.Monitor.IsEntered(player.ItemSyncRoot))
                throw new InvalidOperationException("Mastery preservation requires the rite transaction and owner lock.");
            foreach (int id in ObserveMasteries(player))
                ProgressionEvidence.Complete(c, t, player.Entity.UID, id, "observed-state", "predicate-v1", 1);
        }

        // These predicates are directly observable. Quest, interaction and
        // tournament achievements need their own committed completion producers.
        internal static int[] Observe(GameState player)
        {
            var ids = new List<int>(41);
            if (player == null || player.Fake || player.Entity == null || !player.Entity.FullyLoaded
                || player.Entity.PersistenceRequiresReload) return ids.ToArray();
            if (player.Entity.Money >= 3000000) ids.Add(10106);
            if (player.Entity.Level >= 130) ids.Add(10310);
            if (player.Entity.Level >= 140) ids.Add(10311);
            if (QuestInventory.Player(player, false)) {
                int power = player.Entity.BattlePower;
                if (power >= 100) ids.Add(10312);
                if (power >= 200) ids.Add(10313);
                if (power >= 300) ids.Add(10314);
            }
            if (player.VirtuePoints >= 5000) ids.Add(10317);
            ids.AddRange(ObserveMasteries(player));
            var mount = player.Equipment == null ? null : player.Equipment.TryGetItem(12);
            if (mount != null && mount.UID != 0 && mount.ID == 300000 && mount.Position == 12) {
                ids.Add(10107);
                if (mount.Plus >= 12) ids.Add(10108);
            }
            if (player.Guild != null && player.Guild.ID != 0 && player.Entity.GuildID == player.Guild.ID) {
                ids.Add(10218);
                if (player.Guild.Leader != null && player.Guild.Leader.ID == player.Entity.UID) ids.Add(10219);
            }
            return ids.ToArray();
        }
    }
}
