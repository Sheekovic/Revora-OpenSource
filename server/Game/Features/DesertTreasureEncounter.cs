using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using S=Revora.Game.Features.DesertTreasureStore;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static class DesertTreasureEncounter
    {
        internal const uint FamilyStart=9918280;
        private sealed class Fight {internal GameState Owner;internal Map Map;internal Entity Body;internal Guid Attempt;internal int Kind;internal byte Life;internal DateTime Until;internal DateTime? CorpseUntil;internal bool Recorded;}
        private static readonly object gate=new object();
        private static readonly Dictionary<uint,Fight> owners=new Dictionary<uint,Fight>();
        private static readonly ConditionalWeakTable<Entity,Fight> bodies=new ConditionalWeakTable<Entity,Fight>();
        internal static bool IsEncounter(Entity body){return body!=null && body.MonsterInfo!=null && body.MonsterInfo.ID>=FamilyStart && body.MonsterInfo.ID<FamilyStart+1;}
        private static bool World(Map map){Map current;MapsTable.MapInformation info;return map!=null && map.ID==1001 && map.BaseID==1001 && Kernel.Maps.TryGetValue(1001,out current) && ReferenceEquals(current,map) && MapsTable.MapInformations.TryGetValue(1001,out info) && info.ID==1001 && info.BaseID==1001 && info.Owner==0;}
        private static bool Current(Fight f){Entity body;Fight current;lock(gate){if(!owners.TryGetValue(f.Owner.Entity.UID,out current) || !ReferenceEquals(current,f))return false;}
            return World(f.Map) && S.Eligible(f.Owner) && f.Owner.Entity.Reborn==f.Life && f.Owner.Entity.MapID==1001 && ReferenceEquals(f.Owner.Map,f.Map)
                && S.Clock()<f.Until && f.Map.Entities.TryGetValue(f.Body.UID,out body) && ReferenceEquals(body,f.Body);
        }
        internal static bool Owns(GameState p,Entity body,Guid attempt,int kind){Fight f;return body!=null && bodies.TryGetValue(body,out f)
            && ReferenceEquals(f.Owner,p) && f.Attempt==attempt && f.Kind==kind && !f.Recorded && Current(f) && f.CorpseUntil.HasValue && S.Clock()>=f.CorpseUntil.Value.AddSeconds(-3) && S.Clock()<=f.CorpseUntil.Value;}
        private static Entity Encounter(Entity a,Entity b){return IsEncounter(a)?a:IsEncounter(b)?b:null;}
        private static bool Fighter(Fight f,Entity b){Entity pet;return b!=null && !b.Dead && b.MapID==1001 && (ReferenceEquals(b,f.Owner.Entity)
            || b.Companion && ReferenceEquals(b.Owner,f.Owner) && ReferenceEquals(f.Owner.Companion,b) && f.Map.Companions.TryGetValue(b.UID,out pet) && ReferenceEquals(pet,b));}
        internal static bool BlockAttack(Entity a,Entity b){var body=Encounter(a,b);Fight f;return body!=null && (!bodies.TryGetValue(body,out f) || f.Recorded || body.Dead || !Current(f) || !Fighter(f,ReferenceEquals(body,a)?b:a));}
        private sealed class Lease:IDisposable {private readonly object sync;internal Lease(object value){sync=value;}public void Dispose(){Monitor.Exit(sync);}}
        internal static IDisposable DamageLease(Entity a,Entity b){var body=Encounter(a,b);Fight f;return body!=null && bodies.TryGetValue(body,out f) && Monitor.TryEnter(f.Owner.ItemSyncRoot)?new Lease(f.Owner.ItemSyncRoot):null;}
        internal static bool BlockDamage(Entity a,Entity b){if(BlockAttack(a,b))return true;var body=Encounter(a,b);Fight f;return body!=null && (!bodies.TryGetValue(body,out f) || !Monitor.IsEntered(f.Owner.ItemSyncRoot));}
        internal static MonsterInformation Profile(int kind){
            if(kind!=0)throw new ArgumentOutOfRangeException("kind");
            MonsterInformation source;if(!MonsterInformation.MonsterInformations.TryGetValue(20,out source) || source.Name!="TombBat" || source.Mesh!=206)throw new InvalidOperationException("Desert Treasure bat appearance changed.");
            return new MonsterInformation{ID=FamilyStart,Name="JinxTombBat",Mesh=206,Level=105,Hitpoints=100000,
                MinAttack=1000,MaxAttack=1500,Defence=500,AttackType=1,AttackRange=2,ViewRange=18,
                AttackSpeed=1500,MoveSpeed=800,RunSpeed=500,RespawnTime=1000000000};
        }
        internal static bool HasOwner(uint uid){lock(gate)return owners.ContainsKey(uid);}
        internal static Entity Start(GameState p){
            if(!S.Eligible(p) || p.Entity.MapID!=1001 || p.Map.ID!=1001 || !World(p.Map) || HasOwner(p.Entity.UID) || !SessionInstall.TryEnter(p.Entity.UID))return null;
            try{lock(p.ItemSyncRoot){int kind=S.EncounterKind(p);if(kind<0)return null;
                using(var sha=System.Security.Cryptography.SHA256.Create())using(var file=System.IO.File.OpenRead(System.IO.Path.Combine(Constants.DMapsPath,"maps","1001.map")))
                    if(System.BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant()!="89bdcb2d247add85d8d7b888c8f90d7d4d5127e51499479ca988a0c9884624fc")throw new InvalidOperationException("Desert Treasure terrain changed.");
                ushort x=728,y=676;
                if(Kernel.GetDistance(p.Entity.X,p.Entity.Y,x,y)>8)return null;
                lock(gate){if(owners.Count>=32 || owners.ContainsKey(p.Entity.UID))return null;
                    var map=p.Map;ushort sx=0;
                    for(int i=0;i<3;i++){ushort cx=(ushort)(x+i*2);
                        if(map.Floor[cx,y,MapObjectType.Monster,null] && !map.Entities.Values.Any(b=>b.X==cx && b.Y==y)
                            && !map.Npcs.Values.Any(n=>n.X==cx && n.Y==y) && !map.Companions.Values.Any(b=>b.X==cx && b.Y==y)
                            && !Kernel.GamePool.Values.Any(o=>o.Entity!=null && o.Entity.MapID==1001 && o.Entity.X==cx && o.Entity.Y==y)){sx=cx;break;}}
                    if(sx==0)return null;
                    var info=Profile(kind);info.BoundX=sx;info.BoundY=y;info.BoundCX=info.BoundCY=1;
                    var body=new Entity(EntityFlag.Monster,false){UID=map.EntityUIDCounter.Next,MapID=1001,X=sx,Y=y,MonsterInfo=info,MapObjType=MapObjectType.Monster,
                        Name=info.Name,Body=info.Mesh,Level=info.Level,MinAttack=info.MinAttack,MaxAttack=info.MaxAttack,Defence=info.Defence,IsDropped=true};
                    while(map.Entities.ContainsKey(body.UID) || map.Npcs.ContainsKey(body.UID) || map.Companions.ContainsKey(body.UID) || Kernel.GamePool.ContainsKey(body.UID))body.UID=map.EntityUIDCounter.Next;
                    info.Owner=body;body.Hitpoints=body.MaxHitpoints=info.Hitpoints;body.SendUpdates=true;
                    var f=new Fight{Owner=p,Map=map,Body=body,Kind=kind,Life=p.Entity.Reborn,Attempt=Guid.NewGuid(),Until=S.Clock().AddMinutes(5)};
                    owners.Add(p.Entity.UID,f);bodies.Add(body,f);try{map.AddEntity2(body);body.SendSpawn(p,false);return body;}catch{Remove(f);throw;}
                }
            }}finally{SessionInstall.Exit(p.Entity.UID);}
        }
        internal static bool TryDie(Entity body,Entity killer){
            if(!IsEncounter(body))return false;Fight f;if(!bodies.TryGetValue(body,out f) || !Monitor.TryEnter(f.Owner.ItemSyncRoot))return true;
            try{if(f.Recorded || body.Dead || !Current(f) || !Fighter(f,killer) || Kernel.GetDistance(f.Owner.Entity.X,f.Owner.Entity.Y,body.X,body.Y)>Constants.pScreenDistance)return true;
                body.DieCore(f.Owner.Entity);f.CorpseUntil=S.Clock().AddSeconds(3);
            }catch(Exception ex){f.Recorded=true;Q.Fail(f.Owner,"treasure-encounter-death",ex);}finally{Monitor.Exit(f.Owner.ItemSyncRoot);}return true;
        }
        internal static bool Capture(GameState p,uint bottle){
            if(p==null || !Monitor.IsEntered(p.ItemSyncRoot))return false;Fight f;lock(gate)if(!owners.TryGetValue(p.Entity.UID,out f))return false;
            if(!ReferenceEquals(f.Owner,p) || !Owns(p,f.Body,f.Attempt,0) || Kernel.GetDistance(p.Entity.X,p.Entity.Y,f.Body.X,f.Body.Y)>3)return false;
            if(!S.CaptureRelic(p,f.Body,f.Attempt,bottle,()=>Owns(p,f.Body,f.Attempt,0)))return false;f.Recorded=true;return true;
        }
        private static void Remove(Fight f){Entity body;if(f.Map.Entities.TryGetValue(f.Body.UID,out body) && ReferenceEquals(body,f.Body)){
                f.Map.RemoveEntity(body);body.MonsterInfo.SendScreen(new Data(true){UID=body.UID,ID=Data.RemoveEntity});}
            bodies.Remove(f.Body);lock(gate){Fight current;if(owners.TryGetValue(f.Owner.Entity.UID,out current) && ReferenceEquals(current,f))owners.Remove(f.Owner.Entity.UID);}
        }
        internal static void Tick(){Fight[] pending;lock(gate)pending=owners.Values.ToArray();foreach(var f in pending){if(!Monitor.TryEnter(f.Owner.ItemSyncRoot))continue;
                try{if(f.Recorded || f.Body.Dead && (!f.CorpseUntil.HasValue || S.Clock()>f.CorpseUntil.Value) || !Current(f) || S.EncounterKind(f.Owner)!=f.Kind)Remove(f);}catch(Exception ex){Q.Fail(f.Owner,"treasure-encounter-cleanup",ex);}finally{Monitor.Exit(f.Owner.ItemSyncRoot);}}
        }
    }
}
