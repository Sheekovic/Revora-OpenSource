using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Team=Revora.Game.ConquerStructures.Team;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type Terato=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeratoEncounter");
    static readonly Type TeratoLoot=typeof(Entity).Assembly.GetType("Revora.Game.Features.TeratoRewards");
    static DateTime teratoNow;
    static object[] TeratoActors(){return ((IDictionary)Terato.GetField("Actors",Hidden).GetValue(null)).Values.Cast<object>().ToArray();}
    static Entity TeratoBoss(GameState p){return (Entity)DragonField(TeratoActors().Single(a=>ReferenceEquals(DragonField(a,"Owner"),p)),"Body");}
    static void ClearTerato(){foreach(var a in TeratoActors())Call(Terato,null,"Release",a);Call(TeratoLoot,null,"ClearGround");}
    static long TeratoRuns(GameState p,string state="true"){return Scalar("SELECT count(*) FROM revora_terato_runs WHERE uid=@uid AND "+state,p.Entity.UID);}
    static long TeratoStudy(GameState p,string state="true"){return Scalar("SELECT count(*) FROM revora_terato_study WHERE uid=@uid AND "+state,p.Entity.UID);}
    static GameState TeratoPlayer(Map map){var p=DragonHunter(map);p.Entity.MyAchievement=new Achievement(p.Entity);p.Entity.MyAchievement.Load("");TeratoSite(p);EntityTable.SaveEntity(p);return p;}
    static void TeratoSite(GameState p){p.Entity.X=p.Map.ID==2056?(ushort)535:(ushort)520;p.Entity.Y=p.Map.ID==2056?(ushort)394:(ushort)742;}
    static void TeratoNpc(GameState p,byte option){TeratoSite(p);ExplorationNpc(p,p.Map.ID==2056?900000127u:180810u,option);}
    static Team TeratoTeam(params GameState[] players){var t=(Team)FormatterServices.GetUninitializedObject(typeof(Team));t.Lider=players[0];t.Members=new ConcurrentDictionary<uint,Team.Member>();
        foreach(var p in players){p.Team=t;t.Members[p.Entity.UID]=new Team.Member{entry=p};}return t;}
    static FloorItem[] TeratoDrops(Map map){return map.FloorItems.Values.Where(d=>(bool)Call(TeratoLoot,null,"Managed",d)).ToArray();}
    static void SlayTerato(GameState p,Entity boss){p.Entity.X=boss.X;p.Entity.Y=boss.Y;boss.Die(p.Entity);}
    static void VerifyTerato(string runtime,GameState earned) {
        teratoNow=DateTime.UtcNow;Terato.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>teratoNow));TeratoLoot.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(n=>n==4?0:1));
        Call(Terato,null,"Initialize");Call(Terato,null,"Initialize");var map=Kernel.Maps[2056];var town=Kernel.Maps[1002];
        Check(map.Npcs[900000127].X==532 && town.Npcs[180810].X==517 && town.Npcs[180810].Y==742,"both installed Dragon Pill locations have ordinary summon/recovery NPCs");
        earned.Entity.MyAchievement=new Achievement(earned.Entity);earned.Entity.MyAchievement.Load("");
        TeratoSite(earned);var pill=earned.Inventory.Objects.Single(i=>i.ID==720598);var partner=TeratoPlayer(map);var far=TeratoPlayer(map);var team=TeratoTeam(earned,partner,far);
        StoredQuestGarment(partner);
        ItemHandler.UseItem(pill,earned);var boss=TeratoBoss(earned);
        // Retain the legacy contract for this earned P1 artifact-application journey.
        Query("UPDATE revora_terato_runs SET valuable_reward=false WHERE uid=@uid AND state='active'","uid",(long)earned.Entity.UID);
        Check(!Fenced(earned) && !earned.Inventory.ContainsUID(pill.UID) && TeratoRuns(earned,"state='active'")==1 && boss.Name=="TeratoDragon"
            && boss.Hitpoints==50000000 && boss.MaxHitpoints==50000000 && boss.Boss==1 && boss.MonsterInfo.Boss,"the fully earned Dragon Pill summons a native-named 50-million-HP boss and pays once");
        BossHealthPackets(boss);
        ItemHandler.UseItem(pill,earned);TeratoNpc(earned,3);Check(TeratoActors().Length==1 && TeratoRuns(earned)==1,"spent pill and resume replay cannot multiply a living paid dragon");
        var outsider=TeratoPlayer(map);StoredQuestGarment(outsider);outsider.Entity.X=boss.X;outsider.Entity.Y=boss.Y;boss.Die(outsider.Entity);
        Check(!boss.Dead && TeratoRuns(earned,"state='victory'")==0 && !Revora.Game.Attacking.Handle.CanAttack(outsider.Entity,boss,null,true),"bystanders cannot kill or steal another party's paid dragon");
        Check(Revora.Game.Attacking.Handle.CanAttack(partner.Entity,boss,null,true),"an admitted canonical teammate may fight the dragon");
        partner.Team=null;boss.Die(partner.Entity);Check(!boss.Dead && !Revora.Game.Attacking.Handle.CanAttack(partner.Entity,boss,null,true),"a teammate who leaves the summoner's party loses encounter access");partner.Team=team;
        far.Entity.X=1;far.Entity.Y=1;earned.Entity.X=boss.X;earned.Entity.Y=boss.Y;partner.Entity.X=boss.X;partner.Entity.Y=boss.Y;
        earned.Entity.Hitpoints=0;
        earned.Entity.MyAchievement.Send();bool summonerUnarmedBefore=HasAchievement(earned,10525);
        uint hit=boss.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(partner.Entity,boss,new Attack(true){AttackType=Attack.Melee,Attacked=boss.UID,Attacker=partner.Entity.UID},ref hit,null);
        Check(boss.Dead && !Fenced(partner) && TeratoRuns(earned,"state='victory'")==1 && TeratoActors().Length==0,"production lethal attack records one victory and removes the paid encounter");
        foreach(var player in new[]{partner,earned,far,outsider})player.Entity.MyAchievement.Send();
        Check(HasAchievement(partner,10525) && HasAchievement(earned,10525)==summonerUnarmedBefore && HasAchievement(partner,10526) && !HasAchievement(earned,10526) && !HasAchievement(far,10526) && !HasAchievement(outsider,10526),
            "committed Terato kill awards the catalog achievement to the actual canonical killer only");
        Check(EpkCount("SELECT count(*) FROM revora_achievement_evidence e JOIN revora_terato_runs r ON e.source_key=replace(r.id::text,'-','') WHERE e.uid=@uid AND e.achievement_id=10526 AND e.source='terato-kill-v1' AND r.killer=e.uid AND r.state='victory'","uid",(long)partner.Entity.UID)==1,
            "Terato kill evidence refers to the committed victory and its saved killer");
        Check(earned.Entity.SubClasses.StudyPoints==50 && partner.Entity.SubClasses.StudyPoints==50 && far.Entity.SubClasses.StudyPoints==0
            && TeratoStudy(earned,"paid_at IS NOT NULL")==1 && TeratoStudy(partner)==1 && TeratoStudy(far)==0,"nearby admitted teammates, including the fallen summoner, each receive fifty Study Points; distant teammates do not");
        Check(!HasAchievement(earned,10932) && !HasAchievement(partner,10932),"the Dragon Slayer achievement requires receiving its Dragon Soul rather than merely killing Terato");
        boss.Die(partner.Entity);Call(TeratoLoot,null,"Tick",true);Check(partner.Entity.SubClasses.StudyPoints==50 && TeratoDrops(map).Length==3,"replayed boss death cannot pay or create the three loot pieces again");
        Check(MonsterInformation.MonsterInformations[4152].Name=="DragonKing" && MonsterInformation.MonsterInformations[4152].Hitpoints==100000000,"Terato cannot overwrite the existing DragonKing template");
        var drops=TeratoDrops(map);var pack=drops.Single(d=>d.ItemID==720399);Loot(outsider,pack);
        Check(!pack.PickedUpAlready && outsider.Inventory.Count==0,"boss loot's original ownership window rejects a stranger");
        partner.Entity.X=pack.X;partner.Entity.Y=pack.Y;Parallel.For(0,8,n=>FloorItem.Process(LootPacket(pack),partner));
        Check(!Fenced(partner) && pack.PickedUpAlready && partner.Inventory.Objects.Count(i=>i.ID==720399)==1,"concurrent native pickup frames collect one Superior Refinery Pack once");
        var stale=drops.Single(d=>d.ItemID==350001);Call(TeratoLoot,null,"ClearGround");Call(TeratoLoot,null,"Tick",true);Loot(partner,stale);
        Check(TeratoDrops(map).Length==2 && !partner.Inventory.Objects.Any(i=>i.ID==350001),"world reconstruction restores only unclaimed loot and rejects stale floor objects");
        teratoNow=teratoNow.AddSeconds(16);var accessory=TeratoDrops(map).Single(d=>d.ItemID==350001);Loot(outsider,accessory);var restored=ConquerItemTable.LoadItem(accessory.Item.UID);
        Check(!Fenced(outsider) && accessory.PickedUpAlready && restored.ID==350001 && restored.Days==7 && !restored.Bound,"restored loot retains its original public pickup time and a seven-day accessory persists");
        Call(typeof(Achievement),outsider.Entity.MyAchievement,"Refresh",DateTime.UtcNow,true);
        Check(!HasAchievement(outsider,10503),"Terato accessory pickup alone does not count as equipping it");
        foreach(uint invalidSlot in new uint[]{0,1,4,5,10,11,19,20,35,36,255,uint.MaxValue}) {
            ItemHandler.EquipItem(new ItemUsage(true){UID=restored.UID,dwParam=invalidSlot,Action=ItemUsage.Mode.EquipItem},outsider);
            Call(typeof(Achievement),outsider.Entity.MyAchievement,"Refresh",DateTime.UtcNow,true);
            Check(!Fenced(outsider) && outsider.Inventory.ContainsUID(restored.UID)
                && outsider.Equipment.TryGetItem(restored.UID)==null && ConquerItemTable.LoadItem(restored.UID).Position==0
                && !HasAchievement(outsider,10503),"invalid accessory equipment slot preserves earned loot in its bag: "+invalidSlot);
        }
        ItemHandler.EquipItem(new ItemUsage(true){UID=restored.UID,dwParam=16,Action=ItemUsage.Mode.EquipItem},outsider);
        Call(typeof(Achievement),outsider.Entity.MyAchievement,"Refresh",DateTime.UtcNow,true);
        Check(!Fenced(outsider) && outsider.Equipment.TryGetItem(restored.UID)!=null
            && ConquerItemTable.LoadItem(restored.UID).Position==16 && HasAchievement(outsider,10503),
            "ordinary public-loot recipient equips the earned Terato accessory in the left slot and earns its equipment achievement");
        outsider=ReloadGold(outsider);ConquerItemTable.LoadItems(outsider);
        outsider.Entity.MyAchievement.Send();
        Check(outsider.Equipment.TryGetItem(restored.UID)!=null && outsider.Equipment.TryGetItem(restored.UID).Position==16 && HasAchievement(outsider,10503)
            && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10503 AND source='equipment-state-v1'",outsider.Entity.UID)==1,
            "production login restores the earned accessory and exactly one equipment achievement receipt");
        outsider.LoginAt=DateTime.MinValue;
        ItemHandler.UnequipItem(new ItemUsage(true){dwParam=16},outsider);
        ItemHandler.EquipItem(new ItemUsage(true){UID=restored.UID,dwParam=15,Action=ItemUsage.Mode.EquipItem},outsider);
        Check(outsider.Equipment.TryGetItem(restored.UID)!=null && ConquerItemTable.LoadItem(restored.UID).Position==15
            && !outsider.Inventory.ContainsUID(restored.UID),"earned accessory can still move from the left hand to the right through ordinary equipment handlers");
        var earnedSoul=TeratoDrops(map).Single(d=>d.ItemID==800001);Loot(partner,earnedSoul);
        Check(partner.Inventory.ContainsUID(earnedSoul.Item.UID) && HasAchievement(partner,10932),"actual Terato soul pickup supplies the ordinary artifact application route");
        TeratoLoot.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(n=>0));
        teratoNow=teratoNow.AddMinutes(2);Call(TeratoLoot,null,"Tick",true);Check(TeratoDrops(map).Length==0,"absolute loot expiry survives reconstruction without refreshing the timer");
        VerifyEarnedTeratoRefinery(runtime,partner);
        VerifyTeratoRoyal(town);
        VerifyTeratoFailures(map);
        VerifyTeratoRecovery(map);
        VerifyTeratoLootFailures(map);
        VerifyTeratoCombat(map);
        VerifyTeratoCompanion(map);
        VerifyTeratoStudyEvidence(map);
        ClearTerato();
    }
    static void VerifyEarnedTeratoRefinery(string runtime,GameState p) {
        Query("CREATE TABLE IF NOT EXISTS refinery (LIKE public.refinery INCLUDING ALL);INSERT INTO refinery SELECT * FROM public.refinery;CREATE TABLE IF NOT EXISTS refineryboxes (LIKE public.refineryboxes INCLUDING ALL);INSERT INTO refineryboxes SELECT * FROM public.refineryboxes");
        Refinery.LoadItems();
        var type=typeof(Entity).Assembly.GetType("Revora.Game.Features.DisCityPacks");Call(type,null,"Initialize");
        var roll=type.GetField("Roll",Hidden);object previous=roll.GetValue(null);
        try {
            Check(p.Account.State==AccountTable.AccountState.Player && p.Inventory.Objects.Count(i=>i.ID==720399)==1,
                "ordinary Terato killer retains the single pack collected from its earned boss loot");
            p=ReloadGold(p);ConquerItemTable.LoadItems(p);
            var box=p.Inventory.Objects.Single(i=>i.ID==720399);
            foreach(ushort mapId in new ushort[]{1011,1020,1000})World(runtime,mapId);
            ulong before=p.Entity.Money;
            var clock=Daily.GetField("Clock",Hidden);object oldClock=clock.GetValue(null);
            try {
                dailyNow=new DateTime(2030,1,12,0,0,0,DateTimeKind.Local).ToUniversalTime();
                clock.SetValue(null,(Func<DateTime>)(()=>dailyNow));Call(Daily,null,"Create");DailyTick(DailySchedule().First());
                var boss=DailyActive();Check(boss!=null,"scheduled daily boss is available for ordinary refinery target funding");
                p.BindWorldMap(boss.MapID);p.Entity.X=boss.X;p.Entity.Y=boss.Y;uint damage=boss.MaxHitpoints;
                Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,boss,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=boss.UID},ref damage,null);
                Check(!Fenced(p) && p.Entity.Money==before+10000000
                    && Scalar("SELECT count(*) FROM revora_daily_boss_runs WHERE killer=@uid AND paid",p.Entity.UID)==1,
                    "ordinary refinery player earns target purchase funds from a committed daily boss victory");
            } finally {clock.SetValue(null,oldClock);}
            string oldDirectory=Environment.CurrentDirectory;
            try {
                Environment.CurrentDirectory=runtime;ShopFile.Load();Call(BookShop,null,"Initialize");
                var town=Kernel.Maps[1002];MiningNpc(town,1);p.BindWorldMap(1002);p.Entity.X=town.Npcs[1].X;p.Entity.Y=town.Npcs[1].Y;
                ItemHandler.HandleBuyFromNPC(BuyBook(562000,1,1),p);
            } finally {Environment.CurrentDirectory=oldDirectory;}
            var target=p.Inventory.Objects.Single(i=>i.ID==562000);uint uid=target.UID;
            Check(!Fenced(p) && p.Entity.Money==before+9999000 && ConquerItemTable.LoadItem(uid).ID==562000,
                "earned boss silver buys a persisted Pickaxe through the ordinary shop handler");
            var pool=(uint[])type.GetField("superiorLevel4",Hidden).GetValue(null);
            int index=Array.FindIndex(pool,id=>(bool)Call(RefineProgress,null,"Fits",target,Kernel.DatabaseRefinery[id]));
            Check(index>=0,"earned Superior pack catalog contains material compatible with the purchased Pickaxe");
            uint materialId=pool[index];
            SuperiorDraw(type,0,index);ItemHandler.UseItem(box,p);
            var material=p.Inventory.Objects.Single(i=>i.ID==materialId);
            Check(!Fenced(p) && !p.Inventory.ContainsUID(box.UID) && RefineryPackReceipts(p)==1,
                "production character reload preserves earned pack ownership and opening consumes it once");
            Refine(p,target,material);
            Check(!Fenced(p) && target.RefineItem==materialId && !p.Inventory.ContainsUID(material.UID) && HasAchievement(p,10431),
                "earned Terato pack material applies through the actual refinery handler and awards first refinery");
            p=ReloadGold(p);ConquerItemTable.LoadItems(p);p.Entity.MyAchievement.Send();
            target=p.Inventory.Objects.Single(i=>i.UID==uid);
            Check(!Fenced(p) && target.RefineItem==materialId && target.RefineStats.Level==4 && RefineRows(target,"duration>0")==1
                && !p.Inventory.ContainsUID(material.UID) && !p.Inventory.ContainsUID(box.UID) && HasAchievement(p,10431)
                && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10431",p.Entity.UID)==1,
                "full production character and item reload restores earned refinery effect and exactly one achievement without consumed rewards");
            ItemHandler.UseItem(box,p);Refine(p,target,material);
            Check(!Fenced(p) && RefineryPackReceipts(p)==1 && RefineRows(target,"true")==1,
                "stale pre-reload pack and material requests cannot duplicate earned refinery rewards");
            VerifyEarnedArtifactRoute(runtime,p);
        } finally {roll.SetValue(null,previous);}
    }
    static void NobilityDialog(GameState p,uint npc,byte option,string input="") {
        byte[] text=System.Text.Encoding.ASCII.GetBytes(input),packet=new byte[28+input.Length];
        Buffer.BlockCopy(System.BitConverter.GetBytes(npc),0,packet,8,4);packet[14]=option;packet[17]=(byte)text.Length;Buffer.BlockCopy(text,0,packet,18,text.Length);
        var request=new NpcRequest(false);request.Deserialize(packet);Npcs.GetDialog(request,p);
    }
    static void VerifyEarnedNobility(GameState p,string runtime,Action<GameState> completed=null,int expectedBosses=1) {
        var service=typeof(Entity).Assembly.GetType("Revora.Game.Features.NobilityDonations");
        Query("CREATE TABLE IF NOT EXISTS nobility (LIKE public.nobility INCLUDING ALL)");Call(service,null,"Initialize");
        SobNpcSpawn donor;
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT mapid,cellx,celly,lookface,type,name,life,maxlife FROM public.sobnpcs WHERE id=900042",c))
        using(var r=q.ExecuteReader()){if(!r.Read())throw new Exception("Installed nobility donor missing.");donor=new SobNpcSpawn{UID=900042,MapID=Convert.ToUInt16(r[0]),X=Convert.ToUInt16(r[1]),Y=Convert.ToUInt16(r[2]),Mesh=Convert.ToUInt16(r[3]),Type=(Enums.NpcType)Convert.ToByte(r[4]),Name=r.GetString(5),Hitpoints=Convert.ToUInt32(r[6]),MaxHitpoints=Convert.ToUInt32(r[7])};}}
        var map=World(runtime,donor.MapID);map.AddNpc(donor);p.BindWorldMap(donor.MapID);p.Entity.X=donor.X;p.Entity.Y=donor.Y;p.ActiveNpc=900042;
        uint before=p.Entity.ConquerPoints;Check(before>=100000 && p.Account.State==AccountTable.AccountState.Player
            && Scalar("SELECT count(*) FROM revora_daily_boss_runs WHERE killer=@uid AND paid",p.Entity.UID)==expectedBosses,
            "ordinary nobility donor has sufficient CPs from its paid daily-boss victory");
        DateTime now=DateTime.UtcNow;object old=service.GetField("Clock",Hidden).GetValue(null);service.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        try {
            foreach(string mode in new[]{"forged","cancel","expired","remote","switch","invalid"}) {
                p.ActiveNpc=900042;p.Entity.X=map.Npcs[900042].X;p.Entity.Y=map.Npcs[900042].Y;
                NobilityDialog(p,900042,0);
                if(mode!="forged")NobilityDialog(p,900042,1,"100000");
                if(mode=="cancel")NobilityDialog(p,0,255);
                if(mode=="expired")now=now.AddMinutes(2);
                if(mode=="remote")p.Entity.X=(ushort)(p.Entity.X+20);
                if(mode=="switch"){p.ActiveNpc=761124;NobilityDialog(p,761124,2);p.ActiveNpc=900042;}
                if(mode=="invalid")NobilityDialog(p,900042,1,"99999");
                NobilityDialog(p,0,2);
                Check(!Fenced(p) && p.Entity.ConquerPoints==before && p.NobilityInformation.Donation==0
                    && Scalar("SELECT count(*) FROM revora_nobility_payments WHERE uid=@uid",p.Entity.UID)==0,
                    "nobility NPC refuses invalid confirmation without spending earned CPs: "+mode);
            }
            p.Entity.X=map.Npcs[900042].X;p.Entity.Y=map.Npcs[900042].Y;
            NobilityDialog(p,900042,0);NobilityDialog(p,900042,1,"100000");
            Parallel.For(0,8,n=>NobilityDialog(p,0,2));
            Check(!Fenced(p) && p.Entity.ConquerPoints==before-100000 && p.NobilityInformation.Donation==5000000000UL
                && HasAchievement(p,10318) && HasAchievement(p,11120) && Scalar("SELECT count(*) FROM revora_nobility_payments WHERE uid=@uid",p.Entity.UID)==1,
                "actual donor NPC spends earned boss CPs once and grants first-rank and King achievements");
            p=ReloadBall(p);p.Entity.MyAchievement.Send();p.ActiveNpc=900042;NobilityDialog(p,0,2);
            Check(p.Entity.ConquerPoints==before-100000 && p.NobilityInformation.Donation==5000000000UL && HasAchievement(p,10318) && HasAchievement(p,11120)
                && Scalar("SELECT count(*) FROM revora_nobility_payments WHERE uid=@uid",p.Entity.UID)==1,
                "full reload preserves earned NPC donation and both rank achievements and discards confirmation replay");
            if(completed!=null)completed(p);
        } finally {service.GetField("Clock",Hidden).SetValue(null,old);}
    }
    static void VerifyEarnedArtifactRoute(string runtime,GameState p) {
        string oldDirectory=Environment.CurrentDirectory;
        try {
            Environment.CurrentDirectory=runtime;SoulProtection.Load();ShopFile.Load();EShopFile.Load();EShopV2File.Load();
            ushort shopMap;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT mapid FROM public.npcs WHERE id=10015",c)) {
                object found=q.ExecuteScalar();if(found==null)throw new Exception("Installed weapon shop NPC 10015 is absent.");shopMap=Convert.ToUInt16(found);
            }}
            var town=Kernel.Maps.ContainsKey(shopMap)?Kernel.Maps[shopMap]:World(runtime,shopMap);
            MiningNpc(town,10015);p.BindWorldMap(shopMap);p.Entity.X=town.Npcs[10015].X;p.Entity.Y=town.Npcs[10015].Y;
            ulong gold=p.Entity.Money;uint cp=p.Entity.ConquerPoints;
            Query("CREATE FUNCTION suppress_earned_weapon() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_earned_weapon BEFORE INSERT ON items FOR EACH ROW EXECUTE FUNCTION suppress_earned_weapon()");
            try{ItemHandler.HandleBuyFromNPC(BuyBook(410233,1,10015),p);}
            finally{Query("DROP TRIGGER suppress_earned_weapon ON items;DROP FUNCTION suppress_earned_weapon()");}
            Check(Fenced(p) && p.Entity.Money==gold && !p.Inventory.Objects.Any(i=>i.ID==410233)
                && Scalar("SELECT count(*) FROM revora_weapon_book_purchases WHERE uid=@uid AND item_type=410233",p.Entity.UID)==0,
                "failed earned equipment insert rolls back silver and purchase receipt together");
            p=ReloadGold(p);ConquerItemTable.LoadItems(p);p.BindWorldMap(shopMap);p.Entity.X=town.Npcs[10015].X;p.Entity.Y=town.Npcs[10015].Y;
            Check(p.Entity.Money==gold && p.Entity.ConquerPoints==cp,"real reload after failed equipment purchase preserves both earned purses");
            Parallel.For(0,8,n=>ItemHandler.HandleBuyFromNPC(BuyBook(410233,1,10015),p));
            Check(Scalar("SELECT count(*) FROM revora_weapon_book_purchases WHERE uid=@uid AND item_type=410233",p.Entity.UID)==1,
                "concurrent earned equipment purchase records exactly one charge and reward");
            var target=p.Inventory.Objects.Single(i=>i.ID==410233);var soul=p.Inventory.Objects.Single(i=>i.ID==800001);
            Check(!Fenced(p) && p.Entity.Money==gold-ConquerItemInformation.BaseInformations[410233].GoldWorth,
                "earned daily boss silver purchases the installed level-120 blade from its actual shop");
            ItemHandler.HandleBuyFromNPC(BuyBook(1088001,3,2888),p);
            ItemHandler.HandleBuyFromNPC(BuyBook(723694,1,2888),p);
            var stone=p.Inventory.Objects.Single(i=>i.ID==723694);uint targetUid=target.UID;
            ulong savedGold=p.Entity.Money;uint savedCp=p.Entity.ConquerPoints;
            Check(p.Entity.ConquerPoints==cp-3*ConquerItemInformation.BaseInformations[1088001].ConquerPointsWorth-ConquerItemInformation.BaseInformations[723694].ConquerPointsWorth,
                "earned boss CPs purchase exactly the required three Meteors and ten-point stabilization stone");
            Check(SavedRune(target,"stacksize")==1 && target.StackSize==1,
                "ordinary silver shop persists a single equipment unit before adding it to the active inventory");
            var packet=RefineRequest(target.UID,soul.UID);packet[4]=1;Purification.Process(packet,p);
            Check(!Fenced(p) && target.Purification.PurificationItemID==800001 && HasAchievement(p,10501)
                && !p.Inventory.ContainsUID(soul.UID) && !p.Inventory.Objects.Any(i=>i.ID==1088001),
                "earned soul and shop-funded Meteor payment apply to the purchased blade through the actual handler");
            p=ReloadGold(p);ConquerItemTable.LoadItems(p);p.Entity.MyAchievement.Send();
            target=p.Inventory.Objects.Single(i=>i.UID==targetUid);stone=p.Inventory.Objects.Single(i=>i.UID==stone.UID);
            Check(target.Purification.PurificationItemID==800001 && target.Purification.PurificationDuration>0 && HasAchievement(p,10501)
                && p.Entity.Money==savedGold && p.Entity.ConquerPoints==savedCp,
                "full character and item load retain the earned temporary soul and application achievement");
            var stabilize=StabilizeRequest(target.UID,stone.UID);stabilize[4]=1;ItemAddingStabilization.Process(stabilize,p);
            Check(!Fenced(p) && target.Purification.PurificationDuration==0 && HasAchievement(p,10502) && !p.Inventory.ContainsUID(stone.UID),
                "earned stabilization stone makes the applied soul permanent with its own achievement");
            p=ReloadGold(p);ConquerItemTable.LoadItems(p);p.Entity.MyAchievement.Send();target=p.Inventory.Objects.Single(i=>i.UID==targetUid);
            Check(!Fenced(p) && target.Purification.PurificationItemID==800001 && target.Purification.PurificationDuration==0
                && HasAchievement(p,10501) && HasAchievement(p,10502) && !p.Inventory.ContainsUID(stone.UID) && !p.Inventory.ContainsUID(soul.UID)
                && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id IN(10501,10502)",p.Entity.UID)==2,
                "full reload retains the earned permanent Dragon Soul and both exact achievement receipts without consumed materials");
            Purification.Process(packet,p);ItemAddingStabilization.Process(stabilize,p);
            Check(!Fenced(p) && p.Entity.Money==savedGold && p.Entity.ConquerPoints==savedCp && target.Purification.PurificationDuration==0
                && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id IN(10501,10502)",p.Entity.UID)==2,
                "post-reload spent-material replay preserves paid purses and both unique artifact achievements");
            VerifyEarnedNobility(p,runtime);
        } finally {Environment.CurrentDirectory=oldDirectory;}
    }
    static void VerifyTeratoRoyal(Map town) {
        var p=TeratoPlayer(town);var pill=Pack(p,720598,2);var meteors=Pack(p,1088001,8);TeratoNpc(p,2);
        Check(pill.StackSize==2 && meteors.StackSize==8 && TeratoRuns(p)==0,"forged Royal confirmation cannot spend an offering");
        TeratoNpc(p,1);Npcs.GetDialog(new NpcRequest(true){NpcID=0,OptionID=255},p);TeratoNpc(p,2);
        Check(TeratoRuns(p)==0 && pill.StackSize==2,"native zero-ID cancel revokes a Royal offering");
        TeratoNpc(p,1);teratoNow=teratoNow.AddMinutes(2);TeratoNpc(p,2);Check(TeratoRuns(p)==0,"expired Royal offer cannot consume materials");
        TeratoNpc(p,1);Parallel.For(0,8,n=>Npcs.GetDialog(new NpcRequest(true){NpcID=180810,OptionID=2},p));
        Check(!Fenced(p) && pill.StackSize==1 && meteors.StackSize==3 && TeratoRuns(p)==1 && TeratoBoss(p).MapID==1002
            && Scalar("SELECT sum(consumed) FROM revora_terato_materials m JOIN revora_terato_runs r ON r.id=m.run WHERE r.uid=@uid",p.Entity.UID)==6,
            "concurrent Royal confirmation spends one pill and five stacked Meteors with exact payment receipts");
        ClearTerato();TeratoNpc(p,3);Check(!Fenced(p) && pill.StackSize==1 && meteors.StackSize==3 && TeratoRuns(p)==1,"Royal paid summon reconstructs without charging another pill or Meteor");
        var other=TeratoPlayer(Kernel.Maps[2056]);var oldCounter=other.Map.EntityUIDCounter;other.Map.EntityUIDCounter=new Counter(TeratoBoss(p).UID-1);
        try{ItemHandler.UseItem(Pack(other,720598,1),other);}finally{other.Map.EntityUIDCounter=oldCounter;}
        Check(!Fenced(other) && TeratoActors().Length==2 && TeratoBoss(other).UID!=TeratoBoss(p).UID,"simultaneous Royal and Grotto summons avoid overlapping per-map monster counters");
        var stale=TeratoPlayer(town);var stalePill=Pack(stale,720598,1);var staleMeteors=Pack(stale,1088001,5);TeratoNpc(stale,1);
        Query("UPDATE items SET locked=1 WHERE uid=@uid","uid",(long)staleMeteors.UID);TeratoNpc(stale,2);
        Check(Fenced(stale) && TeratoRuns(stale)==0 && stale.Inventory.ContainsUID(stalePill.UID) && SavedRune(stalePill,"id")==720598
            && SavedRune(staleMeteors,"stacksize")==5,"a failed Meteor debit rolls back the earlier pill debit and the Royal summon together");
        ClearTerato();
    }
    static void VerifyTeratoFailures(Map map) {
        foreach(string failure in new[]{"material","receipt","run"}) {
            var p=TeratoPlayer(map);var pill=Pack(p,720598,1);
            if(failure=="material")Query("UPDATE items SET locked=1 WHERE uid=@uid","uid",(long)pill.UID);
            string table=failure=="receipt"?"revora_terato_materials":"revora_terato_runs";
            if(failure!="material")Query("CREATE FUNCTION suppress_terato() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_terato BEFORE INSERT ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_terato()");
            try{ItemHandler.UseItem(pill,p);}finally{if(failure!="material")Query("DROP TRIGGER suppress_terato ON "+table+";DROP FUNCTION suppress_terato()");}
            Check(Fenced(p) && TeratoRuns(p)==0 && p.Inventory.ContainsUID(pill.UID) && SavedRune(pill,"id")==720598,"failed summon rolls back material, run and receipts: "+failure);
        }
        foreach(string table in new[]{"revora_terato_study","revora_terato_loot","revora_achievement_evidence"}) {
            var p=TeratoPlayer(map);ItemHandler.UseItem(Pack(p,720598,1),p);var boss=TeratoBoss(p);
            Query("CREATE FUNCTION suppress_terato_reward() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_terato_reward BEFORE INSERT ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_terato_reward()");
            try{SlayTerato(p,boss);}finally{Query("DROP TRIGGER suppress_terato_reward ON "+table+";DROP FUNCTION suppress_terato_reward()");}
            Check(Fenced(p) && !boss.Dead && TeratoRuns(p,"state='active'")==1 && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10526",p.Entity.UID)==0 && TeratoStudy(p)==0,"failed team, loot or achievement receipt preserves the living paid boss: "+table);
            DemonReset(p);SlayTerato(p,boss);Check(!Fenced(p) && boss.Dead && TeratoStudy(p)==1 && HasAchievement(p,10526),"reloaded session can retry the rolled-back boss victory: "+table);ClearTerato();
        }
        var capacity=new System.Collections.Generic.List<GameState>();for(int n=0;n<16;n++){var p=TeratoPlayer(map);ItemHandler.UseItem(Pack(p,720598,1),p);capacity.Add(p);}
        var full=TeratoPlayer(map);var offered=Pack(full,720598,1);ItemHandler.UseItem(offered,full);
        Check(TeratoActors().Length==16 && !Fenced(full) && full.Inventory.ContainsUID(offered.UID) && TeratoRuns(full)==0,"bounded encounter capacity rejects a seventeenth summon without payment");ClearTerato();
    }
    static void VerifyTeratoRecovery(Map map) {
        var old=TeratoPlayer(map);ItemHandler.UseItem(Pack(old,720598,1),old);var boss=TeratoBoss(old);teratoNow=teratoNow.AddMinutes(31);boss.Die(old.Entity);Call(Terato,null,"Tick");
        Check(TeratoRuns(old,"state='active'")==1 && TeratoStudy(old)==0 && !TeratoActors().Any(a=>ReferenceEquals(DragonField(a,"Owner"),old)),"expired dragon cannot give late credit and retains its paid summon");
        TeratoNpc(old,3);Check(TeratoBoss(old).Hitpoints==50000000 && old.Inventory.Count==0,"expired paid hunt resumes at full boss health without another offering");EntityTable.SaveEntity(old);
        var fresh=TeratoPlayer(map);uint temp=fresh.Entity.UID;fresh.Account.EntityID=old.Entity.UID;fresh.Account.Username="terato_fixture";fresh.Quests=new Quests(fresh);
        Check((bool)Call(typeof(EntityTable),null,"LoadEntityState",fresh),"ordinary character state reloads in the middle of a paid dragon hunt");
        Kernel.GamePool.Remove(temp);Kernel.GamePool[old.Entity.UID]=fresh;fresh.Action=2;ConquerItemTable.LoadItems(fresh);Call(Terato,null,"Tick");TeratoNpc(fresh,3);
        Check(!Fenced(fresh) && fresh.Inventory.Count==0 && TeratoRuns(fresh)==1 && TeratoBoss(fresh).Hitpoints==50000000,"fresh session reconstructs its persisted paid hunt and invalidates the old actor");
        fresh.Entity.SubClasses.StudyPoints=65520;Query("UPDATE entities SET studypoints=65520 WHERE uid=@uid","uid",(long)fresh.Entity.UID);SlayTerato(fresh,TeratoBoss(fresh));
        Check(!Fenced(fresh) && fresh.Entity.SubClasses.StudyPoints==65520 && TeratoStudy(fresh,"paid_at IS NULL")==1,"Study Point overflow leaves the full fifty-point entitlement pending");
        fresh.Entity.SubClasses.StudyPoints=100;Query("UPDATE entities SET studypoints=100 WHERE uid=@uid","uid",(long)fresh.Entity.UID);
        Parallel.For(0,8,n=>Call(TeratoLoot,null,"PayStudy",fresh));Check(!Fenced(fresh) && fresh.Entity.SubClasses.StudyPoints==150 && TeratoStudy(fresh,"paid_at IS NOT NULL")==1,"pending Study Points pay once when balance space is available");
    }
    static void VerifyTeratoLootFailures(Map map) {
        ClearTerato();teratoNow=teratoNow.AddMinutes(3);Call(TeratoLoot,null,"Tick",true);
        var p=TeratoPlayer(map);ItemHandler.UseItem(Pack(p,720598,1),p);var boss=TeratoBoss(p);
        Query("CREATE FUNCTION suppress_terato_study_pay() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.studypoints>OLD.studypoints THEN RETURN NULL; END IF; RETURN NEW; END $$;CREATE TRIGGER suppress_terato_study_pay BEFORE UPDATE ON entities FOR EACH ROW EXECUTE FUNCTION suppress_terato_study_pay()");
        try{SlayTerato(p,boss);}finally{Query("DROP TRIGGER suppress_terato_study_pay ON entities;DROP FUNCTION suppress_terato_study_pay()");}
        Check(Fenced(p) && boss.Dead && TeratoRuns(p,"state='victory'")==1 && TeratoStudy(p,"paid_at IS NULL")==1 && p.Entity.SubClasses.StudyPoints==0,"failed Study Point payment preserves the committed victory and unpaid fifty-point entitlement");
        DemonReset(p);Call(TeratoLoot,null,"PayStudy",p);Check(!Fenced(p) && p.Entity.SubClasses.StudyPoints==50 && TeratoStudy(p,"paid_at IS NOT NULL")==1,"unpaid Study Points recover exactly once after session reload");
        var drop=TeratoDrops(map).Single(d=>d.ItemID==720399);p.Entity.X=drop.X;p.Entity.Y=drop.Y;
        foreach(string mode in new[]{"short","length","family","seal","oversize"}) {
            var packet=LootPacket(drop);if(mode=="short")Array.Resize(ref packet,12);else if(mode=="length")packet[0]--;else if(mode=="family")packet[2]++;else if(mode=="seal")packet[packet.Length-1]^=1;else Array.Resize(ref packet,129);
            FloorItem.Process(packet,p);Check(!Fenced(p) && !drop.PickedUpAlready && p.Inventory.Count==0,"malformed native Terato pickup cannot mutate inventory: "+mode);
        }
        for(int n=0;n<40;n++)Pack(p,1088001,1);Loot(p,drop);Check(!Fenced(p) && !drop.PickedUpAlready && p.Inventory.Count==40,"full bag leaves Terato loot available on the floor");
        foreach(var filler in p.Inventory.Objects.ToArray())DisRemove(p,filler);
        Query("CREATE FUNCTION suppress_terato_pickup() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_terato_pickup BEFORE INSERT ON items FOR EACH ROW EXECUTE FUNCTION suppress_terato_pickup()");
        try{Loot(p,drop);}finally{Query("DROP TRIGGER suppress_terato_pickup ON items;DROP FUNCTION suppress_terato_pickup()");}
        Check(Fenced(p) && !drop.PickedUpAlready && p.Inventory.Count==0 && Scalar("SELECT count(*) FROM revora_terato_loot WHERE owner=@uid AND collector IS NOT NULL",p.Entity.UID)==0,"failed loot insertion rolls back the persisted pickup claim and retains its floor object");
        DemonReset(p);Loot(p,drop);Check(!Fenced(p) && drop.PickedUpAlready && p.Inventory.Objects.Single().ID==720399,"uncommitted floor loot can be retried once after reload");
        var soul=TeratoDrops(map).Single(d=>d.ItemID>=800000 && d.ItemID<900000);
        Query("CREATE FUNCTION suppress_dragon_achievement() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_dragon_achievement BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW EXECUTE FUNCTION suppress_dragon_achievement()");
        try{Loot(p,soul);}finally{Query("DROP TRIGGER suppress_dragon_achievement ON revora_achievement_evidence;DROP FUNCTION suppress_dragon_achievement()");}
        Check(Fenced(p) && !soul.PickedUpAlready && !p.Inventory.ContainsUID(soul.Item.UID) && !HasAchievement(p,10932),"silently suppressed Dragon Slayer evidence rolls back the soul pickup and reward together");
        DemonReset(p);Loot(p,soul);var saved=ConquerItemTable.LoadItem(soul.Item.UID);
        Check(!Fenced(p) && saved.ID==soul.ItemID && ConquerItemInformation.BaseInformations[saved.ID].PurificationLevel==7
            && saved.Bound,"the new bound P7 contract survives native pickup and item loading");
        Check(HasAchievement(p,10932) && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10932 AND source='dragon-slayer-v1'",p.Entity.UID)==1,
            "receiving the quest-provenanced soul sets installed achievement 10932 in the ordinary player's serialized progress");
        Check(EntityTable.SaveEntity(p) && Scalar("SELECT studypoints FROM entities WHERE uid=@uid",p.Entity.UID)==50,"ordinary full character save preserves the committed Study Point reward");
        var fresh=TeratoPlayer(map);uint temporary=fresh.Entity.UID;fresh.Account.EntityID=p.Entity.UID;fresh.Account.Username="terato_reward_fixture";fresh.Quests=new Quests(fresh);
        Check((bool)Call(typeof(EntityTable),null,"LoadEntityState",fresh),"ordinary completed-hunt character reload succeeds");
        Kernel.GamePool.Remove(temporary);Kernel.GamePool[p.Entity.UID]=fresh;fresh.Action=2;ConquerItemTable.LoadItems(fresh);
        fresh.Entity.MyAchievement.Send(); // GeneralData's ordinary entry path refreshes evidence here.
        Check(fresh.Entity.SubClasses.StudyPoints==50 && fresh.Inventory.ContainsUID(soul.Item.UID) && HasAchievement(fresh,10932),"fresh character and item load retain Terato Study Points, collected soul and Dragon Slayer achievement");
        ClearTerato();
    }
    static void VerifyTeratoCombat(Map map) {
        if(!SpellTable.SpellInformations.ContainsKey(12680))SpellTable.Load();
        var front=TeratoPlayer(map);var flank=TeratoPlayer(map);var back=TeratoPlayer(map);var stranger=TeratoPlayer(map);TeratoTeam(front,flank,back);
        ItemHandler.UseItem(Pack(front,720598,1),front);var boss=TeratoBoss(front);var members=new[]{front,flank,back};
        foreach(var p in members.Concat(new[]{stranger})){p.Attackable=true;p.Entity.MaxHitpoints=200000;p.Entity.Hitpoints=200000;}
        front.Entity.X=(ushort)(boss.X+4);front.Entity.Y=boss.Y;flank.Entity.X=boss.X;flank.Entity.Y=(ushort)(boss.Y+4);back.Entity.X=(ushort)(boss.X-4);back.Entity.Y=boss.Y;
        stranger.Entity.X=front.Entity.X;stranger.Entity.Y=front.Entity.Y;
        Check(Revora.Game.Attacking.Handle.CanAttack(boss,front.Entity,null,true) && !Revora.Game.Attacking.Handle.CanAttack(boss,stranger.Entity,null,true),"monster combat admission permits the paid party and excludes nearby bystanders");
        uint[] before=members.Select(p=>p.Entity.Hitpoints).ToArray();new Revora.Game.Attacking.Handle(null,boss,front.Entity);
        Check(members.Select(p=>p.Entity.Hitpoints).SequenceEqual(before),"Dragon Breath gives a warning before dealing damage");
        teratoNow=teratoNow.AddSeconds(2);new Revora.Game.Attacking.Handle(null,boss,front.Entity);
        Check(front.Entity.Hitpoints<before[0] && flank.Entity.Hitpoints==before[1] && back.Entity.Hitpoints==before[2] && stranger.Entity.Hitpoints==200000,"Dragon Breath hits the forward cone using normal magic defense and spares flanks, rear and bystanders");
        uint frontHp=front.Entity.Hitpoints;Parallel.For(0,8,n=>new Revora.Game.Attacking.Handle(null,boss,front.Entity));
        Check(front.Entity.Hitpoints==frontHp,"concurrent monster callbacks cannot replay a resolved cast");
        teratoNow=teratoNow.AddSeconds(4);new Revora.Game.Attacking.Handle(null,boss,front.Entity);teratoNow=teratoNow.AddSeconds(2);new Revora.Game.Attacking.Handle(null,boss,front.Entity);
        Check(back.Entity.Hitpoints<before[2] && front.Entity.Hitpoints==frontHp,"Tail Sweep hits the backward cone instead of the forward target");
        before=members.Select(p=>p.Entity.Hitpoints).ToArray();teratoNow=teratoNow.AddSeconds(4);new Revora.Game.Attacking.Handle(null,boss,front.Entity);
        flank.Entity.Y=(ushort)(boss.Y+9);teratoNow=teratoNow.AddSeconds(2);Call(Terato,null,"Tick");
        Check(front.Entity.Hitpoints<before[0] && flank.Entity.Hitpoints==before[1] && back.Entity.Hitpoints<before[2],"moving beyond Flame Shower's announced circle avoids its damage");
        before=members.Select(p=>p.Entity.Hitpoints).ToArray();back.Entity.X=(ushort)(boss.X-1);teratoNow=teratoNow.AddSeconds(4);new Revora.Game.Attacking.Handle(null,boss,front.Entity);teratoNow=teratoNow.AddSeconds(2);new Revora.Game.Attacking.Handle(null,boss,front.Entity);
        Check(front.Entity.Hitpoints<before[0] && back.Entity.Hitpoints==before[2] && flank.Entity.Hitpoints==before[1],"Misty Flame's ring leaves room close to the dragon and beyond its reach");
        teratoNow=teratoNow.AddSeconds(4);new Revora.Game.Attacking.Handle(null,boss,front.Entity);teratoNow=teratoNow.AddSeconds(2);new Revora.Game.Attacking.Handle(null,boss,front.Entity);
        Check(front.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.FreezeSmall) && front.Entity.Fright==2 && !flank.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.FreezeSmall)
            && !stranger.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.FreezeSmall),"Fear Roar briefly frightens nearby party members while retreating players and strangers remain unaffected");
        ClearTerato();
        foreach(bool kill in new[]{false,true}){
            var warrior=TeratoPlayer(map);warrior.Entity.Class=25;EntityTable.SaveEntity(warrior);RebirthSkill(warrior,12680);WarriorFists(warrior);
            ItemHandler.UseItem(Pack(warrior,720598,1),warrior);boss=TeratoBoss(warrior);warrior.Attackable=true;warrior.Entity.MaxHitpoints=warrior.Entity.Hitpoints=200000;warrior.Entity.Stamina=0;
            warrior.Entity.X=(ushort)(boss.X+4);warrior.Entity.Y=boss.Y;if(kill)boss.Hitpoints=50000;uint bossHp=boss.Hitpoints;WarriorCast(warrior,12680);
            new Revora.Game.Attacking.Handle(null,boss,warrior.Entity);teratoNow=teratoNow.AddSeconds(2);new Revora.Game.Attacking.Handle(null,boss,warrior.Entity);
            Check(!Fenced(warrior) && !warrior.Entity.OnBackFire() && warrior.Entity.Stamina==30 && warrior.Entity.Hitpoints<200000
                && (kill?boss.Dead && TeratoRuns(warrior,"state='victory'")==1 && warrior.Entity.SubClasses.StudyPoints==50:boss.Hitpoints==bossHp-50000),"Terato named hit triggers one counter and preserves owned victory: lethal="+kill);
            ClearTerato();
        }
    }
    static void VerifyTeratoStudyEvidence(Map map) {
        var p=TeratoPlayer(map);ItemHandler.UseItem(Pack(p,720598,1),p);var boss=TeratoBoss(p);
        Query("CREATE FUNCTION suppress_study() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.achievement_id=10319 THEN RETURN NULL; END IF; RETURN NEW; END $$;CREATE TRIGGER suppress_study BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW EXECUTE FUNCTION suppress_study()");
        try{SlayTerato(p,boss);}finally{Query("DROP TRIGGER suppress_study ON revora_achievement_evidence;DROP FUNCTION suppress_study()");}
        Check(Fenced(p) && boss.Dead && TeratoRuns(p,"state='victory'")==1 && TeratoStudy(p,"paid_at IS NULL")==1 && p.Entity.SubClasses.StudyPoints==0,
            "failed first-study evidence preserves a committed Terato victory and its unpaid Study Point entitlement");VerifyStudyRollback(p);
        p=ReloadBall(p);Call(TeratoLoot,null,"PayStudy",p);VerifyStudyAward(p,"terato");
        Check(TeratoStudy(p,"paid_at IS NULL")==0 && p.Entity.SubClasses.StudyPoints==50,"production reconnect pays the retained boss reward once with its evidence");
        Call(TeratoLoot,null,"PayStudy",p);p=ReloadBall(p);VerifyStudyAward(p,"terato");
        Check(p.Entity.SubClasses.StudyPoints==50 && StudyEvidence(p)==1,"paid entitlement replay and reconnect retain one Study Point credit and achievement");ClearTerato();
    }
    static void VerifyTeratoCompanion(Map map) {
        var p=TeratoPlayer(map);ItemHandler.UseItem(Pack(p,720598,1),p);var boss=TeratoBoss(p);p.Entity.X=boss.X;p.Entity.Y=boss.Y;
        var pet=new Entity(EntityFlag.Monster,true){UID=p.Entity.UID-200000,Owner=p,MonsterInfo=new MonsterInformation(),MapID=map.ID,X=boss.X,Y=boss.Y,Hitpoints=1000};p.Companion=pet;
        boss.Die(pet);Check(!boss.Dead && TeratoStudy(p)==0,"unregistered companion cannot credit a Terato victory");
        map.Companions.Add(pet.UID,pet);boss.Die(pet);
        Check(boss.Dead && !Fenced(p) && TeratoRuns(p,"state='victory'")==1 && p.Entity.SubClasses.StudyPoints==50,"a registered summoned companion can finish the dragon for its ordinary owner");
        Check(HasAchievement(p,10526),"registered companion kill awards Terato achievement to its owner");
        map.Companions.Remove(pet.UID);p.Companion=null;ClearTerato();
        p=ReloadBall(p);p.Entity.MyAchievement.Send();Check(HasAchievement(p,10526) && !HasAchievement(p,10932),"production reload preserves the kill achievement independently of an uncollected Dragon Soul");
        p.BindWorldMap(map.ID);TeratoSite(p);ItemHandler.UseItem(Pack(p,720598,1),p);SlayTerato(p,TeratoBoss(p));
        Check(!Fenced(p) && TeratoRuns(p,"state='victory'")==2 && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10526",p.Entity.UID)==1,
            "a later legitimate Terato victory succeeds without duplicating its permanent achievement");ClearTerato();
    }
}
