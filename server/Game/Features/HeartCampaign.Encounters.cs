using System;
using Revora.Client;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static partial class HeartCampaign
    {
        private static void InitializeEncounters()
        {
            using(var c=Database.DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_heart_encounters(
id uuid PRIMARY KEY,run uuid NOT NULL REFERENCES revora_heart_runs(id),uid bigint NOT NULL CHECK(uid>0),
arena integer NOT NULL CHECK(arena BETWEEN 11000 AND 59999),started_at timestamptz NOT NULL,until_at timestamptz NOT NULL,
state varchar(9) NOT NULL DEFAULT 'active' CHECK(state IN('active','abandoned','complete')),
CHECK(until_at>started_at));
ALTER TABLE revora_heart_encounters ADD COLUMN IF NOT EXISTS returned boolean NOT NULL DEFAULT false;
CREATE UNIQUE INDEX IF NOT EXISTS revora_heart_active_owner ON revora_heart_encounters(uid) WHERE state='active';
CREATE UNIQUE INDEX IF NOT EXISTS revora_heart_pending_return ON revora_heart_encounters(uid) WHERE NOT returned;
CREATE UNIQUE INDEX IF NOT EXISTS revora_heart_active_arena ON revora_heart_encounters(arena) WHERE state='active';
CREATE TABLE IF NOT EXISTS revora_heart_actors(
id uuid PRIMARY KEY,encounter uuid NOT NULL REFERENCES revora_heart_encounters(id),
slot integer NOT NULL CHECK(slot BETWEEN 0 AND 11),UNIQUE(encounter,slot));");}
        }
        // Called only after the world coordinator reserves its dynamic arena.
        internal static Guid BeginEncounter(GameState p,Guid runId,int arena)
        {
            if(arena<11000 || arena>59999)return Guid.Empty;
            Guid id=Guid.NewGuid();
            bool saved=Mutate(p,runId,(c,t,run,now)=>{
                if(run.Mask==4095)return false;
                using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_heart_encounters WHERE (uid=@uid AND NOT returned) OR (arena=@arena AND state='active'))","uid",(long)p.Entity.UID,"arena",arena))
                    if(Equals(q.ExecuteScalar(),true))return false;
                DateTime end=PlanetWeekend.Current(now).EndUtc,until=now.AddMinutes(15);
                if(until>end)until=end;
                if(Q.Execute(c,t,@"INSERT INTO revora_heart_encounters(id,run,uid,arena,started_at,until_at)
VALUES(@id,@run,@uid,@arena,@now,@until)","id",id,"run",run.Id,"uid",(long)p.Entity.UID,"arena",arena,"now",now,"until",until)!=1)
                    throw new InvalidOperationException("Heart encounter reservation not saved.");
                return true;
            });
            return saved?id:Guid.Empty;
        }
        internal static Guid ActorIdentity(GameState p,Guid runId,Guid encounter,int slot)
        {
            if(slot<0 || slot>11 || encounter==Guid.Empty)return Guid.Empty;
            Guid id=Guid.Empty;
            bool saved=Mutate(p,runId,(c,t,run,now)=>{
                if((run.Mask&(1<<slot))!=0)return false;
                using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_heart_encounters
WHERE id=@id AND run=@run AND uid=@uid AND state='active' AND started_at<=@now AND until_at>@now)",
                    "id",encounter,"run",run.Id,"uid",(long)p.Entity.UID,"now",now))if(!Equals(q.ExecuteScalar(),true))return false;
                using(var q=Q.Command(c,t,"SELECT id FROM revora_heart_actors WHERE encounter=@encounter AND slot=@slot","encounter",encounter,"slot",slot)){
                    object existing=q.ExecuteScalar();if(existing!=null){id=(Guid)existing;return true;}
                }
                id=Guid.NewGuid();
                if(Q.Execute(c,t,"INSERT INTO revora_heart_actors(id,encounter,slot) VALUES(@id,@encounter,@slot)","id",id,"encounter",encounter,"slot",slot)!=1)
                    throw new InvalidOperationException("Heart actor identity not saved.");
                return true;
            });
            return saved?id:Guid.Empty;
        }
        internal static bool EndEncounter(uint uid,Guid encounter)
        {
            if(uid==0 || encounter==Guid.Empty)return false;
            using(var c=Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                string weekend;
                using(var q=Q.Command(c,t,"SELECT r.weekend FROM revora_heart_encounters e JOIN revora_heart_runs r ON r.id=e.run WHERE e.id=@id AND e.uid=@uid","id",encounter,"uid",(long)uid))weekend=q.ExecuteScalar() as string;
                if(weekend==null)return false;
                Q.Execute(c,t,"SELECT pg_advisory_xact_lock(6711,@key)","key",30000000+int.Parse(weekend));
                bool ended=Q.Execute(c,t,@"UPDATE revora_heart_encounters e
SET state=CASE WHEN r.mask=4095 THEN 'complete' ELSE 'abandoned' END FROM revora_heart_runs r
WHERE e.id=@id AND e.uid=@uid AND e.run=r.id AND e.state='active'","id",encounter,"uid",(long)uid)==1;
                if(!ended)using(var q=Q.Command(c,t,"SELECT state<>'active' FROM revora_heart_encounters WHERE id=@id AND uid=@uid","id",encounter,"uid",(long)uid))ended=Equals(q.ExecuteScalar(),true);
                t.Commit();return ended;
            }}
        }
        // Startup-only: the world coordinator must prove no owned scenes are live first.
        internal static int RecoverInterruptedEncounters()
        {
            using(var c=Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                // Cache credit commits before encounter close/return. A stopped process
                // may therefore leave a fully earned run attached to an active scene.
                int count=Q.Execute(c,t,@"UPDATE revora_heart_encounters e
SET state=CASE WHEN r.mask=4095 THEN 'complete' ELSE 'abandoned' END
FROM revora_heart_runs r WHERE e.run=r.id AND e.state='active'");
                using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_heart_encounters WHERE state='active')"))
                    if(Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Heart interrupted encounters did not close.");
                t.Commit();return count;
            }}
        }
    }
}
