using System;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class GrottoKeyDrops
    {
        private sealed class Life { internal Time32 Stamp; internal bool Rolled; }
        private sealed class Origin {
            internal Guid Id; internal uint Monster,Family,ItemUid,FloorUid,Owner;
            internal ushort Map,X,Y; internal ConquerItem Item;
        }
        private static readonly ConditionalWeakTable<Entity,Life> lives=new ConditionalWeakTable<Entity,Life>();
        private static readonly ConditionalWeakTable<FloorItem,Origin> origins=new ConditionalWeakTable<FloorItem,Origin>();
        internal static Func<int,int> Roll=max=>Kernel.Random.Next(max);
        private static bool HuntingMap(ushort id) {return id==1999 || id==2054;}
        internal static void Initialize() {
            if(!ConquerItemInformation.BaseInformations.ContainsKey(GrottoPassage.Key))throw new InvalidOperationException("Grotto Key catalog entry is missing.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_grotto_key_pickups(
id uuid PRIMARY KEY,item_uid bigint NOT NULL UNIQUE CHECK(item_uid>0),monster_uid bigint NOT NULL,
monster_type bigint NOT NULL,map integer NOT NULL CHECK(map IN(1999,2054)),
collector bigint NOT NULL CHECK(collector>0),picked_at timestamptz NOT NULL DEFAULT now());");}
        }
        internal static void TryDrop(Entity monster,Entity killer) {
            if(monster==null || monster.MonsterInfo==null || killer==null || !Q.Player(killer.Owner) || !ReferenceEquals(killer.Owner.Entity,killer)
                || !ReferenceEquals(monster.MonsterInfo.Owner,monster) || monster.EntityFlag!=EntityFlag.Monster || !monster.Dead
                || monster.Companion || monster.MonsterInfo.Boss || monster.MonsterInfo.Guard || monster.MonsterInfo.Reviver
                || CityGuardSystem.IsCityGuard(monster) || LivingWorldSystem.IsAdventurer(monster)
                || monster.MapID!=killer.MapID || !HuntingMap(monster.MapID))return;
            var map=killer.Owner.Map;Entity current;
            if(map==null || map.ID!=monster.MapID || map.BaseID!=map.ID || map.Floor==null
                || !map.Entities.TryGetValue(monster.UID,out current) || !ReferenceEquals(current,monster))return;
            lock(monster) {
                var life=lives.GetValue(monster,_=>new Life());if(life.Rolled && life.Stamp==monster.DeathStamp)return;
                life.Rolled=true;life.Stamp=monster.DeathStamp;
                int chance=Math.Max(0,Math.Min(10000,Config.GrottoKeyBasisPoints));
                if(chance==0 || Roll(10000)>=chance)return;
                ushort x=monster.X,y=monster.Y;if(!map.SelectCoordonates(ref x,ref y))return;
                var item=Q.New(GrottoPassage.Key,false);
                var drop=new FloorItem(true){UID=FloorItem.FloorUID.Next,Item=item,ItemID=item.ID,MapID=map.ID,
                    X=x,Y=y,Owner=killer.Owner,OwnerUID=killer.UID,Type=FloorItem.Drop,ValueType=FloorItem.FloorValueType.Item,
                    OnFloor=Time32.Now,ItemColor=item.Color,MapObjType=MapObjectType.Item};
                while(map.Npcs.ContainsKey(drop.UID) || map.FloorItems.ContainsKey(drop.UID))drop.UID=FloorItem.FloorUID.Next;
                origins.Add(drop,new Origin{Id=Guid.NewGuid(),Monster=monster.UID,Family=monster.MonsterInfo.ID,
                    Item=item,ItemUid=item.UID,FloorUid=drop.UID,Owner=killer.UID,Map=map.ID,X=x,Y=y});
                map.AddFloorItem(drop);monster.MonsterInfo.SendScreenSpawn(drop);
            }
        }
        internal static bool Managed(FloorItem drop) {Origin origin;return drop!=null && origins.TryGetValue(drop,out origin);}
        internal static bool Pickup(GameState p,FloorItem drop) {
            Origin origin;if(drop==null || !origins.TryGetValue(drop,out origin))return false;
            if(!Q.Player(p))return true;
            lock(p.ItemSyncRoot)lock(drop)try {
                var map=p.Map;FloorItem current;
                if(!Q.Player(p) || p.Booth!=null || p.Trade==null || p.Trade.InTrade || map==null || map.ID!=origin.Map || map.BaseID!=map.ID
                    || p.Entity.X!=origin.X || p.Entity.Y!=origin.Y || drop.PickedUpAlready || drop.Type!=FloorItem.Drop
                    || Time32.Now>=drop.OnFloor.AddSeconds(Constants.FloorItemSeconds)
                    || !map.FloorItems.TryGetValue(origin.FloorUid,out current) || !ReferenceEquals(current,drop))return true;
                if(origin.Owner!=p.Entity.UID && Time32.Now<drop.OnFloor.AddSeconds(Constants.FloorItemAvailableAfter)
                    && !(p.Team!=null && p.Team.PickupItems && p.Team.IsTeammate(origin.Owner))) {p.Send(Constants.FloorItemNotAvailable);return true;}
                var item=origin.Item;
                if(drop.UID!=origin.FloorUid || drop.OwnerUID!=origin.Owner || drop.MapID!=origin.Map || drop.X!=origin.X || drop.Y!=origin.Y
                    || !ReferenceEquals(item,drop.Item) || item.UID!=origin.ItemUid || item.ID!=GrottoPassage.Key || drop.ItemID!=item.ID
                    || item.Bound || item.Plus!=0 || item.StackSize!=1 || item.Position!=0 || item.Warehouse!=0 || item.Lock!=0
                    || item.Suspicious || item.InWardrobe || item.Days!=0)
                    throw new InvalidOperationException("Grotto Key origin changed.");
                if(p.Inventory.Count>=40){p.Send(Constants.FullInventory);return true;}
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    SkillTable.LockOwner(c,t,p);
                    using(var q=Q.Command(c,t,"SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0","uid",(long)p.Entity.UID))
                        if(Convert.ToInt64(q.ExecuteScalar())!=p.Inventory.Count)throw new InvalidOperationException("Grotto Key inventory requires reload.");
                    if(Q.Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability)
VALUES(@item,721347,@uid,0,0,1,0,@dur,@max)","item",(long)item.UID,"uid",(long)p.Entity.UID,
                        "dur",(int)item.Durability,"max",(int)item.MaximDurability)!=1)throw new InvalidOperationException("Grotto Key was not saved.");
                    if(Q.Execute(c,t,@"INSERT INTO revora_grotto_key_pickups(id,item_uid,monster_uid,monster_type,map,collector)
VALUES(@id,@item,@monster,@family,@map,@uid)","id",origin.Id,"item",(long)item.UID,"monster",(long)origin.Monster,
                        "family",(long)origin.Family,"map",(int)origin.Map,"uid",(long)p.Entity.UID)!=1)
                        throw new InvalidOperationException("Grotto Key pickup was not recorded.");
                    t.Commit();
                }}
                drop.PickedUpAlready=true;drop.Type=FloorItem.Remove;map.RemoveFloorItem(drop);
                Q.Install(p,new uint[0],new[]{item});p.RemoveScreenSpawn(drop,true);
                p.Send(Constants.PickupItem(ConquerItemInformation.BaseInformations[item.ID].Name));
            }catch(Exception ex){Q.Fail(p,"grotto-key-pickup",ex);}
            return true;
        }
    }
}
