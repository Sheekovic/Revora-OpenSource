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
    // Catalog-defined rewards shared by daily quests; ownership alone grants no quest progress.
    internal static class QuestGiftPacks
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        private static bool enabled;
        internal static Func<int,int> SoulRoll=max=>Kernel.Random.Next(max);
        private static uint[] souls;
        private sealed class Recent {internal readonly Dictionary<uint,DateTime> Until=new Dictionary<uint,DateTime>();}
        private static readonly ConditionalWeakTable<GameState,Recent> recent=new ConditionalWeakTable<GameState,Recent>();
        private static bool Handles(uint id){return id==3003126 || id==3005134 || id==720027 || id==723714 || id==710212 || id==721269 || id==720957;}
        internal static void Initialize(){
            JiangTraining.InitializePills();
            foreach(uint id in new uint[]{3005134,751099,755099,720027,723714,1088001,710212,711504,721269,720957})
                if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Quest gift catalog is missing.");
            souls=ConquerItemInformation.BaseInformations.Values.Where(i=>i.PurificationLevel==7 && i.ID>=800000 && i.ID<900000).Select(i=>i.ID).OrderBy(i=>i).ToArray();
            if(souls.Length==0)throw new InvalidOperationException("Custom Magnolia P7 catalog missing.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_quest_gift_openings(
id uuid PRIMARY KEY,uid bigint NOT NULL CHECK(uid>0),item_uid bigint NOT NULL,item_type integer NOT NULL CHECK(item_type IN(720027,723714)),
before_stack integer NOT NULL CHECK(before_stack>=0),bound boolean NOT NULL,rewards bigint[] NOT NULL,
silver bigint NOT NULL,opened_at timestamptz NOT NULL,
CHECK((item_type=720027 AND silver=0 AND cardinality(rewards)=10) OR (item_type=723714 AND silver=800000 AND cardinality(rewards)=0)));
ALTER TABLE revora_quest_gift_openings ADD COLUMN IF NOT EXISTS cps bigint NOT NULL DEFAULT 0 CHECK(cps>=0);
ALTER TABLE revora_quest_gift_openings ADD COLUMN IF NOT EXISTS rvps bigint NOT NULL DEFAULT 0 CHECK(rvps>=0);
ALTER TABLE revora_quest_gift_openings DROP CONSTRAINT IF EXISTS revora_quest_gift_openings_item_type_check;
ALTER TABLE revora_quest_gift_openings ADD CONSTRAINT revora_quest_gift_openings_item_type_check CHECK(item_type IN(720027,723714,710212,721269,720957,3005134));
ALTER TABLE revora_quest_gift_openings DROP CONSTRAINT IF EXISTS revora_quest_gift_openings_check;
ALTER TABLE revora_quest_gift_openings ADD CONSTRAINT revora_quest_gift_openings_check CHECK(
(item_type=720027 AND silver=0 AND cardinality(rewards)=10) OR (item_type=723714 AND silver=800000 AND cardinality(rewards)=0)
OR (item_type=710212 AND silver=0 AND cardinality(rewards)=3)
OR (item_type=3005134 AND silver=0 AND cardinality(rewards)=2)
OR (item_type IN(721269,720957) AND silver=0 AND cardinality(rewards) IN(0,1)));
ALTER TABLE revora_quest_gift_openings DROP CONSTRAINT IF EXISTS revora_quest_gift_currency_check;
ALTER TABLE revora_quest_gift_openings ADD CONSTRAINT revora_quest_gift_currency_check CHECK(
(item_type IN(721269,720957) AND cps=5000 AND rvps=1000) OR (item_type=3005134 AND cps=5000 AND rvps=0) OR (item_type NOT IN(721269,720957,3005134) AND cps=0 AND rvps=0));");}enabled=true;
        }
        private static bool Ready(GameState p){return enabled && Q.Player(p) && p.Booth==null && p.Trade!=null && !p.Trade.InTrade;}
        private static bool AtLottery(GameState p){return Q.Near(p,923,1002) || Q.Near(p,923,1036);}
        private static bool Usable(ConquerItem item,DateTime now){return item.Position==0 && item.Warehouse==0 && item.Lock==0
            && !item.Suspicious && !item.InWardrobe && (item.Days==0?item.TimeLeftInMinutes==0:now.ToLocalTime()<item.DayStamp.AddDays(item.Days));}
        internal static void ExchangeLottery(GameState p){
            if(!Ready(p) || !AtLottery(p))return;
            lock(p.ItemSyncRoot){
                var source=p.Inventory.Objects.Where(i=>i.ID==710212 && Usable(i,Clock())).OrderBy(i=>i.UID).FirstOrDefault();
                if(source==null){p.Send("Bring me a Lottery Ticket, and I will exchange it for three Small Lottery Tickets.");return;}
                Use(source,p,true);
            }
        }
        internal static bool TryHandle(GameState p,byte[] packet){
            ConquerItem item;
            if(p==null || p.Inventory==null || packet==null || packet.Length<12 || !p.Inventory.TryGetItem(BitConverter.ToUInt32(packet,8),out item) || !Handles(item.ID))return false;
            if(packet.Length<32 || packet.Length>128 || BitConverter.ToUInt16(packet,0)+8!=packet.Length || BitConverter.ToUInt16(packet,2)!=1009
                || BitConverter.ToUInt16(packet,20)!=4 || BitConverter.ToUInt64(packet,packet.Length-8)!=Revora.Network.PacketHandler.ClientSeal)return true;
            TryUse(item,p);return true;
        }
        internal static bool TryUse(ConquerItem item,GameState p){
            return Use(item,p,false);
        }
        private static bool Use(ConquerItem item,GameState p,bool exchange){
            if(item==null || !Handles(item.ID))return false;if(!Ready(p))return true;
            if(item.ID==3003126){JiangTraining.ArmPill(p,item);return true;}
            bool love=item.ID==3005134;bool ticket=item.ID==710212;bool custom=item.ID==721269 || item.ID==720957;
            if(ticket && (!exchange || !AtLottery(p)))return true;
            lock(p.ItemSyncRoot)try{
                ConquerItem owned;DateTime now=Clock();
                if(!Ready(p) || !p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(item,owned) || item.Position!=0 || item.Warehouse!=0
                    || item.Lock!=0 || item.Suspicious || item.InWardrobe || (item.Days==0?item.TimeLeftInMinutes!=0:now.ToLocalTime()>=item.DayStamp.AddDays(item.Days)))return true;
                var history=recent.GetValue(p,_=>new Recent());foreach(uint uid in history.Until.Where(x=>x.Value<=now).Select(x=>x.Key).ToArray())history.Until.Remove(uid);
                uint requestKey=ticket?0u:item.UID;
                if(history.Until.ContainsKey(requestKey))return true;
                bool scroll=item.ID==720027;bool silver=item.ID==723714;int slots=love?2:scroll?10:ticket?3:custom?1:0;
                if(p.Inventory.Count+slots-(item.StackSize<=1?1:0)>40){p.Send(love?"Make room for your roses and kisses.":custom?"Make room for a P7 Dragon Soul.":ticket?"Make room for three Small Lottery Tickets.":"Make room for ten Meteors before opening this scroll.");return true;}
                ulong next=p.Entity.Money;
                if(silver){if(next>(ulong)long.MaxValue-800000){p.Send("Store some silver before opening this bag.");return true;}next+=800000;}
                uint nextCp=p.Entity.ConquerPoints,nextRvp=p.Entity.RevoraPoint,soul=0;
                if(love){if((ulong)nextCp+5000>uint.MaxValue){p.Send("Spend some CPs before opening this pack.");return true;}nextCp+=5000;}
                if(custom){
                    if((ulong)nextCp+5000>uint.MaxValue || (ulong)nextRvp+1000>uint.MaxValue){p.Send("Spend some points before opening this pack.");return true;}
                    nextCp+=5000;nextRvp+=1000;int draw=SoulRoll(souls.Length);
                    if(draw<0 || draw>=souls.Length)throw new InvalidOperationException("Custom pack soul draw outside catalog.");soul=souls[draw];
                }
                var rewards=love?new[]{Q.New(751099,true),Q.New(755099,true)}:Enumerable.Range(0,slots).Select(_=>Q.New(custom?soul:ticket?711504u:1088001u,custom || item.Bound)).ToArray();
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("Quest gift owner missing.");
                    var ids=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader())while(r.Read()){if(ids.Count>=40)throw new InvalidOperationException("Quest gift inventory exceeds capacity.");ids.Add(checked((uint)Convert.ToInt64(r[0])));}
                    if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Quest gift inventory changed.");
                    string sql=item.StackSize<=1?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
                    if(Q.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=@type AND warehouse=0 AND position=0 AND stacksize=@stack AND bound=@bound
AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                        "item",(long)item.UID,"uid",(long)p.Entity.UID,"type",(int)item.ID,"stack",(int)item.StackSize,"bound",item.Bound?1:0,
                        "days",item.Days.ToString(CultureInfo.InvariantCulture),"permanent",item.Days==0,"stamp",item.DayStamp.ToBinary())!=1)
                        throw new InvalidOperationException("Quest gift ownership changed.");
                    if(silver)SilverBalance.Persist(c,t,p,next);
                    if((custom || love) && Q.Execute(c,t,@"UPDATE entities SET conquerpoints=@cp,revorapoint=@rvp WHERE uid=@uid
AND COALESCE(conquerpoints,0)=@oldcp AND COALESCE(revorapoint,0)=@oldrvp",
                        "cp",(long)nextCp,"rvp",(long)nextRvp,"uid",(long)p.Entity.UID,"oldcp",PurseBalance.PreimageCp(p),"oldrvp",PurseBalance.PreimageRvp(p))!=1)
                        throw new InvalidOperationException("Custom pack currency changed.");
                    foreach(var reward in rewards)
                        if(Q.Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability)
VALUES(@item,@type,@uid,0,0,1,@bound,@dur,@max)","item",(long)reward.UID,"type",(int)reward.ID,"uid",(long)p.Entity.UID,
                            "bound",reward.Bound?1:0,"dur",(int)reward.Durability,"max",(int)reward.MaximDurability)!=1)throw new InvalidOperationException("Quest gift item was not saved.");
                    if(Q.Execute(c,t,@"INSERT INTO revora_quest_gift_openings(id,uid,item_uid,item_type,before_stack,bound,rewards,silver,opened_at,cps,rvps)
VALUES(@id,@uid,@item,@type,@stack,@bound,@rewards,@silver,@now,@cp,@rvp)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"item",(long)item.UID,
                        "type",(int)item.ID,"stack",(int)item.StackSize,"bound",item.Bound,"rewards",rewards.Select(i=>(long)i.UID).ToArray(),"silver",silver?800000L:0L,"now",now,"cp",custom || love?5000L:0L,"rvp",custom?1000L:0L)!=1)
                        throw new InvalidOperationException("Quest gift receipt was not saved.");
                    if(!Ready(p) || (ticket && !AtLottery(p)))throw new InvalidOperationException("Quest gift session changed.");t.Commit();
                }}
                if(history.Until.Count>=64)history.Until.Remove(history.Until.OrderBy(x=>x.Value).First().Key);history.Until[requestKey]=now.AddSeconds(1);
                Q.Install(p,item.StackSize<=1?new[]{item.UID}:new uint[0],rewards);
                if(item.StackSize>1){item.StackSize--;item.Mode=Enums.ItemMode.Update;item.Send(p);item.Mode=Enums.ItemMode.Default;}
                if(silver)SilverBalance.ApplyCommitted(p,next);
                if(love){p.Entity.ApplyCommittedConquerPoints(nextCp);p.Send("You received 99 Red Roses, 99 Kisses and 5,000 CPs.");}
                if(custom){p.Entity.ApplyCommittedConquerPoints(nextCp);p.Entity.ApplyCommittedRevoraPoints(nextRvp);p.Send("You received a P7 Dragon Soul, 1,000 RVPs and 5,000 CPs.");}
            }catch(Exception ex){Q.Fail(p,"quest-gift-pack",ex);}return true;
        }
    }
}
