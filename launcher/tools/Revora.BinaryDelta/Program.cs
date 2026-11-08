using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

const int HeaderLength = 96;
const long MaximumImageSize = 64L * 1024 * 1024;
static ReadOnlySpan<byte> Magic() => "RVDLT001"u8;

try
{
    if (args.Length == 4 && args[0] == "create") Create(args[1], args[2], args[3]);
    else if (args.Length == 4 && args[0] == "apply") Transform(args[1], args[2], args[3], reverse: false);
    else if (args.Length == 4 && args[0] == "reverse") Transform(args[1], args[2], args[3], reverse: true);
    else if (args.Length == 4 && args[0] == "verify-package") VerifyPackage(args[1], args[2], args[3]);
    else if (args.Length == 1 && args[0] == "self-test") SelfTest();
    else return Usage();
}
catch (Exception exception) when (exception is IOException or InvalidDataException or CryptographicException
                                   or UnauthorizedAccessException or FormatException or JsonException or InvalidOperationException)
{
    Console.Error.WriteLine($"Revora delta operation failed: {exception.Message}");
    return 1;
}
return 0;

static int Usage()
{
    Console.Error.WriteLine("Revora.BinaryDelta create <source> <target> <delta>");
    Console.Error.WriteLine("Revora.BinaryDelta apply <delta> <source> <output>");
    Console.Error.WriteLine("Revora.BinaryDelta reverse <delta> <target> <output>");
    Console.Error.WriteLine("Revora.BinaryDelta verify-package <directory> <key-id> <public-key-spki-base64>");
    Console.Error.WriteLine("Revora.BinaryDelta self-test");
    return 2;
}

static void Create(string sourcePath, string targetPath, string deltaPath)
{
    byte[] source = ReadBounded(sourcePath);
    byte[] target = ReadBounded(targetPath);
    int length = Math.Max(source.Length, target.Length);
    byte[] xor = new byte[length];
    for (int index = 0; index < length; index++)
        xor[index] = (byte)((index < source.Length ? source[index] : 0) ^ (index < target.Length ? target[index] : 0));

    using var compressed = new MemoryStream();
    using (var brotli = new BrotliStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true)) brotli.Write(xor);
    byte[] payload = compressed.ToArray();
    byte[] header = new byte[HeaderLength];
    Magic().CopyTo(header);
    BinaryPrimitives.WriteInt64LittleEndian(header.AsSpan(8), source.Length);
    BinaryPrimitives.WriteInt64LittleEndian(header.AsSpan(16), target.Length);
    SHA256.HashData(source).CopyTo(header, 24);
    SHA256.HashData(target).CopyTo(header, 56);
    BinaryPrimitives.WriteInt64LittleEndian(header.AsSpan(88), payload.Length);
    WriteAtomic(deltaPath, stream => { stream.Write(header); stream.Write(payload); });
    CryptographicOperations.ZeroMemory(xor);
    Console.WriteLine($"Created deterministic Revora delta: {source.Length} -> {target.Length} bytes; payload={payload.Length}.");
}

static void Transform(string deltaPath, string inputPath, string outputPath, bool reverse)
{
    byte[] delta = ReadBounded(deltaPath);
    if (delta.Length < HeaderLength || !delta.AsSpan(0, 8).SequenceEqual(Magic())) throw new InvalidDataException("Invalid delta header.");
    long sourceLength = BinaryPrimitives.ReadInt64LittleEndian(delta.AsSpan(8));
    long targetLength = BinaryPrimitives.ReadInt64LittleEndian(delta.AsSpan(16));
    ValidateLength(sourceLength); ValidateLength(targetLength);
    long payloadLength = BinaryPrimitives.ReadInt64LittleEndian(delta.AsSpan(88));
    if (payloadLength <= 0 || payloadLength != delta.LongLength - HeaderLength) throw new InvalidDataException("Invalid delta payload length.");
    ReadOnlySpan<byte> expectedInputHash = reverse ? delta.AsSpan(56, 32) : delta.AsSpan(24, 32);
    ReadOnlySpan<byte> expectedOutputHash = reverse ? delta.AsSpan(24, 32) : delta.AsSpan(56, 32);
    long expectedInputLength = reverse ? targetLength : sourceLength;
    int outputLength = checked((int)(reverse ? sourceLength : targetLength));
    byte[] input = ReadBounded(inputPath);
    if (input.LongLength != expectedInputLength || !SHA256.HashData(input).AsSpan().SequenceEqual(expectedInputHash))
        throw new InvalidDataException("Input identity does not match the hash-gated delta contract.");
    int xorLength = checked((int)Math.Max(sourceLength, targetLength));
    byte[] xor = new byte[xorLength];
    using (var compressed = new MemoryStream(delta, HeaderLength, checked((int)payloadLength), writable: false))
    using (var brotli = new BrotliStream(compressed, CompressionMode.Decompress))
    {
        int offset = 0;
        while (offset < xor.Length)
        {
            int read = brotli.Read(xor, offset, xor.Length - offset);
            if (read == 0) throw new InvalidDataException("Delta payload ended early.");
            offset += read;
        }
        if (brotli.ReadByte() != -1) throw new InvalidDataException("Delta payload exceeds declared output bounds.");
    }
    byte[] output = new byte[outputLength];
    for (int index = 0; index < output.Length; index++) output[index] = (byte)((index < input.Length ? input[index] : 0) ^ xor[index]);
    if (!SHA256.HashData(output).AsSpan().SequenceEqual(expectedOutputHash)) throw new InvalidDataException("Reconstructed output hash mismatch.");
    WriteAtomic(outputPath, stream => stream.Write(output));
    CryptographicOperations.ZeroMemory(xor);
    Console.WriteLine($"Verified and wrote {output.Length} bytes: {Convert.ToHexString(expectedOutputHash)}");
}

static void VerifyPackage(string directory, string expectedKeyId, string publicKeySpkiBase64)
{
    string root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    string manifestPath = Path.Combine(root, "incremental-manifest.signed.json");
    using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(manifestPath), new() { MaxDepth = 12 });
    JsonElement manifest = document.RootElement;
    if (manifest.GetProperty("schemaVersion").GetInt32() != 1) throw new InvalidDataException("Unsupported package manifest schema.");
    string version = manifest.GetProperty("version").GetString() ?? throw new InvalidDataException("Missing version.");
    JsonElement signature = manifest.GetProperty("signature");
    if (signature.GetProperty("algorithm").GetString() != "ECDSA_P256_SHA256"
        || signature.GetProperty("keyId").GetString() != expectedKeyId) throw new InvalidDataException("Package trust identity mismatch.");
    var files = new List<PackageFile>();
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (JsonElement item in manifest.GetProperty("files").EnumerateArray())
    {
        string relative = item.GetProperty("relativePath").GetString() ?? throw new InvalidDataException("Missing package path.");
        ValidateRelative(relative);
        if (!seen.Add(relative)) throw new InvalidDataException("Duplicate package path.");
        long size = item.GetProperty("size").GetInt64();
        string hash = item.GetProperty("sha256").GetString() ?? throw new InvalidDataException("Missing package hash.");
        if (size < 0 || hash.Length != 64) throw new InvalidDataException("Invalid package entry.");
        string path = ResolveInside(root, relative);
        var info = new FileInfo(path);
        if (!info.Exists || info.Length != size || !Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).Equals(hash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Package file mismatch: {relative}");
        files.Add(new(relative, size, hash.ToUpperInvariant()));
    }
    string[] actual = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
        .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
        .Where(path => !path.Equals("incremental-manifest.signed.json", StringComparison.OrdinalIgnoreCase))
        .OrderBy(path => path, StringComparer.Ordinal).ToArray();
    string[] declared = files.Select(file => file.RelativePath).OrderBy(path => path, StringComparer.Ordinal).ToArray();
    if (!actual.SequenceEqual(declared, StringComparer.Ordinal)) throw new InvalidDataException("Package contains undeclared or missing files.");
    byte[] canonical = JsonSerializer.SerializeToUtf8Bytes(new PackagePayload(1, version, files), PackageJsonOptions());
    byte[] rawSignature = Convert.FromBase64String(signature.GetProperty("valueBase64").GetString() ?? "");
    using ECDsa key = ECDsa.Create();
    key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeySpkiBase64), out int read);
    if (read == 0 || !key.VerifyData(canonical, rawSignature, HashAlgorithmName.SHA256)) throw new CryptographicException("Package signature is invalid.");
    Console.WriteLine($"Verified signed Revora package {version}: {files.Count} files.");
}

static JsonSerializerOptions PackageJsonOptions() => new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

static void SelfTest()
{
    string root = Path.Combine(Path.GetTempPath(), "revora-delta-selftest-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        byte[] source = Enumerable.Range(0, 8193).Select(index => (byte)(index * 17)).ToArray();
        byte[] target = source.Concat(new byte[257]).ToArray();
        for (int index = 111; index < 777; index += 13) target[index] ^= 0xA5;
        string sourcePath = Path.Combine(root, "source.bin"), targetPath = Path.Combine(root, "target.bin");
        File.WriteAllBytes(sourcePath, source); File.WriteAllBytes(targetPath, target);
        string first = Path.Combine(root, "first.delta"), second = Path.Combine(root, "second.delta");
        Create(sourcePath, targetPath, first); Create(sourcePath, targetPath, second);
        if (!File.ReadAllBytes(first).SequenceEqual(File.ReadAllBytes(second))) throw new InvalidDataException("Delta output is not deterministic.");
        string applied = Path.Combine(root, "applied.bin"), reversed = Path.Combine(root, "reversed.bin");
        Transform(first, sourcePath, applied, false); Transform(first, targetPath, reversed, true);
        if (!File.ReadAllBytes(applied).SequenceEqual(target) || !File.ReadAllBytes(reversed).SequenceEqual(source)) throw new InvalidDataException("Delta round trip failed.");
        byte[] tampered = File.ReadAllBytes(first); tampered[^1] ^= 1; File.WriteAllBytes(Path.Combine(root, "tampered.delta"), tampered);
        ExpectFailure(() => Transform(Path.Combine(root, "tampered.delta"), sourcePath, Path.Combine(root, "bad.bin"), false));
        byte[] wrong = source.ToArray(); wrong[0] ^= 1; File.WriteAllBytes(Path.Combine(root, "wrong.bin"), wrong);
        ExpectFailure(() => Transform(first, Path.Combine(root, "wrong.bin"), Path.Combine(root, "wrong-out.bin"), false));
        Console.WriteLine("Revora.BinaryDelta self-test passed: deterministic, reversible, hash-gated, tamper-failing.");
    }
    finally { Directory.Delete(root, recursive: true); }
}

static void ExpectFailure(Action action)
{
    try { action(); }
    catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException) { return; }
    throw new InvalidDataException("Expected a fail-closed operation.");
}

static byte[] ReadBounded(string path)
{
    var info = new FileInfo(Path.GetFullPath(path));
    if (!info.Exists) throw new FileNotFoundException("Required file is missing.");
    ValidateLength(info.Length);
    return File.ReadAllBytes(info.FullName);
}

static void ValidateLength(long length)
{
    if (length < 0 || length > MaximumImageSize) throw new InvalidDataException("File exceeds the bounded delta limit.");
}

static void ValidateRelative(string relative)
{
    if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains(':')) throw new InvalidDataException("Invalid package path.");
    if (relative.Replace('\\', '/').Split('/').Any(part => part is "" or "." or "..")) throw new InvalidDataException("Invalid package path.");
}

static string ResolveInside(string root, string relative)
{
    string path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
    if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Package path escapes its root.");
    for (string? current = path; current is not null && current.StartsWith(root, StringComparison.OrdinalIgnoreCase); current = Path.GetDirectoryName(current))
        if (File.Exists(current) || Directory.Exists(current))
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Package reparse points are forbidden.");
    return path;
}

static void WriteAtomic(string path, Action<FileStream> writer)
{
    string full = Path.GetFullPath(path);
    Directory.CreateDirectory(Path.GetDirectoryName(full)!);
    string temporary = full + ".tmp." + Guid.NewGuid().ToString("N");
    try
    {
        using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { writer(stream); stream.Flush(flushToDisk: true); }
        File.Move(temporary, full, overwrite: false);
    }
    finally { if (File.Exists(temporary)) File.Delete(temporary); }
}

sealed record PackageFile(string RelativePath, long Size, string Sha256);
sealed record PackagePayload(int SchemaVersion, string Version, IReadOnlyList<PackageFile> Files);
