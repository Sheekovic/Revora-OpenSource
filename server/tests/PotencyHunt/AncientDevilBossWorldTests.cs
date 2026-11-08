using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.Attacking;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type AncientBossWorld=typeof(Entity).Assembly.GetType("Revora.Game.Features.AncientDevilBossWorld");
    static object BossStart(GameState p,uint[] offering=null){lock(p.ItemSyncRoot)return Call(AncientBossWorld,null,"Start",p,offering);}
    static Entity[] BossActors(object hunt){return (Entity[])AbyssField(hunt,"Bodies");}
    static void VerifyAncientBossWorld(string runtime,Map town) {
        VerifyAncientSummons(runtime,town,true);var p=ancientCollector;Guid paid=ancientPaidRun;
        Kernel.GamePool.Remove(p.Entity.UID);Call(AncientBossWorld,null,"Initialize");p=ReloadBall(p);SealAt(p);
        var hunt=BossStart(p);var actors=BossActors(hunt);ushort map=p.Entity.MapID;
        Check(p.Map.BaseID==1082 && p.Map.Entities.Count==5 && p.Map.Npcs.ContainsKey(300016),"paid summon publishes four guards, boss and return guide in owned island");
        Check(actors.Take(4).All(actor=>actor.Name=="DevilGuard" && actor.Body==205 && actor.MaxHitpoints==6590),"four guardians preserve installed DevilGuard identity and health");
        Check(AbyssId(AbyssField(hunt,"Run"))==paid && p.Inventory.Count==1,"paid summon resumes original offering without further consumption");
        Check(actors[4].Name=="AncientDevil" && actors[4].MaxHitpoints==65535 && actors[4].Hitpoints==65535
            && actors[4].Boss==1 && actors[4].MonsterInfo.Boss && actors[4].MinAttack==1000 && actors[4].MaxAttack==1500,"Ancient boss uses full health and the existing boss HUD flag path");
        Check(!Handle.CanAttack(p.Entity,actors[4],null,true) && AncientHit(p.Entity,actors[4],65535)==0,"four standing guards protect boss before damage");
        p.Attackable=false;Check(!Handle.CanAttack(actors[0],p.Entity,null,true),"guard attacks retain ordinary player attackability protection");
        p.Attackable=true;Check(Handle.CanAttack(actors[0],p.Entity,null,true) && !Handle.CanAttack(actors[4],p.Entity,null,true),"guards can fight while the sealed boss remains dormant");
        var outsider=AncientHunter(town,Kernel.Maps[1052]);outsider.Entity.Teleport((ushort)1082,map,(ushort)216,(ushort)208);
        Check(outsider.Entity.MapID==1052,"foreign player cannot teleport into paid battle");
        uint health=actors[0].Hitpoints;
        Check(AncientHit(outsider.Entity,actors[0],health)==0 && actors[0].Hitpoints==health,"foreign direct damage cannot mutate guard HP");
        actors[0].Die(outsider.Entity);Check(!actors[0].Dead,"foreign death callback cannot credit guard");
        var copy=new Entity(EntityFlag.Monster,false){UID=actors[0].UID,MapID=map,MonsterInfo=actors[0].MonsterInfo.Copy(),Hitpoints=health};copy.MonsterInfo.Owner=copy;
        copy.Die(p.Entity);Check(!actors[0].Dead,"counterfeit actor with matching identity cannot credit guard");
        p.Trade.InTrade=true;Check(AncientHit(p.Entity,actors[0],health)==0,"trade suspends owned battle damage");p.Trade.InTrade=false;
        using(var ready=new ManualResetEventSlim())using(var release=new ManualResetEventSlim()) {
            var held=Task.Run(()=>{lock(p.ItemSyncRoot){ready.Set();release.Wait();}});ready.Wait();
            try{Check(AncientHit(p.Entity,actors[0],health)==0,"busy owner lock defers battle damage");}finally{release.Set();held.Wait();}
        }
        uint hit=AncientHit(p.Entity,actors[0],100);Check(hit>0 && actors[0].Hitpoints==health-hit,"normal combat subtracts partial guard damage");
        AncientHit(p.Entity,actors[0],actors[0].Hitpoints);
        Check(!Fenced(p) && actors[0].Dead && p.Map.Entities.Count==4 && p.Map.FloorItems.Count==0,"actual guard death records progress and suppresses legacy loot");
        AdventureNear(p,300016);AncientDialog(p,300016,0);AncientDialog(p,300016,1);
        Check(p.Entity.MapID==1052 && !Kernel.Maps.ContainsKey(map),"ordinary return dialogue releases paid scene");
        SealAt(p);hunt=BossStart(p);var resumed=BossActors(hunt);
        Check(AbyssId(AbyssField(hunt,"Run"))==paid && p.Map.Entities.Count==4 && !p.Map.Entities.ContainsKey(resumed[0].UID),"resume retains defeated guard and original paid run");
        actors[1].Die(p.Entity);Check((int)AbyssField(AbyssField(hunt,"Battle"),"Defeated")==1,"released old actor cannot credit resumed battle");
        for(int slot=1;slot<4;slot++){AdventureWalk(p,resumed[slot].X,(ushort)(resumed[slot].Y-1));AncientHit(p.Entity,resumed[slot],resumed[slot].Hitpoints);}
        Check(!Fenced(p) && (int)AbyssField(AbyssField(hunt,"Battle"),"Defeated")==15 && Handle.CanAttack(p.Entity,resumed[4],null,true),"four real guard deaths unlock boss combat");
        p.Entity.MyAchievement.Send();Check(!HasAchievement(p,10525) && resumed.Take(4).All(a=>a.MonsterInfo.Level==100) && resumed[4].MonsterInfo.Level==120,"Ancient level-100 guards do not earn the level-120 unarmed achievement");
        AdventureWalk(p,resumed[4].X,(ushort)(resumed[4].Y-1));
        Query("CREATE FUNCTION suppress_ancient_unarmed() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_ancient_unarmed BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW WHEN(NEW.achievement_id=10525) EXECUTE FUNCTION suppress_ancient_unarmed()");
        try{AncientHit(p.Entity,resumed[4],resumed[4].Hitpoints);}finally{Query("DROP TRIGGER suppress_ancient_unarmed ON revora_achievement_evidence;DROP FUNCTION suppress_ancient_unarmed()");}
        Check(Fenced(p) && !resumed[4].Dead && Scalar("SELECT count(*) FROM revora_ancient_victories v JOIN revora_ancient_summons s ON s.id=v.summon WHERE s.uid=@uid",p.Entity.UID)==0
            && Scalar("SELECT defeated FROM revora_ancient_battles b JOIN revora_ancient_summons s ON s.id=b.summon WHERE s.uid=@uid",p.Entity.UID)==15
            && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10525",p.Entity.UID)==0,"suppressed Ancient unarmed evidence rolls back final defeat and reward while preserving four guard kills");
        map=p.Entity.MapID;Call(AncientBossWorld,null,"Tick");p=ReloadBall(p);SealAt(p);hunt=BossStart(p);resumed=BossActors(hunt);
        Check(!Kernel.Maps.ContainsKey(map) && AbyssId(AbyssField(hunt,"Run"))==paid && p.Map.Entities.Count==1,"unarmed failure reconnect restores the original paid Ancient boss without redoing guards");
        AncientBattles.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>19));
        AdventureWalk(p,resumed[4].X,(ushort)(resumed[4].Y-1));AncientHit(p.Entity,resumed[4],resumed[4].Hitpoints);
        Check(HasAchievement(p,10525),"retried actual Ancient boss lethal hit immediately projects unarmed credit");
        Check(!Fenced(p) && resumed[4].Dead && (bool)AbyssField(hunt,"Won") && p.Map.FloorItems.Count==0,"actual boss death commits saved reward without legacy drops");
        map=p.Entity.MapID;Call(AncientBossWorld,null,"Tick");
        Check(p.Entity.MapID==1052 && !Kernel.Maps.ContainsKey(map),"victory cleanup returns player and releases guarded map");
        SealAt(p);AncientDialog(p,300017,0);AncientDialog(p,300017,3);int rewardCount=p.Inventory.Count;
        Check(rewardCount==3 || rewardCount==8,"earned battle victory grants its one saved reward recipe");
        p=ReloadBall(p);SealAt(p);BattleClaim(p,paid);Check(p.Inventory.Count==rewardCount && p.Inventory.Objects.Any(i=>i.ID>=800000 && i.ID<900000 && i.Bound && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7),"combat-earned reward survives reload and duplicate claim");
        p.Entity.MyAchievement.Send();Check(HasAchievement(p,10525),"Ancient unarmed credit survives production reward reload and duplicate claim");
        ancientNow=ancientNow.AddDays(1);EarnNextOffering(p);SealAt(p);
        uint[] types={710011,710012,710013,710014,710015,710635,711336,711600,3004947};
        var offering=p.Inventory.Objects.Where(i=>types.Contains(i.ID)).Select(i=>i.UID).ToArray();int before=p.Inventory.Count,maps=Kernel.Maps.Count;
        Query("CREATE FUNCTION suppress_boss_binding() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_boss_binding BEFORE INSERT ON revora_ancient_battles FOR EACH ROW EXECUTE FUNCTION suppress_boss_binding()");
        try{Check(AbyssRejected(()=>BossStart(p,offering)),"failed world binding aborts fresh nine-amulet payment");}
        finally{Query("DROP TRIGGER suppress_boss_binding ON revora_ancient_battles;DROP FUNCTION suppress_boss_binding()");}
        Check(p.Inventory.Count==before && Scalar("SELECT count(*) FROM items WHERE entityid=@uid",p.Entity.UID)==before && Kernel.Maps.Count==maps,"failed binding restores all offerings and releases unpublished scene");
        hunt=BossStart(p,offering);var second=AbyssField(hunt,"Run");Guid secondId=AbyssId(second);
        Check(p.Inventory.Count==before-9,"successful fresh world admission consumes exactly nine earned amulets");
        actors=BossActors(hunt);AncientHit(p.Entity,actors[0],actors[0].Hitpoints);map=p.Entity.MapID;
        Kernel.GamePool.Remove(p.Entity.UID);Call(AncientBossWorld,null,"Tick");
        Check(!Kernel.Maps.ContainsKey(map) && Scalar("SELECT mapid FROM entities WHERE uid=@uid",p.Entity.UID)==1052,"disconnect returns saved owner and releases private scene");
        p=ReloadBall(p);SealAt(p);hunt=BossStart(p);
        Check(AbyssId(AbyssField(hunt,"Run"))==secondId && p.Map.Entities.Count==4,"disconnect resume preserves payment and defeated guard");
        var survivor=BossActors(hunt)[1];
        Query("CREATE FUNCTION suppress_boss_kill() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_boss_kill BEFORE INSERT ON revora_ancient_battle_kills FOR EACH ROW EXECUTE FUNCTION suppress_boss_kill()");
        try{survivor.Die(p.Entity);Check(Fenced(p) && !survivor.Dead,"failed canonical death receipt fences owner without committing guard death");}
        finally{Query("DROP TRIGGER suppress_boss_kill ON revora_ancient_battle_kills;DROP FUNCTION suppress_boss_kill()");}
        map=p.Entity.MapID;Call(AncientBossWorld,null,"Tick");p=ReloadBall(p);SealAt(p);hunt=BossStart(p);
        Check(!Kernel.Maps.ContainsKey(map) && p.Map.Entities.Count==4,"failed death recovers paid attempt with only committed guards defeated");
        map=p.Entity.MapID;ancientNow=(DateTime)AbyssField(hunt,"Until");
        Query("CREATE FUNCTION suppress_boss_expiry() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_boss_expiry BEFORE UPDATE ON revora_ancient_summons FOR EACH ROW EXECUTE FUNCTION suppress_boss_expiry()");
        try{Call(AncientBossWorld,null,"Tick");Check(Fenced(p) && Kernel.Maps.ContainsKey(map)
            && Scalar("SELECT count(*) FROM revora_ancient_summons WHERE uid=@uid AND state='pending'",p.Entity.UID)==1,"failed expiry retains scene ownership for cleanup retry");}
        finally{Query("DROP TRIGGER suppress_boss_expiry ON revora_ancient_summons;DROP FUNCTION suppress_boss_expiry()");}
        Call(AncientBossWorld,null,"Tick");p=ReloadBall(p);
        Check(p.Entity.MapID==1052 && !Kernel.Maps.ContainsKey(map) && Scalar("SELECT count(*) FROM revora_ancient_summons WHERE uid=@uid AND state='expired'",p.Entity.UID)==1,"deadline expires battle and returns owner without extending entry");
        SealAt(p);Check(AbyssRejected(()=>BossStart(p)) && Kernel.Maps.Count==maps,"expired same-day entry cannot allocate another playable scene");
        VerifyAncientEarnedUpgrades(p,town);
    }
    static void VerifyAncientEarnedUpgrades(GameState p,Map town) {
        var upgrade=typeof(Entity).Assembly.GetType("Revora.Game.Features.EquipmentUpgrades");Call(upgrade,null,"Initialize");
        var clock=upgrade.GetField("Clock",Hidden);var roll=upgrade.GetField("Roll",Hidden);
        object savedClock=clock.GetValue(null),savedRoll=roll.GetValue(null);
        var now=new DateTime(2035,2,1,0,0,0,DateTimeKind.Utc);
        clock.SetValue(null,(Func<DateTime>)(()=>now));roll.SetValue(null,(Func<int,int>)(max=>0));
        try {
            var dragon=p.Inventory.Objects.Single(i=>i.ID==1088000);
            Check(p.Account.State==AccountTable.AccountState.Player && Scalar(@"SELECT count(*) FROM revora_ancient_victories v
JOIN revora_ancient_summons s ON s.id=v.summon JOIN items i ON i.uid=ANY(v.claimed_items)
WHERE i.uid=@uid AND i.entityid=s.uid AND v.claimed_at IS NOT NULL AND v.item_type=1088000",dragon.UID)==1,
                "ordinary upgrade player owns the exact DragonBall claimed from actual Ancient boss combat");
            ancientNow=ancientNow.AddDays(1);EarnNextOffering(p);SealAt(p);
            AncientDialog(p,300017,0);AncientDialog(p,300017,1);
            var registry=AncientBossWorld.GetField("Hunts",Hidden).GetValue(null);
            var hunt=registry.GetType().GetProperty("Item").GetValue(registry,new object[]{p.Entity.MapID});
            var actors=BossActors(hunt);
            AncientBattles.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>20));
            foreach(var actor in actors){AdventureWalk(p,actor.X,(ushort)(actor.Y-1));AncientHit(p.Entity,actor,actor.Hitpoints);}
            Check(!Fenced(p) && (bool)AbyssField(hunt,"Won"),"nine newly earned and traded amulets open another actual five-monster victory");
            Call(AncientBossWorld,null,"Tick");SealAt(p);
            AncientDialog(p,300017,0);AncientDialog(p,300017,3);AncientDialog(p,300017,3);
            var meteors=p.Inventory.Objects.Where(i=>i.ID==1088001).OrderBy(i=>i.UID).ToArray();
            Check(meteors.Length==6 && Scalar(@"SELECT count(*) FROM items i JOIN revora_ancient_victories v ON i.uid=ANY(v.claimed_items)
JOIN revora_ancient_summons s ON s.id=v.summon WHERE i.entityid=@uid AND s.uid=@uid AND i.id=1088001 AND v.item_type=1088001",p.Entity.UID)==6,
                "real seal dialogue delivers exactly six combat-earned Meteor UIDs and rejects repeated claims");
            ShopFile.Load();Call(BookShop,null,"Initialize");MiningNpc(town,5);
            p.BindWorldMap(1002);p.Entity.X=town.Npcs[5].X;p.Entity.Y=town.Npcs[5].Y;
            ulong gold=p.Entity.Money;uint price=ConquerItemInformation.BaseInformations[410005].GoldWorth;
            ItemUsage.Process(BuyBook(410005,3,5).ToArray(),p);
            var weapons=p.Inventory.Objects.Where(i=>i.ID==410005).OrderBy(i=>i.UID).ToArray();
            Check(!Fenced(p) && weapons.Length==3 && price>0 && p.Entity.Money==gold-3UL*price
                && weapons.All(i=>i.Durability==i.MaximDurability && i.SocketProgress==0 && i.SocketOne==0 && i.SocketTwo==0),
                "ordinary silver shop buys three fully repaired weapons at their loaded catalog price without seeded item metadata");
            uint next=new ConquerItemInformation(410005,0).CalculateUplevel();
            ItemUsage.Process(new Revora.Network.GamePackets.ItemUsage(true){UID=weapons[0].UID,dwParam=meteors[0].UID,Action=Revora.Network.GamePackets.ItemUsage.Mode.MeteorUpgrade}.ToArray(),p);
            now=now.AddSeconds(2);roll.SetValue(null,(Func<int,int>)(max=>max-1));
            ItemUsage.Process(new Revora.Network.GamePackets.ItemUsage(true){UID=weapons[1].UID,dwParam=meteors[1].UID,Action=Revora.Network.GamePackets.ItemUsage.Mode.MeteorUpgrade}.ToArray(),p);
            now=now.AddSeconds(2);roll.SetValue(null,(Func<int,int>)(max=>0));
            ItemUsage.Process(new Revora.Network.GamePackets.ItemUsage(true){UID=weapons[2].UID,dwParam=dragon.UID,Action=Revora.Network.GamePackets.ItemUsage.Mode.DragonBallUpgrade}.ToArray(),p);
            Check(!Fenced(p) && weapons[0].ID==next && weapons[1].ID==410005 && weapons[1].Durability==weapons[1].MaximDurability/2
                && weapons[2].ID==410006 && weapons[2].SocketProgress==10,
                "combat-earned materials produce one Meteor success, one paid Meteor failure and one DragonBall quality success through packet dispatch");
            Check(Scalar(@"SELECT count(*) FROM revora_equipment_upgrade_materials m JOIN revora_equipment_upgrades u ON u.id=m.receipt
JOIN revora_ancient_victories v ON m.item_uid=ANY(v.claimed_items) JOIN revora_ancient_summons s ON s.id=v.summon
WHERE u.uid=@uid AND s.uid=@uid AND v.claimed_at IS NOT NULL AND v.item_type=m.item_type",p.Entity.UID)==3,
                "all three consumed upgrade materials link to saved combat victory claims by their original UIDs");
            uint[] weaponUids=weapons.Select(i=>i.UID).ToArray();uint[] spent={meteors[0].UID,meteors[1].UID,dragon.UID};
            p=ReloadBall(p);p.Entity.MyAchievement.Send();
            Check(new[]{10420,10421,10423}.All(id=>HasAchievement(p,id)) && new[]{10422,10424,10425,10426}.All(id=>!HasAchievement(p,id))
                && p.Entity.Money==gold-3UL*price && spent.All(id=>!p.Inventory.ContainsUID(id))
                && ConquerItemTable.LoadItem(weaponUids[0]).ID==next && ConquerItemTable.LoadItem(weaponUids[2]).ID==410006
                && p.Inventory.Objects.Single(i=>i.UID==weaponUids[1]).ID==410005
                && p.Inventory.Objects.Single(i=>i.UID==weaponUids[1]).Durability==p.Inventory.Objects.Single(i=>i.UID==weaponUids[1]).MaximDurability/2
                && p.Inventory.Objects.Count(i=>i.ID==1088001)==4,
                "full login retains three earned upgrade achievements, changed weapons, exact shop debit and consumed combat rewards without unearned socket credit");
            System.Console.WriteLine("Earned upgrade weapon cost: "+(3UL*price)+" silver; boss reward materials consumed: 2 Meteors, 1 DragonBall.");
        }finally{clock.SetValue(null,savedClock);roll.SetValue(null,savedRoll);}
    }

}
