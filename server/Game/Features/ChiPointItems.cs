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
    // Ordinary Chi consumers. Six have a current producer. Five catalog pills are
    // consumer-only: DropItemTable is not activated and no stock is added.
    // SalesPamphlet stays outside this catalog. 729479 reports the committed 400, not 5.
    internal static class ChiPointItems
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static Action<string> Spoken;
        internal static Action<GameState> BeforeInstall;
        private static bool enabled;
        private sealed class Spec { internal uint Chi; internal string Text; internal bool NamedRed; }
        private static readonly Dictionary<uint,Spec> Catalog=new Dictionary<uint,Spec>{
            {729481,new Spec{Chi=500,Text="You received 500 Chi Points!",NamedRed=true}},
            {729242,new Spec{Chi=500,Text="You recieved 500 Chi Points",NamedRed=true}},
            {3005126,new Spec{Chi=500,Text="You received 500 Chi Points!",NamedRed=false}},
            {3005127,new Spec{Chi=1000,Text="You received 1000 Chi Points!",NamedRed=false}},
            {3005128,new Spec{Chi=2000,Text="You received 2000 Chi Points!",NamedRed=false}},
            {3005129,new Spec{Chi=3000,Text="You received 3000 Chi Points!",NamedRed=false}},
            {3000051,new Spec{Chi=5,Text="You received 5 Chi Points!",NamedRed=true}},
            {729476,new Spec{Chi=100,Text="You received 100 Chi Points!",NamedRed=false}},
            {729477,new Spec{Chi=200,Text="You received 200 Chi Points!",NamedRed=true}},
            {729478,new Spec{Chi=300,Text="You received 300 Chi Points!",NamedRed=true}},
            {729479,new Spec{Chi=400,Text="You received 400 Chi Points!",NamedRed=true}}
        };
        private static bool Handles(uint id){return Catalog.ContainsKey(id);}
        internal static void Initialize(){
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_chi_item_uses(
id uuid PRIMARY KEY,uid bigint NOT NULL CHECK(uid>0),item_uid bigint NOT NULL,
item_type integer NOT NULL,
before_stack integer NOT NULL CHECK(before_stack>=0),bound boolean NOT NULL,
chi bigint NOT NULL CHECK(chi>0),chi_before bigint NOT NULL CHECK(chi_before>=0),chi_after bigint NOT NULL CHECK(chi_after>=chi),
used_at timestamptz NOT NULL);
ALTER TABLE revora_chi_item_uses DROP CONSTRAINT IF EXISTS revora_chi_item_uses_item_type_check;
ALTER TABLE revora_chi_item_uses ADD CONSTRAINT revora_chi_item_uses_item_type_check CHECK(item_type IN (729481,729242,3005126,3005127,3005128,3005129,3000051,729476,729477,729478,729479));");}enabled=true;
        }
        private static bool Ready(GameState p){return enabled && Q.Player(p) && ChiWallet.Ready(p) && p.Booth==null && p.Trade!=null && !p.Trade.InTrade;}
        private static void Tell(GameState p,string msg){p.Send(msg);var s=Spoken;if(s!=null)s(msg);}
        private static void Speak(GameState p,Spec spec){
            if(spec.NamedRed)p.Send(new Message(spec.Text,Color.Red,Message.System));
            else p.Send(new Message(spec.Text,Message.System));
            var s=Spoken;if(s!=null)s(spec.Text);
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
                Spec spec=Catalog[item.ID];uint before=p.ChiPoints;
                if(!ChiWallet.Matches(p,before)){Tell(p,"Your Chi is still being saved. Try again shortly.");return true;}
                if(before>uint.MaxValue-spec.Chi){Tell(p,"Spend some Chi before using this item.");return true;}
                uint after=before+spec.Chi;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("Chi item owner missing.");
                    using(var q=Q.Command(c,t,"SELECT points FROM chi WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID)){
                        var stored=q.ExecuteScalar();
                        if(stored==null || stored==DBNull.Value)throw new InvalidOperationException("Chi item wallet missing.");
                        if(Convert.ToUInt32(stored)!=before)throw new InvalidOperationException("Chi item wallet changed.");
                    }
                    var ids=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader())while(r.Read()){if(ids.Count>=40)throw new InvalidOperationException("Chi item inventory exceeds capacity.");ids.Add(checked((uint)Convert.ToInt64(r[0])));}
                    if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Chi item inventory changed.");
                    string sql=item.StackSize<=1?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
                    if(Q.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=@type AND warehouse=0 AND position=0 AND stacksize=@stack AND bound=@bound
AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                        "item",(long)item.UID,"uid",(long)p.Entity.UID,"type",(int)item.ID,"stack",(int)item.StackSize,"bound",item.Bound?1:0,
                        "days",item.Days.ToString(CultureInfo.InvariantCulture),"permanent",item.Days==0,"stamp",item.DayStamp.ToBinary())!=1)
                        throw new InvalidOperationException("Chi item ownership changed.");
                    if(Q.Execute(c,t,"UPDATE chi SET points=@after WHERE uid=@uid AND points=@before",
                        "after",(long)after,"before",(long)before,"uid",(long)p.Entity.UID)!=1)
                        throw new InvalidOperationException("Chi item wallet preimage changed.");
                    if(Q.Execute(c,t,@"INSERT INTO revora_chi_item_uses(id,uid,item_uid,item_type,before_stack,bound,chi,chi_before,chi_after,used_at)
VALUES(@id,@uid,@item,@type,@stack,@bound,@chi,@before,@after,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"item",(long)item.UID,
                        "type",(int)item.ID,"stack",(int)item.StackSize,"bound",item.Bound,"chi",(long)spec.Chi,"before",(long)before,"after",(long)after,"now",now)!=1)
                        throw new InvalidOperationException("Chi item receipt was not saved.");
                    if(!Ready(p) || !ChiWallet.Matches(p,before))throw new InvalidOperationException("Chi item session changed.");t.Commit();
                }}
                var beforeInstall=BeforeInstall;if(beforeInstall!=null)beforeInstall(p);
                Q.Install(p,item.StackSize<=1?new[]{item.UID}:new uint[0],new ConquerItem[0]);
                if(item.StackSize>1){item.StackSize--;item.Mode=Enums.ItemMode.Update;item.Send(p);item.Mode=Enums.ItemMode.Default;}
                ChiWallet.Committed(p,before,after);
                Speak(p,spec);
            }catch(Exception ex){Q.Fail(p,"chi-item",ex);}return true;
        }
    }
}
