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
    static readonly Type HonorRefinery=typeof(Entity).Assembly.GetType("Revora.Game.Features.HonorRefineryPacks");
    static DateTime honorRefineNow=DateTime.UtcNow;
    static long HonorRefineUses(GameState p){return Scalar("SELECT count(*) FROM revora_honor_refinery_pack_uses WHERE uid=@uid",p.Entity.UID);}
    static void OpenHonorRefine(GameState p,ConquerItem item){ItemUsage.Process(PrayerPacket(item.UID),p);}
    static void VerifyHonorRefineryPacks(string runtime,Map town){
        Query("CREATE TABLE IF NOT EXISTS teamarena (LIKE public.teamarena INCLUDING ALL);CREATE TABLE IF NOT EXISTS itemadding (LIKE public.itemadding INCLUDING ALL)");
        Call(HonorRefinery,null,"Initialize");Call(HonorRefinery,null,"Initialize");
        Call(HonorBuys,null,"Initialize");HonorShop.Load();
        HonorRefinery.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>honorRefineNow));
        HonorRefinery.GetField("Extra",Hidden).SetValue(null,(Func<byte>)(()=>(byte)0));
        string handler=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemHandler.cs")));
        string usage=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemUsage.cs")));
        string thread=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Thread.cs")));
        string audit=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../docs/ITEM_USE_COMPOUND_AUDIT.md")));
        string honorIni=File.ReadAllText("database\\shops\\HonorShop.ini");
        uint[] owned=new uint[]{723679u,723680u,723682u,723683u,723691u,723692u,724149u,724150u,724193u,724194u,724196u,724197u,724199u,724200u,724202u,724203u,724205u,724206u,724208u,724209u,724211u,724212u,725161u,725162u,725164u,725165u,725167u,725168u,725170u,725171u};
        Check(handler.IndexOf("Honor refinery pack must use the owned service.")>=0 && handler.IndexOf("HonorRefineryPacks.TryUse")>handler.IndexOf("BloodshedSeaBoosts.TryUse")
            && usage.IndexOf("HonorRefineryPacks.TryHandle")>usage.IndexOf("BloodshedSeaBoosts.TryHandle")
            && thread.IndexOf("HonorRefineryPacks.Initialize")>thread.IndexOf("BloodshedSeaBoosts.Initialize"),
            "HonorRefineryPacks intercepts sit after BloodshedSeaBoosts and leftover HonorShop packs fail closed");
        Check(handler.IndexOf("GetPositionFromID")>=0 && handler.IndexOf("Inventory = 0")>=0,
            "ordinary 1009/action-4 still dispatches non-equipment IDs through UseItem");
        int switchStart=handler.IndexOf("switch (item.ID)"),switchEnd=handler.IndexOf("public static byte GetNextRefineryItem",switchStart);
        var sourceIds=System.Text.RegularExpressions.Regex.Matches(handler.Substring(switchStart,switchEnd-switchStart),@"(?m)^ {16}case (\d+):").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        var auditIds=System.Text.RegularExpressions.Regex.Matches(audit,@"(?m)^\| (\d+) \|").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        Check(sourceIds.Length==518 && sourceIds.OrderBy(id=>id).SequenceEqual(auditIds.OrderBy(id=>id)),
            "compound audit still enumerates the exact 518 numeric top-level UseItem case IDs");
        Check(audit.IndexOf("| live-split-write | 145 |")>=0 && audit.IndexOf("| owned-fail-closed | 275 |")>=0,
            "census is 145 live-split-write / 275 owned-fail-closed after HonorShop leftover packs");
        for(int n=0;n<owned.Length;n++){
            Check(HonorShop.Shop.Items.ContainsKey(owned[n]) && honorIni.IndexOf(owned[n].ToString()+",")>=0,
                "HonorShop.ini stocks leftover GetNextRefineryItem pack "+owned[n]);
            Check(audit.IndexOf("| "+owned[n].ToString()+" | owned-fail-closed | source+items |")>=0,
                "audit owns HonorShop leftover pack "+owned[n]);
        }
        Check(audit.IndexOf("| 723678 | live-split-write | source+items |")>=0 && audit.IndexOf("| 724148 | live-split-write | source+items |")>=0,
            "unstocked primary leftover GetNextRefineryItem IDs stay live-split-write");
        var buyer=HonorBuyer(town,8000);HonorBuy(buyer,723679);
        Check(!Fenced(buyer) && CountId(buyer,723679)==1 && buyer.CurrentHonor==0
            && Scalar("SELECT count(*) FROM revora_honor_purchases WHERE uid=@uid AND item_type=723679",buyer.Entity.UID)==1,
            "ordinary HonorShop UID 6000 purchase issues unbound 723679");
        var bought=buyer.Inventory.Objects.Single(i=>i.ID==723679);OpenHonorRefine(buyer,bought);
        Check(!Fenced(buyer) && HonorRefineUses(buyer)==1 && !buyer.Inventory.ContainsUID(bought.UID) && CountId(buyer,724494)==1,
            "Honor-issued 723679 opens leftover extra-0 Detoxication material 724494");
        buyer=ReloadBall(buyer);
        Check(!Fenced(buyer) && CountId(buyer,724494)==1 && HonorRefineUses(buyer)==1,"Honor refinery material survives production reload");
        HonorRefinery.GetField("Extra",Hidden).SetValue(null,(Func<byte>)(()=>(byte)1));
        var mid=BallPlayer(town);var midPack=Pack(mid,723679,1);OpenHonorRefine(mid,midPack);
        Check(!Fenced(mid) && CountId(mid,724495)==1,"leftover extra 1 opens 724495");
        HonorRefinery.GetField("Extra",Hidden).SetValue(null,(Func<byte>)(()=>(byte)2));
        var high=BallPlayer(town);var highPack=Pack(high,723679,1);OpenHonorRefine(high,highPack);
        Check(!Fenced(high) && CountId(high,724496)==1,"leftover extra 2 opens 724496");
        HonorRefinery.GetField("Extra",Hidden).SetValue(null,(Func<byte>)(()=>(byte)0));
        var neck=BallPlayer(town);var neckPack=Pack(neck,723682,1);OpenHonorRefine(neck,neckPack);
        Check(!Fenced(neck) && CountId(neck,724499)==1,"leftover necklace remap 724349+=150 yields 724499");
        HonorRefinery.GetField("Extra",Hidden).SetValue(null,(Func<byte>)(()=>(byte)1));
        var ring=BallPlayer(town);var ringPack=Pack(ring,724199,1);OpenHonorRefine(ring,ringPack);
        Check(!Fenced(ring) && CountId(ring,724470)==1,"leftover ring remap >=724466 +=4 yields 724470");
        HonorRefinery.GetField("Extra",Hidden).SetValue(null,(Func<byte>)(()=>(byte)0));
        var boundP=BallPlayer(town);var boundPack=NestedPack(boundP,723679,1,true);OpenHonorRefine(boundP,boundPack);
        Check(!Fenced(boundP) && boundP.Inventory.Objects.Single(i=>i.ID==724494).Bound,"owned Honor refinery pack copies leftover Bound onto the material");
        var piled=BallPlayer(town);var pile=Pack(piled,723679,2);OpenHonorRefine(piled,pile);
        Check(!Fenced(piled) && HonorRefineUses(piled)==1 && pile.StackSize==1 && piled.Inventory.ContainsUID(pile.UID) && CountId(piled,724494)==1,
            "stacked Honor refinery pack keeps the leftover remainder");
        var leftover=BallPlayer(town);var primary=Pack(leftover,723678,1);ItemHandler.UseItem(primary,leftover);
        Check(!Fenced(leftover) && !leftover.Inventory.ContainsUID(primary.UID) && leftover.Inventory.Count==1,
            "unstocked primary 723678 stays leftover live-split-write");
        var race=BallPlayer(town);var raceItem=Pack(race,723679,1);
        Parallel.For(0,16,n=>OpenHonorRefine(race,raceItem));
        Check(!Fenced(race) && HonorRefineUses(race)==1 && !race.Inventory.ContainsUID(raceItem.UID) && CountId(race,724494)==1,
            "parallel packets cannot duplicate Honor refinery pack use");
        Query(@"CREATE OR REPLACE FUNCTION suppress_honor_refine() RETURNS trigger AS $$ BEGIN RAISE EXCEPTION 'suppressed'; END; $$ LANGUAGE plpgsql;
CREATE TRIGGER suppress_honor_refine BEFORE INSERT ON revora_honor_refinery_pack_uses FOR EACH ROW EXECUTE PROCEDURE suppress_honor_refine();");
        var fault=BallPlayer(town);var failItem=Pack(fault,723679,1);
        try{OpenHonorRefine(fault,failItem);
            Check(Fenced(fault) && HonorRefineUses(fault)==0 && fault.Inventory.ContainsUID(failItem.UID),
                "suppressed Honor refinery persist fences without consuming the pack");}
        finally{Query("DROP TRIGGER IF EXISTS suppress_honor_refine ON revora_honor_refinery_pack_uses;DROP FUNCTION IF EXISTS suppress_honor_refine()");}
        fault=ReloadBall(fault);OpenHonorRefine(fault,fault.Inventory.Objects.Single(i=>i.ID==723679));
        Check(!Fenced(fault) && HonorRefineUses(fault)==1 && CountId(fault,724494)==1,"production reload retries Honor refinery pack after durable write failure");
        var direct=BallPlayer(town);var directPack=Pack(direct,725161,1);ItemHandler.UseItem(directPack,direct);
        Check(!Fenced(direct) && HonorRefineUses(direct)==1 && CountId(direct,725193)==1,"direct UseItem opens leftover M-Defense 725161 extra 0 as 725193");
    }
}
