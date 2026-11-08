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
    static readonly Type DailySign=typeof(Entity).Assembly.GetType("Revora.Game.Features.DailySignInPacks");
    static DateTime dailySignNow=DateTime.UtcNow;
    static long DailySignUses(GameState p){return Scalar("SELECT count(*) FROM revora_daily_sign_in_pack_uses WHERE uid=@uid",p.Entity.UID);}
    static void OpenDailySign(GameState p,ConquerItem item){ItemUsage.Process(PrayerPacket(item.UID),p);}
    static bool DailyGift(GameState p,uint id,int n){
        ushort durability=ConquerItemInformation.BaseInformations[id].Durability;
        var gifts=p.Inventory.Objects.Where(i=>i.ID==id).ToArray();
        return gifts.Length==n && gifts.All(i=>!i.Bound && i.Plus==0 && i.StackSize==1 && i.Days==0 && i.Durability==durability && i.MaximDurability==durability
            && Scalar("SELECT count(*) FROM items WHERE uid=@uid AND bound=0 AND plus=0 AND stacksize=1 AND durability="+durability+" AND maximdurability="+durability,i.UID)==1);
    }
    static void PrefillSignDays(GameState p,int signed){
        p.Entity.DailySignDays=new byte[31];
        int today=DateTime.Now.Day-1,filled=0;
        for(int i=0;i<31 && filled<signed;i++){if(i==today)continue;p.Entity.DailySignDays[i]=1;filled++;}
    }
    static void VerifyDailySignInPacks(string runtime,Map town){
        Call(DailySign,null,"Initialize");Call(DailySign,null,"Initialize");
        DailySign.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>dailySignNow));
        SignInTable.Load();
        string handler=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemHandler.cs")));
        string usage=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemUsage.cs")));
        string thread=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Thread.cs")));
        string service=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/DailySignInPacks.cs")));
        string sign=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/SignIn/MsgSignIn.cs")));
        string packets=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/PacketHandler.cs")));
        string server=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Server.cs")));
        string audit=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../docs/ITEM_USE_COMPOUND_AUDIT.md")));
        string catalog=File.ReadAllText("database\\itemtype-new.txt");
        string signIni=File.ReadAllText("database\\slayer\\DayliSignIn\\signin.ini");
        string shopDat=File.ReadAllText("database\\shops\\Shop.dat");
        string v2=File.ReadAllText("database\\shops\\emoneyshopV2.ini");
        string eshop=File.ReadAllText("database\\shops\\emoneyshop.ini");
        string shopTxt=File.ReadAllText("database\\shops\\Shop.txt");
        string exchange=File.ReadAllText("database\\shops\\exchange_shop_goods.txt");
        string heroes=File.ReadAllText("database\\Slayer\\StageGoal.txt");
        Check(handler.IndexOf("Daily sign-in pack must use the owned service.")>=0 && handler.IndexOf("DailySignInPacks.TryUse")>=0
            && handler.IndexOf("case 3100011:")>=0 && handler.IndexOf("case 3100013:")>=0 && handler.IndexOf("case 3100014:")>=0
            && handler.IndexOf("case 3100015:")>=0 && handler.IndexOf("case 3100016:")>=0 && handler.IndexOf("case 3100017:")>=0,
            "Daily Sign-In packs are fail-closed on the live handler");
        Check(handler.IndexOf("DailySignInPacks.TryUse")>handler.IndexOf("EasterGoldPacks.TryUse")
            && usage.IndexOf("DailySignInPacks.TryHandle")>usage.IndexOf("EasterGoldPacks.TryHandle")
            && thread.IndexOf("DailySignInPacks.Initialize")>thread.IndexOf("EasterGoldPacks.Initialize"),
            "DailySignInPacks intercepts sit after EasterGoldPacks");
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
        Check(audit.IndexOf("| 3100011 | owned-fail-closed | source+items+silver+cp+rvp |")>=0
            && audit.IndexOf("| 3100013 | owned-fail-closed | source+items+silver+cp |")>=0
            && audit.IndexOf("| 3100014 | owned-fail-closed | source+items+boundcps |")>=0
            && audit.IndexOf("| 3100015 | owned-fail-closed | source+items |")>=0
            && audit.IndexOf("| 3100016 | owned-fail-closed | source+items+silver+cp+rvp+boundcps |")>=0
            && audit.IndexOf("| 3100017 | owned-fail-closed | source+items |")>=0,
            "audit owns the six Daily Sign-In packs and records BoundCps on 3100014/3100016");
        Check(catalog.IndexOf("3100011@@DailySign-inPack")>=0 && catalog.IndexOf("3100013@@2-daySign-inPack")>=0
            && catalog.IndexOf("3100014@@7-daySign-inPack")>=0 && catalog.IndexOf("3100015@@14-daySign-inPack")>=0
            && catalog.IndexOf("3100016@@21-daySign-inPack")>=0 && catalog.IndexOf("3100017@@28-daySign-inPack")>=0
            && catalog.IndexOf("2100085@@GoldTrophy")>=0 && catalog.IndexOf("723727@@PenitenceAmulet")>=0
            && catalog.IndexOf("3009002@@RadiantStarStone")>=0 && catalog.IndexOf("3009003@@SplendidStarStone")>=0
            && catalog.IndexOf("3004247@@P7WeaponSoulPack")>=0 && catalog.IndexOf("721170@@HousePermit")>=0
            && catalog.IndexOf("3100012@@SeniorSign-inBox")>=0 && handler.IndexOf("case 3100012:")<0,
            "catalog identities match leftover Daily Sign-In pack names; 3100012 has no UseItem case");
        Check(server.IndexOf("Database.SignInTable.Load();")>=0 && packets.IndexOf("MsgSignIn.Handle")>=0
            && signIni.IndexOf("prizeItemType=3100011")>=0 && signIni.IndexOf("needDays=1")>=0
            && SignInTable.Prizes[1].PrizeItemID==3100011u && SignInTable.Prizes[2].PrizeItemID==3100013u
            && SignInTable.Prizes[7].PrizeItemID==3100014u && SignInTable.Prizes[14].PrizeItemID==3100015u
            && SignInTable.Prizes[21].PrizeItemID==3100016u && SignInTable.Prizes[30].PrizeItemID==3100017u
            && !SignInTable.Prizes.ContainsKey(28),
            "startup-loaded signin.ini maps 1/2/7/14/21/30 days onto the six packs");
        Check(sign.IndexOf("Level >= 140 && client.Entity.Reborn == 2 && client.Entity.VIPLevel >= 5 && client.Entity.RevoraPoint >= 4000")>=0
            && sign.IndexOf("DailySignRewards == 21) next = 30")>=0 && sign.IndexOf("DailySignRewards == 21) next = 28")>=0,
            "normal Sign requires L140/reborn 2/VIP 5/4000 RVP; leftover LateSignIn still looks up day 28");
        Check(service.IndexOf("BoundCps(item.ID)")>=0 && service.IndexOf("500000u")>=0 && service.IndexOf("1500000u")>=0
            && service.IndexOf("ApplyCommittedBoundCps")>=0 && service.IndexOf("ApplyCommittedConquerPoints")>=0
            && service.IndexOf("SilverBalance.Persist")>=0 && service.IndexOf("revorapoint=@rvp")>=0,
            "owned service distinguishes BoundCps, ConquerPoints, silver and RevoraPoint");
        foreach(uint id in new uint[]{3100011u,3100013u,3100014u,3100015u,3100016u,3100017u})
            Check(eshop.IndexOf(id.ToString())<0 && v2.IndexOf(id.ToString())<0 && shopTxt.IndexOf(id.ToString())<0 && shopDat.IndexOf(id.ToString())<0
                && exchange.IndexOf(id.ToString())<0 && heroes.IndexOf(id.ToString())<0,
                id+" has no gold/emoney/exchange/StageGoal producer");
        string heard="";DailySign.GetField("Spoken",Hidden).SetValue(null,(Action<string>)(m=>{heard=heard+m+"|";}));
        heard="";var d1=WelcomePlayer(town,1,0,15);uint cp=d1.Entity.ConquerPoints;ulong silver=d1.Entity.Money;uint rvp=d1.Entity.RevoraPoint;var d1Pack=Pack(d1,3100011,1);OpenDailySign(d1,d1Pack);
        Check(!Fenced(d1) && DailySignUses(d1)==1 && !d1.Inventory.ContainsUID(d1Pack.UID) && DailyGift(d1,723727,2) && DailyGift(d1,3009002,1)
            && d1.Entity.ConquerPoints==cp+10000 && d1.Entity.Money==silver+50000000UL && d1.Entity.RevoraPoint==rvp+2000
            && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",d1.Entity.UID)==cp+10000
            && heard.IndexOf("Congratulations! You opened the daily pack and received everything.")>=0,"3100011 pays leftover items, 10k CP, 50m silver and 2000 RVP");
        heard="";var d2=WelcomePlayer(town,1,0,15);cp=d2.Entity.ConquerPoints;silver=d2.Entity.Money;var d2Pack=Pack(d2,3100013,1);OpenDailySign(d2,d2Pack);
        Check(!Fenced(d2) && DailySignUses(d2)==1 && DailyGift(d2,3009003,1) && d2.Entity.ConquerPoints==cp+20000 && d2.Entity.Money==silver+100000000UL
            && heard.IndexOf("Congratulations! You opened the 2-day daily pack and received everything.")>=0,"3100013 pays SplendidStarStone, 20k CP and 100m silver");
        heard="";var d7=WelcomePlayer(town,1,0,15);uint bound=d7.Entity.BoundCps;var d7Pack=Pack(d7,3100014,1);OpenDailySign(d7,d7Pack);
        Check(!Fenced(d7) && DailySignUses(d7)==1 && DailyGift(d7,720121,1) && CountId(d7,3004247)==0 && CountId(d7,3004248)==0 && CountId(d7,3004249)==0
            && d7.Entity.BoundCps==bound+500000 && Scalar("SELECT COALESCE(boundcps,0) FROM entities WHERE uid=@uid",d7.Entity.UID)==bound+500000
            && heard.IndexOf("Congratulations! You opened the 7-day daily pack and received everything.")>=0,"3100014 pays Holy pack, no P7 packs and 500000 Bound CPs");
        heard="";var d14=WelcomePlayer(town,1,0,15);cp=d14.Entity.ConquerPoints;silver=d14.Entity.Money;rvp=d14.Entity.RevoraPoint;bound=d14.Entity.BoundCps;var d14Pack=Pack(d14,3100015,1);OpenDailySign(d14,d14Pack);
        Check(!Fenced(d14) && DailySignUses(d14)==1 && DailyGift(d14,3009002,5) && DailyGift(d14,720121,1) && CountId(d14,3004247)==0
            && CountId(d14,3004248)==0 && CountId(d14,3004249)==0 && DailyGift(d14,721170,1)
            && d14.Entity.ConquerPoints==cp && d14.Entity.Money==silver && d14.Entity.RevoraPoint==rvp && d14.Entity.BoundCps==bound
            && heard.IndexOf("Congratulations! You opened the 14-day daily pack and received everything.")>=0,"3100015 pays seven non-P7 item outputs with no currency");
        heard="";var d21=WelcomePlayer(town,1,0,15);cp=d21.Entity.ConquerPoints;silver=d21.Entity.Money;rvp=d21.Entity.RevoraPoint;bound=d21.Entity.BoundCps;var d21Pack=Pack(d21,3100016,1);OpenDailySign(d21,d21Pack);
        Check(!Fenced(d21) && DailySignUses(d21)==1 && DailyGift(d21,3009003,5) && d21.Entity.ConquerPoints==cp+500000 && d21.Entity.Money==silver+1000000000UL
            && d21.Entity.RevoraPoint==rvp+20000 && d21.Entity.BoundCps==bound+1500000
            && heard.IndexOf("Congratulations! You opened the 21-day daily pack and received everything.")>=0,"3100016 pays five SplendidStarStones and leftover CP/silver/RVP/Bound CPs");
        heard="";var d30=WelcomePlayer(town,1,0,15);cp=d30.Entity.ConquerPoints;silver=d30.Entity.Money;rvp=d30.Entity.RevoraPoint;bound=d30.Entity.BoundCps;var d30Pack=Pack(d30,3100017,1);OpenDailySign(d30,d30Pack);
        Check(!Fenced(d30) && DailySignUses(d30)==1 && DailyGift(d30,2100085,1) && DailyGift(d30,3009003,5)
            && d30.Entity.ConquerPoints==cp && d30.Entity.Money==silver && d30.Entity.RevoraPoint==rvp && d30.Entity.BoundCps==bound
            && heard.IndexOf("Congratulations! You opened the 30-day daily pack and received the cup.")>=0,"3100017 pays GoldTrophy and five SplendidStarStones");
        foreach(bool boundSrc in new[]{false,true})foreach(ushort stack in new ushort[]{0,1,2}){
            var p=WelcomePlayer(town,1,0,15);var pack=NestedPack(p,3100013,stack,boundSrc);
            Parallel.For(0,24,i=>{if(i%3==0)EntityTable.SaveEntity(p);else OpenDailySign(p,pack);});
            int expected=stack>1?2:1;
            Check(!Fenced(p) && DailySignUses(p)==expected && CountId(p,3009003)==expected && CountId(p,3100013)==0,
                "3100013 pays with stack remainder semantics: "+boundSrc+"/"+stack);
        }
        foreach(bool boundSrc in new[]{false,true})foreach(ushort stack in new ushort[]{0,1,2}){
            var p=WelcomePlayer(town,1,0,15);var pack=NestedPack(p,3100011,stack,boundSrc);
            Parallel.For(0,24,i=>{if(i%3==0)EntityTable.SaveEntity(p);else OpenDailySign(p,pack);});
            int expected=stack>1?2:1;
            Check(!Fenced(p) && DailySignUses(p)==expected && CountId(p,723727)==2*expected && CountId(p,3009002)==expected,
                "3100011 pays with stack remainder semantics: "+boundSrc+"/"+stack);
        }
        foreach(bool boundSrc in new[]{false,true})foreach(ushort stack in new ushort[]{0,1,2}){
            var p=WelcomePlayer(town,1,0,15);uint purse=p.Entity.BoundCps;var pack=NestedPack(p,3100014,stack,boundSrc);
            Parallel.For(0,24,i=>{if(i%3==0)EntityTable.SaveEntity(p);else OpenDailySign(p,pack);});
            int expected=stack>1?2:1;
            Check(!Fenced(p) && DailySignUses(p)==expected && CountId(p,720121)==expected && p.Entity.BoundCps==purse+(uint)(500000*expected),
                "3100014 pays with stack remainder semantics: "+boundSrc+"/"+stack);
        }
        foreach(bool boundSrc in new[]{false,true})foreach(ushort stack in new ushort[]{0,1,2}){
            var p=WelcomePlayer(town,1,0,15);var pack=NestedPack(p,3100017,stack,boundSrc);
            Parallel.For(0,24,i=>{if(i%3==0)EntityTable.SaveEntity(p);else OpenDailySign(p,pack);});
            int expected=stack>1?2:1;
            Check(!Fenced(p) && DailySignUses(p)==expected && CountId(p,2100085)==expected && CountId(p,3009003)==5*expected,
                "3100017 pays with stack remainder semantics: "+boundSrc+"/"+stack);
        }
        heard="";var fit=WelcomePlayer(town,1,0,15);var fitSrc=Pack(fit,3100015,1);while(fit.Inventory.Count<34)Pack(fit,1000000,1);OpenDailySign(fit,fitSrc);
        Check(!Fenced(fit) && DailySignUses(fit)==1 && CountId(fit,3009002)==5 && !fit.Inventory.ContainsUID(fitSrc.UID),"3100015 singleton fits 7 outputs when Count is 34");
        heard="";var shortBag=WelcomePlayer(town,1,0,15);var shortSrc=Pack(shortBag,3100015,1);while(shortBag.Inventory.Count<35)Pack(shortBag,1000000,1);OpenDailySign(shortBag,shortSrc);
        Check(!Fenced(shortBag) && DailySignUses(shortBag)==0 && shortBag.Inventory.ContainsUID(shortSrc.UID)
            && heard.IndexOf("Your inventory is Full.")>=0,"3100015 singleton refuses at Count 35");
        heard="";var keepFit=WelcomePlayer(town,1,0,15);var keepSrc=NestedPack(keepFit,3100015,2,false);while(keepFit.Inventory.Count<33)Pack(keepFit,1000000,1);OpenDailySign(keepFit,keepSrc);
        Check(!Fenced(keepFit) && DailySignUses(keepFit)==1 && keepSrc.StackSize==1,"3100015 remainder fits 7 outputs when Count is 33");
        heard="";var keepShort=WelcomePlayer(town,1,0,15);var keepShortSrc=NestedPack(keepShort,3100015,2,false);while(keepShort.Inventory.Count<34)Pack(keepShort,1000000,1);OpenDailySign(keepShort,keepShortSrc);
        Check(!Fenced(keepShort) && DailySignUses(keepShort)==0 && keepShort.Inventory.ContainsUID(keepShortSrc.UID)
            && heard.IndexOf("Your inventory is Full.")>=0,"3100015 remainder refuses at Count 34");
        heard="";var cpOverflow=WelcomePlayer(town,1,0,15);Query("UPDATE entities SET conquerpoints=4294967295 WHERE uid=@uid","uid",(long)cpOverflow.Entity.UID);cpOverflow=ReloadBall(cpOverflow);
        var cpPack=Pack(cpOverflow,3100011,1);OpenDailySign(cpOverflow,cpPack);
        Check(!Fenced(cpOverflow) && DailySignUses(cpOverflow)==0 && cpOverflow.Inventory.ContainsUID(cpPack.UID) && cpOverflow.Entity.ConquerPoints==uint.MaxValue
            && heard.IndexOf("Spend some CPs before opening this pack.")>=0,"CP overflow preserves 3100011");
        heard="";var silverOverflow=WelcomePlayer(town,1,0,15);silverOverflow.Entity.Money=Entity.MaximumSilver;EntityTable.UpdateMoney(silverOverflow);
        var silverPack=Pack(silverOverflow,3100013,1);OpenDailySign(silverOverflow,silverPack);
        Check(!Fenced(silverOverflow) && DailySignUses(silverOverflow)==0 && silverOverflow.Inventory.ContainsUID(silverPack.UID)
            && heard.IndexOf("Store some silver before opening this pack.")>=0,"silver overflow preserves 3100013");
        heard="";var rvpOverflow=WelcomePlayer(town,1,0,15);Query("UPDATE entities SET revorapoint=4294967295 WHERE uid=@uid","uid",(long)rvpOverflow.Entity.UID);rvpOverflow=ReloadBall(rvpOverflow);
        var rvpPack=Pack(rvpOverflow,3100011,1);OpenDailySign(rvpOverflow,rvpPack);
        Check(!Fenced(rvpOverflow) && DailySignUses(rvpOverflow)==0 && rvpOverflow.Inventory.ContainsUID(rvpPack.UID)
            && heard.IndexOf("Spend some Revora Points before opening this pack.")>=0,"RVP overflow preserves 3100011");
        heard="";var boundOverflow=WelcomePlayer(town,1,0,15);Query("UPDATE entities SET boundcps=4294967295 WHERE uid=@uid","uid",(long)boundOverflow.Entity.UID);boundOverflow=ReloadBall(boundOverflow);
        var boundPack=Pack(boundOverflow,3100014,1);OpenDailySign(boundOverflow,boundPack);
        Check(!Fenced(boundOverflow) && DailySignUses(boundOverflow)==0 && boundOverflow.Inventory.ContainsUID(boundPack.UID)
            && heard.IndexOf("Spend some Bound CPs before opening this pack.")>=0,"Bound CP overflow preserves 3100014");
        foreach(string fault in new[]{"source","output","receipt"}){
            var p=WelcomePlayer(town,1,0,15);var item=Pack(p,3100013,1);
            string table=fault=="receipt"?"revora_daily_sign_in_pack_uses":"items";
            string verb=fault=="source"?"DELETE":"INSERT";
            Query("CREATE FUNCTION suppress_daily_sign() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_daily_sign BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_daily_sign()");OpenDailySign(p,item);
            Check(Fenced(p) && DailySignUses(p)==0 && p.Inventory.ContainsUID(item.UID) && CountId(p,3009003)==0,"suppressed "+fault+" preserves 3100013");
            Query("DROP TRIGGER suppress_daily_sign ON "+table+";DROP FUNCTION suppress_daily_sign()");p=ReloadBall(p);OpenDailySign(p,p.Inventory.Objects.Single(i=>i.ID==3100013));
            Check(!Fenced(p) && DailySignUses(p)==1 && CountId(p,3009003)==1,"production reload retries 3100013 after "+fault+" rollback");
        }
        foreach(string fault in new[]{"cps","silver","rvp"}){
            var p=WelcomePlayer(town,1,0,15);var item=Pack(p,3100011,1);uint beforeCp=p.Entity.ConquerPoints;ulong beforeSilver=p.Entity.Money;uint beforeRvp=p.Entity.RevoraPoint;
            string table=fault=="silver"?"entities":"entities";
            string verb="UPDATE";
            string body=fault=="cps"?"IF NEW.conquerpoints IS DISTINCT FROM OLD.conquerpoints THEN RETURN NULL; END IF; RETURN NEW;"
                :fault=="rvp"?"IF NEW.revorapoint IS DISTINCT FROM OLD.revorapoint THEN RETURN NULL; END IF; RETURN NEW;"
                :"IF NEW.money IS DISTINCT FROM OLD.money THEN RETURN NULL; END IF; RETURN NEW;";
            Query("CREATE FUNCTION suppress_daily_ccy() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN "+body+" END';CREATE TRIGGER suppress_daily_ccy BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_daily_ccy()");OpenDailySign(p,item);
            Check(Fenced(p) && DailySignUses(p)==0 && p.Inventory.ContainsUID(item.UID) && p.Entity.ConquerPoints==beforeCp && p.Entity.Money==beforeSilver && p.Entity.RevoraPoint==beforeRvp,
                "suppressed "+fault+" preserves 3100011 source and currencies");
            Query("DROP TRIGGER suppress_daily_ccy ON entities;DROP FUNCTION suppress_daily_ccy()");p=ReloadBall(p);OpenDailySign(p,p.Inventory.Objects.Single(i=>i.ID==3100011));
            Check(!Fenced(p) && DailySignUses(p)==1,"production reload retries 3100011 after "+fault+" rollback");
        }
        var boundFault=WelcomePlayer(town,1,0,15);var boundItem=Pack(boundFault,3100014,1);uint beforePurse=boundFault.Entity.BoundCps;
        Query("CREATE FUNCTION suppress_daily_bound() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF NEW.boundcps IS DISTINCT FROM OLD.boundcps THEN RETURN NULL; END IF; RETURN NEW; END';CREATE TRIGGER suppress_daily_bound BEFORE UPDATE ON entities FOR EACH ROW EXECUTE FUNCTION suppress_daily_bound()");OpenDailySign(boundFault,boundItem);
        Check(Fenced(boundFault) && DailySignUses(boundFault)==0 && boundFault.Inventory.ContainsUID(boundItem.UID) && boundFault.Entity.BoundCps==beforePurse,"suppressed Bound CPs preserve 3100014");
        Query("DROP TRIGGER suppress_daily_bound ON entities;DROP FUNCTION suppress_daily_bound()");boundFault=ReloadBall(boundFault);OpenDailySign(boundFault,boundFault.Inventory.Objects.Single(i=>i.ID==3100014));
        Check(!Fenced(boundFault) && DailySignUses(boundFault)==1 && boundFault.Entity.BoundCps==beforePurse+500000,"production reload retries 3100014 after Bound CP rollback");
        var replaced=WelcomePlayer(town,1,0,15);var replacedPack=Pack(replaced,3100017,1);
        Query("UPDATE items SET id=1000000 WHERE uid=@uid","uid",(long)replacedPack.UID);OpenDailySign(replaced,replacedPack);
        Check(Fenced(replaced) && DailySignUses(replaced)==0 && CountId(replaced,2100085)==0,"same-UID durable source replacement cannot consume a Daily Sign-In pack");
        Query("UPDATE items SET id=3100017 WHERE uid=@uid","uid",(long)replacedPack.UID);replaced=ReloadBall(replaced);
        OpenDailySign(replaced,replaced.Inventory.Objects.Single(i=>i.ID==3100017));
        Check(!Fenced(replaced) && DailySignUses(replaced)==1 && CountId(replaced,2100085)==1,"restored source replacement retries once after production reload");
        var extraBag=WelcomePlayer(town,1,0,15);var extraSrc=Pack(extraBag,3100013,1);uint extraUid=ConquerItem.ItemUID.Next;
        Query("INSERT INTO items(uid,id,entityid,stacksize) VALUES(@uid,@id,@owner,1)","uid",(long)extraUid,"id",1000000,"owner",(long)extraBag.Entity.UID);OpenDailySign(extraBag,extraSrc);
        Check(Fenced(extraBag) && DailySignUses(extraBag)==0 && extraBag.Inventory.ContainsUID(extraSrc.UID),"an extra durable bag UID cannot open a Daily Sign-In pack");
        Query("DELETE FROM items WHERE uid=@uid","uid",(long)extraUid);extraBag=ReloadBall(extraBag);OpenDailySign(extraBag,extraBag.Inventory.Objects.Single(i=>i.ID==3100013));
        Check(!Fenced(extraBag) && DailySignUses(extraBag)==1,"production reload retries after the extra durable bag UID is removed");
        var missingBag=WelcomePlayer(town,1,0,15);var missingSrc=Pack(missingBag,3100013,1);var cacheOnly=Pack(missingBag,1000000,1);
        Query("DELETE FROM items WHERE uid=@uid","uid",(long)cacheOnly.UID);OpenDailySign(missingBag,missingSrc);
        Check(Fenced(missingBag) && DailySignUses(missingBag)==0 && missingBag.Inventory.ContainsUID(missingSrc.UID),"a cache-only bag UID cannot open a Daily Sign-In pack");
        missingBag=ReloadBall(missingBag);OpenDailySign(missingBag,missingBag.Inventory.Objects.Single(i=>i.ID==3100013));
        Check(!Fenced(missingBag) && DailySignUses(missingBag)==1,"production reload retries after the cache-only bag UID is dropped");
        var frames=WelcomePlayer(town,1,0,15);var wire=Pack(frames,3100013,1);
        foreach(string bad in new[]{"short","long","length","type","seal"}){
            byte[] frame=bad=="short"?new byte[31]:bad=="long"?new byte[129]:PrayerPacket(wire.UID);
            Writer.WriteUInt16((ushort)(frame.Length-8),0,frame);Writer.WriteUInt16(1009,2,frame);Writer.WriteUInt32(wire.UID,8,frame);Writer.WriteUInt16(4,20,frame);Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);
            if(bad=="length")Writer.WriteUInt16(1,0,frame);if(bad=="type")Writer.WriteUInt16(1008,2,frame);if(bad=="seal")frame[frame.Length-1]^=1;
            ItemUsage.Process(frame,frames);Check(!Fenced(frames) && DailySignUses(frames)==0 && frames.Inventory.ContainsUID(wire.UID),"bounded Daily Sign-In pack rejects "+bad+" frame");
        }OpenDailySign(frames,wire);Check(!Fenced(frames) && DailySignUses(frames)==1,"ordinary Daily Sign-In pack works after malformed frames");
        var replay=WelcomePlayer(town,1,0,15);var once=Pack(replay,3100017,1);uint uid=once.UID;OpenDailySign(replay,once);
        ItemHandler.UseItem(once,replay);Check(DailySignUses(replay)==1 && !replay.Inventory.ContainsUID(uid),"consumed Daily Sign-In pack UID cannot replay");
        var direct=WelcomePlayer(town,1,0,15);var directPack=Pack(direct,3100016,1);ItemHandler.UseItem(directPack,direct);
        Check(!Fenced(direct) && DailySignUses(direct)==1 && CountId(direct,3009003)==5,"direct UseItem opens 3100016");
        var race=WelcomePlayer(town,1,0,15);var racePack=Pack(race,3100014,1);
        Parallel.For(0,16,n=>OpenDailySign(race,racePack));
        Check(!Fenced(race) && DailySignUses(race)==1 && CountId(race,720121)==1 && race.Entity.BoundCps==500000,"parallel packets cannot duplicate 3100014 rewards");
        var signer=WelcomePlayer(town,140,2,15);signer.Entity.VIPLevel=5;signer.Entity.RevoraPoint=4000;signer.Entity.DailySignDays=new byte[31];
        MsgSignIn.Handle(signer,SignInFrame(0));
        Check(CountId(signer,3100011)==1 && signer.Inventory.Objects.Single(i=>i.ID==3100011).Days==2,"packet-driven normal Sign issues the 2-day 3100011 pack");
        MsgSignIn.Handle(signer,SignInFrame(0));
        Check(CountId(signer,3100011)==1,"same-day Sign replay does not issue a second pack");
        OpenDailySign(signer,signer.Inventory.Objects.Single(i=>i.ID==3100011));
        Check(!Fenced(signer) && DailySignUses(signer)==1 && CountId(signer,723727)==2,"sign-issued 3100011 redeems through the owned service");
        var low=WelcomePlayer(town,139,2,15);low.Entity.VIPLevel=5;low.Entity.RevoraPoint=4000;low.Entity.DailySignDays=new byte[31];
        MsgSignIn.Handle(low,SignInFrame(0));
        Check(CountId(low,3100011)==0,"Sign refuses below level 140");
        var vip=WelcomePlayer(town,140,2,15);vip.Entity.VIPLevel=4;vip.Entity.RevoraPoint=4000;vip.Entity.DailySignDays=new byte[31];
        MsgSignIn.Handle(vip,SignInFrame(0));
        Check(CountId(vip,3100011)==0,"Sign refuses below VIP 5");
        var two=WelcomePlayer(town,140,2,15);two.Entity.VIPLevel=5;two.Entity.RevoraPoint=4000;two.Entity.DailySignRewards=0;PrefillSignDays(two,1);
        MsgSignIn.Handle(two,SignInFrame(0));
        Check(CountId(two,3100011)==1 && CountId(two,3100013)==1,"packet-driven Sign at two days issues 3100013");
        OpenDailySign(two,two.Inventory.Objects.Single(i=>i.ID==3100013));
        Check(!Fenced(two) && DailySignUses(two)==1 && CountId(two,3009003)==1,"sign-issued 3100013 redeems through the owned service");
        var week=WelcomePlayer(town,140,2,15);week.Entity.VIPLevel=5;week.Entity.RevoraPoint=4000;week.Entity.DailySignRewards=2;PrefillSignDays(week,6);
        MsgSignIn.Handle(week,SignInFrame(0));
        Check(CountId(week,3100014)==1,"packet-driven Sign at seven days issues 3100014");
        OpenDailySign(week,week.Inventory.Objects.Single(i=>i.ID==3100014));
        Check(!Fenced(week) && DailySignUses(week)==1 && CountId(week,720121)==1,"sign-issued 3100014 redeems through the owned service");
        var fortnight=WelcomePlayer(town,140,2,15);fortnight.Entity.VIPLevel=5;fortnight.Entity.RevoraPoint=4000;fortnight.Entity.DailySignRewards=7;PrefillSignDays(fortnight,13);
        MsgSignIn.Handle(fortnight,SignInFrame(0));
        Check(CountId(fortnight,3100015)==1,"packet-driven Sign at fourteen days issues 3100015");
        OpenDailySign(fortnight,fortnight.Inventory.Objects.Single(i=>i.ID==3100015));
        Check(!Fenced(fortnight) && DailySignUses(fortnight)==1 && CountId(fortnight,721170)==1,"sign-issued 3100015 redeems through the owned service");
        var three=WelcomePlayer(town,140,2,15);three.Entity.VIPLevel=5;three.Entity.RevoraPoint=4000;three.Entity.DailySignRewards=14;PrefillSignDays(three,20);
        MsgSignIn.Handle(three,SignInFrame(0));
        Check(CountId(three,3100016)==1,"packet-driven Sign at twenty-one days issues 3100016");
        OpenDailySign(three,three.Inventory.Objects.Single(i=>i.ID==3100016));
        Check(!Fenced(three) && DailySignUses(three)==1 && CountId(three,3009003)==5,"sign-issued 3100016 redeems through the owned service");
        var month=WelcomePlayer(town,140,2,15);month.Entity.VIPLevel=5;month.Entity.RevoraPoint=4000;month.Entity.DailySignRewards=21;PrefillSignDays(month,29);
        MsgSignIn.Handle(month,SignInFrame(0));
        Check(CountId(month,3100017)==1,"packet-driven Sign at thirty days issues 3100017");
        OpenDailySign(month,month.Inventory.Objects.Single(i=>i.ID==3100017));
        Check(!Fenced(month) && DailySignUses(month)==1 && CountId(month,2100085)==1,"sign-issued 3100017 redeems through the owned service");
        var late=WelcomePlayer(town,140,2,15);late.Entity.VIPLevel=7;late.Entity.RevoraPoint=20000;late.Entity.DailySignVIPChances=10;late.Entity.DailySignRewards=0;
        late.Entity.DailySignDays=new byte[31];late.Entity.DailySignDays[DateTime.Now.Day>=31?29:30]=1;
        MsgSignIn.Handle(late,SignInFrame(1));
        Check(CountId(late,3100011)==1 && CountId(late,3100013)==1,"packet-driven LateSignIn at two days issues 3100013");
        var vip6=WelcomePlayer(town,140,2,15);vip6.Entity.VIPLevel=6;vip6.Entity.RevoraPoint=20000;vip6.Entity.DailySignVIPChances=10;vip6.Entity.DailySignDays=new byte[31];
        MsgSignIn.Handle(vip6,SignInFrame(1));
        Check(CountId(vip6,3100011)==0,"LateSignIn refuses VIP 6");
        var late28=WelcomePlayer(town,140,2,15);late28.Entity.VIPLevel=7;late28.Entity.RevoraPoint=200000;late28.Entity.DailySignVIPChances=10;late28.Entity.DailySignRewards=21;PrefillSignDays(late28,27);
        bool threw=false;try{MsgSignIn.Handle(late28,SignInFrame(1));}catch{threw=true;}
        Check(threw && CountId(late28,3100017)==0,"leftover LateSignIn day-28 lookup has no installed prize");
        var holy=WelcomePlayer(town,1,0,15);var holyPack=Pack(holy,720121,1);
        Check(!(bool)Call(DailySign,null,"TryUse",holyPack,holy) && holy.Inventory.ContainsUID(holyPack.UID),"EasterGoldPack 720121 stays outside DailySignInPacks");
        var stones=WelcomePlayer(town,1,0,15);var holyStone=Pack(stones,3004246,20);
        Check(!(bool)Call(DailySign,null,"TryUse",holyStone,stones) && stones.Inventory.ContainsUID(holyStone.UID),"leftover 3004246 combine stays outside DailySignInPacks");
        DailySign.GetField("Spoken",Hidden).SetValue(null,null);
    }
}
