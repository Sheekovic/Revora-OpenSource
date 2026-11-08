using System;
using System.Runtime.CompilerServices;
using Revora.Client;

namespace Revora.Game.Features
{
    // Death display observes this snapshot; only the accepted death path advances it.
    internal static class MonsterKoCounter
    {
        private sealed class Life
        {
            internal bool Recorded;
            internal ushort Count;
        }
        private static readonly ConditionalWeakTable<Entity, Life> lives = new ConditionalWeakTable<Entity, Life>();
        internal static void Revived(Entity monster)
        {
            if (monster == null || monster.EntityFlag != EntityFlag.Monster) return;
            var life = lives.GetOrCreateValue(monster);
            lock (monster.MonsterLifeSyncRoot) { life.Recorded = false; life.Count = 0; XpKillLedger.Revived(monster); GuildContestCombat.Revived(monster); }
        }
        internal static ushort Snapshot(Entity monster)
        {
            Life life;
            if (monster == null || !lives.TryGetValue(monster, out life)) return 0;
            lock (monster.MonsterLifeSyncRoot) return life.Count;
        }
        internal static void AcceptedDeath(Entity monster, Entity killer)
        {
            if (monster == null || monster.EntityFlag != EntityFlag.Monster || monster.MonsterInfo == null || !monster.Dead) return;
            var life = lives.GetOrCreateValue(monster);
            ushort acceptedCount;
            GameState credited;
            lock (monster.MonsterLifeSyncRoot)
            {
                if (!monster.Dead || life.Recorded) return;
                life.Recorded = true;
                GameState p = killer == null ? null : killer.Owner;
                Entity owned;
                if (monster.Companion || monster.MonsterInfo.Guard || monster.MonsterInfo.Reviver
                    || killer == null || killer.EntityFlag != EntityFlag.Player || killer.Companion
                    || !QuestInventory.Player(p) || !ReferenceEquals(p.Entity, killer)
                    || !ReferenceEquals(monster.Killer, killer) || p.Map == null || p.Map.ID != monster.MapID
                    || !p.Map.Entities.TryGetValue(monster.UID, out owned) || !ReferenceEquals(owned, monster)) return;
                life.Count = killer.AdvanceMonsterKo();
                acceptedCount = life.Count;
                credited = p;
            }
            if (acceptedCount == 10 || acceptedCount == 100 || acceptedCount == 300)
                SaveMilestone(credited, acceptedCount);
        }
        internal static void Initialize()
        {
            XpKillLedger.Initialize();
            using (var c = Revora.Database.DataHolder.MySqlConnection)
            {
                c.Open();
                QuestInventory.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_monster_ko_milestones(
uid bigint NOT NULL CHECK(uid>0), threshold integer NOT NULL CHECK(threshold IN(10,100,300)),
receipt uuid NOT NULL UNIQUE, earned_at timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
completed_at timestamptz, PRIMARY KEY(uid,threshold))");
            }
        }
        internal static void Recover(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,uint uid)
        {
            var pending = new System.Collections.Generic.List<Tuple<int,Guid>>();
            using(var q=QuestInventory.Command(c,t,"SELECT threshold,receipt FROM revora_monster_ko_milestones WHERE uid=@uid AND completed_at IS NULL ORDER BY threshold FOR UPDATE","uid",(long)uid))
            using(var r=q.ExecuteReader())while(r.Read())pending.Add(Tuple.Create(r.GetInt32(0),r.GetGuid(1)));
            foreach(var row in pending)
            {
                string key=row.Item2.ToString("N");
                if(row.Item1==10)ProgressionEvidence.Complete(c,t,uid,10520,"monster-ko-v1",key,1);
                else if(row.Item1==100)ProgressionEvidence.Complete(c,t,uid,10521,"monster-ko-v1",key,1);
                else if(row.Item1==300)ProgressionEvidence.Complete(c,t,uid,10522,"monster-ko-v1",key,1);
                else throw new InvalidOperationException("Invalid saved KO threshold.");
                if(QuestInventory.Execute(c,t,"UPDATE revora_monster_ko_milestones SET completed_at=CURRENT_TIMESTAMP WHERE uid=@uid AND threshold=@threshold AND completed_at IS NULL","uid",(long)uid,"threshold",row.Item1)!=1)
                    throw new InvalidOperationException("KO achievement receipt was not consumed.");
            }
        }
        private static void SaveMilestone(GameState p, ushort count)
        {
            // Persist the earned receipt before projection so a failed achievement
            // write can recover on the next full progression read, including login.
            try
            {
                using (var c = Revora.Database.DataHolder.MySqlConnection)
                {
                    c.Open();
                    QuestInventory.Execute(c,null,"INSERT INTO revora_monster_ko_milestones(uid,threshold,receipt) VALUES(@uid,@threshold,@receipt) ON CONFLICT(uid,threshold) DO NOTHING",
                        "uid",(long)p.Entity.UID,"threshold",(int)count,"receipt",Guid.NewGuid());
                    using(var q=QuestInventory.Command(c,null,"SELECT EXISTS(SELECT 1 FROM revora_monster_ko_milestones WHERE uid=@uid AND threshold=@threshold)","uid",(long)p.Entity.UID,"threshold",(int)count))
                        if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("KO threshold receipt was not saved.");
                    using (var t = c.BeginTransaction()) { Recover(c,t,p.Entity.UID);t.Commit(); }
                }
                QuestInventory.Refresh(p);
            }
            catch (Exception ex) { QuestInventory.Fail(p,"monster-ko-milestone",ex); }
        }
    }
}


namespace Revora.Game.Features
{
    // Synchronous direct-XP proof. Delayed effects require their own owned lifetime;
    // never propagate this scope to a timer, task or another attack invocation.
    internal static class XpCastAttribution
    {
        [ThreadStatic] private static Scope current;
        [ThreadStatic] private static Hit hit;
        private sealed class Hit : IDisposable
        {
            internal Hit Previous;
            internal Scope Cast;
            internal Entity Body,Target;
            internal Revora.Database.SpellInformation Spell;
            public void Dispose() { if(!ReferenceEquals(hit,this))throw new InvalidOperationException("XP hit scopes must close in order.");hit=Previous; }
        }
        internal static IDisposable Damage(Entity body,Entity target,Revora.Database.SpellInformation spell)
        {
            var next=new Hit { Previous=hit,Cast=current,Body=body,Target=target,Spell=Direct(body,spell)?spell:null };
            hit=next;return next;
        }
        internal static ushort Accepted(Entity monster,Entity body)
        {
            return hit!=null && ReferenceEquals(hit.Cast,current) && ReferenceEquals(hit.Body,body)
                && ReferenceEquals(hit.Target,monster) && Direct(body,hit.Spell)?hit.Spell.ID:(ushort)0;
        }
        private sealed class Scope : IDisposable
        {
            internal Scope Previous;
            internal Entity Body;
            internal GameState Owner;
            internal long Transition;
            internal int Map;
            internal Revora.Database.SpellInformation Prepared;
            private bool disposed;
            public void Dispose()
            {
                if (disposed) return;
                if (!ReferenceEquals(current,this)) throw new InvalidOperationException("XP cast scopes must close in order.");
                current=Previous;Prepared=null;disposed=true;
            }
        }
        internal static IDisposable Begin(Entity body)
        {
            var scope=new Scope { Previous=current, Body=body, Owner=body==null?null:body.Owner,
                Transition=body==null?0:body.MapTransitionVersion, Map=body==null?0:body.MapID };
            current=scope;return scope;
        }
        private static bool Owned(Scope scope,Entity body)
        {
            return scope!=null && body!=null && ReferenceEquals(scope.Body,body)
                && ReferenceEquals(scope.Owner,body.Owner) && QuestInventory.Player(scope.Owner)
                && ReferenceEquals(scope.Owner.Entity,body) && !body.Dead && !body.Companion
                && !scope.Owner.DisconnectStarted && body.MapID==scope.Map
                && body.MapTransitionVersion==scope.Transition;
        }
        internal static void Prepared(Revora.Database.SpellInformation spell,GameState owner)
        {
            var scope=current;
            // Called before PrepareSpell consumes XP readiness. Only the installed,
            // learned spell in this owned invocation may establish direct-hit proof.
            if (scope==null) return;
            scope.Prepared=null;
            Revora.Interfaces.ISkill learned;
            if (owner==null || !Owned(scope,owner.Entity) || spell==null || spell.NeedXP!=1
                || !owner.Entity.ContainsFlag((ulong)Revora.Network.GamePackets.FlagsUpdate.Flags.XPList)
                || owner.Spells==null || !owner.Spells.TryGetValue(spell.ID,out learned) || !learned.Available
                || learned.Level!=spell.Level
                || !ReferenceEquals(Revora.Database.SpellTable.GetSpell(spell.ID,learned.Level),spell)) return;
            scope.Prepared=spell;
        }
        internal static void Transformed(Entity body)
        {
            var scope=current;
            XpTransformAttribution.Applied(body,scope!=null && Owned(scope,body)?scope.Prepared:null);
        }
        internal static void Applied(Entity target,ulong flag,int seconds)
        {
            var scope=current;
            XpEffectAttribution.Applied(target,flag,seconds,
                scope!=null && Owned(scope,scope.Body)?scope.Prepared:null);
        }
        internal static bool Direct(Entity body,Revora.Database.SpellInformation spell)
        {
            return spell!=null && Owned(current,body) && ReferenceEquals(current.Prepared,spell);
        }
    }
}


namespace Revora.Game.Features
{
    internal static class XpKillLedger
    {
        private sealed class Completion { internal volatile bool Done; }
        private static readonly ConditionalWeakTable<Entity,Completion> completed=new ConditionalWeakTable<Entity,Completion>();
        internal static void Initialize()
        {
            using(var c=Revora.Database.DataHolder.MySqlConnection){c.Open();QuestInventory.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_xp_kills(
life uuid PRIMARY KEY,uid bigint NOT NULL CHECK(uid>0),map integer NOT NULL,
monster bigint NOT NULL,family bigint NOT NULL,spell integer NOT NULL CHECK(spell>0),
earned_at timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP);
CREATE INDEX IF NOT EXISTS revora_xp_kills_owner ON revora_xp_kills(uid)");}
        }
        private sealed class Life { internal readonly Guid Id=Guid.NewGuid();internal bool Attempted; }
        private static readonly ConditionalWeakTable<Entity,Life> lives=new ConditionalWeakTable<Entity,Life>();
        internal static Action AfterCommit;
        internal static void Revived(Entity monster) { lives.Remove(monster); }
        internal static bool Accept(Entity monster,Entity killer)
        {
            if(monster==null || monster.EntityFlag!=EntityFlag.Monster || monster.MonsterInfo==null)return true;
            if(!System.Threading.Monitor.IsEntered(monster.MonsterLifeSyncRoot))throw new InvalidOperationException("XP victory requires monster life ownership.");
            var life=lives.GetOrCreateValue(monster);
            ushort spell=XpCastAttribution.Accepted(monster,killer);
            if(spell==0)spell=XpEffectAttribution.Active(killer);
            if(spell==0)spell=XpTransformAttribution.Active(killer);
            if(spell==0 && !life.Attempted)return true;
            var player=killer==null?null:killer.Owner;Entity owned;
            if(monster.Dead || monster.Companion || monster.MonsterInfo.Guard || monster.MonsterInfo.Reviver
                || killer==null || killer.EntityFlag!=EntityFlag.Player || killer.Companion
                || !QuestInventory.Player(player) || !ReferenceEquals(player.Entity,killer)
                || player.Map==null || player.Map.ID!=monster.MapID || killer.MapID!=monster.MapID
                || !player.Map.Entities.TryGetValue(monster.UID,out owned) || !ReferenceEquals(owned,monster))return !life.Attempted;
            var state=completed.GetOrCreateValue(killer);
            if(state.Done && !life.Attempted)return true;
            life.Attempted=true;
            try
            {
                if(!System.Threading.Monitor.TryEnter(player.ItemSyncRoot))return false;
                try {
                using(var c=Revora.Database.DataHolder.MySqlConnection)
                {
                    c.Open();using(var t=c.BeginTransaction())
                    {
                        Revora.Database.SkillTable.LockOwner(c,t,player);
                        using(var q=QuestInventory.Command(c,t,"SELECT uid FROM revora_xp_kills WHERE life=@life","life",life.Id)) {
                            var prior=q.ExecuteScalar();
                            if(prior!=null && prior!=DBNull.Value)return Convert.ToInt64(prior)==(long)killer.UID;
                        }
                        // A rolled-back decision leaves the monster alive. A later
                        // ordinary finishing hit may kill it, but cannot invent XP credit.
                        if(spell==0){life.Attempted=false;return true;}
                        int count;
                        using(var q=QuestInventory.Command(c,t,"SELECT count(*) FROM revora_xp_kills WHERE uid=@uid","uid",(long)killer.UID))count=Convert.ToInt32(q.ExecuteScalar());
                        if(count<20)
                        {
                            if(QuestInventory.Execute(c,t,@"INSERT INTO revora_xp_kills(life,uid,map,monster,family,spell)
VALUES(@life,@uid,@map,@monster,@family,@spell)",
                                "life",life.Id,"uid",(long)killer.UID,"map",(int)monster.MapID,"monster",(long)monster.UID,
                                "family",(long)monster.MonsterInfo.ID,"spell",(int)spell)!=1)
                                throw new InvalidOperationException("XP kill receipt was not retained.");
                            count++;
                        }
                        t.Commit();var after=AfterCommit;if(after!=null)after();
                        if(count>=20)state.Done=true;return true;
                    }
                }
                } finally { System.Threading.Monitor.Exit(player.ItemSyncRoot); }
            }
            catch(Exception ex)
            {
                // Death has not been installed. Retrying this life resolves an
                // uncertain commit using the retained life identity and owner.
                Console.WriteLine("[quest] source=xp-kill-receipt uid="+killer.UID+" error="+ex.GetType().Name);
                return false;
            }
        }
    }
}


namespace Revora.Game.Features
{
    internal static class XpEffectAttribution
    {
        private sealed class Effect { internal GameState Owner;internal Entity Body;internal ushort Spell;internal StatusFlagsBigVector32.Flag Flag; }
        private sealed class State { internal readonly object Gate=new object();internal readonly System.Collections.Generic.Dictionary<ulong,Effect> Effects=new System.Collections.Generic.Dictionary<ulong,Effect>(); }
        private static readonly ConditionalWeakTable<Entity,State> states=new ConditionalWeakTable<Entity,State>();
        internal static Func<DateTime> Clock=()=>DateTime.Now;
        private static bool Matches(ushort spell,ulong flag)
        {
            var f=(Revora.Network.GamePackets.FlagsUpdate.Flags)flag;
            switch(spell) {
                case 1110:return f==Revora.Network.GamePackets.FlagsUpdate.Flags.Cyclone;
                case 1025:return f==Revora.Network.GamePackets.FlagsUpdate.Flags.Superman;
                case 10390:return f==Revora.Network.GamePackets.FlagsUpdate.Flags.Oblivion;
                case 10309:return f==Revora.Network.GamePackets.FlagsUpdate.Flags.ChainBoltActive;
                case 6011:return f==Revora.Network.GamePackets.FlagsUpdate.Flags.FatalStrike;
                case 1015:return f==Revora.Network.GamePackets.FlagsUpdate.Flags.StarOfAccuracy;
                case 1020:return f==Revora.Network.GamePackets.FlagsUpdate.Flags.MagicShield || f==Revora.Network.GamePackets.FlagsUpdate.Flags.AuraAzul;
                case 8002:return f==Revora.Network.GamePackets.FlagsUpdate.Flags.Fly;
                case 6010:return f==Revora.Network.GamePackets.FlagsUpdate.Flags.ShurikenVortex;
                case 11610:return f==Revora.Network.GamePackets.FlagsUpdate.Flags.BladeFlurry;
                case 11970:return f==Revora.Network.GamePackets.FlagsUpdate.Flags.SuperCyclone;
                case 11050:return f==Revora.Network.GamePackets.FlagsUpdate.Flags.CannonBarrage;
                case 11060:return f==Revora.Network.GamePackets.FlagsUpdate.Flags.BlackbeardsRage;
                case 12290:return f==Revora.Network.GamePackets.FlagsUpdate.Flags.DragonCyclone;
                case 12700:return f==Revora.Network.GamePackets.FlagsUpdate.Flags.ManiacDance;
                case 12860:return f==Revora.Network.GamePackets.FlagsUpdate.Flags.Omnipotence;
                default:return false;
            }
        }
        internal static void Applied(Entity target,ulong flag,int seconds,Revora.Database.SpellInformation spell)
        {
            State state;
            bool valid=target!=null && target.EntityFlag==EntityFlag.Player && !target.Companion
                && QuestInventory.Player(target.Owner) && ReferenceEquals(target.Owner.Entity,target)
                && spell!=null && spell.NeedXP==1 && Matches(spell.ID,flag) && seconds>0 && seconds<=86400
                && target.ContainsFlag(flag);
            if(target==null)return;
            if(!valid){Removed(target,flag);return;}
            StatusFlagsBigVector32.Flag applied;
            if(!target.BitVector.ArrayFlags.TryGetValue((int)flag,out applied)){Removed(target,flag);return;}
            state=states.GetOrCreateValue(target);
            lock(state.Gate)state.Effects[flag]=new Effect{Owner=target.Owner,Body=target,Spell=spell.ID,Flag=applied};
        }
        internal static void Removed(Entity body,ulong flag) { State state;if(body!=null && states.TryGetValue(body,out state))lock(state.Gate)state.Effects.Remove(flag); }
        internal static void Clear(Entity body) { if(body!=null)states.Remove(body); }
        internal static ushort Active(Entity body)
        {
            State state;if(body==null || !states.TryGetValue(body,out state) || !QuestInventory.Player(body.Owner)
                || !ReferenceEquals(body.Owner.Entity,body) || body.Owner.DisconnectStarted || body.Companion)return 0;
            lock(state.Gate) {
                foreach(var pair in state.Effects) {
                    var effect=pair.Value;StatusFlagsBigVector32.Flag applied;
                    if(ReferenceEquals(effect.Body,body) && ReferenceEquals(effect.Owner,body.Owner)
                        && body.ContainsFlag(pair.Key) && body.BitVector.ArrayFlags.TryGetValue((int)pair.Key,out applied)
                        && ReferenceEquals(effect.Flag,applied) && !applied.Expire(Clock()))return effect.Spell;
                }
            }
            return 0;
        }
    }
}


namespace Revora.Game.Features
{
    internal static class XpTransformAttribution
    {
        private sealed class Effect { internal GameState Owner;internal ushort Spell,Form;internal Time32 Stamp; }
        private sealed class State { internal volatile Effect Current; }
        private static readonly ConditionalWeakTable<Entity,State> effects=new ConditionalWeakTable<Entity,State>();
        internal static Func<Time32> Clock=()=>Time32.Now;
        internal static void Clear(Entity body) { State state;if(body!=null && effects.TryGetValue(body,out state))state.Current=null; }
        private static bool Matches(ushort spell,ushort form)
        {
            return spell==1270 && (form==214 || form==274)
                || spell==1280 && (form==213 || form==273)
                || spell==1360 && (form==217 || form==277);
        }
        internal static void Applied(Entity body,Revora.Database.SpellInformation spell)
        {
            Clear(body);
            if(body==null || body.Companion || !QuestInventory.Player(body.Owner)
                || !ReferenceEquals(body.Owner.Entity,body) || spell==null || spell.NeedXP!=1
                || !Matches(spell.ID,body.TransformationID) || body.TransformationTime<=0)return;
            effects.GetOrCreateValue(body).Current=new Effect{Owner=body.Owner,Spell=spell.ID,Form=body.TransformationID,Stamp=body.TransformationStamp};
        }
        internal static ushort Active(Entity body)
        {
            State state;Effect effect;
            if(body==null || !effects.TryGetValue(body,out state) || (effect=state.Current)==null || !QuestInventory.Player(body.Owner)
                || !ReferenceEquals(body.Owner,effect.Owner) || !ReferenceEquals(body.Owner.Entity,body)
                || body.Owner.DisconnectStarted || body.Companion || !body.Transformed
                || body.TransformationID!=effect.Form || body.TransformationStamp!=effect.Stamp
                || body.TransformationTime<=0 || Clock()>=effect.Stamp.AddSeconds(body.TransformationTime))return 0;
            return effect.Spell;
        }
    }
}
