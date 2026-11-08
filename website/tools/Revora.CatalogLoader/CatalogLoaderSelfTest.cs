using System.Security.Cryptography;
using System.Text.Json;

namespace Revora.CatalogLoader;

internal static class CatalogLoaderSelfTest
{
    public static async Task<int> RunAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "revora-catalog-loader-" + Guid.NewGuid().ToString("N"));
        string referenceRoot = root + "-reference";
        Directory.CreateDirectory(root);
        try
        {
            await WriteFixtureAsync(root, unresolved: false);
            ReviewedCatalog catalog = await ReviewedCatalog.LoadAsync(root);
            Check(catalog.Categories["items"].Rows.Count == 1 && catalog.TotalRows == 15, "valid reviewed fixture");

            string itemPath = Path.Combine(root, "items.reviewed.json");
            string itemJson = await File.ReadAllTextAsync(itemPath);
            await File.WriteAllTextAsync(itemPath, itemJson.Replace("100000", "100001", StringComparison.Ordinal));
            await ExpectFailureAsync(root, "category hash tampering");

            Recreate(root); await WriteFixtureAsync(root, unresolved: true);
            await ExpectFailureAsync(root, "reference closure");

            Recreate(root); await WriteFixtureAsync(root, unresolved: false);
            catalog = await ReviewedCatalog.LoadAsync(root);
            Directory.CreateDirectory(referenceRoot);
            await WriteReferenceFixtureAsync(referenceRoot);
            ReviewedGameplayReference reference = await ReviewedGameplayReference.LoadAsync(referenceRoot);
            GameplayReferenceClosure closure = reference.EvaluateAgainst(catalog);
            Check(closure.ValidNpcs.Count == 1 && closure.ValidPortals.Count == 1 && closure.ResolvedShopRows == 1 &&
                closure.ConflictingItemAddRows == 0, "combined reference closure");

            Check(!LoaderOptions.Parse([root]).Apply, "dry-run default");
            try { _ = LoaderOptions.Parse([root, "--apply"]); throw new Exception("Apply without a protected secret was accepted."); }
            catch (ArgumentException) { }
            Console.WriteLine("Revora.CatalogLoader: 5 bounded reviewed-JSON self-tests passed.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error.Message); return 1; }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
            if (Directory.Exists(referenceRoot)) Directory.Delete(referenceRoot, true);
        }
    }

    private static async Task WriteReferenceFixtureAsync(string root)
    {
        var categories = new SortedDictionary<string, object[]>(StringComparer.Ordinal)
        {
            ["npcs"] = [new { legacyUid=10000L, npcType=120L, flags=2L, face=6L, mapId=1002L, x=1L, y=1L,
                activeMonth=0L, activeBegin=0L, activeEnd=0L, destinationMap=0L, destinationX=0L, destinationY=0L, duration=0L }],
            ["portals"] = [new { legacyId=1L, sourceMap=1002L, sourceX=1L, sourceY=1L, destinationMap=1002L, destinationX=2L, destinationY=2L }],
            ["customshop"] = [new { legacyId=1L, itemType=100000L, price=1L, currencyType=0L }],
            ["itemadd"] = [new { legacyUniqueId=1L, baseItemType=100000L, plus=1L, health=1L, minimumDamage=0L,
                maximumDamage=1L, defense=0L, magicDamage=0L, magicDefense=0L, accuracy=0L, dodge=0L }],
        };
        var declarations = new SortedDictionary<string, object>(StringComparer.Ordinal);
        foreach ((string name, object[] rows) in categories)
        {
            declarations[name] = new { rowCount = rows.Length, contentSha256 = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(rows))) };
            await File.WriteAllBytesAsync(Path.Combine(root, name + ".reviewed.json"), JsonSerializer.SerializeToUtf8Bytes(rows));
        }
        var manifest = new
        {
            schemaVersion = 1,
            parserVersion = 1,
            sourceFilename = "revora.sql",
            sourceSha256 = new string('c', 64),
            sourceBytes = 123L,
            archiveFilename = "revora.zip",
            archiveSha256 = new string('d', 64),
            archiveBytes = 45L,
            classification = "reviewed-gameplay-reference-data-only",
            categories = declarations,
            exclusions = Array.Empty<string>(),
        };
        await File.WriteAllBytesAsync(Path.Combine(root, "manifest.reviewed.json"), JsonSerializer.SerializeToUtf8Bytes(manifest));
    }

    private static async Task WriteFixtureAsync(string root, bool unresolved)
    {
        var categories = new SortedDictionary<string, object[]>(StringComparer.Ordinal)
        {
            ["maps"] = [new { Id = 1002u, Path = "map/map/newplain.DMap", Flags = 0u }],
            ["dmaps"] = [new { Path = "map/map/newplain.DMap", Length = 1000L, Sha256 = new string('a', 64), Format = "numeric-1003", Width = 100, Height = 100 }],
            ["items"] = [new { Id=100000u, Name="TestItem", Class=(byte)0, Profession=(byte)0, Level=(byte)1, Gender=(byte)0, Strength=(ushort)0,
                Agility=(ushort)0, SilverPrice=1u, MaximumAttack=(ushort)1, MinimumAttack=(ushort)0, Defense=0u, Accuracy=(byte)0, Dodge=(byte)0,
                Health=(ushort)0, Mana=(ushort)0, Durability=(ushort)1, MagicAttack=0u, MagicDefense=0u, ConquerPointPrice=0u }],
            ["item-upgrades"] = [new { ItemType = 100000u, Plus = (byte)1, Health = (ushort)1, MinimumAttack = 0u, MaximumAttack = 1u, Defense = (ushort)0, MagicAttack = (ushort)0, MagicDefense = (ushort)0, Accuracy = (ushort)0, Dodge = (byte)0 }],
            ["shops"] = [new { ShopId = 10000u, Ordinal = 0, ItemType = 100000u, CurrencyType = (byte)0 }],
            ["monsters"] = [new { Id=1, Name="TestMonster", Behavior=(byte)0, Mesh=(ushort)1, Level=(byte)1, MaximumHealth=1u, Defense=(ushort)0,
                MagicDefense=(ushort)0, MagicAttack=(ushort)0, MinimumAttack=(ushort)0, MaximumAttack=(ushort)1, DamageReduction=(byte)0, Dodge=(byte)0,
                AttackType=(byte)0, MagicSkill=(ushort)0, MagicLevel=(byte)0, GivesRewards=true, AttackDistance=(byte)1, MinimumSilver=0, MaximumSilver=0,
                MoveDelay=(ushort)100, SpawnDelay=100u, LevelDifferenceDamage=false }],
            ["spawns"] = [new { MonsterId = 1, Count = 1, MapId = unresolved ? 9999u : 1002u, MinimumX = (ushort)1, MinimumY = (ushort)1, MaximumX = (ushort)2, MaximumY = (ushort)2 }],
            ["drops"] = [new { MonsterId = 1, ItemType = 100000u, Plus = (byte)0, Sockets = (byte)0, Bless = (byte)0, ChancePercent = 1m }],
            ["equipment-drops"] = [new { Level = (byte)1, ItemType = 100000u }],
            ["magic-types"] = [new { Id = (ushort)1, Level = (byte)0, Sort = (byte)0, Name = "TestMagic", Damage = 1u, Mana = 0, ActivationChance = (byte)100, RequiredExperience = 0u, RequiredLevelValue = 0u, RequiredLevel = (byte)1, SourceFieldCount = 10 }],
            ["skills"] = [new { Id = (ushort)1 }],
            ["experience"] = [new { Level = 1, RequiredExperience = 0u }],
            ["portals"] = [new { SourceMap = 1002u, SourceX = (ushort)1, SourceY = (ushort)1, DestinationMap = 1002u, DestinationX = (ushort)2, DestinationY = (ushort)2 }],
            ["character-stats"] = [new { ClassName = "Taoist", Level = 1, Strength = (ushort)0, Agility = (ushort)0, Vitality = (ushort)1, Spirit = (ushort)0 }],
            ["companions"] = [new { Type = 1u }],
        };
        var manifestCategories = new SortedDictionary<string, object>(StringComparer.Ordinal);
        foreach ((string name, object[] rows) in categories)
        {
            byte[] canonical = JsonSerializer.SerializeToUtf8Bytes(rows);
            manifestCategories[name] = new { RowCount = rows.Length, ContentSha256 = Convert.ToHexStringLower(SHA256.HashData(canonical)) };
            await File.WriteAllBytesAsync(Path.Combine(root, name + ".reviewed.json"), JsonSerializer.SerializeToUtf8Bytes(rows, new JsonSerializerOptions { WriteIndented = true }));
        }
        var manifest = new
        {
            schemaVersion = 1,
            parserVersion = 1,
            sourceFilename = "ReVora-Essentials.zip",
            sourceSha256 = new string('b', 64),
            sourceBytes = 123L,
            classification = "owner-authorized-gameplay-reference-intake",
            categories = manifestCategories,
            integrity = new
            {
                missingMapAssets = Array.Empty<string>(),
                unresolvedShopItems = Array.Empty<uint>(),
                unresolvedSpawnMonsters = Array.Empty<int>(),
                unresolvedSpawnMaps = Array.Empty<uint>()
            },
            exclusions = Array.Empty<string>(),
            artifacts = Array.Empty<object>()
        };
        await File.WriteAllBytesAsync(Path.Combine(root, "manifest.reviewed.json"), JsonSerializer.SerializeToUtf8Bytes(manifest, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static async Task ExpectFailureAsync(string root, string name)
    {
        try { _ = await ReviewedCatalog.LoadAsync(root); throw new Exception($"Self-test failed: {name} was accepted."); }
        catch (InvalidDataException) { }
    }
    private static void Check(bool condition, string name) { if (!condition) throw new Exception("Self-test failed: " + name); }
    private static void Recreate(string path) { Directory.Delete(path, true); Directory.CreateDirectory(path); }
}
