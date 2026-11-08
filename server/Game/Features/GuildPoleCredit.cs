using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features {
    internal static class GuildPoleCredit {
        private sealed class State {internal bool Saved;}
        private static readonly ConditionalWeakTable<Entity,State> states=new ConditionalWeakTable<Entity,State>();
        private static bool initialized;
        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_guild_pole_hits(
uid bigint PRIMARY KEY CHECK(uid>0),day varchar(10) NOT NULL REFERENCES revora_guild_battles(day),
guild_id bigint NOT NULL CHECK(guild_id>0),structure bigint NOT NULL CHECK(structure=810),
damage bigint NOT NULL CHECK(damage BETWEEN 1 AND 4294967295),accepted_at timestamptz NOT NULL)");}
            initialized=true;
        }
        internal static bool Known(Entity e){State s;return states.TryGetValue(e,out s) && s.Saved;}
        internal static void Record(NpgsqlConnection c,NpgsqlTransaction t,GameState p,string day,uint damage,DateTime utc) {
            if(!initialized || t==null || !Monitor.IsEntered(p.ItemSyncRoot) || !Q.Player(p) || damage==0)throw new InvalidOperationException("Guild pole acceptance context missing.");
            if(Known(p.Entity))return;
            using(var q=Q.Command(c,t,"SELECT guildid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID)){
                object saved=q.ExecuteScalar();if(saved==null || Convert.ToUInt32(saved)!=p.Entity.GuildID || p.Entity.GuildID==0)throw new InvalidOperationException("Guild pole membership changed.");}
            using(var q=Q.Command(c,t,"SELECT state='active' AND starts_at<=@now AND ends_at>@now FROM revora_guild_battles WHERE day=@day FOR UPDATE","day",day,"now",utc))
                if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Guild pole battle is not active.");
            Q.Execute(c,t,"INSERT INTO revora_guild_pole_hits(uid,day,guild_id,structure,damage,accepted_at) VALUES(@uid,@day,@guild,810,@damage,@now) ON CONFLICT(uid) DO NOTHING",
                "uid",(long)p.Entity.UID,"day",day,"guild",(long)p.Entity.GuildID,"damage",(long)damage,"now",utc);
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_guild_pole_hits WHERE uid=@uid)","uid",(long)p.Entity.UID))
                if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Guild pole hit was not saved.");
            Recover(c,t,p.Entity.UID);
        }
        internal static void Recover(NpgsqlConnection c,NpgsqlTransaction t,uint uid) {
            if(!initialized)return;
            using(var q=Q.Command(c,t,@"SELECT day||':'||guild_id FROM revora_guild_pole_hits WHERE uid=@uid
AND NOT EXISTS(SELECT 1 FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10232)","uid",(long)uid)){
                string receipt=q.ExecuteScalar() as string;if(receipt!=null)ProgressionEvidence.Complete(c,t,uid,10232,"guild-pole-hit-v1",receipt,1);
            }
        }
        internal static void Project(GameState p) {
            if(!Monitor.IsEntered(p.ItemSyncRoot))throw new InvalidOperationException("Guild pole projection owner lock missing.");
            states.GetOrCreateValue(p.Entity).Saved=true;
            if(p.Entity.MyAchievement!=null)Q.Refresh(p);
        }
    }
}
