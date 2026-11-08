using System;
using System.IO;
using System.Linq;
using Npgsql;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static class EquipmentSetEarningRunner {
    static int Main(){return PotencyHuntTests.EquipmentSetEarningMain();}
}
internal static partial class PotencyHuntTests {
    internal static int EquipmentSetEarningMain() {
        var b=new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("REVORA_QUARANTINE_TEST_CONNECTION"));
        Environment.SetEnvironmentVariable("REVORA_QUARANTINE_TEST_CONNECTION",null);
        if(b.Host!="127.0.0.1" || b.Port!=25439 || b.Database!="revora_candidate_game")throw new InvalidOperationException("private candidate required");
        string runtime=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(Entity).Assembly.Location),"../../../bin/Debug"));
        string fixture=Path.GetFullPath(Path.Combine(runtime,"../../obj/equipment-earning-fixture"));
        string previous=Environment.CurrentDirectory;
        DataHolder.ConnectionString=b.ConnectionString;Environment.CurrentDirectory=runtime;
        ConquerItemInformation.Load();MonsterInformation.Load();InnerPowerTable.LoadDBInformation();
        Query("CREATE SCHEMA equipment_earning_fixture");
        try {
            b.SearchPath="equipment_earning_fixture";DataHolder.ConnectionString=b.ConnectionString;
            Query("CREATE TABLE entities(uid bigint PRIMARY KEY,name text NOT NULL,unionexploits bigint NOT NULL DEFAULT 0);CREATE TABLE items(LIKE public.items INCLUDING ALL);CREATE TABLE skills AS TABLE public.skills WITH NO DATA;CREATE TABLE arena AS TABLE public.arena WITH NO DATA;CREATE TABLE monsterspawns(LIKE public.monsterspawns INCLUDING ALL)");
            Call(Packs,null,"Initialize");
            Call(FirstRite,null,"Initialize");
            Call(SecondRite,null,"Initialize");
            Call(PaidRite,null,"Initialize");
            Call(AssassinTrainer,null,"Initialize");
            Call(NinjaTrainer,null,"Initialize");
            Call(TokenShop,null,"Initialize");
            Call(ExpItems,null,"Initialize");
            Call(ExpPotions,null,"Initialize");
            Call(QuestGifts,null,"Initialize");
            Call(PrizePacks,null,"Initialize");
            Call(Magnolia,null,"Initialize");
            Call(PriceQuest,null,"Initialize");
            Call(RareQuest,null,"Initialize");
            Call(SoulBall,null,"Initialize");
            Call(Anniversary,null,"Initialize");
            Call(ExorcismGifts,null,"Initialize");
            Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.ExorcismQuest"),null,"Initialize");
            Call(EvidenceType,null,"Initialize");
            Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.MentorDepartures"),null,"Initialize");
            Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.MentorStoneClaims"),null,"Initialize");
            Call(typeof(Server).Assembly.GetType("Revora.Game.Features.MentorBlessingClaims"),null,"Initialize");
            typeof(CompanionTests).GetMethod("Reset",Hidden).Invoke(null,null);
            Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.Tournaments.LocalTeamPkLedger"),null,"Initialize");
            World(runtime,1217);var town=World(runtime,1002);
            VerifyRuneAcquisition(runtime,town,fixture,true,true);
            System.Console.WriteLine("Equipment earning checks passed: "+checks);return 0;
        } finally {
            Environment.CurrentDirectory=previous;NpgsqlConnection.ClearAllPools();b.SearchPath="public";DataHolder.ConnectionString=b.ConnectionString;
            Query("DROP SCHEMA equipment_earning_fixture CASCADE");DataHolder.ConnectionString=null;
        }
    }
    static byte[] BlessingFrame(uint target,params uint[] materials) {
        var frame=DedicatedSocketFrame(target,materials);Revora.Network.Writer.WriteUInt16(40,20,frame);return frame;
    }
    static void VerifyEarnedEquipmentSets(GameState p,string runtime) {
        string previous=Environment.CurrentDirectory;
        try {
            Environment.CurrentDirectory=runtime;ShopFile.Load();EShopFile.Load();EShopV2File.Load();
            Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.WeaponBookShop"),null,"Initialize");
            var upgrades=typeof(Entity).Assembly.GetType("Revora.Game.Features.EquipmentUpgrades");Call(upgrades,null,"Initialize");
            Check(p.Account.State==AccountTable.AccountState.Player && p.Entity.ConquerPoints==1000000 && p.Entity.Money==10000000 && p.Inventory.Count==0,
                "ordinary boss earner starts equipment route with exact saved reward and empty bag");
            foreach(int coreSlot in new[]{1,2,3,4,6,8}) {
                var choice=ShopFile.Shops.Values.Where(shop=>shop.MoneyType==ShopFile.MoneyType.Gold && shop.Items!=null)
                    .SelectMany(shop=>shop.Items.Where(id=>ConquerItemInformation.BaseInformations.ContainsKey(id) && (int)ItemHandler.GetPositionFromID(id)==coreSlot && (coreSlot!=4 || id/1000==410) && (coreSlot!=1 || id/1000==118) && (coreSlot!=3 || id/1000==130) && ItemHandler.Equipable(new ConquerItem(true){ID=id},p))
                        .Select(id=>new {Shop=shop.UID,Type=id})).OrderBy(v=>v.Type).First();
                System.Console.WriteLine("Equipment route shop="+choice.Shop+" type="+choice.Type+" playerLevel="+p.Entity.Level+" body="+p.Entity.Body);
                p.Entity.X=300;p.Entity.Y=300;p.ActiveNpc=choice.Shop;
                p.Map.Npcs[choice.Shop]=new NpcSpawn{UID=choice.Shop,MapID=1002,X=300,Y=300,Type=Enums.NpcType.Talker};
                var before=p.Inventory.Objects.Select(i=>i.UID).ToArray();ulong money=p.Entity.Money;
                ItemHandler.HandleBuyFromNPC(BuyBook(choice.Type,1,choice.Shop),p);
                var added=p.Inventory.Objects.Where(i=>!before.Contains(i.UID)).ToArray();
                Check(!Fenced(p) && added.Length==1 && added[0].ID==choice.Type && p.Entity.Money<money,
                    "ordinary purchase supplies core slot "+coreSlot);
                uint slot=(uint)ItemHandler.GetPositionFromID(choice.Type);
                ItemHandler.EquipItem(new ItemUsage(true){UID=added[0].UID,dwParam=slot,Action=ItemUsage.Mode.EquipItem},p);
                Check(p.Equipment.TryGetItem(added[0].UID)!=null && ConquerItemTable.LoadItem(added[0].UID).Position==slot,
                    "purchased core equipment saves its ordinary equip position "+slot+" memory="+added[0].Position+" saved="+ConquerItemTable.LoadItem(added[0].UID).Position+" equipment="+(p.Equipment.TryGetItem(added[0].UID)!=null)+" bag="+p.Inventory.ContainsUID(added[0].UID)+" fenced="+Fenced(p));
            }
            var clock=upgrades.GetField("Clock",Hidden);var roll=upgrades.GetField("Roll",Hidden);
            object oldClock=clock.GetValue(null),oldRoll=roll.GetValue(null);DateTime now=DateTime.UtcNow;
            clock.SetValue(null,(Func<DateTime>)(()=>now));roll.SetValue(null,(Func<int,int>)(maximum=>0));
            uint initialCp=p.Entity.ConquerPoints;int balls=0;
            try {
                Check(EShopFile.Shops[2888].Items.Contains(1088000),"loaded mall supplies DragonBalls for the quality route");
                foreach(uint quality in new uint[]{6,7,8,9}) {
                    var targets=p.Equipment.Objects.Where(i=>i!=null && new[]{1,2,3,4,6,8}.Contains((int)i.Position)).ToArray();
                    foreach(var target in targets) {
                        if(target.ID%10>=quality)continue;
                        uint cp=p.Entity.ConquerPoints;var before=p.Inventory.Objects.Select(i=>i.UID).ToArray();
                        ItemHandler.HandleBuyFromNPC(BuyBook(1088000,1,2888),p);
                        var material=p.Inventory.Objects.Single(i=>!before.Contains(i.UID));
                        Check(material.ID==1088000 && p.Entity.ConquerPoints==cp-ConquerItemInformation.BaseInformations[1088000].ConquerPointsWorth,
                            "boss-earned CPs buy one real quality-upgrade material");
                        var frame=UpgradeComposeFrame(target.UID,new[]{material.UID},7,1);var request=new Compose(false);request.Deserialize(frame);
                        now=now.AddSeconds(2);ItemHandler.ComposePlus(request,p);ItemHandler.ComposePlus(request,p);balls++;
                        Check(!Fenced(p) && target.ID%10==quality && !p.Inventory.ContainsUID(material.UID)
                            && ConquerItemTable.LoadItem(target.UID).ID==target.ID,
                            "ordinary equipped quality upgrade consumes its purchased material exactly once");
                    }
                    Call(typeof(Achievement),p.Entity.MyAchievement,"Refresh",DateTime.UtcNow,true);
                    foreach(int achievement in new[]{10410,10411,10412})
                        Check(HasAchievement(p,achievement)==(quality>=(uint)(achievement-10403)),"full-set quality milestone "+achievement+" at quality "+quality);
                }
                Check(p.Entity.ConquerPoints==initialCp-(uint)balls*ConquerItemInformation.BaseInformations[1088000].ConquerPointsWorth
                    && Scalar("SELECT count(*) FROM revora_equipment_upgrades WHERE uid=@uid",p.Entity.UID)==balls,
                    "complete quality route retains exact earned currency debits and one receipt per upgrade");
            } finally {clock.SetValue(null,oldClock);roll.SetValue(null,oldRoll);}
            Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.EquipmentSockets"),null,"Initialize");
            var socketTargets=p.Equipment.Objects.Where(i=>i!=null && new[]{1,2,3,4,6,8}.Contains((int)i.Position)).ToArray();
            int socketCount=0;
            foreach(var target in socketTargets) {
                int need=target.Position==4?1:12;var materials=new uint[need];
                for(int n=0;n<need;n++) {
                    var before=p.Inventory.Objects.Select(i=>i.UID).ToArray();uint cp=p.Entity.ConquerPoints;
                    ItemHandler.HandleBuyFromNPC(BuyBook(1088000,1,2888),p);
                    var material=p.Inventory.Objects.Single(i=>!before.Contains(i.UID));materials[n]=material.UID;
                    Check(material.ID==1088000 && p.Entity.ConquerPoints==cp-ConquerItemInformation.BaseInformations[1088000].ConquerPointsWorth,
                        "socket material is purchased from the earned purse");
                }
                var request=DedicatedSocketFrame(target.UID,materials);
                Revora.Network.PacketHandler.HandlePacket(request,p);Revora.Network.PacketHandler.HandlePacket(request,p);socketCount++;
                Check(!Fenced(p) && target.SocketOne==Enums.Gem.EmptySocket && materials.All(uid=>!p.Inventory.ContainsUID(uid))
                    && ConquerItemTable.LoadItem(target.UID).SocketOne==Enums.Gem.EmptySocket,
                    "ordinary socket request commits purchased materials once for slot "+target.Position);
                Call(typeof(Achievement),p.Entity.MyAchievement,"Refresh",DateTime.UtcNow,true);
                Check(HasAchievement(p,10415)==(socketCount==6),"socket set only completes after the sixth saved piece");
            }
            Check(Scalar("SELECT count(*) FROM revora_equipment_sockets WHERE uid=@uid",p.Entity.UID)==6,
                "socket replays create six receipts, not twelve");
            Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.ItemEnchantment"),null,"Initialize");
            uint gemType=EShopFile.Shops[2888].Items.Where(id=>id>=700001 && id<=700123 && (id%10==1 || id==700012 || id==700013)
                && ConquerItemInformation.BaseInformations.ContainsKey(id) && ConquerItemInformation.BaseInformations[id].ConquerPointsWorth>0
                && Enum.IsDefined(typeof(Enums.Gem),(byte)(id%1000)))
                .OrderBy(id=>ConquerItemInformation.BaseInformations[id].ConquerPointsWorth).ThenBy(id=>id).First();
            int enchantCount=0;
            foreach(var target in socketTargets) {
                byte slot=target.Position;
                ItemHandler.UnequipItem(new ItemUsage(true){UID=target.UID,dwParam=slot,Action=ItemUsage.Mode.UnequipItem},p);
                Check(p.Inventory.ContainsUID(target.UID) && ConquerItemTable.LoadItem(target.UID).Position==0,"ordinary unequip prepares enchantment");
                uint cp=p.Entity.ConquerPoints;var before=p.Inventory.Objects.Select(i=>i.UID).ToArray();
                ItemHandler.HandleBuyFromNPC(BuyBook(gemType,1,2888),p);
                var gem=p.Inventory.Objects.Single(i=>!before.Contains(i.UID));
                Check(gem.ID==gemType && p.Entity.ConquerPoints==cp-ConquerItemInformation.BaseInformations[gemType].ConquerPointsWorth,
                    "enchantment gem is bought using earned CPs");
                var request=new ItemUsage(true){UID=target.UID,dwParam=gem.UID,Action=ItemUsage.Mode.Enchant};
                ItemHandler.EnchantItem(request,p);ItemHandler.EnchantItem(request,p);
                Check(!Fenced(p) && target.Enchant>0 && !p.Inventory.ContainsUID(gem.UID) && ConquerItemTable.LoadItem(target.UID).Enchant==target.Enchant,
                    "ordinary enchantment spends the purchased gem once");
                ItemHandler.EquipItem(new ItemUsage(true){UID=target.UID,dwParam=slot,Action=ItemUsage.Mode.EquipItem},p);enchantCount++;
                Call(typeof(Achievement),p.Entity.MyAchievement,"Refresh",DateTime.UtcNow,true);
                Check(HasAchievement(p,10416)==(enchantCount==6),"enchanted-set milestone waits for sixth re-equipped piece");
            }
            Check(Scalar("SELECT count(*) FROM revora_item_enchantments WHERE uid=@uid",p.Entity.UID)==6,"enchantment replay cannot create duplicate payment receipts");
            Call(upgrades,null,"InitializePlus");
            Check(EShopFile.Shops[2888].Items.Contains(730006),"existing mall supplies +6 stones for ordinary composition");
            var stoneShop=typeof(Entity).Assembly.GetType("Revora.Game.Features.ExemptionTokenShop");
            var shopClock=stoneShop.GetField("Clock",Hidden);object originalShopClock=shopClock.GetValue(null);
            DateTime buyTime=DateTime.UtcNow;shopClock.SetValue(null,(Func<DateTime>)(()=>buyTime));
            uint plusCost=0;int plusCount=0;
            try {
                foreach(var target in socketTargets) {
                    var before=p.Inventory.Objects.Select(i=>i.UID).ToArray();uint cp=p.Entity.ConquerPoints;
                    buyTime=buyTime.AddSeconds(2);var purchase=BuyBook(730006,33,2888);
                    ItemHandler.HandleBuyFromNPC(purchase,p);ItemHandler.HandleBuyFromNPC(purchase,p);
                    var stones=p.Inventory.Objects.Where(i=>!before.Contains(i.UID)).ToArray();
                    uint price=33u*ConquerItemInformation.BaseInformations[730006].ConquerPointsWorth;
                    Check(!Fenced(p) && stones.Length==33 && stones.All(i=>i.ID==730006 && i.Plus==6 && i.StackSize==1)
                        && p.Entity.ConquerPoints==cp-price,"ordinary bulk purchase and replay supplies33 initialized +6 stones for one payment");
                    int length=12+4*stones.Length;var frame=new byte[length+8];
                    Revora.Network.Writer.WriteUInt16((ushort)length,0,frame);Revora.Network.Writer.WriteUInt16(2036,2,frame);
                    frame[4]=Compose.Plus;frame[5]=(byte)(stones.Length+1);Revora.Network.Writer.WriteUInt32(target.UID,8,frame);
                    for(int n=0;n<stones.Length;n++)Revora.Network.Writer.WriteUInt32(stones[n].UID,12+4*n,frame);
                    Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,length,frame);
                    var request=new Compose(false);request.Deserialize(frame);ItemHandler.ComposePlus(request,p);ItemHandler.ComposePlus(request,p);
                    var saved=ConquerItemTable.LoadItem(target.UID);
                    Check(!Fenced(p) && stones.All(i=>!p.Inventory.ContainsUID(i.UID)) && saved.Plus==12 && target.Plus==12
                        && saved.PlusProgress==0 && target.PlusProgress==0,"33 purchased stones reach saved +12 exactly once for core slot "+target.Position);
                    plusCount++;plusCost+=price;Call(typeof(Achievement),p.Entity.MyAchievement,"Refresh",DateTime.UtcNow,true);
                    Check(HasAchievement(p,10413)==(plusCount==6),"full +12 milestone waits for sixth upgraded piece");
                }
                Check(Scalar("SELECT count(*) FROM revora_plus_compositions WHERE uid=@uid AND cardinality(materials)=33",p.Entity.UID)==6
                    && Scalar("SELECT count(*) FROM revora_exemption_purchases WHERE uid=@uid AND item_type=730006 AND quantity=33",p.Entity.UID)==6,
                    "198 paid stones produce six purchases and six compositions despite replay");
            } finally {shopClock.SetValue(null,originalShopClock);}
            uint finishingCost=0;
            var socketService=typeof(Entity).Assembly.GetType("Revora.Game.Features.EquipmentSockets");
            var socketRoll=socketService.GetField("Roll",Hidden);var socketClock=socketService.GetField("Clock",Hidden);
            object originalSocketRoll=socketRoll.GetValue(null),originalSocketClock=socketClock.GetValue(null);
            DateTime socketTime=DateTime.UtcNow.AddMinutes(1);
            socketRoll.SetValue(null,(Func<int,int>)(max=>max-1));socketClock.SetValue(null,(Func<DateTime>)(()=>socketTime));
            try {
                foreach(var target in socketTargets) {
                    uint materialType=target.Position==4?1088000u:1200005u;int count=target.Position==4?5:1;
                    Check(EShopFile.Shops[2888].Items.Contains(materialType),"existing mall supplies the second-socket material");
                    var prior=p.Inventory.Objects.Select(i=>i.UID).ToArray();uint cp=p.Entity.ConquerPoints;
                    ItemHandler.HandleBuyFromNPC(BuyBook(materialType,(uint)count,2888),p);
                    var materials=p.Inventory.Objects.Where(i=>!prior.Contains(i.UID)).ToArray();uint cost=(uint)count*ConquerItemInformation.BaseInformations[materialType].ConquerPointsWorth;
                    Check(materials.Length==count && p.Entity.ConquerPoints==cp-cost,"earned purse pays exact second-socket cost");finishingCost+=cost;
                    socketTime=socketTime.AddSeconds(2);var request=DedicatedSocketFrame(target.UID,materials.Select(i=>i.UID).ToArray());
                    Revora.Network.PacketHandler.HandlePacket(request,p);Revora.Network.PacketHandler.HandlePacket(request,p);
                    Check(!Fenced(p) && target.SocketTwo==Enums.Gem.EmptySocket && materials.All(i=>!p.Inventory.ContainsUID(i.UID)),"second socket consumes its purchased materials once");
                }
            } finally {socketRoll.SetValue(null,originalSocketRoll);socketClock.SetValue(null,originalSocketClock);}
            uint[] blessingTargets=socketTargets.Select(i=>i.UID).ToArray();int blessed=0;
            foreach(uint targetUid in blessingTargets) {
                foreach(int required in new[]{5,1,3,5}) {
                    var target=p.Equipment.TryGetItem(targetUid);byte beforeBless=target.Bless;
                    var prior=p.Inventory.Objects.Select(i=>i.UID).ToArray();uint cp=p.Entity.ConquerPoints;
                    ItemHandler.HandleBuyFromNPC(BuyBook(700073,(uint)required,2888),p);
                    var gems=p.Inventory.Objects.Where(i=>!prior.Contains(i.UID)).ToArray();uint cost=(uint)required*ConquerItemInformation.BaseInformations[700073].ConquerPointsWorth;
                    Check(gems.Length==required && gems.All(i=>i.ID==700073 && i.StackSize==1) && p.Entity.ConquerPoints==cp-cost,
                        "ordinary purchase supplies exact Super Tortoise Gem recipe");finishingCost+=cost;
                    var request=BlessingFrame(targetUid,gems.Select(i=>i.UID).ToArray());
                    if(blessed==0 && beforeBless==0) {
                        foreach(string fault in new[]{"short","length","family","action","count","seal","zero","duplicate","self","oversize"}) {
                            var invalid=(byte[])request.Clone();
                            switch(fault) {
                                case "short":Array.Resize(ref invalid,29);break;
                                case "length":invalid[0]--;break;
                                case "family":invalid[2]++;break;
                                case "action":invalid[20]++;break;
                                case "count":invalid[27]=1;break;
                                case "seal":invalid[invalid.Length-1]^=1;break;
                                case "zero":Array.Clear(invalid,91,4);break;
                                case "duplicate":Array.Copy(invalid,91,invalid,95,4);break;
                                case "self":Revora.Network.Writer.WriteUInt32(targetUid,91,invalid);break;
                                case "oversize":Array.Resize(ref invalid,124);break;
                            }
                            Call(upgrades,null,"Bless",p,invalid);
                            Check(!Fenced(p) && target.Bless==0 && gems.All(i=>p.Inventory.ContainsUID(i.UID)),"malformed blessing pays nothing: "+fault);
                        }
                        var wrong=Pack(p,1088001,1);var wrongFrame=(byte[])request.Clone();Revora.Network.Writer.WriteUInt32(wrong.UID,91,wrongFrame);
                        ItemUsage.Process(wrongFrame,p);Check(!Fenced(p) && target.Bless==0 && p.Inventory.ContainsUID(wrong.UID),"Meteor cannot replace a Super Tortoise Gem");
                        foreach(string state in new[]{"fake","unloaded","trade","uncanonical","locked","rental","bound-gem"}) {
                            if(state=="fake")p.Fake=true;
                            if(state=="unloaded")p.Entity.FullyLoaded=false;
                            if(state=="trade")p.Trade.InTrade=true;
                            if(state=="uncanonical")Kernel.GamePool.Remove(p.Entity.UID);
                            if(state=="locked")target.Lock=1;
                            if(state=="rental")target.Days=1;
                            if(state=="bound-gem")gems[0].Bound=true;
                            try {ItemUsage.Process(request,p);} finally {
                                p.Fake=false;p.Entity.FullyLoaded=true;p.Trade.InTrade=false;Kernel.GamePool[p.Entity.UID]=p;
                                target.Lock=0;target.Days=0;gems[0].Bound=false;
                            }
                            Check(!Fenced(p) && target.Bless==0 && gems.All(i=>p.Inventory.ContainsUID(i.UID)),"ineligible blessing refuses payment: "+state);
                        }
                        var insufficient=BlessingFrame(targetUid,gems.Take(3).Select(i=>i.UID).ToArray());ItemUsage.Process(insufficient,p);
                        Check(!Fenced(p) && target.Bless==0 && gems.All(i=>p.Inventory.ContainsUID(i.UID)),"incomplete first-blessing recipe cannot spend gems");
                        Query("UPDATE items SET warehouse=1 WHERE uid=@uid","uid",(long)gems[0].UID);
                        ItemUsage.Process(request,p);
                        Check(Fenced(p) && ConquerItemTable.LoadItem(targetUid).Bless==0
                            && Scalar("SELECT count(*) FROM revora_equipment_blessings WHERE uid=@uid",p.Entity.UID)==0,
                            "saved material ownership mismatch fences session and rolls back target");
                        Query("UPDATE items SET warehouse=0 WHERE uid=@uid","uid",(long)gems[0].UID);
                        p=ReloadGold(p);ConquerItemTable.LoadItems(p);target=p.Equipment.TryGetItem(targetUid);
                        Query("CREATE FUNCTION suppress_blessing() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_blessing BEFORE INSERT ON revora_equipment_blessings FOR EACH ROW EXECUTE FUNCTION suppress_blessing()");
                        try {ItemUsage.Process(request,p);} finally {Query("DROP TRIGGER suppress_blessing ON revora_equipment_blessings;DROP FUNCTION suppress_blessing()");}
                        Check(Fenced(p) && ConquerItemTable.LoadItem(targetUid).Bless==0 && gems.All(i=>ConquerItemTable.LoadItem(i.UID).ID==700073)
                            && Scalar("SELECT count(*) FROM revora_equipment_blessings WHERE uid=@uid",p.Entity.UID)==0,"suppressed receipt rolls back blessing and all five payments");
                        p=ReloadGold(p);ConquerItemTable.LoadItems(p);target=p.Equipment.TryGetItem(targetUid);
                    }
                    if(blessed==0 && beforeBless==1) {
                        var projection=upgrades.GetField("BeforeBlessProjection",Hidden);
                        projection.SetValue(null,(Action)(()=>{throw new InvalidOperationException("fixture after blessing commit");}));
                        try {ItemUsage.Process(request,p);} finally {projection.SetValue(null,null);}
                        Check(Fenced(p) && ConquerItemTable.LoadItem(targetUid).Bless==3
                            && Scalar("SELECT count(*) FROM revora_equipment_blessings WHERE uid=@uid",p.Entity.UID)==2,
                            "projection failure fences stale session but preserves committed -3 blessing and receipt");
                        p=ReloadGold(p);ConquerItemTable.LoadItems(p);target=p.Equipment.TryGetItem(targetUid);
                        Check(target.Bless==3 && gems.All(i=>!p.Inventory.ContainsUID(i.UID)),"full reload recovers paid result after projection failure");
                    }
                    if(beforeBless==3) {
                        // The minimum legacy UID payload omits the shared-builder four-byte trailer.
                        var compact=new byte[request.Length-4];Array.Copy(request,compact,compact.Length-8);
                        Revora.Network.Writer.WriteUInt16((ushort)(compact.Length-8),0,compact);
                        Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,compact.Length-8,compact);request=compact;
                    }
                    System.Threading.Tasks.Parallel.For(0,4,n=>ItemUsage.Process(request,p));
                    byte wanted=beforeBless==0?(byte)1:(byte)(beforeBless+2);
                    Check(!Fenced(p) && target.Bless==wanted && ConquerItemTable.LoadItem(targetUid).Bless==wanted
                        && gems.All(i=>!p.Inventory.ContainsUID(i.UID)),"concurrent ordinary blessing pays once and saves the exact next tier");
                }
                blessed++;Call(typeof(Achievement),p.Entity.MyAchievement,"Refresh",DateTime.UtcNow,true);
                Check(HasAchievement(p,10414)==(blessed==6) && HasAchievement(p,10417)==(blessed==6),"-7 and perfect set wait for sixth paid blessed piece");
            }
            Check(Scalar("SELECT count(*) FROM revora_equipment_blessings WHERE uid=@uid",p.Entity.UID)==24,
                "six complete blessing paths persist exactly24 receipts");
            uint expectedCp=initialCp-85u*ConquerItemInformation.BaseInformations[1088000].ConquerPointsWorth
                -6u*ConquerItemInformation.BaseInformations[gemType].ConquerPointsWorth-plusCost-finishingCost;
            Check(p.Entity.ConquerPoints==expectedCp,"all quality, sockets, enchantment, +12 and blessing materials have the exact loaded total price");
            p=ReloadGold(p);ConquerItemTable.LoadItems(p);p.Entity.MyAchievement.Send();
            Check(p.Account.State==AccountTable.AccountState.Player && p.Entity.ConquerPoints==expectedCp
                && p.Entity.RevoraPoint==50000,"production reload preserves ordinary permissions, exact CP spending and unspent earned RVPs");
            Check(p.Equipment.Objects.Count(i=>i!=null && new[]{1,2,3,4,6,8}.Contains((int)i.Position))==6,
                "all six ordinarily purchased core pieces survive production character and item reload");
            foreach(int achievement in new[]{10410,10411,10412,10413,10414,10415,10416,10417})Check(HasAchievement(p,achievement)
                && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id="+achievement,p.Entity.UID)==1,
                "earned equipment achievement survives full production reload once: "+achievement);
            p=VerifyGemAndAlternateEarning(p);
            // Seeded negative fixtures are separate from the completed ordinary earning path above.
            var stackTarget=Pack(p,410009,1);var stacked=Enumerable.Range(0,5).Select(n=>Pack(p,700073,2)).ToArray();
            var firstSelection=BlessingFrame(stackTarget.UID,stacked.Select(i=>i.UID).ToArray());
            ItemUsage.Process(firstSelection,p);ItemUsage.Process(firstSelection,p);
            Check(!Fenced(p) && stackTarget.Bless==1 && stacked.All(i=>i.StackSize==1 && ConquerItemTable.LoadItem(i.UID).StackSize==1),
                "stacked gem selection consumes exactly one unit per distinct UID");
            foreach(int size in new[]{1,3}) {
                var extra=Enumerable.Range(0,size).Select(n=>Pack(p,700073,1)).ToArray();
                ItemUsage.Process(BlessingFrame(stackTarget.UID,extra.Select(i=>i.UID).ToArray()),p);
            }
            ItemUsage.Process(firstSelection,p);
            Check(!Fenced(p) && stackTarget.Bless==5 && stacked.All(i=>i.StackSize==1),
                "old five-gem selection cannot replay as a later five-gem tier even while units remain");
            var boundTarget=Pack(p,410009,1);boundTarget.Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)boundTarget.UID);
            var boundGems=Enumerable.Range(0,5).Select(n=>Pack(p,700073,1)).ToArray();
            foreach(var gem in boundGems){gem.Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)gem.UID);}
            ItemUsage.Process(BlessingFrame(boundTarget.UID,boundGems.Select(i=>i.UID).ToArray()),p);
            Check(!Fenced(p) && boundTarget.Bound && boundTarget.Bless==1 && ConquerItemTable.LoadItem(boundTarget.UID).Bound
                && boundGems.All(i=>!p.Inventory.ContainsUID(i.UID)),"bound gems may bless a bound owned target without unbinding it");
            p=VerifyEmbeddingSafety(p);
            VerifyEnchantRanges(p);
        } finally {Environment.CurrentDirectory=previous;}
    }
    // One connection, fixed disposable PostgreSQL only. No payload logging.
    // Drop the server's successful COMMIT response before Npgsql receives it.
    sealed class CommitReplyDrop : IDisposable {
        readonly System.Net.Sockets.TcpListener listener;
        System.Net.Sockets.TcpClient peer,backend;
        readonly System.Threading.Tasks.Task worker;
        internal volatile bool Dropped;
        internal int Port {get{return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;}}
        static byte[] ReadExact(Stream stream,int size) {
            var bytes=new byte[size];int offset=0;
            while(offset<size){int count=stream.Read(bytes,offset,size-offset);if(count==0)throw new EndOfStreamException();offset+=count;}
            return bytes;
        }
        internal CommitReplyDrop() {
            listener=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0);listener.Start(1);
            worker=System.Threading.Tasks.Task.Run(()=> {
                System.Threading.Tasks.Task outbound=null;
                try {
                    peer=listener.AcceptTcpClient();backend=new System.Net.Sockets.TcpClient();backend.Connect(System.Net.IPAddress.Loopback,25439);
                    var client=peer.GetStream();var server=backend.GetStream();client.ReadTimeout=server.ReadTimeout=5000;client.WriteTimeout=server.WriteTimeout=5000;
                    outbound=System.Threading.Tasks.Task.Run(()=>{try{client.CopyTo(server);}catch(IOException){}catch(ObjectDisposedException){}});
                    while(true){
                        var header=ReadExact(server,5);int length=(header[1]<<24)|(header[2]<<16)|(header[3]<<8)|header[4];
                        if(length<4||length>1024*1024)throw new InvalidDataException("Private PostgreSQL response exceeds fixture bounds.");
                        var payload=ReadExact(server,length-4);
                        if(header[0]==(byte)'C'&&payload.SequenceEqual(new byte[]{67,79,77,77,73,84,0})){
                            Dropped=true;peer.Client.LingerState=new System.Net.Sockets.LingerOption(true,0);break;
                        }
                        client.Write(header,0,header.Length);client.Write(payload,0,payload.Length);
                    }
                } finally {
                    if(peer!=null)peer.Close();if(backend!=null)backend.Close();listener.Stop();
                    if(outbound!=null&&!outbound.Wait(5000))throw new TimeoutException("Private relay did not stop.");
                }
            });
        }
        public void Dispose() {
            listener.Stop();if(peer!=null)peer.Close();if(backend!=null)backend.Close();
            if(!worker.Wait(5000))throw new TimeoutException("Private COMMIT drop fixture did not stop.");
        }
    }
    static Exception LoseCommitReply(Action action) {
        string original=DataHolder.ConnectionString;
        var config=new NpgsqlConnectionStringBuilder(original);
        if(config.Host!="127.0.0.1"||config.Port!=25439||config.Database!="revora_candidate_game"||config.SearchPath!="equipment_earning_fixture")
            throw new InvalidOperationException("COMMIT drop fixture requires the isolated test schema.");
        using(var proxy=new CommitReplyDrop()) {
            config.Port=proxy.Port;config.Pooling=false;config.SslMode=SslMode.Disable;config.Timeout=5;config.CommandTimeout=5;
            Exception observed=null;
            try {DataHolder.ConnectionString=config.ConnectionString;action();}
            catch(System.Reflection.TargetInvocationException error){observed=error.InnerException;}
            catch(NpgsqlException error){observed=error;}
            finally {DataHolder.ConnectionString=original;}
            Check(proxy.Dropped,"private relay observed a successful COMMIT and withheld its response");
            return observed;
        }
    }
    static GameState VerifyGemAndAlternateEarning(GameState p) {
        var pieces=p.Equipment.Objects.Where(i=>i!=null && new[]{1,2,3,4,6,8}.Contains((int)i.Position)).ToArray();
        int filled=0;
        foreach(var target in pieces)foreach(ushort slot in new ushort[]{1,2}) {
            uint cp=p.Entity.ConquerPoints;var before=p.Inventory.Objects.Select(i=>i.UID).ToArray();
            ItemHandler.HandleBuyFromNPC(BuyBook(700073,1,2888),p);
            Check(p.Inventory.Objects.Any(i=>!before.Contains(i.UID)),"gem purchase supplies item; CP="+cp+" price="+ConquerItemInformation.BaseInformations[700073].ConquerPointsWorth);
            var gem=p.Inventory.Objects.Single(i=>!before.Contains(i.UID));
            Check(gem.ID==700073 && p.Entity.ConquerPoints==cp-ConquerItemInformation.BaseInformations[700073].ConquerPointsWorth,"ordinary CP purchase supplies Super Tortoise Gem");
            var frame=new byte[32];Revora.Network.Writer.WriteUInt16(24,0,frame);
            Revora.Network.Writer.WriteUInt16(1027,2,frame);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,24,frame);
            Revora.Network.Writer.WriteUInt32(target.UID,12,frame);Revora.Network.Writer.WriteUInt32(gem.UID,16,frame);
            Revora.Network.Writer.WriteUInt16(slot,20,frame);
            if(filled==11) {
                LoseCommitReply(()=>EmbedSocket.Process(frame,p));
                Check(Fenced(p) && p.Inventory.ContainsUID(gem.UID) && !HasAchievement(p,10428),
                    "lost successful twelfth embedding reply fences unprojected cache");
                Check(ConquerItemTable.LoadItem(target.UID).SocketTwo==Enums.Gem.SuperTortoiseGem
                    && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND uid="+gem.UID,p.Entity.UID)==0
                    && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10428",p.Entity.UID)==1,
                    "lost COMMIT reply preserves the paid twelfth socket and achievement atomically");
                p=ReloadGold(p);ConquerItemTable.LoadItems(p);p.Entity.MyAchievement.Send();
                Check(!Fenced(p) && HasAchievement(p,10428) && !p.Inventory.ContainsUID(gem.UID),
                    "ordinary reload recovers the exact earned gem achievement after actual transport loss");
            }
            System.Threading.Tasks.Parallel.For(0,4,n=>EmbedSocket.Process(frame,p));
            Check(!p.Inventory.ContainsUID(gem.UID) && (slot==1?ConquerItemTable.LoadItem(target.UID).SocketOne:ConquerItemTable.LoadItem(target.UID).SocketTwo)==Enums.Gem.SuperTortoiseGem,"ordinary embedding consumes purchased gem and saves socket");
            filled++;Call(typeof(Achievement),p.Entity.MyAchievement,"Refresh",DateTime.UtcNow,true);
            Check(HasAchievement(p,10428)==(filled==12),"gem achievement waits for twelfth filled socket");
        }
        Check(Scalar("SELECT count(*) FROM revora_gem_embeddings WHERE uid=@uid",p.Entity.UID)==12,"twelve paid sockets retain twelve receipts despite concurrent replays");
        Check(!HasAchievement(p,10432),"main equipment cannot earn alternate achievement");
        GeneralData.SwitchEquipment(p,true);GeneralData.SwitchEquipment(p,false);
        Check(!HasAchievement(p,10432),"switch to empty alternate bank earns nothing");
        pieces=p.Equipment.Objects.Where(i=>i!=null && new[]{1,2,3,4,6,8}.Contains((int)i.Position)).ToArray();
        foreach(var target in pieces) {
            byte slot=target.Position;ItemHandler.UnequipItem(new ItemUsage(true){dwParam=slot,Action=ItemUsage.Mode.UnequipItem},p);
            ItemHandler.EquipItem(new ItemUsage(true){UID=target.UID,dwParam=(uint)(slot+20),Action=ItemUsage.Mode.EquipItem},p);
            Check(p.Equipment.TryGetItem((byte)(slot+20))!=null && ConquerItemTable.LoadItem(target.UID).Position==slot+20,"ordinary move saves alternate slot "+slot);
        }
        Call(typeof(Achievement),p.Entity.MyAchievement,"Refresh",DateTime.UtcNow,true);
        Check(!HasAchievement(p,10432),"populating all alternate slots still requires a switch");
        p.Entity.AddFlag((ulong)FlagsUpdate.Flags.Fly,30,true);
        Check(!GeneralData.SwitchEquipment(p,true) && !p.AlternateEquipment,"rejected flying switch leaves main bank active");
        p.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Fly);
        Call(typeof(Achievement),p.Entity.MyAchievement,"Refresh",DateTime.UtcNow,true);
        Check(!HasAchievement(p,10432),"failed switch cannot award alternate achievement");
        var cachedOffhand=new ConquerItem(true){UID=3999999,ID=410009,Position=ConquerItem.AlternateLeftWeapon,StackSize=1};
        p.Equipment.Add(cachedOffhand);
        Check(GeneralData.SwitchEquipment(p,true),"switch itself retains existing behavior for stale cache fixture");
        Call(typeof(Achievement),p.Entity.MyAchievement,"Refresh",DateTime.UtcNow,true);
        Check(!HasAchievement(p,10432),"cached offhand missing durable ownership blocks evidence");
        p=ReloadGold(p);ConquerItemTable.LoadItems(p);
        Check(GeneralData.SwitchEquipment(p,true),"ordinary complete alternate switch succeeds");
        GeneralData.SwitchEquipment(p,true);GeneralData.SwitchEquipment(p,false);GeneralData.SwitchEquipment(p,true);
        p=ReloadGold(p);ConquerItemTable.LoadItems(p);p.Entity.MyAchievement.Send();
        foreach(int id in new[]{10428,10432})Check(HasAchievement(p,id)
            && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id="+id,p.Entity.UID)==1,
            "earned identity survives full reload and repeated switches exactly once: "+id);
        return p;
    }
    static byte[] EmbedFrame(uint target,uint gem,ushort slot,ushort mode=0) {
        var f=new byte[32];Revora.Network.Writer.WriteUInt16(24,0,f);Revora.Network.Writer.WriteUInt16(1027,2,f);
        Revora.Network.Writer.WriteUInt32(target,12,f);Revora.Network.Writer.WriteUInt32(gem,16,f);
        Revora.Network.Writer.WriteUInt16(slot,20,f);Revora.Network.Writer.WriteUInt16(mode,22,f);
        Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,24,f);return f;
    }
    static GameState VerifyEmbeddingSafety(GameState p) {
        var target=Pack(p,410009,1);var gem=Pack(p,700013,2);
        target.SocketOne=Enums.Gem.EmptySocket;Query("UPDATE items SET socketone=255 WHERE uid=@uid","uid",(long)target.UID);
        foreach(string fault in new[]{"short","length","family","seal","slot","mode","target","gem","oversize"}) {
            var f=EmbedFrame(target.UID,gem.UID,1);
            switch(fault) {
                case "short":Array.Resize(ref f,10);break;case "length":f[0]--;break;case "family":f[2]++;break;
                case "seal":f[31]++;break;case "slot":f[20]=3;break;case "mode":f[22]=2;break;
                case "target":Revora.Network.Writer.WriteUInt32(0,12,f);break;case "gem":Revora.Network.Writer.WriteUInt32(target.UID,16,f);break;
                case "oversize":Array.Resize(ref f,100);break;
            }
            EmbedSocket.Process(f,p);Check(!Fenced(p) && gem.StackSize==2 && target.SocketOne==Enums.Gem.EmptySocket,"malformed embedding rejected: "+fault);
        }
        EmbedSocket.Process(EmbedFrame(target.UID,gem.UID,2),p);
        Check(gem.StackSize==2 && target.SocketTwo==0,"embedding cannot create a missing second hole");
        var fakeGem=Pack(p,1088001,1);EmbedSocket.Process(EmbedFrame(target.UID,fakeGem.UID,1),p);
        Check(p.Inventory.ContainsUID(fakeGem.UID) && target.SocketOne==Enums.Gem.EmptySocket,"non-gem item with gem-like numeric suffix cannot pay");
        var talismanGem=Pack(p,700103,1);EmbedSocket.Process(EmbedFrame(target.UID,talismanGem.UID,1),p);
        Check(p.Inventory.ContainsUID(talismanGem.UID) && target.SocketOne==Enums.Gem.EmptySocket,"Thunder gem cannot enter core equipment");
        gem.Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)gem.UID);
        EmbedSocket.Process(EmbedFrame(target.UID,gem.UID,1),p);
        Check(gem.StackSize==2 && target.SocketOne==Enums.Gem.EmptySocket,"bound gem cannot upgrade unbound equipment");
        gem.Bound=false;Query("UPDATE items SET bound=0 WHERE uid=@uid","uid",(long)gem.UID);
        Query("CREATE FUNCTION reject_embed_receipt() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER reject_embed_receipt BEFORE INSERT ON revora_gem_embeddings FOR EACH ROW EXECUTE PROCEDURE reject_embed_receipt()");
        EmbedSocket.Process(EmbedFrame(target.UID,gem.UID,1),p);
        Check(Fenced(p) && ConquerItemTable.LoadItem(gem.UID).StackSize==2 && ConquerItemTable.LoadItem(target.UID).SocketOne==Enums.Gem.EmptySocket,
            "receipt failure rolls back consumed gem and socket together");
        Query("DROP TRIGGER reject_embed_receipt ON revora_gem_embeddings;DROP FUNCTION reject_embed_receipt()");
        uint targetUid=target.UID,gemUid=gem.UID;p=ReloadGold(p);ConquerItemTable.LoadItems(p);target=p.Inventory.Objects.Single(i=>i.UID==targetUid);gem=p.Inventory.Objects.Single(i=>i.UID==gemUid);
        var request=EmbedFrame(targetUid,gemUid,1);System.Threading.Tasks.Parallel.For(0,4,n=>EmbedSocket.Process(request,p));
        Check(!Fenced(p) && gem.StackSize==1 && ConquerItemTable.LoadItem(gemUid).StackSize==1 && target.SocketOne==Enums.Gem.SuperDragonGem,
            "concurrent embedding consumes exactly one stacked gem");
        EmbedSocket.Process(EmbedFrame(targetUid,0,1,1),p);EmbedSocket.Process(request,p);
        Check(target.SocketOne==Enums.Gem.EmptySocket && gem.StackSize==1,"delayed insertion replay cannot spend remaining stack after removal");
        target.SocketOne=Enums.Gem.SuperDragonGem;target.SocketTwo=Enums.Gem.SuperTortoiseGem;
        Query("UPDATE items SET socketone=13,sockettwo=73 WHERE uid=@uid","uid",(long)targetUid);
        EmbedSocket.Process(EmbedFrame(targetUid,0,2,1),p);
        Check(target.SocketOne==Enums.Gem.SuperDragonGem && target.SocketTwo==Enums.Gem.EmptySocket
            && ConquerItemTable.LoadItem(targetUid).SocketOne==Enums.Gem.SuperDragonGem,"removing second gem preserves first gem");
        var fresh=Pack(p,700013,1);var service=typeof(Entity).Assembly.GetType("Revora.Game.Features.EquipmentSockets");
        var hook=service.GetField("BeforeProjection",Hidden);hook.SetValue(null,(Action)(()=>{throw new InvalidOperationException("fixture projection failure");}));
        try {EmbedSocket.Process(EmbedFrame(targetUid,fresh.UID,2),p);}finally{hook.SetValue(null,null);}
        Check(Fenced(p) && ConquerItemTable.LoadItem(targetUid).SocketTwo==Enums.Gem.SuperDragonGem
            && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND uid="+fresh.UID,p.Entity.UID)==0,"postcommit projection failure preserves paid saved socket");
        p=ReloadGold(p);ConquerItemTable.LoadItems(p);target=p.Inventory.Objects.Single(i=>i.UID==targetUid);
        EmbedSocket.Process(EmbedFrame(targetUid,fresh.UID,2),p);
        Check(!Fenced(p) && target.SocketTwo==Enums.Gem.SuperDragonGem && !p.Inventory.ContainsUID(fresh.UID),"reload recovers paid socket without replay charge");
        return p;
    }
    static void VerifyEnchantRanges(GameState p) {
        var service=typeof(Entity).Assembly.GetType("Revora.Game.Features.ItemEnchantment");
        var draw=service.GetField("Draw",Hidden);object originalDraw=draw.GetValue(null);
        var target=Pack(p,410009,1);
        // Independent numeric expectations from the official table, inclusive endpoints.
        int[][] rows={new[]{700000,60,109,170,229},new[]{700010,100,159,200,255},
            new[]{700020,40,89,90,149},new[]{700030,80,129,170,229},new[]{700040,40,89,70,119},
            new[]{700050,60,109,140,199},new[]{700060,60,109,140,199},new[]{700070,40,89,170,229}};
        try {
            foreach(var row in rows)for(int grade=1;grade<=3;grade++)foreach(bool upper in new[]{false,true}) {
                int minimum=grade==1?1:row[grade==2?1:3],maximum=grade==1?59:row[grade==2?2:4];
                target.Enchant=0;Query("UPDATE items SET enchant=0 WHERE uid=@uid","uid",(long)target.UID);
                var gem=Pack(p,(uint)(row[0]+grade),1);
                draw.SetValue(null,(Func<int,int,int>)((low,high)=>{
                    if(low!=minimum || high!=maximum+1)throw new Exception("Incorrect enchant RNG interval");return upper?high-1:low;
                }));
                var request=new ItemUsage(true){UID=target.UID,dwParam=gem.UID,Action=ItemUsage.Mode.Enchant};
                ItemHandler.EnchantItem(request,p);ItemHandler.EnchantItem(request,p);
                Check(!Fenced(p) && target.Enchant==(upper?maximum:minimum) && ConquerItemTable.LoadItem(target.UID).Enchant==target.Enchant
                    && !p.Inventory.ContainsUID(gem.UID),"official enchant range endpoint persists and replay pays once: gem="+gem.ID+" upper="+upper);
            }
            foreach(uint type in new[]{700101U,700121U}) {
                target.Enchant=0;Query("UPDATE items SET enchant=0 WHERE uid=@uid","uid",(long)target.UID);
                var gem=Pack(p,type,1);draw.SetValue(null,(Func<int,int,int>)((low,high)=>high-1));
                ItemHandler.EnchantItem(new ItemUsage(true){UID=target.UID,dwParam=gem.UID,Action=ItemUsage.Mode.Enchant},p);
                Check(!Fenced(p) && target.Enchant==59 && !p.Inventory.ContainsUID(gem.UID),"existing normal talisman-gem enchant policy retained: "+type);
            }
            foreach(uint type in new[]{700102U,700103U,700122U,700123U,1088001U}) {
                var gem=Pack(p,type,1);byte before=target.Enchant;
                ItemHandler.EnchantItem(new ItemUsage(true){UID=target.UID,dwParam=gem.UID,Action=ItemUsage.Mode.Enchant},p);
                Check(!Fenced(p) && target.Enchant==before && p.Inventory.ContainsUID(gem.UID) && ConquerItemTable.LoadItem(gem.UID).ID==type,
                    "unsupported enchant material cannot be consumed: "+type);
            }
            var normal=Pack(p,700011,1);target.Enchant=254;Query("UPDATE items SET enchant=254 WHERE uid=@uid","uid",(long)target.UID);
            Call(service,null,"Apply",p,target,normal,(byte)0);
            Check(!Fenced(p) && target.Enchant==254 && p.Inventory.ContainsUID(normal.UID),"zero roll is refused before material payment");
            ItemHandler.EnchantItem(new ItemUsage(true){UID=target.UID,dwParam=normal.UID,Action=ItemUsage.Mode.Enchant},p);
            Check(!Fenced(p) && target.Enchant==254 && ConquerItemTable.LoadItem(target.UID).Enchant==254 && !p.Inventory.ContainsUID(normal.UID),
                "a legitimate lower roll consumes one gem while retaining better saved enchantment");
            var capped=Pack(p,700013,1);target.Enchant=255;Query("UPDATE items SET enchant=255 WHERE uid=@uid","uid",(long)target.UID);
            ItemHandler.EnchantItem(new ItemUsage(true){UID=target.UID,dwParam=capped.UID,Action=ItemUsage.Mode.Enchant},p);
            Check(!Fenced(p) && target.Enchant==255 && p.Inventory.ContainsUID(capped.UID),"maximum enchantment cannot consume another gem");
        } finally {draw.SetValue(null,originalDraw);}
    }
}
