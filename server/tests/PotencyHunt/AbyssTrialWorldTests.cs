using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.Attacking;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type AbyssWorld=typeof(Entity).Assembly.GetType("Revora.Game.Features.AbyssTrialWorld");
    static DateTime abyssNow=new DateTime(2032,4,1,0,0,0,DateTimeKind.Utc);
    static GameState abyssWorldCompleter;
    static GameState AbyssHunter(Map town,Map map) {
        var p=SecondPlayer(town); p.Quests=new Quests(p); p.Quests.Load(); AbyssWrite(p,"Enroll");
        Call(AbyssJournal,null,"Refresh",p,false); AbyssAt(p,map,3601); return p;
    }
    static object AbyssActor(GameState p,Map map,uint family, bool resume=false) {
        uint npc=family==3632?3605u:family==3633?3606u:family==3634?3607u:family==3635?3609u:family>=3644?3608u
            :new uint[]{3616,3618,3620,3622,3628,3630,3633,3635}[family-3636];
        AbyssAt(p,map,npc); object plan=!resume && (family>=3632 && family<=3635 || family==3644)?AbyssSelect(p,"SummonCost",family):null;
        lock(p.ItemSyncRoot) return Call(AbyssWorld,null,"Summon",p,resume?-1:AbyssVersion(p),family,plan);
    }
    static Entity AbyssBody(object actor) { return (Entity)AbyssField(actor,"Body"); }
    static void AbyssFight(GameState p,object actor) {
        var body=AbyssBody(actor); body.Die(p.Entity);
        if(Fenced(p))throw new Exception("Abyss combat fixture was fenced.");
        Check(body.Dead && !p.Map.Entities.ContainsKey(body.UID),"registered Abyss death removes its paid actor once: "+body.MonsterInfo.Name);
    }
    static Entity AbyssWild(GameState p,uint family) {
        var info=MonsterInformation.MonsterInformations[family].Copy(); info.OwnItemRate=0;
        var body=new Entity(EntityFlag.Monster,false){UID=p.Map.EntityUIDCounter.Next,MapID=1700,MonsterInfo=info,X=(ushort)(p.Entity.X+2),Y=p.Entity.Y,
            Name=info.Name,Body=info.Mesh,Level=info.Level,MapObjType=MapObjectType.Monster,MinAttack=info.MinAttack,MaxAttack=info.MaxAttack,Defence=info.Defence};
        info.Owner=body; body.MaxHitpoints=body.Hitpoints=info.Hitpoints; p.Map.AddEntity2(body); return body;
    }
    static void AbyssWorldCraft(GameState p,Map map,uint output) {
        uint family=output==722722?3632u:output==722726?3633u:output==722729?3634u:output==722731?3635u:0;
        foreach(object cost in AbyssCost(family==0?"ExchangeCost":"SummonCost",family==0?output:family))
            while(AbyssAmount(p,AbyssType(cost))<AbyssCount(cost))AbyssWorldCraft(p,map,AbyssType(cost));
        if(family==0) { AbyssAt(p,map,3601);var plan=AbyssSelect(p,"ExchangeCost",output);var item=AbyssWrite(p,"Exchange",AbyssVersion(p),output,plan);AbyssApply(p,plan,item); }
        else AbyssFight(p,AbyssActor(p,map,family));
    }
    static void VerifyAbyssWorld(string runtime,Map town) {
        VerifyAbyssJournal(runtime,town); var map=Kernel.Maps[1700];
        AbyssWorld.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>abyssNow));
        AbyssWorld.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>0)); Call(AbyssWorld,null,"Initialize");
        var p=AbyssHunter(town,map); var serpent=AbyssWild(p,3606);
        Check(serpent.Level==120 && serpent.MaxHitpoints==50000 && serpent.MinAttack==1400 && serpent.MaxAttack==1728
            && MonsterInformation.MonsterInformations[3606].Hitpoints==1000000,"Abyss fixes Crying Serpent locally without rewriting its shared catalog");
        map.RemoveEntity(serpent);
        foreach(uint family in Enumerable.Range(3632,15).Select(n=>(uint)n)) {
            var profile=(MonsterInformation)Call(AbyssWorld,null,"Profile",family);
            Check(profile.ID==9920000+family && profile.Boss && profile.MinAttack<=profile.MaxAttack && profile.Level==120,
                "reviewed Abyss encounter has a scoped identity, boss presentation and valid combat range: "+family);
            var expectedNames=new[]{"Hill Spirit","Swift Devil","Vigor Banshee","Cleansing Devil","Cursed Andrew","Cursed Peter","Cursed Philip","Cursed Timothy","Cursed Daphne","Cursed Victoria","Cursed Wayne","Cursed Theodore","Sealed Satan","Beast Satan","Fury Satan"};
            uint[] health={50000,60000,60460,65535,65535,65535,65535,65535,65535,65535,65535,65535,100000,150000,200000};
            var body=new Entity(EntityFlag.Monster,false){UID=890000+family,MapID=1700,Name=profile.Name,MonsterInfo=profile,MaxHitpoints=profile.Hitpoints};profile.Owner=body;
            Check(profile.Name==expectedNames[family-3632] && profile.Hitpoints==health[family-3632]
                && System.Text.Encoding.ASCII.GetString(body.CharBuffer.Names[0])==expectedNames[family-3632]
                && profile.Name!=MonsterInformation.MonsterInformations[family==3646?3647:family].Name,
                "Abyss spawn name and health match the independent native alias without overwriting the legacy name: "+family);
            BossHealthPackets(body);
        }
        var originalHill=MonsterInformation.MonsterInformations[3632];uint hillHp=originalHill.Hitpoints;originalHill.Hitpoints++;
        try {Check(AbyssRejected(()=>Call(AbyssWorld,null,"Profile",3632u)),"changed catalog health cannot silently diverge from the staged Abyss HUD");}
        finally {originalHill.Hitpoints=hillHp;}
        // Establish exact base inputs, then exercise every paid actor in the full chain.
        foreach(uint type in new uint[]{722723,722724,722725})NestedPack(p,type,117,type==722723);
        for(int n=0;n<3;n++)AbyssWorldCraft(p,map,722731);
        Check(AbyssAmount(p,722731)==3 && p.Inventory.Objects.Where(i=>i.ID==722731).All(i=>i.Bound)
            && new uint[]{722723,722724,722725}.All(type=>AbyssAmount(p,type)==0),"120 actual registered material deaths deliver exactly three bound Pure Vigors");
        AbyssAt(p,map,3601);var vigor=AbyssVigor(p);AbyssWrite(p,"TurnIn",AbyssVersion(p),3601u,vigor);AbyssApply(p,vigor,null);
        AbyssAt(p,map,3602);AbyssWrite(p,"Introduce",AbyssVersion(p));Call(AbyssJournal,null,"Refresh",p,false);
        var lord=AbyssWild(p,3603);lord.Hitpoints=0;
        for(int n=0;n<80;n++) {
            if(n>0){lord.Hitpoints=lord.MaxHitpoints;lord.ClearFlagsMobs();Call(AbyssWorld,null,"Revived",lord);lord.Hitpoints=0;}
            Call(AbyssWorld,null,"OnWildDeath",lord,p.Entity);Call(AbyssWorld,null,"OnWildDeath",lord,p.Entity);
            if(Fenced(p))throw new Exception("Abyss wild fixture fenced.");
        }
        Check(AbyssValue(AbyssField(AbyssRead(p),"Progress"),"Meters")==40000,"eighty canonical lord lives count once each across repeated death notifications");map.RemoveEntity(lord);
        AbyssAt(p,map,3602);AbyssWrite(p,"TurnIn",AbyssVersion(p),3602u,null);AbyssAt(p,map,3603);AbyssWrite(p,"Introduce",AbyssVersion(p));Call(AbyssJournal,null,"Refresh",p,false);
        for(int n=0;n<72;n++)AbyssFight(p,AbyssActor(p,map,3636u+(uint)(n%8)));
        AbyssAt(p,map,3603);AbyssWrite(p,"TurnIn",AbyssVersion(p),3603u,null);Call(AbyssJournal,null,"Refresh",p,false);
        var beadLord=AbyssWild(p,3607);beadLord.Hitpoints=0;Call(AbyssWorld,null,"OnWildDeath",beadLord,p.Entity);map.RemoveEntity(beadLord);
        for(uint family=3644;family<=3646;family++) {
            var finalActor=AbyssActor(p,map,family);
            if(family==3646)foreach(string target in new[]{"revora_abyss_completions","revora_achievement_evidence"}) {
                Query("CREATE FUNCTION abyss_final_reject() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$");
                Query("CREATE TRIGGER abyss_final_reject BEFORE INSERT ON "+target+" FOR EACH ROW EXECUTE FUNCTION abyss_final_reject()");
                try { AbyssBody(finalActor).Die(p.Entity);Check(Fenced(p) && !AbyssBody(finalActor).Dead
                    && AbyssValue(AbyssField(AbyssRead(p),"Progress"),"SatanForms")==2
                    && Scalar("SELECT count(*) FROM revora_abyss_completions WHERE uid=@uid",p.Entity.UID)==0,
                    "failed final proof or achievement rolls back Satan's last defeat: "+target); }
                finally { Query("DROP TRIGGER abyss_final_reject ON "+target);Query("DROP FUNCTION abyss_final_reject()"); }
                p=ReloadBall(p);SkillTable.LoadProficiencies(p);Call(AbyssJournal,null,"Refresh",p,false);Call(AbyssWorld,null,"Tick");finalActor=AbyssActor(p,map,family,true);
            }
            AbyssFight(p,finalActor);
        }
        Check(HasAchievement(p,11031) && !HasAchievement(p,10316)
            && Scalar("SELECT count(*) FROM revora_abyss_completions WHERE uid=@uid",p.Entity.UID)==1,
            "final registered Satan death records quest achievement 11031 without second-rebirth credit");
        var loaded=ReloadBall(p);SkillTable.LoadProficiencies(loaded);loaded.Entity.MyAchievement.Send();
        Check(HasAchievement(loaded,11031) && HasAchievement(loaded,10525) && AbyssValue(AbyssField(AbyssRead(loaded),"Progress"),"Step")==5,"actual Abyss combat completion and achievement survive production reconnect");
        abyssWorldCompleter=loaded;
        VerifyAbyssOwnership(town,map);VerifyAbyssCombatRecovery(town,map);VerifyAbyssDropIsolation(town,map);
        VerifyAbyssPvp(runtime,town,map);
    }
    static void VerifyAbyssOwnership(Map town,Map map) {
        var p=AbyssHunter(town,map);foreach(uint type in new uint[]{722723,722724,722725})Pack(p,type,2);
        var actor=AbyssActor(p,map,3632);var body=AbyssBody(actor);var stranger=AbyssHunter(town,map);stranger.Entity.X=p.Entity.X;stranger.Entity.Y=p.Entity.Y;
        Check(Handle.CanAttack(p.Entity,body,null,true) && !Handle.CanAttack(stranger.Entity,body,null,true)
            && !Handle.CanAttack(body,stranger.Entity,null,true),"paid Abyss encounter accepts only its owner and cannot attack nearby strangers");
        body.Die(stranger.Entity);Check(!body.Dead && AbyssPending(p)==0,"foreign kill callback cannot steal an owned Abyss encounter or its spoils");
        var copied=new Entity(EntityFlag.Monster,false){UID=body.UID,MapID=1700,MonsterInfo=body.MonsterInfo.Copy()};copied.MonsterInfo.Owner=copied;
        copied.Die(p.Entity);Check(!body.Dead && AbyssPending(p)==0,"copied actor UID and family cannot author a death receipt");
        p.Trade.InTrade=true;body.Die(p.Entity);Check(!Fenced(p) && !body.Dead && !Handle.CanAttack(p.Entity,body,null,true),"trade suspends owned encounter combat without fencing or charging the player");p.Trade.InTrade=false;
        var same=AbyssActor(p,map,3632,true);Check(ReferenceEquals(actor,same) && AbyssAmount(p,722723)==1,"repeated summon reuses the live actor and offering");
        abyssNow=abyssNow.AddMinutes(31);Call(AbyssWorld,null,"Tick");
        Check(!map.Entities.ContainsKey(body.UID) && !body.Dead,"expired live actor retires without defeating or refunding its durable entitlement");
        var replacement=AbyssActor(p,map,3632,true);Check(!ReferenceEquals(actor,replacement) && AbyssId(AbyssField(actor,"Encounter"))==AbyssId(AbyssField(replacement,"Encounter"))
            && AbyssAmount(p,722723)==1,"expired encounter resumes the same paid identity without another ingredient");
        AbyssFight(p,replacement);var item=p.Inventory.Objects.Single(i=>i.ID==722722);AbyssBody(replacement).Die(p.Entity);
        Check(AbyssAmount(p,722722)==1 && !item.Bound,"duplicate death cannot replay unbound paid spoils");
    }
    static void VerifyAbyssCombatRecovery(Map town,Map map) {
        var p=AbyssHunter(town,map);foreach(uint type in new uint[]{722723,722724,722725})NestedPack(p,type,1,type==722723);
        var actor=AbyssActor(p,map,3632);var body=AbyssBody(actor);Guid receipt=AbyssId(AbyssField(actor,"Encounter"));
        Query("CREATE FUNCTION abyss_combat_reject() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$");
        Query("CREATE TRIGGER abyss_combat_reject BEFORE UPDATE ON revora_abyss_encounters FOR EACH ROW EXECUTE FUNCTION abyss_combat_reject()");
        try {body.Die(p.Entity);Check(Fenced(p) && !body.Dead && AbyssPending(p)==0,"failed paid death save leaves the monster undefeated and no partial spoils");}
        finally {Query("DROP TRIGGER abyss_combat_reject ON revora_abyss_encounters");Query("DROP FUNCTION abyss_combat_reject()");}
        p=ReloadBall(p);SkillTable.LoadProficiencies(p);Call(AbyssJournal,null,"Refresh",p,false);Call(AbyssWorld,null,"Tick");
        var resumed=AbyssActor(p,map,3632,true);Check(AbyssId(AbyssField(resumed,"Encounter"))==receipt,"reconnect after death-save failure resumes the original paid encounter");
        var retryBody=AbyssBody(resumed);
        Query("CREATE FUNCTION abyss_unarmed_reject() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER abyss_unarmed_reject BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW WHEN(NEW.achievement_id=10525) EXECUTE FUNCTION abyss_unarmed_reject()");
        try{retryBody.Die(p.Entity);}finally{Query("DROP TRIGGER abyss_unarmed_reject ON revora_achievement_evidence;DROP FUNCTION abyss_unarmed_reject()");}
        Check(Fenced(p) && !retryBody.Dead && AbyssPending(p)==0
            && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10525",p.Entity.UID)==0,"suppressed Abyss unarmed credit rolls back encounter defeat and spoils");
        p=ReloadBall(p);SkillTable.LoadProficiencies(p);Call(AbyssJournal,null,"Refresh",p,false);Call(AbyssWorld,null,"Tick");resumed=AbyssActor(p,map,3632,true);
        Check(AbyssId(AbyssField(resumed,"Encounter"))==receipt,"unarmed failure reconnect preserves the original paid Abyss entitlement");
        while(p.Inventory.Count<40)Pack(p,1000000,1);AbyssFight(p,resumed);
        Check(HasAchievement(p,10525),"reconnected Abyss retry grants unarmed credit with its saved spoils");
        Check(AbyssPending(p)==1 && AbyssAmount(p,722722)==0,"full bag retains actual combat spoils as a saved pending reward");
        var filler=p.Inventory.Objects.First(i=>i.ID==1000000);Query("DELETE FROM items WHERE uid=@uid","uid",(long)filler.UID);p.Inventory.Remove(filler.UID,Enums.ItemUse.None,true);
        lock(p.ItemSyncRoot)Check((bool)Call(AbyssWorld,null,"ClaimOne",p),"ordinary claim recovers previously saved full-bag combat spoils");
        Check(p.Inventory.Objects.Single(i=>i.ID==722722).Bound && AbyssPending(p)==0,"recovered combat spoils preserve ingredient binding");
    }
    static void VerifyAbyssDropIsolation(Map town,Map map) {
        var p=AbyssHunter(town,map);p.Quests.Accept((QuestID)2414);p.Quests.Accept((QuestID)2419);
        var loose=AbyssWild(p,3644);loose.MonsterInfo.Drop(p.Entity);
        Check(p.Entity.MapID==1700 && !p.Entity.SecoundQuestDone && p.Quests.CheckQuest((QuestID)2419)==MsgTaskStatus.QuestData.QuestStatus.Accepted
            && !HasAchievement(p,11031),"selected Abyss route suppresses the old loose-Satan one-kill completion and teleport");map.RemoveEntity(loose);
        var wild=AbyssWild(p,3600);wild.Die(p.Entity);
        Check(!Fenced(p) && wild.Dead && AbyssAmount(p,722723)==1
            && Scalar("SELECT count(*) FROM revora_abyss_deaths d JOIN revora_abyss_trials r ON r.id=d.run WHERE r.uid=@uid",p.Entity.UID)==1,
            "ordinary monster DieCore reaches the durable Abyss material hook exactly once");map.RemoveEntity(wild);
        foreach(uint type in new uint[]{722724,722725})Pack(p,type,1);
        var actor=AbyssActor(p,map,3632);var pet=new Entity(EntityFlag.Monster,false){UID=899999,MapID=1700,Owner=p,Companion=true,X=p.Entity.X,Y=p.Entity.Y};
        pet.MonsterInfo=MonsterInformation.MonsterInformations[3600].Copy();pet.MonsterInfo.Owner=pet;pet.MaxHitpoints=pet.Hitpoints=1000;p.Companion=pet;map.Companions.Add(pet.UID,pet);
        var body=AbyssBody(actor);Check(Handle.CanAttack(pet,body,null,true),"registered owned companion may fight its master's paid encounter");body.Die(pet);
        Check(!Fenced(p) && body.Dead && AbyssAmount(p,722722)==1,"canonical companion kill credits the paid encounter to its owner once");
        p.Entity.MyAchievement.Send();Check(!HasAchievement(p,10525),"Abyss companion encounter credit does not grant an unarmed player kill");map.Companions.Remove(pet.UID);p.Companion=null;
        foreach(uint type in new uint[]{722723,722724,722725})Pack(p,type,1);
        actor=AbyssActor(p,map,3632);body=AbyssBody(actor);map.Entities.Remove(body.UID);
        var other=new Entity(EntityFlag.Monster,false){UID=body.UID,MapID=1700,MonsterInfo=body.MonsterInfo.Copy()};other.MonsterInfo.Owner=other;map.Entities.Add(other.UID,other);
        Call(AbyssWorld,null,"Release",actor);Check(ReferenceEquals(map.Entities[other.UID],other),"retiring a stale actor never removes another entity that now owns its UID");map.RemoveEntity(other);
        var changing=AbyssHunter(town,map);foreach(uint type in new uint[]{722723,722724,722725})Pack(changing,type,1);
        AbyssWorld.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>{changing.Trade.InTrade=true;return abyssNow;}));
        try { Check(AbyssRejected(()=>AbyssActor(changing,map,3632)) && AbyssVersion(changing)==0
            && new uint[]{722723,722724,722725}.All(type=>AbyssAmount(changing,type)==1)
            && Scalar("SELECT count(*) FROM revora_abyss_encounters e JOIN revora_abyss_trials r ON r.id=e.run WHERE r.uid=@uid",changing.Entity.UID)==0,
            "summon rechecks session context after payment writes and rolls back a newly started trade"); }
        finally {changing.Trade.InTrade=false;AbyssWorld.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>abyssNow));}
    }
}
