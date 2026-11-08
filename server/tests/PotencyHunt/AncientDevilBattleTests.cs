using System;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type AncientBattles=typeof(Entity).Assembly.GetType("Revora.Game.Features.AncientDevilBattles");
    static object BattleWrite(GameState p,string method,params object[] args) {
        lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
            var result=Call(AncientBattles,null,method,new object[]{c,t,p}.Concat(args).ToArray());t.Commit();return result;
        }}
    }
    static void BattleClaim(GameState p,Guid id) {
        lock(p.ItemSyncRoot){var items=(ConquerItem[])BattleWrite(p,"Claim",id);
            if(items!=null)Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.QuestInventory"),null,"Install",p,new uint[0],items);}
    }
    static void RemoveFixtureItem(GameState p,uint id) {p.Inventory.Remove(id,Enums.ItemUse.None,true);Query("DELETE FROM items WHERE uid=@uid","uid",(long)id);}
    static void EarnNextOffering(GameState collector) {
        ancientClassPlayers[0]=collector;
        for(int i=0;i<9;i++) {
            var p=ancientClassPlayers[i];uint guard=i<5?300011u+(uint)i:300011u;
            AdventureNear(p,guard);AncientDialog(p,guard,0);AncientDialog(p,guard,1);
            var body=AncientBody(CurrentAncient(p));AncientHit(p.Entity,body,body.Hitpoints);Call(AncientQuest,null,"Tick");
            AdventureNear(p,guard);AncientDialog(p,guard,0);AncientDialog(p,guard,2);AncientDialog(p,guard,3);AdventureWalk(p,216,208);
            Check(!Fenced(p) && AncientState(p,"exchanged")>=2,"next-day offering earns another actual class amulet "+i);
            if(i==0)continue;
            var item=p.Inventory.Objects.Single();p.Entity.TradeOn=collector.Entity.TradeOn=true;
            TradeSend(p,Trade.Request,collector.Entity.UID);TradeSend(collector,Trade.Request,p.Entity.UID);TradeSend(p,Trade.AddItem,item.UID);
            TradeSend(p,Trade.Accept,0);TradeSend(collector,Trade.Accept,0);
            Check(!Fenced(p) && !Fenced(collector) && collector.Inventory.ContainsUID(item.UID),"new class amulet reaches the collector through ordinary trade "+i);
        }
    }
    static void VerifyAncientBattles(string runtime,Map town) {
        VerifyAncientSummons(runtime,town,true);var p=ancientCollector;Guid id=ancientPaidRun;
        uint wardrobe=StoredQuestGarment(p);
        Call(AncientBattles,null,"Initialize");Call(AncientBattles,null,"Initialize");
        var terrain=Kernel.Maps[1082];var arena=AncientMap(11940,terrain);var resumedArena=AncientMap(11941,terrain);
        uint[] actors={500001,500002,500003,500004,500005};uint[] next={501001,501002,501003,501004,501005};
        Check(AbyssRejected(()=>BattleWrite(p,"Bind",id,(ushort)11940,new uint[]{1,2,3,4,4})),"duplicate battle actor identities cannot bind");
        var battle=BattleWrite(p,"Bind",id,(ushort)11940,actors);Guid generation=(Guid)AbyssField(battle,"Generation");
        Check(!(bool)BattleWrite(p,"Defeat",id,generation,0,actors[0]),"public-island callback cannot credit private battle");
        AncientAt(p,arena);
        Check(!(bool)BattleWrite(p,"Defeat",id,generation,4,actors[4]),"boss cannot complete while its four guards stand");
        Check(!(bool)BattleWrite(p,"Defeat",id,Guid.NewGuid(),0,actors[0]),"foreign battle generation cannot credit a guard");
        Check(!(bool)BattleWrite(p,"Defeat",id,generation,0,actors[1]),"wrong monster cannot credit a guard slot");
        Check(!(bool)BattleWrite(p,"Defeat",id,generation,-1,actors[0]) && !(bool)BattleWrite(p,"Defeat",id,generation,5,actors[0]),"out-of-range guard slots are rejected");
        Check((bool)BattleWrite(p,"Defeat",id,generation,0,actors[0]) && !(bool)BattleWrite(p,"Defeat",id,generation,0,actors[0]),"guard death commits once");
        Check((bool)BattleWrite(p,"Defeat",id,generation,2,actors[2]),"guard order is not artificially fixed");
        DateTime originalDeadline=(DateTime)AbyssField(battle,"Until");
        Check(AbyssRejected(()=>Call(AncientBattles,null,"RecoverStartup")),"startup recovery refuses an admitted summoner");
        Kernel.GamePool.Remove(p.Entity.UID);
        Query("CREATE FUNCTION suppress_battle_return() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_battle_return BEFORE UPDATE ON entities FOR EACH ROW EXECUTE FUNCTION suppress_battle_return()");
        try{Check(AbyssRejected(()=>Call(AncientBattles,null,"RecoverStartup")),"failed startup return aborts recovery");}
        finally{Query("DROP TRIGGER suppress_battle_return ON entities;DROP FUNCTION suppress_battle_return()");}
        Check(Scalar("SELECT mapid FROM entities WHERE uid=@uid",p.Entity.UID)==11940,"failed startup return preserves original saved location");
        Call(AncientBattles,null,"RecoverStartup");
        p=ReloadBall(p);SealAt(p);var recovered=ReserveAncient(p,null);
        Check(AbyssId(recovered)==id && !(bool)AbyssField(recovered,"Created") && (DateTime)AbyssField(recovered,"Until")==originalDeadline,
            "startup recovery preserves paid identity and deadline without another offering");
        AncientAt(p,arena);Check(!(bool)BattleWrite(p,"Defeat",id,generation,1,actors[1]),"startup recovery invalidates old actor generation before resume");
        SealAt(p);var resumed=BattleWrite(p,"Bind",id,(ushort)11941,next);Guid fresh=(Guid)AbyssField(resumed,"Generation");
        Check(fresh!=generation && (int)AbyssField(resumed,"Defeated")==5,"resumed battle rotates actor generation and retains defeated guards");
        AncientAt(p,resumedArena);
        Check(!(bool)BattleWrite(p,"Defeat",id,generation,1,actors[1]),"stale pre-resume death cannot credit new actors");
        Check(!(bool)BattleWrite(p,"Defeat",id,fresh,0,next[0]),"already-defeated guard cannot reward again after resume");
        Check((bool)BattleWrite(p,"Defeat",id,fresh,1,next[1]) && (bool)BattleWrite(p,"Defeat",id,fresh,3,next[3]),"remaining guards complete the protective seal");
        AncientBattles.GetField("Roll",Hidden).SetValue(null,new Func<int,int>(max=>100));
        Check(AbyssRejected(()=>BattleWrite(p,"Defeat",id,fresh,4,next[4])),"out-of-range reward draw rolls back boss completion");
        int draws=0;AncientBattles.GetField("Roll",Hidden).SetValue(null,new Func<int,int>(max=>{draws++;return 19;}));
        Query("CREATE FUNCTION suppress_ancient_reward() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_ancient_reward BEFORE INSERT ON revora_ancient_victories FOR EACH ROW EXECUTE FUNCTION suppress_ancient_reward()");
        try{Check(AbyssRejected(()=>BattleWrite(p,"Defeat",id,fresh,4,next[4])),"suppressed victory reward aborts boss completion");}
        finally{Query("DROP TRIGGER suppress_ancient_reward ON revora_ancient_victories;DROP FUNCTION suppress_ancient_reward()");}
        Check(Scalar("SELECT b.defeated FROM revora_ancient_battles b JOIN revora_ancient_summons s ON s.id=b.summon WHERE s.uid=@uid",p.Entity.UID)==15
            && Scalar("SELECT count(*) FROM revora_ancient_battle_kills k JOIN revora_ancient_summons s ON s.id=k.summon WHERE s.uid=@uid",p.Entity.UID)==4,
            "failed final reward preserves four guards and removes partial boss kill");
        int before=draws;Check((bool)BattleWrite(p,"Defeat",id,fresh,4,next[4]),"boss victory saves its Dragon Ball outcome at draw 19");
        Check(!(bool)BattleWrite(p,"Defeat",id,fresh,4,next[4]) && draws==before+1,"duplicate boss callback never redraws the saved reward");
        SealAt(p);p=ReloadBall(p);SealAt(p);ancientNow=ancientNow.AddHours(1);
        Check(ReserveAncient(p,null)==null,"outstanding victory blocks another paid summon after deadline");
        var foreign=HousePlayer(town);SealAt(foreign);Check(BattleWrite(foreign,"Claim",id)==null,"foreign player cannot claim saved victory");
        var fill=new System.Collections.Generic.List<uint>();while(p.Inventory.Count<40)fill.Add(Pack(p,1088001,1).UID);
        Check(BattleWrite(p,"Claim",id)==null,"full inventory retains saved Dragon Ball reward");
        foreach(uint uid in fill)RemoveFixtureItem(p,uid);
        Query("CREATE FUNCTION suppress_ancient_claim() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_ancient_claim BEFORE UPDATE ON revora_ancient_victories FOR EACH ROW EXECUTE FUNCTION suppress_ancient_claim()");
        try{Check(AbyssRejected(()=>BattleClaim(p,id)),"suppressed final claim receipt aborts inserted reward");}
        finally{Query("DROP TRIGGER suppress_ancient_claim ON revora_ancient_victories;DROP FUNCTION suppress_ancient_claim()");}
        Check(Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND (id=1088000 OR id BETWEEN 800000 AND 899999)",p.Entity.UID)==0,"failed claim leaves no partial Dragon Ball");
        Parallel.For(0,8,n=>BattleClaim(p,id));
        Check(p.Inventory.Objects.Count(i=>i.ID==1088000)==1 && draws==before+1,"concurrent claims grant one Dragon Ball without reroll");
        p=ReloadBall(p);SealAt(p);Check(p.Inventory.Objects.Count(i=>i.ID==1088000)==1 && BattleWrite(p,"Claim",id)==null,"claimed reward survives production reload and replay");
        var soul=p.Inventory.Objects.Single(i=>i.ID>=800000 && i.ID<900000);
        Check(soul.Bound && ConquerItemInformation.BaseInformations[soul.ID].PurificationLevel==7
            && Scalar("SELECT v.soul_uid FROM revora_ancient_victories v JOIN revora_ancient_summons s ON s.id=v.summon WHERE s.uid=@uid",p.Entity.UID)==soul.UID,"new Ancient victory preserves one bound P7 soul and its receipt after reload");
        RemoveFixtureItem(p,soul.UID);
        ancientNow=ancientNow.AddDays(1);EarnNextOffering(p);
        uint[] amuletTypes={710011,710012,710013,710014,710015,710635,711336,711600,3004947};
        uint[] offering=p.Inventory.Objects.Where(i=>amuletTypes.Contains(i.ID)).Select(i=>i.UID).ToArray();SealAt(p);
        var paid=ReserveAncient(p,offering);Guid second=AbyssId(paid);Query("UPDATE revora_ancient_summons SET valuable_reward=false WHERE id=@id","id",second);var bound=BattleWrite(p,"Bind",second,(ushort)11940,actors);Guid secondGeneration=(Guid)AbyssField(bound,"Generation");
        AncientAt(p,arena);for(int slot=0;slot<4;slot++)Check((bool)BattleWrite(p,"Defeat",second,secondGeneration,slot,actors[slot]),"next paid encounter has its own guard progress "+slot);
        AncientBattles.GetField("Roll",Hidden).SetValue(null,new Func<int,int>(max=>20));
        Check((bool)BattleWrite(p,"Defeat",second,secondGeneration,4,actors[4]),"draw 20 selects six Meteors at the reward boundary");
        Kernel.GamePool.Remove(p.Entity.UID);ancientNow=ancientNow.AddHours(1);Call(AncientBattles,null,"RecoverStartup");
        Check(Scalar("SELECT mapid FROM entities WHERE uid=@uid",p.Entity.UID)==1052,"startup returns victorious summoner from lost private map");
        p=ReloadBall(p);SealAt(p);
        fill.Clear();while(p.Inventory.Count<35)fill.Add(Pack(p,1088001,1).UID);
        Check(BattleWrite(p,"Claim",second)==null,"six-Meteor reward requires six free inventory slots");
        RemoveFixtureItem(p,fill[0]);fill.RemoveAt(0);
        Query("CREATE FUNCTION suppress_last_meteor() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF NEW.id=1088001 AND (SELECT count(*) FROM items WHERE entityid=NEW.entityid AND id=1088001)>=37 THEN RETURN NULL; END IF; RETURN NEW; END';CREATE TRIGGER suppress_last_meteor BEFORE INSERT ON items FOR EACH ROW EXECUTE FUNCTION suppress_last_meteor()");
        try{Check(AbyssRejected(()=>BattleClaim(p,second)),"failed sixth Meteor aborts the whole reward delivery");}
        finally{Query("DROP TRIGGER suppress_last_meteor ON items;DROP FUNCTION suppress_last_meteor()");}
        Check(p.Inventory.Count==34 && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0",p.Entity.UID)==34,"failed sixth Meteor restores all five earlier inserts");
        BattleClaim(p,second);Check(p.Inventory.Count==40 && BattleWrite(p,"Claim",second)==null,"six-Meteor claim fills exactly six slots once");
        p=ReloadBall(p);Check(p.Inventory.Count==40 && p.Inventory.Objects.Count(i=>i.ID==1088001)==38,"all six saved Meteor UIDs survive production reload");
        Check(Scalar("SELECT count(*) FROM items WHERE uid=@uid AND position=0 AND warehouse=0 AND inwardrobe=1",wardrobe)==1,"Ancient battle rewards, recovery and production reload preserve stored wardrobe design");
        foreach(uint uid in fill)RemoveFixtureItem(p,uid);
        ancientNow=ancientNow.AddDays(1);EarnNextOffering(p);SealAt(p);
        offering=p.Inventory.Objects.Where(i=>amuletTypes.Contains(i.ID)).Select(i=>i.UID).ToArray();
        var expiring=ReserveAncient(p,offering);Guid third=AbyssId(expiring);
        BattleWrite(p,"Bind",third,(ushort)11940,actors);AncientAt(p,arena);
        ancientNow=(DateTime)AbyssField(expiring,"Until");Kernel.GamePool.Remove(p.Entity.UID);
        Call(AncientBattles,null,"RecoverStartup");Call(AncientBattles,null,"RecoverStartup");
        Check(Scalar("SELECT count(*) FROM revora_ancient_summons WHERE uid=@uid AND state='expired'",p.Entity.UID)==1
            && Scalar("SELECT mapid FROM entities WHERE uid=@uid",p.Entity.UID)==1052,"exact-deadline startup expires and returns paid encounter idempotently");
        p=ReloadBall(p);SealAt(p);Check(ReserveAncient(p,null)==null && BattleWrite(p,"Claim",third)==null,
            "expired paid encounter cannot reopen that day or claim a victory");
    }
}
