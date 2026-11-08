using System;
using System.Collections.Concurrent;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using S = Revora.Game.Features.WarriorEpicStore;
using W = Revora.Game.Features.WarriorEpicScenes;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class WarriorEpicCombat
    {
        internal const uint SteleFamily = 9916401, EvilFamily = 9916402;
        internal sealed class Encounter
        {
            internal W.Scene Scene;
            internal Entity Body;
            internal Guid Id;
            internal S.Stage Stage;
        }
        private static readonly ConcurrentDictionary<ushort, Encounter> actors = new ConcurrentDictionary<ushort, Encounter>();
        internal static bool IsEncounter(Entity body)
        {
            return body != null && body.EntityFlag == EntityFlag.Monster && body.MonsterInfo != null
                && (body.MonsterInfo.ID == SteleFamily || body.MonsterInfo.ID == EvilFamily);
        }
        private static bool Current(Encounter encounter)
        {
            Encounter current; Entity body;
            return encounter != null && W.Owns(encounter.Scene, encounter.Scene.Owner) && W.Clock() < encounter.Scene.Until
                && actors.TryGetValue(encounter.Scene.Map.ID, out current) && ReferenceEquals(current, encounter)
                && encounter.Scene.Map.Entities.TryGetValue(encounter.Body.UID, out body) && ReferenceEquals(body, encounter.Body)
                && ReferenceEquals(body.MonsterInfo.Owner, body) && !body.Dead;
        }
        private static bool Fighter(Encounter encounter, Entity fighter)
        {
            var owner = encounter.Scene.Owner;
            if (fighter == null || fighter.Dead || fighter.MapID != encounter.Scene.Map.ID || !W.Owns(encounter.Scene, owner)) return false;
            if (ReferenceEquals(fighter, owner.Entity)) return true;
            Entity registered;
            return fighter.Companion && ReferenceEquals(fighter.Owner, owner) && ReferenceEquals(owner.Companion, fighter)
                && encounter.Scene.Map.Companions.TryGetValue(fighter.UID, out registered) && ReferenceEquals(registered, fighter);
        }
        internal static bool BlockAttack(Entity attacker, Entity target)
        {
            var body = IsEncounter(attacker) ? attacker : IsEncounter(target) ? target : null;
            if (body == null) return attacker != null && W.Contains(attacker.MapID) || target != null && W.Contains(target.MapID);
            Encounter encounter;
            return !actors.TryGetValue(body.MapID, out encounter) || !ReferenceEquals(encounter.Body, body) || !Current(encounter)
                || !Fighter(encounter, ReferenceEquals(body, attacker) ? target : attacker);
        }
        private sealed class DamageScope : IDisposable
        {
            private readonly object gate;
            internal DamageScope(object gate) { this.gate = gate; }
            public void Dispose() { Monitor.Exit(gate); }
        }
        internal static IDisposable DamageLease(Entity attacker, Entity target)
        {
            var body = IsEncounter(attacker) ? attacker : IsEncounter(target) ? target : null;
            Encounter encounter;
            if (body == null || !actors.TryGetValue(body.MapID, out encounter)) return null;
            var gate = encounter.Scene.Owner.ItemSyncRoot;
            return Monitor.TryEnter(gate) ? new DamageScope(gate) : null;
        }
        internal static bool BlockDamage(Entity attacker, Entity target)
        {
            if (BlockAttack(attacker, target)) return true;
            var body = IsEncounter(attacker) ? attacker : IsEncounter(target) ? target : null;
            Encounter encounter;
            return body != null && (!actors.TryGetValue(body.MapID, out encounter) || !Monitor.IsEntered(encounter.Scene.Owner.ItemSyncRoot));
        }
        internal static Entity Spawn(W.Scene scene, GameState owner)
        {
            if (owner == null || !Monitor.IsEntered(owner.ItemSyncRoot) || !W.Owns(scene, owner) || W.Clock() >= scene.Until)
                throw new InvalidOperationException("Warrior encounter requires its locked scene owner.");
            var stage = S.Load(owner).Stage;
            bool stele = stage == S.Stage.MemoryWitnessed;
            if ((!stele && stage != S.Stage.EvilReleased) || scene.Map.BaseID != (stele ? 10010 : 10011))
                throw new InvalidOperationException("Warrior encounter stage or world changed.");
            Encounter prior;
            if (actors.TryGetValue(scene.Map.ID, out prior))
            {
                if (!Current(prior) || prior.Stage != stage) throw new InvalidOperationException("Warrior encounter changed.");
                return prior.Body;
            }
            var info = new MonsterInformation {
                ID = stele ? SteleFamily : EvilFamily, Mesh = stele ? (ushort)916 : (ushort)917,
                Name = stele ? "SealingStele" : "TinyMindofEvil", Level = stele ? (byte)72 : (byte)70,
                Hitpoints = stele ? 30000u : 50000u, Boss = !stele, Defence = 80, MinAttack = 300, MaxAttack = 450,
                AttackType = 1, AttackRange = 2, ViewRange = 18, AttackSpeed = 1500,
                MoveSpeed = stele ? 1000000000 : 800, RunSpeed = stele ? 1000000000 : 400,
                RespawnTime = 1000000000, BoundX = stele ? (ushort)83 : (ushort)49,
                BoundY = stele ? (ushort)86 : (ushort)47, BoundCX = 1, BoundCY = 1 };
            var body = new Entity(EntityFlag.Monster, false) {
                UID = scene.Map.EntityUIDCounter.Next, MapID = scene.Map.ID, X = info.BoundX, Y = info.BoundY,
                MonsterInfo = info, MapObjType = MapObjectType.Monster, Name = info.Name, Body = info.Mesh,
                Level = info.Level, MinAttack = info.MinAttack, MaxAttack = info.MaxAttack, Defence = info.Defence };
            while (scene.Map.Entities.ContainsKey(body.UID) || scene.Map.Npcs.ContainsKey(body.UID)
                || scene.Map.Companions.ContainsKey(body.UID) || Kernel.GamePool.ContainsKey(body.UID)) body.UID = scene.Map.EntityUIDCounter.Next;
            info.Owner = body; body.MaxHitpoints = body.Hitpoints = info.Hitpoints; body.ApplyMonsterBossHud(); body.SendUpdates = true;
            var encounter = new Encounter { Scene = scene, Body = body, Stage = stage };
            encounter.Id = S.OpenEncounter(owner, stage, () => W.Owns(scene, owner) && W.Clock() < scene.Until);
            if (encounter.Id == Guid.Empty) throw new InvalidOperationException("Warrior encounter receipt was not created.");
            if (!actors.TryAdd(scene.Map.ID, encounter)) throw new InvalidOperationException("Warrior encounter registration collision.");
            try { scene.Map.AddEntity2(body); body.SendSpawn(owner, false); return body; }
            catch { Cancel(scene); throw; }
        }
        internal static void Cancel(W.Scene scene)
        {
            if (!Monitor.IsEntered(scene.Owner.ItemSyncRoot)) throw new InvalidOperationException("Warrior encounter cleanup requires its owner lock.");
            Encounter encounter;
            if (!actors.TryRemove(scene.Map.ID, out encounter)) return;
            scene.Map.RemoveEntity(encounter.Body);
            encounter.Body.MonsterInfo.SendScreen(new Data(true) { UID = encounter.Body.UID, ID = Data.RemoveEntity });
        }
        internal static bool TryDie(Entity body, Entity killer)
        {
            if (!IsEncounter(body)) return false;
            Encounter encounter;
            if (!actors.TryGetValue(body.MapID, out encounter) || !ReferenceEquals(encounter.Body, body)) return true;
            var owner = encounter.Scene.Owner;
            if (!Monitor.TryEnter(owner.ItemSyncRoot)) return true;
            try
            {
                Func<bool> proof = () => Current(encounter) && Fighter(encounter, killer)
                    && Kernel.GetDistance(owner.Entity.X, owner.Entity.Y, body.X, body.Y) <= Constants.pScreenDistance;
                if (!proof() || !S.Defeat(owner, encounter.Stage, encounter.Id, proof)) return true;
                Cancel(encounter.Scene);
                body.DieCore(owner.Entity);
                WarriorEpicJournal.Refresh(owner);
                WarriorEpicQuest.Victory(owner);
            }
            catch (Exception ex) { Q.Fail(owner, "warrior-epic-victory", ex); }
            finally { Monitor.Exit(owner.ItemSyncRoot); }
            return true;
        }
    }
}
