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
    internal static partial class MadManScenes
    {
        internal const int Capacity=8;
        internal const uint StoneStart=9917810;
        internal const ushort EntryX=553,EntryY=584;
        internal static readonly ushort[,] StonePoints={{555,561},{578,560},{600,602},{570,630},{549,624},{577,600},{600,575}};
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        private const string TerrainHash="bdb39e79fd6a7701da0ea10a12c0753e8cd6f4187ff8719b05608699bc903b95";
        internal sealed class Scene
        {
            internal GameState Owner;
            internal Map Map;
            internal MapsTable.MapInformation Metadata;
            internal Revora.Interfaces.INpc[] Stones=new Revora.Interfaces.INpc[7];

            internal bool Released,Entered,Completed;
            internal Guid Visit=Guid.NewGuid();
            internal DateTime Until;
        }
        private static readonly object gate=new object();
        private static readonly Dictionary<ushort,Scene> scenes=new Dictionary<ushort,Scene>();
        internal const ushort BaseID=1020;
        internal static void ValidateTerrain()
        {
            ushort id=BaseID;
            using(var sha=SHA256.Create())using(var f=File.OpenRead(Path.Combine(Constants.DMapsPath,"maps",id+".map")))
                if(System.BitConverter.ToString(sha.ComputeHash(f)).Replace("-","").ToLowerInvariant()!=TerrainHash)
                    throw new InvalidOperationException("Mad Man terrain changed.");
        }
        internal static Scene TryCreate(GameState owner){
            lock(gate){if(scenes.Count>=Capacity || scenes.Values.Any(s=>s.Owner.Entity.UID==owner.Entity.UID))return null;return Create(owner);}
        }
        internal static Scene Create(GameState owner)
        {
            ushort baseId=BaseID;
            if(!MadManStore.Eligible(owner) || !Monitor.IsEntered(owner.ItemSyncRoot))
                throw new InvalidOperationException("Mad Man scene requires its locked eligible owner.");
            ValidateTerrain();
            lock(gate)
            {
                if(scenes.Count>=Capacity || scenes.Values.Any(s=>s.Owner.Entity.UID==owner.Entity.UID))
                    throw new InvalidOperationException("Mad Man capacity or owner already reserved.");
                var now=Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Mad Man requires UTC.");
                var scene=new Scene{Owner=owner,Until=now.AddMinutes(5)};
                try
                {
                    scene.Map=Map.CreateDynamic(baseId,"maps\\"+baseId+".map",false);
                    if(scene.Map.Entities.Count!=0 || scene.Map.Npcs.Count!=0 || scene.Map.Portals.Count!=0)
                        throw new InvalidOperationException("Mad Man scene inherited catalog actors.");
                    if(!scene.Map.Floor[EntryX,EntryY,MapObjectType.Player])throw new InvalidOperationException("Mad Man entry blocked.");
                    for(int i=0;i<StonePoints.GetLength(0);i++)if(!scene.Map.Floor[StonePoints[i,0],StonePoints[i,1],MapObjectType.Player])
                        throw new InvalidOperationException("Mad Man stone placement blocked.");
                    if(!Q.Player(owner))throw new InvalidOperationException("Mad Man owner changed during allocation.");
                    scene.Metadata=new MapsTable.MapInformation{ID=scene.Map.ID,BaseID=baseId,Owner=owner.Entity.UID,Status=2};
                    MapsTable.MapInformations.Add(scene.Map.ID,scene.Metadata);
                    for(int i=0;i<StonePoints.GetLength(0);i++){
                        CityGuardSystem.SpawnTalker(StoneStart+(uint)i,scene.Map.ID,StonePoints[i,0],StonePoints[i,1],(ushort)((555+i)*10),new[]{"RedStone","OrangeRock","YellowRock","GreenRock","BlueRock","IndigoRock","PurpleRock"}[i],0);
                        scene.Stones[i]=scene.Map.Npcs[StoneStart+(uint)i];
                        if(!scene.Map.Floor[StonePoints[i,0]-2,StonePoints[i,1],MapObjectType.Player])throw new InvalidOperationException("Mad Man stone approach blocked.");
                    }
                    if(!scene.Map.Floor[EntryX,EntryY,MapObjectType.Player])throw new InvalidOperationException("Mad Man entry blocked by a stone.");
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
                    throw new InvalidOperationException("Mad Man scene ownership changed.");
                MapsTable.MapInformation metadata;
                if(!MapsTable.MapInformations.TryGetValue(scene.Map.ID,out metadata) || !metadata.Equals(scene.Metadata))
                    throw new InvalidOperationException("Mad Man metadata ownership changed.");
                if(Kernel.GamePool.Values.Any(p=>p!=null && p.Entity!=null && p.Entity.MapID==scene.Map.ID))
                    throw new InvalidOperationException("Mad Man scene must be evacuated first.");
                if(scene.Map.Entities.Count!=0 || scene.Map.Companions.Count!=0)
                    throw new InvalidOperationException("Mad Man actors must be removed before terrain release.");
                if(scene.Map.Npcs.Count!=7 || scene.Map.Npcs.Values.Any(n=>n.UID<StoneStart || n.UID>=StoneStart+7 || n.MapID!=scene.Map.ID || !ReferenceEquals(n,scene.Stones[n.UID-StoneStart])))
                    throw new InvalidOperationException("Mad Man stone ownership changed.");
                foreach(var npc in scene.Map.Npcs.Values.ToArray())scene.Map.RemoveNpc(npc);
                MapsTable.MapInformations.Remove(scene.Map.ID);scene.Map.Dispose();scenes.Remove(scene.Map.ID);scene.Released=true;
            }
        }
    }
}
