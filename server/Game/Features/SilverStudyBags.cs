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
    // Ordinary use of Way of Heroes silver/study bags and main-stage bound-CP packs.
    // Executable amounts are preserved even when catalog copy names a larger purse.
    internal static class SilverStudyBags
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static Action<string> Spoken;
        private static bool enabled;
        private sealed class Spec { internal ulong Silver; internal ushort Study; internal uint BoundCps; internal string Text; }
        private static readonly Dictionary<uint,Spec> Catalog=new Dictionary<uint,Spec>{
            {3005107,new Spec{BoundCps=20}},{3005108,new Spec{BoundCps=30}},
            {3005109,new Spec{BoundCps=50}},{3005110,new Spec{BoundCps=80}},
            {3005111,new Spec{BoundCps=100}},{3005112,new Spec{BoundCps=200}},
            {3005113,new Spec{BoundCps=300}},{3005114,new Spec{BoundCps=500}},
            {3005115,new Spec{BoundCps=800}},{3005116,new Spec{BoundCps=1000}},
            {3005117,new Spec{Silver=5000}},{3005118,new Spec{Silver=5000}},{3005119,new Spec{Silver=5000}},
            {3005120,new Spec{Silver=5000}},{3005121,new Spec{Silver=10000}},
            {3005122,new Spec{Silver=20000,Study=400,Text="You received 400 Study Points!"}},
            {3005123,new Spec{Study=200,Text="You received 200 Study Points!"}},
            {3005124,new Spec{Study=300,Text="You received 300 Study Points!"}},
            {3005125,new Spec{Study=500,Text="You received 500 Study Points!"}}
        };
        private static bool Handles(uint id){return Catalog.ContainsKey(id);}
        internal static void Initialize(){
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_silver_study_bag_uses(
id uuid PRIMARY KEY,uid bigint NOT NULL CHECK(uid>0),item_uid bigint NOT NULL,
item_type integer NOT NULL CHECK(item_type IN(3005117,3005118,3005119,3005120,3005121,3005122,3005123,3005124,3005125)),
before_stack integer NOT NULL CHECK(before_stack>=0),bound boolean NOT NULL,
silver bigint NOT NULL CHECK(silver>=0),study integer NOT NULL CHECK(study>=0),
used_at timestamptz NOT NULL);
ALTER TABLE revora_silver_study_bag_uses ADD COLUMN IF NOT EXISTS boundcps bigint NOT NULL DEFAULT 0 CHECK(boundcps>=0);
ALTER TABLE revora_silver_study_bag_uses DROP CONSTRAINT IF EXISTS revora_silver_study_bag_uses_item_type_check;
ALTER TABLE revora_silver_study_bag_uses ADD CONSTRAINT revora_silver_study_bag_uses_item_type_check CHECK(item_type BETWEEN 3005107 AND 3005125);");}enabled=true;
        }
        private static bool Ready(GameState p){return enabled && Q.Player(p) && p.Booth==null && p.Trade!=null && !p.Trade.InTrade;}
        private static void Tell(GameState p,string msg){p.Send(msg);var s=Spoken;if(s!=null)s(msg);}
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
                Spec spec=Catalog[item.ID];
                ulong nextSilver=p.Entity.Money;
                if(spec.Silver!=0){
                    if(p.Entity.Money>Entity.MaximumSilver-spec.Silver){Tell(p,"Spend some silver before opening this pack.");return true;}
                    nextSilver=p.Entity.Money+spec.Silver;
                }
                ulong nextBound=(ulong)p.Entity.BoundCps+spec.BoundCps;
                if(nextBound>uint.MaxValue){Tell(p,"Spend some bound CPs before opening this pack.");return true;}
                int beforeStudy=p.Entity.SubClasses.StudyPoints;
                int nextStudy=beforeStudy;
                if(spec.Study!=0){
                    if(beforeStudy>ushort.MaxValue-spec.Study){Tell(p,"Spend some Study Points before opening this pack.");return true;}
                    nextStudy=beforeStudy+spec.Study;
                }
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("Silver/study bag owner missing.");
                    if(spec.Silver!=0)SilverBalance.AssertSaved(c,t,p);
                    if(spec.Study!=0){
                        using(var q=Q.Command(c,t,"SELECT COALESCE(studypoints,0) FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID)){
                            var stored=q.ExecuteScalar();
                            if(stored==null || Convert.ToInt32(stored)!=beforeStudy)throw new InvalidOperationException("Study points changed.");
                        }
                    }
                    var ids=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader())while(r.Read()){if(ids.Count>=40)throw new InvalidOperationException("Silver/study bag inventory exceeds capacity.");ids.Add(checked((uint)Convert.ToInt64(r[0])));}
                    if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Silver/study bag inventory changed.");
                    string sql=item.StackSize<=1?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
                    if(Q.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=@type AND warehouse=0 AND position=0 AND stacksize=@stack AND bound=@bound
AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                        "item",(long)item.UID,"uid",(long)p.Entity.UID,"type",(int)item.ID,"stack",(int)item.StackSize,"bound",item.Bound?1:0,
                        "days",item.Days.ToString(CultureInfo.InvariantCulture),"permanent",item.Days==0,"stamp",item.DayStamp.ToBinary())!=1)
                        throw new InvalidOperationException("Silver/study bag ownership changed.");
                    if(spec.Silver!=0)SilverBalance.Persist(c,t,p,nextSilver);
                    if(spec.Study!=0 && Q.Execute(c,t,"UPDATE entities SET studypoints=@after WHERE uid=@uid AND COALESCE(studypoints,0)=@before",
                        "after",nextStudy,"before",beforeStudy,"uid",(long)p.Entity.UID)!=1)
                        throw new InvalidOperationException("Study points preimage changed.");
                    if(spec.BoundCps!=0 && Q.Execute(c,t,"UPDATE entities SET boundcps=@after WHERE uid=@uid AND COALESCE(boundcps,0)=@before",
                        "after",(long)nextBound,"before",(long)p.Entity.BoundCps,"uid",(long)p.Entity.UID)!=1)
                        throw new InvalidOperationException("Bound CP balance changed.");
                    if(Q.Execute(c,t,@"INSERT INTO revora_silver_study_bag_uses(id,uid,item_uid,item_type,before_stack,bound,silver,study,boundcps,used_at)
VALUES(@id,@uid,@item,@type,@stack,@bound,@silver,@study,@boundcps,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"item",(long)item.UID,
                        "type",(int)item.ID,"stack",(int)item.StackSize,"bound",item.Bound,"silver",checked((long)spec.Silver),"study",(int)spec.Study,"boundcps",(long)spec.BoundCps,"now",now)!=1)
                        throw new InvalidOperationException("Silver/study bag receipt was not saved.");
                    if(!Ready(p))throw new InvalidOperationException("Silver/study bag session changed.");t.Commit();
                }}
                Q.Install(p,item.StackSize<=1?new[]{item.UID}:new uint[0],new ConquerItem[0]);
                if(item.StackSize>1){item.StackSize--;item.Mode=Enums.ItemMode.Update;item.Send(p);item.Mode=Enums.ItemMode.Default;}
                if(spec.BoundCps!=0){p.Entity.ApplyCommittedBoundCps((uint)nextBound);Tell(p,"You received "+spec.BoundCps+" bound CPs.");}
                if(spec.Silver!=0)SilverBalance.ApplyCommitted(p,nextSilver);
                if(spec.Study!=0){
                    p.Entity.SubClasses.StudyPoints=(ushort)nextStudy;
                    p.Send(new SubClassShowFull(0){ID=8,Study=p.Entity.SubClasses.StudyPoints,StudyReceive=spec.Study}.ToArray());
                    var fx=new _String(true){Type=10,UID=p.Entity.UID};fx.Texts.Add("zf2-e300");p.SendScreen(fx.ToArray(),true);
                    p.Send(new Message(spec.Text,Message.System));var s=Spoken;if(s!=null)s(spec.Text);
                }
            }catch(Exception ex){Q.Fail(p,"silver-study-bag",ex);}return true;
        }
    }
}
