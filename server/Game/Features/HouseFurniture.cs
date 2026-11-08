using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Npgsql;
using ProtoBuf;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class HouseFurniture
    {
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;
        private sealed class Offer
        {
            internal ConquerItem Item;
            internal uint Uid, Type;
            internal ushort Stack, Map;
            internal bool Bound;
            internal DateTime Until;
        }
        private static readonly ConditionalWeakTable<GameState, Offer> offers = new ConditionalWeakTable<GameState, Offer>();
        private sealed class Purchase { internal uint Type; internal DateTime Until; }
        private static readonly ConditionalWeakTable<GameState, Purchase> purchases = new ConditionalWeakTable<GameState, Purchase>();
        private const ulong BoxSilverPrice = 100000;
        private const uint DecorationCpPrice = 2500;
        private static string PriceLabel(uint type){return type==BoxItem?"100,000 silver":"2,500 CPs";}
        internal const uint BoxItem = 721189;
        private static ushort Appearance(uint type){return type==BoxItem?(ushort)8200:(ushort)((type-721176)*10);}
        private static readonly string[] Names={"Lamp","Low Shelf","Cabinet","High Shelf","Bombe Chest","Rosewood Cabinet","High Cabinet","Folding Screen","Dresser","Basin Rack","Chair","End Table","Item Box"};
        [ProtoContract] private sealed class Control
        {
            [ProtoMember(1, IsRequired=true)] public uint Action;
            [ProtoMember(2, IsRequired=true)] public uint Uid;
            [ProtoMember(3, IsRequired=true)] public uint Type;
            [ProtoMember(4, IsRequired=true)] public uint Look;
        }
        internal static void Initialize()
        {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){Q.Execute(c,t,@"CREATE SEQUENCE IF NOT EXISTS revora_furniture_uid AS bigint MINVALUE 1800000000 MAXVALUE 1899999999 START 1800000000 NO CYCLE;
CREATE TABLE IF NOT EXISTS revora_house_furniture(
actor bigint PRIMARY KEY,uid bigint NOT NULL REFERENCES entities(uid),item_uid bigint NOT NULL UNIQUE,
item_type integer NOT NULL CHECK(item_type BETWEEN 721177 AND 721189),bound boolean NOT NULL,
mapid integer NOT NULL,x integer NOT NULL,y integer NOT NULL,look integer NOT NULL,
placed_at timestamptz NOT NULL DEFAULT now(),removed_at timestamptz);
CREATE UNIQUE INDEX IF NOT EXISTS revora_furniture_cell ON revora_house_furniture(mapid,x,y) WHERE removed_at IS NULL;
CREATE TABLE IF NOT EXISTS revora_furniture_purchases(item_uid bigint PRIMARY KEY,uid bigint NOT NULL REFERENCES entities(uid),
item_type integer NOT NULL CHECK(item_type BETWEEN 721177 AND 721189),silver_paid bigint NOT NULL CHECK(silver_paid>0),purchased_at timestamptz NOT NULL DEFAULT now());
ALTER TABLE revora_house_furniture DROP CONSTRAINT IF EXISTS revora_house_furniture_item_type_check;
ALTER TABLE revora_house_furniture ADD CONSTRAINT revora_house_furniture_item_type_check CHECK(item_type BETWEEN 721177 AND 721189);
ALTER TABLE revora_furniture_purchases DROP CONSTRAINT IF EXISTS revora_furniture_purchases_item_type_check;
ALTER TABLE revora_furniture_purchases ADD CONSTRAINT revora_furniture_purchases_item_type_check CHECK(item_type BETWEEN 721177 AND 721189);
ALTER TABLE revora_furniture_purchases ADD COLUMN IF NOT EXISTS cps_paid bigint NOT NULL DEFAULT 0;
ALTER TABLE revora_furniture_purchases DROP CONSTRAINT IF EXISTS revora_furniture_purchases_silver_paid_check;
ALTER TABLE revora_furniture_purchases ADD CONSTRAINT revora_furniture_purchases_silver_paid_check
 CHECK((silver_paid>0 AND cps_paid=0) OR (silver_paid=0 AND cps_paid>0));");t.Commit();}}
        }
        internal static bool HandleNpc(GameState p,NpcRequest request)
        {
            if(p==null || p.ActiveNpc!=30161)return false;
            if(!Q.Near(p,30161,1002) || p.Map.BaseID!=1002 || p.Entity.PokerTable!=0)return true;
            lock(p.ItemSyncRoot)try{
                if(!Q.Near(p,30161,1002))return true;
                var d=new Npcs(p);
                if(request.OptionID==40){
                    Purchase offer;
                    if(!purchases.TryGetValue(p,out offer))return true;
                    purchases.Remove(p);
                    bool box=offer.Type==BoxItem;uint beforeCp=p.Entity.ConquerPoints;
                    if(Clock()>=offer.Until || p.Inventory.Count>=40 || (box?p.Entity.Money<BoxSilverPrice:beforeCp<DecorationCpPrice)){
                        p.Send("You need an empty pack slot and "+PriceLabel(offer.Type)+".");return true;}
                    var item=Q.New(offer.Type,false);ulong after=box?p.Entity.Money-BoxSilverPrice:p.Entity.Money;
                    using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                        SkillTable.LockOwner(c,t,p);
                        if(box)SilverBalance.Persist(c,t,p,after);
                        else if(Q.Execute(c,t,"UPDATE entities SET conquerpoints=@after WHERE uid=@uid AND conquerpoints=@before",
                            "after",(long)(beforeCp-DecorationCpPrice),"uid",(long)p.Entity.UID,"before",(long)beforeCp)!=1)
                            throw new InvalidOperationException("Furniture CP balance changed.");
                        Q.Insert(c,t,p.Entity.UID,item);
                        if(Q.Execute(c,t,"INSERT INTO revora_furniture_purchases(item_uid,uid,item_type,silver_paid,cps_paid) VALUES(@item,@uid,@type,@silver,@cp)",
                            "item",(long)item.UID,"uid",(long)p.Entity.UID,"type",(int)item.ID,"silver",box?(long)BoxSilverPrice:0L,"cp",box?0L:(long)DecorationCpPrice)!=1)
                            throw new InvalidOperationException("Furniture purchase was not recorded.");
                        if(!Q.Near(p,30161,1002) || p.Entity.PokerTable!=0 || Clock()>=offer.Until)throw new InvalidOperationException("Furniture buyer moved.");
                        t.Commit();
                    }}
                    Q.Install(p,new uint[0],new[]{item});
                    if(box)SilverBalance.ApplyCommitted(p,after);else p.Entity.ApplyCommittedConquerPoints(beforeCp-DecorationCpPrice);
                    d.Text("A fine choice. Unpack it inside your own home.");
                }else if(request.OptionID>=10 && request.OptionID<23){
                    purchases.Remove(p);uint type=(uint)(721177+request.OptionID-10);
                    purchases.Add(p,new Purchase{Type=type,Until=Clock().AddMinutes(2)});
                    d.Text(Names[request.OptionID-10]+" costs "+PriceLabel(type)+". Once placed, removing it destroys it.");d.Option("I'll take it.",40);
                }else{
                    purchases.Remove(p);
                    if(request.OptionID==255)return true;
                    if(request.OptionID>2)return true;
                    d.Text(request.OptionID==2?"An Item Box costs 100,000 silver.":"A comfortable home deserves fine furnishings. Each decoration costs 2,500 CPs.");
                    int page=request.OptionID*6;
                    for(int i=page;i<Math.Min(page+6,Names.Length);i++)d.Option(Names[i]+".",(byte)(10+i));
                    d.Option(page<12?"Show me more.":"Back.",page<12?(byte)(request.OptionID+1):(byte)0);
                }
                d.Option("Farewell.",255);d.Send();
            }catch(Exception ex){Q.Fail(p,"house-furniture-shop",ex);}return true;
        }
        private static bool Ready(GameState p)
        {
            MapsTable.MapInformation house;
            return Housing.Enabled && Q.Player(p) && p.Trade!=null && !p.Trade.InTrade && p.Booth==null && p.Entity.PokerTable==0
                && p.Map!=null && MapsTable.MapInformations.TryGetValue(p.Entity.MapID,out house) && house.Owner==p.Entity.UID;
        }
        private static bool Usable(ConquerItem i)
        {
            return i!=null && i.ID>=721177 && i.ID<=BoxItem && i.StackSize<=1 && i.Position==0 && i.Warehouse==0
                && i.Lock==0 && !i.Suspicious && !i.InWardrobe && i.Days==0 && i.TimeLeftInMinutes==0;
        }
        private static bool Same(GameState p,Offer o)
        {
            ConquerItem i;
            return Ready(p) && p.Entity.MapID==o.Map && Clock()<o.Until && p.Inventory.TryGetItem(o.Uid,out i)
                && ReferenceEquals(i,o.Item) && Usable(i) && i.ID==o.Type && i.StackSize==o.Stack && i.Bound==o.Bound;
        }
        private static int SavedHouse(NpgsqlConnection c,NpgsqlTransaction t,GameState p)
        {
            SkillTable.LockOwner(c,t,p);
            int level=Housing.Level(c,t,p.Entity.UID);
            using(var q=Q.Command(c,t,"SELECT map_id FROM revora_house_maps WHERE uid=@uid","uid",(long)p.Entity.UID))
                if(Convert.ToInt64(q.ExecuteScalar())!=p.Entity.MapID)throw new InvalidOperationException("Furniture house ownership changed.");
            return level;
        }
        private static bool Room(NpgsqlConnection c,NpgsqlTransaction t,GameState p,int level,uint type)
        {
            bool box=type==BoxItem;
            if(level<(box?1:2) || level>5)return false;
            using(var q=Q.Command(c,t,"SELECT count(*) FROM furniture WHERE mapid=@map AND ((type=4)=@box)","map",(long)p.Entity.MapID,"box",box))
                return Convert.ToInt64(q.ExecuteScalar())<(box?(level==1?1:2):new[]{0,0,8,9,10,12}[level]);
        }
        internal static bool TryUse(ConquerItem item,GameState p)
        {
            if(item==null || item.ID<721177 || item.ID>BoxItem)return false;
            if(!Ready(p)){if(p!=null)p.Send("Bring this furnishing into your own home first.");return true;}
            lock(p.ItemSyncRoot)try{
                offers.Remove(p);ConquerItem owned;
                if(!Ready(p) || !Usable(item) || !p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(item,owned))return true;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    if(!Room(c,t,p,SavedHouse(c,t,p),item.ID)){p.Send("There is no room for more furniture in this home.");return true;}
                    t.Commit();
                }}
                offers.Add(p,new Offer{Item=item,Uid=item.UID,Type=item.ID,Stack=item.StackSize,Bound=item.Bound,Map=p.Entity.MapID,Until=Clock().AddMinutes(2)});
                p.Send(new Control{Action=5,Uid=0,Type=item.ID==BoxItem?4u:26u,Look=Appearance(item.ID)}.ToConquerPacket(2031));
            }catch(Exception ex){Q.Fail(p,"house-furniture-select",ex);}return true;
        }
        private static bool Cell(GameState p,ushort x,ushort y,ushort look)
        {
            if(Kernel.GetDistance(p.Entity.X,p.Entity.Y,x,y)>17)return false;
            var cells=FurnitureFootprint.Cells(x,y,look).ToArray();
            return cells.All(cell=>cell.X>=0 && cell.Y>=0 && cell.X<p.Map.Floor.Bounds.Width && cell.Y<p.Map.Floor.Bounds.Height
                && p.Map.Floor[cell.X,cell.Y,MapObjectType.Player]
                && !p.Map.Portals.Any(v=>Kernel.GetDistance(v.CurrentX,v.CurrentY,(ushort)cell.X,(ushort)cell.Y)<=3)
                && !p.Map.Npcs.Values.Any(v=>FurnitureFootprint.Known(v)?FurnitureFootprint.Cells(v.X,v.Y,v.Mesh).Contains(cell):v.X==cell.X && v.Y==cell.Y)
                && !Kernel.GamePool.Values.Any(v=>v!=null && v.Entity!=null && v.Entity.MapID==p.Entity.MapID && v.Entity.X==cell.X && v.Entity.Y==cell.Y));
        }
        internal static void Place(GameState p,byte[] frame)
        {
            FurnitureRequest.Placement request;
            if(!FurnitureRequest.TryPlacement(frame,out request) || !Ready(p))return;
            lock(p.ItemSyncRoot)try{
                Offer o;
                if(!offers.TryGetValue(p,out o))return;
                if(!Same(p,o)){offers.Remove(p);return;}
                ushort model=Appearance(o.Type);
                if(request.Look/10!=model/10 || request.Look%10>7 || !Cell(p,request.X,request.Y,request.Look))return;
                uint actor;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    if(!Room(c,t,p,SavedHouse(c,t,p),o.Type)){offers.Remove(p);p.Send("There is no room for more furniture in this home.");return;}
                    using(var q=Q.Command(c,t,"SELECT nextval('revora_furniture_uid')"))actor=checked((uint)Convert.ToInt64(q.ExecuteScalar()));
                    if(p.Map.Npcs.ContainsKey(actor))throw new InvalidOperationException("Furniture identity collision.");
                    if(o.Type==BoxItem){
                        if(p.Warehouses.ContainsKey((ConquerStructures.Warehouse.WarehouseID)actor))throw new InvalidOperationException("Box storage identity collision.");
                        using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM items WHERE uid=@actor OR warehouse=@actor)","actor",(long)actor))
                            if(Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Box identity has existing item state.");
                    }
                    if(Q.Execute(c,t,@"DELETE FROM items WHERE uid=@item AND entityid=@uid AND id=@type AND position=0 AND warehouse=0
AND COALESCE(stacksize,0)=@stack AND COALESCE(bound,0)=@bound AND COALESCE(locked,0)=0 AND COALESCE(suspicious,0)=0
AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0'",
                        "item",(long)o.Uid,"uid",(long)p.Entity.UID,"type",(int)o.Type,"stack",(int)o.Stack,"bound",o.Bound?1:0)!=1)
                        throw new InvalidOperationException("Furniture material changed.");
                    if(Q.Execute(c,t,@"INSERT INTO furniture(id,nameowner,type,lookface,mapid,cellx,celly)
VALUES(@actor,@name,@npcType,@look,@map,@x,@y)","actor",(long)actor,"name",p.Entity.Name,"npcType",o.Type==BoxItem?4:26,"look",(int)request.Look,"map",(long)o.Map,"x",(int)request.X,"y",(int)request.Y)!=1
                        || Q.Execute(c,t,@"INSERT INTO revora_house_furniture(actor,uid,item_uid,item_type,bound,mapid,x,y,look)
VALUES(@actor,@uid,@item,@type,@bound,@map,@x,@y,@look)","actor",(long)actor,"uid",(long)p.Entity.UID,"item",(long)o.Uid,"type",(int)o.Type,"bound",o.Bound,
                        "map",(int)o.Map,"x",(int)request.X,"y",(int)request.Y,"look",(int)request.Look)!=1)
                        throw new InvalidOperationException("Furniture placement was not recorded.");
                    if(o.Type!=BoxItem)ProgressionEvidence.Complete(c,t,p.Entity.UID,10101,"house-furniture-v1",actor.ToString(System.Globalization.CultureInfo.InvariantCulture),1);
                    if(!Same(p,o) || !Cell(p,request.X,request.Y,request.Look))throw new InvalidOperationException("Furniture placement changed.");
                    t.Commit();
                }}
                offers.Remove(p);Q.Install(p,new[]{o.Uid},new ConquerItem[0]);
                var npc=new NpcSpawn{UID=actor,MapID=o.Map,X=request.X,Y=request.Y,Mesh=request.Look,Type=o.Type==BoxItem?(Enums.NpcType)4:Enums.NpcType.Furniture};
                p.Map.AddNpc(npc);npc.SendSpawn(p);if(o.Type==BoxItem)RegisterBox(p,actor);Q.Refresh(p);
            }catch(Exception ex){offers.Remove(p);Q.Fail(p,"house-furniture-place",ex);}
        }
        internal static bool Remove(GameState p,byte[] frame)
        {
            uint actor;if(!FurnitureRequest.TryRemoval(frame,out actor))return false;
            if(!Ready(p))return true;
            lock(p.ItemSyncRoot)try{
                if(!Ready(p))return true;
                Interfaces.INpc npc;
                if(!p.Map.Npcs.TryGetValue(actor,out npc) || (npc.Type!=Enums.NpcType.Furniture && !BoxActor(npc)) || Kernel.GetDistance(p.Entity.X,p.Entity.Y,npc.X,npc.Y)>17)return true;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    SavedHouse(c,t,p);
                    if(BoxActor(npc))using(var q=Q.Command(c,t,"SELECT count(*) FROM items WHERE warehouse=@actor","actor",(long)actor))
                        if(Convert.ToInt64(q.ExecuteScalar())!=0){p.Send("Empty the box before removing it.");return true;}
                    if(Q.Execute(c,t,@"UPDATE revora_house_furniture SET removed_at=now() WHERE actor=@actor AND uid=@uid AND mapid=@map
AND x=@x AND y=@y AND look=@look AND removed_at IS NULL","actor",(long)actor,"uid",(long)p.Entity.UID,"map",(int)p.Entity.MapID,
                        "x",(int)npc.X,"y",(int)npc.Y,"look",(int)npc.Mesh)!=1)return true;
                    if(Q.Execute(c,t,@"DELETE FROM furniture WHERE id=@actor AND mapid=@map AND type=@npcType AND cellx=@x AND celly=@y AND lookface=@look",
                        "actor",(long)actor,"npcType",(int)npc.Type,"map",(long)p.Entity.MapID,"x",(int)npc.X,"y",(int)npc.Y,"look",(int)npc.Mesh)!=1)
                        throw new InvalidOperationException("Furniture removal was not saved.");
                    if(!Ready(p))throw new InvalidOperationException("Furniture owner session changed.");
                    t.Commit();
                }}
                offers.Remove(p);if(BoxActor(npc))p.Warehouses.Remove((Game.ConquerStructures.Warehouse.WarehouseID)actor);p.Map.RemoveNpc(npc);p.Screen.Remove((Interfaces.IMapObject)npc);
                p.Send(new Control{Action=3,Uid=actor,Type=0,Look=0}.ToConquerPacket(2031));
            }catch(Exception ex){Q.Fail(p,"house-furniture-remove",ex);}return true;
        }
        internal static bool BoxActor(Interfaces.INpc npc)
        {
            return npc!=null && npc.UID>=1800000000 && npc.UID<=1899999999 && (byte)npc.Type==4
                && npc.Mesh/10==820 && npc.Mesh%10<=7;
        }
        private static void RegisterBox(GameState p,uint actor)
        {
            var key=(ConquerStructures.Warehouse.WarehouseID)actor;ConquerStructures.Warehouse existing;
            if(p.Warehouses.TryGetValue(key,out existing)){
                if(!existing.HouseBox)throw new InvalidOperationException("House box storage key collision.");
                return;
            }
            p.Warehouses.Add(key,new ConquerStructures.Warehouse(p,key,60,true));
        }
        internal static void LoadBoxes(GameState p)
        {
            if(!Housing.Enabled || p==null || p.Fake)return;
            lock(p.ItemSyncRoot)try{
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    SkillTable.LockOwner(c,t,p);
                    var actors=new System.Collections.Generic.List<uint>();
                    using(var q=Q.Command(c,t,@"SELECT r.actor,COALESCE(h.map_id=r.mapid AND f.mapid=r.mapid AND f.type=4
AND f.lookface=r.look AND f.cellx=r.x AND f.celly=r.y AND r.look BETWEEN 8200 AND 8207
AND (SELECT count(*) FROM items i WHERE i.warehouse=r.actor)<=60
AND NOT EXISTS(SELECT 1 FROM items i WHERE i.warehouse=r.actor AND i.entityid<>r.uid),false)
FROM revora_house_furniture r LEFT JOIN furniture f ON f.id=r.actor LEFT JOIN revora_house_maps h ON h.uid=r.uid
WHERE r.uid=@uid AND r.item_type=721189 AND r.removed_at IS NULL","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader())while(r.Read()){
                        uint actor=Convert.ToUInt32(r[0]);
                        if(!r.GetBoolean(1) || actor<1800000000 || actor>1899999999)throw new InvalidOperationException("Saved box requires reconciliation.");
                        actors.Add(actor);
                    }
                    if(actors.Count>0){int level=Housing.Level(c,t,p.Entity.UID);
                        if(level<1 || actors.Count>(level==1?1:2))throw new InvalidOperationException("Saved box capacity is invalid.");}
                    using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM items i WHERE i.entityid=@uid
AND i.warehouse BETWEEN 1800000000 AND 1899999999 AND NOT EXISTS(SELECT 1 FROM revora_house_furniture r
WHERE r.actor=i.warehouse AND r.uid=i.entityid AND r.item_type=721189 AND r.removed_at IS NULL))","uid",(long)p.Entity.UID))
                        if(Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Saved box contents have no owned active box.");
                    t.Commit();foreach(uint actor in actors)RegisterBox(p,actor);
                }}
            }catch(Exception ex){Q.Fail(p,"house-box-load",ex);throw;}
        }
        internal static bool StorageAccess(NpgsqlConnection c,NpgsqlTransaction t,GameState p,uint actor,bool opening=false)
        {
            Interfaces.INpc npc;
            if(!Ready(p) || (!opening && p.ActiveNpc!=actor) || p.WarehousePW!=0 && !p.WarehouseOpen
                || !p.Map.Npcs.TryGetValue(actor,out npc) || !BoxActor(npc) || Kernel.GetDistance(p.Entity.X,p.Entity.Y,npc.X,npc.Y)>6)return false;
            if(SavedHouse(c,t,p)<1)return false;
            using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_house_furniture r JOIN furniture f ON f.id=r.actor
WHERE r.actor=@actor AND r.uid=@uid AND r.mapid=@map AND r.item_type=721189 AND r.removed_at IS NULL
AND f.type=4 AND f.mapid=r.mapid AND f.cellx=r.x AND f.celly=r.y AND f.lookface=r.look
AND r.x=@x AND r.y=@y AND r.look=@look)","actor",(long)actor,"uid",(long)p.Entity.UID,"map",(int)p.Entity.MapID,
                "x",(int)npc.X,"y",(int)npc.Y,"look",(int)npc.Mesh))return Equals(q.ExecuteScalar(),true);
        }
        internal static bool OpenBox(GameState p,byte[] frame)
        {
            uint actor;if(!FurnitureRequest.TryInteraction(frame,out actor) || actor<1800000000 || actor>1899999999)return false;
            if(!Ready(p))return true;
            lock(p.ItemSyncRoot)try{
                Interfaces.INpc npc;
                if(!Ready(p) || !p.Map.Npcs.TryGetValue(actor,out npc) || !BoxActor(npc)
                    || Kernel.GetDistance(p.Entity.X,p.Entity.Y,npc.X,npc.Y)>6)return true;
                if(p.WarehousePW!=0 && !p.WarehouseOpen){
                    p.Send(new Data(true){ID=Data.OpenWindow,UID=p.Entity.UID,TimeStamp=Time32.Now,dwParam=568,wParam1=p.Entity.X,wParam2=p.Entity.Y});
                    p.Send("Unlock your storage, then open the box again.");return true;
                }
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    if(!StorageAccess(c,t,p,actor,true))return true;t.Commit();
                }}
                RegisterBox(p,actor);p.ActiveNpc=actor;
                p.Send(new Data(true){ID=Data.OpenWindow,UID=p.Entity.UID,TimeStamp=Time32.Now,
                    dwParam=Data.WindowCommands.Warehouse,wParam1=p.Entity.X,wParam2=p.Entity.Y});
            }catch(Exception ex){Q.Fail(p,"house-box-open",ex);}return true;
        }
    }
}
