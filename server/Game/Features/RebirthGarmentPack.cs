#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class RebirthGarmentPack
    {
        internal const uint ItemId = 723981;
        internal static Func<int,int> Roll = RandomIndex;
        internal static Action? BeforeCommit = null;
        internal static Action? AfterCommit = null;
        private static bool enabled;
        internal static uint[] Pool() => StorageItem.Garments.Values
            .Where(x => x.Type == 1 && (x.Stars == 4 || x.Stars == 5)
                && ConquerItemInformation.BaseInformations.ContainsKey(x.ID)
                && ConquerItemInformation.BaseInformations[x.ID].TimeTillRemovInMinutes == 0)
            .Select(x => x.ID).Distinct().OrderBy(x => x).ToArray();
        private static int RandomIndex(int count)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            var bytes = new byte[4]; uint value, limit = uint.MaxValue - uint.MaxValue % (uint)count;
            using (var rng = RandomNumberGenerator.Create())
                do { rng.GetBytes(bytes); value = BitConverter.ToUInt32(bytes, 0); } while (value >= limit);
            return (int)(value % (uint)count);
        }
        private static int Draw(int count)
        {
            int value = Roll(count);
            if (value < 0 || value >= count) throw new InvalidOperationException("Garment draw outside pool.");
            return value;
        }
        internal static void Initialize()
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open();
                Q.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_rebirth_garments(
pack_uid bigint PRIMARY KEY, uid bigint NOT NULL, result_uid bigint NOT NULL UNIQUE,
result_type integer NOT NULL, stars integer NOT NULL CHECK(stars IN(4,5)),
bless integer NOT NULL CHECK(bless IN(0,1)), used_at timestamptz NOT NULL DEFAULT now());");
            }
            enabled = true;
        }
        private static bool Ready(GameState p) => enabled && Q.Player(p) && p.Booth == null && p.Trade != null && !p.Trade.InTrade;
        internal static bool TryHandle(GameState p, byte[] packet)
        {
            ConquerItem item;
            if (p == null || p.Inventory == null || packet == null || packet.Length < 12
                || !p.Inventory.TryGetItem(BitConverter.ToUInt32(packet, 8), out item) || item.ID != ItemId) return false;
            if (packet.Length < 32 || packet.Length > 128 || BitConverter.ToUInt16(packet, 0) + 8 != packet.Length
                || BitConverter.ToUInt16(packet, 2) != 1009 || BitConverter.ToUInt16(packet, 20) != 4
                || BitConverter.ToUInt64(packet, packet.Length - 8) != Revora.Network.PacketHandler.ClientSeal) return true;
            return TryUse(item, p);
        }
        internal static bool TryUse(ConquerItem item, GameState p)
        {
            if (item == null || item.ID != ItemId) return false;
            if (!Ready(p) || !SessionInstall.EnterCurrent(p.Entity.UID, p)) return true;
            try
            {
                lock (p.ItemSyncRoot)
                {
                    ConquerItem current;
                    if (!Ready(p) || !item.Bound || item.StackSize != 1 || item.Position != 0 || item.Warehouse != 0
                        || item.InWardrobe || item.Lock != 0 || item.Suspicious || item.Days != 0 || item.TimeLeftInMinutes != 0
                        || !p.Inventory.TryGetItem(item.UID, out current) || !ReferenceEquals(current, item)) return true;
                    var pool = Pool();
                    if (pool.Length == 0) { p.Send("Your garment reward is not available yet."); return true; }
                    ConquerItem reward;
                    using (var c = DataHolder.MySqlConnection)
                    {
                        c.Open(); using (var t = c.BeginTransaction())
                        {
                            SkillTable.LockOwner(c, t, p);
                            var bag = new List<uint>();
                            using (var q = Q.Command(c, t, @"SELECT uid FROM items WHERE entityid=@uid AND position=0
AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid", "uid", (long)p.Entity.UID))
                            using (var r = q.ExecuteReader()) while (r.Read())
                            {
                                if (bag.Count >= 40) throw new InvalidOperationException("Garment inventory exceeds capacity.");
                                bag.Add(checked((uint)r.GetInt64(0)));
                            }
                            if (!bag.SequenceEqual(p.Inventory.Objects.Select(i => i.UID).OrderBy(i => i)))
                                throw new InvalidOperationException("Garment inventory changed.");
                            if (Q.Execute(c, t, @"DELETE FROM items WHERE uid=@item AND entityid=@uid AND id=@type
AND bound=1 AND stacksize=1 AND position=0 AND warehouse=0 AND locked=0
AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0'",
                                "item", (long)item.UID, "uid", (long)p.Entity.UID, "type", (int)ItemId) != 1)
                                throw new InvalidOperationException("Garment pack ownership changed.");
                            reward = Q.New(pool[Draw(pool.Length)]);
                            reward.Bless = (byte)(Draw(100) < 10 ? 1 : 0);
                            Q.Insert(c, t, p.Entity.UID, reward);
                            if (Q.Execute(c, t, "UPDATE items SET bless=@bless WHERE uid=@item AND entityid=@uid",
                                "bless", (int)reward.Bless, "item", (long)reward.UID, "uid", (long)p.Entity.UID) != 1)
                                throw new InvalidOperationException("Garment blessing was not saved.");
                            if (Q.Execute(c, t, @"INSERT INTO revora_rebirth_garments(pack_uid,uid,result_uid,result_type,stars,bless)
VALUES(@pack,@uid,@result,@type,@stars,@bless)", "pack", (long)item.UID, "uid", (long)p.Entity.UID,
                                "result", (long)reward.UID, "type", (int)reward.ID, "stars", (int)StorageItem.Garments[reward.ID].Stars,
                                "bless", (int)reward.Bless) != 1) throw new InvalidOperationException("Garment receipt was not saved.");
                            BeforeCommit?.Invoke();
                            if (!Ready(p) || !SessionInstall.IsCurrent(p.Entity.UID, p)) throw new InvalidOperationException("Garment session changed.");
                            t.Commit(); AfterCommit?.Invoke();
                        }
                    }
                    Q.Install(p, new[] { item.UID }, new[] { reward });
                    p.Send("Your bound " + StorageItem.Garments[reward.ID].Stars + "-star garment has arrived" + (reward.Bless == 1 ? " with -1 damage blessing." : "."));
                }
            }
            catch (Exception ex) { Q.Fail(p, "rebirth-garment", ex); }
            finally { SessionInstall.Exit(p.Entity.UID); }
            return true;
        }
    }
}
