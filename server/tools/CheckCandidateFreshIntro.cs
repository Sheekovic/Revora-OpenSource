using System;
using System.Linq;
using System.Reflection;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshIntroSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object('prior',@prior::json,'intro',(SELECT row_to_json(r) FROM revora_newcomer_intro r WHERE uid=@uid),'potions',(SELECT json_agg(r ORDER BY id) FROM revora_exp_potion_uses r WHERE uid=@uid))::text","prior",FreshEquipmentPacksSnapshot(p),"uid",(long)p.Entity.UID));}
    static void FreshIntroSave(GameState p,string stage){Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_intro_probe SET stage=@stage,snapshot=@snapshot","stage",stage,"snapshot",FreshIntroSnapshot(p));}
    static void FreshIntroProcess(string phase){
        var p=FreshLoadSaved(true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_fresh_intro_probe') IS NULL")){
            Require(phase.EndsWith("earn") && FreshEquipmentPacksSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_equipment_packs_probe")) && (long)Sql("SELECT count(*) FROM revora_newcomer_intro WHERE uid=@uid","uid",(long)p.Entity.UID)==0);
            Sql("CREATE TABLE revora_fresh_intro_probe(stage text,snapshot text,potion bigint);INSERT INTO revora_fresh_intro_probe VALUES('new',@snapshot,NULL)","snapshot",FreshIntroSnapshot(p));
        }
        if(phase.EndsWith("inspect")){System.IO.File.WriteAllText("fresh-intro-before.json",Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_intro_probe")));System.IO.File.WriteAllText("fresh-intro-current.json",FreshIntroSnapshot(p));System.Console.WriteLine("[career-process] "+phase+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Intro inspection did not close.");}
        if(phase.EndsWith("claim") && FreshIntroSnapshot(p)!=Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_intro_probe"))){
            Func<string,string> digest=value=>{using(var sha=System.Security.Cryptography.SHA256.Create())return System.BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant();};
            Require(digest(Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_intro_probe")))=="9bdbed85deb89a32ea1e1c3c754bc538ddf3415bda525bc2b4228925141c48e5" && digest(FreshIntroSnapshot(p))=="9ebad43352bf815bddc11888595270d52ba99a25c09dbd5a51df98c809ac7481");
            // Reviewed interruption added only the104 claim, its potion and Way of Heroes state.
            Sql("UPDATE revora_fresh_intro_probe SET stage='claimed',snapshot=@snapshot WHERE stage='earned'","snapshot",FreshIntroSnapshot(p));
        }
        Require(FreshIntroSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_intro_probe")));string stage=Convert.ToString(Sql("SELECT stage FROM revora_fresh_intro_probe"));
        Func<int> step=()=>Convert.ToInt32(Sql("SELECT COALESCE((SELECT stage FROM revora_newcomer_intro WHERE uid=@uid),0)","uid",(long)p.Entity.UID));
        Action refuse=()=>{string before=FreshIntroSnapshot(p);NobilityClaimPacket(p,104);Require(before==FreshIntroSnapshot(p));};
        if(phase.EndsWith("earn")){
            Require(stage=="new");refuse();FreshClassicTalk(p,1002,2001,1);Require(step()==0);FreshClassicTalk(p,1002,5673,1);Require(step()==1);refuse();FreshClassicTalk(p,1002,8,0);IntroWarehouse(p);Require(step()==2);refuse();FreshClassicTalk(p,1002,3,0);
            uint type=ShopFile.Shops[3].Items.Where(id=>ConquerItemInformation.BaseInformations[id].GoldWorth>0).OrderBy(id=>ConquerItemInformation.BaseInformations[id].GoldWorth).First();ulong silver=p.Entity.Money;uint price=ConquerItemInformation.BaseInformations[type].GoldWorth;var before=p.Inventory.Objects.Select(i=>i.UID).ToArray();
            ItemHandler.HandleBuyFromNPC(new ItemUsage(true){UID=3,dwParam=type,dwExtraInfo=1},p);Require(step()==3 && p.Entity.Money==silver-price && p.Inventory.Objects.Count(i=>i.ID==type && !before.Contains(i.UID))==1);refuse();before=p.Inventory.Objects.Select(i=>i.UID).ToArray();FreshClassicTalk(p,1002,2001,1);var added=p.Inventory.Objects.Where(i=>!before.Contains(i.UID)).ToArray();Require(step()==4 && added.Length==1 && added[0].ID==4030101 && added[0].Bound);
            Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10930 AND source='newcomer-intro-v1'","uid",(long)p.Entity.UID)==1);FreshIntroSave(p,"earned");
        }else if(phase.EndsWith("claim")){
            Require(stage=="earned" || stage=="claimed");
            if(stage=="earned"){var before=p.Inventory.Objects.Select(i=>i.UID).ToArray();NobilityClaimPacket(p,104);var added=p.Inventory.Objects.Where(i=>!before.Contains(i.UID)).ToArray();Require(added.Length==1 && added[0].ID==723017 && added[0].Bound && before.All(uid=>p.Inventory.ContainsUID(uid)));Sql("UPDATE revora_fresh_intro_probe SET potion=@uid","uid",(long)added[0].UID);}
            var potion=p.Inventory.Objects.Single(i=>i.UID==Convert.ToUInt32(Sql("SELECT potion FROM revora_fresh_intro_probe")));Require(potion.ID==723017 && potion.Bound);

            if(p.Entity.DoubleExperienceTime>0){string active=FreshIntroSnapshot(p);RetainedUpgradeUsage(p,potion.UID,0,ItemUsage.Mode.EquipItem);Require(active==FreshIntroSnapshot(p) && p.Inventory.ContainsUID(potion.UID));}
            int remaining=p.Entity.DoubleExperienceTime;var tick=typeof(Revora.Game.Entity).GetMethod("TickDoubleExperience",BindingFlags.Instance|BindingFlags.NonPublic);var now=Time32.Now;
            for(int i=0;i<remaining;i++){now=now.AddSeconds(2);tick.Invoke(p.Entity,new object[]{now});}Require(p.Entity.DoubleExperienceTime==0);SoulClaimUse(p,potion);Require(!p.Inventory.ContainsUID(potion.UID) && p.Entity.DoubleExperienceTime==3600);FreshIntroSave(p,"used");
        }else Require(phase.EndsWith("reload") && stage=="used");
        if(!phase.EndsWith("earn")){
            Require(p.Entity.DoubleExperienceTime==3600 && (long)Sql("SELECT count(*) FROM revora_exp_potion_uses WHERE uid=@uid AND item_uid=(SELECT potion FROM revora_fresh_intro_probe) AND item_type=723017 AND bound AND seconds=3600","uid",(long)p.Entity.UID)==1 && (long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=104 AND NOT legacy","uid",(long)p.Entity.UID)==1);
            string exact=FreshIntroSnapshot(p);FreshClassicTalk(p,1002,2001,1);NobilityClaimPacket(p,104);RetainedUpgradeUsage(p,Convert.ToUInt32(Sql("SELECT potion FROM revora_fresh_intro_probe")),0,ItemUsage.Mode.EquipItem);Require(exact==FreshIntroSnapshot(p));
        }
        Require((bool)Sql("SELECT EXISTS(SELECT 1 FROM revora_newcomer_intro r JOIN items i ON i.uid=r.reward_uid WHERE r.uid=@uid AND r.stage=4 AND i.entityid=r.uid AND i.id=4030101 AND i.bound=1)","uid",(long)p.Entity.UID) && FreshIntroSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_intro_probe")) && !PirateFenced(p));
        System.Console.WriteLine("[career-process] "+phase+"-passed uid="+p.Entity.UID+" bag="+p.Inventory.Count+" double-exp="+p.Entity.DoubleExperienceTime);Server.CommandsAI("@close");throw new InvalidOperationException("Fresh introduction did not close.");
    }
}
