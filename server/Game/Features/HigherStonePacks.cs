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
    // MillionaireLee NPC 11275 options 4 and 5 pack ten 730002/730003 into unbound
    // 729022/729023. That packing path stays a legacy Contains/Remove/Add split-write.
    // Leftover UseItem pays ten stones at Plus 2 or Plus 3. Bound packs keep Bound
    // on the stones. Slot capacity credits the source slot only when it is deleted.
    internal static class HigherStonePacks
    {
        internal const uint Pack2=729022,Stone2=730002,Pack3=729023,Stone3=730003;
        internal const int Count=10;
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static Action<string> Spoken;
        internal static Action<GameState> BeforeInstall;
        private static bool enabled;
        internal static bool Enabled{get{return enabled;}}
        private static bool Handles(uint id){return id==Pack2 || id==Pack3;}
        private static uint StoneOf(uint pack){return pack==Pack3?Stone3:Stone2;}
        private static byte PlusOf(uint pack){return pack==Pack3?(byte)3:(byte)2;}
        internal static void Initialize(){
            using(var c=DataHolder.MySqlConnection){c.Open();
                Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_higher_stone_pack_uses(
id uuid PRIMARY KEY,uid bigint NOT NULL,pack_uid bigint NOT NULL,
pack_type integer NOT NULL CHECK(pack_type IN (729022,729023)),
before_stack integer NOT NULL CHECK(before_stack>=0),bound boolean NOT NULL,
rewards bigint[] NOT NULL,used_at timestamptz NOT NULL);");}
            if(!ConquerItemInformation.BaseInformations.ContainsKey(Pack2) || !ConquerItemInformation.BaseInformations.ContainsKey(Pack3)
                || !ConquerItemInformation.BaseInformations.ContainsKey(Stone2) || !ConquerItemInformation.BaseInformations.ContainsKey(Stone3))
                throw new InvalidOperationException("Higher stone pack catalog incomplete.");
            enabled=true;
        }
        private static bool Ready(GameState p){return enabled && Q.Player(p) && p.Booth==null && p.Trade!=null && !p.Trade.InTrade;}
        private static void Full(GameState p){p.Send(Constants.FullInventory);var s=Spoken;if(s!=null)s("Your inventory is Full.");}
        private static ConquerItem StoneItem(uint pack,bool bound){
            uint stone=StoneOf(pack);
            ushort dur=1;
            if(ConquerItemInformation.BaseInformations.ContainsKey(stone))dur=ConquerItemInformation.BaseInformations[stone].Durability;
            return new ConquerItem(true){ID=stone,UID=ConquerItem.ItemUID.Next,Plus=PlusOf(pack),Bound=bound,StackSize=1,Durability=dur,MaximDurability=dur};
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
                if(!Ready(p) || !p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(owned,item) || item.Position!=0 || item.Warehouse!=0
                    || item.Lock!=0 || item.Suspicious || item.InWardrobe || (item.Days==0?item.TimeLeftInMinutes!=0:now.ToLocalTime()>=item.DayStamp.AddDays(item.Days)))return true;
                bool deplete=item.StackSize<=1;
                int spaces=Count-(deplete?1:0);
                if(p.Inventory.Count+spaces>40){Full(p);return true;}
                var rewards=new ConquerItem[Count];
                for(int i=0;i<Count;i++)rewards[i]=StoneItem(item.ID,item.Bound);
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("Higher stone pack owner missing.");
                    var ids=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader())while(r.Read()){if(ids.Count>=40)throw new InvalidOperationException("Higher stone pack inventory exceeds capacity.");ids.Add(checked((uint)Convert.ToInt64(r[0])));}
                    if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Higher stone pack inventory changed.");
                    string sql=deplete?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
                    if(Q.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=@type AND warehouse=0 AND position=0 AND stacksize=@stack AND bound=@bound
AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                        "item",(long)item.UID,"uid",(long)p.Entity.UID,"type",(int)item.ID,"stack",(int)item.StackSize,"bound",item.Bound?1:0,
                        "days",item.Days.ToString(CultureInfo.InvariantCulture),"permanent",item.Days==0,"stamp",item.DayStamp.ToBinary())!=1)
                        throw new InvalidOperationException("Higher stone pack ownership changed.");
                    foreach(var reward in rewards)
                        if(Q.Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,plus,durability,maximdurability)
VALUES(@item,@type,@uid,0,0,1,@bound,@plus,@dur,@max)","item",(long)reward.UID,"type",(int)reward.ID,"uid",(long)p.Entity.UID,
                            "bound",reward.Bound?1:0,"plus",(int)reward.Plus,"dur",(int)reward.Durability,"max",(int)reward.MaximDurability)!=1)
                            throw new InvalidOperationException("Higher stone pack item was not saved.");
                    if(Q.Execute(c,t,@"INSERT INTO revora_higher_stone_pack_uses(id,uid,pack_uid,pack_type,before_stack,bound,rewards,used_at)
VALUES(@id,@uid,@pack,@type,@stack,@bound,@rewards,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"pack",(long)item.UID,
                        "type",(int)item.ID,"stack",(int)item.StackSize,"bound",item.Bound,"rewards",rewards.Select(r=>(long)r.UID).ToArray(),"now",now)!=1)
                        throw new InvalidOperationException("Higher stone pack receipt was not saved.");
                    if(!Ready(p))throw new InvalidOperationException("Higher stone pack session changed.");t.Commit();
                }}
                var before=BeforeInstall;
                if(before!=null)before(p);
                Q.Install(p,deplete?new[]{item.UID}:new uint[0],rewards);
                if(!deplete){item.StackSize--;item.Mode=Enums.ItemMode.Update;item.Send(p);item.Mode=Enums.ItemMode.Default;}
            }catch(Exception ex){Q.Fail(p,"higher-stone-pack",ex);}return true;
        }
    }
}
