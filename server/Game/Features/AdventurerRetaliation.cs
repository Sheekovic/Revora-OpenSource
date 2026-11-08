using System;
using System.Drawing;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    public static partial class LivingWorldSystem
    {
        private const int RetaliationLeash = 36;
        private static readonly object RetaliationGate = new object();

        private static Wanderer RegisteredAdventurer(Entity body)
        {
            if (body == null) return null;
            lock (Gate)
            {
                Wanderer actor;
                return ByUid.TryGetValue(body.UID, out actor) && actor.State != null
                    && ReferenceEquals(actor.State.Entity, body) && actor.State.Fake ? actor : null;
            }
        }

        private static bool PresentPlayer(Entity player, Entity body)
        {
            GameState session;
            return player != null && player.EntityFlag == EntityFlag.Player && player.Owner != null
                && !player.Owner.Fake && player.Owner.Socket != null && player.Owner.Socket.Alive
                && player.Owner.Attackable && !player.Dead && !body.Dead
                && player.MapID == body.MapID && ReferenceEquals(player.Owner.Map, body.Owner.Map)
                && Kernel.GamePool.TryGetValue(player.UID, out session) && ReferenceEquals(session, player.Owner)
                && !player.Owner.IsWatching();
        }

        internal static bool OwnsCombatBody(Entity body) { return RegisteredAdventurer(body) != null; }

        // Twin City permits deliberate provocation of a registered adventurer, like a guard.
        // Other protected maps (especially the Market) remain protected.
        private static bool RetaliationMap(Entity body)
        {
            return body.MapID == 1002 || !Constants.PKForbiddenMaps.Contains(body.MapID);
        }

        public static bool CheckProvocation(Entity attacker, Entity target, SpellInformation spell, bool melee, out bool allowed)
        {
            allowed = false;
            Wanderer actor = RegisteredAdventurer(target);
            if (actor == null || attacker == null || attacker.Owner == null || attacker.Owner.Fake) return false;
            lock (actor.Sync)
            {
                allowed = target.Owner.Attackable && PresentPlayer(attacker, target) && RetaliationMap(target)
                    && actor.OwnerUid != attacker.UID
                    && (attacker.PKMode == Enums.PKMode.PK || attacker.PKMode == Enums.PKMode.Team)
                    && Time32.Now >= attacker.Owner.CantAttack
                    && Kernel.GetDistance(attacker.X, attacker.Y, target.X, target.Y) <= 18
                    && HasSight(target, attacker.X, attacker.Y)
                    && (!target.ContainsFlag((ulong)FlagsUpdate.Flags.Fly) || (!melee && spell != null && !spell.OnlyGround));
                return true;
            }
        }

        // Called only after the normal attack/skill calculation, before fake characters
        // can enter real-player quest, event, reward or persistence paths.
        public static bool ReceivePlayerHit(Entity attacker, Entity target, SpellInformation spell, ref uint damage)
        {
            Wanderer actor = RegisteredAdventurer(target);
            if (actor == null || attacker == null || attacker.Owner == null || attacker.Owner.Fake) return false;
            lock (actor.Sync)
            {
                bool allowed;
                CheckProvocation(attacker, target, spell, spell == null && attacker.Class != 45, out allowed);
                if (!allowed || damage == 0) { damage = 0; return true; }
                damage = Math.Min(damage, target.Hitpoints);
                if (damage >= target.Hitpoints) { target.Die(attacker); return true; }
                target.Hitpoints -= damage;
                DateTime now = DateTime.UtcNow;
                if (actor.AggressorUid == 0)
                {
                    actor.FightMap = target.MapID; actor.FightX = target.X; actor.FightY = target.Y;
                    actor.NextCombat = now; actor.NextReaction = now; actor.NextEvasion = now;
                    actor.Navigation.Reset();
                }
                // Stay focused on a live aggressor; repeated hits cannot reset the attack cooldown.
                GameState current;
                if (actor.AggressorUid == 0 || !Kernel.GamePool.TryGetValue(actor.AggressorUid, out current)
                    || !PresentPlayer(current.Entity, target)) actor.AggressorUid = attacker.UID;
                if (actor.AggressorUid == attacker.UID) actor.AngerUntil = now.AddSeconds(30);
                if (actor.Sitting) Stand(actor, target);
                return true;
            }
        }

        private static void ClearRetaliation(Wanderer actor)
        {
            actor.AggressorUid = 0;
            actor.AngerUntil = DateTime.MinValue;
            actor.NextReaction = DateTime.MinValue;
        }

        public static void TickRetaliation()
        {
            if (!Ready || !System.Threading.Monitor.TryEnter(RetaliationGate)) return;
            try
            {
                Wanderer[] actors;
                lock (Gate) actors = Actors.ToArray();
                DateTime now = DateTime.UtcNow;
                foreach (var actor in actors)
                {
                    if (!System.Threading.Monitor.TryEnter(actor.Sync)) continue;
                    try { if (actor.AggressorUid != 0) DriveRetaliation(actor, now); }
                    finally { System.Threading.Monitor.Exit(actor.Sync); }
                }
            }
            finally { System.Threading.Monitor.Exit(RetaliationGate); }
        }

        private static void DriveRetaliation(Wanderer actor, DateTime now)
        {
            Entity body = actor.State.Entity;
            GameState targetSession;
            if (now >= actor.AngerUntil || body.MapID != actor.FightMap || !RetaliationMap(body)
                || !Kernel.GamePool.TryGetValue(actor.AggressorUid, out targetSession)
                || !PresentPlayer(targetSession.Entity, body)
                || Kernel.GetDistance(actor.FightX, actor.FightY, targetSession.Entity.X, targetSession.Entity.Y) > RetaliationLeash
                || Kernel.GetDistance(actor.FightX, actor.FightY, body.X, body.Y) > RetaliationLeash)
            { ClearRetaliation(actor); actor.Navigation.Reset(); return; }
            Entity target = targetSession.Entity;
            if (actor.OwnerUid != 0)
            {
                GameState leader;
                if (!Kernel.GamePool.TryGetValue(actor.OwnerUid, out leader) || leader.Entity.MapID != body.MapID
                    || Kernel.GetDistance(leader.Entity.X, leader.Entity.Y, target.X, target.Y) > 16)
                { ClearRetaliation(actor); return; }
            }
            if (now < actor.NextReaction) return;
            actor.NextReaction = now.AddMilliseconds(180);
            int distance = Kernel.GetDistance(body.X, body.Y, target.X, target.Y);
            bool ranged = actor.Class == 45 || actor.Class == 135 || actor.Class == 145;
            if (ranged && distance <= 3 && now >= actor.NextEvasion)
            {
                if (Evade(actor, body, target)) actor.NextEvasion = now.AddMilliseconds(900);
                distance = Kernel.GetDistance(body.X, body.Y, target.X, target.Y);
            }
            if (distance > actor.CombatRange || !HasSight(body, target.X, target.Y))
            {
                // Lead a moving opponent by one tile when approaching; navigation still
                // checks the complete jump/path and never crosses a wall.
                int x = target.X + Math.Sign((int)target.X - target.PX);
                int y = target.Y + Math.Sign((int)target.Y - target.PY);
                if (x <= 0 || y <= 0 || !body.Owner.Map.Floor[x,y,MapObjectType.Player,null])
                { x = target.X; y = target.Y; }
                MoveToward(body, (ushort)x, (ushort)y);
            }
            if ((ulong)body.Hitpoints * 100 < (ulong)body.MaxHitpoints * 35 && TryHeal(actor,body,body,now)) return;
            StrikeAggressor(actor, body, target, now);
        }

        private static bool Evade(Wanderer actor, Entity body, Entity target)
        {
            Point best = new Point(body.X, body.Y);
            int bestScore = 0;
            for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
            {
                int x = body.X + dx * 4, y = body.Y + dy * 4;
                if (x <= 0 || y <= 0 || Kernel.GetDistance(actor.FightX, actor.FightY, (ushort)x, (ushort)y) > RetaliationLeash
                    || !HasSight(body,(ushort)x,(ushort)y)) continue;
                int distance = Kernel.GetDistance((ushort)x,(ushort)y,target.X,target.Y);
                int score = 10 - Math.Abs(6-distance);
                if (distance > 3 && score > bestScore) { bestScore = score; best = new Point(x,y); }
            }
            return bestScore > 0 && WorldActorMotion.JumpClear(body,(ushort)best.X,(ushort)best.Y);
        }

        private static void StrikeAggressor(Wanderer actor, Entity body, Entity target, DateTime now)
        {
            using(var health=HealthMutationLease.Acquire(body,target))
                if(health.Allowed)StrikeAggressorOwned(actor,body,target,now);
        }
        private static void StrikeAggressorOwned(Wanderer actor, Entity body, Entity target, DateTime now)
        {
            if (now < actor.NextCombat || !PresentPlayer(target,body) || !HasSight(body,target.X,target.Y)) return;
            SpellInformation spell = KnownSpell(body,actor.CombatSkill);
            if (actor.CombatSkill != 0 && spell == null) return;
            int range = spell == null ? actor.CombatRange : Math.Min(actor.CombatRange,spell.Distance);
            if (Kernel.GetDistance(body.X,body.Y,target.X,target.Y) > range
                || (target.ContainsFlag((ulong)FlagsUpdate.Flags.Fly) && (actor.CombatRange <= 2 || (spell != null && spell.OnlyGround)))) return;
            if (spell != null && !spell.isPhysical && target.ContainsFlag((ulong)FlagsUpdate.Flags.ImmortalForce)) return;
            if (spell != null && !CanCast(body,spell)) return;
            var attack = new Attack(true){Attacker=body.UID,Attacked=target.UID,X=target.X,Y=target.Y,
                AttackType=actor.Class==45?Attack.Ranged:Attack.Melee};
            uint damage;
            if (spell != null) { SpendSpell(body,spell); damage=Attacking.Calculate.Magic(body,target,spell,ref attack); }
            else if (actor.Class==45) damage=Attacking.Calculate.Ranged(body,target,ref attack);
            else damage=Attacking.Calculate.Melee(body,target,ref attack);
            actor.NextCombat = now.AddMilliseconds(Math.Max(spell == null ? 450 : 600,spell == null ? 0 : spell.ColdTime));
            WarriorModes.Counter(body,target,attack,damage,Time32.Now);
            if (body.MapID != target.MapID) return;
            damage=Math.Min(damage,target.Hitpoints);
            if (spell != null) SendSpell(body,target,spell,damage,attack);
            else {attack.Damage=damage;Broadcast(body,attack);}
            if (damage==0)return;
            if (target.Hitpoints<=damage) {target.Die(body);ClearRetaliation(actor);}
            else target.Hitpoints-=damage;
        }
    }
}
