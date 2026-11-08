using System;
using System.Collections.Generic;
using Revora.Client;
using Revora.Database;
using Revora.Game.ConquerStructures.Society;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features {
    internal static class PlayerDefeatCredit {
        internal sealed class Snapshot {
            internal Entity Victim,Killer;internal uint Map;internal int[] Kinds;
            internal Guild KillerGuild,VictimGuild;
        }
        internal static Snapshot Capture(Entity victim,Entity killer){
            if(victim==null || killer==null || victim==killer || victim.EntityFlag!=EntityFlag.Player || killer.EntityFlag!=EntityFlag.Player
                || victim.Companion || killer.Companion || !Q.Player(victim.Owner) || !Q.Player(killer.Owner)
                || !ReferenceEquals(victim.Owner.Entity,victim) || !ReferenceEquals(killer.Owner.Entity,killer)
                || victim.MapID!=killer.MapID || !ReferenceEquals(victim.Owner.Map,killer.Owner.Map))return null;
            var kinds=new List<int>();var now=DateTime.Now;
            if(Active(victim,FlagsUpdate.Flags.FlashingName,now))kinds.Add(10605);
            if(Active(victim,FlagsUpdate.Flags.RedName,now))kinds.Add(10606);
            if(Active(victim,FlagsUpdate.Flags.BlackName,now))kinds.Add(10607);
            if(killer.Owner.Enemy!=null && killer.Owner.Enemy.ContainsKey(victim.UID))kinds.Add(10611);
            var s=new Snapshot{Victim=victim,Killer=killer,Map=victim.MapID,Kinds=kinds.ToArray(),KillerGuild=killer.Owner.Guild,VictimGuild=victim.Owner.Guild};
            if(!GuildEnemy(s)){s.KillerGuild=null;s.VictimGuild=null;}
            return kinds.Count==0 && s.KillerGuild==null?null:s;
        }
        private static bool Member(GameState p,Guild g){
            Guild live;Guild.Member member;
            return g!=null && g.ID!=0 && ReferenceEquals(p.Guild,g) && p.Entity.GuildID==g.ID
                && Kernel.Guilds.TryGetValue(g.ID,out live) && ReferenceEquals(live,g)
                && g.Members.TryGetValue(p.Entity.UID,out member) && ReferenceEquals(member,p.AsMember) && member.GuildID==g.ID;
        }
        private static bool GuildEnemy(Snapshot s){
            Guild enemy;
            return Member(s.Killer.Owner,s.KillerGuild) && Member(s.Victim.Owner,s.VictimGuild)
                && s.KillerGuild.ID!=s.VictimGuild.ID && s.KillerGuild.Enemy.TryGetValue(s.VictimGuild.ID,out enemy)
                && ReferenceEquals(enemy,s.VictimGuild);
        }
        private static bool Active(Entity entity,FlagsUpdate.Flags kind,DateTime now){
            StatusFlagsBigVector32.Flag flag;
            return entity.BitVector.ArrayFlags.TryGetValue((int)kind,out flag) && !flag.Expire(now);
        }
        internal static void Initialize(){using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_player_defeat_credit(
uid bigint NOT NULL CHECK(uid>0),kind integer NOT NULL CHECK(kind IN(10605,10606,10607,10611)),
victim bigint NOT NULL CHECK(victim>0 AND victim<>uid),map bigint NOT NULL,receipt uuid NOT NULL UNIQUE,
created_at timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,completed_at timestamptz,PRIMARY KEY(uid,kind));
CREATE TABLE IF NOT EXISTS revora_enemy_guild_defeat(
uid bigint PRIMARY KEY CHECK(uid>0),victim bigint NOT NULL CHECK(victim>0 AND victim<>uid),map bigint NOT NULL,
killer_guild bigint NOT NULL CHECK(killer_guild>0),victim_guild bigint NOT NULL CHECK(victim_guild>0 AND victim_guild<>killer_guild),
receipt uuid NOT NULL UNIQUE,created_at timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,completed_at timestamptz)");}}
        internal static void Recover(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,uint uid){
            var pending=new List<Tuple<int,Guid>>();
            using(var q=Q.Command(c,t,"SELECT kind,receipt FROM revora_player_defeat_credit WHERE uid=@uid AND completed_at IS NULL ORDER BY kind FOR UPDATE","uid",(long)uid))
            using(var r=q.ExecuteReader())while(r.Read())pending.Add(Tuple.Create(r.GetInt32(0),r.GetGuid(1)));
            foreach(var row in pending){string key=row.Item2.ToString("N");switch(row.Item1){
                case 10605:ProgressionEvidence.Complete(c,t,uid,10605,"player-defeat-v1",key,1);break;
                case 10606:ProgressionEvidence.Complete(c,t,uid,10606,"player-defeat-v1",key,1);break;
                case 10607:ProgressionEvidence.Complete(c,t,uid,10607,"player-defeat-v1",key,1);break;
                case 10611:ProgressionEvidence.Complete(c,t,uid,10611,"player-defeat-v1",key,1);break;
            }
            if(Q.Execute(c,t,"UPDATE revora_player_defeat_credit SET completed_at=CURRENT_TIMESTAMP WHERE uid=@uid AND kind=@kind AND completed_at IS NULL","uid",(long)uid,"kind",row.Item1)!=1)
                throw new InvalidOperationException("Player defeat receipt not completed.");}
            object receipt;
            using(var q=Q.Command(c,t,"SELECT receipt FROM revora_enemy_guild_defeat WHERE uid=@uid AND completed_at IS NULL FOR UPDATE","uid",(long)uid))receipt=q.ExecuteScalar();
            if(receipt!=null){
                ProgressionEvidence.Complete(c,t,uid,10227,"enemy-guild-defeat-v1",((Guid)receipt).ToString("N"),1);
                if(Q.Execute(c,t,"UPDATE revora_enemy_guild_defeat SET completed_at=CURRENT_TIMESTAMP WHERE uid=@uid AND completed_at IS NULL","uid",(long)uid)!=1)
                    throw new InvalidOperationException("Enemy guild defeat receipt not completed.");
            }
        }
        private static void RecordGuild(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,Snapshot s){
            if(!GuildEnemy(s))return;
            // One statement observes both saved memberships and the directed rivalry.
            // Do not lock the victim after locking the killer: reciprocal kills could deadlock.
            using(var q=Q.Command(c,t,@"WITH eligible AS (
SELECT 1 FROM entities k JOIN entities v ON v.uid=@victim
WHERE k.uid=@uid AND k.guildid=@kg AND v.guildid=@vg
AND EXISTS(SELECT 1 FROM guildenemy WHERE guildid=@kg AND enemyid=@vg)),
written AS (INSERT INTO revora_enemy_guild_defeat(uid,victim,map,killer_guild,victim_guild,receipt)
SELECT @uid,@victim,@map,@kg,@vg,@receipt FROM eligible ON CONFLICT(uid) DO NOTHING RETURNING uid)
SELECT NOT EXISTS(SELECT 1 FROM eligible) OR EXISTS(SELECT 1 FROM written)
OR EXISTS(SELECT 1 FROM revora_enemy_guild_defeat WHERE uid=@uid)",
                "uid",(long)s.Killer.UID,"victim",(long)s.Victim.UID,"map",(long)s.Map,
                "kg",(long)s.KillerGuild.ID,"vg",(long)s.VictimGuild.ID,"receipt",Guid.NewGuid()))
                if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Enemy guild defeat receipt not saved.");
        }
        internal static void Accepted(Snapshot s){
            if(s==null || s.Victim.Hitpoints!=0 || !ReferenceEquals(s.Victim.Killer,s.Killer))return;
            var p=s.Killer.Owner;
            lock(p.ItemSyncRoot) {
                if(!Q.Player(p) || !Q.Player(s.Victim.Owner,false) || !ReferenceEquals(p.Entity,s.Killer)
                    || !ReferenceEquals(s.Victim.Owner.Entity,s.Victim) || !ReferenceEquals(p.Map,s.Victim.Owner.Map)
                    || s.Victim.MapID!=s.Map || s.Killer.MapID!=s.Map)return;
                try{using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    SkillTable.LockOwner(c,t,p);
                    foreach(int kind in s.Kinds){
                        Q.Execute(c,t,"INSERT INTO revora_player_defeat_credit(uid,kind,victim,map,receipt) VALUES(@uid,@kind,@victim,@map,@key) ON CONFLICT(uid,kind) DO NOTHING",
                            "uid",(long)p.Entity.UID,"kind",kind,"victim",(long)s.Victim.UID,"map",(long)s.Map,"key",Guid.NewGuid());
                        using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_player_defeat_credit WHERE uid=@uid AND kind=@kind)","uid",(long)p.Entity.UID,"kind",kind))
                            if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Player defeat receipt not saved.");
                    }RecordGuild(c,t,s);t.Commit();}
                    using(var t=c.BeginTransaction()){Recover(c,t,p.Entity.UID);t.Commit();}}
                    Q.Refresh(p);
                }catch(Exception ex){Q.Fail(p,"player-defeat-credit",ex);}
            }
        }
    }
}
