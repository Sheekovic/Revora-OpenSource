using System.Text.Json;
using Revora.ClientEndpoint;

try
{
    object result = args switch
    {
        ["inspect", var path] => ClientFiles.Inspect(path),
        ["apply", var path, var ip, var port] => ClientFiles.Apply(path, ip, int.Parse(port, System.Globalization.CultureInfo.InvariantCulture)),
        ["restore", var path] => ClientFiles.Restore(path),
        _ => throw new ArgumentException("Usage: inspect <Conquer.exe> | apply <Conquer.exe> <IPv4> <account-port> | restore <Conquer.exe>")
    };
    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}
