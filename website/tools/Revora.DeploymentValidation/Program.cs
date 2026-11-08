using System.Formats.Tar;
using System.IO.Compression;
using System.Text.Json;

if (args.Length != 2)
{
    throw new ArgumentException("Usage: Revora.DeploymentValidation <archive> <template-directory>");
}

ValidateArchive(Path.GetFullPath(args[0]));
ValidateTemplate(Path.GetFullPath(args[1]));
ValidatePrivatePermissions();
Console.WriteLine("Revora.DeploymentValidation: archive, template, and permission checks passed");

static void ValidateArchive(string archivePath)
{
    using var file = File.OpenRead(archivePath);
    using var gzip = new GZipStream(file, CompressionMode.Decompress);
    using var archive = new TarReader(gzip);
    var entries = new List<TarEntry>();
    while (archive.GetNextEntry(copyData: false) is { } entry)
    {
        entries.Add(entry);
    }

    Require(entries.Count == 1, "Deployment archive must contain exactly one entry.");
    var host = entries[0];
    Require(host.EntryType is TarEntryType.RegularFile or TarEntryType.V7RegularFile,
        "Deployment entry must be a regular file.");
    Require(host.Name == "Revora.Host", "Deployment entry must be exactly Revora.Host.");
    Require(!Path.IsPathRooted(host.Name), "Archive path must be relative.");
    Require(!host.Name.Split('/').Contains("..", StringComparer.Ordinal), "Archive path traversal is forbidden.");
    Require(string.IsNullOrEmpty(host.LinkName), "Archive links are forbidden.");
    Require(((int)host.Mode & 0xC00) == 0, "setuid and setgid bits are forbidden.");
    Require(((int)host.Mode & 0x49) != 0, "Revora.Host must be executable.");
    Require(((int)host.Mode & 0x12) == 0, "Revora.Host must not be group/other writable.");
}

static void ValidateTemplate(string templateDirectory)
{
    var required = new[]
    {
        "revora.kvp", "revoraconfig.json", "revorametaconfig.json",
        "revoraports.json", "revoraupdates.json", "config/revora.json",
        "config/revora.secrets.example.json",
    };
    foreach (var relative in required)
    {
        Require(File.Exists(Path.Combine(templateDirectory, relative)), $"Missing template file: {relative}");
    }

    foreach (var jsonPath in Directory.EnumerateFiles(templateDirectory, "*.json", SearchOption.AllDirectories))
    {
        using var document = JsonDocument.Parse(File.ReadAllText(jsonPath));
        _ = document.RootElement.ValueKind;
    }

    var kvp = File.ReadLines(Path.Combine(templateDirectory, "revora.kvp"))
        .Where(line => !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith('#'))
        .Select(line => line.Split('=', 2))
        .ToDictionary(parts => parts[0], parts => parts.Length == 2 ? parts[1] : string.Empty,
            StringComparer.Ordinal);
    Equal("revoraconfig.json", kvp["Meta.ConfigManifest"]);
    Equal("revorametaconfig.json", kvp["Meta.MetaConfigManifest"]);
    Equal("revora.kvp", kvp["Meta.ConfigRoot"]);
    Equal("app/Revora.Host", kvp["App.ExecutableLinux"]);
    Equal("app", kvp["App.WorkingDir"]);
    Equal("--config config/revora.json", kvp["App.CommandLineArgs"]);
    Equal("String", kvp["App.ExitMethod"]);
    Equal("stop", kvp["App.ExitString"]);
    Equal("True", kvp["App.HasWriteableConsole"]);
    Equal("True", kvp["App.HasReadableConsole"]);
    Equal("127.0.0.1", kvp["App.ApplicationIPBinding"]);
    Equal("False", kvp["App.UseRandomAdminPassword"]);
    Equal("False", kvp["App.PersistRandomPassword"]);
    Equal(string.Empty, kvp["App.RemoteAdminPassword"]);
    Equal("@IncludeJson[revoraports.json]", kvp["App.Ports"]);
    Equal("@IncludeJson[revoraupdates.json]", kvp["App.UpdateSources"]);
    Equal("None", kvp["App.Compatibility"]);
    Equal("RegexMatch", kvp["App.ApplicationReadyMode"]);
    Equal(@"^\[Revora\] READY$", kvp["Console.AppReadyRegex"]);
    Equal("0", kvp["Limits.AutoRetryCount"]);
    Require(string.IsNullOrEmpty(kvp["Meta.ExtraSetupStepsURI"]), "Setup scripts are forbidden.");
    Equal("{}", kvp["App.EnvironmentVariables"]);
    Require(!kvp["App.CommandLineArgs"].Contains("secret", StringComparison.OrdinalIgnoreCase),
        "Secrets are forbidden in command arguments.");

    using var updates = JsonDocument.Parse(File.ReadAllText(Path.Combine(templateDirectory, "revoraupdates.json")));
    Require(updates.RootElement.GetArrayLength() == 0, "Automatic updates must remain disabled.");
    using var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(templateDirectory, "config/revora.json")));
    Equal("Disabled", config.RootElement.GetProperty("RuntimeAdapter").GetString());
    Equal("127.0.0.1", config.RootElement.GetProperty("AccountListener").GetProperty("Address").GetString());
    Equal("127.0.0.1", config.RootElement.GetProperty("GameListener").GetProperty("Address").GetString());

    var combined = string.Join('\n', Directory.EnumerateFiles(templateDirectory, "*", SearchOption.AllDirectories)
        .Select(File.ReadAllText));
    foreach (var forbidden in new[] { "BEGIN PRIVATE KEY", "git pull", "dotnet run", "dotnet build", "docker run", "sudo " })
    {
        Require(!combined.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
            $"Forbidden template material found: {forbidden}");
    }
}

static void ValidatePrivatePermissions()
{
    if (OperatingSystem.IsWindows()) return;
    var root = Path.Combine(Path.GetTempPath(), $"revora-permission-validation-{Guid.NewGuid():N}");
    Directory.CreateDirectory(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
        UnixFileMode.GroupRead | UnixFileMode.GroupExecute);
    var secret = Path.Combine(root, "revora.secrets.json");
    File.WriteAllText(secret, "{}");
    File.SetUnixFileMode(secret, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    Equal((UnixFileMode)0x1E8, File.GetUnixFileMode(root));
    Equal((UnixFileMode)0x180, File.GetUnixFileMode(secret));
    File.Delete(secret);
    Directory.Delete(root);
}

static void Equal<T>(T expected, T? actual)
{
    Require(EqualityComparer<T>.Default.Equals(expected, actual), $"Expected '{expected}', got '{actual}'.");
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidDataException(message);
}
