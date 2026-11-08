using System.Buffers.Binary;
using System.Text.Json;
using Revora.ClientEndpoint;

int passed = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }
void Refuses(Action action, string name)
{
    try { action(); } catch (Exception e) when (e is ArgumentException or InvalidDataException or IOException or InvalidOperationException or System.Security.Cryptography.CryptographicException or System.Xml.XmlException)
    { passed++; Console.WriteLine("PASS " + name); return; }
    throw new Exception("Expected refusal: " + name);
}
byte[] original = new byte[EndpointPatch.ClientSize];
void U16(int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(original.AsSpan(offset), value);
void U32(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(original.AsSpan(offset), value);
original[0] = (byte)'M'; original[1] = (byte)'Z'; U32(0x3c, 0x80);
"PE\0\0"u8.CopyTo(original.AsSpan(0x80)); U16(0x84, 0x14c); U16(0x86, 1); U16(0x94, 224); U16(0x98, 0x10b);
int section = 0x178; U32(section + 12, 0x1000); U32(section + 16, 0x800000); U32(section + 20, 0x400); U32(section + 36, 0x60000020);
Convert.FromHexString("5581ECA8000000").CopyTo(original, EndpointPatch.EntryOffset);
var profiles = new Dictionary<string, string> { [EndpointPatch.Hash(original)] = "Synthetic test fixture; never accepted by production" };
Check(!EndpointPatch.Inspect(original, profiles).Patched, "synthetic original recognized by test-only profile");
Refuses(() => EndpointPatch.Inspect(original), "production refuses synthetic fixture");
string artifacts = Path.GetFullPath(args.Length == 0 ? "tools/ClientEndpoint/artifacts/tests" : args[0]);
Directory.CreateDirectory(artifacts);
var cases = new List<object>();
foreach (var (ip, port) in new[] { ("127.0.0.1", 9958), ("192.168.100.200", 65535), ("1.1.1.1", 1), ("10.0.0.10", 19558) })
{
    var patch = EndpointPatch.Patch(original, ip, port, profiles);
    var state = EndpointPatch.Inspect(patch.Bytes, profiles);
    Check(state.Patched && state.Address == ip && state.Port == port, $"round trip {ip}:{port}");
    Check(EndpointPatch.Restore(patch.Bytes, profiles).Bytes.SequenceEqual(original), "restore entire image hash");
    Check(original.Where((b, i) => i < EndpointPatch.EntryOffset || (i >= EndpointPatch.EntryOffset + 7 && i < EndpointPatch.CaveOffset) || i >= EndpointPatch.CaveOffset + EndpointPatch.StubSize)
        .SequenceEqual(patch.Bytes.Where((b, i) => i < EndpointPatch.EntryOffset || (i >= EndpointPatch.EntryOffset + 7 && i < EndpointPatch.CaveOffset) || i >= EndpointPatch.CaveOffset + EndpointPatch.StubSize)), "only two reviewed spans change");
    var retarget = EndpointPatch.Patch(patch.Bytes, "10.10.10.10", 9959, profiles);
    Check(EndpointPatch.Inspect(retarget.Bytes, profiles).Address == "10.10.10.10", "retarget recognized output");
    string file = $"stub-{cases.Count}.bin";
    File.WriteAllBytes(Path.Combine(artifacts, file), [.. patch.Bytes.AsSpan(EndpointPatch.EntryOffset, 7), .. patch.Bytes.AsSpan(EndpointPatch.CaveOffset, EndpointPatch.StubSize)]);
    cases.Add(new { file, ip, port });
}
File.WriteAllText(Path.Combine(artifacts, "native-cases.json"), JsonSerializer.Serialize(cases));
foreach (string ip in new[] { "", "localhost", "::1", "1.2.3", "127.00.0.1", "256.1.2.3", "1.2.3.4\0", " 1.2.3.4", "0.0.0.0", "224.0.0.1", "255.255.255.255" })
    Refuses(() => EndpointPatch.Patch(original, ip, 9958, profiles), "reject invalid IPv4 " + ip.Replace('\0', '?'));
foreach (int port in new[] { -1, 0, 65536 }) Refuses(() => EndpointPatch.Patch(original, "127.0.0.1", port, profiles), "invalid port");
Refuses(() => EndpointPatch.Inspect(original[..100], profiles), "truncated image");
byte[] changed = EndpointPatch.Patch(original, "127.0.0.1", 9958, profiles).Bytes;
changed[1000] ^= 1;
Refuses(() => EndpointPatch.Inspect(changed, profiles), "outside patch tampering");
changed[1000] ^= 1; changed[EndpointPatch.CaveOffset + 2] ^= 1;
Refuses(() => EndpointPatch.Inspect(changed, profiles), "stub tampering");
var noExecute = original.ToArray(); noExecute[section + 39] = 0x40;
Refuses(() => EndpointPatch.Inspect(noExecute, profiles), "non-executable section");
Refuses(() => EndpointPatch.Restore(original, profiles), "restore original refuses redundant write");
string ioDirectory = Path.Combine(artifacts, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(ioDirectory);
string target = Path.Combine(ioDirectory, "fixture.bin"); File.WriteAllBytes(target, original);
var operation = ClientFiles.Change(target, b => EndpointPatch.Patch(b, "192.168.100.200", 9958, profiles));
Check(File.ReadAllBytes(operation.Backup).SequenceEqual(original), "backup is exact pre-operation image");
Check(EndpointPatch.Hash(File.ReadAllBytes(target)) == operation.AfterSha256, "atomic replacement matches result");
ClientFiles.Change(target, b => EndpointPatch.Restore(b, profiles));
Check(File.ReadAllBytes(target).SequenceEqual(original), "file restore exact");
File.WriteAllBytes(operation.Backup, [0]);
Refuses(() => ClientFiles.Change(target, b => EndpointPatch.Patch(b, "127.0.0.1", 9958, profiles)), "corrupt backup blocks replacement");
Check(File.ReadAllBytes(target).SequenceEqual(original), "failed operation leaves input unchanged");
if (OperatingSystem.IsWindows())
{
    string link = Path.Combine(ioDirectory, "junction");
    // Junction creation needs no developer-mode symlink privilege.
    using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe")
    { Arguments = $"/d /c mklink /J \"{link}\" \"{ioDirectory}\"", UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true });
    process!.WaitForExit();
    if (process.ExitCode != 0) throw new Exception("Could not create test junction");
    Refuses(() => ClientFiles.Inspect(Path.Combine(link, "fixture.bin")), "junction path refused");
    Directory.Delete(link);
}
ServerDatTests.Run(original, artifacts, Check, Refuses);
Console.WriteLine($"{passed} checks passed. Synthetic native cases: {artifacts}");
