using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PlayRevora.Launcher.Models;
using PlayRevora.Launcher.Services;

const string Algorithm = "ECDSA_P256_SHA256";
var json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, WriteIndented = true };
if (args.Length == 0) return Usage();

try
{
    switch (args[0].ToLowerInvariant())
    {
        case "keygen" when args.Length == 4:
            GenerateKey(args[1], args[2], args[3]); break;
        case "keygen-windows" when args.Length == 4:
            GenerateWindowsKey(args[1], args[2], args[3]); break;
        case "generate" when args.Length == 7:
            await GenerateManifestAsync(args[1], args[2], args[3], args[4], args[5], args[6]); break;
        case "sign" when args.Length == 4:
            SignManifest(args[1], args[2], args[3]); break;
        case "sign-windows" when args.Length == 4:
            SignManifestWithWindowsKey(args[1], args[2], args[3]); break;
        case "sign-index-windows" when args.Length == 4:
            SignIndexWithWindowsKey(args[1], args[2], args[3]); break;
        case "retarget-owner-test" when args.Length == 4:
            RetargetOwnerTest(args[1], args[2], args[3]); break;
        case "refresh-revora-dx9" when args.Length == 7:
            RefreshRevoraDx9(args[1], args[2], args[3], args[4], args[5], args[6]); break;
        case "verify" when args.Length == 3:
            VerifyManifest(args[1], args[2]); break;
        case "verify-index" when args.Length == 3:
            VerifyIndex(args[1], args[2]); break;
        case "verify-tree" when args.Length == 4:
            await VerifyTreeAsync(args[1], args[2], args[3]); break;
        default: return Usage();
    }
}
catch (Exception exception) when (exception is IOException or InvalidDataException or CryptographicException
                                  or UnauthorizedAccessException or FormatException)
{
    Console.Error.WriteLine($"Owner-Test operation failed: {SafeFailure(exception)}");
    return 1;
}
return 0;

int Usage()
{
    Console.Error.WriteLine("PlayRevora Owner-Test manifest operator tool");
    Console.Error.WriteLine("  keygen <encrypted-private.pem> <public-trust.json> <key-id>");
    Console.Error.WriteLine("  keygen-windows <public-trust.json> <key-id> <CNG-key-name>");
    Console.Error.WriteLine("  generate <client-root> <unsigned.json> <key-id> <manifest-version> <issued-utc> <expires-utc>");
    Console.Error.WriteLine("  sign <unsigned.json> <encrypted-private.pem> <signed.json>");
    Console.Error.WriteLine("  sign-windows <unsigned.json> <CNG-key-name> <signed.json>");
    Console.Error.WriteLine("  sign-index-windows <unsigned.json> <CNG-key-name> <signed.json>");
    Console.Error.WriteLine("  retarget-owner-test <unsigned.json> <new-key-id> <output.json>");
    Console.Error.WriteLine("  refresh-revora-dx9 <signed-input.json> <public-trust.json> <client-root> <unsigned-output.json> <version> <expires-utc>");
    Console.Error.WriteLine("  verify <signed.json> <public-trust.json>");
    Console.Error.WriteLine("  verify-index <signed.json> <public-trust.json>");
    Console.Error.WriteLine("  verify-tree <signed.json> <public-trust.json> <client-root>");
    return 2;
}

void GenerateWindowsKey(string publicPath, string keyId, string keyName)
{
    if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("Windows-protected signing requires Windows.");
    RequireNewOutput(publicPath);
    if (string.IsNullOrWhiteSpace(keyId) || !keyId.Contains("owner-test", StringComparison.OrdinalIgnoreCase)
        || string.IsNullOrWhiteSpace(keyName) || !keyName.Contains("OwnerTest", StringComparison.OrdinalIgnoreCase))
        throw new InvalidDataException("Owner-Test key ID and Windows key name are required.");
    var provider = CngProvider.MicrosoftSoftwareKeyStorageProvider;
    if (CngKey.Exists(keyName, provider, CngKeyOpenOptions.None)) throw new InvalidDataException("Windows signing key already exists.");
    var parameters = new CngKeyCreationParameters
    {
        Provider = provider,
        ExportPolicy = CngExportPolicies.None,
        KeyUsage = CngKeyUsages.Signing,
        KeyCreationOptions = CngKeyCreationOptions.None
    };
    using CngKey stored = CngKey.Create(CngAlgorithm.ECDsaP256, keyName, parameters);
    using var key = new ECDsaCng(stored);
    byte[] publicKey = key.ExportSubjectPublicKeyInfo();
    WriteAtomic(publicPath, JsonSerializer.Serialize(new PublicTrust(keyId, Algorithm,
        Convert.ToBase64String(publicKey), Convert.ToHexString(SHA256.HashData(publicKey)), "OWNER-TEST NON-PRODUCTION"), json));
    Console.WriteLine($"Created non-exportable Windows Owner-Test key. Public fingerprint: {Convert.ToHexString(SHA256.HashData(publicKey))}");
}

void GenerateKey(string privatePath, string publicPath, string keyId)
{
    RequireNewOutput(privatePath); RequireNewOutput(publicPath);
    if (string.IsNullOrWhiteSpace(keyId) || !keyId.Contains("owner-test", StringComparison.OrdinalIgnoreCase))
        throw new InvalidDataException("Owner-Test key IDs must contain 'owner-test'.");
    string password = ReadSecret("New Owner-Test private-key passphrase: ");
    string confirmation = ReadSecret("Confirm passphrase: ");
    if (password.Length < 16 || password != confirmation) throw new InvalidDataException("Passphrase mismatch or fewer than 16 characters.");
    using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    byte[] encrypted = key.ExportEncryptedPkcs8PrivateKey(password,
        new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 250_000));
    byte[] publicKey = key.ExportSubjectPublicKeyInfo();
    WritePem(privatePath, "ENCRYPTED PRIVATE KEY", encrypted);
    WriteAtomic(publicPath, JsonSerializer.Serialize(new PublicTrust(keyId, Algorithm,
        Convert.ToBase64String(publicKey), Convert.ToHexString(SHA256.HashData(publicKey)), "OWNER-TEST NON-PRODUCTION"), json));
    CryptographicOperations.ZeroMemory(encrypted); password = confirmation = string.Empty;
    Console.WriteLine($"Created Owner-Test trust root. Public fingerprint: {Convert.ToHexString(SHA256.HashData(publicKey))}");
}

async Task GenerateManifestAsync(string clientRoot, string output, string keyId, string version, string issued, string expires)
{
    RequireNewOutput(output);
    string root = Path.GetFullPath(clientRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
    if (!Directory.Exists(root)) throw new DirectoryNotFoundException("Client root is unavailable.");
    RejectReparse(root);
    var rootFiles = new List<RequiredProfileFile>(); var dx8 = new List<RequiredProfileFile>(); var dx9 = new List<RequiredProfileFile>();
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (string path in Directory.EnumerateFiles(root, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = false, AttributesToSkip = 0 }))
    {
        RejectReparse(path);
        string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
        ValidatedClientRoot.ValidateRelative(relative);
        if (!seen.Add(relative)) throw new InvalidDataException("Duplicate client path.");
        if (ClientMutablePathPolicy.IsExcluded(relative)) continue;
        FileInfo before = new(path);
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        string hash = Convert.ToHexString(await SHA256.HashDataAsync(stream));
        FileInfo after = new(path);
        if (before.Length != after.Length || before.LastWriteTimeUtc != after.LastWriteTimeUtc)
            throw new IOException("A client file changed while hashing.");
        bool critical = relative.Equals("Env_DX8/Conquer.exe", StringComparison.OrdinalIgnoreCase)
            || relative.Equals("Env_DX9/Revora-Conquer.exe", StringComparison.OrdinalIgnoreCase)
            || relative.Equals("Env_DX9/RevoraAuthClient.dll", StringComparison.OrdinalIgnoreCase)
            || relative.Equals("Revora-Server.dat", StringComparison.OrdinalIgnoreCase);
        var entry = new RequiredProfileFile(relative, after.Length, hash, critical);
        if (relative.StartsWith("Env_DX8/", StringComparison.OrdinalIgnoreCase)) dx8.Add(entry);
        else if (relative.StartsWith("Env_DX9/", StringComparison.OrdinalIgnoreCase)) dx9.Add(entry);
        else rootFiles.Add(entry);
    }
    EnsureContract(dx8, "Env_DX8/Conquer.exe"); EnsureContract(dx9, "Env_DX9/Revora-Conquer.exe");
    EnsureContract(dx9, "Env_DX9/RevoraAuthClient.dll");
    var manifest = new SignedClientManifest
    {
        SchemaVersion = 2, ManifestVersion = version, IssuedAtUtc = DateTimeOffset.Parse(issued),
        ExpiresAtUtc = DateTimeOffset.Parse(expires), MinimumLauncherVersion = "1.0.0",
        Rollback = new(), RootRequiredFiles = rootFiles.OrderBy(item => item.RelativePath, StringComparer.Ordinal).ToList(),
        Profiles =
        [
            Profile("dx8", "DirectX 8", "Env_DX8/Conquer.exe", "Env_DX8", ["blacknull"], dx8, true, false, 50),
            Profile("dx9", "DirectX 9 — Revora", "Env_DX9/Revora-Conquer.exe", "Env_DX9", ["blacknull"], dx9, false, true, 100)
        ],
        Signature = new() { Algorithm = Algorithm, KeyId = keyId, ValueBase64 = "" }
    };
    _ = SignedManifestCanonicalizer.Canonicalize(manifest);
    WriteAtomic(output, JsonSerializer.Serialize(manifest, json));
    Console.WriteLine($"Generated unsigned Owner-Test manifest: root={rootFiles.Count}, dx8={dx8.Count}, dx9={dx9.Count}, total={rootFiles.Count + dx8.Count + dx9.Count}.");
}

void SignManifest(string unsignedPath, string privatePath, string output)
{
    RequireNewOutput(output);
    SignedClientManifest manifest = Read<SignedClientManifest>(unsignedPath);
    if (!manifest.Signature.KeyId.Contains("owner-test", StringComparison.OrdinalIgnoreCase) || manifest.Signature.ValueBase64.Length != 0)
        throw new InvalidDataException("Input must be an unsigned Owner-Test manifest.");
    string password = ReadSecret("Owner-Test private-key passphrase: ");
    using ECDsa key = ECDsa.Create();
    key.ImportFromEncryptedPem(File.ReadAllText(privatePath), password);
    manifest.Signature.ValueBase64 = Convert.ToBase64String(key.SignData(
        SignedManifestCanonicalizer.Canonicalize(manifest), HashAlgorithmName.SHA256));
    WriteAtomic(output, JsonSerializer.Serialize(manifest, json)); password = string.Empty;
    Console.WriteLine($"Signed Owner-Test manifest. SHA-256: {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(output)))}");
}

void SignManifestWithWindowsKey(string unsignedPath, string keyName, string output)
{
    if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("Windows-protected signing requires Windows.");
    RequireNewOutput(output);
    SignedClientManifest manifest = Read<SignedClientManifest>(unsignedPath);
    if (!manifest.Signature.KeyId.Contains("owner-test", StringComparison.OrdinalIgnoreCase) || manifest.Signature.ValueBase64.Length != 0)
        throw new InvalidDataException("Input must be an unsigned Owner-Test manifest.");
    using CngKey stored = CngKey.Open(keyName, CngProvider.MicrosoftSoftwareKeyStorageProvider, CngKeyOpenOptions.None);
    using var key = new ECDsaCng(stored);
    manifest.Signature.ValueBase64 = Convert.ToBase64String(key.SignData(
        SignedManifestCanonicalizer.Canonicalize(manifest), HashAlgorithmName.SHA256));
    WriteAtomic(output, JsonSerializer.Serialize(manifest, json));
    Console.WriteLine($"Signed Owner-Test manifest with Windows CNG. SHA-256: {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(output)))}");
}

void SignIndexWithWindowsKey(string unsignedPath, string keyName, string output)
{
    if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("Windows-protected signing requires Windows.");
    RequireNewOutput(output);
    SignedPatchIndex index = Read<SignedPatchIndex>(unsignedPath);
    if (!index.Signature.KeyId.Contains("owner-test", StringComparison.OrdinalIgnoreCase) || index.Signature.ValueBase64.Length != 0
        || !index.Channel.Equals("owner-test", StringComparison.Ordinal))
        throw new InvalidDataException("Input must be an unsigned Owner-Test patch index.");
    using CngKey stored = CngKey.Open(keyName, CngProvider.MicrosoftSoftwareKeyStorageProvider, CngKeyOpenOptions.None);
    using var key = new ECDsaCng(stored);
    index.Signature.ValueBase64 = Convert.ToBase64String(key.SignData(UpdateCanonicalizer.Index(index), HashAlgorithmName.SHA256));
    WriteAtomic(output, JsonSerializer.Serialize(index, json));
    Console.WriteLine($"Signed Owner-Test update index with Windows CNG. SHA-256: {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(output)))}");
}

void RetargetOwnerTest(string input, string keyId, string output)
{
    RequireNewOutput(output); SignedClientManifest manifest = Read<SignedClientManifest>(input);
    if (manifest.Signature.ValueBase64.Length != 0 || !keyId.Contains("owner-test", StringComparison.OrdinalIgnoreCase))
        throw new InvalidDataException("Only an unsigned Owner-Test manifest can be retargeted.");
    manifest.Signature.KeyId = keyId; _ = SignedManifestCanonicalizer.Canonicalize(manifest);
    WriteAtomic(output, JsonSerializer.Serialize(manifest, json));
    Console.WriteLine("Retargeted unsigned Owner-Test manifest to the new public trust identity.");
}

void RefreshRevoraDx9(string input, string publicPath, string clientRoot, string output,
    string version, string expires)
{
    RequireNewOutput(output);
    VerifyManifest(input, publicPath);
    SignedClientManifest manifest = Read<SignedClientManifest>(input);
    string root = Path.GetFullPath(clientRoot);
    string executablePath = Path.Combine(root, "Env_DX9", "Revora-Conquer.exe");
    string authClientPath = Path.Combine(root, "Env_DX9", "RevoraAuthClient.dll");
    string serverPath = Path.Combine(root, "Revora-Server.dat");
    string exitConfigurationPath = Path.Combine(root, "ini", "oem.ini");
    foreach (string path in new[] { executablePath, authClientPath, serverPath, exitConfigurationPath }) RejectReparse(path);
    var executable = new RequiredProfileFile("Env_DX9/Revora-Conquer.exe",
        new FileInfo(executablePath).Length, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(executablePath))), true);
    var authClient = new RequiredProfileFile("Env_DX9/RevoraAuthClient.dll",
        new FileInfo(authClientPath).Length, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(authClientPath))), true);
    var server = new RequiredProfileFile("Revora-Server.dat",
        new FileInfo(serverPath).Length, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(serverPath))), true);
    var exitConfiguration = new RequiredProfileFile("ini/oem.ini",
        new FileInfo(exitConfigurationPath).Length, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(exitConfigurationPath))), true);
    ManifestProfile dx9 = manifest.Profiles.Single(profile => profile.Id.Equals("dx9", StringComparison.Ordinal));
    ClientMutablePathPolicy.RemoveExcluded(manifest);
    dx9.RequiredFiles.RemoveAll(file => file.RelativePath.Equals(executable.RelativePath, StringComparison.OrdinalIgnoreCase)
        || file.RelativePath.Equals(authClient.RelativePath, StringComparison.OrdinalIgnoreCase)
        || file.RelativePath.Equals(server.RelativePath, StringComparison.OrdinalIgnoreCase)
        || file.RelativePath.Equals("Env_DX9/Revora-Server.dat", StringComparison.OrdinalIgnoreCase));
    dx9.RequiredFiles.Add(executable);
    dx9.RequiredFiles.Add(authClient);
    dx9.RequiredFiles = dx9.RequiredFiles.OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToList();
    dx9.CriticalLaunchFiles = [executable, authClient];
    dx9.AutomaticFallbackAllowed = false;
    manifest.RootRequiredFiles.RemoveAll(file => file.RelativePath.Equals(server.RelativePath, StringComparison.OrdinalIgnoreCase)
        || file.RelativePath.Equals("Env_DX9/Revora-Server.dat", StringComparison.OrdinalIgnoreCase)
        || file.RelativePath.Equals(exitConfiguration.RelativePath, StringComparison.OrdinalIgnoreCase));
    manifest.RootRequiredFiles.AddRange([server, exitConfiguration]);
    manifest.RootRequiredFiles = manifest.RootRequiredFiles.OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToList();
    manifest.Profiles = [dx9];
    manifest.ManifestVersion = version;
    manifest.IssuedAtUtc = DateTimeOffset.UtcNow;
    manifest.ExpiresAtUtc = DateTimeOffset.Parse(expires);
    manifest.Signature.ValueBase64 = "";
    _ = SignedManifestCanonicalizer.Canonicalize(manifest);
    WriteAtomic(output, JsonSerializer.Serialize(manifest, json));
    Console.WriteLine("Refreshed only the hash-bound Revora DX9 runtime entries; unchanged catalog files were not rehashed.");
}

void VerifyManifest(string manifestPath, string publicPath)
{
    SignedClientManifest manifest = Read<SignedClientManifest>(manifestPath); PublicTrust trust = Read<PublicTrust>(publicPath);
    if (trust.Label != "OWNER-TEST NON-PRODUCTION" || !trust.KeyId.Contains("owner-test", StringComparison.OrdinalIgnoreCase))
        throw new InvalidDataException("Public trust file is not an Owner-Test root.");
    VerifiedClientManifest verified = new SignedManifestVerifier(new OperatorClock(DateTimeOffset.UtcNow), new Version(1, 0, 0))
        .Verify(manifest, new(trust.KeyId, trust.PublicKeySpkiBase64));
    Console.WriteLine($"Verified Owner-Test manifest: root={verified.RootRequiredFiles.Count}, profiles={verified.Profiles.Count}.");
}

void VerifyIndex(string indexPath, string publicPath)
{
    SignedPatchIndex index = Read<SignedPatchIndex>(indexPath); PublicTrust trust = Read<PublicTrust>(publicPath);
    if (trust.Label != "OWNER-TEST NON-PRODUCTION" || !index.Channel.Equals("owner-test", StringComparison.Ordinal))
        throw new InvalidDataException("Owner-Test index trust or channel is invalid.");
    new SignedUpdateMetadataVerifier(new OperatorClock(DateTimeOffset.UtcNow)).VerifyIndex(index,
        new(trust.KeyId, trust.PublicKeySpkiBase64), "owner-test");
    Console.WriteLine($"Verified signed Owner-Test update index: {index.IndexId}; patches={index.Patches.Count}.");
}

async Task VerifyTreeAsync(string manifestPath, string publicPath, string clientRoot)
{
    SignedClientManifest manifest = Read<SignedClientManifest>(manifestPath); PublicTrust trust = Read<PublicTrust>(publicPath);
    if (trust.Label != "OWNER-TEST NON-PRODUCTION" || !trust.KeyId.Contains("owner-test", StringComparison.OrdinalIgnoreCase))
        throw new InvalidDataException("Public trust file is not an Owner-Test root.");
    VerifiedClientManifest verified = new SignedManifestVerifier(new OperatorClock(DateTimeOffset.UtcNow), new Version(1, 0, 0))
        .Verify(manifest, new(trust.KeyId, trust.PublicKeySpkiBase64));
    var fileSystem = new PhysicalProfileFileSystem(); var root = new ValidatedClientRoot(clientRoot, fileSystem);
    var verifier = new ClientFileIntegrityVerifier(root, fileSystem); await verifier.VerifyAsync(verified.RootRequiredFiles, CancellationToken.None);
    foreach (ClientProfile profile in verified.Profiles) await verifier.VerifyAsync(profile.RequiredFiles, CancellationToken.None);
    Console.WriteLine($"Verified complete Owner-Test client tree: root={verified.RootRequiredFiles.Count}, profiles={verified.Profiles.Sum(profile => profile.RequiredFiles.Count)}, total={verified.RootRequiredFiles.Count + verified.Profiles.Sum(profile => profile.RequiredFiles.Count)}.");
}

ManifestProfile Profile(string id, string name, string exe, string cwd, List<string> arguments,
    List<RequiredProfileFile> files, bool dx8, bool dx9, int priority) => new()
{
    Id = id, DisplayName = name, ExecutablePath = exe, WorkingDirectory = cwd, Arguments = arguments,
    RequiredFiles = files.OrderBy(item => item.RelativePath, StringComparer.Ordinal).ToList(),
    CriticalLaunchFiles = files.Where(item => item.RelativePath.Replace('\\', '/').Equals(exe, StringComparison.OrdinalIgnoreCase)).ToList(), MinimumWindowsVersion = "10.0",
    SupportedArchitectures = ["x64"], RequiresDirectX8 = dx8, RequiresDirectX9 = dx9, Priority = priority,
    AutomaticFallbackAllowed = true
};
void EnsureContract(List<RequiredProfileFile> files, string executable)
{
    if (!files.Any(item => item.RelativePath.Equals(executable, StringComparison.OrdinalIgnoreCase)))
        throw new InvalidDataException("Profile executable is missing.");
}
void RejectReparse(string path)
{
    if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Reparse points are forbidden.");
}
void RequireNewOutput(string path)
{
    string full = Path.GetFullPath(path); if (File.Exists(full)) throw new IOException("Output already exists.");
    Directory.CreateDirectory(Path.GetDirectoryName(full)!);
}
void WriteAtomic(string path, string content)
{
    string temporary = path + ".tmp." + Guid.NewGuid().ToString("N"); File.WriteAllText(temporary, content, new UTF8Encoding(false)); File.Move(temporary, path);
}
void WritePem(string path, string label, byte[] data) => WriteAtomic(path,
    $"-----BEGIN {label}-----\n{Convert.ToBase64String(data, Base64FormattingOptions.InsertLineBreaks)}\n-----END {label}-----\n");
T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllText(path), json) ?? throw new InvalidDataException("Invalid JSON.");
string ReadSecret(string prompt)
{
    if (Console.IsInputRedirected) throw new InvalidOperationException("Secret input requires an interactive operator console.");
    Console.Error.Write(prompt); var value = new StringBuilder();
    while (true)
    {
        ConsoleKeyInfo key = Console.ReadKey(true);
        if (key.Key == ConsoleKey.Enter) break;
        if (key.Key == ConsoleKey.Backspace) { if (value.Length > 0) value.Length--; continue; }
        if (!char.IsControl(key.KeyChar)) value.Append(key.KeyChar);
    }
    Console.Error.WriteLine(); return value.ToString();
}
string SafeFailure(Exception exception) => exception switch
{
    ClientFileVerificationException failure => $"{failure.Category}: {failure.RelativePath}",
    CryptographicException => "cryptographic verification or key operation failed",
    UnauthorizedAccessException => "access was denied",
    IOException => "a required file operation failed",
    FormatException => "an input format was invalid",
    _ => "manifest validation failed"
};

sealed record PublicTrust(string KeyId, string Algorithm, string PublicKeySpkiBase64, string Sha256Fingerprint, string Label);
sealed class OperatorClock(DateTimeOffset now) : IClock { public DateTimeOffset UtcNow => now; public Task Delay(TimeSpan duration, CancellationToken cancellationToken) => Task.CompletedTask; }
