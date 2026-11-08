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
    static void OpenSurprise(GameState p,ConquerItem item){ItemUsage.Process(PrayerPacket(item.UID),p);}
    static void VerifySurpriseBox(string runtime,Map town){
        string handler=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemHandler.cs")));
        string audit=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../docs/ITEM_USE_COMPOUND_AUDIT.md")));
        int switchStart=handler.IndexOf("switch (item.ID)"),switchEnd=handler.IndexOf("public static byte GetNextRefineryItem",switchStart);
        var sourceIds=System.Text.RegularExpressions.Regex.Matches(handler.Substring(switchStart,switchEnd-switchStart),@"(?m)^ {16}case (\d+):").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        var auditIds=System.Text.RegularExpressions.Regex.Matches(audit,@"(?m)^\| (\d+) \|").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        Check(sourceIds.Length==518 && sourceIds.OrderBy(id=>id).SequenceEqual(auditIds.OrderBy(id=>id)),
            "compound audit still enumerates the exact 518 numeric top-level UseItem case IDs");
        Check(audit.IndexOf("| 722178 | live-split-write | source+cp |")>=0,
            "722178 stays live-split-write source+cp after the commented PowerEXPBall grant");
        Check(handler.IndexOf("GetPositionFromID")>=0 && handler.IndexOf("Inventory = 0")>=0,
            "ordinary 1009/action-4 still dispatches non-equipment IDs through UseItem");
        Check(ConquerItemInformation.BaseInformations.ContainsKey(722178u),"installed catalog contains SurpriseBox 722178");
        Check(File.ReadAllText("database\\itemtype-new.txt").IndexOf("722178@@SurpriseBox")>=0,
            "itemtype identity is SurpriseBox 722178");
        int box=handler.IndexOf("case 722178:");
        Check(box>=0 && handler.IndexOf("ConquerPoints += 2000",box)>=0 && handler.IndexOf("Inventory.Count <= 38",box)>=0
            && handler.IndexOf("//client.Inventory.Add(722057, 0, 1);//PowerEXPBall",box)>=0
            && handler.IndexOf("You Must have 1 space in you Inventory To Open You Box",box)>=0
            && handler.IndexOf("Color.White, 255",box)>=0,
            "leftover 722178 still pays +2000 CP behind Count<=38 with the commented PowerEXPBall and exact refuse text");
        Check(handler.IndexOf("Surprise box must use")<0 && handler.IndexOf("case 722178:")<handler.IndexOf("ConquerPoints += 2000",box),
            "722178 is not fail-closed onto an owned service");
        string exchange=File.ReadAllText("database\\shops\\exchange_shop_goods.txt");
        string eshop=File.ReadAllText("database\\shops\\emoneyshopV2.ini");
        string shop=File.ReadAllText("database\\shops\\Shop.txt");
        string heroes=File.ReadAllText("database\\Slayer\\StageGoal.txt");
        string drops=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Database/DropItemTable.cs")));
        string npcs=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Npcs.cs")));
        byte[] shopDat=File.ReadAllBytes("database\\shops\\Shop.dat");
        Check(exchange.IndexOf("722178")<0 && eshop.IndexOf("722178")<0 && shop.IndexOf("722178")<0 && heroes.IndexOf("722178")<0
            && drops.IndexOf("722178")<0 && npcs.IndexOf("722178")<0 && System.Text.Encoding.ASCII.GetString(shopDat).IndexOf("722178")<0
            && File.ReadAllText("database\\shops\\emoneyshop.ini").IndexOf("722178")<0,
            "722178 has no shop, exchange, StageGoal, NPC or DropItemTable producer");
        var outside=WelcomePlayer(town,1,0,15);var boxItem=Pack(outside,722178,1);
        Check(!(bool)Call(CpBags,null,"TryUse",boxItem,outside) && !(bool)Call(ChiItems,null,"TryUse",boxItem,outside)
            && outside.Inventory.ContainsUID(boxItem.UID),"ConquerPointBags and ChiPointItems do not own 722178");
        uint before=outside.Entity.ConquerPoints;OpenSurprise(outside,boxItem);
        Check(!Fenced(outside) && !outside.Inventory.ContainsUID(boxItem.UID) && outside.Entity.ConquerPoints==before+2000u,
            "leftover 722178 still split-writes +2000 CP from ordinary action-4");
        var full=WelcomePlayer(town,1,0,15);var fullBox=Pack(full,722178,1);while(full.Inventory.Count<39)Pack(full,1000000,1);
        uint fullCp=full.Entity.ConquerPoints;OpenSurprise(full,fullBox);
        Check(!Fenced(full) && full.Inventory.ContainsUID(fullBox.UID) && full.Entity.ConquerPoints==fullCp,
            "leftover 722178 still refuses at Count 39 even though no replacement item is granted");
        var direct=WelcomePlayer(town,1,0,15);var directBox=Pack(direct,722178,1);uint directCp=direct.Entity.ConquerPoints;
        ItemHandler.UseItem(directBox,direct);
        Check(!Fenced(direct) && !direct.Inventory.ContainsUID(directBox.UID) && direct.Entity.ConquerPoints==directCp+2000u,
            "direct UseItem still consumes leftover 722178 for +2000 CP");
    }
}
