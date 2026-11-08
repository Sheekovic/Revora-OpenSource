using System;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    internal static class TeamArenaPoison
    {
        internal sealed class Effect
        {
            internal GameState Caster;
            internal TeamQualifier.QualifierList.QualifierGroup Match;
            internal Map GuildMap;
            internal Time32 GuildUntil;
        }
        internal static bool Apply(Entity caster, Entity target, SpellInformation spell)
        {
            if(GuildPkBanner.InBattle(caster) || GuildPkBanner.InBattle(target)) {
                using(var guild=GuildPkBanner.PlayerCombatLease.Acquire(caster,target)) {
                    if(guild==null || !guild.Allowed || spell==null)return false;
                    var now=Time32.Now;
                    target.ArenaPoison=new Effect{Caster=caster.Owner,GuildMap=caster.Owner.Map,GuildUntil=now.AddSeconds(40)};
                    target.ToxicFogStamp=now;target.ToxicFogLeft=20;target.ToxicFogPercent=spell.PowerPercent;
                    target.AddFlag((ulong)FlagsUpdate.Flags.Poisoned,spell.Duration,true);return true;
                }
            }
            using (var lease = TeamArenaCombatLease.Acquire(caster, target))
            {
                if (lease != null && !lease.Allowed) return false;
                target.ArenaPoison = lease == null ? null : new Effect { Caster = caster.Owner, Match = caster.Owner.TeamQualifierGroup };
                target.ToxicFogStamp = TeamArenaResultCoordinator.MatchClock();
                target.ToxicFogLeft = 20;
                target.ToxicFogPercent = spell.PowerPercent;
                target.AddFlag((ulong)FlagsUpdate.Flags.Poisoned, spell.Duration, true);
                return true;
            }
        }
        private static void Clear(Entity target)
        {
            target.ArenaPoison = null;target.ToxicFogLeft = 0;
            target.RemoveFlag((ulong)FlagsUpdate.Flags.Poisoned);
        }
        private static bool TickGuild(GameState target,Effect effect,Time32 now) {
            var body=target.Entity;
            if(effect==null || effect.GuildMap==null || now>=effect.GuildUntil || body.ToxicFogLeft==0
                || !ReferenceEquals(target.Map,effect.GuildMap) || effect.Caster==null
                || !ReferenceEquals(effect.Caster.Map,effect.GuildMap) || !QuestInventory.Player(effect.Caster)
                || !QuestInventory.Player(target) || !GuildPkBanner.InBattle(body)) {
                if(!SessionInstall.TryEnter(body.UID))return true;bool held=false;
                try {if(held=Monitor.TryEnter(target.ItemSyncRoot))if(ReferenceEquals(body.ArenaPoison,effect))Clear(body);}
                finally{if(held)Monitor.Exit(target.ItemSyncRoot);SessionInstall.Exit(body.UID);}
                return true;
            }
            using(var guild=GuildPkBanner.PlayerCombatLease.Acquire(effect.Caster.Entity,body))
            using(var health=HealthMutationLease.Acquire(effect.Caster.Entity,body)) {
                if(guild==null || !guild.Allowed || !health.Allowed || !ReferenceEquals(body.ArenaPoison,effect))return true;
                if(unchecked(now.Value-body.ToxicFogStamp.Value)<2000)return true;
                body.ToxicFogStamp=now;if(--body.ToxicFogLeft==0){Clear(body);return true;}
                if(body.Hitpoints<=1)return true;
                uint damage=Math.Min(body.Hitpoints-1,Attacking.Calculate.Percent(body,body.ToxicFogPercent));
                if(body.ContainsFlag((ulong)FlagsUpdate.Flags.AzureShield)) {
                    uint absorbed=Math.Min(damage,body.AzureShieldDefence);body.AzureShieldDefence-=absorbed;damage-=absorbed;body.AzureShieldPacket();
                }
                body.Hitpoints-=damage;
                var packet=new SpellUse(true){Attacker=effect.Caster.Entity.UID,SpellID=10010};packet.AddTarget(body.UID,damage,null);target.SendScreen(packet,true);
                return true;
            }
        }
        // True means the Arena path owns this tick, including deferred busy ticks.
        internal static bool Tick(GameState target, Time32 now)
        {
            if (target == null || target.Entity == null) return false;
            var body = target.Entity;var effect = body.ArenaPoison;
            if(effect!=null && effect.GuildMap!=null || GuildPkBanner.InBattle(body))return TickGuild(target,effect,now);
            if (effect == null && target.TeamQualifierGroup == null) return false;
            if (effect == null && body.ToxicFogLeft == 0) return true;
            if (effect == null || !ReferenceEquals(target.TeamQualifierGroup, effect.Match)
                || !ReferenceEquals(effect.Caster.TeamQualifierGroup, effect.Match) || body.ToxicFogLeft == 0)
            {
                if (!Monitor.TryEnter(target.ItemSyncRoot)) return true;
                try { if (ReferenceEquals(body.ArenaPoison, effect)) Clear(body); }
                finally { Monitor.Exit(target.ItemSyncRoot); }
                return true;
            }
            using (var lease = TeamArenaCombatLease.Acquire(effect.Caster.Entity, body))
            {
                if (lease == null || !lease.Allowed)
                {
                    if (lease != null && lease.Busy) return true;
                    if (!Monitor.TryEnter(target.ItemSyncRoot)) return true;
                    try { if (ReferenceEquals(body.ArenaPoison, effect)) Clear(body); }
                    finally { Monitor.Exit(target.ItemSyncRoot); }
                    return true;
                }
                if (!ReferenceEquals(body.ArenaPoison, effect)) return true;
                if (!ReferenceEquals(target.TeamQualifierGroup, effect.Match) || !ReferenceEquals(effect.Caster.TeamQualifierGroup, effect.Match))
                { Clear(body);return true; }
                if (unchecked(now.Value - body.ToxicFogStamp.Value) < 2000) return true;
                body.ToxicFogStamp = now;
                if (--body.ToxicFogLeft == 0) { Clear(body);return true; }
                if (body.Hitpoints <= 1) return true;
                uint damage = Math.Min(body.Hitpoints - 1, Attacking.Calculate.Percent(body, body.ToxicFogPercent));
                if (body.ContainsFlag((ulong)FlagsUpdate.Flags.AzureShield))
                {
                    uint absorbed = Math.Min(damage, body.AzureShieldDefence);
                    body.AzureShieldDefence -= absorbed;damage -= absorbed;
                    body.AzureShieldPacket();
                }
                body.Hitpoints -= damage;
                effect.Match.UpdateDamage(effect.Caster, damage);
                var packet = new SpellUse(true) { Attacker = effect.Caster.Entity.UID, SpellID = 10010 };
                packet.AddTarget(body.UID, damage, null);target.SendScreen(packet, true);
                return true;
            }
        }
    }
}
