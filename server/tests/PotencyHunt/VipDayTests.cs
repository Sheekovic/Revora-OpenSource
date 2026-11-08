using System;
using System.IO;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static void DrinkVip(GameState p,ConquerItem item){ItemUsage.Process(PrayerPacket(item.UID),p);}
    static void VerifyVipDay(string runtime,Map town){
        string handler=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemHandler.cs")));
        string server=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Server.cs")));
        string thread=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Thread.cs")));
        string constants=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../ServerBase/Constants.cs")));
        string audit=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../docs/ITEM_USE_COMPOUND_AUDIT.md")));
        string catalog=File.ReadAllText("database\\itemtype-new.txt");
        string slayer=File.ReadAllText("database\\shops\\SlayerPointshop.ini");
        string race=File.ReadAllText("database\\shops\\RacePointShop.ini");
        string v2=File.ReadAllText("database\\shops\\emoneyshopV2.ini");
        string eshop=File.ReadAllText("database\\shops\\emoneyshop.ini");
        string shopTxt=File.ReadAllText("database\\shops\\Shop.txt");
        string shopDat=File.ReadAllText("database\\shops\\Shop.dat");
        string exchange=File.ReadAllText("database\\shops\\exchange_shop_goods.txt");
        string heroes=File.ReadAllText("database\\Slayer\\StageGoal.txt");
        string drops=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Database/DropItemTable.cs")));
        string monsters=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Database/MonsterTable.cs")));
        string npcs=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Npcs.cs")));
        uint[] ids=new uint[]{3004121u,3004122u,3004123u,3004124u,3000794u};
        string[] names=new string[]{"VIP4Token","VIP5Token","VIP6Token","VIP7Token","VIP7FreedomBox"};
        Check(handler.IndexOf("GetPositionFromID")>=0 && handler.IndexOf("Inventory = 0")>=0,
            "ordinary 1009/action-4 still dispatches non-equipment IDs through UseItem");
        int switchStart=handler.IndexOf("switch (item.ID)"),switchEnd=handler.IndexOf("public static byte GetNextRefineryItem",switchStart);
        var sourceIds=System.Text.RegularExpressions.Regex.Matches(handler.Substring(switchStart,switchEnd-switchStart),@"(?m)^ {16}case (\d+):").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        var auditIds=System.Text.RegularExpressions.Regex.Matches(audit,@"(?m)^\| (\d+) \|").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        Check(sourceIds.Length==518 && sourceIds.OrderBy(id=>id).SequenceEqual(auditIds.OrderBy(id=>id)),
            "compound audit still enumerates the exact 518 numeric top-level UseItem case IDs");
        Check(audit.IndexOf("| live-split-write | 145 |")>=0 && audit.IndexOf("| owned-fail-closed | 275 |")>=0,
            "census is 145 live-split-write / 275 owned-fail-closed");
        for(int n=0;n<ids.Length;n++){
            Check(audit.IndexOf("| "+ids[n].ToString()+" | live-split-write | source+timer |")>=0,"audit keeps VIP-day "+ids[n]+" live-split-write");
            Check(ConquerItemInformation.BaseInformations.ContainsKey(ids[n]),"installed catalog contains VIP-day "+ids[n]);
            Check(catalog.IndexOf(ids[n].ToString()+"@@"+names[n])>=0,"catalog identity for "+ids[n]+" is "+names[n]);
            Check(eshop.IndexOf(ids[n].ToString())<0 && v2.IndexOf(ids[n].ToString())<0 && shopTxt.IndexOf(ids[n].ToString())<0
                && shopDat.IndexOf(ids[n].ToString())<0 && exchange.IndexOf(ids[n].ToString())<0 && heroes.IndexOf(ids[n].ToString())<0
                && drops.IndexOf(ids[n].ToString())<0 && monsters.IndexOf(ids[n].ToString())<0 && npcs.IndexOf("Add("+ids[n].ToString())<0,
                "VIP-day "+ids[n]+" has no emoney, Shop.txt, Shop.dat, exchange, StageGoal, NPC Add, DropItemTable or MonsterTable producer");
        }
        Check(slayer.IndexOf("3004121 20000")>=0 && slayer.IndexOf("3004122 60000")>=0 && slayer.IndexOf("3004123 150000")>=0
            && slayer.IndexOf("3004124")<0 && slayer.IndexOf("3000794")<0,
            "SlayerPointshop.ini stocks 3004121-3004123 and not 3004124/3000794");
        Check(race.IndexOf("3004121 20000")>=0 && constants.IndexOf("RaceShopPath = \"database\\\\shops\\\\SlayerPointshop.ini\"")>=0,
            "RacePointShop.ini copy and RaceShopPath still point at SlayerPointshop.ini");
        int shopUid=handler.IndexOf("if (itemUsage.UID == Database.REVORAPointhop.UID)");
        Check(shopUid>=0 && shopUid<handler.IndexOf("client.Map.Npcs.TryGetValue(itemUsage.UID, out npc)"),
            "dedicated UID 6001 buy runs before the generic map-NPC gate");
        Check(handler.IndexOf("if (Database.REVORAPointhop.Shop == null || Database.REVORAPointhop.Shop.Items == null)")>=0,
            "dedicated UID 6001 buy fail-closes when the shop is uninitialized");
        Check(server.IndexOf("//  Database.RevoraPointhop.Load();")>=0
            && System.Text.RegularExpressions.Regex.Replace(server,@"//.*"," ").IndexOf("Pointhop.Load")<0
            && thread.IndexOf("Pointhop.Load")<0,
            "installed startup comments out the only REVORAPointhop.Load call");
        var pointShop=typeof(Entity).Assembly.GetType("Revora.Database.REVORAPointhop");
        var shopFields=System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static;
        Check((uint)pointShop.GetField("UID",shopFields).GetValue(null)==6001u && pointShop.GetField("Shop",shopFields).GetValue(null)==null,
            "REVORAPointhop.UID is 6001 and Shop is uninitialized in this process");
        Check(PublicScalar("SELECT count(*) FROM public.npcs WHERE id=6001")+PublicScalar("SELECT count(*) FROM public.sobnpcs WHERE id=6001")==0,
            "installed database has no NPC/SOBNPC 6001 as secondary evidence");
        uint[] slayerIds=new uint[]{3004121u,3004122u,3004123u};
        for(int n=0;n<slayerIds.Length;n++){
            var buyer=BallPlayer(town);buyer.Entity.RevoraPoint=5000000;uint rvp=buyer.Entity.RevoraPoint;
            ItemHandler.HandleBuyFromNPC(new ItemUsage(true){UID=6001,dwParam=slayerIds[n],dwExtraInfo=1},buyer);
            Check(!Fenced(buyer) && buyer.Inventory.Objects.Count(i=>i.ID==slayerIds[n])==0 && buyer.Entity.RevoraPoint==rvp,
                "ordinary UID 6001 buy of "+slayerIds[n]+" issues nothing and does not debit when the dedicated shop is uninitialized");
        }
        string two=NestedCase(handler,3004121u);
        Check(two.IndexOf("client.Entity.VIPLevel == 5")>=0 && two.IndexOf("client.Entity.VIPDays = 2;")>=0
            && two.IndexOf("client.Inventory.Remove(item, Game.Enums.ItemUse.Remove)")>=0,
            "leftover 3004121 still requires VIP 5 and sets 2 VIPDays");
        var deny=BallPlayer(town);deny.Entity.VIPLevel=4;deny.Entity.VIPDays=0;var denyItem=Pack(deny,3004121,1);
        DrinkVip(deny,denyItem);
        Check(!Fenced(deny) && deny.Inventory.ContainsUID(denyItem.UID) && deny.Entity.VIPLevel==4 && deny.Entity.VIPDays==0,
            "leftover 3004121 refuses when the player is not VIP 5");
        var twoDay=BallPlayer(town);twoDay.Entity.VIPLevel=5;twoDay.Entity.VIPDays=0;twoDay.Entity.VIPLevelDays=0;var twoItem=Pack(twoDay,3004121,1);
        DrinkVip(twoDay,twoItem);
        Check(!Fenced(twoDay) && !twoDay.Inventory.ContainsUID(twoItem.UID) && twoDay.Entity.VIPLevel==6 && twoDay.Entity.VIPLevelDays==6 && twoDay.Entity.VIPDays==2,
            "leftover 3004121 on VIP 5 consumes one unit and sets 2 days of VIP 6");
        var five=BallPlayer(town);five.Entity.VIPLevel=5;five.Entity.VIPDays=0;var fiveItem=Pack(five,3004122,1);
        DrinkVip(five,fiveItem);
        Check(!Fenced(five) && !five.Inventory.ContainsUID(fiveItem.UID) && five.Entity.VIPLevel==6 && five.Entity.VIPDays==5,
            "leftover 3004122 on VIP 5 consumes one unit and sets 5 days of VIP 6");
        var stacked=BallPlayer(town);stacked.Entity.VIPLevel=5;stacked.Entity.VIPDays=3;var stackedItem=Pack(stacked,3004122,1);
        DrinkVip(stacked,stackedItem);
        Check(!Fenced(stacked) && stacked.Inventory.ContainsUID(stackedItem.UID) && stacked.Entity.VIPDays==3 && stacked.Entity.VIPLevel==5,
            "leftover 3004122 refuses to stack when VIPDays is already above 2");
        var fifteen=BallPlayer(town);fifteen.Entity.VIPLevel=5;fifteen.Entity.VIPDays=0;var fifteenItem=Pack(fifteen,3004123,1);
        DrinkVip(fifteen,fifteenItem);
        Check(!Fenced(fifteen) && !fifteen.Inventory.ContainsUID(fifteenItem.UID) && fifteen.Entity.VIPLevel==6 && fifteen.Entity.VIPDays==15,
            "leftover 3004123 on VIP 5 consumes one unit and sets 15 days of VIP 6");
        var thirty=BallPlayer(town);thirty.Entity.VIPLevel=4;thirty.Entity.VIPDays=0;var thirtyItem=Pack(thirty,3004124,1);
        DrinkVip(thirty,thirtyItem);
        Check(!Fenced(thirty) && !thirty.Inventory.ContainsUID(thirtyItem.UID) && thirty.Entity.VIPLevel==7 && thirty.Entity.VIPLevelDays==7 && thirty.Entity.VIPDays==30,
            "leftover 3004124 consumes one unit with no VIP gate and adds 30 days of VIP 7");
        var xmas=BallPlayer(town);xmas.Entity.VIPLevel=4;xmas.Entity.VIPDays=0;var xmasItem=Pack(xmas,3000794,1);
        DrinkVip(xmas,xmasItem);
        Check(!Fenced(xmas) && !xmas.Inventory.ContainsUID(xmasItem.UID) && xmas.Entity.VIPLevel==7 && xmas.Entity.VIPDays==1
            && NestedCase(handler,3000794u).IndexOf("Christmas gift")>=0,
            "leftover 3000794 consumes one unit and adds 1 day of VIP 7 with Christmas copy");
        Check(catalog.IndexOf("3000794@@VIP7FreedomBox")>=0 && catalog.IndexOf("Pay~7,999~CPs")>=0,
            "catalog copy for 3000794 is VIP7FreedomBox / 7,999 CPs and is not adopted by leftover use");
    }
}
