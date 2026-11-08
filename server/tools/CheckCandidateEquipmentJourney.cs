using System;
using System.Linq;
using System.Reflection;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle {
    static string EquipmentItems(GameState p) {
        var items=p.Inventory.Objects.Concat(p.Warehouses.Values.SelectMany(w=>w.Objects)).OrderBy(i=>i.UID).ToArray();
        Require(items.Select(i=>i.UID).Distinct().Count()==items.Length);
        string loaded=string.Join(",",items.Select(i=>i.UID+":"+i.ID+":"+(i.Bound?1:0)+":"+i.StackSize+":"+i.Warehouse+":"+i.Position));
        string saved=(string)Sql("SELECT coalesce(string_agg(uid::text||':'||id::text||':'||bound::text||':'||stacksize::text||':'||warehouse::text||':'||position::text,',' ORDER BY uid),'') FROM items WHERE entityid=@uid","uid",(long)p.Entity.UID);
        if(loaded!=saved){
            System.Console.Error.WriteLine("[equipment-items] loaded-only="+string.Join(",",loaded.Split(',').Except(saved.Split(',')).Take(3)));
            System.Console.Error.WriteLine("[equipment-items] saved-only="+string.Join(",",saved.Split(',').Except(loaded.Split(',')).Take(3)));
        }
        Require(loaded==saved);return loaded;
    }
    static void EquipmentTalk(GameState p,ushort map,uint npc,byte option) {
        p.BindWorldMap(map);HeartTalk(p,npc,option);
        Require(!(bool)typeof(Entity).GetField("PersistenceRequiresReload",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(p.Entity));
    }
    static void EquipmentKill(GameState p,ushort map,uint family) {
        if(p.Entity.MapID!=map || p.Map==null || p.Map.ID!=map)p.BindWorldMap(map);
        var body=p.Map.Entities.Values.FirstOrDefault(e=>e.MonsterInfo!=null && e.MonsterInfo.ID==family && !e.Dead);
        if(body==null && p.Map.Entities.Values.Any(e=>e.MonsterInfo!=null && e.MonsterInfo.ID==family)) {
            var until=DateTime.UtcNow.AddSeconds(45);
            do {
                System.Threading.Thread.Sleep(100);
                body=p.Map.Entities.Values.FirstOrDefault(e=>e.MonsterInfo!=null && e.MonsterInfo.ID==family && !e.Dead);
            } while(body==null && DateTime.UtcNow<until);
        }
        if(body==null)throw new InvalidOperationException("Missing equipment quest monster: map="+map+" family="+family);
        EliminationHit(p,body);
        Require(ReferenceEquals(body.Killer,p.Entity));
    }
    static long EquipmentEarlyCompleted() {
        return (long)Sql(@"SELECT
(SELECT count(*) FROM revora_connector WHERE uid=@uid AND life=0 AND stage=6 AND cardinality(rewards)=3)+
(SELECT count(*) FROM revora_spun_gold WHERE uid=@uid AND life=0 AND stage=8 AND cardinality(rewards)=2)+
(SELECT count(*) FROM revora_azure_pill WHERE uid=@uid AND life=0 AND stage=7 AND cardinality(rewards)=2)+
(SELECT count(*) FROM revora_artisan_feather WHERE uid=@uid AND life=0 AND stage=6 AND cardinality(rewards)=2)+
(SELECT count(*) FROM revora_mystic_ring WHERE uid=@uid AND life=0 AND stage=10 AND cardinality(rewards)=2)+
(SELECT count(*) FROM revora_frantic_monkeys WHERE uid=@uid AND life=0 AND stage=10 AND cardinality(rewards)=2)+
(SELECT count(*) FROM revora_soul_general WHERE uid=@uid AND life=0 AND stage=12 AND cardinality(rewards)=2)+
(SELECT count(*) FROM revora_mad_man WHERE uid=@uid AND life=0 AND stage=9 AND cardinality(rewards)=3)+
(SELECT count(*) FROM revora_ghost_city WHERE uid=@uid AND life=0 AND stage=11 AND cardinality(rewards)=3)+
(SELECT count(*) FROM revora_tomboy WHERE uid=@uid AND life=0 AND stage=6 AND cardinality(rewards)=3)+
(SELECT count(*) FROM revora_desert_blood WHERE uid=@uid AND life=0 AND stage=11 AND cardinality(rewards)=3)+
(SELECT count(*) FROM revora_hero_wings WHERE uid=@uid AND life=0 AND stage=12 AND cardinality(rewards)=3)+
(SELECT count(*) FROM revora_family_feud WHERE uid=@uid AND life=0 AND stage=10 AND cardinality(rewards)=3)+
(SELECT count(*) FROM revora_desert_treasure WHERE uid=@uid AND life=0 AND stage=8 AND cardinality(rewards)=3)+
(SELECT count(*) FROM revora_desert_expedition WHERE uid=@uid AND life=0 AND stage=6 AND cardinality(rewards)=3)+
(SELECT count(*) FROM revora_power_book q JOIN revora_power_book_rewards r USING(uid,life) WHERE q.uid=@uid AND q.life=0 AND q.stage=14 AND r.pack_uid>0 AND r.soul_uid>0 AND r.pack_uid<>r.soul_uid)+
(SELECT count(*) FROM revora_defense_talisman WHERE uid=@uid AND stage=10 AND cardinality(rewards)=2)+
(SELECT count(*) FROM revora_attack_talisman WHERE uid=@uid AND stage=11 AND cardinality(rewards)=2)+
(SELECT count(*) FROM revora_tempest_wings WHERE uid=@uid AND stage=4 AND cardinality(rewards)=2)","uid",(long)FirstUid);
    }
    static void EquipmentBusyKill(GameState p) {
        var body=p.Map.Entities.Values.First(e=>e.MonsterInfo!=null && e.MonsterInfo.ID==1 && !e.Dead);
        p.Entity.X=body.X;p.Entity.Y=(ushort)(body.Y+2);uint before=body.Hitpoints;
        var session=Feature("SessionInstall");Call(session,"Enter",p.Entity.UID);
        try {
            var attack=System.Threading.Tasks.Task.Run(()=>{
                uint damage=body.Hitpoints;
                Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,body,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=body.UID},ref damage,null);
            });
            Require(attack.Wait(TimeSpan.FromSeconds(5)));
            if(body.Dead || body.Hitpoints!=before)throw new InvalidOperationException("Busy character lease allowed lethal quest combat.");
            Require(Convert.ToInt64(Sql("SELECT feathers FROM revora_artisan_feather WHERE uid=@uid AND life=0","uid",(long)p.Entity.UID))==0);
        } finally {Call(session,"Exit",p.Entity.UID);}
    }
    static string EquipmentEarlyFingerprint() {
        return (string)Sql(@"SELECT md5(
(SELECT row_to_json(q)::text FROM revora_connector q WHERE uid=@uid AND life=0)||
(SELECT row_to_json(q)::text FROM revora_spun_gold q WHERE uid=@uid AND life=0)||
(SELECT row_to_json(q)::text FROM revora_azure_pill q WHERE uid=@uid AND life=0)||
(SELECT row_to_json(q)::text FROM revora_artisan_feather q WHERE uid=@uid AND life=0)||
(SELECT row_to_json(q)::text FROM revora_mystic_ring q WHERE uid=@uid AND life=0)||
(SELECT row_to_json(q)::text FROM revora_frantic_monkeys q WHERE uid=@uid AND life=0)||
(SELECT row_to_json(q)::text FROM revora_soul_general q WHERE uid=@uid AND life=0)||
(SELECT row_to_json(q)::text FROM revora_mad_man q WHERE uid=@uid AND life=0)||
(SELECT row_to_json(q)::text FROM revora_ghost_city q WHERE uid=@uid AND life=0)||
(SELECT row_to_json(q)::text FROM revora_tomboy q WHERE uid=@uid AND life=0)||
(SELECT row_to_json(q)::text FROM revora_desert_blood q WHERE uid=@uid AND life=0)||
(SELECT row_to_json(q)::text FROM revora_hero_wings q WHERE uid=@uid AND life=0)||
(SELECT row_to_json(q)::text FROM revora_family_feud q WHERE uid=@uid AND life=0)||
(SELECT row_to_json(q)::text FROM revora_desert_treasure q WHERE uid=@uid AND life=0)||
(SELECT row_to_json(q)::text FROM revora_desert_expedition q WHERE uid=@uid AND life=0)||
(SELECT row_to_json(q)::text FROM revora_power_book q WHERE uid=@uid AND life=0)||
(SELECT row_to_json(q)::text FROM revora_power_book_rewards q WHERE uid=@uid AND life=0)||
(SELECT row_to_json(q)::text FROM revora_defense_talisman q WHERE uid=@uid)||
(SELECT row_to_json(q)::text FROM revora_attack_talisman q WHERE uid=@uid)||
(SELECT row_to_json(q)::text FROM revora_tempest_wings q WHERE uid=@uid)||
(SELECT row_to_json(q)::text FROM revora_achievement_evidence q WHERE uid=@uid AND achievement_id=10931)||
(SELECT money::text||':'||conquerpoints::text||':'||revorapoint::text FROM entities WHERE uid=@uid))","uid",(long)FirstUid);
    }
    static void EquipmentEarlyJourney(bool restoring) {
        EquipmentMapCollectionChecks();
        Func<int,int,int,bool> respawn=(now,death,delay)=>(bool)Call(typeof(Map),"RespawnDue",new Time32(now),new Time32(death),delay);
        Require(new Time32(1000001)>new Time32(1000000).AddSeconds(1000000000)); // Old overflow regression.
        Require(!respawn(1000001,1000000,1000000000) && !respawn(1000001,1000000,int.MaxValue/2));
        Require(!respawn(31000,1000,30) && respawn(31001,1000,30));
        Require(!respawn(int.MinValue+14999,int.MaxValue-15000,30) && respawn(int.MinValue+15000,int.MaxValue-15000,30));
        Require(!respawn(15000,-15000,30) && respawn(15001,-15000,30));
        Require(!respawn(1,1,0) && respawn(2,1,0));
        Require(!respawn(999,1000,0) && !respawn(999,1000,30));
        if(!restoring)Player(Kernel.Maps[1002]);nextUid=FirstUid;
        // Town warehouse registration must precede loading its saved contents.
        var p=Player(Kernel.Maps[1002],true,1003,true);p.LoadItemStats();
        if(!restoring) {
            // Supplied starting eligibility; quest stages, materials and rewards
            // must all come from ordinary NPC and loaded monster handlers.
            p.Entity.Level=140;Require(EntityTable.SaveEntity(p));
            EquipmentTalk(p,1002,900071727,51);
            EquipmentTalk(p,1002,900071718,1);EquipmentTalk(p,1011,4437,1);
            for(int i=0;i<5;i++)EquipmentKill(p,1011,2140);
            EquipmentTalk(p,1011,4437,1);EquipmentTalk(p,1002,900071718,1);EquipmentTalk(p,1002,900071727,51);
            Require(EquipmentEarlyCompleted()==1);
            var spunRoll=Feature("SpunGoldStore").GetField("Roll",Hidden);object oldSpunRoll=spunRoll.GetValue(null);
            try {
                spunRoll.SetValue(null,(Func<int,int>)(max=>0));
                EquipmentTalk(p,1002,900071727,61);EquipmentTalk(p,1011,4464,1);
                EquipmentKill(p,1011,7);EquipmentTalk(p,1011,4464,1);EquipmentTalk(p,1011,4465,1);
                p.Entity.X=100;p.Entity.Y=430;
                ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==729972),p);
                EquipmentKill(p,1011,9921723);Call(Feature("SpunGoldQuest"),"Tick");
                ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==729973),p);
                EquipmentTalk(p,1011,4464,1);EquipmentTalk(p,1002,900071727,61);
            } finally {spunRoll.SetValue(null,oldSpunRoll);}
            Require(EquipmentEarlyCompleted()==2);
            DateTime now=DateTime.UtcNow;var azure=Feature("AzurePillQuest");
            var clock=azure.GetField("Clock",Hidden);var drop=azure.GetField("Drop",Hidden);
            object previousClock=clock.GetValue(null),previousDrop=drop.GetValue(null);
            try {
                clock.SetValue(null,(Func<DateTime>)(()=>now));drop.SetValue(null,(Func<bool>)(()=>true));
                EquipmentTalk(p,1002,900071727,1);EquipmentTalk(p,1011,900071728,1);EquipmentTalk(p,1011,900071729,1);
                for(int i=0;i<4;i++)EquipmentKill(p,1011,67);
                EquipmentTalk(p,1002,900071730,1);EquipmentTalk(p,1002,900071730,1);
                EquipmentTalk(p,1011,900071729,1);now=now.AddMinutes(2).AddSeconds(1);
                EquipmentTalk(p,1011,900071729,1);EquipmentTalk(p,1011,900071728,1);EquipmentTalk(p,1002,900071727,1);
            } finally {clock.SetValue(null,previousClock);drop.SetValue(null,previousDrop);}
            Require(EquipmentEarlyCompleted()==3);
            var roll=Feature("ArtisanFeatherQuest").GetField("Roll",Hidden);object previousRoll=roll.GetValue(null);
            try {
                roll.SetValue(null,(Func<int,int>)(max=>0));
                EquipmentTalk(p,1002,900071727,71);EquipmentTalk(p,1002,4466,1);EquipmentTalk(p,1002,4467,1);
                EquipmentBusyKill(p);
                for(int i=0;i<100;i++) {
                    EquipmentKill(p,1002,1);
                    long feathers=Convert.ToInt64(Sql("SELECT feathers FROM revora_artisan_feather WHERE uid=@uid AND life=0","uid",(long)p.Entity.UID));
                    if(feathers!=i+1)throw new InvalidOperationException("Artisan kill credit mismatch: kill="+(i+1)+" feathers="+feathers);
                }
                EquipmentTalk(p,1002,4467,1);EquipmentKill(p,1020,2143);
                EquipmentTalk(p,1002,4467,1);EquipmentTalk(p,1002,900071727,71);
            } finally {roll.SetValue(null,previousRoll);}
            Require(EquipmentEarlyCompleted()==4 && p.Inventory.Count==9 && p.Inventory.Objects.All(i=>i.Bound));
            EquipmentTalk(p,1002,900071727,81);EquipmentTalk(p,1011,4500,1);EquipmentTalk(p,1011,4501,1);
            foreach(uint medicine in new uint[]{1000000,1001000}) {
                p.BindWorldMap(1002);
                var shop=ShopFile.Shops.Values.First(s=>s.MoneyType==ShopFile.MoneyType.Gold && s.Items.Contains(medicine) && p.Map.Npcs.ContainsKey(s.UID));
                var npc=p.Map.Npcs[shop.UID];p.Entity.X=npc.X;p.Entity.Y=npc.Y;
                ulong before=p.Entity.Money;uint price=ConquerItemInformation.BaseInformations[medicine].GoldWorth;
                Require(price>0 && before>=12*price);
                ItemHandler.HandleBuyFromNPC(new ItemUsage(true){UID=shop.UID,dwParam=medicine,dwExtraInfo=12},p);
                Require(p.Entity.Money==before-12*price && p.Inventory.Objects.Where(i=>i.ID==medicine).Sum(i=>(int)i.StackSize)==12);
            }
            EquipmentTalk(p,1011,4501,1);
            for(int i=0;i<20;i++)EquipmentKill(p,1011,8);
            EquipmentTalk(p,1011,4501,1);EquipmentTalk(p,1011,900071742,1);
            EquipmentTalk(p,1011,4501,1);EquipmentTalk(p,1011,4500,1);
            ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==721708),p);
            EquipmentTalk(p,1002,900071727,81);Call(Feature("QuestAppearance"),"Tick");
            Require(EquipmentEarlyCompleted()==5 && p.Inventory.Count==11 && p.Inventory.Objects.All(i=>i.Bound));
            var frantic=Feature("FranticMonkeysQuest");var franticClock=Feature("FranticMonkeysScenes").GetField("Clock",Hidden);
            var franticRoll=Feature("FranticMonkeysStore").GetField("Roll",Hidden);
            object oldFranticClock=franticClock.GetValue(null),oldFranticRoll=franticRoll.GetValue(null);
            now=DateTime.UtcNow;
            try {
                franticClock.SetValue(null,(Func<DateTime>)(()=>now));franticRoll.SetValue(null,(Func<int,int>)(max=>0));
                EquipmentTalk(p,1002,900071727,91);EquipmentTalk(p,1020,4485,1);
                EquipmentKill(p,1020,10);EquipmentTalk(p,1020,4485,1);EquipmentTalk(p,1002,4486,1);
                ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==721700),p);
                Require(p.Entity.MapID!=1002 && p.Entity.Mesh==156);
                now=now.AddSeconds(63);Call(frantic,"Tick");
                for(int i=0;i<5;i++) {
                    var monkey=p.Map.Entities.Values.First(b=>!b.Dead && b.MonsterInfo!=null && b.MonsterInfo.ID==9917450);
                    EliminationHit(p,monkey);Require(ReferenceEquals(monkey.Killer,p.Entity));
                    now=now.AddSeconds(3);Call(frantic,"Tick");
                }
                Require(p.Entity.MapID==1002);
                EquipmentTalk(p,1002,4486,1);p.BindWorldMap(1020);p.Entity.X=795;p.Entity.Y=471;
                ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==721701),p);
                for(int i=0;i<3;i++) {
                    var ape=p.Map.Entities.Values.Single(b=>!b.Dead && b.MonsterInfo!=null && b.MonsterInfo.ID>=9917451 && b.MonsterInfo.ID<=9917452);
                    EliminationHit(p,ape);Require(ReferenceEquals(ape.Killer,p.Entity));
                    now=now.AddSeconds(3);Call(frantic,"Tick");
                }
                EquipmentTalk(p,1020,4485,1);EquipmentTalk(p,1002,900071727,91);
            } finally {franticClock.SetValue(null,oldFranticClock);franticRoll.SetValue(null,oldFranticRoll);}
            Require(EquipmentEarlyCompleted()==6 && p.Inventory.Count==13 && p.Inventory.Objects.All(i=>i.Bound));
            var soul=Feature("SoulGeneralQuest");var soulClock=Feature("SoulGeneralPhantom").GetField("Clock",Hidden);
            object oldSoulClock=soulClock.GetValue(null);now=DateTime.UtcNow;
            try {
                soulClock.SetValue(null,(Func<DateTime>)(()=>now));
                var phoenix=Kernel.Maps[1011];
                Require(phoenix.Entities.Values.Count(b=>b.MonsterInfo!=null && b.MonsterInfo.ID==106)==20);
                Require((int)Call(soul,"EnsureRockPopulation",phoenix)==0);
                EquipmentTalk(p,1002,900071727,101);EquipmentTalk(p,1020,4487,1);
                EquipmentTalk(p,1002,4488,1);EquipmentTalk(p,1020,4487,1);
                EquipmentKill(p,1020,13);
                for(int i=0;i<10;i++)EquipmentKill(p,1011,106);
                for(int i=0;i<10;i++)EquipmentKill(p,1020,70);
                EquipmentTalk(p,1020,4487,1);EquipmentTalk(p,1020,4489,1);EquipmentTalk(p,1020,4487,1);
                EquipmentTalk(p,1002,4488,1);EquipmentTalk(p,1002,4490,1);EquipmentTalk(p,1020,4487,1);
                EquipmentKill(p,1020,9917510);now=now.AddSeconds(2);Call(soul,"Tick");
                EquipmentTalk(p,1002,4488,1);EquipmentTalk(p,1002,900071727,101);
            } finally {soulClock.SetValue(null,oldSoulClock);}
            Require(EquipmentEarlyCompleted()==7 && p.Inventory.Count==15 && p.Inventory.Objects.All(i=>i.Bound));
            var madRoll=Feature("MadManStore").GetField("Roll",Hidden);object oldMadRoll=madRoll.GetValue(null);
            try {
                madRoll.SetValue(null,(Func<int,int>)(max=>0));
                EquipmentTalk(p,1002,900071727,111);EquipmentTalk(p,1020,4468,1);EquipmentTalk(p,1002,1611,1);
                EquipmentKill(p,1020,13);EquipmentTalk(p,1002,1611,1);EquipmentTalk(p,1002,4470,1);
                ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==729981),p);
                ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==729980),p);
                Require(p.Entity.MapID>=11000);
                for(uint id=9917810;id<9917817;id++) {
                    var stone=p.Map.Npcs[id];p.Entity.X=(ushort)(stone.X-2);p.Entity.Y=stone.Y;p.ActiveNpc=id;
                    Npcs.GetDialog(new NpcRequest(true){NpcID=id,OptionID=0},p);
                    Require(p.Inventory.Objects.Count(i=>i.ID==729983+id-9917810)==1);
                }
                ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==729983),p);
                Require(p.Entity.MapID==1020);
                EquipmentTalk(p,1020,4468,1);EquipmentTalk(p,1002,900071727,111);
            } finally {madRoll.SetValue(null,oldMadRoll);}
            Require(EquipmentEarlyCompleted()==8 && p.Inventory.Count==19 && p.Inventory.Objects.All(i=>i.Bound));
            Require(p.Inventory.Objects.Count(i=>i.ID==729979)==1); // Unused red toxin is not a reward.
            var ghost=Feature("GhostCityQuest");var ghostClock=Feature("GhostCityScenes").GetField("Clock",Hidden);
            object oldGhostClock=ghostClock.GetValue(null);now=DateTime.UtcNow;
            Action<uint> ghostTalk=npc=>{
                var actor=p.Map.Npcs[npc];p.Entity.X=(ushort)(actor.X-2);p.Entity.Y=actor.Y;p.ActiveNpc=npc;
                Npcs.GetDialog(new NpcRequest(true){NpcID=npc,OptionID=1},p);
            };
            Action ghostEnter=()=>{
                p.BindWorldMap(1002);p.Entity.X=440;p.Entity.Y=308;
                ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==721713),p);
                Require(p.Entity.MapID>=11000);
            };
            try {
                ghostClock.SetValue(null,(Func<DateTime>)(()=>now));
                EquipmentTalk(p,1002,900071727,121);EquipmentTalk(p,1002,4503,1);EquipmentTalk(p,1002,4504,1);
                ushort[,] sparks={{440,308},{443,344},{465,356}};
                for(int i=0;i<3;i++) {
                    p.Entity.X=sparks[i,0];p.Entity.Y=sparks[i,1];
                    ItemHandler.UseItem(p.Inventory.Objects.Single(item=>item.ID==721717),p);
                }
                EquipmentTalk(p,1002,4504,1);EquipmentTalk(p,1002,4505,1);
                ghostEnter();ghostTalk(4526);ghostTalk(9917911);
                Require(p.Entity.MapID==1002 && p.Inventory.Objects.Count(i=>i.ID==721712)==1);
                EquipmentTalk(p,1002,4505,1);ghostEnter();now=now.AddSeconds(3);Call(ghost,"Tick");
                for(int i=0;i<102;i++) {
                    var target=p.Map.Entities.Values.First(b=>!b.Dead && b.MonsterInfo!=null);
                    EliminationHit(p,target);Require(ReferenceEquals(target.Killer,p.Entity));
                    now=now.AddSeconds(3);Call(ghost,"Tick");
                }
                Require(p.Inventory.Objects.Count(i=>i.ID==721714)==1);
                for(uint npc=9917930;npc<9917935;npc++)ghostTalk(npc);
                Require(Convert.ToInt64(Sql("SELECT count(*) FROM revora_ghost_children WHERE uid=@uid","uid",(long)p.Entity.UID))==5);
                ghostTalk(4526);EquipmentTalk(p,1002,4503,1);EquipmentTalk(p,1002,900071727,121);
            } finally {ghostClock.SetValue(null,oldGhostClock);}
            Require(EquipmentEarlyCompleted()==9 && p.Inventory.Count==22 && p.Inventory.Objects.All(i=>i.Bound));
            var tomboyRoll=Feature("TomboyStore").GetField("Roll",Hidden);object oldTomboyRoll=tomboyRoll.GetValue(null);
            try {
                tomboyRoll.SetValue(null,(Func<int,int>)(max=>0));
                EquipmentTalk(p,1002,900071727,131);EquipmentTalk(p,1000,4718,1);
                for(int i=0;i<10;i++)EquipmentKill(p,1000,16);
                Require(p.Inventory.Objects.Count(i=>i.ID==710581)==10);
                EquipmentTalk(p,1000,4718,1);
                for(int kind=0;kind<3;kind++) {
                    int selected=kind;tomboyRoll.SetValue(null,(Func<int,int>)(max=>max==3?selected:0));
                    EquipmentKill(p,1000,75);EquipmentKill(p,1000,75);
                }
                tomboyRoll.SetValue(null,(Func<int,int>)(max=>0));
                foreach(uint stone in new uint[]{721951,721952,721953})
                    ItemHandler.UseItem(p.Inventory.Objects.First(i=>i.ID==stone),p);
                Require(new uint[]{710582,710583,710584}.All(id=>p.Inventory.Objects.Count(i=>i.ID==id)==1));
                EquipmentTalk(p,1000,4718,1);EquipmentTalk(p,1000,4719,12);EquipmentTalk(p,1002,900071727,131);
            } finally {tomboyRoll.SetValue(null,oldTomboyRoll);}
            Require(EquipmentEarlyCompleted()==10 && p.Inventory.Count==25 && p.Inventory.Objects.All(i=>i.Bound));
            var bloodRoll=Feature("DesertBloodStore").GetField("Roll",Hidden);object oldBloodRoll=bloodRoll.GetValue(null);
            Action<uint,byte> cryptTalk=(npc,option)=>{
                var actor=p.Map.Npcs[npc];p.Entity.X=(ushort)(actor.X-2);p.Entity.Y=actor.Y;p.ActiveNpc=npc;
                Npcs.GetDialog(new NpcRequest(true){NpcID=npc,OptionID=option},p);
            };
            try {
                bloodRoll.SetValue(null,(Func<int,int>)(max=>0));
                EquipmentTalk(p,1002,900071727,141);EquipmentTalk(p,1000,4621,1);
                EquipmentKill(p,1000,76);ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==721843),p);
                EquipmentTalk(p,1000,4622,1);Require(p.Map.BaseID==1801);
                cryptTalk(4623,1);cryptTalk(9918011,2);
                for(int i=0;i<200;i++)EquipmentKill(p,1000,i%2==0?17u:76u);
                Require(Convert.ToInt64(Sql("SELECT kills FROM revora_desert_blood WHERE uid=@uid AND life=0","uid",(long)p.Entity.UID))==200);
                EquipmentTalk(p,1000,4622,1);cryptTalk(4623,1);cryptTalk(9918011,2);
                for(int i=0;i<5;i++)EquipmentKill(p,1000,76);
                EquipmentTalk(p,1000,4622,1);cryptTalk(4623,1);cryptTalk(9918011,2);
                EquipmentTalk(p,1000,4621,1);EquipmentTalk(p,1000,4622,1);cryptTalk(4623,1);cryptTalk(9918011,2);
                EquipmentTalk(p,1015,4633,1);ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==721844),p);
                EquipmentTalk(p,1015,4633,1);EquipmentTalk(p,1002,900071727,141);
            } finally {bloodRoll.SetValue(null,oldBloodRoll);}
            Require(EquipmentEarlyCompleted()==11 && p.Inventory.Count==28 && p.Inventory.Objects.All(i=>i.Bound));
            // Earn the spending money through the existing daily-boss reward path.
            var daily=Feature("DailyWorldBosses");var dailyClock=daily.GetField("Clock",Hidden);object oldDailyClock=dailyClock.GetValue(null);
            now=DateTime.Now.Date.AddDays(1).ToUniversalTime();ulong beforeBounty=p.Entity.Money;
            uint beforeCp=p.Entity.ConquerPoints,beforeRvp=p.Entity.RevoraPoint;
            try {
                dailyClock.SetValue(null,(Func<DateTime>)(()=>now));Call(daily,"Tick");
                now=DateTime.SpecifyKind((DateTime)Sql("SELECT min(planned) FROM revora_daily_boss_runs WHERE planned>=@now AND state=0","now",now),DateTimeKind.Utc).AddSeconds(1);
                Call(daily,"Tick");var active=daily.GetField("Active",Hidden).GetValue(null);Require(active!=null);
                var boss=(Entity)active.GetType().GetField("Entity",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(active);
                p.BindWorldMap(boss.MapID);EliminationHit(p,boss);
                Require(p.Entity.ConquerPoints==beforeCp+1000000 && p.Entity.RevoraPoint==beforeRvp+50000 && p.Entity.Money==beforeBounty+10000000);
                Require((long)Sql("SELECT count(*) FROM revora_daily_boss_runs WHERE killer=@uid AND state=2 AND paid","uid",(long)p.Entity.UID)==1);
            } finally {dailyClock.SetValue(null,oldDailyClock);}
            p.BindWorldMap(1002);uint meteorPrice=ConquerItemInformation.BaseInformations[1088001].ConquerPointsWorth;
            uint shopCp=p.Entity.ConquerPoints;Require(meteorPrice>0 && shopCp>=meteorPrice);
            ItemHandler.HandleBuyFromNPC(new ItemUsage(true){UID=2888,dwParam=1088001,dwExtraInfo=1},p);
            Require(p.Entity.ConquerPoints==shopCp-meteorPrice && p.Inventory.Objects.Count(i=>i.ID==1088001)==1);
            var heroStore=Feature("HeroWingsStore");var heroRoll=heroStore.GetField("Roll",Hidden);var heroClock=heroStore.GetField("Clock",Hidden);
            object oldHeroRoll=heroRoll.GetValue(null),oldHeroClock=heroClock.GetValue(null);now=DateTime.UtcNow;
            try {
                heroRoll.SetValue(null,(Func<int,int>)(max=>0));heroClock.SetValue(null,(Func<DateTime>)(()=>now));
                EquipmentTalk(p,1002,900071727,151);EquipmentTalk(p,1015,4700,1);EquipmentTalk(p,1015,4701,1);
                EquipmentTalk(p,1015,4700,1);EquipmentTalk(p,1015,4701,1);EquipmentTalk(p,1015,4701,1);
                Require(!p.Inventory.Objects.Any(i=>i.ID==1088001));
                for(int i=0;i<10;i++)EquipmentKill(p,1015,18);
                EquipmentTalk(p,1015,4701,1);ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==721912),p);
                EquipmentKill(p,1015,19);var sites=(ushort[,])heroStore.GetField("Sites",Hidden).GetValue(null);
                p.Entity.X=sites[0,0];p.Entity.Y=sites[0,1];ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==721909),p);
                EquipmentTalk(p,1015,4701,1);
                for(int i=0;i<5;i++){EquipmentKill(p,1015,78);now=now.AddSeconds(1);EquipmentKill(p,1015,78);}
                Require(p.Inventory.Objects.Count(i=>i.ID==721910)==5);
                EquipmentTalk(p,1015,4701,1);EquipmentTalk(p,1015,4700,1);EquipmentTalk(p,1015,4701,1);
                EquipmentTalk(p,1015,4700,1);EquipmentTalk(p,1002,900071727,151);
            } finally {heroRoll.SetValue(null,oldHeroRoll);heroClock.SetValue(null,oldHeroClock);}
            Require(EquipmentEarlyCompleted()==12 && p.Inventory.Count==31 && p.Inventory.Objects.All(i=>i.Bound));
            var family=Feature("FamilyFeudQuest");var familyStore=Feature("FamilyFeudStore");
            var familyClock=familyStore.GetField("Clock",Hidden);var familyRoll=familyStore.GetField("Roll",Hidden);
            object oldFamilyClock=familyClock.GetValue(null),oldFamilyRoll=familyRoll.GetValue(null);now=DateTime.UtcNow;
            Action<string,ushort,ushort> ownedBandit=(name,x,y)=>{
                p.BindWorldMap(1015);p.Entity.X=x;p.Entity.Y=y;now=now.AddSeconds(4);Call(family,"Tick");
                var enemy=p.Map.Entities.Values.Single(b=>!b.Dead && b.Name==name);
                EliminationHit(p,enemy);Require(ReferenceEquals(enemy.Killer,p.Entity));Call(family,"Tick");
            };
            try {
                familyClock.SetValue(null,(Func<DateTime>)(()=>now));familyRoll.SetValue(null,(Func<int,int>)(max=>0));
                EquipmentTalk(p,1002,900071727,161);EquipmentTalk(p,1015,4705,1);
                for(int i=0;i<50;i++)EquipmentKill(p,1015,55);
                ownedBandit("BanditLeaderL97",363,468);EquipmentTalk(p,1015,4705,1);
                EquipmentKill(p,1015,79);EquipmentTalk(p,1015,4705,1);
                for(int i=0;i<10;i++)EquipmentKill(p,1015,84);
                ownedBandit("BanditCourier",526,344);EquipmentTalk(p,1015,4705,1);
                for(int i=0;i<10;i++)ownedBandit("BanditDeliverer",526,344);
                EquipmentTalk(p,1015,4705,1);ownedBandit("SeniorBandit",571,415);EquipmentTalk(p,1015,4705,1);
                Require(p.Entity.Mesh==201);EquipmentTalk(p,1015,4706,1);Require(p.Map.BaseID==1801 && p.Entity.Mesh!=201);
                cryptTalk(4707,1);ushort[,] pads={{50,60},{54,64},{58,64},{60,60},{58,60}};
                for(int i=0;i<5;i++) {
                    now=now.AddSeconds(1);p.lastJumpDistance=0;
                    GeneralData.PlayerJump(new Data(true){UID=p.Entity.UID,ID=Data.Jump,wParam1=p.Entity.X,wParam2=p.Entity.Y,Xt1=pads[i,0],Yt1=pads[i,1],TimeStamp=Time32.Now},p);
                    Require(p.Entity.X==pads[i,0] && p.Entity.Y==pads[i,1]);
                    Require(Convert.ToInt64(Sql("SELECT puzzle_step FROM revora_family_visits WHERE uid=@uid AND active","uid",(long)p.Entity.UID))==i+1);
                }
                cryptTalk(4707,2);Require(p.Entity.MapID==1015);
                EquipmentTalk(p,1015,4705,1);EquipmentTalk(p,1002,900071727,161);
            } finally {familyClock.SetValue(null,oldFamilyClock);familyRoll.SetValue(null,oldFamilyRoll);}
            Require(EquipmentEarlyCompleted()==13 && p.Inventory.Count==34 && p.Inventory.Objects.All(i=>i.Bound));
            p.Entity.Teleport(1002,317,322);
            foreach(var item in p.Inventory.Objects.ToArray()) {
                var packet=new byte[172];Writer.WriteUInt16(164,0,packet);Writer.WriteUInt16(1102,2,packet);
                Writer.WriteUInt32(8,4,packet);packet[12]=1;packet[13]=10;Writer.WriteUInt32(item.UID,18,packet);
                Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,164,packet);Warehouse.Process(packet,p);
            }
            Require(p.Inventory.Count==0 && p.Warehouses[Revora.Game.ConquerStructures.Warehouse.WarehouseID.TwinCity].Count==34);
            EquipmentItems(p);
            var treasure=Feature("DesertTreasureQuest");var treasureStore=Feature("DesertTreasureStore");
            var treasureClock=treasureStore.GetField("Clock",Hidden);var huntRoll=treasureStore.GetField("HuntRoll",Hidden);var vaultRoll=treasureStore.GetField("VaultRoll",Hidden);
            object oldTreasureClock=treasureClock.GetValue(null),oldHuntRoll=huntRoll.GetValue(null),oldVaultRoll=vaultRoll.GetValue(null);
            now=DateTime.UtcNow;ulong treasureSilver=p.Entity.Money;
            try {
                treasureClock.SetValue(null,(Func<DateTime>)(()=>now));huntRoll.SetValue(null,(Func<int,int>)(max=>0));vaultRoll.SetValue(null,(Func<int,int>)(max=>0));
                EquipmentTalk(p,1002,900071727,171);EquipmentTalk(p,1000,4651,1);EquipmentKill(p,1001,20);
                p.Entity.X=90;p.Entity.Y=283;ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==721873),p);
                p.lastJumpDistance=0;GeneralData.PlayerJump(new Data(true){UID=p.Entity.UID,ID=Data.Jump,wParam1=90,wParam2=283,Xt1=91,Yt1=283,TimeStamp=Time32.Now},p);
                Require(p.Entity.X==91 && p.Entity.Y==283);
                p.ActiveNpc=9918281;Npcs.GetDialog(new NpcRequest(true){NpcID=9918281,OptionID=1},p);
                ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==721875),p);EquipmentTalk(p,1000,4651,1);
                EquipmentTalk(p,1001,4652,12);EquipmentTalk(p,1001,4652,12);Require(p.Entity.Money==treasureSilver-99699);
                ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==721878),p);EquipmentTalk(p,1001,4652,1);
                foreach(uint familyId in new uint[]{56,81,86,56,86})EquipmentKill(p,1001,familyId);
                EquipmentTalk(p,1000,4651,0);ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==721878),p);EquipmentTalk(p,1000,4651,1);
                p.BindWorldMap(1001);p.Entity.X=727;p.Entity.Y=675;now=now.AddSeconds(4);Call(treasure,"Tick");
                var jinx=p.Map.Entities.Values.Single(b=>!b.Dead && b.Name=="JinxTombBat");EliminationHit(p,jinx);
                ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==721874),p);Call(treasure,"Tick");
                Require(p.Inventory.Objects.Count(i=>i.ID==721872)==1);
                EquipmentTalk(p,1000,4651,1);EquipmentTalk(p,1002,900071727,171);Require(p.Entity.Money==treasureSilver-93499);
            } finally {treasureClock.SetValue(null,oldTreasureClock);huntRoll.SetValue(null,oldHuntRoll);vaultRoll.SetValue(null,oldVaultRoll);}
            Require(EquipmentEarlyCompleted()==14 && p.Inventory.Count==4 && p.Inventory.Objects.All(i=>i.Bound));
            Require(p.Inventory.Objects.Count(i=>i.ID==721873)==1); // Reusable Reveal Vial is not a final reward.
            var expedition=Feature("DesertExpeditionQuest");var expeditionStore=Feature("DesertExpeditionStore");
            var expeditionFights=Feature("DesertExpeditionEncounters");
            var expeditionClock=expeditionStore.GetField("Clock",Hidden);var expeditionRoll=expeditionStore.GetField("HuntRoll",Hidden);
            object oldExpeditionClock=expeditionClock.GetValue(null),oldExpeditionRoll=expeditionRoll.GetValue(null);
            now=DateTime.UtcNow;ulong expeditionSilver=p.Entity.Money;
            try {
                expeditionClock.SetValue(null,(Func<DateTime>)(()=>now));expeditionRoll.SetValue(null,(Func<int,int>)(max=>0));
                EquipmentTalk(p,1002,900071727,181);EquipmentTalk(p,1000,4702,1);
                p.BindWorldMap(1001);p.Entity.X=173;p.Entity.Y=329;uint initialHp=p.Entity.Hitpoints;
                Require(initialHp>0);DateTime activated=now;
                ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==721920),p);
                for(int i=0;i<10;i++){now=now.AddSeconds(2);EquipmentKill(p,1001,57);}
                Require(p.Inventory.Objects.Count(i=>i.ID==710259)==10);
                EquipmentTalk(p,1000,4702,1);Require(p.Inventory.Objects.Count(i=>i.ID==710259)==10);
                now=activated.AddSeconds(60);Call(expedition,"Tick");
                Require(Convert.ToInt64(Sql("SELECT pill_paid FROM revora_desert_expedition WHERE uid=@uid AND life=0","uid",(long)p.Entity.UID))==6);
                Require(p.Entity.Hitpoints>0 && p.Entity.Hitpoints<initialHp);
                EquipmentTalk(p,1000,4702,1);ushort[,] traps={{172,399},{102,292},{109,369}};
                for(int site=0;site<3;site++) {
                    p.BindWorldMap(1001);p.Entity.X=traps[site,0];p.Entity.Y=traps[site,1];now=now.AddSeconds(2);
                    ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==721921),p);
                    var bull=p.Map.Entities.Values.Single(b=>b.MonsterInfo!=null && b.MonsterInfo.ID==9918350 && !b.Dead);
                    p.Entity.X=(ushort)(traps[site,0]-3);
                    var lureUntil=DateTime.UtcNow.AddSeconds(2);
                    while(Kernel.GetDistance(bull.X,bull.Y,traps[site,0],traps[site,1])>1 && DateTime.UtcNow<lureUntil) {
                        bull.MonsterInfo.LastMove=new Time32(1);Call(expeditionFights,"DriveOwned",p,bull,Time32.Now);
                        System.Threading.Thread.Sleep(10);
                    }
                    Require(Kernel.GetDistance(bull.X,bull.Y,traps[site,0],traps[site,1])<=1);
                    EliminationHit(p,bull);Call(expedition,"Tick");Require(p.Inventory.Objects.Count(i=>i.ID==710261)==site+1);
                }
                EquipmentTalk(p,1000,4702,1);p.BindWorldMap(1001);p.Entity.X=185;p.Entity.Y=359;
                Entity master=null;var spawnUntil=DateTime.UtcNow.AddSeconds(2);
                do {
                    now=now.AddSeconds(2);Call(expedition,"Tick");
                    master=p.Map.Entities.Values.SingleOrDefault(b=>b.MonsterInfo!=null && b.MonsterInfo.ID==9918351 && !b.Dead);
                    if(master==null)System.Threading.Thread.Sleep(10);
                } while(master==null && DateTime.UtcNow<spawnUntil);
                Require(master!=null);
                EliminationHit(p,master);Call(expedition,"Tick");Require(p.Inventory.Objects.Count(i=>i.ID==710260)==1);
                EquipmentTalk(p,1000,4702,1);EquipmentTalk(p,1002,900071727,181);
                Require(p.Entity.Money==expeditionSilver+6300);
            } finally {expeditionClock.SetValue(null,oldExpeditionClock);expeditionRoll.SetValue(null,oldExpeditionRoll);}
            Require(EquipmentEarlyCompleted()==15 && p.Inventory.Count==7 && p.Inventory.Objects.All(i=>i.Bound));
            p.BindWorldMap(1002);shopCp=p.Entity.ConquerPoints;
            ItemHandler.HandleBuyFromNPC(new ItemUsage(true){UID=2888,dwParam=1088001,dwExtraInfo=1},p);
            Require(p.Entity.ConquerPoints==shopCp-meteorPrice && p.Inventory.Objects.Count(i=>i.ID==1088001)==1);
            var power=Feature("PowerBookQuest");var powerStore=Feature("PowerBookStore");
            var powerClock=powerStore.GetField("Clock",Hidden);var powerRoll=powerStore.GetField("Roll",Hidden);
            object oldPowerClock=powerClock.GetValue(null),oldPowerRoll=powerRoll.GetValue(null);now=DateTime.UtcNow;
            Action powerPulse=()=>{now=now.AddSeconds(3);Call(power,"Tick");};
            Action<uint> powerUse=id=>ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==id),p);
            Action powerGate=()=>{p.Entity.X=140;p.Entity.Y=140;powerUse(721924);};
            Action<int,bool> powerKill=(kind,herd)=>{
                Func<Entity> select=()=>p.Map.Entities.Values.Where(b=>!b.Dead && b.MonsterInfo!=null && b.MonsterInfo.ID==9918400+kind)
                    .OrderBy(b=>herd?Kernel.GetDistance(b.X,b.Y,77,23):0).ThenBy(b=>b.UID).FirstOrDefault();
                var body=select();if(body==null){powerPulse();body=select();}Require(body!=null);
                if(herd) {
                    // Supply player positions and timing, but let ordinary monster movement earn the lure.
                    for(int step=0;step<120 && Kernel.GetDistance(body.X,body.Y,77,23)>3;step++) {
                        p.Entity.X=(ushort)(body.X+Math.Sign(77-body.X)*Math.Min(4,Math.Abs(77-body.X)));
                        p.Entity.Y=(ushort)(body.Y+Math.Sign(23-body.Y)*Math.Min(4,Math.Abs(23-body.Y)));
                        body.MonsterInfo.LastMove=new Time32(1);
                        typeof(Screen).GetMethod("DriveMonsterAt",Hidden).Invoke(null,new object[]{body,p.Entity,Time32.Now});
                    }
                    Require(Kernel.GetDistance(body.X,body.Y,77,23)<=3);
                }
                EliminationHit(p,body);
                Require(!(bool)typeof(Entity).GetField("PersistenceRequiresReload",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(p.Entity));
            };
            Func<int> powerStage=()=>Convert.ToInt32(Sql("SELECT stage FROM revora_power_book WHERE uid=@uid AND life=0","uid",(long)p.Entity.UID));
            try {
                powerClock.SetValue(null,(Func<DateTime>)(()=>now));powerRoll.SetValue(null,(Func<int,int>)(max=>0));
                EquipmentTalk(p,1002,900071727,41);EquipmentTalk(p,1002,900071841,1);EquipmentTalk(p,1002,900071841,1);
                Require(powerStage()==2 && !p.Inventory.Objects.Any(i=>i.ID==1088001));
                EquipmentTalk(p,1002,900071841,2);Require(p.Map.BaseID==1500);
                for(int i=0;i<20;i++)powerKill(0,false);Require(powerStage()==3);powerPulse();
                for(int i=0;i<10;i++)powerKill(1,true);Require(powerStage()==4);powerPulse();
                for(int i=0;i<15;i++){powerKill(0,false);powerKill(1,false);}powerGate();Require(powerStage()==5 && p.Map.BaseID==1501);
                powerKill(2,false);powerKill(2,false);Require(powerStage()==6);powerPulse();powerKill(3,false);powerUse(721926);Require(powerStage()==7);powerPulse();
                for(int i=0;i<10;i++){powerKill(2,false);powerKill(3,false);}powerGate();Require(powerStage()==8 && p.Map.BaseID==1502);
                powerKill(4,false);powerKill(4,false);Require(powerStage()==9);powerPulse();powerKill(5,false);
                p.Entity.X=100;p.Entity.Y=66;powerUse(721928);powerUse(721929);Require(powerStage()==10);powerPulse();
                for(int i=0;i<5;i++){powerKill(4,false);powerKill(5,false);}powerGate();Require(powerStage()==11 && p.Map.BaseID==1503);
                for(int i=0;i<10;i++){powerKill(6,false);powerUse(721931);}Require(powerStage()==12);
                p.Entity.X=77;p.Entity.Y=27;powerUse(721924);Require(p.Entity.MapID==1002);
                EquipmentTalk(p,1002,900071841,3);EquipmentTalk(p,1002,900071727,43);
                Require(!p.Inventory.Objects.Any(i=>i.ID==721715 || i.ID==721924 || i.ID==721931));
            } finally {powerClock.SetValue(null,oldPowerClock);powerRoll.SetValue(null,oldPowerRoll);}
            Require(EquipmentEarlyCompleted()==16 && p.Inventory.Count==9 && p.Inventory.Objects.All(i=>i.Bound));
            EquipmentFinalChains(p);
            Require(EntityTable.SaveEntity(p));
            Sql("CREATE TABLE revora_equipment_journey_probe(uid bigint PRIMARY KEY,items text NOT NULL,receipts text NOT NULL)");
            Sql("INSERT INTO revora_equipment_journey_probe VALUES(@uid,@items,@receipts)","uid",(long)p.Entity.UID,"items",EquipmentItems(p),"receipts",EquipmentEarlyFingerprint());
        }
        Require(EquipmentEarlyCompleted()==19);
        EquipmentTalk(p,1002,900071727,51);EquipmentTalk(p,1002,900071727,61);EquipmentTalk(p,1002,900071727,1);EquipmentTalk(p,1002,900071727,71);
        EquipmentTalk(p,1002,900071727,81);
        EquipmentTalk(p,1002,900071727,91);
        EquipmentTalk(p,1002,900071727,101);EquipmentTalk(p,1002,900071727,111);
        EquipmentTalk(p,1002,900071727,121);
        EquipmentTalk(p,1002,900071727,131);
        EquipmentTalk(p,1002,900071727,141);
        EquipmentTalk(p,1002,900071727,151);
        EquipmentTalk(p,1002,900071727,161);
        EquipmentTalk(p,1002,900071727,171);
        EquipmentTalk(p,1002,900071727,181);
        EquipmentTalk(p,1002,900071841,3);EquipmentTalk(p,1002,900071727,43);
        TowerTalk(p,4565,1);FanTalk(p,4565,81);TempestTalk(p,10850,3);
        Require(EquipmentItems(p)==(string)Sql("SELECT items FROM revora_equipment_journey_probe"));
        Require(EquipmentEarlyFingerprint()==(string)Sql("SELECT receipts FROM revora_equipment_journey_probe"));
        new TitleStorage().Login(p);
        Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10931 AND source='equipment-quests-revora-v1' AND source_key='nineteen-chains-cross-life' AND rule_version=1","uid",(long)p.Entity.UID)==1);
        const int aggregateBit=(10931/100-101)*32+10931%100-1;
        Require((System.BitConverter.ToUInt32(p.Entity.MyAchievement.ToArray(),16+aggregateBit/32*4)&(1u<<(aggregateBit%32)))!=0);
        System.Console.WriteLine("[equipment-journey] "+(restoring?"equipment-early-reload":"equipment-early")+"-passed");
        Server.CommandsAI("@close");throw new Exception("Equipment journey did not close.");
    }
    static void EquipmentMapCollectionChecks() {
        var map=(Map)Call(typeof(Map),"CreateDynamic",(ushort)1002,"maps\\1002.map",false);
        Func<uint,ushort,Entity> body=(uid,x)=>{
            var info=new MonsterInformation{ID=1,Name="Pheasant"};
            var actor=new Entity(EntityFlag.Monster,false){UID=uid,MapID=map.ID,X=x,Y=100,Name="CollectionProbe",MapObjType=MapObjectType.Monster,MonsterInfo=info};
            info.Owner=actor;actor.MaxHitpoints=actor.Hitpoints=10;return actor;
        };
        try {
            var anchor=body(590000,100);map.AddEntity2(anchor);
            // Reproduce the old collection's deterministic mutation failure.
            var old=new System.Collections.Generic.SafeDictionary<uint,Entity>();old.Add(anchor.UID,anchor);
            var oldReader=old.Values.GetEnumerator();Require(oldReader.MoveNext());old.Add(590001,body(590001,101));
            bool invalidated=false;try{oldReader.MoveNext();}catch(InvalidOperationException){invalidated=true;}Require(invalidated);
            var snapshot=map.Entities.Values;var reader=snapshot.GetEnumerator();Require(reader.MoveNext());
            var extra=body(590001,101);map.AddEntity2(extra);Require(!reader.MoveNext() && snapshot.Count==1 && map.Entities.Count==2);
            map.RemoveEntity(extra);map.AddEntity2(body(anchor.UID,102));Require(ReferenceEquals(map.Entities[anchor.UID],anchor));
            var replacement=body(anchor.UID,100);map.Entities[anchor.UID]=replacement;map.RemoveEntity(anchor);
            Require(ReferenceEquals(map.Entities[anchor.UID],replacement));anchor=replacement;
            Require(map.Entities[599999]==null);
            using(var start=new System.Threading.ManualResetEventSlim(false)) {
                var work=Enumerable.Range(0,4).Select(worker=>System.Threading.Tasks.Task.Factory.StartNew(()=>{
                    start.Wait();
                    for(int i=0;i<2000;i++) {
                        if(worker<2) {
                            var actor=body((uint)(591000+worker*100+i%16),(ushort)(110+worker*20+i%16));
                            map.AddEntity2(actor);map.RemoveEntity(actor);
                        } else {
                            foreach(var actor in map.Entities.Values)Require(actor!=null);
                            Require(ReferenceEquals(Revora.Game.Features.CityGuardSystem.FindNearbyMonster(map.ID,100,100,40),anchor));
                            var optional=map.Entities[591000];Require(optional==null || optional.UID==591000);
                        }
                    }
                },System.Threading.Tasks.TaskCreationOptions.LongRunning)).ToArray();
                start.Set();System.Threading.Tasks.Task.WaitAll(work);
            }
            Require(map.Entities.Count==1 && ReferenceEquals(map.Entities[anchor.UID],anchor));
            map.Entities.Clear();Require(map.Entities.Count==0 && snapshot.Count==1 && map.Entities[anchor.UID]==null);
            System.Console.WriteLine("[equipment-journey] map-snapshot-concurrency-passed");
        } finally {map.Dispose();}
    }
    static void EquipmentFinalChains(GameState p) {
        var towerClock=Feature("DefenseTalismanStore").GetField("Clock",Hidden);object oldTowerClock=towerClock.GetValue(null);
        towerNow=DateTime.UtcNow;
        try {
            towerClock.SetValue(null,(Func<DateTime>)(()=>towerNow));
            TowerTalk(p,4565,1);TowerTalk(p,4565,1);Require(p.Map.BaseID==1782);
            p.Entity.X=40;p.Entity.Y=57;TowerTick();for(int i=0;i<5;i++)TowerKill(p);
            TowerTalk(p,4567,1);TowerTalk(p,4566,1);Require(TowerStage()==3);
            for(int i=0;i<3;i++){TowerTalk(p,4579,1);p.Entity.X=310;p.Entity.Y=380;TowerUse(p,721779);TowerKill(p);}
            TowerTalk(p,4569,1);TowerTalk(p,4575,1);TowerTalk(p,4576,1);TowerTalk(p,4577,1);Require(TowerStage()==4 && p.Map.BaseID==1784);
            TowerTalk(p,4568,1);TowerTalk(p,4568,2);p.BindWorldMap(1015);p.Entity.X=400;p.Entity.Y=650;
            towerNow=towerNow.AddMinutes(10-towerNow.Minute%10);TowerTick();TowerKill(p);TowerTalk(p,4577,1);TowerTalk(p,4568,1);Require(TowerStage()==5);
            TowerTalk(p,4572,1);p.Entity.X=166;p.Entity.Y=226;TowerTick();TowerKill(p);TowerKill(p);
            TowerTalk(p,4566,1);TowerTalk(p,4566,1);Require(TowerStage()==7 && p.Map.BaseID==1783);
            p.Entity.X=40;p.Entity.Y=57;TowerTick();for(int i=0;i<5;i++)TowerKill(p);
            TowerTalk(p,4574,1);TowerTalk(p,4573,1);Require(TowerStage()==8 && p.Map.BaseID==1785);
            ushort[,] stands={{96,124},{70,64},{113,126},{62,85}};
            for(int i=0;i<4;i++){p.Entity.X=stands[i,0];p.Entity.Y=stands[i,1];TowerTick();TowerKill(p);if(i==1 || i==3){p.Entity.X=(ushort)(i==1?70:62);p.Entity.Y=(ushort)(i==1?56:77);TowerUse(p,721786);}}
            Require(p.Map.BaseID==1787);TowerTick();TowerKill(p);
            for(byte i=10;i<14;i++)TowerTalk(p,4578,i);TowerTalk(p,4578,1);TowerTalk(p,4565,1);
            Require(EquipmentEarlyCompleted()==17 && p.Inventory.Count==11);
            Require((long)Sql("SELECT count(*) FROM revora_defense_talisman_purchases WHERE uid=@uid AND price=30000","uid",(long)p.Entity.UID)==3);
        } finally {towerClock.SetValue(null,oldTowerClock);}
        var fanStore=Feature("AttackTalismanStore");var fanClock=fanStore.GetField("Clock",Hidden);var fanRoll=fanStore.GetField("HuntRoll",Hidden);
        object oldFanClock=fanClock.GetValue(null),oldFanRoll=fanRoll.GetValue(null);fanNow=DateTime.UtcNow;ulong beforeFan=p.Entity.Money;
        try {
            fanClock.SetValue(null,(Func<DateTime>)(()=>fanNow));fanRoll.SetValue(null,(Func<int,int>)(max=>0));
            FanTalk(p,4565,80);FanTalk(p,4565,81);FanTalk(p,4582,1);FanTalk(p,4581,1);FanTalk(p,4581,1);
            Require(FanStage()==2 && p.Inventory.Objects.Count(i=>i.ID==721800)==1 && p.Entity.Money==beforeFan+100-10000);
            for(int i=0;i<5;i++)EquipmentKill(p,1011,6);
            Require(p.Inventory.Objects.Count(i=>i.ID==710220)==5);
            FanTalk(p,4582,1);FanTalk(p,4565,81);FanTalk(p,4583,1);FanTalk(p,4583,2);Require(FanStage()==5 && p.Map.BaseID==1025);
            p.Entity.X=54;p.Entity.Y=50;FanKill(p);p.Entity.X=58;p.Entity.Y=50;FanKill(p);TowerUse(p,721806);Require(p.Entity.MapID==1000);
            FanTalk(p,4583,1);TowerUse(p,721802);TowerUse(p,721804);FanTalk(p,4582,1);Require(FanStage()==7);
            p.Entity.X=790;p.Entity.Y=460;for(int i=0;i<5;i++)FanKill(p);FanTalk(p,4582,1);Require(FanStage()==8);
            FanTalk(p,9910430,1);p.Entity.X=475;p.Entity.Y=844;TowerUse(p,721799);FanTalk(p,9910401,1);p.Entity.X=477;p.Entity.Y=844;FanKill(p);
            FanTalk(p,4582,1);int site=Convert.ToInt32(Sql("SELECT courtesan_site FROM revora_attack_talisman WHERE uid=@uid","uid",(long)p.Entity.UID));
            var points=(ushort[,])fanStore.GetField("CourtesanSites",Hidden).GetValue(null);p.BindWorldMap(1015);p.Entity.X=(ushort)(points[site,0]-2);p.Entity.Y=points[site,1];FanKill(p);
            for(uint npc=9910410;npc<9910414;npc++)FanTalk(p,npc,1);FanTalk(p,9910414,1);Require(FanStage()==10 && p.Map.BaseID==3937);
            int winning=Convert.ToInt32(Sql("SELECT box_winner FROM revora_attack_talisman WHERE uid=@uid","uid",(long)p.Entity.UID)),wrong=(winning+1)%3;
            FanTalk(p,9910420u+(uint)wrong,1);p.Entity.X=(ushort)(52+wrong*6);p.Entity.Y=53;FanKill(p);
            FanTalk(p,9910420u+(uint)winning,1);FanTalk(p,9910423,1);FanTalk(p,4565,81);
            Require(EquipmentEarlyCompleted()==18 && p.Inventory.Count==13 && p.Entity.Money==beforeFan-20000+900);
        } finally {fanClock.SetValue(null,oldFanClock);fanRoll.SetValue(null,oldFanRoll);}
        uint openingCp=p.Entity.ConquerPoints,price=ConquerItemInformation.BaseInformations[1088001].ConquerPointsWorth;Require(price>0);
        for(int pack=0;pack<5;pack++) {
            p.BindWorldMap(1002);ItemHandler.HandleBuyFromNPC(new ItemUsage(true){UID=2888,dwParam=1088001,dwExtraInfo=10},p);
            Require(p.Inventory.Objects.Where(i=>i.ID==1088001).Sum(i=>(int)i.StackSize)==10);
            TempestTalk(p,11275,0);TempestTalk(p,11275,1);Require(p.Inventory.Objects.Count(i=>i.ID==720027)==pack+1);
        }
        Require(p.Entity.ConquerPoints==openingCp-50*price);
        TempestTalk(p,8796,1);TempestTalk(p,10850,1);Require(p.Map.BaseID==1100 && p.Inventory.Count==13);
        var until=DateTime.UtcNow.AddSeconds(4);
        while(p.Map.Entities.Count==0 && DateTime.UtcNow<until){Call(Feature("TempestWingsQuest"),"Tick");System.Threading.Thread.Sleep(50);}
        EliminationHit(p,p.Map.Entities.Values.Single(e=>e.MonsterInfo!=null && e.MonsterInfo.ID==9905500));Call(Feature("TempestWingsQuest"),"Tick");
        Require(p.Inventory.Objects.Count(i=>i.ID==3006392)==1);TempestTalk(p,9910500,1);
        Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10931","uid",(long)p.Entity.UID)==0);
        // Set the normal NPC context once, then race final claims without racing test teleports.
        TempestTalk(p,10850,0);
        System.Threading.Tasks.Parallel.For(0,4,i=>Npcs.GetDialog(new NpcRequest(true){NpcID=10850,OptionID=3},p));
        Require(EquipmentEarlyCompleted()==19 && p.Inventory.Count==15 && p.Inventory.Objects.All(i=>i.Bound));
        Require((long)Sql("SELECT sum(spent) FROM revora_tempest_payments WHERE uid=@uid","uid",(long)p.Entity.UID)==5);
        Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10931","uid",(long)p.Entity.UID)==1);
    }
}
