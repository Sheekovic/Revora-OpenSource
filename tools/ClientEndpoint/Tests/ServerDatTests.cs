using System.Buffers.Binary;
using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Revora.ClientEndpoint;

internal static class ServerDatTests
{
    internal static void Run(byte[] baseFixture, string artifacts, Action<bool, string> check, Action<Action, string> refuses)
    {
        using RSA originalKey = RSA.Create(2048);
        using RSA otherKey = RSA.Create(2048);
        byte[] modulus = originalKey.ExportParameters(false).Modulus!;
        byte[] fixture = baseFixture.ToArray();
        // Synthetic constructor with the reviewed stack layout, never proprietary client bytes.
        uint[] words = Enumerable.Range(0, 64).Select(i => BinaryPrimitives.ReadUInt32BigEndian(modulus.AsSpan(i * 4, 4))).ToArray();
        uint[] encoded = new uint[64]; encoded[63] = words[16];
        for (int i = 62; i >= 0; i--) encoded[i] = words[(17 + i) % 64] ^ encoded[i + 1];
        for (int i = 0; i < 64; i++)
        {
            int offset = ServerDatCodec.WordOffsets[i], start = offset - (i < 37 ? 6 : 3);
            fixture[start] = 0xc7; fixture[start + 1] = i < 37 ? (byte)0x85 : (byte)0x45;
            if (i < 37) BinaryPrimitives.WriteInt32LittleEndian(fixture.AsSpan(start + 2, 4), -276 + i * 4);
            else fixture[start + 2] = unchecked((byte)(-276 + i * 4));
            BinaryPrimitives.WriteUInt32LittleEndian(fixture.AsSpan(offset, 4), encoded[i]);
        }
        check(ServerDatCodec.ReadModulus(fixture).SequenceEqual(modulus), "native constructor word order matches generated RSA modulus");
        byte[] rotated = ServerDatCodec.ReplaceModulus(fixture, otherKey.ExportParameters(false).Modulus!);
        check(ServerDatCodec.ReadModulus(rotated).SequenceEqual(otherKey.ExportParameters(false).Modulus!), "public modulus rotation round trip");
        var offsets = ServerDatCodec.WordOffsets.SelectMany(o => Enumerable.Range(o, 4)).ToHashSet();
        check(fixture.Where((_, i) => !offsets.Contains(i)).SequenceEqual(rotated.Where((_, i) => !offsets.Contains(i))), "RSA rotation changes only 64 public immediates");

        byte[] xml = Encoding.UTF8.GetBytes(SampleXml);
        byte[] signed = ServerDatCodec.Encode(xml, originalKey);
        check(ServerDatCodec.Decode(signed, modulus).SequenceEqual(xml), "legacy RSA/gzip signature round trip");
        refuses(() => ServerDatCodec.Decode(signed, otherKey.ExportParameters(false).Modulus!), "mismatched executable/list signature refused");
        byte[] tampered = signed.ToArray(); tampered[17] ^= 0x42;
        refuses(() => ServerDatCodec.Decode(tampered, modulus), "tampered DAT signature refused");
        refuses(() => ServerDatCodec.Decode(signed[..^1], modulus), "partial RSA block refused");
        refuses(() => ServerDatCodec.Decode(modulus, modulus), "RSA representative at modulus refused");
        refuses(() => ServerDatCodec.Decode([], modulus), "empty DAT refused");
        refuses(() => ServerDatCodec.Decode(new byte[ServerDatCodec.MaximumCiphertext + 256], modulus), "oversized DAT refused");
        refuses(() => ServerDatCodec.Encode(new byte[ServerDatCodec.MaximumXml + 1], originalKey), "oversized plaintext refused");
        using (var compressed = new MemoryStream())
        {
            using (var gzip = new GZipStream(compressed, CompressionLevel.SmallestSize, true)) gzip.Write(new byte[ServerDatCodec.MaximumXml + 1]);
            byte[] bomb = IndependentSign(compressed.ToArray(), originalKey);
            refuses(() => ServerDatCodec.Decode(bomb, modulus), "signed gzip expansion bomb bounded");
        }
        byte[] edited = new ServerListDocument(xml).Edit(101, "Revora <&> العربية", "192.168.100.200", 65535);
        var parsed = new ServerListDocument(edited);
        check(parsed.Entries.Single(e => e.Id == 101) is { Address: "192.168.100.200", Port: 65535, Name: "Revora <&> العربية" }, "Unicode name, XML escaping and longest IPv4 edit");
        XElement sourceOther = XDocument.Parse(SampleXml).Descendants("row").Last();
        check(XNode.DeepEquals(sourceOther, XDocument.Parse(Encoding.UTF8.GetString(edited)).Descendants("row").Last()), "unselected row and attributes preserved");
        check(XDocument.Parse(Encoding.UTF8.GetString(edited)).Descendants("field").Single(e => e.Attribute("name")?.Value == "OwnerTag").Value == "Keep this", "unknown selected-row fields preserved");
        refuses(() => new ServerListDocument(xml).Edit(1, "Revora", "127.0.0.1", 9958), "category row cannot become a server");
        refuses(() => new ServerListDocument(xml).Edit(999, "Revora", "127.0.0.1", 9958), "unknown server ID refused");
        refuses(() => new ServerListDocument(xml).Edit(101, "Bad\nName", "127.0.0.1", 9958), "control characters in server name refused");
        refuses(() => new ServerListDocument(xml).Edit(101, new string('x', 51), "127.0.0.1", 9958), "server name schema bound enforced");
        refuses(() => new ServerListDocument(xml).Edit(101, "Revora", "127.00.0.1", 9958), "DAT address uses strict IPv4 validation");
        refuses(() => new ServerListDocument(xml).Edit(101, "Revora", "127.0.0.1", 0), "DAT port range enforced");
        refuses(() => new ServerListDocument(Encoding.UTF8.GetBytes("<!DOCTYPE mysqldump [<!ENTITY a 'x'>]>" + SampleXml)), "XML DTD and entities refused");
        refuses(() => new ServerListDocument(Encoding.UTF8.GetBytes(SampleXml.Replace("name=\"outenserver\"", "name=\"accounts\""))), "unrelated XML tables refused");
        refuses(() => new ServerListDocument(Encoding.UTF8.GetBytes(SampleXml.Replace("<field name=\"id\">101</field>", "<field name=\"id\">101</field><field name=\"id\">101</field>"))), "duplicate row fields refused");

        string fixtureHash = EndpointPatch.Hash(fixture);
        var profiles = new Dictionary<string, string> { [fixtureHash] = "Synthetic RSA fixture" };
        ClientState Inspect(byte[] bytes)
        {
            byte[] normalized = bytes.ToArray();
            foreach (int offset in ServerDatCodec.WordOffsets) fixture.AsSpan(offset, 4).CopyTo(normalized.AsSpan(offset, 4));
            ClientState state = EndpointPatch.Inspect(normalized, profiles);
            byte[] currentModulus = ServerDatCodec.ReadModulus(bytes);
            return state with { CurrentSha256 = EndpointPatch.Hash(bytes), ServerListKeyChanged = !currentModulus.SequenceEqual(modulus) };
        }
        byte[] hooked = EndpointPatch.Patch(fixture, "127.0.0.1", 19558, profiles).Bytes;
        (string Exe, string Dat) Fresh()
        {
            string directory = Path.Combine(artifacts, "pair-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            string exe = Path.Combine(directory, "synthetic-client.bin"), dat = Path.Combine(directory, "synthetic-list.bin");
            File.WriteAllBytes(exe, hooked); File.WriteAllBytes(dat, signed); return (exe, dat);
        }
        var pair = Fresh();
        var stale = new ServerListState(Inspect(hooked), EndpointPatch.Hash(signed), new ServerListDocument(xml).Entries);
        PairChange first = ServerDatFiles.ApplyCore(pair.Exe, pair.Dat, 101, "Revora Test", "192.168.100.200", 65535, true, Inspect);
        byte[] afterExe = File.ReadAllBytes(pair.Exe), afterDat = File.ReadAllBytes(pair.Dat), keyBytes = File.ReadAllBytes(first.KeyFile);
        check(Inspect(afterExe) is { ServerListKeyChanged: true, Patched: false }, "paired edit selects signed-list routing and removes endpoint override");
        check(new ServerListDocument(ServerDatCodec.Decode(afterDat, ServerDatCodec.ReadModulus(afterExe))).Entries.Single(e => e.Id == 101).Name == "Revora Test", "paired saved files verify together");
        refuses(() => ServerDatFiles.ApplyCore(pair.Exe, pair.Dat, 101, "Stale", "127.0.0.1", 9958, true, Inspect, expected: stale), "stale UI revision refused under pair locks");
        check(File.ReadAllBytes(pair.Exe).SequenceEqual(afterExe) && File.ReadAllBytes(pair.Dat).SequenceEqual(afterDat), "stale save does not modify either file");
        if (OperatingSystem.IsWindows())
        {
            var security = new FileInfo(first.KeyFile).GetAccessControl();
            check(security.AreAccessRulesProtected && security.GetAccessRules(true, false, typeof(System.Security.Principal.SecurityIdentifier)).Count == 1, "new private key has a protected owner-only Windows ACL");
        }
        ServerDatFiles.ApplyCore(pair.Exe, pair.Dat, 102, "Another", "10.0.0.10", 9958, true, Inspect);
        check(File.ReadAllBytes(first.KeyFile).SequenceEqual(keyBytes), "operator signing key reused without overwrite");
        ServerDatFiles.RestoreCore(pair.Exe, pair.Dat, Inspect);
        check(File.ReadAllBytes(pair.Exe).SequenceEqual(afterExe) && File.ReadAllBytes(pair.Dat).SequenceEqual(afterDat), "latest paired restore is byte exact");
        ServerDatFiles.RestoreCore(pair.Exe, pair.Dat, Inspect);
        check(File.ReadAllBytes(pair.Exe).SequenceEqual(hooked) && File.ReadAllBytes(pair.Dat).SequenceEqual(signed), "earlier paired restore returns initial hook and list");
        refuses(() => ServerDatFiles.RestoreCore(pair.Exe, pair.Dat, Inspect), "repeated paired restore does not redo an edit");
        check(File.Exists(first.KeyFile), "restoration preserves the private signing key");

        foreach (int step in new[] { 1, 2 })
        {
            var failed = Fresh();
            refuses(() => ServerDatFiles.ApplyCore(failed.Exe, failed.Dat, 101, "Failure", "127.0.0.1", 9958, true, Inspect,
                checkpoint: n => { if (n == step) throw new IOException("Simulated I/O failure"); }), "injected pair write failure at step " + step);
            check(File.ReadAllBytes(failed.Exe).SequenceEqual(hooked) && File.ReadAllBytes(failed.Dat).SequenceEqual(signed), "automatic rollback restores both files at step " + step);
            var interrupted = Fresh();
            refuses(() => ServerDatFiles.ApplyCore(interrupted.Exe, interrupted.Dat, 101, "Power loss", "127.0.0.1", 9958, true, Inspect,
                checkpoint: n => { if (n == step) throw new IOException("Simulated power loss"); }, rollbackOnError: false), "simulated process interruption at step " + step);
            ServerDatFiles.RestoreCore(interrupted.Exe, interrupted.Dat, Inspect);
            check(File.ReadAllBytes(interrupted.Exe).SequenceEqual(hooked) && File.ReadAllBytes(interrupted.Dat).SequenceEqual(signed), "Prepared journal recovers interruption at step " + step);
        }
        var corrupt = Fresh(); ServerDatFiles.ApplyCore(corrupt.Exe, corrupt.Dat, 101, "Backup test", "127.0.0.1", 9958, true, Inspect);
        byte[] current = File.ReadAllBytes(corrupt.Dat); current[0] ^= 1; File.WriteAllBytes(corrupt.Dat, current);
        refuses(() => ServerDatFiles.RestoreCore(corrupt.Exe, corrupt.Dat, Inspect), "unrelated DAT changes block restore");
        check(File.ReadAllBytes(corrupt.Dat).SequenceEqual(current), "restore does not overwrite unrelated DAT changes");
        current[0] ^= 1; File.WriteAllBytes(corrupt.Dat, current);
        string backup = Path.Combine(Path.GetDirectoryName(corrupt.Exe)!, ".revora-endpoint", "backups", EndpointPatch.Hash(signed) + ".bin");
        File.WriteAllBytes(backup, [0x42]);
        refuses(() => ServerDatFiles.RestoreCore(corrupt.Exe, corrupt.Dat, Inspect), "corrupt paired backup blocks restore");
        check(File.ReadAllBytes(corrupt.Dat).SequenceEqual(current), "corrupt backup refusal leaves current files intact");
        CryptographicOperations.ZeroMemory(keyBytes);
    }

    // Independent tiny signer for the decoder's decompression-limit test only.
    private static byte[] IndependentSign(byte[] compressed, RSA key)
    {
        RSAParameters p = key.ExportParameters(true);
        try
        {
            BigInteger n = new(p.Modulus!, true, true), d = new(p.D!, true, true);
            using var output = new MemoryStream();
            for (int offset = 0; offset < compressed.Length; offset += 245)
            {
                byte[] block = new byte[256]; int count = Math.Min(245, compressed.Length - offset);
                block[1] = 1; Array.Fill(block, (byte)255, 2, 253 - count); Array.Copy(compressed, offset, block, 256 - count, count);
                byte[] encoded = BigInteger.ModPow(new BigInteger(block, true, true), d, n).ToByteArray(true, true);
                byte[] padded = new byte[256]; encoded.CopyTo(padded, 256 - encoded.Length); output.Write(padded);
            }
            return output.ToArray();
        }
        finally { if (p.D is not null) CryptographicOperations.ZeroMemory(p.D); }
    }

    private const string SampleXml = """
        <mysqldump><database name="serverdat"><table_data name="outenserver">
        <row><field name="id">1</field><field name="ServerName">Group</field><field name="ServerIP">0</field><field name="ServerPort">0</field><field name="Child">1</field></row>
        <row><field name="id">101</field><field name="ServerName">Old</field><field name="ServerIP">127.0.0.1</field><field name="ServerPort">9958</field><field name="Child">0</field><field name="FlashName">Old</field><field name="OwnerTag">Keep this</field></row>
        <row custom="keep"><field name="id">102</field><field name="ServerName">Other</field><field name="ServerIP">10.0.0.10</field><field name="ServerPort">9959</field><field name="Child">0</field></row>
        </table_data></database></mysqldump>
        """;
}
