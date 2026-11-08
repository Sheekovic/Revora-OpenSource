using System.Diagnostics;

namespace PlayRevora.Launcher.Services;

public interface IExternalUriOpener
{
    void Open(Uri uri);
}

public sealed class WindowsExternalUriOpener : IExternalUriOpener
{
    public void Open(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        Process.Start(new ProcessStartInfo
        {
            FileName = uri.AbsoluteUri,
            UseShellExecute = true
        });
    }
}

public sealed class OfficialRegistrationLinkService
{
    public const string ApprovedRegistrationUrl = "https://playrevora.com/register";
    private static readonly Uri ApprovedUri = new(ApprovedRegistrationUrl, UriKind.Absolute);
    private readonly IExternalUriOpener opener;
    private readonly Func<long> timestamp;
    private readonly long minimumIntervalTicks;
    private long lastOpenedAt = long.MinValue;

    public OfficialRegistrationLinkService(
        IExternalUriOpener opener,
        TimeSpan? minimumInterval = null,
        Func<long>? timestamp = null)
    {
        this.opener = opener ?? throw new ArgumentNullException(nameof(opener));
        this.timestamp = timestamp ?? Stopwatch.GetTimestamp;
        minimumIntervalTicks = (long)((minimumInterval ?? TimeSpan.FromSeconds(3)).TotalSeconds * Stopwatch.Frequency);
    }

    public bool TryOpen(Uri destination)
    {
        Validate(destination);
        long now = timestamp();
        long previous = Interlocked.Read(ref lastOpenedAt);
        if (previous != long.MinValue && now - previous < minimumIntervalTicks)
            return false;

        Interlocked.Exchange(ref lastOpenedAt, now);
        try
        {
            opener.Open(ApprovedUri);
            return true;
        }
        catch
        {
            Interlocked.Exchange(ref lastOpenedAt, previous);
            throw;
        }
    }

    private static void Validate(Uri destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.IsAbsoluteUri ||
            !string.Equals(destination.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal) ||
            !string.Equals(destination.Host, "playrevora.com", StringComparison.OrdinalIgnoreCase) ||
            destination.Port != 443 ||
            !string.Equals(destination.AbsolutePath, "/register", StringComparison.Ordinal) ||
            !string.IsNullOrEmpty(destination.Query) ||
            !string.IsNullOrEmpty(destination.Fragment) ||
            !string.IsNullOrEmpty(destination.UserInfo))
        {
            throw new InvalidOperationException("The registration destination is not approved.");
        }
    }
}
