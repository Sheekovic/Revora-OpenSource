using System;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
internal static partial class PotencyHuntTests {
    static void VerifyMonsterKo(string runtime,Map town) {
        Query("DROP TABLE entities;CREATE TABLE entities AS TABLE public.entities WITH NO DATA;CREATE TABLE titles (LIKE public.titles INCLUDING ALL)");
        foreach(string table in new[]{"teamarena","championarena","quests","status","reward","rewardclameid","merchant","dialyquests","activeness","house","pkexploits","achievement","itemadding"})
            Query("CREATE TABLE IF NOT EXISTS "+table+" AS TABLE public."+table+" WITH NO DATA");
        Query("CREATE TABLE prizes(uid bigint PRIMARY KEY,name text,prizes bytea)");
        StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
        RunesSystem.LoadRunesEXP();SpellTable.Load();Call(EvidenceType,null,"Initialize");
        foreach(ushort id in new ushort[]{1011,1020,1000,1004})World(runtime,id);
        ConquerItem.ItemUID=new Counter(7900000);
        Server.GamePool=new GameState[0];
        var type=typeof(Entity).Assembly.GetType("Revora.Game.Features.MonsterKoCounter");
        var p=FullRunePlayer(town,15);p.Entity.FullyLoaded=true;p.Entity.CycloneStamp=Time32.Now;p.Entity.CycloneTime=300;p.Entity.AddFlag((ulong)FlagsUpdate.Flags.Cyclone,StatusFlagsBigVector32.PermanentFlag,true);
        Func<Entity> monster=()=>{var info=new MonsterInformation{ID=1,Level=1,Hitpoints=10};
            var m=new Entity(EntityFlag.Monster,false){UID=town.EntityUIDCounter.Next,MapID=town.ID,X=p.Entity.X,Y=p.Entity.Y,MonsterInfo=info};
            info.Owner=m;town.Entities[m.UID]=m;m.Hitpoints=10;return m;};
        Action<Entity> death=m=>{m.Killer=p.Entity;m.Hitpoints=0;Call(type,null,"AcceptedDeath",m,p.Entity);};
        Func<Entity,ushort> snapshot=m=>(ushort)Call(type,null,"Snapshot",m);
        var first=monster();Call(type,null,"AcceptedDeath",first,p.Entity);
        Check(p.Entity.KOCount==0,"living monster cannot advance KO chain");death(first);
        Check(p.Entity.KOCount==1 && snapshot(first)==1,"accepted canonical monster death increments its ordinary killer once");
        Parallel.For(0,64,n=>Call(type,null,"AcceptedDeath",first,p.Entity));
        Check(p.Entity.KOCount==1 && snapshot(first)==1,"concurrent duplicate death notifications share one KO snapshot");
        for(int n=0;n<100;n++)snapshot(first);
        Check(p.Entity.KOCount==1,"multiple screen observers cannot advance KO count");
        first.Hitpoints=10;death(first);Check(p.Entity.KOCount==2 && snapshot(first)==2,"same monster object can count again only after revival");
        var second=monster();death(second);Check(snapshot(first)==2 && snapshot(second)==3,"late death display retains its original count after later kills");
        p.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Cyclone);var inactive=monster();death(inactive);
        Check(p.Entity.KOCount==3 && snapshot(inactive)==0,"kills outside active XP mode do not advance the chain");
        p.Entity.KOCount=0;p.Entity.CycloneStamp=Time32.Now;p.Entity.CycloneTime=300;p.Entity.AddFlag((ulong)FlagsUpdate.Flags.Cyclone,StatusFlagsBigVector32.PermanentFlag,true);
        Call(type,null,"AcceptedDeath",inactive,p.Entity);Check(p.Entity.KOCount==0,"starting a new chain cannot recount an earlier non-XP death");
        foreach(string rejected in new[]{"guard","reviver","unregistered","fake","companion"}) {
            var m=monster();if(rejected=="guard")m.MonsterInfo.Guard=true;if(rejected=="reviver")m.MonsterInfo.Reviver=true;
            if(rejected=="unregistered"){town.Entities.Remove(m.UID);}if(rejected=="fake")p.Fake=true;if(rejected=="companion")m.Companion=true;
            death(m);p.Fake=false;Check(p.Entity.KOCount==0 && snapshot(m)==0,"ineligible death earns no KO: "+rejected);
        }
        p.Entity.KOCount=ushort.MaxValue;var saturated=monster();death(saturated);
        Check(p.Entity.KOCount==ushort.MaxValue && snapshot(saturated)==ushort.MaxValue,"KO count saturates instead of wrapping to zero");
        p.Entity.KOCount=0;var parallel=new Entity[64];for(int n=0;n<parallel.Length;n++){parallel[n]=monster();parallel[n].Killer=p.Entity;parallel[n].Hitpoints=0;}
        Parallel.For(0,64,n=>Call(type,null,"AcceptedDeath",parallel[n],p.Entity));
        Check(p.Entity.KOCount==64,"simultaneous distinct deaths lose no KO increments");
        var actual=monster();actual.IsDropped=true;actual.Die(p.Entity);
        Check(actual.Dead && p.Entity.KOCount==65 && snapshot(actual)==65,"production monster death path records the KO snapshot without any screen callback");
        p=FullRunePlayer(town,15);p.Entity.DailySignDays=new byte[0];EntityTable.SaveEntity(p);
        p.Entity.CycloneStamp=Time32.Now;p.Entity.CycloneTime=300;p.Entity.AddFlag((ulong)FlagsUpdate.Flags.Cyclone,StatusFlagsBigVector32.PermanentFlag,true);
        for(int n=1;n<=300;n++) {
            var m=monster();m.IsDropped=true;uint damage=m.Hitpoints;
            if(n==1) {
                damage=1;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,m,new Attack(true){Attacker=p.Entity.UID,Attacked=m.UID,AttackType=Attack.Melee},ref damage,null);
                Check(!m.Dead && m.Hitpoints==9 && p.Entity.KOCount==0 && snapshot(m)==0,"accepted nonlethal damage cannot advance KO progress");damage=m.Hitpoints;
            }
            Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,m,new Attack(true){Attacker=p.Entity.UID,Attacked=m.UID,AttackType=Attack.Melee},ref damage,null);
            if(n==9 || n==10 || n==99 || n==100 || n==299 || n==300)
                Check(!Fenced(p) && p.Entity.KOCount==n && HasAchievement(p,10520)==(n>=10)
                    && HasAchievement(p,10521)==(n>=100) && HasAchievement(p,10522)==(n>=300),
                    "lethal ReceiveAttack calls grant exact KO boundaries: "+n);
        }
        p=ReloadBall(p);p.Entity.MyAchievement.Send();
        Check(HasAchievement(p,10520) && HasAchievement(p,10521) && HasAchievement(p,10522) && p.Entity.KOCount==0,
            "completed KO achievements survive full login while unfinished XP chain does not");
        p=FullRunePlayer(town,15);p.Entity.DailySignDays=new byte[0];EntityTable.SaveEntity(p);
        p.Entity.CycloneStamp=Time32.Now;p.Entity.CycloneTime=300;p.Entity.AddFlag((ulong)FlagsUpdate.Flags.Cyclone,StatusFlagsBigVector32.PermanentFlag,true);
        for(int n=0;n<9;n++){var m=monster();m.IsDropped=true;m.Die(p.Entity);}
        Query("CREATE FUNCTION suppress_ko() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
        Query("CREATE TRIGGER suppress_ko BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW WHEN (NEW.achievement_id=10520) EXECUTE FUNCTION suppress_ko()");
        try{var m=monster();m.IsDropped=true;m.Die(p.Entity);}
        finally{Query("DROP TRIGGER suppress_ko ON revora_achievement_evidence;DROP FUNCTION suppress_ko()");}
        Check(Fenced(p) && Scalar("SELECT count(*) FROM revora_monster_ko_milestones WHERE uid=@uid AND threshold=10 AND completed_at IS NULL",p.Entity.UID)==1
            && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10520",p.Entity.UID)==0,
            "suppressed achievement write preserves durable earned KO receipt without false credit");
        p=ReloadBall(p);p.Entity.MyAchievement.Send();
        Check(!Fenced(p) && HasAchievement(p,10520) && Scalar("SELECT count(*) FROM revora_monster_ko_milestones WHERE uid=@uid AND completed_at IS NOT NULL",p.Entity.UID)==1,
            "full login recovers the earned threshold after achievement rollback");
        p.Entity.CycloneStamp=Time32.Now;p.Entity.CycloneTime=300;p.Entity.AddFlag((ulong)FlagsUpdate.Flags.Cyclone,StatusFlagsBigVector32.PermanentFlag,true);
        for(int n=0;n<10;n++){var m=monster();m.IsDropped=true;m.Die(p.Entity);}
        Check(!Fenced(p) && Scalar("SELECT count(*) FROM revora_monster_ko_milestones WHERE uid=@uid",p.Entity.UID)==1
            && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10520",p.Entity.UID)==1,
            "earning the same KO threshold in another chain cannot duplicate credit");
        p=FullRunePlayer(town,15);p.Entity.DailySignDays=new byte[0];EntityTable.SaveEntity(p);
        p.Entity.CycloneStamp=Time32.Now;p.Entity.CycloneTime=300;p.Entity.AddFlag((ulong)FlagsUpdate.Flags.Cyclone,StatusFlagsBigVector32.PermanentFlag,true);
        for(int n=0;n<9;n++){var m=monster();m.IsDropped=true;m.Die(p.Entity);}
        Query("CREATE FUNCTION suppress_ko_done() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
        Query("CREATE TRIGGER suppress_ko_done BEFORE UPDATE ON revora_monster_ko_milestones FOR EACH ROW EXECUTE FUNCTION suppress_ko_done()");
        try{var m=monster();m.IsDropped=true;m.Die(p.Entity);}
        finally{Query("DROP TRIGGER suppress_ko_done ON revora_monster_ko_milestones;DROP FUNCTION suppress_ko_done()");}
        Check(Fenced(p) && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10520",p.Entity.UID)==0
            && Scalar("SELECT count(*) FROM revora_monster_ko_milestones WHERE uid=@uid AND completed_at IS NULL",p.Entity.UID)==1,
            "suppressed receipt finalization rolls back achievement insertion and retains pending recovery");
        p=ReloadBall(p);p.Entity.MyAchievement.Send();
        Check(!Fenced(p) && HasAchievement(p,10520),"login retries receipt finalization and achievement atomically");
        p=FullRunePlayer(town,15);
        foreach(string mode in new[]{"Cyclone","Superman","SuperCyclone","DragonCyclone","Oblivion","ManiacDance","Omnipotence"}) {
            var flag=(FlagsUpdate.Flags)Enum.Parse(typeof(FlagsUpdate.Flags),mode);
            var stamp=typeof(Entity).GetField(mode+"Stamp");var duration=typeof(Entity).GetField(mode+"Time");var start=Time32.Now;
            stamp.SetValue(p.Entity,start);duration.SetValue(p.Entity,Convert.ChangeType(20,duration.FieldType));
            p.Entity.AddFlag((ulong)flag,StatusFlagsBigVector32.PermanentFlag,true);
            Check((bool)Call(typeof(Entity),p.Entity,"HasLiveKoMode",start) && !(bool)Call(typeof(Entity),p.Entity,"HasLiveKoMode",start.AddSeconds(20)),
                "KO mode respects exact expiry despite retained display flag: "+mode);
            p.Entity.RemoveFlag((ulong)flag);
        }
        p.Entity.KOSpell=1110;p.Entity.CycloneTime=20;p.Entity.CycloneStamp=Time32.Now.AddSeconds(-40);
        p.Entity.AddFlag((ulong)FlagsUpdate.Flags.Cyclone,StatusFlagsBigVector32.PermanentFlag,true);p.Entity.KOCount=9;
        var expired=monster();expired.IsDropped=true;expired.Die(p.Entity);
        Check(snapshot(expired)==0 && p.Entity.KOCount==9 && !HasAchievement(p,10520)
            && !(bool)Call(typeof(Entity),p.Entity,"HasLiveKoMode",Time32.Now),
            "expired XP flag cannot complete tenth KO or revive its expired timer through a kill");
        Check((ushort)Call(typeof(Entity),p.Entity,"ResetExpiredKo")==9 && p.Entity.KOCount==0,
            "timer drains expired chain once even before stale flag removal");
        p.Entity.CycloneStamp=Time32.Now;p.Entity.CycloneTime=20;p.Entity.KOCount=3;
        Check((ushort)Call(typeof(Entity),p.Entity,"ResetExpiredKo")==0 && p.Entity.KOCount==3,
            "timer rechecks live mode while holding counter lock and preserves its chain");




    }
}
