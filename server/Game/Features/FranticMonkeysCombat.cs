using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using S=Revora.Game.Features.FranticMonkeysStore;
using W=Revora.Game.Features.FranticMonkeysScenes;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static class FranticMonkeysCombat
    {
        private sealed class Fight {internal W.Scene Scene;internal Entity Body;internal DateTime RemoveAt,ReadyAt;internal bool Recorded;}
        private sealed class Population {internal DateTime Next;}
        private static readonly ConditionalWeakTable<W.Scene,Population> populations=new ConditionalWeakTable<W.Scene,Population>();
        private static readonly ConditionalWeakTable<Entity,Fight> fights=new ConditionalWeakTable<Entity,Fight>();
        internal static bool IsEncounter(Entity body){return body!=null && body.MonsterInfo!=null && body.MonsterInfo.ID==W.FamilyStart;}
        private static bool Current(Fight fight)
        {
            Entity body;var p=fight.Scene.Owner;
            return W.Registered(fight.Scene,p) && S.Eligible(p) && p.Entity.MapID==fight.Scene.Map.ID && ReferenceEquals(p.Map,fight.Scene.Map)
                && W.Clock()>=fight.ReadyAt && W.Clock()<fight.Scene.Until && fight.Scene.Map.Entities.TryGetValue(fight.Body.UID,out body) && ReferenceEquals(body,fight.Body);
        }
        private static bool Fighter(Fight fight,Entity entity)
        {
            var p=fight.Scene.Owner;if(entity==null || entity.Dead || entity.MapID!=fight.Scene.Map.ID)return false;
            if(ReferenceEquals(entity,p.Entity))return true;
            Entity companion;return entity.Companion && ReferenceEquals(entity.Owner,p) && ReferenceEquals(p.Companion,entity)
                && fight.Scene.Map.Companions.TryGetValue(entity.UID,out companion) && ReferenceEquals(companion,entity);
        }
        internal static bool BlockAttack(Entity attacker,Entity target)
        {
            var body=IsEncounter(attacker)?attacker:IsEncounter(target)?target:null;
            if(body==null)return attacker!=null && W.Contains(attacker.MapID) || target!=null && W.Contains(target.MapID);
            Fight fight;return !fights.TryGetValue(body,out fight) || !Current(fight) || body.Dead || !Fighter(fight,ReferenceEquals(body,attacker)?target:attacker);
        }
        private sealed class Lease:IDisposable {private readonly object gate;internal Lease(object value){gate=value;}public void Dispose(){Monitor.Exit(gate);}}
        internal static IDisposable DamageLease(Entity attacker,Entity target)
        {
            var body=IsEncounter(attacker)?attacker:IsEncounter(target)?target:null;Fight fight;
            if(body==null || !fights.TryGetValue(body,out fight))return null;
            return Monitor.TryEnter(fight.Scene.Owner.ItemSyncRoot)?new Lease(fight.Scene.Owner.ItemSyncRoot):null;
        }
        internal static bool BlockDamage(Entity attacker,Entity target)
        {
            if(BlockAttack(attacker,target))return true;
            var body=IsEncounter(attacker)?attacker:IsEncounter(target)?target:null;Fight fight;
            return body!=null && (!fights.TryGetValue(body,out fight) || !Monitor.IsEntered(fight.Scene.Owner.ItemSyncRoot));
        }
        internal static Entity Spawn(W.Scene scene,GameState p,int kind,ushort x,ushort y)
        {
            if(!S.Eligible(p) || !Monitor.IsEntered(p.ItemSyncRoot) || !W.Registered(scene,p) || p.Entity.MapID!=scene.Map.ID || W.Clock()>=scene.Until
                || scene.Map.Entities.Count>=10 || !scene.Map.Floor[x,y,MapObjectType.Monster,null])throw new InvalidOperationException("Frantic Monkeys spawn ownership, capacity or landing changed.");
            var trial=S.ActiveTrial(p);var run=S.Load(p);
            if(kind!=0 || !scene.Entered || run.Stage!=5 || trial==null || trial.ID!=scene.Visit || trial.Map!=scene.Map.ID || W.Clock()<trial.ReadyAt || W.Clock()>=trial.Until)
                throw new InvalidOperationException("Frantic trial is not ready for combat.");
            bool allowed=false;for(int i=0;i<W.SpawnPoints.GetLength(0);i++)if(W.SpawnPoints[i,0]==x && W.SpawnPoints[i,1]==y)allowed=true;
            if(!allowed || scene.Map.Entities.Values.Any(b=>b.X==x && b.Y==y) || scene.Map.Companions.Values.Any(b=>b.X==x && b.Y==y) || p.Entity.X==x && p.Entity.Y==y)
                throw new InvalidOperationException("Frantic trial landing occupied or unaudited.");
            var info=W.Profile(kind);info.BoundX=x;info.BoundY=y;info.BoundCX=info.BoundCY=1;
            var body=new Entity(EntityFlag.Monster,false){UID=scene.Map.EntityUIDCounter.Next,MapID=scene.Map.ID,X=x,Y=y,MonsterInfo=info,
                MapObjType=MapObjectType.Monster,Name=info.Name,Body=info.Mesh,Level=info.Level,MinAttack=info.MinAttack,MaxAttack=info.MaxAttack,Defence=info.Defence};
            while(scene.Map.Entities.ContainsKey(body.UID) || scene.Map.Npcs.ContainsKey(body.UID) || scene.Map.Companions.ContainsKey(body.UID) || Kernel.GamePool.ContainsKey(body.UID))body.UID=scene.Map.EntityUIDCounter.Next;
            info.Owner=body;body.MaxHitpoints=body.Hitpoints=info.Hitpoints;body.IsDropped=true;body.SendUpdates=true;
            try{scene.Map.AddEntity2(body);fights.Add(body,new Fight{Scene=scene,Body=body,ReadyAt=trial.ReadyAt});body.SendSpawn(p,false);return body;}
            catch{scene.Map.RemoveEntity(body);throw;}
        }
        internal static bool TryDie(Entity body,Entity killer)
        {
            if(!IsEncounter(body))return false;Fight fight;if(!fights.TryGetValue(body,out fight))return true;
            var p=fight.Scene.Owner;if(!Monitor.TryEnter(p.ItemSyncRoot))return true;
            try{
                if(!Current(fight) || fight.Recorded || !Fighter(fight,killer) || Kernel.GetDistance(p.Entity.X,p.Entity.Y,body.X,body.Y)>Constants.pScreenDistance)return true;
                if(!body.Dead)body.DieCore(p.Entity);
                if(!S.TrialKill(p,fight.Scene,body,()=>Current(fight)))throw new InvalidOperationException("Frantic owned death was not committed.");
                fight.Recorded=true;fight.RemoveAt=W.Clock().AddSeconds(1);
            }catch(Exception ex){Q.Fail(p,"frantic-combat",ex);}finally{Monitor.Exit(p.ItemSyncRoot);}
            return true;
        }
        internal static void Populate(W.Scene scene)
        {
            if(scene==null || !Monitor.IsEntered(scene.Owner.ItemSyncRoot))throw new InvalidOperationException("Frantic population requires owner lock.");
            var p=scene.Owner;var now=W.Clock();
            if(!W.Registered(scene,p) || !scene.Entered || !S.Eligible(p) || !ReferenceEquals(p.Map,scene.Map) || p.Entity.MapID!=scene.Map.ID || now>=scene.Until)return;
            Prune(scene);var population=populations.GetValue(scene,key=>new Population());if(now<population.Next)return;population.Next=now.AddSeconds(3);
            var run=S.Load(p);var trial=S.ActiveTrial(p);
            if(run.Stage!=5 || trial==null || trial.ID!=scene.Visit || trial.Map!=scene.Map.ID || now<trial.ReadyAt || now>=trial.Until)return;
            int needed=5-run.Kills-scene.Map.Entities.Values.Count(b=>!b.Dead);
            for(int i=0;i<W.SpawnPoints.GetLength(0) && needed>0 && scene.Map.Entities.Count<10;i++){
                ushort x=W.SpawnPoints[i,0],y=W.SpawnPoints[i,1];
                if(p.Entity.X==x && p.Entity.Y==y || scene.Map.Companions.Values.Any(b=>b.X==x && b.Y==y) || scene.Map.Entities.Values.Any(b=>b.X==x && b.Y==y))continue;
                Spawn(scene,p,0,x,y);needed--;
            }
        }
        private static void Remove(W.Scene scene,Entity body)
        {
            scene.Map.RemoveEntity(body);body.MonsterInfo.SendScreen(new Data(true){UID=body.UID,ID=Data.RemoveEntity});fights.Remove(body);
        }
        internal static void Prune(W.Scene scene)
        {
            if(!Monitor.IsEntered(scene.Owner.ItemSyncRoot))throw new InvalidOperationException("Frantic cleanup requires owner lock.");
            foreach(var body in scene.Map.Entities.Values.ToArray()){
                Fight fight;if(!fights.TryGetValue(body,out fight) || !body.Dead || !fight.Recorded || W.Clock()<fight.RemoveAt)continue;
                Remove(scene,body);
            }
        }
    }
}
