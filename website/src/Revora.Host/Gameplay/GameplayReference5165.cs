using Revora.Persistence.PostgreSql;

namespace Revora.Host.Gameplay;

public sealed record PortalReference5165(
    long LegacyId, int SourceMap, int SourceX, int SourceY,
    int DestinationMap, int DestinationX, int DestinationY);

public sealed record NpcReference5165(
    uint LegacyUid, ushort Type, ushort Flags, ushort Face,
    int MapId, int X, int Y);

public sealed class GameplayReferenceCatalog5165
{
    private readonly IReadOnlyDictionary<int, PortalReference5165[]> portals;
    private readonly IReadOnlyDictionary<int, NpcReference5165[]> npcs;
    private readonly IReadOnlyDictionary<uint, NpcReference5165> npcsById;

    public GameplayReferenceCatalog5165(IEnumerable<PortalReference5165>? portals = null,
        IEnumerable<NpcReference5165>? npcs = null)
    {
        PortalReference5165[] portalRows = portals?.ToArray() ?? [];
        NpcReference5165[] npcRows = npcs?.ToArray() ?? [];
        if (portalRows.GroupBy(row => row.LegacyId).Any(group => group.Count() != 1))
            throw new InvalidDataException("A portal legacy ID was duplicated.");
        if (npcRows.GroupBy(row => row.LegacyUid).Any(group => group.Count() != 1))
            throw new InvalidDataException("An NPC legacy UID was duplicated.");
        if (npcRows.Any(row => row.LegacyUid == 0 || row.Type > short.MaxValue || row.Flags > short.MaxValue))
            throw new InvalidDataException("An NPC reference exceeds the exact 5165 field bounds.");
        this.portals = portalRows.GroupBy(row => row.SourceMap)
            .ToDictionary(group => group.Key, group => group.OrderBy(row => row.LegacyId).ToArray());
        this.npcs = npcRows.GroupBy(row => row.MapId)
            .ToDictionary(group => group.Key, group => group.OrderBy(row => row.LegacyUid).ToArray());
        npcsById = npcRows.ToDictionary(row => row.LegacyUid);
    }

    public IReadOnlyList<PortalReference5165> Portals(int mapId) =>
        portals.TryGetValue(mapId, out PortalReference5165[]? rows) ? rows : [];

    public IReadOnlyList<NpcReference5165> Npcs(int mapId) =>
        npcs.TryGetValue(mapId, out NpcReference5165[]? rows) ? rows : [];

    public bool TryGetNpc(uint id, out NpcReference5165 npc) => npcsById.TryGetValue(id, out npc!);
}

public readonly record struct PortalTransition5165(
    CharacterState Character, long PortalId, int DocumentId, int DestinationX, int DestinationY,
    bool DestinationCorrected);
