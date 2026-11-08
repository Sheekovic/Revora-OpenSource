using System;
using System.Linq;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Q=Revora.Game.Features.QuestInventory;
using S=Revora.Game.Features.FranticMonkeysStore;
namespace Revora.Game.Features
{
    internal static partial class FranticMonkeysScenes
    {
        [ThreadStatic] private static Scene transfer;
        internal static void InitializeTravel()
        {
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_frantic_visits(
uid bigint PRIMARY KEY,visit uuid NOT NULL UNIQUE,life smallint NOT NULL,map integer NOT NULL,
active boolean NOT NULL,until_at timestamptz NOT NULL)");}
        }
        internal static Scene[] Active(){lock(gate)return scenes.Values.ToArray();}
        internal static Scene Find(GameState p){lock(gate)return scenes.Values.FirstOrDefault(s=>ReferenceEquals(s.Owner,p) && !s.Released);}
        internal static bool AllowTeleport(GameState p,ushort destination,ushort x,ushort y)
        {
            lock(gate){
                Scene scene;if(!scenes.TryGetValue(destination,out scene))return true;
                if(!Registered(scene,p) || !S.Eligible(p) || Clock()>=scene.Until || !scene.Map.Floor[x,y,MapObjectType.Player])return false;
                if(ReferenceEquals(transfer,scene))return true;
                return scene.Entered && p.Entity.MapID==destination && ReferenceEquals(p.Map,scene.Map) && Kernel.GetDistance(p.Entity.X,p.Entity.Y,x,y)<=20;
            }
        }
        internal static void Enter(Scene scene,GameState p,Func<bool> context)
        {
            if(!S.Eligible(p) || !Monitor.IsEntered(p.ItemSyncRoot) || !Registered(scene,p) || Clock()>=scene.Until
                || context==null || !context() || p.Entity.OnDeath!=null || p.InQualifier() || p.InTeamQualifier() || transfer!=null)
                throw new InvalidOperationException("Frantic Monkeys admission changed.");
            var run=S.Load(p);var trial=S.ActiveTrial(p);if(run==null || run.Stage!=5 || trial==null || trial.ID!=scene.Visit || trial.Map!=scene.Map.ID || Clock()>=trial.Until)throw new InvalidOperationException("Frantic Monkeys admission stage changed.");
            if(scene.Entered){if(p.Entity.MapID!=scene.Map.ID)throw new InvalidOperationException("Frantic Monkeys visit moved.");return;}
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                SkillTable.LockOwner(c,t,p);
                using(var q=Q.Command(c,t,"SELECT stage FROM revora_frantic_monkeys WHERE uid=@uid AND life=@life FOR UPDATE","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn))
                    if(Convert.ToInt32(q.ExecuteScalar())!=run.Stage)throw new InvalidOperationException("Frantic Monkeys saved admission changed.");
                if(Q.Execute(c,t,@"INSERT INTO revora_frantic_visits(uid,visit,life,map,active,until_at) VALUES(@uid,@visit,@life,@map,true,@until)
ON CONFLICT(uid) DO UPDATE SET visit=EXCLUDED.visit,life=EXCLUDED.life,map=EXCLUDED.map,active=true,until_at=EXCLUDED.until_at
WHERE NOT revora_frantic_visits.active","uid",(long)p.Entity.UID,"visit",scene.Visit,"life",(int)p.Entity.Reborn,"map",(int)scene.Map.ID,"until",scene.Until)!=1)
                    throw new InvalidOperationException("Frantic Monkeys unresolved prior visit.");
                using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_frantic_trials WHERE id=@visit AND uid=@uid AND life=@life AND map=@map AND state=0 AND until_at>@now)","visit",scene.Visit,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"map",(int)scene.Map.ID,"now",Clock()))
                    if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Frantic Monkeys admission attempt changed.");
                if(!Registered(scene,p) || !S.Eligible(p) || !context())throw new InvalidOperationException("Frantic Monkeys admission owner changed.");t.Commit();
            }}
            transfer=scene;
            try{
                if(!context())throw new InvalidOperationException("Frantic Monkeys entry context changed.");
                p.Entity.Teleport(scene.Map.ID,EntryX,EntryY);
                if(p.Entity.MapID!=scene.Map.ID || !ReferenceEquals(p.Map,scene.Map))throw new InvalidOperationException("Frantic Monkeys entry failed.");
                scene.Entered=true;if(!EntityTable.SaveEntity(p))throw new InvalidOperationException("Frantic Monkeys arrival was not saved.");
            }finally{transfer=null;}
        }
        private static void Retire(Scene scene)
        {
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,"UPDATE revora_frantic_visits SET active=false WHERE uid=@uid AND visit=@visit","uid",(long)scene.Owner.Entity.UID,"visit",scene.Visit);}
        }
        private static void ClearActors(Scene scene)
        {
            if(scene.Appearance!=Guid.Empty){QuestAppearance.Clear(scene.Owner,scene.Appearance,QuestAppearance.MonkeyMesh);scene.Appearance=Guid.Empty;}
            foreach(var body in scene.Map.Entities.Values.ToArray()){
                if(body.EntityFlag!=EntityFlag.Monster || body.MonsterInfo==null || body.MonsterInfo.ID<FamilyStart || body.MonsterInfo.ID>FamilyStart+2)
                    throw new InvalidOperationException("Frantic Monkeys scene contains a foreign actor.");
                scene.Map.RemoveEntity(body);
            }
            foreach(var body in scene.Map.Companions.Values.ToArray()){
                if(!ReferenceEquals(body.Owner,scene.Owner))throw new InvalidOperationException("Frantic Monkeys scene contains a foreign companion.");scene.Map.RemoveEntity(body);
            }
            scene.Map.Npcs.Clear();scene.Map.FloorItems.Clear();
        }
        internal static void Exit(Scene scene,GameState p)
        {
            if(scene==null || scene.Released)return;
            if(!ReferenceEquals(scene.Owner,p) || !Q.Player(p,false) || !Monitor.IsEntered(p.ItemSyncRoot))throw new InvalidOperationException("Frantic Monkeys return owner changed.");
            if(p.Entity.MapID==scene.Map.ID){
                Map home;if(!Kernel.Maps.TryGetValue(1002,out home) || !home.Floor[440,345,MapObjectType.Player])throw new InvalidOperationException("Frantic Monkeys return point blocked.");
                if(p.Entity.Dead)p.Entity.BringToLife();p.Entity.Teleport(1002,440,345);
                if(p.Entity.MapID!=1002 || !EntityTable.SaveEntity(p))throw new InvalidOperationException("Frantic Monkeys return was not saved.");
            }
            Retire(scene);ClearActors(scene);Release(scene);
        }
        private static void RecoverOffline(uint uid)
        {
            if(Kernel.GamePool.ContainsKey(uid))throw new InvalidOperationException("Frantic Monkeys offline recovery found a loaded session.");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)uid))if(q.ExecuteScalar()==null)throw new InvalidOperationException("Frantic Monkeys return owner missing.");
                object saved;
                using(var q=Q.Command(c,t,"SELECT map FROM revora_frantic_visits WHERE uid=@uid AND active FOR UPDATE","uid",(long)uid))saved=q.ExecuteScalar();
                if(saved!=null){
                    object current;using(var q=Q.Command(c,t,"SELECT mapid FROM entities WHERE uid=@uid","uid",(long)uid))current=q.ExecuteScalar();
                    if(Convert.ToInt32(current)==Convert.ToInt32(saved) && Q.Execute(c,t,"UPDATE entities SET mapid=1002,x=440,y=345 WHERE uid=@uid AND mapid=@map","uid",(long)uid,"map",Convert.ToInt32(saved))!=1)
                        throw new InvalidOperationException("Frantic Monkeys offline return was not saved.");
                    if(Q.Execute(c,t,"UPDATE revora_frantic_visits SET active=false WHERE uid=@uid AND active","uid",(long)uid)!=1)throw new InvalidOperationException("Frantic Monkeys return receipt was not saved.");
                }t.Commit();
            }}
        }
        private static void RecoverLoginLocation(GameState p)
        {
            if(!Q.Player(p,false))return;
            lock(p.ItemSyncRoot){
                Guid visit;int map;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT visit,map FROM revora_frantic_visits WHERE uid=@uid AND active","uid",(long)p.Entity.UID))using(var r=q.ExecuteReader()){
                    if(!r.Read())return;visit=r.GetGuid(0);map=r.GetInt32(1);
                }}
                if(p.Entity.MapID==map){
                    if(p.Entity.Dead)p.Entity.BringToLife();p.Entity.Teleport(1002,440,345);
                    if(p.Entity.MapID!=1002)throw new InvalidOperationException("Frantic Monkeys reconnect return failed.");
                }
                if(!EntityTable.SaveEntity(p))throw new InvalidOperationException("Frantic Monkeys reconnect location was not saved.");
                using(var c=DataHolder.MySqlConnection){c.Open();if(Q.Execute(c,null,"UPDATE revora_frantic_visits SET active=false WHERE uid=@uid AND visit=@visit AND active","uid",(long)p.Entity.UID,"visit",visit)!=1)
                    throw new InvalidOperationException("Frantic Monkeys reconnect receipt changed.");}
            }
        }
        internal static void RecoverLogin(GameState p)
        {
            if(!Q.Player(p,false))return;
            lock(p.ItemSyncRoot){RecoverLoginLocation(p);if(S.Eligible(p) && !HasOwner(p.Entity.UID)){
                var trial=S.ActiveTrial(p);if(trial!=null)S.RecoverTrial(p,trial.ID,()=>S.Eligible(p) && !HasOwner(p.Entity.UID));
            }}
        }
        internal static void RecoverInterrupted()
        {
            lock(gate)if(scenes.Count!=0)throw new InvalidOperationException("Frantic Monkeys startup recovery requires empty scene registry.");
            var ids=new System.Collections.Generic.List<uint>();
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT uid FROM revora_frantic_visits WHERE active ORDER BY uid"))using(var r=q.ExecuteReader())while(r.Read())ids.Add(checked((uint)r.GetInt64(0)));}
            foreach(uint uid in ids)RecoverOffline(uid);
        }
        internal static void Tick()
        {
            QuestAppearance.Tick();
            Scene[] pending;lock(gate)pending=scenes.Values.ToArray();
            foreach(var scene in pending){var p=scene.Owner;lock(p.ItemSyncRoot)try{
                if(scene.Released)continue;
                if(!Kernel.GamePool.ContainsKey(p.Entity.UID)){RecoverOffline(p.Entity.UID);ClearActors(scene);Release(scene);continue;}
                GameState current;if(Kernel.GamePool.TryGetValue(p.Entity.UID,out current) && !ReferenceEquals(current,p)){
                    if(current.Entity!=null && current.Entity.MapID==scene.Map.ID)continue;
                    Retire(scene);ClearActors(scene);Release(scene);continue;
                }
                if(!Q.Player(p,false))continue;
                var run=S.Load(p);
                if(Clock()>=scene.Until || p.Entity.Dead || scene.Entered && p.Entity.MapID!=scene.Map.ID || !S.Eligible(p) || run.Stage>=6){
                    Exit(scene,p);if(S.Eligible(p) && run.Stage==5)S.RecoverTrial(p,scene.Visit,()=>S.Eligible(p) && !HasOwner(p.Entity.UID));
                }else if(scene.Entered)FranticMonkeysCombat.Populate(scene);
            }catch(Exception ex){Q.Fail(p,"frantic-travel",ex);}}
        }
    }
}
