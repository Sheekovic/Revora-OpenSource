using System.Security.Cryptography;
using System.Text;

namespace Revora.NativeLogin.Core;

public sealed record Test4jPatchAuditResult(string Sha256, string EventFilename, string MarkerFilename,
    bool StageOneBypassesPreCrtCounter, bool PostResultInstrumented, bool CommandConsumptionInstrumented,
    bool ReadinessGuardInstrumented);

public static class Test4jPatchAudit5165
{
    public const string ExpectedSha256 = "8f2d5d837cf894bfb362c900370f065d4b3c916dba7724af2a3184f3d8b8d4f2";

    public static Test4jPatchAuditResult ValidateExactExecutable(string path)
    {
        byte[] image = File.ReadAllBytes(path);
        string hash = Convert.ToHexString(SHA256.HashData(image)).ToLowerInvariant();
        if (hash != ExpectedSha256) throw new InvalidDataException("The Test 4j executable hash is not exact.");

        Require(image, 0x20c616, "e8e5030000909090909090", "sequence helper call");
        Require(image, 0x20ca00, "837d08017506b801000000c36800536900ff15c0d1600040c3", "pre/post-CRT sequence helper");
        Require(image, 0x10ee1b, "e8f9db0f0090", "PostMessage result wrapper");
        Require(image, 0x0d10f9, "e842b913009090909090", "command-consumption hook");
        Require(image, 0x0bcaf5, "e85bff14009090", "pending/readiness hook");
        Require(image, 0x20ca5e, "833d04536900007514c70504536900010000006a15", "one-shot pending diagnostic gate");

        string eventName = ReadAscii(image, 0x20c83f), markerName = ReadAscii(image, 0x20c997);
        if (eventName != ".revora-owner-test/test4j-events.bin" || markerName != ".revora-owner-test/test4j-startup.bin")
            throw new InvalidDataException("The managed reader and client writer paths differ.");
        return new(hash, eventName, markerName, true, true, true, true);
    }

    private static void Require(byte[] image, int offset, string expectedHex, string component)
    {
        byte[] expected = Convert.FromHexString(expectedHex);
        if (offset < 0 || offset > image.Length - expected.Length || !image.AsSpan(offset, expected.Length).SequenceEqual(expected))
            throw new InvalidDataException($"The reviewed {component} differs from the exact Test 4j payload.");
    }

    private static string ReadAscii(byte[] image, int offset)
    {
        int end = Array.IndexOf(image, (byte)0, offset);
        if (end < 0 || end - offset > 128) throw new InvalidDataException("A diagnostic filename is not bounded and terminated.");
        return Encoding.ASCII.GetString(image, offset, end - offset);
    }
}
