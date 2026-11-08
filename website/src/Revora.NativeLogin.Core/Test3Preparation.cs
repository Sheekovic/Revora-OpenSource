using System.Security.Cryptography;
using System.Text.Json;
using Revora.Launcher.Core;

namespace Revora.NativeLogin.Core;

public sealed record PreparationProgress(string Phase, int Completed, int Total, string RelativePath);
public sealed record PreparationResult(string Destination, int Files, long Bytes, string ExecutableSha256);

public sealed class Test3Preparation
{
    private const string StageMarker = ".revora-native-login-test3-staging";
    private static readonly string[] Forbidden = ["Flash.ocx", "TimeDelay.exe", "AutoPatch.exe", "play.exe", "Help/credit.exe"];

    public async Task<PreparationResult> PrepareAsync(string source, string destination, ClientProfile sourceProfile,
        ClientProfile targetProfile, string patchMetadataPath, string serverDatPath, CancellationToken token,
        IProgress<PreparationProgress>? progress = null)
    {
        ValidateRoots(source, destination);
        VerificationResult sourceVerification = await new ClientVerifier().VerifyAsync(source, sourceProfile, token);
        if (!sourceVerification.IsValid) throw new InvalidDataException("The selected Test 2c client does not match its signed profile.");
        RejectForbidden(source);
        if (Directory.Exists(destination)) throw new IOException("The Test 3 destination already exists.");
        string stage = destination + ".staging";
        RecoverStage(stage, targetProfile.BuildId);
        Directory.CreateDirectory(stage);
        await File.WriteAllTextAsync(Path.Combine(stage, StageMarker), targetProfile.BuildId, token);
        try
        {
            int completed = 0;
            foreach (ClientFile file in sourceProfile.Files)
            {
                token.ThrowIfCancellationRequested();
                string input = SafePath.ResolveContained(source, file.Path);
                string output = SafePath.ResolveContained(stage, file.Path);
                SafePath.RejectExistingReparsePoints(source, input);
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                File.Copy(input, output, overwrite: false);
                progress?.Report(new("copy", ++completed, sourceProfile.Files.Count, file.Path));
            }
            ApplyPatch(SafePath.ResolveContained(stage, "Conquer.exe"), patchMetadataPath);
            File.Copy(serverDatPath, SafePath.ResolveContained(stage, "Server.dat"), overwrite: true);
            VerificationResult targetVerification = await new ClientVerifier().VerifyAsync(stage, targetProfile, token);
            if (!targetVerification.IsValid) throw new InvalidDataException("The Test 3 working copy failed its signed profile.");
            File.Delete(Path.Combine(stage, StageMarker));
            Directory.Move(stage, destination);
            ClientFile executable = targetProfile.Files.Single(file => file.Path == "Conquer.exe");
            return new(destination, targetProfile.Files.Count, targetProfile.Files.Sum(file => file.Size), executable.Sha256);
        }
        catch
        {
            TryDeleteOwnedStage(stage, targetProfile.BuildId);
            throw;
        }
    }

    private static void ApplyPatch(string executablePath, string metadataPath)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(metadataPath));
        JsonElement root = document.RootElement;
        byte[] bytes = File.ReadAllBytes(executablePath);
        RequireHash(bytes, root.GetProperty("inputSha256").GetString()!);
        foreach (JsonElement change in root.GetProperty("changes").EnumerateArray())
        {
            int offset = change.GetProperty("Offset").GetInt32();
            byte[] original = Convert.FromHexString(change.GetProperty("OriginalHex").GetString()!);
            byte[] replacement = Convert.FromHexString(change.GetProperty("ReplacementHex").GetString()!);
            if (offset < 0 || offset > bytes.Length - original.Length || original.Length != replacement.Length ||
                !bytes.AsSpan(offset, original.Length).SequenceEqual(original))
                throw new InvalidDataException("A deterministic patch byte did not match the reviewed input.");
            replacement.CopyTo(bytes, offset);
        }
        RequireHash(bytes, root.GetProperty("outputSha256").GetString()!);
        File.WriteAllBytes(executablePath, bytes);
    }

    private static void ValidateRoots(string source, string destination)
    {
        source = Path.GetFullPath(source); destination = Path.GetFullPath(destination);
        string relative = Path.GetRelativePath(source, destination);
        if (!Directory.Exists(source) || relative == "." ||
            (!relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) && relative != ".."))
            throw new InvalidDataException("The Test 3 destination must be separate from the source.");
        if (source.StartsWith("\\\\", StringComparison.Ordinal) || destination.StartsWith("\\\\", StringComparison.Ordinal))
            throw new InvalidDataException("Network paths are not supported.");
        if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("A reparse-point source is not supported.");
    }

    private static void RejectForbidden(string source)
    {
        foreach (string path in Forbidden)
            if (File.Exists(SafePath.ResolveContained(source, path))) throw new InvalidDataException($"Forbidden client component present: {path}");
        if (Directory.Exists(SafePath.ResolveContained(source, "zftqat"))) throw new InvalidDataException("Forbidden zftqat directory present.");
    }

    private static void RecoverStage(string stage, string buildId)
    {
        if (!Directory.Exists(stage)) return;
        if (!Owned(stage, buildId)) throw new IOException("An unmarked Test 3 staging directory exists and was preserved.");
        if (!TryDeleteOwnedStage(stage, buildId)) throw new IOException("The marked Test 3 staging directory could not be recovered safely.");
    }

    private static bool TryDeleteOwnedStage(string stage, string buildId)
    {
        try
        {
            if (!Owned(stage, buildId)) return false;
            if (Directory.EnumerateFileSystemEntries(stage, "*", SearchOption.AllDirectories)
                .Any(entry => (File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)) return false;
            Directory.Delete(stage, recursive: true);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return false; }
    }

    private static bool Owned(string stage, string buildId)
    {
        try
        {
            if ((File.GetAttributes(stage) & FileAttributes.ReparsePoint) != 0) return false;
            string marker = Path.Combine(stage, StageMarker);
            return File.Exists(marker) && File.ReadAllText(marker) == buildId;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return false; }
    }

    private static void RequireHash(byte[] bytes, string expected)
    {
        byte[] actual = SHA256.HashData(bytes);
        if (!CryptographicOperations.FixedTimeEquals(actual, Convert.FromHexString(expected)))
            throw new CryptographicException("A reviewed file hash did not match.");
    }
}
