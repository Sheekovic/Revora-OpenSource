using System;
using System.Linq;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Interfaces;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Revora introduction: one bound gift and one reading reward per character.
    internal static class MonkSutta
    {
        internal const uint Book = 3007561;
        internal static void Initialize()
        {
            if (!ConquerItemInformation.BaseInformations.ContainsKey(Book)) throw new InvalidOperationException("Sutta catalog missing.");
            using (var c = DataHolder.MySqlConnection) { c.Open(); Q.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_monk_sutta_gifts(
uid bigint PRIMARY KEY,item_uid bigint NOT NULL UNIQUE,created_at timestamptz NOT NULL DEFAULT now());
CREATE TABLE IF NOT EXISTS revora_monk_sutta_readings(uid bigint PRIMARY KEY,item_uid bigint NOT NULL,
stack_before integer NOT NULL,bound boolean NOT NULL,experience bigint NOT NULL CHECK(experience>0),
level_before integer NOT NULL,level_after integer NOT NULL,created_at timestamptz NOT NULL DEFAULT now());"); }
        }
        private static bool Near(GameState p)
        {
            INpc actor;
            return MonkEpicQuest.Enabled && MonkEpicStore.Eligible(p) && p.Spells != null && p.Map.ID == 1004 && p.Map.BaseID == 1004
                && p.Map.Npcs.TryGetValue(4271, out actor) && actor.MapID == 1004 && Kernel.GetDistance(p.Entity.X, p.Entity.Y, actor.X, actor.Y) <= 6;
        }
        private static bool Read(NpgsqlConnection c, NpgsqlTransaction t, uint uid)
        {
            using (var q = Q.Command(c, t, "SELECT EXISTS(SELECT 1 FROM revora_monk_sutta_readings WHERE uid=@uid)", "uid", (long)uid)) return Equals(q.ExecuteScalar(), true);
        }
        private static void Location(NpgsqlConnection c, NpgsqlTransaction t, GameState p)
        {
            using (var q = Q.Command(c, t, "SELECT mapid FROM entities WHERE uid=@uid", "uid", (long)p.Entity.UID))
                if (Convert.ToInt32(q.ExecuteScalar()) != 1004) throw new InvalidOperationException("Sutta reader location changed.");
        }
        internal static void Give(GameState p)
        {
            if (p == null || p.ItemSyncRoot == null) return;
            lock (p.ItemSyncRoot) try
            {
                if (!Near(p) || p.ActiveNpc != 4271) return;
                if (p.Inventory.Count >= 40) { p.Send("Make room for a sacred text."); return; }
                var profile = CharacterProgression.Capture(p); var book = Q.New(Book, true);
                using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                    SkillTable.LockOwner(c, t, p);
                    if (Read(c, t, p.Entity.UID)) { p.Send("You have learned this text's lesson. Seek the trials in Paradise."); return; }
                    using (var q = Q.Command(c, t, "SELECT EXISTS(SELECT 1 FROM revora_monk_sutta_gifts WHERE uid=@uid)", "uid", (long)p.Entity.UID))
                        if (Equals(q.ExecuteScalar(), true)) { p.Send("I have entrusted a Sutta to you. Read it beside me."); return; }
                    CharacterProgression.Persist(c, t, p, profile); Location(c, t, p);
                    var bag = new System.Collections.Generic.List<uint>();
                    using (var q = Q.Command(c, t, "SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid", "uid", (long)p.Entity.UID))
                    using (var r = q.ExecuteReader()) while (r.Read()) { if (bag.Count >= 40) throw new InvalidOperationException("Sutta bag exceeds capacity."); bag.Add(checked((uint)r.GetInt64(0))); }
                    if (!bag.SequenceEqual(p.Inventory.Objects.Select(i => i.UID).OrderBy(i => i))) throw new InvalidOperationException("Sutta bag changed.");
                    if (Q.Execute(c, t, @"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability)
VALUES(@item,@type,@uid,0,0,1,1,@dur,@dur)", "item", (long)book.UID, "type", (int)Book, "uid", (long)p.Entity.UID, "dur", (int)book.Durability) != 1)
                        throw new InvalidOperationException("Sutta gift not saved.");
                    if (Q.Execute(c, t, "INSERT INTO revora_monk_sutta_gifts(uid,item_uid) VALUES(@uid,@item)", "uid", (long)p.Entity.UID, "item", (long)book.UID) != 1)
                        throw new InvalidOperationException("Sutta gift receipt not saved.");
                    if (!Near(p) || p.ActiveNpc != 4271) throw new InvalidOperationException("Sutta recipient changed."); t.Commit();
                }}
                CharacterProgression.Committed(p, profile); Q.Install(p, new uint[0], new[] { book });
                p.Send("Read this Sutta beside me. Its lesson grants 30 minutes of experience, once in your lifetime.");
            }
            catch (Exception ex) { Q.Fail(p, "monk-sutta-gift", ex); }
        }
        internal static bool TryHandle(GameState p, byte[] packet)
        {
            ConquerItem item;
            if (p == null || p.Inventory == null || packet == null || packet.Length < 12 || !p.Inventory.TryGetItem(BitConverter.ToUInt32(packet, 8), out item) || item.ID != Book) return false;
            if (packet.Length < 32 || packet.Length > 128 || BitConverter.ToUInt16(packet, 0) + 8 != packet.Length || BitConverter.ToUInt16(packet, 2) != 1009
                || BitConverter.ToUInt16(packet, 20) != 4 || BitConverter.ToUInt64(packet, packet.Length - 8) != Revora.Network.PacketHandler.ClientSeal) return true;
            TryUse(item, p); return true;
        }
        internal static bool TryUse(ConquerItem item, GameState p)
        {
            if (item == null || item.ID != Book) return false;
            if (!MonkEpicQuest.Enabled || !Q.Player(p)) return true;
            lock (p.ItemSyncRoot) lock (MentorExperience.Sync) try
            {
                ConquerItem owned;
                if (!Q.Player(p) || !p.Inventory.TryGetItem(item.UID, out owned) || !ReferenceEquals(item, owned) || !MonkEpicStore.Usable(item)) return true;
                if (!Near(p)) { p.Send("Seek Head Abbot in the Job Center (80,45) to read this Sutta."); return true; }
                if (p.Entity.Level >= Config.LevelCap || p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Cursed)) { p.Send("Keep this Sutta until you can receive its experience."); return true; }
                ulong award = ExperienceMath.Scale(p.ExpBall, 30, 60); if (award == 0) return true;
                byte level = p.Entity.Level;
                var plan = new ExperienceRewardPlan(CharacterProgression.Capture(p), award, Config.LevelCap, Config.AttributePointsPerLevel, ReincarnationRecovery.Snapshot(p));
                ulong autoHunt = p.Entity.InAutoHunt ? checked(p.Entity.AutoHuntEXP + award / Math.Max(1u, Config.AutoHuntExperienceDivisor)) : p.Entity.AutoHuntEXP;
                ExperienceRewards.Effects effects;
                using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                    SkillTable.LockOwner(c, t, p);
                    if (Read(c, t, p.Entity.UID)) { p.Send("You have learned this text's lesson. Seek the trials in Paradise."); return true; }
                    CharacterProgression.Persist(c, t, p, plan.After); Location(c, t, p);
                    string debit = item.StackSize <= 1 ? "DELETE FROM items" : "UPDATE items SET stacksize=stacksize-1";
                    if (Q.Execute(c, t, debit + @" WHERE uid=@item AND entityid=@uid AND id=@type AND position=0 AND warehouse=0
AND stacksize=@stack AND bound=@bound AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0'",
                        "item", (long)item.UID, "uid", (long)p.Entity.UID, "type", (int)Book, "stack", (int)item.StackSize, "bound", item.Bound ? 1 : 0) != 1)
                        throw new InvalidOperationException("Sutta ownership changed.");
                    effects = ExperienceRewards.Persist(c, t, p, plan); ReincarnationRecovery.Consume(c, t, p, plan.Restored);
                    if (Q.Execute(c, t, @"INSERT INTO revora_monk_sutta_readings(uid,item_uid,stack_before,bound,experience,level_before,level_after)
VALUES(@uid,@item,@stack,@bound,@exp,@before,@after)", "uid", (long)p.Entity.UID, "item", (long)item.UID, "stack", (int)item.StackSize,
                        "bound", item.Bound, "exp", checked((long)award), "before", (int)level, "after", plan.After.Values[0]) != 1)
                        throw new InvalidOperationException("Sutta reading receipt not saved.");
                    if (!Near(p) || !ReferenceEquals(p.Inventory.Objects.SingleOrDefault(i => i.UID == item.UID), item)) throw new InvalidOperationException("Sutta reader changed."); t.Commit();
                }}
                Q.Install(p, item.StackSize <= 1 ? new[] { item.UID } : new uint[0], new ConquerItem[0]);
                if (item.StackSize > 1) { item.StackSize--; item.Mode = Enums.ItemMode.Update; item.Send(p); item.Mode = Enums.ItemMode.Default; }
                ReincarnationRecovery.Committed(p, plan.Restored); MentorExperience.Apply(p, effects.MentorCredit);
                p.ApplyExperienceReward(plan, effects, autoHunt, true);
                p.Send("Offer flowers to Fortune Arhat, then seek the Altar Cleanser and Victory Buddha. A Boundless Heart will awaken your Prayer Beads.");
            }
            catch (Exception ex) { Q.Fail(p, "monk-sutta-reading", ex); }
            return true;
        }
    }
}
