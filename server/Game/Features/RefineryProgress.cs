using System;
using System.Collections.Generic;
using System.Linq;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class RefineryProgress
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        private static readonly int[] Costs={0,10,30,70,150,270,430};
        internal sealed class State
        {
            internal ItemAdding.Refinery_ Permanent,Temporary;
            internal int Credit;
            internal ItemAdding.Refinery_ Current(DateTime now) {
                return Temporary.EffectID!=0 && now<Temporary.AddedOn.AddSeconds(Temporary.EffectDuration)?Temporary:Permanent;
            }
        }
        internal static int Cost(uint level){return level>=1 && level<=6?Costs[level]:0;}
        internal static uint Duration(byte vip){return (uint)(vip==0?7:vip==1?8:vip==2?9:vip==3?11:14)*86400;}
        private static bool Eligible(GameState p){return Q.Player(p) && p.Equipment!=null && p.Booth==null && p.Trade!=null && !p.Trade.InTrade;}
        private static ConquerItem Find(GameState p,uint uid) {
            ConquerItem item;if(!p.Inventory.TryGetItem(uid,out item))item=p.Equipment.TryGetItem(uid);
            if(item==null || item.UID!=uid || item.Warehouse!=0 || item.InWardrobe || item.StackSize>1)return null;
            return item;
        }
        internal static bool Fits(ConquerItem item,Refinery.RefineryItem material) {
            int pos=(int)ItemHandler.GetPositionFromID(item.ID);uint family=item.ID/1000;
            if(pos!=1 && pos!=2 && pos!=3 && pos!=4 && pos!=5 && pos!=6 && pos!=8)return false;
            if(material.Position==family)return true;
            if(material.Position==4)return pos==4 && family!=500 && !item.IsTwoHander();
            if(material.Position==5)return family>=510 && family<=580;
            return material.Position==(uint)pos;
        }
        internal static State Read(NpgsqlConnection c,NpgsqlTransaction t,uint uid) {
            var result=new State();
            using(var q=Q.Command(c,t,@"SELECT addingid,addinglevel,addingpercent,duration,addedon,stabilizationpoints
FROM itemadding WHERE uid=@uid AND addingtype=1 ORDER BY iditemadding FOR UPDATE","uid",(long)uid))
            using(var r=q.ExecuteReader())while(r.Read()) {
                var effect=new ItemAdding.Refinery_{Available=true,ItemUID=uid,EffectID=Convert.ToUInt32(r.GetValue(0)),
                    EffectLevel=Convert.ToUInt32(r.GetValue(1)),EffectPercent=Convert.ToUInt32(r.GetValue(2)),
                    EffectDuration=Convert.ToUInt32(r.GetValue(3)),AddedOn=DateTime.FromBinary(Convert.ToInt64(r.GetValue(4)))};
                Refinery.RefineryItem known;
                if(!Kernel.DatabaseRefinery.TryGetValue(effect.EffectID,out known) || known.Level!=effect.EffectLevel || known.Percent!=effect.EffectPercent || Cost(effect.EffectLevel)==0)
                    throw new InvalidOperationException("Unrecognized saved refinery effect.");
                result.Credit=Math.Max(result.Credit,Convert.ToInt32(r.GetValue(5)));
                if(effect.EffectDuration==0) {
                    if(result.Permanent.EffectID!=0)throw new InvalidOperationException("Duplicate permanent refinery effect.");
                    result.Permanent=effect;result.Credit=Math.Max(result.Credit,Cost(effect.EffectLevel));
                }else {
                    if(result.Temporary.EffectID!=0)throw new InvalidOperationException("Duplicate temporary refinery effect.");
                    result.Temporary=effect;
                }
            }
            return result;
        }
        private static void Own(NpgsqlConnection c,NpgsqlTransaction t,GameState p,ConquerItem item) {
            using(var q=Q.Command(c,t,@"SELECT uid FROM items WHERE uid=@item AND entityid=@uid AND id=@type AND position=@pos
AND warehouse=0 AND COALESCE(inwardrobe,0)=0 AND locked=@locked AND stacksize=@stack AND bound=@bound FOR UPDATE","item",(long)item.UID,"uid",(long)p.Entity.UID,
                "type",(int)item.ID,"pos",(int)item.Position,"locked",(int)item.Lock,"stack",(int)item.StackSize,"bound",item.Bound?1:0))
                if(q.ExecuteScalar()==null)throw new InvalidOperationException("Refinery item ownership changed.");
        }
        private static void Owner(NpgsqlConnection c,NpgsqlTransaction t,GameState p) {
            using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                if(q.ExecuteScalar()==null)throw new InvalidOperationException("Refinery owner missing.");
        }
        private static void Save(NpgsqlConnection c,NpgsqlTransaction t,ConquerItem item,State state,DateTime now) {
            if(state.Temporary.EffectID!=0 && now>=state.Temporary.AddedOn.AddSeconds(state.Temporary.EffectDuration))state.Temporary=default(ItemAdding.Refinery_);
            long previous;using(var q=Q.Command(c,t,"SELECT count(*) FROM itemadding WHERE uid=@uid AND addingtype=1","uid",(long)item.UID))previous=Convert.ToInt64(q.ExecuteScalar());
            if(Q.Execute(c,t,"DELETE FROM itemadding WHERE uid=@uid AND addingtype=1","uid",(long)item.UID)!=previous)
                throw new InvalidOperationException("Refinery previous effect was not replaced.");
            foreach(var effect in new[]{state.Permanent,state.Temporary}.Where(e=>e.EffectID!=0))if(Q.Execute(c,t,@"INSERT INTO itemadding
(uid,addingtype,addingid,addinglevel,addingpercent,duration,addedon,stabilizationpoints) VALUES(@uid,1,@id,@level,@percent,@duration,@time,@credit)",
                "uid",(long)item.UID,"id",(long)effect.EffectID,"level",(long)effect.EffectLevel,"percent",(long)effect.EffectPercent,
                "duration",(long)effect.EffectDuration,"time",effect.AddedOn.Ticks,"credit",(long)state.Credit)!=1)
                throw new InvalidOperationException("Refinery effect was not saved.");
            var active=state.Current(now);long expiry=active.EffectDuration==0?0:active.AddedOn.AddSeconds(active.EffectDuration).Ticks;
            if(Q.Execute(c,t,"UPDATE items SET refineryitem=@id,refinerytime=@time WHERE uid=@uid","uid",(long)item.UID,"id",(long)active.EffectID,"time",expiry)!=1)
                throw new InvalidOperationException("Refinery target disappeared.");
        }
        internal static void Project(ConquerItem item,State state) {
            var current=state.Current(Clock().ToLocalTime());item.RefineryState=state;item.ExtraEffect=current;item.RefineItem=current.EffectID;
            item.RefineryTime=current.EffectDuration==0?new DateTime(0):current.AddedOn.AddSeconds(current.EffectDuration);
        }
        private static void Updated(GameState p,ConquerItem item,State state) {
            uint previous=item.RefineItem;Project(item,state);
            if(previous!=0 && item.RefineItem==0)new ItemSoul{Identifier=item.UID}.Expired(p);
            item.Mode=Enums.ItemMode.Update;item.Send(p);
            if(item.Position!=0){p.LoadItemStats();p.CalculateHPBonus();PrestigeRanking.SendSelf(p);}
            p.Send(new WindowStats(p).ToArray());
        }
        private static bool Frame(byte[] packet,int min,int max) {
            return packet!=null && packet.Length>=min && packet.Length<=max && BitConverter.ToUInt16(packet,0)==packet.Length-8 && packet[4]==0;
        }
        internal static void Apply(GameState p,byte[] packet) {
            if(!Eligible(p) || !Frame(packet,24,128) || BitConverter.ToUInt16(packet,2)!=2076)return;
            uint uid=BitConverter.ToUInt32(packet,8),source=BitConverter.ToUInt32(packet,12);if(uid==source)return;
            lock(p.ItemSyncRoot)try {
                if(!Eligible(p))return;var item=Find(p,uid);ConquerItem material;Refinery.RefineryItem info;
                if(item==null || !p.Inventory.TryGetItem(source,out material) || material.UID!=source || material.Position!=0 || material.Warehouse!=0
                    || material.Lock!=0 || material.InWardrobe || !Kernel.DatabaseRefinery.TryGetValue(material.ID,out info) || Cost(info.Level)==0 || !Fits(item,info))return;
                if((material.Bound || info.Untradable) && !item.Bound){p.Send("Use this bound material on bound equipment.");return;}
                var now=Clock().ToLocalTime();State state;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    Owner(c,t,p);foreach(var owned in new[]{item,material}.OrderBy(i=>i.UID))Own(c,t,p,owned);
                    state=Read(c,t,uid);
                    if(state.Temporary.EffectID==info.Identifier && now<state.Temporary.AddedOn.AddSeconds(1))return;
                    state.Temporary=new ItemAdding.Refinery_{Available=true,ItemUID=uid,EffectID=info.Identifier,EffectLevel=info.Level,
                        EffectPercent=info.Percent,EffectDuration=Duration(p.Entity.VIPLevel),AddedOn=now};
                    Save(c,t,item,state,now);
                    string consume=material.StackSize<=1?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
                    if(Q.Execute(c,t,consume+" WHERE uid=@uid AND entityid=@owner","uid",(long)source,"owner",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Refinery material changed.");
                    ProgressionEvidence.Complete(c,t,p.Entity.UID,10431,"refinery-apply",uid+"-"+source,1);
                    t.Commit();
                }}
                if(material.StackSize<=1)Q.Install(p,new[]{source},new ConquerItem[0]);
                else {material.StackSize--;material.Mode=Enums.ItemMode.Update;material.Send(p);}
                Updated(p,item,state);Q.Refresh(p);
            }catch(Exception ex){Q.Fail(p,"refinery-apply",ex);}
        }
        internal static void Stabilize(GameState p,byte[] packet) {
            if(!Eligible(p) || !Frame(packet,24,184) || BitConverter.ToUInt16(packet,2)!=PacketID.ItemAdding)return;
            uint count=BitConverter.ToUInt32(packet,12);if(count>40 || packet.Length!=24+4*count)return;
            uint uid=BitConverter.ToUInt32(packet,8);var selected=new HashSet<uint>();
            for(int i=0;i<count;i++){uint id=BitConverter.ToUInt32(packet,16+4*i);if(id==0 || id==uid || !selected.Add(id))return;}
            lock(p.ItemSyncRoot)try {
                if(!Eligible(p))return;var item=Find(p,uid);if(item==null)return;
                var stones=new List<ConquerItem>();int points=0;
                foreach(uint id in selected) {
                    ConquerItem stone;if(!p.Inventory.TryGetItem(id,out stone) || stone.UID!=id || stone.Position!=0 || stone.Warehouse!=0 || stone.Lock!=0
                        || stone.ID!=723694 && stone.ID!=723695 || stone.Bound && !item.Bound)return;
                    stones.Add(stone);points+=stone.ID==723694?10:100;
                }
                var now=Clock().ToLocalTime();State state;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    Owner(c,t,p);foreach(var owned in stones.Concat(new[]{item}).OrderBy(i=>i.UID))Own(c,t,p,owned);
                    state=Read(c,t,uid);var effect=state.Current(now);
                    if(effect.EffectID==0 || effect.EffectDuration==0)return;
                    int need=Math.Max(0,Cost(effect.EffectLevel)-state.Credit);
                    if(points<need){p.Send("This refinery needs "+need+" more stabilization points.");return;}
                    if(need==0)stones.Clear();
                    effect.EffectDuration=0;state.Permanent=effect;state.Temporary=default(ItemAdding.Refinery_);state.Credit=Math.Max(state.Credit,Cost(effect.EffectLevel));
                    Save(c,t,item,state,now);
                    foreach(var stone in stones) {
                        string sql=stone.StackSize<=1?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
                        if(Q.Execute(c,t,sql+" WHERE uid=@uid AND entityid=@owner AND stacksize=@stack","uid",(long)stone.UID,"owner",(long)p.Entity.UID,"stack",(int)stone.StackSize)!=1)
                            throw new InvalidOperationException("Refinery stabilization material changed.");
                    }
                    t.Commit();
                }}
                Q.Install(p,stones.Where(i=>i.StackSize<=1).Select(i=>i.UID),new ConquerItem[0]);
                foreach(var stone in stones.Where(i=>i.StackSize>1)){stone.StackSize--;stone.Mode=Enums.ItemMode.Update;stone.Send(p);}
                Updated(p,item,state);var effectPacket=new ItemAdding(true){Type=ItemAdding.StabilizationEffectRefined};effectPacket.Append2(item.ExtraEffect);p.Send(effectPacket);
            }catch(Exception ex){Q.Fail(p,"refinery-stabilize",ex);}
        }
        internal static void Expire(GameState p,ConquerItem item) {
            if(!Q.Player(p,false) || item.RefineryState==null || item.RefineryState.Temporary.EffectID==0
                || Clock().ToLocalTime()<item.RefineryState.Temporary.AddedOn.AddSeconds(item.RefineryState.Temporary.EffectDuration))return;
            lock(p.ItemSyncRoot)try {
                if(!Q.Player(p,false) || !ReferenceEquals(Find(p,item.UID),item))return;State state;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    Owner(c,t,p);Own(c,t,p,item);state=Read(c,t,item.UID);Save(c,t,item,state,Clock().ToLocalTime());t.Commit();
                }}
                Updated(p,item,state);
            }catch(Exception ex){Q.Fail(p,"refinery-expire",ex);}
        }
        internal static void Load(ConquerItem item) {
            using(var c=DataHolder.MySqlConnection){c.Open();var state=Read(c,null,item.UID);
                if(state.Permanent.EffectID!=0 || state.Temporary.EffectID!=0 || item.RefineItem==0)Project(item,state);
            }
        }
        private static DateTime nextScan;
        internal static void Tick() {
            var now=Clock();if(now<nextScan)return;nextScan=now.AddSeconds(1);
            foreach(var p in Kernel.GamePool.Values)if(Q.Player(p,false) && p.Equipment!=null)foreach(var item in p.Inventory.Objects.Concat(p.Equipment.Objects.Where(i=>i!=null)).ToArray())Expire(p,item);
        }
    }
}
