using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using S=Revora.Game.Features.SpunGoldStore;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static class SpunGoldCombat
    {
        private sealed class Fight {internal GameState Owner;internal Map Map;internal Entity Body;internal Guid Attempt;internal DateTime Until;internal bool Won;}
        private static readonly object gate=new object();
        private static readonly Dictionary<uint,Fight> owners=new Dictionary<uint,Fight>();
        private static readonly ConditionalWeakTable<Entity,Fight> bodies=new ConditionalWeakTable<Entity,Fight>();
        internal static bool IsEncounter(Entity b){return b!=null && b.MonsterInfo!=null && b.MonsterInfo.ID==S.Family;}
        private static bool Registered(Fight f){lock(gate){Fight current;return owners.TryGetValue(f.Owner.Entity.UID,out current) && ReferenceEquals(current,f);}}
        private static bool Current(Fight f){Entity body;return Registered(f) && S.Eligible(f.Owner) && ReferenceEquals(f.Owner.Map,f.Map)
            && f.Owner.Entity.MapID==1011 && SpunGoldAppearance.Clock()<f.Until && f.Map.Entities.TryGetValue(f.Body.UID,out body) && ReferenceEquals(body,f.Body);}
        private static bool Fighter(Fight f,Entity b){Entity pet;return b!=null && !b.Dead && b.MapID==1011 && (ReferenceEquals(b,f.Owner.Entity)
            || b.Companion && ReferenceEquals(b.Owner,f.Owner) && ReferenceEquals(f.Owner.Companion,b) && f.Map.Companions.TryGetValue(b.UID,out pet) && ReferenceEquals(pet,b));}
        private static Entity Encounter(Entity a,Entity b){return IsEncounter(a)?a:IsEncounter(b)?b:null;}
        internal static bool BlockAttack(Entity a,Entity b){var body=Encounter(a,b);if(body==null)return false;Fight f;
            return !bodies.TryGetValue(body,out f) || f.Won || body.Dead || !Current(f) || !Fighter(f,ReferenceEquals(body,a)?b:a);}
        private sealed class Lease:IDisposable {private readonly object sync;internal Lease(object value){sync=value;}public void Dispose(){Monitor.Exit(sync);}}
        internal static IDisposable DamageLease(Entity a,Entity b){var body=Encounter(a,b);Fight f;
            return body!=null && bodies.TryGetValue(body,out f) && Monitor.TryEnter(f.Owner.ItemSyncRoot)?new Lease(f.Owner.ItemSyncRoot):null;}
        internal static bool BlockDamage(Entity a,Entity b){if(BlockAttack(a,b))return true;var body=Encounter(a,b);Fight f;
            return body!=null && (!bodies.TryGetValue(body,out f) || !Monitor.IsEntered(f.Owner.ItemSyncRoot));}

        // The quest coordinator owns the session-install lease and item lock.
        internal static Entity Summon(GameState p,Guid disguise,Func<bool> context)
        {
            if(!S.Eligible(p) || !Monitor.IsEntered(p.ItemSyncRoot) || context==null || !context() || p.Entity.MapID!=1011
                || Kernel.GetDistance(p.Entity.X,p.Entity.Y,100,430)>3 || !SpunGoldAppearance.Owns(p,disguise))return null;
            MonsterInformation source;if(!MonsterInformation.MonsterInformations.TryGetValue(2141,out source) || source.Name!="CloudtheLustful"
                || source.Level!=32 || source.Hitpoints!=1600 || source.Guard || source.Reviver || source.Boss || source.Mesh==0)throw new InvalidOperationException("Spun Gold Cloud catalog changed.");
            var map=p.Map;ushort x=0,y=0;
            // These 25 cells are open in both audited Phoenix terrain hashes.
            for(ushort cy=428;cy<=432 && x==0;cy++)for(ushort cx=97;cx<=101;cx++){
                if(!map.Floor[cx,cy,MapObjectType.Monster,null] || map.Entities.Values.Any(b=>b.X==cx && b.Y==cy)
                    || map.Companions.Values.Any(b=>b.X==cx && b.Y==cy) || map.Npcs.Values.Any(n=>n.X==cx && n.Y==cy)
                    || Kernel.GamePool.Values.Any(o=>o.Entity!=null && o.Entity.MapID==1011 && o.Entity.X==cx && o.Entity.Y==cy))continue;
                lock(gate)if(owners.Values.Any(v=>ReferenceEquals(v.Map,map) && v.Body.X==cx && v.Body.Y==cy))continue;
                x=cx;y=cy;break;
            }
            if(x==0)return null;
            var info=source.Copy();info.ID=S.Family;info.BoundX=x;info.BoundY=y;info.BoundCX=info.BoundCY=1;info.RespawnTime=1000000000;info.OwnItemID=info.OwnItemRate=0;
            var body=new Entity(EntityFlag.Monster,false){UID=map.EntityUIDCounter.Next,MapID=1011,X=x,Y=y,MonsterInfo=info,MapObjType=MapObjectType.Monster,
                Name=info.Name,Body=info.Mesh,Level=info.Level,MinAttack=info.MinAttack,MaxAttack=info.MaxAttack,Defence=info.Defence,IsDropped=true};
            while(map.Entities.ContainsKey(body.UID) || map.Npcs.ContainsKey(body.UID) || map.Companions.ContainsKey(body.UID) || Kernel.GamePool.ContainsKey(body.UID))body.UID=map.EntityUIDCounter.Next;
            info.Owner=body;body.Hitpoints=body.MaxHitpoints=1600;body.SendUpdates=true;
            var f=new Fight{Owner=p,Map=map,Body=body,Until=SpunGoldAppearance.Clock().AddMinutes(5)};
            lock(gate){if(owners.ContainsKey(p.Entity.UID) || owners.Count>=64 || owners.Values.Any(v=>ReferenceEquals(v.Map,map) && v.Body.X==x && v.Body.Y==y))return null;owners.Add(p.Entity.UID,f);bodies.Add(body,f);}
            try{
                var id=S.Reserve(p,disguise,body.UID,()=>Registered(f) && context());
                if(!id.HasValue){Remove(f);return null;}f.Attempt=id.Value;
                map.AddEntity2(body);body.SendSpawn(p,false);return body;
            }catch{Remove(f);throw;} // A committed interrupted reservation is recovered from its durable attempt.
        }
        internal static bool TryDie(Entity body,Entity killer)
        {
            if(!IsEncounter(body))return false;Fight f;if(!bodies.TryGetValue(body,out f) || !Monitor.TryEnter(f.Owner.ItemSyncRoot))return true;
            try{
                if(f.Won || !Current(f) || !Fighter(f,killer) || Kernel.GetDistance(f.Owner.Entity.X,f.Owner.Entity.Y,body.X,body.Y)>Constants.pScreenDistance)return true;
                if(!body.Dead)body.DieCore(f.Owner.Entity);
                if(S.Win(f.Owner,f.Attempt,body,()=>Current(f))){f.Won=true;f.Until=SpunGoldAppearance.Clock().AddSeconds(1);}
            }catch(Exception ex){Q.Fail(f.Owner,"spun-gold-combat",ex);}finally{Monitor.Exit(f.Owner.ItemSyncRoot);}return true;
        }
        private static void Remove(Fight f)
        {
            try{Entity current;if(f.Map.Entities.TryGetValue(f.Body.UID,out current) && ReferenceEquals(current,f.Body)){
                f.Map.RemoveEntity(f.Body);f.Body.MonsterInfo.SendScreen(new Data(true){UID=f.Body.UID,ID=Data.RemoveEntity});
            }}finally{lock(gate){Fight owner;if(owners.TryGetValue(f.Owner.Entity.UID,out owner) && ReferenceEquals(owner,f))owners.Remove(f.Owner.Entity.UID);bodies.Remove(f.Body);}}
        }
        internal static bool RecoverAttempt(GameState p,Func<bool> context)
        {
            if(!S.Eligible(p) || !Monitor.IsEntered(p.ItemSyncRoot) || context==null || !context())return false;
            Fight f;lock(gate)owners.TryGetValue(p.Entity.UID,out f);
            // Replacement sessions cannot mutate a former owner's runtime body under the new owner's lock.
            if(f!=null)return false;
            var run=S.Load(p);return run!=null && run.Stage==4 && run.Attempt.HasValue && S.Abandon(p,run.Attempt.Value,context);
        }
        internal static void Tick()
        {
            Fight[] snapshot;lock(gate)snapshot=owners.Values.ToArray();
            foreach(var f in snapshot){if(!Monitor.TryEnter(f.Owner.ItemSyncRoot))continue;try{
                if(!Registered(f))continue;
                if(Current(f) && (!f.Body.Dead || f.Won))continue;
                Remove(f);
                if(!f.Won && S.Eligible(f.Owner))RecoverAttempt(f.Owner,()=>S.Eligible(f.Owner));
            }catch(Exception ex){Q.Fail(f.Owner,"spun-gold-cleanup",ex);}finally{Monitor.Exit(f.Owner.ItemSyncRoot);}}
        }
    }
}
