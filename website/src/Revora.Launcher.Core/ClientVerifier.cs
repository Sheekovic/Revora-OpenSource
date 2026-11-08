using System.Security.Cryptography;

namespace Revora.Launcher.Core;

public sealed class ClientVerifier
{
    public async Task<VerificationResult> VerifyAsync(string clientRoot, ClientProfile profile, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientRoot);
        var failures = new List<VerificationFailure>();

        foreach (ClientFile expected in profile.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string path = SafePath.ResolveContained(clientRoot, expected.Path);
            SafePath.RejectExistingReparsePoints(clientRoot, path);
            if (!File.Exists(path))
            {
                failures.Add(new(expected.Path, "Missing"));
                continue;
            }

            var info = new FileInfo(path);
            if (info.Length != expected.Size)
            {
                failures.Add(new(expected.Path, "Unexpected size"));
                continue;
            }

            string actual;
            await using (FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
            }
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(actual), Convert.FromHexString(expected.Sha256)))
            {
                failures.Add(new(expected.Path, "Hash mismatch"));
            }
        }

        return new(failures.Count == 0, failures);
    }
}
