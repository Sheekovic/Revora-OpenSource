using System.Security.Cryptography;
using System.Text.Json;

namespace Revora.NativeLogin.Core;

public sealed record ConfigurationOnlyPatchAuditResult(int ChangeRecords, int AllowedBytes, int ActualChangedBytes);

/// <summary>Fail-closed audit for the reviewed patch-5165 Server.dat RSA trust-anchor replacement.</summary>
public static class ConfigurationOnlyPatchAudit5165
{
    public const string DonorSha256 = "e3a4570314a0e9c06cee62e41662cd745da811cfdc8c28a85fb626920155db5c";
    public const string OutputSha256 = "902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f";
    private static readonly int[] AllowedOffsets =
    [
        1435632,1435642,1435652,1435662,1435672,1435682,1435692,1435702,
        1435712,1435722,1435732,1435742,1435752,1435762,1435772,1435782,
        1435792,1435802,1435812,1435822,1435832,1435842,1435852,1435862,
        1435872,1435882,1435892,1435902,1435912,1435922,1435932,1435942,
        1435952,1435962,1435972,1435982,1435992,1435999,1436006,1436013,
        1436020,1436027,1436034,1436041,1436048,1436055,1436062,1436069,
        1436076,1436083,1436090,1436097,1436102,1436111,1436118,1436125,
        1436132,1436139,1436146,1436153,1436160,1436167,1436174,1436181,
    ];

    public static ConfigurationOnlyPatchAuditResult Verify(ReadOnlySpan<byte> donor, ReadOnlySpan<byte> output,
        ReadOnlySpan<byte> metadataUtf8)
    {
        RequireHash(donor, DonorSha256);
        RequireHash(output, OutputSha256);
        if (donor.Length != output.Length || donor.Length < 0x100 || donor[0] != 'M' || donor[1] != 'Z' ||
            output[0] != 'M' || output[1] != 'Z')
            throw new InvalidDataException("The reviewed x86 PE structure is unavailable.");

        using JsonDocument document = JsonDocument.Parse(metadataUtf8.ToArray());
        JsonElement root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1 ||
            root.GetProperty("inputSha256").GetString() != DonorSha256 ||
            root.GetProperty("outputSha256").GetString() != OutputSha256)
            throw new InvalidDataException("The configuration-only patch identity is not approved.");

        JsonElement.ArrayEnumerator changes = root.GetProperty("changes").EnumerateArray();
        var observedOffsets = new List<int>();
        foreach (JsonElement change in changes)
        {
            int offset = change.GetProperty("Offset").GetInt32();
            byte[] original = Convert.FromHexString(change.GetProperty("OriginalHex").GetString()!);
            byte[] replacement = Convert.FromHexString(change.GetProperty("ReplacementHex").GetString()!);
            if (original.Length != 4 || replacement.Length != 4 || offset < 0 || offset > donor.Length - 4 ||
                !donor.Slice(offset, 4).SequenceEqual(original) || !output.Slice(offset, 4).SequenceEqual(replacement))
                throw new InvalidDataException("A reviewed modulus immediate differs from the approved bytes.");
            observedOffsets.Add(offset);
        }

        if (observedOffsets.Count != AllowedOffsets.Length ||
            !observedOffsets.Order().SequenceEqual(AllowedOffsets.Order()))
            throw new InvalidDataException("The patch offset set is not the reviewed 64-word modulus set.");

        var allowedBytes = new HashSet<int>(AllowedOffsets.SelectMany(offset => Enumerable.Range(offset, 4)));
        int actual = 0;
        for (int index = 0; index < donor.Length; index++)
        {
            if (donor[index] == output[index]) continue;
            actual++;
            if (!allowedBytes.Contains(index))
                throw new InvalidDataException("Executable bytes outside the reviewed RSA modulus changed.");
        }
        if (actual == 0) throw new InvalidDataException("The configuration trust anchor was not replaced.");
        return new(AllowedOffsets.Length, allowedBytes.Count, actual);
    }

    private static void RequireHash(ReadOnlySpan<byte> bytes, string expected)
    {
        Span<byte> actual = stackalloc byte[32];
        SHA256.HashData(bytes, actual);
        if (!CryptographicOperations.FixedTimeEquals(actual, Convert.FromHexString(expected)))
            throw new CryptographicException("The exact reviewed client hash did not match.");
    }
}
