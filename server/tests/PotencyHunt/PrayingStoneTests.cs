using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network;
using Revora.Network.GamePackets;
using People = Revora.Database.KnownPersons;

internal static partial class PotencyHuntTests
{
    static readonly Type Prayers=typeof(Server).Assembly.GetType("Revora.Game.Features.PrayingStones");
    static DateTime prayerNow;
    static byte[] PrayerPacket(uint uid)
    {
        var q=new ItemUsage(true){UID=uid,Action=ItemUsage.Mode.EquipItem,TimeStamp=0x12345678};var frame=q.ToArray();
        Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);return frame;
    }
    static void Pray(GameState p,ConquerItem item) { ItemUsage.Process(PrayerPacket(item.UID),p); }
    static void VerifyPrayingStones(Map map)
    {
        Query("DROP TABLE entities;CREATE TABLE entities AS TABLE public.entities WITH NO DATA;CREATE TABLE titles (LIKE public.titles INCLUDING ALL);CREATE TABLE apprentice (LIKE public.apprentice INCLUDING ALL);CREATE TABLE itemadding (LIKE public.itemadding INCLUDING ALL)");
        StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);Call(EvidenceType,null,"Initialize");Call(Admissions,null,"Initialize");Call(Prayers,null,"Initialize");ConquerItem.ItemUID=new Counter(4960000);
        prayerNow=new DateTime(2026,9,15,12,0,0,DateTimeKind.Utc);Prayers.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>prayerNow));
        uint[] ids={1200000,1200001,1200002};uint[] durations={259200,604800,2592000};long[] hours={7,16,72};
        for(int n=0;n<3;n++)for(int bound=0;bound<2;bound++)
        {
            var mentor=DeparturePlayer(map,130);var child=DeparturePlayer(map,100);MentorLink(mentor,child,0);var item=Pack(child,ids[n],1);
            if(bound==1){item.Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)item.UID);}
            Pray(child,item);
            Check(!Fenced(child) && child.Account.State==AccountTable.AccountState.Player && child.Entity.HeavenBlessing==durations[n]
                && BlessingSeconds(child)==durations[n] && !child.Inventory.ContainsUID(item.UID) && Scalar("SELECT count(*) FROM items WHERE uid=@uid",item.UID)==0,
                "ordinary native Praying Stone use consumes only its saved item and grants the full duration: "+ids[n]+" bound="+bound);
            Check(BlessingHours(child)==hours[n] && mentor.PrizeHeavenBlessing==hours[n] && child.AsApprentice.Actual_HeavenBlessing==hours[n]
                && Scalar("SELECT mentor_hours FROM revora_praying_stone_uses WHERE uid=@uid",child.Entity.UID)==hours[n],"stone grant and exact Revora mentor hours commit and refresh together");
            Pray(child,item);ItemHandler.UseItem(item,child);EntityTable.SaveEntity(child);People.LoadMentor(child);People.LoadMentor(mentor);
            Check(BlessingSeconds(child)==durations[n] && BlessingHours(child)==hours[n]
                && Scalar("SELECT count(*) FROM revora_praying_stone_uses WHERE uid=@uid",child.Entity.UID)==1,"consumed item replay and production save/reload cannot repeat delivery");
        }
        var stacked=DeparturePlayer(map,130);var stack=Pack(stacked,1200000,3);var untouched=Pack(stacked,1200000,1);
        Task.WaitAll(Enumerable.Range(0,8).Select(i=>Task.Run(()=>Pray(stacked,stack))).ToArray());
        Check(!Fenced(stacked) && stacked.Entity.HeavenBlessing==259200 && stack.StackSize==2 && SavedRune(stack,"stacksize")==2 && untouched.StackSize==1,
            "parallel use of the selected stack is debounced and leaves another same-type item untouched");
        prayerNow=prayerNow.AddSeconds(1);Pray(stacked,stack);
        Check(stacked.Entity.HeavenBlessing==518400 && stack.StackSize==1 && SavedRune(stack,"stacksize")==1,"a later intentional use consumes exactly one more stacked stone");
        var capped=DeparturePlayer(map,130);capped.Entity.HeavenBlessing=uint.MaxValue-100;var fullStone=Pack(capped,1200002,1);Pray(capped,fullStone);
        Check(!Fenced(capped) && capped.Inventory.ContainsUID(fullStone.UID) && capped.Entity.HeavenBlessing==uint.MaxValue-100,"a full blessing duration preserves the entire stone without partial payment");
        var mentorOffline=DeparturePlayer(map,130);var childOnline=DeparturePlayer(map,100);MentorLink(mentorOffline,childOnline,0);Kernel.GamePool.Remove(mentorOffline.Entity.UID);Pray(childOnline,Pack(childOnline,1200001,1));
        Kernel.GamePool[mentorOffline.Entity.UID]=mentorOffline;People.LoadMentor(mentorOffline);ClaimBlessing(mentorOffline);
        Check(!Fenced(childOnline) && !Fenced(mentorOffline) && mentorOffline.Entity.HeavenBlessing==57600 && BlessingHours(childOnline)==0,
            "an offline mentor can reload and claim the saved Praying Stone contribution");
        var generic=DeparturePlayer(map,130);var recipient=DeparturePlayer(map,100);MentorLink(generic,recipient,0);recipient.AddBless(259200);
        Check(BlessingSeconds(recipient)==259200 && BlessingHours(recipient)==0,"generic quest/event blessing grants no longer create mentor contribution");
        var rapidParent=DeparturePlayer(map,130);var rapidChild=DeparturePlayer(map,100);MentorLink(rapidParent,rapidChild,0);
        var one=Pack(rapidChild,1200000,1);var two=Pack(rapidChild,1200000,1);Pray(rapidChild,one);Pray(rapidChild,two);
        Check(!Fenced(rapidChild) && BlessingHours(rapidChild)==14 && rapidParent.PrizeHeavenBlessing==14 && BlessingSeconds(rapidChild)==518400,
            "two distinct stones used within five seconds retain both source contributions");
        var racingParent=DeparturePlayer(map,130);var racingChild=DeparturePlayer(map,100);MentorLink(racingParent,racingChild,0);var racingStone=Pack(racingChild,1200000,1);
        Task.WaitAll(Task.Run(()=>Pray(racingChild,racingStone)),Task.Run(()=>ClaimBlessing(racingParent)));
        Check(!Fenced(racingChild) && !Fenced(racingParent) && BlessingSeconds(racingChild)==259200
            && BlessingHours(racingChild)+racingParent.Entity.HeavenBlessing/3600==7,"concurrent source credit and mentor claim retain every earned hour in either ordering");
        var wideParent=DeparturePlayer(map,130);var wideChild=DeparturePlayer(map,100);MentorLink(wideParent,wideChild,0);
        Query("UPDATE apprentice SET total_heavenblessing=200000 WHERE apprenticeid=@uid","uid",(long)wideChild.Entity.UID);People.LoadMentor(wideChild);Pray(wideChild,Pack(wideChild,1200000,1));
        Check(!Fenced(wideChild) && wideChild.AsApprentice.Total_HeavenBlessing==65535 && BlessingHours(wideChild)==7
            && Scalar("SELECT total_heavenblessing FROM apprentice WHERE apprenticeid=@uid",wideChild.Entity.UID)==200007,
            "source grants preserve wide durable lifetime history while bounding native displays");
        VerifyPrayerGuards(map);VerifyPrayerFailures(map);VerifyPrayerLegacyMedium(map);
    }
    static void VerifyPrayerLegacyMedium(Map map)
    {
        foreach(string table in new[]{"teamarena","championarena","quests","status","reward","rewardclameid","merchant","dialyquests","activeness","house","pkexploits","achievement"})
            Query("CREATE TABLE IF NOT EXISTS "+table+" (LIKE public."+table+" INCLUDING ALL)");
        long before=EpkCount("SELECT count(*) FROM revora_praying_stone_uses");
        Query(@"ALTER TABLE revora_praying_stone_uses DROP CONSTRAINT revora_praying_stone_uses_item_type_check,
DROP CONSTRAINT revora_praying_stone_uses_stack_before_check,DROP CONSTRAINT revora_praying_stone_uses_check,DROP CONSTRAINT revora_praying_stone_uses_check1,
ADD CONSTRAINT revora_praying_stone_uses_item_type_check CHECK(item_type BETWEEN 1200000 AND 1200002),
ADD CONSTRAINT revora_praying_stone_uses_stack_before_check CHECK(stack_before BETWEEN 1 AND 65535),
ADD CONSTRAINT revora_praying_stone_uses_check CHECK(seconds_after=seconds_before+CASE item_type WHEN 1200000 THEN 259200 WHEN 1200001 THEN 604800 ELSE 2592000 END),
ADD CONSTRAINT revora_praying_stone_uses_check1 CHECK(mentor_hours=CASE WHEN mentor=0 THEN 0 WHEN item_type=1200000 THEN 7 WHEN item_type=1200001 THEN 16 ELSE 72 END)");
        Call(Prayers,null,"Initialize");Call(Prayers,null,"Initialize");
        Check(before>0 && EpkCount("SELECT count(*) FROM revora_praying_stone_uses")==before,"legacy receipt migration preserves every existing use across repeated initialization");
        foreach(bool bound in new[]{false,true})foreach(ushort size in new ushort[]{0,1,2})
        {
            var mentor=DeparturePlayer(map,130);var p=DeparturePlayer(map,100);MentorLink(mentor,p,0);var item=Pack(p,720578,size);
            item.Bound=bound;Query("UPDATE items SET bound=@bound WHERE uid=@uid","bound",bound?1:0,"uid",(long)item.UID);
            Parallel.For(0,16,n=>{if(n%3==0)EntityTable.SaveEntity(p);else Pray(p,item);});
            Check(!Fenced(p) && p.Entity.HeavenBlessing==604800 && BlessingSeconds(p)==604800 && BlessingHours(p)==16
                && Scalar("SELECT count(*) FROM revora_praying_stone_uses WHERE uid=@uid",p.Entity.UID)==1
                && (size>1?item.StackSize==1:!p.Inventory.ContainsUID(item.UID)),"legacy medium stone grants exactly seven days and sixteen Revora mentor hours: "+bound+"/"+size);
            Check(Scalar("SELECT item_type FROM revora_praying_stone_uses WHERE uid=@uid",p.Entity.UID)==720578
                && Scalar("SELECT stack_before FROM revora_praying_stone_uses WHERE uid=@uid",p.Entity.UID)==size,"receipt preserves the actual legacy identity and exact source stack");
            Check(EntityTable.SaveEntity(p),"legacy medium blessing can be checkpointed by production autosave");
            p=ReloadGold(p);ConquerItemTable.LoadItems(p);People.LoadMentor(p);ItemHandler.UseItem(item,p);
            Check(!Fenced(p) && p.Entity.HeavenBlessing==604800 && BlessingHours(p)==16
                && Scalar("SELECT count(*) FROM revora_praying_stone_uses WHERE uid=@uid",p.Entity.UID)==1,"production reload preserves medium blessing and rejects an already consumed or stale selected object");
            if(size>1){item=p.Inventory.Objects.Single();prayerNow=prayerNow.AddSeconds(1);Pray(p,item);
                Check(!Fenced(p) && p.Entity.HeavenBlessing==1209600 && BlessingHours(p)==32 && p.Inventory.Count==0,"a reloaded remaining medium stone grants one additional full week");}
        }
        foreach(string target in new[]{"source","stack","blessing","credit","receipt"})
        {
            var mentor=DeparturePlayer(map,130);var p=DeparturePlayer(map,100);MentorLink(mentor,p,0);var item=Pack(p,720578,(ushort)(target=="stack"?2:0));
            string table=target=="blessing"?"entities":target=="credit"?"apprentice":target=="receipt"?"revora_praying_stone_uses":"items";
            string op=target=="source"?"DELETE":target=="receipt"?"INSERT":"UPDATE";
            Query("CREATE FUNCTION suppress_medium() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_medium BEFORE "+op+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_medium()");Pray(p,item);
            Check(Fenced(p) && p.Entity.HeavenBlessing==0 && BlessingSeconds(p)==0 && BlessingHours(p)==0 && p.Inventory.ContainsUID(item.UID)
                && SavedRune(item,"stacksize")==item.StackSize && Scalar("SELECT count(*) FROM revora_praying_stone_uses WHERE uid=@uid",p.Entity.UID)==0,"medium source, blessing and mentor receipt roll back on suppressed "+target);
            Query("DROP TRIGGER suppress_medium ON "+table+";DROP FUNCTION suppress_medium()");p=ReloadGold(p);ConquerItemTable.LoadItems(p);People.LoadMentor(p);Pray(p,p.Inventory.Objects.Single());
            Check(!Fenced(p) && BlessingSeconds(p)==604800 && BlessingHours(p)==16,"production reload retries the full medium grant after "+target+" rollback");
        }
        var bad=DeparturePlayer(map,130);var stone=Pack(bad,720578,1);
        foreach(string mode in new[]{"short","oversize","length","family","seal","action-high"}){
            byte[] frame=PrayerPacket(stone.UID);if(mode=="short")Array.Resize(ref frame,31);if(mode=="oversize")Array.Resize(ref frame,129);
            if(mode=="length")frame[0]--;if(mode=="family")frame[2]++;if(mode=="seal")frame[frame.Length-1]^=1;if(mode=="action-high")frame[21]=1;
            ItemUsage.Process(frame,bad);Check(!Fenced(bad) && bad.Inventory.ContainsUID(stone.UID) && BlessingSeconds(bad)==0,"legacy medium handler rejects malformed "+mode+" use");
        }
        Pray(bad,stone);Check(!Fenced(bad) && BlessingSeconds(bad)==604800,"valid medium use remains available after malformed requests");
        foreach(string column in new[]{"seconds_after","mentor_hours","item_type"}){
            bool rejected=false;try{Query("UPDATE revora_praying_stone_uses SET "+column+"=1 WHERE uid=@uid","uid",(long)bad.Entity.UID);}catch(Npgsql.PostgresException ex){rejected=ex.SqlState=="23514";}
            Check(rejected,"migrated receipt constraints reject incorrect "+column);
        }
        Call(Prayers,null,"Initialize");Check(BlessingSeconds(bad)==604800,"repeat initialization preserves valid legacy-medium records");
    }
    static void VerifyPrayerGuards(Map map)
    {
        var p=DeparturePlayer(map,130);var item=Pack(p,1200000,1);
        foreach(string mode in new[]{"short","length","family","seal","action-high","oversize"})
        {
            var frame=PrayerPacket(item.UID);
            if(mode=="short")Array.Resize(ref frame,22);else if(mode=="length")frame[0]--;else if(mode=="family")frame[2]++;
            else if(mode=="seal")frame[frame.Length-1]^=1;else if(mode=="action-high")frame[21]=1;else Array.Resize(ref frame,129);
            ItemUsage.Process(frame,p);
            Check(!Fenced(p) && p.Inventory.ContainsUID(item.UID) && p.Entity.HeavenBlessing==0,"malformed Praying Stone use cannot consume a stone: "+mode);
        }
        var other=DeparturePlayer(map,130);ItemUsage.Process(PrayerPacket(item.UID),other);ItemHandler.UseItem(item,other);
        Check(p.Inventory.ContainsUID(item.UID) && other.Entity.HeavenBlessing==0,"another player's selected UID or object cannot authorize stone use");
        item.Lock=1;Pray(p,item);item.Lock=0;item.Suspicious=true;Pray(p,item);item.Suspicious=false;item.Days=1;item.DayStamp=prayerNow.ToLocalTime().AddDays(-2);Pray(p,item);
        Check(!Fenced(p) && p.Inventory.ContainsUID(item.UID) && p.Entity.HeavenBlessing==0,"locked, suspicious and expired cached stones are not consumed");
        var stale=DeparturePlayer(map,130);var staleItem=Pack(stale,1200000,1);Query("UPDATE items SET stacksize=2 WHERE uid=@uid","uid",(long)staleItem.UID);Pray(stale,staleItem);
        Check(Fenced(stale) && BlessingSeconds(stale)==0 && SavedRune(staleItem,"stacksize")==2,"saved stack mismatch preserves the item and requires reload");
        var savedLock=DeparturePlayer(map,130);var locked=Pack(savedLock,1200000,1);Query("UPDATE items SET locked=1 WHERE uid=@uid","uid",(long)locked.UID);Pray(savedLock,locked);
        Check(Fenced(savedLock) && BlessingSeconds(savedLock)==0 && SavedRune(locked,"locked")==1,"a stale unlocked cache cannot consume a saved locked stone");
        var duplicate=DeparturePlayer(map,130);var dupChild=DeparturePlayer(map,100);MentorLink(duplicate,dupChild,0);Query("INSERT INTO apprentice SELECT * FROM apprentice WHERE apprenticeid=@uid","uid",(long)dupChild.Entity.UID);var dupStone=Pack(dupChild,1200000,1);Pray(dupChild,dupStone);
        Check(Fenced(dupChild) && dupChild.Inventory.ContainsUID(dupStone.UID) && BlessingSeconds(dupChild)==0 && AdmissionRows(dupChild)==2,
            "ambiguous historical membership cannot consume the apprentice's Praying Stone");
    }
    static void VerifyPrayerFailures(Map map)
    {
        foreach(string mode in new[]{"item","credit","wallet","receipt","silent-item","silent-receipt"})
        {
            var mentor=DeparturePlayer(map,130);var child=DeparturePlayer(map,100);MentorLink(mentor,child,0);var item=Pack(child,1200000,1);
            string table=mode.EndsWith("item")?"items":mode=="credit"?"apprentice":mode=="wallet"?"entities":"revora_praying_stone_uses";
            string op=table=="items"?"DELETE":table=="apprentice"?"UPDATE OF actual_heavenblessing":table=="entities"?"UPDATE OF heavenblessingtime":"INSERT";
            string effect=mode.StartsWith("silent")?"RETURN NULL;":"RAISE EXCEPTION ''fixture prayer failure'';";
            Query("CREATE FUNCTION fail_prayer() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN "+effect+" END';CREATE TRIGGER fail_prayer BEFORE "+op+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION fail_prayer()");Pray(child,item);
            Check(Fenced(child) && child.Inventory.ContainsUID(item.UID) && SavedRune(item,"stacksize")==1 && BlessingSeconds(child)==0 && BlessingHours(child)==0
                && mentor.PrizeHeavenBlessing==0 && Scalar("SELECT count(*) FROM revora_praying_stone_uses WHERE uid=@uid",child.Entity.UID)==0,
                "selected item, duration, mentor credit and receipt roll back together: "+mode);
            Query("DROP TRIGGER fail_prayer ON "+table+";DROP FUNCTION fail_prayer()");DemonReset(child);People.LoadMentor(child);LoadBlessingImage(child);Pray(child,item);
            Check(!Fenced(child) && !child.Inventory.ContainsUID(item.UID) && BlessingSeconds(child)==259200 && BlessingHours(child)==7,"Praying Stone use recovers after reload: "+mode);
        }
    }
}
