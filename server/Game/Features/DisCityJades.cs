using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class DisCityJades
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        private sealed class Recent {internal readonly Dictionary<uint,DateTime> Items=new Dictionary<uint,DateTime>();}
        private static readonly ConditionalWeakTable<GameState,Recent> recent=new ConditionalWeakTable<GameState,Recent>();
        internal static void Initialize() {
            if(!ConquerItemInformation.BaseInformations.ContainsKey(3008059) || !ConquerItemInformation.BaseInformations.ContainsKey(3008060))
                throw new InvalidOperationException("Dis City jade catalog missing.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_dis_jade_uses(
id uuid PRIMARY KEY,uid bigint NOT NULL,jade_uid bigint NOT NULL,jade_type integer NOT NULL,jade_stack integer NOT NULL,
partner_uid bigint NOT NULL,partner_stack integer NOT NULL,chi_before bigint NOT NULL,chi_after bigint NOT NULL,
used_at timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP);");}
        }
        private static bool Usable(ConquerItem item,DateTime now) {
            return item.Position==0 && item.Warehouse==0 && item.Lock==0 && !item.InWardrobe
                && (item.Days==0 || now.ToLocalTime()<item.DayStamp.AddDays(item.Days));
        }
        internal static bool TryUse(ConquerItem item,GameState p) {
            if(item==null || item.ID<3008059 || item.ID>3008060)return false;
            if(!Q.Player(p) || !ChiWallet.Ready(p) || p.Booth!=null || p.Trade==null || p.Trade.InTrade)return true;
            lock(p.ItemSyncRoot)try {
                ConquerItem owned;var now=Clock();
                if(!Q.Player(p) || p.Booth!=null || p.Trade==null || p.Trade.InTrade || !p.Inventory.TryGetItem(item.UID,out owned)
                    || !ReferenceEquals(owned,item) || !Usable(item,now))return true;
                uint before=p.ChiPoints;
                if(before>=8000){p.Send("Your Chi is too full to draw from this jade.");return true;}
                if(!ChiWallet.Matches(p,before)){p.Send("Your Chi is still being saved. Try again shortly.");return true;}
                var history=recent.GetValue(p,_=>new Recent());
                foreach(uint uid in history.Items.Where(i=>i.Value<=now).Select(i=>i.Key).ToArray())history.Items.Remove(uid);
                if(history.Items.ContainsKey(item.UID))return true;
                uint opposite=item.ID==3008059?3008060u:3008059u;
                var partner=p.Inventory.Objects.Where(i=>i.ID==opposite && Usable(i,now)).OrderBy(i=>i.Bound==item.Bound?0:1).ThenBy(i=>i.UID).FirstOrDefault();
                if(partner!=null && history.Items.ContainsKey(partner.UID))return true;
                var consumed=partner==null?new[]{item}:new[]{item,partner};uint after=before+(partner==null?300u:1200u);
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("Jade owner missing.");
                    if(Q.Execute(c,t,"UPDATE chi SET points=@after WHERE uid=@uid AND points=@before","after",(long)after,
                        "uid",(long)p.Entity.UID,"before",(long)before)!=1)throw new InvalidOperationException("Jade Chi preimage changed.");
                    foreach(var material in consumed) {
                        string sql=material.StackSize<=1?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
                        if(Q.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=@type AND warehouse=0 AND position=0
AND stacksize=@stack AND bound=@bound AND locked=0","item",(long)material.UID,"uid",(long)p.Entity.UID,"type",(int)material.ID,
                            "stack",(int)material.StackSize,"bound",material.Bound?1:0)!=1)throw new InvalidOperationException("Jade ownership changed.");
                    }
                    Q.Execute(c,t,@"INSERT INTO revora_dis_jade_uses(id,uid,jade_uid,jade_type,jade_stack,partner_uid,partner_stack,chi_before,chi_after)
VALUES(@id,@uid,@jade,@type,@stack,@partner,@partner_stack,@before,@after)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,
                        "jade",(long)item.UID,"type",(int)item.ID,"stack",(int)item.StackSize,"partner",partner==null?0L:(long)partner.UID,
                        "partner_stack",partner==null?0:(int)partner.StackSize,"before",(long)before,"after",(long)after);t.Commit();
                }}
                ChiWallet.Committed(p,before,after);
                foreach(var material in consumed) {
                    if(history.Items.Count>=64)history.Items.Remove(history.Items.OrderBy(i=>i.Value).First().Key);
                    history.Items[material.UID]=now.AddSeconds(1);
                }
                Q.Install(p,consumed.Where(i=>i.StackSize<=1).Select(i=>i.UID),new ConquerItem[0]);
                foreach(var material in consumed.Where(i=>i.StackSize>1)){material.StackSize--;material.Mode=Enums.ItemMode.Update;material.Send(p);}
                p.Send(new ChiPowers(true).Query(p));p.Send(partner==null?"The jade grants you 300 Chi points.":"Yin and Yang unite, granting you 1,200 Chi points.");
            }catch(Exception ex){Q.Fail(p,"dis-city-jade",ex);}
            return true;
        }
    }
}
