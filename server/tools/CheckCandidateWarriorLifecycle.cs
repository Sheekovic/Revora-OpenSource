using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static bool stanceTrainingFixture;
    static void StanceTrainingCast(GameState p){p.Entity.AttackStamp=default(Time32);p.Entity.SpellStamp=default(Time32);p.Spells[11160].ColdTime=default(Time32);new Revora.Game.Attacking.Handle(new Attack(true){AttackType=Attack.Magic,Decoded=true,Damage=11160,Attacker=p.Entity.UID,Attacked=p.Entity.UID,X=p.Entity.X,Y=p.Entity.Y},p.Entity,p.Entity);}
    static string StanceTrainingSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object('skills',(SELECT json_agg(r ORDER BY id) FROM skills r WHERE entityid=@uid),'items',(SELECT json_agg(r ORDER BY uid) FROM items r WHERE entityid=@uid),'lesson',(SELECT json_agg(r ORDER BY id) FROM revora_warrior_training r WHERE uid=@uid),'money',(SELECT money FROM entities WHERE uid=@uid))::text","uid",(long)p.Entity.UID));}
    static void StanceTrainingProcess(string phase){
        stanceTrainingFixture=true;const uint uid=4294918302;
        if((bool)Sql("SELECT to_regclass('revora_stance_training_v2_probe') IS NULL")){
            Require(phase=="practice" && (long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==0);nextUid=uid;Player(Kernel.Maps[1002]);
            Sql("UPDATE entities SET level=70,class=23,strength=500,agility=500,money=10000000 WHERE uid=@uid","uid",(long)uid);
            Sql("CREATE TABLE revora_stance_training_v2_probe(step integer,snapshot text);INSERT INTO revora_stance_training_v2_probe VALUES(0,NULL)");
        }
        nextUid=uid;var p=Player(Kernel.Maps[1002],true);SkillTable.LoadSpells(p);SkillTable.LoadProficiencies(p);p.LoadItemStats();Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        int step=Convert.ToInt32(Sql("SELECT step FROM revora_stance_training_v2_probe"));
        if(step==0){Require(phase=="practice");p.Entity.Teleport(1004,33,32);HeartTalk(p,10001,10);Require(p.Spells.ContainsKey(11160) && p.Spells[11160].Level==0 && p.Spells[11160].Experience<=27000);
            foreach(byte slot in new byte[]{4,5}){
                if(p.Equipment.TryGetItem(slot)!=null)continue;
                uint family=slot==4?410u:900u;var offer=ShopFile.Shops.SelectMany(shop=>shop.Value.Items.Select(id=>new{Shop=shop.Key,Item=ConquerItemInformation.BaseInformations[id]})).Where(x=>x.Item.ID/1000==family && x.Item.Level<=70 && x.Item.Proficiency==0 && x.Item.GoldWorth>0 && Kernel.Maps.Values.Any(m=>m.Npcs.ContainsKey(x.Shop))).OrderBy(x=>x.Item.Level).ThenBy(x=>x.Item.ID).First();
                var item=CareerSilverBuy(p,offer.Item.ID,offer.Shop);RetainedUpgradeUsage(p,item.UID,slot,ItemUsage.Mode.EquipItem);Require(p.Equipment.TryGetItem(slot).UID==item.UID);
            }
            p.Entity.Teleport(1002,408,330);Require(EntityTable.SaveEntity(p));
            uint threshold=SpellTable.GetSpell(11160,0).NeedExperience;Require(threshold==27000 && SpellTable.GetSpell(11160,1).NeedLevel==100);
            Require(p.Spells[11160].Experience==0);string preFault=StanceTrainingSnapshot(p);
            Sql(@"CREATE OR REPLACE FUNCTION revora_stance_test_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.entityid=4294918302 AND NEW.id=11160 THEN RAISE EXCEPTION 'Injected stance training write failure'; END IF; RETURN NEW; END $$;
CREATE TRIGGER revora_stance_test_failure BEFORE UPDATE ON skills FOR EACH ROW EXECUTE FUNCTION revora_stance_test_failure()");
            try{StanceTrainingCast(p);Require(PirateFenced(p) && !p.Entity.IsDefensiveStance && preFault==StanceTrainingSnapshot(p));}
            finally{Sql("DROP TRIGGER revora_stance_test_failure ON skills;DROP FUNCTION revora_stance_test_failure()");}
            p=StanceTrainingReload(p);Require(!PirateFenced(p) && p.Spells[11160].Experience==0);
            StanceTrainingCast(p);System.Console.WriteLine("[stance-training] ready="+Call(Feature("WarriorStance"),"Ready",p)+" active="+p.Entity.IsDefensiveStance+" xp="+p.Spells[11160].Experience+" map="+p.Entity.MapID);Require(p.Entity.IsDefensiveStance && p.Spells[11160].Experience>0);uint partial=p.Spells[11160].Experience;Require(partial==Convert.ToUInt32(Sql("SELECT experience FROM skills WHERE entityid=@uid AND id=11160","uid",(long)uid)));StanceTrainingCast(p);Require(!p.Entity.IsDefensiveStance && p.Spells[11160].Experience==partial);
            p=StanceTrainingReload(p);Require(p.Spells[11160].Experience==partial && p.Spells[11160].Level==0);
            var shield=p.Equipment.TryGetItem(5);RetainedUpgradeUsage(p,shield.UID,5,ItemUsage.Mode.UnequipItem);StanceTrainingCast(p);Require(!p.Entity.IsDefensiveStance && p.Spells[11160].Experience==partial);RetainedUpgradeUsage(p,shield.UID,5,ItemUsage.Mode.EquipItem);
            for(int n=0;p.Spells[11160].Experience<threshold && n<1000;n++){StanceTrainingCast(p);Require(p.Entity.IsDefensiveStance);StanceTrainingCast(p);Require(!p.Entity.IsDefensiveStance && !PirateFenced(p));}
            Require(p.Spells[11160].Level==0 && p.Spells[11160].Experience==threshold);Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_stance_training_v2_probe SET step=1,snapshot=@snapshot","snapshot",StanceTrainingSnapshot(p));step=1;
        }
        Require(StanceTrainingSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_stance_training_v2_probe")));
        if(phase=="promote" && step==1){
            Require(p.Entity.Level==70 && p.Spells[11160].Level==0 && p.Spells[11160].Experience==27000 && !p.Entity.IsDefensiveStance);
            p.Entity.Level=99;Require(EntityTable.SaveEntity(p));StanceTrainingCast(p);StanceTrainingCast(p);Require(p.Spells[11160].Level==0 && p.Spells[11160].Experience==27000);
            p.Entity.Level=100;Require(EntityTable.SaveEntity(p));StanceTrainingCast(p);Require(p.Spells[11160].Level==1 && p.Spells[11160].Experience==0 && p.Entity.DefensiveStance==120);StanceTrainingCast(p);StanceTrainingCast(p);Require(p.Entity.DefensiveStance==240);StanceTrainingCast(p);
            Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_stance_training_v2_probe SET step=2,snapshot=@snapshot","snapshot",StanceTrainingSnapshot(p));step=2;
        }
        if(phase=="reload"){
            Require(step==2 && p.Spells[11160].Level==1 && p.Spells[11160].Experience==0 && !p.Entity.IsDefensiveStance);string before=StanceTrainingSnapshot(p);StanceTrainingCast(p);Require(p.Entity.IsDefensiveStance && p.Entity.DefensiveStance==240);StanceTrainingCast(p);Require(before==StanceTrainingSnapshot(p));
        }
        System.Console.WriteLine("[career-process] career-stance-training-"+phase+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Stance training did not close.");
    }
    static GameState StanceTrainingReload(GameState p){if(!PirateFenced(p))Require(EntityTable.SaveEntity(p));nextUid=p.Entity.UID;var next=Player(Kernel.Maps[1002],true);SkillTable.LoadSpells(next);SkillTable.LoadProficiencies(next);next.LoadItemStats();return next;}
    static void WarriorTalk(GameState p,uint npc,byte option) { HeartTalk(p,npc,0);HeartTalk(p,npc,option); }
    static int WarriorStage(GameState p) {
        return Convert.ToInt32(Sql("SELECT COALESCE((SELECT stage FROM revora_warrior_epic WHERE uid=@uid),0)","uid",(long)p.Entity.UID));
    }
    static Entity WarriorTarget(GameState p,string name) { return p.Map.Entities.Values.Single(e=>e.MonsterInfo!=null && e.MonsterInfo.Name==name); }
    static void WarriorCapture() {
        Player(Kernel.Maps[1002]);Sql("UPDATE entities SET class=25 WHERE uid=@uid","uid",(long)FirstUid);nextUid=FirstUid;
        var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();p.BindWorldMap(1004);
        WarriorTalk(p,10001,86);Require(WarriorStage(p)==0 && p.Inventory.Count==0);
        WarriorTalk(p,10001,87);var jade=p.Inventory.Objects.Single(i=>i.ID==3200206);Require(jade.Bound && WarriorStage(p)==1);
        WarriorTalk(p,10001,87);Require(p.Inventory.Objects.Count(i=>i.ID==3200206)==1);
        ItemUsage.Process(MountUsePacket(jade.UID),p);
        Require(p.Map.BaseID==10009 && WarriorStage(p)==2 && !p.Inventory.ContainsUID(jade.UID));
        WarriorTalk(p,19253,1);Require(p.Map.BaseID==10010 && WarriorStage(p)==3);
        EliminationHit(p,WarriorTarget(p,"SealingStele"));Require(p.Map.BaseID==10011 && WarriorStage(p)==4);
        WarriorTalk(p,19254,1);var evil=WarriorTarget(p,"TinyMindofEvil");Require(WarriorStage(p)==5 && evil.Hitpoints==50000);
        EntityTable.SaveEntity(p);
        Sql("CREATE TABLE revora_warrior_process_probe(encounter uuid NOT NULL,fingerprint text); INSERT INTO revora_warrior_process_probe(encounter) SELECT id FROM revora_warrior_epic_encounters WHERE uid=@uid AND stage=5 AND state='active'","uid",(long)p.Entity.UID);
        Require((long)Sql("SELECT count(*) FROM revora_warrior_process_probe")==1);
        System.Console.WriteLine("[warrior-process] warrior-capture-passed");Environment.Exit(0);
    }
    static void WarriorFinish() {
        var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();Require(p.Entity.MapID==1002 && WarriorStage(p)==5 && p.Inventory.Count==0);
        p.BindWorldMap(1004);WarriorTalk(p,10001,88);
        Require(p.Map.BaseID==10011 && WarriorStage(p)==5);
        var evil=WarriorTarget(p,"TinyMindofEvil");Require(evil.Hitpoints==50000);
        EliminationHit(p,evil);Require(WarriorStage(p)==6 && p.Inventory.Count==0);
        WarriorTalk(p,19259,1);WarriorTalk(p,19259,1);Require(WarriorStage(p)==7);
        Require(p.Inventory.Count==2 && p.Inventory.Objects.All(i=>i.ID==624439 && !i.Bound && i.Plus==12 && i.Bless==7
            && i.SocketOne==Enums.Gem.EmptySocket && i.SocketTwo==Enums.Gem.EmptySocket));
        Require(p.Entity.ConquerPoints==0 && p.Entity.Money==100000);
        Require((long)Sql("SELECT count(*) FROM revora_warrior_epic_encounters WHERE id=(SELECT encounter FROM revora_warrior_process_probe) AND state='retired'")==1);
        Sql("UPDATE revora_warrior_process_probe SET fingerprint=(SELECT string_agg(uid::text,',' ORDER BY uid) FROM items WHERE entityid=@uid AND id=624439)","uid",(long)p.Entity.UID);
        System.Console.WriteLine("[warrior-process] warrior-finish-passed");
        Server.CommandsAI("@close");throw new InvalidOperationException("Warrior finish process did not close.");
    }
    static void WarriorVerify() {
        var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();Require(WarriorStage(p)==7);
        Require(p.Inventory.Count==2 && p.Inventory.Objects.All(i=>i.ID==624439 && !i.Bound && i.Plus==12 && i.Bless==7
            && i.SocketOne==Enums.Gem.EmptySocket && i.SocketTwo==Enums.Gem.EmptySocket));
        Require(Equals(Sql("SELECT fingerprint FROM revora_warrior_process_probe"),Sql("SELECT string_agg(uid::text,',' ORDER BY uid) FROM items WHERE entityid=@uid AND id=624439","uid",(long)p.Entity.UID)));
        Require((long)Sql("SELECT count(*) FROM revora_warrior_epic_rewards WHERE uid=@uid","uid",(long)p.Entity.UID)==2);
        Require((long)Sql("SELECT count(*) FROM revora_warrior_epic_steps WHERE uid=@uid","uid",(long)p.Entity.UID)==7);
        Require((long)Sql("SELECT count(*) FROM revora_warrior_epic_encounters WHERE uid=@uid AND state='victory'","uid",(long)p.Entity.UID)==2);
        Require(p.Entity.ConquerPoints==0 && p.Entity.Money==100000);
        System.Console.WriteLine("[warrior-process] warrior-verify-passed");
        Server.CommandsAI("@close");throw new InvalidOperationException("Warrior verification process did not close.");
    }
}
