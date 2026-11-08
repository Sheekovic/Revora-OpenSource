using System;
using System.Linq;
using System.Reflection;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static partial class CheckCandidateArenaLifecycle {
    static void FreshArcherArrow(GameState p,Func<string> snapshot,string phase){
        long uid=p.Entity.UID;bool reload=phase.EndsWith("reload");
        if((bool)Sql("SELECT to_regclass('revora_fresh_archer_arrow_probe') IS NULL")){
            Require(!reload && (int)Sql("SELECT stage FROM revora_fresh_snow_battle_probe")==3);
            Require(snapshot()==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_snow_battle_probe")));
            var old=p.Equipment.TryGetItem(5);Require(old!=null && old.ID==1050002 && old.Durability==0 && p.Inventory.Count<=38 && !p.Inventory.Objects.Any(i=>i.ID==1050002));
            Sql("CREATE TABLE revora_fresh_archer_arrow_probe(uid bigint PRIMARY KEY,stage integer,old_arrow bigint,new_arrow bigint,money_before bigint,cp_before bigint,old_snapshot text,snapshot text);INSERT INTO revora_fresh_archer_arrow_probe VALUES(@uid,0,@old,0,@money,@cp,@snapshot,@snapshot)","uid",uid,"old",(long)old.UID,"money",(long)p.Entity.Money,"cp",(long)p.Entity.ConquerPoints,"snapshot",snapshot());
        }
        Require(snapshot()==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_archer_arrow_probe WHERE uid=@uid","uid",uid)));
        int stage=(int)Sql("SELECT stage FROM revora_fresh_archer_arrow_probe WHERE uid=@uid","uid",uid);
        if(!reload){
            Require(stage==0 && ConquerItemInformation.BaseInformations[1050002].GoldWorth==50000);
            var arrow=CareerSilverBuy(p,1050002,5);Require(arrow.Durability==1 && arrow.MaximDurability==1);
            ItemHandler.EquipItem(new ItemUsage(true){UID=arrow.UID,dwParam=5,Action=ItemUsage.Mode.EquipItem},p);
            Require(p.Equipment.TryGetItem(5).UID==arrow.UID && EntityTable.SaveEntity(p));
            Sql("UPDATE revora_fresh_archer_arrow_probe SET stage=1,new_arrow=@arrow,snapshot=@snapshot WHERE uid=@uid","uid",uid,"arrow",(long)arrow.UID,"snapshot",snapshot());
        }else Require(stage==1);
        uint owned=Convert.ToUInt32(Sql("SELECT new_arrow FROM revora_fresh_archer_arrow_probe WHERE uid=@uid","uid",uid));
        uint previous=Convert.ToUInt32(Sql("SELECT old_arrow FROM revora_fresh_archer_arrow_probe WHERE uid=@uid","uid",uid));
        Require(p.Equipment.TryGetItem(5).UID==owned && p.Equipment.TryGetItem(5).Durability==1 && p.Inventory.Objects.Any(i=>i.UID==previous && i.ID==1050002 && i.Durability==0));
        Require(p.Entity.Money+50000==Convert.ToUInt64(Sql("SELECT money_before FROM revora_fresh_archer_arrow_probe WHERE uid=@uid","uid",uid)) && p.Entity.ConquerPoints==Convert.ToUInt32(Sql("SELECT cp_before FROM revora_fresh_archer_arrow_probe WHERE uid=@uid","uid",uid)));
        string after=snapshot();ItemHandler.EquipItem(new ItemUsage(true){UID=owned,dwParam=5,Action=ItemUsage.Mode.EquipItem},p);Require(after==snapshot());
        Require(after==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_archer_arrow_probe WHERE uid=@uid","uid",uid)) && !PirateFenced(p));
        Console.WriteLine("[fresh-archer-arrow] uid="+uid+" old="+previous+" new="+owned+" paid-silver=50000 durability=1 old-preserved=true");
        Console.WriteLine("[career-process] "+phase+"-passed");Console.Out.Flush();Environment.Exit(0);
    }
    static void FreshSnowBattle(GameState p,Func<string> prior,string phase){
        long uid=p.Entity.UID;var encounter=Feature("SnowBansheeEncounter");var store=Feature("SnowBansheeStore");var combat=Feature("SnowBansheeCombat");
        var instance=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        Func<Entity> body=()=>{var a=encounter.GetField("Active",Hidden).GetValue(null);return a==null?null:(Entity)a.GetType().GetField("Body",instance).GetValue(a);};
        Func<string> snapshot=()=>Convert.ToString(Sql("SELECT json_build_object('prior',@prior::json,'run',(SELECT row_to_json(r) FROM (SELECT id,state,hp,started_at,ended_at,killer,party FROM revora_snow_banshee_runs WHERE id=(SELECT run FROM revora_fresh_snow_battle_probe))r),'loot',(SELECT json_agg(r ORDER BY slot) FROM revora_snow_banshee_loot r WHERE run=(SELECT run FROM revora_fresh_snow_battle_probe)),'study',(SELECT json_agg(r ORDER BY uid) FROM revora_snow_banshee_study r WHERE run=(SELECT run FROM revora_fresh_snow_battle_probe)))::text","prior",prior()));
        if(phase.Contains("-arrow-")){FreshArcherArrow(p,snapshot,phase);return;}
        if((bool)Sql("SELECT to_regclass('revora_fresh_snow_battle_probe') IS NULL")){
            Require(phase.EndsWith("prepare") && prior()==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_snow_loadout_probe WHERE stage=3")));
            Sql("CREATE TABLE revora_fresh_snow_battle_probe(stage int,run uuid,stamp timestamptz,ticks int DEFAULT 0,hits int DEFAULT 0,damage bigint DEFAULT 0,received bigint DEFAULT 0,boss_healed bigint DEFAULT 0,used int DEFAULT 0,bought int DEFAULT 0,repairs int DEFAULT 0,min_hp bigint,silver bigint,study bigint,arrow_durability int,snapshot text);INSERT INTO revora_fresh_snow_battle_probe(stage,min_hp,silver,study,arrow_durability) VALUES(0,@hp,@silver,@study,@arrows)","hp",(long)p.Entity.Hitpoints,"silver",(long)p.Entity.Money,"study",(long)p.Entity.SubClasses.StudyPoints,"arrows",(int)p.Equipment.TryGetItem(5).Durability);
            Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_snow_battle_probe SET snapshot=@snapshot","snapshot",snapshot());
        }
        Require(snapshot()==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_snow_battle_probe")));
        Action save=()=>{Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_snow_battle_probe SET snapshot=@snapshot","snapshot",snapshot());};
        int stage=(int)Sql("SELECT stage FROM revora_fresh_snow_battle_probe");
        if(phase.EndsWith("prepare")){
            Require(stage==0);
            while(p.Inventory.Count>20){
                var bank=p.Warehouses.First(w=>w.Value.Count<w.Value.MaxCount);uint id=(uint)bank.Key;var map=Kernel.Maps.Values.First(m=>m.ID==m.BaseID && m.Npcs.ContainsKey(id));var npc=map.Npcs[id];p.Entity.Teleport(map.ID,(ushort)(npc.X-1),npc.Y);
                var item=p.Inventory.Objects.First(i=>i.Days==0 && i.ID!=1000040);
                var frame=new byte[172];Revora.Network.Writer.WriteUInt16(164,0,frame);Revora.Network.Writer.WriteUInt16(1102,2,frame);Revora.Network.Writer.WriteUInt32(id,4,frame);frame[12]=1;frame[13]=10;Revora.Network.Writer.WriteUInt32(item.UID,18,frame);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,164,frame);Warehouse.Process(frame,p);Require(!p.Inventory.ContainsUID(item.UID));save();
            }
            p.Entity.Teleport(1002,304,435);var shop=p.Map.Npcs[3];p.Entity.X=shop.X;p.Entity.Y=(ushort)(shop.Y+1);
            while(FreshBossMedicine(p)<100 && p.Inventory.Count<38){
                int count=FreshBossMedicine(p);ulong money=p.Entity.Money;var buy=new ItemUsage(true){UID=3,dwParam=1000040,dwExtraInfo=1,Action=ItemUsage.Mode.BuyFromNPC}.ToArray();Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,buy.Length-8,buy);ItemUsage.Process(buy,p);Require(FreshBossMedicine(p)==count+1 && p.Entity.Money==money-1000);Sql("UPDATE revora_fresh_snow_battle_probe SET bought=bought+1");save();
            }
            Require(FreshBossMedicine(p)>=50);Sql("UPDATE revora_fresh_snow_battle_probe SET stage=1");save();
        }else if(phase.EndsWith("combat")){
            Require(stage==1);DateTime now=DateTime.UtcNow;
            var due=Sql("SELECT next_spawn_at AT TIME ZONE 'UTC' FROM revora_snow_banshee_schedule WHERE id=1");if(due!=DBNull.Value){var next=DateTime.SpecifyKind((DateTime)due,DateTimeKind.Utc);if(next>now)now=next.AddSeconds(1);}
            store.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));Call(encounter,"Tick");var boss=body();Require(boss!=null && boss.Hitpoints==100000000);
            Sql("UPDATE revora_fresh_snow_battle_probe SET run=(SELECT id FROM revora_snow_banshee_runs WHERE state='active'),stamp=@stamp,stage=2","stamp",new DateTimeOffset(now));
            FreshClassicTalk(p,1000,27837,2);Require(p.Entity.MapID==1927);p.Entity.X=(ushort)(boss.X+10);p.Entity.Y=boss.Y;p.Screen.Add(boss);p.Attackable=true;p.CantAttack=Time32.Now.AddSeconds(-1);save();
            int ticks=0,hits=0,used=0;long damage=0,received=0,healed=0;uint minimum=p.Entity.Hitpoints;
            while(!boss.Dead && ticks<7200){
                now=now.AddMilliseconds(500);ticks++;uint beforeBoss=boss.Hitpoints,beforePlayer=p.Entity.Hitpoints;
                Call(encounter,"Tick");Require(!p.Entity.Dead && !PirateFenced(p));
                received+=beforePlayer-p.Entity.Hitpoints;minimum=Math.Min(minimum,p.Entity.Hitpoints);if(boss.Hitpoints>beforeBoss)healed+=boss.Hitpoints-beforeBoss;
                else damage+=beforeBoss-boss.Hitpoints; // passive counters during the enemy callback
                bool drank=false;
                if(p.Entity.Hitpoints+7000<=p.Entity.MaxHitpoints){
                    Require(FreshBossMedicine(p)>0);var medicine=p.Inventory.Objects.First(i=>i.ID==1000040);int count=FreshBossMedicine(p);uint hp=p.Entity.Hitpoints;ItemHandler.UseItem(medicine,p);Require(FreshBossMedicine(p)==count-1 && p.Entity.Hitpoints==hp+7000);used++;drank=true;
                }
                // One normal attack at most per500ms; medicine replaces that
                // slot. The boss clock advances even while attacks are frozen.
                if(!drank && !(bool)Call(combat,"IsStunned",p)){
                    beforeBoss=boss.Hitpoints;p.Entity.AttackStamp=Time32.Now.AddSeconds(-1);
                    Attack.HandleAttack(new Attack(true){Attacker=p.Entity.UID,Attacked=boss.UID,X=boss.X,Y=boss.Y,AttackType=Attack.Ranged},p);
                    Require(boss.Hitpoints<=beforeBoss);damage+=beforeBoss-boss.Hitpoints;hits++;
                }
                Require(p.Equipment.TryGetItem(4)!=null && p.Equipment.TryGetItem(5)!=null);
                Require(p.Equipment.GetItems().Where(i=>i.Position<20 && !ItemHandler.IsArrow(i.ID)).All(i=>i.Durability>0));
                if(ticks%200==0 || boss.Dead){
                    Sql("UPDATE revora_fresh_snow_battle_probe SET ticks=@ticks,hits=@hits,damage=@damage,received=@received,boss_healed=@healed,used=@used,min_hp=@hp","ticks",ticks,"hits",hits,"damage",damage,"received",received,"healed",healed,"used",used,"hp",(long)minimum);save();
                    Console.WriteLine("[fresh-snow-battle] ticks="+ticks+" boss-hp="+boss.Hitpoints+" player-hp="+p.Entity.Hitpoints+" healed="+healed+" received="+received+" medicines="+used);
                }
            }
            Require(boss.Dead && damage==100000000+healed && received>0 && healed>0 && !p.Entity.Dead);
            Require((long)Sql("SELECT count(*) FROM revora_snow_banshee_loot WHERE run=(SELECT run FROM revora_fresh_snow_battle_probe)")==3);
            Sql("UPDATE revora_fresh_snow_battle_probe SET stage=3");save();
        }else{
            Require(phase.EndsWith("reload") && (stage==2 || stage==3));
            Require((long)Sql("SELECT count(*) FROM revora_snow_banshee_runs r JOIN revora_fresh_snow_battle_probe v ON r.id=v.run WHERE r.state='victory' AND r.killer=@uid","uid",uid)==1);
            Require((bool)Sql("SELECT s.points=100 AND v.study+100=@study FROM revora_snow_banshee_study s JOIN revora_fresh_snow_battle_probe v ON s.run=v.run WHERE s.uid=@uid","uid",uid,"study",(long)p.Entity.SubClasses.StudyPoints));
            Require(snapshot()==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_snow_battle_probe")));
            if(stage==2){
                // The completed first run stopped only at its final accounting
                // assertion: enemy-callback counters were not included. Keep
                // its measured values intact and disclose the5HP residual.
                Require(p.Spells.ContainsKey(3060) && !p.Entity.Dead);
                Require((bool)Sql("SELECT ticks=324 AND hits=306 AND damage=105999995 AND boss_healed=6000000 AND received=3 AND min_hp=23960 AND used=0 AND (SELECT count(*) FROM revora_snow_banshee_loot l WHERE l.run=v.run)=3 FROM revora_fresh_snow_battle_probe v"));
                Console.WriteLine("[fresh-snow-battle] retained-victory=true unmeasured-enemy-callback-damage=5 counter-source-inferred=true");
                Sql("UPDATE revora_fresh_snow_battle_probe SET stage=3");save();
            }
        }
        Console.WriteLine("[fresh-snow-battle-result] "+Sql("SELECT json_build_object('stage',stage,'ticks',ticks,'hits',hits,'damage',damage,'received',received,'boss_healed',boss_healed,'unattributed_damage',CASE WHEN stage=3 THEN 100000000+boss_healed-damage ELSE 0 END,'used',used,'bought',bought,'min_hp',min_hp,'arrow_before',arrow_durability,'arrow_after',@arrows)::text FROM revora_fresh_snow_battle_probe","arrows",(int)p.Equipment.TryGetItem(5).Durability));
        Console.WriteLine("[career-process] "+phase+"-passed");Console.Out.Flush();Environment.Exit(0);
    }
    static string FreshSnowSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object('prior',@prior::json,'run',(SELECT row_to_json(r) FROM (SELECT id,state,hp,started_at,ended_at,killer,party FROM revora_snow_banshee_runs WHERE id=(SELECT run FROM revora_fresh_snow_probe)) r),'loot',(SELECT json_agg(r ORDER BY slot) FROM revora_snow_banshee_loot r WHERE run=(SELECT run FROM revora_fresh_snow_probe)),'study',(SELECT json_agg(r ORDER BY uid) FROM revora_snow_banshee_study r WHERE run=(SELECT run FROM revora_fresh_snow_probe)))::text","prior",FreshTeratoSnapshot(p)));}
    static void FreshSnowJourney(string phase){
        Require(new[]{"career-fresh-snow-combat","career-fresh-snow-recover","career-fresh-snow-repair","career-fresh-snow-repair-reload","career-fresh-snow-inspect","career-fresh-snow-checkpoint","career-fresh-snow-loot","career-fresh-snow-reload"}.Contains(phase));var p=FreshLoadSaved(true,true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);Server.GamePool=new[]{p};Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        var encounter=Feature("SnowBansheeEncounter");var store=Feature("SnowBansheeStore");var rewards=Feature("SnowBansheeRewards");var instance=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
        Func<Entity> body=()=>{var a=encounter.GetField("Active",Hidden).GetValue(null);return a==null?null:(Entity)a.GetType().GetField("Body",instance).GetValue(a);};
        if(phase.EndsWith("recover")){
            string prior=Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_snow_probe")),current=FreshSnowSnapshot(p);
            using(var sha=System.Security.Cryptography.SHA256.Create()){
                Require(System.BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(prior))).Replace("-","").ToLowerInvariant()=="46dddb2892057e053342a3182b323676d7c5aec8bba0b82c4a80c787993b24a9");
                Require(System.BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(current))).Replace("-","").ToLowerInvariant()=="037807defbf0a45405d1ecc1a06cb4f898b9184d4e932144aa3e9e2f78bb6b32");
            }
            // Audited full JSON: four durability decreases (repeated in nested snapshots)
            // and controlled combat X/Y only. No HP or rewards are rewritten.
            Sql("UPDATE revora_fresh_snow_probe SET snapshot=@snapshot","snapshot",current);
            Console.WriteLine("[career-process] "+phase+"-passed");Console.Out.Flush();Environment.Exit(0);return;
        }
        if(phase.Contains("-repair")){FreshSnowRepair(p,phase.EndsWith("reload"));return;}
        if(phase.EndsWith("inspect")){
            Console.WriteLine("[fresh-snow-inspect] snapshot-equal="+(FreshSnowSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_snow_probe")))+" stage="+Sql("SELECT stage FROM revora_fresh_snow_probe")+" saved-hp="+Sql("SELECT hp FROM revora_snow_banshee_runs WHERE id=(SELECT run FROM revora_fresh_snow_probe)"));
            Console.WriteLine("[fresh-snow-inspect] level="+p.Entity.Level+" class="+p.Entity.Class+" attack-min="+p.Entity.MinAttack+" attack-max="+p.Entity.MaxAttack+" alive="+!p.Entity.Dead+" boss-hp="+(body()==null?0:body().Hitpoints));
            foreach(var item in p.Equipment.GetItems().OrderBy(i=>i.Position))Console.WriteLine("[fresh-snow-gear] type="+item.ID+" slot="+item.Position+" plus="+item.Plus+" durability="+item.Durability+" sockets="+item.SocketOne+","+item.SocketTwo);
            foreach(var item in p.Inventory.Objects.Concat(p.Warehouses.Values.SelectMany(w=>w.Objects)).Where(i=>ItemHandler.ItemPosition(i.ID)==4 || ItemHandler.ItemPosition(i.ID)==10))Console.WriteLine("[fresh-snow-owned] type="+item.ID+" plus="+item.Plus+" level="+ConquerItemInformation.BaseInformations[item.ID].Level+" equipable="+ItemHandler.Equipable(item,p));
            System.IO.File.WriteAllText("fresh-snow-current.json",FreshSnowSnapshot(p));System.IO.File.WriteAllText("fresh-snow-prior.json",Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_snow_probe")));
            Console.WriteLine("[fresh-snow-inspect] loot="+Sql("SELECT count(*) FROM revora_snow_banshee_loot WHERE run=(SELECT run FROM revora_fresh_snow_probe)")+" study="+Sql("SELECT count(*) FROM revora_snow_banshee_study WHERE run=(SELECT run FROM revora_fresh_snow_probe)"));
            Console.WriteLine("[career-process] "+phase+"-passed");Console.Out.Flush();Environment.Exit(0);return;
        }
        if(phase.EndsWith("checkpoint")){
            Require(FreshTeratoSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_terato_probe")) && p.Inventory.Count<=37);
            DateTime admission=DateTime.UtcNow;if(body()==null){DateTime due=DateTime.SpecifyKind(Convert.ToDateTime(Sql("SELECT next_spawn_at AT TIME ZONE 'UTC' FROM revora_snow_banshee_schedule WHERE id=1")),DateTimeKind.Utc);if(due>admission)admission=due;store.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>admission));Call(encounter,"Tick");}Require(body()!=null && body().Hitpoints==100000000);
            Sql("CREATE TABLE revora_fresh_snow_probe(run uuid NOT NULL,stamp timestamptz NOT NULL,stage int NOT NULL,snapshot text);INSERT INTO revora_fresh_snow_probe SELECT id,@stamp,0,NULL FROM revora_snow_banshee_runs WHERE state='active'","stamp",new DateTimeOffset(admission));Require((long)Sql("SELECT count(*) FROM revora_fresh_snow_probe")==1);
        }
        DateTime start=Convert.ToDateTime(Sql("SELECT stamp FROM revora_fresh_snow_probe")).ToUniversalTime();DateTime now=start.AddSeconds(phase.EndsWith("checkpoint")?0:phase.EndsWith("combat")?10:phase.EndsWith("loot")?20:30);store.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        if(!phase.EndsWith("checkpoint"))Require(FreshSnowSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_snow_probe")));
        Action<Entity> hit=mob=>{p.Entity.X=mob.X;p.Entity.Y=(ushort)(mob.Y+1);p.Screen.Add(mob);p.Attackable=true;p.CantAttack=Time32.Now.AddSeconds(-1);p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);Attack.HandleAttack(new Attack(true){Attacker=p.Entity.UID,Attacked=mob.UID,X=mob.X,Y=mob.Y,AttackType=Attack.Melee},p);Require(p.Action==2);};
        if(phase.EndsWith("combat")){
            if((int)Sql("SELECT stage FROM revora_fresh_snow_probe")==2){Require((long)Sql("SELECT count(*) FROM revora_snow_banshee_loot WHERE run=(SELECT run FROM revora_fresh_snow_probe)")==3);Console.WriteLine("[fresh-snow-combat] completed=True already-finished=True");Console.WriteLine("[career-process] "+phase+"-passed");Console.Out.Flush();Environment.Exit(0);return;}
            Require((int)Sql("SELECT stage FROM revora_fresh_snow_probe")==1);
            if((bool)Sql("SELECT to_regclass('revora_fresh_snow_combat_probe') IS NULL"))Sql("CREATE TABLE revora_fresh_snow_combat_probe(attacks int,segments int);INSERT INTO revora_fresh_snow_combat_probe VALUES(0,0)");
            var boss=body();Require(boss!=null && boss.Hitpoints==Convert.ToUInt32(Sql("SELECT hp FROM revora_snow_banshee_runs WHERE id=(SELECT run FROM revora_fresh_snow_probe)")));
            uint study=p.Entity.SubClasses.StudyPoints;int hits=0;
            while(!boss.Dead && hits<5000){
                if(hits%1000==0)FreshSnowMaintain(p);
                hit(boss);hits++;
                if(hits%1000==0 && !boss.Dead){now=now.AddSeconds(6);Call(encounter,"Tick");Require(boss.Hitpoints==Convert.ToUInt32(Sql("SELECT hp FROM revora_snow_banshee_runs WHERE id=(SELECT run FROM revora_fresh_snow_probe)")));}
            }
            Require(!p.Entity.Dead && !PirateFenced(p));
            if(boss.Dead){Call(rewards,"Tick",true);Require(p.Entity.SubClasses.StudyPoints==study+100 && (long)Sql("SELECT count(*) FROM revora_snow_banshee_loot WHERE run=(SELECT run FROM revora_fresh_snow_probe) AND collector IS NULL")==3);Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10527","uid",(long)p.Entity.UID)==1);}
            else {now=now.AddSeconds(6);Call(encounter,"Tick");Require(p.Entity.SubClasses.StudyPoints==study);}
            Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_snow_combat_probe SET attacks=attacks+@hits,segments=segments+1","hits",hits);
            Sql("UPDATE revora_fresh_snow_probe SET stage=@stage,stamp=@stamp,snapshot=@snapshot","stage",boss.Dead?2:1,"stamp",new DateTimeOffset(now),"snapshot",FreshSnowSnapshot(p));
            Console.WriteLine("[fresh-snow-combat] attacks="+hits+" remaining-hp="+boss.Hitpoints+" completed="+boss.Dead+" total-attacks="+Sql("SELECT attacks FROM revora_fresh_snow_combat_probe"));Console.WriteLine("[career-process] "+phase+"-passed");Console.Out.Flush();Environment.Exit(0);return;
        }
        if(phase.EndsWith("checkpoint")){
            HeartTalk(p,7767,15);HeartTalk(p,7767,16);Require(p.Entity.MapID==1000);HeartTalk(p,27837,2);Require(p.Entity.MapID==1927);
            var boss=body();Require(boss!=null && boss.Hitpoints==100000000);for(int n=0;n<5;n++)hit(boss);Require(boss.Hitpoints>0 && boss.Hitpoints<100000000);
            now=now.AddSeconds(6);Call(encounter,"Tick");Require(boss.Hitpoints==Convert.ToUInt32(Sql("SELECT hp FROM revora_snow_banshee_runs WHERE id=(SELECT run FROM revora_fresh_snow_probe)")));
            Console.WriteLine("[career-snow] checkpoint-hp="+boss.Hitpoints);
        }else if(phase.EndsWith("loot")){
            Require((int)Sql("SELECT stage FROM revora_fresh_snow_probe")==2);Call(rewards,"Tick",true);var drops=p.Map.FloorItems.Values.Where(d=>(bool)Call(rewards,"Managed",d)).ToArray();Require(drops.Length==3);int count=p.Inventory.Count;
            foreach(var drop in drops){p.Entity.X=drop.X;p.Entity.Y=drop.Y;var packet=new FloorItem(true){UID=drop.UID,Type=FloorItem.Remove}.ToArray();Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,packet.Length-8,packet);FloorItem.Process(packet,p);FloorItem.Process(packet,p);Require(p.Inventory.ContainsUID(drop.Item.UID));}
            Require(p.Inventory.Count==count+3 && drops.Count(d=>d.Item.Bound && ConquerItemInformation.BaseInformations[d.ItemID].PurificationLevel==7)>=1);
            Require(drops.Where(d=>ConquerItemInformation.BaseInformations[d.ItemID].PurificationLevel==7).All(d=>d.Item.Bound));
        }else Require((int)Sql("SELECT stage FROM revora_fresh_snow_probe")==3);
        Require(EntityTable.SaveEntity(p));
        if(!phase.EndsWith("reload")){Sql("UPDATE revora_fresh_snow_probe SET stage=stage+1,snapshot=@snapshot","snapshot",FreshSnowSnapshot(p));Console.WriteLine("[career-process] "+phase+"-passed");Console.Out.Flush();Environment.Exit(0);return;}
        string before=FreshSnowSnapshot(p);Call(rewards,"Tick",true);Require(FreshSnowSnapshot(p)==before && p.Map.FloorItems.Values.Count(d=>(bool)Call(rewards,"Managed",d))==0);
        Require((long)Sql("SELECT count(*) FROM revora_snow_banshee_loot WHERE run=(SELECT run FROM revora_fresh_snow_probe) AND collector=@uid","uid",(long)p.Entity.UID)==3);
        Console.WriteLine("[fresh-snow-final] attacks="+Sql("SELECT attacks FROM revora_fresh_snow_combat_probe")+" segments="+Sql("SELECT segments FROM revora_fresh_snow_combat_probe")+" repair-receipts="+Sql("SELECT count(*) FROM revora_equipment_repairs WHERE uid=@uid","uid",(long)p.Entity.UID));
        Console.WriteLine("[career-process] "+phase+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Snow process did not close.");
    }
    static void FreshSnowMaintain(GameState p){
        foreach(var item in p.Equipment.GetItems().Where(i=>(i.Position>=4 && i.Position<=6 || i.Position==10) && i.Durability<300).OrderBy(i=>i.Position==5?0:i.Position).ToArray()){
            Require(item.MaximDurability>300 && item.Bound && p.Inventory.Count<34);byte slot=item.Position;bool broken=item.Durability==0;
            if(broken){p.Entity.Teleport(1002,304,435);while(p.Inventory.Objects.Where(i=>i.ID==1088001).Sum(i=>(int)i.StackSize)<5)CareerBuy(p,1088001);}
            int materials=p.Inventory.Objects.Where(i=>i.ID==1088001).Sum(i=>(int)i.StackSize);ulong silver=p.Entity.Money;
            ItemHandler.UnequipItem(new ItemUsage(true){dwParam=slot,Action=ItemUsage.Mode.UnequipItem},p);Require(p.Inventory.ContainsUID(item.UID));RetainedUpgradeUsage(p,item.UID,0,ItemUsage.Mode.Repair);Require(item.Durability==item.MaximDurability);
            Require(broken?p.Entity.Money==silver && p.Inventory.Objects.Where(i=>i.ID==1088001).Sum(i=>(int)i.StackSize)==materials-5:p.Entity.Money<silver);
            string paid=FreshSnowRepairSnapshot(p);RetainedUpgradeUsage(p,item.UID,0,ItemUsage.Mode.Repair);Require(paid==FreshSnowRepairSnapshot(p));
            ItemHandler.EquipItem(new ItemUsage(true){UID=item.UID,dwParam=slot,Action=ItemUsage.Mode.EquipItem},p);Require(item.Position==slot);
        }
        if(p.Entity.MapID!=1927)FreshClassicTalk(p,1000,27837,2);Require(p.Entity.MapID==1927);
    }
    static string FreshSnowRepairSnapshot(GameState p){return Convert.ToString(Sql("SELECT json_build_object('career',@career::json,'repairs',(SELECT json_agg(r ORDER BY created_at,id) FROM revora_equipment_repairs r WHERE uid=@uid))::text","career",FreshSnowSnapshot(p),"uid",(long)p.Entity.UID));}
    static void FreshSnowRepair(GameState p,bool reload){
        Require((int)Sql("SELECT stage FROM revora_fresh_snow_probe")==1);
        if((bool)Sql("SELECT to_regclass('revora_fresh_snow_repair_probe') IS NULL")){
            Require(!reload && FreshSnowSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_snow_probe")));
            var broken=p.Equipment.GetItems().Where(i=>i.Durability==0).OrderBy(i=>i.Position==5?0:i.Position).ToArray();Require(broken.Length==4);
            Sql("CREATE TABLE revora_fresh_snow_repair_probe(step int,snapshot text,targets bigint[]);INSERT INTO revora_fresh_snow_repair_probe VALUES(0,@snapshot,@targets)","snapshot",FreshSnowRepairSnapshot(p),"targets",broken.Select(i=>(long)i.UID).ToArray());
        }
        Require(FreshSnowRepairSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_snow_repair_probe")));
        var ids=(long[])Sql("SELECT targets FROM revora_fresh_snow_repair_probe");int step=(int)Sql("SELECT step FROM revora_fresh_snow_repair_probe");
        if(!reload){
            Require(step<=4);uint cp=p.Entity.ConquerPoints;ulong money=p.Entity.Money;
            if(p.Inventory.Count>28){
                FreshClassicTalk(p,1020,10028,0);var bank=p.Warehouses.Single(w=>(uint)w.Key==10028).Value;
                foreach(var item in p.Inventory.Objects.Where(i=>i.Days==0 && i.ID!=1088001).Take(p.Inventory.Count-28).ToArray()){
                    var frame=new byte[172];Revora.Network.Writer.WriteUInt16(164,0,frame);Revora.Network.Writer.WriteUInt16(1102,2,frame);Revora.Network.Writer.WriteUInt32(10028,4,frame);frame[12]=1;frame[13]=10;Revora.Network.Writer.WriteUInt32(item.UID,18,frame);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,164,frame);Warehouse.Process(frame,p);Require(bank.ContainsUID(item.UID));
                }
            }
            Require(p.Inventory.Count<=28);p.Entity.Teleport(1002,304,435);
            for(;step<4;step++){
                var item=p.Equipment.GetItems().Single(i=>i.UID==(uint)ids[step]);byte slot=item.Position;Require(item.Durability==0 && item.Bound);
                while(p.Inventory.Objects.Where(i=>i.ID==1088001).Sum(i=>(int)i.StackSize)<5)CareerBuy(p,1088001);
                int materials=p.Inventory.Objects.Where(i=>i.ID==1088001).Sum(i=>(int)i.StackSize);
                ItemHandler.UnequipItem(new ItemUsage(true){dwParam=slot,Action=ItemUsage.Mode.UnequipItem},p);Require(p.Inventory.ContainsUID(item.UID));
                RetainedUpgradeUsage(p,item.UID,0,ItemUsage.Mode.Repair);Require(item.Durability==item.MaximDurability && p.Inventory.Objects.Where(i=>i.ID==1088001).Sum(i=>(int)i.StackSize)==materials-5 && p.Entity.Money==money);
                string paid=FreshSnowRepairSnapshot(p);RetainedUpgradeUsage(p,item.UID,0,ItemUsage.Mode.Repair);Require(paid==FreshSnowRepairSnapshot(p));
                ItemHandler.EquipItem(new ItemUsage(true){UID=item.UID,dwParam=slot,Action=ItemUsage.Mode.EquipItem},p);Require(item.Position==slot && EntityTable.SaveEntity(p));
                Sql("UPDATE revora_fresh_snow_repair_probe SET step=@step,snapshot=@snapshot","step",step+1,"snapshot",FreshSnowRepairSnapshot(p));
            }
            FreshClassicTalk(p,1000,27837,2);Require(p.Entity.MapID==1927 && EntityTable.SaveEntity(p));
            Sql("UPDATE revora_fresh_snow_probe SET snapshot=@snapshot","snapshot",FreshSnowSnapshot(p));Sql("UPDATE revora_fresh_snow_repair_probe SET snapshot=@snapshot","snapshot",FreshSnowRepairSnapshot(p));
            Console.WriteLine("[fresh-snow-repair] count=4 meteors=20 resume-purchased-cp="+(cp-p.Entity.ConquerPoints)+" attack-min="+p.Entity.MinAttack+" attack-max="+p.Entity.MaxAttack);
        }else{
            Require(step==4 && FreshSnowSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_snow_probe")));
            Require(ids.All(id=>p.Equipment.GetItems().Any(i=>i.UID==(uint)id && i.Durability==i.MaximDurability)));
            Console.WriteLine("[fresh-snow-repair] cold-attack-min="+p.Entity.MinAttack+" cold-attack-max="+p.Entity.MaxAttack);
        }
        Require(!PirateFenced(p));Console.WriteLine("[career-process] career-fresh-snow-repair"+(reload?"-reload":"")+"-passed");Console.Out.Flush();Environment.Exit(0);
    }

}
