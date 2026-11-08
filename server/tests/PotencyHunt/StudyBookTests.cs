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
    static readonly Type StudyBooks=typeof(Entity).Assembly.GetType("Revora.Game.Features.StudyBooks");
    static readonly Type HonorBuys=typeof(Entity).Assembly.GetType("Revora.Game.Features.HonorPurchases");
    static DateTime studyBookNow=DateTime.UtcNow;
    static long StudyBookUses(GameState p){return Scalar("SELECT count(*) FROM revora_study_book_uses WHERE uid=@uid",p.Entity.UID);}
    static void ReadStudyBook(GameState p,ConquerItem item){ItemUsage.Process(PrayerPacket(item.UID),p);if(p.MessageOK!=null)p.MessageOK(p);}
    static void VerifyStudyBooks(string runtime,Map town){
        Query("CREATE TABLE IF NOT EXISTS teamarena (LIKE public.teamarena INCLUDING ALL);CREATE TABLE IF NOT EXISTS itemadding (LIKE public.itemadding INCLUDING ALL)");
        Call(StudyBooks,null,"Initialize");Call(StudyBooks,null,"Initialize");
        Call(HonorBuys,null,"Initialize");Call(HonorBuys,null,"Initialize");
        StudyBooks.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>studyBookNow));
        HonorShop.Load();
        string handler=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemHandler.cs")));
        string service=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/StudyBooks.cs")));
        string honor=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/HonorPurchases.cs")));
        string shop=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Database/Shops/HonorShop.cs")));
        string server=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Server.cs")));
        string invite=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/AutoInvite.cs")));
        string box=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Client/GameState.cs")));
        string daily=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/ArenaDailyRewards.cs")));
        string qualifier=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/Tournaments/Qualifier.cs")));
        string packets=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/PacketHandler.cs")));
        string audit=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../docs/ITEM_USE_COMPOUND_AUDIT.md")));
        string catalog=File.ReadAllText("database\\itemtype-new.txt");
        string honorIni=File.ReadAllText("database\\shops\\HonorShop.ini");
        Check(handler.IndexOf("Study book must use the owned service.")>=0 && handler.IndexOf("StudyBooks.TryUse")>=0
            && handler.IndexOf("case 720774:")>=0 && handler.IndexOf("case 720775:")>=0,
            "study books are fail-closed on the live handler");
        Check(handler.IndexOf("GetPositionFromID")>=0 && handler.IndexOf("Inventory = 0")>=0,
            "ordinary 1009/action-4 still dispatches non-equipment IDs through UseItem");
        int switchStart=handler.IndexOf("switch (item.ID)"),switchEnd=handler.IndexOf("public static byte GetNextRefineryItem",switchStart);
        var sourceIds=System.Text.RegularExpressions.Regex.Matches(handler.Substring(switchStart,switchEnd-switchStart),@"(?m)^ {16}case (\d+):").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        var auditIds=System.Text.RegularExpressions.Regex.Matches(audit,@"(?m)^\| (\d+) \|").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        Check(sourceIds.Length==518 && sourceIds.OrderBy(id=>id).SequenceEqual(auditIds.OrderBy(id=>id)),
            "compound audit still enumerates the exact 518 numeric top-level UseItem case IDs");
        Check(audit.IndexOf("| live-split-write | 254 |")>=0 && audit.IndexOf("| owned-fail-closed | 96 |")>=0,
            "census is 254 live-split-write / 96 owned-fail-closed after two study books");
        Check(audit.IndexOf("| 720774 | owned-fail-closed |")>=0 && audit.IndexOf("| 720775 | owned-fail-closed |")>=0,
            "audit owns 720774 and 720775");
        Check(ConquerItemInformation.BaseInformations.ContainsKey(720774u) && ConquerItemInformation.BaseInformations.ContainsKey(720775u),
            "installed catalog contains both study books");
        Check(catalog.IndexOf("720774@@StudyBook(Arena)")>=0 && catalog.IndexOf("720775@@StudyBook(Race)")>=0
            && catalog.IndexOf("You~can~only~use~such~a~book~once~a~day.")>=0,
            "catalog identities are StudyBook(Arena/Race) and advertise once a day");
        Check(service.IndexOf("Do~you~want~to~read~this~book~and~gain~50~Study~points?")>=0
            && service.IndexOf("You received 50 Study Points!")>=0 && service.IndexOf("Color.Red")>=0
            && service.IndexOf("zf2-e300")>=0 && service.IndexOf("StudyReceive=Award")>=0,
            "owned service preserves leftover prompt, +50 study, Color.Red text and zf2-e300");
        Check(service.IndexOf("CHECK(study=50)")>=0 && service.IndexOf("once_per_day")<0 && service.IndexOf("revora_study_book_days")<0
            && handler.IndexOf("Study book must use the owned service.")>=0,
            "leftover executable has no day ledger; owned service does not invent one");
        Check(invite.IndexOf("public static bool Enabled = false;")>=0
            && box.IndexOf("if (!AutoInvite.Enabled && msg_ok != null)")>=0,
            "leftover GameState.MessageBox no-ops while AutoInvite.Enabled stays false");
        Check(server.IndexOf("Database.HonorShop.Load();")>=0 && System.Text.RegularExpressions.Regex.Replace(server,@"//.*"," ").IndexOf("HonorShop.Load()")>=0
            && shop.IndexOf("public const uint UID = 6000;")>=0,
            "installed startup loads HonorShop UID 6000");
        Check(handler.IndexOf("if (itemUsage.UID == Database.HonorShop.UID)")>=0
            && handler.IndexOf("if (itemUsage.UID == Database.HonorShop.UID)")<handler.IndexOf("client.Map.Npcs.TryGetValue(itemUsage.UID, out npc)")
            && handler.IndexOf("HonorPurchases.Buy(client, itemUsage.dwParam)")>=0,
            "dedicated HonorShop buy runs before the generic map-NPC gate");
        Check(HonorShop.Shop.Items[720774]==10000u && HonorShop.Shop.Items[720775]==1500u
            && honorIni.IndexOf("720774,10000,0")>=0 && honorIni.IndexOf("720775,1500,0")>=0,
            "loaded HonorShop stocks 720774 at 10000 Honor and 720775 at 1500 Honor unbound");
        Check(honor.IndexOf("Q.New(type,false)")>=0,"HonorPurchases still issues unbound Honor stock");
        Check(daily.IndexOf("p.CurrentHonor=before+500")>=0 && qualifier.IndexOf("winner.CurrentHonor += 500")>=0,
            "ordinary Arena daily reward and 100-win Qualifier still award Honor");
        Check(packets.IndexOf("if (req.OptionID == 255 && client.MessageOK != null)")>=0,
            "NpcDialog OptionID 255 still invokes MessageOK");
        string heard="";StudyBooks.GetField("Spoken",Hidden).SetValue(null,(Action<string>)(m=>{heard=heard+m+"|";}));
        uint[] ids=new uint[]{720774u,720775u};
        for(int n=0;n<ids.Length;n++){
            heard="";var p=WelcomePlayer(town,1,0,15);int before=p.Entity.SubClasses.StudyPoints;var item=Pack(p,ids[n],1);ReadStudyBook(p,item);
            Check(!Fenced(p) && StudyBookUses(p)==1 && p.Entity.SubClasses.StudyPoints==before+50 && !p.Inventory.ContainsUID(item.UID)
                && heard.IndexOf("You received 50 Study Points!")>=0,
                "study book "+ids[n]+" pays 50 study after confirm");
            p=ReloadBall(p);
            Check(StudyBookUses(p)==1 && p.Entity.SubClasses.StudyPoints==before+50 && StudyStored(p)==before+50,
                "study book "+ids[n]+" survives production reload");
        }
        foreach(bool bound in new[]{false,true})foreach(ushort stack in new ushort[]{0,1,2}){
            var p=WelcomePlayer(town,1,0,15);var pack=NestedPack(p,720775,stack,bound);int before=p.Entity.SubClasses.StudyPoints;
            Parallel.For(0,24,n=>{if(n%3==0)EntityTable.SaveEntity(p);else ReadStudyBook(p,pack);});
            int expected=stack>1?2:1;
            Check(!Fenced(p) && StudyBookUses(p)==expected && p.Entity.SubClasses.StudyPoints==before+50*expected && CountId(p,720775)==0,
                "720775 pays 50 study with stack remainder semantics: "+bound+"/"+stack);
        }
        var cancel=WelcomePlayer(town,1,0,15);var cancelPack=Pack(cancel,720774,1);ItemUsage.Process(PrayerPacket(cancelPack.UID),cancel);
        Check(cancel.MessageOK!=null && cancel.MessageCancel!=null,"study book presents a confirm offer");
        cancel.MessageCancel(cancel);
        Check(StudyBookUses(cancel)==0 && cancel.Inventory.ContainsUID(cancelPack.UID) && cancel.Entity.SubClasses.StudyPoints==0,
            "cancel leaves the study book and study unchanged");
        var replaced=WelcomePlayer(town,1,0,15);var first=Pack(replaced,720774,1);var second=Pack(replaced,720775,1);
        ItemUsage.Process(PrayerPacket(first.UID),replaced);var stale=replaced.MessageOK;
        ItemUsage.Process(PrayerPacket(second.UID),replaced);
        if(stale!=null)stale(replaced);
        Check(StudyBookUses(replaced)==0 && replaced.Inventory.ContainsUID(first.UID),"replaced study-book offer cannot claim the first book");
        replaced.MessageOK(replaced);
        Check(!Fenced(replaced) && StudyBookUses(replaced)==1 && !replaced.Inventory.ContainsUID(second.UID) && replaced.Inventory.ContainsUID(first.UID),
            "the replacement offer still pays the second study book");
        var expired=WelcomePlayer(town,1,0,15);var expiredPack=Pack(expired,720774,1);
        ItemUsage.Process(PrayerPacket(expiredPack.UID),expired);studyBookNow=studyBookNow.AddMinutes(2);if(expired.MessageOK!=null)expired.MessageOK(expired);
        Check(StudyBookUses(expired)==0 && expired.Inventory.ContainsUID(expiredPack.UID),"expired study-book offer pays nothing");
        studyBookNow=studyBookNow.AddMinutes(-2);
        heard="";var overflow=WelcomePlayer(town,1,0,15);overflow.Entity.SubClasses.StudyPoints=ushort.MaxValue;var rich=Pack(overflow,720774,1);ReadStudyBook(overflow,rich);
        Check(!Fenced(overflow) && StudyBookUses(overflow)==0 && overflow.Inventory.ContainsUID(rich.UID) && overflow.Entity.SubClasses.StudyPoints==ushort.MaxValue
            && heard.IndexOf("Spend some Study Points before opening this pack.")>=0,"checked ushort overflow preserves the study book");
        var boundary=WelcomePlayer(town,1,0,15);boundary.Entity.SubClasses.StudyPoints=ushort.MaxValue-50;EntityTable.SaveEntity(boundary);
        var boundaryBook=Pack(boundary,720774,1);ReadStudyBook(boundary,boundaryBook);
        Check(!Fenced(boundary) && StudyBookUses(boundary)==1 && !boundary.Inventory.ContainsUID(boundaryBook.UID)
            && boundary.Entity.SubClasses.StudyPoints==ushort.MaxValue && StudyStored(boundary)==ushort.MaxValue,
            "exact ushort maximum boundary consumes once and persists the full Study balance");
        var uncapped=WelcomePlayer(town,1,0,15);var arenaBook=Pack(uncapped,720774,1);var raceBook=Pack(uncapped,720775,1);
        ReadStudyBook(uncapped,arenaBook);ReadStudyBook(uncapped,raceBook);
        Check(!Fenced(uncapped) && StudyBookUses(uncapped)==2 && uncapped.Entity.SubClasses.StudyPoints==100
            && !uncapped.Inventory.ContainsUID(arenaBook.UID) && !uncapped.Inventory.ContainsUID(raceBook.UID),
            "both Study-book IDs remain usable in one session without an invented shared daily cap");
        foreach(string fault in new[]{"source","study","receipt"}){
            heard="";var p=WelcomePlayer(town,1,0,15);var item=Pack(p,720775,1);
            string table=fault=="receipt"?"revora_study_book_uses":fault=="source"?"items":"entities";
            string verb=fault=="source"?"DELETE":fault=="receipt"?"INSERT":"UPDATE";
            string body=fault=="study"?"IF NEW.studypoints IS DISTINCT FROM OLD.studypoints THEN RETURN NULL; END IF; RETURN NEW;":"RETURN NULL;";
            Query("CREATE FUNCTION suppress_study_book() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN "+body+" END';CREATE TRIGGER suppress_study_book BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_study_book()");ReadStudyBook(p,item);
            Check(Fenced(p) && StudyBookUses(p)==0 && p.Inventory.ContainsUID(item.UID) && p.Entity.SubClasses.StudyPoints==0 && heard.IndexOf("You received 50 Study Points!")<0,
                "suppressed "+fault+" preserves the study book and does not send success text");
            Query("DROP TRIGGER suppress_study_book ON "+table+";DROP FUNCTION suppress_study_book()");p=ReloadBall(p);ReadStudyBook(p,p.Inventory.Objects.Single(i=>i.ID==720775));
            Check(!Fenced(p) && StudyBookUses(p)==1 && p.Entity.SubClasses.StudyPoints==50,"production reload retries the study book after "+fault+" rollback");
        }
        var durableReplacement=WelcomePlayer(town,1,0,15);var offeredBook=Pack(durableReplacement,720774,1);
        ItemUsage.Process(PrayerPacket(offeredBook.UID),durableReplacement);
        Query("UPDATE items SET id=1000000 WHERE uid=@uid","uid",(long)offeredBook.UID);if(durableReplacement.MessageOK!=null)durableReplacement.MessageOK(durableReplacement);
        Check(Fenced(durableReplacement) && StudyBookUses(durableReplacement)==0 && durableReplacement.Entity.SubClasses.StudyPoints==0
            && Scalar("SELECT id FROM items WHERE uid=@uid",offeredBook.UID)==1000000,
            "same-UID durable source replacement between offer and confirm cannot consume or pay a Study book");
        Query("UPDATE items SET id=720774 WHERE uid=@uid","uid",(long)offeredBook.UID);durableReplacement=ReloadBall(durableReplacement);
        ReadStudyBook(durableReplacement,durableReplacement.Inventory.Objects.Single(i=>i.ID==720774));
        Check(!Fenced(durableReplacement) && StudyBookUses(durableReplacement)==1 && durableReplacement.Entity.SubClasses.StudyPoints==50,
            "restored Study-book source retries once after production reload");
        var extraBag=WelcomePlayer(town,1,0,15);var extraSrc=Pack(extraBag,720774,1);uint extraUid=ConquerItem.ItemUID.Next;
        Query("INSERT INTO items(uid,id,entityid,stacksize) VALUES(@uid,@id,@owner,1)","uid",(long)extraUid,"id",1000000,"owner",(long)extraBag.Entity.UID);ReadStudyBook(extraBag,extraSrc);
        Check(Fenced(extraBag) && StudyBookUses(extraBag)==0 && extraBag.Inventory.ContainsUID(extraSrc.UID),"an extra durable bag UID cannot read a study book");
        Query("DELETE FROM items WHERE uid=@uid","uid",(long)extraUid);extraBag=ReloadBall(extraBag);ReadStudyBook(extraBag,extraBag.Inventory.Objects.Single(i=>i.ID==720774));
        Check(!Fenced(extraBag) && StudyBookUses(extraBag)==1,"production reload retries after the extra durable bag UID is removed");
        var missingBag=WelcomePlayer(town,1,0,15);var missingSrc=Pack(missingBag,720774,1);var cacheOnly=Pack(missingBag,1000000,1);
        Query("DELETE FROM items WHERE uid=@uid","uid",(long)cacheOnly.UID);ReadStudyBook(missingBag,missingSrc);
        Check(Fenced(missingBag) && StudyBookUses(missingBag)==0 && missingBag.Inventory.ContainsUID(missingSrc.UID),"a cache-only bag UID cannot read a study book");
        missingBag=ReloadBall(missingBag);ReadStudyBook(missingBag,missingBag.Inventory.Objects.Single(i=>i.ID==720774));
        Check(!Fenced(missingBag) && StudyBookUses(missingBag)==1,"production reload retries after the cache-only bag UID is dropped");
        var frames=WelcomePlayer(town,1,0,15);var wire=Pack(frames,720775,1);
        foreach(string bad in new[]{"short","long","length","type","seal"}){
            byte[] frame=bad=="short"?new byte[31]:bad=="long"?new byte[129]:PrayerPacket(wire.UID);
            Writer.WriteUInt16((ushort)(frame.Length-8),0,frame);Writer.WriteUInt16(1009,2,frame);Writer.WriteUInt32(wire.UID,8,frame);Writer.WriteUInt16(4,20,frame);Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);
            if(bad=="length")Writer.WriteUInt16(1,0,frame);if(bad=="type")Writer.WriteUInt16(1008,2,frame);if(bad=="seal")frame[frame.Length-1]^=1;
            ItemUsage.Process(frame,frames);Check(!Fenced(frames) && StudyBookUses(frames)==0 && frames.Inventory.ContainsUID(wire.UID),"bounded study book rejects "+bad+" frame");
        }ReadStudyBook(frames,wire);Check(!Fenced(frames) && StudyBookUses(frames)==1,"ordinary study book works after malformed frames");
        var replay=WelcomePlayer(town,1,0,15);var once=Pack(replay,720774,1);uint uid=once.UID;ReadStudyBook(replay,once);
        ItemHandler.UseItem(once,replay);if(replay.MessageOK!=null)replay.MessageOK(replay);
        Check(StudyBookUses(replay)==1 && !replay.Inventory.ContainsUID(uid),"consumed study book UID cannot replay");
        var direct=WelcomePlayer(town,1,0,15);var directPack=Pack(direct,720775,1);ItemHandler.UseItem(directPack,direct);if(direct.MessageOK!=null)direct.MessageOK(direct);
        Check(!Fenced(direct) && StudyBookUses(direct)==1 && direct.Entity.SubClasses.StudyPoints==50,"direct UseItem opens 720775");
        var twice=WelcomePlayer(town,1,0,15);var twicePack=Pack(twice,720774,1);ReadStudyBook(twice,twicePack);if(twice.MessageOK!=null)twice.MessageOK(twice);
        Check(StudyBookUses(twice)==1 && twice.Entity.SubClasses.StudyPoints==50,"double confirm cannot pay a second study award");
        var buyer=HonorBuyer(town,10000);HonorBuy(buyer,720774);HonorBuy(buyer,720775);
        Check(!Fenced(buyer) && CountId(buyer,720774)==1 && buyer.CurrentHonor==0
            && Scalar("SELECT count(*) FROM revora_honor_purchases WHERE uid=@uid AND item_type=720774",buyer.Entity.UID)==1,
            "ordinary HonorShop UID 6000 purchase issues unbound 720774");
        buyer.CurrentHonor=1500;HonorBuy(buyer,720775);
        Check(CountId(buyer,720775)==1 && buyer.CurrentHonor==0,"ordinary HonorShop purchase issues unbound 720775");
        var duplicateBuyer=HonorBuyer(town,25000);HonorBuy(duplicateBuyer,720774);HonorBuy(duplicateBuyer,720774);
        Check(!Fenced(duplicateBuyer) && CountId(duplicateBuyer,720774)==1 && duplicateBuyer.CurrentHonor==15000
            && Scalar("SELECT count(*) FROM revora_honor_purchases WHERE uid=@uid AND item_type=720774",duplicateBuyer.Entity.UID)==1,
            "duplicate HonorShop packets issue and debit exactly one Study book");
        var bought=buyer.Inventory.Objects.Single(i=>i.ID==720774);ReadStudyBook(buyer,bought);
        Check(!Fenced(buyer) && StudyBookUses(buyer)==1 && buyer.Entity.SubClasses.StudyPoints==50,"Honor-issued 720774 redeems through the owned service");
        var skip=WelcomePlayer(town,1,0,15);var skipPack=Pack(skip,722178,1);
        Check(!(bool)Call(StudyBooks,null,"TryUse",skipPack,skip) && skip.Inventory.ContainsUID(skipPack.UID),"Surprise box 722178 stays outside StudyBooks");
        StudyBooks.GetField("Spoken",Hidden).SetValue(null,null);
    }
}
