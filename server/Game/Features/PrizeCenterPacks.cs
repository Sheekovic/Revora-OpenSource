using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Ordinary Prize Center coin-reward consumables from MammonEnvoy 9623.
    internal static class PrizeCenterPacks
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static Func<int,int,int> Roll=(min,max)=>Revora.Kernel.Random.Next(min,max);
        internal static Action<string> Spoken;
        private static bool enabled;
        private static readonly uint[] Accessories=new uint[]{
            350001,350002,350004,350005,350006,350007,350008,350009,350010,350011,
            350012,350014,350015,350016,350017,350018,350019,350020,
            360001,360002,360003,360004,360005,360006,360007,360008,360009,360010,
            360011,360012,360013,360014,360015,360016,360017,360018,360019,360020,
            360021,360022,360023,360024,360025,360026,360027,360028,360029,360030,
            360031,360032,360033,360034,360035,360036,360037,360038,360039,360040,
            370001,370002,370003,370004,370005,370011,370012,370013,370014,370015,
            380001,380002,380003,380004,380005,380011,380012,380013,380014};
        private static readonly uint[] Scraps=new uint[]{350081,350082,360151,360152,360153};
        private static bool Handles(uint id){return id==720836 || id==728898 || id==728899 || id==728900 || id==3004257;}
        internal static void Initialize(){
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_prize_center_pack_uses(
id uuid PRIMARY KEY,uid bigint NOT NULL CHECK(uid>0),item_uid bigint NOT NULL,
item_type integer NOT NULL CHECK(item_type IN(720836,728898,728899,728900,3004257)),
before_stack integer NOT NULL CHECK(before_stack>=0),consumed integer NOT NULL CHECK(consumed>0),
bound boolean NOT NULL,choice integer NOT NULL CHECK(choice>=0),rvp integer NOT NULL CHECK(rvp>=0),
rewards bigint[] NOT NULL,used_at timestamptz NOT NULL);");}enabled=true;
        }
        private static bool Ready(GameState p){return enabled && Q.Player(p) && p.Booth==null && p.Trade!=null && !p.Trade.InTrade;}
        private static void Hear(GameState p,string text){p.Send(text);var s=Spoken;if(s!=null)s(text);}
        private static void Need(GameState p,string text){p.MessageBox(text,null,null,0);var s=Spoken;if(s!=null)s(text);}
        private static void Full(GameState p){p.Send(Constants.FullInventory);var s=Spoken;if(s!=null)s("Your inventory is Full.");}
        private static int Cost(uint id){return id==728899?6:id==728898||id==728900?8:1;}
        private static uint Gift(uint id,int choice){
            if(id==728898)return 723714;if(id==728900)return 723713;
            if(id==720836)return choice>=1 && choice<=Accessories.Length?Accessories[choice-1]:0;
            if(id==3004257)return choice>=1 && choice<=Scraps.Length?Scraps[choice-1]:0;
            return 0;
        }
        private static ConquerItem Item(uint id){
            ushort dur=1;
            if(ConquerItemInformation.BaseInformations.ContainsKey(id))dur=ConquerItemInformation.BaseInformations[id].Durability;
            return new ConquerItem(true){ID=id,UID=ConquerItem.ItemUID.Next,Plus=0,Bound=false,StackSize=1,Durability=dur,MaximDurability=dur};
        }
        internal static bool TryHandle(GameState p,byte[] packet){
            ConquerItem item;
            if(p==null || p.Inventory==null || packet==null || packet.Length<12 || !p.Inventory.TryGetItem(BitConverter.ToUInt32(packet,8),out item) || !Handles(item.ID))return false;
            if(packet.Length<32 || packet.Length>128 || BitConverter.ToUInt16(packet,0)+8!=packet.Length || BitConverter.ToUInt16(packet,2)!=1009
                || BitConverter.ToUInt16(packet,20)!=4 || BitConverter.ToUInt64(packet,packet.Length-8)!=Revora.Network.PacketHandler.ClientSeal)return true;
            TryUse(item,p);return true;
        }
        internal static bool TryUse(ConquerItem item,GameState p){
            if(item==null || !Handles(item.ID))return false;if(!Ready(p))return true;
            lock(p.ItemSyncRoot)try{
                ConquerItem owned;DateTime now=Clock();
                if(!Ready(p) || !p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(item,owned) || item.Position!=0 || item.Warehouse!=0
                    || item.Lock!=0 || item.Suspicious || item.InWardrobe || (item.Days==0?item.TimeLeftInMinutes!=0:now.ToLocalTime()>=item.DayStamp.AddDays(item.Days)))return true;
                int cost=Cost(item.ID);
                if(cost>1 && item.StackSize<cost){
                    if(item.ID==728898)Need(p,"You~need~to~have~8~Tiny~Class2~Money~Pouches~to~combine~and~get~a~Class2~Money~Bag.");
                    else if(item.ID==728900)Need(p,"You~need~to~have~8~Tiny~Class1~Money~Pouches~to~combine~and~get~a~Class1~Money~Bag.");
                    else Need(p,"You~need~to~have~6~Tiny~Horse~Racing~Points~Bags~to~combine~and~get~a~Horse~Racing~Points~Pack(3K).");
                    return true;
                }
                if(item.ID==3004257 && p.Inventory.Count>38){Hear(p,"You need atleast 2 open spots!");return true;}
                int choice=0;uint gift=0;int rvp=item.ID==728899?3000:0;
                if(item.ID==720836){choice=Roll(1,78);gift=Gift(item.ID,choice);}
                else if(item.ID==3004257){choice=Roll(1,6);gift=Gift(item.ID,choice);}
                else gift=Gift(item.ID,0);
                if(item.ID==720836 || item.ID==3004257){
                    if(gift==0 || !ConquerItemInformation.BaseInformations.ContainsKey(gift))return true;
                }
                if(rvp>0){
                    long next=(long)p.Entity.RevoraPoint+rvp;
                    if(next>uint.MaxValue){Hear(p,"Spend some Revora Points before combining these bags.");return true;}
                }
                bool deplete=cost>1?item.StackSize==cost:item.StackSize<=1;
                int spaces=gift==0?0:(deplete?0:1);
                if(item.ID==720836 && p.Inventory.Count+spaces>40){Full(p);return true;}
                if((item.ID==728898 || item.ID==728900) && p.Inventory.Count+spaces>40){Full(p);return true;}
                ConquerItem reward=gift==0?null:Item(gift);
                if(item.Bound && reward!=null) reward.Bound=true;
                long nextRvp=rvp==0?p.Entity.RevoraPoint:(long)p.Entity.RevoraPoint+rvp;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("Prize Center pack owner missing.");
                    var ids=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader())while(r.Read()){if(ids.Count>=40)throw new InvalidOperationException("Prize Center pack inventory exceeds capacity.");ids.Add(checked((uint)Convert.ToInt64(r[0])));}
                    if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Prize Center pack inventory changed.");
                    string sql=deplete?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-"+cost.ToString(CultureInfo.InvariantCulture);
                    if(Q.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=@type AND warehouse=0 AND position=0 AND stacksize=@stack AND bound=@bound
AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                        "item",(long)item.UID,"uid",(long)p.Entity.UID,"type",(int)item.ID,"stack",(int)item.StackSize,"bound",item.Bound?1:0,
                        "days",item.Days.ToString(CultureInfo.InvariantCulture),"permanent",item.Days==0,"stamp",item.DayStamp.ToBinary())!=1)
                        throw new InvalidOperationException("Prize Center pack ownership changed.");
                    if(reward!=null
                        && Q.Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,plus,durability,maximdurability)
VALUES(@item,@type,@uid,0,0,1,@bound,@plus,@dur,@max)","item",(long)reward.UID,"type",(int)reward.ID,"uid",(long)p.Entity.UID,
                            "bound",reward.Bound?1:0,"plus",(int)reward.Plus,"dur",(int)reward.Durability,"max",(int)reward.MaximDurability)!=1)
                        throw new InvalidOperationException("Prize Center pack item was not saved.");
                    if(rvp>0 && Q.Execute(c,t,@"UPDATE entities SET revorapoint=@rvp WHERE uid=@uid AND COALESCE(revorapoint,0)=@old",
                        "rvp",nextRvp,"uid",(long)p.Entity.UID,"old",(long)p.Entity.RevoraPoint)!=1)
                        throw new InvalidOperationException("Prize Center Revora Point balance changed.");
                    if(Q.Execute(c,t,@"INSERT INTO revora_prize_center_pack_uses(id,uid,item_uid,item_type,before_stack,consumed,bound,choice,rvp,rewards,used_at)
VALUES(@id,@uid,@item,@type,@stack,@consumed,@bound,@choice,@rvp,@rewards,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"item",(long)item.UID,
                        "type",(int)item.ID,"stack",(int)item.StackSize,"consumed",cost,"bound",item.Bound,"choice",choice,"rvp",rvp,
                        "rewards",reward==null?new long[0]:new[]{(long)reward.UID},"now",now)!=1)
                        throw new InvalidOperationException("Prize Center pack receipt was not saved.");
                    if(!Ready(p))throw new InvalidOperationException("Prize Center pack session changed.");t.Commit();
                }}
                Q.Install(p,deplete?new[]{item.UID}:new uint[0],reward==null?new ConquerItem[0]:new[]{reward});
                if(!deplete){item.StackSize=(ushort)(item.StackSize-cost);item.Mode=Enums.ItemMode.Update;item.Send(p);item.Mode=Enums.ItemMode.Default;}
                if(rvp>0)p.Entity.ApplyCommittedRevoraPoints((uint)nextRvp);
                if(item.ID==728898)Hear(p,"You have recieved Class2MoneyBag");
                else if(item.ID==728900)Hear(p,"You have recieved Class1MoneyBag");
                else if(item.ID==728899)Hear(p,"You have received 3,000 Revora Points.");
            }catch(Exception ex){Q.Fail(p,"prize-center-pack",ex);}return true;
        }
    }
}
