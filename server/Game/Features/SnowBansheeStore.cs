using System;
using System.Linq;
using System.Threading;
using System.IO;
using System.Security.Cryptography;
using Npgsql;
using Revora.Database;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Inactive persistence participant. An encounter coordinator must own actor,
    // combat, killer/team eligibility and reward authorization before activation.
    internal static class SnowBansheeStore
    {
        internal const ushort MapId=1927,SpawnX=370,SpawnY=365;
        internal const uint Health=100000000;
        internal static readonly object Gate=new object();
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal sealed class Run {
            internal readonly Guid Id,Lease;
            internal readonly uint Hp;
            internal readonly ushort X,Y;
            internal readonly long Sequence;
            internal Run(Guid id,Guid lease,uint hp,ushort x,ushort y,long sequence) {
                Id=id;Lease=lease;Hp=hp;X=x;Y=y;Sequence=sequence;
            }
        }
        private static DateTime Now() {
            var now=Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Snow Banshee clock requires UTC.");return now;
        }
        private static void Locked() {
            if(!Monitor.IsEntered(Gate))throw new InvalidOperationException("Snow Banshee lifecycle lock is required.");
        }
        internal static bool Site(Map map,ushort x,ushort y) {
            Map current;MapsTable.MapInformation info;
            long dx=(long)x-SpawnX,dy=(long)y-SpawnY;
            return map!=null && map.ID==MapId && map.BaseID==MapId && map.Floor!=null
                && Kernel.Maps.TryGetValue(MapId,out current) && ReferenceEquals(current,map)
                && MapsTable.MapInformations.TryGetValue(MapId,out info) && info.ID==MapId && info.BaseID==MapId && info.Owner==0
                && dx*dx+dy*dy<=24*24 && x<map.Floor.Bounds.Width && y<map.Floor.Bounds.Height
                && map.Floor[x,y,MapObjectType.Player];
        }
        internal static void Initialize(Map map) {
            Locked();
            using(var hash=SHA256.Create())using(var file=File.OpenRead(Path.Combine(Constants.DMapsPath,"maps","1927.map")))
                if(System.BitConverter.ToString(hash.ComputeHash(file)).Replace("-","")!="5F4B3F51A8A022C217CF553909D96583D05D489953128A4FBF599B088611CBCE")
                    throw new InvalidOperationException("Snow Banshee terrain changed.");
            if(!Site(map,SpawnX,SpawnY))throw new InvalidOperationException("Snow Banshee public world is unavailable.");
            var now=Now();
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Q.Execute(c,t,@"CREATE TABLE IF NOT EXISTS revora_snow_banshee_runs(
id uuid PRIMARY KEY,lease uuid NOT NULL,state varchar(8) NOT NULL CHECK(state IN('active','victory')),
hp bigint NOT NULL CHECK(hp BETWEEN 0 AND 100000000),x integer NOT NULL,y integer NOT NULL,
sequence bigint NOT NULL DEFAULT 0 CHECK(sequence>=0),started_at timestamptz NOT NULL,ended_at timestamptz,
killer bigint,party bigint[],
CHECK((x-370)::bigint*(x-370)+(y-365)::bigint*(y-365)<=576),
CHECK((state='active' AND hp>0 AND ended_at IS NULL AND killer IS NULL AND party IS NULL)
 OR (state='victory' AND hp=0 AND ended_at IS NOT NULL AND killer>0 AND killer IS NOT NULL
 AND party IS NOT NULL AND cardinality(party) BETWEEN 1 AND 5 AND killer=ANY(party))));
CREATE UNIQUE INDEX IF NOT EXISTS revora_snow_banshee_one_active ON revora_snow_banshee_runs((true)) WHERE state='active';
CREATE TABLE IF NOT EXISTS revora_snow_banshee_schedule(
id integer PRIMARY KEY CHECK(id=1),next_spawn_at timestamptz NOT NULL,active_run uuid REFERENCES revora_snow_banshee_runs(id));
INSERT INTO revora_snow_banshee_schedule(id,next_spawn_at) VALUES(1,@now) ON CONFLICT(id) DO NOTHING;","now",now);
                t.Commit();
            }}
        }
        private static Guid? Schedule(NpgsqlConnection c,NpgsqlTransaction t,out DateTime due) {
            // Legacy Npgsql materializes timestamptz in the machine's local zone.
            // Ask PostgreSQL for UTC wall time before attaching DateTimeKind.Utc.
            using(var q=Q.Command(c,t,"SELECT active_run,next_spawn_at AT TIME ZONE 'UTC' FROM revora_snow_banshee_schedule WHERE id=1 FOR UPDATE"))
            using(var r=q.ExecuteReader()) {
                if(!r.Read())throw new InvalidOperationException("Snow Banshee schedule is missing.");
                due=DateTime.SpecifyKind(r.GetDateTime(1),DateTimeKind.Utc);return r.IsDBNull(0)?(Guid?)null:r.GetGuid(0);
            }
        }
        private static void Context(Map map,ushort x,ushort y,Func<bool> proof) {
            if(proof==null || !Site(map,x,y) || !proof())throw new InvalidOperationException("Snow Banshee world ownership changed.");
        }
        // Call only when starting/reconstructing an actor. A new lease invalidates
        // every callback from the previous actor; same-lease reads are idempotent.
        internal static Run Acquire(Map map,Guid lease,Func<bool> proof) {
            Locked();if(lease==Guid.Empty)throw new ArgumentException("Snow Banshee actor lease is required.");
            Context(map,SpawnX,SpawnY,proof);var now=Now();
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                DateTime due;var active=Schedule(c,t,out due);Run run;
                if(active.HasValue) {
                    using(var q=Q.Command(c,t,"SELECT lease,hp,x,y,sequence,state FROM revora_snow_banshee_runs WHERE id=@id FOR UPDATE","id",active.Value))
                    using(var r=q.ExecuteReader()) {
                        if(!r.Read() || r.GetString(5)!="active")throw new InvalidOperationException("Snow Banshee active pointer is inconsistent.");
                        run=new Run(active.Value,lease,Convert.ToUInt32(r[1]),Convert.ToUInt16(r[2]),Convert.ToUInt16(r[3]),r.GetGuid(0)==lease?r.GetInt64(4):0);
                    }
                    if(Q.Execute(c,t,"UPDATE revora_snow_banshee_runs SET lease=@lease,sequence=@seq WHERE id=@id AND state='active'",
                        "lease",lease,"seq",run.Sequence,"id",run.Id)!=1)throw new InvalidOperationException("Snow Banshee recovery was not saved.");
                } else {
                    if(now<due)return null;
                    run=new Run(Guid.NewGuid(),lease,Health,SpawnX,SpawnY,0);
                    if(Q.Execute(c,t,@"INSERT INTO revora_snow_banshee_runs(id,lease,state,hp,x,y,started_at)
VALUES(@id,@lease,'active',@hp,@x,@y,@now)","id",run.Id,"lease",lease,"hp",(long)Health,"x",(int)SpawnX,"y",(int)SpawnY,"now",now)!=1)
                        throw new InvalidOperationException("Snow Banshee run was not saved.");
                    if(Q.Execute(c,t,"UPDATE revora_snow_banshee_schedule SET active_run=@id WHERE id=1 AND active_run IS NULL","id",run.Id)!=1)
                        throw new InvalidOperationException("Snow Banshee spawn was not scheduled.");
                }
                Context(map,run.X,run.Y,proof);t.Commit();return run;
            }}
        }
        internal static bool Checkpoint(Map map,Run run,long sequence,uint hp,ushort x,ushort y,Func<bool> proof) {
            Locked();if(run==null || sequence<=0 || hp==0 || hp>Health)throw new ArgumentException("Invalid Snow Banshee checkpoint.");
            Context(map,x,y,proof);
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                DateTime due;if(Schedule(c,t,out due)!=run.Id)return false;
                int changed=Q.Execute(c,t,@"UPDATE revora_snow_banshee_runs SET hp=@hp,x=@x,y=@y,sequence=@seq
WHERE id=@id AND lease=@lease AND state='active' AND sequence<@seq", "hp",(long)hp,"x",(int)x,"y",(int)y,
                    "seq",sequence,"id",run.Id,"lease",run.Lease);
                if(changed!=1)return false;Context(map,x,y,proof);t.Commit();return true;
            }}
        }
        // Caller supplies already-authorized frozen participants. This method is
        // neither a player handler nor a reward grant. Commit together with all
        // reward/spirit receipts; do not retire the actor before that commit.
        internal static bool Victory(NpgsqlConnection c,NpgsqlTransaction t,Map map,Run run,uint killer,uint[] party,ushort x,ushort y,Func<bool> proof) {
            Locked();if(t==null || run==null || killer==0 || party==null || party.Length<1 || party.Length>5
                || party.Any(id=>id==0) || party.Distinct().Count()!=party.Length || !party.Contains(killer))
                throw new ArgumentException("Invalid Snow Banshee victory participant.");
            Context(map,x,y,proof);var now=Now();DateTime due;if(Schedule(c,t,out due)!=run.Id)return false;
            if(Q.Execute(c,t,@"UPDATE revora_snow_banshee_runs SET state='victory',hp=0,x=@x,y=@y,killer=@killer,party=@party,ended_at=@now
WHERE id=@id AND lease=@lease AND state='active'","x",(int)x,"y",(int)y,"killer",(long)killer,
                "party",party.OrderBy(id=>id).Select(id=>(long)id).ToArray(),"now",now,"id",run.Id,"lease",run.Lease)!=1)return false;
            if(Q.Execute(c,t,"UPDATE revora_snow_banshee_schedule SET active_run=NULL,next_spawn_at=@next WHERE id=1 AND active_run=@id",
                "next",now.AddMinutes(30),"id",run.Id)!=1)throw new InvalidOperationException("Snow Banshee cooldown was not saved.");
            Context(map,x,y,proof);return true;
        }
    }
}
