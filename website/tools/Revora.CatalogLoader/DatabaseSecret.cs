using System.Runtime.InteropServices;
using System.Text.Json;
using Npgsql;

namespace Revora.CatalogLoader;

internal sealed record DatabaseSecret(string Host, int Port, string Database, string Username, string Password, Guid RealmId)
{
    public static async Task<DatabaseSecret> LoadProtectedAsync(string path)
    {
        if (!File.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("The database secret file is missing or unsafe.");
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            UnixFileMode mode = File.GetUnixFileMode(path);
            if ((mode & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                         UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0)
                throw new InvalidDataException("The database secret file must not be accessible by group or other users.");
        }
        byte[] bytes = await File.ReadAllBytesAsync(path);
        try
        {
            using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 4 });
            JsonElement root = document.RootElement;
            string[] names = ["host", "port", "database", "username", "password", "realmId"];
            var expected = names.ToHashSet(StringComparer.Ordinal);
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Any(property => !expected.Remove(property.Name)) || expected.Count != 0)
                throw new InvalidDataException("The database secret file schema is invalid.");
            string host = Text(root, "host", 255);
            string database = Text(root, "database", 63);
            string username = Text(root, "username", 63);
            string password = Text(root, "password", 1024);
            int port = root.GetProperty("port").GetInt32();
            if (port is < 1 or > 65535 || !Guid.TryParse(root.GetProperty("realmId").GetString(), out Guid realmId))
                throw new InvalidDataException("The database port or realm is invalid.");
            return new(host, port, database, username, password, realmId);
        }
        catch (JsonException error) { throw new InvalidDataException("The database secret file is malformed.", error); }
        finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes); }
    }

    public string ConnectionString => new NpgsqlConnectionStringBuilder
    {
        Host = Host,
        Port = Port,
        Database = Database,
        Username = Username,
        Password = Password,
        SslMode = Path.IsPathRooted(Host) ? SslMode.Disable : SslMode.Require,
        Timeout = 10,
        CommandTimeout = 120,
        ApplicationName = "Revora.CatalogLoader",
        Pooling = false,
    }.ConnectionString;

    private static string Text(JsonElement root, string name, int maximum)
    {
        string? value = root.GetProperty(name).GetString();
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximum || value.Any(char.IsControl))
            throw new InvalidDataException($"Database secret field {name} is invalid.");
        return value;
    }
}
