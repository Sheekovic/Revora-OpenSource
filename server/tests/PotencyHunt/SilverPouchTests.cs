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
    static readonly Type SilverPurses=typeof(Entity).Assembly.GetType("Revora.Game.Features.SilverPouches");
    static DateTime silverPouchNow=DateTime.UtcNow;
    static long SilverPouchUses(GameState p){return Scalar("SELECT count(*) FROM revora_silver_pouch_uses WHERE uid=@uid",p.Entity.UID);}
    static void OpenSilverPouch(GameState p,ConquerItem item){ItemUsage.Process(PrayerPacket(item.UID),p);}
    static void VerifySilverPouches(string runtime,Map town){
        Call(SilverPurses,null,"Initialize");Call(SilverPurses,null,"Initialize");
        SilverPurses.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>silverPouchNow));
        string handler=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemHandler.cs")));
        string service=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/SilverPouches.cs")));
        string audit=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../docs/ITEM_USE_COMPOUND_AUDIT.md")));
        string npcs=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Npcs.cs")));
        string entity=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Entity.cs")));
        string monsters=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Database/MonsterTable.cs")));
        string shelby=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/ShelbyRewards.cs")));
        string chat=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/Chat.cs")));
        string data=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/Data.cs")));
        string catalog=File.ReadAllText("database\\itemtype-new.txt");
        string shop=File.ReadAllText("database\\shops\\SlayerPointshop.ini");
        string server=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Server.cs")));
        string constants=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../ServerBase/Constants.cs")));
        string thread=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Thread.cs")));
        string v2=File.ReadAllText("database\\shops\\emoneyshopV2.ini");
        string eshop=File.ReadAllText("database\\shops\\emoneyshop.ini");
        string shopTxt=File.ReadAllText("database\\shops\\Shop.txt");
        string exchange=File.ReadAllText("database\\shops\\exchange_shop_goods.txt");
        string heroes=File.ReadAllText("database\\Slayer\\StageGoal.txt");
        string lottery=File.ReadAllText("database\\task_reward_type.ini");
        Check(handler.IndexOf("Silver pouch must use the owned service.")>=0 && handler.IndexOf("SilverPouches.TryUse")>=0
            && handler.IndexOf("case 3006594:")>=0 && handler.IndexOf("case 723721:")>=0 && handler.IndexOf("case 723723:")>=0,
            "proved silver pouches are fail-closed on the live handler");
        Check(handler.IndexOf("GetPositionFromID")>=0 && handler.IndexOf("Inventory = 0")>=0,
            "ordinary 1009/action-4 still dispatches non-equipment IDs through UseItem");
        int switchStart=handler.IndexOf("switch (item.ID)"),switchEnd=handler.IndexOf("public static byte GetNextRefineryItem",switchStart);
        var sourceIds=System.Text.RegularExpressions.Regex.Matches(handler.Substring(switchStart,switchEnd-switchStart),@"(?m)^ {16}case (\d+):").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        var auditIds=System.Text.RegularExpressions.Regex.Matches(audit,@"(?m)^\| (\d+) \|").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        Check(sourceIds.Length==518 && sourceIds.OrderBy(id=>id).SequenceEqual(auditIds.OrderBy(id=>id)),
            "compound audit still enumerates the exact 518 numeric top-level UseItem case IDs");
        Check(audit.IndexOf("| live-split-write | 256 |")>=0 && audit.IndexOf("| owned-fail-closed | 94 |")>=0,
            "census is 256 live-split-write / 94 owned-fail-closed after three proved silver pouches");
        uint[] owned=new uint[]{3006594u,723721u,723723u};
        ulong[] pays=new ulong[]{200000UL,100000000UL,200000000000UL};
        string[] names=new string[]{"MagicSpar(200Thousand)","Class9MoneyBag","TopMoneyBag"};
        for(int n=0;n<owned.Length;n++){
            Check(audit.IndexOf("| "+owned[n].ToString()+" | owned-fail-closed |")>=0,"audit owns silver pouch "+owned[n]);
            Check(ConquerItemInformation.BaseInformations.ContainsKey(owned[n]),"installed catalog contains silver pouch "+owned[n]);
            Check(catalog.IndexOf(owned[n].ToString()+"@@"+names[n])>=0,"catalog identity for "+owned[n]+" is "+names[n]);
            Check(service.IndexOf("{"+owned[n].ToString()+","+pays[n].ToString()+"UL}")>=0,"owned service pays leftover "+pays[n]+" for "+owned[n]);
        }
        Check(catalog.IndexOf("Right~click~on~it~to~get~500000000~silvers.")>=0,
            "723723 catalog copy names 500,000,000 silver while leftover pays 200,000,000,000");
        Check(PublicScalar("SELECT count(*) FROM public.npcs WHERE id=30000 AND mapid=1036")==1,
            "installed public.npcs has Shelby 30000 on market 1036");
        Check(shelby.IndexOf("kind==1?1088001u:723723u")>=0 && shelby.IndexOf("Q.Near(p,30000,1036)")>=0,
            "ShelbyRewards issues unbound 723723 on NPC 30000 map 1036");
        Check(PublicScalar("SELECT count(*) FROM public.sobnpcs WHERE id=900043 AND mapid=1002")==1,
            "installed public.sobnpcs has ChiJiang clerk 900043 on Twin City");
        Check(npcs.IndexOf("const uint MagicSpar = 3006594;")>=0 && npcs.IndexOf("case 900043:")>=0,
            "ChiJiang clerk 900043 exchanges hunt packs for Magic Spar 3006594");
        Check(entity.IndexOf("MonsterInfo.Drop(killer)")>=0 && monsters.IndexOf("ItemID = 723721;// Lvl 9 Wallet")>=0
            && monsters.IndexOf("Name == \"FatDemon\"")>=0 && monsters.IndexOf("Name == \"FuriousBear\"")>=0,
            "live MonsterTable.Drop SuperBoss path can floor-drop 723721");
        Check(PublicScalar("SELECT count(*) FROM public.monsterspawns s JOIN public.monsterinfos m ON m.id=s.npctype WHERE m.name='FuriousBear'")>=1,
            "installed world spawns FuriousBear for the SuperBoss 723721 drop");
        Check(chat.IndexOf("case \"primercredito\":")>=0 && data.IndexOf("AddBoundItem(3001266, 0, 1);//Fortune Bag")>=0
            && data.IndexOf("Sorry, but the GM has not granted you the welcome gift.")>=0,
            "3001266 FirstCredit is granted only after GM primercredito points");
        uint[] leftover=new uint[]{3001266u,723722u,3007365u,3007366u,3007367u,3007368u};
        for(int n=0;n<leftover.Length;n++){
            Check(audit.IndexOf("| "+leftover[n].ToString()+" | live-split-write | source+silver |")>=0,"classified silver ID "+leftover[n]+" stays live-split-write");
            Check(ConquerItemInformation.BaseInformations.ContainsKey(leftover[n]),"installed catalog contains classified silver ID "+leftover[n]);
            Check(service.IndexOf(leftover[n].ToString())<0,"owned silver pouch service excludes classified ID "+leftover[n]);
        }
        Check(handler.IndexOf("AdjustMoney((decimal)(200000000))")>=0 && handler.IndexOf("You claimed 200,000,000 gold.")>=0,
            "leftover 3001266 still pays 200,000,000 gold with the exact claim text");
        Check(handler.IndexOf("AdjustMoney((decimal)(300000000))")>=0 && handler.IndexOf("client.Inventory.Remove(723722, 1)")>=0,
            "leftover 723722 still pays 300,000,000 silver");
        Check(handler.IndexOf("AdjustMoney((decimal)(2000000000))")>=0 && handler.IndexOf("AdjustMoney((decimal)(50000000000))")>=0
            && handler.IndexOf("AdjustMoney((decimal)(250000000000))")>=0 && handler.IndexOf("AdjustMoney((decimal)(500000000000))")>=0,
            "leftover 3007365-3007368 still pay their executable silver amounts");
        Check(eshop.IndexOf("3001266")<0 && v2.IndexOf("3001266")<0 && shopTxt.IndexOf("3001266")<0 && exchange.IndexOf("3001266")<0 && heroes.IndexOf("3001266")<0,
            "3001266 has no shop, exchange or StageGoal producer");
        Check(eshop.IndexOf("723722")<0 && v2.IndexOf("723722")<0 && shopTxt.IndexOf("723722")<0 && exchange.IndexOf("723722")<0 && heroes.IndexOf("723722")<0
            && lottery.IndexOf("723722")>=0,
            "723722 has no shop, exchange or StageGoal producer; task_reward_type.ini lists it");
        Check(shop.IndexOf("3007366 10000")>=0 && shop.IndexOf("3007367 200000")>=0 && shop.IndexOf("3007365 600000")>=0 && shop.IndexOf("3007368 1000000")>=0,
            "SlayerPointshop.ini stocks 3007365-3007368");
        Check(constants.IndexOf("RaceShopPath = \"database\\\\shops\\\\SlayerPointshop.ini\"")>=0,
            "RaceShopPath is database\\\\shops\\\\SlayerPointshop.ini");
        int shopUid=handler.IndexOf("if (itemUsage.UID == Database.REVORAPointhop.UID)");
        Check(shopUid>=0 && shopUid<handler.IndexOf("client.Map.Npcs.TryGetValue(itemUsage.UID, out npc)"),
            "dedicated UID 6001 buy runs before the generic map-NPC gate");
        Check(handler.IndexOf("if (Database.REVORAPointhop.Shop == null || Database.REVORAPointhop.Shop.Items == null)")>=0,
            "dedicated UID 6001 buy fail-closes when the shop is uninitialized");
        Check(server.IndexOf("//  Database.RevoraPointhop.Load();")>=0
            && System.Text.RegularExpressions.Regex.Replace(server,@"//.*"," ").IndexOf("Pointhop.Load")<0
            && thread.IndexOf("Pointhop.Load")<0,
            "installed startup comments out the only REVORAPointhop.Load call");
        var pointShop=typeof(Entity).Assembly.GetType("Revora.Database.REVORAPointhop");
        var shopFields=System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static;
        Check((uint)pointShop.GetField("UID",shopFields).GetValue(null)==6001u && pointShop.GetField("Shop",shopFields).GetValue(null)==null,
            "REVORAPointhop.UID is 6001 and Shop is uninitialized in this process");
        Check(PublicScalar("SELECT count(*) FROM public.npcs WHERE id=6001")+PublicScalar("SELECT count(*) FROM public.sobnpcs WHERE id=6001")==0,
            "installed database has no NPC/SOBNPC 6001 as secondary evidence");
        uint[] slayerBags=new uint[]{3007365u,3007366u,3007367u,3007368u};
        for(int n=0;n<slayerBags.Length;n++){
            var buyer=WelcomePlayer(town,1,0,15);buyer.Entity.RevoraPoint=5000000;uint rvp=buyer.Entity.RevoraPoint;
            ItemHandler.HandleBuyFromNPC(new ItemUsage(true){UID=6001,dwParam=slayerBags[n],dwExtraInfo=1},buyer);
            Check(!Fenced(buyer) && CountId(buyer,slayerBags[n])==0 && buyer.Entity.RevoraPoint==rvp,
                "ordinary UID 6001 buy of "+slayerBags[n]+" issues nothing and does not debit when the dedicated shop is uninitialized");
        }
        string heard="";SilverPurses.GetField("Spoken",Hidden).SetValue(null,(Action<string>)(m=>{heard=heard+m+"|";}));
        for(int n=0;n<owned.Length;n++){
            heard="";var p=WelcomePlayer(town,1,0,15);ulong before=p.Entity.Money;var item=Pack(p,owned[n],1);OpenSilverPouch(p,item);
            Check(!Fenced(p) && SilverPouchUses(p)==1 && p.Entity.Money==before+pays[n] && !p.Inventory.ContainsUID(item.UID) && heard.IndexOf("You claimed")<0,
                "consumer leftover action-4 is replaced by owned silver pouch "+owned[n]);
            p=ReloadBall(p);
            Check(SilverPouchUses(p)==1 && p.Entity.Money==before+pays[n] && SilverStored(p)==(long)(before+pays[n]),
                "silver pouch "+owned[n]+" survives production reload");
        }
        foreach(bool bound in new[]{false,true})foreach(ushort stack in new ushort[]{0,1,2}){
            var p=WelcomePlayer(town,1,0,15);var pack=NestedPack(p,3006594,stack,bound);ulong before=p.Entity.Money;
            Parallel.For(0,24,n=>{if(n%3==0)EntityTable.SaveEntity(p);else OpenSilverPouch(p,pack);});
            int expected=stack>1?2:1;
            Check(!Fenced(p) && SilverPouchUses(p)==expected && p.Entity.Money==before+200000UL*(ulong)expected && CountId(p,3006594)==0,
                "3006594 pays 200000 silver with stack remainder semantics: "+bound+"/"+stack);
        }
        heard="";var overflow=WelcomePlayer(town,1,0,15);overflow.Entity.Money=Entity.MaximumSilver;var rich=Pack(overflow,3006594,1);OpenSilverPouch(overflow,rich);
        Check(!Fenced(overflow) && SilverPouchUses(overflow)==0 && overflow.Inventory.ContainsUID(rich.UID) && overflow.Entity.Money==Entity.MaximumSilver
            && heard.IndexOf("Spend some silver before opening this pack.")>=0,"checked signed-bigint overflow preserves the silver pouch");
        var boundary=WelcomePlayer(town,1,0,15);boundary.Entity.Money=Entity.MaximumSilver-200000UL;EntityTable.UpdateMoney(boundary);
        var boundaryPack=Pack(boundary,3006594,1);OpenSilverPouch(boundary,boundaryPack);
        Check(!Fenced(boundary) && SilverPouchUses(boundary)==1 && !boundary.Inventory.ContainsUID(boundaryPack.UID)
            && boundary.Entity.Money==Entity.MaximumSilver && SilverStored(boundary)==long.MaxValue,
            "exact signed-bigint maximum boundary consumes once and persists the full silver balance");
        foreach(string fault in new[]{"source","silver","receipt"}){
            var p=WelcomePlayer(town,1,0,15);var item=Pack(p,3006594,1);
            string table=fault=="receipt"?"revora_silver_pouch_uses":"entities";
            if(fault=="source")table="items";
            string verb=fault=="source"?"DELETE":fault=="receipt"?"INSERT":"UPDATE";
            Query("CREATE FUNCTION suppress_silver_pouch() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_silver_pouch BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_silver_pouch()");OpenSilverPouch(p,item);
            Check(Fenced(p) && SilverPouchUses(p)==0 && p.Inventory.ContainsUID(item.UID),"suppressed "+fault+" preserves 3006594 source and silver");
            Query("DROP TRIGGER suppress_silver_pouch ON "+table+";DROP FUNCTION suppress_silver_pouch()");p=ReloadBall(p);OpenSilverPouch(p,p.Inventory.Objects.Single(i=>i.ID==3006594));
            Check(!Fenced(p) && SilverPouchUses(p)==1 && p.Entity.Money==200000UL,"production reload retries 3006594 after "+fault+" rollback");
        }
        var replaced=WelcomePlayer(town,1,0,15);var replacedPack=Pack(replaced,3006594,1);
        Query("UPDATE items SET id=1000000 WHERE uid=@uid","uid",(long)replacedPack.UID);OpenSilverPouch(replaced,replacedPack);
        Check(Fenced(replaced) && SilverPouchUses(replaced)==0 && replaced.Entity.Money==0 && Scalar("SELECT id FROM items WHERE uid=@uid",replacedPack.UID)==1000000,
            "same-UID durable source replacement cannot consume or pay a silver pouch");
        Query("UPDATE items SET id=3006594 WHERE uid=@uid","uid",(long)replacedPack.UID);replaced=ReloadBall(replaced);
        OpenSilverPouch(replaced,replaced.Inventory.Objects.Single(i=>i.ID==3006594));
        Check(!Fenced(replaced) && SilverPouchUses(replaced)==1 && replaced.Entity.Money==200000UL,
            "restored source replacement retries once after production reload");
        var extraBag=WelcomePlayer(town,1,0,15);var extraSrc=Pack(extraBag,3006594,1);uint extraUid=ConquerItem.ItemUID.Next;
        Query("INSERT INTO items(uid,id,entityid,stacksize) VALUES(@uid,@id,@owner,1)","uid",(long)extraUid,"id",1000000,"owner",(long)extraBag.Entity.UID);OpenSilverPouch(extraBag,extraSrc);
        Check(Fenced(extraBag) && SilverPouchUses(extraBag)==0 && extraBag.Inventory.ContainsUID(extraSrc.UID),"an extra durable bag UID cannot open a silver pouch");
        Query("DELETE FROM items WHERE uid=@uid","uid",(long)extraUid);extraBag=ReloadBall(extraBag);OpenSilverPouch(extraBag,extraBag.Inventory.Objects.Single(i=>i.ID==3006594));
        Check(!Fenced(extraBag) && SilverPouchUses(extraBag)==1,"production reload retries after the extra durable bag UID is removed");
        var missingBag=WelcomePlayer(town,1,0,15);var missingSrc=Pack(missingBag,3006594,1);var cacheOnly=Pack(missingBag,1000000,1);
        Query("DELETE FROM items WHERE uid=@uid","uid",(long)cacheOnly.UID);OpenSilverPouch(missingBag,missingSrc);
        Check(Fenced(missingBag) && SilverPouchUses(missingBag)==0 && missingBag.Inventory.ContainsUID(missingSrc.UID),"a cache-only bag UID cannot open a silver pouch");
        missingBag=ReloadBall(missingBag);OpenSilverPouch(missingBag,missingBag.Inventory.Objects.Single(i=>i.ID==3006594));
        Check(!Fenced(missingBag) && SilverPouchUses(missingBag)==1,"production reload retries after the cache-only bag UID is dropped");
        var frames=WelcomePlayer(town,1,0,15);var wire=Pack(frames,3006594,1);
        foreach(string bad in new[]{"short","long","length","type","seal"}){
            byte[] frame=bad=="short"?new byte[31]:bad=="long"?new byte[129]:PrayerPacket(wire.UID);
            Writer.WriteUInt16((ushort)(frame.Length-8),0,frame);Writer.WriteUInt16(1009,2,frame);Writer.WriteUInt32(wire.UID,8,frame);Writer.WriteUInt16(4,20,frame);Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);
            if(bad=="length")Writer.WriteUInt16(1,0,frame);if(bad=="type")Writer.WriteUInt16(1008,2,frame);if(bad=="seal")frame[frame.Length-1]^=1;
            ItemUsage.Process(frame,frames);Check(!Fenced(frames) && SilverPouchUses(frames)==0 && frames.Inventory.ContainsUID(wire.UID),"bounded silver pouch rejects "+bad+" frame");
        }OpenSilverPouch(frames,wire);Check(!Fenced(frames) && SilverPouchUses(frames)==1,"ordinary silver pouch works after malformed frames");
        var replay=WelcomePlayer(town,1,0,15);var once=Pack(replay,723721,1);uint uid=once.UID;OpenSilverPouch(replay,once);
        ItemHandler.UseItem(once,replay);Check(SilverPouchUses(replay)==1 && !replay.Inventory.ContainsUID(uid),"consumed silver pouch UID cannot replay");
        var direct=WelcomePlayer(town,1,0,15);var directPack=Pack(direct,723723,1);ItemHandler.UseItem(directPack,direct);
        Check(!Fenced(direct) && SilverPouchUses(direct)==1 && direct.Entity.Money==200000000000UL,"direct UseItem opens 723723");
        int rare=Config.ChiJiangRarePercent;Config.ChiJiangRarePercent=0;
        try{
            town.Npcs[900043]=new NpcSpawn{UID=900043,MapID=1002,X=396,Y=348};
            var hunter=WelcomePlayer(town,110,1,15);hunter.Entity.X=396;hunter.Entity.Y=348;hunter.ActiveNpc=900043;
            Pack(hunter,3000060,1);Npcs.GetDialog(new NpcRequest(true){NpcID=900043,OptionID=1},hunter);Npcs.GetDialog(new NpcRequest(true){NpcID=900043,OptionID=2},hunter);
            Check(CountId(hunter,3006594)==1 && CountId(hunter,3000060)==0,"ordinary ChiJiang clerk exchange issues Magic Spar 3006594");
            var spar=hunter.Inventory.Objects.Single(i=>i.ID==3006594);OpenSilverPouch(hunter,spar);
            Check(!Fenced(hunter) && SilverPouchUses(hunter)==1 && hunter.Entity.Money==200000UL,"issued ChiJiang Magic Spar redeems through the owned service");
        }finally{Config.ChiJiangRarePercent=rare;}
        var market=World(runtime,1036);market.Npcs[30000]=new NpcSpawn{UID=30000,MapID=1036,X=240,Y=249};
        var wallet=WelcomePlayer(market,1,0,15);wallet.Entity.X=240;wallet.Entity.Y=249;wallet.ActiveNpc=30000;wallet.Entity.Money=200500000000UL;EntityTable.UpdateMoney(wallet);
        Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.ShelbyRewards"),null,"Initialize");
        Npcs.GetDialog(new NpcRequest(true){NpcID=30000,OptionID=6},wallet);Npcs.GetDialog(new NpcRequest(true){NpcID=30000,OptionID=7},wallet);
        Check(CountId(wallet,723723)==1 && wallet.Entity.Money==0,"ordinary Shelby wrap issues TopMoneyBag 723723");
        var bag=wallet.Inventory.Objects.Single(i=>i.ID==723723);OpenSilverPouch(wallet,bag);
        Check(!Fenced(wallet) && SilverPouchUses(wallet)==1 && wallet.Entity.Money==200000000000UL,"issued Shelby wallet redeems leftover 200,000,000,000 silver");
        var fortune=WelcomePlayer(town,1,0,15);var box=Pack(fortune,3001266,1);ulong gold=fortune.Entity.Money;ItemUsage.Process(PrayerPacket(box.UID),fortune);
        Check(!Fenced(fortune) && !fortune.Inventory.ContainsUID(box.UID) && fortune.Entity.Money==gold+200000000UL,
            "consumer leftover 3001266 still split-writes 200,000,000 gold");
        var skip=WelcomePlayer(town,1,0,15);var skipBox=Pack(skip,3001266,1);
        Check(!(bool)Call(SilverPurses,null,"TryUse",skipBox,skip) && skip.Inventory.ContainsUID(skipBox.UID),
            "3001266 stays outside SilverPouches");
        var ten=WelcomePlayer(town,1,0,15);var tenBag=Pack(ten,723722,1);ulong tenBefore=ten.Entity.Money;ItemUsage.Process(PrayerPacket(tenBag.UID),ten);
        Check(!Fenced(ten) && !ten.Inventory.ContainsUID(tenBag.UID) && ten.Entity.Money==tenBefore+300000000UL,
            "consumer leftover 723722 still split-writes 300,000,000 silver");
        var starry=WelcomePlayer(town,1,0,15);var starryBag=Pack(starry,3007366,1);ulong starryBefore=starry.Entity.Money;ItemUsage.Process(PrayerPacket(starryBag.UID),starry);
        Check(!Fenced(starry) && !starry.Inventory.ContainsUID(starryBag.UID) && starry.Entity.Money==starryBefore+2000000000UL,
            "consumer leftover 3007366 still split-writes 2,000,000,000 silver");
        SilverPurses.GetField("Spoken",Hidden).SetValue(null,null);
    }
}
