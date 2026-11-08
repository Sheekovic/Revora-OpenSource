using Npgsql;
namespace Revora.Admin;

public sealed record AccountSummary(string Username, long CharacterId, short State);
public sealed record CharacterSummary(long Uid, string Name, short Level, short Profession, int Map, int X, int Y, bool Online);
public sealed record GameSnapshot(List<AccountSummary> Accounts, List<CharacterSummary> Characters);

/// <summary>Only explicit public fields are read; passwords and account recovery data are never queried.</summary>
public sealed class LiveGameStore
{
    private readonly string connectionString;
    public LiveGameStore()
    {
        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        bool section = false;
        string path = Environment.GetEnvironmentVariable("REVORA_ADMIN_GAME_INI")
            ?? @"C:\Users\Administrator\Desktop\Revora\Source\Revora-6711\bin\Debug\Configuracion.ini";
        foreach (string raw in File.ReadLines(path))
        {
            string line = raw.Trim();
            if (line.StartsWith(';') || line.StartsWith('#') || line.Length == 0) continue;
            if (line.StartsWith('[')) { section = line.Equals("[MySql]", StringComparison.OrdinalIgnoreCase); continue; }
            int equals = line.IndexOf('=');
            if (section && equals > 0) settings[line[..equals].Trim()] = line[(equals + 1)..].Trim();
        }
        var host = settings["Host"].Split(';');
        int port = settings.TryGetValue("Port", out string? value) ? int.Parse(value) : 25432;
        foreach (string option in host.Skip(1))
            if (option.Trim().StartsWith("Port=", StringComparison.OrdinalIgnoreCase)) port = int.Parse(option.Trim()[5..]);
        if (host[0].Trim() != "127.0.0.1" || port != 25432 || settings["Database"] != "revora6711_3d")
            throw new InvalidOperationException("Admin database does not match the approved live game database.");
        connectionString = new NpgsqlConnectionStringBuilder
        {
            Host = "127.0.0.1", Port = port, Database = "revora6711_3d",
            Username = settings["Username"], Password = settings["Password"],
            ApplicationName = "Revora.Admin", Timeout = 5, CommandTimeout = 10,
            MaxPoolSize = 5, Options = "-c default_transaction_read_only=on",
            IncludeErrorDetail = false
        }.ConnectionString;
    }

    public async Task<GameSnapshot> ReadAsync(CancellationToken cancellation)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellation);
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellation);
        await using (var check = new NpgsqlCommand("SELECT current_database()='revora6711_3d' AND inet_server_port()=25432 AND current_setting('transaction_read_only')='on'", connection, transaction))
            if (!Equals(await check.ExecuteScalarAsync(cancellation), true))
                throw new InvalidOperationException("Admin database identity or read-only guard failed.");
        var accounts = new List<AccountSummary>();
        await using (var command = new NpgsqlCommand("SELECT username,entityid,state FROM public.accounts ORDER BY lower(username)", connection, transaction))
        await using (var reader = await command.ExecuteReaderAsync(cancellation))
            while (await reader.ReadAsync(cancellation))
                accounts.Add(new(reader.GetString(0), reader.GetInt64(1), reader.GetInt16(2)));
        var characters = new List<CharacterSummary>();
        await using (var command = new NpgsqlCommand("SELECT uid,name,level,class,mapid,x,y,online FROM public.entities ORDER BY uid", connection, transaction))
        await using (var reader = await command.ExecuteReaderAsync(cancellation))
            while (await reader.ReadAsync(cancellation))
                characters.Add(new(reader.GetInt64(0), reader.GetString(1), reader.GetInt16(2), reader.GetInt16(3), reader.GetInt32(4), reader.GetInt32(5), reader.GetInt32(6), reader.GetInt16(7) != 0));
        await transaction.CommitAsync(cancellation);
        return new(accounts, characters);
    }
}
