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
    static string EliteOriginalContents(GameState p) {
        return string.Join(";",p.Inventory.Objects.Where(i=>ConquerItemInformation.BaseInformations[i.ID].PurificationLevel!=7).GroupBy(i=>i.ID).OrderBy(g=>g.Key).Select(g=>g.Key+":"+g.Count()));
    }
    static long EliteSoulCount(GameState p) {
        long saved=Scalar("SELECT count(*) FROM revora_epk_prize_claims c JOIN items i ON i.uid=ANY(c.rewards) WHERE c.uid=@uid AND i.bound=1 AND i.id BETWEEN 800000 AND 899999",p.Entity.UID);
        return p.Inventory.Objects.Count(i=>i.Bound && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)==saved?saved:-1;
    }
    static long EliteClaims(GameState p){return Scalar("SELECT count(*) FROM revora_epk_prize_claims WHERE uid=@uid",p.Entity.UID);}
    static void EliteDesk(GameState p){
        p.ActiveNpc=448582;var npc=p.Map.Npcs[448582];
        for(int radius=1;radius<=3;radius++)for(int dx=-radius;dx<=radius;dx++)for(int dy=-radius;dy<=radius;dy++)
            if(p.Map.Floor[npc.X+dx,npc.Y+dy,MapObjectType.Player]){p.Entity.X=(ushort)(npc.X+dx);p.Entity.Y=(ushort)(npc.Y+dy);return;}
        throw new Exception("Elite prize desk has no legal standing tile.");
    }
    static void EliteWire(GameState p,byte option,uint npc=448582){
        var frame=new byte[28];Writer.WriteUInt16(20,0,frame);Writer.WriteUInt16(2032,2,frame);
        Writer.WriteUInt32(npc,8,frame);frame[14]=option;Writer.WriteUInt64(PacketHandler.ClientSeal,20,frame);PacketHandler.HandlePacket(frame,p);
    }
    static void EliteCollect(GameState p){EliteDesk(p);EliteWire(p,6);EliteWire(p,7);}
    // Fault fixtures start from a saved entitlement; earned journeys below create theirs through real duels.
    static void EliteFixtureQuery(string sql,params object[] args){
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand(sql,c)){
            for(int n=0;n<args.Length;n+=2){if(args[n+1] is DateTime)q.Parameters.Add((string)args[n],NpgsqlTypes.NpgsqlDbType.TimestampTz).Value=args[n+1];else q.Parameters.AddWithValue((string)args[n],args[n+1]);}q.ExecuteNonQuery();
        }}
    }
    static void EliteEntitlement(GameState p,int place=1){
        string run="claim-"+p.Entity.UID;int bracket=p.Entity.Level>=130?3:p.Entity.Level>=120?2:p.Entity.Level>=100?1:0;
        EliteFixtureQuery("INSERT INTO revora_epk_runs(run,cycle,bracket,starts_at,deadline,state,valuable_reward) VALUES(@run,@run,@b,@now,@end,'complete',true)",
            "run",run,"b",bracket,"now",epkNow.AddMinutes(-10),"end",epkNow.AddMinutes(30));
        EliteFixtureQuery("INSERT INTO revora_epk_entries(run,cycle,uid,level,power,seed) VALUES(@run,@run,@uid,@level,100,@seed)",
            "run",run,"uid",(long)p.Entity.UID,"level",(int)p.Entity.Level,"seed",Guid.NewGuid());
        EliteFixtureQuery("INSERT INTO revora_epk_placements(run,uid,bracket,signup_level,place,pack_type,earned_at,expires_at) VALUES(@run,@uid,@b,@level,@place,@type,@now,@end)",
            "run",run,"uid",(long)p.Entity.UID,"b",bracket,"level",(int)p.Entity.Level,"place",place,"type",720714+bracket*4+(place==1?3:place==2?2:place==3?1:0),"now",epkNow,"end",epkNow.AddDays(7));
        EliteDesk(p);
    }
    static void FinishElitePrizeCombat(object e){
        EpkTick(e,660);int guard=0;
        while(EpkGroups(e).Any(g=>!(bool)Field(g,"Done"))){
            if(++guard>16)throw new Exception("Prize tournament did not settle.");EpkTick(e,11);EpkTick(e,11);
            foreach(var m in EpkMatches(e).Where(m=>(bool)Field(m,"Imported") && Field(m,"Winner")==null).ToArray()){
                var a=EpkFighter(m,"Left");var b=EpkFighter(m,"Right");EpkDamage(a,b,100);b.Entity.Die(a.Entity);
            }EpkTick(e,1);
        }
    }
    static void VerifyElitePrizes(string runtime,Map town){
        VerifyFirstRebirth(runtime,town);Call(PrizePacks,null,"Initialize");
        Query("CREATE TABLE IF NOT EXISTS refinery AS TABLE public.refinery;CREATE TABLE IF NOT EXISTS refineryboxes AS TABLE public.refineryboxes");
        Refinery.LoadItems();Call(NestedPrizes,null,"Initialize");NestedPrizes.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>0));
        Query("CREATE TABLE IF NOT EXISTS npcs (LIKE public.npcs INCLUDING ALL);INSERT INTO npcs SELECT * FROM public.npcs WHERE id=448582 AND NOT EXISTS(SELECT 1 FROM npcs WHERE id=448582)");
        Call(Epk,null,"PlaceEnvoy");epkNow=new DateTime(2027,1,2,3,55,0,DateTimeKind.Utc);
        var e=NewEpk(runtime);Call(Epk,e,"Tick");
        string[] low={"723341:1;729626:1","720731:1;723341:2;729626:1","720731:2;723341:3;729626:1","720731:3;723341:5;729626:1;1088000:1"};
        string[] high={"720598:1;720731:1;723341:1;723700:3;729626:1","720598:1;720731:2;723342:1;723700:5;729626:2","720598:2;720730:1;720731:3;723342:1;729626:3;1088000:1","720049:1;720598:3;720730:3;720731:5;723342:3;729626:5;1088000:3"};
        var players=new System.Collections.Generic.List<GameState>();
        foreach(byte level in new byte[]{99,119,129,135})for(int n=0;n<8;n++){
            var p=BallPlayer(town);p.Entity.Level=level;p.Entity.Experience=0;EntityTable.SaveEntity(p);EliteDesk(p);EpkJoin(p);players.Add(p);
        }
        Query("UPDATE revora_epk_runs SET valuable_reward=false WHERE bracket=0"); // Old admitted bracket, before any duels.
        Check(players.All(p=>p.Entity.MapID!=1002),"all 32 ordinary entrants enroll through the Envoy across four brackets");
        FinishElitePrizeCombat(e);
        Check(EpkCount("SELECT count(*) FROM revora_epk_placements")==32,"all four real eight-player brackets earn eight verified placements");
        GameState champion=null;
        foreach(var original in players){
            var p=original;int place=(int)Scalar("SELECT place FROM revora_epk_placements WHERE uid=@uid",p.Entity.UID);
            byte level=p.Entity.Level;int rank=place==1?3:place==2?2:place==3?1:0;ulong curve=DataHolder.LevelExperience(level);
            ulong award=rank==3?curve:rank==2?curve/2:rank==1?curve/10*3:curve/100*15;
            uint cp=level>=130?(rank==0?3000000u:5000000u):0;
            EliteDesk(p);EliteWire(p,7);Check(EliteClaims(p)==0,"claim requires a current prize review");
            EliteWire(p,6);Parallel.For(0,12,n=>{if(n%3==0)EntityTable.SaveEntity(p);else EliteWire(p,7);});
            Check(!Fenced(p) && EliteClaims(p)==1 && p.Entity.ConquerPoints==cp && EliteOriginalContents(p)==(level>=130?high:low)[rank] && EliteSoulCount(p)==(level==99?0:1)
                && p.Entity.Level==(rank==3?level+1:level) && p.Entity.Experience==(rank==3?0:award)
                && Scalar("SELECT experience FROM revora_epk_prize_claims WHERE uid=@uid",p.Entity.UID)==(long)award,
                "earned place "+place+" at signup level "+level+" delivers exact bundle, CPs and EXP once under concurrent replay/autosave");
            Check(p.Inventory.Objects.All(i=>i.Bound==(ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7 || level>=130 && (i.ID==723700 || i.ID==720730))),"earned bundle preserves intentional item binding");
            var earnedIds=p.Inventory.Objects.Select(i=>i.UID).OrderBy(id=>id).ToArray();
            p=ReloadBall(p);EliteCollect(p);
            Check(p.Inventory.Objects.Select(i=>i.UID).OrderBy(id=>id).SequenceEqual(earnedIds),"earned prize preserves every physical item UID through fresh character reload and replay");
            Check(!Fenced(p) && EliteClaims(p)==1 && p.Entity.ConquerPoints==cp && EliteOriginalContents(p)==(level>=130?high:low)[rank] && EliteSoulCount(p)==(level==99?0:1),"saved claim survives production reload and another NPC review");
            if(level==135 && place==1)champion=p;
        }
        NewEpk(runtime);EliteCollect(champion);Check(EliteClaims(champion)==1,"new tournament lifecycle cannot reopen a completed prize");
        foreach(var item in champion.Inventory.Objects.Where(i=>i.ID==720049 || i.ID==720731 || i.ID==729626).ToArray())ItemHandler.UseItem(item,champion);
        champion=ReloadBall(champion);
        Check(!Fenced(champion) && NestedReceipts(champion)==11 && champion.Inventory.Count==24 && champion.Entity.ConquerPoints==5000000
            && champion.Inventory.Objects.Count(i=>i.ID==724409)==5 && champion.Inventory.Objects.Single(i=>i.ID==300000).Plus==6,
            "earned champion claims and opens all eleven nested gifts with persistent horse, accessories and usable Super refiners");
        var weapon=Pack(champion,410009,1);var material=champion.Inventory.Objects.First(i=>i.ID==724409);Refine(champion,weapon,material);champion=ReloadBall(champion);
        Check(!Fenced(champion) && !champion.Inventory.ContainsUID(material.UID) && champion.Inventory.Objects.Single(i=>i.UID==weapon.UID).RefineStats.Level==5,
            "ordinary battle-to-NPC-to-nested-pack-to-weapon-refinery journey survives reload");
        VerifyElitePrizeFailures(town);
        epkNow=new DateTime(2027,1,9,3,55,0,DateTimeKind.Utc);e=NewEpk(runtime);Call(Epk,e,"Tick");
        var newLow=new System.Collections.Generic.List<GameState>();
        for(int n=0;n<8;n++){
            var p=BallPlayer(town);p.Entity.Level=99;p.Entity.Experience=0;
            if(!EntityTable.SaveEntity(p))throw new Exception("Low-bracket profile save failed.");
            EliteDesk(p);EpkJoin(p);newLow.Add(p);
        }
        Check(newLow.All(p=>p.Account.State==AccountTable.AccountState.Player && p.Entity.MapID!=1002),
            "eight ordinary entrants join a new lowest-level bracket without editing its reward contract");
        FinishElitePrizeCombat(e);
        foreach(var original in newLow){
            var p=original;int place=(int)Scalar("SELECT place FROM revora_epk_placements WHERE uid=@uid",p.Entity.UID);
            int rank=place==1?3:place==2?2:place==3?1:0;ulong curve=DataHolder.LevelExperience(99);
            ulong award=rank==3?curve:rank==2?curve/2:rank==1?curve/10*3:curve/100*15;
            EliteCollect(p);
            Check(!Fenced(p) && EliteClaims(p)==1 && EliteSoulCount(p)==1 && EliteOriginalContents(p)==low[rank]
                && p.Entity.ConquerPoints==0 && p.Entity.Level==(rank==3?100:99) && p.Entity.Experience==(rank==3?0:award),
                "new lowest-bracket place "+place+" earns its bound P7 soul, original bundle and exact EXP");
            var itemIds=p.Inventory.Objects.Select(i=>i.UID).OrderBy(id=>id).ToArray();p=ReloadBall(p);EliteCollect(p);
            Check(!Fenced(p) && EliteClaims(p)==1 && EliteSoulCount(p)==1 && p.Inventory.Objects.Select(i=>i.UID).OrderBy(id=>id).SequenceEqual(itemIds),
                "new lowest-bracket prize survives fresh character reload and claim replay at place "+place);
        }

    }
    static void VerifyElitePrizeFailures(Map town){
        Query("CREATE TABLE IF NOT EXISTS apprentice AS TABLE public.apprentice WITH NO DATA");
        foreach(string fault in new[]{"profile","cp","late-item","soul","receipt","skills","recovery","mentor"}){
            var p=fault=="recovery"?PendingFirstLife(town):BallPlayer(town);EliteEntitlement(p);
            if(fault=="mentor")MentorLink(BallPlayer(town),p,100);
            byte level=p.Entity.Level;ulong xp=p.Entity.Experience;int count=p.Inventory.Count;
            string table=fault=="profile" || fault=="cp"?"entities":(fault=="late-item" || fault=="soul")?"items":fault=="receipt"?"revora_epk_prize_claims":fault=="skills"?"skills":fault=="recovery"?"revora_first_rebirths":"apprentice";
            string verb=fault=="soul" || fault=="late-item" || fault=="receipt" || fault=="skills"?"INSERT":"UPDATE";
            string body=fault=="late-item"?"IF NEW.id=729626 THEN RETURN NULL; END IF; RETURN NEW;":fault=="cp"?"IF NEW.conquerpoints<>OLD.conquerpoints THEN RETURN NULL; END IF; RETURN NEW;":"RETURN NULL;";
            if(fault=="soul")body="IF NEW.id BETWEEN 800000 AND 899999 THEN RETURN NULL; END IF; RETURN NEW;";
            Query("CREATE FUNCTION suppress_elite_claim() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN "+body+" END';CREATE TRIGGER suppress_elite_claim BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_elite_claim()");
            EliteCollect(p);
            Check(Fenced(p) && EliteClaims(p)==0 && p.Entity.Level==level && p.Entity.Experience==xp && p.Entity.ConquerPoints==0 && p.Inventory.Count==count
                && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0",p.Entity.UID)==count
                && Scalar("SELECT level FROM entities WHERE uid=@uid",p.Entity.UID)==level && (fault!="recovery" || LifePending(p)==1)
                && (fault!="mentor" || MentorBalance(p)==100),"suppressed "+fault+" rolls back complete claim including EXP, CPs, items and side effects");
            Query("DROP TRIGGER suppress_elite_claim ON "+table+";DROP FUNCTION suppress_elite_claim()");p=ReloadBall(p);EliteCollect(p);
            Check(!Fenced(p) && EliteClaims(p)==1 && (fault!="recovery" || LifePending(p)==0),"saved entitlement retries after reload following "+fault+" failure");
        }
        foreach(string condition in new[]{"curse","space","overflow","level-review","level-changed","cap","expired","not-earned","offer-time","remote","fake","trade","dead","echo","stale-cp","stale-profile","stale-bag","replaced"}){
            var p=BallPlayer(town);if(condition=="cap")p.Entity.Level=Config.LevelCap;EntityTable.SaveEntity(p);EliteEntitlement(p);EliteWire(p,6);
            ulong before=p.Entity.Experience;DateTime saved=epkNow;
            if(condition=="curse")p.Entity.AddFlag((ulong)FlagsUpdate.Flags.Cursed,60,false);
            if(condition=="space")while(p.Inventory.Count<17)Pack(p,1000000,1);
            if(condition=="overflow")p.Entity.ConquerPoints=uint.MaxValue-1;
            if(condition=="level-review" || condition=="level-changed")p.Entity.Level++;
            if(condition=="level-changed")EliteWire(p,6);
            if(condition=="expired")epkNow=epkNow.AddDays(7);
            if(condition=="not-earned")epkNow=epkNow.AddSeconds(-1);
            if(condition=="offer-time")epkNow=epkNow.AddMinutes(2);
            if(condition=="remote")p.Entity.X=10;
            if(condition=="fake")p.Fake=true;
            if(condition=="trade")p.Trade.InTrade=true;
            if(condition=="dead")p.Entity.Hitpoints=0;
            if(condition=="stale-cp")Query("UPDATE entities SET conquerpoints=1 WHERE uid=@uid","uid",(long)p.Entity.UID);
            if(condition=="stale-profile")Query("UPDATE entities SET experience=experience+1 WHERE uid=@uid","uid",(long)p.Entity.UID);
            if(condition=="stale-bag"){var i=Pack(p,1000000,1);Query("DELETE FROM items WHERE uid=@uid","uid",(long)i.UID);}
            if(condition=="replaced")ReloadBall(p);
            EliteWire(p,7,condition=="echo"?123u:448582u);epkNow=saved;
            bool receives=condition=="level-changed" || condition=="cap";
            Check(EliteClaims(p)==(receives?1:0) && p.Entity.Experience==before && (receives?p.Entity.ConquerPoints==5000000:true),
                condition+" applies correct claim eligibility without partial XP");
            if(condition.StartsWith("stale-"))Check(Fenced(p),"stale durable "+condition+" fences the session after rollback");
            if(condition=="space"){
                DisRemove(p,p.Inventory.Objects.First());EliteCollect(p);Check(!Fenced(p) && EliteClaims(p)==1 && p.Inventory.Count==40,"exactly 24 free slots admits the full champion bundle");
            }
            if(condition=="level-review"){EliteCollect(p);Check(EliteClaims(p)==1 && Scalar("SELECT experience FROM revora_epk_prize_claims WHERE uid=@uid",p.Entity.UID)==0,"fresh review permits changed-level prize without EXP");}
        }
        foreach(string boundary in new[]{"deadline","future","cancelled","running","changed-placement"}){
            var p=BallPlayer(town);EliteEntitlement(p);DateTime saved=epkNow;
            if(boundary=="deadline")EliteFixtureQuery("UPDATE revora_epk_placements SET expires_at=@end WHERE uid=@uid","end",epkNow.AddSeconds(30),"uid",(long)p.Entity.UID);
            if(boundary=="future")EliteFixtureQuery("UPDATE revora_epk_placements SET earned_at=@end WHERE uid=@uid","end",epkNow.AddSeconds(1),"uid",(long)p.Entity.UID);
            EliteWire(p,6);
            if(boundary=="deadline")epkNow=epkNow.AddSeconds(30);
            if(boundary=="cancelled" || boundary=="running")Query("UPDATE revora_epk_runs SET state=@state WHERE run=@run","state",boundary,"run","claim-"+p.Entity.UID);
            if(boundary=="changed-placement")Query("UPDATE revora_epk_placements SET place=2,pack_type=720728 WHERE uid=@uid","uid",(long)p.Entity.UID);
            EliteWire(p,7);epkNow=saved;
            Check(!Fenced(p) && EliteClaims(p)==0 && p.Inventory.Count==0 && p.Entity.ConquerPoints==0,"authoritative placement recheck prevents "+boundary+" claim");
        }
        var arena=BallPlayer(town);arena.Entity.Level=69;arena.Entity.Experience=0;EntityTable.SaveEntity(arena);EliteEntitlement(arena);
        Query("DELETE FROM arena WHERE entityid=@uid","uid",(long)arena.Entity.UID);arena.ArenaStatistic=null;
        Query("CREATE FUNCTION suppress_elite_arena() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_elite_arena BEFORE INSERT ON arena FOR EACH ROW EXECUTE FUNCTION suppress_elite_arena()");
        EliteCollect(arena);Check(Fenced(arena) && EliteClaims(arena)==0 && arena.Entity.Level==69 && arena.Inventory.Count==0,"arena admission failure rolls back a level-70 prize and all items");
        Query("DROP TRIGGER suppress_elite_arena ON arena;DROP FUNCTION suppress_elite_arena()");arena=ReloadBall(arena);EliteCollect(arena);
        Check(!Fenced(arena) && EliteClaims(arena)==1 && arena.Entity.Level==70,"arena admission and full prize retry together after reload");
        var wire=BallPlayer(town);EliteEntitlement(wire);EliteWire(wire,6);
        foreach(string bad in new[]{"short","long","trailer","input"}){
            int length=bad=="short"?18:bad=="long"?121:20;var frame=new byte[length+(bad=="trailer"?9:8)];
            Writer.WriteUInt16((ushort)length,0,frame);Writer.WriteUInt16(2032,2,frame);frame[14]=7;if(bad=="input")frame[17]=20;
            Writer.WriteUInt64(PacketHandler.ClientSeal,length,frame);PacketHandler.HandlePacket(frame,wire);
            Check(!Fenced(wire) && EliteClaims(wire)==0 && wire.Inventory.Count==0,"malformed Elite NPC continuation rejected: "+bad);
        }
        EliteWire(wire,7,0);Check(!Fenced(wire) && EliteClaims(wire)==1,"valid zero-echo NPC continuation follows malformed requests safely");
        var legacy=BallPlayer(town);EliteDesk(legacy);EliteCollect(legacy);Check(EliteClaims(legacy)==0,"no historical placement is inferred from a character or honor");
    }
}
