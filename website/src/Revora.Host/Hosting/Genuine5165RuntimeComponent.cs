using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Revora.Application.Authentication;
using Revora.Domain.Accounts;
using Revora.Gameplay.Models;
using Revora.NativeLogin.Core;
using Revora.Persistence.PostgreSql;
using Revora.Host.Logging;
using Revora.Host.Gameplay;
using Revora.Protocol5165;

namespace Revora.Host.Hosting;

public sealed class Genuine5165RuntimeComponent : IRevoraRuntimeComponent, IPlayerCountProvider
{
    private enum GameSessionStage
    {
        RegisteringCharacter,
        CharacterCommitted,
        EnteringWorld,
        InWorld,
    }

    private readonly IPAddress address;
    private readonly int port;
    private readonly int maximumConnections;
    private readonly bool accountMode;
    private readonly string advertisedGameAddress;
    private readonly int gamePort;
    private readonly IPAddress allowedSource;
    private readonly PostgreSqlGameDataStore store;
    private readonly SafeConsoleLog log;
    private readonly GameplayWorld5165? gameplay;
    private readonly ItemEconomyRuntime5165? economy;
    private readonly LiveCombatRuntime5165? combat;
    private readonly AuthenticationRateLimiter limiter = new(5, TimeSpan.FromMinutes(1), 10_000);
    private readonly ConcurrentDictionary<Guid, (TcpClient Client, Task Task)> connections = new();
    private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TcpListener? listener;
    private CancellationTokenSource? lifetime;
    private Task? acceptTask;
    private int state = (int)HealthState.Stopped;
    private int intentionalStop;
    private int players;

    public Genuine5165RuntimeComponent(string name, IPAddress address, int port, int maximumConnections,
        bool accountMode, string advertisedGameAddress, int gamePort, string allowedSourceAddress, PostgreSqlGameDataStore store, SafeConsoleLog log,
        GameplayWorld5165? gameplay = null, ItemEconomyRuntime5165? economy = null,
        LiveCombatRuntime5165? combat = null)
    {
        Name = name; this.address = address; this.port = port; this.maximumConnections = maximumConnections; this.log = log;
        this.accountMode = accountMode; this.advertisedGameAddress = advertisedGameAddress; this.gamePort = gamePort; this.store = store;
        this.gameplay = gameplay;
        this.economy = economy;
        this.combat = combat;
        if (!accountMode && gameplay is null)
            throw new ArgumentNullException(nameof(gameplay), "The Game listener requires a reviewed gameplay world.");
        if (!IPAddress.TryParse(allowedSourceAddress, out allowedSource!) || allowedSource.AddressFamily != AddressFamily.InterNetwork)
            throw new ArgumentException("The owner source must be numeric IPv4.", nameof(allowedSourceAddress));
        if (!IPAddress.TryParse(advertisedGameAddress, out IPAddress? advertised) || advertised.AddressFamily != AddressFamily.InterNetwork)
            throw new ArgumentException("The advertised Game endpoint must be numeric IPv4.", nameof(advertisedGameAddress));
        Span<byte> proof = stackalloc byte[AccountPackets5165.ConnectSuccessLength];
        AccountPackets5165.WriteSuccess(proof, 1, advertisedGameAddress, checked((uint)gamePort));
        _ = AccountPackets5165.ParseSuccessAsExactClient(proof);
        CryptographicOperations.ZeroMemory(proof);
    }

    public string Name { get; }
    public Task Completion => completion.Task;
    public int PlayerCount => accountMode ? 0 : Volatile.Read(ref players);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Volatile.Write(ref state, (int)HealthState.Starting);
        lifetime = new(); listener = new(address, port); listener.Start(maximumConnections);
        acceptTask = AcceptAsync(listener, lifetime.Token);
        Volatile.Write(ref state, (int)HealthState.Ready);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        Interlocked.Exchange(ref intentionalStop, 1); Volatile.Write(ref state, (int)HealthState.Draining);
        listener?.Stop(); if (lifetime is not null) await lifetime.CancelAsync();
        foreach ((TcpClient client, _) in connections.Values) client.Dispose();
        if (acceptTask is not null) await IgnoreAsync(acceptTask);
        await Task.WhenAll(connections.Values.Select(value => value.Task)).WaitAsync(cancellationToken);
        Volatile.Write(ref state, (int)HealthState.Stopped); completion.TrySetResult();
    }

    public ComponentHealth GetHealth() => new(Name, (HealthState)Volatile.Read(ref state),
        $"Genuine 5165 bounded runtime; connections={connections.Count}; authenticatedPlayers={PlayerCount}.");

    private async Task AcceptAsync(TcpListener active, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient client = await active.AcceptTcpClientAsync(token); client.NoDelay = true;
                if (connections.Count >= maximumConnections) { client.Dispose(); continue; }
                Guid id = Guid.NewGuid(); Task task = RunAsync(client, token);
                if (!connections.TryAdd(id, (client, task))) { client.Dispose(); continue; }
                _ = task.ContinueWith(completed => { _ = completed.Exception; connections.TryRemove(id, out var removed); }, CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (SocketException) when (Volatile.Read(ref intentionalStop) != 0) { }
        catch (ObjectDisposedException) when (Volatile.Read(ref intentionalStop) != 0) { }
        catch (Exception error) { Volatile.Write(ref state, (int)HealthState.Failed); completion.TrySetException(new InvalidOperationException("5165 listener failed.", error)); }
    }

    private async Task RunAsync(TcpClient client, CancellationToken lifetimeToken)
    {
        using (client)
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetimeToken))
        {
            timeout.CancelAfter(accountMode ? TimeSpan.FromSeconds(30) : TimeSpan.FromMinutes(2));
            try
            {
                string source = ((IPEndPoint?)client.Client.RemoteEndPoint)?.Address.ToString() ?? "unknown";
                if (!IPAddress.TryParse(source, out IPAddress? remote) || !remote.Equals(allowedSource)) return;
                if (!limiter.TryAcquire(source)) return;
                if (accountMode) await RunAccountAsync(client.GetStream(), timeout.Token);
                else await RunGameAsync(client.GetStream(), timeout);
            }
            catch (Exception error) when (error is IOException or SocketException or InvalidDataException or OperationCanceledException)
            {
                log.Warning(Name, "BoundedConnectionRejected", $"A 5165 connection stopped safely ({SafeCategory(error)}).");
            }
            catch
            {
                log.Error(Name, "UnexpectedConnectionFailure", "A 5165 connection failed without exposing exception or packet details.");
            }
        }
    }

    private async Task RunAccountAsync(NetworkStream stream, CancellationToken token)
    {
        byte[] encrypted = new byte[AccountPackets5165.LoginLength]; byte[] plaintext = new byte[encrypted.Length];
        byte[] response = new byte[AccountPackets5165.ConnectSuccessLength]; byte[] outbound = new byte[response.Length];
        try
        {
            await stream.ReadExactlyAsync(encrypted, token);
            var cipher = new AccountCipher5165(); cipher.DecryptClient(encrypted, plaintext);
            using AccountLogin5165 login = AccountPackets5165.ParseLogin(plaintext);
            log.Audit("Account1086Validated", "A bounded genuine 276/1086 request was validated.");
            Guid? account = await store.AuthenticateAsync(login.Username, login.PasswordBytes, token);
            if (account is null) { WriteRejection(response); cipher.EncryptServer(response.AsSpan(0, 12), outbound); await stream.WriteAsync(outbound.AsMemory(0, 12), token); return; }
            ulong handoff = await store.IssueLegacyHandoffAsync(account.Value, TimeSpan.FromSeconds(30), token);
            AccountPackets5165.WriteSuccess(response, handoff, advertisedGameAddress, checked((uint)gamePort));
            _ = AccountPackets5165.ParseSuccessAsExactClient(response);
            cipher.EncryptServer(response, outbound); await stream.WriteAsync(outbound, token); await stream.FlushAsync(token);
            log.Audit("Account1055Sent", "A genuine 32/1055 success response was sent with a short-lived handoff.");
        }
        finally { CryptographicOperations.ZeroMemory(encrypted); CryptographicOperations.ZeroMemory(plaintext); CryptographicOperations.ZeroMemory(response); CryptographicOperations.ZeroMemory(outbound); }
    }

    private async Task RunGameAsync(NetworkStream stream, CancellationTokenSource deadline)
    {
        CancellationToken token = deadline.Token;
        bool authenticated = false;
        Guid? sessionId = null;
        using var handshake = new GameHandshake5165(); byte[] hello = handshake.CreateServerHandshake();
        log.Audit("GameConnectionAccepted", "The owner-scoped genuine Game connection was accepted.");
        try { await stream.WriteAsync(hello, token); await stream.FlushAsync(token); }
        finally { CryptographicOperations.ZeroMemory(hello); }
        ResetGameIdleDeadline(deadline);
        byte[] first = new byte[11]; await stream.ReadExactlyAsync(first, token);
        try { if (handshake.ConsumeClientDh(first)) throw new InvalidDataException("Premature DH completion."); }
        finally { CryptographicOperations.ZeroMemory(first); }
        int dhLength = handshake.ExpectedClientDhWireLength ?? throw new InvalidDataException("Missing DH length.");
        ResetGameIdleDeadline(deadline);
        byte[] remainder = new byte[dhLength - 11];
        try { await stream.ReadExactlyAsync(remainder, token); if (!handshake.ConsumeClientDh(remainder)) throw new InvalidDataException("DH rejected."); }
        finally { CryptographicOperations.ZeroMemory(remainder); }
        ResetGameIdleDeadline(deadline);
        byte[] game = new byte[GamePackets5165.ConnectLength + 8];
        try
        {
            await stream.ReadExactlyAsync(game, token); if (!handshake.ConsumeEncryptedGame(game)) throw new InvalidDataException("1052 rejected.");
            GameConnect5165 connect = handshake.Connect ?? throw new InvalidDataException("1052 missing.");
            ulong wire = ((ulong)connect.TokenHigh << 32) | connect.TokenLow;
            Guid? account = await store.ConsumeLegacyHandoffAsync(wire, token); if (account is null) throw new InvalidDataException("Handoff rejected.");
            log.Audit("Game1052Validated", "Genuine 28/1052 and TQClient validated; the handoff was consumed once.");
            Interlocked.Increment(ref players); authenticated = true;
            sessionId = Guid.NewGuid();
            await store.OpenSessionAsync(sessionId.Value, account.Value, Guid.NewGuid(), token);
            CharacterState? character = await store.GetCharacterAsync(account.Value, token);
            if (character is null)
                await RunCharacterRegistrationAsync(stream, handshake, account.Value, connect, deadline);
            else
                await RunFirstWorldEntryAsync(stream, handshake, new AccountId(account.Value), character, deadline,
                    sendEntranceAcknowledgement: true);
        }
        finally
        {
            if (sessionId is not null)
            {
                try { await store.CloseSessionAsync(sessionId.Value, "connection-ended", CancellationToken.None); }
                catch { log.Error(Name, "SessionCloseFailure", "A Game session close could not be recorded."); }
            }
            if (authenticated) Interlocked.Decrement(ref players);
            CryptographicOperations.ZeroMemory(game);
        }
    }

    private async Task RunCharacterRegistrationAsync(NetworkStream stream, GameHandshake5165 handshake, Guid accountId,
        GameConnect5165 connect, CancellationTokenSource deadline)
    {
        CancellationToken token = deadline.Token;
        GameSessionStage sessionStage = GameSessionStage.RegisteringCharacter;
        await SendGamePacketAsync(stream, handshake, EncodeNewRoleForEvidence(), token);
        log.Audit("GameNewRoleSent", "The evidence-supported encrypted NEW_ROLE response was sent.");
        ResetGameIdleDeadline(deadline);
        byte[] registrationPacket = await ReadGamePacketAsync(stream, handshake, token);
        try
        {
            ushort observedLength = BinaryPrimitives.ReadUInt16LittleEndian(registrationPacket);
            ushort observedId = BinaryPrimitives.ReadUInt16LittleEndian(registrationPacket.AsSpan(2));
            log.Audit("PostNewRoleFrameReceived", $"A decrypted frame passed bounded framing and footer validation (length={observedLength}; id={observedId}).");
            using CharacterRegistration5165 registration = GamePacketsAfterAuthentication5165.ParseRegistration(registrationPacket);
            log.Audit("CharacterRegistration1001Validated", "A bounded genuine 104/1001 registration request was validated.");
            bool accountFieldExact = registration.Account.Length == 0;
            bool accountWordMatches = registration.AccountId == connect.TokenLow;
            bool nameValid = GamePacketsAfterAuthentication5165.IsValidCharacterName(registration.Name);
            bool reservedEmpty = registration.SensitiveReserved.AsSpan().IndexOfAnyExcept((byte)0) < 0;
            bool meshValid = GamePacketsAfterAuthentication5165.IsValidMesh(registration.Mesh);
            bool professionValid = GamePacketsAfterAuthentication5165.IsValidProfession(registration.Profession);
            log.Audit("CharacterRegistrationDecision",
                $"The exact-client registration predicates were evaluated (accountFieldExact={accountFieldExact}; accountWordMatches={accountWordMatches}; nameValid={nameValid}; reservedEmpty={reservedEmpty}; reservedAccepted=true; mesh={registration.Mesh}; meshValid={meshValid}; profession={registration.Profession}; professionValid={professionValid}).");
            bool valid = accountFieldExact && accountWordMatches && nameValid && meshValid && professionValid;
            if (!valid)
            {
                log.Warning(Name, "CharacterRegistrationRejected", "The 104/1001 request failed bounded account, token, name, mesh, or profession validation.");
                await SendGamePacketAsync(stream, handshake,
                    GamePacketsAfterAuthentication5165.EncodeTalk(GamePacketsAfterAuthentication5165.RegistrationChannel, "Invalid character registration"), token);
                return;
            }

            CharacterState? created = await store.TryCreateCharacterAsync(accountId, registration.Name,
                registration.Mesh, registration.Profession, token);
            if (created is null)
            {
                await SendGamePacketAsync(stream, handshake,
                    GamePacketsAfterAuthentication5165.EncodeTalk(GamePacketsAfterAuthentication5165.RegistrationChannel, "Character name already in use"), token);
                return;
            }

            sessionStage = GameSessionStage.CharacterCommitted;
            await SendGamePacketAsync(stream, handshake,
                GamePacketsAfterAuthentication5165.EncodeTalk(GamePacketsAfterAuthentication5165.RegistrationChannel, "ANSWER_OK"), token);
            log.Audit("CharacterCreated", "The character was created atomically; the authenticated Game session is being promoted directly into world entry.");
            sessionStage = GameSessionStage.EnteringWorld;
            await RunFirstWorldEntryAsync(stream, handshake, new AccountId(accountId), created, deadline,
                sendEntranceAcknowledgement: false,
                () =>
                {
                    sessionStage = GameSessionStage.InWorld;
                    log.Audit("CharacterRegistrationSessionPromoted",
                        "The registration session reached the in-world state without another Account login, handoff, DH exchange, or 1052.");
                });
        }
        finally
        {
            if (sessionStage is GameSessionStage.CharacterCommitted or GameSessionStage.EnteringWorld)
                log.Warning(Name, "PostCommitWorldEntryFailure", "Character persistence succeeded but same-session world initialization did not complete; normal login recovery remains available.");
            CryptographicOperations.ZeroMemory(registrationPacket);
        }
    }

    private async Task RunFirstWorldEntryAsync(NetworkStream stream, GameHandshake5165 handshake, AccountId accountId,
        CharacterState character,
        CancellationTokenSource deadline, bool sendEntranceAcknowledgement, Action? onEnteredWorld = null)
    {
        CancellationToken token = deadline.Token;
        // SemaphoreSlim allocates no kernel handle unless WaitHandle is used.
        // Do not dispose it here: a peer broadcast may already hold a bounded
        // reference while this session is being removed from the world.
        var sendGate = new SemaphoreSlim(1, 1);
        var npcRouter = new NpcActivationRouter5165(gameplay!);
        ItemEconomySession5165? economySession = economy is null ? null :
            await economy.OpenSessionAsync(accountId, new CharacterUid(checked((uint)character.LegacyUid)), token);
        ProgressionServiceSession5165? progressionSession = economy is null ? null :
            await economy.OpenProgressionSessionAsync(accountId,
                new CharacterUid(checked((uint)character.LegacyUid)), token);
        bool equipmentAppearanceEnabled = economySession is not null &&
            await store.IsCharacterFeatureEnabledAsync(accountId, character.LegacyUid,
                "world.player.equipment.visibility", token);
        log.Audit("PlayerEquipmentAppearanceGateEvaluated",
            $"The authoritative peer-equipment presentation gate was evaluated " +
            $"(enabled={equipmentAppearanceEnabled}; scope=immutable-character-canary; characterUid={character.LegacyUid}).");
        // Hydration is authoritative and read-only for every connected character.
        // Pairwise emission remains restricted to the immutable canary below, so
        // an ordinary peer can still be represented correctly to the canary.
        GamePlayerAppearance5165 initialAppearance = economySession is null
            ? default
            : await economySession.ReadAppearanceAsync(token);
        var worldSession = new GameplaySession5165(character,
            (packet, sendToken) => SendGamePacketAsync(stream, handshake, packet, sendGate, sendToken),
            appearance: initialAppearance, peerAppearanceCanary: equipmentAppearanceEnabled);
        bool worldRegistered = false;
        bool combatRegistered = false;
        bool staminaRegistered = false;
        ulong pingSequence = 0;
        if (sendEntranceAcknowledgement)
            await SendGamePacketAsync(stream, handshake,
                GamePacketsAfterAuthentication5165.EncodeTalk(GamePacketsAfterAuthentication5165.EntranceChannel, "ANSWER_OK"), token);
        ProgressionCharacterSnapshot? progression = progressionSession is null
            ? null
            : await progressionSession.ReadAsync(token);
        byte[] userInfo = progression is null
            ? GamePacketsAfterAuthentication5165.EncodeUserInfo(checked((uint)character.LegacyUid),
                character.Name, character.Mesh, character.Profession)
            : GamePacketsAfterAuthentication5165.EncodeUserInfo(checked((uint)character.LegacyUid), character.Name,
                character.Mesh, progression.Profession, progression.Level, checked((uint)progression.Silver),
                checked((uint)progression.ConquerPoints), checked((ulong)progression.Experience),
                progression.Strength, progression.Agility, progression.Vitality, progression.Spirit,
                checked((ushort)progression.AvailableAttributePoints), checked((ushort)progression.CurrentHealth),
                checked((ushort)progression.CurrentMana), 0);
        UserInformation5165 exact = GamePacketsAfterAuthentication5165.ParseUserInfoAsExactClient(userInfo);
        await SendGamePacketAsync(stream, handshake, userInfo, token);
        log.Audit("CharacterInformationSent",
            $"The exact-client 1006 character information was sent (entranceAcknowledgement={sendEntranceAcknowledgement}; length={GamePacketsAfterAuthentication5165.UserInfoStringPackOffset + 3 + exact.Name.Length + exact.Spouse.Length}; stringPackOffset={GamePacketsAfterAuthentication5165.UserInfoStringPackOffset}).");
        if (economySession is not null)
        {
            IReadOnlyList<byte[]> initial = await economySession.InitialPacketsAsync(token);
            foreach (byte[] itemPacket in initial)
                await SendGamePacketAsync(stream, handshake, itemPacket, sendGate, token);
            log.Audit("ItemEconomySessionPrepared",
                $"The bounded item/economy runtime was evaluated (enabled={economySession.Enabled}; scope={economySession.Rollout.Scope}; initialPackets={initial.Count}).");
            if (economySession.Enabled)
            {
                DateTimeOffset utcNow = TimeProvider.System.GetUtcNow();
                byte[] serverClock = ClientTimingPackets5165.EncodeServerClock(utcNow,
                    ClientTimingPackets5165.ResolveCairoTimeZone());
                ClientClockFields5165 fields = ClientTimingPackets5165.ParseAsExactClient(serverClock);
                await SendGamePacketAsync(stream, handshake, serverClock, sendGate, token);
                log.Audit("ServerClock1033Sent",
                    $"The exact 36/1033 calendar frame was sent to the timing canary " +
                    $"(zone={ClientTimingPackets5165.CairoTimeZoneId}; year={fields.Year}; month={fields.Month}; " +
                    $"day={fields.Day}; hour={fields.Hour}; minute={fields.Minute}; second={fields.Second}).");
            }
        }

        bool entered = false;
        try
        {
            while (!token.IsCancellationRequested)
            {
                ResetGameIdleDeadline(deadline);
                byte[] packet = await ReadGamePacketAsync(stream, handshake, token);
                try
                {
                    ushort packetLength = BinaryPrimitives.ReadUInt16LittleEndian(packet);
                    ushort packetId = BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(2));
                    if (packetId == GamePacketsAfterAuthentication5165.RegisterPacketId)
                        throw new InvalidDataException("Character registration was repeated after the session left its registration state.");
                    if (packetId == GamePacketsAfterAuthentication5165.MovementPacketId)
                    {
                        if (!worldRegistered) throw new InvalidDataException("Movement arrived before world entry completed.");
                        GameMovement5165 movement = GamePacketsAfterAuthentication5165.ParseMovement(packet);
                        CharacterState updated = await gameplay!.WalkAsync(worldSession, movement, store, token);
                        character = updated;
                        combat?.MarkStaminaActivity(worldSession, StaminaActivity.Moving);
                        if (combatRegistered) await combat!.RelocatePlayerAsync(worldSession, token);
                        log.Audit("Movement10005Accepted",
                            $"An authoritative movement was accepted (length=20; id=10005; direction={movement.Direction % 8}; mode={movement.Mode}; map={updated.MapId}; x={updated.X}; y={updated.Y}).");
                        continue;
                    }
                    if (packetId == NinjaPackets5165.InteractionPacketId)
                    {
                        if (!worldRegistered) throw new InvalidDataException("Combat arrived before world entry completed.");
                        try
                        {
                            uint attackAction = packetLength >= 24
                                ? BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(20))
                                : uint.MaxValue;
                            if (attackAction is NinjaPackets5165.MeleeAction or NinjaPackets5165.RangedAction)
                            {
                                combat?.MarkStaminaActivity(worldSession, StaminaActivity.Attacking);
                                NormalAttack5165 normal = NinjaPackets5165.ParseNormalAttack(packet,
                                    checked((uint)character.LegacyUid));
                                if (combat is null)
                                {
                                    log.Audit("Combat1022Rejected", "A well-framed normal attack was rejected because the gated combat runtime is unavailable.");
                                    continue;
                                }
                                await combat.HandleNormalAttackAsync(accountId, worldSession, normal, token);
                            }
                            else if (attackAction == NinjaPackets5165.MagicAction)
                            {
                                combat?.MarkStaminaActivity(worldSession, StaminaActivity.Attacking);
                                SkillAttack5165 skill = NinjaPackets5165.ParseSkillAttack(packet,
                                    checked((uint)character.LegacyUid));
                                if (combat is null)
                                {
                                    log.Audit("Ninja1022Rejected", "A well-framed skill request was rejected because the gated combat runtime is unavailable.");
                                    continue;
                                }
                                await combat.HandleNinjaSkillAsync(accountId, worldSession, skill, token);
                            }
                            else
                            {
                                log.Audit("Combat1022Rejected", $"A well-framed interaction used an unsupported attack action (action={attackAction}).");
                            }
                        }
                        catch (InvalidDataException)
                        {
                            log.Audit("Combat1022Rejected", $"A malformed or unauthenticated combat frame was rejected without closing the Game session (length={packetLength}).");
                        }
                        continue;
                    }
                    if (packetId == GamePacketsAfterAuthentication5165.NpcInteractionPacketId)
                    {
                        if (!worldRegistered) throw new InvalidDataException("NPC interaction arrived before world entry completed.");
                        NpcInteraction5165 interaction = GamePacketsAfterAuthentication5165.ParseNpcInteraction(packet);
                        NpcActivationOutcome5165 route = await npcRouter.ActivateAsync(
                            worldSession, economySession, progressionSession, interaction, token);
                        log.Audit(route.Accepted ? "Npc2031Routed" : "Npc2031Rejected",
                            $"A bounded NPC activation was classified (length=16; id=2031; npc={interaction.Id}; " +
                            $"map={worldSession.Character.MapId}; playerX={route.PlayerX}; playerY={route.PlayerY}; " +
                            $"npcX={route.NpcX}; npcY={route.NpcY}; visibleAndDistance={(route.Accepted ? "passed" : "rejected")}; " +
                            $"route={route.Kind}; category={route.Category}; packets={route.PacketCount}).");
                        continue;
                    }
                    if (packetId == GroundItemPackets5165.PacketId)
                    {
                        if (!worldRegistered || combat is null || economySession is null)
                            throw new InvalidDataException("Ground pickup arrived before the authenticated world runtime was available.");
                        uint subject = packetLength >= 8
                            ? BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(4))
                            : 0;
                        uint field8 = packetLength >= 12
                            ? BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(8))
                            : 0;
                        ushort x = packetLength >= 14
                            ? BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(12))
                            : (ushort)0;
                        ushort y = packetLength >= 16
                            ? BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(14))
                            : (ushort)0;
                        ushort field16 = packetLength >= 18
                            ? BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(16))
                            : (ushort)0;
                        byte field18 = packetLength >= 19 ? packet[18] : (byte)0;
                        byte field19 = packetLength >= 20 ? packet[19] : (byte)0;
                        log.Audit("Ground1101FrameDecoded",
                            $"A bounded ground-item frame passed encrypted framing (length={packetLength}; id=1101; " +
                            $"subject={subject}; field8={field8}; x={x}; y={y}; field16={field16}; " +
                            $"field18={field18}; field19={field19}).");
                        IReadOnlyList<byte[]> responses;
                        try
                        {
                            responses = await combat.HandleGroundPickupAsync(accountId,
                                worldSession, packet, economySession, token);
                        }
                        catch (InvalidDataException)
                        {
                            log.Audit("Ground1101Rejected",
                                $"A well-framed ground-item request failed bounded semantic validation without closing the authenticated session (length={packetLength}; subject={subject}).");
                            await SendGamePacketAsync(stream, handshake,
                                GamePacketsAfterAuthentication5165.EncodeTalk(2005,
                                    "That ground item is not available."), sendGate, token);
                            continue;
                        }
                        foreach (byte[] response in responses)
                            await SendGamePacketAsync(stream, handshake, response, sendGate, token);
                        log.Audit("Ground1101Handled",
                            $"A bounded authenticated ground-item request completed (length={packetLength}; responses={responses.Count}).");
                        continue;
                    }
                    if (packetId == GamePacketsAfterAuthentication5165.NpcDialogPacketId)
                    {
                        if (!worldRegistered) throw new InvalidDataException("NPC dialog answer arrived before world entry completed.");
                        byte option = packetLength > 10 ? packet[10] : byte.MaxValue;
                        byte dialogAction = packetLength > 11 ? packet[11] : byte.MaxValue;
                        string trailing = packetLength >= 12 && packet.AsSpan(12).IndexOfAnyExcept((byte)0) < 0
                            ? "all-zero"
                            : "structured-or-nonzero";
                        log.Audit("Npc2032FrameDecoded",
                            $"A bounded NPC dialog frame passed encrypted framing (length={packetLength}; id=2032; option={option}; action={dialogAction}; trailing={trailing}).");
                        NpcDialogAnswer5165 answer = GamePacketsAfterAuthentication5165.ParseNpcDialogAnswer(packet);
                        try
                        {
                            if (worldSession.ActiveNpcInteractionKind == NpcInteractionKind5165.ProgressionService &&
                                progressionSession is not null)
                            {
                                ProgressionMutationResult? result = await progressionSession.HandleAsync(
                                    worldSession, economySession, answer, 6, token);
                                if (result is not null)
                                    log.Audit(result.Succeeded ? "ProgressionServiceCommitted" : "ProgressionServiceRejected",
                                        $"A reviewed paid service completed safely (category={result.Category}; charged={result.ChargedConquerPoints}; createdItems={result.CreatedItemUids.Count}; replayed={result.Replayed}).");
                            }
                            else
                            {
                                await gameplay!.SelectNpcDialogOptionAsync(worldSession, answer, token);
                            }
                            log.Audit("Npc2032Accepted",
                                $"A bounded NPC dialog event was accepted (length={packetLength}; id=2032; option={answer.Option}; action={answer.Action}; route={worldSession.ActiveNpcInteractionKind}).");
                        }
                        catch (InvalidDataException)
                        {
                            log.Audit("Npc2032Rejected",
                                $"A stale or cross-route NPC dialog event was rejected without closing the authenticated session (length={packetLength}; option={answer.Option}; action={answer.Action}).");
                        }
                        continue;
                    }
                    if (packetId == GamePacketsAfterAuthentication5165.TalkPacketId)
                    {
                        uint channel = packetLength >= 12
                            ? BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(8))
                            : uint.MaxValue;
                        byte stringCount = packetLength >= 25 ? packet[24] : byte.MaxValue;
                        log.Audit("Talk1004FrameDecoded",
                            $"A bounded Talk frame passed encrypted framing (length={packetLength}; id=1004; channel={channel}; " +
                            $"stringCount={stringCount}; {DescribeTalkStructure(packet)})." );
                        string words;
                        try
                        {
                            words = GamePacketsAfterAuthentication5165.ParseClientTalkText(packet);
                        }
                        catch (InvalidDataException error)
                        {
                            log.Audit("Talk1004Rejected",
                                $"A Talk frame was rejected without closing the authenticated session (category={SafeTalkFailureCategory(error)}; length={packetLength}; channel={channel}; stringCount={stringCount}).");
                            await SendGamePacketAsync(stream, handshake,
                                GamePacketsAfterAuthentication5165.EncodeTalk(2005, "That command is not available."), sendGate, token);
                            continue;
                        }
                        if (string.Equals(words, "@logout", StringComparison.OrdinalIgnoreCase))
                        {
                            if (channel != 2000)
                            {
                                await SendGamePacketAsync(stream, handshake,
                                    GamePacketsAfterAuthentication5165.EncodeTalk(2005, "That command is not available."), sendGate, token);
                                log.Audit("GameplayLogoutRejected", "A reviewed GM command was rejected because it did not arrive on the normal Talk channel.");
                                continue;
                            }
                            bool authorized = await store.HasGameplayRoleAsync(accountId.Value, character.Id, "gm", token);
                            if (!authorized)
                            {
                                await SendGamePacketAsync(stream, handshake,
                                    GamePacketsAfterAuthentication5165.EncodeTalk(2005, "That command is not available."), sendGate, token);
                                log.Audit("GameplayLogoutRejected", "A self-logout command was rejected because the authenticated character lacks the reviewed gameplay role.");
                                continue;
                            }
                            await SendGamePacketAsync(stream, handshake,
                                GamePacketsAfterAuthentication5165.EncodeTalk(2000, "Logging out safely."), sendGate, token);
                            log.Audit("GameplayLogoutAccepted", "A reviewed GM self-logout command closed only its authenticated Game session.");
                            return;
                        }
                        if (string.Equals(words, "@givemeall", StringComparison.OrdinalIgnoreCase))
                        {
                            if (channel != 2000)
                            {
                                await SendGamePacketAsync(stream, handshake,
                                    GamePacketsAfterAuthentication5165.EncodeTalk(2005, "That command is not available."), sendGate, token);
                                log.Audit("GameplayGiveMeAllRejected", "A reviewed GM command was rejected because it did not arrive on the normal Talk channel.");
                                continue;
                            }
                            if (progressionSession is null)
                                throw new InvalidDataException("The reviewed progression runtime is unavailable.");
                            ProgressionMutationResult result = await progressionSession.GiveMeAllAsync(token);
                            if (!result.Succeeded)
                            {
                                await SendGamePacketAsync(stream, handshake,
                                    GamePacketsAfterAuthentication5165.EncodeTalk(2005, "That command is not available."), sendGate, token);
                                log.Audit("GameplayGiveMeAllRejected", "A reviewed GM balance-cap command was rejected without changing state.");
                                continue;
                            }
                            await SendGamePacketAsync(stream, handshake,
                                ItemEconomyPackets5165.EncodeStatus(result.Character.LegacyUid, 4,
                                    checked((ulong)result.Character.Silver)), sendGate, token);
                            await SendGamePacketAsync(stream, handshake,
                                ItemEconomyPackets5165.EncodeStatus(result.Character.LegacyUid, 29,
                                    checked((ulong)result.Character.ConquerPoints)), sendGate, token);
                            await SendGamePacketAsync(stream, handshake,
                                GamePacketsAfterAuthentication5165.EncodeTalk(2000,
                                    $"Balances set safely: Silver {result.Character.Silver}; CPs {result.Character.ConquerPoints}."),
                                sendGate, token);
                            log.Audit("GameplayGiveMeAllAccepted", "The authenticated active GM received the exact reviewed balance caps.");
                            continue;
                        }
                        if (string.Equals(words, "@clearinv", StringComparison.OrdinalIgnoreCase))
                        {
                            if (channel != 2000)
                            {
                                await SendGamePacketAsync(stream, handshake,
                                    GamePacketsAfterAuthentication5165.EncodeTalk(2005, "That command is not available."), sendGate, token);
                                log.Audit("GameplayClearInventoryRejected", "The GM inventory-clear command was rejected outside the normal Talk channel.");
                                continue;
                            }
                            if (progressionSession is null)
                                throw new InvalidDataException("The reviewed progression runtime is unavailable.");
                            GmInventoryClearResult result = await progressionSession.ClearInventoryAsync(token);
                            if (!result.Succeeded)
                            {
                                await SendGamePacketAsync(stream, handshake,
                                    GamePacketsAfterAuthentication5165.EncodeTalk(2005, "That command is not available."), sendGate, token);
                                log.Audit("GameplayClearInventoryRejected", "The GM inventory-clear command was rejected without changing state.");
                                continue;
                            }
                            foreach (uint itemUid in result.RemovedItemUids)
                                await SendGamePacketAsync(stream, handshake,
                                    ItemEconomyPackets5165.EncodeItemAction(itemUid, 0, ItemEconomyPackets5165.InventoryRemove),
                                    sendGate, token);
                            await SendGamePacketAsync(stream, handshake,
                                GamePacketsAfterAuthentication5165.EncodeTalk(2000,
                                    $"Inventory cleared safely: {result.RemovedItemUids.Count} item(s)."), sendGate, token);
                            log.Audit("GameplayClearInventoryAccepted",
                                $"The authenticated active GM cleared carried inventory (count={result.RemovedItemUids.Count}).");
                            continue;
                        }
                        log.Audit("Chat1004Observed", "A bounded client chat frame was accepted without enabling an unreviewed gameplay command.");
                        continue;
                    }
                    if (packetId == ItemEconomyPackets5165.ItemActionPacketId)
                    {
                        if (!worldRegistered || economySession is null)
                            throw new InvalidDataException("An item action arrived before the reviewed runtime was available.");
                        ItemAction5165 itemAction = ItemEconomyPackets5165.ParseItemAction(packet);
                        bool isPing = itemAction.Action == ItemEconomyPackets5165.Ping && economySession.Enabled;
                        long pingReceiveTimestamp = isPing ? Stopwatch.GetTimestamp() : 0;
                        DateTimeOffset pingReceiveUtc = isPing ? TimeProvider.System.GetUtcNow() : default;
                        if (!isPing)
                            log.Audit("Item1009FrameDecoded",
                                $"A bounded item action passed encrypted framing (length={packetLength}; id=1009; " +
                                $"action={itemAction.Action}; subject={itemAction.Subject}; command={itemAction.Command}; " +
                                $"argument={itemAction.Argument}; timestamp={(itemAction.Timestamp == 0 ? "zero" : "present")}; reserved=all-zero).");
                        bool NpcAuthorized(uint npc) => gameplay!.CanInteractWithNpc(worldSession, npc) &&
                            (itemAction.Action is ItemEconomyPackets5165.ShopBuy or ItemEconomyPackets5165.ShopSell
                                ? worldSession.HasActiveNpcInteraction(NpcInteractionKind5165.Shop, npc)
                                : itemAction.Action == ItemEconomyPackets5165.WarehouseOpen
                                    ? worldSession.HasActiveNpcInteraction(NpcInteractionKind5165.Warehouse, npc)
                                    : true);
                        IReadOnlyList<byte[]> responses = await economySession.HandleItemActionAsync(packet,
                            NpcAuthorized,
                            combat?.IsItemUseSuppressed(checked((uint)worldSession.Character.LegacyUid)) == true,
                            async (operationId, itemUid, itemType, healthRecovery, manaRecovery, cancellationToken) =>
                                combat is null
                                    ? new RecoveryItemPersistenceResult(RecoveryItemPersistenceCode.Conflict, 0, 0, 0)
                                    : await combat.ConsumeRecoveryItemAsync(accountId, worldSession, operationId,
                                        itemUid, itemType, healthRecovery, manaRecovery, cancellationToken),
                            token);
                        string responseOrder = string.Join(',', responses.Select(response =>
                            $"{BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(2))}/{BinaryPrimitives.ReadUInt16LittleEndian(response)}"));
                        long pingResponseSendTimestamp = isPing ? Stopwatch.GetTimestamp() : 0;
                        DateTimeOffset pingResponseSendUtc = isPing ? TimeProvider.System.GetUtcNow() : default;
                        foreach (byte[] response in responses)
                            await SendGamePacketAsync(stream, handshake, response, sendGate, token);
                        if (!isPing)
                        {
                            await gameplay!.RefreshAppearanceAsync(worldSession,
                                await economySession.ReadAppearanceAsync(token), token);
                            log.Audit("PlayerEquipmentAppearanceRefreshed",
                                $"An authoritative item action refreshed visible-peer equipment presentation " +
                                $"(characterUid={character.LegacyUid}; action={itemAction.Action}).");
                        }
                        if (isPing)
                        {
                            long pingSendCompletedTimestamp = Stopwatch.GetTimestamp();
                            double processingMilliseconds = Stopwatch.GetElapsedTime(pingReceiveTimestamp,
                                pingResponseSendTimestamp).TotalMilliseconds;
                            double socketWriteMilliseconds = Stopwatch.GetElapsedTime(pingResponseSendTimestamp,
                                pingSendCompletedTimestamp).TotalMilliseconds;
                            log.Audit("Ping1009Timing",
                                $"The exact-client monotonic ping echo completed " +
                                $"(sequence={checked(++pingSequence)}; correlationTick={itemAction.Timestamp}; " +
                                $"clientSendTickMs={itemAction.Timestamp}; serverReceiveUtc={pingReceiveUtc:O}; " +
                                $"serverResponseSendUtc={pingResponseSendUtc:O}; serverProcessingMs={processingMilliseconds:F3}; " +
                                $"serverSocketWriteMs={socketWriteMilliseconds:F3}; clientResponseReceive=not-transmitted; " +
                                $"calculatedRtt=client-local-monotonic; responseTimestamp=exact-echo; tcpNoDelay=true; " +
                                $"responseOrder={responseOrder}; sends=complete).");
                        }
                        else
                            log.Audit("Item1009Handled",
                                $"A bounded item action completed through the authenticated runtime (length={packetLength}; " +
                                $"action={itemAction.Action}; responses={responses.Count}; responseOrder={responseOrder}; sends=complete).");
                        continue;
                    }
                    if (packetId == ItemEconomyPackets5165.WarehousePacketId)
                    {
                        if (!worldRegistered || economySession is null)
                            throw new InvalidDataException("A warehouse action arrived before the reviewed runtime was available.");
                        uint warehouseNpc = packetLength >= 8 ? BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(4)) : 0;
                        byte warehouseAction = packetLength >= 9 ? packet[8] : byte.MaxValue;
                        byte warehouseSelector = packetLength >= 10 ? packet[9] : byte.MaxValue;
                        uint warehouseItem = packetLength >= 16 ? BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(12)) : 0;
                        string warehouseTail = packetLength >= 16 && packet.AsSpan(16).IndexOfAnyExcept((byte)0) < 0
                            ? "all-zero"
                            : "structured-or-nonzero";
                        log.Audit("Warehouse1102FrameDecoded",
                            $"An exact-client warehouse mutation passed encrypted framing (length={packetLength}; id=1102; " +
                            $"npc={warehouseNpc}; action={warehouseAction}; item={warehouseItem}; selector={warehouseSelector}; tail={warehouseTail}; buffering=complete-frame).");
                        IReadOnlyList<byte[]> responses = await economySession.HandleWarehouseActionAsync(packet,
                            npc => gameplay!.CanInteractWithNpc(worldSession, npc) &&
                                worldSession.HasActiveNpcInteraction(NpcInteractionKind5165.Warehouse, npc), token);
                        string responseOrder = string.Join(',', responses.Select(response =>
                            $"{BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(2))}/{BinaryPrimitives.ReadUInt16LittleEndian(response)}"));
                        foreach (byte[] response in responses)
                            await SendGamePacketAsync(stream, handshake, response, sendGate, token);
                        log.Audit("Warehouse1102Handled",
                            $"A bounded warehouse action completed through the authenticated runtime (length={packetLength}; " +
                            $"action={warehouseAction}; responses={responses.Count}; responseOrder={responseOrder}; sends=complete).");
                        continue;
                    }
                    if (packetId != GamePacketsAfterAuthentication5165.ActionPacketId)
                    {
                        log.Audit("PostCharacterInformationFrameReceived",
                            $"A bounded post-1006 frame was received (length={packetLength}; id={packetId}; category={(packetId == 1134 ? "quest-status" : "non-action")}).");
                        continue;
                    }
                    var action = GamePacketsAfterAuthentication5165.ParseAction(packet);
                    log.Audit("PostCharacterInformationFrameReceived",
                        $"A bounded post-1006 action was received (length={packetLength}; id={packetId}; action={action.Action}).");
                    if (action.Id != checked((uint)character.LegacyUid))
                        throw new InvalidDataException("The action character identity does not belong to the authenticated account.");
                    if (action.Action == GamePacketsAfterAuthentication5165.EnterMapAction)
                    {
                        await SendGamePacketAsync(stream, handshake,
                            GamePacketsAfterAuthentication5165.EncodeEnterMap(checked((uint)character.LegacyUid), character.MapId,
                                character.X, character.Y, character.Direction, action.Timestamp), sendGate, token);
                        await SendGamePacketAsync(stream, handshake, GamePacketsAfterAuthentication5165.EncodeMapInfo(character.MapId), sendGate, token);
                        if (!entered)
                        {
                            entered = true;
                            await gameplay!.EnterAsync(worldSession, token);
                            worldRegistered = true;
                            if (combat is not null)
                            {
                                await combat.EnterStaminaAsync(accountId, worldSession, token);
                                staminaRegistered = true;
                                combatRegistered = await combat.EnterPlayerAsync(accountId, worldSession, token);
                            }
                            log.Audit("FirstWorldEntrySent",
                                $"The authoritative 32/10010 action 74 and 16/1110 sequence was sent (map={character.MapId}; x={character.X}; y={character.Y}).");
                            onEnteredWorld?.Invoke();
                        }
                    }
                    else if (action.Action == GamePacketsAfterAuthentication5165.JumpAction)
                    {
                        if (!worldRegistered) throw new InvalidDataException("Jump arrived before world entry completed.");
                        JumpOutcome5165 outcome = await gameplay!.JumpAsync(worldSession, action, store, token);
                        character = outcome.Character;
                        combat?.MarkStaminaActivity(worldSession, StaminaActivity.Moving);
                        if (combatRegistered) await combat!.RelocatePlayerAsync(worldSession, token);
                        log.Audit(outcome.Accepted ? "Jump10010Accepted" : "Jump10010Corrected",
                            outcome.Accepted
                                ? $"An authoritative jump was accepted (length=32; id=10010; action=137; map={character.MapId}; sourceX={action.X}; sourceY={action.Y}; targetX={outcome.DestinationX}; targetY={outcome.DestinationY}; distance={Math.Max(Math.Abs(outcome.DestinationX - action.X), Math.Abs(outcome.DestinationY - action.Y))}; mapValidation=passed)."
                                : $"A semantic jump was corrected without closing the session (length=32; id=10010; action=137; category={outcome.Category}; map={character.MapId}; authoritativeX={character.X}; authoritativeY={character.Y}).");
                    }
                    else if (action.Action == GamePacketsAfterAuthentication5165.PortalAction)
                    {
                        if (!worldRegistered) throw new InvalidDataException("Portal use arrived before world entry completed.");
                        log.Audit("Portal10010Observed",
                            $"An exact-client portal request passed framing (length=32; id=10010; action=85; map={character.MapId}; authoritativeX={character.X}; authoritativeY={character.Y}; sourceX={(ushort)(action.Data & 0xffff)}; sourceY={(ushort)(action.Data >> 16)}; portalCellX={action.X}; portalCellY={action.Y}; trailing=exact-special-action-sentinel).");
                        PortalTransition5165 transition = await gameplay!.UsePortalAsync(worldSession, action, store, token);
                        character = transition.Character;
                        combat?.MarkStaminaActivity(worldSession, StaminaActivity.Moving);
                        if (combatRegistered) await combat!.RelocatePlayerAsync(worldSession, token);
                        log.Audit("Portal10010Accepted",
                            $"An authoritative portal transition was accepted (length=32; id=10010; action=85; portal={transition.PortalId}; map={character.MapId}; x={character.X}; y={character.Y}; destinationCorrected={transition.DestinationCorrected}).");
                    }
                    else if (action.Action is 75 or 76 or 77 or 78 or 97)
                    {
                        await SendGamePacketAsync(stream, handshake, packet.ToArray(), sendGate, token);
                    }
                    else if (action.Action == 250)
                    {
                        if (!worldRegistered) throw new InvalidDataException("Sit arrived before world entry completed.");
                        combat?.MarkStaminaActivity(worldSession, StaminaActivity.Sitting);
                        await SendGamePacketAsync(stream, handshake, packet.ToArray(), sendGate, token);
                    }
                    else if (action.Action == 94)
                    {
                        if (!worldRegistered || combat is null)
                            throw new InvalidDataException("Revive arrived before the combat world was available.");
                        if (await combat.RevivePlayerAsync(accountId, worldSession, token))
                        {
                            await SendGamePacketAsync(stream, handshake, packet.ToArray(), sendGate, token);
                            log.Audit("CombatPlayerRevived",
                                $"An authenticated dead player revived atomically (uid={character.LegacyUid}; map={character.MapId}).");
                        }
                        else
                            log.Audit("CombatReviveRejected",
                                "A premature or stale revive request was rejected without disconnecting the session.");
                    }
                }
                finally { CryptographicOperations.ZeroMemory(packet); }
            }
        }
        finally
        {
            if (staminaRegistered) combat!.LeaveStamina(worldSession);
            if (combatRegistered)
            {
                try { await combat!.LeavePlayerAsync(worldSession, CancellationToken.None); }
                catch { log.Error(Name, "CombatEntityCleanupFailure", "An authenticated combat entity could not be removed cleanly."); }
            }
            if (worldRegistered) gameplay!.Leave(worldSession);
        }
    }

    private static void ResetGameIdleDeadline(CancellationTokenSource deadline) =>
        deadline.CancelAfter(TimeSpan.FromMinutes(2));

    private static async Task<byte[]> ReadGamePacketAsync(NetworkStream stream, GameHandshake5165 handshake, CancellationToken token)
    {
        byte[] encryptedHeader = new byte[4];
        byte[] header = new byte[4];
        try
        {
            await stream.ReadExactlyAsync(encryptedHeader, token);
            handshake.DecryptAuthenticatedClientBytes(encryptedHeader, header);
            int length = BinaryPrimitives.ReadUInt16LittleEndian(header);
            if (length is < 4 or > 4096) throw new InvalidDataException("The game frame length is invalid.");
            byte[] encryptedRemainder = new byte[length - 4 + 8];
            byte[] remainder = new byte[encryptedRemainder.Length];
            try
            {
                await stream.ReadExactlyAsync(encryptedRemainder, token);
                handshake.DecryptAuthenticatedClientBytes(encryptedRemainder, remainder);
                if (!remainder.AsSpan(length - 4, 8).SequenceEqual("TQClient"u8))
                    throw new InvalidDataException("The game frame footer is invalid.");
                byte[] packet = new byte[length];
                header.CopyTo(packet, 0);
                remainder.AsSpan(0, length - 4).CopyTo(packet.AsSpan(4));
                return packet;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(encryptedRemainder);
                CryptographicOperations.ZeroMemory(remainder);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encryptedHeader);
            CryptographicOperations.ZeroMemory(header);
        }
    }

    private static async Task SendGamePacketAsync(NetworkStream stream, GameHandshake5165 handshake, byte[] packet,
        CancellationToken token)
    {
        byte[] encrypted = handshake.EncryptServerPacket(packet);
        try { await stream.WriteAsync(encrypted, token); await stream.FlushAsync(token); }
        finally { CryptographicOperations.ZeroMemory(packet); CryptographicOperations.ZeroMemory(encrypted); }
    }

    private static async Task SendGamePacketAsync(NetworkStream stream, GameHandshake5165 handshake, byte[] packet,
        SemaphoreSlim gate, CancellationToken token)
    {
        await gate.WaitAsync(token);
        try { await SendGamePacketAsync(stream, handshake, packet, token); }
        finally { gate.Release(); }
    }

    public static byte[] EncodeNewRoleForEvidence()
    {
        string[] values = ["SYSTEM", "ALLUSERS", "", "NEW_ROLE"];
        int length = 25 + values.Sum(value => 1 + Encoding.ASCII.GetByteCount(value));
        byte[] packet = new byte[length]; BinaryPrimitives.WriteUInt16LittleEndian(packet, checked((ushort)length));
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), 1004); BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), 0x00ffffff);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(8), 2101); packet[24] = 4; int offset = 25;
        foreach (string value in values) { packet[offset++] = checked((byte)value.Length); Encoding.ASCII.GetBytes(value, packet.AsSpan(offset)); offset += value.Length; }
        return packet;
    }

    private static void WriteRejection(Span<byte> packet)
    {
        packet.Clear(); BinaryPrimitives.WriteUInt16LittleEndian(packet, 12); BinaryPrimitives.WriteUInt16LittleEndian(packet[2..], 1055);
        BinaryPrimitives.WriteUInt32LittleEndian(packet[8..], 1);
    }

    private static async Task IgnoreAsync(Task task) { try { await task; } catch { } }

    private static string SafeCategory(Exception error) => error switch
    {
        OperationCanceledException => "timeout-or-shutdown",
        InvalidDataException => "protocol-validation",
        SocketException => "socket-io",
        _ => "network-io",
    };

    private static string SafeTalkFailureCategory(InvalidDataException error) => error.Message switch
    {
        "The patch-5165 talk frame is invalid." => "header-or-string-count",
        "The talk string pack is truncated." => "string-pack-truncated",
        "The talk string is truncated." => "string-truncated",
        "The talk string contains unsupported bytes." => "string-encoding",
        "The talk string pack has an unsupported reserved tail." => "reserved-tail",
        _ => "talk-validation",
    };

    private static string DescribeTalkStructure(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < 25) return "fieldLengths=unavailable; tailLength=unavailable; tail=unavailable";
        int offset = 25;
        Span<int> lengths = stackalloc int[4];
        for (int field = 0; field < lengths.Length; field++)
        {
            if (offset >= packet.Length)
                return $"fieldLengths={string.Join(',', lengths[..field].ToArray())}; tailLength=unavailable; tail=truncated";
            int length = packet[offset++];
            lengths[field] = length;
            if (offset + length > packet.Length)
                return $"fieldLengths={string.Join(',', lengths[..(field + 1)].ToArray())}; tailLength=unavailable; tail=truncated";
            offset += length;
        }
        ReadOnlySpan<byte> tail = packet[offset..];
        string category = tail.Length == 0 ? "none" : tail.IndexOfAnyExcept((byte)0) < 0 ? "all-zero" : "nonzero";
        return $"fieldLengths={string.Join(',', lengths.ToArray())}; tailLength={tail.Length}; tail={category}";
    }
}
