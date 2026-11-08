using System.Security.Cryptography;
using System.Text.Json;
using Revora.Launcher.Core;

if (args is ["publish-launcher", _, _, _, _, _, _, _, _])
    return await PublishLauncherAsync(args);
if (args is ["verify-publication", _, _, _, _, _])
    return await VerifyPublicationAsync(args);

if (args.Length != 11 || args[0] != "publish")
{
    Console.Error.WriteLine("Usage: publish <client-root> <profile-payload.json> <output-root> <stable|test> <release-guid> <published-utc> <private-key.pem> <key-id> <cdn-base-uri> <origin-base-uri>\n       publish-launcher <publish-dir> <output-root> <stable|test> <release-guid> <published-utc> <private-key.pem> <key-id> <base-uri>\n       verify-publication <manifest.json> <object-root> <public-key.der> <key-id> <launcher-version>");
    return 2;
}

string clientRoot = Path.GetFullPath(args[1]);
string profilePath = Path.GetFullPath(args[2]);
string outputRoot = Path.GetFullPath(args[3]);
string channel = args[4];
Guid releaseId = Guid.Parse(args[5]);
DateTimeOffset published = DateTimeOffset.Parse(args[6], System.Globalization.CultureInfo.InvariantCulture,
    System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal);
string privateKeyPath = Path.GetFullPath(args[7]);
string keyId = args[8];
string baseUri = args[9];
string originUri = args[10];

if (channel is not ("stable" or "test") || releaseId == Guid.Empty || !File.Exists(privateKeyPath))
    throw new InvalidDataException("The publication identity is invalid.");
ClientProfile profile = JsonSerializer.Deserialize<ClientProfile>(await File.ReadAllBytesAsync(profilePath))
    ?? throw new InvalidDataException("The approved profile payload is empty.");
ManifestVerifier.ValidatePolicy(profile);
Directory.CreateDirectory(outputRoot);

var entries = new List<DesiredStateFile>(profile.Files.Count);
long totalBytes = 0;
int objectsCreated = 0;
foreach (ClientFile expected in profile.Files.OrderBy(file => file.Path, StringComparer.Ordinal))
{
    string source = SafePath.ResolveContained(clientRoot, expected.Path);
    SafePath.RejectExistingReparsePoints(clientRoot, source);
    if (!File.Exists(source)) throw new FileNotFoundException("A canonical managed file is missing.", expected.Path);
    var info = new FileInfo(source);
    if (info.Length != expected.Size) throw new InvalidDataException($"Canonical size mismatch: {expected.Path}");
    await using FileStream verify = new(source, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024,
        FileOptions.Asynchronous | FileOptions.SequentialScan);
    string hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(verify));
    if (hash != expected.Sha256) throw new CryptographicException($"Canonical hash mismatch: {expected.Path}");
    string objectPath = $"objects/sha256/{hash[..2]}/{hash}";
    string destination = SafePath.ResolveContained(outputRoot, objectPath);
    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
    if (!File.Exists(destination)) { File.Copy(source, destination, false); objectsCreated++; }
    bool critical = expected.Path.Equals(LauncherPolicy.ClientExecutable, StringComparison.OrdinalIgnoreCase) ||
        expected.Path.Equals("Server.dat", StringComparison.OrdinalIgnoreCase) ||
        expected.Path.Equals("version.dat", StringComparison.OrdinalIgnoreCase) ||
        expected.Path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || expected.Path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
    entries.Add(new(expected.Path, expected.Size, hash, profile.BuildId, objectPath, "none", null,
        expected.Required, critical, "replace", expected.Path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)));
    totalBytes = checked(totalBytes + expected.Size);
}

DesiredStateManifest manifest = new(2, releaseId, profile.BuildId, channel, published, LauncherPolicy.ProductVersion,
    profile.Files.Single(file => file.Path == profile.Executable).Sha256, baseUri, originUri,
    LauncherPolicy.ServerHost, LauncherPolicy.AccountPort, LauncherPolicy.GamePort, null, null, entries);
DesiredStateManifestVerifier.Validate(manifest, Version.Parse(LauncherPolicy.ProductVersion));
byte[] payload = JsonSerializer.SerializeToUtf8Bytes(manifest, new JsonSerializerOptions { WriteIndented = false });
byte[] pem = await File.ReadAllBytesAsync(privateKeyPath);
byte[] signature;
try
{
    using ECDsa signer = ECDsa.Create();
    signer.ImportFromPem(System.Text.Encoding.ASCII.GetString(pem));
    signature = signer.SignData(payload, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
}
finally { CryptographicOperations.ZeroMemory(pem); }
byte[] envelope = JsonSerializer.SerializeToUtf8Bytes(new SignedEnvelope(keyId, "ECDSA-P256-SHA256",
    Convert.ToBase64String(payload), Convert.ToBase64String(signature)));
CryptographicOperations.ZeroMemory(signature);
string manifests = Path.Combine(outputRoot, "manifests", channel, releaseId.ToString("N"));
Directory.CreateDirectory(manifests);
await File.WriteAllBytesAsync(Path.Combine(manifests, "manifest.json"), envelope);
await File.WriteAllTextAsync(Path.Combine(manifests, "release-report.json"), JsonSerializer.Serialize(new
{
    schemaVersion = 1, releaseId, channel, publishedAtUtc = published, profileBuildId = profile.BuildId,
    files = entries.Count, totalBytes, uniqueObjectsCreated = objectsCreated,
    manifestSha256 = Convert.ToHexStringLower(SHA256.HashData(envelope)),
}, new JsonSerializerOptions { WriteIndented = true }));
CryptographicOperations.ZeroMemory(payload);
CryptographicOperations.ZeroMemory(envelope);
Console.WriteLine($"Published {entries.Count} managed files ({totalBytes} bytes); {objectsCreated} new objects.");
return 0;

static async Task<int> PublishLauncherAsync(string[] values)
{
    string sourceRoot = Path.GetFullPath(values[1]), outputRoot = Path.GetFullPath(values[2]), channel = values[3];
    Guid releaseId = Guid.Parse(values[4]);
    DateTimeOffset published = DateTimeOffset.Parse(values[5], System.Globalization.CultureInfo.InvariantCulture,
        System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal);
    string privateKeyPath = Path.GetFullPath(values[6]), keyId = values[7];
    Uri baseUri = new(values[8].EndsWith('/') ? values[8] : values[8] + '/', UriKind.Absolute);
    if (channel is not ("stable" or "test") || releaseId == Guid.Empty || baseUri.Scheme != Uri.UriSchemeHttps ||
        baseUri.Host != LauncherPolicy.ServerHost || !Directory.Exists(sourceRoot))
        throw new InvalidDataException("The launcher publication identity is invalid.");
    string[] allowedExtensions = [".exe", ".dll", ".json", ".png", ".ico"];
    FileInfo[] sources = new DirectoryInfo(sourceRoot).EnumerateFiles("*", SearchOption.AllDirectories)
        .Where(file => allowedExtensions.Contains(file.Extension, StringComparer.OrdinalIgnoreCase) &&
            !file.Name.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)).OrderBy(file => file.FullName, StringComparer.Ordinal).ToArray();
    if (sources.Length is 0 or > 1000 || !sources.Any(file => file.Name == "Revora.Launcher.exe") ||
        !sources.Any(file => file.Name == "Revora.Bootstrapper.exe"))
        throw new InvalidDataException("The launcher publication is incomplete.");
    var files = new List<LauncherReleaseFile>(sources.Length);
    foreach (FileInfo source in sources)
    {
        string relative = Path.GetRelativePath(sourceRoot, source.FullName).Replace(Path.DirectorySeparatorChar, '/');
        if (!SafePath.IsNormalizedRelative(relative)) throw new InvalidDataException("A launcher release path is unsafe.");
        await using FileStream input = source.OpenRead(); string hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(input));
        string objectPath = $"objects/sha256/{hash[..2]}/{hash}";
        string destination = SafePath.ResolveContained(outputRoot, Path.Combine("launcher", objectPath).Replace(Path.DirectorySeparatorChar, '/'));
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!); if (!File.Exists(destination)) File.Copy(source.FullName, destination);
        files.Add(new(relative, source.Length, hash, new Uri(baseUri, objectPath).AbsoluteUri,
            relative is "Revora.Launcher.exe" or "Revora.Launcher.dll" or "Revora.Launcher.Core.dll"));
    }
    var manifest = new LauncherReleaseManifest(1, releaseId, channel, LauncherPolicy.ProductVersion, published,
        "Revora.Launcher.exe", files);
    LauncherReleaseVerifier.Validate(manifest);
    byte[] payload = JsonSerializer.SerializeToUtf8Bytes(manifest);
    byte[] keyBytes = await File.ReadAllBytesAsync(privateKeyPath); byte[] signature;
    try { using ECDsa signer = ECDsa.Create(); signer.ImportFromPem(System.Text.Encoding.ASCII.GetString(keyBytes)); signature = signer.SignData(payload, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence); }
    finally { CryptographicOperations.ZeroMemory(keyBytes); }
    byte[] envelope = JsonSerializer.SerializeToUtf8Bytes(new SignedEnvelope(keyId, "ECDSA-P256-SHA256", Convert.ToBase64String(payload), Convert.ToBase64String(signature)));
    CryptographicOperations.ZeroMemory(signature); CryptographicOperations.ZeroMemory(payload);
    string directory = Path.Combine(outputRoot, "launcher", "manifests", channel, releaseId.ToString("N")); Directory.CreateDirectory(directory);
    await File.WriteAllBytesAsync(Path.Combine(directory, "manifest.json"), envelope);
    Console.WriteLine($"Published launcher {LauncherPolicy.ProductVersion} with {files.Count} files."); CryptographicOperations.ZeroMemory(envelope);
    return 0;
}

static async Task<int> VerifyPublicationAsync(string[] values)
{
    byte[] publicKey = await File.ReadAllBytesAsync(Path.GetFullPath(values[3]));
    byte[] envelope = await File.ReadAllBytesAsync(Path.GetFullPath(values[1]));
    DesiredStateManifest manifest;
    try { manifest = new DesiredStateManifestVerifier(publicKey, values[4]).Verify(envelope, Version.Parse(values[5])); }
    finally { CryptographicOperations.ZeroMemory(publicKey); CryptographicOperations.ZeroMemory(envelope); }
    string objectRoot = Path.GetFullPath(values[2]); long bytes = 0; int verified = 0;
    foreach (DesiredStateFile file in manifest.Files)
    {
        string path = SafePath.ResolveContained(objectRoot, file.ObjectPath);
        if (!File.Exists(path)) throw new FileNotFoundException("A published object is missing.", file.ObjectPath);
        var info = new FileInfo(path); if (info.Length != (file.CompressedSize ?? file.Size)) throw new InvalidDataException("A published object size differs.");
        await using FileStream input = info.OpenRead(); string hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(input));
        if (file.Compression == "none" && hash != file.Sha256) throw new CryptographicException("A published object hash differs.");
        bytes = checked(bytes + file.Size); verified++;
    }
    Console.WriteLine($"Verified signed release {manifest.ReleaseId:D}: {verified} file references, {bytes} bytes.");
    return 0;
}
