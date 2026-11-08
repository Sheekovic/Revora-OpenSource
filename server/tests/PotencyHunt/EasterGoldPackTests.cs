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
    static readonly Type EasterGold=typeof(Entity).Assembly.GetType("Revora.Game.Features.EasterGoldPacks");
    static DateTime easterNow=DateTime.UtcNow;
    static int easterRoll=1;
    static long EasterUses(GameState p){return Scalar("SELECT count(*) FROM revora_easter_gold_pack_uses WHERE uid=@uid",p.Entity.UID);}
    static void OpenEaster(GameState p,ConquerItem item){ItemUsage.Process(PrayerPacket(item.UID),p);}
    static readonly uint[] EasterRewards=new uint[]{
        193555,193565,193625,188915,193445,193525,200500,200517,200540,200482,
        200531,200494,200543,200544,350090,370040,360176,360186,380046,200563,
        200564,200570,200571,200572,200573,200574,200575,200481,200499,193775};
    static byte[] SignInFrame(byte action){
        byte[] frame=new byte[20];Writer.WriteUInt16(12,0,frame);Writer.WriteUInt16(3200,2,frame);frame[4]=action;return frame;
    }
    static void VerifyEasterGoldPacks(string runtime,Map town){
        Call(EasterGold,null,"Initialize");Call(EasterGold,null,"Initialize");
        EasterGold.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>easterNow));
        EasterGold.GetField("Roll",Hidden).SetValue(null,(Func<int,int,int>)((min,max)=>easterRoll));
        SignInTable.Load();
        string handler=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemHandler.cs")));
        string usage=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemUsage.cs")));
        string thread=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Thread.cs")));
        string service=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/EasterGoldPacks.cs")));
        string npcs=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Npcs.cs")));
        string plaza=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/TwinCityPlaza.cs")));
        string data=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/Data.cs")));
        string chat=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/Chat.cs")));
        string packets=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/PacketHandler.cs")));
        string sign=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/SignIn/MsgSignIn.cs")));
        string signTable=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/SignIn/MsgSignInTable.cs")));
        string server=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Server.cs")));
        string monsters=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Database/MonsterTable.cs")));
        string drops=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Database/DropItemTable.cs")));
        string audit=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../docs/ITEM_USE_COMPOUND_AUDIT.md")));
        string catalog=File.ReadAllText("database\\itemtype-new.txt");
        string signIni=File.ReadAllText("database\\slayer\\DayliSignIn\\signin.ini");
        string shop=File.ReadAllText("database\\shops\\SlayerPointshop.ini");
        string shopDat=File.ReadAllText("database\\shops\\Shop.dat");
        string v2=File.ReadAllText("database\\shops\\emoneyshopV2.ini");
        string eshop=File.ReadAllText("database\\shops\\emoneyshop.ini");
        string shopTxt=File.ReadAllText("database\\shops\\Shop.txt");
        string exchange=File.ReadAllText("database\\shops\\exchange_shop_goods.txt");
        string heroes=File.ReadAllText("database\\Slayer\\StageGoal.txt");
        Check(handler.IndexOf("Easter gold pack must use the owned service.")>=0 && handler.IndexOf("EasterGoldPacks.TryUse")>=0
            && handler.IndexOf("case 720121:")>=0,
            "720121 is fail-closed on the live handler");
        Check(handler.IndexOf("EasterGoldPacks.TryUse")>handler.IndexOf("PrizeCenterPacks.TryUse")
            && usage.IndexOf("EasterGoldPacks.TryHandle")>usage.IndexOf("PrizeCenterPacks.TryHandle")
            && thread.IndexOf("EasterGoldPacks.Initialize")>thread.IndexOf("PrizeCenterPacks.Initialize"),
            "EasterGoldPacks intercepts sit after PrizeCenterPacks");
        Check(handler.IndexOf("GetPositionFromID")>=0 && handler.IndexOf("Inventory = 0")>=0,
            "ordinary 1009/action-4 still dispatches non-equipment IDs through UseItem");
        int switchStart=handler.IndexOf("switch (item.ID)"),switchEnd=handler.IndexOf("public static byte GetNextRefineryItem",switchStart);
        var sourceIds=System.Text.RegularExpressions.Regex.Matches(handler.Substring(switchStart,switchEnd-switchStart),@"(?m)^ {16}case (\d+):").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        var auditIds=System.Text.RegularExpressions.Regex.Matches(audit,@"(?m)^\| (\d+) \|").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        Check(sourceIds.Length==518 && sourceIds.OrderBy(id=>id).SequenceEqual(auditIds.OrderBy(id=>id)),
            "compound audit still enumerates the exact 518 numeric top-level UseItem case IDs");
        Check(audit.IndexOf("| live-split-write | 145 |")>=0 && audit.IndexOf("| owned-fail-closed | 275 |")>=0
            && audit.IndexOf("| single-consume | 55 |")>=0 && audit.IndexOf("| 721624 | owned-fail-closed | source+timer |")>=0,
            "census is 145 live-split-write / 275 owned-fail-closed / 55 single-consume after Bloodshed Sea ownership");
        Check(audit.IndexOf("| 720121 | owned-fail-closed |")>=0 && audit.IndexOf("| 720654 | live-split-write | source+rvp |")>=0,
            "audit owns 720121 and keeps 720654 live-split-write");
        Check(catalog.IndexOf("720121@@EasterGoldPack")>=0 && catalog.IndexOf("720654@@JoyCPPack")>=0
            && catalog.IndexOf("Right~click~to~get~1380~CPs.")>=0,
            "catalog identities are EasterGoldPack and JoyCPPack / 1380 CPs");
        Check(service.IndexOf("Roll(1,31)")>=0 && service.IndexOf("193775")>=0 && service.IndexOf("193785")<0,
            "owned service preserves leftover Next(1,31) bounds and omits unreachable case 31");
        Check(EasterRewards.Length==30,"reachable leftover set is 30 garments and mounts");
        for(int n=0;n<EasterRewards.Length;n++)
            Check(ConquerItemInformation.BaseInformations.ContainsKey(EasterRewards[n]),"installed catalog contains leftover reward "+EasterRewards[n]);
        Check(ConquerItemInformation.BaseInformations.ContainsKey(193785u),"unreachable leftover case 31 identity still exists in catalog");
        Check(handler.IndexOf("RevoraPoint += 25000")>=0 && handler.IndexOf("You claimed 25,000 Revora Points!")>=0
            && handler.IndexOf("case 720654:")>=0 && service.IndexOf("720654")<0,
            "leftover 720654 still pays +25,000 RVP and is excluded from EasterGoldPacks");
        Check(chat.IndexOf("case \"primercredito\":")>=0 && data.IndexOf("AddBoundItem(720121, 0, 1);//Holy Pack")>=0
            && data.IndexOf("AddBoundItem(720654, 0, 1);//Revora Points Pack")>=0
            && data.IndexOf("Sorry, but the GM has not granted you the welcome gift.")>=0,
            "FirstCredit bound 720121/720654 requires GM primercredito points");
        Check(drops.IndexOf("//new Type() { Item = 720654")>=0 && drops.IndexOf("Item = 720121")<0,
            "DropItemTable 720654 entry is commented and 720121 is absent");
        Check(plaza.IndexOf("900035")>=0 && plaza.IndexOf("ClosedDeskIds")>=0
            && plaza.Substring(plaza.IndexOf("ClosedDeskIds")).IndexOf("900035")>=0
            && npcs.IndexOf("Add(720121, 0, 1)")>=0 && npcs.IndexOf("Holy Package")>=0,
            "Prizewell 900035 Holy Package route is a Twin City closed desk");
        Check(shop.IndexOf("720121 300000")>=0 && server.IndexOf("//  Database.RevoraPointhop.Load();")>=0
            && System.Text.RegularExpressions.Regex.Replace(server,@"//.*"," ").IndexOf("Pointhop.Load")<0,
            "SlayerPointshop.ini stocks 720121 but REVORAPointhop.Load stays commented out");
        Check(server.IndexOf("Database.SignInTable.Load();")>=0 && packets.IndexOf("MsgSignIn.Handle")>=0
            && signIni.IndexOf("prizeItemType=3100014")>=0 && signIni.IndexOf("needDays=7")>=0
            && signIni.IndexOf("prizeItemType=3100015")>=0 && signTable.IndexOf("Prizes.Add(prize.Days2Claim, prize)")>=0
            && SignInTable.Prizes[7].PrizeItemID==3100014u && SignInTable.Prizes[14].PrizeItemID==3100015u,
            "startup loads Daily Sign-In prizes 3100014/3100015 for 7/14 days");
        Check(handler.IndexOf("DailySignInPacks.TryUse")>=0 && handler.IndexOf("AncientStoneCombine.TryUse")>=0
            && File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/DailySignInPacks.cs"))).IndexOf("720121,3004247,3004248,3004249")>=0
            && File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/AncientStoneCombine.cs"))).IndexOf("Cost=20")>=0,
            "owned Daily Sign-In packs and owned 20x 3004246 still issue 720121");
        Check(monsters.IndexOf("ItemID = 3004246; //HolyStone")>=0 && monsters.IndexOf("Name == \"FuriousBear\"")>=0
            && PublicScalar("SELECT count(*) FROM public.monsterspawns s JOIN public.monsterinfos m ON m.id=s.npctype WHERE m.name='FuriousBear'")>=1,
            "spawned SuperBoss FuriousBear VIP7 loot can floor-drop 3004246");
        Check(monsters.IndexOf("Uid = 720121")>=0 && monsters.IndexOf("Name == \"SunDevil\"")>=0
            && monsters.IndexOf("ItemID = 720121; //SplendidStarStone")>=0 && monsters.IndexOf("Kernel.Random.Next(1, 9)")>=0
            && PublicScalar("SELECT count(*) FROM public.monsterspawns s JOIN public.monsterinfos m ON m.id=s.npctype WHERE m.name='SunDevil'")>=1,
            "installed SunDevil can drop 720121; CursedChief table lists 720121 only on unreachable Next(1,9) case 9");
        Check(eshop.IndexOf("720121")<0 && v2.IndexOf("720121")<0 && shopTxt.IndexOf("720121")<0 && shopDat.IndexOf("720121")<0
            && exchange.IndexOf("720121")<0 && heroes.IndexOf("720121")<0
            && eshop.IndexOf("720654")<0 && v2.IndexOf("720654")<0 && shopTxt.IndexOf("720654")<0 && shopDat.IndexOf("720654")<0
            && exchange.IndexOf("720654")<0 && heroes.IndexOf("720654")<0 && shop.IndexOf("720654")<0,
            "neither ID is in gold/emoney/exchange/StageGoal shops; 720654 is also absent from SlayerPointshop");
        string heard="";EasterGold.GetField("Spoken",Hidden).SetValue(null,(Action<string>)(m=>{heard=heard+m+"|";}));
        for(int n=1;n<=30;n++){
            easterRoll=n;heard="";var p=WelcomePlayer(town,1,0,15);var pack=Pack(p,720121,1);OpenEaster(p,pack);
            var gift=p.Inventory.Objects.Single(i=>i.ID==EasterRewards[n-1]);
            ushort durability=ConquerItemInformation.BaseInformations[EasterRewards[n-1]].Durability;
            Check(!Fenced(p) && EasterUses(p)==1 && !p.Inventory.ContainsUID(pack.UID) && gift.Plus==0 && !gift.Bound && gift.StackSize==1 && gift.Days==0
                && gift.Durability==durability && gift.MaximDurability==durability
                && Scalar("SELECT count(*) FROM items WHERE uid=@uid AND bound=0 AND plus=0 AND stacksize=1 AND durability="+durability+" AND maximdurability="+durability,gift.UID)==1
                && heard=="",
                "720121 roll "+n+" pays permanent unbound plus-0 stack-1 "+EasterRewards[n-1]+" silently");
        }
        easterRoll=31;var miss=WelcomePlayer(town,1,0,15);var missPack=Pack(miss,720121,1);OpenEaster(miss,missPack);
        Check(!Fenced(miss) && EasterUses(miss)==0 && miss.Inventory.ContainsUID(missPack.UID) && CountId(miss,193785)==0,
            "720121 excluded bound 31 issues nothing and does not consume");
        easterRoll=0;var zero=WelcomePlayer(town,1,0,15);var zeroPack=Pack(zero,720121,1);OpenEaster(zero,zeroPack);
        Check(!Fenced(zero) && EasterUses(zero)==0 && zero.Inventory.ContainsUID(zeroPack.UID) && zero.Inventory.Count==1,
            "out-of-range roll 0 never inserts item 0");
        easterRoll=1;
        foreach(bool bound in new[]{false,true})foreach(ushort stack in new ushort[]{0,1,2}){
            var p=WelcomePlayer(town,1,0,15);var pack=NestedPack(p,720121,stack,bound);
            Parallel.For(0,24,i=>{if(i%3==0)EntityTable.SaveEntity(p);else OpenEaster(p,pack);});
            int expected=stack>1?2:1;
            Check(!Fenced(p) && EasterUses(p)==expected && CountId(p,193555)==expected && CountId(p,720121)==0
                && p.Inventory.Objects.Where(i=>i.ID==193555).All(i=>i.Bound==bound && i.Plus==0 && i.StackSize==1 && i.Days==0),
                "720121 pays one leftover reward with stack remainder semantics: "+bound+"/"+stack);
        }
        heard="";var fullKeep=WelcomePlayer(town,1,0,15);var keepSrc=NestedPack(fullKeep,720121,2,false);while(fullKeep.Inventory.Count<40)Pack(fullKeep,1000000,1);OpenEaster(fullKeep,keepSrc);
        Check(!Fenced(fullKeep) && EasterUses(fullKeep)==0 && fullKeep.Inventory.ContainsUID(keepSrc.UID)
            && heard.IndexOf("Your inventory is Full.")>=0,"720121 remainder refuses when the reward would not fit");
        var fullPay=WelcomePlayer(town,1,0,15);var paySrc=Pack(fullPay,720121,1);while(fullPay.Inventory.Count<40)Pack(fullPay,1000000,1);OpenEaster(fullPay,paySrc);
        Check(!Fenced(fullPay) && EasterUses(fullPay)==1 && CountId(fullPay,193555)==1 && !fullPay.Inventory.ContainsUID(paySrc.UID),
            "720121 singleton credits the source slot in a full bag");
        foreach(string fault in new[]{"source","output","receipt"}){
            var p=WelcomePlayer(town,1,0,15);var item=Pack(p,720121,1);
            string table=fault=="receipt"?"revora_easter_gold_pack_uses":"items";
            string verb=fault=="source"?"DELETE":"INSERT";
            Query("CREATE FUNCTION suppress_easter_pack() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_easter_pack BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_easter_pack()");OpenEaster(p,item);
            Check(Fenced(p) && EasterUses(p)==0 && p.Inventory.ContainsUID(item.UID) && CountId(p,193555)==0,
                "suppressed "+fault+" preserves 720121 source and reward");
            Query("DROP TRIGGER suppress_easter_pack ON "+table+";DROP FUNCTION suppress_easter_pack()");p=ReloadBall(p);OpenEaster(p,p.Inventory.Objects.Single(i=>i.ID==720121));
            Check(!Fenced(p) && EasterUses(p)==1 && CountId(p,193555)==1,"production reload retries 720121 after "+fault+" rollback");
        }
        var replaced=WelcomePlayer(town,1,0,15);var replacedPack=Pack(replaced,720121,1);
        Query("UPDATE items SET id=1000000 WHERE uid=@uid","uid",(long)replacedPack.UID);OpenEaster(replaced,replacedPack);
        Check(Fenced(replaced) && EasterUses(replaced)==0 && CountId(replaced,193555)==0 && Scalar("SELECT id FROM items WHERE uid=@uid",replacedPack.UID)==1000000,
            "same-UID durable source replacement cannot consume or pay EasterGoldPack");
        Query("UPDATE items SET id=720121 WHERE uid=@uid","uid",(long)replacedPack.UID);replaced=ReloadBall(replaced);
        OpenEaster(replaced,replaced.Inventory.Objects.Single(i=>i.ID==720121));
        Check(!Fenced(replaced) && EasterUses(replaced)==1 && CountId(replaced,193555)==1,"restored source replacement retries once after production reload");
        var extraBag=WelcomePlayer(town,1,0,15);var extraSrc=Pack(extraBag,720121,1);uint extraUid=ConquerItem.ItemUID.Next;
        Query("INSERT INTO items(uid,id,entityid,stacksize) VALUES(@uid,@id,@owner,1)","uid",(long)extraUid,"id",1000000,"owner",(long)extraBag.Entity.UID);OpenEaster(extraBag,extraSrc);
        Check(Fenced(extraBag) && EasterUses(extraBag)==0 && extraBag.Inventory.ContainsUID(extraSrc.UID),"an extra durable bag UID cannot open EasterGoldPack");
        Query("DELETE FROM items WHERE uid=@uid","uid",(long)extraUid);extraBag=ReloadBall(extraBag);OpenEaster(extraBag,extraBag.Inventory.Objects.Single(i=>i.ID==720121));
        Check(!Fenced(extraBag) && EasterUses(extraBag)==1,"production reload retries after the extra durable bag UID is removed");
        var missingBag=WelcomePlayer(town,1,0,15);var missingSrc=Pack(missingBag,720121,1);var cacheOnly=Pack(missingBag,1000000,1);
        Query("DELETE FROM items WHERE uid=@uid","uid",(long)cacheOnly.UID);OpenEaster(missingBag,missingSrc);
        Check(Fenced(missingBag) && EasterUses(missingBag)==0 && missingBag.Inventory.ContainsUID(missingSrc.UID),"a cache-only bag UID cannot open EasterGoldPack");
        missingBag=ReloadBall(missingBag);OpenEaster(missingBag,missingBag.Inventory.Objects.Single(i=>i.ID==720121));
        Check(!Fenced(missingBag) && EasterUses(missingBag)==1,"production reload retries after the cache-only bag UID is dropped");
        var frames=WelcomePlayer(town,1,0,15);var wire=Pack(frames,720121,1);
        foreach(string bad in new[]{"short","long","length","type","seal"}){
            byte[] frame=bad=="short"?new byte[31]:bad=="long"?new byte[129]:PrayerPacket(wire.UID);
            Writer.WriteUInt16((ushort)(frame.Length-8),0,frame);Writer.WriteUInt16(1009,2,frame);Writer.WriteUInt32(wire.UID,8,frame);Writer.WriteUInt16(4,20,frame);Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);
            if(bad=="length")Writer.WriteUInt16(1,0,frame);if(bad=="type")Writer.WriteUInt16(1008,2,frame);if(bad=="seal")frame[frame.Length-1]^=1;
            ItemUsage.Process(frame,frames);Check(!Fenced(frames) && EasterUses(frames)==0 && frames.Inventory.ContainsUID(wire.UID),"bounded EasterGoldPack rejects "+bad+" frame");
        }OpenEaster(frames,wire);Check(!Fenced(frames) && EasterUses(frames)==1,"ordinary EasterGoldPack works after malformed frames");
        var replay=WelcomePlayer(town,1,0,15);var once=Pack(replay,720121,1);uint uid=once.UID;OpenEaster(replay,once);
        ItemHandler.UseItem(once,replay);Check(EasterUses(replay)==1 && !replay.Inventory.ContainsUID(uid),"consumed EasterGoldPack UID cannot replay");
        var direct=WelcomePlayer(town,1,0,15);var directPack=Pack(direct,720121,1);ItemHandler.UseItem(directPack,direct);
        Check(!Fenced(direct) && EasterUses(direct)==1 && CountId(direct,193555)==1,"direct UseItem opens 720121");
        var race=WelcomePlayer(town,1,0,15);var racePack=Pack(race,720121,1);
        Parallel.For(0,16,n=>OpenEaster(race,racePack));
        Check(!Fenced(race) && EasterUses(race)==1 && CountId(race,193555)==1,"parallel packets cannot duplicate EasterGoldPack output");
        var stones=WelcomePlayer(town,1,0,15);var holy=Pack(stones,3004246,20);
        ItemHandler.UseItem(holy,stones);
        Check(CountId(stones,720121)==1 && CountId(stones,3004246)==0,"ordinary 20x 3004246 combine issues 720121");
        OpenEaster(stones,stones.Inventory.Objects.Single(i=>i.ID==720121));
        Check(!Fenced(stones) && EasterUses(stones)==1 && CountId(stones,193555)==1,"combined EasterGoldPack redeems through the owned service");
        var week=WelcomePlayer(town,1,0,15);var weekPack=Pack(week,3100014,1);ItemHandler.UseItem(weekPack,week);
        Check(CountId(week,720121)==1,"leftover 7-day sign-in pack 3100014 still issues 720121");
        OpenEaster(week,week.Inventory.Objects.Single(i=>i.ID==720121));
        Check(!Fenced(week) && EasterUses(week)==1,"3100014-issued EasterGoldPack redeems through the owned service");
        var signer=WelcomePlayer(town,140,2,15);signer.Entity.VIPLevel=7;signer.Entity.RevoraPoint=200000;signer.Entity.DailySignDays=new byte[31];signer.Entity.DailySignVIPChances=20;
        for(int n=0;n<16;n++)MsgSignIn.Handle(signer,SignInFrame(1));
        MsgSignIn.Handle(signer,SignInFrame(0));
        Check(CountId(signer,3100014)>=1,"packet-driven Daily Sign-In issues 3100014 after seven signed days");
        ItemHandler.UseItem(signer.Inventory.Objects.First(i=>i.ID==3100014),signer);
        Check(CountId(signer,720121)>=1,"sign-in 3100014 use issues 720121");
        OpenEaster(signer,signer.Inventory.Objects.First(i=>i.ID==720121));
        Check(!Fenced(signer) && EasterUses(signer)>=1,"sign-in issued EasterGoldPack redeems through the owned service");
        var joy=WelcomePlayer(town,1,0,15);uint rvp=joy.Entity.RevoraPoint;var joyPack=Pack(joy,720654,1);
        Check(!(bool)Call(EasterGold,null,"TryUse",joyPack,joy) && joy.Inventory.ContainsUID(joyPack.UID),"720654 stays outside EasterGoldPacks");
        ItemHandler.UseItem(joyPack,joy);
        Check(joy.Entity.RevoraPoint==rvp+25000 && !joy.Inventory.ContainsUID(joyPack.UID),"classified leftover 720654 still pays +25,000 RVP");
        var skip=WelcomePlayer(town,1,0,15);var skipPack=Pack(skip,722178,1);
        Check(!(bool)Call(EasterGold,null,"TryUse",skipPack,skip) && skip.Inventory.ContainsUID(skipPack.UID),"Surprise box 722178 stays outside EasterGoldPacks");
        EasterGold.GetField("Spoken",Hidden).SetValue(null,null);
        EasterGold.GetField("Roll",Hidden).SetValue(null,(Func<int,int,int>)((min,max)=>Revora.Kernel.Random.Next(min,max)));
    }
}
