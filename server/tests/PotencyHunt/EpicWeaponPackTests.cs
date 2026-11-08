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
    static void VerifyEpicWeaponPacks(string runtime,Map town){
        Call(HonorBuys,null,"Initialize");HonorShop.Load();
        string handler=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemHandler.cs")));
        string usage=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemUsage.cs")));
        string server=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Server.cs")));
        string thread=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Thread.cs")));
        string trojan=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/TrojanEpicStore.cs")));
        string audit=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../docs/ITEM_USE_COMPOUND_AUDIT.md")));
        string catalog=File.ReadAllText("database\\itemtype-new.txt");
        string sub=File.ReadAllText("database\\ItemtypeSub.txt");
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
        uint[] packs=new uint[]{3003340u,3006016u,3006226u,3006700u,3307451u};
        Check(typeof(Entity).Assembly.GetType("Revora.Game.Features.EpicWeaponPacks")==null
            && !File.Exists(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/EpicWeaponPacks.cs"))),
            "fixed epic-weapon leftover cases are not compiled onto an owned service");
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
        Check(server.IndexOf("//Revora.Booths.Load();")>=0 && thread.IndexOf("Booths.Load")<0,
            "Booths.Load is commented out of startup and is not an ordinary producer");
        int stall=npcs.IndexOf("case 62148:");
        Check(stall>=0 && npcs.IndexOf("This stall is closed.",stall)>=0
            && npcs.IndexOf("This stall is closed.",stall)<npcs.IndexOf("case 17:",stall),
            "Seller stall 62148 returns closed before leftover 5,000,000-CP epic options");
        for(int n=0;n<packs.Length;n++){
            uint id=packs[n];
            Check(ItemHandler.GetPositionFromID(id)==ItemHandler.Positions.Inventory,
                "leftover epic pack "+id+" still dispatches through inventory UseItem");
            Check(audit.IndexOf("| "+id.ToString()+" | live-split-write | source+items |")>=0,
                "audit keeps leftover epic pack "+id+" live-split-write");
            Check(ConquerItemInformation.BaseInformations.ContainsKey(id),"installed catalog contains leftover epic pack "+id);
            Check(!HonorShop.Shop.Items.ContainsKey(id) && honorIni.IndexOf(id.ToString()+",")<0
                && eshop.IndexOf(id.ToString())<0 && v2.IndexOf(id.ToString())<0 && shopTxt.IndexOf(id.ToString())<0
                && shopDatText.IndexOf(id.ToString())<0 && exchange.IndexOf(id.ToString())<0 && heroes.IndexOf(id.ToString())<0
                && drops.IndexOf(id.ToString())<0,
                "leftover epic pack "+id+" has no HonorShop, emoney, Shop, exchange, StageGoal or DropItemTable producer");
            string body=NestedCase(handler,id);
            Check(body.IndexOf("must use the owned service")<0 && body.IndexOf("ItemUse.Remove")>=0
                && usage.IndexOf("EpicWeaponPacks")<0,
                "leftover epic pack "+id+" stays live-split-write and is not intercepted");
        }
        Check(catalog.IndexOf("3003340@@SolarBlade")>=0 && catalog.IndexOf("614439@@FangCrossSaber")>=0
            && NestedCase(handler,3003340u).IndexOf("Add(614439, 0, 1)")>=0
            && trojan.IndexOf("SolarBlade = 3003340")>=0 && trojan.IndexOf("Reward = 614439")>=0
            && monsters.IndexOf("Name == \"GhostReaver\"")>=0 && monsters.IndexOf("uint ItemID = 3003340")>=0
            && npcs.IndexOf("case 122861:")>=0 && npcs.IndexOf("Contains(3003340, 5)")>=0
            && npcs.IndexOf("Remove(3003340, 5)")>=0 && npcs.IndexOf("Add(614439, 0, 1)")>=0
            && questIni.IndexOf("[chk_item 3003340,1]")>=0,
            "3003340 is SolarBlade with leftover 614439 UseItem, GhostReaver drop, NPC 122861 five-item sink and TrojanEpicStore loan");
        Check(catalog.IndexOf("3006016@@DivinePanacea")>=0 && catalog.IndexOf("620439@@ImperialBacksword")>=0
            && catalog.IndexOf("619439@@FierceImmortal")>=0
            && NestedCase(handler,3006016u).IndexOf("Add(620439, 0, 1)")>=0
            && npcs.IndexOf("Add(3006016, 0, 1)")>=0 && npcs.IndexOf("Contains(3006016, 1)")>=0
            && npcs.IndexOf("Remove(3006016, 1)")>=0 && npcs.IndexOf("Add(619439, 0, 1)")>=0,
            "3006016 leftover UseItem pays 620439 while leftover NPC 48654 sinks it for 619439");
        Check(catalog.IndexOf("3006226@@SuperHossuToken")>=0 && NestedCase(handler,3006226u).IndexOf("Add(619439, 0, 1)")>=0
            && npcs.IndexOf("Add(3006226")<0 && monsters.IndexOf("3006226")<0,
            "3006226 leftover UseItem pays 619439 and has no C# Add or MonsterTable producer");
        Check(catalog.IndexOf("3006700@@EliteMiracleStone")>=0 && catalog.IndexOf("622439@@AlmightyEmperor")>=0
            && NestedCase(handler,3006700u).IndexOf("Add(622439, 0, 1)")>=0
            && catalog.IndexOf("Pay~1,000,000~Sliver")>=0 && npcs.IndexOf("Add(3006700")<0 && monsters.IndexOf("3006700")<0,
            "3006700 leftover UseItem pays 622439; catalog silver-gamble copy is not leftover C#");
        Check(sub.IndexOf("3307451@@OracleoftheSea")>=0 && catalog.IndexOf("3307451@@")<0
            && sub.IndexOf("670439@@OceanDominator")>=0 && sub.IndexOf("671439@@MightyRapierofViolence")>=0
            && NestedCase(handler,3307451u).IndexOf("Add(670439, 0, 1)")>=0
            && NestedCase(handler,3307451u).IndexOf("Add(671439, 0, 1)")>=0
            && npcs.IndexOf("Add(3307451")<0 && monsters.IndexOf("3307451")<0,
            "3307451 leftover UseItem pays two ItemtypeSub pirate weapons and has no C# Add producer");
        Check(PublicScalar("SELECT count(*) FROM public.monsterinfos WHERE ownitem IN (3003340,3006016,3006226,3006700,3307451)")==0,
            "public.monsterinfos.ownitem does not drop leftover epic packs");
        var blade=BallPlayer(town);var solar=Pack(blade,3003340,1);ItemHandler.UseItem(solar,blade);
        Check(!Fenced(blade) && !blade.Inventory.ContainsUID(solar.UID) && CountId(blade,614439)==1
            && !blade.Inventory.Objects.Single(i=>i.ID==614439).Bound && blade.Inventory.Objects.Single(i=>i.ID==614439).Plus==0,
            "leftover 3003340 still converts one SolarBlade into unbound plus-0 614439");
        var panacea=BallPlayer(town);var pill=Pack(panacea,3006016,1);ItemHandler.UseItem(pill,panacea);
        Check(!Fenced(panacea) && CountId(panacea,620439)==1 && CountId(panacea,619439)==0,
            "leftover 3006016 still pays 620439 and not the NPC 48654 619439 sink");
        var token=BallPlayer(town);var hossu=Pack(token,3006226,1);ItemUsage.Process(PrayerPacket(hossu.UID),token);
        Check(!Fenced(token) && CountId(token,619439)==1 && !token.Inventory.ContainsUID(hossu.UID)
            && token.Inventory.Objects.Single(i=>i.ID==619439).Plus==0,
            "ordinary 1009/action-4 leftover 3006226 still pays unbound plus-0 619439");
        var stone=BallPlayer(town);var miracle=Pack(stone,3006700,1);ItemHandler.UseItem(miracle,stone);
        Check(!Fenced(stone) && CountId(stone,622439)==1 && !stone.Inventory.ContainsUID(miracle.UID),
            "leftover 3006700 still pays unbound plus-0 622439");
        var pirate=BallPlayer(town);var oracle=Pack(pirate,3307451,1);ItemHandler.UseItem(oracle,pirate);
        Check(!Fenced(pirate) && CountId(pirate,670439)==1 && CountId(pirate,671439)==1
            && !pirate.Inventory.ContainsUID(oracle.UID)
            && pirate.Inventory.Objects.All(i=>!i.Bound && i.Plus==0),
            "leftover 3307451 still pays both unbound plus-0 pirate weapons");
        var tight=BallPlayer(town);var tightPack=Pack(tight,3307451,1);while(tight.Inventory.Count<39)Pack(tight,1088001,1);
        ItemHandler.UseItem(tightPack,tight);
        Check(!Fenced(tight) && !tight.Inventory.ContainsUID(tightPack.UID) && tight.Inventory.Count==39
            && (CountId(tight,670439)+CountId(tight,671439))==1,
            "leftover 3307451 at Count 39 consumes the source and can insert only one of two rewards");
        var full=BallPlayer(town);var fullPack=Pack(full,3006226,1);while(full.Inventory.Count<40)Pack(full,1088001,1);
        ItemHandler.UseItem(fullPack,full);
        Check(!Fenced(full) && !full.Inventory.ContainsUID(fullPack.UID) && CountId(full,619439)==0,
            "leftover 3006226 at Count 40 still Removes the source when Add refuses");
    }
}
