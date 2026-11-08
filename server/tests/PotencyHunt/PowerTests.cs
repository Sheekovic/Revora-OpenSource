using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;

internal static partial class PotencyHuntTests
{
    static readonly Type Power=typeof(Entity).Assembly.GetType("Revora.Game.Features.PowerArenaEvent");
    static readonly Type PowerPacks=typeof(Entity).Assembly.GetType("Revora.Game.Features.PowerArenaPacks");
    static DateTime powerNow;
    static int[] powerNeeds={30,45,60,35,45,60,90};
    static long PowerCount(GameState p,string expression) {return Scalar("SELECT "+expression+" FROM revora_power_runs WHERE uid=@uid",p.Entity.UID);}
    static void PowerNpc(GameState p,byte option) {MoonNpc(p,p.Entity.MapID==1002?500010u:(uint)(3956+p.Entity.MapID-1730),option);}
    static Entity PowerMob(GameState p) {
        var m=(Entity)typeof(CompanionTests).GetMethod("Monster",Hidden).Invoke(null,new object[]{p.Map,p.Entity.X,p.Entity.Y,1u});
        m.MonsterInfo.ID=(uint)(2133+p.Entity.MapID-1730);m.MonsterInfo.Name=m.Name="ArenaSentinel"+(p.Entity.MapID-1729);m.MonsterInfo.Level=MonsterInformation.MonsterInformations[m.MonsterInfo.ID].Level;m.Level=m.MonsterInfo.Level;m.IsDropped=true;return m;
    }
    static void PowerHunt(GameState p,int count) {for(int i=0;i<count;i++){PowerMob(p).Die(p.Entity);if(Fenced(p))throw new Exception("Power hunt persistence failed.");}}
    static void PowerPreview(GameState p,int family,int tier) {PowerNpc(p,(byte)(40+family));PowerNpc(p,(byte)(100+family*3+tier));}
    static void VerifyPower(string runtime,Map town) {
        Query("DROP TABLE entities;CREATE TABLE entities AS TABLE public.entities WITH NO DATA");
        Query("CREATE TABLE titles (LIKE public.titles INCLUDING ALL)");Query("CREATE TABLE itemadding (LIKE public.itemadding INCLUDING ALL)");
        StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);ConquerItem.ItemUID=new Counter(5100000);
        Query("CREATE TABLE npcs (LIKE public.npcs INCLUDING ALL)");Query("INSERT INTO npcs SELECT * FROM public.npcs WHERE id=500010 OR id BETWEEN 3956 AND 3962");
        for(ushort map=1730;map<=1736;map++)World(runtime,map);
        powerNow=new DateTime(2026,9,14,20,0,0,DateTimeKind.Utc);Power.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>powerNow));
        Call(Power,null,"Initialize");Call(Power,null,"PlaceNpcs");
        Check(town.Npcs.ContainsKey(500010) && Enumerable.Range(0,7).All(i=>Kernel.Maps[(ushort)(1730+i)].Npcs.ContainsKey((uint)(3956+i))),"manager and all seven Agates are restored on installed floors");
        Check(CtfCount("SELECT count(DISTINCT mapid) FROM public.monsterspawns WHERE mapid BETWEEN 1730 AND 1736 AND npctype=2133+mapid-1730")==7,"all arena floors have installed guardian spawns");
        var guards=Enumerable.Range(0,7).Select(i=>MonsterInformation.MonsterInformations[(uint)(2133+i)]).ToArray();
        Check(guards[0].Hitpoints==15000 && guards[6].Hitpoints==105000 && guards.Zip(guards.Skip(1),(a,b)=>a.Hitpoints<b.Hitpoints && a.MaxAttack<b.MaxAttack && a.Level<b.Level).All(v=>v),"loaded guardians grow stronger through the seven floors");
        Check((DateTime)Call(Power,null,"Window",powerNow.AddTicks(-1))==DateTime.MinValue && (DateTime)Call(Power,null,"Window",powerNow)==powerNow
            && (DateTime)Call(Power,null,"Window",powerNow.AddHours(1))==DateTime.MinValue,"summer arena opening and exclusive closing boundaries are exact");
        Check((DateTime)Call(Power,null,"Window",new DateTime(2026,12,14,21,0,0,DateTimeKind.Utc))!=DateTime.MinValue
            && (DateTime)Call(Power,null,"Window",new DateTime(2026,12,15,4,0,0,DateTimeKind.Utc))!=DateTime.MinValue,"winter Pacific offsets retain both daily sessions");
        var p=MoonPlayer(town);powerNow=powerNow.AddMinutes(-1);PowerNpc(p,1);Check(p.Entity.MapID==1002,"closed session rejects ordinary entry");powerNow=powerNow.AddMinutes(1);
        p.Fake=true;PowerNpc(p,1);p.Fake=false;p.ActiveNpc=500010;p.Entity.X=1;p.Entity.Y=1;Npcs.GetDialog(new NpcRequest(true){NpcID=500010,OptionID=1},p);
        Check(Scalar("SELECT count(*) FROM revora_power_runs WHERE uid=@uid",p.Entity.UID)==0,"fake and remote entry cannot create an arena run");
        MoonNear(p,500010);Parallel.For(0,12,i=>Npcs.GetDialog(new NpcRequest(true){NpcID=500010,OptionID=1},p));
        Check(p.Entity.MapID==1730 && PowerCount(p,"stage")==1,"concurrent ordinary signup creates one first-floor run");
        p.Entity.KillArena1=90;PowerNpc(p,1);PowerNpc(p,3);p.Entity.Teleport(1736,50,50);
        Check(p.Entity.MapID==1730 && p.Inventory.Count==0,"legacy counters, early advance and external teleport cannot skip floors");
        p.ActiveNpc=3962;Npcs.GetDialog(new NpcRequest(true){NpcID=3962,OptionID=1},p);Check(!HasAchievement(p,11013),"forged seventh-floor NPC request cannot grant completion");
        var wrong=PowerMob(p);wrong.MonsterInfo.ID=1413;wrong.Die(p.Entity);var far=PowerMob(p);far.X=(ushort)(p.Entity.X+30);far.Die(p.Entity);
        Check(PowerCount(p,"kills")==0,"wrong-family and distant deaths cannot advance the stage");
        PowerHunt(p,29);PowerNpc(p,1);Check(p.Inventory.Count==0,"partial guardian quota cannot claim a reward");
        Query("ALTER TABLE revora_power_runs ADD CONSTRAINT power_kill_failure CHECK(kills<30) NOT VALID");PowerMob(p).Die(p.Entity);
        Check(Fenced(p) && PowerCount(p,"kills")==29,"failed kill progress rolls back and fences the character");Query("ALTER TABLE revora_power_runs DROP CONSTRAINT power_kill_failure");DemonReset(p);
        var corpse=PowerMob(p);Parallel.For(0,12,i=>corpse.Die(p.Entity));Check(PowerCount(p,"kills")==30,"duplicate corpse deaths commit only one final guardian");
        var full=Enumerable.Range(0,40).Select(i=>Pack(p,1088001,1)).ToArray();PowerNpc(p,1);
        Check(PowerCount(p,"claimed")==0 && p.Inventory.Count==40,"full inventory preserves the cleared quota and reward");
        foreach(var item in full){Query("DELETE FROM items WHERE uid=@id","id",(long)item.UID);p.Inventory.Remove(item.UID,Enums.ItemUse.None,false);}
        Query("ALTER TABLE revora_power_claims ADD CONSTRAINT power_claim_failure CHECK(false) NOT VALID");PowerNpc(p,1);
        Check(Fenced(p) && p.Inventory.Count==0 && PowerCount(p,"claimed")==0,"failed floor reward atomically preserves quota and badge entitlement");
        Query("ALTER TABLE revora_power_claims DROP CONSTRAINT power_claim_failure");DemonReset(p);MoonNear(p,3956);
        Parallel.For(0,12,i=>Npcs.GetDialog(new NpcRequest(true){NpcID=3956,OptionID=1},p));
        Check(p.Inventory.Count==1 && p.Inventory.Objects[0].ID==720148 && p.Inventory.Objects[0].Bound && PowerCount(p,"claimed")==1,"concurrent first-floor claims deliver exactly one bound badge");
        PowerNpc(p,3);PowerHunt(p,3);var opponent=MoonPlayer(town);opponent.BindWorldMap(1731);opponent.Entity.X=p.Entity.X;opponent.Entity.Y=p.Entity.Y;p.Entity.Die(opponent.Entity);
        Check(PowerCount(p,"stage")==1 && PowerCount(p,"CASE WHEN pending_death THEN 1 ELSE 0 END")==1,"actual PvP defeat persists the lower floor before queued revival");
        Call(Power,null,"Initialize");Call(Power,null,"OnLogin",p);Call(Power,null,"Tick");
        Check(p.Entity.MapID==1730 && !p.Entity.Dead && PowerCount(p,"stage")==1 && PowerCount(p,"kills")==0,"restart recovery applies a saved defeat once and revives on the lower floor");
        PowerHunt(p,30);PowerNpc(p,1);Check(p.Inventory.Count==1,"re-clearing a defeated floor cannot replay its session reward");PowerNpc(p,3);
        for(int stage=2;stage<=7;stage++) {
            int done=0;
            if(stage==5){
                var retry=PowerMob(p);Check(retry.MonsterInfo.Level==120 && !HasAchievement(p,10525),"fifth-floor loaded guardian reaches the unarmed threshold");
                Query("ALTER TABLE revora_power_runs ADD CONSTRAINT reject_power_unarmed CHECK(kills<1) NOT VALID");
                retry.Die(p.Entity);Query("ALTER TABLE revora_power_runs DROP CONSTRAINT reject_power_unarmed");
                Check(!retry.Dead && PowerCount(p,"kills")==0 && !HasAchievement(p,10525),"failed managed counter preserves living guardian and grants no unarmed credit");
                Query("CREATE FUNCTION reject_power_unarmed() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER reject_power_unarmed BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW WHEN(NEW.achievement_id=10525) EXECUTE FUNCTION reject_power_unarmed()");
                try{retry.Die(p.Entity);}finally{Query("DROP TRIGGER reject_power_unarmed ON revora_achievement_evidence;DROP FUNCTION reject_power_unarmed()");}
                Check(!retry.Dead && PowerCount(p,"kills")==0 && !HasAchievement(p,10525)
                    && Scalar("SELECT count(*) FROM revora_unarmed_wild_victories WHERE uid=@uid",p.Entity.UID)==0,"failed unarmed receipt rolls back guardian counter and accepted-life row together");
                retry.Die(p.Entity);retry.Die(p.Entity);
                Check(retry.Dead && PowerCount(p,"kills")==1 && HasAchievement(p,10525),"retried guardian grants one counter and immediate unarmed achievement");done=1;
            }
            if(stage==6){
                var retry=PowerMob(p);var victory=typeof(Entity).Assembly.GetType("Revora.Game.Features.WildMonsterVictory");
                victory.GetField("AfterCommit",Hidden).SetValue(null,(Action)(()=>{throw new System.IO.IOException("fixture acknowledgement lost");}));
                try{retry.Die(p.Entity);}finally{victory.GetField("AfterCommit",Hidden).SetValue(null,null);}
                Check(!retry.Dead && PowerCount(p,"kills")==1,"uncertain managed commit retains the saved guardian counter");
                retry.Die(p.Entity);Check(retry.Dead && PowerCount(p,"kills")==1,"managed life retry does not repeat its already committed progress");done=1;
            }
            PowerHunt(p,powerNeeds[stage-1]-done);
            if(stage==5){
                long before=Scalar("SELECT count(*) FROM revora_unarmed_wild_victories WHERE uid=@uid",p.Entity.UID);PowerMob(p).Die(p.Entity);
                Check(PowerCount(p,"kills")==powerNeeds[4] && Scalar("SELECT count(*) FROM revora_unarmed_wild_victories WHERE uid=@uid",p.Entity.UID)==before,"already cleared quota cannot create redundant managed kill records");
            }
            if(stage==7){
                var crowded=Enumerable.Range(0,33).Select(i=>Pack(p,1088001,1)).ToArray();PowerNpc(p,1);PowerNpc(p,2);
                Check(p.Inventory.Count==39 && PowerCount(p,"claimed")==63 && !p.Inventory.Objects.Any(i=>i.ID==200449),"one free slot cannot split the final floor gift from its mount");
                foreach(var item in crowded){Query("DELETE FROM items WHERE uid=@id","id",(long)item.UID);p.Inventory.Remove(item.UID,Enums.ItemUse.None,false);}
                Query("ALTER TABLE items ADD CONSTRAINT power_mount_failure CHECK(id<>200449) NOT VALID");PowerNpc(p,1);
                Check(Fenced(p) && p.Inventory.Count==6 && PowerCount(p,"claimed")==63
                    && Scalar("SELECT count(*) FROM items WHERE entityid=@uid",p.Entity.UID)==6,"failed mount insertion rolls back the final badge and claim");
                Query("ALTER TABLE items DROP CONSTRAINT power_mount_failure");DemonReset(p);
                Query("ALTER TABLE revora_achievement_evidence ADD CONSTRAINT power_top_failure CHECK(achievement_id<>11013) NOT VALID");PowerNpc(p,1);
                Check(Fenced(p) && p.Inventory.Count==6 && !HasAchievement(p,11013),"failed seventh-floor achievement rolls back its badge and terminal claim");
                Query("ALTER TABLE revora_achievement_evidence DROP CONSTRAINT power_top_failure");DemonReset(p);}
            if(stage==7){MoonNear(p,3962);Parallel.For(0,12,i=>Npcs.GetDialog(new NpcRequest(true){NpcID=3962,OptionID=1},p));}
            PowerNpc(p,1);if(stage<7)PowerNpc(p,3);
        }
        Check(HasAchievement(p,11013) && !HasAchievement(p,11014) && p.Inventory.Objects.Count(i=>i.ID==720148)==7,"full first climb records floor seven and seven earned badges only");
        var earnedMount=p.Inventory.Objects.Single(i=>i.ID==200449);
        Check(StorageItem.Mounts[200449].Stars==5 && !earnedMount.Bound && earnedMount.Days==0 && earnedMount.TimeLeftInMinutes==0
            && Scalar("SELECT count(*) FROM revora_power_claims c JOIN items i ON i.uid=c.mount_uid WHERE c.uid=@uid AND c.stage=7 AND i.id=200449 AND i.bound=0",p.Entity.UID)==1,
            "seven-floor earning commits a permanent unbound five-star Somersault Cloud and its receipt");
        PowerNpc(p,1);PowerNpc(p,2);Check(p.Inventory.Count==8,"repeat final-floor choices cannot duplicate either reward");
        foreach(var item in p.Inventory.Objects)p.Inventory.Remove(item.UID,Enums.ItemUse.None,false);ConquerItemTable.LoadItems(p);Call(Power,null,"Initialize");Call(Power,null,"OnLogin",p);
        Check(p.Entity.MapID==1736 && p.Inventory.Count==8 && HasAchievement(p,11013),"ordinary item and event reload retain badges, cleared floor and achievement");
        opponent.BindWorldMap(1736);opponent.Entity.X=p.Entity.X;opponent.Entity.Y=p.Entity.Y;p.Entity.Die(opponent.Entity);Call(Power,null,"Tick");
        Check(p.Entity.MapID==1002 && !p.Entity.Dead && PowerCount(p,"CASE WHEN entered THEN 1 ELSE 0 END")==0,"seventh-floor defeat ejects and revives the player");
        PowerNpc(p,1);Check(p.Entity.MapID==1002,"immediate re-entry after knockout is blocked");
        powerNow=powerNow.AddMinutes(5).AddTicks(-1);PowerNpc(p,1);Check(p.Entity.MapID==1002,"five-minute knockout cooldown is exclusive before its boundary");
        powerNow=powerNow.AddTicks(1);PowerNpc(p,1);Check(p.Entity.MapID==1730 && PowerCount(p,"claimed")==127,"cooldown expiry permits entry without resetting earlier rewards");PowerNpc(p,5);

        var buyer=MoonPlayer(town);var stack=Pack(buyer,720148,10);var remainder=Pack(buyer,720148,2);PowerNpc(buyer,230);
        Check(buyer.Inventory.Count==2 && stack.StackSize==10,"unconfirmed exchange request consumes no badges");
        PowerPreview(buyer,4,2);Query("ALTER TABLE revora_power_exchanges ADD CONSTRAINT power_exchange_failure CHECK(false) NOT VALID");PowerNpc(buyer,230);
        Check(Fenced(buyer) && buyer.Inventory.Count==2 && stack.StackSize==10,"failed badge exchange restores both stacks and creates no pack");
        Query("ALTER TABLE revora_power_exchanges DROP CONSTRAINT power_exchange_failure");DemonReset(buyer);PowerPreview(buyer,4,2);MoonNear(buyer,500010);
        Parallel.For(0,12,i=>Npcs.GetDialog(new NpcRequest(true){NpcID=500010,OptionID=230},buyer));
        Check(buyer.Inventory.Count==1 && buyer.Inventory.Objects[0].ID==724138 && buyer.Inventory.Objects[0].Bound && !HasAchievement(buyer,11014),"confirmed concurrent twelve-badge purchase delivers one bound pack without fabricated earning achievement");
        var pack=buyer.Inventory.Objects[0];Query("ALTER TABLE revora_power_pack_results ADD CONSTRAINT power_pack_failure CHECK(false) NOT VALID");ItemHandler.UseItem(pack,buyer);
        Check(Fenced(buyer) && buyer.Inventory.Count==1 && buyer.Inventory.Objects[0].UID==pack.UID,"failed pack opening preserves the original pack and records no random reward");
        Query("ALTER TABLE revora_power_pack_results DROP CONSTRAINT power_pack_failure");DemonReset(buyer);
        var forged=new ConquerItem(true){UID=pack.UID,ID=pack.ID,Bound=true,StackSize=1};ItemHandler.UseItem(forged,buyer);
        Check(buyer.Inventory.Objects[0].UID==pack.UID,"copied item object cannot open an owned refinery pack");
        Parallel.For(0,12,i=>ItemHandler.UseItem(pack,buyer));var material=buyer.Inventory.Objects[0];
        Check(buyer.Inventory.Count==1 && material.UID!=pack.UID && material.ID>=724432 && material.ID<=724434 && material.Bound
            && Scalar("SELECT count(*) FROM revora_power_pack_results WHERE uid=@uid",buyer.Entity.UID)==1,"concurrent pack opening commits one persistent bound material and random result");
        buyer.Inventory.Remove(material.UID,Enums.ItemUse.None,false);ConquerItemTable.LoadItems(buyer);Check(buyer.Inventory.Objects[0].UID==material.UID,"refinery material survives ordinary inventory loading");
        var partial=Pack(buyer,720148,10);PowerPreview(buyer,0,1);PowerNpc(buyer,230);
        Check(partial.StackSize==4 && Scalar("SELECT stacksize FROM items WHERE uid="+partial.UID+" AND entityid=@uid",buyer.Entity.UID)==4,"six-badge exchange updates a partial stack in memory and storage");
        PowerPreview(buyer,0,0);PowerNpc(buyer,230);Check(partial.StackSize==2,"primary exchange costs exactly two badges");
        PowerPreview(buyer,0,0);powerNow=powerNow.AddMinutes(2);PowerNpc(buyer,230);Check(partial.StackSize==2,"expired confirmation cannot deduct badges");

        powerNow=new DateTime(2026,9,15,3,0,0,DateTimeKind.Utc);PowerNpc(p,1);
        Check(p.Entity.MapID==1730 && PowerCount(p,"claimed")==0,"second daily session creates fresh floor entitlements");
        for(int stage=1;stage<=5;stage++){PowerHunt(p,powerNeeds[stage-1]);PowerNpc(p,1);if(stage<5)PowerNpc(p,3);}
        Check(HasAchievement(p,11014) && Scalar("SELECT badges FROM revora_power_totals WHERE uid=@uid",p.Entity.UID)==12,"twelfth genuinely earned badge grants achievement across saved sessions");
        PowerNpc(p,3);PowerHunt(p,powerNeeds[5]);PowerNpc(p,2);
        Check(p.Inventory.Objects.Any(i=>i.ID==722136 && i.Bound) && Scalar("SELECT badges FROM revora_power_totals WHERE uid=@uid",p.Entity.UID)==12,"experience choice delivers a bound EXP Ball without adding badge credit");
        PowerNpc(p,3);PowerHunt(p,3);powerNow=powerNow.AddHours(1);PowerNpc(p,1);Call(Power,null,"Tick");
        Check(p.Entity.MapID==1002 && p.Inventory.Count==14 && PowerCount(p,"CASE WHEN entered THEN 1 ELSE 0 END")==0,"session closure prevents reward claims and returns remaining players");
        var inactive=MoonPlayer(town);inactive.BindWorldMap(1736);Call(Power,null,"OnLogin",inactive);
        Check(inactive.Entity.MapID==1002 && !HasAchievement(inactive,11013),"unregistered saved arena position is returned safely without progress");
        powerNow=new DateTime(2026,9,15,20,0,0,DateTimeKind.Utc);var legacy=MoonPlayer(town);PowerNpc(legacy,1);
        Query("UPDATE revora_power_runs SET valuable_reward=false WHERE uid=@uid","uid",(long)legacy.Entity.UID);
        Call(Power,null,"Initialize");Call(Power,null,"OnLogin",legacy);
        for(int stage=1;stage<=7;stage++){PowerHunt(legacy,powerNeeds[stage-1]);PowerNpc(legacy,2);if(stage<7)PowerNpc(legacy,3);}
        PowerNpc(legacy,1);Check(legacy.Inventory.Count==7 && legacy.Inventory.Objects.All(i=>i.ID==722136)
            && Scalar("SELECT count(*) FROM revora_power_claims WHERE uid=@uid AND mount_uid IS NOT NULL",legacy.Entity.UID)==0,
            "old arena contract survives restart and earns only its original floor gifts");
        PowerNpc(legacy,5);powerNow=powerNow.AddHours(7);PowerNpc(legacy,1);
        for(int stage=1;stage<=7;stage++){PowerHunt(legacy,powerNeeds[stage-1]);PowerNpc(legacy,2);if(stage<7)PowerNpc(legacy,3);}
        var cloud=legacy.Inventory.Objects.Single(i=>i.ID==200449);
        Check(!cloud.Bound && Scalar("SELECT count(*) FROM revora_power_claims WHERE uid=@uid AND mount_uid IS NOT NULL AND choice='exp-ball'",legacy.Entity.UID)==1,
            "next session upgrades the old contract and EXP choice earns the same final mount");
        DataHolder.ReadStats();legacy.Entity.Vitality=100;legacy.LoadItemStats();
        ItemHandler.EquipItem(new ItemUsage(true){UID=cloud.UID,dwParam=ConquerItem.SteedArmor,Action=ItemUsage.Mode.EquipItem},legacy);
        Check(legacy.Account.State==AccountTable.AccountState.Player && legacy.Equipment.TryGetItem(ConquerItem.SteedArmor)!=null
            && legacy.Equipment.TryGetItem(ConquerItem.SteedArmor).UID==cloud.UID && SavedRune(cloud,"position")==ConquerItem.SteedArmor,
            "ordinary hunter equips the earned five-star mount armor and saves its slot");
        foreach(var item in legacy.Inventory.Objects)legacy.Inventory.Remove(item.UID,Enums.ItemUse.None,false);
        Array.Clear(legacy.Equipment.Objects,0,legacy.Equipment.Objects.Length);ConquerItemTable.LoadItems(legacy);
        Check(legacy.Equipment.TryGetItem(ConquerItem.SteedArmor)!=null && legacy.Equipment.TryGetItem(ConquerItem.SteedArmor).UID==cloud.UID
            && !legacy.Equipment.TryGetItem(ConquerItem.SteedArmor).Bound,"production item reload preserves the equipped earned mount UID and binding");
        var families=(uint[])PowerPacks.GetField("Bases",Hidden).GetValue(null);
        Check(families.Length==16 && Enumerable.Range(0,16).All(f=>Enumerable.Range(0,3).All(t=>Enumerable.Range(0,3).All(e=>ConquerItemInformation.BaseInformations.ContainsKey((uint)Call(PowerPacks,null,"Result",f,t,(byte)e))))),"all forty-eight offered packs have installed material outcomes");
        var crafts=MoonPlayer(town);
        for(int f=0;f<16;f++)for(int tier=0;tier<3;tier++) {
            Pack(crafts,720148,(ushort)(tier==0?2:tier==1?6:12));PowerPreview(crafts,f,tier);PowerNpc(crafts,230);
            var offered=crafts.Inventory.Objects.Single();ItemHandler.UseItem(offered,crafts);var outcome=crafts.Inventory.Objects.Single();
            Check(offered.ID==families[f]+tier && offered.Bound && outcome.Bound && Enumerable.Range(0,3).Any(e=>outcome.ID==(uint)Call(PowerPacks,null,"Result",f,tier,(byte)e)),
                "ordinary purchase and pack opening: family "+f+", quality "+tier);
            Query("DELETE FROM items WHERE uid=@id","id",(long)outcome.UID);crafts.Inventory.Remove(outcome.UID,Enums.ItemUse.None,false);
        }
        var packed=MoonPlayer(town);var badgeStack=Pack(packed,720148,10);
        var occupied=Enumerable.Range(0,39).Select(i=>Pack(packed,1088001,1)).ToArray();PowerPreview(packed,0,0);PowerNpc(packed,230);
        Check(packed.Inventory.Count==40 && badgeStack.StackSize==10,"full bag blocks an exchange that would leave a partial badge stack");
        Query("DELETE FROM items WHERE uid=@id","id",(long)occupied[0].UID);packed.Inventory.Remove(occupied[0].UID,Enums.ItemUse.None,false);
        PowerPreview(packed,0,0);PowerNpc(packed,230);Check(packed.Inventory.Count==40 && badgeStack.StackSize==8,"freeing one slot allows the exact pending-price exchange");
        Query("UPDATE items SET stacksize=7 WHERE uid=@id","id",(long)badgeStack.UID);PowerPreview(packed,0,0);
        Query("DELETE FROM items WHERE uid=@id","id",(long)occupied[1].UID);packed.Inventory.Remove(occupied[1].UID,Enums.ItemUse.None,false);PowerNpc(packed,230);
        Check(Fenced(packed) && badgeStack.StackSize==8 && Scalar("SELECT stacksize FROM items WHERE uid="+badgeStack.UID+" AND entityid=@uid",packed.Entity.UID)==7,
            "stale badge ownership fences without charging or creating a pack");
        p.Entity.Vitality=100;p.LoadItemStats();new TitleStorage().Login(p);
        var wardrobeMount=p.Inventory.Objects.Single(i=>i.ID==200449);
        var coat=new CoatStorage{Info=new CoatStorageProto{ActionId=CoatStorage.Action.Addtowardrobe,ItemGuid=wardrobeMount.UID,ItemId=(int)wardrobeMount.ID}};
        coat.Handle(p);coat.Handle(p);
        Check(p.Entity.StorageItems.ContainsKey(wardrobeMount.UID) && !p.Inventory.ContainsUID(wardrobeMount.UID)
            && SavedRune(wardrobeMount,"inwardrobe")==1 && OwnedTitles(p).ContainsKey(60040023),
            "earned five-star mount enters the wardrobe once and qualifies for its mount-wing title");
        foreach(string table in new[]{"teamarena","championarena","quests","status","reward","rewardclameid","merchant","dialyquests","activeness","house","pkexploits","achievement"})
            Query("CREATE TABLE IF NOT EXISTS "+table+" AS TABLE public."+table+" WITH NO DATA");
        Query("CREATE TABLE IF NOT EXISTS prizes(uid bigint PRIMARY KEY,name text,prizes bytea)");
        foreach(var owner in new[]{p,legacy}){
            owner.Entity.DailySignDays=new byte[0];
            if(!EntityTable.SaveEntity(owner))throw new Exception("Power reconnect profile save failed.");
        }
        long savedUnarmed=Scalar("SELECT count(*) FROM revora_unarmed_wild_victories WHERE uid=@uid AND unarmed",p.Entity.UID);
        var reloaded=ReloadBall(p);new TitleStorage().Login(reloaded);Call(Power,null,"OnLogin",reloaded);
        reloaded.Entity.MyAchievement.Send();
        Check(HasAchievement(reloaded,10525) && savedUnarmed==1 && Scalar("SELECT count(*) FROM revora_unarmed_wild_victories WHERE uid=@uid AND unarmed",reloaded.Entity.UID)==savedUnarmed,"Power Arena unarmed credit survives full character reload without a second award");
        Check(reloaded.Account.State==AccountTable.AccountState.Player && reloaded.Entity.StorageItems.ContainsKey(wardrobeMount.UID)
            && !reloaded.Entity.StorageItems[wardrobeMount.UID].Bound && OwnedTitles(reloaded).ContainsKey(60040023)
            && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id=200449",reloaded.Entity.UID)==1,
            "fresh character reload retains the earned mount wardrobe ownership and five-star title without duplication");
        legacy=ReloadBall(legacy);Call(Power,null,"OnLogin",legacy);PowerNpc(legacy,1);PowerNpc(legacy,2);
        Check(legacy.Equipment.TryGetItem(ConquerItem.SteedArmor)!=null && legacy.Equipment.TryGetItem(ConquerItem.SteedArmor).UID==cloud.UID
            && !legacy.Equipment.TryGetItem(ConquerItem.SteedArmor).Bound
            && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id=200449",legacy.Entity.UID)==1,
            "fresh character reload restores the equipped earned mount and rejects both final-claim replays");
        VerifyEarnedFashionCollection(reloaded);
    }
    static void VerifyEarnedFashionCollection(GameState p)
    {
        var fashion=typeof(Entity).Assembly.GetType("Revora.Game.Features.FashionCompetition");
        var clock=fashion.GetField("Clock",Hidden);object previous=clock.GetValue(null);
        var titleClock=TitleRules.GetField("Clock",Hidden);object previousTitle=titleClock.GetValue(null);
        DateTime close=new DateTime(2035,2,1,8,0,0,DateTimeKind.Utc),now=close;
        clock.SetValue(null,(Func<DateTime>)(()=>now));titleClock.SetValue(null,(Func<DateTime>)(()=>now));
        try {
            Call(fashion,null,"Initialize");
            Check((bool)Call(fashion,null,"Freeze",close),"earned Power Arena collection enters the monthly Fashion snapshot");
            Check(Scalar("SELECT score FROM revora_fashion_results WHERE season='2035-01' AND uid=@uid",p.Entity.UID)==5
                && Scalar("SELECT placement FROM revora_fashion_results WHERE season='2035-01' AND uid=@uid",p.Entity.UID)==1,
                "earned permanent Somersault Cloud supplies five stars and a tied first place without seeded collection designs");
            new TitleStorage().Login(p);TitleRequest(20522052,TitleStorage.Action.Equip).Handle(p);
            Check(OwnedTitles(p).ContainsKey(20522052) && p.Entity.EquippedTitle==20522052,
                "ordinary arena winner earns and equips Fashion Icon through the actual collection result");
            p=ReloadBall(p);new TitleStorage().Login(p);
            Check(OwnedTitles(p).ContainsKey(20522052) && p.Entity.EquippedTitle==20522052
                && Scalar("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source='fashion-revora-v1'",p.Entity.UID)==1,
                "production character and wardrobe reload preserve the earned Fashion title without duplicate grants");
            now=close.AddDays(30);new TitleStorage().Login(p);
            Check(!OwnedTitles(p).ContainsKey(20522052) && p.Entity.EquippedTitle==0 && OwnedTitles(p).ContainsKey(60040023),
                "earned Fashion Icon expires while permanent mount ownership still qualifies for Moonlight Wings");
        } finally {clock.SetValue(null,previous);titleClock.SetValue(null,previousTitle);}
    }
}
