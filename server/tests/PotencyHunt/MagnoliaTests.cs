using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type Magnolia = typeof(Entity).Assembly.GetType("Revora.Game.Features.MagnoliaQuest");
    static DateTime flowerNow = DateTime.UtcNow.Date.AddHours(19);
    static GameState GardenPlayer(Map garden) {
        var p = BallPlayer(garden); FlowerStand(p); p.ActiveNpc = 44100;
        p.Entity.ConquerPoints = 1000; p.LastResetTime = flowerNow.ToLocalTime();
        if (!EntityTable.SaveEntity(p)) throw new Exception("Garden fixture save failed."); return p;
    }
    static void FlowerNpc(GameState p, byte choice) { p.ActiveNpc=44100; Npcs.GetDialog(new NpcRequest(true) {NpcID=44100,OptionID=choice},p); }
    static ConquerItem FlowerSeed(GameState p, bool super=false) {
        FlowerNpc(p,1); FlowerNpc(p,super ? (byte)4 : (byte)3);
        return p.Inventory.Objects.Single(i => i.ID == (super ? 729310u : 729306u));
    }
    static Tuple<ushort,ushort> FlowerTile(Map map, ushort x, ushort y) {
        for (int radius=0; radius<=6; radius++) for (int dx=-radius; dx<=radius; dx++) for (int dy=-radius; dy<=radius; dy++) {
            int nx=x+dx,ny=y+dy;
            if (Math.Max(Math.Abs(dx),Math.Abs(dy))!=radius || Kernel.GetDistance((ushort)nx,(ushort)ny,x,y)>=7 || !map.Floor[nx,ny,MapObjectType.Player]) continue;
            return Tuple.Create((ushort)nx,(ushort)ny);
        }
        throw new Exception("Garden landmark has no legal approach: "+x+","+y);
    }
    static void FlowerStand(GameState p) { var npc=p.Map.Npcs[44100]; var xy=FlowerTile(p.Map,npc.X,npc.Y);p.Entity.X=xy.Item1;p.Entity.Y=xy.Item2; }
    static void FlowerBed(GameState p, bool second=false) { var xy=FlowerTile(p.Map,second ? (ushort)106 : (ushort)98,second ? (ushort)98 : (ushort)111);p.Entity.X=xy.Item1;p.Entity.Y=xy.Item2; }
    static long FlowerRuns(GameState p) { return Scalar("SELECT count(*) FROM revora_magnolia_runs WHERE uid=@uid",p.Entity.UID); }
    static long Flowers(GameState p) { return Scalar("SELECT count(*) FROM revora_magnolia_runs WHERE uid=@uid AND completed=true AND legacy=false",p.Entity.UID); }
    static void VerifyMagnolia(string runtime, Map town) {
        VerifyFirstRebirth(runtime,town); Call(Magnolia,null,"Initialize");
        Magnolia.GetField("BonusRoll",Hidden).SetValue(null,(Func<int,int>)(max=>99));
        Magnolia.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(() => flowerNow));
        Call(Travel,null,"Initialize"); Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.ExplorationWorld"),null,"PrepareMaps");
        var garden = World(runtime,1806); MiningNpc(garden,44100); MiningNpc(town,79882);
        var firstBed=FlowerTile(garden,98,111);var secondBed=FlowerTile(garden,106,98);
        Check(garden.Npcs[44100].X == 102 && garden.Npcs[44100].Y == 105
            && garden.Floor[firstBed.Item1,firstBed.Item2,MapObjectType.Player] && garden.Floor[secondBed.Item1,secondBed.Item2,MapObjectType.Player],
            "installed keeper and legal flower-bed approaches: first="+firstBed+" second="+secondBed);
        Check((string)Call(Magnolia,null,"Day",new DateTime(2026,7,3,6,59,59,DateTimeKind.Utc)) == "20260702"
            && (string)Call(Magnolia,null,"Day",new DateTime(2026,7,3,7,0,0,DateTimeKind.Utc)) == "20260703"
            && (string)Call(Magnolia,null,"Day",new DateTime(2026,1,3,8,0,0,DateTimeKind.Utc)) == "20260103",
            "Magnolia daily boundary follows Pacific midnight through daylight-saving changes");
        var walker = AppearancePlayer(town); Collection(walker); walker.Entity.MyAchievement = new Achievement(walker.Entity); walker.Entity.MyAchievement.Load("");
        var plaza=FlowerTile(town,Config.DailyEnvoyX,Config.DailyEnvoyY);walker.Entity.X=plaza.Item1;walker.Entity.Y=plaza.Item2;EntityTable.SaveEntity(walker);
        AdventureNear(walker,79882); AdventureNpc(walker,79882,5);
        Check(!Fenced(walker) && walker.Entity.MapID == 1806 && HasAchievement(walker,10808), "ordinary Daily Envoy passage reaches the actual garden and earns its existing travel discovery");
        AdventureNear(walker,44100); var earned = FlowerSeed(walker); ulong before = walker.Entity.Experience, award = walker.ExpBall;
        uint cp = walker.Entity.ConquerPoints, rvp = walker.Entity.RevoraPoint; ushort study = walker.Entity.SubClasses.StudyPoints;
        AdventureWalk(walker,firstBed.Item1,firstBed.Item2); ItemHandler.UseItem(earned,walker);
        Check(!Fenced(walker) && Flowers(walker) == 1 && walker.Entity.Experience == before+award
            && walker.Entity.ConquerPoints == cp+Config.DailyQuestCp && walker.Entity.RevoraPoint == rvp+Config.DailyQuestRevoraPoints
            && walker.Entity.SubClasses.StudyPoints == study+Config.DailyMagnoliaStudy && walker.Inventory.Contains(Config.DailyQuestChiToken,1)
            && !walker.Inventory.ContainsUID(earned.UID) && walker.Entity.MagniloQuest,
            "walked free-seed journey consumes its issued seed and grants the whole configured daily reward");
        AdventureNear(walker,44100); FlowerNpc(walker,2); EntityTable.SaveEntity(walker); walker = ReloadBall(walker);
        Call(Magnolia,null,"Refresh",walker);
        Check(!Fenced(walker) && walker.Entity.MapID == 1002 && walker.Entity.MagniloQuest && walker.Entity.CliamSeed
            && Flowers(walker) == 1 && walker.Inventory.Contains(Config.DailyQuestChiToken,1), "guarded return and production reload retain the full garden reward and daily completion");
            VerifyStudyAward(walker,"magnolia");
        foreach (bool super in new[] {false,true}) {
            var p = GardenPlayer(garden); var seed = FlowerSeed(p,super); uint paid = super ? Config.DailyMagnoliaSuperSeedCp : 0;
            Check(!Fenced(p) && FlowerRuns(p) == 1 && p.Entity.ConquerPoints == 1000-paid && p.Entity.CliamSeed
                && Scalar("SELECT fee FROM revora_magnolia_runs WHERE uid=@uid",p.Entity.UID) == paid, "free/paid seed and receipt commit together: super=" + super);
            p = ReloadBall(p); p.BindWorldMap(1806); FlowerStand(p); uint seedUid=seed.UID;
            FlowerNpc(p,1); Parallel.For(0,12,i => FlowerNpc(p,super ? (byte)4 : (byte)3));
            Check(!Fenced(p) && p.Inventory.Count == 1 && p.Entity.ConquerPoints == 1000-paid && FlowerRuns(p) == 1,
                "reconnect and concurrent repeated seed requests cannot charge or issue twice: super=" + super);
            seed=p.Inventory.Objects.Single(i => i.UID==seedUid); var bedTarget=super ? secondBed : firstBed; AdventureWalk(p,bedTarget.Item1,bedTarget.Item2); ItemHandler.UseItem(seed,p);
            Check(!Fenced(p) && Flowers(p) == 1 && p.Entity.ConquerPoints == 1000-paid+Config.DailyQuestCp && p.Entity.MagniloQuest,
                "each reviewed seed plants at its actual bed and commits the full reward: super=" + super);
            p.Entity.MagniloQuest=false;p.Entity.CliamSeed=false;DialyQuestTable.Save(p);
            var extra=Pack(p,729307,1);ItemHandler.UseItem(extra,p); FlowerStand(p);FlowerNpc(p,1);FlowerNpc(p,4);
            Check(!Fenced(p) && Flowers(p) == 1 && p.Inventory.ContainsUID(extra.UID) && p.Entity.ConquerPoints == 1000-paid+Config.DailyQuestCp,
                "legacy flag resets cannot reopen a completed daily reward or charge another seed");
        }
        var race=GardenPlayer(garden);var stack=Pack(race,729307,3); FlowerBed(race); ulong ball=race.ExpBall;
        Parallel.For(0,36,i => { if(i%3==0)EntityTable.SaveEntity(race);else ItemHandler.UseItem(stack,race); });
        Check(!Fenced(race) && Flowers(race)==1 && stack.StackSize==2 && race.Entity.Experience==5000+ball*3/2
            && race.Entity.ConquerPoints==1000+Config.DailyQuestCp, "concurrent planting and autosave consume exactly one seed and one daily reward");
        flowerNow=flowerNow.AddDays(1);ItemHandler.UseItem(stack,race);
        Check(!Fenced(race) && Flowers(race)==2 && stack.StackSize==1, "online daily rollover accepts the next owned seed without reconnecting");
        flowerNow=flowerNow.AddDays(-2);ItemHandler.UseItem(stack,race);
        Check(!Fenced(race) && Flowers(race)==2 && stack.StackSize==1, "clock rollback cannot create an earlier daily reward");
        flowerNow=DateTime.UtcNow.Date.AddHours(19);
        foreach (string fault in new[] {"entities","items","revora_magnolia_runs"}) {
            var p=GardenPlayer(garden);FlowerNpc(p,1);
            Query("CREATE FUNCTION suppress_flower() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_flower BEFORE "
                +(fault=="entities" ? "UPDATE" : "INSERT")+" ON "+fault+" FOR EACH ROW EXECUTE FUNCTION suppress_flower()");
            FlowerNpc(p,4);
            Check(Fenced(p) && p.Entity.ConquerPoints==1000 && p.Inventory.Count==0 && FlowerRuns(p)==0
                && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==1000, "seed issue failure preserves CPs and creates no item or daily charge: "+fault);
            Query("DROP TRIGGER suppress_flower ON "+fault+";DROP FUNCTION suppress_flower()");
            p=ReloadBall(p);p.BindWorldMap(1806);FlowerStand(p);FlowerSeed(p,true);
            Check(!Fenced(p) && FlowerRuns(p)==1 && p.Inventory.Count==1 && p.Entity.ConquerPoints==1000-Config.DailyMagnoliaSuperSeedCp,
                "seed issue retries through normal NPC flow after production reconnect: "+fault);
        }
        foreach (string fault in new[] {"evidence","profile","seed","stack","reward","rune","receipt","skills","recovery"}) {
            var p=fault=="recovery" ? PendingFirstLife(town) : GardenPlayer(garden);
            p.BindWorldMap(1806);FlowerBed(p);
            if(fault=="skills") {p.Entity.Experience=DataHolder.LevelExperience(135)-1;EntityTable.SaveEntity(p);}
            var seed=Pack(p,729307,(ushort)(fault=="stack" ? 2 : 1));ulong xp=p.Entity.Experience;uint funds=p.Entity.ConquerPoints;int bag=p.Inventory.Count;
            string table=fault=="evidence"?"revora_achievement_evidence":fault=="profile" ? "entities" : fault=="receipt" ? "revora_magnolia_runs" : fault=="skills" ? "skills" : fault=="recovery" ? "revora_first_rebirths" : "items";
            string verb=fault=="evidence"?"INSERT":fault=="seed" ? "DELETE" : fault=="reward" || fault=="rune" || fault=="skills" ? "INSERT" : "UPDATE";
            Query("CREATE FUNCTION suppress_flower() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_flower BEFORE "+verb+" ON "+table+" FOR EACH ROW"+(fault=="rune"?" WHEN(NEW.id=4030101)":"")+" EXECUTE FUNCTION suppress_flower()");
            ItemHandler.UseItem(seed,p);
            Check(Fenced(p) && p.Entity.Experience==xp && p.Entity.ConquerPoints==funds && Flowers(p)==0 && p.Inventory.Count==bag
                && p.Inventory.ContainsUID(seed.UID) && SavedRune(seed,"stacksize")==seed.StackSize
                && (fault!="recovery" || LifePending(p)==1), "planting failure rolls back seed, all currencies, EXP and receipt: "+fault);
            Query("DROP TRIGGER suppress_flower ON "+table+";DROP FUNCTION suppress_flower()");
            if(fault=="evidence")VerifyStudyRollback(p);
            p=ReloadBall(p);p.BindWorldMap(1806);FlowerBed(p);ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.UID==seed.UID),p);
            Check(!Fenced(p) && Flowers(p)==1 && (fault!="recovery" || p.Entity.Level==139 && LifePending(p)==0), "production reload retries the complete planting reward after "+fault+" failure");
            if(fault=="evidence")VerifyStudyAward(p,"magnolia");
        }
        foreach (string currency in new[] {"conquerpoints","revorapoint","studypoints"}) {
            var p=GardenPlayer(garden);var seed=FlowerSeed(p);FlowerBed(p);
            Query("UPDATE entities SET "+currency+"="+currency+"+1 WHERE uid=@uid","uid",(long)p.Entity.UID);ItemHandler.UseItem(seed,p);
            Check(Fenced(p) && Flowers(p)==0 && p.Inventory.ContainsUID(seed.UID) && p.Entity.Experience==5000,
                "stale "+currency+" preimage cannot overwrite another reward or consume the seed");
        }
        var legacy=GardenPlayer(garden);DialyQuestTable.Load(legacy);legacy.Entity.MagniloQuest=true;DialyQuestTable.Save(legacy);FlowerNpc(legacy,1);FlowerNpc(legacy,3);
        Check(!Fenced(legacy) && FlowerRuns(legacy)==1 && Flowers(legacy)==0 && legacy.Inventory.Count==0
            && Scalar("SELECT count(*) FROM revora_magnolia_runs WHERE uid=@uid AND completed AND legacy",legacy.Entity.UID)==1,
            "today's saved legacy completion imports without fabricating a seed or reward");
        legacy.Entity.MagniloQuest=false;DialyQuestTable.Save(legacy);FlowerNpc(legacy,1);FlowerNpc(legacy,4);
        Check(!Fenced(legacy) && legacy.Inventory.Count==0 && legacy.Entity.ConquerPoints==1000,
            "imported legacy completion remains authoritative after the old flag is reset");
        var frozen=GardenPlayer(garden);var frozenSeed=FlowerSeed(frozen);uint savedCp=Config.DailyQuestCp;
        try {Config.DailyQuestCp+=100;FlowerBed(frozen);ItemHandler.UseItem(frozenSeed,frozen);}
        finally {Config.DailyQuestCp=savedCp;}
        Check(!Fenced(frozen) && Flowers(frozen)==1 && frozen.Entity.ConquerPoints==1000+savedCp,
            "issued seed keeps its recorded reward when operator configuration changes before planting");
        foreach (string state in new[] {"remote","trade","dead","fake","unloaded","below-level","replaced"}) {
            var p=GardenPlayer(garden);uint hp=p.Entity.Hitpoints;
            if(state=="remote") {p.Entity.X=1;p.Entity.Y=1;}
            if(state=="trade")p.Trade.InTrade=true;if(state=="dead")p.Entity.Hitpoints=0;
            if(state=="fake")p.Fake=true;if(state=="unloaded")p.Entity.FullyLoaded=false;
            if(state=="below-level")p.Entity.Level=(byte)(Config.DailyMagnoliaMinLevel-1);
            if(state=="replaced")Kernel.GamePool[p.Entity.UID]=new GameState(null);
            FlowerNpc(p,1);FlowerNpc(p,4);
            Check(!Fenced(p) && p.Entity.ConquerPoints==1000 && p.Inventory.Count==0 && FlowerRuns(p)==0,
                "ineligible seed request cannot charge or issue: "+state);
            if(state=="replaced")Kernel.GamePool[p.Entity.UID]=p;
            p.Trade.InTrade=false;p.Entity.Hitpoints=hp;p.Fake=false;p.Entity.FullyLoaded=true;p.Entity.Level=135;
        }
        var minimum=GardenPlayer(garden);minimum.Entity.Level=Config.DailyMagnoliaMinLevel;FlowerSeed(minimum);
        Check(!Fenced(minimum) && FlowerRuns(minimum)==1 && minimum.Inventory.Count==1, "seed service accepts the exact configured minimum level");
        foreach (string state in new[] {"remote","wrong-map","curse","locked","suspicious","substituted","expired"}) {
            var p=GardenPlayer(garden);var seed=FlowerSeed(p);FlowerBed(p);
            if(state=="remote") {p.Entity.X=1;p.Entity.Y=1;}
            if(state=="wrong-map")p.BindWorldMap(1002);
            if(state=="curse")p.Entity.AddFlag((ulong)FlagsUpdate.Flags.Cursed,60,false);
            if(state=="locked")seed.Lock=1;if(state=="suspicious")seed.Suspicious=true;
            if(state=="expired") {seed.Days=1;seed.DayStamp=DateTime.Now.AddDays(-2);}
            ItemHandler.UseItem(state=="substituted" ? new ConquerItem(true){UID=seed.UID,ID=seed.ID} : seed,p);
            Check(!Fenced(p) && Flowers(p)==0 && p.Inventory.ContainsUID(seed.UID) && p.Entity.ConquerPoints==1000
                && p.Entity.Experience==5000, "ineligible planting preserves the seed and all rewards: "+state);
        }
        foreach (string currency in new[] {"cp","rvp","study"}) {
            var p=GardenPlayer(garden);var seed=FlowerSeed(p);FlowerBed(p);
            if(currency=="cp")p.Entity.ConquerPoints=uint.MaxValue;
            if(currency=="rvp")p.Entity.RevoraPoint=uint.MaxValue;
            if(currency=="study")p.Entity.SubClasses.StudyPoints=ushort.MaxValue;
            EntityTable.SaveEntity(p);ItemHandler.UseItem(seed,p);
            Check(!Fenced(p) && Flowers(p)==0 && p.Inventory.ContainsUID(seed.UID) && p.Entity.Experience==5000,
                "full "+currency+" balance preserves the seed and whole daily reward for a later claim");
        }
        var full=GardenPlayer(garden);var fullSeed=FlowerSeed(full);
        while(full.Inventory.Count<40)Pack(full,1000000,1);FlowerBed(full);ItemHandler.UseItem(fullSeed,full);
        Check(!Fenced(full) && Flowers(full)==0 && full.Inventory.ContainsUID(fullSeed.UID),"new harvest needs a free slot beside the consumed seed");
        DisRemove(full,full.Inventory.Objects.First(i=>i.ID==1000000));ItemHandler.UseItem(fullSeed,full);
        Check(!Fenced(full) && Flowers(full)==1 && full.Inventory.Count==40 && !full.Inventory.ContainsUID(fullSeed.UID),
            "consumed seed makes room for the daily Chi Token");
        ItemHandler.UseItem(fullSeed,full);
        Check(!Fenced(full) && Flowers(full)==1 && full.Inventory.Count==40,"full inventory replay preserves exactly one daily reward");
        var fullStack=GardenPlayer(garden);var stacked=Pack(fullStack,729307,2);
        while(fullStack.Inventory.Count<40)Pack(fullStack,1000000,1);FlowerBed(fullStack);ItemHandler.UseItem(stacked,fullStack);
        Check(!Fenced(fullStack) && Flowers(fullStack)==0 && stacked.StackSize==2, "full bag with a stacked seed preserves its unclaimed reward");
        var cap=GardenPlayer(garden);cap.Entity.Level=Config.LevelCap;var capSeed=FlowerSeed(cap);FlowerBed(cap);ItemHandler.UseItem(capSeed,cap);
        Check(!Fenced(cap) && Flowers(cap)==1 && cap.Entity.Experience==5000 && cap.Entity.ConquerPoints==1000+Config.DailyQuestCp,
            "maximum-level gardener receives the configured non-EXP reward without losing saved EXP");
        var empty=GardenPlayer(garden);
        Query("UPDATE entities SET conquerpoints=NULL,revorapoint=NULL,studypoints=NULL WHERE uid=@uid","uid",(long)empty.Entity.UID);
        empty=ReloadBall(empty);empty.BindWorldMap(1806);FlowerStand(empty);var emptySeed=FlowerSeed(empty);FlowerBed(empty);ItemHandler.UseItem(emptySeed,empty);
        Check(!Fenced(empty) && Flowers(empty)==1 && empty.Entity.ConquerPoints==Config.DailyQuestCp
            && empty.Entity.RevoraPoint==Config.DailyQuestRevoraPoints && empty.Entity.SubClasses.StudyPoints==Config.DailyMagnoliaStudy,
            "legacy nullable balances loaded as zero can complete the normal quest atomically");
        Query("CREATE TABLE IF NOT EXISTS apprentice AS TABLE public.apprentice WITH NO DATA");
        var mentor=GardenPlayer(garden);var child=GardenPlayer(garden);MentorLink(mentor,child,100);
        var childSeed=FlowerSeed(child);child.Entity.Experience=DataHolder.LevelExperience(135)-1;EntityTable.SaveEntity(child);FlowerBed(child);
        Query("CREATE FUNCTION suppress_flower() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_flower BEFORE UPDATE ON apprentice FOR EACH ROW EXECUTE FUNCTION suppress_flower()");
        ItemHandler.UseItem(childSeed,child);
        Check(Fenced(child) && Flowers(child)==0 && child.Entity.ConquerPoints==1000 && child.Entity.Level==135
            && child.Inventory.ContainsUID(childSeed.UID) && MentorBalance(child)==100, "mentor write failure preserves the seed and entire daily bundle");
        Query("DROP TRIGGER suppress_flower ON apprentice;DROP FUNCTION suppress_flower()");
        child=ReloadBall(child);child.BindWorldMap(1806);FlowerBed(child);Revora.Database.KnownPersons.LoadMentor(child);ItemHandler.UseItem(child.Inventory.Objects.Single(),child);
        Check(!Fenced(child) && Flowers(child)==1 && child.Entity.Level==136 && MentorBalance(child)==372,
            "ordinary reconnect retries daily progression and mentor credit together");
        var bound=GardenPlayer(garden);var boundSeed=Pack(bound,729307,1);boundSeed.Bound=true;
        Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)boundSeed.UID);FlowerBed(bound);ItemHandler.UseItem(boundSeed,bound);
        Check(!Fenced(bound) && Flowers(bound)==1 && bound.Inventory.Contains(Config.DailyQuestChiToken,1)
            && bound.Inventory.Objects.Single(i=>i.ID==Config.DailyQuestChiToken).Bound, "legacy bound seed cannot become a tradable Chi Token");
        var zero=GardenPlayer(garden);var zeroSeed=Pack(zero,729310,0);FlowerBed(zero);ItemHandler.UseItem(zeroSeed,zero);ItemHandler.UseItem(zeroSeed,zero);
        Check(!Fenced(zero) && Flowers(zero)==1 && !zero.Inventory.ContainsUID(zeroSeed.UID) && zero.Inventory.Count==2,
            "legacy zero-stack seed remains a single owned planting, with replay rejected");
        var zeroStudy=GardenPlayer(garden);var noStudySeed=FlowerSeed(zeroStudy);
        Query("UPDATE revora_magnolia_runs SET study=0 WHERE uid=@uid","uid",(long)zeroStudy.Entity.UID);FlowerBed(zeroStudy);ItemHandler.UseItem(noStudySeed,zeroStudy);
        Check(!Fenced(zeroStudy) && Flowers(zeroStudy)==1 && zeroStudy.Entity.SubClasses.StudyPoints==0 && StudyEvidence(zeroStudy)==0,
            "a preserved zero-study contract grants its other rewards without inventing first-study evidence");
        VerifyMagnoliaGrades(garden,runtime);
        VerifyMagnoliaContestEarning(garden,town);


    }

    static void VerifyMagnoliaContestEarning(Map garden,Map town)
    {
        town=Kernel.Maps[1002];
        var contest=typeof(Entity).Assembly.GetType("Revora.Game.Features.MrMs2017");
        Call(contest,null,"Initialize");
        Query("ALTER TABLE entities ADD COLUMN IF NOT EXISTS body integer NOT NULL DEFAULT 1003");
        DateTime savedNow=flowerNow;
        var fields=new[]{Magnolia.GetField("SeedRoll",Hidden),Magnolia.GetField("BonusRoll",Hidden),contest.GetField("Clock",Hidden),TitleRules.GetField("Clock",Hidden)};
        var saved=fields.Select(f=>f.GetValue(null)).ToArray();
        Revora.Interfaces.INpc oldNpc;town.Npcs.TryGetValue(380,out oldNpc);
        try {
            flowerNow=new DateTime(2052,1,15,19,0,0,DateTimeKind.Utc);
            fields[0].SetValue(null,(Func<int,int>)(max=>80));
            fields[1].SetValue(null,(Func<int,int>)(max=>max==100?0:1));
            fields[2].SetValue(null,(Func<DateTime>)(()=>flowerNow));
            fields[3].SetValue(null,(Func<DateTime>)(()=>flowerNow));
            var donor=GardenPlayer(garden);donor.Entity.ConquerPoints=0;
            Check(EntityTable.SaveEntity(donor),"contest earning starts with durably zero CPs");
            var free=FlowerSeed(donor);FlowerBed(donor);ItemHandler.UseItem(free,donor);
            Check(!Fenced(donor) && donor.Entity.ConquerPoints==Config.DailyQuestCp && Flowers(donor)==1,"free normal Magnolia earns seed-upgrade CPs");
            var winners=new GameState[2];
            for(int category=0;category<2;category++) {
                flowerNow=flowerNow.AddDays(1);donor.BindWorldMap(1806);FlowerStand(donor);
                FlowerSeed(donor);uint before=donor.Entity.ConquerPoints;
                FlowerWire(donor,5);FlowerWire(donor,6);
                var elite=donor.Inventory.Objects.Single(i=>i.ID==729308);
                Check(!Fenced(donor) && donor.Entity.ConquerPoints==before-1 && !elite.Bound,"earned CP pays one seed upgrade and preserves unbound seed");
                FlowerBed(donor);ItemHandler.UseItem(elite,donor);donor=ReloadBall(donor);
                var gift=donor.Inventory.Objects.Single(i=>i.ID==753003);
                Check(!Fenced(donor) && !gift.Bound,"planted earned Elite seed produces durable unbound contest orchids");
                var winner=ProgressPlayer(town,"");winner.Entity.Body=(ushort)(category==0?2001:1003);
                Query("UPDATE entities SET body=@body WHERE uid=@uid","body",(int)winner.Entity.Body,"uid",(long)winner.Entity.UID);
                winner.ActiveNpc=380;town.Npcs[380]=new NpcSpawn{UID=380,MapID=1002,X=winner.Entity.X,Y=winner.Entity.Y};
                Call(contest,null,"Page",winner,(byte)240);Call(contest,null,"Page",winner,(byte)241);
                donor.BindWorldMap(1002);donor.Entity.X=winner.Entity.X;donor.Entity.Y=winner.Entity.Y;donor.ActiveNpc=380;
                Call(contest,null,"Page",donor,(byte)240);Call(contest,null,"Page",donor,category==0?(byte)244:(byte)243);
                Call(contest,null,"Page",donor,(byte)180);Call(contest,null,"Page",donor,(byte)190);
                Check(((string)Call(contest,null,"Page",donor,(byte)247)).Contains("have been delivered") && !donor.Inventory.ContainsUID(gift.UID),"ordinary NPC consumes the Magnolia-earned flower");
                Check(Scalar("SELECT sum(points) FROM revora_mrms2017_donations WHERE candidate=@uid",winner.Entity.UID)==3,"earned flower credits the selected contest category");
                winners[category]=winner;
            }
            flowerNow=(DateTime)Call(contest,null,"CloseOf","2052-01");Call(contest,null,"ProcessDue");
            for(int category=0;category<2;category++) {
                var winner=winners[category];new TitleStorage().Login(winner);
                int champion=category==0?20402040:20412041,local=category==0?20442044:20452045;
                Check(OwnedTitles(winner).ContainsKey(champion) && OwnedTitles(winner).ContainsKey(local) && OwnedTitles(winner).ContainsKey(60126012),"Magnolia-funded category winner recovers both titles and permanent Romance wings");
            }
        } finally {
            flowerNow=savedNow;for(int i=0;i<fields.Length;i++)fields[i].SetValue(null,saved[i]);
            if(oldNpc==null)town.Npcs.Remove(380);else town.Npcs[380]=oldNpc;
        }
    }
}
