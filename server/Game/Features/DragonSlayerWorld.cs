using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Revora.Client;
using Revora.Database;
using Revora.Interfaces;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class DragonSlayerWorld
    {
        internal const ushort MapId=2056;
        internal const uint StatueFirst=900000120,FamilyFirst=9916000;
        internal const int Capacity=32;
        internal static readonly object Gate=new object();
        internal sealed class Branch {
            internal int Index;internal string Name;internal ushort X,Y,StatueType;
            internal uint Grass {get{return (uint)(711189+Index);}}
            internal uint Pellet {get{return (uint)(720591+Index);}}
            internal uint Pill {get{return (uint)(711181+Index);}}
            internal uint Statue {get{return StatueFirst+(uint)Index;}}
        }
        internal static readonly Branch[] Branches={
            new Branch{Index=0,Name="Ice",X=511,Y=570,StatueType=825},new Branch{Index=1,Name="Stone",X=599,Y=439,StatueType=826},
            new Branch{Index=2,Name="Point",X=515,Y=329,StatueType=827},new Branch{Index=3,Name="Ray",X=441,Y=317,StatueType=834},
            new Branch{Index=4,Name="Grey",X=183,Y=375,StatueType=837},new Branch{Index=5,Name="Blood",X=109,Y=335,StatueType=838},
            new Branch{Index=6,Name="Bloom",X=125,Y=279,StatueType=839}};
        internal sealed class Hunt {
            internal Guid Run;internal GameState Player;internal Map Map;internal Branch Branch;internal bool Ghoul,Released;internal DateTime Until;
            internal readonly List<Actor> Monsters=new List<Actor>();internal Point[] Cells;
        }
        internal sealed class Actor {internal Hunt Hunt;internal Entity Body;internal int Slot;}
        internal static readonly ConcurrentDictionary<uint,Hunt> Hunts=new ConcurrentDictionary<uint,Hunt>();
        internal static readonly ConcurrentDictionary<uint,Actor> Actors=new ConcurrentDictionary<uint,Actor>();
        internal static void Initialize() {
            using(var sha=SHA256.Create())using(var file=File.OpenRead(Path.Combine(Constants.DMapsPath,"maps","2056.map")))
                if(System.BitConverter.ToString(sha.ComputeHash(file)).Replace("-","")!="60E67D7058814B81E2FB602A9480F919FD247F177922EB308BD121E30CD73766")
                    throw new InvalidOperationException("Dragon Slayer terrain differs from the reviewed floor.");
            foreach(uint id in new uint[]{2419,4151})if(!MonsterInformation.MonsterInformations.ContainsKey(id))throw new InvalidOperationException("Dragon Slayer presentation template is missing.");
            if(MonsterInformation.MonsterInformations[2419].Name!="FrostBladeDevil" || MonsterInformation.MonsterInformations[2419].Mesh!=154
                || MonsterInformation.MonsterInformations[4151].Name!="MagmaDemon" || MonsterInformation.MonsterInformations[4151].Mesh!=163)
                throw new InvalidOperationException("Dragon Slayer presentation templates changed.");
            Map map;if(!Kernel.Maps.TryGetValue(MapId,out map) || map.ID!=MapId || map.BaseID!=MapId || map.Floor==null)throw new InvalidOperationException("Dragon Slayer world is missing.");
            foreach(var b in Branches) {
                INpc old;if(map.Npcs.TryGetValue(b.Statue,out old)) {
                    if(old.X!=b.X || old.Y!=b.Y || old.Mesh!=b.StatueType*10)throw new InvalidOperationException("Dragon Slayer statue identity collision.");
                    continue;
                }
                if(!map.Floor[b.X,b.Y,MapObjectType.Player])throw new InvalidOperationException("Dragon Slayer statue site is blocked.");
            }
            foreach(var b in Branches)if(!map.Npcs.ContainsKey(b.Statue))
                map.AddNpc(new NpcSpawn{UID=b.Statue,MapID=MapId,X=b.X,Y=b.Y,Mesh=(ushort)(b.StatueType*10),Name=b.Name+"GhoulStatue",Type=Enums.NpcType.Talker});
        }
        internal static bool IsEncounter(Entity entity) {return entity!=null && entity.MonsterInfo!=null && entity.MonsterInfo.ID>=FamilyFirst && entity.MonsterInfo.ID<FamilyFirst+14;}
        internal static bool OwnFighter(GameState p,Entity entity) {
            if(!Q.Player(p) || entity==null || entity.Dead || entity.MapID!=MapId || p.Entity.MapID!=MapId)return false;
            if(ReferenceEquals(p.Entity,entity))return true;
            Entity registered;return entity.Companion && ReferenceEquals(entity.Owner,p) && ReferenceEquals(p.Companion,entity)
                && p.Map.Companions.TryGetValue(entity.UID,out registered) && ReferenceEquals(registered,entity);
        }
        internal static bool BlockAttack(Entity attacker,Entity target) {
            Entity mob=IsEncounter(attacker)?attacker:IsEncounter(target)?target:null;if(mob==null)return false;
            Actor actor;Entity player=ReferenceEquals(mob,attacker)?target:attacker;
            return !Actors.TryGetValue(mob.UID,out actor) || !ReferenceEquals(actor.Body,mob) || actor.Hunt.Released
                || !OwnFighter(actor.Hunt.Player,player);
        }
        internal static Hunt Prepare(GameState p,Guid run,int branch,int mask,bool ghoul) {
            // Caller holds the player inventory lock and Gate until payment and publication finish.
            if(!System.Threading.Monitor.IsEntered(Gate))throw new InvalidOperationException("Dragon Slayer allocation requires its gate.");
            Hunt current;if(Hunts.TryGetValue(p.Entity.UID,out current)) {
                if(current.Run==run && current.Ghoul==ghoul && current.Player==p && !current.Released && DragonSlayerHunt.Clock()<current.Until)return current;
                Release(current);
            }
            if(Hunts.Count>=Capacity)return null;
            var map=p.Map;if(!Q.Player(p) || map.ID!=MapId || map.BaseID!=MapId)throw new InvalidOperationException("Dragon Slayer summoner left the world.");
            var b=Branches[branch];int needed=ghoul?1:9-Count(mask);var cells=new List<Point>();
            int x=ghoul?b.X:p.Entity.X,y=ghoul?b.Y:p.Entity.Y;
            for(int radius=2;radius<=12 && cells.Count<needed;radius++)for(int dx=-radius;dx<=radius && cells.Count<needed;dx++)for(int dy=-radius;dy<=radius && cells.Count<needed;dy++) {
                if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=radius)continue;var cell=new Point(x+dx,y+dy);
                if(cell.X<0 || cell.Y<0 || cell.X>ushort.MaxValue || cell.Y>ushort.MaxValue || !map.Floor[cell.X,cell.Y,MapObjectType.Monster]
                    || Kernel.GetDistance(p.Entity.X,p.Entity.Y,(ushort)cell.X,(ushort)cell.Y)<2
                    || map.Npcs.Values.Any(n=>Kernel.GetDistance(n.X,n.Y,(ushort)cell.X,(ushort)cell.Y)<2))continue;
                cells.Add(cell);
            }
            if(cells.Count!=needed)return null;
            var hunt=new Hunt{Run=run,Player=p,Map=map,Branch=b,Ghoul=ghoul,Until=DragonSlayerHunt.Clock().AddMinutes(15),Cells=cells.ToArray()};
            if(!Hunts.TryAdd(p.Entity.UID,hunt))throw new InvalidOperationException("Dragon Slayer ownership collision.");return hunt;
        }
        internal static int Count(int mask) {int n=0;for(int bit=0;bit<9;bit++)if((mask&(1<<bit))!=0)n++;return n;}
        internal static void Publish(Hunt hunt,int mask) {
            if(hunt.Monsters.Count!=0)return;int cell=0;
            for(int slot=0;slot<(hunt.Ghoul?1:9);slot++) {
                if(!hunt.Ghoul && (mask&(1<<slot))!=0)continue;
                var point=hunt.Cells[cell++];var info=MonsterInformation.MonsterInformations[hunt.Ghoul?4151u:2419u].Copy();
                info.ID=FamilyFirst+(uint)hunt.Branch.Index+(hunt.Ghoul?7u:0);info.Name=hunt.Branch.Name+(hunt.Ghoul?"Ghoul":"Minion");
                info.Guard=info.Reviver=info.Boss=false;info.Hitpoints=65535;info.Level=128;info.SpellID=0;info.AttackType=1;
                info.MinAttack=hunt.Ghoul?4000u:1600u;info.MaxAttack=hunt.Ghoul?6000u:2328u;info.Defence=hunt.Ghoul?(ushort)1200:(ushort)100;
                info.ViewRange=18;info.AttackRange=2;info.AttackSpeed=1500;info.MoveSpeed=500;info.RunSpeed=500;info.RespawnTime=int.MaxValue/2;
                info.BoundX=(ushort)point.X;info.BoundY=(ushort)point.Y;info.BoundCX=info.BoundCY=1;info.InSight=hunt.Player.Entity.UID;
                var body=new Entity(EntityFlag.Monster,false){UID=hunt.Map.EntityUIDCounter.Next,MapID=MapId,MonsterInfo=info,MapObjType=MapObjectType.Monster,
                    X=(ushort)point.X,Y=(ushort)point.Y,Name=info.Name,Body=info.Mesh,Level=info.Level,MinAttack=info.MinAttack,MaxAttack=info.MaxAttack,
                    MagicAttack=info.MaxAttack,Defence=info.Defence};
                while(hunt.Map.Entities.ContainsKey(body.UID) || hunt.Map.Npcs.ContainsKey(body.UID))body.UID=hunt.Map.EntityUIDCounter.Next;
                info.Owner=body;body.MaxHitpoints=body.Hitpoints=info.Hitpoints;body.SendUpdates=true;
                var actor=new Actor{Hunt=hunt,Body=body,Slot=hunt.Ghoul?9:slot};
                if(!Actors.TryAdd(body.UID,actor))throw new InvalidOperationException("Dragon Slayer actor collision.");
                hunt.Monsters.Add(actor);hunt.Map.AddEntity2(body);body.SendSpawn(hunt.Player,false);
            }
        }
        internal static void Remove(Actor actor) {
            actor.Body.MonsterInfo.SendScreen(new Data(true){UID=actor.Body.UID,ID=Data.RemoveEntity});actor.Hunt.Map.RemoveEntity(actor.Body);
            Actor ignored;Actors.TryRemove(actor.Body.UID,out ignored);actor.Hunt.Monsters.Remove(actor);
        }
        internal static void Release(Hunt hunt) {
            lock(Gate){if(hunt.Released)return;hunt.Released=true;foreach(var actor in hunt.Monsters.ToArray())Remove(actor);
                ((ICollection<KeyValuePair<uint,Hunt>>)Hunts).Remove(new KeyValuePair<uint,Hunt>(hunt.Player.Entity.UID,hunt));}
        }
        internal static void Release(GameState p) {Hunt hunt;if(Hunts.TryGetValue(p.Entity.UID,out hunt))Release(hunt);}
        internal static void Tick() {
            foreach(var hunt in Hunts.Values.ToArray()) {
                if(!System.Threading.Monitor.TryEnter(hunt.Player.ItemSyncRoot))continue;
                try{if(!Q.Player(hunt.Player) || hunt.Player.Entity.MapID!=MapId || DragonSlayerHunt.Clock()>=hunt.Until)Release(hunt);
                    else foreach(var actor in hunt.Monsters.ToArray())actor.Body.MonsterInfo.InSight=hunt.Player.Entity.UID;
                }finally{System.Threading.Monitor.Exit(hunt.Player.ItemSyncRoot);}
            }
        }
    }
}
