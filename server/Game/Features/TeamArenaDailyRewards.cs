using System;
using Npgsql;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static class TeamArenaDailyRewards
    {
        internal static bool Initialized {get;private set;}
        internal static void Initialize() {
            using(var c=Database.DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_team_arena_daily_rewards(
uid bigint NOT NULL,day varchar(8) NOT NULL,milestone varchar(12) NOT NULL CHECK(milestone IN('wins9','matches20')),
source_run uuid NOT NULL REFERENCES revora_team_arena_results(id),honor integer NOT NULL CHECK(honor=500),
item_type integer NOT NULL CHECK(item_type=723700),paid_at timestamptz,item_uid bigint UNIQUE,
PRIMARY KEY(uid,day,milestone),CHECK((paid_at IS NULL AND item_uid IS NULL) OR (paid_at IS NOT NULL AND item_uid IS NOT NULL)));
ALTER TABLE revora_team_arena_daily_rewards ADD COLUMN IF NOT EXISTS rune_type integer NOT NULL DEFAULT 0 CHECK(rune_type IN(0,4030101));
ALTER TABLE revora_team_arena_daily_rewards ADD COLUMN IF NOT EXISTS rune_uid bigint UNIQUE CHECK(
(rune_type=0 AND rune_uid IS NULL) OR (rune_type=4030101 AND
((paid_at IS NULL AND rune_uid IS NULL) OR (paid_at IS NOT NULL AND rune_uid IS NOT NULL))));");}
            Initialized=true;
        }
        // Both normal completion and fenced/startup recovery call inside their result transaction.
        internal static void Record(NpgsqlConnection c,NpgsqlTransaction t,Guid run) {
            if(!Initialized || t==null || t.Connection!=c)throw new InvalidOperationException("Team daily rewards require their result transaction.");
            string day;
            using(var q=Q.Command(c,t,@"SELECT to_char(completed_at AT TIME ZONE 'America/Los_Angeles','YYYYMMDD')
FROM revora_team_arena_results r WHERE id=@run AND participants=(SELECT count(*) FROM revora_team_arena_statistics s WHERE s.run=r.id)","run",run))
                day=q.ExecuteScalar() as string;
            if(day==null)throw new InvalidOperationException("Team daily result roster is incomplete.");
            var owners=new System.Collections.Generic.List<long>();
            using(var q=Q.Command(c,t,"SELECT uid FROM revora_team_arena_statistics WHERE run=@run ORDER BY uid","run",run))
            using(var r=q.ExecuteReader())while(r.Read())owners.Add(r.GetInt64(0));
            foreach(var uid in owners) {
                long matches,wins;
                using(var q=Q.Command(c,t,@"SELECT count(*),count(*) FILTER(WHERE s.won)
FROM revora_team_arena_statistics s JOIN revora_team_arena_results r ON r.id=s.run
WHERE s.uid=@uid AND to_char(r.completed_at AT TIME ZONE 'America/Los_Angeles','YYYYMMDD')=@day
AND r.participants=(SELECT count(*) FROM revora_team_arena_statistics members WHERE members.run=r.id)","uid",uid,"day",day))
                using(var r=q.ExecuteReader()){r.Read();matches=r.GetInt64(0);wins=r.GetInt64(1);}
                foreach(var milestone in new[]{"wins9","matches20"}) {
                    if(milestone=="wins9"?wins<9:matches<20)continue;
                    Q.Execute(c,t,@"INSERT INTO revora_team_arena_daily_rewards(uid,day,milestone,source_run,honor,item_type,rune_type)
VALUES(@uid,@day,@milestone,@run,500,723700,4030101) ON CONFLICT(uid,day,milestone) DO NOTHING",
                        "uid",uid,"day",day,"milestone",milestone,"run",run);
                    using(var q=Q.Command(c,t,"SELECT count(*) FROM revora_team_arena_daily_rewards WHERE uid=@uid AND day=@day AND milestone=@milestone AND honor=500 AND item_type=723700",
                        "uid",uid,"day",day,"milestone",milestone))if(Convert.ToInt64(q.ExecuteScalar())!=1)throw new InvalidOperationException("Team daily entitlement was not saved.");
                }
            }
        }
        internal static void Pay(Client.GameState player){if(Initialized)ArenaDailyRewards.PayTeam(player);}
    }
}
