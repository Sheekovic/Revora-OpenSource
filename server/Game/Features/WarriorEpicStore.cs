using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Runtime callers hold the owner's lock and supply registered world/actor
    // validation. NPC, item and combat routes share these durable transactions.
    internal static class WarriorEpicStore
    {
        internal const uint Jade = 3200206, Fist = 624078, MaxFist = 624439;
        internal enum Stage { None, JadeIssued, MemoryEntered, MemoryWitnessed, SteleBroken, EvilReleased, EvilDefeated, Claimed }
        internal sealed class Progress { internal Stage Stage; internal uint JadeUid; internal bool Valuable; }
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;

        internal static void Initialize()
        {
            foreach (uint id in new[] { Jade, Fist, MaxFist })
                if (!ConquerItemInformation.BaseInformations.ContainsKey(id)) throw new InvalidOperationException("Warrior epic item catalog missing.");
            var max = ConquerItemInformation.BaseInformations[MaxFist];
            if (max.Class != 20 || max.Level != 140 || ConquerItemInformation.BaseInformations.Values.Any(i => i.ID / 1000 == 624 && i.Level > max.Level))
                throw new InvalidOperationException("Warrior maximum reward catalog changed.");
            var fist = ConquerItemInformation.BaseInformations[Fist];
            if (fist.Level != 40) throw new InvalidOperationException("Warrior epic reward level changed.");
            using (var c = DataHolder.MySqlConnection) { c.Open(); Q.Execute(c, null, @"
CREATE TABLE IF NOT EXISTS revora_warrior_epic(
uid bigint PRIMARY KEY,stage integer NOT NULL CHECK(stage BETWEEN 1 AND 7),jade_uid bigint NOT NULL UNIQUE,
created_at timestamptz NOT NULL,updated_at timestamptz NOT NULL);
CREATE TABLE IF NOT EXISTS revora_warrior_epic_steps(
uid bigint NOT NULL REFERENCES revora_warrior_epic(uid),stage integer NOT NULL CHECK(stage BETWEEN 1 AND 7),
encounter uuid,created_at timestamptz NOT NULL,PRIMARY KEY(uid,stage),
CHECK((stage IN(4,6))=(encounter IS NOT NULL)));
CREATE TABLE IF NOT EXISTS revora_warrior_epic_encounters(
id uuid PRIMARY KEY,uid bigint NOT NULL REFERENCES revora_warrior_epic(uid),stage integer NOT NULL CHECK(stage IN(3,5)),
state varchar(8) NOT NULL CHECK(state IN('active','victory','retired')),created_at timestamptz NOT NULL,finished_at timestamptz);
CREATE UNIQUE INDEX IF NOT EXISTS revora_warrior_epic_active ON revora_warrior_epic_encounters(uid) WHERE state='active';
CREATE TABLE IF NOT EXISTS revora_warrior_epic_rewards(
uid bigint NOT NULL REFERENCES revora_warrior_epic(uid),slot integer NOT NULL CHECK(slot IN(0,1)),
item_uid bigint NOT NULL UNIQUE,item_type integer NOT NULL CHECK(item_type=624078),bound boolean NOT NULL CHECK(bound),
PRIMARY KEY(uid,slot));
ALTER TABLE revora_warrior_epic ADD COLUMN IF NOT EXISTS valuable_reward boolean NOT NULL DEFAULT false;
ALTER TABLE revora_warrior_epic_rewards DROP CONSTRAINT IF EXISTS revora_warrior_epic_rewards_item_type_check;
ALTER TABLE revora_warrior_epic_rewards DROP CONSTRAINT IF EXISTS revora_warrior_epic_rewards_bound_check;
ALTER TABLE revora_warrior_epic_rewards DROP CONSTRAINT IF EXISTS revora_warrior_epic_reward_profile;
ALTER TABLE revora_warrior_epic_rewards ADD CONSTRAINT revora_warrior_epic_reward_profile CHECK((item_type=624078 AND bound) OR (item_type=624439 AND NOT bound));"); }
        }
        internal static bool Eligible(GameState p)
        {
            return Q.Player(p) && ReferenceEquals(p.Entity.Owner, p) && CharacterProgression.Managed(p) && p.Entity.Class >= 20 && p.Entity.Class <= 25
                && p.Entity.Level >= 40 && p.Trade != null && !p.Trade.InTrade && p.Booth == null && p.Entity.PokerTable == 0;
        }
        private static void Ready(GameState p, Func<bool> context)
        {
            if (p == null || p.ItemSyncRoot == null || !Monitor.IsEntered(p.ItemSyncRoot) || !Eligible(p)
                || context == null || !context()) throw new InvalidOperationException("Warrior epic owner or world changed.");
        }
        private static Progress Read(NpgsqlConnection c, NpgsqlTransaction t, uint uid)
        {
            using (var q = Q.Command(c, t, "SELECT stage,jade_uid,valuable_reward FROM revora_warrior_epic WHERE uid=@uid FOR UPDATE", "uid", (long)uid))
            using (var r = q.ExecuteReader()) return r.Read()
                ? new Progress { Stage = (Stage)r.GetInt32(0), JadeUid = checked((uint)r.GetInt64(1)), Valuable = r.GetBoolean(2) } : new Progress();
        }
        internal static Progress Load(GameState p)
        {
            if (!Q.Player(p, false)) return null;
            lock (p.ItemSyncRoot) using (var c = DataHolder.MySqlConnection) { c.Open(); return Read(c, null, p.Entity.UID); }
        }
        private static void Bag(NpgsqlConnection c, NpgsqlTransaction t, GameState p)
        {
            var saved = new List<uint>();
            using (var q = Q.Command(c, t, "SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid FOR UPDATE", "uid", (long)p.Entity.UID))
            using (var r = q.ExecuteReader()) while (r.Read()) {
                if (saved.Count >= 40) throw new InvalidOperationException("Warrior epic bag exceeds capacity.");
                saved.Add(Convert.ToUInt32(r[0]));
            }
            if (!saved.SequenceEqual(p.Inventory.Objects.Select(i => i.UID).OrderBy(i => i))) throw new InvalidOperationException("Warrior epic bag requires reload.");
        }
        private static void Owner(NpgsqlConnection c, NpgsqlTransaction t, GameState p, CharacterProgression.Profile profile)
        {
            SkillTable.LockOwner(c, t, p);
            CharacterProgression.Persist(c, t, p, profile);
            using (var q = Q.Command(c, t, "SELECT mapid FROM entities WHERE uid=@uid", "uid", (long)p.Entity.UID))
                if (Convert.ToInt32(q.ExecuteScalar()) != p.Entity.MapID) throw new InvalidOperationException("Warrior epic saved location changed.");
        }
        private static void Step(NpgsqlConnection c, NpgsqlTransaction t, GameState p, Stage from, Stage to, Guid? encounter)
        {
            if (Q.Execute(c, t, "UPDATE revora_warrior_epic SET stage=@next,updated_at=@now WHERE uid=@uid AND stage=@prior",
                "next", (int)to, "prior", (int)from, "uid", (long)p.Entity.UID, "now", Clock()) != 1)
                throw new InvalidOperationException("Warrior epic stage changed.");
            Record(c, t, p.Entity.UID, to, encounter);
        }
        private static void Record(NpgsqlConnection c, NpgsqlTransaction t, uint uid, Stage stage, Guid? encounter)
        {
            if (Q.Execute(c, t, "INSERT INTO revora_warrior_epic_steps(uid,stage,encounter,created_at) VALUES(@uid,@stage,@encounter,@now)",
                "uid", (long)uid, "stage", (int)stage, "encounter", encounter.HasValue ? (object)encounter.Value : DBNull.Value, "now", Clock()) != 1)
                throw new InvalidOperationException("Warrior epic step receipt missing.");
        }
        private static void Insert(NpgsqlConnection c, NpgsqlTransaction t, GameState p, ConquerItem item)
        {
            Q.Insert(c, t, p.Entity.UID, item);
            if (item.ID == MaxFist && Q.Execute(c, t, "UPDATE items SET plus=12,bless=7,socketone=255,sockettwo=255 WHERE uid=@item AND entityid=@uid",
                "item", (long)item.UID, "uid", (long)p.Entity.UID) != 1) throw new InvalidOperationException("Warrior reward enhancements not saved.");
            using (var q = Q.Command(c, t, @"SELECT count(*) FROM items WHERE uid=@item AND id=@type AND entityid=@uid
AND position=0 AND warehouse=0 AND stacksize=1 AND bound=@bound AND durability=@dur AND maximdurability=@dur
AND COALESCE(plus,0)=@plus AND COALESCE(bless,0)=@bless AND COALESCE(socketone,0)=@one AND COALESCE(sockettwo,0)=@two",
                "item", (long)item.UID, "type", (int)item.ID, "uid", (long)p.Entity.UID, "dur", (int)item.Durability, "bound", item.Bound ? 1 : 0, "plus", (int)item.Plus, "bless", (int)item.Bless, "one", (int)item.SocketOne, "two", (int)item.SocketTwo))
                if (Convert.ToInt64(q.ExecuteScalar()) != 1) throw new InvalidOperationException("Warrior epic item receipt missing.");
        }
        internal static bool Begin(GameState p, Func<bool> context)
        {
            Ready(p, context); var profile = CharacterProgression.Capture(p); ConquerItem jade;
            using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                Owner(c, t, p, profile); Bag(c, t, p); var progress = Read(c, t, p.Entity.UID);
                if (progress.Stage != Stage.None) return false;
                if (p.Inventory.Count >= 40) return false;
                jade = Q.New(Jade);
                if (Q.Execute(c, t, "INSERT INTO revora_warrior_epic(uid,stage,jade_uid,created_at,updated_at,valuable_reward) VALUES(@uid,1,@item,@now,@now,true)",
                    "uid", (long)p.Entity.UID, "item", (long)jade.UID, "now", Clock()) != 1) throw new InvalidOperationException("Warrior epic admission missing.");
                Insert(c, t, p, jade); Record(c, t, p.Entity.UID, Stage.JadeIssued, null); Ready(p, context); t.Commit();
            }}
            CharacterProgression.Committed(p, profile); Q.Install(p, new uint[0], new[] { jade }); return true;
        }
        internal static bool RecoverJade(GameState p, Func<bool> context)
        {
            Ready(p, context); var profile = CharacterProgression.Capture(p); ConquerItem jade;
            using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                Owner(c, t, p, profile); Bag(c, t, p); var progress = Read(c, t, p.Entity.UID);
                if (progress.Stage != Stage.JadeIssued || p.Inventory.Count >= 40) return false;
                using (var q = Q.Command(c, t, "SELECT EXISTS(SELECT 1 FROM items WHERE uid=@item)", "item", (long)progress.JadeUid))
                    if (Equals(q.ExecuteScalar(), true)) return false;
                jade = Q.New(Jade); jade.UID = progress.JadeUid; Insert(c, t, p, jade); Ready(p, context); t.Commit();
            }}
            CharacterProgression.Committed(p, profile); Q.Install(p, new uint[0], new[] { jade }); return true;
        }
        internal static bool Advance(GameState p, Stage from, Func<bool> context)
        {
            if (from != Stage.JadeIssued && from != Stage.MemoryEntered && from != Stage.SteleBroken) throw new ArgumentOutOfRangeException("from");
            Ready(p, context); var profile = CharacterProgression.Capture(p); uint removed = 0;
            using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                Owner(c, t, p, profile); var progress = Read(c, t, p.Entity.UID);
                if (progress.Stage != from) return false;
                if (from == Stage.JadeIssued) {
                    Bag(c, t, p); ConquerItem jade;
                    if (!p.Inventory.TryGetItem(progress.JadeUid, out jade) || jade.ID != Jade || !jade.Bound || jade.StackSize != 1
                        || jade.Position != 0 || jade.Warehouse != 0 || jade.Lock != 0 || jade.Suspicious || jade.InWardrobe || jade.Days != 0 || jade.TimeLeftInMinutes != 0)
                        return false;
                    if (Q.Execute(c, t, @"DELETE FROM items WHERE uid=@item AND id=@type AND entityid=@uid AND position=0 AND warehouse=0
AND stacksize=1 AND bound=1 AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0'",
                        "item", (long)jade.UID, "type", (int)Jade, "uid", (long)p.Entity.UID) != 1) throw new InvalidOperationException("Warrior epic jade owner changed.");
                    removed = jade.UID;
                }
                Step(c, t, p, from, from + 1, null); Ready(p, context); t.Commit();
            }}
            CharacterProgression.Committed(p, profile); if (removed != 0) Q.Install(p, new[] { removed }, new ConquerItem[0]); return true;
        }
        internal static Guid OpenEncounter(GameState p, Stage stage, Func<bool> context)
        {
            if (stage != Stage.MemoryWitnessed && stage != Stage.EvilReleased) throw new ArgumentOutOfRangeException("stage");
            Ready(p, context); var profile = CharacterProgression.Capture(p); Guid id = Guid.NewGuid();
            using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                Owner(c, t, p, profile); if (Read(c, t, p.Entity.UID).Stage != stage) return Guid.Empty;
                Q.Execute(c, t, "UPDATE revora_warrior_epic_encounters SET state='retired',finished_at=@now WHERE uid=@uid AND state='active'", "uid", (long)p.Entity.UID, "now", Clock());
                if (Q.Execute(c, t, "INSERT INTO revora_warrior_epic_encounters(id,uid,stage,state,created_at) VALUES(@id,@uid,@stage,'active',@now)",
                    "id", id, "uid", (long)p.Entity.UID, "stage", (int)stage, "now", Clock()) != 1) throw new InvalidOperationException("Warrior epic encounter receipt missing.");
                Ready(p, context); t.Commit();
            }}
            CharacterProgression.Committed(p, profile); return id;
        }
        internal static bool Defeat(GameState p, Stage stage, Guid encounter, Func<bool> liveVictory)
        {
            if (stage != Stage.MemoryWitnessed && stage != Stage.EvilReleased) throw new ArgumentOutOfRangeException("stage");
            Ready(p, liveVictory); if (encounter == Guid.Empty) return false; var profile = CharacterProgression.Capture(p);
            using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                Owner(c, t, p, profile); if (Read(c, t, p.Entity.UID).Stage != stage) return false;
                if (Q.Execute(c, t, "UPDATE revora_warrior_epic_encounters SET state='victory',finished_at=@now WHERE id=@id AND uid=@uid AND stage=@stage AND state='active'",
                    "id", encounter, "uid", (long)p.Entity.UID, "stage", (int)stage, "now", Clock()) != 1) return false;
                Step(c, t, p, stage, stage + 1, encounter); Ready(p, liveVictory); t.Commit();
            }}
            CharacterProgression.Committed(p, profile); return true;
        }
        internal static bool Claim(GameState p, Func<bool> context)
        {
            Ready(p, context); var profile = CharacterProgression.Capture(p); ConquerItem[] rewards;
            using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                Owner(c, t, p, profile); Bag(c, t, p);
                var progress = Read(c, t, p.Entity.UID);
                if (progress.Stage != Stage.EvilDefeated || p.Inventory.Count > 38) return false;
                using (var q = Q.Command(c, t, @"SELECT count(*) FROM revora_warrior_epic_steps s
JOIN revora_warrior_epic_encounters e ON e.id=s.encounter AND e.uid=s.uid AND e.stage+1=s.stage AND e.state='victory'
WHERE s.uid=@uid AND s.stage IN(4,6)", "uid", (long)p.Entity.UID))
                    if (Convert.ToInt32(q.ExecuteScalar()) != 2) throw new InvalidOperationException("Warrior epic combat evidence missing.");
                rewards = new[] { Q.New(progress.Valuable ? MaxFist : Fist, !progress.Valuable), Q.New(progress.Valuable ? MaxFist : Fist, !progress.Valuable) };
                if (progress.Valuable) foreach (var item in rewards) { item.Plus = 12; item.Bless = 7; item.SocketOne = item.SocketTwo = Enums.Gem.EmptySocket; }
                for (int slot = 0; slot < rewards.Length; slot++) {
                    var item = rewards[slot]; Insert(c, t, p, item);
                    if (Q.Execute(c, t, "INSERT INTO revora_warrior_epic_rewards(uid,slot,item_uid,item_type,bound) VALUES(@uid,@slot,@item,@type,@bound)",
                        "uid", (long)p.Entity.UID, "slot", slot, "item", (long)item.UID, "type", (int)item.ID, "bound", item.Bound) != 1) throw new InvalidOperationException("Warrior epic reward receipt missing.");
                }
                Step(c, t, p, Stage.EvilDefeated, Stage.Claimed, null); Ready(p, context); t.Commit();
            }}
            CharacterProgression.Committed(p, profile); Q.Install(p, new uint[0], rewards); return true;
        }
    }
}
