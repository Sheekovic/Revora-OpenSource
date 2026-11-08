using System;
using BitConverter=System.BitConverter;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type AuctionWire=typeof(Entity).Assembly.GetType("Revora.Game.Features.AuctionGateway");
    static byte[] AuctionPacket(byte action,uint uid,byte currency=1,uint price=100,uint buyout=1000,byte hours=12)
    {
        var p=new Auction(true){Action=(Auction.Mode)action,ItemUID=uid,MoneyType=currency,BasePrice=price,FixedPrice=buyout,Time=hours};
        byte[] frame=p.ToArray();Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);return frame;
    }
    static uint AuctionWireUid(uint item)
    {return (uint)Scalar("SELECT w.wire_uid FROM revora_auction_wire w JOIN revora_auctions a ON a.id=w.auction WHERE a.item_uid=@uid ORDER BY w.wire_uid DESC LIMIT 1",item);}
    static void AuctionSend(GameState p,byte[] frame){Auction.ProcesoInglesCompletoSubasta(frame,p);}
    static void VerifyAuctionWire(Map map)
    {
        Query("CREATE TABLE auctionitems(uid bigint)");
        ConquerItemTable.LoadAuctionItems();
        var seller=AuctionPlayer(map);var buyer=AuctionPlayer(map);var item=AuctionItem(seller);
        var list=AuctionPacket(0,item.UID);AuctionSend(seller,list);PacketHandler.HandlePacket(list,seller);
        uint wire=AuctionWireUid(item.UID);
        Check(wire!=0 && wire!=item.UID && !Fenced(seller) && seller.Entity.Money==99500
            && !seller.Inventory.ContainsUID(item.UID) && Scalar("SELECT count(*) FROM items WHERE uid=@uid",wire)==0,
            "native listing creates a distinct durable display identity and a repeated request cannot charge twice");
        Guid id=(Guid)Call(AuctionWire,null,"Resolve",wire);var snapshot=Call(Auctions,null,"Get",id);
        var preview=(ConquerItem)Call(AuctionWire,null,"Preview",snapshot);
        Check(preview.UID==wire && preview.AuctionInfo.ItemUID==wire && preview.Purification.ItemUID==wire && preview.ExtraEffect.ItemUID==wire
            && SavedRune(item,"entityid")==0,"auction preview remaps item and effect references without recreating or moving the physical item");
        preview.AuctionInfo.SellerName=new string('S',32);preview.AuctionInfo.BidderName="1234567890ABCDEF-more-than-sixteen";
        byte[] response=MsgAuctionItem.ToArray(preview,0,seller);Writer.WriteUInt64(PacketHandler.ClientSeal,response.Length-8,response);
        Check(response.Length==128 && BitConverter.ToUInt16(response,0)==120 && BitConverter.ToUInt32(response,52)==wire
            && Server.Encoding.GetString(response,104,16)=="1234567890ABCDEF" && BitConverter.ToUInt64(response,120)==PacketHandler.ClientSeal
            && BitConverter.ToUInt32(response,80)==1,"public auction record holds both bounded names and a separate eight-byte suffix");
        byte[] personal=Auction.AuctionInfo(preview,0,seller);Writer.WriteUInt64(PacketHandler.ClientSeal,personal.Length-8,personal);
        Check(personal.Length==96 && BitConverter.ToUInt16(personal,0)==88 && Server.Encoding.GetString(personal,72,16)=="1234567890ABCDEF"
            && BitConverter.ToUInt32(personal,48)==1,"personal auction record preserves its complete bounded layout");
        var requests=Enumerable.Range(0,16).Select(_=>Task.Run(()=>(uint)Call(AuctionWire,null,"WireId",id))).ToArray();Task.WaitAll(requests);
        Check(requests.All(t=>t.Result==wire),"concurrent board reads retain one durable display identity per listing");
        ConquerItem.ItemUID.Now=1;ConquerItemTable.LoadAuctionItems();
        Check(ConquerItem.ItemUID.Now>=wire && (uint)Call(AuctionWire,null,"WireId",id)==wire,"startup restores the item counter above issued auction identities and keeps existing display IDs");
        var malformed=AuctionPacket(3,wire);
        foreach(int length in new[]{0,1,3,8,12,20,32,38,45,129,65536})AuctionSend(buyer,new byte[length]);
        foreach(int offset in new[]{0,2,38}){var bad=(byte[])malformed.Clone();bad[offset]++;AuctionSend(buyer,bad);}
        var unknown=(byte[])malformed.Clone();unknown[8]=255;AuctionSend(buyer,unknown);
        Check(!Fenced(buyer) && buyer.Entity.Money==100000 && buyer.Inventory.Count==0,"malformed lengths, headers, suffixes and auction actions cannot charge a purse");
        AuctionSend(buyer,AuctionPacket(1,wire));AuctionSend(buyer,AuctionPacket(3,item.UID));
        Check(!Fenced(buyer) && buyer.Entity.Money==100000 && SavedRune(item,"entityid")==0,"native non-owner cancellation and physical-UID substitution cannot claim an auction item");
        var fullBuy=AuctionPacket(3,wire,2,uint.MaxValue,1,0);var buy=new byte[44];Array.Copy(fullBuy,buy,36);
        Writer.WriteUInt16(36,0,buy);Writer.WriteUInt64(PacketHandler.ClientSeal,36,buy);PacketHandler.HandlePacket(buy,buyer);
        Check(!Fenced(buyer) && buyer.Entity.Money==99000 && buyer.Entity.ConquerPoints==100000 && buyer.Inventory.ContainsUID(item.UID)
            && SavedRune(item,"entityid")==buyer.Entity.UID,"native buyout trusts stored price and currency, then installs the original physical item");
        AuctionSend(buyer,buy);
        Check(buyer.Entity.Money==99000 && buyer.Inventory.Count==1,"replayed native buyout cannot pay twice after delivery");
        auctionNow=auctionNow.AddSeconds(11);Call(AuctionWire,null,"Tick");
        Check(seller.Entity.Money==100500,"scheduled native integration delivers seller proceeds without requiring another packet");

        var repeat=AuctionItem(seller);AuctionSend(seller,AuctionPacket(0,repeat.UID));uint oldWire=AuctionWireUid(repeat.UID);
        byte[] cancel=AuctionPacket(1,oldWire);AuctionSend(seller,cancel);
        Check(seller.Inventory.ContainsUID(repeat.UID),"native seller cancellation returns the original item immediately when space is available");
        ulong feeBefore=seller.Entity.Money;AuctionSend(seller,AuctionPacket(0,repeat.UID));
        Check(seller.Entity.Money==feeBefore && seller.Inventory.ContainsUID(repeat.UID),"rapid cancelled-listing replay keeps the item and cannot charge another fee");
        auctionNow=auctionNow.AddSeconds(1);AuctionSend(seller,AuctionPacket(0,repeat.UID));uint newWire=AuctionWireUid(repeat.UID);
        AuctionSend(seller,cancel);AuctionSend(buyer,AuctionPacket(3,oldWire));
        Check(newWire!=oldWire && !seller.Inventory.ContainsUID(repeat.UID) && buyer.Entity.Money==99000
            && Scalar("SELECT state FROM revora_auctions WHERE item_uid=@uid AND state=0",repeat.UID)==0,
            "old native cancel and buy packets cannot target a later relisting of the same physical item");
        uint allocated=ConquerItem.ItemUID.Next;
        Check(allocated>newWire,"ordinary newly generated items skip reserved auction display IDs");
        var pages=AuctionPlayer(map);
        for(int n=0;n<12;n++){var i=AuctionItem(pages);AuctionSend(pages,AuctionPacket(0,i.UID,2));}
        object[] args={2,0u,false,0,10,0L};var firstPage=(Array)Auctions.GetMethod("Page",Hidden).Invoke(null,args);long total=(long)args[5];
        args=new object[]{2,0u,false,1,10,0L};var secondPage=(Array)Auctions.GetMethod("Page",Hidden).Invoke(null,args);
        Check(firstPage.Length==10 && secondPage.Length==2 && total==12 && (long)args[5]==12,"public auction query selects bounded database pages with the correct currency filter and total");
        var query=new byte[60];Writer.WriteUInt16(52,0,query);Writer.WriteUInt16(1321,2,query);query[12]=2;Writer.WriteUInt16(1,50,query);Writer.WriteUInt64(PacketHandler.ClientSeal,52,query);
        MsgAuctionItem.Process(query,buyer);MsgAuctionItem.Process(query,buyer);Auction.ProcessAuctionQuery(query,buyer);
        var own=new byte[28];Writer.WriteUInt16(20,0,own);Writer.WriteUInt16(1322,2,own);Writer.WriteUInt64(PacketHandler.ClientSeal,20,own);
        auctionNow=auctionNow.AddSeconds(2);Auction.ProcessAuctionInfo(own,pages);
        foreach(int length in new[]{0,1,8,12,27,59,129}){MsgAuctionItem.Process(new byte[length],buyer);Auction.ProcessAuctionInfo(new byte[length],buyer);}
        Check(!Fenced(buyer) && !Fenced(pages) && buyer.Entity.Money==99000 && pages.Entity.Money==94000,
            "native public and personal queries are bounded, debounced and cannot mutate balances or read short frames");
        auctionNow=auctionNow.AddHours(12);Call(AuctionWire,null,"Tick");
        for(int n=0;n<3;n++){auctionNow=auctionNow.AddSeconds(11);Call(AuctionWire,null,"Tick");}
        Check(pages.Inventory.Count==12 && seller.Inventory.ContainsUID(repeat.UID),"event-loop expiry and delivery retries return unsold listings with original physical identities");
        var blocked=AuctionItem(seller);Query("INSERT INTO auctionitems(uid) VALUES(777)");
        bool stopped=false;try{ConquerItemTable.LoadAuctionItems();}catch(InvalidOperationException){stopped=true;}
        ulong prior=seller.Entity.Money;AuctionSend(seller,AuctionPacket(0,blocked.UID));ConquerItemTable.SaveAuctionItems();
        Check(stopped && seller.Inventory.ContainsUID(blocked.UID) && seller.Entity.Money==prior
            && Scalar("SELECT count(*) FROM auctionitems WHERE uid=@uid",777)==1,"legacy persisted auctions block activation and snapshot saving does not delete them");
        Query("DELETE FROM auctionitems WHERE uid=777");Kernel.AuctionItems[blocked.UID]=blocked;
        stopped=false;try{ConquerItemTable.LoadAuctionItems();}catch(InvalidOperationException){stopped=true;}
        Check(stopped && Kernel.AuctionItems.ContainsKey(blocked.UID),"unsaved legacy auction cache blocks activation without discarding its items");
        ConquerItem removed;Kernel.AuctionItems.TryRemove(blocked.UID,out removed);ConquerItemTable.LoadAuctionItems();
        Check(EntityTable.SaveEntity(seller) && EntityTable.SaveEntity(buyer) && EntityTable.SaveEntity(pages),"native auction payments and deliveries remain durable through production character saves");
        var counter=new Counter(uint.MaxValue-2);var next=typeof(Counter).GetProperty("NextStrict",Instance);
        Check((uint)next.GetValue(counter,null)==uint.MaxValue-1,"last supported reserved identity is allocated exactly");
        stopped=false;try{next.GetValue(counter,null);}catch(TargetInvocationException ex){stopped=ex.InnerException is OverflowException;}
        Check(stopped && counter.Now==uint.MaxValue-1,"reserved identity allocation fails without wrapping to an old listing");
        var identityItem=AuctionItem(seller);prior=seller.Entity.Money;
        Query("ALTER TABLE revora_auction_wire ADD CONSTRAINT fail_wire_identity CHECK(false) NOT VALID");
        AuctionSend(seller,AuctionPacket(0,identityItem.UID));
        Check(Fenced(seller) && seller.Entity.Money==prior && SavedRune(identityItem,"entityid")==seller.Entity.UID
            && Scalar("SELECT count(*) FROM revora_auctions WHERE item_uid=@uid",identityItem.UID)==0,
            "failed native display identity rolls back the listing, escrow and fee together");
        Query("ALTER TABLE revora_auction_wire DROP CONSTRAINT fail_wire_identity");ReloadSilver(seller);
        var held=AuctionPlayer(map);var heldItem=AuctionItem(held);AuctionSend(held,AuctionPacket(0,heldItem.UID));
        AuctionSend(buyer,AuctionPacket(3,AuctionWireUid(heldItem.UID)));
        auctionNow=auctionNow.AddSeconds(11);
        lock(held.ItemSyncRoot)
        {
            var tick=Task.Run(()=>Call(AuctionWire,null,"Tick"));
            Check(tick.Wait(5000) && held.Entity.Money==99500,"scheduled auction delivery skips a busy player without blocking the event loop");
        }
        for(int n=0;n<3;n++){auctionNow=auctionNow.AddSeconds(11);Call(AuctionWire,null,"Tick");}
        Check(held.Entity.Money==100500,"busy owner's pending proceeds arrive on a later scheduled pass");
        var capSeller=AuctionPlayer(map);var capBidder=AuctionPlayer(map);
        for(int n=0;n<50;n++){var i=AuctionItem(capSeller);var auction=AuctionList(capSeller,i);if(auction==Guid.Empty || !AuctionBid(capBidder,auction,100))throw new Exception("Auction capacity setup failed.");}
        var overLimit=AuctionItem(capSeller);prior=capSeller.Entity.Money;
        Check(AuctionList(capSeller,overLimit)==Guid.Empty && capSeller.Inventory.ContainsUID(overLimit.UID) && capSeller.Entity.Money==prior,
            "fifty active listings fill the personal board and further listings keep their item and fee");
        var extraSeller=AuctionPlayer(map);var extraItem=AuctionItem(extraSeller);var extra=AuctionList(extraSeller,extraItem);prior=capBidder.Entity.Money;
        Check(!AuctionBid(capBidder,extra,100) && capBidder.Entity.Money==prior && AuctionBid(capBidder,extra,1000,true),
            "fifty leading bids fill the personal board while an immediate buyout remains available");
        args=new object[]{0,capBidder.Entity.UID,true,0,50,0L};var bids=(Array)Auctions.GetMethod("Page",Hidden).Invoke(null,args);
        Check(bids.Length==50 && (long)args[5]==50,"every allowed leading bid fits the bounded personal board");
    }
}
