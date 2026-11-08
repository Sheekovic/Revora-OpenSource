using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using S=Revora.Game.Features.FranticMonkeysStore;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static class FranticMonkeysApes
    {
        private sealed class Fight {internal GameState Owner;internal Map Map;internal Entity Body;internal Guid Attempt;internal DateTime Until;internal bool Won,Recorded;internal DateTime Next;}
        private static readonly object gate=new object();
        private static readonly Dictionary<uint,Fight> owners=new Dictionary<uint,Fight>();
        private static readonly ConditionalWeakTable<Entity,Fight> bodies=new ConditionalWeakTable<Entity,Fight>();
        internal static bool IsEncounter(Entity b){return b!=null && b.MonsterInfo!=null && b.MonsterInfo.ID>=FranticMonkeysScenes.FamilyStart+1 && b.MonsterInfo.ID<=FranticMonkeysScenes.FamilyStart+2;}
        private static bool Registered(Fight f){lock(gate){Fight current;return owners.TryGetValue(f.Owner.Entity.UID,out current) && ReferenceEquals(current,f);}}
        private static bool Live(Fight f){return Registered(f) && S.Eligible(f.Owner) && ReferenceEquals(f.Owner.Map,f.Map) && f.Owner.Entity.MapID==1020 && FranticMonkeysScenes.Clock()<f.Until;}
        private static bool Current(Fight f){Entity body;return f.Body!=null && Registered(f) && S.Eligible(f.Owner) && ReferenceEquals(f.Owner.Map,f.Map)
            && f.Owner.Entity.MapID==1020 && FranticMonkeysScenes.Clock()<f.Until && f.Map.Entities.TryGetValue(f.Body.UID,out body) && ReferenceEquals(body,f.Body);}
        private static bool Fighter(Fight f,Entity b){Entity pet;return b!=null && !b.Dead && b.MapID==1020 && (ReferenceEquals(b,f.Owner.Entity)
            || b.Companion && ReferenceEquals(b.Owner,f.Owner) && ReferenceEquals(f.Owner.Companion,b) && f.Map.Companions.TryGetValue(b.UID,out pet) && ReferenceEquals(pet,b));}
        private static Entity Encounter(Entity a,Entity b){return IsEncounter(a)?a:IsEncounter(b)?b:null;}
        internal static bool BlockAttack(Entity a,Entity b){var body=Encounter(a,b);if(body==null)return false;Fight f;
            return !bodies.TryGetValue(body,out f) || f.Won || body.Dead || !Current(f) || !Fighter(f,ReferenceEquals(body,a)?b:a);}
        private sealed class Lease:IDisposable {private readonly object sync;internal Lease(object value){sync=value;}public void Dispose(){Monitor.Exit(sync);}}
        internal static IDisposable DamageLease(Entity a,Entity b){var body=Encounter(a,b);Fight f;
            return body!=null && bodies.TryGetValue(body,out f) && Monitor.TryEnter(f.Owner.ItemSyncRoot)?new Lease(f.Owner.ItemSyncRoot):null;}
        internal static bool BlockDamage(Entity a,Entity b){if(BlockAttack(a,b))return true;var body=Encounter(a,b);Fight f;
            return body!=null && (!bodies.TryGetValue(body,out f) || !Monitor.IsEntered(f.Owner.ItemSyncRoot));}

        private static bool Near(GameState p){return S.Eligible(p) && p.Entity.MapID==1020 && p.Map.ID==1020 && Kernel.GetDistance(p.Entity.X,p.Entity.Y,795,471)<=3;}
        internal static bool HasOwner(uint uid){lock(gate)return owners.ContainsKey(uid);}
        private static bool Spawn(Fight f,int kind){
            ushort x=0,y=0;var p=f.Owner;var map=f.Map;
            for(int i=0;i<FranticMonkeysScenes.SpawnPoints.GetLength(0);i++){
                ushort cx=FranticMonkeysScenes.SpawnPoints[i,0],cy=FranticMonkeysScenes.SpawnPoints[i,1];
                if(!map.Floor[cx,cy,MapObjectType.Monster,null] || map.Entities.Values.Any(b=>b.X==cx && b.Y==cy)
                    || map.Companions.Values.Any(b=>b.X==cx && b.Y==cy) || map.Npcs.Values.Any(n=>n.X==cx && n.Y==cy)
                    || Kernel.GamePool.Values.Any(o=>o.Entity!=null && o.Entity.MapID==1020 && o.Entity.X==cx && o.Entity.Y==cy))continue;
                x=cx;y=cy;break;
            }
            if(x==0)return false;
            var info=FranticMonkeysScenes.Profile(kind);info.BoundX=x;info.BoundY=y;info.BoundCX=info.BoundCY=1;
            var body=new Entity(EntityFlag.Monster,false){UID=map.EntityUIDCounter.Next,MapID=1020,X=x,Y=y,MonsterInfo=info,MapObjType=MapObjectType.Monster,
                Name=info.Name,Body=info.Mesh,Level=info.Level,MinAttack=info.MinAttack,MaxAttack=info.MaxAttack,Defence=info.Defence,IsDropped=true};
            while(map.Entities.ContainsKey(body.UID) || map.Npcs.ContainsKey(body.UID) || map.Companions.ContainsKey(body.UID) || Kernel.GamePool.ContainsKey(body.UID))body.UID=map.EntityUIDCounter.Next;
            info.Owner=body;body.Hitpoints=body.MaxHitpoints=info.Hitpoints;body.SendUpdates=true;
            f.Body=body;f.Recorded=false;bodies.Add(body,f);try{map.AddEntity2(body);body.SendSpawn(p,false);return true;}catch{RemoveBody(f);throw;}
        }
        internal static bool Start(GameState p,ConquerItem vial){
            if(vial==null || vial.ID!=721701 || !Near(p) || !SessionInstall.TryEnter(p.Entity.UID))return false;
            try{lock(p.ItemSyncRoot){ConquerItem owned;if(!Near(p) || !p.Inventory.TryGetItem(vial.UID,out owned) || !ReferenceEquals(vial,owned))return false;
                FranticMonkeysScenes.ValidateTerrain();var f=new Fight{Owner=p,Map=p.Map,Attempt=Guid.NewGuid(),Until=FranticMonkeysScenes.Clock().AddMinutes(5)};
                lock(gate){if(owners.Count>=32 || owners.ContainsKey(p.Entity.UID))return false;owners.Add(p.Entity.UID,f);}
                try{
                    if(!S.ReserveApes(p,vial.UID,f.Attempt,()=>Registered(f) && Near(p))){Remove(f);return false;}
                    var saved=S.OpenApes(p);if(saved==null || saved.ID!=f.Attempt)throw new InvalidOperationException("Frantic ape reservation missing.");f.Until=saved.Until;
                    // Spawn under the registry gate so simultaneous encounters cannot choose the same cell.
                    lock(gate)Spawn(f,1);f.Next=FranticMonkeysScenes.Clock().AddSeconds(1);return true;
                }catch{Remove(f);throw;}
            }}catch(Exception ex){Q.Fail(p,"frantic-apes-start",ex);return false;}finally{SessionInstall.Exit(p.Entity.UID);}
        }
        internal static bool TryDie(Entity body,Entity killer){
            if(!IsEncounter(body))return false;Fight f;if(!bodies.TryGetValue(body,out f) || !Monitor.TryEnter(f.Owner.ItemSyncRoot))return true;
            try{
                if(f.Won || f.Recorded || !ReferenceEquals(body,f.Body) || !Current(f) || !Fighter(f,killer) || Kernel.GetDistance(f.Owner.Entity.X,f.Owner.Entity.Y,body.X,body.Y)>Constants.pScreenDistance)return true;
                if(!body.Dead)body.DieCore(f.Owner.Entity);
                if(!S.ApeDeath(f.Owner,f.Attempt,body,()=>Current(f)))throw new InvalidOperationException("Frantic ape death was not committed.");
                f.Recorded=true;f.Won=body.MonsterInfo.ID==FranticMonkeysScenes.FamilyStart+2;f.Next=FranticMonkeysScenes.Clock().AddSeconds(1);
                if(f.Won)S.DeliverSword(f.Owner,f.Attempt,()=>Current(f));
            }catch(Exception ex){Q.Fail(f.Owner,"frantic-apes-death",ex);}finally{Monitor.Exit(f.Owner.ItemSyncRoot);}return true;
        }
        private static void RemoveBody(Fight f){
            var body=f.Body;if(body==null)return;Entity current;
            if(f.Map.Entities.TryGetValue(body.UID,out current) && ReferenceEquals(current,body)){f.Map.RemoveEntity(body);body.MonsterInfo.SendScreen(new Data(true){UID=body.UID,ID=Data.RemoveEntity});}
            bodies.Remove(body);f.Body=null;
        }
        private static void Remove(Fight f){RemoveBody(f);lock(gate){Fight current;if(owners.TryGetValue(f.Owner.Entity.UID,out current) && ReferenceEquals(current,f))owners.Remove(f.Owner.Entity.UID);}}
        internal static bool Recover(GameState p){
            if(!S.Eligible(p) || !Monitor.IsEntered(p.ItemSyncRoot) || HasOwner(p.Entity.UID))return false;var fight=S.OpenApes(p);if(fight==null)return false;
            return fight.State==0?S.AbandonApes(p,fight.ID,()=>S.Eligible(p) && !HasOwner(p.Entity.UID)):S.DeliverSword(p,fight.ID,()=>S.Eligible(p) && !HasOwner(p.Entity.UID));
        }
        internal static void Tick(){
            Fight[] pending;lock(gate)pending=owners.Values.ToArray();
            foreach(var f in pending){if(!Monitor.TryEnter(f.Owner.ItemSyncRoot))continue;try{
                if(!Registered(f))continue;
                if(!Live(f)){Remove(f);if(S.Eligible(f.Owner))Recover(f.Owner);continue;}
                var now=FranticMonkeysScenes.Clock();if(now<f.Next)continue;f.Next=now.AddSeconds(3);
                if(f.Recorded)RemoveBody(f);
                var saved=S.OpenApes(f.Owner);
                if(saved==null || saved.ID!=f.Attempt){Remove(f);continue;}
                if(saved.State==1){f.Won=true;S.DeliverSword(f.Owner,f.Attempt,()=>Live(f));Remove(f);continue;}
                if(f.Body!=null && (!Current(f) || f.Body.Dead)){Remove(f);Recover(f.Owner);continue;}
                if(f.Body==null)lock(gate)Spawn(f,saved.Kills<2?1:2);
            }catch(Exception ex){Q.Fail(f.Owner,"frantic-apes-tick",ex);}finally{Monitor.Exit(f.Owner.ItemSyncRoot);}}
        }
    }
}
