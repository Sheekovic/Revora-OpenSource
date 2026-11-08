using System;
using Npgsql;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features {
    // Persistence only. The world adapter owns canonical body / killer checks.
    // GuildBeastEvent provides the owned world and inventory-delivery adapter.
    internal static class GuildBeastRuns {
        private static bool initialized;
        internal sealed class Run {
            internal string Day,State;internal uint Guild,Actor,Killer,Soul;internal Guid Life;
            internal DateTime Start,End,Expiry;internal bool Claimed;
        }
        internal static void Initialize() {
            GuildBeastFunding.Initialize();
            using(var c=Revora.Database.DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"ALTER TABLE revora_guild_beast_days
ADD COLUMN IF NOT EXISTS life uuid,
ADD COLUMN IF NOT EXISTS actor bigint CHECK(actor>0),
ADD COLUMN IF NOT EXISTS killer bigint CHECK(killer>0),
ADD COLUMN IF NOT EXISTS settled_at timestamptz,
ADD COLUMN IF NOT EXISTS claimed_uid bigint CHECK(claimed_uid>0),
ADD COLUMN IF NOT EXISTS reward_item bigint CHECK(reward_item>0),
ADD COLUMN IF NOT EXISTS claimed_at timestamptz,
ADD COLUMN IF NOT EXISTS soul_type bigint NOT NULL DEFAULT 0,
ADD COLUMN IF NOT EXISTS soul_uid bigint NOT NULL DEFAULT 0;
CREATE UNIQUE INDEX IF NOT EXISTS revora_guild_beast_life ON revora_guild_beast_days(life) WHERE life IS NOT NULL;");}initialized=true;
        }
        internal static void Recover(NpgsqlConnection c,NpgsqlTransaction t,uint uid) {
            if(!initialized)return;
            using(var q=Q.Command(c,t,@"SELECT to_char(day,'YYYY-MM-DD') FROM revora_guild_beast_days WHERE state='killed' AND killer=@uid
AND NOT EXISTS(SELECT 1 FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10303) ORDER BY day LIMIT 1","uid",(long)uid)) {
                var day=q.ExecuteScalar();if(day!=null)ProgressionEvidence.Complete(c,t,uid,10303,"guild-beast-daily-v1",Convert.ToString(day),1);
            }
        }
        private static void Require(NpgsqlConnection c,NpgsqlTransaction t,DateTime now) {
            if(c==null || t==null || !ReferenceEquals(t.Connection,c) || now.Kind!=DateTimeKind.Utc)throw new ArgumentException("Guild Beast lifecycle requires its owning transaction and UTC clock.");
        }
        internal static Run Read(NpgsqlConnection c,NpgsqlTransaction t,string day) {
            if(c==null || t==null || !ReferenceEquals(t.Connection,c))throw new ArgumentException("Guild Beast read requires its owning transaction.");
            using(var q=Q.Command(c,t,@"SELECT to_char(day,'YYYY-MM-DD'),state,guild_id,actor,life,killer,starts_at,ends_at,expires_at,claimed_at,soul_type
FROM revora_guild_beast_days WHERE day=CAST(@day AS date) FOR UPDATE","day",day))using(var r=q.ExecuteReader()) {
                if(!r.Read())return null;
                return new Run{Day=r.GetString(0),State=r.GetString(1),Guild=Convert.ToUInt32(r[2]),Actor=r.IsDBNull(3)?0:Convert.ToUInt32(r[3]),
                    Life=r.IsDBNull(4)?Guid.Empty:r.GetGuid(4),Killer=r.IsDBNull(5)?0:Convert.ToUInt32(r[5]),Start=r.GetFieldValue<DateTimeOffset>(6).UtcDateTime,
                    End=r.GetFieldValue<DateTimeOffset>(7).UtcDateTime,Expiry=r.GetFieldValue<DateTimeOffset>(8).UtcDateTime,Claimed=!r.IsDBNull(9),Soul=Convert.ToUInt32(r[10])};
            }
        }
        internal static bool Bind(NpgsqlConnection c,NpgsqlTransaction t,string day,Guid life,uint actor,DateTime now) {
            Require(c,t,now);if(life==Guid.Empty || actor==0)return false;
            return Q.Execute(c,t,@"UPDATE revora_guild_beast_days SET state='active',life=@life,actor=@actor
WHERE day=CAST(@day AS date) AND state IN('funded','active') AND starts_at<=@now AND ends_at>@now",
                "day",day,"life",life,"actor",(long)actor,"now",now)==1;
        }
        internal static bool Defeat(NpgsqlConnection c,NpgsqlTransaction t,string day,Guid life,uint actor,uint killer,DateTime now) {
            Require(c,t,now);if(life==Guid.Empty || actor==0 || killer==0)return false;
            // Runtime acceptance and character row ownership precede this call.
            if(Q.Execute(c,t,@"UPDATE revora_guild_beast_days SET state='killed',killer=@killer,settled_at=@now
WHERE day=CAST(@day AS date) AND state='active' AND life=@life AND actor=@actor AND starts_at<=@now AND ends_at>@now",
                "day",day,"life",life,"actor",(long)actor,"killer",(long)killer,"now",now)!=1)return false;
            ProgressionEvidence.Complete(c,t,killer,10303,"guild-beast-daily-v1",day,1);return true;
        }
        internal static void Advance(NpgsqlConnection c,NpgsqlTransaction t,DateTime now) {
            Require(c,t,now);
            Q.Execute(c,t,@"UPDATE revora_guild_beast_days SET state=CASE WHEN expires_at>@now THEN 'fallback' ELSE 'expired' END,settled_at=@now
WHERE state IN('funded','active') AND ends_at<=@now","now",now);
            Q.Execute(c,t,"UPDATE revora_guild_beast_days SET state='expired' WHERE state='fallback' AND expires_at<=@now AND claimed_at IS NULL","now",now);
        }
        internal static bool MarkClaim(NpgsqlConnection c,NpgsqlTransaction t,string day,uint uid,uint item,DateTime now) {
            Require(c,t,now);if(uid==0 || item==0)return false;
            // Eligibility and the item INSERT must be in this same transaction.
            // Keep a killed player's reward recoverable even after midnight;
            // unclaimed fallback rewards retain the original midnight deadline.
            return Q.Execute(c,t,@"UPDATE revora_guild_beast_days SET claimed_uid=@uid,reward_item=@item,claimed_at=@now
WHERE day=CAST(@day AS date) AND claimed_at IS NULL AND settled_at<=@now AND ((state='killed' AND killer=@uid)
OR (state='fallback' AND expires_at>@now AND EXISTS(SELECT 1 FROM entities e JOIN guilds g ON g.id=e.guildid
WHERE e.uid=@uid AND e.guildid=revora_guild_beast_days.guild_id AND
((e.guildrank=@leader AND e.name=g.leadername) OR e.guildrank=@deputy))))",
                "day",day,"uid",(long)uid,"item",(long)item,"now",now,"leader",(int)Enums.GuildMemberRank.GuildLeader,"deputy",(int)Enums.GuildMemberRank.DeputyLeader)==1;
        }
    }
}
