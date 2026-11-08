using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Npgsql;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Interfaces;
using Revora.Network.GamePackets;
using Console = System.Console;

internal static class SubclassShopTests
{
    static readonly Type Service = typeof(Entity).Assembly.GetType("Revora.Game.Features.SubclassShop");
    static readonly BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Static;
    static int checks;
    static void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; Console.WriteLine("PASS: " + name); }
    static object Call(string name, params object[] args) { return Service.GetMethod(name, Hidden).Invoke(null, args); }
    static void Query(string sql, params object[] values)
    {
        using (var c = DataHolder.MySqlConnection) { c.Open(); using (var q = new NpgsqlCommand(sql, c)) {
            for (int i = 0; i < values.Length; i += 2) q.Parameters.AddWithValue((string)values[i], values[i+1]);
            q.ExecuteNonQuery();
        }}
    }
    static long Scalar(string sql, uint uid)
    {
        using (var c = DataHolder.MySqlConnection) { c.Open(); using (var q = new NpgsqlCommand(sql, c)) {
            q.Parameters.AddWithValue("uid", (long)uid); return Convert.ToInt64(q.ExecuteScalar());
        }}
    }
    static GameState Player(Map map, uint cps, ushort x=424)
    {
        var p=(GameState)typeof(CompanionTests).GetMethod("Player", Hidden).Invoke(null,new object[]{map,false,x,(ushort)339,(byte)165});
        p.Action=2; p.Account.State=AccountTable.AccountState.Player;
        p.ItemSyncRoot=new object(); p.Trade=new Revora.Game.ConquerStructures.Trade();
        typeof(Entity).GetField("_conquerpoints",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(p.Entity,cps);
        Query("INSERT INTO entities(uid,conquerpoints,studypoints,revorapoint) VALUES(@uid,@cps,0,0)","uid",(long)p.Entity.UID,"cps",(long)cps);
        return p;
    }
    static ItemUsage Request(uint id, uint amount) { return new ItemUsage(true) { UID=91008, dwParam=id, dwExtraInfo=amount }; }
    static long SavedCps(GameState p) { return Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID); }
    static long SavedItems(GameState p) { return Scalar("SELECT COALESCE(SUM(stacksize),0) FROM items WHERE entityid=@uid",p.Entity.UID); }
    static bool ReloadRequired(GameState p) { return (bool)typeof(Entity).GetField("PersistenceRequiresReload",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(p.Entity); }
    static int Main()
    {
        string original=null; bool created=false;
        try
        {
            var ini=new IniFile("Configuracion.ini");
            string host=ini.ReadString("MySql","Host"), port=ini.ReadString("MySql","Port");
            if (!String.IsNullOrEmpty(port) && host.IndexOf("Port=",StringComparison.OrdinalIgnoreCase)<0) host=host.Trim().TrimEnd(';')+";Port="+port;
            DataHolder.CreateConnection(ini.ReadString("MySql","Username"),ini.ReadString("MySql","Password"),ini.ReadString("MySql","Database"),host);
            original=DataHolder.ConnectionString;
            // Separate disposable schema: never insert a test character into the live tables.
            Query("CREATE SCHEMA revora_subclass_shop_validation"); created=true;
            Query("CREATE TABLE revora_subclass_shop_validation.entities AS SELECT uid,conquerpoints,studypoints,revorapoint FROM public.entities WITH NO DATA");
            Query("ALTER TABLE revora_subclass_shop_validation.entities ADD PRIMARY KEY (uid)");
            Query("CREATE TABLE revora_subclass_shop_validation.items (LIKE public.items INCLUDING ALL)");
            var builder=new NpgsqlConnectionStringBuilder(original); builder.SearchPath="revora_subclass_shop_validation";
            DataHolder.ConnectionString=builder.ConnectionString;
            ConquerItemInformation.Load();
            uint[] ids={721259,721261,711188,723087,1088001,711679,723903,723340,723341,728883,723342};
            uint[] prices={30,10,150,5,3,150,3,1,4,10,99};
            for(int i=0;i<ids.Length;i++) Check(ConquerItemInformation.BaseInformations[ids[i]].ConquerPointsWorth==prices[i], "loaded subclass catalog price matches approved economy: "+ids[i]);
            ShopFile.Load();
            Check(ShopFile.Shops[91008].MoneyType==ShopFile.MoneyType.ConquerPoints && ShopFile.Shops[91008].Items.OrderBy(i=>i).SequenceEqual(ids.OrderBy(i=>i)), "loaded subclass shop stock and currency match");
            uint[] innerIds={3005365,3005366,3005395,3005396,3007113,3007114,3005397,3005398,3007115,3007116,3007117,3005399,3005400,3005401,3007230,3007231,3007232};
            uint[] innerPrices={100,100,100,100,200,200,200,200,250,250,250,250,250,250,300,300,300};
            for(int i=0;i<innerIds.Length;i++) Check(ConquerItemInformation.BaseInformations[innerIds[i]].ConquerPointsWorth==innerPrices[i], "loaded Inner Power catalog price matches approved economy: "+innerIds[i]);
            Check(ShopFile.Shops[91009].MoneyType==ShopFile.MoneyType.ConquerPoints && ShopFile.Shops[91009].Items.OrderBy(i=>i).SequenceEqual(innerIds.OrderBy(i=>i)), "loaded Inner Power shop stock and currency match");
            InnerPowerTable.LoadDBInformation();
            Check(InnerPowerTable.Stages.SelectMany(s=>s.NeiGongAtributes).Select(g=>g.ItemID).OrderBy(id=>id).SequenceEqual(innerIds.OrderBy(id=>id)),
                "Inner Power shop covers all 17 configured requirement books");
            ConquerItem.ItemUID = new Counter(1000000);
            typeof(CompanionTests).GetMethod("Reset",Hidden).Invoke(null,null);
            var map=(Map)typeof(CompanionTests).GetMethod("World",Hidden).Invoke(null,new object[]{(ushort)1002});
            map.Npcs=new Dictionary<uint,INpc>{{91008,new NpcSpawn{UID=91008,Type=Enums.NpcType.Shop,X=424,Y=339,MapID=1002,Mesh=46070}}};
            map.Npcs.Add(91009,new NpcSpawn{UID=91009,Type=Enums.NpcType.Shop,X=428,Y=343,MapID=1002,Mesh=46080});

            var p6=typeof(Entity).Assembly.GetType("Revora.Game.Features.P6SoulShop");
            p6.GetMethod("Create",Hidden).Invoke(null,null);
            p6.GetMethod("Create",Hidden).Invoke(null,null);
            var p6shop=ShopFile.Shops[91010];
            Check(map.Npcs[91010].X==401 && map.Npcs[91010].Y==339 && map.Npcs[91010].Type==Enums.NpcType.Shop,
                "P6 native shop spawns once at requested Twin City coordinates");
            Check(p6shop.Items.Count==27 && p6shop.Items.Distinct().Count()==27 && p6shop.MoneyType==ShopFile.MoneyType.ConquerPoints
                && p6shop.Items.All(id=>ConquerItemInformation.BaseInformations[id].PurificationLevel==6 && ConquerItemInformation.BaseInformations[id].ConquerPointsWorth==2500),
                "all 27 native P6 souls cost 2500 regular CPs");
            var soulBuyer=Player(map,68000,401);
            foreach(uint id in p6shop.Items) ItemHandler.HandleBuyFromNPC(new ItemUsage(true){UID=91010,dwParam=id,dwExtraInfo=1},soulBuyer);
            Check(soulBuyer.Inventory.Count==27 && SavedItems(soulBuyer)==27 && SavedCps(soulBuyer)==500 && soulBuyer.Entity.ConquerPoints==500,
                "ordinary player buys every P6 soul with atomic saved deductions and immediate balance updates");
            Check(soulBuyer.Inventory.Objects.All(i=>!i.Bound && p6shop.Items.Contains(i.ID)),"P6 shop grants the selected unbound souls only");
            var poor=Player(map,2499,401);
            ItemHandler.HandleBuyFromNPC(new ItemUsage(true){UID=91010,dwParam=p6shop.Items[0],dwExtraInfo=1},poor);
            Check(SavedCps(poor)==2499 && SavedItems(poor)==0,"P6 insufficient CPs cannot charge or grant");
            ItemHandler.HandleBuyFromNPC(new ItemUsage(true){UID=91010,dwParam=723342,dwExtraInfo=1},soulBuyer);
            Check(SavedCps(soulBuyer)==500 && SavedItems(soulBuyer)==27,"P6 shop rejects spoofed off-catalog stock");
            var remote=Player(map,2500);
            ItemHandler.HandleBuyFromNPC(new ItemUsage(true){UID=91010,dwParam=p6shop.Items[0],dwExtraInfo=1},remote);
            Check(SavedCps(remote)==2500 && SavedItems(remote)==0,"remote P6 purchases cannot charge or grant");

            object[] cost={uint.MaxValue,uint.MaxValue,uint.MaxValue,(uint)0};
            Check(!(bool)Call("TryCost",cost),"overflowing price multiplication is rejected");
            Check(Call("Plan",new ConquerItem[0],723342u,uint.MaxValue,(ushort)10000)==null,"oversized quantities are bounded");
            var full=Enumerable.Range(0,40).Select(i=>new ConquerItem(true){UID=(uint)(500+i),ID=1088001,StackSize=1}).ToArray();
            Check(Call("Plan",full,723342u,1u,(ushort)10000)==null,"full bag rejects a new item without charging");
            full[0].ID=723342; full[0].StackSize=9;
            Check(Call("Plan",full,723342u,1u,(ushort)10)!=null,"full bag can fill an existing compatible stack");
            full[0].Bound=true;
            Check(Call("Plan",full,723342u,1u,(ushort)10)==null,"paid unbound items never merge into bound stacks");

            var supplies=Player(map,1000);
            foreach(uint id in ids) ItemHandler.HandleBuyFromNPC(Request(id,1),supplies);
            Check(supplies.Account.State==AccountTable.AccountState.Player && supplies.Inventory.Count==11 && SavedItems(supplies)==11 && SavedCps(supplies)==535, "ordinary player buys all eleven subclass items for 465 CPs using loaded prices");
            var p=Player(map,1000);
            ItemHandler.HandleBuyFromNPC(Request(723342,2),p);
            Check(p.Entity.ConquerPoints==802 && SavedCps(p)==802,"ordinary player pays 198 CPs once for two Modesty Books");
            Check(p.Inventory.Count==1 && SavedItems(p)==2,"purchased stack persists with exact quantity");
            var book=p.Inventory.Objects[0];
            ItemHandler.UseItem(book,p);
            Check(p.Entity.SubClasses.StudyPoints==500 && book.StackSize==1 && SavedItems(p)==1,"Modesty Book consumes one book and grants 500 points");
            Check(Scalar("SELECT studypoints FROM entities WHERE uid=@uid",p.Entity.UID)==500,"study points persist immediately");
            ItemHandler.UseItem(book,p);
            ItemHandler.UseItem(book,p);
            Check(p.Entity.SubClasses.StudyPoints==1000 && p.Inventory.Count==0 && SavedItems(p)==0,"reusing a consumed book cannot award duplicate study points");
            ItemHandler.HandleBuyFromNPC(Request(728883,1),p);
            ItemHandler.UseItem(p.Inventory.Objects[0],p);
            Check(p.Entity.SubClasses.StudyPoints==1050,"Cordiality Book awards 50 points");
            ItemHandler.HandleBuyFromNPC(Request(723340,0),p);
            ItemHandler.UseItem(p.Inventory.Objects[0],p);
            Check(p.Entity.SubClasses.StudyPoints==1055,"zero quantity follows native single-item purchase convention");
            ItemHandler.HandleBuyFromNPC(Request(723341,1),p);
            ItemHandler.UseItem(p.Inventory.Objects[0],p);
            Check(p.Entity.SubClasses.StudyPoints==1075,"Endurance Book awards 20 points");

            uint balance=p.Entity.ConquerPoints;
            ItemHandler.HandleBuyFromNPC(Request(723342,1000),p);
            ItemHandler.HandleBuyFromNPC(Request(999999,1),p);
            var bound=Request(723342,1); bound.dwExtraInfo3=2; ItemHandler.HandleBuyFromNPC(bound,p);
            Check(p.Entity.ConquerPoints==balance && SavedCps(p)==balance && SavedItems(p)==0,"insufficient CPs, unlisted items and bound-CP requests grant nothing");
            p.Entity.X=450; ItemHandler.HandleBuyFromNPC(Request(723342,1),p); p.Entity.X=424;
            Check(p.Entity.ConquerPoints==balance,"remote NPC purchases are rejected");
            p.Trade.InTrade=true; ItemHandler.HandleBuyFromNPC(Request(723342,1),p); p.Trade.InTrade=false;
            Check(p.Entity.ConquerPoints==balance,"trading blocks purchases");
            ItemHandler.HandleBuyFromNPC(Request(723342,1),p);
            p.Entity.SubClasses.StudyPoints=65500;
            ItemHandler.UseItem(p.Inventory.Objects[0],p);
            Check(p.Entity.SubClasses.StudyPoints==65500 && SavedItems(p)==1,"study-point overflow preserves the book");

            var concurrent=Player(map,1000);
            Parallel.For(0,20,i=>ItemHandler.HandleBuyFromNPC(Request(723342,1),concurrent));
            Check(!ReloadRequired(concurrent) && concurrent.Entity.ConquerPoints==10 && SavedCps(concurrent)==10 && SavedItems(concurrent)==10,
                "concurrent purchases cannot overspend or grant unpaid items");
            var inner=Player(map,6000);
            foreach(uint id in innerIds) { var request=Request(id,1); request.UID=91009; ItemHandler.HandleBuyFromNPC(request,inner); }
            Check(inner.Account.State==AccountTable.AccountState.Player && inner.Inventory.Count==17 && SavedItems(inner)==17,
                "ordinary player can buy every Inner Power book from the second shop");
            Check(inner.Entity.ConquerPoints==2400 && SavedCps(inner)==2400,"17 Inner Power books cost exactly 3600 CPs in total");
            var innerBulk=Request(3005365,2); innerBulk.UID=91009; ItemHandler.HandleBuyFromNPC(innerBulk,inner);
            Check(inner.Inventory.Count==19 && SavedItems(inner)==19 && SavedCps(inner)==2200,"non-stackable Inner Power books use separate inventory slots");
            var wrongShop=Request(723342,1); wrongShop.UID=91009; ItemHandler.HandleBuyFromNPC(wrongShop,inner);
            ItemHandler.HandleBuyFromNPC(Request(3005365,1),inner);
            Check(inner.Entity.ConquerPoints==2200 && SavedItems(inner)==19,"each shop enforces its own item list");
            const uint vesper=Revora.Game.Features.RevoraPointShopSystem.NpcId;
            map.Npcs.Add(vesper,new NpcSpawn{UID=vesper,Type=Enums.NpcType.Talker,X=396,Y=340,MapID=1002,Mesh=50110});
            var runes=Player(map,123); runes.ActiveNpc=vesper; runes.Entity.RevoraPoint=24000;
            Revora.Game.Features.RevoraPointShopSystem.HandleNpc(runes,new NpcRequest(true){OptionID=180});
            Check(runes.Inventory.Count==0 && runes.Entity.RevoraPoint==24000,"Vesper rejects rune purchases from outside interaction range");
            runes.Entity.X=396; runes.Entity.Y=340;
            foreach(byte page in new byte[]{0,20,110,111,112,113,114,115,116})
                Check(Revora.Game.Features.RevoraPointShopSystem.HandleNpc(runes,new NpcRequest(true){OptionID=page}),"Vesper rune dialogue page "+page+" handles ordinary players");
            for(byte option=180;option<=212;option++) Revora.Game.Features.RevoraPointShopSystem.HandleNpc(runes,new NpcRequest(true){OptionID=option});
            Check(runes.Inventory.Count==33 && SavedItems(runes)==33 && runes.Inventory.Objects.All(i=>i.ID>=4010000 && i.ID<4040000 && i.ID%100==1),
                "all 33 Vesper rune options grant the advertised level-one rune");
            Check(runes.Entity.RevoraPoint==0 && Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",runes.Entity.UID)==0,
                "Vesper rune prices total 24000 RVPs and persist immediately");
            Check(runes.Entity.ConquerPoints==123 && SavedCps(runes)==123,"RVP rune purchases never charge CPs");
            Revora.Game.Features.RevoraPointShopSystem.HandleNpc(runes,new NpcRequest(true){OptionID=200});
            Revora.Game.Features.RevoraPointShopSystem.HandleNpc(runes,new NpcRequest(true){OptionID=21});
            Check(runes.Inventory.Count==33 && SavedItems(runes)==33,"insufficient RVPs and removed Khopesh option cannot grant items");
            var failed=Player(map,1000);
            Query("ALTER TABLE items ADD CONSTRAINT validation_failure CHECK (stacksize < 5) NOT VALID");
            try { ItemHandler.HandleBuyFromNPC(Request(723342,5),failed); } catch(NullReferenceException) { /* Offline fixture has no server thread for Disconnect. */ }
            catch(ArgumentNullException) { /* Offline fixture has no screen timer locks for Disconnect. */ }
            Check(SavedCps(failed)==1000 && SavedItems(failed)==0 && failed.Inventory.Count==0,"database failure rolls back both CP deduction and item insertion");
            Check(ReloadRequired(failed),"failed or ambiguous persistence closes the purchase path until reload");
            Console.WriteLine("Subclass shop checks passed: "+checks); return 0;
        }
        catch(Exception e) { Console.Error.WriteLine(e.GetType().Name+": "+e.Message+"\n"+e.StackTrace); return 1; }
        finally
        {
            if(original!=null) DataHolder.ConnectionString=original;
            if(created) {
                Query("DROP TABLE IF EXISTS revora_subclass_shop_validation.items");
                Query("DROP TABLE IF EXISTS revora_subclass_shop_validation.entities");
                Query("DROP SCHEMA revora_subclass_shop_validation");
                Console.WriteLine("Disposable validation schema removed.");
            }
        }
    }
}
