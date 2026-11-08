using System;
using System.Linq;
using System.Threading;
using System.Runtime.CompilerServices;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features {
    internal static class ClanWarResults {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        private sealed class Probe {internal DateTime Last;}
        private static readonly ConditionalWeakTable<GameState,Probe> probes=new ConditionalWeakTable<GameState,Probe>();
        private static bool initialized;
        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_clan_wars(
run uuid PRIMARY KEY,arena integer NOT NULL CHECK(arena BETWEEN 0 AND 9),day integer NOT NULL,starts_at timestamptz NOT NULL,
state varchar(12) NOT NULL CHECK(state IN('open','complete','cancelled')),winner bigint NOT NULL DEFAULT 0,
cp bigint NOT NULL CHECK(cp BETWEEN 0 AND 4294967295),rvp bigint NOT NULL CHECK(rvp BETWEEN 0 AND 4294967295),ended_at timestamptz,
UNIQUE(arena,day));
CREATE TABLE IF NOT EXISTS revora_clan_war_entries(run uuid REFERENCES revora_clan_wars(run),uid bigint NOT NULL,clan bigint NOT NULL,
admitted boolean NOT NULL DEFAULT false,PRIMARY KEY(run,uid));
CREATE TABLE IF NOT EXISTS revora_clan_war_awards(run uuid REFERENCES revora_clan_wars(run),uid bigint NOT NULL,
cp bigint NOT NULL CHECK(cp BETWEEN 0 AND 4294967295),rvp bigint NOT NULL CHECK(rvp BETWEEN 0 AND 4294967295),paid_at timestamptz,PRIMARY KEY(run,uid));
ALTER TABLE revora_clan_war_awards ADD COLUMN IF NOT EXISTS soul_type integer NOT NULL DEFAULT 0 CHECK(soul_type=0 OR soul_type BETWEEN 800000 AND 899999);
ALTER TABLE revora_clan_war_awards ADD COLUMN IF NOT EXISTS soul_uid bigint UNIQUE CHECK((soul_uid IS NOT NULL)=(paid_at IS NOT NULL AND soul_type<>0));
CREATE INDEX IF NOT EXISTS revora_clan_war_unpaid ON revora_clan_war_awards(uid,run) WHERE paid_at IS NULL;
CREATE TABLE IF NOT EXISTS revora_clan_territories(arena integer PRIMARY KEY CHECK(arena BETWEEN 0 AND 9),clan bigint NOT NULL,
name varchar(64) NOT NULL,fund bigint NOT NULL,days bigint NOT NULL CHECK(days BETWEEN 1 AND 4294967295),run uuid NOT NULL REFERENCES revora_clan_wars(run));");}
            initialized=true;
        }
        internal static void CancelInterrupted() {
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,"UPDATE revora_clan_wars SET state='cancelled',ended_at=@now WHERE state='open'","now",Clock());}
        }
        internal static Guid Open(ClanWar.ClanArena arena,DateTime start,int day) {
            if(!initialized)throw new InvalidOperationException("Clan War persistence is not initialized.");
            Guid run=Guid.NewGuid();uint cp=Config.ClanWarCp,rvp=Config.ClanWarRevoraPoints;if(arena!=ClanWar.ClanArena.TwinCityClan){cp/=2;rvp/=2;}
            using(var c=DataHolder.MySqlConnection){c.Open();return Q.Execute(c,null,@"INSERT INTO revora_clan_wars(run,arena,day,starts_at,state,cp,rvp)
VALUES(@run,@arena,@day,@start,'open',@cp,@rvp) ON CONFLICT(arena,day) DO NOTHING","run",run,"arena",(int)arena,"day",day,"start",start,"cp",(long)cp,"rvp",(long)rvp)==1?run:Guid.Empty;}
        }
        internal static bool Register(Guid run,GameState p,Clan clan) {
            if(!Monitor.IsEntered(p.ItemSyncRoot))throw new InvalidOperationException("Clan entrant lock missing.");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                SkillTable.LockOwner(c,t,p);
                using(var q=Q.Command(c,t,"SELECT clanid FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))if(Convert.ToUInt32(q.ExecuteScalar())!=clan.ID)return false;
                using(var q=Q.Command(c,t,"SELECT identifier FROM clans WHERE identifier=@clan FOR UPDATE","clan",(long)clan.ID))if(q.ExecuteScalar()==null)return false;
                using(var q=Q.Command(c,t,"SELECT state='open' AND starts_at<=@now AND starts_at+INTERVAL '20 minutes'>@now FROM revora_clan_wars WHERE run=@run FOR UPDATE","run",run,"now",Clock()))if(!Equals(q.ExecuteScalar(),true))return false;
                Q.Execute(c,t,"INSERT INTO revora_clan_war_entries(run,uid,clan) VALUES(@run,@uid,@clan) ON CONFLICT DO NOTHING","run",run,"uid",(long)p.Entity.UID,"clan",(long)clan.ID);
                using(var q=Q.Command(c,t,"SELECT clan=@clan FROM revora_clan_war_entries WHERE run=@run AND uid=@uid","run",run,"uid",(long)p.Entity.UID,"clan",(long)clan.ID))if(!Equals(q.ExecuteScalar(),true))return false;
                t.Commit();return true;
            }}
        }
        internal static void Admit(Guid run,GameState p,uint clan) {
            using(var c=DataHolder.MySqlConnection){c.Open();if(Q.Execute(c,null,@"UPDATE revora_clan_war_entries e SET admitted=true FROM revora_clan_wars w
WHERE e.run=w.run AND e.run=@run AND e.uid=@uid AND e.clan=@clan AND w.state='open' AND w.starts_at+INTERVAL '20 minutes'>@now",
                "run",run,"uid",(long)p.Entity.UID,"clan",(long)clan,"now",Clock())!=1)throw new InvalidOperationException("Clan admission was not saved.");}
        }
        internal static bool Finish(Guid run,ClanWar.ClanArena arena,Clan winner,GameState[] players) {
            if(!Monitor.IsEntered(TournamentCombat.Gate) || !Monitor.IsEntered(Clan.RelationshipSync) || players.Any(p=>!Monitor.IsEntered(p.ItemSyncRoot)))throw new InvalidOperationException("Clan settlement locks missing.");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                foreach(var p in players.OrderBy(p=>p.Entity.UID))SkillTable.LockOwner(c,t,p);
                if(winner!=null)using(var q=Q.Command(c,t,"SELECT identifier FROM clans WHERE identifier=@clan FOR UPDATE","clan",(long)winner.ID))if(q.ExecuteScalar()==null)return false;
                long cp,rvp;using(var q=Q.Command(c,t,"SELECT state,cp,rvp,arena,starts_at+INTERVAL '20 minutes'<=@now FROM revora_clan_wars WHERE run=@run FOR UPDATE","run",run,"now",Clock()))
                using(var r=q.ExecuteReader()){if(!r.Read() || r.GetString(0)!="open" || r.GetInt32(3)!=(int)arena || !r.GetBoolean(4))return false;cp=r.GetInt64(1);rvp=r.GetInt64(2);}
                if(winner!=null){
                    if(players.Length==0 || players.Select(p=>p.Entity.UID).Distinct().Count()!=players.Length)throw new InvalidOperationException("Clan result has no unique participants.");
                    foreach(var p in players){
                        if(!Q.Player(p) || p.Entity.MapID!=ClanWar.GetMap(arena) || !ReferenceEquals(p.Entity.GetClan,winner))throw new InvalidOperationException("Clan result participant changed.");
                        int rank;using(var q=Q.Command(c,t,@"SELECT e.clanrank FROM entities e JOIN revora_clan_war_entries a ON a.uid=e.uid
WHERE a.run=@run AND a.admitted AND a.clan=@clan AND e.clanid=@clan AND e.uid=@uid","run",run,"clan",(long)winner.ID,"uid",(long)p.Entity.UID)){
                            object saved=q.ExecuteScalar();if(saved==null)throw new InvalidOperationException("Clan result lacks durable admission or membership.");rank=Convert.ToInt32(saved);}
                        long awardCp=rank==100?cp:cp/3,awardRvp=rank==100?rvp:rvp>1?rvp/3:rvp;
                        if(Q.Execute(c,t,"INSERT INTO revora_clan_war_awards(run,uid,cp,rvp,soul_type) VALUES(@run,@uid,@cp,@rvp,@soul)","run",run,"uid",(long)p.Entity.UID,"cp",awardCp,"rvp",awardRvp,"soul",(int)QuestSoulReward.Create().ID)!=1)throw new InvalidOperationException("Clan prize entitlement was not saved.");
                        string key=run.ToString("N");switch(arena){
                            case ClanWar.ClanArena.TwinCityClan:ProgressionEvidence.Complete(c,t,p.Entity.UID,10305,"clan-territory-v1",key,1);break;
                            case ClanWar.ClanArena.DesertCityClan:ProgressionEvidence.Complete(c,t,p.Entity.UID,10306,"clan-territory-v1",key,1);break;
                            case ClanWar.ClanArena.BirdCityClan:ProgressionEvidence.Complete(c,t,p.Entity.UID,10307,"clan-territory-v1",key,1);break;
                            case ClanWar.ClanArena.ApeCityClan:ProgressionEvidence.Complete(c,t,p.Entity.UID,10308,"clan-territory-v1",key,1);break;
                            case ClanWar.ClanArena.PhoenixCastleClan:ProgressionEvidence.Complete(c,t,p.Entity.UID,10309,"clan-territory-v1",key,1);break;
                        }
                    }
                    if(Q.Execute(c,t,@"INSERT INTO revora_clan_territories(arena,clan,name,fund,days,run) VALUES(@arena,@clan,@name,@fund,1,@run)
ON CONFLICT(arena) DO UPDATE SET clan=EXCLUDED.clan,name=EXCLUDED.name,fund=EXCLUDED.fund,run=EXCLUDED.run,
days=CASE WHEN revora_clan_territories.clan=EXCLUDED.clan THEN revora_clan_territories.days+1 ELSE 1 END",
                        "arena",(int)arena,"clan",(long)winner.ID,"name",winner.Name,"fund",(long)winner.Fund,"run",run)!=1)throw new InvalidOperationException("Clan occupation was not saved.");
                } else if(players.Length!=0)throw new InvalidOperationException("Cancelled clan result cannot pay participants.");
                if(Q.Execute(c,t,"UPDATE revora_clan_wars SET state=@state,winner=@winner,ended_at=@now WHERE run=@run AND state='open'",
                    "state",winner==null?"cancelled":"complete","winner",winner==null?0L:(long)winner.ID,"now",Clock(),"run",run)!=1)throw new InvalidOperationException("Clan result was not saved.");
                t.Commit();return true;
            }}
        }
        internal static void Load(ClanWar.ClanTournament[] tournaments) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT arena,clan,name,fund,days FROM revora_clan_territories"))
            using(var r=q.ExecuteReader())while(r.Read()){
                var tour=tournaments[r.GetInt32(0)];var view=tour.Client;view.UID=Convert.ToUInt32(r[1]);view.Name=r.GetString(2);view.Free=Convert.ToUInt32(r[3]);view.OccupationDays=Convert.ToUInt32(r[4]);
            }}
        }
        internal static bool Pay(GameState p) {
            if(!initialized || !Q.Player(p,false) || p.Trade==null || p.Trade.InTrade || p.Booth!=null)return false;
            uint uid=p.Entity.UID;if(!SessionInstall.TryEnter(uid))return false;
            lock(p.ItemSyncRoot)try{
                if(!Q.Player(p,false))return false;Guid run;uint nextCp,nextRvp;ConquerItem soul=null;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    SkillTable.LockOwner(c,t,p);long cp,rvp;uint soulType;
                    using(var q=Q.Command(c,t,"SELECT run,cp,rvp,soul_type FROM revora_clan_war_awards WHERE uid=@uid AND paid_at IS NULL ORDER BY run LIMIT 1 FOR UPDATE","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader()){if(!r.Read())return false;run=r.GetGuid(0);cp=r.GetInt64(1);rvp=r.GetInt64(2);soulType=checked((uint)r.GetInt32(3));}
                    if((long)p.Entity.ConquerPoints+cp>uint.MaxValue || (long)p.Entity.RevoraPoint+rvp>uint.MaxValue)return false;
                    if(soulType!=0) {
                        ConquerItemBaseInformation info;
                        if(!ConquerItemInformation.BaseInformations.TryGetValue(soulType,out info) || info.PurificationLevel!=7)throw new InvalidOperationException("Clan prize soul catalog changed.");
                        if(p.Inventory.Count>=40)return false;
                        using(var q=Q.Command(c,t,"SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0","uid",(long)uid))
                            if(Convert.ToInt64(q.ExecuteScalar())!=p.Inventory.Count)throw new InvalidOperationException("Clan prize inventory requires reload.");
                        soul=Q.New(soulType,true);Q.Insert(c,t,uid,soul);
                    }
                    nextCp=(uint)(p.Entity.ConquerPoints+cp);nextRvp=(uint)(p.Entity.RevoraPoint+rvp);
                    if(Q.Execute(c,t,"UPDATE entities SET conquerpoints=@cp,revorapoint=@rvp WHERE uid=@uid AND conquerpoints=@oldcp AND revorapoint=@oldrvp",
                        "uid",(long)p.Entity.UID,"cp",(long)nextCp,"rvp",(long)nextRvp,"oldcp",(long)p.Entity.ConquerPoints,"oldrvp",(long)p.Entity.RevoraPoint)!=1)throw new InvalidOperationException("Clan prize balances changed.");
                    if(Q.Execute(c,t,"UPDATE revora_clan_war_awards SET paid_at=@now,soul_uid=@soul WHERE run=@run AND uid=@uid AND paid_at IS NULL","run",run,"uid",(long)p.Entity.UID,"now",Clock(),"soul",soul==null?(object)DBNull.Value:(long)soul.UID)!=1)throw new InvalidOperationException("Clan payment receipt was not saved.");
                    if(!Q.Player(p,false) || p.Trade.InTrade || p.Booth!=null)throw new InvalidOperationException("Clan prize session changed.");
                    t.Commit();
                }}
                p.Entity.ApplyCommittedConquerPoints(nextCp);p.Entity.ApplyCommittedRevoraPoints(nextRvp);if(soul!=null)Q.Install(p,new uint[0],new[]{soul});Q.Refresh(p);p.Send("Your Clan War purse has arrived. You fought well.");return true;
            }catch(Exception ex){Q.Fail(p,"clan-war-prize",ex);return false;}
            finally{SessionInstall.Exit(uid);}
        }
        internal static void Tick(GameState p) {
            if(!initialized || !Q.Player(p,false) || !Monitor.TryEnter(p.ItemSyncRoot))return;
            try{var now=Clock();var probe=probes.GetOrCreateValue(p);if(probe.Last!=default(DateTime) && now>=probe.Last && now-probe.Last<TimeSpan.FromSeconds(30))return;probe.Last=now;
                bool pending;using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT EXISTS(SELECT 1 FROM revora_clan_war_awards WHERE uid=@uid AND paid_at IS NULL)","uid",(long)p.Entity.UID))pending=Equals(q.ExecuteScalar(),true);}
                if(pending)Pay(p);ClanWar.RecoverLocation(p);
            }catch(Exception ex){System.Console.WriteLine("[clan-war] delivery-probe uid="+p.Entity.UID+" error="+ex.GetType().Name);}
            finally{Monitor.Exit(p.ItemSyncRoot);}
        }
    }
}
