using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type PlusOneStone=typeof(Entity).Assembly.GetType("Revora.Game.Features.PlusOneStonePacks");
    static DateTime plusStoneNow=DateTime.UtcNow;
    static long PlusStoneUses(GameState p){return Scalar("SELECT count(*) FROM revora_plus_one_stone_pack_uses WHERE uid=@uid",p.Entity.UID);}
    static void OpenPlusStone(GameState p,ConquerItem item){ItemUsage.Process(PrayerPacket(item.UID),p);}
    static int PlusStones(GameState p){return p.Inventory.Objects.Count(i=>i.ID==730001u && i.Plus==1);}
    static void VerifyPlusOneStonePacks(string runtime,Map town){
        Query("CREATE TABLE IF NOT EXISTS teamarena (LIKE public.teamarena INCLUDING ALL);CREATE TABLE IF NOT EXISTS itemadding (LIKE public.itemadding INCLUDING ALL)");
        Call(PlusOneStone,null,"Initialize");Call(PlusOneStone,null,"Initialize");
        PlusOneStone.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>plusStoneNow));
        string handler=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemHandler.cs")));
        string usage=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemUsage.cs")));
        string thread=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Thread.cs")));
        string npcs=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Npcs.cs")));
        string plaza=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/TwinCityPlaza.cs")));
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
        string shopDatText=Encoding.ASCII.GetString(File.ReadAllBytes("database\\shops\\Shop.dat"));
        Check(handler.IndexOf("+1 stone pack must use the owned service.")>=0 && handler.IndexOf("PlusOneStonePacks.TryUse")>handler.IndexOf("RedRunePacks.TryUse")
            && usage.IndexOf("PlusOneStonePacks.TryHandle")>usage.IndexOf("RedRunePacks.TryHandle")
            && thread.IndexOf("PlusOneStonePacks.Initialize")>thread.IndexOf("RedRunePacks.Initialize"),
            "PlusOneStonePacks intercepts sit after RedRunePacks and leftover 723712 fails closed");
        Check(handler.IndexOf("GetPositionFromID")>=0 && handler.IndexOf("Inventory = 0")>=0,
            "ordinary 1009/action-4 still dispatches non-equipment IDs through UseItem");
        int switchStart=handler.IndexOf("switch (item.ID)"),switchEnd=handler.IndexOf("public static byte GetNextRefineryItem",switchStart);
        var sourceIds=System.Text.RegularExpressions.Regex.Matches(handler.Substring(switchStart,switchEnd-switchStart),@"(?m)^ {16}case (\d+):").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        var auditIds=System.Text.RegularExpressions.Regex.Matches(audit,@"(?m)^\| (\d+) \|").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        Check(sourceIds.Length==518 && sourceIds.OrderBy(id=>id).SequenceEqual(auditIds.OrderBy(id=>id)),
            "compound audit still enumerates the exact 518 numeric top-level UseItem case IDs");
        Check(audit.IndexOf("| live-split-write | 145 |")>=0 && audit.IndexOf("| owned-fail-closed | 275 |")>=0
            && audit.IndexOf("| single-consume | 55 |")>=0 && audit.IndexOf("| other | 43 |")>=0,
            "census is 145 live-split-write / 275 owned-fail-closed after +1 stone pack ownership");
        int intercepts=System.Text.RegularExpressions.Regex.Matches(handler.Substring(0,switchStart),@"if \(Revora\.Game\.Features\.(\w+)\.TryUse\(item, client\)\) return;").Count;
        Check(intercepts==67,"UseItem has 67 pre-switch TryUse intercepts");
        Check(audit.IndexOf("| PlusOneStonePacks | 723712 |")>=0 && audit.IndexOf("| PlusOneStonePacks | 723712 | fail-closed |")>=0,
            "handled-ID catalog and leftover matrix own PlusOneStonePacks 723712");
        Check(audit.IndexOf("| 723712 | owned-fail-closed | source+items |")>=0,"audit owns +1StonePack 723712");
        Check(audit.IndexOf("| 721330 | live-split-write | source+items |")>=0 && audit.IndexOf("| 723711 | owned-fail-closed | source+items |")>=0,
            "721330 stays leftover live-split-write and 723711 is owned fail-closed");
        Check(catalog.IndexOf("723712@@")>=0 && catalog.IndexOf("730001@@Stone")>=0 && ConquerItemInformation.BaseInformations.ContainsKey(723712u)
            && ConquerItemInformation.BaseInformations.ContainsKey(730001u),
            "installed catalog contains 723712 and Stone 730001");
        string packing=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/MillionaireStonePacking.cs")));
        Check(npcs.IndexOf("case 11275:")>=0 && npcs.IndexOf("Millionaire stone packing must use the owned service.")>=0
            && packing.IndexOf("stone=Stone1;pack=Pack1;need=5")>=0 && npcs.IndexOf("MillionaireStonePacking.HandleNpc")>=0,
            "MillionaireLee option 3 is owned packing of five 730001 into 723712");
        Check(PublicScalar("SELECT count(*) FROM public.npcs WHERE id=11275 AND mapid=1002")==1
            && PublicScalar("SELECT count(*) FROM public.sobnpcs WHERE id=11275")+PublicScalar("SELECT count(*) FROM public.furniture WHERE id=11275")==0,
            "installed public.npcs has MillionaireLee 11275 on Twin City");
        int closed=plaza.IndexOf("ClosedDeskIds");
        Check(plaza.IndexOf("PackingClerkId = 11275")>=0 && closed>=0 && plaza.Substring(closed).IndexOf("11275")<0,
            "MillionaireLee 11275 is the Twin City packing clerk and is not a closed desk");
        uint[] classified=new uint[]{721330u,723711u,723712u};
        for(int n=0;n<classified.Length;n++){
            uint id=classified[n];
            Check(honorIni.IndexOf(id.ToString()+",")<0 && eshop.IndexOf(id.ToString())<0 && v2.IndexOf(id.ToString())<0
                && shopTxt.IndexOf(id.ToString())<0 && shopDatText.IndexOf(id.ToString())<0 && exchange.IndexOf(id.ToString())<0
                && heroes.IndexOf(id.ToString())<0 && drops.IndexOf(id.ToString())<0 && monsters.IndexOf(id.ToString())<0,
                id+" has no HonorShop, emoney, Shop, exchange, StageGoal, DropItemTable or MonsterTable producer");
        }
        Check(npcs.IndexOf("Add(721330")<0 && npcs.IndexOf("Add(723711")<0 && catalog.IndexOf("721330@@")<0 && sub.IndexOf("721330@@")<0
            && handler.IndexOf("Add(1002050, 0, 3)")>=0 && catalog.IndexOf("723711@@MeteorTearPack")>=0
            && handler.IndexOf("Add(1088002, 0, 5)")<0 && handler.IndexOf("case 723711:")>=0
            && NestedCase(handler,723711u).IndexOf("must use the owned service")>=0,
            "721330 stays a leftover consumer with no catalog row; 723711 has no C# Add producer and fails closed");
        string heard="";PlusOneStone.GetField("Spoken",Hidden).SetValue(null,(Action<string>)(m=>{heard=heard+m+"|";}));
        heard="";var ok=BallPlayer(town);var pack=Pack(ok,723712,1);OpenPlusStone(ok,pack);
        Check(!Fenced(ok) && PlusStoneUses(ok)==1 && !ok.Inventory.ContainsUID(pack.UID) && PlusStones(ok)==5
            && ok.Inventory.Objects.Where(i=>i.ID==730001).All(i=>!i.Bound && i.Plus==1 && i.StackSize==1) && heard=="",
            "unbound 723712 pays five unbound Plus-1 stones");
        ok=ReloadBall(ok);
        Check(!Fenced(ok) && PlusStoneUses(ok)==1 && PlusStones(ok)==5,"unbound +1 stone unpack survives production reload");
        var boundP=BallPlayer(town);var boundPack=NestedPack(boundP,723712,1,true);OpenPlusStone(boundP,boundPack);
        Check(!Fenced(boundP) && PlusStones(boundP)==5 && boundP.Inventory.Objects.Where(i=>i.ID==730001).All(i=>i.Bound && i.Plus==1),
            "bound 723712 cannot launder unbound stones");
        var piled=BallPlayer(town);var pile=Pack(piled,723712,2);OpenPlusStone(piled,pile);
        Check(!Fenced(piled) && PlusStoneUses(piled)==1 && pile.StackSize==1 && PlusStones(piled)==5,
            "stacked 723712 keeps leftover remainder");
        var tight=BallPlayer(town);var tightPack=Pack(tight,723712,1);while(tight.Inventory.Count<36)Pack(tight,1088001,1);OpenPlusStone(tight,tightPack);
        Check(!Fenced(tight) && PlusStoneUses(tight)==1 && PlusStones(tight)==5,
            "depleted pack at Count 36 reuses its slot for five Plus-1 stones");
        heard="";var full=BallPlayer(town);var fullPack=Pack(full,723712,1);while(full.Inventory.Count<37)Pack(full,1088001,1);OpenPlusStone(full,fullPack);
        Check(!Fenced(full) && PlusStoneUses(full)==0 && full.Inventory.ContainsUID(fullPack.UID) && heard.IndexOf("Your inventory is Full.")>=0,
            "depleted pack at Count 37 refuses without consume");
        var stackedOk=BallPlayer(town);var stackedOkPack=Pack(stackedOk,723712,2);while(stackedOk.Inventory.Count<35)Pack(stackedOk,1088001,1);OpenPlusStone(stackedOk,stackedOkPack);
        Check(!Fenced(stackedOk) && PlusStoneUses(stackedOk)==1 && stackedOkPack.StackSize==1 && PlusStones(stackedOk)==5,
            "stacked pack at Count 35 pays five Plus-1 stones and keeps the remainder");
        heard="";var stackedFull=BallPlayer(town);var stackedPack=Pack(stackedFull,723712,2);while(stackedFull.Inventory.Count<36)Pack(stackedFull,1088001,1);OpenPlusStone(stackedFull,stackedPack);
        Check(!Fenced(stackedFull) && PlusStoneUses(stackedFull)==0 && stackedPack.StackSize==2 && heard.IndexOf("Your inventory is Full.")>=0,
            "stacked pack at Count 36 refuses because the remainder keeps its slot");
        var direct=BallPlayer(town);var directPack=Pack(direct,723712,1);ItemHandler.UseItem(directPack,direct);
        Check(!Fenced(direct) && PlusStoneUses(direct)==1 && PlusStones(direct)==5,"direct UseItem opens leftover +1 stone pack");
        var spent=BallPlayer(town);var spentPack=Pack(spent,723712,1);OpenPlusStone(spent,spentPack);ItemHandler.UseItem(spentPack,spent);
        Check(!Fenced(spent) && PlusStoneUses(spent)==1 && PlusStones(spent)==5,"consumed +1 stone pack UID cannot replay");
        var race=BallPlayer(town);var raceItem=Pack(race,723712,1);
        Parallel.For(0,16,n=>OpenPlusStone(race,raceItem));
        Check(!Fenced(race) && PlusStoneUses(race)==1 && PlusStones(race)==5,"parallel packets cannot duplicate +1 stone pack use");
        foreach(string fault in new[]{"source","output","receipt"}){
            var faultPlayer=BallPlayer(town);var failItem=Pack(faultPlayer,723712,1);
            string table=fault=="receipt"?"revora_plus_one_stone_pack_uses":"items";
            string verb=fault=="source"?"DELETE":"INSERT";
            Query("CREATE FUNCTION suppress_plus_stone() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_plus_stone BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_plus_stone()");
            try{OpenPlusStone(faultPlayer,failItem);
                Check(Fenced(faultPlayer) && PlusStoneUses(faultPlayer)==0 && faultPlayer.Inventory.ContainsUID(failItem.UID) && PlusStones(faultPlayer)==0,
                    "suppressed "+fault+" preserves +1 stone pack and every reward");}
            finally{Query("DROP TRIGGER suppress_plus_stone ON "+table+";DROP FUNCTION suppress_plus_stone()");}
            faultPlayer=ReloadBall(faultPlayer);OpenPlusStone(faultPlayer,faultPlayer.Inventory.Objects.Single(i=>i.ID==723712));
            Check(!Fenced(faultPlayer) && PlusStoneUses(faultPlayer)==1 && PlusStones(faultPlayer)==5,
                "production reload retries +1 stone pack after "+fault+" rollback");
        }
        Check(handler.IndexOf("case 721330:")>=0 && NestedCase(handler,721330u).IndexOf("Add(1002050, 0, 3)")>=0
            && NestedCase(handler,721330u).IndexOf("must use the owned service")<0,
            "leftover 721330 still pays three 1002050 and is not fail-closed");
    }
}
