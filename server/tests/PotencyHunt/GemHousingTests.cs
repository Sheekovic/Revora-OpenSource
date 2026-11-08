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
    static GameState GemHousePlayer(Map town,ulong money=1000000)
    {
        var p=HousePlayer(town);p.Entity.Money=money;p.Entity.ConquerPoints=0;EntityTable.UpdateMoney(p);
        Query("UPDATE entities SET conquerpoints=0 WHERE uid=@uid","uid",(long)p.Entity.UID);return p;
    }
    static void VerifyGemHouses(string runtime,Map town)
    {
        Call(Gems,null,"Initialize");var now=new DateTime(2032,3,1,0,0,0,DateTimeKind.Utc);
        Gems.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>now));
        Call(MiningService,null,"Initialize");Call(BookShop,null,"Initialize");ShopFile.Load();
        World(runtime,1025);var phoenix=World(runtime,1011);MiningNpc(phoenix,125);MiningNpc(town,1);
        foreach(ushort id in new ushort[]{1002,1011,1025})DMaps.MapPaths[id]="maps\\"+id+".map";
        MiningService.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>now));
        MiningService.GetField("Roll",Hidden).SetValue(null,new Func<int,int>(max=>0));
        int chance=Config.MiningNormalGemBasisPoints;Config.MiningNormalGemBasisPoints=100;
        try {
            var p=GemHousePlayer(town,90061200);
            AdventureNear(p,1);ItemHandler.HandleBuyFromNPC(BuyBook(562000,1,1),p);
            var pick=p.Inventory.Objects.Single(i=>i.ID==562000);
            ItemHandler.EquipItem(new ItemUsage(true){UID=pick.UID,dwParam=4,Action=ItemUsage.Mode.EquipItem},p);
            AdventureNear(p,3);ItemHandler.HandleBuyFromNPC(BuyBook(1060020,1,3),p);
            AdventureGo(p,1011);AdventureNear(p,125);AdventureNpc(p,125,1);
            Check(!Fenced(p) && p.Entity.MapID==1025 && p.Account.State==AccountTable.AccountState.Player
                && p.Equipment.TryGetItem(4).UID==pick.UID,"ordinary gem-house miner buys and equips a pickaxe and reaches the installed mine");
            StartMining(p);
            for(int i=0;i<6;i++) {
                for(int n=0;n<15;n++){now=now.AddSeconds(2);MineTick(p);}
                Check(!Fenced(p) && MineCount(p)==(i+1)*15 && p.Inventory.Objects.Count(g=>g.ID==700001)==15,
                    "production mining supplies the next fifteen normal house gems: "+(i+1));
                ItemUsage.Process(GemRequest(700001,false,(uint)(i+1)),p);
            }
            UseHouseReturn(p);AdventureNear(p,30156);
            Check(!Fenced(p) && p.Inventory.Count==6 && p.Inventory.Objects.All(i=>i.ID==700002) && p.Entity.Money==90000000,
                "ninety mined gems become six paid refined gems after exact tool, travel and composition costs");
            p=ReloadBall(p);AdventureNear(p,30156);
            int[] count={6,7,8,2,40};ulong[] cost={1000000,2000000,3000000,4000000,80000000};
            int[] award={10102,10103,10104,10105,10122};ushort house=0;
            uint[] family={700000,700010,700020,700030,700040,700050,700060,700070,700100,700120};
            for(int level=1;level<=5;level++) {
                if(level>1)for(int i=0;i<count[level-1];i++)GemStock(p,family[i%family.Length]+(level<=3?2u:3u),1,i%2==0);
                ulong before=p.Entity.Money;var consumed=p.Inventory.Objects.Select(i=>i.UID).ToArray();
                HouseNpc(p,10);Parallel.For(0,10,i=>HouseNpc(p,40));
                ushort saved=(ushort)Scalar("SELECT map_id FROM revora_house_maps WHERE uid=@uid",p.Entity.UID);
                if(level==1)house=saved;
                Check(!Fenced(p) && p.Entity.Money==before-cost[level-1] && p.Entity.ConquerPoints==0 && p.Inventory.Count==0
                    && Scalar("SELECT houselevel FROM house WHERE id=@uid",p.Entity.UID)==level && saved==house && HasAchievement(p,award[level-1]),
                    "gem-funded house level commits exact materials, silver and achievement once: "+level);
                Check(Scalar("SELECT count(*) FROM revora_house_build_materials WHERE uid=@uid AND level=(SELECT max(level) FROM revora_house_builds WHERE uid=@uid)",p.Entity.UID)==count[level-1]
                    && consumed.All(uid=>Scalar("SELECT count(*) FROM items WHERE uid=@uid",uid)==0),"complete material receipt and durable consumption cover house level "+level);
                HouseNpc(p,2);p=ReloadBall(p);new TitleStorage().Login(p);
                Check(!Fenced(p) && p.Entity.MapID==house && HasAchievement(p,award[level-1]) && p.Entity.Money==before-cost[level-1]
                    && p.Inventory.Count==0,"gem-funded house entry and production reconnect preserve level "+level);
                p.Entity.Teleport(1002,345,529);AdventureNear(p,30156);
            }
            HouseNpc(p,10);HouseNpc(p,40);
            Check(!Fenced(p) && p.Entity.Money==0 && Scalar("SELECT count(*) FROM revora_house_builds WHERE uid=@uid",p.Entity.UID)==5,
                "exact final gem-route balance is zero and a sixth house cannot be purchased");
            var selection=GemHousePlayer(town);GemStock(selection,700002,6);GemStock(selection,700012,6,true);
            HouseNpc(selection,10);HouseNpc(selection,40);
            Check(!Fenced(selection) && selection.Inventory.Count==6 && selection.Inventory.Objects.All(i=>i.ID==700002 && !i.Bound),
                "bound-first house selection preserves the six unbound gems when enough bound gems exist");
            foreach(string why in new[]{"short","normal","locked","stored","equipped","temporary","suspicious","stacked","poor","trade","unoffered","changed-binding","foreign"}) {
                var f=GemHousePlayer(town,why=="poor"?999999UL:1000000UL);GemStock(f,700002,why=="short"?5:6);
                var item=f.Inventory.Objects.First();
                if(why=="normal")item.ID=700001;
                if(why=="locked")item.Lock=1;
                if(why=="stored")item.Warehouse=1;
                if(why=="equipped")item.Position=4;
                if(why=="temporary")item.Days=1;
                if(why=="suspicious")item.Suspicious=true;
                if(why=="stacked")item.StackSize=2;
                if(why!="unoffered")HouseNpc(f,10);
                if(why=="trade")f.Trade.InTrade=true;
                if(why=="changed-binding")item.Bound=true;
                if(why=="foreign")Query("UPDATE items SET entityid=0 WHERE uid=@uid","uid",(long)item.UID);
                ulong before=f.Entity.Money;int bag=f.Inventory.Count;HouseNpc(f,40);
                Check(f.Entity.Money==before && f.Inventory.Count==bag && Scalar("SELECT count(*) FROM house WHERE id=@uid",f.Entity.UID)==0
                    && Scalar("SELECT count(*) FROM revora_house_build_materials WHERE uid=@uid",f.Entity.UID)==0
                    && Scalar("SELECT money FROM entities WHERE uid=@uid",f.Entity.UID)==(long)before,"invalid gem-funded construction preserves payment and creates no house: "+why);
            }
            foreach(string table in new[]{"entities","items","house","revora_house_builds","revora_house_build_materials","revora_achievement_evidence"}) {
                var f=GemHousePlayer(town);GemStock(f,700002,6);HouseNpc(f,10);
                string verb=table=="entities"?"UPDATE":table=="items"?"DELETE":"INSERT";
                Query("CREATE FUNCTION suppress_gem_house() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_gem_house BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_gem_house()");
                try{HouseNpc(f,40);}finally{Query("DROP TRIGGER suppress_gem_house ON "+table+";DROP FUNCTION suppress_gem_house()");}
                Check(Fenced(f) && f.Entity.Money==1000000 && f.Inventory.Count==6 && Scalar("SELECT money FROM entities WHERE uid=@uid",f.Entity.UID)==1000000
                    && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id=700002",f.Entity.UID)==6
                    && Scalar("SELECT count(*) FROM house WHERE id=@uid",f.Entity.UID)==0
                    && Scalar("SELECT count(*) FROM revora_house_builds WHERE uid=@uid",f.Entity.UID)==0
                    && Scalar("SELECT count(*) FROM revora_house_build_materials WHERE uid=@uid",f.Entity.UID)==0
                    && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10102",f.Entity.UID)==0,
                    "suppressed gem-house participant rolls back house, payment, all gems and achievement: "+table);
                f=ReloadBall(f);AdventureNear(f,30156);HouseNpc(f,10);HouseNpc(f,40);
                Check(!Fenced(f) && f.Entity.Money==0 && f.Inventory.Count==0 && HasAchievement(f,10102),"gem-funded construction retries after production reload: "+table);
            }
        }finally{
            Gems.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>DateTime.UtcNow));
            MiningService.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>DateTime.UtcNow));
            MiningService.GetField("Roll",Hidden).SetValue(null,new Func<int,int>(max=>Kernel.Random.Next(max)));
            Config.MiningNormalGemBasisPoints=chance;
        }
    }
}
