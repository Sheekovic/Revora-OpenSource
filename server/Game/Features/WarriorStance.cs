using System;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Database;
using Revora.Game.Attacking;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class WarriorStance
    {
        private sealed class State
        {
            internal readonly object Gate = new object();
            internal Entity Body;
            internal bool Active;
            internal ushort Map;
            internal Time32 Started, Expires;
            internal uint Percent;
        }
        private static readonly ConditionalWeakTable<GameState,State> states = new ConditionalWeakTable<GameState,State>();
        private static bool Ready(GameState p)
        {
            Interfaces.ISkill skill;
            if (!Q.Player(p) || !ReferenceEquals(p.Entity.Owner,p) || p.Equipment == null || p.Entity.Class < 20 || p.Entity.Class > 25
                || p.Spells == null || !p.Spells.TryGetValue(11160,out skill) || !skill.Available || skill.Level > 1 || p.Entity.Transformed
                || p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Ride) || p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Fly)) return false;
            var shield = p.ComputeWeapons().Item2;
            return shield != null && shield.ID / 1000 == 900 && shield.Durability > 0;
        }
        private static void Clear(GameState p, State s)
        {
            bool wasActive = s.Active; s.Active = false; s.Body = null;
            if (p.Entity == null) return;
            var e = p.Entity;
            if (wasActive || e.IsDefensiveStance || e.DefensiveStance != 0 || e.ContainsFlag((ulong)FlagsUpdate.Flags.DefensiveStance))
            {
                e.RemoveFlag((ulong)FlagsUpdate.Flags.DefensiveStance); Detail(p,0,0,0);
            }
            e.IsDefensiveStance = false; e.DefensiveStance = 0; e.DefensiveStanceStamp = default(Time32);
        }
        // Revora training rule: accepted activation earns 100 base EXP. The installed
        // next-level requirement gates promotion; lower-level practice may fill the bar.
        private static bool Train(GameState p, SpellInformation information)
        {
            var skill=p.Spells[11160];var next=SpellTable.GetSpell(11160,1);
            if(skill.Level!=0 || information.NeedExperience==0 || next==null)return true;
            uint total=WeaponSkillMastery.Accumulate(skill.Experience,100,information.NeedExperience,
                Config.SpellExperienceRate,Config.SpellExperienceRateMultiplier,p.Entity.Gems[6],
                p.Map.BaseID==1039?Config.TrainingGroundSpellExperienceDivisor:1u,
                p.Entity.VIPLevel>0?Config.VipSpellExperienceMultiplier:1u);
            bool promote=total>=information.NeedExperience && p.Entity.Level>=next.NeedLevel;
            uint experience=promote?0:total;
            if(!promote && experience==skill.Experience)return true;
            skill.Experience=experience;if(promote)skill.Level=1;
            SkillTable.SaveSpells(p);
            if(p.Entity.PersistenceRequiresReload)return false;
            skill.Send(p);return true;
        }
        internal static void Toggle(GameState p)
        {
            if (p == null || p.ItemSyncRoot == null) return;
            lock (p.ItemSyncRoot)
            {
                if (!Q.Player(p)) return;
                var s = states.GetOrCreateValue(p);
                lock (s.Gate)
                {
                    if (s.Active) { Clear(p,s); return; }
                    if (!Ready(p)) return;
                    var spell = SpellTable.GetSpell(11160,p.Spells[11160].Level);
                    if (spell == null || spell.Duration != (spell.Level == 0 ? 120 : 240)
                        || spell.Power % 1000 != (spell.Level == 0 ? 200u : 300u) || !Handle.CanUseSpell(spell,p)) return;
                    if(!Train(p,spell))return;
                    Handle.PrepareSpell(spell,p); var e = p.Entity; var now = Time32.Now;
                    s.Body = e; s.Map = e.MapID; s.Started = now; s.Expires = now.AddSeconds(spell.Duration); s.Percent = spell.Power % 1000; s.Active = true;
                    e.IsDefensiveStance = true; e.DefensiveStance = spell.Duration; e.DefensiveStanceStamp = now;
                    e.AddFlag((ulong)FlagsUpdate.Flags.DefensiveStance,spell.Duration,true); Detail(p,(uint)spell.Duration,s.Percent,spell.Level);
                    p.SendScreen(new SpellUse(true){Attacker=e.UID,SpellID=11160,SpellLevel=spell.Level,X=e.X,Y=e.Y},true);
                }
            }
        }
        private static uint Power(GameState p, Time32 now)
        {
            State s;
            if (p == null || !states.TryGetValue(p,out s)) return 100;
            bool ready = Ready(p);
            lock (s.Gate)
            {
                if (!s.Active || now < s.Started) return 100;
                if (!ready || !ReferenceEquals(s.Body,p.Entity) || p.Entity.MapID != s.Map || now >= s.Expires)
                { Clear(p,s); return 100; }
                return s.Percent;
            }
        }
        internal static bool Active(GameState p) { return Power(p,Time32.Now) > 100; }
        internal static int Defence(Entity body, int value, Time32 now)
        {
            if (body == null || body.EntityFlag != EntityFlag.Player || body.Owner == null || !ReferenceEquals(body.Owner.Entity,body)) return value;
            return (int)Math.Min(int.MaxValue,(long)Math.Max(0,value)*Power(body.Owner,now)/100);
        }
        internal static void Tick(GameState p, Time32 now) { Power(p,now); }
        internal static void Cancel(GameState p)
        {
            State s; if (p == null || !states.TryGetValue(p,out s)) return;
            lock (s.Gate) Clear(p,s);
        }
        internal static bool BlockMovement(GameState p)
        {
            if (!Active(p)) return false;
            // Reuse the existing authoritative position correction projection.
            p.Send(new Data(true){UID=p.Entity.UID,ID=Data.FlashStep,dwParam=p.Entity.MapID,wParam1=p.Entity.X,wParam2=p.Entity.Y});
            return true;
        }
        private static void Detail(GameState p, uint duration, uint percent, byte level)
        {
            var update = new FlagsUpdate(true){UID=p.Entity.UID};
            update.Append((byte)FlagsUpdate.DataType.DefensiveStance,126,duration,percent/100,level); p.Send(update.ToArray());
        }
    }
}
