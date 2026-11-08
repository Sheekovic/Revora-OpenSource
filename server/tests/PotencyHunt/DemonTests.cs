using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.ConquerStructures;
using Revora.Network.GamePackets;
using Console=System.Console;
using Team=Revora.Game.ConquerStructures.Team;

internal static partial class PotencyHuntTests
{
    static readonly Type Demon=typeof(Entity).Assembly.GetType("Revora.Game.Features.DemonExterminators");
    static DateTime demonNow;
    static Map demonMarket;
    static void DemonNpc(GameState p,byte option) {
        p.ActiveNpc=2000;Npcs.GetDialog(new NpcRequest(true){NpcID=2000,OptionID=option},p);
    }
    static void DemonMove(GameState p,Map map) {
        p.BindWorldMap(map.ID);p.Entity.X=(ushort)(map.ID==1015?240:232);p.Entity.Y=(ushort)(map.ID==1015?208:199);
        if(map==demonMarket) {p.Entity.X=map.Npcs[2000].X;p.Entity.Y=map.Npcs[2000].Y;}
        else if(!map.Floor[p.Entity.X,p.Entity.Y,MapObjectType.Player])throw new Exception("Demon hunt fixture anchor is blocked.");
    }
    static GameState DemonPlayer() {
        var p=ProgressPlayer(demonMarket,"");p.Entity.Level=140;
        p.Entity.Titles=new ConcurrentDictionary<TitlePacket.Titles,DateTime>();DemonMove(p,demonMarket);return p;
    }
    static Entity DemonMob(Map map,uint type) {
        var mob=(Entity)typeof(CompanionTests).GetMethod("Monster",Hidden).Invoke(null,new object[]{map,(ushort)(map.ID==1015?240:232),(ushort)(map.ID==1015?208:199),1u});
        mob.MonsterInfo.ID=type;mob.MonsterInfo.Level=MonsterInformation.MonsterInformations[type].Level;mob.MonsterInfo.Name=mob.Name=MonsterInformation.MonsterInformations[type].Name;
        mob.IsDropped=true;return mob; // No loot/EXP fixture dependencies; still run the actual server death path.
    }
    static long DemonKills(GameState p) { return Scalar("SELECT kills FROM revora_demon_runs WHERE uid=@uid ORDER BY day DESC LIMIT 1",p.Entity.UID); }
    static void DemonCredit(Entity mob,Entity killer) {Call(Demon,null,"Credit",killer,mob.MapID,mob.X,mob.Y,mob.MonsterInfo.ID);}
    static void DemonReset(GameState p) { typeof(Entity).GetField("PersistenceRequiresReload",Instance).SetValue(p.Entity,false);p.Action=2; }
    static void VerifyDemon(string runtime) {
        Query("ALTER TABLE entities ADD COLUMN previousmapid integer DEFAULT 0,ADD COLUMN previousx integer DEFAULT 0,ADD COLUMN previousy integer DEFAULT 0,ADD COLUMN my_title integer DEFAULT 0, ADD COLUMN level integer DEFAULT 140, ADD COLUMN mapid integer DEFAULT 1036, ADD COLUMN x integer DEFAULT 206, ADD COLUMN y integer DEFAULT 213");
        Query("CREATE TABLE titles (LIKE public.titles INCLUDING ALL)");StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
        Query("CREATE TABLE npcs (LIKE public.npcs INCLUDING ALL)");Query("INSERT INTO npcs SELECT * FROM public.npcs WHERE id=2000");
        Query("CREATE TABLE itemadding (LIKE public.itemadding INCLUDING ALL)");
        ConquerItem.ItemUID=new Counter(3990000);demonMarket=World(runtime,1036);var birds=World(runtime,1015);var castle=World(runtime,1001);
        demonNow=new DateTime(2026,1,4,20,0,0,DateTimeKind.Utc);Demon.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>demonNow));Call(Demon,null,"Initialize");Call(Demon,null,"PlaceNpc");
        Check(demonMarket.Npcs.ContainsKey(2000) && demonMarket.Npcs[2000].Mesh==1640,"installed Cloud Saint restored on a walkable Market tile");
        Check((string)Call(Demon,null,"Day",new DateTime(2026,7,3,6,59,59,DateTimeKind.Utc))=="20260702"
            && (string)Call(Demon,null,"Day",new DateTime(2026,7,3,7,0,0,DateTimeKind.Utc))=="20260703","summer daily boundary uses Pacific midnight");
        Check((string)Call(Demon,null,"Day",new DateTime(2026,1,3,7,59,59,DateTimeKind.Utc))=="20260102"
            && (string)Call(Demon,null,"Day",new DateTime(2026,1,3,8,0,0,DateTimeKind.Utc))=="20260103","winter daily boundary uses Pacific midnight");
        var p=DemonPlayer();p.Entity.Level=119;DemonNpc(p,1);
        Check(Scalar("SELECT count(*) FROM revora_demon_runs WHERE uid=@uid",p.Entity.UID)==0,"level 119 cannot accept the daily hunt");
        p.Entity.Level=140;p.Fake=true;DemonNpc(p,1);p.Fake=false;
        p.Entity.X=1;p.Entity.Y=1;DemonNpc(p,1);
        Check(p.Inventory.Count==0,"fake actors and distant NPC requests receive no jar");DemonMove(p,demonMarket);
        Parallel.For(0,12,i=>DemonNpc(p,1));
        Check(p.Inventory.Count==1 && p.Inventory.Objects[0].ID==750000 && p.Inventory.Objects[0].Bound
            && Scalar("SELECT count(*) FROM revora_demon_runs WHERE uid=@uid",p.Entity.UID)==1,"ordinary concurrent signup commits one bound jar and one daily run");
        uint savedRune=(uint)Scalar("SELECT rune_reward_id FROM revora_demon_runs WHERE uid=@uid",p.Entity.UID);
        Check(savedRune/10000==403 && savedRune%100==1 && ConquerItemInformation.BaseInformations[savedRune].Class==0,"new contract freezes a class-neutral level-one yellow rune");
        p.Entity.Status4=uint.MaxValue;DemonNpc(p,2);
        Check(Scalar("SELECT stage FROM revora_demon_runs WHERE uid=@uid",p.Entity.UID)==0 && !HasAchievement(p,11001),"legacy counter and early turn-in cannot skip the first hunt");
        DemonMove(p,birds);var mob=DemonMob(birds,18);mob.Die(p.Entity);
        Check(DemonKills(p)==1 && (uint)Call(Demon,null,"JarCount",p)==1,"actual monster death persists the first soul and authoritative jar count");
        Parallel.For(0,12,i=>mob.Die(p.Entity));Check(DemonKills(p)==1,"duplicate and concurrent death requests cannot count the same life twice");
        var wrong=DemonMob(birds,20);DemonCredit(wrong,p.Entity);
        Check(DemonKills(p)==1,"a different monster family cannot fill the active jar");
        Call(TitleRules,null,"Login",p);
        CloudSaintsJar.Calculate(p,new Attack(true){AttackType=Attack.CloudSaints,Attacker=123,ResponseDamage=999999});
        Check(DemonKills(p)==1,"forged jar request identity and count cannot change saved progress");
        var q=DemonPlayer();DemonNpc(q,1);DemonMove(q,birds);
        var team=(Team)FormatterServices.GetUninitializedObject(typeof(Team));team.Lider=p;team.Members=new ConcurrentDictionary<uint,Team.Member>();
        team.Members[p.Entity.UID]=new Team.Member{entry=p};team.Members[q.Entity.UID]=new Team.Member{entry=q};p.Team=q.Team=team;
        var shared=DemonMob(birds,18);shared.Die(p.Entity);
        Check(DemonKills(p)==2 && DemonKills(q)==1,"nearby ordinary teammates share a legitimate matching kill");
        q.BindWorldMap(castle.ID);DemonMob(birds,18).Die(p.Entity);
        Check(DemonKills(q)==1,"matching coordinates on another map do not share credit");q.BindWorldMap(birds.ID);
        q.Entity.X=1;q.Entity.Y=1;DemonMob(birds,18).Die(p.Entity);
        Check(DemonKills(q)==1,"out-of-range teammate does not receive souls");DemonMove(q,birds);
        q.Fake=true;DemonMob(birds,18).Die(p.Entity);q.Fake=false;
        Check(DemonKills(q)==1,"fake teammates cannot collect quest progress");
        var left=DemonMob(birds,18);var right=DemonMob(birds,18);long pBefore=DemonKills(p),qBefore=DemonKills(q);
        using(var barrier=new System.Threading.Barrier(2)) {
            var task=Task.Run(()=>Parallel.Invoke(
                ()=>{lock(p.ItemSyncRoot){barrier.SignalAndWait();left.Die(p.Entity);}},
                ()=>{lock(q.ItemSyncRoot){barrier.SignalAndWait();right.Die(q.Entity);}}));
            Check(task.Wait(TimeSpan.FromSeconds(10)) && DemonKills(p)==pBefore+2 && DemonKills(q)==qBefore+2,
                "simultaneous item-triggered team kills count both lives without cross-player lock deadlock");
        }
        p.Team=q.Team=null;
        long before=DemonKills(p);var guard=DemonMob(birds,18);guard.Owner=p;
        DemonCredit(shared,guard);p.Fake=true;DemonCredit(shared,p.Entity);p.Fake=false;
        Check(DemonKills(p)==before,"owned guard and fake killer cannot supply souls");
        var jar=p.Inventory.Objects.Single(i=>i.ID==750000);
        Query("UPDATE items SET warehouse=8 WHERE uid=@uid","uid",(long)jar.UID);DemonMob(birds,18).Die(p.Entity);
        Check(DemonKills(p)==before,"jar outside the persisted inventory cannot count a kill");Query("UPDATE items SET warehouse=0 WHERE uid=@uid","uid",(long)jar.UID);
        Query("ALTER TABLE revora_demon_runs ADD CONSTRAINT demon_counter_failure CHECK(kills=0) NOT VALID");DemonMob(birds,18).Die(p.Entity);
        Check(DemonKills(p)==before && Fenced(p),"failed progress write leaves the saved count unchanged and fences stale state");
        Query("ALTER TABLE revora_demon_runs DROP CONSTRAINT demon_counter_failure");DemonReset(p);Call(Demon,null,"Initialize");
        Check((uint)Call(Demon,null,"JarCount",p)==before,"restart reloads active progress from the saved daily run");
        var active=(ConcurrentDictionary<uint,string>)Demon.GetField("active",Hidden).GetValue(null);string removed;active.TryRemove(p.Entity.UID,out removed);
        p.Inventory.Remove(jar.UID,Enums.ItemUse.None,false);ConquerItemTable.LoadItems(p);Call(Demon,null,"OnLogin",p);
        Check(active.ContainsKey(p.Entity.UID) && p.Inventory.Objects.Any(i=>i.UID==jar.UID && i.Bound)
            && (uint)Call(Demon,null,"JarCount",p)==before,"ordinary item loader and login recover committed jar and active membership after a lost response");
        uint[] ids={18,20,57,58};int[] needs={600,1200,1500,1800};
        for(int stage=0;stage<4;stage++) {
            var map=stage==0?birds:castle;DemonMove(p,map);
            var victim=DemonMob(map,ids[stage]);
            if(stage==3) {
                Check(victim.MonsterInfo.Level==135 && !HasAchievement(p,10525),"loaded Red Devil qualifies at 135 while earlier hunt targets grant no unarmed credit");
                Query("UPDATE revora_demon_runs SET stage=3,kills=0 WHERE uid=@uid","uid",(long)q.Entity.UID);DemonMove(q,castle);p.Team=q.Team=team;
                var summer=typeof(Entity).Assembly.GetType("Revora.Game.Features.SummerHunt");
                summer.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>demonNow));Call(summer,null,"Initialize");Call(summer,null,"Accept",p);
                Check(Scalar("SELECT route FROM revora_summer_daily WHERE uid=@uid",p.Entity.UID)==3,"deterministic summer day selects the same Red Devil target");
                Query("CREATE TABLE reject_demon_recipient(uid bigint PRIMARY KEY)");Query("INSERT INTO reject_demon_recipient(uid) VALUES(@uid)","uid",(long)q.Entity.UID);
                Query("CREATE FUNCTION reject_demon_team() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF EXISTS(SELECT 1 FROM reject_demon_recipient WHERE uid=NEW.uid) THEN RAISE EXCEPTION 'fixture recipient failure'; END IF; RETURN NEW; END $$;CREATE TRIGGER reject_demon_team BEFORE UPDATE ON revora_demon_runs FOR EACH ROW EXECUTE FUNCTION reject_demon_team()");victim.Die(p.Entity);
                Check(!victim.Dead && !Fenced(p) && DemonKills(p)==0 && DemonKills(q)==0 && !HasAchievement(p,10525),"teammate counter failure rolls back the whole high-level kill without consuming its life");
                Query("DROP TRIGGER reject_demon_team ON revora_demon_runs;DROP FUNCTION reject_demon_team();DROP TABLE reject_demon_recipient");
                Query("ALTER TABLE revora_summer_daily ADD CONSTRAINT summer_shared_failure CHECK(kills=0) NOT VALID");victim.Die(p.Entity);
                Check(!victim.Dead && DemonKills(p)==0 && DemonKills(q)==0 && !HasAchievement(p,10525),"Summer Hunt failure rolls back both team counters and unarmed credit");
                Query("ALTER TABLE revora_summer_daily DROP CONSTRAINT summer_shared_failure");
                Query("CREATE FUNCTION reject_demon_unarmed() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER reject_demon_unarmed BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW WHEN(NEW.achievement_id=10525) EXECUTE FUNCTION reject_demon_unarmed()");victim.Die(p.Entity);
                Check(!victim.Dead && DemonKills(p)==0 && DemonKills(q)==0 && Scalar("SELECT kills FROM revora_summer_daily WHERE uid=@uid",p.Entity.UID)==0,"missing unarmed evidence rolls back all shared hunt counters");
                Query("DROP TRIGGER reject_demon_unarmed ON revora_achievement_evidence;DROP FUNCTION reject_demon_unarmed()");
                var victory=typeof(Entity).Assembly.GetType("Revora.Game.Features.WildMonsterVictory");
                victory.GetField("AfterCommit",Hidden).SetValue(null,(Action)(()=>{throw new System.IO.IOException("fixture acknowledgement lost");}));
                try{victim.Die(p.Entity);}finally{victory.GetField("AfterCommit",Hidden).SetValue(null,null);}
                Check(!victim.Dead && DemonKills(p)==1 && DemonKills(q)==1 && Scalar("SELECT kills FROM revora_summer_daily WHERE uid=@uid",p.Entity.UID)==1,"uncertain commit preserves one saved kill for every eligible hunt");
                p.Team=q.Team=null;p.Entity.Status4=q.Entity.Status4=0;
                Parallel.For(0,12,i=>victim.Die(p.Entity));
                Check(victim.Dead && DemonKills(p)==1 && DemonKills(q)==1 && p.Entity.Status4==1 && q.Entity.Status4==1 && HasAchievement(p,10525) && !HasAchievement(q,10525)
                    && Scalar("SELECT kills FROM revora_summer_daily WHERE uid=@uid",p.Entity.UID)==1,"same-life retry gives no duplicate team or summer credit and unarmed belongs only to killer");
                p.Team=q.Team=team;
                var a=DemonMob(castle,58);var b=DemonMob(castle,58);
                using(var barrier=new System.Threading.Barrier(2)) {
                    var task=Task.Run(()=>Parallel.Invoke(
                        ()=>{lock(p.ItemSyncRoot){barrier.SignalAndWait();a.Die(p.Entity);}},
                        ()=>{lock(q.ItemSyncRoot){barrier.SignalAndWait();b.Die(q.Entity);}}));
                    Check(task.Wait(TimeSpan.FromSeconds(10)) && a.Dead && b.Dead && DemonKills(p)==3 && DemonKills(q)==3,
                        "durable concurrent item-triggered team kills finish without cross-player inventory lock deadlock");
                }
                p.Team=q.Team=null;
            }
            int start=(int)DemonKills(p);
            for(int kill=start;kill<needs[stage];kill++) {victim.Hitpoints=victim.MaxHitpoints;victim.Die(p.Entity);}
            Check(DemonKills(p)==needs[stage],"stage "+(stage+1)+" reaches its original count through server deaths");
            victim.Hitpoints=victim.MaxHitpoints;victim.Die(p.Entity);Check(DemonKills(p)==needs[stage],"stage "+(stage+1)+" count cannot exceed its cap");
            DemonMove(p,demonMarket);
            if(stage<3) {DemonNpc(p,2);Check(DemonKills(p)==0 && Scalar("SELECT stage FROM revora_demon_runs WHERE uid=@uid",p.Entity.UID)==stage+1,"turn-in advances exactly one stage and clears its counter");}
        }
        Check(Scalar("SELECT kills FROM revora_summer_daily WHERE uid=@uid",p.Entity.UID)==120
            && Scalar("SELECT count(*) FROM revora_demon_kill_credits WHERE uid=@uid",p.Entity.UID)==1800,
            "shared hunt caps summer at 120 and records exactly the 1800 credited Red Devil lives");
        Query("ALTER TABLE revora_demon_runs ADD CONSTRAINT demon_claim_failure CHECK(state<>'complete') NOT VALID");DemonNpc(p,2);
        Check(Fenced(p) && p.Inventory.Count==1 && Scalar("SELECT count(*) FROM items WHERE entityid=@uid",p.Entity.UID)==1
            && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11001",p.Entity.UID)==0,"failed final transaction preserves jar and rolls back all six rewards and achievement");
        Query("ALTER TABLE revora_demon_runs DROP CONSTRAINT demon_claim_failure");DemonReset(p);
        var fillers=Enumerable.Range(0,36).Select(i=>Pack(p,1088001,1)).ToArray();DemonNpc(p,2);
        Check(p.Inventory.Count==37 && Scalar("SELECT count(*) FROM revora_demon_runs WHERE uid=@uid AND state='complete'",p.Entity.UID)==0,"full reward inventory preserves the completed hunt for a later claim");
        foreach(var item in fillers) {Query("DELETE FROM items WHERE uid=@uid","uid",(long)item.UID);p.Inventory.Remove(item.UID,Enums.ItemUse.None,false);}
        var edge=Enumerable.Range(0,35).Select(i=>Pack(p,1088001,1)).ToArray();DemonNpc(p,2);
        Check(p.Inventory.Count==36 && Scalar("SELECT count(*) FROM revora_demon_runs WHERE uid=@uid AND state='active'",p.Entity.UID)==1,"new rune reward needs one more free slot than the legacy contract");
        foreach(var item in edge){Query("DELETE FROM items WHERE uid=@uid","uid",(long)item.UID);p.Inventory.Remove(item.UID,Enums.ItemUse.None,false);}
        Call(Demon,null,"Initialize");
        Check(Scalar("SELECT rune_reward_id FROM revora_demon_runs WHERE uid=@uid",p.Entity.UID)==savedRune,"initialization preserves the promised rune");
        Parallel.For(0,12,i=>DemonNpc(p,2));
        Check(p.Inventory.Count==6 && p.Inventory.Objects.Count(i=>i.ID==722136 && i.Bound)==5 && p.Inventory.Objects.Count(i=>i.ID==savedRune && i.Bound)==1
            && Scalar("SELECT count(*) FROM items WHERE entityid=@uid",p.Entity.UID)==6,"concurrent final claims exchange the jar for five bound EXP Balls and the saved yellow rune");
        Check(HasAchievement(p,11001) && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11001",p.Entity.UID)==1,"terminal transaction immediately awards the verified Demon Exterminators achievement");
        foreach(var item in p.Inventory.Objects.ToArray())p.Inventory.Remove(item.UID,Enums.ItemUse.None,false);
        ConquerItemTable.LoadItems(p);
        Check(p.Inventory.Count==6 && p.Inventory.Objects.Count(i=>i.ID==722136 && i.Bound)==5 && p.Inventory.Objects.Count(i=>i.ID==savedRune && i.Bound)==1,"ordinary item loader restores all six bound rewards after reconnect");
        EvidenceRefresh(p);DemonNpc(p,1);DemonNpc(p,2);
        Check(HasAchievement(p,11001) && HasAchievement(p,10525) && p.Inventory.Count==6,"evidence reload and repeated accept/claim preserve one completion and one reward");
        Check(Scalar("SELECT cardinality(reward_ids) FROM revora_demon_runs WHERE uid=@uid",p.Entity.UID)==6
            && p.Inventory.Objects.All(i=>i.ID<800000 || i.ID>=900000),"daily receipt records all six rewards without any P7 soul");
        var legacy=DemonPlayer();DemonNpc(legacy,1);
        Query("UPDATE revora_demon_runs SET rune_reward_id=0,stage=3,kills=1800 WHERE uid=@uid","uid",(long)legacy.Entity.UID);
        Call(Demon,null,"Initialize");DemonNpc(legacy,2);
        Check(legacy.Inventory.Count==5 && legacy.Inventory.Objects.All(i=>i.ID==722136 && i.Bound),"legacy contract retains only its original five EXP Balls after initialization");
        DemonMove(q,demonMarket);DemonNpc(q,3);Check(q.Inventory.Count==1,"abandonment preview does not consume the jar");DemonNpc(q,4);DemonNpc(q,1);
        Check(q.Inventory.Count==0 && Scalar("SELECT count(*) FROM revora_demon_runs WHERE uid=@uid AND state='abandoned'",q.Entity.UID)==1,"abandonment consumes the jar and uses today's chance");
        demonNow=demonNow.AddDays(1);DemonNpc(q,1);
        Check(q.Inventory.Count==1 && Scalar("SELECT count(*) FROM revora_demon_runs WHERE uid=@uid",q.Entity.UID)==2,"next Pacific day permits a fresh hunt without erasing history");
        var previousJar=q.Inventory.Objects.Single().UID;demonNow=demonNow.AddDays(1);DemonMove(q,birds);DemonMob(birds,18).Die(q.Entity);
        Check(DemonKills(q)==0 && (uint)Call(Demon,null,"JarCount",q)==0,"an unfinished hunt stops collecting souls after its daily boundary");
        DemonMove(q,demonMarket);DemonNpc(q,1);
        Check(q.Inventory.Count==1 && q.Inventory.Objects.Single().UID!=previousJar
            && Scalar("SELECT count(*) FROM items WHERE entityid=@uid",q.Entity.UID)==1,"accepting the next day replaces the expired jar without accumulating inventory clutter");
    }
}
