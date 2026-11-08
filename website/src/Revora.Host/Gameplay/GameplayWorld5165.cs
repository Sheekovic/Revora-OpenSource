using System.Collections.Concurrent;
using System.Diagnostics;
using System.Buffers.Binary;
using Revora.NativeLogin.Core;
using Revora.Persistence.PostgreSql;

namespace Revora.Host.Gameplay;

public sealed class GameplayWorld5165
{
    public const int ViewRadius = 18;
    public const int MaximumJumpDistance = 17;
    private readonly IMapCatalog5165 maps;
    private readonly GameplayReferenceCatalog5165 references;
    private readonly ConcurrentDictionary<int, GameplaySession5165> sessions = new();
    private readonly Action<PlayerPeerSpawnTrace5165>? peerSpawnTrace;

    public GameplayWorld5165(IMapValidation5165 map) : this(new MapCatalog5165([map]), new()) { }

    public GameplayWorld5165(IMapCatalog5165 maps, GameplayReferenceCatalog5165 references,
        Action<PlayerPeerSpawnTrace5165>? peerSpawnTrace = null)
    {
        this.maps = maps ?? throw new ArgumentNullException(nameof(maps));
        this.references = references ?? throw new ArgumentNullException(nameof(references));
        this.peerSpawnTrace = peerSpawnTrace;
    }

    public int SessionCount => sessions.Count;

    public bool CanInteractWithNpc(GameplaySession5165 session, uint npcUid, int maximumDistance = ViewRadius) =>
        maximumDistance is >= 1 and <= ViewRadius &&
        references.TryGetNpc(npcUid, out NpcReference5165 npc) &&
        npc.MapId == session.Character.MapId &&
        Chebyshev(npc.X, npc.Y, session.Character.X, session.Character.Y) <= maximumDistance &&
        session.IsNpcVisible(npc.LegacyUid);

    public bool TryValidateNpcActivation(GameplaySession5165 session, NpcInteraction5165 interaction,
        out NpcReference5165 npc, out string category)
    {
        npc = default!;
        if (!session.NpcDialogRate.TryAcquire()) { category = "rate-limit"; return false; }
        if (interaction.Event != 0 || interaction.Type != 0) { category = "event-or-type"; return false; }
        if (!references.TryGetNpc(interaction.Id, out npc)) { category = "unknown-uid"; return false; }
        if (npc.MapId != session.Character.MapId) { category = "wrong-map"; return false; }
        if (!session.IsNpcVisible(npc.LegacyUid)) { category = "not-visible"; return false; }
        if (Chebyshev(npc.X, npc.Y, session.Character.X, session.Character.Y) > ViewRadius)
        { category = "excessive-distance"; return false; }
        category = "accepted";
        return true;
    }

    public async Task EnterAsync(GameplaySession5165 session, CancellationToken cancellationToken)
    {
        if (!maps.TryGet(session.Character.MapId, out IMapValidation5165 map) ||
            !map.CanStand(session.Character.X, session.Character.Y))
            throw new InvalidDataException("The persisted character position is not valid on the reviewed map.");
        if (!sessions.TryAdd(session.Character.LegacyUid, session))
            throw new InvalidDataException("The character already has an active world session.");

        try
        {
            foreach (GameplaySession5165 peer in sessions.Values)
            {
                if (ReferenceEquals(peer, session) || !AreVisible(session.Character, peer.Character)) continue;
                await session.SendAsync(Spawn(peer, session, "screen-entry"), cancellationToken);
                try
                {
                    await peer.SendAsync(Spawn(session, peer, "screen-entry"), cancellationToken);
                    session.MarkVisible(peer.Character.LegacyUid);
                    peer.MarkVisible(session.Character.LegacyUid);
                }
                catch (Exception exception) when (IsClosedPeer(exception))
                {
                    sessions.TryRemove(new KeyValuePair<int, GameplaySession5165>(peer.Character.LegacyUid, peer));
                    await session.SendAsync(GamePacketsAfterAuthentication5165.EncodeRemoveEntity(
                        PeerVisibleUid(peer, session)), cancellationToken);
                }
            }
            await SynchronizeNpcsAsync(session, cancellationToken);
        }
        catch
        {
            sessions.TryRemove(session.Character.LegacyUid, out _);
            throw;
        }
    }

    public void Leave(GameplaySession5165 session)
    {
        if (!sessions.TryRemove(new KeyValuePair<int, GameplaySession5165>(session.Character.LegacyUid, session)))
            return;
        foreach (GameplaySession5165 peer in sessions.Values)
        {
            peer.MarkNotVisible(session.Character.LegacyUid);
            session.MarkNotVisible(peer.Character.LegacyUid);
        }
    }

    public async Task<CharacterState> WalkAsync(GameplaySession5165 session, GameMovement5165 movement,
        PostgreSqlGameDataStore store, CancellationToken cancellationToken)
    {
        if (movement.Id != checked((uint)session.Character.LegacyUid))
            throw new InvalidDataException("Movement identity does not belong to the authenticated character.");
        if (movement.Direction > byte.MaxValue || movement.Mode is not (0 or 1) || !session.MovementRate.TryAcquire())
            throw new InvalidDataException("Movement direction, mode, or rate is invalid.");

        int direction = (int)(movement.Direction % 8);
        (int dx, int dy) = DirectionDelta(direction);
        CharacterState current = session.Character;
        IMapValidation5165 map = RequireMap(current.MapId);
        int nextX = checked(current.X + dx);
        int nextY = checked(current.Y + dy);
        if (!map.CanMove(current.X, current.Y, nextX, nextY))
            throw new InvalidDataException("Movement destination is not a valid map cell.");

        CharacterState updated = await store.MoveAsync(current.Id, nextX, nextY, checked((short)direction),
            current.Version, cancellationToken) ?? throw new InvalidDataException("Movement persistence version was stale.");
        session.Character = updated;
        byte[] authoritative = GamePacketsAfterAuthentication5165.EncodeMovement(
            movement.Direction, checked((uint)updated.LegacyUid), movement.Mode, movement.Timestamp);
        await SynchronizeMovementAsync(session, authoritative, cancellationToken);
        await SynchronizeNpcsAsync(session, cancellationToken);
        return updated;
    }

    public async Task<JumpOutcome5165> JumpAsync(GameplaySession5165 session, GameAction5165 action,
        PostgreSqlGameDataStore store, CancellationToken cancellationToken)
    {
        if (action.Action != GamePacketsAfterAuthentication5165.JumpAction ||
            action.Id != checked((uint)session.Character.LegacyUid))
            throw new InvalidDataException("Jump identity or action is invalid.");
        CharacterState current = session.Character;
        IMapValidation5165 map = RequireMap(current.MapId);
        if (!session.JumpRate.TryAcquire())
            return await RejectJumpAsync(session, current, action.Timestamp, "rate-limit", cancellationToken);
        if (action.X != current.X || action.Y != current.Y)
            return await RejectJumpAsync(session, current, action.Timestamp, "origin-mismatch", cancellationToken);
        int destinationX = checked((ushort)(action.Data & 0xffff));
        int destinationY = checked((ushort)(action.Data >> 16));
        if (Chebyshev(current.X, current.Y, destinationX, destinationY) > MaximumJumpDistance)
            return await RejectJumpAsync(session, current, action.Timestamp, "distance", cancellationToken);
        if (!map.CanMove(current.X, current.Y, destinationX, destinationY))
            return await RejectJumpAsync(session, current, action.Timestamp, "map-validation", cancellationToken);
        short direction = checked((short)(action.Direction % 8));
        CharacterState updated = await store.MoveAsync(current.Id, destinationX, destinationY, direction,
            current.Version, cancellationToken) ?? throw new InvalidDataException("Jump persistence version was stale.");
        session.Character = updated;
        byte[] authoritative = GamePacketsAfterAuthentication5165.EncodeJump(
            checked((uint)updated.LegacyUid), checked((ushort)destinationX), checked((ushort)destinationY),
            checked((ushort)current.X), checked((ushort)current.Y), checked((ushort)direction), action.Timestamp);
        await SynchronizeMovementAsync(session, authoritative, cancellationToken);
        await SynchronizeNpcsAsync(session, cancellationToken);
        return new(updated, true, "accepted", destinationX, destinationY);
    }

    public async Task<PortalTransition5165> UsePortalAsync(GameplaySession5165 session, GameAction5165 action,
        PostgreSqlGameDataStore store, CancellationToken cancellationToken)
    {
        if (action.Action != GamePacketsAfterAuthentication5165.PortalAction ||
            action.Id != checked((uint)session.Character.LegacyUid))
            throw new InvalidDataException("Portal identity or action is invalid.");
        CharacterState current = session.Character;
        int requestedX = checked((ushort)(action.Data & 0xffff));
        int requestedY = checked((ushort)(action.Data >> 16));
        PortalReference5165? portal = references.Portals(current.MapId).FirstOrDefault(candidate =>
            Chebyshev(current.X, current.Y, candidate.SourceX, candidate.SourceY) <= 4 &&
            Chebyshev(requestedX, requestedY, candidate.SourceX, candidate.SourceY) <= 4 &&
            Chebyshev(action.X, action.Y, candidate.SourceX, candidate.SourceY) <= 4);
        if (portal is null) throw new InvalidDataException("The requested portal is not within the authoritative area.");
        IMapValidation5165 destination = RequireMap(portal.DestinationMap);
        (int x, int y, bool corrected) = ResolveDestination(destination, portal.DestinationX, portal.DestinationY);
        CharacterState updated = await store.TransitionMapAsync(current.Id, portal.DestinationMap, x, y,
            current.Direction, current.Version, cancellationToken)
            ?? throw new InvalidDataException("Portal persistence version was stale.");

        await RemoveOldMapVisibilityAsync(session, cancellationToken);
        session.Character = updated;
        byte[] fly = GamePacketsAfterAuthentication5165.EncodeFlyMap(checked((uint)updated.LegacyUid),
            maps.DocumentId(updated.MapId), updated.X, updated.Y, updated.Direction, action.Timestamp);
        await session.SendAsync(fly, cancellationToken);
        await session.SendAsync(GamePacketsAfterAuthentication5165.EncodeMapInfo(updated.MapId), cancellationToken);
        await AddNewMapVisibilityAsync(session, cancellationToken);
        await SynchronizeNpcsAsync(session, cancellationToken);
        return new(updated, portal.LegacyId, maps.DocumentId(updated.MapId), x, y, corrected);
    }

    public async Task OpenGuideDialogAsync(GameplaySession5165 session, NpcReference5165 npc,
        CancellationToken cancellationToken)
    {
        session.BeginNpcInteraction(NpcInteractionKind5165.GuideDialog, npc.LegacyUid);
        await session.SendAsync(GamePacketsAfterAuthentication5165.EncodeNpcDialogText(
            "Welcome to Revora. This guide is safely available."), cancellationToken);
        await session.SendAsync(GamePacketsAfterAuthentication5165.EncodeNpcDialogOption(
            "Tell me about this place.", 1), cancellationToken);
        await session.SendAsync(GamePacketsAfterAuthentication5165.EncodeNpcDialogOption("Goodbye.", byte.MaxValue), cancellationToken);
        await session.SendAsync(GamePacketsAfterAuthentication5165.EncodeNpcDialogFace(npc.Face), cancellationToken);
        await session.SendAsync(GamePacketsAfterAuthentication5165.EncodeNpcDialogFinish(), cancellationToken);
    }

    public async Task OpenUnavailableDialogAsync(GameplaySession5165 session, NpcReference5165 npc,
        CancellationToken cancellationToken)
    {
        session.BeginNpcInteraction(NpcInteractionKind5165.DeclarativeDialog, npc.LegacyUid);
        await session.SendAsync(GamePacketsAfterAuthentication5165.EncodeNpcDialogText(
            "This NPC is not available yet."), cancellationToken);
        await session.SendAsync(GamePacketsAfterAuthentication5165.EncodeNpcDialogOption(
            "Goodbye.", byte.MaxValue), cancellationToken);
        await session.SendAsync(GamePacketsAfterAuthentication5165.EncodeNpcDialogFace(npc.Face), cancellationToken);
        await session.SendAsync(GamePacketsAfterAuthentication5165.EncodeNpcDialogFinish(), cancellationToken);
    }

    public async Task SelectNpcDialogOptionAsync(GameplaySession5165 session, NpcDialogAnswer5165 answer,
        CancellationToken cancellationToken)
    {
        if (!session.NpcDialogRate.TryAcquire())
            throw new InvalidDataException("The NPC dialog event rate was exceeded.");
        if (answer.Action == 102)
        {
            if (session.ActiveNpc is null)
            {
                if (!session.TryConsumeNpcDialogCloseConfirmation())
                    throw new InvalidDataException("No bounded NPC dialog close is pending.");
                return;
            }
            session.CloseNpcDialog(answer);
            return;
        }
        if (answer.Action != 101)
            throw new InvalidDataException("The NPC dialog action is not recognized.");
        if (answer.Option == byte.MaxValue && session.ActiveNpc is null)
        {
            if (session.TryConsumeNpcDialogCloseReplay(answer)) return;
            throw new InvalidDataException("No bounded NPC dialog replay is pending.");
        }
        if (session.ActiveNpc is not uint active ||
            session.ActiveNpcInteractionKind is not (NpcInteractionKind5165.GuideDialog or NpcInteractionKind5165.DeclarativeDialog) ||
            !references.TryGetNpc(active, out NpcReference5165 npc) ||
            npc.MapId != session.Character.MapId || Chebyshev(npc.X, npc.Y, session.Character.X, session.Character.Y) > ViewRadius)
            throw new InvalidDataException("No bounded NPC dialog is active.");
        if (answer.Option == byte.MaxValue)
        {
            session.CloseNpcDialog(answer);
            return;
        }
        if (answer.Option != 1 || session.ActiveNpcInteractionKind != NpcInteractionKind5165.GuideDialog)
            throw new InvalidDataException("The NPC dialog option is not recognized.");
        await session.SendAsync(GamePacketsAfterAuthentication5165.EncodeNpcDialogText(
            "This reviewed area links Twin City and the Arena. Explore safely."), cancellationToken);
        await session.SendAsync(GamePacketsAfterAuthentication5165.EncodeNpcDialogOption("Close.", byte.MaxValue), cancellationToken);
        await session.SendAsync(GamePacketsAfterAuthentication5165.EncodeNpcDialogFace(npc.Face), cancellationToken);
        await session.SendAsync(GamePacketsAfterAuthentication5165.EncodeNpcDialogFinish(), cancellationToken);
    }

    private static async Task<JumpOutcome5165> RejectJumpAsync(GameplaySession5165 session, CharacterState current,
        uint timestamp, string category, CancellationToken cancellationToken)
    {
        byte[] correction = GamePacketsAfterAuthentication5165.EncodePositionCorrection(
            checked((uint)current.LegacyUid), checked((ushort)current.X), checked((ushort)current.Y), timestamp);
        try { await session.SendAsync(correction, cancellationToken); }
        finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(correction); }
        return new(current, false, category, current.X, current.Y);
    }

    private async Task SynchronizeMovementAsync(GameplaySession5165 source, byte[] packet,
        CancellationToken cancellationToken)
    {
        try
        {
            await source.SendAsync(packet.ToArray(), cancellationToken);
            foreach (GameplaySession5165 peer in sessions.Values)
            {
                if (ReferenceEquals(peer, source)) continue;
                bool wasVisible = source.IsVisible(peer.Character.LegacyUid);
                bool isVisible = AreVisible(source.Character, peer.Character);
                try
                {
                    if (wasVisible && isVisible)
                    {
                        source.MarkVisible(peer.Character.LegacyUid);
                        peer.MarkVisible(source.Character.LegacyUid);
                        await peer.SendAsync(PeerMovementPacket(source, peer, packet), cancellationToken);
                    }
                    else if (!wasVisible && isVisible)
                    {
                        source.MarkVisible(peer.Character.LegacyUid);
                        peer.MarkVisible(source.Character.LegacyUid);
                        await source.SendAsync(Spawn(peer, source, "enter-range"), cancellationToken);
                        await peer.SendAsync(Spawn(source, peer, "enter-range"), cancellationToken);
                    }
                    else if (wasVisible && !isVisible)
                    {
                        source.MarkNotVisible(peer.Character.LegacyUid);
                        peer.MarkNotVisible(source.Character.LegacyUid);
                        // The peer saw the actor at the previous position. Deliver the
                        // authoritative move before removing it from the peer's screen.
                        await peer.SendAsync(PeerMovementPacket(source, peer, packet), cancellationToken);
                        await source.SendAsync(GamePacketsAfterAuthentication5165.EncodeRemoveEntity(
                            PeerVisibleUid(peer, source)), cancellationToken);
                        await peer.SendAsync(GamePacketsAfterAuthentication5165.EncodeRemoveEntity(
                            PeerVisibleUid(source, peer)), cancellationToken);
                    }
                }
                catch (Exception exception) when (IsClosedPeer(exception))
                {
                    sessions.TryRemove(new KeyValuePair<int, GameplaySession5165>(peer.Character.LegacyUid, peer));
                    source.MarkNotVisible(peer.Character.LegacyUid);
                }
            }
        }
        finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(packet); }
    }

    private async Task RemoveOldMapVisibilityAsync(GameplaySession5165 session, CancellationToken cancellationToken)
    {
        foreach (GameplaySession5165 peer in sessions.Values)
        {
            if (ReferenceEquals(peer, session) || !session.IsVisible(peer.Character.LegacyUid)) continue;
            session.MarkNotVisible(peer.Character.LegacyUid);
            peer.MarkNotVisible(session.Character.LegacyUid);
            await peer.SendAsync(GamePacketsAfterAuthentication5165.EncodeRemoveEntity(
                PeerVisibleUid(session, peer)), cancellationToken);
        }
        foreach (uint npc in session.VisibleNpcs())
        {
            session.MarkNpcNotVisible(npc);
            await session.SendAsync(GamePacketsAfterAuthentication5165.EncodeRemoveEntity(npc), cancellationToken);
        }
        session.ResetNpcDialog();
    }

    private async Task AddNewMapVisibilityAsync(GameplaySession5165 session, CancellationToken cancellationToken)
    {
        foreach (GameplaySession5165 peer in sessions.Values)
        {
            if (ReferenceEquals(peer, session) || !AreVisible(session.Character, peer.Character)) continue;
            session.MarkVisible(peer.Character.LegacyUid);
            peer.MarkVisible(session.Character.LegacyUid);
            await session.SendAsync(Spawn(peer, session, "map-entry"), cancellationToken);
            await peer.SendAsync(Spawn(session, peer, "map-entry"), cancellationToken);
        }
    }

    private async Task SynchronizeNpcsAsync(GameplaySession5165 session, CancellationToken cancellationToken)
    {
        var nowVisible = references.Npcs(session.Character.MapId)
            .Where(npc => Chebyshev(npc.X, npc.Y, session.Character.X, session.Character.Y) <= ViewRadius)
            .ToDictionary(npc => npc.LegacyUid);
        foreach (uint existing in session.VisibleNpcs())
        {
            if (nowVisible.ContainsKey(existing)) continue;
            session.MarkNpcNotVisible(existing);
            if (session.ActiveNpc == existing) session.ResetNpcDialog();
            await session.SendAsync(GamePacketsAfterAuthentication5165.EncodeRemoveEntity(existing), cancellationToken);
        }
        foreach (NpcReference5165 npc in nowVisible.Values)
        {
            if (session.IsNpcVisible(npc.LegacyUid)) continue;
            session.MarkNpcVisible(npc.LegacyUid);
            await session.SendAsync(GamePacketsAfterAuthentication5165.EncodeNpcSpawn(npc.LegacyUid,
                checked((ushort)npc.X), checked((ushort)npc.Y), npc.Type, npc.Flags), cancellationToken);
        }
    }

    private IMapValidation5165 RequireMap(int mapId) =>
        maps.TryGet(mapId, out IMapValidation5165 map) ? map :
            throw new InvalidDataException("The authoritative map is not loaded.");

    private static (int X, int Y, bool Corrected) ResolveDestination(IMapValidation5165 map, int x, int y)
    {
        if (map.CanStand(x, y)) return (x, y, false);
        for (int radius = 1; radius <= 4; radius++)
            for (int candidateY = y - radius; candidateY <= y + radius; candidateY++)
                for (int candidateX = x - radius; candidateX <= x + radius; candidateX++)
                    if (Math.Max(Math.Abs(candidateX - x), Math.Abs(candidateY - y)) == radius &&
                        map.CanStand(candidateX, candidateY)) return (candidateX, candidateY, true);
        throw new InvalidDataException("The portal destination has no reviewed accessible correction cell.");
    }

    private static bool IsClosedPeer(Exception exception) =>
        exception is IOException or ObjectDisposedException or System.Net.Sockets.SocketException;

    public async Task RefreshAppearanceAsync(GameplaySession5165 session,
        GamePlayerAppearance5165 appearance, CancellationToken cancellationToken)
    {
        if (!sessions.TryGetValue(session.Character.LegacyUid, out GameplaySession5165? registered) ||
            !ReferenceEquals(registered, session)) return;
        if (!session.TrySetAppearance(appearance)) return;
        foreach (GameplaySession5165 peer in sessions.Values)
        {
            if (ReferenceEquals(peer, session) || !session.IsVisible(peer.Character.LegacyUid) ||
                !AreVisible(session.Character, peer.Character) || !UsesCanaryPresentation(session, peer)) continue;
            try { await peer.SendAsync(Spawn(session, peer, "equipment-refresh"), cancellationToken); }
            catch (Exception exception) when (IsClosedPeer(exception))
            {
                sessions.TryRemove(new KeyValuePair<int, GameplaySession5165>(peer.Character.LegacyUid, peer));
                session.MarkNotVisible(peer.Character.LegacyUid);
            }
        }
    }

    private byte[] Spawn(GameplaySession5165 source, GameplaySession5165 recipient, string reason)
    {
        bool canaryPresentation = UsesCanaryPresentation(source, recipient);
        byte[] packet = GamePacketsAfterAuthentication5165.EncodePlayerSpawn(
            PeerVisibleUid(source, recipient), source.Character.Name, source.Character.Mesh,
            checked((ushort)source.Character.Hair), checked((ushort)source.Character.X),
            checked((ushort)source.Character.Y), checked((byte)source.Character.Direction), 100,
            checked((ushort)source.Character.Level), canaryPresentation ? source.Appearance : default);
        if (canaryPresentation)
        {
            GamePlayerSpawn5165 parsed = GamePacketsAfterAuthentication5165.ParsePlayerSpawnAsExactClient(packet);
            peerSpawnTrace?.Invoke(new(reason, source.Character.LegacyUid, recipient.Character.LegacyUid,
                parsed.Id, parsed.Headwear, parsed.Garment, parsed.Armor, parsed.LeftHand, parsed.RightHand,
                parsed.Steed, parsed.ArmorColor, parsed.LeftHandColor, parsed.HeadwearColor,
                Convert.ToHexString(packet)));
        }
        return packet;
    }

    private static bool UsesCanaryPresentation(GameplaySession5165 source, GameplaySession5165 recipient) =>
        source.PeerAppearanceCanary || recipient.PeerAppearanceCanary;

    private static uint PeerVisibleUid(GameplaySession5165 source, GameplaySession5165 recipient)
    {
        uint immutableUid = checked((uint)source.Character.LegacyUid);
        return UsesCanaryPresentation(source, recipient)
            ? GamePacketsAfterAuthentication5165.ToPlayerPresentationUid(immutableUid)
            : immutableUid;
    }

    private static byte[] PeerMovementPacket(GameplaySession5165 source, GameplaySession5165 recipient,
        ReadOnlySpan<byte> packet)
    {
        byte[] result = packet.ToArray();
        if (!UsesCanaryPresentation(source, recipient)) return result;
        ushort id = BinaryPrimitives.ReadUInt16LittleEndian(result.AsSpan(2));
        int offset = id switch
        {
            GamePacketsAfterAuthentication5165.MovementPacketId => 8,
            GamePacketsAfterAuthentication5165.ActionPacketId => 4,
            _ => throw new InvalidDataException("The peer movement packet identity offset is unsupported."),
        };
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(offset), PeerVisibleUid(source, recipient));
        return result;
    }

    private static bool AreVisible(CharacterState first, CharacterState second) =>
        first.MapId == second.MapId && Chebyshev(first.X, first.Y, second.X, second.Y) <= ViewRadius;

    private static int Chebyshev(int x1, int y1, int x2, int y2) => Math.Max(Math.Abs(x1 - x2), Math.Abs(y1 - y2));

    private static (int X, int Y) DirectionDelta(int direction) => direction switch
    {
        0 => (0, 1), 1 => (-1, 1), 2 => (-1, 0), 3 => (-1, -1),
        4 => (0, -1), 5 => (1, -1), 6 => (1, 0), 7 => (1, 1),
        _ => throw new ArgumentOutOfRangeException(nameof(direction)),
    };
}

public readonly record struct JumpOutcome5165(
    CharacterState Character, bool Accepted, string Category, int DestinationX, int DestinationY);

public sealed record PlayerPeerSpawnTrace5165(
    string Reason,
    int SourceLegacyUid,
    int RecipientLegacyUid,
    uint WireUid,
    uint Headwear,
    uint Garment,
    uint Armor,
    uint LeftHand,
    uint RightHand,
    uint Steed,
    ushort ArmorColor,
    ushort LeftHandColor,
    ushort HeadwearColor,
    string PlaintextHex);

public sealed class GameplaySession5165
{
    private readonly Func<byte[], CancellationToken, Task> sender;
    private readonly HashSet<int> visiblePeers = [];
    private readonly object visibilityGate = new();
    private readonly HashSet<uint> visibleNpcs = [];
    private GamePlayerAppearance5165 appearance;

    public GameplaySession5165(CharacterState character, Func<byte[], CancellationToken, Task> sender,
        BoundedEventRate5165? movementRate = null, BoundedEventRate5165? jumpRate = null,
        BoundedEventRate5165? npcDialogRate = null, GamePlayerAppearance5165 appearance = default,
        bool peerAppearanceCanary = false)
    {
        Character = character; this.sender = sender; this.appearance = appearance;
        PeerAppearanceCanary = peerAppearanceCanary;
        MovementRate = movementRate ?? new(30, TimeSpan.FromSeconds(1));
        JumpRate = jumpRate ?? new(4, TimeSpan.FromSeconds(1));
        NpcDialogRate = npcDialogRate ?? new(12, TimeSpan.FromSeconds(2));
    }

    public CharacterState Character { get; set; }
    public bool PeerAppearanceCanary { get; }
    public GamePlayerAppearance5165 Appearance { get { lock (visibilityGate) return appearance; } }
    public bool TrySetAppearance(GamePlayerAppearance5165 value)
    {
        lock (visibilityGate)
        {
            if (appearance == value) return false;
            appearance = value;
            return true;
        }
    }
    public BoundedEventRate5165 MovementRate { get; }
    public BoundedEventRate5165 JumpRate { get; }
    public BoundedEventRate5165 NpcDialogRate { get; }
    public uint? ActiveNpc { get; private set; }
    public NpcInteractionKind5165 ActiveNpcInteractionKind { get; private set; }
    public int ActiveNpcMapId { get; private set; }
    public int ActiveNpcCharacterUid { get; private set; }
    private NpcDialogAnswer5165? pendingNpcDialogClose;
    public void BeginNpcInteraction(NpcInteractionKind5165 kind, uint legacyUid)
    {
        if (kind == NpcInteractionKind5165.None) throw new ArgumentOutOfRangeException(nameof(kind));
        ActiveNpc = legacyUid;
        ActiveNpcInteractionKind = kind;
        ActiveNpcMapId = Character.MapId;
        ActiveNpcCharacterUid = Character.LegacyUid;
        pendingNpcDialogClose = null;
    }
    public void BeginNpcDialog(uint legacyUid) => BeginNpcInteraction(NpcInteractionKind5165.GuideDialog, legacyUid);
    public bool HasActiveNpcInteraction(NpcInteractionKind5165 kind, uint npcUid) =>
        ActiveNpc == npcUid && ActiveNpcInteractionKind == kind && ActiveNpcMapId == Character.MapId &&
        ActiveNpcCharacterUid == Character.LegacyUid;
    public void CloseNpcDialog(NpcDialogAnswer5165 answer)
    {
        ActiveNpc = null;
        ActiveNpcInteractionKind = NpcInteractionKind5165.None;
        pendingNpcDialogClose = answer;
    }
    public bool TryConsumeNpcDialogCloseReplay(NpcDialogAnswer5165 answer)
    {
        if (pendingNpcDialogClose is not NpcDialogAnswer5165 expected || expected != answer) return false;
        pendingNpcDialogClose = null;
        return true;
    }
    public bool TryConsumeNpcDialogCloseConfirmation()
    {
        if (pendingNpcDialogClose is null) return false;
        pendingNpcDialogClose = null;
        return true;
    }
    public void ResetNpcDialog()
    {
        ActiveNpc = null;
        ActiveNpcInteractionKind = NpcInteractionKind5165.None;
        pendingNpcDialogClose = null;
    }
    public Task SendAsync(byte[] packet, CancellationToken cancellationToken) => sender(packet, cancellationToken);
    public void MarkVisible(int legacyUid) { lock (visibilityGate) visiblePeers.Add(legacyUid); }
    public void MarkNotVisible(int legacyUid) { lock (visibilityGate) visiblePeers.Remove(legacyUid); }
    public bool IsVisible(int legacyUid) { lock (visibilityGate) return visiblePeers.Contains(legacyUid); }
    public void MarkNpcVisible(uint legacyUid) { lock (visibilityGate) visibleNpcs.Add(legacyUid); }
    public void MarkNpcNotVisible(uint legacyUid) { lock (visibilityGate) visibleNpcs.Remove(legacyUid); }
    public bool IsNpcVisible(uint legacyUid) { lock (visibilityGate) return visibleNpcs.Contains(legacyUid); }
    public uint[] VisibleNpcs() { lock (visibilityGate) return visibleNpcs.ToArray(); }
}

public enum NpcInteractionKind5165
{
    None,
    GuideDialog,
    DeclarativeDialog,
    Shop,
    Warehouse,
    ProgressionService,
}

public sealed class BoundedEventRate5165
{
    private readonly int maximum;
    private readonly long windowTicks;
    private readonly Func<long> timestamp;
    private readonly Queue<long> accepted = new();
    private readonly object gate = new();

    public BoundedEventRate5165(int maximum, TimeSpan window, Func<long>? timestamp = null)
    {
        if (maximum < 1 || window <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(maximum));
        this.maximum = maximum;
        windowTicks = checked((long)(window.TotalSeconds * Stopwatch.Frequency));
        this.timestamp = timestamp ?? Stopwatch.GetTimestamp;
    }

    public bool TryAcquire()
    {
        long now = timestamp();
        lock (gate)
        {
            while (accepted.Count > 0 && now - accepted.Peek() >= windowTicks) accepted.Dequeue();
            if (accepted.Count >= maximum) return false;
            accepted.Enqueue(now);
            return true;
        }
    }
}
