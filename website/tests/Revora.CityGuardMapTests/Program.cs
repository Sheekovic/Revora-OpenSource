using Revora.Host.Gameplay;

if (args.Length != 2) throw new InvalidOperationException("Pass the reviewed maps root and manifest.");
MapCatalog5165 catalog = MapCatalog5165.LoadReviewedOwnerCatalog(args[0], args[1]);
if (!catalog.TryGet(1002, out IMapValidation5165? loaded) || loaded is not DMapValidation5165 map)
    throw new InvalidOperationException("Reviewed map 1002 is unavailable.");
if (!map.CanStand(456, 378) || !map.CanStand(455, 378))
    throw new InvalidOperationException("Requested or selected Commander tile is blocked.");
var safe = new List<(int X, int Y)>();
for (int radius = 0; radius <= 2; radius++)
for (int y = 378 - radius; y <= 378 + radius; y++)
for (int x = 456 - radius; x <= 456 + radius; x++)
    if (map.CanStand(x, y) && !safe.Contains((x, y))) safe.Add((x, y));
if (safe.Count == 0 || safe[0] != (456, 378)) throw new InvalidOperationException("Safe-tile selection is not deterministic.");
Console.WriteLine($"Revora.CityGuardMapTests: requested=456,378 standable=true selected=455,378 standable=true nearby-safe={safe.Count} map={map.Width}x{map.Height}");
