using System;
using System.Collections.Concurrent;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using S = Revora.Game.Features.TrojanEpicStore;
using W = Revora.Game.Features.TrojanEpicScenes;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class TrojanEpicCombat
    {
        internal const uint ReaverFamily = 9916801, TraitorFamily = 9916802, DevastatorFamily = 9916803;
        internal sealed class Encounter
        {
            internal W.Scene Scene;
            internal Entity Body;
            internal Guid Id;
            internal S.Stage Stage;
            internal int Revision;
        }
        private static readonly ConcurrentDictionary<ushort, Encounter> actors = new ConcurrentDictionary<ushort, Encounter>();
        internal static void ValidateCatalog()
        {
            uint[] ids = { 7483, 7484, 7487 }; ushort[] meshes = { 979, 981, 978 };
            string[] names = { "GhostReaver", "EvilMonkMisery", "FuriousFlameDevastator" };
            for (int i = 0; i < ids.Length; i++) {
                MonsterInformation info;
                if (!MonsterInformation.MonsterInformations.TryGetValue(ids[i], out info) || info.Mesh != meshes[i] || info.Name != names[i])
                    throw new InvalidOperationException("Trojan encounter catalog changed.");
            }
            foreach (uint id in new[] { ReaverFamily, TraitorFamily, DevastatorFamily })
                if (MonsterInformation.MonsterInformations.ContainsKey(id)) throw new InvalidOperationException("Trojan encounter family is reserved.");
        }
        internal static bool IsEncounter(Entity body)
        {
            return body != null && body.EntityFlag == EntityFlag.Monster && body.MonsterInfo != null
                && (body.MonsterInfo.ID == ReaverFamily || body.MonsterInfo.ID == TraitorFamily || body.MonsterInfo.ID == DevastatorFamily);
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
                throw new InvalidOperationException("Trojan encounter requires its locked scene owner.");
            var progress = S.Load(owner); var stage = progress.Stage;
            bool reaver = stage == S.Stage.Ghosts, traitor = stage == S.Stage.Traitor;
            if ((!reaver && !traitor && stage != S.Stage.Devastator) || scene.Run != progress.Run
                || scene.Part != W.PartFor(stage)) throw new InvalidOperationException("Trojan encounter stage or world changed.");
            ValidateCatalog();
            Encounter prior;
            if (actors.TryGetValue(scene.Map.ID, out prior))
            {
                if (!Current(prior) || prior.Stage != stage || prior.Revision != progress.Revision) throw new InvalidOperationException("Trojan encounter changed.");
                return prior.Body;
            }
            // Explicit Revora combat tuning; these are raw stats, not promised final damage.
            var info = new MonsterInformation {
                ID = reaver ? ReaverFamily : traitor ? TraitorFamily : DevastatorFamily,
                Mesh = reaver ? (ushort)979 : traitor ? (ushort)981 : (ushort)978,
                Name = reaver ? "GhostReaver" : traitor ? "EvilMonkMisery" : "FuriousFlameDevastator", Level = 120,
                Hitpoints = reaver ? 150000u : traitor ? 350000u : 1000000u, Boss = !reaver,
                Defence = 250, MinAttack = reaver ? 800u : traitor ? 1200u : 1800u,
                MaxAttack = reaver ? 1200u : traitor ? 1800u : 2500u,
                AttackType = 1, AttackRange = 2, ViewRange = 18, AttackSpeed = 1500,
                MoveSpeed = 800, RunSpeed = 400, RespawnTime = 1000000000,
                BoundX = W.CombatX, BoundY = W.CombatY, BoundCX = 1, BoundCY = 1 };
            var body = new Entity(EntityFlag.Monster, false) {
                UID = scene.Map.EntityUIDCounter.Next, MapID = scene.Map.ID, X = info.BoundX, Y = info.BoundY,
                MonsterInfo = info, MapObjType = MapObjectType.Monster, Name = info.Name, Body = info.Mesh,
                Level = info.Level, MinAttack = info.MinAttack, MaxAttack = info.MaxAttack, Defence = info.Defence };
            while (scene.Map.Entities.ContainsKey(body.UID) || scene.Map.Npcs.ContainsKey(body.UID)
                || scene.Map.Companions.ContainsKey(body.UID) || Kernel.GamePool.ContainsKey(body.UID)) body.UID = scene.Map.EntityUIDCounter.Next;
            info.Owner = body; body.MaxHitpoints = body.Hitpoints = info.Hitpoints; body.ApplyMonsterBossHud(); body.SendUpdates = true;
            var encounter = new Encounter { Scene = scene, Body = body, Stage = stage, Revision = progress.Revision };
            encounter.Id = S.OpenEncounter(owner, progress.Revision, () => W.Owns(scene, owner) && W.Clock() < scene.Until);
            if (encounter.Id == Guid.Empty) return null; // Energy unavailable or issued Solar Blade absent.
            if (!actors.TryAdd(scene.Map.ID, encounter)) throw new InvalidOperationException("Trojan encounter registration collision.");
            try { scene.Map.AddEntity2(body); body.SendSpawn(owner, false); return body; }
            catch { Cancel(scene); throw; }
        }
        internal static void Cancel(W.Scene scene)
        {
            if (!Monitor.IsEntered(scene.Owner.ItemSyncRoot)) throw new InvalidOperationException("Trojan encounter cleanup requires its owner lock.");
            Encounter encounter;
            if (!actors.TryGetValue(scene.Map.ID, out encounter)) return;
            if (!ReferenceEquals(encounter.Scene, scene)) throw new InvalidOperationException("Trojan encounter cleanup ownership changed.");
            S.RetireEncounter(scene.Owner, encounter.Id);
            if (!actors.TryRemove(scene.Map.ID, out encounter)) throw new InvalidOperationException("Trojan encounter cleanup raced.");
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
                if (!proof() || !S.Defeat(owner, encounter.Revision, encounter.Id, body, killer, proof)) return true;
                Cancel(encounter.Scene);
                body.DieCore(owner.Entity);Q.Refresh(owner);
                TrojanEpicJournal.Refresh(owner);
                var progress = S.Load(owner);
                owner.Send(encounter.Stage == S.Stage.Ghosts
                    ? progress.Stage == S.Stage.Ghosts ? "The shade fades. Return to Mr. Mirror for your next hunt." : "The last shade fades. Report to General Pak."
                    : encounter.Stage == S.Stage.Traitor ? "Monk Misery is defeated. Report to General Pak."
                    : "The Devastator has fallen. Bring the Solar Blade to General Pak.");
            }
            catch (Exception ex) { Q.Fail(owner, "trojan-epic-victory", ex); }
            finally { Monitor.Exit(owner.ItemSyncRoot); }
            return true;
        }
    }
}
