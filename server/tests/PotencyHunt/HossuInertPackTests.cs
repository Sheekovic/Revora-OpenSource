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
    static void VerifyHossuInertPacks(string runtime,Map town){
        Query("CREATE TABLE IF NOT EXISTS teamarena (LIKE public.teamarena INCLUDING ALL);CREATE TABLE IF NOT EXISTS itemadding (LIKE public.itemadding INCLUDING ALL)");
        Call(HonorBuys,null,"Initialize");HonorShop.Load();
        Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.HossuHairPacks"),null,"Initialize");
        string handler=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemHandler.cs")));
        string honor=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/HonorPurchases.cs")));
        string hossu=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/HossuHairPacks.cs")));
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
        string shopDatText=Encoding.ASCII.GetString(File.ReadAllBytes("database\\shops\\Shop.dat"));
        uint[] inert=new uint[]{3006316u,3006317u,3006318u,3006320u,3006321u};
        uint[] costs=new uint[]{8000u,20000u,30000u,2000u,4000u};
        string[] names=new string[]{"FancyHossuHairPack","LuxuryHossuHairPack","LegendaryHossuHairPack","LuxuryHossuHairPack","LegendaryHossuHairPack"};
        Check(HonorShop.UID==6000 && handler.IndexOf("if (itemUsage.UID == Database.HonorShop.UID)")>=0
            && handler.IndexOf("HonorPurchases.Buy(client, itemUsage.dwParam)")>=0,
            "ordinary HandleBuyFromNPC UID 6000 dispatches to HonorPurchases.Buy");
        Check(handler.IndexOf("GetPositionFromID")>=0 && handler.IndexOf("Inventory = 0")>=0,
            "ordinary 1009/action-4 still dispatches non-equipment IDs through UseItem");
        int switchStart=handler.IndexOf("switch (item.ID)"),switchEnd=handler.IndexOf("public static byte GetNextRefineryItem",switchStart);
        var sourceIds=System.Text.RegularExpressions.Regex.Matches(handler.Substring(switchStart,switchEnd-switchStart),@"(?m)^ {16}case (\d+):").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        var auditIds=System.Text.RegularExpressions.Regex.Matches(audit,@"(?m)^\| (\d+) \|").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        Check(sourceIds.Length==518 && sourceIds.OrderBy(id=>id).SequenceEqual(auditIds.OrderBy(id=>id)),
            "compound audit still enumerates the exact 518 numeric top-level UseItem case IDs");
        Check(audit.IndexOf("| live-split-write | 145 |")>=0 && audit.IndexOf("| owned-fail-closed | 275 |")>=0
            && audit.IndexOf("| single-consume | 55 |")>=0 && audit.IndexOf("| other | 43 |")>=0,
            "census stays 164 live-split-write / 256 owned-fail-closed; inert Hossu packs are not UseItem cases");
        Check(handler.IndexOf("case 3006319:")>=0 && hossu.IndexOf("Pack=3006319")>=0
            && hossu.IndexOf("uint[] Rewards=new uint[]{3006165,3006166,3006167,3006168,3006169}")>=0
            && hossu.IndexOf("{3006319,0}")>=0 && honor.IndexOf("3006319")<0,
            "owned 3006319 leftover mapping is unchanged and HonorPurchases does not refuse it");
        Check(honor.IndexOf("3006316u")>=0 && honor.IndexOf("3006317u")>=0 && honor.IndexOf("3006318u")>=0
            && honor.IndexOf("3006320u")>=0 && honor.IndexOf("3006321u")>=0 && honor.IndexOf("This item cannot be used.")>=0,
            "HonorPurchases refuses Honor spend on inert Hossu packs");
        Check(ItemHandler.GetPositionFromID(3006320)==ItemHandler.Positions.Inventory
            && ItemHandler.GetPositionFromID(3006321)==ItemHandler.Positions.Inventory
            && ItemHandler.GetPositionFromID(3006317)==ItemHandler.Positions.Inventory,
            "inert Hossu packs still dispatch through inventory UseItem");
        Check(catalog.IndexOf("[Hossu][MaxHP]GiftPack")>=0
            && catalog.IndexOf("random~refinery~material~for~Hossu")>=0,
            "catalog Hossu pack copy is client text, not leftover UseItem mapping");
        Check(PublicScalar("SELECT count(*) FROM public.monsterinfos WHERE ownitem IN (3006316,3006317,3006318,3006320,3006321)")==0,
            "public.monsterinfos.ownitem does not drop inert Hossu packs");
        for(int n=0;n<inert.Length;n++){
            uint id=inert[n];
            Check(handler.IndexOf("case "+id.ToString()+":")<0,"UseItem has no leftover case for "+id);
            Check(HonorShop.Shop.Items.ContainsKey(id) && HonorShop.Shop.Items[id]==costs[n]
                && honorIni.IndexOf(id.ToString()+","+costs[n].ToString()+",")>=0,
                "HonorShop UID 6000 stocks inert "+id+" at cost "+costs[n]);
            Check(catalog.IndexOf(id.ToString()+"@@"+names[n])>=0 && ConquerItemInformation.BaseInformations.ContainsKey(id)
                && ConquerItemInformation.BaseInformations[id].Name==names[n],
                "catalog identity for "+id+" is "+names[n]);
            Check(honorIni.IndexOf(id.ToString()+"=Open~to~get~a~random~bundled~Hossu~Hair")>=0,
                "HonorShop recommend copy for "+id+" is client text, not a leftover UseItem mapping");
            Check(eshop.IndexOf(id.ToString())<0 && v2.IndexOf(id.ToString())<0 && shopTxt.IndexOf(id.ToString())<0
                && shopDatText.IndexOf(id.ToString())<0 && exchange.IndexOf(id.ToString())<0 && heroes.IndexOf(id.ToString())<0
                && drops.IndexOf(id.ToString())<0 && monsters.IndexOf(id.ToString())<0 && npcs.IndexOf("Add("+id.ToString())<0
                && questIni.IndexOf(id.ToString())<0,
                "inert "+id+" has no emoney, Shop, exchange, StageGoal, NPC Add, drop or Questinfo producer");
            var buyer=HonorBuyer(town,costs[n]);HonorBuy(buyer,id);
            Check(!Fenced(buyer) && buyer.Inventory.Count==0 && buyer.CurrentHonor==costs[n]
                && Scalar("SELECT count(*) FROM revora_honor_purchases WHERE uid=@uid AND item_type="+(int)id,buyer.Entity.UID)==0
                && Scalar("SELECT currenthonor FROM arena WHERE entityid=@uid",buyer.Entity.UID)==77
                && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id="+(int)id,buyer.Entity.UID)==0,
                "ordinary UID 6000 buy of inert "+id+" issues nothing and does not debit Honor");
            var held=BallPlayer(town);var pack=Pack(held,id,1);ItemUsage.Process(PrayerPacket(pack.UID),held);ItemHandler.UseItem(pack,held);
            Check(!Fenced(held) && held.Inventory.ContainsUID(pack.UID) && held.Inventory.Count==1
                && Scalar("SELECT count(*) FROM items WHERE uid=@uid AND id="+(int)id,pack.UID)==1,
                "already-held inert "+id+" is preserved and leftover UseItem does not consume it");
        }
        Check(catalog.IndexOf("3006159@@NormalHossuHair(B)")>=0 && catalog.IndexOf("3006164@@SacredHossuHair(B)")>=0
            && catalog.IndexOf("3006171@@BlankBook")>=0,
            "catalog has bound Hossu hair 3006159-3006164 then BlankBook 3006171; no extra luxury/legendary hair IDs");
        var ok=HonorBuyer(town,800);HonorBuy(ok,3006319);
        Check(!Fenced(ok) && CountId(ok,3006319)==1 && ok.CurrentHonor==0
            && Scalar("SELECT count(*) FROM revora_honor_purchases WHERE uid=@uid AND item_type=3006319",ok.Entity.UID)==1,
            "ordinary Honor buy of owned FancyHossuHairPack 3006319 still issues the pack");
    }
}
