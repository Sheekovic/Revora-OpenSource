using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using S = Revora.Game.Features.TrojanEpicStore;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Owned scene lifecycle. The quest coordinator still controls admission,
    // stage transitions and scheduling; constructing terrain grants no progress.
    internal static class TrojanEpicScenes
    {
        internal const int Capacity = 32;
        internal sealed class Scene
        {
            internal GameState Owner;
            internal Map Map;
            internal S.Stage EntryStage;
            internal int Run, Part;
            internal ushort X, Y;
            internal bool Released;
            internal bool Entered;
            internal DateTime Until;
        }
        private static readonly object gate = new object();
        private static readonly Dictionary<ushort, Scene> scenes = new Dictionary<ushort, Scene>();
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;
        [ThreadStatic] private static Scene transfer;
        internal const ushort TerrainId = 3825, CombatX = 45, CombatY = 40;
        private const string TerrainHash = "459DF7EB9716F1380A06177B0B20ADBC058324421E1FCFDB818416F70CFA6A32";
        internal static int PartFor(S.Stage stage)
        {
            if (stage >= S.Stage.Arrival && stage <= S.Stage.Traitor) return 1;
            if (stage >= S.Stage.Essences && stage <= S.Stage.Infuse) return 2;
            if (stage >= S.Stage.BorrowBlade && stage <= S.Stage.Victory) return 3;
            throw new InvalidOperationException("No Trojan scene at this stage.");
        }
        internal static void ValidateTerrain()
        {
            using (var sha = SHA256.Create()) using (var file = File.OpenRead(Path.Combine(Constants.DMapsPath, "maps", TerrainId + ".map")))
                if (System.BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "") != TerrainHash)
                    throw new InvalidOperationException("Trojan scene terrain changed.");
        }
        private static ushort[] ReturnTile()
        {
            Map home; Revora.Interfaces.INpc trainer;
            if (!Kernel.Maps.TryGetValue(1004, out home) || home.BaseID != 1004 || !home.Npcs.TryGetValue(10022, out trainer))
                throw new InvalidOperationException("Trojan Star return is unavailable.");
            for (int distance = 2; distance <= 4; distance++)
                for (int dx = -distance; dx <= distance; dx++)
                    for (int dy = -distance; dy <= distance; dy++) {
                        int x = trainer.X + dx, y = trainer.Y + dy;
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != distance || x < 0 || y < 0
                            || x >= home.Floor.Bounds.Width || y >= home.Floor.Bounds.Height) continue;
                        if (home.Floor[x, y, MapObjectType.Player]) return new[] { (ushort)x, (ushort)y };
                    }
            throw new InvalidOperationException("Trojan Star has no clear return tile.");
        }
        private static void ValidateRoutes(Scene scene)
        {
            var floor = scene.Map.Floor;
            if (floor.Bounds.Width != 96 || floor.Bounds.Height != 96 || !floor[scene.X, scene.Y, MapObjectType.Player])
                throw new InvalidOperationException("Trojan scene landing changed.");
            var seen = new HashSet<int>(); var queue = new Queue<int>();
            int start = scene.X + scene.Y * 96; seen.Add(start); queue.Enqueue(start);
            while (queue.Count != 0) {
                int tile = queue.Dequeue(), x = tile % 96, y = tile / 96;
                foreach (int next in new[] { tile - 96, tile + 96, tile - 1, tile + 1 }) {
                    int nx = next % 96, ny = next / 96;
                    if (next < 0 || next >= 96 * 96 || Math.Abs(nx - x) + Math.Abs(ny - y) != 1
                        || seen.Contains(next) || !floor[nx, ny, MapObjectType.Player]) continue;
                    seen.Add(next); queue.Enqueue(next);
                }
            }
            if (!seen.Contains(CombatX + CombatY * 96) || scene.Map.Npcs.Values.Any(npc =>
                !seen.Any(tile => Math.Max(Math.Abs(tile % 96 - npc.X), Math.Abs(tile / 96 - npc.Y)) <= 4)))
                throw new InvalidOperationException("Trojan scene actor is unreachable.");
        }
        internal static Scene Find(GameState owner)
        {
            lock (gate) return scenes.Values.FirstOrDefault(scene => !scene.Released && ReferenceEquals(scene.Owner, owner));
        }
        private static void Npc(Map map, uint id, ushort mesh, string name, ushort x, ushort y)
        {
            map.AddNpc(new NpcSpawn { UID = id, Mesh = mesh, Name = name, MapID = map.ID,
                X = x, Y = y, Type = Enums.NpcType.Talker });
        }
        internal static Scene Create(GameState owner, S.Stage stage)
        {
            return Allocate(owner, stage, false);
        }
        internal static Scene TryCreate(GameState owner, S.Stage stage)
        {
            return Allocate(owner, stage, true);
        }
        private static Scene Allocate(GameState owner, S.Stage stage, bool waitForRoom)
        {
            if (owner == null || owner.ItemSyncRoot == null || !Monitor.IsEntered(owner.ItemSyncRoot) || !S.Eligible(owner))
                throw new InvalidOperationException("Trojan scene requires its locked live owner.");
            ushort baseId = TerrainId; int part = PartFor(stage); var progress = S.Load(owner);
            if (progress.Stage != stage) throw new InvalidOperationException("Trojan scene progress changed.");
            ValidateTerrain(); ReturnTile();
            lock (gate)
            {
                if (scenes.Count >= Capacity || scenes.Values.Any(s => s.Owner.Entity.UID == owner.Entity.UID)) {
                    if (waitForRoom) return null;
                    throw new InvalidOperationException("Trojan scene capacity or owner already reserved.");
                }
                ushort id = 0;
                var scene = new Scene { Owner = owner, EntryStage = stage, Run = progress.Run, Part = part,
                    Until = Clock().AddMinutes(30), X = 45, Y = 45 };
                string path = "maps\\" + baseId + ".map";
                try
                {
                    scene.Map = Map.CreateDynamic(baseId, path, false);
                    id = scene.Map.ID;
                    Map registered;
                    if (!Kernel.Maps.TryGetValue(id, out registered) || !ReferenceEquals(registered, scene.Map))
                        throw new InvalidOperationException("Trojan scene registration collided.");
                    if (scene.Map.Entities.Count != 0 || scene.Map.Npcs.Count != 0)
                        throw new InvalidOperationException("Trojan scene must start empty.");
                    scene.Map.Portals.Clear();
                    MapsTable.MapInformations.Add(id, new MapsTable.MapInformation { ID = id, BaseID = baseId, Owner = owner.Entity.UID, Status = 2 });
                    Npc(scene.Map, part == 1 ? 10581u : part == 2 ? 10600u : 10603u, 29216, "GeneralPak", 34, 54);
                    if (part == 1) {
                        Npc(scene.Map, 10583, 29236, "MonkMisery", 54, 34);
                        Npc(scene.Map, 10582, 29226, "Mr.Mirror", 40, 35);
                    }
                    if (part == 2) Npc(scene.Map, 10602, 29256, "DivineJade", 54, 54);
                    ValidateRoutes(scene);
                    if (!S.Eligible(owner)) throw new InvalidOperationException("Trojan scene owner changed.");
                    scenes.Add(id, scene);
                    return scene;
                }
                catch
                {
                    Map registered;
                    if (Kernel.Maps.TryGetValue(id, out registered) &&
                        ReferenceEquals(registered, scene.Map))
                    {
                        MapsTable.MapInformations.Remove(id);
                        registered.Dispose();
                    }
                    throw;
                }
            }
        }
        internal static bool Registered(Scene scene, GameState owner)
        {
            if (scene == null || scene.Released || !ReferenceEquals(scene.Owner, owner) || !S.Eligible(owner) || Clock() >= scene.Until) return false;
            lock (gate)
            {
                Scene current; Map map;
                return scenes.TryGetValue(scene.Map.ID, out current) && ReferenceEquals(current, scene)
                    && Kernel.Maps.TryGetValue(scene.Map.ID, out map) && ReferenceEquals(map, scene.Map);
            }
        }
        internal static bool Owns(Scene scene, GameState owner)
        {
            return Registered(scene, owner) && owner.Entity.MapID == scene.Map.ID && ReferenceEquals(owner.Map, scene.Map);
        }
        internal static bool Contains(ushort map) { lock (gate) return scenes.ContainsKey(map); }
        internal static bool AllowTeleport(GameState owner, ushort destination, ushort x, ushort y)
        {
            lock (gate)
            {
                Scene scene;
                if (!scenes.TryGetValue(destination, out scene)) return true;
                if (!Registered(scene, owner) || Clock() >= scene.Until || !scene.Map.Floor[x, y, MapObjectType.Player]) return false;
                if (ReferenceEquals(transfer, scene)) return true;
                if (!Owns(scene, owner)) return false;
                long dx = (long)x - owner.Entity.X, dy = (long)y - owner.Entity.Y;
                return dx * dx + dy * dy < 441;
            }
        }
        internal static void Enter(Scene scene, GameState owner)
        {
            if (owner == null || !Monitor.IsEntered(owner.ItemSyncRoot) || !Registered(scene, owner)
                || S.Load(owner).Stage != scene.EntryStage || S.Load(owner).Run != scene.Run || Clock() >= scene.Until)
                throw new InvalidOperationException("Trojan entry ownership or progress changed.");
            if (transfer != null) throw new InvalidOperationException("Nested Trojan travel rejected.");
            transfer = scene;
            try
            {
                owner.Entity.Teleport(scene.Map.ID, scene.X, scene.Y);
                if (!Owns(scene, owner)) throw new InvalidOperationException("Trojan travel did not arrive.");
                scene.Entered = true;
                if (!EntityTable.SaveEntity(owner)) throw new InvalidOperationException("Trojan arrival was not saved.");
            }
            finally { transfer = null; }
        }
        internal static void Exit(Scene scene, GameState owner)
        {
            if (owner == null || !Monitor.IsEntered(owner.ItemSyncRoot) || scene == null || scene.Released
                || !ReferenceEquals(scene.Owner, owner) || !Q.Player(owner, false))
                throw new InvalidOperationException("Trojan exit requires its locked live owner.");
            if (owner.Entity.MapID == scene.Map.ID)
            {
                var tile = ReturnTile();
                if (owner.Entity.Dead) owner.Entity.BringToLife();
                owner.Entity.Teleport(1004, tile[0], tile[1]);
                if (owner.Entity.MapID != 1004) throw new InvalidOperationException("Trojan exit did not arrive.");
                if (!EntityTable.SaveEntity(owner)) throw new InvalidOperationException("Trojan return was not saved.");
            }
            Release(scene);
        }
        internal static void Tick()
        {
            Scene[] pending; lock (gate) pending = scenes.Values.ToArray();
            foreach (var scene in pending)
            {
                var owner = scene.Owner;
                lock (owner.ItemSyncRoot) try
                {
                    if (scene.Released) continue;
                    if (!Q.Player(owner, false)) {
                        GameState replacement;
                        if (Kernel.GamePool.TryGetValue(owner.Entity.UID, out replacement) && !ReferenceEquals(replacement, owner)
                            && replacement != null && replacement.Entity != null && replacement.Entity.MapID == scene.Map.ID) {
                            // Reconnect cannot inherit an old connection's private room.
                            // Wait until the new session is loaded before relocating it.
                            if (!Q.Player(replacement, false)) continue;
                            lock (replacement.ItemSyncRoot) {
                                var tile = ReturnTile();
                                if (replacement.Entity.Dead) replacement.Entity.BringToLife();
                                replacement.Entity.Teleport(1004, tile[0], tile[1]);
                                if (replacement.Entity.MapID != 1004 || !EntityTable.SaveEntity(replacement))
                                    throw new InvalidOperationException("Reconnected Trojan return was not saved.");
                            }
                        }
                        Release(scene); continue;
                    }
                    if (scene.Entered && owner.Entity.MapID != scene.Map.ID || Clock() >= scene.Until || owner.Entity.Dead || !S.Eligible(owner)) Exit(scene, owner);
                }
                catch (Exception ex) { Q.Fail(owner, "trojan-scene-recovery", ex); }
            }
        }
        internal static void Release(Scene scene)
        {
            if (scene == null) return;
            lock (scene.Owner.ItemSyncRoot)
            lock (gate)
            {
                if (scene.Released) return;
                Scene current; Map map;
                if (!scenes.TryGetValue(scene.Map.ID, out current) || !ReferenceEquals(current, scene))
                    throw new InvalidOperationException("Trojan scene ownership changed.");
                if (Kernel.GamePool.Values.Any(p => p != null && p.Entity != null && p.Entity.MapID == scene.Map.ID))
                    throw new InvalidOperationException("Evacuate Trojan scene occupants before release.");
                if (!Kernel.Maps.TryGetValue(scene.Map.ID, out map) || !ReferenceEquals(map, scene.Map))
                    throw new InvalidOperationException("Trojan scene map replaced.");
                TrojanEpicCombat.Cancel(scene);
                if (scene.Map.Entities.Count != 0) throw new InvalidOperationException("Unexpected actors remain in Trojan scene.");
                scene.Map.Npcs.Clear();
                MapsTable.MapInformations.Remove(scene.Map.ID);
                scene.Map.Dispose();
                scenes.Remove(scene.Map.ID);
                scene.Released = true;
            }
        }
    }
}
