using System;
using System.Linq;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Q=Revora.Game.Features.QuestInventory;
using S=Revora.Game.Features.DefenseTalismanStore;
namespace Revora.Game.Features
{
    internal static partial class DefenseTalismanScenes
    {
        [ThreadStatic] private static Scene transfer;
        internal static void InitializeTravel()
        {
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_tower_visits(
uid bigint NOT NULL,visit uuid PRIMARY KEY,map integer NOT NULL,
active boolean NOT NULL,until_at timestamptz NOT NULL);
CREATE UNIQUE INDEX IF NOT EXISTS revora_tower_active_visit ON revora_tower_visits(uid) WHERE active;
CREATE TABLE IF NOT EXISTS revora_tower_house_kills(attempt uuid PRIMARY KEY,uid bigint NOT NULL,
visit uuid NOT NULL REFERENCES revora_tower_visits(visit),body bigint NOT NULL,UNIQUE(visit,body));");}
        }

        internal static Scene[] Active(){lock(gate)return scenes.Values.ToArray();}
        internal static Scene Find(GameState p){lock(gate)return scenes.Values.FirstOrDefault(s=>ReferenceEquals(s.Owner,p) && !s.Released);}
        internal static bool Descend(GameState p,Func<bool> context){
            if(!S.Eligible(p) || !Monitor.IsEntered(p.ItemSyncRoot))return false;
            var before=Find(p);if(before==null || !S.AdvanceDungeon(p,context))return false;
            // Retire the old visit before allocation; saved floor survives a failed new admission.
            Exit(before,p);var next=CreateDungeon(p);
            try{if(Enter(next,p,()=>S.Eligible(p)))return true;Exit(next,p);return false;}
            catch{if(!next.Released && Q.Player(p,false))Exit(next,p);throw;}
        }
        internal static bool AllowTeleport(GameState p,ushort destination,ushort x,ushort y)
        {
            lock(gate){
                Scene scene;if(!scenes.TryGetValue(destination,out scene))return true;
                if(!Registered(scene,p) || !S.Eligible(p) || Clock()>=scene.Until || !scene.Map.Floor[x,y,MapObjectType.Player])return false;
                if(ReferenceEquals(transfer,scene))return true;
                return false; // Movement uses validated walk/jump packets; scene teleport shortcuts are denied.
            }
        }
        internal static bool Enter(Scene scene,GameState p,Func<bool> context)
        {
            if(!S.Eligible(p) || !Monitor.IsEntered(p.ItemSyncRoot) || !Registered(scene,p) || Clock()>=scene.Until
                || context==null || !context() || p.Entity.OnDeath!=null || p.InQualifier() || p.InTeamQualifier() || transfer!=null)return false;
            if(scene.Entered)return p.Entity.MapID==scene.Map.ID && ReferenceEquals(p.Map,scene.Map);
            if(!S.BeginVisit(p,scene,context))return false;
            transfer=scene;
            try{
                if(!context())throw new InvalidOperationException("Defense Talisman entry context changed.");
                p.Entity.Teleport(scene.Map.ID,scene.EntryX,scene.EntryY);
                if(p.Entity.MapID!=scene.Map.ID || !ReferenceEquals(p.Map,scene.Map))throw new InvalidOperationException("Defense Talisman entry failed.");
                scene.Entered=true;if(!EntityTable.SaveEntity(p))throw new InvalidOperationException("Defense Talisman arrival was not saved.");return true;
            }finally{transfer=null;}
        }
        private static void Retire(Scene scene)
        {
            using(var c=DataHolder.MySqlConnection){c.Open();if(Q.Execute(c,null,"UPDATE revora_tower_visits SET active=false WHERE uid=@uid AND visit=@visit","uid",(long)scene.Owner.Entity.UID,"visit",scene.Visit)!=1){
                using(var q=Q.Command(c,null,"SELECT EXISTS(SELECT 1 FROM revora_tower_visits WHERE uid=@uid AND visit=@visit)","uid",(long)scene.Owner.Entity.UID,"visit",scene.Visit))
                    if(scene.Entered || Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Defense Talisman visit retirement was not saved.");
            }}
        }
        private static void ClearActors(Scene scene)
        {
            DefenseTalismanEncounters.Cancel(scene.Owner);
            if(scene.Map.Entities.Count!=0)throw new InvalidOperationException("Defense Talisman scene contains unexpected actors.");
            foreach(var body in scene.Map.Companions.Values.ToArray()){
                if(!ReferenceEquals(body.Owner,scene.Owner))throw new InvalidOperationException("Defense Talisman scene contains a foreign companion.");scene.Map.RemoveEntity(body);
            }
            scene.Map.FloorItems.Clear();
        }
        internal static void Exit(Scene scene,GameState p)
        {
            if(scene==null || scene.Released)return;
            if(!ReferenceEquals(scene.Owner,p) || !Q.Player(p,false) || !Monitor.IsEntered(p.ItemSyncRoot))throw new InvalidOperationException("Defense Talisman return owner changed.");
            if(p.Entity.MapID==scene.Map.ID){
                Map home;if(!Kernel.Maps.TryGetValue(1015,out home) || !home.Floor[667,780,MapObjectType.Player])throw new InvalidOperationException("Defense Talisman return point blocked.");
                if(p.Entity.Dead)p.Entity.BringToLife();p.Entity.Teleport(1015,667,780);
                if(p.Entity.MapID!=1015 || !EntityTable.SaveEntity(p))throw new InvalidOperationException("Defense Talisman return was not saved.");
            }
            Retire(scene);ClearActors(scene);Release(scene);
        }
        private static void RecoverOffline(uint uid)
        {
            if(Kernel.GamePool.ContainsKey(uid))throw new InvalidOperationException("Defense Talisman offline recovery found a loaded session.");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)uid))if(q.ExecuteScalar()==null)throw new InvalidOperationException("Defense Talisman return owner missing.");
                object saved;
                using(var q=Q.Command(c,t,"SELECT map FROM revora_tower_visits WHERE uid=@uid AND active FOR UPDATE","uid",(long)uid))saved=q.ExecuteScalar();
                if(saved!=null){
                    object current;using(var q=Q.Command(c,t,"SELECT mapid FROM entities WHERE uid=@uid","uid",(long)uid))current=q.ExecuteScalar();
                    if(Convert.ToInt32(current)==Convert.ToInt32(saved) && Q.Execute(c,t,"UPDATE entities SET mapid=1015,x=667,y=780 WHERE uid=@uid AND mapid=@map","uid",(long)uid,"map",Convert.ToInt32(saved))!=1)
                        throw new InvalidOperationException("Defense Talisman offline return was not saved.");
                    if(Q.Execute(c,t,"UPDATE revora_tower_visits SET active=false WHERE uid=@uid AND active","uid",(long)uid)!=1)throw new InvalidOperationException("Defense Talisman return receipt was not saved.");
                }t.Commit();
            }}
        }
        private static void RecoverLoginLocation(GameState p)
        {
            if(!Q.Player(p,false))return;
            lock(p.ItemSyncRoot){
                Guid visit;int map;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT visit,map FROM revora_tower_visits WHERE uid=@uid AND active","uid",(long)p.Entity.UID))using(var r=q.ExecuteReader()){
                    if(!r.Read())return;visit=r.GetGuid(0);map=r.GetInt32(1);
                }}
                if(p.Entity.MapID==map){
                    if(p.Entity.Dead)p.Entity.BringToLife();p.Entity.Teleport(1015,667,780);
                    if(p.Entity.MapID!=1015)throw new InvalidOperationException("Defense Talisman reconnect return failed.");
                }
                if(!EntityTable.SaveEntity(p))throw new InvalidOperationException("Defense Talisman reconnect location was not saved.");
                using(var c=DataHolder.MySqlConnection){c.Open();if(Q.Execute(c,null,"UPDATE revora_tower_visits SET active=false WHERE uid=@uid AND visit=@visit AND active","uid",(long)p.Entity.UID,"visit",visit)!=1)
                    throw new InvalidOperationException("Defense Talisman reconnect receipt changed.");}
            }
        }
        internal static void RecoverLogin(GameState p){RecoverLoginLocation(p);}
        internal static void RecoverInterrupted()
        {
            lock(gate)if(scenes.Count!=0)throw new InvalidOperationException("Defense Talisman startup recovery requires empty scene registry.");
            var ids=new System.Collections.Generic.List<uint>();
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT uid FROM revora_tower_visits WHERE active ORDER BY uid"))using(var r=q.ExecuteReader())while(r.Read())ids.Add(checked((uint)r.GetInt64(0)));}
            foreach(uint uid in ids)RecoverOffline(uid);
        }
        internal static void Tick()
        {
            Scene[] pending;lock(gate)pending=scenes.Values.ToArray();
            foreach(var scene in pending){var p=scene.Owner;lock(p.ItemSyncRoot)try{
                if(scene.Released)continue;
                if(!Kernel.GamePool.ContainsKey(p.Entity.UID)){RecoverOffline(p.Entity.UID);ClearActors(scene);Release(scene);continue;}
                GameState current;if(Kernel.GamePool.TryGetValue(p.Entity.UID,out current) && !ReferenceEquals(current,p)){
                    if(current.Entity!=null && current.Entity.MapID==scene.Map.ID)continue;
                    Retire(scene);ClearActors(scene);Release(scene);continue;
                }
                if(!Q.Player(p,false))continue;
                if(Clock()>=scene.Until || p.Entity.Dead || scene.Entered && p.Entity.MapID!=scene.Map.ID || !S.Eligible(p) || scene.Completed){
                    Exit(scene,p);
                }
            }catch(Exception ex){Q.Fail(p,"tower-travel",ex);}}
        }
    }
}
