using System;
using Npgsql;
using Revora.Client;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Durable campaign state shared by the ordinary NPC, owned encounters and prize delivery.
    internal static partial class HeartCampaign
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal sealed class Run
        {
            internal Guid Id;
            internal string Weekend,Faction;
            internal int Ordinal,Mask;
            internal bool Delivered;
        }
        internal static void Initialize()
        {
            using(var c=Database.DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_heart_weekends(
weekend varchar(8) PRIMARY KEY,starts_at timestamptz NOT NULL,ends_at timestamptz NOT NULL,closed boolean NOT NULL DEFAULT false,
CHECK(ends_at>starts_at));
CREATE TABLE IF NOT EXISTS revora_heart_entries(
weekend varchar(8) NOT NULL REFERENCES revora_heart_weekends(weekend),uid bigint NOT NULL CHECK(uid>0),
faction varchar(7) NOT NULL CHECK(faction IN('couples','singles')),accepted_at timestamptz NOT NULL,
PRIMARY KEY(weekend,uid));
CREATE TABLE IF NOT EXISTS revora_heart_runs(
id uuid PRIMARY KEY,weekend varchar(8) NOT NULL,uid bigint NOT NULL,ordinal integer NOT NULL CHECK(ordinal BETWEEN 1 AND 3),
mask integer NOT NULL DEFAULT 0 CHECK(mask BETWEEN 0 AND 4095),started_at timestamptz NOT NULL,delivered_at timestamptz,
UNIQUE(weekend,uid,ordinal),FOREIGN KEY(weekend,uid) REFERENCES revora_heart_entries(weekend,uid),
CHECK(delivered_at IS NULL OR mask=4095));
CREATE TABLE IF NOT EXISTS revora_heart_objectives(
run uuid NOT NULL REFERENCES revora_heart_runs(id),slot integer NOT NULL CHECK(slot BETWEEN 0 AND 11),
actor uuid NOT NULL UNIQUE,occurred_at timestamptz NOT NULL,PRIMARY KEY(run,slot));");}
            InitializeEncounters();
            InitializeResults();
        }
        private static bool Ready(GameState p){return Q.Player(p) && p.Booth==null && p.Trade!=null && !p.Trade.InTrade;}
        private static bool LockWindow(NpgsqlConnection c,NpgsqlTransaction t,PlanetWeekend.Window w,DateTime now)
        {
            // All mutations and eventual closing share this lock; character locks come second.
            Q.Execute(c,t,"SELECT pg_advisory_xact_lock(6711,@key)","key",30000000+int.Parse(w.Key));
            Q.Execute(c,t,@"INSERT INTO revora_heart_weekends(weekend,starts_at,ends_at) VALUES(@weekend,@start,@end) ON CONFLICT DO NOTHING",
                "weekend",w.Key,"start",w.StartUtc,"end",w.EndUtc);
            using(var q=Q.Command(c,t,@"SELECT NOT closed AND starts_at=@start AND ends_at=@end FROM revora_heart_weekends WHERE weekend=@weekend FOR UPDATE",
                "weekend",w.Key,"start",w.StartUtc,"end",w.EndUtc))
                return Equals(q.ExecuteScalar(),true) && now>=w.StartUtc && now<w.EndUtc && Clock()>=w.StartUtc && Clock()<w.EndUtc;
        }
        private static Run Read(NpgsqlConnection c,NpgsqlTransaction t,uint uid,string weekend)
        {
            using(var q=Q.Command(c,t,@"SELECT r.id,r.ordinal,r.mask,r.delivered_at IS NOT NULL,e.faction FROM revora_heart_runs r
JOIN revora_heart_entries e USING(weekend,uid) WHERE r.uid=@uid AND r.weekend=@weekend ORDER BY r.ordinal DESC LIMIT 1 FOR UPDATE OF r",
                "uid",(long)uid,"weekend",weekend))
            using(var r=q.ExecuteReader())return !r.Read()?null:new Run{Id=r.GetGuid(0),Ordinal=r.GetInt32(1),Mask=r.GetInt32(2),Delivered=r.GetBoolean(3),Faction=r.GetString(4),Weekend=weekend};
        }
        internal static Run Resume(GameState p)
        {
            if(!Ready(p))return null;
            lock(p.ItemSyncRoot){
                var w=PlanetWeekend.Current(Clock());if(w==null)return null;
                using(var c=Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    var run=Read(c,t,p.Entity.UID,w.Key);t.Commit();return run;
                }}
            }
        }
        internal static Run Enroll(GameState p,string faction)
        {
            if(!Ready(p) || (faction!="couples" && faction!="singles"))return null;
            lock(p.ItemSyncRoot)
            {
                if(!Ready(p))return null;
                DateTime now=Clock();var w=PlanetWeekend.Current(now);if(w==null)return null;
                using(var c=Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    if(!LockWindow(c,t,w,now))return null;
                    using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(q.ExecuteScalar()==null)return null;
                    Q.Execute(c,t,@"INSERT INTO revora_heart_entries(weekend,uid,faction,accepted_at) VALUES(@weekend,@uid,@faction,@now) ON CONFLICT DO NOTHING",
                        "weekend",w.Key,"uid",(long)p.Entity.UID,"faction",faction,"now",now);
                    using(var q=Q.Command(c,t,"SELECT faction FROM revora_heart_entries WHERE weekend=@weekend AND uid=@uid","weekend",w.Key,"uid",(long)p.Entity.UID))
                        if(!Equals(q.ExecuteScalar(),faction))return null;
                    var run=Read(c,t,p.Entity.UID,w.Key);
                    if(run!=null && !run.Delivered){t.Commit();return run;}
                    if(run!=null && run.Ordinal==3)return null;
                    Guid id=Guid.NewGuid();int ordinal=run==null?1:run.Ordinal+1;
                    if(Q.Execute(c,t,"INSERT INTO revora_heart_runs(id,weekend,uid,ordinal,started_at) VALUES(@id,@weekend,@uid,@ordinal,@now)",
                        "id",id,"weekend",w.Key,"uid",(long)p.Entity.UID,"ordinal",ordinal,"now",now)!=1)throw new InvalidOperationException("Heart sortie not saved.");
                    run=Read(c,t,p.Entity.UID,w.Key);
                    if(run==null || run.Id!=id)throw new InvalidOperationException("Heart sortie receipt missing.");
                    if(!Ready(p) || Clock()>=w.EndUtc)return null;
                    t.Commit();return run;
                }}
            }
        }
        // Server-only seam: actor UUID must be owned by this run and minted at spawn.
        // Only owned encounter callbacks supply objective identities.
        internal static bool Record(GameState p,Guid runId,int slot,Guid actor,DateTime occurred)
        {
            if(slot<0 || slot>11 || actor==Guid.Empty || occurred.Kind!=DateTimeKind.Utc)return false;
            return Mutate(p,runId,(c,t,run,now)=>{
                if(occurred>now || occurred<PlanetWeekend.Current(now).StartUtc)return false;
                using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_heart_actors a
JOIN revora_heart_encounters e ON e.id=a.encounter WHERE a.id=@actor AND e.run=@run AND e.uid=@uid AND a.slot=@slot
AND e.state='active' AND e.started_at<=@at AND e.until_at>@now)",
                    "actor",actor,"run",run.Id,"uid",(long)p.Entity.UID,"slot",slot,"at",occurred,"now",now))
                    if(!Equals(q.ExecuteScalar(),true))return false;
                using(var q=Q.Command(c,t,"SELECT started_at<=@at FROM revora_heart_runs WHERE id=@id",
                    "at",occurred,"id",run.Id))if(!Equals(q.ExecuteScalar(),true))return false;
                int bit=1<<slot;
                if((run.Mask&bit)!=0 || (slot==10 && run.Mask!=1023) || (slot==11 && run.Mask!=2047))return false;
                if(Q.Execute(c,t,"INSERT INTO revora_heart_objectives(run,slot,actor,occurred_at) VALUES(@run,@slot,@actor,@at) ON CONFLICT DO NOTHING",
                    "run",run.Id,"slot",slot,"actor",actor,"at",occurred)!=1)return false;
                if(Q.Execute(c,t,"UPDATE revora_heart_runs SET mask=@mask WHERE id=@id AND mask=@before AND delivered_at IS NULL",
                    "mask",run.Mask|bit,"id",run.Id,"before",run.Mask)!=1)throw new InvalidOperationException("Heart objective not saved.");
                return true;
            });
        }
        internal static bool Deliver(GameState p,Guid runId)
        {
            return Mutate(p,runId,(c,t,run,now)=>{
                if(run.Mask!=4095)return false;
                if(Q.Execute(c,t,"UPDATE revora_heart_runs SET delivered_at=@now WHERE id=@id AND delivered_at IS NULL AND mask=4095",
                    "now",now,"id",run.Id)!=1)throw new InvalidOperationException("Heart delivery not saved.");
                return true;
            });
        }
        private static bool Mutate(GameState p,Guid id,Func<NpgsqlConnection,NpgsqlTransaction,Run,DateTime,bool> change)
        {
            if(!Ready(p) || id==Guid.Empty)return false;
            lock(p.ItemSyncRoot){
                if(!Ready(p))return false;
                DateTime now=Clock();var w=PlanetWeekend.Current(now);if(w==null)return false;
                using(var c=Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    if(!LockWindow(c,t,w,now))return false;
                    var run=Read(c,t,p.Entity.UID,w.Key);
                    if(run==null || run.Id!=id || run.Delivered || !change(c,t,run,now))return false;
                    if(!Ready(p) || Clock()>=w.EndUtc)return false;
                    t.Commit();return true;
                }}
            }
        }
    }
}
