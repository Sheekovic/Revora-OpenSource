using Revora.Launcher.Core;

if (args.Length != 5 || args[0] != "apply" || !int.TryParse(args[4], out int oldPid))
{
    Environment.ExitCode = 2;
    return;
}

try
{
    Uri manifestUri = new(args[1], UriKind.Absolute);
    if (manifestUri.Scheme != Uri.UriSchemeHttps || manifestUri.Host != LauncherPolicy.ServerHost)
        throw new InvalidDataException("The launcher release endpoint is not approved.");
    using var http = new HttpClient(new SocketsHttpHandler { AutomaticDecompression = System.Net.DecompressionMethods.All })
    { Timeout = TimeSpan.FromMinutes(10) };
    byte[] envelope = await http.GetByteArrayAsync(manifestUri);
    LauncherReleaseManifest release = new LauncherReleaseVerifier(TrustedProfileKeys.Production,
        LauncherPolicy.ProductionSigningKeyId).Verify(envelope);
    await new BootstrapperEngine(http).ApplyAsync(release, args[2], args[3], oldPid,
        TimeSpan.FromSeconds(20), CancellationToken.None);
    Environment.ExitCode = 0;
}
catch
{
    Environment.ExitCode = 1;
}
