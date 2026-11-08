using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using S=Revora.Game.Features.AttackTalismanStore;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static class AttackTalismanEncounters
    {
        internal const uint FamilyStart=9905400;
        private sealed class Fight {internal GameState Owner;internal Map Map;internal Entity Body;internal Guid Attempt;internal int Kind;internal byte Life;internal DateTime Until;internal bool Recorded;}
        private static readonly object gate=new object();
        private static readonly Dictionary<uint,Fight> owners=new Dictionary<uint,Fight>();
        private static readonly ConditionalWeakTable<Entity,Fight> bodies=new ConditionalWeakTable<Entity,Fight>();
        internal static bool IsEncounter(Entity body){return body!=null && body.MonsterInfo!=null && body.MonsterInfo.ID>=FamilyStart && body.MonsterInfo.ID<FamilyStart+7;}
        private static bool Current(Fight f){Entity body;Fight current;lock(gate){if(!owners.TryGetValue(f.Owner.Entity.UID,out current) || !ReferenceEquals(current,f))return false;}
            return ((f.Kind==2 || f.Kind==4)?PublicWorld(f.Map):HouseWorld(f.Owner,f.Map)) && S.Eligible(f.Owner) && f.Owner.Entity.Reborn==f.Life && f.Owner.Entity.MapID==f.Map.ID && ReferenceEquals(f.Owner.Map,f.Map)
                && S.Clock()<f.Until && f.Map.Entities.TryGetValue(f.Body.UID,out body) && ReferenceEquals(body,f.Body);
        }
        internal static bool Owns(GameState p,Entity body,Guid attempt,int kind){Fight f;return body!=null && bodies.TryGetValue(body,out f)
            && ReferenceEquals(f.Owner,p) && f.Attempt==attempt && f.Kind==kind && !f.Recorded && Current(f);}
        private static bool HouseWorld(GameState p,Map map){var scene=AttackTalismanScenes.Find(p);return scene!=null && scene.Entered && !scene.Completed && ReferenceEquals(scene.Map,map) && p.Entity.MapID==map.ID && AttackTalismanScenes.Registered(scene,p) && S.Clock()<scene.Until;}
        private static bool PublicWorld(Map map){Map current;MapsTable.MapInformation info;return map!=null && (map.ID==1011 || map.ID==1015) && map.BaseID==map.ID && Kernel.Maps.TryGetValue(map.ID,out current) && ReferenceEquals(current,map) && MapsTable.MapInformations.TryGetValue(map.ID,out info) && info.Owner==0 && info.BaseID==map.ID;}
        private static int Kind(GameState p){return PublicWorld(p.Map)?(p.Map.ID==1015?S.CourtesanKind(p):S.AmbushKind(p)):p.Map!=null && p.Map.BaseID==1011?S.LakeKind(p):p.Map!=null && p.Map.BaseID==3937?S.GardenKind(p):S.CaveKind(p);}
        internal static void Cancel(GameState p){Fight f;lock(gate){if(!owners.TryGetValue(p.Entity.UID,out f) || !ReferenceEquals(f.Owner,p))return;}Remove(f);}
        private static Entity Encounter(Entity a,Entity b){return IsEncounter(a)?a:IsEncounter(b)?b:null;}
        private static bool Fighter(Fight f,Entity b){Entity pet;return b!=null && !b.Dead && b.MapID==f.Map.ID && (ReferenceEquals(b,f.Owner.Entity)
            || b.Companion && ReferenceEquals(b.Owner,f.Owner) && ReferenceEquals(f.Owner.Companion,b) && f.Map.Companions.TryGetValue(b.UID,out pet) && ReferenceEquals(pet,b));}
        internal static bool BlockAttack(Entity a,Entity b){var body=Encounter(a,b);Fight f;return body!=null && (!bodies.TryGetValue(body,out f) || f.Recorded || body.Dead || !Current(f) || !Fighter(f,ReferenceEquals(body,a)?b:a));}
        private sealed class Lease:IDisposable {private readonly object sync;internal Lease(object value){sync=value;}public void Dispose(){Monitor.Exit(sync);}}
        internal static IDisposable DamageLease(Entity a,Entity b){var body=Encounter(a,b);Fight f;return body!=null && bodies.TryGetValue(body,out f) && Monitor.TryEnter(f.Owner.ItemSyncRoot)?new Lease(f.Owner.ItemSyncRoot):null;}
        internal static bool BlockDamage(Entity a,Entity b){if(BlockAttack(a,b))return true;var body=Encounter(a,b);Fight f;return body!=null && (!bodies.TryGetValue(body,out f) || !Monitor.IsEntered(f.Owner.ItemSyncRoot));}
        internal static MonsterInformation Profile(int kind){
            if(kind<0 || kind>6)throw new ArgumentOutOfRangeException("kind");MonsterInformation source;
            bool snake=kind==0 || kind==3;uint sourceId=kind>=5?100u:kind==2?18u:snake?6u:55u;string sourceName=kind>=5?"Skeleton":kind==2?"Birdman":snake?"WingedSnake":"BanditL97";ushort mesh=(ushort)(kind>=5?163:kind==2?205:snake?203:201);
            if(!MonsterInformation.MonsterInformations.TryGetValue(sourceId,out source) || source.Name!=sourceName || source.Mesh!=mesh || source.Guard || source.Reviver || source.Boss)throw new InvalidOperationException("Fan encounter appearance changed.");
            return new MonsterInformation{ID=FamilyStart+(uint)kind,Name=kind==5?"PoemGhost":kind==6?"LyraGhost":kind==4?"JadeCourtesan":kind==3?"CrimsonViper":kind==2?"CrimsonClaw":kind==0?"CaveViper":"CaveBrigand",Mesh=mesh,Level=(byte)(kind>=3?110:105),Hitpoints=kind>=5?55000u:kind==4?60000u:kind==3?50000u:kind==2?25000u:kind==0?30000u:40000u,
                MinAttack=(uint)(kind>=3?900:800),MaxAttack=(uint)(kind>=3?1400:1200),Defence=(ushort)(kind>=3?400:350),AttackType=1,AttackRange=2,ViewRange=18,AttackSpeed=1500,MoveSpeed=800,RunSpeed=500,RespawnTime=1000000000};
        }
        internal static bool HasOwner(uint uid){lock(gate)return owners.ContainsKey(uid);}
        internal static Entity Start(GameState p){
            if(!S.Eligible(p) || (!HouseWorld(p,p.Map) && !PublicWorld(p.Map)) || HasOwner(p.Entity.UID) || !SessionInstall.TryEnter(p.Entity.UID))return null;
            try{lock(p.ItemSyncRoot){int kind=Kind(p);if(kind<0 || ((kind==2 || kind==4)?!PublicWorld(p.Map):!HouseWorld(p,p.Map)))return null;
                if(kind==2 || kind==4)using(var sha=System.Security.Cryptography.SHA256.Create())using(var file=System.IO.File.OpenRead(System.IO.Path.Combine(Constants.DMapsPath,"maps",p.Map.ID+".map")))
                    if(System.BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant()!=(kind==4?"e47df70501200965040755a424873395172c03e7dad920d483414f85c0359ba7":"7e8f7a8262f8ae93a314699af6d276f0324fff6c32bcc6061bffc2f336f1e817"))throw new InvalidOperationException("Fan ambush terrain changed.");
                ushort x=(ushort)(kind>=5?54:kind==3?479:kind==2?788:kind==0?56:60),y=(ushort)(kind>=5?58:kind==3?844:kind==2?460:50);
                if(kind==4){int site=S.Load(p).CourtesanSite;if(site<0 || site>3)return null;x=S.CourtesanSites[site,0];y=S.CourtesanSites[site,1];}
                if(Kernel.GetDistance(p.Entity.X,p.Entity.Y,x,y)>(kind>=5?18:8))return null;
                lock(gate){if(owners.Count>=32 || owners.ContainsKey(p.Entity.UID))return null;
                    var map=p.Map;ushort sx=0;
                    for(int i=0;i<3;i++){ushort cx=(ushort)(x+i*2);
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
                if(f.Kind>=5)S.GhostKill(f.Owner,body,f.Attempt,f.Kind,()=>Current(f));else if(f.Kind==4)S.CourtesanKill(f.Owner,body,f.Attempt,()=>Current(f));else if(f.Kind==3)S.LakeKill(f.Owner,body,f.Attempt,()=>Current(f));else if(f.Kind==2)S.AmbushKill(f.Owner,body,f.Attempt,()=>Current(f));else S.CaveKill(f.Owner,body,f.Attempt,f.Kind,()=>Current(f));f.Recorded=true;AttackTalismanQuest.Refresh(f.Owner);
            }catch(Exception ex){f.Recorded=true;Q.Fail(f.Owner,"fan-cave-death",ex);}finally{Monitor.Exit(f.Owner.ItemSyncRoot);}return true;
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
                try{if(f.Recorded || f.Body.Dead || !Current(f) || Kind(f.Owner)!=f.Kind)Remove(f);}catch(Exception ex){Q.Fail(f.Owner,"fan-cave-cleanup",ex);}finally{Monitor.Exit(f.Owner.ItemSyncRoot);}}
        }
    }
}
