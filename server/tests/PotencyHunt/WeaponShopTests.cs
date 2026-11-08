using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type BookShop=typeof(Entity).Assembly.GetType("Revora.Game.Features.WeaponBookShop");
    static DateTime bookShopNow=new DateTime(2030,1,2,3,4,5,DateTimeKind.Utc);
    static GameState BookBuyer(Map map,ulong money=100000)
    {
        var p=SkillPlayer(map);p.Entity.Money=money;p.Entity.X=550;p.Entity.Y=550;
        if(!EntityTable.SaveEntity(p))throw new Exception("Book buyer save failed.");return p;
    }
    static ItemUsage BuyBook(uint id,uint amount=1,uint shop=141,ushort action=1)
    {
        var request=new ItemUsage(true){UID=shop,dwParam=id,dwExtraInfo=amount};var bytes=request.ToArray();
        Writer.WriteUInt16(action,20,bytes);Writer.WriteUInt64(PacketHandler.ClientSeal,bytes.Length-8,bytes);return request;
    }
    static long BookPurchases(GameState p) {return Scalar("SELECT count(*) FROM revora_weapon_book_purchases WHERE uid=@uid",p.Entity.UID);}
    static void VerifyWeaponShop(Map map)
    {
        ShopFile.Load();Call(BookShop,null,"Initialize");BookShop.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>bookShopNow));
        Query("CREATE TABLE IF NOT EXISTS itemadding (LIKE public.itemadding INCLUDING ALL)");
        map.Npcs[141]=new NpcSpawn{UID=141,MapID=map.ID,X=550,Y=550};
        map.Npcs[423]=new NpcSpawn{UID=423,MapID=map.ID,X=550,Y=550};
        uint[] books={725005,725010,725011,725012,725013,725014,725026,725027,725029,725030,725031,725040,725041,725042,725043,725044};
        foreach(uint id in books)
        {
            var p=BookBuyer(map);var request=BuyBook(id,2);ItemHandler.HandleBuyFromNPC(request,p);
            Check(!Fenced(p) && p.Entity.Money==90000 && p.Inventory.Count==2 && p.Inventory.Objects.All(i=>i.ID==id && !i.Bound && i.StackSize==1)
                && Scalar("SELECT money FROM entities WHERE uid=@uid",p.Entity.UID)==90000 && BookPurchases(p)==1
                && Scalar("SELECT count(*) FROM items i JOIN revora_weapon_book_purchases b ON i.uid=ANY(b.item_uids) AND i.id=b.item_type AND b.uid=i.entityid WHERE i.entityid=@uid AND i.stacksize=1 AND i.bound=0 AND i.color=3",p.Entity.UID)==2,
                "ordinary shop purchase commits two exact nonstacking books and their 10,000 silver payment "+id);
            Parallel.For(0,10,i=>ItemHandler.HandleBuyFromNPC(BuyBook(id,2),p));
            Check(!Fenced(p) && p.Entity.Money==90000 && p.Inventory.Count==2 && BookPurchases(p)==1,
                "concurrent duplicate purchase clicks cannot charge twice within the durable debounce "+id);
            Check(EntityTable.SaveEntity(p) && !Fenced(p),"book payment advances the silver preimage used by production character saves "+id);
        }
        var p1=BookBuyer(map);ItemHandler.HandleBuyFromNPC(BuyBook(725005,0),p1);ItemHandler.HandleBuyFromNPC(BuyBook(725005),p1);
        Check(p1.Inventory.Count==1 && p1.Entity.Money==95000 && BookPurchases(p1)==1,"zero quantity means one and shares the same duplicate-click identity");
        bookShopNow=bookShopNow.AddSeconds(1);ItemHandler.HandleBuyFromNPC(BuyBook(725005),p1);
        Check(p1.Inventory.Count==2 && p1.Entity.Money==90000 && BookPurchases(p1)==2,"a later intentional matching purchase works at the exact one-second boundary");
        var reloaded=BookBuyer(map);Kernel.GamePool.Remove(reloaded.Entity.UID);p1.Action=0;
        reloaded.Entity.UID=p1.Entity.UID;reloaded.Account.EntityID=p1.Entity.UID;Kernel.GamePool[p1.Entity.UID]=reloaded;
        using(var command=new MySqlCommand(MySqlCommandType.SELECT).Select("entities").Where("UID",reloaded.Entity.UID))
        using(var reader=new MySqlReader(command)){if(!reader.Read())throw new Exception("Book buyer reload missing.");reloaded.Entity.Money=reader.ReadUInt64("Money");Call(SilverStore,null,"Loaded",reloaded,reader);}
        ConquerItemTable.LoadItems(reloaded);Call(BookShop,null,"Initialize");
        ItemHandler.HandleBuyFromNPC(BuyBook(725005),reloaded);ItemHandler.HandleBuyFromNPC(BuyBook(725005),p1);
        Check(!Fenced(reloaded) && reloaded.Entity.Money==90000 && reloaded.Inventory.Count==2 && BookPurchases(reloaded)==2
            && reloaded.Inventory.Objects.All(i=>i.ID==725005 && i.StackSize==1 && !i.Bound && (byte)i.Color==3),
            "fresh canonical session reloads purchased books and the durable debounce rejects both reconnect and stale-session repeats");
        var all=BookBuyer(map,200000);ItemHandler.HandleBuyFromNPC(BuyBook(725029,40,423,55),all);
        Check(!Fenced(all) && all.Entity.Money==0 && all.Inventory.Count==40 && BookPurchases(all)==1,
            "the second installed shop and legacy buy alias can fill forty slots for exactly 200,000 silver");
        var denied=BookBuyer(map,4999);ItemHandler.HandleBuyFromNPC(BuyBook(725029),denied);
        Check(!Fenced(denied) && denied.Inventory.Count==0 && denied.Entity.Money==4999 && BookPurchases(denied)==0,"insufficient funds preserve the entire purse");
        denied.Entity.Money=1000000;EntityTable.UpdateMoney(denied);
        foreach(uint amount in new[]{41u,uint.MaxValue})ItemHandler.HandleBuyFromNPC(BuyBook(725029,amount),denied);
        Check(!Fenced(denied) && denied.Inventory.Count==0 && denied.Entity.Money==1000000,"oversized quantities cannot enter an unbounded item loop or overflow the price");
        var partial=BookBuyer(map);for(int i=0;i<39;i++)Pack(partial,1000000,1);
        ItemHandler.HandleBuyFromNPC(BuyBook(725029,2),partial);
        Check(!Fenced(partial) && partial.Inventory.Count==39 && partial.Entity.Money==100000 && BookPurchases(partial)==0,
            "insufficient total space cannot partially deliver or partially charge a multi-buy");
        ItemHandler.HandleBuyFromNPC(BuyBook(725029),partial);
        Check(!Fenced(partial) && partial.Inventory.Count==40 && partial.Entity.Money==95000,"a single requested book uses the remaining slot");
        var failure=BookBuyer(map);Query("ALTER TABLE revora_weapon_book_purchases ADD CONSTRAINT fail_book_receipt CHECK(false) NOT VALID");
        ItemHandler.HandleBuyFromNPC(BuyBook(725029,3),failure);
        Check(Fenced(failure) && failure.Entity.Money==100000 && failure.Inventory.Count==0 && BookPurchases(failure)==0
            && Scalar("SELECT money FROM entities WHERE uid=@uid",failure.Entity.UID)==100000
            && Scalar("SELECT count(*) FROM items WHERE entityid=@uid",failure.Entity.UID)==0,
            "failed receipt rolls back payment and all delivered item rows before cache updates");
        Query("ALTER TABLE revora_weapon_book_purchases DROP CONSTRAINT fail_book_receipt");DemonReset(failure);
        ItemHandler.HandleBuyFromNPC(BuyBook(725029,3),failure);
        Check(!Fenced(failure) && failure.Entity.Money==85000 && failure.Inventory.Count==3 && BookPurchases(failure)==1,
            "failed transaction can retry immediately without false duplicate suppression");
        var silent=BookBuyer(map);
        Query("CREATE FUNCTION suppress_book_receipt() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER suppress_book_receipt BEFORE INSERT ON revora_weapon_book_purchases FOR EACH ROW EXECUTE FUNCTION suppress_book_receipt()");
        ItemHandler.HandleBuyFromNPC(BuyBook(725029),silent);
        Check(Fenced(silent) && silent.Inventory.Count==0 && silent.Entity.Money==100000 && BookPurchases(silent)==0
            && Scalar("SELECT money FROM entities WHERE uid=@uid",silent.Entity.UID)==100000
            && Scalar("SELECT count(*) FROM items WHERE entityid=@uid",silent.Entity.UID)==0,
            "silently suppressed receipt cannot commit an unrecorded charge or item delivery");
        Query("DROP TRIGGER suppress_book_receipt ON revora_weapon_book_purchases; DROP FUNCTION suppress_book_receipt()");
        var pending=BookBuyer(map);pending.Entity.AdjustMoney(100);ItemHandler.HandleBuyFromNPC(BuyBook(725029),pending);
        Check(!Fenced(pending) && pending.Entity.Money==100100 && pending.Inventory.Count==0,"unsaved silver defers purchase without losing the pending credit");
        EntityTable.UpdateMoney(pending);ItemHandler.HandleBuyFromNPC(BuyBook(725029),pending);
        Check(!Fenced(pending) && pending.Entity.Money==95100 && pending.Inventory.Count==1,"purchase resumes after the pending purse is saved");
        var stale=BookBuyer(map);Query("UPDATE entities SET money=100001 WHERE uid=@uid","uid",(long)stale.Entity.UID);
        ItemHandler.HandleBuyFromNPC(BuyBook(725029),stale);
        Check(Fenced(stale) && stale.Inventory.Count==0 && Scalar("SELECT money FROM entities WHERE uid=@uid",stale.Entity.UID)==100001,
            "stale cached purse cannot overwrite a changed durable balance during purchase");
        var inventory=BookBuyer(map);var absent=Pack(inventory,1000000,1);inventory.Inventory.Remove(absent.UID,Enums.ItemUse.None,false);
        ItemHandler.HandleBuyFromNPC(BuyBook(725029),inventory);
        Check(Fenced(inventory) && BookPurchases(inventory)==0 && inventory.Entity.Money==100000,"mismatched durable inventory count prevents purchase against stale capacity");
        var guarded=BookBuyer(map);guarded.Entity.X=580;ItemHandler.HandleBuyFromNPC(BuyBook(725029),guarded);guarded.Entity.X=550;
        guarded.Trade.InTrade=true;ItemHandler.HandleBuyFromNPC(BuyBook(725029),guarded);guarded.Trade.InTrade=false;
        guarded.Fake=true;ItemHandler.HandleBuyFromNPC(BuyBook(725029),guarded);guarded.Fake=false;
        guarded.Entity.FullyLoaded=false;ItemHandler.HandleBuyFromNPC(BuyBook(725029),guarded);guarded.Entity.FullyLoaded=true;
        ItemHandler.HandleBuyFromNPC(BuyBook(725029,1,99999),guarded);
        Check(!Fenced(guarded) && guarded.Inventory.Count==0 && BookPurchases(guarded)==0,"distance, trade, fake, incomplete login and missing shop actors cannot purchase");
        var shopRule=ShopFile.Shops[141];shopRule.Items.Remove(725029);ItemHandler.HandleBuyFromNPC(BuyBook(725029),guarded);shopRule.Items.Add(725029);
        shopRule.MoneyType=ShopFile.MoneyType.ConquerPoints;ItemHandler.HandleBuyFromNPC(BuyBook(725029),guarded);shopRule.MoneyType=ShopFile.MoneyType.Gold;
        guarded.Booth=new Revora.Game.ConquerStructures.Booth();ItemHandler.HandleBuyFromNPC(BuyBook(725029),guarded);guarded.Booth=null;
        Check(!Fenced(guarded) && guarded.Inventory.Count==0 && guarded.Entity.Money==100000,
            "catalog membership, supported currency and booth state remain server-authoritative");
        var racing=BookBuyer(map);
        Parallel.Invoke(()=>{for(int i=0;i<40;i++)ItemHandler.HandleBuyFromNPC(BuyBook(725029),racing);},
            ()=>{for(int i=0;i<100;i++){racing.Entity.AdjustMoney(1);EntityTable.UpdateMoney(racing);}},
            ()=>{for(int i=0;i<10;i++)if(!EntityTable.SaveEntity(racing))throw new Exception("Racing purchase save failed.");});
        ItemHandler.HandleBuyFromNPC(BuyBook(725029),racing);EntityTable.UpdateMoney(racing);
        Check(!Fenced(racing) && racing.Entity.Money==95100 && BookPurchases(racing)==1 && racing.Inventory.Count==1
            && Scalar("SELECT money FROM entities WHERE uid=@uid",racing.Entity.UID)==95100,
            "concurrent purchase, silver credits and character saves preserve every credit and one exact charge");
        var info=ConquerItemInformation.BaseInformations[725029];uint price=info.GoldWorth;
        var wide=BookBuyer(map,(ulong)uint.MaxValue*2);
        try{info.GoldWorth=uint.MaxValue;ItemHandler.HandleBuyFromNPC(BuyBook(725029,2),wide);}finally{info.GoldWorth=price;}
        Check(!Fenced(wide) && wide.Entity.Money==0 && wide.Inventory.Count==2
            && Scalar("SELECT cost FROM revora_weapon_book_purchases WHERE uid=@uid",wide.Entity.UID)==(long)uint.MaxValue*2,
            "price multiplication remains exact above the old UInt32 limit");
        var learned=BookBuyer(map);BookProf(learned,410,5);ItemHandler.HandleBuyFromNPC(BuyBook(725005),learned);
        ItemHandler.UseItem(learned.Inventory.Objects.Single(),learned);
        for(int i=0;i<4;i++)learned.IncreaseSpellExperience(uint.MaxValue,1045);
        Check(!Fenced(learned) && learned.Inventory.Count==0 && learned.Entity.Money==95000 && learned.Spells[1045].Level==4
            && HasAchievement(learned,10504) && BookPurchases(learned)==1,"ordinary purchase, book learning and training complete the saved Fast Blade achievement route");
        VerifyBookPurchaseFrames(map);
    }
    static void VerifyBookPurchaseFrames(Map map)
    {
        var p=BookBuyer(map);
        foreach(int length in new[]{0,8,15,16,29,37,129})
        {
            var bytes=new byte[length];if(length>=16)Writer.WriteUInt32(725029,12,bytes);
            if(length>=22)Writer.WriteUInt16(1,20,bytes);
            var request=new ItemUsage(false);request.Deserialize(bytes);ItemHandler.HandleBuyFromNPC(request,p);
            ItemUsage.Process(bytes,p);
        }
        for(int mutation=0;mutation<4;mutation++)
        {
            var request=BuyBook(725029);var bytes=request.ToArray();
            if(mutation==0)bytes[0]--;if(mutation==1)bytes[2]++;if(mutation==2)bytes[bytes.Length-1]^=1;if(mutation==3)Writer.WriteUInt16(257,20,bytes);
            ItemHandler.HandleBuyFromNPC(request,p);
            ItemUsage.Process(bytes,p);
        }
        Check(!Fenced(p) && p.Entity.Money==100000 && p.Inventory.Count==0 && BookPurchases(p)==0,
            "short, oversized, malformed header/seal and high-action-bit book frames cannot charge");
        var minimal=new byte[38];Array.Copy(BuyBook(725029).ToArray(),minimal,30);Writer.WriteUInt16(30,0,minimal);Writer.WriteUInt64(PacketHandler.ClientSeal,30,minimal);
        minimal[16]=44;
        ItemUsage.Process(minimal,p);
        Check(!Fenced(p) && p.Inventory.Count==1 && p.Entity.Money==95000,"reviewed minimum buy frame uses action twenty without the legacy equipment fallback at byte sixteen");
    }
}
