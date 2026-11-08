using System;
using System.Collections.Generic;
using Npgsql;
using Revora.Client;
using Revora.Game;
using Revora.Game.Features;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Database {
    internal static class GuildPkRewardTable {
        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_guild_pk_pools(
run uuid NOT NULL REFERENCES revora_guild_pk_matches(id),guild_id bigint NOT NULL,amount bigint NOT NULL CHECK(amount BETWEEN 1 AND 1000000000),
state text NOT NULL CHECK(state IN('funded','allocated','refunded','orphaned')),refund bigint NOT NULL DEFAULT 0 CHECK(refund>=0),settled_at timestamptz,
PRIMARY KEY(run,guild_id));
CREATE TABLE IF NOT EXISTS revora_guild_pk_funding(
id uuid PRIMARY KEY,run uuid NOT NULL,guild_id bigint NOT NULL,uid bigint NOT NULL,amount bigint NOT NULL CHECK(amount>0),funded_at timestamptz NOT NULL,
FOREIGN KEY(run,guild_id) REFERENCES revora_guild_pk_pools(run,guild_id));
CREATE TABLE IF NOT EXISTS revora_guild_pk_payouts(
run uuid NOT NULL,guild_id bigint NOT NULL,uid bigint NOT NULL,points bigint NOT NULL CHECK(points>0),amount bigint NOT NULL CHECK(amount>=0),paid_at timestamptz,attempted_at timestamptz,
PRIMARY KEY(run,guild_id,uid),FOREIGN KEY(run,guild_id) REFERENCES revora_guild_pk_pools(run,guild_id),FOREIGN KEY(run,uid) REFERENCES revora_guild_pk_entries(run,uid));
CREATE INDEX IF NOT EXISTS revora_guild_pk_pool_pending ON revora_guild_pk_pools(run,guild_id) WHERE state='funded';
CREATE INDEX IF NOT EXISTS revora_guild_pk_pay_pending ON revora_guild_pk_payouts(uid,run,guild_id) WHERE paid_at IS NULL;");}
        }
        internal static int Fund(NpgsqlConnection c,NpgsqlTransaction t,Guid key,Guid run,uint guild,uint uid,long amount,DateTime utc,out ulong balance) {
            balance=0;if(t==null || key==Guid.Empty || amount<=0 || amount>GuildPkRules.PoolLimit || utc.Kind!=DateTimeKind.Utc)return 0;
            Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s'");
            Guid contest;bool open;
            using(var q=Q.Command(c,t,"SELECT contest,state='scheduled' AND @now>=starts_at-interval '5 minutes' AND @now<starts_at FROM revora_guild_pk_matches WHERE id=@run AND @guild IN(first_guild,second_guild) FOR UPDATE","run",run,"guild",(long)guild,"now",utc))using(var r=q.ExecuteReader()) {
                if(!r.Read())return 0;contest=r.GetGuid(0);open=r.GetBoolean(1);
            }
            string name;int rank;
            using(var q=Q.Command(c,t,"SELECT name,guildrank FROM entities WHERE uid=@uid AND guildid=@guild AND guildrank IN(990,1000) FOR UPDATE","uid",(long)uid,"guild",(long)guild))using(var r=q.ExecuteReader()) {
                if(!r.Read())return 0;name=r.GetString(0);rank=Convert.ToInt32(r[1]);
            }
            decimal saved;
            using(var q=Q.Command(c,t,"SELECT silverfund,leadername FROM guilds WHERE id=@guild FOR UPDATE","guild",(long)guild))using(var r=q.ExecuteReader()) {
                if(!r.Read() || rank==1000 && name!=r.GetString(1))return 0;saved=Convert.ToDecimal(r[0]);
            }
            if(saved<0 || saved>long.MaxValue)return 0;
            using(var q=Q.Command(c,t,"SELECT eligible FROM revora_guild_contest_owners WHERE run=@run AND guild_id=@guild FOR SHARE","run",contest,"guild",(long)guild))if(!Equals(q.ExecuteScalar(),true))return 0;
            using(var q=Q.Command(c,t,"SELECT run=@run AND guild_id=@guild AND uid=@uid AND amount=@amount FROM revora_guild_pk_funding WHERE id=@id","id",key,"run",run,"guild",(long)guild,"uid",(long)uid,"amount",amount)) {
                var receipt=q.ExecuteScalar();if(receipt!=null){balance=(ulong)saved;return Equals(receipt,true)?2:0;}
            }
            if(!open || saved<amount)return 0;
            long total=0;using(var q=Q.Command(c,t,"SELECT amount,state FROM revora_guild_pk_pools WHERE run=@run AND guild_id=@guild FOR UPDATE","run",run,"guild",(long)guild))using(var r=q.ExecuteReader()) {
                if(r.Read()){if(r.GetString(1)!="funded")return 0;total=r.GetInt64(0);}
            }
            if(total>GuildPkRules.PoolLimit-amount)return 0;
            if(Q.Execute(c,t,@"INSERT INTO revora_guild_pk_pools(run,guild_id,amount,state) VALUES(@run,@guild,@amount,'funded')
ON CONFLICT(run,guild_id) DO UPDATE SET amount=EXCLUDED.amount","run",run,"guild",(long)guild,"amount",total+amount)!=1)throw new InvalidOperationException("Guild PK pool was not saved.");
            if(Q.Execute(c,t,"INSERT INTO revora_guild_pk_funding VALUES(@id,@run,@guild,@uid,@amount,@now)","id",key,"run",run,"guild",(long)guild,"uid",(long)uid,"amount",amount,"now",utc)!=1)throw new InvalidOperationException("Guild PK funding receipt was not saved.");
            if(Q.Execute(c,t,"UPDATE guilds SET silverfund=@balance WHERE id=@guild","guild",(long)guild,"balance",saved-amount)!=1)throw new InvalidOperationException("Guild PK funding was not deducted.");
            balance=(ulong)(saved-amount);return 1;
        }
        internal static bool Allocate(NpgsqlConnection c,NpgsqlTransaction t,Guid run,uint guild,DateTime utc,out ulong? refundBalance) {
            refundBalance=null;Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s'");
            Guid contest;string state;
            using(var q=Q.Command(c,t,"SELECT contest,state FROM revora_guild_pk_matches WHERE id=@run FOR UPDATE","run",run))using(var r=q.ExecuteReader()) {
                if(!r.Read())return false;contest=r.GetGuid(0);state=r.GetString(1);
            }
            if(state!="complete" && state!="cancelled")return false;
            if(state=="complete")using(var q=Q.Command(c,t,"SELECT run FROM revora_guild_pk_results WHERE run=@run","run",run))if(q.ExecuteScalar()==null)return false;
            long amount;
            using(var q=Q.Command(c,t,"SELECT amount,state FROM revora_guild_pk_pools WHERE run=@run AND guild_id=@guild FOR UPDATE","run",run,"guild",(long)guild))using(var r=q.ExecuteReader()) {
                if(!r.Read())return false;if(r.GetString(1)!="funded")return true;amount=r.GetInt64(0);
            }
            var points=new Dictionary<uint,long>();
            if(state=="complete")using(var q=Q.Command(c,t,@"SELECT earned.uid,sum(earned.points)::bigint FROM (
SELECT d.killer AS uid,sum(d.exploits)::bigint AS points FROM revora_guild_pk_defeats d JOIN revora_guild_pk_entries e ON e.run=d.run AND e.uid=d.killer AND e.admission=d.killer_admission
WHERE d.run=@run AND e.guild_id=@guild AND d.exploits IS NOT NULL GROUP BY d.killer
UNION ALL SELECT uid,exploits FROM revora_guild_pk_banner_contributions WHERE run=@run AND guild_id=@guild
UNION ALL SELECT uid,exploits FROM revora_guild_pk_revival_contributions WHERE run=@run AND guild_id=@guild
) earned GROUP BY earned.uid HAVING sum(earned.points)>0 ORDER BY earned.uid LIMIT 4097","run",run,"guild",(long)guild))using(var r=q.ExecuteReader())while(r.Read())points.Add(checked((uint)r.GetInt64(0)),r.GetInt64(1));
            if(points.Count==0) {
                using(var q=Q.Command(c,t,@"SELECT g.silverfund FROM guilds g JOIN revora_guild_contest_owners o ON o.guild_id=g.id AND o.run=@contest AND o.eligible
WHERE g.id=@guild FOR UPDATE OF g FOR SHARE OF o","contest",contest,"guild",(long)guild)) {
                    var value=q.ExecuteScalar();if(value!=null){decimal current=Convert.ToDecimal(value);if(current<0 || current>long.MaxValue-amount) return false;
                        if(Q.Execute(c,t,"UPDATE guilds SET silverfund=@balance WHERE id=@guild","guild",(long)guild,"balance",current+amount)!=1)throw new InvalidOperationException("Guild PK refund was not saved.");refundBalance=(ulong)(current+amount);}
                }
                Finish(c,t,run,guild,refundBalance.HasValue?"refunded":"orphaned",refundBalance.HasValue?amount:0,utc);return true;
            }
            var shares=GuildPkRules.DividePool(amount,points);
            foreach(var share in shares)if(Q.Execute(c,t,@"INSERT INTO revora_guild_pk_payouts(run,guild_id,uid,points,amount,paid_at)
VALUES(@run,@guild,@uid,@points,@amount,CASE WHEN @amount=0 THEN @now ELSE NULL END)","run",run,"guild",(long)guild,"uid",(long)share.Key,"points",points[share.Key],"amount",share.Value,"now",utc)!=1)throw new InvalidOperationException("Guild PK member share was not saved.");
            Finish(c,t,run,guild,"allocated",0,utc);return true;
        }
        private static void Finish(NpgsqlConnection c,NpgsqlTransaction t,Guid run,uint guild,string state,long refund,DateTime utc) {
            if(Q.Execute(c,t,"UPDATE revora_guild_pk_pools SET state=@state,refund=@refund,settled_at=@now WHERE run=@run AND guild_id=@guild AND state='funded'","run",run,"guild",(long)guild,"state",state,"refund",refund,"now",utc)!=1)throw new InvalidOperationException("Guild PK pool settlement changed.");
        }
        internal static bool Pay(NpgsqlConnection c,NpgsqlTransaction t,Guid run,uint guild,uint uid,GameState player,DateTime utc,out ulong balance) {
            balance=0;
            if(t==null || !ReferenceEquals(t.Connection,c) || utc.Kind!=DateTimeKind.Utc || !SessionInstall.Holds(uid))throw new InvalidOperationException("Guild PK payout requires transaction and session ownership.");
            GameState current;
            if(player!=null ? !Q.Player(player,false) || player.Entity.UID!=uid || !System.Threading.Monitor.IsEntered(player.ItemSyncRoot) : Kernel.GamePool.TryGetValue(uid,out current))throw new InvalidOperationException("Guild PK payout session changed.");
            Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s'");
            long amount;using(var q=Q.Command(c,t,"SELECT amount FROM revora_guild_pk_payouts WHERE run=@run AND guild_id=@guild AND uid=@uid AND paid_at IS NULL FOR UPDATE","run",run,"guild",(long)guild,"uid",(long)uid)) {
                var value=q.ExecuteScalar();if(value==null)return false;amount=Convert.ToInt64(value);
            }
            if(Q.Execute(c,t,"UPDATE revora_guild_pk_payouts SET attempted_at=@now WHERE run=@run AND guild_id=@guild AND uid=@uid","run",run,"guild",(long)guild,"uid",(long)uid,"now",utc)!=1)throw new InvalidOperationException("Guild PK reward attempt changed.");
            if(player!=null){if(player.Entity.UID!=uid)throw new InvalidOperationException("Guild PK payout owner changed.");SilverBalance.AssertSaved(c,t,player);balance=player.Entity.Money;}
            else using(var q=Q.Command(c,t,"SELECT money FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)uid)) {
                var value=q.ExecuteScalar();if(value==null)return false;balance=value==DBNull.Value?0:checked((ulong)Convert.ToInt64(value));
            }
            if(balance>Entity.MaximumSilver-(ulong)amount)return false;balance+=(ulong)amount;
            if(player!=null)SilverBalance.Persist(c,t,player,balance);
            else if(Q.Execute(c,t,"UPDATE entities SET money=@amount WHERE uid=@uid","uid",(long)uid,"amount",checked((long)balance))!=1)throw new InvalidOperationException("Guild PK offline reward was not saved.");
            if(Q.Execute(c,t,"UPDATE revora_guild_pk_payouts SET paid_at=@now WHERE run=@run AND guild_id=@guild AND uid=@uid AND paid_at IS NULL","run",run,"guild",(long)guild,"uid",(long)uid,"now",utc)!=1)throw new InvalidOperationException("Guild PK reward receipt changed.");
            return true;
        }
    }
}
