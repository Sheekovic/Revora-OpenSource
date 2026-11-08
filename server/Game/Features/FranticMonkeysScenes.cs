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
    internal static partial class FranticMonkeysScenes
    {
        internal const int Capacity=8;
        internal const uint FamilyStart=9917450;
        internal const ushort EntryX=795,EntryY=471;
        internal static readonly ushort[,] SpawnPoints={{794,470},{796,470},{794,472},{796,472},{795,471}};
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        private const string TerrainHash="bdb39e79fd6a7701da0ea10a12c0753e8cd6f4187ff8719b05608699bc903b95";
        internal sealed class Scene
        {
            internal GameState Owner;
            internal Map Map;
            internal MapsTable.MapInformation Metadata;

            internal bool Released,Entered;
            internal Guid Visit=Guid.NewGuid(),Appearance;
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
                    throw new InvalidOperationException("Frantic Monkeys terrain changed.");
        }
        internal static MonsterInformation Profile(int kind)
        {
            if(kind<0 || kind>2)throw new ArgumentOutOfRangeException("kind");
            MonsterInformation source;
            if(!MonsterInformation.MonsterInformations.TryGetValue(10,out source) || source.Name!="Macaque" || source.Mesh!=156)
                throw new InvalidOperationException("Frantic monkey appearance changed.");
            // Revora trial/encounter profiles; only the verified macaque mesh is reused.
            var info=new MonsterInformation{Mesh=source.Mesh};info.ID=FamilyStart+(uint)kind;
            info.Name=kind==0?"PoisonTrialMonkey":kind==1?"VenomousApe":"ApeLeader";info.Level=50;
            info.Hitpoints=kind==0?1467u:kind==1?4400u:8800u;
            info.Boss=info.Guard=info.Reviver=false;info.MinAttack=kind==0?120u:kind==1?260u:400u;info.MaxAttack=kind==0?200u:kind==1?380u:600u;info.Defence=100;
            info.SpellID=0;info.AttackType=1;info.AttackRange=2;info.ViewRange=18;
            info.AttackSpeed=1500;info.MoveSpeed=800;info.RunSpeed=500;info.RespawnTime=1000000000;
            info.OwnItemID=info.OwnItemRate=0;info.Owner=null;
            return info;
        }
        internal static Scene Create(GameState owner)
        {
            ushort baseId=BaseID;
            if(!FranticMonkeysStore.Eligible(owner) || !Monitor.IsEntered(owner.ItemSyncRoot))
                throw new InvalidOperationException("Frantic Monkeys scene requires its locked eligible owner.");
            ValidateTerrain();
            lock(gate)
            {
                if(scenes.Count>=Capacity || scenes.Values.Any(s=>s.Owner.Entity.UID==owner.Entity.UID))
                    throw new InvalidOperationException("Frantic Monkeys capacity or owner already reserved.");
                var now=Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Frantic Monkeys requires UTC.");
                var scene=new Scene{Owner=owner,Until=now.AddMinutes(10)};
                try
                {
                    scene.Map=Map.CreateDynamic(baseId,"maps\\"+baseId+".map",false);
                    if(scene.Map.Entities.Count!=0 || scene.Map.Npcs.Count!=0 || scene.Map.Portals.Count!=0)
                        throw new InvalidOperationException("Frantic Monkeys scene inherited catalog actors.");
                    if(!scene.Map.Floor[EntryX,EntryY,MapObjectType.Player])throw new InvalidOperationException("Frantic entry blocked.");
                    for(int i=0;i<SpawnPoints.GetLength(0);i++)if(!scene.Map.Floor[SpawnPoints[i,0],SpawnPoints[i,1],MapObjectType.Player])
                        throw new InvalidOperationException("Frantic spawn point blocked.");
                    if(!Q.Player(owner))throw new InvalidOperationException("Frantic Monkeys owner changed during allocation.");
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
                    throw new InvalidOperationException("Frantic Monkeys scene ownership changed.");
                MapsTable.MapInformation metadata;
                if(!MapsTable.MapInformations.TryGetValue(scene.Map.ID,out metadata) || !metadata.Equals(scene.Metadata))
                    throw new InvalidOperationException("Frantic Monkeys metadata ownership changed.");
                if(Kernel.GamePool.Values.Any(p=>p!=null && p.Entity!=null && p.Entity.MapID==scene.Map.ID))
                    throw new InvalidOperationException("Frantic Monkeys scene must be evacuated first.");
                if(scene.Map.Entities.Count!=0 || scene.Map.Companions.Count!=0)
                    throw new InvalidOperationException("Frantic Monkeys actors must be removed before terrain release.");
                MapsTable.MapInformations.Remove(scene.Map.ID);scene.Map.Dispose();scenes.Remove(scene.Map.ID);scene.Released=true;
            }
        }
    }
}
