using System;
using System.Linq;
using System.Collections.Generic;
using Revora;
using Revora.Client;
using Revora.Game;
using Revora.Game.Features.Tournaments;
using System.Reflection;
using Revora.Database;
using Revora.Network.GamePackets;
using Console=System.Console;

internal static partial class CheckCandidateArenaLifecycle {
    static bool surpriseBoxFixture;
    static string SurpriseBoxSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object('cp',(SELECT conquerpoints FROM entities WHERE uid=@uid),'items',(SELECT json_agg(r ORDER BY uid) FROM items r WHERE entityid=@uid),'uses',(SELECT json_agg(r ORDER BY id) FROM revora_cp_bag_uses r WHERE uid=@uid))::text","uid",(long)p.Entity.UID));}
    static void SurpriseBoxProcess(bool reload){
        surpriseBoxFixture=true;const uint uid=4294918401;
        if((bool)Sql("SELECT to_regclass('revora_surprise_box_probe') IS NULL")){
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==0);nextUid=uid;Player(Kernel.Maps[1002]);
            Sql("CREATE TABLE revora_surprise_box_probe(snapshot text,before_cp bigint);INSERT INTO revora_surprise_box_probe SELECT NULL,conquerpoints FROM entities WHERE uid=@uid","uid",(long)uid);
        }
        nextUid=uid;var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if(Sql("SELECT snapshot FROM revora_surprise_box_probe")==DBNull.Value){
            Require(!reload && p.Inventory.Count==0);var box=RefineryClaimGive(p,722178);var frame=new ItemUsage(true){UID=box.UID,Action=ItemUsage.Mode.EquipItem}.ToArray();Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,frame.Length-8,frame);
            while(p.Inventory.Count<39)RefineryClaimGive(p,1088001);string full=SurpriseBoxSnapshot(p);ItemUsage.Process(frame,p);Require(full==SurpriseBoxSnapshot(p) && !PirateFenced(p));
            var filler=p.Inventory.Objects.First(i=>i.ID==1088001);p.Inventory.Remove(filler,Enums.ItemUse.Remove);Require(p.Inventory.Count==38);
            string before=SurpriseBoxSnapshot(p);var malformed=(byte[])frame.Clone();malformed[malformed.Length-1]^=1;ItemUsage.Process(malformed,p);Require(before==SurpriseBoxSnapshot(p));
            p.Fake=true;ItemHandler.UseItem(box,p);p.Fake=false;Require(before==SurpriseBoxSnapshot(p));
            Sql(@"CREATE OR REPLACE FUNCTION revora_surprise_test_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.uid=4294918401 THEN RAISE EXCEPTION 'Injected Surprise Box receipt failure'; END IF; RETURN NEW; END $$;
CREATE TRIGGER revora_surprise_test_failure BEFORE INSERT ON revora_cp_bag_uses FOR EACH ROW EXECUTE FUNCTION revora_surprise_test_failure()");
            try{ItemUsage.Process(frame,p);Require(PirateFenced(p) && before==SurpriseBoxSnapshot(p));}
            finally{Sql("DROP TRIGGER revora_surprise_test_failure ON revora_cp_bag_uses;DROP FUNCTION revora_surprise_test_failure()");}
            nextUid=uid;p=Player(Kernel.Maps[1002],true);p.LoadItemStats();Require(!PirateFenced(p) && before==SurpriseBoxSnapshot(p));
            System.Threading.Tasks.Parallel.For(0,8,_=>ItemUsage.Process(frame,p));Require(!p.Inventory.ContainsUID(box.UID) && p.Entity.ConquerPoints==Convert.ToUInt32(Sql("SELECT before_cp FROM revora_surprise_box_probe"))+2000);
            var direct=RefineryClaimGive(p,722178);ItemHandler.UseItem(direct,p);Require(!p.Inventory.ContainsUID(direct.UID));string paid=SurpriseBoxSnapshot(p);ItemHandler.UseItem(direct,p);ItemUsage.Process(frame,p);Require(paid==SurpriseBoxSnapshot(p));
            var ordinaryBag=RefineryClaimGive(p,729911);SoulClaimUse(p,ordinaryBag);Require(p.Entity.ConquerPoints==Convert.ToUInt32(Sql("SELECT before_cp FROM revora_surprise_box_probe"))+4003);
            Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_surprise_box_probe SET snapshot=@snapshot","snapshot",SurpriseBoxSnapshot(p));
        }
        Require((long)Sql("SELECT count(*) FROM revora_cp_bag_uses WHERE uid=@uid AND item_type=722178 AND cps=2000","uid",(long)uid)==2);
        Require(p.Entity.ConquerPoints==Convert.ToUInt32(Sql("SELECT before_cp FROM revora_surprise_box_probe"))+4003 && !p.Inventory.Objects.Any(i=>i.ID==722178));
        string exact=SurpriseBoxSnapshot(p);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_surprise_box_probe")));
        Call(Feature("ConquerPointBags"),"Initialize");Require(exact==SurpriseBoxSnapshot(p) && !PirateFenced(p));
        Console.WriteLine("[career-process] career-surprise-box-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Surprise Box did not close.");
    }
    static bool medicineBundleFixture;
    static string MedicineBundleSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object('items',(SELECT json_agg(r ORDER BY uid) FROM items r WHERE entityid=@uid),'uses',(SELECT json_agg(r ORDER BY id) FROM revora_medicine_bundle_uses r WHERE uid=@uid),'purse',(SELECT json_build_array(conquerpoints,money,revorapoint) FROM entities WHERE uid=@uid))::text","uid",(long)p.Entity.UID));}
    static void MedicineBundleProcess(bool reload){
        medicineBundleFixture=true;const uint uid=4294918501;uint[] outputs={1000030,1002000,1002010,1002020,1001030,1001040,1002030,1002040};
        if((bool)Sql("SELECT to_regclass('revora_medicine_bundle_probe') IS NULL")){
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==0);nextUid=uid;Player(Kernel.Maps[1002]);Sql("CREATE TABLE revora_medicine_bundle_probe(snapshot text);INSERT INTO revora_medicine_bundle_probe VALUES(NULL)");
        }
        nextUid=uid;var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if(Sql("SELECT snapshot FROM revora_medicine_bundle_probe")==DBNull.Value){
            Require(!reload && p.Inventory.Count==0);
            for(int n=0;n<8;n++){
                uint type=(uint)(720010+n);var info=ConquerItemInformation.BaseInformations[type];var source=new ConquerItem(true){ID=type,Bound=n%2==0,StackSize=1,Durability=info.Durability,MaximDurability=info.Durability};Require(p.Inventory.Add(source,Enums.ItemUse.CreateAndAdd));
                var frame=new ItemUsage(true){UID=source.UID,Action=ItemUsage.Mode.EquipItem}.ToArray();Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,frame.Length-8,frame);
                if(n==0){
                    while(p.Inventory.Count<39)RefineryClaimGive(p,1088001);string full=MedicineBundleSnapshot(p);ItemUsage.Process(frame,p);Require(full==MedicineBundleSnapshot(p) && !PirateFenced(p));
                    p.Inventory.Remove(p.Inventory.Objects.First(i=>i.ID==1088001),Enums.ItemUse.Remove);Require(p.Inventory.Count==38);string before=MedicineBundleSnapshot(p);
                    var bad=(byte[])frame.Clone();bad[bad.Length-1]^=1;ItemUsage.Process(bad,p);Require(before==MedicineBundleSnapshot(p));p.Fake=true;ItemHandler.UseItem(source,p);p.Fake=false;Require(before==MedicineBundleSnapshot(p));
                    Sql(@"CREATE OR REPLACE FUNCTION revora_medicine_bundle_test_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.uid=4294918501 THEN RAISE EXCEPTION 'Injected medicine bundle receipt failure'; END IF; RETURN NEW; END $$;
CREATE TRIGGER revora_medicine_bundle_test_failure BEFORE INSERT ON revora_medicine_bundle_uses FOR EACH ROW EXECUTE FUNCTION revora_medicine_bundle_test_failure()");
                    try{ItemUsage.Process(frame,p);Require(PirateFenced(p) && before==MedicineBundleSnapshot(p));}
                    finally{Sql("DROP TRIGGER revora_medicine_bundle_test_failure ON revora_medicine_bundle_uses;DROP FUNCTION revora_medicine_bundle_test_failure()");}
                    nextUid=uid;p=Player(Kernel.Maps[1002],true);p.LoadItemStats();Require(!PirateFenced(p) && before==MedicineBundleSnapshot(p));
                }
                uint hp=p.Entity.Hitpoints;ushort mana=p.Entity.Mana;long[] old=p.Inventory.Objects.Select(i=>(long)i.UID).ToArray();
                if(n%2==0)System.Threading.Tasks.Parallel.For(0,8,_=>ItemUsage.Process(frame,p));else ItemHandler.UseItem(source,p);
                var prize=p.Inventory.Objects.Single(i=>!old.Contains((long)i.UID));Require(prize.ID==outputs[n] && prize.StackSize==3 && !prize.Bound && prize.Plus==0 && !p.Inventory.ContainsUID(source.UID) && !PirateFenced(p));Require(p.Entity.Hitpoints==hp && p.Entity.Mana==mana);
                string once=MedicineBundleSnapshot(p);ItemUsage.Process(frame,p);ItemHandler.UseItem(source,p);Require(once==MedicineBundleSnapshot(p));
                if(n==0)foreach(var filler in p.Inventory.Objects.Where(i=>i.ID==1088001).ToArray())p.Inventory.Remove(filler,Enums.ItemUse.Remove);
            }
            Require(p.Inventory.Count==8 && p.Inventory.Objects.Sum(i=>(int)i.StackSize)==24);Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_medicine_bundle_probe SET snapshot=@snapshot","snapshot",MedicineBundleSnapshot(p));
        }
        Require(p.Inventory.Count==8 && outputs.All(id=>p.Inventory.Objects.Count(i=>i.ID==id && i.StackSize==3 && !i.Bound && i.Plus==0)==1));
        Require((long)Sql("SELECT count(*) FROM revora_medicine_bundle_uses WHERE uid=@uid AND cardinality(rewards)=1","uid",(long)uid)==8);
        Require((long)Sql("SELECT count(DISTINCT item_type) FROM revora_medicine_bundle_uses WHERE uid=@uid","uid",(long)uid)==8);
        string exact=MedicineBundleSnapshot(p);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_medicine_bundle_probe")));Call(Feature("HealingMedicine"),"Initialize");Require(exact==MedicineBundleSnapshot(p) && !PirateFenced(p));
        Console.WriteLine("[career-process] career-medicine-bundle-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Medicine bundle did not close.");
    }
    static bool mainClaimFixture;
    static bool mainThirdFixture;
    static bool mainFourthFixture;
    static string MainCareerSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object(
'base',@snapshot::json,'xp',(SELECT json_agg(r ORDER BY life) FROM revora_xp_kills r WHERE uid=@uid),
'intro',(SELECT row_to_json(r) FROM revora_newcomer_intro r WHERE uid=@uid),
'promotions',(SELECT json_agg(r ORDER BY achievement_id) FROM revora_achievement_evidence r WHERE uid=@uid AND source='class-promotion-v1'))::text",
"snapshot",MainPackSnapshot(p),"uid",(long)p.Entity.UID));}
    static void MainCareerProcess(bool reload){
        careerRitesFixture=true;combinedCollectionFixture=true;nextUid=CareerUid;var p=Player(Kernel.Maps[1002],true,1003,true,true);combinedCollectionFixture=false;
        SkillTable.LoadSpells(p);SkillTable.LoadProficiencies(p);SubClassTable.Load(p.Entity);Revora.Database.KnownPersons.LoadMentor(p);
        new CoatStorage().Login(p);new TitleStorage().Login(p);p.LoadItemStats();ChiTable.Load(p);WayofHeroes.Load(p);EntityVariableTable.Load(p);
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_main_career_probe') IS NULL")){
            Require(!reload);Sql("CREATE TABLE revora_main_career_probe(snapshot text,bound_before bigint);INSERT INTO revora_main_career_probe VALUES(NULL,@bound)","bound",(long)p.Entity.BoundCps);
            if(p.Inventory.Count>33)GrailMakeRoom(p,33,who=>Require(EntityTable.SaveEntity(who)),4294917901u);
        }
        Func<long> xp=()=> (long)Sql("SELECT count(*) FROM revora_xp_kills WHERE uid=@uid","uid",(long)CareerUid);
        if(Sql("SELECT snapshot FROM revora_main_career_probe")==DBNull.Value){
            Require(!reload);
            for(uint id=101;id<=105;id++)if(id!=103){
                string before=MainCareerSnapshot(p);MainClaimPacket(p,1);Require(before==MainCareerSnapshot(p));
                NobilityClaimPacket(p,id);Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=@stage AND NOT legacy","uid",(long)CareerUid,"stage",(int)id)==1);
            }
            if(xp()<20){
                p.Entity.Teleport(1004,30,30);if(!p.Spells.ContainsKey(10390))HeartTalk(p,4271,7);Require(p.Spells.ContainsKey(10390));SkillTable.SaveSpells(p);
                p.Entity.Teleport(1002,400,350);
                Func<Entity> target=()=>p.Map.Entities.Values.First(m=>m.MonsterInfo!=null && !m.MonsterInfo.Boss && !m.MonsterInfo.Guard && !m.MonsterInfo.Reviver && !m.Companion && !m.Dead && m.MaxHitpoints<1000);
                Action<Entity> kill=m=>{
                    p.Entity.X=m.X;p.Entity.Y=(ushort)(m.Y+1);p.Screen.Add(m);p.CantAttack=Time32.Now.AddSeconds(-1);
                    int hits=0;while(!m.Dead && hits++<10){p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);Attack.HandleAttack(new Attack(true){Attacker=p.Entity.UID,Attacked=m.UID,X=m.X,Y=m.Y,AttackType=Attack.Melee},p);}
                    Require(m.Dead && !PirateFenced(p));
                };
                int preparatory=0;while(p.XPCount<100 && preparatory++<110)kill(target());Require(p.XPCount>=100);
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;var wrapper=typeof(Revora.Network.Sockets.ClientWrapper);
                wrapper.GetField("SendQueue",flags).SetValue(p.Socket,new Queue<byte[]>());wrapper.GetField("SendSyncRoot",flags).SetValue(p.Socket,new object());
                p.Socket.Alive=true;p.XPCountStamp=Time32.Now.AddSeconds(-4);
                try{typeof(Revora.Thread).GetMethod("BuffersCallback",flags).Invoke(Server.Thread,new object[]{p,unchecked((int)Time32.Now.Value)});Require(p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.XPList));}
                finally{p.Socket.Alive=false;}
                p.Entity.AttackStamp=p.Entity.SpellStamp=Time32.Now.AddSeconds(-10);p.Spells[10390].ColdTime=Time32.Now.AddSeconds(-10);
                new Revora.Game.Attacking.Handle(new Attack(true){AttackType=Attack.Magic,Decoded=true,Damage=10390,Attacker=p.Entity.UID,Attacked=p.Entity.UID,X=p.Entity.X,Y=p.Entity.Y},p.Entity,p.Entity);
                Require((ushort)Call(Feature("XpEffectAttribution"),"Active",p.Entity)==10390);
                while(xp()<20){long n=xp();kill(target());Require(xp()==n+1);}
                p.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Oblivion);Require(EntityTable.SaveEntity(p));
            }
            Require(xp()==20);
            if((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=103","uid",(long)CareerUid)==0){
                string before=MainCareerSnapshot(p);MainClaimPacket(p,1);Require(before==MainCareerSnapshot(p));NobilityClaimPacket(p,103);
            }
            Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid BETWEEN 101 AND 105 AND NOT legacy","uid",(long)CareerUid)==5);
            System.Threading.Tasks.Parallel.For(0,8,_=>MainClaimPacket(p,1));
            var reward=p.Inventory.Objects.SingleOrDefault(i=>i.ID==3005107);if(reward!=null){Require(reward.Bound && reward.StackSize==1);SoulClaimUse(p,reward);}
            Require(p.Entity.BoundCps==Convert.ToUInt32(Sql("SELECT bound_before FROM revora_main_career_probe"))+20);
            Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_main_career_probe SET snapshot=@snapshot","snapshot",MainCareerSnapshot(p));
        }
        Require(p.Way2Heroes[1].Done && p.Way2Heroes[1].SubStages.Count==5 && xp()==20);
        Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=100 AND NOT legacy","uid",(long)CareerUid)==1);
        Require((long)Sql("SELECT count(*) FROM revora_silver_study_bag_uses WHERE uid=@uid AND item_type=3005107 AND boundcps=20","uid",(long)CareerUid)==1);
        string exact=MainCareerSnapshot(p);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_main_career_probe")));
        for(uint id=101;id<=105;id++)NobilityClaimPacket(p,id);MainClaimPacket(p,1);Require(exact==MainCareerSnapshot(p) && !PirateFenced(p));
        Console.WriteLine("[career-process] career-main-career-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Main career did not close.");
    }
    static string MainSecondSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object(
'career',@snapshot::json,'team',(SELECT row_to_json(r) FROM revora_team_earning r WHERE uid=@uid),
'potions',(SELECT json_agg(r ORDER BY id) FROM revora_exp_potion_uses r WHERE uid=@uid),
'double_experience',(SELECT doubleexptime FROM entities WHERE uid=@uid))::text","snapshot",MainCareerSnapshot(p),"uid",(long)p.Entity.UID));}
    static void MainSecondCareer(bool reload){
        careerRitesFixture=true;combinedCollectionFixture=true;nextUid=CareerUid;var p=Player(Kernel.Maps[1002],true,1003,true,true);combinedCollectionFixture=false;
        SkillTable.LoadSpells(p);SkillTable.LoadProficiencies(p);SubClassTable.Load(p.Entity);Revora.Database.KnownPersons.LoadMentor(p);
        new CoatStorage().Login(p);new TitleStorage().Login(p);p.LoadItemStats();ChiTable.Load(p);WayofHeroes.Load(p);EntityVariableTable.Load(p);
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_main_second_probe') IS NULL")){
            Require(!reload && MainCareerSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_main_career_probe")));
            Sql("CREATE TABLE revora_main_second_probe(snapshot text,bound_before bigint);INSERT INTO revora_main_second_probe VALUES(NULL,@bound)","bound",(long)p.Entity.BoundCps);
            if(p.Inventory.Count>31)GrailMakeRoom(p,31,who=>Require(EntityTable.SaveEntity(who)),4294917901u);
        }
        if(Sql("SELECT snapshot FROM revora_main_second_probe")==DBNull.Value){
            Require(!reload);p.Entity.Teleport(1002,400,350);
            Console.WriteLine("[main-second] bag="+p.Inventory.Count);
            if(p.Inventory.Count>31){var purse=p.Inventory.Objects.First(i=>i.ID==3005117);SoulClaimUse(p,purse);}
            Require(p.Inventory.Count<=31);
            Action refused=()=>{string before=MainSecondSnapshot(p);MainClaimPacket(p,2);Require(before==MainSecondSnapshot(p));};
            if((long)Sql("SELECT count(*) FROM revora_team_earning WHERE uid=@uid","uid",(long)CareerUid)==0){
                refused();TeamClaimPacket(p,Team.Create,CareerUid);Require(p.Team!=null && p.Team.Lider==p);TeamClaimPacket(p,Team.Dismiss,CareerUid);Require(p.Team==null);
            }
            for(uint id=201;id<=207;id++){
                if((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=@stage","uid",(long)CareerUid,"stage",(int)id)!=0)continue;
                refused();
                if(id==206 && (long)Sql("SELECT count(*) FROM revora_exp_potion_uses WHERE uid=@uid AND item_type=723017 AND seconds=3600","uid",(long)CareerUid)==0){
                    Console.WriteLine("[main-second] prior-double-experience="+p.Entity.DoubleExperienceTime);
                    Require(p.Entity.DoubleExperienceTime==0);var potion=p.Inventory.Objects.Where(i=>i.ID==723017 && i.Bound).OrderByDescending(i=>i.UID).First();
                    SoulClaimUse(p,potion);Require(p.Entity.DoubleExperienceTime==3600);
                }
                NobilityClaimPacket(p,id);Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=@stage AND NOT legacy","uid",(long)CareerUid,"stage",(int)id)==1);
            }
            Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid BETWEEN 201 AND 207 AND NOT legacy","uid",(long)CareerUid)==7);
            System.Threading.Tasks.Parallel.For(0,8,_=>MainClaimPacket(p,2));var pack=p.Inventory.Objects.SingleOrDefault(i=>i.ID==3005108);
            if(pack!=null){Require(pack.Bound && pack.StackSize==1);SoulClaimUse(p,pack);}
            Require(p.Entity.BoundCps==Convert.ToUInt32(Sql("SELECT bound_before FROM revora_main_second_probe"))+30);
            Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_main_second_probe SET snapshot=@snapshot","snapshot",MainSecondSnapshot(p));
        }
        Require(p.Way2Heroes[1].Done && p.Way2Heroes[2].Done && p.Way2Heroes[2].SubStages.Count==7 && p.Entity.DoubleExperienceTime==3600);
        Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=200 AND NOT legacy","uid",(long)CareerUid)==1);
        Require((long)Sql("SELECT count(*) FROM revora_silver_study_bag_uses WHERE uid=@uid AND item_type=3005108 AND boundcps=30","uid",(long)CareerUid)==1);
        string exact=MainSecondSnapshot(p);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_main_second_probe")));
        for(uint id=201;id<=207;id++)NobilityClaimPacket(p,id);MainClaimPacket(p,2);Require(exact==MainSecondSnapshot(p) && !PirateFenced(p));
        Console.WriteLine("[career-process] career-main-second-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Main second career did not close.");
    }
    static void MainCareerRoom(GameState p,GameState peer,int target){
        Func<ConquerItem,bool> portable=i=>i.Days==0 && i.Lock==0 && !i.Suspicious && !ConquerItem.isRune(i.ID)
            && !(bool)Call(Feature("MoonBoxQuest"),"Material",i.ID) && !(i.ID>=729960 && i.ID<=729970);
        Func<ConquerItem,bool> bankGift=i=>!i.Bound && i.StackSize==1 && i.Days==0 && i.Lock==0 && !i.Suspicious
            && ConquerItemInformation.BaseInformations[i.ID].Type==ConquerItemBaseInformation.ItemType.Dropable;
        while(p.Inventory.Count>target){
            Require(p.Inventory.Count<40 && peer.Inventory.Count<40);
            var gift=p.Inventory.Objects.FirstOrDefault(i=>(bool)Call(Feature("AtomicTrade"),"Tradable",p,peer,i));
            ConquerItem stored=null;uint banker=8;var location=Kernel.Maps[1002];
            if(gift==null){
                stored=p.Inventory.Objects.Where(portable).OrderBy(i=>i.UID).First();
                var bank=p.Warehouses.First(w=>w.Value.Objects.Any(bankGift)
                    && new ushort[]{1002,1011,1020,1000,1015,1036,1213}.Any(map=>Kernel.Maps[map].Npcs.ContainsKey((uint)w.Key)));
                banker=(uint)bank.Key;location=new ushort[]{1002,1011,1020,1000,1015,1036,1213}.Select(map=>Kernel.Maps[map]).First(map=>map.Npcs.ContainsKey(banker));
                gift=bank.Value.Objects.First(bankGift);
                var npc=location.Npcs[banker];p.Entity.Teleport(location.ID,npc.X,npc.Y);GrailWarehouse(p,gift.UID,2,banker);Require(p.Inventory.ContainsUID(gift.UID));
            }
            var actor=location.Npcs[banker];p.Entity.Teleport(location.ID,actor.X,actor.Y);peer.Entity.Teleport(location.ID,actor.X,actor.Y);
            CareerTradePacket(p,Trade.Request,peer.Entity.UID);CareerTradePacket(peer,Trade.Request,p.Entity.UID);CareerTradePacket(p,Trade.AddItem,gift.UID);
            Require(p.Trade.Items.Count==1);CareerTradePacket(p,Trade.Accept,0);CareerTradePacket(peer,Trade.Accept,0);
            Require(!p.Inventory.ContainsUID(gift.UID) && peer.Inventory.ContainsUID(gift.UID));
            if(stored!=null){GrailWarehouse(p,stored.UID,1,banker);Require(!p.Inventory.ContainsUID(stored.UID));}
            Require(EntityTable.SaveEntity(p) && EntityTable.SaveEntity(peer));
        }
    }
    static string MainThirdSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object(
'career',@snapshot::json,'friends',(SELECT json_agg(f ORDER BY entityid,friendid) FROM friends f WHERE entityid=@uid OR friendid=@uid),
'consents',(SELECT json_agg(r ORDER BY id) FROM revora_friend_consents r WHERE requester=@uid OR receiver=@uid),
'arena',(SELECT json_agg(r ORDER BY run) FROM revora_team_arena_statistics r WHERE uid=@uid),
'peer_items',(SELECT json_agg(i ORDER BY uid) FROM items i WHERE entityid BETWEEN 4294918101 AND 4294918105))::text",
"snapshot",MainSecondSnapshot(p),"uid",(long)p.Entity.UID));}
    static void MainThirdCareer(bool reload){
        mainThirdFixture=true;const uint first=4294918101;
        for(uint uid=first;uid<first+5;uid++)if((long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==0){Require(!reload);nextUid=uid;Player(Kernel.Maps[1002]);}
        careerRitesFixture=true;combinedCollectionFixture=true;nextUid=CareerUid;var p=Player(Kernel.Maps[1002],true,1003,true,true);combinedCollectionFixture=false;
        SkillTable.LoadSpells(p);SkillTable.LoadProficiencies(p);SubClassTable.Load(p.Entity);Revora.Database.KnownPersons.LoadMentor(p);
        new CoatStorage().Login(p);new TitleStorage().Login(p);p.LoadItemStats();ChiTable.Load(p);WayofHeroes.Load(p);EntityVariableTable.Load(p);
        var peers=new GameState[5];for(int n=0;n<peers.Length;n++){nextUid=first+(uint)n;peers[n]=Player(Kernel.Maps[1002],true);peers[n].LoadItemStats();}
        var all=new[]{p}.Concat(peers).ToArray();foreach(var who in all){Revora.Database.KnownPersons.LoaderFriends(who);Revora.Database.KnownPersons.LoadPartner(who);Revora.Database.KnownPersons.LoadMentor(who);Revora.Database.KnownPersons.LoadEnemy(who);Require(!who.Fake && who.Account.State==AccountTable.AccountState.Player && !PirateFenced(who));}
        if((bool)Sql("SELECT to_regclass('revora_main_third_probe') IS NULL")){
            Require(!reload && MainSecondSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_main_second_probe")));
            Sql("CREATE TABLE revora_main_third_probe(snapshot text,bound_before bigint);INSERT INTO revora_main_third_probe VALUES(NULL,@bound)","bound",(long)p.Entity.BoundCps);
        }
        if(Sql("SELECT snapshot FROM revora_main_third_probe")==DBNull.Value){
            Require(!reload);MainCareerRoom(p,peers[4],27);
            foreach(var who in all)who.Entity.Teleport(1002,408,330);
            var clock=Feature("Friendships").GetField("Clock",Hidden);object previous=clock.GetValue(null);DateTime now=DateTime.UtcNow;
            try{clock.SetValue(null,(Func<DateTime>)(()=>now));
                foreach(var peer in peers)if(!p.Friends.ContainsKey(peer.Entity.UID)){
                    now=now.AddSeconds(2);FriendClaimSend(p,peer,Revora.Network.GamePackets.KnownPersons.FriendApply);
                    string pending=MainThirdSnapshot(p);MainClaimPacket(p,3);Require(pending==MainThirdSnapshot(p));
                    FriendClaimSend(peer,p,Revora.Network.GamePackets.KnownPersons.FriendAccept);Require(p.Friends.ContainsKey(peer.Entity.UID) && peer.Friends.ContainsKey(CareerUid));
                }
            }finally{clock.SetValue(null,previous);}
            if((long)Sql("SELECT count(*) FROM revora_team_arena_statistics WHERE uid=@uid AND won","uid",(long)CareerUid)==0){
                var mate=peers[0];var enemy=peers[1];var enemyMate=peers[2];var fighters=new[]{p,mate,enemy,enemyMate};
                TeamClaimPacket(p,Team.Create,CareerUid);TeamClaimPacket(p,Team.InviteRequest,mate.Entity.UID);TeamClaimPacket(mate,Team.AcceptInvitation,CareerUid);
                TeamClaimPacket(enemy,Team.Create,enemy.Entity.UID);TeamClaimPacket(enemy,Team.InviteRequest,enemyMate.Entity.UID);TeamClaimPacket(enemyMate,Team.AcceptInvitation,enemy.Entity.UID);
                Require(p.Team!=null && ReferenceEquals(p.Team,mate.Team) && enemy.Team!=null && ReferenceEquals(enemy.Team,enemyMate.Team));
                bool accepting=TeamQualifier.AcceptingNewBattles;
                try{TeamQualifier.AcceptingNewBattles=true;
                    string before=MainThirdSnapshot(p);NobilityClaimPacket(p,305);Require(before==MainThirdSnapshot(p));
                    TeamArenaClaimRequest(p,0,0);TeamArenaClaimRequest(enemy,0,0);TeamQualifier.EngagePlayers();Require(ReferenceEquals(p.TeamArenaStatistic.PlayWith,enemy));
                    TeamArenaClaimRequest(p,3,1);TeamArenaClaimRequest(enemy,3,1);TeamQualifier.VerifyAwaitingPeople();
                    var group=p.TeamQualifierGroup;Require(group!=null && fighters.All(who=>ReferenceEquals(who.TeamQualifierGroup,group)));
                    before=MainThirdSnapshot(p);NobilityClaimPacket(p,305);Require(before==MainThirdSnapshot(p));
                    group.ImportTime=group.CreateTime=Time32.Now.AddSeconds(-13);foreach(var who in fighters){who.Attackable=true;who.CantAttack=Time32.Now.AddSeconds(-1);}
                    foreach(var target in new[]{enemy,enemyMate}){
                        int hits=0;while(!target.Entity.Dead && !group.Done && hits++<100){p.Entity.X=target.Entity.X;p.Entity.Y=(ushort)(target.Entity.Y+1);p.Screen.Add(target.Entity);target.Screen.Add(p.Entity);
                            p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);Attack.HandleAttack(new Attack(true){Attacker=CareerUid,Attacked=target.Entity.UID,X=target.Entity.X,Y=target.Entity.Y,AttackType=Attack.Melee},p);}
                    }
                    Require(group.Done);group.DoneStamp=Time32.Now.AddSeconds(-6);var limit=DateTime.UtcNow.AddSeconds(10);
                    do{TeamQualifier.CheckGroups();if(fighters.All(who=>who.TeamQualifierGroup==null))break;System.Threading.Thread.Sleep(50);}while(DateTime.UtcNow<limit);
                    Require(fighters.All(who=>who.TeamQualifierGroup==null));foreach(var who in fighters)Require(EntityTable.SaveEntity(who));
                }finally{TeamQualifier.AcceptingNewBattles=accepting;}
            }
            Require((long)Sql("SELECT count(*) FROM revora_team_arena_statistics WHERE uid=@uid AND won","uid",(long)CareerUid)==1);
            for(uint id=301;id<=306;id++)if((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=@stage","uid",(long)CareerUid,"stage",(int)id)==0){
                string before=MainThirdSnapshot(p);MainClaimPacket(p,3);Require(before==MainThirdSnapshot(p));NobilityClaimPacket(p,id);
                Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=@stage AND NOT legacy","uid",(long)CareerUid,"stage",(int)id)==1);
            }
            System.Threading.Tasks.Parallel.For(0,8,_=>MainClaimPacket(p,3));var reward=p.Inventory.Objects.SingleOrDefault(i=>i.ID==3005109);
            if(reward!=null){Require(reward.Bound && reward.StackSize==1);SoulClaimUse(p,reward);}
            Require(p.Entity.BoundCps==Convert.ToUInt32(Sql("SELECT bound_before FROM revora_main_third_probe"))+50);
            foreach(var who in all)Require(EntityTable.SaveEntity(who));Sql("UPDATE revora_main_third_probe SET snapshot=@snapshot","snapshot",MainThirdSnapshot(p));
        }
        Require(p.Way2Heroes[3].Done && p.Way2Heroes[3].SubStages.Count==6);
        Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid BETWEEN 300 AND 306 AND NOT legacy","uid",(long)CareerUid)==7);
        Require((long)Sql("SELECT count(*) FROM revora_silver_study_bag_uses WHERE uid=@uid AND item_type=3005109 AND boundcps=50","uid",(long)CareerUid)==1);
        string exact=MainThirdSnapshot(p);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_main_third_probe")));
        for(uint id=301;id<=306;id++)NobilityClaimPacket(p,id);MainClaimPacket(p,3);Require(exact==MainThirdSnapshot(p) && !PirateFenced(p));
        Console.WriteLine("[career-process] career-main-third-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Main third career did not close.");
    }
    static string MainFourthSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object(
'career',@snapshot::json,
'composition',(SELECT json_agg(r ORDER BY id) FROM revora_plus_compositions r WHERE uid=@uid),
'composition_targets',(SELECT json_agg(e ORDER BY receipt) FROM revora_plus_composition_targets e JOIN revora_plus_compositions r ON r.id=e.receipt WHERE r.uid=@uid),
'participation',(SELECT json_agg(json_build_array(a.run,r.kind,a.participated_at) ORDER BY a.run) FROM revora_local_team_pk_admissions a JOIN revora_local_team_pk_runs r USING(run) WHERE a.uid=@uid),
'peer_items',(SELECT json_agg(i ORDER BY uid) FROM items i WHERE entityid BETWEEN 4294918201 AND 4294918215))::text",
"snapshot",MainThirdSnapshot(p),"uid",(long)p.Entity.UID));}
    static void MainFourthTournament(GameState[] players,int kind){
        var p=players[0];uint stage=(uint)(406+kind);
        if((long)Sql("SELECT count(*) FROM revora_local_team_pk_admissions a JOIN revora_local_team_pk_runs r USING(run) WHERE a.uid=@uid AND r.kind=@kind AND a.participated_at IS NOT NULL","uid",(long)CareerUid,"kind",kind)>0)return;
        foreach(var who in players)who.Entity.Teleport(1002,408,330);
        for(int n=0;n<players.Length;n+=2){var leader=players[n];var mate=players[n+1];
            if(leader.Team==null){TeamClaimPacket(leader,Team.Create,leader.Entity.UID);TeamClaimPacket(leader,Team.InviteRequest,mate.Entity.UID);TeamClaimPacket(mate,Team.AcceptInvitation,leader.Entity.UID);}
            Require(leader.Team!=null && ReferenceEquals(leader.Team,mate.Team));
        }
        string before=MainFourthSnapshot(p);NobilityClaimPacket(p,stage);MainClaimPacket(p,4);Require(before==MainFourthSnapshot(p));
        DateTime opening=DateTime.Today.AddDays(35).AddHours(19).AddMinutes(45);while(opening.DayOfWeek!=DayOfWeek.Wednesday)opening=opening.AddDays(1);
        if(kind==0){var fresh=TeamElitePk.TeamTournament.Tournaments[3];Require((bool)fresh.GetType().GetMethod("SubscribeUntil",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(fresh,new object[]{opening.AddDays(1),opening.AddDays(1).AddHours(1)}));TeamElitePk.TeamTournament.Opened=true;}
        else Require((bool)Call(typeof(TeamElitePk.SkillTeamTournament),"OpenAt",opening));
        var bracket=kind==0?TeamElitePk.TeamTournament.Tournaments[3]:TeamElitePk.SkillTeamTournament.Tournaments[3];
        foreach(var who in players){HeartTalk(who,kind==0?417171u:801717u,2);Require(who.Entity.MapID==bracket.WaitingArea.ID);}
        before=MainFourthSnapshot(p);NobilityClaimPacket(p,stage);MainClaimPacket(p,4);Require(before==MainFourthSnapshot(p));
        Require(bracket.Teams.Count==8);var flags=BindingFlags.Instance|BindingFlags.NonPublic;var bt=bracket.GetType();
        bt.GetField("StarTimer",flags).SetValue(bracket,DateTime.Now.AddMinutes(-2));bt.GetField("pStamp",flags).SetValue(bracket,Time32.Now.AddSeconds(-90));
        bt.GetMethod("timerCallback",flags).Invoke(bracket,new object[]{0});
        foreach(var who in players)Require((long)Sql("SELECT count(*) FROM revora_local_team_pk_admissions a JOIN revora_local_team_pk_runs r USING(run) WHERE a.uid=@uid AND r.kind=@kind AND a.participated_at IS NOT NULL","uid",(long)who.Entity.UID,"kind",kind)==1);
        // Controlled deadlines finish the bracket; this proves participation, not combat victory.
        for(int pulse=0;pulse<80 && bracket.State!=ElitePkStatus.States.GUI_ReconstructTop;pulse++){
            bt.GetField("StarTimer",flags).SetValue(bracket,DateTime.Now.AddMinutes(-2));bt.GetField("pStamp",flags).SetValue(bracket,Time32.Now.AddSeconds(-90));
            var extended=(TeamElitePk.Match[])bt.GetField("ExtendedMatchArray",flags).GetValue(bracket);
            foreach(var match in bracket.Matches.Values.Concat(extended??new TeamElitePk.Match[0]).Distinct().Where(m=>m.Inside && !m.Done))match.ImportTime=Time32.Now.AddSeconds(-1000);
            bt.GetMethod("timerCallback",flags).Invoke(bracket,new object[]{0});
        }
        Console.WriteLine("[main-fourth] kind="+kind+" terminal-state="+bracket.State);Require(bracket.State==ElitePkStatus.States.GUI_ReconstructTop);
        bracket.ConstructTop8=DateTime.Now.AddMinutes(-3);bt.GetMethod("timerCallback",flags).Invoke(bracket,new object[]{0});Require(!bracket.Alive);
        foreach(var who in players)Require(who.Entity.MapID==1002 && EntityTable.SaveEntity(who));
    }
    static void MainFourthCareer(bool reload){
        mainFourthFixture=true;const uint first=4294918201;
        for(uint uid=first;uid<first+15;uid++)if((long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==0){
            Require(!reload);nextUid=uid;Player(Kernel.Maps[1002]);
            // New ordinary opponent prerequisites only; retained career progress is never supplied.
            Sql("UPDATE entities SET level=130,reborn=0 WHERE uid=@uid","uid",(long)uid);
        }
        careerRitesFixture=true;combinedCollectionFixture=true;nextUid=CareerUid;var p=Player(Kernel.Maps[1002],true,1003,true,true);combinedCollectionFixture=false;
        SkillTable.LoadSpells(p);SkillTable.LoadProficiencies(p);SubClassTable.Load(p.Entity);Revora.Database.KnownPersons.LoadMentor(p);
        new CoatStorage().Login(p);new TitleStorage().Login(p);p.LoadItemStats();ChiTable.Load(p);WayofHeroes.Load(p);EntityVariableTable.Load(p);
        var peers=new GameState[15];for(int n=0;n<peers.Length;n++){nextUid=first+(uint)n;peers[n]=Player(Kernel.Maps[1002],true);peers[n].LoadItemStats();}
        var all=new[]{p}.Concat(peers).ToArray();foreach(var who in all){Revora.Database.KnownPersons.LoaderFriends(who);Revora.Database.KnownPersons.LoadPartner(who);Revora.Database.KnownPersons.LoadMentor(who);Revora.Database.KnownPersons.LoadEnemy(who);Require(!who.Fake && who.Account.State==AccountTable.AccountState.Player && !PirateFenced(who));}
        if((bool)Sql("SELECT to_regclass('revora_main_fourth_probe') IS NULL")){
            Require(!reload && MainThirdSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_main_third_probe")));
            Sql("CREATE TABLE revora_main_fourth_probe(snapshot text,bound_before bigint);INSERT INTO revora_main_fourth_probe VALUES(NULL,@bound)","bound",(long)p.Entity.BoundCps);
        }
        if(Sql("SELECT snapshot FROM revora_main_fourth_probe")==DBNull.Value){
            Require(!reload);MainCareerRoom(p,peers[14],25);
            if((long)Sql("SELECT count(*) FROM revora_plus_compositions WHERE uid=@uid","uid",(long)CareerUid)==0){
                string before=MainFourthSnapshot(p);NobilityClaimPacket(p,403);Require(before==MainFourthSnapshot(p));
                var target=p.Inventory.Objects.Concat(p.Equipment.GetItems()).FirstOrDefault(i=>i.Bound && i.Plus<12 && (bool)Call(Feature("EquipmentUpgrades"),"PlusItem",i) && (bool)Call(Feature("WayOfHeroesClaims"),"BonusEquipment",i.ID));
                if(target==null){
                    var stored=p.Warehouses.Values.SelectMany(w=>w.Objects).FirstOrDefault(i=>i.Bound && i.Plus<12 && i.Lock==0 && i.Days==0 && (bool)Call(Feature("WayOfHeroesClaims"),"BonusEquipment",i.ID));
                    Require(stored!=null);uint banker=stored.Warehouse;var location=Kernel.Maps.Values.Single(m=>m.Npcs.ContainsKey(banker));var npc=location.Npcs[banker];p.Entity.Teleport(location.ID,npc.X,npc.Y);GrailWarehouse(p,stored.UID,2,banker);target=p.Inventory.Objects.Single(i=>i.UID==stored.UID);
                }
                Console.WriteLine("[main-fourth] composition target="+target.ID+" plus="+target.Plus+" bound="+target.Bound);
                var stone=p.Inventory.Objects.First(i=>i.ID==730001 && i.Plus==1 && i.Bound);CompositionClaimCompose(p,target,stone);
                Require(EntityTable.SaveEntity(p));
            }
            for(int kind=0;kind<2;kind++)MainFourthTournament(all,kind);
            for(uint id=401;id<=409;id++)if((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=@stage","uid",(long)CareerUid,"stage",(int)id)==0){
                string before=MainFourthSnapshot(p);MainClaimPacket(p,4);Require(before==MainFourthSnapshot(p));NobilityClaimPacket(p,id);
                Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=@stage AND NOT legacy","uid",(long)CareerUid,"stage",(int)id)==1);
            }
            System.Threading.Tasks.Parallel.For(0,8,_=>MainClaimPacket(p,4));var reward=p.Inventory.Objects.SingleOrDefault(i=>i.ID==3005110);
            if(reward!=null){Require(reward.Bound && reward.StackSize==1);SoulClaimUse(p,reward);}
            Require(p.Entity.BoundCps==Convert.ToUInt32(Sql("SELECT bound_before FROM revora_main_fourth_probe"))+80);
            foreach(var who in all)Require(EntityTable.SaveEntity(who));Sql("UPDATE revora_main_fourth_probe SET snapshot=@snapshot","snapshot",MainFourthSnapshot(p));
        }
        Require(p.Way2Heroes[4].Done && p.Way2Heroes[4].SubStages.Count==9);
        Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid BETWEEN 400 AND 409 AND NOT legacy","uid",(long)CareerUid)==10);
        Require((long)Sql("SELECT count(*) FROM revora_silver_study_bag_uses WHERE uid=@uid AND item_type=3005110 AND boundcps=80","uid",(long)CareerUid)==1);
        string exact=MainFourthSnapshot(p);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_main_fourth_probe")));
        for(uint id=401;id<=409;id++)NobilityClaimPacket(p,id);MainClaimPacket(p,4);Require(exact==MainFourthSnapshot(p) && !PirateFenced(p));
        Console.WriteLine("[career-process] career-main-fourth-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Main fourth career did not close.");
    }
    static string MainFifthSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object(
'career',@snapshot::json,'champion',@champion::json,
'arena_results',(SELECT json_agg(r ORDER BY id) FROM revora_team_arena_results r JOIN revora_team_arena_statistics s ON s.run=r.id WHERE s.uid=@uid),
'arena_admissions',(SELECT json_agg(a ORDER BY run) FROM revora_team_arena_admission_players a WHERE a.uid=@uid))::text",
"snapshot",MainFourthSnapshot(p),"champion",ChampionResultSnapshot(p.Entity.UID),"uid",(long)p.Entity.UID));}
    static void MainFifthChampion(GameState p,GameState other){
        if((bool)Sql("SELECT EXISTS(SELECT 1 FROM revora_champion_entries e JOIN revora_champion_entry_players a ON a.run=e.id WHERE a.uid=@uid AND e.entered_at IS NOT NULL AND e.state IN('entered','returned','interrupted'))","uid",(long)CareerUid))return;
        var clock=typeof(Champion.QualifyEngine).GetField("RegistrationClock",Hidden);object old=clock.GetValue(null);DateTime local=ChampionFixtureSignupTime(CareerUid);
        try{clock.SetValue(null,(Func<DateTime>)(()=>local));
            p.Entity.Teleport(1002,310,288);other.Entity.Teleport(1002,310,288);
            string before=MainFifthSnapshot(p);NobilityClaimPacket(p,505);MainClaimPacket(p,5);Require(before==MainFifthSnapshot(p));
            ChampionChoice(p,703001,1);ChampionChoice(other,703001,1);Require(p.ChampionStats.SignedUp && other.ChampionStats.SignedUp);
            before=MainFifthSnapshot(p);NobilityClaimPacket(p,505);Require(before==MainFifthSnapshot(p));
            p.ChampionStats.SignUpTime=other.ChampionStats.SignUpTime=Time32.Now.AddSeconds(-20);local=local.AddMinutes(5);Champion.EngagePlayers();Require(ReferenceEquals(p.ChampionStats.Opponent,other));
            ChampionChoice(p,703001,2);ChampionChoice(other,703001,2);Champion.VerifyAwaitingPeople();var group=p.ChampionGroup;Require(group!=null && ReferenceEquals(group,other.ChampionGroup));
            Require((long)Sql("SELECT count(*) FROM revora_champion_entries e JOIN revora_champion_entry_players a ON a.run=e.id WHERE a.uid=@uid AND e.state='entered' AND e.entered_at IS NOT NULL","uid",(long)CareerUid)==1);
            // The milestone is paired participation. Withdraw through the actual arena steward.
            ChampionChoice(p,703002,4);Require(group.Done && ReferenceEquals(group.Winner,other));group.Export();Require(p.ChampionGroup==null && other.ChampionGroup==null);
            Require(EntityTable.SaveEntity(p) && EntityTable.SaveEntity(other));
        }finally{clock.SetValue(null,old);}
    }
    static void MainFifthArena(GameState[] fighters){
        var p=fighters[0];var mate=fighters[1];var enemy=fighters[2];var enemyMate=fighters[3];
        foreach(var who in fighters){if(who.Entity.Dead){who.Entity.DeathStamp=Time32.Now.AddSeconds(-20);GeneralData.Revive(new Data(true){dwParam=0},who);}Require(!who.Entity.Dead);who.Entity.Teleport(1002,408,330);}
        for(int n=0;n<4;n+=2){var leader=fighters[n];var partner=fighters[n+1];TeamClaimPacket(leader,Team.Create,leader.Entity.UID);TeamClaimPacket(leader,Team.InviteRequest,partner.Entity.UID);TeamClaimPacket(partner,Team.AcceptInvitation,leader.Entity.UID);Require(leader.Team!=null && ReferenceEquals(leader.Team,partner.Team));}
        int wins=Convert.ToInt32(Sql("SELECT count(*) FROM revora_team_arena_statistics WHERE uid=@uid AND won","uid",(long)CareerUid));Require(wins>=1 && wins<=20);
        var clock=Feature("ArenaMatchLedger").GetField("Clock",Hidden);object old=clock.GetValue(null);DateTime now=DateTime.UtcNow;bool accepting=TeamQualifier.AcceptingNewBattles;
        try{clock.SetValue(null,(Func<DateTime>)(()=>now));TeamQualifier.AcceptingNewBattles=true;
            for(;wins<20;wins++){
                now=now.AddMinutes(1);
                foreach(var who in fighters){if(who.Entity.Dead){who.Entity.DeathStamp=Time32.Now.AddSeconds(-20);GeneralData.Revive(new Data(true){dwParam=0},who);}Require(!who.Entity.Dead && who.TeamQualifierGroup==null);who.Entity.Teleport(1002,408,330);}
                string before=MainFifthSnapshot(p);NobilityClaimPacket(p,507);MainClaimPacket(p,5);Require(before==MainFifthSnapshot(p));
                TeamQualifier.AcceptingNewBattles=true;TeamArenaClaimRequest(p,0,0);TeamArenaClaimRequest(enemy,0,0);TeamQualifier.EngagePlayers();if(!ReferenceEquals(p.TeamArenaStatistic.PlayWith,enemy))foreach(var actor in new[]{p,enemy})Console.WriteLine("[main-fifth] signup uid="+actor.Entity.UID+" status="+actor.TeamArenaStatistic.Status+" solo="+actor.ArenaStatistic.Status+" points="+actor.ArenaPoints+" team-points="+actor.TeamArenaStatistic.ArenaPoints+" leader="+actor.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.TeamLeader)+" shackle="+actor.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.SoulShackle)+" group="+(actor.TeamQualifierGroup!=null)+" fenced="+PirateFenced(actor)+" opponent="+(actor.TeamArenaStatistic.PlayWith==null?0:actor.TeamArenaStatistic.PlayWith.Entity.UID)+" accepting="+TeamQualifier.AcceptingNewBattles+" queued="+TeamQualifier.PlayerList.ContainsKey(actor.Entity.UID)+" canjoin="+Call(typeof(TeamQualifier),"CanJoin",actor,Time32.Now)+" map="+actor.Map.ID+" base="+actor.Map.BaseID+" team="+actor.Team.Teammates.Length);Require(ReferenceEquals(p.TeamArenaStatistic.PlayWith,enemy));
                TeamArenaClaimRequest(p,3,1);TeamArenaClaimRequest(enemy,3,1);TeamQualifier.VerifyAwaitingPeople();var group=p.TeamQualifierGroup;Require(group!=null && fighters.All(who=>ReferenceEquals(who.TeamQualifierGroup,group)));
                group.ImportTime=group.CreateTime=Time32.Now.AddSeconds(-13);foreach(var who in fighters){who.Attackable=true;who.CantAttack=Time32.Now.AddSeconds(-1);}
                foreach(var target in new[]{enemy,enemyMate}){int hits=0;while(!target.Entity.Dead && !group.Done && hits++<100){p.Entity.X=target.Entity.X;p.Entity.Y=(ushort)(target.Entity.Y+1);p.Screen.Add(target.Entity);target.Screen.Add(p.Entity);
                    p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);Attack.HandleAttack(new Attack(true){Attacker=CareerUid,Attacked=target.Entity.UID,X=target.Entity.X,Y=target.Entity.Y,AttackType=Attack.Melee},p);}}
                Require(group.Done);group.DoneStamp=Time32.Now.AddSeconds(-6);var limit=DateTime.UtcNow.AddSeconds(10);
                do{TeamQualifier.CheckGroups();if(fighters.All(who=>who.TeamQualifierGroup==null))break;System.Threading.Thread.Sleep(50);}while(DateTime.UtcNow<limit);
                Require(fighters.All(who=>who.TeamQualifierGroup==null));foreach(var who in fighters)Require(EntityTable.SaveEntity(who));
                Require((long)Sql("SELECT count(*) FROM revora_team_arena_statistics WHERE uid=@uid AND won","uid",(long)CareerUid)==wins+1);Console.WriteLine("[main-fifth] saved-team-wins="+(wins+1));
            }
        }finally{clock.SetValue(null,old);TeamQualifier.AcceptingNewBattles=accepting;}
    }
    static void MainFifthCareer(bool reload){
        careerRitesFixture=true;combinedCollectionFixture=true;nextUid=CareerUid;var p=Player(Kernel.Maps[1002],true,1003,true,true);combinedCollectionFixture=false;
        SkillTable.LoadSpells(p);SkillTable.LoadProficiencies(p);SubClassTable.Load(p.Entity);Revora.Database.KnownPersons.LoadMentor(p);
        new CoatStorage().Login(p);new TitleStorage().Login(p);p.LoadItemStats();ChiTable.Load(p);WayofHeroes.Load(p);EntityVariableTable.Load(p);
        var peers=new GameState[4];uint[] ids={4294918201,4294918202,4294918203,4294918215};
        for(int n=0;n<peers.Length;n++){nextUid=ids[n];peers[n]=Player(Kernel.Maps[1002],true);peers[n].LoadItemStats();}
        var all=new[]{p}.Concat(peers).ToArray();foreach(var who in all){Revora.Database.KnownPersons.LoaderFriends(who);Revora.Database.KnownPersons.LoadPartner(who);Revora.Database.KnownPersons.LoadMentor(who);Revora.Database.KnownPersons.LoadEnemy(who);Require(!who.Fake && who.Account.State==AccountTable.AccountState.Player && !PirateFenced(who));}
        if((bool)Sql("SELECT to_regclass('revora_main_fifth_probe') IS NULL")){
            Require(!reload && MainFourthSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_main_fourth_probe")));
            Sql("CREATE TABLE revora_main_fifth_probe(snapshot text,bound_before bigint);INSERT INTO revora_main_fifth_probe VALUES(NULL,@bound)","bound",(long)p.Entity.BoundCps);
        }
        if(Sql("SELECT snapshot FROM revora_main_fifth_probe")==DBNull.Value){
            Require(!reload);MainCareerRoom(p,peers[3],28);MainFifthChampion(p,peers[1]);MainFifthArena(all.Take(4).ToArray());
            for(uint id=501;id<=507;id++)if((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=@stage","uid",(long)CareerUid,"stage",(int)id)==0){
                string before=MainFifthSnapshot(p);MainClaimPacket(p,5);Require(before==MainFifthSnapshot(p));NobilityClaimPacket(p,id);
                Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=@stage AND NOT legacy","uid",(long)CareerUid,"stage",(int)id)==1);
            }
            System.Threading.Tasks.Parallel.For(0,8,_=>MainClaimPacket(p,5));var reward=p.Inventory.Objects.SingleOrDefault(i=>i.ID==3005111);
            if(reward!=null){Require(reward.Bound && reward.StackSize==1);SoulClaimUse(p,reward);}
            Require(p.Entity.BoundCps==Convert.ToUInt32(Sql("SELECT bound_before FROM revora_main_fifth_probe"))+100);
            foreach(var who in all)Require(EntityTable.SaveEntity(who));Sql("UPDATE revora_main_fifth_probe SET snapshot=@snapshot","snapshot",MainFifthSnapshot(p));
        }
        Require(p.Way2Heroes[5].Done && p.Way2Heroes[5].SubStages.Count==7);
        Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid BETWEEN 500 AND 507 AND NOT legacy","uid",(long)CareerUid)==8);
        Require((long)Sql("SELECT count(*) FROM revora_silver_study_bag_uses WHERE uid=@uid AND item_type=3005111 AND boundcps=100","uid",(long)CareerUid)==1);
        Require((long)Sql("SELECT count(*) FROM revora_team_arena_statistics WHERE uid=@uid AND won","uid",(long)CareerUid)==20);
        string exact=MainFifthSnapshot(p);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_main_fifth_probe")));
        for(uint id=501;id<=507;id++)NobilityClaimPacket(p,id);MainClaimPacket(p,5);Require(exact==MainFifthSnapshot(p) && !PirateFenced(p));
        Console.WriteLine("[career-process] career-main-fifth-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Main fifth career did not close.");
    }
    static string MainSixthGrowthSnapshot(GameState p,GameState recipient){return Convert.ToString(Sql(@"SELECT json_build_object(
'career',@career::json,'recipient',@recipient::json,
'awards',(SELECT json_agg(a ORDER BY id) FROM revora_enlight_awards a WHERE donor=@uid OR recipient=@uid),
'point_day',(SELECT row_to_json(d) FROM revora_enlight_days d WHERE uid=@uid))::text",
"career",MainFifthSnapshot(p),"recipient",EnlightCreditSnapshot(recipient),"uid",(long)p.Entity.UID));}
    static void MainSixthStudy(GameState p,byte power,int keepScore=75){
        var current=p.ChiPowers[power-1];uint locks=0;
        for(int n=0;n<4;n++){var a=current.Attributes[n];if(a.Type!=0 && 100*a.Value/Enums.ChiMaxValues(a.Type)>=keepScore)locks|=1u<<n;}
        Require(locks<15);uint cost=50+50*(uint)Enumerable.Range(0,4).Count(n=>(locks&(1u<<n))!=0);
        if(p.ChiPoints<cost)ChiClaimRequest(p,Chi.BuyStrength,power);
        uint before=p.ChiPoints;var frame=new Chi(true){UID=p.Entity.UID,Type=Chi.Study,Mode=(Enums.ChiPowerType)power,dwParam=locks}.ToArray();
        Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,frame.Length-8,frame);Revora.Network.PacketHandler.HandlePacket(frame,p);
        Require(!PirateFenced(p) && p.ChiPoints==before-cost);
    }
    static void MainSixthGrowth(bool reload){
        careerRitesFixture=true;combinedCollectionFixture=true;nextUid=CareerUid;var p=Player(Kernel.Maps[1002],true,1003,true,true);combinedCollectionFixture=false;
        SkillTable.LoadSpells(p);SkillTable.LoadProficiencies(p);SubClassTable.Load(p.Entity);Revora.Database.KnownPersons.LoadMentor(p);
        new CoatStorage().Login(p);new TitleStorage().Login(p);p.LoadItemStats();ChiTable.Load(p);WayofHeroes.Load(p);EntityVariableTable.Load(p);
        nextUid=4294918104;var recipient=Player(Kernel.Maps[1002],true);recipient.LoadItemStats();ChiTable.Load(recipient);WayofHeroes.Load(recipient);
        nextUid=4294918214;var storage=Player(Kernel.Maps[1002],true);storage.LoadItemStats();
        foreach(var who in new[]{p,recipient,storage})Require(!who.Fake && who.Account.State==AccountTable.AccountState.Player && !PirateFenced(who));
        if((bool)Sql("SELECT to_regclass('revora_main_sixth_growth_probe') IS NULL")){
            Require(!reload && MainFifthSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_main_fifth_probe")));
            Sql("CREATE TABLE revora_main_sixth_growth_probe(snapshot text);INSERT INTO revora_main_sixth_growth_probe VALUES(NULL)");
        }
        if(Sql("SELECT snapshot FROM revora_main_sixth_growth_probe")==DBNull.Value){
            Require(!reload);MainCareerRoom(p,storage,29);
            if((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=605","uid",(long)CareerUid)==0){
                if(p.ChiPowers.Sum(x=>x.Points)<500){string before=MainSixthGrowthSnapshot(p,recipient);NobilityClaimPacket(p,605);Require(before==MainSixthGrowthSnapshot(p,recipient));}
                if(p.ChiPowers.Count==0){if(p.ChiPoints<300)ChiClaimRequest(p,Chi.BuyStrength,1);ChiClaimRequest(p,Chi.Unlock,1);Require(p.ChiPowers.Count==1);}
                for(int n=0;p.ChiPowers[0].Points<300 && n<100;n++)MainSixthStudy(p,1);Require(p.ChiPowers[0].Points>=300);
                if(p.ChiPowers.Count==1){if(p.ChiPoints<300)ChiClaimRequest(p,Chi.BuyStrength,2);ChiClaimRequest(p,Chi.Unlock,2);Require(p.ChiPowers.Count==2);}
                for(int n=0;p.ChiPowers.Sum(x=>x.Points)<500 && n<100;n++)MainSixthStudy(p,2);Require(p.ChiPowers.Sum(x=>x.Points)>=500);
                Console.WriteLine("[main-sixth] earned-chi-score="+p.ChiPowers.Sum(x=>x.Points));
                System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,605));var prize=p.Inventory.Objects.Single(i=>i.ID==3005126);Require(prize.Bound && prize.StackSize==1);uint points=p.ChiPoints;SoulClaimUse(p,prize);Require(p.ChiPoints==points+500);
            }
            if((long)Sql("SELECT count(*) FROM revora_enlight_awards WHERE donor=@uid AND NOT legacy","uid",(long)CareerUid)==0){
                p.Entity.Teleport(1002,327,303);recipient.Entity.Teleport(1002,328,303);
                string before=MainSixthGrowthSnapshot(p,recipient);NobilityClaimPacket(p,606);Require(before==MainSixthGrowthSnapshot(p,recipient));
                lock(p.ItemSyncRoot)Call(Feature("EnlightenmentProgress"),"RefreshDay",p,null);lock(recipient.ItemSyncRoot)Call(Feature("EnlightenmentProgress"),"RefreshDay",recipient,null);
                ushort points=p.Entity.EnlightenPoints;byte received=recipient.Entity.ReceivedEnlightenPoints;Require(points>=100 && p.Entity.Level>=recipient.Entity.Level+20);
                var frame=EnlightFrame(p,recipient);Enlight.Process(frame,p);Require(!PirateFenced(p) && !PirateFenced(recipient) && p.Entity.EnlightenPoints==points-100 && recipient.Entity.ReceivedEnlightenPoints==received+1);
                before=MainSixthGrowthSnapshot(p,recipient);Enlight.Process(frame,p);Require(before==MainSixthGrowthSnapshot(p,recipient));
            }
            if((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=606","uid",(long)CareerUid)==0){
                var old=p.Inventory.Objects.Select(i=>i.UID).ToArray();System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,606));var prizes=p.Inventory.Objects.Where(i=>!old.Contains(i.UID)).ToArray();Require(prizes.Length==3 && prizes.All(i=>i.ID==1088001 && i.Bound && i.StackSize==1));
            }
            string pending=MainSixthGrowthSnapshot(p,recipient);MainClaimPacket(p,6);Require(pending==MainSixthGrowthSnapshot(p,recipient));
            foreach(var who in new[]{p,recipient,storage})Require(EntityTable.SaveEntity(who));Sql("UPDATE revora_main_sixth_growth_probe SET snapshot=@snapshot","snapshot",MainSixthGrowthSnapshot(p,recipient));
        }
        Require(!p.Way2Heroes[6].Done && p.Way2Heroes[6].SubStages[605].Done && p.Way2Heroes[6].SubStages[606].Done);
        Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid IN(605,606) AND NOT legacy","uid",(long)CareerUid)==2);
        Require((long)Sql("SELECT count(*) FROM revora_enlight_awards WHERE donor=@uid AND NOT legacy","uid",(long)CareerUid)==1 && p.ChiPowers.Sum(x=>x.Points)>=500);
        string exact=MainSixthGrowthSnapshot(p,recipient);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_main_sixth_growth_probe")));
        NobilityClaimPacket(p,605);NobilityClaimPacket(p,606);MainClaimPacket(p,6);Require(exact==MainSixthGrowthSnapshot(p,recipient) && !PirateFenced(p));
        Console.WriteLine("[career-process] career-main-sixth-growth-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Main sixth growth did not close.");
    }
    static string MainSixthSoulSnapshot(GameState p,GameState recipient){return Convert.ToString(Sql(@"SELECT json_build_object(
'career',@career::json,
'effects',(SELECT json_agg(a ORDER BY a.uid,a.addingtype) FROM itemadding a JOIN items i ON i.uid=a.uid WHERE i.entityid=@uid),
'upgrades',(SELECT json_agg(u ORDER BY id) FROM revora_equipment_upgrades u WHERE uid=@uid))::text",
"career",MainSixthGrowthSnapshot(p,recipient),"uid",(long)p.Entity.UID));}
    static void MainSixthSoul(bool reload){
        careerRitesFixture=true;combinedCollectionFixture=true;nextUid=CareerUid;var p=Player(Kernel.Maps[1002],true,1003,true,true);combinedCollectionFixture=false;
        SkillTable.LoadSpells(p);SkillTable.LoadProficiencies(p);SubClassTable.Load(p.Entity);Revora.Database.KnownPersons.LoadMentor(p);
        new CoatStorage().Login(p);new TitleStorage().Login(p);p.LoadItemStats();ChiTable.Load(p);WayofHeroes.Load(p);EntityVariableTable.Load(p);
        nextUid=4294918104;var recipient=Player(Kernel.Maps[1002],true);recipient.LoadItemStats();ChiTable.Load(recipient);WayofHeroes.Load(recipient);
        nextUid=4294918213;var storage=Player(Kernel.Maps[1002],true);storage.LoadItemStats();
        foreach(var who in new[]{p,recipient,storage})Require(!who.Fake && who.Account.State==AccountTable.AccountState.Player && !PirateFenced(who));
        if((bool)Sql("SELECT to_regclass('revora_main_sixth_soul_probe') IS NULL")){
            Require(!reload && MainSixthGrowthSnapshot(p,recipient)==Convert.ToString(Sql("SELECT snapshot FROM revora_main_sixth_growth_probe")));
            var worn=p.Equipment.TryGetItem(8);Require(worn!=null && worn.Bound && worn.ID/1000==160);
            uint maximum=ConquerItemInformation.BaseInformations.Values.Where(i=>i.ID/1000==160 && i.Level<=p.Entity.Level).OrderByDescending(i=>i.Level).First().ID;
            var soul=p.Warehouses[(Revora.Game.ConquerStructures.Warehouse.WarehouseID)8].Objects.Where(i=>i.Bound && i.Lock==0 && i.Days==0 && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7 && (bool)Call(Feature("ArtifactApplication"),"Fits",maximum,i.ID)).OrderBy(i=>i.UID).First();
            Sql("CREATE TABLE revora_main_sixth_soul_probe(snapshot text,target bigint,soul bigint,soul_type integer);INSERT INTO revora_main_sixth_soul_probe VALUES(NULL,@target,@soul,@type)","target",(long)worn.UID,"soul",(long)soul.UID,"type",(int)soul.ID);
            Console.WriteLine("[main-sixth] selected-owned-soul="+soul.ID+" target="+worn.ID);
        }
        uint targetUid=Convert.ToUInt32(Sql("SELECT target FROM revora_main_sixth_soul_probe")),soulUid=Convert.ToUInt32(Sql("SELECT soul FROM revora_main_sixth_soul_probe")),soulType=Convert.ToUInt32(Sql("SELECT soul_type FROM revora_main_sixth_soul_probe"));
        var boots=p.Inventory.Objects.Concat(p.Equipment.GetItems()).Single(i=>i.UID==targetUid);
        if(Sql("SELECT snapshot FROM revora_main_sixth_soul_probe")==DBNull.Value){
            Require(!reload);MainCareerRoom(p,storage,23);
            bool applied=boots.Purification.PurificationItemID==soulType && boots.Purification.PurificationDuration>0 && (bool)Sql("SELECT EXISTS(SELECT 1 FROM revora_achievement_evidence WHERE uid=@uid AND source='artifact-apply' AND source_key=@evidence)","uid",(long)CareerUid,"evidence",targetUid+"-"+soulUid);
            if(!applied){
                if(boots.Position!=0)RetainedUpgradeUsage(p,boots.UID,8,ItemUsage.Mode.UnequipItem);Require(p.Inventory.ContainsUID(boots.UID));
                if(!p.Inventory.ContainsUID(soulUid)){var bank=Kernel.Maps[1002].Npcs[8];p.Entity.Teleport(1002,bank.X,bank.Y);GrailWarehouse(p,soulUid,2,8);Require(p.Inventory.ContainsUID(soulUid));}
                var clock=Feature("EquipmentUpgrades").GetField("Clock",Hidden);object old=clock.GetValue(null);DateTime now=DateTime.UtcNow;
                try{clock.SetValue(null,(Func<DateTime>)(()=>now));
                    for(int attempt=0;!(bool)Call(Feature("ArtifactApplication"),"Fits",boots.ID,soulType) && attempt<300;attempt++){
                        if(boots.Durability<boots.MaximDurability){RetainedUpgradeUsage(p,boots.UID,0,ItemUsage.Mode.Repair);Require(boots.Durability==boots.MaximDurability && !PirateFenced(p));}
                        var meteor=p.Inventory.Objects.FirstOrDefault(i=>i.ID==1088001);if(meteor==null){CareerBuy(p,1088001);meteor=p.Inventory.Objects.First(i=>i.ID==1088001);}
                        now=now.AddSeconds(2);ushort stack=meteor.StackSize;uint before=boots.ID;RetainedUpgradeUsage(p,boots.UID,meteor.UID,ItemUsage.Mode.MeteorUpgrade);
                        Require(!PirateFenced(p) && (stack==1?!p.Inventory.ContainsUID(meteor.UID):meteor.StackSize==stack-1));
                        if(boots.ID!=before)Console.WriteLine("[main-sixth] boots-level="+ConquerItemInformation.BaseInformations[boots.ID].Level);
                    }
                }finally{clock.SetValue(null,old);}
                Require((bool)Call(Feature("ArtifactApplication"),"Fits",boots.ID,soulType));
                int need=ConquerItemInformation.BaseInformations[soulType].PurificationMeteorNeed;
                Func<int> units=()=>p.Inventory.Objects.Where(i=>i.ID==1088001 || i.ID==720027).Sum(i=>(int)i.StackSize*(i.ID==720027?10:1));
                while(units()<need){Require(p.Inventory.Count<=30);for(int n=0;n<10;n++)CareerBuy(p,1088001);p.Entity.Teleport(1002,400,350);HeartTalk(p,11275,1);}
                int paid=units();var apply=RetainedArtifactPacket(boots.UID,soulUid);Purification.Process(apply,p);
                Require(!PirateFenced(p) && !p.Inventory.ContainsUID(soulUid) && paid-units()==need && boots.Purification.PurificationItemID==soulType && boots.Purification.PurificationLevel==7 && boots.Purification.PurificationDuration>0);
                string once=MainSixthSoulSnapshot(p,recipient);Purification.Process(apply,p);Require(once==MainSixthSoulSnapshot(p,recipient));
                string unequipped=MainSixthSoulSnapshot(p,recipient);NobilityClaimPacket(p,604);Require(unequipped==MainSixthSoulSnapshot(p,recipient));
            }
            if(boots.Position==0)RetainedUpgradeUsage(p,boots.UID,8,ItemUsage.Mode.EquipItem);Require(p.Equipment.TryGetItem(8).UID==boots.UID);
            if((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=604","uid",(long)CareerUid)==0){
                var before=p.Inventory.Objects.Select(i=>i.UID).ToArray();System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,604));var prize=p.Inventory.Objects.Single(i=>!before.Contains(i.UID));Require(prize.ID==723694 && prize.Bound && prize.StackSize==1);
            }
            string pending=MainSixthSoulSnapshot(p,recipient);MainClaimPacket(p,6);Require(pending==MainSixthSoulSnapshot(p,recipient));
            foreach(var who in new[]{p,recipient,storage})Require(EntityTable.SaveEntity(who));Sql("UPDATE revora_main_sixth_soul_probe SET snapshot=@snapshot","snapshot",MainSixthSoulSnapshot(p,recipient));
        }
        Require(!p.Way2Heroes[6].Done && p.Way2Heroes[6].SubStages[604].Done && boots.Position==8 && boots.Purification.PurificationLevel==7 && (bool)Call(Feature("ArtifactApplication"),"Fits",boots.ID,soulType));
        Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=604 AND NOT legacy","uid",(long)CareerUid)==1);
        string exact=MainSixthSoulSnapshot(p,recipient);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_main_sixth_soul_probe")));
        NobilityClaimPacket(p,604);MainClaimPacket(p,6);Require(exact==MainSixthSoulSnapshot(p,recipient) && !PirateFenced(p));
        Console.WriteLine("[career-process] career-main-sixth-soul-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Main sixth soul did not close.");
    }
    static string MainSixthGuildSnapshot(GameState p,GameState recipient){return Convert.ToString(Sql(@"SELECT json_build_object(
'career',@career::json,'participation',(SELECT row_to_json(r) FROM revora_guild_pk_participation r WHERE uid=@uid),
'contest',(SELECT row_to_json(c) FROM revora_guild_contests c WHERE id=(SELECT run FROM revora_main_sixth_guild_probe)),
'seeds',(SELECT json_agg(s ORDER BY rank) FROM revora_guild_contest_seeds s WHERE run=(SELECT run FROM revora_main_sixth_guild_probe)),
'kills',(SELECT json_agg(k ORDER BY life) FROM revora_guild_contest_kills k WHERE run=(SELECT run FROM revora_main_sixth_guild_probe)),
'matches',(SELECT json_agg(m ORDER BY id) FROM revora_guild_pk_matches m WHERE contest=(SELECT run FROM revora_main_sixth_guild_probe)),
'entries',(SELECT json_agg(e ORDER BY e.run,e.uid) FROM revora_guild_pk_entries e JOIN revora_guild_pk_matches m ON m.id=e.run WHERE m.contest=(SELECT run FROM revora_main_sixth_guild_probe)))::text",
"career",MainSixthSoulSnapshot(p,recipient),"uid",(long)p.Entity.UID));}
    static void MainSixthGuild(bool reload){
        careerRitesFixture=true;combinedCollectionFixture=true;nextUid=CareerUid;var p=Player(Kernel.Maps[1002],true,1003,true,true);combinedCollectionFixture=false;
        SkillTable.LoadSpells(p);SkillTable.LoadProficiencies(p);SubClassTable.Load(p.Entity);Revora.Database.KnownPersons.LoadMentor(p);
        new CoatStorage().Login(p);new TitleStorage().Login(p);p.LoadItemStats();ChiTable.Load(p);WayofHeroes.Load(p);EntityVariableTable.Load(p);
        nextUid=4294918104;var recipient=Player(Kernel.Maps[1002],true);recipient.LoadItemStats();ChiTable.Load(recipient);WayofHeroes.Load(recipient);
        nextUid=4294917701;var rival=Player(Kernel.Maps[1002],true);rival.LoadItemStats();
        nextUid=4294918212;var storage=Player(Kernel.Maps[1002],true);storage.LoadItemStats();
        foreach(var who in new[]{p,recipient,rival,storage})Require(!who.Fake && who.Account.State==AccountTable.AccountState.Player && !PirateFenced(who));
        Require(p.Guild!=null && rival.Guild!=null && p.Guild.ID!=rival.Guild.ID);
        if((bool)Sql("SELECT to_regclass('revora_main_sixth_guild_probe') IS NULL")){
            Require(!reload && MainSixthSoulSnapshot(p,recipient)==Convert.ToString(Sql("SELECT snapshot FROM revora_main_sixth_soul_probe")));
            DateTime local=new DateTime(2097,1,1,20,30,0,DateTimeKind.Local);while(local.DayOfWeek!=DayOfWeek.Monday)local=local.AddDays(1);
            Sql("CREATE TABLE revora_main_sixth_guild_probe(snapshot text,bound_before bigint,run uuid,starts timestamptz);INSERT INTO revora_main_sixth_guild_probe VALUES(NULL,@bound,NULL,@start)","bound",(long)p.Entity.BoundCps,"start",new DateTimeOffset(local));
        }
        if(Sql("SELECT snapshot FROM revora_main_sixth_guild_probe")==DBNull.Value){
            Require(!reload);MainCareerRoom(p,storage,25);
            if((long)Sql("SELECT count(*) FROM revora_guild_admissions WHERE uid=@uid","uid",(long)CareerUid)==0){
                var guild=p.Guild;ushort previousRank=(ushort)p.AsMember.Rank;var leaderMember=guild.Members.Values.Single(m=>m.Rank==Enums.GuildMemberRank.GuildLeader);GameState leader;
                p.Entity.Teleport(1002,409,288);storage.Entity.Teleport(1002,410,288);
                foreach(var who in new[]{p,storage}){Revora.Database.KnownPersons.LoaderFriends(who);Revora.Database.KnownPersons.LoadPartner(who);Revora.Database.KnownPersons.LoadMentor(who);Revora.Database.KnownPersons.LoadEnemy(who);}
                if(leaderMember.ID==CareerUid){
                    if(storage.Guild==null){GuildClaimPacket(p,GuildCommand.InviteRequest,storage.Entity.UID);GuildClaimPacket(storage,GuildCommand.JoinRequest,CareerUid);Require(ReferenceEquals(storage.Guild,guild));}
                    RetainedOfficeRank(p,storage.Entity.Name,(ushort)Enums.GuildMemberRank.GuildLeader);Require(storage.AsMember.Rank==Enums.GuildMemberRank.GuildLeader);leader=storage;
                }else{nextUid=leaderMember.ID;leader=Player(Kernel.Maps[1002],true);leader.LoadItemStats();Require(!leader.Fake && leader.Account.State==AccountTable.AccountState.Player);}
                leader.Entity.Teleport(1002,410,288);GuildClaimPacket(p,GuildCommand.Quit,guild.ID);Require(p.Guild==null);
                GuildClaimPacket(leader,GuildCommand.InviteRequest,CareerUid);GuildClaimPacket(p,GuildCommand.JoinRequest,leader.Entity.UID);Require(ReferenceEquals(p.Guild,guild));
                RetainedOfficeRank(leader,p.Entity.Name,previousRank);Require((ushort)p.AsMember.Rank==previousRank && !PirateFenced(p) && !PirateFenced(leader));
                Require(EntityTable.SaveEntity(p) && EntityTable.SaveEntity(leader));
                Require((long)Sql("SELECT count(*) FROM revora_guild_admissions WHERE uid=@uid AND guild_id=@guild","uid",(long)CareerUid,"guild",(long)guild.ID)==1);
                Console.WriteLine("[main-sixth] normal-guild-readmission-committed");
            }
            var parent=Feature("GuildContestCoordinator");var clock=parent.GetField("Clock",Hidden);object old=clock.GetValue(null);
            DateTime starts=DateTime.SpecifyKind((DateTime)Sql("SELECT starts AT TIME ZONE 'UTC' FROM revora_main_sixth_guild_probe"),DateTimeKind.Utc),now=starts.AddMinutes(-4);
            try{clock.SetValue(null,(Func<DateTime>)(()=>now));
                object previousRun=Sql("SELECT run FROM revora_main_sixth_guild_probe");
                if(previousRun!=DBNull.Value && (Convert.ToString(Sql("SELECT state FROM revora_guild_contests WHERE id=@run","run",previousRun))=="cancelled" || (bool)Sql("SELECT EXISTS(SELECT 1 FROM revora_guild_pk_matches WHERE contest=@run AND state='cancelled')","run",previousRun))){
                    starts=starts.AddDays(7);now=starts.AddMinutes(-4);Sql("UPDATE revora_main_sixth_guild_probe SET run=NULL,starts=@start","start",new DateTimeOffset(starts));previousRun=DBNull.Value;
                }
                if(previousRun==DBNull.Value){
                    // A persisted ordinary opponent equipment prerequisite; retained career gear is untouched.
                    if(rival.Equipment.TryGetItem(4)==null){
                        uint type=ConquerItemInformation.BaseInformations.Values.Where(i=>i.ID/1000==410 && i.ID%10==9 && i.Level<=15 && i.Proficiency==0).OrderBy(i=>i.Level).First().ID;
                        var weapon=rival.Inventory.Objects.FirstOrDefault(i=>i.ID==type);if(weapon==null){var info=ConquerItemInformation.BaseInformations[type];weapon=new ConquerItem(true){ID=type,Plus=12,Bound=true,StackSize=1,Durability=info.Durability,MaximDurability=info.Durability};Require(rival.Inventory.Add(weapon,Enums.ItemUse.CreateAndAdd));}
                        RetainedUpgradeUsage(rival,weapon.UID,4,ItemUsage.Mode.EquipItem);Require(rival.Equipment.TryGetItem(4).UID==weapon.UID);rival.LoadItemStats();Require(EntityTable.SaveEntity(rival));
                    }
                    foreach(var who in new[]{p,rival})who.Entity.Teleport(1002,409,288);
                    Call(parent,"Tick");Guid created=(Guid)Sql("SELECT id FROM revora_guild_contests WHERE starts_at=@start","start",new DateTimeOffset(starts));Sql("UPDATE revora_main_sixth_guild_probe SET run=@run","run",created);
                    foreach(var who in new[]{p,rival}){HeartTalk(who,706070,1);Require(who.Map.BaseID==700);}
                    string registered=MainSixthGuildSnapshot(p,recipient);NobilityClaimPacket(p,607);MainClaimPacket(p,6);Require(registered==MainSixthGuildSnapshot(p,recipient));
                    now=starts;Call(parent,"Tick");
                    foreach(var who in new[]{p,rival})for(int n=0;n<(ReferenceEquals(who,p)?2:1);n++){
                        var monster=who.Map.Entities.Values.First(m=>!m.Dead);Console.WriteLine("[main-sixth] hunt uid="+who.Entity.UID+" hp="+monster.Hitpoints+" attack="+who.Entity.MinAttack);
                        int hits=0;while(!monster.Dead && hits++<1000){who.Entity.X=monster.X;who.Entity.Y=(ushort)(monster.Y+1);who.Screen.Add(monster);who.Entity.AttackStamp=Time32.Now.AddSeconds(-2);
                            Attack.HandleAttack(new Attack(true){AttackType=Attack.Melee,Attacker=who.Entity.UID,Attacked=monster.UID,X=monster.X,Y=monster.Y},who);}
                        Require(monster.Dead && !PirateFenced(who));now=now.AddSeconds(2);Call(parent,"Tick");
                    }
                    now=starts.AddHours(2);Call(parent,"Tick");Require(p.Entity.MapID==1002 && rival.Entity.MapID==1002);
                }
                Guid run=(Guid)Sql("SELECT run FROM revora_main_sixth_guild_probe");
                Require((long)Sql("SELECT count(*) FROM revora_guild_contest_kills WHERE run=@run","run",run)==3);
                Require((long)Sql("SELECT count(*) FROM revora_guild_contest_seeds WHERE run=@run","run",run)==2);
                now=starts.AddHours(2).AddSeconds(32);Call(parent,"Tick");
                Guid match=(Guid)Sql("SELECT id FROM revora_guild_pk_matches WHERE contest=@run AND round=4","run",run);
                DateTime begins=DateTime.SpecifyKind((DateTime)Sql("SELECT starts_at AT TIME ZONE 'UTC' FROM revora_guild_pk_matches WHERE id=@run","run",match),DateTimeKind.Utc);
                if(Convert.ToString(Sql("SELECT state FROM revora_guild_pk_matches WHERE id=@run","run",match))!="complete"){
                    now=begins.AddMinutes(-4);Call(parent,"Tick");foreach(var who in new[]{p,rival}){who.Entity.Teleport(1002,409,288);Console.WriteLine("[main-sixth] entering guild="+who.Entity.GuildID+" uid="+who.Entity.UID+" membership="+Convert.ToString(Sql("SELECT json_build_object('joined',joined_at,'guild',guild_id)::text FROM revora_guild_admissions WHERE uid=@uid","uid",(long)who.Entity.UID)));HeartTalk(who,706072,1);Console.WriteLine("[main-sixth] entered-map="+who.Map.ID+" base="+who.Map.BaseID+" fenced="+PirateFenced(who));Require(who.Map.BaseID==700);}
                    string waiting=MainSixthGuildSnapshot(p,recipient);NobilityClaimPacket(p,607);Require(waiting==MainSixthGuildSnapshot(p,recipient));
                    now=begins;Call(parent,"Tick");now=now.AddSeconds(2);Call(parent,"Tick");
                    Require((long)Sql("SELECT count(*) FROM revora_guild_pk_participation WHERE uid=@uid AND run=@run","uid",(long)CareerUid,"run",match)==1);
                    // Both sides actively entered; let the unscored match reach its normal draw deadline.
                    now=DateTime.SpecifyKind((DateTime)Sql("SELECT ends_at AT TIME ZONE 'UTC' FROM revora_guild_pk_matches WHERE id=@run","run",match),DateTimeKind.Utc);Call(parent,"Tick");now=now.AddSeconds(32);Call(parent,"Tick");Require(p.Entity.MapID==1002 && rival.Entity.MapID==1002);
                }
                Require(Convert.ToString(Sql("SELECT state FROM revora_guild_pk_matches WHERE id=@run","run",match))=="complete");
            }finally{clock.SetValue(null,old);}
            for(uint id=601;id<=609;id++)if((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=@stage","uid",(long)CareerUid,"stage",(int)id)==0){
                string before=MainSixthGuildSnapshot(p,recipient);MainClaimPacket(p,6);Require(before==MainSixthGuildSnapshot(p,recipient));NobilityClaimPacket(p,id);
                Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=@stage AND NOT legacy","uid",(long)CareerUid,"stage",(int)id)==1);
            }
            System.Threading.Tasks.Parallel.For(0,8,_=>MainClaimPacket(p,6));var reward=p.Inventory.Objects.SingleOrDefault(i=>i.ID==3005112);
            if(reward!=null){Require(reward.Bound && reward.StackSize==1);SoulClaimUse(p,reward);}
            Require(p.Entity.BoundCps==Convert.ToUInt32(Sql("SELECT bound_before FROM revora_main_sixth_guild_probe"))+200);
            foreach(var who in new[]{p,recipient,rival,storage})Require(EntityTable.SaveEntity(who));Sql("UPDATE revora_main_sixth_guild_probe SET snapshot=@snapshot","snapshot",MainSixthGuildSnapshot(p,recipient));
        }
        Require(p.Way2Heroes[6].Done && p.Way2Heroes[6].SubStages.Count==9);
        Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid BETWEEN 600 AND 609 AND NOT legacy","uid",(long)CareerUid)==10);
        Require((long)Sql("SELECT count(*) FROM revora_silver_study_bag_uses WHERE uid=@uid AND item_type=3005112 AND boundcps=200","uid",(long)CareerUid)==1);
        string exact=MainSixthGuildSnapshot(p,recipient);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_main_sixth_guild_probe")));
        for(uint id=601;id<=609;id++)NobilityClaimPacket(p,id);MainClaimPacket(p,6);Require(exact==MainSixthGuildSnapshot(p,recipient) && !PirateFenced(p));
        Console.WriteLine("[career-process] career-main-sixth-guild-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Main sixth guild did not close.");
    }
    static string MainSeventhSnapshot(GameState p,GameState recipient){return Convert.ToString(Sql(@"SELECT json_build_object(
'career',@career::json,'jiang',@jiang::json,'pills',(SELECT json_agg(u ORDER BY id) FROM revora_jiang_pill_uses u WHERE uid=@uid))::text",
"career",MainSixthGuildSnapshot(p,recipient),"jiang",JiangClaimSnapshot(p),"uid",(long)p.Entity.UID));}
    static void MainSeventhTrain(GameState p,int stage,int star){
        uint cp=p.Entity.ConquerPoints,cost=Revora.Game.JiangHu.GetTrainingCost(p.Entity.MyJiang.RoundBuyPoints);ushort study=p.Entity.SubClasses.StudyPoints;
        var frame=new byte[28];Revora.Network.Writer.WriteUInt16(20,0,frame);Revora.Network.Writer.WriteUInt16(2702,2,frame);frame[9]=1;frame[10]=(byte)(star+1);frame[11]=(byte)(stage+1);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,20,frame);
        Revora.Network.PacketHandler.HandlePacket(frame,p);Require(!PirateFenced(p) && p.Entity.ConquerPoints==cp-cost && p.Entity.SubClasses.StudyPoints==study+20);
    }
    static void MainSeventhCareer(bool reload){
        careerRitesFixture=true;combinedCollectionFixture=true;nextUid=CareerUid;var p=Player(Kernel.Maps[1002],true,1003,true,true);combinedCollectionFixture=false;
        SkillTable.LoadSpells(p);SkillTable.LoadProficiencies(p);SubClassTable.Load(p.Entity);Revora.Database.KnownPersons.LoadMentor(p);
        new CoatStorage().Login(p);new TitleStorage().Login(p);p.LoadItemStats();ChiTable.Load(p);WayofHeroes.Load(p);EntityVariableTable.Load(p);
        nextUid=4294918104;var recipient=Player(Kernel.Maps[1002],true);recipient.LoadItemStats();ChiTable.Load(recipient);WayofHeroes.Load(recipient);
        nextUid=4294918211;var storage=Player(Kernel.Maps[1002],true);storage.LoadItemStats();
        foreach(var who in new[]{p,recipient,storage})Require(!who.Fake && who.Account.State==AccountTable.AccountState.Player && !PirateFenced(who));
        if((bool)Sql("SELECT to_regclass('revora_main_seventh_probe') IS NULL")){
            Require(!reload && MainSixthGuildSnapshot(p,recipient)==Convert.ToString(Sql("SELECT snapshot FROM revora_main_sixth_guild_probe")));
            Sql("CREATE TABLE revora_main_seventh_probe(snapshot text,bound_before bigint);INSERT INTO revora_main_seventh_probe VALUES(NULL,@bound)","bound",(long)p.Entity.BoundCps);
        }
        Call(typeof(Revora.Database.JiangHu),"LoadClient",p);
        if(Sql("SELECT snapshot FROM revora_main_seventh_probe")==DBNull.Value){
            Require(!reload);MainCareerRoom(p,storage,22);
            if(p.ChiPowers.Sum(x=>x.Points)<1300){string before=MainSeventhSnapshot(p,recipient);NobilityClaimPacket(p,702);Require(before==MainSeventhSnapshot(p,recipient));}
            for(byte power=1;power<=4;power++){
                if(p.ChiPowers.Count<power){if(p.ChiPoints<300)ChiClaimRequest(p,Chi.BuyStrength,power);ChiClaimRequest(p,Chi.Unlock,power);Require(p.ChiPowers.Count==power);}
                for(int n=0;p.ChiPowers[power-1].Points<330 && n<150;n++)MainSixthStudy(p,power,85);
                Require(p.ChiPowers[power-1].Points>=330);Console.WriteLine("[main-seventh] chi-power="+power+" score="+p.ChiPowers[power-1].Points);
            }
            Require(p.ChiPowers.Sum(x=>x.Points)>=1300);
            if(p.Entity.MyJiang==null){var create=new byte[19];Revora.Network.Writer.WriteUInt16(11,0,create);Revora.Network.Writer.WriteUInt16(2700,2,create);create[4]=1;create[5]=1;create[6]=4;Revora.Network.Writer.WriteString("Path",7,create);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,11,create);Revora.Network.PacketHandler.HandlePacket(create,p);Require(p.Entity.MyJiang!=null && !PirateFenced(p));}
            if((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=702","uid",(long)CareerUid)==0){
                var old=p.Inventory.Objects.Select(i=>i.UID).ToArray();System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,702));var prize=p.Inventory.Objects.Single(i=>!old.Contains(i.UID));Require(prize.ID==3003126 && prize.Bound && prize.StackSize==1);SoulClaimUse(p,prize);
            }
            if(p.Entity.MyJiang.Inner_Strength<10000){string before=MainSeventhSnapshot(p,recipient);NobilityClaimPacket(p,703);Require(before==MainSeventhSnapshot(p,recipient));}
            for(int n=0;p.Entity.MyJiang.Inner_Strength<10000 && n<300;n++){
                var choice=Enumerable.Range(0,9).Where(stage=>p.Entity.MyJiang.Stagers[stage].Activate).SelectMany(stage=>Enumerable.Range(0,9).Select(star=>new{Stage=stage,Star=star,Value=p.Entity.MyJiang.Stagers[stage].Stars[star]})).OrderBy(x=>x.Value.Activate?1:0).ThenBy(x=>x.Value.Level).ThenBy(x=>x.Stage).ThenBy(x=>x.Star).First();
                MainSeventhTrain(p,choice.Stage,choice.Star);if(n%9==0)Console.WriteLine("[main-seventh] jiang-score="+p.Entity.MyJiang.Inner_Strength);
            }
            Require(p.Entity.MyJiang.Inner_Strength>=10000 && !PirateFenced(p));
            for(uint id=701;id<=711;id++)if((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=@stage","uid",(long)CareerUid,"stage",(int)id)==0){
                string before=MainSeventhSnapshot(p,recipient);MainClaimPacket(p,7);Require(before==MainSeventhSnapshot(p,recipient));NobilityClaimPacket(p,id);
                Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=@stage AND NOT legacy","uid",(long)CareerUid,"stage",(int)id)==1);
            }
            System.Threading.Tasks.Parallel.For(0,8,_=>MainClaimPacket(p,7));var reward=p.Inventory.Objects.SingleOrDefault(i=>i.ID==3005113);
            if(reward!=null){Require(reward.Bound && reward.StackSize==1);SoulClaimUse(p,reward);}
            Require(p.Entity.BoundCps==Convert.ToUInt32(Sql("SELECT bound_before FROM revora_main_seventh_probe"))+300);
            Require((long)Sql("SELECT count(*) FROM revora_jiang_pill_uses WHERE uid=@uid AND consumed_at IS NOT NULL","uid",(long)CareerUid)>=1);
            foreach(var who in new[]{p,recipient,storage})Require(EntityTable.SaveEntity(who));Revora.Database.JiangHu.SaveJiangHu(p);Sql("UPDATE revora_main_seventh_probe SET snapshot=@snapshot","snapshot",MainSeventhSnapshot(p,recipient));
        }
        Require(p.Way2Heroes[7].Done && p.Way2Heroes[7].SubStages.Count==11 && p.ChiPowers.Sum(x=>x.Points)>=1300 && p.Entity.MyJiang.Inner_Strength>=10000);
        Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid BETWEEN 700 AND 711 AND NOT legacy","uid",(long)CareerUid)==12);
        Require((long)Sql("SELECT count(*) FROM revora_silver_study_bag_uses WHERE uid=@uid AND item_type=3005113 AND boundcps=300","uid",(long)CareerUid)==1);
        string exact=MainSeventhSnapshot(p,recipient);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_main_seventh_probe")));
        for(uint id=701;id<=711;id++)NobilityClaimPacket(p,id);MainClaimPacket(p,7);Require(exact==MainSeventhSnapshot(p,recipient) && !PirateFenced(p));
        Console.WriteLine("[career-process] career-main-seventh-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Main seventh career did not close.");
    }
    static string MainEighthSnapshot(GameState p,GameState recipient){return MainSeventhSnapshot(p,recipient);}
    static void MainEighthCareer(bool reload){
        careerRitesFixture=true;combinedCollectionFixture=true;nextUid=CareerUid;var p=Player(Kernel.Maps[1002],true,1003,true,true);combinedCollectionFixture=false;
        SkillTable.LoadSpells(p);SkillTable.LoadProficiencies(p);SubClassTable.Load(p.Entity);Revora.Database.KnownPersons.LoadMentor(p);
        new CoatStorage().Login(p);new TitleStorage().Login(p);p.LoadItemStats();ChiTable.Load(p);WayofHeroes.Load(p);EntityVariableTable.Load(p);Call(typeof(Revora.Database.JiangHu),"LoadClient",p);
        nextUid=4294918104;var recipient=Player(Kernel.Maps[1002],true);recipient.LoadItemStats();ChiTable.Load(recipient);WayofHeroes.Load(recipient);
        nextUid=4294918210;var storage=Player(Kernel.Maps[1002],true);storage.LoadItemStats();
        foreach(var who in new[]{p,recipient,storage})Require(!who.Fake && who.Account.State==AccountTable.AccountState.Player && !PirateFenced(who));
        if((bool)Sql("SELECT to_regclass('revora_main_eighth_probe') IS NULL")){
            Require(!reload && MainSeventhSnapshot(p,recipient)==Convert.ToString(Sql("SELECT snapshot FROM revora_main_seventh_probe")));
            Sql("CREATE TABLE revora_main_eighth_probe(snapshot text,bound_before bigint);INSERT INTO revora_main_eighth_probe VALUES(NULL,@bound)","bound",(long)p.Entity.BoundCps);
        }
        if(Sql("SELECT snapshot FROM revora_main_eighth_probe")==DBNull.Value){
            Require(!reload);MainCareerRoom(p,storage,24);
            if(p.ChiPowers.Sum(x=>x.Points)<1400){string before=MainEighthSnapshot(p,recipient);NobilityClaimPacket(p,804);Require(before==MainEighthSnapshot(p,recipient));}
            for(byte power=1;power<=4 && p.ChiPowers.Sum(x=>x.Points)<1400;power++){
                for(int n=0;p.ChiPowers[power-1].Points<355 && n<150;n++)MainSixthStudy(p,power,90);
                Require(p.ChiPowers[power-1].Points>=355);
            }
            Require(p.ChiPowers.Sum(x=>x.Points)>=1400);Console.WriteLine("[main-eighth] chi-score="+p.ChiPowers.Sum(x=>x.Points));
            if((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=804","uid",(long)CareerUid)==0){
                var old=p.Inventory.Objects.Select(i=>i.UID).ToArray();System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,804));var prizes=p.Inventory.Objects.Where(i=>!old.Contains(i.UID)).ToArray();Require(prizes.Length==3 && prizes.All(i=>i.ID==3003126 && i.Bound && i.StackSize==1));
            }
            if(p.Entity.MyJiang.Inner_Strength<20000){string before=MainEighthSnapshot(p,recipient);NobilityClaimPacket(p,805);Require(before==MainEighthSnapshot(p,recipient));}
            for(int n=0;p.Entity.MyJiang.Inner_Strength<20000 && n<300;n++){
                var pill=p.Inventory.Objects.FirstOrDefault(i=>i.ID==3003126);if(pill!=null && !(bool)Sql("SELECT EXISTS(SELECT 1 FROM revora_jiang_pill_uses WHERE uid=@uid AND consumed_at IS NULL)","uid",(long)CareerUid))SoulClaimUse(p,pill);
                var choice=Enumerable.Range(0,9).Where(stage=>p.Entity.MyJiang.Stagers[stage].Activate).SelectMany(stage=>Enumerable.Range(0,9).Select(star=>new{Stage=stage,Star=star,Value=p.Entity.MyJiang.Stagers[stage].Stars[star]})).OrderBy(x=>x.Value.Activate?1:0).ThenBy(x=>x.Value.Level).ThenBy(x=>x.Stage).ThenBy(x=>x.Star).First();
                MainSeventhTrain(p,choice.Stage,choice.Star);if(n%9==0)Console.WriteLine("[main-eighth] jiang-score="+p.Entity.MyJiang.Inner_Strength);
            }
            Require(p.Entity.MyJiang.Inner_Strength>=20000 && !PirateFenced(p));
            for(uint id=801;id<=808;id++)if((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=@stage","uid",(long)CareerUid,"stage",(int)id)==0){
                string before=MainEighthSnapshot(p,recipient);MainClaimPacket(p,8);Require(before==MainEighthSnapshot(p,recipient));NobilityClaimPacket(p,id);
                Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=@stage AND NOT legacy","uid",(long)CareerUid,"stage",(int)id)==1);
            }
            System.Threading.Tasks.Parallel.For(0,8,_=>MainClaimPacket(p,8));var reward=p.Inventory.Objects.SingleOrDefault(i=>i.ID==3005114);
            if(reward!=null){Require(reward.Bound && reward.StackSize==1);SoulClaimUse(p,reward);}
            Require(p.Entity.BoundCps==Convert.ToUInt32(Sql("SELECT bound_before FROM revora_main_eighth_probe"))+500);
            foreach(var who in new[]{p,recipient,storage})Require(EntityTable.SaveEntity(who));Revora.Database.JiangHu.SaveJiangHu(p);Sql("UPDATE revora_main_eighth_probe SET snapshot=@snapshot","snapshot",MainEighthSnapshot(p,recipient));
        }
        Require(p.Way2Heroes[8].Done && p.Way2Heroes[8].SubStages.Count==8 && p.ChiPowers.Sum(x=>x.Points)>=1400 && p.Entity.MyJiang.Inner_Strength>=20000);
        Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid BETWEEN 800 AND 808 AND NOT legacy","uid",(long)CareerUid)==9);
        Require((long)Sql("SELECT count(*) FROM revora_silver_study_bag_uses WHERE uid=@uid AND item_type=3005114 AND boundcps=500","uid",(long)CareerUid)==1);
        string exact=MainEighthSnapshot(p,recipient);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_main_eighth_probe")));
        for(uint id=801;id<=808;id++)NobilityClaimPacket(p,id);MainClaimPacket(p,8);Require(exact==MainEighthSnapshot(p,recipient) && !PirateFenced(p));
        Console.WriteLine("[career-process] career-main-eighth-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Main eighth career did not close.");
    }
    static string MainNinthSnapshot(GameState p,GameState recipient){return MainNinthSocketsSnapshot(p,recipient);}
    static void MainNinthCareer(bool reload){
        careerRitesFixture=true;combinedCollectionFixture=true;nextUid=CareerUid;var p=Player(Kernel.Maps[1002],true,1003,true,true);combinedCollectionFixture=false;
        SkillTable.LoadSpells(p);SkillTable.LoadProficiencies(p);SubClassTable.Load(p.Entity);Revora.Database.KnownPersons.LoadMentor(p);
        new CoatStorage().Login(p);new TitleStorage().Login(p);p.LoadItemStats();ChiTable.Load(p);WayofHeroes.Load(p);EntityVariableTable.Load(p);Call(typeof(Revora.Database.JiangHu),"LoadClient",p);
        nextUid=4294918104;var recipient=Player(Kernel.Maps[1002],true);recipient.LoadItemStats();ChiTable.Load(recipient);WayofHeroes.Load(recipient);
        nextUid=4294918208;var storage=Player(Kernel.Maps[1002],true);storage.LoadItemStats();
        foreach(var who in new[]{p,recipient,storage})Require(!who.Fake && who.Account.State==AccountTable.AccountState.Player && !PirateFenced(who));
        if((bool)Sql("SELECT to_regclass('revora_main_ninth_probe') IS NULL")){
            Require(!reload && MainNinthSocketsSnapshot(p,recipient)==Convert.ToString(Sql("SELECT snapshot FROM revora_main_ninth_sockets_probe")));
            Sql("CREATE TABLE revora_main_ninth_probe(snapshot text,bound_before bigint);INSERT INTO revora_main_ninth_probe VALUES(NULL,@bound)","bound",(long)p.Entity.BoundCps);
        }
        if(Sql("SELECT snapshot FROM revora_main_ninth_probe")==DBNull.Value){
            Require(!reload);MainCareerRoom(p,storage,20);
            var weapon=p.Equipment.TryGetItem(4);if(weapon.Plus<8){
                uint stoneType=EShopFile.Shops[2888].Items.Where(id=>id>=730001 && id<=730008 && ConquerItemInformation.BaseInformations.ContainsKey(id) && ConquerItemInformation.BaseInformations[id].ConquerPointsWorth>0).OrderByDescending(id=>id).First();
                for(int n=0;weapon.Plus<8 && n<20;n++){System.Threading.Thread.Sleep(1100);var old=p.Inventory.Objects.Select(i=>i.UID).ToArray();CareerBuy(p,stoneType);var stone=p.Inventory.Objects.Single(i=>!old.Contains(i.UID));CompositionClaimCompose(p,weapon,stone);}
                Require(weapon.Plus>=8);
            }
            if(p.ChiPowers.Sum(x=>x.Points)<1450){string before=MainNinthSnapshot(p,recipient);NobilityClaimPacket(p,905);Require(before==MainNinthSnapshot(p,recipient));}
            for(byte power=1;power<=4 && p.ChiPowers.Sum(x=>x.Points)<1450;power++){
                for(int n=0;p.ChiPowers[power-1].Points<365 && n<150;n++)MainSixthStudy(p,power,95);
                Require(p.ChiPowers[power-1].Points>=365);
            }
            Require(p.ChiPowers.Sum(x=>x.Points)>=1450);Console.WriteLine("[main-ninth] chi-score="+p.ChiPowers.Sum(x=>x.Points));
            if((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=905","uid",(long)CareerUid)==0){
                var old=p.Inventory.Objects.Select(i=>i.UID).ToArray();System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,905));var prizes=p.Inventory.Objects.Where(i=>!old.Contains(i.UID)).ToArray();Require(prizes.Length==5 && prizes.All(i=>i.ID==3003126 && i.Bound && i.StackSize==1));
            }
            if(p.Entity.MyJiang.Inner_Strength<30000){string before=MainNinthSnapshot(p,recipient);NobilityClaimPacket(p,906);Require(before==MainNinthSnapshot(p,recipient));}
            for(int n=0;p.Entity.MyJiang.Inner_Strength<30000 && n<500;n++){
                var pill=p.Inventory.Objects.FirstOrDefault(i=>i.ID==3003126);if(pill!=null && !(bool)Sql("SELECT EXISTS(SELECT 1 FROM revora_jiang_pill_uses WHERE uid=@uid AND consumed_at IS NULL)","uid",(long)CareerUid))SoulClaimUse(p,pill);
                var choice=Enumerable.Range(0,9).Where(stage=>p.Entity.MyJiang.Stagers[stage].Activate).SelectMany(stage=>Enumerable.Range(0,9).Select(star=>new{Stage=stage,Star=star,Value=p.Entity.MyJiang.Stagers[stage].Stars[star]})).OrderBy(x=>x.Value.Activate?1:0).ThenBy(x=>x.Value.Level).ThenBy(x=>x.Stage).ThenBy(x=>x.Star).First();
                MainSeventhTrain(p,choice.Stage,choice.Star);if(n%25==0)Console.WriteLine("[main-ninth] jiang-score="+p.Entity.MyJiang.Inner_Strength);
            }
            Require(p.Entity.MyJiang.Inner_Strength>=30000 && !PirateFenced(p));
            for(uint id=901;id<=909;id++)if((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=@stage","uid",(long)CareerUid,"stage",(int)id)==0){
                string before=MainNinthSnapshot(p,recipient);MainClaimPacket(p,9);Require(before==MainNinthSnapshot(p,recipient));NobilityClaimPacket(p,id);
                Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=@stage AND NOT legacy","uid",(long)CareerUid,"stage",(int)id)==1);
            }
            System.Threading.Tasks.Parallel.For(0,8,_=>MainClaimPacket(p,9));var reward=p.Inventory.Objects.SingleOrDefault(i=>i.ID==3005115);
            if(reward!=null){Require(reward.Bound && reward.StackSize==1);SoulClaimUse(p,reward);}
            Require(p.Entity.BoundCps==Convert.ToUInt32(Sql("SELECT bound_before FROM revora_main_ninth_probe"))+800);
            foreach(var who in new[]{p,recipient,storage})Require(EntityTable.SaveEntity(who));Revora.Database.JiangHu.SaveJiangHu(p);Sql("UPDATE revora_main_ninth_probe SET snapshot=@snapshot","snapshot",MainNinthSnapshot(p,recipient));
        }
        Require(p.Way2Heroes[9].Done && p.Way2Heroes[9].SubStages.Count==9 && p.ChiPowers.Sum(x=>x.Points)>=1450 && p.Entity.MyJiang.Inner_Strength>=30000);
        Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid BETWEEN 900 AND 909 AND NOT legacy","uid",(long)CareerUid)==10);
        Require((long)Sql("SELECT count(*) FROM revora_silver_study_bag_uses WHERE uid=@uid AND item_type=3005115 AND boundcps=800","uid",(long)CareerUid)==1);
        string exact=MainNinthSnapshot(p,recipient);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_main_ninth_probe")));
        for(uint id=901;id<=909;id++)NobilityClaimPacket(p,id);MainClaimPacket(p,9);Require(exact==MainNinthSnapshot(p,recipient) && !PirateFenced(p));
        Console.WriteLine("[career-process] career-main-ninth-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Main ninth career did not close.");
    }
    static string MainNinthSocketsSnapshot(GameState p,GameState recipient){return Convert.ToString(Sql(@"SELECT json_build_object('base',@snapshot::json,
'sockets',(SELECT json_agg(r ORDER BY id) FROM revora_equipment_sockets r WHERE uid=@uid))::text","snapshot",MainEighthSnapshot(p,recipient),"uid",(long)CareerUid));}
    static void MainNinthSocketsCareer(bool reload){
        careerRitesFixture=true;combinedCollectionFixture=true;nextUid=CareerUid;var p=Player(Kernel.Maps[1002],true,1003,true,true);combinedCollectionFixture=false;
        SkillTable.LoadSpells(p);SkillTable.LoadProficiencies(p);SubClassTable.Load(p.Entity);Revora.Database.KnownPersons.LoadMentor(p);
        new CoatStorage().Login(p);new TitleStorage().Login(p);p.LoadItemStats();ChiTable.Load(p);WayofHeroes.Load(p);EntityVariableTable.Load(p);Call(typeof(Revora.Database.JiangHu),"LoadClient",p);
        nextUid=4294918104;var recipient=Player(Kernel.Maps[1002],true);recipient.LoadItemStats();ChiTable.Load(recipient);WayofHeroes.Load(recipient);
        nextUid=4294918209;var storage=Player(Kernel.Maps[1002],true);storage.LoadItemStats();
        foreach(var who in new[]{p,recipient,storage})Require(!who.Fake && who.Account.State==AccountTable.AccountState.Player && !PirateFenced(who));
        if((bool)Sql("SELECT to_regclass('revora_main_ninth_sockets_probe') IS NULL")){
            Require(!reload && MainEighthSnapshot(p,recipient)==Convert.ToString(Sql("SELECT snapshot FROM revora_main_eighth_probe")));
            Sql("CREATE TABLE revora_main_ninth_sockets_probe(snapshot text,main bigint,offhand bigint);INSERT INTO revora_main_ninth_sockets_probe VALUES(NULL,NULL,NULL)");
        }
        if(Sql("SELECT snapshot FROM revora_main_ninth_sockets_probe")==DBNull.Value){
            Require(!reload);MainCareerRoom(p,storage,24);
            uint type=ShopFile.Shops[5].Items.Distinct().Select(id=>ConquerItemInformation.BaseInformations[id]).Where(i=>i.ID/1000==410 && i.Class==0 && i.Proficiency==0 && i.GoldWorth>0 && i.Level<=p.Entity.Level).OrderBy(i=>i.Level).ThenBy(i=>i.ID).First().ID;
            var clock=Feature("EquipmentSockets").GetField("Clock",Hidden);object oldClock=clock.GetValue(null);DateTime now=DateTime.UtcNow;
            try{clock.SetValue(null,(Func<DateTime>)(()=>now));
                foreach(byte slot in new byte[]{4,5}){
                    string column=slot==4?"main":"offhand";object saved=Sql("SELECT "+column+" FROM revora_main_ninth_sockets_probe");ConquerItem weapon;
                    if(saved==DBNull.Value){
                        if(slot==4)foreach(byte oldSlot in new byte[]{5,4})if(p.Equipment.TryGetItem(oldSlot)!=null)RetainedUpgradeUsage(p,p.Equipment.TryGetItem(oldSlot).UID,oldSlot,ItemUsage.Mode.UnequipItem);
                        System.Threading.Thread.Sleep(1100);weapon=CareerSilverBuy(p,type);Require(weapon.SocketOne==0 && weapon.SocketTwo==0);
                        RetainedUpgradeUsage(p,weapon.UID,slot,ItemUsage.Mode.EquipItem);Require(p.Equipment.TryGetItem(slot)!=null && p.Equipment.TryGetItem(slot).UID==weapon.UID);
                        Sql("UPDATE revora_main_ninth_sockets_probe SET "+column+"=@item","item",(long)weapon.UID);
                    }else{weapon=p.Equipment.TryGetItem(slot);Require(weapon!=null && weapon.UID==Convert.ToUInt32(saved));}
                    while(weapon.SocketTwo==0){
                        string before=MainNinthSocketsSnapshot(p,recipient);NobilityClaimPacket(p,904);Require(before==MainNinthSocketsSnapshot(p,recipient));
                        int required=weapon.SocketOne==0?1:5;var selected=new List<uint>();
                        for(int n=0;n<required;n++){var old=p.Inventory.Objects.Select(i=>i.UID).ToArray();CareerBuy(p,1088000);var ball=p.Inventory.Objects.Single(i=>!old.Contains(i.UID) && i.ID==1088000);Require(!ball.Bound && ball.StackSize==1);selected.Add(ball.UID);}
                        int length=95+4*required;var frame=new byte[length+8];Revora.Network.Writer.WriteUInt16((ushort)length,0,frame);Revora.Network.Writer.WriteUInt16(1009,2,frame);Revora.Network.Writer.WriteUInt32(weapon.UID,8,frame);Revora.Network.Writer.WriteUInt16(43,20,frame);Revora.Network.Writer.WriteUInt32((uint)required,26,frame);
                        for(int n=0;n<required;n++)Revora.Network.Writer.WriteUInt32(selected[n],91+4*n,frame);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,length,frame);
                        now=now.AddSeconds(2);Revora.Network.PacketHandler.HandlePacket(frame,p);Require(selected.All(id=>!p.Inventory.ContainsUID(id)) && !PirateFenced(p));Require(required==1?weapon.SocketOne!=0:weapon.SocketTwo!=0);
                        Console.WriteLine("[main-ninth-sockets] slot="+slot+" socket-count="+(weapon.SocketTwo==0?1:2)+" paid-dbs="+required);
                    }
                }
            }finally{clock.SetValue(null,oldClock);}
            if((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=904","uid",(long)CareerUid)==0){
                var old=p.Inventory.Objects.Select(i=>i.UID).ToArray();System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,904));var rewards=p.Inventory.Objects.Where(i=>!old.Contains(i.UID)).ToArray();Require(rewards.Length==3 && rewards.All(i=>i.ID==728919 && i.Bound && i.StackSize==1));
            }
            foreach(var who in new[]{p,recipient,storage})Require(EntityTable.SaveEntity(who));Revora.Database.JiangHu.SaveJiangHu(p);Sql("UPDATE revora_main_ninth_sockets_probe SET snapshot=@snapshot","snapshot",MainNinthSocketsSnapshot(p,recipient));
        }
        Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=904 AND NOT legacy","uid",(long)CareerUid)==1);
        Require((long)Sql("SELECT count(*) FROM revora_equipment_sockets WHERE uid=@uid AND target_uid IN(SELECT main FROM revora_main_ninth_sockets_probe UNION ALL SELECT offhand FROM revora_main_ninth_sockets_probe) AND success","uid",(long)CareerUid)==4);
        Require(p.Entity.Class==61 && p.Entity.Reborn==2 && p.Inventory.Objects.Count(i=>i.ID==728919 && i.Bound)==3);
        string exact=MainNinthSocketsSnapshot(p,recipient);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_main_ninth_sockets_probe")));NobilityClaimPacket(p,904);MainClaimPacket(p,9);Require(exact==MainNinthSocketsSnapshot(p,recipient) && !PirateFenced(p));
        Console.WriteLine("[career-process] career-main-ninth-sockets-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Main ninth sockets career did not close.");
    }
    static string MainTenthEquipmentSnapshot(GameState p,GameState recipient){return MainNinthSnapshot(p,recipient);}
    static void MainTenthEquipmentCareer(bool reload){
        careerRitesFixture=true;combinedCollectionFixture=true;nextUid=CareerUid;var p=Player(Kernel.Maps[1002],true,1003,true,true);combinedCollectionFixture=false;
        SkillTable.LoadSpells(p);SkillTable.LoadProficiencies(p);SubClassTable.Load(p.Entity);Revora.Database.KnownPersons.LoadMentor(p);
        new CoatStorage().Login(p);new TitleStorage().Login(p);p.LoadItemStats();ChiTable.Load(p);WayofHeroes.Load(p);EntityVariableTable.Load(p);Call(typeof(Revora.Database.JiangHu),"LoadClient",p);
        nextUid=4294918104;var recipient=Player(Kernel.Maps[1002],true);recipient.LoadItemStats();ChiTable.Load(recipient);WayofHeroes.Load(recipient);
        nextUid=4294918207;var storage=Player(Kernel.Maps[1002],true);storage.LoadItemStats();
        foreach(var who in new[]{p,recipient,storage})Require(!who.Fake && who.Account.State==AccountTable.AccountState.Player && !PirateFenced(who));
        if((bool)Sql("SELECT to_regclass('revora_main_tenth_equipment_probe') IS NULL")){
            Require(!reload && MainNinthSnapshot(p,recipient)==Convert.ToString(Sql("SELECT snapshot FROM revora_main_ninth_probe")));
            Sql("CREATE TABLE revora_main_tenth_equipment_probe(snapshot text);INSERT INTO revora_main_tenth_equipment_probe VALUES(NULL)");
        }
        if(Sql("SELECT snapshot FROM revora_main_tenth_equipment_probe")==DBNull.Value){
            Require(!reload);MainCareerRoom(p,storage,20);
            var boots=p.Equipment.TryGetItem(8);Require(boots.Bound && boots.Purification.PurificationLevel==7);
            if(boots.Purification.PurificationDuration!=0){
                string before=MainTenthEquipmentSnapshot(p,recipient);NobilityClaimPacket(p,1008);Require(before==MainTenthEquipmentSnapshot(p,recipient));
                var small=p.Inventory.Objects.Concat(p.Warehouses.Values.SelectMany(w=>w.Objects)).First(i=>i.ID==723694 && i.Bound);
                if(!p.Inventory.ContainsUID(small.UID)){uint banker=small.Warehouse;var map=Kernel.Maps.Values.First(m=>m.Npcs.ContainsKey(banker));var npc=map.Npcs[banker];p.Entity.Teleport(map.ID,npc.X,npc.Y);GrailWarehouse(p,small.UID,2,banker);Require(p.Inventory.ContainsUID(small.UID));}
                int large=(DataHolder.PurifyStabilizationPoints(7)-10+99)/100;var stones=new List<uint>{small.UID};
                for(int n=0;n<large;n++){var old=p.Inventory.Objects.Select(i=>i.UID).ToArray();CareerBuy(p,723695);stones.Add(p.Inventory.Objects.Single(i=>!old.Contains(i.UID) && i.ID==723695).UID);}
                var frame=RetainedArtifactStones(boots.UID,stones.ToArray());ItemAddingStabilization.Process(frame,p);Require(!PirateFenced(p) && boots.Purification.PurificationDuration==0 && stones.All(id=>!p.Inventory.ContainsUID(id)));
                string stable=MainTenthEquipmentSnapshot(p,recipient);ItemAddingStabilization.Process(frame,p);Require(stable==MainTenthEquipmentSnapshot(p,recipient));
                Console.WriteLine("[main-tenth-equipment] permanent-P7-stones="+stones.Count);
            }
            uint stoneType=EShopFile.Shops[2888].Items.Where(id=>id>=730001 && id<=730008 && ConquerItemInformation.BaseInformations.ContainsKey(id) && ConquerItemInformation.BaseInformations[id].ConquerPointsWorth>0).OrderByDescending(id=>id).First();
            var clock=Feature("ExemptionTokenShop").GetField("Clock",Hidden);object oldClock=clock.GetValue(null);DateTime now=DateTime.UtcNow;
            try{clock.SetValue(null,(Func<DateTime>)(()=>now));foreach(byte slot in new byte[]{4,5}){
                var weapon=p.Equipment.TryGetItem(slot);Require(weapon!=null);
                for(int n=0;weapon.Plus<12 && n<1000;n++){
                    now=now.AddSeconds(2);var old=p.Inventory.Objects.Select(i=>i.UID).ToArray();CareerBuy(p,stoneType);var stone=p.Inventory.Objects.Single(i=>!old.Contains(i.UID));CompositionClaimCompose(p,weapon,stone);
                    if(n%25==0)Console.WriteLine("[main-tenth-equipment] slot="+slot+" plus="+weapon.Plus+" progress="+weapon.PlusProgress);
                }Require(weapon.Plus==12);
            }}finally{clock.SetValue(null,oldClock);}
            uint gemType=EShopFile.Shops[2888].Items.Where(id=>id>=700001 && id<=700073 && id%10==3 && ConquerItemInformation.BaseInformations.ContainsKey(id) && ConquerItemInformation.BaseInformations[id].ConquerPointsWorth>0 && Enum.IsDefined(typeof(Enums.Gem),(byte)(id%1000))).OrderBy(id=>ConquerItemInformation.BaseInformations[id].ConquerPointsWorth).First();
            foreach(byte slot in new byte[]{4,5}){var weapon=p.Equipment.TryGetItem(slot);foreach(ushort socket in new ushort[]{1,2})if((socket==1?weapon.SocketOne:weapon.SocketTwo)==Enums.Gem.EmptySocket){var old=p.Inventory.Objects.Select(i=>i.UID).ToArray();CareerBuy(p,gemType);var gem=p.Inventory.Objects.Single(i=>!old.Contains(i.UID));StoneClaimEmbed(p,weapon,gem,socket);}}
            p.LoadItemStats();
            foreach(uint id in new uint[]{1001,1006,1007,1008})if((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=@stage","uid",(long)CareerUid,"stage",(int)id)==0){
                System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,id));Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=@stage AND NOT legacy","uid",(long)CareerUid,"stage",(int)id)==1);
            }
            foreach(var who in new[]{p,recipient,storage})Require(EntityTable.SaveEntity(who));Revora.Database.JiangHu.SaveJiangHu(p);Sql("UPDATE revora_main_tenth_equipment_probe SET snapshot=@snapshot","snapshot",MainTenthEquipmentSnapshot(p,recipient));
        }
        Require(p.Equipment.TryGetItem(4).Plus==12 && p.Equipment.TryGetItem(5).Plus==12 && p.Equipment.TryGetItem(8).Purification.PurificationDuration==0);
        Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid IN(1001,1006,1007,1008) AND NOT legacy","uid",(long)CareerUid)==4);
        string exact=MainTenthEquipmentSnapshot(p,recipient);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_main_tenth_equipment_probe")));foreach(uint id in new uint[]{1001,1006,1007,1008})NobilityClaimPacket(p,id);MainClaimPacket(p,10);Require(exact==MainTenthEquipmentSnapshot(p,recipient) && !PirateFenced(p));
        Console.WriteLine("[career-process] career-main-tenth-equipment-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Main tenth equipment did not close.");
    }
    static int MainTenthStageScore(GameState p,int stage,int replacement,int kind,byte level){
        var stars=p.Entity.MyJiang.Stagers[stage].Stars;int total=0;
        for(int i=0;i<9;){if(!stars[i].Activate){i++;continue;}int type=i==replacement?kind:(int)stars[i].Typ,end=i,score=0;
            while(end<9 && stars[end].Activate && (end==replacement?kind:(int)stars[end].Typ)==type){score+=Revora.Database.JiangHu.GetStatusPoints(end==replacement?level:stars[end].Level);end++;}
            int count=end-i,alignment=count<2?0:count<8?count-1:count==8?8:10;total+=score+score*alignment/10;i=end;
        }return total;
    }
    static string MainTenthSnapshot(GameState p,GameState recipient){return MainTenthEquipmentSnapshot(p,recipient);}
    static void MainTenthCareer(bool reload){
        careerRitesFixture=true;combinedCollectionFixture=true;nextUid=CareerUid;var p=Player(Kernel.Maps[1002],true,1003,true,true);combinedCollectionFixture=false;
        SkillTable.LoadSpells(p);SkillTable.LoadProficiencies(p);SubClassTable.Load(p.Entity);Revora.Database.KnownPersons.LoadMentor(p);
        new CoatStorage().Login(p);new TitleStorage().Login(p);p.LoadItemStats();ChiTable.Load(p);WayofHeroes.Load(p);EntityVariableTable.Load(p);Call(typeof(Revora.Database.JiangHu),"LoadClient",p);
        nextUid=4294918104;var recipient=Player(Kernel.Maps[1002],true);recipient.LoadItemStats();ChiTable.Load(recipient);WayofHeroes.Load(recipient);
        nextUid=4294918206;var storage=Player(Kernel.Maps[1002],true);storage.LoadItemStats();
        foreach(var who in new[]{p,recipient,storage})Require(!who.Fake && who.Account.State==AccountTable.AccountState.Player && !PirateFenced(who));
        if((bool)Sql("SELECT to_regclass('revora_main_tenth_probe') IS NULL")){
            Require(!reload && MainTenthEquipmentSnapshot(p,recipient)==Convert.ToString(Sql("SELECT snapshot FROM revora_main_tenth_equipment_probe")));
            Sql("CREATE TABLE revora_main_tenth_probe(snapshot text,bound_before bigint);INSERT INTO revora_main_tenth_probe VALUES(NULL,@bound)","bound",(long)p.Entity.BoundCps);
        }
        if(Sql("SELECT snapshot FROM revora_main_tenth_probe")==DBNull.Value){
            Require(!reload);MainCareerRoom(p,storage,16);
            if(p.ChiPowers.Sum(x=>x.Points)<1580){string before=MainTenthSnapshot(p,recipient);NobilityClaimPacket(p,1003);Require(before==MainTenthSnapshot(p,recipient));}
            for(byte power=1;power<=4 && p.ChiPowers.Sum(x=>x.Points)<1580;power++){
                for(int n=0;p.ChiPowers[power-1].Points<396 && n<1500;n++)MainSixthStudy(p,power,99);
                Require(p.ChiPowers[power-1].Points>=396);
            }
            Require(p.ChiPowers.Sum(x=>x.Points)>=1580);Console.WriteLine("[main-tenth] chi-score="+p.ChiPowers.Sum(x=>x.Points));
            if((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=1003","uid",(long)CareerUid)==0){
                var old=p.Inventory.Objects.Select(i=>i.UID).ToArray();System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,1003));var prizes=p.Inventory.Objects.Where(i=>!old.Contains(i.UID)).ToArray();Require(prizes.Length==10 && prizes.All(i=>i.ID==3003126 && i.Bound && i.StackSize==1));
            }
            if(p.Entity.MyJiang.Inner_Strength<50000){string before=MainTenthSnapshot(p,recipient);NobilityClaimPacket(p,1004);Require(before==MainTenthSnapshot(p,recipient));}
            Console.WriteLine("[main-tenth] jiang-start="+p.Entity.MyJiang.Inner_Strength+" study="+p.Entity.SubClasses.StudyPoints);
            for(int n=0;p.Entity.MyJiang.Inner_Strength<50000 && n<2000;n++){
                var pill=p.Inventory.Objects.FirstOrDefault(i=>i.ID==3003126);if(pill!=null && !(bool)Sql("SELECT EXISTS(SELECT 1 FROM revora_jiang_pill_uses WHERE uid=@uid AND consumed_at IS NULL)","uid",(long)CareerUid))SoulClaimUse(p,pill);
                var choice=Enumerable.Range(0,9).Where(stage=>p.Entity.MyJiang.Stagers[stage].Activate).SelectMany(stage=>Enumerable.Range(0,9).Select(star=>new{Stage=stage,Star=star,Gain=Revora.Database.JiangHu.CultivateStatus[(byte)(stage+1)].Max(kind=>MainTenthStageScore(p,stage,star,kind,6))-MainTenthStageScore(p,stage,-1,0,0)})).OrderByDescending(x=>x.Gain).ThenBy(x=>x.Stage).ThenBy(x=>x.Star).First();Require(choice.Gain>0);
                uint before=p.Entity.MyJiang.Inner_Strength;ushort original=p.Entity.MyJiang.Stagers[choice.Stage].Stars[choice.Star].UID;
                MainSeventhTrain(p,choice.Stage,choice.Star);
                if(p.Entity.MyJiang.Inner_Strength<=before){
                    uint cp=p.Entity.ConquerPoints;ushort study=p.Entity.SubClasses.StudyPoints;var frame=new byte[15];Revora.Network.Writer.WriteUInt16(7,0,frame);Revora.Network.Writer.WriteUInt16(2700,2,frame);frame[4]=10;Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,frame.Length-8,frame);Revora.Network.PacketHandler.HandlePacket(frame,p);
                    Require(!PirateFenced(p) && p.Entity.ConquerPoints==cp-Config.JiangHuRestoreCp && p.Entity.SubClasses.StudyPoints==study && p.Entity.MyJiang.Inner_Strength==before && p.Entity.MyJiang.Stagers[choice.Stage].Stars[choice.Star].UID==original);
                }
                if(n%50==0)Console.WriteLine("[main-tenth] jiang-score="+p.Entity.MyJiang.Inner_Strength+" rolls="+(n+1)+" study="+p.Entity.SubClasses.StudyPoints);
            }
            Require(p.Entity.MyJiang.Inner_Strength>=50000 && !PirateFenced(p));
            for(uint id=1001;id<=1008;id++)if((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=@stage","uid",(long)CareerUid,"stage",(int)id)==0){
                string before=MainTenthSnapshot(p,recipient);MainClaimPacket(p,10);Require(before==MainTenthSnapshot(p,recipient));NobilityClaimPacket(p,id);
                Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=@stage AND NOT legacy","uid",(long)CareerUid,"stage",(int)id)==1);
            }
            System.Threading.Tasks.Parallel.For(0,8,_=>MainClaimPacket(p,10));var reward=p.Inventory.Objects.SingleOrDefault(i=>i.ID==3005116);
            if(reward!=null){Require(reward.Bound && reward.StackSize==1);SoulClaimUse(p,reward);}
            Require(p.Entity.BoundCps==Convert.ToUInt32(Sql("SELECT bound_before FROM revora_main_tenth_probe"))+1000);
            foreach(var who in new[]{p,recipient,storage})Require(EntityTable.SaveEntity(who));Revora.Database.JiangHu.SaveJiangHu(p);Sql("UPDATE revora_main_tenth_probe SET snapshot=@snapshot","snapshot",MainTenthSnapshot(p,recipient));
        }
        Require(p.Way2Heroes[10].Done && p.Way2Heroes[10].SubStages.Count==8 && p.ChiPowers.Sum(x=>x.Points)>=1580 && p.Entity.MyJiang.Inner_Strength>=50000);
        Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid BETWEEN 1000 AND 1008 AND NOT legacy","uid",(long)CareerUid)==9);
        Require((long)Sql("SELECT count(*) FROM revora_silver_study_bag_uses WHERE uid=@uid AND item_type=3005116 AND boundcps=1000","uid",(long)CareerUid)==1);
        string exact=MainTenthSnapshot(p,recipient);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_main_tenth_probe")));
        for(uint id=1001;id<=1008;id++)NobilityClaimPacket(p,id);MainClaimPacket(p,10);Require(exact==MainTenthSnapshot(p,recipient) && !PirateFenced(p));
        Require(p.Way2Heroes.Values.Count(stage=>stage.Done)==10 && p.Way2Heroes.Values.Sum(stage=>stage.SubStages.Count)==79);
        Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND NOT legacy","uid",(long)CareerUid)==89);
        Require((long)Sql("SELECT count(*) FROM revora_silver_study_bag_uses WHERE uid=@uid AND item_type BETWEEN 3005107 AND 3005116","uid",(long)CareerUid)==10);
        Require(Convert.ToInt64(Sql("SELECT sum(boundcps) FROM revora_silver_study_bag_uses WHERE uid=@uid AND item_type BETWEEN 3005107 AND 3005116","uid",(long)CareerUid))==3080);
        for(uint page=1;page<=10;page++){
            var state=p.Way2Heroes[page];var reply=new WayofHeroes(page);reply.check(state);var wire=reply.ToArray();Require(wire[8]==1 && System.BitConverter.ToUInt16(wire,9)==state.SubStages.Count);
            foreach(uint stage in (uint[])Call(Feature("WayOfHeroesClaims"),"ExpectedSubstages",page*100))NobilityClaimPacket(p,stage);MainClaimPacket(p,page);
        }
        Require(exact==MainTenthSnapshot(p,recipient) && !PirateFenced(p));
        Console.WriteLine("[main-tenth] complete-main=10 substages=79 receipts=89 bound-cp-from-main-packs=3080");
        Console.WriteLine("[career-process] career-main-tenth-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Main tenth career did not close.");
    }
    static void MainCareerInspect(){
        careerRitesFixture=true;combinedCollectionFixture=true;nextUid=CareerUid;var p=Player(Kernel.Maps[1002],true,1003,true,true);combinedCollectionFixture=false;
        SkillTable.LoadSpells(p);SkillTable.LoadProficiencies(p);SubClassTable.Load(p.Entity);Revora.Database.KnownPersons.LoadMentor(p);
        new CoatStorage().Login(p);new TitleStorage().Login(p);p.LoadItemStats();ChiTable.Load(p);WayofHeroes.Load(p);
        var rules=(System.Collections.IDictionary)Feature("WayOfHeroesClaims").GetField("Rules",Hidden).GetValue(null);
        using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
            for(uint page=1;page<=10;page++){
                var ids=(uint[])Call(Feature("WayOfHeroesClaims"),"ExpectedSubstages",page*100);
                var pending=ids.Where(id=>!(bool)Call(Feature("WayOfHeroesClaims"),"Finished",c,t,rules[id],CareerUid,(int)p.Entity.Reborn,(int)p.Entity.Level,p)).ToArray();
                Console.WriteLine("[main-career] page="+page+" currently-unmet="+string.Join(",",pending));
            }
            t.Rollback();
        }}
        Console.WriteLine("[main-career] bag="+p.Inventory.Count+" class="+p.Entity.Class+" level="+p.Entity.Level+" xp-skill="+p.Spells.ContainsKey(12860));
        Console.WriteLine("[main-career] "+Convert.ToString(Sql(@"SELECT json_build_object('claims',(SELECT json_agg(stage_uid ORDER BY stage_uid) FROM revora_way_of_heroes_claims WHERE uid=@uid),'intro',(SELECT stage FROM revora_newcomer_intro WHERE uid=@uid),'xp',(SELECT count(*) FROM revora_xp_kills WHERE uid=@uid))::text","uid",(long)CareerUid)));
        Console.WriteLine("[main-career] chi-score="+p.ChiPowers.Sum(x=>x.Points)+" chi-points="+p.ChiPoints+" cp="+p.Entity.ConquerPoints+" enlight="+p.Entity.EnlightenPoints+" guild="+p.Entity.GuildID);
        foreach(var i in p.Inventory.Objects.Concat(p.Equipment.GetItems()).Concat(p.Warehouses.Values.SelectMany(w=>w.Objects)).Where(i=>ConquerItemInformation.BaseInformations.ContainsKey(i.ID) && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel>0).Take(15))Console.WriteLine("[main-career] soul="+i.ID+" uid="+i.UID+" bound="+i.Bound+" warehouse="+i.Warehouse);
        foreach(var i in p.Equipment.GetItems().Where(i=>i.Position<=8))Console.WriteLine("[main-career] worn="+i.ID+" uid="+i.UID+" slot="+i.Position+" bound="+i.Bound+" purification="+i.Purification.PurificationLevel+" duration="+i.Purification.PurificationDuration);
        foreach(var i in p.Inventory.Objects.Concat(p.Equipment.GetItems()).Concat(p.Warehouses.Values.SelectMany(w=>w.Objects)).Where(i=>ItemHandler.ItemPosition(i.ID)==4 || i.Position==10 || i.Position==11).OrderBy(i=>i.UID))Console.WriteLine("[main-career] gear="+i.ID+" uid="+i.UID+" slot="+i.Position+" warehouse="+i.Warehouse+" plus="+i.Plus+" sockets="+i.SocketOne+","+i.SocketTwo);
        Console.WriteLine("[career-process] career-main-inspect-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Main inspection did not close.");
    }

    static void MainClaimPacket(GameState p,uint page,byte mode=2){
        var frame=new byte[17];Revora.Network.Writer.WriteUInt16(9,0,frame);Revora.Network.Writer.WriteUInt16(2830,2,frame);
        frame[4]=mode;Revora.Network.Writer.WriteUInt32(page,5,frame);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,9,frame);
        Revora.Network.PacketHandler.HandlePacket(frame,p);
    }
    static GameState MainClaimLoad(uint uid){nextUid=uid;var p=Player(Kernel.Maps[1002],true);ChiTable.Load(p);WayofHeroes.Load(p);return p;}
    static SafeDictionary<uint,WayofHeroes.StageInfo> MainClaimState(){
        uint[] goals={5,7,6,9,7,9,11,8,9,8};var states=new SafeDictionary<uint,WayofHeroes.StageInfo>();
        for(uint page=1;page<=10;page++){
            var stage=new WayofHeroes.StageInfo{UID=page};states.Add(page,stage);
            for(uint n=1;n<=goals[page-1];n++)stage.SubStages.Add(page*100+n,new WayofHeroes.SubStageInfo{UID=page*100+n,Done=true,points=1});
        }return states;
    }
    static void MainClaimBoundaries(){
        var states=MainClaimState();
        for(uint page=1;page<=10;page++){
            Require((bool)Call(Feature("WayOfHeroesClaims"),"MainFinished",states,page*100));
            foreach(var sub in states[page].SubStages.Values.ToArray()){
                sub.Done=false;Require(!(bool)Call(Feature("WayOfHeroesClaims"),"MainFinished",states,page*100));sub.Done=true;
            }
            var last=states[page].SubStages.Values.OrderBy(x=>x.UID).Last();states[page].SubStages.Remove(last.UID);
            states[page].SubStages.Add(page*100+99,new WayofHeroes.SubStageInfo{UID=page*100+99,Done=true});
            Require(!(bool)Call(Feature("WayOfHeroesClaims"),"MainFinished",states,page*100));
            states[page].SubStages.Remove(page*100+99);states[page].SubStages.Add(last.UID,last);
        }
        foreach(uint invalid in new[]{0u,99u,101u,1100u,uint.MaxValue})Require(!(bool)Call(Feature("WayOfHeroesClaims"),"MainFinished",states,invalid));
    }
    static void MainClaimProcess(bool reload){
        mainClaimFixture=true;const uint uid=4294918001,legacyUid=4294918002;
        MainClaimBoundaries();
        if((bool)Sql("SELECT to_regclass('revora_main_claim_probe') IS NULL")){
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN @first AND @last","first",(long)uid,"last",(long)legacyUid)==0);
            for(uint n=uid;n<=legacyUid;n++){nextUid=n;Player(Kernel.Maps[1002]);}
            Sql("UPDATE entities SET level=140,reborn=2 WHERE uid BETWEEN @first AND @last","first",(long)uid,"last",(long)legacyUid);
            Sql("CREATE TABLE revora_main_claim_probe(uid bigint PRIMARY KEY,snapshot text);INSERT INTO revora_main_claim_probe VALUES(@first,NULL),(@last,NULL)","first",(long)uid,"last",(long)legacyUid);
            var fresh=MainClaimLoad(uid);var old=MainClaimLoad(legacyUid);
            string empty=MainPackSnapshot(fresh);for(uint page=1;page<=10;page++)MainClaimPacket(fresh,page);Require(empty==MainPackSnapshot(fresh));
            // Explicit legacy-completion fixtures, not fabricated quest earning receipts.
            byte[] blob=(byte[])Call(Feature("WayOfHeroesClaims"),"Blob",MainClaimState());
            Sql("UPDATE wayofheroes SET wayofheroes=@blob WHERE uid=@uid","blob",blob,"uid",(long)uid);
            var legacy=new SafeDictionary<uint,WayofHeroes.StageInfo>();legacy.Add(1,new WayofHeroes.StageInfo{UID=1,Done=true});
            Sql("UPDATE wayofheroes SET wayofheroes=@blob WHERE uid=@uid","blob",(byte[])Call(Feature("WayOfHeroesClaims"),"Blob",legacy),"uid",(long)legacyUid);
        }
        var p=MainClaimLoad(uid);var prior=MainClaimLoad(legacyUid);
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        foreach(uint invalid in new[]{0u,11u,uint.MaxValue,1073741825u}){string before=MainPackSnapshot(p);MainClaimPacket(p,invalid);Require(before==MainPackSnapshot(p));}
        for(uint page=1;page<=10;page++){string before=MainPackSnapshot(p);MainClaimPacket(p,page*100,1);Require(before==MainPackSnapshot(p));}
        if(Sql("SELECT snapshot FROM revora_main_claim_probe WHERE uid=@uid","uid",(long)uid)==DBNull.Value){
            Require(!reload);
            if((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=100","uid",(long)uid)==0){
                var hook=Feature("WayOfHeroesClaims").GetField("BeforeInstall",Hidden);object previous=hook.GetValue(null);
                try{hook.SetValue(null,(Action<GameState>)(who=>{if(who.Entity.UID==uid)throw new InvalidOperationException("Injected main-stage install failure.");}));MainClaimPacket(p,1);Require(PirateFenced(p));}
                finally{hook.SetValue(null,previous);}
                Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=100 AND NOT legacy","uid",(long)uid)==1);
                p=MainClaimLoad(uid);Require(!PirateFenced(p) && p.Way2Heroes[1].Done && !p.Way2Heroes[1].SubStages.ContainsKey(100));
            }
            for(uint page=1;page<=10;page++){
                uint current=page;System.Threading.Tasks.Parallel.For(0,8,_=>MainClaimPacket(p,current));
                Require(p.Way2Heroes[page].Done && !p.Way2Heroes[page].SubStages.ContainsKey(page*100));
                var view=new WayofHeroes(page);view.check(p.Way2Heroes[page]);Require(view.ToArray()[8]==1);
                var pack=p.Inventory.Objects.SingleOrDefault(i=>i.ID==3005106+page);
                if(pack!=null){Require(pack.Bound && pack.StackSize==1);SoulClaimUse(p,pack);}
                Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=@stage AND NOT legacy","uid",(long)uid,"stage",(int)(page*100))==1);
            }
            Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_main_claim_probe SET snapshot=@snapshot WHERE uid=@uid","uid",(long)uid,"snapshot",MainPackSnapshot(p));
        }
        if(Sql("SELECT snapshot FROM revora_main_claim_probe WHERE uid=@uid","uid",(long)legacyUid)==DBNull.Value){
            Require(!reload);System.Threading.Tasks.Parallel.For(0,8,_=>MainClaimPacket(prior,1));
            Require(prior.Inventory.Count==0 && prior.Way2Heroes[1].Done && prior.Way2Heroes[1].SubStages.Count==0);
            Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=100 AND legacy","uid",(long)legacyUid)==1);
            Require(EntityTable.SaveEntity(prior));Sql("UPDATE revora_main_claim_probe SET snapshot=@snapshot WHERE uid=@uid","uid",(long)legacyUid,"snapshot",MainPackSnapshot(prior));
        }
        Require(p.Inventory.Count==0 && p.Entity.BoundCps==3080 && p.Way2Heroes.Values.All(stage=>stage.Done));
        Require((long)Sql("SELECT count(*) FROM revora_silver_study_bag_uses WHERE uid=@uid","uid",(long)uid)==10);
        foreach(var who in new[]{p,prior}){
            string exact=MainPackSnapshot(who);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_main_claim_probe WHERE uid=@uid","uid",(long)who.Entity.UID)));
            for(uint page=1;page<=10;page++)MainClaimPacket(who,page);Require(exact==MainPackSnapshot(who) && !PirateFenced(who));
        }
        Console.WriteLine("[career-process] career-main-claims-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Main claims did not close.");
    }
}
