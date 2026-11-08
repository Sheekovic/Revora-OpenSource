using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network;
using Revora.Network.GamePackets;
using Store=Revora.Game.ConquerStructures.Warehouse;

internal static partial class PotencyHuntTests
{
    static int flowerRoll,flowerDraws;
    static long FlowerChanges(GameState p){return Scalar("SELECT count(*) FROM revora_magnolia_changes WHERE uid=@uid",p.Entity.UID);}
    static void FlowerWire(GameState p,byte choice,uint echo=44100){
        p.ActiveNpc=44100;var frame=new byte[28];Writer.WriteUInt16(20,0,frame);Writer.WriteUInt16(2032,2,frame);
        Writer.WriteUInt32(echo,8,frame);frame[14]=choice;Writer.WriteUInt64(PacketHandler.ClientSeal,20,frame);PacketHandler.HandlePacket(frame,p);
    }
    static void VerifyMagnoliaGrades(Map garden,string runtime){
        VerifyFlowerAdmission(garden);
        Call(Magnolia,null,"Initialize");Magnolia.GetField("SeedRoll",Hidden).SetValue(null,(Func<int,int>)(max=>{flowerDraws++;return flowerRoll;}));
        var counts=new int[5];for(flowerRoll=0;flowerRoll<100;flowerRoll++)counts[(uint)Call(Magnolia,null,"DrawSeed")-729306]++;
        Check(counts.SequenceEqual(new[]{50,30,15,4,1}),"every Revora seed lottery slot matches the disclosed 50/30/15/4/1 distribution");
        int[] rolls={0,50,80,95,99},minutes={60,90,120,180,300},studies={10,20,50,100,300};
        for(int grade=0;grade<5;grade++)foreach(bool bound in new[]{false,true}){
            flowerRoll=rolls[grade];var p=GardenPlayer(garden);var source=FlowerSeed(p);source.Bound=bound;
            Query("UPDATE items SET bound=@bound WHERE uid=@uid","bound",bound?1:0,"uid",(long)source.UID);FlowerWire(p,5);flowerDraws=0;
            Parallel.For(0,18,n=>{if(n%3==0)EntityTable.SaveEntity(p);else FlowerWire(p,6);});
            var seed=p.Inventory.Objects.Single();uint uid=seed.UID;
            Check(!Fenced(p) && FlowerChanges(p)==1 && flowerDraws==1 && p.Entity.ConquerPoints==999 && seed.ID==729306+grade && seed.Bound==bound
                && seed.UID!=source.UID && seed.StackSize==1 && !p.Inventory.ContainsUID(source.UID),"ordinary paid change draws and charges once under concurrent replay: "+grade+"/"+bound);
            p=ReloadBall(p);p.BindWorldMap(1806);FlowerStand(p);FlowerWire(p,6);
            Check(!Fenced(p) && FlowerChanges(p)==1 && p.Entity.ConquerPoints==999 && p.Inventory.Objects.Single().UID==uid,"reconnect retains new seed and cannot replay old payment");
            seed=p.Inventory.Objects.Single();ulong xp=p.Entity.Experience,award=p.ExpBall*(uint)minutes[grade]/60;ushort study=p.Entity.SubClasses.StudyPoints;
            var bed=FlowerTile(garden,98,111);AdventureWalk(p,bed.Item1,bed.Item2);ItemHandler.UseItem(seed,p);EntityTable.SaveEntity(p);p=ReloadBall(p);
            Check(!Fenced(p) && Flowers(p)==1 && p.Entity.Experience==xp+award && p.Entity.SubClasses.StudyPoints==study+studies[grade]
                && p.Entity.ConquerPoints==999+Config.DailyQuestCp && p.Entity.RevoraPoint==Config.DailyQuestRevoraPoints
                && p.Inventory.Contains(Config.DailyQuestChiToken,1) && p.Inventory.Objects.Single(i=>i.ID==Config.DailyQuestChiToken).Bound==bound,
                "earned grade "+grade+" walks to the bed and persists its exact EXP, study and whole daily bundle: bound="+bound);
        }
        var direct=GardenPlayer(garden);var normal=FlowerSeed(direct);FlowerWire(direct,5);flowerDraws=0;
        Parallel.For(0,12,n=>FlowerWire(direct,7));
        Check(!Fenced(direct) && FlowerChanges(direct)==1 && flowerDraws==0 && direct.Entity.ConquerPoints==900 && direct.Inventory.Objects.Single().ID==729310
            && !direct.Inventory.ContainsUID(normal.UID),"100 CP direct Super replacement is deterministic and charges once");
        FlowerWire(direct,5);FlowerWire(direct,6);FlowerWire(direct,7);Check(FlowerChanges(direct)==1 && direct.Entity.ConquerPoints==900,"Super seed cannot be downgraded or charged again");
        var bought=GardenPlayer(garden);FlowerSeed(bought,true);FlowerBed(bought);ulong before=bought.Entity.Experience,ball=bought.ExpBall;ItemHandler.UseItem(bought.Inventory.Objects.Single(),bought);
        Check(!Fenced(bought) && Flowers(bought)==1 && bought.Entity.ConquerPoints==900+Config.DailyQuestCp && bought.Entity.Experience==before+ball*5
            && bought.Entity.SubClasses.StudyPoints==300,"fresh Super purchase earns the same grade-five reward without a second seed fee");
        var old=GardenPlayer(garden);var oldSeed=FlowerSeed(old,true);
        Query("UPDATE revora_magnolia_runs SET rules_version=0,exp_minutes=60,study=10,fee=500 WHERE uid=@uid","uid",(long)old.Entity.UID);
        Call(Magnolia,null,"Initialize");FlowerBed(old);before=old.Entity.Experience;ball=old.ExpBall;ItemHandler.UseItem(oldSeed,old);
        Check(!Fenced(old) && Flowers(old)==1 && old.Entity.Experience==before+ball && old.Entity.SubClasses.StudyPoints==10
            && Scalar("SELECT fee FROM revora_magnolia_runs WHERE uid=@uid",old.Entity.UID)==500,"pre-upgrade daily records retain their promised study, EXP and recorded price");
        var full=GardenPlayer(garden);FlowerSeed(full);while(full.Inventory.Count<40)Pack(full,1000000,1);FlowerWire(full,5);flowerRoll=95;FlowerWire(full,6);
        Check(!Fenced(full) && FlowerChanges(full)==1 && full.Inventory.Count==40 && full.Inventory.Objects.Any(i=>i.ID==729309),"full bag exchanges exactly the seed's released slot");
        var carried=GardenPlayer(garden);FlowerSeed(carried);flowerNow=flowerNow.AddDays(1);FlowerWire(carried,5);flowerRoll=80;FlowerWire(carried,6);
        Check(!Fenced(carried) && FlowerRuns(carried)==2 && FlowerChanges(carried)==1,"carried seed change creates today's one durable run");flowerNow=flowerNow.AddDays(-1);
        foreach(string fault in new[]{"source","balance","replacement","terms","receipt","random"}){
            var p=GardenPlayer(garden);var seed=FlowerSeed(p);FlowerWire(p,5);flowerRoll=fault=="random"?100:80;
            string table=fault=="source" || fault=="replacement"?"items":fault=="balance"?"entities":fault=="terms"?"revora_magnolia_runs":"revora_magnolia_changes";
            string verb=fault=="source"?"DELETE":fault=="balance" || fault=="terms"?"UPDATE":"INSERT";
            if(fault!="random")Query("CREATE FUNCTION suppress_seed_change() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_seed_change BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_seed_change()");
            FlowerWire(p,6);
            Check(Fenced(p) && FlowerChanges(p)==0 && p.Entity.ConquerPoints==1000 && p.Inventory.Count==1 && p.Inventory.ContainsUID(seed.UID)
                && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==1000 && SavedRune(seed,"id")==729306,
                "suppressed "+fault+" preserves original seed, CPs, terms and empty receipt");
            if(fault!="random")Query("DROP TRIGGER suppress_seed_change ON "+table+";DROP FUNCTION suppress_seed_change()");
            p=ReloadBall(p);p.BindWorldMap(1806);FlowerStand(p);FlowerWire(p,5);flowerRoll=80;FlowerWire(p,6);
            Check(!Fenced(p) && FlowerChanges(p)==1 && p.Entity.ConquerPoints==999 && p.Inventory.Objects.Single().ID==729308,"seed change retries after "+fault+" rollback and production reload");
        }
        foreach(string guard in new[]{"no-review","expired-review","new-day","clock-back","remote","dead","fake","trade","locked","stack","timed","foreign-echo","completed","below-level","stale-bag","stale-balance","stale-seed"}){
            var p=GardenPlayer(garden);var seed=FlowerSeed(p);if(guard!="no-review")FlowerWire(p,5);DateTime saved=flowerNow;flowerDraws=0;
            if(guard=="expired-review")flowerNow=flowerNow.AddMinutes(2);if(guard=="new-day")flowerNow=flowerNow.AddDays(1);if(guard=="clock-back")flowerNow=flowerNow.AddSeconds(-1);
            if(guard=="remote")p.Entity.X=1;if(guard=="dead")p.Entity.Hitpoints=0;if(guard=="fake")p.Fake=true;if(guard=="trade")p.Trade.InTrade=true;
            if(guard=="locked")seed.Lock=1;if(guard=="stack")seed.StackSize=2;if(guard=="timed")seed.Days=1;
            if(guard=="below-level")p.Entity.Level=79;
            if(guard=="completed")Query("UPDATE revora_magnolia_runs SET completed=true WHERE uid=@uid","uid",(long)p.Entity.UID);
            if(guard=="stale-bag"){var junk=Pack(p,1000000,1);Query("DELETE FROM items WHERE uid=@uid","uid",(long)junk.UID);}
            if(guard=="stale-balance")Query("UPDATE entities SET conquerpoints=999 WHERE uid=@uid","uid",(long)p.Entity.UID);
            if(guard=="stale-seed")Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)seed.UID);
            FlowerWire(p,6,guard=="foreign-echo"?123u:44100u);flowerNow=saved;
            Check(FlowerChanges(p)==0 && flowerDraws==0 && p.Entity.ConquerPoints==1000 && p.Inventory.ContainsUID(seed.UID),"ineligible seed change never draws, charges or consumes: "+guard);
            if(guard.StartsWith("stale-"))Check(Fenced(p),"stale durable seed input fences for reload: "+guard);
        }
        var wire=GardenPlayer(garden);FlowerSeed(wire);FlowerWire(wire,5);flowerDraws=0;
        foreach(string bad in new[]{"short","long","trailer","input"}){
            int length=bad=="short"?18:bad=="long"?121:20;var frame=new byte[length+8+(bad=="trailer"?1:0)];
            Writer.WriteUInt16((ushort)length,0,frame);Writer.WriteUInt16(2032,2,frame);frame[14]=6;if(bad=="input")frame[17]=20;
            Writer.WriteUInt64(PacketHandler.ClientSeal,length,frame);PacketHandler.HandlePacket(frame,wire);
            Check(!Fenced(wire) && FlowerChanges(wire)==0 && flowerDraws==0,"bounded Magnolia continuation rejects "+bad);
        }
        FlowerWire(wire,6,0);Check(!Fenced(wire) && FlowerChanges(wire)==1,"valid zero-echo seed change remains usable after malformed requests");
        var alternate=GardenPlayer(garden);FlowerSeed(alternate);var foreign=Pack(alternate,729310,1);FlowerBed(alternate);ItemHandler.UseItem(foreign,alternate);
        Check(!Fenced(alternate) && Flowers(alternate)==0 && alternate.Inventory.ContainsUID(foreign.UID),"new daily contract cannot be redeemed with a substituted grade or UID");
        var frozen=GardenPlayer(garden);FlowerSeed(frozen);FlowerWire(frozen,5);uint original=Config.DailyMagnoliaSuperSeedCp;
        try{Config.DailyMagnoliaSuperSeedCp=500;FlowerWire(frozen,7);}finally{Config.DailyMagnoliaSuperSeedCp=original;}
        Check(!Fenced(frozen) && FlowerChanges(frozen)==1 && frozen.Entity.ConquerPoints==900,"confirmed Super replacement uses the displayed price even if configuration changes");
        VerifyMagnoliaBonuses(garden,runtime);
    }
    static void VerifyFlowerAdmission(Map garden) {
        Call(FlowerStore,null,"Initialize");FlowerStore.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>flowerNow));
        var sender=GardenPlayer(garden);var recipient=GardenPlayer(garden);
        sender.Entity.Body=1003;recipient.Entity.Body=2001;
        sender.Entity.MyFlowers=new Revora.Game.Features.Flowers(sender.Entity.UID,"fixture-sender");
        recipient.Entity.MyFlowers=new Revora.Game.Features.Flowers(recipient.Entity.UID,"fixture-recipient");
        foreach(uint id in new uint[]{1000000,1088001}) {
            var item=Pack(sender,id,1);var frame=new SendFlower().ToArray();
            Writer.WriteUInt32(recipient.Entity.UID,8,frame);Writer.WriteUInt32(item.UID,12,frame);Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);
            Revora.Game.Features.Flowers.SendFlower(sender,frame);
            Check(sender.Inventory.ContainsUID(item.UID) && SavedRune(item,"id")==id && recipient.Entity.MyFlowers.RedRoses==0,
                "non-gift item cannot be consumed as a rose: "+id);
        }
        var gift=Pack(sender,753003,1);
        foreach(string bad in new[]{"null","short","overlong","length","id","seal","action-alias"}) {
            var frame=bad=="short"?new byte[23]:bad=="overlong"?new byte[129]:new SendFlower().ToArray();
            Writer.WriteUInt16((ushort)(frame.Length-8),0,frame);Writer.WriteUInt16(1150,2,frame);
            Writer.WriteUInt32(recipient.Entity.UID,8,frame);Writer.WriteUInt32(gift.UID,12,frame);
            Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);
            if(bad=="length")Writer.WriteUInt16(1,0,frame);if(bad=="id")Writer.WriteUInt16(1151,2,frame);
            if(bad=="seal")frame[frame.Length-1]^=1;if(bad=="action-alias")Writer.WriteUInt32(256,4,frame);
            Revora.Game.Features.Flowers.SendFlower(sender,bad=="null"?null:frame);
            Check(sender.Inventory.ContainsUID(gift.UID) && SavedRune(gift,"id")==753003 && recipient.Entity.MyFlowers.Orchads==0,
                "malformed flower request preserves owned gift and recipient counters: "+bad);
        }
        foreach(uint type in new uint[]{753003,753009,754001,754003,754009,756001,756003,756009}) {
            bool letter=type/1000==756;
            sender.Entity.Body=letter?(ushort)1003:(ushort)2001;recipient.Entity.Body=letter?(ushort)2001:(ushort)1003;
            var wrong=Pack(sender,type,1);var frame=new SendFlower().ToArray();
            Writer.WriteUInt32(letter?0u:1u,4,frame);Writer.WriteUInt32(recipient.Entity.UID,8,frame);Writer.WriteUInt32(wrong.UID,12,frame);
            uint lilies=recipient.Entity.MyFlowers.Lilies,orchids=recipient.Entity.MyFlowers.Orchads,tulips=recipient.Entity.MyFlowers.Tulips;
            Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);Revora.Game.Features.Flowers.SendFlower(sender,frame);
            // The rejected direction must also preserve the recipient's prior gifts.
            Check(sender.Inventory.ContainsUID(wrong.UID) && SavedRune(wrong,"id")==type
                && recipient.Entity.MyFlowers.Lilies==lilies && recipient.Entity.MyFlowers.Orchads==orchids && recipient.Entity.MyFlowers.Tulips==tulips,
                "Magnolia gift cannot be consumed as the opposite recipient's counter family: "+type);
            sender.Entity.Body=letter?(ushort)2001:(ushort)1003;recipient.Entity.Body=letter?(ushort)1003:(ushort)2001;
            Writer.WriteUInt32(letter?1u:0u,4,frame);Revora.Game.Features.Flowers.SendFlower(sender,frame);
            Check(!sender.Inventory.ContainsUID(wrong.UID) && Scalar("SELECT count(*) FROM items WHERE uid=@uid",wrong.UID)==0
                && recipient.Entity.MyFlowers.Lilies==lilies+(letter?type%1000:0)
                && recipient.Entity.MyFlowers.Orchads==orchids+(type/1000==753?type%1000:0)
                && recipient.Entity.MyFlowers.Tulips==tulips+(type/1000==754?type%1000:0),
                "supported gift direction preserves the existing exact in-memory delivery amount: "+type);
        }
    }
    static void VerifyEarnedMagnoliaGift(GameState p,uint type,bool bound) {
        bool pack=type==723714 || type==720027;
        Type service=pack?QuestGifts:type==723017?ExpPotions:Prayers;
        string table=pack?"revora_quest_gift_openings":type==723017?"revora_exp_potion_uses":"revora_praying_stone_uses";
        Call(service,null,"Initialize");service.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>flowerNow));
        var source=p.Inventory.Objects.Single(i=>i.ID==type);uint uid=source.UID;
        ulong silver=p.Entity.Money;uint blessing=p.Entity.HeavenBlessing;
        var beforeItems=p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i).ToArray();
        Query("CREATE FUNCTION fail_earned_gift() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER fail_earned_gift BEFORE INSERT ON "+table+" FOR EACH ROW EXECUTE FUNCTION fail_earned_gift()");
        try{OpenGift(p,source);}finally{Query("DROP TRIGGER fail_earned_gift ON "+table+";DROP FUNCTION fail_earned_gift()");}
        Check(Fenced(p) && Scalar("SELECT count(*) FROM "+table+" WHERE uid=@uid",p.Entity.UID)==0,
            "earned gift receipt failure fences without claiming delivery: "+type);
        p=ReloadBall(p);
        Check(!Fenced(p) && p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i).SequenceEqual(beforeItems)
            && p.Inventory.Objects.Single(i=>i.UID==uid).Bound==bound && p.Entity.Money==silver
            && p.Entity.HeavenBlessing==blessing && p.Entity.DoubleExperienceTime==0 && Flowers(p)==1,
            "failed earned gift reload preserves original item, planting and all reward balances: "+type);
        source=p.Inventory.Objects.Single(i=>i.UID==uid);OpenGift(p,source);ItemHandler.UseItem(source,p);
        Check(!Fenced(p) && !p.Inventory.ContainsUID(uid) && Scalar("SELECT count(*) FROM "+table+" WHERE uid=@uid",p.Entity.UID)==1,
            "ordinary earned gift retry consumes exactly its source once: "+type);
        EntityTable.SaveEntity(p);p=ReloadBall(p);OpenGift(p,source);
        Check(!Fenced(p) && !p.Inventory.ContainsUID(uid) && Flowers(p)==1
            && Scalar("SELECT count(*) FROM "+table+" WHERE uid=@uid",p.Entity.UID)==1
            && p.Entity.Money==silver+(type==723714?800000u:0u)
            && p.Entity.HeavenBlessing==blessing+(type==720578?604800u:0u)
            && p.Entity.DoubleExperienceTime==(type==723017?3600:0)
            && p.Inventory.Objects.Count(i=>i.ID==1088001)==(type==720027?10:0)
            && p.Inventory.Objects.Where(i=>i.ID==1088001).All(i=>i.Bound==bound && i.StackSize==1),
            "earned gift use, autosave and cold replay retain exact silver, timer, blessing or ten bound-aware Meteors: "+type);
    }
    static void VerifyCustomMagnoliaPack(GameState p,uint type,bool bound) {
        Call(QuestGifts,null,"Initialize");QuestGifts.GetField("SoulRoll",Hidden).SetValue(null,(Func<int,int>)(max=>max-1));
        var beforeSouls=p.Inventory.Objects.Select(i=>i.UID).ToArray();uint cp=p.Entity.ConquerPoints,rvp=p.Entity.RevoraPoint;var pack=p.Inventory.Objects.Single(i=>i.ID==type);
        Query(@"CREATE FUNCTION refuse_custom_pack() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;
CREATE TRIGGER refuse_custom_pack BEFORE INSERT ON revora_quest_gift_openings FOR EACH ROW EXECUTE FUNCTION refuse_custom_pack();");
        try {OpenGift(p,pack);Check(Fenced(p) && p.Inventory.ContainsUID(pack.UID) && p.Entity.ConquerPoints==cp && p.Entity.RevoraPoint==rvp
            && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==cp && GiftReceipts(p)==0,
            "custom Magnolia pack late failure preserves source and both currency balances");}
        finally{Query("DROP TRIGGER refuse_custom_pack ON revora_quest_gift_openings;DROP FUNCTION refuse_custom_pack()");}
        p=ReloadBall(p);pack=p.Inventory.Objects.Single(i=>i.ID==type);
        Parallel.For(0,12,i=>{if(i%3==0)EntityTable.SaveEntity(p);else OpenGift(p,pack);});
        var soul=p.Inventory.Objects.Single(i=>!beforeSouls.Contains(i.UID) && i.ID>=800000 && i.ID<900000 && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7);
        Check(!Fenced(p) && !p.Inventory.ContainsUID(pack.UID) && soul.Bound && p.Entity.ConquerPoints==cp+5000 && p.Entity.RevoraPoint==rvp+1000
            && GiftReceipts(p)==1,"earned custom Magnolia pack grants one bound-aware P7 and exact CP/RVP rewards once");
        uint soulUid=soul.UID;p=ReloadBall(p);ItemHandler.UseItem(pack,p);Call(QuestGifts,null,"Initialize");
        Check(p.Inventory.ContainsUID(soulUid) && p.Entity.ConquerPoints==cp+5000 && p.Entity.RevoraPoint==rvp+1000 && GiftReceipts(p)==1,
            "custom pack reward survives cold reload, replay and schema reinitialization");
    }
    static void VerifyMagnoliaBonuses(Map garden,string runtime) {
        uint[][] expected={new uint[0],new uint[]{1088001,700001,700011,700021,700031,700041,700051,700061,700071,700101,700121},
            new uint[]{723017,753003,754001,756001,723903},
            new uint[]{720128,723714,753009,754003,756003,700003,700013,700023,700033,700043,700053,700063,700073,700103,700123,721090,721269},
            new uint[]{1088000,720027,720578,721259,754009,756009,710212,720957}};
        int[] gradeRoll={0,50,80,95,99};
        for(int grade=1;grade<5;grade++)for(int slot=0;slot<expected[grade].Length;slot++) {
            int chosen=slot;Magnolia.GetField("BonusRoll",Hidden).SetValue(null,(Func<int,int>)(max=>max==100?19:chosen));
            var p=GardenPlayer(garden);
            if(expected[grade][slot]/1000>=753 && expected[grade][slot]/1000<=756){p.Entity.Body=expected[grade][slot]/1000==756?(ushort)2001:(ushort)1003;EntityTable.SaveEntity(p);}
            if(expected[grade][slot]==721090){p.Entity.Class=14;EntityTable.SaveEntity(p);}
            if(expected[grade][slot]==721259){p.Entity.Class=15;p.Entity.Level=130;p.Entity.Body=1003;EntityTable.SaveEntity(p);}
            var first=FlowerSeed(p);flowerRoll=gradeRoll[grade];FlowerWire(p,5);FlowerWire(p,6);
            var seed=p.Inventory.Objects.Single();bool bound=slot%2==0;seed.Bound=bound;
            Query("UPDATE items SET bound=@bound WHERE uid=@uid","bound",bound?1:0,"uid",(long)seed.UID);
            if(expected[grade][slot]==720128){p.Entity.PKPoints=100;EntityTable.SaveEntity(p);}
            var bed=FlowerTile(garden,98,111);AdventureWalk(p,bed.Item1,bed.Item2);ItemHandler.UseItem(seed,p);
            uint type=expected[grade][slot];int count=type==723903?5:1;
            Check(!Fenced(p) && Flowers(p)==1 && p.Inventory.Objects.Count(i=>i.ID==type && i.Bound==bound)==count
                && Scalar("SELECT item_type FROM revora_magnolia_bonuses WHERE uid=@uid AND roll=19",p.Entity.UID)==type,
                "ordinary earned Magnolia grade "+grade+" bonus slot "+slot+" has exact identity, quantity and binding");
            ItemHandler.UseItem(seed,p);p=ReloadBall(p);
            Check(!Fenced(p) && p.Inventory.Objects.Count(i=>i.ID==type)==count && Flowers(p)==1,
                "earned Magnolia bonus persists through production reload and cannot replay");
            if(type/1000>=753 && type/1000<=756)VerifyEarnedFlower(p,type,garden);
            if(type==721269 || type==720957)VerifyCustomMagnoliaPack(p,type,bound);
            if(type==710212)VerifyEarnedLottery(p,runtime,bound);
            if(type==723714 || type==720027 || type==723017 || type==720578)VerifyEarnedMagnoliaGift(p,type,bound);
            if(type==720128) {
                Call(Amulets,null,"Initialize");Amulets.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>flowerNow));
                OpenGift(p,p.Inventory.Objects.Single(i=>i.ID==type));p=ReloadBall(p);
                Check(!Fenced(p) && p.Entity.PKPoints==70 && AmuletUses(p)==1 && !p.Inventory.Objects.Any(i=>i.ID==type),
                    "ordinary planting-to-amulet-to-PK-reduction journey survives cold reload");
            }
            if(type==721090) {
                var job=World(runtime,1004);MiningNpc(job,10022);var trainer=job.Npcs[10022];
                p.Entity.Teleport(1004,(ushort)(trainer.X-1),trainer.Y);p.ActiveNpc=10022;EntityTable.SaveEntity(p);
                PromotionPrepare(p);PromotionNpc(p,3);p=ReloadBall(p);new TitleStorage().Login(p);
                Check(!Fenced(p) && p.Entity.Class==15 && !p.Inventory.Objects.Any(i=>i.ID==721090)
                    && PromotionCount(p)==1 && !HasAchievement(p,11022),
                    "earned Magnolia Moon Box funds promotion and reload without granting the unrelated Moon Box quest achievement");
            }
            if(type==721259) {
                var town=Kernel.Maps[1002];var guide=town.Npcs[59558];
                p.Entity.Teleport(1002,(ushort)(guide.X-2),(ushort)(guide.Y-2));p.ActiveNpc=59558;
                EntityTable.SaveEntity(p);LoadAppearance(p);SkillTable.LoadProficiencies(p);
                uint stone=p.Inventory.Objects.Single(i=>i.ID==type).UID;
                RebirthOffer(p);StoneNpc(p,214);StoneNpc(p,214);p=ReloadBall(p);new TitleStorage().Login(p);
                Check(!Fenced(p) && p.Entity.Reborn==1 && p.Entity.FirstRebornClass==15 && p.Entity.Level==15
                    && Rebirths(p)==1 && !p.Inventory.ContainsUID(stone) && Flowers(p)==1
                    && p.Inventory.Objects.Count(i=>i.ID==700003 && i.Bound==bound)==1
                    && p.Inventory.Objects.Count(i=>i.ID==410077 && i.Bound==bound)==1
                    && HasAchievement(p,11030) && HasAchievement(p,10315) && !HasAchievement(p,11031),
                    "earned Magnolia Celestial Stone pays one first rebirth with bound-aware gifts and durable achievements");
            }
        }
        foreach(int roll in new[]{20,99}) {
            Magnolia.GetField("BonusRoll",Hidden).SetValue(null,(Func<int,int>)(max=>roll));
            var p=GardenPlayer(garden);var seed=FlowerSeed(p,true);FlowerBed(p);ItemHandler.UseItem(seed,p);
            Check(!Fenced(p) && Flowers(p)==1 && p.Inventory.Objects.Count()==2
                && Scalar("SELECT count(*) FROM revora_magnolia_bonuses WHERE uid=@uid AND item_type=0",p.Entity.UID)==1,
                "no-bonus outcome still pays the full daily base reward at roll "+roll);VerifyDailySoul(p,"magnolia");
        }
        var blocked=GardenPlayer(garden);FlowerSeed(blocked);flowerRoll=80;FlowerWire(blocked,5);FlowerWire(blocked,6);
        var blockedSeed=blocked.Inventory.Objects.Single();while(blocked.Inventory.Count<35)Pack(blocked,1000000,1);
        int draws=0;Magnolia.GetField("BonusRoll",Hidden).SetValue(null,(Func<int,int>)(max=>{draws++;return max==100?0:4;}));
        FlowerBed(blocked);ItemHandler.UseItem(blockedSeed,blocked);
        Check(!Fenced(blocked) && Flowers(blocked)==0 && draws==0 && blocked.Inventory.ContainsUID(blockedSeed.UID),
            "capacity is checked before any draw so a full bag cannot reroll bonus outcomes");
        DisRemove(blocked,blocked.Inventory.Objects.First(i=>i.ID==1000000));ItemHandler.UseItem(blockedSeed,blocked);
        Check(!Fenced(blocked) && Flowers(blocked)==1 && blocked.Inventory.Count==40 && blocked.Inventory.Objects.Count(i=>i.ID==723903)==5,
            "exact capacity delivers all five earned saddles and Chi Token");
        var failed=GardenPlayer(garden);var failedSeed=FlowerSeed(failed,true);uint cp=failed.Entity.ConquerPoints;
        Magnolia.GetField("BonusRoll",Hidden).SetValue(null,(Func<int,int>)(max=>0));FlowerBed(failed);
        Query("CREATE FUNCTION suppress_flower_bonus() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_flower_bonus BEFORE INSERT ON revora_magnolia_bonuses FOR EACH ROW EXECUTE FUNCTION suppress_flower_bonus()");
        try {ItemHandler.UseItem(failedSeed,failed);}finally{Query("DROP TRIGGER suppress_flower_bonus ON revora_magnolia_bonuses;DROP FUNCTION suppress_flower_bonus()");}
        Check(Fenced(failed) && Flowers(failed)==0 && failed.Inventory.ContainsUID(failedSeed.UID) && failed.Entity.ConquerPoints==cp
            && Scalar("SELECT count(*) FROM items WHERE entityid=@uid",failed.Entity.UID)==1,
            "failed bonus receipt rolls back seed, base reward, bonus and daily completion together");
        var legacy=GardenPlayer(garden);var legacySeed=FlowerSeed(legacy,true);
        Query("UPDATE revora_magnolia_runs SET bonus_version=0,valuable_reward=true,rune_reward=false WHERE uid=@uid","uid",(long)legacy.Entity.UID);
        Magnolia.GetField("BonusRoll",Hidden).SetValue(null,(Func<int,int>)(max=>{throw new Exception("Unexpected legacy draw");}));
        FlowerBed(legacy);ItemHandler.UseItem(legacySeed,legacy);
        Check(!Fenced(legacy) && Flowers(legacy)==1 && legacy.Inventory.Count==1
            && Scalar("SELECT count(*) FROM revora_magnolia_bonuses WHERE uid=@uid",legacy.Entity.UID)==0,
            "existing daily contracts retain their pre-bonus terms without a retroactive lottery");
        var normal=GardenPlayer(garden);var normalSeed=FlowerSeed(normal);FlowerBed(normal);ItemHandler.UseItem(normalSeed,normal);
        Check(!Fenced(normal) && Flowers(normal)==1 && normal.Inventory.Count==2
            && Scalar("SELECT count(*) FROM revora_magnolia_bonuses WHERE uid=@uid AND grade=0 AND roll=-1 AND item_type=0",normal.Entity.UID)==1,
            "new Normal seeds receive no bonus and consume no random draw");
        var obsolete=GardenPlayer(garden);var unchanged=FlowerSeed(obsolete);FlowerWire(obsolete,5);FlowerWire(obsolete,8);
        Check(!Fenced(obsolete) && FlowerChanges(obsolete)==0 && obsolete.Inventory.ContainsUID(unchanged.UID),
            "removed pre-6711 Study Point seed payment remains unavailable");
        var prior=GardenPlayer(garden);FlowerSeed(prior);flowerRoll=95;FlowerWire(prior,5);FlowerWire(prior,6);
        Query("UPDATE revora_magnolia_runs SET bonus_version=1 WHERE uid=@uid","uid",(long)prior.Entity.UID);
        Magnolia.GetField("BonusRoll",Hidden).SetValue(null,(Func<int,int>)(max=>{if(max==100)return 0;if(max!=15)throw new Exception("Old bonus pool changed");return 14;}));
        FlowerBed(prior);ItemHandler.UseItem(prior.Inventory.Objects.Single(),prior);
        Check(!Fenced(prior) && Flowers(prior)==1 && prior.Inventory.Objects.Any(i=>i.ID==700123)
            && !prior.Inventory.Objects.Any(i=>i.ID==721090),"version-one issued seeds retain their original fifteen-entry Elite pool");
        var oldSuper=GardenPlayer(garden);var oldSuperSeed=FlowerSeed(oldSuper,true);
        Query("UPDATE revora_magnolia_runs SET bonus_version=2 WHERE uid=@uid","uid",(long)oldSuper.Entity.UID);
        Magnolia.GetField("BonusRoll",Hidden).SetValue(null,(Func<int,int>)(max=>{if(max==100)return 0;if(max!=6)throw new Exception("Old Super pool changed");return 5;}));
        FlowerBed(oldSuper);ItemHandler.UseItem(oldSuperSeed,oldSuper);
        Check(!Fenced(oldSuper) && Flowers(oldSuper)==1 && oldSuper.Inventory.Objects.Any(i=>i.ID==756009)
            && !oldSuper.Inventory.Objects.Any(i=>i.ID==710212),"version-two Super seeds retain their six-entry pool without retroactive lottery tickets");
        VerifyMagnoliaWrangler(garden);
        VerifyFreeFlowers(garden);
        Magnolia.GetField("BonusRoll",Hidden).SetValue(null,(Func<int,int>)(max=>99));
    }
    static void VerifyEarnedLottery(GameState p,string runtime,bool bound){
        PrepareLotteryFixture();Draws.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>flowerNow));
        Call(QuestGifts,null,"Initialize");QuestGifts.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>flowerNow));
        var town=World(runtime,1002);MiningNpc(town,923);var lady=town.Npcs[923];
        var room=World(runtime,700);MiningNpc(room,925);
        p.Entity.Teleport(1002,(ushort)(lady.X-2),(ushort)(lady.Y-2));AdventureNear(p,923);LotteryExchange(p);p=ReloadBall(p);AdventureNear(p,923);
        Check(!Fenced(p) && p.Inventory.Objects.Count(i=>i.ID==711504 && i.Bound==bound)==3 && !p.Inventory.Objects.Any(i=>i.ID==710212),
            "actual planted Magnolia ticket exchanges into three matching-bound tickets and survives reload");
        p.ActiveNpc=923;Npcs.GetDialog(new NpcRequest(true){NpcID=923,OptionID=1},p);AdventureNear(p,925);
        Query("CREATE FUNCTION reject_earned_lottery() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER reject_earned_lottery BEFORE INSERT ON revora_lottery_steps FOR EACH ROW EXECUTE FUNCTION reject_earned_lottery()");
        try{OpenBox(p,925);}finally{Query("DROP TRIGGER reject_earned_lottery ON revora_lottery_steps;DROP FUNCTION reject_earned_lottery()");}
        Check(Fenced(p) && DrawCount(p)==0 && p.Inventory.Objects.Count(i=>i.ID==711504)==3,"failed earned-ticket draw preserves all three paid tickets and creates no pending prize");
        p=ReloadBall(p);AdventureNear(p,925);OpenBox(p,925);p=ReloadBall(p);AdventureNear(p,925);OpenBox(p,925);
        Check(!Fenced(p) && DrawCount(p)==1 && !p.Inventory.Objects.Any(i=>i.ID==711504),"earned-ticket retry and reconnect restore exactly one paid draw");
        LotteryAction(p,0);LotteryAction(p,0);EntityTable.SaveEntity(p);p=ReloadBall(p);new TitleStorage().Login(p);
        uint prizeUid=checked((uint)Scalar("SELECT item_uid FROM revora_lottery_draws WHERE uid=@uid AND claimed",p.Entity.UID));
        Check(!Fenced(p) && p.Inventory.Objects.Any(i=>i.UID==prizeUid && i.ID==LotteryTable.LotteryItems[0].ID && i.Bound==bound)
            && Flowers(p)==1 && GiftReceipts(p)==1 && DrawCount(p)==1 && HasAchievement(p,10118),
            "Magnolia planting through NPC exchange, paid draw, reconnect and once-only prize claim retains all receipts");
    }
    static void VerifyMagnoliaWrangler(Map garden) {
        // Magnolia's isolated setup previously had no subclass table.
        Query("CREATE TABLE subclasses (LIKE public.subclasses INCLUDING ALL)");
        DateTime start=flowerNow;var p=ReloadBall(GardenPlayer(garden));
        Magnolia.GetField("BonusRoll",Hidden).SetValue(null,(Func<int,int>)(max=>max==100?0:4));
        try {
            for(int day=0;day<8;day++) {
                // Complete the login stat projection before quest EXP can level
                // the character and refill HP from the calculated maximum.
                p.LoadItemStats();p.CalculateStatBonus();p.CalculateHPBonus();
                flowerNow=start.AddDays(day);p.Entity.Teleport(1806,102,105);FlowerStand(p);
                FlowerSeed(p);flowerRoll=80;FlowerWire(p,5);FlowerWire(p,6);
                var seed=p.Inventory.Objects.Single(i=>i.ID==729308);
                var bed=FlowerTile(garden,98,111);AdventureWalk(p,bed.Item1,bed.Item2);ItemHandler.UseItem(seed,p);
                Check(!Fenced(p) && Flowers(p)==day+1,"daily earned saddle bundle commits on distinct day "+(day+1));
                p.Entity.Teleport(1002,317,322);
                foreach(var item in p.Inventory.Objects.Where(i=>i.ID==729304 || i.ID==4030101 || i.ID>=800000 && i.ID<900000 || day==0 && i.ID==723903).ToArray())
                    Warehouse.Process(StorageFrame(1,item.UID),p);
                EntityTable.SaveEntity(p);p=ReloadBall(p);
                Check(!Fenced(p) && p.Inventory.Objects.Count(i=>i.ID==723903)==day*5
                    && p.Warehouses[Store.WarehouseID.TwinCity].Objects.Count(i=>i.ID==723903)==5,
                    "ordinary warehouse handling and reload preserve all accumulated earned saddles on day "+(day+1));
            }
            foreach(var saddle in p.Warehouses[Store.WarehouseID.TwinCity].Objects.Where(i=>i.ID==723903).ToArray())
                Warehouse.Process(StorageFrame(2,saddle.UID),p);
            Check(p.Inventory.Count==40 && p.Inventory.Objects.All(i=>i.ID==723903),"eight Magnolia gifts fund all forty Wrangler saddles without supplied materials");
            SubRequest(p,9,9);SubRequest(p,9,9);
            Check(!Fenced(p) && p.Entity.SubClasses.Classes.ContainsKey(9),"earned saddles enroll the ordinary player through the subclass dispatcher: level="+p.Entity.Level+" dead="+p.Entity.Dead+" trade="+(p.Trade==null?"null":p.Trade.InTrade.ToString())+" fenced="+Fenced(p)+" bagUnits="+p.Inventory.Objects.Where(i=>i.ID==723903 && i.Position==0).Sum(i=>Math.Max(1,(int)i.StackSize)));
            p=ReloadBall(p);SubClassTable.Load(p.Entity);new TitleStorage().Login(p);
            Check(!Fenced(p) && p.Entity.SubClasses.Classes[9].Level==1 && p.Entity.SubClasses.Classes[9].Phase==1
                && !p.Inventory.Objects.Any(i=>i.ID==723903) && Flowers(p)==8 && HasAchievement(p,11201) && !HasAchievement(p,11202),
                "earned forty-saddle enrollment persists once with P1 evidence and no unearned P9");
        }finally{flowerNow=start;}
    }
    static readonly Type FlowerStore=typeof(Entity).Assembly.GetType("Revora.Game.Features.FlowerDelivery");
    static byte[] FlowerRequest(GameState sender,GameState receiver,uint item) {
        var frame=new SendFlower().ToArray();Writer.WriteUInt32(sender.Entity.Body>=2000?1u:0u,4,frame);
        Writer.WriteUInt32(receiver.Entity.UID,8,frame);Writer.WriteUInt32(item,12,frame);Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);return frame;
    }
    static long Deliveries(GameState p){return Scalar("SELECT count(*) FROM revora_flower_deliveries WHERE sender=@uid",p.Entity.UID);}
    static void VerifyEarnedFlower(GameState sender,uint type,Map garden) {
        var receiver=GardenPlayer(garden);receiver.Entity.Body=type/1000==756?(ushort)1003:(ushort)2001;EntityTable.SaveEntity(receiver);receiver=ReloadBall(receiver);
        var item=sender.Inventory.Objects.Single(i=>i.ID==type);var frame=FlowerRequest(sender,receiver,item.UID);
        Query("CREATE FUNCTION reject_flower() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER reject_flower BEFORE INSERT ON revora_flower_deliveries FOR EACH ROW EXECUTE FUNCTION reject_flower()");
        try{Revora.Game.Features.Flowers.SendFlower(sender,frame);}finally{Query("DROP TRIGGER reject_flower ON revora_flower_deliveries;DROP FUNCTION reject_flower()");}
        Check(Fenced(sender) && Fenced(receiver) && Deliveries(sender)==0 && SavedRune(item,"id")==type,
            "failed earned flower receipt retains item and rejects partial credit: "+type);
        sender=ReloadBall(sender);receiver=ReloadBall(receiver);
        Check(sender.Inventory.ContainsUID(item.UID) && receiver.Entity.MyFlowers.Lilies==0 && receiver.Entity.MyFlowers.Orchads==0 && receiver.Entity.MyFlowers.Tulips==0,
            "recipient and source reload agree after failed earned delivery: "+type);
        Parallel.For(0,12,n=>Revora.Game.Features.Flowers.SendFlower(sender,frame));
        EvictFlowerMemory(sender);EvictFlowerMemory(receiver);
        sender=ReloadBall(sender);receiver=ReloadBall(receiver);new TitleStorage().Login(sender);new TitleStorage().Login(receiver);Revora.Game.Features.Flowers.SendFlower(sender,frame);
        int category=(int)Revora.Game.Features.Flowers.GetFlowerTyp(type);uint[] totals={receiver.Entity.MyFlowers.RedRoses,receiver.Entity.MyFlowers.Lilies,receiver.Entity.MyFlowers.Orchads,receiver.Entity.MyFlowers.Tulips};
        Check(!Fenced(sender) && !Fenced(receiver) && !sender.Inventory.ContainsUID(item.UID) && Deliveries(sender)==1
            && totals[category]==type%1000 && totals.Sum(v=>(long)v)==type%1000 && Flowers(sender)==1,
            "ordinary earned gift delivers exact durable recipient total once through concurrent use and reload: "+type);
        Check(HasAchievement(sender,10127)==(type/1000<=755) && HasAchievement(receiver,10127)==(type/1000<=755),
            "earned flower gives both participants durable achievement evidence; letter alone does not: "+type);
    }
    static void VerifyFreeFlowers(Map garden) {
        DateTime start=flowerNow;
        try {
            var sender=ReloadBall(GardenPlayer(garden));var receiver=GardenPlayer(garden);receiver.Entity.Body=2001;EntityTable.SaveEntity(receiver);receiver=ReloadBall(receiver);
            var frame=FlowerRequest(sender,receiver,0);
            Query("CREATE FUNCTION reject_free_flower() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER reject_free_flower BEFORE INSERT ON revora_flower_deliveries FOR EACH ROW EXECUTE FUNCTION reject_free_flower()");
            try{Revora.Game.Features.Flowers.SendFlower(sender,frame);}finally{Query("DROP TRIGGER reject_free_flower ON revora_flower_deliveries;DROP FUNCTION reject_free_flower()");}
            sender=ReloadBall(sender);receiver=ReloadBall(receiver);
            Check(Deliveries(sender)==0 && sender.Entity.MyFlowers.aFlower==1 && receiver.Entity.MyFlowers.RedRoses==0,
                "failed free-gift receipt preserves daily allowance and recipient totals through reload");
            Parallel.For(0,12,n=>Revora.Game.Features.Flowers.SendFlower(sender,frame));
            sender=ReloadBall(sender);receiver=ReloadBall(receiver);Revora.Game.Features.Flowers.SendFlower(sender,frame);
            Check(!Fenced(sender) && !Fenced(receiver) && Deliveries(sender)==1 && sender.Entity.MyFlowers.aFlower==0 && receiver.Entity.MyFlowers.RedRoses==1,
                "daily free flower is consumed once across concurrent requests and both reloads");
            flowerNow=start.AddMonths(1);Call(FlowerStore,null,"EnsureDay",sender);Call(FlowerStore,null,"EnsureDay",receiver);
            Check(sender.Entity.MyFlowers.aFlower==1 && receiver.Entity.MyFlowers.RedRoses2day==0 && receiver.Entity.MyFlowers.RedRoses==1,
                "query-time daily refresh exposes new allowance without discarding lifetime gifts");
            Revora.Game.Features.Flowers.SendFlower(sender,frame);sender=ReloadBall(sender);receiver=ReloadBall(receiver);
            Check(Deliveries(sender)==2 && receiver.Entity.MyFlowers.RedRoses==2 && receiver.Entity.MyFlowers.RedRoses2day==1 && sender.Entity.MyFlowers.aFlower==0,
                "same day number in a later month gets one new allowance and resets daily totals only");
            // Run ranking transitions before the overflow fixture deliberately
            // installs a permanent uint.MaxValue orchid leader.
            VerifyFlowerQueens(garden);
            VerifyCompetingFlowerQueens(garden);
            VerifyGenderQueenVacancy(garden);
            VerifyFlowerFaults(garden);
            VerifyFlowerAchievementWrites(garden);
        }finally{flowerNow=start;}
    }
    static void VerifyFlowerQueens(Map garden) {
        var mapping=new[]{new[]{751001,11124},new[]{752001,11127},new[]{753003,11125},new[]{754001,11126}};
        foreach(var entry in mapping) {
            var sender=ReloadBall(GardenPlayer(garden));var queen=GardenPlayer(garden);
            queen.Entity.Body=2001;EntityTable.SaveEntity(queen);queen=ReloadBall(queen);
            int category=entry[0]/1000-750;
            // Put the candidate exactly level with the existing leader. Her newer
            // UID loses the tie; only the actual delivered item takes first place.
            Query("UPDATE revora_flower_balances SET totals["+category+"]=(SELECT COALESCE(max(f.totals["+category+"]),0) FROM revora_flower_balances f JOIN entities e ON e.uid=f.uid WHERE e.body IN (2001,2002,2005,2006)) WHERE uid=@uid","uid",(long)queen.Entity.UID);
            EvictFlowerMemory(queen);queen=ReloadBall(queen);new TitleStorage().Login(queen);
            Check(!HasAchievement(queen,entry[1]),"zero or losing tie is not queen: "+entry[1]);
            var item=Pack(sender,(uint)entry[0],1);var wire=FlowerRequest(sender,queen,item.UID);
            Query("ALTER TABLE revora_achievement_evidence ADD CONSTRAINT queen_failure CHECK(achievement_id<>"+entry[1]+") NOT VALID");
            try{Revora.Game.Features.Flowers.SendFlower(sender,wire);}finally{Query("ALTER TABLE revora_achievement_evidence DROP CONSTRAINT queen_failure");}
            Check(Fenced(sender) && Fenced(queen) && Deliveries(sender)==0 && SavedRune(item,"id")==entry[0],"queen evidence failure rolls back delivery and source: "+entry[1]);
            sender=ReloadBall(sender);queen=ReloadBall(queen);
            Revora.Game.Features.Flowers.SendFlower(sender,wire);Revora.Game.Features.Flowers.SendFlower(sender,wire);
            Check(!Fenced(sender) && !Fenced(queen) && Deliveries(sender)==1 && HasAchievement(queen,entry[1])
                && !sender.Inventory.ContainsUID(item.UID),"ordinary real gift earns exact queen identity once: "+entry[1]);
            var successor=GardenPlayer(garden);successor.Entity.Body=2002;EntityTable.SaveEntity(successor);successor=ReloadBall(successor);
            Query("UPDATE revora_flower_balances SET totals["+category+"]=(SELECT max(totals["+category+"])+1 FROM revora_flower_balances) WHERE uid=@uid","uid",(long)successor.Entity.UID);
            EvictFlowerMemory(successor);successor=ReloadBall(successor);new TitleStorage().Login(successor);
            EvictFlowerMemory(queen);queen=ReloadBall(queen);new TitleStorage().Login(queen);
            Check(HasAchievement(successor,entry[1]) && HasAchievement(queen,entry[1]),"saved leader recovery and historical queen retention survive reload: "+entry[1]);
        }
    }
    static void VerifyCompetingFlowerQueens(Map garden) {
        foreach(var entry in new[]{new[]{751001,11124},new[]{752001,11127},new[]{753003,11125},new[]{754001,11126}}) {
            int category=entry[0]/1000-750;
            var senders=new[]{ReloadBall(GardenPlayer(garden)),ReloadBall(GardenPlayer(garden))};
            var candidates=new[]{GardenPlayer(garden),GardenPlayer(garden)};
            for(int n=0;n<2;n++) {
                candidates[n].Entity.Body=2001;EntityTable.SaveEntity(candidates[n]);candidates[n]=ReloadBall(candidates[n]);
                Query("UPDATE revora_flower_balances SET totals["+category+"]=(SELECT COALESCE(max(f.totals["+category+"]),0) FROM revora_flower_balances f JOIN entities e ON e.uid=f.uid WHERE e.body IN (2001,2002,2005,2006)) WHERE uid=@uid","uid",(long)candidates[n].Entity.UID);
                EvictFlowerMemory(candidates[n]);candidates[n]=ReloadBall(candidates[n]);
            }
            Check(candidates[0].Entity.UID<candidates[1].Entity.UID && candidates.Concat(senders).All(p=>!p.Fake && p.Account.State==AccountTable.AccountState.Player),"competing queens use distinct ordinary participants: "+entry[1]);
            var items=senders.Select(p=>Pack(p,(uint)entry[0],1)).ToArray();
            var requests=new[]{FlowerRequest(senders[0],candidates[0],items[0].UID),FlowerRequest(senders[1],candidates[1],items[1].UID)};
            using(var start=new System.Threading.ManualResetEventSlim(false)) {
                var tasks=Enumerable.Range(0,2).Select(n=>Task.Run(()=>{start.Wait();for(int replay=0;replay<6;replay++)Revora.Game.Features.Flowers.SendFlower(senders[n],requests[n]);})).ToArray();
                start.Set();Check(Task.WaitAll(tasks,10000),"independent competing deliveries finish without deadlock: "+entry[1]);
            }
            Check(senders.Concat(candidates).All(p=>!Fenced(p)) && senders.All(p=>Deliveries(p)==1)
                && !senders[0].Inventory.ContainsUID(items[0].UID) && !senders[1].Inventory.ContainsUID(items[1].UID),"concurrent replay consumes each competitor gift exactly once: "+entry[1]);
            long first=Scalar("SELECT totals["+category+"] FROM revora_flower_balances WHERE uid=@uid",candidates[0].Entity.UID);
            Check(first==Scalar("SELECT totals["+category+"] FROM revora_flower_balances WHERE uid=@uid",candidates[1].Entity.UID)
                && HasAchievement(candidates[0],entry[1]),"final equal totals recognize the lower UID queen: "+entry[1]);
            // The higher UID may legitimately have led briefly if her delivery
            // committed first; historical completion must not be rejected then.
            var extra=Pack(senders[1],(uint)entry[0],1);Revora.Game.Features.Flowers.SendFlower(senders[1],FlowerRequest(senders[1],candidates[1],extra.UID));
            foreach(var p in candidates){EvictFlowerMemory(p);var loaded=ReloadBall(p);new TitleStorage().Login(loaded);Check(HasAchievement(loaded,entry[1]),"both accepted reigns survive final overtake and reload: "+entry[1]);}
            var male=senders[0];Query("UPDATE revora_flower_balances SET totals["+category+"]=4294967295 WHERE uid=@uid","uid",(long)male.Entity.UID);
            EvictFlowerMemory(male);male=ReloadBall(male);new TitleStorage().Login(male);
            Check(!HasAchievement(male,entry[1]),"male love-gift leader cannot earn the female queen achievement: "+entry[1]);
        }
    }
    static void VerifyGenderQueenVacancy(Map garden) {
        var queen=GardenPlayer(garden);queen.Entity.Body=2001;queen.Entity.ConquerPoints=20000000;queen.Entity.Spouse="FixturePartner";EntityTable.SaveEntity(queen);queen=ReloadBall(queen);
        var successor=GardenPlayer(garden);successor.Entity.Body=2002;EntityTable.SaveEntity(successor);successor=ReloadBall(successor);
        Query("UPDATE revora_flower_balances SET totals[1]=(SELECT COALESCE(max(f.totals[1]),0)+10 FROM revora_flower_balances f JOIN entities e ON e.uid=f.uid WHERE e.body IN (2001,2002,2005,2006)) WHERE uid=@uid","uid",(long)queen.Entity.UID);
        Query("UPDATE revora_flower_balances SET totals[1]=(SELECT totals[1]-1 FROM revora_flower_balances WHERE uid=@queen) WHERE uid=@uid","queen",(long)queen.Entity.UID,"uid",(long)successor.Entity.UID);
        EvictFlowerMemory(queen);queen=ReloadBall(queen);new TitleStorage().Login(queen);
        GameState removed;Kernel.GamePool.TryRemove(successor.Entity.UID,out removed);
        Check(HasAchievement(queen,11124) && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11124",successor.Entity.UID)==0,"offline successor starts below the current queen");
        var garment=RebirthGear(queen,9,187405);
        uint before=queen.Entity.ConquerPoints,price=Revora.Game.Features.GenderChange.Price;
        Query("ALTER TABLE revora_achievement_evidence ADD CONSTRAINT vacancy_failure CHECK(uid<>"+successor.Entity.UID+" OR achievement_id<>11124) NOT VALID");
        bool failed=false;try{lock(queen.ItemSyncRoot)Call(typeof(Revora.Game.Features.GenderChange),null,"ApplyOwned",queen);}catch(Npgsql.PostgresException){failed=true;}finally{Query("ALTER TABLE revora_achievement_evidence DROP CONSTRAINT vacancy_failure");}
        Check(failed && Scalar("SELECT body FROM entities WHERE uid=@uid",queen.Entity.UID)==2001 && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",queen.Entity.UID)==before,
            "offline successor evidence failure rolls back gender and CP payment together");
        Check(SavedRune(garment,"position")==9 && queen.Equipment.TryGetItem((byte)9)==garment,"failed vacancy retains the equipped garment");
        bool applied;lock(queen.ItemSyncRoot)applied=(bool)Call(typeof(Revora.Game.Features.GenderChange),null,"ApplyOwned",queen);
        Check(applied && queen.Action==0 && !Fenced(queen) && Scalar("SELECT body FROM entities WHERE uid=@uid",queen.Entity.UID)==1003 && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",queen.Entity.UID)==before-price
            && Scalar("SELECT count(*) FROM entities WHERE uid=@uid AND spouse='FixturePartner' AND mapid=1002 AND x=300 AND y=278",queen.Entity.UID)==1,
            "gender transaction charges once, preserves marriage and saves reconnect location before closing service admission");
        Check(SavedRune(garment,"position")==0 && queen.Inventory.ContainsUID(garment.UID) && queen.Equipment.TryGetItem((byte)9)==null,"committed gender change moves garment to the bag without loss");
        Check(Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11124",successor.Entity.UID)==1,"offline successor earns queen at the committed vacancy");
        Check(!Revora.Game.Features.GenderChange.Apply(queen) && EntityTable.SaveEntity(queen)
            && Scalar("SELECT body FROM entities WHERE uid=@uid",queen.Entity.UID)==1003
            && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",queen.Entity.UID)==before-price,"repeated confirmation is rejected and autosave retains committed appearance");
        // End the offline reign before login: historical evidence must remain.
        Query("UPDATE revora_flower_balances SET totals[1]=0 WHERE uid=@uid","uid",(long)successor.Entity.UID);
        EvictFlowerMemory(successor);successor=ReloadBall(successor);new TitleStorage().Login(successor);
        Check(HasAchievement(successor,11124),"offline successor retains her brief reign after losing rank before login");
        var full=ReloadBall(GardenPlayer(garden));full.Entity.ConquerPoints=20000000;EntityTable.SaveEntity(full);var worn=RebirthGear(full,9,187405);
        while(full.Inventory.Count<40)Pack(full,1000000,1);
        Check(!Revora.Game.Features.GenderChange.Apply(full) && !Fenced(full) && SavedRune(worn,"position")==9
            && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",full.Entity.UID)==20000000,"full bag rejects gender change before payment or garment movement");
        var ordinary=ReloadBall(GardenPlayer(garden));ordinary.Entity.ConquerPoints=20000000;EntityTable.SaveEntity(ordinary);
        Check(Revora.Game.Features.GenderChange.Apply(ordinary) && !Revora.Game.Features.GenderChange.Apply(ordinary)
            && Scalar("SELECT body FROM entities WHERE uid=@uid",ordinary.Entity.UID)==2001
            && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",ordinary.Entity.UID)==20000000-price,
            "ordinary public gender service commits once and rejects repeated confirmation through disconnect");
    }
    static void VerifyFlowerAchievementWrites(Map garden) {
        foreach(bool free in new[]{false,true})foreach(bool recipientFault in new[]{false,true}) {
            var sender=ReloadBall(GardenPlayer(garden));var receiver=GardenPlayer(garden);
            receiver.Entity.Body=2001;EntityTable.SaveEntity(receiver);receiver=ReloadBall(receiver);
            uint uid=free?0:Pack(sender,753003,1).UID;var frame=FlowerRequest(sender,receiver,uid);
            string predicate=recipientFault?"(SELECT count(*) FROM revora_achievement_evidence WHERE source_key=NEW.source_key AND achievement_id=10127)=1":"TRUE";
            Query("CREATE FUNCTION suppress_flower_achievement() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF NEW.achievement_id=10127 AND "
                +predicate+" THEN RETURN NULL; END IF; RETURN NEW; END';CREATE TRIGGER suppress_flower_achievement BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW EXECUTE FUNCTION suppress_flower_achievement()");
            try{Revora.Game.Features.Flowers.SendFlower(sender,frame);}
            finally{Query("DROP TRIGGER suppress_flower_achievement ON revora_achievement_evidence;DROP FUNCTION suppress_flower_achievement()");}
            Check(Fenced(sender) && Fenced(receiver) && Deliveries(sender)==0
                && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10127",sender.Entity.UID)==0
                && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10127",receiver.Entity.UID)==0,
                "missing "+(recipientFault?"recipient":"sender")+" achievement rolls back both participants and delivery, free="+free);
            sender=ReloadBall(sender);receiver=ReloadBall(receiver);
            new TitleStorage().Login(sender);new TitleStorage().Login(receiver);
            Check(!HasAchievement(sender,10127) && !HasAchievement(receiver,10127)
                && receiver.Entity.MyFlowers.RedRoses==0 && receiver.Entity.MyFlowers.Orchads==0
                && (free?sender.Entity.MyFlowers.aFlower==1:sender.Inventory.ContainsUID(uid)),
                "failed flower achievement retains source or free allowance through production reload");
            Parallel.For(0,12,n=>Revora.Game.Features.Flowers.SendFlower(sender,frame));
            Check(!Fenced(sender) && !Fenced(receiver) && HasAchievement(sender,10127) && HasAchievement(receiver,10127)
                && Deliveries(sender)==1,"retried delivery immediately awards both participants once");
            sender=ReloadBall(sender);receiver=ReloadBall(receiver);new TitleStorage().Login(sender);new TitleStorage().Login(receiver);Revora.Game.Features.Flowers.SendFlower(sender,frame);
            Check(HasAchievement(sender,10127) && HasAchievement(receiver,10127) && Deliveries(sender)==1,
                "both flower achievement records survive reload and stale packet replay");
        }
        var woman=GardenPlayer(garden);woman.Entity.Body=2001;EntityTable.SaveEntity(woman);woman=ReloadBall(woman);
        var man=ReloadBall(GardenPlayer(garden));
        Revora.Game.Features.Flowers.SendFlower(woman,FlowerRequest(woman,man,0));
        Check(!Fenced(woman) && !Fenced(man) && HasAchievement(woman,10127) && HasAchievement(man,10127),
            "ordinary free kiss awards the female sender and male recipient");
        var kiss=Pack(woman,755001,1);Revora.Game.Features.Flowers.SendFlower(woman,FlowerRequest(woman,man,kiss.UID));
        Check(!Fenced(woman) && !Fenced(man) && Deliveries(woman)==2
            && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10127",woman.Entity.UID)==1
            && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10127",man.Entity.UID)==1,
            "later owned kiss delivers normally while retaining one lifetime achievement per participant");
    }
    static void EvictFlowerMemory(GameState p) {
        var type=typeof(Revora.Game.Features.Flowers);Revora.Game.Features.Flowers discarded;
        Revora.Game.Features.Flowers.Flowers_Poll.TryRemove(p.Entity.UID,out discarded);Revora.Game.Features.Flowers.BoyFlowers.TryRemove(p.Entity.UID,out discarded);
        foreach(string name in new[]{"Rankrose","Ranklili","Rankorchid","RankTulips","RankKiss","RankLove","RankTine","RankJade"})
            ((System.Collections.Generic.List<Revora.Game.Features.Flowers>)type.GetField(name).GetValue(null)).RemoveAll(f=>f.UID==p.Entity.UID);
        foreach(string name in new[]{"RedRousesTop100","LiliesTop100","OrchidsTop100","TulipsTop100","KissTop100","LoveTop100","TineTop100","JadeTop100"}) {
            var field=type.GetField(name);field.SetValue(null,((Revora.Game.Features.Flowers[])field.GetValue(null)).Where(f=>f.UID!=p.Entity.UID).ToArray());
        }
    }
    static void VerifyFlowerFaults(Map garden) {
        foreach(string fault in new[]{"source","sender","recipient"}) {
            var sender=ReloadBall(GardenPlayer(garden));var receiver=GardenPlayer(garden);receiver.Entity.Body=2001;EntityTable.SaveEntity(receiver);receiver=ReloadBall(receiver);
            var item=Pack(sender,753003,2);var frame=FlowerRequest(sender,receiver,item.UID);
            string table=fault=="source"?"items":"revora_flower_balances";
            string predicate=fault=="source"?"TRUE":fault=="sender"?"NEW.totals=OLD.totals":"NEW.totals<>OLD.totals";
            Query("CREATE FUNCTION reject_flower_write() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF "+predicate+" THEN RETURN NULL; END IF; RETURN NEW; END';CREATE TRIGGER reject_flower_write BEFORE UPDATE ON "+table+" FOR EACH ROW EXECUTE FUNCTION reject_flower_write()");
            try{Revora.Game.Features.Flowers.SendFlower(sender,frame);}finally{Query("DROP TRIGGER reject_flower_write ON "+table+";DROP FUNCTION reject_flower_write()");}
            sender=ReloadBall(sender);receiver=ReloadBall(receiver);
            Check(Deliveries(sender)==0 && sender.Inventory.Objects.Single(i=>i.UID==item.UID).StackSize==2 && receiver.Entity.MyFlowers.Orchads==0,
                "suppressed flower "+fault+" write rolls back source, both balances and receipt through reload");
            Revora.Game.Features.Flowers.SendFlower(sender,frame);Revora.Game.Features.Flowers.SendFlower(sender,frame);
            Check(!Fenced(sender) && !Fenced(receiver) && Deliveries(sender)==1 && sender.Inventory.Objects.Single(i=>i.UID==item.UID).StackSize==1 && receiver.Entity.MyFlowers.Orchads==3,
                "preserved stacked flower retries once with duplicate-request protection after "+fault);
        }
        var a=ReloadBall(GardenPlayer(garden));var b=GardenPlayer(garden);b.Entity.Body=2001;EntityTable.SaveEntity(b);b=ReloadBall(b);
        var orchid=Pack(a,753003,1);var letter=Pack(b,756003,1);var ab=FlowerRequest(a,b,orchid.UID);var ba=FlowerRequest(b,a,letter.UID);
        var tasks=new[]{Task.Run(()=>Revora.Game.Features.Flowers.SendFlower(a,ab)),Task.Run(()=>Revora.Game.Features.Flowers.SendFlower(b,ba))};
        Check(Task.WaitAll(tasks,5000) && !Fenced(a) && !Fenced(b) && Deliveries(a)==1 && Deliveries(b)==1
            && a.Entity.MyFlowers.Lilies==3 && b.Entity.MyFlowers.Orchads==3,"reciprocal gifts use consistent owner locking and preserve both credits");
        var extra=Pack(a,753003,1);Query("UPDATE revora_flower_balances SET totals[3]=100 WHERE uid=@uid","uid",(long)b.Entity.UID);
        Revora.Game.Features.Flowers.SendFlower(a,FlowerRequest(a,b,extra.UID));
        Check(Fenced(a) && Fenced(b) && a.Inventory.ContainsUID(extra.UID) && Scalar("SELECT totals[3] FROM revora_flower_balances WHERE uid=@uid",b.Entity.UID)==100,
            "stale recipient cannot overwrite newer durable totals or consume a new gift");
        a=ReloadBall(a);b=ReloadBall(b);Query("UPDATE revora_flower_balances SET totals[3]=4294967295 WHERE uid=@uid","uid",(long)b.Entity.UID);b=ReloadBall(b);
        Revora.Game.Features.Flowers.SendFlower(a,FlowerRequest(a,b,extra.UID));
        Check(!Fenced(a) && !Fenced(b) && a.Inventory.ContainsUID(extra.UID) && b.Entity.MyFlowers.Orchads==uint.MaxValue && Deliveries(a)==1,
            "full native counter preserves the entire gift without wrapping or partial credit");
        var legacy=GardenPlayer(garden);legacy.Entity.MyFlowers=new Revora.Game.Features.Flowers(legacy.Entity.UID,"legacy"){RedRoses=5};
        bool rejected=false;try{Call(FlowerStore,null,"Load",legacy);}catch(InvalidOperationException){rejected=true;}
        Check(rejected && legacy.Entity.MyFlowers.RedRoses==5 && Scalar("SELECT count(*) FROM revora_flower_balances WHERE uid=@uid",legacy.Entity.UID)==0,
            "unmigrated memory history is retained and never silently initialized to zero");
        VerifyFlowerStartup(a,b);
    }
    static void ClearFlowerCaches() {
        var type=typeof(Revora.Game.Features.Flowers);
        Revora.Game.Features.Flowers.Flowers_Poll.Clear();Revora.Game.Features.Flowers.BoyFlowers.Clear();
        foreach(string name in new[]{"Rankrose","Ranklili","Rankorchid","RankTulips","RankKiss","RankLove","RankTine","RankJade"})
            ((System.Collections.Generic.List<Revora.Game.Features.Flowers>)type.GetField(name).GetValue(null)).Clear();
        foreach(string name in new[]{"RedRousesTop100","LiliesTop100","OrchidsTop100","TulipsTop100","KissTop100","LoveTop100","TineTop100","JadeTop100"})
            type.GetField(name).SetValue(null,new Revora.Game.Features.Flowers[0]);
    }
    static void VerifyFlowerStartup(GameState male,GameState female) {
        var original=male.Entity.MyFlowers;bool rejected=false;
        try{Call(FlowerStore,null,"RestoreStartup");}catch(InvalidOperationException){rejected=true;}
        Check(rejected && ReferenceEquals(original,male.Entity.MyFlowers),"offline flower restoration cannot replace active participants");
        Kernel.GamePool.Clear();ClearFlowerCaches();
        string day=(string)Call(Magnolia,null,"Day",flowerNow);
        Query("UPDATE revora_flower_balances SET day='99991231' WHERE uid=@uid","uid",(long)male.Entity.UID);
        rejected=false;try{Call(FlowerStore,null,"RestoreStartup");}catch(InvalidOperationException){rejected=true;}
        Check(rejected && Revora.Game.Features.Flowers.Flowers_Poll.Count==0 && Revora.Game.Features.Flowers.BoyFlowers.Count==0,
            "invalid startup snapshot cannot publish a partially restored ranking");
        Query("UPDATE revora_flower_balances SET day=@day WHERE uid=@uid","day",day,"uid",(long)male.Entity.UID);
        Call(FlowerStore,null,"RestoreStartup");
        var man=Revora.Game.Features.Flowers.BoyFlowers[male.Entity.UID];var woman=Revora.Game.Features.Flowers.Flowers_Poll[female.Entity.UID];
        Check(Kernel.GamePool.Count==0 && man.Lilies==3 && woman.Orchads==uint.MaxValue
            && Revora.Game.Features.Flowers.LoveTop100.Any(f=>f.UID==male.Entity.UID)
            && Revora.Game.Features.Flowers.OrchidsTop100.Any(f=>f.UID==female.Entity.UID),
            "startup restores saved offline male and female recipients into their ranking groups");
        Call(FlowerStore,null,"Initialize");
        Check(ReferenceEquals(man,Revora.Game.Features.Flowers.BoyFlowers[male.Entity.UID]),"repeat initialization preserves already restored ranking identities");
        male=ReloadBall(male);female=ReloadBall(female);
        Check(ReferenceEquals(man,male.Entity.MyFlowers) && ReferenceEquals(woman,female.Entity.MyFlowers)
            && Revora.Game.Features.Flowers.LoveTop100.Count(f=>f.UID==male.Entity.UID)==1,
            "later ordinary login attaches restored histories without duplicate rank entries");
        Kernel.GamePool.Clear();ClearFlowerCaches();flowerNow=flowerNow.AddDays(1);Call(FlowerStore,null,"RestoreStartup");
        man=Revora.Game.Features.Flowers.BoyFlowers[male.Entity.UID];woman=Revora.Game.Features.Flowers.Flowers_Poll[female.Entity.UID];
        Check(man.Lilies==3 && woman.Orchads==uint.MaxValue && man.Lilies2day==0 && woman.Orchads2day==0,
            "next-day startup retains lifetime ranking totals and resets displayed daily totals");
        VerifyFlowerRestartProcess(male.Entity.UID,female.Entity.UID);
    }
    static void VerifyFlowerRestartProcess(uint male,uint female) {
        var start=new System.Diagnostics.ProcessStartInfo {
            FileName=typeof(PotencyHuntTests).Assembly.Location,Arguments="--flower-restart-check",
            WorkingDirectory=Environment.CurrentDirectory,UseShellExecute=false,CreateNoWindow=true,
            RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
        using(var child=System.Diagnostics.Process.Start(start)) {
            var output=child.StandardOutput.ReadToEndAsync();var errors=child.StandardError.ReadToEndAsync();
            // The connection string stays in the private inherited pipe, never
            // in process arguments, environment dumps, files or test output.
            child.StandardInput.WriteLine(DataHolder.ConnectionString);
            child.StandardInput.WriteLine(flowerNow.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture));
            child.StandardInput.WriteLine(male.ToString(System.Globalization.CultureInfo.InvariantCulture));
            child.StandardInput.WriteLine(female.ToString(System.Globalization.CultureInfo.InvariantCulture));child.StandardInput.Close();
            if(!child.WaitForExit(20000)){child.Kill();child.WaitForExit();throw new Exception("Flower restart child timed out.");}
            string result=output.GetAwaiter().GetResult();string error=errors.GetAwaiter().GetResult();
            Check(child.ExitCode==0 && error.Length==0 && result.Contains("Flower restart checks passed: 3"),
                "fresh validation process restores offline ranks and daily totals from the existing isolated database");
        }
    }
    static int FlowerRestartChild() {
        try {
            string connection=System.Console.ReadLine();
            if(string.IsNullOrEmpty(connection) || connection.Length>8192)return 2;
            var builder=new Npgsql.NpgsqlConnectionStringBuilder(connection);
            if(builder.SearchPath!="revora_potency_validation")return 2;
            DataHolder.ConnectionString=builder.ConnectionString;
            var now=new DateTime(long.Parse(System.Console.ReadLine(),System.Globalization.CultureInfo.InvariantCulture),DateTimeKind.Utc);
            uint male=uint.Parse(System.Console.ReadLine(),System.Globalization.CultureInfo.InvariantCulture);
            uint female=uint.Parse(System.Console.ReadLine(),System.Globalization.CultureInfo.InvariantCulture);
            Check(Scalar("SELECT count(*) FROM information_schema.tables WHERE table_schema='revora_potency_validation' AND table_name='revora_flower_balances'",0)==1
                && Kernel.GamePool.Count==0 && Revora.Game.Features.Flowers.Flowers_Poll.Count==0 && Revora.Game.Features.Flowers.BoyFlowers.Count==0
                && !(bool)FlowerStore.GetField("enabled",Hidden).GetValue(null),"restart child begins with fresh static state and an existing isolated ledger");
            FlowerStore.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));Call(FlowerStore,null,"Initialize");
            var man=Revora.Game.Features.Flowers.BoyFlowers[male];var woman=Revora.Game.Features.Flowers.Flowers_Poll[female];
            Check(Kernel.GamePool.Count==0 && man.Lilies==3 && woman.Orchads==uint.MaxValue && man.Lilies2day==0 && woman.Orchads2day==0
                && Revora.Game.Features.Flowers.LoveTop100.Any(f=>f.UID==male) && Revora.Game.Features.Flowers.OrchidsTop100.Any(f=>f.UID==female),
                "new process hydrates offline recipients and date-normalized rankings without any login");
            Call(FlowerStore,null,"Initialize");
            Check(ReferenceEquals(man,Revora.Game.Features.Flowers.BoyFlowers[male]) && Revora.Game.Features.Flowers.LoveTop100.Count(f=>f.UID==male)==1,
                "repeated initialization in the new process cannot duplicate ranking owners");
            System.Console.WriteLine("Flower restart checks passed: "+checks);return 0;
        }catch(Exception ex){System.Console.Error.WriteLine("Flower restart failed: "+ex.GetType().Name);return 1;}
    }
}
