using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

const string ApprovedDumpSha256 = "d912367110f5a18836234060fc43425b7d853ee441dbba461a2d99c90ae42f9c";
const long ApprovedDumpBytes = 1_145_278;
const string ApprovedArchiveSha256 = "f5e88d2729306ca6946356a52b3964f9ddb245db9f1e40a0539e0e126ade3d63";
const long ApprovedArchiveBytes = 109_779;
var schemas = new Dictionary<string, string[]>(StringComparer.Ordinal)
{
    ["portals"] = ["legacyId", "sourceMap", "sourceX", "sourceY", "destinationMap", "destinationX", "destinationY"],
    ["npcs"] = ["legacyUid", "npcType", "flags", "face", "mapId", "x", "y", "activeMonth", "activeBegin", "activeEnd", "destinationMap", "destinationX", "destinationY", "duration"],
    ["customshop"] = ["legacyId", "itemType", "price", "currencyType"],
    ["itemadd"] = ["legacyUniqueId", "baseItemType", "plus", "health", "minimumDamage", "maximumDamage", "defense", "magicDamage", "magicDefense", "accuracy", "dodge"],
};

if (args is ["--self-test"])
    return SelfTest(schemas);
if (args.Length != 2)
    return Fail("Usage: Revora.OwnerGameplayImport <reviewed-revora.sql> <output-directory>");
string input = Path.GetFullPath(args[0]);
string output = Path.GetFullPath(args[1]);
if (!File.Exists(input) || (File.GetAttributes(input) & FileAttributes.ReparsePoint) != 0)
    return Fail("The reviewed input is missing or unsafe.");
string actualHash;
await using (Stream stream = OpenReviewedSql(input))
    actualHash = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream));
if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(actualHash), Convert.FromHexString(ApprovedDumpSha256)))
    return Fail("The input hash is not the approved owner database export.");
Directory.CreateDirectory(output);
if ((File.GetAttributes(output) & FileAttributes.ReparsePoint) != 0)
    return Fail("The output directory is unsafe.");
if (Directory.EnumerateFileSystemEntries(output).Any())
    return Fail("The output directory must be empty.");

var rows = schemas.Keys.ToDictionary(key => key, _ => new List<Dictionary<string, long>>(), StringComparer.Ordinal);
await using (Stream sql = OpenReviewedSql(input))
using (var reader = new StreamReader(sql))
{
    while (await reader.ReadLineAsync() is { } line)
    {
        try
        {
            Dictionary<string, long>? row = ParseAllowedInsert(line, schemas, out string? table);
            if (row is not null) rows[table!].Add(row);
        }
        catch (InvalidDataException error) { return Fail(error.Message); }
    }
}

var options = new JsonSerializerOptions { WriteIndented = true };
var categoryManifest = new SortedDictionary<string, object>(StringComparer.Ordinal);
foreach ((string table, List<Dictionary<string, long>> data) in rows)
{
    string destination = Path.Combine(output, table + ".reviewed.json");
    await using FileStream stream = new(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
    await JsonSerializer.SerializeAsync(stream, data, options);
    categoryManifest.Add(table, new
    {
        rowCount = data.Count,
        contentSha256 = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(data))),
    });
}
var manifest = new
{
    schemaVersion = 1,
    parserVersion = 1,
    sourceFilename = "revora.sql",
    sourceSha256 = actualHash,
    sourceBytes = ApprovedDumpBytes,
    archiveFilename = "revora.zip",
    archiveSha256 = ApprovedArchiveSha256,
    archiveBytes = ApprovedArchiveBytes,
    classification = "reviewed-gameplay-reference-data-only",
    categories = categoryManifest,
    exclusions = new[]
    {
        "accounts-and-passwords", "historical-characters-and-inventories", "ip-payment-voting-vip-and-audit-history",
        "guild-house-ranking-and-private-message-state", "raw-sql-execution",
    },
};
await File.WriteAllTextAsync(Path.Combine(output, "manifest.reviewed.json"), JsonSerializer.Serialize(manifest, options));
Console.WriteLine("Reviewed gameplay/reference extraction completed; identity and historical state tables were not parsed.");
return 0;

static int Fail(string message) { Console.Error.WriteLine(message); return 1; }

static Stream OpenReviewedSql(string path)
{
    FileStream file = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    if (Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase) || IsZip(file))
    {
        var archive = new ZipArchive(file, ZipArchiveMode.Read, leaveOpen: false);
        ZipArchiveEntry[] files = archive.Entries.Where(entry => !string.IsNullOrEmpty(entry.Name)).ToArray();
        if (files.Length != 1 || !files[0].FullName.Equals("revora.sql", StringComparison.Ordinal) ||
            files[0].Length is <= 0 or > 64 * 1024 * 1024 || files[0].CompressedLength is <= 0)
        {
            archive.Dispose();
            throw new InvalidDataException("The reviewed database archive does not contain the one approved SQL entry.");
        }
        return new ArchiveEntryStream(archive, files[0].Open());
    }
    file.Position = 0;
    return file;
}

static bool IsZip(FileStream file)
{
    Span<byte> signature = stackalloc byte[4];
    int read = file.Read(signature);
    file.Position = 0;
    return read == 4 && signature.SequenceEqual(new byte[] { 0x50, 0x4b, 0x03, 0x04 });
}

static Dictionary<string, long>? ParseAllowedInsert(string line,
    IReadOnlyDictionary<string, string[]> schemas, out string? table)
{
    table = null;
    if (!line.StartsWith("INSERT INTO `", StringComparison.Ordinal)) return null;
    int endName = line.IndexOf('`', 13);
    if (endName < 0) return null;
    table = line[13..endName];
    if (!schemas.TryGetValue(table, out string[]? columns)) { table = null; return null; }
    int valuesAt = line.IndexOf(" VALUES (", endName, StringComparison.Ordinal);
    if (valuesAt < 0 || !line.EndsWith(");", StringComparison.Ordinal))
        throw new InvalidDataException($"The approved {table} data contains an unsupported INSERT form.");
    string[] values = line[(valuesAt + 9)..^2].Split(',', StringSplitOptions.TrimEntries);
    if (values.Length != columns.Length)
        throw new InvalidDataException($"The approved {table} row width does not match its reviewed schema.");
    var row = new Dictionary<string, long>(StringComparer.Ordinal);
    for (int index = 0; index < values.Length; index++)
    {
        string token = values[index];
        if (token.Length < 2 || token[0] != '\'' || token[^1] != '\'' ||
            !long.TryParse(token[1..^1], NumberStyles.None, CultureInfo.InvariantCulture, out long value) || value < 0)
            throw new InvalidDataException($"The approved {table} data contains a non-numeric or negative field.");
        row.Add(columns[index], value);
    }
    return row;
}

static int SelfTest(IReadOnlyDictionary<string, string[]> schemas)
{
    Dictionary<string, long>? portal = ParseAllowedInsert(
        "INSERT INTO `portals` VALUES ('1', '1002', '430', '378', '1000', '20', '30');",
        schemas, out string? table);
    if (table != "portals" || portal is null || portal["sourceMap"] != 1002 || portal["destinationY"] != 30)
        return Fail("The allowlisted numeric parser self-test failed.");
    if (ParseAllowedInsert("INSERT INTO `accounts` VALUES ('sensitive');", schemas, out table) is not null || table is not null)
        return Fail("A prohibited identity table crossed the extraction allowlist.");
    try
    {
        _ = ParseAllowedInsert("INSERT INTO `portals` VALUES ('1', 'bad');", schemas, out _);
        return Fail("Malformed allowlisted data was accepted.");
    }
    catch (InvalidDataException) { }
    Console.WriteLine("Revora.OwnerGameplayImport: allowlist, numeric parsing, and prohibited-table self-test passed.");
    return 0;
}

sealed class ArchiveEntryStream(ZipArchive archive, Stream entry) : Stream
{
    public override bool CanRead => entry.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() => entry.Flush();
    public override int Read(byte[] buffer, int offset, int count) => entry.Read(buffer, offset, count);
    public override int Read(Span<byte> buffer) => entry.Read(buffer);
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => entry.ReadAsync(buffer, cancellationToken);
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            entry.Dispose();
            archive.Dispose();
        }
        base.Dispose(disposing);
    }
}
