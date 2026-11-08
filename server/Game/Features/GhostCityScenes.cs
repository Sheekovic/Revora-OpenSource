using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Private terrain is owned by the durable quest coordinator and combat guards.
    internal static partial class GhostCityScenes
    {
        internal const int Capacity=8;
        internal const uint MoMo=4526,Pillar=9917911;
        internal const ushort EntryX=150,EntryY=150;
        internal static readonly ushort[,] NpcPoints={{145,134},{162,217}};
        internal static readonly ushort[,] DarkNpcPoints={{145,134},{162,217},{150,200},{160,200},{170,200},{150,210},{170,210}};
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        private const string TerrainHash="a8e54974e3127c3235a788269df3f9f662b3c40be93130638469022266fd532b";
        internal sealed class Scene
        {
            internal GameState Owner;
            internal Map Map;
            internal MapsTable.MapInformation Metadata;
            internal Revora.Interfaces.INpc[] Actors=new Revora.Interfaces.INpc[2];

            internal bool Released,Entered,Completed,Dark;
            internal Guid Visit=Guid.NewGuid();
            internal DateTime Until;
        }
        private static readonly object gate=new object();
        private static readonly Dictionary<ushort,Scene> scenes=new Dictionary<ushort,Scene>();
        internal const ushort BaseID=1780;
        internal static void ValidateTerrain()
        {
            ushort id=BaseID;
            using(var sha=SHA256.Create())using(var f=File.OpenRead(Path.Combine(Constants.DMapsPath,"maps",id+".map")))
                if(System.BitConverter.ToString(sha.ComputeHash(f)).Replace("-","").ToLowerInvariant()!=TerrainHash)
                    throw new InvalidOperationException("Ghost City terrain changed.");
        }
        internal static Scene TryCreate(GameState owner){
            lock(gate){if(scenes.Count>=Capacity || scenes.Values.Any(s=>s.Owner.Entity.UID==owner.Entity.UID))return null;return Create(owner);}
        }
        internal static Scene Create(GameState owner)
        {
            ushort baseId=BaseID;
            if(!GhostCityStore.Eligible(owner) || !Monitor.IsEntered(owner.ItemSyncRoot))
                throw new InvalidOperationException("Ghost City scene requires its locked eligible owner.");
            ValidateTerrain();
            lock(gate)
            {
                if(scenes.Count>=Capacity || scenes.Values.Any(s=>s.Owner.Entity.UID==owner.Entity.UID))
                    throw new InvalidOperationException("Ghost City capacity or owner already reserved.");
                var now=Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Ghost City requires UTC.");
                var scene=new Scene{Owner=owner,Until=now.AddMinutes(20),Dark=GhostCityStore.Load(owner).Stage>=7};
                var points=scene.Dark?DarkNpcPoints:NpcPoints;scene.Actors=new Revora.Interfaces.INpc[points.GetLength(0)];
                try
                {
                    scene.Map=Map.CreateDynamic(baseId,"maps\\"+baseId+".map",false);
                    if(scene.Map.Entities.Count!=0 || scene.Map.Npcs.Count!=0 || scene.Map.Portals.Count!=0)
                        throw new InvalidOperationException("Ghost City scene inherited catalog actors.");
                    if(!scene.Map.Floor[EntryX,EntryY,MapObjectType.Player])throw new InvalidOperationException("Ghost City entry blocked.");
                    for(int i=0;i<points.GetLength(0);i++)if(!scene.Map.Floor[points[i,0],points[i,1],MapObjectType.Player])
                        throw new InvalidOperationException("Ghost City NPC placement blocked.");
                    if(!Q.Player(owner))throw new InvalidOperationException("Ghost City owner changed during allocation.");
                    scene.Metadata=new MapsTable.MapInformation{ID=scene.Map.ID,BaseID=baseId,Owner=owner.Entity.UID,Status=2};
                    MapsTable.MapInformations.Add(scene.Map.ID,scene.Metadata);
                    for(int i=0;i<points.GetLength(0);i++){
                        uint uid=i==0?MoMo:i==1?Pillar:9917930u+(uint)(i-2);
                        string[] names={"MoMo","Pillar","SweetyPuddy","SweetyLily","SweetyMindy","SweetyMay","SweetyCuty"};
                        CityGuardSystem.SpawnTalker(uid,scene.Map.ID,points[i,0],points[i,1],(ushort)(i==0?5650:i==1?5770:(564+i)*10),names[i],0);
                        scene.Actors[i]=scene.Map.Npcs[uid];
                        if(!scene.Map.Floor[points[i,0]-2,points[i,1],MapObjectType.Player])throw new InvalidOperationException("Ghost City NPC approach blocked.");
                    }
                    if(!scene.Map.Floor[EntryX,EntryY,MapObjectType.Player])throw new InvalidOperationException("Ghost City entry blocked by an NPC.");
                    scenes.Add(scene.Map.ID,scene);return scene;
                }
                catch
                {
                    Map registered;
                    if(scene.Map!=null && Kernel.Maps.TryGetValue(scene.Map.ID,out registered) && ReferenceEquals(registered,scene.Map))
                    {MapsTable.MapInformation metadata;if(MapsTable.MapInformations.TryGetValue(scene.Map.ID,out metadata) && metadata.Equals(scene.Metadata))MapsTable.MapInformations.Remove(scene.Map.ID);scene.Map.Dispose();}
                    throw;
                }
            }
        }
        internal static bool Contains(ushort map){lock(gate)return scenes.ContainsKey(map);}
        internal static bool HasOwner(uint uid){lock(gate)return scenes.Values.Any(s=>s.Owner.Entity.UID==uid);}
        internal static bool Registered(Scene scene,GameState owner)
        {
            if(scene==null || scene.Released || !ReferenceEquals(scene.Owner,owner) || !Q.Player(owner,false))return false;
            lock(gate){Scene current;Map map;return scenes.TryGetValue(scene.Map.ID,out current) && ReferenceEquals(current,scene)
                && Kernel.Maps.TryGetValue(scene.Map.ID,out map) && ReferenceEquals(map,scene.Map);}
        }
        internal static void Release(Scene scene)
        {
            if(scene==null)return;
            lock(scene.Owner.ItemSyncRoot)lock(gate)
            {
                if(scene.Released)return;
                Scene current;Map map;
                if(!scenes.TryGetValue(scene.Map.ID,out current) || !ReferenceEquals(current,scene)
                    || !Kernel.Maps.TryGetValue(scene.Map.ID,out map) || !ReferenceEquals(map,scene.Map))
                    throw new InvalidOperationException("Ghost City scene ownership changed.");
                MapsTable.MapInformation metadata;
                if(!MapsTable.MapInformations.TryGetValue(scene.Map.ID,out metadata) || !metadata.Equals(scene.Metadata))
                    throw new InvalidOperationException("Ghost City metadata ownership changed.");
                if(Kernel.GamePool.Values.Any(p=>p!=null && p.Entity!=null && p.Entity.MapID==scene.Map.ID))
                    throw new InvalidOperationException("Ghost City scene must be evacuated first.");
                if(scene.Map.Entities.Count!=0 || scene.Map.Companions.Count!=0)
                    throw new InvalidOperationException("Ghost City actors must be removed before terrain release.");
                if(scene.Map.Npcs.Count!=scene.Actors.Length || scene.Map.Npcs.Values.Any(n=>n.MapID!=scene.Map.ID || !scene.Actors.Any(a=>ReferenceEquals(a,n))))
                    throw new InvalidOperationException("Ghost City NPC ownership changed.");
                foreach(var npc in scene.Map.Npcs.Values.ToArray())scene.Map.RemoveNpc(npc);
                MapsTable.MapInformations.Remove(scene.Map.ID);scene.Map.Dispose();scenes.Remove(scene.Map.ID);scene.Released=true;
            }
        }
    }
}
