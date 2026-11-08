using System;
using System.IO;
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
    static readonly Type Bloodshed=typeof(Entity).Assembly.GetType("Revora.Game.Features.BloodshedSeaBoosts");
    static DateTime seaNow=DateTime.UtcNow;
    static long BoostUses(GameState p){return Scalar("SELECT count(*) FROM revora_bloodshed_boost_uses WHERE uid=@uid",p.Entity.UID);}
    static void DrinkBoost(GameState p,ConquerItem item){ItemUsage.Process(PrayerPacket(item.UID),p);}
    // Match login's final stat rebuild after the fixture restores items/spells.
    static GameState ReloadBloodshed(GameState p) {
        p=ReloadBall(p);p.LoadItemStats();return p;
    }
    static void VerifyBloodshedSea(string runtime,Map town){
        Call(Bloodshed,null,"Initialize");Call(Bloodshed,null,"Initialize");
        Bloodshed.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>seaNow));
        string heard="";Bloodshed.GetField("Spoken",Hidden).SetValue(null,(Action<string>)(m=>{heard=heard+m+"|";}));
        string handler=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemHandler.cs")));
        string usage=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemUsage.cs")));
        string thread=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Thread.cs")));
        string audit=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../docs/ITEM_USE_COMPOUND_AUDIT.md")));
        string npcs=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Npcs.cs")));
        Check(handler.IndexOf("Bloodshed Sea boost must use the owned service.")>=0 && handler.IndexOf("BloodshedSeaBoosts.TryUse")>handler.IndexOf("HealingMedicine.TryUse")
            && usage.IndexOf("BloodshedSeaBoosts.TryHandle")>usage.IndexOf("HealingMedicine.TryHandle")
            && thread.IndexOf("BloodshedSeaBoosts.Initialize")>thread.IndexOf("HealingMedicine.Initialize")
            && thread.IndexOf("BloodshedSeaBoosts.ExpireSharedShield")>=0 && thread.IndexOf("BloodshedSeaBoosts.Tick")>=0,
            "BloodshedSeaBoosts intercepts sit after HealingMedicine and leftover cases fail closed");
        int switchStart=handler.IndexOf("switch (item.ID)"),switchEnd=handler.IndexOf("public static byte GetNextRefineryItem",switchStart);
        var sourceIds=System.Text.RegularExpressions.Regex.Matches(handler.Substring(switchStart,switchEnd-switchStart),@"(?m)^ {16}case (\d+):").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        var auditIds=System.Text.RegularExpressions.Regex.Matches(audit,@"(?m)^\| (\d+) \|").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        Check(sourceIds.Length==518 && sourceIds.OrderBy(id=>id).SequenceEqual(auditIds.OrderBy(id=>id)),
            "compound audit still enumerates the exact 518 numeric top-level UseItem case IDs");
        Check(audit.IndexOf("| live-split-write | 145 |")>=0 && audit.IndexOf("| owned-fail-closed | 275 |")>=0,
            "census is 145 live-split-write / 275 owned-fail-closed");
        uint[] ids=new uint[]{3004230u,3004182u,3004231u,3004232u,3004233u,3004234u,3004235u,3004236u,3004237u,3004238u};
        string[] names=new string[]{"SuperUniversalPotion","DragonTear","DragonBreakPotion","DragonStrikePotion","DragonAnti-breakPotion","DragonImmunityPotion","DragonP-AttackPotion","DragonM-AttackPotion","DragonP-DamagePotion","DragonM-DamagePotion"};
        uint[] shops=new uint[]{16834u,16852u,16853u,16854u,16855u,16856u};
        string catalog=File.ReadAllText("database\\itemtype-new.txt");
        string v2=File.ReadAllText("database\\shops\\emoneyshopV2.ini");
        ShopFile.Load();EShopFile.Load();EShopV2File.Load();
        for(int n=0;n<ids.Length;n++){
            Check(audit.IndexOf("| "+ids[n].ToString()+" | owned-fail-closed | source+timer |")>=0,"audit owns Bloodshed Sea "+ids[n]);
            Check(ConquerItemInformation.BaseInformations.ContainsKey(ids[n]),"installed catalog contains Bloodshed Sea "+ids[n]);
            Check(catalog.IndexOf(ids[n].ToString()+"@@"+names[n])>=0,"catalog identity for "+ids[n]+" is "+names[n]);
        }
        for(int n=0;n<shops.Length;n++){
            uint shop=shops[n];
            Check(v2.IndexOf("ID="+shop.ToString())>=0 && EShopFile.Shops.ContainsKey(shop) && EShopFile.Shops[shop].MoneyType==ShopFile.MoneyType.ConquerPoints
                && EShopFile.Shops[shop].Items.Contains(3004230u) && EShopFile.Shops[shop].Items.Contains(3004182u)
                && EShopFile.Shops[shop].Items.Contains(3004238u),
                "loaded EShop "+shop+" is ConquerPoints stock of Bloodshed Sea potions");
            Check(PublicScalar("SELECT count(*) FROM public.npcs WHERE id="+shop+" AND mapid=3846")==1,
                "installed NPC "+shop+" is on Bloodshed Sea 3846");
            Check(npcs.IndexOf("case "+shop.ToString()+":")>=0,"Npcs.cs still has leftover dialog for shop UID "+shop);
        }
        var sea=World(runtime,3846);
        for(int n=0;n<shops.Length;n++){
            uint shop=shops[n];
            long sx=PublicScalar("SELECT cellx FROM public.npcs WHERE id="+shop);
            long sy=PublicScalar("SELECT celly FROM public.npcs WHERE id="+shop);
            sea.Npcs[shop]=new NpcSpawn{UID=shop,MapID=3846,X=(ushort)sx,Y=(ushort)sy};
        }
        long nx=PublicScalar("SELECT cellx FROM public.npcs WHERE id=16834");
        long ny=PublicScalar("SELECT celly FROM public.npcs WHERE id=16834");
        var buyer=BallPlayer(sea);buyer.Entity.MapID=3846;buyer.Entity.X=(ushort)nx;buyer.Entity.Y=(ushort)ny;
        uint price=ConquerItemInformation.BaseInformations[3004231].ConquerPointsWorth;
        buyer.Entity.ConquerPoints=price+8000;if(!EntityTable.SaveEntity(buyer))throw new Exception("buy fixture save failed");
        uint purse=buyer.Entity.ConquerPoints;
        ItemHandler.HandleBuyFromNPC(BuyBook(3004231,1,16834),buyer);
        Check(!Fenced(buyer) && buyer.Inventory.Objects.Count(i=>i.ID==3004231)==1 && buyer.Entity.ConquerPoints==purse-price,
            "nearby ordinary HandleBuyFromNPC obtains stocked 3004231 without a visible dialog option");
        var bought=buyer.Inventory.Objects.Single(i=>i.ID==3004231);int beforeBreak=buyer.Entity.Breaktrough;
        DrinkBoost(buyer,bought);
        Check(!Fenced(buyer) && BoostUses(buyer)==1 && !buyer.Inventory.ContainsUID(bought.UID) && buyer.Entity.IncreaseBreack && buyer.Entity.Breaktrough==beforeBreak+1500,
            "ordinary bought 3004231 use applies leftover Break once");
        Check(heard.IndexOf("You`ve used an item.")>=0 && heard.IndexOf("Your Break increased by 150%for 80 seconds. Please seize the chance!")>=0,
            "owned 3004231 sends leftover Break chance text");
        Check(Scalar("SELECT count(*) FROM revora_bloodshed_state WHERE uid=@uid AND break_at IS NOT NULL",buyer.Entity.UID)==1,
            "bought Break persists a durable grant stamp");
        buyer=ReloadBloodshed(buyer);buyer.Entity.MapID=3846;
        bool persistedBreak=buyer.Entity.IncreaseBreack;int boostedBreak=buyer.Entity.Breaktrough;
        buyer.Entity.IncreaseBreack=false;buyer.LoadItemStats();int baseBreak=buyer.Entity.Breaktrough;
        buyer.Entity.IncreaseBreack=persistedBreak;buyer.LoadItemStats();
        Check(!Fenced(buyer) && buyer.Entity.IncreaseBreack && buyer.Entity.Breaktrough==baseBreak+1500 && buyer.Entity.Breaktrough==boostedBreak,
            "Bloodshed Break bonus survives production reload");
        var packetBuyer=BallPlayer(sea);packetBuyer.Entity.MapID=3846;packetBuyer.Entity.X=(ushort)nx;packetBuyer.Entity.Y=(ushort)ny;
        packetBuyer.Entity.ConquerPoints=price+8000;if(!EntityTable.SaveEntity(packetBuyer))throw new Exception("packet buy save failed");
        byte[] buyBytes=BuyBook(3004182,1,16834).ToArray();Writer.WriteUInt16(1,20,buyBytes);Writer.WriteUInt64(PacketHandler.ClientSeal,buyBytes.Length-8,buyBytes);
        ItemUsage.Process(buyBytes,packetBuyer);
        Check(!Fenced(packetBuyer) && packetBuyer.Inventory.Objects.Count(i=>i.ID==3004182)==1,
            "ordinary ItemUsage buy packet obtains stocked 3004182 without opening shop UI");
        var tear=packetBuyer.Inventory.Objects.Single(i=>i.ID==3004182);DrinkBoost(packetBuyer,tear);
        Check(!Fenced(packetBuyer) && BoostUses(packetBuyer)==1 && packetBuyer.Entity.GodlyShield && packetBuyer.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.GodlyShield),
            "ordinary bought 3004182 use applies leftover GodlyShield");
        packetBuyer=ReloadBloodshed(packetBuyer);packetBuyer.Entity.MapID=3846;
        Check(!Fenced(packetBuyer) && packetBuyer.Entity.GodlyShield && packetBuyer.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.GodlyShield),
            "GodlyShield bonus survives production reload");
        var leave=BallPlayer(sea);leave.Entity.MapID=3846;DrinkBoost(leave,Pack(leave,3004182,1));
        Check(leave.Entity.GodlyShield && leave.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.GodlyShield),"3004182 grants leftover GodlyShield on map 3846");
        leave.Entity.MapID=1002;leave.GuardStamp=Time32.Now.AddSeconds(-11);Call(Bloodshed,null,"ExpireSharedShield",leave);
        Check(!leave.RaceGuard && leave.Entity.GodlyShield && leave.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.GodlyShield),
            "production race timer off map 3846 does not strip an active Bloodshed GodlyShield");
        Call(Bloodshed,null,"Tick",leave);
        Check(leave.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.GodlyShield),"Tick keeps or restores Bloodshed GodlyShield while the durable grant remains");
        leave.Entity.MapID=3846;
        Check(leave.Entity.GodlyShield && leave.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.GodlyShield),"re-entering map 3846 keeps the Bloodshed GodlyShield flag");
        var raceOnly=BallPlayer(town);raceOnly.Entity.MapID=1002;raceOnly.ApplyRacePotion(Enums.RaceItemType.GuardPotion,0);
        Check(raceOnly.RaceGuard && raceOnly.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.GodlyShield),"ordinary GuardPotion sets leftover RaceGuard GodlyShield");
        raceOnly.GuardStamp=Time32.Now.AddSeconds(-11);raceOnly.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.GodlyShield);
        Call(Bloodshed,null,"ExpireSharedShield",raceOnly);
        Check(!raceOnly.RaceGuard && !raceOnly.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.GodlyShield),
            "ordinary RaceGuard expiry off 3846 clears ownership after the generic flag loop removes flag 57");
        var raceFirst=BallPlayer(sea);raceFirst.Entity.MapID=3846;raceFirst.ApplyRacePotion(Enums.RaceItemType.GuardPotion,0);DrinkBoost(raceFirst,Pack(raceFirst,3004182,1));
        seaNow=seaNow.AddSeconds(80);Call(Bloodshed,null,"Tick",raceFirst);
        Check(raceFirst.RaceGuard && raceFirst.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.GodlyShield) && !raceFirst.Entity.GodlyShield,
            "Bloodshed expiry keeps flag 57 while leftover RaceGuard is still live");
        raceFirst.Entity.MapID=1002;raceFirst.GuardStamp=Time32.Now.AddSeconds(-11);Call(Bloodshed,null,"ExpireSharedShield",raceFirst);
        Check(!raceFirst.RaceGuard && !raceFirst.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.GodlyShield),
            "RaceGuard expiry after Bloodshed grant cleanup removes flag 57");
        seaNow=seaNow.AddSeconds(-80);
        var shieldFirst=BallPlayer(sea);shieldFirst.Entity.MapID=3846;DrinkBoost(shieldFirst,Pack(shieldFirst,3004182,1));shieldFirst.ApplyRacePotion(Enums.RaceItemType.GuardPotion,0);
        Check((int)Call(Bloodshed,null,"CoordinateRaceGuard",shieldFirst)>10,
            "later GuardPotion preserves the longer Bloodshed DivineShield UI duration");
        shieldFirst.Entity.MapID=1002;shieldFirst.GuardStamp=Time32.Now.AddSeconds(-11);shieldFirst.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.GodlyShield);
        Call(Bloodshed,null,"ExpireSharedShield",shieldFirst);
        Check(!shieldFirst.RaceGuard && shieldFirst.Entity.GodlyShield && shieldFirst.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.GodlyShield),
            "RaceGuard expiry restores active Bloodshed flag 57 after the generic flag loop removes it");
        seaNow=seaNow.AddSeconds(80);Call(Bloodshed,null,"Tick",shieldFirst);
        Check(!shieldFirst.Entity.GodlyShield && !shieldFirst.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.GodlyShield),
            "Bloodshed expiry after RaceGuard cleanup removes flag 57");
        seaNow=seaNow.AddSeconds(-80);
        var lateRace=BallPlayer(sea);lateRace.Entity.MapID=3846;DrinkBoost(lateRace,Pack(lateRace,3004182,1));lateRace.ApplyRacePotion(Enums.RaceItemType.GuardPotion,0);
        seaNow=seaNow.AddSeconds(80);lateRace.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.GodlyShield);Call(Bloodshed,null,"Tick",lateRace);
        Check(lateRace.RaceGuard && !lateRace.Entity.GodlyShield && lateRace.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.GodlyShield),
            "Bloodshed expiry restores flag 57 when a later RaceGuard timer still owns the already-removed shared flag");
        lateRace.Entity.MapID=1002;lateRace.GuardStamp=Time32.Now.AddSeconds(-11);lateRace.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.GodlyShield);
        Call(Bloodshed,null,"ExpireSharedShield",lateRace);
        Check(!lateRace.RaceGuard && !lateRace.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.GodlyShield),
            "later RaceGuard expiry clears ownership after both shared timers finish");
        seaNow=seaNow.AddSeconds(-80);
        var expiryFault=BallPlayer(sea);expiryFault.Entity.MapID=3846;DrinkBoost(expiryFault,Pack(expiryFault,3004182,1));
        Query("CREATE FUNCTION suppress_bloodshed_expiry() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_bloodshed_expiry BEFORE UPDATE ON revora_bloodshed_state FOR EACH ROW EXECUTE FUNCTION suppress_bloodshed_expiry()");
        try{seaNow=seaNow.AddSeconds(80);Call(Bloodshed,null,"Tick",expiryFault);
        Check(Fenced(expiryFault) && expiryFault.Entity.GodlyShield && expiryFault.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.GodlyShield)
            && PublicScalar("SELECT COUNT(*) FROM revora_bloodshed_state WHERE uid="+expiryFault.Entity.UID+" AND shield_at IS NOT NULL")==1,
            "suppressed Bloodshed expiry keeps durable and live GodlyShield state for a fenced retry");}
        finally{seaNow=seaNow.AddSeconds(-80);Query("DROP TRIGGER IF EXISTS suppress_bloodshed_expiry ON revora_bloodshed_state;DROP FUNCTION IF EXISTS suppress_bloodshed_expiry()");}
        for(int n=0;n<shops.Length;n++){
            uint shop=shops[n];uint potion=ids[n];
            long sx=PublicScalar("SELECT cellx FROM public.npcs WHERE id="+shop);
            long sy=PublicScalar("SELECT celly FROM public.npcs WHERE id="+shop);
            var shopper=BallPlayer(sea);shopper.Entity.MapID=3846;shopper.Entity.X=(ushort)sx;shopper.Entity.Y=(ushort)sy;
            uint cost=ConquerItemInformation.BaseInformations[potion].ConquerPointsWorth;
            shopper.Entity.ConquerPoints=cost+8000;if(!EntityTable.SaveEntity(shopper))throw new Exception("shop buy save failed");
            ItemHandler.HandleBuyFromNPC(BuyBook(potion,1,shop),shopper);
            Check(!Fenced(shopper) && shopper.Inventory.Objects.Count(i=>i.ID==potion)==1,
                "nearby ordinary HandleBuyFromNPC obtains stocked "+potion+" from EShop "+shop+" without a visible dialog option");
        }
        var cart=BallPlayer(sea);cart.Entity.MapID=3846;cart.Entity.X=(ushort)nx;cart.Entity.Y=(ushort)ny;
        cart.Entity.ConquerPoints=5000000;if(!EntityTable.SaveEntity(cart))throw new Exception("cart buy save failed");
        for(int n=0;n<ids.Length;n++){
            ItemHandler.HandleBuyFromNPC(BuyBook(ids[n],1,16834),cart);
            Check(!Fenced(cart) && cart.Inventory.Objects.Count(i=>i.ID==ids[n])==1,
                "nearby ordinary HandleBuyFromNPC obtains stocked "+ids[n]+" from EShop 16834");
        }
        var off=BallPlayer(town);var refuse=Pack(off,3004231,1);off.Entity.MapID=1002;int offBreak=off.Entity.Breaktrough;DrinkBoost(off,refuse);
        Check(!Fenced(off) && BoostUses(off)==0 && off.Inventory.ContainsUID(refuse.UID) && !off.Entity.IncreaseBreack && off.Entity.Breaktrough==offBreak,
            "owned 3004231 refuses outside map 3846 without consume or Break");
        Check(heard.IndexOf("This~potion~can~only~be~used~in~the~Bloodshed~Sea!")>=0,"owned off-map refuse uses leftover Bloodshed Sea text");
        var first=BallPlayer(sea);first.Entity.MapID=3846;first.LoadItemStats();var one=Pack(first,3004231,1);int startBreak=first.Entity.Breaktrough;DrinkBoost(first,one);
        Check(!Fenced(first) && BoostUses(first)==1 && first.Entity.IncreaseBreack && first.Entity.Breaktrough==startBreak+1500,
            "owned 3004231 on map 3846 consumes one unit and applies leftover Break once");
        var two=Pack(first,3004231,1);int stacked=first.Entity.Breaktrough;DrinkBoost(first,two);
        Check(!Fenced(first) && BoostUses(first)==2 && first.Entity.IncreaseBreack && first.Entity.Breaktrough==stacked,
            "owned 3004231 refresh consumes a second unit without stacking Break");
        int leftBreak=first.Entity.Breaktrough;first.Entity.MapID=1002;first.LoadItemStats();
        Check(first.Entity.IncreaseBreack && first.Entity.Breaktrough==leftBreak,"leaving map 3846 does not strip leftover Break");
        first.Entity.MapID=3846;first.LoadItemStats();
        Check(first.Entity.IncreaseBreack && first.Entity.Breaktrough==leftBreak,"re-entering map 3846 does not double leftover Break");
        seaNow=seaNow.AddSeconds(80);Call(Bloodshed,null,"Tick",first);
        Check(!Fenced(first) && !first.Entity.IncreaseBreack && first.Entity.Breaktrough==startBreak,
            "owned expiry cleanup removes leftover Break without a stranded bonus");
        seaNow=seaNow.AddSeconds(-80);
        var attr=BallPlayer(sea);attr.Entity.MapID=3846;attr.LoadItemStats();var uni=Pack(attr,3004230,1);
        uint minAtk=attr.Entity.MinAttack,maxAtk=attr.Entity.MaxAttack,magicAtk=attr.Entity.MagicAttack;DrinkBoost(attr,uni);
        Check(!Fenced(attr) && BoostUses(attr)==1 && attr.Entity.IncreaseAttribute
            && attr.Entity.MinAttack==minAtk+3000u && attr.Entity.MaxAttack==maxAtk+3000u && attr.Entity.MagicAttack==magicAtk+3000u,
            "owned 3004230 on map 3846 consumes one unit and applies leftover attributes once");
        var uni2=Pack(attr,3004230,1);uint min2=attr.Entity.MinAttack;DrinkBoost(attr,uni2);
        Check(!Fenced(attr) && BoostUses(attr)==2 && attr.Entity.IncreaseAttribute && attr.Entity.MinAttack==min2,
            "owned 3004230 refresh consumes a second unit without stacking attributes");
        attr=ReloadBloodshed(attr);attr.Entity.MapID=3846;
        bool persistedAttribute=attr.Entity.IncreaseAttribute;uint keepMin=attr.Entity.MinAttack;
        attr.Entity.IncreaseAttribute=false;attr.LoadItemStats();minAtk=attr.Entity.MinAttack;
        attr.Entity.IncreaseAttribute=persistedAttribute;attr.LoadItemStats();
        Check(!Fenced(attr) && attr.Entity.IncreaseAttribute && attr.Entity.MinAttack==keepMin && keepMin==minAtk+3000u,"attribute bonus survives production reload");
        seaNow=seaNow.AddSeconds(80);Call(Bloodshed,null,"Tick",attr);attr.LoadItemStats();
        Check(!Fenced(attr) && !attr.Entity.IncreaseAttribute && attr.Entity.MinAttack==minAtk,"owned expiry cleanup removes leftover attributes");
        seaNow=seaNow.AddSeconds(-80);
        var fault=BallPlayer(sea);fault.Entity.MapID=3846;var failItem=Pack(fault,3004231,1);int faultBreak=fault.Entity.Breaktrough;
        Query("CREATE FUNCTION suppress_bloodshed() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_bloodshed BEFORE INSERT ON revora_bloodshed_state FOR EACH ROW EXECUTE FUNCTION suppress_bloodshed()");
        try{DrinkBoost(fault,failItem);
        Check(Fenced(fault) && BoostUses(fault)==0 && fault.Inventory.ContainsUID(failItem.UID) && !fault.Entity.IncreaseBreack && fault.Entity.Breaktrough==faultBreak,
            "suppressed Bloodshed persist fences without applying leftover Break");}
        finally{Query("DROP TRIGGER IF EXISTS suppress_bloodshed ON revora_bloodshed_state;DROP FUNCTION IF EXISTS suppress_bloodshed()");}
        uint faultMax=fault.Entity.MaxHitpoints;fault=ReloadBloodshed(fault);fault.Entity.MapID=3846;fault.Entity.MaxHitpoints=faultMax;
        DrinkBoost(fault,fault.Inventory.Objects.Single(i=>i.ID==3004231));
        Check(!Fenced(fault) && BoostUses(fault)==1 && fault.Entity.IncreaseBreack,"production reload retries Bloodshed Sea boost after durable write failure");
        var race=BallPlayer(sea);race.Entity.MapID=3846;var raceItem=Pack(race,3004231,1);
        Parallel.For(0,16,n=>DrinkBoost(race,raceItem));
        Check(!Fenced(race) && BoostUses(race)==1 && !race.Inventory.ContainsUID(raceItem.UID) && race.Entity.IncreaseBreack,
            "parallel packets cannot duplicate Bloodshed Sea boost use");
        var direct=BallPlayer(sea);direct.Entity.MapID=3846;direct.LoadItemStats();var directItem=Pack(direct,3004234,1);int immune=direct.Entity.Immunity;
        ItemHandler.UseItem(directItem,direct);
        Check(!Fenced(direct) && BoostUses(direct)==1 && direct.Entity.IncreaseImunity && direct.Entity.Immunity==immune+15000,
            "direct UseItem applies leftover Immunity once");
        var rest=BallPlayer(sea);rest.Entity.MapID=3846;rest.LoadItemStats();
        int crit=rest.Entity.CriticalStrike,skill=rest.Entity.SkillCStrike,counter=rest.Entity.Counteraction;
        int pAtk=rest.Entity.PhysicalDamageIncrease,mAtk=rest.Entity.MagicDamageIncrease,pDef=rest.Entity.PhysicalDamageDecrease,mDef=rest.Entity.MagicDamageDecrease;
        DrinkBoost(rest,Pack(rest,3004232,1));
        Check(!Fenced(rest) && rest.Entity.IncreasePStrike && rest.Entity.CriticalStrike==crit+15000 && rest.Entity.SkillCStrike==skill+15000,
            "owned 3004232 applies leftover P-Strike and M-Strike once");
        DrinkBoost(rest,Pack(rest,3004233,1));
        Check(!Fenced(rest) && rest.Entity.IncreaseAntiBreack && rest.Entity.Counteraction==counter+1500,
            "owned 3004233 applies leftover Anti-break once");
        DrinkBoost(rest,Pack(rest,3004235,1));
        Check(!Fenced(rest) && rest.Entity.IncreaseFinalPAttack && rest.Entity.PhysicalDamageIncrease==pAtk+3000,
            "owned 3004235 applies leftover Final-P-Attack once");
        DrinkBoost(rest,Pack(rest,3004236,1));
        Check(!Fenced(rest) && rest.Entity.IncreaseFinalMAttack && rest.Entity.MagicDamageIncrease==mAtk+3000,
            "owned 3004236 applies leftover Final-M-Attack once");
        DrinkBoost(rest,Pack(rest,3004237,1));
        Check(!Fenced(rest) && rest.Entity.IncreaseFinalPDamage && rest.Entity.PhysicalDamageDecrease==pDef+3000,
            "owned 3004237 applies leftover Final-P-Damage once");
        DrinkBoost(rest,Pack(rest,3004238,1));
        Check(!Fenced(rest) && BoostUses(rest)==6 && rest.Entity.IncreaseFinalMDamage && rest.Entity.MagicDamageDecrease==mDef+3000,
            "owned 3004238 applies leftover Final-M-Damage once");
        rest=ReloadBloodshed(rest);rest.Entity.MapID=3846;
        bool persistedStrike=rest.Entity.IncreasePStrike;int keepCrit=rest.Entity.CriticalStrike;
        rest.Entity.IncreasePStrike=false;rest.LoadItemStats();int baseCrit=rest.Entity.CriticalStrike;
        rest.Entity.IncreasePStrike=persistedStrike;rest.LoadItemStats();
        Check(!Fenced(rest) && rest.Entity.IncreasePStrike && rest.Entity.IncreaseAntiBreack && rest.Entity.IncreaseFinalPAttack
            && rest.Entity.IncreaseFinalMAttack && rest.Entity.IncreaseFinalPDamage && rest.Entity.IncreaseFinalMDamage
            && rest.Entity.CriticalStrike==keepCrit && keepCrit==baseCrit+15000,"remaining Bloodshed Sea kinds survive production reload together");
        foreach(bool boundSrc in new[]{false,true}){
            var piled=BallPlayer(sea);piled.Entity.MapID=3846;var pile=NestedPack(piled,3004231,2,boundSrc);int pileBreak=piled.Entity.Breaktrough;
            DrinkBoost(piled,pile);
            Check(!Fenced(piled) && BoostUses(piled)==1 && pile.StackSize==1 && piled.Inventory.ContainsUID(pile.UID)
                && piled.Entity.IncreaseBreack && piled.Entity.Breaktrough==pileBreak+1500
                && Scalar("SELECT CASE WHEN bound THEN 1 ELSE 0 END FROM revora_bloodshed_boost_uses WHERE uid=@uid",piled.Entity.UID)==(boundSrc?1:0),
                "stacked Bloodshed Sea boost keeps the leftover remainder: "+boundSrc);
            DrinkBoost(piled,pile);
            Check(!Fenced(piled) && BoostUses(piled)==2 && !piled.Inventory.ContainsUID(pile.UID) && piled.Entity.Breaktrough==pileBreak+1500,
                "remaining Bloodshed Sea unit refreshes without stacking Break: "+boundSrc);
        }
        var replay=BallPlayer(sea);replay.Entity.MapID=3846;var once=Pack(replay,3004231,1);uint replayUid=once.UID;DrinkBoost(replay,once);
        ItemHandler.UseItem(once,replay);
        Check(!Fenced(replay) && BoostUses(replay)==1 && !replay.Inventory.ContainsUID(replayUid),"consumed Bloodshed Sea UID cannot replay");
        Bloodshed.GetField("Spoken",Hidden).SetValue(null,null);
    }
}
