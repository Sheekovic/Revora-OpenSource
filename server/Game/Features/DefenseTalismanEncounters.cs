using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using S=Revora.Game.Features.DefenseTalismanStore;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static class DefenseTalismanEncounters
    {
        internal const uint FamilyStart=9905200;
        private sealed class Fight {internal GameState Owner;internal Map Map;internal Entity Body;internal Guid Attempt;internal int Kind;internal byte Life;internal DateTime Until;internal bool Recorded;}
        private static readonly object gate=new object();
        private static readonly Dictionary<uint,Fight> owners=new Dictionary<uint,Fight>();
        private static readonly ConditionalWeakTable<Entity,Fight> bodies=new ConditionalWeakTable<Entity,Fight>();
        internal static bool IsEncounter(Entity body){return body!=null && body.MonsterInfo!=null && body.MonsterInfo.ID>=FamilyStart && body.MonsterInfo.ID<FamilyStart+11;}
        private static bool World(Map map){Map current;MapsTable.MapInformation info;return map!=null && map.ID==map.BaseID && (map.ID==1000 || map.ID==1015) && Kernel.Maps.TryGetValue(map.ID,out current) && ReferenceEquals(current,map) && MapsTable.MapInformations.TryGetValue(map.ID,out info) && info.ID==map.ID && info.BaseID==map.ID && info.Owner==0;}
        private static bool Current(Fight f){Entity body;Fight current;lock(gate){if(!owners.TryGetValue(f.Owner.Entity.UID,out current) || !ReferenceEquals(current,f))return false;}
            return (PrivateKind(f.Kind)?HouseWorld(f.Owner,f.Map):World(f.Map) && f.Map.ID==(f.Kind>=3?1000:1015)) && S.Eligible(f.Owner) && f.Owner.Entity.Reborn==f.Life && f.Owner.Entity.MapID==f.Map.ID && ReferenceEquals(f.Owner.Map,f.Map)
                && S.Clock()<f.Until && f.Map.Entities.TryGetValue(f.Body.UID,out body) && ReferenceEquals(body,f.Body);
        }
        internal static bool Owns(GameState p,Entity body,Guid attempt,int kind){Fight f;return body!=null && bodies.TryGetValue(body,out f)
            && ReferenceEquals(f.Owner,p) && f.Attempt==attempt && f.Kind==kind && !f.Recorded && Current(f);}
        private static bool HouseWorld(GameState p,Map map){var scene=DefenseTalismanScenes.Find(p);return scene!=null && scene.Entered && !scene.Completed && ReferenceEquals(scene.Map,map) && p.Entity.MapID==map.ID && DefenseTalismanScenes.Registered(scene,p) && S.Clock()<scene.Until;}
        private static bool PrivateKind(int kind){return kind==1 || kind==5 || kind>=6 && kind<=10;}
        private static int Kind(GameState p){var scene=DefenseTalismanScenes.Find(p);if(scene!=null && HouseWorld(p,p.Map)){
            var run=S.Load(p);if(run.Stage==8)return scene.Map.BaseID==1787?S.FengKind(p):S.GeneralKind(p);if(S.HouseKills(p,scene.Visit)<5){if(scene.Map.BaseID==1782 && run.Stage==1)return 1;if(scene.Map.BaseID==1783 && run.Stage==7)return 5;}
        }return S.EncounterKind(p);}
        internal static void Cancel(GameState p){Fight f;lock(gate){if(!owners.TryGetValue(p.Entity.UID,out f) || !ReferenceEquals(f.Owner,p))return;}Remove(f);}
        private static Entity Encounter(Entity a,Entity b){return IsEncounter(a)?a:IsEncounter(b)?b:null;}
        private static bool Fighter(Fight f,Entity b){Entity pet;return b!=null && !b.Dead && b.MapID==f.Map.ID && (ReferenceEquals(b,f.Owner.Entity)
            || b.Companion && ReferenceEquals(b.Owner,f.Owner) && ReferenceEquals(f.Owner.Companion,b) && f.Map.Companions.TryGetValue(b.UID,out pet) && ReferenceEquals(pet,b));}
        internal static bool BlockAttack(Entity a,Entity b){var body=Encounter(a,b);Fight f;return body!=null && (!bodies.TryGetValue(body,out f) || f.Recorded || body.Dead || !Current(f) || !Fighter(f,ReferenceEquals(body,a)?b:a));}
        private sealed class Lease:IDisposable {private readonly object sync;internal Lease(object value){sync=value;}public void Dispose(){Monitor.Exit(sync);}}
        internal static IDisposable DamageLease(Entity a,Entity b){var body=Encounter(a,b);Fight f;return body!=null && bodies.TryGetValue(body,out f) && Monitor.TryEnter(f.Owner.ItemSyncRoot)?new Lease(f.Owner.ItemSyncRoot):null;}
        internal static bool BlockDamage(Entity a,Entity b){if(BlockAttack(a,b))return true;var body=Encounter(a,b);Fight f;return body!=null && (!bodies.TryGetValue(body,out f) || !Monitor.IsEntered(f.Owner.ItemSyncRoot));}
        internal static MonsterInformation Profile(int kind){
            if(kind<0 || kind>10)throw new ArgumentOutOfRangeException("kind");
            MonsterInformation source;if(!MonsterInformation.MonsterInformations.TryGetValue(3579,out source) || source.Name!="FishB" || source.Mesh!=311)throw new InvalidOperationException("Defense Talisman fish appearance changed.");
            MonsterInformation bandit;if(kind!=0 && (!MonsterInformation.MonsterInformations.TryGetValue(55,out bandit) || bandit.Name!="BanditL97" || bandit.Mesh!=201))throw new InvalidOperationException("Tower bandit appearance changed.");
            MonsterInformation bird;if(kind==3 && (!MonsterInformation.MonsterInformations.TryGetValue(18,out bird) || bird.Name!="Birdman" || bird.Mesh!=205))throw new InvalidOperationException("Tower condor appearance changed.");
            MonsterInformation rat;if(kind==5 && (!MonsterInformation.MonsterInformations.TryGetValue(8,out rat) || rat.Name!="Ratling" || rat.Mesh!=202))throw new InvalidOperationException("Tower rat appearance changed.");
            MonsterInformation skeleton;if(kind>=6 && (!MonsterInformation.MonsterInformations.TryGetValue(100,out skeleton) || skeleton.Name!="Skeleton" || skeleton.Mesh!=163))throw new InvalidOperationException("Tower general appearance changed.");
            // Installed appearances; explicit Revora encounter tuning.
            return new MonsterInformation{ID=FamilyStart+(uint)kind,Name=new[]{"TroutElf","TowerBandit","BloodyShawn","DesertCondor","CapriceLeader","ViciousRat","MausoleumGeneral2","MausoleumGeneral3","MausoleumGeneral1","MausoleumGeneral4","YangFeng"}[kind],Mesh=(ushort)(kind>=6?163:kind==0?311:kind==3?205:kind==5?202:201),
                Level=(byte)(kind>=6?110:kind>=2?105:100),Hitpoints=kind==10?100000u:kind>=6?60000u:kind==5?25000u:kind==4?60000u:kind==3?35000u:kind==2?50000u:18000u,MinAttack=kind>=6?1000u:kind==4?1200u:kind>=2?800u:600u,MaxAttack=kind>=6?1500u:kind==4?1600u:kind>=2?1200u:900u,Defence=(ushort)(kind>=6?500:kind==4?500:kind>=2?350:250),
                AttackType=1,AttackRange=2,ViewRange=18,AttackSpeed=1500,MoveSpeed=800,RunSpeed=500,RespawnTime=1000000000};
        }
        internal static bool HasOwner(uint uid){lock(gate)return owners.ContainsKey(uid);}
        internal static Entity Start(GameState p){
            if(!S.Eligible(p) || (!World(p.Map) && !HouseWorld(p,p.Map)) || HasOwner(p.Entity.UID) || !SessionInstall.TryEnter(p.Entity.UID))return null;
            try{lock(p.ItemSyncRoot){int kind=Kind(p);if(kind<0 || !PrivateKind(kind) && (!World(p.Map) || p.Map.ID!=(kind>=3?1000:1015)))return null;
                if(kind==2){var now=S.Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Tower schedule requires UTC.");if(now.Minute%10!=0)return null;}
                if(!PrivateKind(kind)){using(var sha=System.Security.Cryptography.SHA256.Create())using(var file=System.IO.File.OpenRead(System.IO.Path.Combine(Constants.DMapsPath,"maps",p.Map.ID+".map")))
                    if(System.BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant()!=(kind>=3?"c874aa5659420dfcf03545f1bfad67bea0197ae55fbc8140fcd7e85ffbae33f1":"e47df70501200965040755a424873395172c03e7dad920d483414f85c0359ba7"))throw new InvalidOperationException("Defense Talisman terrain changed.");}
                ushort x=(ushort)(kind==0?310:PrivateKind(kind)?40:kind==2?400:166),y=(ushort)(kind==0?380:PrivateKind(kind)?55:kind==2?650:226);
                if(kind==10){x=56;y=64;}else if(kind>=6){x=S.GeneralPositions[kind-6,0];y=S.GeneralPositions[kind-6,1];}
                if(Kernel.GetDistance(p.Entity.X,p.Entity.Y,x,y)>8)return null;
                lock(gate){if(owners.Count>=32 || owners.ContainsKey(p.Entity.UID))return null;
                    var map=p.Map;ushort sx=0;
                    for(int i=0;i<3;i++){ushort cx=(ushort)(x+(kind>=6?0:2)+i*2);
                        if(map.Floor[cx,y,MapObjectType.Monster,null] && !map.Entities.Values.Any(b=>b.X==cx && b.Y==y)
                            && !map.Npcs.Values.Any(n=>n.X==cx && n.Y==y) && !map.Companions.Values.Any(b=>b.X==cx && b.Y==y)
                            && !Kernel.GamePool.Values.Any(o=>o.Entity!=null && o.Entity.MapID==map.ID && o.Entity.X==cx && o.Entity.Y==y)){sx=cx;break;}}
                    if(sx==0)return null;
                    var info=Profile(kind);info.BoundX=sx;info.BoundY=y;info.BoundCX=info.BoundCY=1;
                    var body=new Entity(EntityFlag.Monster,false){UID=map.EntityUIDCounter.Next,MapID=map.ID,X=sx,Y=y,MonsterInfo=info,MapObjType=MapObjectType.Monster,
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
            try{if(f.Recorded || !Current(f) || !Fighter(f,killer) || Kernel.GetDistance(f.Owner.Entity.X,f.Owner.Entity.Y,body.X,body.Y)>Constants.pScreenDistance)return true;
                if(!body.Dead)body.DieCore(f.Owner.Entity);
                if(f.Kind==0)S.FishKill(f.Owner,body,f.Attempt,()=>Current(f));else if(f.Kind==1 || f.Kind==5)S.HouseKill(f.Owner,body,f.Attempt,()=>Current(f));else if(f.Kind==2)S.ShawnKill(f.Owner,body,f.Attempt,()=>Current(f));else if(f.Kind==10)S.FengKill(f.Owner,body,f.Attempt,()=>Current(f));else if(f.Kind>=6)S.GeneralKill(f.Owner,body,f.Attempt,f.Kind,()=>Current(f));else S.DesertKill(f.Owner,body,f.Attempt,f.Kind,()=>Current(f));f.Recorded=true;DefenseTalismanQuest.Refresh(f.Owner);
            }catch(Exception ex){f.Recorded=true;Q.Fail(f.Owner,"tower-fishing-death",ex);}finally{Monitor.Exit(f.Owner.ItemSyncRoot);}return true;
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
                try{if(f.Recorded || f.Body.Dead || !Current(f) || Kind(f.Owner)!=f.Kind)Remove(f);}catch(Exception ex){Q.Fail(f.Owner,"tower-fishing-cleanup",ex);}finally{Monitor.Exit(f.Owner.ItemSyncRoot);}}
        }
    }
}
