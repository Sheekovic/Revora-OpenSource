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
    static readonly Type HossuHair=typeof(Entity).Assembly.GetType("Revora.Game.Features.HossuHairPacks");
    static DateTime hossuNow=DateTime.UtcNow;
    static int hossuPick=1;
    static long HossuUses(GameState p){return Scalar("SELECT count(*) FROM revora_hossu_hair_pack_uses WHERE uid=@uid",p.Entity.UID);}
    static void OpenHossu(GameState p,ConquerItem item){ItemUsage.Process(PrayerPacket(item.UID),p);}
    static void VerifyHossuHairPacks(string runtime,Map town){
        Query("CREATE TABLE IF NOT EXISTS teamarena (LIKE public.teamarena INCLUDING ALL);CREATE TABLE IF NOT EXISTS itemadding (LIKE public.itemadding INCLUDING ALL)");
        Call(HossuHair,null,"Initialize");Call(HossuHair,null,"Initialize");
        Call(HonorBuys,null,"Initialize");HonorShop.Load();
        HossuHair.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>hossuNow));
        HossuHair.GetField("Roll",Hidden).SetValue(null,(Func<int,int,int>)((min,max)=>hossuPick));
        string handler=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemHandler.cs")));
        string usage=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemUsage.cs")));
        string thread=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Thread.cs")));
        string monsters=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Database/MonsterTable.cs")));
        string audit=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../docs/ITEM_USE_COMPOUND_AUDIT.md")));
        string honorIni=File.ReadAllText("database\\shops\\HonorShop.ini");
        string catalog=File.ReadAllText("database\\itemtype-new.txt");
        uint[] hairs=new uint[]{3006165u,3006166u,3006167u,3006168u,3006169u};
        string[] names=new string[]{"NormalHossuHair","RefinedHossuHair","UniqueHossuHair","EliteHossuHair","SuperHossuHair"};
        Check(handler.IndexOf("Hossu hair pack must use the owned service.")>=0 && handler.IndexOf("HossuHairPacks.TryUse")>handler.IndexOf("GainRefineryPacks.TryUse")
            && usage.IndexOf("HossuHairPacks.TryHandle")>usage.IndexOf("GainRefineryPacks.TryHandle")
            && thread.IndexOf("HossuHairPacks.Initialize")>thread.IndexOf("GainRefineryPacks.Initialize"),
            "HossuHairPacks intercepts sit after GainRefineryPacks and leftover 3006319 fails closed");
        Check(handler.IndexOf("GetPositionFromID")>=0 && handler.IndexOf("Inventory = 0")>=0,
            "ordinary 1009/action-4 still dispatches non-equipment IDs through UseItem");
        int switchStart=handler.IndexOf("switch (item.ID)"),switchEnd=handler.IndexOf("public static byte GetNextRefineryItem",switchStart);
        var sourceIds=System.Text.RegularExpressions.Regex.Matches(handler.Substring(switchStart,switchEnd-switchStart),@"(?m)^ {16}case (\d+):").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        var auditIds=System.Text.RegularExpressions.Regex.Matches(audit,@"(?m)^\| (\d+) \|").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        Check(sourceIds.Length==518 && sourceIds.OrderBy(id=>id).SequenceEqual(auditIds.OrderBy(id=>id)),
            "compound audit still enumerates the exact 518 numeric top-level UseItem case IDs");
        Check(audit.IndexOf("| live-split-write | 145 |")>=0 && audit.IndexOf("| owned-fail-closed | 275 |")>=0
            && audit.IndexOf("| single-consume | 55 |")>=0 && audit.IndexOf("| other | 43 |")>=0,
            "census is 145 live-split-write / 275 owned-fail-closed / 55 single-consume / 43 other after Hossu 3006319");
        Check(audit.IndexOf("| 3006319 | owned-fail-closed | source+items |")>=0,"audit owns FancyHossuHairPack 3006319");
        Check(HonorShop.Shop.Items.ContainsKey(3006319u) && HonorShop.Shop.Items[3006319u]==800u && honorIni.IndexOf("3006319,800,")>=0,
            "HonorShop UID 6000 stocks 3006319 at leftover cost 800");
        Check(catalog.IndexOf("3006319@@FancyHossuHairPack")>=0 && ConquerItemInformation.BaseInformations.ContainsKey(3006319u),
            "installed catalog identity for 3006319 is FancyHossuHairPack");
        Check(handler.IndexOf("case 3006320:")<0 && handler.IndexOf("case 3006321:")<0
            && HonorShop.Shop.Items.ContainsKey(3006320u) && HonorShop.Shop.Items.ContainsKey(3006321u)
            && catalog.IndexOf("3006320@@LuxuryHossuHairPack")>=0 && catalog.IndexOf("3006321@@LegendaryHossuHairPack")>=0,
            "HonorShop 3006320/3006321 stay stocked without a UseItem consumer");
        Check(monsters.IndexOf("Kernel.Random.Next(1, 9)")>=0 && monsters.IndexOf("Uid = 3006319;")>=0
            && monsters.IndexOf("ItemID = 3006319;")>=0 && monsters.IndexOf("Name == \"DrunkWatchman\"")>=0
            && monsters.IndexOf("Name == \"SnowBansheeSoul\"")>=0,
            "DrunkWatchman and SnowBanshee/PurpleBanshee leftover case 9 name 3006319 after Next(1,9)");
        Check(monsters.IndexOf("Kernel.Random.Next(1, 9)")>0,
            "leftover Next(1,9) yields 1-8 so case 9 is not ordinary issuance");
        string heard="";HossuHair.GetField("Spoken",Hidden).SetValue(null,(Action<string>)(m=>{heard=heard+m+"|";}));
        for(int n=0;n<hairs.Length;n++){
            Check(catalog.IndexOf(hairs[n].ToString()+"@@"+names[n])>=0 && ConquerItemInformation.BaseInformations.ContainsKey(hairs[n]),
                "reachable leftover Hossu hair "+hairs[n]+" is "+names[n]);
            hossuPick=n+1;heard="";var p=BallPlayer(town);var pack=Pack(p,3006319,1);OpenHossu(p,pack);
            Check(!Fenced(p) && HossuUses(p)==1 && !p.Inventory.ContainsUID(pack.UID) && CountId(p,hairs[n])==1
                && !p.Inventory.Objects.Single(i=>i.ID==hairs[n]).Bound && p.Inventory.Objects.Single(i=>i.ID==hairs[n]).Plus==0
                && heard.IndexOf("has obtained "+names[n])>=0,
                "leftover pick "+(n+1)+" opens unbound "+names[n]);
        }
        Check(catalog.IndexOf("3006170@@SacredHossuHair")>=0,"leftover SacredHossuHair 3006170 remains in catalog");
        hossuPick=6;var miss=BallPlayer(town);var missPack=Pack(miss,3006319,1);OpenHossu(miss,missPack);
        Check(!Fenced(miss) && HossuUses(miss)==0 && miss.Inventory.ContainsUID(missPack.UID) && CountId(miss,3006170)==0,
            "leftover Next(1,6) pick 6 / 3006170 stays unreachable and does not consume");
        hossuPick=1;
        var buyer=HonorBuyer(town,800);HonorBuy(buyer,3006319);
        Check(!Fenced(buyer) && CountId(buyer,3006319)==1 && buyer.CurrentHonor==0
            && Scalar("SELECT count(*) FROM revora_honor_purchases WHERE uid=@uid AND item_type=3006319",buyer.Entity.UID)==1,
            "ordinary HonorShop UID 6000 purchase issues unbound 3006319 at cost 800");
        var bought=buyer.Inventory.Objects.Single(i=>i.ID==3006319);OpenHossu(buyer,bought);
        Check(!Fenced(buyer) && HossuUses(buyer)==1 && !buyer.Inventory.ContainsUID(bought.UID) && CountId(buyer,3006165)==1,
            "Honor-issued 3006319 opens leftover NormalHossuHair");
        buyer=ReloadBall(buyer);
        Check(!Fenced(buyer) && CountId(buyer,3006165)==1 && HossuUses(buyer)==1,"Hossu hair survives production reload");
        var boundP=BallPlayer(town);var boundPack=NestedPack(boundP,3006319,1,true);OpenHossu(boundP,boundPack);
        Check(!Fenced(boundP) && CountId(boundP,3006165)==1 && boundP.Inventory.Objects.Single(i=>i.ID==3006165).Bound,
            "bound FancyHossuHairPack preserves bound hair");
        var piled=BallPlayer(town);var pile=Pack(piled,3006319,2);OpenHossu(piled,pile);
        Check(!Fenced(piled) && HossuUses(piled)==1 && pile.StackSize==1 && piled.Inventory.ContainsUID(pile.UID) && CountId(piled,3006165)==1,
            "stacked FancyHossuHairPack keeps the leftover remainder");
        var tight=BallPlayer(town);var tightPack=Pack(tight,3006319,1);while(tight.Inventory.Count<38)Pack(tight,1088001,1);OpenHossu(tight,tightPack);
        Check(!Fenced(tight) && HossuUses(tight)==1 && CountId(tight,3006165)==1,
            "leftover Count<=38 still opens at Count 38");
        var full=BallPlayer(town);var fullPack=Pack(full,3006319,1);while(full.Inventory.Count<39)Pack(full,1088001,1);OpenHossu(full,fullPack);
        Check(!Fenced(full) && HossuUses(full)==0 && full.Inventory.ContainsUID(fullPack.UID),
            "leftover Count<=38 refuses at Count 39 without consume");
        var race=BallPlayer(town);var raceItem=Pack(race,3006319,1);
        Parallel.For(0,16,n=>OpenHossu(race,raceItem));
        Check(!Fenced(race) && HossuUses(race)==1 && !race.Inventory.ContainsUID(raceItem.UID) && CountId(race,3006165)==1,
            "parallel packets cannot duplicate Hossu hair pack use");
        Query(@"CREATE OR REPLACE FUNCTION suppress_hossu() RETURNS trigger AS $$ BEGIN RAISE EXCEPTION 'suppressed'; END; $$ LANGUAGE plpgsql;
CREATE TRIGGER suppress_hossu BEFORE INSERT ON revora_hossu_hair_pack_uses FOR EACH ROW EXECUTE PROCEDURE suppress_hossu();");
        var fault=BallPlayer(town);var failItem=Pack(fault,3006319,1);
        try{OpenHossu(fault,failItem);
            Check(Fenced(fault) && HossuUses(fault)==0 && fault.Inventory.ContainsUID(failItem.UID),
                "suppressed Hossu persist fences without consuming the pack");}
        finally{Query("DROP TRIGGER IF EXISTS suppress_hossu ON revora_hossu_hair_pack_uses;DROP FUNCTION IF EXISTS suppress_hossu()");}
        fault=ReloadBall(fault);OpenHossu(fault,fault.Inventory.Objects.Single(i=>i.ID==3006319));
        Check(!Fenced(fault) && HossuUses(fault)==1 && CountId(fault,3006165)==1,"production reload retries Hossu pack after durable write failure");
        var direct=BallPlayer(town);var directPack=Pack(direct,3006319,1);ItemHandler.UseItem(directPack,direct);
        Check(!Fenced(direct) && HossuUses(direct)==1 && CountId(direct,3006165)==1,"direct UseItem opens leftover FancyHossuHairPack");
        var spent=BallPlayer(town);var spentPack=Pack(spent,3006319,1);OpenHossu(spent,spentPack);ItemHandler.UseItem(spentPack,spent);
        Check(!Fenced(spent) && HossuUses(spent)==1 && CountId(spent,3006165)==1,"consumed FancyHossuHairPack UID cannot replay");
    }
}
