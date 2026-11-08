using System;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Interfaces;
using Revora.Network;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class TravelScrollShop
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static void Initialize()
        {
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_scroll_purchases(
id uuid PRIMARY KEY,uid bigint NOT NULL,shop bigint NOT NULL,item_type integer NOT NULL CHECK(item_type BETWEEN 1060020 AND 1060024),
quantity integer NOT NULL CHECK(quantity BETWEEN 1 AND 40),unit_price bigint NOT NULL CHECK(unit_price>0),
cost bigint NOT NULL CHECK(cost=quantity*unit_price),silver_before bigint NOT NULL CHECK(silver_before>=cost),
silver_after bigint NOT NULL CHECK(silver_after=silver_before-cost),item_uid bigint NOT NULL,
stack_before integer NOT NULL CHECK(stack_before BETWEEN 0 AND 65535),stack_after integer NOT NULL CHECK(stack_after BETWEEN 1 AND 65535),
purchased_at timestamptz NOT NULL);
CREATE INDEX IF NOT EXISTS revora_scroll_recent ON revora_scroll_purchases(uid,shop,item_type,quantity,purchased_at DESC);");}
        }
        private static bool Ready(GameState p){return Q.Player(p) && p.Map!=null && p.Map.ID==p.Entity.MapID && p.Trade!=null && !p.Trade.InTrade && p.Booth==null && p.Entity.PokerTable==0;}
        private static bool Stock(GameState p,ItemUsage request,out ConquerItemBaseInformation info)
        {
            info=null;INpc npc;ShopFile.Shop shop;
            return Ready(p) && p.Map.Npcs.TryGetValue(request.UID,out npc) && npc.MapID==p.Entity.MapID
                && Kernel.GetDistance(p.Entity.X,p.Entity.Y,npc.X,npc.Y)<=17
                && ShopFile.Shops!=null && ShopFile.Shops.TryGetValue(request.UID,out shop) && shop.UID==request.UID
                && shop.MoneyType==ShopFile.MoneyType.Gold && shop.Items.Contains(request.dwParam)
                && ConquerItemInformation.BaseInformations.TryGetValue(request.dwParam,out info) && info.GoldWorth>0 && info.StackSize>0;
        }
        private static bool Stackable(ConquerItem i,uint type)
        {return i.ID==type && i.Position==0 && i.Warehouse==0 && !i.Bound && i.Lock==0 && !i.Suspicious && !i.InWardrobe && i.Days==0 && i.TimeLeftInMinutes==0;}
        internal static bool TryBuy(GameState p,ItemUsage request)
        {
            if(request==null)return false;byte[] frame=request.ToArray();
            if(frame==null || frame.Length<16)return true;
            uint type=BitConverter.ToUInt32(frame,12);if(type<1060020 || type>1060024)return false;
            if(frame.Length<38 || frame.Length>128 || BitConverter.ToUInt16(frame,0)!=frame.Length-8
                || BitConverter.ToUInt16(frame,2)!=1009 || BitConverter.ToUInt64(frame,frame.Length-8)!=PacketHandler.ClientSeal)return true;
            ushort action=BitConverter.ToUInt16(frame,20);if(action!=1 && action!=55 || !Ready(p))return true;
            ShopFile.Shop route;
            if(ShopFile.Shops!=null && ShopFile.Shops.TryGetValue(request.UID,out route) && route.MoneyType!=ShopFile.MoneyType.Gold)return false;
            if((ShopFile.Shops==null || !ShopFile.Shops.ContainsKey(request.UID)) && EShopFile.Shops!=null && EShopFile.Shops.TryGetValue(request.UID,out route))return false;
            uint quantity=request.dwExtraInfo==0?1u:request.dwExtraInfo;if(quantity>40)return true;
            lock(p.ItemSyncRoot)try {
                ConquerItemBaseInformation info;if(!Stock(p,request,out info) || quantity>info.StackSize)return true;
                ulong before=p.Entity.Money,cost=checked((ulong)info.GoldWorth*quantity);if(before<cost)return true;
                var existing=p.Inventory.Objects.Where(i=>Stackable(i,type) && Math.Max(1,(int)i.StackSize)+quantity<=info.StackSize).OrderBy(i=>i.UID).FirstOrDefault();
                if(existing==null && p.Inventory.Count>=40){p.Send(Constants.FullInventory);return true;}
                ushort old=existing==null?(ushort)0:existing.StackSize;
                ushort after=checked((ushort)((existing==null?0:Math.Max(1,(int)old))+quantity));
                var result=existing??Q.New(type,false);DateTime now=Clock();
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    SkillTable.LockOwner(c,t,p);
                    using(var q=Q.Command(c,t,"SELECT mapid FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))
                        if(Convert.ToUInt16(q.ExecuteScalar())!=p.Entity.MapID)throw new InvalidOperationException("Scroll buyer location changed.");
                    using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_scroll_purchases WHERE uid=@uid AND shop=@shop
AND item_type=@type AND quantity=@amount AND purchased_at>@now-interval '1 second')","uid",(long)p.Entity.UID,"shop",(long)request.UID,"type",(int)type,"amount",(int)quantity,"now",now))
                        if(Equals(q.ExecuteScalar(),true))return true;
                    var saved=new System.Collections.Generic.List<uint>();
                    using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader())while(r.Read()){if(saved.Count>=40)throw new InvalidOperationException("Scroll buyer bag exceeds capacity.");saved.Add(checked((uint)Convert.ToInt64(r[0])));}
                    if(!saved.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Scroll buyer inventory changed.");
                    SilverBalance.Persist(c,t,p,before-cost);
                    int changed=existing==null?Q.Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability)
VALUES(@item,@type,@uid,0,0,@stack,0,@dur,@dur)","item",(long)result.UID,"type",(int)type,"uid",(long)p.Entity.UID,"stack",(int)after,"dur",(int)info.Durability)
                        :Q.Execute(c,t,@"UPDATE items SET stacksize=@after WHERE uid=@item AND entityid=@uid AND id=@type AND stacksize=@before
AND position=0 AND warehouse=0 AND bound=0 AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0'",
                            "after",(int)after,"item",(long)existing.UID,"uid",(long)p.Entity.UID,"type",(int)type,"before",(int)old);
                    if(changed!=1)throw new InvalidOperationException("Scroll stock was not saved.");
                    if(Q.Execute(c,t,@"INSERT INTO revora_scroll_purchases(id,uid,shop,item_type,quantity,unit_price,cost,silver_before,silver_after,item_uid,stack_before,stack_after,purchased_at)
VALUES(@id,@uid,@shop,@type,@amount,@price,@cost,@before,@after,@item,@stack,@newstack,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"shop",(long)request.UID,
                        "type",(int)type,"amount",(int)quantity,"price",(long)info.GoldWorth,"cost",checked((long)cost),"before",checked((long)before),"after",checked((long)(before-cost)),
                        "item",(long)result.UID,"stack",(int)old,"newstack",(int)after,"now",now)!=1)throw new InvalidOperationException("Scroll purchase was not recorded.");
                    ConquerItemBaseInformation current;
                    if(!Stock(p,request,out current) || !ReferenceEquals(current,info) || p.Entity.Money!=before
                        || existing!=null && (!p.Inventory.Objects.Any(i=>ReferenceEquals(i,existing)) || !Stackable(existing,type) || existing.StackSize!=old))
                        throw new InvalidOperationException("Scroll buyer changed.");
                    t.Commit();
                }}
                SilverBalance.ApplyCommitted(p,before-cost);result.StackSize=after;result.MaxStackSize=info.StackSize;
                if(existing==null)Q.Install(p,new uint[0],new[]{result});else {result.Mode=Enums.ItemMode.Update;result.Send(p);}
            }catch(Exception ex){Q.Fail(p,"travel-scroll-shop",ex);}return true;
        }
    }
}
