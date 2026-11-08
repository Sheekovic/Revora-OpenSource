using System.Text.Json;
using Revora.ClientEndpoint;

try
{
    object result = args switch
    {
        ["inspect", var path] => ClientFiles.Inspect(path),
        ["apply", var path, var ip, var port] => ClientFiles.Apply(path, ip, int.Parse(port, System.Globalization.CultureInfo.InvariantCulture)),
        ["restore", var path] => ClientFiles.Restore(path),
        ["dat-inspect", var exe, var dat] => ServerDatFiles.Inspect(exe, dat),
        ["dat-apply", var exe, var dat, var id, var name, var ip, var port] => ServerDatFiles.Apply(exe, dat,
            uint.Parse(id, System.Globalization.CultureInfo.InvariantCulture), name, ip, int.Parse(port, System.Globalization.CultureInfo.InvariantCulture)),
        ["dat-restore", var exe, var dat] => ServerDatFiles.Restore(exe, dat),
        _ => throw new ArgumentException("Usage: inspect <exe> | apply <exe> <IPv4> <account-port> | restore <exe> | dat-inspect <exe> <dat> | dat-apply <exe> <dat> <server-id> <name> <IPv4> <account-port> | dat-restore <exe> <dat>")
    };
    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}
