using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type ChiItems=typeof(Entity).Assembly.GetType("Revora.Game.Features.ChiPointItems");
    static readonly Type ChiPurse=typeof(Entity).Assembly.GetType("Revora.Game.Features.ChiWallet");
    static DateTime chiItemNow=DateTime.UtcNow;
    static long ChiItemUses(GameState p){return Scalar("SELECT count(*) FROM revora_chi_item_uses WHERE uid=@uid",p.Entity.UID);}
    static long ChiStored(GameState p){return Scalar("SELECT COALESCE(points,0) FROM chi WHERE uid=@uid",p.Entity.UID);}
    static void OpenChiItem(GameState p,ConquerItem item){ItemUsage.Process(PrayerPacket(item.UID),p);}
    static GameState ChiPlayer(Map town,uint points){
        var p=WelcomePlayer(town,1,0,15);ChiTable.Load(p);
        if(points!=0){Query("UPDATE chi SET points=@points WHERE uid=@uid","points",(long)points,"uid",(long)p.Entity.UID);ChiTable.Load(p);}
        return p;
    }
    static void VerifyChiPointItems(string runtime,Map town){
        Query("CREATE TABLE IF NOT EXISTS chi (LIKE public.chi INCLUDING ALL)");
        Call(ChiItems,null,"Initialize");Call(ChiItems,null,"Initialize");
        ChiItems.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>chiItemNow));
        string handler=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemHandler.cs")));
        string service=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/ChiPointItems.cs")));
        string audit=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../docs/ITEM_USE_COMPOUND_AUDIT.md")));
        Check(handler.IndexOf("Chi item must use the owned service.")>=0 && handler.IndexOf("ChiPointItems.TryUse")>=0
            && handler.IndexOf("case 729242:")>=0 && handler.IndexOf("case 729481:")>=0 && handler.IndexOf("case 3005126:")>=0,
            "proved Chi items are fail-closed on the live handler");
        Check(handler.IndexOf("GetPositionFromID")>=0 && handler.IndexOf("Inventory = 0")>=0,
            "ordinary 1009/action-4 still dispatches non-equipment IDs through UseItem");
        Check(service.IndexOf("Chi=500,Text=\"You recieved 500 Chi Points\",NamedRed=true")>=0,
            "729242 keeps the misspelled Vital Pill receipt without an exclamation");
        Check(service.IndexOf("new Message(spec.Text,Color.Red,Message.System)")>=0
            && service.IndexOf("new Message(spec.Text,Message.System)")>=0,
            "Chi items preserve both the named Color.Red constructor and the two-argument System constructor");
        Check(service.IndexOf("4000")<0 && File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/ChiWallet.cs"))).IndexOf("points>=4000")>=0,
            "Chi items do not invent the refill 4,000 cap");
        int switchStart=handler.IndexOf("switch (item.ID)"),switchEnd=handler.IndexOf("public static byte GetNextRefineryItem",switchStart);
        var sourceIds=System.Text.RegularExpressions.Regex.Matches(handler.Substring(switchStart,switchEnd-switchStart),@"(?m)^ {16}case (\d+):").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        var auditIds=System.Text.RegularExpressions.Regex.Matches(audit,@"(?m)^\| (\d+) \|").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        Check(sourceIds.Length==518 && sourceIds.OrderBy(id=>id).SequenceEqual(auditIds.OrderBy(id=>id)),
            "compound audit still enumerates the exact 518 numeric top-level UseItem case IDs");
        uint[] ids=new uint[]{729481u,729242u,3005126u,3005127u,3005128u,3005129u};
        uint[] pays=new uint[]{500u,500u,500u,1000u,2000u,3000u};
        string[] texts=new string[]{
            "You received 500 Chi Points!","You recieved 500 Chi Points","You received 500 Chi Points!",
            "You received 1000 Chi Points!","You received 2000 Chi Points!","You received 3000 Chi Points!"};
        for(int n=0;n<ids.Length;n++){
            Check(audit.IndexOf("| "+ids[n].ToString()+" | owned-fail-closed |")>=0,"audit owns proved Chi item "+ids[n]);
            Check(ConquerItemInformation.BaseInformations.ContainsKey(ids[n]),"installed catalog contains Chi item "+ids[n]);
        }
        uint[] catalogPills=new uint[]{3000051u,729476u,729477u,729478u,729479u};
        uint[] catalogPays=new uint[]{5u,100u,200u,300u,400u};
        for(int n=0;n<catalogPills.Length;n++){
            Check(ConquerItemInformation.BaseInformations.ContainsKey(catalogPills[n]),"installed catalog contains Chi pill "+catalogPills[n]);
            Check(audit.IndexOf("| "+catalogPills[n].ToString()+" | owned-fail-closed |")>=0,"audit owns catalog Chi pill "+catalogPills[n]);
            Check(service.IndexOf("{"+catalogPills[n].ToString()+",")>=0,"owned Chi service accepts catalog pill "+catalogPills[n]);
        }
        Check(handler.IndexOf("AdjustChiPoints(")<0 && handler.IndexOf("case 3000051:")>=0 && handler.IndexOf("case 729479:")>=0,
            "catalog Chi pills fail closed and no longer split-write AdjustChiPoints");
        Check(File.ReadAllText("database\\itemtype-new.txt").IndexOf("729480@@SalesPamphlet")>=0,
            "729480 remains SalesPamphlet and is not a Chi handler");
        Check(handler.IndexOf("case 729480:")<0 && service.IndexOf("729480")<0,"729480 is excluded from the Chi item service");
        string exchange=File.ReadAllText("database\\shops\\exchange_shop_goods.txt");
        string eshop=File.ReadAllText("database\\shops\\emoneyshopV2.ini");
        string heroes=File.ReadAllText("database\\Slayer\\StageGoal.txt");
        string way=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/WayofHeroes.cs")));
        Check(exchange.IndexOf("@@729242@@")>=0 && eshop.IndexOf("729242")>=0,"729242 has EShop V2 and exchange-shop issuance");
        Check(exchange.IndexOf("@@729481@@")>=0,"729481 has exchange-shop issuance");
        Check(heroes.IndexOf("Prize1=3005126")>=0 && heroes.IndexOf("Prize1=3005127")>=0 && heroes.IndexOf("Prize1=3005128")>=0 && heroes.IndexOf("Prize1=3005129")>=0
            && way.IndexOf("WayOfHeroesClaims.Claim")>=0,
            "3005126-3005129 remain StageGoal prizes and claims go through the owned service");
        Check(exchange.IndexOf("729479")<0 && eshop.IndexOf("729479")<0 && heroes.IndexOf("729479")<0,
            "729479 has no current shop, exchange or Way of Heroes producer");
        string drops=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Database/DropItemTable.cs")));
        Check(drops.IndexOf("3000051")>=0 && drops.IndexOf("729476")>=0 && drops.IndexOf("729477")>=0 && drops.IndexOf("729478")>=0
            && handler.IndexOf("DropItemTable")<0 && File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Thread.cs"))).IndexOf("DropItemTable")<0
            && File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Server.cs"))).IndexOf("DropItemTable")<0,
            "DropItemTable still lists 3000051/729476-729478 and remains unreferenced");
        string heard="";ChiItems.GetField("Spoken",Hidden).SetValue(null,(Action<string>)(m=>{heard=heard+m+"|";}));
        for(int n=0;n<ids.Length;n++){
            heard="";var p=ChiPlayer(town,0);var item=Pack(p,ids[n],1);OpenChiItem(p,item);
            Check(!Fenced(p) && ChiItemUses(p)==1 && p.ChiPoints==pays[n] && ChiStored(p)==pays[n] && !p.Inventory.ContainsUID(item.UID)
                && heard.IndexOf(texts[n])>=0,"Chi item "+ids[n]+" pays "+pays[n]+" from ordinary action-4");
            p=ReloadBall(p);ChiTable.Load(p);
            Check(ChiItemUses(p)==1 && p.ChiPoints==pays[n] && ChiStored(p)==pays[n],"Chi item "+ids[n]+" survives production reload");
        }
        foreach(bool bound in new[]{false,true})foreach(ushort stack in new ushort[]{0,1,2}){
            var p=ChiPlayer(town,10);var pack=NestedPack(p,729242,stack,bound);
            Parallel.For(0,24,n=>{if(n%3==0)ChiTable.Save(p);else OpenChiItem(p,pack);});
            int expected=stack>1?2:1;
            Check(!Fenced(p) && ChiItemUses(p)==expected && p.ChiPoints==10u+500u*(uint)expected && CountId(p,729242)==0,
                "Chi item 729242 pays 500 with stack remainder semantics: "+bound+"/"+stack);
            ChiTable.Save(p);p=ReloadBall(p);ChiTable.Load(p);
            Check(p.ChiPoints==10u+500u*(uint)expected && ChiStored(p)==10u+500u*(uint)expected,"Chi item remainder survives save and reload: "+bound+"/"+stack);
        }
        heard="";var miss=ChiPlayer(town,0);var missItem=Pack(miss,729242,1);OpenChiItem(miss,missItem);
        Check(!Fenced(miss) && miss.ChiPoints==500 && heard.IndexOf("You recieved 500 Chi Points")>=0 && heard.IndexOf("You received 500 Chi Points!")<0,
            "729242 keeps the misspelled receipt without an exclamation");
        var overCap=ChiPlayer(town,3999);var pill=Pack(overCap,729242,1);OpenChiItem(overCap,pill);
        Check(!Fenced(overCap) && overCap.ChiPoints==4499 && ChiStored(overCap)==4499,"Chi items are not clipped at 4,000");
        Call(ChiPurse,null,"Refill",overCap);
        Check(overCap.ChiPoints==4499,"Chi refill leaves an above-4,000 Chi item award alone");
        heard="";var overflow=ChiPlayer(town,uint.MaxValue);var rich=Pack(overflow,729242,1);OpenChiItem(overflow,rich);
        Check(!Fenced(overflow) && ChiItemUses(overflow)==0 && overflow.Inventory.ContainsUID(rich.UID) && overflow.ChiPoints==uint.MaxValue
            && heard.IndexOf("Spend some Chi before using this item.")>=0,"checked uint overflow preserves the Chi item");
        foreach(string fault in new[]{"source","chi","receipt"}){
            var p=ChiPlayer(town,20);var item=Pack(p,729242,1);
            string table=fault=="receipt"?"revora_chi_item_uses":fault=="chi"?"chi":"items";
            string verb=fault=="source"?"DELETE":fault=="receipt"?"INSERT":"UPDATE";
            Query("CREATE FUNCTION suppress_chi_item() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_chi_item BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_chi_item()");OpenChiItem(p,item);
            Check(Fenced(p) && ChiItemUses(p)==0 && p.Inventory.ContainsUID(item.UID) && ChiStored(p)==20,"suppressed "+fault+" preserves Chi item source and wallet");
            Query("DROP TRIGGER suppress_chi_item ON "+table+";DROP FUNCTION suppress_chi_item()");p=ReloadBall(p);ChiTable.Load(p);OpenChiItem(p,p.Inventory.Objects.Single(i=>i.ID==729242));
            Check(!Fenced(p) && ChiItemUses(p)==1 && p.ChiPoints==520,"production reload retries the Chi item after "+fault+" rollback");
        }
        var extraBag=ChiPlayer(town,0);var extraSrc=Pack(extraBag,729242,1);uint extraUid=ConquerItem.ItemUID.Next;
        Query("INSERT INTO items(uid,id,entityid,stacksize) VALUES(@uid,@id,@owner,1)","uid",(long)extraUid,"id",1000000,"owner",(long)extraBag.Entity.UID);OpenChiItem(extraBag,extraSrc);
        Check(Fenced(extraBag) && ChiItemUses(extraBag)==0 && extraBag.Inventory.ContainsUID(extraSrc.UID) && extraBag.ChiPoints==0 && ChiStored(extraBag)==0,
            "an extra durable bag UID cannot pay a Chi item");
        Query("DELETE FROM items WHERE uid=@uid","uid",(long)extraUid);extraBag=ReloadBall(extraBag);ChiTable.Load(extraBag);OpenChiItem(extraBag,extraBag.Inventory.Objects.Single(i=>i.ID==729242));
        Check(!Fenced(extraBag) && ChiItemUses(extraBag)==1 && extraBag.ChiPoints==500,"production reload retries after the extra durable bag UID is removed");
        var missingBag=ChiPlayer(town,0);var missingSrc=Pack(missingBag,729242,1);var cacheOnly=Pack(missingBag,1000000,1);
        Query("DELETE FROM items WHERE uid=@uid","uid",(long)cacheOnly.UID);OpenChiItem(missingBag,missingSrc);
        Check(Fenced(missingBag) && ChiItemUses(missingBag)==0 && missingBag.Inventory.ContainsUID(missingSrc.UID) && missingBag.ChiPoints==0 && ChiStored(missingBag)==0,
            "a cache-only bag UID cannot pay a Chi item");
        missingBag=ReloadBall(missingBag);ChiTable.Load(missingBag);OpenChiItem(missingBag,missingBag.Inventory.Objects.Single(i=>i.ID==729242));
        Check(!Fenced(missingBag) && ChiItemUses(missingBag)==1 && missingBag.ChiPoints==500,"production reload retries after the cache-only bag UID is dropped");
        var staleWallet=ChiPlayer(town,30);var stalePack=Pack(staleWallet,729242,1);staleWallet.AdjustChiPoints(1);heard="";OpenChiItem(staleWallet,stalePack);
        Check(!Fenced(staleWallet) && ChiItemUses(staleWallet)==0 && staleWallet.Inventory.ContainsUID(stalePack.UID) && ChiStored(staleWallet)==30
            && heard.IndexOf("Your Chi is still being saved. Try again shortly.")>=0,"unsaved Chi revision defers the item without payment");
        var staleRow=ChiPlayer(town,40);var staleItem=Pack(staleRow,729242,1);Query("UPDATE chi SET points=41 WHERE uid=@uid","uid",(long)staleRow.Entity.UID);OpenChiItem(staleRow,staleItem);
        Check(Fenced(staleRow) && ChiItemUses(staleRow)==0 && staleRow.Inventory.ContainsUID(staleItem.UID) && ChiStored(staleRow)==41,"stale durable Chi row cannot pay a Chi item");
        var missing=ChiPlayer(town,0);var missingItem=Pack(missing,729242,1);Query("DELETE FROM chi WHERE uid=@uid","uid",(long)missing.Entity.UID);OpenChiItem(missing,missingItem);
        Check(Fenced(missing) && ChiItemUses(missing)==0 && missing.Inventory.ContainsUID(missingItem.UID),"missing Chi row cannot pay a Chi item");
        var staleBag=ChiPlayer(town,0);var bagItem=Pack(staleBag,729242,1);Query("UPDATE items SET id=723700 WHERE uid=@uid","uid",(long)bagItem.UID);OpenChiItem(staleBag,bagItem);
        Check(Fenced(staleBag) && ChiItemUses(staleBag)==0 && ChiStored(staleBag)==0,"stale durable source metadata cannot pay a Chi item");
        foreach(string guard in new[]{"dead","fake","trade","booth","locked","suspicious","wardrobe","expired","substituted"}){
            var p=ChiPlayer(town,7);var item=Pack(p,729242,1);uint chi=p.ChiPoints;
            if(guard=="dead")p.Entity.Hitpoints=0;if(guard=="fake")p.Fake=true;if(guard=="trade")p.Trade.InTrade=true;
            if(guard=="booth")p.Booth=(Revora.Game.ConquerStructures.Booth)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Revora.Game.ConquerStructures.Booth));if(guard=="locked")item.Lock=1;
            if(guard=="suspicious")item.Suspicious=true;if(guard=="wardrobe")item.InWardrobe=true;if(guard=="expired"){item.Days=1;item.DayStamp=chiItemNow.ToLocalTime().AddDays(-2);}
            if(guard=="substituted")ItemHandler.UseItem(new ConquerItem(true){ID=item.ID,UID=item.UID,StackSize=1},p);else OpenChiItem(p,item);
            Check(ChiItemUses(p)==0 && p.ChiPoints==chi && p.Inventory.ContainsUID(item.UID),"ineligible Chi item leaves source unchanged: "+guard);
        }
        var unready=WelcomePlayer(town,1,0,15);var unreadyItem=Pack(unready,729242,1);OpenChiItem(unready,unreadyItem);
        Check(!Fenced(unready) && ChiItemUses(unready)==0 && unready.Inventory.ContainsUID(unreadyItem.UID),"a session without a loaded Chi wallet cannot consume a Chi item");
        var frames=ChiPlayer(town,0);var wire=Pack(frames,729242,1);
        foreach(string bad in new[]{"short","long","length","type","seal"}){
            byte[] frame=bad=="short"?new byte[31]:bad=="long"?new byte[129]:PrayerPacket(wire.UID);
            Writer.WriteUInt16((ushort)(frame.Length-8),0,frame);Writer.WriteUInt16(1009,2,frame);Writer.WriteUInt32(wire.UID,8,frame);Writer.WriteUInt16(4,20,frame);Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);
            if(bad=="length")Writer.WriteUInt16(1,0,frame);if(bad=="type")Writer.WriteUInt16(1008,2,frame);if(bad=="seal")frame[frame.Length-1]^=1;
            ItemUsage.Process(frame,frames);Check(!Fenced(frames) && ChiItemUses(frames)==0 && frames.Inventory.ContainsUID(wire.UID),"bounded Chi item rejects "+bad+" frame");
        }OpenChiItem(frames,wire);Check(!Fenced(frames) && ChiItemUses(frames)==1 && frames.ChiPoints==500,"ordinary Chi item works after malformed frames");
        var replay=ChiPlayer(town,0);var once=Pack(replay,729242,1);uint uid=once.UID;OpenChiItem(replay,once);
        ItemHandler.UseItem(once,replay);Check(ChiItemUses(replay)==1 && !replay.Inventory.ContainsUID(uid),"consumed Chi item UID cannot replay");
        var direct=ChiPlayer(town,0);var directPack=Pack(direct,3005127,1);ItemHandler.UseItem(directPack,direct);
        Check(!Fenced(direct) && ChiItemUses(direct)==1 && direct.ChiPoints==1000,"direct UseItem opens 3005127");
        var pamphlet=ChiPlayer(town,0);var sales=Pack(pamphlet,729480,1);
        Check(!(bool)Call(ChiItems,null,"TryUse",sales,pamphlet) && pamphlet.Inventory.ContainsUID(sales.UID) && pamphlet.ChiPoints==0,
            "SalesPamphlet 729480 stays outside ChiPointItems");
        var leftover=ChiPlayer(town,0);var surprise=Pack(leftover,722178,1);
        Check(!(bool)Call(ChiItems,null,"TryUse",surprise,leftover) && leftover.Inventory.ContainsUID(surprise.UID),"Surprise box 722178 stays outside ChiPointItems");
        var spent=ChiPlayer(town,50);var spentItem=Pack(spent,729242,1);OpenChiItem(spent,spentItem);
        ChiTable.Save(spent);ChiTable.Load(spent);
        Check(!Fenced(spent) && spent.ChiPoints==550 && ChiStored(spent)==550,"ChiTable.Save keeps a committed Chi item award");
        VerifyCatalogChiPills(runtime,town,catalogPills,catalogPays);
        ChiItems.GetField("BeforeInstall",Hidden).SetValue(null,null);
        ChiItems.GetField("Spoken",Hidden).SetValue(null,null);
    }
    static void VerifyCatalogChiPills(string runtime,Map town,uint[] ids,uint[] pays){
        string drops=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Database/DropItemTable.cs")));
        string handler=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemHandler.cs")));
        Check(drops.IndexOf("new Type() { Item = 3000051")>=0 && handler.IndexOf("DropItemTable")<0
            && File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Thread.cs"))).IndexOf("DropItemTable")<0,
            "DropItemTable still names SmallChiPill and is not activated");
        long preserved=Scalar("SELECT count(*) FROM revora_chi_item_uses WHERE uid<>@uid",0);
        Query("ALTER TABLE revora_chi_item_uses DROP CONSTRAINT revora_chi_item_uses_item_type_check;ALTER TABLE revora_chi_item_uses ADD CONSTRAINT revora_chi_item_uses_item_type_check CHECK(item_type IN (729481,729242,3005126,3005127,3005128,3005129))");
        bool rejected=false;
        try{Query("INSERT INTO revora_chi_item_uses(id,uid,item_uid,item_type,before_stack,bound,chi,chi_before,chi_after,used_at) VALUES('22222222-2222-2222-2222-222222222222',1,1,3000051,1,false,5,0,5,now())");}
        catch(Exception){rejected=true;}
        Check(rejected && preserved>0,"old six-ID Chi receipt constraint rejects 3000051 and keeps historical receipts");
        Call(ChiItems,null,"Initialize");Call(ChiItems,null,"Initialize");
        Check(Scalar("SELECT count(*) FROM revora_chi_item_uses WHERE uid<>@uid",0)==preserved,"repeated Chi Initialize preserves historical receipts");
        string[] texts=new string[]{"You received 5 Chi Points!","You received 100 Chi Points!","You received 200 Chi Points!","You received 300 Chi Points!","You received 400 Chi Points!"};
        string heard="";ChiItems.GetField("Spoken",Hidden).SetValue(null,(Action<string>)(m=>{heard=heard+m+"|";}));
        for(int n=0;n<ids.Length;n++){
            heard="";var wired=ChiPlayer(town,0);var wire=Pack(wired,ids[n],1);OpenChiItem(wired,wire);
            Check(!Fenced(wired) && ChiItemUses(wired)==1 && wired.ChiPoints==pays[n] && ChiStored(wired)==pays[n] && heard.IndexOf(texts[n])>=0 && heard.IndexOf("You received 5 Chi Points!")==(n==0?0:-1),
                "1009/action-4 pays catalog Chi pill "+ids[n]+" exactly "+pays[n]);
            wired=ReloadBall(wired);ChiTable.Load(wired);
            Check(!Fenced(wired) && ChiItemUses(wired)==1 && wired.ChiPoints==pays[n],"catalog Chi pill "+ids[n]+" survives production reload");
            heard="";var direct=ChiPlayer(town,12);var directPack=Pack(direct,ids[n],1);ItemHandler.UseItem(directPack,direct);
            Check(!Fenced(direct) && direct.ChiPoints==12u+pays[n] && heard.IndexOf(texts[n])>=0,"direct UseItem pays catalog Chi pill "+ids[n]);
        }
        Check(heard.IndexOf("You received 400 Chi Points!")>=0 && File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/ChiPointItems.cs"))).IndexOf("You received 5 Chi Points!")>=0
            && File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/ChiPointItems.cs"))).IndexOf("Chi=400,Text=\"You received 400 Chi Points!\"")>=0,
            "729479 reports the committed 400 and 3000051 still reports 5");
        foreach(ushort size in new ushort[]{0,1,2}){
            var p=ChiPlayer(town,3);var pack=NestedPack(p,3000051,size,size==2);while(p.Inventory.Count<40)Pack(p,1000000,1);OpenChiItem(p,pack);
            Check(!Fenced(p) && ChiItemUses(p)==1 && p.ChiPoints==8 && p.Inventory.Count==(size<=1?39:40),"full bag needs no output slot for catalog pill size="+size);
        }
        var boundStack=ChiPlayer(town,1);var remainder=NestedPack(boundStack,729479,2,true);OpenChiItem(boundStack,remainder);
        Check(!Fenced(boundStack) && ChiItemUses(boundStack)==1 && remainder.StackSize==1 && remainder.Bound && boundStack.ChiPoints==401,
            "bound stacked 729479 pays 400 and keeps one bound unit");
        var twice=ChiPlayer(town,0);var fresh=NestedPack(twice,729476,2,false);OpenChiItem(twice,fresh);OpenChiItem(twice,fresh);
        Check(!Fenced(twice) && ChiItemUses(twice)==2 && twice.ChiPoints==200 && !twice.Inventory.ContainsUID(fresh.UID),"two fresh uses of a 729476 stack pay 200");
        var race=ChiPlayer(town,0);var raceItem=Pack(race,729477,1);
        Parallel.For(0,16,n=>{if(n%2==0)ChiTable.Save(race);else OpenChiItem(race,raceItem);});
        Check(!Fenced(race) && ChiItemUses(race)==1 && race.ChiPoints==200,"parallel packets and Chi saves cannot duplicate 729477");
        var above=ChiPlayer(town,5000);var aboveItem=Pack(above,3000051,1);OpenChiItem(above,aboveItem);
        Check(!Fenced(above) && above.ChiPoints==5005,"a balance above 4,000 still accepts a 5-point catalog pill");
        var edge=ChiPlayer(town,uint.MaxValue-400u);var edgeItem=Pack(edge,729479,1);OpenChiItem(edge,edgeItem);
        Check(!Fenced(edge) && edge.ChiPoints==uint.MaxValue && ChiStored(edge)==uint.MaxValue,"729479 pays the exact uint maximum");
        heard="";var over=ChiPlayer(town,uint.MaxValue-399u);var overItem=Pack(over,729479,1);OpenChiItem(over,overItem);
        Check(!Fenced(over) && ChiItemUses(over)==0 && over.Inventory.ContainsUID(overItem.UID) && over.ChiPoints==uint.MaxValue-399u
            && heard.IndexOf("Spend some Chi before using this item.")>=0,"one point past the uint maximum preserves 729479");
        foreach(string guard in new[]{"locked","suspicious","position","warehouse","wardrobe","expired","timer","trade","booth","dead","fenced","session","fake","loading"}){
            var p=ChiPlayer(town,9);var source=Pack(p,729478,1);
            if(guard=="locked")source.Lock=1;if(guard=="suspicious")source.Suspicious=true;if(guard=="position")source.Position=1;
            if(guard=="warehouse")source.Warehouse=8;if(guard=="wardrobe")source.InWardrobe=true;
            if(guard=="expired"){source.Days=1;source.DayStamp=chiItemNow.ToLocalTime().AddDays(-2);}if(guard=="timer")source.TimeLeftInMinutes=10;
            if(guard=="trade")p.Trade.InTrade=true;
            if(guard=="booth")p.Booth=(Revora.Game.ConquerStructures.Booth)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Revora.Game.ConquerStructures.Booth));
            if(guard=="dead")p.Entity.Hitpoints=0;if(guard=="fenced")typeof(Entity).GetField("PersistenceRequiresReload",Instance).SetValue(p.Entity,true);
            if(guard=="session")Kernel.GamePool[p.Entity.UID]=ChiPlayer(town,9);if(guard=="fake")p.Fake=true;if(guard=="loading")p.Action=1;
            ItemHandler.UseItem(source,p);
            Check(ChiItemUses(p)==0 && p.ChiPoints==9 && p.Inventory.ContainsUID(source.UID),"ordinary guard "+guard+" refuses 729478");
        }
        var foreign=ChiPlayer(town,0);var owned=Pack(foreign,3000051,1);var other=ChiPlayer(town,0);ItemHandler.UseItem(owned,other);
        Check(ChiItemUses(foreign)==0 && ChiItemUses(other)==0 && foreign.Inventory.ContainsUID(owned.UID),"foreign direct catalog pill cannot be spent");
        foreach(string bad in new[]{"short","long","length","type","seal"}){
            var frames=ChiPlayer(town,0);var item=Pack(frames,729476,1);
            byte[] frame=bad=="short"?new byte[31]:bad=="long"?new byte[129]:PrayerPacket(item.UID);
            Writer.WriteUInt16((ushort)(frame.Length-8),0,frame);Writer.WriteUInt16(1009,2,frame);Writer.WriteUInt32(item.UID,8,frame);Writer.WriteUInt16(4,20,frame);Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);
            if(bad=="length")Writer.WriteUInt16(1,0,frame);if(bad=="type")Writer.WriteUInt16(1008,2,frame);if(bad=="seal")frame[frame.Length-1]^=1;
            ItemUsage.Process(frame,frames);Check(!Fenced(frames) && ChiItemUses(frames)==0 && frames.Inventory.ContainsUID(item.UID),"bounded 729476 rejects "+bad+" frame");
        }
        foreach(string change in new[]{"stacksize=99","bound=1","locked=1","suspicious=1","position=1","warehouse=8","inwardrobe=1","days='1'","entityid=1","id=1088001"}){
            var p=ChiPlayer(town,4);var source=Pack(p,3000051,1);Query("UPDATE items SET "+change+" WHERE uid=@uid","uid",(long)source.UID);OpenChiItem(p,source);
            Check(Fenced(p) && ChiItemUses(p)==0 && p.ChiPoints==4 && ChiStored(p)==4,"stale durable source "+change+" fences 3000051");
        }
        var stalePoints=ChiPlayer(town,15);var stalePill=Pack(stalePoints,729476,1);Query("UPDATE chi SET points=16 WHERE uid=@uid","uid",(long)stalePoints.Entity.UID);OpenChiItem(stalePoints,stalePill);
        Check(Fenced(stalePoints) && ChiItemUses(stalePoints)==0 && stalePoints.ChiPoints==15 && ChiStored(stalePoints)==16,"stale durable Chi wallet fences 729476");
        var missingWallet=ChiPlayer(town,0);var missingPill=Pack(missingWallet,729477,1);Query("DELETE FROM chi WHERE uid=@uid","uid",(long)missingWallet.Entity.UID);OpenChiItem(missingWallet,missingPill);
        Check(Fenced(missingWallet) && ChiItemUses(missingWallet)==0 && missingWallet.Inventory.ContainsUID(missingPill.UID),"missing Chi wallet cannot pay 729477");
        foreach(bool extra in new[]{false,true}){
            var p=ChiPlayer(town,0);var source=Pack(p,729478,1);var filler=Pack(p,1088001,1);
            if(extra)p.Inventory.Remove(filler.UID,Enums.ItemUse.None,true);else Query("DELETE FROM items WHERE uid=@uid","uid",(long)filler.UID);
            OpenChiItem(p,source);Check(Fenced(p) && ChiItemUses(p)==0 && p.ChiPoints==0,"complete durable bag rejects "+(extra?"extra":"missing")+" catalog-pill row");
        }
        var rebound=ChiPlayer(town,2);var staleBound=Pack(rebound,3000051,1);Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)staleBound.UID);OpenChiItem(rebound,staleBound);
        rebound=ReloadBall(rebound);ChiTable.Load(rebound);OpenChiItem(rebound,rebound.Inventory.Objects.Single(i=>i.ID==3000051));rebound=ReloadBall(rebound);ChiTable.Load(rebound);
        Check(!Fenced(rebound) && rebound.ChiPoints==7 && ChiItemUses(rebound)==1,"reload retries stale catalog-pill binding without changing the 5-point award");
        foreach(string fault in new[]{"delete","update","chi","receipt"}){
            var p=ChiPlayer(town,20);var source=Pack(p,729479,(ushort)(fault=="update"?2:1));
            string table=fault=="receipt"?"revora_chi_item_uses":fault=="chi"?"chi":"items";
            string op=fault=="delete"?"DELETE":fault=="receipt"?"INSERT":"UPDATE";
            Query("CREATE FUNCTION suppress_catalog_chi() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_catalog_chi BEFORE "+op+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_catalog_chi()");
            try{heard="";OpenChiItem(p,source);Check(Fenced(p) && ChiItemUses(p)==0 && p.Inventory.ContainsUID(source.UID) && ChiStored(p)==20 && heard.IndexOf("You received 400")<0,"silent "+fault+" rolls back 729479 without a success message");}
            finally{Query("DROP TRIGGER suppress_catalog_chi ON "+table+";DROP FUNCTION suppress_catalog_chi()");}
            p=ReloadBall(p);ChiTable.Load(p);Check(p.ChiPoints==20 && p.Inventory.Objects.Any(i=>i.ID==729479),"reload proves 729479 rollback after "+fault);
            OpenChiItem(p,p.Inventory.Objects.Single(i=>i.ID==729479));p=ReloadBall(p);ChiTable.Load(p);
            Check(!Fenced(p) && ChiItemUses(p)==1 && p.ChiPoints==420,"reload/retry commits 400 Chi after "+fault);
        }
        var install=ChiPlayer(town,6);var bound=NestedPack(install,729479,3,true);
        ChiItems.GetField("BeforeInstall",Hidden).SetValue(null,(Action<GameState>)(g=>{throw new InvalidOperationException("installation");}));
        try{heard="";OpenChiItem(install,bound);OpenChiItem(install,bound);ChiTable.Save(install);
            Check(Fenced(install) && ChiItemUses(install)==1 && bound.StackSize==3 && install.ChiPoints==6 && heard.IndexOf("You received 400")<0,
                "post-commit 729479 failure fences the stale projection and does not announce or duplicate the award");}
        finally{ChiItems.GetField("BeforeInstall",Hidden).SetValue(null,null);}
        install=ReloadBall(install);ChiTable.Load(install);
        var kept=install.Inventory.Objects.Single(i=>i.UID==bound.UID);
        Check(!Fenced(install) && ChiItemUses(install)==1 && install.ChiPoints==406 && ChiStored(install)==406 && kept.StackSize==2 && kept.Bound,
            "reload recovers 400 Chi and the exact bound 729479 remainder");
        ChiTable.Save(install);install=ReloadBall(install);ChiTable.Load(install);
        Check(!Fenced(install) && install.ChiPoints==406 && ChiStored(install)==406 && ChiItemUses(install)==1,"a later Chi save cannot roll back the committed 400");
    }
}
