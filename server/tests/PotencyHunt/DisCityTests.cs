using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type Dis=typeof(Entity).Assembly.GetType("Revora.Game.Features.DisCityQuest");
    static readonly Type DisWorld=typeof(Entity).Assembly.GetType("Revora.Game.Features.DisCityWorld");
    static DateTime disNow;
    static long DisValue(GameState p,string expression){return Scalar("SELECT "+expression+" FROM revora_dis_runs WHERE uid=@uid",p.Entity.UID);}
    static long DisClaims(GameState p){return Scalar("SELECT count(*) FROM revora_dis_stage_claims WHERE uid=@uid",p.Entity.UID);}
    static bool DisReady(GameState p){using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){var run=Call(Dis,null,"Read",c,t,p.Entity.UID);return (bool)Call(Dis,null,"ObjectiveReady",run);}}}
    static void DisNpc(GameState p,uint id,byte option){LabNear(p,id);Npcs.GetDialog(new NpcRequest(true){NpcID=id,OptionID=option},p);}
    static void DisTick(){disNow=disNow.AddSeconds(3);Call(Dis,null,"Tick");}
    static Entity DisMonster(GameState p){DisTick();return p.Map.Entities.Values.First(i=>i.EntityFlag==EntityFlag.Monster && !i.Dead);}
    static void DisApproach(GameState p,Entity mob) {
        const uint target=900009999;p.Map.Npcs[target]=new NpcSpawn{UID=target,MapID=p.Entity.MapID,X=mob.X,Y=mob.Y};
        try{LabNear(p,target);}finally{p.Map.Npcs.Remove(target);}
    }
    static void DisKill(GameState p,Entity mob=null){mob=mob??DisMonster(p);DisApproach(p,mob);mob.Die(p.Entity);if(Fenced(p))throw new Exception("Dis City kill fenced the fixture.");}
    static void DisHunt(GameState p,int target){while(DisValue(p,"progress")<target)DisKill(p);}
    static GameState DisPlayer(Map town) {var p=LoadoutPlayer(town);p.Quests=new Quests(p);p.Quests.Load();p.Entity.Class=15;p.Entity.X=330;p.Entity.Y=253;return p;}
    static void DisRemove(GameState p,ConquerItem item){Query("DELETE FROM items WHERE uid=@uid","uid",(long)item.UID);p.Inventory.Remove(item.UID,Enums.ItemUse.None,false);}
    static void VerifyDisUnarmedExclusion(Map town) {
        var p=DisPlayer(town);DisNpc(p,3215,1);RebirthGear(p,4);var mob=DisMonster(p);DisKill(p,mob);
        p.Entity.MyAchievement.Send();Check(!Fenced(p) && mob.Dead && !HasAchievement(p,10525),"armed Dis City kill advances the hunt without unarmed credit");
        Call(Dis,null,"OnLogin",p);
    }
    static void VerifyDisCity(string runtime,Map town) {
        Query("ALTER TABLE entities ADD COLUMN class integer DEFAULT 15,ADD COLUMN level integer DEFAULT 140,ADD COLUMN my_title integer DEFAULT 0,ADD COLUMN mapid integer DEFAULT 1002,ADD COLUMN x integer DEFAULT 0,ADD COLUMN y integer DEFAULT 0");
        Query("CREATE TABLE titles (LIKE public.titles INCLUDING ALL);CREATE TABLE quests (LIKE public.quests INCLUDING ALL);CREATE TABLE itemadding (LIKE public.itemadding INCLUDING ALL)");
        Query("CREATE TABLE npcs (LIKE public.npcs INCLUDING ALL);CREATE TABLE sobnpcs (LIKE public.sobnpcs INCLUDING ALL);CREATE TABLE furniture (LIKE public.furniture INCLUDING ALL)");
        StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);ConquerItem.ItemUID=new Counter(8800000);Server.GamePool=new GameState[0];
        DMaps.MapPaths[1002]="maps\\1002.map";
        for(ushort id=2021;id<=2024;id++){DMaps.MapPaths[id]="maps\\"+id+".map";MapsTable.MapInformations[id]=new MapsTable.MapInformation{ID=id,BaseID=1500,Status=123};}
        DMaps.MapPaths[3954]="maps\\3954.map";
        disNow=new DateTime(2026,9,14,20,0,0,DateTimeKind.Utc);Dis.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>disNow));Dis.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(n=>0));
        Call(Dis,null,"Initialize");Call(DisWorld,null,"PlaceSaint");Revora.Game.Features.QuestListSystem.EnsureCatalog();
        Relics.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(n=>0));
        var counter=new Counter(1000);var ids=new uint[20000];Parallel.For(0,ids.Length,n=>ids[n]=counter.Next);Check(ids.Distinct().Count()==ids.Length && counter.Now==21000,"concurrent item/map counters allocate distinct identifiers");
        var wrap=new Counter(7){Finish=10};Check(wrap.Next==8 && wrap.Next==9 && wrap.Next==7,"counter wrap preserves the established start/finish convention");
        foreach(var row in new[]{new[]{15,400},new[]{45,600},new[]{135,300},new[]{145,300},new[]{165,400}})Check((int)Call(Dis,null,"Quota",(byte)row[0])==row[1],"Dis City class quota "+row[0]);
        var p=DisPlayer(town);p.Fake=true;DisNpc(p,3215,1);p.Fake=false;p.Entity.Level=109;DisNpc(p,3215,1);p.Entity.Level=140;
        Check(Scalar("SELECT count(*) FROM revora_dis_runs WHERE uid=@uid",p.Entity.UID)==0,"fake and under-level entrants cannot start Dis City");
        var forged=new MsgTaskStatus(true,1){Action=MsgTaskStatus.QuestAction.Begin};forged.Apend(MsgTaskStatus.QuestData.Create((QuestID)6475,MsgTaskStatus.QuestData.QuestStatus.Finished,0));MsgTaskStatus.Process(forged.ToArray(),p);
        Check(!HasAchievement(p,11005) && DisClaims(p)==0,"native quest begin cannot claim a Dis City stage achievement");
        DisNpc(p,3215,1);Check(!Fenced(p) && p.Account.State==AccountTable.AccountState.Player && p.Map.BaseID==2021 && p.Inventory.Objects.Count(i=>i.ID==3006981)==1,"ordinary admission creates an owned Hell Gate and its bound mirror");
        VerifyDisRunRules(p,town);
        Check(MapsTable.MapInformations[2021].BaseID==1500 && MapsTable.MapInformations[p.Entity.MapID].BaseID==2021 && MapsTable.MapInformations[p.Entity.MapID].Status==2,"private dungeon mapdoc/status do not overwrite shared house metadata");
        ushort firstMap=p.Entity.MapID;var foreign=DisPlayer(town);foreign.Entity.Teleport(2021,firstMap,p.Entity.X,p.Entity.Y);
        Check(foreign.Entity.MapID==1002,"another player cannot teleport into an owned Dis City instance");
        var first=DisMonster(p);long oldProgress=DisValue(p,"progress");first.Die(foreign.Entity);Check(!first.Dead && DisValue(p,"progress")==oldProgress,"foreign killer cannot consume or advance an owned encounter");
        Check(first.Level==120 && first.MonsterInfo.Level==120,"Dis City displayed and authoritative monster levels agree");
        foreach(string table in new[]{"revora_dis_runs","revora_dis_steps"}) {
            DisApproach(p,first);int savedItems=p.Inventory.Count;
            Query("CREATE FUNCTION suppress_dis_receipt() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_dis_receipt BEFORE INSERT ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_dis_receipt()");
            try{first.Die(p.Entity);}finally{Query("DROP TRIGGER suppress_dis_receipt ON "+table+";DROP FUNCTION suppress_dis_receipt()");}
            Check(Fenced(p) && !first.Dead && DisValue(p,"progress")==oldProgress && p.Inventory.Count==savedItems
                && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10525",p.Entity.UID)==0,"suppressed Dis receipt cannot acknowledge a kill or unarmed achievement: "+table);
            DemonReset(p);
        }
        DisApproach(p,first);Query("CREATE FUNCTION suppress_dis_unarmed() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_dis_unarmed BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW WHEN(NEW.achievement_id=10525) EXECUTE FUNCTION suppress_dis_unarmed()");
        int beforeItems=p.Inventory.Count;long beforeSteps=Scalar("SELECT count(*) FROM revora_dis_steps WHERE uid=@uid",p.Entity.UID);
        try{first.Die(p.Entity);}finally{Query("DROP TRIGGER suppress_dis_unarmed ON revora_achievement_evidence;DROP FUNCTION suppress_dis_unarmed()");}
        Check(Fenced(p) && !first.Dead && DisValue(p,"progress")==oldProgress && p.Inventory.Count==beforeItems
            && Scalar("SELECT count(*) FROM revora_dis_steps WHERE uid=@uid",p.Entity.UID)==beforeSteps
            && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10525",p.Entity.UID)==0,"suppressed Dis unarmed evidence rolls back death, progress, reward and step receipt");
        DemonReset(p);uint lethal=first.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,first,new Attack(true){AttackType=Attack.Melee,Attacked=first.UID,Attacker=p.Entity.UID},ref lethal,null);
        Check(!Fenced(p) && first.Dead && HasAchievement(p,10525),"Dis City actual lethal attack retries and projects unarmed credit");
        first.Die(p.Entity);Check(DisValue(p,"progress")==1,"an owned monster death credits at most one Soul Stone");
        Check(!DisReady(p),"the first Soul Stone does not check the stage-complete tracker intention");
        p.Entity.MyAchievement=new Achievement(p.Entity);p.Entity.MyAchievement.Send();
        Check(HasAchievement(p,10525) && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10525",p.Entity.UID)==1,"fresh achievement projection reloads one durable Dis unarmed receipt after corpse replay");
        uint cps=p.Entity.ConquerPoints;int ground=p.Map.FloorItems.Count;first.MonsterInfo.Drop(p.Entity);
        Check(p.Entity.ConquerPoints==cps && p.Map.FloorItems.Count==ground,"retired owned encounter objects cannot enter generic loot or CP reward code");
        var mirror=p.Inventory.Objects.Single(i=>i.ID==3006981);ItemHandler.UseItem(mirror,p);Check(Kernel.GetDistance(p.Entity.X,p.Entity.Y,p.Map.Npcs[3215].X,p.Map.Npcs[3215].Y)<=6,"owned Mirror of Hell returns to the actual stage guide");
        var filler=new List<ConquerItem>();while(p.Inventory.Count<40)filler.Add(Pack(p,1088001,1));DisHunt(p,5);
        Check(DisValue(p,"progress")==5 && p.Inventory.Objects.Count(i=>i.ID==723085)==1,"full bags retain earned Soul Stones as uncollected progress");
        Check(!DisReady(p),"earned but uncollected stones do not mark the submission ready");
        DisNpc(p,3215,1);Check(DisValue(p,"stage")==1 && DisClaims(p)==0,"uncollected or unsubmitted stones cannot open Hell Hall");
        foreach(var f in filler.Take(4))DisRemove(p,f);DisNpc(p,3215,2);
        Check(p.Inventory.Objects.Count(i=>i.ID==723085)==5,"the guide delivers all earned Soul Stones after bag space is freed");
        Check(DisReady(p),"collecting all five issued stones marks the gate objective ready");
        Query("ALTER TABLE revora_dis_stage_claims ADD CONSTRAINT dis_claim_failure CHECK(false) NOT VALID");DisNpc(p,3215,1);
        Check(Fenced(p) && DisValue(p,"stage")==1 && p.Inventory.Objects.Count(i=>i.ID==723085)==5 && !HasAchievement(p,11002),"stage receipt failure rolls back stone consumption and achievement");
        Query("ALTER TABLE revora_dis_stage_claims DROP CONSTRAINT dis_claim_failure");DemonReset(p);DisNpc(p,3215,1);
        Check(!Fenced(p) && DisValue(p,"stage")==2 && p.Map.BaseID==2022 && !Kernel.Maps.ContainsKey(firstMap) && HasAchievement(p,11002) && !HasAchievement(p,11003),"Hell Gate completion rewards only its own achievement and releases its instance");
        foreach(var f in filler.Skip(4))DisRemove(p,f);p.Entity.Class=135;
        Check(DisValue(p,"quota")==400,"Hall quota remains frozen after a class change");
        DisHunt(p,3);Call(Dis,null,"OnDeath",p);DisTick();Check(p.Entity.MapID==1002 && DisValue(p,"progress")==3,"death cleanup returns home and retains earned stage progress");
        Check(!DisReady(p),"partial Hall kills do not check the completed quota intention");
        disNow=disNow.AddDays(1);DisNpc(p,3215,1);Check(p.Map.BaseID==2022 && DisValue(p,"progress")==3,"an admitted run can resume on a closed admission day");
        DisHunt(p,400);Check(DisReady(p),"the frozen Hall quota marks the tracker objective ready");DisNpc(p,3215,1);Check(p.Map.BaseID==2023 && HasAchievement(p,11003) && !HasAchievement(p,11004),"genuine Hall kills open the Cloister with the Hall achievement only");
        for(int crystal=0;crystal<9;crystal++) {
            uint next=(uint)(900000080+DisValue(p,"crystal_order[progress+1]"));uint wrong=900000080+(next-900000080+1)%9;
            DisNpc(p,wrong,1);Check(DisValue(p,"progress")==crystal,"out-of-order crystal cannot advance seal "+crystal);
            DisNpc(p,next,1);Check(DisValue(p,"progress")==crystal,"living guardian prevents crystal activation "+crystal);
            DisKill(p);DisNpc(p,next,1);Check(DisValue(p,"progress")==crystal+1,"owned guardian death unlocks the next genuine crystal "+crystal);
            Check(DisReady(p)==(crystal==8),"only the ninth sealed crystal checks the Cloister intention "+crystal);
        }
        DisNpc(p,3215,1);Check(p.Map.BaseID==2024 && HasAchievement(p,11004) && !HasAchievement(p,11005),"all nine ordered guarded crystals open Battle Formation");
        var fakeToken=Pack(p,3006984,1);ItemHandler.UseItem(fakeToken,p);Check(DisValue(p,"token")==0,"an unrelated Pluto Token cannot summon the quest boss");DisRemove(p,fakeToken);
        DisHunt(p,4);var scraps=p.Inventory.Objects.Where(i=>i.ID==3006983).ToArray();ItemHandler.UseItem(scraps[0],p);
        Check(!DisReady(p),"four scraps and a combined token cannot mark Pluto defeated");
        var token=p.Inventory.Objects.Single(i=>i.ID==3006984);Check(scraps.All(s=>!p.Inventory.ContainsUID(s.UID)) && DisValue(p,"token")==token.UID,"four issued scraps combine atomically into the owned summon token");
        Parallel.For(0,12,n=>ItemHandler.UseItem(token,p));var boss=p.Map.Entities.Values.Single(e=>e.EntityFlag==EntityFlag.Monster);
        Check(!p.Inventory.ContainsUID(token.UID) && boss.Name=="UltimatePluto" && boss.Hitpoints==5000000 && boss.MaxHitpoints==5000000 && boss.MonsterInfo.Boss,"the issued token summons one full-health boss using the boss HP projection");
        DisApproach(p,boss);Query("ALTER TABLE revora_dis_steps ADD CONSTRAINT dis_kill_failure CHECK(action<>'defeat') NOT VALID");boss.Die(p.Entity);
        Check(Fenced(p) && !boss.Dead && !HasAchievement(p,11005),"failed final death record preserves the unacknowledged boss encounter");
        Query("ALTER TABLE revora_dis_steps DROP CONSTRAINT dis_kill_failure");DemonReset(p);
        Query("ALTER TABLE revora_dis_finishes ADD CONSTRAINT fail_finish CHECK(false) NOT VALID");boss.Die(p.Entity);
        Check(Fenced(p) && !boss.Dead && Scalar("SELECT count(*) FROM revora_dis_finishes WHERE uid=@uid",p.Entity.UID)==0,
            "failed ranking finish rolls back owned Pluto defeat and preserves the living retryable boss");
        Query("ALTER TABLE revora_dis_finishes DROP CONSTRAINT fail_finish");DemonReset(p);p.Attackable=true;uint damage=boss.Hitpoints;
        Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,boss,new Attack(true){AttackType=Attack.Melee,Attacked=boss.UID,Attacker=p.Entity.UID},ref damage,null);
        Check(boss.Dead && !Fenced(p),"production attack receipt invokes the durable summoned-boss death path");
        Check(DisReady(p),"committed Pluto defeat checks the final tracker intention before reward claim");
        Check(!HasAchievement(p,11005) && DisClaims(p)==3,"boss defeat creates durable victory before final reward claim");
        long frozenFinish=Scalar("SELECT elapsed_ticks FROM revora_dis_finishes WHERE uid=@uid",p.Entity.UID);disNow=disNow.AddMinutes(2);
        Relics.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(n=>99));
        var finalFill=new List<ConquerItem>();while(p.Inventory.Count<40)finalFill.Add(Pack(p,1088001,1));DisNpc(p,3215,1);
        Check(DisClaims(p)==3 && !HasAchievement(p,11005),"full bag preserves the final victory reward");
        Check(DisValue(p,"star_sword")==1,"deferred victory claim preserves its saved Star Sword result instead of drawing again");
        foreach(var f in finalFill.Take(5))DisRemove(p,f);
        Query("ALTER TABLE revora_dis_keys ADD CONSTRAINT fail_key_issue CHECK(false) NOT VALID");DisNpc(p,3215,1);
        Check(Fenced(p) && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id BETWEEN 800000 AND 899999",p.Entity.UID)==0 && !HasAchievement(p,11005) && p.Inventory.ContainsUID(mirror.UID) && Scalar("SELECT count(*) FROM revora_dis_completions WHERE uid=@uid",p.Entity.UID)==0 && Scalar("SELECT count(*) FROM revora_dis_keys WHERE uid=@uid",p.Entity.UID)==0,"failed key issuance rolls back the whole final reward, mirror consumption and weekly completion");
        Query("ALTER TABLE revora_dis_keys DROP CONSTRAINT fail_key_issue");DemonReset(p);
        LabNear(p,3215);Parallel.For(0,12,n=>Npcs.GetDialog(new NpcRequest(true){NpcID=3215,OptionID=1},p));
        Check(p.Entity.MapID==1002 && HasAchievement(p,11005) && DisClaims(p)==4 && p.Inventory.Objects.Count(i=>i.ID==3008059)==1,"final claim grants Yin Jade, Sacred pack and the Battle Formation achievement once");
        Check(Scalar("SELECT elapsed_ticks FROM revora_dis_finishes WHERE uid=@uid",p.Entity.UID)==frozenFinish,
            "delayed final reward collection cannot increase the already frozen battle finish time");
        long count=Scalar("SELECT count(*) FROM revora_dis_completions WHERE uid=@uid",p.Entity.UID);DisNpc(p,3215,1);Call(Dis,null,"OnLogin",p);DisNpc(p,3215,1);
        Check(p.Entity.MapID==1002 && count==1 && DisClaims(p)==4,"weekly victory and reconnect prevent another reward claim");
        DisNpc(p,3215,7);DisNpc(p,3215,12);
        Check(p.Entity.MapID==1002 && Scalar("SELECT count(*) FROM revora_dis_run_rules WHERE uid=@uid",p.Entity.UID)==1,
            "a different difficulty cannot bypass the shared weekly victory limit");
        VerifyIslandSoul(p,"revora_dis_stage_claims");
        Check(!p.Inventory.Objects.Any(i=>i.ID==3006981),"completed run consumes its remaining owned mirror");
        var earnedKey=p.Inventory.Objects.Single(i=>i.ID==3006982);
        Check(earnedKey.Bound && Scalar("SELECT count(*) FROM revora_dis_keys WHERE uid=@uid AND used_at IS NULL",p.Entity.UID)==1,"weekly final claim issues one bound treasury key with its durable victory receipt");
        Check(p.Inventory.Objects.Count(i=>i.ID==790001)==1 && Scalar("SELECT count(*) FROM revora_dis_relics WHERE uid=@uid AND item_type=790001 AND used_at IS NULL",p.Entity.UID)==1,"weekly Pluto victory issues one owned Dark Horn with the final reward");
        VerifyDisRelics(p,foreign);
        VerifyDisTreasury(p,foreign,earnedKey,town);
        DisNpc(foreign,3215,1);Check(foreign.Entity.MapID==1002,"new admission stays closed on Tuesday while existing runs may resume");
        disNow=new DateTime(2026,9,21,20,0,0,DateTimeKind.Utc);foreach(var f in finalFill.Skip(4))DisRemove(p,f);DisNpc(p,3215,1);
        Check(p.Map.BaseID==2021 && DisValue(p,"progress")==0,"next Pacific week permits a fresh four-stage run");
        Query("DELETE FROM revora_dis_run_rules WHERE uid=@uid AND run IN(SELECT run FROM revora_dis_completions WHERE uid=@uid)","uid",(long)p.Entity.UID);
        Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.DisCityRunRules"),null,"Initialize");
        Check(Scalar("SELECT count(*) FROM revora_dis_run_rules WHERE uid=@uid AND difficulty=0 AND revision=1",p.Entity.UID)==2,
            "legacy historical completion and next-week active run retain separate standard combat identities");
        Check(!DisReady(p) && !p.Quests.HasQuest((QuestID)6473) && !p.Quests.HasQuest((QuestID)6474) && !p.Quests.HasQuest((QuestID)6475),"new weekly run clears later-stage tracker completions without revoking achievements");
        DisKill(p);ushort recovery=p.Entity.MapID;Call(Dis,null,"OnLogin",p);
        Check(p.Entity.MapID==1002 && !Kernel.Maps.ContainsKey(recovery) && DisValue(p,"progress")==1,"login releases an old private instance while retaining durable progress");
        DisNpc(p,3215,1);Check(p.Map.BaseID==2021 && DisValue(p,"progress")==1 && p.Entity.MapID!=recovery,"resume rebuilds a private field from its persisted run");
        var last=p.Map.Entities.Values.First(e=>e.EntityFlag==EntityFlag.Monster);DisApproach(p,last);disNow=disNow.AddHours(2);last.Die(p.Entity);
        Check(!last.Dead && DisValue(p,"progress")==1,"an expired instance cannot award a late kill");DisTick();
        Check(p.Entity.MapID==1002 && DisValue(p,"progress")==1,"two-hour instance cleanup preserves the resumable objective");
        DisNpc(p,3215,1);ushort oldMap=p.Entity.MapID;var instance=((System.Collections.IDictionary)DisWorld.GetField("Players",Hidden).GetValue(null))[p.Entity.UID];Call(DisWorld,null,"Release",instance);
        Call(Dis,null,"OnLogin",p);Check(!Fenced(p) && p.Entity.MapID==1002 && !Kernel.Maps.ContainsKey(oldMap),"persisted last-map ownership recovers a player after runtime instance state is lost");
        var capped=new List<GameState>();for(int n=0;n<16;n++){var entrant=DisPlayer(town);DisNpc(entrant,3215,1);capped.Add(entrant);}
        Check(capped.All(a=>a.Map.BaseID==2021) && capped.Select(a=>a.Entity.MapID).Distinct().Count()==16,"sixteen ordinary entrants receive distinct bounded private maps");
        var overflow=DisPlayer(town);DisNpc(overflow,3215,1);Check(!Fenced(overflow) && overflow.Entity.MapID==1002 && overflow.Inventory.Count==0,"capacity rejection leaves the seventeenth entrant uncharged with no orphan mirror");
        foreach(var entrant in capped)Call(Dis,null,"OnLogin",entrant);
        Check(((System.Collections.IDictionary)DisWorld.GetField("Players",Hidden).GetValue(null)).Count==0,"private instance cleanup releases all capped admissions");
        Check(skyJumps>0,"all stage guides and ordered crystals were reached through production player jumps");
        VerifyDisUnarmedExclusion(town);
        VerifyDisRanking(town,p);
        // Simulate the migration's false default on an admitted pre-upgrade run,
        // then earn every stage instead of fabricating a final victory receipt.
        disNow=new DateTime(2026,12,7,20,0,0,DateTimeKind.Utc);
        var legacy=DisPlayer(town);DisNpc(legacy,3215,1);
        Query("UPDATE revora_dis_runs SET valuable_reward=false WHERE uid=@uid","uid",(long)legacy.Entity.UID);
        Call(Dis,null,"OnLogin",legacy);DisNpc(legacy,3215,1);
        DisHunt(legacy,5);DisNpc(legacy,3215,1);DisHunt(legacy,400);DisNpc(legacy,3215,1);
        for(int crystal=0;crystal<9;crystal++) {
            uint next=(uint)(900000080+DisValue(legacy,"crystal_order[progress+1]"));DisKill(legacy);DisNpc(legacy,next,1);
        }
        DisNpc(legacy,3215,1);DisHunt(legacy,4);
        ItemHandler.UseItem(legacy.Inventory.Objects.First(i=>i.ID==3006983),legacy);
        ItemHandler.UseItem(legacy.Inventory.Objects.Single(i=>i.ID==3006984),legacy);DisKill(legacy);DisNpc(legacy,3215,1);
        Check(!Fenced(legacy) && DisClaims(legacy)==4 && HasAchievement(legacy,11005)
            && !legacy.Inventory.Objects.Any(i=>i.ID>=800000 && i.ID<900000)
            && legacy.Inventory.Contains(3008059,1) && DisValue(legacy,"CASE WHEN valuable_reward THEN 1 ELSE 0 END")==0,
            "old saved expedition retains original final rewards through earned stages and reconnect");
        foreach(var item in legacy.Inventory.Objects.ToArray())legacy.Inventory.Remove(item.UID,Enums.ItemUse.None,false);
        ConquerItemTable.LoadItems(legacy);Call(Dis,null,"OnLogin",legacy);DisNpc(legacy,3215,1);
        Check(DisClaims(legacy)==4 && legacy.Inventory.Contains(3008059,1)
            && !legacy.Inventory.Objects.Any(i=>i.ID>=800000 && i.ID<900000),"old final reward survives item reload and cannot replay into a new soul");
    }
}
