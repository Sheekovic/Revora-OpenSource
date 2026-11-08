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
    static readonly Type RedRune=typeof(Entity).Assembly.GetType("Revora.Game.Features.RedRunePacks");
    static DateTime redRuneNow=DateTime.UtcNow;
    static int redRunePick=1;
    static long RedCombines(GameState p){return Scalar("SELECT count(*) FROM revora_red_rune_combines WHERE uid=@uid",p.Entity.UID);}
    static long RedPackUses(GameState p){return Scalar("SELECT count(*) FROM revora_red_rune_pack_uses WHERE uid=@uid",p.Entity.UID);}
    static void OpenRedRune(GameState p,ConquerItem item){ItemUsage.Process(PrayerPacket(item.UID),p);}
    static int RedFragUnits(GameState p){return p.Inventory.Objects.Where(i=>i.ID==3306369u).Sum(i=>i.StackSize<=1?1:i.StackSize);}
    static void VerifyRedRunePacks(string runtime,Map town){
        Query("CREATE TABLE IF NOT EXISTS teamarena (LIKE public.teamarena INCLUDING ALL);CREATE TABLE IF NOT EXISTS itemadding (LIKE public.itemadding INCLUDING ALL)");
        Call(RedRune,null,"Initialize");Call(RedRune,null,"Initialize");
        Call(YbRune,null,"Initialize");
        RedRune.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>redRuneNow));
        RedRune.GetField("Roll",Hidden).SetValue(null,(Func<int,int,int>)((min,max)=>redRunePick));
        string handler=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemHandler.cs")));
        string usage=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemUsage.cs")));
        string thread=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Thread.cs")));
        string npcs=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Npcs.cs")));
        string monsters=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Database/MonsterTable.cs")));
        string audit=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../docs/ITEM_USE_COMPOUND_AUDIT.md")));
        string catalog=File.ReadAllText("database\\itemtype-new.txt");
        string honorIni=File.ReadAllText("database\\shops\\HonorShop.ini");
        string v2=File.ReadAllText("database\\shops\\emoneyshopV2.ini");
        string eshop=File.ReadAllText("database\\shops\\emoneyshop.ini");
        string shopTxt=File.ReadAllText("database\\shops\\Shop.txt");
        string exchange=File.ReadAllText("database\\shops\\exchange_shop_goods.txt");
        string heroes=File.ReadAllText("database\\Slayer\\StageGoal.txt");
        string drops=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Database/DropItemTable.cs")));
        string shopDatText=Encoding.ASCII.GetString(File.ReadAllBytes("database\\shops\\Shop.dat"));
        uint[] redRunes=new uint[]{4010001u,4010101u,4010201u,4010301u,4010401u,4010501u,4010601u,4010701u,4010801u,4010901u};
        Check(handler.IndexOf("Red rune item must use the owned service.")>=0 && handler.IndexOf("RedRunePacks.TryUse")>handler.IndexOf("HossuHairPacks.TryUse")
            && usage.IndexOf("RedRunePacks.TryHandle")>usage.IndexOf("HossuHairPacks.TryHandle")
            && thread.IndexOf("RedRunePacks.Initialize")>thread.IndexOf("HossuHairPacks.Initialize"),
            "RedRunePacks intercepts sit after HossuHairPacks and leftover 3306369/3306509 fail closed");
        Check(handler.IndexOf("GetPositionFromID")>=0 && handler.IndexOf("Inventory = 0")>=0,
            "ordinary 1009/action-4 still dispatches non-equipment IDs through UseItem");
        int switchStart=handler.IndexOf("switch (item.ID)"),switchEnd=handler.IndexOf("public static byte GetNextRefineryItem",switchStart);
        var sourceIds=System.Text.RegularExpressions.Regex.Matches(handler.Substring(switchStart,switchEnd-switchStart),@"(?m)^ {16}case (\d+):").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        var auditIds=System.Text.RegularExpressions.Regex.Matches(audit,@"(?m)^\| (\d+) \|").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        Check(sourceIds.Length==518 && sourceIds.OrderBy(id=>id).SequenceEqual(auditIds.OrderBy(id=>id)),
            "compound audit still enumerates the exact 518 numeric top-level UseItem case IDs");
        Check(audit.IndexOf("| live-split-write | 145 |")>=0 && audit.IndexOf("| owned-fail-closed | 275 |")>=0
            && audit.IndexOf("| single-consume | 55 |")>=0 && audit.IndexOf("| other | 43 |")>=0,
            "census is 145 live-split-write / 275 owned-fail-closed after red rune ownership");
        Check(audit.IndexOf("| 3306369 | owned-fail-closed | source+items |")>=0 && audit.IndexOf("| 3306509 | owned-fail-closed | source+items |")>=0,
            "audit owns red fragment 3306369 and pack 3306509");
        Check(audit.IndexOf("| 3306370 | owned-fail-closed | source+items |")>=0 && audit.IndexOf("| 3306371 | owned-fail-closed | source+items |")>=0
            && audit.IndexOf("| 3306510 | owned-fail-closed | source+items |")>=0 && audit.IndexOf("| 3306511 | owned-fail-closed | source+items |")>=0,
            "yellow and blue leftover rune IDs now fail closed through their owned service");
        Check(catalog.IndexOf("3306369@@RedRuneFragment")>=0 && catalog.IndexOf("3306509@@RandomRedRune(B)Pack")>=0
            && ConquerItemInformation.BaseInformations.ContainsKey(3306369u) && ConquerItemInformation.BaseInformations.ContainsKey(3306509u),
            "installed catalog identities are RedRuneFragment and RandomRedRune(B)Pack");
        Check(npcs.IndexOf("case 900181:")>=0 && npcs.IndexOf("Add(3306369, 0, 5)")>=0 && npcs.IndexOf("Add(3306369, 0, 15)")>=0
            && npcs.IndexOf("Add(3306369, 0, 20)")>=0,
            "Online Points Seller 900181 issues leftover unbound 3306369");
        Check(PublicScalar("SELECT count(*) FROM public.npcs WHERE id=900181")+PublicScalar("SELECT count(*) FROM public.sobnpcs WHERE id=900181")>=1,
            "installed database has NPC/SOBNPC 900181");
        Check(monsters.IndexOf("Kernel.Random.Next(1, 17)")>=0 && monsters.IndexOf("ItemID = 3306370;")>=0
            && monsters.IndexOf("case 17:")>=0,
            "SuperBoss leftover case 17 names yellow 3306370 after Next(1,17)");
        Check(npcs.IndexOf("Add(3306368, 0, 5)")>=0 && npcs.IndexOf("Add(3306371")<0 && handler.IndexOf("case 3306368:")<0,
            "blue seller options issue 3306368 and not leftover 3306371; 3306368 has no UseItem case");
        uint[] leftover=new uint[]{3306370u,3306371u,3306510u,3306511u};
        for(int n=0;n<leftover.Length;n++){
            uint id=leftover[n];
            Check(eshop.IndexOf(id.ToString())<0 && v2.IndexOf(id.ToString())<0 && shopTxt.IndexOf(id.ToString())<0
                && shopDatText.IndexOf(id.ToString())<0 && exchange.IndexOf(id.ToString())<0 && heroes.IndexOf(id.ToString())<0
                && honorIni.IndexOf(id.ToString()+",")<0 && drops.IndexOf(id.ToString())<0 && npcs.IndexOf("Add("+id.ToString())<0,
                "classified leftover "+id+" has no HonorShop, emoney, Shop, exchange, StageGoal, DropItemTable or NPC Add producer");
        }
        string heard="";RedRune.GetField("Spoken",Hidden).SetValue(null,(Action<string>)(m=>{heard=heard+m+"|";}));
        heard="";var need=BallPlayer(town);var few=Pack(need,3306369,19);OpenRedRune(need,few);
        Check(!Fenced(need) && RedCombines(need)==0 && RedFragUnits(need)==19 && heard.IndexOf("You do not have 20 pieces")>=0,
            "19 fragments refuse without consume");
        heard="";var ok=BallPlayer(town);var twenty=Pack(ok,3306369,20);OpenRedRune(ok,twenty);
        Check(!Fenced(ok) && RedCombines(ok)==1 && !ok.Inventory.ContainsUID(twenty.UID) && CountId(ok,3306509)==1
            && ok.Inventory.Objects.Single(i=>i.ID==3306509).Bound && ok.Inventory.Objects.Single(i=>i.ID==3306509).Plus==0,
            "20-stack combine pays one bound plus-0 3306509");
        var piled=BallPlayer(town);var pile=Pack(piled,3306369,21);OpenRedRune(piled,pile);
        Check(!Fenced(piled) && RedCombines(piled)==1 && pile.StackSize==1 && CountId(piled,3306509)==1,
            "21-stack combine keeps leftover remainder 1");
        var boundSrc=BallPlayer(town);var boundFrag=NestedPack(boundSrc,3306369,20,true);OpenRedRune(boundSrc,boundFrag);
        Check(!Fenced(boundSrc) && boundSrc.Inventory.Objects.Single(i=>i.ID==3306509).Bound,
            "bound fragments still issue leftover bound pack");
        var tight=BallPlayer(town);var tightFrag=Pack(tight,3306369,20);while(tight.Inventory.Count<40)Pack(tight,1088001,1);OpenRedRune(tight,tightFrag);
        Check(!Fenced(tight) && RedCombines(tight)==1 && CountId(tight,3306509)==1,
            "released fragment stack slot opens the pack at Count 40");
        var full=BallPlayer(town);var fullFrag=Pack(full,3306369,21);while(full.Inventory.Count<40)Pack(full,1088001,1);heard="";OpenRedRune(full,fullFrag);
        Check(!Fenced(full) && RedCombines(full)==0 && fullFrag.StackSize==21 && heard.IndexOf("You do not have 20 pieces")>=0,
            "remainder combine at Count 40 refuses without a free slot");
        redRunePick=1;
        for(int n=0;n<redRunes.Length;n++){
            redRunePick=n+1;var opener=BallPlayer(town);var pack=Pack(opener,3306509,1);OpenRedRune(opener,pack);
            Check(!Fenced(opener) && RedPackUses(opener)==1 && !opener.Inventory.ContainsUID(pack.UID) && CountId(opener,redRunes[n])==1
                && opener.Inventory.Objects.Single(i=>i.ID==redRunes[n]).Bound && opener.Inventory.Objects.Single(i=>i.ID==redRunes[n]).Plus==0,
                "leftover pick "+(n+1)+" opens bound red rune "+redRunes[n]);
        }
        redRunePick=11;var miss=BallPlayer(town);var missPack=Pack(miss,3306509,1);OpenRedRune(miss,missPack);
        Check(!Fenced(miss) && RedPackUses(miss)==0 && miss.Inventory.ContainsUID(missPack.UID),
            "leftover Next(1,11) pick 11 stays unreachable and does not consume");
        redRunePick=1;
        var stacked=BallPlayer(town);var stackPack=Pack(stacked,3306509,2);OpenRedRune(stacked,stackPack);
        Check(!Fenced(stacked) && RedPackUses(stacked)==1 && stackPack.StackSize==1 && CountId(stacked,4010001)==1,
            "stacked red rune pack keeps leftover remainder");
        var direct=BallPlayer(town);var directPack=Pack(direct,3306509,1);ItemHandler.UseItem(directPack,direct);
        Check(!Fenced(direct) && RedPackUses(direct)==1 && CountId(direct,4010001)==1,"direct UseItem opens leftover red rune pack");
        var spent=BallPlayer(town);var spentPack=Pack(spent,3306509,1);OpenRedRune(spent,spentPack);ItemHandler.UseItem(spentPack,spent);
        Check(!Fenced(spent) && RedPackUses(spent)==1 && CountId(spent,4010001)==1,"consumed red rune pack UID cannot replay");
        var race=BallPlayer(town);var raceItem=Pack(race,3306509,1);
        Parallel.For(0,16,n=>OpenRedRune(race,raceItem));
        Check(!Fenced(race) && RedPackUses(race)==1 && CountId(race,4010001)==1,"parallel packets cannot duplicate red rune pack use");
        Query(@"CREATE OR REPLACE FUNCTION suppress_red_rune() RETURNS trigger AS $$ BEGIN RAISE EXCEPTION 'suppressed'; END; $$ LANGUAGE plpgsql;
CREATE TRIGGER suppress_red_rune BEFORE INSERT ON revora_red_rune_pack_uses FOR EACH ROW EXECUTE PROCEDURE suppress_red_rune();");
        var fault=BallPlayer(town);var failItem=Pack(fault,3306509,1);
        try{OpenRedRune(fault,failItem);
            Check(Fenced(fault) && RedPackUses(fault)==0 && fault.Inventory.ContainsUID(failItem.UID),
                "suppressed red rune persist fences without consuming the pack");}
        finally{Query("DROP TRIGGER IF EXISTS suppress_red_rune ON revora_red_rune_pack_uses;DROP FUNCTION IF EXISTS suppress_red_rune()");}
        fault=ReloadBall(fault);OpenRedRune(fault,fault.Inventory.Objects.Single(i=>i.ID==3306509));
        Check(!Fenced(fault) && RedPackUses(fault)==1 && CountId(fault,4010001)==1,"production reload retries red rune pack after durable write failure");
        var yellow=BallPlayer(town);var yFrag=Pack(yellow,3306370,20);ItemHandler.UseItem(yFrag,yellow);
        Check(!Fenced(yellow) && CountId(yellow,3306510)==1 && yellow.Inventory.Objects.Single(i=>i.ID==3306510).Bound,
            "leftover yellow 3306370 still combines into bound 3306510");
        var blue=BallPlayer(town);var bFrag=Pack(blue,3306371,20);ItemHandler.UseItem(bFrag,blue);
        Check(!Fenced(blue) && CountId(blue,3306511)==1,"leftover blue 3306371 still combines into bound 3306511");
    }
}
