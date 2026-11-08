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
    internal static partial class PowerBookScenes
    {
        internal const int Capacity=16;
        internal const uint FamilyStart=9918400;
        internal const ushort EntryX=77,EntryY=25,ExitX=77,ExitY=27,GateX=140,GateY=140;
        internal const ushort FireX=100,FireY=66;
        internal static readonly ushort[,] HerdPoints={{77,23},{102,136},{146,104}};
        private static readonly string[] hashes={
            "64c54fbf5f6b9a091e1702728b7836afc3537ac32cdbfb4fb501819de1058c34",
            "64c54fbf5f6b9a091e1702728b7836afc3537ac32cdbfb4fb501819de1058c34",
            "25d9fcbd50143d01e67345fdc1fd6eaee8f3634793c1bd04c644491171adc3fd",
            "2a84195994296a0e12b40cd35be8d97dddf0c69f0bf9c780b755c880b4321ad8"};
        internal sealed class Scene
        {
            internal GameState Owner;
            internal Map Map;
            internal MapsTable.MapInformation Metadata;
            internal int Floor;
            internal bool Released,Entered;
            internal Guid Visit=Guid.NewGuid();
            internal DateTime Until;
        }
        private static readonly object gate=new object();
        private static readonly Dictionary<ushort,Scene> scenes=new Dictionary<ushort,Scene>();
        internal static ushort Base(int floor){if(floor<0 || floor>3)throw new ArgumentOutOfRangeException("floor");return (ushort)(1500+floor);}
        internal static void ValidateTerrain(int floor)
        {
            ushort id=Base(floor);
            using(var sha=SHA256.Create())using(var f=File.OpenRead(Path.Combine(Constants.DMapsPath,"maps",id+".map")))
                if(System.BitConverter.ToString(sha.ComputeHash(f)).Replace("-","").ToLowerInvariant()!=hashes[floor])
                    throw new InvalidOperationException("Power Book terrain changed.");
        }
        internal static MonsterInformation Profile(int kind)
        {
            uint[] sources={3141,3142,3144,3145,3147,3148,3155};
            string[] names={"Slinger","GoldGhost","Bladeling","AgileRat","BlueBird","FiendBat","MinotaurL120"};
            byte[] levels={103,108,110,112,115,117,120};
            uint[] hp={30000,60000,35000,70000,45000,80000,60000};
            if(kind<0 || kind>=sources.Length)throw new ArgumentOutOfRangeException("kind");
            MonsterInformation source;
            if(!MonsterInformation.MonsterInformations.TryGetValue(sources[kind],out source) || source.Mesh==0)
                throw new InvalidOperationException("Power Book monster appearance missing.");
            // Copy appearance, then explicitly set Revora trial combat values.
            // In particular, never inherit family 3147's unrelated boss stats.
            var info=new MonsterInformation{Mesh=source.Mesh};info.ID=FamilyStart+(uint)kind;info.Name=names[kind];info.Level=levels[kind];info.Hitpoints=hp[kind];
            info.Boss=info.Guard=info.Reviver=false;info.MinAttack=900;info.MaxAttack=1300;info.Defence=200;
            info.SpellID=0;info.AttackType=1;info.AttackRange=2;info.ViewRange=18;
            info.AttackSpeed=1500;info.MoveSpeed=800;info.RunSpeed=500;info.RespawnTime=1000000000;
            info.OwnItemID=info.OwnItemRate=0;info.Owner=null;
            return info;
        }
        internal static Scene Create(GameState owner,int floor)
        {
            ushort baseId=Base(floor);
            if(!Q.Player(owner) || !Monitor.IsEntered(owner.ItemSyncRoot) || owner.Entity.Level<115 || owner.Entity.Reborn>2)
                throw new InvalidOperationException("Power Book scene requires its locked eligible owner.");
            ValidateTerrain(floor);
            lock(gate)
            {
                if(scenes.Count>=Capacity || scenes.Values.Any(s=>s.Owner.Entity.UID==owner.Entity.UID))
                    throw new InvalidOperationException("Power Book capacity or owner already reserved.");
                var now=PowerBookStore.Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Power Book requires UTC.");
                var scene=new Scene{Owner=owner,Floor=floor,Until=now.AddMinutes(30)};
                try
                {
                    scene.Map=Map.CreateDynamic(baseId,"maps\\"+baseId+".map",false);
                    if(scene.Map.Entities.Count!=0 || scene.Map.Npcs.Count!=0 || scene.Map.Portals.Count!=0)
                        throw new InvalidOperationException("Power Book scene inherited catalog actors.");
                    if(!scene.Map.Floor[EntryX,EntryY,MapObjectType.Player] || !scene.Map.Floor[ExitX,ExitY,MapObjectType.Player]
                        || !scene.Map.Floor[GateX,GateY,MapObjectType.Player] || !scene.Map.Floor[FireX,FireY,MapObjectType.Player])
                        throw new InvalidOperationException("Power Book scene landmarks blocked.");
                    for(int i=0;i<3;i++)if(!scene.Map.Floor[HerdPoints[i,0],HerdPoints[i,1],MapObjectType.Player])
                        throw new InvalidOperationException("Power Book herding point blocked.");
                    if(!Q.Player(owner))throw new InvalidOperationException("Power Book owner changed during allocation.");
                    scene.Metadata=new MapsTable.MapInformation{ID=scene.Map.ID,BaseID=baseId,Owner=owner.Entity.UID,Status=2};
                    MapsTable.MapInformations.Add(scene.Map.ID,scene.Metadata);
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
                    throw new InvalidOperationException("Power Book scene ownership changed.");
                MapsTable.MapInformation metadata;
                if(!MapsTable.MapInformations.TryGetValue(scene.Map.ID,out metadata) || !metadata.Equals(scene.Metadata))
                    throw new InvalidOperationException("Power Book metadata ownership changed.");
                if(Kernel.GamePool.Values.Any(p=>p!=null && p.Entity!=null && p.Entity.MapID==scene.Map.ID))
                    throw new InvalidOperationException("Power Book scene must be evacuated first.");
                if(scene.Map.Entities.Count!=0 || scene.Map.Companions.Count!=0)
                    throw new InvalidOperationException("Power Book actors must be removed before terrain release.");
                MapsTable.MapInformations.Remove(scene.Map.ID);scene.Map.Dispose();scenes.Remove(scene.Map.ID);scene.Released=true;
            }
        }
    }
}
