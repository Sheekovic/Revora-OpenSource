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
    // Durable story contract. World integration must supply registered actor/death
    // validation while holding ItemSyncRoot. Initialized through TrojanEpicQuest.
    internal static class TrojanEpicStore
    {
        internal const uint Reward = 614439;
        internal const uint SolarBlade = 3003340;
        internal enum Stage { None, Scraps, Reforge, Arrival, Array, ArrayReport, Ghosts,
            GhostReport, Traitor, Essences, Infuse, BorrowBlade, Devastator, Victory, Claimed }
        internal sealed class Progress
        {
            internal Stage Stage;
            internal int Run, Revision, Pulses, Energy, Completed;
            internal DateTime? LastEnergy;
        }
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;
        internal static bool Eligible(GameState p)
        {
            return Q.Player(p) && ReferenceEquals(p.Entity.Owner, p) && CharacterProgression.Managed(p)
                && p.Entity.Class >= 10 && p.Entity.Class <= 15 && (p.Entity.Level >= 120 || p.Entity.Reborn > 0)
                && p.Trade != null && !p.Trade.InTrade && p.Booth == null && p.Entity.PokerTable == 0;
        }
        internal static void Initialize()
        {
            if (!ConquerItemInformation.BaseInformations.ContainsKey(Reward) || !ConquerItemInformation.BaseInformations.ContainsKey(SolarBlade))
                throw new InvalidOperationException("Trojan item catalog missing.");
            var reward = ConquerItemInformation.BaseInformations[Reward];
            if (reward.Class != 15 || reward.Level != 140
                || ConquerItemInformation.BaseInformations.Values.Any(i => i.ID / 1000 == 614 && i.Level > reward.Level))
                throw new InvalidOperationException("Trojan maximum weapon catalog changed.");
            using (var c = DataHolder.MySqlConnection) { c.Open(); Q.Execute(c, null, @"
CREATE TABLE IF NOT EXISTS revora_trojan_epic(
uid bigint PRIMARY KEY,run integer NOT NULL CHECK(run>0),revision integer NOT NULL CHECK(revision>0),
stage integer NOT NULL CHECK(stage BETWEEN 1 AND 14),pulses integer NOT NULL CHECK(pulses BETWEEN 0 AND 26),
energy integer NOT NULL CHECK(energy BETWEEN 0 AND 90),completed integer NOT NULL CHECK(completed>=0),
last_energy date,updated_at timestamptz NOT NULL,
CHECK(completed=run-1+CASE WHEN stage=14 THEN 1 ELSE 0 END));
CREATE TABLE IF NOT EXISTS revora_trojan_epic_steps(
uid bigint NOT NULL REFERENCES revora_trojan_epic(uid),run integer NOT NULL,revision integer NOT NULL,
stage integer NOT NULL,pulses integer NOT NULL,energy integer NOT NULL,encounter uuid,created_at timestamptz NOT NULL,
PRIMARY KEY(uid,revision));
CREATE TABLE IF NOT EXISTS revora_trojan_epic_energy(
uid bigint NOT NULL REFERENCES revora_trojan_epic(uid),day date NOT NULL,run integer NOT NULL,
ordinal integer NOT NULL CHECK(ordinal BETWEEN 1 AND 90),created_at timestamptz NOT NULL,
PRIMARY KEY(uid,day),UNIQUE(uid,run,ordinal));
CREATE TABLE IF NOT EXISTS revora_trojan_epic_encounters(
id uuid PRIMARY KEY,uid bigint NOT NULL REFERENCES revora_trojan_epic(uid),run integer NOT NULL,revision integer NOT NULL,
stage integer NOT NULL CHECK(stage IN(6,8,12)),state varchar(8) NOT NULL CHECK(state IN('active','victory','retired')),
created_at timestamptz NOT NULL,finished_at timestamptz);
CREATE UNIQUE INDEX IF NOT EXISTS revora_trojan_epic_active ON revora_trojan_epic_encounters(uid) WHERE state='active';
CREATE TABLE IF NOT EXISTS revora_trojan_epic_rewards(
uid bigint NOT NULL REFERENCES revora_trojan_epic(uid),run integer NOT NULL,item_uid bigint NOT NULL UNIQUE,
item_type integer NOT NULL CHECK(item_type=614439),bound boolean NOT NULL CHECK(NOT bound),created_at timestamptz NOT NULL,
PRIMARY KEY(uid,run));
CREATE TABLE IF NOT EXISTS revora_trojan_epic_blades(
uid bigint NOT NULL REFERENCES revora_trojan_epic(uid),run integer NOT NULL,item_uid bigint NOT NULL UNIQUE,
state varchar(8) NOT NULL CHECK(state IN('loaned','returned')),recoveries integer NOT NULL DEFAULT 0 CHECK(recoveries>=0),
created_at timestamptz NOT NULL,recovered_at timestamptz,returned_at timestamptz,PRIMARY KEY(uid,run));"); }
        }
        private static void Ready(GameState p, Func<bool> context)
        {
            if (p == null || p.ItemSyncRoot == null || !Monitor.IsEntered(p.ItemSyncRoot) || !Eligible(p)
                || context == null || !context()) throw new InvalidOperationException("Trojan quest owner or world changed.");
        }
        private static DateTime Now()
        {
            var now = Clock();
            if (now.Kind != DateTimeKind.Utc) throw new InvalidOperationException("Trojan quest requires UTC.");
            return now;
        }
        private static int Limit(Stage stage)
        {
            switch (stage) { case Stage.Scraps: return 27; case Stage.Array: return 23;
                case Stage.Ghosts: return 18; case Stage.Essences: return 25; default: return 0; }
        }
        private static bool Combat(Stage stage) { return stage == Stage.Ghosts || stage == Stage.Traitor || stage == Stage.Devastator; }
        private static bool Paid(Progress s) { return Limit(s.Stage) != 0 && (s.Stage == Stage.Scraps || s.Pulses != 0); }
        // These are Revora's deterministic distributions, not inferred native packets.
        internal static int Objective(Progress s)
        {
            switch (s.Stage) { case Stage.Array: return s.Pulses * 10000 / 23;
                case Stage.Essences: return s.Pulses * 99 / 25; default: return s.Pulses; }
        }
        private static void Validate(Progress s)
        {
            int expected;
            switch (s.Stage) {
                case Stage.Scraps: expected = s.Pulses; break;
                case Stage.Reforge: case Stage.Arrival: expected = 27; break;
                case Stage.Array: expected = 27 + Math.Max(0, s.Pulses - 1); break;
                case Stage.ArrayReport: expected = 49; break;
                case Stage.Ghosts: expected = 49 + Math.Max(0, s.Pulses - 1); break;
                case Stage.GhostReport: case Stage.Traitor: expected = 66; break;
                case Stage.Essences: expected = 66 + Math.Max(0, s.Pulses - 1); break;
                case Stage.Infuse: case Stage.BorrowBlade: case Stage.Devastator: case Stage.Victory: case Stage.Claimed: expected = 90; break;
                default: throw new InvalidOperationException("Invalid Trojan story stage.");
            }
            int limit = Limit(s.Stage);
            if (s.Run < 1 || s.Revision < 1 || s.Completed != s.Run - 1 + (s.Stage == Stage.Claimed ? 1 : 0)
                || s.Energy != expected || s.Pulses < 0 || (limit == 0 ? s.Pulses != 0 : s.Pulses >= limit)
                || s.Energy > 0 && !s.LastEnergy.HasValue)
                throw new InvalidOperationException("Trojan story progress is inconsistent.");
        }
        private static Progress Read(NpgsqlConnection c, NpgsqlTransaction t, uint uid)
        {
            using (var q = Q.Command(c, t, "SELECT run,revision,stage,pulses,energy,completed,last_energy FROM revora_trojan_epic WHERE uid=@uid FOR UPDATE", "uid", (long)uid))
            using (var r = q.ExecuteReader()) {
                if (!r.Read()) return new Progress();
                var s = new Progress { Run = r.GetInt32(0), Revision = r.GetInt32(1), Stage = (Stage)r.GetInt32(2),
                    Pulses = r.GetInt32(3), Energy = r.GetInt32(4), Completed = r.GetInt32(5),
                    LastEnergy = r.IsDBNull(6) ? (DateTime?)null : r.GetDateTime(6).Date };
                Validate(s); return s;
            }
        }
        internal static Progress Load(GameState p)
        {
            if (!Q.Player(p, false)) return null;
            lock (p.ItemSyncRoot) using (var c = DataHolder.MySqlConnection) { c.Open(); return Read(c, null, p.Entity.UID); }
        }
        private static void Owner(NpgsqlConnection c, NpgsqlTransaction t, GameState p, CharacterProgression.Profile profile)
        {
            SkillTable.LockOwner(c, t, p); CharacterProgression.Persist(c, t, p, profile);
        }
        private static void Bag(NpgsqlConnection c, NpgsqlTransaction t, GameState p)
        {
            var bag = new List<uint>();
            using (var q = Q.Command(c, t, @"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0
AND COALESCE(inwardrobe,0)=0 ORDER BY uid FOR UPDATE", "uid", (long)p.Entity.UID))
            using (var r = q.ExecuteReader()) while (r.Read()) {
                if (bag.Count >= 40) throw new InvalidOperationException("Trojan inventory exceeds its bound.");
                bag.Add(checked((uint)r.GetInt64(0)));
            }
            if (!bag.SequenceEqual(p.Inventory.Objects.Select(i => i.UID).OrderBy(id => id)))
                throw new InvalidOperationException("Trojan inventory requires reload.");
        }
        private static uint BladeUid(NpgsqlConnection c, NpgsqlTransaction t, GameState p, Progress s)
        {
            using (var q = Q.Command(c, t, "SELECT item_uid FROM revora_trojan_epic_blades WHERE uid=@uid AND run=@run AND state='loaned' FOR UPDATE",
                "uid", (long)p.Entity.UID, "run", s.Run)) {
                object value = q.ExecuteScalar(); return value == null ? 0 : checked((uint)Convert.ToInt64(value));
            }
        }
        private static bool BladeSaved(NpgsqlConnection c, NpgsqlTransaction t, GameState p, uint uid)
        {
            using (var q = Q.Command(c, t, @"SELECT uid FROM items WHERE uid=@item AND entityid=@uid AND id=3003340
AND position=0 AND warehouse=0 AND stacksize=1 AND bound=1 AND COALESCE(locked,0)=0
AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0' FOR UPDATE",
                "item", (long)uid, "uid", (long)p.Entity.UID)) {
                object value = q.ExecuteScalar(); return value != null && Convert.ToInt64(value) == uid;
            }
        }
        private static uint HeldBlade(NpgsqlConnection c, NpgsqlTransaction t, GameState p, Progress s)
        {
            uint uid = BladeUid(c, t, p, s); ConquerItem item;
            return uid != 0 && p.Inventory.TryGetItem(uid, out item) && item.ID == SolarBlade && item.Bound && item.StackSize == 1
                && item.Position == 0 && item.Warehouse == 0 && item.Lock == 0 && !item.Suspicious && !item.InWardrobe
                && item.Days == 0 && item.TimeLeftInMinutes == 0 && BladeSaved(c, t, p, uid) ? uid : 0;
        }
        internal static bool RecoverBlade(GameState p, int revision, Func<bool> context)
        {
            Ready(p, context); var now = Now(); var profile = CharacterProgression.Capture(p); ConquerItem blade;
            using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                Owner(c, t, p, profile); var s = Read(c, t, p.Entity.UID);
                if (s.Revision != revision || s.Stage != Stage.Devastator && s.Stage != Stage.Victory || p.Inventory.Count >= 40) return false;
                Bag(c, t, p); uint uid = BladeUid(c, t, p, s);
                if (uid == 0) return false;
                using (var q = Q.Command(c, t, "SELECT EXISTS(SELECT 1 FROM items WHERE uid=@item)", "item", (long)uid))
                    if (Equals(q.ExecuteScalar(), true)) return false;
                blade = Q.New(SolarBlade); blade.UID = uid; Q.Insert(c, t, p.Entity.UID, blade);
                if (!BladeSaved(c, t, p, uid)) throw new InvalidOperationException("Recovered Solar Blade was not saved.");
                if (Q.Execute(c, t, @"UPDATE revora_trojan_epic_blades SET recoveries=recoveries+1,recovered_at=@now
WHERE uid=@uid AND run=@run AND item_uid=@item AND state='loaned'", "uid", (long)p.Entity.UID, "run", s.Run,
                    "item", (long)uid, "now", now) != 1) throw new InvalidOperationException("Solar Blade recovery receipt missing.");
                Ready(p, context); t.Commit();
            }}
            CharacterProgression.Committed(p, profile); Q.Install(p, new uint[0], new[] { blade }); return true;
        }
        private static void Record(NpgsqlConnection c, NpgsqlTransaction t, GameState p, Progress s, Guid? encounter, DateTime now)
        {
            if (Q.Execute(c, t, @"INSERT INTO revora_trojan_epic_steps(uid,run,revision,stage,pulses,energy,encounter,created_at)
VALUES(@uid,@run,@rev,@stage,@pulses,@energy,@encounter,@now)", "uid", (long)p.Entity.UID, "run", s.Run, "rev", s.Revision,
                "stage", (int)s.Stage, "pulses", s.Pulses, "energy", s.Energy, "encounter", encounter.HasValue ? (object)encounter.Value : DBNull.Value, "now", now) != 1)
                throw new InvalidOperationException("Trojan story receipt missing.");
        }
        private static void Save(NpgsqlConnection c, NpgsqlTransaction t, GameState p, Progress s, int prior, Guid? encounter, DateTime now)
        {
            Validate(s);
            if (Q.Execute(c, t, @"UPDATE revora_trojan_epic SET run=@run,revision=@rev,stage=@stage,pulses=@pulses,energy=@energy,
completed=@completed,updated_at=@now WHERE uid=@uid AND revision=@prior", "uid", (long)p.Entity.UID, "run", s.Run,
                "rev", s.Revision, "stage", (int)s.Stage, "pulses", s.Pulses, "energy", s.Energy, "completed", s.Completed, "now", now, "prior", prior) != 1)
                throw new InvalidOperationException("Trojan story revision changed.");
            Record(c, t, p, s, encounter, now);
            var saved = Read(c, t, p.Entity.UID);
            if (saved.Run != s.Run || saved.Revision != s.Revision || saved.Stage != s.Stage || saved.Pulses != s.Pulses
                || saved.Energy != s.Energy || saved.Completed != s.Completed || saved.LastEnergy != s.LastEnergy)
                throw new InvalidOperationException("Trojan story write was not persisted.");
        }
        internal static bool Begin(GameState p, int revision, Func<bool> context)
        {
            Ready(p, context); var now = Now(); var profile = CharacterProgression.Capture(p);
            using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                Owner(c, t, p, profile); var s = Read(c, t, p.Entity.UID);
                if (s.Revision != revision || s.Stage != Stage.None && s.Stage != Stage.Claimed) return false;
                s.Run = checked(s.Run + 1); s.Revision = checked(s.Revision + 1); s.Stage = Stage.Scraps; s.Pulses = s.Energy = 0;
                if (revision == 0) {
                    if (Q.Execute(c, t, @"INSERT INTO revora_trojan_epic(uid,run,revision,stage,pulses,energy,completed,updated_at)
VALUES(@uid,1,1,1,0,0,0,@now)", "uid", (long)p.Entity.UID, "now", now) != 1) throw new InvalidOperationException("Trojan admission missing.");
                    Record(c, t, p, s, null, now);
                } else Save(c, t, p, s, revision, null, now);
                Ready(p, context); t.Commit();
            }}
            CharacterProgression.Committed(p, profile); return true;
        }
        private static bool CanAdvance(Progress s, DateTime now)
        {
            return s.Stage >= Stage.Scraps && s.Stage <= Stage.Devastator
                && (!Paid(s) || !s.LastEnergy.HasValue || now.Date > s.LastEnergy.Value);
        }
        internal static Guid OpenEncounter(GameState p, int revision, Func<bool> context)
        {
            Ready(p, context); var now = Now(); var profile = CharacterProgression.Capture(p); var id = Guid.NewGuid();
            using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                Owner(c, t, p, profile); var s = Read(c, t, p.Entity.UID);
                if (s.Revision != revision || !Combat(s.Stage) || !CanAdvance(s, now)) return Guid.Empty;
                if (s.Stage == Stage.Devastator && HeldBlade(c, t, p, s) == 0) return Guid.Empty;
                Q.Execute(c, t, "UPDATE revora_trojan_epic_encounters SET state='retired',finished_at=@now WHERE uid=@uid AND state='active'", "uid", (long)p.Entity.UID, "now", now);
                if (Q.Execute(c, t, @"INSERT INTO revora_trojan_epic_encounters(id,uid,run,revision,stage,state,created_at)
VALUES(@id,@uid,@run,@rev,@stage,'active',@now)", "id", id, "uid", (long)p.Entity.UID, "run", s.Run, "rev", s.Revision,
                    "stage", (int)s.Stage, "now", now) != 1) throw new InvalidOperationException("Trojan encounter missing.");
                Ready(p, context); t.Commit();
            }}
            CharacterProgression.Committed(p, profile); return id;
        }
        internal static void RetireEncounter(GameState owner, Guid id)
        {
            // Registered world cleanup may outlive the connection. It can only
            // retire that owner's exact active encounter, never grant progress.
            if (owner == null || owner.Entity == null || owner.ItemSyncRoot == null || !Monitor.IsEntered(owner.ItemSyncRoot) || id == Guid.Empty)
                throw new InvalidOperationException("Trojan encounter retirement requires its owner lock.");
            using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) { Q.Execute(c, t,
                "UPDATE revora_trojan_epic_encounters SET state='retired',finished_at=@now WHERE id=@id AND uid=@uid AND state='active'",
                "id", id, "uid", (long)owner.Entity.UID, "now", Now());
                using (var q = Q.Command(c, t, "SELECT state FROM revora_trojan_epic_encounters WHERE id=@id AND uid=@uid",
                    "id", id, "uid", (long)owner.Entity.UID))
                    if (Equals(q.ExecuteScalar(), "active")) throw new InvalidOperationException("Trojan encounter retirement was not saved.");
                t.Commit();
            }}
        }
        internal static bool Claim(GameState p, int revision, Func<bool> context)
        {
            Ready(p, context); var now = Now(); var profile = CharacterProgression.Capture(p); ConquerItem item; uint blade;
            using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                Owner(c, t, p, profile); var s = Read(c, t, p.Entity.UID);
                if (s.Stage != Stage.Victory || s.Revision != revision) return false;
                Bag(c, t, p); blade = HeldBlade(c, t, p, s); if (blade == 0) return false;
                using (var q = Q.Command(c, t, "SELECT count(*) FROM revora_trojan_epic_energy WHERE uid=@uid AND run=@run", "uid", (long)p.Entity.UID, "run", s.Run))
                    if (Convert.ToInt32(q.ExecuteScalar()) != 90) throw new InvalidOperationException("Trojan energy proof missing.");
                foreach (int stage in new[] { 6, 8, 12 }) {
                    using (var q = Q.Command(c, t, @"SELECT count(DISTINCT e.id) FROM revora_trojan_epic_encounters e
JOIN revora_trojan_epic_steps s ON s.encounter=e.id AND s.uid=e.uid AND s.run=e.run AND s.revision=e.revision+1
WHERE e.uid=@uid AND e.run=@run AND e.stage=@stage AND e.state='victory'", "uid", (long)p.Entity.UID, "run", s.Run, "stage", stage))
                        if (Convert.ToInt32(q.ExecuteScalar()) != (stage == 6 ? 18 : 1)) throw new InvalidOperationException("Trojan combat proof missing.");
                }
                // Owner-defined reward: a new maximum enhanced saber. Existing
                // equipped weapons and their purification are never overwritten.
                Q.Consume(c, t, p, blade, SolarBlade);
                if (Q.Execute(c, t, "UPDATE revora_trojan_epic_blades SET state='returned',returned_at=@now WHERE uid=@uid AND run=@run AND state='loaned'",
                    "uid", (long)p.Entity.UID, "run", s.Run, "now", now) != 1) throw new InvalidOperationException("Solar Blade return receipt missing.");
                if (BladeUid(c, t, p, s) != 0) throw new InvalidOperationException("Solar Blade return was not persisted.");
                item = Q.New(Reward, false); item.Plus = 12; item.Bless = 7;
                item.SocketOne = item.SocketTwo = Enums.Gem.EmptySocket;
                Q.Insert(c, t, p.Entity.UID, item);
                if (Q.Execute(c, t, "UPDATE items SET plus=12,bless=7,socketone=255,sockettwo=255 WHERE uid=@item AND entityid=@uid",
                    "item", (long)item.UID, "uid", (long)p.Entity.UID) != 1) throw new InvalidOperationException("Trojan reward enhancements missing.");
                using (var q = Q.Command(c, t, @"SELECT count(*) FROM items WHERE uid=@item AND entityid=@uid AND id=614439
AND position=0 AND warehouse=0 AND stacksize=1 AND bound=0 AND plus=12 AND bless=7 AND socketone=255 AND sockettwo=255
AND durability=@dur AND maximdurability=@dur", "item", (long)item.UID, "uid", (long)p.Entity.UID, "dur", (int)item.Durability))
                    if (Convert.ToInt32(q.ExecuteScalar()) != 1) throw new InvalidOperationException("Trojan reward item was not persisted.");
                if (Q.Execute(c, t, @"INSERT INTO revora_trojan_epic_rewards(uid,run,item_uid,item_type,bound,created_at)
VALUES(@uid,@run,@item,614439,false,@now)", "uid", (long)p.Entity.UID, "run", s.Run, "item", (long)item.UID, "now", now) != 1)
                    throw new InvalidOperationException("Trojan reward receipt missing.");
                s.Stage = Stage.Claimed; s.Completed++; s.Revision = checked(s.Revision + 1);
                Save(c, t, p, s, revision, null, now); Ready(p, context); t.Commit();
            }}
            CharacterProgression.Committed(p, profile); Q.Install(p, new[] { blade }, new[] { item }); return true;
        }
        internal static bool Advance(GameState p, int revision, Guid? encounter, Func<bool> context)
        {
            return AdvanceCore(p, revision, encounter, context, null, null);
        }
        internal static bool Defeat(GameState p, int revision, Guid encounter, Entity body, Entity killer, Func<bool> context)
        {
            if (body == null || killer == null) return false;
            return AdvanceCore(p, revision, encounter, context, body, killer);
        }
        private static bool AdvanceCore(GameState p, int revision, Guid? encounter, Func<bool> context, Entity body, Entity killer)
        {
            Ready(p, context); var now = Now(); var profile = CharacterProgression.Capture(p); ConquerItem blade = null;
            using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                Owner(c, t, p, profile); var s = Read(c, t, p.Entity.UID);
                if (s.Revision != revision || !CanAdvance(s, now) || Combat(s.Stage) != encounter.HasValue) return false;
                if (s.Stage == Stage.Devastator && HeldBlade(c, t, p, s) == 0) return false;
                if (s.Stage == Stage.BorrowBlade) {
                    if (p.Inventory.Count >= 40) return false;
                    Bag(c, t, p); blade = Q.New(SolarBlade); Q.Insert(c, t, p.Entity.UID, blade);
                    if (!BladeSaved(c, t, p, blade.UID)) throw new InvalidOperationException("Borrowed Solar Blade was not saved.");
                    if (Q.Execute(c, t, @"INSERT INTO revora_trojan_epic_blades(uid,run,item_uid,state,created_at)
VALUES(@uid,@run,@item,'loaned',@now)", "uid", (long)p.Entity.UID, "run", s.Run, "item", (long)blade.UID, "now", now) != 1)
                        throw new InvalidOperationException("Solar Blade loan receipt missing.");
                    if (BladeUid(c, t, p, s) != blade.UID) throw new InvalidOperationException("Solar Blade loan identity changed.");
                }
                if (encounter.HasValue && Q.Execute(c, t, @"UPDATE revora_trojan_epic_encounters SET state='victory',finished_at=@now
WHERE id=@id AND uid=@uid AND run=@run AND revision=@rev AND stage=@stage AND state='active'",
                    "id", encounter.Value, "uid", (long)p.Entity.UID, "run", s.Run, "rev", s.Revision, "stage", (int)s.Stage, "now", now) != 1) return false;
                if (Paid(s)) {
                    s.Energy++; s.LastEnergy = now.Date;
                    if (Q.Execute(c, t, @"INSERT INTO revora_trojan_epic_energy(uid,day,run,ordinal,created_at)
VALUES(@uid,CAST(@now AT TIME ZONE 'UTC' AS date),@run,@ordinal,@now)", "uid", (long)p.Entity.UID, "run", s.Run, "ordinal", s.Energy, "now", now) != 1)
                        throw new InvalidOperationException("Trojan daily energy receipt missing.");
                    if (Q.Execute(c, t, "UPDATE revora_trojan_epic SET last_energy=CAST(@now AT TIME ZONE 'UTC' AS date) WHERE uid=@uid AND revision=@rev",
                        "uid", (long)p.Entity.UID, "rev", revision, "now", now) != 1) throw new InvalidOperationException("Trojan daily energy changed.");
                }
                int limit = Limit(s.Stage);
                if (limit == 0 || ++s.Pulses == limit) { s.Stage++; s.Pulses = 0; }
                s.Revision = checked(s.Revision + 1); Save(c, t, p, s, revision, encounter, now);
                if (body != null && encounter.HasValue)
                    EquipmentAchievements.RecordUnarmedVictory(c, t, p, body, killer, "trojan:" + encounter.Value.ToString("N"));
                Ready(p, context); t.Commit();
            }}
            CharacterProgression.Committed(p, profile);
            if (blade != null) Q.Install(p, new uint[0], new[] { blade });
            return true;
        }
    }
}
