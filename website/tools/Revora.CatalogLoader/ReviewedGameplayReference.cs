using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Revora.CatalogLoader;

internal sealed class ReviewedGameplayReference
{
    private static readonly string[] RequiredCategories = ["customshop", "itemadd", "npcs", "portals"];

    private ReviewedGameplayReference(string sourceSha256, long sourceBytes, string archiveSha256, long archiveBytes, string manifestSha256,
        IReadOnlyDictionary<string, ReviewedCategory> categories)
    {
        SourceSha256 = sourceSha256;
        SourceBytes = sourceBytes;
        ArchiveSha256 = archiveSha256;
        ArchiveBytes = archiveBytes;
        ManifestSha256 = manifestSha256;
        Categories = categories;
        SourceId = DeterministicGuid("owner-sql:" + archiveSha256 + ":" + sourceSha256 + ":" + manifestSha256);
        BatchId = DeterministicGuid("owner-sql-batch:" + sourceSha256 + ":" + manifestSha256);
    }

    public string SourceSha256 { get; }
    public long SourceBytes { get; }
    public string ArchiveSha256 { get; }
    public long ArchiveBytes { get; }
    public string ManifestSha256 { get; }
    public Guid SourceId { get; }
    public Guid BatchId { get; }
    public IReadOnlyDictionary<string, ReviewedCategory> Categories { get; }
    public int TotalRows => Categories.Values.Sum(static category => category.Rows.Count);

    public GameplayReferenceClosure EvaluateAgainst(ReviewedCatalog catalog)
    {
        HashSet<long> maps = catalog.Categories["maps"].Rows
            .Select(row => ReviewedCatalog.Integer(row, "Id", 1, uint.MaxValue)).ToHashSet();
        HashSet<long> items = catalog.Categories["items"].Rows
            .Select(row => ReviewedCatalog.Integer(row, "Id", 1, uint.MaxValue)).ToHashSet();
        JsonElement[] validNpcs = Categories["npcs"].Rows
            .Where(row => maps.Contains(Integer(row, "mapId", 1, uint.MaxValue))).ToArray();
        long[] missingNpcMaps = Categories["npcs"].Rows
            .Select(row => Integer(row, "mapId", 1, uint.MaxValue)).Where(map => !maps.Contains(map)).Distinct().Order().ToArray();
        HashSet<long> validNpcIds = validNpcs.Select(row => Integer(row, "legacyUid", 1, uint.MaxValue)).ToHashSet();
        long[] unresolvedShopNpcIds = catalog.Categories["shops"].Rows
            .Select(row => ReviewedCatalog.Integer(row, "ShopId", 1, uint.MaxValue)).Distinct()
            .Where(id => !validNpcIds.Contains(id)).Order().ToArray();
        int resolvedShopRows = catalog.Categories["shops"].Rows.Count(row =>
            validNpcIds.Contains(ReviewedCatalog.Integer(row, "ShopId", 1, uint.MaxValue)));
        JsonElement[] validPortals = Categories["portals"].Rows.Where(row =>
            maps.Contains(Integer(row, "sourceMap", 1, uint.MaxValue)) &&
            maps.Contains(Integer(row, "destinationMap", 1, uint.MaxValue))).ToArray();
        long[] missingPortalMaps = Categories["portals"].Rows
            .SelectMany(row => new[] { Integer(row, "sourceMap", 1, uint.MaxValue), Integer(row, "destinationMap", 1, uint.MaxValue) })
            .Where(map => !maps.Contains(map)).Distinct().Order().ToArray();
        int unresolvedCustomShopItems = Categories["customshop"].Rows.Count(row =>
            !items.Contains(Integer(row, "itemType", 1, uint.MaxValue)));
        int conflictingItemAddRows = CountItemAddConflicts(catalog);
        return new(validNpcs, validPortals, missingNpcMaps, missingPortalMaps, unresolvedShopNpcIds,
            resolvedShopRows, unresolvedCustomShopItems, conflictingItemAddRows);
    }

    private int CountItemAddConflicts(ReviewedCatalog catalog)
    {
        var current = catalog.Categories["item-upgrades"].Rows.ToDictionary(
            row => (ReviewedCatalog.Integer(row, "ItemType", 1, uint.MaxValue), ReviewedCatalog.Integer(row, "Plus", 0, 12)));
        int conflicts = 0;
        foreach (JsonElement row in Categories["itemadd"].Rows)
        {
            if (!current.TryGetValue((Integer(row, "baseItemType", 1, uint.MaxValue), Integer(row, "plus", 0, 12)), out JsonElement candidate))
            {
                conflicts++;
                continue;
            }
            string[] pairs = ["health:Health", "minimumDamage:MinimumAttack", "maximumDamage:MaximumAttack", "defense:Defense",
                "magicDamage:MagicAttack", "magicDefense:MagicDefense", "accuracy:Accuracy", "dodge:Dodge"];
            if (pairs.Any(pair =>
            {
                string[] names = pair.Split(':');
                return Integer(row, names[0], 0, long.MaxValue) != ReviewedCatalog.Integer(candidate, names[1], 0, long.MaxValue);
            })) conflicts++;
        }
        return conflicts;
    }

    public static async Task<ReviewedGameplayReference> LoadAsync(string directory)
    {
        directory = Path.GetFullPath(directory);
        if (!Directory.Exists(directory) || (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("The reviewed gameplay-reference directory is missing or unsafe.");
        string manifestPath = SafeFile(directory, "manifest.reviewed.json");
        byte[] bytes = await File.ReadAllBytesAsync(manifestPath);
        try
        {
            using JsonDocument document = Parse(bytes, "gameplay-reference manifest", 1024 * 1024);
            JsonElement root = document.RootElement;
            RequireProperties(root, "gameplay-reference manifest", "schemaVersion", "parserVersion", "sourceFilename",
                "sourceSha256", "sourceBytes", "archiveFilename", "archiveSha256", "archiveBytes", "classification", "categories", "exclusions");
            if (Integer(root, "schemaVersion", 1, 1) != 1 || Integer(root, "parserVersion", 1, 1) != 1 ||
                Text(root, "sourceFilename") != "revora.sql" ||
                Text(root, "archiveFilename") != "revora.zip" ||
                Text(root, "classification") != "reviewed-gameplay-reference-data-only")
                throw new InvalidDataException("The reviewed gameplay-reference manifest classification is unsupported.");
            string sourceSha = Hash(Text(root, "sourceSha256"));
            long sourceBytes = Integer(root, "sourceBytes", 1, 64 * 1024 * 1024);
            string archiveSha = Hash(Text(root, "archiveSha256"));
            long archiveBytes = Integer(root, "archiveBytes", 1, 64 * 1024 * 1024);
            JsonElement categoriesElement = root.GetProperty("categories");
            if (categoriesElement.ValueKind != JsonValueKind.Object ||
                categoriesElement.EnumerateObject().Count() != RequiredCategories.Length)
                throw new InvalidDataException("The reviewed gameplay-reference category set is invalid.");
            var categories = new SortedDictionary<string, ReviewedCategory>(StringComparer.Ordinal);
            foreach (string category in RequiredCategories)
            {
                JsonElement declaration = categoriesElement.GetProperty(category);
                RequireProperties(declaration, category, "rowCount", "contentSha256");
                int count = checked((int)Integer(declaration, "rowCount", 0, 1_000_000));
                string contentSha = Hash(Text(declaration, "contentSha256"));
                byte[] categoryBytes = await File.ReadAllBytesAsync(SafeFile(directory, category + ".reviewed.json"));
                try
                {
                    using JsonDocument categoryDocument = Parse(categoryBytes, category, 128 * 1024 * 1024);
                    if (categoryDocument.RootElement.ValueKind != JsonValueKind.Array)
                        throw new InvalidDataException($"Reviewed gameplay-reference category {category} is not an array.");
                    JsonElement[] rows = categoryDocument.RootElement.EnumerateArray().Select(static row => row.Clone()).ToArray();
                    if (rows.Length != count || !FixedHash(ReviewedCatalog.CanonicalHash(categoryDocument.RootElement), contentSha))
                        throw new InvalidDataException($"Reviewed gameplay-reference category {category} differs from its manifest.");
                    categories.Add(category, new ReviewedCategory(category, contentSha, rows));
                }
                finally { CryptographicOperations.ZeroMemory(categoryBytes); }
            }
            ValidateRows(categories);
            return new(sourceSha, sourceBytes, archiveSha, archiveBytes, ReviewedCatalog.CanonicalHash(root), categories);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private static void ValidateRows(IReadOnlyDictionary<string, ReviewedCategory> categories)
    {
        Unique(categories["npcs"].Rows, "legacyUid", 1, uint.MaxValue, "NPC");
        Unique(categories["portals"].Rows, "legacyId", 1, long.MaxValue, "portal");
        Unique(categories["customshop"].Rows, "legacyId", 1, long.MaxValue, "custom-shop row");
        Unique(categories["itemadd"].Rows, "legacyUniqueId", 1, long.MaxValue, "item-add row");
        foreach (JsonElement npc in categories["npcs"].Rows)
        {
            _ = Integer(npc, "mapId", 0, uint.MaxValue);
            _ = Integer(npc, "x", 0, ushort.MaxValue);
            _ = Integer(npc, "y", 0, ushort.MaxValue);
        }
        foreach (JsonElement portal in categories["portals"].Rows)
        {
            _ = Integer(portal, "sourceMap", 1, uint.MaxValue);
            _ = Integer(portal, "destinationMap", 1, uint.MaxValue);
        }
    }

    private static void Unique(IEnumerable<JsonElement> rows, string field, long minimum, long maximum, string category)
    {
        var values = new HashSet<long>();
        foreach (JsonElement row in rows)
            if (!values.Add(Integer(row, field, minimum, maximum)))
                throw new InvalidDataException($"Reviewed {category} data contains a duplicate {field}.");
    }

    internal static long Integer(JsonElement row, string name, long minimum, long maximum)
    {
        if (!row.TryGetProperty(name, out JsonElement value) || !value.TryGetInt64(out long result) || result < minimum || result > maximum)
            throw new InvalidDataException($"Reviewed gameplay-reference field {name} is invalid.");
        return result;
    }

    private static string SafeFile(string directory, string filename)
    {
        string path = Path.Combine(directory, filename);
        if (!File.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException($"Reviewed gameplay-reference component {filename} is missing or unsafe.");
        return path;
    }

    private static JsonDocument Parse(byte[] bytes, string component, int maximum)
    {
        if (bytes.Length is 0 || bytes.Length > maximum)
            throw new InvalidDataException($"Reviewed {component} has an invalid size.");
        try { return JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 16, CommentHandling = JsonCommentHandling.Disallow }); }
        catch (JsonException error) { throw new InvalidDataException($"Reviewed {component} is malformed JSON.", error); }
    }

    private static void RequireProperties(JsonElement element, string component, params string[] names)
    {
        var expected = names.ToHashSet(StringComparer.Ordinal);
        if (element.ValueKind != JsonValueKind.Object || element.EnumerateObject().Any(property => !expected.Remove(property.Name)) || expected.Count != 0)
            throw new InvalidDataException($"Reviewed {component} has an unexpected schema.");
    }

    private static string Text(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.String)
            throw new InvalidDataException($"Reviewed gameplay-reference field {name} is missing.");
        return value.GetString()!;
    }

    private static string Hash(string value)
    {
        if (value.Length != 64 || !value.All(Uri.IsHexDigit)) throw new InvalidDataException("A reviewed SHA-256 value is malformed.");
        return value.ToLowerInvariant();
    }

    private static bool FixedHash(string actual, string expected) =>
        CryptographicOperations.FixedTimeEquals(Convert.FromHexString(actual), Convert.FromHexString(expected));

    private static Guid DeterministicGuid(string value)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        Span<byte> bytes = hash.AsSpan(0, 16);
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes);
    }
}

internal sealed record GameplayReferenceClosure(
    IReadOnlyList<JsonElement> ValidNpcs,
    IReadOnlyList<JsonElement> ValidPortals,
    IReadOnlyList<long> MissingNpcMaps,
    IReadOnlyList<long> MissingPortalMaps,
    IReadOnlyList<long> UnresolvedShopNpcIds,
    int ResolvedShopRows,
    int UnresolvedCustomShopItems,
    int ConflictingItemAddRows);
