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
    static readonly Type PrizeCenter=typeof(Entity).Assembly.GetType("Revora.Game.Features.PrizeCenterPacks");
    static DateTime prizeNow=DateTime.UtcNow;
    static int prizeRoll=1;
    static long PrizeUses(GameState p){return Scalar("SELECT count(*) FROM revora_prize_center_pack_uses WHERE uid=@uid",p.Entity.UID);}
    static long PrizeRvp(GameState p){return Scalar("SELECT COALESCE(revorapoint,0) FROM entities WHERE uid=@uid",p.Entity.UID);}
    static void OpenPrize(GameState p,ConquerItem item){ItemUsage.Process(PrayerPacket(item.UID),p);}
    static bool PrizeOutputContract(GameState p,uint id,int count,bool bound=false){
        var items=p.Inventory.Objects.Where(i=>i.ID==id).ToArray();
        ushort durability=ConquerItemInformation.BaseInformations[id].Durability;
        return items.Length==count && items.All(i=>i.Bound==bound && i.Plus==0 && i.StackSize==1 && i.Days==0
            && i.TimeLeftInMinutes==0 && i.Durability==durability && i.MaximDurability==durability);
    }
    static readonly uint[] PrizeAccessories=new uint[]{
        350001,350002,350004,350005,350006,350007,350008,350009,350010,350011,
        350012,350014,350015,350016,350017,350018,350019,350020,
        360001,360002,360003,360004,360005,360006,360007,360008,360009,360010,
        360011,360012,360013,360014,360015,360016,360017,360018,360019,360020,
        360021,360022,360023,360024,360025,360026,360027,360028,360029,360030,
        360031,360032,360033,360034,360035,360036,360037,360038,360039,360040,
        370001,370002,370003,370004,370005,370011,370012,370013,370014,370015,
        380001,380002,380003,380004,380005,380011,380012,380013,380014};
    static readonly uint[] PrizeScraps=new uint[]{350081,350082,360151,360152,360153};
    static void VerifyPrizeCenterPacks(string runtime,Map town){
        Call(PrizeCenter,null,"Initialize");Call(PrizeCenter,null,"Initialize");
        PrizeCenter.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>prizeNow));
        PrizeCenter.GetField("Roll",Hidden).SetValue(null,(Func<int,int,int>)((min,max)=>prizeRoll));
        string handler=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemHandler.cs")));
        string usage=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemUsage.cs")));
        string thread=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Thread.cs")));
        string service=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/PrizeCenterPacks.cs")));
        string inventory=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/ConquerStructures/Inventory.cs")));
        string npcs=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Npcs.cs")));
        string monsters=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Database/MonsterTable.cs")));
        string audit=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../docs/ITEM_USE_COMPOUND_AUDIT.md")));
        string catalog=File.ReadAllText("database\\itemtype-new.txt");
        string shopDat=File.ReadAllText("database\\shops\\Shop.dat");
        string v2=File.ReadAllText("database\\shops\\emoneyshopV2.ini");
        string eshop=File.ReadAllText("database\\shops\\emoneyshop.ini");
        string shopTxt=File.ReadAllText("database\\shops\\Shop.txt");
        string exchange=File.ReadAllText("database\\shops\\exchange_shop_goods.txt");
        string heroes=File.ReadAllText("database\\Slayer\\StageGoal.txt");
        string drops=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Database/DropItemTable.cs")));
        Check(handler.IndexOf("Prize Center pack must use the owned service.")>=0 && handler.IndexOf("PrizeCenterPacks.TryUse")>=0
            && handler.IndexOf("case 720836:")>=0 && handler.IndexOf("case 728898:")>=0 && handler.IndexOf("case 728899:")>=0
            && handler.IndexOf("case 728900:")>=0 && handler.IndexOf("case 3004257:")>=0,
            "Prize Center packs are fail-closed on the live handler");
        Check(handler.IndexOf("PrizeCenterPacks.TryUse")>handler.IndexOf("DragonBallPacks.TryUse")
            && usage.IndexOf("PrizeCenterPacks.TryHandle")>usage.IndexOf("DragonBallPacks.TryHandle")
            && thread.IndexOf("PrizeCenterPacks.Initialize")>thread.IndexOf("DragonBallPacks.Initialize"),
            "PrizeCenterPacks intercepts sit after DragonBallPacks");
        Check(handler.IndexOf("GetPositionFromID")>=0 && handler.IndexOf("Inventory = 0")>=0,
            "ordinary 1009/action-4 still dispatches non-equipment IDs through UseItem");
        int switchStart=handler.IndexOf("switch (item.ID)"),switchEnd=handler.IndexOf("public static byte GetNextRefineryItem",switchStart);
        var sourceIds=System.Text.RegularExpressions.Regex.Matches(handler.Substring(switchStart,switchEnd-switchStart),@"(?m)^ {16}case (\d+):").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        var auditIds=System.Text.RegularExpressions.Regex.Matches(audit,@"(?m)^\| (\d+) \|").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        Check(sourceIds.Length==518 && sourceIds.OrderBy(id=>id).SequenceEqual(auditIds.OrderBy(id=>id)),
            "compound audit still enumerates the exact 518 numeric top-level UseItem case IDs");
        Check(audit.IndexOf("| live-split-write | 247 |")>=0 && audit.IndexOf("| owned-fail-closed | 103 |")>=0,
            "census is 247 live-split-write / 103 owned-fail-closed after five Prize Center packs");
        foreach(uint id in new uint[]{720836u,728898u,728899u,728900u,3004257u})
            Check(audit.IndexOf("| "+id.ToString()+" | owned-fail-closed |")>=0,"audit owns "+id);
        Check(catalog.IndexOf("720836@@WeaponAccessoryPack")>=0 && catalog.IndexOf("728898@@TinyClass2MoneyPouch")>=0
            && catalog.IndexOf("728899@@TinyHorseRacingPointsBag")>=0 && catalog.IndexOf("728900@@TinyClass1MoneyPouch")>=0
            && catalog.IndexOf("3004257@@AccessoryScrapPack")>=0 && catalog.IndexOf("723714@@Class2MoneyBag")>=0
            && catalog.IndexOf("723713@@Class1MoneyBag")>=0,
            "catalog identities match leftover Prize Center names");
        Check(ConquerItemInformation.BaseInformations[728898].StackSize==8 && ConquerItemInformation.BaseInformations[728900].StackSize==8
            && ConquerItemInformation.BaseInformations[728899].StackSize==6,
            "catalog stack sizes are 8/8/6 for the three combines");
        Check(service.IndexOf("Roll(1,78)")>=0 && service.IndexOf("Roll(1,6)")>=0 && service.IndexOf("Accessories.Length")>=0
            && service.IndexOf("380014")>=0 && service.IndexOf("360153")>=0 && service.IndexOf("360154")<0 && service.IndexOf("380015")<0,
            "owned service preserves leftover Next(1,78)/Next(1,6) bounds and omits unreachable case 78/6 IDs");
        Check(service.IndexOf("You have recieved Class2MoneyBag")>=0 && service.IndexOf("You have recieved Class1MoneyBag")>=0
            && service.IndexOf("You have received 3,000 Revora Points.")>=0 && service.IndexOf("You need atleast 2 open spots!")>=0,
            "owned service preserves leftover combine texts and AccessoryScrapPack refusal spelling");
        Check(inventory.IndexOf("if (Owner.Entity.UseItem && item.StackSize > 1)")>=0
            && service.IndexOf("stacksize=stacksize-")>=0 && service.IndexOf("item.StackSize==cost")>=0,
            "leftover Remove after stack decrement can eat remainder; owned combine consumes the exact advertised 8/6");
        Check(npcs.IndexOf("case 9623:")>=0 && npcs.IndexOf("Add(728898, 0, 1)")>=0 && npcs.IndexOf("Add(3004257, 0, 1)")>=0
            && npcs.IndexOf("Add(720836, 0, 1)")>=0 && npcs.IndexOf("Add(728899, 0, 1)")>=0 && npcs.IndexOf("Add(728900, 0, 1)")>=0
            && npcs.IndexOf("Teleport(1068")>=0 && npcs.IndexOf("case 9619:")>=0,
            "MammonEnvoy 9623 issues all five packs and teleporter 9619 reaches Prize Center 1068");
        Check(monsters.IndexOf("Name == \"CoinsStealer\"")>=0 && monsters.IndexOf("AddTime(711609)")>=0
            && monsters.IndexOf("AddTime(711610)")>=0 && monsters.IndexOf("AddTime(711611)")>=0,
            "CoinsStealer can award gold, silver and copper coins");
        Check(PublicScalar("SELECT count(*) FROM public.npcs WHERE id=9623 AND mapid=1068")==1
            && PublicScalar("SELECT count(*) FROM public.npcs WHERE id=9619")==1
            && PublicScalar("SELECT count(*) FROM public.monsterinfos WHERE name='CoinsStealer'")>=1
            && PublicScalar("SELECT count(*) FROM public.monsterspawns s JOIN public.monsterinfos m ON m.id=s.npctype WHERE m.name='CoinsStealer'")>=1,
            "installed Prize Center NPC, teleporter and CoinsStealer spawn exist");
        foreach(uint id in new uint[]{720836u,728898u,728899u,728900u,3004257u})
            Check(eshop.IndexOf(id.ToString())<0 && v2.IndexOf(id.ToString())<0 && shopTxt.IndexOf(id.ToString())<0 && shopDat.IndexOf(id.ToString())<0
                && exchange.IndexOf(id.ToString())<0 && heroes.IndexOf(id.ToString())<0 && drops.IndexOf(id.ToString())<0,
                id+" is not listed in shops, exchange, StageGoal or DropItemTable");
        Check(PrizeAccessories.Length==77 && PrizeScraps.Length==5, "reachable random tables are 77 accessories and 5 scraps");
        for(int n=0;n<PrizeAccessories.Length;n++)
            Check(ConquerItemInformation.BaseInformations.ContainsKey(PrizeAccessories[n]),"installed catalog contains accessory "+PrizeAccessories[n]);
        for(int n=0;n<PrizeScraps.Length;n++)
            Check(ConquerItemInformation.BaseInformations.ContainsKey(PrizeScraps[n]),"installed catalog contains scrap "+PrizeScraps[n]);
        Check(ConquerItemInformation.BaseInformations.ContainsKey(360154u) && ConquerItemInformation.BaseInformations.ContainsKey(380015u),
            "unreachable leftover case 6/78 identities still exist in catalog");
        string heard="";PrizeCenter.GetField("Spoken",Hidden).SetValue(null,(Action<string>)(m=>{heard=heard+m+"|";}));
        prizeRoll=1;
        for(int n=1;n<=77;n++){
            prizeRoll=n;heard="";var p=WelcomePlayer(town,1,0,15);var pack=Pack(p,720836,1);OpenPrize(p,pack);
            Check(!Fenced(p) && PrizeUses(p)==1 && !p.Inventory.ContainsUID(pack.UID) && CountId(p,PrizeAccessories[n-1])==1
                && PrizeOutputContract(p,PrizeAccessories[n-1],1)
                && heard=="","720836 roll "+n+" pays unbound plus-0 "+PrizeAccessories[n-1]+" silently");
        }
        prizeRoll=78;heard="";var miss=WelcomePlayer(town,1,0,15);var missPack=Pack(miss,720836,1);OpenPrize(miss,missPack);
        Check(!Fenced(miss) && PrizeUses(miss)==0 && miss.Inventory.ContainsUID(missPack.UID) && miss.Inventory.Count==1,
            "720836 excluded bound 78 issues nothing and does not consume");
        prizeRoll=1;
        for(int n=1;n<=5;n++){
            prizeRoll=n;var p=WelcomePlayer(town,1,0,15);var pack=Pack(p,3004257,1);OpenPrize(p,pack);
            Check(!Fenced(p) && PrizeUses(p)==1 && PrizeOutputContract(p,PrizeScraps[n-1],1),
                "3004257 roll "+n+" pays permanent unbound plus-0 stack-1 "+PrizeScraps[n-1]+" with catalog durability");
        }
        prizeRoll=6;var scrapMiss=WelcomePlayer(town,1,0,15);var scrapMissPack=Pack(scrapMiss,3004257,1);OpenPrize(scrapMiss,scrapMissPack);
        Check(!Fenced(scrapMiss) && PrizeUses(scrapMiss)==0 && scrapMiss.Inventory.ContainsUID(scrapMissPack.UID) && CountId(scrapMiss,360154)==0,
            "3004257 excluded bound 6 never issues catalog item 360154");
        prizeRoll=1;
        foreach(bool bound in new[]{false,true})foreach(ushort stack in new ushort[]{0,1,2}){
            var p=WelcomePlayer(town,1,0,15);var pack=NestedPack(p,720836,stack,bound);
            Parallel.For(0,24,i=>{if(i%3==0)EntityTable.SaveEntity(p);else OpenPrize(p,pack);});
            int expected=stack>1?2:1;
            Check(!Fenced(p) && PrizeUses(p)==expected && PrizeOutputContract(p,350001,expected,bound) && CountId(p,720836)==0,
                "720836 pays one accessory with stack remainder semantics: "+bound+"/"+stack);
        }
        foreach(bool bound in new[]{false,true})foreach(ushort stack in new ushort[]{0,1,2}){
            var p=WelcomePlayer(town,1,0,15);var pack=NestedPack(p,3004257,stack,bound);
            int expected=stack>1?2:1;for(int n=0;n<expected;n++)OpenPrize(p,pack);
            Check(!Fenced(p) && PrizeUses(p)==expected && PrizeOutputContract(p,350081,expected,bound) && CountId(p,3004257)==0,
                "3004257 honors both source bindings and stack 0/1/2 semantics: "+bound+"/"+stack);
        }
        foreach(bool bound in new[]{false,true})foreach(ushort stack in new ushort[]{8,9,10}){
            var p=WelcomePlayer(town,1,0,15);var pack=NestedPack(p,728898,stack,bound);
            Parallel.For(0,24,i=>{if(i%3==0)EntityTable.SaveEntity(p);else OpenPrize(p,pack);});
            int expected=stack>=16?2:1;int remain=stack-expected*8;
            Check(!Fenced(p) && PrizeUses(p)==expected && PrizeOutputContract(p,723714,expected,bound)
                && (remain==0?!p.Inventory.ContainsUID(pack.UID):pack.StackSize==remain),
                "728898 consumes exactly eight and keeps remainder: "+bound+"/"+stack);
        }
        foreach(bool bound in new[]{false,true})foreach(ushort stack in new ushort[]{8,9,10}){
            var p=WelcomePlayer(town,1,0,15);var pack=NestedPack(p,728900,stack,bound);OpenPrize(p,pack);int remain=stack-8;
            Check(!Fenced(p) && PrizeUses(p)==1 && PrizeOutputContract(p,723713,1,bound)
                && (remain==0?!p.Inventory.ContainsUID(pack.UID):pack.StackSize==remain),
                "728900 consumes exactly eight with both source bindings and keeps remainder: "+bound+"/"+stack);
        }
        foreach(bool bound in new[]{false,true})foreach(ushort stack in new ushort[]{6,7,8}){
            heard="";var p=WelcomePlayer(town,1,0,15);uint rvp=p.Entity.RevoraPoint;var pack=NestedPack(p,728899,stack,bound);
            Parallel.For(0,24,i=>{if(i%3==0)EntityTable.SaveEntity(p);else OpenPrize(p,pack);});
            int expected=stack>=12?2:1;int remain=stack-expected*6;
            Check(!Fenced(p) && PrizeUses(p)==expected && p.Entity.RevoraPoint==rvp+3000u*(uint)expected
                && PrizeRvp(p)==rvp+3000L*expected && (remain==0?!p.Inventory.ContainsUID(pack.UID):pack.StackSize==remain)
                && heard.IndexOf("You have received 3,000 Revora Points.")>=0,
                "728899 consumes exactly six and pays 3000 RVP: "+bound+"/"+stack);
        }
        foreach(ushort shortStack in new ushort[]{0,1,7}){
            heard="";var p=WelcomePlayer(town,1,0,15);var pack=Pack(p,728898,shortStack);OpenPrize(p,pack);
            Check(!Fenced(p) && PrizeUses(p)==0 && p.Inventory.ContainsUID(pack.UID) && pack.StackSize==shortStack && CountId(p,723714)==0
                && heard.IndexOf("You~need~to~have~8~Tiny~Class2~Money~Pouches")>=0,"728898 undersized stack awards nothing: "+shortStack);
        }
        heard="";var class1Need=WelcomePlayer(town,1,0,15);var class1Short=Pack(class1Need,728900,7);OpenPrize(class1Need,class1Short);
        Check(PrizeUses(class1Need)==0 && heard.IndexOf("You~need~to~have~8~Tiny~Class1~Money~Pouches")>=0,"728900 undersized stack uses leftover MessageBox copy");
        heard="";var horseNeed=WelcomePlayer(town,1,0,15);var horseShort=Pack(horseNeed,728899,5);OpenPrize(horseNeed,horseShort);
        Check(PrizeUses(horseNeed)==0 && heard.IndexOf("You~need~to~have~6~Tiny~Horse~Racing~Points~Bags")>=0,"728899 undersized stack uses leftover MessageBox copy");
        heard="";var spots=WelcomePlayer(town,1,0,15);var spotsPack=Pack(spots,3004257,1);while(spots.Inventory.Count<39)Pack(spots,1000000,1);OpenPrize(spots,spotsPack);
        Check(!Fenced(spots) && PrizeUses(spots)==0 && spots.Inventory.ContainsUID(spotsPack.UID)
            && heard.IndexOf("You need atleast 2 open spots!")>=0,"3004257 refuses at leftover Count 39");
        var spotsOk=WelcomePlayer(town,1,0,15);var spotsOkPack=Pack(spotsOk,3004257,1);while(spotsOk.Inventory.Count<38)Pack(spotsOk,1000000,1);OpenPrize(spotsOk,spotsOkPack);
        Check(!Fenced(spotsOk) && PrizeUses(spotsOk)==1 && CountId(spotsOk,350081)==1,"3004257 pays at leftover Count 38");
        heard="";var accFull=WelcomePlayer(town,1,0,15);var accSrc=NestedPack(accFull,720836,2,false);while(accFull.Inventory.Count<40)Pack(accFull,1000000,1);OpenPrize(accFull,accSrc);
        Check(!Fenced(accFull) && PrizeUses(accFull)==0 && accFull.Inventory.ContainsUID(accSrc.UID)
            && heard.IndexOf("Your inventory is Full.")>=0,"720836 remainder refuses when the accessory would not fit");
        var accFit=WelcomePlayer(town,1,0,15);var accFitSrc=Pack(accFit,720836,1);while(accFit.Inventory.Count<40)Pack(accFit,1000000,1);OpenPrize(accFit,accFitSrc);
        Check(!Fenced(accFit) && PrizeUses(accFit)==1 && CountId(accFit,350001)==1 && !accFit.Inventory.ContainsUID(accFitSrc.UID),
            "720836 singleton credits the source slot in a full bag");
        var bagFit=WelcomePlayer(town,1,0,15);var eight=Pack(bagFit,728900,8);while(bagFit.Inventory.Count<40)Pack(bagFit,1000000,1);OpenPrize(bagFit,eight);
        Check(!Fenced(bagFit) && PrizeUses(bagFit)==1 && CountId(bagFit,723713)==1 && !bagFit.Inventory.ContainsUID(eight.UID),
            "exact eight-stack 728900 needs no extra slot in a full bag");
        heard="";var bagKeep=WelcomePlayer(town,1,0,15);var nine=Pack(bagKeep,728900,9);while(bagKeep.Inventory.Count<40)Pack(bagKeep,1000000,1);OpenPrize(bagKeep,nine);
        Check(!Fenced(bagKeep) && PrizeUses(bagKeep)==0 && nine.StackSize==9 && bagKeep.Inventory.Count==40
            && heard.IndexOf("Your inventory is Full.")>=0,"728900 remainder requires one free bag slot");
        heard="";var recieved=WelcomePlayer(town,1,0,15);var recievedPack=Pack(recieved,728898,8);OpenPrize(recieved,recievedPack);
        Check(!Fenced(recieved) && CountId(recieved,723714)==1 && heard.IndexOf("You have recieved Class2MoneyBag")>=0,"728898 keeps leftover recieved spelling");
        heard="";var recieved1=WelcomePlayer(town,1,0,15);var recieved1Pack=Pack(recieved1,728900,8);OpenPrize(recieved1,recieved1Pack);
        Check(!Fenced(recieved1) && CountId(recieved1,723713)==1 && heard.IndexOf("You have recieved Class1MoneyBag")>=0,"728900 keeps leftover recieved spelling");
        var overflow=WelcomePlayer(town,1,0,15);Query("UPDATE entities SET revorapoint=4294967295 WHERE uid=@uid","uid",(long)overflow.Entity.UID);overflow=ReloadBall(overflow);
        var overflowPack=Pack(overflow,728899,6);OpenPrize(overflow,overflowPack);
        Check(!Fenced(overflow) && PrizeUses(overflow)==0 && overflow.Inventory.ContainsUID(overflowPack.UID) && overflow.Entity.RevoraPoint==uint.MaxValue,
            "728899 RVP overflow preserves every bag");
        foreach(string fault in new[]{"source","output","receipt"}){
            heard="";var p=WelcomePlayer(town,1,0,15);var item=Pack(p,720836,1);
            string table=fault=="receipt"?"revora_prize_center_pack_uses":"items";
            string verb=fault=="source"?"DELETE":"INSERT";
            Query("CREATE FUNCTION suppress_prize_pack() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_prize_pack BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_prize_pack()");OpenPrize(p,item);
            Check(Fenced(p) && PrizeUses(p)==0 && p.Inventory.ContainsUID(item.UID) && CountId(p,350001)==0 && heard=="",
                "suppressed "+fault+" preserves 720836 source and accessory");
            Query("DROP TRIGGER suppress_prize_pack ON "+table+";DROP FUNCTION suppress_prize_pack()");p=ReloadBall(p);OpenPrize(p,p.Inventory.Objects.Single(i=>i.ID==720836));
            Check(!Fenced(p) && PrizeUses(p)==1 && CountId(p,350001)==1,"production reload retries 720836 after "+fault+" rollback");
        }
        foreach(string fault in new[]{"source","rvp","receipt"}){
            var p=WelcomePlayer(town,1,0,15);var item=Pack(p,728899,6);uint rvp=p.Entity.RevoraPoint;
            string table=fault=="rvp"?"entities":fault=="receipt"?"revora_prize_center_pack_uses":"items";
            string verb=fault=="source"?"DELETE":fault=="receipt"?"INSERT":"UPDATE";
            Query("CREATE FUNCTION suppress_prize_rvp() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_prize_rvp BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_prize_rvp()");OpenPrize(p,item);
            Check(Fenced(p) && PrizeUses(p)==0 && p.Inventory.ContainsUID(item.UID) && p.Entity.RevoraPoint==rvp,
                "suppressed "+fault+" preserves 728899 source and RVP");
            Query("DROP TRIGGER suppress_prize_rvp ON "+table+";DROP FUNCTION suppress_prize_rvp()");p=ReloadBall(p);OpenPrize(p,p.Inventory.Objects.Single(i=>i.ID==728899));
            Check(!Fenced(p) && PrizeUses(p)==1 && p.Entity.RevoraPoint==rvp+3000,"production reload retries 728899 after "+fault+" rollback");
        }
        var replaced=WelcomePlayer(town,1,0,15);var replacedPack=Pack(replaced,720836,1);
        Query("UPDATE items SET id=1000000 WHERE uid=@uid","uid",(long)replacedPack.UID);OpenPrize(replaced,replacedPack);
        Check(Fenced(replaced) && PrizeUses(replaced)==0 && CountId(replaced,350001)==0 && Scalar("SELECT id FROM items WHERE uid=@uid",replacedPack.UID)==1000000,
            "same-UID durable source replacement cannot consume or pay a Prize Center pack");
        Query("UPDATE items SET id=720836 WHERE uid=@uid","uid",(long)replacedPack.UID);replaced=ReloadBall(replaced);
        OpenPrize(replaced,replaced.Inventory.Objects.Single(i=>i.ID==720836));
        Check(!Fenced(replaced) && PrizeUses(replaced)==1 && CountId(replaced,350001)==1,"restored source replacement retries once after production reload");
        var extraBag=WelcomePlayer(town,1,0,15);var extraSrc=Pack(extraBag,728898,8);uint extraUid=ConquerItem.ItemUID.Next;
        Query("INSERT INTO items(uid,id,entityid,stacksize) VALUES(@uid,@id,@owner,1)","uid",(long)extraUid,"id",1000000,"owner",(long)extraBag.Entity.UID);OpenPrize(extraBag,extraSrc);
        Check(Fenced(extraBag) && PrizeUses(extraBag)==0 && extraBag.Inventory.ContainsUID(extraSrc.UID),"an extra durable bag UID cannot open a Prize Center pack");
        Query("DELETE FROM items WHERE uid=@uid","uid",(long)extraUid);extraBag=ReloadBall(extraBag);OpenPrize(extraBag,extraBag.Inventory.Objects.Single(i=>i.ID==728898));
        Check(!Fenced(extraBag) && PrizeUses(extraBag)==1 && CountId(extraBag,723714)==1,"production reload retries after the extra durable bag UID is removed");
        var missingBag=WelcomePlayer(town,1,0,15);var missingSrc=Pack(missingBag,728900,8);var cacheOnly=Pack(missingBag,1000000,1);
        Query("DELETE FROM items WHERE uid=@uid","uid",(long)cacheOnly.UID);OpenPrize(missingBag,missingSrc);
        Check(Fenced(missingBag) && PrizeUses(missingBag)==0 && missingBag.Inventory.ContainsUID(missingSrc.UID),"a cache-only bag UID cannot open a Prize Center pack");
        missingBag=ReloadBall(missingBag);OpenPrize(missingBag,missingBag.Inventory.Objects.Single(i=>i.ID==728900));
        Check(!Fenced(missingBag) && PrizeUses(missingBag)==1,"production reload retries after the cache-only bag UID is dropped");
        var frames=WelcomePlayer(town,1,0,15);var wire=Pack(frames,720836,1);
        foreach(string bad in new[]{"short","long","length","type","seal"}){
            byte[] frame=bad=="short"?new byte[31]:bad=="long"?new byte[129]:PrayerPacket(wire.UID);
            Writer.WriteUInt16((ushort)(frame.Length-8),0,frame);Writer.WriteUInt16(1009,2,frame);Writer.WriteUInt32(wire.UID,8,frame);Writer.WriteUInt16(4,20,frame);Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);
            if(bad=="length")Writer.WriteUInt16(1,0,frame);if(bad=="type")Writer.WriteUInt16(1008,2,frame);if(bad=="seal")frame[frame.Length-1]^=1;
            ItemUsage.Process(frame,frames);Check(!Fenced(frames) && PrizeUses(frames)==0 && frames.Inventory.ContainsUID(wire.UID),"bounded Prize Center pack rejects "+bad+" frame");
        }OpenPrize(frames,wire);Check(!Fenced(frames) && PrizeUses(frames)==1 && CountId(frames,350001)==1,"ordinary Prize Center pack works after malformed frames");
        var replay=WelcomePlayer(town,1,0,15);var once=Pack(replay,720836,1);uint uid=once.UID;OpenPrize(replay,once);
        ItemHandler.UseItem(once,replay);Check(PrizeUses(replay)==1 && !replay.Inventory.ContainsUID(uid),"consumed Prize Center pack UID cannot replay");
        var direct=WelcomePlayer(town,1,0,15);var directPack=Pack(direct,728899,6);ItemHandler.UseItem(directPack,direct);
        Check(!Fenced(direct) && PrizeUses(direct)==1 && direct.Entity.RevoraPoint==3000,"direct UseItem combines 728899");
        var race=WelcomePlayer(town,1,0,15);var racePack=Pack(race,728898,8);
        Parallel.For(0,16,n=>OpenPrize(race,racePack));
        Check(!Fenced(race) && PrizeUses(race)==1 && CountId(race,723714)==1,"parallel packets cannot duplicate Class2MoneyBag");
        var prize=World(runtime,1068);prize.Npcs[9623]=new NpcSpawn{UID=9623,MapID=1068,X=50,Y=50};
        var gold=WelcomePlayer(prize,1,0,15);gold.Entity.X=50;gold.Entity.Y=50;gold.ActiveNpc=9623;Pack(gold,711609,1);
        Npcs.GetDialog(new NpcRequest(true){NpcID=9623,OptionID=5},gold);
        Check(CountId(gold,728898)==1 && CountId(gold,3004257)==1 && CountId(gold,711609)==0,"ordinary MammonEnvoy gold exchange issues 728898 and 3004257");
        prizeRoll=1;var goldPouch=gold.Inventory.Objects.Single(i=>i.ID==728898);goldPouch.StackSize=8;Query("UPDATE items SET stacksize=8 WHERE uid=@uid","uid",(long)goldPouch.UID);
        OpenPrize(gold,goldPouch);OpenPrize(gold,gold.Inventory.Objects.Single(i=>i.ID==3004257));
        Check(!Fenced(gold) && PrizeUses(gold)==2 && CountId(gold,723714)==1 && CountId(gold,350081)==1,"gold-issued packs redeem through the owned service");
        var silver=WelcomePlayer(prize,1,0,15);silver.Entity.X=50;silver.Entity.Y=50;silver.ActiveNpc=9623;Pack(silver,711610,1);
        Npcs.GetDialog(new NpcRequest(true){NpcID=9623,OptionID=6},silver);
        Check(CountId(silver,720836)==1 && CountId(silver,728899)==1 && CountId(silver,711610)==0,"ordinary MammonEnvoy silver exchange issues 720836 and 728899");
        OpenPrize(silver,silver.Inventory.Objects.Single(i=>i.ID==720836));
        var horse=silver.Inventory.Objects.Single(i=>i.ID==728899);horse.StackSize=6;Query("UPDATE items SET stacksize=6 WHERE uid=@uid","uid",(long)horse.UID);OpenPrize(silver,horse);
        Check(!Fenced(silver) && PrizeUses(silver)==2 && CountId(silver,350001)==1 && silver.Entity.RevoraPoint==3000,"silver-issued packs redeem through the owned service");
        var copper=WelcomePlayer(prize,1,0,15);copper.Entity.X=50;copper.Entity.Y=50;copper.ActiveNpc=9623;Pack(copper,711611,1);
        Npcs.GetDialog(new NpcRequest(true){NpcID=9623,OptionID=7},copper);
        Check(CountId(copper,720884)==1 && CountId(copper,728900)==1 && CountId(copper,711611)==0,"ordinary MammonEnvoy copper exchange issues 720884 and 728900");
        var copperPouch=copper.Inventory.Objects.Single(i=>i.ID==728900);copperPouch.StackSize=8;Query("UPDATE items SET stacksize=8 WHERE uid=@uid","uid",(long)copperPouch.UID);
        OpenPrize(copper,copperPouch);
        Check(!Fenced(copper) && PrizeUses(copper)==1 && CountId(copper,723713)==1,"copper-issued 728900 redeems through the owned service");
        var skip=WelcomePlayer(town,1,0,15);var skipPack=Pack(skip,722178,1);
        Check(!(bool)Call(PrizeCenter,null,"TryUse",skipPack,skip) && skip.Inventory.ContainsUID(skipPack.UID),"Surprise box 722178 stays outside PrizeCenterPacks");
        PrizeCenter.GetField("Spoken",Hidden).SetValue(null,null);
        PrizeCenter.GetField("Roll",Hidden).SetValue(null,(Func<int,int,int>)((min,max)=>Revora.Kernel.Random.Next(min,max)));
    }
}
