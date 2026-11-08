using System.Text.Json;

namespace Revora.Web;

public sealed class WebRuntimeState
{
    private int ready;
    private int maintenance;
    public bool IsReady => Volatile.Read(ref ready) == 1;
    public bool IsMaintenance => Volatile.Read(ref maintenance) == 1;
    public void MarkReady() => Interlocked.Exchange(ref ready, 1);
    public void SetMaintenance(bool enabled) => Interlocked.Exchange(ref maintenance, enabled ? 1 : 0);
}

public sealed class WebConsoleService(IHostApplicationLifetime lifetime, WebRuntimeState state) : IHostedService
{
    private int stopping;
    public Task StartAsync(CancellationToken cancellationToken)
    {
        Thread thread = new(ReadLoop) { IsBackground = true, Name = "Revora.Web.Console" };
        thread.Start();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Interlocked.Exchange(ref stopping, 1);
        return Task.CompletedTask;
    }

    private void ReadLoop()
    {
        while (Volatile.Read(ref stopping) == 0)
        {
            string? line = Console.In.ReadLine();
            if (line is null) break;
            Handle(line.Trim());
        }
    }

    private void Handle(string command)
    {
        switch (command)
        {
            case "help": Write("command.help", "help status health version database migration backup cache-clear maintenance-on maintenance-off stop"); break;
            case "status": Write("command.status", state.IsMaintenance ? "maintenance" : state.IsReady ? "ready" : "starting"); break;
            case "health": Write("command.health", state.IsMaintenance ? "process=alive readiness=degraded maintenance=enabled database=not-configured" : state.IsReady ? "process=alive readiness=ready database=not-configured" : "process=alive readiness=starting database=not-configured"); break;
            case "version": Write("command.version", typeof(WebConsoleService).Assembly.GetName().Version?.ToString() ?? "unknown"); break;
            case "database": Write("command.database", "not-configured"); break;
            case "migration": Write("command.migration", "automatic-migrations=disabled"); break;
            case "backup": Write("command.backup", "unavailable-without-durable-database"); break;
            case "cache-clear": Write("audit.cache-clear", "completed=no-mutable-cache"); break;
            case "maintenance-on": state.SetMaintenance(true); Write("audit.maintenance", "enabled"); break;
            case "maintenance-off": state.SetMaintenance(false); Write("audit.maintenance", "disabled"); break;
            case "stop": Write("audit.stop", "graceful-shutdown-requested"); lifetime.StopApplication(); break;
            case "": break;
            default: Write("command.rejected", "unknown-command"); break;
        }
    }

    private static void Write(string eventName, string message) => Console.Out.WriteLine(JsonSerializer.Serialize(new { timestamp = DateTimeOffset.UtcNow, severity = "Information", component = "Revora.Web.Console", eventName, message }));
}

public static class CustomConfiguration
{
    public static string? FindPath(string[] args)
    {
        for (int index = 0; index < args.Length; index++)
        {
            if (args[index] == "--config" && index + 1 < args.Length) return Validate(args[index + 1]);
            if (args[index].StartsWith("--config=", StringComparison.Ordinal)) return Validate(args[index][9..]);
        }
        return null;
    }

    private static string Validate(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate) || Path.IsPathRooted(candidate) || candidate.Contains("..", StringComparison.Ordinal) || !candidate.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("The configuration path must be a safe relative JSON path.");
        return candidate;
    }
}
