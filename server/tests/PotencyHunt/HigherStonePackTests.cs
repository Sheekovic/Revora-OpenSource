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
    static readonly Type HigherStone=typeof(Entity).Assembly.GetType("Revora.Game.Features.HigherStonePacks");
    static DateTime higherStoneNow=DateTime.UtcNow;
    static long HigherUses(GameState p){return Scalar("SELECT count(*) FROM revora_higher_stone_pack_uses WHERE uid=@uid",p.Entity.UID);}
    static void OpenHigher(GameState p,ConquerItem item){ItemUsage.Process(PrayerPacket(item.UID),p);}
    static int HigherStones(GameState p,uint stone,byte plus){return p.Inventory.Objects.Count(i=>i.ID==stone && i.Plus==plus);}
    static void FillTo(GameState p,int count){while(p.Inventory.Count<count)Pack(p,1088001,1);}
    static void VerifyHigherStonePacks(string runtime,Map town){
        Query("CREATE TABLE IF NOT EXISTS teamarena (LIKE public.teamarena INCLUDING ALL);CREATE TABLE IF NOT EXISTS itemadding (LIKE public.itemadding INCLUDING ALL)");
        Call(HigherStone,null,"Initialize");Call(HigherStone,null,"Initialize");
        HigherStone.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>higherStoneNow));
        HigherStone.GetField("BeforeInstall",Hidden).SetValue(null,null);
        string handler=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemHandler.cs")));
        string usage=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemUsage.cs")));
        string thread=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Thread.cs")));
        string npcs=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Npcs.cs")));
        string plaza=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/TwinCityPlaza.cs")));
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
        string shopDatText=Encoding.ASCII.GetString(File.ReadAllBytes("database\\shops\\Shop.dat"));
        Check(handler.IndexOf("Higher stone pack must use the owned service.")>=0 && handler.IndexOf("HigherStonePacks.TryUse")>handler.IndexOf("PlusOneStonePacks.TryUse")
            && usage.IndexOf("HigherStonePacks.TryHandle")>usage.IndexOf("PlusOneStonePacks.TryHandle")
            && thread.IndexOf("HigherStonePacks.Initialize")>thread.IndexOf("PlusOneStonePacks.Initialize"),
            "HigherStonePacks intercepts sit after PlusOneStonePacks and leftover 729022/729023 fail closed");
        Check(handler.IndexOf("GetPositionFromID")>=0 && handler.IndexOf("Inventory = 0")>=0,
            "ordinary 1009/action-4 still dispatches non-equipment IDs through UseItem");
        int switchStart=handler.IndexOf("switch (item.ID)"),switchEnd=handler.IndexOf("public static byte GetNextRefineryItem",switchStart);
        var sourceIds=System.Text.RegularExpressions.Regex.Matches(handler.Substring(switchStart,switchEnd-switchStart),@"(?m)^ {16}case (\d+):").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        var auditIds=System.Text.RegularExpressions.Regex.Matches(audit,@"(?m)^\| (\d+) \|").Cast<System.Text.RegularExpressions.Match>().Select(m=>uint.Parse(m.Groups[1].Value)).ToArray();
        Check(sourceIds.Length==518 && sourceIds.OrderBy(id=>id).SequenceEqual(auditIds.OrderBy(id=>id)),
            "compound audit still enumerates the exact 518 numeric top-level UseItem case IDs");
        Check(audit.IndexOf("| live-split-write | 145 |")>=0 && audit.IndexOf("| owned-fail-closed | 275 |")>=0
            && audit.IndexOf("| single-consume | 55 |")>=0 && audit.IndexOf("| other | 43 |")>=0,
            "census is 145 live-split-write / 275 owned-fail-closed after higher stone pack ownership");
        int intercepts=System.Text.RegularExpressions.Regex.Matches(handler.Substring(0,switchStart),@"if \(Revora\.Game\.Features\.(\w+)\.TryUse\(item, client\)\) return;").Count;
        Check(intercepts==67,"UseItem has 67 pre-switch TryUse intercepts");
        Check(audit.IndexOf("| HigherStonePacks | 729022,729023 |")>=0
            && audit.IndexOf("| HigherStonePacks | 729022 | fail-closed |")>=0
            && audit.IndexOf("| HigherStonePacks | 729023 | fail-closed |")>=0,
            "handled-ID catalog and leftover matrix own HigherStonePacks");
        Check(audit.IndexOf("| 729022 | owned-fail-closed | source+items |")>=0 && audit.IndexOf("| 729023 | owned-fail-closed | source+items |")>=0,
            "audit owns +2StonePack 729022 and +3StonePack 729023");
        Check(catalog.IndexOf("729022@@")>=0 && catalog.IndexOf("729023@@")>=0 && catalog.IndexOf("730002@@Stone")>=0 && catalog.IndexOf("730003@@Stone")>=0
            && ConquerItemInformation.BaseInformations.ContainsKey(729022u) && ConquerItemInformation.BaseInformations.ContainsKey(729023u)
            && ConquerItemInformation.BaseInformations.ContainsKey(730002u) && ConquerItemInformation.BaseInformations.ContainsKey(730003u),
            "installed catalog contains 729022, 729023 and Stone 730002/730003");
        string packingSvc=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/MillionaireStonePacking.cs")));
        Check(npcs.IndexOf("Millionaire stone packing must use the owned service.")>=0
            && npcs.IndexOf("MillionaireStonePacking.HandleNpc")>=0
            && packingSvc.IndexOf("stone=Stone2;pack=Pack2;need=10")>=0 && packingSvc.IndexOf("stone=Stone3;pack=Pack3;need=10")>=0,
            "MillionaireLee options 4 and 5 are owned packing of ten 730002/730003");
        Check(PublicScalar("SELECT count(*) FROM public.npcs WHERE id=11275 AND mapid=1002")==1
            && PublicScalar("SELECT count(*) FROM public.sobnpcs WHERE id=11275")+PublicScalar("SELECT count(*) FROM public.furniture WHERE id=11275")==0,
            "installed public.npcs has MillionaireLee 11275 on Twin City");
        int closed=plaza.IndexOf("ClosedDeskIds");
        Check(plaza.IndexOf("PackingClerkId = 11275")>=0 && closed>=0 && plaza.Substring(closed).IndexOf("11275")<0,
            "MillionaireLee 11275 is the Twin City packing clerk and is not a closed desk");
        string votePath="database\\Seguridad\\VotarObjetos\\2018-1\\17.txt";
        Check(File.Exists(votePath) && File.ReadAllText(votePath).IndexOf("729023")>=0,
            "2018 vote log names 729023 and is not an ordinary producer");
        uint[] packs=new uint[]{729022u,729023u};
        uint[] stones=new uint[]{730002u,730003u};
        byte[] pluses=new byte[]{2,3};
        for(int n=0;n<packs.Length;n++){
            uint packId=packs[n],stone=stones[n];byte plus=pluses[n];
            Check(honorIni.IndexOf(packId.ToString()+",")<0 && eshop.IndexOf(packId.ToString())<0 && v2.IndexOf(packId.ToString())<0
                && shopTxt.IndexOf(packId.ToString())<0 && shopDatText.IndexOf(packId.ToString())<0 && exchange.IndexOf(packId.ToString())<0
                && heroes.IndexOf(packId.ToString())<0 && drops.IndexOf(packId.ToString())<0 && monsters.IndexOf(packId.ToString())<0,
                packId+" has no HonorShop, emoney, Shop, exchange, StageGoal, DropItemTable or MonsterTable producer");
            string heard="";HigherStone.GetField("Spoken",Hidden).SetValue(null,(Action<string>)(m=>{heard=heard+m+"|";}));
            var ok=BallPlayer(town);var pack=Pack(ok,packId,1);OpenHigher(ok,pack);
            Check(!Fenced(ok) && HigherUses(ok)==1 && !ok.Inventory.ContainsUID(pack.UID) && HigherStones(ok,stone,plus)==10
                && ok.Inventory.Objects.Where(i=>i.ID==stone).All(i=>!i.Bound && i.Plus==plus && i.StackSize==1) && heard=="",
                "unbound "+packId+" pays ten unbound Plus-"+plus+" stones");
            ok=ReloadBall(ok);
            Check(!Fenced(ok) && HigherUses(ok)==1 && HigherStones(ok,stone,plus)==10,"unbound "+packId+" unpack survives production reload");
            var boundP=BallPlayer(town);var boundPack=NestedPack(boundP,packId,1,true);OpenHigher(boundP,boundPack);
            Check(!Fenced(boundP) && HigherStones(boundP,stone,plus)==10 && boundP.Inventory.Objects.Where(i=>i.ID==stone).All(i=>i.Bound && i.Plus==plus),
                "bound "+packId+" cannot launder unbound stones");
            boundP=ReloadBall(boundP);
            Check(!Fenced(boundP) && HigherUses(boundP)==1 && HigherStones(boundP,stone,plus)==10
                && boundP.Inventory.Objects.Where(i=>i.ID==stone).All(i=>i.Bound && i.Plus==plus && i.StackSize==1),
                "bound "+packId+" stones retain binding and Plus after production reload");
            var zero=BallPlayer(town);var zeroPack=Pack(zero,packId,0);OpenHigher(zero,zeroPack);
            Check(!Fenced(zero) && HigherUses(zero)==1 && !zero.Inventory.ContainsUID(zeroPack.UID) && HigherStones(zero,stone,plus)==10,
                "zero-stack "+packId+" is consumed once");
            var piled=BallPlayer(town);var pile=Pack(piled,packId,3);OpenHigher(piled,pile);
            Check(!Fenced(piled) && HigherUses(piled)==1 && pile.StackSize==2 && HigherStones(piled,stone,plus)==10,
                "stack 3 "+packId+" keeps remainder 2");
            piled=ReloadBall(piled);
            Check(!Fenced(piled) && HigherUses(piled)==1 && HigherStones(piled,stone,plus)==10
                && piled.Inventory.Objects.Single(i=>i.ID==packId).StackSize==2,
                "stacked "+packId+" remainder and ten stones survive production reload");
            var tight=BallPlayer(town);var tightPack=Pack(tight,packId,1);FillTo(tight,31);OpenHigher(tight,tightPack);
            Check(!Fenced(tight) && HigherUses(tight)==1 && HigherStones(tight,stone,plus)==10,
                "depleted "+packId+" at Count 31 reuses its slot for ten stones");
            heard="";var full=BallPlayer(town);var fullPack=Pack(full,packId,1);FillTo(full,32);OpenHigher(full,fullPack);
            Check(!Fenced(full) && HigherUses(full)==0 && full.Inventory.ContainsUID(fullPack.UID) && heard.IndexOf("Your inventory is Full.")>=0,
                "depleted "+packId+" at Count 32 refuses without consume");
            var stackedOk=BallPlayer(town);var stackedOkPack=Pack(stackedOk,packId,2);FillTo(stackedOk,30);OpenHigher(stackedOk,stackedOkPack);
            Check(!Fenced(stackedOk) && HigherUses(stackedOk)==1 && stackedOkPack.StackSize==1 && HigherStones(stackedOk,stone,plus)==10,
                "stacked "+packId+" at Count 30 pays ten stones and keeps the remainder");
            heard="";var stackedFull=BallPlayer(town);var stackedPack=Pack(stackedFull,packId,2);FillTo(stackedFull,31);OpenHigher(stackedFull,stackedPack);
            Check(!Fenced(stackedFull) && HigherUses(stackedFull)==0 && stackedPack.StackSize==2 && heard.IndexOf("Your inventory is Full.")>=0,
                "stacked "+packId+" at Count 31 refuses because the remainder keeps its slot");
            var direct=BallPlayer(town);var directPack=Pack(direct,packId,1);ItemHandler.UseItem(directPack,direct);
            Check(!Fenced(direct) && HigherUses(direct)==1 && HigherStones(direct,stone,plus)==10,"direct UseItem opens "+packId);
            var spent=BallPlayer(town);var spentPack=Pack(spent,packId,1);OpenHigher(spent,spentPack);ItemHandler.UseItem(spentPack,spent);
            Check(!Fenced(spent) && HigherUses(spent)==1 && HigherStones(spent,stone,plus)==10,"consumed "+packId+" UID cannot replay");
        }
        var race=BallPlayer(town);var raceItem=Pack(race,729022,1);
        Parallel.For(0,16,n=>OpenHigher(race,raceItem));
        Check(!Fenced(race) && HigherUses(race)==1 && HigherStones(race,730002,2)==10,"parallel packets cannot duplicate +2 stone pack use");
        var deletePlayer=BallPlayer(town);var deleteItem=Pack(deletePlayer,729022,1);
        Query("CREATE FUNCTION suppress_higher_delete() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_higher_delete BEFORE DELETE ON items FOR EACH ROW EXECUTE FUNCTION suppress_higher_delete()");
        try{OpenHigher(deletePlayer,deleteItem);
            Check(Fenced(deletePlayer) && HigherUses(deletePlayer)==0 && deletePlayer.Inventory.ContainsUID(deleteItem.UID) && HigherStones(deletePlayer,730002,2)==0,
                "silent zero-row source DELETE preserves the +2 pack and pays nothing");}
        finally{Query("DROP TRIGGER suppress_higher_delete ON items;DROP FUNCTION suppress_higher_delete()");}
        deletePlayer=ReloadBall(deletePlayer);OpenHigher(deletePlayer,deletePlayer.Inventory.Objects.Single(i=>i.ID==729022));
        Check(!Fenced(deletePlayer) && HigherUses(deletePlayer)==1 && HigherStones(deletePlayer,730002,2)==10,
            "production reload retries +2 pack after source DELETE rollback");
        var updatePlayer=BallPlayer(town);var updateItem=Pack(updatePlayer,729023,2);
        Query("CREATE FUNCTION suppress_higher_update() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_higher_update BEFORE UPDATE ON items FOR EACH ROW EXECUTE FUNCTION suppress_higher_update()");
        try{OpenHigher(updatePlayer,updateItem);
            Check(Fenced(updatePlayer) && HigherUses(updatePlayer)==0 && updateItem.StackSize==2 && HigherStones(updatePlayer,730003,3)==0,
                "silent zero-row stacked UPDATE preserves the +3 pack remainder");}
        finally{Query("DROP TRIGGER suppress_higher_update ON items;DROP FUNCTION suppress_higher_update()");}
        updatePlayer=ReloadBall(updatePlayer);OpenHigher(updatePlayer,updatePlayer.Inventory.Objects.Single(i=>i.ID==729023));
        Check(!Fenced(updatePlayer) && HigherUses(updatePlayer)==1 && HigherStones(updatePlayer,730003,3)==10,
            "production reload retries stacked +3 pack after source UPDATE rollback");
        var partial=BallPlayer(town);var partialItem=Pack(partial,729022,1);
        Query("CREATE SEQUENCE revora_higher_stone_fault_seq START 1;CREATE FUNCTION suppress_higher_partial() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF NEW.id=730002 OR NEW.id=730003 THEN IF nextval(''revora_higher_stone_fault_seq'')>1 THEN RETURN NULL; END IF; END IF; RETURN NEW; END';CREATE TRIGGER suppress_higher_partial BEFORE INSERT ON items FOR EACH ROW EXECUTE FUNCTION suppress_higher_partial()");
        try{OpenHigher(partial,partialItem);
            Check(Fenced(partial) && HigherUses(partial)==0 && partial.Inventory.ContainsUID(partialItem.UID) && HigherStones(partial,730002,2)==0,
                "partial reward insert after the first stone rolls back the pack and every stone");}
        finally{Query("DROP TRIGGER suppress_higher_partial ON items;DROP FUNCTION suppress_higher_partial();DROP SEQUENCE revora_higher_stone_fault_seq");}
        partial=ReloadBall(partial);OpenHigher(partial,partial.Inventory.Objects.Single(i=>i.ID==729022));
        Check(!Fenced(partial) && HigherUses(partial)==1 && HigherStones(partial,730002,2)==10,
            "production reload retries +2 pack after partial reward rollback");
        var receipt=BallPlayer(town);var receiptItem=Pack(receipt,729023,1);
        Query("CREATE FUNCTION suppress_higher_receipt() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_higher_receipt BEFORE INSERT ON revora_higher_stone_pack_uses FOR EACH ROW EXECUTE FUNCTION suppress_higher_receipt()");
        try{OpenHigher(receipt,receiptItem);
            Check(Fenced(receipt) && HigherUses(receipt)==0 && receipt.Inventory.ContainsUID(receiptItem.UID) && HigherStones(receipt,730003,3)==0,
                "silent zero-row receipt insert preserves the +3 pack and pays nothing");}
        finally{Query("DROP TRIGGER suppress_higher_receipt ON revora_higher_stone_pack_uses;DROP FUNCTION suppress_higher_receipt()");}
        receipt=ReloadBall(receipt);OpenHigher(receipt,receipt.Inventory.Objects.Single(i=>i.ID==729023));
        Check(!Fenced(receipt) && HigherUses(receipt)==1 && HigherStones(receipt,730003,3)==10,
            "production reload retries +3 pack after receipt rollback");
        var install=BallPlayer(town);var installItem=Pack(install,729022,1);
        HigherStone.GetField("BeforeInstall",Hidden).SetValue(null,(Action<GameState>)(g=>{throw new InvalidOperationException("install");}));
        try{OpenHigher(install,installItem);
            Check(Fenced(install) && HigherUses(install)==1 && install.Inventory.ContainsUID(installItem.UID) && HigherStones(install,730002,2)==0,
                "post-commit install failure fences without projecting the committed stones");}
        finally{HigherStone.GetField("BeforeInstall",Hidden).SetValue(null,null);}
        install=ReloadBall(install);
        Check(!Fenced(install) && HigherUses(install)==1 && HigherStones(install,730002,2)==10 && install.Inventory.Objects.Count(i=>i.ID==729022)==0,
            "production reload installs the committed +2 stones after the live install failure");
        var stackedInstall=BallPlayer(town);var stackedInstallItem=NestedPack(stackedInstall,729023,2,true);
        HigherStone.GetField("BeforeInstall",Hidden).SetValue(null,(Action<GameState>)(g=>{throw new InvalidOperationException("install");}));
        try{OpenHigher(stackedInstall,stackedInstallItem);
            Check(Fenced(stackedInstall) && HigherUses(stackedInstall)==1 && stackedInstallItem.StackSize==2 && HigherStones(stackedInstall,730003,3)==0,
                "post-commit stacked install failure fences the stale remainder");
            ItemHandler.UseItem(stackedInstallItem,stackedInstall);
            Check(HigherUses(stackedInstall)==1,"fenced stacked source cannot pay again before reload");}
        finally{HigherStone.GetField("BeforeInstall",Hidden).SetValue(null,null);}
        stackedInstall=ReloadBall(stackedInstall);
        var recoveredPack=stackedInstall.Inventory.Objects.Single(i=>i.ID==729023);
        Check(!Fenced(stackedInstall) && recoveredPack.StackSize==1 && recoveredPack.Bound && HigherStones(stackedInstall,730003,3)==10
            && stackedInstall.Inventory.Objects.Where(i=>i.ID==730003).All(i=>i.Bound),
            "production reload recovers the committed bound +3 stones and one pack remainder");
        OpenHigher(stackedInstall,recoveredPack);
        Check(!Fenced(stackedInstall) && HigherUses(stackedInstall)==2 && HigherStones(stackedInstall,730003,3)==20
            && !stackedInstall.Inventory.ContainsUID(recoveredPack.UID),
            "recovered stacked source pays only its remaining unit");
    }
}
