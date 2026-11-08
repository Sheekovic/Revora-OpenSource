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
    static uint NestedGear(uint id){
        uint[] gears=new uint[]{131069u,111069u,130069u,118069u,135069u,112069u,134069u,114069u,133049u,113049u,120129u,150139u,121129u,152149u,160139u,410139u,420139u,480139u,900049u,601139u,421139u,500129u};
        return gears[id-721224];
    }
    static string NestedCase(string handler,uint id){
        string key="                case "+id.ToString()+":";
        int at=handler.IndexOf(key);
        int next=handler.IndexOf("                case ",at+key.Length);
        int end=handler.IndexOf("                #endregion",at);
        int stop=next>=0 && (end<0 || next<end)?next:end;
        return at>=0 && stop>at?handler.Substring(at,stop-at):"";
    }
    static long PublicScalar(string sql){
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand(sql,c)){
            object v=q.ExecuteScalar();return v==null||v is DBNull?0:Convert.ToInt64(v);
        }}
    }
    static void OpenNested(GameState p,ConquerItem item){ItemUsage.Process(PrayerPacket(item.UID),p);}
    static void VerifyNestedGearPacks(string runtime,Map town){
        string handler=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemHandler.cs")));
        string usage=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemUsage.cs")));
        string thread=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Thread.cs")));
        string project=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Revora.csproj")));
        string audit=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../docs/ITEM_USE_COMPOUND_AUDIT.md")));
        string catalog=File.ReadAllText("database\\itemtype-new.txt");
        string shop=File.ReadAllText("database\\shops\\emoneyshop.ini");
        string v2=File.ReadAllText("database\\shops\\emoneyshopV2.ini");
        string shopTxt=File.ReadAllText("database\\shops\\Shop.txt");
        string exchange=File.ReadAllText("database\\shops\\exchange_shop_goods.txt");
        string heroes=File.ReadAllText("database\\Slayer\\StageGoal.txt");
        string drops=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Database/DropItemTable.cs")));
        string npcs=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Npcs.cs")));
        byte[] shopDat=File.ReadAllBytes("database\\shops\\Shop.dat");
        string[] names=new string[]{
            "PromotionPack","ToughDrillPack","SuperWarrior`sArmorPack","SuperWarrior`sHelmetPack","SuperTrojan`sArmorPack","SuperTrojan`sCoronetPack",
            "SuperNinja`sVestPack","SuperNinja`sVeilPack","SuperTaoist`sRobePack","SuperTaoist`sCapPack","SuperArcher`sCoatPack","SuperArcher`sHatPack",
            "SuperNecklacePack","SuperRingPack","SuperTaoist`sBagPack","SuperTaoist`sBraceletPack","SuperBootsPack","SuperBladePack","SuperSwordPack",
            "SuperClubPack","SuperShieldPack","SuperKatanaPack","SuperBackswordPack","SuperBowPack"};
        string[] shopSlots=new string[]{
            "Item1=721224","Item2=721225","Item3=721226","Item4=721227","Item5=721228","Item6=721229","Item7=721230","Item8=721231",
            "Item9=721232","Item10=721233","Item11=721234","Item12=721235","Item13=721236","Item14=721237","Item15=721238","Item16=721239",
            "Item17=721240","Item18=721241","Item19=721242","Item20=721243","Item21=721244","Item22=721245","Item23=721222","Item24=721223"};
        Check(typeof(Entity).Assembly.GetType("Revora.Game.Features.NestedGearPacks")==null
            && !File.Exists(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/NestedGearPacks.cs"))),
            "NestedGearPacks runtime ownership is not compiled");
        Check(handler.IndexOf("NestedGearPacks")<0 && usage.IndexOf("NestedGearPacks")<0 && thread.IndexOf("NestedGearPacks")<0
            && project.IndexOf("NestedGearPacks.cs")<0 && handler.IndexOf("Nested gear pack must use the owned service.")<0,
            "startup, project and dispatch hooks do not own nested gear packs");
        Check(handler.IndexOf("GetPositionFromID")>=0 && handler.IndexOf("Inventory = 0")>=0,
            "ordinary 1009/action-4 still dispatches non-equipment IDs through UseItem");
        Check(handler.IndexOf("if (client.Map.Npcs.TryGetValue(itemUsage.UID, out npc) || itemUsage.UID == 2888)")>=0,
            "generic shop-buy requires a map NPC UID except global 2888");
        Check(handler.IndexOf("if (!Database.EShopFile.Shops.TryGetValue(itemUsage.UID, out shop))")>=0,
            "generic shop-buy looks up the exact request UID in EShopFile.Shops");
        int switchStart=handler.IndexOf("switch (item.ID)"),switchEnd=handler.IndexOf("public static byte GetNextRefineryItem",switchStart);
        var sourceIds=System.Text.RegularExpressions.Regex.Matches(handler.Substring(switchStart,switchEnd-switchStart),@"(?m)^ {16}case (\d+):").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        var auditIds=System.Text.RegularExpressions.Regex.Matches(audit,@"(?m)^\| (\d+) \|").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        Check(sourceIds.Length==518 && sourceIds.OrderBy(id=>id).SequenceEqual(auditIds.OrderBy(id=>id)),
            "compound audit still enumerates the exact 518 numeric top-level UseItem case IDs");
        Check(audit.IndexOf("| live-split-write | 259 |")>=0 && audit.IndexOf("| owned-fail-closed | 91 |")>=0,
            "census is restored to 259 live-split-write / 91 owned-fail-closed");
        int shop8422=shop.IndexOf("ID=8422");
        int nextShop=shop.IndexOf("\nID=",shop8422+1);
        string block=shop.Substring(shop8422, (nextShop>shop8422?nextShop:shop.Length)-shop8422);
        Check(shop8422>=0 && block.IndexOf("MoneyType=1")>=0 && block.IndexOf("Amount=25")>=0 && block.IndexOf("Item0=184325")>=0,
            "installed EShop 8422 is MoneyType CP with Amount=25 including Item0=184325");
        Check(v2.IndexOf("8422")<0 && v2.IndexOf("721222")<0 && v2.IndexOf("721245")<0,
            "emoneyshopV2.ini contains neither 8422 nor 721222-721245");
        Check(PublicScalar("SELECT count(*) FROM public.npcs WHERE id=8422")==0,
            "installed public.npcs has no NPC 8422");
        Check(PublicScalar("SELECT count(*) FROM public.sobnpcs WHERE id=8422")==0,
            "installed public.sobnpcs has no SOBNPC 8422");
        EShopFile.Load();
        Check(EShopFile.Shops.ContainsKey(8422u) && EShopFile.Shops[8422].MoneyType==ShopFile.MoneyType.ConquerPoints,
            "loaded EShop 8422 is ConquerPoints stock");
        for(uint id=721222;id<=721245;id++){
            Check(audit.IndexOf("| "+id.ToString()+" | live-split-write | source+items |")>=0,"audit keeps nested pack "+id+" live-split-write");
            Check(ConquerItemInformation.BaseInformations.ContainsKey(id),"installed catalog contains nested pack "+id);
            int at=catalog.IndexOf(id.ToString()+"@@");int nl=at>=0?catalog.IndexOf("\n",at):-1;
            Check(at>=0 && nl>at && catalog.Substring(at,nl-at).IndexOf(names[id-721222])>=0,
                "catalog identity for "+id+" is "+names[id-721222]);
            Check(block.IndexOf(id.ToString())>=0 && EShopFile.Shops[8422].Items.Contains(id),"EShop 8422 stocks nested pack "+id);
            Check(v2.IndexOf(id.ToString())<0 && shopTxt.IndexOf(id.ToString())<0 && exchange.IndexOf(id.ToString())<0
                && heroes.IndexOf(id.ToString())<0 && drops.IndexOf(id.ToString())<0 && npcs.IndexOf(id.ToString())<0
                && System.Text.Encoding.ASCII.GetString(shopDat).IndexOf(id.ToString())<0,
                "nested pack "+id+" has no V2, Shop.txt, exchange, StageGoal, NPC or DropItemTable producer");
            string body=NestedCase(handler,id);
            Check(body.IndexOf("client.Entity.Level >= 10")>=0 && body.IndexOf("You must be atleast level 10 to open the Pack")>=0,
                "leftover "+id+" keeps the level-10 gate and exact refuse text");
            if(id<=721223){
                Check(body.IndexOf("client.Inventory.Count < 38")>=0
                    && body.IndexOf("You need to make atleast 2 free spots in your inventory.")>=0,
                    "leftover "+id+" keeps Count<38 and the two-spot refuse text");
            }else{
                Check(body.IndexOf("client.Inventory.Count < 32")>=0
                    && body.IndexOf("You need to make atleast 8 free spots in your inventory.")>=0
                    && body.IndexOf("uint itemid = "+NestedGear(id).ToString()+";")>=0
                    && body.IndexOf("items.Color = Game.Enums.Color.White;")>=0
                    && body.IndexOf("items.Plus = 5;")>=0
                    && body.IndexOf("items.SocketOne = Game.Enums.Gem.EmptySocket;")>=0
                    && body.IndexOf("client.Inventory.Add(1200000, 0, 1);")>=0
                    && body.IndexOf("client.Inventory.Add(723700, 0, 1);")>=0
                    && body.IndexOf("client.Inventory.Add(723017, 0, 5);")>=0,
                    "leftover "+id+" keeps Count<32, praying stone, EXP Ball, five potions and plus-5 white empty-socket gear "+NestedGear(id));
                Check(ConquerItemInformation.BaseInformations.ContainsKey(NestedGear(id)),"installed catalog contains gear output "+NestedGear(id));
            }
        }
        for(int n=0;n<shopSlots.Length;n++) Check(block.IndexOf(shopSlots[n])>=0,"EShop 8422 exact stock slot "+shopSlots[n]);
        string stone=NestedCase(handler,721222),drill=NestedCase(handler,721223);
        Check(stone.IndexOf("client.Inventory.Add(730008, 8, 1);")>=0 && stone.IndexOf("client.Inventory.Add(730006, 6, 1);")>=0
            && stone.IndexOf("Message.System")>=0 && stone.IndexOf("Color.Red")<0,
            "leftover 721222 pays plus-8 730008 and plus-6 730006 through Message.System");
        Check(drill.IndexOf("client.Inventory.Add(1200005, 0, 1);")>=0 && drill.IndexOf("client.Inventory.Add(730003, 3, 1);")>=0
            && drill.IndexOf("Color.Red")>=0 && drill.IndexOf("Message.TopLeft")>=0,
            "leftover 721223 pays ToughDrill and plus-3 stone through Color.Red TopLeft");
        var unreachable=WelcomePlayer(town,10,0,15);unreachable.Entity.ConquerPoints=500000;
        ItemHandler.HandleBuyFromNPC(new ItemUsage(true){UID=8422,dwParam=721222,dwExtraInfo=1},unreachable);
        ItemHandler.HandleBuyFromNPC(new ItemUsage(true){UID=8422,dwParam=721224,dwExtraInfo=1},unreachable);
        ItemHandler.HandleBuyFromNPC(new ItemUsage(true){UID=8422,dwParam=721245,dwExtraInfo=1},unreachable);
        Check(CountId(unreachable,721222)==0 && CountId(unreachable,721224)==0 && CountId(unreachable,721245)==0
            && unreachable.Entity.ConquerPoints==500000 && !town.Npcs.ContainsKey(8422u),
            "ordinary shop-buy UID 8422 cannot issue packs without map NPC 8422");
        for(uint id=721222;id<=721245;id++){
            var p=WelcomePlayer(town,10,0,15);var pack=Pack(p,id,1);OpenNested(p,pack);
            Check(!Fenced(p) && !p.Inventory.ContainsUID(pack.UID),"consumer leftover action-4 still split-writes nested pack "+id);
            if(id==721222)Check(CountPlus(p,730008,8)==1 && CountPlus(p,730006,6)==1,"consumer leftover 721222 still pays plus-8 730008 and plus-6 730006");
            else if(id==721223)Check(CountId(p,1200005)==1 && CountPlus(p,730003,3)==1,"consumer leftover 721223 still pays ToughDrill and plus-3 stone");
            else {
                var gear=p.Inventory.Objects.Single(i=>i.ID==NestedGear(id));
                int potions=p.Inventory.Objects.Where(i=>i.ID==723017).Sum(i=>i.StackSize==0?1:i.StackSize);
                Check(CountId(p,1200000)==1 && CountId(p,723700)==1 && potions==5
                    && !gear.Bound && gear.Plus==5 && gear.Color==Enums.Color.White && gear.SocketOne==Enums.Gem.EmptySocket
                    && gear.Durability==ConquerItemInformation.BaseInformations[NestedGear(id)].Durability,
                    "consumer leftover "+id+" still pays stone, EXP Ball, five potions and plus-5 white empty-socket gear");
            }
        }
        var low=WelcomePlayer(town,9,0,15);var lowPack=Pack(low,721224,1);OpenNested(low,lowPack);
        Check(!Fenced(low) && low.Inventory.ContainsUID(lowPack.UID) && CountId(low,131069)==0,
            "consumer leftover 721224 still refuses at level 9");
        var twoFull=WelcomePlayer(town,10,0,15);var twoSrc=Pack(twoFull,721222,1);while(twoFull.Inventory.Count<38)Pack(twoFull,1000000,1);OpenNested(twoFull,twoSrc);
        Check(!Fenced(twoFull) && twoFull.Inventory.ContainsUID(twoSrc.UID) && CountId(twoFull,730008)==0,
            "consumer leftover 721222 still refuses at leftover Count 38");
        var twoOk=WelcomePlayer(town,10,0,15);var twoOkSrc=Pack(twoOk,721222,1);while(twoOk.Inventory.Count<37)Pack(twoOk,1000000,1);OpenNested(twoOk,twoOkSrc);
        Check(!Fenced(twoOk) && CountPlus(twoOk,730008,8)==1,"consumer leftover 721222 still pays at leftover Count 37");
        var eightFull=WelcomePlayer(town,10,0,15);var eightSrc=Pack(eightFull,721224,1);while(eightFull.Inventory.Count<32)Pack(eightFull,1000000,1);OpenNested(eightFull,eightSrc);
        Check(!Fenced(eightFull) && eightFull.Inventory.ContainsUID(eightSrc.UID) && CountId(eightFull,131069)==0,
            "consumer leftover 721224 still refuses at leftover Count 32");
        var eightOk=WelcomePlayer(town,10,0,15);var eightOkSrc=Pack(eightOk,721224,1);while(eightOk.Inventory.Count<31)Pack(eightOk,1000000,1);OpenNested(eightOk,eightOkSrc);
        Check(!Fenced(eightOk) && CountId(eightOk,131069)==1,"consumer leftover 721224 still pays at leftover Count 31");
        var drink=WelcomePlayer(town,10,0,15);var drinkPack=Pack(drink,721224,1);OpenNested(drink,drinkPack);
        var ball=drink.Inventory.Objects.First(i=>i.ID==723700);Call(ExpItems,null,"Initialize");ulong xp=drink.Entity.Experience;ItemHandler.UseItem(ball,drink);
        Check(!Fenced(drink) && (drink.Entity.Experience>xp || drink.ExpBalls>0 || !drink.Inventory.ContainsUID(ball.UID) || ball.StackSize==0),
            "consumer leftover EXP Ball remains usable through ExperienceConsumables");
        var direct=WelcomePlayer(town,10,0,15);var directPack=Pack(direct,721245,1);ItemHandler.UseItem(directPack,direct);
        Check(!Fenced(direct) && CountId(direct,500129)==1,"consumer leftover direct UseItem still opens 721245");
        var leftover=WelcomePlayer(town,10,0,15);var surprise=Pack(leftover,722178,1);uint surpriseCp=leftover.Entity.ConquerPoints;
        ItemUsage.Process(PrayerPacket(surprise.UID),leftover);
        Check(!leftover.Inventory.ContainsUID(surprise.UID) && leftover.Entity.ConquerPoints==surpriseCp+2000u,
            "Surprise box 722178 stays outside nested gear classification and keeps leftover +2000 CP");
    }
}
