using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
internal static partial class PotencyHuntTests
{
    static readonly Type MonkQuest=typeof(Entity).Assembly.GetType("Revora.Game.Features.MonkEpicQuest");
    static readonly Type MonkStore=typeof(Entity).Assembly.GetType("Revora.Game.Features.MonkEpicStore");
    static readonly Type MonkRules=typeof(Entity).Assembly.GetType("Revora.Game.Features.MonkEpicRules");
    static DateTime monkNow=new DateTime(2032,5,1,0,0,0,DateTimeKind.Utc);
    static void MonkRoll(int roll){MonkStore.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>roll));}
    static long MonkOperations(GameState p){return Scalar("SELECT count(*) FROM revora_monk_epic_operations WHERE uid=@uid",p.Entity.UID);}
    static GameState MonkPlayer(Map job){
        var p=PromotionPlayer(job,65,130,4271);p.Entity.Reborn=2;p.Entity.FirstRebornClass=65;p.Entity.SecondRebornClass=65;
        p.Entity.FirstRebornLevel=130;p.Entity.SecondRebornLevel=130;p.Entity.ConquerPoints=1000;p.Entity.X=78;p.Entity.Y=42;
        p.Quests=new Quests(p);p.Quests.Load();EntityTable.SaveEntity(p);return p;
    }
    static void MonkEnter(GameState p){EpicNpc(p,4271,86);if(p.Entity.MapID!=3983 || Fenced(p))throw new Exception("Monk quest entry failed.");}
    static void MonkOffer(GameState p,uint npc,byte option,int roll){MonkRoll(roll);EpicNpc(p,npc,option);EpicConfirm(p);}
    static void MonkGem(GameState p){NestedPack(p,722732,3,true);NestedPack(p,722736,3,false);MonkOffer(p,199484,1,0);}
    static void VerifyMonkEpic(string runtime){
        VerifyMonkTraining(runtime);PrepareNinjaFlowers(runtime);Call(NinjaFlowers,null,"Initialize");
        Query("CREATE TABLE IF NOT EXISTS sobnpcs AS TABLE public.sobnpcs WITH NO DATA;CREATE TABLE IF NOT EXISTS furniture AS TABLE public.furniture WITH NO DATA");
        Call(MonkQuest,null,"PrepareMaps");var map=new Map(3983,"map/map/epicm-monk.7z");Call(MonkQuest,null,"Initialize");Call(MonkQuest,null,"Initialize");
        MonkStore.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>monkNow));
        var job=Kernel.Maps[1004];var p=MonkPlayer(job);MonkEnter(p);
        Check(p.Account.State==AccountTable.AccountState.Player && !p.Fake && p.Entity.X==87 && p.Entity.Y==71 && map.Npcs.Count==5,"ordinary Monk enters native map 3983 with five registered quest actors");
        foreach(uint npc in new uint[]{199483,199484,199485,199486,199487}){
            AdventureNear(p,npc);AbyssOpen(p,npc);
            Check(p.ActiveNpc==npc && map.Floor[p.Entity.X,p.Entity.Y,MapObjectType.Player] && !map.Floor[map.Npcs[npc].X,map.Npcs[npc].Y,MapObjectType.Player],"actual movement reaches the quest actor and respects NPC collision: "+npc);
        }
        VerifyMonkRules();
        NestedPack(p,722732,3,true);NestedPack(p,722736,3,false);MonkOffer(p,199484,1,99);
        Check(!Fenced(p) && MonkOperations(p)==1 && EpicAmount(p,722732)==0 && EpicAmount(p,722736)==0 && EpicAmount(p,3007562)==0 && p.Entity.ConquerPoints==1000,"failed Fortune consumes exactly three of each flower without CPs or invented reward");
        Parallel.For(0,12,_=>SoulWire(p,20,199484));Check(MonkOperations(p)==1,"concurrent confirmation replay cannot repeat the offering");
        MonkGem(p);Check(!Fenced(p) && EpicAmount(p,3007562)==1 && p.Inventory.Objects.Single(i=>i.ID==3007562).Bound,"mixed bound flowers produce a bound Spiritual Gem");
        MonkOffer(p,199485,1,99);Check(!Fenced(p) && EpicAmount(p,3007562)==0 && EpicAmount(p,3007565)==1,"failed Altar grants one incense and requires a fresh Fortune Gem");
        MonkGem(p);MonkOffer(p,199485,1,0);MonkOffer(p,199486,1,99);
        Check(!Fenced(p) && EpicAmount(p,3007563)==0 && EpicAmount(p,3007562)==1 && EpicAmount(p,3007565)==4 && p.Inventory.Objects.All(i=>i.Bound),"failed Victory grants three bound incense and restores a bound Gem for the Altar retry");
        MonkOffer(p,199485,1,0);MonkOffer(p,199486,1,0);
        Check(EpicAmount(p,3007564)==1 && EpicAmount(p,3007562)==0 && EpicAmount(p,3007563)==0,"documented Altar retry can reach a Boundless Heart");
        foreach(string admission in new[]{"level","class","reborn","trade","fake","dead","far","echo","short"}){
            var denied=MonkPlayer(job);
            if(admission=="level")denied.Entity.Level=69;if(admission=="class")denied.Entity.Class=55;if(admission=="reborn")denied.Entity.Reborn=1;
            if(admission=="trade")denied.Trade.InTrade=true;if(admission=="fake")denied.Fake=true;if(admission=="dead")denied.Entity.Hitpoints=0;
            if(admission=="far")denied.Entity.X=1;
            SoulWire(denied,86,admission=="echo"?1u:4271u,admission=="short"?18:20);
            Check(denied.Entity.MapID==1004 && MonkOperations(denied)==0,"ordinary admission rejects ineligible or forged entry: "+admission);
        }
        VerifyMonkFaults(job);VerifyMonkHearts(job);VerifyMonkHuntedRoute(job);VerifyMonkJournal(job);VerifyMonkSutta(job);
    }
    static long SuttaReadings(GameState p){return Scalar("SELECT count(*) FROM revora_monk_sutta_readings WHERE uid=@uid",p.Entity.UID);}
    static void VerifyMonkSutta(Map job){
        var p=MonkPlayer(job);ulong before=p.Entity.Experience,award=p.ExpBall/2;byte balls=p.ExpBalls;
        Parallel.For(0,12,_=>SoulWire(p,88,4271));var book=p.Inventory.Objects.Single(i=>i.ID==3007561);
        Check(book.Bound && book.StackSize==1 && Scalar("SELECT count(*) FROM revora_monk_sutta_gifts WHERE uid=@uid",p.Entity.UID)==1,"ordinary Head Abbot requests issue exactly one bound introductory book");
        Parallel.For(0,12,_=>Drink(p,book));
        Check(!Fenced(p) && SuttaReadings(p)==1 && p.Entity.Experience==before+award && !p.Inventory.ContainsUID(book.UID) && p.ExpBalls==balls && p.Entity.ConquerPoints==1000,"ordinary owned Sutta grants thirty-minute EXP once without CP cost or EXP-ball allowance");
        Check(MonkOperations(p)==0 && MonkObjectives(p).All(n=>n==0),"introductory reading grants no epic trial progress");
        p=ReloadBall(p);p.ActiveNpc=4271;before=p.Entity.Experience;SoulWire(p,88,4271);book=NestedPack(p,3007561,2,false);Drink(p,book);
        Check(!Fenced(p) && SuttaReadings(p)==1 && p.Entity.Experience==before && book.StackSize==2 && SavedRune(book,"stacksize")==2,"cold reload and a second owned book cannot repeat the lifetime reward or gift");
        foreach(byte rank in new byte[]{60,61,62,63,64,65}){
            var owner=MonkPlayer(job);owner.Entity.Class=rank;owner.Entity.Level=70;EntityTable.SaveEntity(owner);before=owner.Entity.Experience;award=owner.ExpBall/2;
            book=NestedPack(owner,3007561,2,rank%2==0);Drink(owner,book);
            Check(!Fenced(owner) && owner.Entity.Experience==before+award && book.StackSize==1 && SavedRune(book,"stacksize")==1 && SuttaReadings(owner)==1,"owned legacy Sutta rewards eligible rank with exact stack debit: "+rank);
        }
        var levelUp=MonkPlayer(job);levelUp.Entity.Experience=DataHolder.LevelExperience(130)-1;EntityTable.SaveEntity(levelUp);award=levelUp.ExpBall/2;ushort points=levelUp.Entity.Atributes;
        book=Pack(levelUp,3007561,1);Drink(levelUp,book);
        Check(!Fenced(levelUp) && levelUp.Entity.Level==131 && levelUp.Entity.Experience==award-1 && levelUp.Entity.Atributes==points+Config.AttributePointsPerLevel,"Sutta level-up persists normal progression and attribute rewards");
        levelUp=ReloadBall(levelUp);Check(levelUp.Entity.Level==131 && levelUp.Entity.Experience==award-1 && levelUp.Entity.Atributes==points+Config.AttributePointsPerLevel && SuttaReadings(levelUp)==1,"Sutta level-up and lifetime receipt survive reload together");
        foreach(string mode in new[]{"class","level","reborn","dead","fake","far","trade","cap","curse","locked","suspicious","temporary","foreign"}){
            var owner=MonkPlayer(job);book=Pack(owner,3007561,1);before=owner.Entity.Experience;
            if(mode=="class")owner.Entity.Class=55;if(mode=="level")owner.Entity.Level=69;if(mode=="reborn")owner.Entity.Reborn=1;
            if(mode=="dead")owner.Entity.Hitpoints=0;if(mode=="fake")owner.Fake=true;if(mode=="far")owner.Entity.X=1;if(mode=="trade")owner.Trade.InTrade=true;
            if(mode=="cap")owner.Entity.Level=Config.LevelCap;if(mode=="curse")owner.Entity.AddFlag((ulong)FlagsUpdate.Flags.Cursed,60,false);
            if(mode=="locked")book.Lock=1;if(mode=="suspicious")book.Suspicious=true;if(mode=="temporary")book.Days=1;
            if(mode=="foreign")ItemHandler.UseItem(new ConquerItem(true){UID=book.UID,ID=3007561},owner);else Drink(owner,book);
            Check(SuttaReadings(owner)==0 && owner.Entity.Experience==before && owner.Inventory.ContainsUID(book.UID) && SavedRune(book,"stacksize")==1,"ineligible Sutta use preserves the book and experience: "+mode);
        }
        var malformed=MonkPlayer(job);book=Pack(malformed,3007561,1);
        foreach(string mode in new[]{"short","length","type","seal","long"}){
            var packet=PrayerPacket(book.UID);if(mode=="short")packet=packet.Take(24).ToArray();if(mode=="length")Revora.Network.Writer.WriteUInt16(30,0,packet);
            if(mode=="type")Revora.Network.Writer.WriteUInt16(1111,2,packet);if(mode=="seal")packet[packet.Length-1]^=1;
            if(mode=="long"){Array.Resize(ref packet,129);Revora.Network.Writer.WriteUInt16(121,0,packet);}
            ItemUsage.Process(packet,malformed);Check(!Fenced(malformed) && SuttaReadings(malformed)==0 && malformed.Inventory.ContainsUID(book.UID),"malformed Sutta item request rejected: "+mode);
        }
        foreach(bool gift in new[]{false,true})foreach(string table in gift?new[]{"entities","items","revora_monk_sutta_gifts"}:new[]{"entities","items","revora_monk_sutta_readings"}){
            var owner=MonkPlayer(job);before=owner.Entity.Experience;book=gift?null:Pack(owner,3007561,1);
            Query("CREATE FUNCTION suppress_monk_sutta() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
            Query("CREATE TRIGGER suppress_monk_sutta BEFORE INSERT OR UPDATE OR DELETE ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_monk_sutta()");
            try{if(gift)SoulWire(owner,88,4271);else Drink(owner,book);}finally{Query("DROP TRIGGER suppress_monk_sutta ON "+table+";DROP FUNCTION suppress_monk_sutta()");}
            Check(Fenced(owner) && SuttaReadings(owner)==0 && owner.Entity.Experience==before,"Sutta failure preserves experience and no reading receipt: gift="+gift+" table="+table);
            owner=ReloadBall(owner);owner.ActiveNpc=4271;
            Check(owner.Entity.Experience==before && (gift?EpicAmount(owner,3007561)==0:owner.Inventory.ContainsUID(book.UID)),"failed Sutta gift/reading keeps durable inventory consistent");
            if(gift)SoulWire(owner,88,4271);else Drink(owner,owner.Inventory.Objects.Single(i=>i.ID==3007561));
            Check(!Fenced(owner) && (gift?EpicAmount(owner,3007561)==1:SuttaReadings(owner)==1),"reconnected player can retry an uncommitted Sutta transaction");
        }
        var full=MonkPlayer(job);for(int n=0;n<40;n++)Pack(full,1088001,1);SoulWire(full,88,4271);
        Check(!Fenced(full) && Scalar("SELECT count(*) FROM revora_monk_sutta_gifts WHERE uid=@uid",full.Entity.UID)==0 && full.Inventory.Count==40,"full bag leaves the one-time Sutta gift available");
    }
    static readonly Type MonkJournal=typeof(Entity).Assembly.GetType("Revora.Game.Features.MonkEpicJournal");
    static MsgTaskStatus.QuestData MonkJournalRow(GameState p){return AbyssList(p).Single(i=>(uint)i.UID==3526);}
    static uint[] MonkObjectives(GameState p){var detail=(MsgTaskDetailInfo)Call(MonkJournal,null,"Details",Call(MonkJournal,null,"Snapshot",p));return new[]{detail[1],detail[2],detail[3]};}
    static void VerifyMonkJournal(Map job){
        var p=MonkPlayer(job);Pack(p,3007564,1);Pack(p,3007562,1);Pack(p,3007563,1);
        p.Quests.Accept((QuestID)3526);p.Quests.IncreaseQuestKills((QuestID)3526,99);p.Quests.Save();
        Check(MonkJournalRow(p).Status==MsgTaskStatus.QuestData.QuestStatus.Available && MonkObjectives(p).All(n=>n==0),"owned trial gifts and legacy accepted progress cannot author Monk trial evidence");
        var begin=new MsgTaskStatus(true,1){Action=MsgTaskStatus.QuestAction.Begin};begin.Apend(MsgTaskStatus.QuestData.Create((QuestID)3526,MsgTaskStatus.QuestData.QuestStatus.Finished,0));
        MsgTaskStatus.Process(begin.ToArray(),p);Revora.Network.PacketHandler.HandlePacket(AbyssQuit(3526),p);
        Check(MonkJournalRow(p).Status==MsgTaskStatus.QuestData.QuestStatus.Available && p.Quests.GetQuest((QuestID)3526).Kills==99 && MonkOperations(p)==0,"forged Monk Begin/quit preserve historical evidence and serialized legacy data");
        MonkEnter(p);NestedPack(p,722732,3,false);NestedPack(p,722736,3,false);MonkOffer(p,199484,1,99);
        Check(MonkJournalRow(p).Status==MsgTaskStatus.QuestData.QuestStatus.Accepted && MonkObjectives(p).All(n=>n==0),"first failed flower offering starts journal without marking a success");
        MonkGem(p);Check(MonkObjectives(p).SequenceEqual(new uint[]{1,0,0}),"committed Fortune success immediately projects only the first objective");
        MonkOffer(p,199485,1,99);Check(MonkObjectives(p).SequenceEqual(new uint[]{1,0,0}),"failed Altar preserves earned Fortune and does not grant a Necklace objective");
        MonkOffer(p,199485,1,0);Check(MonkObjectives(p).SequenceEqual(new uint[]{1,1,0}),"personally earned Necklace advances the second journal objective");
        MonkOffer(p,199486,1,99);Check(MonkObjectives(p).SequenceEqual(new uint[]{1,1,0}),"Victory retry Gem does not falsely complete the Heart trial");
        MonkOffer(p,199486,1,0);
        Check(MonkJournalRow(p).Status==MsgTaskStatus.QuestData.QuestStatus.Finished && MonkObjectives(p).All(n=>n==1),"all three actual successful trials finish the historical journal");
        var detail=(MsgTaskDetailInfo)Call(MonkJournal,null,"Details",Call(MonkJournal,null,"Snapshot",p));var bytes=detail.ToArray();
        Check(System.BitConverter.ToUInt16(bytes,2)==1135 && System.BitConverter.ToUInt32(bytes,8)==3526 && System.BitConverter.ToUInt32(bytes,12)==1 && System.BitConverter.ToUInt32(bytes,20)==1,"existing 1135 objective offsets carry the three native mission intentions");
        long operations=MonkOperations(p),hearts=EpicAmount(p,3007564);p.ActiveNpc=0;
        for(int i=0;i<5;i++){MsgTaskStatus.Process(begin.ToArray(),p);Revora.Network.PacketHandler.HandlePacket(AbyssQuit(3526),p);}
        foreach(int body in new[]{19,121})Revora.Network.PacketHandler.HandlePacket(AbyssQuit(3526,body),p);
        Check(!Fenced(p) && MonkOperations(p)==operations && EpicAmount(p,3007564)==hearts && MonkJournalRow(p).Status==MsgTaskStatus.QuestData.QuestStatus.Finished,"replayed and malformed journal requests cannot reset completion or mint rewards");
        p.Quests.Save();var loaded=ReloadBall(p);
        Check(MonkJournalRow(loaded).Status==MsgTaskStatus.QuestData.QuestStatus.Finished && MonkObjectives(loaded).All(n=>n==1) && loaded.Quests.GetQuest((QuestID)3526).Kills==99,"cold character reload restores earned trial flags independently of legacy quest data");
        Check(Call(MonkJournal,null,"Snapshot",p)==null,"superseded Monk session cannot project another connection's journal");
        MonkGem(loaded);Check(!Fenced(loaded) && MonkOperations(loaded)==operations+1 && MonkJournalRow(loaded).Status==MsgTaskStatus.QuestData.QuestStatus.Finished,"completed historical journal still permits another weapon's trials");
        loaded.Entity.Class=15;EntityTable.SaveEntity(loaded);Check(MonkJournalRow(loaded).Status==MsgTaskStatus.QuestData.QuestStatus.Finished,"earned history remains visible after changing school without granting Monk eligibility");
        foreach(string mode in new[]{"class","level","reborn"}){
            var denied=MonkPlayer(job);denied.Quests.Accept((QuestID)3526);
            if(mode=="class")denied.Entity.Class=55;if(mode=="level")denied.Entity.Level=69;if(mode=="reborn")denied.Entity.Reborn=1;
            Check(!AbyssList(denied).Any(i=>(uint)i.UID==3526),"ineligible character cannot expose legacy unearned Monk progress: "+mode);
        }
        var failed=MonkPlayer(job);MonkEnter(failed);Check(MonkJournalRow(failed).Status==MsgTaskStatus.QuestData.QuestStatus.Available,"entering paradise alone grants no trial progress");
        NestedPack(failed,722732,3,false);NestedPack(failed,722736,3,false);MonkRoll(0);EpicNpc(failed,199484,1);
        Query("CREATE FUNCTION suppress_monk_journal() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
        Query("CREATE TRIGGER suppress_monk_journal BEFORE INSERT ON revora_monk_epic_rewards FOR EACH ROW EXECUTE FUNCTION suppress_monk_journal()");
        try{EpicConfirm(failed);}finally{Query("DROP TRIGGER suppress_monk_journal ON revora_monk_epic_rewards;DROP FUNCTION suppress_monk_journal()");}
        failed=ReloadBall(failed);Check(MonkOperations(failed)==0 && MonkJournalRow(failed).Status==MsgTaskStatus.QuestData.QuestStatus.Available && MonkObjectives(failed).All(n=>n==0),"late reward failure rolls back journal evidence with the flower debit across reload");
    }
    static void VerifyMonkRules(){
        for(int stage=1;stage<=3;stage++){
            int successes=0;for(int roll=0;roll<100;roll++){
                var result=Call(MonkRules,null,"Resolve",Enum.ToObject(MonkRules.GetNestedType("Trial",System.Reflection.BindingFlags.NonPublic),stage),roll);
                bool success=NinjaResultField<bool>(result,"Success");if(success)successes++;
                uint item=NinjaResultField<uint>(result,"Item");int count=NinjaResultField<int>(result,"Count");bool retry=NinjaResultField<bool>(result,"RetryGem");
                if(success?item!=3007561u+stage || count!=1 || retry:item!=(stage==1?0u:3007565u) || count!=(stage==1?0:stage==2?1:3) || retry!=(stage==3))throw new Exception("Monk trial graph differs from disclosed policy.");
            }Check(successes==50,"every possible draw implements exactly fifty percent success: stage "+stage);
        }
        foreach(var bad in new[]{new[]{0,0},new[]{4,0},new[]{1,-1},new[]{3,100}}){bool denied=false;try{Call(MonkRules,null,"Resolve",Enum.ToObject(MonkRules.GetNestedType("Trial",System.Reflection.BindingFlags.NonPublic),bad[0]),bad[1]);}catch(ArgumentException){denied=true;}Check(denied,"invalid trial or RNG draw fails closed");}
        int pairs=0;Func<uint,bool> catalog=id=>ConquerItemInformation.BaseInformations.ContainsKey(id);
        foreach(var i in ConquerItemInformation.BaseInformations.Values.Where(i=>i.ID/1000==610)){
            uint target=(uint)Call(MonkRules,null,"EpicBeads",i.ID,catalog);if(target==0)continue;
            if(target/1000!=622 || target%1000!=i.ID%1000 || ConquerItemInformation.BaseInformations[target].Level!=i.Level)throw new Exception("Epic conversion changes catalog quality/level.");pairs++;
        }Check(pairs==343,"all 343 installed Prayer Beads conversions preserve quality and level");
        foreach(uint id in new uint[]{601439,622439,410339,610999})Check((uint)Call(MonkRules,null,"EpicBeads",id,catalog)==0,"unsupported, absent and already-epic inputs rejected: "+id);
        foreach(byte school in new byte[]{60,61,62,63,64,65})Check((bool)Call(MonkRules,null,"Eligible",school,(byte)2,(byte)70),"all six Monk ranks qualify at second rebirth level 70");
        foreach(var row in new[]{new[]{65,2,69},new[]{65,1,140},new[]{65,0,140},new[]{65,3,140},new[]{55,2,140},new[]{75,2,140}})
            Check(!(bool)Call(MonkRules,null,"Eligible",(byte)row[0],(byte)row[1],(byte)row[2]),"invalid quest admission is rejected");
    }
    static void VerifyMonkFaults(Map job){
        foreach(string mode in new[]{"no-offer","expired","cancel","balance","bag","trade","wrong-echo","short","long","far"}){
            var p=MonkPlayer(job);MonkEnter(p);NestedPack(p,722732,3,true);NestedPack(p,722736,3,false);MonkRoll(0);EpicNpc(p,199484,1);
            if(mode=="no-offer")AbyssOpen(p,199484);if(mode=="expired")monkNow=monkNow.AddMinutes(2);if(mode=="cancel")SoulWire(p,255,199484);
            if(mode=="balance")p.Entity.ConquerPoints--;if(mode=="bag")Pack(p,1088001,1);if(mode=="trade")p.Trade.InTrade=true;if(mode=="far")p.Entity.X=1;
            SoulWire(p,20,mode=="wrong-echo"?1u:199484u,mode=="short"?18:mode=="long"?121:20);
            Check(MonkOperations(p)==0 && EpicAmount(p,3007562)==0 && EpicAmount(p,722732)==3 && EpicAmount(p,722736)==3,"invalid confirmation preserves flower payments: "+mode);
        }
        foreach(string table in new[]{"entities","items","revora_monk_epic_operations","revora_monk_epic_materials","revora_monk_epic_rewards"})foreach(bool victory in new[]{false,true}){
            var p=MonkPlayer(job);MonkEnter(p);var material=NestedPack(p,victory?3007563u:3007565u,victory?(ushort)1:(ushort)10,true);
            MonkRoll(99);EpicNpc(p,victory?199486u:199487u,1);
            Query("CREATE FUNCTION suppress_monk_epic() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
            Query("CREATE TRIGGER suppress_monk_epic BEFORE INSERT OR UPDATE OR DELETE ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_monk_epic()");
            try{EpicConfirm(p);}finally{Query("DROP TRIGGER suppress_monk_epic ON "+table+";DROP FUNCTION suppress_monk_epic()");}
            Check(Fenced(p) && MonkOperations(p)==0 && SavedRune(material,"stacksize")==material.StackSize && p.Inventory.ContainsUID(material.UID)
                && EpicAmount(p,3007562)==0 && EpicAmount(p,3007568)==0,"failed exchange rolls back materials and all rewards: "+table+" victory="+victory);
            p=ReloadBall(p);Check(MonkOperations(p)==0 && p.Inventory.ContainsUID(material.UID),"reload preserves failed offering and no receipt");
        }
        foreach(bool retryGem in new[]{false,true}){
            var owner=MonkPlayer(job);MonkEnter(owner);
            if(retryGem)NestedPack(owner,3007563,1,true);else{NestedPack(owner,722732,3,true);NestedPack(owner,722736,3,false);}
            MonkRoll(retryGem?99:0);EpicNpc(owner,retryGem?199486u:199484u,1);
            Query("CREATE FUNCTION suppress_monk_late() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
            Query("CREATE TRIGGER suppress_monk_late BEFORE "+(retryGem?"INSERT":"DELETE")+" ON items FOR EACH ROW WHEN ("+(retryGem?"NEW.id=3007562":"OLD.id=722736")+") EXECUTE FUNCTION suppress_monk_late()");
            try{EpicConfirm(owner);}finally{Query("DROP TRIGGER suppress_monk_late ON items;DROP FUNCTION suppress_monk_late()");}
            Check(Fenced(owner) && MonkOperations(owner)==0 && EpicAmount(owner,3007562)==0 && EpicAmount(owner,3007565)==0
                && (retryGem?EpicAmount(owner,3007563)==1:EpicAmount(owner,722732)==3 && EpicAmount(owner,722736)==3),
                "late failure rolls back earlier flower debits or three already-written incense rewards: retryGem="+retryGem);
            owner=ReloadBall(owner);Check(MonkOperations(owner)==0 && EpicAmount(owner,3007565)==0 && (retryGem?EpicAmount(owner,3007563)==1:EpicAmount(owner,722732)==3 && EpicAmount(owner,722736)==3),"late-failure rollback survives reload");
        }
        foreach(int bag in new[]{37,38}){
            var owner=MonkPlayer(job);MonkEnter(owner);var necklace=NestedPack(owner,3007563,1,false);
            for(int n=1;n<bag;n++)Pack(owner,1088001,1);
            MonkOffer(owner,199486,1,99);
            Check(!Fenced(owner) && (bag==37?owner.Inventory.Count==40 && !owner.Inventory.ContainsUID(necklace.UID) && EpicAmount(owner,3007565)==3 && EpicAmount(owner,3007562)==1
                :owner.Inventory.Count==38 && owner.Inventory.ContainsUID(necklace.UID) && MonkOperations(owner)==0),"Victory reserves four reward slots before spending: bag="+bag);
        }
        foreach(string mode in new[]{"class","map","material","session","rng","overflow"}){
            var p=MonkPlayer(job);MonkEnter(p);var material=NestedPack(p,3007563,1,true);MonkRoll(99);EpicNpc(p,199486,1);
            if(mode=="class")Query("UPDATE entities SET class=15 WHERE uid=@uid","uid",(long)p.Entity.UID);
            if(mode=="map")Query("UPDATE entities SET mapid=1002 WHERE uid=@uid","uid",(long)p.Entity.UID);
            if(mode=="material")Query("UPDATE items SET bound=0 WHERE uid=@uid","uid",(long)material.UID);
            if(mode=="session")MonkStore.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>{p.Trade.InTrade=true;return 99;}));
            if(mode=="rng")MonkRoll(100);
            if(mode=="overflow")for(int n=0;n<39;n++)Pack(p,1088001,1);
            EpicConfirm(p);Check(MonkOperations(p)==0 && p.Inventory.ContainsUID(material.UID) && EpicAmount(p,3007562)==0,"changed saved/session/capacity/RNG state cannot produce partial rewards: "+mode);MonkRoll(0);
        }
    }
    static void VerifyEpicSoul(GameState p,string family,int count){
        if(family!="monk" && family!="ninja")throw new ArgumentException("Unknown epic fixture");
        var souls=p.Inventory.Objects.Where(i=>i.ID>=800000 && i.ID<900000).ToArray();
        Check(souls.Length==count && souls.All(i=>i.Bound && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)
            && Scalar("SELECT count(*) FROM revora_"+family+"_epic_rewards r JOIN items i ON i.uid=r.item_uid WHERE i.entityid=@uid AND r.item_type BETWEEN 800000 AND 899999 AND r.bound AND i.bound=1",p.Entity.UID)==count,
            family+" awakening souls retain catalog P7 identity, bound state and durable receipt membership");
    }
    static void VerifyMonkHearts(Map job){
        var packed=MonkPlayer(job);RebirthGear(packed,4,610439);var stacked=NestedPack(packed,3007564,2,true);
        while(packed.Inventory.Count<40)Pack(packed,1088001,1);
        ItemHandler.UseItem(stacked,packed);Check(MonkOperations(packed)==0 && stacked.StackSize==2,"full bag rejects awakening when the Heart stack cannot free a soul slot");
        DisRemove(packed,packed.Inventory.Objects.First(i=>i.ID==1088001));ItemHandler.UseItem(stacked,packed);VerifyEpicSoul(packed,"monk",1);

        var p=MonkPlayer(job);var right=RebirthGear(p,4,610439);var left=RebirthGear(p,5,610439);var heart=NestedPack(p,3007564,2,true);
        right.Plus=6;right.Bless=3;right.Enchant=100;right.SocketOne=Enums.Gem.SuperDragonGem;right.PerfectionOwnerGuid=p.Entity.UID;right.PerfectionOwner=p.Entity.Name;right.Signature="Earned pillar";
        Query("UPDATE items SET plus=6,bless=3,enchant=100,socketone=@socket,perfectionownerguid=@owner,perfectionowner=@name,signature=@sig WHERE uid=@uid","socket",(int)right.SocketOne,"owner",(long)p.Entity.UID,"name",p.Entity.Name,"sig",right.Signature,"uid",(long)right.UID);
        ItemHandler.UseItem(heart,p);ItemHandler.UseItem(heart,p);
        Check(!Fenced(p) && right.ID==622439 && right.Bound && left.ID==610439 && heart.StackSize==1 && MonkOperations(p)==1,"Heart use awakens right beads and rejects immediate duplicate use");
        VerifyEpicSoul(p,"monk",1);p=ReloadBall(p);VerifyEpicSoul(p,"monk",1);p.LoadItemStats();p.CalculateStatBonus();p.CalculateHPBonus();p.GemAlgorithm();right=p.Equipment.TryGetItem(4);left=p.Equipment.TryGetItem(5);heart=p.Inventory.Objects.Single(i=>i.ID==3007564);ItemHandler.UseItem(heart,p);
        Check(!Fenced(p) && MonkOperations(p)==1 && right.Plus==6 && right.Bless==3 && right.Enchant==100 && right.SocketOne==Enums.Gem.SuperDragonGem && right.PerfectionOwnerGuid==p.Entity.UID && right.Signature=="Earned pillar" && left.ID==610439,"Heart replay guard and enhanced gear survive full reload");
        monkNow=monkNow.AddSeconds(2);ItemHandler.UseItem(heart,p);Check(!Fenced(p) && left.ID==622439 && left.Bound && !p.Inventory.ContainsUID(heart.UID) && MonkOperations(p)==2,"later deliberate Heart use awakens left beads");
        foreach(string mode in new[]{"weapon-write","material-write","reward-write","saved-metadata","stale-item","wrong-weapon","wrong-heart"}){
            p=MonkPlayer(job);right=RebirthGear(p,4,mode=="wrong-weapon"?601439u:610439u);heart=NestedPack(p,mode=="wrong-heart"?3007115u:3007564u,1,true);
            if(mode=="saved-metadata")Query("UPDATE items SET plus=7 WHERE uid=@uid","uid",(long)right.UID);
            if(mode.EndsWith("-write")){
                Query("CREATE FUNCTION suppress_monk_heart() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
                Query("CREATE TRIGGER suppress_monk_heart BEFORE "+(mode=="weapon-write"?"UPDATE":mode=="reward-write"?"INSERT":"DELETE")+" ON items FOR EACH ROW EXECUTE FUNCTION suppress_monk_heart()");
            }
            try{if(mode=="wrong-heart")Call(MonkQuest,null,"TryUse",heart,p);else ItemHandler.UseItem(mode=="stale-item"?new ConquerItem(true){UID=heart.UID,ID=heart.ID}:heart,p);}
            finally{if(mode.EndsWith("-write"))Query("DROP TRIGGER suppress_monk_heart ON items;DROP FUNCTION suppress_monk_heart()");}
            Check(!p.Inventory.Objects.Any(i=>i.ID>=800000 && i.ID<900000) && MonkOperations(p)==0 && right.ID==(mode=="wrong-weapon"?601439u:610439u) && p.Inventory.ContainsUID(heart.UID),"invalid Heart/conversion cannot spend or replace gear: "+mode);
        }
    }
    static void VerifyMonkHuntedRoute(Map job){
        bool money=Config.MoneyDropEnabled,cp=Config.KillConquerPointsEnabled;Config.MoneyDropEnabled=false;Config.KillConquerPointsEnabled=false;
        NinjaFlowers.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>ninjaFlowerRoll));
        try{
            var ninja=EpicPlayer(job);ninja.Quests=new Quests(ninja);ninja.Quests.Load();EpicNpc(ninja,4720,87);
            var ninjaDrop=NinjaFlower(ninja,0);Loot(ninja,ninjaDrop);EpicNpc(ninja,3604,90);
            Check(!Fenced(ninja) && ninja.Entity.MapID==1004 && ninja.Entity.X==38 && ninja.Entity.Y==42 && FlowerReceipts(ninja)==1 && EpicAmount(ninja,722732)==1,
                "enabled Monk route preserves Ninja flower payment ownership and Wind Sage return");
            var p=MonkPlayer(job);EpicNpc(p,4271,87);Check(p.Entity.MapID==1700,"Head Abbot sends ordinary eligible Monks to the existing flower hunt");
            for(uint family=3600;family<=3631;family++){var drop=NinjaFlower(p,(int)(family%2),family);Loot(p,drop);Check(!Fenced(p) && drop.PickedUpAlready && p.Inventory.ContainsUID(drop.Item.UID),"Monk earns durable flower from actual family death: "+family);}
            // Eighteen hunted flowers fund three Gem routes: ten incense each via three Victory failures and one Altar failure.
            EpicNpc(p,3604,90);Check(p.Entity.MapID==1004 && p.Entity.X==78 && p.Entity.Y==42,"Bruce returns the Monk beside Head Abbot");MonkEnter(p);
            ushort[] spells={12550,12560,12570};byte[] learns={23,24,25};
            for(byte scroll=1;scroll<=3;scroll++){
                MonkOffer(p,199484,1,0);
                for(int retry=0;retry<3;retry++){MonkOffer(p,199485,1,0);MonkOffer(p,199486,1,99);}
                MonkOffer(p,199485,1,99);Check(EpicAmount(p,3007565)==10,"actual failed trials earn ten incense for a scroll");
                MonkOffer(p,199487,scroll,0);Check(EpicAmount(p,(uint)(scroll==1?3007568:scroll==2?3007566:3007567))==1 && EpicAmount(p,3007565)==0,"earned incense buys the selected exact scroll");
                EpicNpc(p,199487,254);EpicNpc(p,4271,learns[scroll-1]);Check(!Fenced(p) && p.Spells.ContainsKey(spells[scroll-1]),"hunted-material scroll teaches the ordinary Monk art: "+spells[scroll-1]);MonkEnter(p);
            }
            MonkOffer(p,199484,1,0);MonkOffer(p,199485,1,0);MonkOffer(p,199486,1,0);var gear=RebirthGear(p,4,610439);MonkOffer(p,199483,4,0);
            Check(!Fenced(p) && gear.ID==622439 && MonkOperations(p)==31 && EpicAmount(p,3007564)==0,"hunted offerings complete three scrolls and one epic weapon conversion");
            VerifyEpicSoul(p,"monk",1);p=ReloadBall(p);VerifyEpicSoul(p,"monk",1);Check(!Fenced(p) && spells.All(id=>p.Spells.ContainsKey(id)) && p.Equipment.TryGetItem(4).ID==622439 && p.Entity.ConquerPoints==1000 && FlowerReceipts(p)==32,"complete hunted-material-to-three-skills-and-epic journey survives production reload without quest CP debit");
        }finally{Config.MoneyDropEnabled=money;Config.KillConquerPointsEnabled=cp;}
    }
}
