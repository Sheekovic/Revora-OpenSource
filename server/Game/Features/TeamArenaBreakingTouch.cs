using System;
using System.Threading;
using Revora.Client;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    internal static class TeamArenaBreakingTouch
    {
        internal sealed class Effect
        {
            internal GameState Caster;
            internal TeamQualifier.QualifierList.QualifierGroup Match;
        }
        private static bool HasStack(Entity target)
        {
            return target.ContainsFlag((ulong)FlagsUpdate.Flags.lianhuaran)
                || target.ContainsFlag((ulong)FlagsUpdate.Flags.lianhuaran02)
                || target.ContainsFlag((ulong)FlagsUpdate.Flags.lianhuaran03)
                || target.ContainsFlag((ulong)FlagsUpdate.Flags.lianhuaran04);
        }
        internal static bool Record(Entity caster, Entity target)
        {
            using (var lease = TeamArenaCombatLease.Acquire(caster, target))
            {
                if (lease != null && !lease.Allowed) return false;
                // A fourth-stack burst removes every flag. It must not restart a
                // percentage timer using the previous stack's cached percentage.
                if (lease != null && !HasStack(target)) { Clear(target); return true; }
                target.ArenaBreakingTouch = lease == null ? null : new Effect { Caster = caster.Owner, Match = caster.Owner.TeamQualifierGroup };
                if (lease != null)
                {
                    // Shared stack flags now belong to this percentage effect;
                    // an earlier flame must not pulse through the new stack.
                    target.FlameOwner = null; target.FlameMatch = null; target.FlameTime = 0;
                }
                target.lianhuaranStamp = TeamArenaResultCoordinator.MatchClock();
                return true;
            }
        }
        private static void Clear(Entity target)
        {
            target.ArenaBreakingTouch = null;target.lianhuaranLeft = 0;
            target.RemoveFlag((ulong)FlagsUpdate.Flags.lianhuaran);
            target.RemoveFlag((ulong)FlagsUpdate.Flags.lianhuaran02);
            target.RemoveFlag((ulong)FlagsUpdate.Flags.lianhuaran03);
            target.RemoveFlag((ulong)FlagsUpdate.Flags.lianhuaran04);
        }
        // True means the Arena path owns this tick, including deferred busy ticks.
        internal static bool Tick(GameState target, Time32 now)
        {
            if (target == null || target.Entity == null) return false;
            var body = target.Entity;var effect = body.ArenaBreakingTouch;
            if (effect == null && target.TeamQualifierGroup == null) return false;
            if (effect == null && body.lianhuaranLeft == 0) return true;
            if (effect == null || !ReferenceEquals(target.TeamQualifierGroup, effect.Match)
                || !ReferenceEquals(effect.Caster.TeamQualifierGroup, effect.Match) || body.lianhuaranLeft == 0)
            {
                if (!Monitor.TryEnter(target.ItemSyncRoot)) return true;
                try { if (ReferenceEquals(body.ArenaBreakingTouch, effect)) Clear(body); }
                finally { Monitor.Exit(target.ItemSyncRoot); }
                return true;
            }
            using (var lease = TeamArenaCombatLease.Acquire(effect.Caster.Entity, body))
            {
                if (lease == null || !lease.Allowed)
                {
                    if (lease != null && lease.Busy) return true;
                    if (!Monitor.TryEnter(target.ItemSyncRoot)) return true;
                    try { if (ReferenceEquals(body.ArenaBreakingTouch, effect)) Clear(body); }
                    finally { Monitor.Exit(target.ItemSyncRoot); }
                    return true;
                }
                if (!ReferenceEquals(body.ArenaBreakingTouch, effect)) return true;
                if (!HasStack(body)) { Clear(body);return true; }
                if (!ReferenceEquals(target.TeamQualifierGroup, effect.Match) || !ReferenceEquals(effect.Caster.TeamQualifierGroup, effect.Match))
                { Clear(body);return true; }
                if (unchecked(now.Value - body.lianhuaranStamp.Value) < 2000) return true;
                body.lianhuaranStamp = now;
                if (--body.lianhuaranLeft == 0) { Clear(body);return true; }
                if (body.Hitpoints <= 1) return true;
                uint damage = Math.Min(body.Hitpoints - 1, Attacking.Calculate.Percent(body, body.lianhuaranPercent * Math.Max(0f, 1f - body.Detoxication / 100f)));
                if (body.ContainsFlag((ulong)FlagsUpdate.Flags.AzureShield))
                {
                    uint absorbed = Math.Min(damage, body.AzureShieldDefence);
                    body.AzureShieldDefence -= absorbed;damage -= absorbed;
                    body.AzureShieldPacket();
                }
                body.Hitpoints -= damage;
                effect.Match.UpdateDamage(effect.Caster, damage);
                var packet = new SpellUse(true) { Attacker = effect.Caster.Entity.UID, SpellID = 12400 };
                packet.AddTarget(body.UID, damage, null);target.SendScreen(packet, true);
                return true;
            }
        }
    }
}
