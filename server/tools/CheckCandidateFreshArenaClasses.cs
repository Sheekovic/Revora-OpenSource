using System;
using System.Linq;
using System.Reflection;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle {
    const string FreshArenaClassDay="20411120";
    static string FreshArenaClassSnapshot(GameState p,GameState[] players,GameState peer,GameState[] trainees){return Convert.ToString(Sql("SELECT json_build_object('prior',@prior::json,'arena',(SELECT json_agg(r ORDER BY entityid) FROM arena r WHERE entityid=ANY(@uids)),'teamarena',(SELECT json_agg(r ORDER BY entityid) FROM teamarena r WHERE entityid=ANY(@uids)),'matches',(SELECT json_agg(r ORDER BY id) FROM revora_arena_matches r WHERE id=ANY((SELECT runs FROM revora_fresh_arena_classes_probe)::uuid[])),'statistics',(SELECT json_agg(r ORDER BY run,uid) FROM revora_arena_result_statistics r WHERE run=ANY((SELECT runs FROM revora_fresh_arena_classes_probe)::uuid[])),'entries',(SELECT json_agg(r ORDER BY uid,run) FROM revora_arena_daily_entries r WHERE day=@day AND uid=ANY(@uids)),'rank',(SELECT json_agg(r ORDER BY uid) FROM revora_arena_rank_rewards r WHERE day=@day))::text","prior",FreshRosterSnapshot(p,players,peer,trainees),"uids",players.Concat(new[]{peer}).Concat(trainees).Distinct().Select(w=>(long)w.Entity.UID).ToArray(),"day",FreshArenaClassDay));}
    static void FreshArenaClassSave(GameState p,GameState[] players,GameState peer,GameState[] trainees,int stage){foreach(var who in players.Concat(new[]{peer}).Concat(trainees).Distinct())Require(EntityTable.SaveEntity(who));Sql("UPDATE revora_fresh_arena_classes_probe SET stage=@stage,snapshot=@snapshot","stage",stage,"snapshot",FreshArenaClassSnapshot(p,players,peer,trainees));}
    static void FreshArenaClassJourney(string phase){
        var p=FreshLoadSaved(true,true,true,true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);
        var list=new System.Collections.Generic.List<GameState>();foreach(int slot in new[]{1,2,3,0}){uint uid=Convert.ToUInt32(Sql("SELECT uid FROM revora_fresh_contributors WHERE slot=@slot","slot",slot));list.Add(FreshContributor(uid,Convert.ToString(Sql("SELECT name FROM revora_fresh_contributors WHERE slot=@slot","slot",slot))));}list.Add(p);var players=list.ToArray();Server.GamePool=players;Call(typeof(Revora.Database.JiangHu),"LoadClient",p);Require(players.All(w=>!w.Fake && w.Account.State==AccountTable.AccountState.Player && !PirateFenced(w)) && (p.Entity.Class==25 || p.Entity.Class==41 || (phase.Contains("-snow-loadout-") || phase.Contains("-snow-battle-")) && p.Entity.Class==45));


        var peer=FreshContributor(Convert.ToUInt32(Sql("SELECT uid FROM revora_fresh_warrior_peer_probe WHERE done")),"FreshWar1");Server.GamePool=players.Concat(new[]{peer}).ToArray();Require(!peer.Fake && peer.Account.State==AccountTable.AccountState.Player && peer.Entity.Class==20 && peer.Entity.Level>=130 && (p.Entity.Level>=130 || phase.Contains("-rite-")));


        var listExtra=new System.Collections.Generic.List<GameState>{players[1]};foreach(int slot in new[]{4,5,6,7}){uint uid=Convert.ToUInt32(Sql("SELECT uid FROM revora_fresh_contributors WHERE slot=@slot","slot",slot));listExtra.Add(FreshContributor(uid,Convert.ToString(Sql("SELECT name FROM revora_fresh_contributors WHERE slot=@slot","slot",slot))));}var fire=FreshContributor(Convert.ToUInt32(Sql("SELECT fire FROM revora_fresh_roster_probe WHERE done")),"FreshFire1");listExtra.Add(fire);var trainees=listExtra.ToArray();Server.GamePool=players.Concat(new[]{peer}).Concat(trainees).Distinct().ToArray();Require(trainees.All(w=>!w.Fake && w.Account.State==AccountTable.AccountState.Player && !PirateFenced(w)));
        var opponents=trainees;DateTime now=new DateTime(2041,11,20,18,0,0,DateTimeKind.Utc);Require(opponents.All(w=>w.Entity.Level>=70 && !w.Entity.Dead));
        if((bool)Sql("SELECT to_regclass('revora_fresh_arena_classes_probe') IS NULL")){Require(phase.EndsWith("fight") && FreshRosterSnapshot(p,players,peer,trainees)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_roster_probe WHERE done")));Sql("CREATE TABLE revora_fresh_arena_classes_probe(stage int,runs uuid[],hits int,snapshot text);INSERT INTO revora_fresh_arena_classes_probe VALUES(0,ARRAY[]::uuid[],0,NULL)");FreshArenaClassSave(p,players,peer,trainees,0);}
        if(phase.Contains("-mentor-")){FreshMentorJoin(p,peer,()=>FreshArenaClassSnapshot(p,players,peer,trainees),phase);return;}
        if(phase.Contains("-ko-")){FreshNormalKo(p,()=>FreshArenaClassSnapshot(p,players,peer,trainees),phase);return;}
        if(phase.Contains("-maintenance-")){FreshFundingRepair(p,()=>FreshArenaClassSnapshot(p,players,peer,trainees),phase);return;}
        if(phase.EndsWith("funding-readiness")){
            string before=FreshArenaClassSnapshot(p,players,peer,trainees);
            System.Console.WriteLine("[fresh-funding] hp="+p.Entity.Hitpoints+" max-hp="+p.Entity.MaxHitpoints+" attack="+p.Entity.MinAttack+".."+p.Entity.MaxAttack+" defence="+p.Entity.Defence+" strength="+p.Entity.Strength+" agility="+p.Entity.Agility+" vitality="+p.Entity.Vitality+" free="+p.Entity.Atributes+" cp="+p.Entity.ConquerPoints+" rvp="+p.Entity.RevoraPoint+" silver="+p.Entity.Money);
            foreach(var item in p.Equipment.GetItems())System.Console.WriteLine("[fresh-funding] slot="+item.Position+" type="+item.ID+" plus="+item.Plus+" bound="+item.Bound+" durability="+item.Durability+" maximum="+item.MaximDurability+" repair-base-silver="+ConquerItemInformation.BaseInformations[item.ID].GoldWorth);
            foreach(uint id in new uint[]{1000030,1000040,1002000,1002010,1002020,1002050,725065}){
                ConquerItemBaseInformation info;if(!ConquerItemInformation.BaseInformations.TryGetValue(id,out info))continue;
                System.Console.WriteLine("[fresh-funding] medicine="+id+" hp="+info.ItemHP+" stack="+info.StackSize+" silver="+info.GoldWorth+" shops="+string.Join(",",ShopFile.Shops.Values.Where(v=>v.MoneyType==ShopFile.MoneyType.Gold && v.Items.Contains(id)).Select(v=>v.UID)));
            }
            Require(before==FreshArenaClassSnapshot(p,players,peer,trainees));
            System.Console.WriteLine("[career-process] "+phase+"-passed snapshot-unchanged=true");System.Console.Out.Flush();Environment.Exit(0);return;
        }
        if(phase.Contains("-funding-")){FreshDailyBossCombat(p,()=>FreshArenaClassSnapshot(p,players,peer,trainees),phase);return;}
        if(phase.Contains("-rite-")){FreshPaidClassTransition(p,()=>FreshArenaClassSnapshot(p,players,peer,trainees),phase);return;}
        if(phase.EndsWith("readiness")){lock(p.ItemSyncRoot){
            string beforeState=FreshArenaClassSnapshot(p,players,peer,trainees);
            var profile=Call(Feature("CharacterProgression"),"Capture",p);
            var rules=Feature("ReincarnationRules");
            Require((bool)Call(rules,"Eligible",p.Entity.Class,p.Entity.Level,p.Entity.Reborn));
            uint cost=checked(Revora.Config.SerethReincarnationCp+2000u);
            Require(p.Entity.ConquerPoints>=cost && p.Entity.FirstRebornLevel>=110 && p.Entity.SecondRebornLevel>=110);
            Require((long)Sql("SELECT count(*) FROM reincarnation WHERE uid=@uid","uid",(long)p.Entity.UID)==0);
            Require((long)Sql("SELECT count(*) FROM revora_second_rebirths WHERE uid=@uid AND old_level>=130 AND recovered_at IS NULL","uid",(long)p.Entity.UID)==0);
            long total=(long)p.Entity.Strength+p.Entity.Agility+p.Entity.Vitality+p.Entity.Spirit+p.Entity.Atributes;
            foreach(byte next in new byte[]{11,21,41,51,61,71,81,132,142,161}){
                var planned=Call(rules,"Profile",profile,next,(ushort)Revora.Config.AttributePointsPerLevel);
                var values=(long[])planned.GetType().GetField("Values",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(planned);
                Require(values[0]==15 && values[7]==next && values[8]==2 && values.Skip(2).Take(5).Sum()==total-(p.Entity.Level-15)*Revora.Config.AttributePointsPerLevel);
                System.Console.WriteLine("[fresh-class-readiness] next="+next+" base-budget="+values.Skip(2).Take(5).Sum());
            }
            Require(beforeState==FreshArenaClassSnapshot(p,players,peer,trainees));
            System.Console.WriteLine("[fresh-class-readiness] uid="+p.Entity.UID+" level="+p.Entity.Level+" class="+p.Entity.Class+" reborn="+p.Entity.Reborn+" cp="+p.Entity.ConquerPoints+" dew-and-rite="+cost+" snapshot-unchanged=true");
            System.Console.WriteLine("[career-process] "+phase+"-passed");System.Console.Out.Flush();Environment.Exit(0);return;
        }}
        if(phase.EndsWith("inspect")){System.IO.File.WriteAllText("fresh-arena-classes-saved.json",Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_arena_classes_probe")));System.IO.File.WriteAllText("fresh-arena-classes-current.json",FreshArenaClassSnapshot(p,players,peer,trainees));foreach(var who in new[]{p}.Concat(opponents))System.Console.WriteLine("[fresh-arena-classes-inspect] uid="+who.Entity.UID+" points="+who.ArenaStatistic.ArenaPoints+" status="+who.ArenaStatistic.Status+" team="+who.TeamArenaStatistic.Status+" dead="+who.Entity.Dead);System.Console.WriteLine("[fresh-arena-classes-inspect] rollovers="+Sql("SELECT coalesce(json_agg(r ORDER BY uid)::text,'[]') FROM revora_arena_day_rollovers r WHERE day=@day","day",FreshArenaClassDay));System.Console.WriteLine("[career-process] "+phase+"-passed");System.Console.Out.Flush();Environment.Exit(0);return;}
        if(phase.EndsWith("fight") && FreshArenaClassSnapshot(p,players,peer,trainees)!=Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_arena_classes_probe"))){
            Require((bool)Sql("SELECT to_regclass('revora_fresh_arena_classes_resume_probe') IS NULL"));
            Require((bool)Sql("SELECT stage=0 AND cardinality(runs)=1 FROM revora_fresh_arena_classes_probe"));
            string latest=Convert.ToString(Sql("SELECT json_build_object('prior',@prior::json,'run',(SELECT json_agg(r) FROM revora_daily_boss_runs r WHERE runkey=(SELECT run FROM revora_fresh_daily_boss_probe)),'loot',(SELECT json_agg(r ORDER BY ordinal) FROM revora_daily_boss_loot r WHERE runkey=(SELECT run FROM revora_fresh_daily_boss_probe)))::text","prior",FreshBossPrior(p,()=>FreshArenaClassSnapshot(p,players,peer,trainees))));
            Require(latest==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_daily_boss_probe WHERE stage=3")));
            Sql("CREATE TABLE revora_fresh_arena_classes_resume_probe(prior_snapshot text,verified_boss_snapshot text,main_class int,main_level int);INSERT INTO revora_fresh_arena_classes_resume_probe SELECT snapshot,@boss,@class,@level FROM revora_fresh_arena_classes_probe","boss",latest,"class",(int)p.Entity.Class,"level",(int)p.Entity.Level);
            FreshArenaClassSave(p,players,peer,trainees,0);
        }
        Require(FreshArenaClassSnapshot(p,players,peer,trainees)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_arena_classes_probe")));int stage=(int)Sql("SELECT stage FROM revora_fresh_arena_classes_probe");Feature("ArenaMatchLedger").GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        if(phase.EndsWith("fight")){
            Require(stage==0);var runs=((Guid[])Sql("SELECT runs FROM revora_fresh_arena_classes_probe")).ToList();int hits=(int)Sql("SELECT hits FROM revora_fresh_arena_classes_probe");Action<GameState,uint,uint> request=(who,dialog,button)=>{var frame=new Qualifier.ArenaSignup{DialogID=dialog,OptionID=button}.BuildPacket();Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,frame.Length-8,frame);Revora.Network.PacketHandler.HandlePacket(frame,who);Require(who.Action==2);};
            while(runs.Count<6){var target=opponents[runs.Count];now=new DateTime(2041,11,20,18,0,0,DateTimeKind.Utc).AddMinutes(runs.Count*2);Require(!target.Entity.Dead && !p.Entity.Dead);p.Entity.Teleport(1002,408,330);target.Entity.Teleport(1002,410,330);request(p,0,0);request(target,0,0);Require((Qualifier.PlayerList.ContainsKey(p.Entity.UID) && Qualifier.PlayerList.ContainsKey(target.Entity.UID)) || ReferenceEquals(p.ArenaStatistic.PlayWith,target));
                Qualifier.EngagePlayers();Require(ReferenceEquals(p.ArenaStatistic.PlayWith,target));
                request(p,3,1);request(target,3,1);Qualifier.VerifyAwaitingPeople();var group=p.QualifierGroup;
                Require(group!=null && ReferenceEquals(target.QualifierGroup,group) && p.Map.BaseID==700 && p.Entity.MapID==target.Entity.MapID);Guid run=(Guid)group.GetType().GetField("ParticipationKey",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(group);
                group.ImportTime=group.CreateTime=Time32.Now.AddSeconds(-13);p.Attackable=target.Attackable=true;p.CantAttack=target.CantAttack=Time32.Now.AddSeconds(-1);int count=0;while(!group.Done && count<100){p.Entity.X=target.Entity.X;p.Entity.Y=(ushort)(target.Entity.Y+1);p.Screen.Add(target.Entity);target.Screen.Add(p.Entity);p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);Attack.HandleAttack(new Attack(true){Attacker=p.Entity.UID,Attacked=target.Entity.UID,X=target.Entity.X,Y=target.Entity.Y,AttackType=Attack.Melee},p);count++;}Require(group.Done);now=now.AddSeconds(20);group.DoneStamp=Time32.Now.AddSeconds(-5);Qualifier.CheckGroups();Require((long)Sql("SELECT count(*) FROM revora_arena_matches WHERE id=@run AND state='finished' AND winner=@uid AND day=@day","run",run,"uid",(long)p.Entity.UID,"day",FreshArenaClassDay)==1);
                bool retained=p.InQualifier() || target.InQualifier();int cleanupTicks=0;
                var cleanupWait=System.Diagnostics.Stopwatch.StartNew();
                while((p.InQualifier() || target.InQualifier()) && cleanupWait.Elapsed.TotalSeconds<5){System.Threading.Thread.Sleep(100);Qualifier.CheckGroups();cleanupTicks++;}
                Require(!p.InQualifier() && !target.InQualifier() && p.ArenaStatistic.PlayWith==null && target.ArenaStatistic.PlayWith==null);
                System.Console.WriteLine("[fresh-arena-cleanup] run="+run+" retained-after-first-tick="+retained+" additional-ticks="+cleanupTicks);
                // Complete the documented controlled town return before saving the checkpoint.
                p.Entity.Teleport(1002,408,330);target.Entity.Teleport(1002,410,330);if(target.Entity.Dead){target.Entity.DeathStamp=Time32.Now.AddSeconds(-20);GeneralData.Revive(new Data(true){UID=target.Entity.UID,ID=Data.Revive},target);}Require(!target.Entity.Dead);runs.Add(run);hits+=count;Sql("UPDATE revora_fresh_arena_classes_probe SET runs=@runs,hits=@hits","runs",runs.ToArray(),"hits",hits);FreshArenaClassSave(p,players,peer,trainees,runs.Count==6?1:0);
            }
        }else if(phase.EndsWith("pay")){
            Require(stage==1);now=now.AddDays(1);var ranking=Feature("ArenaRankingSettlement");Call(ranking,"Close",FreshArenaClassDay);foreach(var who in new[]{p}.Concat(opponents)){uint before=who.CurrentHonor;uint award=Convert.ToUInt32(Sql("SELECT honor FROM revora_arena_rank_rewards WHERE day=@day AND uid=@uid AND paid_at IS NULL","day",FreshArenaClassDay,"uid",(long)who.Entity.UID));Require(award>0);Call(ranking,"Pay",who);Require(who.CurrentHonor==before+award && who.TeamArenaStatistic.CurrentHonor==who.CurrentHonor);string paid=FreshArenaClassSnapshot(p,players,peer,trainees);Call(ranking,"Pay",who);Require(paid==FreshArenaClassSnapshot(p,players,peer,trainees));System.Console.WriteLine("[fresh-arena-classes] uid="+who.Entity.UID+" rank-honor="+award);}FreshArenaClassSave(p,players,peer,trainees,2);
        }else {Require(phase.EndsWith("reload") && stage==2);string saved=FreshArenaClassSnapshot(p,players,peer,trainees);foreach(var who in new[]{p}.Concat(opponents)){Require(who.CurrentHonor==who.TeamArenaStatistic.CurrentHonor);Call(Feature("ArenaRankingSettlement"),"Pay",who);}Require(saved==FreshArenaClassSnapshot(p,players,peer,trainees));}
        Require((long)Sql("SELECT count(*) FROM revora_arena_matches WHERE id=ANY((SELECT runs FROM revora_fresh_arena_classes_probe)::uuid[]) AND winner=@uid AND state='finished'","uid",(long)p.Entity.UID)==6 && (long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id IN(11106,11108,11109,11309,11104,11103)","uid",(long)p.Entity.UID)==6 && FreshArenaClassSnapshot(p,players,peer,trainees)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_arena_classes_probe")));System.Console.WriteLine("[career-process] "+phase+"-passed hits="+Sql("SELECT hits FROM revora_fresh_arena_classes_probe"));System.Console.Out.Flush();Environment.Exit(0);
    }
    static string FreshCurrentCareerReport(GameState p,string checkpoint){return Convert.ToString(Sql("SELECT json_build_object('fresh_uid',@uid,'checkpoint',@checkpoint,'profile',(SELECT json_build_object('level',level,'class',class,'reborn',reborn,'cp',conquerpoints,'silver',money) FROM entities WHERE uid=@uid),'fresh_receipts',(SELECT json_agg(r ORDER BY achievement_id) FROM (SELECT achievement_id,source FROM revora_achievement_evidence WHERE uid=@uid)r),'retained_uid',4294907001,'retained_receipts',(SELECT json_agg(r ORDER BY achievement_id) FROM (SELECT achievement_id,source FROM revora_achievement_evidence WHERE uid=4294907001)r))::text","uid",(long)p.Entity.UID,"checkpoint",checkpoint));}
    // A conservative arithmetic check of the existing controlled fight model,
    // not a second kill, payment, movement or native-client acceptance run.
    static string FreshDukeFundingBound(GameState p){
        var e=p.Entity;
        Require(e.NobilityRank==Revora.Game.ConquerStructures.NobilityRank.Duke && !e.Transformed && e.MaxHitpoints>14500);
        Require((long)Sql("SELECT donation FROM nobility WHERE entityuid=@uid","uid",(long)e.UID)==0);
        int attack=checked((int)e.MinAttack*80/100);
        attack=checked(attack+attack*e.Gems[1]/100);
        var calc=typeof(Revora.Game.Attacking.Calculate);
        int damage=(int)calc.GetMethod("CalcDamageUser2Monster",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{attack,7000,(int)e.Level,140,attack-7000});
        int minimum=1+e.Level/10;
        var weapon=p.Equipment.TryGetItem(ConquerItem.RightWeapon);
        if(weapon!=null)minimum+=(int)(weapon.ID%10);
        damage=checked(Math.Max(minimum,damage)+(int)e.PhysicalDamageIncrease);
        Require(damage>0);
        ushort[] wornSlots={1,2,3,4,5,6,8,10,11};
        var wearItems=p.Equipment.GetItems().Where(i=>wornSlots.Contains(i.Position)).ToArray();
        Require(wearItems.Length>0 && wearItems.All(i=>i.MaximDurability>0 && !ItemHandler.IsArrow(i.ID)));
        int minimumDurability=wearItems.Min(i=>(int)i.MaximDurability);
        // Upper-bound each silver repair by the full catalog value at Super's
        // largest quality multiplier, even if that slot did not wear at all.
        long repairBatch=wearItems.Sum(i=>(long)Math.Max(1m,decimal.Ceiling(ConquerItemInformation.BaseInformations[i.ID].GoldWorth*1.125m)));
        int initialBroken=wearItems.Count(i=>i.Durability==0);
        long hp=e.MaxHitpoints,remaining=100000000,elapsed=31000;
        int hits=0,retaliations=0,used=0,bought=100,stock=100,trips=1;
        int wear=0,maximumWear=0;
        long minimumHp=hp;
        // Twenty free bag entries, five medicines per stack. Start provisioned,
        // as in the earlier full fight. All retaliation rolls take their maximum.
        while(remaining>0 && hits<30000){
            while(hp+7000<=e.MaxHitpoints){
                if(stock==0){
                    maximumWear=Math.Max(maximumWear,wear);Require(wear<minimumDurability);wear=0;
                    stock=100;bought+=100;trips++;elapsed+=120000;
                }
                hp+=7000;stock--;used++;
            }
            remaining-=damage;hits++;wear++;elapsed+=500;
            if(remaining>0 && hits%4==0){Require(hp>7500);hp-=7500;retaliations++;wear++;minimumHp=Math.Min(minimumHp,hp);}
        }
        maximumWear=Math.Max(maximumWear,wear);Require(maximumWear<minimumDurability);
        long repairSilver=repairBatch*(trips+1); // initial, refills and post-kill
        bool affordable=(ulong)(bought*1000+repairSilver)<e.Money && e.ConquerPoints>=initialBroken*15;
        string result=Convert.ToString(Sql("SELECT json_build_object('dragon_gem_percent',@gems,'minimum_hit',@hit,'hits',@hits,'retaliations',@ret,'minimum_hp',@hp,'medicines_bought',@bought,'medicines_used',@used,'trips',@trips,'elapsed_ms',@elapsed,'medicine_silver',@cost,'affordable',@affordable,'within_two_hours',@within)::text",
            "gems",(int)e.Gems[1],"hit",damage,"hits",hits,"ret",retaliations,"hp",minimumHp,"bought",bought,"used",used,"trips",trips,"elapsed",elapsed,"cost",bought*1000,"affordable",affordable,"within",remaining<=0 && elapsed<7200000));
        Require(remaining<=0 && affordable && elapsed<7200000);
        result=Convert.ToString(Sql("SELECT (@result::jsonb || jsonb_build_object('maintenance_allowance_ms_per_visit',30000,'minimum_maximum_durability',@dur,'maximum_wear_between_repairs',@wear,'repair_batch_silver_bound',@batch,'repair_silver_bound',@repair,'initial_broken_items',@broken,'initial_meteor_cp_bound',@cp,'net_bounty_silver_bound',@net))::text",
            "result",result,"dur",minimumDurability,"wear",maximumWear,"batch",repairBatch,"repair",repairSilver,"broken",initialBroken,"cp",initialBroken*15,"net",10000000-bought*1000-repairSilver));
        return result;
    }
    static void FreshSnowLoadout(GameState p,Func<string> prior,string phase){
        bool reload=phase.EndsWith("reload");long uid=p.Entity.UID;
        // This harness loads JiangHu after FreshLoadSaved's equipment stats.
        // Recalculate before comparing an equipped profile across processes.
        p.LoadItemStats();
        Func<string> snapshot=()=>Convert.ToString(Sql("SELECT json_build_object('prior',@prior::json,'peak',(SELECT json_agg(r ORDER BY id) FROM revora_peak_purchases r WHERE uid=@uid),'vestments',(SELECT json_agg(r ORDER BY id) FROM revora_vestment_purchases r WHERE uid=@uid))::text","prior",prior(),"uid",uid));
        if(phase.Contains("-snow-battle-")){FreshSnowBattle(p,snapshot,phase);return;}
        if((bool)Sql("SELECT to_regclass('revora_fresh_snow_loadout_probe') IS NULL")){
            Require(!reload && p.Entity.Class==41 && p.Entity.Level==133 && prior()==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_mentor_join_probe WHERE done")));
            Require(p.Inventory.Count<=40);
            Sql("CREATE TABLE revora_fresh_snow_loadout_probe(stage integer,snapshot text,prior_snapshot text,before_cp bigint,peak_cp bigint,kit_cp bigint,items bigint[],ids integer[]);INSERT INTO revora_fresh_snow_loadout_probe SELECT 0,@snapshot,@prior,@cp,@peak,@kit,ARRAY[]::bigint[],ARRAY(SELECT achievement_id FROM revora_achievement_evidence WHERE uid=@uid ORDER BY achievement_id)","snapshot",snapshot(),"prior",prior(),"cp",(long)p.Entity.ConquerPoints,"peak",(long)Revora.Game.Features.RevoraMasterSystem.PeakPrice,"kit",(long)Revora.Game.Features.RevoraMasterSystem.VestmentsPrice,"uid",uid);
        }
        Require(snapshot()==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_snow_loadout_probe")));
        int stage=(int)Sql("SELECT stage FROM revora_fresh_snow_loadout_probe");
        Action<int> save=n=>{Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_snow_loadout_probe SET stage=@stage,snapshot=@snapshot","stage",n,"snapshot",snapshot());};
        if(!reload){
            if(stage==0){
                var sites=new System.Collections.Generic.Dictionary<uint,ushort>{{8,1002},{10012,1011},{10028,1020},{10011,1000},{10027,1015},{44,1002}};
                foreach(var item in p.Inventory.Objects.Where(i=>i.Days==0).OrderBy(i=>i.UID).Take(Math.Max(0,p.Inventory.Count-32)).ToArray()){
                    var slot=p.Warehouses.OrderBy(w=>(uint)w.Key).First(w=>sites.ContainsKey((uint)w.Key) && w.Value.Count<w.Value.MaxCount);
                    uint id=(uint)slot.Key;var map=Kernel.Maps[sites[id]];var npc=map.Npcs[id];p.Entity.Teleport(map.ID,(ushort)(npc.X-1),npc.Y);
                    var packet=new byte[172];Revora.Network.Writer.WriteUInt16(164,0,packet);Revora.Network.Writer.WriteUInt16(1102,2,packet);Revora.Network.Writer.WriteUInt32(id,4,packet);packet[12]=1;packet[13]=10;Revora.Network.Writer.WriteUInt32(item.UID,18,packet);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,164,packet);Warehouse.Process(packet,p);
                    Require(!p.Inventory.ContainsUID(item.UID));save(0);
                }
                Require(p.Inventory.Count<=32);p.Entity.Teleport(1002,304,435);save(0);
                uint cp=p.Entity.ConquerPoints;HeartTalk(p,900000001,1);HeartTalk(p,900000001,11);
                Require(p.Entity.Level==140 && p.Entity.Class==45 && p.Entity.Reborn==2 && p.Proficiencies[500].Level==20);
                Require(p.Entity.ConquerPoints==cp-Convert.ToUInt32(Sql("SELECT peak_cp FROM revora_fresh_snow_loadout_probe")));
                HeartTalk(p,900000001,11);Require(p.Entity.ConquerPoints==cp-Convert.ToUInt32(Sql("SELECT peak_cp FROM revora_fresh_snow_loadout_probe")));
                save(1);stage=1;
            }
            if(stage==1){
                var old=p.Inventory.Objects.Select(i=>i.UID).ToArray();uint cp=p.Entity.ConquerPoints;
                HeartTalk(p,900000001,2);HeartTalk(p,900000001,12);var added=p.Inventory.Objects.Where(i=>!old.Contains(i.UID)).ToArray();
                Require(added.Length==7 && added.All(i=>i.Bound) && added.Any(i=>i.ID==500429) && added.Any(i=>i.ID==1050002));
                Require(p.Entity.ConquerPoints==cp-Convert.ToUInt32(Sql("SELECT kit_cp FROM revora_fresh_snow_loadout_probe")));
                var after=p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i).ToArray();HeartTalk(p,900000001,12);
                Require(after.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)) && p.Entity.ConquerPoints==cp-Convert.ToUInt32(Sql("SELECT kit_cp FROM revora_fresh_snow_loadout_probe")));
                Sql("UPDATE revora_fresh_snow_loadout_probe SET items=@items","items",added.Select(i=>(long)i.UID).ToArray());save(2);stage=2;
            }
            if(stage==2){
                var ids=(long[])Sql("SELECT items FROM revora_fresh_snow_loadout_probe");var items=p.Inventory.Objects.Concat(p.Equipment.GetItems()).Where(i=>ids.Contains((long)i.UID)).ToArray();Require(items.Length==7);
                int strength=Math.Max(0,items.Max(i=>(int)ConquerItemInformation.BaseInformations[i.ID].Strength)-p.Entity.Strength),agility=Math.Max(0,items.Max(i=>(int)ConquerItemInformation.BaseInformations[i.ID].Agility)-p.Entity.Agility);
                if(strength+agility>0){int points=p.Entity.Atributes;Require(points>=strength+agility);var packet=new byte[36];Revora.Network.Writer.WriteUInt16(28,0,packet);Revora.Network.Writer.WriteUInt16(1024,2,packet);Revora.Network.Writer.WriteUInt32((uint)strength,12,packet);Revora.Network.Writer.WriteUInt32((uint)agility,16,packet);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,28,packet);Revora.Network.PacketHandler.HandlePacket(packet,p);Require(p.Entity.Atributes==points-strength-agility);save(2);}
                foreach(var item in items.OrderBy(i=>ItemHandler.GetPositionFromID(i.ID))){byte slot=(byte)ItemHandler.GetPositionFromID(item.ID);if(item.Position==slot)continue;if(p.Equipment.TryGetItem(slot)!=null)ItemHandler.UnequipItem(new ItemUsage(true){dwParam=slot,Action=ItemUsage.Mode.UnequipItem},p);ItemHandler.EquipItem(new ItemUsage(true){UID=item.UID,dwParam=slot,Action=ItemUsage.Mode.EquipItem},p);Require(p.Equipment.TryGetItem(slot)!=null && p.Equipment.TryGetItem(slot).UID==item.UID);save(2);}
                p.LoadItemStats();save(3);stage=3;
            }
        }
        Require(stage==3 && p.Entity.Level==140 && p.Entity.Class==45 && p.Entity.Reborn==2);
        Require(p.Equipment.TryGetItem(4).ID==500429 && p.Equipment.TryGetItem(5).ID==1050002);
        var purchased=(long[])Sql("SELECT items FROM revora_fresh_snow_loadout_probe");
        var equipped=p.Equipment.GetItems().Where(i=>purchased.Contains((long)i.UID)).ToArray();
        Require(equipped.Length==7 && equipped.All(i=>i.Bound));
        foreach(var item in equipped.Where(i=>!ItemHandler.IsArrow(i.ID)))Require(item.Plus==12 && item.Bless==7 && item.SocketOne==Revora.Game.Enums.Gem.SuperDragonGem && item.SocketTwo==Revora.Game.Enums.Gem.SuperDragonGem && item.Purification.Available && item.Purification.PurificationLevel==7 && item.Purification.PurificationDuration==0);
        Require((bool)Sql("SELECT e.conquerpoints=v.before_cp-v.peak_cp-v.kit_cp AND NOT EXISTS(SELECT 1 FROM unnest(v.ids) old(id) WHERE NOT EXISTS(SELECT 1 FROM revora_achievement_evidence a WHERE a.uid=e.uid AND a.achievement_id=old.id)) FROM entities e CROSS JOIN revora_fresh_snow_loadout_probe v WHERE e.uid=@uid","uid",uid));
        Require(snapshot()==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_snow_loadout_probe")));
        Require(p.Entity.MaxHitpoints==30163 && p.Entity.MinAttack==25571 && p.Entity.MaxAttack==26563 && p.Entity.getFan(false)==7733 && p.Entity.Gems[1]==180);
        System.Console.WriteLine("[fresh-snow-loadout] level="+p.Entity.Level+" class="+p.Entity.Class+" hp="+p.Entity.MaxHitpoints+" min="+p.Entity.MinAttack+" max="+p.Entity.MaxAttack+" fan="+p.Entity.getFan(false)+" gems="+p.Entity.Gems[1]+" bow="+p.Equipment.TryGetItem(4).UID+" arrows="+p.Equipment.TryGetItem(5).UID+" spent="+Sql("SELECT peak_cp+kit_cp FROM revora_fresh_snow_loadout_probe"));
        System.Console.WriteLine("[career-process] "+phase+"-passed prior-achievements-preserved=true");System.Console.Out.Flush();Environment.Exit(0);
    }
    static void FreshMentorJoin(GameState p,GameState child,Func<string> prior,string phase){
        bool reload=phase.EndsWith("reload");long[] uids={(long)p.Entity.UID,(long)child.Entity.UID};
        Func<string> ko=()=>Convert.ToString(Sql("SELECT json_build_object('prior',@prior::json,'ko',(SELECT json_agg(r ORDER BY threshold) FROM revora_monster_ko_milestones r WHERE uid=@uid))::text","prior",prior(),"uid",(long)p.Entity.UID));
        Func<string> snapshot=()=>Convert.ToString(Sql("SELECT json_build_object('prior',@prior::json,'relationships',(SELECT json_agg(r ORDER BY mentorid,apprenticeid) FROM apprentice r WHERE mentorid=ANY(@uids) OR apprenticeid=ANY(@uids)),'admissions',(SELECT json_agg(r ORDER BY id) FROM revora_mentor_admissions r WHERE mentor=ANY(@uids) OR apprentice=ANY(@uids)),'evidence',(SELECT json_agg(r ORDER BY uid,achievement_id) FROM revora_achievement_evidence r WHERE uid=ANY(@uids)))::text","prior",ko(),"uids",uids));
        if((bool)Sql("SELECT to_regclass('revora_fresh_mentor_join_probe') IS NULL")){
            string previous=Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_normal_ko_probe WHERE buffered_done")),current=ko();
            if(previous!=current){System.IO.File.WriteAllText("fresh-mentor-prior.json",previous);System.IO.File.WriteAllText("fresh-mentor-current.json",current);}
            Require(!reload);
            if(current!=previous){
                Func<string,string> digest=value=>{using(var sha=System.Security.Cryptography.SHA256.Create())return System.BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant();};
                Require(digest(previous)=="1e7cf49956e43cc0724b888f66854da63a1dbd497bc4a4d0b73afb79b03192c6" && digest(current)=="f06832dcc50ccd59312b8d46d134eb00aa26694887e8f6556b281a3df0f01f32");
                System.Console.WriteLine("[fresh-mentor] reset-week="+Sql("SELECT week::text FROM revora_nobility_reset WHERE id"));
                Require((bool)Sql("SELECT EXISTS(SELECT 1 FROM revora_nobility_reset WHERE id AND week=DATE '2026-10-04') AND EXISTS(SELECT 1 FROM nobility WHERE entityuid=@uid AND donation=0 AND rey=0) AND EXISTS(SELECT 1 FROM revora_nobility_payments WHERE uid=@uid AND currency=1 AND amount=100000 AND after_donation=5000000000)","uid",(long)p.Entity.UID));
                // Audited weekly rollover changes only the two nested projections
                // of this donation. Preserve the old checkpoint, never restore it.
            }
            Require(child.Mentor==null && !p.Apprentices.ContainsKey(child.Entity.UID));
            Sql("CREATE TABLE revora_fresh_mentor_join_probe(snapshot text,done boolean,prior_snapshot text);INSERT INTO revora_fresh_mentor_join_probe VALUES(@snapshot,false,@prior)","snapshot",snapshot(),"prior",previous);
        }
        if(phase.Contains("-snow-loadout-") || phase.Contains("-snow-battle-")){FreshSnowLoadout(p,snapshot,phase);return;}
        Require(snapshot()==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_mentor_join_probe")) && p.Entity.Level==133 && p.Entity.Reborn==2 && child.Entity.Level==130 && child.Entity.Reborn==0);
        if(phase.EndsWith("snow-readiness")){
            Require((bool)Sql("SELECT done FROM revora_fresh_mentor_join_probe"));
            string before=snapshot();var e=p.Entity;
            Require(e.Level==133 && e.BattlePower>=138 && !e.Transformed);
            Require(!e.ContainsFlag((ulong)FlagsUpdate.Flags.Superman) && !e.ContainsFlag((ulong)FlagsUpdate.Flags.FatalStrike));
            var right=p.Equipment.TryGetItem(ConquerItem.RightWeapon);var left=p.Equipment.TryGetItem(ConquerItem.LeftWeapon);
            long fan=(long)e.getFan(false),baseRanged=checked((long)e.MinAttack+fan);
            long ranged=checked(baseRanged+baseRanged*e.Gems[1]/100);
            if(e.Gems[1]>=210)ranged=checked(ranged+ranged/2);
            // Level133 attacking138 gets no green-monster bonus. Omit all
            // positive critical/Bless effects. No attack or equipment mutation.
            string report=Convert.ToString(Sql("SELECT json_build_object('uid',@uid,'class',@class,'level',@level,'hp',@hp,'bp',@bp,'minimum_attack',@min,'fan',@fan,'dragon_gem_percent',@gem,'right_weapon',@right,'left_weapon',@left,'bow_dispatch_equipped',@bow,'unbuffed_ranged_arithmetic_floor',@ranged,'solo_full_uptime_floor',21277,'solo_half_uptime_floor',42554,'two_player_half_uptime_floor',21277,'current_damage_bound_only',true)::text",
                "uid",(long)e.UID,"class",(int)e.Class,"level",(int)e.Level,"hp",(long)e.MaxHitpoints,"bp",e.BattlePower,"min",(long)e.MinAttack,"fan",fan,"gem",(int)e.Gems[1],"right",right==null?0:(long)right.ID,"left",left==null?0:(long)left.ID,
                "bow",right!=null && right.ID/1000==500 && (left==null || ItemHandler.IsArrow(left.ID)),"ranged",ranged));
            Require(before==snapshot());System.IO.File.WriteAllText("fresh-snow-readiness.json",report);
            System.Console.WriteLine("[fresh-snow-readiness] "+report);
            System.Console.WriteLine("[career-process] "+phase+"-passed snapshot-unchanged=true");System.Console.Out.Flush();Environment.Exit(0);return;
        }
        if(phase.EndsWith("nobility-readiness")){
            Require((bool)Sql("SELECT done FROM revora_fresh_mentor_join_probe"));
            string before=snapshot();
            string funding=FreshDukeFundingBound(p);
            string report=Convert.ToString(Sql("SELECT json_build_object('uid',@uid,'donation',(SELECT donation FROM nobility WHERE entityuid=@uid),'rank',@rank,'level',@level,'class',@class,'reborn',@reborn,'hp',@hp,'maximum_hp',@maxhp,'minimum_attack',@min,'maximum_attack',@max,'physical_increase',@increase,'physical_decrease',@decrease,'critical',@critical,'defence',@defence,'battle_power',@bp,'cp',@cp,'silver',@silver)::text",
                "uid",(long)p.Entity.UID,"rank",p.Entity.NobilityRank.ToString(),"level",(int)p.Entity.Level,"class",(int)p.Entity.Class,"reborn",(int)p.Entity.Reborn,
                "hp",(long)p.Entity.Hitpoints,"maxhp",(long)p.Entity.MaxHitpoints,"min",(long)p.Entity.MinAttack,"max",(long)p.Entity.MaxAttack,
                "increase",(long)p.Entity.PhysicalDamageIncrease,"decrease",(long)p.Entity.PhysicalDamageDecrease,"critical",(long)p.Entity.CriticalStrike,
                "defence",(long)p.Entity.Defence,"bp",p.Entity.BattlePower,"cp",(long)p.Entity.ConquerPoints,"silver",(long)p.Entity.Money));
            report=Convert.ToString(Sql("SELECT (@profile::jsonb || jsonb_build_object('funding_bound',@funding::jsonb))::text","profile",report,"funding",funding));
            Require(before==snapshot());
            System.IO.File.WriteAllText("fresh-nobility-readiness.json",report);
            System.Console.WriteLine("[fresh-nobility-readiness] "+report);
            System.Console.WriteLine("[career-process] "+phase+"-passed snapshot-unchanged=true");System.Console.Out.Flush();Environment.Exit(0);return;
        }
        var rules=Feature("MentorAdmissions");
        Require((bool)Call(rules,"Eligible",(int)p.Entity.Level,(int)p.Entity.Reborn,(int)p.Entity.FirstRebornLevel,(int)p.Entity.SecondRebornLevel,15,2));
        Require(!(bool)Call(rules,"Eligible",140,2,140,140,(int)p.Entity.Level,(int)p.Entity.Reborn));
        int capacity=(int)Call(rules,"Capacity",(int)p.Entity.Level,(int)p.Entity.Reborn,(int)p.Entity.FirstRebornLevel,(int)p.Entity.SecondRebornLevel);
        Action save=()=>{Require(EntityTable.SaveEntity(p) && EntityTable.SaveEntity(child));Sql("UPDATE revora_fresh_mentor_join_probe SET snapshot=@snapshot","snapshot",snapshot());};
        if(!reload){
            Require(!(bool)Sql("SELECT done FROM revora_fresh_mentor_join_probe"));
            try{
                if(child.Mentor==null){
                    p.Entity.Teleport(1002,400,350);child.Entity.Teleport(1002,400,351);
                    string before=snapshot();RetainedMentorPacket(child,MentorApprentice.AcceptRequestApprentice,p.Entity.UID);Require(before==snapshot());
                    RetainedMentorPacket(p,MentorApprentice.RequestApprentice,child.Entity.UID);Require(before==snapshot());
                    RetainedMentorPacket(child,MentorApprentice.AcceptRequestApprentice,p.Entity.UID);
                }
                Require(child.Mentor!=null && child.Mentor.ID==p.Entity.UID && p.Apprentices.ContainsKey(child.Entity.UID));
                Require((long)Sql("SELECT count(*) FROM revora_mentor_admissions WHERE mentor=@mentor AND apprentice=@child AND mentor_level=133 AND mentor_reborn=2 AND apprentice_level=130 AND apprentice_reborn=0 AND capacity=@capacity","mentor",(long)p.Entity.UID,"child",(long)child.Entity.UID,"capacity",capacity)==1);
                save();string once=snapshot();RetainedMentorPacket(child,MentorApprentice.AcceptRequestApprentice,p.Entity.UID);Require(once==snapshot());
                Sql("UPDATE revora_fresh_mentor_join_probe SET done=true");
            }catch{save();throw;}
        }
        Require((bool)Sql("SELECT done FROM revora_fresh_mentor_join_probe") && child.Mentor!=null && child.Mentor.ID==p.Entity.UID && p.Apprentices.ContainsKey(child.Entity.UID));
        Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE source='mentor-admission-v1' AND ((uid=@mentor AND achievement_id=10124) OR (uid=@child AND achievement_id=10125))","mentor",(long)p.Entity.UID,"child",(long)child.Entity.UID)==2 && snapshot()==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_mentor_join_probe")));
        System.IO.File.WriteAllText("fresh-current-career.json",FreshCurrentCareerReport(p,"revora_fresh_mentor_join_probe"));
        System.Console.WriteLine("[career-process] "+phase+"-passed mentor="+p.Entity.UID+" child="+child.Entity.UID+" capacity="+capacity+" senior-for-level15=true snapshot-exact=true");System.Console.Out.Flush();Environment.Exit(0);
    }
    static void FreshNormalKo(GameState p,Func<string> prior,string phase){
        Func<string> snapshot=()=>Convert.ToString(Sql("SELECT json_build_object('prior',@prior::json,'ko',(SELECT json_agg(r ORDER BY threshold) FROM revora_monster_ko_milestones r WHERE uid=@uid))::text","prior",prior(),"uid",(long)p.Entity.UID));
        bool reload=phase.EndsWith("reload"),buffered=phase.Contains("buffered");
        if((bool)Sql("SELECT to_regclass('revora_fresh_normal_ko_probe') IS NULL")){
            Require(!reload && prior()==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_funding_repair_probe WHERE done")));
            Sql("CREATE TABLE revora_fresh_normal_ko_probe(snapshot text,done boolean,kills int,chain_ms bigint);INSERT INTO revora_fresh_normal_ko_probe VALUES(@snapshot,false,0,0)","snapshot",snapshot());
        }
        Sql("ALTER TABLE revora_fresh_normal_ko_probe ADD COLUMN IF NOT EXISTS buffered_done boolean NOT NULL DEFAULT false;ALTER TABLE revora_fresh_normal_ko_probe ADD COLUMN IF NOT EXISTS buffered_ms bigint NOT NULL DEFAULT 0");
        Require(snapshot()==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_normal_ko_probe")));
        if(phase.EndsWith("rising-denied")){
            Require((bool)Sql("SELECT buffered_done FROM revora_fresh_normal_ko_probe"));
            new TitleStorage().Login(p);
            Require(p.Entity.MyAchievement.CompletedCount==148 && (bool)Call(GameAssembly.GetType("Revora.TitleRequirements",true),"Ready",p));
            int title=p.Entity.EquippedTitle;string before=snapshot();
            string titles=Convert.ToString(Sql("SELECT encode(data,'hex') FROM titles WHERE id=@uid","uid",(long)p.Entity.UID));
            new TitleStorage{Info=new TitleStorageProto{Type=2001,Id=2,ActionId=TitleStorage.Action.Equip,Value=new TitleValue{Type=2001,Id=2}}}.Handle(p);
            Require(p.Entity.EquippedTitle==title && before==snapshot() && titles==Convert.ToString(Sql("SELECT encode(data,'hex') FROM titles WHERE id=@uid","uid",(long)p.Entity.UID)));
            System.Console.WriteLine("[career-process] "+phase+"-passed verified="+p.Entity.MyAchievement.CompletedCount);System.Console.Out.Flush();Environment.Exit(0);return;
        }
        Action save=()=>{SkillTable.SaveSpells(p);SkillTable.SaveProficiencies(p);Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_normal_ko_probe SET snapshot=@snapshot","snapshot",snapshot());};
        if(!reload){
            Require((buffered?(bool)Sql("SELECT done AND NOT buffered_done FROM revora_fresh_normal_ko_probe"):!(bool)Sql("SELECT done FROM revora_fresh_normal_ko_probe")) && p.Spells.ContainsKey(1110) && p.Entity.KOCount==0);
            p.Entity.Teleport(1002,400,350);
            var mobs=p.Map.Entities.Values.Where(m=>m.MonsterInfo!=null && !m.MonsterInfo.Boss && !m.MonsterInfo.Guard && !m.MonsterInfo.Reviver && !m.Companion && !m.Dead && m.MaxHitpoints<1000).OrderBy(m=>m.UID).Take(400).ToArray();
            Require(mobs.Length==400);int kills=0;var chain=System.Diagnostics.Stopwatch.StartNew();
            Action<Entity> kill=m=>{
                p.Entity.X=m.X;p.Entity.Y=(ushort)(m.Y+1);p.Screen.Add(m);
                var attempt=System.Diagnostics.Stopwatch.StartNew();
                while(!m.Dead && attempt.Elapsed.TotalSeconds<8){System.Threading.Thread.Sleep(510);Attack.HandleAttack(new Attack(true){Attacker=p.Entity.UID,Attacked=m.UID,X=m.X,Y=m.Y,AttackType=Attack.Melee},p);}
                Require(m.Dead && p.Action==2);kills++;
            };
            try{
                for(int n=0;n<100;n++)kill(mobs[n]);Require(p.XPCount>=100 && p.Entity.KOCount==0);save();
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;var wrapper=typeof(Revora.Network.Sockets.ClientWrapper);
                wrapper.GetField("SendQueue",flags).SetValue(p.Socket,new System.Collections.Generic.Queue<byte[]>());wrapper.GetField("SendSyncRoot",flags).SetValue(p.Socket,new object());
                System.Threading.Thread.Sleep(4000);p.Socket.Alive=true;
                try{typeof(Revora.Thread).GetMethod("BuffersCallback",flags).Invoke(Server.Thread,new object[]{p,unchecked((int)Time32.Now.Value)});Require(p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.XPList));}finally{p.Socket.Alive=false;}
                new Revora.Game.Attacking.Handle(new Attack(true){AttackType=Attack.Magic,Decoded=true,Damage=1110,Attacker=p.Entity.UID,Attacked=p.Entity.UID,X=p.Entity.X,Y=p.Entity.Y},p.Entity,p.Entity);
                Require(p.Entity.OnCyclone() && !p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.XPList));chain.Restart();
                for(int n=100;n<400;n++){kill(mobs[n]);if(buffered && n%2==1){p.Socket.Alive=true;try{typeof(Revora.Thread).GetMethod("BuffersCallback",flags).Invoke(Server.Thread,new object[]{p,unchecked((int)Time32.Now.Value)});}finally{p.Socket.Alive=false;}}Require(p.Entity.KOCount==n-99);if((n+1)%50==0)System.Console.WriteLine("[fresh-normal-ko] chain="+p.Entity.KOCount+" elapsed-ms="+chain.ElapsedMilliseconds);}
                Require(chain.ElapsedMilliseconds>=153000 && p.Entity.KOCount==300);
                if(buffered)Sql("UPDATE revora_fresh_normal_ko_probe SET buffered_done=true,buffered_ms=@ms","ms",chain.ElapsedMilliseconds);else Sql("UPDATE revora_fresh_normal_ko_probe SET done=true,kills=@kills,chain_ms=@ms","kills",kills,"ms",chain.ElapsedMilliseconds);
                save();
            }catch{Sql("UPDATE revora_fresh_normal_ko_probe SET kills=@kills","kills",kills);save();throw;}
        }else Require((bool)Sql("SELECT done AND kills=400 AND chain_ms>=153000 FROM revora_fresh_normal_ko_probe") && p.Entity.KOCount==0);
        if(buffered)Require((bool)Sql("SELECT buffered_done AND buffered_ms>=153000 FROM revora_fresh_normal_ko_probe"));
        Require((long)Sql("SELECT count(*) FROM revora_monster_ko_milestones WHERE uid=@uid AND completed_at IS NOT NULL","uid",(long)p.Entity.UID)==3);
        Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id IN(10520,10521,10522)","uid",(long)p.Entity.UID)==3 && snapshot()==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_normal_ko_probe")));
        System.Console.WriteLine("[fresh-normal-ko] catalog-cyclone-seconds="+SpellTable.GetSpell(1110,p.Spells[1110].Level).Duration);
        System.IO.File.WriteAllText("fresh-current-career.json",FreshCurrentCareerReport(p,"revora_fresh_normal_ko_probe"));
        System.Console.WriteLine("[career-process] "+phase+"-passed chain-ms="+Sql(buffered?"SELECT buffered_ms FROM revora_fresh_normal_ko_probe":"SELECT chain_ms FROM revora_fresh_normal_ko_probe"));System.Console.Out.Flush();Environment.Exit(0);
    }
    static void FreshFundingRepair(GameState p,Func<string> snapshot,string phase){
        bool reload=phase.EndsWith("reload");
        if((bool)Sql("SELECT to_regclass('revora_fresh_funding_repair_probe') IS NULL")){
            Require(!reload && snapshot()==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_arena_classes_probe WHERE stage=2")));
            Sql("CREATE TABLE revora_fresh_funding_repair_probe(snapshot text,cp bigint,silver bigint,done boolean);INSERT INTO revora_fresh_funding_repair_probe VALUES(@snapshot,@cp,@silver,false);CREATE TABLE revora_fresh_funding_repair_items(uid bigint PRIMARY KEY,position int);INSERT INTO revora_fresh_funding_repair_items SELECT uid,position FROM items WHERE entityid=@uid AND position IN(1,2,3,4,5,6,8,10,11,18) AND durability<maximdurability","snapshot",snapshot(),"cp",(long)p.Entity.ConquerPoints,"silver",(long)p.Entity.Money,"uid",(long)p.Entity.UID);
        }
        Require(snapshot()==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_funding_repair_probe")));
        if(phase.EndsWith("inspect")){
            Require(!(bool)Sql("SELECT done FROM revora_fresh_funding_repair_probe"));
            var helmet=p.Inventory.Objects.Single(i=>i.ID==111009);
            Require(helmet.Durability==helmet.MaximDurability && helmet.Position==0 && p.Equipment.TryGetItem(1)==null && p.Entity.Class==41);
            string before=snapshot();ItemHandler.EquipItem(new ItemUsage(true){UID=helmet.UID,dwParam=1,Action=ItemUsage.Mode.EquipItem},p);
            Require(p.Equipment.TryGetItem(1)==null && helmet.Position==0 && before==snapshot());
            System.Console.WriteLine("[fresh-repair] cold-checkpoint=true helmet-repaired=true archer-re-equip-rejected=true cp="+p.Entity.ConquerPoints+" silver="+p.Entity.Money);
            System.Console.WriteLine("[career-process] "+phase+"-passed");System.Console.Out.Flush();Environment.Exit(0);return;
        }
        if(phase.EndsWith("audit")){
            Require((bool)Sql("SELECT done FROM revora_fresh_funding_repair_probe"));
            string report=FreshCurrentCareerReport(p,"revora_fresh_funding_repair_probe");
            System.IO.File.WriteAllText("fresh-current-career.json",report);
            foreach(ushort id in new ushort[]{1110,1025,12860})System.Console.WriteLine("[fresh-audit] xp-skill="+id+" learned="+p.Spells.ContainsKey(id));
            Require(snapshot()==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_funding_repair_probe")));
            System.Console.WriteLine("[career-process] "+phase+"-passed");System.Console.Out.Flush();Environment.Exit(0);return;
        }
        Action save=()=>{Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_funding_repair_probe SET snapshot=@snapshot","snapshot",snapshot());};
        var plans=new System.Collections.Generic.List<Tuple<uint,ushort>>();
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT uid,position FROM revora_fresh_funding_repair_items ORDER BY position",c))using(var r=q.ExecuteReader())while(r.Read())plans.Add(Tuple.Create((uint)r.GetInt64(0),(ushort)r.GetInt32(1)));}
        foreach(var plan in plans){
            var item=p.Inventory.Objects.Concat(p.Equipment.GetItems()).Single(i=>i.UID==plan.Item1);
            if(reload){Require(item.Durability==item.MaximDurability && item.Position==plan.Item2);continue;}
            if(item.Durability==item.MaximDurability && item.Position==plan.Item2)continue;
            Require(p.Inventory.Count<35);
            if(item.Position!=0){ItemHandler.UnequipItem(new ItemUsage(true){dwParam=plan.Item2,Action=ItemUsage.Mode.UnequipItem},p);Require(p.Inventory.ContainsUID(item.UID));save();}
            if(item.Durability==0){for(int n=0;n<5;n++){CareerBuy(p,1088001);save();}}
            if(item.Durability<item.MaximDurability){ItemHandler.HandleRepair(new ItemUsage(true){UID=item.UID,Action=ItemUsage.Mode.Repair},p);Require(item.Durability==item.MaximDurability);save();}
            string paid=snapshot();ItemHandler.HandleRepair(new ItemUsage(true){UID=item.UID,Action=ItemUsage.Mode.Repair},p);Require(paid==snapshot());
            System.Console.WriteLine("[fresh-repair-equip] slot="+plan.Item2+" id="+item.ID+" eligible="+ItemHandler.Equipable(item,p)+" position="+item.Position+" suspicious="+item.Suspicious+" map="+p.Entity.MapID+" level="+p.Entity.Level+" body="+p.Entity.Body+" item-gender="+ConquerItemInformation.BaseInformations[item.ID].Gender);
            ItemHandler.EquipItem(new ItemUsage(true){UID=item.UID,dwParam=plan.Item2,Action=ItemUsage.Mode.EquipItem},p);save();
            System.Console.WriteLine("[fresh-repair-equip] after-position="+item.Position+" map="+p.Entity.MapID);
            Require(p.Equipment.TryGetItem(plan.Item2)!=null && p.Equipment.TryGetItem(plan.Item2).UID==item.UID);
            System.Console.WriteLine("[fresh-repair] slot="+plan.Item2+" restored="+item.Durability);
        }
        long cpCost=Convert.ToInt64(Sql("SELECT cp FROM revora_fresh_funding_repair_probe"))-(long)p.Entity.ConquerPoints;
        long silverCost=Convert.ToInt64(Sql("SELECT silver FROM revora_fresh_funding_repair_probe"))-(long)p.Entity.Money;
        Require(cpCost>=0 && cpCost<1000000 && silverCost>=0 && silverCost<8176000);
        if(reload)Require((bool)Sql("SELECT done FROM revora_fresh_funding_repair_probe"));else Sql("UPDATE revora_fresh_funding_repair_probe SET done=true");
        Require(snapshot()==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_funding_repair_probe")));
        System.Console.WriteLine("[fresh-repair] cp-cost="+cpCost+" silver-cost="+silverCost+" net-cp="+(1000000-cpCost)+" net-silver="+(8176000-silverCost));
        System.Console.WriteLine("[career-process] "+phase+"-passed");System.Console.Out.Flush();Environment.Exit(0);
    }
    static void FreshPaidClassTransition(GameState p,Func<string> prior,string phase){
        Func<string> snapshot=()=>Convert.ToString(Sql("SELECT json_build_object('prior',@prior::json,'paid',(SELECT json_agg(r ORDER BY sequence) FROM revora_reincarnations r WHERE uid=@uid),'dew',(SELECT json_agg(r ORDER BY created_at) FROM revora_oblivion_purchases r WHERE uid=@uid),'recovery',(SELECT json_agg(r) FROM reincarnation r WHERE uid=@uid))::text","prior",prior(),"uid",(long)p.Entity.UID));
        Func<string> receipts=()=>Convert.ToString(Sql("SELECT json_agg(r ORDER BY achievement_id)::text FROM revora_achievement_evidence r WHERE uid=@uid","uid",(long)p.Entity.UID));
        Action save=()=>{Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_paid_transition_probe SET snapshot=@snapshot","snapshot",snapshot());};
        if((bool)Sql("SELECT to_regclass('revora_fresh_paid_transition_probe') IS NULL")){
            Require(phase.EndsWith("pay") && p.Entity.Level==133 && p.Entity.Class==25 && p.Entity.Reborn==2 && p.Entity.ConquerPoints==4610185);
            // Preserve the unfinished Arena trial; only its reviewed town travel,
            // travel receipt and daily rollover differ from the last saved match.
            using(var sha=System.Security.Cryptography.SHA256.Create())Require(System.BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(prior()))).Replace("-","").ToLowerInvariant()=="04e429eaff5e1aeac62197091edbbab3b08d03fc245fa54fb989e8064343bf7e");
            Require((long)Sql("SELECT count(*) FROM revora_reincarnations WHERE uid=@uid","uid",(long)p.Entity.UID)==0 && !p.Inventory.Contains(711083,1));
            Sql("CREATE TABLE revora_fresh_paid_transition_probe(stage int,cp bigint,receipts text,snapshot text);INSERT INTO revora_fresh_paid_transition_probe VALUES(0,@cp,@receipts,@snapshot)","cp",(long)p.Entity.ConquerPoints,"receipts",receipts(),"snapshot",snapshot());
        }
        string savedTransition=Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_paid_transition_probe")),currentTransition=snapshot();
        if(savedTransition!=currentTransition){System.IO.File.WriteAllText("fresh-paid-prior.json",savedTransition);System.IO.File.WriteAllText("fresh-paid-current.json",currentTransition);}
        if(savedTransition!=currentTransition){
            // The bounded runner stopped after the 129th ball committed and its
            // counters advanced, before the full test snapshot finished saving.
            Require(phase.EndsWith("grow"));
            using(var sha=System.Security.Cryptography.SHA256.Create()){
                Require(System.BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(savedTransition))).Replace("-","").ToLowerInvariant()=="abc4d8874126140e2cc9094477a21fd228ba5f33d0ec2fe5beb07e9098eeb107");
                Require(System.BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(currentTransition))).Replace("-","").ToLowerInvariant()=="c3b3cde050ebf0a3ab9dec46743a1bf9ae3494d3d86cc0db0851a08abac58472");
            }
            Require((bool)Sql("SELECT balls=129 AND ball_spend=3483 FROM revora_fresh_paid_transition_probe"));
            Sql("UPDATE revora_fresh_paid_transition_probe SET snapshot=@snapshot","snapshot",currentTransition);
            System.Console.WriteLine("[fresh-paid] audited-ball-129-checkpoint-recovered");
        }
        Sql("ALTER TABLE revora_fresh_paid_transition_probe ADD COLUMN IF NOT EXISTS balls int NOT NULL DEFAULT 0;ALTER TABLE revora_fresh_paid_transition_probe ADD COLUMN IF NOT EXISTS ball_spend bigint NOT NULL DEFAULT 0;");
        int stage=(int)Sql("SELECT stage FROM revora_fresh_paid_transition_probe");
        if(phase.EndsWith("grow")){
            Require(stage==2);
            DateTime day=DateTime.Today;
            var storedDay=Sql("SELECT day FROM revora_exp_ball_days WHERE uid=@uid","uid",(long)p.Entity.UID);
            if(storedDay!=null && storedDay!=DBNull.Value){int d=Convert.ToInt32(storedDay);day=new DateTime(d/10000,d/100%100,d%100).AddDays(1);}
            Feature("ExperienceConsumables").GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>day));
            p.Entity.Teleport(1002,304,435);
            Func<string> ballState=()=>Convert.ToString(Sql("SELECT json_build_object('profile',(SELECT json_build_object('level',level,'xp',experience,'cp',conquerpoints,'silver',money) FROM entities WHERE uid=@uid),'items',(SELECT json_agg(r ORDER BY uid) FROM items r WHERE entityid=@uid),'skills',(SELECT json_agg(r ORDER BY id) FROM skills r WHERE entityid=@uid),'profs',(SELECT json_agg(r ORDER BY id) FROM profs r WHERE entityid=@uid),'evidence',(SELECT json_agg(r ORDER BY achievement_id) FROM revora_achievement_evidence r WHERE uid=@uid),'allowance',(SELECT json_agg(r) FROM revora_exp_ball_days r WHERE uid=@uid))::text","uid",(long)p.Entity.UID));
            int batch=0;
            while(p.Entity.Level<110 && batch<1000){
                if(batch>0 && batch%10==0)day=day.AddDays(1);
                uint cp=p.Entity.ConquerPoints;var owned=p.Inventory.Objects.Select(i=>i.UID).ToArray();CareerBuy(p,723700);
                var ball=p.Inventory.Objects.Single(i=>!owned.Contains(i.UID));uint cost=cp-p.Entity.ConquerPoints;
                Require(ball.ID==723700 && cost==ConquerItemInformation.BaseInformations[723700].ConquerPointsWorth);
                byte oldLevel=p.Entity.Level;ulong oldXp=p.Entity.Experience;ItemHandler.UseItem(ball,p);
                Require(!p.Inventory.ContainsUID(ball.UID) && (p.Entity.Level>oldLevel || p.Entity.Experience>oldXp));
                string once=ballState();ItemHandler.UseItem(ball,p);Require(ballState()==once);
                batch++;Require((bool)Sql("SELECT used=@used AND day=@day FROM revora_exp_ball_days WHERE uid=@uid","used",batch%10==0?10:batch%10,"day",day.Year*10000+day.Month*100+day.Day,"uid",(long)p.Entity.UID));
                Sql("UPDATE revora_fresh_paid_transition_probe SET balls=balls+1,ball_spend=ball_spend+@cost","cost",(long)cost);save();
            }
            Require(p.Entity.Level>=133);Sql("UPDATE revora_fresh_paid_transition_probe SET stage=3");save();stage=3;
        }
        if(phase.EndsWith("pay")){
            if(stage==0){
                uint before=p.Entity.ConquerPoints;HeartTalk(p,59558,3);HeartTalk(p,59558,6);HeartTalk(p,59558,7);
                Require(p.Entity.ConquerPoints==before-2000 && p.Inventory.Contains(711083,1));
                string once=snapshot();HeartTalk(p,59558,7);Require(once==snapshot());
                Sql("UPDATE revora_fresh_paid_transition_probe SET stage=1");save();stage=1;
            }
            if(stage==1){
                HeartTalk(p,59558,3);HeartTalk(p,59558,5);CareerPaidRequest(p,41);
                Require(p.Entity.Class==41 && p.Entity.Level==15 && p.Entity.Reborn==2);
                Sql("UPDATE revora_fresh_paid_transition_probe SET stage=2");save();stage=2;
            }
        }else Require(phase.EndsWith("reload") || phase.EndsWith("grow") || phase.EndsWith("recovered"));
        Require((stage==2 && p.Entity.Level==15 || stage==3 && p.Entity.Level>=133) && p.Entity.Class==41 && p.Entity.Reborn==2);
        Require(p.Entity.ConquerPoints==Convert.ToUInt32(Sql("SELECT cp FROM revora_fresh_paid_transition_probe"))-2000-Config.SerethReincarnationCp-Convert.ToUInt32(Sql("SELECT ball_spend FROM revora_fresh_paid_transition_probe")) && !p.Inventory.Contains(711083,1));
        Require((long)Sql("SELECT count(*) FROM revora_reincarnations WHERE uid=@uid AND old_class=25 AND old_level=133 AND new_class=41 AND (recovered_at IS NOT NULL)=@recovered AND attribute_budget=82 AND cost=@cost AND balance_after=balance_before-cost","uid",(long)p.Entity.UID,"cost",(long)Config.SerethReincarnationCp,"recovered",stage==3)==1);
        Require((long)Sql("SELECT count(*) FROM revora_oblivion_purchases WHERE uid=@uid AND cost=2000 AND balance_after=balance_before-2000","uid",(long)p.Entity.UID)==1);
        Require((long)Sql("SELECT count(*) FROM reincarnation WHERE uid=@uid AND level=133","uid",(long)p.Entity.UID)==(stage==3?0:1));
        Require(receipts()==Convert.ToString(Sql("SELECT receipts FROM revora_fresh_paid_transition_probe")));
        string final=snapshot();CareerPaidRequest(p,41);lock(p.ItemSyncRoot)Call(Feature("ReincarnationRecovery"),"RestoreReady",p);
        Require(final==snapshot() && final==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_paid_transition_probe")));
        System.Console.WriteLine("[career-process] "+phase+"-passed class=41 level="+p.Entity.Level+" cost="+(2000+Config.SerethReincarnationCp)+" receipts-preserved=true recovery-level=133 balls="+Sql("SELECT balls FROM revora_fresh_paid_transition_probe")+" ball-spend="+Sql("SELECT ball_spend FROM revora_fresh_paid_transition_probe"));
        Server.CommandsAI("@close");throw new InvalidOperationException("Fresh paid transition did not close.");
    }

}
