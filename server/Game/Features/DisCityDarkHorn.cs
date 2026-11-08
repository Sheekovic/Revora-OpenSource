using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class DisCityDarkHorn
    {
        internal const uint Horn = 790001;
        private sealed class Offer { internal uint Item; internal DateTime Until; }
        private static readonly ConditionalWeakTable<GameState, Offer> offers = new ConditionalWeakTable<GameState, Offer>();
        private static bool enabled;
        internal static void Initialize()
        {
            if (!ConquerItemInformation.BaseInformations.ContainsKey(Horn)) throw new InvalidOperationException("Dark Horn catalog missing.");
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); Q.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_dis_horn_claims(
item_uid bigint PRIMARY KEY REFERENCES revora_dis_relics(item_uid), uid bigint NOT NULL,
experience bigint NOT NULL CHECK(experience>0), level_before integer NOT NULL, level_after integer NOT NULL,
claimed_at timestamptz NOT NULL);");
            }
            enabled = true;
        }
        private static bool Owned(NpgsqlConnection c, NpgsqlTransaction t, GameState p, uint uid)
        {
            ConquerItem item;
            if (!p.Inventory.TryGetItem(uid, out item) || item.Lock != 0 || item.Days != 0 || item.InWardrobe
                || item.StackSize != 1 || !Q.Owns(c, t, p, uid, Horn)) return false;
            using (var q = Q.Command(c, t, @"SELECT EXISTS(SELECT 1 FROM revora_dis_relics r
JOIN revora_dis_completions v ON v.run=r.run AND v.uid=r.uid JOIN items i ON i.uid=r.item_uid
WHERE r.item_uid=@item AND r.uid=@uid AND r.item_type=@type AND r.used_at IS NULL AND i.locked=0 AND i.days='0')",
                "item", (long)uid, "uid", (long)p.Entity.UID, "type", (int)Horn)) return Equals(q.ExecuteScalar(), true);
        }
        private static string Claim(GameState p)
        {
            Offer offer;
            if (!offers.TryGetValue(p, out offer) || offer.Until <= DisCityQuest.Clock()) return "Show me your Dark Horn first.";
            if (!CharacterProgression.Managed(p)) return "Return after reconnecting, and I will prepare your reward.";
            if (p.Entity.Level >= Config.LevelCap) return "You have reached the summit. Keep your Dark Horn for another day.";
            if (p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Cursed)) return "Let the curse pass before taking this reward.";
            lock (MentorExperience.Sync)
            {
                ExperienceRewardPlan plan; ExperienceRewards.Effects effects;
                ulong award = ExperienceMath.Scale(p.ExpBall, 5, 1);
                ulong autoHunt = p.Entity.InAutoHunt ? checked(p.Entity.AutoHuntEXP + award / Math.Max(1u, Config.AutoHuntExperienceDivisor)) : p.Entity.AutoHuntEXP;
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open(); using (var t = c.BeginTransaction())
                    {
                        SkillTable.LockOwner(c, t, p);
                        if (!Owned(c, t, p, offer.Item)) return "Bring the Dark Horn earned in your own battle.";
                        plan = new ExperienceRewardPlan(CharacterProgression.Capture(p), award, Config.LevelCap,
                            Config.AttributePointsPerLevel, ReincarnationRecovery.Snapshot(p));
                        if (Q.Execute(c, t, @"DELETE FROM items WHERE uid=@item AND entityid=@uid AND id=@type
AND position=0 AND warehouse=0 AND bound=1 AND stacksize=1 AND locked=0 AND days='0'",
                            "item", (long)offer.Item, "uid", (long)p.Entity.UID, "type", (int)Horn) != 1)
                            throw new InvalidOperationException("Dark Horn ownership changed.");
                        if (Q.Execute(c, t, @"UPDATE revora_dis_relics SET used_at=@now
WHERE item_uid=@item AND uid=@uid AND item_type=@type AND used_at IS NULL", "now", DisCityQuest.Clock(),
                            "item", (long)offer.Item, "uid", (long)p.Entity.UID, "type", (int)Horn) != 1)
                            throw new InvalidOperationException("Dark Horn receipt changed.");
                        CharacterProgression.Persist(c, t, p, plan.After);
                        effects = ExperienceRewards.Persist(c, t, p, plan);
                        ReincarnationRecovery.Consume(c, t, p, plan.Restored);
                        Q.Execute(c, t, @"INSERT INTO revora_dis_horn_claims(item_uid,uid,experience,level_before,level_after,claimed_at)
VALUES(@item,@uid,@exp,@before,@after,@now)", "item", (long)offer.Item, "uid", (long)p.Entity.UID,
                            "exp", checked((long)award), "before", (int)p.Entity.Level, "after", (int)plan.After.Values[0], "now", DisCityQuest.Clock());
                        t.Commit();
                    }
                }
                offers.Remove(p); ReincarnationRecovery.Committed(p, plan.Restored);
                MentorExperience.Apply(p, effects.MentorCredit);
                Q.Install(p, new[] { offer.Item }, new ConquerItem[0]);
                p.ApplyExperienceReward(plan, effects, autoHunt, true);
                return "The horn's power is yours. May it serve you well in battle.";
            }
        }
        internal static bool HandleNpc(GameState p, NpcRequest request)
        {
            if (!enabled || p == null || request == null || p.ActiveNpc != DisCityWorld.Saint
                || request.OptionID != 5 && request.OptionID != 50 && request.OptionID != 255) return false;
            if (!Q.Player(p)) return true;
            lock (p.ItemSyncRoot) try
            {
                if (!Q.Near(p, DisCityWorld.Saint, 1002)) return request.OptionID != 255;
                if (request.OptionID == 255) { offers.Remove(p); return false; }
                var d = new Npcs(p);
                if (request.OptionID == 50) d.Text(Claim(p));
                else
                {
                    offers.Remove(p); uint horn = 0;
                    using (var c = DataHolder.MySqlConnection)
                    {
                        c.Open(); foreach (var item in p.Inventory.Objects.Where(i => i.ID == Horn).OrderBy(i => i.UID))
                            if (Owned(c, null, p, item.UID)) { horn = item.UID; break; }
                    }
                    if (horn == 0) d.Text("Bring me a Dark Horn from your victory over Pluto.");
                    else
                    {
                        offers.Add(p, new Offer { Item = horn, Until = DisCityQuest.Clock().AddMinutes(1) });
                        d.Text("This Dark Horn holds 300 minutes of experience. Shall I release its power?");
                        d.Option("Take the experience.", 50);
                    }
                }
                d.Option("Farewell.", 255); d.Send();
            }
            catch (Exception ex) { Q.Fail(p, "dis-city-dark-horn", ex); }
            return true;
        }
        internal static bool TryUse(ConquerItem item, GameState p)
        {
            if (item == null || item.ID != Horn) return false;
            if (!enabled || !Q.Player(p)) return true;
            lock (p.ItemSyncRoot)
            {
                ConquerItem owned;
                if (p.Inventory.TryGetItem(item.UID, out owned) && ReferenceEquals(owned, item))
                    p.Send("Bring your Dark Horn to Solar Saint in Twin City (327,250) for 300 minutes of experience.");
            }
            return true;
        }
    }
}
