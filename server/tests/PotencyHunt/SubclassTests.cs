using System;
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
    static readonly Type SubProgress=typeof(Entity).Assembly.GetType("Revora.Game.Features.SubclassProgression");
    static byte[] SubRequest(ushort action,byte id) {
        var packet=new byte[38];Writer.WriteUInt16(30,0,packet);Writer.WriteUInt16(2320,2,packet);Writer.WriteUInt16(action,8,packet);packet[10]=id;return packet;
    }
    static void SubRequest(GameState p,ushort action,byte id){SendSubClass.Process(SubRequest(action,id),p);}
    static void Study(GameState p,ushort amount){Query("UPDATE entities SET studypoints=@amount WHERE uid=@uid","uid",(long)p.Entity.UID,"amount",(int)amount);p.Entity.SubClasses.StudyPoints=amount;}
    static void VerifySubclasses(Map map) {
        Query("ALTER TABLE entities ADD COLUMN level integer DEFAULT 140,ADD COLUMN reborn integer DEFAULT 0,ADD COLUMN studypoints integer DEFAULT 0,ADD COLUMN subclass integer DEFAULT 0,ADD COLUMN my_title integer DEFAULT 0");
        Query("CREATE TABLE titles (LIKE public.titles INCLUDING ALL);CREATE TABLE subclasses (LIKE public.subclasses INCLUDING ALL)");
        StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);ConquerItem.ItemUID=new Counter(7600000);
        var p=MoonPlayer(map);p.Entity.FullyLoaded=false;p.Entity.Vitality=100;p.Entity.Strength=100;p.Entity.FullyLoaded=true;p.Entity.Reborn=2;
        Check(p.Account.State==AccountTable.AccountState.Player,"subclass enrollment uses ordinary non-GM ownership");
        VerifySubclassSuppressedWrites(map);
        VerifySubclassMaterialOwnership(map);
        for(int size=0;size<38;size++)SendSubClass.Process(SubRequest(9,1).Take(size).ToArray(),p);
        foreach(ushort action in new ushort[]{256,258,265,266,65535})SubRequest(p,action,1);
        foreach(byte id in new byte[]{0,7,8,10,255})SubRequest(p,9,id);
        Check(p.Entity.SubClasses.Classes.Count==0,"truncated, aliased-action and unsupported-class requests cannot enroll");
        var rows=new[]{new[]{1,721259,5,10922,10923},new[]{2,721261,10,11203,11204},new[]{3,711188,1,10924,10925},new[]{4,723087,20,10926,10927},
            new[]{5,1088001,10,10920,10921},new[]{6,711679,1,10928,10929},new[]{9,723903,40,11201,11202}};
        foreach(var row in rows) {
            byte id=(byte)row[0];var material=Pack(p,(uint)row[1],(ushort)(row[2]+1));
            if(id==1) {
                p.Entity.Level=69;SubRequest(p,9,id);Check(p.Inventory.ContainsUID(material.UID) && material.StackSize==6 && p.Entity.SubClasses.Classes.Count==0,"level 69 cannot consume subclass enrollment materials");p.Entity.Level=140;
                Query("ALTER TABLE revora_achievement_evidence ADD CONSTRAINT subclass_join_failure CHECK(achievement_id<>10922) NOT VALID");SubRequest(p,9,id);
                Check(Fenced(p) && material.StackSize==6 && Scalar("SELECT count(*) FROM subclasses WHERE id=@uid",p.Entity.UID)==0,"failed enrollment achievement rolls back material and subclass together");
                Query("ALTER TABLE revora_achievement_evidence DROP CONSTRAINT subclass_join_failure");DemonReset(p);
            }
            Parallel.For(0,8,i=>SubRequest(p,9,id));
            Check(p.Entity.SubClasses.Classes[id].Phase==1 && p.Entity.SubClasses.Classes[id].Level==1 && material.StackSize==1 && HasAchievement(p,row[3]),"enroll once with exact material consumption and P1 evidence: "+id);
            Check(Scalar("SELECT count(*) FROM subclasses WHERE id=@uid",p.Entity.UID)==p.Entity.SubClasses.Classes.Count,"one durable enrollment row after concurrent requests: "+id);
            Study(p,60000);int spent=0;
            for(byte level=1;level<9;level++){ushort cost=(ushort)Call(SubProgress,null,"Cost",id,level);spent+=cost;SubRequest(p,2,id);}
            Check(p.Entity.SubClasses.Classes[id].Level==9 && p.Entity.SubClasses.Classes[id].Phase==1 && p.Entity.SubClasses.StudyPoints==60000-spent && !HasAchievement(p,row[4]),"eight paid levels preserve independent promotion phase: "+id+" level="+p.Entity.SubClasses.Classes[id].Level+" phase="+p.Entity.SubClasses.Classes[id].Phase+" balance="+p.Entity.SubClasses.StudyPoints+" expected="+(60000-spent));
            ushort balance=p.Entity.SubClasses.StudyPoints;Parallel.For(0,8,i=>SubRequest(p,2,id));
            Check(p.Entity.SubClasses.Classes[id].Level==9 && p.Entity.SubClasses.StudyPoints==balance,"maximum-level replay cannot charge or advance past nine: "+id);
            for(byte phase=2;phase<=9;phase++)SubRequest(p,10,id);
            Check(p.Entity.SubClasses.Classes[id].Phase==9 && HasAchievement(p,row[4]) && Scalar("SELECT min(phase) FROM subclasses WHERE id=@uid",p.Entity.UID)==9,"qualified promotion through P9 persists with the matching achievement: "+id);
        }
        Check(Enumerable.Range(1415,9).All(id=>p.Spells.ContainsKey((ushort)id)),"paid Performer enrollment preserves its existing dance entitlement");
        p.Spells.Remove(1415);GeneralData.SubclassSendInfo(p);Check(p.Spells.ContainsKey(1415),"saved Performer membership restores a missing dance after reconnect projection");
        p.Entity.SubClasses.Classes.Clear();SubClassTable.Load(p.Entity);Check(p.Entity.SubClasses.Classes.Count==7 && p.Entity.SubClasses.Classes.Values.All(s=>s.Phase==9 && s.Level==9),"production loader restores all seven completed subclasses");
        p.Entity.MyAchievement=new Achievement(p.Entity);p.Entity.MyAchievement.Load("");EvidenceRefresh(p);
        Check(rows.All(row=>HasAchievement(p,row[3]) && HasAchievement(p,row[4])),"all fourteen subclass achievements survive evidence reload");
        var limited=MoonPlayer(map);limited.Entity.FullyLoaded=false;limited.Entity.Vitality=100;limited.Entity.Strength=100;limited.Entity.FullyLoaded=true;Pack(limited,721259,5);SubRequest(limited,9,1);Study(limited,60000);
        for(int i=0;i<8;i++)SubRequest(limited,2,1);
        limited.Entity.Level=89;SubRequest(limited,10,1);Check(limited.Entity.SubClasses.Classes[1].Phase==1,"P2 requires level ninety");
        limited.Entity.Level=140;for(int i=0;i<4;i++)SubRequest(limited,10,1);Check(limited.Entity.SubClasses.Classes[1].Phase==4,"non-reborn characters stop at P4");
        limited.Entity.Reborn=1;for(int i=0;i<4;i++)SubRequest(limited,10,1);Check(limited.Entity.SubClasses.Classes[1].Phase==7,"first-rebirth characters stop at P7");
        limited.Entity.Reborn=2;limited.Entity.Level=129;SubRequest(limited,10,1);SubRequest(limited,10,1);Check(limited.Entity.SubClasses.Classes[1].Phase==8,"P9 requires level 130 as well as second rebirth");
        limited.Entity.Level=130;Query("ALTER TABLE revora_achievement_evidence ADD CONSTRAINT subclass_final_failure CHECK(achievement_id<>10923) NOT VALID");SubRequest(limited,10,1);
        Check(Fenced(limited) && limited.Entity.SubClasses.Classes[1].Phase==8 && !HasAchievement(limited,10923),"failed final promotion leaves phase and achievement unchanged");
        Query("ALTER TABLE revora_achievement_evidence DROP CONSTRAINT subclass_final_failure");DemonReset(limited);SubRequest(limited,10,1);Check(HasAchievement(limited,10923),"P9 retries safely after rollback");
        var payer=MoonPlayer(map);payer.Entity.FullyLoaded=false;payer.Entity.Vitality=100;payer.Entity.Strength=100;payer.Entity.FullyLoaded=true;
        Pack(payer,721259,5);SubRequest(payer,9,1);Study(payer,299);SubRequest(payer,2,1);
        Check(payer.Entity.SubClasses.Classes[1].Level==1 && payer.Entity.SubClasses.StudyPoints==299,"insufficient study points preserve both balance and training level");
        Study(payer,300);Query("ALTER TABLE subclasses ADD CONSTRAINT subclass_level_failure CHECK(level<>2) NOT VALID");SubRequest(payer,2,1);
        Check(Fenced(payer) && payer.Entity.SubClasses.StudyPoints==300 && Scalar("SELECT studypoints FROM entities WHERE uid=@uid",payer.Entity.UID)==300,"failed level update rolls back its study-point payment");
        Query("ALTER TABLE subclasses DROP CONSTRAINT subclass_level_failure");DemonReset(payer);
        Parallel.For(0,12,i=>SubRequest(payer,2,1));
        Check(payer.Entity.SubClasses.Classes[1].Level==2 && payer.Entity.SubClasses.StudyPoints==0 && Scalar("SELECT studypoints FROM entities WHERE uid=@uid",payer.Entity.UID)==0,"one affordable concurrent progression step costs exactly its saved price");
        Study(payer,900);Query("UPDATE entities SET studypoints=899 WHERE uid=@uid","uid",(long)payer.Entity.UID);SubRequest(payer,2,1);
        Check(!Fenced(payer) && payer.Entity.SubClasses.StudyPoints==900 && payer.Entity.SubClasses.Classes[1].Level==2,"unsettled legacy study balance waits without charge or discarding unsaved points");
        var first=Pack(payer,721261,4);var second=Pack(payer,721261,7);second.Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)second.UID);SubRequest(payer,9,2);
        Check(!payer.Inventory.ContainsUID(first.UID) && second.StackSize==1 && payer.Entity.SubClasses.Classes.ContainsKey(2),"enrollment consumes exact units across ordinary and bound stacks");
        var foreign=Pack(payer,711188,1);Query("UPDATE items SET entityid=@owner WHERE uid=@uid","owner",(long)p.Entity.UID,"uid",(long)foreign.UID);SubRequest(payer,9,3);
        Check(Fenced(payer) && !payer.Entity.SubClasses.Classes.ContainsKey(3) && payer.Inventory.ContainsUID(foreign.UID),"foreign persisted material cannot fund subclass enrollment");
        DemonReset(payer);SubRequest(payer,0,7);Check(!Fenced(payer),"switching to an unknown subclass is rejected safely");
        SubRequest(payer,0,1);Check(payer.Entity.SubClasses.Active==1 && Scalar("SELECT subclass FROM entities WHERE uid=@uid",payer.Entity.UID)==1,"switching an owned subclass persists its active identity");
        payer.Entity.SubClasses.Classes.Clear();payer.LoadItemStats();int critical=payer.Entity.CriticalStrike;uint attack=payer.Entity.MinAttack,magic=payer.Entity.MagicAttack,hp=payer.Entity.MaxHitpoints;
        payer.Entity.SubClasses.Classes[1]=new SubClass{ID=1,Level=9,Phase=1};payer.LoadItemStats();Check(payer.Entity.CriticalStrike==critical+100,"level-nine training with P1 promotion grants only P1 combat power");
        payer.Entity.SubClasses.Classes[1].Phase=9;payer.LoadItemStats();Check(payer.Entity.CriticalStrike==critical+1500,"earned P9 grants the full Martial Artist contribution");
        payer.Entity.SubClasses.Classes.Clear();payer.Entity.SubClasses.Classes[6]=new SubClass{ID=6,Level=9,Phase=1};payer.LoadItemStats();
        Check(payer.Entity.MinAttack==attack+100 && payer.Entity.MagicAttack==magic+100,"Performer P1 attack survives final stat calculation at its real phase value");
        payer.Entity.SubClasses.Classes[6].Phase=9;payer.LoadItemStats();Check(payer.Entity.MinAttack==attack+1000 && payer.Entity.MagicAttack==magic+1000,"Performer P9 contributes exactly one thousand physical and magic attack");
        payer.Entity.SubClasses.Classes.Clear();payer.Entity.SubClasses.Classes[9]=new SubClass{ID=9,Level=9,Phase=1};payer.LoadItemStats();Check(payer.Entity.MaxHitpoints==hp+100,"Wrangler training alone cannot bypass its P1 health bonus");
        payer.Entity.SubClasses.Classes[9].Phase=9;payer.LoadItemStats();Check(payer.Entity.MaxHitpoints==hp+1200,"Wrangler P9 health follows the installed phase requirement");
    }
    static void VerifySubclassMaterialOwnership(Map map) {
        DateTime now=new DateTime(2032,1,1,12,0,0,DateTimeKind.Utc);
        SubProgress.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        foreach(string guard in new[]{"locked","suspicious","wardrobe","warehouse","equipped","expired"}) {
            var p=MoonPlayer(map);var item=Pack(p,723903,40);
            if(guard=="locked")item.Lock=1;if(guard=="suspicious")item.Suspicious=true;
            if(guard=="wardrobe")item.InWardrobe=true;if(guard=="warehouse")item.Warehouse=8;
            if(guard=="equipped")item.Position=4;if(guard=="expired"){item.Days=1;item.DayStamp=now.ToLocalTime().AddDays(-1);}
            SubRequest(p,9,9);
            Check(!Fenced(p) && !p.Entity.SubClasses.Classes.ContainsKey(9) && SavedRune(item,"stacksize")==40
                && !HasAchievement(p,11201),"subclass enrollment preserves unusable cached materials: "+guard);
        }
        foreach(string column in new[]{"locked","suspicious","inwardrobe","warehouse","position","days"}) {
            var p=MoonPlayer(map);var first=Pack(p,723903,20);var changed=Pack(p,723903,20);
            // Column names come only from the constant fixture list above.
            Query("UPDATE items SET "+column+"="+(column=="days"?"'1'":"1")+" WHERE uid=@uid","uid",(long)changed.UID);
            SubRequest(p,9,9);
            Check(Fenced(p) && SavedRune(first,"stacksize")==20 && SavedRune(changed,"stacksize")==20
                && !p.Entity.SubClasses.Classes.ContainsKey(9) && Scalar("SELECT count(*) FROM subclasses WHERE id=@uid",p.Entity.UID)==0,
                "stale durable material metadata rolls back the earlier stack too: "+column);
        }
        var timed=MoonPlayer(map);var token=Pack(timed,723903,40);token.Days=1;token.DayStamp=now.ToLocalTime().AddHours(-1);
        Query("UPDATE items SET days='1',daystamp=@stamp WHERE uid=@uid","stamp",token.DayStamp.ToBinary(),"uid",(long)token.UID);
        SubRequest(timed,9,9);
        Check(!Fenced(timed) && timed.Entity.SubClasses.Classes.ContainsKey(9) && !timed.Inventory.ContainsUID(token.UID),
            "unexpired timed enrollment materials retain supported use with exact saved expiry");
        var stale=MoonPlayer(map);var altered=Pack(stale,723903,40);altered.Days=1;altered.DayStamp=now.ToLocalTime().AddHours(-1);
        Query("UPDATE items SET days='1',daystamp=@stamp WHERE uid=@uid","stamp",altered.DayStamp.AddHours(-1).ToBinary(),"uid",(long)altered.UID);
        SubRequest(stale,9,9);
        Check(Fenced(stale) && SavedRune(altered,"stacksize")==40 && !stale.Entity.SubClasses.Classes.ContainsKey(9),
            "changed saved expiry cannot authorize cached timed materials");
        var mixed=MoonPlayer(map);var locked=Pack(mixed,723903,40);locked.Lock=1;var available=Pack(mixed,723903,40);
        SubRequest(mixed,9,9);
        Check(!Fenced(mixed) && mixed.Entity.SubClasses.Classes.ContainsKey(9) && mixed.Inventory.ContainsUID(locked.UID)
            && !mixed.Inventory.ContainsUID(available.UID),"eligible materials can pay while a locked same-type stack stays untouched");
        SubProgress.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow));
    }
    static void VerifySubclassSuppressedWrites(Map map) {
        foreach(string table in new[]{"subclasses","revora_achievement_evidence"}) {
            var p=MoonPlayer(map);var saddle=Pack(p,723903,40);
            Query("CREATE FUNCTION suppress_subclass() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_subclass BEFORE INSERT ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_subclass()");
            try{SubRequest(p,9,9);}finally{Query("DROP TRIGGER suppress_subclass ON "+table+";DROP FUNCTION suppress_subclass()");}
            Check(Fenced(p) && p.Inventory.ContainsUID(saddle.UID) && SavedRune(saddle,"stacksize")==40
                && !p.Entity.SubClasses.Classes.ContainsKey(9) && Scalar("SELECT count(*) FROM subclasses WHERE id=@uid",p.Entity.UID)==0
                && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11201",p.Entity.UID)==0,
                "suppressed "+table+" enrollment write preserves all forty saddles and membership");
            DemonReset(p);SubRequest(p,9,9);p.Entity.SubClasses.Classes.Clear();SubClassTable.Load(p.Entity);EvidenceRefresh(p);
            Check(!Fenced(p) && !p.Inventory.ContainsUID(saddle.UID) && p.Entity.SubClasses.Classes[9].Phase==1 && HasAchievement(p,11201),
                "ordinary Wrangler retry and production membership/evidence load succeed after "+table+" rollback");
        }
        var payer=MoonPlayer(map);Pack(payer,723903,40);SubRequest(payer,9,9);Study(payer,400);
        Query("CREATE FUNCTION suppress_subclass() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_subclass BEFORE UPDATE ON entities FOR EACH ROW EXECUTE FUNCTION suppress_subclass()");
        try{SubRequest(payer,2,9);}finally{Query("DROP TRIGGER suppress_subclass ON entities;DROP FUNCTION suppress_subclass()");}
        Check(Fenced(payer) && payer.Entity.SubClasses.Classes[9].Level==1 && payer.Entity.SubClasses.StudyPoints==400
            && Scalar("SELECT level FROM subclasses WHERE id=@uid AND uid=9",payer.Entity.UID)==1
            && Scalar("SELECT studypoints FROM entities WHERE uid=@uid",payer.Entity.UID)==400,
            "suppressed Study Point payment cannot grant free subclass training");
        DemonReset(payer);SubRequest(payer,2,9);
        Check(!Fenced(payer) && payer.Entity.SubClasses.Classes[9].Level==2 && payer.Entity.SubClasses.StudyPoints==0,
            "training retry charges exactly once after suppressed payment rollback");
        var prior=MoonPlayer(map);Pack(prior,723903,40);
        Query("INSERT INTO revora_achievement_evidence(uid,achievement_id,source,source_key,rule_version) VALUES(@uid,11201,'subclass-v1','phase-1',1)","uid",(long)prior.Entity.UID);
        SubRequest(prior,9,9);
        Check(!Fenced(prior) && prior.Entity.SubClasses.Classes.ContainsKey(9) && HasAchievement(prior,11201),
            "existing durable achievement evidence does not block a valid paid enrollment");
    }
}
