using System;
using System.Linq;
using System.Collections.Generic;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static GameState ancientCollector;
    static List<GameState> ancientClassPlayers;
    static readonly Type AncientQuest=typeof(Entity).Assembly.GetType("Revora.Game.Features.AncientDevilQuest");
    static byte[] AncientDialogFrame(uint npc,byte option) {
        var bytes=new byte[28];Writer.WriteUInt16(20,0,bytes);Writer.WriteUInt16(2032,2,bytes);
        Writer.WriteUInt32(npc,8,bytes);bytes[14]=option;Writer.WriteUInt64(PacketHandler.ClientSeal,20,bytes);return bytes;
    }
    static void AncientDialog(GameState p,uint npc,byte option) {
        p.ActiveNpc=npc;
        if(option==0)Npcs.GetDialog(new NpcRequest(true){NpcID=npc,OptionID=0},p);
        else PacketHandler.HandlePacket(AncientDialogFrame(npc,option),p);
    }
    static GameState AncientVisitor(Map town,Map bird,byte profession=25) {
        var p=HousePlayer(town);p.Entity.Class=profession;p.BindWorldMap(1015);p.Entity.X=713;p.Entity.Y=584;
        if(!EntityTable.SaveEntity(p))throw new Exception("Ancient visitor save failed.");return p;
    }
    static object CurrentAncient(GameState p) {
        var registry=AncientWorld.GetField("Hunts",Hidden).GetValue(null);
        return registry.GetType().GetProperty("Item").GetValue(registry,new object[]{p.Entity.MapID});
    }
    static void AncientEnter(GameState p) {AncientDialog(p,300010,0);AncientDialog(p,300010,1);}
    static void VerifyAncientQuest(string runtime,Map town) {
        InitializeHousingFixture(runtime,town);
        var terrain=World(runtime,1082);DMaps.MapPaths[1082]="maps\\1082.map";
        var island=AncientMap(1052,terrain);var bird=World(runtime,1015);DMaps.MapPaths[1015]="maps\\1015.map";
        Query("INSERT INTO npcs SELECT * FROM public.npcs WHERE id BETWEEN 300011 AND 300015");
        AncientWorld.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>ancientNow));
        Call(AncientQuest,null,"Initialize");Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.AtomicTrade"),null,"Initialize");
        Check(bird.Npcs[300010].X==715 && bird.Npcs[300010].Y==584 && island.Npcs.ContainsKey(300016),"Exorcist and island return guide have reachable installed-terrain positions");
        byte[] classes={25,15,145,135,45,55,65,75,85,165};
        uint[] expected={710011,710012,710013,710014,710015,710635,711336,711600,3004947,3301712};
        var players=new List<GameState>();
        for(int i=0;i<classes.Length;i++) {
            var p=AncientVisitor(town,bird,classes[i]);Check(p.Account.State==AccountTable.AccountState.Player,"class route uses ordinary account "+classes[i]);
            uint wardrobe=i==9?StoredQuestGarment(p):0;
            AncientEnter(p);Check(!Fenced(p) && p.Entity.MapID==1052,"Exorcist dialog admits class "+classes[i]);
            uint guard=i<5?300011u+(uint)i:300011u;
            AdventureNear(p,guard);AncientDialog(p,guard,0);AncientDialog(p,guard,1);
            Check(!Fenced(p) && p.Entity.MapID>=11000 && p.Map.Npcs.ContainsKey(300016),"guard starts private class trial "+classes[i]);
            var h=CurrentAncient(p);var body=AncientBody(h);AncientHit(p.Entity,body,body.Hitpoints);Call(AncientQuest,null,"Tick");
            Check(!Fenced(p) && p.Entity.MapID==1052 && AncientState(p,"victory")==1,"real lethal damage returns class hunter with pending certificate "+classes[i]);
            AdventureNear(p,guard);AncientDialog(p,guard,0);AncientDialog(p,guard,2);
            Check(!Fenced(p) && p.Inventory.Count==1 && p.Inventory.Objects.Single().Bound,"guard claims exactly one personal certificate "+classes[i]);
            AncientDialog(p,guard,2);Check(p.Inventory.Count==1,"duplicate claim option is harmless "+classes[i]);
            if(i==0) {
                var visitor=AncientVisitor(town,bird);AncientEnter(visitor);AdventureNear(visitor,guard);
                p.Entity.TradeOn=visitor.Entity.TradeOn=true;TradeSend(p,Trade.Request,visitor.Entity.UID);TradeSend(visitor,Trade.Request,p.Entity.UID);
                Check(p.Trade.InTrade && visitor.Trade.InTrade,"certificate transfer probe uses normal trade admission");
                TradeSend(p,Trade.AddItem,p.Inventory.Objects.Single().UID);
                Check(p.Trade.Items.Count==0 && visitor.Trade.Items.Count==0,"personal earned certificate cannot be offered in player trade");
                TradeSend(p,Trade.Close,0);AncientDialog(p,guard,0);
            }
            AncientDialog(p,guard,3);AncientDialog(p,guard,3);
            Check(!Fenced(p) && p.Inventory.Count==1 && p.Inventory.Objects.Single().ID==expected[i] && !p.Inventory.Objects.Single().Bound,
                "guard exchanges own-class amulet once "+classes[i]);
            p=ReloadBall(p);AdventureWalk(p,216,208);players.Add(p);
            Check(!Fenced(p) && p.Inventory.Objects.Single().ID==expected[i] && p.Entity.ConquerPoints==36000 && p.Entity.Money==7800000,
                "earned class amulet survives reload with no invented currency fee "+classes[i]);
            if(wardrobe!=0)Check(Scalar("SELECT count(*) FROM items WHERE uid=@uid AND position=0 AND warehouse=0 AND inwardrobe=1",wardrobe)==1,"Windwalker trial, certificate, amulet exchange and production reload preserve stored wardrobe design");
        }
        var collector=players[0];
        for(int i=1;i<players.Count;i++) {
            var donor=players[i];uint item=donor.Inventory.Objects.Single().UID;
            if(i==8) {
                Check(!HasAchievement(collector,11020),"eight earned classes do not satisfy the nine-class collection");
                Query("CREATE FUNCTION suppress_ancient_achievement() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF NEW.achievement_id=11020 THEN RETURN NULL; END IF; RETURN NEW; END';CREATE TRIGGER suppress_ancient_achievement BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW EXECUTE FUNCTION suppress_ancient_achievement()");
                try {
                    collector.Entity.TradeOn=donor.Entity.TradeOn=true;
                    TradeSend(donor,Trade.Request,collector.Entity.UID);TradeSend(collector,Trade.Request,donor.Entity.UID);
                    TradeSend(donor,Trade.AddItem,item);TradeSend(donor,Trade.Accept,0);TradeSend(collector,Trade.Accept,0);
                    Check(Fenced(donor) && Fenced(collector) && Scalar("SELECT count(*) FROM items WHERE entityid=@uid",collector.Entity.UID)==8
                        && Scalar("SELECT count(*) FROM revora_ancient_collections WHERE uid=@uid",collector.Entity.UID)==0,
                        "failed collection achievement rolls back final trade and all collection receipts");
                }finally{Query("DROP TRIGGER suppress_ancient_achievement ON revora_achievement_evidence;DROP FUNCTION suppress_ancient_achievement()");}
                collector=ReloadBall(collector);donor=ReloadBall(donor);players[0]=collector;players[i]=donor;
            }
            collector.Entity.TradeOn=donor.Entity.TradeOn=true;
            TradeSend(donor,Trade.Request,collector.Entity.UID);TradeSend(collector,Trade.Request,donor.Entity.UID);
            Check(donor.Trade.InTrade && collector.Trade.InTrade,"ordinary island trade opens "+classes[i]);
            TradeSend(donor,Trade.AddItem,item);TradeSend(donor,Trade.Accept,0);TradeSend(collector,Trade.Accept,0);
            Check(!Fenced(donor) && !Fenced(collector) && collector.Inventory.ContainsUID(item) && donor.Inventory.Count==0,
                "earned amulet transfers through atomic player trade "+classes[i]);
            Check(HasAchievement(collector,11020)==(i>=8),"collection boundary updates achievement immediately after committed trade "+i);
        }
        collector=ReloadBall(collector);
        Check(collector.Inventory.Count==10 && collector.Inventory.Objects.Select(i=>i.ID).OrderBy(i=>i).SequenceEqual(expected.OrderBy(i=>i)),
            "ten actual class trials and nine trades produce the complete distinct collection after reload");
        Check(Scalar("SELECT count(*) FROM revora_ancient_trials t JOIN items i ON i.uid=t.amulet_uid WHERE i.entityid=@uid AND t.state='exchanged'",collector.Entity.UID)==10,
            "every collected amulet retains a durable trial earning receipt");
        new TitleStorage().Login(collector);
        Check(HasAchievement(collector,11020),"earned nine-class collection survives reload before any summon or boss fight");
        Check(Scalar("SELECT count(*) FROM revora_ancient_collection_items i JOIN revora_ancient_collections c ON c.id=i.collection JOIN revora_ancient_trials t ON t.id=i.origin_trial WHERE c.uid=@uid AND t.amulet_uid=i.item_uid AND t.amulet=i.item_type",collector.Entity.UID)==9,
            "one collection receipt retains exactly nine legitimate earning origins after the tenth trade");
        var fakeDonor=HousePlayer(town);var fakeCollector=HousePlayer(town);
        foreach(uint type in expected.Take(9)){var fake=Pack(fakeDonor,type,1);fake.Bound=false;Query("UPDATE items SET bound=0 WHERE uid=@uid","uid",(long)fake.UID);}
        TradeOpen(fakeDonor,fakeCollector);foreach(var fake in fakeDonor.Inventory.Objects.ToArray())TradeSend(fakeDonor,Trade.AddItem,fake.UID);
        TradeSend(fakeDonor,Trade.Accept,0);TradeSend(fakeCollector,Trade.Accept,0);
        Check(!Fenced(fakeCollector) && fakeCollector.Inventory.Count==9 && !HasAchievement(fakeCollector,11020)
            && Scalar("SELECT count(*) FROM revora_ancient_collections WHERE uid=@uid",fakeCollector.Entity.UID)==0,
            "nine traded catalog items without earned trial origins do not award collection");
        var lastExchange=AncientVisitor(town,bird);AncientEnter(lastExchange);AdventureWalk(lastExchange,216,208);
        var borrowed=collector.Inventory.Objects.Where(i=>i.ID!=710011 && i.ID!=3301712).Select(i=>i.UID).ToArray();
        collector.Entity.TradeOn=lastExchange.Entity.TradeOn=true;
        TradeSend(collector,Trade.Request,lastExchange.Entity.UID);TradeSend(lastExchange,Trade.Request,collector.Entity.UID);
        foreach(uint uid in borrowed)TradeSend(collector,Trade.AddItem,uid);
        TradeSend(collector,Trade.Accept,0);TradeSend(lastExchange,Trade.Accept,0);
        Check(lastExchange.Inventory.Count==8 && !HasAchievement(lastExchange,11020),"eight legitimately traded classes await the recipient's own amulet");
        AdventureNear(lastExchange,300011);AncientDialog(lastExchange,300011,0);AncientDialog(lastExchange,300011,1);
        var finalDevil=AncientBody(CurrentAncient(lastExchange));AncientHit(lastExchange.Entity,finalDevil,finalDevil.Hitpoints);Call(AncientQuest,null,"Tick");
        AdventureNear(lastExchange,300011);AncientDialog(lastExchange,300011,0);AncientDialog(lastExchange,300011,2);AncientDialog(lastExchange,300011,3);
        Check(!Fenced(lastExchange) && lastExchange.Inventory.Count==9 && HasAchievement(lastExchange,11020),"ninth class from real certificate exchange also commits collection achievement immediately");
        AdventureWalk(lastExchange,216,208);TradeSend(lastExchange,Trade.Request,collector.Entity.UID);TradeSend(collector,Trade.Request,lastExchange.Entity.UID);
        foreach(uint uid in borrowed)TradeSend(lastExchange,Trade.AddItem,uid);
        TradeSend(lastExchange,Trade.Accept,0);TradeSend(collector,Trade.Accept,0);
        Check(collector.Inventory.Count==10 && lastExchange.Inventory.Count==1 && HasAchievement(lastExchange,11020),"collection evidence survives later ordinary transfer of its amulets");
        AdventureNear(collector,300016);AncientDialog(collector,300016,0);AncientDialog(collector,300016,1);
        Check(collector.Entity.MapID==1015 && !Fenced(collector),"island return dialogue reaches Bird Island");
        collector=ReloadBall(collector);Check(collector.Entity.MapID==1015 && collector.Inventory.Count==10,"return location and traded collection survive reload");
        ancientCollector=collector;ancientClassPlayers=players;ancientClassPlayers[0]=collector;
        var novice=AncientVisitor(town,bird);novice.Entity.Level=49;EntityTable.SaveEntity(novice);
        AncientDialog(novice,300010,0);AncientDialog(novice,300010,1);Check(novice.Entity.MapID==1015 && !Fenced(novice),"level 49 cannot accept passage");
        var stale=AncientVisitor(town,bird);AncientDialog(stale,300010,1);Check(stale.Entity.MapID==1015,"unoffered travel option does nothing");
        AncientDialog(stale,300010,0);ancientNow=ancientNow.AddSeconds(61);AncientDialog(stale,300010,1);Check(stale.Entity.MapID==1015,"expired travel offer does nothing");
        AncientDialog(stale,300010,0);stale.Entity.X=690;AncientDialog(stale,300010,1);Check(stale.Entity.MapID==1015,"distant travel confirmation does nothing");stale.Entity.X=713;
        AncientDialog(stale,300010,0);stale.Entity.Class=15;AncientDialog(stale,300010,1);Check(stale.Entity.MapID==1015,"changed class invalidates menu confirmation");stale.Entity.Class=25;
        AncientDialog(stale,300010,0);var wrong=AncientDialogFrame(300011,1);PacketHandler.HandlePacket(wrong,stale);Check(stale.Entity.MapID==1015,"mismatched NPC identity does nothing");
        foreach(string invalid in new[]{"short","trailing","text","oversized"}) {
            AncientDialog(stale,300010,0);var frame=AncientDialogFrame(300010,1);
            if(invalid=="short")Writer.WriteUInt16(19,0,frame);
            if(invalid=="trailing")Array.Resize(ref frame,29);
            if(invalid=="text")frame[17]=255;
            if(invalid=="oversized"){Array.Resize(ref frame,132);Writer.WriteUInt16(124,0,frame);}
            // Keep the transport seal valid to exercise this NPC family's bounds, not the unrelated socket disconnect fixture.
            Writer.WriteUInt64(PacketHandler.ClientSeal,System.BitConverter.ToUInt16(frame,0),frame);
            PacketHandler.HandlePacket(frame,stale);Check(stale.Entity.MapID==1015 && !Fenced(stale),"malformed Ancient dialog refused: "+invalid);
        }
        AncientDialog(stale,300010,0);stale=ReloadBall(stale);AncientDialog(stale,300010,1);
        Check(stale.Entity.MapID==1015 && !Fenced(stale),"reconnect invalidates the prior session's NPC confirmation");
        AncientEnter(stale);AdventureNear(stale,300012);AncientDialog(stale,300012,0);AncientDialog(stale,300012,1);
        Check(stale.Entity.MapID==1052 && AncientState(stale,"hunting")==0,"classic class cannot enter another class guard trial");
        AdventureNear(stale,300011);AncientDialog(stale,300011,0);AncientDialog(stale,300011,1);AdventureNear(stale,300016);
        AncientDialog(stale,300016,0);AncientDialog(stale,300016,1);
        Check(!Fenced(stale) && stale.Entity.MapID==1052 && AncientState(stale,"abandoned")==1,"private trial return guide abandons unfinished hunt safely");
    }
}
