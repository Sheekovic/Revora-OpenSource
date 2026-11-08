namespace Revora.Launcher.Core;

public enum LauncherState
{
    Starting,
    ResolvingClient,
    FetchingManifest,
    VerifyingManifest,
    Scanning,
    RepairRequired,
    Downloading,
    Paused,
    VerifyingDownloads,
    Applying,
    Recovering,
    Ready,
    Launching,
    GameRunning,
    Maintenance,
    Offline,
    Error,
}

public sealed record LauncherStateSnapshot(
    LauncherState State,
    string Operation,
    string? RelativePath = null,
    long CompletedBytes = 0,
    long TotalBytes = 0,
    double BytesPerSecond = 0,
    TimeSpan? Remaining = null);

public sealed class LauncherStateMachine
{
    private static readonly IReadOnlyDictionary<LauncherState, HashSet<LauncherState>> Allowed =
        new Dictionary<LauncherState, HashSet<LauncherState>>
        {
            [LauncherState.Starting] = [LauncherState.ResolvingClient, LauncherState.Error],
            [LauncherState.ResolvingClient] = [LauncherState.Recovering, LauncherState.FetchingManifest, LauncherState.Error],
            [LauncherState.FetchingManifest] = [LauncherState.VerifyingManifest, LauncherState.Offline, LauncherState.Maintenance, LauncherState.Error],
            [LauncherState.VerifyingManifest] = [LauncherState.Scanning, LauncherState.Maintenance, LauncherState.Error],
            [LauncherState.Scanning] = [LauncherState.Ready, LauncherState.RepairRequired, LauncherState.Error],
            [LauncherState.RepairRequired] = [LauncherState.Downloading, LauncherState.Scanning, LauncherState.Error],
            [LauncherState.Downloading] = [LauncherState.Paused, LauncherState.VerifyingDownloads, LauncherState.Error],
            [LauncherState.Paused] = [LauncherState.Downloading, LauncherState.Error],
            [LauncherState.VerifyingDownloads] = [LauncherState.Applying, LauncherState.Error],
            [LauncherState.Applying] = [LauncherState.Scanning, LauncherState.Recovering, LauncherState.Error],
            [LauncherState.Recovering] = [LauncherState.FetchingManifest, LauncherState.Scanning, LauncherState.Error],
            [LauncherState.Ready] = [LauncherState.Scanning, LauncherState.Launching, LauncherState.Error, LauncherState.Offline],
            [LauncherState.Launching] = [LauncherState.GameRunning, LauncherState.Ready, LauncherState.Error],
            [LauncherState.GameRunning] = [LauncherState.Launching, LauncherState.Ready, LauncherState.Error],
            [LauncherState.Maintenance] = [LauncherState.FetchingManifest, LauncherState.Error],
            [LauncherState.Offline] = [LauncherState.FetchingManifest, LauncherState.VerifyingManifest, LauncherState.Ready, LauncherState.Error],
            [LauncherState.Error] = [LauncherState.ResolvingClient, LauncherState.FetchingManifest, LauncherState.Scanning, LauncherState.Recovering],
        };

    private LauncherStateSnapshot current = new(LauncherState.Starting, "Starting");
    public LauncherStateSnapshot Current => current;
    public event EventHandler<LauncherStateSnapshot>? Changed;

    public void Transition(LauncherState next, string operation, string? relativePath = null,
        long completedBytes = 0, long totalBytes = 0, double bytesPerSecond = 0, TimeSpan? remaining = null)
    {
        if (next != current.State && (!Allowed.TryGetValue(current.State, out HashSet<LauncherState>? choices) || !choices.Contains(next)))
            throw new InvalidOperationException($"Launcher transition {current.State} -> {next} is not permitted.");
        if (completedBytes < 0 || totalBytes < 0 || completedBytes > totalBytes || bytesPerSecond < 0)
            throw new ArgumentOutOfRangeException(nameof(completedBytes), "Launcher progress is invalid.");
        current = new(next, operation, relativePath, completedBytes, totalBytes, bytesPerSecond, remaining);
        Changed?.Invoke(this, current);
    }

    public static string PrimaryAction(LauncherState state, bool clientLocated = true) => state switch
    {
        LauncherState.Starting or LauncherState.ResolvingClient when !clientLocated => "Locate Client",
        LauncherState.Starting or LauncherState.ResolvingClient => "Install & Repair",
        LauncherState.Scanning => "Check Files",
        LauncherState.RepairRequired => "Repair Client",
        LauncherState.Downloading or LauncherState.VerifyingDownloads or LauncherState.Applying => "Update Revora",
        LauncherState.Paused => "Resume",
        LauncherState.Ready => "Play Revora",
        LauncherState.GameRunning => "Open Another Account",
        LauncherState.Maintenance => "Maintenance",
        LauncherState.Offline or LauncherState.Error => "Retry",
        _ => "Please wait",
    };
}
