using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using Revora.Launcher.Core;
using Revora.NativeLogin.Core;

var tests = new (string, Func<Task>)[]
{
    ("Deterministic patch and separate copy", Prepare),
    ("Wrong signed source hash rejected", WrongHash),
    ("Unexpected Flash rejected", FlashRejected),
    ("Unexpected zftqat rejected", ZftqatRejected),
    ("Patch original bytes rejected", PatchBytesRejected),
    ("Unmarked staging preserved", UnmarkedStage),
    ("Probe binds loopback and redacts payload", ProbeLoopback),
    ("Probe handles fragmented and coalesced frames", ProbeFraming),
    ("Probe rejects oversized frames", ProbeOversized),
    ("Account cipher known initial header", AccountCipherKnownHeader),
    ("Account cipher round trip and reset", AccountCipherRoundTrip),
    ("Account cipher fragmented input", AccountCipherFragmented),
    ("Test 3b encrypted request and controlled response", Probe3bResponse),
    ("Test 3b rejects invalid encrypted header", Probe3bInvalidHeader),
    ("Test 3b rejects truncated encrypted frame", Probe3bTruncated),
    ("Account cipher resets independently per connection", AccountCipherConnectionReset),
    ("Automatic bootstrap rejects unknown executable", AutomaticBootstrapRejectsUnknown),
    ("Test 4c bootstrap rejects unknown executable", AutomaticBootstrap4cRejectsUnknown),
    ("Test 4d diagnostic rejects unknown executable", AutomaticBootstrap4dRejectsUnknown),
    ("Test 4d breadcrumbs accept one ordered stage each", Bootstrap4dBreadcrumbs),
    ("Test 4e startup and window controls validate in strict order", Bootstrap4eControlTrace),
    ("Test 4f self-test accepts cross-thread partial order", Bootstrap4fSelfTest),
    ("Test 4f trace binds PID and reports invalid transitions", Bootstrap4fAttribution),
    ("Test 4g independent startup controls classify boundaries", Bootstrap4gControls),
    ("Test 4h startup marker rejects stale and wrong PID evidence", Bootstrap4hMarker),
    ("Test 4i resets legacy diagnostics and attributes current files", Bootstrap4iControls),
    ("Exact Test 4i generated PE payload passes structural audit", Test4iExactPayloadAudit),
    ("Test 4j accepts strict sequence and bounded post-wrapper stages", Bootstrap4jBoundaryTrace),
    ("Exact Test 4j generated PE payload passes structural audit", Test4jExactPayloadAudit),
    ("Owner workspace is created once and reused", OwnerWorkspaceReused),
    ("Known Test 4c workspace state is recognized", KnownTest4cWorkspaceState),
    ("Schema-1 marker canonical checksum upgrades in place", Schema1MarkerUpgrade),
    ("Unknown controlled hash is rejected", UnknownControlledHash),
    ("Interruption before replacement preserves workspace", InterruptedUpgradePreservesWorkspace),
    ("Replacement failure rolls controlled files back", ReplacementFailureRollsBack),
    ("Early validation failure creates sanitized report", EarlyFailureReport),
    ("Root classification distinguishes donor workspace and unknown", RootClassification),
    ("Unmarked owner workspace is preserved", UnmarkedOwnerWorkspacePreserved),
    ("Bridge binds expected PID and consumes ticket once", BootstrapBridgeConsumesOnce),
    ("Bridge rejects foreign PID and revokes ticket", BootstrapBridgeRejectsForeignPid),
    ("Test 4b message boundary is exact and Windows-only", BootstrapMessageBoundary),
    ("M1 exact configuration-only difference audit", ManualBaselineExactDifferenceAudit),
    ("M1 fragmented 276/1086 receives controlled 1055", ManualBaselineControlledResponse),
    ("M1 rejects a foreign client PID", ManualBaselineRejectsForeignPid),
    ("M1 bounded timeout cleans up", ManualBaselineTimeout),
    ("M2 exact 276/1086 credential fields parse and zero", M2LoginLayout),
    ("M2 successful 32/1055 response layout", M2SuccessLayout),
    ("Exact client 1055 parser rejects 127 before connect", M2ExactClientEndpointGuard),
    ("M2 exact 28/1052 Game connect layout", M2GameConnectLayout),
    ("5165 exact 104/1001 character registration layout", CharacterRegistrationLayout),
    ("5165 character registration rejects malformed fields", CharacterRegistrationRejection),
    ("5165 registration and entrance response encoders", CharacterResponseLayouts),
    ("5165 first-world action and map encoders", FirstWorldEntryLayouts),
    ("H1 deterministic DH and Blowfish fixture", H1HandshakeRoundTrip),
    ("H1 Blowfish standard known-answer vector", H1BlowfishKnownAnswer),
    ("H1 fragmented DH and Game frames", H1FragmentedHandshake),
    ("H1 rejects invalid DH public values", H1InvalidDhPublic),
    ("H1 rejects malformed Game footer", H1InvalidGameFooter),
    ("H1 probe fails closed for exact-client-rejected loopback endpoint", H1LoopbackProbe),
};
int failed = 0;
foreach ((string name, Func<Task> run) in tests) try { await run(); Console.WriteLine($"PASS {name}"); } catch (Exception error) { failed++; Console.Error.WriteLine($"FAIL {name}: {error.Message}"); }
Console.WriteLine($"Revora.NativeLoginTests: {tests.Length - failed}/{tests.Length} passed.");
return failed == 0 ? 0 : 1;

static async Task Prepare()
{
    Setup setup = await Create();
    PreparationResult result = await new Test3Preparation().PrepareAsync(setup.Source, setup.Destination, setup.Input, setup.Output, setup.Patch, setup.Server, default);
    Assert(result.Files == 3 && File.Exists(Path.Combine(setup.Destination, "Conquer.exe")) && !Directory.Exists(setup.Destination + ".staging"));
    Assert((await File.ReadAllBytesAsync(Path.Combine(setup.Destination, "Conquer.exe")))[2] == 9);
}

static async Task WrongHash() { Setup s = await Create(); ClientProfile bad = s.Input with { Files = s.Input.Files.Select(f => f.Path == "data.wdf" ? f with { Sha256 = new string('0', 64) } : f).ToArray() }; await Throws<InvalidDataException>(() => new Test3Preparation().PrepareAsync(s.Source, s.Destination, bad, s.Output, s.Patch, s.Server, default)); }
static async Task FlashRejected() { Setup s = await Create(); await File.WriteAllTextAsync(Path.Combine(s.Source, "Flash.ocx"), "forbidden"); await Throws<InvalidDataException>(() => new Test3Preparation().PrepareAsync(s.Source, s.Destination, s.Input, s.Output, s.Patch, s.Server, default)); }
static async Task ZftqatRejected() { Setup s = await Create(); Directory.CreateDirectory(Path.Combine(s.Source, "zftqat")); await Throws<InvalidDataException>(() => new Test3Preparation().PrepareAsync(s.Source, s.Destination, s.Input, s.Output, s.Patch, s.Server, default)); }
static async Task PatchBytesRejected() { Setup s = await Create(); JsonElement root = JsonDocument.Parse(await File.ReadAllTextAsync(s.Patch)).RootElement.Clone(); var invalid = new { schemaVersion = 1, inputSha256 = root.GetProperty("inputSha256").GetString(), outputSha256 = root.GetProperty("outputSha256").GetString(), changes = new[] { new { Offset = 2, OriginalHex = "ff", ReplacementHex = "09" } } }; await File.WriteAllTextAsync(s.Patch, JsonSerializer.Serialize(invalid)); await Throws<InvalidDataException>(() => new Test3Preparation().PrepareAsync(s.Source, s.Destination, s.Input, s.Output, s.Patch, s.Server, default)); }
static async Task UnmarkedStage() { Setup s = await Create(); Directory.CreateDirectory(s.Destination + ".staging"); await File.WriteAllTextAsync(Path.Combine(s.Destination + ".staging", "owner.txt"), "preserve"); await Throws<IOException>(() => new Test3Preparation().PrepareAsync(s.Source, s.Destination, s.Input, s.Output, s.Patch, s.Server, default)); Assert(File.Exists(Path.Combine(s.Destination + ".staging", "owner.txt"))); }
static async Task ProbeLoopback()
{
    await using var probe = new LoopbackProtocolProbe(0); probe.Start();
    using var client = new TcpClient(); await client.ConnectAsync(IPAddress.Loopback, probe.Port);
    byte[] canary = [8, 0, 0x1b, 4, 0x73, 0x65, 0x63, 0x72]; await client.GetStream().WriteAsync(canary); client.Close(); await Task.Delay(100);
    string report = Path.Combine(Temp(), "probe.json"); await probe.WriteSanitizedAsync(report, default); string json = await File.ReadAllTextAsync(report);
    Assert(json.Contains($"127.0.0.1:{probe.Port}", StringComparison.Ordinal) && json.Contains("1051", StringComparison.Ordinal) && !json.Contains("secr", StringComparison.Ordinal));
}

static async Task ProbeFraming()
{
    await using var probe = new LoopbackProtocolProbe(0); probe.Start();
    using var client = new TcpClient(); await client.ConnectAsync(IPAddress.Loopback, probe.Port);
    NetworkStream stream = client.GetStream();
    await stream.WriteAsync(new byte[] { 6, 0 });
    await Task.Delay(20);
    await stream.WriteAsync(new byte[] { 0x34, 0x12, 0xaa, 0xbb, 4, 0, 0x78, 0x56 });
    client.Close(); await Task.Delay(100);
    ProbeReport report = probe.Snapshot();
    Assert(report.Frames.Count == 2 && report.Frames[0].Length == 6 && report.Frames[0].Type == 0x1234 && report.Frames[1].Length == 4 && report.Frames[1].Type == 0x5678);
}

static async Task ProbeOversized()
{
    await using var probe = new LoopbackProtocolProbe(0); probe.Start();
    using var client = new TcpClient(); await client.ConnectAsync(IPAddress.Loopback, probe.Port);
    await client.GetStream().WriteAsync(new byte[] { 1, 16, 1, 0 });
    client.Close(); await Task.Delay(100);
    ProbeReport report = probe.Snapshot();
    Assert(report.Frames.Count == 1 && report.Frames[0].Length == 4097);
}

static Task AccountCipherKnownHeader()
{
    byte[] plaintext = new byte[4];
    new AccountCipher5165().DecryptClient(new byte[] { 0x15, 0x94, 0x56, 0x65 }, plaintext);
    Assert(plaintext.SequenceEqual(new byte[] { 0x14, 0x01, 0x3e, 0x04 }));
    return Task.CompletedTask;
}

static Task AccountCipherRoundTrip()
{
    byte[] plaintext = Enumerable.Range(0, 276).Select(value => (byte)value).ToArray();
    byte[] encrypted = new byte[plaintext.Length];
    byte[] recovered = new byte[plaintext.Length];
    var peer = new AccountCipher5165(); peer.EncryptClientForTest(plaintext, encrypted);
    new AccountCipher5165().DecryptClient(encrypted, recovered);
    Assert(recovered.SequenceEqual(plaintext));
    byte[] response = Enumerable.Range(0, 12).Select(value => (byte)(value + 7)).ToArray();
    byte[] responseEncrypted = new byte[response.Length]; byte[] responseRecovered = new byte[response.Length];
    var server = new AccountCipher5165(); server.EncryptServer(response, responseEncrypted);
    new AccountCipher5165().DecryptServerForTest(responseEncrypted, responseRecovered);
    Assert(responseRecovered.SequenceEqual(response));
    return Task.CompletedTask;
}

static Task AccountCipherFragmented()
{
    byte[] plaintext = Enumerable.Range(0, 276).Select(value => (byte)(value * 3)).ToArray();
    byte[] encrypted = new byte[plaintext.Length]; new AccountCipher5165().EncryptClientForTest(plaintext, encrypted);
    byte[] recovered = new byte[plaintext.Length]; var server = new AccountCipher5165();
    server.DecryptClient(encrypted.AsSpan(0, 1), recovered.AsSpan(0, 1));
    server.DecryptClient(encrypted.AsSpan(1, 3), recovered.AsSpan(1, 3));
    server.DecryptClient(encrypted.AsSpan(4), recovered.AsSpan(4));
    Assert(recovered.SequenceEqual(plaintext));
    return Task.CompletedTask;
}

static async Task Probe3bResponse()
{
    await using var probe = new AccountProbe3b(0); probe.Start(); probe.SetLoginProvenance("manual-in-game-login");
    using var client = new TcpClient(); await client.ConnectAsync(IPAddress.Loopback, probe.Port);
    byte[] request = new byte[AccountProbe3b.ExpectedLength];
    request[0] = 0x14; request[1] = 0x01; request[2] = 0x3e; request[3] = 0x04;
    RandomNumberGenerator.Fill(request.AsSpan(4));
    byte[] ciphertext = new byte[request.Length]; new AccountCipher5165().EncryptClientForTest(request, ciphertext);
    NetworkStream stream = client.GetStream();
    await stream.WriteAsync(ciphertext.AsMemory(0, 2)); await Task.Delay(10); await stream.WriteAsync(ciphertext.AsMemory(2));
    byte[] responseCiphertext = new byte[12]; await ReadExactly(stream, responseCiphertext);
    byte[] response = new byte[12]; new AccountCipher5165().DecryptServerForTest(responseCiphertext, response);
    Assert(response[0] == 12 && response[2] == 0x1f && response[3] == 0x04 && response[8] == 1);
    await Task.Delay(50); AccountProbeReport report = probe.Snapshot();
    Assert(report.DecryptedLength == 276 && report.DecryptedPacketId == 1086 && report.EncryptedBytes == 276 && report.ResponseSent && report.Provenance == "manual-in-game-login");
}

static async Task Probe3bInvalidHeader()
{
    await using var probe = new AccountProbe3b(0); probe.Start();
    using var client = new TcpClient(); await client.ConnectAsync(IPAddress.Loopback, probe.Port);
    await client.GetStream().WriteAsync(new byte[] { 0, 0, 0, 0 });
    client.Close(); await Task.Delay(50);
    AccountProbeReport report = probe.Snapshot();
    Assert(report.FailureCategory == "invalid-decrypted-header" && !report.ResponseSent);
}

static async Task Probe3bTruncated()
{
    await using var probe = new AccountProbe3b(0); probe.Start();
    using var client = new TcpClient(); await client.ConnectAsync(IPAddress.Loopback, probe.Port);
    byte[] plaintext = new byte[AccountProbe3b.ExpectedLength];
    plaintext[0] = 0x14; plaintext[1] = 0x01; plaintext[2] = 0x3e; plaintext[3] = 0x04;
    byte[] ciphertext = new byte[plaintext.Length]; new AccountCipher5165().EncryptClientForTest(plaintext, ciphertext);
    await client.GetStream().WriteAsync(ciphertext.AsMemory(0, 17));
    client.Close(); await Task.Delay(50);
    AccountProbeReport report = probe.Snapshot();
    Assert(report.FailureCategory == "truncated-frame" && !report.ResponseSent);
}

static Task AccountCipherConnectionReset()
{
    byte[] plaintext = new byte[AccountProbe3b.ExpectedLength];
    RandomNumberGenerator.Fill(plaintext);
    byte[] first = new byte[plaintext.Length], second = new byte[plaintext.Length];
    new AccountCipher5165().EncryptClientForTest(plaintext, first);
    new AccountCipher5165().EncryptClientForTest(plaintext, second);
    Assert(first.SequenceEqual(second));
    byte[] recovered = new byte[plaintext.Length];
    new AccountCipher5165().DecryptClient(first, recovered);
    Assert(recovered.SequenceEqual(plaintext));
    return Task.CompletedTask;
}

static Task H1HandshakeRoundTrip()
{
    using var server = NewHandshake();
    byte[] hello = server.CreateServerHandshake(Enumerable.Range(1, 23).Select(value => (byte)value).ToArray());
    using H1ClientFixture client = H1ClientFixture.Accept(hello);
    byte[] reply = client.CreateReply();
    Assert(server.ConsumeClientDh(reply));
    byte[] connect = client.CreateConnectPacket();
    Assert(server.ConsumeEncryptedGame(connect));
    GameHandshakeResult5165 result = server.Snapshot();
    Assert(result.State == GameHandshakeState5165.Completed && result.ServerHandshakeLength == 333 &&
        result.SharedSecretDerived && result.BlowfishActivated && result.GamePacketLength == 28 &&
        result.GamePacketId == 1052 && result.FooterValidation == "tqclient-valid");
    return Task.CompletedTask;
}

static Task H1BlowfishKnownAnswer()
{
    using var cipher = new BlowfishCfb64(new byte[8]);
    byte[] encrypted = new byte[8];
    cipher.EncryptServer(new byte[8], encrypted);
    Assert(Convert.ToHexString(encrypted) == "4EF997456198DD78");
    return Task.CompletedTask;
}

static Task H1FragmentedHandshake()
{
    using var server = NewHandshake();
    using H1ClientFixture client = H1ClientFixture.Accept(server.CreateServerHandshake(new byte[23]));
    byte[] reply = client.CreateReply();
    Assert(!server.ConsumeClientDh(reply.AsSpan(0, 1)));
    Assert(!server.ConsumeClientDh(reply.AsSpan(1, 10)));
    Assert(server.ConsumeClientDh(reply.AsSpan(11)));
    byte[] connect = client.CreateConnectPacket();
    Assert(!server.ConsumeEncryptedGame(connect.AsSpan(0, 4)));
    Assert(server.ConsumeEncryptedGame(connect.AsSpan(4)));
    return Task.CompletedTask;
}

static Task H1InvalidDhPublic()
{
    using var server = NewHandshake();
    using H1ClientFixture client = H1ClientFixture.Accept(server.CreateServerHandshake(new byte[23]), invalidPublic: true);
    Assert(!server.ConsumeClientDh(client.CreateReply()));
    Assert(server.Snapshot().FailureCategory == "client-public-key-range-invalid");
    return Task.CompletedTask;
}

static Task H1InvalidGameFooter()
{
    using var server = NewHandshake();
    using H1ClientFixture client = H1ClientFixture.Accept(server.CreateServerHandshake(new byte[23]));
    Assert(server.ConsumeClientDh(client.CreateReply()));
    byte[] connect = client.CreateConnectPacket(invalidFooter: true);
    Assert(!server.ConsumeEncryptedGame(connect));
    Assert(server.Snapshot().FailureCategory == "client-footer-invalid");
    return Task.CompletedTask;
}

static GameHandshake5165 NewHandshake() => new(
    Enumerable.Range(1, 32).Select(value => (byte)value).ToArray(),
    new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 },
    new byte[] { 9, 10, 11, 12, 13, 14, 15, 16 });

static async Task H1LoopbackProbe()
{
    var authentication = new FixtureH1Authenticator();
    await using var probe = new ManualPlayableH1Probe(new FixedOwner(77), authentication,
        TimeSpan.FromSeconds(5), 0, 0);
    probe.Start(); probe.BindExpectedProcess(77);
    using var account = new TcpClient(); await account.ConnectAsync(IPAddress.Loopback, probe.BoundAccountPort);
    byte[] login = new byte[AccountPackets5165.LoginLength];
    System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(login, 276);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(login.AsSpan(2), 1086);
    "fixture1"u8.CopyTo(login.AsSpan(4)); byte[] clear = new byte[16]; "Fixture5165!"u8.CopyTo(clear);
    LegacyPasswordCipher5165.EncryptForTest(clear, login.AsSpan(132, 16)); "Revora"u8.CopyTo(login.AsSpan(260));
    byte[] encryptedLogin = new byte[login.Length]; var accountPeer = new AccountCipher5165(); accountPeer.EncryptClientForTest(login, encryptedLogin);
    await account.GetStream().WriteAsync(encryptedLogin.AsMemory(0, 31)); await account.GetStream().WriteAsync(encryptedLogin.AsMemory(31));
    ManualPlayableH1Result result = await probe.Completion;
    Assert(result.Classification == "m2-h1-partial-or-failed" && !result.GameConnectionObserved &&
        result.FirstGamePacketLength is null && result.FirstGamePacketId is null && result.FailureCategory == "protocol-validation" &&
        result.TokenLifecycle == "revocation-attempted" && authentication.ConsumeCount == 1);
    CryptographicOperations.ZeroMemory(login); CryptographicOperations.ZeroMemory(clear); CryptographicOperations.ZeroMemory(encryptedLogin);
}

static Task AutomaticBootstrapRejectsUnknown()
{
    ThrowsSync<CryptographicException>(() => AutomaticAccountBootstrap5165.Apply(new byte[2_841_656]));
    return Task.CompletedTask;
}

static Task AutomaticBootstrap4cRejectsUnknown()
{
    ThrowsSync<CryptographicException>(() => AutomaticAccountBootstrap4c5165.Apply(new byte[2_841_656]));
    return Task.CompletedTask;
}

static Task AutomaticBootstrap4dRejectsUnknown()
{
    ThrowsSync<CryptographicException>(() => AutomaticAccountBootstrap4d5165.Apply(new byte[2_841_656]));
    return Task.CompletedTask;
}

static async Task Bootstrap4dBreadcrumbs()
{
    Setup setup = await Create();
    string workspace = Path.Combine(Path.GetDirectoryName(setup.Source)!, OwnerTestWorkspace.DirectoryName);
    Directory.CreateDirectory(Path.Combine(workspace, ".revora-owner-test"));
    string path = Path.Combine(workspace, ".revora-owner-test", "test4d-stage.bin");
    await File.WriteAllBytesAsync(path, Enumerable.Range(1, 13).Select(value => (byte)value).ToArray());
    BootstrapBreadcrumbResult valid = BootstrapBreadcrumbs5165.Read(workspace);
    Assert(valid.Valid && valid.Stages.Count == 13 && valid.Stages[^1] == 13);
    await File.WriteAllBytesAsync(path, new byte[] { 1, 2, 2 });
    BootstrapBreadcrumbResult duplicate = BootstrapBreadcrumbs5165.Read(workspace);
    Assert(!duplicate.Valid && duplicate.FailureCategory == "breadcrumb-sequence-invalid");
    BootstrapBreadcrumbs5165.Reset(workspace);
    Assert(!File.Exists(path));
}

static async Task Bootstrap4eControlTrace()
{
    Setup setup = await Create();
    string workspace = Path.Combine(Path.GetDirectoryName(setup.Source)!, OwnerTestWorkspace.DirectoryName);
    Directory.CreateDirectory(Path.Combine(workspace, ".revora-owner-test"));
    string path = Path.Combine(workspace, ".revora-owner-test", "test4e-stage.bin");
    await File.WriteAllBytesAsync(path, Enumerable.Range(1, 11).Select(value => (byte)value).ToArray());
    BootstrapBreadcrumbResult valid = BootstrapControlTrace5165.Read(workspace);
    Assert(valid.Valid && valid.Stages.Count == 11 && valid.Stages[0] == 1 && valid.Stages[2] == 3);
    await File.WriteAllBytesAsync(path, new byte[] { 1, 3 });
    BootstrapBreadcrumbResult gap = BootstrapControlTrace5165.Read(workspace);
    Assert(!gap.Valid && gap.FailureCategory == "control-trace-sequence-invalid");
    BootstrapControlTrace5165.Reset(workspace);
    Assert(!File.Exists(path));
}

static Task Bootstrap4fSelfTest()
{
    Assert(BootstrapControlTrace4f5165.SelfTest());
    return Task.CompletedTask;
}

static Task Bootstrap4fAttribution()
{
    byte[] valid = TraceRecords((1, 1U, 77U, 10U), (2, 2U, 77U, 10U), (4, 3U, 77U, 11U), (3, 4U, 77U, 10U));
    BootstrapTraceResult5165 accepted = BootstrapControlTrace4f5165.Parse(valid, 77);
    Assert(accepted.TransportValid && accepted.PartialOrderValid && accepted.DiagnosticChannelProcessId == 77);
    Assert(accepted.ControlAttributionResult == "both-controls-attributed-to-launched-pid");
    byte[] invalid = TraceRecords((1, 1U, 77U, 10U), (4, 2U, 77U, 11U));
    BootstrapTraceResult5165 rejected = BootstrapControlTrace4f5165.Parse(invalid, 77);
    Assert(rejected.TransportValid && !rejected.PartialOrderValid && rejected.FirstInvalidTransition == "stage-4-before-stage-2");
    byte[] foreign = TraceRecords((1, 1U, 88U, 10U), (2, 2U, 88U, 10U), (3, 3U, 88U, 10U));
    BootstrapTraceResult5165 wrongPid = BootstrapControlTrace4f5165.Parse(foreign, 77);
    Assert(!wrongPid.TransportValid && wrongPid.FailureCategory == "control-trace-pid-attribution-failed");
    return Task.CompletedTask;
}

static byte[] TraceRecords(params (int Stage, uint Sequence, uint Pid, uint Tid)[] events)
{
    byte[] bytes = new byte[events.Length * 28];
    for (int index = 0; index < events.Length; index++)
    {
        Span<byte> record = bytes.AsSpan(index * 28, 28);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(record, 0x46345652);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(record[4..], checked((uint)events[index].Stage));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(record[8..], events[index].Sequence);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(record[12..], events[index].Pid);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(record[16..], events[index].Tid);
        System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(record[20..], 1000 + index);
    }
    return bytes;
}

static async Task OwnerWorkspaceReused()
{
    Setup s = await Create();
    var workspace = new OwnerTestWorkspace();
    OwnerWorkspaceResult first = await workspace.PrepareAsync(s.Source, s.Input, s.Output, s.Patch, s.Server, default);
    Assert(first.Created && Path.GetFileName(first.Workspace) == OwnerTestWorkspace.DirectoryName);
    await File.WriteAllTextAsync(Path.Combine(first.Workspace, "unrelated-owner-file.txt"), "preserve");
    OwnerWorkspaceResult second = await workspace.PrepareAsync(s.Source, s.Input, s.Output, s.Patch, s.Server, default);
    Assert(!second.Created && first.Workspace == second.Workspace && File.Exists(Path.Combine(first.Workspace, "unrelated-owner-file.txt")));
}

static Task KnownTest4cWorkspaceState()
{
    Assert(OwnerTestWorkspace.ClassifyKnownExecutableHash("8040023d599116c5db3d91eb3eb97065ec15521ed8b6a3a7ac6c3a28247ed310") == "native-login-5165-owner-test-4c");
    Assert(OwnerTestWorkspace.ClassifyKnownExecutableHash("7aa96e49cb30ded6db3944b5fd4a7f36282cc26036e1152e7b1f1802c8891df4") == "native-login-5165-owner-test-4e");
    Assert(OwnerTestWorkspace.ClassifyKnownExecutableHash("b4d4977ef50980b656397d0e339bd0d34f350917663b6c9af393586a49cdd6df") == "native-login-5165-owner-test-4f");
    Assert(OwnerTestWorkspace.ClassifyKnownExecutableHash("206bcee90388633930dc4171a4cbc23e2c5b140ae55d25c7220af0f35a0c89a9") == "native-login-5165-owner-test-4g");
    Assert(OwnerTestWorkspace.ClassifyKnownExecutableHash("cf4525da3f43f73bfb1eecc6252f3b9c3a570db1963bbe7b2a7a1ec0a6abfc68") == "native-login-5165-owner-test-4h");
    Assert(OwnerTestWorkspace.ClassifyKnownExecutableHash("e3a4570314a0e9c06cee62e41662cd745da811cfdc8c28a85fb626920155db5c") == "doctor-reconstruction-5165-test-2c");
    Assert(OwnerTestWorkspace.ClassifyKnownExecutableHash(new string('f', 64)) is null);
    return Task.CompletedTask;
}

static async Task Schema1MarkerUpgrade()
{
    Setup s = await Create(); OwnerWorkspaceResult first = await new OwnerTestWorkspace().PrepareAsync(s.Source, s.Input, s.Output, s.Patch, s.Server, default);
    Guid workspaceId = first.WorkspaceId; DateTimeOffset created = DateTimeOffset.Parse("2026-08-28T21:41:21.454361+00:00");
    var unsigned = new { SchemaVersion = 1, BaselineBuildId = s.Input.BuildId, WorkspaceId = workspaceId, CreatedUtc = created, Checksum = "" };
    string checksum = Hash(JsonSerializer.SerializeToUtf8Bytes(unsigned));
    string marker = Path.Combine(first.Workspace, ".revora-owner-test", "workspace-marker.json");
    await File.WriteAllTextAsync(marker, JsonSerializer.Serialize(new { unsigned.SchemaVersion, unsigned.BaselineBuildId, unsigned.WorkspaceId, unsigned.CreatedUtc, Checksum = checksum }));
    OwnerWorkspaceResult upgraded = await new OwnerTestWorkspace().PrepareAsync(s.Source, s.Input, s.Output, s.Patch, s.Server, default);
    Assert(!upgraded.Created && upgraded.WorkspaceId == workspaceId && (await File.ReadAllTextAsync(marker)).Contains("\"SchemaVersion\":2", StringComparison.Ordinal));
}

static async Task UnknownControlledHash()
{
    Setup s = await Create(); OwnerWorkspaceResult first = await new OwnerTestWorkspace().PrepareAsync(s.Source, s.Input, s.Output, s.Patch, s.Server, default);
    await File.WriteAllBytesAsync(Path.Combine(first.Workspace, "Conquer.exe"), new byte[] { 9, 9, 9, 9 });
    try { await new OwnerTestWorkspace().PrepareAsync(s.Source, s.Input, s.Output, s.Patch, s.Server, default); }
    catch (WorkspaceUpgradeException error) { Assert(error.Category == "unknown-active-hash" && error.Component == "Conquer.exe"); return; }
    throw new InvalidOperationException("Unknown active state was accepted.");
}

static async Task InterruptedUpgradePreservesWorkspace()
{
    Setup s = await Create(); OwnerWorkspaceResult first = await new OwnerTestWorkspace().PrepareAsync(s.Source, s.Input, s.Output, s.Patch, s.Server, default);
    byte[] before = await File.ReadAllBytesAsync(Path.Combine(first.Workspace, "Conquer.exe"));
    string marker = await File.ReadAllTextAsync(Path.Combine(first.Workspace, ".revora-owner-test", "workspace-marker.json"));
    var interrupted = new OwnerTestWorkspace(new(BeforeStageComplete: _ => throw new IOException("simulated")));
    await Throws<WorkspaceUpgradeException>(() => interrupted.PrepareAsync(s.Source, s.Input, s.Output, s.Patch, s.Server, default));
    byte[] after = await File.ReadAllBytesAsync(Path.Combine(first.Workspace, "Conquer.exe"));
    string markerAfter = await File.ReadAllTextAsync(Path.Combine(first.Workspace, ".revora-owner-test", "workspace-marker.json"));
    Assert(before.SequenceEqual(after) && marker == markerAfter);
}

static async Task ReplacementFailureRollsBack()
{
    Setup s = await Create(); OwnerWorkspaceResult first = await new OwnerTestWorkspace().PrepareAsync(s.Source, s.Input, s.Output, s.Patch, s.Server, default);
    foreach (string relative in new[] { "Conquer.exe", "Server.dat" }) File.Copy(Path.Combine(s.Source, relative), Path.Combine(first.Workspace, relative), true);
    var failing = new OwnerTestWorkspace(new(BeforeAtomicReplace: relative => { if (relative == "Server.dat") throw new IOException("simulated"); }));
    try { await failing.PrepareAsync(s.Source, s.Input, s.Output, s.Patch, s.Server, default); }
    catch (WorkspaceUpgradeException error)
    {
        Assert(error.RollbackAttempted && error.RollbackResult == "succeeded");
        byte[] restored = await File.ReadAllBytesAsync(Path.Combine(first.Workspace, "Conquer.exe"));
        byte[] canonical = await File.ReadAllBytesAsync(Path.Combine(s.Source, "Conquer.exe"));
        Assert(restored.SequenceEqual(canonical));
        return;
    }
    throw new InvalidOperationException("Replacement failure was not surfaced.");
}

static async Task EarlyFailureReport()
{
    string root = Temp(); var writer = new OwnerTestReportWriter(root);
    string report = await writer.WriteFailureAsync(new("native-login-5165-owner-test-4d-r1", "marker-validation", "marker-checksum-mismatch", "workspace-marker.json", new string('1', 64), new string('2', 64), "doctor-reconstruction-5165-test-2c", "native-login-5165-owner-test-4c", false, "not-required"), default);
    string json = await File.ReadAllTextAsync(report);
    Assert(File.Exists(report) && json.Contains("marker-checksum-mismatch", StringComparison.Ordinal) && !json.Contains(root, StringComparison.Ordinal));
}

static async Task RootClassification()
{
    Setup setup = await Create();
    OwnerWorkspaceResult prepared = await new OwnerTestWorkspace().PrepareAsync(setup.Source, setup.Input, setup.Output, setup.Patch, setup.Server, default);
    string expectedWorkspace = Path.Combine(Path.GetDirectoryName(setup.Source)!, OwnerTestWorkspace.DirectoryName);
    OwnerRootEvidence donor = await OwnerTestWorkspace.InspectRootAsync(setup.Source, expectedWorkspace, setup.Input, default);
    OwnerRootEvidence workspace = await OwnerTestWorkspace.InspectRootAsync(prepared.Workspace, prepared.Workspace, setup.Input, default);
    string unknownRoot = Temp();
    OwnerRootEvidence unknown = await OwnerTestWorkspace.InspectRootAsync(unknownRoot, Path.Combine(Path.GetDirectoryName(unknownRoot)!, OwnerTestWorkspace.DirectoryName), setup.Input, default);
    Assert(donor.SourceKind == "verified-donor" && donor.ExpectedClientRoot && donor.SignedProfileMatch == "matched");
    Assert(workspace.SourceKind == "owned-workspace" && workspace.WorkspaceId == prepared.WorkspaceId && !workspace.ExpectedClientRoot);
    Assert(unknown.SourceKind == "unknown" && unknown.SignedProfileMatch == "not-matched");
    Assert(donor.SafeRootFingerprint.Length == 64 && !donor.SafeRootFingerprint.Contains(setup.Source, StringComparison.Ordinal));
}

static async Task UnmarkedOwnerWorkspacePreserved()
{
    Setup s = await Create();
    string workspace = Path.Combine(Path.GetDirectoryName(s.Source)!, OwnerTestWorkspace.DirectoryName);
    Directory.CreateDirectory(workspace);
    await File.WriteAllTextAsync(Path.Combine(workspace, "owner.txt"), "preserve");
    await Throws<IOException>(() => new OwnerTestWorkspace().PrepareAsync(s.Source, s.Input, s.Output, s.Patch, s.Server, default));
    Assert(File.Exists(Path.Combine(workspace, "owner.txt")));
}

static async Task BootstrapBridgeConsumesOnce()
{
    byte[] rawTicket = RandomNumberGenerator.GetBytes(32);
    using var locked = new LockedLaunchTicket(rawTicket);
    int consumes = 0;
    await using var bridge = new AccountBootstrapBridge(locked, new FixedOwner(4242), (ticket, _) =>
    {
        Assert(ticket.Span.SequenceEqual(rawTicket)); consumes++; return Task.FromResult(true);
    }, 0);
    bridge.Start(); bridge.BindExpectedProcess(4242);
    using var client = new TcpClient(); await client.ConnectAsync(IPAddress.Loopback, bridge.Port);
    byte[] request = new byte[AccountProbe3b.ExpectedLength]; request[0] = 0x14; request[1] = 1; request[2] = 0x3e; request[3] = 4;
    byte[] encrypted = new byte[request.Length]; new AccountCipher5165().EncryptClientForTest(request, encrypted);
    await client.GetStream().WriteAsync(encrypted);
    byte[] response = new byte[12]; await ReadExactly(client.GetStream(), response);
    BootstrapBridgeResult result = await bridge.Completion;
    Assert(result.State == "completed" && result.ClientProcessId == 4242 && result.Length == 276 && result.PacketId == 1086 && result.TicketLifecycle == "consumed" && consumes == 1);
}

static async Task BootstrapBridgeRejectsForeignPid()
{
    using var locked = new LockedLaunchTicket(RandomNumberGenerator.GetBytes(32));
    int consumes = 0;
    await using var bridge = new AccountBootstrapBridge(locked, new FixedOwner(9),
        (_, _) => { consumes++; return Task.FromResult(true); }, 0);
    bridge.Start(); bridge.BindExpectedProcess(10);
    using var client = new TcpClient(); await client.ConnectAsync(IPAddress.Loopback, bridge.Port);
    BootstrapBridgeResult result = await bridge.Completion;
    Assert(result.FailureCategory == "unexpected-client-process" && result.TicketLifecycle == "revoked" && consumes == 1 && !result.ResponseSent);
}

static async Task BootstrapMessageBoundary()
{
    Assert(AccountBootstrapMessage5165.AccountStartMessage == 0x480);
    if (!OperatingSystem.IsWindows())
        await Throws<PlatformNotSupportedException>(() => new AccountBootstrapMessage5165().WaitAndPostOnceAsync(Process.GetCurrentProcess(), DateTimeOffset.UtcNow.AddSeconds(5), default));
}

static Task ManualBaselineExactDifferenceAudit()
{
    string? donor = Environment.GetEnvironmentVariable("REVORA_M1_DONOR_PE");
    string? output = Environment.GetEnvironmentVariable("REVORA_M1_OUTPUT_PE");
    string? metadata = Environment.GetEnvironmentVariable("REVORA_M1_PATCH_METADATA");
    if (string.IsNullOrWhiteSpace(donor) || string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(metadata))
    {
        Assert(ConfigurationOnlyPatchAudit5165.DonorSha256 == "e3a4570314a0e9c06cee62e41662cd745da811cfdc8c28a85fb626920155db5c");
        Assert(ConfigurationOnlyPatchAudit5165.OutputSha256 == "902954c9b66911838fc8277a2f3d40cf8cd77ff413da2e759e3b74bb1ed93c2f");
        return Task.CompletedTask;
    }
    ConfigurationOnlyPatchAuditResult result = ConfigurationOnlyPatchAudit5165.Verify(
        File.ReadAllBytes(donor), File.ReadAllBytes(output), File.ReadAllBytes(metadata));
    Assert(result.ChangeRecords == 64 && result.AllowedBytes == 256 && result.ActualChangedBytes is > 0 and <= 256);
    return Task.CompletedTask;
}

static async Task ManualBaselineControlledResponse()
{
    await using var probe = new ManualPlayableAccountProbe(new FixedOwner(4242), TimeSpan.FromSeconds(5), 0);
    probe.Start(); probe.BindExpectedProcess(4242);
    using var client = new TcpClient(); await client.ConnectAsync(IPAddress.Loopback, probe.Port);
    byte[] plaintext = new byte[276]; plaintext[0] = 0x14; plaintext[1] = 1; plaintext[2] = 0x3e; plaintext[3] = 4;
    RandomNumberGenerator.Fill(plaintext.AsSpan(4));
    byte[] encrypted = new byte[276]; new AccountCipher5165().EncryptClientForTest(plaintext, encrypted);
    await client.GetStream().WriteAsync(encrypted.AsMemory(0, 3));
    await client.GetStream().WriteAsync(encrypted.AsMemory(3, 100));
    await client.GetStream().WriteAsync(encrypted.AsMemory(103));
    byte[] responseCiphertext = new byte[12]; await ReadExactly(client.GetStream(), responseCiphertext);
    byte[] response = new byte[12]; new AccountCipher5165().DecryptServerForTest(responseCiphertext, response);
    ManualPlayableProbeResult result = await probe.Completion;
    Assert(result is { ConnectedClientProcessId: 4242, EncryptedRequestLength: 276, DecryptedPacketLength: 276,
        PacketId: 1086, ResponseLength: 12, ResponsePacketId: 1055, ResponseSent: true });
    Assert(response[0] == 12 && response[2] == 0x1f && response[3] == 4 && response[8] == 1);
}

static async Task ManualBaselineRejectsForeignPid()
{
    await using var probe = new ManualPlayableAccountProbe(new FixedOwner(41), TimeSpan.FromSeconds(5), 0);
    probe.Start(); probe.BindExpectedProcess(42);
    using var client = new TcpClient(); await client.ConnectAsync(IPAddress.Loopback, probe.Port);
    ManualPlayableProbeResult result = await probe.Completion;
    Assert(result.FailureCategory == "unexpected-client-process" && !result.ResponseSent);
}

static async Task ManualBaselineTimeout()
{
    await using var probe = new ManualPlayableAccountProbe(new FixedOwner(1), TimeSpan.FromMilliseconds(50), 0);
    probe.Start(); probe.BindExpectedProcess(1);
    ManualPlayableProbeResult result = await probe.Completion;
    Assert(result.FailureCategory == "bounded-timeout" && !result.ResponseSent);
}

static Task Bootstrap4gControls()
{
    string workspace = Temp(), control = Path.Combine(workspace, ".revora-owner-test"); Directory.CreateDirectory(control);
    StartupControl4g5165.Reset(workspace);
    Assert(StartupControl4g5165.Read(workspace, 4242).Classification == "patch-or-entry-stub-execution-failed");
    WriteControl(Path.Combine(control, "test4g-startup.bin"), 0x474D5652, 4242, 101, 1000);
    Assert(StartupControl4g5165.Read(workspace, 4242).Classification == "client-to-launcher-transport-failed");
    WriteControl(Path.Combine(control, "test4g-events.bin"), 0x47345652, 4242, 202, 1001);
    StartupControlResult4g5165 result = StartupControl4g5165.Read(workspace, 4242);
    Assert(result.Classification == "startup-controls-proven" && result.MarkerValid && result.EventValid &&
        result.MarkerProcessId == 4242 && result.EventThreadId == 202);
    Assert(StartupControl4g5165.Read(workspace, 9).Classification == "patch-or-entry-stub-execution-failed");
    return Task.CompletedTask;
}

static Task Bootstrap4hMarker()
{
    string workspace = Temp(), control = Path.Combine(workspace, ".revora-owner-test"); Directory.CreateDirectory(control);
    StartupMarker4h.Reset(workspace);
    Assert(!StartupMarker4h.Read(workspace, 77).Valid);
    WriteControl(Path.Combine(control, "test4h-startup.bin"), 0x484D5652, 77, 88, 1234);
    Assert(StartupMarker4h.Read(workspace, 77).Valid);
    Assert(!StartupMarker4h.Read(workspace, 78).Valid);
    BootstrapControlTrace4f5165.ResetTest4h(workspace);
    Assert(BootstrapControlTrace4f5165.ReadTest4h(workspace, 77).Events.Count == 0);
    return Task.CompletedTask;
}

static Task Bootstrap4iControls()
{
    string workspace = Temp(), control = Path.Combine(workspace, ".revora-owner-test"); Directory.CreateDirectory(control);
    WriteControl(Path.Combine(control, "test4h-events.bin"), 0x46345652, 77, 88, 1000);
    BootstrapControlTrace4f5165.ResetSupportedDiagnosticFiles(workspace);
    Assert(!File.Exists(Path.Combine(control, "test4h-events.bin")));
    DateTimeOffset launched = DateTimeOffset.UtcNow;
    WriteControl(Path.Combine(control, "test4i-startup.bin"), 0x484D5652, 77, 88, 1001);
    WriteControl(Path.Combine(control, "test4i-events.bin"), 0x46345652, 77, 88, 1002);
    Assert(StartupMarker4i.Read(workspace, 77).Valid);
    Assert(BootstrapControlTrace4f5165.ReadTest4i(workspace, 77).Events.Single().StageId == 1);
    IReadOnlyList<DiagnosticFilePresence5165> files = DiagnosticFileInspection5165.Inspect(workspace, 77, launched);
    Assert(files.Single(item => item.Name == "test4i-events.bin") is { Present: true, CurrentLaunchWindow: true, FirstRecordValid: true, ProcessIdMatches: true });
    Assert(files.Single(item => item.Name == "test4h-events.bin").Present == false);
    return Task.CompletedTask;
}

static Task Test4iExactPayloadAudit()
{
    string? path = Environment.GetEnvironmentVariable("REVORA_TEST4I_EXACT_PE");
    if (string.IsNullOrWhiteSpace(path)) return Task.CompletedTask;
    Test4iPatchAuditResult result = Test4iPatchAudit5165.ValidateExactExecutable(path);
    Assert(result.Sha256 == Test4iPatchAudit5165.ExpectedSha256 && result.CounterAddress == 0x695300 &&
        result.EventFilename.EndsWith("test4i-events.bin", StringComparison.Ordinal));
    return Task.CompletedTask;
}

static Task Bootstrap4jBoundaryTrace()
{
    string workspace = Temp(), control = Path.Combine(workspace, ".revora-owner-test"); Directory.CreateDirectory(control);
    int[] stages = [1, 2, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 24, 17, 20, 21, 23, 3];
    (int Stage, uint Sequence, uint Pid, uint Tid)[] records = stages.Select((stage, index) => (stage, checked((uint)(index + 1)), 77U, 88U)).ToArray();
    File.WriteAllBytes(Path.Combine(control, "test4j-events.bin"), TraceRecords(records));
    BootstrapTraceResult5165 result = BootstrapControlTrace4f5165.ReadTest4j(workspace, 77);
    Assert(result.TransportValid && result.PartialOrderValid && result.MissingStageIds.SequenceEqual(new[] { 18, 19 }) &&
        result.Events.Any(item => item.StageId == 24) && result.Events.Any(item => item.StageId == 23));
    return Task.CompletedTask;
}

static Task Test4jExactPayloadAudit()
{
    string? path = Environment.GetEnvironmentVariable("REVORA_TEST4J_EXACT_PE");
    if (string.IsNullOrWhiteSpace(path)) return Task.CompletedTask;
    Test4jPatchAuditResult result = Test4jPatchAudit5165.ValidateExactExecutable(path);
    Assert(result.Sha256 == Test4jPatchAudit5165.ExpectedSha256 && result.StageOneBypassesPreCrtCounter &&
        result.PostResultInstrumented && result.CommandConsumptionInstrumented && result.ReadinessGuardInstrumented &&
        result.EventFilename.EndsWith("test4j-events.bin", StringComparison.Ordinal));
    return Task.CompletedTask;
}

static Task M2LoginLayout()
{
    byte[] packet = new byte[AccountPackets5165.LoginLength];
    System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(packet, AccountPackets5165.LoginLength);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), AccountPackets5165.LoginPacketId);
    "fixture1"u8.CopyTo(packet.AsSpan(4));
    Span<byte> clear = stackalloc byte[16];
    "Fixture5165!"u8.CopyTo(clear);
    LegacyPasswordCipher5165.EncryptForTest(clear, packet.AsSpan(132, 16));
    "Revora"u8.CopyTo(packet.AsSpan(260));
    using AccountLogin5165 login = AccountPackets5165.ParseLogin(packet);
    Assert(login.Username == "fixture1" && System.Text.Encoding.ASCII.GetString(login.PasswordBytes) == "Fixture5165!" && login.Realm == "Revora");
    CryptographicOperations.ZeroMemory(packet); CryptographicOperations.ZeroMemory(clear);
    return Task.CompletedTask;
}

static Task M2SuccessLayout()
{
    byte[] response = new byte[AccountPackets5165.ConnectSuccessLength];
    AccountPackets5165.WriteSuccess(response, 0x8877665544332211, "127.0.0.1", 9959);
    Assert(System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(response) == 32 &&
        System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(2)) == 1055 &&
        System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(response.AsSpan(4)) == 0x8877665544332211 &&
        System.Text.Encoding.ASCII.GetString(response, 12, 9) == "127.0.0.1" &&
        System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(response.AsSpan(28)) == 9959);
    return Task.CompletedTask;
}

static async Task M2ExactClientEndpointGuard()
{
    byte[] response = new byte[AccountPackets5165.ConnectSuccessLength];
    AccountPackets5165.WriteSuccess(response, 0x8877665544332211, "127.0.0.1", 9959);
    await Throws<InvalidDataException>(() => Task.FromResult(AccountPackets5165.ParseSuccessAsExactClient(response)));

    AccountPackets5165.WriteSuccess(response, 0x8877665544332211, "10.0.0.1", 9959);
    AccountConnectSuccess5165 parsed = AccountPackets5165.ParseSuccessAsExactClient(response);
    Assert(parsed.AccountWord == 0x44332211 && parsed.HandoffWord == 0x88776655 &&
        parsed.Address == "10.0.0.1" && parsed.Port == 9959 && response[20] == 0);

    response.AsSpan(12, 16).Fill((byte)'1');
    await Throws<InvalidDataException>(() => Task.FromResult(AccountPackets5165.ParseSuccessAsExactClient(response)));
}

static Task M2GameConnectLayout()
{
    byte[] packet = new byte[GamePackets5165.ConnectLength];
    System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(packet, 28);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), 1052);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), 0x44332211);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8), 0x88776655);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(12), 124);
    "English"u8.CopyTo(packet.AsSpan(14));
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(24), 5165);
    GameConnect5165 connect = GamePackets5165.ParseConnect(packet);
    Assert(connect == new GameConnect5165(0x44332211, 0x88776655, 124, "English", 5165));
    return Task.CompletedTask;
}

static Task CharacterRegistrationLayout()
{
    byte[] wire = ExactClientRegistrationBuilder5165.BuildSmallMaleTaoist("SHEEKO2", 0x44332211);
    Assert(wire.Length == 112 && wire.AsSpan(104).SequenceEqual("TQClient"u8));
    CharacterRegistration5165 parsed = GamePacketsAfterAuthentication5165.ParseRegistration(wire.AsSpan(0, 104));
    Assert(parsed.Account.Length == 0 && parsed.Name == "SHEEKO2" && parsed.SensitiveReserved.AsSpan().IndexOfAnyExcept((byte)0) < 0 &&
        parsed.Mesh == 1003 && parsed.Profession == 100 && parsed.AccountId == 0x44332211);
    parsed.SensitiveReserved[0] = 0x5a;
    parsed.Dispose();
    Assert(parsed.SensitiveReserved.AsSpan().IndexOfAnyExcept((byte)0) < 0);
    return Task.CompletedTask;
}

static async Task CharacterRegistrationRejection()
{
    byte[] packet = new byte[104];
    System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(packet, 103);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), 1001);
    await Throws<InvalidDataException>(() => Task.FromResult(GamePacketsAfterAuthentication5165.ParseRegistration(packet)));
    byte[] nonCanonicalAccount = ExactClientRegistrationBuilder5165.BuildSmallMaleTaoist("SHEEKO2", 0x44332211);
    nonCanonicalAccount[5] = (byte)'x';
    await Throws<InvalidDataException>(() => Task.FromResult(
        GamePacketsAfterAuthentication5165.ParseRegistration(nonCanonicalAccount.AsSpan(0, 104))));
    Assert(!GamePacketsAfterAuthentication5165.IsValidCharacterName("GMowner") &&
        !GamePacketsAfterAuthentication5165.IsValidCharacterName("bad name") &&
        GamePacketsAfterAuthentication5165.IsValidCharacterName("SHEEKO") &&
        GamePacketsAfterAuthentication5165.IsValidMesh(2002) && !GamePacketsAfterAuthentication5165.IsValidMesh(9999) &&
        GamePacketsAfterAuthentication5165.IsValidProfession(100) && GamePacketsAfterAuthentication5165.IsValidProfession(50) &&
        !GamePacketsAfterAuthentication5165.IsValidProfession(30) && !GamePacketsAfterAuthentication5165.IsValidProfession(101));
}

static async Task CharacterResponseLayouts()
{
    byte[] register = GamePacketsAfterAuthentication5165.EncodeTalk(2100, "ANSWER_OK");
    Assert(System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(register) == register.Length &&
        System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(register.AsSpan(2)) == 1004 &&
        System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(register.AsSpan(8)) == 2100 &&
        System.Text.Encoding.ASCII.GetString(register).Contains("ANSWER_OK", StringComparison.Ordinal));
    byte[] info = GamePacketsAfterAuthentication5165.EncodeUserInfo(1000000, "SHEEKO", 1003, 10);
    UserInformation5165 exact = GamePacketsAfterAuthentication5165.ParseUserInfoAsExactClient(info);
    Assert(System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(info) == info.Length &&
        System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(info.AsSpan(2)) == 1006 &&
        System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(info.AsSpan(4)) == 1000000 &&
        System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(info.AsSpan(8)) == 1003 && info[67] == 10 &&
        info.Length == 100 && info[87] == 2 && exact.Name == "SHEEKO" && exact.Spouse == "None");
    byte[] oldInvalidLayout = new byte[80];
    System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(oldInvalidLayout, 80);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(oldInvalidLayout.AsSpan(2), 1006);
    await Throws<InvalidDataException>(() => Task.FromResult(
        GamePacketsAfterAuthentication5165.ParseUserInfoAsExactClient(oldInvalidLayout)));
}

static Task FirstWorldEntryLayouts()
{
    byte[] request = new byte[32];
    System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(request, 32);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(2), 10010);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(4), 1000000);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(12), 0x76543210);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(16), 74);
    Assert(GamePacketsAfterAuthentication5165.ParseAction(request) ==
        new GameAction5165(1000000, 0, 0x76543210, 74, 0, 0, 0));
    byte[] response = GamePacketsAfterAuthentication5165.EncodeEnterMap(1000000, 1002, 430, 378, 0, 0x76543210);
    Assert(GamePacketsAfterAuthentication5165.ParseAction(response) ==
        new GameAction5165(1000000, 1002, 0x76543210, 74, 0, 430, 378));
    byte[] rejectedOlderGeneration = new byte[24];
    System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(rejectedOlderGeneration, 24);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(rejectedOlderGeneration.AsSpan(2), 1010);
    AssertThrowsSync<InvalidDataException>(() => GamePacketsAfterAuthentication5165.ParseAction(rejectedOlderGeneration));
    byte[] map = GamePacketsAfterAuthentication5165.EncodeMapInfo(1002);
    Assert(map.Length == 16 && System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(map.AsSpan(2)) == 1110);
    return Task.CompletedTask;
}

static void AssertThrowsSync<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new InvalidOperationException($"Expected {typeof(T).Name}.");
}

static void WriteControl(string path, uint magic, uint processId, uint threadId, long timestamp)
{
    byte[] bytes = new byte[28];
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes, magic);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 1);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 1);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), processId);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), threadId);
    System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(20), timestamp);
    File.WriteAllBytes(path, bytes);
}

static async Task ReadExactly(NetworkStream stream, Memory<byte> destination)
{
    int offset = 0;
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
    while (offset < destination.Length) { int read = await stream.ReadAsync(destination[offset..], timeout.Token); if (read == 0) throw new EndOfStreamException(); offset += read; }
}

static async Task<Setup> Create()
{
    string parent = Temp(), source = Path.Combine(parent, "clean"), destination = Path.Combine(parent, "test3"); Directory.CreateDirectory(source);
    byte[] executable = [1, 2, 3, 4], data = "data"u8.ToArray(), serverOriginal = "original"u8.ToArray(), serverReplacement = "test3"u8.ToArray();
    await File.WriteAllBytesAsync(Path.Combine(source, "Conquer.exe"), executable); await File.WriteAllBytesAsync(Path.Combine(source, "data.wdf"), data); await File.WriteAllBytesAsync(Path.Combine(source, "Server.dat"), serverOriginal);
    ClientProfile input = Profile("doctor-reconstruction-5165-test-2c", executable, data, serverOriginal);
    byte[] patched = executable.ToArray(); patched[2] = 9; ClientProfile output = Profile("native-login-5165-test-3", patched, data, serverReplacement);
    string patch = Path.Combine(parent, "patch.json"), server = Path.Combine(parent, "Server.test3.dat"); await File.WriteAllBytesAsync(server, serverReplacement);
    await File.WriteAllTextAsync(patch, JsonSerializer.Serialize(new { schemaVersion = 1, inputSha256 = Hash(executable), outputSha256 = Hash(patched), changes = new[] { new { Offset = 2, OriginalHex = "03", ReplacementHex = "09" } } }));
    return new(source, destination, input, output, patch, server);
}
static ClientProfile Profile(string build, byte[] executable, byte[] data, byte[] server) => new(1, 5165, "playrevora.com", 9958, 9959, "Conquer.exe", [Entry("Conquer.exe", executable, "active"), Entry("data.wdf", data, "passive"), Entry("Server.dat", server, "active")], "testing", build, ["blacknull"], 19045, "native-input-v1");
static ClientFile Entry(string path, byte[] value, string role) => new(path, value.Length, Hash(value), null, role);
static string Hash(byte[] value) => Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
static string Temp() { string path = Path.Combine(Path.GetTempPath(), "revora-native-tests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path); return path; }
static async Task Throws<T>(Func<Task> action) where T : Exception { try { await action(); } catch (T) { return; } throw new InvalidOperationException($"Expected {typeof(T).Name}."); }
static void ThrowsSync<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new InvalidOperationException($"Expected {typeof(T).Name}."); }
static void Assert(bool condition) { if (!condition) throw new InvalidOperationException("Assertion failed."); }
sealed record Setup(string Source, string Destination, ClientProfile Input, ClientProfile Output, string Patch, string Server);
sealed class FixedOwner(int processId) : ITcpOwnerResolver { public int? Resolve(IPEndPoint clientEndpoint, IPEndPoint serverEndpoint) => processId; }

sealed class FixtureH1Authenticator : IH1AccountAuthenticator
{
    public int ConsumeCount { get; private set; }
    public Task<LauncherSession> AuthenticateAsync(string username, ReadOnlyMemory<byte> passwordUtf8, Guid sessionId, CancellationToken token)
    {
        if (username != "fixture1" || !passwordUtf8.Span.SequenceEqual("Fixture5165!"u8)) throw new UnauthorizedAccessException();
        return Task.FromResult(new LauncherSession(sessionId, Guid.NewGuid(), "Revora", DateTimeOffset.UtcNow.AddSeconds(60), RandomNumberGenerator.GetBytes(32)));
    }
    public Task<bool> ConsumeAsync(LauncherSession session, CancellationToken token) { ConsumeCount++; return Task.FromResult(ConsumeCount == 1); }
}

sealed class H1ClientFixture : IDisposable
{
    private readonly BlowfishCfb64 initial;
    private readonly BlowfishCfb64 game;
    private readonly string publicHex;

    private H1ClientFixture(BlowfishCfb64 initial, BlowfishCfb64 game, string publicHex) =>
        (this.initial, this.game, this.publicHex) = (initial, game, publicHex);

    public static H1ClientFixture Accept(byte[] encryptedHello, bool invalidPublic = false)
    {
        var initial = new BlowfishCfb64("DR654dt34trg4UI6"u8);
        byte[] hello = new byte[encryptedHello.Length];
        initial.DecryptClient(encryptedHello, hello);
        AssertFixture(hello.AsSpan(^8).SequenceEqual("TQServer"u8));
        int offset = 15;
        int junk = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(hello.AsSpan(offset)); offset += 4 + junk;
        byte[] serverEncryptIv = ReadField(hello, ref offset);
        byte[] serverDecryptIv = ReadField(hello, ref offset);
        string primeHex = System.Text.Encoding.ASCII.GetString(ReadField(hello, ref offset));
        string generatorHex = System.Text.Encoding.ASCII.GetString(ReadField(hello, ref offset));
        string serverPublicHex = System.Text.Encoding.ASCII.GetString(ReadField(hello, ref offset));
        var prime = Parse(primeHex); var generator = Parse(generatorHex); var serverPublic = Parse(serverPublicHex);
        var exponent = new System.Numerics.BigInteger(Enumerable.Range(65, 32).Select(value => (byte)value).ToArray(), true, true);
        exponent = 2 + exponent % (prime - 3);
        var publicValue = invalidPublic ? System.Numerics.BigInteger.One : System.Numerics.BigInteger.ModPow(generator, exponent, prime);
        byte[] shared = System.Numerics.BigInteger.ModPow(serverPublic, exponent, prime).ToByteArray(true, true);
        var game = new BlowfishCfb64(shared);
        game.SetServerIvs(serverEncryptIv, serverDecryptIv); // client inbound, then client outbound
        CryptographicOperations.ZeroMemory(shared);
        return new(initial, game, Convert.ToHexString(publicValue.ToByteArray(true, true)));
    }

    public byte[] CreateReply()
    {
        byte[] key = System.Text.Encoding.ASCII.GetBytes(publicHex);
        byte[] plaintext = new byte[7 + 4 + 4 + 12 + 4 + key.Length + 8];
        for (int index = 0; index < 7; index++) plaintext[index] = (byte)(0xa0 + index);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(plaintext.AsSpan(7), plaintext.Length - 7);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(plaintext.AsSpan(11), 12);
        for (int index = 0; index < 12; index++) plaintext[15 + index] = (byte)(0xb0 + index);
        int offset = 27;
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(plaintext.AsSpan(offset), key.Length); offset += 4;
        key.CopyTo(plaintext.AsSpan(offset)); offset += key.Length;
        "TQClient"u8.CopyTo(plaintext.AsSpan(offset));
        byte[] encrypted = new byte[plaintext.Length];
        initial.EncryptServer(plaintext, encrypted);
        CryptographicOperations.ZeroMemory(plaintext);
        return encrypted;
    }

    public byte[] CreateConnectPacket(bool invalidFooter = false) => CreateConnectPacket(0x8877665544332211, invalidFooter);

    public byte[] CreateConnectPacket(ulong wireToken, bool invalidFooter = false)
    {
        byte[] plaintext = new byte[36];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(plaintext, 28);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(plaintext.AsSpan(2), 1052);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(plaintext.AsSpan(4), (uint)wireToken);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(plaintext.AsSpan(8), (uint)(wireToken >> 32));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(plaintext.AsSpan(12), 124);
        "English"u8.CopyTo(plaintext.AsSpan(14));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(plaintext.AsSpan(24), 5165);
        (invalidFooter ? "BADFOOT!"u8 : "TQClient"u8).CopyTo(plaintext.AsSpan(28));
        byte[] encrypted = new byte[plaintext.Length];
        game.EncryptServer(plaintext, encrypted);
        CryptographicOperations.ZeroMemory(plaintext);
        return encrypted;
    }

    private static byte[] ReadField(byte[] source, ref int offset)
    {
        int length = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan(offset)); offset += 4;
        byte[] value = source.AsSpan(offset, length).ToArray(); offset += length; return value;
    }
    private static System.Numerics.BigInteger Parse(string hex) => new(Convert.FromHexString(hex.Length % 2 == 0 ? hex : "0" + hex), true, true);
    private static void AssertFixture(bool condition) { if (!condition) throw new InvalidOperationException("H1 client fixture rejected the server candidate."); }
    public void Dispose() { initial.Dispose(); game.Dispose(); }
}
