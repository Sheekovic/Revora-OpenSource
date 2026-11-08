using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type Planet=typeof(Entity).Assembly.GetType("Revora.Game.Features.PlanetWeekend");
    static DateTime planetNow;
    static GameState PlanetPlayer(Map map) {
        var p=ProgressPlayer(map,"");p.Entity.Level=140;
        p.Entity.Titles=new System.Collections.Concurrent.ConcurrentDictionary<TitlePacket.Titles,DateTime>();return p;
    }
    static long PlanetKills(GameState p) { return Scalar("SELECT coalesce(sum(kills),0) FROM revora_planet_runs WHERE uid=@uid",p.Entity.UID); }
    static void PlanetTown(GameState p,Map town) {
        p.BindWorldMap(town.ID);p.Entity.X=319;p.Entity.Y=299;p.ActiveNpc=900000016;
    }
    static void PlanetKill(GameState p,Map birds) {
        var mob=DemonMob(birds,18);mob.Die(p.Entity);birds.Entities.Remove(mob.UID);
    }
    static void VerifyPlanet(string runtime,Map town)
    {
        Query("ALTER TABLE entities ADD COLUMN my_title integer DEFAULT 0, ADD COLUMN level integer DEFAULT 140, ADD COLUMN mapid integer DEFAULT 1002, ADD COLUMN x integer DEFAULT 319, ADD COLUMN y integer DEFAULT 299, ADD COLUMN previousmapid integer DEFAULT 1002, ADD COLUMN previousx integer DEFAULT 319, ADD COLUMN previousy integer DEFAULT 299");
        Query("CREATE TABLE titles (LIKE public.titles INCLUDING ALL)");
        Query("CREATE TABLE itemadding (LIKE public.itemadding INCLUDING ALL)");
        Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.Tournaments.LocalTeamPkLedger",true),null,"Initialize");
        StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
        ConquerItem.ItemUID=new Counter(4930000);
        var birds=World(runtime,1015);
        town.Npcs[900000016]=new NpcSpawn{UID=900000016,MapID=1002,X=319,Y=299,Mesh=50050,Type=Enums.NpcType.Talker};
        planetNow=new DateTime(2030,3,9,9,0,0,DateTimeKind.Utc);
        Planet.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>planetNow));
        Summer.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>planetNow));
        Demon.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>planetNow));
        Call(Demon,null,"Initialize");Call(Planet,null,"Initialize");
        var p=PlanetPlayer(town);PlanetTown(p,town);
        Check(p.Account.State==AccountTable.AccountState.Player,"Planet uses an ordinary non-GM character");
        p.ActiveNpc=0;
        Check(Call(Planet,null,"Accept",p)==null,"Planet admission requires Rowan interaction");
        p.ActiveNpc=900000016;p.Entity.X=330;
        Check(Call(Planet,null,"Accept",p)==null,"Planet remote admission is rejected");
        PlanetTown(p,town);p.Fake=true;
        Check(Call(Planet,null,"Accept",p)==null,"synthetic players cannot enter Planet");p.Fake=false;
        planetNow=planetNow.AddDays(-1);
        Check(Call(Planet,null,"Accept",p)==null,"weekday admission creates no Planet run");planetNow=planetNow.AddDays(1);
        Query("CREATE FUNCTION reject_planet_admission() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER reject_planet_admission BEFORE INSERT ON revora_planet_runs FOR EACH ROW EXECUTE FUNCTION reject_planet_admission()");
        bool failed=false;try{Call(Planet,null,"Accept",p);}catch(InvalidOperationException){failed=true;}
        finally {Query("DROP TRIGGER reject_planet_admission ON revora_planet_runs; DROP FUNCTION reject_planet_admission()");}
        Check(failed && Scalar("SELECT count(*) FROM revora_planet_runs WHERE uid=@uid",p.Entity.UID)==0,"suppressed admission fails without a phantom run");
        var admissions=new object[8];Parallel.For(0,8,i=>admissions[i]=Call(Planet,null,"Accept",p));
        Check(admissions.All(r=>r!=null) && Scalar("SELECT count(*) FROM revora_planet_runs WHERE uid=@uid",p.Entity.UID)==1,
            "concurrent admissions produce one durable character/weekend run");
        Check(Scalar("SELECT count(*) FROM revora_planet_runs WHERE uid=@uid AND weekend='20300309' AND stage='hunt' AND kills=0 AND ends_at-starts_at=interval '47 hours'",p.Entity.UID)==1,
            "Planet admission freezes the DST-aware weekend boundaries");
        DemonMove(p,birds);
        Call(Planet,null,"Credit",p.Entity,(ushort)1015,p.Entity.X,p.Entity.Y,18u,planetNow.AddTicks(-1));
        Call(Planet,null,"Credit",p.Entity,(ushort)1015,p.Entity.X,p.Entity.Y,18u,planetNow.AddTicks(1));
        Check(PlanetKills(p)==0,"Planet excludes pre-admission and future death timestamps");
        var mob=DemonMob(birds,18);
        try {Parallel.For(0,12,i=>mob.Die(p.Entity));}
        catch (AggregateException ex) {System.Console.WriteLine(ex.Flatten().InnerExceptions[0].ToString());throw;}
        Check(PlanetKills(p)==1,"one actual Birdman life counts once despite concurrent death callbacks");birds.Entities.Remove(mob.UID);
        var stranger=PlanetPlayer(birds);DemonMove(stranger,birds);PlanetKill(stranger,birds);
        Check(PlanetKills(stranger)==0 && PlanetKills(p)==1,"unadmitted killer neither earns progress nor credits another player");
        var wrong=DemonMob(birds,20);wrong.Die(p.Entity);birds.Entities.Remove(wrong.UID);
        Check(PlanetKills(p)==1,"non-Birdman death cannot progress Planet hunt");
        p.Entity.X=1;p.Entity.Y=1;PlanetKill(p,birds);DemonMove(p,birds);
        Check(PlanetKills(p)==1,"remote kills do not progress Planet hunt");
        p.Fake=true;PlanetKill(p,birds);p.Fake=false;
        Check(PlanetKills(p)==1,"synthetic killer cannot earn Planet progress");
        Call(Planet,null,"Initialize");PlanetKill(p,birds);
        Check(PlanetKills(p)==1,"clearing process caches preserves durable progress and requires session recovery");
        Call(Planet,null,"OnLogin",p);PlanetKill(p,birds);
        Check(PlanetKills(p)==2,"login recovery resumes the saved hunt through actual monster death");
        var replacement=PlanetPlayer(birds);GameState ignored;
        Kernel.GamePool.TryRemove(replacement.Entity.UID,out ignored);
        replacement.Entity.UID=p.Entity.UID;Kernel.GamePool[p.Entity.UID]=replacement;DemonMove(replacement,birds);
        PlanetKill(p,birds);PlanetKill(replacement,birds);
        Check(PlanetKills(p)==2,"stale session and unrecovered replacement cannot credit the saved run");
        Call(Planet,null,"OnLogin",replacement);p=replacement;PlanetKill(p,birds);
        Check(PlanetKills(p)==3,"new canonical session recovers progress without inheriting the old session cache");
        for(int i=3;i<95;i++)PlanetKill(p,birds);
        var finalMobs=Enumerable.Range(0,12).Select(i=>DemonMob(birds,18)).ToArray();
        Parallel.ForEach(finalMobs,m=>m.Die(p.Entity));
        foreach(var victim in finalMobs)birds.Entities.Remove(victim.UID);
        Check(PlanetKills(p)==100 && Scalar("SELECT count(*) FROM revora_planet_runs WHERE uid=@uid AND stage='encounter'",p.Entity.UID)==1,
            "100 actual Birdman deaths atomically unlock the guarded encounter stage");
        PlanetKill(p,birds);Call(Planet,null,"OnLogin",p);PlanetKill(p,birds);
        Check(PlanetKills(p)==100,"completed hunt cannot overcount after repeat deaths or login recovery");
        Check(Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND title_key=60096009",p.Entity.UID)==0,
            "hunt alone never grants Planet wings");
        PlanetTown(p,town);Call(Planet,null,"Accept",p);
        Check(Scalar("SELECT count(*) FROM revora_planet_runs WHERE uid=@uid",p.Entity.UID)==1 && PlanetKills(p)==100,
            "readmission preserves the encounter stage without resetting earned kills");
        var boundary=PlanetPlayer(town);PlanetTown(boundary,town);Call(Planet,null,"Accept",boundary);DemonMove(boundary,birds);
        planetNow=new DateTime(2030,3,11,7,0,0,DateTimeKind.Utc);PlanetKill(boundary,birds);
        Check(PlanetKills(boundary)==0,"Monday midnight closes kill credit exactly");
        planetNow=new DateTime(2030,3,16,9,0,0,DateTimeKind.Utc);PlanetTown(p,town);Call(Planet,null,"Accept",p);DemonMove(p,birds);PlanetKill(p,birds);
        Check(Scalar("SELECT count(*) FROM revora_planet_runs WHERE uid=@uid",p.Entity.UID)==2 && PlanetKills(p)==101,
            "a new weekend has independent progress and preserves the old receipt");
        var fault=PlanetPlayer(town);PlanetTown(fault,town);Call(Planet,null,"Accept",fault);DemonMove(fault,birds);
        Query("CREATE FUNCTION reject_planet_kill() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER reject_planet_kill BEFORE UPDATE ON revora_planet_runs FOR EACH ROW EXECUTE FUNCTION reject_planet_kill()");
        try {PlanetKill(fault,birds);} finally {Query("DROP TRIGGER reject_planet_kill ON revora_planet_runs; DROP FUNCTION reject_planet_kill()");}
        Check(PlanetKills(fault)==0 && Fenced(fault),"suppressed kill persistence prevents false credit and fences the session");
        Check(Scalar("SELECT count(*) FROM revora_planet_runs WHERE uid=@uid AND stage='complete'",p.Entity.UID)==0,
            "hunt receipts cannot substitute for encounter and delivery completion");
        VerifyPlanetEncounter(runtime,town,birds,p);
    }
    static void VerifyPlanetEncounter(string runtime,Map town,Map birds,GameState p)
    {
        var encounter=typeof(Entity).Assembly.GetType("Revora.Game.Features.PlanetEncounter");
        Call(encounter,null,"Initialize");Call(encounter,null,"RecoverInterrupted");
        var terrain=World(runtime,700);terrain.Path="planet-fixture";
        DMaps.MapPaths[700]="planet-fixture";DMaps.MapPaths[1002]="planet-town-fixture";
        PlanetTown(p,town);
        Check(!(bool)Call(encounter,null,"Enter",p),"Planet guarded encounter cannot skip the unfinished Birdman hunt");
        DemonMove(p,birds);for(int i=1;i<100;i++)PlanetKill(p,birds);PlanetTown(p,town);
        Check((bool)Call(encounter,null,"Enter",p),"ordinary hunter enters the guarded arena after earning 100 kills");
        var arena=p.Map;ushort arenaId=arena.ID;
        var guards=arena.Entities.Values.Where(e=>e.EntityFlag==EntityFlag.Monster).OrderBy(e=>e.MonsterInfo.ID).ToArray();
        Check(arenaId!=700 && arena.BaseID==700 && guards.Length==3 && guards[0].MaxHitpoints==70100 && guards[2].MaxHitpoints==210300,
            "Planet creates an owned map with two sentries and one captain using local stat copies");
        Check(guards.All(g=>g.Level==g.MonsterInfo.Level && g.Level<120),"installed Planet guards are below the level-120 unarmed threshold");
        System.Console.WriteLine("Planet guard levels: "+string.Join(",",guards.Select(g=>g.Level.ToString())));
        Check(MonsterInformation.MonsterInformations[1413].Hitpoints==35050 && MonsterInformation.MonsterInformations[1413].MaxAttack==900,
            "Planet combat tuning does not alter the shared AlienSerpent template");
        Check(Scalar("SELECT count(*) FROM revora_planet_attempts WHERE uid=@uid AND state='active' AND NOT returned",p.Entity.UID)==1,
            "Planet admission is durable before player combat");
        var outsider=PlanetPlayer(town);PlanetTown(outsider,town);
        outsider.Entity.Teleport(700,arenaId,48,41);
        Check(outsider.Entity.MapID==1002,"outside teleport cannot enter another player's Planet arena");
        Check((bool)Call(encounter,null,"BlockAttack",outsider.Entity,guards[0]),"outsider cannot damage an owned Planet guard");
        Check(!(bool)Call(encounter,null,"BlockAttack",p.Entity,guards[0]) && !(bool)Call(encounter,null,"BlockAttack",guards[0],p.Entity),
            "admitted player and owned sentries can fight through the combat authorization gate");
        outsider.Attackable=true;uint outsideHp=outsider.Entity.Hitpoints;
        new Revora.Game.Attacking.Handle(null,guards[0],outsider.Entity);
        Check(outsider.Entity.Hitpoints==outsideHp,"direct monster execution cannot bypass Planet ownership and hit an outsider");
        VerifyPlanetCombat(p,outsider,guards);

        p.ActiveNpc=900000190;
        Check(!(bool)Call(encounter,null,"Collect",p),"cache cannot be recovered while sentries are alive");
        p.Entity.X=48;p.Entity.Y=45;
        Query("CREATE FUNCTION reject_planet_guard() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER reject_planet_guard BEFORE UPDATE ON revora_planet_attempts FOR EACH ROW EXECUTE FUNCTION reject_planet_guard()");
        try {guards[0].Die(p.Entity);}finally{Query("DROP TRIGGER reject_planet_guard ON revora_planet_attempts; DROP FUNCTION reject_planet_guard()");}
        Check(!guards[0].Dead && Scalar("SELECT defeated FROM revora_planet_attempts WHERE uid=@uid AND NOT returned",p.Entity.UID)==0 && Fenced(p),
            "failed guard receipt preserves the living guard and fences the session");
        DemonReset(p);
        foreach(var guard in guards) {guard.Die(p.Entity);guard.Die(p.Entity);}
        Check(Scalar("SELECT defeated FROM revora_planet_attempts WHERE uid=@uid AND NOT returned",p.Entity.UID)==7 && guards.All(g=>g.Dead),
            "three owned deaths save distinct guard bits without repeat credit");
        Check(Scalar("SELECT count(*) FROM revora_planet_runs WHERE uid=@uid AND stage='delivery'",p.Entity.UID)==0,
            "defeating guards does not substitute for collecting the cache");
        Query("CREATE FUNCTION reject_planet_delivery() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER reject_planet_delivery BEFORE UPDATE ON revora_planet_runs FOR EACH ROW EXECUTE FUNCTION reject_planet_delivery()");
        bool failed=false;try{Call(encounter,null,"Collect",p);}catch(InvalidOperationException){failed=true;}
        finally{Query("DROP TRIGGER reject_planet_delivery ON revora_planet_runs; DROP FUNCTION reject_planet_delivery()");}
        Check(failed && Scalar("SELECT count(*) FROM revora_planet_attempts WHERE uid=@uid AND state='victory'",p.Entity.UID)==0,
            "delivery write failure rolls back the cache victory receipt");
        Check((bool)Call(encounter,null,"Collect",p),"cache collection commits the earned delivery stage");
        Check(p.Entity.MapID==1002 && p.Entity.X==321 && p.Entity.Y==299 && !Kernel.Maps.ContainsKey(arenaId),
            "successful cache recovery returns to Rowan and disposes the empty private arena");
        Check(Scalar("SELECT count(*) FROM revora_planet_attempts WHERE uid=@uid AND state='victory' AND defeated=7 AND returned AND cache_at IS NOT NULL",p.Entity.UID)==1
            && Scalar("SELECT count(*) FROM entities WHERE uid=@uid AND mapid=1002 AND x=321 AND y=299",p.Entity.UID)==1,
            "cache victory and acknowledged safe return persist");
        Check(!(bool)Call(encounter,null,"Collect",p) && !(bool)Call(encounter,null,"Enter",p),
            "collected cache cannot be replayed or sent back through the encounter");
        Call(encounter,null,"OnLogin",p);
        Check(Scalar("SELECT count(*) FROM revora_planet_runs WHERE uid=@uid AND stage='delivery'",p.Entity.UID)==1
            && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND title_key=60096009",p.Entity.UID)==0,
            "saved cache survives recovery without an early wing grant");
        PlanetTown(outsider,town);Call(Planet,null,"Accept",outsider);DemonMove(outsider,birds);
        for(int i=0;i<100;i++)PlanetKill(outsider,birds);PlanetTown(outsider,town);
        Check((bool)Call(encounter,null,"Enter",outsider),"second ordinary hunter earns an independent guarded encounter");
        ushort abandoned=outsider.Entity.MapID;
        outsider.Entity.Hitpoints=0;planetNow=planetNow.AddSeconds(2);Call(encounter,null,"Tick");
        Check(outsider.Entity.MapID==1002 && !Kernel.Maps.ContainsKey(abandoned)
            && Scalar("SELECT count(*) FROM revora_planet_attempts WHERE uid=@uid AND state='abandoned' AND returned",outsider.Entity.UID)==1,
            "dead participant forfeits the attempt and receives a durable safe return");
        outsider.Entity.Hitpoints=outsider.Entity.MaxHitpoints;
        Check(PlanetKills(outsider)==100 && (bool)Call(encounter,null,"Enter",outsider),"failed fight retries without repeating the saved Birdman hunt");
        abandoned=outsider.Entity.MapID;planetNow=planetNow.AddMinutes(15);Call(encounter,null,"Tick");
        Check(outsider.Entity.MapID==1002 && !Kernel.Maps.ContainsKey(abandoned)
            && Scalar("SELECT count(*) FROM revora_planet_attempts WHERE uid=@uid AND state='victory'",outsider.Entity.UID)==0,
            "fifteen-minute timeout returns the player without inventing cache victory");
        Check((bool)Call(encounter,null,"Enter",outsider),"timed-out encounter can be retried within the same weekend");
        abandoned=outsider.Entity.MapID;var fight=outsider.Map;
        var first=fight.Entities.Values.First(e=>e.EntityFlag==EntityFlag.Monster);outsider.Entity.X=48;outsider.Entity.Y=45;first.Die(outsider.Entity);
        var reconnected=PlanetPlayer(town);GameState ignored;
        Kernel.GamePool.TryRemove(reconnected.Entity.UID,out ignored);reconnected.Entity.UID=outsider.Entity.UID;
        Kernel.GamePool[reconnected.Entity.UID]=reconnected;reconnected.BindWorldMap(abandoned);
        reconnected.Entity.X=48;reconnected.Entity.Y=45;
        Call(encounter,null,"OnLogin",reconnected);outsider=reconnected;
        Check(outsider.Entity.MapID==1002 && !Kernel.Maps.ContainsKey(abandoned)
            && Scalar("SELECT count(*) FROM revora_planet_attempts WHERE uid=@uid AND NOT returned",outsider.Entity.UID)==0,
            "replacement canonical session forfeits the interrupted scene and recovers its return");
        PlanetTown(outsider,town);Check((bool)Call(encounter,null,"Enter",outsider),"recovered player can retry the unfinished encounter");
        Check(Scalar("SELECT defeated FROM revora_planet_attempts WHERE uid=@uid AND NOT returned",outsider.Entity.UID)==0,
            "retry cannot inherit guard defeats from the interrupted attempt");
        abandoned=outsider.Entity.MapID;
        bool liveRecoveryRejected=false;try{Call(encounter,null,"RecoverInterrupted");}catch(InvalidOperationException){liveRecoveryRejected=true;}
        Check(liveRecoveryRejected,"startup recovery refuses to erase a live owned encounter");
        // Simulate process memory loss explicitly; do not claim an actual Host restart.
        var maps=encounter.GetField("Maps",Hidden).GetValue(null);maps.GetType().GetMethod("Clear").Invoke(maps,null);
        Kernel.Maps[abandoned].Dispose();MapsTable.MapInformations.Remove(abandoned);
        Call(encounter,null,"RecoverInterrupted");Call(encounter,null,"OnLogin",outsider);
        Check(outsider.Entity.MapID==1002 && Scalar("SELECT count(*) FROM revora_planet_attempts WHERE uid=@uid AND state='active'",outsider.Entity.UID)==0
            && Scalar("SELECT count(*) FROM revora_planet_runs WHERE uid=@uid AND stage='encounter' AND kills=100",outsider.Entity.UID)==1,
            "empty-process recovery cancels unfinished attempts while preserving earned hunt eligibility");
        Check(Scalar("SELECT count(*) FROM revora_planet_attempts WHERE uid=@uid AND state='victory'",p.Entity.UID)==1,
            "interrupted-attempt recovery preserves already committed cache victory");
        PlanetTown(outsider,town);Check((bool)Call(encounter,null,"Enter",outsider),"interrupted hunter can retry after startup recovery");
        outsider.Entity.X=48;outsider.Entity.Y=45;outsider.ActiveNpc=900000190;
        foreach(var guard in outsider.Map.Entities.Values.Where(e=>e.EntityFlag==EntityFlag.Monster).ToArray())guard.Die(outsider.Entity);
        Query("CREATE FUNCTION reject_planet_return() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER reject_planet_return BEFORE UPDATE ON revora_planet_attempts FOR EACH ROW WHEN (NEW.returned) EXECUTE FUNCTION reject_planet_return()");
        try {Check((bool)Call(encounter,null,"Collect",outsider),"cache victory commits independently of subsequent return acknowledgement");}
        finally {Query("DROP TRIGGER reject_planet_return ON revora_planet_attempts; DROP FUNCTION reject_planet_return()");}
        Check(Fenced(outsider) && Scalar("SELECT count(*) FROM revora_planet_attempts WHERE uid=@uid AND state='victory' AND NOT returned",outsider.Entity.UID)==1,
            "failed return acknowledgement keeps a durable pending return without losing cache victory");
        DemonReset(outsider);Call(encounter,null,"OnLogin",outsider);
        Check(outsider.Entity.MapID==1002 && Scalar("SELECT count(*) FROM revora_planet_attempts WHERE uid=@uid AND NOT returned",outsider.Entity.UID)==0,
            "recovery safely retries and acknowledges a failed return without replaying cache collection");
        VerifyPlanetDelivery(town,birds,p,outsider);


    }
    static void PlanetNpc(GameState p,byte option) {
        p.ActiveNpc=900000016;
        Npcs.GetDialog(new NpcRequest(true){NpcID=900000016,OptionID=option},p);
    }
    static void VerifyPlanetDelivery(Map town,Map birds,GameState p,GameState other)
    {
        var encounter=typeof(Entity).Assembly.GetType("Revora.Game.Features.PlanetEncounter");
        TitleRules.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>planetNow));
        try {
            PlanetTown(p,town);p.ActiveNpc=0;
            Check(!(bool)Call(Planet,null,"Deliver",p),"Planet delivery requires the actual Rowan interaction");
            p.ActiveNpc=900000016;p.Entity.X=330;
            Check(!(bool)Call(Planet,null,"Deliver",p),"remote cache delivery cannot complete a weekend");
            PlanetTown(p,town);
            var saved=planetNow;planetNow=new DateTime(2030,3,18,7,0,0,DateTimeKind.Utc);PlanetTown(other,town);
            Check(!(bool)Call(Planet,null,"Deliver",other),"cache delivery closes exactly at Monday midnight");planetNow=saved;
            var deliveries=new bool[8];Parallel.For(0,8,i=>deliveries[i]=(bool)Call(Planet,null,"Deliver",p));
            Check(deliveries.Count(v=>v)==1 && Scalar("SELECT count(*) FROM revora_planet_completions WHERE uid=@uid",p.Entity.UID)==1,
                "concurrent deliveries record exactly one completed weekend");
            Check(!OwnedTitles(p).ContainsKey(60096009),"one complete hunt/encounter/delivery route cannot grant Planet wings");
            Check(((string)Call(Planet,null,"Status",p)).Contains("1/13"),"Rowan reports saved collection progress after delivery");
            PlanetNpc(p,211);
            Check(Scalar("SELECT count(*) FROM revora_planet_runs WHERE uid=@uid AND weekend='20300316'",p.Entity.UID)==1,
                "repeated Rowan acceptance cannot create a second run for the completed weekend");
            var weekends=new[]{new DateTime(2030,4,6,9,0,0,DateTimeKind.Utc),new DateTime(2030,4,20,9,0,0,DateTimeKind.Utc),new DateTime(2030,5,4,9,0,0,DateTimeKind.Utc)};
            for(int n=0;n<weekends.Length;n++) {
                planetNow=weekends[n];PlanetTown(p,town);PlanetNpc(p,211);
                Check(!Fenced(p) && ((string)Call(Planet,null,"Status",p)).Contains("0/100"),"Rowan admits a fresh nonconsecutive weekend hunt "+(n+2));
                Check(!(bool)Call(Planet,null,"Deliver",p),"unfinished new weekend cannot reuse a previous cache "+(n+2));
                DemonMove(p,birds);for(int i=0;i<100;i++)PlanetKill(p,birds);PlanetTown(p,town);PlanetNpc(p,212);
                Check(p.Map.BaseID==700 && p.Map.ID!=700 && !Fenced(p),"Rowan sends the ordinary hunter into the earned encounter "+(n+2));
                p.Entity.X=48;p.Entity.Y=45;
                foreach(var guard in p.Map.Entities.Values.Where(e=>e.EntityFlag==EntityFlag.Monster).ToArray())PlanetCombatDefeat(p,guard);
                p.ActiveNpc=900000190;
                Check((bool)Call(encounter,null,"Collect",p),"earned cache becomes deliverable for weekend "+(n+2));
                PlanetTown(p,town);
                if(n==2) {
                    Query("CREATE FUNCTION reject_planet_award() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER reject_planet_award BEFORE INSERT ON revora_event_awards FOR EACH ROW WHEN (NEW.source='planet-revora-v1') EXECUTE FUNCTION reject_planet_award()");
                    bool failed=false;try{Call(Planet,null,"Deliver",p);}catch(InvalidOperationException){failed=true;}
                    finally{Query("DROP TRIGGER reject_planet_award ON revora_event_awards; DROP FUNCTION reject_planet_award()");}
                    Check(failed && Scalar("SELECT count(*) FROM revora_planet_completions WHERE uid=@uid",p.Entity.UID)==3
                        && Scalar("SELECT count(*) FROM revora_planet_runs WHERE uid=@uid AND weekend='20300504' AND stage='delivery'",p.Entity.UID)==1,
                        "suppressed fourth award rolls back completion and preserves the earned cache for retry");
                }
                PlanetNpc(p,213);
                Check(!Fenced(p) && Scalar("SELECT count(*) FROM revora_planet_completions WHERE uid=@uid",p.Entity.UID)==n+2,
                    "Rowan records earned distinct weekend delivery "+(n+2));
                Check(OwnedTitles(p).ContainsKey(60096009)==(n==2),"only the fourth earned delivery publishes Planet wings "+(n+2));
            }
            Check(Scalar(@"SELECT count(*) FROM revora_planet_completions d JOIN revora_planet_runs r ON r.id=d.run
JOIN revora_planet_attempts a ON a.id=d.attempt WHERE d.uid=@uid AND a.uid=d.uid AND a.run=d.run
AND r.stage='complete' AND r.kills=100 AND a.state='victory' AND a.defeated=7 AND a.returned AND d.completed_at>=a.cache_at",p.Entity.UID)==4,
                "all four completion receipts trace to actual earned hunts and collected owned caches");
            Check(Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source='planet-revora-v1' AND title_key=60096009 AND expires_at-earned_at=interval '720 hours'",p.Entity.UID)==1,
                "fourth delivery stores one wing award with an exact thirty-day term");
            DateTime expiry;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT expires_at FROM revora_event_awards WHERE uid=@uid AND source='planet-revora-v1'",c)) {
                q.Parameters.AddWithValue("uid",(long)p.Entity.UID);using(var r=q.ExecuteReader()){r.Read();expiry=r.GetFieldValue<DateTimeOffset>(0).UtcDateTime;}
            }}
            Check(expiry==planetNow.AddDays(30),"Planet expiry starts at the fourth delivery rather than an earlier hunt or later login");
            p.Entity.MyAchievement=new Achievement(p.Entity);p.Entity.WTitles=null;new TitleStorage().Login(p);
            TitleRequest(60096009,TitleStorage.Action.Equip).Handle(p);
            Check(OwnedTitles(p).ContainsKey(60096009) && p.Entity.EquippedWing==60096009,
                "ordinary player reloads the durable Planet wing and equips it");
            p=VerifyPlanetExchange(town,p,other);
            VerifyPlanetRestartProcess(town,birds,p,expiry);
            planetNow=planetNow.AddDays(7);PlanetTown(p,town);PlanetNpc(p,211);PlanetNpc(p,213);
            Check(Scalar("SELECT count(*) FROM revora_planet_completions WHERE uid=@uid",p.Entity.UID)==4
                && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source='planet-revora-v1'",p.Entity.UID)==1,
                "later-weekend acceptance and delivery cannot renew or duplicate the one-time wing award");
            planetNow=expiry.AddTicks(-10);new TitleStorage().Login(p);
            Check(OwnedTitles(p).ContainsKey(60096009),"Planet wing remains available one microsecond before expiry");
            planetNow=expiry;new TitleStorage().Login(p);
            Check(!OwnedTitles(p).ContainsKey(60096009) && p.Entity.EquippedWing==0,
                "Planet wing expires and unequips exactly at the saved deadline");
            planetNow=planetNow.AddDays(6);PlanetTown(p,town);PlanetNpc(p,211);new TitleStorage().Login(p);
            Check(!OwnedTitles(p).ContainsKey(60096009) && Scalar("SELECT count(*) FROM revora_planet_completions WHERE uid=@uid",p.Entity.UID)==4,
                "expiry does not reopen the one-time expedition award or erase completion history");
            for(int week=0;week<4;week++) {
                planetNow=planetNow.AddDays(7);
                while(Call(Planet,null,"Current",planetNow)==null)planetNow=planetNow.AddDays(1);
                PlanetTown(p,town);PlanetNpc(p,211);DemonMove(p,birds);
                for(int kill=0;kill<100;kill++)PlanetKill(p,birds);
                PlanetTown(p,town);PlanetNpc(p,212);p.Entity.X=48;p.Entity.Y=45;
                foreach(var guard in p.Map.Entities.Values.Where(e=>e.EntityFlag==EntityFlag.Monster).ToArray())PlanetCombatDefeat(p,guard);
                p.ActiveNpc=900000190;Check((bool)Call(encounter,null,"Collect",p),"later collection weekend earns its guarded cache");
                PlanetTown(p,town);PlanetNpc(p,213);
            }
            Check(Scalar("SELECT count(*) FROM revora_planet_completions WHERE uid=@uid",p.Entity.UID)==8,"all eight collection deliveries are earned");
            var exchange=typeof(Entity).Assembly.GetType("Revora.Game.Features.PlanetCollectionExchange",true);
            uint[] stock=((uint[])exchange.GetField("Garments",Hidden).GetValue(null)).Concat((uint[])exchange.GetField("Mounts",Hidden).GetValue(null)).ToArray();
            for(int i=79;i<156;i++)if(i!=108) {
                PlanetNpc(p,(byte)(40+i));var item=p.Inventory.Objects.Single(g=>g.ID==stock[i]);
                new CoatStorage{Info=new CoatStorageProto{ActionId=CoatStorage.Action.Addtowardrobe,ItemGuid=item.UID,ItemId=(int)item.ID}}.Handle(p);
                Check(item.InWardrobe && item.Bound && item.Days==0,"remaining exchange design earned and stored: "+stock[i]);
            }
            foreach(var item in p.Inventory.Objects.ToArray())
                new CoatStorage{Info=new CoatStorageProto{ActionId=CoatStorage.Action.Addtowardrobe,ItemGuid=item.UID,ItemId=(int)item.ID}}.Handle(p);
            Check(Scalar("SELECT count(*) FROM revora_planet_exchange e JOIN items i ON i.uid=e.item_uid AND i.entityid=e.uid AND i.id=e.design WHERE e.uid=@uid AND i.inwardrobe=1 AND i.bound=1 AND i.days::text='0'",p.Entity.UID)==156,"all156 frozen exchange designs preserve exact paid receipt and wardrobe ownership");
            Check(Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11231",p.Entity.UID)==1,"combined Planet and exchange earning reaches100 permanent garments");
            string beforeUpgrade=FullPlanetCollectionSnapshot(p.Entity.UID);
            Query("ALTER TABLE revora_planet_runs DROP CONSTRAINT revora_planet_collection_step_v2; ALTER TABLE revora_planet_runs ADD CONSTRAINT revora_planet_runs_collection_step_check CHECK(collection_step BETWEEN 0 AND 8)");
            Call(Planet,null,"Initialize");Call(Planet,null,"Initialize");
            Check(FullPlanetCollectionSnapshot(p.Entity.UID)==beforeUpgrade,"upgrade of an existing eight-step database preserves every earned receipt and item and is idempotent");
            for(int week=9;week<=13;week++) {
                planetNow=planetNow.AddDays(7);
                while(Call(Planet,null,"Current",planetNow)==null)planetNow=planetNow.AddDays(1);
                PlanetTown(p,town);PlanetNpc(p,211);DemonMove(p,birds);
                for(int kill=0;kill<100;kill++)PlanetKill(p,birds);
                PlanetTown(p,town);PlanetNpc(p,212);p.Entity.X=48;p.Entity.Y=45;
                foreach(var guard in p.Map.Entities.Values.Where(e=>e.EntityFlag==EntityFlag.Monster).ToArray())PlanetCombatDefeat(p,guard);
                p.ActiveNpc=900000190;Check((bool)Call(encounter,null,"Collect",p),"extended weekend earns its guarded cache");
                PlanetTown(p,town);var before=p.Inventory.Objects.Select(g=>g.UID).ToArray();PlanetNpc(p,213);
                var rune=p.Inventory.Objects.Single(g=>!before.Contains(g.UID));
                Check(rune.ID==4030101 && rune.Bound && Scalar("SELECT max(collection_step) FROM revora_planet_runs WHERE uid=@uid AND stage='complete'",p.Entity.UID)==week,"new weekend delivers one bound rune and the next saved collection step");
                PlanetNpc(p,213);Check(p.Inventory.Objects.Count(g=>!before.Contains(g.UID))==1,"extended delivery replay cannot duplicate its rune");
            }
            PlanetNpc(p,246);
            for(int i=156;i<stock.Length;i++) {
                PlanetNpc(p,(byte)(220+(i-156)/6));PlanetNpc(p,(byte)(40+(i-156)));
                var item=p.Inventory.Objects.Single(g=>g.ID==stock[i]);
                new CoatStorage{Info=new CoatStorageProto{ActionId=CoatStorage.Action.Addtowardrobe,ItemGuid=item.UID,ItemId=(int)item.ID}}.Handle(p);
                Check(item.InWardrobe && item.Bound && item.Days==0,"extended catalogue purchase/deposit: "+stock[i]);
            }
            Check(Scalar("SELECT count(*) FROM revora_planet_exchange WHERE uid=@uid",p.Entity.UID)==257,"thirteen earned weekends pay for every257 frozen exchange design");
            using(var c=DataHolder.MySqlConnection){c.Open();Check((int)Call(exchange,null,"Balance",c,null,p.Entity.UID)==3,"260 earned credits minus257 paid designs leave three credits");}
            p=PlanetRestartPlayer(town,p.Entity.UID);ConquerItemTable.LoadItems(p);PlanetTown(p,town);
            Check(Scalar("SELECT count(*) FROM revora_planet_exchange e JOIN items i ON i.uid=e.item_uid AND i.entityid=e.uid AND i.id=e.design WHERE e.uid=@uid AND i.inwardrobe=1 AND i.bound=1 AND i.days::text='0'",p.Entity.UID)==257,"production reload retains every permanent paid exchange item");
            PlanetNpc(p,246);PlanetNpc(p,40);PlanetNpc(p,247);PlanetNpc(p,40);
            Check(Scalar("SELECT count(*) FROM revora_planet_exchange WHERE uid=@uid",p.Entity.UID)==257,"both catalogue sections refuse owned-design replay after reload");
            planetNow=planetNow.AddDays(7);Check(Call(Planet,null,"Accept",p)==null,"completed thirteenth delivery closes further collection admission");

            VerifyFullPlanetCollectionRestart(p);
        } finally {TitleRules.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow));}
    }
    static GameState VerifyPlanetExchange(Map town,GameState p,GameState other)
    {
        var exchange=typeof(Entity).Assembly.GetType("Revora.Game.Features.PlanetCollectionExchange",true);
        Func<GameState,int,string> buy=(owner,index)=>(string)Call(exchange,null,"Buy",owner,index);
        Func<long> spent=()=>Scalar("SELECT count(*) FROM revora_planet_exchange WHERE uid=@uid",p.Entity.UID);
        PlanetTown(other,town);buy(other,0);
        Check(Scalar("SELECT count(*) FROM revora_planet_exchange WHERE uid=@uid",other.Entity.UID)==0,"unfinished expedition gives no exchange credit");
        PlanetTown(p,town);p.Entity.X=340;buy(p,0);PlanetTown(p,town);
        Check(spent()==0,"remote exchange refuses an otherwise earned balance");
        buy(p,-1);buy(p,257);Check(spent()==0,"invalid exchange indices cannot select arbitrary items");
        var old=p.Inventory.Objects.Select(i=>i.UID).ToArray();
        while(p.Inventory.Count<40)Check(p.Inventory.Add(611004,0,1),"exchange bag fixture fills");
        buy(p,0);Check(spent()==0,"full bag preserves expedition credit");
        foreach(var item in p.Inventory.Objects.Where(i=>!old.Contains(i.UID)).ToArray())p.Inventory.Remove(item,Enums.ItemUse.Remove);
        var hook=exchange.GetField("BeforeCommit",Hidden);hook.SetValue(null,(Action)(()=>{throw new InvalidOperationException("exchange rollback probe");}));
        try{buy(p,0);}finally{hook.SetValue(null,(Action)(()=>{}));}
        Check(spent()==0 && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id=189135",p.Entity.UID)==0 && Fenced(p),"exchange item and credit roll back together and fence the session");
        p=PlanetRestartPlayer(town,p.Entity.UID);p.Entity.FullyLoaded=false;p.Entity.Vitality=100;p.Entity.FullyLoaded=true;ConquerItemTable.LoadItems(p);PlanetTown(p,town);
        Parallel.For(0,8,n=>buy(p,0));
        Check(spent()==1 && CountId(p,189135)==1,"concurrent exchange replay charges once and grants once");
        var gift=p.Inventory.Objects.Single(i=>i.ID==189135);
        Check(gift.Bound && gift.Days==0 && gift.TimeLeftInMinutes==0,"exchange cosmetic is permanent and bound");
        uint giftUid=gift.UID;
        p=PlanetRestartPlayer(town,p.Entity.UID);p.Entity.FullyLoaded=false;p.Entity.Vitality=100;p.Entity.FullyLoaded=true;ConquerItemTable.LoadItems(p);PlanetTown(p,town);buy(p,0);
        Check(spent()==1 && p.Inventory.Objects.Single(i=>i.ID==189135).UID==giftUid,"canonical replacement and production item loading preserve the exact exchange item and receipt");
        Action<uint> store=id=>{
            var item=p.Inventory.Objects.Single(i=>i.ID==id);
            new CoatStorage{Info=new CoatStorageProto{ActionId=CoatStorage.Action.Addtowardrobe,ItemGuid=item.UID,ItemId=(int)item.ID}}.Handle(p);
            Check(item.InWardrobe && !p.Inventory.ContainsUID(item.UID),"exchange design enters ordinary permanent wardrobe: "+id);
        };
        store(189135);
        PlanetNpc(p,148);Check(spent()==2 && CountId(p,200021)==1,"Rowan dialogue buys the selected mount from earned credits");store(200021);
        var garments=(uint[])exchange.GetField("Garments",Hidden).GetValue(null);
        for(int i=1;i<=78;i++){PlanetNpc(p,(byte)(40+i));Check(CountId(p,garments[i])==1,"dialogue purchases exchange index "+i);store(garments[i]);}
        PlanetNpc(p,119);
        Check(spent()==80 && CountId(p,garments[79])==0,"four earned weekend deliveries fund exactly eighty designs and refuse overspending");
        return p;
    }
}
