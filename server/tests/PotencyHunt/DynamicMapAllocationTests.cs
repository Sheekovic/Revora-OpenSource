using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Revora;
using Revora.Game;
using Revora.Database;
using Console = System.Console;

// Standalone process; no database or live listener. Run from bin/Debug.
internal static class DynamicMapAllocationTests
{
    static int checks;
    static readonly MethodInfo Factory = typeof(Map).GetMethod("CreateDynamic", BindingFlags.Static | BindingFlags.NonPublic);
    static Map Create(ushort terrain = 1098, string path = "maps\\1098.map")
    {
        try { return (Map)Factory.Invoke(null, new object[] { terrain, path, false }); }
        catch (TargetInvocationException e) { throw e.InnerException; }
    }
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++; Console.WriteLine("PASS: " + message);
    }
    static void Fails(Action action, string message)
    {
        bool failed = false;
        try { action(); } catch (InvalidOperationException) { failed = true; }
        Check(failed, message);
    }
    static void Main()
    {
        Map.DynamicIDs.Now = 11000;
        var occupied = new Map(11001, 1098, "maps\\1098.map", false);
        DMaps.MapPaths.Add(11002, "reserved-static-map");
        MapsTable.MapInformations.Add(11003, new MapsTable.MapInformation { ID = 11003 });
        var first = Create();
        Check(first.ID == 11004 && ReferenceEquals(Kernel.Maps[11001], occupied),
            "allocation preserves loaded worlds, terrain reservations and metadata reservations");
        Check(first.BaseID == 1098 && first.Floor.Bounds.Width == 56 && first.Floor.Bounds.Height == 56,
            "allocated house terrain has its own runtime identity");
        Map.DynamicIDs.Now = 59999;
        var wrapped = Create();
        Check(wrapped.ID == 11000, "counter wrap uses an available reserved identity");
        Map.DynamicIDs.Now = 59999;
        var afterWrap = Create();
        Check(afterWrap.ID == 11005, "counter wrap skips every occupied and reserved identity");
        var parallel = new Map[32];
        Parallel.For(0, parallel.Length, i => parallel[i] = Create());
        Check(parallel.Select(m => m.ID).Distinct().Count() == parallel.Length
            && parallel.All(m => ReferenceEquals(Kernel.Maps[m.ID], m)),
            "concurrent allocations publish distinct owned maps");
        int count = Kernel.Maps.Count;
        Fails(() => Create(65534, "missing-private-floor"), "missing terrain rejects creation");
        Check(Kernel.Maps.Count == count, "failed terrain creation leaves no partial map");
        var stale = Create();
        Kernel.Maps.Remove(stale.ID);
        var replacement = new Map(stale.ID, 1098, "maps\\1098.map", false);
        stale.Dispose(); stale.Dispose();
        Check(ReferenceEquals(Kernel.Maps[replacement.ID], replacement), "stale disposal cannot remove a replacement world");
        replacement.Dispose(); replacement.Dispose();
        Check(!Kernel.Maps.ContainsKey(replacement.ID), "current owner disposal is idempotent");
        uint before = Map.DynamicIDs.Now;
        Map.DynamicIDs.Now = 65535;
        Fails(() => Create(), "invalid counter cannot truncate into a public map identity");
        Map.DynamicIDs.Now = before;
        for (ushort id = 11000; id < 60000; id++)
            if (!DMaps.MapPaths.ContainsKey(id)) DMaps.MapPaths.Add(id, "exhaustion-fixture");
        count = Kernel.Maps.Count;
        Fails(() => Create(), "full reserved range fails in a bounded scan");
        Check(Kernel.Maps.Count == count, "exhaustion preserves every live instance");
        Parallel.ForEach(parallel, m => m.Dispose());
        Check(parallel.All(m => !Kernel.Maps.ContainsKey(m.ID)), "concurrent disposal removes only owned instances");
        Console.WriteLine("Dynamic map allocation checks passed: " + checks);
    }
}
