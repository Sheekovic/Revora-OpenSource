using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Revora.Launcher.Core;

public sealed record LauncherReleaseFile(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("critical")] bool Critical);

public sealed record LauncherReleaseManifest(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("releaseId")] Guid ReleaseId,
    [property: JsonPropertyName("channel")] string Channel,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("publishedAtUtc")] DateTimeOffset PublishedAtUtc,
    [property: JsonPropertyName("entryPoint")] string EntryPoint,
    [property: JsonPropertyName("files")] IReadOnlyList<LauncherReleaseFile> Files);

public sealed partial class LauncherReleaseVerifier(ReadOnlyMemory<byte> publicKey, string keyId)
{
    public LauncherReleaseManifest Verify(ReadOnlySpan<byte> bytes)
    {
        SignedEnvelope envelope = JsonSerializer.Deserialize<SignedEnvelope>(bytes)
            ?? throw new InvalidDataException("The launcher release envelope is empty.");
        if (envelope.KeyId != keyId || envelope.Algorithm != "ECDSA-P256-SHA256")
            throw new CryptographicException("The launcher release signing policy is untrusted.");
        byte[] payload;
        byte[] signature;
        try { payload = Convert.FromBase64String(envelope.Payload); signature = Convert.FromBase64String(envelope.Signature); }
        catch (FormatException error) { throw new InvalidDataException("The launcher release encoding is invalid.", error); }
        try
        {
            using ECDsa verifier = ECDsa.Create();
            verifier.ImportSubjectPublicKeyInfo(publicKey.Span, out int consumed);
            if (consumed != publicKey.Length || !verifier.VerifyData(payload, signature, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence))
                throw new CryptographicException("The launcher release signature is invalid.");
            LauncherReleaseManifest manifest = JsonSerializer.Deserialize<LauncherReleaseManifest>(payload)
                ?? throw new InvalidDataException("The launcher release is empty.");
            Validate(manifest);
            return manifest;
        }
        finally { CryptographicOperations.ZeroMemory(payload); CryptographicOperations.ZeroMemory(signature); }
    }

    public static void Validate(LauncherReleaseManifest manifest)
    {
        if (manifest.SchemaVersion != 1 || manifest.ReleaseId == Guid.Empty || manifest.Channel is not ("stable" or "test") ||
            !Version.TryParse(manifest.Version, out _) || !SafePath.IsNormalizedRelative(manifest.EntryPoint) ||
            manifest.EntryPoint != "Revora.Launcher.exe" || manifest.Files.Count is 0 or > 1000 ||
            manifest.Files.Count(file => file.Path == manifest.EntryPoint) != 1)
            throw new InvalidDataException("The launcher release metadata is invalid.");
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (LauncherReleaseFile file in manifest.Files)
        {
            if (!SafePath.IsNormalizedRelative(file.Path) || !paths.Add(file.Path) || file.Size <= 0 ||
                file.Size > 512 * 1024 * 1024 || !ShaPattern().IsMatch(file.Sha256) ||
                !Uri.TryCreate(file.Url, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps ||
                !string.Equals(uri.Host, LauncherPolicy.ServerHost, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("A launcher release file is invalid.");
        }
    }

    [GeneratedRegex("^[a-f0-9]{64}$", RegexOptions.CultureInvariant)] private static partial Regex ShaPattern();
}

public interface ILauncherReadinessRunner
{
    Task RunAsync(string entryPoint, string workingDirectory, string readinessPath,
        TimeSpan timeout, CancellationToken token);
}

public sealed class WindowsLauncherReadinessRunner : ILauncherReadinessRunner
{
    public async Task RunAsync(string entryPoint, string workingDirectory, string readinessPath,
        TimeSpan timeout, CancellationToken token)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Launcher self-update is Windows-only.");
        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = entryPoint,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
        }) ?? throw new InvalidOperationException("The updated launcher could not start.");
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        while (!File.Exists(readinessPath) && !process.HasExited && DateTimeOffset.UtcNow < deadline)
            await Task.Delay(200, token);
        if (File.Exists(readinessPath)) return;
        if (!process.HasExited)
        {
            process.Kill(true);
            await process.WaitForExitAsync(token).WaitAsync(TimeSpan.FromSeconds(5), token);
        }
        throw new InvalidOperationException("The updated launcher did not report readiness.");
    }
}

public sealed class BootstrapperEngine(HttpClient httpClient, ILauncherReadinessRunner? readinessRunner = null)
{
    public async Task ApplyAsync(LauncherReleaseManifest manifest, string installRoot, string stagingRoot,
        int? oldProcessId, TimeSpan readinessTimeout, CancellationToken token)
    {
        LauncherReleaseVerifier.Validate(manifest);
        string install = Path.GetFullPath(installRoot);
        string staging = Path.GetFullPath(stagingRoot);
        if (!staging.StartsWith(Path.Combine(install, ".revora-launcher", "self-update") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Launcher self-update staging must remain inside its controlled directory.");
        if (oldProcessId is > 0)
        {
            try
            {
                using System.Diagnostics.Process process = System.Diagnostics.Process.GetProcessById(oldProcessId.Value);
                await process.WaitForExitAsync(token).WaitAsync(TimeSpan.FromSeconds(30), token);
            }
            catch (ArgumentException) { }
        }
        if (Directory.Exists(staging)) Directory.Delete(staging, true);
        Directory.CreateDirectory(staging);
        foreach (LauncherReleaseFile file in manifest.Files)
        {
            string destination = SafePath.ResolveContained(staging, file.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using HttpResponseMessage response = await httpClient.GetAsync(file.Url, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();
            await using (Stream input = await response.Content.ReadAsStreamAsync(token))
            await using (FileStream output = new(destination, FileMode.Create, FileAccess.Write, FileShare.None, 128 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await input.CopyToAsync(output, token); await output.FlushAsync(token);
                if (output.Length != file.Size) throw new InvalidDataException("A launcher update file has an invalid size.");
            }
            await using FileStream verify = new(destination, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (!CryptographicOperations.FixedTimeEquals(await SHA256.HashDataAsync(verify, token), Convert.FromHexString(file.Sha256)))
                throw new CryptographicException("A launcher update file failed hash validation.");
        }

        string rollback = Path.Combine(install, ".revora-launcher", "launcher-rollback", manifest.ReleaseId.ToString("N"));
        if (Directory.Exists(rollback)) Directory.Delete(rollback, true);
        Directory.CreateDirectory(rollback);
        var applied = new List<(string Path, bool Existed)>();
        try
        {
            foreach (LauncherReleaseFile file in manifest.Files.OrderBy(file => file.Path, StringComparer.Ordinal))
            {
                string target = SafePath.ResolveContained(install, file.Path);
                string replacement = SafePath.ResolveContained(staging, file.Path);
                SafePath.RejectExistingReparsePoints(install, target);
                bool existed = File.Exists(target);
                if (existed)
                {
                    string backup = SafePath.ResolveContained(rollback, file.Path);
                    Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                    File.Move(target, backup, false);
                }
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                try { File.Move(replacement, target, false); }
                catch
                {
                    if (existed) File.Move(SafePath.ResolveContained(rollback, file.Path), target, false);
                    throw;
                }
                applied.Add((file.Path, existed));
            }
            string readiness = Path.Combine(install, ".launcher-ready");
            if (File.Exists(readiness)) File.Delete(readiness);
            await (readinessRunner ?? new WindowsLauncherReadinessRunner()).RunAsync(
                SafePath.ResolveContained(install, manifest.EntryPoint), install, readiness, readinessTimeout, token);
        }
        catch
        {
            foreach ((string relative, bool existed) in applied.AsEnumerable().Reverse())
            {
                string target = SafePath.ResolveContained(install, relative);
                string backup = SafePath.ResolveContained(rollback, relative);
                if (File.Exists(target)) File.Delete(target);
                if (existed)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Move(backup, target, false);
                }
            }
            throw;
        }
    }
}
