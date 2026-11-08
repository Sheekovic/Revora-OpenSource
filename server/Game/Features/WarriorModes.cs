using System;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Database;
using Revora.Game.Attacking;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class WarriorModes
    {
        private sealed class BackfireState
        {
            internal readonly object Gate = new object();
            internal bool Armed;
            internal Entity Caster;
            internal Time32 Starts, Expires;
            internal uint Power;
        }
        private static readonly ConditionalWeakTable<GameState, BackfireState> backfires = new ConditionalWeakTable<GameState, BackfireState>();
        private static bool Ready(GameState p, ushort id)
        {
            Interfaces.ISkill learned;
            if (!Q.Player(p) || p.Entity.Class < 20 || p.Entity.Class > 25 || p.Spells == null || !p.Spells.TryGetValue(id,out learned) || !learned.Available) return false;
            var weapons = p.ComputeWeapons();
            return weapons.Item1 != null && weapons.Item2 != null && weapons.Item1.UID != weapons.Item2.UID
                && weapons.Item1.ID / 1000 == 624 && weapons.Item2.ID / 1000 == 624 && weapons.Item1.Durability > 0 && weapons.Item2.Durability > 0;
        }
        internal static void Cast(GameState p, ushort id)
        {
            if (p == null || p.ItemSyncRoot == null || id != 12680 && id != 12700) return;
            lock (p.ItemSyncRoot)
            using (XpCastAttribution.Begin(p.Entity))
            {
                if (!Ready(p,id)) return;
                var e = p.Entity; bool dance = id == 12700;
                if (dance ? e.OnManiacDance() || !e.ContainsFlag((ulong)FlagsUpdate.Flags.XPList) : e.OnBackFire()) return;
                var spell = SpellTable.GetSpell(id,p.Spells[id].Level);
                if (spell == null || !Handle.CanUseSpell(spell,p)) return;
                Handle.PrepareSpell(spell,p);
                if (dance)
                {
                    if (!e.HasLiveKoMode(Time32.Now)) e.KOCount = 0;
                    e.KOSpell = id; e.ManiacDanceTime = 15; e.ManiacDanceStamp = Time32.Now; e.ManiacDancePulseStamp = e.ManiacDanceStamp.AddSeconds(-1);
                    e.AddFlag((ulong)FlagsUpdate.Flags.ManiacDance,15,true); e.RemoveFlag((ulong)FlagsUpdate.Flags.XPList);
                }
                else
                {
                    var state = backfires.GetOrCreateValue(p);
                    lock (state.Gate)
                    {
                        e.BackFireTime = spell.Duration; e.BackFireStamp = Time32.Now;
                        e.AddFlag((ulong)FlagsUpdate.Flags.BackFire,spell.Duration,true);
                        state.Caster = e; state.Starts = e.BackFireStamp; state.Expires = state.Starts.AddSeconds(spell.Duration);
                        state.Power = (uint)spell.Power; state.Armed = true;
                    }
                }
                p.SendScreen(new SpellUse(true){Attacker=e.UID,SpellID=id,SpellLevel=spell.Level,X=e.X,Y=e.Y},true);
            }
        }
        // Called after ReceiveAttack's event protection. Claim only under this
        // session's mode lock, never acquire a second character's inventory lock.
        internal static void MonsterCounter(Entity attacker, Entity defender, uint damage)
        {
            if (defender == null || !defender.OnBackFire()) return;
            using (ElitePkEvent.CombatLease(attacker,defender))
            using (LocalPkWar.CombatLease(attacker,defender))
            using (CouplePkEvent.CombatLease(attacker,defender))
            {
                if (ElitePkEvent.BlockAttack(attacker,defender) || LocalPkWar.BlockAttack(attacker,defender) || CouplePkEvent.BlockAttack(attacker,defender)) return;
                Counter(attacker,defender,new Attack(true){AttackType=Attack.Melee},damage,Time32.Now);
            }
        }
        internal static bool Counter(Entity attacker, Entity defender, Attack incoming, uint incomingDamage, Time32 now)
        {
            if (attacker == null || defender == null || incoming == null || incomingDamage == 0 || ReferenceEquals(attacker,defender)
                || incoming.AttackType == Attack.BackFire || incoming.AttackType == Attack.Reflect || attacker.Dead || attacker.MapID != defender.MapID
                || defender.EntityFlag != EntityFlag.Player || defender.Owner == null) return false;
            if (attacker.EntityFlag == EntityFlag.Player)
            {
                GameState current;
                if (attacker.Owner == null || !ReferenceEquals(attacker.Owner.Entity,attacker)
                    || !Kernel.GamePool.TryGetValue(attacker.UID,out current) || !ReferenceEquals(current,attacker.Owner)) return false;
                if (attacker.Owner.Fake)
                {
                    if (!CityGuardSystem.OwnsCombatBody(attacker) && !LivingWorldSystem.OwnsCombatBody(attacker)) return false;
                }
                else if (!Q.Player(attacker.Owner)) return false;
            }
            else if (attacker.EntityFlag == EntityFlag.Monster)
            {
                Map map; Entity registered;
                if (attacker.MonsterInfo == null || !Kernel.Maps.TryGetValue(attacker.MapID,out map)) return false;
                if (attacker.Companion)
                {
                    if (!OwnedCompanion(attacker,map)) return false;
                }
                else if (!map.Entities.TryGetValue(attacker.UID,out registered) || !ReferenceEquals(registered,attacker)) return false;
            }
            else return false;
            var p = defender.Owner; BackfireState state;
            if (!backfires.TryGetValue(p,out state)) return false;
            uint reflected;
            lock (state.Gate)
            {
                if (!state.Armed || !ReferenceEquals(state.Caster,defender) || !ReferenceEquals(p.Entity,defender)
                    || !Ready(p,12680) || !defender.OnBackFire() || now < state.Starts || now >= state.Expires) return false;
                reflected = (uint)Math.Min(50000UL,(ulong)defender.Hitpoints * state.Power / 100UL);
                state.Armed = false;
                defender.RemoveFlag((ulong)FlagsUpdate.Flags.BackFire); defender.BackFireTime = 0; defender.Backfire = false;
                // Match the existing natural regeneration ceiling, including its
                // blessing bonus. Never reduce stamina that is already above it.
                int cap = defender.HeavenBlessing > 0 ? 160 : 100;
                defender.Stamina = (byte)Math.Max(defender.Stamina,Math.Min(cap,defender.Stamina + 30));
            }
            var response = new Attack(true){Attacker=defender.UID,Attacked=attacker.UID,AttackType=Attack.BackFire,
                Damage=reflected,ResponseDamage=reflected,X=defender.X,Y=defender.Y};
            ElitePkEvent.Damage(defender,attacker,reflected); LocalPkWar.Damage(defender,attacker,reflected); CouplePkEvent.Damage(defender,attacker,reflected);
            if (attacker.EntityFlag == EntityFlag.Player && !attacker.Owner.Fake) p.UpdateQualifier(p,attacker.Owner,Math.Min(reflected,attacker.Hitpoints));
            p.SendScreen(response,true);
            if (reflected >= attacker.Hitpoints) attacker.Die(defender);
            else attacker.Hitpoints -= reflected;
            return true;
        }
        private static bool OwnedCompanion(Entity body, Map map)
        {
            var owner=body.Owner;Entity registered;
            if (!Q.Player(owner) || !ReferenceEquals(owner.Map,map)) return false;
            if (map.Companions.TryGetValue(body.UID,out registered) && ReferenceEquals(registered,body)
                || map.Entities.TryGetValue(body.UID,out registered) && ReferenceEquals(registered,body))
            {
                if (ReferenceEquals(owner.Companion,body)) return true;
                Entity clone;
                if (body.IAmShadow && owner.Entity.MyClones.TryGetValue(body.UID,out clone) && ReferenceEquals(clone,body)) return true;
            }
            // The pet collection is its own authoritative registry; this older
            // summoner path does not insert its bodies in Map.Companions.
            Pet.PetInfo pet;
            return owner.Pet != null && ReferenceEquals(owner.Pet.Owner,owner) && owner.Pet.Pets != null
                && owner.Pet.Pets.TryGetValue(body.pettype,out pet) && pet != null
                && ReferenceEquals(pet.Owner,owner) && ReferenceEquals(pet.Entity,body);
        }
        internal static void DanceTick(GameState p, Time32 now)
        {
            if (p == null || p.ItemSyncRoot == null) return;
            lock (p.ItemSyncRoot)
            {
                if (!Ready(p,12700) || !p.Entity.OnManiacDance() || now >= p.Entity.ManiacDanceStamp.AddSeconds(15)
                    || now < p.Entity.ManiacDancePulseStamp.AddSeconds(1)) return;
                var e = p.Entity; var spell = SpellTable.GetSpell(12700,p.Spells[12700].Level); if (spell == null) return;
                e.ManiacDancePulseStamp = now;
                var use = new SpellUse(true){Attacker=e.UID,SpellID=spell.ID,SpellLevel=spell.Level,X=e.X,Y=e.Y};
                var attack = new Attack(true){Attacker=e.UID};
                foreach (var obj in p.Screen.Objects)
                {
                    var target = obj as Entity;
                    if (target == null || target.MapID != e.MapID || target.MapObjType != MapObjectType.Monster && target.MapObjType != MapObjectType.Player
                        || Kernel.GetDistance(e.X,e.Y,target.X,target.Y) > spell.Range || !Handle.CanAttack(e,target,spell,attack.AttackType == Attack.Melee)) continue;
                    uint damage = Calculate.Melee(e,target,ref attack); use.Effect = attack.Effect;
                    Handle.ReceiveAttack(e,target,attack,ref damage,spell); use.AddTarget(target.UID,damage,attack);
                }
                p.SendScreen(use,true);
            }
        }
        internal static void Clear(GameState p)
        {
            if (!System.Threading.Monitor.IsEntered(p.ItemSyncRoot)) throw new InvalidOperationException("Warrior mode cleanup requires the character lock.");
            var e = p.Entity;
            var state = backfires.GetOrCreateValue(p);
            lock (state.Gate)
            {
                state.Armed = false; state.Caster = null;
                e.RemoveFlag((ulong)FlagsUpdate.Flags.BackFire);
                e.BackFireTime = 0; e.Backfire = false; e.BackFireStamp = e.BackfireStamp = default(Time32);
            }
            bool block = e.BlockShieldCheck || e.IsShieldBlock || e.ContainsFlag((ulong)FlagsUpdate.Flags.ShieldBlock);
            bool magic = e.HasMagicDefender || e.MagicDefender != 0 || e.ContainsFlag((ulong)FlagsUpdate.Flags.MagicDefender);
            foreach (var flag in new[]{FlagsUpdate.Flags.ManiacDance,FlagsUpdate.Flags.ShieldBlock,
                FlagsUpdate.Flags.MagicDefender,FlagsUpdate.Flags.DefensiveStance,FlagsUpdate.Flags.StarOfAccuracy,FlagsUpdate.Flags.MagicShield,FlagsUpdate.Flags.Superman,FlagsUpdate.Flags.AuraAzul})
                if (e.ContainsFlag((ulong)flag)) e.RemoveFlag((ulong)flag);
            e.ManiacDanceTime = 0; e.ManiacDancePacket = null; e.ManiacDanceStamp = e.ManiacDancePulseStamp = default(Time32); e.XPCountTwist = 0;
            if (e.KOSpell == 12700 || e.KOSpell == 1025) { e.KOSpell = 0; e.KOCount = 0; }
            e.BlockShieldCheck = e.IsShieldBlock = false; e.BlockShield = 0; e.ShieldBlockPercent = 0; e.BlockShieldStamp = default(Time32);
            e.MagicDefenderOwner = e.HasMagicDefender = false; e.MagicDefender = 0; e.MagicDefenderSecs = 0; e.MagicDefenderStamp = default(Time32);
            e.IsDefensiveStance = false; e.DefensiveStance = 0; e.DefensiveStanceStamp = default(Time32);
            e.AccuracyTime = e.ShieldTime = e.StarOfAccuracyTime = e.MagicShieldTime = 0; e.SupermanTime = 0;
            e.AccuracyStamp = e.ShieldStamp = e.SupermanStamp = e.StarOfAccuracyStamp = e.MagicShieldStamp = e.WarriorEpicShield = e.XpBlueStamp = default(Time32);
            e.ShieldIncrease = e.MagicShieldIncrease = 0; e.WaveOfBloodStamp = default(Time32); e.Coo = 0;
            foreach (uint index in block && magic ? new uint[]{113,128} : block ? new uint[]{113} : magic ? new uint[]{128} : new uint[0])
            {
                var update = new FlagsUpdate(true){UID=e.UID}; update.Append((byte)FlagsUpdate.DataType.AzureShield,index,0,0,0); p.Send(update.ToArray());
            }
            // Reconcile surviving team protection after retiring the old life's
            // own display, so a later zero-detail packet cannot hide that source.
            MagicDefenderProtection.Cancel(p);
            WarriorStance.Cancel(p);
        }
    }
}
