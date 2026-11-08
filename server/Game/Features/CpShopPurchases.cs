#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class CpShopPurchases
    {
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;
        internal static Action? BeforeCommit = null;
        internal static Action? AfterCommit = null;
        private static bool enabled;
        private sealed class StackChange { internal ConquerItem Item = null!; internal ushort Before, After; }
        internal static void Initialize()
        {
            using (var c = DataHolder.MySqlConnection) {
                c.Open(); Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_cp_shop_purchases(
id uuid PRIMARY KEY,uid bigint NOT NULL,shop bigint NOT NULL,item_type integer NOT NULL,
quantity integer NOT NULL CHECK(quantity BETWEEN 1 AND 65535),bound boolean NOT NULL,
cost bigint NOT NULL CHECK(cost>0),balance_before bigint NOT NULL,balance_after bigint NOT NULL CHECK(balance_after>=0),
item_uids bigint[] NOT NULL,purchased_at timestamptz NOT NULL,CHECK(balance_before-balance_after=cost));
CREATE INDEX IF NOT EXISTS revora_cp_shop_recent ON revora_cp_shop_purchases(uid,shop,item_type,quantity,bound,purchased_at);");
            }
            enabled=true;
        }
        private static ShopFile.Shop? Shop(uint id)
        {
            ShopFile.Shop shop;
            if (ShopFile.Shops!=null && ShopFile.Shops.TryGetValue(id,out shop)) return shop;
            return EShopFile.Shops!=null && EShopFile.Shops.TryGetValue(id,out shop)?shop:null;
        }
        private static bool Ready(GameState p) => enabled && Q.Player(p) && p.Map!=null && p.Entity.MapID==p.Map.ID
            && p.Booth==null && p.Trade!=null && !p.Trade.InTrade && p.Entity.PokerTable==0;
        private static bool Near(GameState p,uint shop)
        {
            Revora.Interfaces.INpc npc;
            // Garment merchant 6002 opens the existing client shop window 502.
            uint npcId = shop == 502 ? 6002u : shop;
            return shop==2888 || p.Map.Npcs!=null && p.Map.Npcs.TryGetValue(npcId,out npc) && npc.MapID==p.Entity.MapID
                && Kernel.GetDistance(p.Entity.X,p.Entity.Y,npc.X,npc.Y)<=17;
        }
        private static bool Stackable(ConquerItem item,uint type,bool bound,byte plus,ushort max) => item.ID==type
            && item.Bound==bound && item.StackSize>0 && item.StackSize<max && item.Plus==plus && item.Bless==0
            && item.Enchant==0 && item.SocketOne==0 && item.SocketTwo==0 && item.Position==0 && item.Warehouse==0
            && item.Lock==0 && !item.Suspicious && !item.InWardrobe && item.Days==0 && item.TimeLeftInMinutes==0;
        internal static bool TryBuy(GameState p,ItemUsage request)
        {
            if (request==null) return false;
            var frame=request.ToArray();
            if (frame==null || frame.Length<38 || frame.Length>128 || BitConverter.ToUInt16(frame,0)+8!=frame.Length
                || BitConverter.ToUInt16(frame,2)!=1009 || BitConverter.ToUInt64(frame,frame.Length-8)!=Revora.Network.PacketHandler.ClientSeal) return true;
            var shop=Shop(request.UID);
            bool boundOnly=shop!=null && shop.MoneyType==ShopFile.MoneyType.BoundCps;
            if (shop==null || !boundOnly && shop.MoneyType!=ShopFile.MoneyType.ConquerPoints) return false;
            ushort action=BitConverter.ToUInt16(frame,20);
            if ((action!=1 && action!=55) || request.dwExtraInfo3>2 || !Ready(p)) return true;
            if (!SessionInstall.EnterCurrent(p.Entity.UID,p)) return true;
            try { lock(p.ItemSyncRoot) {
                ConquerItemBaseInformation info;
                uint type=request.dwParam,amount=request.dwExtraInfo==0?1:request.dwExtraInfo;
                bool bound=boundOnly || request.dwExtraInfo3==2;
                if (!Ready(p) || !Near(p,request.UID) || shop.UID!=request.UID || shop.Items==null || !shop.Items.Contains(type)
                    || amount>65535 || !ConquerItemInformation.BaseInformations.TryGetValue(type,out info) || info.ConquerPointsWorth==0) return true;
                ulong cost=(ulong)info.ConquerPointsWorth*amount;
                uint before=bound?p.Entity.BoundCps:p.Entity.ConquerPoints;
                if (cost>before) return true;
                uint after=before-(uint)cost;
                ushort max=(ushort)Math.Max(1,(int)info.StackSize);
                byte plus=type>=730001 && type<=730012?(byte)(type-730000): (byte)0;
                uint remaining=amount;
                var changes=new List<StackChange>();
                foreach(var item in p.Inventory.Objects.Where(i=>Stackable(i,type,bound,plus,max)).OrderBy(i=>i.UID)) {
                    uint take=Math.Min(remaining,(uint)(max-item.StackSize));
                    if(take==0) break;
                    changes.Add(new StackChange{Item=item,Before=item.StackSize,After=(ushort)(item.StackSize+take)});remaining-=take;
                }
                if ((ulong)p.Inventory.Count+(remaining+max-1)/max>40) {p.Send("Make room for your purchase.");return true;}
                var added=new List<ConquerItem>();
                while(remaining>0) {
                    var item=Q.New(type,bound);item.Plus=plus;item.Color=Enums.Color.Orange;item.MaxStackSize=max;
                    item.StackSize=(ushort)Math.Min(remaining,max);remaining-=item.StackSize;added.Add(item);
                }
                DateTime now=Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Purchase clock requires UTC.");
                using(var c=DataHolder.MySqlConnection) { c.Open();using(var t=c.BeginTransaction()) {
                    SkillTable.LockOwner(c,t,p);
                    using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_cp_shop_purchases
WHERE uid=@uid AND shop=@shop AND item_type=@type AND quantity=@amount AND bound=@bound AND purchased_at>@now-interval '1 second')",
                        "uid",(long)p.Entity.UID,"shop",(long)request.UID,"type",(int)type,"amount",(int)amount,"bound",bound,"now",now))
                        if(Equals(q.ExecuteScalar(),true))return true;
                    var bag=new List<uint>();
                    using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader())while(r.Read()){if(bag.Count>=40)throw new InvalidOperationException("Purchase bag exceeds capacity.");bag.Add(checked((uint)r.GetInt64(0)));}
                    if(!bag.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Purchase inventory changed.");
                    foreach(var change in changes)
                        if(Q.Execute(c,t,@"UPDATE items SET stacksize=@after WHERE uid=@item AND entityid=@uid AND id=@type
AND stacksize=@before AND bound=@bound AND plus=@plus AND bless=0 AND enchant=0 AND socketone=0 AND sockettwo=0
AND position=0 AND warehouse=0 AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0'",
                            "after",(int)change.After,"item",(long)change.Item.UID,"uid",(long)p.Entity.UID,"type",(int)type,
                            "before",(int)change.Before,"bound",bound?1:0,"plus",(int)plus)!=1)throw new InvalidOperationException("Purchase stack changed.");
                    foreach(var item in added)
                        if(Q.Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,plus,durability,maximdurability,color)
VALUES(@item,@type,@uid,0,0,@stack,@bound,@plus,@dur,@dur,3)","item",(long)item.UID,"type",(int)type,"uid",(long)p.Entity.UID,
                            "stack",(int)item.StackSize,"bound",bound?1:0,"plus",(int)plus,"dur",(int)item.Durability)!=1)throw new InvalidOperationException("Purchase insert failed.");
                    string debit=bound?"UPDATE entities SET boundcps=@after WHERE uid=@uid AND COALESCE(boundcps,0)=@before"
                        :"UPDATE entities SET conquerpoints=@after WHERE uid=@uid AND COALESCE(conquerpoints,0)=@before";
                    if(Q.Execute(c,t,debit,"after",(long)after,"uid",(long)p.Entity.UID,"before",(long)before)!=1)throw new InvalidOperationException("Purchase balance changed.");
                    if(Q.Execute(c,t,@"INSERT INTO revora_cp_shop_purchases(id,uid,shop,item_type,quantity,bound,cost,balance_before,balance_after,item_uids,purchased_at)
VALUES(@id,@uid,@shop,@type,@amount,@bound,@cost,@before,@after,@items,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"shop",(long)request.UID,
                        "type",(int)type,"amount",(int)amount,"bound",bound,"cost",(long)cost,"before",(long)before,"after",(long)after,
                        "items",changes.Select(x=>(long)x.Item.UID).Concat(added.Select(x=>(long)x.UID)).ToArray(),"now",now)!=1)throw new InvalidOperationException("Purchase receipt failed.");
                    BeforeCommit?.Invoke();
                    if(!Ready(p) || !Near(p,request.UID) || !ReferenceEquals(Shop(request.UID),shop) || !shop.Items.Contains(type)
                        || !SessionInstall.IsCurrent(p.Entity.UID,p))throw new InvalidOperationException("Purchase context changed.");
                    t.Commit();AfterCommit?.Invoke();
                }}
                if(bound)p.Entity.ApplyCommittedBoundCps(after);else p.Entity.ApplyCommittedConquerPoints(after);
                Q.Install(p,new uint[0],added);
                foreach(var change in changes){change.Item.StackSize=change.After;change.Item.Mode=Enums.ItemMode.Update;change.Item.Send(p);change.Item.Mode=Enums.ItemMode.Default;}
            }}catch(Exception ex){Q.Fail(p,"cp-shop-purchase",ex);}finally{SessionInstall.Exit(p.Entity.UID);}
            return true;
        }
    }
}
