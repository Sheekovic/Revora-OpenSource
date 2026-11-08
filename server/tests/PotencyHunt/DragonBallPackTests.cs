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
    static readonly Type DbPacks=typeof(Entity).Assembly.GetType("Revora.Game.Features.DragonBallPacks");
    static DateTime dbPackNow=DateTime.UtcNow;
    static long DbPackUses(GameState p){return Scalar("SELECT count(*) FROM revora_dragon_ball_pack_uses WHERE uid=@uid",p.Entity.UID);}
    static int DbBallCount(GameState p){return p.Inventory.Objects.Count(i=>i.ID==1088000);}
    static void OpenDbPack(GameState p,ConquerItem item){ItemUsage.Process(PrayerPacket(item.UID),p);}
    static void VerifyDragonBallPacks(string runtime,Map town){
        Call(DbPacks,null,"Initialize");Call(DbPacks,null,"Initialize");
        DbPacks.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>dbPackNow));
        string handler=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemHandler.cs")));
        string usage=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemUsage.cs")));
        string thread=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Thread.cs")));
        string service=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/DragonBallPacks.cs")));
        string inventory=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/ConquerStructures/Inventory.cs")));
        string npcs=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Npcs.cs")));
        string plaza=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/TwinCityPlaza.cs")));
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
        Check(handler.IndexOf("DragonBall pack must use the owned service.")>=0 && handler.IndexOf("DragonBallPacks.TryUse")>=0
            && handler.IndexOf("case 720028:")>=0 && handler.IndexOf("case 720884:")>=0,
            "DragonBall packs are fail-closed on the live handler");
        Check(handler.IndexOf("DragonBallPacks.TryUse")>handler.IndexOf("FixedOutputPacks.TryUse")
            && usage.IndexOf("DragonBallPacks.TryHandle")>usage.IndexOf("FixedOutputPacks.TryHandle")
            && thread.IndexOf("DragonBallPacks.Initialize")>thread.IndexOf("FixedOutputPacks.Initialize"),
            "DragonBallPacks intercepts sit after FixedOutputPacks");
        Check(handler.IndexOf("GetPositionFromID")>=0 && handler.IndexOf("Inventory = 0")>=0,
            "ordinary 1009/action-4 still dispatches non-equipment IDs through UseItem");
        int switchStart=handler.IndexOf("switch (item.ID)"),switchEnd=handler.IndexOf("public static byte GetNextRefineryItem",switchStart);
        var sourceIds=System.Text.RegularExpressions.Regex.Matches(handler.Substring(switchStart,switchEnd-switchStart),@"(?m)^ {16}case (\d+):").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        var auditIds=System.Text.RegularExpressions.Regex.Matches(audit,@"(?m)^\| (\d+) \|").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        Check(sourceIds.Length==518 && sourceIds.OrderBy(id=>id).SequenceEqual(auditIds.OrderBy(id=>id)),
            "compound audit still enumerates the exact 518 numeric top-level UseItem case IDs");
        Check(audit.IndexOf("| live-split-write | 145 |")>=0 && audit.IndexOf("| owned-fail-closed | 275 |")>=0,
            "census remains 145 live-split-write / 275 owned-fail-closed");
        Check(audit.IndexOf("| 720028 | owned-fail-closed |")>=0 && audit.IndexOf("| 720884 | owned-fail-closed |")>=0,
            "audit owns 720028 and 720884");
        Check(ConquerItemInformation.BaseInformations.ContainsKey(720028u) && ConquerItemInformation.BaseInformations.ContainsKey(720884u)
            && ConquerItemInformation.BaseInformations.ContainsKey(1088000u) && ConquerItemInformation.BaseInformations.ContainsKey(711611u),
            "installed catalog contains both packs, DragonBall 1088000 and CopperCoin 711611");
        Check(catalog.IndexOf("720028@@DBScroll")>=0 && catalog.IndexOf("Equivalent~of~10~DragonBalls")>=0
            && catalog.IndexOf("720884@@DragonBallPack(5)")>=0 && catalog.IndexOf("get~5~Dragon~Balls")>=0
            && catalog.IndexOf("1088000@@DragonBall")>=0 && catalog.IndexOf("711611@@CopperCoin")>=0,
            "catalog identities are DBScroll, DragonBallPack(5), DragonBall and CopperCoin");
        Check(ConquerItemInformation.BaseInformations[1088000].StackSize<=1,
            "DragonBall catalog stack size does not stack leftover Add(id,0,count) units");
        Check(inventory.IndexOf("public bool Add(uint id, byte plus, byte times")>=0
            && inventory.IndexOf("while (times > 0)")>=0 && inventory.IndexOf("if (Count <= 39)")>=0
            && inventory.IndexOf("item.ID = id;")>=0,
            "Inventory.Add(id,plus,times) creates one bag slot per unit when Count<=39");
        Check(service.IndexOf("id==720028?10:5")>=0 && service.IndexOf("Count>31")>=0 && service.IndexOf("Count>30")>=0
            && service.IndexOf("item.ID==720028 && p.Inventory.Count+spaces>40")>=0
            && service.IndexOf("p.Send(Constants.FullInventory)")>=0 && service.IndexOf("You received")<0 && service.IndexOf("Congratulations")<0,
            "owned service preserves leftover ten/five outputs, Count<=31/30 gates, 720028 extra room and silent success");
        Check(plaza.IndexOf("PackingClerkId = 11275")>=0 && plaza.IndexOf("PackingClerkX = 304")>=0 && plaza.IndexOf("PackingClerkY = 433")>=0
            && npcs.IndexOf("case 11275:")>=0 && npcs.IndexOf("Add(720028, 0, 1)")<0
            && File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/MillionaireStonePacking.cs"))).IndexOf("stone=Ball;pack=BallPack;need=10")>=0,
            "MillionaireLee 11275 option 2 is owned packing of ten DragonBalls into 720028");
        int closed=plaza.IndexOf("ClosedDeskIds");
        Check(closed>=0 && plaza.Substring(closed).IndexOf("11275")<0 && plaza.IndexOf("9623")<0 && plaza.IndexOf("9619")<0,
            "MillionaireLee, MammonEnvoy and the Prize Center teleporter are not Twin City closed desks");
        Check(npcs.IndexOf("case 9623:")>=0 && npcs.IndexOf("Add(720884, 0, 1)")>=0 && npcs.IndexOf("Contains(711611, 1)")>=0
            && npcs.IndexOf("case 9619:")>=0 && npcs.IndexOf("Teleport(1068")>=0,
            "MammonEnvoy 9623 exchanges a copper coin for 720884 and teleporter 9619 reaches Prize Center 1068");
        Check(npcs.IndexOf("Remove(720028, 1)")>=0 && npcs.IndexOf("AdjustChiPoints(8000)")>=0,
            "Chi clerks consume DBScroll and are not an issuance route");
        Check(monsters.IndexOf("Name == \"CoinsStealer\"")>=0 && monsters.IndexOf("AddTime(711611)")>=0,
            "CoinsStealer kills can award CopperCoin 711611");
        Check(PublicScalar("SELECT count(*) FROM public.npcs WHERE id=11275 AND mapid=1002")==1
            && PublicScalar("SELECT count(*) FROM public.sobnpcs WHERE id=11275")+PublicScalar("SELECT count(*) FROM public.furniture WHERE id=11275")==0,
            "installed public.npcs has MillionaireLee 11275 on Twin City");
        Check(PublicScalar("SELECT count(*) FROM public.npcs WHERE id=9623 AND mapid=1068")==1
            && PublicScalar("SELECT count(*) FROM public.sobnpcs WHERE id=9623")+PublicScalar("SELECT count(*) FROM public.furniture WHERE id=9623")==0,
            "installed public.npcs has MammonEnvoy 9623 on Prize Center 1068");
        Check(PublicScalar("SELECT count(*) FROM public.npcs WHERE id=9619")
            +PublicScalar("SELECT count(*) FROM public.sobnpcs WHERE id=9619")
            +PublicScalar("SELECT count(*) FROM public.furniture WHERE id=9619")==1,
            "installed world has exactly one Prize Center teleporter 9619");
        Check(PublicScalar("SELECT count(*) FROM public.monsterinfos WHERE name='CoinsStealer'")>=1
            && PublicScalar("SELECT count(*) FROM public.monsterspawns s JOIN public.monsterinfos m ON m.id=s.npctype WHERE m.name='CoinsStealer'")>=1,
            "CoinsStealer is spawned and can drop CopperCoin 711611");
        Check(eshop.IndexOf("720028")<0 && v2.IndexOf("720028")<0 && shopTxt.IndexOf("720028")<0 && shopDat.IndexOf("720028")<0
            && exchange.IndexOf("720028")<0 && heroes.IndexOf("720028")<0 && drops.IndexOf("720028")<0
            && eshop.IndexOf("720884")<0 && v2.IndexOf("720884")<0 && shopTxt.IndexOf("720884")<0 && shopDat.IndexOf("720884")<0
            && exchange.IndexOf("720884")<0 && heroes.IndexOf("720884")<0 && drops.IndexOf("720884")<0,
            "neither DragonBall pack is listed in shops, exchange, StageGoal or DropItemTable");
        string heard="";DbPacks.GetField("Spoken",Hidden).SetValue(null,(Action<string>)(m=>{heard=heard+m+"|";}));
        uint[] ids=new uint[]{720028u,720884u};int[] balls=new int[]{10,5};
        for(int n=0;n<ids.Length;n++){
            heard="";var p=WelcomePlayer(town,1,0,15);var item=Pack(p,ids[n],1);OpenDbPack(p,item);
            Check(!Fenced(p) && DbPackUses(p)==1 && !p.Inventory.ContainsUID(item.UID) && DbBallCount(p)==balls[n]
                && p.Inventory.Objects.All(i=>i.ID!=1088000 || (!i.Bound && i.Plus==0 && i.StackSize==1))
                && heard=="",
                "DragonBall pack "+ids[n]+" pays "+balls[n]+" unbound plus-0 DragonBalls silently");
            p=ReloadBall(p);
            Check(DbPackUses(p)==1 && DbBallCount(p)==balls[n],"DragonBall pack "+ids[n]+" survives production reload");
        }
        foreach(uint id in ids)foreach(bool bound in new[]{false,true})foreach(ushort stack in new ushort[]{0,1,2}){
            int n=id==720028u?10:5;
            var p=WelcomePlayer(town,1,0,15);var pack=NestedPack(p,id,stack,bound);
            Parallel.For(0,24,i=>{if(i%3==0)EntityTable.SaveEntity(p);else OpenDbPack(p,pack);});
            int expected=stack>1?2:1;
            bool copy=id==720028u && bound;
            Check(!Fenced(p) && DbPackUses(p)==expected && DbBallCount(p)==n*expected && CountId(p,id)==0
                && p.Inventory.Objects.Where(i=>i.ID==1088000).All(i=>i.Bound==copy && i.Plus==0 && i.StackSize==1),
                id+" pays DragonBalls with stack remainder semantics: "+bound+"/"+stack);
        }
        heard="";var fullScroll=WelcomePlayer(town,1,0,15);var fullSrc=Pack(fullScroll,720028,1);while(fullScroll.Inventory.Count<32)Pack(fullScroll,1000000,1);OpenDbPack(fullScroll,fullSrc);
        Check(!Fenced(fullScroll) && DbPackUses(fullScroll)==0 && fullScroll.Inventory.ContainsUID(fullSrc.UID) && DbBallCount(fullScroll)==0
            && heard.IndexOf("Your inventory is Full.")>=0,"720028 refuses at leftover Count 32");
        heard="";var okScroll=WelcomePlayer(town,1,0,15);var okSrc=Pack(okScroll,720028,1);while(okScroll.Inventory.Count<31)Pack(okScroll,1000000,1);OpenDbPack(okScroll,okSrc);
        Check(!Fenced(okScroll) && DbPackUses(okScroll)==1 && DbBallCount(okScroll)==10 && heard=="","720028 pays at leftover Count 31");
        heard="";var scrollStackFit=WelcomePlayer(town,1,0,15);var scrollStack=NestedPack(scrollStackFit,720028,2,false);while(scrollStackFit.Inventory.Count<30)Pack(scrollStackFit,1000000,1);OpenDbPack(scrollStackFit,scrollStack);
        Check(!Fenced(scrollStackFit) && DbPackUses(scrollStackFit)==1 && DbBallCount(scrollStackFit)==10,"720028 stack remainder fits ten outputs when Count is 30");
        heard="";var scrollStackOver=WelcomePlayer(town,1,0,15);var scrollStackOverSrc=NestedPack(scrollStackOver,720028,2,false);while(scrollStackOver.Inventory.Count<31)Pack(scrollStackOver,1000000,1);OpenDbPack(scrollStackOver,scrollStackOverSrc);
        Check(!Fenced(scrollStackOver) && DbPackUses(scrollStackOver)==0 && scrollStackOver.Inventory.ContainsUID(scrollStackOverSrc.UID)
            && heard.IndexOf("Your inventory is Full.")>=0,"720028 stack remainder refuses at Count 31 instead of granting a partial ten");
        heard="";var fullPack=WelcomePlayer(town,1,0,15);var fullPackSrc=Pack(fullPack,720884,1);while(fullPack.Inventory.Count<31)Pack(fullPack,1000000,1);OpenDbPack(fullPack,fullPackSrc);
        Check(!Fenced(fullPack) && DbPackUses(fullPack)==0 && fullPack.Inventory.ContainsUID(fullPackSrc.UID) && DbBallCount(fullPack)==0
            && heard.IndexOf("Your inventory is Full.")>=0,"720884 refuses at leftover Count 31 even though five outputs would physically fit");
        heard="";var okPack=WelcomePlayer(town,1,0,15);var okPackSrc=Pack(okPack,720884,1);while(okPack.Inventory.Count<30)Pack(okPack,1000000,1);OpenDbPack(okPack,okPackSrc);
        Check(!Fenced(okPack) && DbPackUses(okPack)==1 && DbBallCount(okPack)==5 && heard=="","720884 pays at leftover Count 30");
        foreach(string fault in new[]{"source","output","receipt"}){
            heard="";var p=WelcomePlayer(town,1,0,15);var item=Pack(p,720884,1);
            string table=fault=="receipt"?"revora_dragon_ball_pack_uses":"items";
            string verb=fault=="source"?"DELETE":"INSERT";
            Query("CREATE FUNCTION suppress_db_pack() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_db_pack BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_db_pack()");OpenDbPack(p,item);
            Check(Fenced(p) && DbPackUses(p)==0 && p.Inventory.ContainsUID(item.UID) && DbBallCount(p)==0 && heard=="",
                "suppressed "+fault+" preserves 720884 source and DragonBalls");
            Query("DROP TRIGGER suppress_db_pack ON "+table+";DROP FUNCTION suppress_db_pack()");p=ReloadBall(p);OpenDbPack(p,p.Inventory.Objects.Single(i=>i.ID==720884));
            Check(!Fenced(p) && DbPackUses(p)==1 && DbBallCount(p)==5,"production reload retries 720884 after "+fault+" rollback");
        }
        var replaced=WelcomePlayer(town,1,0,15);var replacedPack=Pack(replaced,720028,1);
        Query("UPDATE items SET id=1000000 WHERE uid=@uid","uid",(long)replacedPack.UID);OpenDbPack(replaced,replacedPack);
        Check(Fenced(replaced) && DbPackUses(replaced)==0 && DbBallCount(replaced)==0 && Scalar("SELECT id FROM items WHERE uid=@uid",replacedPack.UID)==1000000,
            "same-UID durable source replacement cannot consume or pay a DragonBall pack");
        Query("UPDATE items SET id=720028 WHERE uid=@uid","uid",(long)replacedPack.UID);replaced=ReloadBall(replaced);
        OpenDbPack(replaced,replaced.Inventory.Objects.Single(i=>i.ID==720028));
        Check(!Fenced(replaced) && DbPackUses(replaced)==1 && DbBallCount(replaced)==10,
            "restored source replacement retries once after production reload");
        var extraBag=WelcomePlayer(town,1,0,15);var extraSrc=Pack(extraBag,720028,1);uint extraUid=ConquerItem.ItemUID.Next;
        Query("INSERT INTO items(uid,id,entityid,stacksize) VALUES(@uid,@id,@owner,1)","uid",(long)extraUid,"id",1000000,"owner",(long)extraBag.Entity.UID);OpenDbPack(extraBag,extraSrc);
        Check(Fenced(extraBag) && DbPackUses(extraBag)==0 && extraBag.Inventory.ContainsUID(extraSrc.UID),"an extra durable bag UID cannot open a DragonBall pack");
        Query("DELETE FROM items WHERE uid=@uid","uid",(long)extraUid);extraBag=ReloadBall(extraBag);OpenDbPack(extraBag,extraBag.Inventory.Objects.Single(i=>i.ID==720028));
        Check(!Fenced(extraBag) && DbPackUses(extraBag)==1 && DbBallCount(extraBag)==10,"production reload retries after the extra durable bag UID is removed");
        var missingBag=WelcomePlayer(town,1,0,15);var missingSrc=Pack(missingBag,720028,1);var cacheOnly=Pack(missingBag,1000000,1);
        Query("DELETE FROM items WHERE uid=@uid","uid",(long)cacheOnly.UID);OpenDbPack(missingBag,missingSrc);
        Check(Fenced(missingBag) && DbPackUses(missingBag)==0 && missingBag.Inventory.ContainsUID(missingSrc.UID),"a cache-only bag UID cannot open a DragonBall pack");
        missingBag=ReloadBall(missingBag);OpenDbPack(missingBag,missingBag.Inventory.Objects.Single(i=>i.ID==720028));
        Check(!Fenced(missingBag) && DbPackUses(missingBag)==1,"production reload retries after the cache-only bag UID is dropped");
        foreach(string guard in new[]{"dead","fake","trade","booth","locked","suspicious","wardrobe","expired","substituted"}){
            var p=WelcomePlayer(town,1,0,15);var item=Pack(p,720884,1);
            if(guard=="dead")p.Entity.Hitpoints=0;if(guard=="fake")p.Fake=true;if(guard=="trade")p.Trade.InTrade=true;
            if(guard=="booth")p.Booth=(Revora.Game.ConquerStructures.Booth)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Revora.Game.ConquerStructures.Booth));if(guard=="locked")item.Lock=1;
            if(guard=="suspicious")item.Suspicious=true;if(guard=="wardrobe")item.InWardrobe=true;if(guard=="expired"){item.Days=1;item.DayStamp=dbPackNow.ToLocalTime().AddDays(-2);}
            if(guard=="substituted")ItemHandler.UseItem(new ConquerItem(true){ID=item.ID,UID=item.UID,StackSize=1},p);else OpenDbPack(p,item);
            Check(DbPackUses(p)==0 && DbBallCount(p)==0 && p.Inventory.ContainsUID(item.UID),"ineligible 720884 leaves source unchanged: "+guard);
        }
        var frames=WelcomePlayer(town,1,0,15);var wire=Pack(frames,720884,1);
        foreach(string bad in new[]{"short","long","length","type","seal"}){
            byte[] frame=bad=="short"?new byte[31]:bad=="long"?new byte[129]:PrayerPacket(wire.UID);
            Writer.WriteUInt16((ushort)(frame.Length-8),0,frame);Writer.WriteUInt16(1009,2,frame);Writer.WriteUInt32(wire.UID,8,frame);Writer.WriteUInt16(4,20,frame);Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);
            if(bad=="length")Writer.WriteUInt16(1,0,frame);if(bad=="type")Writer.WriteUInt16(1008,2,frame);if(bad=="seal")frame[frame.Length-1]^=1;
            ItemUsage.Process(frame,frames);Check(!Fenced(frames) && DbPackUses(frames)==0 && frames.Inventory.ContainsUID(wire.UID),"bounded DragonBall pack rejects "+bad+" frame");
        }OpenDbPack(frames,wire);Check(!Fenced(frames) && DbPackUses(frames)==1 && DbBallCount(frames)==5,"ordinary DragonBall pack works after malformed frames");
        var replay=WelcomePlayer(town,1,0,15);var once=Pack(replay,720028,1);uint uid=once.UID;OpenDbPack(replay,once);
        ItemHandler.UseItem(once,replay);Check(DbPackUses(replay)==1 && !replay.Inventory.ContainsUID(uid) && DbBallCount(replay)==10,"consumed DragonBall pack UID cannot replay");
        var direct=WelcomePlayer(town,1,0,15);var directPack=Pack(direct,720884,1);ItemHandler.UseItem(directPack,direct);
        Check(!Fenced(direct) && DbPackUses(direct)==1 && DbBallCount(direct)==5,"direct UseItem opens 720884");
        var race=WelcomePlayer(town,1,0,15);var racePack=Pack(race,720884,1);
        Parallel.For(0,16,n=>OpenDbPack(race,racePack));
        Check(!Fenced(race) && DbPackUses(race)==1 && DbBallCount(race)==5,"parallel packets cannot duplicate DragonBall outputs");
        Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.MillionaireStonePacking"),null,"Initialize");
        town.Npcs[11275]=new NpcSpawn{UID=11275,MapID=1002,X=304,Y=433};
        var lee=WelcomePlayer(town,1,0,15);lee.Entity.X=304;lee.Entity.Y=433;lee.ActiveNpc=11275;
        for(int i=0;i<10;i++)Pack(lee,1088000,1);
        Npcs.GetDialog(new NpcRequest(true){NpcID=11275,OptionID=2},lee);
        Check(CountId(lee,720028)==1 && DbBallCount(lee)==0,"ordinary MillionaireLee packs ten DragonBalls into 720028");
        var scroll=lee.Inventory.Objects.Single(i=>i.ID==720028);OpenDbPack(lee,scroll);
        Check(!Fenced(lee) && DbPackUses(lee)==1 && DbBallCount(lee)==10,"packed DBScroll redeems through the owned service");
        var prize=World(runtime,1068);prize.Npcs[9623]=new NpcSpawn{UID=9623,MapID=1068,X=50,Y=50};
        var mammon=WelcomePlayer(prize,1,0,15);mammon.Entity.X=50;mammon.Entity.Y=50;mammon.ActiveNpc=9623;Pack(mammon,711611,1);
        Npcs.GetDialog(new NpcRequest(true){NpcID=9623,OptionID=7},mammon);
        Check(CountId(mammon,720884)==1 && CountId(mammon,711611)==0,"ordinary MammonEnvoy copper-coin exchange issues 720884");
        var five=mammon.Inventory.Objects.Single(i=>i.ID==720884);OpenDbPack(mammon,five);
        Check(!Fenced(mammon) && DbPackUses(mammon)==1 && DbBallCount(mammon)==5,"issued DragonBallPack(5) redeems through the owned service");
        var skip=WelcomePlayer(town,1,0,15);var skipPack=Pack(skip,722178,1);
        Check(!(bool)Call(DbPacks,null,"TryUse",skipPack,skip) && skip.Inventory.ContainsUID(skipPack.UID),"Surprise box 722178 stays outside DragonBallPacks");
        DbPacks.GetField("Spoken",Hidden).SetValue(null,null);
    }
}
