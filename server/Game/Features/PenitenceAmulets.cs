using System;
using System.Globalization;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class PenitenceAmulets
    {
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;
        private static bool enabled;
        private static bool Handles(uint id) { return id == 720128 || id == 723727; }
        internal static void Initialize() {
            foreach (uint id in new uint[] { 720128, 723727 })
                if (!ConquerItemInformation.BaseInformations.ContainsKey(id)) throw new InvalidOperationException("Amulet catalog missing.");
            using (var c = DataHolder.MySqlConnection) { c.Open(); Q.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_penitence_cooldowns(
uid bigint PRIMARY KEY CHECK(uid>0),used_at timestamptz NOT NULL);
CREATE TABLE IF NOT EXISTS revora_penitence_uses(
id uuid PRIMARY KEY,uid bigint NOT NULL CHECK(uid>0),item_uid bigint NOT NULL,item_type integer NOT NULL CHECK(item_type IN(720128,723727)),
stack_before integer NOT NULL CHECK(stack_before>=0),bound boolean NOT NULL,pk_before integer NOT NULL CHECK(pk_before BETWEEN 30 AND 65535),
pk_after integer NOT NULL CHECK(pk_after=pk_before-30),map_base integer NOT NULL,vip integer NOT NULL,used_at timestamptz NOT NULL);"); }
            enabled = true;
        }
        private static bool Ready(GameState p) {
            return enabled && Q.Player(p) && p.Map != null && p.Map.ID == p.Entity.MapID
                && p.Booth == null && p.Trade != null && !p.Trade.InTrade;
        }
        internal static bool TryHandle(GameState p, byte[] packet) {
            ConquerItem item;
            if (p == null || p.Inventory == null || packet == null || packet.Length < 12
                || !p.Inventory.TryGetItem(BitConverter.ToUInt32(packet, 8), out item) || !Handles(item.ID)) return false;
            if (packet.Length < 32 || packet.Length > 128 || BitConverter.ToUInt16(packet, 0) + 8 != packet.Length
                || BitConverter.ToUInt16(packet, 2) != 1009 || BitConverter.ToUInt16(packet, 20) != 4
                || BitConverter.ToUInt64(packet, packet.Length - 8) != Revora.Network.PacketHandler.ClientSeal) return true;
            TryUse(item, p); return true;
        }
        internal static bool TryUse(ConquerItem item, GameState p) {
            if (item == null || !Handles(item.ID)) return false;
            if (!Ready(p)) return true;
            lock (p.ItemSyncRoot) lock (p.PkSyncRoot) try {
                ConquerItem owned; DateTime now = Clock();
                if (now.Kind != DateTimeKind.Utc) throw new InvalidOperationException("Amulet clock requires UTC.");
                if (!Ready(p) || !p.Inventory.TryGetItem(item.UID, out owned) || !ReferenceEquals(item, owned)
                    || item.Position != 0 || item.Warehouse != 0 || item.Lock != 0 || item.Suspicious || item.InWardrobe
                    || (item.Days == 0 ? item.TimeLeftInMinutes != 0 : now.ToLocalTime() >= item.DayStamp.AddDays(item.Days))) return true;
                ushort before = p.Entity.PKPoints;
                if (before < 30) { p.Send("You need at least 30 PK points to use this amulet."); return true; }
                ushort after = (ushort)(before - 30); var map = p.Map; byte vip = p.Entity.VIPLevel;
                // Catalog interpretation: VIP3+ overrides either location's gameplay cooldown.
                // One second is request debounce, shared by aliases and reconnects.
                int seconds = vip >= 3 ? 1 : map.BaseID == 10250 ? 1800 : 3600;
                using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                    using (var q = Q.Command(c, t, "SELECT uid FROM entities WHERE uid=@uid FOR UPDATE", "uid", (long)p.Entity.UID))
                        if (q.ExecuteScalar() == null) throw new InvalidOperationException("Amulet owner missing.");
                    using (var q = Q.Command(c, t, "SELECT used_at FROM revora_penitence_cooldowns WHERE uid=@uid", "uid", (long)p.Entity.UID)) {
                        object used = q.ExecuteScalar();
                        if (used != null && now < ((DateTime)used).ToUniversalTime().AddSeconds(seconds)) {
                            p.Send("The amulet's power has not yet recovered."); return true;
                        }
                    }
                    string verb = item.StackSize <= 1 ? "DELETE FROM items" : "UPDATE items SET stacksize=stacksize-1";
                    if (Q.Execute(c, t, verb + @" WHERE uid=@item AND entityid=@uid AND id=@type AND position=0 AND warehouse=0
AND stacksize=@stack AND bound=@bound AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0
AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                        "item", (long)item.UID, "uid", (long)p.Entity.UID, "type", (int)item.ID, "stack", (int)item.StackSize,
                        "bound", item.Bound ? 1 : 0, "days", item.Days.ToString(CultureInfo.InvariantCulture),
                        "permanent", item.Days == 0, "stamp", item.DayStamp.ToBinary()) != 1) throw new InvalidOperationException("Amulet ownership changed.");
                    PkBalance.Persist(c, t, p, after);
                    if (Q.Execute(c, t, @"INSERT INTO revora_penitence_cooldowns(uid,used_at) VALUES(@uid,@now)
ON CONFLICT(uid) DO UPDATE SET used_at=EXCLUDED.used_at", "uid", (long)p.Entity.UID, "now", now) != 1)
                        throw new InvalidOperationException("Amulet cooldown was not saved.");
                    if (Q.Execute(c, t, @"INSERT INTO revora_penitence_uses(id,uid,item_uid,item_type,stack_before,bound,pk_before,pk_after,map_base,vip,used_at)
VALUES(@id,@uid,@item,@type,@stack,@bound,@before,@after,@map,@vip,@now)", "id", Guid.NewGuid(), "uid", (long)p.Entity.UID,
                        "item", (long)item.UID, "type", (int)item.ID, "stack", (int)item.StackSize, "bound", item.Bound,
                        "before", (int)before, "after", (int)after, "map", (int)map.BaseID, "vip", (int)vip, "now", now) != 1)
                        throw new InvalidOperationException("Amulet receipt was not saved.");
                    if (!Ready(p) || !ReferenceEquals(map, p.Map) || p.Entity.VIPLevel != vip)
                        throw new InvalidOperationException("Amulet session changed.");
                    t.Commit();
                }}
                PkBalance.ApplyCommitted(p, after);
                if (item.StackSize <= 1) Q.Install(p, new[] { item.UID }, new ConquerItem[0]);
                else { item.StackSize--; item.Mode = Enums.ItemMode.Update; item.Send(p); item.Mode = Enums.ItemMode.Default; }
            } catch (Exception ex) { Q.Fail(p, "penitence-use", ex); }
            return true;
        }
    }
}
