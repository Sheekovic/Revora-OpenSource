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

namespace Revora.Game.Features
{
    internal static class DisCityWorld
    {
        internal const uint Saint=3215,CrystalFirst=900000080,Treasury=900000089;
        internal const int Capacity=16;
        internal sealed class Encounter {internal Entity Body;internal int Crystal=-1;internal bool Boss;}
        internal sealed class Instance {
            internal Guid Run,Epoch=Guid.NewGuid();internal uint Owner;internal int Stage;internal Map Map;internal DateTime Until;internal GameState Player;internal bool Released;
            internal Point Landing;internal Point[] Crystals;internal List<Point> Cells;
            internal DisCityRunRules.Profile Rules;
            internal readonly Dictionary<uint,Encounter> Monsters=new Dictionary<uint,Encounter>();
        }
        private static readonly object allocation=new object();
        internal static readonly ConcurrentDictionary<uint,Instance> Players=new ConcurrentDictionary<uint,Instance>();
        internal static readonly ConcurrentDictionary<ushort,Instance> Maps=new ConcurrentDictionary<ushort,Instance>();
        [ThreadStatic] private static HashSet<uint> transfers;
        private static readonly Point[] Starts={new Point(253,57),new Point(215,344),new Point(341,383),new Point(147,27),new Point(46,48)};
        private static readonly ushort[] Profiles={2021,2022,2023,2024,3954};
        private static readonly string[] Hashes={"e9bb085858ac547b70136f843f94449b97bbe9a547a4b9ada6e512df294e5baf","0e47937c0537fad3c67497d0a017af88ef145aa159e0702aa4cbfcd9e7c893b9","398115067c61112ae6ff11427a73a68e6c46f9490e6ede42d1311bb5dab34443","eee895cf2a93c8b1c3f12a04461556ba46b0d3bf9e7117b0dd8e77cf8cc0ce56"};
        internal static void Initialize() {
            for(int i=0;i<4;i++)using(var sha=SHA256.Create())using(var file=File.OpenRead(Path.Combine(Constants.DMapsPath,"maps",(2021+i)+".map")))
                if(System.BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant()!=Hashes[i])throw new InvalidOperationException("Dis City terrain differs from reviewed floor.");
            foreach(uint type in new uint[]{5050,5051,5053,5054,5055,5056,5057,5058,5059})
                if(!MonsterInformation.MonsterInformations.ContainsKey(type))throw new InvalidOperationException("Dis City monster catalog missing.");
            using(var sha=SHA256.Create())using(var file=File.OpenRead(Path.Combine(Constants.DMapsPath,"maps","3954.map")))
                if(System.BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant()!="efc6436eb9274f9fb6e0fd088817d5cd850c3773ca7c848b7db0056fc9b7a22e")throw new InvalidOperationException("Dis City treasury differs from reviewed floor.");
        }
        internal static void PlaceSaint() {
            var town=Kernel.Maps[1002];INpc old;if(town.Npcs.TryGetValue(Saint,out old))town.RemoveNpc(old);
            ushort x=327,y=250;if(!PotencyGuide.FindLanding(town,ref x,ref y))throw new InvalidOperationException("Solar Saint landing blocked.");
            town.AddNpc(new NpcSpawn{UID=Saint,MapID=1002,X=x,Y=y,Mesh=1310,Name="SolarSaint",Type=Enums.NpcType.Talker});
        }
        private static List<Point> Reachable(Map map,Point desired,out Point landing) {
            ushort x=(ushort)desired.X,y=(ushort)desired.Y;if(!PotencyGuide.FindLanding(map,ref x,ref y))throw new InvalidOperationException("Dis City entry blocked.");
            landing=new Point(x,y);var queue=new Queue<Point>();var visited=new HashSet<Point>();var result=new List<Point>();queue.Enqueue(landing);visited.Add(landing);
            while(queue.Count>0) {
                var p=queue.Dequeue();if(p.X%4==0 && p.Y%4==0)result.Add(p);
                foreach(var n in new[]{new Point(p.X-1,p.Y),new Point(p.X+1,p.Y),new Point(p.X,p.Y-1),new Point(p.X,p.Y+1)})
                    if(map.Floor[n.X,n.Y,MapObjectType.Player] && visited.Add(n))queue.Enqueue(n);
            }
            if(result.Count<100)throw new InvalidOperationException("Dis City connected field is too small.");return result;
        }
        private static int Distance(Point a,Point b){return Math.Max(Math.Abs(a.X-b.X),Math.Abs(a.Y-b.Y));}
        internal static Instance Enter(GameState p,Guid run,int stage,DateTime now,Action<Instance> remember) {
            if(stage<1 || stage>5)throw new ArgumentOutOfRangeException("stage");
            var rules=stage==5?null:DisCityRunRules.Load(p.Entity.UID,run); // Treasury has no combat edition.
            Instance old;Players.TryGetValue(p.Entity.UID,out old);
            if(old!=null && old.Player==p && old.Run==run && old.Stage==stage && now<old.Until){remember(old);Travel(p,old.Map,old.Landing);return old;}
            Instance created;
            lock(allocation) {
                if(old==null && Players.Count>=Capacity)throw new InvalidOperationException("Dis City instance capacity reached.");
                var map=Map.CreateDynamic(Profiles[stage-1],"maps\\"+Profiles[stage-1]+".map",true);
                ushort id=map.ID;
                try {
                    if(!ReferenceEquals(Kernel.Maps[id],map) || map.Entities.Count!=0 || map.Npcs.Count!=0)throw new InvalidOperationException("Dis City dynamic map ownership collision.");
                    map.Portals.Clear();MapsTable.MapInformations[id]=new MapsTable.MapInformation{ID=id,BaseID=Profiles[stage-1],Status=2};
                    created=new Instance{Run=run,Owner=p.Entity.UID,Player=p,Stage=stage,Map=map,Until=now.AddHours(2),Rules=rules};
                    Point landing;created.Cells=Reachable(map,Starts[stage-1],out landing);created.Landing=landing;
                    Point saint=created.Cells.Where(c=>Distance(c,landing)>=3 && Distance(c,landing)<=8).OrderBy(c=>Distance(c,landing)).First();
                    map.AddNpc(new NpcSpawn{UID=Saint,MapID=id,X=(ushort)saint.X,Y=(ushort)saint.Y,Mesh=1310,Name="SolarSaint",Type=Enums.NpcType.Talker});
                    if(stage==3) {
                        var spots=new List<Point>();for(int i=0;i<9;i++) {
                            var target=new Point(landing.X-80+(i%3)*80,landing.Y-80+(i/3)*80);
                            var cell=created.Cells.Where(c=>Distance(c,saint)>=12 && spots.All(s=>Distance(c,s)>=18)).OrderBy(c=>Distance(c,target)).First();spots.Add(cell);
                            map.AddNpc(new NpcSpawn{UID=CrystalFirst+(uint)i,MapID=id,X=(ushort)cell.X,Y=(ushort)cell.Y,Mesh=37480,Name="HellCrystal"+(i+1),Type=Enums.NpcType.Talker});
                        }created.Crystals=spots.ToArray();
                    }
                    if(stage==5) {
                        var chest=created.Cells.Where(c=>Distance(c,saint)>=5 && Distance(c,landing)>=4).OrderBy(c=>Distance(c,new Point(49,45))).First();
                        map.AddNpc(new NpcSpawn{UID=Treasury,MapID=id,X=(ushort)chest.X,Y=(ushort)chest.Y,Mesh=37490,Name="HellTreasury",Type=Enums.NpcType.Talker});
                    }
                    Maps[id]=created;Players[p.Entity.UID]=created;
                }catch {map.Dispose();MapsTable.MapInformations.Remove(id);throw;}
            }
            try{remember(created);Travel(p,created.Map,created.Landing);}catch {Release(created);if(old!=null)Players[p.Entity.UID]=old;throw;}
            if(old!=null)Release(old);return created;
        }
        internal static void Travel(GameState p,Map map,Point point,bool heal=false) {
            ushort x=(ushort)point.X,y=(ushort)point.Y;if(!PotencyGuide.FindLanding(map,ref x,ref y))throw new InvalidOperationException("Dis City return landing blocked.");
            if(transfers==null)transfers=new HashSet<uint>();transfers.Add(p.Entity.UID);
            try{p.Entity.Teleport(map.BaseID,map.ID,x,y);if(p.Entity.MapID!=map.ID)throw new InvalidOperationException("Dis City travel did not arrive.");
                if(heal){p.Entity.BringToLife();p.Entity.Mana=p.Entity.MaxMana;}}
            finally{transfers.Remove(p.Entity.UID);}
        }
        internal static void Home(GameState p,bool heal=false) {var npc=Kernel.Maps[1002].Npcs[Saint];Travel(p,Kernel.Maps[1002],new Point(npc.X+3,npc.Y+3),heal);}
        internal static bool AllowTeleport(GameState p,ushort destination,ushort x,ushort y) {
            Instance i;if(p==null || p.Entity==null || !Maps.TryGetValue(destination,out i))return true;
            if(transfers!=null && transfers.Contains(p.Entity.UID))return i.Owner==p.Entity.UID;
            if(i.Owner==p.Entity.UID && p.Entity.MapID==destination && p.Map.Floor[x,y,MapObjectType.Player]) {
                long dx=(long)x-p.Entity.X,dy=(long)y-p.Entity.Y;if(dx*dx+dy*dy<441)return true;
            }
            return false;
        }
        internal static bool IsEncounter(Entity mob) {
            return mob!=null && mob.EntityFlag==EntityFlag.Monster && mob.MonsterInfo!=null && mob.MonsterInfo.ID>=9914001 && mob.MonsterInfo.ID<=9914004;
        }
        internal static void Spawn(Instance i,int crystal=-1,bool boss=false) {
            if(i.Rules==null)throw new InvalidOperationException("Dis City combat rules are not available in this instance.");
            Point desired=crystal>=0?i.Crystals[crystal]:i.Cells[Kernel.Random.Next(i.Cells.Count)];
            var cell=i.Cells.Where(p=>i.Map.Floor[p.X,p.Y,MapObjectType.Monster] && i.Map.Npcs.Values.All(n=>Distance(p,new Point(n.X,n.Y))>=4)
                && Distance(p,i.Landing)>=8).OrderBy(p=>Distance(p,desired)).First();
            uint type=boss?5059u:crystal>=0?5055u:i.Stage==1?new uint[]{5050,5051,5053,5054}[Kernel.Random.Next(4)]:i.Stage==2?new uint[]{5055,5056,5057}[Kernel.Random.Next(3)]:5058u;
            var info=MonsterInformation.MonsterInformations[type].Copy();info.ID=(uint)(9914000+i.Stage);info.Name=boss?"UltimatePluto":info.Name;
            info.Level=boss?(byte)140:(byte)120;
            info.Guard=info.Reviver=false;info.Boss=boss;info.SpellID=0;info.RespawnTime=int.MaxValue/2;info.BoundX=(ushort)cell.X;info.BoundY=(ushort)cell.Y;info.BoundCX=info.BoundCY=1;
            info.ViewRange=18;info.AttackRange=3;info.AttackSpeed=1500;info.Hitpoints=boss?i.Rules.BossHp:crystal>=0?i.Rules.GuardianHp:i.Rules.OrdinaryHp;
            info.MinAttack=boss?i.Rules.BossMinimumAttack:i.Rules.MinimumAttack;info.MaxAttack=boss?i.Rules.BossMaximumAttack:i.Rules.MaximumAttack;
            var body=new Entity(EntityFlag.Monster,false){UID=i.Map.EntityUIDCounter.Next,MapID=i.Map.ID,MonsterInfo=info,MapObjType=MapObjectType.Monster,
                X=(ushort)cell.X,Y=(ushort)cell.Y,Name=info.Name,Body=info.Mesh,Level=info.Level,MinAttack=info.MinAttack,MaxAttack=info.MaxAttack,MagicAttack=info.MaxAttack,Defence=info.Defence};
            info.Owner=body;body.MaxHitpoints=body.Hitpoints=info.Hitpoints;body.ApplyMonsterBossHud();body.SendUpdates=true;i.Map.AddEntity2(body);
            i.Monsters.Add(body.UID,new Encounter{Body=body,Crystal=crystal,Boss=boss});
            GameState player;if(Kernel.GamePool.TryGetValue(i.Owner,out player) && player.Entity.MapID==i.Map.ID) {
                if(Kernel.GetDistance(player.Entity.X,player.Entity.Y,body.X,body.Y)<Constants.nScreenDistance)body.SendSpawn(player,false);
                if(boss)player.Send("Ultimate Pluto has emerged at ("+body.X+","+body.Y+").");
            }
        }
        internal static void Remove(Instance i,Entity body) {
            body.MonsterInfo.SendScreen(new Data(true){UID=body.UID,ID=Data.RemoveEntity});i.Map.RemoveEntity(body);i.Monsters.Remove(body.UID);
        }
        internal static void Release(Instance i) {
            lock(i) {
            if(i.Released)return;i.Released=true;
            Instance current;if(Players.TryGetValue(i.Owner,out current) && ReferenceEquals(current,i))Players.TryRemove(i.Owner,out current);
            Maps.TryRemove(i.Map.ID,out current);foreach(var mob in i.Monsters.Values.ToArray())Remove(i,mob.Body);
            i.Map.Npcs.Clear();MapsTable.MapInformations.Remove(i.Map.ID);i.Map.Dispose();
            }
        }
    }
}
