using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type RetiredDyes=typeof(Entity).Assembly.GetType("Revora.Game.Features.RetiredHairDyes");
    static DateTime dyeNow;
    static byte[] CosmeticFrame(params byte[] payload) {
        var frame=new byte[payload.Length+12];Array.Copy(payload,0,frame,4,payload.Length);
        Writer.WriteUInt16((ushort)(frame.Length-8),0,frame);Writer.WriteUInt16(3510,2,frame);
        Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);return frame;
    }
    static void CosmeticSend(GameState p,params byte[] payload) { PacketHandler.HandlePacket(CosmeticFrame(payload),p); }
    static long CosmeticAchievements(GameState p) { return Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id IN(10113,10114,10116)",p.Entity.UID); }
    static void VerifyCosmeticHandler(Map map) {
        var p=CosmeticPlayer(map);
        CosmeticSend(p,8,0);CosmeticSend(p,8,9);CosmeticSend(p,8,8);
        Check(!Fenced(p) && p.Entity.Money==50000 && CosmeticAchievements(p)==0,"native query and both windows cause no payment or achievement");
        CosmeticSend(p,8,3,18,4,8,0,16,3);
        Check(p.Entity.HairStyle==2 && p.Entity.Money==50000,"production handler rejects selecting an unowned look");
        CosmeticSend(p,8,4,18,4,8,0,16,3);
        Check(!Fenced(p) && p.Entity.Money==49500 && CosmeticAchievements(p)==0,"native purchase acknowledges saved ownership without awarding a change achievement");
        CosmeticSend(p,8,3,18,4,8,0,16,3);
        Check(!Fenced(p) && p.Entity.HairStyle==3 && CosmeticAchievements(p)==1 && p.Entity.MyAchievement!=null,"native hairstyle selection saves its achievement and refreshes player progression");
        CosmeticSend(p,8,7,26,4,8,3,16,6);
        Check(p.Entity.Money==48500 && CosmeticAchievements(p)==1 && p.Entity.HairStyle==3,"buying colour is separate from completing a dye action");
        CosmeticSend(p,8,6,26,4,8,3,16,6);
        Check(!Fenced(p) && p.Entity.HairStyle==6003 && CosmeticAchievements(p)==2,"native worn colour selection earns the dye achievement in the same transaction");
        p.Entity.Class=75;CosmeticSend(p,8,4,18,5,8,1,16,154,1);CosmeticSend(p,8,3,18,5,8,1,16,154,1);
        Check(!Fenced(p) && p.Entity.Face==154 && CosmeticAchievements(p)==3 && p.Entity.Money==48000,"ordinary pirate avatar selection earns the third cosmetic achievement");
        Check(HasAchievement(p,10113) && HasAchievement(p,10114) && HasAchievement(p,10116),"all three completed cosmetics appear immediately in the achievement packet");
        CosmeticSend(p,8,4,18,4,8,0,16,3);CosmeticSend(p,8,7,26,4,8,3,16,6);CosmeticSend(p,8,3,18,5,8,1,16,154,1);
        Check(p.Entity.Money==48000 && CosmeticAchievements(p)==3 && EntityTable.SaveEntity(p),"native duplicate requests preserve all three achievements and never charge again");
        var malformed=CosmeticPlayer(map);
        foreach(byte[] body in new[]{new byte[]{8,7,42,0},new byte[]{8,3,18,4,8,2,16,3},new byte[]{8,7,26,4,8,3,16,0},new byte[]{8,0,8,0},new byte[]{8,3,18,2,8,0}})
            FaceAndHair.Handle(malformed,CosmeticFrame(body));
        var truncated=CosmeticFrame(8,4,18,4,8,0,16,3);for(int i=0;i<truncated.Length;i++)FaceAndHair.Handle(malformed,truncated.Take(i).ToArray());
        Check(!Fenced(malformed) && malformed.Entity.Money==50000 && CosmeticsSnapshot(malformed).Length==0,"malformed and truncated native requests cannot reach paid mutations");
        var fault=CosmeticPlayer(map);CosmeticSend(fault,8,4,18,4,8,0,16,3);
        Query("CREATE FUNCTION suppress_cosmetic_achievement() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER suppress_cosmetic_achievement BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW EXECUTE FUNCTION suppress_cosmetic_achievement()");
        try{CosmeticSend(fault,8,3,18,4,8,0,16,3);}finally{Query("DROP TRIGGER suppress_cosmetic_achievement ON revora_achievement_evidence; DROP FUNCTION suppress_cosmetic_achievement()");}
        Check(Fenced(fault) && fault.Entity.HairStyle==2 && Scalar("SELECT hairstyle FROM entities WHERE uid=@uid",fault.Entity.UID)==2 && CosmeticAchievements(fault)==0
            && Scalar("SELECT count(*) FROM revora_cosmetic_receipts WHERE uid=@uid",fault.Entity.UID)==1,"suppressed achievement aborts selection and production handler fences the session");
        var stale=CosmeticPlayer(map);Query("UPDATE entities SET money=60000 WHERE uid=@uid","uid",(long)stale.Entity.UID);CosmeticSend(stale,8,4,18,4,8,0,16,3);
        Check(Fenced(stale) && Scalar("SELECT money FROM entities WHERE uid=@uid",stale.Entity.UID)==60000 && Scalar("SELECT count(*) FROM revora_cosmetic_ownership WHERE uid=@uid",stale.Entity.UID)==0,"native stale-wallet failure preserves external money and fences the session");
        var unworn=CosmeticPlayer(map);Cosmetic(unworn,4,0,3);Cosmetic(unworn,7,3,6);Cosmetic(unworn,6,3,6);
        Check(CosmeticAchievements(unworn)==0 && unworn.Entity.HairStyle==2,"colouring an unworn stored style earns no worn-appearance achievement");
        var same=CosmeticPlayer(map);Cosmetic(same,4,0,2);Cosmetic(same,3,0,2);
        Check(CosmeticAchievements(same)==0,"unlocking and selecting the current style does not invent a hairstyle change");
        var normalized=CosmeticPlayer(map);normalized.Entity.HairStyle=6003;
        Check(normalized.Entity.HairColor==6,"shared entity hair-colour reader uses the native factor");
        normalized.Entity.HairColor=4;Check(normalized.Entity.HairStyle==4003,"shared hair-colour writer preserves the native style portion");
        var npc=CosmeticPlayer(map);
        map.Npcs[10002]=new NpcSpawn{UID=10002,MapID=map.ID,X=npc.Entity.X,Y=npc.Entity.Y};
        map.Npcs[3216]=new NpcSpawn{UID=3216,MapID=map.ID,X=npc.Entity.X,Y=npc.Entity.Y};
        npc.ActiveNpc=10002;Npcs.GetDialog(new NpcRequest(true){NpcID=10002,OptionID=40},npc);
        npc.ActiveNpc=3216;Npcs.GetDialog(new NpcRequest(true){NpcID=3216,OptionID=40},npc);
        Check(npc.Entity.HairStyle==2 && npc.Entity.Money==50000,"old forged barber and Mr Free options can no longer grant free styles");
        // This invokes the actual game-state loader. Public login additionally
        // delivers external website grants, which never run in isolated fixtures.
        foreach(string table in new[]{"teamarena","championarena","quests","status","reward","rewardclameid","merchant","dialyquests","activeness","house","pkexploits","achievement"})
            Query("CREATE TABLE IF NOT EXISTS "+table+" AS TABLE public."+table+" WITH NO DATA");
        Query("CREATE TABLE prizes(uid bigint PRIMARY KEY,name text,prizes bytea)");
        p.LastResetTime=DateTime.Now;if(!EntityTable.SaveEntity(p))throw new Exception("Reconnect fixture save failed");
        var reloaded=CosmeticPlayer(map);uint placeholder=reloaded.Entity.UID;
        reloaded.Account.EntityID=p.Entity.UID;reloaded.Account.Username="cosmetic_fixture";reloaded.Quests=new Quests(reloaded);
        Check((bool)Call(typeof(EntityTable),null,"LoadEntityState",reloaded),"production game-state loader reads the saved cosmetic character");
        Kernel.GamePool.Remove(placeholder);Kernel.GamePool[p.Entity.UID]=reloaded;reloaded.Action=2;
        Check(!Fenced(reloaded) && reloaded.Entity.Face==154 && reloaded.Entity.HairStyle==6003 && reloaded.Entity.Money==48000
            && CosmeticsSnapshot(reloaded).Length==2,"new session reload preserves saved appearance, purse and durable ownership");
        CosmeticSend(reloaded,8,4,18,4,8,0,16,3);CosmeticSend(reloaded,8,7,26,4,8,3,16,6);
        Check(!Fenced(reloaded) && reloaded.Entity.Money==48000 && CosmeticAchievements(reloaded)==3 && EntityTable.SaveEntity(reloaded),"new session replays cannot charge again or revert saved achievements");
        EvidenceRefresh(reloaded);
        Check(HasAchievement(reloaded,10113) && HasAchievement(reloaded,10114) && HasAchievement(reloaded,10116),"fresh-session achievement packet restores all three durable completions");

    }
    static void VerifyRetiredDyes(Map map) {
        Call(RetiredDyes,null,"Initialize");ConquerItem.ItemUID=new Counter(4990000);
        dyeNow=new DateTime(2026,9,15,18,0,0,DateTimeKind.Utc);RetiredDyes.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>dyeNow));
        for(uint id=1060030;id<=1060090;id+=10)for(int bound=0;bound<2;bound++) {
            var p=CosmeticPlayer(map);var item=Pack(p,id,1);
            if(bound==1){item.Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)item.UID);}
            ItemUsage.Process(PrayerPacket(item.UID),p);
            Check(!Fenced(p) && p.Entity.Money==50200 && !p.Inventory.ContainsUID(item.UID) && Scalar("SELECT money FROM entities WHERE uid=@uid",p.Entity.UID)==50200
                && p.Entity.HairStyle==2 && CosmeticAchievements(p)==0,"native retired dye redeems 200 silver without a cosmetic achievement: "+id+" bound="+bound);
            ItemHandler.UseItem(item,p);Check(p.Entity.Money==50200 && EntityTable.SaveEntity(p),"consumed dye replay cannot award silver twice");
        }
        var stacked=CosmeticPlayer(map);var stack=Pack(stacked,1060030,3);var other=Pack(stacked,1060030,1);
        Parallel.For(0,8,i=>ItemUsage.Process(PrayerPacket(stack.UID),stacked));
        Check(!Fenced(stacked) && stacked.Entity.Money==50200 && stack.StackSize==2 && SavedRune(stack,"stacksize")==2 && other.StackSize==1,"parallel dye uses consume one selected stack unit and leave another item untouched");
        dyeNow=dyeNow.AddSeconds(1);ItemHandler.UseItem(stack,stacked);
        Check(stacked.Entity.Money==50400 && stack.StackSize==1,"a later deliberate use consumes one further dye");
        var cap=CosmeticPlayer(map);cap.Entity.Money=(ulong)long.MaxValue-100;EntityTable.UpdateMoney(cap);var capped=Pack(cap,1060030,1);ItemHandler.UseItem(capped,cap);
        Check(!Fenced(cap) && cap.Inventory.ContainsUID(capped.UID) && cap.Entity.Money==(ulong)long.MaxValue-100,"full silver purse preserves the entire dye");
        var fail=CosmeticPlayer(map);var failedItem=Pack(fail,1060030,1);
        Query("CREATE FUNCTION suppress_dye_receipt() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER suppress_dye_receipt BEFORE INSERT ON revora_retired_dye_uses FOR EACH ROW EXECUTE FUNCTION suppress_dye_receipt()");
        try{ItemHandler.UseItem(failedItem,fail);}finally{Query("DROP TRIGGER suppress_dye_receipt ON revora_retired_dye_uses; DROP FUNCTION suppress_dye_receipt()");}
        Check(Fenced(fail) && fail.Entity.Money==50000 && fail.Inventory.ContainsUID(failedItem.UID) && SavedRune(failedItem,"entityid")==fail.Entity.UID
            && Scalar("SELECT money FROM entities WHERE uid=@uid",fail.Entity.UID)==50000,"failed redemption receipt rolls back item and silver then fences the session");
        var foreign=CosmeticPlayer(map);var original=CosmeticPlayer(map);var foreignItem=Pack(original,1060030,1);ItemHandler.UseItem(foreignItem,foreign);
        Check(foreign.Entity.Money==50000 && original.Inventory.ContainsUID(foreignItem.UID),"foreign inventory object cannot be redeemed");
        var moved=CosmeticPlayer(map);var movedItem=Pack(moved,1060030,1);Query("UPDATE items SET entityid=@owner WHERE uid=@uid","owner",(long)foreign.Entity.UID,"uid",(long)movedItem.UID);ItemHandler.UseItem(movedItem,moved);
        Check(Fenced(moved) && moved.Entity.Money==50000 && SavedRune(movedItem,"entityid")==foreign.Entity.UID,"changed durable item ownership rejects redemption without moving the item back");
        var trade=CosmeticPlayer(map);var tradeItem=Pack(trade,1060030,1);trade.Trade.InTrade=true;ItemHandler.UseItem(tradeItem,trade);
        Check(trade.Entity.Money==50000 && trade.Inventory.ContainsUID(tradeItem.UID),"trade prevents retired dye use");
    }
    static readonly Type Tulips=typeof(Entity).Assembly.GetType("Revora.Game.Features.RetiredBlackTulip");
    static GameState TulipPlayer(Map map,uint points=100) {
        var p=CosmeticPlayer(map);ChiTable.Load(p);SetJadeChi(p,points);return p;
    }
    static void VerifyRetiredTulips(Map map) {
        Query("CREATE TABLE chi (LIKE public.chi INCLUDING ALL)");
        Call(Tulips,null,"Initialize");Call(Tulips,null,"Initialize");
        Tulips.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>dyeNow));
        foreach(bool bound in new[]{false,true}) {
            var p=TulipPlayer(map);var item=Pack(p,723584,1);item.Bound=bound;
            Query("UPDATE items SET bound=@bound WHERE uid=@uid","bound",bound?1:0,"uid",(long)item.UID);
            ItemUsage.Process(PrayerPacket(item.UID),p);
            Check(!Fenced(p) && p.ChiPoints==110 && JadeChi(p)==110 && !p.Inventory.ContainsUID(item.UID)
                && Scalar("SELECT count(*) FROM revora_retired_tulip_uses WHERE uid=@uid",p.Entity.UID)==1,
                "ordinary native Black Tulip use grants ten saved Chi without requiring armor: bound="+bound);
            ItemHandler.UseItem(item,p);ChiTable.Save(p);ChiTable.Load(p);
            Check(p.ChiPoints==110 && p.Entity.Money==50000 && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10115",p.Entity.UID)==0,"save, reload and consumed tulip replay retain Chi without clothing evidence");
        }
        var stacked=TulipPlayer(map);var stack=Pack(stacked,723584,3);
        Parallel.For(0,12,i=>ItemUsage.Process(PrayerPacket(stack.UID),stacked));
        Check(!Fenced(stacked) && stacked.ChiPoints==110 && stack.StackSize==2 && SavedRune(stack,"stacksize")==2,"parallel native tulip use consumes one selected stack unit");
        dyeNow=dyeNow.AddSeconds(1);ItemHandler.UseItem(stack,stacked);
        Check(stacked.ChiPoints==120 && stack.StackSize==1 && JadeChi(stacked)==120,"later deliberate tulip use redeems another unit");
        foreach(uint before in new[]{0u,3999u,4000u,7999u,8000u,uint.MaxValue-10,uint.MaxValue-9,uint.MaxValue}) {
            var p=TulipPlayer(map,before);var item=Pack(p,723584,1);ItemHandler.UseItem(item,p);
            uint expected=before<=uint.MaxValue-10?before+10:before;
            Check(!Fenced(p) && p.ChiPoints==expected && JadeChi(p)==expected && p.Inventory.ContainsUID(item.UID)==(before>uint.MaxValue-10),"tulip credit bounds preserve the full advertised reward: "+before);
        }
        var pending=TulipPlayer(map);var pendingItem=Pack(pending,723584,1);pending.ChiPoints=101;ItemHandler.UseItem(pendingItem,pending);
        Check(!Fenced(pending) && pending.ChiPoints==101 && JadeChi(pending)==100 && pending.Inventory.ContainsUID(pendingItem.UID),"pending unsaved Chi preserves tulip for a later retry");
        ChiTable.Save(pending);ItemHandler.UseItem(pendingItem,pending);
        Check(!Fenced(pending) && pending.ChiPoints==111 && JadeChi(pending)==111,"saved pending Chi permits the full tulip credit");
        var stale=TulipPlayer(map);var staleItem=Pack(stale,723584,1);Query("UPDATE chi SET points=900 WHERE uid=@uid","uid",(long)stale.Entity.UID);ItemHandler.UseItem(staleItem,stale);
        Check(Fenced(stale) && stale.ChiPoints==100 && JadeChi(stale)==900 && stale.Inventory.ContainsUID(staleItem.UID) && SavedRune(staleItem,"entityid")==stale.Entity.UID,"stale durable Chi rolls back item debit and fences without overwriting points");
        var receipt=TulipPlayer(map);var receiptItem=Pack(receipt,723584,1);
        Query("CREATE FUNCTION suppress_tulip_receipt() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER suppress_tulip_receipt BEFORE INSERT ON revora_retired_tulip_uses FOR EACH ROW EXECUTE FUNCTION suppress_tulip_receipt()");
        try{ItemHandler.UseItem(receiptItem,receipt);}finally{Query("DROP TRIGGER suppress_tulip_receipt ON revora_retired_tulip_uses; DROP FUNCTION suppress_tulip_receipt()");}
        Check(Fenced(receipt) && receipt.ChiPoints==100 && JadeChi(receipt)==100 && receipt.Inventory.ContainsUID(receiptItem.UID) && SavedRune(receiptItem,"entityid")==receipt.Entity.UID,"suppressed tulip receipt rolls back item and Chi");
        var owner=TulipPlayer(map);var stranger=TulipPlayer(map);var foreign=Pack(owner,723584,1);ItemHandler.UseItem(foreign,stranger);
        Check(!Fenced(stranger) && stranger.ChiPoints==100 && owner.Inventory.ContainsUID(foreign.UID),"foreign tulip object cannot be redeemed");
        Query("UPDATE items SET entityid=@owner WHERE uid=@uid","owner",(long)stranger.Entity.UID,"uid",(long)foreign.UID);ItemHandler.UseItem(foreign,owner);
        Check(Fenced(owner) && JadeChi(owner)==100 && SavedRune(foreign,"entityid")==stranger.Entity.UID,"changed durable ownership cannot be redeemed or reclaimed");
        foreach(string mode in new[]{"trade","locked","suspicious","wardrobe","expired","warehouse","position","fake"}) {
            var p=TulipPlayer(map);var item=Pack(p,723584,1);
            if(mode=="trade")p.Trade.InTrade=true;
            if(mode=="locked")item.Lock=1;
            if(mode=="suspicious")item.Suspicious=true;
            if(mode=="wardrobe")item.InWardrobe=true;
            if(mode=="expired"){item.Days=1;item.DayStamp=dyeNow.ToLocalTime().AddDays(-2);}
            if(mode=="warehouse")item.Warehouse=8;
            if(mode=="position")item.Position=3;
            if(mode=="fake")p.Fake=true;
            ItemHandler.UseItem(item,p);
            Check(!Fenced(p) && p.ChiPoints==100 && p.Inventory.ContainsUID(item.UID),"tulip rejects ineligible state: "+mode);
        }
    }

}
