using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type Auctions=typeof(Entity).Assembly.GetType("Revora.Game.Features.AuctionHouse");
    static DateTime auctionNow=new DateTime(2030,1,1,12,0,0,DateTimeKind.Utc);
    static GameState AuctionPlayer(Map map)
    {
        var p=StoredProgressionPlayer(map);p.Entity.ConquerPoints=100000;p.Entity.Money=100000;EntityTable.UpdateMoney(p);return p;
    }
    static ConquerItem AuctionItem(GameState p)
    {
        var item=Pack(p,410009,1);p.Inventory.Remove(item.UID,Enums.ItemUse.None,false);
        item=ConquerItemTable.LoadItem(item.UID);p.Inventory.Add(item,Enums.ItemUse.None);return item;
    }
    static Guid AuctionList(GameState p,ConquerItem item,int currency=1,uint reserve=100,uint buyout=1000,int hours=12)
    {return (Guid)Call(Auctions,null,"List",p,item.UID,currency,reserve,buyout,hours);}
    static bool AuctionBid(GameState p,Guid id,uint offer,bool buyout=false){return (bool)Call(Auctions,null,"Bid",p,id,offer,buyout);}
    static int AuctionClaim(GameState p){return (int)Call(Auctions,null,"Claim",p);}
    static long AuctionCount(string expression,GameState p)
    {return Scalar("SELECT count(*) FROM revora_auction_deliveries WHERE recipient=@uid AND "+expression,p.Entity.UID);}
    static void VerifyAuctions(Map map)
    {
        Query("DROP TABLE entities;CREATE TABLE entities AS TABLE public.entities WITH NO DATA");
        Query("CREATE TABLE titles (LIKE public.titles INCLUDING ALL);CREATE TABLE itemadding (LIKE public.itemadding INCLUDING ALL);CREATE TABLE refinery (LIKE public.refinery INCLUDING ALL);INSERT INTO refinery SELECT * FROM public.refinery;CREATE TABLE refineryboxes (LIKE public.refineryboxes INCLUDING ALL);INSERT INTO refineryboxes SELECT * FROM public.refineryboxes");
        Refinery.LoadItems();StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
        Call(Auctions,null,"Initialize");Auctions.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>auctionNow));
        foreach(int currency in new[]{1,2})
        {
            var seller=AuctionPlayer(map);var first=AuctionPlayer(map);var second=AuctionPlayer(map);var item=AuctionItem(seller);
            uint wardrobe=StoredQuestGarment(second);
            Query("UPDATE items SET plus=12,perfectionlevel=54,perfectionprogress=777,perfectionowner='Fixture',perfectionownerguid=@owner,refineryitem=3004137,refinerytime=@expires WHERE uid=@uid",
                "owner",(long)seller.Entity.UID,"expires",DateTime.Now.AddDays(7).Ticks,"uid",(long)item.UID);
            Query("INSERT INTO itemadding(uid,addingtype,addingid,addinglevel,addingpercent,duration,addedon,stabilizationpoints) VALUES(@uid,1,724409,5,6,0,@time,270),(@uid,1,3004137,6,7,604800,@time,270)","uid",(long)item.UID,"time",DateTime.Now.Ticks);
            seller.Inventory.Remove(item.UID,Enums.ItemUse.None,false);item=ConquerItemTable.LoadItem(item.UID);seller.Inventory.Add(item,Enums.ItemUse.None);
            Guid id=AuctionList(seller,item,currency);
            Check(id!=Guid.Empty && !Fenced(seller) && seller.Entity.Money==99500 && !seller.Inventory.ContainsUID(item.UID)
                && SavedRune(item,"entityid")==0 && SavedRune(item,"plus")==12,"listing commits fee and original-item escrow for currency "+currency);
            Check(AuctionList(seller,item,currency)==Guid.Empty && seller.Entity.Money==99500,"duplicate listing cannot charge or escrow twice");
            Check(!(bool)Call(Auctions,null,"Cancel",first,id) && !AuctionBid(seller,id,100) && !AuctionBid(first,id,99),
                "non-owner cancellation, self-bidding and below-reserve bids cannot mutate the listing");
            Check(AuctionBid(first,id,100) && !AuctionBid(first,id,100) && AuctionBid(first,id,250)
                && (currency==1?first.Entity.Money:first.Entity.ConquerPoints)==99750,"a repeat bid is rejected and the same bidder pays only the increase");
            Check(AuctionBid(second,id,500) && AuctionCount("kind='refund' AND claimed_at IS NULL",first)==1
                && (currency==1?first.Entity.Money:first.Entity.ConquerPoints)==99750,"outbid refund remains durably owed before delivery");
            Check(AuctionClaim(first)==1 && AuctionClaim(first)==0 && (currency==1?first.Entity.Money:first.Entity.ConquerPoints)==100000,
                "outbid refund commits once and immediately restores the original purse");
            Check(AuctionBid(second,id,uint.MaxValue) && !AuctionBid(first,id,1000,true)
                && (currency==1?second.Entity.Money:second.Entity.ConquerPoints)==99000,"buyout charges the fixed price including the held bid and rejects later buyers");
            var fillers=Enumerable.Range(0,40).Select(i=>Pack(second,1088001,1)).ToArray();
            Check(AuctionClaim(second)==0 && AuctionCount("item_uid>0 AND claimed_at IS NULL",second)==1 && SavedRune(item,"entityid")==0,
                "full inventory preserves the purchased item in original-UID escrow");
            Query("DELETE FROM items WHERE uid=@uid","uid",(long)fillers[0].UID);second.Inventory.Remove(fillers[0].UID,Enums.ItemUse.None,false);
            GameState removed;Kernel.GamePool.TryRemove(second.Entity.UID,out removed);
            Check(AuctionClaim(second)==0,"offline recipient cannot project an auction delivery into a stale session");
            Kernel.GamePool[second.Entity.UID]=second;Call(Auctions,null,"Initialize");
            Check(AuctionClaim(second)==1 && AuctionClaim(second)==0 && second.Inventory.ContainsUID(item.UID) && SavedRune(item,"entityid")==second.Entity.UID,
                "pending auction item survives service reinitialization and is delivered once with its original UID");
            var delivered=second.Inventory.Objects.Single(i=>i.UID==item.UID);
            Check(delivered.Plus==12 && delivered.PerfectionLevel==54 && delivered.PerfectionProgress==777 && delivered.PerfectionOwner=="Fixture"
                && RefineRows(delivered,"duration=0 AND stabilizationpoints=270")==1 && RefineRows(delivered,"duration>0 AND stabilizationpoints=270")==1,
                "auction delivery preserves perfection, active refinery, permanent fallback and paid stabilization credit");
            Check(AuctionClaim(seller)==1 && AuctionClaim(seller)==0 && (currency==1?seller.Entity.Money:seller.Entity.ConquerPoints)==(currency==1?100500UL:101000UL),
                "seller receives the exact buyout once while the listing fee remains paid");
            Check(EntityTable.SaveEntity(seller) && EntityTable.SaveEntity(first) && EntityTable.SaveEntity(second),"auction currency changes remain valid through production character saves");
            var savedDelivery=ConquerItemTable.LoadItem(item.UID);
            Check(savedDelivery.ID==item.ID && savedDelivery.Plus==12 && savedDelivery.PerfectionLevel==54
                && Scalar("SELECT count(*) FROM items WHERE uid=@uid AND position=0 AND warehouse=0 AND inwardrobe=1",wardrobe)==1,
                "auction item load and character saves preserve delivered equipment and stored wardrobe design for currency "+currency);
        }
        var owner=AuctionPlayer(map);var buyer=AuctionPlayer(map);var cancelItem=AuctionItem(owner);var cancel=AuctionList(owner,cancelItem);
        AuctionBid(buyer,cancel,200);
        Check((bool)Call(Auctions,null,"Cancel",owner,cancel) && !(bool)Call(Auctions,null,"Cancel",owner,cancel)
            && AuctionClaim(buyer)==1 && buyer.Entity.Money==100000 && AuctionClaim(owner)==1 && owner.Inventory.ContainsUID(cancelItem.UID),
            "seller cancellation returns the item and refunds the held bid exactly once");
        auctionNow=auctionNow.AddSeconds(1);
        var sold=AuctionList(owner,cancelItem,1,100,0);AuctionBid(buyer,sold,300);
        var unsoldItem=AuctionItem(owner);AuctionList(owner,unsoldItem,2,100,0);
        auctionNow=auctionNow.AddHours(12);
        Check(!AuctionBid(buyer,sold,400) && (int)Call(Auctions,null,"Expire")==2 && (int)Call(Auctions,null,"Expire")==0,
            "exact expiry closes sold and unsold auctions once and rejects late bids");
        Check(AuctionClaim(buyer)==1 && buyer.Inventory.ContainsUID(cancelItem.UID) && AuctionClaim(owner)==2 && owner.Inventory.ContainsUID(unsoldItem.UID),
            "expiry delivers the winner's original item, seller proceeds and the unsold item");
        var invalid=AuctionItem(owner);ulong before=owner.Entity.Money;
        Check(AuctionList(owner,invalid,3)==Guid.Empty && AuctionList(owner,invalid,1,0)==Guid.Empty
            && AuctionList(owner,invalid,1,100,50)==Guid.Empty && AuctionList(owner,invalid,1,100,1000,1)==Guid.Empty && owner.Entity.Money==before,
            "unsupported currency, invalid prices and unsupported duration do not charge a fee");
        invalid.Bound=true;Check(AuctionList(owner,invalid)==Guid.Empty,"bound items cannot enter auction escrow");invalid.Bound=false;
        invalid.Lock=1;Check(AuctionList(owner,invalid)==Guid.Empty,"locked items cannot enter auction escrow");invalid.Lock=0;
        invalid.Inscribed=true;Check(AuctionList(owner,invalid)==Guid.Empty,"guild-arsenal items cannot be auctioned while inscribed");invalid.Inscribed=false;
        invalid.Days=1;Check(AuctionList(owner,invalid)==Guid.Empty,"time-limited items remain with their owner instead of expiring inside escrow");invalid.Days=0;
        Query("ALTER TABLE revora_auction_events ADD CONSTRAINT fail_auction_event CHECK(false) NOT VALID");
        Check(AuctionList(owner,invalid)==Guid.Empty && Fenced(owner) && owner.Inventory.ContainsUID(invalid.UID) && SavedRune(invalid,"entityid")==owner.Entity.UID
            && Scalar("SELECT money FROM entities WHERE uid=@uid",owner.Entity.UID)==(long)before,"listing receipt failure rolls back fee and item escrow together");
        Query("ALTER TABLE revora_auction_events DROP CONSTRAINT fail_auction_event");ReloadSilver(owner);
        var retry=AuctionList(owner,invalid);AuctionBid(buyer,retry,100);
        var rival=AuctionPlayer(map);
        Query("ALTER TABLE revora_auction_deliveries ADD CONSTRAINT fail_auction_delivery CHECK(false) NOT VALID");
        Check(!AuctionBid(rival,retry,200) && Fenced(rival) && rival.Entity.Money==100000
            && Scalar("SELECT highest FROM revora_auctions WHERE item_uid=@uid AND state=0",invalid.UID)==100,
            "failed outbid refund rolls back the new debit and preserves the current winning bid");
        Query("ALTER TABLE revora_auction_deliveries DROP CONSTRAINT fail_auction_delivery");ReloadSilver(rival);
        Check(AuctionBid(rival,retry,200),"failed outbid can retry without duplicate charges or phantom refunds");
        Check((bool)Call(Auctions,null,"Cancel",owner,retry),"cancellation records pending delivery before inventory installation");
        Query("ALTER TABLE revora_auction_deliveries ADD CONSTRAINT fail_auction_claim CHECK(claimed_at IS NULL) NOT VALID");
        Check(AuctionClaim(owner)==0 && Fenced(owner) && !owner.Inventory.ContainsUID(invalid.UID) && SavedRune(invalid,"entityid")==0,
            "failed claim acknowledgement rolls back item ownership and leaves the parcel claimable");
        Query("ALTER TABLE revora_auction_deliveries DROP CONSTRAINT fail_auction_claim");ReloadSilver(owner);
        Check(AuctionClaim(owner)==1 && owner.Inventory.ContainsUID(invalid.UID),"failed parcel delivery retries with the same physical item UID");
        auctionNow=auctionNow.AddSeconds(1);
        var racing=AuctionList(owner,invalid);var a=AuctionPlayer(map);var b=AuctionPlayer(map);bool aw=false,bw=false;
        Parallel.Invoke(()=>aw=AuctionBid(a,racing,1000,true),()=>bw=AuctionBid(b,racing,1000,true));
        Check(aw!=bw && a.Entity.Money+b.Entity.Money==199000 && AuctionCount("item_uid>0 AND claimed_at IS NULL",aw?a:b)==1,
            "competing buyouts charge exactly one buyer and create exactly one item entitlement");
        owner.Entity.Money=Entity.MaximumSilver;EntityTable.UpdateMoney(owner);
        Check(AuctionClaim(owner)==0 && AuctionCount("kind='proceeds' AND claimed_at IS NULL",owner)==1,"a full seller purse preserves unpaid auction proceeds");
        owner.Entity.Money=0;EntityTable.UpdateMoney(owner);Check(AuctionClaim(owner)==1 && owner.Entity.Money==1000,"pending proceeds arrive after the seller makes room");
        var proceeds=AuctionPlayer(map);var payer=AuctionPlayer(map);var cpItem=AuctionItem(proceeds);var cpSale=AuctionList(proceeds,cpItem,2);
        AuctionBid(payer,cpSale,1000,true);proceeds.Entity.ConquerPoints=uint.MaxValue;
        Check(AuctionClaim(proceeds)==0 && AuctionCount("kind='proceeds' AND claimed_at IS NULL",proceeds)==1,
            "CP proceeds wait without overflowing the seller's full purse");
        proceeds.Entity.ConquerPoints=0;
        Check(AuctionClaim(proceeds)==1 && proceeds.Entity.ConquerPoints==1000,"pending CP proceeds commit after the seller makes room");
        var fresh=AuctionPlayer(map);uint freshUid=fresh.Entity.UID;GameState gone;
        Kernel.GamePool.TryRemove(freshUid,out gone);fresh.Entity.UID=payer.Entity.UID;fresh.Account.EntityID=payer.Entity.UID;
        payer.Action=0;Kernel.GamePool[payer.Entity.UID]=fresh;ReloadSilver(fresh);
        typeof(Entity).GetMethod("ApplyCommittedConquerPoints",Instance).Invoke(fresh.Entity,new object[]{(uint)Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",fresh.Entity.UID)});
        ConquerItemTable.LoadItems(fresh);Call(Auctions,null,"Initialize");
        Check(AuctionClaim(fresh)==1 && fresh.Inventory.ContainsUID(cpItem.UID) && AuctionClaim(payer)==0,
            "fresh canonical session loads and claims the durable parcel while its old session cannot replay it");
        var oldId=cpSale;var relisted=AuctionList(fresh,fresh.Inventory.Objects.Single(i=>i.UID==cpItem.UID));
        Check(relisted!=Guid.Empty && !AuctionBid(owner,oldId,1000,true) && !(bool)Call(Auctions,null,"Cancel",fresh,oldId),
            "closed listing identity cannot mutate a later relisting of the same physical item");

        var stale=AuctionPlayer(map);var staleItem=AuctionItem(stale);Query("UPDATE entities SET money=money+1 WHERE uid=@uid","uid",(long)stale.Entity.UID);
        Check(AuctionList(stale,staleItem)==Guid.Empty && Fenced(stale) && SavedRune(staleItem,"entityid")==stale.Entity.UID
            && Scalar("SELECT money FROM entities WHERE uid=@uid",stale.Entity.UID)==100001,"stale silver cannot overwrite an external credit while paying an auction fee");
        ReloadSilver(stale);staleItem.Plus=1;
        Check(AuctionList(stale,staleItem)==Guid.Empty && Fenced(stale) && SavedRune(staleItem,"plus")==0
            && Scalar("SELECT money FROM entities WHERE uid=@uid",stale.Entity.UID)==100001,"unsaved item changes cannot be silently discarded into auction escrow");
        ReloadSilver(stale);staleItem.Plus=0;
        Query("CREATE FUNCTION suppress_auction_event() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_auction_event BEFORE INSERT ON revora_auction_events FOR EACH ROW EXECUTE FUNCTION suppress_auction_event()");
        Check(AuctionList(stale,staleItem)==Guid.Empty && Fenced(stale) && SavedRune(staleItem,"entityid")==stale.Entity.UID
            && Scalar("SELECT money FROM entities WHERE uid=@uid",stale.Entity.UID)==100001,"silently suppressed listing receipt cannot commit a fee or escrow transfer");
        Query("DROP TRIGGER suppress_auction_event ON revora_auction_events;DROP FUNCTION suppress_auction_event()");ReloadSilver(stale);
        var expireItem=AuctionItem(stale);var expire=AuctionList(stale,expireItem,1,100,0,24);AuctionBid(owner,expire,500);
        auctionNow=auctionNow.AddHours(24);
        Query("ALTER TABLE revora_auction_events ADD CONSTRAINT fail_auction_expiry CHECK(kind<>'expire') NOT VALID");
        bool failed=false;try{Call(Auctions,null,"Expire");}catch(Exception){failed=true;}
        Check(failed && Scalar("SELECT state FROM revora_auctions WHERE item_uid=@uid AND state=0",expireItem.UID)==0
            && AuctionCount("item_uid="+expireItem.UID+" AND claimed_at IS NULL",owner)==0,"expiry failure rolls back closure and all winner and seller entitlements");
        Query("ALTER TABLE revora_auction_events DROP CONSTRAINT fail_auction_expiry");
        Check((int)Call(Auctions,null,"Expire")>=1 && AuctionClaim(owner)==1 && owner.Inventory.ContainsUID(expireItem.UID),
            "expiry retries from the durable open listing without duplicating the winning item");
        Query("UPDATE entities SET money=money+1 WHERE uid=@uid","uid",(long)stale.Entity.UID);
        Check(AuctionClaim(stale)==0 && Fenced(stale) && AuctionCount("kind='proceeds' AND claimed_at IS NULL",stale)==1,
            "stale seller purse preserves unclaimed proceeds and requires a reload");
        ReloadSilver(stale);Check(AuctionClaim(stale)==1,"seller reload restores access to the same unpaid proceeds");
        var denied=AuctionPlayer(map);var deniedItem=AuctionItem(denied);
        denied.Fake=true;Check(AuctionList(denied,deniedItem)==Guid.Empty,"simulated adventurers cannot list player items");denied.Fake=false;
        denied.Entity.FullyLoaded=false;Check(AuctionList(denied,deniedItem)==Guid.Empty,"incomplete login cannot list player items");denied.Entity.FullyLoaded=true;
        denied.Trade.InTrade=true;Check(AuctionList(denied,deniedItem)==Guid.Empty,"active trade cannot also escrow the same inventory");denied.Trade.InTrade=false;
        denied.Booth=new Revora.Game.ConquerStructures.Booth();Check(AuctionList(denied,deniedItem)==Guid.Empty,"active stall cannot also escrow the same inventory");denied.Booth=null;
        Guid longListing=AuctionList(denied,deniedItem,1,100,1000,48);
        Check(longListing!=Guid.Empty && denied.Entity.Money==97750,"forty-eight-hour listing uses the existing 2,250-silver fee");
        VerifyAuctionWire(map);
    }
}
