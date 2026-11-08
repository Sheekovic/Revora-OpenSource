using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class MiningRewards
    {
        private sealed class Session {
            internal Map Map; internal ushort X, Y; internal ConquerItem Tool; internal uint Uid, Type;
            internal byte Slot; internal DateTime Next;
        }
        private static readonly ConditionalWeakTable<GameState, Session> sessions = new ConditionalWeakTable<GameState, Session>();
        private static readonly uint[] plain = { 700001, 700011, 700021 };
        private static readonly uint[] forest = { 700001, 700011, 700031, 700041 };
        private static readonly uint[] canyon = { 700051, 700061 };
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;
        internal static Func<int, int> Roll = max => Kernel.Random.Next(max);
        private static uint[] Gems(ushort map) {
            switch (map) {
                case 1028: return plain; case 1025: case 1218: case 6000: case 6001: return forest;
                case 1026: case 1027: return canyon; default: return null;
            }
        }
        internal static void Initialize() {
            foreach (uint id in plain.Concat(forest).Concat(canyon).Concat(new uint[] { 562000, 562001 }).Concat(Enumerable.Range(1072020,10).Select(i=>(uint)i)))
                if (!ConquerItemInformation.BaseInformations.ContainsKey(id)) throw new InvalidOperationException("Mining catalog is incomplete.");
            using (var c = DataHolder.MySqlConnection) { c.Open(); Q.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_mining_state(
uid bigint PRIMARY KEY CHECK(uid>0),next_at timestamptz NOT NULL,yields bigint NOT NULL CHECK(yields>0),
last_item bigint NOT NULL CHECK(last_item>0),last_type integer NOT NULL CHECK(last_type>0),map integer NOT NULL
CHECK(map IN(1025,1026,1027,1028,1218,6000,6001)));"); }
        }
        private static bool Ready(GameState p, out ConquerItem tool) {
            tool = null;
            if (!Q.Player(p) || p.Map == null || p.Map.ID != p.Entity.MapID || p.Map.BaseID != (p.Map.ID == 1218 ? 1025 : p.Map.ID) || Gems(p.Map.ID) == null
                || p.Booth != null || p.Trade == null || p.Trade.InTrade || p.Equipment == null || p.Entity.PokerTable != 0) return false;
            tool = p.Equipment.TryGetItem(p.AlternateEquipment ? (byte)24 : (byte)4); ConquerItemBaseInformation info;
            return tool != null && tool.UID != 0 && (tool.ID == 562000 || tool.ID == 562001)
                && tool.Position == (p.AlternateEquipment ? 24 : 4) && tool.Warehouse == 0 && tool.StackSize <= 1 && tool.Durability > 0
                && !tool.Suspicious && !tool.InWardrobe && tool.Days == 0 && tool.TimeLeftInMinutes == 0
                && ConquerItemInformation.BaseInformations.TryGetValue(tool.ID, out info) && p.Entity.Level >= info.Level;
        }
        private static bool Same(GameState p, Session session) {
            ConquerItem tool;
            return Ready(p, out tool) && ReferenceEquals(p.Map, session.Map) && p.Entity.X == session.X && p.Entity.Y == session.Y
                && ReferenceEquals(tool, session.Tool) && tool.UID == session.Uid && tool.ID == session.Type && tool.Position == session.Slot;
        }
        internal static void Begin(GameState p, Data request) {
            if (p == null || p.ItemSyncRoot == null || request == null || request.ID != Data.SwingPickaxe) return;
            lock (p.ItemSyncRoot) {
                ConquerItem tool;
                if (!Ready(p, out tool) || (request.UID != 0 && request.UID != p.Entity.UID)) { p.Mining = false; sessions.Remove(p); return; }
                Session existing;
                if (p.Mining && sessions.TryGetValue(p, out existing) && Same(p, existing)) return;
                sessions.Remove(p); sessions.Add(p, new Session { Map = p.Map, X = p.Entity.X, Y = p.Entity.Y,
                    Tool = tool, Uid = tool.UID, Type = tool.ID, Slot = tool.Position, Next = Clock().AddSeconds(2) });
                p.Mining = true; p.MiningStamp = Time32.Now;
            }
        }
        // Restore copper to the otherwise missing first ore slot in Maple Forest.
        // The inherited 40%/30% selection is Revora policy, not verified retail odds.
        private static uint ExistingLoot(ushort terrain) {
            if (Roll(100) >= 40) return 0;
            foreach (var row in new uint[][] { new uint[] { terrain==1025?1072020u:31072050u, 30 }, new uint[] { 1072031, 20 }, new uint[] { 1072050, 30 }, new uint[] { 1072040, 30 } }) {
                if (Roll(100) >= row[1]) continue; uint id = row[0] + (uint)Roll(row[0]==1072020?10:9);
                if (ConquerItemInformation.BaseInformations.ContainsKey(id)) return id;
            }
            uint fourth = 3004244;
            foreach (uint type in new uint[] { 3009001, 3004245, 3004243, 3004244 }) {
                if (Roll(100) >= 5) continue;
                // The old implementation adjusts the fourth candidate even in earlier branches.
                if (Roll(250) == 0) fourth++; else if (Roll(100) == 0) fourth += 2;
                uint id = type == 3004244 ? fourth : type;
                if (ConquerItemInformation.BaseInformations.ContainsKey(id)) return id;
            }
            return 0;
        }
        private static void Yield(GameState p, Session session, uint type, DateTime now) {
            var item = Q.New(type, false); item.Color = (Enums.Color)(4 + Roll(4));
            if (type < 700000 || type > 700100) item.Durability = item.MaximDurability = 1;
            using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                SkillTable.LockOwner(c, t, p);
                using (var q = Q.Command(c, t, "SELECT mapid FROM entities WHERE uid=@uid", "uid", (long)p.Entity.UID))
                    if (Convert.ToUInt32(q.ExecuteScalar()) != p.Map.ID) throw new InvalidOperationException("Mining owner location changed.");
                using (var q = Q.Command(c, t, "SELECT next_at FROM revora_mining_state WHERE uid=@uid FOR UPDATE", "uid", (long)p.Entity.UID)) {
                    object next = q.ExecuteScalar(); if (next != null && Convert.ToDateTime(next).ToUniversalTime() > now) return;
                }
                using (var q = Q.Command(c, t, @"SELECT uid,id,warehouse,stacksize,durability,COALESCE(suspicious,0),
COALESCE(inwardrobe,0),COALESCE(NULLIF(days,''),'0') FROM items WHERE entityid=@uid AND position=@slot FOR UPDATE",
                    "uid", (long)p.Entity.UID, "slot", (int)session.Slot))
                using (var r = q.ExecuteReader()) {
                    if (!r.Read() || Convert.ToUInt32(r[0]) != session.Uid || Convert.ToUInt32(r[1]) != session.Type
                        || Convert.ToInt32(r[2]) != 0 || Convert.ToInt32(r[3]) < 0 || Convert.ToInt32(r[3]) > 1
                        || Convert.ToInt32(r[4]) <= 0 || Convert.ToInt32(r[5]) != 0 || Convert.ToInt32(r[6]) != 0 || r.GetString(7) != "0" || r.Read())
                        throw new InvalidOperationException("Mining tool ownership changed.");
                }
                using (var q = Q.Command(c, t, "SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0", "uid", (long)p.Entity.UID))
                    if (Convert.ToInt64(q.ExecuteScalar()) != p.Inventory.Count) throw new InvalidOperationException("Mining inventory changed.");
                if (Q.Execute(c, t, @"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability,color)
VALUES(@item,@type,@uid,0,0,1,0,@dur,@max,@color)", "item", (long)item.UID, "type", (int)item.ID, "uid", (long)p.Entity.UID,
                    "dur", (int)item.Durability, "max", (int)item.MaximDurability, "color", (int)item.Color) != 1)
                    throw new InvalidOperationException("Mining yield was not saved.");
                if (Q.Execute(c, t, @"INSERT INTO revora_mining_state(uid,next_at,yields,last_item,last_type,map)
VALUES(@uid,@next,1,@item,@type,@map) ON CONFLICT(uid) DO UPDATE SET next_at=EXCLUDED.next_at,
yields=revora_mining_state.yields+1,last_item=EXCLUDED.last_item,last_type=EXCLUDED.last_type,map=EXCLUDED.map",
                    "uid", (long)p.Entity.UID, "next", now.AddSeconds(2), "item", (long)item.UID, "type", (int)item.ID, "map", (int)p.Map.ID) != 1)
                    throw new InvalidOperationException("Mining state was not saved.");
                if (!p.Mining || !Same(p, session) || p.Inventory.Count >= 40) throw new InvalidOperationException("Mining session changed.");
                t.Commit();
            }}
            Q.Install(p, new uint[0], new[] { item }); p.Send("You mined " + ConquerItemInformation.BaseInformations[type].Name + ".");
        }
        internal static void Tick(GameState p) {
            if (p == null || p.ItemSyncRoot == null) return;
            lock (p.ItemSyncRoot) {
                Session session;
                if (!p.Mining || !sessions.TryGetValue(p, out session) || !Same(p, session)) { p.Mining = false; sessions.Remove(p); return; }
                if (p.Inventory.Count >= 40) { p.Mining = false; sessions.Remove(p); p.Send(Constants.FullInventory); return; }
                DateTime now = Clock(); if (now < session.Next) return;
                session.Next = now.AddSeconds(2); p.MiningStamp = Time32.Now;
                try {
                    p.SendScreen(new Data(true) { ID = Data.SwingPickaxe, UID = p.Entity.UID }, true);
                    int chance = Math.Max(0, Math.Min(10000, Config.MiningNormalGemBasisPoints)); uint type;
                    if (chance > 0 && Roll(10000) < chance) { var choices = Gems(p.Map.ID); type = choices[Roll(choices.Length)]; }
                    else type = ExistingLoot(p.Map.BaseID);
                    if (type != 0) Yield(p, session, type, now);
                } catch (Exception ex) { p.Mining = false; sessions.Remove(p); Q.Fail(p, "mining-yield", ex); }
            }
        }
    }
}
