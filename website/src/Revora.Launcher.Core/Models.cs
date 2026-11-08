using System.Text.Json.Serialization;

namespace Revora.Launcher.Core;

public sealed record SignedEnvelope(
    [property: JsonPropertyName("keyId")] string KeyId,
    [property: JsonPropertyName("algorithm")] string Algorithm,
    [property: JsonPropertyName("payload")] string Payload,
    [property: JsonPropertyName("signature")] string Signature);

public sealed record ClientFile(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("downloadUrl")] string? DownloadUrl,
    [property: JsonPropertyName("role")] string Role = "active",
    [property: JsonPropertyName("required")] bool Required = true,
    [property: JsonPropertyName("fileType")] string FileType = "application/octet-stream");

public sealed record ClientProfile(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("clientPatch")] int ClientPatch,
    [property: JsonPropertyName("serverHost")] string ServerHost,
    [property: JsonPropertyName("accountPort")] int AccountPort,
    [property: JsonPropertyName("gamePort")] int GamePort,
    [property: JsonPropertyName("executable")] string Executable,
    [property: JsonPropertyName("files")] IReadOnlyList<ClientFile> Files,
    [property: JsonPropertyName("channel")] string Channel = "testing",
    [property: JsonPropertyName("buildId")] string BuildId = "unapproved",
    [property: JsonPropertyName("launcherArguments")] IReadOnlyList<string>? LauncherArguments = null,
    [property: JsonPropertyName("minimumWindowsBuild")] int MinimumWindowsBuild = 19045,
    [property: JsonPropertyName("compatibilityVersion")] string CompatibilityVersion = "none");

public sealed record VerificationFailure(string Path, string Reason);

public sealed record VerificationResult(bool IsValid, IReadOnlyList<VerificationFailure> Failures);
