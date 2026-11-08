using System;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Database;
using Revora.Game.Attacking;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Cast authority belongs to a session. Recipient flags are projections only;
    // they never establish immunity or turn a recipient into another caster.
    internal static class MagicDefenderProtection
    {
        private sealed class State
        {
            internal readonly object Gate = new object();
            internal Entity Body;
            internal bool Active;
            internal Time32 Started, Expires, DisplayExpires, LastRefresh;
            internal ushort Map, X, Y;
            internal byte Level;
            internal Entity DisplaySource;
        }
        private static readonly ConditionalWeakTable<GameState,State> states = new ConditionalWeakTable<GameState,State>();
        private static bool Ready(GameState p)
        {
            Interfaces.ISkill skill;
            if (!Q.Player(p) || !ReferenceEquals(p.Entity.Owner,p) || p.Equipment == null || p.Entity.Class < 20 || p.Entity.Class > 25 || p.Spells == null
                || !p.Spells.TryGetValue(11200,out skill) || !skill.Available || skill.Level > 4) return false;
            var shield = p.ComputeWeapons().Item2;
            return shield != null && shield.ID / 1000 == 900 && shield.Durability > 0;
        }
        internal static void Cast(GameState p)
        {
            if (p == null || p.ItemSyncRoot == null) return;
            lock (p.ItemSyncRoot)
            {
                if (!Ready(p)) return;
                var s = states.GetOrCreateValue(p); var now = Time32.Now;
                lock (s.Gate)
                {
                    if (s.Active && now < s.Expires) return;
                    var spell = SpellTable.GetSpell(11200,p.Spells[11200].Level);
                    if (spell == null || spell.Duration <= 0 || spell.Duration > 60 || !Handle.CanUseSpell(spell,p)) return;
                    Handle.PrepareSpell(spell,p);
                    s.Body = p.Entity; s.Map = p.Entity.MapID; s.X = p.Entity.X; s.Y = p.Entity.Y;
                    s.Started = now; s.Expires = now.AddSeconds(spell.Duration); s.Level = spell.Level; s.Active = true;
                    p.SendScreen(new SpellUse(true){Attacker=p.Entity.UID,SpellID=11200,SpellLevel=spell.Level,X=s.X,Y=s.Y},true);
                }
            }
            RefreshTeam(p,Time32.Now);
        }
        private static bool Member(GameState p, Game.ConquerStructures.Team team)
        {
            GameState registered;
            return team != null && ReferenceEquals(p.Team,team) && team.TryGetMember(p.Entity.UID,out registered) && ReferenceEquals(registered,p);
        }
        private static bool Source(GameState caster, GameState target, Time32 now, out Time32 expires, out byte level)
        {
            expires = default(Time32); level = 0; State s;
            if (caster == null || !states.TryGetValue(caster,out s)) return false;
            bool ready = Ready(caster);
            lock (s.Gate)
            {
                if (!s.Active) return false;
                if (!ready || !ReferenceEquals(s.Body,caster.Entity) || now >= s.Expires
                    || caster.Entity.MapID != s.Map || caster.Entity.X != s.X || caster.Entity.Y != s.Y)
                { s.Active = false; return false; }
                if (now < s.Started || target.Entity.MapID != s.Map || !ReferenceEquals(caster.Map,target.Map)) return false;
                if (!ReferenceEquals(caster,target) && (!Member(caster,target.Team) || !Member(target,target.Team)
                    || Kernel.GetDistance(s.X,s.Y,target.Entity.X,target.Entity.Y) > 4)) return false;
                expires = s.Expires; level = s.Level; return true;
            }
        }
        private static GameState[] Candidates(GameState p)
        {
            return p.Team == null ? new[]{p} : p.Team.Teammates;
        }
        internal static bool Blocks(Entity attacker, Entity target, Time32 now)
        {
            if (attacker == null || target == null || target.EntityFlag != EntityFlag.Player || !Q.Player(target.Owner)
                || !ReferenceEquals(target.Owner.Entity,target) || attacker.MapID != target.MapID) return false;
            Time32 expires; byte level;
            // Check self even if a malformed or changing team omits its owner.
            if (Source(target.Owner,target.Owner,now,out expires,out level) && (long)attacker.BattlePower - target.BattlePower < 20) return true;
            foreach (var caster in Candidates(target.Owner))
                if (Source(caster,target.Owner,now,out expires,out level) && (long)attacker.BattlePower - caster.Entity.BattlePower < 20) return true;
            return false;
        }
        internal static void Refresh(GameState p, Time32 now)
        {
            if (p == null || p.Entity == null || p.Fake) return;
            var s = states.GetOrCreateValue(p); GameState source = null; Time32 expires = default(Time32); byte level = 0;
            lock (s.Gate) if (now < s.LastRefresh || s.Active && now < s.Started) return;
            if (Q.Player(p))
            {
                Time32 candidateEnd; byte candidateLevel;
                if (Source(p,p,now,out expires,out level)) source = p;
                foreach (var caster in Candidates(p))
                    if (Source(caster,p,now,out candidateEnd,out candidateLevel) && (source == null || candidateEnd > expires))
                    { source = caster; expires = candidateEnd; level = candidateLevel; }
            }
            lock (s.Gate)
            {
                if (now < s.LastRefresh || s.Active && now < s.Started) return;
                s.LastRefresh = now;
                var e = p.Entity;
                bool own = s.Active && ReferenceEquals(s.Body,e);
                bool flag = e.ContainsFlag((ulong)FlagsUpdate.Flags.MagicDefender);
                if (source == null)
                {
                    if (flag || e.HasMagicDefender || e.MagicDefenderOwner || e.MagicDefender != 0 || s.DisplaySource != null)
                    {
                        e.RemoveFlag((ulong)FlagsUpdate.Flags.MagicDefender); Detail(p,0,0);
                    }
                    e.HasMagicDefender = e.MagicDefenderOwner = false; e.MagicDefender = e.MagicDefenderSecs = 0;
                    e.MagicDefenderStamp = default(Time32); s.DisplaySource = null; s.DisplayExpires = default(Time32); return;
                }
                int seconds = Math.Max(1,(int)Math.Ceiling((expires-now).AllMilliseconds()/1000.0));
                e.HasMagicDefender = true; e.MagicDefenderOwner = own;
                // Compatibility fields describe the display; Source uses only owned state.
                e.MagicDefenderStamp = now; e.MagicDefender = seconds; e.MagicDefenderSecs = (byte)seconds;
                if (!flag || !ReferenceEquals(s.DisplaySource,source.Entity) || s.DisplayExpires != expires)
                {
                    e.AddFlag((ulong)FlagsUpdate.Flags.MagicDefender,seconds,true); Detail(p,(uint)seconds,level);
                    s.DisplaySource = source.Entity; s.DisplayExpires = expires;
                }
            }
        }
        private static void Detail(GameState p, uint seconds, byte level)
        {
            var packet = new FlagsUpdate(true){UID=p.Entity.UID};
            packet.Append((byte)FlagsUpdate.DataType.AzureShield,128,seconds,0,level); p.Send(packet.ToArray());
        }
        internal static void RefreshTeam(GameState p, Time32 now)
        {
            Refresh(p,now);
            if (p != null && p.Team != null) foreach (var member in p.Team.Teammates) if (!ReferenceEquals(member,p)) Refresh(member,now);
        }
        internal static void Cancel(GameState p)
        {
            State s;
            if (p == null || !states.TryGetValue(p,out s)) return;
            lock (s.Gate) { s.Active = false; }
            RefreshTeam(p,Time32.Now);
        }
        internal static void Moved(Entity body)
        {
            State s;
            if (body == null || body.EntityFlag != EntityFlag.Player || body.Owner == null || !states.TryGetValue(body.Owner,out s)) return;
            bool changed;
            lock (s.Gate) { changed = s.Active && (body.MapID != s.Map || body.X != s.X || body.Y != s.Y); if (changed) s.Active = false; }
            if (changed) RefreshTeam(body.Owner,Time32.Now);
        }
    }
}
