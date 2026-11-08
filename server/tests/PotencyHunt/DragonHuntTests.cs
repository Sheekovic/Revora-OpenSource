using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type DragonHunt=typeof(Entity).Assembly.GetType("Revora.Game.Features.DragonSlayerHunt");
    static readonly Type DragonWorld=typeof(Entity).Assembly.GetType("Revora.Game.Features.DragonSlayerWorld");
    static DateTime dragonNow;
    static object DragonField(object o,string name){return o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).GetValue(o);}
    static Entity[] DragonActors(GameState p){return ((IDictionary)DragonWorld.GetField("Actors",Hidden).GetValue(null)).Values.Cast<object>()
        .Where(a=>ReferenceEquals(DragonField(DragonField(a,"Hunt"),"Player"),p)).Select(a=>(Entity)DragonField(a,"Body")).ToArray();}
    static GameState DragonHunter(Map map){var p=DragonPlayer(map);p.Entity.Level=140;p.Quests=new Quests(p);p.Quests.Load();EntityTable.SaveEntity(p);return p;}
    static void DragonGuard(GameState p,byte option){p.Entity.X=361;p.Entity.Y=645;DragonNpc(p,option);}
    static long HuntRows(GameState p,string predicate="true"){return Scalar("SELECT count(*) FROM revora_dragon_hunts WHERE uid=@uid AND "+predicate,p.Entity.UID);}
    static long HuntMask(GameState p){return Scalar("SELECT mask FROM revora_dragon_hunts WHERE uid=@uid AND phase<>'complete'",p.Entity.UID);}
    static long HuntKills(GameState p){return Scalar("SELECT count(*) FROM revora_dragon_hunt_kills k JOIN revora_dragon_hunts h ON h.id=k.run WHERE h.uid=@uid",p.Entity.UID);}
    static void KillDragonActor(GameState p,Entity mob){p.Entity.X=mob.X;p.Entity.Y=mob.Y;mob.Die(p.Entity);if(Fenced(p))throw new Exception("Dragon hunt kill fenced the fixture");}
    static void AtDragonSite(GameState p,int branch){ushort[] x={511,599,515,441,183,109,125},y={570,439,329,317,375,335,279};p.Entity.X=(ushort)(x[branch]+3);p.Entity.Y=y[branch];}
    static void ClearDragonWorld(){foreach(var h in ((IDictionary)DragonWorld.GetField("Hunts",Hidden).GetValue(null)).Values.Cast<object>().ToArray())
        DragonWorld.GetMethods(Hidden).Single(m=>m.Name=="Release" && m.GetParameters()[0].ParameterType!=typeof(GameState)).Invoke(null,new[]{h});}
    static void FinishDragonMinions(GameState p){foreach(var mob in DragonActors(p))KillDragonActor(p,mob);DragonGuard(p,40);}
    static void VerifyDragonHunt(string runtime) {
        dragonNow=DateTime.UtcNow;DragonHunt.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>dragonNow));
        var map=Kernel.Maps[2056];Call(DragonHunt,null,"Initialize");Call(DragonHunt,null,"Initialize");
        Check(Enumerable.Range(0,7).All(i=>map.Npcs.ContainsKey((uint)(900000120+i))),"all seven reviewed ghoul statue sites are installed idempotently");
        var p=DragonHunter(map);uint wardrobe=StoredQuestGarment(p);
        for(int branch=0;branch<7;branch++) {
            DragonGuard(p,(byte)(30+branch));var grass=p.Inventory.Objects.Single(i=>i.ID==711189+branch);ItemHandler.UseItem(grass,p);
            Check(!Fenced(p) && DragonActors(p).Length==9 && DragonActors(p).All(m=>m.Level==128 && m.MaxHitpoints==65535)
                && grass.Bound,"branch "+branch+" claims owned grass and summons nine reviewed minion profiles");
            ItemHandler.UseItem(grass,p);Check(DragonActors(p).Length==9,"repeated grass use does not multiply branch "+branch+" summons");
            if(branch==0) {
                var first=DragonActors(p)[0];p.Entity.X=first.X;p.Entity.Y=first.Y;Parallel.For(0,8,i=>first.Die(p.Entity));
                Check(!Fenced(p) && HuntKills(p)==1 && DragonActors(p).Length==8,"concurrent lethal dispatch credits one minion slot once");
                var stranger=DragonHunter(map);var target=DragonActors(p)[0];stranger.Entity.X=target.X;stranger.Entity.Y=target.Y;
                target.Die(stranger.Entity);Check(!target.Dead && HuntKills(p)==1 && (bool)Call(DragonWorld,null,"BlockAttack",stranger.Entity,target)
                    && (bool)Call(DragonWorld,null,"BlockAttack",target,stranger.Entity),"owned minions cannot attack bystanders or be credited to another player");
                Check(!(bool)Call(DragonWorld,null,"BlockAttack",p.Entity,target) && !(bool)Call(DragonWorld,null,"BlockAttack",target,p.Entity),"the eligible owner and their minions can fight each other");
                Check(!Revora.Game.Attacking.Handle.CanAttack(stranger.Entity,target,null,true)
                    && !Revora.Game.Attacking.Handle.CanAttack(target,stranger.Entity,null,true)
                    && Revora.Game.Attacking.Handle.CanAttack(p.Entity,target,null,true),
                    "production combat admission permits the hunter and rejects unrelated attackers and targets");
                dragonNow=dragonNow.AddMinutes(16);Call(DragonWorld,null,"Tick");Check(DragonActors(p).Length==0 && HuntMask(p)==1,"summon timeout releases actors while retaining the credited slot");
                p.Entity.X=361;p.Entity.Y=645;ItemHandler.UseItem(grass,p);Check(DragonActors(p).Length==8,"resuming a grass hunt recreates only its eight unfinished minions");
            }
            FinishDragonMinions(p);var pellet=p.Inventory.Objects.Single(i=>i.ID==720591+branch);
            Check(!Fenced(p) && HuntMask(p)==511 && !p.Inventory.ContainsUID(grass.UID) && pellet.Bound && HuntRows(p,"phase='pellet'")==1,
                "branch "+branch+" exchanges exactly nine recorded minions and its grass for a pellet");
            ItemHandler.UseItem(pellet,p);Check(p.Inventory.ContainsUID(pellet.UID) && DragonActors(p).Length==0,"pellet "+branch+" cannot be spent away from its statue");
            AtDragonSite(p,branch);ItemHandler.UseItem(pellet,p);
            Check(!Fenced(p) && DragonActors(p).Length==1 && !p.Inventory.ContainsUID(pellet.UID) && HuntRows(p,"phase='ghoul'")==1,
                "branch "+branch+" spends its pellet once and awakens the matching ghoul");
            if(branch==0){dragonNow=dragonNow.AddMinutes(16);Call(DragonWorld,null,"Tick");AtDragonSite(p,branch);ExplorationNpc(p,900000120,1);
                Check(!Fenced(p) && DragonActors(p).Length==1 && HuntRows(p,"phase='ghoul'")==1,"a paid ghoul summon resumes at its statue without a second pellet");}
            KillDragonActor(p,DragonActors(p).Single());
            Check(!Fenced(p) && p.Inventory.Objects.Any(i=>i.ID==711181+branch && i.Bound) && HuntRows(p,"phase='complete'")==branch+1,
                "branch "+branch+" yields its distinct durable ghoul pill from genuine encounter death");
        }
        DragonGuard(p,6);DragonNpc(p,60);
        Check(!Fenced(p) && p.Inventory.Count==1 && p.Inventory.Objects[0].ID==720598 && p.Inventory.Objects[0].Bound && HuntKills(p)==70 && DragonCrafts(p)==1,
            "all seven ordinary grass-to-ghoul earning branches feed one atomically crafted Dragon Pill");
        Check(EntityTable.SaveEntity(p) && ConquerItemTable.LoadItem(p.Inventory.Objects[0].UID).ID==720598,"the fully earned Dragon Pill survives production character and item save");
        var early=DragonHunter(map);DragonGuard(early,30);var earlyGrass=early.Inventory.Objects[0];DragonGuard(early,40);
        Check(early.Inventory.ContainsUID(earlyGrass.UID) && HuntRows(early,"phase='minions'")==1,"grass cannot be exchanged before all nine minions are defeated");
        DragonGuard(early,31);Check(HuntRows(early)==1 && early.Inventory.Count==1,"a second branch cannot replace an unfinished owned hunt");
        var legacy=Pack(early,720591,1);AtDragonSite(early,0);ItemHandler.UseItem(legacy,early);
        Check(early.Inventory.ContainsUID(legacy.UID) && DragonActors(early).Length==0,"unreceipted pellets cannot impersonate earned hunt materials");
        var recover=DragonHunter(map);DragonGuard(recover,30);var lost=recover.Inventory.Objects[0];Query("DELETE FROM items WHERE uid=@uid","uid",(long)lost.UID);
        recover.Inventory.Remove(lost.UID,Enums.ItemUse.None,false);DragonGuard(recover,42);
        Check(!Fenced(recover) && recover.Inventory.Count==1 && recover.Inventory.Objects[0].UID!=lost.UID && HuntRows(recover)==1,"lost unspent quest material can be recovered without creating a second hunt");
        var warehouse=DragonHunter(map);DragonGuard(warehouse,30);var stored=warehouse.Inventory.Objects[0];Query("UPDATE items SET warehouse=1002 WHERE uid=@uid","uid",(long)stored.UID);
        warehouse.Inventory.Remove(stored.UID,Enums.ItemUse.None,false);DragonGuard(warehouse,42);
        Check(warehouse.Inventory.Count==0 && HuntRows(warehouse)==1,"a stored quest material cannot be duplicated through recovery");
        var full=DragonHunter(map);DragonGuard(full,30);ItemHandler.UseItem(full.Inventory.Objects[0],full);FinishDragonMinions(full);AtDragonSite(full,0);
        ItemHandler.UseItem(full.Inventory.Objects.Single(i=>i.ID==720591),full);for(int n=0;n<40;n++)Pack(full,1088001,1);
        KillDragonActor(full,DragonActors(full).Single());Check(!Fenced(full) && full.Inventory.Count==40 && HuntRows(full,"phase='victory'")==1,"full inventory retains a defeated ghoul's unclaimed pill");
        var removed=full.Inventory.Objects[0];Query("DELETE FROM items WHERE uid=@uid","uid",(long)removed.UID);full.Inventory.Remove(removed.UID,Enums.ItemUse.None,false);DragonGuard(full,42);
        Check(full.Inventory.Count==40 && full.Inventory.Objects.Any(i=>i.ID==711181) && HuntRows(full,"phase='complete'")==1,"earned ghoul pill can be claimed after making room");
        VerifyDragonUnarmed(map);
        VerifyDragonHuntFailures(map);
        VerifyDragonCompanion(map);
        VerifyDragonHuntReconnect(map);
        ClearDragonWorld();
        VerifyTerato(runtime,p);
        Check(Scalar("SELECT count(*) FROM items WHERE uid=@uid AND position=0 AND warehouse=0 AND inwardrobe=1",wardrobe)==1,
            "seven ghoul hunts, Dragon Pill crafting and Terato victory preserve the stored wardrobe design");
    }
    static void VerifyDragonUnarmed(Map map) {
        foreach(string scenario in new[]{"direct","weapon","evidence"}) {
            var p=DragonHunter(map);DragonGuard(p,30);ItemHandler.UseItem(p.Inventory.Objects[0],p);var mob=DragonActors(p)[0];
            if(scenario=="weapon")RebirthGear(p,4);
            p.Entity.X=mob.X;p.Entity.Y=mob.Y;
            if(scenario=="evidence")Query("CREATE FUNCTION suppress_dragon_unarmed() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_dragon_unarmed BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW WHEN(NEW.achievement_id=10525) EXECUTE FUNCTION suppress_dragon_unarmed()");
            try {
                uint hit=mob.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,mob,new Attack(true){AttackType=Attack.Melee,Attacked=mob.UID,Attacker=p.Entity.UID},ref hit,null);
            } finally {if(scenario=="evidence")Query("DROP TRIGGER suppress_dragon_unarmed ON revora_achievement_evidence;DROP FUNCTION suppress_dragon_unarmed()");}
            if(scenario=="evidence") {
                Check(Fenced(p) && !mob.Dead && HuntKills(p)==0 && HuntMask(p)==0
                    && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10525",p.Entity.UID)==0,"failed unarmed evidence rolls back Dragon kill receipt and objective");
                p=ReloadBall(p);Call(DragonWorld,null,"Tick");DragonGuard(p,0);ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==711189),p);mob=DragonActors(p)[0];
                KillDragonActor(p,mob);
            }
            p.Entity.MyAchievement.Send();
            Check(!Fenced(p) && mob.Dead && HuntKills(p)==1 && HasAchievement(p,10525)==(scenario!="weapon"),"Dragon lethal dispatch records correct unarmed eligibility: "+scenario);
            mob.Die(p.Entity);Check(HuntKills(p)==1,"Dragon corpse replay cannot duplicate its kill receipt: "+scenario);
            ClearDragonWorld();p=ReloadBall(p);p.Entity.MyAchievement.Send();
            Check(HasAchievement(p,10525)==(scenario!="weapon"),"Dragon weapon eligibility persists after full reload: "+scenario);
        }
    }
    static void VerifyDragonHuntFailures(Map map) {
        foreach(string state in new[]{"far","dead","fake","trade","replaced"}) {
            var p=DragonHunter(map);p.ActiveNpc=7767;if(state=="far")p.Entity.X=1;if(state=="dead")p.Entity.Hitpoints=0;
            if(state=="fake")p.Fake=true;if(state=="trade")p.Trade.InTrade=true;if(state=="replaced")Kernel.GamePool.Remove(p.Entity.UID);
            DragonNpc(p,30);Check(HuntRows(p)==0 && p.Inventory.Count==0,"ineligible hunt admission is rejected: "+state);
        }
        var failed=DragonHunter(map);DragonGuard(failed,30);ItemHandler.UseItem(failed.Inventory.Objects[0],failed);var target=DragonActors(failed)[0];
        Query("CREATE FUNCTION suppress_dragon_kill() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_dragon_kill BEFORE INSERT ON revora_dragon_hunt_kills FOR EACH ROW EXECUTE FUNCTION suppress_dragon_kill()");
        failed.Entity.X=target.X;failed.Entity.Y=target.Y;try{target.Die(failed.Entity);}finally{Query("DROP TRIGGER suppress_dragon_kill ON revora_dragon_hunt_kills;DROP FUNCTION suppress_dragon_kill()");}
        Check(Fenced(failed) && HuntMask(failed)==0 && HuntKills(failed)==0 && !target.Dead,"failed death receipt preserves the living actor and uncredited objective");
        DemonReset(failed);KillDragonActor(failed,target);Check(HuntKills(failed)==1 && !Fenced(failed),"a death receipt can recover after session reload without duplicate credit");
        var debit=DragonHunter(map);DragonGuard(debit,30);ItemHandler.UseItem(debit.Inventory.Objects[0],debit);FinishDragonMinions(debit);var pellet=debit.Inventory.Objects.Single();
        Query("UPDATE items SET locked=1 WHERE uid=@uid","uid",(long)pellet.UID);AtDragonSite(debit,0);ItemHandler.UseItem(pellet,debit);
        Check(Fenced(debit) && debit.Inventory.ContainsUID(pellet.UID) && DragonActors(debit).Length==0 && HuntRows(debit,"phase='pellet'")==1,
            "stale pellet ownership rolls back payment and removes its reserved ghoul encounter");
        var reward=DragonHunter(map);DragonGuard(reward,30);ItemHandler.UseItem(reward.Inventory.Objects[0],reward);FinishDragonMinions(reward);
        AtDragonSite(reward,0);ItemHandler.UseItem(reward.Inventory.Objects[0],reward);var ghoul=DragonActors(reward).Single();
        Query("CREATE FUNCTION suppress_ghoul_reward() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_ghoul_reward BEFORE INSERT ON items FOR EACH ROW EXECUTE FUNCTION suppress_ghoul_reward()");
        reward.Entity.X=ghoul.X;reward.Entity.Y=ghoul.Y;try{ghoul.Die(reward.Entity);}finally{Query("DROP TRIGGER suppress_ghoul_reward ON items;DROP FUNCTION suppress_ghoul_reward()");}
        Check(Fenced(reward) && HuntKills(reward)==9 && HuntRows(reward,"phase='ghoul'")==1 && !ghoul.Dead && reward.Inventory.Count==0,
            "failed ghoul reward rolls back death credit and retains the paid encounter entitlement");
        DemonReset(reward);KillDragonActor(reward,ghoul);Check(!Fenced(reward) && reward.Inventory.Objects.Single().ID==711181 && HuntKills(reward)==10,
            "ghoul reward retry grants exactly one pill without another pellet payment");
    }
    static void VerifyDragonCompanion(Map map) {
        var p=DragonHunter(map);DragonGuard(p,30);ItemHandler.UseItem(p.Inventory.Objects[0],p);var target=DragonActors(p)[0];
        p.Entity.X=target.X;p.Entity.Y=target.Y;
        var pet=new Entity(EntityFlag.Monster,true){UID=p.Entity.UID-200000,Owner=p,MonsterInfo=new MonsterInformation(),MapID=2056,X=target.X,Y=target.Y,Hitpoints=1000,MaxHitpoints=1000};
        p.Companion=pet;
        target.Die(pet);Check(!target.Dead && HuntKills(p)==0,"unregistered companion cannot claim hunt credit");
        map.Companions.Add(pet.UID,pet);
        Check(Revora.Game.Attacking.Handle.CanAttack(pet,target,null,true)
            && !(bool)Call(DragonWorld,null,"BlockAttack",target,pet),"registered owner companion can fight its hunter's encounter");
        var replacement=new Entity(EntityFlag.Monster,true){UID=pet.UID,Owner=p,MonsterInfo=new MonsterInformation(),MapID=2056,Hitpoints=1000};
        p.Companion=replacement;target.Die(pet);
        Check(!target.Dead && HuntKills(p)==0 && (bool)Call(DragonWorld,null,"BlockAttack",pet,target),"replaced companion loses encounter access and death credit");
        p.Companion=pet;target.Die(pet);
        Check(!Fenced(p) && HuntKills(p)==1 && DragonActors(p).Length==8,"registered companion death credits its ordinary owner's objective exactly once");
        p.Entity.MyAchievement.Send();Check(!HasAchievement(p,10525),"Dragon companion objective credit does not imply an unarmed player kill");
        map.Companions.Remove(pet.UID);p.Companion=null;ClearDragonWorld();
    }
    static void VerifyDragonHuntReconnect(Map map) {
        ClearDragonWorld();var old=DragonHunter(map);DragonGuard(old,32);ItemHandler.UseItem(old.Inventory.Objects[0],old);
        foreach(var mob in DragonActors(old).Take(3))KillDragonActor(old,mob);
        Check(EntityTable.SaveEntity(old) && HuntKills(old)==3,"partial minion progress survives a normal character save");
        var fresh=DragonHunter(map);uint placeholder=fresh.Entity.UID;fresh.Account.EntityID=old.Entity.UID;fresh.Account.Username="dragon_hunt_fixture";fresh.Quests=new Quests(fresh);
        Check((bool)Call(typeof(EntityTable),null,"LoadEntityState",fresh),"mid-hunt reconnect loads the ordinary character state");
        Kernel.GamePool.Remove(placeholder);Kernel.GamePool[old.Entity.UID]=fresh;fresh.Action=2;ConquerItemTable.LoadItems(fresh);Call(DragonWorld,null,"Tick");
        fresh.Entity.X=361;fresh.Entity.Y=645;ItemHandler.UseItem(fresh.Inventory.Objects.Single(),fresh);
        Check(!Fenced(fresh) && DragonActors(old).Length==0 && DragonActors(fresh).Length==6 && HuntKills(fresh)==3,
            "replaced session loses actor ownership and the fresh session resumes only six unfinished minions");
        fresh.Entity.MyAchievement.Send();Check(HasAchievement(fresh,10525),"Dragon unarmed credit survives production reconnect alongside partial hunt progress");
        FinishDragonMinions(fresh);AtDragonSite(fresh,2);ItemHandler.UseItem(fresh.Inventory.Objects.Single(),fresh);ClearDragonWorld();
        Check(fresh.Inventory.Count==0 && HuntRows(fresh,"phase='ghoul'")==1,"clearing transient actors preserves the committed ghoul summon");
        AtDragonSite(fresh,2);ExplorationNpc(fresh,900000122,1);KillDragonActor(fresh,DragonActors(fresh).Single());
        Check(!Fenced(fresh) && fresh.Inventory.Objects.Single().ID==711183 && HuntKills(fresh)==10,
            "a persisted paid summon can be reconstructed after world loss and grant its earned Point Pill");
    }
}
