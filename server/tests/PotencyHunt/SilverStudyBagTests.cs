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
    static readonly Type SilverBags=typeof(Entity).Assembly.GetType("Revora.Game.Features.SilverStudyBags");
    static DateTime silverBagNow=DateTime.UtcNow;
    static long SilverBagUses(GameState p){return Scalar("SELECT count(*) FROM revora_silver_study_bag_uses WHERE uid=@uid",p.Entity.UID);}
    static long StudyStored(GameState p){return Scalar("SELECT COALESCE(studypoints,0) FROM entities WHERE uid=@uid",p.Entity.UID);}
    static long SilverStored(GameState p){return Scalar("SELECT COALESCE(money,0) FROM entities WHERE uid=@uid",p.Entity.UID);}
    static void OpenSilverBag(GameState p,ConquerItem item){ItemUsage.Process(PrayerPacket(item.UID),p);}
    static GameState SilverBagPlayer(Map town){return WelcomePlayer(town,1,0,15);}
    static void VerifySilverStudyBags(string runtime,Map town){
        Call(SilverBags,null,"Initialize");Call(SilverBags,null,"Initialize");
        SilverBags.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>silverBagNow));
        string handler=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemHandler.cs")));
        string service=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/SilverStudyBags.cs")));
        string audit=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../docs/ITEM_USE_COMPOUND_AUDIT.md")));
        Check(handler.IndexOf("Silver/study bag must use the owned service.")>=0 && handler.IndexOf("SilverStudyBags.TryUse")>=0
            && handler.IndexOf("case 3005117:")>=0 && handler.IndexOf("case 3005125:")>=0,
            "silver/study bags are fail-closed on the live handler");
        Check(handler.IndexOf("GetPositionFromID")>=0 && handler.IndexOf("Inventory = 0")>=0,
            "ordinary 1009/action-4 still dispatches non-equipment IDs through UseItem");
        Check(service.IndexOf("{3005118,new Spec{Silver=5000}}")>=0 && service.IndexOf("{3005121,new Spec{Silver=10000}}")>=0
            && service.IndexOf("{3005122,new Spec{Silver=20000,Study=400")>=0,
            "executable silver amounts stay 5000/5000/5000/5000/10000/20000 even when catalog copy names larger purses");
        int switchStart=handler.IndexOf("switch (item.ID)"),switchEnd=handler.IndexOf("public static byte GetNextRefineryItem",switchStart);
        var sourceIds=System.Text.RegularExpressions.Regex.Matches(handler.Substring(switchStart,switchEnd-switchStart),@"(?m)^ {16}case (\d+):").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        var auditIds=System.Text.RegularExpressions.Regex.Matches(audit,@"(?m)^\| (\d+) \|").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        Check(sourceIds.Length==518 && sourceIds.OrderBy(id=>id).SequenceEqual(auditIds.OrderBy(id=>id)),
            "compound audit still enumerates the exact 518 numeric top-level UseItem case IDs");
        uint[] ids=new uint[]{3005117u,3005118u,3005119u,3005120u,3005121u,3005122u,3005123u,3005124u,3005125u};
        ulong[] silver=new ulong[]{5000UL,5000UL,5000UL,5000UL,10000UL,20000UL,0UL,0UL,0UL};
        ushort[] study=new ushort[]{0,0,0,0,0,400,200,300,500};
        string[] texts=new string[]{null,null,null,null,null,"You received 400 Study Points!","You received 200 Study Points!","You received 300 Study Points!","You received 500 Study Points!"};
        string[] names=new string[]{"Class1FortunePack","Class2FortunePack","Class3FortunePack","Class4FortunePack","Class5FortunePack","Class6FortunePack","DiligentStudyPack","EliteStudyPack","SuperStudyPack"};
        string[] descriptions=new string[]{"5,000~Silver","10,000~Silver","20,000~Silver","50,000~Silver","100,000~Silver","200,000~Silver~and~400~Study~points","200~Study~Points","300~Study~Points","500~Study~Points"};
        string heroes=File.ReadAllText("database\\Slayer\\StageGoal.txt");
        string itemtype=File.ReadAllText("database\\itemtype-new.txt");
        for(int n=0;n<ids.Length;n++){
            Check(audit.IndexOf("| "+ids[n].ToString()+" | owned-fail-closed |")>=0,"audit owns silver/study bag "+ids[n]);
            Check(ConquerItemInformation.BaseInformations.ContainsKey(ids[n]),"installed catalog contains silver/study bag "+ids[n]);
            Check(heroes.IndexOf("Prize1="+ids[n].ToString())>=0,"Way of Heroes StageGoal issues "+ids[n]);
            string catalogLine=itemtype.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).Single(line=>line.StartsWith(ids[n].ToString()+"@@",StringComparison.Ordinal));
            Check(catalogLine.IndexOf("@@"+names[n]+"@@",StringComparison.Ordinal)>=0 && catalogLine.IndexOf(descriptions[n],StringComparison.Ordinal)>=0,
                "installed catalog identity and copy match silver/study bag "+ids[n]);
        }
        Check(itemtype.IndexOf("3005122@@Class6FortunePack",StringComparison.Ordinal)>=0 && itemtype.IndexOf("200,000~Silver~and~400~Study~points",StringComparison.Ordinal)>=0,
            "3005122 catalog copy names 200,000 Silver while the leftover handler pays 20,000");
        Check(File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/WayOfHeroesClaims.cs"))).IndexOf("Plus=0,Bound=true")>=0,
            "Way of Heroes owned claims grant a bound plus-0 prize");
        string heard="";SilverBags.GetField("Spoken",Hidden).SetValue(null,(Action<string>)(m=>{heard=heard+m+"|";}));
        for(int n=0;n<ids.Length;n++){
            heard="";var p=SilverBagPlayer(town);ulong beforeSilver=p.Entity.Money;int beforeStudy=p.Entity.SubClasses.StudyPoints;
            var item=Pack(p,ids[n],1);OpenSilverBag(p,item);
            Check(!Fenced(p) && SilverBagUses(p)==1 && p.Entity.Money==beforeSilver+silver[n]
                && p.Entity.SubClasses.StudyPoints==beforeStudy+study[n] && !p.Inventory.ContainsUID(item.UID)
                && (texts[n]==null?heard.IndexOf("You received")<0:heard.IndexOf(texts[n])>=0),
                "silver/study bag "+ids[n]+" pays from ordinary action-4");
            p=ReloadBall(p);
            Check(SilverBagUses(p)==1 && p.Entity.Money==beforeSilver+silver[n] && p.Entity.SubClasses.StudyPoints==beforeStudy+study[n]
                && SilverStored(p)==(long)(beforeSilver+silver[n]) && StudyStored(p)==beforeStudy+study[n],
                "silver/study bag "+ids[n]+" survives production reload");
        }
        foreach(bool bound in new[]{false,true})foreach(ushort stack in new ushort[]{0,1,2}){
            var p=SilverBagPlayer(town);var pack=NestedPack(p,3005117,stack,bound);ulong before=p.Entity.Money;
            Parallel.For(0,24,n=>{if(n%3==0)EntityTable.SaveEntity(p);else OpenSilverBag(p,pack);});
            int expected=stack>1?2:1;
            Check(!Fenced(p) && SilverBagUses(p)==expected && p.Entity.Money==before+5000UL*(ulong)expected && CountId(p,3005117)==0,
                "3005117 pays 5000 silver with stack remainder semantics: "+bound+"/"+stack);
        }
        var studyRace=SilverBagPlayer(town);var studyStack=NestedPack(studyRace,3005125,2,true);
        Parallel.For(0,24,n=>{if(n%3==0)EntityTable.SaveEntity(studyRace);else OpenSilverBag(studyRace,studyStack);});
        Check(!Fenced(studyRace) && SilverBagUses(studyRace)==2 && studyRace.Entity.SubClasses.StudyPoints==1000
            && StudyStored(studyRace)==1000 && CountId(studyRace,3005125)==0,
            "parallel character save and study-bag use preserve both payments");
        heard="";var overflow=SilverBagPlayer(town);Query("UPDATE entities SET money=@m WHERE uid=@uid","m",long.MaxValue,"uid",(long)overflow.Entity.UID);
        overflow=ReloadBall(overflow);var rich=Pack(overflow,3005117,1);OpenSilverBag(overflow,rich);
        Check(!Fenced(overflow) && SilverBagUses(overflow)==0 && overflow.Inventory.ContainsUID(rich.UID)
            && heard.IndexOf("Spend some silver before opening this pack.")>=0,"checked silver overflow preserves the bag");
        heard="";var studyFull=SilverBagPlayer(town);studyFull.Entity.SubClasses.StudyPoints=ushort.MaxValue;
        Query("UPDATE entities SET studypoints=@s WHERE uid=@uid","s",(int)ushort.MaxValue,"uid",(long)studyFull.Entity.UID);
        var fullStudy=Pack(studyFull,3005125,1);OpenSilverBag(studyFull,fullStudy);
        Check(!Fenced(studyFull) && SilverBagUses(studyFull)==0 && studyFull.Inventory.ContainsUID(fullStudy.UID)
            && studyFull.Entity.SubClasses.StudyPoints==ushort.MaxValue && heard.IndexOf("Spend some Study Points before opening this pack.")>=0,
            "checked study overflow preserves the bag");
        foreach(string fault in new[]{"source","silver","receipt"}){
            var p=SilverBagPlayer(town);var item=Pack(p,3005117,1);ulong before=p.Entity.Money;
            string table=fault=="receipt"?"revora_silver_study_bag_uses":fault=="silver"?"entities":"items";
            string verb=fault=="source"?"DELETE":fault=="receipt"?"INSERT":"UPDATE";
            Query("CREATE FUNCTION suppress_silver_bag() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_silver_bag BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_silver_bag()");OpenSilverBag(p,item);
            Check(Fenced(p) && SilverBagUses(p)==0 && p.Inventory.ContainsUID(item.UID) && p.Entity.Money==before,"suppressed "+fault+" preserves silver bag source and purse");
            Query("DROP TRIGGER suppress_silver_bag ON "+table+";DROP FUNCTION suppress_silver_bag()");p=ReloadBall(p);OpenSilverBag(p,p.Inventory.Objects.Single(i=>i.ID==3005117));
            Check(!Fenced(p) && SilverBagUses(p)==1 && p.Entity.Money==before+5000UL,"production reload retries the silver bag after "+fault+" rollback");
        }
        heard="";var combinedFault=SilverBagPlayer(town);var combinedPack=Pack(combinedFault,3005122,1);
        Query("CREATE FUNCTION suppress_study_bag() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF NEW.studypoints IS DISTINCT FROM OLD.studypoints THEN RETURN NULL; END IF; RETURN NEW; END';CREATE TRIGGER suppress_study_bag BEFORE UPDATE ON entities FOR EACH ROW EXECUTE FUNCTION suppress_study_bag()");
        OpenSilverBag(combinedFault,combinedPack);
        Check(Fenced(combinedFault) && SilverBagUses(combinedFault)==0 && combinedFault.Inventory.ContainsUID(combinedPack.UID)
            && combinedFault.Entity.Money==0 && combinedFault.Entity.SubClasses.StudyPoints==0 && SilverStored(combinedFault)==0 && StudyStored(combinedFault)==0
            && heard.IndexOf("You received 400 Study Points!")<0,
            "suppressed study update rolls back the combined source, silver, study, receipt and message");
        Query("DROP TRIGGER suppress_study_bag ON entities;DROP FUNCTION suppress_study_bag()");
        combinedFault=ReloadBall(combinedFault);OpenSilverBag(combinedFault,combinedFault.Inventory.Objects.Single(i=>i.ID==3005122));
        Check(!Fenced(combinedFault) && SilverBagUses(combinedFault)==1 && combinedFault.Entity.Money==20000
            && combinedFault.Entity.SubClasses.StudyPoints==400 && SilverStored(combinedFault)==20000 && StudyStored(combinedFault)==400,
            "production reload retries the combined silver/study bag after rollback");
        var extraBag=SilverBagPlayer(town);var extraSrc=Pack(extraBag,3005117,1);uint extraUid=ConquerItem.ItemUID.Next;
        Query("INSERT INTO items(uid,id,entityid,stacksize) VALUES(@uid,@id,@owner,1)","uid",(long)extraUid,"id",1000000,"owner",(long)extraBag.Entity.UID);OpenSilverBag(extraBag,extraSrc);
        Check(Fenced(extraBag) && SilverBagUses(extraBag)==0 && extraBag.Inventory.ContainsUID(extraSrc.UID) && extraBag.Entity.Money==0,
            "an extra durable bag UID cannot pay a silver bag");
        Query("DELETE FROM items WHERE uid=@uid","uid",(long)extraUid);extraBag=ReloadBall(extraBag);OpenSilverBag(extraBag,extraBag.Inventory.Objects.Single(i=>i.ID==3005117));
        Check(!Fenced(extraBag) && SilverBagUses(extraBag)==1 && extraBag.Entity.Money==5000UL,"production reload retries after the extra durable bag UID is removed");
        var missingBag=SilverBagPlayer(town);var missingSrc=Pack(missingBag,3005117,1);var cacheOnly=Pack(missingBag,1000000,1);
        Query("DELETE FROM items WHERE uid=@uid","uid",(long)cacheOnly.UID);OpenSilverBag(missingBag,missingSrc);
        Check(Fenced(missingBag) && SilverBagUses(missingBag)==0 && missingBag.Inventory.ContainsUID(missingSrc.UID),
            "a cache-only bag UID cannot pay a silver bag");
        missingBag=ReloadBall(missingBag);OpenSilverBag(missingBag,missingBag.Inventory.Objects.Single(i=>i.ID==3005117));
        Check(!Fenced(missingBag) && SilverBagUses(missingBag)==1,"production reload retries after the cache-only bag UID is dropped");
        foreach(string guard in new[]{"dead","fake","trade","booth","locked","suspicious","wardrobe","expired","substituted"}){
            var p=SilverBagPlayer(town);var item=Pack(p,3005117,1);ulong money=p.Entity.Money;
            if(guard=="dead")p.Entity.Hitpoints=0;if(guard=="fake")p.Fake=true;if(guard=="trade")p.Trade.InTrade=true;
            if(guard=="booth")p.Booth=(Revora.Game.ConquerStructures.Booth)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Revora.Game.ConquerStructures.Booth));if(guard=="locked")item.Lock=1;
            if(guard=="suspicious")item.Suspicious=true;if(guard=="wardrobe")item.InWardrobe=true;if(guard=="expired"){item.Days=1;item.DayStamp=silverBagNow.ToLocalTime().AddDays(-2);}
            if(guard=="substituted")ItemHandler.UseItem(new ConquerItem(true){ID=item.ID,UID=item.UID,StackSize=1},p);else OpenSilverBag(p,item);
            Check(SilverBagUses(p)==0 && p.Entity.Money==money && p.Inventory.ContainsUID(item.UID),"ineligible silver bag leaves source unchanged: "+guard);
        }
        var frames=SilverBagPlayer(town);var wire=Pack(frames,3005117,1);
        foreach(string bad in new[]{"short","long","length","type","seal"}){
            byte[] frame=bad=="short"?new byte[31]:bad=="long"?new byte[129]:PrayerPacket(wire.UID);
            Writer.WriteUInt16((ushort)(frame.Length-8),0,frame);Writer.WriteUInt16(1009,2,frame);Writer.WriteUInt32(wire.UID,8,frame);Writer.WriteUInt16(4,20,frame);Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);
            if(bad=="length")Writer.WriteUInt16(1,0,frame);if(bad=="type")Writer.WriteUInt16(1008,2,frame);if(bad=="seal")frame[frame.Length-1]^=1;
            ItemUsage.Process(frame,frames);Check(!Fenced(frames) && SilverBagUses(frames)==0 && frames.Inventory.ContainsUID(wire.UID),"bounded silver bag rejects "+bad+" frame");
        }OpenSilverBag(frames,wire);Check(!Fenced(frames) && SilverBagUses(frames)==1 && frames.Entity.Money==5000UL,"ordinary silver bag works after malformed frames");
        var replay=SilverBagPlayer(town);var once=Pack(replay,3005117,1);uint uid=once.UID;OpenSilverBag(replay,once);
        ItemHandler.UseItem(once,replay);Check(SilverBagUses(replay)==1 && !replay.Inventory.ContainsUID(uid),"consumed silver bag UID cannot replay");
        var direct=SilverBagPlayer(town);var directPack=Pack(direct,3005125,1);ItemHandler.UseItem(directPack,direct);
        Check(!Fenced(direct) && SilverBagUses(direct)==1 && direct.Entity.SubClasses.StudyPoints==500,"direct UseItem opens 3005125");
        var leftover=SilverBagPlayer(town);var surprise=Pack(leftover,722178,1);
        Check(!(bool)Call(SilverBags,null,"TryUse",surprise,leftover) && leftover.Inventory.ContainsUID(surprise.UID),"Surprise box 722178 stays outside SilverStudyBags");
        SilverBags.GetField("Spoken",Hidden).SetValue(null,null);
    }
}
