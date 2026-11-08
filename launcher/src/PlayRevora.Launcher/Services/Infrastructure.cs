using System.Security.Cryptography;
using System.Text.Json;

namespace PlayRevora.Launcher.Services;

public static class JsonFiles
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static async Task<T> ReadAsync<T>(string path, CancellationToken cancellationToken) where T : new()
    {
        if (!File.Exists(path)) return new T();
        await using FileStream stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, Options, cancellationToken) ?? new T();
    }

    public static T Read<T>(string path) where T : new()
    {
        if (!File.Exists(path)) return new T();
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) ?? new T();
    }

    public static async Task WriteAtomicAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temporary = path + ".tmp." + Guid.NewGuid().ToString("N");
        await using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(stream, value, Options, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
        File.Move(temporary, path, true);
    }
}

public static class SafePaths
{
    public static string UnderRoot(string root, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
            throw new InvalidDataException("Manifest paths must be non-empty and relative.");
        string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string candidate = Path.GetFullPath(Path.Combine(fullRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!candidate.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Manifest path escapes the game directory.");
        return candidate;
    }

    public static Uri RelativeHttps(Uri root, string relative)
    {
        if (root.Scheme != Uri.UriSchemeHttps) throw new InvalidOperationException("Update endpoints must use HTTPS.");
        if (!Uri.TryCreate(root, relative, out Uri? result) || result.Scheme != Uri.UriSchemeHttps
            || !string.Equals(result.Host, root.Host, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Manifest URL escaped the configured HTTPS origin.");
        return result;
    }
}

public static class FileIntegrity
{
    public static void ValidateMetadata(Models.ManifestFile file)
    {
        if (file.Size < 0 || file.Sha256.Length != 64 || !file.Sha256.All(Uri.IsHexDigit))
            throw new InvalidDataException($"Invalid integrity metadata for '{file.Path}'.");
    }

    public static async Task<bool> MatchesAsync(string path, long size, string sha256, CancellationToken cancellationToken)
    {
        FileInfo info = new(path);
        if (!info.Exists || info.Length != size) return false;
        await using FileStream stream = info.OpenRead();
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).Equals(sha256, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class SemanticVersion : IComparable<SemanticVersion>
{
    private SemanticVersion(int major, int minor, int patch) => (Major, Minor, Patch) = (major, minor, patch);
    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }

    public static SemanticVersion Parse(string value)
    {
        string normalized = value.Trim().TrimStart('v', 'V').Split('-', '+')[0];
        string[] parts = normalized.Split('.');
        if (parts.Length is < 1 or > 3 || parts.Any(part => !int.TryParse(part, out _)))
            throw new InvalidDataException($"Unsupported semantic version '{value}'.");
        return new SemanticVersion(int.Parse(parts[0]), parts.Length > 1 ? int.Parse(parts[1]) : 0,
            parts.Length > 2 ? int.Parse(parts[2]) : 0);
    }

    public int CompareTo(SemanticVersion? other) => other is null ? 1
        : Major != other.Major ? Major.CompareTo(other.Major)
        : Minor != other.Minor ? Minor.CompareTo(other.Minor) : Patch.CompareTo(other.Patch);
    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}
