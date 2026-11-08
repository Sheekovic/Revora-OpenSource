using System;
using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type SpookQuest=typeof(Entity).Assembly.GetType("Revora.Game.Features.ExorcismQuest");
    static readonly Type SpookWorld=typeof(Entity).Assembly.GetType("Revora.Game.Features.ExorcismWorld");
    static DateTime spookNow=DateTime.UtcNow;
    static object[] SpookWorlds(){return ((IDictionary)SpookWorld.GetField("Maps",Hidden).GetValue(null)).Values.Cast<object>().ToArray();}
    static object SpookHunt(GameState p){return SpookWorlds().Single(h=>(uint)DragonField(h,"Owner")==p.Entity.UID);}
    static Entity SpookBoss(GameState p){return (Entity)DragonField(SpookHunt(p),"Body");}
    static long SpookDays(GameState p,string condition="true"){return Scalar("SELECT count(*) FROM revora_exorcism_daily WHERE uid=@uid AND "+condition,p.Entity.UID);}
    static long SpookRuns(GameState p,string condition="true"){return Scalar("SELECT count(*) FROM revora_exorcism_hunts WHERE uid=@uid AND "+condition,p.Entity.UID);}
    static GameState SpookPlayer(Map map){var p=ExorcismPlayer(map);p.Entity.MyAchievement=new Achievement(p.Entity);p.Entity.MyAchievement.Load("");p.LastResetTime=spookNow.ToLocalTime();p.Entity.ConquerPoints=20000;EntityTable.SaveEntity(p);return p;}
    static void SpookNpc(GameState p,byte option){p.ActiveNpc=12333;ExorcismFrame(p,option);}
    static void EarthNpc(GameState p,byte option){p.ActiveNpc=900000132;var frame=new byte[28];Writer.WriteUInt16(20,0,frame);Writer.WriteUInt16(2032,2,frame);frame[14]=option;Writer.WriteUInt64(PacketHandler.ClientSeal,20,frame);PacketHandler.HandlePacket(frame,p);}
    static void SpookBegin(GameState p,byte grade){SpookNpc(p,grade);SpookNpc(p,12);EarthNpc(p,3);}
    static void ClearSpooks(){foreach(var h in SpookWorlds())Call(SpookWorld,null,"Release",h);}
    static void SpookTick(){spookNow=spookNow.AddSeconds(3);Call(SpookWorld,null,"Tick");}
    static void SpookKill(GameState p,Entity boss){p.Entity.X=(ushort)(boss.X+1);p.Entity.Y=boss.Y;uint hit=boss.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,boss,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=boss.UID},ref hit,null);}
    static void SpookReturn(GameState p){AdventureNear(p,900000132);EarthNpc(p,2);ExorcismStand(p);}
    static void VerifyExorcismQuest(string runtime,Map town) {
        VerifyFirstRebirth(runtime,town);Query("CREATE TABLE npcs (LIKE public.npcs INCLUDING ALL);CREATE TABLE sobnpcs (LIKE public.sobnpcs INCLUDING ALL);CREATE TABLE furniture (LIKE public.furniture INCLUDING ALL)");var market=World(runtime,1036);World(runtime,1013);MiningNpc(market,12333);MiningNpc(town,79882);
        foreach(ushort id in new ushort[]{1002,1036,1013})DMaps.MapPaths[id]="maps\\"+id+".map";
        Call(SpookQuest,null,"Initialize");Call(SpookQuest,null,"Initialize");
        SpookQuest.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>spookNow));SoulBall.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>spookNow));
        VerifySpookCombat(market);
        Call(MonsterLoot,null,"Initialize");
        MonsterLoot.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>max-1));
        for(byte grade=1;grade<=3;grade++) {
            var p=SpookPlayer(market);SpookNpc(p,11);Check(!Fenced(p) && SpookDays(p)==0,"unearned daily claim cannot invent a victory");
            SpookBegin(p,grade);var hunt=SpookHunt(p);var boss=SpookBoss(p);var map=p.Map;
            Check(!Fenced(p) && p.Account.State==AccountTable.AccountState.Player && p.Entity.MapID!=1013 && map.BaseID==1013 && map.Portals.Count==0
                && map.Npcs[900000132].MapID==map.ID && map.Npcs[900000132].X==36 && map.Npcs[8308].MapID==map.ID && map.Npcs.Count==2,
                "ordinary NPC route enters an isolated, portal-free hunt with owned NPCs: "+grade);
            Check(SpookRuns(p,"state='active'")==1 && SpookDays(p,"state='accepted'")==1 && boss.Boss==1 && boss.MonsterInfo.Boss
                && boss.Hitpoints==new uint[]{10000000,32000000,55000000}[grade-1] && boss.Hitpoints==boss.MaxHitpoints,"difficulty persists and summons the complete long-bar HP value: "+grade);
            BossHealthPackets(boss);EarthNpc(p,3);Check(ReferenceEquals(boss,SpookBoss(p)) && map.Entities.Count==1,"repeated summoning cannot create another boss");
            AdventureWalk(p,(ushort)(boss.X+1),boss.Y);Check(Revora.Game.Attacking.Handle.CanAttack(p.Entity,boss,null,true),"ordinary admitted player can fight its owned boss");
            SpookKill(p,boss);Check(!Fenced(p) && boss.Dead && SpookRuns(p,"state='victory'")==1 && SpookDays(p,"state='victory'")==1
                && p.Entity.ThirllingSpookPrice && !p.Entity.ThirllingSpook,"lethal production hit records an unpaid victory without conflicting completion flags");
            Check(HasAchievement(p,10529) && EpkCount("SELECT count(*) FROM revora_achievement_evidence e JOIN revora_exorcism_hunts h ON e.source_key=replace(h.id::text,'-','') WHERE e.uid=@uid AND e.achievement_id=10529 AND e.source='spook-kill-v1' AND h.state='victory' AND h.killer=e.uid","uid",(long)p.Entity.UID)==1,
                "each difficulty earns the Spook kill achievement from its committed lethal hit: "+grade);
            Check(HasAchievement(p,10525)==(grade>=2),"Spook unarmed credit respects the level-120 boundary: "+grade);
            boss.Die(p.Entity);Check(SpookDays(p,"state='victory'")==1 && p.Inventory.Count==0,"corpse replay cannot pay or create uncontrolled loot");
            SpookReturn(p);ulong xp=p.Entity.Experience,award=p.ExpBall;SpookNpc(p,11);int study=Config.DailyExorcismStudy*(1<<(grade-1));
            Check(!Fenced(p) && SpookDays(p,"state='claimed'")==1 && p.Entity.ThirllingSpook && !p.Entity.ThirllingSpookPrice
                && p.Entity.ConquerPoints==22500 && p.Entity.RevoraPoint==15 && p.Entity.SubClasses.StudyPoints==study && p.Entity.Experience==xp+award
                && p.Inventory.Objects.Count(i=>i.ID==711211)==grade && p.Inventory.Contains(Config.DailyQuestChiToken,1) && p.Inventory.Objects.All(i=>i.Bound==(i.ID==4030101)),"earned tier pays complete CP/RVP/EXP/study/Chi/token bundle once: "+grade);
            p=ReloadBall(p);ExorcismStand(p);Call(SpookQuest,null,"Refresh",p);SpookNpc(p,11);SpookNpc(p,grade);SpookNpc(p,12);
            Check(!Fenced(p) && SpookDays(p)==1 && p.Entity.ThirllingSpook && p.Entity.ConquerPoints==22500 && p.Entity.MapID==1036,"earned completion and exact bundle survive production reload and replay");
            VerifyStudyAward(p,"exorcism");
            VerifyNoDailyP7(p);
            var rune=p.Inventory.Objects.Single(i=>i.ID==4030101);
            Check(rune.Bound && SavedRune(rune,"bound")==1
                && Scalar("SELECT count(*) FROM revora_exorcism_daily r JOIN items i ON i.uid=ANY(r.reward_uids) AND i.entityid=r.uid WHERE r.uid=@uid AND r.rune_reward AND r.state='claimed' AND i.id=4030101 AND i.bound=1",p.Entity.UID)==1,
                "earned Exorcism tier preserves exact bound rune receipt through fresh reload");
            p.Entity.MyAchievement.Send();Check(HasAchievement(p,10529),"production reload retains the earned Spook kill achievement");
            Check(HasAchievement(p,10525)==(grade>=2),"Spook unarmed eligibility survives production reload: "+grade);
            Check(SpookWorlds().Length==0 && !Kernel.Maps.ContainsKey(map.ID),"return releases owned map and NPCs without affecting public terrain");
            if(grade==3){SpookNpc(p,6);SpookNpc(p,8);var pack=p.Inventory.Objects.Single(i=>i.ID==720948);ExorcismRoll(85,99);ItemHandler.UseItem(pack,p);
                Check(!Fenced(p) && ExorcismExchanges(p)==1 && ExorcismOpenings(p)==1 && p.Inventory.Contains(800253,1),"earned strongest-hunt tokens exchange into an owned pack and P4 soul");}
        }
        VerifySpookPaid(market);
        VerifySpookTeams(market);
        VerifySpookSources(town,market);
        VerifySpookFailures(market);
        VerifySpookLifecycle(market);
        VerifySpookBoundaries(town,market);
        VerifySpookSpiritFailures(town,market);
        ClearSpooks();
        var oldContract=SpookPlayer(market);SpookBegin(oldContract,2);
        Query("UPDATE revora_exorcism_daily SET valuable_reward=true,rune_reward=false WHERE uid=@uid","uid",(long)oldContract.Entity.UID);
        SpookKill(oldContract,SpookBoss(oldContract));SpookReturn(oldContract);SpookNpc(oldContract,11);
        Check(!Fenced(oldContract) && SpookDays(oldContract,"state='claimed' AND valuable_reward")==1
            && oldContract.Inventory.Count==3 && oldContract.Inventory.Objects.Count(i=>i.ID==Config.DailyExorcismToken)==2
            && oldContract.Inventory.Contains(Config.DailyQuestChiToken,1)
            && oldContract.Inventory.Objects.All(i=>i.ID<800000 || i.ID>=900000),
            "obsolete P7 flag still pays only original Chi and two tokens");
        var oldItems=oldContract.Inventory.Objects.Select(i=>i.UID).OrderBy(id=>id).ToArray();uint oldCp=oldContract.Entity.ConquerPoints;
        oldContract=ReloadBall(oldContract);ExorcismStand(oldContract);SpookNpc(oldContract,11);
        Check(!Fenced(oldContract) && oldContract.Entity.ConquerPoints==oldCp
            && oldContract.Inventory.Objects.Select(i=>i.UID).OrderBy(id=>id).SequenceEqual(oldItems)
            && SpookDays(oldContract,"state='claimed' AND valuable_reward")==1,
            "old contract gifts persist and replay after reconnect cannot award another bundle");
        ClearSpooks();
    }
    static void VerifySpookPaid(Map market) {
        var p=SpookPlayer(market);SpookNpc(p,10);Check(SpookDays(p)==0 && p.Entity.ConquerPoints==20000,"forged paid confirmation cannot charge");
        SpookNpc(p,7);SpookNpc(p,255);SpookNpc(p,10);Check(SpookDays(p)==0 && p.Entity.ConquerPoints==20000,"native cancellation revokes paid completion");
        SpookNpc(p,7);spookNow=spookNow.AddMinutes(2);SpookNpc(p,10);Check(SpookDays(p)==0,"expired paid offer cannot complete a daily hunt");
        SpookNpc(p,7);Parallel.For(0,16,n=>SpookNpc(p,10));
        Check(!Fenced(p) && SpookDays(p,"state='claimed' AND paid AND grade=3")==1 && p.Entity.ConquerPoints==17500 && p.Entity.RevoraPoint==15
            && p.Entity.SubClasses.StudyPoints==100 && p.Inventory.Objects.Count(i=>i.ID==711211)==3,"concurrent paid completion charges once and grants the strongest-tier reward");
        p.Entity.MyAchievement.Send();Check(!HasAchievement(p,10529) && !HasAchievement(p,10525),"paid quest completion cannot claim Spook or unarmed kill achievements");
        p=ReloadBall(p);ExorcismStand(p);DialyQuestTable.Load(p);p.Entity.ThirllingSpook=false;p.Entity.ThirlSpookLevel=0;DialyQuestTable.Save(p);SpookNpc(p,7);SpookNpc(p,10);
        Check(!Fenced(p) && SpookDays(p)==1 && p.Entity.ConquerPoints==17500,"legacy flag reset cannot reopen the durable completed day");
        spookNow=spookNow.AddDays(1);SpookNpc(p,7);SpookNpc(p,10);Check(!Fenced(p) && SpookDays(p)==2 && p.Entity.ConquerPoints==15000,"next Pacific day permits exactly one new daily reward");
        spookNow=spookNow.AddDays(-2);SpookNpc(p,7);SpookNpc(p,10);Check(SpookDays(p)==2,"clock rollback cannot reopen an earlier completion day");spookNow=DateTime.UtcNow;
        var accepted=SpookPlayer(market);SpookNpc(accepted,1);SpookNpc(accepted,7);SpookNpc(accepted,10);
        Check(!Fenced(accepted) && SpookDays(accepted,"grade=3 AND state='claimed'")==1 && accepted.Entity.SubClasses.StudyPoints==100,"paid finish upgrades an accepted lesser hunt to the reviewed strongest-tier reward");
    }
    static void VerifySpookTeams(Map market) {
        var leader=SpookPlayer(market);var member=SpookPlayer(market);var absent=SpookPlayer(market);var stranger=SpookPlayer(market);TeratoTeam(leader,member,absent);
        SpookNpc(leader,3);SpookNpc(member,1);SpookNpc(absent,3);SpookNpc(leader,12);var map=leader.Map;
        SpookNpc(member,12);Check(member.Entity.MapID==1036,"mismatched accepted difficulty cannot join a different tier");
        SpookNpc(member,3);SpookNpc(member,12);Check(!Fenced(member) && member.Entity.MapID==map.ID && SpookWorlds().Length==1,"invited teammate may match the summoner's tier and join the same instance");
        stranger.Entity.Teleport(1013,map.ID,36,27);Check(stranger.Entity.MapID==1036,"outsider teleport cannot enter a private hunt");
        EarthNpc(member,3);var boss=SpookBoss(leader);leader.Entity.X=member.Entity.X=(ushort)(boss.X+1);leader.Entity.Y=member.Entity.Y=boss.Y;
        Check(!Revora.Game.Attacking.Handle.CanAttack(member.Entity,leader.Entity,null,true),"private Exorcism hunt blocks player-versus-player damage");
        boss.Die(stranger.Entity);Check(!boss.Dead,"unadmitted killer cannot forge a team victory");
        SpookKill(member,boss);Check(!Fenced(member) && SpookDays(leader,"state='victory'")==1 && SpookDays(member,"state='victory'")==1 && SpookDays(absent,"state='victory'")==0,"only physically admitted nearby teammates earn victory");
        foreach(var player in new[]{member,leader,absent,stranger})player.Entity.MyAchievement.Send();
        Check(HasAchievement(member,10529) && !HasAchievement(leader,10529) && !HasAchievement(absent,10529) && !HasAchievement(stranger,10529),
            "Spook kill evidence belongs to the actual killer, independently of nearby team quest entitlements");
        Check(HasAchievement(member,10525) && !HasAchievement(leader,10525) && !HasAchievement(absent,10525) && !HasAchievement(stranger,10525),"Spook unarmed credit belongs only to the actual killer");
        SpookReturn(member);SpookNpc(member,11);SpookReturn(leader);SpookNpc(leader,11);
        Check(!Fenced(leader) && !Fenced(member) && SpookDays(leader,"state='claimed'")==1 && SpookDays(member,"state='claimed'")==1,"each teammate independently claims a complete strongest-tier daily reward");
        var fresh=SpookPlayer(market);TeratoTeam(fresh,member);SpookNpc(fresh,1);SpookNpc(fresh,12);SpookNpc(member,12);EarthNpc(fresh,3);boss=SpookBoss(fresh);
        fresh.Entity.X=member.Entity.X=(ushort)(boss.X+1);fresh.Entity.Y=member.Entity.Y=boss.Y;SpookKill(member,boss);SpookReturn(member);SpookNpc(member,11);SpookReturn(fresh);SpookNpc(fresh,11);
        Check(!Fenced(fresh) && SpookDays(fresh,"state='claimed'")==1 && SpookDays(member,"state='claimed'")==1 && member.Entity.ConquerPoints==22500,"already-rewarded teammate may help another eligible hunter without earning twice");
    }
    static void VerifySpookSources(Map town,Map market) {
        for(byte grade=1;grade<=3;grade++) {
            var p=SoulBallPlayer(town);p.LastResetTime=spookNow.ToLocalTime();EntityTable.SaveEntity(p);SoulNpc(p,7);SoulNpc(p,8);p.Entity.Teleport(1036,243,152);ExorcismStand(p);
            SpookBegin(p,grade);var boss=SpookBoss(p);SpookKill(p,boss);
            Check(!Fenced(p) && SpookDays(p,"state='victory'")==1 && SoulSpirits(p)==new uint[]{600,800,1000}[grade-1]
                && Scalar("SELECT count(*) FROM revora_soul_ball_bosses WHERE uid=@uid AND source='spook'",p.Entity.UID)==1,"registered daily Spook victory commits its exact spirit bonus: "+grade);
            boss.Die(p.Entity);SpookReturn(p);p=ReloadBall(p);Call(SoulBall,null,"Refresh",p);
            Check(!Fenced(p) && SoulSpirits(p)==new uint[]{600,800,1000}[grade-1],"Spook spirit credit persists and cannot replay after reconnect");
        }
    }
    static void VerifySpookFailures(Map market) {
        foreach(string table in new[]{"revora_exorcism_hunts","revora_exorcism_daily","revora_exorcism_members","revora_achievement_evidence"}) {
            var p=SpookPlayer(market);SpookBegin(p,2);var boss=SpookBoss(p);
            Query("CREATE FUNCTION suppress_spook() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_spook BEFORE "+(table=="revora_achievement_evidence"?"INSERT":"UPDATE")+" ON "+table+" FOR EACH ROW"+(table=="revora_achievement_evidence"?" WHEN(NEW.achievement_id=10525)":"")+" EXECUTE FUNCTION suppress_spook()");
            try{SpookKill(p,boss);}finally{Query("DROP TRIGGER suppress_spook ON "+table+";DROP FUNCTION suppress_spook()");}
            Check(Fenced(p) && !boss.Dead && SpookRuns(p,"state='active'")==1 && SpookDays(p,"state='accepted'")==1 && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10529",p.Entity.UID)==0,"failed victory write preserves living boss and unpaid commission: "+table);
            Check(Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10525",p.Entity.UID)==0,"failed Spook victory rolls back unarmed evidence: "+table);
            ClearSpooks();p=ReloadBall(p);p.Entity.Teleport(1036,243,152);ExorcismStand(p);SpookNpc(p,12);boss=SpookBoss(p);SpookKill(p,boss);SpookReturn(p);
            Check(!Fenced(p) && SpookRuns(p)==1 && SpookDays(p,"state='victory'")==1 && HasAchievement(p,10529),"production reload resumes the same saved hunt and retries victory: "+table);
            Check(HasAchievement(p,10525),"retried Spook victory earns unarmed credit: "+table);
        }
        foreach(string fault in new[]{"evidence","profile","balance","item","rune","receipt","skills","recovery"}) {
            var p=fault=="recovery"?PendingFirstLife(Kernel.Maps[1002]):SpookPlayer(market);if(p.Entity.MapID!=1036)p.Entity.Teleport(1036,243,152);ExorcismStand(p);p.Entity.ConquerPoints=20000;
            if(fault=="skills")p.Entity.Experience=DataHolder.LevelExperience(135)-1;EntityTable.SaveEntity(p);SpookNpc(p,7);
            string table=fault=="evidence"?"revora_achievement_evidence":fault=="profile" || fault=="balance"?"entities":fault=="item" || fault=="rune"?"items":fault=="receipt"?"revora_exorcism_daily":fault=="skills"?"skills":"revora_first_rebirths";
            string verb=fault=="evidence"?"INSERT":fault=="item" || fault=="rune" || fault=="skills"?"INSERT":"UPDATE";
            string body=fault=="balance"?"IF NEW.conquerpoints<>OLD.conquerpoints THEN RETURN NULL; END IF; RETURN NEW;":"RETURN NULL;";
            Query("CREATE FUNCTION suppress_spook_claim() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN "+body+" END';CREATE TRIGGER suppress_spook_claim BEFORE "+verb+" ON "+table+" FOR EACH ROW"+(fault=="rune"?" WHEN(NEW.id=4030101)":"")+" EXECUTE FUNCTION suppress_spook_claim()");
            ulong xp=p.Entity.Experience;uint rvp=p.Entity.RevoraPoint;ushort study=p.Entity.SubClasses.StudyPoints;int count=p.Inventory.Count;byte level=p.Entity.Level;try{SpookNpc(p,10);}finally{Query("DROP TRIGGER suppress_spook_claim ON "+table+";DROP FUNCTION suppress_spook_claim()");}
            Check(Fenced(p) && SpookDays(p,"state='claimed'")==0 && p.Entity.ConquerPoints==20000 && p.Entity.RevoraPoint==rvp && p.Entity.SubClasses.StudyPoints==study
                && p.Entity.Experience==xp && p.Entity.Level==level && p.Inventory.Count==count && Scalar("SELECT count(*) FROM items WHERE entityid=@uid",p.Entity.UID)==count,"failed paid claim rolls back fee and complete reward: "+fault);
            if(fault=="evidence")VerifyStudyRollback(p);
            p=ReloadBall(p);ExorcismStand(p);SpookNpc(p,7);SpookNpc(p,10);
            Check(!Fenced(p) && SpookDays(p,"state='claimed'")==1 && p.Entity.ConquerPoints==17500 && (fault!="recovery" || p.Entity.Level==139 && LifePending(p)==0),"ordinary reload retries the complete paid reward: "+fault);
            if(fault=="evidence")VerifyStudyAward(p,"exorcism");
        }
    }
    static void VerifySpookLifecycle(Map market) {
        var p=SpookPlayer(market);SpookBegin(p,2);var hunt=SpookHunt(p);var until=(DateTime)DragonField(hunt,"Until");var old=SpookBoss(p);ClearSpooks();p=ReloadBall(p);ExorcismStand(p);SpookNpc(p,12);
        Check(!Fenced(p) && SpookRuns(p)==1 && SpookBoss(p)!=old && (DateTime)DragonField(SpookHunt(p),"Until")==until,"runtime reconstruction retains original absolute deadline and one saved hunt");
        spookNow=until;Call(SpookWorld,null,"Tick");Check(!Fenced(p) && p.Entity.MapID==1036 && SpookWorlds().Length==0 && SpookRuns(p,"state='expired'")==1,"one-hour timeout returns the hunter and removes the private instance");
        ExorcismStand(p);SpookNpc(p,12);Check(!Fenced(p) && SpookRuns(p)==2 && p.Entity.MapID!=1036,"failed timed hunt may retry its unpaid daily commission");
        ClearSpooks();spookNow=DateTime.UtcNow;
        var leader=SpookPlayer(market);var member=SpookPlayer(market);TeratoTeam(leader,member);SpookNpc(leader,1);SpookNpc(member,1);SpookNpc(leader,12);SpookNpc(member,12);EarthNpc(leader,3);
        member.Team=null;SpookTick();Check(member.Entity.MapID==1036 && leader.Entity.MapID!=1036 && !Fenced(leader),"leaving the summoner's team ejects only that participant");
        ExorcismStand(member);SpookNpc(member,12);Check(member.Entity.MapID!=leader.Entity.MapID,"departed participant cannot rejoin the original hunt by replaying entry");ClearSpooks();
        var full=SpookPlayer(market);SpookBegin(full,3);SpookKill(full,SpookBoss(full));SpookReturn(full);while(full.Inventory.Count<36)Pack(full,1088001,1);SpookNpc(full,11);
        Check(!Fenced(full) && SpookDays(full,"state='victory'")==1 && full.Entity.ConquerPoints==20000,"full bag preserves earned victory and every reward");
        DisRemove(full,full.Inventory.Objects.First());SpookNpc(full,11);Check(!Fenced(full) && SpookDays(full,"state='claimed'")==1 && full.Inventory.Count==40,"making room claims the full saved victory bundle");
    }
    static void VerifySpookBoundaries(Map town,Map market) {
        var social=SpookPlayer(market);int beforeTeam=Kernel.Maps.Count;var ordinaryTeam=new Revora.Game.ConquerStructures.Team(social);
        Check(Kernel.Maps.Count==beforeTeam && ordinaryTeam.SpookMap==null,"creating an ordinary team does not allocate an unused quest map");
        foreach(string state in new[]{"remote","trade","dead","fake","unloaded","replaced","low-level"}) {
            var p=SpookPlayer(market);if(state=="remote")p.Entity.X=1;if(state=="trade")p.Trade.InTrade=true;if(state=="dead")p.Entity.Hitpoints=0;
            if(state=="fake")p.Fake=true;if(state=="unloaded")p.Entity.FullyLoaded=false;if(state=="replaced")Kernel.GamePool[p.Entity.UID]=new GameState(null);
            if(state=="low-level"){p.Entity.Level=89;EntityTable.SaveEntity(p);}SpookNpc(p,3);SpookNpc(p,7);SpookNpc(p,10);
            Check(!Fenced(p) && SpookDays(p)==0 && p.Entity.ConquerPoints==20000,"ineligible Exorcism service cannot accept or charge: "+state);
        }
        foreach(string state in new[]{"cp-full","rvp-full","study-full","poor","curse"}) {
            var p=SpookPlayer(market);if(state=="cp-full")p.Entity.ConquerPoints=uint.MaxValue;if(state=="rvp-full")p.Entity.RevoraPoint=uint.MaxValue;
            if(state=="study-full")p.Entity.SubClasses.StudyPoints=ushort.MaxValue;if(state=="poor")p.Entity.ConquerPoints=0;
            if(state=="curse")p.Entity.AddFlag((ulong)FlagsUpdate.Flags.Cursed,60,false);EntityTable.SaveEntity(p);
            if(state=="cp-full"){SpookBegin(p,1);SpookKill(p,SpookBoss(p));SpookReturn(p);SpookNpc(p,11);}
            else{SpookNpc(p,7);SpookNpc(p,10);}
            Check(!Fenced(p) && SpookDays(p,"state='claimed'")==0 && p.Inventory.Count==0,"unreceivable complete reward remains unpaid: "+state);
        }
        var low=SpookPlayer(market);low.Entity.Level=90;EntityTable.SaveEntity(low);SpookBegin(low,1);SpookKill(low,SpookBoss(low));SpookReturn(low);SpookNpc(low,11);
        Check(!Fenced(low) && SpookDays(low,"state='claimed'")==1,"exact level-90 minimum can complete the ordinary hunt");
        var legacy=SpookPlayer(market);DialyQuestTable.Load(legacy);legacy.Entity.ThirllingSpook=true;legacy.Entity.ThirllingSpookPrice=true;legacy.Entity.ThirlSpookLevel=3;DialyQuestTable.Save(legacy);SpookNpc(legacy,0);SpookNpc(legacy,11);
        Check(!Fenced(legacy) && SpookDays(legacy,"legacy AND state='claimed'")==1 && legacy.Entity.ConquerPoints==20000 && legacy.Inventory.Count==0,"ambiguous persisted old completion flags close the day without inventing another reward");
        var oldAccepted=SpookPlayer(market);DialyQuestTable.Load(oldAccepted);oldAccepted.Entity.ThirlSpookLevel=2;oldAccepted.Entity.ThirllingSpookPrice=true;DialyQuestTable.Save(oldAccepted);SpookNpc(oldAccepted,0);SpookNpc(oldAccepted,11);
        Check(!Fenced(oldAccepted) && SpookDays(oldAccepted,"legacy AND state='accepted' AND grade=2")==1 && oldAccepted.Inventory.Count==0,"legacy selection imports but an untrusted old price flag cannot prove victory");
        var frozen=SpookPlayer(market);SpookNpc(frozen,1);uint cp=Config.DailyQuestCp,fee=Config.DailyExorcismSkipCp;ushort study=Config.DailyExorcismStudy;
        try{Config.DailyQuestCp+=100;Config.DailyExorcismSkipCp+=100;Config.DailyExorcismStudy+=100;SpookNpc(frozen,7);SpookNpc(frozen,10);}
        finally{Config.DailyQuestCp=cp;Config.DailyExorcismSkipCp=fee;Config.DailyExorcismStudy=study;}
        Check(!Fenced(frozen) && frozen.Entity.ConquerPoints==20000-fee+cp && frozen.Entity.SubClasses.StudyPoints==study*4,"accepted commission freezes its purse, price and study terms");
        var active=SpookPlayer(market);SpookBegin(active,2);var boss=SpookBoss(active);var hunt=SpookHunt(active);
        active.Entity.X=(ushort)(boss.X+1);active.Entity.Y=boss.Y;active.Attackable=true;active.Entity.MaxHitpoints=active.Entity.Hitpoints=200000;
        new Revora.Game.Attacking.Handle(null,boss,active.Entity);
        Check(active.Entity.Hitpoints==200000,"registered Spook announces its attack before dealing damage");
        spookNow=spookNow.AddSeconds(2);new Revora.Game.Attacking.Handle(null,boss,active.Entity);
        Check(active.Entity.Hitpoints<200000 && !active.Entity.Dead,"registered Spook's named attack deals damage to its admitted player");spookNow=spookNow.AddSeconds(2);
        var ready=new ManualResetEventSlim();var release=new ManualResetEventSlim();var holder=Task.Run(()=>{lock(active.ItemSyncRoot){ready.Set();release.Wait();}});ready.Wait();
        try{boss.Die(active.Entity);Check(!boss.Dead && SpookDays(active,"state='victory'")==0,"busy inventory owner leaves the living boss available for another attack");}finally{release.Set();holder.Wait();ready.Dispose();release.Dispose();}
        var fake=new Entity(EntityFlag.Monster,false){UID=boss.UID,MapID=boss.MapID,MonsterInfo=boss.MonsterInfo.Copy(),Hitpoints=100,MaxHitpoints=100};fake.MonsterInfo.Owner=fake;fake.Die(active.Entity);
        Check(!boss.Dead && SpookDays(active,"state='victory'")==0,"copied family and actor UID cannot impersonate the registered Spook");
        var pet=new Entity(EntityFlag.Monster,true){UID=active.Entity.UID-200000,Owner=active,MonsterInfo=new MonsterInformation(),MapID=active.Map.ID,X=boss.X,Y=boss.Y,Hitpoints=1000};active.Companion=pet;
        boss.Die(pet);Check(!boss.Dead,"unregistered companion cannot claim a Spook victory");active.Map.Companions.Add(pet.UID,pet);boss.Die(pet);
        Check(!Fenced(active) && boss.Dead && SpookDays(active,"state='victory'")==1,"registered companion credits its ordinary owner once");Check(HasAchievement(active,10529),"registered companion earns Spook kill evidence for its ordinary owner");active.Map.Companions.Remove(pet.UID);active.Companion=null;SpookReturn(active);
        Check(!HasAchievement(active,10525),"level-120 Spook companion kill cannot grant unarmed credit to its owner");
        Query("CREATE TABLE IF NOT EXISTS apprentice AS TABLE public.apprentice WITH NO DATA");var mentor=SpookPlayer(market);var child=SpookPlayer(market);MentorLink(mentor,child,100);
        child.Entity.Experience=DataHolder.LevelExperience(135)-1;EntityTable.SaveEntity(child);SpookNpc(child,7);
        Query("CREATE FUNCTION suppress_spook_mentor() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_spook_mentor BEFORE UPDATE ON apprentice FOR EACH ROW EXECUTE FUNCTION suppress_spook_mentor()");
        try{SpookNpc(child,10);}finally{Query("DROP TRIGGER suppress_spook_mentor ON apprentice;DROP FUNCTION suppress_spook_mentor()");}
        Check(Fenced(child) && child.Entity.ConquerPoints==20000 && child.Entity.Level==135 && MentorBalance(child)==100 && SpookDays(child,"state='claimed'")==0,"mentor write failure rolls back the paid daily reward and level gain");
        child=ReloadBall(child);Revora.Database.KnownPersons.LoadMentor(child);ExorcismStand(child);SpookNpc(child,7);SpookNpc(child,10);
        Check(!Fenced(child) && child.Entity.Level==136 && MentorBalance(child)==372 && SpookDays(child,"state='claimed'")==1,"ordinary reconnect commits earned progression and mentor credit with the quest purse");
        var reserved=Map.DynamicIDs.Next;var oldCounter=Map.DynamicIDs;Map.DynamicIDs=new Counter(reserved-1){Finish=60000};MapsTable.MapInformations[(ushort)reserved]=new MapsTable.MapInformation{ID=(ushort)reserved,BaseID=1500,Status=123};
        try{var p=SpookPlayer(market);SpookBegin(p,1);Check(!Fenced(p) && p.Map.ID!=reserved && MapsTable.MapInformations[(ushort)reserved].BaseID==1500,"private allocation preserves unloaded public-map metadata");ClearSpooks();}
        finally{MapsTable.MapInformations.Remove((ushort)reserved);Map.DynamicIDs=oldCounter;}
        var broken=SpookPlayer(market);SpookNpc(broken,1);int maps=Kernel.Maps.Count;Query("ALTER TABLE npcs RENAME TO held_spook_npcs");
        try{SpookNpc(broken,12);}finally{Query("ALTER TABLE held_spook_npcs RENAME TO npcs");}
        Check(Fenced(broken) && SpookRuns(broken)==1 && SpookDays(broken,"state='accepted'")==1 && SpookWorlds().Length==0 && Kernel.Maps.Count==maps,"failed map construction cleans its partial map while retaining the saved unpaid hunt");
        broken=ReloadBall(broken);ExorcismStand(broken);SpookNpc(broken,12);EarthNpc(broken,3);Check(!Fenced(broken) && SpookRuns(broken)==1 && SpookBoss(broken)!=null,"reconnect restores the saved hunt after construction failure");ClearSpooks();
        var capacity=new System.Collections.Generic.List<GameState>();for(int n=0;n<16;n++){var p=SpookPlayer(market);SpookBegin(p,1);capacity.Add(p);}
        var overflow=SpookPlayer(market);SpookNpc(overflow,1);SpookNpc(overflow,12);
        Check(!Fenced(overflow) && SpookWorlds().Length==16 && SpookRuns(overflow)==0 && overflow.Entity.ConquerPoints==20000,"seventeenth concurrent hunt is refused without a charge or leaked map");ClearSpooks();
    }
    static void VerifySpookSpiritFailures(Map town,Map market) {
        foreach(string table in new[]{"revora_soul_ball_runs","revora_soul_ball_bosses"}) {
            var p=SoulBallPlayer(town);SoulNpc(p,7);SoulNpc(p,8);p.Entity.Teleport(1036,243,152);ExorcismStand(p);SpookBegin(p,3);var boss=SpookBoss(p);
            Query("CREATE FUNCTION suppress_spook_spirit() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_spook_spirit BEFORE "+(table=="revora_soul_ball_runs"?"UPDATE":"INSERT")+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_spook_spirit()");
            try{SpookKill(p,boss);}finally{Query("DROP TRIGGER suppress_spook_spirit ON "+table+";DROP FUNCTION suppress_spook_spirit()");}
            Check(Fenced(p) && !boss.Dead && SoulSpirits(p)==0 && SoulBossCredits(p)==0 && SpookDays(p,"state='accepted'")==1 && SpookRuns(p,"state='active'")==1,"failed spirit write rolls back the boss and every daily victory entitlement: "+table);
            ClearSpooks();p=ReloadBall(p);p.Entity.Teleport(1036,243,152);ExorcismStand(p);SpookNpc(p,12);SpookKill(p,SpookBoss(p));SpookReturn(p);
            Check(!Fenced(p) && SoulSpirits(p)==1000 && SoulBossCredits(p)==1 && SpookDays(p,"state='victory'")==1,"resume retries one complete boss-to-bead transaction: "+table);
        }
        var complete=SoulBallPlayer(town);SoulNpc(complete,7);SoulNpc(complete,9);complete.Entity.Teleport(1036,243,152);ExorcismStand(complete);SpookBegin(complete,3);
        SpookKill(complete,SpookBoss(complete));Check(!Fenced(complete) && SoulSpirits(complete)==500,"Spook bonus caps at the issued Super bead's actual requirement");
        SpookReturn(complete);SpookNpc(complete,11);ItemHandler.UseItem(SoulBead(complete),complete);complete=ReloadBall(complete);
        Check(!Fenced(complete) && SpookDays(complete,"state='claimed'")==1 && SoulDone(complete)==1 && complete.Entity.ConquerPoints==24200
            && complete.Entity.SubClasses.StudyPoints==180 && complete.Entity.HeavenBlessing==172800,"earned Spook victory pays its daily bundle and completes an issued Super bead through both atomic reward handlers");
        var helper=SoulBallPlayer(town);SoulNpc(helper,7);SoulNpc(helper,8);helper.Entity.Teleport(1036,243,152);ExorcismStand(helper);SpookNpc(helper,7);SpookNpc(helper,10);
        var hunter=SpookPlayer(market);TeratoTeam(hunter,helper);SpookNpc(hunter,1);SpookNpc(hunter,12);SpookNpc(helper,12);EarthNpc(hunter,3);var assisted=SpookBoss(hunter);
        hunter.Entity.X=helper.Entity.X=(ushort)(assisted.X+1);hunter.Entity.Y=helper.Entity.Y=assisted.Y;SpookKill(helper,assisted);
        Check(!Fenced(helper) && SpookDays(hunter,"state='victory'")==1 && SpookDays(helper,"state='claimed'")==1 && SoulSpirits(helper)==0 && SoulBossCredits(helper)==0,"already-completed helper cannot farm repeated Spook spirit bonuses for its carried bead");SpookReturn(helper);SpookReturn(hunter);
        var oldLeader=SpookPlayer(market);var traveler=SpookPlayer(market);TeratoTeam(oldLeader,traveler);SpookNpc(oldLeader,1);SpookNpc(traveler,1);SpookNpc(oldLeader,12);SpookNpc(traveler,12);EarthNpc(oldLeader,3);
        var firstBoss=SpookBoss(oldLeader);oldLeader.Entity.X=traveler.Entity.X=(ushort)(firstBoss.X+1);oldLeader.Entity.Y=traveler.Entity.Y=firstBoss.Y;SpookKill(traveler,firstBoss);
        traveler.Entity.Teleport(1036,243,152);ExorcismStand(traveler);SpookNpc(traveler,11);
        var nextLeader=SpookPlayer(market);TeratoTeam(nextLeader,traveler);SpookNpc(nextLeader,1);SpookNpc(nextLeader,12);SpookNpc(traveler,12);EarthNpc(nextLeader,3);ushort destination=traveler.Entity.MapID;SpookTick();
        Check(!Fenced(traveler) && traveler.Entity.MapID==destination && destination==nextLeader.Entity.MapID && SpookRuns(nextLeader,"state='active'")==1,
            "cleanup of a prior completed instance cannot eject its former teammate from a newer hunt");ClearSpooks();
        var late=SpookPlayer(market);SpookBegin(late,1);SpookKill(late,SpookBoss(late));SpookReturn(late);spookNow=spookNow.AddDays(1);SpookNpc(late,11);SpookNpc(late,1);
        Check(!Fenced(late) && SpookDays(late)==1 && SpookDays(late,"state='claimed'")==1,"overnight victory claim consumes the claim day's allowance");spookNow=DateTime.UtcNow;
    }

}
