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
    static readonly Type GainRefine=typeof(Entity).Assembly.GetType("Revora.Game.Features.GainRefineryPacks");
    static DateTime gainRefineNow=DateTime.UtcNow;
    static long GainRefineUses(GameState p){return Scalar("SELECT count(*) FROM revora_gain_refinery_pack_uses WHERE uid=@uid",p.Entity.UID);}
    static void OpenGainRefine(GameState p,ConquerItem item){ItemUsage.Process(PrayerPacket(item.UID),p);}
    static uint[] GainPool(uint pack){return (uint[])Call(GainRefine,null,"Possible",pack);}
    static void LoadGainBoxes(){
        Query("CREATE TABLE IF NOT EXISTS refinery (LIKE public.refinery INCLUDING ALL);INSERT INTO refinery SELECT * FROM public.refinery WHERE NOT EXISTS (SELECT 1 FROM refinery LIMIT 1);CREATE TABLE IF NOT EXISTS refineryboxes (LIKE public.refineryboxes INCLUDING ALL);INSERT INTO refineryboxes SELECT * FROM public.refineryboxes WHERE NOT EXISTS (SELECT 1 FROM refineryboxes LIMIT 1)");
        if(Kernel.DatabaseRefineryBoxes.Count==0)Refinery.LoadItems();
    }
    static void VerifyGainRefineryPacks(string runtime,Map town){
        Query("CREATE TABLE IF NOT EXISTS teamarena (LIKE public.teamarena INCLUDING ALL);CREATE TABLE IF NOT EXISTS itemadding (LIKE public.itemadding INCLUDING ALL)");
        LoadGainBoxes();
        Call(GainRefine,null,"Initialize");Call(GainRefine,null,"Initialize");
        Call(HonorBuys,null,"Initialize");HonorShop.Load();
        GainRefine.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>gainRefineNow));
        GainRefine.GetField("Pick",Hidden).SetValue(null,(Func<int,int>)(count=>1));
        string handler=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemHandler.cs")));
        string usage=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemUsage.cs")));
        string thread=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Thread.cs")));
        string audit=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../docs/ITEM_USE_COMPOUND_AUDIT.md")));
        string honorIni=File.ReadAllText("database\\shops\\HonorShop.ini");
        string catalog=File.ReadAllText("database\\itemtype-new.txt");
        string shopDat=File.ReadAllText("database\\shops\\Shop.dat");
        string shopTxt=File.ReadAllText("database\\shops\\Shop.txt");
        string eshop=File.ReadAllText("database\\shops\\emoneyshop.ini");
        string v2=File.ReadAllText("database\\shops\\emoneyshopV2.ini");
        string exchange=File.ReadAllText("database\\shops\\exchange_shop_goods.txt");
        uint[] owned=new uint[]{724131u,724132u,724134u,724135u,724152u,724153u,724155u,724156u,724158u,724159u,724161u,724162u,724164u,724165u,724167u,724168u,724170u,724171u,724173u,724174u,724176u,724177u,724179u,724180u,724182u,724183u,724185u,724186u,724191u,724192u};
        uint[] leftover=new uint[]{724130u,724133u,724151u,724154u,724157u,724160u,724163u,724166u,724169u,724172u,724175u,724178u,724181u,724184u,724190u,725055u,725056u,725057u,725058u};
        Check(handler.IndexOf("Gain refinery pack must use the owned service.")>=0 && handler.IndexOf("GainRefineryPacks.TryUse")>handler.IndexOf("HonorRefineryPacks.TryUse")
            && usage.IndexOf("GainRefineryPacks.TryHandle")>usage.IndexOf("HonorRefineryPacks.TryHandle")
            && thread.IndexOf("GainRefineryPacks.Initialize")>thread.IndexOf("HonorRefineryPacks.Initialize"),
            "GainRefineryPacks intercepts sit after HonorRefineryPacks and leftover HonorShop GainRefineryItem packs fail closed");
        Check(handler.IndexOf("GetPositionFromID")>=0 && handler.IndexOf("Inventory = 0")>=0,
            "ordinary 1009/action-4 still dispatches non-equipment IDs through UseItem");
        int switchStart=handler.IndexOf("switch (item.ID)"),switchEnd=handler.IndexOf("public static byte GetNextRefineryItem",switchStart);
        var sourceIds=System.Text.RegularExpressions.Regex.Matches(handler.Substring(switchStart,switchEnd-switchStart),@"(?m)^ {16}case (\d+):").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        var auditIds=System.Text.RegularExpressions.Regex.Matches(audit,@"(?m)^\| (\d+) \|").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        Check(sourceIds.Length==518 && sourceIds.OrderBy(id=>id).SequenceEqual(auditIds.OrderBy(id=>id)),
            "compound audit still enumerates the exact 518 numeric top-level UseItem case IDs");
        Check(audit.IndexOf("| live-split-write | 145 |")>=0 && audit.IndexOf("| owned-fail-closed | 275 |")>=0
            && audit.IndexOf("| single-consume | 55 |")>=0 && audit.IndexOf("| other | 43 |")>=0,
            "census is 145 live-split-write / 275 owned-fail-closed / 55 single-consume / 43 other after GainRefineryItem packs");
        for(int n=0;n<owned.Length;n++){
            Check(HonorShop.Shop.Items.ContainsKey(owned[n]) && honorIni.IndexOf(owned[n].ToString()+",")>=0,
                "HonorShop.ini stocks leftover GainRefineryItem pack "+owned[n]);
            Check(audit.IndexOf("| "+owned[n].ToString()+" | owned-fail-closed | source+items |")>=0,
                "audit owns HonorShop leftover GainRefineryItem pack "+owned[n]);
            Check(catalog.IndexOf(owned[n].ToString()+"@@")>=0 && ConquerItemInformation.BaseInformations.ContainsKey(owned[n]),
                "installed catalog contains GainRefineryItem pack "+owned[n]);
            Check(shopDat.IndexOf(owned[n].ToString())<0 && shopTxt.IndexOf(owned[n].ToString())<0
                && eshop.IndexOf(owned[n].ToString())<0 && v2.IndexOf(owned[n].ToString())<0 && exchange.IndexOf(owned[n].ToString())<0,
                "gold/CP shops do not stock HonorShop GainRefineryItem pack "+owned[n]);
            uint[] pool=GainPool(owned[n]);
            Check(pool.Length>=2 && pool.Skip(1).All(id=>ConquerItemInformation.BaseInformations.ContainsKey(id)),
                "every reachable leftover DatabaseRefineryBoxes pick for "+owned[n]+" is installed");
        }
        for(int k=0;k<leftover.Length;k++){
            Check(!HonorShop.Shop.Items.ContainsKey(leftover[k]) && honorIni.IndexOf(leftover[k].ToString()+",")<0,
                "HonorShop does not stock leftover GainRefineryItem "+leftover[k]);
            Check(audit.IndexOf("| "+leftover[k].ToString()+" | other |")>=0,
                "unstocked GainRefineryItem "+leftover[k]+" stays other");
        }
        Check(audit.IndexOf("| 723678 | live-split-write | source+items |")>=0 && audit.IndexOf("| 724148 | live-split-write | source+items |")>=0,
            "unstocked primary leftover GetNextRefineryItem IDs stay live-split-write");
        var buyer=HonorBuyer(town,800);HonorBuy(buyer,724131);
        Check(!Fenced(buyer) && CountId(buyer,724131)==1 && buyer.CurrentHonor==0
            && Scalar("SELECT count(*) FROM revora_honor_purchases WHERE uid=@uid AND item_type=724131",buyer.Entity.UID)==1,
            "ordinary HonorShop UID 6000 purchase issues unbound 724131");
        var bought=buyer.Inventory.Objects.Single(i=>i.ID==724131);uint expect=GainPool(724131u)[1];OpenGainRefine(buyer,bought);
        Check(!Fenced(buyer) && GainRefineUses(buyer)==1 && !buyer.Inventory.ContainsUID(bought.UID) && CountId(buyer,expect)==1
            && !buyer.Inventory.Objects.Single(i=>i.ID==expect).Bound && buyer.Inventory.Objects.Single(i=>i.ID==expect).Plus==0,
            "Honor-issued 724131 opens leftover DatabaseRefineryBoxes pick-1 material unbound");
        buyer=ReloadBall(buyer);
        Check(!Fenced(buyer) && CountId(buyer,expect)==1 && GainRefineUses(buyer)==1,"Gain refinery material survives production reload");
        uint skip=GainPool(724131u)[0];
        Check(expect!=skip,"leftover Next(1,count) skips DatabaseRefineryBoxes index 0");
        var boundP=BallPlayer(town);var boundPack=NestedPack(boundP,724131,1,true);OpenGainRefine(boundP,boundPack);
        Check(!Fenced(boundP) && CountId(boundP,expect)==1 && boundP.Inventory.Objects.Single(i=>i.ID==expect).Bound,
            "owned GainRefineryItem pack preserves bound material");
        var piled=BallPlayer(town);var pile=Pack(piled,724131,2);OpenGainRefine(piled,pile);
        Check(!Fenced(piled) && GainRefineUses(piled)==1 && pile.StackSize==1 && piled.Inventory.ContainsUID(pile.UID) && CountId(piled,expect)==1,
            "stacked GainRefineryItem pack keeps the leftover remainder");
        var leftoverPrimary=BallPlayer(town);var primary=Pack(leftoverPrimary,724130,1);ItemHandler.UseItem(primary,leftoverPrimary);
        Check(!Fenced(leftoverPrimary) && !leftoverPrimary.Inventory.ContainsUID(primary.UID) && leftoverPrimary.Inventory.Count==1,
            "unstocked primary 724130 stays leftover GainRefineryItem live-split-write");
        var empty=BallPlayer(town);var emptyPack=Pack(empty,725055,1);ItemHandler.UseItem(emptyPack,empty);
        Check(!Fenced(empty) && empty.Inventory.ContainsUID(emptyPack.UID) && GainPool(725055u).Length==0,
            "unstocked empty-box 725055 leftover GainRefineryItem does not consume");
        var race=BallPlayer(town);var raceItem=Pack(race,724131,1);
        Parallel.For(0,16,n=>OpenGainRefine(race,raceItem));
        Check(!Fenced(race) && GainRefineUses(race)==1 && !race.Inventory.ContainsUID(raceItem.UID) && CountId(race,expect)==1,
            "parallel packets cannot duplicate GainRefineryItem pack use");
        Query(@"CREATE OR REPLACE FUNCTION suppress_gain_refine() RETURNS trigger AS $$ BEGIN RAISE EXCEPTION 'suppressed'; END; $$ LANGUAGE plpgsql;
CREATE TRIGGER suppress_gain_refine BEFORE INSERT ON revora_gain_refinery_pack_uses FOR EACH ROW EXECUTE PROCEDURE suppress_gain_refine();");
        var fault=BallPlayer(town);var failItem=Pack(fault,724131,1);
        try{OpenGainRefine(fault,failItem);
            Check(Fenced(fault) && GainRefineUses(fault)==0 && fault.Inventory.ContainsUID(failItem.UID),
                "suppressed Gain refinery persist fences without consuming the pack");}
        finally{Query("DROP TRIGGER IF EXISTS suppress_gain_refine ON revora_gain_refinery_pack_uses;DROP FUNCTION IF EXISTS suppress_gain_refine()");}
        fault=ReloadBall(fault);OpenGainRefine(fault,fault.Inventory.Objects.Single(i=>i.ID==724131));
        Check(!Fenced(fault) && GainRefineUses(fault)==1 && CountId(fault,expect)==1,"production reload retries Gain refinery pack after durable write failure");
        var direct=BallPlayer(town);var directPack=Pack(direct,724155,1);uint directExpect=GainPool(724155u)[1];ItemHandler.UseItem(directPack,direct);
        Check(!Fenced(direct) && GainRefineUses(direct)==1 && CountId(direct,directExpect)==1,"direct UseItem opens leftover HonorShop GainRefineryItem 724155");
        var full=BallPlayer(town);var fullPack=Pack(full,724131,2);while(full.Inventory.Count<40)Pack(full,1088001,1);OpenGainRefine(full,fullPack);
        Check(!Fenced(full) && GainRefineUses(full)==0 && fullPack.StackSize==2 && full.Inventory.ContainsUID(fullPack.UID),
            "full bag refuses a stacked GainRefineryItem open before consume");
        var spent=BallPlayer(town);var spentPack=Pack(spent,724131,1);OpenGainRefine(spent,spentPack);ItemHandler.UseItem(spentPack,spent);
        Check(!Fenced(spent) && GainRefineUses(spent)==1 && CountId(spent,expect)==1,"consumed GainRefineryItem UID cannot replay");
        for(int m=0;m<owned.Length;m++){
            var p=BallPlayer(town);var pack=Pack(p,owned[m],1);uint result=GainPool(owned[m])[1];OpenGainRefine(p,pack);
            Check(!Fenced(p) && GainRefineUses(p)==1 && CountId(p,result)==1 && !p.Inventory.ContainsUID(pack.UID),
                "owned leftover pick-1 opens HonorShop GainRefineryItem "+owned[m]);
        }
    }
}
