using System.Security.Cryptography;
using System.Text.Json;

namespace Revora.CatalogLoader;

internal sealed record LoaderOptions(string ReviewedDirectory, string? GameplayReferenceDirectory, bool Apply, string? SecretFile)
{
    public static LoaderOptions Parse(string[] args)
    {
        if (args.Length is < 1 or > 5)
            throw new ArgumentException("Usage: Revora.CatalogLoader <reviewed-directory> [--reference <reviewed-gameplay-directory>] [--apply <protected-secret-file>]");
        string directory = Path.GetFullPath(args[0]);
        string? reference = null;
        string? secret = null;
        for (int index = 1; index < args.Length; index += 2)
        {
            if (index + 1 >= args.Length) throw new ArgumentException("Every loader option requires a value.");
            if (args[index] == "--reference" && reference is null)
                reference = Path.GetFullPath(args[index + 1]);
            else if (args[index] == "--apply" && secret is null)
                secret = Path.GetFullPath(args[index + 1]);
            else throw new ArgumentException("The loader options are invalid or duplicated.");
        }
        if (secret is not null && reference is null)
            throw new ArgumentException("Apply requires the reviewed gameplay-reference catalog.");
        return new(directory, reference, secret is not null, secret);
    }
}

internal sealed class ReviewedCatalog
{
    private static readonly string[] RequiredCategories =
    [
        "maps", "dmaps", "items", "item-upgrades", "shops", "monsters", "spawns", "drops",
        "equipment-drops", "magic-types", "skills", "experience", "portals", "character-stats", "companions",
    ];

    private ReviewedCatalog(string directory, string sourceSha256, long sourceBytes, string manifestSha256,
        IReadOnlyDictionary<string, ReviewedCategory> categories, IReadOnlySet<string> declaredMissingMapAssets,
        IReadOnlySet<long> declaredUnresolvedSpawnMaps)
    {
        Directory = directory;
        SourceSha256 = sourceSha256;
        SourceBytes = sourceBytes;
        ManifestSha256 = manifestSha256;
        Categories = categories;
        DeclaredMissingMapAssets = declaredMissingMapAssets;
        DeclaredUnresolvedSpawnMaps = declaredUnresolvedSpawnMaps;
        SourceId = DeterministicGuid("source:" + sourceSha256 + ":" + manifestSha256);
        BatchId = DeterministicGuid("batch:" + sourceSha256 + ":" + manifestSha256);
        ReleaseId = DeterministicGuid("release:" + sourceSha256 + ":" + manifestSha256);
    }

    public string Directory { get; }
    public string SourceSha256 { get; }
    public long SourceBytes { get; }
    public string ManifestSha256 { get; }
    public Guid SourceId { get; }
    public Guid BatchId { get; }
    public Guid ReleaseId { get; }
    public IReadOnlyDictionary<string, ReviewedCategory> Categories { get; }
    public IReadOnlySet<string> DeclaredMissingMapAssets { get; }
    public IReadOnlySet<long> DeclaredUnresolvedSpawnMaps { get; }
    public int TotalRows => Categories.Values.Sum(static category => category.Rows.Count);

    public static async Task<ReviewedCatalog> LoadAsync(string directory)
    {
        ValidateDirectory(directory);
        string manifestPath = SafeFile(directory, "manifest.reviewed.json");
        byte[] manifestBytes = await File.ReadAllBytesAsync(manifestPath);
        using JsonDocument manifest = ParseBounded(manifestBytes, "manifest", 8 * 1024 * 1024);
        JsonElement root = manifest.RootElement;
        RequireExactProperties(root, "manifest", "schemaVersion", "parserVersion", "sourceFilename", "sourceSha256", "sourceBytes",
            "classification", "categories", "integrity", "exclusions", "artifacts");
        if (RequiredInt(root, "schemaVersion") != 1 || RequiredInt(root, "parserVersion") != 1)
            throw new InvalidDataException("The reviewed manifest schema or parser version is unsupported.");
        if (RequiredString(root, "classification") != "owner-authorized-gameplay-reference-intake")
            throw new InvalidDataException("The reviewed manifest classification is not approved.");
        string sourceSha = Hash(RequiredString(root, "sourceSha256"));
        long sourceBytes = RequiredLong(root, "sourceBytes", 1, long.MaxValue);
        JsonElement categoryManifest = RequiredObject(root, "categories");
        var categories = new SortedDictionary<string, ReviewedCategory>(StringComparer.Ordinal);
        foreach (string name in RequiredCategories)
        {
            JsonElement declared = RequiredObject(categoryManifest, name);
            RequireExactProperties(declared, $"category {name}", "RowCount", "ContentSha256");
            int expectedCount = RequiredInt(declared, "RowCount", 0, 1_000_000);
            string expectedHash = Hash(RequiredString(declared, "ContentSha256"));
            string path = SafeFile(directory, name + ".reviewed.json");
            byte[] bytes = await File.ReadAllBytesAsync(path);
            using JsonDocument document = ParseBounded(bytes, name, 128 * 1024 * 1024);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException($"Reviewed category {name} is not an array.");
            JsonElement[] rows = document.RootElement.EnumerateArray().Select(static row => row.Clone()).ToArray();
            if (rows.Length != expectedCount)
                throw new InvalidDataException($"Reviewed category {name} row count differs from its manifest.");
            string actualHash = CanonicalHash(document.RootElement);
            if (!FixedHash(actualHash, expectedHash))
                throw new InvalidDataException($"Reviewed category {name} content hash differs from its manifest.");
            categories.Add(name, new(name, expectedHash, rows));
        }
        if (categoryManifest.EnumerateObject().Count() != RequiredCategories.Length)
            throw new InvalidDataException("The reviewed manifest has unexpected or duplicate categories.");
        JsonElement integrity = RequiredObject(root, "integrity");
        HashSet<string> missingMapAssets = StringArray(integrity, "missingMapAssets", StringComparer.OrdinalIgnoreCase);
        HashSet<long> unresolvedSpawnMaps = IntegerArray(integrity, "unresolvedSpawnMaps", 1, uint.MaxValue);
        if (IntegerArray(integrity, "unresolvedShopItems", 1, uint.MaxValue).Count != 0 ||
            IntegerArray(integrity, "unresolvedSpawnMonsters", 1, int.MaxValue).Count != 0)
            throw new InvalidDataException("The reviewed manifest has unresolved shop-item or spawn-monster references.");
        var catalog = new ReviewedCatalog(directory, sourceSha, sourceBytes, CanonicalHash(root), categories,
            missingMapAssets, unresolvedSpawnMaps);
        catalog.ValidateRowsAndClosure();
        CryptographicOperations.ZeroMemory(manifestBytes);
        return catalog;
    }

    private void ValidateRowsAndClosure()
    {
        HashSet<long> maps = UniqueIds("maps", "Id", 1, uint.MaxValue);
        HashSet<long> items = UniqueIds("items", "Id", 1, uint.MaxValue);
        HashSet<long> monsters = UniqueIds("monsters", "Id", 1, int.MaxValue);
        UniqueCompound("item-upgrades", "ItemType", "Plus");
        UniqueCompound("shops", "ShopId", "Ordinal");
        UniqueCompound("magic-types", "Id", "Level"); // Owner importer already applies ordered-last canonicalization.
        UniqueCompound("character-stats", "ClassName", "Level");
        UniqueIds("experience", "Level", 1, 255);

        // ItemAdd keys are donor item-family keys, not necessarily concrete ItemType identifiers.
        foreach (JsonElement row in Categories["item-upgrades"].Rows)
            _ = Integer(row, "ItemType", 1, uint.MaxValue);
        RequireReferences("shops", "ItemType", items);
        RequireReferences("spawns", "MonsterId", monsters);
        HashSet<long> actualUnresolvedSpawnMaps = Categories["spawns"].Rows
            .Select(row => Integer(row, "MapId", 1, uint.MaxValue)).Where(map => !maps.Contains(map)).ToHashSet();
        if (!actualUnresolvedSpawnMaps.SetEquals(DeclaredUnresolvedSpawnMaps))
            throw new InvalidDataException("Spawn-map closure differs from the exact quarantined set declared by the reviewed importer.");
        RequireReferences("drops", "MonsterId", monsters);
        RequireReferences("drops", "ItemType", items);
        RequireReferences("equipment-drops", "ItemType", items);
        foreach (IGrouping<long, JsonElement> shop in Categories["shops"].Rows.GroupBy(row => Integer(row, "ShopId", 1, uint.MaxValue)))
        {
            long[] currencies = shop.Select(row => Integer(row, "CurrencyType", 0, 2)).Distinct().ToArray();
            if (currencies.Length != 1) throw new InvalidDataException("A reviewed shop mixes currency types.");
        }
        // Portals are retained and integrity-checked as reviewed input, but are outside this V011-V022 loader.
        HashSet<string> dmaps = Categories["dmaps"].Rows.Select(row => RequiredString(row, "Path")).ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string> actualMissingMapAssets = Categories["maps"].Rows.Select(row => RequiredString(row, "Path"))
            .Where(path => !dmaps.Contains(path)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!actualMissingMapAssets.SetEquals(DeclaredMissingMapAssets))
            throw new InvalidDataException("Map/DMap closure differs from the exact quarantined set declared by the reviewed importer.");
    }

    private HashSet<long> UniqueIds(string category, string field, long minimum, long maximum)
    {
        var result = new HashSet<long>();
        foreach (JsonElement row in Categories[category].Rows)
            if (!result.Add(Integer(row, field, minimum, maximum)))
                throw new InvalidDataException($"Reviewed category {category} has a duplicate {field}.");
        return result;
    }

    private void UniqueCompound(string category, string first, string second)
    {
        var values = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement row in Categories[category].Rows)
        {
            string a = row.GetProperty(first).ToString();
            string b = row.GetProperty(second).ToString();
            if (!values.Add(a + ":" + b)) throw new InvalidDataException($"Reviewed category {category} has a duplicate key.");
        }
    }

    private void RequireReferences(string category, string field, HashSet<long> targets)
    {
        foreach (JsonElement row in Categories[category].Rows)
            if (!targets.Contains(Integer(row, field, 1, uint.MaxValue)))
                throw new InvalidDataException($"Reviewed category {category} has an unresolved {field} reference.");
    }

    internal static string CanonicalHash(JsonElement value) =>
        Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    internal static string RecordHash(JsonElement value) => CanonicalHash(value);
    internal static long Integer(JsonElement row, string field, long minimum, long maximum) =>
        RequiredLong(row, field, minimum, maximum);
    internal static string Text(JsonElement row, string field, int maximum = 128)
    {
        string value = RequiredString(row, field).Trim();
        if (value.Length is 0 || value.Length > maximum || value.Any(char.IsControl))
            throw new InvalidDataException($"Reviewed field {field} is not safe text.");
        return value;
    }

    private static void ValidateDirectory(string directory)
    {
        if (!System.IO.Directory.Exists(directory) || (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("The reviewed directory is missing or is a reparse point.");
    }
    private static string SafeFile(string directory, string name)
    {
        string path = Path.Combine(directory, name);
        if (!File.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException($"Required reviewed component {name} is missing or unsafe.");
        return path;
    }
    private static JsonDocument ParseBounded(byte[] bytes, string component, int maximum)
    {
        if (bytes.Length is 0 || bytes.Length > maximum) throw new InvalidDataException($"Reviewed {component} has an invalid size.");
        try { return JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 16, CommentHandling = JsonCommentHandling.Disallow }); }
        catch (JsonException error) { throw new InvalidDataException($"Reviewed {component} is malformed JSON.", error); }
    }
    private static JsonElement RequiredObject(JsonElement value, string name)
    {
        JsonElement property = value.GetProperty(name);
        return property.ValueKind == JsonValueKind.Object ? property : throw new InvalidDataException($"{name} is not an object.");
    }
    private static string RequiredString(JsonElement value, string name)
    {
        if (!value.TryGetProperty(name, out JsonElement property) || property.ValueKind != JsonValueKind.String)
            throw new InvalidDataException($"Required string {name} is absent.");
        return property.GetString()!;
    }
    private static int RequiredInt(JsonElement value, string name, int minimum = int.MinValue, int maximum = int.MaxValue) =>
        checked((int)RequiredLong(value, name, minimum, maximum));
    private static long RequiredLong(JsonElement value, string name, long minimum, long maximum)
    {
        if (!value.TryGetProperty(name, out JsonElement property) || !property.TryGetInt64(out long result) || result < minimum || result > maximum)
            throw new InvalidDataException($"Required integer {name} is invalid.");
        return result;
    }
    private static void RequireExactProperties(JsonElement value, string component, params string[] names)
    {
        HashSet<string> expected = names.ToHashSet(StringComparer.Ordinal);
        if (value.ValueKind != JsonValueKind.Object || value.EnumerateObject().Any(property => !expected.Remove(property.Name)) || expected.Count != 0)
            throw new InvalidDataException($"Reviewed {component} has an unexpected schema.");
    }
    private static string Hash(string value)
    {
        if (value.Length != 64 || !value.All(Uri.IsHexDigit)) throw new InvalidDataException("A SHA-256 value is malformed.");
        return value.ToLowerInvariant();
    }
    private static HashSet<string> StringArray(JsonElement root, string name, IEqualityComparer<string> comparer)
    {
        if (!root.TryGetProperty(name, out JsonElement array) || array.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"Integrity field {name} is absent.");
        var values = new HashSet<string>(comparer);
        foreach (JsonElement value in array.EnumerateArray())
        {
            string? text = value.GetString();
            if (string.IsNullOrWhiteSpace(text) || !values.Add(text)) throw new InvalidDataException($"Integrity field {name} is invalid.");
        }
        return values;
    }
    private static HashSet<long> IntegerArray(JsonElement root, string name, long minimum, long maximum)
    {
        if (!root.TryGetProperty(name, out JsonElement array) || array.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"Integrity field {name} is absent.");
        var values = new HashSet<long>();
        foreach (JsonElement value in array.EnumerateArray())
        {
            if (!value.TryGetInt64(out long number) || number < minimum || number > maximum || !values.Add(number))
                throw new InvalidDataException($"Integrity field {name} is invalid.");
        }
        return values;
    }
    private static bool FixedHash(string first, string second) =>
        CryptographicOperations.FixedTimeEquals(Convert.FromHexString(first), Convert.FromHexString(second));
    private static Guid DeterministicGuid(string input)
    {
        byte[] bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        byte[] guid = bytes[..16]; guid[6] = (byte)((guid[6] & 0x0f) | 0x50); guid[8] = (byte)((guid[8] & 0x3f) | 0x80);
        return new Guid(guid);
    }
}

internal sealed record ReviewedCategory(string Name, string ContentSha256, IReadOnlyList<JsonElement> Rows);
