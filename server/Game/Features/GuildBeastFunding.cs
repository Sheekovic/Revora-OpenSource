using System;
using System.Globalization;
using Npgsql;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features {
    // The NPC adapter holds session, inventory and GuildLeadership ownership,
    // then projects the committed fund balance.
    internal static class GuildBeastFunding {
        internal const long Price=500000;
        internal enum Result { Rejected, Funded, AlreadyFunded }
        internal static void Initialize() {
            using(var c=Revora.Database.DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_guild_beast_days(
 day date PRIMARY KEY,guild_id bigint NOT NULL CHECK(guild_id>0),leader_uid bigint NOT NULL CHECK(leader_uid>0),
 battle_day varchar(10) NOT NULL REFERENCES revora_guild_battles(day),
 starts_at timestamptz NOT NULL,ends_at timestamptz NOT NULL,expires_at timestamptz NOT NULL,
 paid_silver bigint NOT NULL CHECK(paid_silver=500000),
 state varchar(12) NOT NULL CHECK(state IN('funded','active','killed','fallback','expired')),
 paid_at timestamptz NOT NULL,CHECK(starts_at<ends_at AND ends_at<expires_at))");}
        }
        internal static bool Window(DateTime utc,out DateTime day,out DateTime start,out DateTime end,out DateTime expiry) {
            if(utc.Kind!=DateTimeKind.Utc)throw new ArgumentException("Guild Beast clock must be UTC.","utc");
            var local=utc.ToLocalTime();day=DateTime.SpecifyKind(local.Date,DateTimeKind.Unspecified);
            start=DateTime.SpecifyKind(day.AddHours(21),DateTimeKind.Local).ToUniversalTime();
            end=DateTime.SpecifyKind(day.AddHours(22),DateTimeKind.Local).ToUniversalTime();
            expiry=DateTime.SpecifyKind(day.AddDays(1),DateTimeKind.Local).ToUniversalTime();
            return utc<start;
        }
        // Caller owns the transaction: a failed write must roll back, never commit.
        internal static Result Reserve(NpgsqlConnection c,NpgsqlTransaction t,uint uid,uint guild,DateTime utc) {
            if(c==null || t==null || !ReferenceEquals(t.Connection,c))throw new ArgumentException("Guild Beast funding requires its owning transaction.");
            DateTime day,start,end,expiry;
            if(uid==0 || guild==0 || !Window(utc,out day,out start,out end,out expiry))return Result.Rejected;
            string name;
            using(var q=Q.Command(c,t,"SELECT name FROM entities WHERE uid=@uid AND guildid=@guild AND guildrank=@rank FOR UPDATE",
                "uid",(long)uid,"guild",(long)guild,"rank",(int)Enums.GuildMemberRank.GuildLeader)) {
                var value=q.ExecuteScalar();if(value==null)return Result.Rejected;name=Convert.ToString(value,CultureInfo.InvariantCulture);
            }
            decimal funds;
            using(var q=Q.Command(c,t,"SELECT silverfund FROM guilds WHERE id=@guild AND leadername=@name FOR UPDATE","guild",(long)guild,"name",name)) {
                var value=q.ExecuteScalar();if(value==null)return Result.Rejected;funds=Convert.ToDecimal(value,CultureInfo.InvariantCulture);
            }
            string battle;
            // Inspect the latest scheduled battle, including empty/cancelled/active
            // results: an old winner must not survive a later invalid result.
            using(var q=Q.Command(c,t,@"SELECT day,state,keeper,ends_at FROM revora_guild_battles
WHERE starts_at<=@now ORDER BY starts_at DESC,day DESC LIMIT 1 FOR SHARE","now",utc))
            using(var r=q.ExecuteReader()) {
                if(!r.Read() || r.GetString(1)!="won" || Convert.ToInt64(r[2])!=guild)return Result.Rejected;
                var ended=DateTime.SpecifyKind(r.GetDateTime(3),DateTimeKind.Utc);
                if(ended>utc || utc>=ended.AddDays(7))return Result.Rejected;
                battle=r.GetString(0);
            }
            using(var q=Q.Command(c,t,"SELECT guild_id FROM revora_guild_beast_days WHERE day=CAST(@day AS date) FOR UPDATE","day",day.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture))) {
                var value=q.ExecuteScalar();if(value!=null)return Convert.ToInt64(value)==guild?Result.AlreadyFunded:Result.Rejected;
            }
            if(funds<Price)return Result.Rejected;
            // The unique day also arbitrates competing processes with different
            // guild rows. Only the process that inserted may debit the fund.
            if(Q.Execute(c,t,@"INSERT INTO revora_guild_beast_days(day,guild_id,leader_uid,battle_day,starts_at,ends_at,expires_at,paid_silver,state,paid_at)
VALUES(CAST(@day AS date),@guild,@uid,@battle,@start,@end,@expiry,500000,'funded',@now) ON CONFLICT(day) DO NOTHING",
                "day",day.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),"guild",(long)guild,"uid",(long)uid,"battle",battle,"start",start,"end",end,"expiry",expiry,"now",utc)!=1)return Result.Rejected;
            if(Q.Execute(c,t,"UPDATE guilds SET silverfund=silverfund-@price WHERE id=@guild AND leadername=@name AND silverfund>=@price",
                "price",Price,"guild",(long)guild,"name",name)!=1)throw new InvalidOperationException("Guild Beast funding did not debit its reserved fee.");
            return Result.Funded;
        }
    }
}
