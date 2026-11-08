using System;
using System.Linq;
using Revora.Database;
using Revora.Game.Features;

// Uses actual merged catalogs; no listener, database, character or item grants.
internal static class CheckPirateEpicCatalog
{
    static void Main() {
        ConquerItemInformation.Load();
        var catalog=ConquerItemInformation.BaseInformations;
        foreach(uint id in new uint[]{3307443,3307444,3307445,3307446,3307447,3307448,3307449,3307450})
            if(!catalog.ContainsKey(id))throw new Exception("Missing Pirate quest item "+id);
        Console.WriteLine("family,source_count,compatible_count,unsupported_source_ids");
        foreach(uint family in new uint[]{611,612}) {
            var inputs=catalog.Keys.Where(id=>id/1000==family).OrderBy(id=>id).ToArray();
            var missing=inputs.Where(id=>PirateEpicRules.EpicWeapon(id,catalog.ContainsKey)==0).ToArray();
            if(inputs.Length==0)throw new Exception("Missing ordinary pirate weapon family");
            foreach(uint source in inputs.Except(missing)) {
                uint target=PirateEpicRules.EpicWeapon(source,catalog.ContainsKey);
                var a=catalog[source];var b=catalog[target];
                if(a.Level!=b.Level || a.Class!=b.Class || source%10!=target%10)
                    throw new Exception("Unreviewed conversion metadata "+source+" -> "+target);
            }
            Console.WriteLine(family+","+inputs.Length+","+(inputs.Length-missing.Length)+",\""+string.Join(" ",missing)+"\"");
        }
    }
}
