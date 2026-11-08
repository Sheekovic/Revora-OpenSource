using System.Net;
using System.Text.Json;

namespace Revora.Host.Configuration;

public sealed record RevoraHostOptions
{
    public string InstanceName { get; init; } = "Revora Development";
    public string RealmName { get; init; } = "Revora";
    public string Environment { get; init; } = "Development";
    public string RuntimeAdapter { get; init; } = "Disabled";
    public ListenerOptions AccountListener { get; init; } = new("127.0.0.1", 9958);
    public ListenerOptions GameListener { get; init; } = new("127.0.0.1", 9959);
    public string AdvertisedGameAddress { get; init; } = "127.0.0.1";
    public string OwnerSourceAddress { get; init; } = "127.0.0.1";
    public int MaximumPlayers { get; init; } = 100;
    public int ShutdownTimeoutSeconds { get; init; } = 30;
    public int SaveIntervalSeconds { get; init; } = 300;
    public string LogLevel { get; init; } = "Information";
    public int LogRetentionDays { get; init; } = 14;
    public bool RegistrationEnabled { get; init; }
    public bool MaintenanceMode { get; init; } = true;
    public bool ApplyMigrations { get; init; }
    public string SecretFile { get; init; } = "config/revora.secrets.json";
    public string DataDirectory { get; init; } = "data";
    public string LogDirectory { get; init; } = "logs";

    public static async Task<RevoraHostOptions> LoadAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var options = await JsonSerializer.DeserializeAsync<RevoraHostOptions>(
            stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken).ConfigureAwait(false);
        return options ?? throw new InvalidDataException("Configuration is empty.");
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(InstanceName) || string.IsNullOrWhiteSpace(RealmName))
        {
            throw new InvalidDataException("InstanceName and RealmName are required.");
        }

        AccountListener.Validate(nameof(AccountListener));
        GameListener.Validate(nameof(GameListener));
        if (AccountListener == GameListener)
        {
            throw new InvalidDataException("Account and game listeners must use different endpoints.");
        }

        if (MaximumPlayers is < 1 or > 10_000)
        {
            throw new InvalidDataException("MaximumPlayers must be between 1 and 10000.");
        }

        if (ShutdownTimeoutSeconds is < 5 or > 300)
        {
            throw new InvalidDataException("ShutdownTimeoutSeconds must be between 5 and 300.");
        }

        if (SaveIntervalSeconds is < 30 or > 86_400)
        {
            throw new InvalidDataException("SaveIntervalSeconds must be between 30 and 86400.");
        }

        if (ApplyMigrations && !string.Equals(Environment, "Development", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Automatic migrations are restricted to Development.");
        }

        ValidateReviewedRelativePath(DataDirectory, "data", nameof(DataDirectory));
        ValidateReviewedRelativePath(LogDirectory, "logs", nameof(LogDirectory));


        if (!string.Equals(RuntimeAdapter, "Disabled", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(RuntimeAdapter, "InMemory", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(RuntimeAdapter, "PostgreSqlSyntheticTest", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(RuntimeAdapter, "PostgreSql5165", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("RuntimeAdapter is not supported.");
        }

        if (string.Equals(RuntimeAdapter, "PostgreSql5165", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(Environment, "OwnerTest", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The genuine 5165 runtime is restricted to OwnerTest.");
            if (!IPAddress.TryParse(AdvertisedGameAddress, out IPAddress? advertised) || advertised.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                throw new InvalidDataException("AdvertisedGameAddress must be numeric IPv4.");
            if (!IPAddress.TryParse(OwnerSourceAddress, out IPAddress? owner) || owner.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                throw new InvalidDataException("OwnerSourceAddress must be numeric IPv4.");
        }

        if (string.Equals(RuntimeAdapter, "InMemory", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(RuntimeAdapter, "PostgreSqlSyntheticTest", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(Environment, "Development", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(Environment, "Test", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Development runtime adapters are restricted to Development or Test.");
            }

            if (!IPAddress.IsLoopback(AccountListener.ParseAddress()) ||
                !IPAddress.IsLoopback(GameListener.ParseAddress()))
            {
                throw new InvalidDataException("Development runtime adapters require loopback-only listeners.");
            }
        }
    }

    private static void ValidateReviewedRelativePath(string path, string leaf, string name)
    {
        string parent = $"..{Path.DirectorySeparatorChar}{leaf}";
        string alternateParent = $"..{Path.AltDirectorySeparatorChar}{leaf}";
        if (!string.Equals(path, leaf, StringComparison.Ordinal) &&
            !string.Equals(path, parent, StringComparison.Ordinal) &&
            !string.Equals(path, alternateParent, StringComparison.Ordinal))
            throw new InvalidDataException($"{name} is not an approved service-layout path.");
    }
}

public sealed record RevoraHostSecrets
{
    public string DatabaseMode { get; init; } = "Disabled";
    public string PostgreSqlSocketPath { get; init; } = "../data/postgresql";
    public string PostgreSqlUsername { get; init; } = "amp";
    public string PostgreSqlDatabase { get; init; } = "revora";

    public static RevoraHostSecrets Load(string path)
    {
        return JsonSerializer.Deserialize<RevoraHostSecrets>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Protected Host secret file is empty.");
    }
}

public sealed record ListenerOptions(string Address, int Port)
{
    public IPAddress ParseAddress() =>
        IPAddress.TryParse(Address, out var address)
            ? address
            : throw new InvalidDataException("Listener address must be an IP literal.");

    public void Validate(string name)
    {
        _ = ParseAddress();
        if (Port is < 1024 or > 65535)
        {
            throw new InvalidDataException($"{name} port must be between 1024 and 65535.");
        }
    }
}
