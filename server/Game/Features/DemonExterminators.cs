using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Npgsql;
using NpgsqlTypes;
using Revora.Client;
using Revora.Database;
using Revora.Interfaces;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    internal static class DemonExterminators
    {
        internal const uint NpcId=2000, JarId=750000, RewardId=722136;
        internal static readonly int[] Needs={600,1200,1500,1800};
        private static readonly uint[] Targets={18,20,57,58};
        private static readonly string[] Names={"Birdmen on Bird Island","Tomb Bats in Mystic Castle","Bull Monsters in Mystic Castle","Red Devils in Mystic Castle"};
        private static readonly TimeZoneInfo Zone=TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        private static readonly ConcurrentDictionary<uint,string> active=new ConcurrentDictionary<uint,string>();
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        private static bool enabled;
        private sealed class Progress { internal int Stage,Kills;internal uint Jar,RuneReward;internal string State; }

        internal static string Day(DateTime utc) {
            if(utc.Kind!=DateTimeKind.Utc)throw new ArgumentException("Demon hunt clock requires UTC.");
            return TimeZoneInfo.ConvertTimeFromUtc(utc,Zone).ToString("yyyyMMdd",CultureInfo.InvariantCulture);
        }
        private static NpgsqlCommand Command(NpgsqlConnection c,NpgsqlTransaction t,string sql,params object[] values) {
            var q=new NpgsqlCommand(sql,c,t);
            for(int i=0;i<values.Length;i+=2) {
                if(values[i+1] is DateTime)q.Parameters.Add((string)values[i],NpgsqlDbType.TimestampTz).Value=values[i+1];
                else q.Parameters.AddWithValue((string)values[i],values[i+1]);
            }
            return q;
        }
        private static int Execute(NpgsqlConnection c,NpgsqlTransaction t,string sql,params object[] values) {
            using(var q=Command(c,t,sql,values))return q.ExecuteNonQuery();
        }
        private static Progress Read(NpgsqlConnection c,NpgsqlTransaction t,uint uid,string day) {
            using(var q=Command(c,t,"SELECT stage,kills,jar_uid,state,rune_reward_id FROM revora_demon_runs WHERE uid=@uid AND day=@day FOR UPDATE","uid",(long)uid,"day",day))
            using(var r=q.ExecuteReader())return r.Read()?new Progress{Stage=r.GetInt32(0),Kills=r.GetInt32(1),Jar=Convert.ToUInt32(r[2]),State=r.GetString(3),RuneReward=Convert.ToUInt32(r[4])}:null;
        }
        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection) { c.Open();using(var t=c.BeginTransaction()) {
                Execute(c,t,@"CREATE TABLE IF NOT EXISTS revora_demon_runs(
uid bigint NOT NULL CHECK(uid>0),day varchar(8) NOT NULL,stage integer NOT NULL CHECK(stage BETWEEN 0 AND 3),
kills integer NOT NULL CHECK(kills>=0 AND kills<=CASE stage WHEN 0 THEN 600 WHEN 1 THEN 1200 WHEN 2 THEN 1500 ELSE 1800 END),
jar_uid bigint NOT NULL CHECK(jar_uid>0),state varchar(16) NOT NULL CHECK(state IN ('active','complete','abandoned')),
reward_ids bigint[] NOT NULL DEFAULT '{}',completed_at timestamptz,PRIMARY KEY(uid,day));
CREATE TABLE IF NOT EXISTS revora_demon_kill_credits(life uuid NOT NULL,uid bigint NOT NULL,day varchar(8) NOT NULL,PRIMARY KEY(life,uid));
CREATE INDEX IF NOT EXISTS revora_demon_day ON revora_demon_runs(day,state);
ALTER TABLE revora_demon_runs ADD COLUMN IF NOT EXISTS rune_reward_id bigint NOT NULL DEFAULT 0 CHECK(rune_reward_id=0 OR (rune_reward_id BETWEEN 4030001 AND 4039901 AND rune_reward_id%100=1));");
                active.Clear();string day=Day(Clock());
                using(var q=Command(c,t,"SELECT uid FROM revora_demon_runs WHERE day=@day AND state='active'","day",day))
                using(var r=q.ExecuteReader())while(r.Read())active[Convert.ToUInt32(r[0])]=day;
                t.Commit();enabled=true;
            }}
        }
        internal static void PlaceNpc() {
            Map market=Kernel.Maps[1036];ushort mesh;
            using(var c=DataHolder.MySqlConnection) { c.Open();using(var q=Command(c,null,"SELECT lookface FROM npcs WHERE id=@id","id",(long)NpcId)) {
                object value=q.ExecuteScalar();if(value==null)throw new InvalidOperationException("Installed Cloud Saint is missing.");mesh=Convert.ToUInt16(value);
            }}
            foreach(var map in Kernel.Maps.Values.ToArray()) { INpc old;if(map.Npcs!=null && map.Npcs.TryGetValue(NpcId,out old))map.RemoveNpc(old); }
            ushort x=206,y=213;bool found=false;
            for(int radius=0;radius<=8 && !found;radius++)
                for(int dx=-radius;dx<=radius && !found;dx++)for(int dy=-radius;dy<=radius && !found;dy++) {
                    int tx=206+dx,ty=213+dy;
                    if(!market.Floor[tx,ty,MapObjectType.Player] || market.Npcs.Values.Any(n=>n.X==tx && n.Y==ty))continue;
                    x=(ushort)tx;y=(ushort)ty;found=true;
                }
            if(!found)throw new InvalidOperationException("Cloud Saint landing unavailable.");
            CityGuardSystem.SpawnTalker(NpcId,1036,x,y,mesh,"CloudSaint");
            Console.WriteLine("[demon-hunt] Cloud Saint map=1036 x="+x+" y="+y);
        }
        private static bool Ordinary(GameState p) {
            GameState current;
            return p!=null && !p.Fake && p.Entity!=null && p.Action==2 && p.Entity.FullyLoaded
                && !p.Entity.Dead && !p.Entity.PersistenceRequiresReload && p.ItemSyncRoot!=null && p.Inventory!=null
                && Kernel.GamePool.TryGetValue(p.Entity.UID,out current) && ReferenceEquals(current,p);
        }
        private static bool Near(GameState p) {
            INpc npc;return Ordinary(p) && p.ActiveNpc==NpcId && p.Entity.MapID==1036 && p.Booth==null
                && p.Trade!=null && !p.Trade.InTrade && p.Map.Npcs.TryGetValue(NpcId,out npc)
                && Kernel.GetDistance(p.Entity.X,p.Entity.Y,npc.X,npc.Y)<=6;
        }
        private static ConquerItem NewItem(uint id,ushort target) {
            var info=ConquerItemInformation.BaseInformations[id];
            return new ConquerItem(true){UID=ConquerItem.ItemUID.Next,ID=id,Bound=true,StackSize=1,
                Durability=id==JarId?(ushort)1:info.Durability,MaximDurability=id==JarId?target:info.Durability};
        }
        private static void InsertItem(NpgsqlConnection c,NpgsqlTransaction t,GameState p,ConquerItem item) {
            Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability)
VALUES(@id,@type,@owner,0,0,1,1,@durability,@maximum)","id",(long)item.UID,"type",(long)item.ID,"owner",(long)p.Entity.UID,
                "durability",(int)item.Durability,"maximum",(int)item.MaximDurability);
        }
        private static void Install(GameState p,ConquerItem item) {
            if(!p.Inventory.Add(item,Enums.ItemUse.None))throw new InvalidOperationException("Committed hunt item requires reload.");
            item.Send(p);
        }
        private static bool HasJar(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Progress s,bool checkCache=true) {
            ConquerItem item;
            if(checkCache && (!p.Inventory.TryGetItem(s.Jar,out item) || item.ID!=JarId || !item.Bound || item.Position!=0))return false;
            using(var q=Command(c,t,"SELECT EXISTS(SELECT 1 FROM items WHERE uid=@jar AND id=@type AND entityid=@uid AND bound=1 AND position=0 AND warehouse=0)",
                "jar",(long)s.Jar,"type",(long)JarId,"uid",(long)p.Entity.UID))return Equals(q.ExecuteScalar(),true);
        }
        private static void Projection(GameState p,Progress s) {
            bool live=s!=null && s.State=="active";
            p.Entity.Status4=live?(uint)s.Kills:0;p.Entity.kilid=live?Targets[s.Stage]:0;
        }
        private static void Failure(GameState p,Exception error,bool mutation) {
            Console.WriteLine("[demon-hunt] operation-failed uid="+p.Entity.UID+" error="+error.GetType().Name
                +" inner="+(error.InnerException==null?"none":error.InnerException.GetType().Name));
            if(mutation) { p.Entity.PersistenceRequiresReload=true;p.Action=0;p.Send("Please reconnect before continuing your hunt."); }
        }
        internal static uint JarCount(GameState p) {
            if(!enabled || !Ordinary(p))return 0;
            lock(p.ItemSyncRoot)try {
                if(!Ordinary(p))return 0;
                using(var c=DataHolder.MySqlConnection) { c.Open();using(var t=c.BeginTransaction()) {
                    string day=Day(Clock());var s=Read(c,t,p.Entity.UID,day);
                    uint result=s!=null && s.State=="active" && HasJar(c,t,p,s)?(uint)s.Kills:0;
                    t.Commit();string ignored;
                    if(s!=null && s.State=="active")active[p.Entity.UID]=day;else active.TryRemove(p.Entity.UID,out ignored);
                    Projection(p,s);return result;
                }}
            } catch(Exception ex) { Failure(p,ex,false);return 0; }
        }
        internal static void OnLogin(GameState p) { if(enabled)JarCount(p); }

        private static string Accept(GameState p,string day) {
            if(p.Entity.Level<120)return "Return when you have reached level 120.";
            using(var c=DataHolder.MySqlConnection) { c.Open();using(var t=c.BeginTransaction()) {
                // Also serializes admission before today's quest row exists.
                using(var q=Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                    if(q.ExecuteScalar()==null)throw new InvalidOperationException("Missing hunt character.");
                var s=Read(c,t,p.Entity.UID,day);if(s!=null)return "You have already taken today's hunt. Return tomorrow for another.";
                var old=new List<uint>();
                using(var q=Command(c,t,@"SELECT i.uid FROM items i JOIN revora_demon_runs r ON r.jar_uid=i.uid AND r.uid=i.entityid
WHERE r.uid=@uid AND r.day<>@day AND i.id=@jar AND i.position=0 AND i.warehouse=0", "uid",(long)p.Entity.UID,"day",day,"jar",(long)JarId))
                using(var r=q.ExecuteReader())while(r.Read())old.Add(Convert.ToUInt32(r[0]));
                if(old.Any(id=>!p.Inventory.Objects.Any(i=>i.UID==id)))throw new InvalidOperationException("Hunt inventory requires reload.");
                if(p.Inventory.Count-old.Count>=40)return "Make room for a soul jar, then return.";
                foreach(uint id in old)Execute(c,t,"DELETE FROM items WHERE uid=@id AND entityid=@uid","id",(long)id,"uid",(long)p.Entity.UID);
                var pool=ConquerItemInformation.BaseInformations.Values.Where(i=>i.ID/10000==403 && i.ID%100==1 && i.Class==0).Select(i=>i.ID).OrderBy(i=>i).ToArray();
                if(pool.Length==0)throw new InvalidOperationException("Demon hunt yellow rune catalog missing.");
                uint rune=pool[Kernel.Random.Next(pool.Length)];
                var jar=NewItem(JarId,(ushort)Targets[0]);InsertItem(c,t,p,jar);
                Execute(c,t,"INSERT INTO revora_demon_runs(uid,day,stage,kills,jar_uid,state,rune_reward_id) VALUES(@uid,@day,0,0,@jar,'active',@rune)",
                    "uid",(long)p.Entity.UID,"day",day,"jar",(long)jar.UID,"rune",(long)rune);
                t.Commit();foreach(uint id in old)
                    if(!p.Inventory.Remove(id,Enums.ItemUse.None,true))throw new InvalidOperationException("Expired jar removal requires reload.");
                Install(p,jar);active[p.Entity.UID]=day;Projection(p,new Progress{State="active"});
                return "Fill this jar with 600 souls from Birdmen on Bird Island, then bring it to me.";
            }}
        }
        private static string TurnIn(GameState p,string day,bool abandon) {
            using(var c=DataHolder.MySqlConnection) { c.Open();using(var t=c.BeginTransaction()) {
                var s=Read(c,t,p.Entity.UID,day);
                if(s==null || s.State!="active")return "There is no unfinished hunt to turn in today.";
                if(!HasJar(c,t,p,s))return "Bring your soul jar in your bag.";
                if(!abandon && s.Kills<Needs[s.Stage])return ProgressText(s);
                if(!abandon && s.Stage<3) {
                    s.Stage++;s.Kills=0;
                    Execute(c,t,"UPDATE revora_demon_runs SET stage=@stage,kills=0 WHERE uid=@uid AND day=@day","stage",s.Stage,"uid",(long)p.Entity.UID,"day",day);
                    Execute(c,t,"UPDATE items SET maximdurability=@target WHERE uid=@jar AND entityid=@uid",
                        "target",(int)Targets[s.Stage],"jar",(long)s.Jar,"uid",(long)p.Entity.UID);
                    t.Commit();ConquerItem jar;p.Inventory.TryGetItem(s.Jar,out jar);jar.MaximDurability=(ushort)Targets[s.Stage];jar.Send(p);Projection(p,s);
                    return ProgressText(s);
                }
                if(!abandon && p.Inventory.Count>(s.RuneReward==0?36:35))return s.RuneReward==0
                    ?"Leave four empty spaces beside your jar for five bound EXP Balls."
                    :"Leave five empty spaces beside your jar for five bound EXP Balls and a bound yellow rune.";
                var rewards=new List<ConquerItem>();
                if(!abandon)for(int i=0;i<5;i++) { var item=NewItem(RewardId,0);InsertItem(c,t,p,item);rewards.Add(item); }
                if(!abandon && s.RuneReward!=0) { var rune=NewItem(s.RuneReward,0);InsertItem(c,t,p,rune);rewards.Add(rune); }
                if(Execute(c,t,"DELETE FROM items WHERE uid=@jar AND entityid=@uid AND id=@type AND position=0 AND warehouse=0",
                    "jar",(long)s.Jar,"uid",(long)p.Entity.UID,"type",(long)JarId)!=1)throw new InvalidOperationException("Soul jar ownership changed.");
                if(!abandon)ProgressionEvidence.Complete(c,t,p.Entity.UID,11001,"demon-exterminators-v1",day,1);
                Execute(c,t,"UPDATE revora_demon_runs SET state=@state,reward_ids=@rewards,completed_at=@now WHERE uid=@uid AND day=@day",
                    "state",abandon?"abandoned":"complete","rewards",rewards.Select(i=>(long)i.UID).ToArray(),"now",Clock(),"uid",(long)p.Entity.UID,"day",day);
                t.Commit();string ignored;active.TryRemove(p.Entity.UID,out ignored);
                if(!p.Inventory.Remove(s.Jar,Enums.ItemUse.None,true))throw new InvalidOperationException("Committed jar removal requires reload.");
                foreach(var item in rewards)Install(p,item);Projection(p,null);
                if(!abandon) { if(p.Entity.MyAchievement!=null)p.Entity.MyAchievement.Invalidate();TitleRequirements.Reconcile(p,false); }
                return abandon?"I have taken back the jar. Your next hunt awaits tomorrow.":s.RuneReward==0
                    ?"The four hunts are complete. Take these five bound EXP Balls with my blessing."
                    :"The restless souls are at peace. Take these five bound EXP Balls and a bound yellow rune with my blessing.";
            }}
        }
        private static string ProgressText(Progress s) { return "Hunt "+(s.Stage+1)+" of 4: "+s.Kills+"/"+Needs[s.Stage]+" "+Names[s.Stage]+". Return with the filled jar."; }
        internal static bool HandleNpc(GameState p,NpcRequest request) {
            if(!enabled || p==null || p.Entity==null || request==null || p.ActiveNpc!=NpcId)return false;
            if(p.ItemSyncRoot==null)return true;
            lock(p.ItemSyncRoot) {
                if(!Near(p))return true;
                bool mutation=request.OptionID==1 || request.OptionID==2 || request.OptionID==4;
                try {
                    string day=Day(Clock());var d=new Npcs(p);
                    if(request.OptionID==1)d.Text(Accept(p,day));
                    else if(request.OptionID==2 || request.OptionID==4)d.Text(TurnIn(p,day,request.OptionID==4));
                    else if(request.OptionID==3) { d.Text("Set down the jar and today's hunt is lost. Will you abandon it?");d.Option("Abandon today's hunt.",4); }
                    else {
                        Progress s;using(var c=DataHolder.MySqlConnection) { c.Open();using(var t=c.BeginTransaction()) { s=Read(c,t,p.Entity.UID,day);t.Commit(); }}
                        if(s!=null && s.State=="active") { d.Text(ProgressText(s));d.Option("Here is my jar.",2);d.Option("Give up the hunt.",3); }
                        else if(s!=null)d.Text("Your day's hunt has ended. Return after midnight Pacific for another.");
                        else { d.Text("Troubled souls gather beyond the cities. Complete my four hunts for five bound EXP Balls and a bound yellow rune. I offer one hunt each day.");d.Option("Take the soul jar.",1); }
                    }
                    d.Option("Farewell.",255);d.Send();
                } catch(Exception ex) { Failure(p,ex,mutation); }
            }
            return true;
        }
        private static bool Target(Entity m) {
            if(m==null || m.EntityFlag!=EntityFlag.Monster || m.Companion || m.MonsterInfo==null)return false;
            uint id=m.MonsterInfo.ID;
            return m.MapID==1015 && id==18 || m.MapID==1001 && (id==20 || id==57 || id==58 || id==83);
        }
        internal static bool TryDie(Entity monster,Entity killer) {
            if(!enabled || !Target(monster))return false;
            Map map;Entity owned;
            if(!Kernel.Maps.TryGetValue(monster.MapID,out map) || !map.Entities.TryGetValue(monster.UID,out owned) || !ReferenceEquals(owned,monster))return false;
            ushort deathMap,x,y;uint type;bool died;DateTime occurred;bool durable=monster.MonsterInfo.Level>=120;Guid life=Guid.Empty;
            bool evaluated=false;var updates=new List<Tuple<GameState,Progress>>();Action summerRefresh=null;
            lock(monster) {
                if(monster.Dead)return true;
                deathMap=monster.MapID;x=monster.X;y=monster.Y;type=monster.MonsterInfo.ID;occurred=SummerHunt.Clock();
                if(durable) {
                    if(!System.Threading.Monitor.TryEnter(monster.MonsterLifeSyncRoot))return true;
                    try {
                        life=WildMonsterVictory.ProgressIdentity(monster);
                        WildMonsterVictory.DieWithProgress(monster,killer,(c,t)=>{
                            evaluated=true;bool demon=CreditSaved(c,t,killer,deathMap,x,y,type,life,updates);
                            bool summer=SummerHunt.CreditSaved(c,t,killer,deathMap,x,y,type,occurred,out summerRefresh);
                            return demon || summer;
                        });
                    }finally{System.Threading.Monitor.Exit(monster.MonsterLifeSyncRoot);}
                }else monster.DieCore(killer);
                died=monster.Dead;
            }
            if(died){
                if(durable){
                    if(evaluated){foreach(var update in updates)RefreshCounter(update.Item1,update.Item2);if(summerRefresh!=null)summerRefresh();}
                    else {RefreshCredits(life);SummerHunt.RefreshCredit(killer,occurred);}
                }
                else {Credit(killer,deathMap,x,y,type);SummerHunt.Credit(killer,deathMap,x,y,type,occurred);}
                PlanetWeekend.Credit(killer,deathMap,x,y,type,occurred);
            }
            return true;
        }
        private static bool CreditSaved(NpgsqlConnection c,NpgsqlTransaction t,Entity killer,ushort deathMap,ushort x,ushort y,uint type,Guid life,List<Tuple<GameState,Progress>> updates) {
            if(killer==null || killer.Owner==null || !ReferenceEquals(killer.Owner.Entity,killer) || !Ordinary(killer.Owner))return false;
            var owner=killer.Owner;var team=owner.Team;var recipients=new List<GameState>{owner};
            if(team!=null)recipients.AddRange(team.Teammates.Where(p=>p!=null && p.Entity!=null && p.Team==team && team.IsTeammate(p.Entity.UID)));
            string day=Day(Clock());bool changed=false;
            // Same ordered run-row ownership as ordinary team credit. No other
            // player's inventory or entity lock is acquired by this transaction.
            foreach(var p in recipients.Distinct().OrderBy(p=>p.Entity.UID)) {
                string accepted;
                if(!Ordinary(p) || p.Entity.Level<120 || p.Entity.MapID!=deathMap
                    || Kernel.GetDistance(p.Entity.X,p.Entity.Y,x,y)>Constants.pScreenDistance
                    || !active.TryGetValue(p.Entity.UID,out accepted) || accepted!=day
                    || p!=owner && (p.Team!=team || owner.Team!=team || !team.IsTeammate(p.Entity.UID)))continue;
                var run=Read(c,t,p.Entity.UID,day);
                if(run==null || run.State!="active" || run.Kills>=Needs[run.Stage] || !HasJar(c,t,p,run,false)
                    || type!=Targets[run.Stage] && !(run.Stage==3 && type==83))continue;
                if(Execute(c,t,"UPDATE revora_demon_runs SET kills=kills+1 WHERE uid=@uid AND day=@day","uid",(long)p.Entity.UID,"day",day)!=1)
                    throw new InvalidOperationException("Demon counter was not saved.");
                if(Execute(c,t,"INSERT INTO revora_demon_kill_credits(life,uid,day) VALUES(@life,@uid,@day)","life",life,"uid",(long)p.Entity.UID,"day",day)!=1)
                    throw new InvalidOperationException("Demon life credit was not saved.");
                run.Kills++;updates.Add(Tuple.Create(p,run));changed=true;
            }
            return changed;
        }
        private static void RefreshCounter(GameState p,Progress run) {
            if(!Ordinary(p))return;
            try {Projection(p,run);
                if(run!=null && run.State=="active" && (run.Kills==Needs[run.Stage] || run.Kills%100==0))p.Send(ProgressText(run));
            }catch(Exception ex){Failure(p,ex,true);}
        }
        private static void RefreshCredits(Guid life) {
            // Persisted recipients survive an uncertain-commit retry or a team
            // change. Reload counters; never apply a second in-memory increment.
            var credits=new List<Tuple<uint,string>>();
            try {
                using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Command(c,null,"SELECT uid,day FROM revora_demon_kill_credits WHERE life=@life ORDER BY uid","life",life))
                using(var r=q.ExecuteReader())while(r.Read())credits.Add(Tuple.Create(Convert.ToUInt32(r[0]),r.GetString(1)));}
                foreach(var credit in credits) {
                    GameState p;if(!Kernel.GamePool.TryGetValue(credit.Item1,out p) || !Ordinary(p) || credit.Item2!=Day(Clock()))continue;
                    try {using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                        var run=Read(c,t,credit.Item1,credit.Item2);t.Commit();RefreshCounter(p,run);
                    }}}catch(Exception ex){Failure(p,ex,true);}
                }
            }catch(Exception ex){Console.WriteLine("[quest] source=demon-credit-refresh error="+ex.GetType().Name);}
        }
        private static void Credit(Entity killer,ushort deathMap,ushort x,ushort y,uint type) {
            if(killer==null || killer.EntityFlag!=EntityFlag.Player || killer.Owner==null || !ReferenceEquals(killer.Owner.Entity,killer) || !Ordinary(killer.Owner))return;
            var owner=killer.Owner;var team=owner.Team;var recipients=new List<GameState>{owner};
            if(team!=null)recipients.AddRange(team.Teammates.Where(p=>p!=null && p.Entity!=null && p.Team==team && team.IsTeammate(p.Entity.UID)));
            string day=Day(Clock());
            foreach(var p in recipients.Distinct().OrderBy(p=>p.Entity.UID)) {
                string accepted;
                if(!Ordinary(p) || p.Entity.Level<120 || p.Entity.MapID!=deathMap
                    || Kernel.GetDistance(p.Entity.X,p.Entity.Y,x,y)>Constants.pScreenDistance
                    || !active.TryGetValue(p.Entity.UID,out accepted) || accepted!=day)continue;
                // Counter-only writes use run-row locks and persisted jar ownership.
                // Never take another teammate's item lock from an item-triggered kill.
                try {
                    if(!Ordinary(p) || p!=owner && (p.Team!=team || owner.Team!=team || !team.IsTeammate(p.Entity.UID)))continue;
                    using(var c=DataHolder.MySqlConnection) { c.Open();using(var t=c.BeginTransaction()) {
                        var s=Read(c,t,p.Entity.UID,day);
                        if(s==null || s.State!="active" || s.Kills>=Needs[s.Stage] || !HasJar(c,t,p,s,false)
                            || type!=Targets[s.Stage] && !(s.Stage==3 && type==83))continue;
                        Execute(c,t,"UPDATE revora_demon_runs SET kills=kills+1 WHERE uid=@uid AND day=@day","uid",(long)p.Entity.UID,"day",day);
                        t.Commit();s.Kills++;Projection(p,s);
                        if(s.Kills==Needs[s.Stage] || s.Kills%100==0)p.Send(ProgressText(s));
                    }}
                } catch(Exception ex) { Failure(p,ex,true); }
            }
        }
    }
}
