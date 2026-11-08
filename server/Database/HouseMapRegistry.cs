using System;
using System.Linq;
using Npgsql;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Database
{
    // Transaction participant for housing construction. A reservation is not a paid house.
    internal static class HouseMapRegistry
    {
        internal const int First = 60000, Last = 65534;
        internal static void Initialize()
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open();
                Q.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_house_maps(
uid bigint PRIMARY KEY REFERENCES entities(uid),
map_id integer NOT NULL UNIQUE CHECK(map_id BETWEEN 60000 AND 65534),
CHECK(uid BETWEEN 1 AND 4294967295));");
            }
        }

        internal static ushort Reserve(NpgsqlConnection c, NpgsqlTransaction t, uint owner)
        {
            if (owner == 0 || t == null || t.Connection != c)
                throw new ArgumentException("House reservation requires its owner transaction.");
            // Shared across sessions/processes. The reservation and construction payment
            // must commit or roll back together; this method never commits independently.
            using (var q = Q.Command(c, t, "SELECT uid FROM entities WHERE uid=@uid FOR UPDATE", "uid", (long)owner))
                if (q.ExecuteScalar() == null) throw new InvalidOperationException("House owner is not persisted.");
            Q.Execute(c, t, "SELECT pg_advisory_xact_lock(6711,60000)");
            using (var q = Q.Command(c, t, "SELECT map_id FROM revora_house_maps WHERE uid=@uid FOR UPDATE", "uid", (long)owner))
            {
                object saved = q.ExecuteScalar();
                if (saved != null)
                {
                    ushort id = checked((ushort)Convert.ToInt32(saved));
                    RequireAvailable(id, owner);
                    return id;
                }
            }
            int[] reserved = DMaps.MapPaths.Keys.Concat(MapsTable.MapInformations.Keys).Concat(Kernel.Maps.Keys)
                .Where(id => id >= First && id <= Last).Select(id => (int)id).Distinct().ToArray();
            int selected;
            using (var q = Q.Command(c, t, @"SELECT candidate FROM generate_series(60000,65534) AS candidate
WHERE NOT(candidate=ANY(@reserved)) AND NOT EXISTS(
SELECT 1 FROM revora_house_maps h WHERE h.map_id=candidate) ORDER BY candidate LIMIT 1", "reserved", reserved))
            {
                object value = q.ExecuteScalar();
                if (value == null) throw new InvalidOperationException("House map identities exhausted.");
                selected = Convert.ToInt32(value);
            }
            if (Q.Execute(c, t, "INSERT INTO revora_house_maps(uid,map_id) VALUES(@uid,@map)",
                "uid", (long)owner, "map", selected) != 1)
                throw new InvalidOperationException("House map reservation was not saved.");
            return checked((ushort)selected);
        }

        private static void RequireAvailable(ushort id, uint owner)
        {
            MapsTable.MapInformation metadata; Game.Map map;
            if (id < First || id > Last || DMaps.MapPaths.ContainsKey(id))
                throw new InvalidOperationException("Reserved house map conflicts with static terrain.");
            bool hasMetadata = MapsTable.MapInformations.TryGetValue(id, out metadata);
            if (hasMetadata && (metadata.ID != id || metadata.Owner != owner))
                throw new InvalidOperationException("Reserved house map belongs to another world.");
            if (Kernel.Maps.TryGetValue(id, out map) && (!hasMetadata || map.ID != id || map.BaseID != metadata.BaseID))
                throw new InvalidOperationException("Reserved house runtime requires reconciliation.");
        }
    }
}
