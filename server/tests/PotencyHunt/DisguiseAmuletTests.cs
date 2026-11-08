using System;
using System.IO;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static void DrinkDisguise(GameState p,ConquerItem item){ItemUsage.Process(PrayerPacket(item.UID),p);}
    static bool KnownDisguise(ushort id){return Array.IndexOf(DataHolder.Disguises,id)>=0;}
    static void VerifyDisguiseAmulet(string runtime,Map town){
        string handler=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemHandler.cs")));
        string npcs=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Npcs.cs")));
        string thread=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Thread.cs")));
        string holder=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Database/DataHolderTable.cs")));
        string audit=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../docs/ITEM_USE_COMPOUND_AUDIT.md")));
        string catalog=File.ReadAllText("database\\itemtype-new.txt");
        string slayer=File.ReadAllText("database\\shops\\SlayerPointshop.ini");
        string race=File.ReadAllText("database\\shops\\RacePointShop.ini");
        string v2=File.ReadAllText("database\\shops\\emoneyshopV2.ini");
        string eshop=File.ReadAllText("database\\shops\\emoneyshop.ini");
        string shopTxt=File.ReadAllText("database\\shops\\Shop.txt");
        string shopDat=File.ReadAllText("database\\shops\\Shop.dat");
        string exchange=File.ReadAllText("database\\shops\\exchange_shop_goods.txt");
        string heroes=File.ReadAllText("database\\Slayer\\StageGoal.txt");
        string drops=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Database/DropItemTable.cs")));
        string monsters=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Database/MonsterTable.cs")));
        Check(handler.IndexOf("GetPositionFromID")>=0 && handler.IndexOf("Inventory = 0")>=0,
            "ordinary 1009/action-4 still dispatches non-equipment IDs through UseItem");
        int switchStart=handler.IndexOf("switch (item.ID)"),switchEnd=handler.IndexOf("public static byte GetNextRefineryItem",switchStart);
        var sourceIds=System.Text.RegularExpressions.Regex.Matches(handler.Substring(switchStart,switchEnd-switchStart),@"(?m)^ {16}case (\d+):").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        var auditIds=System.Text.RegularExpressions.Regex.Matches(audit,@"(?m)^\| (\d+) \|").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        Check(sourceIds.Length==518 && sourceIds.OrderBy(id=>id).SequenceEqual(auditIds.OrderBy(id=>id)),
            "compound audit still enumerates the exact 518 numeric top-level UseItem case IDs");
        Check(audit.IndexOf("| live-split-write | 145 |")>=0 && audit.IndexOf("| owned-fail-closed | 275 |")>=0,
            "census is 145 live-split-write / 275 owned-fail-closed");
        Check(audit.IndexOf("| 723724 | live-split-write | source+timer |")>=0,"audit keeps DisguiseAmulet 723724 live-split-write");
        Check(ConquerItemInformation.BaseInformations.ContainsKey(723724u),"installed catalog contains DisguiseAmulet 723724");
        Check(catalog.IndexOf("723724@@DisguiseAmulet")>=0 && catalog.IndexOf("disguise~you~into~a~monster")>=0,
            "catalog identity for 723724 is DisguiseAmulet");
        Check(eshop.IndexOf("723724")<0 && v2.IndexOf("723724")<0 && shopTxt.IndexOf("723724")<0 && shopDat.IndexOf("723724")<0
            && slayer.IndexOf("723724")<0 && race.IndexOf("723724")<0 && exchange.IndexOf("723724")<0 && heroes.IndexOf("723724")<0
            && drops.IndexOf("723724")<0 && monsters.IndexOf("723724")<0 && npcs.IndexOf("Add(723724")<0,
            "723724 has no emoney, Shop.txt, Shop.dat, SlayerPointshop, exchange, StageGoal, NPC Add, DropItemTable or MonsterTable producer");
        Check(npcs.IndexOf("client.Inventory.Remove(723724, 1)")>=0 && npcs.IndexOf("case 3952:")>=0 && npcs.IndexOf("case 2070:")>=0,
            "Collector Zhao 3952 and 2070 leftover option 3 still sinks 723724");
        Check(holder.IndexOf("111, 224, 117, 152, 113, 833, 116, 245, 223, 112, 222, 114, 221, 115, 220")>=0
            && DataHolder.Disguises.Length==15,
            "leftover disguise faces stay the installed 15-entry DataHolder.Disguises list");
        string body=NestedCase(handler,723724u);
        Check(body.IndexOf("TransformationTime = 110")>=0 && body.IndexOf("TransformationMaxHP = 3000")>=0
            && body.IndexOf("SpellID = 1360")>=0 && body.IndexOf("SpellLevel = 4")>=0
            && body.IndexOf("client.Inventory.Remove(item, Enums.ItemUse.Remove)")>=0,
            "leftover 723724 still consumes one unit and applies 110-second TransformationTime");
        Check(thread.IndexOf("client.Entity.Untransform()")>=0 && thread.IndexOf("TransformationStamp.AddSeconds(client.Entity.TransformationTime)")>=0,
            "leftover Thread still expires a disguise with Untransform");
        var p=BallPlayer(town);var item=Pack(p,723724,1);DrinkDisguise(p,item);
        Check(!Fenced(p) && !p.Inventory.ContainsUID(item.UID) && p.Entity.TransformationTime==110 && p.Entity.TransformationMaxHP==3000u
            && p.Entity.Transformed && KnownDisguise(p.Entity.TransformationID),
            "leftover 723724 consumes one unit and applies a leftover 110-second disguise");
        var again=Pack(p,723724,1);DrinkDisguise(p,again);
        Check(!Fenced(p) && !p.Inventory.ContainsUID(again.UID) && p.Entity.TransformationTime==110 && p.Entity.Transformed && KnownDisguise(p.Entity.TransformationID),
            "leftover 723724 refresh consumes a second unit and reapplies a leftover disguise");
        uint gone=item.UID;p=ReloadBall(p);
        Check(!p.Inventory.ContainsUID(gone) && !p.Entity.Transformed && p.Entity.TransformationTime==0,
            "leftover disguise timer is live-only so production reload drops the form after the item delete");
        Check(npcs.IndexOf("CollectorZhaoCp")>=0 && Config.CollectorZhaoCp==10u && Config.CollectorZhaoRevoraPoints==1u,
            "leftover Collector Zhao sink still pays 10 CP and 1 Revora Point");
    }
}
