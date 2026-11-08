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
    // Ordinary combine of ten EXPCarePillScrap 3002560 into one bound
    // EXPCarePill 3002559 and 3,000 Revora Points. Remainder stays on the
    // clicked source. Direct daily P7 is unchanged.
    internal static class ExpCareCombine
    {
        internal const uint Scrap=3002560;
        internal const uint Pill=3002559;
        internal const int Cost=10;
        internal const uint RewardRvp=3000;
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        private static bool enabled;
        internal static void Initialize(){
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_exp_care_combines(
id uuid PRIMARY KEY,uid bigint NOT NULL CHECK(uid>0),item_uid bigint NOT NULL,item_type integer NOT NULL CHECK(item_type=3002560),
before_stack integer NOT NULL CHECK(before_stack>=10),bound boolean NOT NULL,consumed integer NOT NULL CHECK(consumed=10),
pill_uid bigint NOT NULL,rvp integer NOT NULL CHECK(rvp=3000),combined_at timestamptz NOT NULL);");}enabled=true;
        }
        private static bool Ready(GameState p){return enabled && Q.Player(p) && p.Booth==null && p.Trade!=null && !p.Trade.InTrade;}
        private static ConquerItem NewPill(){
            ushort dur=1;
            if(ConquerItemInformation.BaseInformations.ContainsKey(Pill))dur=ConquerItemInformation.BaseInformations[Pill].Durability;
            return new ConquerItem(true){ID=Pill,UID=ConquerItem.ItemUID.Next,Bound=true,StackSize=1,Durability=dur,MaximDurability=dur};
        }
        internal static bool TryHandle(GameState p,byte[] packet){
            ConquerItem item;
            if(p==null || p.Inventory==null || packet==null || packet.Length<12 || !p.Inventory.TryGetItem(BitConverter.ToUInt32(packet,8),out item) || item.ID!=Scrap)return false;
            if(packet.Length<32 || packet.Length>128 || BitConverter.ToUInt16(packet,0)+8!=packet.Length || BitConverter.ToUInt16(packet,2)!=1009
                || BitConverter.ToUInt16(packet,20)!=4 || BitConverter.ToUInt64(packet,packet.Length-8)!=Revora.Network.PacketHandler.ClientSeal)return true;
            TryUse(item,p);return true;
        }
        internal static bool TryUse(ConquerItem item,GameState p){
            if(item==null || item.ID!=Scrap)return false;if(!Ready(p))return true;
            lock(p.ItemSyncRoot)try{
                ConquerItem owned;DateTime now=Clock();
                if(!Ready(p) || !p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(item,owned) || item.Position!=0 || item.Warehouse!=0
                    || item.Lock!=0 || item.Suspicious || item.InWardrobe || (item.Days==0?item.TimeLeftInMinutes!=0:now.ToLocalTime()>=item.DayStamp.AddDays(item.Days)))return true;
                if(item.StackSize<Cost){p.Send("You need 10 EXPCarePill scraps to combine.");return true;}
                long nextRvp=(long)p.Entity.RevoraPoint+RewardRvp;
                if(nextRvp>uint.MaxValue){p.Send("Spend some Revora Points before combining these scraps.");return true;}
                bool deplete=item.StackSize==Cost;
                if(!deplete && p.Inventory.Count>=40){p.Send("Make room for the EXPCarePill.");return true;}
                var pill=NewPill();
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("EXPCare combine owner missing.");
                    var ids=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader())while(r.Read()){if(ids.Count>=40)throw new InvalidOperationException("EXPCare combine inventory exceeds capacity.");ids.Add(checked((uint)Convert.ToInt64(r[0])));}
                    if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("EXPCare combine inventory changed.");
                    string sql=deplete?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-"+Cost.ToString(CultureInfo.InvariantCulture);
                    if(Q.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=@type AND warehouse=0 AND position=0 AND stacksize=@stack AND bound=@bound
AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                        "item",(long)item.UID,"uid",(long)p.Entity.UID,"type",(int)item.ID,"stack",(int)item.StackSize,"bound",item.Bound?1:0,
                        "days",item.Days.ToString(CultureInfo.InvariantCulture),"permanent",item.Days==0,"stamp",item.DayStamp.ToBinary())!=1)
                        throw new InvalidOperationException("EXPCare scrap ownership changed.");
                    Q.Insert(c,t,p.Entity.UID,pill);
                    if(Q.Execute(c,t,@"UPDATE entities SET revorapoint=@rvp WHERE uid=@uid AND COALESCE(revorapoint,0)=@old",
                        "rvp",nextRvp,"uid",(long)p.Entity.UID,"old",(long)p.Entity.RevoraPoint)!=1)
                        throw new InvalidOperationException("EXPCare Revora Point balance changed.");
                    if(Q.Execute(c,t,@"INSERT INTO revora_exp_care_combines(id,uid,item_uid,item_type,before_stack,bound,consumed,pill_uid,rvp,combined_at)
VALUES(@id,@uid,@item,@type,@stack,@bound,@consumed,@pill,@rvp,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"item",(long)item.UID,
                        "type",(int)item.ID,"stack",(int)item.StackSize,"bound",item.Bound,"consumed",Cost,"pill",(long)pill.UID,"rvp",(int)RewardRvp,"now",now)!=1)
                        throw new InvalidOperationException("EXPCare combine receipt was not saved.");
                    if(!Ready(p))throw new InvalidOperationException("EXPCare combine session changed.");t.Commit();
                }}
                Q.Install(p,deplete?new[]{item.UID}:new uint[0],new[]{pill});
                if(!deplete){item.StackSize=(ushort)(item.StackSize-Cost);item.Mode=Enums.ItemMode.Update;item.Send(p);item.Mode=Enums.ItemMode.Default;}
                p.Entity.ApplyCommittedRevoraPoints((uint)nextRvp);
                p.Send("You have recieved EXPCarePill");
            }catch(Exception ex){Q.Fail(p,"exp-care-combine",ex);}return true;
        }
    }
}
