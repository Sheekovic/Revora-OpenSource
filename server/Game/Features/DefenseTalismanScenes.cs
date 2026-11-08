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
    internal static partial class DefenseTalismanScenes
    {
        internal const int Capacity=8;
        internal const uint FamilyStart=9905200;
        internal const ushort EntryX=45,EntryY=60;
        internal static readonly ushort[,] SpawnPoints={{40,55},{42,55},{44,55},{46,55},{48,55}};
        internal static Func<DateTime> Clock=()=>DefenseTalismanStore.Clock();
        private const string TerrainHash="9619a90984e1fd2f959ce89f918075353a5bc5839cbdd6d4eef2a1b05c9a87c4";
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
        internal const ushort BaseID=1782;
        private static void ValidateTerrain(ushort id)
        {
            string expected=id==1785?"5b8dc4a922f7ce95eb3b2b9245bdbe195983bca35f6714d896a9851531ba13be":id==1786?"2325d5c9b27d8cbc80b23e197429bfe7d632f9e8d2a1dc010155f73bdb31b6c0":id==1787?"9b7aca4f515a5c3402113ba946dbc73cd8b57bf01cb957e8a91e059fbe13434a":TerrainHash;
            using(var sha=SHA256.Create())using(var f=File.OpenRead(Path.Combine(Constants.DMapsPath,"maps",id+".map")))
                if(System.BitConverter.ToString(sha.ComputeHash(f)).Replace("-","").ToLowerInvariant()!=expected)
                    throw new InvalidOperationException("Defense Talisman terrain changed.");
        }
        internal static void ValidateProfiles(){
            for(ushort id=1782;id<=1787;id++)ValidateTerrain(id);
            foreach(ushort id in new ushort[]{1000,1015})using(var sha=SHA256.Create())using(var f=File.OpenRead(Path.Combine(Constants.DMapsPath,"maps",id+".map")))
                if(System.BitConverter.ToString(sha.ComputeHash(f)).Replace("-","").ToLowerInvariant()!=(id==1000?"c874aa5659420dfcf03545f1bfad67bea0197ae55fbc8140fcd7e85ffbae33f1":"e47df70501200965040755a424873395172c03e7dad920d483414f85c0359ba7"))throw new InvalidOperationException("Tower public terrain changed.");
        }
        internal static Scene Create(GameState owner){return CreateScene(owner,BaseID);}
        internal static Scene CreateHut(GameState owner){return CreateScene(owner,1784);}
        internal static Scene CreateAmbush(GameState owner){return CreateScene(owner,1783);}
        internal static Scene CreateDungeon(GameState owner){
            if(!DefenseTalismanStore.Eligible(owner) || !Monitor.IsEntered(owner.ItemSyncRoot))throw new InvalidOperationException("Tower dungeon requires its locked owner.");
            var run=DefenseTalismanStore.Load(owner);if(run.Stage!=8 || run.DungeonFloor<1 || run.DungeonFloor>3)throw new InvalidOperationException("Tower dungeon is not unlocked.");
            return CreateScene(owner,(ushort)(1784+run.DungeonFloor));
        }
        private static Scene CreateScene(GameState owner,ushort baseId)
        {
            if(!DefenseTalismanStore.Eligible(owner) || !Monitor.IsEntered(owner.ItemSyncRoot))
                throw new InvalidOperationException("Defense Talisman scene requires its locked eligible owner.");
            ValidateTerrain(baseId);
            lock(gate)
            {
                if(scenes.Count>=Capacity || scenes.Values.Any(s=>s.Owner.Entity.UID==owner.Entity.UID))
                    throw new InvalidOperationException("Defense Talisman capacity or owner already reserved.");
                var now=Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Defense Talisman requires UTC.");
                var scene=new Scene{Owner=owner,Until=now.AddMinutes(10),EntryX=(ushort)(baseId==1785?96:baseId==1786?113:baseId==1787?56:EntryX),EntryY=(ushort)(baseId==1785?124:baseId==1786?126:baseId==1787?68:EntryY)};
                try
                {
                    scene.Map=Map.CreateDynamic(baseId,"maps\\"+baseId+".map",false);
                    if(scene.Map.Entities.Count!=0 || scene.Map.Npcs.Count!=0 || scene.Map.Portals.Count!=0)
                        throw new InvalidOperationException("Defense Talisman scene inherited catalog actors.");
                    if(!scene.Map.Floor[scene.EntryX,scene.EntryY,MapObjectType.Player])throw new InvalidOperationException("Tower entry blocked.");
                    ushort[,] points=baseId==1785?new ushort[,]{{96,120},{70,60},{70,56}}:baseId==1786?new ushort[,]{{113,122},{62,81},{62,77}}:baseId==1787?new ushort[,]{{56,64},{48,39}}:SpawnPoints;
                    for(int i=0;i<points.GetLength(0);i++)if(!scene.Map.Floor[points[i,0],points[i,1],MapObjectType.Player])
                        throw new InvalidOperationException("Tower spawn point blocked.");
                    if(!Q.Player(owner))throw new InvalidOperationException("Defense Talisman owner changed during allocation.");
                    scene.Metadata=new MapsTable.MapInformation{ID=scene.Map.ID,BaseID=baseId,Owner=owner.Entity.UID,Status=2};
                    MapsTable.MapInformations.Add(scene.Map.ID,scene.Metadata);
                    DefenseTalismanQuest.AddSceneNpcs(scene);scenes.Add(scene.Map.ID,scene);return scene;
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
                    throw new InvalidOperationException("Defense Talisman scene ownership changed.");
                MapsTable.MapInformation metadata;
                if(!MapsTable.MapInformations.TryGetValue(scene.Map.ID,out metadata) || !metadata.Equals(scene.Metadata))
                    throw new InvalidOperationException("Defense Talisman metadata ownership changed.");
                if(Kernel.GamePool.Values.Any(p=>p!=null && p.Entity!=null && p.Entity.MapID==scene.Map.ID))
                    throw new InvalidOperationException("Defense Talisman scene must be evacuated first.");
                if(scene.Map.Entities.Count!=0 || scene.Map.Companions.Count!=0)
                    throw new InvalidOperationException("Defense Talisman actors must be removed before terrain release.");
                MapsTable.MapInformations.Remove(scene.Map.ID);scene.Map.Dispose();scenes.Remove(scene.Map.ID);scene.Released=true;
            }
        }
    }
}
