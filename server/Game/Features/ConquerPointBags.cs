using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Ordinary use of leftover CP bags. Amounts match the deleted Bags Cps switch.
    internal static class ConquerPointBags
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        private static bool enabled;
        private static readonly Dictionary<uint,uint> Amounts=new Dictionary<uint,uint>{
            {729911,3},{3001133,5},{720657,5},{729912,6},{720656,10},{3001134,10},{3001135,20},{720655,20},{720658,25},
            {720663,50},{720659,50},{720660,100},{720664,100},{720665,200},{720675,250},{720653,270},{720681,500},{720676,500},
            {3001136,500},{720677,1000},{720682,1000},{720687,1000},{720661,1350},{720683,2000},{720688,2000},{720693,2500},
            {720666,2700},{720689,4000},{720694,5000},{720662,6900},{720695,10000},{720678,13500},{720667,13800},{720684,27000},
            {720685,138000},{722178,2000}
        };
        private static bool Handles(uint id){return Amounts.ContainsKey(id);}
        internal static void Initialize(){
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){Q.Execute(c,t,@"CREATE TABLE IF NOT EXISTS revora_cp_bag_uses(
id uuid PRIMARY KEY,uid bigint NOT NULL CHECK(uid>0),item_uid bigint NOT NULL,
item_type integer NOT NULL CHECK(item_type IN(729911,3001133,720657,729912,720656,3001134,3001135,720655,720658,720663,720659,720660,720664,720665,720675,720653,720681,720676,3001136,720677,720682,720687,720661,720683,720688,720693,720666,720689,720694,720662,720695,720678,720667,720684,720685,722178)),
before_stack integer NOT NULL CHECK(before_stack>=0),bound boolean NOT NULL,
cps bigint NOT NULL CHECK(cps>=0),used_at timestamptz NOT NULL);
ALTER TABLE revora_cp_bag_uses DROP CONSTRAINT IF EXISTS revora_cp_bag_uses_item_type_check;
ALTER TABLE revora_cp_bag_uses ADD CONSTRAINT revora_cp_bag_uses_item_type_check CHECK(item_type IN(729911,3001133,720657,729912,720656,3001134,3001135,720655,720658,720663,720659,720660,720664,720665,720675,720653,720681,720676,3001136,720677,720682,720687,720661,720683,720688,720693,720666,720689,720694,720662,720695,720678,720667,720684,720685,722178));");t.Commit();}}enabled=true;
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
                if(item.ID==722178 && p.Inventory.Count>38){p.Send(new Message("You Must have 1 space in you Inventory To Open You Box",Color.White,255));return true;}
                uint cps=Amounts[item.ID];
                long nextCp=(long)p.Entity.ConquerPoints+cps;
                if(nextCp>uint.MaxValue){p.Send("Spend some CPs before opening this bag.");return true;}
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("CP bag owner missing.");
                    var ids=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader())while(r.Read()){if(ids.Count>=40)throw new InvalidOperationException("CP bag inventory exceeds capacity.");ids.Add(checked((uint)Convert.ToInt64(r[0])));}
                    if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("CP bag inventory changed.");
                    string sql=item.StackSize<=1?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
                    if(Q.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=@type AND warehouse=0 AND position=0 AND stacksize=@stack AND bound=@bound
AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                        "item",(long)item.UID,"uid",(long)p.Entity.UID,"type",(int)item.ID,"stack",(int)item.StackSize,"bound",item.Bound?1:0,
                        "days",item.Days.ToString(CultureInfo.InvariantCulture),"permanent",item.Days==0,"stamp",item.DayStamp.ToBinary())!=1)
                        throw new InvalidOperationException("CP bag ownership changed.");
                    if(Q.Execute(c,t,@"UPDATE entities SET conquerpoints=@cp WHERE uid=@uid AND COALESCE(conquerpoints,0)=@old",
                        "cp",nextCp,"uid",(long)p.Entity.UID,"old",PurseBalance.PreimageCp(p))!=1)
                        throw new InvalidOperationException("CP bag balance changed.");
                    if(Q.Execute(c,t,@"INSERT INTO revora_cp_bag_uses(id,uid,item_uid,item_type,before_stack,bound,cps,used_at)
VALUES(@id,@uid,@item,@type,@stack,@bound,@cps,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"item",(long)item.UID,
                        "type",(int)item.ID,"stack",(int)item.StackSize,"bound",item.Bound,"cps",(long)cps,"now",now)!=1)
                        throw new InvalidOperationException("CP bag receipt was not saved.");
                    if(!Ready(p))throw new InvalidOperationException("CP bag session changed.");t.Commit();
                }}
                Q.Install(p,item.StackSize<=1?new[]{item.UID}:new uint[0],new ConquerItem[0]);
                if(item.StackSize>1){item.StackSize--;item.Mode=Enums.ItemMode.Update;item.Send(p);item.Mode=Enums.ItemMode.Default;}
                p.Entity.ApplyCommittedConquerPoints((uint)nextCp);
                // The legacy Nimbus case announced globally but did not send the ordinary personal receipt.
                if(item.ID!=720685 && item.ID!=722178)p.Send(new Message("You received "+cps.ToString(CultureInfo.InvariantCulture)+" ConquerPoints!",Color.Red,Message.System));
                if(item.ID==720685)Kernel.SendWorldMessage(new Message("Congratulations! "+p.Entity.Name+" opened a NimbusCPPack and obtained 138000 CPs.",System.Drawing.Color.Yellow,Message.Center),Server.GamePool);
            }catch(Exception ex){Q.Fail(p,"cp-bag",ex);}return true;
        }
    }
}
