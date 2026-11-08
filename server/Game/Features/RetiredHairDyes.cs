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
    // The installed 6711 item descriptions redeem these retired dyes for silver.
    internal static class RetiredHairDyes
    {
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;
        private sealed class Recent { internal readonly Dictionary<uint, DateTime> Items = new Dictionary<uint, DateTime>(); }
        private static readonly ConditionalWeakTable<GameState, Recent> recent = new ConditionalWeakTable<GameState, Recent>();
        private static bool Matches(uint id) { return id >= 1060030 && id <= 1060090 && id % 10 == 0; }
        internal static void Initialize()
        {
            using (var c = DataHolder.MySqlConnection) {
                c.Open(); Q.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_retired_dye_uses(
id uuid PRIMARY KEY,uid bigint NOT NULL CHECK(uid>0),item_uid bigint NOT NULL CHECK(item_uid>0),
item_type integer NOT NULL CHECK(item_type BETWEEN 1060030 AND 1060090 AND item_type%10=0),
stack_before integer NOT NULL CHECK(stack_before BETWEEN 1 AND 65535),bound boolean NOT NULL,
silver_before bigint NOT NULL CHECK(silver_before>=0),silver_after bigint NOT NULL CHECK(silver_after=silver_before+200),
used_at timestamptz NOT NULL DEFAULT now());");
            }
        }
        private static bool Ready(GameState p)
        { return Q.Player(p) && p.Booth == null && p.Trade != null && !p.Trade.InTrade; }
        internal static bool TryUse(ConquerItem item, GameState p)
        {
            if (item == null || !Matches(item.ID)) return false;
            if (!Ready(p)) return true;
            lock (p.ItemSyncRoot) try {
                ConquerItem owned; DateTime now = Clock();
                if (!Ready(p) || !p.Inventory.TryGetItem(item.UID, out owned) || !ReferenceEquals(owned, item)
                    || item.Position != 0 || item.Warehouse != 0 || item.StackSize == 0 || item.Lock != 0 || item.Suspicious || item.InWardrobe
                    || item.Days != 0 && now.ToLocalTime() >= item.DayStamp.AddDays(item.Days)) return true;
                var history = recent.GetValue(p, _ => new Recent());
                foreach (uint uid in history.Items.Where(x => x.Value <= now).Select(x => x.Key).ToArray()) history.Items.Remove(uid);
                if (history.Items.ContainsKey(item.UID)) return true;
                ulong before = p.Entity.Money;
                if (before > (ulong)long.MaxValue - 200) { p.Send("Your silver purse is full. Keep the dye for later."); return true; }
                ulong after = before + 200; ushort stack = item.StackSize;
                using (var c = DataHolder.MySqlConnection) {
                    c.Open(); using (var t = c.BeginTransaction()) {
                        SilverBalance.AssertSaved(c, t, p);
                        string debit = stack == 1 ? "DELETE FROM items" : "UPDATE items SET stacksize=stacksize-1";
                        if (Q.Execute(c, t, debit + @" WHERE uid=@item AND entityid=@uid AND id=@type AND warehouse=0 AND position=0
AND stacksize=@stack AND bound=@bound AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0
AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                            "item", (long)item.UID, "uid", (long)p.Entity.UID, "type", (int)item.ID, "stack", (int)stack,
                            "bound", item.Bound ? 1 : 0, "days", item.Days.ToString(CultureInfo.InvariantCulture),
                            "permanent", item.Days == 0, "stamp", item.DayStamp.ToBinary()) != 1)
                            throw new InvalidOperationException("Retired dye ownership changed.");
                        SilverBalance.Persist(c, t, p, after);
                        if (Q.Execute(c, t, @"INSERT INTO revora_retired_dye_uses(id,uid,item_uid,item_type,stack_before,bound,silver_before,silver_after)
VALUES(@id,@uid,@item,@type,@stack,@bound,@before,@after)", "id", Guid.NewGuid(), "uid", (long)p.Entity.UID,
                            "item", (long)item.UID, "type", (int)item.ID, "stack", (int)stack, "bound", item.Bound,
                            "before", checked((long)before), "after", checked((long)after)) != 1)
                            throw new InvalidOperationException("Retired dye redemption was not recorded.");
                        t.Commit();
                    }
                }
                if (history.Items.Count >= 64) history.Items.Remove(history.Items.OrderBy(x => x.Value).First().Key);
                history.Items[item.UID] = now.AddSeconds(1);
                Q.Install(p, stack == 1 ? new[] { item.UID } : new uint[0], new ConquerItem[0]);
                if (stack > 1) { item.StackSize = (ushort)(stack - 1); item.Mode = Enums.ItemMode.Update; item.Send(p); }
                SilverBalance.ApplyCommitted(p, after);
            } catch (Exception ex) { Q.Fail(p, "retired-dye", ex); }
            return true;
        }
    }
}
