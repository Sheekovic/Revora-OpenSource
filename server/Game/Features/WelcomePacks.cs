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
    // Ordinary use of the JustCreated 723753 welcome chain. The parallel
    // 727026 starter pack stays a separate handler.
    internal static class WelcomePacks
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        private static bool enabled;
        private sealed class Gift { internal uint ID; internal byte Plus; internal bool Bound; }
        private static bool Handles(uint id){return id==723753 || id==723755 || id==723767 || id==723772 || id==723774 || id==723776;}
        internal static void Initialize(){
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_welcome_pack_uses(
id uuid PRIMARY KEY,uid bigint NOT NULL CHECK(uid>0),item_uid bigint NOT NULL,
item_type integer NOT NULL CHECK(item_type IN(723753,723755,723767,723772,723774,723776)),
before_stack integer NOT NULL CHECK(before_stack>=0),bound boolean NOT NULL,class integer NOT NULL,level integer NOT NULL,reborn integer NOT NULL,
cps bigint NOT NULL CHECK(cps>=0),silver bigint NOT NULL CHECK(silver>=0),rewards bigint[] NOT NULL,used_at timestamptz NOT NULL);");}enabled=true;
        }
        private static bool Ready(GameState p){return enabled && Q.Player(p) && p.Booth==null && p.Trade!=null && !p.Trade.InTrade;}
        private static byte MinLevel(uint id){return id==723776?(byte)140:id==723774?(byte)110:id==723772?(byte)100:id==723767?(byte)70:id==723755?(byte)10:(byte)1;}
        private static uint Cps(uint id){return id==723772?1000000u:id==723774?30u:id==723767?20u:id==723753?10u:0u;}
        private static ulong Silver(uint id){return id==723776?5000000UL:0UL;}
        private static void Add(List<Gift> list,uint id,byte plus,bool bound,int n){for(int i=0;i<n;i++)list.Add(new Gift{ID=id,Plus=plus,Bound=bound});}
        private static List<Gift> Outputs(uint id,byte cls){
            var list=new List<Gift>();
            if(id==723753){Add(list,1000000,0,false,3);Add(list,1001000,0,false,3);Add(list,723755,0,false,1);}
            else if(id==723755){Add(list,723700,0,false,10);Add(list,723017,0,false,3);Add(list,723767,0,false,1);}
            else if(id==723767){Add(list,723017,0,false,5);Add(list,1080001,0,false,1);Add(list,723772,0,false,1);}
            else if(id==723772){
                Add(list,723774,0,false,1);Add(list,723017,0,false,10);
                uint gem=cls<=15||(cls>45 && cls<=65)?800010u:cls<=25?800410u:cls<=45?800610u:800508u;
                Add(list,gem,0,false,1);
            }
            else if(id==723774){
                Add(list,723776,0,false,1);Add(list,721090,0,false,1);Add(list,723017,0,false,10);
                if(cls<=15){Add(list,614439,8,true,2);Add(list,118209,0,false,1);Add(list,130209,0,false,1);Add(list,160249,8,false,1);Add(list,120269,8,false,1);Add(list,150269,8,false,1);}
                else if(cls<=25){Add(list,560439,8,false,1);Add(list,111309,0,false,1);Add(list,131309,0,false,1);Add(list,160249,8,false,1);Add(list,120269,8,false,1);Add(list,150269,8,false,1);Add(list,900309,8,false,1);}
                else if(cls<=45){Add(list,613429,8,false,2);Add(list,113309,0,false,1);Add(list,133309,0,false,1);Add(list,160249,8,false,1);Add(list,120269,8,false,1);Add(list,150269,8,false,1);}
                else if(cls<=55){Add(list,616439,8,true,2);Add(list,112309,0,false,1);Add(list,135309,0,false,1);Add(list,160249,8,false,1);Add(list,120269,8,false,1);Add(list,150269,8,false,1);}
                else Add(list,160249,8,false,1);
                Add(list,120269,8,false,1);Add(list,150269,8,false,1);
            }
            else {Add(list,3009003,0,true,5);}
            return list;
        }
        private static ConquerItem Item(Gift g){
            ushort dur=1;
            if(ConquerItemInformation.BaseInformations.ContainsKey(g.ID))dur=ConquerItemInformation.BaseInformations[g.ID].Durability;
            return new ConquerItem(true){ID=g.ID,UID=ConquerItem.ItemUID.Next,Plus=g.Plus,Bound=g.Bound,StackSize=1,Durability=dur,MaximDurability=dur};
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
                if(p.Entity.Level<MinLevel(item.ID) || (item.ID==723776 && p.Entity.Reborn!=2)){
                    p.Send(item.ID==723776?"You must be atleast level 140 - 2 rebon":"You must be atleast level "+MinLevel(item.ID).ToString(CultureInfo.InvariantCulture));return true;
                }
                var gifts=Outputs(item.ID,p.Entity.Class);int spaces=gifts.Count-(item.StackSize<=1?1:0);
                if(p.Inventory.Count+spaces>40){p.Send("Make room for this welcome pack.");return true;}
                uint cps=Cps(item.ID);ulong silver=Silver(item.ID);
                long nextCp=(long)p.Entity.ConquerPoints+cps;
                if(cps!=0 && nextCp>uint.MaxValue){p.Send("Spend some CPs before opening this pack.");return true;}
                ulong nextSilver=p.Entity.Money+silver;
                if(silver!=0 && (nextSilver<silver || nextSilver>Entity.MaximumSilver)){p.Send("Store some silver before opening this pack.");return true;}
                var rewards=gifts.Select(Item).ToArray();
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("Welcome pack owner missing.");
                    if(silver!=0)SilverBalance.AssertSaved(c,t,p);
                    var ids=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader())while(r.Read()){if(ids.Count>=40)throw new InvalidOperationException("Welcome pack inventory exceeds capacity.");ids.Add(checked((uint)Convert.ToInt64(r[0])));}
                    if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Welcome pack inventory changed.");
                    string sql=item.StackSize<=1?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
                    if(Q.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=@type AND warehouse=0 AND position=0 AND stacksize=@stack AND bound=@bound
AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                        "item",(long)item.UID,"uid",(long)p.Entity.UID,"type",(int)item.ID,"stack",(int)item.StackSize,"bound",item.Bound?1:0,
                        "days",item.Days.ToString(CultureInfo.InvariantCulture),"permanent",item.Days==0,"stamp",item.DayStamp.ToBinary())!=1)
                        throw new InvalidOperationException("Welcome pack ownership changed.");
                    foreach(var reward in rewards)
                        if(Q.Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,plus,durability,maximdurability)
VALUES(@item,@type,@uid,0,0,1,@bound,@plus,@dur,@max)","item",(long)reward.UID,"type",(int)reward.ID,"uid",(long)p.Entity.UID,
                            "bound",reward.Bound?1:0,"plus",(int)reward.Plus,"dur",(int)reward.Durability,"max",(int)reward.MaximDurability)!=1)
                            throw new InvalidOperationException("Welcome pack item was not saved.");
                    if(cps!=0 && Q.Execute(c,t,@"UPDATE entities SET conquerpoints=@cp WHERE uid=@uid AND COALESCE(conquerpoints,0)=@old",
                        "cp",nextCp,"uid",(long)p.Entity.UID,"old",(long)p.Entity.ConquerPoints)!=1)
                        throw new InvalidOperationException("Welcome pack CP balance changed.");
                    if(silver!=0)SilverBalance.Persist(c,t,p,nextSilver);
                    if(Q.Execute(c,t,@"INSERT INTO revora_welcome_pack_uses(id,uid,item_uid,item_type,before_stack,bound,class,level,reborn,cps,silver,rewards,used_at)
VALUES(@id,@uid,@item,@type,@stack,@bound,@class,@level,@reborn,@cps,@silver,@rewards,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"item",(long)item.UID,
                        "type",(int)item.ID,"stack",(int)item.StackSize,"bound",item.Bound,"class",(int)p.Entity.Class,"level",(int)p.Entity.Level,"reborn",(int)p.Entity.Reborn,
                        "cps",(long)cps,"silver",(long)silver,"rewards",rewards.Select(r=>(long)r.UID).ToArray(),"now",now)!=1)
                        throw new InvalidOperationException("Welcome pack receipt was not saved.");
                    if(!Ready(p))throw new InvalidOperationException("Welcome pack session changed.");t.Commit();
                }}
                Q.Install(p,item.StackSize<=1?new[]{item.UID}:new uint[0],rewards);
                if(item.StackSize>1){item.StackSize--;item.Mode=Enums.ItemMode.Update;item.Send(p);item.Mode=Enums.ItemMode.Default;}
                if(cps!=0)p.Entity.ApplyCommittedConquerPoints((uint)nextCp);
                if(silver!=0)SilverBalance.ApplyCommitted(p,nextSilver);
            }catch(Exception ex){Q.Fail(p,"welcome-pack",ex);}return true;
        }
    }
}
