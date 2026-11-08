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
    // Ordinary leftover DragonBall packs: 720028 ten balls, 720884 five balls.
    // 720028 copies pack Bound. 720884 stays unbound; its producer is not this clerk.
    internal static class DragonBallPacks
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static Action<string> Spoken;
        private static bool enabled;
        private const uint Ball=1088000;
        private static bool Handles(uint id){return id==720028 || id==720884;}
        internal static void Initialize(){
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_dragon_ball_pack_uses(
id uuid PRIMARY KEY,uid bigint NOT NULL CHECK(uid>0),item_uid bigint NOT NULL,
item_type integer NOT NULL CHECK(item_type IN(720028,720884)),
before_stack integer NOT NULL CHECK(before_stack>=0),bound boolean NOT NULL,
rewards bigint[] NOT NULL,used_at timestamptz NOT NULL);");}enabled=true;
        }
        private static bool Ready(GameState p){return enabled && Q.Player(p) && p.Booth==null && p.Trade!=null && !p.Trade.InTrade;}
        private static void Full(GameState p){p.Send(Constants.FullInventory);var s=Spoken;if(s!=null)s("Your inventory is Full.");}
        private static int Count(uint id){return id==720028?10:5;}
        private static ConquerItem BallItem(bool bound){
            ushort dur=1;
            if(ConquerItemInformation.BaseInformations.ContainsKey(Ball))dur=ConquerItemInformation.BaseInformations[Ball].Durability;
            return new ConquerItem(true){ID=Ball,UID=ConquerItem.ItemUID.Next,Plus=0,Bound=bound,StackSize=1,Durability=dur,MaximDurability=dur};
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
                int n=Count(item.ID);
                if(item.ID==720028 && p.Inventory.Count>31){Full(p);return true;}
                if(item.ID==720884 && p.Inventory.Count>30){Full(p);return true;}
                int spaces=n-(item.StackSize<=1?1:0);
                if(item.ID==720028 && p.Inventory.Count+spaces>40){Full(p);return true;}
                var rewards=new ConquerItem[n];
                bool bind=item.ID==720028 && item.Bound;
                for(int i=0;i<n;i++)rewards[i]=BallItem(bind);
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("DragonBall pack owner missing.");
                    var ids=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader())while(r.Read()){if(ids.Count>=40)throw new InvalidOperationException("DragonBall pack inventory exceeds capacity.");ids.Add(checked((uint)Convert.ToInt64(r[0])));}
                    if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("DragonBall pack inventory changed.");
                    string sql=item.StackSize<=1?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
                    if(Q.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=@type AND warehouse=0 AND position=0 AND stacksize=@stack AND bound=@bound
AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                        "item",(long)item.UID,"uid",(long)p.Entity.UID,"type",(int)item.ID,"stack",(int)item.StackSize,"bound",item.Bound?1:0,
                        "days",item.Days.ToString(CultureInfo.InvariantCulture),"permanent",item.Days==0,"stamp",item.DayStamp.ToBinary())!=1)
                        throw new InvalidOperationException("DragonBall pack ownership changed.");
                    foreach(var reward in rewards)
                        if(Q.Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,plus,durability,maximdurability)
VALUES(@item,@type,@uid,0,0,1,@bound,@plus,@dur,@max)","item",(long)reward.UID,"type",(int)reward.ID,"uid",(long)p.Entity.UID,
                            "bound",reward.Bound?1:0,"plus",(int)reward.Plus,"dur",(int)reward.Durability,"max",(int)reward.MaximDurability)!=1)
                            throw new InvalidOperationException("DragonBall pack item was not saved.");
                    if(Q.Execute(c,t,@"INSERT INTO revora_dragon_ball_pack_uses(id,uid,item_uid,item_type,before_stack,bound,rewards,used_at)
VALUES(@id,@uid,@item,@type,@stack,@bound,@rewards,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"item",(long)item.UID,
                        "type",(int)item.ID,"stack",(int)item.StackSize,"bound",item.Bound,"rewards",rewards.Select(r=>(long)r.UID).ToArray(),"now",now)!=1)
                        throw new InvalidOperationException("DragonBall pack receipt was not saved.");
                    if(!Ready(p))throw new InvalidOperationException("DragonBall pack session changed.");t.Commit();
                }}
                Q.Install(p,item.StackSize<=1?new[]{item.UID}:new uint[0],rewards);
                if(item.StackSize>1){item.StackSize--;item.Mode=Enums.ItemMode.Update;item.Send(p);item.Mode=Enums.ItemMode.Default;}
            }catch(Exception ex){Q.Fail(p,"dragon-ball-pack",ex);}return true;
        }
    }
}
