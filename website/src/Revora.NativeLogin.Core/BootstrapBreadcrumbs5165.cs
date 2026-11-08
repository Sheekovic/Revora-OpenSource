namespace Revora.NativeLogin.Core;

public sealed record BootstrapBreadcrumbResult(IReadOnlyList<int> Stages, bool Valid, string? FailureCategory);

public static class BootstrapBreadcrumbs5165
{
    public const string RelativePath = ".revora-owner-test/test4d-stage.bin";
    public const int LastClientStage = 13;

    public static void Reset(string workspace)
    {
        string path = Resolve(workspace);
        if (File.Exists(path))
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("The diagnostic breadcrumb file is a reparse point.");
            File.Delete(path);
        }
    }

    public static BootstrapBreadcrumbResult Read(string workspace)
    {
        string path = Resolve(workspace);
        if (!File.Exists(path)) return new([], true, null);
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length > LastClientStage) return new([], false, "breadcrumb-count-invalid");
        for (int i = 0; i < bytes.Length; i++)
            if (bytes[i] != i + 1) return new(bytes.Select(value => (int)value).ToArray(), false, "breadcrumb-sequence-invalid");
        return new(bytes.Select(value => (int)value).ToArray(), true, null);
    }

    private static string Resolve(string workspace)
    {
        string root = Path.GetFullPath(workspace);
        string control = Path.Combine(root, ".revora-owner-test");
        if (!Directory.Exists(control) || (File.GetAttributes(control) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("The tool-owned workspace control directory is unavailable.");
        return Path.Combine(control, "test4d-stage.bin");
    }
}

public static class BootstrapControlTrace5165
{
    public const string RelativePath = ".revora-owner-test/test4e-stage.bin";
    public const int LastClientStage = 11;

    public static void Reset(string workspace) => ResetOrRead(workspace, read: false).GetAwaiter().GetResult();
    public static BootstrapBreadcrumbResult Read(string workspace) => ResetOrRead(workspace, read: true).GetAwaiter().GetResult();

    private static Task<BootstrapBreadcrumbResult> ResetOrRead(string workspace, bool read)
    {
        string root = Path.GetFullPath(workspace), control = Path.Combine(root, ".revora-owner-test");
        if (!Directory.Exists(control) || (File.GetAttributes(control) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("The tool-owned workspace control directory is unavailable.");
        string path = Path.Combine(control, "test4e-stage.bin");
        if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("The Test 4e control trace is a reparse point.");
        if (!read)
        {
            if (File.Exists(path)) File.Delete(path);
            return Task.FromResult(new BootstrapBreadcrumbResult([], true, null));
        }
        if (!File.Exists(path)) return Task.FromResult(new BootstrapBreadcrumbResult([], true, null));
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length > LastClientStage) return Task.FromResult(new BootstrapBreadcrumbResult([], false, "control-trace-count-invalid"));
        for (int index = 0; index < bytes.Length; index++)
            if (bytes[index] != index + 1)
                return Task.FromResult(new BootstrapBreadcrumbResult(bytes.Select(value => (int)value).ToArray(), false, "control-trace-sequence-invalid"));
        return Task.FromResult(new BootstrapBreadcrumbResult(bytes.Select(value => (int)value).ToArray(), true, null));
    }
}

public sealed record BootstrapTraceEvent5165(int StageId, uint Sequence, uint ProcessId, uint ThreadId,
    long MonotonicTimestamp, int FileOrder);

public sealed record BootstrapTraceResult5165(IReadOnlyList<BootstrapTraceEvent5165> Events,
    bool TransportValid, bool PartialOrderValid, string? FailureCategory, string? FirstInvalidTransition,
    IReadOnlyList<int> DuplicateStageIds, IReadOnlyList<int> MissingStageIds,
    IReadOnlyList<int> UnexpectedStageIds, uint? DiagnosticChannelProcessId,
    bool StartupControlAttributed, bool WindowControlAttributed, string ControlAttributionResult);

public static class BootstrapControlTrace4f5165
{
    public const string RelativePath = ".revora-owner-test/test4f-events.bin";
    private const int RecordSize = 28;
    private const uint Magic = 0x46345652;
    private const int LastStage = 21;

    public static void Reset(string workspace)
    {
        string path = Resolve(workspace);
        if (File.Exists(path)) File.Delete(path);
    }

    public static BootstrapTraceResult5165 Read(string workspace, int expectedProcessId) =>
        Parse(File.Exists(Resolve(workspace)) ? File.ReadAllBytes(Resolve(workspace)) : [], checked((uint)expectedProcessId));

    public static BootstrapTraceResult5165 ReadTest4h(string workspace, int expectedProcessId)
    {
        string path = ResolveNamed(workspace, "test4h-events.bin");
        return Parse(File.Exists(path) ? File.ReadAllBytes(path) : [], checked((uint)expectedProcessId));
    }

    public static BootstrapTraceResult5165 ReadTest4i(string workspace, int expectedProcessId)
    {
        string path = ResolveNamed(workspace, "test4i-events.bin");
        return Parse(File.Exists(path) ? File.ReadAllBytes(path) : [], checked((uint)expectedProcessId));
    }

    public static BootstrapTraceResult5165 ReadTest4j(string workspace, int expectedProcessId)
    {
        string path = ResolveNamed(workspace, "test4j-events.bin");
        return Parse(File.Exists(path) ? File.ReadAllBytes(path) : [], checked((uint)expectedProcessId));
    }

    public static void ResetTest4h(string workspace)
    {
        string path = ResolveNamed(workspace, "test4h-events.bin");
        if (File.Exists(path)) File.Delete(path);
    }

    public static void ResetSupportedDiagnosticFiles(string workspace)
    {
        foreach (string name in DiagnosticFileInspection5165.SupportedNames)
        {
            string path = ResolveNamed(workspace, name);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    public static bool SelfTest()
    {
        byte[] records = BuildSelfTestRecords();
        BootstrapTraceResult5165 result = Parse(records, 4242);
        return result.TransportValid && result.PartialOrderValid && result.StartupControlAttributed &&
            result.WindowControlAttributed && result.Events.Select(item => item.StageId).SequenceEqual([1, 2, 4, 3]);
    }

    internal static BootstrapTraceResult5165 Parse(ReadOnlySpan<byte> data, uint expectedProcessId)
    {
        if (data.Length % RecordSize != 0 || data.Length > RecordSize * 128)
            return Failed("control-trace-record-size-invalid");
        var events = new List<BootstrapTraceEvent5165>(data.Length / RecordSize);
        for (int offset = 0, fileOrder = 0; offset < data.Length; offset += RecordSize, fileOrder++)
        {
            ReadOnlySpan<byte> record = data.Slice(offset, RecordSize);
            if (System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(record) != Magic)
                return Failed("control-trace-record-magic-invalid");
            events.Add(new(
                checked((int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(record[4..])),
                System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(record[8..]),
                System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(record[12..]),
                System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(record[16..]),
                System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(record[20..]), fileOrder));
        }

        BootstrapTraceEvent5165[] ordered = events.OrderBy(item => item.Sequence).ToArray();
        int[] duplicates = ordered.GroupBy(item => item.StageId).Where(group => group.Count() > 1).Select(group => group.Key).Order().ToArray();
        int[] unexpected = ordered.Where(item => item.StageId is < 1 or > 25).Select(item => item.StageId).Distinct().Order().ToArray();
        int[] missing = Enumerable.Range(1, 19).Except(ordered.Select(item => item.StageId)).ToArray();
        bool sequenceValid = ordered.Select((item, index) => item.Sequence == index + 1).All(value => value);
        uint[] pids = ordered.Select(item => item.ProcessId).Distinct().ToArray();
        uint? channelPid = pids.Length == 1 ? pids[0] : null;
        bool pidValid = channelPid == expectedProcessId;
        (bool partialOrder, string? firstInvalid) = ValidatePartialOrder(ordered);
        bool structural = sequenceValid && duplicates.Length == 0 && unexpected.Length == 0 && pidValid;
        bool startup = ordered.Any(item => item.StageId == 1 && item.ProcessId == expectedProcessId);
        bool window = ordered.Any(item => item.StageId == 3 && item.ProcessId == expectedProcessId);
        string attribution = !startup ? "startup-control-not-received-from-launched-pid" :
            !window ? "window-control-not-received-from-launched-pid" :
            !pidValid ? "diagnostic-record-pid-mismatch" : "both-controls-attributed-to-launched-pid";
        string? category = !sequenceValid ? "control-trace-monotonic-sequence-invalid" :
            duplicates.Length != 0 ? "control-trace-duplicate-stage" : unexpected.Length != 0 ? "control-trace-unexpected-stage" :
            !pidValid ? "control-trace-pid-attribution-failed" : !partialOrder ? "control-trace-partial-order-invalid" : null;
        return new(ordered, structural, partialOrder, category, firstInvalid, duplicates, missing, unexpected,
            channelPid, startup, window, attribution);

        BootstrapTraceResult5165 Failed(string category) => new([], false, false, category, null, [],
            Enumerable.Range(1, 19).ToArray(), [], null, false, false, "diagnostic-transport-invalid");
    }

    private static (bool Valid, string? FirstInvalid) ValidatePartialOrder(IReadOnlyList<BootstrapTraceEvent5165> events)
    {
        var seen = new HashSet<int>();
        foreach (BootstrapTraceEvent5165 item in events)
        {
            int[] required = item.StageId switch
            {
                2 => [1], 3 => [2], 4 => [2], 5 => [4], 6 => [5], 7 => [6], 8 => [7],
                9 => [8], 10 => [9], 11 => [10], 12 => [11], 13 => [12], 14 => [13],
                15 => [14], 16 => [15], 17 => [16], 18 => [17], 19 => [18],
                20 => [17, 24], 21 => [20], 22 or 23 => [21], 24 or 25 => [16], _ => [],
            };
            int missing = required.FirstOrDefault(requiredStage => !seen.Contains(requiredStage));
            if (missing != 0) return (false, $"stage-{item.StageId}-before-stage-{missing}");
            seen.Add(item.StageId);
        }
        return (true, null);
    }

    private static byte[] BuildSelfTestRecords()
    {
        int[] stages = [1, 2, 4, 3];
        byte[] bytes = new byte[stages.Length * RecordSize];
        for (int index = 0; index < stages.Length; index++)
        {
            Span<byte> record = bytes.AsSpan(index * RecordSize, RecordSize);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(record, Magic);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(record[4..], checked((uint)stages[index]));
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(record[8..], checked((uint)(index + 1)));
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(record[12..], 4242);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(record[16..], index % 2 == 0 ? 101U : 202U);
            System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(record[20..], 10_000 + index);
        }
        return bytes;
    }

    private static string Resolve(string workspace)
        => ResolveNamed(workspace, "test4f-events.bin");

    private static string ResolveNamed(string workspace, string name)
    {
        string root = Path.GetFullPath(workspace), control = Path.Combine(root, ".revora-owner-test");
        if (!Directory.Exists(control) || (File.GetAttributes(control) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("The tool-owned workspace control directory is unavailable.");
        string path = Path.Combine(control, name);
        if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("The Test 4f control trace is a reparse point.");
        return path;
    }
}

public sealed record StartupControlResult4g5165(bool MarkerPresent, bool EventPresent, bool MarkerValid,
    bool EventValid, uint? MarkerProcessId, uint? EventProcessId, uint? MarkerThreadId,
    uint? EventThreadId, long? MarkerTimestamp, long? EventTimestamp, string Classification);

public static class StartupControl4g5165
{
    public const string MarkerRelativePath = ".revora-owner-test/test4g-startup.bin";
    public const string EventRelativePath = ".revora-owner-test/test4g-events.bin";
    private const int RecordSize = 28;
    private const uint MarkerMagic = 0x474D5652;
    private const uint EventMagic = 0x47345652;

    public static void Reset(string workspace)
    {
        foreach (string relative in new[] { MarkerRelativePath, EventRelativePath })
        {
            string path = Resolve(workspace, relative);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    public static StartupControlResult4g5165 Read(string workspace, int expectedProcessId)
    {
        Record? marker = ReadRecord(Resolve(workspace, MarkerRelativePath), MarkerMagic);
        Record? transport = ReadRecord(Resolve(workspace, EventRelativePath), EventMagic);
        uint expected = checked((uint)expectedProcessId);
        bool markerValid = marker is { VersionOrStage: 1 } && marker.ProcessId == expected;
        bool eventValid = transport is { VersionOrStage: 1 } && transport.ProcessId == expected;
        string classification = !markerValid ? "patch-or-entry-stub-execution-failed" :
            !eventValid ? "client-to-launcher-transport-failed" : "startup-controls-proven";
        return new(marker is not null, transport is not null, markerValid, eventValid,
            marker?.ProcessId, transport?.ProcessId, marker?.ThreadId, transport?.ThreadId,
            marker?.Timestamp, transport?.Timestamp, classification);
    }

    private static Record? ReadRecord(string path, uint magic)
    {
        if (!File.Exists(path)) return null;
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length != RecordSize) return null;
        ReadOnlySpan<byte> value = bytes;
        if (System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(value) != magic) return null;
        return new(
            System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(value[4..]),
            System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(value[12..]),
            System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(value[16..]),
            System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(value[20..]));
    }

    private static string Resolve(string workspace, string relative)
    {
        string root = Path.GetFullPath(workspace), control = Path.Combine(root, ".revora-owner-test");
        if (!Directory.Exists(control) || (File.GetAttributes(control) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("The tool-owned workspace control directory is unavailable.");
        string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("A Test 4g startup control is a reparse point.");
        return path;
    }

    private sealed record Record(uint VersionOrStage, uint ProcessId, uint ThreadId, long Timestamp);
}

public sealed record StartupMarker4h5165(bool Present, bool Valid, uint? ProcessId, uint? ThreadId,
    long? Timestamp, string Classification);

public static class StartupMarker4h
{
    public const string RelativePath = ".revora-owner-test/test4h-startup.bin";
    private const uint Magic = 0x484D5652;

    public static void Reset(string workspace)
    {
        string path = Resolve(workspace);
        if (File.Exists(path)) File.Delete(path);
    }

    public static StartupMarker4h5165 Read(string workspace, int expectedProcessId)
    {
        string path = Resolve(workspace);
        if (!File.Exists(path)) return new(false, false, null, null, null, "startup-marker-missing");
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length != 28 || System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes) != Magic)
            return new(true, false, null, null, null, "startup-marker-malformed");
        uint version = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4));
        uint pid = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(12));
        uint thread = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(16));
        long timestamp = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(20));
        bool valid = version == 1 && pid == checked((uint)expectedProcessId) && thread != 0 && timestamp > 0;
        return new(true, valid, pid, thread, timestamp, valid ? "startup-marker-proven" : "startup-marker-attribution-failed");
    }

    private static string Resolve(string workspace)
    {
        string root = Path.GetFullPath(workspace), control = Path.Combine(root, ".revora-owner-test");
        if (!Directory.Exists(control) || (File.GetAttributes(control) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("The tool-owned workspace control directory is unavailable.");
        string path = Path.Combine(control, "test4h-startup.bin");
        if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("The Test 4h marker is a reparse point.");
        return path;
    }
}

public static class StartupMarker4i
{
    public const string RelativePath = ".revora-owner-test/test4i-startup.bin";
    private const uint Magic = 0x484D5652;

    public static StartupMarker4h5165 Read(string workspace, int expectedProcessId)
    {
        string path = Resolve(workspace);
        if (!File.Exists(path)) return new(false, false, null, null, null, "startup-marker-missing");
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length != 28 || System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes) != Magic)
            return new(true, false, null, null, null, "startup-marker-malformed");
        uint version = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4));
        uint pid = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(12));
        uint thread = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(16));
        long timestamp = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(20));
        bool valid = version == 1 && pid == checked((uint)expectedProcessId) && thread != 0 && timestamp > 0;
        return new(true, valid, pid, thread, timestamp, valid ? "startup-marker-proven" : "startup-marker-attribution-failed");
    }

    private static string Resolve(string workspace)
    {
        string control = Path.Combine(Path.GetFullPath(workspace), ".revora-owner-test");
        if (!Directory.Exists(control) || (File.GetAttributes(control) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("The tool-owned workspace control directory is unavailable.");
        string path = Path.Combine(control, "test4i-startup.bin");
        if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("The Test 4i startup marker is a reparse point.");
        return path;
    }
}

public static class StartupMarker4j
{
    public const string RelativePath = ".revora-owner-test/test4j-startup.bin";
    private const uint Magic = 0x484D5652;

    public static StartupMarker4h5165 Read(string workspace, int expectedProcessId)
    {
        string control = Path.Combine(Path.GetFullPath(workspace), ".revora-owner-test");
        if (!Directory.Exists(control) || (File.GetAttributes(control) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("The tool-owned workspace control directory is unavailable.");
        string path = Path.Combine(control, "test4j-startup.bin");
        if (!File.Exists(path)) return new(false, false, null, null, null, "startup-marker-missing");
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("The Test 4j startup marker is a reparse point.");
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length != 28 || System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes) != Magic)
            return new(true, false, null, null, null, "startup-marker-malformed");
        uint version = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4));
        uint pid = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(12));
        uint thread = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(16));
        long timestamp = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(20));
        bool valid = version == 1 && pid == checked((uint)expectedProcessId) && thread != 0 && timestamp > 0;
        return new(true, valid, pid, thread, timestamp, valid ? "startup-marker-proven" : "startup-marker-attribution-failed");
    }
}

public sealed record DiagnosticFilePresence5165(string Name, bool Present, long? Size,
    bool CurrentLaunchWindow, bool FirstRecordValid, bool ProcessIdMatches);

public static class DiagnosticFileInspection5165
{
    public static readonly string[] SupportedNames =
    [
        "test4j-startup.bin", "test4j-events.bin", "test4i-startup.bin", "test4i-events.bin", "test4h-startup.bin", "test4h-events.bin",
        "test4g-startup.bin", "test4g-events.bin", "test4f-events.bin"
    ];

    public static IReadOnlyList<DiagnosticFilePresence5165> Inspect(string workspace, int expectedProcessId,
        DateTimeOffset launchStartedUtc)
    {
        string control = Path.Combine(Path.GetFullPath(workspace), ".revora-owner-test");
        if (!Directory.Exists(control) || (File.GetAttributes(control) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("The tool-owned workspace control directory is unavailable.");
        return SupportedNames.Select(name => InspectOne(control, name, expectedProcessId, launchStartedUtc)).ToArray();
    }

    private static DiagnosticFilePresence5165 InspectOne(string control, string name, int expectedProcessId,
        DateTimeOffset launchStartedUtc)
    {
        string path = Path.Combine(control, name);
        if (!File.Exists(path)) return new(name, false, null, false, false, false);
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            return new(name, true, null, false, false, false);
        var info = new FileInfo(path);
        bool current = info.LastWriteTimeUtc >= launchStartedUtc.UtcDateTime.AddSeconds(-1) &&
            info.LastWriteTimeUtc <= DateTime.UtcNow.AddSeconds(1);
        byte[] first = new byte[Math.Min(28, checked((int)Math.Min(info.Length, 28)))];
        using (FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            _ = stream.Read(first);
        uint[] magics = [0x484D5652, 0x46345652, 0x474D5652, 0x47345652];
        bool valid = first.Length == 28 && magics.Contains(System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(first));
        bool pid = valid && System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(first.AsSpan(12)) == checked((uint)expectedProcessId);
        return new(name, true, info.Length, current, valid, pid);
    }
}
