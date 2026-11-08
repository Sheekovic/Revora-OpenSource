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
    static readonly Type GemSoul=typeof(Entity).Assembly.GetType("Revora.Game.Features.GemDragonSoulPacks");
    static DateTime gemSoulNow=DateTime.UtcNow;
    static long GemSoulUses(GameState p){return Scalar("SELECT count(*) FROM revora_gem_dragon_soul_pack_uses WHERE uid=@uid",p.Entity.UID);}
    static void OpenGemSoul(GameState p,ConquerItem item){ItemUsage.Process(PrayerPacket(item.UID),p);}
    static uint[] GemSoulPool(uint pack){return (uint[])GemSoul.GetMethod("Pool",Hidden).Invoke(null,new object[]{pack});}
    static void FillGemSoul(GameState p,int count){while(p.Inventory.Count<count)Pack(p,1088001,1);}
    static void VerifyGemDragonSoulPacks(string runtime,Map town){
        Query("CREATE TABLE IF NOT EXISTS teamarena (LIKE public.teamarena INCLUDING ALL);CREATE TABLE IF NOT EXISTS itemadding (LIKE public.itemadding INCLUDING ALL)");
        Call(GemSoul,null,"Initialize");Call(GemSoul,null,"Initialize");
        GemSoul.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>gemSoulNow));
        GemSoul.GetField("BeforeInstall",Hidden).SetValue(null,null);
        GemSoul.GetField("Roll",Hidden).SetValue(null,(Func<int,int,int>)((min,max)=>min));
        string handler=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemHandler.cs")));
        string usage=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemUsage.cs")));
        string thread=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Thread.cs")));
        string chat=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/Chat.cs")));
        string heroesSvc=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/WayofHeroes.cs")));
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
        Check(handler.IndexOf("Gem or Dragon Soul pack must use the owned service.")>=0 && handler.IndexOf("GemDragonSoulPacks.TryUse")>handler.IndexOf("HigherStonePacks.TryUse")
            && usage.IndexOf("GemDragonSoulPacks.TryHandle")>usage.IndexOf("HigherStonePacks.TryHandle")
            && thread.IndexOf("GemDragonSoulPacks.Initialize")>thread.IndexOf("HigherStonePacks.Initialize"),
            "GemDragonSoulPacks intercepts sit after HigherStonePacks and the four leftover cases fail closed");
        Check(audit.IndexOf("| GemDragonSoulPacks | 3005130,3005132,3005133,3005135 |")>=0
            && audit.IndexOf("| 3005130 | owned-fail-closed | source+items |")>=0
            && audit.IndexOf("| 3005132 | owned-fail-closed | source+items |")>=0
            && audit.IndexOf("| 3005133 | owned-fail-closed | source+items |")>=0
            && audit.IndexOf("| 3005135 | owned-fail-closed | source+items |")>=0
            && audit.IndexOf("| 3005368 | live-split-write | source+items+cp |")>=0,
            "audit owns the four Way of Heroes packs and leaves LuxuryGoldEquipmentPack live");
        Check(catalog.IndexOf("3005130@@RefinedhGem(B)Pack")>=0 && catalog.IndexOf("3005132@@SuperGem(B)Pack")>=0
            && catalog.IndexOf("3005133@@P4DragonSoulBag")>=0 && catalog.IndexOf("3005135@@P6DragonSoulBag")>=0
            && catalog.IndexOf("3005368@@LuxuryGoldEquipmentPack")>=0,
            "installed catalog keeps the gem, Dragon Soul and luxury gold names");
        Check(heroes.IndexOf("Prize1=3005130@@1@@1")>=0 && heroes.IndexOf("Prize1=3005130@@2@@1")>=0
            && heroes.IndexOf("Prize1=3005132@@1@@1")>=0 && heroes.IndexOf("Prize1=3005133@@1@@1")>=0
            && heroes.IndexOf("Prize1=3005135@@1@@1")>=0
            && File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/WayOfHeroesClaims.cs"))).IndexOf("Plus=0,Bound=true")>=0
            && heroesSvc.IndexOf("WayOfHeroesClaims.Claim")>=0 && heroesSvc.IndexOf("AddBoundItem(substage.Prize[0]")<0,
            "Way of Heroes claims the four packs through the owned bound plus-0 grant");
        Check(chat.IndexOf("case \"testitem\"")>=0 && chat.IndexOf("item.ID = 3005368")>=0 && heroes.IndexOf("3005368")<0
            && handler.IndexOf("int dis = Rand.Next(1, 4);")>=0 && handler.IndexOf("if (dis == 4)")>=0,
            "LuxuryGoldEquipmentPack is only a testitem chat reference, and Next(1, 4) never reaches 4");
        uint[] refined=(uint[])GemSoul.GetField("RefinedIds",Hidden).GetValue(null);
        uint[] super=(uint[])GemSoul.GetField("SuperIds",Hidden).GetValue(null);
        uint[] phase4=GemSoulPool(3005133),phase6=GemSoulPool(3005135);
        Check(refined.SequenceEqual(GemSoulPool(3005130)) && super.SequenceEqual(GemSoulPool(3005132))
            && phase4.Length>0 && phase6.Length>0
            && phase4.All(id=>ConquerItemInformation.BaseInformations[id].PurificationLevel==4)
            && phase6.All(id=>ConquerItemInformation.BaseInformations[id].PurificationLevel==6),
            "refined and super pools are the leftover gem lists; purification pools are installed levels 4 and 6");
        System.Console.WriteLine("Gem Dragon Soul pools: refined "+refined.Length+" super "+super.Length+" phase4 "+phase4.Length+" phase6 "+phase6.Length);
        uint[] packs=new uint[]{3005130u,3005132u,3005133u,3005135u};
        for(int n=0;n<packs.Length;n++){
            uint packId=packs[n];
            Check(honorIni.IndexOf(packId.ToString()+",")<0 && eshop.IndexOf(packId.ToString())<0 && v2.IndexOf(packId.ToString())<0
                && shopTxt.IndexOf(packId.ToString())<0 && shopDatText.IndexOf(packId.ToString())<0 && exchange.IndexOf(packId.ToString())<0
                && drops.IndexOf(packId.ToString())<0 && monsters.IndexOf(packId.ToString())<0,
                packId+" has no HonorShop, emoney, Shop, exchange, DropItemTable or MonsterTable producer");
            uint[] pool=GemSoulPool(packId);
            GemSoul.GetField("Roll",Hidden).SetValue(null,(Func<int,int,int>)((min,max)=>min));
            var first=BallPlayer(town);var firstPack=Pack(first,packId,1);OpenGemSoul(first,firstPack);
            var firstReward=first.Inventory.Objects.Single(i=>i.ID==pool[0] && i.Bound && i.Plus==0 && i.StackSize==1);
            Check(!Fenced(first) && GemSoulUses(first)==1 && !first.Inventory.ContainsUID(firstPack.UID)
                && firstReward.Durability==ConquerItemInformation.BaseInformations[pool[0]].Durability,
                "ordinary non-GM "+packId+" pays the first pool reward bound, plus 0, once");
            first=ReloadBall(first);
            Check(!Fenced(first) && GemSoulUses(first)==1 && first.Inventory.Objects.Count(i=>i.ID==pool[0] && i.Bound && i.Plus==0)==1,
                packId+" first reward survives production reload");
            ItemHandler.UseItem(firstPack,first);
            Check(GemSoulUses(first)==1,"a consumed "+packId+" cannot pay again");
            GemSoul.GetField("Roll",Hidden).SetValue(null,(Func<int,int,int>)((min,max)=>max-1));
            var last=BallPlayer(town);var lastPack=NestedPack(last,packId,1,true);OpenGemSoul(last,lastPack);
            Check(!Fenced(last) && GemSoulUses(last)==1 && last.Inventory.Objects.Any(i=>i.ID==pool[pool.Length-1] && i.Bound && i.Plus==0),
                "bound "+packId+" pays the last pool reward still bound");
            var unbound=BallPlayer(town);var unboundPack=Pack(unbound,packId,1);OpenGemSoul(unbound,unboundPack);
            Check(!Fenced(unbound) && unbound.Inventory.Objects.Any(i=>i.ID==pool[pool.Length-1] && i.Bound),
                "an unbound "+packId+" still pays a bound reward");
            GemSoul.GetField("Roll",Hidden).SetValue(null,(Func<int,int,int>)((min,max)=>min));
            var piled=BallPlayer(town);var pile=Pack(piled,packId,3);OpenGemSoul(piled,pile);
            Check(!Fenced(piled) && GemSoulUses(piled)==1 && pile.StackSize==2 && piled.Inventory.Objects.Any(i=>i.ID==pool[0] && i.Bound),
                "stack 3 "+packId+" keeps remainder 2");
            piled=ReloadBall(piled);
            Check(!Fenced(piled) && GemSoulUses(piled)==1 && piled.Inventory.Objects.Single(i=>i.ID==packId).StackSize==2,
                "stacked "+packId+" remainder survives production reload");
            var zero=BallPlayer(town);var zeroPack=Pack(zero,packId,0);OpenGemSoul(zero,zeroPack);
            Check(!Fenced(zero) && GemSoulUses(zero)==1 && !zero.Inventory.ContainsUID(zeroPack.UID),
                "zero-stack "+packId+" is consumed once");
            var tight=BallPlayer(town);var tightPack=Pack(tight,packId,1);FillGemSoul(tight,40);OpenGemSoul(tight,tightPack);
            Check(!Fenced(tight) && GemSoulUses(tight)==1 && !tight.Inventory.ContainsUID(tightPack.UID),
                "a single "+packId+" at Count 40 reuses its slot");
            string heard="";GemSoul.GetField("Spoken",Hidden).SetValue(null,(Action<string>)(m=>{heard=heard+m+"|";}));
            var stackedFull=BallPlayer(town);var stackedPack=Pack(stackedFull,packId,2);FillGemSoul(stackedFull,40);OpenGemSoul(stackedFull,stackedPack);
            Check(!Fenced(stackedFull) && GemSoulUses(stackedFull)==0 && stackedPack.StackSize==2 && heard.IndexOf("Your inventory is Full.")>=0,
                "stacked "+packId+" at Count 40 refuses without consume");
        }
        uint[] saved=GemSoulPool(3005133);
        GemSoul.GetField("PhaseFour",Hidden).SetValue(null,new uint[0]);
        try{
            string heard="";GemSoul.GetField("Spoken",Hidden).SetValue(null,(Action<string>)(m=>{heard=heard+m+"|";}));
            var empty=BallPlayer(town);var emptyPack=Pack(empty,3005133,1);OpenGemSoul(empty,emptyPack);
            Check(!Fenced(empty) && GemSoulUses(empty)==0 && empty.Inventory.ContainsUID(emptyPack.UID) && heard.IndexOf("This pack has no installed reward.")>=0,
                "an empty purification pool refuses without consuming the P4 bag");
        }finally{GemSoul.GetField("PhaseFour",Hidden).SetValue(null,saved);}
        GemSoul.GetField("Roll",Hidden).SetValue(null,(Func<int,int,int>)((min,max)=>min));
        var race=BallPlayer(town);var raceItem=Pack(race,3005130,1);
        Parallel.For(0,16,n=>OpenGemSoul(race,raceItem));
        Check(!Fenced(race) && GemSoulUses(race)==1 && race.Inventory.Objects.Count(i=>i.ID==refined[0] && i.Bound)==1,
            "parallel packets cannot open one refined gem pack twice");
        var direct=BallPlayer(town);var directPack=Pack(direct,3005132,1);ItemHandler.UseItem(directPack,direct);
        Check(!Fenced(direct) && GemSoulUses(direct)==1 && direct.Inventory.Objects.Any(i=>i.ID==super[0] && i.Bound),
            "direct UseItem opens a super gem pack");
        var deletePlayer=BallPlayer(town);var deleteItem=Pack(deletePlayer,3005130,1);
        Query("CREATE FUNCTION suppress_gem_soul_delete() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_gem_soul_delete BEFORE DELETE ON items FOR EACH ROW EXECUTE FUNCTION suppress_gem_soul_delete()");
        try{OpenGemSoul(deletePlayer,deleteItem);
            Check(Fenced(deletePlayer) && GemSoulUses(deletePlayer)==0 && deletePlayer.Inventory.ContainsUID(deleteItem.UID),
                "silent zero-row source DELETE preserves the refined gem pack");}
        finally{Query("DROP TRIGGER suppress_gem_soul_delete ON items;DROP FUNCTION suppress_gem_soul_delete()");}
        deletePlayer=ReloadBall(deletePlayer);OpenGemSoul(deletePlayer,deletePlayer.Inventory.Objects.Single(i=>i.ID==3005130));
        Check(!Fenced(deletePlayer) && GemSoulUses(deletePlayer)==1,"production reload retries the refined gem pack after DELETE rollback");
        var receipt=BallPlayer(town);var receiptItem=Pack(receipt,3005135,1);
        Query("CREATE FUNCTION suppress_gem_soul_receipt() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_gem_soul_receipt BEFORE INSERT ON revora_gem_dragon_soul_pack_uses FOR EACH ROW EXECUTE FUNCTION suppress_gem_soul_receipt()");
        try{OpenGemSoul(receipt,receiptItem);
            Check(Fenced(receipt) && GemSoulUses(receipt)==0 && receipt.Inventory.ContainsUID(receiptItem.UID),
                "silent zero-row receipt insert preserves the P6 bag");}
        finally{Query("DROP TRIGGER suppress_gem_soul_receipt ON revora_gem_dragon_soul_pack_uses;DROP FUNCTION suppress_gem_soul_receipt()");}
        receipt=ReloadBall(receipt);OpenGemSoul(receipt,receipt.Inventory.Objects.Single(i=>i.ID==3005135));
        Check(!Fenced(receipt) && GemSoulUses(receipt)==1,"production reload retries the P6 bag after receipt rollback");
        var install=BallPlayer(town);var installItem=Pack(install,3005132,1);
        GemSoul.GetField("BeforeInstall",Hidden).SetValue(null,(Action<GameState>)(g=>{throw new InvalidOperationException("install");}));
        try{OpenGemSoul(install,installItem);
            Check(Fenced(install) && GemSoulUses(install)==1 && install.Inventory.ContainsUID(installItem.UID),
                "post-commit install failure fences without projecting the committed gem");}
        finally{GemSoul.GetField("BeforeInstall",Hidden).SetValue(null,null);}
        install=ReloadBall(install);
        Check(!Fenced(install) && GemSoulUses(install)==1 && install.Inventory.Objects.Any(i=>i.ID==super[0] && i.Bound)
            && install.Inventory.Objects.Count(i=>i.ID==3005132)==0,
            "production reload installs the committed super gem after the live install failure");
        OpenGemSoul(install,installItem);
        Check(GemSoulUses(install)==1 && install.Inventory.Objects.Count(i=>i.ID==super[0])==1,
            "REVIEW-322 replay after failed projection and reload cannot pay twice");

        // Exercise every installed reward, not just the endpoints of each pool.
        foreach(uint packId in packs){
            uint[] pool=GemSoulPool(packId);
            for(int index=0;index<pool.Length;index++){
                int choice=index;
                GemSoul.GetField("Roll",Hidden).SetValue(null,(Func<int,int,int>)((min,max)=>choice));
                var p=BallPlayer(town);var source=NestedPack(p,packId,1,true);OpenGemSoul(p,source);
                var reward=p.Inventory.Objects.Single(i=>i.ID==pool[choice]);
                Check(!Fenced(p) && reward.Bound && reward.Plus==0 && reward.StackSize==1
                    && GemSoulReceiptMatches(p,source,reward,choice,pool.Length),
                    "REVIEW-322 durable outcome matches installed reward "+packId+"/"+choice);
            }
        }
        GemSoul.GetField("Roll",Hidden).SetValue(null,(Func<int,int,int>)((min,max)=>min));
        foreach(uint packId in packs){
            foreach(ushort stack in new ushort[]{1,2}){
                var p=BallPlayer(town);var source=NestedPack(p,packId,stack,true);
                Query("CREATE FUNCTION suppress_gem_soul_reward() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_gem_soul_reward BEFORE INSERT ON items FOR EACH ROW EXECUTE FUNCTION suppress_gem_soul_reward()");
                try{OpenGemSoul(p,source);
                    Check(Fenced(p) && GemSoulUses(p)==0
                        && Scalar("SELECT count(*) FROM items WHERE entityid=@uid",p.Entity.UID)==1
                        && Scalar("SELECT stacksize FROM items WHERE entityid=@uid",p.Entity.UID)==stack,
                        "REVIEW-322 failed reward insert rolls back source and receipt "+packId+" stack "+stack);
                }finally{Query("DROP TRIGGER suppress_gem_soul_reward ON items;DROP FUNCTION suppress_gem_soul_reward()");}
                p=ReloadBall(p);OpenGemSoul(p,p.Inventory.Objects.Single(i=>i.ID==packId));
                Check(!Fenced(p) && GemSoulUses(p)==1 && p.Inventory.Objects.Count(i=>i.ID==GemSoulPool(packId)[0])==1,
                    "REVIEW-322 reload retries failed reward insertion once "+packId+" stack "+stack);
            }
        }

        var guarded=BallPlayer(town);var guardedItem=Pack(guarded,3005130,1);
        guardedItem.Lock=1;OpenGemSoul(guarded,guardedItem);guardedItem.Lock=0;
        guardedItem.Suspicious=true;OpenGemSoul(guarded,guardedItem);guardedItem.Suspicious=false;
        guardedItem.InWardrobe=true;OpenGemSoul(guarded,guardedItem);guardedItem.InWardrobe=false;
        guarded.Trade.InTrade=true;OpenGemSoul(guarded,guardedItem);guarded.Trade.InTrade=false;
        var stranger=BallPlayer(town);ItemHandler.UseItem(guardedItem,stranger);
        byte[] malformed=PrayerPacket(guardedItem.UID);malformed[malformed.Length-1]^=1;OpenGemSoulPacketGuard(guarded,malformed);
        Check(!Fenced(guarded) && GemSoulUses(guarded)==0 && GemSoulUses(stranger)==0
            && guarded.Inventory.ContainsUID(guardedItem.UID),"REVIEW-322 locked, suspicious, wardrobe, trade, foreign and bad-seal uses refuse");
        Query("UPDATE items SET bound=1 WHERE uid=@item","item",(long)guardedItem.UID);
        OpenGemSoul(guarded,guardedItem);
        Check(Fenced(guarded) && GemSoulUses(guarded)==0
            && Scalar("SELECT count(*) FROM items WHERE entityid=@uid",guarded.Entity.UID)==1,
            "REVIEW-322 stale durable binding fences without consuming or paying");
        guarded=ReloadBall(guarded);OpenGemSoul(guarded,guarded.Inventory.Objects.Single(i=>i.ID==3005130));
        Check(!Fenced(guarded) && GemSoulUses(guarded)==1,"REVIEW-322 durable binding reload permits one valid retry");
    }
    static void OpenGemSoulPacketGuard(GameState p,byte[] packet){
        Check((bool)GemSoul.GetMethod("TryHandle",Hidden).Invoke(null,new object[]{p,packet}),
            "REVIEW-322 owned pack claims a malformed packet without falling through");
    }
    static bool GemSoulReceiptMatches(GameState p,ConquerItem source,ConquerItem reward,int pick,int size){
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand(
            "SELECT pack_uid,pack_type,result_uid,result_type,pick,pool_size FROM revora_gem_dragon_soul_pack_uses WHERE uid=@uid",c)){
            q.Parameters.AddWithValue("uid",(long)p.Entity.UID);
            using(var r=q.ExecuteReader()){
                return r.Read() && Convert.ToInt64(r[0])==source.UID && Convert.ToInt64(r[1])==source.ID
                    && Convert.ToInt64(r[2])==reward.UID && Convert.ToInt64(r[3])==reward.ID
                    && Convert.ToInt32(r[4])==pick && Convert.ToInt32(r[5])==size && !r.Read();
            }
        }}
    }
}
