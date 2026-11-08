using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class PrayingStones
    {
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;
        private sealed class Recent { internal readonly Dictionary<uint, DateTime> Items = new Dictionary<uint, DateTime>(); }
        private static readonly ConditionalWeakTable<GameState, Recent> recent = new ConditionalWeakTable<GameState, Recent>();
        private static bool Handles(uint id) { return id == 720578 || id >= 1200000 && id <= 1200002; }
        internal static void Initialize()
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var t = c.BeginTransaction()) { Q.Execute(c, t, @"CREATE TABLE IF NOT EXISTS revora_praying_stone_uses(
id uuid PRIMARY KEY,uid bigint NOT NULL CHECK(uid>0),item_uid bigint NOT NULL CHECK(item_uid>0),
item_type integer NOT NULL CHECK(item_type BETWEEN 1200000 AND 1200002),stack_before integer NOT NULL CHECK(stack_before BETWEEN 1 AND 65535),
bound boolean NOT NULL,seconds_before bigint NOT NULL CHECK(seconds_before BETWEEN 0 AND 4294967295),
seconds_after bigint NOT NULL CHECK(seconds_after BETWEEN 0 AND 4294967295),mentor bigint NOT NULL CHECK(mentor>=0),
mentor_hours integer NOT NULL CHECK(mentor_hours>=0),used_at timestamptz NOT NULL DEFAULT now(),
CHECK(seconds_after=seconds_before+CASE item_type WHEN 1200000 THEN 259200 WHEN 1200001 THEN 604800 ELSE 2592000 END),
CHECK(mentor_hours=CASE WHEN mentor=0 THEN 0 WHEN item_type=1200000 THEN 7 WHEN item_type=1200001 THEN 16 ELSE 72 END));
ALTER TABLE revora_praying_stone_uses DROP CONSTRAINT IF EXISTS revora_praying_stone_uses_item_type_check,
DROP CONSTRAINT IF EXISTS revora_praying_stone_uses_stack_before_check,
DROP CONSTRAINT IF EXISTS revora_praying_stone_uses_check,
DROP CONSTRAINT IF EXISTS revora_praying_stone_uses_check1,
ADD CONSTRAINT revora_praying_stone_uses_item_type_check CHECK(item_type IN(720578,1200000,1200001,1200002)),
ADD CONSTRAINT revora_praying_stone_uses_stack_before_check CHECK(stack_before BETWEEN 0 AND 65535),
ADD CONSTRAINT revora_praying_stone_uses_check CHECK(seconds_after=seconds_before+CASE item_type WHEN 1200000 THEN 259200 WHEN 1200002 THEN 2592000 ELSE 604800 END),
ADD CONSTRAINT revora_praying_stone_uses_check1 CHECK(mentor_hours=CASE WHEN mentor=0 THEN 0 WHEN item_type=1200000 THEN 7 WHEN item_type=1200002 THEN 72 ELSE 16 END);"); t.Commit(); }
            }
        }
        internal static bool TryHandle(GameState p, byte[] packet)
        {
            ConquerItem item;
            if (p == null || p.Inventory == null || packet == null || packet.Length < 12
                || !p.Inventory.TryGetItem(BitConverter.ToUInt32(packet, 8), out item) || !Handles(item.ID)) return false;
            if (packet.Length < 32 || packet.Length > 128 || BitConverter.ToUInt16(packet, 0) + 8 != packet.Length
                || BitConverter.ToUInt16(packet, 2) != 1009 || BitConverter.ToUInt16(packet, 20) != 4
                || BitConverter.ToUInt64(packet, packet.Length - 8) != Revora.Network.PacketHandler.ClientSeal) return true;
            TryUse(item, p); return true;
        }
        private static bool Ready(GameState p)
        { return Q.Player(p) && p.Booth == null && p.Trade != null && !p.Trade.InTrade; }
        internal static bool TryUse(ConquerItem item, GameState p)
        {
            if (item == null || !Handles(item.ID)) return false;
            if (!Ready(p)) return true;
            lock (p.ItemSyncRoot)
            lock (MentorExperience.Sync) try
            {
                ConquerItem owned; var now = Clock();
                if (!Ready(p) || !p.Inventory.TryGetItem(item.UID, out owned) || !ReferenceEquals(owned, item)
                    || item.Position != 0 || item.Warehouse != 0 || item.Lock != 0 || item.Suspicious || item.InWardrobe
                    || (item.Days == 0 ? item.TimeLeftInMinutes != 0 : now.ToLocalTime() >= item.DayStamp.AddDays(item.Days))) return true;
                var history = recent.GetValue(p, _ => new Recent());
                foreach (uint uid in history.Items.Where(i => i.Value <= now).Select(i => i.Key).ToArray()) history.Items.Remove(uid);
                if (history.Items.ContainsKey(item.UID)) return true;
                uint seconds = (item.ID == 1200000 ? 3u : item.ID == 1200002 ? 30u : 7u) * 86400;
                uint before = p.Entity.HeavenBlessing;
                if (seconds > uint.MaxValue - before) { p.Send("Your blessing time is full. Keep the stone for later."); return true; }
                uint next = before + seconds; ushort stack = item.StackSize;
                MentorBlessingClaims.Contribution credit;
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open(); using (var t = c.BeginTransaction())
                    {
                        BlessingBalance.AssertSaved(c, t, p);
                        string debit = stack <= 1 ? "DELETE FROM items" : "UPDATE items SET stacksize=stacksize-1";
                        if (Q.Execute(c, t, debit + @" WHERE uid=@item AND entityid=@uid AND id=@type AND warehouse=0 AND position=0
AND stacksize=@stack AND bound=@bound AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0
AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                            "item", (long)item.UID, "uid", (long)p.Entity.UID, "type", (int)item.ID, "stack", (int)stack, "bound", item.Bound ? 1 : 0,
                            "days", item.Days.ToString(CultureInfo.InvariantCulture), "permanent", item.Days == 0, "stamp", item.DayStamp.ToBinary()) != 1)
                            throw new InvalidOperationException("Praying Stone ownership changed.");
                        credit = MentorBlessingClaims.Credit(c, t, p, seconds);
                        BlessingBalance.Persist(c, t, p, next);
                        if (Q.Execute(c, t, @"INSERT INTO revora_praying_stone_uses(id,uid,item_uid,item_type,stack_before,bound,seconds_before,seconds_after,mentor,mentor_hours)
VALUES(@id,@uid,@item,@type,@stack,@bound,@before,@after,@mentor,@hours)", "id", Guid.NewGuid(), "uid", (long)p.Entity.UID,
                            "item", (long)item.UID, "type", (int)item.ID, "stack", (int)stack, "bound", item.Bound,
                            "before", (long)before, "after", (long)next, "mentor", credit == null ? 0L : (long)credit.Mentor,
                            "hours", credit == null ? 0 : (int)(seconds / 36000)) != 1)
                            throw new InvalidOperationException("Praying Stone use was not recorded.");
                        if (!Ready(p)) throw new InvalidOperationException("Praying Stone session changed.");
                        t.Commit();
                    }
                }
                if (history.Items.Count >= 64) history.Items.Remove(history.Items.OrderBy(i => i.Value).First().Key);
                history.Items[item.UID] = now.AddSeconds(1);
                Q.Install(p, stack <= 1 ? new[] { item.UID } : new uint[0], new ConquerItem[0]);
                if (stack > 1) { item.StackSize = (ushort)(stack - 1); item.Mode = Enums.ItemMode.Update; item.Send(p); item.Mode = Enums.ItemMode.Default; }
                BlessingBalance.ApplyCommitted(p, next);
                MentorBlessingClaims.ApplyCredit(p, credit);
                p.Entity.Update((byte)FlagsUpdate.DataType.OnlineTraining, p.OnlineTrainingPoints, false);
            }
            catch (Exception ex) { Q.Fail(p, "praying-stone", ex); }
            return true;
        }
    }
}
