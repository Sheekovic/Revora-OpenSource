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
    static readonly Type Houses=typeof(Entity).Assembly.GetType("Revora.Game.Features.Housing");
    static GameState HousePlayer(Map town)
    {
        var p=AdventureVisitor(town,30156);p.Entity.Money=7800000;p.Entity.ConquerPoints=36000;
        var actor=town.Npcs[30156];bool placed=false;
        for(int dx=-6;dx<=6 && !placed;dx++)for(int dy=-6;dy<=6 && !placed;dy++)
            if(dx*dx+dy*dy<=36 && town.Floor[actor.X+dx,actor.Y+dy,MapObjectType.Player]){p.Entity.X=(ushort)(actor.X+dx);p.Entity.Y=(ushort)(actor.Y+dy);placed=true;}
        if(!placed)throw new Exception("House Admin has no legal nearby standing tile.");
        EntityTable.UpdateMoney(p);Query("UPDATE entities SET conquerpoints=36000 WHERE uid=@uid","uid",(long)p.Entity.UID);return p;
    }
    static void HouseNpc(GameState p,byte option){AdventureNpc(p,30156,option);}
    static void BuyHouseCert(GameState p,int level){HouseNpc(p,(byte)(level+4));HouseNpc(p,40);}
    static void InitializeHousingFixture(string runtime,Map town)
    {
        Query("DROP TABLE entities;CREATE TABLE entities AS TABLE public.entities WITH NO DATA;ALTER TABLE entities ADD PRIMARY KEY(uid);CREATE TABLE titles (LIKE public.titles INCLUDING ALL)");
        foreach(string table in new[]{"teamarena","championarena","quests","status","reward","rewardclameid","merchant","dialyquests","activeness","pkexploits","achievement","itemadding","profs","npcs","sobnpcs"})
            Query("CREATE TABLE IF NOT EXISTS "+table+" AS TABLE public."+table+" WITH NO DATA");
        Query("CREATE TABLE house (LIKE public.house INCLUDING ALL);CREATE TABLE furniture (LIKE public.furniture INCLUDING ALL)");
        Query("CREATE TABLE prizes(uid bigint PRIMARY KEY,name text,prizes bytea)");
        StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
        Call(EvidenceType,null,"Initialize");
        foreach(ushort terrain in new ushort[]{1098,1099,2080,1765,3024}){World(runtime,terrain);DMaps.MapPaths[terrain]="maps\\"+terrain+".map";}
        MiningNpc(town,30156);Call(Houses,null,"Initialize");Call(Houses,null,"Initialize");
        World(runtime,1036);
    }
    static void VerifyHousing(string runtime,Map town,bool earning=false)
    {
        InitializeHousingFixture(runtime,town);
        uint[] types={721170,721174,711198,711200,711553};int[] achievements={10102,10103,10104,10105,10122};
        ulong[] costs={300000,500000,2000000,5000000,0};uint[] cp={8000,4000,6000,8000,10000};
        var p=HousePlayer(town);uint wardrobe=StoredQuestGarment(p);uint previousCp=p.Entity.ConquerPoints;ulong previousSilver=p.Entity.Money;ushort identity=0;
        HouseNpc(p,40);Check(Scalar("SELECT count(*) FROM house WHERE id=@uid",p.Entity.UID)==0,"unoffered house confirmation creates nothing");
        for(int level=1;level<=5;level++) {
            HouseNpc(p,(byte)(level+4));Parallel.For(0,10,i=>HouseNpc(p,40));
            Check(!Fenced(p) && p.Entity.ConquerPoints==previousCp-cp[level-1] && p.Inventory.Objects.Count(i=>i.ID==types[level-1])==1,
                "ordinary certificate purchase commits one item and exact CP payment: "+level);
            previousCp=p.Entity.ConquerPoints;
            HouseNpc(p,level==1?(byte)1:(byte)3);Parallel.For(0,10,i=>HouseNpc(p,40));
            ushort saved=(ushort)Scalar("SELECT map_id FROM revora_house_maps WHERE uid=@uid",p.Entity.UID);
            Check(!Fenced(p) && p.Entity.Money==previousSilver-costs[level-1] && !p.Inventory.Objects.Any(i=>i.ID==types[level-1])
                && Scalar("SELECT houselevel FROM house WHERE id=@uid",p.Entity.UID)==level && HasAchievement(p,achievements[level-1]),
                "ordinary construction commits certificate, silver, level and achievement once: "+level);
            if(identity==0)identity=saved;
            Check(saved==identity && saved>=60000 && MapsTable.MapInformations[saved].Owner==p.Entity.UID,"house upgrades retain durable owner identity: "+level);
            previousSilver=p.Entity.Money;
            HouseNpc(p,2);
            Check(!Fenced(p) && p.Entity.MapID==identity && p.Map.Floor[p.Entity.X,p.Entity.Y,MapObjectType.Player],"owner enters a legal house landing: "+level);
            p=ReloadBall(p);new TitleStorage().Login(p);
            Check(!Fenced(p) && p.Entity.MapID==identity && HasAchievement(p,achievements[level-1]) && p.Entity.Money==previousSilver,
                "production character/item/title reload preserves house, balance and achievement: "+level);
            var portal=p.Map.Portals.Single();AdventureWalk(p,portal.CurrentX,portal.CurrentY);
            GeneralData.UsePortal(new Data(true){ID=Data.UsePortal,UID=p.Entity.UID},p);
            Check(p.Entity.MapID==portal.DestinationMapID && !Fenced(p),"installed house exit returns its owner: "+level);
            if(p.Entity.MapID!=1002)p.Entity.Teleport(1002,345,529);
            AdventureNear(p,30156);
        }
        Check(p.Entity.ConquerPoints==0 && p.Entity.Money==0,"exact final CP and silver balances are accepted");
        Check(Scalar("SELECT count(*) FROM items WHERE uid=@uid AND position=0 AND warehouse=0 AND inwardrobe=1",wardrobe)==1,"all five house purchases, upgrades and production reloads preserve the stored wardrobe garment");
        var outsider=HousePlayer(town);outsider.Entity.Teleport(Kernel.Maps[identity].BaseID,identity,90,70);
        Check(outsider.Entity.MapID==1002,"another character cannot teleport into a private house");
        Query("UPDATE entities SET mapid=@map WHERE uid=@uid","map",(int)identity,"uid",(long)outsider.Entity.UID);
        outsider=ReloadBall(outsider);
        Check(outsider.Entity.MapID==1002,"saved foreign house location cannot bypass login ownership");
        HouseNpc(p,2);Kernel.Maps[identity].Dispose();MapsTable.MapInformations.Remove(identity);
        p=ReloadBall(p);new TitleStorage().Login(p);
        Check(p.Entity.MapID==identity && MapsTable.MapInformations[identity].Owner==p.Entity.UID && HasAchievement(p,10122),
            "production reload reconstructs the owned house after runtime-map eviction");
        p.Entity.Teleport(1002,345,529);AdventureNear(p,30156);
        foreach(string table in new[]{"items","house","revora_house_builds","revora_achievement_evidence"}) {
            var f=HousePlayer(town);BuyHouseCert(f,1);uint cert=f.Inventory.Objects.Single(i=>i.ID==types[0]).UID;
            HouseNpc(f,1);string verb=table=="items"?"DELETE":"INSERT";
            Query("CREATE FUNCTION suppress_house_write() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_house_write BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_house_write()");
            try{HouseNpc(f,40);}finally{Query("DROP TRIGGER suppress_house_write ON "+table+";DROP FUNCTION suppress_house_write()");}
            Check(Fenced(f) && Scalar("SELECT money FROM entities WHERE uid=@uid",f.Entity.UID)==7800000
                && Scalar("SELECT count(*) FROM house WHERE id=@uid",f.Entity.UID)==0
                && Scalar("SELECT count(*) FROM revora_house_maps WHERE uid=@uid",f.Entity.UID)==0
                && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10102",f.Entity.UID)==0
                && ConquerItemTable.LoadItem(cert).ID==types[0],"failed "+table+" write rolls back house, reservation, payment and award");
            f=ReloadBall(f);AdventureNear(f,30156);HouseNpc(f,1);HouseNpc(f,40);
            Check(!Fenced(f) && HasAchievement(f,10102) && f.Entity.Money==7500000,"ordinary reload retries construction once after "+table+" failure");
        }
        var buyer=HousePlayer(town);HouseNpc(buyer,5);
        Query("CREATE FUNCTION suppress_house_item() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_house_item BEFORE INSERT ON items FOR EACH ROW EXECUTE FUNCTION suppress_house_item()");
        try{HouseNpc(buyer,40);}finally{Query("DROP TRIGGER suppress_house_item ON items;DROP FUNCTION suppress_house_item()");}
        Check(Fenced(buyer) && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",buyer.Entity.UID)==36000 && buyer.Inventory.Count==0,
            "suppressed certificate delivery preserves all CPs and produces no phantom item");
        var rejected=HousePlayer(town);BuyHouseCert(rejected,1);HouseNpc(rejected,1);rejected.Trade.InTrade=true;HouseNpc(rejected,40);rejected.Trade.InTrade=false;
        Check(Scalar("SELECT count(*) FROM house WHERE id=@uid",rejected.Entity.UID)==0 && rejected.Entity.Money==7800000,
            "trading customer cannot confirm construction");
        var full=HousePlayer(town);for(int i=0;i<40;i++)Pack(full,1088001,1);BuyHouseCert(full,1);
        Check(!Fenced(full) && full.Entity.ConquerPoints==36000 && full.Inventory.Count==40,"full bag preserves CPs during certificate purchase");
        var poor=HousePlayer(town);BuyHouseCert(poor,1);poor.Entity.Money=299999;EntityTable.UpdateMoney(poor);HouseNpc(poor,1);HouseNpc(poor,40);
        Check(!Fenced(poor) && poor.Entity.Money==299999 && poor.Inventory.Objects.Any(i=>i.ID==types[0])
            && Scalar("SELECT count(*) FROM house WHERE id=@uid",poor.Entity.UID)==0,"insufficient silver preserves certificate and creates no house");
        var upgrade=HousePlayer(town);BuyHouseCert(upgrade,1);HouseNpc(upgrade,1);HouseNpc(upgrade,40);BuyHouseCert(upgrade,2);
        ushort upgradeMap=(ushort)Scalar("SELECT map_id FROM revora_house_maps WHERE uid=@uid",upgrade.Entity.UID);
        Query("INSERT INTO furniture(id,nameowner,type,lookface,mapid,cellx,celly) VALUES(93000001,@name,26,100, @map,32,32)","name",upgrade.Entity.Name,"map",(long)upgradeMap);
        HouseNpc(upgrade,3);HouseNpc(upgrade,40);
        Check(!Fenced(upgrade) && Scalar("SELECT houselevel FROM house WHERE id=@uid",upgrade.Entity.UID)==1 && upgrade.Entity.Money==7500000
            && upgrade.Inventory.Objects.Any(i=>i.ID==types[1]) && Scalar("SELECT count(*) FROM furniture WHERE mapid=(SELECT map_id FROM revora_house_maps WHERE uid=@uid)",upgrade.Entity.UID)==1,
            "furniture blocks renovation without deleting possessions or charging payment");
        Query("DELETE FROM furniture WHERE id=93000001");HouseNpc(upgrade,3);
        Query("CREATE FUNCTION suppress_house_upgrade() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_house_upgrade BEFORE UPDATE ON house FOR EACH ROW EXECUTE FUNCTION suppress_house_upgrade()");
        try{HouseNpc(upgrade,40);}finally{Query("DROP TRIGGER suppress_house_upgrade ON house;DROP FUNCTION suppress_house_upgrade()");}
        Check(Fenced(upgrade) && Scalar("SELECT houselevel FROM house WHERE id=@uid",upgrade.Entity.UID)==1
            && Scalar("SELECT money FROM entities WHERE uid=@uid",upgrade.Entity.UID)==7500000 && Kernel.Maps[upgradeMap].BaseID==1098,
            "failed renovation preserves saved payment, old level and existing runtime house");
        upgrade=ReloadBall(upgrade);AdventureNear(upgrade,30156);HouseNpc(upgrade,3);HouseNpc(upgrade,40);
        Check(!Fenced(upgrade) && HasAchievement(upgrade,10103) && upgrade.Entity.Money==7000000 && Kernel.Maps[upgradeMap].BaseID==1099,
            "renovation retries once after reload without changing house identity");
        Query("CREATE FUNCTION suppress_house_arrival() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF NEW.mapid>=60000 THEN RETURN NULL; END IF; RETURN NEW; END';CREATE TRIGGER suppress_house_arrival BEFORE UPDATE ON entities FOR EACH ROW EXECUTE FUNCTION suppress_house_arrival()");
        try{HouseNpc(p,2);}finally{Query("DROP TRIGGER suppress_house_arrival ON entities;DROP FUNCTION suppress_house_arrival()");}
        Check(Fenced(p) && Scalar("SELECT mapid FROM entities WHERE uid=@uid",p.Entity.UID)==1002,"suppressed arrival fences runtime-only house entry");
        VerifyHouseMaterials(town);
        VerifyScrollPurchases(town);
        VerifyGemHouses(runtime,town);
        VerifyHouseInstallments(town);VerifyHouseFurniture(town);
        if(earning)VerifyMinedHouses(runtime,town);
        VerifySwordAdmissions(town);VerifySwordRewards(town);
    }
    static readonly Type SwordAdmission=typeof(Entity).Assembly.GetType("Revora.Game.Features.SwordMasterAdmission");
    static GameState SwordHousePlayer(Map town) {
        var p=HousePlayer(town);BuyHouseCert(p,1);HouseNpc(p,1);HouseNpc(p,40);HouseNpc(p,2);
        p.Entity.Level=100;if(!EntityTable.SaveEntity(p))throw new Exception("Sword owner save failed.");return p;
    }
    static object SwordAdmit(GameState p,ConquerItem soul,Func<bool> context=null) {
        ushort x=0,y=0;bool found=false;
        for(int dx=-10;dx<=10 && !found;dx++)for(int dy=-10;dy<=10 && !found;dy++) {
            int tx=p.Entity.X+dx,ty=p.Entity.Y+dy;
            if(tx<0 || ty<0 || tx>ushort.MaxValue || ty>ushort.MaxValue)continue;
            if((bool)Call(SwordAdmission,null,"Cell",p.Map,(ushort)tx,(ushort)ty)){x=(ushort)tx;y=(ushort)ty;found=true;}
        }
        if(!found)throw new Exception("Sword fixture has no summon cell.");
        lock(p.ItemSyncRoot)return Call(SwordAdmission,null,"Admit",p,soul,p.Map,x,y,context??new Func<bool>(()=>true));
    }
    static void VerifySwordAdmissions(Map town) {
        Call(SwordAdmission,null,"Initialize");Call(SwordAdmission,null,"Initialize");
        var now=new DateTime(2038,1,4,20,0,0,DateTimeKind.Utc);
        SwordAdmission.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>now));
        try {
            var p=SwordHousePlayer(town);var soul=Pack(p,720842,3);soul.Bound=true;
            Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)soul.UID);
            Check(SwordAdmit(p,soul,()=>false)==null && soul.StackSize==3,
                "Sword Master admission requires the coordinator's live authorization");
            p.Entity.Level=99;Check(SwordAdmit(p,soul)==null && soul.StackSize==3,"under-level owner cannot spend a Sword Soul");p.Entity.Level=100;
            soul.Lock=1;Check(SwordAdmit(p,soul)==null,"locked Sword Soul cannot authorize a summon");soul.Lock=0;
            object won=null;int admitted=0;
            Parallel.For(0,8,n=>{var run=SwordAdmit(p,soul);if(run!=null){System.Threading.Interlocked.Increment(ref admitted);won=run;}});
            Guid id=(Guid)Field(won,"Id");
            Check(admitted==1 && soul.StackSize==2 && soul.Bound
                && Scalar("SELECT count(*) FROM revora_sword_master_runs WHERE uid=@uid AND state='active' AND before_stack=3 AND bound",p.Entity.UID)==1
                && ConquerItemTable.LoadItem(soul.UID).StackSize==2,
                "concurrent house admissions commit one daily attempt and consume one exact bound stack unit");
            Check(Scalar("SELECT map FROM revora_sword_master_runs WHERE uid=@uid",p.Entity.UID)==p.Entity.MapID
                && Scalar("SELECT terrain FROM revora_sword_master_runs WHERE uid=@uid",p.Entity.UID)==p.Map.BaseID
                && ((uint[])Field(won,"Party")).SequenceEqual(new[]{p.Entity.UID}),
                "admission freezes the registered house identity, terrain and owner party");
            p=ReloadBall(p);soul=p.Inventory.Objects.Single(i=>i.ID==720842);Call(SwordAdmission,null,"Initialize");
            Check(SwordAdmit(p,soul)==null && soul.StackSize==2,"reload and schema initialization cannot reopen an active daily attempt");
            Check(!(bool)Call(SwordAdmission,null,"Fail",p.Entity.UID+1,id)
                && (bool)Call(SwordAdmission,null,"Fail",p.Entity.UID,id) && !(bool)Call(SwordAdmission,null,"Fail",p.Entity.UID,id),
                "attempt failure requires exact owner/run and cannot close twice");
            Check(SwordAdmit(p,soul)==null,"failed hunt still consumes today's attempt");
            now=now.AddDays(-1);Check(SwordAdmit(p,soul)==null,"clock rollback cannot reopen Sword Master admission");
            now=now.AddDays(2);Check(SwordAdmit(p,soul)!=null && soul.StackSize==1,"next Pacific day permits a fresh attempt after the previous failure");
            var outsider=HousePlayer(town);var foreign=Pack(outsider,720842,1);
            outsider.Entity.MapID=p.Entity.MapID;outsider.Entity.X=p.Entity.X;outsider.Entity.Y=p.Entity.Y;
            Check(SwordAdmit(outsider,foreign)==null && foreign.StackSize==1,"foreign house metadata cannot authorize another owner's Sword Soul");
            outsider.Entity.MapID=1002;
            foreach(string fault in new[]{"receipt","consume","context"}) {
                var f=SwordHousePlayer(town);var token=Pack(f,720842,1);bool rejected=false;
                string table=fault=="receipt"?"revora_sword_master_runs":"items";
                string verb=fault=="receipt"?"INSERT":"DELETE";
                if(fault!="context")Query("CREATE FUNCTION suppress_sword_admission() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_sword_admission BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_sword_admission()");
                int calls=0;
                try{SwordAdmit(f,token,fault=="context"?new Func<bool>(()=>++calls==1):null);}catch(InvalidOperationException){rejected=true;}
                finally{if(fault!="context")Query("DROP TRIGGER suppress_sword_admission ON "+table+";DROP FUNCTION suppress_sword_admission()");}
                Check(rejected && Scalar("SELECT count(*) FROM revora_sword_master_runs WHERE uid=@uid",f.Entity.UID)==0
                    && ConquerItemTable.LoadItem(token.UID).ID==720842 && f.Inventory.Objects.Any(i=>i.UID==token.UID),
                    "failed Sword admission rolls back exact item and daily reservation: "+fault);
                Check(SwordAdmit(f,token)!=null && !f.Inventory.Objects.Any(i=>i.UID==token.UID),"single Sword Soul can be admitted once after rollback: "+fault);
            }
        } finally {SwordAdmission.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>DateTime.UtcNow));}
    }
    static readonly Type SwordRewards=typeof(Entity).Assembly.GetType("Revora.Game.Features.SwordMasterRewards");
    static FloorItem[] SwordDrops(GameState p) {return p.Map.FloorItems.Values.Where(d=>(bool)Call(SwordRewards,null,"Managed",d)).ToArray();}
    // Storage fixture only: a future owned-combat coordinator must prove the death before this transaction.
    static bool SwordRewardCommit(GameState p,object run,uint[] party=null,bool markVictory=true) {
        lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction())try {
            if(markVictory)using(var q=new Npgsql.NpgsqlCommand("UPDATE revora_sword_master_runs SET state='victory',ended_at=@now,killer=@uid WHERE id=@run AND state='active'",c,t)) {
                q.Parameters.AddWithValue("now",(DateTime)((Func<DateTime>)SwordAdmission.GetField("Clock",Hidden).GetValue(null))());
                q.Parameters.AddWithValue("uid",(long)p.Entity.UID);q.Parameters.AddWithValue("run",(Guid)Field(run,"Id"));q.ExecuteNonQuery();
            }
            Call(SwordRewards,null,"Record",c,t,(Guid)Field(run,"Id"),p,party??new[]{p.Entity.UID});t.Commit();return true;
        }catch(Exception){t.Rollback();return false;}}
    }
    static void VerifySwordRewards(Map town) {
        Call(SwordRewards,null,"Initialize");Call(SwordRewards,null,"Initialize");
        var now=new DateTime(2039,1,4,20,0,0,DateTimeKind.Utc);
        SwordAdmission.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>now));
        var random= SwordRewards.GetField("Roll",Hidden);var original=random.GetValue(null);
        try {
            int artifacts=0,accessories=0;
            for(int n=0;n<100;n++){int branch=n,draw=0;random.SetValue(null,new Func<int,int>(max=>draw++==0?branch:0));uint type=(uint)Call(SwordRewards,null,"Draw");
                if(type>=800000 && type<900000){Check(ConquerItemInformation.BaseInformations[type].PurificationLevel<=3,"Sword artifact stays within P1-P3");artifacts++;}else accessories++;}
            Check(artifacts==60 && accessories==40,"all one hundred branch outcomes give the documented 60/40 split");
            foreach(int invalid in new[]{-1,100}){random.SetValue(null,new Func<int,int>(max=>invalid));bool rejected=false;try{Call(SwordRewards,null,"Draw");}catch(InvalidOperationException){rejected=true;}Check(rejected,"invalid Sword loot draw fails closed");}
            int choice=0;random.SetValue(null,new Func<int,int>(max=>max==100?(choice++%2==0?59:60):0));
            var p=SwordHousePlayer(town);var mate=HousePlayer(town);mate.Entity.Level=100;EntityTable.SaveEntity(mate);
            // Admission's team snapshot is exercised here; protected team travel belongs to encounter acceptance.
            mate.Entity.MapID=p.Entity.MapID;mate.Entity.X=p.Entity.X;mate.Entity.Y=p.Entity.Y;TeratoTeam(p,mate);
            var run=SwordAdmit(p,Pack(p,720842,1));
            Check(!SwordRewardCommit(p,run,null,false),"active attempt cannot manufacture victory rewards");
            Check(!SwordRewardCommit(p,run,new[]{p.Entity.UID,p.Entity.UID+900000}),"foreign reward recipient rolls back the victory");
            Check(SwordRewardCommit(p,run,new[]{p.Entity.UID,mate.Entity.UID}),"victory stores frozen-team study and all three drop entitlements atomically");
            Check(!SwordRewardCommit(p,run,new[]{p.Entity.UID,mate.Entity.UID}),"replayed victory cannot issue a second reward set");
            Parallel.For(0,8,n=>Call(SwordRewards,null,"PayStudy",p));Call(SwordRewards,null,"PayStudy",mate);
            Check(!Fenced(p) && !Fenced(mate) && p.Entity.SubClasses.StudyPoints==20 && mate.Entity.SubClasses.StudyPoints==20,
                "ordinary teammates receive exactly twenty Study Points each under repeated callbacks");
            Call(SwordRewards,null,"Restore");var drops=SwordDrops(p);
            Check(drops.Length==3 && drops.Count(d=>d.Item.Bound && ConquerItemInformation.BaseInformations[d.ItemID].PurificationLevel==7)==1,
                "owned house restores two original drops plus exactly one bound P7 event reward");
            foreach(string mode in new[]{"short","length","family","seal","oversize"}) {
                var packet=LootPacket(drops[0]);if(mode=="short")Array.Resize(ref packet,12);else if(mode=="length")packet[0]--;else if(mode=="family")packet[2]++;else if(mode=="seal")packet[packet.Length-1]^=1;else Array.Resize(ref packet,129);
                p.Entity.X=drops[0].X;p.Entity.Y=drops[0].Y;FloorItem.Process(packet,p);
                Check(!Fenced(p) && !drops[0].PickedUpAlready && p.Inventory.Count==0,"malformed Sword pickup makes no mutation: "+mode);
            }
            while(p.Inventory.Count<40)Pack(p,1088001,1);Loot(p,drops[0]);
            Check(!Fenced(p) && !drops[0].PickedUpAlready && p.Inventory.Count==40,"full bag leaves Sword drop and claim untouched");
            foreach(var filler in p.Inventory.Objects.ToArray())DisRemove(p,filler);
            foreach(var d in drops){p.Entity.X=d.X;p.Entity.Y=d.Y;Parallel.For(0,8,n=>FloorItem.Process(LootPacket(d),p));}
            Check(!Fenced(p) && p.Inventory.Count==3 && Scalar("SELECT count(*) FROM revora_sword_master_loot WHERE owner=@uid AND collector=@uid",p.Entity.UID)==3,
                "native pickup commits each Sword drop once");
            Check(p.Inventory.Objects.Single(i=>i.ID<800000).Days==7,"chosen Revora accessory retains its reviewed seven-day lifetime");
            p=ReloadBall(p);Call(SwordRewards,null,"PayStudy",p);
            Check(p.Entity.SubClasses.StudyPoints==20 && p.Inventory.Objects.Count(i=>i.Bound && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)==1,
                "production reload preserves bound event P7 and exactly-once Study Points");
            foreach(string table in new[]{"revora_sword_master_study","revora_sword_master_loot"}) {
                var f=SwordHousePlayer(town);var attempt=SwordAdmit(f,Pack(f,720842,1));
                Query("CREATE FUNCTION suppress_sword_reward() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_sword_reward BEFORE INSERT ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_sword_reward()");
                bool accepted;try{accepted=SwordRewardCommit(f,attempt);}finally{Query("DROP TRIGGER suppress_sword_reward ON "+table+";DROP FUNCTION suppress_sword_reward()");}
                Check(!accepted && Scalar("SELECT count(*) FROM revora_sword_master_runs WHERE uid=@uid AND state='active'",f.Entity.UID)==1
                    && Scalar("SELECT count(*) FROM revora_sword_master_study WHERE uid=@uid",f.Entity.UID)==0
                    && Scalar("SELECT count(*) FROM revora_sword_master_loot WHERE owner=@uid",f.Entity.UID)==0,"failed reward insert rolls back the entire victory: "+table);
                Check(SwordRewardCommit(f,attempt),"failed Sword victory can retry exactly once: "+table);
            }
            var recover=SwordHousePlayer(town);var rr=SwordAdmit(recover,Pack(recover,720842,1));Check(SwordRewardCommit(recover,rr),"recovery fixture earns pending Sword rewards");
            recover.Entity.SubClasses.StudyPoints=65520;Query("UPDATE entities SET studypoints=65520 WHERE uid=@uid","uid",(long)recover.Entity.UID);Call(SwordRewards,null,"PayStudy",recover);
            Check(!Fenced(recover) && recover.Entity.SubClasses.StudyPoints==65520 && Scalar("SELECT count(*) FROM revora_sword_master_study WHERE uid=@uid AND paid_at IS NULL",recover.Entity.UID)==1,"Study Point overflow retains the pending entitlement");
            recover.Entity.SubClasses.StudyPoints=0;Query("UPDATE entities SET studypoints=0 WHERE uid=@uid","uid",(long)recover.Entity.UID);
            Query("CREATE FUNCTION suppress_sword_study() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_sword_study BEFORE UPDATE ON entities FOR EACH ROW EXECUTE FUNCTION suppress_sword_study()");
            try{Call(SwordRewards,null,"PayStudy",recover);}finally{Query("DROP TRIGGER suppress_sword_study ON entities;DROP FUNCTION suppress_sword_study()");}
            Check(Fenced(recover) && Scalar("SELECT count(*) FROM revora_sword_master_study WHERE uid=@uid AND paid_at IS NULL",recover.Entity.UID)==1,"suppressed Study credit rolls back claim and fences stale session");
            recover=ReloadBall(recover);Call(SwordRewards,null,"PayStudy",recover);Check(!Fenced(recover) && recover.Entity.SubClasses.StudyPoints==20,"reload recovers unpaid Sword Study Points");
            Call(SwordRewards,null,"ClearGround");Call(SwordRewards,null,"Restore");var gift=SwordDrops(recover).Single(d=>d.Item.Bound);
            Query("CREATE FUNCTION suppress_sword_item() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_sword_item BEFORE INSERT ON items FOR EACH ROW EXECUTE FUNCTION suppress_sword_item()");
            try{Loot(recover,gift);}finally{Query("DROP TRIGGER suppress_sword_item ON items;DROP FUNCTION suppress_sword_item()");}
            Check(Fenced(recover) && !gift.PickedUpAlready && Scalar("SELECT count(*) FROM revora_sword_master_loot WHERE owner=@uid AND collector IS NOT NULL",recover.Entity.UID)==0,"failed Sword pickup preserves floor loot and rolls back receipt");
            recover=ReloadBall(recover);gift.Item.Bound=false;Loot(recover,gift);
            Check(Fenced(recover) && !gift.PickedUpAlready && Scalar("SELECT count(*) FROM revora_sword_master_loot WHERE owner=@uid AND collector IS NOT NULL",recover.Entity.UID)==0,"altered P7 binding cannot enter inventory or consume its receipt");
            gift.Item.Bound=true;recover=ReloadBall(recover);Loot(recover,gift);Check(!Fenced(recover) && recover.Inventory.Objects.Single().Bound,"bound P7 pickup retries after production reload");
            Call(SwordRewards,null,"ClearGround");var metadata=MapsTable.MapInformations[recover.Entity.MapID];uint owner=metadata.Owner;metadata.Owner++;MapsTable.MapInformations[recover.Entity.MapID]=metadata;
            try{Call(SwordRewards,null,"Restore");Check(SwordDrops(recover).Length==0,"changed house owner prevents loot restoration into a foreign instance");}finally{metadata.Owner=owner;MapsTable.MapInformations[recover.Entity.MapID]=metadata;}
            Call(SwordRewards,null,"Restore");Check(SwordDrops(recover).Length==2,"restoration retains uncollected original drops without recreating collected P7");
            var expired=SwordDrops(recover)[0];int retained=recover.Inventory.Count;now=now.AddMinutes(3);Loot(recover,expired);
            Check(!Fenced(recover) && recover.Inventory.Count==retained,"late native pickup cannot collect expired Sword loot");Call(SwordRewards,null,"Restore");Check(SwordDrops(recover).Length==0,"expired Sword loot cannot reappear after recovery");
        } finally {Call(SwordRewards,null,"ClearGround");random.SetValue(null,original);SwordAdmission.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>DateTime.UtcNow));}
    }
    static void VerifyHouseMaterials(Map town)
    {
        var service=typeof(Entity).Assembly.GetType("Revora.Game.Features.HousingMaterials");
        Check(Scalar("SELECT count(*) FROM public.npcs WHERE id<>30157 AND mapid=1002 AND cellx BETWEEN 341 AND 343 AND celly BETWEEN 517 AND 519",0)==0,"House Agent target does not overlap the installed NPC catalog");
        Query("INSERT INTO npcs SELECT * FROM public.npcs WHERE id=30157");
        var market=Kernel.Maps[1036];foreach(uint npc in new uint[]{30158,30159,30160})MiningNpc(market,npc);
        Call(service,null,"Initialize");Call(service,null,"Initialize");Call(service,null,"PlaceAgent");Call(service,null,"PlaceAgent");
        Check(town.Npcs[30157].X==342 && town.Npcs[30157].Y==518,"House Agent overlay uses installed 6711 tooltip coordinates idempotently");
        var walker=HousePlayer(town);AdventureNear(walker,30157);AdventureNear(walker,30156);
        walker=TravelPlayer(market,141,111);AdventureNear(walker,30158);AdventureNear(walker,30159);AdventureNear(walker,30160);
        uint[] actors={30158,30159,30159,30157,30160,30160,30157};
        uint[] inputs={1072020,721171,721172,721173,1072029,721175,721176};
        uint[] outputs={721171,721172,721173,721170,721175,721176,721174};int[] counts={5,10,10,5,12,10,10};
        for(int recipe=0;recipe<7;recipe++) {
            var map=actors[recipe]==30157?town:market;var p=AdventureVisitor(map,actors[recipe]);
            for(int n=0;n<counts[recipe];n++)Pack(p,inputs[recipe],1);
            AdventureNpc(p,actors[recipe],(byte)(recipe+1));Parallel.For(0,10,i=>AdventureNpc(p,actors[recipe],40));
            Check(!Fenced(p) && p.Inventory.Objects.Count(i=>i.ID==outputs[recipe])==1 && !p.Inventory.Objects.Any(i=>i.ID==inputs[recipe])
                && Scalar("SELECT count(*) FROM revora_house_exchanges WHERE uid=@uid",p.Entity.UID)==1,"recipe consumes exact units and awards once under concurrent confirms: "+recipe);
            uint result=p.Inventory.Objects.Single(i=>i.ID==outputs[recipe]).UID;p=ReloadBall(p);
            Check(!Fenced(p) && p.Inventory.Objects.Any(i=>i.UID==result && i.ID==outputs[recipe])
                && Scalar("SELECT sum(amount) FROM revora_house_materials WHERE exchange IN(SELECT id FROM revora_house_exchanges WHERE uid=@uid)",p.Entity.UID)==counts[recipe],"production item reload preserves housing exchange and material receipt: "+recipe);
            if(recipe==3 || recipe==6) {
                // Earned final certificate feeds the real construction route. Inputs above are fixture supplied.
                p.Entity.X=342;p.Entity.Y=521;p.Entity.Money=800000;p.Entity.ConquerPoints=8000;
                EntityTable.UpdateMoney(p);Query("UPDATE entities SET conquerpoints=8000 WHERE uid=@uid","uid",(long)p.Entity.UID);AdventureNear(p,30156);
                if(recipe==6){BuyHouseCert(p,1);HouseNpc(p,1);HouseNpc(p,40);}
                HouseNpc(p,recipe==3?(byte)1:(byte)3);HouseNpc(p,40);p=ReloadBall(p);new TitleStorage().Login(p);
                Check(!Fenced(p) && HasAchievement(p,recipe==3?10102:10103) && !p.Inventory.Objects.Any(i=>i.UID==result),
                    "exchanged certificate builds the intended house level and persists achievement: "+recipe);
            }
        }
        var split=AdventureVisitor(market,30160);var bound=Pack(split,1072020,7);bound.Bound=true;ConquerItemTable.UpdateBound(bound,split);
        var extra=Pack(split,1072029,8);AdventureNpc(split,30160,5);AdventureNpc(split,30160,40);
        Check(!Fenced(split) && split.Inventory.Objects.Single(i=>i.ID==721175).Bound && extra.StackSize==3
            && ConquerItemTable.LoadItem(extra.UID).StackSize==3 && Scalar("SELECT count(*) FROM items WHERE uid=@uid",bound.UID)==0,"mixed copper grades and stacks consume twelve with binding propagation and saved remainder");
        var full=AdventureVisitor(market,30160);Pack(full,1072020,12);while(full.Inventory.Count<40)Pack(full,1088001,1);
        AdventureNpc(full,30160,5);AdventureNpc(full,30160,40);
        Check(!Fenced(full) && full.Inventory.Count==40 && full.Inventory.Objects.Any(i=>i.ID==721175),"full bag exchanges when consumed stack frees the reward slot");
        var mixed=AdventureVisitor(market,30158);foreach(uint ore in new uint[]{1072010,1072029,1072031,1072049,1072050})Pack(mixed,ore,1);
        AdventureNpc(mixed,30158,1);AdventureNpc(mixed,30158,40);mixed=ReloadBall(mixed);
        Check(!Fenced(mixed) && mixed.Inventory.Count==1 && mixed.Inventory.Objects.Single().ID==721171 && !mixed.Inventory.Objects.Single().Bound,
            "five ordinary ore families exchange together and preserve an unbound timber after reload");
        var unrelated=AdventureVisitor(market,30158);Pack(unrelated,1088001,5);AdventureNpc(unrelated,30158,1);AdventureNpc(unrelated,30158,40);
        Check(!Fenced(unrelated) && unrelated.Inventory.Count==1 && unrelated.Inventory.Objects.Single().ID==1088001 && unrelated.Inventory.Objects.Single().StackSize==5,
            "Craftsman does not consume unrelated mining rewards as ore");
        foreach(string mode in new[]{"locked","temporary","trade","far","wrong-recipe","wrong-actor","expired","changed","full","stale"}) {
            var f=AdventureVisitor(market,30160);var material=Pack(f,1072020,13);
            if(mode=="locked")material.Lock=1;if(mode=="temporary")material.Days=1;
            AdventureNpc(f,30160,5);
            if(mode=="trade")f.Trade.InTrade=true;if(mode=="far")f.Entity.X+=20;
            if(mode=="wrong-recipe")AdventureNpc(f,30160,1);
            if(mode=="wrong-actor")f.ActiveNpc=30159;
            if(mode=="expired")service.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow.AddMinutes(3)));
            if(mode=="changed")material.Bound=true;
            if(mode=="full")while(f.Inventory.Count<40)Pack(f,1088001,1);
            if(mode=="stale")Query("UPDATE items SET stacksize=14 WHERE uid=@uid","uid",(long)material.UID);
            try{AdventureNpc(f,mode=="wrong-actor"?30159u:30160u,40);}finally{service.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow));}
            Check(!f.Inventory.Objects.Any(i=>i.ID==721175) && ConquerItemTable.LoadItem(material.UID).StackSize==(mode=="stale"?14:13)
                && Scalar("SELECT count(*) FROM revora_house_exchanges WHERE uid=@uid",f.Entity.UID)==0,
                "invalid housing exchange cannot consume or award: "+mode);
        }
        foreach(string failure in new[]{"delete","update","reward","exchange","receipt"}) {
            var f=AdventureVisitor(market,30160);var material=Pack(f,1072020,(ushort)(failure=="update"?13:12));AdventureNpc(f,30160,5);
            string table=failure=="exchange"?"revora_house_exchanges":failure=="receipt"?"revora_house_materials":"items";
            string verb=failure=="delete"?"DELETE":failure=="update"?"UPDATE":"INSERT";
            Query("CREATE FUNCTION suppress_house_material() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_house_material BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_house_material()");
            try{AdventureNpc(f,30160,40);}finally{Query("DROP TRIGGER suppress_house_material ON "+table+";DROP FUNCTION suppress_house_material()");}
            Check(Fenced(f) && ConquerItemTable.LoadItem(material.UID).StackSize==material.StackSize
                && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id=721175",f.Entity.UID)==0
                && Scalar("SELECT count(*) FROM revora_house_exchanges WHERE uid=@uid",f.Entity.UID)==0,"suppressed "+failure+" rolls back housing materials and reward");
            f=ReloadBall(f);AdventureNpc(f,30160,5);AdventureNpc(f,30160,40);
            Check(!Fenced(f) && f.Inventory.Objects.Count(i=>i.ID==721175)==1,"reload retries housing exchange once after "+failure+" failure");
        }
    }
    static void VerifyMinedHouses(string runtime,Map town)
    {
        Call(MiningService,null,"Initialize");Call(BookShop,null,"Initialize");ShopFile.Load();
        World(runtime,1025);var phoenix=World(runtime,1011);MiningNpc(phoenix,125);MiningNpc(town,1);MiningNpc(town,3);
        foreach(ushort id in new ushort[]{1002,1011,1025,1036})DMaps.MapPaths[id]="maps\\"+id+".map";
        var p=HousePlayer(town);p.Entity.Money=1000000;p.Entity.ConquerPoints=0;EntityTable.UpdateMoney(p);
        Query("UPDATE entities SET conquerpoints=0 WHERE uid=@uid","uid",(long)p.Entity.UID);
        AdventureNear(p,1);ItemHandler.HandleBuyFromNPC(BuyBook(562000,1,1),p);
        var tool=p.Inventory.Objects.Single(i=>i.ID==562000);
        ItemHandler.EquipItem(new ItemUsage(true){UID=tool.UID,dwParam=4,Action=ItemUsage.Mode.EquipItem},p);
        MiningService.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>miningNow));
        MiningService.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>0));int chance=Config.MiningNormalGemBasisPoints;
        Config.MiningNormalGemBasisPoints=0;
        int trips=0;
        try {
            foreach(int level in new[]{1,2}) {
                int required=level==1?2500:1200,mined=0;
                while(mined<required) {
                    AdventureNear(p,3);
                    ItemHandler.HandleBuyFromNPC(BuyBook(1060020,2,3),p);
                    long savedSilver=Scalar("SELECT money FROM entities WHERE uid=@uid",p.Entity.UID);
                    Check(savedSilver==(long)p.Entity.Money,"return-scroll purchase saves its silver debit immediately: runtime="+p.Entity.Money+", saved="+savedSilver);
                    Check(p.Inventory.Objects.Where(i=>i.ID==1060020).Sum(i=>Math.Max(1,(int)i.StackSize))==2,"two ordinary purchased return scrolls are ready for housing trip "+trips);
                    AdventureGo(p,1011);AdventureNear(p,125);AdventureNpc(p,125,1);
                    Check(!Fenced(p) && p.Entity.MapID==1025 && p.Map.Floor[p.Entity.X,p.Entity.Y,MapObjectType.Player],"housing miner enters Maple Forest through its installed assistant");
                    int amount=Math.Min(required-mined,(40-p.Inventory.Count)/(level==1?5:12)*(level==1?5:12));
                    if(amount<=0)throw new Exception("Housing journey cannot free enough ore capacity.");
                    long before=MineCount(p);StartMining(p);
                    for(int n=0;n<amount;n++){miningNow=miningNow.AddSeconds(2);MineTick(p);}
                    Check(!Fenced(p) && MineCount(p)==before+amount && p.Inventory.Count<=40,"actual mining yields the next housing batch within forty slots");
                    mined+=amount;UseHouseReturn(p);AdventureGo(p,1036);
                    AdventureWalk(p,155,107);SkyJump(p,141,111);
                    Check(!Fenced(p) && p.Entity.MapID==1036,"ordinary bounded jump reaches the Market housing craftsmen's walking area");
                    if(level==1) {
                        AdventureNear(p,30158);while(p.Inventory.Objects.Count(i=>i.ID>=1072020 && i.ID<=1072029)>=5)HouseExchange(p,30158,1);
                        AdventureNear(p,30159);while(p.Inventory.Objects.Count(i=>i.ID==721171)>=10)HouseExchange(p,30159,2);
                        while(p.Inventory.Objects.Count(i=>i.ID==721172)>=10)HouseExchange(p,30159,3);
                    } else {
                        AdventureNear(p,30160);while(p.Inventory.Objects.Count(i=>i.ID>=1072020 && i.ID<=1072029)>=12)HouseExchange(p,30160,5);
                        while(p.Inventory.Objects.Count(i=>i.ID==721175)>=10)HouseExchange(p,30160,6);
                    }
                    UseHouseReturn(p);trips++;
                    if(trips%10==0){p=ReloadBall(p);Check(!Fenced(p) && p.Entity.MapID==1002 && p.Equipment.TryGetItem(4).UID==tool.UID,"housing collection trip reload retains materials and purchased pickaxe: "+trips);}
                }
                AdventureNear(p,30157);HouseExchange(p,30157,level==1?(byte)4:(byte)7);
                Check(p.Inventory.Count==1 && p.Inventory.Objects.Single().ID==(level==1?721170u:721174u),"all mined materials reduce to the intended certificate with no residual vouchers: "+level);
                AdventureNear(p,30156);HouseNpc(p,level==1?(byte)1:(byte)3);HouseNpc(p,40);p=ReloadBall(p);new TitleStorage().Login(p);
                Check(!Fenced(p) && p.Inventory.Count==0 && HasAchievement(p,level==1?10102:10103)
                    && Scalar("SELECT houselevel FROM house WHERE id=@uid",p.Entity.UID)==level,"fully mined and exchanged house construction survives production reload: "+level);
            }
            Check(MineCount(p)==3700,"complete housing chain accounts for 3700 mined ores: "+MineCount(p));
            Check(Scalar("SELECT count(*) FROM revora_house_exchanges WHERE uid=@uid",p.Entity.UID)==667,"complete housing chain accounts for 667 exchanges");
            Check(p.Entity.ConquerPoints==0,"complete housing chain spends no CPs");
            Check(p.Entity.Money==199000ul-(ulong)trips*400,"complete housing chain saves exact tool, scroll and construction silver: actual="+p.Entity.Money+", trips="+trips);
        } finally {
            Config.MiningNormalGemBasisPoints=chance;
            MiningService.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow));
            MiningService.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>Kernel.Random.Next(max)));
        }
    }
    static void HouseExchange(GameState p,uint actor,byte option)
    {
        long before=Scalar("SELECT count(*) FROM revora_house_exchanges WHERE uid=@uid",p.Entity.UID);
        AdventureNpc(p,actor,option);AdventureNpc(p,actor,40);
        if(Fenced(p) || Scalar("SELECT count(*) FROM revora_house_exchanges WHERE uid=@uid",p.Entity.UID)!=before+1)throw new Exception("Mined housing exchange failed: "+option);
    }
    static void UseHouseReturn(GameState p)
    {
        var item=p.Inventory.Objects.First(i=>i.ID==1060020);ItemHandler.UseItem(item,p);
        Check(!Fenced(p) && p.Entity.MapID==1002 && p.Map.Floor[p.Entity.X,p.Entity.Y,MapObjectType.Player],"purchased Twin City Gate returns the ordinary housing miner");
    }
    static void VerifyScrollPurchases(Map town)
    {
        var service=typeof(Entity).Assembly.GetType("Revora.Game.Features.TravelScrollShop");
        Call(service,null,"Initialize");Call(service,null,"Initialize");ShopFile.Load();MiningNpc(town,3);
        DateTime now=new DateTime(2032,1,1,0,0,0,DateTimeKind.Utc);service.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        try {
            foreach(uint type in Enumerable.Range(1060020,5).Select(i=>(uint)i)) {
                var p=AdventureVisitor(town,3);p.Entity.Money=400;EntityTable.UpdateMoney(p);
                Parallel.For(0,10,i=>ItemHandler.HandleBuyFromNPC(BuyBook(type,2,3),p));
                var item=p.Inventory.Objects.Single();
                Check(!Fenced(p) && p.Entity.Money==0 && item.ID==type && item.StackSize==2 && Scalar("SELECT money FROM entities WHERE uid=@uid",p.Entity.UID)==0,
                    "ordinary gate stock saves exact-balance payment and one stack under duplicate requests: "+type);
                p=ReloadBall(p);Check(p.Entity.Money==0 && p.Inventory.Objects.Single().StackSize==2,"gate purchase survives production reconnect: "+type);
            }
            var merge=AdventureVisitor(town,3);var old=Pack(merge,1060020,3);while(merge.Inventory.Count<40)Pack(merge,1088001,1);
            ulong purse=merge.Entity.Money;ItemHandler.HandleBuyFromNPC(BuyBook(1060020,2,3),merge);
            Check(!Fenced(merge) && merge.Inventory.Count==40 && old.StackSize==5 && ConquerItemTable.LoadItem(old.UID).StackSize==5
                && merge.Entity.Money==purse-400,"full bag can merge owned unbound scroll stock atomically");
            now=now.AddSeconds(2);ItemUsage.Process(BuyBook(1060020,2,3).ToArray(),merge);
            Check(!Fenced(merge) && old.StackSize==7 && merge.Entity.Money==purse-800,"new purchase after duplicate window remains available");
            var modeBefore=ShopFile.Shops[3].MoneyType;ShopFile.Shops[3].MoneyType=ShopFile.MoneyType.ConquerPoints;
            try{Check(!(bool)Call(service,null,"TryBuy",merge,BuyBook(1060020,2,3)),"non-silver shops retain their existing purchase handler");}
            finally{ShopFile.Shops[3].MoneyType=modeBefore;}
            foreach(string mode in new[]{"funds","far","trade","full","bound","locked","stale-stack","wrong-stock","wrong-seal","large"}) {
                var f=AdventureVisitor(town,3);var stack=Pack(f,1060020,3);ulong before=f.Entity.Money;
                if(mode=="funds"){f.Entity.Money=399;EntityTable.UpdateMoney(f);before=399;}
                if(mode=="far")f.Entity.X+=40;if(mode=="trade")f.Trade.InTrade=true;
                if(mode=="bound"){stack.Bound=true;ConquerItemTable.UpdateBound(stack,f);}if(mode=="locked"){stack.Lock=1;Query("UPDATE items SET locked=1 WHERE uid=@uid","uid",(long)stack.UID);}
                if(mode=="full" || mode=="bound" || mode=="locked"){if(mode=="full"){stack.StackSize=ConquerItemInformation.BaseInformations[1060020].StackSize;Query("UPDATE items SET stacksize=@stack WHERE uid=@uid","stack",(int)stack.StackSize,"uid",(long)stack.UID);}while(f.Inventory.Count<40)Pack(f,1088001,1);}
                if(mode=="stale-stack")Query("UPDATE items SET stacksize=4 WHERE uid=@uid","uid",(long)stack.UID);
                var request=BuyBook(1060020,mode=="large"?41u:2u,mode=="wrong-stock"?1u:3u);
                if(mode=="wrong-seal")Revora.Network.Writer.WriteUInt64(0,request.ToArray().Length-8,request.ToArray());
                ItemUsage.Process(request.ToArray(),f);
                Check(f.Entity.Money==before && Scalar("SELECT money FROM entities WHERE uid=@uid",f.Entity.UID)==(long)before
                    && Scalar("SELECT count(*) FROM revora_scroll_purchases WHERE uid=@uid",f.Entity.UID)==0,"rejected gate purchase preserves saved payment: "+mode);
            }
            foreach(string failure in new[]{"payment","insert","merge","receipt"}) {
                var f=AdventureVisitor(town,3);ConquerItem stack=failure=="merge"?Pack(f,1060020,3):null;ulong before=f.Entity.Money;
                string table=failure=="payment"?"entities":failure=="receipt"?"revora_scroll_purchases":"items";
                string verb=failure=="payment" || failure=="merge"?"UPDATE":"INSERT";
                Query("CREATE FUNCTION suppress_scroll_write() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_scroll_write BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_scroll_write()");
                try{ItemHandler.HandleBuyFromNPC(BuyBook(1060020,2,3),f);}finally{Query("DROP TRIGGER suppress_scroll_write ON "+table+";DROP FUNCTION suppress_scroll_write()");}
                Check(Fenced(f) && Scalar("SELECT money FROM entities WHERE uid=@uid",f.Entity.UID)==(long)before
                    && Scalar("SELECT COALESCE(sum(stacksize),0) FROM items WHERE entityid=@uid AND id=1060020",f.Entity.UID)==(stack==null?0:3)
                    && Scalar("SELECT count(*) FROM revora_scroll_purchases WHERE uid=@uid",f.Entity.UID)==0,"suppressed "+failure+" rolls back scroll stock, payment and receipt");
                f=ReloadBall(f);ItemHandler.HandleBuyFromNPC(BuyBook(1060020,2,3),f);
                Check(!Fenced(f) && f.Entity.Money==before-400 && f.Inventory.Objects.Single().StackSize==(stack==null?2:5),"scroll purchase retries once after "+failure+" failure");
            }
        } finally {service.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow));}
    }
}
