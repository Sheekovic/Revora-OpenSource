using System;
using System.Linq;
using System.Reflection;
using Revora.Database;

// Read-only catalog evidence. No database, server startup, item grants or purchases.
internal static class CheckCollectionShopCapacity
{
    private static void Main(string[] args)
    {
        if(args.Length>1)throw new ArgumentException("Supply at most one reviewed collection source file.");
        string source=args.Length==0?null:System.IO.File.ReadAllText(args[0]);
        ConquerItemInformation.Load();
        StorageItem.Load();
        EShopFile.Load();
        EShopV2File.Load();
        var catalog = typeof(ConquerItemInformation).Assembly.GetType(
            "Revora.Game.Features.CollectionAchievementCatalog", true);
        var shop = EShopFile.Shops[2888];
        System.Console.WriteLine("shop,category,designs,priced_designs,total_cp");
        foreach (string category in new[] { "Garments", "Mounts" })
        {
            var ids = (uint[])catalog.GetField(category,
                BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            if(source!=null){
                var block=System.Text.RegularExpressions.Regex.Match(source,category+@"\s*=\s*\{([^}]+)\};");
                if(!block.Success)throw new InvalidOperationException("Collection source array missing.");
                string body=block.Groups[1].Value;
                if(!System.Text.RegularExpressions.Regex.IsMatch(body,@"^\s*(\d+u\s*,\s*)+$"))
                    throw new InvalidOperationException("Unreviewed collection source syntax.");
                ids=System.Text.RegularExpressions.Regex.Matches(body,@"(\d+)u").Cast<System.Text.RegularExpressions.Match>()
                    .Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
                if(ids.Distinct().Count()!=ids.Length)throw new InvalidOperationException("Duplicate collection identity.");
                System.Console.WriteLine("source-catalog-override,"+category+","+ids.Length);
            }
            var stock = shop.Items.Distinct().Where(ids.Contains).ToArray();
            var priced = stock.Where(id => ConquerItemInformation.BaseInformations.ContainsKey(id)
                && ConquerItemInformation.BaseInformations[id].ConquerPointsWorth > 0).ToArray();
            ulong cost = priced.Aggregate(0UL, (sum, id) =>
                checked(sum + ConquerItemInformation.BaseInformations[id].ConquerPointsWorth));
            System.Console.WriteLine("2888," + category + "," + stock.Length + "," + priced.Length + "," + cost);
            foreach (uint id in priced.OrderBy(id => id))
                System.Console.WriteLine("design," + category + "," + id + "," +
                    ConquerItemInformation.BaseInformations[id].ConquerPointsWorth);
            var goods = (Array)typeof(Revora.Game.Features.RevoraPointShopSystem)
                .GetField(category, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var vesper = goods.Cast<object>().Select(g => new {
                Id = (uint)g.GetType().GetField("Id").GetValue(g),
                Price = (uint)g.GetType().GetField("Price").GetValue(g)
            }).Where(g => ids.Contains(g.Id) && g.Price > 0
                && ConquerItemInformation.BaseInformations.ContainsKey(g.Id)).ToArray();
            System.Console.WriteLine("vesper," + category + "," + vesper.Select(g => g.Id).Distinct().Count());
            foreach (var good in vesper.OrderBy(g => g.Id))
                System.Console.WriteLine("rvp-design," + category + "," + good.Id + "," + good.Price);
            System.Console.WriteLine("supplier-union," + category + "," +
                priced.Concat(vesper.Select(g => g.Id)).Distinct().Count());
            var shopIds = priced.Concat(vesper.Select(g => g.Id)).Distinct().ToArray();
            var easter = (uint[])typeof(ConquerItemInformation).Assembly.GetType(
                "Revora.Game.Features.EasterGoldPacks", true).GetField("Rewards",
                BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            if (easter.Length != 30) throw new InvalidOperationException("Review the Easter roll range before recounting.");
            var extra = easter.Intersect(ids).Except(shopIds).OrderBy(id => id).ToArray();
            System.Console.WriteLine("easter-additional," + category + "," + extra.Length);
            foreach (uint id in extra) System.Console.WriteLine("easter-design," + category + "," + id);
            System.Console.WriteLine("shops-and-easter-upper-bound," + category + "," +
                shopIds.Concat(extra).Distinct().Count());
            var available = new System.Collections.Generic.HashSet<uint>(shopIds);
            if (category == "Mounts")
            {
                var mountFor = typeof(ConquerItemInformation).Assembly.GetType(
                    "Revora.Game.Features.FixedOutputPacks", true).GetMethod("MountFor",
                    BindingFlags.Static | BindingFlags.NonPublic);
                foreach (uint pack in shop.Items.Distinct().OrderBy(id => id))
                {
                    uint output = (uint)mountFor.Invoke(null, new object[] { pack });
                    if (output == 0 || !ids.Contains(output)
                        || !ConquerItemInformation.BaseInformations.ContainsKey(pack)
                        || !ConquerItemInformation.BaseInformations.ContainsKey(output)
                        || ConquerItemInformation.BaseInformations[pack].ConquerPointsWorth == 0) continue;
                    available.Add(output);
                    System.Console.WriteLine("atomic-mall-pack," + pack + "," + output + "," +
                        ConquerItemInformation.BaseInformations[pack].ConquerPointsWorth);
                }
                System.Console.WriteLine("including-atomic-pack-upper-bound,Mounts," + available.Concat(extra).Distinct().Count());
            }
            var recipe = typeof(ConquerItemInformation).Assembly.GetType(
                "Revora.Game.Features.PlanetWeekend", true).GetMethod("CollectionRewards",
                BindingFlags.Static | BindingFlags.NonPublic);
            var planet = Enumerable.Range(1,8).SelectMany(step =>
                (uint[])recipe.Invoke(null,new object[] { step })).Distinct().ToArray();
            if (planet.Length != 19) throw new InvalidOperationException("Review the Planet collection edition before recounting.");
            foreach (uint id in planet.Intersect(ids).OrderBy(id => id))
                System.Console.WriteLine("planet-design," + category + "," + id);
            available.UnionWith(planet.Intersect(ids));
            System.Console.WriteLine("without-easter-upper-bound," + category + "," + available.Count);
            available.UnionWith(extra);
            var exchange = (uint[])typeof(ConquerItemInformation).Assembly.GetType(
                "Revora.Game.Features.PlanetCollectionExchange", true).GetField(category,
                BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            int expected = category == "Garments" ? 108 : 149;
            var exchangeMetadata = category == "Garments" ? StorageItem.Garments : StorageItem.Mounts;
            byte exchangeKind = category == "Garments" ? (byte)1 : (byte)2;
            if (exchange.Length != expected || exchange.Distinct().Count() != expected
                || exchange.Any(id => !ids.Contains(id) || !exchangeMetadata.ContainsKey(id)
                    || exchangeMetadata[id].ID != id || exchangeMetadata[id].Type != exchangeKind
                    || exchangeMetadata[id].Stars < (category == "Garments" ? 3u : 1u) || exchangeMetadata[id].Stars > 5
                    || !ConquerItemInformation.BaseInformations.ContainsKey(id)))
                throw new InvalidOperationException("Review exchange capacity and metadata before recounting.");
            // Suppliers may overlap after a catalog update. Count each design once;
            // do not reject a valid catalog or count overlapping offers as new designs.
            System.Console.WriteLine("planet-exchange-overlap," + category + "," + exchange.Intersect(available).Count());
            foreach (uint id in exchange)
                System.Console.WriteLine("planet-exchange-design," + category + "," + id + "," + exchangeMetadata[id].Stars);
            available.UnionWith(exchange);
            System.Console.WriteLine("all-reviewed-suppliers-upper-bound," + category + "," + available.Count);
            var wardrobe = category == "Garments" ? StorageItem.Garments : StorageItem.Mounts;
            byte kind = category == "Garments" ? (byte)1 : (byte)2;
            var compatible = ids.Distinct().Where(id => wardrobe.ContainsKey(id)
                && wardrobe[id].ID == id && wardrobe[id].Type == kind
                && ConquerItemInformation.BaseInformations.ContainsKey(id)).OrderBy(id => id).ToArray();
            var uncovered = compatible.Except(available).ToArray();
            System.Console.WriteLine("validated-compatible," + category + "," + compatible.Length);
            System.Console.WriteLine("validated-reviewed-supply," + category + "," + compatible.Intersect(available).Count());
            System.Console.WriteLine("outside-reviewed-supply," + category + "," + uncovered.Length);
            foreach (var group in uncovered.GroupBy(id => wardrobe[id].Stars).OrderBy(g => g.Key))
                System.Console.WriteLine("outside-reviewed-supply-stars," + category + "," + group.Key + "," + group.Count());
            foreach (uint id in uncovered)
                System.Console.WriteLine("unreviewed-design," + category + "," + id + "," + wardrobe[id].Stars);
            foreach (int target in new[] {100,200})
                System.Console.WriteLine("tier-shortfall," + category + "," + target + "," + Math.Max(0,target-compatible.Intersect(available).Count())
                    + ",catalog-feasible=" + (compatible.Length>=target));


        }
    }
}
