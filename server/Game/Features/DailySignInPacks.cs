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
    // Ordinary Daily Sign-In packs 3100011/3100013-3100017.
    internal static class DailySignInPacks
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static Action<string> Spoken;
        private static bool enabled;
        private static bool Handles(uint id){return id==3100011 || id==3100013 || id==3100014 || id==3100015 || id==3100016 || id==3100017;}
        internal static void Initialize(){
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_daily_sign_in_pack_uses(
id uuid PRIMARY KEY,uid bigint NOT NULL CHECK(uid>0),item_uid bigint NOT NULL,
item_type integer NOT NULL CHECK(item_type IN(3100011,3100013,3100014,3100015,3100016,3100017)),
before_stack integer NOT NULL CHECK(before_stack>=0),bound boolean NOT NULL,
cps bigint NOT NULL CHECK(cps>=0),silver bigint NOT NULL CHECK(silver>=0),
rvp integer NOT NULL CHECK(rvp>=0),boundcps bigint NOT NULL CHECK(boundcps>=0),
rewards bigint[] NOT NULL,used_at timestamptz NOT NULL);");}enabled=true;
        }
        private static bool Ready(GameState p){return enabled && Q.Player(p) && p.Booth==null && p.Trade!=null && !p.Trade.InTrade;}
        private static void Hear(GameState p,string text){p.Send(new Message(text,System.Drawing.Color.Red,Message.System));var s=Spoken;if(s!=null)s(text);}
        private static void Full(GameState p){p.Send(Constants.FullInventory);var s=Spoken;if(s!=null)s("Your inventory is Full.");}
        private static uint[] Items(uint id){
            if(id==3100011)return new uint[]{723727,723727,3009002};
            if(id==3100013)return new uint[]{3009003};
            if(id==3100014)return new uint[]{720121};
            if(id==3100015)return new uint[]{3009002,3009002,3009002,3009002,3009002,720121,721170};
            if(id==3100016)return new uint[]{3009003,3009003,3009003,3009003,3009003};
            return new uint[]{2100085,3009003,3009003,3009003,3009003,3009003};
        }
        private static uint Cps(uint id){return id==3100011?10000u:id==3100013?20000u:id==3100016?500000u:0;}
        private static ulong Silver(uint id){return id==3100011?50000000UL:id==3100013?100000000UL:id==3100016?1000000000UL:0;}
        private static uint Rvp(uint id){return id==3100011?2000u:id==3100016?20000u:0;}
        private static uint BoundCps(uint id){return id==3100014?500000u:id==3100016?1500000u:0;}
        private static string Text(uint id){
            if(id==3100011)return "Congratulations! You opened the daily pack and received everything.";
            if(id==3100013)return "Congratulations! You opened the 2-day daily pack and received everything.";
            if(id==3100014)return "Congratulations! You opened the 7-day daily pack and received everything.";
            if(id==3100015)return "Congratulations! You opened the 14-day daily pack and received everything.";
            if(id==3100016)return "Congratulations! You opened the 21-day daily pack and received everything.";
            return "Congratulations! You opened the 30-day daily pack and received the cup.";
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
                uint[] gifts=Items(item.ID);uint cps=Cps(item.ID);ulong silver=Silver(item.ID);uint rvp=Rvp(item.ID);uint bound=BoundCps(item.ID);
                bool deplete=item.StackSize<=1;int spaces=gifts.Length-(deplete?1:0);
                if(p.Inventory.Count+spaces>40){Full(p);return true;}
                long nextCp=(long)p.Entity.ConquerPoints+cps;
                if(cps!=0 && nextCp>uint.MaxValue){Hear(p,"Spend some CPs before opening this pack.");return true;}
                ulong nextSilver=p.Entity.Money+silver;
                if(silver!=0 && (nextSilver<silver || nextSilver>Entity.MaximumSilver)){Hear(p,"Store some silver before opening this pack.");return true;}
                long nextRvp=(long)p.Entity.RevoraPoint+rvp;
                if(rvp!=0 && nextRvp>uint.MaxValue){Hear(p,"Spend some Revora Points before opening this pack.");return true;}
                long nextBound=(long)p.Entity.BoundCps+bound;
                if(bound!=0 && nextBound>uint.MaxValue){Hear(p,"Spend some Bound CPs before opening this pack.");return true;}
                var rewards=gifts.Select(Item).ToArray();
                if(item.Bound) foreach(var reward in rewards) reward.Bound=true;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("Daily sign-in pack owner missing.");
                    if(silver!=0)SilverBalance.AssertSaved(c,t,p);
                    var ids=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader())while(r.Read()){if(ids.Count>=40)throw new InvalidOperationException("Daily sign-in pack inventory exceeds capacity.");ids.Add(checked((uint)Convert.ToInt64(r[0])));}
                    if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Daily sign-in pack inventory changed.");
                    string sql=deplete?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
                    if(Q.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=@type AND warehouse=0 AND position=0 AND stacksize=@stack AND bound=@bound
AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                        "item",(long)item.UID,"uid",(long)p.Entity.UID,"type",(int)item.ID,"stack",(int)item.StackSize,"bound",item.Bound?1:0,
                        "days",item.Days.ToString(CultureInfo.InvariantCulture),"permanent",item.Days==0,"stamp",item.DayStamp.ToBinary())!=1)
                        throw new InvalidOperationException("Daily sign-in pack ownership changed.");
                    foreach(var reward in rewards)
                        if(Q.Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,plus,durability,maximdurability)
VALUES(@item,@type,@uid,0,0,1,@bound,@plus,@dur,@max)","item",(long)reward.UID,"type",(int)reward.ID,"uid",(long)p.Entity.UID,
                            "bound",reward.Bound?1:0,"plus",(int)reward.Plus,"dur",(int)reward.Durability,"max",(int)reward.MaximDurability)!=1)
                            throw new InvalidOperationException("Daily sign-in pack item was not saved.");
                    if(cps!=0 && Q.Execute(c,t,@"UPDATE entities SET conquerpoints=@cp WHERE uid=@uid AND COALESCE(conquerpoints,0)=@old",
                        "cp",nextCp,"uid",(long)p.Entity.UID,"old",(long)p.Entity.ConquerPoints)!=1)
                        throw new InvalidOperationException("Daily sign-in pack CP balance changed.");
                    if(silver!=0)SilverBalance.Persist(c,t,p,nextSilver);
                    if(rvp!=0 && Q.Execute(c,t,@"UPDATE entities SET revorapoint=@rvp WHERE uid=@uid AND COALESCE(revorapoint,0)=@old",
                        "rvp",nextRvp,"uid",(long)p.Entity.UID,"old",(long)p.Entity.RevoraPoint)!=1)
                        throw new InvalidOperationException("Daily sign-in pack Revora Point balance changed.");
                    if(bound!=0 && Q.Execute(c,t,@"UPDATE entities SET boundcps=@cps WHERE uid=@uid AND COALESCE(boundcps,0)=@old",
                        "cps",nextBound,"uid",(long)p.Entity.UID,"old",(long)p.Entity.BoundCps)!=1)
                        throw new InvalidOperationException("Daily sign-in pack Bound CP balance changed.");
                    if(Q.Execute(c,t,@"INSERT INTO revora_daily_sign_in_pack_uses(id,uid,item_uid,item_type,before_stack,bound,cps,silver,rvp,boundcps,rewards,used_at)
VALUES(@id,@uid,@item,@type,@stack,@bound,@cps,@silver,@rvp,@boundcps,@rewards,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"item",(long)item.UID,
                        "type",(int)item.ID,"stack",(int)item.StackSize,"bound",item.Bound,"cps",(long)cps,"silver",checked((long)silver),"rvp",(int)rvp,
                        "boundcps",(long)bound,"rewards",rewards.Select(r=>(long)r.UID).ToArray(),"now",now)!=1)
                        throw new InvalidOperationException("Daily sign-in pack receipt was not saved.");
                    if(!Ready(p))throw new InvalidOperationException("Daily sign-in pack session changed.");t.Commit();
                }}
                Q.Install(p,deplete?new[]{item.UID}:new uint[0],rewards);
                if(!deplete){item.StackSize--;item.Mode=Enums.ItemMode.Update;item.Send(p);item.Mode=Enums.ItemMode.Default;}
                if(cps!=0)p.Entity.ApplyCommittedConquerPoints((uint)nextCp);
                if(silver!=0)SilverBalance.ApplyCommitted(p,nextSilver);
                if(rvp!=0)p.Entity.ApplyCommittedRevoraPoints((uint)nextRvp);
                if(bound!=0)p.Entity.ApplyCommittedBoundCps((uint)nextBound);
                Hear(p,Text(item.ID));
            }catch(Exception ex){Q.Fail(p,"daily-sign-in-pack",ex);}return true;
        }
    }
}
