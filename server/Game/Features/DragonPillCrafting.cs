using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class DragonPillCrafting
    {
        internal const uint Guardian=7767,DragonPill=720598,Fragment=728120;
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        private sealed class Take {internal ConquerItem Item;internal uint Uid,Type;internal ushort Before,Amount;internal bool Bound;internal uint Days;internal DateTime Stamp;}
        private sealed class Offer {internal Guid Id;internal int Recipe;internal DateTime Until;internal List<Take> Materials;}
        private static readonly ConditionalWeakTable<GameState,Offer> offers=new ConditionalWeakTable<GameState,Offer>();
        internal static void Initialize() {
            foreach(uint id in new uint[]{711181,711182,711183,711184,711185,711186,711187,DragonPill,Fragment})
                if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Dragon Pill crafting catalog is missing.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_dragon_pill_crafts(
id uuid PRIMARY KEY,uid bigint NOT NULL CHECK(uid>0),recipe integer NOT NULL CHECK(recipe IN(1,2)),
item_uid bigint NOT NULL UNIQUE CHECK(item_uid>0),bound boolean NOT NULL,created_at timestamptz NOT NULL DEFAULT now());
CREATE TABLE IF NOT EXISTS revora_dragon_pill_materials(
craft uuid NOT NULL REFERENCES revora_dragon_pill_crafts(id),item_uid bigint NOT NULL CHECK(item_uid>0),
item_type integer NOT NULL CHECK(item_type BETWEEN 711181 AND 711187 OR item_type=728120),
stack_before integer NOT NULL CHECK(stack_before BETWEEN 1 AND 65535),amount integer NOT NULL CHECK(amount BETWEEN 1 AND 8),
bound boolean NOT NULL,PRIMARY KEY(craft,item_uid),CHECK(amount<=stack_before));");}
        }
        private static bool Near(GameState p) {return Q.Near(p,Guardian,2056) && p.Map.ID==2056 && p.Map.BaseID==2056;}
        private static bool Usable(ConquerItem i) {
            return i!=null && i.Position==0 && i.Warehouse==0 && i.StackSize>0 && i.Lock==0 && !i.Suspicious && !i.InWardrobe
                && (i.Days==0 || Clock().ToLocalTime()<i.DayStamp.AddDays(i.Days));
        }
        private static List<Take> Select(GameState p,int recipe) {
            var selected=new List<Take>();
            foreach(uint type in recipe==1?new uint[]{711181,711182,711183,711184,711185,711186,711187}:new[]{Fragment}) {
                int needed=recipe==1?1:8;
                foreach(var item in p.Inventory.Objects.Where(i=>i.ID==type && Usable(i)).OrderByDescending(i=>i.Bound).ThenBy(i=>i.UID)) {
                    ushort amount=(ushort)Math.Min(needed,item.StackSize);
                    selected.Add(new Take{Item=item,Uid=item.UID,Type=item.ID,Before=item.StackSize,Amount=amount,Bound=item.Bound,Days=item.Days,Stamp=item.DayStamp});
                    needed-=amount;if(needed==0)break;
                }
                if(needed!=0)return null;
            }
            return selected;
        }
        private static bool Same(GameState p,Take take) {
            ConquerItem item;return p.Inventory.TryGetItem(take.Uid,out item) && ReferenceEquals(item,take.Item) && Usable(item)
                && item.ID==take.Type && item.StackSize==take.Before && item.Bound==take.Bound && item.Days==take.Days && item.DayStamp==take.Stamp;
        }
        private static void Prepare(GameState p,int recipe,Npcs dialog) {
            offers.Remove(p);var materials=Select(p,recipe);
            if(materials==null){dialog.Text(recipe==1?"Bring one each of the Ice, Stone, Point, Ray, Grey, Blood and Bloom Pills.":"Bring eight Dragon Pill Fragments.");return;}
            if(p.Inventory.Count-materials.Count(m=>m.Amount==m.Before)>=40){dialog.Text("Make room in your bag for the Dragon Pill.");return;}
            offers.Add(p,new Offer{Id=Guid.NewGuid(),Recipe=recipe,Until=Clock().AddMinutes(2),Materials=materials});
            dialog.Text(recipe==1?"I will combine these seven ghoul pills into one Dragon Pill.":"I will combine eight fragments into one Dragon Pill.");
            if(materials.Any(m=>m.Bound))dialog.Text("Your bound material will make the Dragon Pill bound.");
            dialog.Option("Forge it.",60);dialog.Option("Not yet.",255);
        }
        private static void Craft(GameState p,Npcs dialog) {
            Offer offer;if(!offers.TryGetValue(p,out offer)){dialog.Text("Show me the materials you want to combine.");return;}
            offers.Remove(p);
            if(offer.Until<=Clock() || !offer.Materials.All(m=>Same(p,m))){dialog.Text("Show me your materials again.");return;}
            if(p.Inventory.Count-offer.Materials.Count(m=>m.Amount==m.Before)>=40){dialog.Text("Make room in your bag for the Dragon Pill.");return;}
            var result=Q.New(DragonPill,offer.Materials.Any(m=>m.Bound));
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                using(var q=Q.Command(c,t,"SELECT mapid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                    if(Convert.ToUInt32(q.ExecuteScalar())!=2056)throw new InvalidOperationException("Dragon Pill owner location changed.");
                using(var q=Q.Command(c,t,"SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0","uid",(long)p.Entity.UID))
                    if(Convert.ToInt64(q.ExecuteScalar())!=p.Inventory.Count)throw new InvalidOperationException("Dragon Pill inventory requires reload.");
                foreach(var take in offer.Materials) {
                    string sql=take.Before==take.Amount?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-@amount";
                    if(Q.Execute(c,t,sql+@" WHERE uid=@item AND id=@type AND entityid=@uid AND position=0 AND warehouse=0
AND stacksize=@before AND bound=@bound AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0
AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)","item",(long)take.Uid,"type",(int)take.Type,
                        "uid",(long)p.Entity.UID,"before",(int)take.Before,"amount",(int)take.Amount,"bound",take.Bound?1:0,
                        "days",take.Days.ToString(CultureInfo.InvariantCulture),"permanent",take.Days==0,"stamp",take.Stamp.ToBinary())!=1)
                        throw new InvalidOperationException("Dragon Pill material ownership changed.");
                }
                if(Q.Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability)
VALUES(@item,720598,@uid,0,0,1,@bound,@dur,@max)","item",(long)result.UID,"uid",(long)p.Entity.UID,"bound",result.Bound?1:0,
                    "dur",(int)result.Durability,"max",(int)result.MaximDurability)!=1)throw new InvalidOperationException("Dragon Pill was not saved.");
                if(Q.Execute(c,t,"INSERT INTO revora_dragon_pill_crafts(id,uid,recipe,item_uid,bound) VALUES(@id,@uid,@recipe,@item,@bound)",
                    "id",offer.Id,"uid",(long)p.Entity.UID,"recipe",offer.Recipe,"item",(long)result.UID,"bound",result.Bound)!=1)
                    throw new InvalidOperationException("Dragon Pill craft was not recorded.");
                foreach(var take in offer.Materials)
                    if(Q.Execute(c,t,@"INSERT INTO revora_dragon_pill_materials(craft,item_uid,item_type,stack_before,amount,bound)
VALUES(@craft,@item,@type,@before,@amount,@bound)","craft",offer.Id,"item",(long)take.Uid,"type",(int)take.Type,
                        "before",(int)take.Before,"amount",(int)take.Amount,"bound",take.Bound)!=1)throw new InvalidOperationException("Dragon Pill material receipt was not saved.");
                t.Commit();
            }}
            Q.Install(p,offer.Materials.Where(m=>m.Before==m.Amount).Select(m=>m.Uid),new ConquerItem[0]);
            foreach(var take in offer.Materials.Where(m=>m.Before>m.Amount)){take.Item.StackSize=(ushort)(take.Before-take.Amount);take.Item.Mode=Enums.ItemMode.Update;take.Item.Send(p);}
            Q.Install(p,new uint[0],new[]{result});dialog.Text("Your Dragon Pill is ready.");
        }
        internal static bool HandleNpc(GameState p,NpcRequest request) {
            if(p==null || request==null || p.ActiveNpc!=Guardian)return false;
            if(p.ItemSyncRoot==null)return true;
            lock(p.ItemSyncRoot)try {
                if((request.NpcID!=0 && request.NpcID!=Guardian) || !Near(p))return true;
                var dialog=new Npcs(p);dialog.Avatar(82);
                if(request.OptionID==0){offers.Remove(p);dialog.Text("Bring seven different ghoul pills, or eight Dragon Pill Fragments. I can forge a Dragon Pill.");
                    dialog.Option("Hunt a ghoul.",20);dialog.Option("My hunt.",21);dialog.Option("Combine seven ghoul pills.",6);dialog.Option("Combine eight fragments.",17);dialog.Option("Take me to Desert City.",15);dialog.Option("Farewell.",255);}
                else if(DragonSlayerHunt.Menu(p,request.OptionID,dialog)) { }
                else if(request.OptionID==6 || request.OptionID==17)Prepare(p,request.OptionID==6?1:2,dialog);
                else if(request.OptionID==60)Craft(p,dialog);
                else if(request.OptionID==15){offers.Remove(p);dialog.Text("Leave the grotto for Desert City?");dialog.Option("Take me back.",16);dialog.Option("I will stay.",255);}
                else if(request.OptionID==16){offers.Remove(p);Map map;MapsTable.MapInformation info;ushort x=482,y=633;
                    if(Kernel.Maps.TryGetValue(1000,out map) && map.ID==1000 && map.BaseID==1000 && map.Floor!=null
                        && MapsTable.MapInformations.TryGetValue(1000,out info) && info.ID==1000 && info.BaseID==1000 && info.Owner==0
                        && PotencyGuide.FindLanding(map,ref x,ref y))p.Entity.Teleport(1000,x,y);
                    else p.Send("The road to Desert City is closed for now.");return true;}
                else {offers.Remove(p);return true;}
                dialog.Send();
            }catch(Exception ex){Q.Fail(p,"dragon-pill-craft",ex);}
            return true;
        }
    }
}
