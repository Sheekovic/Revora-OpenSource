using Revora.Host.Hosting;
using Revora.Host.Logging;

namespace Revora.Host.Console;

public sealed class OperatorConsole
{
    private readonly RevoraCoordinator _coordinator;
    private readonly SafeConsoleLog _log;
    private readonly TextReader _input;
    private readonly TextWriter _output;

    public OperatorConsole(
        RevoraCoordinator coordinator,
        SafeConsoleLog log,
        TextReader input,
        TextWriter output)
    {
        _coordinator = coordinator;
        _log = log;
        _input = input;
        _output = output;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await _input.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                return;
            }

            await ExecuteAsync(line, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task ExecuteAsync(string line, CancellationToken cancellationToken)
    {
        var command = line.Trim().ToLowerInvariant();
        switch (command)
        {
            case "help":
                _output.WriteLine("Commands: help status health version players database migration backup-status save stop");
                break;
            case "status":
                _output.WriteLine($"Revora state: {_coordinator.State}");
                break;
            case "health":
                foreach (var item in _coordinator.GetHealth())
                {
                    _output.WriteLine($"{item.Component}: {item.State} - {item.Detail}");
                }

                break;
            case "version":
                _output.WriteLine($"Revora.Host {typeof(OperatorConsole).Assembly.GetName().Version}");
                break;
            case "players":
                _output.WriteLine($"Players online: {_coordinator.PlayerCount}");
                break;
            case "database":
            case "migration":
            case "backup-status":
                ComponentHealth persistence = _coordinator.GetHealth().Single(item => item.Component == "Persistence");
                _output.WriteLine($"Persistence: {persistence.State} - {persistence.Detail}");
                break;
            case "save":
                await _coordinator.SaveAsync(cancellationToken).ConfigureAwait(false);
                break;
            case "stop":
                _coordinator.RequestShutdown("stdin");
                break;
            case "":
                break;
            default:
                _log.Warning("Console", "UnknownCommand", "Unknown operator command rejected.");
                _output.WriteLine("Unknown command. Type 'help'.");
                break;
        }
    }
}
