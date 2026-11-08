using System.Security.Cryptography;

namespace Revora.NativeLogin.Core;

public sealed record BootstrapPatchChange(int Offset, string OriginalHex, string ReplacementHex, string Purpose, string Evidence);
public sealed record BootstrapPatchResult(byte[] Bytes, IReadOnlyList<BootstrapPatchChange> Changes);

/// <summary>Exact-hash patch for the reviewed 5165 client after its Test-3 Server.dat RSA modulus replacement.</summary>
public static class AutomaticAccountBootstrap5165
{
    public const string InputSha256 = "902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f";
    public const int TextVirtualSizeOffset = 0x230;
    public const int SelectServerCompletionCallOffset = 0x4a3f4;
    public const int StubOffset = 0x20c600;
    private static readonly byte[] OriginalCall = Convert.FromHexString("e8590d0000");
    private static readonly byte[] ReplacementCall = Convert.FromHexString("e807221c00");
    private static readonly byte[] OriginalStub = new byte[21];
    private static readonly byte[] ReplacementStub = Convert.FromHexString("e84debe3ff6a006880040000e8f327f0ff83c408c3");
    private static readonly byte[] OriginalTextVirtualSize = Convert.FromHexString("d2b52000");
    private static readonly byte[] ReplacementTextVirtualSize = Convert.FromHexString("00c02000");

    public static BootstrapPatchResult Apply(ReadOnlySpan<byte> input)
    {
        byte[] hash = SHA256.HashData(input);
        if (!CryptographicOperations.FixedTimeEquals(hash, Convert.FromHexString(InputSha256)))
            throw new CryptographicException("The automatic bootstrap input hash is not approved.");
        if (!input.Slice(TextVirtualSizeOffset, OriginalTextVirtualSize.Length).SequenceEqual(OriginalTextVirtualSize) ||
            !input.Slice(SelectServerCompletionCallOffset, OriginalCall.Length).SequenceEqual(OriginalCall) ||
            !input.Slice(StubOffset, OriginalStub.Length).SequenceEqual(OriginalStub))
            throw new InvalidDataException("The automatic bootstrap original bytes do not match the reviewed client.");

        byte[] output = input.ToArray();
        ReplacementTextVirtualSize.CopyTo(output, TextVirtualSizeOffset);
        ReplacementCall.CopyTo(output, SelectServerCompletionCallOffset);
        ReplacementStub.CopyTo(output, StubOffset);
        return new(output,
        [
            new(TextVirtualSizeOffset, Convert.ToHexString(OriginalTextVirtualSize).ToLowerInvariant(), Convert.ToHexString(ReplacementTextVirtualSize).ToLowerInvariant(),
                "Extend only the executable .text virtual size to its existing raw-data size so the reviewed stub bytes are loader-mapped.",
                "PE .text raw size is 0x20c000 while original virtual size is 0x20b5d2; the stub remains inside the existing section and before .rdata."),
            new(SelectServerCompletionCallOffset, Convert.ToHexString(OriginalCall).ToLowerInvariant(), Convert.ToHexString(ReplacementCall).ToLowerInvariant(),
                "Route the completed CMD_SELECTSERVER state reset through the exact bootstrap stub.",
                "Exact client dispatcher at VA 0x44a31f; observed Revora selection invokes this completion path."),
            new(StubOffset, Convert.ToHexString(OriginalStub).ToLowerInvariant(), Convert.ToHexString(ReplacementStub).ToLowerInvariant(),
                "Preserve the original login-state reset, then post native message 0x480 once.",
                "Exact client CMD_GAMESTART branch posts 0x480 through VA 0x50ee04; main handler calls VA 0x44cda9 and the 1086 builder."),
        ]);
    }
}

/// <summary>
/// Exact-hash Test 4c transition. It runs once at the normal login-surface initialization boundary,
/// installs a fixed non-secret compatibility identity in the client's existing buffers, fixes the
/// reviewed Revora group/server selection, and uses the client's own 0x464 internal dispatch wrapper
/// to request Account start (internal command 0x480). It never contains a Revora account credential.
/// </summary>
public static class AutomaticAccountBootstrap4c5165
{
    public const string InputSha256 = "902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f";
    public const string OutputSha256 = "8040023d599116c5db3d91eb3eb97065ec15521ed8b6a3a7ac6c3a28247ed310";
    public const int TextVirtualSizeOffset = 0x230;
    public const int LoginSurfaceCompletionCallOffset = 0x4afee;
    public const int StubOffset = 0x20c600;
    private static readonly byte[] OriginalCall = Convert.FromHexString("e8c1080000");
    private static readonly byte[] ReplacementCall = Convert.FromHexString("e80d161c00");
    private static readonly byte[] OriginalTextVirtualSize = Convert.FromHexString("d2b52000");
    private static readonly byte[] ReplacementTextVirtualSize = Convert.FromHexString("00c02000");
    private static readonly byte[] OriginalStub = new byte[113];
    private static readonly byte[] ReplacementStub = Convert.FromHexString(
        "516a00e8acf2e3ff59c781fc00000000000000c7810001000000000000518d8904010000" +
        "6865c660006858c66000e87f63dfff59518d89600100006866c66000e80a18f5ff596a00" +
        "6880040000e8b227f0ff83c408c204007265766f72615f62726964676500627269646765" +
        "3531363500");

    public static BootstrapPatchResult Apply(ReadOnlySpan<byte> input)
    {
        byte[] hash = SHA256.HashData(input);
        if (!CryptographicOperations.FixedTimeEquals(hash, Convert.FromHexString(InputSha256)))
            throw new CryptographicException("The Test 4c bootstrap input hash is not approved.");
        if (!input.Slice(TextVirtualSizeOffset, OriginalTextVirtualSize.Length).SequenceEqual(OriginalTextVirtualSize) ||
            !input.Slice(LoginSurfaceCompletionCallOffset, OriginalCall.Length).SequenceEqual(OriginalCall) ||
            !input.Slice(StubOffset, OriginalStub.Length).SequenceEqual(OriginalStub))
            throw new InvalidDataException("The Test 4c original bytes do not match the reviewed client.");

        byte[] output = input.ToArray();
        ReplacementTextVirtualSize.CopyTo(output, TextVirtualSizeOffset);
        ReplacementCall.CopyTo(output, LoginSurfaceCompletionCallOffset);
        ReplacementStub.CopyTo(output, StubOffset);
        byte[] outputHash = SHA256.HashData(output);
        if (!CryptographicOperations.FixedTimeEquals(outputHash, Convert.FromHexString(OutputSha256)))
            throw new CryptographicException("The deterministic Test 4c output hash did not match.");
        return new(output,
        [
            new(TextVirtualSizeOffset, Convert.ToHexString(OriginalTextVirtualSize).ToLowerInvariant(), Convert.ToHexString(ReplacementTextVirtualSize).ToLowerInvariant(),
                "Extend .text virtual size only to its existing raw-data size so the reviewed Test 4c stub is loader-mapped.",
                "The existing .text raw size is 0x20c000; the stub remains within it and before .rdata."),
            new(LoginSurfaceCompletionCallOffset, Convert.ToHexString(OriginalCall).ToLowerInvariant(), Convert.ToHexString(ReplacementCall).ToLowerInvariant(),
                "Route the one-time normal login-surface completion call through the exact Test 4c compatibility transition.",
                "VA 0x44abad loads selected group/server and account UI state; the replaced call at VA 0x44afee is its final normal UI completion call."),
            new(StubOffset, Convert.ToHexString(OriginalStub).ToLowerInvariant(), Convert.ToHexString(ReplacementStub).ToLowerInvariant(),
                "Preserve UI completion, set fixed non-secret compatibility buffers and reviewed selection, then invoke the client's genuine internal Account-start dispatcher once.",
                "VA 0x50ee04 posts Win32 message 0x464 to the client's internal HWND with command 0x480; the main handler sets the Account-start flag and later invokes VA 0x44cda9/0x522ea2."),
        ]);
    }
}

/// <summary>
/// Exact-hash Test 4d diagnostic. Fixed one-byte breadcrumbs are appended to a
/// tool-owned workspace file through imports already present in the client.
/// No dynamic code loading, payload capture, credentials, paths, or ticket data are emitted.
/// </summary>
public static class AutomaticAccountBootstrap4d5165
{
    public const string InputSha256 = AutomaticAccountBootstrap4c5165.InputSha256;
    public const string OutputSha256 = "842204b71208b88646797177f0a3a685bac9868d2e28ed8b1d85f7ce6dea0a83";
    private const int TextVirtualSizeOffset = 0x230;
    private const int LoginSurfaceCompletionCallOffset = 0x4afee;
    private const int StubOffset = 0x20c600;
    private const int CommandHandlerOffset = 0xd10f9;
    private const int AccountTransitionOffset = 0x4cda9;
    private const int EndpointHandoffOffset = 0x4d03c;
    private const int RequestBuilderOffset = 0x122ea2;
    private const int SocketConnectCallOffset = 0x122f87;
    private static readonly byte[] DiagnosticCode = Convert.FromHexString(
        "516a01e8b600000083c4046a00e8a3f2e3ff596a02e8a400000083c404c781fc0000000000000083b9fc000000000f85870000006a03e88300000083c404c781000100000000000083b90001000000756a6a04e86600000083c404518d89040100006880c760006872c76000e84163dfff596a05e84500000083c404518d89600100006880c76000e8c217f5ff596a06e82900000083c4046a07e81f00000083c4046a006880040000e85627f0ff83c4086a08e80600000083c404c204005589e5608b4d0883f901724483f90d773f490fab0db2c76000723568afc76000688bc76000ff15ccd5600083c40885c0741e89c3536a016a018d450850ff15a4d5600083c41053ff15b0d5600083c404615dc36a09e8a6ffffff83c404c786dccc0a0001000000e9d949ecff6a0ae88dffffff83c404b8b8d65e00e97006e4ff6a0be879ffffff83c404e85567f1ffc36a0ce869ffffff83c404b8d0196000e94567f1ff6a0de855ffffff83c404e8168af2ffc37265766f72615f6272696467650062726964676535313635002e7265766f72612d6f776e65722d746573742f7465737434642d73746167652e62696e0061620000000000");

    public static BootstrapPatchResult Apply(ReadOnlySpan<byte> input)
    {
        if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(input), Convert.FromHexString(InputSha256)))
            throw new CryptographicException("The Test 4d diagnostic input hash is not approved.");
        byte[] output = input.ToArray();
        var changes = new List<BootstrapPatchChange>();
        Change(output, TextVirtualSizeOffset, "d2b52000", "00c02000", "Map the existing text raw-data tail.", changes);
        Change(output, LoginSurfaceCompletionCallOffset, "e8c1080000", "e80d161c00", "Route the evidenced completion boundary through the diagnostic transition.", changes);
        Change(output, StubOffset, new string('0', DiagnosticCode.Length * 2), Convert.ToHexString(DiagnosticCode), "Emit fixed stages 1-8 and provide fixed stage trampolines.", changes);
        Change(output, CommandHandlerOffset, "c786dccc0a0001000000", "e913b613000000000000", "Record internal command 0x480 consumption before preserving the original state write.", changes);
        Change(output, AccountTransitionOffset, "b8b8d65e00", "e97cf91b00", "Record Account transition entry and preserve the original prologue instruction.", changes);
        Change(output, EndpointHandoffOffset, "e8615e0d00", "e8fdf61b00", "Record the resolved-endpoint handoff before the original builder call.", changes);
        Change(output, RequestBuilderOffset, "b8d0196000", "e9a7980e00", "Record request-builder entry and preserve the original prologue instruction.", changes);
        Change(output, SocketConnectCallOffset, "e8fb210100", "e8d6970e00", "Record the socket-connect attempt before the original call.", changes);
        if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(output), Convert.FromHexString(OutputSha256)))
            throw new CryptographicException("The deterministic Test 4d output hash did not match.");
        return new(output, changes);
    }

    private static void Change(byte[] output, int offset, string originalHex, string replacementHex, string purpose, List<BootstrapPatchChange> changes)
    {
        byte[] original = Convert.FromHexString(originalHex), replacement = Convert.FromHexString(replacementHex);
        if (original.Length != replacement.Length || offset < 0 || offset > output.Length - original.Length ||
            !output.AsSpan(offset, original.Length).SequenceEqual(original))
            throw new InvalidDataException("A Test 4d diagnostic original-byte assertion failed.");
        replacement.CopyTo(output, offset);
        changes.Add(new(offset, originalHex.ToLowerInvariant(), replacementHex.ToLowerInvariant(), purpose,
            "Exact SHA-256 client; address-independent patch metadata asserts every replaced byte."));
    }
}
