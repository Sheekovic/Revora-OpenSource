using System.Security.Cryptography;
using System.Text.Json;

if (args.Length < 3)
{
    Console.Error.WriteLine("Usage: PlayRevora.ManifestGenerator <client-root> <output.json> <version> [excluded-relative-path ...]");
    return 2;
}

string root = Path.GetFullPath(args[0]).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
string output = Path.GetFullPath(args[1]);
string version = NormalizeVersion(args[2]);
if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
if (output.StartsWith(root, StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("Write the manifest outside the client root being scanned.");

var exclusions = new HashSet<string>(args.Skip(3).Select(NormalizeRelative), StringComparer.OrdinalIgnoreCase);
var enumeration = new EnumerationOptions
{
    RecurseSubdirectories = true,
    IgnoreInaccessible = false,
    AttributesToSkip = FileAttributes.ReparsePoint,
    ReturnSpecialDirectories = false
};

var entries = new List<ManifestEntry>();
foreach (string file in Directory.EnumerateFiles(root, "*", enumeration)
    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
{
    string relative = NormalizeRelative(Path.GetRelativePath(root, file));
    if (exclusions.Contains(relative)) continue;
    FileInfo before = new(file);
    string hash;
    await using (FileStream stream = new(file, FileMode.Open, FileAccess.Read, FileShare.Read))
        hash = Convert.ToHexString(await SHA256.HashDataAsync(stream));
    FileInfo after = new(file);
    if (before.Length != after.Length || before.LastWriteTimeUtc != after.LastWriteTimeUtc)
        throw new IOException($"File changed while hashing: {relative}");
    entries.Add(new ManifestEntry(relative, "baseline/" + relative, after.Length, hash));
}

var manifest = new MasterManifest(1, version, entries);
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
string temporary = output + ".tmp." + Guid.NewGuid().ToString("N");
await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(manifest, new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true
}));
File.Move(temporary, output, true);
Console.WriteLine($"Generated {entries.Count} entries ({entries.Sum(entry => entry.Size):N0} bytes) at {output}");
return 0;

static string NormalizeRelative(string path)
{
    if (Path.IsPathRooted(path)) throw new InvalidDataException("Exclusions must be relative paths.");
    string normalized = path.Replace('\\', '/').TrimStart('/');
    if (normalized.Split('/').Any(segment => segment is "" or "." or ".."))
        throw new InvalidDataException($"Unsafe relative path: {path}");
    return normalized;
}

static string NormalizeVersion(string value)
{
    string[] parts = value.Trim().TrimStart('v', 'V').Split('.');
    if (parts.Length is < 1 or > 3 || parts.Any(part => !int.TryParse(part, out int number) || number < 0))
        throw new InvalidDataException($"Invalid semantic version: {value}");
    return string.Join('.', parts.Concat(Enumerable.Repeat("0", 3 - parts.Length)));
}

internal sealed record MasterManifest(int SchemaVersion, string Version, IReadOnlyList<ManifestEntry> Files);
internal sealed record ManifestEntry(string Path, string Source, long Size, string Sha256);
