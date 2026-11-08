using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using S=Revora.Game.Features.TempestWingsStore;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static class TempestWingsEncounters
    {
        internal const uint FamilyStart=9905500;
        private sealed class Fight {internal GameState Owner;internal Map Map;internal Entity Body;internal Guid Attempt;internal byte Life;internal DateTime Until;internal bool Recorded;}
        private static readonly object gate=new object();
        private static readonly Dictionary<uint,Fight> owners=new Dictionary<uint,Fight>();
        private static readonly ConditionalWeakTable<Entity,Fight> bodies=new ConditionalWeakTable<Entity,Fight>();
        internal static bool IsEncounter(Entity body){return body!=null && body.MonsterInfo!=null && body.MonsterInfo.ID==FamilyStart;}
        private static bool Current(Fight f){Entity body;Fight current;lock(gate){if(!owners.TryGetValue(f.Owner.Entity.UID,out current) || !ReferenceEquals(current,f))return false;}
            return HouseWorld(f.Owner,f.Map) && S.Eligible(f.Owner) && f.Owner.Entity.Reborn==f.Life && f.Owner.Entity.MapID==f.Map.ID && ReferenceEquals(f.Owner.Map,f.Map)
                && S.Clock()<f.Until && f.Map.Entities.TryGetValue(f.Body.UID,out body) && ReferenceEquals(body,f.Body);
        }
        internal static bool Owns(GameState p,Entity body,Guid attempt){Fight f;return body!=null && bodies.TryGetValue(body,out f)
            && ReferenceEquals(f.Owner,p) && f.Attempt==attempt && !f.Recorded && Current(f);}
        private static bool HouseWorld(GameState p,Map map){var scene=TempestWingsScenes.Find(p);return scene!=null && scene.Entered && !scene.Completed && ReferenceEquals(scene.Map,map) && p.Entity.MapID==map.ID && TempestWingsScenes.Registered(scene,p) && S.Clock()<scene.Until;}
        internal static void Cancel(GameState p){Fight f;lock(gate){if(!owners.TryGetValue(p.Entity.UID,out f) || !ReferenceEquals(f.Owner,p))return;}Remove(f);}
        private static Entity Encounter(Entity a,Entity b){return IsEncounter(a)?a:IsEncounter(b)?b:null;}
        private static bool Fighter(Fight f,Entity b){Entity pet;return b!=null && !b.Dead && b.MapID==f.Map.ID && (ReferenceEquals(b,f.Owner.Entity)
            || b.Companion && ReferenceEquals(b.Owner,f.Owner) && ReferenceEquals(f.Owner.Companion,b) && f.Map.Companions.TryGetValue(b.UID,out pet) && ReferenceEquals(pet,b));}
        internal static bool BlockAttack(Entity a,Entity b){var body=Encounter(a,b);Fight f;return body!=null && (!bodies.TryGetValue(body,out f) || f.Recorded || body.Dead || !Current(f) || !Fighter(f,ReferenceEquals(body,a)?b:a));}
        private sealed class Lease:IDisposable {private readonly object sync;internal Lease(object value){sync=value;}public void Dispose(){Monitor.Exit(sync);}}
        internal static IDisposable DamageLease(Entity a,Entity b){var body=Encounter(a,b);Fight f;return body!=null && bodies.TryGetValue(body,out f) && Monitor.TryEnter(f.Owner.ItemSyncRoot)?new Lease(f.Owner.ItemSyncRoot):null;}
        internal static bool BlockDamage(Entity a,Entity b){if(BlockAttack(a,b))return true;var body=Encounter(a,b);Fight f;return body!=null && (!bodies.TryGetValue(body,out f) || !Monitor.IsEntered(f.Owner.ItemSyncRoot));}
        // Revora level100 trial tuning, using the verified SkyRoc appearance only.
        internal static MonsterInformation Profile(){
            MonsterInformation source;
            if(!MonsterInformation.MonsterInformations.TryGetValue(7696,out source) || source.Name!="SkyRoc" || source.Mesh!=997 || source.Guard || source.Reviver || !source.Boss)
                throw new InvalidOperationException("Tempest Roc appearance changed.");
            return new MonsterInformation{ID=FamilyStart,Name="SkyRoc",Mesh=997,Level=100,Hitpoints=100000,
                MinAttack=800,MaxAttack=1200,Defence=350,AttackType=1,AttackRange=2,ViewRange=18,AttackSpeed=1500,
                MoveSpeed=800,RunSpeed=500,RespawnTime=1000000000};
        }
        internal static bool HasOwner(uint uid){lock(gate)return owners.ContainsKey(uid);}
        internal static Entity Start(GameState p){
            if(!S.Eligible(p) || !HouseWorld(p,p.Map) || HasOwner(p.Entity.UID) || !SessionInstall.TryEnter(p.Entity.UID))return null;
            try{lock(p.ItemSyncRoot){if(!HouseWorld(p,p.Map) || S.Load(p).Stage!=2)return null;
                ushort x=136,y=103;
                lock(gate){if(owners.Count>=32 || owners.ContainsKey(p.Entity.UID))return null;
                    var map=p.Map;ushort sx=0;
                    for(int i=0;i<1;i++){ushort cx=(ushort)(x+i*2);
                        if(map.Floor[cx,y,MapObjectType.Monster,null] && !map.Entities.Values.Any(b=>b.X==cx && b.Y==y)
                            && !map.Npcs.Values.Any(n=>n.X==cx && n.Y==y) && !map.Companions.Values.Any(b=>b.X==cx && b.Y==y)
                            && !Kernel.GamePool.Values.Any(o=>o.Entity!=null && o.Entity.MapID==map.ID && o.Entity.X==cx && o.Entity.Y==y)){sx=cx;break;}}
                    if(sx==0)return null;
                    var info=Profile();info.BoundX=sx;info.BoundY=y;info.BoundCX=info.BoundCY=1;
                    var body=new Entity(EntityFlag.Monster,false){UID=map.EntityUIDCounter.Next,MapID=map.ID,X=sx,Y=y,MonsterInfo=info,MapObjType=MapObjectType.Monster,
                        Name=info.Name,Body=info.Mesh,Level=info.Level,MinAttack=info.MinAttack,MaxAttack=info.MaxAttack,Defence=info.Defence,IsDropped=true};
                    while(map.Entities.ContainsKey(body.UID) || map.Npcs.ContainsKey(body.UID) || map.Companions.ContainsKey(body.UID) || Kernel.GamePool.ContainsKey(body.UID))body.UID=map.EntityUIDCounter.Next;
                    info.Owner=body;body.Hitpoints=body.MaxHitpoints=info.Hitpoints;body.SendUpdates=true;
                    var f=new Fight{Owner=p,Map=map,Body=body,Life=p.Entity.Reborn,Attempt=Guid.NewGuid(),Until=TempestWingsScenes.Find(p).Until};
                    owners.Add(p.Entity.UID,f);bodies.Add(body,f);try{map.AddEntity2(body);body.SendSpawn(p,false);return body;}catch{Remove(f);throw;}
                }
            }}finally{SessionInstall.Exit(p.Entity.UID);}
        }
        internal static bool TryDie(Entity body,Entity killer){
            if(!IsEncounter(body))return false;Fight f;if(!bodies.TryGetValue(body,out f) || !Monitor.TryEnter(f.Owner.ItemSyncRoot))return true;
            try{if(f.Recorded || !Current(f) || !Fighter(f,killer) || Kernel.GetDistance(f.Owner.Entity.X,f.Owner.Entity.Y,body.X,body.Y)>Constants.pScreenDistance)return true;
                if(!body.Dead)body.DieCore(f.Owner.Entity);
                if(!S.RocKill(f.Owner,body,f.Attempt,()=>Current(f)))throw new InvalidOperationException("Tempest owned kill was not saved.");
                f.Recorded=true;
                S.Feather(f.Owner,()=>S.InScene(f.Owner));
                TempestWingsQuest.Refresh(f.Owner);

            }catch(Exception ex){f.Recorded=true;Q.Fail(f.Owner,"tempest-roc-death",ex);}finally{Monitor.Exit(f.Owner.ItemSyncRoot);}return true;
        }
        internal static bool DriveOwned(GameState viewer,Entity body,Time32 now){
            if(!IsEncounter(body))return false;Fight f;
            if(!bodies.TryGetValue(body,out f) || !ReferenceEquals(viewer,f.Owner) || !Monitor.TryEnter(f.Owner.ItemSyncRoot))return true;
            try{if(!f.Recorded && !body.Dead && Current(f))Screen.DriveMonsterAt(body,f.Owner.Entity,now);}
            finally{Monitor.Exit(f.Owner.ItemSyncRoot);}return true;
        }
        private static void Remove(Fight f){Entity body;if(f.Map.Entities.TryGetValue(f.Body.UID,out body) && ReferenceEquals(body,f.Body)){
                f.Map.RemoveEntity(body);body.MonsterInfo.SendScreen(new Data(true){UID=body.UID,ID=Data.RemoveEntity});}
            bodies.Remove(f.Body);lock(gate){Fight current;if(owners.TryGetValue(f.Owner.Entity.UID,out current) && ReferenceEquals(current,f))owners.Remove(f.Owner.Entity.UID);}
        }
        internal static void Tick(){Fight[] pending;lock(gate)pending=owners.Values.ToArray();foreach(var f in pending){if(!Monitor.TryEnter(f.Owner.ItemSyncRoot))continue;
                try{if(f.Recorded || f.Body.Dead || !Current(f))Remove(f);}catch(Exception ex){Q.Fail(f.Owner,"tempest-roc-cleanup",ex);}finally{Monitor.Exit(f.Owner.ItemSyncRoot);}}
        }
    }
}
