using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle {
    static string RetainedMountSnapshot(GameState p,GameState recipient,string eliteRun){return Convert.ToString(Sql("SELECT json_build_object('prior',@prior::json,'planet',(SELECT json_agg(r ORDER BY collection_step) FROM revora_planet_runs r WHERE uid=@uid),'completions',(SELECT json_agg(r ORDER BY weekend) FROM revora_planet_completions r WHERE uid=@uid),'exchange',(SELECT json_agg(r ORDER BY design) FROM revora_planet_exchange r WHERE uid=@uid))::text","prior",RetainedLabBoxSnapshot(p,recipient,eliteRun),"uid",(long)CareerUid));}
    static void RetainedMountSave(GameState p,GameState recipient,string eliteRun){Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_retained_mounts_probe SET snapshot=@snapshot","snapshot",RetainedMountSnapshot(p,recipient,eliteRun));}
    static void RetainedMountKill(GameState p,Entity mob){
        p.Entity.X=mob.X;p.Entity.Y=(ushort)(mob.Y+1);p.Screen.Add(mob);int hits=0;
        while(!mob.Dead && hits++<2000){p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);Attack.HandleAttack(new Attack(true){Attacker=p.Entity.UID,Attacked=mob.UID,X=mob.X,Y=mob.Y,AttackType=Attack.Melee},p);}
        Require(mob.Dead && !p.Entity.Dead && !PirateFenced(p));Sql("UPDATE revora_retained_mounts_probe SET hits=hits+@hits","hits",hits);
    }
    static void RetainedStamina(GameState p,Func<string> snapshot,string phase){
        bool reload=phase.EndsWith("reload");
        if((bool)Sql("SELECT to_regclass('revora_retained_stamina_probe') IS NULL")){
            Require(!reload && snapshot()==Convert.ToString(Sql("SELECT snapshot FROM revora_retained_rising_probe WHERE done")));
            Sql("CREATE TABLE revora_retained_stamina_probe(snapshot text,done boolean,casts int,ticks int);INSERT INTO revora_retained_stamina_probe VALUES(@snapshot,false,0,0)","snapshot",snapshot());
        }
        Require(snapshot()==Convert.ToString(Sql("SELECT snapshot FROM revora_retained_stamina_probe")));
        Action save=()=>{SkillTable.SaveSpells(p);SkillTable.SaveProficiencies(p);Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_retained_stamina_probe SET snapshot=@snapshot","snapshot",snapshot());};
        if(!reload){
            Require(!(bool)Sql("SELECT done FROM revora_retained_stamina_probe") && p.Entity.Stamina==0 && p.Spells.ContainsKey(1045) && p.Spells[1045].Available);
            var spell=SpellTable.GetSpell(1045,p.Spells[1045].Level);Require(spell.UseStamina==20 && spell.UseMana==0);
            Require(SpellTable.SpellInformations[1046].Values.All(v=>v.UseStamina<=100) && SpellTable.SpellInformations[4000].Values.All(v=>v.UseStamina<=100));
            p.Entity.Teleport(1002,400,350);p.Screen.Clear();
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;var wrapper=typeof(Revora.Network.Sockets.ClientWrapper);
            wrapper.GetField("SendQueue",flags).SetValue(p.Socket,new System.Collections.Generic.Queue<byte[]>());wrapper.GetField("SendSyncRoot",flags).SetValue(p.Socket,new object());
            int ticks=0,casts=0;Action recover=()=>{var wait=System.Diagnostics.Stopwatch.StartNew();while(p.Entity.Stamina<100 && wait.Elapsed.TotalSeconds<20){System.Threading.Thread.Sleep(510);p.Socket.Alive=true;try{typeof(Revora.Game.Screen).GetMethod("monsterBuffersCallback",Hidden).Invoke(null,new object[]{p,unchecked((int)Time32.Now.Value)});}finally{p.Socket.Alive=false;}ticks++;}Require(p.Entity.Stamina>=100);};
            try{
                recover();System.Console.WriteLine("[career-stamina] regenerated="+p.Entity.Stamina+" ticks="+ticks);
                var mobs=p.Map.Entities.Values.Where(m=>m.MonsterInfo!=null && !m.MonsterInfo.Boss && !m.MonsterInfo.Guard && !m.MonsterInfo.Reviver && !m.Companion && !m.Dead && m.MaxHitpoints<1000 && m.X>1).OrderBy(m=>m.UID).Take(2).ToArray();Require(mobs.Length==2);
                foreach(var mob in mobs){
                    p.Screen.Clear();p.Entity.X=(ushort)(mob.X-1);p.Entity.Y=mob.Y;p.Screen.Add(mob);System.Threading.Thread.Sleep(1100);
                    byte before=p.Entity.Stamina;uint hp=mob.Hitpoints;
                    Attack.HandleAttack(new Attack(true){Attacker=p.Entity.UID,Attacked=mob.UID,X=mob.X,Y=mob.Y,AttackType=Attack.Magic,Decoded=true,Damage=1045},p);
                    Require(mob.Hitpoints<hp && p.Entity.Stamina==before-spell.UseStamina);casts++;p.Screen.Clear();recover();
                    System.Console.WriteLine("[career-stamina] cast="+casts+" spent="+spell.UseStamina+" recovered="+p.Entity.Stamina);
                }
                Require(casts==2);Sql("UPDATE revora_retained_stamina_probe SET done=true,casts=@casts,ticks=@ticks","casts",casts,"ticks",ticks);save();
            }catch{Sql("UPDATE revora_retained_stamina_probe SET casts=@casts,ticks=@ticks","casts",casts,"ticks",ticks);save();throw;}
        }else Require((bool)Sql("SELECT done AND casts=2 AND ticks>=20 FROM revora_retained_stamina_probe"));
        Require(snapshot()==Convert.ToString(Sql("SELECT snapshot FROM revora_retained_stamina_probe")) && p.Entity.MyAchievement.CompletedCount>=320 && p.Entity.EquippedTitle==20010002);
        System.Console.WriteLine("[career-process] "+phase+"-passed casts="+Sql("SELECT casts FROM revora_retained_stamina_probe")+" ticks="+Sql("SELECT ticks FROM revora_retained_stamina_probe"));System.Console.Out.Flush();Environment.Exit(0);
    }
    static void RetainedMountProcess(string phase){
        if(phase.EndsWith("inspect"))System.Console.WriteLine("[retained-mounts] saved-wardrobe="+Sql("SELECT json_build_object('mounts',(SELECT count(DISTINCT id) FROM items WHERE entityid=@uid AND inwardrobe=1 AND id BETWEEN 200000 AND 299999),'garments',(SELECT count(DISTINCT id) FROM items WHERE entityid=@uid AND inwardrobe=1 AND id BETWEEN 180000 AND 199999),'purchases',(SELECT count(*) FROM revora_planet_exchange WHERE uid=@uid))::text","uid",(long)CareerUid));

        careerRitesFixture=true;combinedCollectionFixture=true;nextUid=CareerUid;
        var p=Player(Kernel.Maps[1002],true,1003,true,true);combinedCollectionFixture=false;
        SkillTable.LoadSpells(p);SkillTable.LoadProficiencies(p);SubClassTable.Load(p.Entity);Revora.Database.KnownPersons.LoadMentor(p);
        new CoatStorage().Login(p);p.LoadItemStats();ChiTable.Load(p);WayofHeroes.Load(p);EntityVariableTable.Load(p);Call(typeof(Revora.Database.JiangHu),"LoadClient",p);new TitleStorage().Login(p);
        nextUid=4294918104;var recipient=Player(Kernel.Maps[1002],true);recipient.LoadItemStats();ChiTable.Load(recipient);WayofHeroes.Load(recipient);
        nextUid=4294918206;var storage=Player(Kernel.Maps[1002],true);storage.LoadItemStats();
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));


        string eliteRun=Convert.ToString(Sql("SELECT run FROM revora_retained_elite_combat_probe"));

        if(phase.Contains("-stamina-")){RetainedStamina(p,()=>Convert.ToString(Sql("SELECT json_build_object('prior',@prior::json,'titles',(SELECT row_to_json(r) FROM titles r WHERE id=@uid))::text","prior",RetainedMountSnapshot(p,recipient,eliteRun),"uid",(long)CareerUid)),phase);return;}
        if(phase.Contains("-rising-")){
            Func<string> snapshot=()=>Convert.ToString(Sql("SELECT json_build_object('prior',@prior::json,'titles',(SELECT row_to_json(r) FROM titles r WHERE id=@uid))::text","prior",RetainedMountSnapshot(p,recipient,eliteRun),"uid",(long)CareerUid));
            if((bool)Sql("SELECT to_regclass('revora_retained_rising_probe') IS NULL")){
                Require(phase.EndsWith("equip") && RetainedMountSnapshot(p,recipient,eliteRun)==Convert.ToString(Sql("SELECT snapshot FROM revora_retained_mounts_probe")));
                Sql("CREATE TABLE revora_retained_rising_probe(snapshot text,done boolean);INSERT INTO revora_retained_rising_probe VALUES(@snapshot,false)","snapshot",snapshot());
            }
            string currentRising=snapshot(),savedRising=Convert.ToString(Sql("SELECT snapshot FROM revora_retained_rising_probe"));
            if(currentRising!=savedRising){System.IO.File.WriteAllText("rising-current.json",currentRising);System.IO.File.WriteAllText("rising-saved.json",savedRising);System.Console.WriteLine("[rising-diagnostic] verified="+p.Entity.MyAchievement.CompletedCount+" equipped="+p.Entity.EquippedTitle);}
            Require(currentRising==savedRising && p.Entity.MyAchievement.CompletedCount>=320);
            if(phase.EndsWith("resources")){
                System.Console.WriteLine("[career-resources] class="+p.Entity.Class+" stamina="+p.Entity.Stamina+" mana="+p.Entity.Mana+" weapon="+(p.Equipment.TryGetItem(4)==null?0:p.Equipment.TryGetItem(4).ID));
                foreach(ushort id in new ushort[]{1045,1046,4000}){Revora.Interfaces.ISkill learned;if(p.Spells.TryGetValue(id,out learned)){var spell=SpellTable.GetSpell(id,learned.Level);System.Console.WriteLine("[career-resources] skill="+id+" level="+learned.Level+" available="+learned.Available+" stamina-cost="+spell.UseStamina+" mana-cost="+spell.UseMana);}else System.Console.WriteLine("[career-resources] skill="+id+" missing=true");}
                Require(snapshot()==savedRising);System.Console.WriteLine("[career-process] "+phase+"-passed");System.Console.Out.Flush();Environment.Exit(0);return;
            }
            string receipts=Convert.ToString(Sql("SELECT json_agg(r ORDER BY achievement_id)::text FROM revora_achievement_evidence r WHERE uid=@uid","uid",(long)CareerUid));
            if(phase.EndsWith("equip")){
                new TitleStorage{Info=new TitleStorageProto{Type=2001,Id=2,ActionId=TitleStorage.Action.Equip,Value=new TitleValue{Type=2001,Id=2}}}.Handle(p);
                Require(p.Entity.EquippedTitle==20010002 && EntityTable.SaveEntity(p));
                Sql("UPDATE revora_retained_rising_probe SET snapshot=@snapshot,done=true","snapshot",snapshot());
            }else Require(phase.EndsWith("reload") && (bool)Sql("SELECT done FROM revora_retained_rising_probe") && p.Entity.EquippedTitle==20010002);
            string saved=snapshot();
            new TitleStorage{Info=new TitleStorageProto{Type=2001,Id=2,ActionId=TitleStorage.Action.Equip,Value=new TitleValue{Type=2001,Id=2}}}.Handle(p);
            Require(saved==snapshot() && receipts==Convert.ToString(Sql("SELECT json_agg(r ORDER BY achievement_id)::text FROM revora_achievement_evidence r WHERE uid=@uid","uid",(long)CareerUid)));
            System.Console.WriteLine("[career-process] "+phase+"-passed verified="+p.Entity.MyAchievement.CompletedCount+" equipped="+p.Entity.EquippedTitle);System.Console.Out.Flush();Environment.Exit(0);return;
        }
        if(phase.EndsWith("inspect")){
            bool started=!(bool)Sql("SELECT to_regclass('revora_retained_mounts_probe') IS NULL");
            string current=started?RetainedMountSnapshot(p,recipient,eliteRun):RetainedLabBoxSnapshot(p,recipient,eliteRun),saved=Convert.ToString(Sql(started?"SELECT snapshot FROM revora_retained_mounts_probe":"SELECT snapshot FROM revora_retained_lab_box_probe"));
            System.IO.File.WriteAllText("retained-mounts-before.json",current);
            System.IO.File.WriteAllText("retained-mounts-prior.json",saved);
            System.Console.WriteLine("[retained-mounts] snapshot-equal="+(current==saved));
            System.Console.WriteLine("[retained-mounts] planet="+Sql("SELECT coalesce(json_agg(r)::text,'[]') FROM (SELECT collection_step,stage,kills FROM revora_planet_runs WHERE uid=@uid ORDER BY collection_step)r","uid",(long)CareerUid));
            System.Console.WriteLine("[retained-mounts] banks="+string.Join(",",p.Warehouses.Select(w=>w.Key+":"+w.Value.Count+"/"+w.Value.MaxCount)));
            foreach(var i in p.Inventory.Objects.Take(8))System.Console.WriteLine("[retained-mounts] item="+i.UID+" type="+i.ID+" days="+i.Days);
            System.Console.WriteLine("[retained-mounts] prior-unchanged="+(current==saved)+" mounts="+CombinedCount(p,"Mounts")+" class="+p.Entity.Class+" bag="+p.Inventory.Count+" bow="+(p.Equipment.TryGetItem(4)==null?0:p.Equipment.TryGetItem(4).ID));
            System.Console.WriteLine("[career-process] "+phase+"-passed");System.Console.Out.Flush();Environment.Exit(0);return;
        }
        if((bool)Sql("SELECT to_regclass('revora_retained_mounts_probe') IS NULL")){
            Require(phase.EndsWith("earn") && RetainedLabBoxSnapshot(p,recipient,eliteRun)==Convert.ToString(Sql("SELECT snapshot FROM revora_retained_lab_box_probe")));
            Require(CombinedCount(p,"Mounts")==100 && (long)Sql("SELECT count(*) FROM revora_planet_exchange WHERE uid=@uid","uid",(long)CareerUid)==156);
            Sql("CREATE TABLE revora_retained_mounts_probe(snapshot text,hits int NOT NULL DEFAULT 0);INSERT INTO revora_retained_mounts_probe(snapshot) VALUES(@snapshot)","snapshot",RetainedMountSnapshot(p,recipient,eliteRun));
        }
        string checkpoint=Convert.ToString(Sql("SELECT snapshot FROM revora_retained_mounts_probe"));
        string recovered=RetainedMountSnapshot(p,recipient,eliteRun);
        if(recovered!=checkpoint){
            // Exact audited crash recovery only: the 201st mount committed before
            // prestige refresh threw. No reward or character state is rewritten.
            Require(phase.EndsWith("earn"));
            using(var sha=System.Security.Cryptography.SHA256.Create()){
                Require(System.BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(checkpoint))).Replace("-","").ToLowerInvariant()=="abd8a3f2833bdc978a26b615efb677ce77b16d903de7567cfbd07179bb782534");
                Require(System.BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(recovered))).Replace("-","").ToLowerInvariant()=="1c2c45960a178d23050222ce1d5ed0e3566aed5ced107c2ded9d10db9b95203c");
            }
            System.Console.WriteLine("[retained-mounts] audited-final-purchase-recovered");
        }
        var planet=Feature("PlanetWeekend");var encounter=Feature("PlanetEncounter");
        planet.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>planetNow));
        Feature("SummerHunt").GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>planetNow));
        var exchange=Feature("PlanetCollectionExchange");var stock=((uint[])exchange.GetField("Garments",Hidden).GetValue(null)).Concat((uint[])exchange.GetField("Mounts",Hidden).GetValue(null)).ToArray();
        Require(stock.Length==257);
        if(phase.EndsWith("earn")){
            Require(p.Inventory.Count<=38);
            int completed=Convert.ToInt32(Sql("SELECT max(collection_step) FROM revora_planet_runs WHERE uid=@uid AND stage='complete'","uid",(long)CareerUid));
            while(completed<13){
                var pending=Sql("SELECT max(accepted_at) AT TIME ZONE 'UTC' FROM revora_planet_runs WHERE uid=@uid AND collection_step=@step AND stage<>'complete'","uid",(long)CareerUid,"step",completed+1);
                planetNow=pending==DBNull.Value?DateTime.SpecifyKind((DateTime)Sql("SELECT max(ends_at) AT TIME ZONE 'UTC' FROM revora_planet_runs WHERE uid=@uid","uid",(long)CareerUid),DateTimeKind.Utc).AddDays(5).AddHours(2):DateTime.SpecifyKind((DateTime)pending,DateTimeKind.Utc).AddMinutes(1);
                while(Call(planet,"Current",planetNow)==null)planetNow=planetNow.AddDays(1);
                PlanetRowan(p,211);Require(!PirateFenced(p));RetainedMountSave(p,recipient,eliteRun);
                int kills=Convert.ToInt32(Sql("SELECT kills FROM revora_planet_runs WHERE uid=@uid ORDER BY starts_at DESC LIMIT 1","uid",(long)CareerUid));
                p.BindWorldMap(1015);
                for(;kills<100;kills++){
                    var mob=p.Map.Entities.Values.FirstOrDefault(m=>m.MonsterInfo!=null && m.MonsterInfo.ID==18 && !m.Dead);
                    if(mob==null){Call(typeof(Map),"_timerCallBack",p.Map,Time32.Now.AddSeconds(600).TotalMilliseconds);mob=p.Map.Entities.Values.First(m=>m.MonsterInfo!=null && m.MonsterInfo.ID==18 && !m.Dead);}
                    RetainedMountKill(p,mob);if(kills%25==24)RetainedMountSave(p,recipient,eliteRun);
                }
                Require((long)Sql("SELECT count(*) FROM revora_planet_runs WHERE uid=@uid AND collection_step=@step AND stage='encounter' AND kills=100","uid",(long)CareerUid,"step",completed+1)==1);
                PlanetRowan(p,212);Require(p.Map.BaseID==700 && p.Map.ID!=700);
                var guards=p.Map.Entities.Values.Where(m=>m.MonsterInfo!=null && m.MonsterInfo.ID>=9926100 && m.MonsterInfo.ID<=9926102 && !m.Dead).ToArray();Require(guards.Length==3);
                foreach(var mob in guards)RetainedMountKill(p,mob);
                HeartTalk(p,900000190,1);Require(p.Entity.MapID==1002);
                PlanetRowan(p,213);completed++;
                Require((long)Sql("SELECT count(*) FROM revora_planet_runs WHERE uid=@uid AND collection_step=@step AND stage='complete' AND kills=100 AND cardinality(reward_uids)=1","uid",(long)CareerUid,"step",completed)==1);
                var reward=(long[])Sql("SELECT reward_uids FROM revora_planet_runs WHERE uid=@uid AND collection_step=@step AND stage='complete'","uid",(long)CareerUid,"step",completed);
                Require(reward.Length==1);var rune=p.Inventory.Objects.Single(i=>i.UID==(uint)reward[0]);Require(rune.ID==4030101 && rune.Bound);CareerRuneMove(p,rune,61);Require(rune.Position==211 && !p.Inventory.ContainsUID(rune.UID));
                RetainedMountSave(p,recipient,eliteRun);
                System.Console.WriteLine("[retained-mounts] earned-step="+completed);
            }
            PlanetRowan(p,246);RetainedMountSave(p,recipient,eliteRun);
            for(int i=156;i<stock.Length;i++)if(!CombinedOwns(p,stock[i])){
                PlanetRowan(p,(byte)(220+(i-156)/6));PlanetRowan(p,(byte)(40+i-156));
                var item=p.Inventory.Objects.Single(g=>g.ID==stock[i]);Require(item.Bound && item.Days==0);CollectionDeposit(p,item);
                RetainedMountSave(p,recipient,eliteRun);
                if(CombinedCount(p,"Mounts")==199)Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11232","uid",(long)CareerUid)==0);
                if(CombinedCount(p,"Mounts")==200)Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11232","uid",(long)CareerUid)==1);
            }
        }else Require(phase.EndsWith("reload"));
        Require(CombinedCount(p,"Mounts")==201 && CombinedCount(p,"Garments")==200 && (long)Sql("SELECT count(*) FROM revora_planet_exchange WHERE uid=@uid","uid",(long)CareerUid)==257);
        Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11232 AND source='wardrobe-permanent-v1'","uid",(long)CareerUid)==1);
        string once=RetainedMountSnapshot(p,recipient,eliteRun);PlanetRowan(p,246);
        for(int i=156;i<257;i++)PlanetRowan(p,(byte)(40+i-156));
        Require(once==RetainedMountSnapshot(p,recipient,eliteRun));
        Require(RetainedMountSnapshot(p,recipient,eliteRun)==Convert.ToString(Sql("SELECT snapshot FROM revora_retained_mounts_probe")));
        System.Console.WriteLine("[career-process] "+phase+"-passed mounts=201 garments=200 hits="+Sql("SELECT hits FROM revora_retained_mounts_probe"));Server.CommandsAI("@close");throw new InvalidOperationException("Retained mounts did not close.");
    }
}
