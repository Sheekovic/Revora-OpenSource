using Revora.Host.Configuration;
using Revora.Host.Console;
using Revora.Host.Hosting;
using Revora.Host.Logging;
using System.Runtime.InteropServices;
using Revora.Persistence.PostgreSql;
using Revora.Host.Gameplay;

namespace Revora.Host;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        TextWriter standardOutput = TextWriter.Synchronized(System.Console.Out);
        TextWriter standardError = TextWriter.Synchronized(System.Console.Error);
        var log = new SafeConsoleLog(standardOutput, standardError);
        try
        {
            var configPath = ParseConfigPath(args);
            var options = await RevoraHostOptions.LoadAsync(configPath, CancellationToken.None);
            options.Validate();
            ValidateDirectories(configPath, options.SecretFile, options.DataDirectory, options.LogDirectory);

            IPersistenceRuntime persistence;
            IRevoraRuntimeComponent account;
            IRevoraRuntimeComponent game;
            IPlayerCountProvider players;
            if (string.Equals(options.RuntimeAdapter, "InMemory", StringComparison.OrdinalIgnoreCase))
            {
                var memory = new InMemoryPersistenceRuntime();
                var accountListener = new LoopbackTcpRuntimeComponent(
                    "AccountServer", options.AccountListener.ParseAddress(), options.AccountListener.Port,
                    options.MaximumPlayers);
                var gameListener = new LoopbackTcpRuntimeComponent(
                    "GameServer", options.GameListener.ParseAddress(), options.GameListener.Port,
                    options.MaximumPlayers);
                persistence = memory;
                account = accountListener;
                game = gameListener;
                players = gameListener;
            }
            else if (string.Equals(options.RuntimeAdapter, "PostgreSqlSyntheticTest", StringComparison.OrdinalIgnoreCase))
            {
                RevoraHostSecrets protectedSettings = RevoraHostSecrets.Load(options.SecretFile);
                if (protectedSettings.DatabaseMode != "LocalPeerSocket")
                {
                    throw new InvalidDataException("The reviewed local peer socket database mode is required.");
                }

                var store = new PostgreSqlGameDataStore(protectedSettings.PostgreSqlSocketPath, protectedSettings.PostgreSqlUsername, protectedSettings.PostgreSqlDatabase);
                var database = new PostgreSqlPersistenceRuntime(store);
                var accountListener = new SyntheticProtocolRuntimeComponent(
                    "AccountServer", options.AccountListener.ParseAddress(), options.AccountListener.Port,
                    options.MaximumPlayers, accountMode: true, store);
                var gameListener = new SyntheticProtocolRuntimeComponent(
                    "GameServer", options.GameListener.ParseAddress(), options.GameListener.Port,
                    options.MaximumPlayers, accountMode: false, store);
                persistence = database;
                account = accountListener;
                game = gameListener;
                players = gameListener;
            }
            else if (string.Equals(options.RuntimeAdapter, "PostgreSql5165", StringComparison.OrdinalIgnoreCase))
            {
                RevoraHostSecrets protectedSettings = RevoraHostSecrets.Load(options.SecretFile);
                if (protectedSettings.DatabaseMode != "LocalPeerSocket") throw new InvalidDataException("The reviewed local peer socket database mode is required.");
                var store = new PostgreSqlGameDataStore(protectedSettings.PostgreSqlSocketPath, protectedSettings.PostgreSqlUsername, protectedSettings.PostgreSqlDatabase);
                if (await store.MigrationVersionAsync(CancellationToken.None) < 46)
                {
                    await store.DisposeAsync();
                    throw new InvalidOperationException("PostgreSQL migration level is below the reviewed Host release minimum (V046).");
                }
                string gameplayConnection = PostgreSqlEconomyRuntime.BuildLocalPeerConnectionString(
                    protectedSettings.PostgreSqlSocketPath, protectedSettings.PostgreSqlUsername,
                    protectedSettings.PostgreSqlDatabase);
                ItemEconomyRuntime5165 economy = await ItemEconomyRuntime5165.CreateAsync(
                    gameplayConnection, CancellationToken.None);
                string mapsPath = Path.GetFullPath(Path.Combine(options.DataDirectory, "maps"));
                string reviewedMaps = Path.Combine(mapsPath, "reviewed-owner-maps.json");
                MapCatalog5165 maps = File.Exists(reviewedMaps)
                    ? MapCatalog5165.LoadReviewedOwnerCatalog(mapsPath, reviewedMaps)
                    : MapCatalog5165.LoadApprovedPortalSlice(mapsPath);
                var combat = new LiveCombatRuntime5165(gameplayConnection, log, maps);
                await combat.InitializeAsync(CancellationToken.None);
                var database = new PostgreSqlPersistenceRuntime(store, economy, combat);
                GameplayReferenceRows rows = await store.LoadPortalNpcReferencesAsync([1002, 1004], CancellationToken.None);
                var references = new GameplayReferenceCatalog5165(
                    rows.Portals.Select(row => new PortalReference5165(row.LegacyId, row.SourceMap, row.SourceX, row.SourceY,
                        row.DestinationMap, row.DestinationX, row.DestinationY)),
                    rows.Npcs.Select(row => new NpcReference5165(row.LegacyUid, row.Type, row.Flags, row.Face,
                        row.MapId, row.X, row.Y)));
                var gameplay = new GameplayWorld5165(maps, references, trace =>
                    log.Audit("PlayerPeerSpawn10014Encoded",
                        $"The canary peer presentation was encoded (reason={trace.Reason}; " +
                        $"sourceLegacyUid={trace.SourceLegacyUid}; recipientLegacyUid={trace.RecipientLegacyUid}; " +
                        $"wireUid={trace.WireUid}; headwear={trace.Headwear}; garment={trace.Garment}; " +
                        $"armor={trace.Armor}; leftHand={trace.LeftHand}; rightHand={trace.RightHand}; " +
                        $"steed={trace.Steed}; armorColor={trace.ArmorColor}; " +
                        $"leftHandColor={trace.LeftHandColor}; headwearColor={trace.HeadwearColor}; " +
                        $"plaintextHex={trace.PlaintextHex})."));
                var accountListener = new Genuine5165RuntimeComponent("AccountServer", options.AccountListener.ParseAddress(), options.AccountListener.Port,
                    Math.Min(options.MaximumPlayers, 32), true, options.AdvertisedGameAddress, options.GameListener.Port, options.OwnerSourceAddress, store, log);
                var gameListener = new Genuine5165RuntimeComponent("GameServer", options.GameListener.ParseAddress(), options.GameListener.Port,
                    Math.Min(options.MaximumPlayers, 32), false, options.AdvertisedGameAddress, options.GameListener.Port,
                    options.OwnerSourceAddress, store, log, gameplay, economy, combat);
                persistence = database; account = accountListener; game = gameListener; players = gameListener;
            }
            else
            {
                var unavailable = new UnavailableRuntimeComponent("Persistence");
                persistence = unavailable;
                account = new UnavailableRuntimeComponent("AccountServer");
                game = new UnavailableRuntimeComponent("GameServer");
                players = unavailable;
            }

            var coordinator = new RevoraCoordinator(persistence, account, game, players, log);

            using var signals = new CancellationTokenSource();
            PosixSignalRegistration? sigterm = null;
            PosixSignalRegistration? sigint = null;
            if (OperatingSystem.IsLinux())
            {
                sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
                {
                    context.Cancel = true;
                    coordinator.RequestShutdown("SIGTERM");
                });
                sigint = PosixSignalRegistration.Create(PosixSignal.SIGINT, context =>
                {
                    context.Cancel = true;
                    coordinator.RequestShutdown("SIGINT");
                });
            }

            using (sigterm)
            using (sigint)
            {
                await coordinator.StartAsync(signals.Token).ConfigureAwait(false);
                var operatorConsole = new OperatorConsole(
                    coordinator,
                    log,
                    System.Console.In,
                    standardOutput);
                var consoleTask = Task.Factory.StartNew(
                    () => operatorConsole.RunAsync(signals.Token),
                    CancellationToken.None,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default).Unwrap();
                await coordinator.WaitForShutdownRequestAsync(signals.Token).ConfigureAwait(false);
                log.Audit("ShutdownRequested", "A graceful shutdown request was accepted.");
                await coordinator.StopAsync(TimeSpan.FromSeconds(options.ShutdownTimeoutSeconds))
                    .ConfigureAwait(false);

                if (consoleTask.IsCompleted)
                {
                    await consoleTask.ConfigureAwait(false);
                }
            }

            return coordinator.UnexpectedFailure ? 1 : 0;
        }
        catch (Exception exception)
        {
            log.Error(
                "Host",
                "FatalStartupFailure",
                $"Startup failed safely ({exception.GetType().Name}). Review protected diagnostics locally.");
            return 1;
        }
    }

    private static string ParseConfigPath(string[] args)
    {
        if (args.Length == 0)
        {
            return "config/revora.json";
        }

        if (args.Length == 2 && string.Equals(args[0], "--config", StringComparison.Ordinal))
        {
            return args[1];
        }

        throw new ArgumentException("Only '--config <path>' is supported.");
    }

    private static void ValidateDirectories(string configPath, string secretPath, string dataDirectory, string logDirectory)
    {
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException("Configuration file not found.", configPath);
        }

        var requiredDirectories = new[] { "config", dataDirectory, logDirectory };
        foreach (var directory in requiredDirectories)
        {
            if (!Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException($"Required directory '{directory}' does not exist.");
            }
        }

        if (!File.Exists(secretPath))
        {
            throw new FileNotFoundException("Protected secret file not found.", secretPath);
        }

        if (!OperatingSystem.IsWindows())
        {
            var mode = File.GetUnixFileMode(secretPath);
            var forbidden = UnixFileMode.GroupRead | UnixFileMode.GroupWrite |
                UnixFileMode.GroupExecute | UnixFileMode.OtherRead |
                UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;
            if ((mode & forbidden) != 0)
            {
                throw new UnauthorizedAccessException("Secret file permissions must be 0600 or stricter.");
            }
        }
    }
}
