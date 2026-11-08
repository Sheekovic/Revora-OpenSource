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
    internal static class ChiTokenCash
    {
        internal const uint Token=729304;
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        private static bool enabled;
        private sealed class Recent {internal DateTime Until;}
        private sealed class Debit {internal ConquerItem Item;internal ushort Before;internal int Count;}
        private static readonly ConditionalWeakTable<GameState,Recent> recent=new ConditionalWeakTable<GameState,Recent>();
        internal static void Initialize() {
            if(!ConquerItemInformation.BaseInformations.ContainsKey(Token))throw new InvalidOperationException("Chi Token catalog is missing.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_chi_token_cash(
id uuid PRIMARY KEY,uid bigint NOT NULL CHECK(uid>0),selected_uid bigint NOT NULL,
source_uids bigint[] NOT NULL CHECK(cardinality(source_uids)=7),before_stacks integer[] NOT NULL CHECK(cardinality(before_stacks)=7),
source_bound boolean[] NOT NULL CHECK(cardinality(source_bound)=7),cp bigint NOT NULL CHECK(cp BETWEEN 0 AND 4294967295),
rvp bigint NOT NULL CHECK(rvp BETWEEN 0 AND 4294967295),redeemed_at timestamptz NOT NULL);");}enabled=true;
        }
        private static bool Ready(GameState p) {return enabled && Q.Player(p) && p.Booth==null && p.Trade!=null && !p.Trade.InTrade;}
        private static bool Usable(ConquerItem item,DateTime now) {
            return item!=null && item.ID==Token && item.Position==0 && item.Warehouse==0 && item.Lock==0 && !item.Suspicious && !item.InWardrobe
                && (item.Days==0?item.TimeLeftInMinutes==0:now.ToLocalTime()<item.DayStamp.AddDays(item.Days));
        }
        internal static bool TryHandle(GameState p,byte[] packet) {
            ConquerItem item;
            if(p==null || p.Inventory==null || packet==null || packet.Length<12 || !p.Inventory.TryGetItem(BitConverter.ToUInt32(packet,8),out item) || item.ID!=Token)return false;
            if(packet.Length<32 || packet.Length>128 || BitConverter.ToUInt16(packet,0)+8!=packet.Length || BitConverter.ToUInt16(packet,2)!=1009
                || BitConverter.ToUInt16(packet,20)!=4 || BitConverter.ToUInt64(packet,packet.Length-8)!=Revora.Network.PacketHandler.ClientSeal)return true;
            TryUse(item,p);return true;
        }
        internal static bool TryUse(ConquerItem item,GameState p) {
            if(item==null || item.ID!=Token)return false;if(!Ready(p))return true;
            lock(p.ItemSyncRoot)try {
                ConquerItem owned;var now=Clock();var history=recent.GetValue(p,_=>new Recent());
                if(!Ready(p) || now<history.Until || !p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(item,owned) || !Usable(item,now))return true;
                var debits=new List<Debit>();int remaining=7;
                foreach(var source in p.Inventory.Objects.Where(i=>Usable(i,now)).OrderBy(i=>i.UID==item.UID?0:1).ThenBy(i=>i.UID)) {
                    int count=Math.Min(remaining,Math.Max(1,(int)source.StackSize));debits.Add(new Debit{Item=source,Before=source.StackSize,Count=count});remaining-=count;if(remaining==0)break;
                }
                if(remaining!=0){p.Send("Gather seven Chi Tokens, then use one to claim your reward.");return true;}
                uint rewardCp=Config.ChiTokenCashCp,rewardRvp=Config.ChiTokenCashRevoraPoints;
                long cp=(long)p.Entity.ConquerPoints+rewardCp,rvp=(long)p.Entity.RevoraPoint+rewardRvp;
                if(cp>uint.MaxValue || rvp>uint.MaxValue){p.Send("Spend some points before redeeming your Chi Tokens.");return true;}
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("Chi Token owner missing.");
                    var ids=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader())while(r.Read()){if(ids.Count>=40)throw new InvalidOperationException("Chi Token inventory exceeds capacity.");ids.Add(checked((uint)Convert.ToInt64(r[0])));}
                    if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Chi Token inventory changed.");
                    foreach(var debit in debits) {
                        var source=debit.Item;string sql=debit.Count==Math.Max(1,(int)debit.Before)?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-@count";
                        if(Q.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=@type AND warehouse=0 AND position=0 AND stacksize=@stack AND bound=@bound
AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                            "count",debit.Count,"item",(long)source.UID,"uid",(long)p.Entity.UID,"type",(int)Token,"stack",(int)debit.Before,"bound",source.Bound?1:0,
                            "days",source.Days.ToString(CultureInfo.InvariantCulture),"permanent",source.Days==0,"stamp",source.DayStamp.ToBinary())!=1)
                            throw new InvalidOperationException("Chi Token ownership changed.");
                    }
                    if(Q.Execute(c,t,@"UPDATE entities SET conquerpoints=@cp,revorapoint=@rvp WHERE uid=@uid
AND COALESCE(conquerpoints,0)=@beforecp AND COALESCE(revorapoint,0)=@beforervp", "cp",cp,"rvp",rvp,"uid",(long)p.Entity.UID,
                        "beforecp",(long)p.Entity.ConquerPoints,"beforervp",(long)p.Entity.RevoraPoint)!=1)throw new InvalidOperationException("Chi Token balance changed.");
                    var units=debits.SelectMany(d=>Enumerable.Repeat(d,d.Count)).ToArray();
                    if(Q.Execute(c,t,@"INSERT INTO revora_chi_token_cash(id,uid,selected_uid,source_uids,before_stacks,source_bound,cp,rvp,redeemed_at)
VALUES(@id,@uid,@selected,@sources,@stacks,@bound,@cp,@rvp,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"selected",(long)item.UID,
                        "sources",units.Select(d=>(long)d.Item.UID).ToArray(),"stacks",units.Select(d=>(int)d.Before).ToArray(),"bound",units.Select(d=>d.Item.Bound).ToArray(),
                        "cp",(long)rewardCp,"rvp",(long)rewardRvp,"now",now)!=1)throw new InvalidOperationException("Chi Token receipt was not saved.");
                    var end=Clock();if(!Ready(p) || debits.Any(d=>!Usable(d.Item,end)))throw new InvalidOperationException("Chi Token session changed.");t.Commit();
                }}
                history.Until=now.AddSeconds(1);
                Q.Install(p,debits.Where(d=>d.Count==Math.Max(1,(int)d.Before)).Select(d=>d.Item.UID),new ConquerItem[0]);
                foreach(var debit in debits.Where(d=>d.Count<d.Before)) {debit.Item.StackSize=(ushort)(debit.Before-debit.Count);debit.Item.Mode=Enums.ItemMode.Update;debit.Item.Send(p);debit.Item.Mode=Enums.ItemMode.Default;}
                p.Entity.ApplyCommittedConquerPoints((uint)cp);p.Entity.ApplyCommittedRevoraPoints((uint)rvp);
                p.Send("Your seven Chi Tokens earned "+rewardCp+" CPs and "+rewardRvp+" RVPs.");
            }catch(Exception ex){Q.Fail(p,"chi-token-cash",ex);}return true;
        }
    }
}
