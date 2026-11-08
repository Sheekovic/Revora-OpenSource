using System.IO.Compression;
using System.Text.Json;

namespace Revora.Launcher.Core;

public sealed record LauncherDiagnosticEvent(DateTimeOffset TimestampUtc, string State, string Category,
    string? RelativePath, string Result, string? ExceptionType = null, string? SanitizedStackTrace = null,
    int? HResult = null, int? NativeError = null, string? Operation = null, int? AttemptCount = null,
    bool? LockTransient = null, bool? LockExhausted = null);

public sealed class LauncherDiagnostics
{
    private readonly object gate = new();
    private readonly List<LauncherDiagnosticEvent> events = [];
    public void Record(LauncherState state, string category, string result, string? relativePath = null)
    {
        string? safePath = relativePath is null ? null : SafePath.IsNormalizedRelative(relativePath) ? relativePath : "[invalid-path-redacted]";
        lock (gate)
        {
            events.Add(new(DateTimeOffset.UtcNow, state.ToString(), Bound(category, 80), safePath, Bound(result, 160)));
            if (events.Count > 2000) events.RemoveRange(0, events.Count - 2000);
        }
    }

    public void RecordException(LauncherState state, string category, Exception error,
        string fallbackOperation, string result)
    {
        LauncherFileOperationException? file = error as LauncherFileOperationException;
        IOException? io = file?.InnerException as IOException ?? error as IOException;
        string? safePath = file?.RelativePath is { } value && SafePath.IsNormalizedRelative(value)
            ? value : null;
        var item = new LauncherDiagnosticEvent(DateTimeOffset.UtcNow, state.ToString(), Bound(category, 80),
            safePath, Bound(result, 160), Bound(error.GetType().FullName ?? error.GetType().Name, 160),
            SanitizeStack(error), error.HResult, io is null ? null : WindowsFileErrors.NativeCode(io),
            Bound(file?.Operation ?? fallbackOperation, 80), file?.AttemptCount,
            file is null ? null : !file.Exhausted, file?.Exhausted);
        lock (gate)
        {
            events.Add(item);
            if (events.Count > 2000) events.RemoveRange(0, events.Count - 2000);
        }
    }
    public void RecordFileAccess(LauncherState state, string category, string relativePath, string result,
        int hResult, int nativeError, string operation, int attemptCount, bool transient, bool exhausted)
    {
        string safePath = SafePath.IsNormalizedRelative(relativePath) ? relativePath : "[invalid-path-redacted]";
        var item = new LauncherDiagnosticEvent(DateTimeOffset.UtcNow, state.ToString(), Bound(category, 80), safePath,
            Bound(result, 160), null, null, hResult, nativeError, Bound(operation, 80), attemptCount,
            transient, exhausted);
        lock (gate)
        {
            events.Add(item);
            if (events.Count > 2000) events.RemoveRange(0, events.Count - 2000);
        }
    }

    public async Task<string> ExportAsync(string destinationDirectory, string launcherVersion, Guid? releaseId,
        string? clientBuild, CancellationToken token)
    {
        Directory.CreateDirectory(destinationDirectory);
        string path = Path.Combine(destinationDirectory, $"revora-launcher-diagnostics-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssZ}.zip");
        LauncherDiagnosticEvent[] snapshot;
        lock (gate) snapshot = events.ToArray();
        var report = new
        {
            schemaVersion = 2,
            generatedAtUtc = DateTimeOffset.UtcNow,
            launcherVersion,
            manifestReleaseId = releaseId,
            clientBuild,
            operatingSystem = Environment.OSVersion.VersionString,
            is64BitOperatingSystem = Environment.Is64BitOperatingSystem,
            events = snapshot,
            excluded = new[] { "passwords", "authentication tokens", "authorization headers", "absolute client paths", "environment variables" },
        };
        await using FileStream archiveStream = new(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: true);
        ZipArchiveEntry entry = archive.CreateEntry("launcher-report.json", CompressionLevel.Optimal);
        entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await using Stream output = entry.Open();
        await JsonSerializer.SerializeAsync(output, report, new JsonSerializerOptions { WriteIndented = true }, token);
        return path;
    }

    private static string? SanitizeStack(Exception error)
    {
        if (string.IsNullOrWhiteSpace(error.StackTrace)) return null;
        string[] frames = error.StackTrace.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.Contains("Revora.", StringComparison.Ordinal))
            .Select(line =>
            {
                int source = line.IndexOf(" in ", StringComparison.Ordinal);
                return Bound((source >= 0 ? line[..source] : line).Trim(), 300);
            })
            .Take(24)
            .ToArray();
        return frames.Length == 0 ? null : string.Join(Environment.NewLine, frames);
    }

    private static string Bound(string value, int maximum) => string.IsNullOrWhiteSpace(value) ? "unspecified" : value.Length <= maximum ? value : value[..maximum];
}
