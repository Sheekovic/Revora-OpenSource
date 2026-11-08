using System.Security.Cryptography;
using System.Text.Json;

if (args.Length != 4 || args[0] is not ("apply" or "reverse" or "verify"))
{
    Console.Error.WriteLine("Usage: Revora.StaticClientPatcher <apply|reverse|verify> <manifest.json> <input> <output>");
    return 2;
}

try
{
    PatchManifest manifest = JsonSerializer.Deserialize<PatchManifest>(File.ReadAllText(args[1]),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidDataException("Patch manifest is empty.");
    manifest.Validate();
    byte[] input = File.ReadAllBytes(args[2]);
    bool reverse = args[0] == "reverse";
    string expectedHash = reverse ? manifest.DestinationSha256 : manifest.SourceSha256;
    if (input.LongLength != manifest.SourceSize || !Hash(input).Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
        throw new InvalidDataException("Input identity is not approved for this operation.");

    byte[] output = (byte[])input.Clone();
    foreach (BinaryPatch patch in manifest.Patches)
    {
        byte[] before = Convert.FromHexString(reverse ? patch.ReplacementHex : patch.OriginalHex);
        byte[] after = Convert.FromHexString(reverse ? patch.OriginalHex : patch.ReplacementHex);
        if (!output.AsSpan(checked((int)patch.FileOffset), before.Length).SequenceEqual(before))
            throw new InvalidDataException($"Patch preimage mismatch at {patch.FileOffset}.");
        after.CopyTo(output, checked((int)patch.FileOffset));
    }

    string resultHash = Hash(output);
    string required = reverse ? manifest.SourceSha256 : manifest.DestinationSha256;
    if (!resultHash.Equals(required, StringComparison.OrdinalIgnoreCase))
        throw new InvalidDataException("Patched output hash does not match the reviewed manifest.");
    if (args[0] != "verify") WriteAtomic(args[3], output);
    Console.WriteLine(resultHash);
    return 0;
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException
                                  or JsonException or ArgumentException or OverflowException)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

static void WriteAtomic(string path, byte[] bytes)
{
    string full = Path.GetFullPath(path);
    Directory.CreateDirectory(Path.GetDirectoryName(full)!);
    string temporary = full + ".revora-new";
    File.WriteAllBytes(temporary, bytes);
    if (File.Exists(full)) File.Replace(temporary, full, null);
    else File.Move(temporary, full);
}

sealed class PatchManifest
{
    public int SchemaVersion { get; set; }
    public string SourceFile { get; set; } = "";
    public string DestinationFile { get; set; } = "";
    public long SourceSize { get; set; }
    public string SourceSha256 { get; set; } = "";
    public string DestinationSha256 { get; set; } = "";
    public List<BinaryPatch> Patches { get; set; } = [];

    public void Validate()
    {
        if (SchemaVersion != 1 || SourceFile != "Conquer.exe" || DestinationFile != "Revora-Conquer.exe"
            || SourceSize != 18594824 || SourceSha256.Length != 64 || DestinationSha256.Length != 64
            || Patches.Count is < 1 or > 256) throw new InvalidDataException("Unsupported Revora client patch manifest.");
        long previousEnd = -1;
        foreach (BinaryPatch patch in Patches.OrderBy(item => item.FileOffset))
        {
            byte[] before = Convert.FromHexString(patch.OriginalHex);
            byte[] after = Convert.FromHexString(patch.ReplacementHex);
            if (before.Length == 0 || before.Length != after.Length || patch.FileOffset < 0
                || patch.FileOffset + before.Length > SourceSize || patch.FileOffset < previousEnd)
                throw new InvalidDataException("Invalid or overlapping patch range.");
            previousEnd = patch.FileOffset + before.Length;
        }
    }
}

sealed class BinaryPatch
{
    public long FileOffset { get; set; }
    public string Rva { get; set; } = "";
    public string OriginalHex { get; set; } = "";
    public string ReplacementHex { get; set; } = "";
    public string Purpose { get; set; } = "";
}
