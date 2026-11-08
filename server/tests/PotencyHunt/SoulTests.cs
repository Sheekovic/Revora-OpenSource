using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Revora.Network;
using Console=System.Console;

internal static partial class PotencyHuntTests
{
    static void VerifyReleasedRune(GameState p) {
        VerifyNoDailyP7(p);
        long count=Scalar("SELECT count(*) FROM revora_soul_runs WHERE uid=@uid AND state<>'active' AND rune_reward",p.Entity.UID);
        Check(p.Inventory.Objects.Count(i=>i.ID==4030101 && i.Bound)==count
            && Scalar("SELECT count(*) FROM revora_soul_runs r JOIN items i ON i.uid=ANY(r.rewards) AND i.entityid=r.uid WHERE r.uid=@uid AND r.state<>'active' AND r.rune_reward AND i.id=4030101 AND i.bound=1",p.Entity.UID)==count,
            "Release Souls preserves each promised bound rune and exact reward receipt");
    }
    static readonly Type Souls=typeof(Entity).Assembly.GetType("Revora.Game.Features.ReleaseSoulsQuest");
    static DateTime soulNow;
    static GameState SoulPlayer(Map town) {
        var p=BallPlayer(town);p.Entity.MyAchievement=new Achievement(p.Entity);p.Entity.MyAchievement.Load("");p.Quests=new Quests(p);p.Quests.Load();p.Entity.ConquerPoints=20000;p.Entity.RevoraPoint=100;p.Entity.SubClasses.StudyPoints=10;
        if(!EntityTable.SaveEntity(p))throw new Exception("Soul fixture save failed.");return p;
    }
    static void SoulNpc(GameState p,uint id,byte option) {MoonNpc(p,id,option);}
    static long SoulCount(GameState p,string expression) {return Scalar("SELECT "+expression+" FROM revora_soul_runs WHERE uid=@uid ORDER BY day DESC LIMIT 1",p.Entity.UID);}
    static void SoulVisit(GameState p,uint npc) {SoulNpc(p,npc,0);soulNow=soulNow.AddSeconds(3);Call(Souls,null,"Tick");}
    static void SoulReady(GameState p) {SoulNpc(p,301,1);SoulVisit(p,65822);SoulVisit(p,65823);SoulVisit(p,65824);}
    static void VerifySouls(string runtime,Map town) {
        VerifyFirstRebirth(runtime,town);
        // Production initializes this through WayOfHeroesClaims. Earned EXP
        // Balls also credit Enlightenment; this isolated suite must load it.
        Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.EnlightenmentProgress"),null,"Initialize");
        Query("CREATE TABLE npcs (LIKE public.npcs INCLUDING ALL)");
        Query("INSERT INTO npcs SELECT * FROM public.npcs WHERE id IN (15140,79882) OR id BETWEEN 65822 AND 65840 ON CONFLICT DO NOTHING");
        var desert=World(runtime,1000);town.AddNpc(new NpcSpawn{UID=79882,Mesh=19936,MapID=1002,X=319,Y=307,Type=Enums.NpcType.Talker});
        soulNow=new DateTime(2026,9,14,20,0,0,DateTimeKind.Utc);Souls.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>soulNow));Call(Souls,null,"Initialize");Call(Souls,null,"PlaceNpcs");
        Check(desert.Npcs.ContainsKey(301) && !desert.Npcs.ContainsKey(15140) && Enumerable.Range(65822,19).All(id=>desert.Npcs.ContainsKey((uint)id)),"guardian uses native mission identity while all nineteen installed remains stay available");
        Check((string)Call(Souls,null,"Day",new DateTime(2026,9,15,6,59,59,DateTimeKind.Utc))=="20260914"
            && (string)Call(Souls,null,"Day",new DateTime(2026,9,15,7,0,0,DateTimeKind.Utc))=="20260915"
            && (string)Call(Souls,null,"Day",new DateTime(2026,12,15,7,59,59,DateTimeKind.Utc))=="20261214","daily scope uses Pacific midnight in summer and winter");
        var p=SoulPlayer(town);var native=new MsgTaskStatus(true,1){Action=MsgTaskStatus.QuestAction.Begin};native.Apend(MsgTaskStatus.QuestData.Create((QuestID)6049,MsgTaskStatus.QuestData.QuestStatus.Finished,0));
        MsgTaskStatus.Process(native.ToArray(),p);
        Check(!p.Quests.HasQuest((QuestID)6049) && Scalar("SELECT count(*) FROM revora_soul_runs WHERE uid=@uid",p.Entity.UID)==0,"native client Begin cannot accept or complete the authoritative soul quest");
        p.Entity.Level=69;SoulNpc(p,79882,3);Check(p.Entity.MapID==1002,"under-level Daily Envoy travel is rejected");
        p.Entity.Level=70;EntityTable.SaveEntity(p);SoulNpc(p,79882,3);Check(p.Entity.MapID==1000 && Kernel.GetDistance(p.Entity.X,p.Entity.Y,536,643)<=8,"ordinary Daily Envoy option reaches the native Desert Guardian");
        p.Entity.Level=69;SoulNpc(p,301,1);Check(Scalar("SELECT count(*) FROM revora_soul_runs WHERE uid=@uid",p.Entity.UID)==0,"under-level guardian acceptance is rejected");p.Entity.Level=70;
        p.Fake=true;SoulNpc(p,301,1);p.Fake=false;p.ActiveNpc=301;p.Entity.X=1;p.Entity.Y=1;Npcs.GetDialog(new NpcRequest(true){NpcID=301,OptionID=1},p);
        Check(Scalar("SELECT count(*) FROM revora_soul_runs WHERE uid=@uid",p.Entity.UID)==0,"fake and remote guardian requests cannot create progress");
        MoonNear(p,301);Parallel.For(0,12,i=>Npcs.GetDialog(new NpcRequest(true){NpcID=301,OptionID=1},p));
        Check(SoulCount(p,"cardinality(seen)")==0 && p.Quests.CheckQuest((QuestID)6049)==MsgTaskStatus.QuestData.QuestStatus.Accepted,"concurrent acceptance starts one daily run and native tracker entry");
        p.Entity.RelasesGuard=99;SoulNpc(p,301,3);Check(p.Inventory.Count==0 && p.Entity.ConquerPoints==20000 && !HasAchievement(p,11012),"legacy counter and direct reward option cannot bypass distinct souls");
        MoonNear(p,65822);Parallel.For(0,12,i=>Npcs.GetDialog(new NpcRequest(true){NpcID=65822,OptionID=0},p));soulNow=soulNow.AddSeconds(3).AddTicks(-1);Call(Souls,null,"Tick");
        Check(SoulCount(p,"cardinality(seen)")==0,"inspection cannot finish before its three-second boundary");soulNow=soulNow.AddTicks(1);Call(Souls,null,"Tick");
        Check(SoulCount(p,"cardinality(seen)")==1 && p.Quests.GetQuest((QuestID)6049).Kills==1,"one completed inspection saves one distinct soul and native objective count");
        SoulVisit(p,65822);Check(SoulCount(p,"cardinality(seen)")==1,"repeated inspection of the same soldier never adds another soul");
        SoulNpc(p,65823,0);p.Entity.X++;soulNow=soulNow.AddSeconds(3);Call(Souls,null,"Tick");Check(SoulCount(p,"cardinality(seen)")==1,"moving during inspection cancels progress");
        SoulNpc(p,65823,0);p.Entity.OnCheckGuard=false;soulNow=soulNow.AddSeconds(3);Call(Souls,null,"Tick");Check(SoulCount(p,"cardinality(seen)")==1,"existing movement cancellation flag stops the inspection");
        SoulNpc(p,65823,0);Query("ALTER TABLE revora_soul_runs ADD CONSTRAINT soul_visit_failure CHECK(cardinality(seen)<=1) NOT VALID");soulNow=soulNow.AddSeconds(3);Call(Souls,null,"Tick");
        Check(Fenced(p) && SoulCount(p,"cardinality(seen)")==1,"failed inspection write preserves prior progress and fences further actions");Query("ALTER TABLE revora_soul_runs DROP CONSTRAINT soul_visit_failure");DemonReset(p);
        p.Quests.Save();p.Quests=new Quests(p);p.Quests.Load();Call(Souls,null,"OnLogin",p);
        Check(p.Quests.GetQuest((QuestID)6049).Kills==1 && !p.Entity.OnCheckGuard,"ordinary quest reload restores saved objectives and cancels unfinished inspection");
        SoulVisit(p,65823);SoulVisit(p,65824);Check(SoulCount(p,"cardinality(seen)")==3 && p.Quests.GetQuest((QuestID)6049).Kills==3,"three distinct soldiers satisfy the installed 6711 objective");
        var other=SoulPlayer(town);SoulNpc(other,79882,3);SoulNpc(other,301,1);SoulVisit(other,65822);Check(SoulCount(other,"cardinality(seen)")==1,"another ordinary player can release the same world soldier independently");
        var full=Enumerable.Range(0,39).Select(i=>Pack(p,1088001,1)).ToArray();SoulNpc(p,301,3);Check(p.Inventory.Count==39 && p.Entity.ConquerPoints==20000,"full inventory preserves the soul reward and all currencies");
        foreach(var item in full){Query("DELETE FROM items WHERE uid=@id","id",(long)item.UID);p.Inventory.Remove(item.UID,Enums.ItemUse.None,false);}
        p.Entity.SubClasses.StudyPoints=ushort.MaxValue;SoulNpc(p,301,3);Check(p.Inventory.Count==0 && p.Entity.ConquerPoints==20000,"study-point overflow leaves the reward claimable");p.Entity.SubClasses.StudyPoints=10;
        Query("ALTER TABLE revora_soul_runs ADD CONSTRAINT soul_claim_failure CHECK(state='active') NOT VALID");SoulNpc(p,301,3);
        Check(Fenced(p) && p.Inventory.Count==0 && p.Entity.ConquerPoints==20000 && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==20000 && !HasAchievement(p,11012),"failed final transaction rolls back currency, items, study and achievement");
        Query("ALTER TABLE revora_soul_runs DROP CONSTRAINT soul_claim_failure");DemonReset(p);MoonNear(p,301);
        Parallel.For(0,12,i=>Npcs.GetDialog(new NpcRequest(true){NpcID=301,OptionID=3},p));
        Check(p.Entity.ConquerPoints==22500 && p.Entity.RevoraPoint==115 && p.Entity.SubClasses.StudyPoints==60 && p.Inventory.Count==3,"concurrent final claims deliver one configured purse, fifty study points and three bound gifts");
        Check(p.Inventory.Objects.Count(i=>i.Bound)==3 && p.Inventory.Objects.Any(i=>i.ID==722136) && p.Inventory.Objects.Any(i=>i.ID==729304) && HasAchievement(p,11012),"legitimate completion delivers usable reward identities and achievement 11012");
        Check(Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",p.Entity.UID)==115 && Scalar("SELECT studypoints FROM entities WHERE uid=@uid",p.Entity.UID)==60 && p.Quests.CheckQuest((QuestID)6049)==MsgTaskStatus.QuestData.QuestStatus.Finished,"native completion and committed account balances agree immediately");
        foreach(var item in p.Inventory.Objects)p.Inventory.Remove(item.UID,Enums.ItemUse.None,false);ConquerItemTable.LoadItems(p);p.Quests.Save();p.Quests=new Quests(p);p.Quests.Load();Call(Souls,null,"OnLogin",p);
        Check(p.Inventory.Count==3 && HasAchievement(p,11012) && p.Quests.CheckQuest((QuestID)6049)==MsgTaskStatus.QuestData.QuestStatus.Finished,"ordinary item and quest loading retain reward and terminal completion");
        VerifyReleasedRune(p);
        DialyQuestTable.ResetFromNpc(p);Call(Souls,null,"Refresh",p);SoulNpc(p,301,3);
        Check(p.Entity.RelasesTheSoul && p.Entity.ConquerPoints==22500 && p.Inventory.Count==3,"legacy daily reset cannot replay the new soul quest reward");
        var paid=SoulPlayer(town);SoulNpc(paid,79882,3);SoulNpc(paid,301,4);Check(paid.Entity.ConquerPoints==20000 && paid.Inventory.Count==0,"unconfirmed paid shortcut cannot charge the player");
        SoulNpc(paid,301,2);MoonNear(paid,301);Parallel.For(0,12,i=>Npcs.GetDialog(new NpcRequest(true){NpcID=301,OptionID=4},paid));
        Check(paid.Entity.ConquerPoints==17500 && paid.Inventory.Count==3 && !HasAchievement(paid,11012) && paid.Quests.CheckQuest((QuestID)6049)==MsgTaskStatus.QuestData.QuestStatus.Finished,"paid shortcut deducts its fee once and completes the daily without an earned achievement");
        VerifyReleasedRune(paid);
        var old=SoulPlayer(town);SoulNpc(old,79882,3);SoulReady(old);
        Query("UPDATE revora_soul_runs SET valuable_reward=true,rune_reward=false WHERE uid=@uid","uid",(long)old.Entity.UID);
        old=SoulReload(old);SoulNpc(old,301,3);
        Check(!Fenced(old) && old.Inventory.Count==2 && old.Inventory.Objects.All(i=>i.Bound),"previous Release Souls P7 flag still pays only daily gifts after reload");
        Call(Souls,null,"Initialize");VerifyReleasedRune(paid);
        var broke=SoulPlayer(town);SoulNpc(broke,79882,3);broke.Entity.ConquerPoints=4999;SoulNpc(broke,301,2);SoulNpc(broke,301,4);
        Check(broke.Entity.ConquerPoints==4999 && broke.Inventory.Count==0,"insufficient up-front shortcut payment preserves all rewards and balances");
        var expires=SoulPlayer(town);SoulNpc(expires,79882,3);SoulNpc(expires,301,2);soulNow=soulNow.AddMinutes(2);SoulNpc(expires,301,4);
        Check(expires.Entity.ConquerPoints==20000 && expires.Inventory.Count==0,"expired paid confirmation cannot charge the player");
        soulNow=new DateTime(2026,9,15,6,59,59,DateTimeKind.Utc);SoulNpc(other,65823,0);soulNow=soulNow.AddSeconds(3);Call(Souls,null,"Tick");
        Check(SoulCount(other,"cardinality(seen)")==1,"inspection crossing Pacific midnight cannot advance yesterday's quest");
        Call(Souls,null,"Refresh",p);Check(!p.Entity.RelasesTheSoul && !p.Quests.HasQuest((QuestID)6049),"new day clears the native daily projection without deleting achievement history");
        SoulReady(p);SoulNpc(p,301,3);Check(p.Entity.ConquerPoints==25000 && p.Inventory.Count==6 && Scalar("SELECT count(*) FROM revora_soul_runs WHERE uid=@uid",p.Entity.UID)==2,"next day permits a new full route and one additional daily reward");
        Check(Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11012",p.Entity.UID)==1,"repeat daily completions retain one permanent achievement");
        VerifyStudyAward(p,"release-souls");VerifyStudyAward(paid,"release-souls");
        VerifySoulTransactions(town);
        VerifySoulJourney(town);

    }
    static void SoulWire(GameState p,byte option,uint echo=0,int body=20) {
        var frame=new byte[body+8];Writer.WriteUInt16((ushort)body,0,frame);Writer.WriteUInt16(2032,2,frame);
        Writer.WriteUInt32(echo,8,frame);frame[14]=option;Writer.WriteUInt64(PacketHandler.ClientSeal,body,frame);PacketHandler.HandlePacket(frame,p);
    }
    static GameState SoulReload(GameState p) {
        p=ReloadBall(p);Call(Souls,null,"OnLogin",p);if(p.Entity.MapID==1002)SoulNpc(p,79882,3);return p;
    }
    static void VerifySoulTransactions(Map town) {
        foreach(string fault in new[]{"entities","item1","item2","item3","completion","quest-evidence","study-evidence"}) {
            var p=SoulPlayer(town);SoulNpc(p,79882,3);SoulReady(p);
            string table=fault=="entities"?"entities":fault.StartsWith("item")?"items":fault=="completion"?"revora_soul_runs":"revora_achievement_evidence";
            string op=table=="entities" || table=="revora_soul_runs"?"UPDATE":"INSERT";
            string condition=fault=="item1"?"NEW.id=722136":fault=="item2"?"NEW.id=729304":fault=="item3"?"NEW.id=4030101":fault=="quest-evidence"?"NEW.achievement_id=11012":fault=="study-evidence"?"NEW.achievement_id=10319":"true";
            Query("CREATE FUNCTION suppress_soul() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF "+condition+" THEN RETURN NULL; END IF; RETURN NEW; END';CREATE TRIGGER suppress_soul BEFORE "+op+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_soul()");
            try {SoulNpc(p,301,3);}finally {Query("DROP TRIGGER suppress_soul ON "+table+";DROP FUNCTION suppress_soul()");}
            Check(Fenced(p) && p.Inventory.Count==0 && SoulCount(p,"CASE WHEN state='active' THEN 1 ELSE 0 END")==1
                && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==20000
                && Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",p.Entity.UID)==100
                && Scalar("SELECT studypoints FROM entities WHERE uid=@uid",p.Entity.UID)==10
                && Scalar("SELECT count(*) FROM items WHERE entityid=@uid",p.Entity.UID)==0
                && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id IN (11012,10319)",p.Entity.UID)==0,
                "suppressed "+fault+" rolls back the entire soul reward");
            p=SoulReload(p);MoonNear(p,301);SoulWire(p,3,301);VerifyStudyAward(p,"release-souls");
            Check(!Fenced(p) && p.Inventory.Count==3 && p.Entity.ConquerPoints==22500 && HasAchievement(p,11012),"production reload retries soul reward: "+fault);VerifyReleasedRune(p);
            p=SoulReload(p);p.Entity.MyAchievement.Send();Check(!Fenced(p) && p.Inventory.Count==3 && p.Entity.SubClasses.StudyPoints==60 && HasAchievement(p,11012),"saved soul gifts and achievements survive full reload: "+fault);
        }
        foreach(string phase in new[]{"accept","inspect"}) {
            var p=SoulPlayer(town);SoulNpc(p,79882,3);if(phase=="inspect")SoulNpc(p,301,1);
            Query("CREATE FUNCTION suppress_soul() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_soul BEFORE "+(phase=="accept"?"INSERT":"UPDATE")+" ON revora_soul_runs FOR EACH ROW EXECUTE FUNCTION suppress_soul()");
            try {if(phase=="accept")SoulNpc(p,301,1);else SoulVisit(p,65822);}finally{Query("DROP TRIGGER suppress_soul ON revora_soul_runs;DROP FUNCTION suppress_soul()");}
            Check(Fenced(p) && (phase=="accept"?Scalar("SELECT count(*) FROM revora_soul_runs WHERE uid=@uid",p.Entity.UID)==0:SoulCount(p,"cardinality(seen)")==0),"suppressed soul "+phase+" cannot project unsaved progress");
            p=SoulReload(p);SoulReady(p);SoulNpc(p,301,3);Check(!Fenced(p) && p.Inventory.Count==3,"reconnect recovers failed "+phase);
        }
        foreach(string field in new[]{"conquerpoints","revorapoint","studypoints","bag"}) {
            var p=SoulPlayer(town);SoulNpc(p,79882,3);SoulReady(p);
            if(field=="bag") {var item=Pack(p,1088001,1);p.Inventory.Remove(item.UID,Enums.ItemUse.None,false);}
            else Query("UPDATE entities SET "+field+"="+field+"+1 WHERE uid=@uid","uid",(long)p.Entity.UID);
            SoulNpc(p,301,3);Check(Fenced(p) && SoulCount(p,"CASE WHEN state='active' THEN 1 ELSE 0 END")==1,"stale "+field+" rejects the whole reward");
            p=SoulReload(p);SoulNpc(p,301,3);Check(!Fenced(p) && p.Inventory.Count==(field=="bag"?4:3),"authoritative reload preserves changed "+field+" and rewards once");
        }
        foreach(string change in new[]{"day","position","npc"}) {
            var p=SoulPlayer(town);SoulNpc(p,79882,3);SoulReady(p);int calls=0;
            Souls.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=> {
                if(++calls==2) {if(change=="day")return soulNow.AddDays(1);if(change=="position")typeof(Entity).GetField("_x",Hidden).SetValue(p.Entity,(ushort)1);else p.ActiveNpc=65822;}return soulNow;
            }));
            try {SoulNpc(p,301,3);}finally {Souls.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>soulNow));}
            Check(Fenced(p) && p.Inventory.Count==0 && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==20000 && StudyEvidence(p)==0,"changed "+change+" immediately before commit rolls back the complete soul reward");
            p=SoulReload(p);SoulNpc(p,301,3);Check(!Fenced(p) && p.Inventory.Count==3,"reconnect retries expired "+change+" context");
        }
        var offers=SoulPlayer(town);SoulNpc(offers,79882,3);SoulNpc(offers,301,2);SoulWire(offers,255);SoulWire(offers,4);
        Check(offers.Inventory.Count==0 && offers.Entity.ConquerPoints==20000,"wire farewell cancels paid soul offer");
        SoulNpc(offers,301,2);var stale=offers;offers=SoulReload(offers);SoulNpc(offers,301,4);SoulNpc(stale,301,4);
        Check(offers.Inventory.Count==0 && stale.Inventory.Count==0,"replaced session cannot transfer or spend a paid offer");
        SoulNpc(offers,301,2);SoulWire(offers,4,65822);SoulWire(offers,4,301,19);SoulWire(offers,4,301,121);
        Check(offers.Inventory.Count==0 && !Fenced(offers),"wrong NPC echo and short or oversized frames cannot charge");
        SoulWire(offers,4,301);Check(offers.Inventory.Count==3 && offers.Entity.ConquerPoints==17500 && !HasAchievement(offers,11012),"valid paid frame grants one bundle without quest achievement");VerifyStudyAward(offers,"release-souls");
        var future=SoulPlayer(town);SoulNpc(future,79882,3);SoulReady(future);SoulNpc(future,301,3);soulNow=soulNow.AddDays(-1);
        SoulNpc(future,301,1);Check(Fenced(future) && Scalar("SELECT count(*) FROM revora_soul_runs WHERE uid=@uid",future.Entity.UID)==1,"backward day cannot reopen an earlier reward");soulNow=soulNow.AddDays(1);
    }

    static void SoulWalkNpc(GameState p,uint id,byte option) {
        var npc=p.Map.Npcs[id];var xy=FlowerTile(p.Map,npc.X,npc.Y);AdventureWalk(p,xy.Item1,xy.Item2);p.ActiveNpc=id;SoulWire(p,option,id);
    }
    static void VerifySoulJourney(Map town) {
        var p=SoulPlayer(town);SoulNpc(p,79882,3);SoulWalkNpc(p,301,1);
        foreach(uint id in new uint[]{65822,65823,65824}) {
            SoulWalkNpc(p,id,1);soulNow=soulNow.AddSeconds(3);Call(Souls,null,"Tick");
            EntityTable.SaveEntity(p);p=ReloadBall(p);Call(Souls,null,"OnLogin",p);
            Check(!Fenced(p) && p.Quests.GetQuest((QuestID)6049).Kills==id-65821,"ordinary walked inspection survives full character reload");
        }
        SoulWalkNpc(p,301,3);VerifyStudyAward(p,"release-souls");var ball=p.Inventory.Objects.Single(i=>i.ID==722136);ulong before=p.Entity.Experience,award=p.ExpBall;
        ItemHandler.UseItem(ball,p);Check(!Fenced(p) && p.Entity.Experience==before+award && !p.Inventory.ContainsUID(ball.UID) && BallCount(p)==1,"earned soul EXP Ball grants exact ordinary EXP and consumes once");
        p=ReloadBall(p);p.Entity.MyAchievement.Send();Check(!Fenced(p) && p.Entity.Experience==before+award && p.Inventory.Count==2 && p.Inventory.Contains(729304,1) && p.Inventory.Objects.Any(i=>i.ID==4030101 && i.Bound) && HasAchievement(p,11012) && HasAchievement(p,10319),"walked soul reward and used EXP Ball persist through production reload");
    }

}
