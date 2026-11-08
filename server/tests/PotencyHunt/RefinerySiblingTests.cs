using System;
using System.IO;
using System.Linq;
using System.Text;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static void LoadSiblingBoxes(){
        Query("CREATE TABLE IF NOT EXISTS refinery (LIKE public.refinery INCLUDING ALL);INSERT INTO refinery SELECT * FROM public.refinery WHERE NOT EXISTS (SELECT 1 FROM refinery LIMIT 1);CREATE TABLE IF NOT EXISTS refineryboxes (LIKE public.refineryboxes INCLUDING ALL);INSERT INTO refineryboxes SELECT * FROM public.refineryboxes WHERE NOT EXISTS (SELECT 1 FROM refineryboxes LIMIT 1)");
        if(Kernel.DatabaseRefineryBoxes.Count==0)Refinery.LoadItems();
    }
    static void VerifyRefinerySiblingAudit(string runtime,Map town){
        Query("CREATE TABLE IF NOT EXISTS teamarena (LIKE public.teamarena INCLUDING ALL);CREATE TABLE IF NOT EXISTS itemadding (LIKE public.itemadding INCLUDING ALL)");
        LoadSiblingBoxes();
        Call(HonorBuys,null,"Initialize");HonorShop.Load();
        string handler=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemHandler.cs")));
        string usage=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemUsage.cs")));
        string thread=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Thread.cs")));
        string project=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Revora.csproj")));
        string questInfo=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/[1134 - 1135] - MsgTaskStatus.cs")));
        string audit=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../docs/ITEM_USE_COMPOUND_AUDIT.md")));
        string catalog=File.ReadAllText("database\\itemtype-new.txt");
        string honorIni=File.ReadAllText("database\\shops\\HonorShop.ini");
        string v2=File.ReadAllText("database\\shops\\emoneyshopV2.ini");
        string eshop=File.ReadAllText("database\\shops\\emoneyshop.ini");
        string shopTxt=File.ReadAllText("database\\shops\\Shop.txt");
        string exchange=File.ReadAllText("database\\shops\\exchange_shop_goods.txt");
        string heroes=File.ReadAllText("database\\Slayer\\StageGoal.txt");
        string drops=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Database/DropItemTable.cs")));
        string monsters=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Database/MonsterTable.cs")));
        string npcs=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Npcs.cs")));
        string questIni=File.ReadAllText("database\\Questinfo.ini");
        byte[] shopDat=File.ReadAllBytes("database\\shops\\Shop.dat");
        string shopDatText=Encoding.ASCII.GetString(shopDat);
        uint[] nextIds=new uint[]{723678u,723681u,723690u,724148u,724195u,724198u,724201u,724204u,724207u,724210u,724215u,725160u,725163u,725166u,725169u};
        string[] nextNames=new string[]{"Detoxication(Primary)Pack","Detoxication(Primary)Pack","Detoxication(Primary)Pack","Penetration(Primary)Pack","SkillCriticalStrike(Primary)Pack","Breakthrough(Primary)Pack","Breakthrough(Primary)Pack","Counteraction(Primary)Pack","Counteraction(Primary)Pack","Immunity(Primary)Pack","CriticalStrike(Primary)Pack","M-Defense(Primary)Pack","M-Defense(Primary)Pack","M-Defense(Primary)Pack","M-Defense(Primary)Pack"};
        uint[] gainIds=new uint[]{724130u,724133u,724151u,724154u,724157u,724160u,724163u,724166u,724169u,724172u,724175u,724178u,724181u,724184u,724190u};
        string[] gainNames=new string[]{"Penetration(Primary)Pack","SkillCriticalStrike(Primary)Pack","CriticalStrike(Primary)Pack","CriticalStrike(Primary)Pack","CriticalStrike(Primary)Pack","Breakthrough(Primary)Pack","Breakthrough(Primary)Pack","Breakthrough(Primary)Pack","Block(Primary)Pack","Detoxication(Primary)Pack","Detoxication(Primary)Pack","Detoxication(Primary)Pack","Detoxication(Primary)Pack","Penetration(Primary)Pack","Detoxication(Primary)Pack"};
        uint[] emptyIds=new uint[]{725055u,725056u,725057u,725058u};
        string[] emptyNames=new string[]{"CriticalStrike(Primary)Pack","SkillCriticalStrike(Primary)Pack","Breakthrough(Primary)Pack","Breakthrough(Primary)Pack"};
        uint[] siblingIds=nextIds.Concat(new uint[]{723693u}).Concat(gainIds).Concat(emptyIds).ToArray();
        Check(typeof(Entity).Assembly.GetType("Revora.Game.Features.RefinerySiblingPacks")==null
            && !File.Exists(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/RefinerySiblingPacks.cs"))),
            "unstocked refinery siblings are not compiled onto an owned service");
        Check(handler.IndexOf("GetPositionFromID")>=0 && handler.IndexOf("Inventory = 0")>=0,
            "ordinary 1009/action-4 still dispatches non-equipment IDs through UseItem");
        int switchStart=handler.IndexOf("switch (item.ID)"),switchEnd=handler.IndexOf("public static byte GetNextRefineryItem",switchStart);
        var sourceIds=System.Text.RegularExpressions.Regex.Matches(handler.Substring(switchStart,switchEnd-switchStart),@"(?m)^ {16}case (\d+):").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        var auditIds=System.Text.RegularExpressions.Regex.Matches(audit,@"(?m)^\| (\d+) \|").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        Check(sourceIds.Length==518 && sourceIds.OrderBy(id=>id).SequenceEqual(auditIds.OrderBy(id=>id)),
            "compound audit still enumerates the exact 518 numeric top-level UseItem case IDs");
        Check(audit.IndexOf("| live-split-write | 145 |")>=0 && audit.IndexOf("| owned-fail-closed | 275 |")>=0
            && audit.IndexOf("| single-consume | 55 |")>=0 && audit.IndexOf("| other | 43 |")>=0,
            "census stays 164 live-split-write / 256 owned-fail-closed / 55 single-consume / 43 other");
        Check(questInfo.IndexOf("split[0] == \"MissionId\"")>=0 && questInfo.IndexOf("split[0] == \"TypeId\"")>=0
            && questInfo.IndexOf("split[0] == \"Prize\"")<0,
            "QuestInfo.Load stores MissionId/TypeId and does not award Prize lines");
        Check(questIni.IndexOf("[item 724175,0,0,0,0,0,0]")>=0,
            "Questinfo.ini Prize copy names unstocked 724175 beside owned Power Arena packs");
        Check(handler.IndexOf("Kernel.Random.Next(1, 97)")>=0 && handler.IndexOf("client.Inventory.Add(ItemID, 0, 1);")>=0
            && handler.IndexOf("client.Inventory.Remove(item, Game.Enums.ItemUse.Delete);")>handler.IndexOf("case 723693:"),
            "leftover 723693 keeps Next(1,97), unbound Add, then ItemUse.Delete");
        Check(PublicScalar("SELECT count(*) FROM public.monsterinfos WHERE ownitem IN ("+string.Join(",",siblingIds)+")")==0,
            "public.monsterinfos.ownitem does not drop any remaining refinery sibling");
        for(int n=0;n<nextIds.Length;n++){
            uint id=nextIds[n];
            Check(audit.IndexOf("| "+id.ToString()+" | live-split-write | source+items |")>=0,"audit keeps GetNext primary "+id+" live-split-write");
            Check(ConquerItemInformation.BaseInformations.ContainsKey(id) && catalog.IndexOf(id.ToString()+"@@"+nextNames[n])>=0,
                "catalog identity for "+id+" is "+nextNames[n]);
            Check(!HonorShop.Shop.Items.ContainsKey(id) && honorIni.IndexOf(id.ToString()+",")<0
                && eshop.IndexOf(id.ToString())<0 && v2.IndexOf(id.ToString())<0 && shopTxt.IndexOf(id.ToString())<0
                && shopDatText.IndexOf(id.ToString())<0 && exchange.IndexOf(id.ToString())<0 && heroes.IndexOf(id.ToString())<0
                && drops.IndexOf(id.ToString())<0 && monsters.IndexOf(id.ToString())<0 && npcs.IndexOf("Add("+id.ToString())<0,
                "GetNext primary "+id+" has no HonorShop, emoney, Shop, exchange, StageGoal, NPC Add or drop producer");
            Check(handler.IndexOf("case "+id.ToString()+":")>=0 && NestedCase(handler,id).IndexOf("GetNextRefineryItem")>=0
                && NestedCase(handler,id).IndexOf("must use the owned service")<0,
                "unstocked GetNext primary "+id+" stays leftover live-split-write");
        }
        for(int k=0;k<gainIds.Length;k++){
            uint id=gainIds[k];
            Check(audit.IndexOf("| "+id.ToString()+" | other |")>=0,"audit keeps Gain primary "+id+" other");
            Check(ConquerItemInformation.BaseInformations.ContainsKey(id) && catalog.IndexOf(id.ToString()+"@@"+gainNames[k])>=0,
                "catalog identity for "+id+" is "+gainNames[k]);
            Check(!HonorShop.Shop.Items.ContainsKey(id) && honorIni.IndexOf(id.ToString()+",")<0
                && eshop.IndexOf(id.ToString())<0 && v2.IndexOf(id.ToString())<0 && shopTxt.IndexOf(id.ToString())<0
                && shopDatText.IndexOf(id.ToString())<0 && exchange.IndexOf(id.ToString())<0 && heroes.IndexOf(id.ToString())<0
                && drops.IndexOf(id.ToString())<0 && monsters.IndexOf(id.ToString())<0 && npcs.IndexOf("Add("+id.ToString())<0,
                "Gain primary "+id+" has no HonorShop, emoney, Shop, exchange, StageGoal, NPC Add or drop producer");
        }
        for(int m=0;m<emptyIds.Length;m++){
            uint id=emptyIds[m];
            Check(audit.IndexOf("| "+id.ToString()+" | other |")>=0,"audit keeps empty-box "+id+" other");
            Check(ConquerItemInformation.BaseInformations.ContainsKey(id) && catalog.IndexOf(id.ToString()+"@@"+emptyNames[m])>=0,
                "catalog identity for "+id+" is "+emptyNames[m]);
            Check(!HonorShop.Shop.Items.ContainsKey(id) && honorIni.IndexOf(id.ToString()+",")<0
                && eshop.IndexOf(id.ToString())<0 && v2.IndexOf(id.ToString())<0 && shopTxt.IndexOf(id.ToString())<0
                && shopDatText.IndexOf(id.ToString())<0 && exchange.IndexOf(id.ToString())<0 && heroes.IndexOf(id.ToString())<0
                && drops.IndexOf(id.ToString())<0 && monsters.IndexOf(id.ToString())<0 && npcs.IndexOf("Add("+id.ToString())<0,
                "empty-box "+id+" has no HonorShop, emoney, Shop, exchange, StageGoal, NPC Add or drop producer");
            Check(Kernel.DatabaseRefineryBoxes.ContainsKey(id),
                "installed refineryboxes still lists empty-box "+id);
        }
        Check(audit.IndexOf("| 723693 | live-split-write | source+items |")>=0
            && catalog.IndexOf("723693@@LegendRefineryPack")>=0 && !HonorShop.Shop.Items.ContainsKey(723693u)
            && honorIni.IndexOf("723693,")<0 && eshop.IndexOf("723693")<0 && v2.IndexOf("723693")<0
            && shopTxt.IndexOf("723693")<0 && exchange.IndexOf("723693")<0 && heroes.IndexOf("723693")<0
            && drops.IndexOf("723693")<0 && monsters.IndexOf("723693")<0 && npcs.IndexOf("Add(723693")<0,
            "LegendRefineryPack 723693 stays leftover live-split-write with no ordinary producer");
        uint[] missingLegendRewards=new uint[]{724378u,724379u,724395u,724396u,724397u,724398u,724399u};
        Check(missingLegendRewards.All(id=>!ConquerItemInformation.BaseInformations.ContainsKey(id)
            && catalog.IndexOf(id.ToString()+"@@")<0),
            "all seven reachable missing-catalog LegendRefineryPack outcomes remain absent");
        Check(HonorShop.Shop.Items.ContainsKey(3006319u) && honorIni.IndexOf("3006319,")>=0
            && audit.IndexOf("| 3006319 | owned-fail-closed | source+items |")>=0
            && catalog.IndexOf("3006319@@FancyHossuHairPack")>=0,
            "HonorShop FancyHossuHairPack 3006319 is owned");
        Check(HonorShop.Shop.Items.ContainsKey(3006320u) && HonorShop.Shop.Items.ContainsKey(3006321u)
            && handler.IndexOf("case 3006320:")<0 && handler.IndexOf("case 3006321:")<0,
            "HonorShop 3006320/3006321 remain stocked without a UseItem consumer");
        var missed=HonorBuyer(town,8000);HonorBuy(missed,723678);HonorBuy(missed,724130);HonorBuy(missed,723693);
        Check(!Fenced(missed) && missed.Inventory.Count==0 && missed.CurrentHonor==8000
            && Scalar("SELECT count(*) FROM revora_honor_purchases WHERE uid=@uid",missed.Entity.UID)==0,
            "ordinary UID 6000 buy of unstocked refinery siblings issues nothing and does not debit");
        var nextP=BallPlayer(town);var nextPack=Pack(nextP,723678,1);ItemHandler.UseItem(nextPack,nextP);
        Check(!Fenced(nextP) && !nextP.Inventory.ContainsUID(nextPack.UID) && nextP.Inventory.Count==1,
            "unstocked GetNext primary 723678 stays leftover live-split-write");
        var gainP=BallPlayer(town);var gainPack=Pack(gainP,724130,1);ItemHandler.UseItem(gainPack,gainP);
        Check(!Fenced(gainP) && !gainP.Inventory.ContainsUID(gainPack.UID) && gainP.Inventory.Count==1,
            "unstocked Gain primary 724130 stays leftover GainRefineryItem");
        var emptyP=BallPlayer(town);var emptyPack=Pack(emptyP,725055,1);ItemHandler.UseItem(emptyPack,emptyP);
        Check(!Fenced(emptyP) && emptyP.Inventory.ContainsUID(emptyPack.UID),
            "empty-box 725055 leftover GainRefineryItem does not consume");
        var legend=BallPlayer(town);var legendPack=Pack(legend,723693,1);ItemHandler.UseItem(legendPack,legend);
        Check(!Fenced(legend) && !legend.Inventory.ContainsUID(legendPack.UID) && legend.Inventory.Count<=1
            && legend.Inventory.Objects.All(i=>!i.Bound && i.Plus==0),
            "unstocked LegendRefineryPack 723693 stays leftover Add-then-Delete");
    }
}
