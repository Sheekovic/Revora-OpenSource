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
    static void OpenMedicine(GameState p,ConquerItem item){ItemUsage.Process(PrayerPacket(item.UID),p);}
    static int MedicineCount(GameState p,uint id){return p.Inventory.Objects.Where(i=>i.ID==id).Sum(i=>i.StackSize==0?1:i.StackSize);}
    static void VerifyMedicineBundles(string runtime,Map town){
        string handler=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemHandler.cs")));
        string audit=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../docs/ITEM_USE_COMPOUND_AUDIT.md")));
        string constants=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../ServerBase/Constants.cs")));
        string server=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Server.cs")));
        string shopFile=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Database/Shops/ShopFile.cs")));
        string catalog=File.ReadAllText("database\\itemtype-new.txt");
        string shopDat=File.ReadAllText("database\\shops\\Shop.dat");
        string v2=File.ReadAllText("database\\shops\\emoneyshopV2.ini");
        string eshop=File.ReadAllText("database\\shops\\emoneyshop.ini");
        string shopTxt=File.ReadAllText("database\\shops\\Shop.txt");
        string exchange=File.ReadAllText("database\\shops\\exchange_shop_goods.txt");
        string heroes=File.ReadAllText("database\\Slayer\\StageGoal.txt");
        string drops=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Database/DropItemTable.cs")));
        string npcs=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Npcs.cs")));
        uint[] ids=new uint[]{720010u,720011u,720012u,720013u,720014u,720015u,720016u,720017u};
        uint[] outs=new uint[]{1000030u,1002000u,1002010u,1002020u,1001030u,1001040u,1002030u,1002040u};
        string[] names=new string[]{"Amrita","Panacea","Ginseng","Vanilla","RecoveryPill","SoulPill","RefreshingPill","ChantPill"};
        uint[] stocked=new uint[]{720011u,720012u,720013u};
        uint[] shops=new uint[]{8159u,8160u,8161u,8162u,8593u,8594u,8595u,8596u};
        Check(handler.IndexOf("GetPositionFromID")>=0 && handler.IndexOf("Inventory = 0")>=0,
            "ordinary 1009/action-4 still dispatches non-equipment IDs through UseItem");
        int switchStart=handler.IndexOf("switch (item.ID)"),switchEnd=handler.IndexOf("public static byte GetNextRefineryItem",switchStart);
        var sourceIds=System.Text.RegularExpressions.Regex.Matches(handler.Substring(switchStart,switchEnd-switchStart),@"(?m)^ {16}case (\d+):").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        var auditIds=System.Text.RegularExpressions.Regex.Matches(audit,@"(?m)^\| (\d+) \|").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        Check(sourceIds.Length==518 && sourceIds.OrderBy(id=>id).SequenceEqual(auditIds.OrderBy(id=>id)),
            "compound audit still enumerates the exact 518 numeric top-level UseItem case IDs");
        Check(audit.IndexOf("| live-split-write | 256 |")>=0 && audit.IndexOf("| owned-fail-closed | 94 |")>=0,
            "census stays 256 live-split-write / 94 owned-fail-closed");
        Check(constants.IndexOf("ShopsPath = \"database\\\\shops\\\\Shop.dat\"")>=0 && shopFile.IndexOf("File.ReadAllLines(Constants.ShopsPath)")>=0
            && server.IndexOf("Database.ShopFile.Load();")>=0 && System.Text.RegularExpressions.Regex.Replace(server,@"//.*"," ").IndexOf("ShopFile.Load()")>=0,
            "installed startup loads Shop.dat through ShopFile.Load");
        Check(handler.IndexOf("if (client.Map.Npcs.TryGetValue(itemUsage.UID, out npc) || itemUsage.UID == 2888)")>=0,
            "generic shop-buy requires a map NPC UID except global 2888");
        Check(constants.IndexOf("FullInventory = new Message(\"Your inventory is Full.\", Message.System)")>=0,
            "Constants.FullInventory is Your inventory is Full.");
        ShopFile.Load();
        for(int n=0;n<ids.Length;n++){
            Check(audit.IndexOf("| "+ids[n].ToString()+" | live-split-write | source+items |")>=0,"audit keeps medicine bundle "+ids[n]+" live-split-write");
            Check(ConquerItemInformation.BaseInformations.ContainsKey(ids[n]),"installed catalog contains medicine bundle "+ids[n]);
            Check(ConquerItemInformation.BaseInformations.ContainsKey(outs[n]),"installed catalog contains medicine output "+outs[n]);
            Check(catalog.IndexOf(ids[n].ToString()+"@@"+names[n])>=0 && catalog.IndexOf(outs[n].ToString()+"@@"+names[n])>=0,
                "catalog identities for bundle and output "+ids[n]+"/"+outs[n]+" are "+names[n]);
            Check(ConquerItemInformation.BaseInformations[outs[n]].StackSize>=3,"medicine output "+outs[n]+" can hold three stacked units");
            string body=NestedCase(handler,ids[n]);
            Check(body.IndexOf("client.Inventory.Count <= 38")>=0 && body.IndexOf("Constants.FullInventory")>=0
                && body.IndexOf("client.Inventory.Add("+outs[n].ToString()+", 0, 3);")>=0
                && body.IndexOf("client.Inventory.Remove(item, Game.Enums.ItemUse.Remove)")>=0,
                "leftover "+ids[n]+" keeps Count<=38, FullInventory and three plus-0 "+outs[n]);
            Check(eshop.IndexOf(ids[n].ToString())<0 && v2.IndexOf(ids[n].ToString())<0 && shopTxt.IndexOf(ids[n].ToString())<0
                && exchange.IndexOf(ids[n].ToString())<0 && heroes.IndexOf(ids[n].ToString())<0 && drops.IndexOf(ids[n].ToString())<0
                && npcs.IndexOf(ids[n].ToString())<0,
                "medicine bundle "+ids[n]+" has no emoney, Shop.txt, exchange, StageGoal, NPC or DropItemTable producer");
        }
        Check(shopDat.IndexOf("Item15=720011")>=0 && shopDat.IndexOf("Item16=720012")>=0 && shopDat.IndexOf("Item17=720013")>=0
            && shopDat.IndexOf("720010")<0 && shopDat.IndexOf("720014")<0 && shopDat.IndexOf("720015")<0 && shopDat.IndexOf("720016")<0 && shopDat.IndexOf("720017")<0,
            "Shop.dat stocks only 720011-720013 and not 720010/720014-720017");
        for(int n=0;n<shops.Length;n++){
            Check(ShopFile.Shops.ContainsKey(shops[n]) && ShopFile.Shops[shops[n]].MoneyType==ShopFile.MoneyType.Gold
                && ShopFile.Shops[shops[n]].Items.Contains(720011u) && ShopFile.Shops[shops[n]].Items.Contains(720012u) && ShopFile.Shops[shops[n]].Items.Contains(720013u)
                && !ShopFile.Shops[shops[n]].Items.Contains(720010u),
                "loaded Shop.dat shop "+shops[n]+" is gold stock of 720011-720013");
            Check(PublicScalar("SELECT count(*) FROM public.npcs WHERE id="+shops[n])
                +PublicScalar("SELECT count(*) FROM public.sobnpcs WHERE id="+shops[n])
                +PublicScalar("SELECT count(*) FROM public.furniture WHERE id="+shops[n])==0,
                "installed database has no NPC/SOBNPC/furniture "+shops[n]);
        }
        for(int n=0;n<stocked.Length;n++){
            var buyer=WelcomePlayer(town,1,0,15);buyer.Entity.Money=1000000UL;
            ItemHandler.HandleBuyFromNPC(new ItemUsage(true){UID=8159,dwParam=stocked[n],dwExtraInfo=1},buyer);
            ItemHandler.HandleBuyFromNPC(new ItemUsage(true){UID=8593,dwParam=stocked[n],dwExtraInfo=1},buyer);
            Check(!Fenced(buyer) && CountId(buyer,stocked[n])==0 && buyer.Entity.Money==1000000UL,
                "ordinary shop-buy UID 8159/8593 cannot issue "+stocked[n]+" without map NPC");
        }
        for(int n=0;n<ids.Length;n++){
            var p=WelcomePlayer(town,1,0,15);var pack=Pack(p,ids[n],1);OpenMedicine(p,pack);
            Check(!Fenced(p) && !p.Inventory.ContainsUID(pack.UID) && MedicineCount(p,outs[n])==3
                && p.Inventory.Objects.All(i=>i.ID!=outs[n] || (!i.Bound && i.Plus==0)),
                "consumer leftover action-4 still split-writes three unbound plus-0 "+outs[n]+" from "+ids[n]);
        }
        var full=WelcomePlayer(town,1,0,15);var fullPack=Pack(full,720010,1);while(full.Inventory.Count<39)Pack(full,1000000,1);OpenMedicine(full,fullPack);
        Check(!Fenced(full) && full.Inventory.ContainsUID(fullPack.UID) && MedicineCount(full,1000030)==0,
            "consumer leftover 720010 still refuses at leftover Count 39");
        var ok=WelcomePlayer(town,1,0,15);var okPack=Pack(ok,720010,1);while(ok.Inventory.Count<38)Pack(ok,1000000,1);OpenMedicine(ok,okPack);
        Check(!Fenced(ok) && MedicineCount(ok,1000030)==3,"consumer leftover 720010 still pays at leftover Count 38");
        var direct=WelcomePlayer(town,1,0,15);var directPack=Pack(direct,720017,1);ItemHandler.UseItem(directPack,direct);
        Check(!Fenced(direct) && MedicineCount(direct,1002040)==3,"consumer leftover direct UseItem still opens 720017");
        var skip=WelcomePlayer(town,1,0,15);var skipPack=Pack(skip,720011,1);
        Check(!(bool)Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.SilverPouches"),null,"TryUse",skipPack,skip) && skip.Inventory.ContainsUID(skipPack.UID),
            "720011 stays outside SilverPouches");
    }
}
