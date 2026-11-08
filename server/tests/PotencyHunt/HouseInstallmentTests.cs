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
    static readonly Type Installments=typeof(Entity).Assembly.GetType("Revora.Game.Features.HouseInstallments");
    static void ManagerNpc(GameState p,byte option){AdventureNpc(p,91017,option);}
    static GameState InstallmentOwner(Map town)
    {
        var p=HousePlayer(town);
        for(int level=1;level<=4;level++){BuyHouseCert(p,level);HouseNpc(p,level==1?(byte)1:(byte)3);HouseNpc(p,40);}
        if(Fenced(p) || Scalar("SELECT houselevel FROM house WHERE id=@uid",p.Entity.UID)!=4)throw new Exception("Installment owner setup failed.");
        HouseNpc(p,11);AdventureNear(p,91017);return p;
    }
    static void InstallmentStock(GameState p,int kind,int count)
    {
        uint[] types={1088000,723087,1072031,720027,700003};
        for(int i=0;i<count;i++)Pack(p,types[kind],1);
    }
    static void Deposit(GameState p,int kind){ManagerNpc(p,(byte)(kind+1));ManagerNpc(p,40);}
    static GameState ReloadInstallmentMiner(GameState p)
    {
        p=ReloadBall(p);p.CalculateStatBonus();p.CalculateHPBonus();
        if(p.Entity.MaxHitpoints==0)throw new Exception("Installment journey login did not restore derived HP.");
        return p;
    }
    static void VerifyInstallmentEarning(string runtime,Map town)
    {
        Call(Installments,null,"PlaceManager");Call(BookShop,null,"Initialize");
        Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.TravelScrollShop"),null,"Initialize");ShopFile.Load();
        MiningNpc(town,91008);town.Npcs[91008].Type=Enums.NpcType.Shop;
        Call(Magnolia,null,"Initialize");Call(ChiCash,null,"Initialize");Call(MiningService,null,"Initialize");
        Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.ExplorationWorld"),null,"PrepareMaps");
        var garden=World(runtime,1806);MiningNpc(garden,44100);MiningNpc(town,79882);
        var phoenix=World(runtime,1011);World(runtime,1025);MiningNpc(phoenix,125);MiningNpc(town,1);MiningNpc(town,3);
        foreach(ushort id in new ushort[]{1002,1011,1025,1806})DMaps.MapPaths[id]="maps\\"+id+".map";
        var p=InstallmentOwner(town);p.CalculateStatBonus();p.CalculateHPBonus();uint initialCp=p.Entity.ConquerPoints;uint initialRvp=p.Entity.RevoraPoint;ushort initialStudy=p.Entity.SubClasses.StudyPoints;
        // Currency and the four-level prerequisite are fixture inputs; every final material is acquired through production services.
        p.Entity.Money=100000;EntityTable.UpdateMoney(p);ulong initialSilver=p.Entity.Money;
        DateTime now=new DateTime(2034,4,1,19,0,0,DateTimeKind.Utc);int gradeDraw=99,bonusSlot=0,bonusDraw=19;
        var oldFlowerClock=Magnolia.GetField("Clock",Hidden).GetValue(null);
        var oldSeed=Magnolia.GetField("SeedRoll",Hidden).GetValue(null);var oldBonus=Magnolia.GetField("BonusRoll",Hidden).GetValue(null);
        var oldMineClock=MiningService.GetField("Clock",Hidden).GetValue(null);var oldMineRoll=MiningService.GetField("Roll",Hidden).GetValue(null);
        var oldCashClock=ChiCash.GetField("Clock",Hidden).GetValue(null);int chance=Config.MiningNormalGemBasisPoints;
        Magnolia.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>now));
        Magnolia.GetField("SeedRoll",Hidden).SetValue(null,new Func<int,int>(max=>gradeDraw));
        Magnolia.GetField("BonusRoll",Hidden).SetValue(null,new Func<int,int>(max=>max==100?bonusDraw:bonusSlot));
        MiningService.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>now));
        ChiCash.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>now));
        try {
            ManagerNpc(p,7);
            long earned=0;
            for(int day=0;day<56;day++) {
                now=now.AddDays(1);bool gem=day>=26;
                gradeDraw=gem?95:99;bonusDraw=day==0?20:19;bonusSlot=gem?5+(day-26)%7:day<=5?0:1;
                uint expected=day==0?0:gem?700003u+(uint)((day-26)%7)*10:day<=5?1088000u:720027u;
                AdventureNear(p,79882);AdventureNpc(p,79882,5);AdventureNear(p,44100);
                FlowerSeed(p);FlowerWire(p,5);FlowerWire(p,6);
                var seed=p.Inventory.Objects.Single(i=>i.ID==(gem?729309u:729310u));
                var bed=FlowerTile(garden,gem?(ushort)98:(ushort)106,gem?(ushort)111:(ushort)98);
                AdventureWalk(p,bed.Item1,bed.Item2);ItemHandler.UseItem(seed,p);
                Check(!Fenced(p) && Flowers(p)==day+1 && (expected==0 || p.Inventory.Objects.Count(i=>i.ID==expected)==1),
                    "installment materials earned from owned daily planting, including no-bonus outcome: day "+day);
                if(CashUnits(p)>=7)CashUse(p,p.Inventory.Objects.First(i=>i.ID==729304));
                AdventureNear(p,44100);FlowerNpc(p,2);
                p=ReloadInstallmentMiner(p);
                Check(!Fenced(p) && p.Entity.MapID==1002 && (expected==0 || p.Inventory.Objects.Count(i=>i.ID==expected)==1),
                    "earned installment gift survives garden return and production reload: day "+day+", map="+p.Entity.MapID+", fenced="+Fenced(p)+", gifts="+p.Inventory.Objects.Count(i=>i.ID==expected));
                if(expected!=0) {
                    uint material=p.Inventory.Objects.Single(i=>i.ID==expected).UID;
                    AdventureNear(p,30156);HouseNpc(p,11);AdventureNear(p,91017);Deposit(p,gem?4:day<=5?0:3);earned++;
                    Check(!Fenced(p) && InstallmentCount(p)==earned && Scalar("SELECT count(*) FROM items WHERE uid=@uid",material)==0
                        && Scalar("SELECT count(*) FROM revora_house_installment_items WHERE item_uid=@uid AND amount=1",material)==1,
                        "manager records the exact earned gift once: day "+day);
                    ManagerNpc(p,40);ManagerNpc(p,7);
                }
            }
            Check(Flowers(p)==56 && CashReceipts(p)==8 && CashUnits(p)==0 && InstallmentCount(p)==55,
                "fifty-six ordinary daily completions yield all five Dragon Balls, twenty scrolls and thirty eligible Super Gems");
            AdventureNear(p,91008);ItemHandler.HandleBuyFromNPC(BuyBook(723087,20,91008),p);
            p=ReloadInstallmentMiner(p);AdventureNear(p,30156);HouseNpc(p,11);AdventureNear(p,91017);Deposit(p,1);
            Check(!Fenced(p) && InstallmentCount(p)==75 && p.Entity.ConquerPoints==initialCp-56-100+56*Config.DailyQuestCp+8*Config.ChiTokenCashCp,
                "earned daily currency funds twenty amulets at the active five-CP price with exact full-route balance");
            ManagerNpc(p,7);AdventureNear(p,1);ItemHandler.HandleBuyFromNPC(BuyBook(562000,1,1),p);
            var tool=p.Inventory.Objects.Single(i=>i.ID==562000);ItemHandler.EquipItem(new ItemUsage(true){UID=tool.UID,dwParam=4,Action=ItemUsage.Mode.EquipItem},p);
            Config.MiningNormalGemBasisPoints=0;
            // Existing Maple Forest ore selector: copper miss, Euxenite hit, grade zero.
            int oreDraw=0;MiningService.GetField("Roll",Hidden).SetValue(null,new Func<int,int>(max=>{if(max!=100)return 0;return ++oreDraw%3==2?99:0;}));
            for(int trip=0;trip<2;trip++) {
                AdventureNear(p,3);ItemHandler.HandleBuyFromNPC(BuyBook(1060020,1,3),p);
                AdventureGo(p,1011);AdventureNear(p,125);AdventureNpc(p,125,1);StartMining(p);
                for(int i=0;i<20;i++){now=now.AddSeconds(2);MineTick(p);}
                Check(!Fenced(p) && MineCount(p)==20*(trip+1) && p.Inventory.Objects.Count(i=>i.ID==1072031)==20,
                    "ordinary purchased pickaxe mines twenty exact Euxenite Ores for installment trip "+trip);
                UseHouseReturn(p);p=ReloadInstallmentMiner(p);AdventureNear(p,30156);HouseNpc(p,11);AdventureNear(p,91017);Deposit(p,2);
                Check(!Fenced(p) && InstallmentCount(p)==95+20*trip,"mined ore delivery persists alongside all other earned materials: "+trip);
                if(trip==0)ManagerNpc(p,7);
            }
            Check(!HasAchievement(p,10122) && p.Entity.Money==initialSilver-1400 && p.Inventory.Count==0,
                "all 115 earned materials retain exact tool and travel costs and await construction confirmation");
            ManagerNpc(p,6);Parallel.For(0,12,i=>ManagerNpc(p,40));p=ReloadInstallmentMiner(p);new TitleStorage().Login(p);
            Check(!Fenced(p) && HasAchievement(p,10122) && InstallmentCount(p)==115
                && Scalar("SELECT houselevel FROM house WHERE id=@uid",p.Entity.UID)==5
                && Scalar("SELECT count(*) FROM revora_house_installments WHERE uid=@uid AND completed",p.Entity.UID)==1,
                "complete earned installment contract survives concurrent finish and production reload");
            ManagerNpc(p,7);AdventureNear(p,30156);HouseNpc(p,2);p=ReloadInstallmentMiner(p);new TitleStorage().Login(p);
            Check(!Fenced(p) && p.Map.BaseID==3024 && p.Map.ID!=3024 && HasAchievement(p,10122),
                "ordinary entry and reconnect reach the private level-five house earned from all 115 materials: map="+p.Entity.MapID+", base="+p.Map.BaseID+", achievement="+HasAchievement(p,10122));
            Check(Scalar("SELECT count(*) FROM revora_house_installment_items i JOIN revora_magnolia_bonuses b ON b.uid=i.uid AND i.item_uid=ANY(b.item_uids) WHERE i.uid=@uid AND i.item_type=b.item_type AND i.amount=1",p.Entity.UID)==55
                && Scalar("SELECT count(*) FROM revora_magnolia_bonuses WHERE uid=@uid AND item_type=0 AND roll=20",p.Entity.UID)==1,
                "all fifty-five delivered garden gifts retain exact earning provenance and one no-bonus receipt");
            Check(p.Entity.ConquerPoints==initialCp-156+56*Config.DailyQuestCp+8*Config.ChiTokenCashCp
                && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==p.Entity.ConquerPoints
                && p.Entity.RevoraPoint==initialRvp+56*Config.DailyQuestRevoraPoints+8*Config.ChiTokenCashRevoraPoints
                && Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",p.Entity.UID)==p.Entity.RevoraPoint
                && p.Entity.SubClasses.StudyPoints==initialStudy+26*300+30*100
                && Scalar("SELECT studypoints FROM entities WHERE uid=@uid",p.Entity.UID)==p.Entity.SubClasses.StudyPoints
                && p.Entity.Money==initialSilver-1400 && Scalar("SELECT money FROM entities WHERE uid=@uid",p.Entity.UID)==(long)p.Entity.Money,
                "final saved and runtime CPs, RVPs, Study Points and silver reconcile across the complete earned house journey");
        }finally{
            Magnolia.GetField("Clock",Hidden).SetValue(null,oldFlowerClock);Magnolia.GetField("SeedRoll",Hidden).SetValue(null,oldSeed);
            Magnolia.GetField("BonusRoll",Hidden).SetValue(null,oldBonus);MiningService.GetField("Clock",Hidden).SetValue(null,oldMineClock);
            MiningService.GetField("Roll",Hidden).SetValue(null,oldMineRoll);ChiCash.GetField("Clock",Hidden).SetValue(null,oldCashClock);
            Config.MiningNormalGemBasisPoints=chance;
        }
    }
    static long InstallmentCount(GameState p){return Scalar("SELECT COALESCE(sum(amount),0) FROM revora_house_installment_items WHERE uid=@uid",p.Entity.UID);}
    static void PayInstallments(GameState p)
    {
        int[] amounts={5,20,40,20,30};for(int k=0;k<5;k++){InstallmentStock(p,k,amounts[k]);Deposit(p,k);}
        if(Fenced(p) || InstallmentCount(p)!=115)throw new Exception("Full installment fixture failed.");
    }
    static void VerifyPurchasedHouseAmulets(Map town)
    {
        ShopFile.Load();
        // Use the installed actor as well as the real item/stock loaders.
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT mapid,cellx,celly,lookface,type FROM public.npcs WHERE id=@id",c)){
            q.Parameters.AddWithValue("id",91008L);using(var r=q.ExecuteReader()){
                if(!r.Read() || Convert.ToUInt16(r[0])!=1002 || Convert.ToByte(r[4])!=(byte)Enums.NpcType.Shop)throw new Exception("Installed subclass shop actor missing.");
                if(!town.Npcs.ContainsKey(91008))town.AddNpc(new NpcSpawn{UID=91008,MapID=1002,X=Convert.ToUInt16(r[1]),Y=Convert.ToUInt16(r[2]),Mesh=Convert.ToUInt16(r[3]),Type=Enums.NpcType.Shop});
            }
        }}
        Check(ConquerItemInformation.BaseInformations[723087].ConquerPointsWorth==5 && ShopFile.Shops[91008].Items.Contains(723087),
            "installment amulet journey uses the loaded five-CP item and installed shop stock");
        var p=InstallmentOwner(town);uint initial=p.Entity.ConquerPoints;
        ManagerNpc(p,7);AdventureNear(p,91008);
        ItemHandler.HandleBuyFromNPC(BuyBook(723087,2001,91008),p);
        Check(!Fenced(p) && p.Inventory.Count==0 && p.Entity.ConquerPoints==initial && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==initial,
            "unaffordable amulet order preserves CPs before installment shopping");
        for(int visit=1;visit<=2;visit++){
            if(visit==2){ManagerNpc(p,7);AdventureNear(p,91008);}
            ItemHandler.HandleBuyFromNPC(BuyBook(723087,10,91008),p);
            var stack=p.Inventory.Objects.Single(i=>i.ID==723087);uint bought=stack.UID;
            Check(!Fenced(p) && !stack.Bound && stack.StackSize==10 && SavedRune(stack,"stacksize")==10
                && p.Entity.ConquerPoints==initial-(uint)(50*visit) && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==p.Entity.ConquerPoints,
                "ordinary ten-amulet purchase persists the exact stack and fifty-CP debit: visit "+visit);
            p=ReloadBall(p);
            Check(!Fenced(p) && p.Entity.ConquerPoints==initial-(uint)(50*visit) && p.Inventory.Objects.Single().UID==bought
                && p.Inventory.Objects.Single().StackSize==10,"purchased amulets and paid balance survive production reload: visit "+visit);
            AdventureNear(p,30156);HouseNpc(p,11);AdventureNear(p,91017);Deposit(p,1);
            Check(!Fenced(p) && p.Inventory.Count==0 && InstallmentCount(p)==10*visit
                && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id=723087",p.Entity.UID)==0
                && Scalar("SELECT count(*) FROM revora_house_installment_items WHERE item_uid=@uid AND kind=1 AND amount=10",bought)==1,
                "manager consumes the exact purchased stack into durable installment evidence: visit "+visit);
            p=ReloadBall(p);AdventureNear(p,91017);ManagerNpc(p,40);Deposit(p,1);
            Check(!Fenced(p) && p.Inventory.Count==0 && InstallmentCount(p)==10*visit && p.Entity.ConquerPoints==initial-(uint)(50*visit),
                "delivery reconnect and repeated confirmation cannot consume or charge again: visit "+visit);
        }
        Check(Scalar("SELECT paid[2] FROM revora_house_installments WHERE uid=@uid",p.Entity.UID)==20
            && Scalar("SELECT houselevel FROM house WHERE id=@uid",p.Entity.UID)==4 && !HasAchievement(p,10122),
            "twenty purchased amulets cost one hundred CPs and do not bypass the other installment requirements");
        ManagerNpc(p,7);
    }
    static void VerifyHouseInstallments(Map town)
    {
        // Exercise production showroom construction, not only a preloaded fixture map.
        Kernel.Maps[3024].Dispose();MapsTable.MapInformations.Remove(3024);
        Call(Installments,null,"Initialize");Call(Installments,null,"PlaceManager");Call(Installments,null,"PlaceManager");
        Check(Kernel.Maps[3024].Npcs[91017].Mesh==12970 && MapsTable.MapInformations[3024].Owner==0 && Kernel.Maps[3024].Npcs.Count==1,
            "idempotent showroom overlay installs one manager without private-house ownership");
        VerifyPurchasedHouseAmulets(town);
        var newcomer=HousePlayer(town);HouseNpc(newcomer,11);
        Check(newcomer.Entity.MapID==1002 && newcomer.Inventory.Count==0,"display-home visit requires a level-four owner");
        var p=InstallmentOwner(town);ushort home=(ushort)Scalar("SELECT map_id FROM revora_house_maps WHERE uid=@uid",p.Entity.UID);
        Check(!Fenced(p) && p.Account.State==AccountTable.AccountState.Player && p.Entity.MapID==3024
            && p.Map.BaseID==3024 && home!=3024,"ordinary level-four owner reaches manager in a separate display home");
        ulong silver=p.Entity.Money;uint cp=p.Entity.ConquerPoints;
        ManagerNpc(p,6);ManagerNpc(p,40);
        Check(InstallmentCount(p)==0 && Scalar("SELECT houselevel FROM house WHERE id=@uid",p.Entity.UID)==4,"unpaid and unoffered completion cannot upgrade the house");
        var amulets=Pack(p,723087,25);amulets.Bound=true;ConquerItemTable.UpdateBound(amulets,p);
        ManagerNpc(p,2);Parallel.For(0,12,i=>ManagerNpc(p,40));
        Check(!Fenced(p) && amulets.StackSize==5 && SavedRune(amulets,"stacksize")==5 && InstallmentCount(p)==20
            && Scalar("SELECT paid[2] FROM revora_house_installments WHERE uid=@uid",p.Entity.UID)==20,
            "partial stack delivery caps at twenty amulets and concurrent confirms consume once");
        p=ReloadBall(p);AdventureNear(p,91017);Deposit(p,1);
        Check(!Fenced(p) && p.Entity.MapID==3024 && InstallmentCount(p)==20 && p.Inventory.Objects.Single().StackSize==5,
            "showroom login reload preserves deliveries and excess bound materials");
        ManagerNpc(p,7);AdventureNear(p,30156);BuyHouseCert(p,5);HouseNpc(p,3);HouseNpc(p,40);
        Check(!Fenced(p) && Scalar("SELECT houselevel FROM house WHERE id=@uid",p.Entity.UID)==4 && p.Inventory.Objects.Any(i=>i.ID==711553)
            && InstallmentCount(p)==20,"alternate certificate construction cannot strand an active installment contract");
        // Keep the purchased certificate and leftover stack; deliver forty ores in two visits.
        HouseNpc(p,11);AdventureNear(p,91017);
        InstallmentStock(p,2,17);Deposit(p,2);p=ReloadBall(p);AdventureNear(p,91017);
        Check(InstallmentCount(p)==37,"partial ore installment survives production reload");
        InstallmentStock(p,2,23);Deposit(p,2);InstallmentStock(p,0,5);Deposit(p,0);
        InstallmentStock(p,3,20);Deposit(p,3);InstallmentStock(p,4,30);Deposit(p,4);
        Check(!Fenced(p) && InstallmentCount(p)==115 && !HasAchievement(p,10122) && Scalar("SELECT houselevel FROM house WHERE id=@uid",p.Entity.UID)==4,
            "all five categories may arrive out of order and final deposit alone does not award construction");
        Query("INSERT INTO furniture(id,nameowner,type,lookface,mapid,cellx,celly) VALUES(93000017,@name,26,100,@map,50,50)","name",p.Entity.Name,"map",(long)home);
        ManagerNpc(p,6);ManagerNpc(p,40);
        Check(!Fenced(p) && Scalar("SELECT houselevel FROM house WHERE id=@uid",p.Entity.UID)==4 && InstallmentCount(p)==115,
            "furniture blocks final construction without losing paid installments");
        Query("DELETE FROM furniture WHERE id=@id","id",93000017L);
        ManagerNpc(p,6);Parallel.For(0,12,i=>ManagerNpc(p,40));
        Check(!Fenced(p) && Scalar("SELECT houselevel FROM house WHERE id=@uid",p.Entity.UID)==5 && HasAchievement(p,10122)
            && Scalar("SELECT count(*) FROM revora_house_installments WHERE uid=@uid AND completed",p.Entity.UID)==1
            && Scalar("SELECT map_id FROM revora_house_maps WHERE uid=@uid",p.Entity.UID)==home && p.Entity.Money==silver && p.Entity.ConquerPoints==cp-10000,
            "final confirmation atomically upgrades the existing home and achievement once with no installment currency fee");
        p=ReloadBall(p);new TitleStorage().Login(p);AdventureNear(p,91017);ManagerNpc(p,6);ManagerNpc(p,40);
        Check(!Fenced(p) && HasAchievement(p,10122) && InstallmentCount(p)==115 && p.Inventory.Count==2,"completed contract survives reconnect and cannot debit the leftovers");
        ManagerNpc(p,7);AdventureNear(p,30156);HouseNpc(p,2);
        Check(!Fenced(p) && p.Entity.MapID==home && p.Map.BaseID==3024 && p.Map.ID!=3024,"completed installment house remains private and distinct from showroom");
        p.Entity.Teleport(1002,345,529);
        foreach(string why in new[]{"locked","foreign","trade","expired","unsupported","stale-binding"}) {
            var f=InstallmentOwner(town);var material=Pack(f,why=="unsupported"?700073u:1088000u,1);
            if(why=="locked")material.Lock=1;if(why=="expired")material.Days=1;
            ManagerNpc(f,why=="unsupported"?(byte)5:(byte)1);
            if(why=="foreign")Query("UPDATE items SET entityid=0 WHERE uid=@uid","uid",(long)material.UID);
            if(why=="trade")f.Trade.InTrade=true;if(why=="stale-binding")material.Bound=true;
            ManagerNpc(f,40);
            Check(InstallmentCount(f)==0 && f.Inventory.Count==1 && Scalar("SELECT houselevel FROM house WHERE id=@uid",f.Entity.UID)==4,
                "ineligible installment delivery consumes nothing: "+why);
        }
        foreach(string participant in new[]{"delete","stack","receipt","progress","new-contract"}) {
            var f=InstallmentOwner(town);bool stack=participant=="stack";var material=Pack(f,stack?723087u:1088000u,stack?(ushort)25:(ushort)1);
            ManagerNpc(f,stack?(byte)2:(byte)1);
            string table=participant=="receipt"?"revora_house_installment_items":participant=="progress" || participant=="new-contract"?"revora_house_installments":"items";
            string verb=participant=="receipt" || participant=="new-contract"?"INSERT":participant=="delete"?"DELETE":"UPDATE";
            Query("CREATE FUNCTION suppress_installment() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_installment BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_installment()");
            try{ManagerNpc(f,40);}finally{Query("DROP TRIGGER suppress_installment ON "+table+";DROP FUNCTION suppress_installment()");}
            Check(Fenced(f) && InstallmentCount(f)==0 && ConquerItemTable.LoadItem(material.UID).StackSize==(stack?25:1),"failed installment participant rolls back debit and progress: "+participant);
            f=ReloadBall(f);AdventureNear(f,91017);Deposit(f,stack?1:0);
            Check(!Fenced(f) && InstallmentCount(f)==(stack?20:1),"delivery retries once after production reload: "+participant);
        }
        foreach(string table in new[]{"house","revora_house_builds","revora_achievement_evidence","revora_house_installments"}) {
            var f=InstallmentOwner(town);PayInstallments(f);ManagerNpc(f,6);
            string verb=table=="house" || table=="revora_house_installments"?"UPDATE":"INSERT";
            Query("CREATE FUNCTION suppress_installment_finish() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_installment_finish BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_installment_finish()");
            try{ManagerNpc(f,40);}finally{Query("DROP TRIGGER suppress_installment_finish ON "+table+";DROP FUNCTION suppress_installment_finish()");}
            Check(Fenced(f) && InstallmentCount(f)==115 && Scalar("SELECT houselevel FROM house WHERE id=@uid",f.Entity.UID)==4
                && Scalar("SELECT count(*) FROM revora_house_installments WHERE uid=@uid AND completed",f.Entity.UID)==0
                && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10122",f.Entity.UID)==0,
                "failed final participant retains full paid contract and old house: "+table);
            f=ReloadBall(f);AdventureNear(f,91017);ManagerNpc(f,6);ManagerNpc(f,40);
            Check(!Fenced(f) && HasAchievement(f,10122) && Scalar("SELECT houselevel FROM house WHERE id=@uid",f.Entity.UID)==5,"final construction retries once after production reload: "+table);
        }
        var expired=InstallmentOwner(town);InstallmentStock(expired,0,1);
        var now=new DateTime(2032,4,1,0,0,0,DateTimeKind.Utc);Installments.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>now));
        try {
            ManagerNpc(expired,1);now=now.AddMinutes(2);ManagerNpc(expired,40);
            Check(!Fenced(expired) && InstallmentCount(expired)==0 && expired.Inventory.Count==1,"installment offer expires at its exact two-minute boundary");
            ManagerNpc(expired,1);ManagerNpc(expired,255);ManagerNpc(expired,40);
            Check(!Fenced(expired) && InstallmentCount(expired)==0 && expired.Inventory.Count==1,"farewell discards the pending material offer");
        }finally{Installments.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>DateTime.UtcNow));}
        var inconsistent=InstallmentOwner(town);PayInstallments(inconsistent);
        Query("DELETE FROM revora_house_installment_items WHERE uid=@uid AND kind=4","uid",(long)inconsistent.Entity.UID);
        ManagerNpc(inconsistent,6);ManagerNpc(inconsistent,40);
        Check(Fenced(inconsistent) && Scalar("SELECT houselevel FROM house WHERE id=@uid",inconsistent.Entity.UID)==4
            && Scalar("SELECT count(*) FROM revora_house_installments WHERE uid=@uid AND completed",inconsistent.Entity.UID)==0,
            "counter totals without matching durable material history cannot award the house");
    }
}
