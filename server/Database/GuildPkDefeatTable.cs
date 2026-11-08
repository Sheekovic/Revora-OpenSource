using System;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Database
{
    internal static class GuildPkDefeatTable
    {
        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_guild_pk_defeats(
run uuid NOT NULL,uid bigint NOT NULL,number integer NOT NULL CHECK(number BETWEEN 1 AND 20),
killer bigint NOT NULL,admission uuid NOT NULL,killer_admission uuid NOT NULL,defeated_at timestamptz NOT NULL,
PRIMARY KEY(run,uid,number),FOREIGN KEY(run,uid) REFERENCES revora_guild_pk_entries(run,uid),
FOREIGN KEY(run,killer) REFERENCES revora_guild_pk_entries(run,uid),CHECK(uid<>killer));
ALTER TABLE revora_guild_pk_defeats ADD COLUMN IF NOT EXISTS killer_bp integer CHECK(killer_bp>=0);
ALTER TABLE revora_guild_pk_defeats ADD COLUMN IF NOT EXISTS victim_bp integer CHECK(victim_bp>=0);
ALTER TABLE revora_guild_pk_defeats ADD COLUMN IF NOT EXISTS exploits integer CHECK(exploits BETWEEN 1 AND 3);
CREATE TABLE IF NOT EXISTS revora_guild_pk_revivals(
run uuid NOT NULL,uid bigint NOT NULL,number integer NOT NULL,reviver bigint,reviver_admission uuid,revived_at timestamptz NOT NULL,
PRIMARY KEY(run,uid,number),FOREIGN KEY(run,uid,number) REFERENCES revora_guild_pk_defeats(run,uid,number),
FOREIGN KEY(run,reviver) REFERENCES revora_guild_pk_entries(run,uid),CHECK(reviver IS NULL OR reviver<>uid),CHECK((reviver IS NULL)=(reviver_admission IS NULL)));
CREATE OR REPLACE VIEW revora_guild_pk_revival_contributions AS
SELECT r.run,r.reviver AS uid,e.guild_id,r.reviver_admission AS admission,(count(*)*10)::bigint AS exploits
FROM revora_guild_pk_revivals r JOIN revora_guild_pk_entries e ON e.run=r.run AND e.uid=r.reviver AND e.admission=r.reviver_admission
WHERE r.reviver IS NOT NULL GROUP BY r.run,r.reviver,e.guild_id,r.reviver_admission;");}
        }
        internal static bool Restore(Guid run,uint uid,int number,uint reviver,Guid admission,DateTime utc) {
            if(uid==0 || number<1 || number>=20 || uid==reviver || utc.Kind!=DateTimeKind.Utc || reviver!=0 && admission==Guid.Empty)return false;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s'");
                using(var q=Q.Command(c,t,"SELECT id FROM revora_guild_pk_matches WHERE id=@run FOR UPDATE","run",run))if(q.ExecuteScalar()==null)return false;
                using(var q=Q.Command(c,t,"SELECT @reviver=0 OR reviver=@reviver AND reviver_admission=@admission FROM revora_guild_pk_revivals WHERE run=@run AND uid=@uid AND number=@number","run",run,"uid",(long)uid,"number",number,"reviver",(long)reviver,"admission",admission)) {
                    var saved=q.ExecuteScalar();if(saved!=null){t.Commit();return Equals(saved,true);}
                }
                using(var q=Q.Command(c,t,@"SELECT d.uid FROM revora_guild_pk_defeats d JOIN revora_guild_pk_entries p ON p.run=d.run AND p.uid=d.uid AND p.admission=d.admission
JOIN entities e ON e.uid=p.uid AND e.mapid=p.map AND e.guildid=p.guild_id
JOIN revora_guild_pk_matches m ON m.id=p.run
WHERE d.run=@run AND d.uid=@uid AND d.number=@number AND p.deaths=d.number AND p.active AND @now>=d.defeated_at
AND (@reviver=0 AND @now>=d.defeated_at+interval '5 seconds' OR @reviver<>0 AND @now<d.defeated_at+interval '5 seconds' AND m.state='active' AND @now<m.ends_at
AND EXISTS(SELECT 1 FROM revora_guild_pk_entries a JOIN entities ae ON ae.uid=a.uid AND ae.mapid=a.map AND ae.guildid=a.guild_id AND ae.guildrank BETWEEN 1 AND 1000
JOIN revora_guild_admissions ga ON ga.uid=a.uid AND ga.guild_id=a.guild_id AND ga.admission=a.admission
JOIN revora_guild_admissions gp ON gp.uid=p.uid AND gp.guild_id=p.guild_id AND gp.admission=p.admission
JOIN revora_guild_contest_owners o ON o.run=m.contest AND o.guild_id=a.guild_id AND o.eligible
WHERE a.run=p.run AND a.uid=@reviver AND a.admission=@admission AND a.guild_id=p.guild_id AND a.map=p.map AND a.active AND a.deaths<20
AND ga.joined_at<=@cutoff AND gp.joined_at<=@cutoff FOR SHARE OF a,ae,ga,gp,o)) FOR SHARE OF p,e",
                    "run",run,"uid",(long)uid,"number",number,"reviver",(long)reviver,"admission",admission,"now",utc,"cutoff",utc.AddDays(-7)))if(q.ExecuteScalar()==null)return false;
                if(Q.Execute(c,t,@"INSERT INTO revora_guild_pk_revivals(run,uid,number,reviver,reviver_admission,revived_at)
VALUES(@run,@uid,@number,CASE WHEN @reviver=0 THEN NULL ELSE @reviver END,CASE WHEN @reviver=0 THEN NULL ELSE @admission END,@now)",
                    "run",run,"uid",(long)uid,"number",number,"reviver",(long)reviver,"admission",admission,"now",utc)!=1)throw new InvalidOperationException("Guild PK recovery was not saved.");
                t.Commit();return true;
            }}
        }
        internal static bool Record(Guid run,uint uid,uint killer,Guid admission,Guid killerAdmission,int number,int killerPower,int victimPower,DateTime utc) {
            if(uid==0 || killer==0 || uid==killer || number<1 || number>20 || killerPower<0 || victimPower<0 || utc.Kind!=DateTimeKind.Utc)return false;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s'");
                bool live;
                using(var q=Q.Command(c,t,"SELECT state='active' AND @now>=starts_at AND @now<ends_at FROM revora_guild_pk_matches WHERE id=@run FOR UPDATE","run",run,"now",utc)) {
                    var value=q.ExecuteScalar();if(value==null)return false;live=(bool)value;
                }
                using(var q=Q.Command(c,t,"SELECT killer=@killer AND admission=@admission AND killer_admission=@other AND killer_bp=@killerbp AND victim_bp=@victimbp FROM revora_guild_pk_defeats WHERE run=@run AND uid=@uid AND number=@number",
                    "run",run,"uid",(long)uid,"number",number,"killer",(long)killer,"admission",admission,"other",killerAdmission,"killerbp",killerPower,"victimbp",victimPower)) {
                    var saved=q.ExecuteScalar();if(saved!=null){t.Commit();return Equals(saved,true);}
                }
                if(!live)return false;
                int count=0;long firstGuild=0;bool different=false;
                using(var q=Q.Command(c,t,@"SELECT p.uid,p.guild_id,p.deaths FROM revora_guild_pk_entries p
JOIN entities e ON e.uid=p.uid AND e.guildid=p.guild_id AND e.guildrank BETWEEN 1 AND 1000 AND e.mapid=p.map
JOIN guilds g ON g.id=e.guildid JOIN revora_guild_admissions a ON a.uid=e.uid AND a.guild_id=g.id AND a.admission=p.admission
JOIN revora_guild_pk_maps f ON f.run=p.run AND f.map=p.map JOIN revora_guild_pk_matches m ON m.id=p.run
JOIN revora_guild_contest_owners o ON o.run=m.contest AND o.guild_id=g.id AND o.eligible
WHERE p.run=@run AND p.active AND p.deaths<20 AND p.guild_id IN(m.first_guild,m.second_guild)
AND a.joined_at<=@cutoff AND ((p.uid=@uid AND p.admission=@admission) OR (p.uid=@killer AND p.admission=@other))
ORDER BY p.uid FOR UPDATE OF p FOR SHARE OF e,g,a,o",
                    "run",run,"uid",(long)uid,"killer",(long)killer,"admission",admission,"other",killerAdmission,"cutoff",utc.AddDays(-7)))using(var r=q.ExecuteReader()) {
                    while(r.Read()) {
                        if(r.GetInt64(0)==uid && r.GetInt32(2)!=number-1)return false;
                        if(count==0)firstGuild=r.GetInt64(1);else different=firstGuild!=r.GetInt64(1);count++;
                    }
                }
                if(count!=2 || !different)return false;
                if(Q.Execute(c,t,"INSERT INTO revora_guild_pk_defeats(run,uid,number,killer,admission,killer_admission,defeated_at,killer_bp,victim_bp,exploits) VALUES(@run,@uid,@number,@killer,@admission,@other,@now,@killerbp,@victimbp,@exploits)",
                    "run",run,"uid",(long)uid,"number",number,"killer",(long)killer,"admission",admission,"other",killerAdmission,"now",utc,"killerbp",killerPower,"victimbp",victimPower,"exploits",Revora.Game.Features.GuildPkRules.KillExploits(killerPower,victimPower))!=1)
                    throw new InvalidOperationException("Guild PK defeat was not saved.");
                if(Q.Execute(c,t,"UPDATE revora_guild_pk_entries SET deaths=@number,active=(@number<20) WHERE run=@run AND uid=@uid AND active AND deaths=@previous",
                    "run",run,"uid",(long)uid,"number",number,"previous",number-1)!=1)throw new InvalidOperationException("Guild PK defeat ownership changed.");
                t.Commit();return true;
            }}
        }
    }
}
