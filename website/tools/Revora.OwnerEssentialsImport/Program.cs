using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

const string ApprovedArchiveSha256 = "dbd508b66abf5966e2b326f4a7cc45e20a5350c8f18a9845579f7022b6a7a457";
const int ParserVersion = 1;
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
Encoding ownerTextEncoding = Encoding.GetEncoding(54936, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
Encoding magicTextEncoding = Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);

if (args is ["--self-test"])
    return SelfTest();
if (args.Length != 2)
    return Fail("Usage: Revora.OwnerEssentialsImport <ReVora-Essentials.zip> <new-empty-output-directory>");

string archivePath = Path.GetFullPath(args[0]);
string outputPath = Path.GetFullPath(args[1]);
if (!File.Exists(archivePath) || (File.GetAttributes(archivePath) & FileAttributes.ReparsePoint) != 0)
    return Fail("The approved owner archive is missing or unsafe.");

string archiveHash;
await using (FileStream stream = new(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read))
    archiveHash = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream));
if (!FixedHashEquals(archiveHash, ApprovedArchiveSha256))
    return Fail("The input is not the approved owner essentials archive.");

Directory.CreateDirectory(outputPath);
if ((File.GetAttributes(outputPath) & FileAttributes.ReparsePoint) != 0 ||
    Directory.EnumerateFileSystemEntries(outputPath).Any())
    return Fail("The output directory must be empty and must not be a reparse point.");

using var archive = ZipFile.OpenRead(archivePath);
var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
foreach (ZipArchiveEntry entry in archive.Entries)
{
    string name = NormalizeEntryName(entry.FullName);
    if (name.Length == 0 || entry.FullName.EndsWith("/", StringComparison.Ordinal)) continue;
    if (!entries.TryAdd(name, entry)) return Fail("The archive has a case-colliding or duplicate path.");
    if (entry.Length > 64L * 1024 * 1024 && !name.EndsWith(".dmap", StringComparison.OrdinalIgnoreCase))
        return Fail("A non-map archive member exceeds the bounded intake size.");
}

var artifacts = new List<ArtifactRecord>(entries.Count);
foreach ((string name, ZipArchiveEntry entry) in entries.OrderBy(static item => item.Key, StringComparer.Ordinal))
{
    await using Stream stream = entry.Open();
    string hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream));
    artifacts.Add(new(name, entry.Length, hash, Classification(name)));
}

List<MapRecord> maps = ParseMaps(Required(entries, "GameMap.dat"));
var artifactByName = artifacts.ToDictionary(static row => row.Name, StringComparer.OrdinalIgnoreCase);
var dmaps = entries.Where(static row => row.Key.EndsWith(".dmap", StringComparison.OrdinalIgnoreCase))
    .OrderBy(static row => row.Key, StringComparer.Ordinal)
    .Select(row => ParseDMap(row.Value, artifactByName[row.Key])).ToArray();
var dmapNames = dmaps.Select(static row => row.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
var missingMapAssets = maps.Select(static row => row.Path).Distinct(StringComparer.OrdinalIgnoreCase)
    .Where(path => !dmapNames.Contains(path)).Order(StringComparer.OrdinalIgnoreCase).ToArray();

List<ItemRecord> itemRows = ParseItems(ReadLines(Required(entries, "Items.txt"), 4 * 1024 * 1024, ownerTextEncoding));
ItemRecord[] items = itemRows.GroupBy(static row => row.Id).Select(group =>
{
    ItemRecord[] variants = group.Distinct().ToArray();
    if (variants.Length != 1) throw new InvalidDataException($"Item {group.Key} has conflicting definitions.");
    return variants[0];
}).OrderBy(static row => row.Id).ToArray();

UpgradeRecord[] upgrades = ParseUpgrades(ReadLines(Required(entries, "ItemAdd.ini"), 2 * 1024 * 1024, ownerTextEncoding));
ShopRecord[] shops = ParseShops(ReadLines(Required(entries, "Shop.dat"), 2 * 1024 * 1024, ownerTextEncoding));
MonsterRecord[] monsters = ParseMonsters(ReadLines(Required(entries, "MobInfos.txt"), 2 * 1024 * 1024, ownerTextEncoding));
SpawnRecord[] spawns = ParseSpawns(ReadLines(Required(entries, "MobSpawns.txt"), 2 * 1024 * 1024, ownerTextEncoding));
DropRecord[] drops = ParseDrops(ReadLines(Required(entries, "DropRates.txt"), 2 * 1024 * 1024, ownerTextEncoding));
EquipmentDropRecord[] equipmentDrops = ParseEquipmentDrops(ReadLines(Required(entries, "EquipDrops.txt"), 2 * 1024 * 1024, ownerTextEncoding));
MagicRecord[] magicRows = ParseMagic(ReadLines(Required(entries, "MagicType.txt"), 4 * 1024 * 1024, magicTextEncoding));
MagicRecord[] magic = magicRows.GroupBy(static row => (row.Id, row.Level))
    .Select(static group => group.Last()).OrderBy(static row => row.Id).ThenBy(static row => row.Level).ToArray();
SkillRecord[] skills = ParseSkills(Required(entries, "Skills.dat"));
ExperienceRecord[] experience = ParseExperience(Required(entries, "ExpNeed.dat"));
PortalRecord[] portals = ParsePortals(ReadLines(Required(entries, "Portals.txt"), 2 * 1024 * 1024, ownerTextEncoding));
StatRecord[] stats = ParseStats(ReadLines(Required(entries, "Stats.txt"), 512 * 1024, ownerTextEncoding));
CompanionRecord[] companions = ParseCompanions(ReadLines(Required(entries, "Companions.txt"), 128 * 1024, ownerTextEncoding));

HashSet<uint> itemIds = items.Select(static row => row.Id).ToHashSet();
HashSet<int> monsterIds = monsters.Select(static row => row.Id).ToHashSet();
HashSet<uint> mapIds = maps.Select(static row => row.Id).ToHashSet();
string[] unresolvedShopItems = shops.Where(row => !itemIds.Contains(row.ItemType))
    .Select(row => row.ItemType.ToString(CultureInfo.InvariantCulture)).Distinct().Order().ToArray();
int[] unresolvedSpawnMonsters = spawns.Where(row => !monsterIds.Contains(row.MonsterId))
    .Select(static row => row.MonsterId).Distinct().Order().ToArray();
uint[] unresolvedSpawnMaps = spawns.Where(row => !mapIds.Contains(row.MapId))
    .Select(static row => row.MapId).Distinct().Order().ToArray();

var categories = new Dictionary<string, object>(StringComparer.Ordinal)
{
    ["maps"] = maps,
    ["dmaps"] = dmaps,
    ["items"] = items,
    ["item-upgrades"] = upgrades,
    ["shops"] = shops,
    ["monsters"] = monsters,
    ["spawns"] = spawns,
    ["drops"] = drops,
    ["equipment-drops"] = equipmentDrops,
    ["magic-types"] = magic,
    ["skills"] = skills,
    ["experience"] = experience,
    ["portals"] = portals,
    ["character-stats"] = stats,
    ["companions"] = companions,
};

var options = new JsonSerializerOptions { WriteIndented = true };
var categoryManifest = new SortedDictionary<string, CategoryManifest>(StringComparer.Ordinal);
foreach ((string name, object rows) in categories)
{
    byte[] canonical = JsonSerializer.SerializeToUtf8Bytes(rows);
    string hash = Convert.ToHexStringLower(SHA256.HashData(canonical));
    int count = rows switch
    {
        System.Collections.ICollection collection => collection.Count,
        _ => throw new InvalidOperationException("A catalog category is not countable."),
    };
    categoryManifest.Add(name, new(count, hash));
    string destination = Path.Combine(outputPath, name + ".reviewed.json");
    await File.WriteAllBytesAsync(destination, JsonSerializer.SerializeToUtf8Bytes(rows, options));
    CryptographicOperations.ZeroMemory(canonical);
}

var manifest = new
{
    schemaVersion = 1,
    parserVersion = ParserVersion,
    sourceFilename = "ReVora-Essentials.zip",
    sourceSha256 = archiveHash,
    sourceBytes = new FileInfo(archivePath).Length,
    classification = "owner-authorized-gameplay-reference-intake",
    categories = categoryManifest,
    integrity = new
    {
        declaredItems = 8405,
        parsedItemRows = itemRows.Count,
        uniqueItems = items.Length,
        duplicateItemRows = itemRows.Count - items.Length,
        parsedMagicRows = magicRows.Length,
        uniqueMagicLevels = magic.Length,
        replacedMagicRows = magicRows.Length - magic.Length,
        missingMapAssets,
        unresolvedShopItems,
        unresolvedSpawnMonsters,
        unresolvedSpawnMaps,
    },
    exclusions = new[]
    {
        "Config.ini: contains legacy plaintext secrets",
        "Users/Characters/Granger.chr: historical player state",
        "Navicat.rar: installer/keygen bundle and nested archive",
        "MySql.Data.dll: compiled third-party binary",
        "granger.sql identity/history/payment/network rows",
    },
    artifacts,
};
await File.WriteAllBytesAsync(Path.Combine(outputPath, "manifest.reviewed.json"),
    JsonSerializer.SerializeToUtf8Bytes(manifest, options));

Console.WriteLine(JsonSerializer.Serialize(new
{
    archiveHash,
    maps = maps.Count,
    dmaps = dmaps.Length,
    items = items.Length,
    upgrades = upgrades.Length,
    shops = shops.Length,
    monsters = monsters.Length,
    spawns = spawns.Length,
    drops = drops.Length,
    magic = magic.Length,
    skills = skills.Length,
    experience = experience.Length,
    portals = portals.Length,
    stats = stats.Length,
    companions = companions.Length,
    missingMapAssets = missingMapAssets.Length,
    unresolvedShopItems = unresolvedShopItems.Length,
    unresolvedSpawnMonsters = unresolvedSpawnMonsters.Length,
    unresolvedSpawnMaps = unresolvedSpawnMaps.Length,
}));
return 0;

static int Fail(string message) { Console.Error.WriteLine(message); return 1; }
static int SelfTest()
{
    if (Percentage("1,5") != 1.5m || Percentage("0.3") != 0.3m)
        return Fail("Locale-independent percentage parsing failed.");
    if (NormalizeEntryName("map/map/newplain.DMap") != "map/map/newplain.DMap" ||
        Classification("Config.ini") != "rejected-secret" || Classification("Navicat.rar") != "rejected-binary")
        return Fail("Path or exclusion classification failed.");
    try { _ = NormalizeEntryName("../unsafe"); return Fail("An unsafe relative path was accepted."); }
    catch (InvalidDataException) { }
    try { _ = Percentage("101"); return Fail("An out-of-range percentage was accepted."); }
    catch (InvalidDataException) { }
    Console.WriteLine("Revora.OwnerEssentialsImport: bounded parsing and exclusion self-test passed.");
    return 0;
}
static bool FixedHashEquals(string first, string second) =>
    CryptographicOperations.FixedTimeEquals(Convert.FromHexString(first), Convert.FromHexString(second));

static string NormalizeEntryName(string value)
{
    if (string.IsNullOrWhiteSpace(value) || value.IndexOf('\0') >= 0 || value.Contains('\\'))
        throw new InvalidDataException("An archive path is malformed.");
    if (value.StartsWith('/') || Path.IsPathRooted(value)) throw new InvalidDataException("An archive path is rooted.");
    string[] parts = value.Split('/', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Any(static part => part is "." or ".." || part.Any(char.IsControl)))
        throw new InvalidDataException("An archive path is unsafe.");
    return string.Join('/', parts);
}

static ZipArchiveEntry Required(IReadOnlyDictionary<string, ZipArchiveEntry> entries, string name) =>
    entries.TryGetValue(name, out ZipArchiveEntry? entry) ? entry : throw new InvalidDataException($"Required {name} is absent.");

static string[] ReadLines(ZipArchiveEntry entry, int maximumBytes, Encoding encoding)
{
    if (entry.Length > maximumBytes) throw new InvalidDataException($"{entry.FullName} exceeds its bounded size.");
    using Stream stream = entry.Open();
    using var reader = new StreamReader(stream, encoding, false, 4096, leaveOpen: false);
    var lines = new List<string>();
    while (reader.ReadLine() is { } line)
    {
        if (line.Length > 4096) throw new InvalidDataException($"{entry.FullName} has an oversized line.");
        lines.Add(line.Trim());
    }
    return lines.ToArray();
}

static string Classification(string name)
{
    if (name.Equals("Navicat.rar", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) return "rejected-binary";
    if (name.Equals("Config.ini", StringComparison.OrdinalIgnoreCase)) return "rejected-secret";
    if (name.StartsWith("Users/", StringComparison.OrdinalIgnoreCase)) return "rejected-player-state";
    if (name.EndsWith(".dmap", StringComparison.OrdinalIgnoreCase)) return "dmap";
    if (name.EndsWith(".scene", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".part", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".pul", StringComparison.OrdinalIgnoreCase)) return "map-resource";
    return "reviewed-candidate";
}

static List<MapRecord> ParseMaps(ZipArchiveEntry entry)
{
    if (entry.Length > 1024 * 1024) throw new InvalidDataException("GameMap.dat is oversized.");
    using MemoryStream stream = ReadBinaryEntry(entry);
    using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: false);
    uint count = reader.ReadUInt32();
    if (count is 0 or > 10_000) throw new InvalidDataException("GameMap.dat count is invalid.");
    var rows = new List<MapRecord>(checked((int)count));
    for (uint index = 0; index < count; index++)
    {
        uint id = reader.ReadUInt32();
        int length = reader.ReadInt32();
        if (id == 0 || length is <= 0 or > 260) throw new InvalidDataException("GameMap.dat record is invalid.");
        string path = NormalizeEntryName(Encoding.ASCII.GetString(reader.ReadBytes(length)));
        uint flags = reader.ReadUInt32();
        rows.Add(new(id, path, flags));
    }
    if (stream.Position != stream.Length) throw new InvalidDataException("GameMap.dat has trailing data.");
    return rows;
}

static DMapRecord ParseDMap(ZipArchiveEntry entry, ArtifactRecord artifact)
{
    const int headerLength = 276;
    if (entry.Length < headerLength || entry.Length > 64L * 1024 * 1024)
        throw new InvalidDataException($"{entry.FullName} has an invalid DMap size.");
    using Stream stream = entry.Open();
    using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: false);
    byte[] header = reader.ReadBytes(headerLength);
    if (header.Length != headerLength) throw new InvalidDataException($"{entry.FullName} has a truncated DMap header.");
    string format = BinaryPrimitives.ReadUInt32LittleEndian(header) switch
    {
        1003 => "numeric-1003",
        1004 => "numeric-1004",
        _ when header.AsSpan(0, 8).SequenceEqual("DMAP100\0"u8) => "dmap-100",
        _ when header.AsSpan(0, 8).SequenceEqual("DMAP101\0"u8) => "dmap-101",
        _ => throw new InvalidDataException($"{entry.FullName} has an unsupported DMap header."),
    };
    uint rawWidth = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(268));
    uint rawHeight = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(272));
    if (rawWidth is 0 or > 4096 || rawHeight is 0 or > 4096 || (ulong)rawWidth * rawHeight > 16_777_216)
        throw new InvalidDataException($"{entry.FullName} has invalid DMap dimensions.");
    int width = checked((int)rawWidth), height = checked((int)rawHeight);
    for (int y = 0; y < height; y++)
    {
        uint checksum = 0;
        for (int x = 0; x < width; x++)
        {
            ushort mask = reader.ReadUInt16();
            ushort terrain = reader.ReadUInt16();
            short altitude = reader.ReadInt16();
            unchecked { checksum += (uint)(mask * (terrain + y + 1) + ((altitude + 2) * (x + 1 + terrain))); }
        }
        if (reader.ReadUInt32() != checksum) throw new InvalidDataException($"{entry.FullName} has an invalid DMap row checksum.");
    }
    return new(artifact.Name, artifact.Length, artifact.Sha256, format, width, height);
}

static List<ItemRecord> ParseItems(IEnumerable<string> lines)
{
    var rows = new List<ItemRecord>();
    foreach (string line in lines.Skip(1).Where(static value => value.Length != 0 && !value.StartsWith("//", StringComparison.Ordinal)))
    {
        string[] value = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (value.Length < 37) throw new InvalidDataException("An item definition is truncated.");
        rows.Add(new(U32(value[0]), SafeText(value[1], 64), U8(value[2]), U8(value[3]), U8(value[4]), U8(value[5]),
            U16(value[6]), U16(value[7]), U32(value[12]), U16(value[14]), U16(value[15]), U32(value[16]),
            U8(value[17]), U8(value[18]), U16(value[19]), U16(value[20]), U16(value[22]), U32(value[29]), U32(value[30]), U32(value[36])));
    }
    return rows;
}

static UpgradeRecord[] ParseUpgrades(IEnumerable<string> lines) => lines.Where(static line => line.Length != 0).Select(line =>
{
    string[] value = Tokens(line, 10);
    return new UpgradeRecord(U32(value[0]), U8(value[1]), U16(value[2]), U32(value[3]), U32(value[4]),
        U16(value[5]), U16(value[6]), U16(value[7]), U16(value[8]), U8(value[9]));
}).Distinct().OrderBy(static row => row.ItemType).ThenBy(static row => row.Plus).ToArray();

static ShopRecord[] ParseShops(IEnumerable<string> lines)
{
    var rows = new List<ShopRecord>();
    uint? shop = null; byte currency = 0; int ordinal = 0;
    foreach (string line in lines)
    {
        if (line.StartsWith("[Shop", StringComparison.Ordinal) && line.EndsWith(']')) { shop = null; currency = 0; ordinal = 0; continue; }
        int separator = line.IndexOf('=');
        if (separator <= 0) continue;
        string key = line[..separator]; string value = line[(separator + 1)..];
        if (key == "ID") shop = U32(value);
        else if (key == "MoneyType") currency = U8(value);
        else if (key.StartsWith("Item", StringComparison.Ordinal) && key != "ItemAmount")
        {
            if (shop is null) throw new InvalidDataException("A shop item precedes its shop ID.");
            rows.Add(new(shop.Value, ordinal++, U32(value), currency));
        }
    }
    return rows.OrderBy(static row => row.ShopId).ThenBy(static row => row.Ordinal).ToArray();
}

static MonsterRecord[] ParseMonsters(IEnumerable<string> lines) => lines.Where(static line => line.Length != 0 && line[0] != '*').Select(line =>
{
    string[] value = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (value.Length is < 21 or > 23) throw new InvalidDataException("A monster definition has an invalid width.");
    bool magic = U8(value[13]) == 21;
    return new MonsterRecord(I32(value[0]), SafeText(value[1], 64), U8(value[2]), U16(value[3]), U8(value[4]), U32(value[5]),
        U16(value[6]), U16(value[7]), U16(value[8]), U16(value[9]), U16(value[10]), U8(value[11]), U8(value[12]), U8(value[13]),
        magic ? U16(value[14]) : (ushort)0, magic ? U8(value[15]) : (byte)0,
        Bool(value[magic ? 16 : 14]), U8(value[magic ? 17 : 15]), I32(value[magic ? 18 : 16]), I32(value[magic ? 19 : 17]),
        U16(value[magic ? 20 : 18]), U32(value[magic ? 21 : 19]), Bool(value[magic ? 22 : 20]));
}).OrderBy(static row => row.Id).ToArray();

static SpawnRecord[] ParseSpawns(IEnumerable<string> lines) => lines.Where(static line => line.Length != 0 && line[0] != '*').Select(line =>
{
    string[] value = Tokens(line, 7);
    int count = I32(value[1]);
    ushort x1 = U16(value[3]), y1 = U16(value[4]), x2 = U16(value[5]), y2 = U16(value[6]);
    if (count is <= 0 or > 10_000) throw new InvalidDataException("A spawn count is invalid.");
    return new SpawnRecord(I32(value[0]), count, U32(value[2]), Math.Min(x1, x2), Math.Min(y1, y2), Math.Max(x1, x2), Math.Max(y1, y2));
}).ToArray();

static DropRecord[] ParseDrops(IEnumerable<string> lines)
{
    var rows = new List<DropRecord>(); int monster = 0;
    foreach (string line in lines.Where(static value => value.Length != 0))
    {
        string[] value = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (value.Length == 1) { monster = I32(value[0]); continue; }
        if (monster <= 0 || value.Length != 5) throw new InvalidDataException("A drop rule is malformed.");
        rows.Add(new(monster, U32(value[0]), U8(value[1]), U8(value[2]), U8(value[3]), Percentage(value[4])));
    }
    return rows.ToArray();
}

static EquipmentDropRecord[] ParseEquipmentDrops(IEnumerable<string> lines) => lines.Where(static line => line.Length != 0).Select(line =>
{
    string[] value = Tokens(line, 2); return new EquipmentDropRecord(U8(value[0]), U32(value[1]));
}).ToArray();

static MagicRecord[] ParseMagic(IEnumerable<string> lines) => lines.Where(static line => line.Length != 0).Select(line =>
{
    string[] value = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (value.Length < 40) throw new InvalidDataException("A magic definition is truncated.");
    return new MagicRecord(U16(value[0]), U8(value[7]), U8(value[1]), SafeText(value[2], 64),
        U32(value[8]), I32(value[9]), U8(value[11]), U32(value[17]), U32(value[18]), U8(value[19]), value.Length);
}).ToArray();

static SkillRecord[] ParseSkills(ZipArchiveEntry entry)
{
    if (entry.Length > 2 * 1024 * 1024) throw new InvalidDataException("Skills.dat is oversized.");
    using MemoryStream stream = ReadBinaryEntry(entry); using var reader = new BinaryReader(stream);
    int count = reader.ReadInt32(); if (count is < 0 or > 100_000) throw new InvalidDataException("Skills.dat count is invalid.");
    var rows = new SkillRecord[count];
    for (int index = 0; index < count; index++)
        rows[index] = new(reader.ReadUInt16(), reader.ReadByte(), reader.ReadUInt16(), reader.ReadByte(), reader.ReadByte(), reader.ReadBoolean(),
            reader.ReadByte(), reader.ReadUInt32(), reader.ReadUInt32(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadUInt16(),
            reader.ReadSingle(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
    if (stream.Position != stream.Length) throw new InvalidDataException("Skills.dat has trailing data.");
    return rows;
}

static ExperienceRecord[] ParseExperience(ZipArchiveEntry entry)
{
    if (entry.Length == 0 || entry.Length % 4 != 0 || entry.Length > 4096) throw new InvalidDataException("ExpNeed.dat length is invalid.");
    using MemoryStream stream = ReadBinaryEntry(entry); using var reader = new BinaryReader(stream);
    var rows = new List<ExperienceRecord>();
    for (int level = 1; stream.Position < stream.Length; level++) rows.Add(new(level, reader.ReadUInt32()));
    return rows.ToArray();
}

static MemoryStream ReadBinaryEntry(ZipArchiveEntry entry)
{
    if (entry.Length > int.MaxValue) throw new InvalidDataException($"{entry.FullName} exceeds the binary reader limit.");
    byte[] bytes = GC.AllocateUninitializedArray<byte>(checked((int)entry.Length));
    using Stream source = entry.Open();
    source.ReadExactly(bytes);
    if (source.ReadByte() != -1) throw new InvalidDataException($"{entry.FullName} exceeded its declared length.");
    return new MemoryStream(bytes, writable: false);
}

static PortalRecord[] ParsePortals(IEnumerable<string> lines) => lines.Where(static line => line.Length != 0).Select(line =>
{
    string[] value = Tokens(line, 6);
    return new PortalRecord(U32(value[0]), U16(value[1]), U16(value[2]), U32(value[3]), U16(value[4]), U16(value[5]));
}).Distinct().ToArray();

static StatRecord[] ParseStats(IEnumerable<string> lines) => lines
    .Where(static line => line.Length != 0 && line[0] != '[')
    .Select(line =>
    {
        int open = line.IndexOf('['), close = line.IndexOf(']'), equals = line.IndexOf('=');
        if (open <= 0 || close <= open + 1 || equals != close + 1)
            throw new InvalidDataException("A character-stat definition is malformed.");
        string className = SafeText(line[..open], 24);
        int level = I32(line[(open + 1)..close]);
        string[] values = Tokens(line[(equals + 1)..].Replace(',', ' '), 4);
        if (level is < 1 or > 255) throw new InvalidDataException("A character-stat level is invalid.");
        return new StatRecord(className, level, U16(values[0]), U16(values[1]), U16(values[2]), U16(values[3]));
    }).OrderBy(static row => row.ClassName, StringComparer.Ordinal).ThenBy(static row => row.Level).ToArray();

static CompanionRecord[] ParseCompanions(IEnumerable<string> lines) => lines.Where(static line => line.Length != 0).Select(line =>
{
    string[] value = Tokens(line, 10);
    return new CompanionRecord(U32(value[0]), U32(value[1]), U32(value[2]), U8(value[3]), U32(value[4]), U16(value[5]),
        U32(value[6]), SafeText(value[7], 64), U8(value[8]), U16(value[9]));
}).OrderBy(static row => row.Type).ToArray();

static string[] Tokens(string line, int expected)
{
    string[] values = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    return values.Length == expected ? values : throw new InvalidDataException($"A record expected {expected} fields but had {values.Length}.");
}
static string SafeText(string value, int maximum) => value.Length is > 0 && value.Length <= maximum && !value.Any(char.IsControl)
    ? value : throw new InvalidDataException("A reference text value is invalid.");
static byte U8(string value) => byte.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture);
static ushort U16(string value) => ushort.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture);
static uint U32(string value) => uint.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture);
static int I32(string value) => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
static bool Bool(string value) => bool.Parse(value);
static decimal Percentage(string value)
{
    decimal parsed = decimal.Parse(value.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
    return parsed is >= 0 and <= 100 ? parsed : throw new InvalidDataException("A percentage is outside its valid range.");
}

internal sealed record ArtifactRecord(string Name, long Length, string Sha256, string Classification);
internal sealed record CategoryManifest(int RowCount, string ContentSha256);
internal sealed record MapRecord(uint Id, string Path, uint Flags);
internal sealed record DMapRecord(string Path, long Length, string Sha256, string Format, int Width, int Height);
internal sealed record ItemRecord(uint Id, string Name, byte Class, byte Profession, byte Level, byte Gender, ushort Strength, ushort Agility,
    uint SilverPrice, ushort MaximumAttack, ushort MinimumAttack, uint Defense, byte Accuracy, byte Dodge, ushort Health, ushort Mana,
    ushort Durability, uint MagicAttack, uint MagicDefense, uint ConquerPointPrice);
internal sealed record UpgradeRecord(uint ItemType, byte Plus, ushort Health, uint MinimumAttack, uint MaximumAttack, ushort Defense,
    ushort MagicAttack, ushort MagicDefense, ushort Accuracy, byte Dodge);
internal sealed record ShopRecord(uint ShopId, int Ordinal, uint ItemType, byte CurrencyType);
internal sealed record MonsterRecord(int Id, string Name, byte Behavior, ushort Mesh, byte Level, uint MaximumHealth, ushort Defense,
    ushort MagicDefense, ushort MagicAttack, ushort MinimumAttack, ushort MaximumAttack, byte DamageReduction, byte Dodge, byte AttackType,
    ushort MagicSkill, byte MagicLevel, bool GivesRewards, byte AttackDistance, int MinimumSilver, int MaximumSilver,
    ushort MoveDelay, uint SpawnDelay, bool LevelDifferenceDamage);
internal sealed record SpawnRecord(int MonsterId, int Count, uint MapId, ushort MinimumX, ushort MinimumY, ushort MaximumX, ushort MaximumY);
internal sealed record DropRecord(int MonsterId, uint ItemType, byte Plus, byte Sockets, byte Bless, decimal ChancePercent);
internal sealed record EquipmentDropRecord(byte Level, uint ItemType);
internal sealed record MagicRecord(ushort Id, byte Level, byte Sort, string Name, uint Damage, int Mana, byte ActivationChance,
    uint RequiredExperience, uint RequiredLevelValue, byte RequiredLevel, int SourceFieldCount);
internal sealed record SkillRecord(ushort Id, byte Level, ushort ManaCost, byte StaminaCost, byte ArrowCost, bool EndsXpWait,
    byte UpgradeLevel, uint UpgradeExperience, uint Damage, byte TargetType, byte DamageType, byte ExtraEffect,
    ushort EffectDuration, float EffectValue, byte ActivationChance, byte MaximumDistance, byte SectorSize);
internal sealed record ExperienceRecord(int Level, uint RequiredExperience);
internal sealed record PortalRecord(uint SourceMap, ushort SourceX, ushort SourceY, uint DestinationMap, ushort DestinationX, ushort DestinationY);
internal sealed record StatRecord(string ClassName, int Level, ushort Strength, ushort Agility, ushort Vitality, ushort Spirit);
internal sealed record CompanionRecord(uint Type, uint MinimumAttack, uint MaximumAttack, byte Level, uint SkillUses, ushort Health,
    uint Mesh, string Name, byte Dodge, ushort Defense);
