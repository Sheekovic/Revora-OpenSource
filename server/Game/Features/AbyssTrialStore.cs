using System;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;
using R = Revora.Game.Features.AbyssTrialRules;
using M = Revora.Game.Features.AbyssTrialMaterials;

namespace Revora.Game.Features
{
    // Durable participants, not packet handlers. The world service must validate
    // the actual NPC/death and commit these writes with any completion evidence.
    // No legacy Cup quest, counter or achievement bit is imported here.
    internal static class AbyssTrialStore
    {
        internal sealed class Run {
            internal Guid Id;
            internal uint Owner;
            internal int FirstClass, FirstLevel, Version;
            internal bool Selected, Bound, Consumed, Introduced;
            internal R.Progress Progress;
        }
        internal sealed class Encounter {
            internal Guid Id, Run;
            internal uint Family;
            internal bool Bound, Created;
        }
        internal static void Initialize() {
            using (var c = DataHolder.MySqlConnection) { c.Open(); Q.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_abyss_trials(
id uuid PRIMARY KEY,uid bigint NOT NULL UNIQUE,first_class integer NOT NULL,first_level integer NOT NULL,
stage integer NOT NULL DEFAULT 1,meters integer NOT NULL DEFAULT 0,spirits integer NOT NULL DEFAULT 0,forms integer NOT NULL DEFAULT 0,
version integer NOT NULL DEFAULT 0 CHECK(version>=0),selected boolean NOT NULL DEFAULT true,bound boolean NOT NULL DEFAULT false,
created_at timestamptz NOT NULL DEFAULT now(),consumed_at timestamptz,
CHECK(first_class BETWEEN 1 AND 255 AND first_level BETWEEN 1 AND 255),
CHECK(meters BETWEEN 0 AND 40000 AND spirits BETWEEN 0 AND 72 AND forms BETWEEN 0 AND 3),
CHECK((stage=1 AND meters=0 AND spirits=0 AND forms=0) OR (stage=2 AND spirits=0 AND forms=0)
OR (stage=3 AND meters=40000 AND forms=0) OR (stage=4 AND meters=40000 AND spirits=72 AND forms<3)
OR (stage=5 AND meters=40000 AND spirits=72 AND forms=3)),CHECK(consumed_at IS NULL OR stage=5));
CREATE TABLE IF NOT EXISTS revora_abyss_operations(
id uuid PRIMARY KEY,run uuid NOT NULL REFERENCES revora_abyss_trials(id),version integer NOT NULL,
kind varchar(10) NOT NULL CHECK(kind IN('exchange','summon','turnin')),product integer NOT NULL,
created_at timestamptz NOT NULL DEFAULT now(),UNIQUE(run,version));
CREATE TABLE IF NOT EXISTS revora_abyss_materials(
operation uuid NOT NULL REFERENCES revora_abyss_operations(id),item_uid bigint NOT NULL,item_type integer NOT NULL CHECK(item_type BETWEEN 722721 AND 722731),
before_stack integer NOT NULL CHECK(before_stack BETWEEN 0 AND 65535),consumed integer NOT NULL CHECK(consumed>0 AND consumed<=GREATEST(1,before_stack)),
bound boolean NOT NULL,PRIMARY KEY(operation,item_uid));
CREATE TABLE IF NOT EXISTS revora_abyss_encounters(
id uuid PRIMARY KEY REFERENCES revora_abyss_operations(id),run uuid NOT NULL REFERENCES revora_abyss_trials(id),
family integer NOT NULL CHECK(family BETWEEN 3632 AND 3646),bound boolean NOT NULL,defeated_at timestamptz);
CREATE UNIQUE INDEX IF NOT EXISTS revora_abyss_active_encounter ON revora_abyss_encounters(run) WHERE defeated_at IS NULL;
CREATE TABLE IF NOT EXISTS revora_abyss_deaths(
run uuid NOT NULL REFERENCES revora_abyss_trials(id),life uuid PRIMARY KEY,family integer NOT NULL CHECK(family BETWEEN 3600 AND 3631));
CREATE TABLE IF NOT EXISTS revora_abyss_rewards(
id uuid PRIMARY KEY,run uuid NOT NULL REFERENCES revora_abyss_trials(id),item_type integer NOT NULL CHECK(item_type BETWEEN 722721 AND 722731),
bound boolean NOT NULL,created_at timestamptz NOT NULL DEFAULT now(),claimed_item bigint UNIQUE,claimed_at timestamptz,
CHECK((claimed_at IS NULL)=(claimed_item IS NULL)));
CREATE INDEX IF NOT EXISTS revora_abyss_unclaimed ON revora_abyss_rewards(run,created_at) WHERE claimed_at IS NULL;
ALTER TABLE revora_abyss_trials ADD COLUMN IF NOT EXISTS introduced boolean NOT NULL DEFAULT false;"); }
        }
        internal static Run Read(NpgsqlConnection c, NpgsqlTransaction t, uint uid, bool locked = false) {
            using (var q = Q.Command(c, t, @"SELECT id,first_class,first_level,stage,meters,spirits,forms,version,selected,bound,consumed_at IS NOT NULL,introduced
FROM revora_abyss_trials WHERE uid=@uid" + (locked ? " FOR UPDATE" : ""), "uid", (long)uid))
            using (var r = q.ExecuteReader()) return !r.Read() ? null : new Run {
                Id = r.GetGuid(0), Owner = uid, FirstClass = r.GetInt32(1), FirstLevel = r.GetInt32(2),
                Progress = new R.Progress((R.Stage)r.GetInt32(3), r.GetInt32(4), r.GetInt32(5), r.GetInt32(6)),
                Version = r.GetInt32(7), Selected = r.GetBoolean(8), Bound = r.GetBoolean(9), Consumed = r.GetBoolean(10), Introduced = r.GetBoolean(11)
            };
        }
        private static void Owner(NpgsqlConnection c, NpgsqlTransaction t, GameState p) {
            if (!Q.Player(p) || !Monitor.IsEntered(p.ItemSyncRoot) || p.Trade == null || p.Trade.InTrade || p.Booth != null
                || !R.Eligible(p.Entity.Reborn, p.Entity.Level) || p.Map == null || p.Map.ID != p.Entity.MapID || p.Map.BaseID != p.Map.ID)
                throw new InvalidOperationException("Abyss owner is not ready.");
            SkillTable.LockOwner(c, t, p);
            using (var q = Q.Command(c, t, "SELECT reborn,level,firstrebornclass,firstrebornlevel,mapid FROM entities WHERE uid=@uid", "uid", (long)p.Entity.UID))
            using (var r = q.ExecuteReader()) if (!r.Read() || Convert.ToInt32(r[0]) != p.Entity.Reborn || Convert.ToInt32(r[1]) != p.Entity.Level
                || Convert.ToInt32(r[2]) != p.Entity.FirstRebornClass || Convert.ToInt32(r[3]) != p.Entity.FirstRebornLevel || Convert.ToInt32(r[4]) != p.Entity.MapID)
                throw new InvalidOperationException("Abyss owner requires reload.");
        }
        internal static Run Require(NpgsqlConnection c, NpgsqlTransaction t, GameState p) {
            Owner(c, t, p); var r = Read(c, t, p.Entity.UID, true);
            if (r == null || r.Consumed || !r.Selected || r.FirstClass != p.Entity.FirstRebornClass || r.FirstLevel != p.Entity.FirstRebornLevel)
                throw new InvalidOperationException("Abyss run is not available.");
            return r;
        }
        internal static Run Enroll(NpgsqlConnection c, NpgsqlTransaction t, GameState p) {
            Owner(c, t, p);
            if (!Q.Near(p, HeavenlyStoneCrafting.Sereth, 1002) || p.Map.Npcs[HeavenlyStoneCrafting.Sereth].MapID != 1002)
                throw new InvalidOperationException("Abyss guide is not nearby.");
            var r = Read(c, t, p.Entity.UID, true);
            if (r == null) {
                if (Q.Execute(c, t, "INSERT INTO revora_abyss_trials(id,uid,first_class,first_level) VALUES(@id,@uid,@class,@level)",
                    "id", Guid.NewGuid(), "uid", (long)p.Entity.UID, "class", (int)p.Entity.FirstRebornClass, "level", (int)p.Entity.FirstRebornLevel) != 1)
                    throw new InvalidOperationException("Abyss admission was not saved.");
            } else {
                if (r.Consumed || r.FirstClass != p.Entity.FirstRebornClass || r.FirstLevel != p.Entity.FirstRebornLevel)
                    throw new InvalidOperationException("Abyss admission history changed.");
                if (!r.Selected && Q.Execute(c, t, "UPDATE revora_abyss_trials SET selected=true,version=version+1 WHERE id=@id AND version=@version AND NOT selected",
                    "id", r.Id, "version", r.Version) != 1) throw new InvalidOperationException("Abyss route was not saved.");
            }
            return Require(c, t, p);
        }
        internal static void SelectCup(NpgsqlConnection c, NpgsqlTransaction t, GameState p) {
            var r = Require(c, t, p);
            if (!Q.Near(p, HeavenlyStoneCrafting.Sereth, 1002) || p.Map.Npcs[HeavenlyStoneCrafting.Sereth].MapID != 1002)
                throw new InvalidOperationException("Abyss guide is not nearby.");
            if (Active(c, t, r) != null) throw new InvalidOperationException("Finish the outstanding Abyss encounter before changing route.");
            if (Q.Execute(c, t, "UPDATE revora_abyss_trials SET selected=false,version=version+1 WHERE id=@id AND version=@version AND selected",
                "id", r.Id, "version", r.Version) != 1) throw new InvalidOperationException("Abyss route was not saved.");
        }
        private static void Version(Run r, int version) {
            if (r.Version != version) throw new InvalidOperationException("Abyss offer changed.");
        }
        private static void Save(NpgsqlConnection c, NpgsqlTransaction t, Run r, R.Progress next, bool bound) {
            if (Q.Execute(c, t, @"UPDATE revora_abyss_trials SET introduced=CASE WHEN stage=@stage THEN introduced ELSE false END,stage=@stage,meters=@meters,spirits=@spirits,forms=@forms,bound=bound OR @bound,version=version+1
WHERE id=@id AND uid=@uid AND version=@version AND consumed_at IS NULL AND selected", "stage", (int)next.Step,
                "meters", next.Meters, "spirits", next.Spirits, "forms", next.SatanForms, "bound", bound, "id", r.Id, "uid", (long)r.Owner, "version", r.Version) != 1)
                throw new InvalidOperationException("Abyss progress was not saved.");
        }
        private static Guid Operation(NpgsqlConnection c, NpgsqlTransaction t, Run r, string kind, uint product) {
            var id = Guid.NewGuid();
            if (Q.Execute(c, t, "INSERT INTO revora_abyss_operations(id,run,version,kind,product) VALUES(@id,@run,@version,@kind,@product)",
                "id", id, "run", r.Id, "version", r.Version, "kind", kind, "product", checked((int)product)) != 1)
                throw new InvalidOperationException("Abyss operation was not saved.");
            return id;
        }
        private static void Site(GameState p, uint npc) {
            if (!Q.Near(p, npc, R.Map) || p.Map.Npcs[npc].MapID != R.Map) throw new InvalidOperationException("Abyss keeper is not nearby.");
        }
        internal static ConquerItem Exchange(NpgsqlConnection c, NpgsqlTransaction t, GameState p, int version, uint output, M.Plan plan) {
            var costs = R.ExchangeCost(output); var r = Require(c, t, p); Version(r, version); Site(p, 3601);
            if (r.Progress.Step != R.Stage.Vigor || plan == null) throw new InvalidOperationException("Abyss exchange is not available.");
            M.Bag(c, t, p);
            if (p.Inventory.Count - plan.Freed + 1 > 40) return null;
            var id = Operation(c, t, r, "exchange", output); M.Debit(c, t, p, id, plan, costs);
            var item = M.Insert(c, t, p, output, plan.Bound); Save(c, t, r, r.Progress, false); return item;
        }
        internal static bool TurnIn(NpgsqlConnection c, NpgsqlTransaction t, GameState p, int version, uint npc, M.Plan plan) {
            var r = Require(c, t, p); Version(r, version); Site(p, npc);
            if (Active(c, t, r) != null || (r.Progress.Step == R.Stage.Grievance || r.Progress.Step == R.Stage.Spirits) && !r.Introduced) return false;
            var next = R.TurnIn(r.Progress, npc, plan == null ? 0 : R.RequiredVigor);
            if (ReferenceEquals(next, r.Progress)) return false;
            var id = Operation(c, t, r, "turnin", npc);
            if (r.Progress.Step == R.Stage.Vigor) M.Debit(c, t, p, id, plan, new[] { new R.Material(722731, R.RequiredVigor) });
            else if (plan != null) throw new InvalidOperationException("Unexpected Abyss stage payment.");
            Save(c, t, r, next, plan != null && plan.Bound); return true;
        }
        internal static bool Introduce(NpgsqlConnection c, NpgsqlTransaction t, GameState p, int version) {
            var r = Require(c, t, p); Version(r, version);
            uint npc = r.Progress.Step == R.Stage.Grievance ? 3602u : r.Progress.Step == R.Stage.Spirits ? 3603u : 0;
            if (npc == 0 || r.Introduced) return false; Site(p, npc);
            if (Q.Execute(c, t, "UPDATE revora_abyss_trials SET introduced=true,version=version+1 WHERE id=@id AND version=@version AND NOT introduced",
                "id", r.Id, "version", r.Version) != 1) throw new InvalidOperationException("Abyss introduction was not saved.");
            return true;
        }
        internal static Encounter Active(NpgsqlConnection c, NpgsqlTransaction t, Run r) {
            using (var q = Q.Command(c, t, "SELECT id,family,bound FROM revora_abyss_encounters WHERE run=@run AND defeated_at IS NULL", "run", r.Id))
            using (var row = q.ExecuteReader()) return !row.Read() ? null : new Encounter { Id = row.GetGuid(0), Run = r.Id, Family = checked((uint)row.GetInt32(1)), Bound = row.GetBoolean(2) };
        }
        internal static Encounter Summon(NpgsqlConnection c, NpgsqlTransaction t, GameState p, int version, uint family, M.Plan plan) {
            var r = Require(c, t, p); uint npc = family == 3632 ? 3605u : family == 3633 ? 3606u : family == 3634 ? 3607u : family == 3635 ? 3609u
                : family >= 3644 && family <= 3646 ? 3608u : SpiritNpc(family, p.ActiveNpc);
            Site(p, npc);
            var active = Active(c, t, r);
            if (active != null) {
                if (active.Family != family) throw new InvalidOperationException("Another Abyss encounter is still owed.");
                return active; // Resuming never spends the old confirmation's material plan.
            }
            Version(r, version);
            bool material = r.Progress.Step == R.Stage.Vigor && family >= 3632 && family <= 3635;
            bool spirit = r.Introduced && family != 0 && family == R.NextSpirit(r.Progress);
            bool satan = r.Progress.Step == R.Stage.Satan && family == 3644u + r.Progress.SatanForms;
            if (!material && !spirit && !satan) throw new InvalidOperationException("Abyss encounter is out of order.");
            bool costs = material || satan && family == 3644;
            if (costs && plan == null || !costs && plan != null) throw new InvalidOperationException("Abyss summon payment changed.");
            Guid id = Operation(c, t, r, "summon", family);
            if (costs) M.Debit(c, t, p, id, plan, R.SummonCost(family));
            // Material outputs inherit their offering, not unrelated earlier payments.
            bool bound = material ? plan.Bound : r.Bound || plan != null && plan.Bound;
            if (Q.Execute(c, t, "INSERT INTO revora_abyss_encounters(id,run,family,bound) VALUES(@id,@run,@family,@bound)",
                "id", id, "run", r.Id, "family", (int)family, "bound", bound) != 1) throw new InvalidOperationException("Abyss summon was not saved.");
            Save(c, t, r, r.Progress, satan && bound); return new Encounter { Id = id, Run = r.Id, Family = family, Bound = bound, Created = true };
        }
        private static uint SpiritNpc(uint family, uint npc) {
            switch (family) {
                case 3636: if (npc == 3615 || npc == 3616) return npc; break;
                case 3637: if (npc == 3617 || npc == 3618) return npc; break;
                case 3638: if (npc >= 3619 && npc <= 3621) return npc; break;
                case 3639: if (npc == 3622 || npc == 3626 || npc == 3627) return npc; break;
                case 3640: if (npc == 3628 || npc == 3629) return npc; break;
                case 3641: if (npc >= 3630 && npc <= 3632) return npc; break;
                case 3642: if (npc == 3633 || npc == 3634) return npc; break;
                case 3643: if (npc >= 3635 && npc <= 3637) return npc; break;
            }
            throw new InvalidOperationException("Unknown Abyss spirit keeper.");
        }
        private static void Reward(NpgsqlConnection c, NpgsqlTransaction t, Guid id, Run r, uint item, bool bound) {
            if (Q.Execute(c, t, "INSERT INTO revora_abyss_rewards(id,run,item_type,bound) VALUES(@id,@run,@item,@bound)",
                "id", id, "run", r.Id, "item", (int)item, "bound", bound) != 1) throw new InvalidOperationException("Abyss spoils were not saved.");
        }
        // The caller supplies a server-generated life identity only after proving
        // canonical combat. Persisted dedup also survives an uncertain commit reply.
        internal static bool Defeat(NpgsqlConnection c, NpgsqlTransaction t, GameState p, Guid encounter, uint family) {
            var r = Require(c, t, p);
            if (p.Entity.MapID != R.Map) throw new InvalidOperationException("Abyss hunter left the world.");
            var a = Active(c, t, r);
            if (a == null || a.Id != encounter || a.Family != family) return false;
            bool material = r.Progress.Step == R.Stage.Vigor && family >= 3632 && family <= 3635;
            var next = R.Kill(r.Progress, family, true);
            if (!material && ReferenceEquals(next, r.Progress)) throw new InvalidOperationException("Abyss encounter no longer matches its stage.");
            if (Q.Execute(c, t, "UPDATE revora_abyss_encounters SET defeated_at=now() WHERE id=@id AND run=@run AND defeated_at IS NULL", "id", a.Id, "run", r.Id) != 1)
                throw new InvalidOperationException("Abyss encounter defeat was not saved.");
            if (material) Reward(c, t, a.Id, r, R.SummonReward(family), a.Bound);
            Save(c, t, r, next, !material && a.Bound); return true;
        }
        internal static bool WildDeath(NpgsqlConnection c, NpgsqlTransaction t, GameState p, Guid life, uint family, int roll) {
            if (life == Guid.Empty || R.GrievanceWeight(family) == 0 || roll < 0 || roll >= 100) throw new ArgumentException("Invalid Abyss wild death.");
            var r = Require(c, t, p);
            if (p.Entity.MapID != R.Map) throw new InvalidOperationException("Abyss hunter left the world.");
            if (r.Progress.Step != R.Stage.Vigor && r.Progress.Step != R.Stage.Grievance && !(r.Progress.Step == R.Stage.Satan && r.Progress.SatanForms == 0 && R.BeadSource(family))) return false;
            if (r.Progress.Step == R.Stage.Grievance && !r.Introduced) return false;
            using (var q = Q.Command(c, t, "SELECT EXISTS(SELECT 1 FROM revora_abyss_deaths WHERE life=@life)", "life", life))
                if (Equals(q.ExecuteScalar(), true)) return false;
            if (Q.Execute(c, t, "INSERT INTO revora_abyss_deaths(run,life,family) VALUES(@run,@life,@family)", "run", r.Id, "life", life, "family", (int)family) != 1)
                throw new InvalidOperationException("Abyss death receipt was not saved.");
            // Revora rates: 50% base material (16/17/17), 25% bead from its four lords.
            uint output = r.Progress.Step == R.Stage.Vigor && roll < 50 ? (roll < 16 ? 722723u : roll < 33 ? 722724u : 722725u)
                : r.Progress.Step == R.Stage.Satan && roll < 25 ? 722727u : 0;
            if (output != 0) Reward(c, t, life, r, output, false);
            var next = R.Kill(r.Progress, family, false);
            if (next.Meters != r.Progress.Meters) Save(c, t, r, next, false);
            return true;
        }
        internal static ConquerItem Claim(NpgsqlConnection c, NpgsqlTransaction t, GameState p) {
            var r = Require(c, t, p); M.Bag(c, t, p);
            if (p.Inventory.Count >= 40) return null;
            Guid id; uint type; bool bound;
            using (var q = Q.Command(c, t, "SELECT id,item_type,bound FROM revora_abyss_rewards WHERE run=@run AND claimed_at IS NULL ORDER BY created_at,id LIMIT 1 FOR UPDATE", "run", r.Id))
            using (var row = q.ExecuteReader()) { if (!row.Read()) return null; id = row.GetGuid(0); type = checked((uint)row.GetInt32(1)); bound = row.GetBoolean(2); }
            var item = M.Insert(c, t, p, type, bound);
            if (Q.Execute(c, t, "UPDATE revora_abyss_rewards SET claimed_at=now(),claimed_item=@item WHERE id=@id AND run=@run AND claimed_at IS NULL",
                "item", (long)item.UID, "id", id, "run", r.Id) != 1) throw new InvalidOperationException("Abyss spoils claim was not saved.");
            return item;
        }
    }
}
