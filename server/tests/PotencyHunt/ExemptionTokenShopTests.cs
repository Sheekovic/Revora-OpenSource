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
    static readonly Type TokenShop=typeof(Entity).Assembly.GetType("Revora.Game.Features.ExemptionTokenShop");
    static DateTime tokenShopNow=new DateTime(2032,3,3,0,0,0,DateTimeKind.Utc);
    static long TokenPurchases(GameState p){return Scalar("SELECT count(*) FROM revora_exemption_purchases WHERE uid=@uid",p.Entity.UID);}
    static ItemUsage BuyToken(uint amount=1,uint shop=2888,bool bound=false,ushort action=1) {
        var packet=BuyBook(723701,amount,shop,action);packet.dwExtraInfo3=bound?2u:0u;return packet;
    }
    static GameState TokenBuyer(Map town,uint cp=10000) {
        var p=BallPlayer(town);p.Entity.ConquerPoints=cp;p.Entity.BoundCps=cp;EntityTable.SaveEntity(p);return p;
    }
    static void VerifyTokenShop(string runtime,Map town) {
        VerifySouls(runtime,town);EShopFile.Load();Call(TokenShop,null,"Initialize");Call(TokenShop,null,"Initialize");
        TokenShop.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>tokenShopNow));
        Check(EShopFile.Shops[2888].Items.Contains(723701) && EShopFile.Shops[432].Items.Contains(723701)
            && ConquerItemInformation.BaseInformations[723701].ConquerPointsWorth==888,"actual loaded token stock and override retain configured 888 CP price");
        foreach(bool bound in new[]{false,true}) {
            var p=TokenBuyer(town);var request=BuyToken(2,2888,bound);
            Parallel.For(0,10,i=>ItemUsage.Process(request.ToArray(),p));
            Check(!Fenced(p) && TokenPurchases(p)==1 && p.Inventory.Objects.Count(i=>i.ID==723701 && i.Bound==bound && i.StackSize==1)==2
                && (bound?p.Entity.BoundCps:p.Entity.ConquerPoints)==8224 && (bound?p.Entity.ConquerPoints:p.Entity.BoundCps)==10000,
                "concurrent native token purchases deliver two tokens and debit selected currency once: "+bound);
            p=ReloadBall(p);ItemHandler.HandleBuyFromNPC(request,p);
            Check(!Fenced(p) && TokenPurchases(p)==1 && p.Inventory.Count==2 && (bound?p.Entity.BoundCps:p.Entity.ConquerPoints)==8224,
                "production reconnect preserves binding/payment and durable duplicate guard: "+bound);
            tokenShopNow=tokenShopNow.AddSeconds(1);ItemHandler.HandleBuyFromNPC(BuyToken(0,2888,bound,55),p);
            Check(TokenPurchases(p)==2 && p.Inventory.Count==3 && (bound?p.Entity.BoundCps:p.Entity.ConquerPoints)==7336 && EntityTable.SaveEntity(p),
                "later purchase accepts zero-as-one and action-55 alias without stale autosave: "+bound);
        }
        foreach(string mode in new[]{"poor","full","quantity","unknown-currency","shop","trade","fake","dead","unloaded","replaced","npc-distance"}) {
            var p=TokenBuyer(town,mode=="poor"?887u:10000u);var packet=BuyToken();
            if(mode=="full")while(p.Inventory.Count<40)Pack(p,1088001,1);
            if(mode=="quantity")packet.dwExtraInfo=uint.MaxValue;if(mode=="unknown-currency")packet.dwExtraInfo3=3;
            if(mode=="shop")packet.UID=1;if(mode=="trade")p.Trade.InTrade=true;if(mode=="fake")p.Fake=true;
            if(mode=="dead")p.Entity.Hitpoints=0;if(mode=="unloaded")p.Entity.FullyLoaded=false;if(mode=="replaced")Kernel.GamePool.Remove(p.Entity.UID);
            if(mode=="npc-distance")packet.UID=432;
            uint before=p.Entity.ConquerPoints;ItemHandler.HandleBuyFromNPC(packet,p);
            Check(TokenPurchases(p)==0 && p.Entity.ConquerPoints==before && !p.Inventory.Contains(723701,1),"rejected token purchase preserves balance: "+mode);
        }
        foreach(bool bound in new[]{false,true})foreach(string fault in new[]{"balance","bag","items","receipt","entities"}) {
            var p=TokenBuyer(town);bool trigger=fault=="items" || fault=="receipt" || fault=="entities";string table=fault=="receipt"?"revora_exemption_purchases":fault;
            if(fault=="balance")Query("UPDATE entities SET "+(bound?"boundcps":"conquerpoints")+"=10001 WHERE uid=@uid","uid",(long)p.Entity.UID);
            if(fault=="bag"){var orphan=Pack(p,1088001,1);p.Inventory.Remove(orphan.UID,Enums.ItemUse.None,false);}
            if(trigger)Query("CREATE FUNCTION suppress_token_buy() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_token_buy BEFORE INSERT OR UPDATE ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_token_buy()");
            try{ItemHandler.HandleBuyFromNPC(BuyToken(1,2888,bound),p);}finally{if(trigger)Query("DROP TRIGGER suppress_token_buy ON "+table+";DROP FUNCTION suppress_token_buy()");}
            Check(Fenced(p) && TokenPurchases(p)==0 && !p.Inventory.Contains(723701,1) && (bound?p.Entity.BoundCps:p.Entity.ConquerPoints)==10000
                && Scalar("SELECT COALESCE("+(bound?"boundcps":"conquerpoints")+",0) FROM entities WHERE uid=@uid",p.Entity.UID)==(fault=="balance"?10001:10000),
                "token transaction rolls back every gift and payment on changed/suppressed write: "+bound+"/"+fault);
        }
        var wire=TokenBuyer(town);
        foreach(string shape in new[]{"short","long","length","type","action","seal"}) {
            byte[] bytes=BuyToken().ToArray();if(shape=="short")Array.Resize(ref bytes,37);if(shape=="long")Array.Resize(ref bytes,129);
            if(shape=="length")bytes[0]++;if(shape=="type")Writer.WriteUInt16(1008,2,bytes);if(shape=="action")Writer.WriteUInt16(5,20,bytes);if(shape=="seal")bytes[bytes.Length-1]^=1;
            var request=new ItemUsage(false);request.Deserialize(bytes);Call(TokenShop,null,"TryBuy",wire,request);
            Check(TokenPurchases(wire)==0 && wire.Entity.ConquerPoints==10000,"malformed token purchase is inert: "+shape);
        }
        town.Npcs[432]=new NpcSpawn{UID=432,MapID=1002,X=550,Y=550};var nearby=TokenBuyer(town);nearby.Entity.X=550;nearby.Entity.Y=550;
        ItemHandler.HandleBuyFromNPC(BuyToken(1,432),nearby);Check(!Fenced(nearby) && TokenPurchases(nearby)==1,"loaded local CP shop requires its nearby canonical NPC");
        VerifyRuneStoneShop(town);
        VerifyBoundOnlyMaterials(town);
        VerifyEarnedSecondRebirth(town);
    }
    static void VerifyBoundOnlyMaterials(Map town) {
        VerifyNativeProgressionShopPriority(town);
        Call(TokenShop,null,"Initialize");
        var clock=TokenShop.GetField("Clock",Hidden);var oldClock=clock.GetValue(null);
        clock.SetValue(null,(Func<DateTime>)(()=>tokenShopNow));
        var shop=EShopFile.Shops[2888];var currency=shop.MoneyType;
        try {
            shop.MoneyType=ShopFile.MoneyType.BoundCps;
            foreach(uint type in new uint[]{723701,730001}) foreach(uint selector in new uint[]{0,1,2}) {
                var p=TokenBuyer(town,1000000);var request=BuyBook(type,1,2888);request.dwExtraInfo3=selector;
                Parallel.For(0,8,n=>ItemHandler.HandleBuyFromNPC(request,p));
                p=ReloadBall(p);ItemHandler.HandleBuyFromNPC(request,p);
                var item=p.Inventory.Objects.Single();
                Check(!Fenced(p)&&TokenPurchases(p)==1&&item.ID==type&&item.Bound
                    &&item.Plus==(type==730001?1:0)&&p.Entity.ConquerPoints==1000000
                    &&p.Entity.BoundCps==1000000-ConquerItemInformation.BaseInformations[type].ConquerPointsWorth,
                    "bound-only material purchase survives concurrent request, cold inventory load and replay: "+type+"/"+selector);
            }
            var empty=TokenBuyer(town,1000000);empty.Entity.BoundCps=0;EntityTable.SaveEntity(empty);
            ItemHandler.HandleBuyFromNPC(BuyToken(),empty);
            Check(TokenPurchases(empty)==0&&empty.Inventory.Count==0&&empty.Entity.ConquerPoints==1000000,
                "bound-only material shop cannot fall back to ordinary CPs");
        } finally {shop.MoneyType=currency;clock.SetValue(null,oldClock);}
    }
    static void VerifyNativeProgressionShopPriority(Map town) {
        var general=typeof(Entity).Assembly.GetType("Revora.Game.Features.CpShopPurchases");
        if(general!=null)Call(general,null,"Initialize");
        if(ShopFile.Shops==null)ShopFile.Load();
        Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.P6SoulShop"),null,"Create");
        uint[] shops={91008,91009,91010}, types={723342,3005365,800000};
        for(int n=0;n<shops.Length;n++) {
            uint id=shops[n],type=types[n];ShopFile.Shop oldShop;
            Revora.Interfaces.INpc oldNpc;ShopFile.Shops.TryGetValue(id,out oldShop);town.Npcs.TryGetValue(id,out oldNpc);
            try {
                ShopFile.Shops[id]=new ShopFile.Shop{UID=id,MoneyType=ShopFile.MoneyType.ConquerPoints,
                    Items=new System.Collections.Generic.List<uint>{type}};
                town.Npcs[id]=new NpcSpawn{UID=id,MapID=1002,X=424,Y=339,Type=Enums.NpcType.Shop};
                var p=TokenBuyer(town,1000000);p.Entity.X=424;p.Entity.Y=339;
                Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.SessionInstall"),null,"Install",p);
                var request=BuyBook(type,1,id);request.dwExtraInfo3=2;
                ItemHandler.HandleBuyFromNPC(request,p);
                Check(!Fenced(p)&&p.Inventory.Count==0&&p.Entity.ConquerPoints==1000000&&p.Entity.BoundCps==1000000
                    &&Scalar("SELECT count(*) FROM items WHERE entityid=@uid",p.Entity.UID)==0,
                    "general CP handler cannot bypass native progression shop regular-CP policy: "+id);
                request.dwExtraInfo3=0;ItemHandler.HandleBuyFromNPC(request,p);p=ReloadBall(p);
                Check(!Fenced(p)&&p.Inventory.Objects.Single().ID==type&&!p.Inventory.Objects.Single().Bound
                    &&p.Entity.ConquerPoints==1000000-ConquerItemInformation.BaseInformations[type].ConquerPointsWorth
                    &&p.Entity.BoundCps==1000000,
                    "native progression purchase keeps exact regular-CP price and unbound reward after reload: "+id);
            } finally {
                if(oldShop==null)ShopFile.Shops.Remove(id);else ShopFile.Shops[id]=oldShop;
                if(oldNpc==null)town.Npcs.Remove(id);else town.Npcs[id]=oldNpc;
            }
        }
    }
    static void VerifyRuneStoneShop(Map town) {
        foreach(bool bound in new[]{false,true}){
            var p=TokenBuyer(town,1000000);var request=BuyBook(730008,12,2888);request.dwExtraInfo3=bound?2u:0u;
            Parallel.For(0,10,i=>ItemUsage.Process(request.ToArray(),p));p=ReloadBall(p);
            Check(!Fenced(p) && TokenPurchases(p)==1 && p.Inventory.Objects.Count(i=>i.ID==730008 && i.Plus==8 && i.Bound==bound)==12
                && (bound?p.Entity.BoundCps:p.Entity.ConquerPoints)==685072 && (bound?p.Entity.ConquerPoints:p.Entity.BoundCps)==1000000,
                "concurrent +8 stone purchase persists exact enhancement binding quantity and selected payment: "+bound);
            ItemUsage.Process(request.ToArray(),p);
            Check(TokenPurchases(p)==1 && p.Inventory.Count==12,"stone replay after reconnect does not repeat payment or delivery: "+bound);
        }
        foreach(string table in new[]{"items","entities","revora_exemption_purchases"}){
            var p=TokenBuyer(town,1000000);
            Query("CREATE FUNCTION suppress_stone_buy() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_stone_buy BEFORE INSERT OR UPDATE ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_stone_buy()");
            try{ItemUsage.Process(BuyBook(730008,12,2888).ToArray(),p);}
            finally{Query("DROP TRIGGER suppress_stone_buy ON "+table+";DROP FUNCTION suppress_stone_buy()");}
            Check(Fenced(p) && TokenPurchases(p)==0 && p.Inventory.Count==0
                && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==1000000,
                "stone transaction rolls back payment materials and receipt when write is suppressed: "+table);
        }
        foreach(uint id in new uint[]{730001,730003,730004,730005,730006}){
            var p=TokenBuyer(town,1000000);ItemUsage.Process(BuyBook(id,1,2888).ToArray(),p);p=ReloadBall(p);
            Check(!Fenced(p) && p.Inventory.Objects.Single().Plus==id-730000
                && p.Entity.ConquerPoints==1000000-ConquerItemInformation.BaseInformations[id].ConquerPointsWorth,
                "loaded mall stone keeps catalog price and persisted enhancement: "+id);
        }
        var absent=TokenBuyer(town,1000000);ItemUsage.Process(BuyBook(730007,1,2888).ToArray(),absent);
        Check(TokenPurchases(absent)==0 && absent.Inventory.Count==0 && absent.Entity.ConquerPoints==1000000,
            "expanded stone route cannot buy an item absent from loaded mall stock");
    }
    static void VerifyEarnedSecondRebirth(Map town) {
        var p=SecondPlayer(town,15,25,135,12345);DisRemove(p,p.Inventory.Objects.Single(i=>i.ID==723701));p.Entity.ConquerPoints=0;
        p.Quests=new Quests(p);p.Quests.Load();EntityTable.SaveEntity(p);SoulNpc(p,79882,3);SoulWalkNpc(p,301,1);
        foreach(uint id in new uint[]{65822,65823,65824}){SoulWalkNpc(p,id,1);soulNow=soulNow.AddSeconds(3);Call(Souls,null,"Tick");}
        SoulWalkNpc(p,301,3);p=ReloadBall(p);uint earned=p.Entity.ConquerPoints;
        Check(!Fenced(p) && earned==Config.DailyQuestCp,"ordinary second-life character earns the actual daily quest CP reward from a zero purse");
        ItemUsage.Process(BuyToken().ToArray(),p);p=ReloadBall(p);var token=p.Inventory.Objects.Single(i=>i.ID==723701);
        Check(!Fenced(p) && p.Entity.ConquerPoints==earned-888 && TokenPurchases(p)==1,"earned CP buys the loaded mall token and survives production reload");
        // Ordinary scroll return; the scroll itself is fixture-supplied, not an earning claim.
        var scroll=Pack(p,1060020,1);ItemHandler.UseItem(scroll,p);
        Check(p.Entity.MapID==1002,"ordinary city scroll returns the token buyer to Twin City");
        var npc=town.Npcs[59558];var landing=FlowerTile(town,npc.X,npc.Y);AdventureWalk(p,landing.Item1,landing.Item2);p.ActiveNpc=59558;
        SecondOffer(p,61);SoulWire(p,180,59558);p=ReloadBall(p);SkillTable.LoadProficiencies(p);p.Entity.MyAchievement.Send();
        Check(!Fenced(p) && p.Entity.Reborn==2 && p.Entity.Class==61 && !p.Inventory.ContainsUID(token.UID) && SecondCount(p)==1
            && HasAchievement(p,10316) && !HasAchievement(p,11031) && p.Entity.ConquerPoints==earned-888,
            "ordinary earned-currency to purchased-token to walked Sereth rite persists complete ownership and only its achievement");
        // Seed only the later level boundary; the recovery receipt comes from the real rite above.
        p.Entity.Level=129;p.Entity.Experience=DataHolder.LevelExperience(129)-1;EntityTable.SaveEntity(p);p.IncreaseExperience(1,false);p=ReloadBall(p);
        Check(!Fenced(p) && p.Entity.Level==135 && p.Entity.Experience==12345 && SecondPending(p)==0,"purchased token receipt restores exact second-life EXP through production reload");
    }
}
