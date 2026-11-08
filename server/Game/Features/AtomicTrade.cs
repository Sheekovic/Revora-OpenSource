using System;
using System.Globalization;
using System.Linq;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using Wire=Revora.Network.GamePackets.Trade;
using Session=Revora.Game.ConquerStructures.Trade;

namespace Revora.Game.Features
{
    internal static class AtomicTrade
    {
        private static bool enabled;
        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_trade_receipts(
id uuid PRIMARY KEY,left_uid bigint NOT NULL CHECK(left_uid>0),right_uid bigint NOT NULL CHECK(right_uid>left_uid),
left_items bigint[] NOT NULL,right_items bigint[] NOT NULL,left_gold numeric(20,0) NOT NULL CHECK(left_gold>=0),
right_gold numeric(20,0) NOT NULL CHECK(right_gold>=0),left_cp bigint NOT NULL CHECK(left_cp BETWEEN 0 AND 4294967295),
right_cp bigint NOT NULL CHECK(right_cp BETWEEN 0 AND 4294967295),settled_at timestamptz NOT NULL DEFAULT now());");}
            enabled=true;
        }
        internal static string Fingerprint(ConquerItem i) {
            return i.ToString()+"|"+i.StackSize.ToString(CultureInfo.InvariantCulture)+"|"+i.RefineItem+"|"+i.RefineryTime.Ticks
                +"|"+i.RunesEXP+"|"+i.PerfectionLevel+"|"+i.PerfectionProgress+"|"+i.PerfectionOwnerGuid;
        }
        private static void Close(GameState a,GameState b,string reason) {
            foreach(var item in a.Trade.Items)item.Mode=Enums.ItemMode.Default;
            if(b!=null)foreach(var item in b.Trade.Items)item.Mode=Enums.ItemMode.Default;
            a.Trade=new Session();if(b!=null)b.Trade=new Session();
            a.Send(new Wire(true){Type=Wire.HideTable,dwParam=b==null?a.Entity.UID:b.Entity.UID});if(b!=null)b.Send(new Wire(true){Type=Wire.HideTable,dwParam=a.Entity.UID});
            if(reason!=null){a.Send(reason);if(b!=null)b.Send(reason);}
        }
        internal static bool Tradable(GameState owner,GameState other,ConquerItem item) {
            if(item==null || item.Position!=0 || item.Warehouse!=0 || item.Bound || item.Suspicious || item.StackSize==0)return false;
            ConquerItemBaseInformation info;if(!ConquerItemInformation.BaseInformations.TryGetValue(item.ID,out info) || info.Type!=ConquerItemBaseInformation.ItemType.Dropable)return false;
            if(item.Lock!=0 && !TradePartnerships.Mature(owner,other))return false;
            return true;
        }
        internal static void Process(Wire wire,GameState p) {
            if(!enabled || !Q.Player(p,false) || p.Trade==null || !p.Entity.TradeOn)return;
            GameState peer;uint uid=wire.Type==Wire.Request?wire.dwParam:p.Trade.TraderUID;
            if(uid==p.Entity.UID)return;
            if(!Kernel.GamePool.TryGetValue(uid,out peer) || !Q.Player(peer,false) || peer.Trade==null) {
                if(wire.Type==Wire.Close)lock(p.ItemSyncRoot)Close(p,null,null);return;
            }
            var first=p.Entity.UID<peer.Entity.UID?p:peer;var second=ReferenceEquals(first,p)?peer:p;
            lock(first.ItemSyncRoot)lock(second.ItemSyncRoot) {
                if(!Q.Player(p,false) || !Q.Player(peer,false) || !p.Entity.TradeOn || !peer.Entity.TradeOn)return;
                if(wire.Type!=Wire.Request && (p.Trade.TraderUID!=peer.Entity.UID || peer.Trade.TraderUID!=p.Entity.UID)) {
                    if(wire.Type==Wire.Close)Close(p,null,null);return;
                }
                if(wire.Type!=Wire.Close && (p.Entity.Dead || peer.Entity.Dead || p.Entity.PokerTable>0 || peer.Entity.PokerTable>0 || p.Entity.MapID!=peer.Entity.MapID || Kernel.GetDistance(p.Entity.X,p.Entity.Y,peer.Entity.X,peer.Entity.Y)>Constants.pScreenDistance
                    || p.Booth!=null || peer.Booth!=null || p.InQualifier() || peer.InQualifier())) {
                    if(wire.Type!=Wire.Request)Close(p,peer,"Stand together outside a contest before trading.");return;
                }
                if(wire.Type!=Wire.Request && wire.Type!=Wire.Close && (!p.Trade.InTrade || !peer.Trade.InTrade))return;
                if((wire.Type==Wire.AddItem || wire.Type==Wire.SetMoney || wire.Type==Wire.SetConquerPoints) && (p.Trade.Accepted || peer.Trade.Accepted)) {
                    Close(p,peer,"The offer changed after acceptance. Reopen the trade to confirm its new terms.");return;
                }
                if(wire.Type==Wire.AddItem && p.Trade.Items.Any(i=>i.UID==wire.dwParam))return;
                if(wire.Type==Wire.Close){Close(p,peer,null);return;}
                Wire.Dispatch(wire,p);
                if(wire.Type==Wire.AddItem) {
                    var item=p.Trade.Items.FirstOrDefault(i=>i.UID==wire.dwParam);
                    if(item!=null)p.Trade.OfferSnapshots[item.UID]=Fingerprint(item);
                }
            }
        }
        private static bool Offered(GameState p,GameState other,ConquerItem[] items) {
            if(items.Length>20 || items.Select(i=>i.UID).Distinct().Count()!=items.Length)return false;
            foreach(var item in items) {
                ConquerItem owned;string original;
                if(!p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(item,owned) || !Tradable(p,other,item)
                    || !p.Trade.OfferSnapshots.TryGetValue(item.UID,out original) || original!=Fingerprint(item))return false;
            }
            return true;
        }
        internal static void AcceptLocked(Wire wire,GameState p) {
            GameState peer;if(!Kernel.GamePool.TryGetValue(p.Trade.TraderUID,out peer))return;
            if(!System.Threading.Monitor.IsEntered(p.ItemSyncRoot) || !System.Threading.Monitor.IsEntered(peer.ItemSyncRoot))throw new InvalidOperationException("Trade settlement requires both owner locks.");
            p.Trade.Accepted=true;peer.Send(wire);if(!peer.Trade.Accepted)return;
            var a=p.Entity.UID<peer.Entity.UID?p:peer;var b=ReferenceEquals(a,p)?peer:p;
            var left=a.Trade;var right=b.Trade;var ai=left.Items.ToArray();var bi=right.Items.ToArray();
            bool ancient=ai.Concat(bi).Any(item=>AncientDevilRules.AmuletName(item.ID)!=null);
            if(left.RevoraPoint!=0 || right.RevoraPoint!=0 || !Offered(a,b,ai) || !Offered(b,a,bi)
                || a.Inventory.Count-ai.Length+bi.Length>40 || b.Inventory.Count-bi.Length+ai.Length>40
                || left.Money>a.Entity.Money || right.Money>b.Entity.Money || left.ConquerPoints>a.Entity.ConquerPoints || right.ConquerPoints>b.Entity.ConquerPoints) {
                Close(a,b,"The offered items, funds or inventory space changed. Nothing was exchanged.");return;
            }
            decimal ag=(decimal)a.Entity.Money-left.Money+right.Money,bg=(decimal)b.Entity.Money-right.Money+left.Money;
            long ac=(long)a.Entity.ConquerPoints-left.ConquerPoints+right.ConquerPoints,bc=(long)b.Entity.ConquerPoints-right.ConquerPoints+left.ConquerPoints;
            if(ag>Revora.Game.Entity.MaximumSilver || bg>Revora.Game.Entity.MaximumSilver || ac>uint.MaxValue || bc>uint.MaxValue) {Close(a,b,"One of you must spend some saved funds before this trade.");return;}
            try {
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    foreach(var owner in new[]{a,b})using(var q=Q.Command(c,t,"SELECT conquerpoints FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)owner.Entity.UID)) {
                        var value=q.ExecuteScalar();if(value==null || Convert.ToUInt32(value)!=owner.Entity.ConquerPoints)throw new InvalidOperationException("Trade purse requires reload.");
                    }
                    SilverBalance.AssertSaved(c,t,a);SilverBalance.AssertSaved(c,t,b);
                    if(ai.Concat(bi).Any(i=>i.Lock!=0) && !TradePartnerships.MatureSaved(c,t,a,b))
                        throw new InvalidOperationException("Locked-item trade requires a saved mature mutual partnership.");
                    Q.Execute(c,t,@"INSERT INTO revora_trade_receipts(id,left_uid,right_uid,left_items,right_items,left_gold,right_gold,left_cp,right_cp)
VALUES(@id,@left,@right,@li,@ri,@lg,@rg,@lc,@rc)","id",left.ReceiptId,"left",(long)a.Entity.UID,"right",(long)b.Entity.UID,
                        "li",ai.Select(i=>(long)i.UID).ToArray(),"ri",bi.Select(i=>(long)i.UID).ToArray(),"lg",(decimal)left.Money,"rg",(decimal)right.Money,"lc",(long)left.ConquerPoints,"rc",(long)right.ConquerPoints);
                    foreach(var item in ai.Concat(bi).OrderBy(i=>i.UID)) {
                        bool fromLeft=ai.Contains(item);var owner=fromLeft?a:b;var receiver=fromLeft?b:a;
                        if(SummerCrafting.Identity(item.ID) && !SummerCrafting.Transferable(c,t,owner,item))throw new InvalidOperationException("Summer trade provenance invalid.");
                        if(Q.Execute(c,t,@"UPDATE items SET entityid=@to WHERE uid=@item AND entityid=@from AND id=@type AND position=0 AND warehouse=0
AND stacksize=@stack AND bound=0 AND locked=@locked AND suspicious=@suspicious", "to",(long)receiver.Entity.UID,"item",(long)item.UID,
                            "from",(long)owner.Entity.UID,"type",(int)item.ID,"stack",(int)item.StackSize,"locked",(int)item.Lock,"suspicious",item.Suspicious?1:0)!=1)
                            throw new InvalidOperationException("Trade item ownership changed.");
                    }
                    // Gold is an owner-session balance in this server; unlike CPs,
                    // its setter does not persist. Commit both captured purses here.
                    if(Q.Execute(c,t,"UPDATE entities SET money=@gold,conquerpoints=@cp WHERE uid=@uid","gold",ag,"cp",ac,"uid",(long)a.Entity.UID)!=1)throw new InvalidOperationException("Left trade purse was not saved.");
                    if(Q.Execute(c,t,"UPDATE entities SET money=@gold,conquerpoints=@cp WHERE uid=@uid","gold",bg,"cp",bc,"uid",(long)b.Entity.UID)!=1)throw new InvalidOperationException("Right trade purse was not saved.");
                    if(ancient){AncientDevilCollection.Record(c,t,a);AncientDevilCollection.Record(c,t,b);}t.Commit();
                }}
                Q.Install(a,ai.Select(i=>i.UID),new ConquerItem[0]);Q.Install(b,bi.Select(i=>i.UID),new ConquerItem[0]);
                foreach(var item in ai.Concat(bi)){item.StatsLoaded=false;item.Mode=Enums.ItemMode.Default;}
                Q.Install(a,new uint[0],bi);Q.Install(b,new uint[0],ai);
                SilverBalance.ApplyCommitted(a,(ulong)ag);SilverBalance.ApplyCommitted(b,(ulong)bg);a.Entity.ApplyCommittedConquerPoints((uint)ac);b.Entity.ApplyCommittedConquerPoints((uint)bc);
                Close(a,b,null);Console.WriteLine("[trade] settled="+left.ReceiptId.ToString("N")+" left="+a.Entity.UID+" right="+b.Entity.UID);
                if(ancient){Q.Refresh(a);Q.Refresh(b);}
            }catch(Exception ex){Close(a,b,null);Q.Fail(a,"trade-settlement",ex);Q.Fail(b,"trade-settlement",ex);}
        }
    }
}
