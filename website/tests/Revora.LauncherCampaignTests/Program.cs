using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Revora.Launcher.Core;

if (args.Length != 4) throw new ArgumentException("Usage: <signed-manifest> <public-key.der> <object-root|public-https> <disposable-client>");
string manifestPath = Path.GetFullPath(args[0]), publicKeyPath = Path.GetFullPath(args[1]);
string objectSource = args[2];
string? objectRoot = objectSource == "public-https" ? null : Path.GetFullPath(objectSource);
string clientRoot = Path.GetFullPath(args[3]);
byte[] envelope = await File.ReadAllBytesAsync(manifestPath), publicKey = await File.ReadAllBytesAsync(publicKeyPath);
DesiredStateManifest manifest;
try { manifest = new DesiredStateManifestVerifier(publicKey, LauncherPolicy.ProductionSigningKeyId).Verify(envelope, Version.Parse(LauncherPolicy.ProductVersion)); }
finally { CryptographicOperations.ZeroMemory(envelope); CryptographicOperations.ZeroMemory(publicKey); }

DesiredStateFile missing = manifest.Files.Single(file => file.Path == "version.dat");
DesiredStateFile corrupt = manifest.Files.Single(file => file.Path == "ini/3DEffect.dbc");
string missingPath = SafePath.ResolveContained(clientRoot, missing.Path), corruptPath = SafePath.ResolveContained(clientRoot, corrupt.Path);
string moved = missingPath + ".campaign-original"; File.Move(missingPath, moved, false);
File.Move(corruptPath, corruptPath + ".campaign-original", false);
await File.WriteAllTextAsync(corruptPath, "deliberately-corrupt-disposable-copy");
string preservedScreenshot = Path.Combine(clientRoot, "screenshots", "revora-campaign-owned.txt"); Directory.CreateDirectory(Path.GetDirectoryName(preservedScreenshot)!); await File.WriteAllTextAsync(preservedScreenshot, "preserve-me");
string preservedSettings = Path.Combine(clientRoot, "revora-user-settings.local"); await File.WriteAllTextAsync(preservedSettings, "preserve-me-too");

var scanner = new DesiredStateScanner();
ClientScanResult damaged = await scanner.ScanAsync(clientRoot, manifest, null, false, null, default);
if (damaged.Failures.Count != 2 || !damaged.Failures.Any(value => value.Path == missing.Path) || !damaged.Failures.Any(value => value.Path == corrupt.Path))
    throw new InvalidOperationException("The campaign did not detect exactly the two deliberate defects.");
DesiredStateFile[] repairs = manifest.Files.Where(file => damaged.Failures.Any(failure => failure.Path == file.Path)).ToArray();
using var http = objectRoot is null
    ? new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false, ConnectTimeout = TimeSpan.FromSeconds(10) }) { Timeout = TimeSpan.FromMinutes(5) }
    : new HttpClient(new LocalObjectHandler(objectRoot));
string staging = Path.Combine(clientRoot, ".revora-launcher", "staging", manifest.ReleaseId.ToString("N"));
await new PatchDownloadEngine(http, new(2, 2)).DownloadAsync(staging, manifest, repairs, new PauseController(), null, default);
using (ClientPatchLock.Acquire(clientRoot)) await new TransactionalPatchApplier().ApplyAsync(clientRoot, staging, manifest, repairs, null, default);
ClientScanResult repaired = await scanner.ScanAsync(clientRoot, manifest, null, true, null, default);
if (!repaired.IsValid) throw new InvalidOperationException("The fully repaired client did not match the signed desired state.");
if (await File.ReadAllTextAsync(preservedScreenshot) != "preserve-me" || await File.ReadAllTextAsync(preservedSettings) != "preserve-me-too")
    throw new InvalidOperationException("The repair changed a user-owned unmanaged file.");
await new VerifiedStateCache().WriteAsync(clientRoot, manifest, repaired.Verified, default);
ClientScanResult quick = await scanner.ScanAsync(clientRoot, manifest, await new VerifiedStateCache().ReadAsync(clientRoot, default), false, null, default);
long criticalBytes = manifest.Files.Where(file => file.Critical).Sum(file => file.Size);
if (!quick.IsValid || quick.HashedBytes != criticalBytes || quick.CachedFiles == 0)
    throw new InvalidOperationException("The quick verification did not hash precisely the critical set and cache unchanged passive files.");
Console.WriteLine($"PASS damaged-client repair: files={manifest.Files.Count}; bytes={manifest.Files.Sum(file => file.Size)}; criticalBytes={criticalBytes}; cachedFiles={quick.CachedFiles}");
return 0;

sealed class LocalObjectHandler(string root) : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        string marker = "/objects/sha256/"; int index = request.RequestUri!.AbsolutePath.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0) return new(HttpStatusCode.NotFound);
        string relative = request.RequestUri.AbsolutePath[(index + 1)..]; string path = SafePath.ResolveContained(root, relative);
        if (!File.Exists(path)) return new(HttpStatusCode.NotFound);
        byte[] content = await File.ReadAllBytesAsync(path, token); long start = request.Headers.Range?.Ranges.Single().From ?? 0;
        if (start < 0 || start > content.Length) return new(HttpStatusCode.RequestedRangeNotSatisfiable);
        byte[] body = content[(int)start..]; var response = new HttpResponseMessage(start == 0 ? HttpStatusCode.OK : HttpStatusCode.PartialContent) { Content = new ByteArrayContent(body) };
        if (start > 0) response.Content.Headers.ContentRange = new ContentRangeHeaderValue(start, content.Length - 1, content.Length);
        return response;
    }
}
