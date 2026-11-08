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
    internal static partial class AttackTalismanScenes
    {
        internal const int Capacity=8;
        internal const uint FamilyStart=9905400;
        internal const ushort EntryX=52,EntryY=50;
        internal static readonly ushort[,] SpawnPoints={{56,50},{58,50},{60,50},{62,50},{64,50}};
        internal static Func<DateTime> Clock=()=>AttackTalismanStore.Clock();
        private const string TerrainHash="64c54fbf5f6b9a091e1702728b7836afc3537ac32cdbfb4fb501819de1058c34";
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
        internal const ushort BaseID=1025,LakeBaseID=1011,GardenBaseID=3937;
        internal const uint Herbalist=9910401;
        private static void ValidateTerrain(ushort id){
            if(id!=BaseID && id!=LakeBaseID && id!=GardenBaseID)throw new InvalidOperationException("Unknown Fan scene.");
            using(var sha=SHA256.Create())using(var f=File.OpenRead(Path.Combine(Constants.DMapsPath,"maps",id+".map")))
                if(System.BitConverter.ToString(sha.ComputeHash(f)).Replace("-","").ToLowerInvariant()!=(id==GardenBaseID?"d76d15a0bf69c2e22e75b782138ae94673e68da916debb052106b184cce307e8":id==BaseID?TerrainHash:"7e8f7a8262f8ae93a314699af6d276f0324fff6c32bcc6061bffc2f336f1e817"))throw new InvalidOperationException("Fan cave terrain changed.");
        }
        internal static void ValidateProfiles(){ValidateTerrain(BaseID);ValidateTerrain(LakeBaseID);ValidateTerrain(GardenBaseID);}
        internal static Scene Create(GameState owner){return CreateScene(owner,BaseID);}
        internal static Scene CreateLake(GameState owner){return CreateScene(owner,LakeBaseID);}
        internal static Scene CreateGarden(GameState owner){return CreateScene(owner,GardenBaseID);}
        private static Scene CreateScene(GameState owner,ushort baseId)
        {
            if(!AttackTalismanStore.Eligible(owner) || !Monitor.IsEntered(owner.ItemSyncRoot))
                throw new InvalidOperationException("Attack Talisman scene requires its locked eligible owner.");
            var run=AttackTalismanStore.Load(owner);if(baseId==GardenBaseID?((run.Stage!=9 && run.Stage!=10) || run.Pillars!=15 || run.BookGiven):(run.Stage!=(baseId==BaseID?5:8) || baseId==LakeBaseID && run.LakeStep>=3))throw new InvalidOperationException("Fan scene is not unlocked.");
            ValidateTerrain(baseId);
            lock(gate)
            {
                if(scenes.Count>=Capacity || scenes.Values.Any(s=>s.Owner.Entity.UID==owner.Entity.UID))
                    throw new InvalidOperationException("Attack Talisman capacity or owner already reserved.");
                var now=Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Attack Talisman requires UTC.");
                var scene=new Scene{Owner=owner,Until=now.AddMinutes(10),EntryX=(ushort)(baseId==GardenBaseID?48:baseId==BaseID?EntryX:471),EntryY=(ushort)(baseId==GardenBaseID?60:baseId==BaseID?EntryY:844)};
                try
                {
                    scene.Map=Map.CreateDynamic(baseId,"maps\\"+baseId+".map",false);
                    if(scene.Map.Entities.Count!=0 || scene.Map.Npcs.Count!=0 || scene.Map.Portals.Count!=0)
                        throw new InvalidOperationException("Attack Talisman scene inherited catalog actors.");
                    if(!scene.Map.Floor[scene.EntryX,scene.EntryY,MapObjectType.Player])throw new InvalidOperationException("Fan entry blocked.");
                    ushort[,] points=baseId==GardenBaseID?new ushort[,]{{52,50},{58,50},{64,50},{60,64},{52,53},{58,53},{64,53},{60,61},{54,58},{56,58},{58,58}}:baseId==BaseID?SpawnPoints:new ushort[,]{{475,844},{475,840},{477,840},{479,844},{481,844},{483,844}};
                    for(int i=0;i<points.GetLength(0);i++)if(!scene.Map.Floor[points[i,0],points[i,1],MapObjectType.Player])
                        throw new InvalidOperationException("Fan spawn point blocked.");
                    if(!Q.Player(owner))throw new InvalidOperationException("Attack Talisman owner changed during allocation.");
                    scene.Metadata=new MapsTable.MapInformation{ID=scene.Map.ID,BaseID=baseId,Owner=owner.Entity.UID,Status=2};
                    MapsTable.MapInformations.Add(scene.Map.ID,scene.Metadata);
                    if(baseId==LakeBaseID && run.LakeStep>=1)RevealHerbalist(scene);
                    if(baseId==GardenBaseID)AttackTalismanStore.GardenActors(scene);
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
        internal static void RevealHerbalist(Scene scene){
            if(scene==null || scene.Released || scene.Map.BaseID!=LakeBaseID)throw new InvalidOperationException("Fan spirit scene changed.");
            CityGuardSystem.SpawnTalker(Herbalist,scene.Map.ID,475,840,3694,"HerbalistChou",4);
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
                    throw new InvalidOperationException("Attack Talisman scene ownership changed.");
                MapsTable.MapInformation metadata;
                if(!MapsTable.MapInformations.TryGetValue(scene.Map.ID,out metadata) || !metadata.Equals(scene.Metadata))
                    throw new InvalidOperationException("Attack Talisman metadata ownership changed.");
                if(Kernel.GamePool.Values.Any(p=>p!=null && p.Entity!=null && p.Entity.MapID==scene.Map.ID))
                    throw new InvalidOperationException("Attack Talisman scene must be evacuated first.");
                if(scene.Map.Entities.Count!=0 || scene.Map.Companions.Count!=0)
                    throw new InvalidOperationException("Attack Talisman actors must be removed before terrain release.");
                MapsTable.MapInformations.Remove(scene.Map.ID);scene.Map.Dispose();scenes.Remove(scene.Map.ID);scene.Released=true;
            }
        }
    }
}
