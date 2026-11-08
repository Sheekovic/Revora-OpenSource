using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type SnowEncounter=typeof(Entity).Assembly.GetType("Revora.Game.Features.SnowBansheeEncounter");
    static readonly Type SnowCombat=typeof(Entity).Assembly.GetType("Revora.Game.Features.SnowBansheeCombat");
    static DateTime snowNow;
    static object SnowActor(){return SnowEncounter.GetField("Active",Hidden).GetValue(null);}
    static Entity SnowBody(){return (Entity)Field(SnowActor(),"Body");}
    static GameState SnowHunter(Map town) {
        var p=HousePlayer(town);PriceStand(p);SoulNpc(p,7);SoulNpc(p,8);
        Check(p.Inventory.Objects.Any(i=>i.ID>=729611 && i.ID<=729703),"ordinary Daily Envoy supplies the hunter's spirit bead");
        p.BindWorldMap(1927);p.Entity.X=373;p.Entity.Y=365;EntityTable.SaveEntity(p);return p;
    }
    static void SnowNext(){snowNow=snowNow.AddMinutes(31);Call(SnowEncounter,null,"Tick");Check(SnowActor()!=null,"next public cycle reconstructs one due Banshee");}
    static void VerifySnowBanshee(string runtime,Map town) {
        InitializeHousingFixture(runtime,town);var map=World(runtime,1927);MiningNpc(town,79882);Server.GamePool=new GameState[0];
        snowNow=new DateTime(2039,1,4,20,0,0,DateTimeKind.Utc);
        SnowStore.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>snowNow));SoulBall.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>snowNow));
        Call(SnowEncounter,null,"Initialize");Call(SnowEncounter,null,"Initialize");
        try {
            var a=SnowActor();var body=SnowBody();var id=(Guid)Field(Field(a,"Run"),"Id");
            Check(body.Hitpoints==100000000 && body.MaxHitpoints==100000000 && body.Boss==1 && body.MonsterInfo.Boss && body.Body==951 && SnowCount("state='active'")==1,
                "repeated initialization owns one full-health public Banshee with boss HUD metadata");
            Check(!body.Move(Enums.ConquerAngle.East,1),"legacy movement cannot move the encounter-owned stationary boss");
            var p=SnowHunter(town);uint wardrobe=StoredQuestGarment(p);p.Attackable=true;
            Check(SwordHit(p,body,1000)==1000 && body.Hitpoints==99999000,"ordinary nonlethal combat damages the registered public boss");
            snowNow=snowNow.AddSeconds(5);Call(SnowEncounter,null,"Tick");lock(SnowGate)Call(SnowEncounter,null,"Release",a);
            snowNow=snowNow.AddSeconds(5);Call(SnowEncounter,null,"Tick");var recovered=SnowBody();
            Check(!ReferenceEquals(recovered,body) && recovered.Hitpoints==99999000 && (Guid)Field(Field(SnowActor(),"Run"),"Id")==id,"actor reconstruction restores checkpoint health and encounter identity");
            body.Die(p.Entity);Check(SnowCount("state='victory'")==0 && SoulSpirits(p)==0,"retired actor callback cannot manufacture a victory or spirit credit");body=recovered;
            p.Entity.X=65535;p.Entity.Y=65535;uint hit=500;
            Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,body,new Attack(true){AttackType=Attack.Melee},ref hit,null);
            Check(hit==0 && body.Hitpoints==99999000,"out-of-range overflow coordinates cannot hit the Banshee");
            VerifySnowCombat(p,body);
            p.Entity.X=body.X;p.Entity.Y=body.Y;p.Attackable=false;
            var lootRoll=SnowRewards.GetField("Roll",Hidden);object savedLootRoll=lootRoll.GetValue(null);
            lootRoll.SetValue(null,new Func<int,int>(max=>0));
            try{Parallel.For(0,8,n=>SwordHit(p,body,100000000));}finally{lootRoll.SetValue(null,savedLootRoll);}
            Check(!Fenced(p) && body.Dead && SnowActor()==null && SnowCount("state='victory'")==1 && SoulSpirits(p)==1000 && SoulBossCredits(p)==1 && p.Entity.SubClasses.StudyPoints==100,
                "concurrent lethal combat commits one public victory, 100 Study Points and 1000 owned spirits");
            body.Die(p.Entity);Check(SoulBossCredits(p)==1 && SnowCount("state='victory'")==1,"defeated public boss cannot replay its rewards");
            p.Entity.MyAchievement.Send();
            Check(HasAchievement(p,10527)
                && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10527 AND source='snow-banshee-kill-v1'",p.Entity.UID)==1,
                "public Banshee victory and replay grant the actual killer one kill achievement receipt");
            Check(!HasAchievement(p,10523) && SnowDrops(map).Count(d=>d.ItemID==1088000)==2,
                "actual monster victory creates DragonBall loot but cannot grant its pickup achievement early");
            foreach(var drop in SnowDrops(map))Loot(p,drop);
            Check(HasAchievement(p,10523) && p.Inventory.Objects.Count(i=>i.ID==1088000)==2
                && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10523",p.Entity.UID)==1,
                "ordinary combat-earned DragonBall pickups grant one historical achievement");
            Check(Scalar("SELECT count(*) FROM items WHERE uid=@uid AND position=0 AND warehouse=0 AND inwardrobe=1",wardrobe)==1,"boss admission, victory and reward pickup preserve the stored wardrobe garment");
            p=ReloadBall(p);
            Check(!Fenced(p) && SoulSpirits(p)==1000 && p.Entity.SubClasses.StudyPoints==100
                && p.Inventory.Objects.Any(i=>i.Bound && i.ID>=800000 && i.ID<900000 && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7),
                "ordinary production reload retains earned Banshee loot, Study Points and spirit credit");
            p.Entity.MyAchievement.Send();Check(HasAchievement(p,10527),"Banshee kill achievement survives production login");
            Check(HasAchievement(p,10523) && p.Inventory.Objects.Count(i=>i.ID==1088000)==2,
                "full production reload preserves combat-earned DragonBalls and their pickup achievement");
            Check(HasAchievement(p,10525),"direct unarmed level-138 boss victory survives full production reload");
            Call(SnowEncounter,null,"Initialize");Call(SnowEncounter,null,"Tick");Check(SnowActor()==null,"completed public boss stays absent during the persisted cooldown");
            foreach(string table in new[]{"revora_snow_banshee_loot","revora_soul_ball_runs","revora_soul_ball_bosses","revora_achievement_evidence"}) {
                SnowNext();var f=SnowHunter(town);var boss=SnowBody();
                Query("CREATE FUNCTION snow_victory_fail() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER snow_victory_fail BEFORE "+(table=="revora_soul_ball_runs"?"UPDATE":"INSERT")+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION snow_victory_fail()");
                try{SwordHit(f,boss,100000000);}finally{Query("DROP TRIGGER snow_victory_fail ON "+table+";DROP FUNCTION snow_victory_fail()");}
                Check(Fenced(f) && !boss.Dead && SnowCount("state='active'")==1 && SoulSpirits(f)==0 && SoulBossCredits(f)==0
                    && Scalar("SELECT count(*) FROM revora_snow_banshee_loot WHERE owner=@uid",f.Entity.UID)==0,"failed victory participant rolls back every reward and leaves the public boss alive: "+table);
                Check(Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10527",f.Entity.UID)==0,"failed Banshee victory leaves no kill achievement: "+table);
                f=ReloadBall(f);SwordHit(f,boss,100000000);Check(!Fenced(f) && boss.Dead && SoulSpirits(f)==1000,"ordinary reconnect can finish the surviving boss after storage recovery: "+table);
                f.Entity.MyAchievement.Send();Check(HasAchievement(f,10527),"recovered public boss victory grants its achievement: "+table);
            }
            SnowNext();var leader=SnowHunter(town);var mate=SnowHunter(town);TeratoTeam(leader,mate);body=SnowBody();
            leader.Entity.X=(ushort)(body.X+1);leader.Entity.Y=body.Y;mate.Entity.X=body.X;mate.Entity.Y=body.Y;
            SwordHit(mate,body,100000000);
            Check(!Fenced(leader) && !Fenced(mate) && leader.Entity.SubClasses.StudyPoints==100 && mate.Entity.SubClasses.StudyPoints==100
                && SoulSpirits(leader)==0 && SoulSpirits(mate)==1000,"nearby ordinary killer team shares Study Points while boss spirits belong to the killer");
            leader.Entity.MyAchievement.Send();mate.Entity.MyAchievement.Send();
            Check(!HasAchievement(leader,10527) && HasAchievement(mate,10527),"Banshee kill achievement belongs to the killer rather than the nearby team leader");
            var owner=SnowHunter(town);owner.Entity.X=370;owner.Entity.Y=365;SnowNext();body=SnowBody();
            Check(Math.Abs((int)body.X-370)>1 || Math.Abs((int)body.Y-365)>1,"public respawn selects a nearby clear tile instead of overlapping a waiting player");
            owner.Entity.X=body.X;owner.Entity.Y=body.Y;
            var pet=new Entity(EntityFlag.Monster,true){UID=owner.Entity.UID-200000,Owner=owner,MonsterInfo=new MonsterInformation(),MapID=1927,X=body.X,Y=body.Y,Hitpoints=1000};owner.Companion=pet;
            body.Die(pet);Check(!body.Dead && SoulSpirits(owner)==0,"unregistered companion cannot claim public boss rewards");
            map.Companions.Add(pet.UID,pet);owner.Entity.X=(ushort)(body.X+30);body.Die(pet);
            Check(!body.Dead && SoulSpirits(owner)==0,"distant owner cannot use a companion to bypass reward range");
            owner.Entity.X=body.X;body.Die(pet);map.Companions.Remove(pet.UID);owner.Companion=null;
            Check(!Fenced(owner) && body.Dead && SoulSpirits(owner)==1000,"registered companion victory and spirits belong to its nearby ordinary owner");
            owner.Entity.MyAchievement.Send();Check(HasAchievement(owner,10527),"registered companion credits its owner with the Banshee kill achievement");
            Check(!HasAchievement(owner,10525),"a registered companion's kill cannot earn the owner's unarmed achievement");
            foreach(string mode in new[]{"right","left","saved","alternate","inactive","level119","level120","evidence"}) {
                SnowNext();var fighter=SnowHunter(town);var victim=SnowBody();fighter.Entity.X=victim.X;fighter.Entity.Y=victim.Y;fighter.Attackable=false;
                if(mode=="right" || mode=="left")RebirthGear(fighter,mode=="right"?(byte)4:(byte)5);
                if(mode=="alternate" || mode=="inactive"){fighter.AlternateEquipment=true;RebirthGear(fighter,mode=="alternate"?(byte)24:(byte)4);}
                if(mode=="saved"){var hidden=Pack(fighter,410339,1);Query("UPDATE items SET position=4 WHERE uid=@uid","uid",(long)hidden.UID);}
                if(mode=="level119")victim.MonsterInfo.Level=119;if(mode=="level120")victim.MonsterInfo.Level=120;
                if(mode=="evidence")Query("CREATE FUNCTION suppress_unarmed() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF NEW.achievement_id=10525 THEN RETURN NULL; END IF; RETURN NEW; END';CREATE TRIGGER suppress_unarmed BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW EXECUTE FUNCTION suppress_unarmed()");
                try{SwordHit(fighter,victim,100000000);}finally{if(mode=="evidence")Query("DROP TRIGGER suppress_unarmed ON revora_achievement_evidence;DROP FUNCTION suppress_unarmed()");}
                if(mode=="evidence") {
                    Check(Fenced(fighter) && !victim.Dead && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id IN(10525,10527)",fighter.Entity.UID)==0
                        && Scalar("SELECT count(*) FROM revora_snow_banshee_loot WHERE owner=@uid",fighter.Entity.UID)==0,
                        "failed unarmed evidence rolls back boss victory, loot and both achievements");
                    fighter=ReloadBall(fighter);SwordHit(fighter,victim,100000000);
                }
                bool expected=mode=="inactive" || mode=="level120" || mode=="evidence";
                fighter.Entity.MyAchievement.Send();
                Check(!Fenced(fighter) && victim.Dead && HasAchievement(fighter,10525)==expected,
                    "owned combat checks active hands, saved state and level before unarmed credit: "+mode);
                fighter=ReloadBall(fighter);fighter.Entity.MyAchievement.Send();
                Check(HasAchievement(fighter,10525)==expected,"unarmed eligibility result survives full reload: "+mode);
            }

            VerifySnowEarnedSockets(town);
            SnowNext();body=SnowBody();SwordHit(owner,body,2000);
            Query("CREATE FUNCTION snow_checkpoint_fail() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER snow_checkpoint_fail BEFORE UPDATE ON revora_snow_banshee_runs FOR EACH ROW EXECUTE FUNCTION snow_checkpoint_fail()");
            snowNow=snowNow.AddSeconds(6);try{Call(SnowEncounter,null,"Tick");}finally{Query("DROP TRIGGER snow_checkpoint_fail ON revora_snow_banshee_runs;DROP FUNCTION snow_checkpoint_fail()");}
            Check(SnowActor()==null && SnowCount("state='active'")==1,"failed health checkpoint retires the unsafe actor without marking a victory");
            snowNow=snowNow.AddSeconds(6);Call(SnowEncounter,null,"Tick");
            Check(SnowActor()!=null && SnowBody().Hitpoints==100000000,"checkpoint recovery restores the last durable health instead of inventing an unsaved value");
        }finally {
            lock(SnowGate)if(SnowActor()!=null)Call(SnowEncounter,null,"Release",SnowActor());Call(SnowRewards,null,"ClearGround");
            SnowStore.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>DateTime.UtcNow));SoulBall.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>DateTime.UtcNow));
        }
    }
    static void VerifySnowEarnedSockets(Map town) {
        var service=typeof(Entity).Assembly.GetType("Revora.Game.Features.EquipmentUpgrades");Call(service,null,"Initialize");
        var sockets=typeof(Entity).Assembly.GetType("Revora.Game.Features.EquipmentSockets");Call(sockets,null,"Initialize");
        var socketClock=sockets.GetField("Clock",Hidden);object savedSocketClock=socketClock.GetValue(null);
        var upgradeClock=service.GetField("Clock",Hidden);var upgradeRoll=service.GetField("Roll",Hidden);var lootRoll=SnowRewards.GetField("Roll",Hidden);
        object savedClock=upgradeClock.GetValue(null),savedUpgrade=upgradeRoll.GetValue(null),savedLoot=lootRoll.GetValue(null);
        DateTime now=new DateTime(2040,1,1,0,0,0,DateTimeKind.Utc);
        socketClock.SetValue(null,(Func<DateTime>)(()=>now));
        upgradeClock.SetValue(null,(Func<DateTime>)(()=>now));upgradeRoll.SetValue(null,(Func<int,int>)(max=>max-1));
        lootRoll.SetValue(null,(Func<int,int>)(max=>0));
        try {
            var p=SnowPlayer(town);p.Attackable=false;p.BindWorldMap(1002);ShopFile.Load();Call(BookShop,null,"Initialize");MiningNpc(town,5);
            p.Entity.X=town.Npcs[5].X;p.Entity.Y=town.Npcs[5].Y;ulong initialSilver=p.Entity.Money;
            uint price=ConquerItemInformation.BaseInformations[410005].GoldWorth;
            ItemUsage.Process(BuyBook(410005,1,5).ToArray(),p);var weapon=p.Inventory.Objects.Single(i=>i.ID==410005);uint weaponUid=weapon.UID;
            p.Entity.MyAchievement.Send();
            Check(p.Account.State==AccountTable.AccountState.Player && weapon.SocketProgress==0 && weapon.SocketOne==0 && weapon.SocketTwo==0
                && !HasAchievement(p,10424) && !HasAchievement(p,10425) && !HasAchievement(p,10426) && p.Entity.Money==initialSilver-price,
                "ordinary socket student buys a normal zero-progress weapon with no upgrade/socket evidence");
            ushort maximum=weapon.MaximDurability;ulong repairPrice=(ulong)Math.Max(1m,decimal.Ceiling(decimal.Floor((decimal)price*(maximum-maximum/2)/maximum)*0.75m));
            int attempts=0;var consumed=new System.Collections.Generic.List<uint>();
            for(int encounter=0;encounter<20;encounter++) {
                p.BindWorldMap(1927);SnowNext();var boss=SnowBody();p.Entity.X=boss.X;p.Entity.Y=boss.Y;SwordHit(p,boss,boss.Hitpoints);
                Check(!Fenced(p) && boss.Dead && SnowActor()==null,"socket student's actual public victory commits before material collection "+(encounter+1));
                var drops=SnowDrops(p.Map).Where(d=>d.ItemID==1088000 && d.OwnerUID==p.Entity.UID).ToArray();
                Check(drops.Length==2,"controlled boss reward draw produces two registered DragonBall entitlements "+(encounter+1));
                foreach(var drop in drops)Loot(p,drop);
                var balls=p.Inventory.Objects.Where(i=>i.ID==1088000).OrderBy(i=>i.UID).ToArray();
                Check(balls.Length==2 && balls.All(i=>Scalar("SELECT count(*) FROM revora_snow_banshee_loot WHERE item_uid=@uid AND collector=owner",i.UID)==1),
                    "both DragonBalls are ordinary pickups with saved monster-origin identities "+(encounter+1));
                foreach(var ball in balls) {
                    now=now.AddSeconds(2);ItemUsage.Process(new ItemUsage(true){UID=weaponUid,dwParam=ball.UID,Action=ItemUsage.Mode.DragonBallUpgrade}.ToArray(),p);
                    attempts++;consumed.Add(ball.UID);
                    uint progress=attempts<10?(uint)attempts*10:attempts<40?(uint)(attempts-10)*10:0;
                    Check(!Fenced(p) && weapon.ID==410005 && weapon.Durability==maximum/2 && !p.Inventory.ContainsUID(ball.UID)
                        && weapon.SocketProgress==progress && (weapon.SocketOne==Enums.Gem.EmptySocket)==(attempts>=10)
                        && (weapon.SocketTwo==Enums.Gem.EmptySocket)==(attempts>=40),
                        "paid quality failure advances socket progress only by its natural ten points: attempt "+attempts);
                    ItemUsage.Process(new ItemUsage(true){UID=weaponUid,Action=ItemUsage.Mode.Repair}.ToArray(),p);
                    Check(!Fenced(p) && weapon.Durability==maximum && p.Entity.Money==initialSilver-price-(ulong)attempts*repairPrice,
                        "ordinary paid repair restores the next attempt with exact cumulative silver cost: "+attempts);
                    if(attempts==9 || attempts==10 || attempts==39 || attempts==40) {
                        p.Entity.MyAchievement.Send();
                        Check(HasAchievement(p,10424) && HasAchievement(p,10425)==(attempts>=10) && HasAchievement(p,10426)==(attempts>=40),
                            "socket achievements match exact paid-attempt boundaries without seeded progress: "+attempts);
                    }
                    if(attempts==10 || attempts==29) {
                        p=ReloadBall(p);weapon=p.Inventory.Objects.Single(i=>i.UID==weaponUid);p.Attackable=false;p.Entity.MyAchievement.Send();
                        Check(weapon.SocketProgress==progress && weapon.SocketOne==Enums.Gem.EmptySocket && weapon.SocketTwo==0
                            && p.Entity.Money==initialSilver-price-(ulong)attempts*repairPrice && HasAchievement(p,10425) && !HasAchievement(p,10426),
                            "full login retains earned first socket and partial second-socket progress at attempt "+attempts);
                    }
                }
            }
            Check(Scalar(@"SELECT count(*) FROM revora_equipment_upgrade_materials m JOIN revora_equipment_upgrades u ON u.id=m.receipt
JOIN revora_snow_banshee_loot l ON l.item_uid=m.item_uid JOIN revora_snow_banshee_runs r ON r.id=l.run
WHERE u.uid=@uid AND l.owner=@uid AND l.collector=@uid AND r.killer=@uid AND r.state='victory' AND m.item_type=1088000",p.Entity.UID)==40
                && Scalar("SELECT count(*) FROM revora_equipment_repairs WHERE uid=@uid",p.Entity.UID)==40,
                "all forty paid attempts trace to actual boss kills and pickups, with forty saved repair receipts");
            SnowNext();var levelBoss=SnowBody();p.Entity.X=levelBoss.X;p.Entity.Y=levelBoss.Y;SwordHit(p,levelBoss,levelBoss.Hitpoints);
            foreach(var drop in SnowDrops(p.Map).Where(d=>d.ItemID==1088000 && d.OwnerUID==p.Entity.UID))Loot(p,drop);
            var levelBall=p.Inventory.Objects.Where(i=>i.ID==1088000).OrderBy(i=>i.UID).First();uint levelMaterial=levelBall.UID;
            uint nextLevel=new ConquerItemInformation(weapon.ID,0).CalculateUplevel();now=now.AddSeconds(2);
            Revora.Network.PacketHandler.HandlePacket(UpgradeComposeFrame(weaponUid,new[]{levelMaterial},6,1),p);
            p.Entity.MyAchievement.Send();consumed.Add(levelMaterial);
            Check(!Fenced(p) && levelBoss.Dead && weapon.ID==nextLevel && !p.Inventory.ContainsUID(levelMaterial) && HasAchievement(p,10422)
                && Scalar(@"SELECT count(*) FROM revora_equipment_upgrade_materials m JOIN revora_equipment_upgrades u ON u.id=m.receipt
JOIN revora_snow_banshee_loot l ON l.item_uid=m.item_uid WHERE u.uid=@uid AND l.collector=@uid AND u.operation=2 AND u.interface=2 AND u.success",p.Entity.UID)==1,
                "full Compose packet dispatcher spends one newly collected boss DragonBall and earns its distinct level-up achievement");
            p=ReloadBall(p);weapon=p.Inventory.Objects.Single(i=>i.UID==weaponUid);p.Entity.MyAchievement.Send();
            Check(weapon.ID==nextLevel && weapon.SocketOne==Enums.Gem.EmptySocket && weapon.SocketTwo==Enums.Gem.EmptySocket && weapon.SocketProgress==0 && weapon.Durability==maximum
                && new[]{10422,10424,10425,10426,10523}.All(id=>HasAchievement(p,id)) && consumed.All(uid=>!p.Inventory.ContainsUID(uid))
                && p.Entity.Money==initialSilver-price-40UL*repairPrice && EntityTable.SaveEntity(p),
                "full final login and character save preserve two earned sockets, consumed monster loot and exact silver balance");
            p.BindWorldMap(1002);p.Entity.X=town.Npcs[5].X;p.Entity.Y=town.Npcs[5].Y;
            ItemUsage.Process(BuyBook(410005,1,5).ToArray(),p);var direct=p.Inventory.Objects.Single(i=>i.ID==410005);
            uint directUid=direct.UID;var firstBall=p.Inventory.Objects.Single(i=>i.ID==1088000);
            var socketMaterials=new System.Collections.Generic.List<uint>{firstBall.UID};
            now=now.AddSeconds(2);Revora.Network.PacketHandler.HandlePacket(DedicatedSocketFrame(directUid,firstBall.UID),p);
            Check(!Fenced(p) && direct.SocketOne==Enums.Gem.EmptySocket && direct.SocketTwo==0 && !p.Inventory.ContainsUID(firstBall.UID),
                "one actual boss-looted DragonBall buys the first socket on a second ordinary purchased weapon");
            for(int cycle=0;cycle<3;cycle++) {
                p.Attackable=false;p.BindWorldMap(1927);SnowNext();var boss=SnowBody();p.Entity.X=boss.X;p.Entity.Y=boss.Y;SwordHit(p,boss,boss.Hitpoints);
                foreach(var drop in SnowDrops(p.Map).Where(d=>d.ItemID==1088000 && d.OwnerUID==p.Entity.UID).ToArray())Loot(p,drop);
            }
            var five=p.Inventory.Objects.Where(i=>i.ID==1088000).OrderBy(i=>i.UID).Take(5).Select(i=>i.UID).ToArray();socketMaterials.AddRange(five);
            now=now.AddSeconds(2);Revora.Network.PacketHandler.HandlePacket(DedicatedSocketFrame(directUid,five),p);
            Check(!Fenced(p) && direct.SocketTwo==Enums.Gem.EmptySocket && socketMaterials.All(uid=>!p.Inventory.ContainsUID(uid))
                && Scalar(@"SELECT count(*) FROM revora_equipment_sockets s CROSS JOIN LATERAL unnest(s.materials) AS m(item_uid)
JOIN revora_snow_banshee_loot l ON l.item_uid=m.item_uid WHERE s.uid=@uid AND l.collector=@uid AND s.success",p.Entity.UID)==6,
                "six exact monster-earned DragonBalls buy both guaranteed weapon sockets through the normal packet dispatcher");
            p=ReloadBall(p);direct=p.Inventory.Objects.Single(i=>i.UID==directUid);p.Entity.MyAchievement.Send();
            Check(!Fenced(p) && direct.SocketOne==Enums.Gem.EmptySocket && direct.SocketTwo==Enums.Gem.EmptySocket
                && socketMaterials.All(uid=>!p.Inventory.ContainsUID(uid)) && p.Inventory.Objects.Count(i=>i.ID==1088000)==1
                && p.Entity.Money==initialSilver-2UL*price-40UL*repairPrice && HasAchievement(p,10425) && HasAchievement(p,10426),
                "production login preserves the six-DragonBall socket route, exact remaining loot and silver");
            System.Console.WriteLine("Socket routes: controlled forty-failure upgrade boundaries plus guaranteed six-DragonBall dedicated service; native UI pending.");
        }finally{socketClock.SetValue(null,savedSocketClock);upgradeClock.SetValue(null,savedClock);upgradeRoll.SetValue(null,savedUpgrade);lootRoll.SetValue(null,savedLoot);}
    }
    static void VerifySnowCombat(GameState p,Entity body) {
        p.Entity.MaxHitpoints=p.Entity.Hitpoints=500000;body.Hitpoints-=10000000;
        // Recovery has just prepared IceBolt; clear its telegraph to start a deterministic cycle.
        lock(SnowGate)Call(SnowCombat,null,"Clear",SnowActor());
        for(int phase=0;phase<7;phase++) {
            p.Entity.X=(ushort)(body.X+1);p.Entity.Y=body.Y;uint hp=p.Entity.Hitpoints,before=body.Hitpoints;
            Call(SnowCombat,null,"Execute",body,p.Entity);Check(p.Entity.Hitpoints==hp && body.Hitpoints==before,"Banshee skill warns before effect: "+phase);
            snowNow=snowNow.AddSeconds(2);Call(SnowCombat,null,"Tick",SnowActor());
            Check(!Fenced(p) && (phase==6?body.Hitpoints==before+2000000:p.Entity.Hitpoints<hp),"Banshee skill applies owned damage or bounded healing: "+phase);
            if(phase==2)for(int pulse=1;pulse<6;pulse++){snowNow=snowNow.AddSeconds(1);Call(SnowCombat,null,"Tick",SnowActor());}
            if(phase==3 || phase==4) {
                Check((bool)Call(SnowCombat,null,"IsStunned",p) && !p.Entity.Move(Enums.ConquerAngle.East,1),"Banshee freeze blocks actual player movement: "+phase);
                ushort oldX=p.Entity.X,oldY=p.Entity.Y;
                GeneralData.PlayerJump(new Data(true){dwParam=(uint)((oldX+2)|(oldY<<16))},p);
                Check(p.Entity.X==oldX && p.Entity.Y==oldY,"native jump handler also respects the Banshee freeze: "+phase);
                uint damage=100;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,body,new Attack(true){AttackType=Attack.Melee},ref damage,null);
                Check(damage==0,"frozen player cannot attack through direct damage dispatch: "+phase);
                snowNow=snowNow.AddSeconds(phase==3?5:3);Check(!(bool)Call(SnowCombat,null,"IsStunned",p),"owned freeze expires at its exact five/three-second boundary: "+phase);
            }
            snowNow=snowNow.AddSeconds(4);
        }
        Check((bool)Call(SnowCombat,null,"Contains",2,10,0,Math.PI/9) && !(bool)Call(SnowCombat,null,"Contains",2,10,0,Math.PI/3)
            && !(bool)Call(SnowCombat,null,"Contains",4,5,0,0.0),"rotating hail cone and IceShock escape boundary follow the reviewed server geometry");
    }
}
