using System;
using System.IO;
using System.Linq;
using Npgsql;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static class RebirthArtsRunner {
    static int Main(){return PotencyHuntTests.DailyUnarmedMain(rebirthArtsOnly:true);}
}
internal static class NobilityAccessRunner {
    static int Main(){return PotencyHuntTests.DailyUnarmedMain(nobilityOnly:true);}
}
internal static class DailyUnarmedRunner {
    static int Main(){return PotencyHuntTests.DailyUnarmedMain();}
}
internal static class PowerUnarmedRunner {
    static int Main(){return PotencyHuntTests.DailyUnarmedMain(true);}
}
internal static class SnakeUnarmedRunner {
    static int Main(){return PotencyHuntTests.DailyUnarmedMain(false,true);}
}
internal static class LabUnarmedRunner {
    static int Main(){return PotencyHuntTests.DailyUnarmedMain(false,false,true);}
}
internal static class DemonUnarmedRunner {
    static int Main(){return PotencyHuntTests.DailyUnarmedMain(false,false,false,true);}
}
internal static class SummerUnarmedRunner {
    static int Main(){return PotencyHuntTests.DailyUnarmedMain(false,false,false,false,true);}
}
internal static class BeastUnarmedRunner {
    static int Main(){return PotencyHuntTests.DailyUnarmedMain(false,false,false,false,false,true);}
}
internal static class BookUnarmedRunner {
    static int Main(){return PotencyHuntTests.DailyUnarmedMain(false,false,false,false,false,false,true);}
}
internal static class ExtractionUnarmedRunner {
    static int Main(){return PotencyHuntTests.DailyUnarmedMain(false,false,false,false,false,false,false,true);}
}
internal static partial class PotencyHuntTests {
    static void LoadBeastSuiteDonor(string runtime) {
        SobNpcSpawn donor;
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new NpgsqlCommand("SELECT mapid,cellx,celly,lookface,type,name,life,maxlife FROM public.sobnpcs WHERE id=900042",c))
        using(var r=q.ExecuteReader()) {
            if(!r.Read())throw new InvalidOperationException("Installed nobility donor missing from guild fixture catalog.");
            donor=new SobNpcSpawn{UID=900042,MapID=Convert.ToUInt16(r[0]),X=Convert.ToUInt16(r[1]),Y=Convert.ToUInt16(r[2]),Mesh=Convert.ToUInt16(r[3]),
                Type=(Enums.NpcType)Convert.ToByte(r[4]),Name=r.GetString(5),Hitpoints=Convert.ToUInt32(r[6]),MaxHitpoints=Convert.ToUInt32(r[7])};
        }}
        World(runtime,donor.MapID).AddNpc(donor);
    }
    internal static int DailyUnarmedMain(bool powerOnly=false,bool snakeOnly=false,bool labOnly=false,bool demonOnly=false,bool summerOnly=false,bool beastOnly=false,bool bookOnly=false,bool extractionOnly=false,bool nobilityOnly=false,bool rebirthArtsOnly=false) {
        var b=new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("REVORA_QUARANTINE_TEST_CONNECTION"));
        Environment.SetEnvironmentVariable("REVORA_QUARANTINE_TEST_CONNECTION",null);
        if(b.Host!="127.0.0.1" || b.Port!=25439 || b.Database!="revora_candidate_game")throw new InvalidOperationException("private candidate required");
        string runtime=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(Entity).Assembly.Location),"../../../bin/Debug"));
        string previous=Environment.CurrentDirectory;
        DataHolder.ConnectionString=b.ConnectionString;Environment.CurrentDirectory=runtime;
        ConquerItemInformation.Load();MonsterInformation.Load();InnerPowerTable.LoadDBInformation();
        Query("CREATE SCHEMA daily_unarmed_fixture");
        try {
            b.SearchPath="daily_unarmed_fixture";DataHolder.ConnectionString=b.ConnectionString;
            Query("CREATE TABLE entities(uid bigint PRIMARY KEY,name text NOT NULL,unionexploits bigint NOT NULL DEFAULT 0);CREATE TABLE items(LIKE public.items INCLUDING ALL);CREATE TABLE skills AS TABLE public.skills WITH NO DATA;CREATE TABLE arena AS TABLE public.arena WITH NO DATA;CREATE TABLE monsterspawns(LIKE public.monsterspawns INCLUDING ALL)");
            Call(Packs,null,"Initialize");
            Call(FirstRite,null,"Initialize");
            Call(SecondRite,null,"Initialize");
            Call(PaidRite,null,"Initialize");
            Call(AssassinTrainer,null,"Initialize");
            Call(NinjaTrainer,null,"Initialize");
            Call(TokenShop,null,"Initialize");
            Call(ExpItems,null,"Initialize");
            Call(ExpPotions,null,"Initialize");
            Call(QuestGifts,null,"Initialize");
            Call(PrizePacks,null,"Initialize");
            Call(Magnolia,null,"Initialize");
            Call(PriceQuest,null,"Initialize");
            Call(RareQuest,null,"Initialize");
            Call(SoulBall,null,"Initialize");
            Call(Anniversary,null,"Initialize");
            Call(ExorcismGifts,null,"Initialize");
            Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.ExorcismQuest"),null,"Initialize");
            Call(EvidenceType,null,"Initialize");
            Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.MentorDepartures"),null,"Initialize");
            Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.MentorStoneClaims"),null,"Initialize");
            Call(typeof(Server).Assembly.GetType("Revora.Game.Features.MentorBlessingClaims"),null,"Initialize");
            typeof(CompanionTests).GetMethod("Reset",Hidden).Invoke(null,null);
            Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.Tournaments.LocalTeamPkLedger"),null,"Initialize");
            World(runtime,1217);var town=World(runtime,1002);
            if(bookOnly || extractionOnly || nobilityOnly || rebirthArtsOnly){
                Query("DROP TABLE entities;CREATE TABLE entities AS TABLE public.entities WITH NO DATA;CREATE TABLE titles (LIKE public.titles INCLUDING ALL);CREATE TABLE profs AS TABLE public.profs WITH NO DATA");
                foreach(string table in new[]{"teamarena","championarena","quests","status","reward","rewardclameid","merchant","dialyquests","activeness","house","pkexploits","achievement","itemadding"})Query("CREATE TABLE IF NOT EXISTS "+table+" AS TABLE public."+table+" WITH NO DATA");
                Query("CREATE TABLE prizes(uid bigint PRIMARY KEY,name text,prizes bytea)");
                DataHolder.ReadStats();SpellTable.Load();RunesSystem.LoadRunesEXP();StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
                ConquerItem.ItemUID=new Counter(8300000);Map.CreateTimerFactories();Revora.Thread.GenericThreadPool=new System.Threading.StaticPool(1);
                if(rebirthArtsOnly){VerifyFiveRebirthArts(runtime,town);System.Console.WriteLine("Rebirth art checks passed: "+checks);return 0;}
                if(nobilityOnly){VerifyPublicBoundDonor(town);VerifyAtomicNobility(town,Check);VerifyEarnedBoundDonation(runtime,town);System.Console.WriteLine("Nobility access checks passed: "+checks);return 0;}
                if(extractionOnly){VerifyExtractionRoute(runtime);VerifyExtractionPurse(town);VerifyExtractionSoul(town);System.Console.WriteLine("Extraction unarmed checks passed: "+checks);return 0;}
                VerifyPowerCombat(town);VerifyPowerBookHunts(town);VerifyPowerBookStore(town);
                System.Console.WriteLine("Power Book unarmed checks passed: "+checks);return 0;
            }
            if(beastOnly){LoadBeastSuiteDonor(runtime);Query("ALTER TABLE entities ADD COLUMN previousmapid integer DEFAULT 0,ADD COLUMN previousx integer DEFAULT 0,ADD COLUMN previousy integer DEFAULT 0");VerifyGuildSeason(runtime);System.Console.WriteLine("Guild season and beast unarmed checks passed: "+checks);return 0;}
            if(summerOnly){VerifySummer(runtime,town);System.Console.WriteLine("Summer shared-kill checks passed: "+checks);return 0;}
            if(demonOnly){VerifyDemon(runtime);System.Console.WriteLine("Demon unarmed checks passed: "+checks);return 0;}
            if(labOnly){VerifyLabyrinth(runtime,town);System.Console.WriteLine("Labyrinth unarmed checks passed: "+checks);return 0;}
            if(snakeOnly){VerifySnake(runtime,town);System.Console.WriteLine("Snake Islands unarmed checks passed: "+checks);return 0;}
            if(powerOnly){VerifyPower(runtime,town);System.Console.WriteLine("Power Arena unarmed checks passed: "+checks);return 0;}
            VerifyMonsterKo(runtime,town);
            VerifyWildUnarmed(town);
            Query("ALTER TABLE revora_unarmed_wild_victories DROP COLUMN unarmed");
            Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.WildMonsterVictory"),null,"Initialize");
            Check(Scalar("SELECT count(*) FROM revora_unarmed_wild_victories WHERE unarmed",0)>0 && Scalar("SELECT count(*) FROM revora_unarmed_wild_victories WHERE NOT unarmed",0)==0,"prior wilderness receipts migrate as unarmed awards");
            VerifyDailyUnarmed(runtime);
            // Reset only these isolated fixture rounds before the existing suite.
            Query("TRUNCATE revora_daily_boss_loot,revora_daily_boss_runs");
            // Match the legacy suite's zero-default purse fixture; production
            // catalog defaults differ from its minimally constructed sessions.
            Query("ALTER TABLE entities ALTER COLUMN conquerpoints SET DEFAULT 0,ALTER COLUMN revorapoint SET DEFAULT 0,ALTER COLUMN money SET DEFAULT 0");
            Kernel.GamePool.Clear();Server.GamePool=new GameState[0];
            Daily.GetField("Initialized",Hidden).SetValue(null,false);
            VerifyDailyBosses(runtime);
            System.Console.WriteLine("Daily unarmed and KO checks passed: "+checks);return 0;
        } finally {
            Environment.CurrentDirectory=previous;NpgsqlConnection.ClearAllPools();b.SearchPath="public";DataHolder.ConnectionString=b.ConnectionString;
            Query("DROP SCHEMA daily_unarmed_fixture CASCADE");DataHolder.ConnectionString=null;
        }
    }
    static void VerifyDailyUnarmed(string runtime) {
        foreach(ushort id in new ushort[]{1011,1020,1000})if(!Kernel.Maps.ContainsKey(id))World(runtime,id);
        dailyNow=new DateTime(2039,1,12,0,0,0,DateTimeKind.Local).ToUniversalTime();
        Daily.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>dailyNow));
        Call(Daily,null,"Create");var schedule=DailySchedule();Check(schedule.Length==4,"four unarmed boss test windows");
        for(int slot=0;slot<4;slot++) {
            DailyTick(schedule[slot]);var boss=DailyActive();Check(boss!=null && boss.Level==140 && boss.MonsterInfo.Level==140,"boss display and qualifying combat level agree");
            var p=FullRunePlayer(Kernel.Maps[boss.MapID],15);p.Entity.X=boss.X;p.Entity.Y=boss.Y;p.Entity.DailySignDays=new byte[0];
            Check(p.Equipment.TryGetItem(4)==null && p.Equipment.TryGetItem(5)==null,"ordinary boss killer starts empty-handed");
            EntityTable.SaveEntity(p);
            if(slot==1 || slot==2) {
                var weapon=Pack(p,410009,1);Query("UPDATE items SET position=4 WHERE uid=@uid","uid",(long)weapon.UID);
                if(slot==2)p.AlternateEquipment=true;
            }
            Action hit=()=>{uint damage=boss.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,boss,new Attack(true){Attacker=p.Entity.UID,Attacked=boss.UID,AttackType=Attack.Melee},ref damage,null);};
            uint cp=p.Entity.ConquerPoints,rp=p.Entity.RevoraPoint;ulong gold=p.Entity.Money;
            if(slot==0) {
                var held=new System.Threading.ManualResetEventSlim(false);var release=new System.Threading.ManualResetEventSlim(false);
                var holder=System.Threading.Tasks.Task.Run(()=>{lock(p.ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
                try {
                    var attempt=System.Threading.Tasks.Task.Run(()=>Call(Daily,null,"Die",boss,p.Entity));
                    Check(attempt.Wait(2000) && DailyActive()==boss && !boss.Dead && !HasAchievement(p,10525),"busy item lock defers victory without deadlocking boss gate");
                }finally{release.Set();holder.Wait();held.Dispose();release.Dispose();}
                Query("CREATE FUNCTION reject_daily_unarmed() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER reject_daily_unarmed BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW WHEN(NEW.achievement_id=10525) EXECUTE FUNCTION reject_daily_unarmed()");
                try{hit();}finally{Query("DROP TRIGGER reject_daily_unarmed ON revora_achievement_evidence;DROP FUNCTION reject_daily_unarmed()");}
                Check(!boss.Dead && DailyActive()==boss && Scalar("SELECT count(*) FROM revora_daily_boss_loot",0)==0 && !HasAchievement(p,10525)
                    && p.Entity.ConquerPoints==cp && p.Entity.RevoraPoint==rp && p.Entity.Money==gold,"suppressed unarmed evidence rolls back boss victory, four souls and bounty");
            }
            if(slot==3) {
                dailyNow=schedule[slot].AddHours(2);hit();
                Check(DailyActive()==null && !HasAchievement(p,10525) && p.Entity.ConquerPoints==cp,"expired boss cannot earn unarmed credit or bounty");continue;
            }
            hit();Check(DailyActive()==null && p.Entity.ConquerPoints==cp+1000000 && p.Entity.RevoraPoint==rp+50000 && p.Entity.Money==gold+10000000,"successful lethal attack retains exact boss bounty");
            bool expected=slot!=1;Check(HasAchievement(p,10525)==expected,"saved active weapon excludes credit; inactive bank does not");
            if(expected)Check(Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10525 AND source='unarmed-monster-victory' AND source_key LIKE 'daily-boss:2039-01-12:%'",p.Entity.UID)==1,"unarmed evidence identifies the durable daily run");
            long receipts=Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10525",p.Entity.UID);
            Call(Daily,null,"Die",boss,p.Entity);Call(Daily,null,"TryPay",p,schedule[slot].ToLocalTime().ToString("yyyy-MM-dd")+"/"+slot);
            Check(Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10525",p.Entity.UID)==receipts && p.Entity.ConquerPoints==cp+1000000,"duplicate death and payout preserve evidence and purse");
            if(slot==0){p=ReloadBall(p);p.Entity.MyAchievement.Send();Check(HasAchievement(p,10525) && p.Entity.ConquerPoints==cp+1000000,"production character reload restores unarmed credit and bounty");}
        }
    }
    static void VerifyWildUnarmed(Map map) {
        Check(Enumerable.Range(3000,25).All(id=>MonsterInformation.MonsterInformations[(uint)id].Level<120),"all loaded Sky Pass profiles remain below unarmed level threshold");
        Check(Enumerable.Range(6000,7).All(id=>MonsterInformation.MonsterInformations[(uint)id].Level<120),"all loaded Moon Box profiles remain below unarmed level threshold");
        var type=typeof(Entity).Assembly.GetType("Revora.Game.Features.WildMonsterVictory");
        Func<GameState> player=()=>{var p=FullRunePlayer(map,15);p.Entity.DailySignDays=new byte[0];EntityTable.SaveEntity(p);return p;};
        Func<GameState,byte,Entity> monster=(p,level)=>{
            var info=new MonsterInformation{ID=987654,Level=level,Hitpoints=10};
            var m=new Entity(EntityFlag.Monster,false){UID=map.EntityUIDCounter.Next,MapID=map.ID,X=p.Entity.X,Y=p.Entity.Y,MonsterInfo=info,IsDropped=true};
            info.Owner=m;map.Entities[m.UID]=m;m.MaxHitpoints=m.Hitpoints=10;return m;};
        Action<GameState,Entity,uint> hit=(p,m,amount)=>{uint damage=amount;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,m,new Attack(true){Attacker=p.Entity.UID,Attacked=m.UID,AttackType=Attack.Melee},ref damage,null);};
        Func<GameState,long> rows=p=>Scalar("SELECT count(*) FROM revora_unarmed_wild_victories WHERE uid=@uid",p.Entity.UID);
        var first=player();var body=monster(first,120);int kills=first.Entity.KillCount;
        hit(first,body,1);Check(body.Hitpoints==9 && rows(first)==0,"nonlethal wilderness hit earns no unarmed victory");
        Query("CREATE FUNCTION reject_wild_unarmed() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER reject_wild_unarmed BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW WHEN(NEW.achievement_id=10525) EXECUTE FUNCTION reject_wild_unarmed()");
        try{hit(first,body,9);}finally{Query("DROP TRIGGER reject_wild_unarmed ON revora_achievement_evidence;DROP FUNCTION reject_wild_unarmed()");}
        Check(!body.Dead && body.Hitpoints==9 && rows(first)==0 && !HasAchievement(first,10525) && first.Entity.KillCount==kills,"failed wilderness evidence rolls back life receipt and preserves living monster and kill counters");
        hit(first,body,9);Check(body.Dead && rows(first)==1 && HasAchievement(first,10525),"retried lethal wilderness hit saves and immediately projects unarmed credit");
        int awardedKills=first.Entity.KillCount;body.Die(first.Entity);Check(rows(first)==1 && first.Entity.KillCount==awardedKills,"duplicate ordinary death cannot repeat credit or kill counters");
        var next=monster(first,120);hit(first,next,10);Check(next.Dead && rows(first)==1,"already earned achievement does not create per-kill ledger rows");
        first=ReloadBall(first);first.Entity.MyAchievement.Send();Check(HasAchievement(first,10525) && rows(first)==1,"wilderness credit survives production character reload");
        var second=player();body.Hitpoints=10;hit(second,body,10);Check(body.Dead && rows(second)==1,"respawned monster object has a new life and can credit another player");
        var uncertain=player();var victim=monster(uncertain,120);
        type.GetField("AfterCommit",Hidden).SetValue(null,(Action)(()=>{throw new IOException("fixture lost commit acknowledgement");}));
        try{hit(uncertain,victim,10);}finally{type.GetField("AfterCommit",Hidden).SetValue(null,null);}
        Check(!victim.Dead && rows(uncertain)==1,"lost commit acknowledgement retains one durable victory for retry");
        var rival=player();hit(rival,victim,10);Check(!victim.Dead && rows(rival)==0,"another killer cannot steal an ambiguously committed monster life");
        rival.Entity.Companion=true;try{victim.Die(rival.Entity);}finally{rival.Entity.Companion=false;}
        Check(!victim.Dead && rows(rival)==0,"ineligible caller cannot bypass an unresolved committed monster life");
        var laterWeapon=Pack(uncertain,410009,1);Query("UPDATE items SET position=4 WHERE uid=@uid","uid",(long)laterWeapon.UID);
        hit(uncertain,victim,10);Check(victim.Dead && rows(uncertain)==1 && HasAchievement(uncertain,10525),"original killer resolves the saved empty-hand decision even after an equipment change");
        var raceA=player();var raceB=player();var shared=monster(raceA,120);
        var race=new[]{System.Threading.Tasks.Task.Run(()=>shared.Die(raceA.Entity)),System.Threading.Tasks.Task.Run(()=>shared.Die(raceB.Entity))};
        Check(System.Threading.Tasks.Task.WaitAll(race,3000) && shared.Dead && rows(raceA)+rows(raceB)==1,"concurrent ordinary killers share exactly one accepted monster life");
        foreach(string mode in new[]{"saved-weapon","inactive-bank","below-level","companion","guard","unregistered"}) {
            var p=player();var m=monster(p,mode=="below-level"?(byte)119:(byte)120);
            if(mode=="saved-weapon" || mode=="inactive-bank") {var w=Pack(p,410009,1);Query("UPDATE items SET position=4 WHERE uid=@uid","uid",(long)w.UID);p.AlternateEquipment=mode=="inactive-bank";}
            if(mode=="companion")m.Companion=true;if(mode=="guard")m.MonsterInfo.Guard=true;if(mode=="unregistered")map.Entities.Remove(m.UID);
            // Excluded non-world fixtures need not run unrelated pet/guard teardown.
            if(mode=="companion" || mode=="guard" || mode=="unregistered") {
                Check(!(bool)Call(type,null,"Ordinary",m,p.Entity) && rows(p)==0,"unarmed admission excludes "+mode);continue;
            }
            hit(p,m,10);Check(m.Dead && rows(p)==(mode=="inactive-bank"?1:0),"wilderness equipment/level eligibility: "+mode);
        }
        var busy=player();var blocked=monster(busy,120);var held=new System.Threading.ManualResetEventSlim(false);var release=new System.Threading.ManualResetEventSlim(false);
        var holder=System.Threading.Tasks.Task.Run(()=>{lock(busy.ItemSyncRoot){held.Set();release.Wait();}});held.Wait();
        try {var attempt=System.Threading.Tasks.Task.Run(()=>blocked.Die(busy.Entity));Check(attempt.Wait(2000) && !blocked.Dead && rows(busy)==0,"busy inventory defers wilderness death without lock inversion");}
        finally{release.Set();holder.Wait();held.Dispose();release.Dispose();}
        hit(busy,blocked,10);Check(blocked.Dead && rows(busy)==1,"deferred wilderness hit can earn on retry");
    }

}
