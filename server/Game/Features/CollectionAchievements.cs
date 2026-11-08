using System.Linq;
using Npgsql;
using Revora.Database;

namespace Revora.Game.Features
{
    internal static class CollectionAchievements
    {
        internal static void Record(NpgsqlConnection c, NpgsqlTransaction t, uint uid)
        {
            // Fixed numeric interoperability identities; catalog repair requires a new audit.
            var garments = CollectionAchievementCatalog.Garments.Where(id => StorageItem.Garments.ContainsKey(id)
                && StorageItem.Garments[id].ID == id && StorageItem.Garments[id].Type == 1
                && ConquerItemInformation.BaseInformations.ContainsKey(id)).Select(id => (long)id).ToArray();
            var mounts = CollectionAchievementCatalog.Mounts.Where(id => StorageItem.Mounts.ContainsKey(id)
                && StorageItem.Mounts[id].ID == id && StorageItem.Mounts[id].Type == 2
                && ConquerItemInformation.BaseInformations.ContainsKey(id)).Select(id => (long)id).ToArray();
            int garmentCount, mountCount;
            using (var q = QuestInventory.Command(c, t, @"SELECT
count(DISTINCT id) FILTER(WHERE id=ANY(@garments) AND position IN(0,9)),
count(DISTINCT id) FILTER(WHERE id=ANY(@mounts) AND position IN(0,17))
FROM items WHERE entityid=@uid AND inwardrobe=1 AND warehouse=0 AND stacksize=1
AND days::text='0'", "garments", garments, "mounts", mounts, "uid", (long)uid))
            using (var r = q.ExecuteReader()) {
                r.Read(); garmentCount = checked((int)r.GetInt64(0)); mountCount = checked((int)r.GetInt64(1));
            }
            RecordCounts(c, t, uid, garmentCount, mountCount);
        }

        internal static void RecordCounts(NpgsqlConnection c, NpgsqlTransaction t, uint uid, int garments, int mounts)
        {
            if (mounts >= 1) ProgressionEvidence.Complete(c,t,uid,11224,"wardrobe-permanent-v1","mounts:1",1);
            if (garments >= 1) ProgressionEvidence.Complete(c,t,uid,11225,"wardrobe-permanent-v1","garments:1",1);
            if (mounts >= 10) ProgressionEvidence.Complete(c,t,uid,11226,"wardrobe-permanent-v1","mounts:10",1);
            if (garments >= 10) ProgressionEvidence.Complete(c,t,uid,11227,"wardrobe-permanent-v1","garments:10",1);
            if (mounts >= 50) ProgressionEvidence.Complete(c,t,uid,11228,"wardrobe-permanent-v1","mounts:50",1);
            if (garments >= 50) ProgressionEvidence.Complete(c,t,uid,11229,"wardrobe-permanent-v1","garments:50",1);
            if (mounts >= 100) ProgressionEvidence.Complete(c,t,uid,11230,"wardrobe-permanent-v1","mounts:100",1);
            if (garments >= 100) ProgressionEvidence.Complete(c,t,uid,11231,"wardrobe-permanent-v1","garments:100",1);
            if (mounts >= 200) ProgressionEvidence.Complete(c,t,uid,11232,"wardrobe-permanent-v1","mounts:200",1);
            if (garments >= 200) ProgressionEvidence.Complete(c,t,uid,11301,"wardrobe-permanent-v1","garments:200",1);
        }
    }
}
