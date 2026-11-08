using System;
using System.Collections.Generic;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features {
    internal sealed class PlayerRevival : IDisposable {
        private readonly List<object> gates=new List<object>();
        private readonly GameState caster,target;
        private GuildPkBanner.PlayerCombatLease guild;
        private PlayerRevival(GameState a,GameState b){caster=a;target=b;}
        internal static PlayerRevival Acquire(Entity a,Entity b) {
            if(a==null || b==null || a.EntityFlag!=EntityFlag.Player || b.EntityFlag!=EntityFlag.Player || ReferenceEquals(a,b)
                || a.Companion || b.Companion || a.Owner==null || b.Owner==null)return null;
            var lease=new PlayerRevival(a.Owner,b.Owner);
            try {
                var first=a.UID<b.UID?a.Owner:b.Owner;var second=ReferenceEquals(first,a.Owner)?b.Owner:a.Owner;
                if(!lease.Own(first.ItemSyncRoot) || !lease.Own(second.ItemSyncRoot)
                    || !Q.Player(a.Owner) || !Q.Player(b.Owner,false) || !ReferenceEquals(a.Owner.Entity,a) || !ReferenceEquals(b.Owner.Entity,b)
                    || !b.Dead || a.MapID!=b.MapID || !ReferenceEquals(a.Owner.Map,b.Owner.Map)) {lease.Dispose();return null;}
                if(GuildPkBanner.InBattle(a) || GuildPkBanner.InBattle(b)) {
                    lease.guild=GuildPkBanner.PlayerCombatLease.Acquire(a,b,false,true);
                    if(lease.guild==null || !lease.guild.Allowed){lease.Dispose();return null;}
                }
                return lease;
            }catch{lease.Dispose();throw;}
        }
        // Returns true when Guild PK owns this Lotus target, even if refused.
        internal static bool HandleLotus(Entity caster,Entity target,ushort floorMap) {
            if(target==null || !GuildPkBanner.InBattle(caster) && !GuildPkBanner.InBattle(target))return false;
            if(caster==null || caster.MapID!=floorMap || target.MapID!=floorMap)return true;
            if(!target.Dead) {
                using(var support=GuildPkBanner.PlayerCombatLease.Acquire(caster,target,false,false,true)) {
                    if(support!=null && support.Allowed)target.BringToLifeLotus();
                }
                return true;
            }
            using(var revival=Acquire(caster,target)) {
                if(revival==null || !revival.Begin())return true;
                target.BringToLifeLotus();revival.Completed();return true;
            }
        }
        private bool Own(object gate){if(gate==null || !Monitor.TryEnter(gate))return false;gates.Add(gate);return true;}
        internal static bool Learn(GameState p,ushort id) {
            if(p==null || p.ItemSyncRoot==null || id!=1050 && id!=1100)return false;
            lock(p.ItemSyncRoot)try {
                if(!Q.Near(p,10000,1004) || p.Map.BaseID!=1004 || p.Entity.PokerTable!=0 || !CharacterProgression.Managed(p)
                    || p.Entity.Class<132 || p.Entity.Class>135 || p.Entity.Level<(id==1050?40:70) || p.Spells.ContainsKey(id))return false;
                var skill=new Revora.Network.GamePackets.Spell(true){ID=id,Level=0};var profile=CharacterProgression.Capture(p);
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    SkillTable.LockOwner(c,t,p);CharacterProgression.Persist(c,t,p,profile);SkillTable.Persist(c,t,p,skill);
                    if(!Q.Near(p,10000,1004) || p.Entity.Class!=profile.Values[7] || p.Entity.Level!=profile.Values[0] || p.Spells.ContainsKey(id))
                        throw new InvalidOperationException("Revival training context changed.");t.Commit();
                }}
                CharacterProgression.Committed(p,profile);skill.Available=true;p.Spells.Add(id,skill);skill.Send(p);return true;
            }catch(Exception ex){Q.Fail(p,"revival-training",ex);return false;}
        }
        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_player_revival_credit(
uid bigint PRIMARY KEY CHECK(uid>0),target_uid bigint NOT NULL CHECK(target_uid>0 AND target_uid<>uid),
receipt uuid NOT NULL UNIQUE,earned_at timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,completed_at timestamptz)");}
        }
        internal static void Recover(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,uint uid) {
            Guid key;
            using(var q=Q.Command(c,t,"SELECT receipt FROM revora_player_revival_credit WHERE uid=@uid AND completed_at IS NULL FOR UPDATE","uid",(long)uid)) {
                object value=q.ExecuteScalar();if(value==null)return;key=(Guid)value;
            }
            ProgressionEvidence.Complete(c,t,uid,10902,"player-revival-v1",key.ToString("N"),1);
            if(Q.Execute(c,t,"UPDATE revora_player_revival_credit SET completed_at=CURRENT_TIMESTAMP WHERE uid=@uid AND completed_at IS NULL","uid",(long)uid)!=1)
                throw new InvalidOperationException("Revival credit receipt was not completed.");
        }
        internal bool Begin() {
            if(guild==null)return true;
            try{return GuildPkBanner.CommitRevival(caster.Entity,target.Entity);}
            catch(Exception ex){Console.WriteLine("[guild-pk] revival save error="+ex.GetType().Name);return false;}
        }
        internal void Completed() {
            if(guild!=null)GuildPkBanner.Revived(target.Entity);
            if(!Q.Player(caster) || !Q.Player(target) || target.Entity.Hitpoints==0
                || caster.Entity.MapID!=target.Entity.MapID || !ReferenceEquals(caster.Map,target.Map))return;
            try {
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    SkillTable.LockOwner(c,t,caster);
                    Q.Execute(c,t,"INSERT INTO revora_player_revival_credit(uid,target_uid,receipt) VALUES(@uid,@target,@key) ON CONFLICT(uid) DO NOTHING",
                        "uid",(long)caster.Entity.UID,"target",(long)target.Entity.UID,"key",Guid.NewGuid());
                    using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_player_revival_credit WHERE uid=@uid)","uid",(long)caster.Entity.UID))
                        if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Revival credit receipt was not saved.");
                    t.Commit();}
                    using(var t=c.BeginTransaction()){Recover(c,t,caster.Entity.UID);t.Commit();}}
                Q.Refresh(caster);
            }catch(Exception ex){Q.Fail(caster,"player-revival-credit",ex);}
        }
        public void Dispose(){if(guild!=null){guild.Dispose();guild=null;}for(int n=gates.Count-1;n>=0;n--)Monitor.Exit(gates[n]);gates.Clear();}
    }
}
