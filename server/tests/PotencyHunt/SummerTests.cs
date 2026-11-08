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
    static readonly Type Summer=typeof(Entity).Assembly.GetType("Revora.Game.Features.SummerHunt");
    static readonly Type SummerCraft=typeof(Entity).Assembly.GetType("Revora.Game.Features.SummerCrafting");
    static DateTime summerNow;
    static int summerRoll;
    static void SummerNpc(GameState p,byte option) {p.BindWorldMap(1002);MoonNpc(p,900000063,option);}
    static long SummerKills(GameState p) {return Scalar("SELECT coalesce(sum(kills),0) FROM revora_summer_daily WHERE uid=@uid",p.Entity.UID);}
    static int SummerAmount(GameState p,uint type) {return p.Inventory.Objects.Where(i=>i.ID==type).Sum(i=>(int)i.StackSize);}
    static void SummerGround(GameState p,out Map map,out uint type) {
        int route=(int)Scalar("SELECT route FROM revora_summer_daily WHERE uid=@uid ORDER BY day DESC LIMIT 1",p.Entity.UID);
        map=Kernel.Maps[route==0?(ushort)1015:(ushort)1001];type=new uint[]{18,20,57,58}[route];DemonMove(p,map);
    }
    static Entity SummerMob(GameState p) {Map map;uint type;SummerGround(p,out map,out type);return DemonMob(map,type);}
    static void SummerKill(GameState p) {var mob=SummerMob(p);mob.Die(p.Entity);p.Map.Entities.Remove(mob.UID);}
    static void StoreSummerRune(GameState p){
        var rune=p.Inventory.Objects.SingleOrDefault(i=>i.ID==4030101);
        if(rune==null)throw new Exception("Summer rune missing: kills="+SummerKills(p)+" days="+Scalar("SELECT count(*) FROM revora_summer_daily WHERE uid=@uid",p.Entity.UID)+" level="+p.Entity.Level+" fenced="+Fenced(p)+" dead="+p.Entity.Dead);
        Check(!rune.Bound && Scalar("SELECT count(*) FROM revora_summer_daily d JOIN items i ON i.uid=ANY(d.reward_ids) WHERE d.uid=@uid AND i.id=4030101 AND i.bound=0",p.Entity.UID)==p.RunesItem.Count+1,
            "earned Summer rune is unbound with a durable daily receipt");
        if(p.Entity.Vitality==0){p.Entity.Vitality=100;p.LoadItemStats();} // Give this preexisting level-120 fixture a viable stat profile before item stat recalculation.
        RuneMove(p,rune,61);
        Check(p.RunesItem.ContainsKey(rune.UID) && !p.Inventory.ContainsUID(rune.UID),"ordinary hunter stores the earned combat rune through the rune-bag handler");
    }
    static void SummerDay(GameState p) {SummerNpc(p,1);for(int i=0;i<120;i++)SummerKill(p);SummerNpc(p,3);StoreSummerRune(p);}
    static ConquerItem SummerMake(GameState p,int kind) {
        var before=p.Inventory.Objects.Select(i=>i.UID).ToArray();SummerNpc(p,(byte)(10+kind));SummerNpc(p,220);
        return p.Inventory.Objects.FirstOrDefault(i=>!before.Contains(i.UID));
    }
    static void SummerSelect(GameState p,ConquerItem item) {
        var list=p.Inventory.Objects.Where(i=>(i.ID>=3303772 && i.ID<=3303776) || (i.ID>=3303797 && i.ID<=3303801)).OrderBy(i=>i.UID).ToArray();
        int index=Array.IndexOf(list,item);if(index<0)throw new Exception("Wing package missing.");SummerNpc(p,(byte)(20+index/8));SummerNpc(p,(byte)(40+index));
    }
    static void SummerDye(GameState p,ConquerItem item,int color) {SummerSelect(p,item);SummerNpc(p,(byte)(80+color));SummerNpc(p,220);}
    static void SummerUnbind(GameState p,ConquerItem item) {SummerSelect(p,item);SummerNpc(p,85);SummerNpc(p,220);}
    static void TradeSend(GameState p,uint action,ulong value) {var packet=new Trade(true){Type=action,dwParam2=value};Trade.Process(packet.ToArray(),p);}
    static void TradeOpen(GameState a,GameState b) {
        a.BindWorldMap(1002);b.BindWorldMap(1002);a.Entity.X=b.Entity.X=460;a.Entity.Y=b.Entity.Y=281;
        a.Entity.TradeOn=b.Entity.TradeOn=true;TradeSend(a,Trade.Request,b.Entity.UID);TradeSend(b,Trade.Request,a.Entity.UID);
        if(!a.Trade.InTrade || !b.Trade.InTrade)throw new Exception("Ordinary reciprocal trade admission failed.");
    }
    static void SummerTradeChecks(GameState p,Map town) {
        var atomic=typeof(Entity).Assembly.GetType("Revora.Game.Features.AtomicTrade");Call(atomic,null,"Initialize");
        var buyer=MoonPlayer(town);ReloadSilver(p);ReloadSilver(buyer);buyer.Entity.ConquerPoints=1000;buyer.Entity.Money=10000;
        ulong overflowPrior=p.Entity.Money;p.Entity.Money=Entity.MaximumSilver;
        TradeOpen(p,buyer);TradeSend(buyer,Trade.SetMoney,1);TradeSend(p,Trade.Accept,0);TradeSend(buyer,Trade.Accept,0);
        Check(!Fenced(p) && !Fenced(buyer) && !p.Trade.InTrade && !buyer.Trade.InTrade
            && buyer.Entity.Money==10000 && p.Entity.Money==Entity.MaximumSilver
            && Scalar("SELECT count(*) FROM revora_trade_receipts WHERE left_uid=@uid OR right_uid=@uid",p.Entity.UID)==0,
            "trade preserves both purses when the recipient cannot durably hold the silver");p.Entity.Money=overflowPrior;
        var wing=SummerMake(p,1);SummerMake(p,4);SummerUnbind(p,wing);
        Check(wing!=null && wing.ID==3303797 && !wing.Bound,"earned Glow package unbinds with certainty for transfer");
        p.Booth=new Revora.Game.ConquerStructures.Booth();
        BoothItem.AddItemOnBooth(new ItemUsage(true){UID=wing.UID,BoothCost=100,Action=ItemUsage.Mode.AddItemOnBoothForConquerPoints},p);
        Check(p.Booth.ItemList.Count==0 && p.Inventory.ContainsUID(wing.UID),"summer packages cannot enter the legacy non-atomic booth sale path");p.Booth=null;
        var stagedBooth=new Revora.Game.ConquerStructures.Booth();
        stagedBooth.ItemList.Add(wing.UID,new Revora.Game.ConquerStructures.BoothItem{Item=wing,Cost=100,Cost_Type=Revora.Game.ConquerStructures.BoothItem.CostType.ConquerPoints});
        Revora.Game.ConquerStructures.Booth.Booths2[998877]=stagedBooth;
        BoothItem.BuyFromBooth(new ItemUsage(true){UID=wing.UID,dwParam=998877},buyer);
        Check(buyer.Entity.ConquerPoints==1000 && !buyer.Inventory.ContainsUID(wing.UID),"stale or automated booth listings cannot charge for or regenerate a summer package");
        Revora.Game.ConquerStructures.Booth.Booths2.Remove(998877);
        TradeOpen(p,buyer);TradeSend(p,Trade.AddItem,wing.UID);TradeSend(p,Trade.AddItem,wing.UID);
        Check(p.Trade.Items.Count==1,"duplicate native item offers retain one physical UID");
        TradeSend(buyer,Trade.SetConquerPoints,100);TradeSend(buyer,Trade.SetMoney,1000);
        TradeSend(p,Trade.Accept,0);TradeSend(buyer,Trade.SetMoney,1001);
        Check(!p.Trade.InTrade && !buyer.Trade.InTrade && p.Inventory.ContainsUID(wing.UID) && buyer.Entity.ConquerPoints==1000,"changing an accepted offer closes both tables without settlement");
        TradeOpen(p,buyer);TradeSend(p,Trade.AddItem,wing.UID);TradeSend(buyer,Trade.SetConquerPoints,100);
        Query("ALTER TABLE revora_trade_receipts ADD CONSTRAINT trade_failure CHECK(left_cp=-1) NOT VALID");
        TradeSend(p,Trade.Accept,0);TradeSend(buyer,Trade.Accept,0);
        Check(Fenced(p) && Fenced(buyer) && p.Inventory.ContainsUID(wing.UID) && buyer.Entity.ConquerPoints==1000
            && Scalar("SELECT count(*) FROM revora_trade_receipts WHERE left_uid=@uid OR right_uid=@uid",p.Entity.UID)==0,"failed trade receipt rolls back payment and item transfer together");
        Query("ALTER TABLE revora_trade_receipts DROP CONSTRAINT trade_failure");DemonReset(p);DemonReset(buyer);
        TradeOpen(p,buyer);TradeSend(p,Trade.AddItem,wing.UID);TradeSend(buyer,Trade.SetConquerPoints,100);
        Query("ALTER TABLE entities ADD CONSTRAINT trade_payment_failure CHECK(conquerpoints<>900) NOT VALID");
        TradeSend(p,Trade.Accept,0);TradeSend(buyer,Trade.Accept,0);
        Check(Fenced(p) && Fenced(buyer) && Scalar("SELECT entityid FROM items WHERE uid=@uid",wing.UID)==p.Entity.UID
            && Scalar("SELECT count(*) FROM revora_trade_receipts WHERE left_uid=@uid OR right_uid=@uid",p.Entity.UID)==0,"failure after item movement rolls back the ownership update and exchange receipt");
        Query("ALTER TABLE entities DROP CONSTRAINT trade_payment_failure");DemonReset(p);DemonReset(buyer);
        TradeOpen(p,buyer);TradeSend(p,Trade.AddItem,wing.UID);wing.StackSize=2;TradeSend(p,Trade.Accept,0);TradeSend(buyer,Trade.Accept,0);wing.StackSize=1;
        Check(!p.Trade.InTrade && p.Inventory.ContainsUID(wing.UID) && !buyer.Inventory.ContainsUID(wing.UID),"mutated offered item cannot be settled from stale acceptance");
        var meteor=Pack(buyer,1088001,1);
        TradeOpen(p,buyer);TradeSend(p,Trade.AddItem,wing.UID);TradeSend(buyer,Trade.AddItem,meteor.UID);TradeSend(buyer,Trade.SetConquerPoints,100);TradeSend(buyer,Trade.SetMoney,1000);
        uint oldCp=p.Entity.ConquerPoints;ulong oldGold=p.Entity.Money;
        var left=Task.Run(()=>TradeSend(p,Trade.Accept,0));var right=Task.Run(()=>TradeSend(buyer,Trade.Accept,0));
        Check(Task.WaitAll(new[]{left,right},10000),"opposing accepts complete under stable owner-lock order");
        Check(!Fenced(p) && !Fenced(buyer) && !p.Inventory.ContainsUID(wing.UID) && buyer.Inventory.ContainsUID(wing.UID) && p.Inventory.ContainsUID(meteor.UID) && !buyer.Inventory.ContainsUID(meteor.UID)
            && buyer.Entity.ConquerPoints==900 && buyer.Entity.Money==9000 && p.Entity.ConquerPoints==oldCp+100 && p.Entity.Money==oldGold+1000,
            "ordinary native trade exchanges genuine wing and both currencies atomically");
        Check(Scalar("SELECT entityid FROM items WHERE uid=@uid",wing.UID)==buyer.Entity.UID
            && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",buyer.Entity.UID)==900
            && Scalar("SELECT money FROM entities WHERE uid=@uid",buyer.Entity.UID)==9000,"trade ownership and purses survive database reload");
        EntityTable.UpdateMoney(p);EntityTable.UpdateMoney(buyer);
        Check(!Fenced(p) && !Fenced(buyer),"settled trade purses advance their durable silver preimages for later saves");
        TradeSend(p,Trade.Accept,0);TradeSend(buyer,Trade.Accept,0);
        Check(Scalar("SELECT count(*) FROM revora_trade_receipts WHERE left_uid=@uid OR right_uid=@uid",p.Entity.UID)==1,"duplicate post-settlement packets cannot replay a trade");
        foreach(var item in buyer.Inventory.Objects)buyer.Inventory.Remove(item.UID,Enums.ItemUse.None,false);ConquerItemTable.LoadItems(buyer);
        ConquerItem received;buyer.Inventory.TryGetItem(wing.UID,out received);ItemHandler.UseItem(wing,p);
        Check(buyer.Inventory.ContainsUID(wing.UID),"previous owner cannot consume a transferred wing");
        ItemHandler.UseItem(received,buyer);
        Check(!buyer.Inventory.ContainsUID(wing.UID) && EligibleTitles(buyer).Contains(60136013),"ordinary buyer activates reloaded unbound wing using its original crafting provenance");
        foreach(int length in new[]{0,1,3,12,13,15,16,25,65536})Trade.Process(new byte[length],buyer);
        var invalid=new Trade(true){Type=Trade.Request,dwParam=p.Entity.UID}.ToArray();invalid[0]=11;Trade.Process(invalid,buyer);
        Check(!buyer.Trade.InTrade && !p.Trade.InTrade,"malformed trade lengths and headers cannot open a session");
        Query("UPDATE entities SET money=money+1 WHERE uid=@uid","uid",(long)buyer.Entity.UID);
        TradeOpen(p,buyer);TradeSend(buyer,Trade.SetMoney,1);TradeSend(p,Trade.Accept,0);TradeSend(buyer,Trade.Accept,0);
        Check(Fenced(p) && Fenced(buyer) && Scalar("SELECT money FROM entities WHERE uid=@uid",buyer.Entity.UID)==9001
            && Scalar("SELECT count(*) FROM revora_trade_receipts WHERE left_uid=@uid OR right_uid=@uid",p.Entity.UID)==1,
            "stale silver prevents settlement of either trade purse and preserves the external credit");
        ReloadSilver(p);ReloadSilver(buyer);
    }
    static void SummerBonusChecks(Map town) {
        summerNow=new DateTime(2026,8,2,12,0,0,DateTimeKind.Utc);
        while(((int)(summerNow.Date-new DateTime(2026,1,1)).TotalDays%3)!=0)summerNow=summerNow.AddDays(1);
        var p=SoulPlayer(town);SummerDay(p);SummerNpc(p,4);
        Check(SummerAmount(p,3303704)==6,"summer bonus requires the real rotating quest completion");
        p.BindWorldMap(1000);SoulNpc(p,301,1);
        foreach(uint npc in new uint[]{65822,65823,65824}){SoulNpc(p,npc,0);summerNow=summerNow.AddSeconds(3);Call(Souls,null,"Tick");}
        SoulNpc(p,301,3);SummerNpc(p,4);
        Check(SummerAmount(p,3303704)==8 && SummerAmount(p,3303705)==4 && HasAchievement(p,11012),"three genuine soul inspections and reward claim unlock the correct daily Summer bonus");
        MoonNear(p,900000063);Parallel.For(0,12,i=>Npcs.GetDialog(new NpcRequest(true){NpcID=900000063,OptionID=4},p));
        Check(SummerAmount(p,3303704)==8 && Scalar("SELECT count(*) FROM revora_summer_daily WHERE uid=@uid AND bonus_claimed",p.Entity.UID)==1,"concurrent bonus claims cannot repeat extra fragments");
        var paid=SoulPlayer(town);SummerDay(paid);paid.BindWorldMap(1000);SoulNpc(paid,301,2);SoulNpc(paid,301,4);SummerNpc(paid,4);
        Check(SummerAmount(paid,3303704)==6 && Scalar("SELECT count(*) FROM revora_soul_runs WHERE uid=@uid AND state='paid'",paid.Entity.UID)==1,"paid soul assistance cannot buy the earned Summer bonus");
    }
    static void VerifySummer(string runtime,Map town,bool tradeOnly=false) {
        Query("DROP TABLE entities;CREATE TABLE entities AS TABLE public.entities WITH NO DATA");
        Query("ALTER TABLE entities ALTER COLUMN conquerpoints SET DEFAULT 0,ALTER COLUMN money SET DEFAULT 0,ALTER COLUMN revorapoint SET DEFAULT 0,ALTER COLUMN studypoints SET DEFAULT 0");
        Query("CREATE TABLE titles (LIKE public.titles INCLUDING ALL)");Query("CREATE TABLE itemadding (LIKE public.itemadding INCLUDING ALL)");Query("CREATE TABLE quests (LIKE public.quests INCLUDING ALL)");
        Query("CREATE TABLE npcs (LIKE public.npcs INCLUDING ALL)");Query("INSERT INTO npcs SELECT * FROM public.npcs WHERE id IN (2000,10062,15140) OR id BETWEEN 65822 AND 65840");
        DataHolder.ReadStats();RunesSystem.LoadRunesEXP();StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);ConquerItem.ItemUID=new Counter(6700000);
        World(runtime,1015);World(runtime,1001);World(runtime,1000);Server.GamePool=new GameState[0];
        summerNow=new DateTime(2026,8,2,12,0,0,DateTimeKind.Utc);Summer.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>summerNow));
        SummerCraft.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>summerRoll));Demon.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>summerNow));
        Souls.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>summerNow));Call(Demon,null,"Initialize");Call(Souls,null,"Initialize");Call(Souls,null,"PlaceNpcs");
        // The bonus reads only this already-verified event receipt shape here.
        Query("CREATE TABLE revora_power_claims(uid bigint,window_start timestamptz,stage integer,choice varchar(16),reward_uid bigint)");
        Call(Summer,null,"Initialize");Call(Summer,null,"PlaceNpc");
        if(tradeOnly){var trader=MoonPlayer(town);for(int day=0;day<5;day++){SummerDay(trader);summerNow=summerNow.AddDays(1);}SummerTradeChecks(trader,town);return;}
        Check(town.Npcs.ContainsKey(900000063) && Kernel.GetDistance(460,281,town.Npcs[900000063].X,town.Npcs[900000063].Y)<=8,"summer artisan is reachable by ordinary players on a walkable Twin City tile");
        Check(ConquerItemInformation.BaseInformations[3303772].StackSize==1 && ConquerItemInformation.BaseInformations[3303765].StackSize==1,"wing packages and crafted tools retain individual item identities");
        Check((string)Call(Summer,null,"Day",new DateTime(2026,9,15,6,59,59,DateTimeKind.Utc))=="20260914"
            && (DateTime)Call(Summer,null,"Midnight",new DateTime(2026,12,1,12,0,0,DateTimeKind.Utc),0)==new DateTime(2026,12,1,8,0,0,DateTimeKind.Utc),"daily calendar boundaries follow Pacific summer and winter time");
        var p=MoonPlayer(town);p.Entity.Level=119;SummerNpc(p,1);Check(Scalar("SELECT count(*) FROM revora_summer_daily WHERE uid=@uid",p.Entity.UID)==0,"under-level player cannot start the far-ground hunt");p.Entity.Level=120;
        p.Fake=true;SummerNpc(p,1);p.Fake=false;p.ActiveNpc=900000063;p.Entity.X=1;p.Entity.Y=1;Npcs.GetDialog(new NpcRequest(true){NpcID=900000063,OptionID=1},p);
        Check(Scalar("SELECT count(*) FROM revora_summer_seasons WHERE uid=@uid",p.Entity.UID)==0,"fake and remote enrollment cannot create a season");
        SummerNpc(p,1);MoonNear(p,900000063);Parallel.For(0,12,i=>Npcs.GetDialog(new NpcRequest(true){NpcID=900000063,OptionID=1},p));
        Check(Scalar("SELECT count(*) FROM revora_summer_seasons WHERE uid=@uid",p.Entity.UID)==1 && Scalar("SELECT count(*) FROM revora_summer_daily WHERE uid=@uid",p.Entity.UID)==1,"concurrent enrollment creates one personal season and one daily hunt");
        SummerNpc(p,3);Check(p.Inventory.Count==0,"uncompleted hunt cannot issue materials");
        var mob=SummerMob(p);Parallel.For(0,12,i=>mob.Die(p.Entity));Check(SummerKills(p)==1,"one actual monster life gives one kill despite simultaneous death callbacks");p.Map.Entities.Remove(mob.UID);
        p.Fake=true;SummerKill(p);p.Fake=false;Check(SummerKills(p)==1,"ambient fake characters cannot create summer progress");
        Map huntMap;uint huntType;SummerGround(p,out huntMap,out huntType);var remote=DemonMob(huntMap,huntType);p.Entity.X=1;p.Entity.Y=1;remote.Die(p.Entity);
        Check(SummerKills(p)==1,"distant kill attribution is rejected");
        var wrongMap=Kernel.Maps[huntMap.ID==1015?(ushort)1001:(ushort)1015];DemonMove(p,wrongMap);DemonMob(wrongMap,huntType).Die(p.Entity);
        Check(SummerKills(p)==1,"matching monster identity on the wrong map does not progress the hunt");
        Query("ALTER TABLE revora_summer_daily ADD CONSTRAINT summer_kill_failure CHECK(kills<=1) NOT VALID");SummerKill(p);
        Check(Fenced(p) && SummerKills(p)==1,"failed hunt counter preserves previous credit and fences the player");Query("ALTER TABLE revora_summer_daily DROP CONSTRAINT summer_kill_failure");DemonReset(p);
        Call(Summer,null,"Initialize");Call(Summer,null,"OnLogin",p);for(int i=1;i<120;i++)SummerKill(p);SummerKill(p);
        Check(SummerKills(p)==120,"relogin resumes the saved hunt and the daily count stops at 120");
        var full=Enumerable.Range(0,40).Select(i=>Pack(p,1088001,1)).ToArray();SummerNpc(p,3);Check(p.Inventory.Count==40,"full bag preserves an unclaimed material reward");
        foreach(var item in full.Take(4)){Query("DELETE FROM items WHERE uid=@id","id",(long)item.UID);p.Inventory.Remove(item.UID,Enums.ItemUse.None,false);}
        SummerNpc(p,3);Check(p.Inventory.Count==36 && !p.Inventory.Objects.Any(i=>i.ID==4030101)
            && Scalar("SELECT count(*) FROM revora_summer_daily WHERE uid=@uid AND claimed",p.Entity.UID)==0,
            "four free slots cannot partially claim four material stacks plus the rune");
        foreach(var item in full){Query("DELETE FROM items WHERE uid=@id","id",(long)item.UID);p.Inventory.Remove(item.UID,Enums.ItemUse.None,false);}
        Query("ALTER TABLE revora_summer_daily ADD CONSTRAINT summer_claim_failure CHECK(NOT claimed) NOT VALID");SummerNpc(p,3);
        Check(Fenced(p) && p.Inventory.Count==0 && Scalar("SELECT count(*) FROM items WHERE entityid=@uid",p.Entity.UID)==0,"failed material receipt rolls back all four bundles");Query("ALTER TABLE revora_summer_daily DROP CONSTRAINT summer_claim_failure");DemonReset(p);
        MoonNear(p,900000063);Parallel.For(0,12,i=>Npcs.GetDialog(new NpcRequest(true){NpcID=900000063,OptionID=3},p));
        Check(SummerAmount(p,3303704)==6 && SummerAmount(p,3303705)==3 && SummerAmount(p,3303706)==1 && SummerAmount(p,3303707)==1,"concurrent final claims issue the four exact bound material quantities once");
        StoreSummerRune(p);
        Check(p.Inventory.Objects.All(i=>i.Bound) && p.Inventory.Count==4,"material bundles use four physical bound stacks");
        SummerNpc(p,220);SummerNpc(p,11);SummerNpc(p,220);Check(p.Inventory.Count==4 && SummerAmount(p,3303704)==6,"unconfirmed or underfunded craft consumes no materials");
        // The complete minimum route uses genuine death callbacks and daily claims,
        // with no GM balance assignment, bonus receipt or fabricated completion.
        for(int day=1;day<33;day++){summerNow=summerNow.AddDays(1);SummerDay(p);}
        Check(SummerKills(p)==3960 && Scalar("SELECT count(*) FROM revora_summer_daily WHERE uid=@uid AND claimed",p.Entity.UID)==33,"ordinary player completes all 33 daily hunts through 3960 real owned-monster death callbacks");
        Check(SummerAmount(p,3303704)==198 && SummerAmount(p,3303705)==99 && SummerAmount(p,3303706)==33 && SummerAmount(p,3303707)==33 && p.Inventory.Count==4,"base route supplies the full published collection budget without optional bonuses");
        var glowMaterial=p.Inventory.Objects.Single(i=>i.ID==3303704);glowMaterial.Lock=1;SummerNpc(p,11);SummerNpc(p,220);
        Check(SummerAmount(p,3303704)==198,"locked material stacks cannot be spent");glowMaterial.Lock=0;
        Query("UPDATE items SET stacksize=197 WHERE uid=@item","item",(long)glowMaterial.UID);SummerNpc(p,11);SummerNpc(p,220);
        Check(Fenced(p) && p.Inventory.Count==4 && Scalar("SELECT count(*) FROM revora_summer_crafts WHERE maker=@uid",p.Entity.UID)==0,"stale material quantity rolls back the entire craft");Query("UPDATE items SET stacksize=198 WHERE uid=@item","item",(long)glowMaterial.UID);DemonReset(p);
        SummerNpc(p,11);summerNow=summerNow.AddMinutes(2);SummerNpc(p,220);Check(SummerAmount(p,3303704)==198,"expired craft confirmation cannot spend fragments");
        Query("ALTER TABLE revora_summer_crafts ADD CONSTRAINT summer_craft_failure CHECK(kind=4) NOT VALID");SummerNpc(p,11);SummerNpc(p,220);
        Check(Fenced(p) && SummerAmount(p,3303704)==198 && p.Inventory.Count==4,"failed output receipt rolls back the material debit and new package");Query("ALTER TABLE revora_summer_crafts DROP CONSTRAINT summer_craft_failure");DemonReset(p);
        SummerNpc(p,11);MoonNear(p,900000063);Parallel.For(0,12,i=>Npcs.GetDialog(new NpcRequest(true){NpcID=900000063,OptionID=220},p));
        var green=p.Inventory.Objects.Single(i=>i.ID==3303772);
        Check(SummerAmount(p,3303704)==168 && green.Bound && green.StackSize==1 && Scalar("SELECT count(*) FROM revora_summer_crafts WHERE maker=@uid",p.Entity.UID)==1,"concurrent random-color confirmation makes one recorded bound package");
        Call(Summer,null,"Initialize");SummerNpc(p,220);Check(p.Inventory.Objects.Count(i=>i.ID==3303772)==1,"restart does not repeat a committed craft or reroll its color");
        Query("ALTER TABLE revora_summer_operations ADD CONSTRAINT summer_activation_failure CHECK(action<>'activate') NOT VALID");ItemHandler.UseItem(green,p);
        Check(Fenced(p) && p.Inventory.ContainsUID(green.UID) && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND title_key=60136013",p.Entity.UID)==0,"failed activation rolls back both item consumption and permanent award");Query("ALTER TABLE revora_summer_operations DROP CONSTRAINT summer_activation_failure");DemonReset(p);
        ItemHandler.UseItem(green,p);Check(!p.Inventory.ContainsUID(green.UID) && EligibleTitles(p).Contains(60136013),"ordinary item activation consumes the package and immediately enables its wardrobe design");
        ItemHandler.UseItem(green,p);Check(Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND title_key=60136013",p.Entity.UID)==1,"replayed item activation cannot duplicate an award");
        var duplicates=Enumerable.Range(0,3).Select(i=>SummerMake(p,1)).ToArray();ItemHandler.UseItem(duplicates[0],p);
        Check(duplicates.All(i=>i!=null && i.ID==3303772 && p.Inventory.ContainsUID(i.UID)),"duplicate random colors remain real packages and duplicate activation preserves them");
        for(int color=1;color<4;color++) {
            var wand=SummerMake(p,3);Check(wand!=null && wand.ID==3303764,"earned Rainbow fragments craft a genuine dye wand");
            SummerDye(p,duplicates[color-1],color);ItemHandler.UseItem(duplicates[color-1],p);
        }
        Check(new[]{60136013,60146014,60156015,60166016}.All(EligibleTitles(p).Contains),"three earned wands resolve the worst duplicate-color draw into all four permanent Glow designs");
        var siren=SummerMake(p,2);var solution=SummerMake(p,4);summerRoll=5;SummerUnbind(p,siren);
        Check(siren.ID==3303776 && siren.Bound && !p.Inventory.ContainsUID(solution.UID),"failed one-in-six Siren unbinding spends only its solution and records the unchanged package");
        long operations=Scalar("SELECT count(*) FROM revora_summer_operations WHERE uid=@uid",p.Entity.UID);SummerNpc(p,220);
        Check(Scalar("SELECT count(*) FROM revora_summer_operations WHERE uid=@uid",p.Entity.UID)==operations,"replaying a failed unbind cannot roll again");
        solution=SummerMake(p,4);summerRoll=0;SummerUnbind(p,siren);
        Check(siren.ID==3303801 && !siren.Bound && !p.Inventory.ContainsUID(solution.UID),"successful Siren unbinding changes the same physical item to its real unbound identity");
        ItemHandler.UseItem(siren,p);
        Check(new[]{60136013,60146014,60156015,60166016,60176017}.All(EligibleTitles(p).Contains),"the genuine 33-day base route earns all five permanent wardrobe designs");
        p.Entity.MyAchievement=new Achievement(p.Entity);p.Entity.MyAchievement.Load("");p.Entity.MyAchievement.Send();RefreshTitles(p);
        Check(new[]{60136013,60146014,60156015,60166016,60176017}.All(EligibleTitles(p).Contains),"all five designs survive ordinary achievement and wardrobe reconciliation after reconnect");
        foreach(var item in p.Inventory.Objects)p.Inventory.Remove(item.UID,Enums.ItemUse.None,false);ConquerItemTable.LoadItems(p);
        Check(SummerAmount(p,3303704)==78 && SummerAmount(p,3303705)==0 && SummerAmount(p,3303706)==3 && SummerAmount(p,3303707)==23,"ordinary inventory reload retains exactly the unspent material budget");
        var unauthored=Pack(p,3303772,1);unauthored.Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@item","item",(long)unauthored.UID);ItemHandler.UseItem(unauthored,p);
        Check(p.Inventory.ContainsUID(unauthored.UID),"an item without crafting provenance cannot bypass activation evidence");
        SummerTradeChecks(p,town);SummerBonusChecks(town);
        var legacy=MoonPlayer(town);SummerNpc(legacy,1);
        Query("UPDATE revora_summer_daily SET valuable_reward=false WHERE uid=@uid","uid",(long)legacy.Entity.UID);
        for(int n=0;n<120;n++)SummerKill(legacy);SummerNpc(legacy,3);SummerNpc(legacy,3);
        Check(legacy.Inventory.Count==4 && legacy.Inventory.Objects.All(i=>i.Bound) && SummerAmount(legacy,3303704)==6
            && Scalar("SELECT count(*) FROM revora_summer_daily WHERE uid=@uid AND claimed AND NOT valuable_reward AND cardinality(reward_ids)=4",legacy.Entity.UID)==1,
            "old daily contract earns its original fragment bundle once without a retroactive rune");
        var equipped=StoredProgressionPlayer(town);equipped.Entity.Reborn=2;
        if(!EntityTable.SaveEntity(equipped))throw new Exception("Summer rune profile save failed.");LoadProgressionImage(equipped);
        SummerDay(equipped);var earned=equipped.RunesItem.Values.Single(i=>i.ID==4030101);
        RuneMove(equipped,earned,57,103);
        Check(equipped.Account.State==AccountTable.AccountState.Player && !Fenced(equipped) && earned.Position==103
            && SavedRune(earned,"position")==103 && HasAchievement(equipped,11310),
            "eligible ordinary hunter equips the earned rune and saves its position with achievement evidence");
        foreach(string table in new[]{"teamarena","championarena","status","reward","rewardclameid","merchant","dialyquests","activeness","house","pkexploits","achievement"})
            Query("CREATE TABLE IF NOT EXISTS "+table+" AS TABLE public."+table+" WITH NO DATA");
        Query("CREATE TABLE IF NOT EXISTS prizes(uid bigint PRIMARY KEY,name text,prizes bytea)");
        var earnedUid=earned.UID;equipped=ReloadBall(equipped);equipped.LoadItemStats();
        Check(equipped.RunesItem.ContainsKey(earnedUid) && equipped.RunesItem[earnedUid].Position==103
            && !equipped.RunesItem[earnedUid].Bound && !Fenced(equipped),
            "fresh character and item loaders restore the equipped earned rune with its unbound identity");
        SummerNpc(equipped,3);
        Check(equipped.RunesItem.Count==1 && !equipped.Inventory.Objects.Any(i=>i.ID==4030101)
            && Scalar("SELECT count(*) FROM revora_summer_daily WHERE uid=@uid AND claimed",equipped.Entity.UID)==1,
            "reconnected hunter cannot repeat the saved daily rune claim");
        var seasonEnd=(DateTime)Call(Summer,null,"Midnight",new DateTime(2026,8,2,12,0,0,DateTimeKind.Utc),60);summerNow=seasonEnd;SummerNpc(p,1);
        Check(Scalar("SELECT count(*) FROM revora_summer_seasons WHERE uid=@uid",p.Entity.UID)==2 && SummerAmount(p,3303704)==48,"a new season starts at the old end boundary while earned materials remain intact");
    }
}
