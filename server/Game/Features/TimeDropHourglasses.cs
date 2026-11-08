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
    // Leftover bag use of TimeDrop hourglasses. Prizewell issuance stays closed.
    // Durations match the live handler: 3007985 30 minutes, 3007989 one hour,
    // 3007992 two hours, for both TimeDropSlayer and double EXP.
    internal static class TimeDropHourglasses
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        private static bool enabled;
        private static bool Handles(uint id){return id==3007985 || id==3007989 || id==3007992;}
        internal static ushort Seconds(uint id){return id==3007992?(ushort)7200:id==3007989?(ushort)3600:(ushort)1800;}
        internal static void Initialize(){
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_timedrop_uses(
id uuid PRIMARY KEY,uid bigint NOT NULL CHECK(uid>0),item_uid bigint NOT NULL,item_type integer NOT NULL CHECK(item_type IN(3007985,3007989,3007992)),
before_stack integer NOT NULL CHECK(before_stack>=0),bound boolean NOT NULL,seconds integer NOT NULL,used_at timestamptz NOT NULL,
CHECK((item_type=3007985 AND seconds=1800) OR (item_type=3007989 AND seconds=3600) OR (item_type=3007992 AND seconds=7200)));");}enabled=true;
        }
        private static bool Ready(GameState p){return enabled && Q.Player(p) && p.Booth==null && p.Trade!=null && !p.Trade.InTrade;}
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
                if(p.Entity.TimeDropSlayer!=0){p.Send("You already have a Revora Points timer. You cannot use another.");return true;}
                if(p.Entity.DoubleExperienceTime!=0){p.Send("Keep this hourglass until your current EXP bonus ends.");return true;}
                ushort seconds=Seconds(item.ID);
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    TimeDropBalance.AssertSaved(c,t,p);
                    DoubleExperienceBalance.AssertSaved(c,t,p);
                    var ids=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader())while(r.Read()){if(ids.Count>=40)throw new InvalidOperationException("TimeDrop inventory exceeds capacity.");ids.Add(checked((uint)Convert.ToInt64(r[0])));}
                    if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("TimeDrop inventory changed.");
                    string sql=item.StackSize<=1?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
                    if(Q.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=@type AND warehouse=0 AND position=0 AND stacksize=@stack AND bound=@bound
AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                        "item",(long)item.UID,"uid",(long)p.Entity.UID,"type",(int)item.ID,"stack",(int)item.StackSize,"bound",item.Bound?1:0,
                        "days",item.Days.ToString(CultureInfo.InvariantCulture),"permanent",item.Days==0,"stamp",item.DayStamp.ToBinary())!=1)
                        throw new InvalidOperationException("TimeDrop ownership changed.");
                    TimeDropBalance.Persist(c,t,p,seconds);
                    DoubleExperienceBalance.Persist(c,t,p,seconds);
                    if(Q.Execute(c,t,@"INSERT INTO revora_timedrop_uses(id,uid,item_uid,item_type,before_stack,bound,seconds,used_at)
VALUES(@id,@uid,@item,@type,@stack,@bound,@seconds,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"item",(long)item.UID,
                        "type",(int)item.ID,"stack",(int)item.StackSize,"bound",item.Bound,"seconds",(int)seconds,"now",now)!=1)
                        throw new InvalidOperationException("TimeDrop receipt was not saved.");
                    if(!Ready(p))throw new InvalidOperationException("TimeDrop session changed.");t.Commit();
                }}
                Q.Install(p,item.StackSize<=1?new[]{item.UID}:new uint[0],new ConquerItem[0]);
                if(item.StackSize>1){item.StackSize--;item.Mode=Enums.ItemMode.Update;item.Send(p);item.Mode=Enums.ItemMode.Default;}
                TimeDropBalance.ApplyCommitted(p,seconds);
                DoubleExperienceBalance.ApplyCommitted(p,seconds);
            }catch(Exception ex){Q.Fail(p,"timedrop",ex);}return true;
        }
    }
}
