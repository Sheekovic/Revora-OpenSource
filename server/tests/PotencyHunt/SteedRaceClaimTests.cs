using System;
using System.Threading.Tasks;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.Features;
using Revora.Network.GamePackets;
internal static partial class PotencyHuntTests {
    static void VerifySteedClaims(string runtime,Map town) {
        Query("DROP TABLE entities;CREATE TABLE entities AS TABLE public.entities WITH NO DATA;CREATE TABLE titles (LIKE public.titles INCLUDING ALL)");
        foreach(string table in new[]{"teamarena","championarena","quests","status","reward","rewardclameid","merchant","dialyquests","activeness","house","pkexploits","achievement","itemadding"})
            Query("CREATE TABLE IF NOT EXISTS "+table+" AS TABLE public."+table+" WITH NO DATA");
        Query("CREATE TABLE prizes(uid bigint PRIMARY KEY,name text,prizes bytea)");
        StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
        RunesSystem.LoadRunesEXP();SpellTable.Load();Call(EvidenceType,null,"Initialize");
        foreach(ushort map in new ushort[]{1011,1020,1000,1004})World(runtime,map);
        ConquerItem.ItemUID=new Counter(7900000);Server.GamePool=new GameState[0];
        Query("CREATE TABLE IF NOT EXISTS variablevault(entityid bigint PRIMARY KEY,data bytea NOT NULL)");
        var service=typeof(Entity).Assembly.GetType("Revora.Game.Features.SteedRaceClaims");Call(service,null,"Initialize");
        town.Npcs[SteedRaceSystem.NpcId]=new NpcSpawn{UID=SteedRaceSystem.NpcId,MapID=1002,X=SteedRaceSystem.ClaimX,Y=SteedRaceSystem.ClaimY,Mesh=SteedRaceSystem.Mesh,Type=Enums.NpcType.Talker};
        Action<GameState> near=p=>{p.BindWorldMap(1002);p.Entity.X=SteedRaceSystem.ClaimX;p.Entity.Y=SteedRaceSystem.ClaimY;p.ActiveNpc=SteedRaceSystem.NpcId;};
        Func<uint,GameState> player=rank=>{var p=FullRunePlayer(town,15);p.Entity.DailySignDays=new byte[0];near(p);EntityVariableTable.Load(p);
            p[SteedRaceSystem.ClaimKey]=rank;p["steed-test-unrelated"]=73u;EntityVariableTable.Save(p);if(!EntityTable.SaveEntity(p))throw new Exception("Steed fixture save failed.");return p;};
        Action<GameState> claim=p=>SteedRaceSystem.HandleNpc(p,new NpcRequest(true){NpcID=SteedRaceSystem.NpcId,OptionID=2});
        Func<GameState,long> receipts=p=>Scalar("SELECT count(*) FROM revora_steed_claims WHERE uid=@uid",p.Entity.UID);
        var projection=service.GetField("AfterCommit",Hidden);object oldProjection=projection.GetValue(null);
        try {
            for(uint rank=1;rank<=3;rank++) {
                var p=player(rank);uint cp=p.Entity.ConquerPoints,rp=p.Entity.RevoraPoint;ulong gold=p.Entity.Money;
                uint awardCp=rank==1?Config.SteedRaceFirstCp:rank==2?Config.SteedRaceSecondCp:Config.SteedRaceThirdCp;
                uint awardRp=rank==1?Config.SteedRaceFirstRevoraPoints:rank==2?Config.SteedRaceSecondRevoraPoints:Config.SteedRaceThirdRevoraPoints;
                ulong awardGold=rank==1?Config.SteedRaceFirstGold:rank==2?Config.SteedRaceSecondGold:Config.SteedRaceThirdGold;
                Parallel.For(0,8,n=>claim(p));
                Check(!Fenced(p) && p.Account.State==AccountTable.AccountState.Player && p.Entity.ConquerPoints==cp+awardCp && p.Entity.RevoraPoint==rp+awardRp
                    && p.Entity.Money==gold+awardGold && (uint)p[SteedRaceSystem.ClaimKey]==0 && receipts(p)==1,"concurrent ordinary rank "+rank+" claim pays all three purses once");
                p=ReloadBall(p);EntityVariableTable.Load(p);near(p);claim(p);
                Check(!Fenced(p) && p.Entity.ConquerPoints==cp+awardCp && p.Entity.RevoraPoint==rp+awardRp && p.Entity.Money==gold+awardGold
                    && (uint)p["steed-test-unrelated"]==73 && receipts(p)==1,"rank "+rank+" purses, unrelated variables and consumed claim survive full reload");
            }
            Check(CtfCount("SELECT count(*) FROM revora_steed_claims WHERE run IS NULL AND item_uid<>0")==0,
                "legacy rank-only race prizes gain no retroactive soul");
            foreach(string fault in new[]{"cp","gold","vault","receipt"}) {
                var p=player(1);uint cp=p.Entity.ConquerPoints,rp=p.Entity.RevoraPoint;ulong gold=p.Entity.Money;
                string table=fault=="vault"?"variablevault":fault=="receipt"?"revora_steed_claims":"entities";
                string verb=fault=="receipt"?"INSERT":fault=="cp"?"UPDATE OF conquerpoints":fault=="gold"?"UPDATE OF money":"UPDATE";
                Query("CREATE FUNCTION suppress_steed() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
                Query("CREATE TRIGGER suppress_steed BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_steed()");
                try{claim(p);}finally{Query("DROP TRIGGER suppress_steed ON "+table);Query("DROP FUNCTION suppress_steed()");}
                Check(Fenced(p) && receipts(p)==0 && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==cp
                    && Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",p.Entity.UID)==rp && Scalar("SELECT money FROM entities WHERE uid=@uid",p.Entity.UID)==(long)gold,
                    "suppressed "+fault+" write rolls back every race purse and receipt");
                p=ReloadBall(p);EntityVariableTable.Load(p);near(p);
                Check((uint)p[SteedRaceSystem.ClaimKey]==1,"failed "+fault+" claim remains available after reload");claim(p);claim(p);
                Check(!Fenced(p) && receipts(p)==1 && p.Entity.ConquerPoints==cp+Config.SteedRaceFirstCp && p.Entity.RevoraPoint==rp+Config.SteedRaceFirstRevoraPoints
                    && p.Entity.Money==gold+Config.SteedRaceFirstGold,"reloaded "+fault+" failure retries the entire prize once");
            }
            foreach(uint invalid in new uint[]{0,4,uint.MaxValue}) {var p=player(invalid);uint cp=p.Entity.ConquerPoints;claim(p);
                Check(!Fenced(p) && receipts(p)==0 && p.Entity.ConquerPoints==cp && (uint)p[SteedRaceSystem.ClaimKey]==invalid,"invalid race rank cannot pay first-place prize: "+invalid);}
            foreach(string guard in new[]{"remote","trade","overflow","unsaved"}) {
                var p=player(1);if(guard=="remote")p.Entity.X+=20;if(guard=="trade")p.Trade.InTrade=true;
                if(guard=="overflow"){p.Entity.ConquerPoints=uint.MaxValue;EntityTable.SaveEntity(p);}
                if(guard=="unsaved"){p[SteedRaceSystem.ClaimKey]=0u;EntityVariableTable.Save(p);p[SteedRaceSystem.ClaimKey]=1u;}
                claim(p);Check(receipts(p)==0 && (uint)p[SteedRaceSystem.ClaimKey]==1 && Fenced(p)==(guard=="unsaved"),"ineligible race claim preserves pending marker without payment: "+guard);
            }
            var registration=player(1);registration.Spells[SteedRaceSystem.RideSpell]=new Spell(true){ID=SteedRaceSystem.RideSpell};
            string denied=(string)Call(typeof(SteedRaceSystem),null,"TryRegister",registration);
            Check(denied.Contains("previous prize"),"pending saved race prize prevents registration from replacing its entitlement");
            var stale=player(1);uint staleCp=stale.Entity.ConquerPoints;
            Query("UPDATE entities SET conquerpoints=conquerpoints+1 WHERE uid=@uid","uid",(long)stale.Entity.UID);claim(stale);
            Check(Fenced(stale) && receipts(stale)==0 && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",stale.Entity.UID)==staleCp+1,
                "concurrent durable purse change cannot be overwritten by race payout");
            stale=ReloadBall(stale);EntityVariableTable.Load(stale);near(stale);claim(stale);
            Check(!Fenced(stale) && receipts(stale)==1 && stale.Entity.ConquerPoints==staleCp+1+Config.SteedRaceFirstCp,
                "reloaded race claimant retains external purse change and receives its pending prize once");
            var projected=player(2);uint oldCp=projected.Entity.ConquerPoints;
            projection.SetValue(null,(Action)(()=>{throw new InvalidOperationException("Steed projection fixture");}));claim(projected);projection.SetValue(null,null);
            Check(Fenced(projected) && receipts(projected)==1,"postcommit race projection failure keeps a single committed receipt");
            projected=ReloadBall(projected);EntityVariableTable.Load(projected);near(projected);claim(projected);
            Check(!Fenced(projected) && projected.Entity.ConquerPoints==oldCp+Config.SteedRaceSecondCp && (uint)projected[SteedRaceSystem.ClaimKey]==0 && receipts(projected)==1,
                "production reload recovers paid race claim without a duplicate payout");
            VerifySteedResults(runtime,town,player,near,claim);
        }finally{projection.SetValue(null,oldProjection);}
    }
    static void VerifySteedResults(string runtime,Map town,Func<uint,GameState> player,Action<GameState> near,Action<GameState> claim) {
        var course=typeof(Entity).Assembly.GetType("Revora.Game.Features.SteedRaceCourse");
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.SteedRaceResults");
        var clock=store.GetField("Clock",Hidden);var projection=store.GetField("AfterFinishCommit",Hidden);
        object oldClock=clock.GetValue(null),oldProjection=projection.GetValue(null);
        DateTime now=new DateTime(2043,1,1,0,0,0,DateTimeKind.Utc);clock.SetValue(null,(Func<DateTime>)(()=>now));
        Func<GameState> racer=()=>{var p=player(0);p.Spells[SteedRaceSystem.RideSpell]=new Spell(true){ID=SteedRaceSystem.RideSpell};
            var mount=BreedMount(p);ItemHandler.EquipItem(new ItemUsage(true){UID=mount.UID,dwParam=ConquerItem.Steed,Action=ItemUsage.Mode.EquipItem},p);
            if(p.Equipment.Free(ConquerItem.Steed))throw new Exception("Race fixture mount failed.");p.Entity.AddFlag((ulong)FlagsUpdate.Flags.Ride,StatusFlagsBigVector32.PermanentFlag,true);return p;};
        Func<Guid> begin=()=>{var id=(Guid)Call(store,null,"Begin",(ushort)1002,(ushort)370,(ushort)311);Call(course,null,"Configure",id,town,(ushort)370,(ushort)341,(ushort)370,(ushort)311,4);return id;};
        Func<Guid,GameState,bool> admit=(run,p)=>{bool ok=(bool)Call(store,null,"Admit",run,p);if(ok)p.Entity.Teleport(1002,370,341);return ok;};
        Func<Guid,GameState,int> finish=(run,p)=>{lock(p.ItemSyncRoot){if(p.Entity.X==370 && p.Entity.Y==341 && (bool)Call(course,null,"CanMove",p))RaceWalkTo(p,370,311,4);return (int)Call(store,null,"Finish",run,p);}};
        try {
            var run=begin();var p=racer();var stranger=racer();
            Check(admit(run,p) && !admit(run,p),"durable race admission accepts a mounted ordinary character only once");
            Check(finish(run,p)==0,"admitted racer cannot finish before gate opening");Call(store,null,"Open",run);
            Check(!admit(run,stranger) && finish(run,stranger)==0,"late join and unadmitted finish cannot consume a placing");
            p.Entity.X+=30;Check(finish(run,p)==0,"admitted finisher must reach the configured finish region");p.Entity.X=370;p.Entity.Y=341;
            int[] ranks=new int[8];Parallel.For(0,8,n=>ranks[n]=finish(run,p));
            Check(ranks.Count(r=>r==1)==1 && ranks.Count(r=>r==0)==7 && (uint)p[SteedRaceSystem.ClaimKey]==1
                && Scalar("SELECT count(*) FROM revora_steed_results WHERE uid=@uid AND rank=1",p.Entity.UID)==1,"concurrent duplicate finishes record one first place and one pending prize");
            Check(HasAchievement(p,11113) && HasAchievement(p,11122),"ordinary winner immediately receives completion and victory achievements");
            uint frozen=Config.SteedRaceFirstCp,oldCp=p.Entity.ConquerPoints;Config.SteedRaceFirstCp=frozen+123;
            try{p=ReloadBall(p);EntityVariableTable.Load(p);near(p);claim(p);claim(p);
                Check(!Fenced(p) && p.Entity.ConquerPoints==oldCp+frozen && Scalar("SELECT count(*) FROM revora_steed_results WHERE uid=@uid AND claimed_at IS NOT NULL",p.Entity.UID)==1,
                    "reloaded race winner receives the start-frozen prize despite a later configuration change");}
            finally{Config.SteedRaceFirstCp=frozen;}
            p.Entity.MyAchievement.Send();Check(HasAchievement(p,11113) && HasAchievement(p,11122),"race completion and victory survive full production reload");
            foreach(string fault in new[]{"result","rank","vault","achievement"}) {
                run=begin();p=racer();var other=racer();admit(run,p);admit(run,other);Call(store,null,"Open",run);
                string table=fault=="result"?"revora_steed_results":fault=="rank"?"revora_steed_runs":fault=="achievement"?"revora_achievement_evidence":"variablevault";
                Query("CREATE FUNCTION suppress_finish() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
                Query("CREATE TRIGGER suppress_finish BEFORE "+(fault=="achievement"?"INSERT":"UPDATE")+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_finish()");
                try{Check(finish(run,p)==0 && Fenced(p),"suppressed race "+fault+" write rejects and fences finisher");}
                finally{Query("DROP TRIGGER suppress_finish ON "+table);Query("DROP FUNCTION suppress_finish()");}
                Check(Scalar("SELECT count(*) FROM revora_steed_results WHERE uid=@uid AND rank IS NOT NULL",p.Entity.UID)==0 && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id IN (11113,11122)",p.Entity.UID)==0,
                    "failed "+fault+" finish leaves no durable placing, prize or race achievements");
                Check(finish(run,other)==1,"same race retains first place for another admitted racer after "+fault+" rollback");
            }
            run=begin();var people=Enumerable.Range(0,4).Select(n=>racer()).ToArray();foreach(var entrant in people)admit(run,entrant);Call(store,null,"Open",run);
            int[] places=new int[4];Parallel.For(0,4,n=>places[n]=finish(run,people[n]));
            Check(places.OrderBy(n=>n).SequenceEqual(new[]{1,2,3,4}) && people.Count(q=>(uint)q[SteedRaceSystem.ClaimKey]>0)==3,
                "concurrent admitted racers receive distinct first through fourth places with prizes only for the top three");
            Check(people.All(q=>HasAchievement(q,11113)) && people.Count(q=>HasAchievement(q,11122))==1,
                "all finishers including fourth earn completion; only first earns victory");
            foreach(var placed in people) {
                int place=(int)Scalar("SELECT rank FROM revora_steed_results WHERE uid=@uid",placed.Entity.UID);
                uint soulType=(uint)Scalar("SELECT soul_type FROM revora_steed_results WHERE uid=@uid",placed.Entity.UID);
                near(placed);claim(placed);
                Check(place<=3 ? soulType!=0 && placed.Inventory.Objects.Any(i=>i.ID==soulType && i.Bound)
                    && Scalar("SELECT item_uid FROM revora_steed_claims WHERE uid=@uid",placed.Entity.UID)>0
                    : soulType==0 && Scalar("SELECT count(*) FROM revora_steed_claims WHERE uid=@uid",placed.Entity.UID)==0,
                    "only top-three finishers receive the saved bound soul: rank "+place);
            }
            run=begin();p=racer();admit(run,p);Call(store,null,"Open",run);Call(store,null,"Withdraw",p);
            Check(finish(run,p)==0,"withdrawn or disconnected participant cannot finish using an old admission");
            run=begin();p=racer();admit(run,p);Call(store,null,"Open",run);now=now.AddMinutes(11);
            Check(finish(run,p)==0,"expired race cannot accept a late finish");
            run=begin();p=racer();admit(run,p);Call(store,null,"Open",run);
            projection.SetValue(null,(Action)(()=>{throw new InvalidOperationException("Race finish projection fixture");}));Check(finish(run,p)==0 && Fenced(p),"postcommit finish projection failure fences without erasing the durable result");projection.SetValue(null,null);
            p=ReloadBall(p);EntityVariableTable.Load(p);near(p);claim(p);
            Check(!Fenced(p) && (uint)p[SteedRaceSystem.ClaimKey]==0 && Scalar("SELECT count(*) FROM revora_steed_results WHERE uid=@uid AND claimed_at IS NOT NULL",p.Entity.UID)==1,
                "production reload recovers a committed finish prize after projection failure");
            run=begin();p=racer();var unfinished=racer();admit(run,p);admit(run,unfinished);Call(store,null,"Open",run);Check(finish(run,p)==1,"restart fixture records a genuine admitted result");
            store.GetField("initialized",Hidden).SetValue(null,false);Call(store,null,"Initialize");
            Check(finish(run,unfinished)==0 && Scalar("SELECT count(*) FROM revora_steed_results r JOIN revora_steed_runs s ON s.id=r.run WHERE r.uid=@uid AND s.state='aborted'",unfinished.Entity.UID)==1,
                "startup aborts interrupted runs and invalidates unfinished admissions");
            p=ReloadBall(p);EntityVariableTable.Load(p);near(p);uint paidBefore=p.Entity.ConquerPoints;
            Query("CREATE FUNCTION suppress_race_paid() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
            Query("CREATE TRIGGER suppress_race_paid BEFORE UPDATE OF claimed_at ON revora_steed_results FOR EACH ROW EXECUTE FUNCTION suppress_race_paid()");
            try{claim(p);}finally{Query("DROP TRIGGER suppress_race_paid ON revora_steed_results");Query("DROP FUNCTION suppress_race_paid()");}
            Check(Fenced(p) && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==paidBefore
                && Scalar("SELECT count(*) FROM revora_steed_results WHERE uid=@uid AND claimed_at IS NULL AND rank=1",p.Entity.UID)==1,
                "suppressed result consumption rolls back the entire frozen prize and preserves its entitlement");
            p=ReloadBall(p);EntityVariableTable.Load(p);near(p);claim(p);
            Check(!Fenced(p) && p.Entity.ConquerPoints==paidBefore+Config.SteedRaceFirstCp
                && Scalar("SELECT count(*) FROM revora_steed_claims WHERE uid=@uid AND run IS NOT NULL",p.Entity.UID)==1,
                "completed prize survives startup abortion of unfinished race and retry pays its frozen result once");
            foreach(string fault in new[]{"full-bag","item","receipt","projection","old-run"}) {
                run=begin();p=racer();admit(run,p);
                if(fault=="old-run")Query("UPDATE revora_steed_runs SET soul_types=ARRAY[0,0,0]::bigint[] WHERE id=@run","run",run);
                Call(store,null,"Open",run);Check(finish(run,p)==1,"reward fixture earns first place through accepted movement: "+fault);
                uint expectedSoul=(uint)Scalar("SELECT soul_type FROM revora_steed_results WHERE uid=@uid",p.Entity.UID);
                uint beforeCp=p.Entity.ConquerPoints;ulong beforeGold=p.Entity.Money;
                near(p);var claimProjection=typeof(Entity).Assembly.GetType("Revora.Game.Features.SteedRaceClaims").GetField("AfterCommit",Hidden);
                if(fault=="full-bag") {
                    var fillers=Enumerable.Range(0,40-p.Inventory.Count).Select(i=>Pack(p,1088001,1)).ToArray();claim(p);
                    Check(!Fenced(p) && p.Entity.ConquerPoints==beforeCp && p.Entity.Money==beforeGold
                        && Scalar("SELECT count(*) FROM revora_steed_claims WHERE uid=@uid",p.Entity.UID)==0,
                        "full bag preserves the complete race purse and soul");
                    foreach(var item in fillers){Query("DELETE FROM items WHERE uid=@id","id",(long)item.UID);p.Inventory.Remove(item.UID,Enums.ItemUse.None,false);}
                } else if(fault=="item" || fault=="receipt") {
                    string table=fault=="item"?"items":"revora_steed_claims";
                    Query("CREATE FUNCTION suppress_soul_prize() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
                    Query("CREATE TRIGGER suppress_soul_prize BEFORE INSERT ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_soul_prize()");
                    try{claim(p);}finally{Query("DROP TRIGGER suppress_soul_prize ON "+table);Query("DROP FUNCTION suppress_soul_prize()");}
                    Check(Fenced(p) && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==beforeCp
                        && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id BETWEEN 800000 AND 899999",p.Entity.UID)==0
                        && Scalar("SELECT count(*) FROM revora_steed_claims WHERE uid=@uid",p.Entity.UID)==0,
                        "failed race "+fault+" insert rolls back the soul, purse and receipt");
                    p=ReloadBall(p);EntityVariableTable.Load(p);near(p);
                } else if(fault=="projection") {
                    claimProjection.SetValue(null,(Action)(()=>{throw new InvalidOperationException("Soul projection fixture");}));
                    try{claim(p);}finally{claimProjection.SetValue(null,null);}
                    Check(Fenced(p) && Scalar("SELECT count(*) FROM revora_steed_claims WHERE uid=@uid AND item_uid>0",p.Entity.UID)==1,
                        "postcommit soul projection failure retains its saved payout");
                    p=ReloadBall(p);EntityVariableTable.Load(p);near(p);
                }
                Parallel.For(0,8,i=>claim(p));
                Check(!Fenced(p) && p.Entity.ConquerPoints==beforeCp+Config.SteedRaceFirstCp && p.Entity.Money==beforeGold+Config.SteedRaceFirstGold
                    && Scalar("SELECT count(*) FROM revora_steed_claims WHERE uid=@uid",p.Entity.UID)==1,
                    "race prize retry pays the complete purse exactly once: "+fault);
                long itemUid=Scalar("SELECT item_uid FROM revora_steed_claims WHERE uid=@uid",p.Entity.UID);
                p=ReloadBall(p);EntityVariableTable.Load(p);near(p);claim(p);
                Check(fault=="old-run" ? expectedSoul==0 && itemUid==0
                    : itemUid>0 && p.Inventory.Objects.Count(i=>i.UID==(uint)itemUid && i.ID==expectedSoul && i.Bound)==1
                    && ConquerItemInformation.BaseInformations[expectedSoul].PurificationLevel==7,
                    "race reward edition and exact bound soul survive full reload: "+fault);
            }
            foreach(var row in new ushort[][] {
                new ushort[]{9929,568,14,30,563,6},new ushort[]{2066,633,432,85,371,6},new ushort[]{1950,88,149,420,431,4},
                new ushort[]{2064,175,250,200,153,6},new ushort[]{2061,60,400,899,816,10},new ushort[]{2063,450,520,682,484,10},new ushort[]{2062,150,350,330,170,6}}) {
                var map=World(runtime,row[0]);run=(Guid)Call(store,null,"Begin",row[0],row[3],row[4]);Call(course,null,"Configure",run,map,row[1],row[2],row[3],row[4],(int)row[5]);
                p=racer();Check((bool)Call(store,null,"Admit",run,p),"mounted entrant admitted to installed terrain "+row[0]);p.Entity.Teleport(row[0],row[1],row[2]);
                Check(!p.Entity.Move(Enums.ConquerAngle.South,1) && p.Entity.X==row[1] && p.Entity.Y==row[2],"pre-gate movement is blocked on track "+row[0]);
                Call(store,null,"Open",run);int walked=RaceWalkTo(p,row[3],row[4],row[5]);
                Check((int)Call(store,null,"Finish",run,p)==1 && walked>100
                    && Scalar("SELECT course_steps FROM revora_steed_results WHERE uid=@uid AND rank=1",p.Entity.UID)==walked,
                    "continuous accepted ground movement completes installed terrain with saved step count: "+row[0]+" steps="+walked);
            }
            run=begin();p=racer();admit(run,p);Call(store,null,"Open",run);p.Entity.Teleport(1002,370,311);
            Check((int)Call(store,null,"Finish",run,p)==0 && Scalar("SELECT count(*) FROM revora_steed_results WHERE uid=@uid AND rank IS NOT NULL",p.Entity.UID)==0,
                "teleporting directly into the finish region cannot create a placing");
            Check(!HasAchievement(p,11113) && !HasAchievement(p,11122),"teleporting to finish cannot earn racing achievements");
            Call(store,null,"End",run);
            VerifyScheduledSteedEntry(town,racer,near,claim);
        }finally{clock.SetValue(null,oldClock);projection.SetValue(null,oldProjection);}
    }

    static void VerifyScheduledSteedEntry(Map town,Func<GameState> racer,Action<GameState> near,Action<GameState> claim) {
        var system=typeof(SteedRaceSystem);
        var roster=(System.Collections.Generic.Dictionary<uint,GameState>)system.GetField("Registered",Hidden).GetValue(null);
        var oldThread=Server.Thread;var oldPool=Server.GamePool;
        var race=(SteedRace)Activator.CreateInstance(typeof(SteedRace),Instance,null,new object[]{false},null);
        Server.Thread=(Revora.Thread)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Revora.Thread));
        Server.Thread.SteedRace=race;
        Action open=()=>{Call(system,null,"ResetCycle");system.GetField("CycleStart",Hidden).SetValue(null,DateTime.Now.AddMinutes(1));SteedRaceSystem.Tick();};
        Action<GameState> register=p=>{near(p);Npcs.GetDialog(new NpcRequest(true){NpcID=SteedRaceSystem.NpcId,OptionID=1},p);};
        Action start=()=>{system.GetField("CycleStart",Hidden).SetValue(null,DateTime.Now.AddSeconds(-1));SteedRaceSystem.Tick();};
        try {
            var a=racer();var b=racer();Server.GamePool=new[]{a,b};open();
            a.Fake=true;register(a);a.Fake=false;b.Trade.InTrade=true;register(b);b.Trade.InTrade=false;
            Check(roster.Count==0,"fake and trading characters cannot fill scheduled race seats");
            register(a);register(a);register(b);
            Check(roster.Count==2 && ReferenceEquals(roster[a.Entity.UID],a),"ordinary NPC registration owns one seat per current session");
            SkillTable.SaveSpells(a);
            var old=a;a=ReloadBall(a);EntityVariableTable.Load(a);Server.GamePool=new[]{a,b};start();
            Check(!(bool)system.GetField("Running",Hidden).GetValue(null) && a.Entity.MapID==1002 && b.Entity.MapID==1002,
                "replacement session cannot inherit registration or satisfy the two-rider minimum");
            open();register(a);register(b);SteedRaceSystem.OnDisconnect(old);
            Check(roster.Count==2 && ReferenceEquals(roster[a.Entity.UID],a),"late old-session disconnect cannot remove a replacement's fresh registration");
            Kernel.GamePool[a.Entity.UID]=old;register(a);Kernel.GamePool[a.Entity.UID]=a;
            Check(ReferenceEquals(roster[a.Entity.UID],a),"stale NPC request cannot replace the owned registration");
            SteedRaceSystem.OnDisconnect(b);
            Check(roster.Count==1,"disconnect removes only its owned registration");register(b);
            foreach(ushort id in new ushort[]{9929,2066,1950,2064,2061,2063,2062})
                MapsTable.MapInformations[id]=new MapsTable.MapInformation{ID=id,BaseID=id,RaceRecord=0};
            start();
            Check((bool)system.GetField("Running",Hidden).GetValue(null) && SteedRace.isOn
                && a.Entity.MapID==SteedRace.MAPID && b.Entity.MapID==SteedRace.MAPID
                && Scalar("SELECT count(*) FROM revora_steed_results WHERE uid=@uid AND rank IS NULL",a.Entity.UID)==1,
                "scheduled tick admits and teleports both ordinary NPC registrants onto the selected track");
            Check(!SteedRace.GateOpen,"scheduled race keeps the start gate closed during its peace period");
            system.GetField("PeaceUntil",Hidden).SetValue(null,new Time32(0));SteedRaceSystem.Tick();
            Check(SteedRace.GateOpen,"scheduled tick opens the durable race gate after the peace period");
            int steps=RaceWalkTo(a,(ushort)SteedRace.Settings[3],(ushort)SteedRace.Settings[4],(int)SteedRace.Settings[5]);
            race.FinishRace(a);
            Check(steps>100 && a.Entity.MapID==1002 && (uint)a[SteedRaceSystem.ClaimKey]==1,
                "registered rider completes actual track movement and production finish return");
            near(a);claim(a);var prize=a.Inventory.Objects.Single(i=>i.ID>=800000 && i.ID<900000);
            Check(prize.Bound && ConquerItemInformation.BaseInformations[prize.ID].PurificationLevel==7,
                "complete scheduled NPC registration-to-finish journey delivers its bound P7 soul");
        } finally {
            if(SteedRace.isOn)race.End();Call(system,null,"ResetCycle");Server.Thread=oldThread;Server.GamePool=oldPool;
        }
    }

    static int RaceWalkTo(GameState p,ushort fx,ushort fy,int radius) {
        int w=p.Map.Floor.Bounds.Width,h=p.Map.Floor.Bounds.Height,start=p.Entity.Y*w+p.Entity.X,end=-1;
        var parent=new int[w*h];for(int n=0;n<parent.Length;n++)parent[n]=-1;
        var queue=new System.Collections.Generic.Queue<int>();queue.Enqueue(start);parent[start]=start;
        while(queue.Count>0){int current=queue.Dequeue(),x=current%w,y=current/w;if(Kernel.GetDistance((ushort)x,(ushort)y,fx,fy)<=radius){end=current;break;}
            for(int dir=0;dir<8;dir++){int nx=x+Entity.XDir[dir],ny=y+Entity.YDir[dir];if(nx<0 || ny<0 || nx>=w || ny>=h)continue;int next=ny*w+nx;
                if(parent[next]<0 && p.Map.Floor[nx,ny,MapObjectType.Player,null]){parent[next]=current;queue.Enqueue(next);}}}
        if(end<0)throw new Exception("Race terrain has no valid route.");var path=new System.Collections.Generic.Stack<int>();for(int n=end;n!=start;n=parent[n])path.Push(n);
        int count=path.Count;while(path.Count>0){int next=path.Pop(),dx=next%w-p.Entity.X,dy=next/w-p.Entity.Y,dir=0;while(dir<8 && (Entity.XDir[dir]!=dx || Entity.YDir[dir]!=dy))dir++;
            if(dir==8 || !p.Entity.Move((Enums.ConquerAngle)dir,1) || p.Entity.X!=next%w || p.Entity.Y!=next/w)throw new Exception("Server rejected race path step.");}
        return count;
    }

}
