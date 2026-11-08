using System;
using System.Threading;
using Revora.Client;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    internal static class TeamArenaFlame
    {
        internal static bool Apply(Entity caster, Entity target, Time32 now, int duration)
        {
            using (var lease = TeamArenaCombatLease.Acquire(caster, target))
            {
                if (lease != null && !lease.Allowed) return false;
                var flags = new[] { FlagsUpdate.Flags.lianhuaran, FlagsUpdate.Flags.lianhuaran02,
                    FlagsUpdate.Flags.lianhuaran03, FlagsUpdate.Flags.lianhuaran04 };
                // Preserve the existing stack progression, including its final
                // flag behavior, while holding ownership across all mutations.
                for (int i = 0; i < flags.Length; i++)
                {
                    ulong flag = (ulong)flags[i];
                    if (!target.ContainsFlag(flag))
                    {
                        if (!Record(caster, target, now)) return false;
                        target.AddFlag(flag, duration, true);
                        return true;
                    }
                    if (i < flags.Length - 1) target.RemoveFlag(flag);
                }
                return true;
            }
        }
        internal static bool Record(Entity caster, Entity target, Time32 now)
        {
            using (var lease = TeamArenaCombatLease.Acquire(caster, target))
            {
                if (lease != null && !lease.Allowed) return false;
                if (lease != null)
                {
                    // The latest application owns the shared stack. Preserve
                    // its flags, but stop the previous percentage timer.
                    target.ArenaBreakingTouch = null; target.lianhuaranLeft = 0;
                }
                target.FlameStamp = now; target.FlameTime = 60;
                target.FlameOwner = caster.Owner; target.FlameMatch = caster.Owner.TeamQualifierGroup;
                return true;
            }
        }
        internal static void Expire(GameState carrier, Time32 now)
        {
            if (carrier == null || carrier.Entity == null || !Monitor.TryEnter(carrier.ItemSyncRoot)) return;
            try
            {
                var body = carrier.Entity;
                // Breaking Touch owns its own countdown; an unrelated/default
                // FlameStamp must not erase it in the character callback.
                if (body.ArenaBreakingTouch != null || now <= body.FlameStamp.AddSeconds(body.FlameTime)) return;
                body.RemoveFlag((ulong)FlagsUpdate.Flags.lianhuaran);
                body.RemoveFlag((ulong)FlagsUpdate.Flags.lianhuaran02);
                body.RemoveFlag((ulong)FlagsUpdate.Flags.lianhuaran03);
                body.RemoveFlag((ulong)FlagsUpdate.Flags.lianhuaran04);
                body.FlameOwner = null; body.FlameMatch = null;
            }
            finally { Monitor.Exit(carrier.ItemSyncRoot); }
        }
        // True prevents the caller from falling through to unowned legacy damage.
        internal static bool Pulse(GameState carrier, GameState target, Time32 now)
        {
            var body = carrier == null ? null : carrier.Entity;
            if (body == null || target == null || target.Entity == null) return false;
            var caster = body.FlameOwner;var match = body.FlameMatch;
            if (match == null && carrier.TeamQualifierGroup == null && target.TeamQualifierGroup == null
                && (caster == null || caster.TeamQualifierGroup == null)) return false;
            if (match == null || caster == null || body.ArenaBreakingTouch != null) return true;
            using (var lease = TeamArenaCombatLease.Acquire(caster.Entity, target.Entity))
            {
                if (lease == null || !lease.Allowed || !Monitor.TryEnter(carrier.ItemSyncRoot)) return true;
                try
                {
                    if (!ReferenceEquals(body.FlameOwner, caster) || !ReferenceEquals(body.FlameMatch, match)
                        || body.ArenaBreakingTouch != null
                        || !ReferenceEquals(caster.TeamQualifierGroup, match) || !ReferenceEquals(carrier.TeamQualifierGroup, match)
                        || !QuestInventory.Player(carrier) || !ReferenceEquals(carrier.Map, target.Map)
                        || !body.ContainsFlag((ulong)FlagsUpdate.Flags.lianhuaran04)
                        || unchecked(now.Value - body.FlameStamp.Value) >= body.FlameTime * 1000
                        || Kernel.GetDistance(body.X, body.Y, target.Entity.X, target.Entity.Y) > 10) return true;
                    if (!match.AllowsCombat(caster, carrier, now)
                        || !Attacking.Handle.CanAttack(caster.Entity, target.Entity, null, false)) return true;
                    uint damage = target.Entity.Hitpoints > 1 ? Math.Min(2000u, target.Entity.Hitpoints - 1) : 0;
                    target.Entity.Hitpoints -= damage;
                    match.UpdateDamage(caster, damage);
                    if (damage > 0) target.Entity.SendSysMesage("The searing flames burn away " + damage + " HP.");
                    return true;
                }
                finally { Monitor.Exit(carrier.ItemSyncRoot); }
            }
        }
    }
}
