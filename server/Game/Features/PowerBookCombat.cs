using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using S=Revora.Game.Features.PowerBookStore;
using W=Revora.Game.Features.PowerBookScenes;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static class PowerBookCombat
    {
        private sealed class Fight {internal W.Scene Scene;internal Entity Body;internal DateTime RemoveAt;internal bool Recorded;}
        private sealed class Population {internal int Stage;internal DateTime Next;}
        private static readonly ConditionalWeakTable<W.Scene,Population> populations=new ConditionalWeakTable<W.Scene,Population>();
        private static readonly ConditionalWeakTable<Entity,Fight> fights=new ConditionalWeakTable<Entity,Fight>();
        internal static bool IsEncounter(Entity body){return body!=null && body.MonsterInfo!=null && body.MonsterInfo.ID>=W.FamilyStart && body.MonsterInfo.ID<=W.FamilyStart+6;}
        private static bool Current(Fight fight)
        {
            Entity body;var p=fight.Scene.Owner;
            return W.Registered(fight.Scene,p) && S.Eligible(p) && p.Entity.MapID==fight.Scene.Map.ID && ReferenceEquals(p.Map,fight.Scene.Map)
                && S.Clock()<fight.Scene.Until && fight.Scene.Map.Entities.TryGetValue(fight.Body.UID,out body) && ReferenceEquals(body,fight.Body);
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
            if(!S.Eligible(p) || !Monitor.IsEntered(p.ItemSyncRoot) || !W.Registered(scene,p) || p.Entity.MapID!=scene.Map.ID || S.Clock()>=scene.Until
                || scene.Map.Entities.Count>=24 || !scene.Map.Floor[x,y,MapObjectType.Monster,null])throw new InvalidOperationException("Power Book spawn ownership, capacity or landing changed.");
            if(kind<0 || kind>6 || (kind<2?0:kind<4?1:kind<6?2:3)!=scene.Floor)throw new InvalidOperationException("Power Book monster belongs to another floor.");
            var info=W.Profile(kind);info.BoundX=x;info.BoundY=y;info.BoundCX=info.BoundCY=1;
            var body=new Entity(EntityFlag.Monster,false){UID=scene.Map.EntityUIDCounter.Next,MapID=scene.Map.ID,X=x,Y=y,MonsterInfo=info,
                MapObjType=MapObjectType.Monster,Name=info.Name,Body=info.Mesh,Level=info.Level,MinAttack=info.MinAttack,MaxAttack=info.MaxAttack,Defence=info.Defence};
            while(scene.Map.Entities.ContainsKey(body.UID) || scene.Map.Npcs.ContainsKey(body.UID) || scene.Map.Companions.ContainsKey(body.UID) || Kernel.GamePool.ContainsKey(body.UID))body.UID=scene.Map.EntityUIDCounter.Next;
            info.Owner=body;body.MaxHitpoints=body.Hitpoints=info.Hitpoints;body.IsDropped=true;body.SendUpdates=true;
            try{scene.Map.AddEntity2(body);S.RegisterLife(p,scene,body);fights.Add(body,new Fight{Scene=scene,Body=body});body.SendSpawn(p,false);return body;}
            catch{scene.Map.RemoveEntity(body);throw;}
        }
        internal static bool TryDie(Entity body,Entity killer)
        {
            if(!IsEncounter(body))return false;Fight fight;if(!fights.TryGetValue(body,out fight))return true;
            var p=fight.Scene.Owner;if(!Monitor.TryEnter(p.ItemSyncRoot))return true;
            try{
                if(!Current(fight) || fight.Recorded || !Fighter(fight,killer) || Kernel.GetDistance(p.Entity.X,p.Entity.Y,body.X,body.Y)>Constants.pScreenDistance)return true;
                bool final=body.MonsterInfo.ID==W.FamilyStart+6;
                if(final && ReferenceEquals(killer,p.Entity)) {
                    WildMonsterVictory.DieWithProgress(body,killer,(c,t)=>S.AcceptFinalLife(c,t,p,body));
                    if(!body.Dead)return true;
                }else {
                    if(!body.Dead)body.DieCore(p.Entity);
                    if(!body.Dead)return true;
                    S.AcceptDeath(p,body);
                }
                fight.Recorded=true;fight.RemoveAt=S.Clock().AddSeconds(final?4:1);
            }catch(Exception ex){Q.Fail(p,"power-book-combat",ex);}finally{Monitor.Exit(p.ItemSyncRoot);}
            return true;
        }
        // The coordinator calls this only for entered scenes. Bound both database
        // polling and monsters; expired corpses count toward the hard spawn cap.
        internal static void Populate(W.Scene scene)
        {
            if(scene==null || !Monitor.IsEntered(scene.Owner.ItemSyncRoot))throw new InvalidOperationException("Power Book population requires owner lock.");
            var p=scene.Owner;
            if(!W.Registered(scene,p) || !scene.Entered || !S.Eligible(p) || !ReferenceEquals(p.Map,scene.Map) || p.Entity.MapID!=scene.Map.ID || S.Clock()>=scene.Until)return;
            Prune(scene);
            var population=populations.GetValue(scene,key=>new Population());var now=S.Clock();
            if(now<population.Next)return;
            population.Next=now.AddSeconds(3);
            var run=S.Load(p);if(run==null)return;
            if(population.Stage!=run.Stage){
                // A prior-stage life must never be relabelled to earn fresh credit.
                foreach(var body in scene.Map.Entities.Values.ToArray()){
                    Fight fight;if(!fights.TryGetValue(body,out fight) || !ReferenceEquals(fight.Scene,scene))throw new InvalidOperationException("Power Book population found an unregistered actor.");
                    Remove(scene,body);
                }
                population.Stage=run.Stage;
            }
            if(!S.AllowsFloor(run.Stage,scene.Floor))return;
            int[] kinds;
            switch(run.Stage){
                case 2:kinds=new[]{0};break;
                case 3:kinds=new[]{1};break;
                case 4:kinds=new[]{0,1};break;
                case 5:kinds=new[]{2};break;
                case 6:kinds=run.MaterialUid==0?new[]{3}:new int[0];break;
                case 7:kinds=new[]{2,3};break;
                case 8:kinds=new[]{4};break;
                case 9:kinds=run.MaterialUid==0?new[]{5}:new int[0];break;
                case 10:kinds=new[]{4,5};break;
                case 11:kinds=scene.Floor==3?new[]{6}:run.MaterialUid==0?new[]{4}:new int[0];break;
                default:return;
            }
            foreach(int kind in kinds){
                int needed=(kinds.Length==1?8:4)-scene.Map.Entities.Values.Count(b=>!b.Dead && b.MonsterInfo!=null && b.MonsterInfo.ID==W.FamilyStart+kind);
                // A fixed, bounded search on validated trial terrain; never loop
                // indefinitely if players, companions or corpses occupy cells.
                for(int radius=0;radius<=6 && needed>0 && scene.Map.Entities.Count<24;radius++)
                    for(int dx=-radius;dx<=radius && needed>0 && scene.Map.Entities.Count<24;dx++)
                        for(int dy=-radius;dy<=radius && needed>0 && scene.Map.Entities.Count<24;dy++){
                            if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=radius)continue;
                            ushort x=(ushort)(90+dx),y=(ushort)(50+dy);
                            if(Kernel.GetDistance(p.Entity.X,p.Entity.Y,x,y)<2 || scene.Map.Companions.Values.Any(b=>b.X==x && b.Y==y)
                                || scene.Map.Entities.Values.Any(b=>b.X==x && b.Y==y) || !scene.Map.Floor[x,y,MapObjectType.Monster,null])continue;
                            Spawn(scene,p,kind,x,y);needed--;
                        }
            }
        }
        private static void Remove(W.Scene scene,Entity body)
        {
            scene.Map.RemoveEntity(body);body.MonsterInfo.SendScreen(new Data(true){UID=body.UID,ID=Data.RemoveEntity});fights.Remove(body);
        }
        internal static void Prune(W.Scene scene)
        {
            if(!Monitor.IsEntered(scene.Owner.ItemSyncRoot))throw new InvalidOperationException("Power Book cleanup requires owner lock.");
            foreach(var body in scene.Map.Entities.Values.ToArray()){
                Fight fight;if(!fights.TryGetValue(body,out fight) || !body.Dead || !fight.Recorded || S.Clock()<fight.RemoveAt)continue;
                Remove(scene,body);
            }
        }
    }
}
