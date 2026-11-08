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
    internal static partial class TempestWingsScenes
    {
        internal const int Capacity=8;
        internal const uint FamilyStart=9905500;
        internal const ushort EntryX=141,EntryY=111;
        internal static readonly ushort[,] SpawnPoints={{136,103},{145,111},{143,111}};
        internal static Func<DateTime> Clock=()=>TempestWingsStore.Clock();
        private const string TerrainHash="ae99b05f276ccacaff95c3772211a5c9b1f0cc5fe4ea88c2cd9da8c97e346bd6";
        internal sealed class Scene
        {
            internal GameState Owner;
            internal Map Map;
            internal MapsTable.MapInformation Metadata;
            internal ushort EntryX,EntryY;

            internal bool Released,Entered,Completed;
            internal Guid Visit=Guid.NewGuid();
            internal DateTime Until;
        }
        private static readonly object gate=new object();
        private static readonly Dictionary<ushort,Scene> scenes=new Dictionary<ushort,Scene>();
        internal const ushort BaseID=1100;
        internal const uint ExitNpc=9910500;
        internal static void ValidateProfiles(){
            using(var sha=SHA256.Create())using(var f=File.OpenRead(Path.Combine(Constants.DMapsPath,"maps","tempest-1100.map")))
                if(System.BitConverter.ToString(sha.ComputeHash(f)).Replace("-","").ToLowerInvariant()!=TerrainHash)throw new InvalidOperationException("Tempest terrain changed.");
        }
        internal static Scene Create(GameState owner){return CreateScene(owner,BaseID);}
        private static Scene CreateScene(GameState owner,ushort baseId)
        {
            if(!TempestWingsStore.Eligible(owner) || !Monitor.IsEntered(owner.ItemSyncRoot))
                throw new InvalidOperationException("Tempest Wings scene requires its locked eligible owner.");
            if(TempestWingsStore.Load(owner).Stage!=2)throw new InvalidOperationException("Tempest permit required.");
            ValidateProfiles();
            lock(gate)
            {
                if(scenes.Count>=Capacity || scenes.Values.Any(s=>s.Owner.Entity.UID==owner.Entity.UID))
                    throw new InvalidOperationException("Tempest Wings capacity or owner already reserved.");
                var now=Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Tempest Wings requires UTC.");
                var scene=new Scene{Owner=owner,Until=now.AddMinutes(15),EntryX=EntryX,EntryY=EntryY};
                try
                {
                    scene.Map=Map.CreateDynamicCompact(baseId,"maps\\tempest-1100.map");
                    if(scene.Map.Entities.Count!=0 || scene.Map.Npcs.Count!=0 || scene.Map.Portals.Count!=0)
                        throw new InvalidOperationException("Tempest Wings scene inherited catalog actors.");
                    if(!scene.Map.Floor[scene.EntryX,scene.EntryY,MapObjectType.Player])throw new InvalidOperationException("Tempest entry blocked.");
                    ushort[,] points=SpawnPoints;
                    for(int i=0;i<points.GetLength(0);i++)if(!scene.Map.Floor[points[i,0],points[i,1],MapObjectType.Player])
                        throw new InvalidOperationException("Tempest spawn point blocked.");
                    if(!Q.Player(owner))throw new InvalidOperationException("Tempest Wings owner changed during allocation.");
                    scene.Metadata=new MapsTable.MapInformation{ID=scene.Map.ID,BaseID=baseId,Owner=owner.Entity.UID,Status=2};
                    MapsTable.MapInformations.Add(scene.Map.ID,scene.Metadata);
                    CityGuardSystem.SpawnTalker(ExitNpc,scene.Map.ID,145,111,3580,"SkyKeeper",0);
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
                    throw new InvalidOperationException("Tempest Wings scene ownership changed.");
                MapsTable.MapInformation metadata;
                if(!MapsTable.MapInformations.TryGetValue(scene.Map.ID,out metadata) || !metadata.Equals(scene.Metadata))
                    throw new InvalidOperationException("Tempest Wings metadata ownership changed.");
                if(Kernel.GamePool.Values.Any(p=>p!=null && p.Entity!=null && p.Entity.MapID==scene.Map.ID))
                    throw new InvalidOperationException("Tempest Wings scene must be evacuated first.");
                if(scene.Map.Entities.Count!=0 || scene.Map.Companions.Count!=0)
                    throw new InvalidOperationException("Tempest Wings actors must be removed before terrain release.");
                MapsTable.MapInformations.Remove(scene.Map.ID);scene.Map.Dispose();scenes.Remove(scene.Map.ID);scene.Released=true;
            }
        }
    }
}
