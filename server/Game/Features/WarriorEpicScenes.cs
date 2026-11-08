using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using S = Revora.Game.Features.WarriorEpicStore;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Owned scene lifecycle. The quest coordinator still controls admission,
    // stage transitions and scheduling; constructing terrain grants no progress.
    internal static class WarriorEpicScenes
    {
        internal const int Capacity = 32;
        internal sealed class Scene
        {
            internal GameState Owner;
            internal Map Map;
            internal S.Stage EntryStage;
            internal ushort X, Y;
            internal bool Released;
            internal bool Entered;
            internal DateTime Until;
        }
        private static readonly object gate = new object();
        private static readonly Dictionary<ushort, Scene> scenes = new Dictionary<ushort, Scene>();
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;
        [ThreadStatic] private static Scene transfer;
        private static readonly string[] hashes = {
            "5E07CF1227E433021BA97F573FD453AAF8DACC2EE88115A42D45F692326818AF",
            "18E8DA52143BDA414A91217348D0236508711745665A797233AB3FA0575C3C62",
            "5964A858CF550804344DA38AA4BE0F70D491696F8BC08BF7661B0160FE2DACC2" };

        internal static ushort BaseFor(S.Stage stage)
        {
            if (stage == S.Stage.JadeIssued || stage == S.Stage.MemoryEntered) return 10009;
            if (stage == S.Stage.MemoryWitnessed) return 10010;
            if (stage >= S.Stage.SteleBroken && stage <= S.Stage.EvilDefeated) return 10011;
            throw new InvalidOperationException("No pending Warrior scene at this stage.");
        }
        private static void Terrain(ushort map)
        {
            using (var sha = SHA256.Create()) using (var file = File.OpenRead(Path.Combine(Constants.DMapsPath, "maps", map + ".map")))
                if (System.BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "") != hashes[map - 10009])
                    throw new InvalidOperationException("Warrior scene terrain changed.");
        }
        internal static void ValidateTerrain() { foreach (ushort map in new ushort[] { 10009, 10010, 10011 }) Terrain(map); }
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
            if (owner == null || owner.ItemSyncRoot == null || !Monitor.IsEntered(owner.ItemSyncRoot) || !S.Eligible(owner))
                throw new InvalidOperationException("Warrior scene requires its locked live owner.");
            ushort baseId = BaseFor(stage);
            if (S.Load(owner).Stage != stage) throw new InvalidOperationException("Warrior scene progress changed.");
            Terrain(baseId);
            lock (gate)
            {
                if (scenes.Count >= Capacity || scenes.Values.Any(s => s.Owner.Entity.UID == owner.Entity.UID))
                    throw new InvalidOperationException("Warrior scene capacity or owner already reserved.");
                ushort id = 0;
                var scene = new Scene { Owner = owner, EntryStage = stage, Until = Clock().AddMinutes(30), X = baseId == 10010 ? (ushort)35 : (ushort)50,
                    Y = baseId == 10010 ? (ushort)45 : (ushort)55 };
                string path = "maps\\" + baseId + ".map";
                try
                {
                    scene.Map = Map.CreateDynamic(baseId, path, false);
                    id = scene.Map.ID;
                    Map registered;
                    if (!Kernel.Maps.TryGetValue(id, out registered) || !ReferenceEquals(registered, scene.Map))
                        throw new InvalidOperationException("Warrior scene registration collided.");
                    if (scene.Map.Entities.Count != 0 || scene.Map.Npcs.Count != 0)
                        throw new InvalidOperationException("Warrior scene must start empty.");
                    scene.Map.Portals.Clear();
                    MapsTable.MapInformations.Add(id, new MapsTable.MapInformation { ID = id, BaseID = baseId, Owner = owner.Entity.UID, Status = 2 });
                    if (baseId == 10009) Npc(scene.Map, 19253, 41310, "Chest", 42, 40);
                    if (baseId == 10010) Npc(scene.Map, 19252, 41320, "GuardingStele", 35, 42);
                    if (baseId == 10011)
                    {
                        Npc(scene.Map, 19254, 41310, "Chest", 41, 39);
                        Npc(scene.Map, 19259, 41360, "Miracle`sSpirit", 39, 44);
                    }
                    // The stele/evil are reserved for attackable actors; no talker
                    // can stand in for their required victory receipts.
                    if (!scene.Map.Floor[scene.X, scene.Y, MapObjectType.Player] || !S.Eligible(owner))
                        throw new InvalidOperationException("Warrior scene landing or owner changed.");
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
            if (scene == null || scene.Released || !ReferenceEquals(scene.Owner, owner) || !S.Eligible(owner)) return false;
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
                || S.Load(owner).Stage != scene.EntryStage || Clock() >= scene.Until)
                throw new InvalidOperationException("Warrior entry ownership or progress changed.");
            if (transfer != null) throw new InvalidOperationException("Nested Warrior travel rejected.");
            transfer = scene;
            try
            {
                owner.Entity.Teleport(scene.Map.ID, scene.X, scene.Y);
                if (!Owns(scene, owner)) throw new InvalidOperationException("Warrior travel did not arrive.");
                scene.Entered = true;
                if (!EntityTable.SaveEntity(owner)) throw new InvalidOperationException("Warrior arrival was not saved.");
            }
            finally { transfer = null; }
        }
        internal static void Exit(Scene scene, GameState owner)
        {
            if (owner == null || !Monitor.IsEntered(owner.ItemSyncRoot) || scene == null || scene.Released
                || !ReferenceEquals(scene.Owner, owner) || !Q.Player(owner, false))
                throw new InvalidOperationException("Warrior exit requires its locked live owner.");
            if (owner.Entity.MapID == scene.Map.ID)
            {
                Map home;
                if (!Kernel.Maps.TryGetValue(1004, out home) || !home.Floor[44, 74, MapObjectType.Player])
                    throw new InvalidOperationException("Warrior instructor return is unavailable.");
                if (owner.Entity.Dead) owner.Entity.BringToLife();
                owner.Entity.Teleport(1004, 44, 74);
                if (owner.Entity.MapID != 1004) throw new InvalidOperationException("Warrior exit did not arrive.");
                if (!EntityTable.SaveEntity(owner)) throw new InvalidOperationException("Warrior return was not saved.");
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
                    if (!Q.Player(owner, false)) { Release(scene); continue; }
                    if (scene.Entered && owner.Entity.MapID != scene.Map.ID || Clock() >= scene.Until || owner.Entity.Dead || !S.Eligible(owner)) Exit(scene, owner);
                }
                catch (Exception ex) { Q.Fail(owner, "warrior-scene-recovery", ex); }
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
                    throw new InvalidOperationException("Warrior scene ownership changed.");
                if (Kernel.GamePool.Values.Any(p => p != null && p.Entity != null && p.Entity.MapID == scene.Map.ID))
                    throw new InvalidOperationException("Evacuate Warrior scene occupants before release.");
                if (!Kernel.Maps.TryGetValue(scene.Map.ID, out map) || !ReferenceEquals(map, scene.Map))
                    throw new InvalidOperationException("Warrior scene map replaced.");
                WarriorEpicCombat.Cancel(scene);
                scene.Map.Npcs.Clear();
                MapsTable.MapInformations.Remove(scene.Map.ID);
                scene.Map.Dispose();
                scenes.Remove(scene.Map.ID);
                scene.Released = true;
            }
        }
    }
}
