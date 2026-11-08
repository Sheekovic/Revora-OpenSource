using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace Revora.Host.Gameplay;

public interface IMapValidation5165
{
    int MapId { get; }
    bool CanStand(int x, int y);
    bool CanMove(int fromX, int fromY, int toX, int toY);
}

public interface IMapCatalog5165
{
    bool TryGet(int mapId, out IMapValidation5165 map);
    int DocumentId(int mapId);
}

public sealed class MapCatalog5165 : IMapCatalog5165
{
    private readonly IReadOnlyDictionary<int, IMapValidation5165> maps;
    private readonly IReadOnlyDictionary<int, int> documentIds;

    public MapCatalog5165(IEnumerable<IMapValidation5165> maps, IReadOnlyDictionary<int, int>? documentIds = null)
    {
        ArgumentNullException.ThrowIfNull(maps);
        var reviewed = new Dictionary<int, IMapValidation5165>();
        foreach (IMapValidation5165 map in maps)
            if (!reviewed.TryAdd(map.MapId, map))
                throw new InvalidDataException("A reviewed map ID was duplicated.");
        if (reviewed.Count == 0) throw new InvalidDataException("At least one reviewed map is required.");
        this.maps = reviewed;
        this.documentIds = documentIds ?? reviewed.Keys.ToDictionary(id => id, id => id);
        if (this.documentIds.Keys.Any(id => !reviewed.ContainsKey(id)))
            throw new InvalidDataException("A map document references an unavailable map.");
    }

    public bool TryGet(int mapId, out IMapValidation5165 map) => maps.TryGetValue(mapId, out map!);

    public int DocumentId(int mapId) =>
        maps.ContainsKey(mapId) && documentIds.TryGetValue(mapId, out int documentId)
            ? documentId
            : throw new InvalidDataException("The map document is not reviewed.");

    public static MapCatalog5165 LoadApprovedPortalSlice(string mapsDirectory)
    {
        string root = Path.GetFullPath(mapsDirectory);
        return new MapCatalog5165(
        [
            DMapValidation5165.LoadApprovedMap1002(Path.Combine(root, "newplain.DMap")),
            DMapValidation5165.Load(Path.Combine(root, "forum.DMap"), 1004,
                DMapValidation5165.ApprovedMap1004Sha256, DMapValidation5165.ApprovedMap1004Length),
        ], new Dictionary<int, int> { [1002] = 1002, [1004] = 1004 });
    }

    public static MapCatalog5165 LoadReviewedOwnerCatalog(string mapsDirectory, string manifestPath)
    {
        string root = Path.GetFullPath(mapsDirectory);
        string manifest = Path.GetFullPath(manifestPath);
        if (!File.Exists(manifest) || (File.GetAttributes(manifest) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("The reviewed DMap manifest is unavailable.");
        ReviewedMapManifest? document = JsonSerializer.Deserialize<ReviewedMapManifest>(
            File.ReadAllBytes(manifest), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (document?.Maps is null || document.Maps.Count is < 1 or > 512)
            throw new InvalidDataException("The reviewed DMap manifest has an invalid map count.");
        var loadedPaths = new Dictionary<string, DMapValidation5165>(StringComparer.OrdinalIgnoreCase);
        var maps = new List<IMapValidation5165>(document.Maps.Count);
        var documents = new Dictionary<int, int>();
        foreach (ReviewedMapEntry entry in document.Maps.OrderBy(value => value.MapId))
        {
            if (entry.MapId <= 0 || entry.DocumentId <= 0 || entry.Length <= 0 ||
                entry.Sha256 is null || entry.Sha256.Length != 64 || entry.RelativePath is null)
                throw new InvalidDataException("A reviewed DMap manifest entry is invalid.");
            string normalized = entry.RelativePath.Replace('\\', '/');
            if (normalized.StartsWith('/') || normalized.Split('/').Any(segment => segment is "" or "." or ".."))
                throw new InvalidDataException("A reviewed DMap path is unsafe.");
            string path = Path.GetFullPath(Path.Combine(root, normalized));
            if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new InvalidDataException("A reviewed DMap path escaped its root.");
            if (!loadedPaths.TryGetValue(path, out DMapValidation5165? loaded))
            {
                loaded = DMapValidation5165.Load(path, entry.MapId, entry.Sha256, entry.Length);
                loadedPaths.Add(path, loaded);
            }
            maps.Add(loaded.MapId == entry.MapId ? loaded : loaded.Rebind(entry.MapId));
            if (!documents.TryAdd(entry.MapId, entry.DocumentId))
                throw new InvalidDataException("A reviewed DMap map ID was duplicated.");
        }
        return new MapCatalog5165(maps, documents);
    }

    private sealed record ReviewedMapManifest(IReadOnlyList<ReviewedMapEntry> Maps);
    private sealed record ReviewedMapEntry(int MapId, int DocumentId, string RelativePath,
        long Length, string Sha256);
}

/// <summary>
/// Strict reader for the collision cells used by the approved map-1002 DMap.
/// The file structure and accessibility/elevation rules are adapted from COPS
/// v7 Emulator (CptSky, BSD-4-Clause) at commit
/// 84919e25e79eded39314ea50bfd787427eadd59f. See THIRD_PARTY_NOTICES.md.
/// </summary>
public sealed class DMapValidation5165 : IMapValidation5165
{
    public const string ApprovedMap1002Sha256 = "68a19eefb77933cc82cec52ca83ebc4fff08c9acf5a8852c8744c594e7ffe121";
    public const long ApprovedMap1002Length = 6_577_748;
    public const string ApprovedMap1004Sha256 = "980ab5448e1ca7b2b41ec3e8f90e6edf755dd32bb5acbab50fc49c336a994b5f";
    public const long ApprovedMap1004Length = 60_180;
    private const int HeaderLength = 276;
    private readonly Cell[] cells;
    private readonly int width;
    private readonly int height;

    private readonly record struct Cell(bool Accessible, short Altitude);

    private DMapValidation5165(int mapId, int width, int height, Cell[] cells)
    {
        MapId = mapId;
        this.width = width;
        this.height = height;
        this.cells = cells;
    }

    public int MapId { get; }
    public int Width => width;
    public int Height => height;

    public static DMapValidation5165 LoadApprovedMap1002(string path) =>
        Load(path, 1002, ApprovedMap1002Sha256, ApprovedMap1002Length);

    public static DMapValidation5165 Load(string path, int mapId, string expectedSha256, long expectedLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path)) throw new FileNotFoundException("The reviewed map data is missing.", path);
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Map data cannot be a reparse point.");
        if (expectedLength <= 0 || new FileInfo(path).Length != expectedLength)
            throw new InvalidDataException("The map data length is not approved.");

        byte[] data = File.ReadAllBytes(path);
        try
        {
            string actual = Convert.ToHexStringLower(SHA256.HashData(data));
            if (!CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(actual), Convert.FromHexString(expectedSha256)))
                throw new InvalidDataException("The map data hash is not approved.");
            return Parse(data, mapId);
        }
        finally { CryptographicOperations.ZeroMemory(data); }
    }

    public static DMapValidation5165 Parse(ReadOnlySpan<byte> data, int mapId)
    {
        // The owner-authorized 5165 catalog contains numeric 1003/1004 and
        // "DMAP100\0"/"DMAP101\0" variants with the same bounded cell layout.
        // Exact hash/length validation at Load remains the trust boundary.
        ReadOnlySpan<byte> dmap100 = "DMAP100\0"u8;
        ReadOnlySpan<byte> dmap101 = "DMAP101\0"u8;
        if (data.Length < HeaderLength ||
            (BinaryPrimitives.ReadUInt32LittleEndian(data) is not (1003 or 1004) &&
             !data[..8].SequenceEqual(dmap100) && !data[..8].SequenceEqual(dmap101)))
            throw new InvalidDataException("The DMap header is invalid.");
        // uint32 version + uint32 data + the 260-byte puzzle-file field.
        uint rawWidth = BinaryPrimitives.ReadUInt32LittleEndian(data[268..]);
        uint rawHeight = BinaryPrimitives.ReadUInt32LittleEndian(data[272..]);
        if (rawWidth is 0 or > 4096 || rawHeight is 0 or > 4096 ||
            (ulong)rawWidth * rawHeight > 16_777_216)
            throw new InvalidDataException("The DMap dimensions are invalid.");

        int width = checked((int)rawWidth);
        int height = checked((int)rawHeight);
        var cells = new Cell[checked(width * height)];
        int offset = HeaderLength;
        for (int y = 0; y < height; y++)
        {
            uint checksum = 0;
            for (int x = 0; x < width; x++)
            {
                if (offset > data.Length - 6) throw new InvalidDataException("The DMap cell data is truncated.");
                ushort mask = BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]);
                ushort terrain = BinaryPrimitives.ReadUInt16LittleEndian(data[(offset + 2)..]);
                short altitude = BinaryPrimitives.ReadInt16LittleEndian(data[(offset + 4)..]);
                offset += 6;
                unchecked
                {
                    checksum += (uint)(mask * (terrain + y + 1) + ((altitude + 2) * (x + 1 + terrain)));
                }
                cells[x + y * width] = new(mask != 1, altitude);
            }
            if (offset > data.Length - 4 || BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]) != checksum)
                throw new InvalidDataException("The DMap row checksum is invalid.");
            offset += 4;
        }
        return new(mapId, width, height, cells);
    }

    public bool CanStand(int x, int y) =>
        x >= 0 && y >= 0 && x < width && y < height && cells[x + y * width].Accessible;

    public bool CanMove(int fromX, int fromY, int toX, int toY)
    {
        if (!CanStand(fromX, fromY) || !CanStand(toX, toY)) return false;
        int elevationGain = cells[toX + toY * width].Altitude - cells[fromX + fromY * width].Altitude;
        return elevationGain <= 210;
    }

    internal DMapValidation5165 Rebind(int mapId) => new(mapId, width, height, cells);
}

public sealed class TestMapValidation5165 : IMapValidation5165
{
    private readonly HashSet<(int X, int Y)> blocked;

    public TestMapValidation5165(int mapId = 1002, int width = 972, int height = 972,
        IEnumerable<(int X, int Y)>? blocked = null)
    {
        MapId = mapId; Width = width; Height = height;
        this.blocked = blocked is null ? [] : new(blocked);
    }

    public int MapId { get; }
    public int Width { get; }
    public int Height { get; }
    public bool CanStand(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height && !blocked.Contains((x, y));
    public bool CanMove(int fromX, int fromY, int toX, int toY) => CanStand(fromX, fromY) && CanStand(toX, toY);
}
