using System.Text.Json;

namespace Revora.Host.Logging;

public sealed class SafeConsoleLog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly TextWriter _standardOutput;
    private readonly TextWriter _standardError;

    public SafeConsoleLog(TextWriter standardOutput, TextWriter standardError)
    {
        _standardOutput = standardOutput;
        _standardError = standardError;
    }

    public void Info(string component, string eventName, string message) =>
        Write(_standardOutput, "Information", component, eventName, message);

    public void Warning(string component, string eventName, string message) =>
        Write(_standardOutput, "Warning", component, eventName, message);

    public void Error(string component, string eventName, string message) =>
        Write(_standardError, "Error", component, eventName, message);

    public void Audit(string eventName, string message) =>
        Write(_standardOutput, "Audit", "Console", eventName, message);

    public void ReadyMarker() => _standardOutput.WriteLine("[Revora] READY");

    public void StoppedMarker() => _standardOutput.WriteLine("[Revora] STOPPED");

    private static void Write(
        TextWriter writer,
        string severity,
        string component,
        string eventName,
        string message)
    {
        var entry = new
        {
            timestamp = DateTimeOffset.UtcNow,
            severity,
            component,
            eventName,
            message,
        };

        writer.WriteLine(JsonSerializer.Serialize(entry, JsonOptions));
    }
}

