using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;

internal static partial class PotencyHuntTests
{
    static void PreparePkReloadSchema() {
        Query("DROP TABLE entities;CREATE TABLE entities AS TABLE public.entities WITH NO DATA");
        Query("ALTER TABLE entities ALTER COLUMN conquerpoints SET DEFAULT 0,ALTER COLUMN revorapoint SET DEFAULT 0,ALTER COLUMN money SET DEFAULT 0");
        foreach(string table in new[]{"teamarena","championarena","quests","reward","rewardclameid","merchant","dialyquests","activeness","house","pkexploits","achievement","itemadding"})
            Query("CREATE TABLE IF NOT EXISTS "+table+" AS TABLE public."+table+" WITH NO DATA");
        Query("CREATE TABLE IF NOT EXISTS prizes(uid bigint PRIMARY KEY,name text,prizes bytea)");
    }
    static void SavePkProfileBeforeClaim(GameState p) {
        p.Entity.DailySignDays=new byte[0];p.Entity.Vitality=100;
        if(!EntityTable.SaveEntity(p))throw new Exception("PK reconnect profile save failed.");
    }
    static GameState ReconnectPkPrize(GameState p,bool classPrize) {
        var earned=p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i).ToArray();
        uint cp=p.Entity.ConquerPoints,rvp=p.Entity.RevoraPoint;ulong gold=p.Entity.Money;
        var fresh=ReloadBall(p);Call(Pk,null,"OnLogin",fresh);
        if(classPrize)ClassNpc(fresh,3);else PkNpc(fresh,3);
        Check(fresh.Account.State==AccountTable.AccountState.Player && fresh.Entity.ConquerPoints==cp && fresh.Entity.RevoraPoint==rvp && fresh.Entity.Money==gold,
            "fresh ordinary character reload preserves all champion currencies without replaying payment");
        Check(PkSoul(fresh) && fresh.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i).SequenceEqual(earned),
            "fresh ordinary character reload preserves exact bound soul and EXP Ball UIDs after claim replay");
        return fresh;
    }
    static bool PkSoul(GameState p) {
        var souls=p.Inventory.Objects.Where(i=>ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7).ToArray();
        return souls.Length==1 && souls[0].Bound && Scalar("SELECT count(*) FROM revora_pk_claims c JOIN items i ON i.uid=ANY(c.rewards) WHERE c.uid=@uid AND i.id BETWEEN 800000 AND 899999 AND i.bound=1",p.Entity.UID)==1;
    }
    static void ReloadPkPrize(GameState p) {
        Query("CREATE TABLE IF NOT EXISTS itemadding (LIKE public.itemadding INCLUDING ALL)");
        var earned=p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i).ToArray();
        foreach(var item in p.Inventory.Objects)p.Inventory.Remove(item.UID,Enums.ItemUse.None,false);
        ConquerItemTable.LoadItems(p);
        Check(PkSoul(p) && p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i).SequenceEqual(earned),
            "production item reload restores exact champion reward UIDs including the bound P7 soul");
    }
    static readonly Type Pk=typeof(Entity).Assembly.GetType("Revora.Game.Features.LocalPkWar");
    static readonly Type PkStore=typeof(Entity).Assembly.GetType("Revora.Game.Features.LocalPkResults");
    static DateTime pkNow,pkStart;
    static object NewPk(string runtime) {
        Func<ushort,Map> make=id=>{var m=World(runtime,id);Kernel.Maps.Remove(id);m.BaseID=id;m.ID=epkMapId++;Kernel.Maps[m.ID]=m;
            MapsTable.MapInformations[m.ID]=new MapsTable.MapInformation{ID=m.ID,BaseID=id,Status=2};return m;};
        var e=Activator.CreateInstance(Pk,BindingFlags.NonPublic|BindingFlags.Instance,null,new object[]{make,(Func<DateTime>)(()=>pkNow)},null);
        Pk.GetField("Current",Hidden).SetValue(null,e);return e;
    }
    static IEnumerable<object> PkRuns(object e){return ((IEnumerable)Field(e,"runs")).Cast<object>();}
    static void PkTick(object e,double minutes){pkNow=pkStart.AddMinutes(minutes);Call(Pk,e,"Tick");}
    static GameState PkPlayer(Map town,byte level){var p=MoonPlayer(town);p.Entity.Level=level;p.Entity.Titles=new System.Collections.Concurrent.ConcurrentDictionary<TitlePacket.Titles,DateTime>();return p;}
    static void PkNpc(GameState p,byte option){MoonNpc(p,2314,option);}
    static long PkEntries(GameState p){return Scalar("SELECT count(*) FROM revora_pk_entries WHERE uid=@uid",p.Entity.UID);}
    static void VerifyLocalPk(string runtime,Map town) {
        PreparePkReloadSchema();
        Query("CREATE TABLE titles (LIKE public.titles INCLUDING ALL); CREATE TABLE status (LIKE public.status INCLUDING ALL)");
        StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
        ConquerItem.ItemUID=new Counter(6800000);Server.GamePool=new GameState[0];Server.Thread=(Revora.Thread)FormatterServices.GetUninitializedObject(typeof(Revora.Thread));
        Call(Pk,null,"PlaceGeneral");Check(town.Npcs.ContainsKey(2314) && !Revora.Game.Features.TwinCityPlaza.IsClosed(2314),"General Bravery is open on ordinary Twin City terrain");
        foreach(var boundary in new[]{Tuple.Create(1,0),Tuple.Create(99,0),Tuple.Create(100,1),Tuple.Create(119,1),Tuple.Create(120,2),Tuple.Create(129,2),Tuple.Create(130,3),Tuple.Create(140,3)})
            Check((int)Call(Pk,null,"Bracket",boundary.Item1)==boundary.Item2,"PK bracket boundary "+boundary.Item1);
        var summer=new DateTime(2026,9,20,3,0,0,DateTimeKind.Utc);var winter=new DateTime(2026,12,20,4,0,0,DateTimeKind.Utc);
        Check((DateTime)Call(Pk,null,"Start",summer)==summer && (DateTime)Call(Pk,null,"Start",winter)==winter,"20:00 Pacific schedule follows summer and winter offsets");
        Check((bool)Call(Pk,null,"Scheduled",summer,0) && !(bool)Call(Pk,null,"Scheduled",summer,1) && !(bool)Call(Pk,null,"Scheduled",summer.AddDays(1),0),"weekly and monthly calendars stay distinct");
        // Saturday August 1 offers both independent events.
        pkStart=pkNow=new DateTime(2026,8,2,3,0,0,DateTimeKind.Utc);var e=NewPk(runtime);Call(Pk,e,"Tick");
        Check(CtfCount("SELECT count(*) FROM revora_pk_wars")==8,"Saturday first-of-month opens eight separate level runs");
        var a=PkPlayer(town,140);var b=PkPlayer(town,140);var c=PkPlayer(town,140);var outsider=PkPlayer(town,140);
        Query("INSERT INTO status(entityid,status,time) VALUES(@uid,20,@time)","uid",(long)a.Entity.UID,"time",(decimal)Kernel.ToDateTimeInt(DateTime.Now.AddDays(10)));
        a.Entity.LoadTopStatus();Check(!a.Entity.Titles.ContainsKey((TitlePacket.Titles)20),"legacy monthly status cannot load a cross-server native title");
        a.Entity.Titles[(TitlePacket.Titles)21]=DateTime.Now.AddDays(7);a.Entity.MyTitle=(TitlePacket.Titles)21;a.Entity.AddFlag(37,StatusFlagsBigVector32.PermanentFlag,false);
        Call(Pk,null,"SynchronizeHonors",a,pkNow);
        Check(a.Entity.MyTitle==TitlePacket.Titles.None && !a.Entity.Titles.ContainsKey((TitlePacket.Titles)21) && !a.Entity.ContainsFlag(37),"unverified legacy title and halo are removed without granting an entitlement");
        outsider.Fake=true;PkNpc(outsider,1);outsider.Fake=false;outsider.ActiveNpc=2314;outsider.Entity.X=1;outsider.Entity.Y=1;
        Npcs.GetDialog(new NpcRequest(true){NpcID=2314,OptionID=1},outsider);Check(PkEntries(outsider)==0,"fake and distant players cannot register");
        PkNpc(a,1);PkNpc(b,1);PkNpc(c,1);PkTick(e,.02);
        Check(new[]{a,b,c}.All(p=>p.Entity.MapID!=1002 && p.Account.State==AccountTable.AccountState.Player) && a.Entity.MapID==b.Entity.MapID,"ordinary NPC admissions reach their owned weekly arena");
        Check((bool)Call(Pk,null,"BlockAttack",a.Entity,b.Entity),"entry preparation blocks combat");
        Revora.Game.Qualifier.QualifyEngine.DoSignup(a);
        Check(a.QualifierGroup==null && a.Entity.MapID==b.Entity.MapID,"ordinary Arena signup cannot import a PK entrant");
        Check(HasAchievement(a,11116) && !HasAchievement(a,11117) && !HasAchievement(a,11118),"competitive weekly field earns only weekly participation");
        ushort arena=a.Entity.MapID;outsider.Entity.Teleport(arena,a.Entity.X,a.Entity.Y);Check(outsider.Entity.MapID==1002,"external teleport cannot enter an owned arena");
        LabNear(a,2315);Check(Kernel.GetDistance(a.Entity.X,a.Entity.Y,a.Map.Npcs[2315].X,a.Map.Npcs[2315].Y)<=6,"first arena clerk is reachable through production jumps");
        uint hp=b.Entity.Hitpoints;EpkDamage(outsider,b,100);Check(b.Entity.Hitpoints==hp,"direct outsider receive attack cannot damage a fighter");
        PkTick(e,.2);EpkDamage(a,b,100);Check(b.Entity.Hitpoints<hp,"accepted ordinary PvP changes HP after preparation");
        epkNow=pkNow;var elite=NewEpk(runtime);
        Check(ReferenceEquals(Field(elite,"gate"),Field(e,"gate")),"Elite and local PK serialize transitions on the same combat gate");
        Parallel.Invoke(()=>{using((IDisposable)Call(Epk,null,"CombatLease",a.Entity,b.Entity))using((IDisposable)Call(Pk,null,"CombatLease",a.Entity,b.Entity))EpkDamage(a,b,1);},
            ()=>{using((IDisposable)Call(Pk,null,"CombatLease",a.Entity,b.Entity))using((IDisposable)Call(Epk,null,"CombatLease",a.Entity,b.Entity))EpkDamage(a,b,1);});
        Check(b.Entity.Hitpoints<hp,"opposite tournament lease order completes without deadlock or duplicate ownership");
        var run=PkRuns(e).Single(r=>(int)Field(r,"Kind")==0 && (int)Field(r,"Bracket")==3);var firstArena=a.Map;
        b.Entity.Die(a.Entity);PkTick(e,.3);Check(b.Entity.MapID==1002 && b.Entity.OnDeath==null && !b.Entity.Dead,"defeat exports and revives the loser without retaining event ownership");
        PkNpc(b,1);Check(b.Entity.MapID==1002 && PkEntries(b)==1,"a defeated entrant cannot reenter the same dated war");
        PkTick(e,19);PkNpc(outsider,1);Check(PkEntries(outsider)==0,"registration closes at exactly minute nineteen");
        PkTick(e,28);Check(a.Map.BaseID==1508 && c.Map.ID==a.Map.ID && firstArena.ID!=a.Map.ID,"survivors transfer to the second installed arena at minute twenty-eight");
        LabNear(a,2315);Check(Kernel.GetDistance(a.Entity.X,a.Entity.Y,a.Map.Npcs[2315].X,a.Map.Npcs[2315].Y)<=6,"second arena clerk is reachable through production jumps");
        Check((bool)Call(Pk,null,"BlockSpell",a),"stage transfer grants ten seconds of preparation");
        // Recreate a partial second-stage transfer, then use the production retry.
        run.GetType().GetField("Moving",Instance).SetValue(run,true);run.GetType().GetField("Stage",Instance).SetValue(run,0);
        var entries=((IEnumerable)Field(run,"Entries")).Cast<object>().ToArray();var cm=entries.Single(m=>ReferenceEquals(Field(m,"Player"),c));
        Call(Pk,e,"Travel",cm,firstArena,false);PkTick(e,28.1);
        Check(c.Map.ID==a.Map.ID && !(bool)Field(run,"Moving"),"partial stage transfer retries without forfeiting either survivor");
        PkTick(e,40);Check(a.Map.BaseID==700 && c.Map.ID==a.Map.ID,"last stage uses the smallest installed arena");
        LabNear(a,2315);Check(Kernel.GetDistance(a.Entity.X,a.Entity.Y,a.Map.Npcs[2315].X,a.Map.Npcs[2315].Y)<=6,"final arena clerk is reachable through production jumps");
        PkTick(e,40.2);EpkDamage(a,c,100);c.Entity.Die(a.Entity);
        Query("ALTER TABLE revora_pk_wars ADD CONSTRAINT pk_result_failure CHECK(winner=0) NOT VALID");PkTick(e,40.3);
        Check((bool)Field(run,"Finalizing") && !(bool)Field(run,"Done") && !HasAchievement(a,11118),"failed winner transaction freezes the run and rolls back the achievement");
        DateTime result=(DateTime)Field(run,"ResultTime");Query("ALTER TABLE revora_pk_wars DROP CONSTRAINT pk_result_failure");PkTick(e,61);
        EvidenceRefresh(a);Check((bool)Field(run,"Done") && HasAchievement(a,11118) && a.Entity.MapID==1002,"delayed finalization retains its captured pre-deadline result and exports the winner");
        Check(Scalar("SELECT count(*) FROM revora_pk_wars WHERE winner=@uid AND expires_at=ended_at+interval '7 days'",a.Entity.UID)==1,"weekly honor expires seven days from the result");
        Call(Pk,null,"SynchronizeHonors",a,result);Check(a.Entity.ContainsFlag(37) && !a.Entity.Titles.ContainsKey((TitlePacket.Titles)21),"verified weekly winner gets the halo flag without a cross-server title");
        Call(Pk,null,"SynchronizeHonors",a,result.AddDays(7));Check(!a.Entity.ContainsFlag(37),"weekly honor disappears at its exact expiry");
        SavePkProfileBeforeClaim(a);
        var filler=Enumerable.Range(0,37).Select(i=>Pack(a,1088001,1)).ToArray();PkNpc(a,3);
        Check(CtfCount("SELECT count(*) FROM revora_pk_claims")==0 && a.Inventory.Count==37,"full bag preserves the saved champion purse");
        foreach(var item in filler){Query("DELETE FROM items WHERE uid=@id","id",(long)item.UID);a.Inventory.Remove(item.UID,Enums.ItemUse.None,false);}
        ulong fullPurseBefore=a.Entity.Money;a.Entity.Money=Entity.MaximumSilver;PkNpc(a,3);
        Check(!Fenced(a) && CtfCount("SELECT count(*) FROM revora_pk_claims")==0 && a.Inventory.Count==0,
            "weekly champion purse waits when its silver would exceed durable storage");a.Entity.Money=fullPurseBefore;
        Query("ALTER TABLE revora_pk_claims ADD CONSTRAINT pk_claim_failure CHECK(cp=0) NOT VALID");uint cp=a.Entity.ConquerPoints,rvp=a.Entity.RevoraPoint;ulong gold=a.Entity.Money;PkNpc(a,3);
        Check(Fenced(a) && a.Inventory.Count==0 && a.Entity.ConquerPoints==cp && CtfCount("SELECT count(*) FROM revora_pk_claims")==0,"failed purse receipt rolls back currency and items together");
        Query("ALTER TABLE revora_pk_claims DROP CONSTRAINT pk_claim_failure");DemonReset(a);MoonNear(a,2314);
        Parallel.For(0,12,i=>Npcs.GetDialog(new NpcRequest(true){NpcID=2314,OptionID=3},a));
        Check(a.Entity.ConquerPoints==cp+100000 && a.Entity.RevoraPoint==rvp+1000 && a.Entity.Money==gold+1000000 && a.Inventory.Count==4,"concurrent prize claims install one weekly purse with immediate balances");
        Check(Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",a.Entity.UID)==a.Entity.ConquerPoints && Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",a.Entity.UID)==a.Entity.RevoraPoint
            && Scalar("SELECT money FROM entities WHERE uid=@uid",a.Entity.UID)==(long)a.Entity.Money,"all champion currencies are durable");
        Check(a.Inventory.Objects.Count(i=>i.ID==722136 && i.Bound)==3 && PkSoul(a) && CtfCount("SELECT count(*) FROM revora_pk_claims")==1,"one saved claim contains exactly three bound EXP Balls and a bound P7 soul");
        ReloadPkPrize(a);a=ReconnectPkPrize(a,false);
        // A fresh monthly window exercises distinct evidence, idle/noncompetitive results and restart recovery.
        pkStart=pkNow=new DateTime(2026,9,2,3,0,0,DateTimeKind.Utc);Call(Pk,e,"Tick");
        var m1=PkPlayer(town,129);var m2=PkPlayer(town,129);var solo=PkPlayer(town,119);var idle1=PkPlayer(town,99);var idle2=PkPlayer(town,99);
        var interrupted=PkPlayer(town,140);var rival=PkPlayer(town,140);
        Query("ALTER TABLE revora_pk_entries ADD CONSTRAINT pk_admission_failure CHECK(admitted_at IS NULL) NOT VALID");PkNpc(interrupted,2);
        Check(Fenced(interrupted) && interrupted.Entity.MapID!=1002 && Scalar("SELECT count(*) FROM revora_pk_entries WHERE uid=@uid AND admitted_at IS NOT NULL",interrupted.Entity.UID)==0,
            "failed admission after transfer blocks combat without fabricating admitted evidence");
        Query("ALTER TABLE revora_pk_entries DROP CONSTRAINT pk_admission_failure");DemonReset(interrupted);Call(Pk,null,"OnLogin",interrupted);PkNpc(interrupted,2);PkNpc(rival,2);
        Check(interrupted.Entity.MapID==rival.Entity.MapID && Scalar("SELECT count(*) FROM revora_pk_entries WHERE uid=@uid AND admitted_at IS NOT NULL",interrupted.Entity.UID)==1,
            "reconnect can retry an unadmitted reservation without a duplicate saved entry");
        foreach(var p in new[]{m1,m2,solo,idle1,idle2})PkNpc(p,2);PkTick(e,.2);EpkDamage(m1,m2,100);m2.Entity.Die(m1.Entity);PkTick(e,22);
        Check(interrupted.Entity.MapID==rival.Entity.MapID && interrupted.Entity.MapID!=1002 && HasAchievement(interrupted,11117),
            "stale cleanup from a failed reservation cannot evict its replacement admission");
        Call(Pk,null,"OnLogin",interrupted);PkTick(e,22.1);
        Check(interrupted.Entity.MapID==1002 && interrupted.Entity.OnDeath==null && Scalar("SELECT count(*) FROM revora_pk_wars WHERE winner=@uid",rival.Entity.UID)==0,
            "reconnect forfeits an admitted place and idle survival never becomes a victory");
        EvidenceRefresh(m1);Check(HasAchievement(m1,11117) && HasAchievement(m1,11119) && !HasAchievement(m1,11116),"monthly contested victory awards its own participation and winner identities");
        Check(!HasAchievement(solo,11117) && Scalar("SELECT count(*) FROM revora_pk_wars WHERE winner=@uid",solo.Entity.UID)==0,"uncontested admission earns neither participation nor a prize");
        var honors=(Dictionary<int,DateTime>)Call(PkStore,null,"Honors",m1.Entity.UID,pkNow);
        Check(honors[1]==pkStart.AddMinutes(22).AddDays(30),"monthly honor lasts exactly thirty days from victory");
        PkTick(e,60);Check(Scalar("SELECT count(*) FROM revora_pk_wars WHERE winner=@uid",idle1.Entity.UID)==0 && idle1.Entity.MapID==1002 && idle2.Entity.MapID==1002,"multiple survivors at deadline leave without a champion");
        Check((bool)Call(PkStore,null,"Open","pk-interrupted",1,3,pkStart.AddMonths(1)),"interrupted-run fixture reserves a future window");
        SavePkProfileBeforeClaim(m1);e=NewPk(runtime);PkNpc(m1,3);Check(m1.Inventory.Count==6 && PkSoul(m1) && m1.Entity.ConquerPoints==250000 && m1.Entity.RevoraPoint==2500 && m1.Entity.Money==5000000,"unclaimed monthly purse survives restart with its distinct five-ball reward");
        Check(Scalar("SELECT count(*) FROM revora_pk_legacy_status WHERE uid=@uid AND kind=20",a.Entity.UID)==1,"old compact status is archived without being reused as a native title");
        Check(CtfCount("SELECT count(*) FROM revora_pk_wars WHERE run='pk-interrupted' AND state='cancelled'")==1,"restart cancels unfinished runs without creating a champion");
        PkNpc(a,3);Check(CtfCount("SELECT count(*) FROM revora_pk_claims")==2 && a.Inventory.Count==4,"restart preserves prior claims and rejects reward replay");
        Call(Pk,null,"SynchronizeHonors",m1,pkStart.AddMinutes(22));Check(m1.Entity.ContainsFlag(36) && !m1.Entity.Titles.ContainsKey((TitlePacket.Titles)20),"saved monthly honor reconstructs after restart");
        m1=ReconnectPkPrize(m1,false);
        Pk.GetField("Current",Hidden).SetValue(null,null);
        VerifyLmsAtomicClaim(town,runtime);
        VerifyRelicAtomicClaims(town);
        VerifyWaveAtomicClaims(town);
        VerifyBeastAtomicClaims(town);
        VerifyBeastResults(town);
        VerifyBeastProgression(town);
        VerifyBeastRecovery(town);
        VerifyBeastLives(town);
    }
    static void VerifyBeastAtomicClaims(Map town) {
        var wave=typeof(Revora.Game.Features.BeastTrialSystem);const string key=Revora.Game.Features.BeastTrialSystem.ClaimKey;
        Call(wave,null,"InitializePrizes");
        Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.BeastAdmissions"),null,"Initialize");
        for(uint kind=1;kind<=2;kind++) {
            var p=PkPlayer(town,140);SavePkProfileBeforeClaim(p);EntityVariableTable.Load(p);p[key]=kind;EntityVariableTable.Save(p);
            uint cp=p.Entity.ConquerPoints,rp=p.Entity.RevoraPoint;ulong gold=p.Entity.Money;
            uint payCp=kind==1?Revora.Config.BeastTrialWinCp:Revora.Config.BeastTrialConsolationCp;
            uint payRp=kind==1?Revora.Config.BeastTrialWinRevoraPoints:Revora.Config.BeastTrialConsolationRevoraPoints;
            ulong payGold=kind==1?Revora.Config.BeastTrialWinGold:Revora.Config.BeastTrialConsolationGold;
            p.Entity.Money=Entity.MaximumSilver;Call(wave,null,"TryClaim",p);
            Check(!Fenced(p) && (uint)p[key]==kind && p.Inventory.Count==0,"Beast Trial overflowing purse preserves reward kind "+kind);p.Entity.Money=gold;
            foreach(string table in kind==1?new[]{"variablevault","items"}:new[]{"variablevault"}) {
                string action=table=="items"?"INSERT":"UPDATE";
                Query("CREATE FUNCTION beast_reject_claim() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER beast_reject_claim BEFORE "+action+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION beast_reject_claim()");
                try{Call(wave,null,"TryClaim",p);Check(Fenced(p),"Beast Trial failed "+table+" write fences kind "+kind);}
                finally{Query("DROP TRIGGER beast_reject_claim ON "+table+";DROP FUNCTION beast_reject_claim()");}
                p=ReloadBall(p);EntityVariableTable.Load(p);
                Check((uint)p[key]==kind && p.Inventory.Count==0 && p.Entity.ConquerPoints==cp && p.Entity.RevoraPoint==rp && p.Entity.Money==gold,
                    "Beast Trial kind "+kind+" rolls back its claim, item and currencies after failed "+table+" write");
            }
            var filler=Enumerable.Range(0,40).Select(i=>Pack(p,1088001,1)).ToArray();
            if(kind==1){Call(wave,null,"TryClaim",p);Check((uint)p[key]==1 && p.Entity.ConquerPoints==cp && !Fenced(p),"Beast Trial full victory bag leaves the prize pending");
                foreach(var item in filler){Query("DELETE FROM items WHERE uid=@id","id",(long)item.UID);p.Inventory.Remove(item.UID,Enums.ItemUse.None,false);}}
            var actor=p;Parallel.Invoke(()=>Call(wave,null,"TryClaim",actor),()=>Call(wave,null,"TryClaim",actor));
            Check((uint)p[key]==0 && p.Entity.ConquerPoints==cp+payCp && p.Entity.RevoraPoint==rp+payRp && p.Entity.Money==gold+payGold,
                "concurrent ordinary Beast Trial claims pay one exact purse for kind "+kind);
            if(kind==1)Check(p.Inventory.Count==1 && p.Inventory.Objects.Single().Bound && ConquerItemInformation.BaseInformations[p.Inventory.Objects.Single().ID].PurificationLevel==7,
                "eight-wave victory gives one bound P7 soul");
            else Check(p.Inventory.Count==40 && p.Inventory.Objects.All(i=>i.ID==1088001),"partial-wave consolation pays with a full bag and awards no soul");
            var ids=p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i).ToArray();
            p=ReloadBall(p);EntityVariableTable.Load(p);Call(wave,null,"TryClaim",p);
            Check((uint)p[key]==0 && p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i).SequenceEqual(ids)
                && p.Entity.ConquerPoints==cp+payCp && p.Entity.RevoraPoint==rp+payRp && p.Entity.Money==gold+payGold,
                "Beast Trial exact items and currencies survive fresh reload and claim replay for kind "+kind);
            if(kind==2){foreach(var item in p.Inventory.Objects.ToArray()){Query("DELETE FROM items WHERE uid=@id","id",(long)item.UID);p.Inventory.Remove(item.UID,Enums.ItemUse.None,false);}}
            p[key]=kind;Call(wave,null,"TryClaim",p);
            Check(Fenced(p) && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==cp+payCp,
                "stale Beast Trial kind "+kind+" cannot recreate a consumed prize");
        }
        var invalid=PkPlayer(town,140);SavePkProfileBeforeClaim(invalid);EntityVariableTable.Load(invalid);invalid[key]=3u;EntityVariableTable.Save(invalid);
        Call(wave,null,"TryClaim",invalid);Check(!Fenced(invalid) && invalid.Inventory.Count==0 && (uint)invalid[key]==3,
            "invalid Beast Trial claim cannot fall through to the victory reward");
    }
    static void VerifyBeastLives(Map town) {
        var beast=typeof(Revora.Game.Features.BeastTrialSystem);var live=(IList)beast.GetField("Live",Hidden).GetValue(null);
        var register=beast.GetField("RegisterOpen",Hidden);object prior=register.GetValue(null);register.SetValue(null,true);
        try {
            var p=PkPlayer(town,140);p.Entity.X=408;p.Entity.Y=330;p.Entity.PKMode=Enums.PKMode.Capture;SavePkProfileBeforeClaim(p);EntityVariableTable.Load(p);
            Call(beast,null,"TryRegister",p);Check(live.Count==1,"ordinary solo enters Beast life-loss journey");
            var inst=live[0];var type=inst.GetType();var arena=(Map)type.GetField("Arena").GetValue(inst);
            var enemy=arena.Entities.Values.First(e=>e.EntityFlag==EntityFlag.Monster);
            long rewards=CtfCount("SELECT count(*) FROM revora_beast_results WHERE kind<>0");
            for(int death=1;death<=3;death++) {
                if(death%2==1)p.Entity.Die(enemy);else p.Entity.Die(enemy.UID);
                Check((int)type.GetField("LivesLeft").GetValue(inst)==3-death,"actual Beast death "+death+" consumes exactly one life even immediately after revival");
                if(death<3){
                    Check(!p.Entity.Dead && p.Entity.Hitpoints==p.Entity.MaxHitpoints/2 && p.Entity.MapID==arena.ID,
                        "Beast defender revives at half health while lives remain");
                    Revora.Game.Features.BeastTrialSystem.OnDeath(p);
                    Check((int)type.GetField("LivesLeft").GetValue(inst)==3-death,"duplicate Beast death callback after revival cannot spend another life");
                }
            }
            Check(live.Count==0 && (bool)type.GetField("Failed").GetValue(inst) && !Kernel.Maps.ContainsKey(arena.ID)
                && p.Entity.MapID==1002 && p.Entity.PKMode==Enums.PKMode.Capture,"third Beast death restores combat mode and releases the arena after saved return");
            p=ReloadBall(p);EntityVariableTable.Load(p);
            Check(p.Entity.MapID==1002
                && Scalar("SELECT count(*) FROM revora_beast_admissions WHERE uid=@uid AND ended AND returned",p.Entity.UID)==1,
                "Beast final life-loss town return and acknowledgement survive fresh reload");
            Check(CtfCount("SELECT count(*) FROM revora_beast_results WHERE kind<>0")==rewards,"first-wave Beast life exhaustion awards no prize");
        } finally {register.SetValue(null,prior);}
    }
    static void VerifyBeastRecovery(Map town) {
        var beast=typeof(Revora.Game.Features.BeastTrialSystem);var admission=typeof(Entity).Assembly.GetType("Revora.Game.Features.BeastAdmissions");
        var live=(IList)beast.GetField("Live",Hidden).GetValue(null);var maps=(IDictionary)beast.GetField("ByMap",Hidden).GetValue(null);
        var register=beast.GetField("RegisterOpen",Hidden);object prior=register.GetValue(null);register.SetValue(null,true);
        try {
            var p=PkPlayer(town,140);p.Entity.X=408;p.Entity.Y=330;p.Entity.PKMode=Enums.PKMode.Capture;
            SavePkProfileBeforeClaim(p);EntityVariableTable.Load(p);
            Query("CREATE FUNCTION beast_reject_admission() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER beast_reject_admission BEFORE INSERT ON revora_beast_admissions FOR EACH ROW EXECUTE FUNCTION beast_reject_admission()");
            try{Call(beast,null,"TryRegister",p);}finally{Query("DROP TRIGGER beast_reject_admission ON revora_beast_admissions;DROP FUNCTION beast_reject_admission()");}
            Check(live.Count==0 && p.Entity.MapID==1002 && Scalar("SELECT count(*) FROM revora_beast_admissions WHERE uid=@uid",p.Entity.UID)==0,
                "failed Beast admission saves no reservation and transfers nobody");
            Call(beast,null,"TryRegister",p);var inst=live[0];var type=inst.GetType();var arena=(Map)type.GetField("Arena").GetValue(inst);
            Check(p.Entity.MapID==arena.ID && Scalar("SELECT count(*) FROM revora_beast_admissions WHERE uid=@uid AND NOT ended AND NOT returned",p.Entity.UID)==1,
                "ordinary Beast entry reserves original combat mode before transfer");
            p.Entity.PKMode=Enums.PKMode.PK;
            Query("CREATE FUNCTION beast_reject_return() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.returned THEN RETURN NULL; END IF; RETURN NEW; END $$; CREATE TRIGGER beast_reject_return BEFORE UPDATE ON revora_beast_admissions FOR EACH ROW EXECUTE FUNCTION beast_reject_return()");
            try{Call(beast,null,"Fail",inst,"fixture loss");}finally{Query("DROP TRIGGER beast_reject_return ON revora_beast_admissions;DROP FUNCTION beast_reject_return()");}
            Check(Fenced(p) && p.Entity.MapID==1002 && live.Count==1 && Kernel.Maps.ContainsKey(arena.ID)
                && Scalar("SELECT count(*) FROM revora_beast_admissions WHERE uid=@uid AND ended AND NOT returned",p.Entity.UID)==1,
                "failed Beast return acknowledgement retains its arena and saved retry");
            p=ReloadBall(p);EntityVariableTable.Load(p);Revora.Game.Features.BeastTrialSystem.OnLogin(p);Call(beast,null,"TickInstance",inst,DateTime.Now);
            Check(!Fenced(p) && p.Entity.MapID==1002 && p.Entity.PKMode==Enums.PKMode.Capture && live.Count==0
                && !Kernel.Maps.ContainsKey(arena.ID) && !Revora.Constants.PKFreeMaps.Contains(arena.ID) && arena.Entities.Count==0,
                "Beast return reload restores mode and releases its owned arena only after acknowledgement");
            var q=PkPlayer(town,140);q.Entity.X=408;q.Entity.Y=330;q.Entity.PKMode=Enums.PKMode.Capture;SavePkProfileBeforeClaim(q);EntityVariableTable.Load(q);
            Call(beast,null,"TryRegister",q);inst=live[0];arena=(Map)type.GetField("Arena").GetValue(inst);EntityTable.SaveEntity(q);
            long results=CtfCount("SELECT count(*) FROM revora_beast_results");
            live.Clear();maps.Clear();Call(admission,null,"RecoverInterrupted");
            q=ReloadBall(q);EntityVariableTable.Load(q);Revora.Game.Features.BeastTrialSystem.OnLogin(q);
            Check(!Fenced(q) && q.Entity.MapID==1002 && q.Entity.PKMode==Enums.PKMode.Capture
                && Scalar("SELECT count(*) FROM revora_beast_admissions WHERE uid=@uid AND ended AND returned",q.Entity.UID)==1,
                "interrupted Beast admission returns a freshly loaded ordinary player to town");
            Check(CtfCount("SELECT count(*) FROM revora_beast_results")==results,"interrupted Beast recovery does not invent a result or prize");
            arena.Dispose();Revora.Constants.PKFreeMaps.Remove(arena.ID);
        } finally {register.SetValue(null,prior);}
    }
    static void VerifyBeastProgression(Map town) {
        var beast=typeof(Revora.Game.Features.BeastTrialSystem);
        var live=(IList)beast.GetField("Live",Hidden).GetValue(null);
        var register=beast.GetField("RegisterOpen",Hidden);object prior=register.GetValue(null);register.SetValue(null,true);
        try {
            for(int scenario=0;scenario<4;scenario++) {
                var p=PkPlayer(town,140);SavePkProfileBeforeClaim(p);EntityVariableTable.Load(p);
                p.Entity.X=408;p.Entity.Y=330;uint cp=p.Entity.ConquerPoints;
                Call(beast,null,"TryRegister",p);Check(live.Count==1,"ordinary solo enters Beast progression scenario "+scenario);
                var inst=live[0];var type=inst.GetType();var arena=(Map)type.GetField("Arena").GetValue(inst);
                try {
                    if(scenario==0) {
                        for(int n=1;n<=8;n++) {
                            var mobs=arena.Entities.Values.Where(e=>e.EntityFlag==EntityFlag.Monster && !e.Dead).ToArray();
                            int count=n==8?8:n>=5?5+n:3+n;if(n==4 || n==8)count++;
                            Check((int)type.GetField("Wave").GetValue(inst)==n && mobs.Length==count,"Beast wave "+n+" spawns its exact regular/boss count");
                            if(n==4 || n==8)Check(mobs.Any(e=>e.MonsterInfo.ID==(n==4?8306u:31u) && e.MaxHitpoints==(n==4?15000u:35000u)),"Beast boss wave "+n+" retains its configured HP");
                            Call(beast,null,"TickInstance",inst,DateTime.Now);
                            Check((int)type.GetField("Wave").GetValue(inst)==n && !(bool)type.GetField("Won").GetValue(inst),"living enemies prevent Beast wave "+n+" completion");
                            foreach(var e in mobs)e.Hitpoints=0;
                            Call(beast,null,"TickInstance",inst,DateTime.Now);
                        }
                        Check(live.Count==0 && p.Entity.MapID==1002 && (bool)type.GetField("Won").GetValue(inst),"eight cleared Beast waves save victory and return the solo defender");
                        Call(beast,null,"TryClaim",p);var soul=p.Inventory.Objects.Single();uint uid=soul.UID;
                        p=ReloadBall(p);EntityVariableTable.Load(p);Call(beast,null,"TryClaim",p);
                        Check(p.Inventory.Count==1 && p.Inventory.Objects.Single().UID==uid && soul.Bound
                            && ConquerItemInformation.BaseInformations[soul.ID].PurificationLevel==7 && p.Entity.ConquerPoints==cp+Revora.Config.BeastTrialWinCp,
                            "earned Beast eight-wave prize survives reload as one exact bound P7 soul and purse without replay");
                    } else {
                        uint missing=scenario==1?2u:scenario==2?8306u:31u;int number=scenario==1?1:scenario==2?4:8;
                        var definition=MonsterInformation.MonsterInformations[missing];long results=CtfCount("SELECT count(*) FROM revora_beast_results");
                        MonsterInformation.MonsterInformations.Remove(missing);
                        try{Call(beast,null,"StartWave",inst,number);}finally{MonsterInformation.MonsterInformations[missing]=definition;}
                        Check((bool)type.GetField("Abort").GetValue(inst),"missing Beast catalog "+missing+" aborts the encounter");
                        Call(beast,null,"Win",inst);Call(beast,null,"TickInstance",inst,DateTime.Now);
                        Check(live.Count==0 && p.Entity.MapID==1002 && CtfCount("SELECT count(*) FROM revora_beast_results")==results,
                            "missing Beast catalog "+missing+" cannot become a victory or consolation");
                    }
                } finally {arena.Dispose();Revora.Constants.PKFreeMaps.Remove(arena.ID);}
            }
        } finally {register.SetValue(null,prior);}
    }
    static object BeastResultFixture(Type beast,GameState player,int number) {
        var type=beast.GetNestedType("Instance",BindingFlags.NonPublic);var inst=Activator.CreateInstance(type,true);
        type.GetField("Player").SetValue(inst,player);type.GetField("PlayerUid").SetValue(inst,player.Entity.UID);
        type.GetField("Wave").SetValue(inst,number);return inst;
    }
    static void VerifyBeastResults(Map town) {
        var beast=typeof(Revora.Game.Features.BeastTrialSystem);const string key=Revora.Game.Features.BeastTrialSystem.ClaimKey;
        var p=PkPlayer(town,140);SavePkProfileBeforeClaim(p);EntityVariableTable.Load(p);p[key]=2u;EntityVariableTable.Save(p);
        uint cp=p.Entity.ConquerPoints;var inst=BeastResultFixture(beast,p,8);var type=inst.GetType();
        Query("CREATE FUNCTION beast_reject_result() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER beast_reject_result BEFORE INSERT ON revora_beast_results FOR EACH ROW EXECUTE FUNCTION beast_reject_result()");
        try{Call(beast,null,"Win",inst);}finally{Query("DROP TRIGGER beast_reject_result ON revora_beast_results;DROP FUNCTION beast_reject_result()");}
        Check(!(bool)type.GetField("Won").GetValue(inst) && (int)type.GetField("PendingKind").GetValue(inst)==1
            && Scalar("SELECT count(*) FROM revora_beast_results WHERE uid=@uid",p.Entity.UID)==0,"Beast result write failure retains its selected outcome without completing");
        GameState removed;Kernel.GamePool.TryRemove(p.Entity.UID,out removed);type.GetField("Player").SetValue(inst,null);type.GetField("Wave").SetValue(inst,1);
        Call(beast,null,"TickInstance",inst,DateTime.Now);Call(beast,null,"Fail",inst,"late failure");
        Check((bool)type.GetField("Won").GetValue(inst) && Scalar("SELECT count(*) FROM revora_beast_results WHERE uid=@uid AND kind=1",p.Entity.UID)==1,
            "Beast tick saves the frozen victory for its offline owner despite changed wave state");
        p=ReloadBall(p);EntityVariableTable.Load(p);
        Check((uint)p[key]==2,"Beast result does not overwrite an older pending consolation");
        var second=BeastResultFixture(beast,p,8);Call(beast,null,"Win",second);Call(beast,null,"Win",second);
        Check(Scalar("SELECT count(*) FROM revora_beast_results WHERE uid=@uid AND kind=1",p.Entity.UID)==2,"two Beast wins queue separate prizes while repeated completion is idempotent");
        Call(beast,null,"TryClaim",p);
        Check(p.Inventory.Count==0 && p.Entity.ConquerPoints==cp+Revora.Config.BeastTrialConsolationCp,"Beast legacy consolation pays before queued victories");
        Query("CREATE FUNCTION beast_reject_import() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER beast_reject_import BEFORE UPDATE ON revora_beast_results FOR EACH ROW EXECUTE FUNCTION beast_reject_import()");
        try{Call(beast,null,"HasClaim",p);Check(Fenced(p),"Beast queued import failure requires a reload");}
        finally{Query("DROP TRIGGER beast_reject_import ON revora_beast_results;DROP FUNCTION beast_reject_import()");}
        p=ReloadBall(p);EntityVariableTable.Load(p);
        Check((uint)p[key]==0 && Scalar("SELECT count(*) FROM revora_beast_results WHERE uid=@uid AND NOT imported",p.Entity.UID)==2,
            "failed Beast import retains both receipts and leaves no partial claim");
        for(int n=0;n<2;n++){Call(beast,null,"TryClaim",p);p=ReloadBall(p);EntityVariableTable.Load(p);}Call(beast,null,"TryClaim",p);
        Check(p.Inventory.Count==2 && p.Inventory.Objects.All(i=>i.Bound && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)
            && p.Entity.ConquerPoints==cp+Revora.Config.BeastTrialConsolationCp+2*Revora.Config.BeastTrialWinCp,
            "two queued Beast victories preserve exact bound souls and purses through reload without replay");
        var early=BeastResultFixture(beast,p,3);Call(beast,null,"Fail",early,"loss");
        var partial=BeastResultFixture(beast,p,4);Call(beast,null,"Fail",partial,"loss");
        Check(Scalar("SELECT count(*) FROM revora_beast_results WHERE uid=@uid AND kind=0",p.Entity.UID)==1
            && Scalar("SELECT count(*) FROM revora_beast_results WHERE uid=@uid AND kind=2",p.Entity.UID)==1,"Beast results distinguish early failure from fourth-wave consolation");
    }
    static void VerifyWaveAtomicClaims(Map town) {
        var wave=typeof(Revora.Game.Features.WaveDefenseSystem);const string key=Revora.Game.Features.WaveDefenseSystem.ClaimKey;
        Call(wave,null,"InitializePrizes");
        Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.WaveAdmissions"),null,"Initialize");
        for(uint kind=1;kind<=2;kind++) {
            var p=PkPlayer(town,140);SavePkProfileBeforeClaim(p);EntityVariableTable.Load(p);p[key]=kind;EntityVariableTable.Save(p);
            uint cp=p.Entity.ConquerPoints,rp=p.Entity.RevoraPoint;ulong gold=p.Entity.Money;
            uint payCp=kind==1?Revora.Config.WaveDefenseWinCp:Revora.Config.WaveDefenseConsolationCp;
            uint payRp=kind==1?Revora.Config.WaveDefenseWinRevoraPoints:Revora.Config.WaveDefenseConsolationRevoraPoints;
            ulong payGold=kind==1?Revora.Config.WaveDefenseWinGold:Revora.Config.WaveDefenseConsolationGold;
            p.Entity.Money=Entity.MaximumSilver;Call(wave,null,"TryClaim",p);
            Check(!Fenced(p) && (uint)p[key]==kind && p.Inventory.Count==0,"Wave Defense overflowing purse preserves reward kind "+kind);p.Entity.Money=gold;
            foreach(string table in kind==1?new[]{"variablevault","items"}:new[]{"variablevault"}) {
                string action=table=="items"?"INSERT":"UPDATE";
                Query("CREATE FUNCTION wave_reject_claim() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER wave_reject_claim BEFORE "+action+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION wave_reject_claim()");
                try{Call(wave,null,"TryClaim",p);Check(Fenced(p),"Wave Defense failed "+table+" write fences kind "+kind);}
                finally{Query("DROP TRIGGER wave_reject_claim ON "+table+";DROP FUNCTION wave_reject_claim()");}
                p=ReloadBall(p);EntityVariableTable.Load(p);
                Check((uint)p[key]==kind && p.Inventory.Count==0 && p.Entity.ConquerPoints==cp && p.Entity.RevoraPoint==rp && p.Entity.Money==gold,
                    "Wave Defense kind "+kind+" rolls back its claim, item and currencies after failed "+table+" write");
            }
            var filler=Enumerable.Range(0,40).Select(i=>Pack(p,1088001,1)).ToArray();
            if(kind==1){Call(wave,null,"TryClaim",p);Check((uint)p[key]==1 && p.Entity.ConquerPoints==cp && !Fenced(p),"Wave Defense full victory bag leaves the prize pending");
                foreach(var item in filler){Query("DELETE FROM items WHERE uid=@id","id",(long)item.UID);p.Inventory.Remove(item.UID,Enums.ItemUse.None,false);}}
            var actor=p;Parallel.Invoke(()=>Call(wave,null,"TryClaim",actor),()=>Call(wave,null,"TryClaim",actor));
            Check((uint)p[key]==0 && p.Entity.ConquerPoints==cp+payCp && p.Entity.RevoraPoint==rp+payRp && p.Entity.Money==gold+payGold,
                "concurrent ordinary Wave Defense claims pay one exact purse for kind "+kind);
            if(kind==1)Check(p.Inventory.Count==1 && p.Inventory.Objects.Single().Bound && ConquerItemInformation.BaseInformations[p.Inventory.Objects.Single().ID].PurificationLevel==7,
                "eight-wave victory gives one bound P7 soul");
            else Check(p.Inventory.Count==40 && p.Inventory.Objects.All(i=>i.ID==1088001),"partial-wave consolation pays with a full bag and awards no soul");
            var ids=p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i).ToArray();
            p=ReloadBall(p);EntityVariableTable.Load(p);Call(wave,null,"TryClaim",p);
            Check((uint)p[key]==0 && p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i).SequenceEqual(ids)
                && p.Entity.ConquerPoints==cp+payCp && p.Entity.RevoraPoint==rp+payRp && p.Entity.Money==gold+payGold,
                "Wave Defense exact items and currencies survive fresh reload and claim replay for kind "+kind);
            if(kind==2){foreach(var item in p.Inventory.Objects.ToArray()){Query("DELETE FROM items WHERE uid=@id","id",(long)item.UID);p.Inventory.Remove(item.UID,Enums.ItemUse.None,false);}}
            p[key]=kind;Call(wave,null,"TryClaim",p);
            Check(Fenced(p) && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==cp+payCp,
                "stale Wave Defense kind "+kind+" cannot recreate a consumed prize");
        }
        var invalid=PkPlayer(town,140);SavePkProfileBeforeClaim(invalid);EntityVariableTable.Load(invalid);invalid[key]=3u;EntityVariableTable.Save(invalid);
        Call(wave,null,"TryClaim",invalid);Check(!Fenced(invalid) && invalid.Inventory.Count==0 && (uint)invalid[key]==3,
            "invalid Wave Defense claim cannot fall through to the victory reward");
        VerifyWaveResults(town,wave);
        VerifyWaveAdmissions(town,wave);
    }
    static void VerifyWaveAdmissions(Map town,Type wave) {
        Query("CREATE TABLE IF NOT EXISTS npcs (LIKE public.npcs INCLUDING ALL); CREATE TABLE IF NOT EXISTS sobnpcs (LIKE public.sobnpcs INCLUDING ALL); CREATE TABLE IF NOT EXISTS furniture (LIKE public.furniture INCLUDING ALL); CREATE TABLE IF NOT EXISTS monsterspawns (LIKE public.monsterspawns INCLUDING ALL)");
        foreach(ushort mapId in new ushort[]{700,1005,1002})if(Kernel.Maps.ContainsKey(mapId)){
            DMaps.MapPaths[mapId]=System.IO.Path.Combine(Environment.CurrentDirectory,"database/maps/"+mapId+".map");break;}
        var admissions=typeof(Entity).Assembly.GetType("Revora.Game.Features.WaveAdmissions");
        var players=Enumerable.Range(0,4).Select(i=>PkPlayer(town,140)).ToArray();
        foreach(var p in players){p.Entity.X=408;p.Entity.Y=330;p.Entity.PKMode=Enums.PKMode.Capture;SavePkProfileBeforeClaim(p);EntityVariableTable.Load(p);}
        var leader=players[0];leader.Team=new Revora.Game.ConquerStructures.Team(leader);foreach(var p in players.Skip(1))leader.Team.Add(p);
        var open=wave.GetField("RegisterOpen",Hidden);object prior=open.GetValue(null);open.SetValue(null,true);
        var live=(IList)wave.GetField("Live",Hidden).GetValue(null);var maps=(IDictionary)wave.GetField("ByMap",Hidden).GetValue(null);
        var registered=(HashSet<uint>)wave.GetField("RegisteredLeaders",Hidden).GetValue(null);
        try {
            Query("CREATE FUNCTION wave_reject_admission() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER wave_reject_admission BEFORE INSERT ON revora_wave_admissions FOR EACH ROW EXECUTE FUNCTION wave_reject_admission()");
            try{Call(wave,null,"TryRegister",leader);}finally{Query("DROP TRIGGER wave_reject_admission ON revora_wave_admissions;DROP FUNCTION wave_reject_admission()");}
            Check(players.All(p=>p.Entity.MapID==1002) && live.Count==0 && CtfCount("SELECT count(*) FROM revora_wave_admissions")==0,
                "Wave Defense failed team reservation transfers nobody and leaves no partial admission");
            using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
                var busy=players[1];var task=Task.Run(()=>{lock(busy.ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
                try{Call(wave,null,"TryRegister",leader);Check(live.Count==0 && CtfCount("SELECT count(*) FROM revora_wave_admissions")==0,
                    "busy Wave Defense teammate defers registration without a partial transfer");}finally{release.Set();task.Wait();}
            }
            Call(wave,null,"TryRegister",leader);
            Check(live.Count==1 && registered.Contains(leader.Entity.UID) && players.All(p=>p.Entity.MapID!=1002)
                && CtfCount("SELECT count(*) FROM revora_wave_admissions WHERE NOT ended AND pkmode=3")==4,
                "ordinary Wave Defense team enters only after all four original combat modes are reserved");
            var inst=live[0];var instanceType=inst.GetType();var arena=(Map)instanceType.GetField("Arena").GetValue(inst);
            foreach(var p in players)SavePkProfileBeforeClaim(p);
            Revora.Game.Features.WaveDefenseSystem.OnDisconnect(leader);GameState removed;Kernel.GamePool.TryRemove(leader.Entity.UID,out removed);
            Call(wave,null,"ProcessReturns");
            leader=ReloadBall(leader);Revora.Game.Features.WaveDefenseSystem.OnLogin(leader);players[0]=leader;
            Check(!Fenced(leader) && leader.Entity.MapID==1002 && leader.Entity.PKMode==Enums.PKMode.Capture
                && Scalar("SELECT count(*) FROM revora_wave_admissions WHERE uid=@uid AND ended AND returned",leader.Entity.UID)==1,
                "disconnected Wave Defense leader reconnects to a saved safe return with original combat mode");
            Call(admissions,null,"RecoverInterrupted");Call(admissions,null,"RecoverInterrupted");
            live.Clear();maps.Remove(arena.ID);registered.Clear();
            Query("CREATE FUNCTION wave_reject_return() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.returned THEN RETURN NULL; END IF; RETURN NEW; END $$; CREATE TRIGGER wave_reject_return BEFORE UPDATE ON revora_wave_admissions FOR EACH ROW EXECUTE FUNCTION wave_reject_return()");
            try{players[1]=ReloadBall(players[1]);Revora.Game.Features.WaveDefenseSystem.OnLogin(players[1]);}
            finally{Query("DROP TRIGGER wave_reject_return ON revora_wave_admissions;DROP FUNCTION wave_reject_return()");}
            Check(Fenced(players[1]) && Scalar("SELECT mapid FROM entities WHERE uid=@uid",players[1].Entity.UID)==1002
                && Scalar("SELECT count(*) FROM revora_wave_admissions WHERE uid=@uid AND ended AND NOT returned",players[1].Entity.UID)==1,
                "failed Wave Defense return acknowledgement preserves retry after saving town destination");
            for(int i=1;i<players.Length;i++){players[i]=ReloadBall(players[i]);Revora.Game.Features.WaveDefenseSystem.OnLogin(players[i]);}
            Check(players.All(p=>!Fenced(p) && p.Entity.MapID==1002 && p.Entity.PKMode==Enums.PKMode.Capture)
                && CtfCount("SELECT count(*) FROM revora_wave_admissions WHERE ended AND returned")==4,
                "interrupted Wave Defense reservations return all ordinary teammates safely across fresh reloads");
            Guid run=(Guid)instanceType.GetField("ResultId").GetValue(inst);
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT count(*) FROM revora_wave_results WHERE run=@run",c)){q.Parameters.AddWithValue("run",run);Check((long)q.ExecuteScalar()==0,"interrupted Wave Defense return creates no invented team reward");}}
            leader.Team=new Revora.Game.ConquerStructures.Team(leader);foreach(var p in players.Skip(1))leader.Team.Add(p);
            Call(wave,null,"TryRegister",leader);Check(live.Count==1,"returned Wave Defense team can enter a later instance");
            inst=live[0];instanceType.GetField("Wave").SetValue(inst,8);
            Revora.Game.Features.WaveDefenseSystem.OnDisconnect(leader);
            Query("CREATE FUNCTION wave_reject_end() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.ended THEN RETURN NULL; END IF; RETURN NEW; END $$; CREATE TRIGGER wave_reject_end BEFORE UPDATE ON revora_wave_admissions FOR EACH ROW EXECUTE FUNCTION wave_reject_end()");
            try{Call(wave,null,"Win",inst);Check((bool)instanceType.GetField("Won").GetValue(inst) && live.Count==1,
                "Wave Defense saved victory remains retained when ending reservations fails");}
            finally{Query("DROP TRIGGER wave_reject_end ON revora_wave_admissions;DROP FUNCTION wave_reject_end()");}
            arena=(Map)instanceType.GetField("Arena").GetValue(inst);
            using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
                var busy=players[1];var task=Task.Run(()=>{lock(busy.ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
                try{Call(wave,null,"TickInstance",inst,DateTime.Now);Check(live.Count==1 && Kernel.Maps.ContainsKey(arena.ID) && busy.Entity.MapID==arena.ID,
                    "Wave Defense retains an occupied arena while a busy player's durable return is pending");}finally{release.Set();task.Wait();}
            }
            Call(wave,null,"TickInstance",inst,DateTime.Now);
            Check(live.Count==0 && !registered.Contains(leader.Entity.UID) && players.All(p=>p.Entity.MapID==1002 && p.Entity.PKMode==Enums.PKMode.Capture),
                "Wave Defense close retry returns the whole roster and releases registration after its original leader departs");
            Check(!Kernel.Maps.ContainsKey(arena.ID) && !Revora.Constants.PKFreeMaps.Contains(arena.ID)
                && arena.Entities.Count==0 && arena.Npcs.Count==0 && arena.Companions.Count==0,
                "Wave Defense acknowledged returns release its owned arena, NPCs, monsters and PK-free registration");
            for(int size=2;size<=3;size++) {
                if(Revora.Thread.GenericThreadPool==null)Revora.Thread.GenericThreadPool=new System.Threading.StaticPool(1);
                Revora.Pet.CreateTimerFactories();Screen.CreateTimerFactories();
                foreach(var p in players.Skip(size))if(p.Team!=null)p.Team.Remove(p);
                foreach(var p in players.Skip(1).Take(size-1))if(p.Team==null)leader.Team.Add(p);
                Call(wave,null,"TryRegister",leader);Check(live.Count==1,"ordinary Wave Defense team of "+size+" enters its ally-backed field");
                inst=live[0];arena=(Map)instanceType.GetField("Arena").GetValue(inst);
                var allies=((List<GameState>)instanceType.GetField("Allies").GetValue(inst)).ToArray();
                Check(allies.Length==4-size && allies.All(p=>p.Fake && p.Entity.MapID==arena.ID && Kernel.GamePool.ContainsKey(p.Entity.UID)),
                    "Wave Defense team of "+size+" gets exactly its missing companion slots");
                VerifyWaveMonsterTargets(wave,inst,arena,allies);
                VerifyWaveCompanionCombat(wave,inst,arena,allies);
                VerifyWaveAutonomousPressure(wave,inst,arena,allies);
                Call(wave,null,"Fail",inst,"fixture cancellation");
                Check(live.Count==0 && !Kernel.Maps.ContainsKey(arena.ID) && allies.All(p=>!Kernel.GamePool.ContainsKey(p.Entity.UID)),
                    "Wave Defense cancellation removes only its owned companions and arena for team size "+size);
                Check(allies.All(p=>!p.Socket.Alive
                    && ((IDisposable[])typeof(Screen).GetField("TimerSubscriptions",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(p.Screen)).All(t=>t==null)
                    && ((IDisposable[])typeof(Revora.Pet).GetField("TimerSubscriptions",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(p.Pet)).All(t=>t==null)),
                    "Wave Defense releases all companion screen and pet timer subscriptions for team size "+size);
            }
            VerifyWaveLives(town,wave,live);
            VerifyWaveProgression(town,wave,live);
        } finally {open.SetValue(null,prior);}
    }
    static void VerifyWaveLives(Map town,Type wave,IList live) {
        var players=Enumerable.Range(0,4).Select(i=>PkPlayer(town,140)).ToArray();
        foreach(var p in players){p.Entity.X=408;p.Entity.Y=330;SavePkProfileBeforeClaim(p);EntityVariableTable.Load(p);}
        players[0].Team=new Revora.Game.ConquerStructures.Team(players[0]);foreach(var p in players.Skip(1))players[0].Team.Add(p);
        Call(wave,null,"TryRegister",players[0]);Check(live.Count==1,"ordinary team enters Wave life-loss journey");
        var inst=live[0];var type=inst.GetType();var arena=(Map)type.GetField("Arena").GetValue(inst);
        var enemy=arena.Entities.Values.First(e=>!Revora.Game.Features.WaveDefenseSystem.IsBanner(e));
        long rewards=CtfCount("SELECT count(*) FROM revora_wave_results WHERE kind<>0");
        for(int death=1;death<=4;death++) {
            var player=players[(death-1)%players.Length];
            if(death%2==1)player.Entity.Die(enemy);else player.Entity.Die(enemy.UID);
            Check((int)type.GetField("Lives").GetValue(inst)==4-death,"actual Wave player death "+death+" consumes exactly one shared life");
            if(death<4){
                Check(!player.Entity.Dead && player.Entity.Hitpoints==player.Entity.MaxHitpoints/2 && player.Entity.MapID==arena.ID,
                    "Wave defeat revives defender at half health while lives remain");
                Revora.Game.Features.WaveDefenseSystem.OnDeath(player);
                Check((int)type.GetField("Lives").GetValue(inst)==4-death,"duplicate Wave death callback does not consume another life after revival");
            }
        }
        Check(live.Count==0 && (bool)type.GetField("Failed").GetValue(inst) && players.All(p=>p.Entity.MapID==1002),
            "fourth player death ends Wave Defense and returns the whole team");
        Check(CtfCount("SELECT count(*) FROM revora_wave_results WHERE kind<>0")==rewards,"first-wave life exhaustion creates no reward entitlement");
    }
    static void VerifyWaveAutonomousPressure(Type wave,object inst,Map arena,GameState[] allies) {
        Call(wave,null,"StartWave",inst,1);
        var type=inst.GetType();var owned=(Dictionary<uint,uint>)type.GetField("MonsterPost").GetValue(inst);
        var enemy=arena.Entities.Values.First(e=>owned.ContainsKey(e.UID) && !e.Dead);
        foreach(var e in arena.Entities.Values.Where(e=>owned.ContainsKey(e.UID) && e!=enemy))e.Hitpoints=0;
        foreach(var ally in allies)ally.Entity.Hitpoints=0;
        foreach(var p in (List<GameState>)type.GetField("Players").GetValue(inst)){p.Entity.X=1;p.Entity.Y=1;}
        ushort x=0,y=0;
        for(int ty=3;ty<arena.Floor.Bounds.Height-2 && x==0;ty++)
            for(int tx=30;tx<arena.Floor.Bounds.Width-26;tx++)
                if(Enumerable.Range(0,26).All(n=>Enumerable.Range(-1,3).All(d=>arena.Floor[tx+n,ty+d,MapObjectType.Monster,null])))
                    {x=(ushort)tx;y=(ushort)ty;break;}
        Check(x>0,"autonomous Wave fixture has a clear lane beyond all defenders' screens");
        var post=arena.Entities.Values.First(e=>e.UID==owned[enemy.UID]);
        enemy.X=x;enemy.Y=y;enemy.MonsterInfo.AttackRange=1;
        post.X=(ushort)(x+25);post.Y=y;post.Hitpoints=post.MaxHitpoints=1000000;
        arena.Floor[x+5,y,MapObjectType.InvalidCast,null]=false;
        DateTime now=DateTime.Now;
        type.GetField("NextMedic").SetValue(inst,now.AddHours(1));type.GetField("FightUntil").SetValue(inst,now.AddHours(1));
        try {
            Call(wave,null,"TickInstance",inst,now);
            Check(enemy.X!=x || enemy.Y!=y,"event tick moves wave monsters without nearby screen callbacks");
            ushort movedX=enemy.X,movedY=enemy.Y;
            Call(wave,null,"TickInstance",inst,now);
            Check(enemy.X==movedX && enemy.Y==movedY,"duplicate event tick cannot accelerate monster movement");
            bool legal=true;
            for(int n=1;n<=80 && post.Hitpoints==post.MaxHitpoints;n++){
                Call(wave,null,"TickInstance",inst,now.AddSeconds(n));
                legal &= arena.Floor[enemy.X,enemy.Y,MapObjectType.Player,null];
            }
            Check(legal && post.Hitpoints<post.MaxHitpoints,"unattended monster routes around terrain and damages its assigned post");
            uint hp=post.Hitpoints;
            Call(wave,null,"DriveMonsters",inst,now);
            Check(post.Hitpoints==hp,"earlier or repeated event time cannot bypass monster attack delay");
            post.Hitpoints=1;
            Call(wave,null,"DriveMonsters",inst,now.AddMinutes(3));
            Check(post.Dead,"autonomous wave attack can destroy an unattended post");
            Check(Revora.Game.Features.WaveDefenseSystem.ResolveTarget(enemy)!=post,"autonomous monster chooses another post after destroying its first");
            enemy.Hitpoints=0;Call(wave,null,"SweepDeadMobs",inst);
            Check(!owned.ContainsKey(enemy.UID) && !((IDictionary)type.GetField("MonsterRoutes").GetValue(inst)).Contains(enemy.UID)
                && !((IDictionary)type.GetField("MonsterActions").GetValue(inst)).Contains(enemy.UID),"dead wave monsters release route and action state");
            foreach(var p in arena.Entities.Values.Where(Revora.Game.Features.WaveDefenseSystem.IsBanner))p.Hitpoints=0;
            post.Hitpoints=1;post.RemoveFlag((ulong)FlagsUpdate.Flags.Dead);
            Call(wave,null,"StartWave",inst,1);
            enemy=arena.Entities.Values.First(e=>owned.ContainsKey(e.UID) && !e.Dead);
            foreach(var e in arena.Entities.Values.Where(e=>owned.ContainsKey(e.UID) && e!=enemy))e.Hitpoints=0;
            enemy.X=(ushort)(post.X-1);enemy.Y=post.Y;
            long rewards=CtfCount("SELECT count(*) FROM revora_wave_results WHERE kind<>0");
            Call(wave,null,"TickInstance",inst,now.AddMinutes(5));
            Check(post.Dead && (bool)type.GetField("Failed").GetValue(inst) && (bool)type.GetField("Closed").GetValue(inst),
                "last post destruction ends and closes Wave Defense in the same event tick");
            Check(CtfCount("SELECT count(*) FROM revora_wave_results WHERE kind<>0")==rewards,
                "first-wave post destruction creates no consolation or victory entitlement");
        } finally {arena.Floor[x+5,y,MapObjectType.InvalidCast,null]=true;}
    }
    static void VerifyWaveMonsterTargets(Type wave,object inst,Map arena,GameState[] allies) {
        var owned=(Dictionary<uint,uint>)inst.GetType().GetField("MonsterPost").GetValue(inst);
        var enemy=arena.Entities.Values.First(e=>owned.ContainsKey(e.UID));
        var posts=arena.Entities.Values.Where(Revora.Game.Features.WaveDefenseSystem.IsBanner).ToArray();
        var players=(List<GameState>)inst.GetType().GetField("Players").GetValue(inst);
        var actors=players.Concat(allies).ToArray();
        var positions=actors.Select(p=>Tuple.Create(p.Entity.X,p.Entity.Y)).ToArray();
        ushort oldX=enemy.X,oldY=enemy.Y;
        uint assigned=owned[enemy.UID];
        var post=posts.First(p=>p.UID==assigned);
        Check(!Revora.Game.Attacking.Handle.CanAttack(players[0].Entity,post,null,true)
            && !Revora.Game.Attacking.Handle.CanAttack(players[0].Entity,post,null,false),"Wave defense posts reject ordinary-player melee and spell targeting");
        Check(!Revora.Game.Attacking.Handle.CanAttack(allies[0].Entity,post,null,true),"Wave companions cannot attack their defense posts");
        var foreign=new Entity(EntityFlag.Monster,false){UID=enemy.UID,MapID=arena.ID,MonsterInfo=enemy.MonsterInfo.Copy(),Hitpoints=100};
        uint postHp=post.Hitpoints;
        new Revora.Game.Attacking.Handle(null,foreign,post);
        Check(post.Hitpoints==postHp,"unregistered monster identity cannot damage a Wave post through the combat handler");
        try {
            enemy.X=100;enemy.Y=100;
            foreach(var p in actors){p.Entity.X=1;p.Entity.Y=1;}
            Check(ReferenceEquals(Revora.Game.Features.WaveDefenseSystem.ResolveTarget(enemy),post),"uncontested wave monster targets its assigned post");
            post.Hitpoints=0;
            var replacement=(Entity)Revora.Game.Features.WaveDefenseSystem.ResolveTarget(enemy);
            Check(replacement!=null && replacement!=post && posts.Contains(replacement) && !replacement.Dead,
                "wave monster switches from a destroyed post to a surviving post");
            var player=players[0];player.Entity.X=100;player.Entity.Y=101;
            Check(ReferenceEquals(Revora.Game.Features.WaveDefenseSystem.ResolveTarget(enemy),player.Entity),"nearby ordinary defender draws a monster away from its post");
            player.Entity.AddFlag((ulong)FlagsUpdate.Flags.Invisibility,60,true);
            bool socketAlive=player.Socket.Alive;
            try {
                Check(!ReferenceEquals(Revora.Game.Features.WaveDefenseSystem.ResolveTarget(enemy),player.Entity),"invisible defenders cannot draw wave monsters");
                player.Socket.Alive=true;player.Screen.Add(enemy);
                enemy.MonsterInfo.InSight=player.Entity.UID;
                enemy.MonsterInfo.LastMove=Time32.Now.AddSeconds(60);
                Call(typeof(Screen),null,"aliveMonstersCallback",player,Time32.Now.GetHashCode());
                Check(enemy.MonsterInfo.InSight==player.Entity.UID,"screen callbacks leave Wave monster decisions to the event tick");
            } finally {player.Socket.Alive=socketAlive;player.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Invisibility);player.Screen.Remove(enemy);}
            player.Entity.MapID=1002;
            Check(!ReferenceEquals(Revora.Game.Features.WaveDefenseSystem.ResolveTarget(enemy),player.Entity),"wave monster drops a defender who left its map");
            player.Entity.MapID=arena.ID;player.Entity.X=1;player.Entity.Y=1;
            var ally=allies[0];ally.Entity.X=100;ally.Entity.Y=101;
            Check(ReferenceEquals(Revora.Game.Features.WaveDefenseSystem.ResolveTarget(enemy),ally.Entity),"nearby companion draws a monster away from its post");
            ally.Entity.MapID=1002;
            Check(!ReferenceEquals(Revora.Game.Features.WaveDefenseSystem.ResolveTarget(enemy),ally.Entity),"wave monster drops a companion outside its map");
            ally.Entity.MapID=arena.ID;ally.Entity.X=1;ally.Entity.Y=1;
            enemy.MonsterInfo.InSight=player.Entity.UID;
            Check(posts.Contains((Entity)Revora.Game.Features.WaveDefenseSystem.ResolveTarget(enemy)),"wave monster abandons a stale distant player target");
            uint hp=replacement.Hitpoints;
            new Revora.Game.Attacking.Handle(null,enemy,replacement);
            Check(replacement.Hitpoints<hp,"actual wave monster combat damages a defense post");
            foreach(var p in posts)p.Hitpoints=0;
            Check(Revora.Game.Features.WaveDefenseSystem.ResolveTarget(enemy)==null && enemy.MonsterInfo.InSight==0,"no surviving post or nearby defender leaves no attack target");
            post.Hitpoints=post.MaxHitpoints;owned.Remove(enemy.UID);
            Check(Revora.Game.Features.WaveDefenseSystem.ResolveTarget(enemy)==null,"monsters outside the wave roster cannot acquire event targets");
        } finally {
            owned[enemy.UID]=assigned;enemy.X=oldX;enemy.Y=oldY;
            foreach(var p in posts)p.Hitpoints=p.MaxHitpoints;
            for(int i=0;i<actors.Length;i++){actors[i].Entity.MapID=arena.ID;actors[i].Entity.X=positions[i].Item1;actors[i].Entity.Y=positions[i].Item2;}
        }
    }
    static void VerifyWaveCompanionCombat(Type wave,object inst,Map arena,GameState[] allies) {
        var owned=(Dictionary<uint,uint>)inst.GetType().GetField("MonsterPost").GetValue(inst);
        var enemy=arena.Entities.Values.First(e=>owned.ContainsKey(e.UID));
        foreach(var e in arena.Entities.Values.Where(e=>owned.ContainsKey(e.UID)))e.Hitpoints=0;
        enemy.Hitpoints=enemy.MaxHitpoints=100000;
        var body=allies[0].Entity;
        foreach(var ally in allies.Skip(1))ally.Entity.Hitpoints=0;
        ushort x=0,y=0;
        for(int ty=1;ty<arena.Floor.Bounds.Height && x==0;ty++)
            for(int tx=1;tx<arena.Floor.Bounds.Width-9;tx++)
                if(Enumerable.Range(0,9).All(n=>arena.Floor[tx+n,ty,MapObjectType.Player,null])){x=(ushort)tx;y=(ushort)ty;break;}
        Check(x>0,"companion combat fixture has a clear approach lane");
        body.X=x;body.Y=y;
        enemy.X=(ushort)(x+8);enemy.Y=y;
        DateTime now=DateTime.UtcNow;
        Call(wave,null,"DriveAllies",inst,now);
        Check(body.X!=x || body.Y!=y,"Wave companion pursues a wave enemy outside melee range");
        for(int n=1;n<=12;n++)Call(wave,null,"DriveAllies",inst,now.AddSeconds(n));
        Check(enemy.Hitpoints<100000 && !enemy.Dead,"Wave companion reaches and damages an owned wave monster");
        uint hp=enemy.Hitpoints;
        Call(wave,null,"DriveAllies",inst,now.AddSeconds(12));
        Check(enemy.Hitpoints==hp,"Wave companion cannot bypass its attack cooldown");
        owned.Remove(enemy.UID);
        Call(wave,null,"DriveAllies",inst,now.AddSeconds(14));
        Check(enemy.Hitpoints==hp,"Wave companion ignores monsters outside its owned wave roster");
        owned[enemy.UID]=0;
        enemy.MonsterInfo.Boss=true;
        Call(wave,null,"DriveAllies",inst,now.AddSeconds(16));
        Check(enemy.Hitpoints<hp,"Wave companion can damage a boss in its owned wave roster");
        hp=enemy.Hitpoints;body.Hitpoints=0;
        Call(wave,null,"DriveAllies",inst,now.AddSeconds(18));
        Check(enemy.Hitpoints==hp,"dead Wave companions cannot attack");
        body.Hitpoints=body.MaxHitpoints;
        body.X=x;body.Y=y;enemy.X=(ushort)(x+2);enemy.Y=y;
        arena.Floor[x+1,y,MapObjectType.InvalidCast,null]=false;
        try {
            Call(wave,null,"DriveAllies",inst,now.AddSeconds(20));
            Check(enemy.Hitpoints==hp && !(body.X==x+1 && body.Y==y),"Wave companion cannot strike through or step into a blocked tile");
        } finally {arena.Floor[x+1,y,MapObjectType.InvalidCast,null]=true;}
        body.X=x;body.Y=y;enemy.Hitpoints=1;
        Call(wave,null,"DriveAllies",inst,now.AddSeconds(22));
        Check(enemy.Dead && (int)Call(wave,null,"RemainingMobs",inst)==0,"Wave companion lethal damage clears the last wave enemy");
    }
    static void VerifyWaveProgression(Map town,Type wave,IList live) {
        var players=Enumerable.Range(0,4).Select(i=>PkPlayer(town,140)).ToArray();
        foreach(var p in players){p.Entity.X=408;p.Entity.Y=330;SavePkProfileBeforeClaim(p);EntityVariableTable.Load(p);}
        var leader=players[0];leader.Team=new Revora.Game.ConquerStructures.Team(leader);foreach(var p in players.Skip(1))leader.Team.Add(p);
        Call(wave,null,"TryRegister",leader);Check(live.Count==1,"fresh ordinary team enters the complete Wave Defense progression fixture");
        var inst=live[0];var type=inst.GetType();var arena=(Map)type.GetField("Arena").GetValue(inst);DateTime now=DateTime.Now;
        for(int number=1;number<=8;number++) {
            var enemies=arena.Entities.Values.Where(e=>e.EntityFlag==EntityFlag.Monster && !e.Dead && !Revora.Game.Features.WaveDefenseSystem.IsBanner(e)).ToArray();
            int expected=(number>=5?10:6)+number+(number==4 || number==8?1:0);
            Check((int)type.GetField("Wave").GetValue(inst)==number && enemies.Length==expected,
                "Wave Defense wave "+number+" spawns its complete configured encounter");
            if(number==4 || number==8)Check(enemies.Any(e=>e.MaxHitpoints==(number==4?25000u:60000u)),"Wave Defense wave "+number+" includes its required boss");
            Call(wave,null,"TickInstance",inst,now);
            Check(!(bool)type.GetField("Won").GetValue(inst) && (DateTime)type.GetField("NextWaveAt").GetValue(inst)==default(DateTime),
                "Wave Defense living enemies prevent wave "+number+" from advancing or paying victory");
            // Controlled monster deaths exercise progression, not combat damage or native attacks.
            foreach(var mob in enemies)mob.Hitpoints=0;
            Call(wave,null,"TickInstance",inst,now);
            if(number<8){var next=(DateTime)type.GetField("NextWaveAt").GetValue(inst);Call(wave,null,"TickInstance",inst,next.AddMilliseconds(-1));
                Check((int)type.GetField("Wave").GetValue(inst)==number,"Wave Defense preserves the full regroup interval after wave "+number);
                now=next;Call(wave,null,"TickInstance",inst,now);}
        }
        Check((bool)type.GetField("Won").GetValue(inst) && live.Count==0 && !Kernel.Maps.ContainsKey(arena.ID) && players.All(p=>p.Entity.MapID==1002),
            "all eight controlled wave clears select victory and release the safely returned team field");
        for(int i=0;i<players.Length;i++) {
            var p=players[i];uint cp=p.Entity.ConquerPoints;Call(wave,null,"TryClaim",p);
            Check(p.Inventory.Count==1 && p.Inventory.Objects.Single().Bound && ConquerItemInformation.BaseInformations[p.Inventory.Objects.Single().ID].PurificationLevel==7
                && p.Entity.ConquerPoints==cp+Revora.Config.WaveDefenseWinCp,"eight-wave progression pays ordinary teammate "+i+" one bound P7 soul and victory purse");
            uint uid=p.Inventory.Objects.Single().UID;p=ReloadBall(p);EntityVariableTable.Load(p);Call(wave,null,"TryClaim",p);
            Check(p.Inventory.Count==1 && p.Inventory.Objects.Single().UID==uid && p.Entity.ConquerPoints==cp+Revora.Config.WaveDefenseWinCp,
                "eight-wave teammate "+i+" preserves the exact saved reward after reconnect and replay");players[i]=p;
        }
        leader=players[0];leader.Team=new Revora.Game.ConquerStructures.Team(leader);foreach(var p in players.Skip(1))leader.Team.Add(p);
        foreach(uint missing in new uint[]{2,8306,31}) {
            Call(wave,null,"TryRegister",leader);inst=live[0];arena=(Map)type.GetField("Arena").GetValue(inst);
            var definition=MonsterInformation.MonsterInformations[missing];MonsterInformation.MonsterInformations.Remove(missing);
            try{Call(wave,null,"StartWave",inst,missing==2?1:missing==8306?4:8);}
            finally{MonsterInformation.MonsterInformations[missing]=definition;}
            Check((bool)type.GetField("Abort").GetValue(inst),"missing Wave Defense catalog "+missing+" aborts instead of treating an empty wave as cleared");
            Call(wave,null,"TickInstance",inst,DateTime.Now);
            Guid run=(Guid)type.GetField("ResultId").GetValue(inst);
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT count(*) FROM revora_wave_results WHERE run=@run",c)){q.Parameters.AddWithValue("run",run);
                Check((long)q.ExecuteScalar()==0 && live.Count==0 && !Kernel.Maps.ContainsKey(arena.ID) && players.All(p=>p.Entity.MapID==1002),
                    "missing Wave Defense catalog "+missing+" returns everyone without generating a consolation or victory");}}
        }
    }
    static object WaveResultFixture(Type wave,Map town,int level,params GameState[] players) {
        var type=wave.GetNestedType("Instance",BindingFlags.NonPublic);var inst=Activator.CreateInstance(type,true);
        type.GetField("Arena").SetValue(inst,town);type.GetField("Wave").SetValue(inst,level);
        type.GetField("Participants").SetValue(inst,players.Select(p=>p.Entity.UID).ToArray());
        ((List<GameState>)type.GetField("Players").GetValue(inst)).AddRange(players);return inst;
    }
    static void VerifyWaveResults(Map town,Type wave) {
        const string key=Revora.Game.Features.WaveDefenseSystem.ClaimKey;
        var a=PkPlayer(town,140);var b=PkPlayer(town,140);
        foreach(var p in new[]{a,b}){SavePkProfileBeforeClaim(p);EntityVariableTable.Load(p);}
        a[key]=2u;EntityVariableTable.Save(a);
        var inst=WaveResultFixture(wave,town,8,a,b);var type=inst.GetType();
        Query("CREATE FUNCTION wave_reject_result() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF EXISTS(SELECT 1 FROM revora_wave_results WHERE run=NEW.run) THEN RETURN NULL; END IF; RETURN NEW; END $$; CREATE TRIGGER wave_reject_result BEFORE INSERT ON revora_wave_results FOR EACH ROW EXECUTE FUNCTION wave_reject_result()");
        try{Call(wave,null,"Win",inst);}finally{Query("DROP TRIGGER wave_reject_result ON revora_wave_results;DROP FUNCTION wave_reject_result()");}
        Check(!(bool)type.GetField("Won").GetValue(inst) && CtfCount("SELECT count(*) FROM revora_wave_results")==0,
            "Wave Defense second result insert failure rolls back the whole roster and retains pending completion");
        ((List<GameState>)type.GetField("Players").GetValue(inst)).Remove(b);GameState offline;Kernel.GamePool.TryRemove(b.Entity.UID,out offline);
        type.GetField("Wave").SetValue(inst,1);Call(wave,null,"TickInstance",inst,DateTime.Now);Call(wave,null,"Fail",inst,"late failure");
        Check((bool)type.GetField("Won").GetValue(inst) && CtfCount("SELECT count(*) FROM revora_wave_results WHERE kind=1")==2,
            "Wave Defense tick retries frozen victory and eligibility despite late departure or changed wave");
        Check((uint)a[key]==2,"Wave Defense completion preserves an older legacy consolation");
        var second=WaveResultFixture(wave,town,8,a,b);Call(wave,null,"Win",second);Call(wave,null,"Win",second);
        Check(Scalar("SELECT count(*) FROM revora_wave_results WHERE uid=@uid AND kind=1",a.Entity.UID)==2,
            "separate Wave Defense wins queue two prizes while repeated completion stays idempotent");
        b=ReloadBall(b);EntityVariableTable.Load(b);Call(wave,null,"InitializePrizes");
        Query("CREATE FUNCTION wave_reject_import() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER wave_reject_import BEFORE UPDATE ON revora_wave_results FOR EACH ROW EXECUTE FUNCTION wave_reject_import()");
        try{Call(wave,null,"HasClaim",b);Check(Fenced(b),"Wave Defense failed queued import fences its owner");}
        finally{Query("DROP TRIGGER wave_reject_import ON revora_wave_results;DROP FUNCTION wave_reject_import()");}
        b=ReloadBall(b);EntityVariableTable.Load(b);
        Check((uint)b[key]==0 && Scalar("SELECT count(*) FROM revora_wave_results WHERE uid=@uid AND NOT imported",b.Entity.UID)==2,
            "Wave Defense failed import retains both queued receipts without consuming or installing a claim");
        for(int n=0;n<2;n++){Call(wave,null,"TryClaim",b);b=ReloadBall(b);EntityVariableTable.Load(b);}Call(wave,null,"TryClaim",b);
        Check(b.Inventory.Count==2 && b.Inventory.Objects.All(i=>i.Bound && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)
            && b.Entity.ConquerPoints==2*Revora.Config.WaveDefenseWinCp,
            "returning Wave Defense teammate collects two saved victories once across fresh reloads");
        for(int n=0;n<3;n++){Call(wave,null,"TryClaim",a);a=ReloadBall(a);EntityVariableTable.Load(a);}Call(wave,null,"TryClaim",a);
        Check(a.Inventory.Count==2 && a.Entity.ConquerPoints==Revora.Config.WaveDefenseConsolationCp+2*Revora.Config.WaveDefenseWinCp,
            "old Wave Defense consolation and two new victories all survive without overwriting or extra souls");
        var early=WaveResultFixture(wave,town,3,a,b);Call(wave,null,"Fail",early,"posts lost");
        var partial=WaveResultFixture(wave,town,4,a,b);((List<GameState>)type.GetField("Players").GetValue(partial)).Remove(b);Call(wave,null,"Fail",partial,"posts lost");
        Check(Scalar("SELECT count(*) FROM revora_wave_results WHERE uid=@uid AND kind=2",a.Entity.UID)==1
            && Scalar("SELECT count(*) FROM revora_wave_results WHERE uid=@uid AND kind=0",b.Entity.UID)==2,
            "Wave Defense pre-four failure and early departure cannot become victory or later-wave consolation");
        Call(wave,null,"TryClaim",a);Check(a.Inventory.Count==2 && a.Entity.ConquerPoints==2*Revora.Config.WaveDefenseConsolationCp+2*Revora.Config.WaveDefenseWinCp,
            "earned fourth-wave consolation adds its purse without a P7 soul");
    }
    static void VerifyRelicAtomicClaims(Map town) {
        var relic=typeof(Revora.Game.Features.RelicRushSystem);
        Call(relic,null,"InitializePrizes");Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.RelicAdmissions"),null,"Initialize");
        const string key=Revora.Game.Features.RelicRushSystem.ClaimKey;
        for(uint place=1;place<=3;place++) {
            var p=PkPlayer(town,140);SavePkProfileBeforeClaim(p);EntityVariableTable.Load(p);
            p[key]=place;EntityVariableTable.Save(p);
            uint cp=p.Entity.ConquerPoints,rp=p.Entity.RevoraPoint;ulong gold=p.Entity.Money;
            uint prizeCp=place==1?Revora.Game.Features.RelicRushSystem.FirstCp:place==2?Revora.Game.Features.RelicRushSystem.SecondCp:Revora.Game.Features.RelicRushSystem.ThirdCp;
            uint prizeRp=place==1?Revora.Game.Features.RelicRushSystem.FirstRevoraPoints:place==2?Revora.Game.Features.RelicRushSystem.SecondRevoraPoints:Revora.Game.Features.RelicRushSystem.ThirdRevoraPoints;
            ulong prizeGold=place==1?Revora.Game.Features.RelicRushSystem.FirstGold:place==2?Revora.Game.Features.RelicRushSystem.SecondGold:Revora.Game.Features.RelicRushSystem.ThirdGold;
            if(place==1) {
                var filler=Enumerable.Range(0,40).Select(i=>Pack(p,1088001,1)).ToArray();Call(relic,null,"TryClaim",p);
                Check(!Fenced(p) && (uint)p[key]==place && p.Entity.ConquerPoints==cp,"full Relic Rush inventory preserves the placement and purse");
                foreach(var item in filler){Query("DELETE FROM items WHERE uid=@id","id",(long)item.UID);p.Inventory.Remove(item.UID,Enums.ItemUse.None,false);}
                p.Entity.Money=Entity.MaximumSilver;Call(relic,null,"TryClaim",p);
                Check(!Fenced(p) && (uint)p[key]==place && p.Inventory.Count==0,"overflowing Relic Rush purse leaves its prize pending");p.Entity.Money=gold;
                foreach(string table in new[]{"variablevault","items"}) {
                    string action=table=="items"?"INSERT":"UPDATE";
                    Query("CREATE FUNCTION relic_reject() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER relic_reject BEFORE "+action+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION relic_reject()");
                    try{Call(relic,null,"TryClaim",p);Check(Fenced(p),"failed Relic Rush "+table+" write requires reload");}
                    finally{Query("DROP TRIGGER relic_reject ON "+table+";DROP FUNCTION relic_reject()");}
                    p=ReloadBall(p);EntityVariableTable.Load(p);
                    Check((uint)p[key]==place && p.Inventory.Count==0 && p.Entity.ConquerPoints==cp && p.Entity.RevoraPoint==rp && p.Entity.Money==gold,
                        "failed Relic Rush "+table+" write rolls back saved placement, currencies and soul");
                }
            }
            var actor=p;Parallel.Invoke(()=>Call(relic,null,"TryClaim",actor),()=>Call(relic,null,"TryClaim",actor));
            Check(p.Account.State==AccountTable.AccountState.Player && p.Inventory.Count==1
                && p.Inventory.Objects.All(i=>i.Bound && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)
                && p.Entity.ConquerPoints==cp+prizeCp && p.Entity.RevoraPoint==rp+prizeRp && p.Entity.Money==gold+prizeGold,
                "concurrent ordinary Relic Rush placement "+place+" claims pay one exact purse and bound P7 soul");
            uint itemUid=p.Inventory.Objects.Single().UID;
            p=ReloadBall(p);EntityVariableTable.Load(p);Call(relic,null,"TryClaim",p);
            Check((uint)p[key]==0 && p.Inventory.Count==1 && p.Inventory.Objects.Single().UID==itemUid && p.Inventory.Objects.Single().Bound
                && p.Entity.ConquerPoints==cp+prizeCp && p.Entity.RevoraPoint==rp+prizeRp && p.Entity.Money==gold+prizeGold,
                "Relic Rush placement "+place+" reload and replay preserve the exact earned item and purse");
            p[key]=place;Call(relic,null,"TryClaim",p);
            Check(Fenced(p) && Scalar("SELECT count(*) FROM items WHERE entityid=@uid",p.Entity.UID)==1,
                "stale Relic Rush placement "+place+" cannot resurrect a consumed saved claim");
        }
        var invalid=PkPlayer(town,140);SavePkProfileBeforeClaim(invalid);EntityVariableTable.Load(invalid);
        invalid[key]=4u;EntityVariableTable.Save(invalid);Call(relic,null,"TryClaim",invalid);
        Check(!Fenced(invalid) && invalid.Inventory.Count==0 && (uint)invalid[key]==4,
            "invalid Relic Rush placement is rejected instead of falling through to third prize");
        VerifyRelicResults(town,relic);
    }
    static void VerifyRelicResults(Map town,Type relic) {
        const string key=Revora.Game.Features.RelicRushSystem.ClaimKey;
        var a=PkPlayer(town,140);var b=PkPlayer(town,140);var zero=PkPlayer(town,140);
        foreach(var p in new[]{a,b,zero}){SavePkProfileBeforeClaim(p);EntityVariableTable.Load(p);}
        a[key]=3u;EntityVariableTable.Save(a);
        var names=new[]{"EventMap","Running","CycleStart","PendingRank","RegisterOpen","Invited"};
        var fields=names.Select(n=>relic.GetField(n,Hidden)).ToArray();var prior=fields.Select(f=>f.GetValue(null)).ToArray();
        var scores=(Dictionary<uint,uint>)relic.GetField("Scores",Hidden).GetValue(null);var oldScores=scores.ToArray();
        try {
            relic.GetField("EventMap",Hidden).SetValue(null,null);relic.GetField("Running",Hidden).SetValue(null,true);
            relic.GetField("CycleStart",Hidden).SetValue(null,new DateTime(2033,1,1,1,0,0));
            relic.GetField("PendingRank",Hidden).SetValue(null,null);scores.Clear();scores[a.Entity.UID]=10;scores[b.Entity.UID]=10;scores[zero.Entity.UID]=0;
            GameState offline;Kernel.GamePool.TryRemove(b.Entity.UID,out offline);
            Query("CREATE FUNCTION relic_reject_result() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.place=2 THEN RETURN NULL; END IF; RETURN NEW; END $$; CREATE TRIGGER relic_reject_result BEFORE INSERT ON revora_relic_placements FOR EACH ROW EXECUTE FUNCTION relic_reject_result()");
            try{Call(relic,null,"FinishByScore");}
            finally{Query("DROP TRIGGER relic_reject_result ON revora_relic_placements;DROP FUNCTION relic_reject_result()");}
            Check((bool)relic.GetField("Running",Hidden).GetValue(null) && CtfCount("SELECT count(*) FROM revora_relic_placements WHERE cycle='203301010100'")==0,
                "failed Relic Rush result insert retains the frozen ranking without partial prizes");
            scores[zero.Entity.UID]=100;Call(relic,null,"FinishByScore");
            Check(!(bool)relic.GetField("Running",Hidden).GetValue(null)
                && Scalar("SELECT count(*) FROM revora_relic_placements WHERE uid=@uid AND place=1 AND score=10",a.Entity.UID)==1
                && Scalar("SELECT count(*) FROM revora_relic_placements WHERE uid=@uid AND place=2 AND score=10",b.Entity.UID)==1
                && Scalar("SELECT count(*) FROM revora_relic_placements WHERE uid=@uid",zero.Entity.UID)==0,
                "retry saves the captured ranking including offline scorer, deterministic UID tie and no zero-score prize");
            Check((uint)a[key]==3,"new Relic Rush result preserves an existing legacy unclaimed placement");
            relic.GetField("Running",Hidden).SetValue(null,true);relic.GetField("CycleStart",Hidden).SetValue(null,new DateTime(2033,1,1,1,0,0));
            scores[a.Entity.UID]=10;scores[b.Entity.UID]=10;scores[zero.Entity.UID]=0;Call(relic,null,"FinishByScore");
            Check(CtfCount("SELECT count(*) FROM revora_relic_placements WHERE cycle='203301010100'")==2,
                "replaying the same captured Relic Rush cycle preserves exactly its original saved placements");
            // A second real finish queues another result instead of replacing the first.
            relic.GetField("Running",Hidden).SetValue(null,true);relic.GetField("CycleStart",Hidden).SetValue(null,new DateTime(2033,1,1,3,0,0));
            scores[a.Entity.UID]=7;Call(relic,null,"FinishByScore");Call(relic,null,"FinishByScore");
            Check(Scalar("SELECT count(*) FROM revora_relic_placements WHERE uid=@uid",a.Entity.UID)==2,
                "a second Relic Rush finish queues a distinct prize and repeated finish cannot duplicate it");
            b=ReloadBall(b);EntityVariableTable.Load(b);Call(relic,null,"InitializePrizes");Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.RelicAdmissions"),null,"Initialize");
            Query("CREATE FUNCTION relic_reject_import() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER relic_reject_import BEFORE UPDATE ON revora_relic_placements FOR EACH ROW EXECUTE FUNCTION relic_reject_import()");
            try{Call(relic,null,"HasClaim",b);Check(Fenced(b),"failed Relic Rush result import fences the ordinary returning winner");}
            finally{Query("DROP TRIGGER relic_reject_import ON revora_relic_placements;DROP FUNCTION relic_reject_import()");}
            b=ReloadBall(b);EntityVariableTable.Load(b);
            Check((uint)b[key]==0 && Scalar("SELECT count(*) FROM revora_relic_placements WHERE uid=@uid AND NOT imported",b.Entity.UID)==1,
                "failed Relic Rush import rolls back its saved placement and retains the queued result");
            Call(relic,null,"TryClaim",b);b=ReloadBall(b);EntityVariableTable.Load(b);Call(relic,null,"TryClaim",b);
            Check(b.Inventory.Count==1 && b.Inventory.Objects.Single().Bound && b.Entity.ConquerPoints==Revora.Game.Features.RelicRushSystem.SecondCp,
                "offline second-place scorer reloads and claims its earned bound soul and purse exactly once");
            for(int n=0;n<3;n++){Call(relic,null,"TryClaim",a);a=ReloadBall(a);EntityVariableTable.Load(a);}
            Call(relic,null,"TryClaim",a);
            Check(a.Inventory.Count==3 && a.Inventory.Objects.All(i=>i.Bound && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)
                && a.Entity.ConquerPoints==Revora.Game.Features.RelicRushSystem.ThirdCp+2*Revora.Game.Features.RelicRushSystem.FirstCp
                && Scalar("SELECT count(*) FROM revora_relic_placements WHERE uid=@uid AND imported",a.Entity.UID)==2,
                "legacy placement plus two earned Relic Rush results all pay once across fresh reloads");
        } finally {
            for(int i=0;i<fields.Length;i++)fields[i].SetValue(null,prior[i]);scores.Clear();foreach(var pair in oldScores)scores[pair.Key]=pair.Value;
        }
    }
    static void VerifyLmsAtomicClaim(Map town,string runtime) {
        var lms=typeof(Revora.Game.Features.LastManStandingSystem);
        Call(lms,null,"InitializePrizes");
        Query("CREATE TABLE IF NOT EXISTS variablevault(entityid bigint PRIMARY KEY,data bytea NOT NULL)");
        var p=PkPlayer(town,140);SavePkProfileBeforeClaim(p);EntityVariableTable.Load(p);
        p[Revora.Game.Features.LastManStandingSystem.ClaimKey]=1u;EntityVariableTable.Save(p);
        uint cp=p.Entity.ConquerPoints,rvp=p.Entity.RevoraPoint;ulong gold=p.Entity.Money;
        var filler=Enumerable.Range(0,40).Select(i=>Pack(p,1088001,1)).ToArray();
        Call(lms,null,"TryClaim",p);
        Check(!Fenced(p) && (uint)p[Revora.Game.Features.LastManStandingSystem.ClaimKey]==1
            && p.Entity.ConquerPoints==cp && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==cp,
            "full LMS inventory retains the saved prize without paying its purse");
        foreach(var item in filler){Query("DELETE FROM items WHERE uid=@id","id",(long)item.UID);p.Inventory.Remove(item.UID,Enums.ItemUse.None,false);}
        Query("CREATE FUNCTION lms_reject_claim() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER lms_reject_claim BEFORE UPDATE ON variablevault FOR EACH ROW EXECUTE FUNCTION lms_reject_claim()");
        try {
            Call(lms,null,"TryClaim",p);
            Check(Fenced(p) && p.Entity.ConquerPoints==cp && p.Entity.RevoraPoint==rvp && p.Entity.Money==gold,
                "failed LMS entitlement write leaves all cached currencies unchanged and requires reload");
            Check(Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==cp
                && Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",p.Entity.UID)==rvp
                && Scalar("SELECT money FROM entities WHERE uid=@uid",p.Entity.UID)==(long)gold,
                "failed LMS entitlement write rolls back all saved currency credits");
        }finally{Query("DROP TRIGGER lms_reject_claim ON variablevault;DROP FUNCTION lms_reject_claim()");}
        Check(p.Inventory.Count==0 && Scalar("SELECT count(*) FROM items WHERE entityid=@uid",p.Entity.UID)==0,
            "failed LMS entitlement cannot leave a soul in memory or storage");
        p=ReloadBall(p);EntityVariableTable.Load(p);
        Check((uint)p[Revora.Game.Features.LastManStandingSystem.ClaimKey]==1,"failed LMS claim survives ordinary character reload");
        Query("CREATE FUNCTION lms_reject_item() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER lms_reject_item BEFORE INSERT ON items FOR EACH ROW EXECUTE FUNCTION lms_reject_item()");
        try {Call(lms,null,"TryClaim",p);}
        finally{Query("DROP TRIGGER lms_reject_item ON items;DROP FUNCTION lms_reject_item()");}
        p=ReloadBall(p);EntityVariableTable.Load(p);
        Check(p.Inventory.Count==0 && (uint)p[Revora.Game.Features.LastManStandingSystem.ClaimKey]==1
            && p.Entity.ConquerPoints==cp && p.Entity.RevoraPoint==rvp && p.Entity.Money==gold,
            "suppressed LMS soul insert rolls back consumed entitlement and all currencies across reload");
        var current=p;Parallel.Invoke(()=>Call(lms,null,"TryClaim",current),()=>Call(lms,null,"TryClaim",current));
        Check(p.Entity.ConquerPoints==cp+Revora.Game.Features.LastManStandingSystem.CpPrize
            && p.Entity.RevoraPoint==rvp+Revora.Game.Features.LastManStandingSystem.RevoraPointPrize
            && p.Entity.Money==gold+Revora.Game.Features.LastManStandingSystem.GoldPrize,
            "concurrent LMS claims pay exactly one complete purse");
        Check(p.Inventory.Count==1 && p.Inventory.Objects.All(i=>i.Bound && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7),"concurrent LMS claims install exactly one bound P7 soul");
        uint soulUid=p.Inventory.Objects.Single().UID;
        p=ReloadBall(p);EntityVariableTable.Load(p);
        Check(p.Inventory.Count==1 && p.Inventory.Objects.All(i=>i.Bound && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7) && p.Inventory.Objects.Single().UID==soulUid,
            "LMS bound P7 identity survives ordinary character and item reload");
        Call(lms,null,"TryClaim",p);
        Check(p.Inventory.Count==1 && Scalar("SELECT count(*) FROM items WHERE entityid=@uid",p.Entity.UID)==1,
            "consumed LMS entitlement cannot mint a second soul");
        Check((uint)p[Revora.Game.Features.LastManStandingSystem.ClaimKey]==0
            && p.Entity.ConquerPoints==cp+Revora.Game.Features.LastManStandingSystem.CpPrize
            && p.Entity.RevoraPoint==rvp+Revora.Game.Features.LastManStandingSystem.RevoraPointPrize
            && p.Entity.Money==gold+Revora.Game.Features.LastManStandingSystem.GoldPrize,
            "LMS consumed entitlement and exact three-currency purse survive reconnect");
        p[Revora.Game.Features.LastManStandingSystem.ClaimKey]=1u;Call(lms,null,"TryClaim",p);
        Check(Fenced(p) && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==cp+Revora.Game.Features.LastManStandingSystem.CpPrize,
            "stale in-memory LMS entitlement cannot revive an already consumed saved claim");
        p=ReloadBall(p);EntityVariableTable.Load(p);Call(lms,null,"InitializePrizes");
        current=p;
        Parallel.Invoke(()=>Call(lms,null,"RecordVictory",current,"203001010000"),()=>Call(lms,null,"RecordVictory",current,"203001010000"));
        Check((uint)p[Revora.Game.Features.LastManStandingSystem.ClaimKey]==1
            && Scalar("SELECT count(*) FROM revora_lms_victories WHERE uid=@uid",p.Entity.UID)==1,
            "repeated concurrent LMS winner recording creates one durable victory and entitlement");
        Call(lms,null,"RecordVictory",p,"203001010200");
        Check((uint)p[Revora.Game.Features.LastManStandingSystem.ClaimKey]==2,
            "another LMS victory preserves the earlier unclaimed purse");
        Query("CREATE FUNCTION lms_reject_victory() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER lms_reject_victory BEFORE UPDATE ON variablevault FOR EACH ROW EXECUTE FUNCTION lms_reject_victory()");
        bool rejected=false;
        try {try{Call(lms,null,"RecordVictory",p,"203001010400");}catch{rejected=true;}}
        finally{Query("DROP TRIGGER lms_reject_victory ON variablevault;DROP FUNCTION lms_reject_victory()");}
        Check(rejected && (uint)p[Revora.Game.Features.LastManStandingSystem.ClaimKey]==2
            && Scalar("SELECT count(*) FROM revora_lms_victories WHERE uid=@uid",p.Entity.UID)==2,
            "failed LMS winner entitlement update rolls back the victory record and preserves earlier wins");
        cp=p.Entity.ConquerPoints;rvp=p.Entity.RevoraPoint;gold=p.Entity.Money;
        Call(lms,null,"TryClaim",p);p=ReloadBall(p);EntityVariableTable.Load(p);
        Check((uint)p[Revora.Game.Features.LastManStandingSystem.ClaimKey]==1,
            "claiming one of two LMS victories leaves the other pending after reload");
        Call(lms,null,"TryClaim",p);p=ReloadBall(p);EntityVariableTable.Load(p);
        Check((uint)p[Revora.Game.Features.LastManStandingSystem.ClaimKey]==0
            && p.Entity.ConquerPoints==cp+2*Revora.Game.Features.LastManStandingSystem.CpPrize
            && p.Entity.RevoraPoint==rvp+2*Revora.Game.Features.LastManStandingSystem.RevoraPointPrize
            && p.Entity.Money==gold+2*Revora.Game.Features.LastManStandingSystem.GoldPrize,
            "two durable LMS victories pay two complete purses exactly once across reloads");
        Check(p.Inventory.Count==3 && p.Inventory.Objects.All(i=>i.Bound && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7),
            "two further saved LMS victories each grant one additional bound P7 soul");
        var names=new[]{"EventMap","Running","CycleStart","PendingWinner","PendingCycle","RegisterOpen","Invited"};
        var fields=names.Select(n=>lms.GetField(n,Hidden)).ToArray();var values=fields.Select(f=>f.GetValue(null)).ToArray();
        var alive=(HashSet<uint>)lms.GetField("Alive",Hidden).GetValue(null);var previous=alive.ToArray();
        try {
            lms.GetField("EventMap",Hidden).SetValue(null,town);lms.GetField("Running",Hidden).SetValue(null,true);
            lms.GetField("CycleStart",Hidden).SetValue(null,new DateTime(2030,1,1,6,0,0));
            lms.GetField("PendingWinner",Hidden).SetValue(null,0u);alive.Clear();alive.Add(p.Entity.UID);
            using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
                var actor=p;var task=Task.Run(()=>{lock(actor.ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
                try {
                    Call(lms,null,"Finish",p);
                    Check(Scalar("SELECT count(*) FROM revora_lms_selected_winners WHERE cycle='203001010600' AND uid=@uid",p.Entity.UID)==1
                        && Scalar("SELECT count(*) FROM revora_lms_victories WHERE cycle='203001010600' AND uid=@uid",p.Entity.UID)==0,
                        "busy LMS winner actor does not prevent saving the selected result before deferred payout");
                }finally{release.Set();task.Wait();}
            }
            Query("CREATE FUNCTION lms_reject_finish() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER lms_reject_finish BEFORE UPDATE ON variablevault FOR EACH ROW EXECUTE FUNCTION lms_reject_finish()");
            try {
                Call(lms,null,"Finish",p);
                Check((uint)lms.GetField("PendingWinner",Hidden).GetValue(null)==p.Entity.UID
                    && (bool)lms.GetField("Running",Hidden).GetValue(null) && (uint)p[Revora.Game.Features.LastManStandingSystem.ClaimKey]==0,
                    "actual LMS Finish retains its selected winner after a failed entitlement write");
            }finally{Query("DROP TRIGGER lms_reject_finish ON variablevault;DROP FUNCTION lms_reject_finish()");}
            GameState disconnected;Kernel.GamePool.TryRemove(p.Entity.UID,out disconnected);
            Call(lms,null,"CompletePendingVictory");
            Check((uint)lms.GetField("PendingWinner",Hidden).GetValue(null)==0u
                && !(bool)lms.GetField("Running",Hidden).GetValue(null)
                && (uint)p[Revora.Game.Features.LastManStandingSystem.ClaimKey]==0
                && Scalar("SELECT count(*) FROM revora_lms_victories WHERE uid=@uid AND cycle='203001010600'",p.Entity.UID)==1
                && Scalar("SELECT count(*) FROM revora_lms_offline_prizes WHERE uid=@uid AND applied_at IS NULL",p.Entity.UID)==1,
                "actual LMS finalization saves a disconnected winner without touching the stale session or stalling the cycle");
            Call(lms,null,"RecordOfflineVictory",p.Entity.UID,"203001010600");
            p=ReloadBall(p);EntityVariableTable.Load(p);cp=p.Entity.ConquerPoints;
            Query("CREATE FUNCTION lms_reject_import() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER lms_reject_import BEFORE UPDATE ON revora_lms_offline_prizes FOR EACH ROW EXECUTE FUNCTION lms_reject_import()");
            try {
                Check(!(bool)Call(lms,null,"HasClaim",p) && Fenced(p)
                    && (uint)p[Revora.Game.Features.LastManStandingSystem.ClaimKey]==0,
                    "failed offline import leaves the LMS cached counter unchanged and requires reload");
                VariableVault saved;EntityVariableTable.Load(p.Entity.UID,out saved);
                Check((uint)saved[Revora.Game.Features.LastManStandingSystem.ClaimKey]==0
                    && Scalar("SELECT count(*) FROM revora_lms_offline_prizes WHERE uid=@uid AND applied_at IS NULL",p.Entity.UID)==1,
                    "failed offline import rolls back the saved counter and retains its unapplied receipt");
            }finally{Query("DROP TRIGGER lms_reject_import ON revora_lms_offline_prizes;DROP FUNCTION lms_reject_import()");}
            p=ReloadBall(p);EntityVariableTable.Load(p);
            Check((bool)Call(lms,null,"HasClaim",p) && (uint)p[Revora.Game.Features.LastManStandingSystem.ClaimKey]==1,
                "returning LMS winner imports its saved offline prize once");
            Call(lms,null,"HasClaim",p);Call(lms,null,"TryClaim",p);p=ReloadBall(p);EntityVariableTable.Load(p);
            Check(!(bool)Call(lms,null,"HasClaim",p) && p.Entity.ConquerPoints==cp+Revora.Game.Features.LastManStandingSystem.CpPrize,
                "replayed offline result and repeated import cannot duplicate the saved LMS payout");
            var rival=PkPlayer(town,140);var arena=World(runtime,700);Kernel.Maps.Remove(700);arena.BaseID=700;arena.ID=epkMapId++;
            Kernel.Maps[arena.ID]=arena;MapsTable.MapInformations[arena.ID]=new MapsTable.MapInformation{ID=arena.ID,BaseID=700,Status=2};
            DMaps.MapPaths[700]=System.IO.Path.Combine(runtime,"database/maps/700.map");
            SavePkProfileBeforeClaim(rival);
            Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.LmsAdmissions"),null,"Begin","203001010800",arena.ID,new GameState[]{p,rival});
            p.Entity.Teleport(700,arena.ID,50,50);rival.Entity.Teleport(700,arena.ID,51,50);
            lms.GetField("EventMap",Hidden).SetValue(null,arena);lms.GetField("Running",Hidden).SetValue(null,true);
            lms.GetField("CycleStart",Hidden).SetValue(null,new DateTime(2030,1,1,8,0,0));
            alive.Clear();alive.Add(p.Entity.UID);alive.Add(rival.Entity.UID);
            long beforeWins=CtfCount("SELECT count(*) FROM revora_lms_victories");
            Call(lms,null,"SweepAndCheck",false);
            Check((bool)lms.GetField("Running",Hidden).GetValue(null) && p.Entity.MapID==arena.ID && rival.Entity.MapID==arena.ID,
                "LMS with two survivors continues before its fight deadline");
            lms.GetField("PendingWinner",Hidden).SetValue(null,p.Entity.UID);
            Call(lms,null,"SweepAndCheck",true);
            Check((bool)lms.GetField("Running",Hidden).GetValue(null) && alive.Count==2,
                "timeout sweep cannot replace or cancel an already frozen LMS winner");
            lms.GetField("PendingWinner",Hidden).SetValue(null,0u);
            Call(lms,null,"SweepAndCheck",true);
            Check(!(bool)lms.GetField("Running",Hidden).GetValue(null) && alive.Count==0
                && p.Entity.MapID==1002 && rival.Entity.MapID==1002
                && CtfCount("SELECT count(*) FROM revora_lms_victories")==beforeWins,
                "expired LMS with multiple survivors returns players and closes without inventing a winner");
            Call(lms,null,"PersistSelectedWinner",p.Entity.UID,"203001011000");
            bool conflict=false;try{Call(lms,null,"PersistSelectedWinner",rival.Entity.UID,"203001011000");}catch{conflict=true;}
            Check(conflict && Scalar("SELECT count(*) FROM revora_lms_selected_winners WHERE cycle='203001011000' AND uid=@uid",p.Entity.UID)==1,
                "saved LMS selection cannot be replaced by another winner");
            Query("CREATE FUNCTION lms_reject_recovery() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER lms_reject_recovery BEFORE INSERT ON revora_lms_offline_prizes FOR EACH ROW EXECUTE FUNCTION lms_reject_recovery()");
            bool failed=false;
            try{try{Call(lms,null,"RecoverSelectedVictories");}catch{failed=true;}}
            finally{Query("DROP TRIGGER lms_reject_recovery ON revora_lms_offline_prizes;DROP FUNCTION lms_reject_recovery()");}
            Check(failed && Scalar("SELECT count(*) FROM revora_lms_victories WHERE cycle='203001011000' AND uid=@uid",p.Entity.UID)==0
                && Scalar("SELECT count(*) FROM revora_lms_selected_winners WHERE cycle='203001011000' AND uid=@uid",p.Entity.UID)==1,
                "failed LMS startup delivery rolls back victory while retaining the saved selected winner");
            lms.GetField("PendingWinner",Hidden).SetValue(null,0u);lms.GetField("PendingCycle",Hidden).SetValue(null,null);
            Call(lms,null,"RecoverSelectedVictories");Call(lms,null,"RecoverSelectedVictories");
            Check(Scalar("SELECT count(*) FROM revora_lms_victories WHERE cycle='203001011000' AND uid=@uid",p.Entity.UID)==1
                && Scalar("SELECT count(*) FROM revora_lms_offline_prizes WHERE cycle='203001011000' AND uid=@uid AND applied_at IS NULL",p.Entity.UID)==1
                && (uint)p[Revora.Game.Features.LastManStandingSystem.ClaimKey]==0,
                "LMS startup recovery needs no old pending fields and grants one offline receipt across repeated passes");
            VerifyLmsAdmissions(lms,town,arena);
            VerifyRelicAdmissions(town,arena);
        }finally{
            for(int i=0;i<fields.Length;i++)fields[i].SetValue(null,values[i]);
            alive.Clear();foreach(uint uid in previous)alive.Add(uid);
        }
    }

    static void VerifyLmsAdmissions(Type lms,Map town,Map arena) {
        var admissions=typeof(Entity).Assembly.GetType("Revora.Game.Features.LmsAdmissions");
        var registered=(HashSet<uint>)lms.GetField("Registered",Hidden).GetValue(null);
        var alive=(HashSet<uint>)lms.GetField("Alive",Hidden).GetValue(null);
        var pending=(HashSet<uint>)lms.GetField("PendingReturns",Hidden).GetValue(null);
        var a=PkPlayer(town,140);var b=PkPlayer(town,140);
        SavePkProfileBeforeClaim(a);SavePkProfileBeforeClaim(b);
        a.Entity.PKMode=Enums.PKMode.Peace;b.Entity.PKMode=Enums.PKMode.Capture;
        registered.Clear();pending.Clear();alive.Clear();
        lms.GetField("EventMap",Hidden).SetValue(null,arena);
        lms.GetField("Running",Hidden).SetValue(null,false);
        lms.GetField("CycleStart",Hidden).SetValue(null,new DateTime(2030,1,2,0,0,0));
        lms.GetField("RegisterOpen",Hidden).SetValue(null,true);
        Call(lms,null,"TryRegister",a);Call(lms,null,"TryRegister",b);
        Check(registered.Count==2,"ordinary players register for the actual LMS admission path");
        Query("CREATE FUNCTION lms_reject_admission() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER lms_reject_admission BEFORE INSERT ON revora_lms_admissions FOR EACH ROW EXECUTE FUNCTION lms_reject_admission()");
        try{Call(lms,null,"StartFight");}
        finally{Query("DROP TRIGGER lms_reject_admission ON revora_lms_admissions;DROP FUNCTION lms_reject_admission()");}
        Check(a.Entity.MapID==1002 && b.Entity.MapID==1002 && !(bool)lms.GetField("Running",Hidden).GetValue(null)
            && CtfCount("SELECT count(*) FROM revora_lms_admissions WHERE cycle='203001020000'")==0,
            "failed durable LMS admission transfers nobody and leaves no partial roster");
        Revora.Game.Features.LastManStandingSystem.Tick();
        lms.GetField("CycleStart",Hidden).SetValue(null,new DateTime(2030,1,2,2,0,0));
        lms.GetField("RegisterOpen",Hidden).SetValue(null,true);
        Call(lms,null,"TryRegister",a);Call(lms,null,"TryRegister",b);Call(lms,null,"StartFight");
        Check(a.Entity.MapID==arena.ID && b.Entity.MapID==arena.ID && a.Entity.PKMode==Enums.PKMode.PK
            && Scalar("SELECT count(*) FROM revora_lms_admissions WHERE uid=@uid AND cycle='203001020200' AND pkmode=1 AND NOT ended",a.Entity.UID)==1
            && Scalar("SELECT count(*) FROM revora_lms_admissions WHERE uid=@uid AND cycle='203001020200' AND pkmode=3 AND NOT ended",b.Entity.UID)==1,
            "actual LMS start reserves both original combat modes before transferring ordinary players");
        SavePkProfileBeforeClaim(a);SavePkProfileBeforeClaim(b);
        long wins=CtfCount("SELECT count(*) FROM revora_lms_victories");
        Call(admissions,null,"RecoverInterrupted");Call(admissions,null,"RecoverInterrupted");
        lms.GetField("Running",Hidden).SetValue(null,false);registered.Clear();alive.Clear();pending.Clear();
        a=ReloadBall(a);Revora.Game.Features.LastManStandingSystem.OnLogin(a);
        Check(!Fenced(a) && a.Entity.MapID==1002 && a.Entity.PKMode==Enums.PKMode.Peace
            && Scalar("SELECT count(*) FROM revora_lms_admissions WHERE uid=@uid AND ended AND returned",a.Entity.UID)==1
            && Scalar("SELECT mapid FROM entities WHERE uid=@uid",a.Entity.UID)==1002,
            "startup reservation recovery and login save the safe town destination and restore the original combat mode");
        Query("CREATE FUNCTION lms_reject_return() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.returned THEN RETURN NULL; END IF; RETURN NEW; END $$; CREATE TRIGGER lms_reject_return BEFORE UPDATE ON revora_lms_admissions FOR EACH ROW EXECUTE FUNCTION lms_reject_return()");
        try{b=ReloadBall(b);Revora.Game.Features.LastManStandingSystem.OnLogin(b);}
        finally{Query("DROP TRIGGER lms_reject_return ON revora_lms_admissions;DROP FUNCTION lms_reject_return()");}
        Check(Fenced(b) && Scalar("SELECT mapid FROM entities WHERE uid=@uid",b.Entity.UID)==1002
            && Scalar("SELECT count(*) FROM revora_lms_admissions WHERE uid=@uid AND ended AND NOT returned",b.Entity.UID)==1,
            "failed LMS return acknowledgement keeps its retry receipt after saving the safe destination");
        b=ReloadBall(b);Revora.Game.Features.LastManStandingSystem.OnLogin(b);Revora.Game.Features.LastManStandingSystem.OnLogin(b);
        Check(!Fenced(b) && b.Entity.PKMode==Enums.PKMode.Capture
            && Scalar("SELECT count(*) FROM revora_lms_admissions WHERE uid=@uid AND returned",b.Entity.UID)==1
            && CtfCount("SELECT count(*) FROM revora_lms_victories")==wins,
            "reconnect retries LMS return once without inventing a winner for an interrupted round");
        lms.GetField("CycleStart",Hidden).SetValue(null,new DateTime(2030,1,2,4,0,0));
        lms.GetField("RegisterOpen",Hidden).SetValue(null,true);
        Call(lms,null,"TryRegister",a);Call(lms,null,"TryRegister",b);Call(lms,null,"StartFight");
        Call(lms,null,"SweepAndCheck",true);
        Check(a.Entity.MapID==1002 && b.Entity.MapID==1002 && a.Entity.PKMode==Enums.PKMode.Peace && b.Entity.PKMode==Enums.PKMode.Capture
            && CtfCount("SELECT count(*) FROM revora_lms_admissions WHERE cycle='203001020400' AND returned")==2,
            "actual LMS timeout durably returns both surviving fighters and restores their different combat modes");
        lms.GetField("CycleStart",Hidden).SetValue(null,new DateTime(2030,1,2,6,0,0));
        EntityVariableTable.Load(a);EntityVariableTable.Load(b);
        lms.GetField("RegisterOpen",Hidden).SetValue(null,true);
        Call(lms,null,"TryRegister",a);Call(lms,null,"TryRegister",b);
        using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
            var actor=a;var task=Task.Run(()=>{lock(actor.ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
            try{Call(lms,null,"StartFight");
                Check(!(bool)lms.GetField("Running",Hidden).GetValue(null) && a.Entity.MapID==1002
                    && CtfCount("SELECT count(*) FROM revora_lms_admissions WHERE cycle='203001020600'")==0,
                    "busy LMS admission actor defers the whole roster without teleporting or reserving a partial match");
            }finally{release.Set();task.Wait();}
        }
        Call(lms,null,"StartFight");
        Revora.Game.Features.LastManStandingSystem.OnDeath(a);
        Check(!(bool)lms.GetField("Running",Hidden).GetValue(null) && a.Entity.MapID==1002 && b.Entity.MapID==1002
            && a.Entity.PKMode==Enums.PKMode.Peace && b.Entity.PKMode==Enums.PKMode.Capture
            && Scalar("SELECT count(*) FROM revora_lms_victories WHERE uid=@uid AND cycle='203001020600'",b.Entity.UID)==1
            && CtfCount("SELECT count(*) FROM revora_lms_admissions WHERE cycle='203001020600' AND returned")==2,
            "actual LMS elimination finalizes the surviving winner and restores both participants through saved returns");
        lms.GetField("CycleStart",Hidden).SetValue(null,new DateTime(2030,1,2,8,0,0));
        lms.GetField("RegisterOpen",Hidden).SetValue(null,true);
        Call(lms,null,"TryRegister",a);Call(lms,null,"TryRegister",b);
        string path=DMaps.MapPaths[700];DMaps.MapPaths.Remove(700);
        try{Call(lms,null,"StartFight");}
        finally{DMaps.MapPaths[700]=path;}
        Check((bool)lms.GetField("AbortRound",Hidden).GetValue(null)
            && CtfCount("SELECT count(*) FROM revora_lms_admissions WHERE cycle='203001020800'")==2,
            "unavailable arena transfer retains the reserved roster and schedules cancellation");
        Call(lms,null,"SweepAndCheck",true);Revora.Game.Features.LastManStandingSystem.Tick();
        Check(!(bool)lms.GetField("Running",Hidden).GetValue(null) && !Fenced(a) && !Fenced(b)
            && a.Entity.PKMode==Enums.PKMode.Peace && b.Entity.PKMode==Enums.PKMode.Capture
            && CtfCount("SELECT count(*) FROM revora_lms_admissions WHERE cycle='203001020800' AND returned")==2
            && CtfCount("SELECT count(*) FROM revora_lms_victories WHERE cycle='203001020800'")==0,
            "failed LMS transfer cancels safely, restores both reserved players and cannot mint a victory");
        Call(lms,null,"TryClaim",b);
        var earnedSouls=b.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i).ToArray();
        Check(earnedSouls.Length==1 && b.Inventory.Objects.All(i=>i.Bound && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7),
            "ordinary surviving LMS fighter earns a bound P7 soul through the actual elimination and claim route");
        b=ReloadBall(b);EntityVariableTable.Load(b);Call(lms,null,"TryClaim",b);
        Check(b.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i).SequenceEqual(earnedSouls)
            && (uint)b[Revora.Game.Features.LastManStandingSystem.ClaimKey]==0,
            "earned LMS soul survives fresh reload and claim replay with its original identity");

    }
    static void VerifyRelicAdmissions(Map town,Map arena) {
        var relic=typeof(Revora.Game.Features.RelicRushSystem);
        Call(relic,null,"InitializePrizes");
        Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.RelicAdmissions"),null,"Initialize");
        var admissions=typeof(Entity).Assembly.GetType("Revora.Game.Features.RelicAdmissions");
        var registered=(HashSet<uint>)relic.GetField("Registered",Hidden).GetValue(null);
        var alive=(HashSet<uint>)relic.GetField("Alive",Hidden).GetValue(null);
        var pending=(HashSet<uint>)relic.GetField("PendingReturns",Hidden).GetValue(null);
        var a=PkPlayer(town,140);var b=PkPlayer(town,140);
        SavePkProfileBeforeClaim(a);SavePkProfileBeforeClaim(b);
        a.Entity.PKMode=Enums.PKMode.Peace;b.Entity.PKMode=Enums.PKMode.Capture;
        registered.Clear();pending.Clear();alive.Clear();
        relic.GetField("EventMap",Hidden).SetValue(null,arena);
        relic.GetField("Running",Hidden).SetValue(null,false);
        relic.GetField("CycleStart",Hidden).SetValue(null,new DateTime(2030,1,2,0,0,0));
        relic.GetField("RegisterOpen",Hidden).SetValue(null,true);
        Call(relic,null,"TryRegister",a);Call(relic,null,"TryRegister",b);
        Check(registered.Count==2,"ordinary players register for the actual Relic Rush admission path");
        Query("CREATE FUNCTION relic_reject_admission() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER relic_reject_admission BEFORE INSERT ON revora_relic_admissions FOR EACH ROW EXECUTE FUNCTION relic_reject_admission()");
        try{Call(relic,null,"StartFight");}
        finally{Query("DROP TRIGGER relic_reject_admission ON revora_relic_admissions;DROP FUNCTION relic_reject_admission()");}
        Check(a.Entity.MapID==1002 && b.Entity.MapID==1002 && !(bool)relic.GetField("Running",Hidden).GetValue(null)
            && CtfCount("SELECT count(*) FROM revora_relic_admissions WHERE cycle='203001020000'")==0,
            "failed durable Relic Rush admission transfers nobody and leaves no partial roster");
        Revora.Game.Features.RelicRushSystem.Tick();
        relic.GetField("CycleStart",Hidden).SetValue(null,new DateTime(2030,1,2,2,0,0));
        relic.GetField("RegisterOpen",Hidden).SetValue(null,true);
        Call(relic,null,"TryRegister",a);Call(relic,null,"TryRegister",b);Call(relic,null,"StartFight");
        Check(a.Entity.MapID==arena.ID && b.Entity.MapID==arena.ID && a.Entity.PKMode==Enums.PKMode.PK
            && Scalar("SELECT count(*) FROM revora_relic_admissions WHERE uid=@uid AND cycle='203001020200' AND pkmode=1 AND NOT ended",a.Entity.UID)==1
            && Scalar("SELECT count(*) FROM revora_relic_admissions WHERE uid=@uid AND cycle='203001020200' AND pkmode=3 AND NOT ended",b.Entity.UID)==1,
            "actual Relic Rush start reserves both original combat modes before transferring ordinary players");
        SavePkProfileBeforeClaim(a);SavePkProfileBeforeClaim(b);
        long wins=CtfCount("SELECT count(*) FROM revora_relic_placements");
        Call(admissions,null,"RecoverInterrupted");Call(admissions,null,"RecoverInterrupted");
        relic.GetField("Running",Hidden).SetValue(null,false);registered.Clear();alive.Clear();pending.Clear();
        a=ReloadBall(a);Revora.Game.Features.RelicRushSystem.OnLogin(a);
        Check(!Fenced(a) && a.Entity.MapID==1002 && a.Entity.PKMode==Enums.PKMode.Peace
            && Scalar("SELECT count(*) FROM revora_relic_admissions WHERE uid=@uid AND ended AND returned",a.Entity.UID)==1
            && Scalar("SELECT mapid FROM entities WHERE uid=@uid",a.Entity.UID)==1002,
            "startup reservation recovery and login save the safe town destination and restore the original combat mode");
        Query("CREATE FUNCTION relic_reject_return() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.returned THEN RETURN NULL; END IF; RETURN NEW; END $$; CREATE TRIGGER relic_reject_return BEFORE UPDATE ON revora_relic_admissions FOR EACH ROW EXECUTE FUNCTION relic_reject_return()");
        try{b=ReloadBall(b);Revora.Game.Features.RelicRushSystem.OnLogin(b);}
        finally{Query("DROP TRIGGER relic_reject_return ON revora_relic_admissions;DROP FUNCTION relic_reject_return()");}
        Check(Fenced(b) && Scalar("SELECT mapid FROM entities WHERE uid=@uid",b.Entity.UID)==1002
            && Scalar("SELECT count(*) FROM revora_relic_admissions WHERE uid=@uid AND ended AND NOT returned",b.Entity.UID)==1,
            "failed Relic Rush return acknowledgement keeps its retry receipt after saving the safe destination");
        b=ReloadBall(b);Revora.Game.Features.RelicRushSystem.OnLogin(b);Revora.Game.Features.RelicRushSystem.OnLogin(b);
        Check(!Fenced(b) && b.Entity.PKMode==Enums.PKMode.Capture
            && Scalar("SELECT count(*) FROM revora_relic_admissions WHERE uid=@uid AND returned",b.Entity.UID)==1
            && CtfCount("SELECT count(*) FROM revora_relic_placements")==wins,
            "reconnect retries Relic Rush return once without inventing a winner for an interrupted round");
        relic.GetField("CycleStart",Hidden).SetValue(null,new DateTime(2030,1,2,4,0,0));
        relic.GetField("RegisterOpen",Hidden).SetValue(null,true);
        Call(relic,null,"TryRegister",a);Call(relic,null,"TryRegister",b);Call(relic,null,"StartFight");
        var scores=(Dictionary<uint,uint>)relic.GetField("Scores",Hidden).GetValue(null);
        scores[a.Entity.UID]=5;scores[b.Entity.UID]=3;
        Revora.Game.Features.RelicRushSystem.OnDeath(a);
        Check(a.Entity.MapID==1002 && a.Entity.PKMode==Enums.PKMode.Peace && b.Entity.MapID==arena.ID,
            "Relic Rush death restores only the eliminated fighter and retains their score");
        Call(relic,null,"FinishByScore");
        Check(b.Entity.MapID==1002 && b.Entity.PKMode==Enums.PKMode.Capture
            && CtfCount("SELECT count(*) FROM revora_relic_admissions WHERE cycle='203001020400' AND returned")==2
            && CtfCount("SELECT count(*) FROM revora_relic_placements WHERE cycle='203001020400'")==2,
            "Relic Rush finish saves ranked prizes and durably returns both original combat modes");
        relic.GetField("CycleStart",Hidden).SetValue(null,new DateTime(2030,1,2,6,0,0));
        relic.GetField("RegisterOpen",Hidden).SetValue(null,true);Call(relic,null,"TryRegister",a);Call(relic,null,"TryRegister",b);
        string path=DMaps.MapPaths[700];DMaps.MapPaths.Remove(700);
        try{Call(relic,null,"StartFight");}finally{DMaps.MapPaths[700]=path;}
        Check((bool)relic.GetField("AbortRound",Hidden).GetValue(null),"failed Relic Rush transfer schedules safe cancellation");
        Revora.Game.Features.RelicRushSystem.Tick();
        Check(a.Entity.MapID==1002 && b.Entity.MapID==1002 && a.Entity.PKMode==Enums.PKMode.Peace && b.Entity.PKMode==Enums.PKMode.Capture
            && CtfCount("SELECT count(*) FROM revora_relic_admissions WHERE cycle='203001020600' AND returned")==2
            && CtfCount("SELECT count(*) FROM revora_relic_placements WHERE cycle='203001020600'")==0,
            "failed Relic Rush entry returns the reserved roster and creates no ranked prizes");
    }
}
