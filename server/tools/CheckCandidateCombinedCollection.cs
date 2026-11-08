using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Store=Revora.Game.ConquerStructures.Warehouse;
using Console=System.Console;

internal static partial class CheckCandidateArenaLifecycle
{
    static bool combinedCollectionFixture;
    static void CollectionSword(bool reload)
    {
        nextUid=FirstUid;var p=CollectionPlayer();SkillTable.LoadSpells(p);SkillTable.LoadProficiencies(p);p.LoadItemStats();
        Require((long)Sql("SELECT count(*) FROM revora_collection_realms_probe WHERE uid=@uid AND snapshot IS NOT NULL","uid",(long)FirstUid)==1);
        if((bool)Sql("SELECT to_regclass('revora_collection_sword_probe') IS NULL")){
            Require(!reload && CollectionRealmsSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_collection_realms_probe WHERE uid=@uid","uid",(long)FirstUid)));
            Sql("CREATE TABLE revora_collection_sword_probe(uid bigint PRIMARY KEY,weapon bigint,level integer,xp bigint,snapshot text);INSERT INTO revora_collection_sword_probe(uid) VALUES(@uid)","uid",(long)FirstUid);
        }
        if(reload){
            Require(CollectionRealmsSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_collection_sword_probe WHERE uid=@uid","uid",(long)FirstUid)));
            Require(p.Proficiencies.ContainsKey(410) && p.Proficiencies[410].Level>=12);
            Require((long)Sql("SELECT count(*) FROM revora_collection_sword_probe WHERE uid=@uid AND level=@level AND xp=@xp","uid",(long)FirstUid,"level",(int)p.Proficiencies[410].Level,"xp",(long)p.Proficiencies[410].Experience)==1);
        } else {
            p.BindWorldMap(1002);var smith=p.Map.Npcs[5];p.Entity.X=smith.X;p.Entity.Y=(ushort)(smith.Y+2);
            var weapon=p.Inventory.Objects.Concat(p.Equipment.GetItems()).SingleOrDefault(i=>i.ID==410301);
            if(weapon==null){
                ulong before=p.Entity.Money;uint price=ConquerItemInformation.BaseInformations[410301].GoldWorth;Require(price>0);
                var packet=new ItemUsage(true){UID=5,dwParam=410301,dwExtraInfo=1,Action=ItemUsage.Mode.BuyFromNPC}.ToArray();
                Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,packet.Length-8,packet);ItemUsage.Process(packet,p);
                weapon=p.Inventory.Objects.Single(i=>i.ID==410301);Require(p.Entity.Money==before-price);
                Sql("UPDATE revora_collection_sword_probe SET weapon=@weapon WHERE uid=@uid","weapon",(long)weapon.UID,"uid",(long)FirstUid);
            }
            if(weapon.Position!=4)ItemHandler.EquipItem(new ItemUsage(true){UID=weapon.UID,dwParam=4,Action=ItemUsage.Mode.EquipItem},p);
            Require(p.Equipment.TryGetItem(4).UID==weapon.UID);p.LoadItemStats();
            int swings=0;var until=DateTime.UtcNow.AddSeconds(90);
            var targets=Kernel.Maps.Values.SelectMany(m=>m.Entities.Values).Where(e=>!e.Dead && e.MonsterInfo!=null && !e.MonsterInfo.Boss && !e.MonsterInfo.Guard && !e.MonsterInfo.Reviver && !e.Companion).OrderByDescending(e=>e.Hitpoints).Take(128).ToArray();
            foreach(var target in targets){
                p.BindWorldMap(target.MapID);p.Entity.X=target.X;p.Entity.Y=target.Y;p.Screen.Add(target);int stalled=0;
                while(!target.Dead && !p.Entity.Dead && DateTime.UtcNow<until && swings<50000){
                    if(p.Proficiencies.ContainsKey(410) && p.Proficiencies[410].Level>=12)break;
                    uint hp=target.Hitpoints;p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);
                    Attack.HandleAttack(new Attack(true){Attacker=p.Entity.UID,Attacked=target.UID,X=target.X,Y=target.Y,AttackType=Attack.Melee},p);swings++;
                    if(target.Hitpoints==hp)stalled++;else stalled=0;if(stalled>=32)break;
                }
                if(p.Entity.Dead || DateTime.UtcNow>=until || swings>=50000 || p.Proficiencies.ContainsKey(410) && p.Proficiencies[410].Level>=12)break;
            }
            SkillTable.SaveProficiencies(p);p.BindWorldMap(1002);Require(EntityTable.SaveEntity(p));
            Console.WriteLine("[collection-sword] swings="+swings+" level="+(p.Proficiencies.ContainsKey(410)?p.Proficiencies[410].Level:0)+" xp="+(p.Proficiencies.ContainsKey(410)?p.Proficiencies[410].Experience:0));
            Require(p.Proficiencies.ContainsKey(410) && p.Proficiencies[410].Level>=12);
        }
        p.Entity.MyAchievement.GetType().GetMethod("Invalidate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p.Entity.MyAchievement,null);p.Entity.MyAchievement.Send();
        Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10323","uid",(long)FirstUid)==1);
        int masteryBit=(10323/100-101)*32+10323%100-1;
        Require((System.BitConverter.ToUInt32(p.Entity.MyAchievement.ToArray(),16+masteryBit/32*4)&(1u<<(masteryBit%32)))!=0);
        Require((bool)Sql(@"SELECT
(@now::jsonb->'collection'->'collection'->'items') @> (snapshot::jsonb->'collection'->'collection'->'items') AND
@now::jsonb->'hunts'=snapshot::jsonb->'hunts' AND @now::jsonb->'travel'=snapshot::jsonb->'travel' AND
@now::jsonb->'collection'->'exchange'=snapshot::jsonb->'collection'->'exchange' AND
@now::jsonb->'collection'->'planet'=snapshot::jsonb->'collection'->'planet'
FROM revora_collection_realms_probe WHERE uid=@uid","now",CollectionRealmsSnapshot(p),"uid",(long)FirstUid));
        Require(CombinedCount(p,"Garments")==200 && CombinedCount(p,"Mounts")==100);CombinedTiers(p);
        if(!reload)Sql("UPDATE revora_collection_sword_probe SET level=@level,xp=@xp,snapshot=@snapshot WHERE uid=@uid","level",(int)p.Proficiencies[410].Level,"xp",(long)p.Proficiencies[410].Experience,"snapshot",CollectionRealmsSnapshot(p),"uid",(long)FirstUid);
        Console.WriteLine("[collection-process] collection-sword-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Collection sword did not close.");
    }
    static string CollectionRealmsSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object(
'collection',@collection::json,
'hunts',(SELECT json_agg(r ORDER BY map,week) FROM revora_forgotten_realms r WHERE uid=@uid),
'travel',(SELECT json_agg(r ORDER BY map_id) FROM revora_travel_visits r WHERE uid=@uid AND map_id BETWEEN 1360 AND 1364),
'achievements',(SELECT json_agg(r ORDER BY achievement_id) FROM revora_achievement_evidence r WHERE uid=@uid AND achievement_id IN(10723,10724,10725,10726,10727)))::text",
        "collection",CombinedSnapshot(p),"uid",(long)p.Entity.UID));}
    static void CollectionRealms(bool reload)
    {
        nextUid=FirstUid;var p=CollectionPlayer();
        Require(Convert.ToInt32(Sql("SELECT stage FROM revora_collection_combined_probe WHERE uid=@uid","uid",(long)FirstUid))==3);
        Feature("ForgottenRealms").GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>planetNow));
        if((bool)Sql("SELECT to_regclass('revora_collection_realms_probe') IS NULL")){
            Require(!reload && CombinedSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_collection_combined_probe WHERE uid=@uid","uid",(long)FirstUid)));
            Require((long)Sql("SELECT count(*) FROM revora_forgotten_realms WHERE uid=@uid","uid",(long)FirstUid)==0);
            Sql("CREATE TABLE revora_collection_realms_probe(uid bigint PRIMARY KEY,snapshot text);INSERT INTO revora_collection_realms_probe(uid) VALUES(@uid)","uid",(long)FirstUid);
        }
        Require((long)Sql("SELECT count(*) FROM revora_collection_realms_probe WHERE uid=@uid","uid",(long)FirstUid)==1);
        if(reload)Require(CollectionRealmsSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_collection_realms_probe WHERE uid=@uid","uid",(long)FirstUid)));
        foreach(int index in new[]{0,1,2,4}){
            ushort map=(ushort)(1360+index);int achievement=10723+index;
            p.BindWorldMap(1002);HeartTalk(p,900000016,(byte)(11+index));
            Require(p.Entity.MapID==map && p.Account.State==AccountTable.AccountState.Player && !(bool)typeof(Entity).GetField("PersistenceRequiresReload",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(p.Entity));
            Require((long)Sql("SELECT count(*) FROM revora_travel_visits WHERE uid=@uid AND map_id=@map AND arrival=3","uid",(long)FirstUid,"map",(int)map)==1);
            if(!reload){
                int kills=Convert.ToInt32(Sql("SELECT kills FROM revora_forgotten_realms WHERE uid=@uid AND map=@map","uid",(long)FirstUid,"map",(int)map));
                var mobs=p.Map.Entities.Values.Where(m=>m.MonsterInfo!=null && m.MonsterInfo.ID==9926300+index && !m.Dead).Take(10-kills).ToArray();
                Require(mobs.Length==10-kills);foreach(var mob in mobs)EliminationHit(p,mob);
            }
            Require(Convert.ToInt32(Sql("SELECT kills FROM revora_forgotten_realms WHERE uid=@uid AND map=@map","uid",(long)FirstUid,"map",(int)map))==10);
            HeartTalk(p,900000196,2);string settled=CollectionRealmsSnapshot(p);HeartTalk(p,900000196,2);Require(CollectionRealmsSnapshot(p)==settled);
            uint gift=Convert.ToUInt32(Sql("SELECT reward_uid FROM revora_forgotten_realms WHERE uid=@uid AND map=@map","uid",(long)FirstUid,"map",(int)map));
            Require(p.Inventory.Objects.Any(i=>i.UID==gift && i.ID==4030101 && i.Bound));
            p.Entity.MyAchievement.GetType().GetMethod("Invalidate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p.Entity.MyAchievement,null);p.Entity.MyAchievement.Send();int bit=(achievement/100-101)*32+achievement%100-1;
            Require((System.BitConverter.ToUInt32(p.Entity.MyAchievement.ToArray(),16+bit/32*4)&(1u<<(bit%32)))!=0);
            HeartTalk(p,900000196,1);Require(p.Entity.MapID==1002 && p.Entity.X==319 && p.Entity.Y==299);
            Console.WriteLine("[collection-realms] earned-map="+map+" reload="+reload);
        }
        HeartTalk(p,900000016,14);Require(p.Entity.MapID==1002);
        Require((long)Sql("SELECT count(*) FROM revora_forgotten_realms WHERE uid=@uid AND map=1363","uid",(long)FirstUid)==0);
        Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10726","uid",(long)FirstUid)==0);
        // Every original collection item and earning receipt remains intact. Combat may add normal rewards.
        Require((bool)Sql(@"SELECT
(@now::jsonb->'collection'->'items') @> (snapshot::jsonb->'collection'->'items') AND
@now::jsonb->'exchange'=snapshot::jsonb->'exchange' AND @now::jsonb->'planet'=snapshot::jsonb->'planet' AND
@now::jsonb->'tiers'=snapshot::jsonb->'tiers'
FROM revora_collection_combined_probe WHERE uid=@uid","now",CombinedSnapshot(p),"uid",(long)FirstUid));
        Require(CombinedCount(p,"Garments")==200 && CombinedCount(p,"Mounts")==100);
        CollectionMemory(p);CombinedTiers(p);
        if(reload)Require(CollectionRealmsSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_collection_realms_probe WHERE uid=@uid","uid",(long)FirstUid)));
        else Sql("UPDATE revora_collection_realms_probe SET snapshot=@snapshot WHERE uid=@uid","snapshot",CollectionRealmsSnapshot(p),"uid",(long)FirstUid);
        Console.WriteLine("[collection-process] collection-realms-"+(reload?"reload":"earn")+"-passed");
        Server.CommandsAI("@close");throw new InvalidOperationException("Collection expedition did not close.");
    }
    static void PrepareCombinedCollection(bool reload)
    {
        combinedCollectionFixture=true;
        if((bool)Sql("SELECT to_regclass('revora_collection_combined_probe') IS NULL")){
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)FirstUid)==0);
            planetNow=DateTime.UtcNow.AddDays(7);
            while(Call(Feature("PlanetWeekend"),"Current",planetNow)==null)planetNow=planetNow.AddDays(1);
            Sql(@"CREATE TABLE revora_collection_combined_probe(uid bigint PRIMARY KEY,stamp timestamptz NOT NULL,stage integer NOT NULL DEFAULT 0,
sign_start bigint,sign_day integer NOT NULL DEFAULT 0,snapshot text);
INSERT INTO revora_collection_combined_probe(uid,stamp) VALUES(@uid,@stamp)","uid",(long)FirstUid,"stamp",new DateTimeOffset(planetNow));
        }
        Require((long)Sql("SELECT count(*) FROM revora_collection_combined_probe WHERE uid=@uid","uid",(long)FirstUid)==1);
        planetNow=DateTime.SpecifyKind((DateTime)Sql("SELECT stamp AT TIME ZONE 'UTC' FROM revora_collection_combined_probe WHERE uid=@uid","uid",(long)FirstUid),DateTimeKind.Utc);
        foreach(string name in new[]{"PlanetWeekend","SummerHunt"})Feature(name).GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>planetNow));
        GameAssembly.GetType("Revora.TitleRequirements",true).GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>planetNow));
    }
    static uint[] CombinedCatalog(string category){return (uint[])Feature("CollectionAchievementCatalog").GetField(category,Hidden).GetValue(null);}
    static bool CombinedOwns(GameState p,uint id){return p.Entity.StorageItems.Values.Any(i=>i.ID==id && i.Days==0);}
    static int CombinedCount(GameState p,string category){return p.Entity.StorageItems.Values.Select(i=>i.ID).Distinct().Intersect(CombinedCatalog(category)).Count();}
    static void CombinedBuy(GameState p,uint id){
        var packet=new ItemUsage(true){UID=2888,dwParam=id,dwExtraInfo=1,ID=(uint)ItemUsage.Mode.BuyFromNPC}.ToArray();
        Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,packet.Length-8,packet);ItemUsage.Process(packet,p);
    }
    static void CombinedDeposit(GameState p){
        var catalog=CombinedCatalog("Garments").Concat(CombinedCatalog("Mounts")).ToArray();
        foreach(var item in p.Inventory.Objects.Where(i=>catalog.Contains(i.ID)).ToArray())CollectionDeposit(p,item);
    }
    static void CombinedStash(GameState p){
        var sites=new Dictionary<uint,ushort>{{8,1002},{10012,1011},{10028,1020},{10011,1000},{10027,1015},{44,1002}};
        foreach(var item in p.Inventory.Objects.ToArray()){
            Require(item.Days==0);
            var slot=p.Warehouses.OrderBy(w=>(uint)w.Key).First(w=>sites.ContainsKey((uint)w.Key) && w.Value.Count<w.Value.MaxCount);
            uint id=(uint)slot.Key;var map=Kernel.Maps[sites[id]];var npc=map.Npcs[id];p.Entity.Teleport(map.ID,(ushort)(npc.X-1),npc.Y);
            var packet=new byte[172];Revora.Network.Writer.WriteUInt16(164,0,packet);Revora.Network.Writer.WriteUInt16(1102,2,packet);
            Revora.Network.Writer.WriteUInt32(id,4,packet);packet[12]=1;packet[13]=10;Revora.Network.Writer.WriteUInt32(item.UID,18,packet);
            Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,164,packet);Warehouse.Process(packet,p);
            Require(!p.Inventory.ContainsUID(item.UID));
        }
        p.BindWorldMap(1002);
    }
    static string CombinedSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object(
'collection',@collection::json,
'exchange',(SELECT json_agg(e ORDER BY design) FROM revora_planet_exchange e WHERE uid=@uid),
'planet',(SELECT json_agg(r ORDER BY collection_step) FROM revora_planet_runs r WHERE uid=@uid),
'tiers',(SELECT json_agg(a ORDER BY achievement_id) FROM revora_achievement_evidence a WHERE uid=@uid AND achievement_id IN(11230,11231,11301)))::text",
        "collection",CollectionSnapshot(p),"uid",(long)p.Entity.UID));}
    static void CombinedTiers(GameState p){
        p.Entity.MyAchievement.Send();
        foreach(int id in new[]{11230,11231,11301}){
            bool expected=id==11230?CombinedCount(p,"Mounts")>=100:CombinedCount(p,"Garments")>=(id==11231?100:200);
            int bit=(id/100-101)*32+id%100-1;
            Require(((System.BitConverter.ToUInt32(p.Entity.MyAchievement.ToArray(),16+bit/32*4)&(1u<<(bit%32)))!=0)==expected);
            Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=@id AND source='wardrobe-permanent-v1'","uid",(long)p.Entity.UID,"id",id)==(expected?1:0));
        }
    }
    static void CombinedCollection(bool reload)
    {
        if((long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)FirstUid)==0){Require(!reload);Player(Kernel.Maps[1002]);}
        nextUid=FirstUid;var p=CollectionPlayer();
        if(reload){
            Require(Convert.ToInt32(Sql("SELECT stage FROM revora_collection_combined_probe WHERE uid=@uid","uid",(long)FirstUid))==3);
            Require(CombinedSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_collection_combined_probe WHERE uid=@uid","uid",(long)FirstUid)));
            CollectionMemory(p);CombinedTiers(p);Require(CombinedCount(p,"Garments")==200 && CombinedCount(p,"Mounts")==100);
            for(int i=0;i<156;i++)PlanetRowan(p,(byte)(40+i));PlanetRowan(p,213);
            Require(CombinedSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_collection_combined_probe WHERE uid=@uid","uid",(long)FirstUid)));
            Console.WriteLine("[collection-process] collection-combined-reload-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Combined reload did not close.");
        }
        SignEligibility(p);Call(Feature("PlanetEncounter"),"OnLogin",p);Call(Feature("PlanetWeekend"),"OnLogin",p);CombinedDeposit(p);
        int completed=Convert.ToInt32(Sql("SELECT count(*) FROM revora_planet_completions WHERE uid=@uid","uid",(long)FirstUid));
        for(int week=completed;week<8;week++){
            var window=Call(Feature("PlanetWeekend"),"Current",planetNow);string key=(string)window.GetType().GetField("Key",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(window);
            if((bool)Sql("SELECT EXISTS(SELECT 1 FROM revora_planet_runs WHERE uid=@uid AND weekend=@week AND stage='complete')","uid",(long)FirstUid,"week",key)){planetNow=planetNow.AddDays(7);Sql("UPDATE revora_collection_combined_probe SET stamp=@stamp WHERE uid=@uid","stamp",new DateTimeOffset(planetNow),"uid",(long)FirstUid);}
            PlanetRowan(p,211);
            string runStage=Convert.ToString(Sql("SELECT stage FROM revora_planet_runs WHERE uid=@uid ORDER BY accepted_at DESC LIMIT 1","uid",(long)FirstUid));
            if(runStage=="hunt"){
                int kills=Convert.ToInt32(Sql("SELECT kills FROM revora_planet_runs WHERE uid=@uid ORDER BY accepted_at DESC LIMIT 1","uid",(long)FirstUid));
                var birds=Kernel.Maps[1015];Call(typeof(Map),"_timerCallBack",birds,Time32.Now.AddSeconds(300).TotalMilliseconds);PlanetBirdmen(p,100-kills);
                runStage="encounter";
            }
            if(runStage=="encounter"){
                PlanetRowan(p,212);var guards=p.Map.Entities.Values.Where(e=>e.MonsterInfo!=null && e.MonsterInfo.ID>=9926100 && e.MonsterInfo.ID<=9926102).ToArray();Require(guards.Length==3);
                foreach(var guard in guards)EliminationHit(p,guard);HeartTalk(p,900000190,1);
            }
            PlanetRowan(p,213);CombinedDeposit(p);
            Require((long)Sql("SELECT count(*) FROM revora_planet_completions WHERE uid=@uid","uid",(long)FirstUid)==week+1);
            Console.WriteLine("[collection-combined] earned-week="+(week+1));
        }
        int stage=Convert.ToInt32(Sql("SELECT stage FROM revora_collection_combined_probe WHERE uid=@uid","uid",(long)FirstUid));
        {
            var daily=Feature("DailyWorldBosses");int paid=Convert.ToInt32(Sql("SELECT count(*) FROM revora_daily_boss_runs WHERE killer=@uid AND state=2 AND paid","uid",(long)FirstUid));Require(paid<=8);
            var latest=Sql("SELECT max(planned) FROM revora_daily_boss_runs");DateTime previous=latest==DBNull.Value?planetNow:DateTime.SpecifyKind((DateTime)latest,DateTimeKind.Utc);
            if(previous<planetNow)previous=planetNow;DateTime now=previous.ToLocalTime().Date.AddDays(1).ToUniversalTime();
            daily.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
            for(int fight=paid;fight<8;fight++){
                Call(daily,"Tick");now=DateTime.SpecifyKind((DateTime)Sql("SELECT min(planned) FROM revora_daily_boss_runs WHERE planned>=@now AND state=0","now",now),DateTimeKind.Utc).AddSeconds(1);
                Call(daily,"Tick");var active=daily.GetField("Active",Hidden).GetValue(null);Require(active!=null);
                var boss=(Entity)active.GetType().GetField("Entity",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(active);
                p.BindWorldMap(boss.MapID);EliminationHit(p,boss);now=now.ToLocalTime().Date.AddDays(1).ToUniversalTime();
            }
        }
        if(stage<2){
            p.BindWorldMap(1002);
            foreach(string category in new[]{"Garments","Mounts"}){
                var goods=(Array)Feature("RevoraPointShopSystem").GetField(category,Hidden).GetValue(null);
                foreach(object good in goods){var t=good.GetType();uint id=(uint)t.GetField("Id").GetValue(good);
                    if(!CombinedCatalog(category).Contains(id) || CombinedOwns(p,id))continue;
                    uint before=p.Entity.RevoraPoint;HeartTalk(p,900000014,(byte)t.GetField("Option").GetValue(good));
                    Require(p.Entity.RevoraPoint==before-(uint)t.GetField("Price").GetValue(good));CombinedDeposit(p);
                }
            }
            foreach(uint id in EShopFile.Shops[2888].Items.Distinct().Intersect(CombinedCatalog("Garments"))){
                if(CombinedOwns(p,id))continue;uint price=ConquerItemInformation.BaseInformations[id].ConquerPointsWorth;Require(price>0);
                uint before=p.Entity.ConquerPoints;CombinedBuy(p,id);
                Require(p.Entity.ConquerPoints==before-price);CollectionDeposit(p,p.Inventory.Objects.Single(i=>i.ID==id));
            }
            foreach(uint id in MountPacks){uint output=(uint)Call(Feature("FixedOutputPacks"),"MountFor",id);if(CombinedOwns(p,output))continue;
                var pack=p.Inventory.Objects.SingleOrDefault(i=>i.ID==id);
                if(pack==null){uint before=p.Entity.ConquerPoints;CombinedBuy(p,id);Require(p.Entity.ConquerPoints==before-2799);pack=p.Inventory.Objects.Single(i=>i.ID==id);}
                ItemHandler.UseItem(pack,p);CollectionDeposit(p,p.Inventory.Objects.Single(i=>i.ID==output));
            }
            var exchange=Feature("PlanetCollectionExchange");var stock=((uint[])exchange.GetField("Garments",Hidden).GetValue(null)).Concat((uint[])exchange.GetField("Mounts",Hidden).GetValue(null)).ToArray();
            for(int i=0;i<stock.Length;i++){if(CombinedOwns(p,stock[i]))continue;PlanetRowan(p,(byte)(40+i));CollectionDeposit(p,p.Inventory.Objects.Single(v=>v.ID==stock[i]));}
            Require(CombinedCount(p,"Garments")==198 && CombinedCount(p,"Mounts")==92);CombinedTiers(p);CombinedStash(p);
            DateTime first=new DateTime(planetNow.Year,planetNow.Month,1).AddMonths(2);
            Sql("UPDATE revora_collection_combined_probe SET stage=2,sign_start=@first WHERE uid=@uid","first",first.ToBinary(),"uid",(long)FirstUid);
            Console.WriteLine("[collection-combined] paid-stock-and-exchange-passed");
        }
        uint[] extras={193555,193775,200481,200499,200500,200543,200563,200570,200571,200574};
        var rewards=(uint[])Feature("EasterGoldPacks").GetField("Rewards",Hidden).GetValue(null);
        DateTime start=DateTime.FromBinary((long)Sql("SELECT sign_start FROM revora_collection_combined_probe WHERE uid=@uid","uid",(long)FirstUid));
        int done=Convert.ToInt32(Sql("SELECT sign_day FROM revora_collection_combined_probe WHERE uid=@uid","uid",(long)FirstUid));
        for(int n=done;n<70;n++){
            int month=n/14,day=n%14+1;SignClock(start.AddMonths(month).AddDays(day-1));DateTime utc=signNow.ToUniversalTime();
            Feature("DailySignInPacks").GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>utc));Feature("EasterGoldPacks").GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>utc));
            SignRequest(p,MsgSignIn.Actions.Sign);Require(SignReceipts(p)==n+1);
            foreach(uint type in new uint[]{3100011,3100013,3100014,3100015}){var pack=p.Inventory.Objects.SingleOrDefault(i=>i.ID==type);if(pack!=null){ItemHandler.UseItem(pack,p);Require(!p.Inventory.ContainsUID(pack.UID));}}
            if(day==7 || day==14){uint design=extras[month*2+(day==14?1:0)];if(!CombinedOwns(p,design)){
                var pack=p.Inventory.Objects.Single(i=>i.ID==720121);int choice=Array.IndexOf(rewards,design)+1;Require(choice>=1 && choice<=30);
                Feature("EasterGoldPacks").GetField("Roll",Hidden).SetValue(null,(Func<int,int,int>)((min,max)=>choice));ItemHandler.UseItem(pack,p);CollectionDeposit(p,p.Inventory.Objects.Single(i=>i.ID==design));
            }}
            CombinedTiers(p);CombinedStash(p);Sql("UPDATE revora_collection_combined_probe SET sign_day=@day WHERE uid=@uid","day",n+1,"uid",(long)FirstUid);
        }
        Require(CombinedCount(p,"Garments")==200 && CombinedCount(p,"Mounts")==100 && p.Entity.StorageItems.Count==300);
        Require(SignReceipts(p)==70 && MountUses(p.Entity.UID)==17 && (long)Sql("SELECT count(*) FROM revora_easter_gold_pack_uses WHERE uid=@uid","uid",(long)FirstUid)==10);
        Require((long)Sql("SELECT count(*) FROM revora_planet_exchange WHERE uid=@uid","uid",(long)FirstUid)==156);
        CollectionMemory(p);CombinedTiers(p);
        Sql("UPDATE revora_collection_combined_probe SET stage=3,snapshot=@snapshot WHERE uid=@uid","snapshot",CombinedSnapshot(p),"uid",(long)FirstUid);
        Console.WriteLine("[collection-process] collection-combined-earn-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Combined earning did not close.");
    }
}
