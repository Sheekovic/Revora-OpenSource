using System;
using System.IO;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static ConquerItem BuyRouteRune(GameState p,byte option) {
        var before=p.Inventory.Objects.Select(i=>i.UID).ToArray();
        Npcs.GetDialog(new NpcRequest(true){NpcID=Revora.Game.Features.RevoraPointShopSystem.NpcId,OptionID=option},p);
        var bought=p.Inventory.Objects.Where(i=>!before.Contains(i.UID)).ToArray();
        if(Fenced(p) || bought.Length!=1)throw new Exception("Ordinary rune purchase failed at option "+option);
        return bought[0];
    }
    static void VerifyRuneAcquisition(string runtime,Map town,string fixture,bool collections=false,bool equipmentSets=false) {
        Query("DROP TABLE entities;CREATE TABLE entities AS TABLE public.entities WITH NO DATA;CREATE TABLE titles (LIKE public.titles INCLUDING ALL)");
        foreach(string table in new[]{"teamarena","championarena","quests","status","reward","rewardclameid","merchant","dialyquests","activeness","house","pkexploits","achievement","itemadding"})
            Query("CREATE TABLE IF NOT EXISTS "+table+" AS TABLE public."+table+" WITH NO DATA");
        Query("CREATE TABLE prizes(uid bigint PRIMARY KEY,name text,prizes bytea)");
        StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
        if(collections && !equipmentSets)VerifyWardrobeCatalogRepair();
        RunesSystem.LoadRunesEXP();SpellTable.Load();Call(EvidenceType,null,"Initialize");
        foreach(ushort map in new ushort[]{1011,1020,1000,1004})World(runtime,map);
        ConquerItem.ItemUID=new Counter(7900000);Server.GamePool=new GameState[0];
        var p=FullRunePlayer(town,15);p.Entity.DailySignDays=new byte[0];
        Check(EntityTable.SaveEntity(p) && p.Account.State==AccountTable.AccountState.Player && p.Entity.Reborn==2
            && p.Entity.RevoraPoint==0 && p.Entity.ConquerPoints==0 && p.Inventory.Count==0 && p.RunesItem.Count==0,
            "qualified ordinary character starts with no currency, rune or upgrade materials");
        new TitleStorage().Login(p);
        Directory.CreateDirectory(Path.Combine(fixture,"database"));Environment.CurrentDirectory=fixture;
        dailyNow=new DateTime(2032,1,1,0,0,0,DateTimeKind.Local).ToUniversalTime();
        Daily.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>dailyNow));Call(Daily,null,"Create");
        int kills=0;var first=dailyNow;
        int needed=collections?1:83;
        while(kills<needed) {
            DailyTick(first.AddDays(kills/4));
            var schedule=DailySchedule().Where(t=>t.ToLocalTime().Date==dailyNow.ToLocalTime().Date).ToArray();
            foreach(var planned in schedule) {
                if(kills==needed)break;DailyTick(planned);var boss=DailyActive();
                if(boss==null)throw new Exception("Scheduled acquisition boss did not spawn.");
                p.Entity.MapID=boss.MapID;p.Entity.X=boss.X;p.Entity.Y=boss.Y;
                uint damage=boss.MaxHitpoints;
                Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,boss,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=boss.UID},ref damage,null);
                if(DailyActive()!=null || Fenced(p) || p.Entity.RevoraPoint!=(kills+1)*50000u)
                    throw new Exception("Scheduled boss combat did not commit its unique bounty.");
                kills++;
            }
        }
        Check(Scalar("SELECT count(*) FROM revora_daily_boss_runs WHERE killer=@uid AND paid AND state=2",p.Entity.UID)==needed
            && p.Entity.RevoraPoint==needed*50000u && p.Entity.ConquerPoints==needed*1000000u && p.Entity.Money==(ulong)needed*10000000u,
            needed+" distinct scheduled boss kills earn the complete purse through combat and durable bounties");
        Check(Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",p.Entity.UID)==needed*50000u,
            "earned RVP balance is durable before spending");
        p.Entity.MapID=1002;p.Entity.X=396;p.Entity.Y=340;p.ActiveNpc=Revora.Game.Features.RevoraPointShopSystem.NpcId;
        town.AddNpc(new NpcSpawn{UID=p.ActiveNpc,MapID=1002,X=396,Y=340,Mesh=50110,Name="Vesper",Type=Enums.NpcType.Talker});
        if(equipmentSets){VerifyEarnedEquipmentSets(p,runtime);return;}
        if(collections){VerifyPurchasedCollections(p);return;}
        byte[] choices={180,190,200,201,202};var runes=new ConquerItem[5];
        for(int i=0;i<runes.Length;i++){runes[i]=BuyRouteRune(p,choices[i]);RuneMove(p,runes[i],61);}
        Check(runes.All(i=>i.Position==211 && i.ID%100==1) && p.Entity.RevoraPoint==4146750,
            "real NPC dispatcher sells the matching red/blue and three distinct yellow runes for 3250 earned RVPs");
        int purchases=0;
        for(int r=1;r<5;r++) {
            // Independent expected cumulative cost from the installed level table:
            // blue 341500-100; each yellow 104600-100, with 100 per level-one material.
            int remaining=r==1?3414:1045;
            while(remaining>0) {
                int count=Math.Min(40,remaining);var materials=new uint[count];
                for(int i=0;i<count;i++)materials[i]=BuyRouteRune(p,choices[r]).UID;
                var packet=ComposePacket(runes[r].UID,materials);RunesSystem.HandlePacket1009(p,packet);
                if(Fenced(p) || p.Inventory.Count!=0)throw new Exception("Bought rune materials did not compose atomically.");
                uint id=runes[r].ID,exp=runes[r].RunesEXP,balance=p.Entity.RevoraPoint;
                RunesSystem.HandlePacket1009(p,packet);
                if(runes[r].ID!=id || runes[r].RunesEXP!=exp || p.Entity.RevoraPoint!=balance)
                    throw new Exception("Consumed-material replay changed the route.");
                remaining-=count;purchases+=count;
            }
            Check(runes[r].ID%100==(r==1?27u:9u) && runes[r].RunesEXP==0
                && SavedRune(runes[r],"id")==runes[r].ID && SavedRune(runes[r],"runesexp")==0,
                "purchased level-one materials reach and persist the exact maximum for rune "+r);
        }
        Check(purchases==6549 && p.Entity.RevoraPoint==18750 && Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",p.Entity.UID)==18750
            && Scalar("SELECT count(*) FROM items WHERE entityid=@uid",p.Entity.UID)==5 && p.Inventory.Count==0,
            "6549 paid materials are consumed once; exact total cost is 4131250 RVPs and only five owned runes remain");
        Check(!EligibleTitles(p).Contains(90001) && !HasAchievement(p,11312),"maximum runes still in their bag do not award equipped progress");
        for(int i=0;i<5;i++)RuneMove(p,runes[i],57,(uint)(101+i));
        Check(!Fenced(p) && OwnedTitles(p).ContainsKey(90001) && HasAchievement(p,11310) && HasAchievement(p,11311) && HasAchievement(p,11312),
            "actual earned purchases and upgrades unlock Chosen One and rune achievements on final equip");
        TitleRequest(90001,TitleStorage.Action.Equip).Handle(p);
        Check(p.Entity.EquippedTitle==90001 && OwnedTitles(p)[90001] && p.Entity.WTitles.Points==500,
            "ordinary wardrobe request equips earned Chosen One with exactly 500 points");
        Check(EntityTable.SaveEntity(p),"ordinary save succeeds after the complete rune earning route");
        var loaded=ReloadGold(p);ConquerItemTable.LoadItems(loaded);loaded.LoadItemStats();
        loaded.Entity.WTitles=null;StorageManager.Load();new TitleStorage().Login(loaded);
        Check(loaded.Account.State==AccountTable.AccountState.Player && loaded.Entity.RevoraPoint==18750 && loaded.RunesItem.Count==5
            && loaded.RunesItem.Values.All(i=>i.Position>=101 && i.Position<=105) && OwnedTitles(loaded).ContainsKey(90001)
            && HasAchievement(loaded,11312) && loaded.Entity.EquippedTitle==90001 && OwnedTitles(loaded)[90001],
            "production character, item and uncached wardrobe reload restores the equipped title and exact remaining purse");
        TitleRequest(90001,TitleStorage.Action.Equip).Handle(loaded);new TitleStorage().Login(loaded);
        Check(loaded.Entity.WTitles.Points==500 && Scalar("SELECT points FROM titles WHERE id=@uid",loaded.Entity.UID)==500
            && loaded.Entity.RevoraPoint==18750,"repeated equip and login preserve title points and cannot repeat a charge or grant");
    }
    static void VerifyWardrobeCatalogRepair() {
        var repair=typeof(Entity).Assembly.GetType("Revora.Game.Features.WardrobeCatalogRepair");
        var rows=(uint[,])repair.GetField("Missing",Hidden).GetValue(null);
        bool matched=true;
        for(int i=0;i<rows.GetLength(0);i++) {
            StorageItem entry;var catalog=rows[i,1]==1?StorageItem.Garments:StorageItem.Mounts;
            matched &= catalog.TryGetValue(rows[i,0],out entry) && entry.ID==rows[i,0]
                && entry.Type==rows[i,1] && entry.Unknown==rows[i,2] && entry.Stars==rows[i,3];
        }
        Check(rows.GetLength(0)==148 && matched,"loaded wardrobe restores all 148 compatible omissions with reviewed category index and stars");
        int garments=StorageItem.Garments.Count,mounts=StorageItem.Mounts.Count;
        var existing=StorageItem.Garments[189695];Call(repair,null,"Restore");
        Check(StorageItem.Garments.Count==garments && StorageItem.Mounts.Count==mounts && ReferenceEquals(existing,StorageItem.Garments[189695]),
            "catalog repair is idempotent and preserves existing server metadata");
        uint id=rows[0,0];var items=ConquerItemInformation.BaseInformations;var info=items[id];
        var target=rows[0,1]==1?StorageItem.Garments:StorageItem.Mounts;var original=target[id];
        target.Remove(id);items.Remove(id);
        try {Call(repair,null,"Restore");Check(!target.ContainsKey(id),"catalog repair does not create a cosmetic without its loaded item definition");}
        finally {items[id]=info;target[id]=original;}
        Check(Enumerable.Range(200618,7).All(i=>items.ContainsKey((uint)i)),
            "secondary item catalog supplies existing mount definitions without repair synthesizing items");
    }
    static void VerifyPurchasedCollections(GameState p) {
        Check(!HasAchievement(p,11224) && !HasAchievement(p,11225),"empty wardrobe grants no collection achievement");
        byte[] options={70,71,72,73,74,80,83,82,94,95,100};
        uint[] ids={189695,194875,187405,189065,188915,200517,200614,200613,200559,200560,200596};
        for(int i=0;i<options.Length;i++) {
            var item=BuyRouteRune(p,options[i]);
            Check(item.ID==ids[i] && !item.Bound && item.Days==0 && item.TimeLeftInMinutes==0
                && p.Entity.RevoraPoint==50000u-(uint)(i+1)*2500,
                "earned bounty buys permanent unbound collection design at exact Vesper price: "+ids[i]);
            var coat=new CoatStorage{Info=new CoatStorageProto{ActionId=CoatStorage.Action.Addtowardrobe,ItemGuid=item.UID,ItemId=(int)item.ID}};
            if(i==0) {
                coat.Info.ItemId++;
                coat.Handle(p);
                Check(!item.InWardrobe && p.Inventory.ContainsUID(item.UID) && SavedRune(item,"inwardrobe")==0,
                    "wardrobe deposit rejects mismatched requested design before mutation");
                item.ID=1088001;coat.Info.ItemId=(int)item.ID;
                coat.Handle(p);
                Check(!item.InWardrobe && p.Inventory.ContainsUID(item.UID) && !p.Entity.StorageItems.ContainsKey(item.UID),
                    "non-cosmetic bag item cannot enter wardrobe");
                item.ID=ids[i];coat.Info.ItemId=(int)item.ID;
                p.Trade.InTrade=true;coat.Handle(p);p.Trade.InTrade=false;
                Check(!item.InWardrobe && SavedRune(item,"inwardrobe")==0,"wardrobe deposit rejects active trade");
                Query("CREATE FUNCTION suppress_coat_deposit() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.inwardrobe=1 THEN RETURN NULL; END IF; RETURN NEW; END $$");
                Query("CREATE TRIGGER suppress_coat_deposit BEFORE UPDATE ON items FOR EACH ROW EXECUTE FUNCTION suppress_coat_deposit()");
                try {
                    coat.Handle(p);
                    Check(Fenced(p) && !item.InWardrobe && p.Inventory.ContainsUID(item.UID)
                        && !p.Entity.StorageItems.ContainsKey(item.UID) && SavedRune(item,"inwardrobe")==0,
                        "suppressed wardrobe write fences session without granting live collection or removing bag item");
                } finally { Query("DROP TRIGGER suppress_coat_deposit ON items");Query("DROP FUNCTION suppress_coat_deposit()"); }
                p=ReloadGold(p);ConquerItemTable.LoadItems(p);new CoatStorage().Login(p);p.Entity.WTitles=null;new TitleStorage().Login(p);
                Check(p.Inventory.TryGetItem(coat.Info.ItemGuid,out item) && !item.InWardrobe && !Fenced(p),
                    "production reload recovers failed deposit as the original bag item");
                p.BindWorldMap(1002);p.Entity.X=396;p.Entity.Y=340;p.ActiveNpc=900000014;
            }
            if(i==1) {
                Query("UPDATE items SET warehouse=1 WHERE uid=@uid","uid",(long)item.UID);
                coat.Handle(p);
                Check(Fenced(p) && !item.InWardrobe && p.Inventory.ContainsUID(item.UID)
                    && !p.Entity.StorageItems.ContainsKey(item.UID) && SavedRune(item,"inwardrobe")==0,
                    "stale bag item saved in warehouse cannot become a wardrobe collection");
                Query("UPDATE items SET warehouse=0 WHERE uid=@uid","uid",(long)item.UID);
                p=ReloadGold(p);ConquerItemTable.LoadItems(p);new CoatStorage().Login(p);p.Entity.WTitles=null;new TitleStorage().Login(p);
                Check(p.Inventory.TryGetItem(coat.Info.ItemGuid,out item) && !item.InWardrobe && !Fenced(p),
                    "restored durable bag location reloads without a phantom wardrobe deposit");
                p.BindWorldMap(1002);p.Entity.X=396;p.Entity.Y=340;p.ActiveNpc=900000014;
            }
            coat.Handle(p);coat.Handle(p);
            if(i==0)Check(HasAchievement(p,11225) && !HasAchievement(p,11224),"first earned permanent garment immediately grants only garment milestone");
            if(i==5)Check(HasAchievement(p,11225) && HasAchievement(p,11224),"first earned permanent mount immediately grants its separate milestone");
            Check(!Fenced(p) && p.Entity.StorageItems.ContainsKey(item.UID) && !p.Inventory.ContainsUID(item.UID)
                && SavedRune(item,"inwardrobe")==1,"purchased design enters the wardrobe once: "+ids[i]);
            if(i==3)Check(!OwnedTitles(p).ContainsKey(20050006),"four bought garments cannot unlock Fashionist");
            if(i==4)Check(OwnedTitles(p).ContainsKey(20050006),"fifth earned garment unlocks Fashionist");
            if(i==8)Check(!OwnedTitles(p).ContainsKey(20060007),"four bought mounts cannot unlock Swift Chaser");
        }
        int[] titles={20050006,20060007,20130014,20140015,20150016,20512051,20632063,20652065};
        Check(titles.All(k=>OwnedTitles(p).ContainsKey(k)) && !OwnedTitles(p).ContainsKey(20160017),
            "earned collection unlocks five-design monkey fox and male romance titles without claiming all eight monkey variants");
        Check(!OwnedTitles(p).ContainsKey(20642064) && !OwnedTitles(p).ContainsKey(20502050),
            "male romance set and Fantasy fox do not grant female or base fox rewards");
        Check(EntityTable.SaveEntity(p),"earned collection character saves before production reload");
        p=ReloadGold(p);ConquerItemTable.LoadItems(p);new CoatStorage().Login(p);p.LoadItemStats();p.Entity.WTitles=null;new TitleStorage().Login(p);
        Check(p.Account.State==AccountTable.AccountState.Player && p.Entity.RevoraPoint==22500
            && ids.All(id=>p.Entity.StorageItems.Values.Count(i=>i.ID==id)==1) && titles.All(k=>OwnedTitles(p).ContainsKey(k)),
            "production reload restores all purchased designs titles and exact remaining earned purse");
        foreach(int title in titles){TitleRequest(title,TitleStorage.Action.Equip).Handle(p);Check(p.Entity.EquippedTitle==title,"ordinary earned collection title equips: "+title);}
        new TitleStorage().Login(p);
        Check(p.Entity.RevoraPoint==22500 && Scalar("SELECT count(*) FROM items WHERE entityid=@uid",p.Entity.UID)==11,
            "collection refresh cannot duplicate purchases or repeat charges");
        p=VerifyCollectionWithdrawals(p);
        p=VerifyCollectionTakeoff(p);
        p=VerifyCollectionSwitching(p);
        VerifyCollectionAchievementRules(p);
        VerifyEarnedFashionPlaces(p);
        VerifyEarnedBigFan(p);
        VerifyEarnedTenCollections(Kernel.GamePool[p.Entity.UID]);
        VerifyEarnedFiftyGarments(Kernel.GamePool[p.Entity.UID]);
        VerifyEquipmentAchievementRules(Kernel.GamePool[p.Entity.UID]);
        VerifyEarnedTalismans(Kernel.GamePool[p.Entity.UID]);
        VerifyEarnedEnchantment(Kernel.GamePool[p.Entity.UID]);
        VerifyEnchantIdentityAndRetention(Kernel.GamePool[p.Entity.UID]);
        VerifyEquipmentUpgradeTransactions(Kernel.GamePool[p.Entity.UID]);
        VerifyEquipmentRepairs(Kernel.GamePool[p.Entity.UID]);
        VerifyDedicatedSockets(Kernel.Maps[1002]);
        VerifySocketInventoryBoundaries(Kernel.Maps[1002]);
        VerifyMountKit(Kernel.GamePool[p.Entity.UID]);
        VerifyEarnedBoothTrade(Kernel.GamePool[p.Entity.UID]);
        VerifyGuardTraining();
        VerifyEarnedGuardOre(Kernel.GamePool[p.Entity.UID]);
        VerifySummonCatalogFailure(Kernel.Maps[1002]);
        VerifyMarriageReload(Kernel.Maps[1002]);
    }
    static void VerifyMarriageReload(Map town) {
        var a=FullRunePlayer(town,15);var b=FullRunePlayer(town,135);
        a.Entity.Body=1003;b.Entity.Body=2001;a.Entity.Spouse=b.Entity.Spouse="None";
        a.Entity.DailySignDays=b.Entity.DailySignDays=new byte[0];
        a.Entity.Teleport(1002,400,350);b.Entity.Teleport(1002,401,350);
        Check(EntityTable.SaveEntity(a) && EntityTable.SaveEntity(b),"ordinary marriage fixtures persist their full characters before proposing");
        MarriageSend(a,b,Attack.MarriageRequest);MarriageSend(b,a,Attack.MarriageAccept);
        Check(a.Entity.Spouse==b.Entity.Name && b.Entity.Spouse==a.Entity.Name && !Fenced(a) && !Fenced(b),"ordinary full characters marry through the real handler");
        a=ReloadBall(a);b=ReloadBall(b);a.Entity.MyAchievement.Send();b.Entity.MyAchievement.Send();
        Check(a.Entity.Spouse==b.Entity.Name && b.Entity.Spouse==a.Entity.Name && HasAchievement(a,10129) && HasAchievement(b,10129),
            "full production character and achievement loading restore both spouse names and completions");
        Check((bool)Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.CouplePkEvent"),null,"Married",a,b),
            "earned and reloaded marriage satisfies the Couple PK spouse predicate");
        MarriageSend(b,a,Attack.MarriageAccept);
        Check(new[]{a,b}.All(p=>Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10129",p.Entity.UID)==1),
            "accept replay after full login preserves one marriage achievement each");
    }
    static void VerifyEarnedGuardOre(GameState p) {
        var roll=MiningService.GetField("Roll",Hidden);var clock=MiningService.GetField("Clock",Hidden);
        var oldRoll=roll.GetValue(null);var oldClock=clock.GetValue(null);int calls=0;
        try {
            var pick=p.Inventory.Objects.Single(i=>i.ID==562000);
            ItemHandler.EquipItem(new ItemUsage(true){UID=pick.UID,dwParam=4,Action=ItemUsage.Mode.EquipItem},p);
            p.Entity.Teleport(1002,Kernel.Maps[1002].Npcs[10080].X,Kernel.Maps[1002].Npcs[10080].Y);p.ActiveNpc=10080;
            ulong money=p.Entity.Money;MineTravel(p);
            Check(p.Entity.MapID==1028 && p.Entity.Money==money-100 && p.Equipment.TryGetItem((byte)4).ID==562000,
                "existing boss-funded miner pays another fare with its earned equipped Pickaxe: map="+p.Entity.MapID+" money="+p.Entity.Money+" before="+money+" weapon="+p.Equipment.TryGetItem((byte)4).ID+" fenced="+Fenced(p));
            DateTime now=DateTime.UtcNow;clock.SetValue(null,new Func<DateTime>(()=>now));
            roll.SetValue(null,new Func<int,int>(max=>max==100?++calls==2?99:0:max==10000?9999:0));
            long before=MineCount(p);StartMining(p);now=now.AddSeconds(2);MineTick(p);
            var ore=p.Inventory.Objects.Single(i=>i.ID==1072031);uint uid=ore.UID;
            Check(MineCount(p)==before+1 && Scalar("SELECT last_item FROM revora_mining_state WHERE uid=@uid",p.Entity.UID)==uid,
                "real mining tick produces Euxenite Ore with saved provenance");
            p.Mining=false;p.Entity.Teleport(1004,51,40);p.ActiveNpc=10000;
            Npcs.GetDialog(new NpcRequest(true){NpcID=10000,OptionID=88},p);
            Check(!Fenced(p) && p.Spells.ContainsKey(4000) && !p.Inventory.ContainsUID(uid)
                && Scalar("SELECT count(*) FROM skills WHERE entityid=@uid AND id=4000",p.Entity.UID)==1,
                "ordinary instructor consumes the mined ore and saves Summon Guard");
            p=ReloadBall(p);Check(p.Spells.ContainsKey(4000) && !p.Inventory.ContainsUID(uid),
                "mined-material guard learning survives production login without restoring the ore");
            p.Entity.Teleport(1002,400,350);p.Entity.Stamina=100;StanceCast(p,4000);
            Check(p.Companion!=null && ReferenceEquals(p.Companion.Owner,p) && p.Companion.Hitpoints>0 && HasAchievement(p,10119),
                "earned guard instruction leads to an actual owned summon and its achievement");
            p.Map.RemoveEntity(p.Companion);p.Companion=null;p=ReloadBall(p);p.Entity.MyAchievement.Send();
            Check(HasAchievement(p,10119) && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10119",p.Entity.UID)==1,
                "earned summon achievement survives dismissal and production login as one receipt");
        }finally{roll.SetValue(null,oldRoll);clock.SetValue(null,oldClock);}
    }
    static void VerifyGuardTraining() {
        var map=Kernel.Maps[1004];map.AddNpc(new NpcSpawn{UID=10000,MapID=1004,X=52,Y=40,Mesh=100,Type=Enums.NpcType.Talker});
        foreach(string mode in new[]{"single","stack","known","skill-failure","item-failure","stack-failure","not-reborn","saved-not-reborn","distant","trade","locked","stale-item"}) {
            var p=FullRunePlayer(map,15);p.Entity.DailySignDays=new byte[0];p.Entity.X=51;p.Entity.Y=40;p.ActiveNpc=10000;
            Check(EntityTable.SaveEntity(p),"guard student persists ordinary qualification: "+mode);
            var book=Pack(p,1072031,1);
            if(mode=="stack" || mode=="stack-failure"){book.StackSize=2;Query("UPDATE items SET stacksize=2 WHERE uid=@uid","uid",(long)book.UID);}
            if(mode=="known"){Check(p.AddSpell(new Spell(true){ID=4000,Level=0}),"known guard fixture owns its skill before another ore");SkillTable.SaveSpells(p);}
            if(mode=="not-reborn")p.Entity.Reborn=0;
            if(mode=="saved-not-reborn")Query("UPDATE entities SET reborn=0 WHERE uid=@uid","uid",(long)p.Entity.UID);
            if(mode=="distant")p.Entity.X=70;
            if(mode=="trade")p.Trade.InTrade=true;
            if(mode=="locked")book.Lock=1;
            if(mode=="stale-item")Query("UPDATE items SET warehouse=1 WHERE uid=@uid","uid",(long)book.UID);
            bool fault=mode.EndsWith("-failure");string table=mode=="skill-failure"?"skills":"items";
            if(fault)Query("CREATE FUNCTION suppress_guard_learning() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_guard_learning BEFORE "+(table=="skills"?"INSERT":mode=="stack-failure"?"UPDATE":"DELETE")+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_guard_learning()");
            try {System.Threading.Tasks.Parallel.For(0,4,n=>Npcs.GetDialog(new NpcRequest(true){NpcID=10000,OptionID=88},p));}
            finally {if(fault)Query("DROP TRIGGER suppress_guard_learning ON "+table+";DROP FUNCTION suppress_guard_learning()");}
            bool learned=mode=="single" || mode=="stack" || mode=="known";
            long remaining=Scalar("SELECT COALESCE(sum(stacksize),0) FROM items WHERE entityid=@uid AND id=1072031",p.Entity.UID);
            Check(p.Spells.ContainsKey(4000)==learned && Scalar("SELECT count(*) FROM skills WHERE entityid=@uid AND id=4000",p.Entity.UID)==(learned?1:0)
                && remaining==(mode=="single"?0:mode=="stack-failure"?2:1),
                "guard learning commits one skill and one consumed ore, or neither: "+mode);
            if(mode=="single" || mode=="stack" || fault) {
                p=ReloadBall(p);p.Entity.Teleport(1004,51,40);p.ActiveNpc=10000;
                Npcs.GetDialog(new NpcRequest(true){NpcID=10000,OptionID=88},p);
                Check(!Fenced(p) && p.Spells.ContainsKey(4000)
                    && Scalar("SELECT count(*) FROM skills WHERE entityid=@uid AND id=4000",p.Entity.UID)==1
                    && Scalar("SELECT COALESCE(sum(stacksize),0) FROM items WHERE entityid=@uid AND id=1072031",p.Entity.UID)==(mode=="stack" || mode=="stack-failure"?1:0),
                    "guard learning survives production login and retry without another ore charge: "+mode);
            }
        }
    }
    static void VerifySummonCatalogFailure(Map town) {
        var p=FullRunePlayer(town,15);p.Entity.DailySignDays=new byte[0];p.Entity.Mana=10000;p.Entity.Stamina=100;p.Entity.Hitpoints=p.Entity.MaxHitpoints=100000;
        Check(EntityTable.SaveEntity(p) && p.AddSpell(new Spell(true){ID=4000,Level=0}),"ordinary summon fixture saves its supplied guard skill");SkillTable.SaveSpells(p);
        var families=SpellTable.SpellInformations[4000].Values.Select(i=>i.Power).Distinct().ToArray();
        var saved=new System.Collections.Generic.Dictionary<uint,MonsterInformation>();
        foreach(uint family in families){MonsterInformation info;if(MonsterInformation.MonsterInformations.TryGetValue(family,out info))saved.Add(family,info);}
        try {
            foreach(uint family in families)MonsterInformation.MonsterInformations.Remove(family);
            uint mana=p.Entity.Mana;byte stamina=p.Entity.Stamina;
            StanceCast(p,4000);
            Check(p.Companion==null && p.Entity.Mana==mana && p.Entity.Stamina==stamina && !Fenced(p)
                && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10119",p.Entity.UID)==0,
                "missing guard definitions reject actual cast without resources, actor or exception");
            foreach(uint family in families)MonsterInformation.MonsterInformations[family]=new MonsterInformation{ID=family,Name="FixtureGuard",Hitpoints=0};
            StanceCast(p,4000);
            Check(p.Companion==null && p.Entity.Mana==mana && p.Entity.Stamina==stamina && !Fenced(p)
                && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10119",p.Entity.UID)==0,
                "zero-health guard definitions reject without consuming casting resources");
            foreach(uint family in families)MonsterInformation.MonsterInformations[family]=saved.ContainsKey(family)?saved[family]:new MonsterInformation{ID=family,Name="FixtureGuard",Hitpoints=1000,Mesh=951,Level=1};
            StanceCast(p,4000);
            Check(p.Companion!=null && ReferenceEquals(p.Companion.Owner,p) && p.Companion.Hitpoints>0
                && p.Companion.MapID==p.Entity.MapID && p.Entity.Stamina<stamina && !Fenced(p),
                "healthy guard definition reaches real owned companion creation and consumes casting stamina");
            System.Threading.Tasks.Parallel.For(0,4,n=>Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.GuardTraining"),null,"RecordSummon",p,p.Companion));
            Check(HasAchievement(p,10119) && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10119",p.Entity.UID)==1,
                "registered owned guard records one durable summon achievement");
            if(p.Companion!=null){p.Map.RemoveEntity(p.Companion);p.Companion=null;}
        } finally {
            foreach(uint family in families) {
                if(saved.ContainsKey(family))MonsterInformation.MonsterInformations[family]=saved[family];
                else MonsterInformation.MonsterInformations.Remove(family);
            }
        }
    }
    static void OpenEarnedBooth(GameState p) {
        var frame=new Data(true){UID=p.Entity.UID,ID=Data.OwnBooth}.ToArray();
        Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,frame.Length-8,frame);Data.Process(frame,p);
    }
    static void VerifyEarnedBoothTrade(GameState seller) {
        var buyer=FullRunePlayer(Kernel.Maps[1002],15);buyer.Entity.DailySignDays=new byte[0];
        Check(buyer.Entity.ConquerPoints==0 && buyer.Entity.Money==0 && EntityTable.SaveEntity(buyer),
            "booth buyer begins with no supplied purchase currency");
        var next=DailySchedule().Where(t=>t>dailyNow).OrderBy(t=>t).First();DailyTick(next);var boss=DailyActive();
        if(boss==null)throw new Exception("Booth earning boss did not spawn.");
        buyer.BindWorldMap(boss.MapID);buyer.Entity.X=boss.X;buyer.Entity.Y=boss.Y;uint damage=boss.MaxHitpoints;
        Revora.Game.Attacking.Handle.ReceiveAttack(buyer.Entity,boss,new Attack(true){AttackType=Attack.Melee,Attacker=buyer.Entity.UID,Attacked=boss.UID},ref damage,null);
        Check(!Fenced(buyer) && buyer.Entity.ConquerPoints==1000000 && buyer.Entity.Money==10000000
            && Scalar("SELECT count(*) FROM revora_daily_boss_runs WHERE killer=@uid AND paid",buyer.Entity.UID)==1,
            "ordinary booth buyer earns its actual CP and silver purses from one owned daily boss");
        var goods=seller.Inventory.Objects.Single(i=>i.ID==201009 && !i.Bound);
        Check(Scalar("SELECT count(*) FROM revora_euro_jersey_purchases WHERE uid=@uid AND item_type=201009",seller.Entity.UID)==1
            && ConquerItemTable.LoadItem(goods.UID).ID==201009 && BoothAchievement(seller)==0 && BoothAchievement(buyer)==0,
            "seller lists the original boss-funded mall Fan displaced by the earned mount kit");
        foreach(var p in new[]{seller,buyer}){p.Entity.Teleport(1002,400,350);if(p.Trade==null)p.Trade=new Revora.Game.ConquerStructures.Trade();}
        uint sellerCp=seller.Entity.ConquerPoints,buyerCp=buyer.Entity.ConquerPoints;ulong sellerGold=seller.Entity.Money,buyerGold=buyer.Entity.Money;
        OpenEarnedBooth(seller);
        if(seller.Booth==null)throw new Exception("Ordinary booth creation failed.");uint booth=seller.Booth.Base.UID;
        try {
            BoothItem.AddItemOnBooth(new ItemUsage(true){UID=goods.UID,BoothCost=100000,Action=ItemUsage.Mode.AddItemOnBoothForSilvers},seller);
            Check(seller.Booth.ItemList.ContainsKey(goods.UID),"ordinary listing dispatcher accepts the earned tradable Fan");
            System.Threading.Tasks.Parallel.For(0,4,n=>BoothItem.BuyFromBooth(new ItemUsage(true){UID=goods.UID,dwParam=booth},buyer));
            Check(!Fenced(buyer) && !Fenced(seller) && buyer.Entity.Money==buyerGold-100000 && seller.Entity.Money==sellerGold+100000
                && buyer.Entity.ConquerPoints==buyerCp && seller.Entity.ConquerPoints==sellerCp
                && buyer.Inventory.ContainsUID(goods.UID) && !seller.Inventory.ContainsUID(goods.UID)
                && BoothAchievement(buyer)==1 && BoothAchievement(seller)==0,
                "boss-earned silver buys the boss-funded Fan once and grants only the buyer achievement");
        } finally {ClearBooth(seller,booth);}
        buyer=ReloadBall(buyer);seller=ReloadBall(seller);buyer.Entity.MyAchievement.Send();
        Check(HasAchievement(buyer,10128) && buyer.Inventory.ContainsUID(goods.UID) && buyer.Entity.Money==buyerGold-100000
            && seller.Entity.Money==sellerGold+100000,"earned silver booth purchase survives production character and item login");
        foreach(var p in new[]{seller,buyer}){p.Entity.Teleport(1002,400,350);if(p.Trade==null)p.Trade=new Revora.Game.ConquerStructures.Trade();}
        OpenEarnedBooth(buyer);booth=buyer.Booth.Base.UID;
        try {
            BoothItem.AddItemOnBooth(new ItemUsage(true){UID=goods.UID,BoothCost=1000,Action=ItemUsage.Mode.AddItemOnBoothForConquerPoints},buyer);
            BoothItem.BuyFromBooth(new ItemUsage(true){UID=goods.UID,dwParam=booth},seller);
            BoothItem.BuyFromBooth(new ItemUsage(true){UID=goods.UID,dwParam=booth},seller);
            Check(!Fenced(buyer) && !Fenced(seller) && seller.Entity.ConquerPoints==sellerCp-1000 && buyer.Entity.ConquerPoints==buyerCp+1000
                && seller.Inventory.ContainsUID(goods.UID) && !buyer.Inventory.ContainsUID(goods.UID)
                && BoothAchievement(seller)==1 && BoothAchievement(buyer)==1,
                "reverse CP sale uses original earned funds and credits the new buyer exactly once");
        } finally {ClearBooth(buyer,booth);}
        seller=ReloadBall(seller);buyer=ReloadBall(buyer);seller.Entity.MyAchievement.Send();buyer.Entity.MyAchievement.Send();
        Check(HasAchievement(seller,10128) && HasAchievement(buyer,10128) && seller.Inventory.ContainsUID(goods.UID)
            && seller.Entity.ConquerPoints==sellerCp-1000 && buyer.Entity.ConquerPoints==buyerCp+1000
            && seller.Entity.Money==sellerGold+100000 && buyer.Entity.Money==buyerGold-100000,
            "both earned booth milestones, exact purses and original item UID survive production login");
    }
    static void VerifyEarnedEnchantment(GameState p) {
        string previous=Environment.CurrentDirectory;
        var rollField=MiningService.GetField("Roll",Hidden);var clockField=MiningService.GetField("Clock",Hidden);
        object oldRoll=rollField.GetValue(null),oldClock=clockField.GetValue(null);int oldRate=Config.MiningNormalGemBasisPoints;
        try {
            string runtime=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(Entity).Assembly.Location),"../../../bin/Debug"));
            Environment.CurrentDirectory=runtime;World(runtime,1028);var town=Kernel.Maps[1002];
            MiningNpc(town,1);MiningNpc(town,10080);ShopFile.Load();
            Call(BookShop,null,"Initialize");Call(MiningService,null,"Initialize");Call(MinePassage,null,"Initialize");
            Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.ItemEnchantment"),null,"Initialize");
            Check(p.Entity.Money==10000000 && !HasAchievement(p,10430),"enchanting route starts with the untouched earned boss gold and no enchanted-item achievement");
            p.BindWorldMap(1002);p.Entity.X=town.Npcs[1].X;p.Entity.Y=town.Npcs[1].Y;
            ItemHandler.HandleBuyFromNPC(BuyBook(562000,1,1),p);var pick=p.Inventory.Objects.Single(i=>i.ID==562000);
            ItemHandler.EquipItem(new ItemUsage(true){UID=pick.UID,dwParam=4,Action=ItemUsage.Mode.EquipItem},p);
            p.Entity.X=town.Npcs[10080].X;p.Entity.Y=town.Npcs[10080].Y;p.ActiveNpc=10080;MineTravel(p);
            Check(!Fenced(p) && p.Entity.MapID==1028 && p.Entity.Money==9998900 && p.Equipment.TryGetItem(pick.UID)!=null,
                "boss gold buys and equips the 1000-silver Pickaxe and pays the 100-silver mine fare");
            DateTime now=DateTime.UtcNow;clockField.SetValue(null,(Func<DateTime>)(()=>now));
            rollField.SetValue(null,(Func<int,int>)(max=>max==10000?0:max==100?99:max==3?1:0));Config.MiningNormalGemBasisPoints=100;
            StartMining(p);now=now.AddSeconds(2);MineTick(p);
            var gem=p.Inventory.Objects.Single(i=>i.ID==700011);uint gemUid=gem.UID;
            Check(MineCount(p)==1 && Scalar("SELECT last_item FROM revora_mining_state WHERE uid=@uid",p.Entity.UID)==gemUid,
                "production mining tick issues the selected Normal Dragon Gem with saved mining provenance");
            p.Mining=false;p.LoginAt=DateTime.MinValue;ItemHandler.UnequipItem(new ItemUsage(true){dwParam=4},p);
            ItemHandler.EnchantItem(new ItemUsage(true){UID=pick.UID,dwParam=gemUid,Action=ItemUsage.Mode.Enchant},p);
            Check(!Fenced(p) && !p.Inventory.ContainsUID(gemUid) && pick.Enchant>0
                && Scalar("SELECT count(*) FROM revora_item_enchantments e JOIN revora_mining_state m ON e.gem_uid=m.last_item AND e.uid=m.uid WHERE e.uid=@uid",p.Entity.UID)==1,
                "actual mined gem enchants the purchased tool and links its committed consumption to mining provenance");
            ItemHandler.EquipItem(new ItemUsage(true){UID=pick.UID,dwParam=4,Action=ItemUsage.Mode.EquipItem},p);
            Call(typeof(Achievement),p.Entity.MyAchievement,"Refresh",DateTime.UtcNow,true);
            Check(HasAchievement(p,10430),"boss-funded purchase and mined material earn the enchanted-equipment achievement");
            uint uid=pick.UID;p=ReloadGold(p);ConquerItemTable.LoadItems(p);new CoatStorage().Login(p);p.Entity.MyAchievement.Send();
            Check(!Fenced(p) && p.Entity.Money==9998900 && p.Equipment.TryGetItem(uid).Enchant>0 && HasAchievement(p,10430)
                && Scalar("SELECT count(*) FROM items WHERE uid=@uid",gemUid)==0,
                "production login preserves the earned enchanted tool, exact gold purse and consumed mining material");
        } finally {Environment.CurrentDirectory=previous;rollField.SetValue(null,oldRoll);clockField.SetValue(null,oldClock);Config.MiningNormalGemBasisPoints=oldRate;}
    }
    static byte[] DedicatedSocketFrame(uint target,params uint[] materials) {
        int length=95+4*materials.Length;var frame=new byte[length+8];
        Revora.Network.Writer.WriteUInt16((ushort)length,0,frame);Revora.Network.Writer.WriteUInt16(1009,2,frame);
        Revora.Network.Writer.WriteUInt32(target,8,frame);Revora.Network.Writer.WriteUInt16(43,20,frame);
        Revora.Network.Writer.WriteUInt32((uint)materials.Length,26,frame);
        for(int n=0;n<materials.Length;n++)Revora.Network.Writer.WriteUInt32(materials[n],91+4*n,frame);
        Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,length,frame);return frame;
    }
    static void VerifySocketService(string runtime,Map town) {
        Query("DROP TABLE entities;CREATE TABLE entities AS TABLE public.entities WITH NO DATA;CREATE TABLE titles (LIKE public.titles INCLUDING ALL)");
        foreach(string table in new[]{"teamarena","championarena","quests","status","reward","rewardclameid","merchant","dialyquests","activeness","house","pkexploits","achievement","itemadding"})
            Query("CREATE TABLE IF NOT EXISTS "+table+" AS TABLE public."+table+" WITH NO DATA");
        Query("CREATE TABLE prizes(uid bigint PRIMARY KEY,name text,prizes bytea)");
        StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
        RunesSystem.LoadRunesEXP();SpellTable.Load();Call(EvidenceType,null,"Initialize");
        foreach(ushort map in new ushort[]{1011,1020,1000,1004})World(runtime,map);
        ConquerItem.ItemUID=new Counter(7900000);Server.GamePool=new GameState[0];
        VerifyDedicatedSockets(town);
        VerifySocketInventoryBoundaries(town);
    }
    static void VerifyDedicatedSockets(Map town) {
        var service=typeof(Entity).Assembly.GetType("Revora.Game.Features.EquipmentSockets");Call(service,null,"Initialize");
        var p=FullRunePlayer(town,15);p.Entity.DailySignDays=new byte[0];if(!EntityTable.SaveEntity(p))throw new Exception("Socket fixture save failed.");
        var clock=service.GetField("Clock",Hidden);var roll=service.GetField("Roll",Hidden);var projection=service.GetField("BeforeProjection",Hidden);
        object oldClock=clock.GetValue(null),oldRoll=roll.GetValue(null),oldProjection=projection.GetValue(null);
        DateTime now=new DateTime(2041,1,1,0,0,0,DateTimeKind.Utc);clock.SetValue(null,(Func<DateTime>)(()=>now));roll.SetValue(null,(Func<int,int>)(max=>0));
        Action<byte[]> send=frame=>Revora.Network.PacketHandler.HandlePacket(frame,p);
        Func<long> receipts=()=>Scalar("SELECT count(*) FROM revora_equipment_sockets WHERE uid=@uid",p.Entity.UID);
        try {
            var weapon=Pack(p,410009,1);var meteor=Pack(p,1088001,1);var ball=Pack(p,1088000,2);
            send(DedicatedSocketFrame(weapon.UID,meteor.UID));
            Check(!Fenced(p) && weapon.SocketOne==0 && p.Inventory.ContainsUID(meteor.UID) && receipts()==0,"owned junk cannot pay for a weapon socket");
            var valid=DedicatedSocketFrame(weapon.UID,ball.UID);
            foreach(string fault in new[]{"short","length","seal","type","count","zero","self","duplicate"}) {
                var frame=(byte[])valid.Clone();
                if(fault=="short")frame=frame.Take(27).ToArray();if(fault=="length")frame[0]++;if(fault=="seal")frame[frame.Length-1]^=1;
                if(fault=="type")frame[2]++;if(fault=="count")frame[26]=5;if(fault=="zero")Revora.Network.Writer.WriteUInt32(0,91,frame);
                if(fault=="self")Revora.Network.Writer.WriteUInt32(weapon.UID,91,frame);
                if(fault=="duplicate")frame=DedicatedSocketFrame(weapon.UID,ball.UID,ball.UID,ball.UID,ball.UID,ball.UID);
                Call(service,null,"Handle",p,frame);Check(!Fenced(p) && receipts()==0 && ball.StackSize==2 && weapon.SocketOne==0,"socket parser refuses malformed request without mutation: "+fault);
            }
            System.Threading.Tasks.Parallel.For(0,8,n=>send(valid));
            Check(!Fenced(p) && weapon.SocketOne==Enums.Gem.EmptySocket && weapon.SocketTwo==0 && ball.StackSize==1 && receipts()==1
                && ConquerItemTable.LoadItem(ball.UID).StackSize==1,"concurrent first-weapon socket consumes one selected DragonBall stack unit and one receipt");
            now=now.AddSeconds(2);var five=Enumerable.Range(0,5).Select(n=>Pack(p,1088000,1).UID).ToArray();
            var mixed=(uint[])five.Clone();mixed[4]=meteor.UID;send(DedicatedSocketFrame(weapon.UID,mixed));
            Check(receipts()==1 && weapon.SocketTwo==0 && five.All(uid=>p.Inventory.ContainsUID(uid)),"unrelated carried DragonBalls cannot subsidize a mixed selected payment");
            send(DedicatedSocketFrame(weapon.UID,five));send(DedicatedSocketFrame(weapon.UID,five));p.Entity.MyAchievement.Send();
            Check(!Fenced(p) && weapon.SocketTwo==Enums.Gem.EmptySocket && five.All(uid=>!p.Inventory.ContainsUID(uid)) && receipts()==2
                && HasAchievement(p,10425) && HasAchievement(p,10426),"five exact DragonBalls open the weapon's second socket and both paid achievements once");
            var boots=Pack(p,160016,1);boots.Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)boots.UID);
            now=now.AddSeconds(2);send(DedicatedSocketFrame(boots.UID,ball.UID));Check(boots.SocketOne==0 && ball.StackSize==1,"nonweapon first socket rejects the one-DragonBall weapon price");
            var twelve=Enumerable.Range(0,12).Select(n=>Pack(p,1088000,1).UID).ToArray();send(DedicatedSocketFrame(boots.UID,twelve));
            Check(!Fenced(p) && boots.SocketOne==Enums.Gem.EmptySocket && twelve.All(uid=>!p.Inventory.ContainsUID(uid)),"twelve selected DragonBalls open a nonweapon first socket");
            var tough=Pack(p,1200005,2);tough.Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)tough.UID);
            now=now.AddSeconds(2);send(DedicatedSocketFrame(boots.UID,tough.UID));var star=p.Inventory.Objects.Single(i=>i.ID==1200006);
            Check(!Fenced(p) && boots.SocketTwo==0 && tough.StackSize==1 && star.Bound && ConquerItemTable.LoadItem(star.UID).Bound,
                "failed Tough Drill consumes one unit and atomically returns a Star Drill with source binding");
            var stars=new[]{star.UID}.Concat(Enumerable.Range(0,6).Select(n=>Pack(p,1200006,1).UID)).ToArray();now=now.AddSeconds(2);
            send(DedicatedSocketFrame(boots.UID,stars));Check(!Fenced(p) && boots.SocketTwo==Enums.Gem.EmptySocket && stars.All(uid=>!p.Inventory.ContainsUID(uid)),
                "seven selected Star Drills guarantee the nonweapon second socket without another roll");
            var drilled=Pack(p,160016,1);drilled.SocketOne=Enums.Gem.EmptySocket;
            Query("UPDATE items SET socketone=255 WHERE uid=@uid","uid",(long)drilled.UID);
            var drill=Pack(p,1200005,1);roll.SetValue(null,(Func<int,int>)(max=>320));now=now.AddSeconds(2);
            long beforeDrill=receipts();send(DedicatedSocketFrame(drilled.UID,drill.UID));
            Check(!Fenced(p) && drilled.SocketTwo==Enums.Gem.EmptySocket && !p.Inventory.ContainsUID(drill.UID)
                && receipts()==beforeDrill+1 && !p.Inventory.Objects.Any(i=>i.ID==1200006),"successful Tough Drill opens second socket without a failure refund");
            roll.SetValue(null,(Func<int,int>)(max=>0));
            var guarded=Pack(p,410009,1);var guardedBall=Pack(p,1088000,1);long guardReceipts=receipts();
            foreach(string guard in new[]{"trade","boothless-fake","locked-target","rental-target","bound-material","locked-material"}) {
                if(guard=="trade")p.Trade.InTrade=true;if(guard=="boothless-fake")p.Fake=true;
                if(guard=="locked-target")guarded.Lock=1;if(guard=="rental-target")guarded.TimeLeftInMinutes=1;
                if(guard=="bound-material")guardedBall.Bound=true;if(guard=="locked-material")guardedBall.Lock=1;
                send(DedicatedSocketFrame(guarded.UID,guardedBall.UID));
                Check(!Fenced(p) && guarded.SocketOne==0 && p.Inventory.ContainsUID(guardedBall.UID) && receipts()==guardReceipts,"ineligible socket request leaves payment untouched: "+guard);
                p.Trade.InTrade=false;p.Fake=false;guarded.Lock=0;guarded.TimeLeftInMinutes=0;guardedBall.Bound=false;guardedBall.Lock=0;
            }
            foreach(string fault in new[]{"material","target","receipt","achievement","refund"}) {
                now=now.AddSeconds(2);bool refund=fault=="refund";var item=Pack(p,refund?160016u:410009u,1);var material=Pack(p,refund?1200005u:1088000u,1);
                if(refund){item.SocketOne=Enums.Gem.EmptySocket;Query("UPDATE items SET socketone=255 WHERE uid=@uid","uid",(long)item.UID);}
                if(fault=="achievement")Query("DELETE FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10425","uid",(long)p.Entity.UID);
                uint uid=item.UID,materialUid=material.UID;long before=receipts();byte first=(byte)item.SocketOne;
                string table=fault=="receipt"?"revora_equipment_sockets":fault=="achievement"?"revora_achievement_evidence":"items";
                string verb=fault=="material"?"DELETE":fault=="target"?"UPDATE":"INSERT";
                Query("CREATE FUNCTION suppress_socket() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
                Query("CREATE TRIGGER suppress_socket BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_socket()");
                try{send(DedicatedSocketFrame(uid,materialUid));}finally{Query("DROP TRIGGER suppress_socket ON "+table);Query("DROP FUNCTION suppress_socket()");}
                Check(Fenced(p) && ConquerItemTable.LoadItem(uid).SocketOne==(Enums.Gem)first && ConquerItemTable.LoadItem(uid).SocketTwo==0
                    && Scalar("SELECT count(*) FROM items WHERE uid=@uid",materialUid)==1 && receipts()==before,"suppressed socket "+fault+" write rolls back payment, output and receipt");
                p=ReloadGold(p);ConquerItemTable.LoadItems(p);send(DedicatedSocketFrame(uid,materialUid));send(DedicatedSocketFrame(uid,materialUid));
                Check(!Fenced(p) && !p.Inventory.ContainsUID(materialUid) && receipts()==before+1,"full character reload retries socket payment exactly once after "+fault);
            }
            var projected=Pack(p,410009,1);var paid=Pack(p,1088000,1);uint projectedUid=projected.UID,paidUid=paid.UID;long prior=receipts();now=now.AddSeconds(2);
            projection.SetValue(null,(Action)(()=>{throw new InvalidOperationException("fixture socket projection fault");}));send(DedicatedSocketFrame(projectedUid,paidUid));projection.SetValue(null,null);
            Check(Fenced(p) && ConquerItemTable.LoadItem(projectedUid).SocketOne==Enums.Gem.EmptySocket && receipts()==prior+1,"postcommit socket projection failure preserves paid result");
            p=ReloadGold(p);ConquerItemTable.LoadItems(p);p.Entity.MyAchievement.Send();send(DedicatedSocketFrame(projectedUid,paidUid));
            Check(!Fenced(p) && p.Inventory.Objects.Single(i=>i.UID==projectedUid).SocketOne==Enums.Gem.EmptySocket && !p.Inventory.ContainsUID(paidUid)
                && HasAchievement(p,10425) && HasAchievement(p,10426) && receipts()==prior+1,"full login recovers committed socket and achievements without a repeated charge");
        }finally{clock.SetValue(null,oldClock);roll.SetValue(null,oldRoll);projection.SetValue(null,oldProjection);}
    }
    static void VerifySocketInventoryBoundaries(Map town) {
        var service=typeof(Entity).Assembly.GetType("Revora.Game.Features.EquipmentSockets");Call(service,null,"Initialize");
        var clock=service.GetField("Clock",Hidden);var roll=service.GetField("Roll",Hidden);
        object oldClock=clock.GetValue(null),oldRoll=roll.GetValue(null);
        DateTime now=new DateTime(2042,1,1,0,0,0,DateTimeKind.Utc);clock.SetValue(null,(Func<DateTime>)(()=>now));roll.SetValue(null,(Func<int,int>)(max=>0));
        Func<GameState> player=()=>{var p=FullRunePlayer(town,15);p.Entity.DailySignDays=new byte[0];if(!EntityTable.SaveEntity(p))throw new Exception("Socket boundary fixture save failed.");return p;};
        try {
            foreach(ushort stack in new ushort[]{1,2}) {
                var p=player();var boots=Pack(p,160016,1);boots.SocketOne=Enums.Gem.EmptySocket;
                Query("UPDATE items SET socketone=255 WHERE uid=@uid","uid",(long)boots.UID);
                var drill=Pack(p,1200005,stack);while(p.Inventory.Count<40)Pack(p,1088001,1);
                var request=DedicatedSocketFrame(boots.UID,drill.UID);Revora.Network.PacketHandler.HandlePacket(request,p);
                if(stack==2) {
                    Check(!Fenced(p) && drill.StackSize==2 && boots.SocketTwo==0 && p.Inventory.Count==40
                        && Scalar("SELECT count(*) FROM revora_equipment_sockets WHERE uid=@uid",p.Entity.UID)==0,
                        "full bag rejects a stacked Tough Drill before risking a lost failure refund");
                    RemoveFixtureItem(p,p.Inventory.Objects.First(i=>i.ID==1088001).UID);
                    Revora.Network.PacketHandler.HandlePacket(request,p);
                }
                uint uid=boots.UID,drillUid=drill.UID;var star=p.Inventory.Objects.Single(i=>i.ID==1200006);uint starUid=star.UID;
                Check(!Fenced(p) && p.Inventory.Count==40 && boots.SocketTwo==0 && (stack==1?!p.Inventory.ContainsUID(drillUid):drill.StackSize==1)
                    && Scalar("SELECT count(*) FROM revora_equipment_sockets WHERE uid=@uid AND NOT success AND refund_uid IS NOT NULL",p.Entity.UID)==1,
                    "exact-capacity drill failure replaces the consumed unit with one saved Star Drill: "+stack);
                p=ReloadBall(p);Revora.Network.PacketHandler.HandlePacket(request,p);
                Check(!Fenced(p) && p.Inventory.Count==40 && p.Inventory.ContainsUID(starUid)
                    && Scalar("SELECT count(*) FROM revora_equipment_sockets WHERE uid=@uid",p.Entity.UID)==1,
                    "full bag and refund survive reload and immediate replay without a second charge: "+stack);
            }
            foreach(byte slot in new byte[]{8,28}) {
                var p=player();var boots=Pack(p,160016,1);boots.Durability=boots.MaximDurability=1000;
                Query("UPDATE items SET durability=1000,maximdurability=1000 WHERE uid=@uid","uid",(long)boots.UID);
                ItemHandler.EquipItem(new ItemUsage(true){UID=boots.UID,dwParam=slot,Action=ItemUsage.Mode.EquipItem},p);
                Check(boots.Position==slot && p.Equipment.TryGetItem(boots.UID)!=null && !p.Inventory.ContainsUID(boots.UID),"ordinary equip prepares socket target in slot "+slot);
                var twelve=Enumerable.Range(0,12).Select(n=>Pack(p,1088000,1).UID).ToArray();
                Revora.Network.PacketHandler.HandlePacket(DedicatedSocketFrame(boots.UID,twelve),p);
                Check(!Fenced(p) && boots.SocketOne==Enums.Gem.EmptySocket && boots.Position==slot && twelve.All(uid=>!p.Inventory.ContainsUID(uid)),
                    "equipped first socket commits twelve exact DragonBalls and preserves slot "+slot);
                uint target=boots.UID;p=ReloadBall(p);var loaded=p.Equipment.TryGetItem(target);
                Check(!Fenced(p) && loaded!=null && loaded.Position==slot && loaded.SocketOne==Enums.Gem.EmptySocket,
                    "production login preserves equipped socket in slot "+slot);
            }
            foreach(bool target in new[]{false,true}) {
                var p=player();var weapon=Pack(p,410009,1);var ball=Pack(p,1088000,1);uint stale=target?weapon.UID:ball.UID;
                Query("UPDATE items SET warehouse=1 WHERE uid=@uid","uid",(long)stale);
                Revora.Network.PacketHandler.HandlePacket(DedicatedSocketFrame(weapon.UID,ball.UID),p);
                Check(Fenced(p) && weapon.SocketOne==0 && Scalar("SELECT count(*) FROM items WHERE uid=@uid",ball.UID)==1
                    && Scalar("SELECT count(*) FROM revora_equipment_sockets WHERE uid=@uid",p.Entity.UID)==0,
                    "saved warehouse ownership mismatch rolls back and fences socket request: target="+target);
            }
        }finally{clock.SetValue(null,oldClock);roll.SetValue(null,oldRoll);}
    }
    static void VerifyEquipmentRepairs(GameState p) {
        var service=typeof(Entity).Assembly.GetType("Revora.Game.Features.EquipmentUpgrades");Call(service,null,"Initialize");
        var projection=service.GetField("BeforeRepairProjection",Hidden);object oldProjection=projection.GetValue(null);
        Func<ushort,ConquerItem> damaged=dur=>{var i=Pack(p,410006,1);i.Durability=dur;i.MaximDurability=1000;
            Query("UPDATE items SET durability=@dur,maximdurability=1000 WHERE uid=@uid","dur",(int)dur,"uid",(long)i.UID);return i;};
        Action<uint> repair=uid=>ItemUsage.Process(new ItemUsage(true){UID=uid,Action=ItemUsage.Mode.Repair}.ToArray(),p);
        Func<long> receipts=()=>Scalar("SELECT count(*) FROM revora_equipment_repairs WHERE uid=@uid",p.Entity.UID);
        ulong cost=(ulong)Math.Max(1m,decimal.Ceiling((ConquerItemInformation.BaseInformations[410006].GoldWorth/2)*0.825m));
        try {
            var target=damaged(500);ulong money=p.Entity.Money;long before=receipts();
            System.Threading.Tasks.Parallel.For(0,8,n=>repair(target.UID));
            Check(!Fenced(p) && target.Durability==1000 && ConquerItemTable.LoadItem(target.UID).Durability==1000
                && p.Entity.Money==money-cost && Scalar("SELECT money FROM entities WHERE uid=@uid",p.Entity.UID)==(long)(money-cost) && receipts()==before+1,
                "concurrent ordinary repair packets charge one exact legacy-price debit and save full durability once");
            RemoveFixtureItem(p,target.UID);
            foreach(string fault in new[]{"money","target","receipt"}) {
                var item=damaged(500);uint uid=item.UID;money=p.Entity.Money;before=receipts();
                string table=fault=="money"?"entities":fault=="target"?"items":"revora_equipment_repairs",verb=fault=="receipt"?"INSERT":"UPDATE";
                Query("CREATE FUNCTION suppress_repair() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
                Query("CREATE TRIGGER suppress_repair BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_repair()");
                try{repair(uid);}finally{Query("DROP TRIGGER suppress_repair ON "+table);Query("DROP FUNCTION suppress_repair()");}
                Check(Fenced(p) && Scalar("SELECT money FROM entities WHERE uid=@uid",p.Entity.UID)==(long)money
                    && ConquerItemTable.LoadItem(uid).Durability==500 && receipts()==before,"suppressed repair "+fault+" write rolls back debit, durability and receipt");
                p=ReloadGold(p);ConquerItemTable.LoadItems(p);repair(uid);repair(uid);
                Check(!Fenced(p) && p.Entity.Money==money-cost && ConquerItemTable.LoadItem(uid).Durability==1000 && receipts()==before+1,
                    "full reload retries ordinary repair exactly once after "+fault);RemoveFixtureItem(p,uid);
            }
            // Isolate broken-repair stack allocation from earlier fixture material tests.
            foreach(var old in p.Inventory.Objects.Where(i=>i.ID==1088001).ToArray())RemoveFixtureItem(p,old.UID);
            var broken=damaged(0);var insufficient=Pack(p,1088001,4);money=p.Entity.Money;before=receipts();repair(broken.UID);
            Check(!Fenced(p) && broken.Durability==0 && insufficient.StackSize==4 && receipts()==before,"four Meteors cannot partially pay for a broken-item repair");
            var remainder=Pack(p,1088001,3);uint brokenUid=broken.UID,firstUid=insufficient.UID,lastUid=remainder.UID;
            Query("CREATE FUNCTION suppress_broken_repair() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_broken_repair BEFORE DELETE ON items FOR EACH ROW EXECUTE FUNCTION suppress_broken_repair()");
            try{repair(brokenUid);}finally{Query("DROP TRIGGER suppress_broken_repair ON items;DROP FUNCTION suppress_broken_repair()");}
            Check(Fenced(p) && ConquerItemTable.LoadItem(brokenUid).Durability==0 && ConquerItemTable.LoadItem(firstUid).StackSize==4
                && ConquerItemTable.LoadItem(lastUid).StackSize==3 && receipts()==before,"suppressed broken-repair material deletion restores full stacks and zero durability");
            p=ReloadGold(p);ConquerItemTable.LoadItems(p);repair(brokenUid);repair(brokenUid);
            Check(!Fenced(p) && ConquerItemTable.LoadItem(brokenUid).Durability==1000 && !p.Inventory.ContainsUID(firstUid)
                && ConquerItemTable.LoadItem(lastUid).StackSize==2 && p.Entity.Money==money && receipts()==before+1,
                "broken repair consumes exactly five Meteors across stacks and no silver after full reload");
            RemoveFixtureItem(p,brokenUid);RemoveFixtureItem(p,lastUid);
            var committed=damaged(500);uint committedUid=committed.UID;money=p.Entity.Money;before=receipts();
            projection.SetValue(null,(Action)(()=>{throw new InvalidOperationException("fixture repair projection fault");}));
            repair(committedUid);projection.SetValue(null,null);
            Check(Fenced(p) && ConquerItemTable.LoadItem(committedUid).Durability==1000 && receipts()==before+1,
                "postcommit repair projection failure fences stale state without undoing paid durability");
            p=ReloadGold(p);ConquerItemTable.LoadItems(p);repair(committedUid);
            Check(p.Entity.Money==money-cost && p.Inventory.Objects.Single(i=>i.UID==committedUid).Durability==1000 && receipts()==before+1,
                "full login recovers paid repair and refuses a second charge for the already repaired item");
            Check(EntityTable.SaveEntity(p) && Scalar("SELECT money FROM entities WHERE uid=@uid",p.Entity.UID)==(long)(money-cost),
                "ordinary character save retains the repair-adjusted silver preimage");RemoveFixtureItem(p,committedUid);
        }finally{projection.SetValue(null,oldProjection);}
    }
    static byte[] UpgradeComposeFrame(uint target,uint[] materials,byte mode,uint value) {
        int length=16+materials.Length*4;var frame=new byte[length+8];
        Revora.Network.Writer.WriteUInt16((ushort)length,0,frame);Revora.Network.Writer.WriteUInt16(2036,2,frame);
        frame[4]=mode;frame[5]=(byte)(materials.Length+2);Revora.Network.Writer.WriteUInt32(target,8,frame);Revora.Network.Writer.WriteUInt32(value,12,frame);
        for(int n=0;n<materials.Length;n++)Revora.Network.Writer.WriteUInt32(materials[n],16+4*n,frame);
        Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,length,frame);return frame;
    }
    static void VerifyEquipmentUpgradeTransactions(GameState p) {
        var service=typeof(Entity).Assembly.GetType("Revora.Game.Features.EquipmentUpgrades");
        Call(service,null,"Initialize");
        var clock=service.GetField("Clock",Hidden);var roll=service.GetField("Roll",Hidden);var projection=service.GetField("BeforeProjection",Hidden);
        object oldClock=clock.GetValue(null),oldRoll=roll.GetValue(null),oldProjection=projection.GetValue(null);
        var now=new DateTime(2035,1,1,0,0,0,DateTimeKind.Utc);
        clock.SetValue(null,(Func<DateTime>)(()=>now));roll.SetValue(null,(Func<int,int>)(max=>0));
        Func<uint,uint[],int,int,bool> apply=(target,materials,operation,route)=> {
            if(route==1) {
                long before=Scalar("SELECT count(*) FROM revora_equipment_upgrades WHERE uid=@uid",p.Entity.UID);
                ItemUsage.Process(new ItemUsage(true){UID=target,dwParam=materials[0],
                    Action=operation==1?ItemUsage.Mode.MeteorUpgrade:ItemUsage.Mode.DragonBallUpgrade}.ToArray(),p);
                return !Fenced(p) && Scalar("SELECT count(*) FROM revora_equipment_upgrades WHERE uid=@uid",p.Entity.UID)>before;
            }
            uint value=0;foreach(uint uid in materials){ConquerItem m;if(p.Inventory.TryGetItem(uid,out m))value+=m.ID==720027 || m.ID==720028?10u:m.ID==723268?5u:1u;}
            long prior=Scalar("SELECT count(*) FROM revora_equipment_upgrades WHERE uid=@uid",p.Entity.UID);
            var compose=new Compose(false);compose.Deserialize(UpgradeComposeFrame(target,materials,operation==3?(byte)7:(byte)6,value));
            ItemHandler.ComposePlus(compose,p);
            return !Fenced(p) && Scalar("SELECT count(*) FROM revora_equipment_upgrades WHERE uid=@uid",p.Entity.UID)>prior;
        };
        Func<ConquerItem> gear=()=>{var i=Pack(p,410006,1);i.Durability=i.MaximDurability=ConquerItemInformation.BaseInformations[i.ID].Durability;
            Query("UPDATE items SET durability=@d,maximdurability=@d WHERE uid=@uid","d",(int)i.Durability,"uid",(long)i.UID);return i;};
        Func<long> receipts=()=>Scalar("SELECT count(*) FROM revora_equipment_upgrades WHERE uid=@uid",p.Entity.UID);
        try {
            Check(p.Account.State==AccountTable.AccountState.Player,"upgrade transaction fixture uses an ordinary player");
            var target=gear();var meteor=Pack(p,1088001,2);var dragon=Pack(p,1088000,1);
            uint next=new ConquerItemInformation(target.ID,0).CalculateUplevel();long baseline=receipts();
            Check(!apply(target.UID,new[]{meteor.UID,meteor.UID},1,2) && !apply(target.UID,new[]{meteor.UID,dragon.UID},1,2)
                && !apply(target.UID,new[]{target.UID},1,1) && receipts()==baseline && meteor.StackSize==2,
                "duplicate, mixed and self-consuming selections cannot pay or upgrade");
            System.Threading.Tasks.Parallel.For(0,8,n=>apply(target.UID,new[]{meteor.UID},1,1));
            Check(!Fenced(p) && target.ID==next && ConquerItemTable.LoadItem(target.UID).ID==next
                && meteor.StackSize==1 && ConquerItemTable.LoadItem(meteor.UID).StackSize==1 && receipts()==baseline+1,
                "concurrent Meteor requests commit one level and one selected stack unit");
            now=now.AddSeconds(2);roll.SetValue(null,(Func<int,int>)(max=>max-1));ushort durability=target.Durability;
            Check(apply(target.UID,new[]{meteor.UID},1,1) && target.ID==next && target.Durability==durability/2
                && ConquerItemTable.LoadItem(target.UID).Durability==durability/2 && !p.Inventory.ContainsUID(meteor.UID)
                && receipts()==baseline+2,"failed Meteor roll atomically consumes payment and halves durability");
            Check(!apply(target.UID,new[]{dragon.UID},3,1) && p.Inventory.ContainsUID(dragon.UID),"damaged gear refuses another paid attempt");
            target.Durability=target.MaximDurability;Query("UPDATE items SET durability=maximdurability WHERE uid=@uid","uid",(long)target.UID);
            now=now.AddSeconds(2);target.SocketProgress=Constants.SocketOneProgress-10;
            Query("UPDATE items SET socketprogress=@progress WHERE uid=@uid","progress",(long)target.SocketProgress,"uid",(long)target.UID);
            Check(apply(target.UID,new[]{dragon.UID},3,1) && target.ID==next && target.SocketOne==Enums.Gem.EmptySocket
                && ConquerItemTable.LoadItem(target.UID).SocketOne==Enums.Gem.EmptySocket && !p.Inventory.ContainsUID(dragon.UID),
                "failed DragonBall quality roll still commits its first socket threshold with the consumed material");
            now=now.AddSeconds(2);roll.SetValue(null,(Func<int,int>)(max=>0));
            target.Durability=target.MaximDurability;target.SocketProgress=Constants.SocketTwoProgress-10;
            Query("UPDATE items SET durability=maximdurability,socketprogress=@progress WHERE uid=@uid","progress",(long)target.SocketProgress,"uid",(long)target.UID);
            var second=Pack(p,1088000,1);uint improved=target.ID+1;
            Check(apply(target.UID,new[]{second.UID},3,1) && target.ID==improved && target.SocketTwo==Enums.Gem.EmptySocket
                && ConquerItemTable.LoadItem(target.UID).SocketTwo==Enums.Gem.EmptySocket && ConquerItemTable.LoadItem(target.UID).ID==improved,
                "successful quality upgrade commits the second socket threshold with its new item type");
            now=now.AddSeconds(2);var meteorA=Pack(p,1088001,1);var meteorB=Pack(p,1088001,1);
            uint composeNext=new ConquerItemInformation(target.ID,0).CalculateUplevel();
            roll.SetValue(null,(Func<int,int>)(max=>{throw new Exception("guaranteed operation must not roll");}));
            Check(apply(target.UID,new[]{meteorA.UID,meteorB.UID},1,2) && target.ID==composeNext
                && !p.Inventory.ContainsUID(meteorA.UID) && !p.Inventory.ContainsUID(meteorB.UID),
                "Compose required Meteor quantity guarantees exactly one level and consumes each selection once");
            now=now.AddSeconds(2);var scrollTarget=gear();var scroll=Pack(p,720027,2);
            uint scrollNext=new ConquerItemInformation(scrollTarget.ID,0).CalculateUplevel();long scrollBefore=receipts();
            var valid=UpgradeComposeFrame(scrollTarget.UID,new[]{scroll.UID},6,10);
            foreach(string malformed in new[]{"short","count","length","type","seal","value","duplicate","target","zero","too-many"}) {
                var bad=(byte[])valid.Clone();
                if(malformed=="short")bad=bad.Take(18).ToArray();
                if(malformed=="count")bad[5]=4;
                if(malformed=="length")bad[0]++;
                if(malformed=="type")bad[2]++;
                if(malformed=="seal")bad[bad.Length-1]^=1;
                if(malformed=="value")Revora.Network.Writer.WriteUInt32(1,12,bad);
                if(malformed=="duplicate")bad=UpgradeComposeFrame(scrollTarget.UID,new[]{scroll.UID,scroll.UID},6,20);
                if(malformed=="target")Revora.Network.Writer.WriteUInt32(scrollTarget.UID,16,bad);
                if(malformed=="zero")Revora.Network.Writer.WriteUInt32(0,16,bad);
                if(malformed=="too-many")bad=UpgradeComposeFrame(scrollTarget.UID,Enumerable.Range(1,41).Select(n=>(uint)n).ToArray(),6,41);
                var rejected=new Compose(false);rejected.Deserialize(bad);ItemHandler.ComposePlus(rejected,p);
                Check(!Fenced(p) && receipts()==scrollBefore && scroll.StackSize==2 && scrollTarget.ID==410006,
                    "Compose upgrade rejects malformed request before payment or legacy fallback: "+malformed);
            }
            Check(apply(scrollTarget.UID,new[]{scroll.UID},1,2) && scrollTarget.ID==scrollNext && scroll.StackSize==1
                && ConquerItemTable.LoadItem(scroll.UID).StackSize==1 && receipts()==scrollBefore+1,
                "one selected MeteorScroll unit supplies ten material points and performs exactly one upgrade");
            now=now.AddSeconds(2);var dbScroll=Pack(p,720028,1);uint quality=scrollTarget.ID+1;
            Check(apply(scrollTarget.UID,new[]{dbScroll.UID},3,2) && scrollTarget.ID==quality && !p.Inventory.ContainsUID(dbScroll.UID),
                "DBScroll quality upgrade uses ten owned DragonBall points without random roll or repeated mutation");
            now=now.AddSeconds(2);var box=Pack(p,723268,1);uint boxNext=new ConquerItemInformation(scrollTarget.ID,0).CalculateUplevel();
            Check(apply(scrollTarget.UID,new[]{box.UID},1,2) && scrollTarget.ID==boxNext && !p.Inventory.ContainsUID(box.UID),
                "installed five-Meteor box supplies five points through the same owned Compose transaction");
            RemoveFixtureItem(p,scroll.UID);RemoveFixtureItem(p,scrollTarget.UID);
            now=now.AddSeconds(2);var failedGear=gear();var failedBall=Pack(p,1088000,1);ushort fullDur=failedGear.Durability;
            roll.SetValue(null,(Func<int,int>)(max=>max-1));
            long failedBefore=Scalar("SELECT count(*) FROM revora_equipment_upgrades WHERE uid=@uid AND interface=2 AND operation=3 AND NOT success",p.Entity.UID);
            Check(apply(failedGear.UID,new[]{failedBall.UID},3,2) && failedGear.ID==410006 && failedGear.Durability==fullDur
                && ConquerItemTable.LoadItem(failedGear.UID).Durability==fullDur && !p.Inventory.ContainsUID(failedBall.UID)
                && Scalar("SELECT count(*) FROM revora_equipment_upgrades WHERE uid=@uid AND interface=2 AND operation=3 AND NOT success",p.Entity.UID)==failedBefore+1,
                "failed Compose quality attempt consumes its exact payment without the direct-interface durability penalty");
            RemoveFixtureItem(p,failedGear.UID);
            foreach(byte slot in new byte[]{8,28}) {
                now=now.AddSeconds(2);var boots=Pack(p,160016,1);boots.Durability=boots.MaximDurability=ConquerItemInformation.BaseInformations[boots.ID].Durability;
                Query("UPDATE items SET durability=@d,maximdurability=@d WHERE uid=@uid","d",(int)boots.Durability,"uid",(long)boots.UID);
                ItemHandler.EquipItem(new ItemUsage(true){UID=boots.UID,dwParam=slot,Action=ItemUsage.Mode.EquipItem},p);
                Check(p.Equipment.TryGetItem(boots.UID)!=null && boots.Position==slot && ConquerItemTable.LoadItem(boots.UID).Position==slot,
                    "ordinary equip installs Compose target in the requested equipment slot "+slot);
                var firstMeteor=Pack(p,1088001,1);var secondMeteor=Pack(p,1088001,1);uint raisedBoots=new ConquerItemInformation(boots.ID,0).CalculateUplevel();
                Check(apply(boots.UID,new[]{firstMeteor.UID,secondMeteor.UID},1,2) && !Fenced(p) && boots.ID==raisedBoots && boots.Position==slot
                    && ConquerItemTable.LoadItem(boots.UID).ID==raisedBoots && ConquerItemTable.LoadItem(boots.UID).Position==slot,
                    "Compose upgrades equipped target once and retains its saved slot "+slot);
                p.LoginAt=DateTime.MinValue;ItemHandler.UnequipItem(new ItemUsage(true){dwParam=slot},p);
                Check(p.Inventory.ContainsUID(boots.UID),"upgraded equipped item returns through ordinary unequip "+slot);RemoveFixtureItem(p,boots.UID);
            }

            now=now.AddSeconds(2);var unused=Pack(p,1088000,1);long refused=receipts();
            foreach(string context in new[]{"trade","locked","bound","foreign"}) {
                if(context=="trade")p.Trade.InTrade=true;
                if(context=="locked")target.Lock=1;
                if(context=="bound")unused.Bound=true;
                if(context=="foreign")Query("UPDATE items SET entityid=0 WHERE uid=@uid","uid",(long)unused.UID);
                try{Check(!apply(target.UID,new[]{unused.UID},3,1) && receipts()==refused && p.Inventory.ContainsUID(unused.UID),
                    "upgrade rejects unsafe ownership/context without consumption: "+context);}
                finally{p.Trade.InTrade=false;target.Lock=0;unused.Bound=false;
                    if(context=="foreign")Query("UPDATE items SET entityid=@owner WHERE uid=@uid","owner",(long)p.Entity.UID,"uid",(long)unused.UID);}
            }
            Check(Fenced(p),"saved foreign material fails closed before outcome calculation");
            p=ReloadGold(p);ConquerItemTable.LoadItems(p);
            p.Entity.MyAchievement.Send();
            Check(new[]{10420,10421,10423,10424,10425,10426}.All(id=>HasAchievement(p,id)) && !HasAchievement(p,10422),
                "ordinary direct request outcomes survive full reload as six matching achievements, never DragonBall level credit");
            Check(Scalar(@"SELECT count(*) FROM revora_achievement_evidence e JOIN revora_equipment_upgrades u
ON replace(u.id::text,'-','')=e.source_key AND u.uid=e.uid
WHERE e.uid=@uid AND e.source='equipment-upgrade' AND u.interface=1",p.Entity.UID)==6,
                "every direct upgrade achievement references its actual saved operation receipt");
            foreach(string fault in new[]{"target","material","receipt","payment","achievement"}) {
                now=now.AddSeconds(2);roll.SetValue(null,(Func<int,int>)(max=>0));
                var candidate=gear();var material=Pack(p,1088000,1);uint targetUid=candidate.UID,materialUid=material.UID;long before=receipts();
                if(fault=="achievement")Query("DELETE FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10423","uid",(long)p.Entity.UID);
                string table=fault=="achievement"?"revora_achievement_evidence":fault=="receipt"?"revora_equipment_upgrades":fault=="payment"?"revora_equipment_upgrade_materials":"items";
                string verb=fault=="target"?"UPDATE":fault=="material"?"DELETE":"INSERT";
                Query("CREATE FUNCTION suppress_upgrade() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$");
                Query("CREATE TRIGGER suppress_upgrade BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_upgrade()");
                try{Check(!apply(targetUid,new[]{materialUid},3,1),"suppressed upgrade write refuses: "+fault);}
                finally{Query("DROP TRIGGER suppress_upgrade ON "+table);Query("DROP FUNCTION suppress_upgrade()");}
                Check(Fenced(p) && ConquerItemTable.LoadItem(targetUid).ID==410006 && ConquerItemTable.LoadItem(materialUid).ID==1088000 && receipts()==before,
                    "upgrade "+fault+" fault rolls back target, payment and receipt");
                p=ReloadGold(p);ConquerItemTable.LoadItems(p);
                Check(apply(targetUid,new[]{materialUid},3,1) && !apply(targetUid,new[]{materialUid},3,1)
                    && ConquerItemTable.LoadItem(targetUid).ID==410007 && Scalar("SELECT count(*) FROM items WHERE uid=@uid",materialUid)==0 && receipts()==before+1,
                    "full character reload permits exactly one upgrade retry after "+fault);
                if(fault=="achievement") {
                    p=ReloadGold(p);ConquerItemTable.LoadItems(p);p.Entity.MyAchievement.Send();
                    Check(HasAchievement(p,10423) && ConquerItemTable.LoadItem(targetUid).ID==410007,
                        "achievement-write rollback and paid retry survive another full character login");
                }
            }
            now=now.AddSeconds(2);roll.SetValue(null,(Func<int,int>)(max=>0));
            var directCommitted=gear();var directPaid=Pack(p,1088001,1);uint directUid=directCommitted.UID,directMaterial=directPaid.UID;
            uint directNext=new ConquerItemInformation(directCommitted.ID,0).CalculateUplevel();long directBefore=receipts();
            projection.SetValue(null,(Action)(()=>{throw new InvalidOperationException("fixture direct projection fault");}));
            Check(!apply(directUid,new[]{directMaterial},1,1) && Fenced(p) && ConquerItemTable.LoadItem(directUid).ID==directNext && receipts()==directBefore+1,
                "direct packet projection fault preserves committed item, receipt and payment");
            projection.SetValue(null,null);p=ReloadGold(p);ConquerItemTable.LoadItems(p);p.Entity.MyAchievement.Send();
            Check(p.Inventory.Objects.Single(i=>i.UID==directUid).ID==directNext && !p.Inventory.ContainsUID(directMaterial)
                && HasAchievement(p,10420) && !apply(directUid,new[]{directMaterial},1,1) && receipts()==directBefore+1,
                "direct packet post-commit reload restores upgrade and achievement without repeat charge");
            now=now.AddSeconds(2);var committed=gear();var paid=Pack(p,1088000,1);uint committedUid=committed.UID,paidUid=paid.UID;
            uint raised=new ConquerItemInformation(committed.ID,0).CalculateUplevel();long beforeProjection=receipts();
            Query("CREATE FUNCTION suppress_upgrade_level() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_upgrade_level BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW WHEN(NEW.achievement_id=10422) EXECUTE FUNCTION suppress_upgrade_level()");
            try{Check(!apply(committedUid,new[]{paidUid},2,2) && Fenced(p) && ConquerItemTable.LoadItem(committedUid).ID==410006
                && ConquerItemTable.LoadItem(paidUid).ID==1088000 && receipts()==beforeProjection,
                "suppressed DragonBall level achievement rolls back Compose result, payment and receipt");}
            finally{Query("DROP TRIGGER suppress_upgrade_level ON revora_achievement_evidence;DROP FUNCTION suppress_upgrade_level()");}
            p=ReloadGold(p);ConquerItemTable.LoadItems(p);
            projection.SetValue(null,(Action)(()=>{throw new InvalidOperationException("fixture projection fault");}));
            Check(!apply(committedUid,new[]{paidUid},2,2) && Fenced(p) && ConquerItemTable.LoadItem(committedUid).ID==raised && receipts()==beforeProjection+1,
                "post-commit projection fault preserves paid guaranteed DragonBall level and fences stale cache");
            projection.SetValue(null,null);p=ReloadGold(p);ConquerItemTable.LoadItems(p);
            Check(p.Inventory.Objects.Single(i=>i.UID==committedUid).ID==raised && !p.Inventory.ContainsUID(paidUid)
                && !apply(committedUid,new[]{paidUid},2,2) && receipts()==beforeProjection+1,
                "full reload recovers the committed upgrade without duplicate payment");
            p.Entity.MyAchievement.Send();Check(HasAchievement(p,10422),"Compose DragonBall level credit persists with its committed result after projection-failure reload");
        }finally{clock.SetValue(null,oldClock);roll.SetValue(null,oldRoll);projection.SetValue(null,oldProjection);}
    }
    static void VerifyEnchantIdentityAndRetention(GameState p) {
        Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.ItemEnchantment"),null,"Initialize");
        long priorReceipts=Scalar("SELECT count(*) FROM revora_item_enchantments WHERE uid=@uid",p.Entity.UID);
        var item=Pack(p,410009,1);var meteor=Pack(p,1088001,1);var gem=Pack(p,700013,1);
        ItemHandler.EnchantItem(new ItemUsage(true){UID=item.UID,dwParam=meteor.UID,Action=ItemUsage.Mode.Enchant},p);
        Check(item.Enchant==0 && ConquerItemTable.LoadItem(item.UID).Enchant==0
            && p.Inventory.ContainsUID(meteor.UID) && p.Inventory.ContainsUID(gem.UID),
            "a non-gem with a valid gem suffix cannot enchant gear or consume an unrelated Dragon Gem");
        item.Enchant=255;Query("UPDATE items SET enchant=255 WHERE uid=@uid","uid",(long)item.UID);
        ItemHandler.EnchantItem(new ItemUsage(true){UID=item.UID,dwParam=gem.UID,Action=ItemUsage.Mode.Enchant},p);
        Check(item.Enchant==255 && ConquerItemTable.LoadItem(item.UID).Enchant==255 && p.Inventory.ContainsUID(gem.UID),
            "equipment already at the client enchant cap cannot consume another gem");
        item.Enchant=254;Query("UPDATE items SET enchant=254 WHERE uid=@uid","uid",(long)item.UID);
        ItemHandler.EnchantItem(new ItemUsage(true){UID=item.UID,dwParam=gem.UID,Action=ItemUsage.Mode.Enchant},p);
        Check(item.Enchant==254 && ConquerItemTable.LoadItem(item.UID).Enchant==254 && !p.Inventory.ContainsUID(gem.UID),
            "existing legacy Dragon roll cannot lower a better saved enchantment");
        var unrelated=Pack(p,700013,1);var selected=Pack(p,700011,2);
        item.Enchant=0;Query("UPDATE items SET enchant=0 WHERE uid=@uid","uid",(long)item.UID);
        var request=new ItemUsage(true){UID=item.UID,dwParam=selected.UID,Action=ItemUsage.Mode.Enchant};
        System.Threading.Tasks.Parallel.For(0,8,n=>ItemHandler.EnchantItem(request,p));
        Check(!Fenced(p) && selected.StackSize==1 && ConquerItemTable.LoadItem(selected.UID).StackSize==1
            && p.Inventory.ContainsUID(unrelated.UID) && ConquerItemTable.LoadItem(unrelated.UID).ID==700013
            && item.Enchant>0 && Scalar("SELECT count(*) FROM revora_item_enchantments WHERE uid=@uid",p.Entity.UID)==priorReceipts+2,
            "concurrent enchanting consumes one unit of the selected Normal gem stack and never the unrelated Super gem");
        foreach(string refusal in new[]{"trade","locked","binding","position","rental"}) {
            var candidate=Pack(p,700011,1);byte hp=item.Enchant;
            long receipts=Scalar("SELECT count(*) FROM revora_item_enchantments WHERE uid=@uid",p.Entity.UID);
            if(refusal=="trade")p.Trade.InTrade=true;
            if(refusal=="locked")candidate.Lock=1;
            if(refusal=="binding")candidate.Bound=true;
            if(refusal=="position")candidate.Position=1;
            if(refusal=="rental")candidate.Days=1;
            try{ItemHandler.EnchantItem(new ItemUsage(true){UID=item.UID,dwParam=candidate.UID,Action=ItemUsage.Mode.Enchant},p);}
            finally{p.Trade.InTrade=false;candidate.Lock=0;candidate.Bound=false;candidate.Position=0;candidate.Days=0;}
            Check(!Fenced(p) && p.Inventory.ContainsUID(candidate.UID) && ConquerItemTable.LoadItem(candidate.UID).ID==700011
                && item.Enchant==hp && Scalar("SELECT count(*) FROM revora_item_enchantments WHERE uid=@uid",p.Entity.UID)==receipts,
                "enchanting refuses unsafe material or player context without consumption: "+refusal);
        }
        foreach(string fault in new[]{"material","target","receipt"}) {
            var material=Pack(p,700011,1);uint targetUid=item.UID,materialUid=material.UID;byte hp=item.Enchant;
            long receipts=Scalar("SELECT count(*) FROM revora_item_enchantments WHERE uid=@uid",p.Entity.UID);
            string table=fault=="receipt"?"revora_item_enchantments":"items";
            string verb=fault=="receipt"?"INSERT":fault=="material"?"DELETE":"UPDATE";
            Query("CREATE FUNCTION suppress_enchantment() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$");
            Query("CREATE TRIGGER suppress_enchantment BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_enchantment()");
            var attempt=new ItemUsage(true){UID=targetUid,dwParam=materialUid,Action=ItemUsage.Mode.Enchant};
            try{ItemHandler.EnchantItem(attempt,p);}finally{Query("DROP TRIGGER suppress_enchantment ON "+table);Query("DROP FUNCTION suppress_enchantment()");}
            Check(Fenced(p) && ConquerItemTable.LoadItem(targetUid).Enchant==hp && ConquerItemTable.LoadItem(materialUid).ID==700011
                && Scalar("SELECT count(*) FROM revora_item_enchantments WHERE uid=@uid",p.Entity.UID)==receipts,
                "suppressed enchanting "+fault+" write rolls back gem consumption, HP and receipt together");
            p=ReloadGold(p);ConquerItemTable.LoadItems(p);new CoatStorage().Login(p);
            item=p.Inventory.Objects.Single(i=>i.UID==targetUid);
            ItemHandler.EnchantItem(attempt,p);ItemHandler.EnchantItem(attempt,p);
            Check(!Fenced(p) && !p.Inventory.ContainsUID(materialUid) && Scalar("SELECT count(*) FROM items WHERE uid=@uid",materialUid)==0
                && item.Enchant>=hp && Scalar("SELECT count(*) FROM revora_item_enchantments WHERE uid=@uid",p.Entity.UID)==receipts+1,
                "production reload retries enchanting exactly once after "+fault+" failure");
        }
        ItemHandler.EquipItem(new ItemUsage(true){UID=item.UID,dwParam=4,Action=ItemUsage.Mode.EquipItem},p);
        Call(typeof(Achievement),p.Entity.MyAchievement,"Refresh",DateTime.UtcNow,true);
        Check(HasAchievement(p,10430) && ConquerItemTable.LoadItem(item.UID).Position==4,
            "transactionally enchanted supplied weapon earns its equipment achievement through ordinary equip");
        uint equipped=item.UID;p=ReloadGold(p);ConquerItemTable.LoadItems(p);p.Entity.MyAchievement.Send();
        Check(p.Equipment.TryGetItem(equipped)!=null && p.Equipment.TryGetItem(equipped).Enchant>0 && HasAchievement(p,10430),
            "production login retains the committed enchantment, equipped weapon and achievement");
    }
    static void VerifyEarnedTalismans(GameState p) {
        uint before=p.Entity.ConquerPoints;ulong cost=0;
        Check(p.Account.State==AccountTable.AccountState.Player && !HasAchievement(p,10418),
            "talisman route starts with the ordinary boss earner and no talisman achievement");
        foreach(uint id in new uint[]{201009,202009}) {
            uint price=ConquerItemInformation.BaseInformations[id].ConquerPointsWorth;
            Check(EShopFile.Shops[2888].Items.Contains(id) && price>0 && price<=p.Entity.ConquerPoints,
                "existing mall stocks an affordable talisman at its loaded CP price: "+id);
            var request=BuyBook(id,1,2888);uint cp=p.Entity.ConquerPoints;
            if(id==201009)foreach(string table in new[]{"entities","items","revora_euro_jersey_purchases"}) {
                long count=Scalar("SELECT count(*) FROM items WHERE entityid=@uid",p.Entity.UID);
                Query("CREATE FUNCTION suppress_talisman_buy() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$");
                Query("CREATE TRIGGER suppress_talisman_buy BEFORE "+(table=="entities"?"UPDATE":"INSERT")+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_talisman_buy()");
                try {ItemHandler.HandleBuyFromNPC(request,p);}
                finally {Query("DROP TRIGGER suppress_talisman_buy ON "+table);Query("DROP FUNCTION suppress_talisman_buy()");}
                Check(Fenced(p) && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==cp
                    && Scalar("SELECT count(*) FROM items WHERE entityid=@uid",p.Entity.UID)==count
                    && Scalar("SELECT count(*) FROM revora_euro_jersey_purchases WHERE uid=@uid AND item_type IN(201009,202009)",p.Entity.UID)==0,
                    "suppressed talisman "+table+" write rolls back payment, item and receipt together");
                p=ReloadGold(p);ConquerItemTable.LoadItems(p);new CoatStorage().Login(p);
            }
            ItemHandler.HandleBuyFromNPC(request,p);ItemHandler.HandleBuyFromNPC(request,p);
            var item=p.Inventory.Objects.Single(i=>i.ID==id);cost+=price;
            Check(!Fenced(p) && !item.Bound && item.Days==0 && p.Entity.ConquerPoints==cp-price,
                "earned CP talisman purchase and immediate replay pay once: "+id);
            if(id==201009) {
                Call(typeof(Achievement),p.Entity.MyAchievement,"Refresh",DateTime.UtcNow,true);
                Check(!HasAchievement(p,10418),"purchased talisman in the bag does not earn equipment credit");
            }
            uint slot=id==201009?10u:11u;
            ItemHandler.EquipItem(new ItemUsage(true){UID=item.UID,dwParam=slot,Action=ItemUsage.Mode.EquipItem},p);
            Call(typeof(Achievement),p.Entity.MyAchievement,"Refresh",DateTime.UtcNow,true);
            Check(p.Equipment.TryGetItem(item.UID)!=null && ConquerItemTable.LoadItem(item.UID).Position==slot && HasAchievement(p,10418) && HasAchievement(p,10409),
                "ordinary purchased talisman equips in its saved slot and grants achievement: "+id);
        }
        p=ReloadGold(p);ConquerItemTable.LoadItems(p);new CoatStorage().Login(p);p.Entity.MyAchievement.Send();
        Check(p.Entity.ConquerPoints==before-cost && p.Entity.RevoraPoint==2500 && HasAchievement(p,10418) && HasAchievement(p,10409)
            && p.Equipment.Objects.Count(i=>i!=null && (i.ID==201009 || i.ID==202009))==2
            && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10418",p.Entity.UID)==1
            && Scalar("SELECT count(*) FROM revora_euro_jersey_purchases WHERE uid=@uid AND item_type IN(201009,202009)",p.Entity.UID)==2,
            "production reload retains both earned talismans, one achievement and exact CP cost "+cost);
    }
    static void VerifyEquipmentAchievementRules(GameState p) {
        Check(HasAchievement(p,10419),"actual purchased garment equip milestone survives wardrobe changes and production login");
        var service=typeof(Entity).Assembly.GetType("Revora.Game.Features.EquipmentAchievements");
        DateTime now=DateTime.UtcNow;
        foreach(uint type in new uint[]{187405,201009,202009,350001,380001,410009,111009,130009,160019,201008,350009}) {
            int position=(int)ItemHandler.GetPositionFromID(type);
            int expected=position==9?10419:position==10 || position==11?10418:position==15 || position==16?10503:0;
            Check(ConquerItemInformation.BaseInformations.ContainsKey(type) && new[]{1,3,4,8,9,10,11,15,16}.Contains(position),
                "equipment rule fixture uses a loaded item in a supported category: "+type);
            foreach(int mode in new[]{0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21}) {
                using(var c=DataHolder.MySqlConnection) {c.Open();using(var t=c.BeginTransaction()) {
                    using(var q=new Npgsql.NpgsqlCommand("DELETE FROM revora_achievement_evidence WHERE uid=@uid AND source='equipment-state-v1'; UPDATE items SET position=0 WHERE entityid=@uid",c,t)) {
                        q.Parameters.AddWithValue("uid",(long)p.Entity.UID);q.ExecuteNonQuery();
                    }
                    using(var q=new Npgsql.NpgsqlCommand(@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,days,daystamp,enchant,socketone,sockettwo)
VALUES(@item,@type,@uid,@position,@warehouse,1,0,@days,@stamp,@enchant,@first,@second)",c,t)) {
                        q.Parameters.AddWithValue("item",(long)ConquerItem.ItemUID.Next);q.Parameters.AddWithValue("type",(int)type);
                        q.Parameters.AddWithValue("uid",(long)p.Entity.UID);
                        q.Parameters.AddWithValue("position",mode==1?0:mode==2?(position==4?3:4):mode==8?16:mode==9?5:position);
                        q.Parameters.AddWithValue("warehouse",mode==3?1:0);
                        q.Parameters.AddWithValue("days",mode==6?"invalid":mode==4 || mode==5?"1":"0");
                        q.Parameters.AddWithValue("enchant",mode==10?1:mode==11?255:mode==12?256:mode==13?-1:0);
                        q.Parameters.AddWithValue("first",mode==14 || mode==20 || mode==21?255:mode==15?13:mode==17?254:mode==18?-1:mode==19?256:0);
                        q.Parameters.AddWithValue("second",mode==16 || mode==21?255:mode==20?254:0);
                        q.Parameters.AddWithValue("stamp",(mode==4?now.AddDays(-2):now).ToBinary());q.ExecuteNonQuery();
                        if(mode==7){q.Parameters["item"].Value=(long)ConquerItem.ItemUID.Next;q.ExecuteNonQuery();}
                    }
                    Call(service,null,"Record",c,t,p.Entity.UID,now);Call(service,null,"Record",c,t,p.Entity.UID,now);
                    using(var q=new Npgsql.NpgsqlCommand("SELECT achievement_id FROM revora_achievement_evidence WHERE uid=@uid AND source='equipment-state-v1'",c,t)) {
                        q.Parameters.AddWithValue("uid",(long)p.Entity.UID);var actual=new System.Collections.Generic.List<int>();
                        using(var reader=q.ExecuteReader())while(reader.Read())actual.Add(reader.GetInt32(0));
                        bool qualifies=mode>=10 || mode==0 || mode==5 || mode==8 && ItemHandler.IsAccessory(type) || mode==9 && type==410009;
                        var wanted=new System.Collections.Generic.List<int>();
                        if(qualifies && expected!=0)wanted.Add(expected);
                        if((mode==10 || mode==11) && (position>=1 && position<=6 || position==8))wanted.Add(10430);
                        if((mode==14 || mode==15 || mode==21) && (position>=1 && position<=6 || position==8 || position==10 || position==11))wanted.Add(10429);
                        if(qualifies && position!=9 && position!=15 && position!=16 && type%10==9)wanted.Add(10409);
                        Check(actual.OrderBy(i=>i).SequenceEqual(wanted.OrderBy(i=>i)),
                            "supplied equipment state and receipt replay validate category "+type+" mode "+mode);
                    }
                    t.Rollback();
                }}
            }
        }
    }
    static void VerifyEarnedFiftyGarments(GameState p) {
        new TitleStorage().Login(p);
        var catalog=typeof(Entity).Assembly.GetType("Revora.Game.Features.CollectionAchievementCatalog");
        var ids=(uint[])catalog.GetField("Garments",Hidden).GetValue(null);
        var owned=new System.Collections.Generic.HashSet<uint>(p.Entity.StorageItems.Values.Select(i=>i.ID));
        var choices=EShopFile.Shops[2888].Items.Distinct().Where(id=>ids.Contains(id) && !owned.Contains(id)
            && ConquerItemInformation.BaseInformations.ContainsKey(id) && ConquerItemInformation.BaseInformations[id].ConquerPointsWorth>0)
            .OrderBy(id=>ConquerItemInformation.BaseInformations[id].ConquerPointsWorth).ThenBy(id=>id).Take(40).ToArray();
        uint before=p.Entity.ConquerPoints;ulong total=choices.Aggregate(0UL,(sum,id)=>sum+ConquerItemInformation.BaseInformations[id].ConquerPointsWorth);
        Check(choices.Length==40 && total<=before && owned.Count(id=>ids.Contains(id))==10 && !HasAchievement(p,11229),
            "existing CP mall has forty additional compatible garments affordable from the original earned boss purse");
        uint last=0;
        for(int i=0;i<choices.Length;i++) {
            uint id=choices[i],price=ConquerItemInformation.BaseInformations[id].ConquerPointsWorth,cp=p.Entity.ConquerPoints;
            var request=BuyBook(id,1,2888);
            if(i==0) {
                long savedItems=Scalar("SELECT count(*) FROM items WHERE entityid=@uid",p.Entity.UID);
                Query("CREATE FUNCTION suppress_cosmetic_payment() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$");
                Query("CREATE TRIGGER suppress_cosmetic_payment BEFORE INSERT ON revora_euro_jersey_purchases FOR EACH ROW EXECUTE FUNCTION suppress_cosmetic_payment()");
                try {ItemHandler.HandleBuyFromNPC(request,p);}
                finally {Query("DROP TRIGGER suppress_cosmetic_payment ON revora_euro_jersey_purchases");Query("DROP FUNCTION suppress_cosmetic_payment()");}
                Check(Fenced(p) && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==cp
                    && Scalar("SELECT count(*) FROM items WHERE entityid=@uid",p.Entity.UID)==savedItems,
                    "non-jersey cosmetic receipt failure rolls back both earned CP debit and item insert");
                p=ReloadGold(p);ConquerItemTable.LoadItems(p);new CoatStorage().Login(p);p.Entity.WTitles=null;new TitleStorage().Login(p);
            }
            ItemHandler.HandleBuyFromNPC(request,p);ItemHandler.HandleBuyFromNPC(request,p);
            var item=p.Inventory.Objects.Single(x=>x.ID==id);
            Check(!Fenced(p) && !item.Bound && item.Days==0 && p.Entity.ConquerPoints==cp-price,
                "ordinary mall purchase and immediate retry pay once for permanent design: "+id);
            new CoatStorage{Info=new CoatStorageProto{ActionId=CoatStorage.Action.Addtowardrobe,ItemGuid=item.UID}}.Handle(p);
            Check(!Fenced(p) && p.Entity.StorageItems.ContainsKey(item.UID),"paid mall garment deposits into owned collection: "+id);
            if(i==38)Check(!HasAchievement(p,11229),"forty-nine actually purchased garment designs do not grant fifty-design milestone");
            last=item.UID;
        }
        Check(HasAchievement(p,11229) && !HasAchievement(p,11231) && p.Entity.ConquerPoints==before-total && p.Entity.RevoraPoint==2500,
            "fiftieth earned garment grants its milestone with exact CP cost "+total+" and unchanged RVP purse");
        new CoatStorage{Info=new CoatStorageProto{ActionId=CoatStorage.Action.Retrieve,ItemGuid=last}}.Handle(p);
        p=ReloadGold(p);ConquerItemTable.LoadItems(p);new CoatStorage().Login(p);p.Entity.WTitles=null;new TitleStorage().Login(p);
        Check(HasAchievement(p,11229) && p.Inventory.ContainsUID(last) && p.Entity.ConquerPoints==before-total
            && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11229",p.Entity.UID)==1,
            "earned fifty-garment receipt survives withdrawal and production login without another payment or achievement");
    }
    static void VerifyEarnedTenCollections(GameState p) {
        new TitleStorage().Login(p);
        Check(p.Account.State==AccountTable.AccountState.Player && p.Entity.RevoraPoint==20000
            && p.Entity.StorageItems.Count==13 && !HasAchievement(p,11226) && !HasAchievement(p,11227),
            "ten-design route continues the original ordinary boss earner with six garments seven mounts and 20000 earned RVPs");
        p.BindWorldMap(1002);p.Entity.X=396;p.Entity.Y=340;p.ActiveNpc=900000014;
        byte[] options={75,60,61,62,81,87,88};
        uint[] designs={193445,193525,193565,193625,200531,200572,200573};
        var acquired=new System.Collections.Generic.List<uint>();
        for(int i=0;i<options.Length;i++) {
            var item=BuyRouteRune(p,options[i]);acquired.Add(item.UID);
            Check(item.ID==designs[i] && !item.Bound && item.Days==0 && p.Entity.RevoraPoint==20000u-(uint)(i+1)*2500,
                "remaining earned bounty buys a distinct permanent ten-tier design at unchanged price: "+designs[i]);
            var request=new CoatStorage{Info=new CoatStorageProto{ActionId=CoatStorage.Action.Addtowardrobe,ItemGuid=item.UID,ItemId=(int)item.ID}};
            if(i==3) {
                Query("CREATE FUNCTION suppress_ten_collection() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.achievement_id=11227 THEN RETURN NULL; END IF; RETURN NEW; END $$");
                Query("CREATE TRIGGER suppress_ten_collection BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW EXECUTE FUNCTION suppress_ten_collection()");
                bool rejected=false;
                try {request.Handle(p);} catch(InvalidOperationException) {rejected=true;}
                finally {Query("DROP TRIGGER suppress_ten_collection ON revora_achievement_evidence");Query("DROP FUNCTION suppress_ten_collection()");}
                Check(rejected && !HasAchievement(p,11227) && p.Entity.StorageItems.ContainsKey(item.UID)
                    && SavedRune(item,"inwardrobe")==1
                    && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11227",p.Entity.UID)==0,
                    "failed ten-garment receipt cannot grant achievement but preserves the separately committed earned deposit");
                EvidenceRefresh(p);
            } else request.Handle(p);
            request.Handle(p);
            Check(!Fenced(p) && p.Entity.StorageItems.ContainsKey(item.UID) && SavedRune(item,"inwardrobe")==1,
                "ten-tier paid design deposits and duplicate request does not duplicate it: "+designs[i]);
            if(i==2)Check(!HasAchievement(p,11227),"nine actually earned garments do not grant the ten-garment milestone");
            if(i==3)Check(HasAchievement(p,11227) && !HasAchievement(p,11226),"retry records the actually earned tenth garment independently of mount count");
            if(i==5)Check(!HasAchievement(p,11226),"nine actually earned mounts do not grant the ten-mount milestone");
        }
        Check(HasAchievement(p,11226) && HasAchievement(p,11227) && !HasAchievement(p,11228) && !HasAchievement(p,11229)
            && p.Entity.StorageItems.Count==20 && p.Entity.RevoraPoint==2500
            && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND source='wardrobe-permanent-v1'",p.Entity.UID)==4,
            "one boss-funded route owns ten garments and ten mounts with exactly four collection milestones and 2500 remaining RVPs");
        uint garment=acquired[3],mount=acquired[6];
        foreach(uint uid in new[]{garment,mount})
            new CoatStorage{Info=new CoatStorageProto{ActionId=CoatStorage.Action.Retrieve,ItemGuid=uid}}.Handle(p);
        p=ReloadGold(p);ConquerItemTable.LoadItems(p);new CoatStorage().Login(p);p.Entity.WTitles=null;new TitleStorage().Login(p);
        Check(!Fenced(p) && p.Inventory.ContainsUID(garment) && p.Inventory.ContainsUID(mount)
            && p.Entity.StorageItems.Count==18 && HasAchievement(p,11226) && HasAchievement(p,11227)
            && p.Entity.RevoraPoint==2500 && Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",p.Entity.UID)==2500,
            "earned ten-tier history persists through wardrobe withdrawal and full production login with exact purse");
        foreach(uint uid in new[]{garment,mount})
            new CoatStorage{Info=new CoatStorageProto{ActionId=CoatStorage.Action.Addtowardrobe,ItemGuid=uid}}.Handle(p);
        EvidenceRefresh(p);
        Check(p.Entity.StorageItems.Count==20 && p.Entity.RevoraPoint==2500
            && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND source='wardrobe-permanent-v1'",p.Entity.UID)==4,
            "redeposit and progression replay preserve twenty earned designs without repeating achievements or charges");
    }
    static void VerifyCollectionAchievementRules(GameState p) {
        var service=typeof(Entity).Assembly.GetType("Revora.Game.Features.CollectionAchievements");
        Check(HasAchievement(p,11224) && HasAchievement(p,11225)
            && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND source='wardrobe-permanent-v1'",p.Entity.UID)==2,
            "earned first-design achievements survive withdrawal takeoff switches and production reload exactly once");
        foreach(int count in new[]{0,1,9,10,49,50,99,100,199,200}) {
            using(var c=DataHolder.MySqlConnection) {c.Open();using(var t=c.BeginTransaction()) {
                using(var q=new Npgsql.NpgsqlCommand("DELETE FROM revora_achievement_evidence WHERE uid=@uid AND source='wardrobe-permanent-v1'",c,t)) {
                    q.Parameters.AddWithValue("uid",(long)p.Entity.UID);q.ExecuteNonQuery();
                }
                Call(service,null,"RecordCounts",c,t,p.Entity.UID,count,count);
                Call(service,null,"RecordCounts",c,t,p.Entity.UID,count,count);
                using(var q=new Npgsql.NpgsqlCommand("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND source='wardrobe-permanent-v1'",c,t)) {
                    q.Parameters.AddWithValue("uid",(long)p.Entity.UID);
                    Check(Convert.ToInt32(q.ExecuteScalar())==2*new[]{1,10,50,100,200}.Count(n=>n<=count),
                        "supplied count boundary and replay persist exact collection milestone set: "+count);
                }
                t.Rollback();
            }}
        }
        foreach(string change in new[]{"days='1'","inwardrobe=0","warehouse=1","position=4"}) {
            using(var c=DataHolder.MySqlConnection) {c.Open();using(var t=c.BeginTransaction()) {
                using(var q=new Npgsql.NpgsqlCommand("DELETE FROM revora_achievement_evidence WHERE uid=@uid AND source='wardrobe-permanent-v1'; UPDATE items SET "+change+" WHERE entityid=@uid",c,t)) {
                    q.Parameters.AddWithValue("uid",(long)p.Entity.UID);q.ExecuteNonQuery();
                }
                Call(service,null,"Record",c,t,p.Entity.UID);
                using(var q=new Npgsql.NpgsqlCommand("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND source='wardrobe-permanent-v1'",c,t)) {
                    q.Parameters.AddWithValue("uid",(long)p.Entity.UID);
                    Check(Convert.ToInt32(q.ExecuteScalar())==0,"durable collection excludes fixture state: "+change);
                }
                t.Rollback();
            }}
        }
        using(var c=DataHolder.MySqlConnection) {c.Open();using(var t=c.BeginTransaction()) {
            for(int i=0;i<20;i++) using(var q=new Npgsql.NpgsqlCommand(@"INSERT INTO items
(uid,id,entityid,position,warehouse,stacksize,bound,days,inwardrobe)
VALUES(@item,189695,@uid,0,0,1,0,'0',1)",c,t)) {
                q.Parameters.AddWithValue("item",(long)ConquerItem.ItemUID.Next);
                q.Parameters.AddWithValue("uid",(long)p.Entity.UID);q.ExecuteNonQuery();
            }
            Call(service,null,"Record",c,t,p.Entity.UID);
            using(var q=new Npgsql.NpgsqlCommand("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND source='wardrobe-permanent-v1'",c,t)) {
                q.Parameters.AddWithValue("uid",(long)p.Entity.UID);
                Check(Convert.ToInt32(q.ExecuteScalar())==2,"twenty supplied duplicate garment instances cannot cross the ten-design milestone");
            }
            t.Rollback();
        }}
    }
    static GameState VerifyCollectionSwitching(GameState p) {
        foreach(byte slot in new byte[]{9,17}) {
            if(slot==17)p.Entity.SubClasses.Classes[9]=new Revora.Game.SubClass{ID=9,Phase=9,Level=9};
            uint uid=p.Entity.StorageItems.Values.Single(i=>i.ID==(slot==9?189695u:200517u)).UID;
            uint oldUid=p.Equipment.TryGetItem(slot).UID;
            var request=new CoatStorage{Info=new CoatStorageProto{ActionId=CoatStorage.Action.Addtowardrobe,ItemGuid=uid}};
            foreach(bool failSelected in new[]{false,true}) {
                Query("CREATE FUNCTION suppress_coat_switch() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.position"+
                    (failSelected?"<>0":"=0")+" THEN RETURN NULL; END IF; RETURN NEW; END $$");
                Query("CREATE TRIGGER suppress_coat_switch BEFORE UPDATE ON items FOR EACH ROW EXECUTE FUNCTION suppress_coat_switch()");
                try {
                    request.Handle(p);
                    Check(Fenced(p) && p.Equipment.TryGetItem(slot).UID==oldUid && p.Entity.StorageItems[uid].Position==0
                        && SavedRune(p.Entity.StorageItems[oldUid],"position")==slot && SavedRune(p.Entity.StorageItems[uid],"position")==0,
                        "suppressed "+(failSelected?"selected":"displaced")+" cosmetic write rolls back entire switch: "+slot);
                } finally {Query("DROP TRIGGER suppress_coat_switch ON items");Query("DROP FUNCTION suppress_coat_switch()");}
                p=ReloadGold(p);ConquerItemTable.LoadItems(p);new CoatStorage().Login(p);p.Entity.WTitles=null;new TitleStorage().Login(p);
                if(slot==17)p.Entity.SubClasses.Classes[9]=new Revora.Game.SubClass{ID=9,Phase=9,Level=9};
                Check(!Fenced(p) && p.Equipment.TryGetItem(slot).UID==oldUid && p.Entity.StorageItems[uid].Position==0,
                    "failed switch restores old equipped and new stored cosmetic through production login: "+slot);
            }
            var fillers=new System.Collections.Generic.List<ConquerItem>();
            try {
                for(int n=0;n<40;n++) {
                    var filler=new ConquerItem(true){UID=ConquerItem.ItemUID.Next,ID=1088001,StackSize=1};
                    if(!p.Inventory.Add(filler,Revora.Game.Enums.ItemUse.None))throw new Exception("Switch capacity fixture failed");
                    fillers.Add(filler);
                }
                request.Handle(p);request.Handle(p);
                Check(!Fenced(p) && p.Inventory.Count==40 && p.Equipment.TryGetItem(slot).UID==uid
                    && p.Entity.StorageItems[oldUid].Position==0 && SavedRune(p.Entity.StorageItems[oldUid],"position")==0
                    && SavedRune(p.Entity.StorageItems[uid],"position")==slot,
                    "stored cosmetic switch and replay succeed with full bag without losing either item: "+slot);
            } finally {foreach(var filler in fillers)p.Inventory.Remove(filler,Revora.Game.Enums.ItemUse.None);}
            p=ReloadGold(p);ConquerItemTable.LoadItems(p);new CoatStorage().Login(p);p.Entity.WTitles=null;new TitleStorage().Login(p);
            Check(p.Equipment.TryGetItem(slot).UID==uid && p.Entity.StorageItems[oldUid].Position==0
                && p.Entity.StorageItems.Count==11 && p.Entity.RevoraPoint==22500,
                "successful switch preserves both item identities full collection and purse through reload: "+slot);
        }
        var selected=p.Equipment.TryGetItem((byte)9);
        new CoatStorage{Info=new CoatStorageProto{ActionId=CoatStorage.Action.Takeoff,ItemGuid=selected.UID}}.Handle(p);
        var ordinary=p.Entity.StorageItems.Values.Single(i=>i.ID==188915);
        new CoatStorage{Info=new CoatStorageProto{ActionId=CoatStorage.Action.Retrieve,ItemGuid=ordinary.UID}}.Handle(p);
        ItemHandler.EquipItem(new ItemUsage(true){UID=ordinary.UID,dwParam=9,Action=ItemUsage.Mode.EquipItem},p);
        Check(!ordinary.InWardrobe && p.Equipment.TryGetItem((byte)9).UID==ordinary.UID && SavedRune(ordinary,"position")==9,
            "earned garment equips through ordinary bag equipment path outside wardrobe");
        new CoatStorage{Info=new CoatStorageProto{ActionId=CoatStorage.Action.Addtowardrobe,ItemGuid=selected.UID}}.Handle(p);
        Check(!Fenced(p) && p.Equipment.TryGetItem((byte)9).UID==selected.UID && p.Inventory.ContainsUID(ordinary.UID)
            && SavedRune(ordinary,"position")==0 && SavedRune(ordinary,"inwardrobe")==0,
            "wardrobe switch returns displaced non-wardrobe cosmetic to its saved bag exactly once");
        p=ReloadGold(p);ConquerItemTable.LoadItems(p);new CoatStorage().Login(p);p.Entity.WTitles=null;new TitleStorage().Login(p);
        Check(p.Inventory.ContainsUID(ordinary.UID) && p.Equipment.TryGetItem((byte)9).UID==selected.UID,
            "mixed ordinary and wardrobe equipment switch survives production login");
        new CoatStorage{Info=new CoatStorageProto{ActionId=CoatStorage.Action.Addtowardrobe,ItemGuid=ordinary.UID}}.Handle(p);
        Check(!Fenced(p) && p.Entity.StorageItems.Count==11 && p.Entity.RevoraPoint==22500,
            "mixed-path displaced garment redeposits without loss or a second purchase");
        return p;
    }
    static GameState VerifyCollectionTakeoff(GameState p) {
        new CoatStorage().Login(p);
        foreach(byte slot in new byte[]{9,17}) {
            if(slot==17) {
                // Supplied riding qualification; this fixture proves cosmetic persistence, not subclass earning.
                p.Entity.SubClasses.Classes[9]=new Revora.Game.SubClass{ID=9,Phase=9,Level=9};
                var mount=p.Entity.StorageItems.Values.Single(i=>i.ID==200596);
                new CoatStorage{Info=new CoatStorageProto{ActionId=CoatStorage.Action.Addtowardrobe,ItemGuid=mount.UID,ItemId=(int)mount.ID}}.Handle(p);
            }
            var item=p.Equipment.TryGetItem(slot);uint uid=item.UID;uint type=item.ID;
            var request=new CoatStorage{Info=new CoatStorageProto{ActionId=CoatStorage.Action.Takeoff,ItemGuid=uid,ItemId=(int)type}};
            p.Trade.InTrade=true;request.Handle(p);p.Trade.InTrade=false;
            Check(ReferenceEquals(p.Equipment.TryGetItem(slot),item) && SavedRune(item,"position")==slot,
                "active trade cannot take off a wardrobe cosmetic: "+slot);
            Query("CREATE FUNCTION suppress_coat_takeoff() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF OLD.position IN(9,17) AND NEW.position=0 THEN RETURN NULL; END IF; RETURN NEW; END $$");
            Query("CREATE TRIGGER suppress_coat_takeoff BEFORE UPDATE ON items FOR EACH ROW EXECUTE FUNCTION suppress_coat_takeoff()");
            try {
                request.Handle(p);
                Check(Fenced(p) && ReferenceEquals(p.Equipment.TryGetItem(slot),item) && item.Position==slot
                    && SavedRune(item,"position")==slot && p.Entity.StorageItems.ContainsKey(uid),
                    "suppressed takeoff preserves equipped cosmetic and wardrobe ownership: "+slot);
            } finally {Query("DROP TRIGGER suppress_coat_takeoff ON items");Query("DROP FUNCTION suppress_coat_takeoff()");}
            p=ReloadGold(p);ConquerItemTable.LoadItems(p);new CoatStorage().Login(p);p.Entity.WTitles=null;new TitleStorage().Login(p);
            item=p.Entity.StorageItems[uid];
            Check(!Fenced(p) && ReferenceEquals(p.Equipment.TryGetItem(slot),item),"failed takeoff reloads equipped: "+slot);
            var fillers=new System.Collections.Generic.List<ConquerItem>();
            try {
                for(int n=0;n<40;n++) {
                    var filler=new ConquerItem(true){UID=ConquerItem.ItemUID.Next,ID=1088001,StackSize=1};
                    if(!p.Inventory.Add(filler,Revora.Game.Enums.ItemUse.None))throw new Exception("Takeoff capacity fixture failed");
                    fillers.Add(filler);
                }
                request.Handle(p);request.Handle(p);
                Check(!Fenced(p) && p.Inventory.Count==40 && p.Equipment.TryGetItem(slot)==null && item.Position==0
                    && item.InWardrobe && SavedRune(item,"position")==0 && SavedRune(item,"inwardrobe")==1
                    && p.Entity.StorageItems.ContainsKey(uid) && !p.Inventory.ContainsUID(uid),
                    "takeoff works with full bag and duplicate request preserves the stored cosmetic: "+slot);
            } finally {foreach(var filler in fillers)p.Inventory.Remove(filler,Revora.Game.Enums.ItemUse.None);}
            p=ReloadGold(p);ConquerItemTable.LoadItems(p);new CoatStorage().Login(p);p.Entity.WTitles=null;new TitleStorage().Login(p);
            Check(p.Equipment.TryGetItem(slot)==null && p.Entity.StorageItems[uid].Position==0
                && OwnedTitles(p).ContainsKey(20050006) && OwnedTitles(p).ContainsKey(20060007) && p.Entity.RevoraPoint==22500,
                "successful takeoff stays unequipped after production login without losing collection titles or purse: "+slot);
            if(slot==17)p.Entity.SubClasses.Classes[9]=new Revora.Game.SubClass{ID=9,Phase=9,Level=9};
            new CoatStorage{Info=new CoatStorageProto{ActionId=CoatStorage.Action.Addtowardrobe,ItemGuid=uid,ItemId=(int)type}}.Handle(p);
            Check(!Fenced(p) && p.Equipment.TryGetItem(slot)!=null && p.Equipment.TryGetItem(slot).UID==uid,
                "stored cosmetic can be equipped again after takeoff: "+slot);
        }
        return p;
    }
    static GameState VerifyCollectionWithdrawals(GameState p) {
        new CoatStorage().Login(p);
        // One stored garment and the currently equipped garment, both earned above.
        uint[] selected={189695,188915};
        foreach(uint type in selected) {
            var item=p.Entity.StorageItems.Values.Single(i=>i.ID==type);uint uid=item.UID;
            byte position=item.Position;
            Check((type==189695 && position==0) || (type==188915 && position==9),
                "withdrawal fixture covers actual stored and equipped purchased garments: "+type);
            var request=new CoatStorage{Info=new CoatStorageProto{ActionId=CoatStorage.Action.Retrieve,ItemGuid=uid,ItemId=(int)type}};
            if(type==189695) {
                var fillers=new System.Collections.Generic.List<ConquerItem>();
                try {
                    for(int n=0;n<40;n++) {
                        var filler=new ConquerItem(true){UID=ConquerItem.ItemUID.Next,ID=1088001,StackSize=1};
                        if(!p.Inventory.Add(filler,Revora.Game.Enums.ItemUse.None))throw new Exception("Capacity fixture failed");
                        fillers.Add(filler);
                    }
                    request.Handle(p);
                    Check(!Fenced(p) && p.Inventory.Count==40 && item.InWardrobe && SavedRune(item,"inwardrobe")==1,
                        "full bag rejects withdrawal without mutating persisted or live wardrobe");
                } finally {foreach(var filler in fillers)p.Inventory.Remove(filler,Revora.Game.Enums.ItemUse.None);}
            }
            p.Trade.InTrade=true;request.Handle(p);p.Trade.InTrade=false;
            Check(item.InWardrobe && !p.Inventory.ContainsUID(uid) && SavedRune(item,"inwardrobe")==1,
                "active trade cannot withdraw a collection item: "+type);
            Query("CREATE FUNCTION suppress_coat_withdrawal() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF OLD.inwardrobe=1 AND NEW.inwardrobe=0 THEN RETURN NULL; END IF; RETURN NEW; END $$");
            Query("CREATE TRIGGER suppress_coat_withdrawal BEFORE UPDATE ON items FOR EACH ROW EXECUTE FUNCTION suppress_coat_withdrawal()");
            try {
                request.Handle(p);
                Check(Fenced(p) && item.InWardrobe && item.Position==position && p.Entity.StorageItems.ContainsKey(uid)
                    && !p.Inventory.ContainsUID(uid) && SavedRune(item,"inwardrobe")==1 && SavedRune(item,"position")==position,
                    "suppressed withdrawal preserves collection equipment and saved location: "+type);
            } finally {Query("DROP TRIGGER suppress_coat_withdrawal ON items");Query("DROP FUNCTION suppress_coat_withdrawal()");}
            p=ReloadGold(p);ConquerItemTable.LoadItems(p);new CoatStorage().Login(p);p.Entity.WTitles=null;new TitleStorage().Login(p);
            item=p.Entity.StorageItems[uid];
            Check(!Fenced(p) && item.Position==position && !p.Inventory.ContainsUID(uid),
                "failed withdrawal restores original collection through production reload: "+type);
            request.Handle(p);request.Handle(p);
            Check(!Fenced(p) && p.Inventory.ContainsUID(uid) && !p.Entity.StorageItems.ContainsKey(uid)
                && !item.InWardrobe && item.Position==0 && SavedRune(item,"inwardrobe")==0 && SavedRune(item,"position")==0
                && (p.Equipment.TryGetItem((byte)9)==null || p.Equipment.TryGetItem((byte)9).UID!=uid),
                "ordinary withdrawal and duplicate request move exactly one owned item to bag: "+type);
            Check(!OwnedTitles(p).ContainsKey(20050006),"withdrawing one of five garments removes live Fashionist eligibility");
            p=ReloadGold(p);ConquerItemTable.LoadItems(p);new CoatStorage().Login(p);p.Entity.WTitles=null;new TitleStorage().Login(p);
            Check(p.Inventory.ContainsUID(uid) && !p.Entity.StorageItems.ContainsKey(uid) && p.Entity.RevoraPoint==22500,
                "successful withdrawal reloads as bag ownership with unchanged earned purse: "+type);
            new CoatStorage{Info=new CoatStorageProto{ActionId=CoatStorage.Action.Addtowardrobe,ItemGuid=uid,ItemId=(int)type}}.Handle(p);
            Check(!Fenced(p) && p.Entity.StorageItems.ContainsKey(uid) && OwnedTitles(p).ContainsKey(20050006),
                "redeposit restores the earned five-design collection without another purchase: "+type);
            // Re-equip the fifth design so the second case tests an equipped withdrawal.
            var fifth=p.Entity.StorageItems.Values.Single(i=>i.ID==188915);
            new CoatStorage{Info=new CoatStorageProto{ActionId=CoatStorage.Action.Addtowardrobe,ItemGuid=fifth.UID,ItemId=(int)fifth.ID}}.Handle(p);
        }
        return p;
    }
    static void VerifyEarnedBigFan(GameState previous) {
        // Fashion tests reload the champion; continue its current canonical session.
        var p=Kernel.GamePool[previous.Entity.UID];
        var original=Environment.CurrentDirectory;
        var shop=typeof(Entity).Assembly.GetType("Revora.Game.Features.EuroJerseyShop");
        var clock=shop.GetField("Clock",Hidden);object oldClock=clock.GetValue(null);
        var now=new DateTime(2032,2,2,8,0,0,DateTimeKind.Utc);clock.SetValue(null,(Func<DateTime>)(()=>now));
        try {
            Environment.CurrentDirectory=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(Entity).Assembly.Location),"../../../bin/Debug");
            ShopFile.Load();EShopFile.Load();EShopV2File.Load();Call(shop,null,"Initialize");
            Check(EShopFile.Shops[2888].Items.Contains(192655) && EShopFile.Shops[2888].Items.Contains(192665),"loaded CP mall stocks both Euro jersey designs");
            uint before=p.Entity.ConquerPoints,price=ConquerItemInformation.BaseInformations[192655].ConquerPointsWorth;
            Check(price>0 && price<=before,"earned boss CPs cover the loaded Euro jersey price: "+price);
            foreach(var rejected in new[]{BuyBook(192655,uint.MaxValue,2888),BuyBook(192655,1,1),BuyBook(192655,1,432)})
                ItemHandler.HandleBuyFromNPC(rejected,p);
            p.Trade.InTrade=true;ItemHandler.HandleBuyFromNPC(BuyBook(192655,1,2888),p);p.Trade.InTrade=false;
            Check(!Fenced(p) && p.Entity.ConquerPoints==before && !p.Inventory.Contains(192655,1),
                "Euro shop rejects excessive quantity unstocked shop remote NPC and active trade without debit");
            ItemHandler.HandleBuyFromNPC(BuyBook(192655,1,2888),p);
            var jersey=p.Inventory.Objects.Single(i=>i.ID==192655);
            Check(!Fenced(p) && !jersey.Bound && p.Entity.ConquerPoints==before-price,"ordinary CP mall buys the Euro jersey using earned CPs");
            ItemHandler.HandleBuyFromNPC(BuyBook(192655,1,2888),p);
            Check(p.Inventory.Objects.Count(i=>i.ID==192655)==1 && p.Entity.ConquerPoints==before-price,"immediate identical jersey purchase retry cannot charge twice");
            new CoatStorage{Info=new CoatStorageProto{ActionId=CoatStorage.Action.Addtowardrobe,ItemGuid=jersey.UID,ItemId=(int)jersey.ID}}.Handle(p);
            Check(!OwnedTitles(p).ContainsKey(20322032),"owned Euro jersey without Euro mount does not grant Big Fan");
            p.BindWorldMap(1002);p.Entity.X=396;p.Entity.Y=340;p.ActiveNpc=900000014;uint rvp=p.Entity.RevoraPoint;
            var mount=BuyRouteRune(p,86);
            new CoatStorage{Info=new CoatStorageProto{ActionId=CoatStorage.Action.Addtowardrobe,ItemGuid=mount.UID,ItemId=(int)mount.ID}}.Handle(p);
            Check(mount.ID==200564 && p.Entity.RevoraPoint==rvp-2500 && OwnedTitles(p).ContainsKey(20322032)
                && !OwnedTitles(p).ContainsKey(20332033),"earned jersey and purchased Euro mount grant Big Fan but not six-design Euro Collector");
            TitleRequest(20322032,TitleStorage.Action.Equip).Handle(p);Check(EntityTable.SaveEntity(p),"Big Fan earned collection saves");
            p=ReloadGold(p);ConquerItemTable.LoadItems(p);new CoatStorage().Login(p);p.Entity.WTitles=null;new TitleStorage().Login(p);
            Check(p.Entity.EquippedTitle==20322032 && OwnedTitles(p).ContainsKey(20322032) && p.Entity.ConquerPoints==before-price
                && p.Entity.RevoraPoint==rvp-2500,"Big Fan and both exact currency debits survive production reload");
            uint secondPrice=ConquerItemInformation.BaseInformations[192665].ConquerPointsWorth;before=p.Entity.ConquerPoints;
            Query("CREATE FUNCTION reject_euro_jersey() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER reject_euro_jersey BEFORE INSERT ON revora_euro_jersey_purchases FOR EACH ROW EXECUTE FUNCTION reject_euro_jersey()");
            try {ItemHandler.HandleBuyFromNPC(BuyBook(192665,1,2888),p);}
            finally {Query("DROP TRIGGER reject_euro_jersey ON revora_euro_jersey_purchases; DROP FUNCTION reject_euro_jersey()");}
            Check(secondPrice>0 && secondPrice<=before && Fenced(p) && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==before
                && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id=192665",p.Entity.UID)==0,"suppressed jersey receipt rolls back payment and item together");
            p=ReloadGold(p);ConquerItemTable.LoadItems(p);new CoatStorage().Login(p);p.Entity.BoundCps=secondPrice*2;
            Check(EntityTable.SaveEntity(p),"bound-currency transaction fixture saves its supplied bound CP balance");
            var boundRequest=BuyBook(192665,1,2888);boundRequest.dwExtraInfo3=2;
            ItemHandler.HandleBuyFromNPC(boundRequest,p);ItemHandler.HandleBuyFromNPC(boundRequest,p);
            Check(!Fenced(p) && p.Entity.ConquerPoints==before && p.Entity.BoundCps==secondPrice
                && p.Inventory.Objects.Single(i=>i.ID==192665).Bound,
                "bound jersey uses only bound CPs and duplicate request pays once");
            Check(EntityTable.SaveEntity(p),"bound jersey saves before production reload");
            p=ReloadGold(p);ConquerItemTable.LoadItems(p);new CoatStorage().Login(p);
            Check(p.Entity.BoundCps==secondPrice && p.Entity.ConquerPoints==before && p.Inventory.Objects.Single(i=>i.ID==192665).Bound,
                "bound jersey and separated purses persist through production reload");
        } finally {Environment.CurrentDirectory=original;clock.SetValue(null,oldClock);}
    }
    static void VerifyEarnedFashionPlaces(GameState champion) {
        var owners=new GameState[]{champion,null,null};var town=Kernel.Maps[1002];
        for(int rank=1;rank<3;rank++) {
            var p=FullRunePlayer(town,15);p.Entity.DailySignDays=new byte[0];
            if(rank==1)p.Entity.Body=2001;
            Check(p.Entity.RevoraPoint==0 && EntityTable.SaveEntity(p),"Fashion challenger begins without purchase funds: "+rank);
            var next=DailySchedule().Where(t=>t>dailyNow).OrderBy(t=>t).First();DailyTick(next);var boss=DailyActive();
            if(boss==null)throw new Exception("Fashion acquisition boss did not spawn.");
            p.BindWorldMap(boss.MapID);p.Entity.X=boss.X;p.Entity.Y=boss.Y;uint damage=boss.MaxHitpoints;
            Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,boss,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=boss.UID},ref damage,null);
            Check(!Fenced(p) && p.Entity.RevoraPoint==50000 && Scalar("SELECT count(*) FROM revora_daily_boss_runs WHERE killer=@uid AND paid",p.Entity.UID)==1,
                "Fashion challenger earns one unique scheduled bounty: "+rank);
            p.BindWorldMap(1002);p.Entity.X=396;p.Entity.Y=340;p.ActiveNpc=900000014;new TitleStorage().Login(p);
            for(int i=0;i<3-rank;i++) {
                var item=BuyRouteRune(p,(byte)(70+i));
                new CoatStorage{Info=new CoatStorageProto{ActionId=CoatStorage.Action.Addtowardrobe,ItemGuid=item.UID,ItemId=(int)item.ID}}.Handle(p);
            }
            if(rank==1) {
                Check(!OwnedTitles(p).ContainsKey(20642064),"Bright Purple Romance alone does not grant Sweet Fairy");
                foreach(byte option in new byte[]{82,83}) {
                    var item=BuyRouteRune(p,option);
                    new CoatStorage{Info=new CoatStorageProto{ActionId=CoatStorage.Action.Addtowardrobe,ItemGuid=item.UID,ItemId=(int)item.ID}}.Handle(p);
                }
                Check(OwnedTitles(p).ContainsKey(20632063) && OwnedTitles(p).ContainsKey(20642064) && !OwnedTitles(p).ContainsKey(20652065),
                    "ordinary female earns Sweet Fairy and Love Joker from purchased complete set without male reward");
                TitleRequest(20642064,TitleStorage.Action.Equip).Handle(p);
                Check(p.Entity.EquippedTitle==20642064 && EntityTable.SaveEntity(p),"earned Sweet Fairy equips and saves");
                p=ReloadGold(p);ConquerItemTable.LoadItems(p);new CoatStorage().Login(p);p.Entity.WTitles=null;new TitleStorage().Login(p);
                Check(p.Entity.Body==2001 && p.Entity.EquippedTitle==20642064 && OwnedTitles(p).ContainsKey(20642064) && p.Entity.RevoraPoint==40000,
                    "female collection title body and exact earned purse persist through production reload");
            }
            Check(!Fenced(p) && EntityTable.SaveEntity(p),"Fashion challenger's bought collection saves: "+rank);owners[rank]=p;
        }
        var fashion=typeof(Entity).Assembly.GetType("Revora.Game.Features.FashionCompetition");
        var clock=fashion.GetField("Clock",Hidden);object previous=clock.GetValue(null);
        var titleClock=TitleRules.GetField("Clock",Hidden);object previousTitle=titleClock.GetValue(null);
        DateTime now=new DateTime(2032,2,1,8,0,0,DateTimeKind.Utc),close=now;
        clock.SetValue(null,(Func<DateTime>)(()=>now));titleClock.SetValue(null,(Func<DateTime>)(()=>now));
        try {
            Call(fashion,null,"Initialize");Check((bool)Call(fashion,null,"Freeze",close),"three earned collections freeze into one real monthly ranking");
            for(int rank=0;rank<3;rank++) {
                var p=owners[rank];int title=20522052+rank*10001;uint funds=p.Entity.RevoraPoint;
                Check(Scalar("SELECT placement FROM revora_fashion_results WHERE season='2032-01' AND uid=@uid",p.Entity.UID)==rank+1,
                    "bounty-funded collection earns actual Fashion placement "+(rank+1));
                new TitleStorage().Login(p);TitleRequest(title,TitleStorage.Action.Equip).Handle(p);
                Check(!Fenced(p) && p.Entity.EquippedTitle==title,"ordinary collection earner equips Fashion placement reward "+(rank+1));
                p=ReloadGold(p);ConquerItemTable.LoadItems(p);new CoatStorage().Login(p);p.Entity.WTitles=null;new TitleStorage().Login(p);owners[rank]=p;
                Check(p.Entity.EquippedTitle==title && OwnedTitles(p).ContainsKey(title) && p.Entity.RevoraPoint==funds
                    && Scalar("SELECT count(*) FROM revora_event_awards WHERE source='fashion-revora-v1' AND uid=@uid",p.Entity.UID)==1,
                    "earned Fashion placement and unchanged remaining purse survive production reload: "+(rank+1));
            }
            now=close.AddDays(30);
            for(int rank=0;rank<3;rank++) {
                var p=owners[rank];int title=20522052+rank*10001;new TitleStorage().Login(p);
                Check(!OwnedTitles(p).ContainsKey(title) && p.Entity.EquippedTitle==0,"earned Fashion placement expires at the original close deadline: "+(rank+1));
            }
        } finally {clock.SetValue(null,previous);titleClock.SetValue(null,previousTitle);}
    }
    static void VerifyMountKit(GameState p) {
        var feature=typeof(Entity).Assembly.GetType("Revora.Game.Features.RevoraMasterSystem");Call(feature,null,"InitializeMountKits");
        uint npc=Revora.Game.Features.RevoraMasterSystem.NpcId;
        var town=Kernel.Maps[1002];town.AddNpc(new NpcSpawn{UID=npc,MapID=1002,X=410,Y=354,Mesh=46030,Type=Enums.NpcType.Talker});
        p.Entity.Teleport(1002,410,353);p.ActiveNpc=npc;
        Action<byte> menu=option=>{p.ActiveNpc=npc;Npcs.GetDialog(new NpcRequest(true){NpcID=npc,OptionID=option},p);};
        uint before=p.Entity.ConquerPoints,price=Revora.Game.Features.RevoraMasterSystem.MountKitPrice;
        var original=p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i).ToArray();
        Check(before>=price && original.Length<=35,"boss-funded collector can afford configured mount kit without supplied CPs");
        menu(14);Check(p.Entity.ConquerPoints==before && p.Inventory.Count==original.Length,"unoffered mount confirmation cannot charge");
        menu(4);menu(255);menu(14);Check(p.Entity.ConquerPoints==before && p.Inventory.Count==original.Length,"cancelled mount quote cannot charge");
        var clock=feature.GetField("MountClock",Hidden);var oldClock=clock.GetValue(null);var now=DateTime.UtcNow;
        try {clock.SetValue(null,new Func<DateTime>(()=>now));menu(4);now=now.AddMinutes(2);menu(14);
            Check(p.Entity.ConquerPoints==before && p.Inventory.Count==original.Length,"expired mount quote cannot charge at its exact deadline");
        } finally {clock.SetValue(null,oldClock);}
        menu(4);p.Trade.InTrade=true;menu(14);p.Trade.InTrade=false;menu(14);
        Check(p.Entity.ConquerPoints==before && p.Inventory.Count==original.Length,"trade invalidates mount confirmation without retaining a reusable offer");
        menu(4);p.Entity.X=430;menu(14);p.Entity.X=410;
        Check(p.Entity.ConquerPoints==before && p.Inventory.Count==original.Length,"distant mount confirmation cannot charge");
        menu(4);p.Booth=new Revora.Game.ConquerStructures.Booth();menu(14);p.Booth=null;menu(14);
        Check(p.Entity.ConquerPoints==before && p.Inventory.Count==original.Length,"booth invalidates mount quote without charging after booth closes");
        menu(4);Kernel.GamePool.Remove(p.Entity.UID);menu(14);Kernel.GamePool[p.Entity.UID]=p;menu(14);
        Check(p.Entity.ConquerPoints==before && p.Inventory.Count==original.Length,"stale session loses its mount quote and cannot charge after restoration");
        menu(4);uint configured=Config.RevoraMasterMountKitPrice;
        try {Config.RevoraMasterMountKitPrice=configured+1;menu(14);} finally {Config.RevoraMasterMountKitPrice=configured;}
        menu(14);Check(p.Entity.ConquerPoints==before && p.Inventory.Count==original.Length,"changed configured price invalidates the quoted purchase without later reuse");
        var fillers=new System.Collections.Generic.List<ConquerItem>();while(p.Inventory.Count<36)fillers.Add(Pack(p,1088001,1));menu(4);menu(14);
        Check(!Fenced(p) && p.Entity.ConquerPoints==before && p.Inventory.Count==36,"mount kit needs five real bag slots before any payment");
        foreach(var filler in fillers)DisRemove(p,filler);
        foreach(string fault in new[]{"300000","201009","202009","203009","204009","properties","purse","receipt"}) {
            string table=fault=="purse"?"entities":fault=="receipt"?"revora_mount_kit_purchases":"items";
            string condition=table=="items" && fault!="properties"?"NEW.id="+fault:"true";
            Query("CREATE FUNCTION suppress_mount_kit() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF "+condition+" THEN RETURN NULL; END IF; RETURN NEW; END';CREATE TRIGGER suppress_mount_kit BEFORE "+(fault=="purse" || fault=="properties"?"UPDATE":"INSERT")+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_mount_kit()");
            try{menu(4);menu(14);}finally{Query("DROP TRIGGER suppress_mount_kit ON "+table+";DROP FUNCTION suppress_mount_kit()");}
            Check(Fenced(p) && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==before
                && Scalar("SELECT count(*) FROM revora_mount_kit_purchases WHERE uid=@uid",p.Entity.UID)==0
                && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0",p.Entity.UID)==original.Length,
                "mount kit failure rolls back payment, every item and receipt: "+fault);
            p=ReloadBall(p);p.Entity.Teleport(1002,410,353);p.ActiveNpc=npc;
        }
        menu(4);System.Threading.Tasks.Parallel.For(0,8,i=>menu(14));
        var kit=p.Inventory.Objects.Where(i=>!original.Contains(i.UID)).OrderBy(i=>i.ID).ToArray();
        Check(!Fenced(p) && p.Entity.ConquerPoints==before-price && kit.Length==5
            && kit.Select(i=>i.ID).SequenceEqual(new uint[]{201009,202009,203009,204009,300000})
            && kit.All(i=>i.Bound && i.Plus==12 && i.Bless==7 && i.Position==0 && i.StackSize==1)
            && Scalar("SELECT count(*) FROM revora_mount_kit_purchases WHERE uid=@uid",p.Entity.UID)==1,
            "parallel ordinary mount confirmation buys one complete bound maxed kit at quoted price");
        var ids=kit.Select(i=>i.UID).ToArray();p=ReloadBall(p);menu(14);
        Check(!Fenced(p) && p.Entity.ConquerPoints==before-price && ids.All(id=>p.Inventory.ContainsUID(id))
            && ids.Select(id=>p.Inventory.Objects.Single(i=>i.UID==id)).All(i=>i.Plus==12 && i.Bless==7 && i.Bound),
            "production mount-kit reload preserves exact paid kit and cannot replay confirmation: cp="+p.Entity.ConquerPoints+" expected="+(before-price)+" fenced="+Fenced(p)+" items="+string.Join(";",p.Inventory.Objects.Where(i=>ids.Contains(i.UID)).Select(i=>i.ID+":"+i.Plus+":"+i.Bless+":"+i.Bound)));
        foreach(var item in kit) {
            var saved=ConquerItemTable.LoadItem(item.UID);
            Check(saved.SocketOne==item.SocketOne && saved.SocketTwo==item.SocketTwo && saved.Effect==item.Effect
                && saved.SocketProgress==item.SocketProgress && saved.Durability==item.Durability && saved.MaximDurability==item.MaximDurability,
                "mount kit production item load retains sockets, appearance and durability: "+item.ID);
        }
        p.Entity.Teleport(1002,410,353);p.ActiveNpc=npc;
        uint paidBefore=p.Entity.ConquerPoints;int bagBefore=p.Inventory.Count;
        var install=feature.GetField("BeforeMountInstall",Hidden);var priorInstall=install.GetValue(null);
        try {
            install.SetValue(null,new Action<GameState>(owner=>{throw new InvalidOperationException("fixture projection failure");}));
            menu(4);menu(14);
        } finally {install.SetValue(null,priorInstall);}
        Check(Fenced(p) && p.Inventory.Count==bagBefore
            && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==paidBefore-price
            && Scalar("SELECT count(*) FROM revora_mount_kit_purchases WHERE uid=@uid",p.Entity.UID)==2
            && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0",p.Entity.UID)==bagBefore+5,
            "postcommit mount projection failure keeps one complete paid purchase without refund or partial database delivery");
        p=ReloadBall(p);menu(14);
        Check(!Fenced(p) && p.Entity.ConquerPoints==paidBefore-price && p.Inventory.Count==bagBefore+5
            && Scalar("SELECT count(*) FROM revora_mount_kit_purchases WHERE uid=@uid",p.Entity.UID)==2,
            "production reconnect recovers the committed mount kit once without a second payment or refund");
        Call(typeof(Achievement),p.Entity.MyAchievement,"Refresh",DateTime.UtcNow,true);
        Check(!HasAchievement(p,10429),"socketed mount-kit purchase in the bag does not earn equipped-socket credit");
        var fan=p.Inventory.Objects.Single(i=>i.UID==kit.Single(k=>k.ID==201009).UID);
        ItemHandler.EquipItem(new ItemUsage(true){UID=fan.UID,dwParam=10,Action=ItemUsage.Mode.EquipItem},p);
        Call(typeof(Achievement),p.Entity.MyAchievement,"Refresh",DateTime.UtcNow,true);
        Check(!Fenced(p) && p.Equipment.TryGetItem(fan.UID)!=null && HasAchievement(p,10429),
            "ordinary boss-funded mount-kit Fan equip earns socket milestone");
        p=ReloadBall(p);p.Entity.MyAchievement.Send();
        Check(p.Equipment.TryGetItem(fan.UID)!=null && HasAchievement(p,10429) && p.Entity.ConquerPoints==paidBefore-price
            && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10429",p.Entity.UID)==1,
            "socketed earning route retains equipment, one receipt and paid purse through production login");

    }

}
