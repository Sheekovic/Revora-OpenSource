using PlayRevora.Launcher.Models;

namespace PlayRevora.Launcher.Services;

public sealed class ClientDoctorService(LauncherConfig config, DownloadService downloads)
{
    private readonly Uri baseUri = new(config.UpdateBaseUrl, UriKind.Absolute);

    public async Task<int> RepairAsync(IProgress<OperationProgress> progress, CancellationToken cancellationToken)
    {
        MasterManifest manifest = await downloads.GetJsonAsync<MasterManifest>(
            SafePaths.RelativeHttps(baseUri, config.MasterManifest), cancellationToken);
        if (manifest.SchemaVersion != 1) throw new InvalidDataException("Unsupported master manifest schema.");
        int repaired = 0;
        for (int index = 0; index < manifest.Files.Count; index++)
        {
            ManifestFile file = manifest.Files[index];
            FileIntegrity.ValidateMetadata(file);
            string target = SafePaths.UnderRoot(config.GameRoot, file.Path);
            if (!await FileIntegrity.MatchesAsync(target, file.Size, file.Sha256, cancellationToken))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                string temporary = target + ".repair." + Guid.NewGuid().ToString("N");
                try
                {
                    string source = string.IsNullOrWhiteSpace(file.Source)
                        ? config.BaselinePath.TrimEnd('/') + "/" + file.Path.Replace('\\', '/') : file.Source;
                    await downloads.DownloadAsync(SafePaths.RelativeHttps(baseUri, source), temporary,
                        file.Size, cancellationToken, progress);
                    if (!await FileIntegrity.MatchesAsync(temporary, file.Size, file.Sha256, cancellationToken))
                        throw new InvalidDataException($"Repair hash failed for '{file.Path}'.");
                    File.Move(temporary, target, true);
                    repaired++;
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
            progress.Report(new($"Checked {file.Path}", (index + 1) * 100d / Math.Max(1, manifest.Files.Count),
                100, "Integrity verified"));
        }
        return repaired;
    }
}
