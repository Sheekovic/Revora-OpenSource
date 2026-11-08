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
    static readonly Type VirtueStore=typeof(Entity).Assembly.GetType("Revora.Game.Features.VirtueBalance");
    static readonly Type Shelby=typeof(Entity).Assembly.GetType("Revora.Game.Features.ShelbyRewards");
    static void LoadVirtue(GameState p) {
        using(var q=new MySqlCommand(MySqlCommandType.SELECT).Select("entities").Where("uid",p.Entity.UID))
        using(var r=new MySqlReader(q)){if(!r.Read())throw new Exception("Virtue fixture missing");Call(VirtueStore,null,"Loaded",p,r);}
    }
    static GameState ShelbyPlayer(Map market,uint points=20000) {
        var p=CosmeticPlayer(market);p.Entity.X=240;p.Entity.Y=249;p.VirtuePoints=points;
        if(!EntityTable.SaveEntity(p))throw new Exception("Virtue fixture initial save failed");return p;
    }
    static void ShelbyNpc(GameState p,byte option) {p.ActiveNpc=30000;Npcs.GetDialog(new NpcRequest(true){NpcID=30000,OptionID=option},p);}
    static long SavedVirtue(GameState p) {return Scalar("SELECT virtuepoints FROM entities WHERE uid=@uid",p.Entity.UID);}
    static long ShelbyReceipts(GameState p) {return Scalar("SELECT count(*) FROM revora_shelby_rewards WHERE uid=@uid",p.Entity.UID);}
    static void VerifyShelby(string runtime,Map town) {
        Query("DROP TABLE entities;CREATE TABLE entities AS TABLE public.entities WITH NO DATA;CREATE TABLE titles (LIKE public.titles INCLUDING ALL)");
        StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
        Call(EvidenceType,null,"Initialize");Call(Shelby,null,"Initialize");Call(Shelby,null,"Initialize");
        var market=World(runtime,1036);market.Npcs[30000]=new NpcSpawn{UID=30000,MapID=1036,X=240,Y=249};
        ConquerItem.ItemUID=new Counter(5100000);var now=new DateTime(2026,9,15,23,0,0,DateTimeKind.Utc);Shelby.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>now));
        var p=ShelbyPlayer(market);ShelbyNpc(p,10);ShelbyNpc(p,8);ShelbyNpc(p,7);
        Check(p.Account.State==AccountTable.AccountState.Player && !Fenced(p) && p.VirtuePoints==20000 && p.Inventory.Count==0,"ordinary player confirmations need a matching fresh offer");
        ShelbyNpc(p,9);Parallel.For(0,12,i=>ShelbyNpc(p,10));
        Check(!Fenced(p) && p.VirtuePoints==15000 && SavedVirtue(p)==15000 && p.Inventory.Count==1 && p.Inventory.Objects[0].ID==1088001
            && !p.Inventory.Objects[0].Bound && ShelbyReceipts(p)==1 && HasAchievement(p,10110),"native Shelby Meteor exchange consumes 5000 Virtue once and immediately earns 10110");
        Check(EntityTable.SaveEntity(p) && SavedVirtue(p)==15000,"full save cannot refund a committed Virtue exchange");
        ShelbyNpc(p,9);ShelbyNpc(p,10);Check(p.VirtuePoints==10000 && p.Inventory.Count==2 && ShelbyReceipts(p)==2,"a second deliberate offer buys one more Meteor");
        ShelbyNpc(p,9);ShelbyNpc(p,8);Check(p.VirtuePoints==10000 && ShelbyReceipts(p)==2,"Meteor offer cannot authorize the experience recipe");
        now=now.AddMinutes(1);ShelbyNpc(p,10);Check(p.VirtuePoints==10000,"expired offer cannot charge");
        var poor=ShelbyPlayer(market,4999);ShelbyNpc(poor,9);ShelbyNpc(poor,10);Check(poor.Inventory.Count==0 && SavedVirtue(poor)==4999,"one point below price preserves the balance");
        var exact=ShelbyPlayer(market,5000);ShelbyNpc(exact,9);ShelbyNpc(exact,10);Check(!Fenced(exact) && SavedVirtue(exact)==0 && exact.Inventory.Count==1,"exact Meteor price is accepted");
        var full=ShelbyPlayer(market);for(int i=0;i<40;i++)Pack(full,1088001,1);ShelbyNpc(full,9);ShelbyNpc(full,10);
        Check(full.Inventory.Count==40 && full.VirtuePoints==20000 && ShelbyReceipts(full)==0,"full bag rejects Meteor payment before deduction");
        var exp=ShelbyPlayer(market);ulong beforeExp=exp.Entity.Experience,award=exp.ExpBall;ShelbyNpc(exp,11);Parallel.For(0,10,i=>ShelbyNpc(exp,8));
        Check(!Fenced(exp) && exp.VirtuePoints==5000 && SavedVirtue(exp)==5000 && exp.Inventory.Count==0 && ShelbyReceipts(exp)==1
            && Scalar("SELECT experience FROM revora_shelby_rewards WHERE uid=@uid",exp.Entity.UID)==checked((long)award),"experience exchange grants one calculated EXP Ball directly, never a ten-ball pack");
        Check(exp.Entity.Experience==beforeExp+award && EntityTable.SaveEntity(exp) && SavedVirtue(exp)==5000
            && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10110",exp.Entity.UID)==0,"experience progress survives full save and does not earn the Meteor achievement");
        ShelbyNpc(full,11);ShelbyNpc(full,8);Check(!Fenced(full) && full.Inventory.Count==40 && full.VirtuePoints==5000,"direct experience exchange works with a full bag");
        var capped=ShelbyPlayer(market);capped.Entity.Level=Config.LevelCap;ShelbyNpc(capped,11);ShelbyNpc(capped,8);
        Check(!Fenced(capped) && capped.VirtuePoints==20000 && ShelbyReceipts(capped)==0,"level cap rejects experience payment");
        var wallet=ShelbyPlayer(market);wallet.Entity.Money=200500000000;EntityTable.UpdateMoney(wallet);ShelbyNpc(wallet,6);Parallel.For(0,10,i=>ShelbyNpc(wallet,7));
        Check(!Fenced(wallet) && wallet.Entity.Money==0 && wallet.VirtuePoints==20000 && wallet.Inventory.Count==1 && wallet.Inventory.Objects[0].ID==723723
            && ShelbyReceipts(wallet)==1 && EntityTable.SaveEntity(wallet),"existing Revora wallet price and item persist atomically without a duplicate charge");
        var earned=ShelbyPlayer(market,100);Call(typeof(GameState),earned,"AddVirtuePoints",5000u);ShelbyNpc(earned,9);ShelbyNpc(earned,10);
        Check(!Fenced(earned) && earned.VirtuePoints==100 && SavedVirtue(earned)==100 && earned.Inventory.Count==1,"newly earned unflushed Virtue can be spent against its saved preimage");
        foreach(string fault in new[]{"balance","item","receipt","achievement"}) {
            var f=ShelbyPlayer(market);ShelbyNpc(f,9);
            if(fault=="balance")Query("UPDATE entities SET virtuepoints=90000 WHERE uid=@uid","uid",(long)f.Entity.UID);
            string table=fault=="item"?"items":fault=="receipt"?"revora_shelby_rewards":"revora_achievement_evidence";
            if(fault!="balance")Query("CREATE FUNCTION suppress_shelby() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER suppress_shelby BEFORE INSERT ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_shelby()");
            try{ShelbyNpc(f,10);}finally{if(fault!="balance")Query("DROP TRIGGER suppress_shelby ON "+table+";DROP FUNCTION suppress_shelby()");}
            Check(Fenced(f) && f.VirtuePoints==20000 && SavedVirtue(f)==(fault=="balance"?90000:20000) && f.Inventory.Count==0 && ShelbyReceipts(f)==0,"failed "+fault+" rolls back Meteor, Virtue and achievement");
        }
        foreach(byte kind in new byte[]{2,3}) {
            var f=ShelbyPlayer(market);if(kind==3){f.Entity.Money=200500000000;EntityTable.UpdateMoney(f);}ulong oldExp=f.Entity.Experience,oldMoney=f.Entity.Money;
            ShelbyNpc(f,kind==2?(byte)11:(byte)6);
            Query("CREATE FUNCTION fail_shelby_receipt() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER fail_shelby_receipt BEFORE INSERT ON revora_shelby_rewards FOR EACH ROW EXECUTE FUNCTION fail_shelby_receipt()");
            try{ShelbyNpc(f,kind==2?(byte)8:(byte)7);}finally{Query("DROP TRIGGER fail_shelby_receipt ON revora_shelby_rewards;DROP FUNCTION fail_shelby_receipt()");}
            Check(Fenced(f) && SavedVirtue(f)==20000 && f.VirtuePoints==20000 && f.Entity.Experience==oldExp && f.Entity.Money==oldMoney
                && Scalar("SELECT money FROM entities WHERE uid=@uid",f.Entity.UID)==checked((long)oldMoney) && Scalar("SELECT experience FROM entities WHERE uid=@uid",f.Entity.UID)==checked((long)oldExp)
                && f.Inventory.Count==0 && ShelbyReceipts(f)==0,"failed receipt rolls back every field for recipe "+kind);
        }
        var save=ShelbyPlayer(market);Query("UPDATE entities SET virtuepoints=77777 WHERE uid=@uid","uid",(long)save.Entity.UID);
        Check(!EntityTable.SaveEntity(save) && Fenced(save) && SavedVirtue(save)==77777,"stale full character save cannot overwrite an external Virtue change");
        foreach(long invalid in new[]{-1L,4294967296L}) {
            var f=ShelbyPlayer(market);Query("UPDATE entities SET virtuepoints=@points WHERE uid=@uid","points",invalid,"uid",(long)f.Entity.UID);bool failed=false;
            try{LoadVirtue(f);}catch(OverflowException){failed=true;}
            Check(failed && Fenced(f),"invalid stored Virtue is rejected: "+invalid);
        }
        var nullable=ShelbyPlayer(market,0);Query("UPDATE entities SET virtuepoints=NULL WHERE uid=@uid","uid",(long)nullable.Entity.UID);LoadVirtue(nullable);Call(typeof(GameState),nullable,"AddVirtuePoints",5000u);ShelbyNpc(nullable,9);ShelbyNpc(nullable,10);
        Check(!Fenced(nullable) && SavedVirtue(nullable)==0 && nullable.Inventory.Count==1,"nullable legacy Virtue preimage supports a legitimate earned Meteor");
        var race=ShelbyPlayer(market,100000);
        Parallel.Invoke(()=>{for(int i=0;i<8;i++){ShelbyNpc(race,9);ShelbyNpc(race,10);}},()=>{for(int i=0;i<8;i++)if(!EntityTable.SaveEntity(race))throw new Exception("Concurrent Virtue save failed");},()=>{for(int i=0;i<8;i++)Call(typeof(GameState),race,"AddVirtuePoints",100u);});
        Check(!Fenced(race) && race.VirtuePoints==60800 && race.Inventory.Count==8 && EntityTable.SaveEntity(race) && SavedVirtue(race)==60800,"concurrent earning, exchanges and full saves retain each debit and credit exactly once");
        foreach(string state in new[]{"far","trade","fake","dead"}) {
            var f=ShelbyPlayer(market);ShelbyNpc(f,9);if(state=="far")f.Entity.X=10;if(state=="trade")f.Trade.InTrade=true;if(state=="fake")f.Fake=true;if(state=="dead")f.Entity.Hitpoints=0;ShelbyNpc(f,10);
            Check(!Fenced(f) && f.VirtuePoints==20000 && f.Inventory.Count==0,"Shelby rejects ineligible claimant: "+state);
        }
        foreach(string table in new[]{"teamarena","championarena","quests","status","reward","rewardclameid","merchant","dialyquests","activeness","house","pkexploits","achievement","itemadding"})
            Query("CREATE TABLE IF NOT EXISTS "+table+" AS TABLE public."+table+" WITH NO DATA");
        Query("CREATE TABLE prizes(uid bigint PRIMARY KEY,name text,prizes bytea)");
        p.LastResetTime=DateTime.Now;if(!EntityTable.SaveEntity(p))throw new Exception("Shelby reconnect save failed");ShelbyNpc(p,9);
        var reloaded=ShelbyPlayer(market);uint placeholder=reloaded.Entity.UID;reloaded.Account.EntityID=p.Entity.UID;reloaded.Account.Username="shelby_fixture";reloaded.Quests=new Quests(reloaded);
        Check((bool)Call(typeof(EntityTable),null,"LoadEntityState",reloaded),"production character loader restores the exchanged Virtue preimage");
        Kernel.GamePool.Remove(placeholder);Kernel.GamePool[p.Entity.UID]=reloaded;reloaded.Action=2;ConquerItemTable.LoadItems(reloaded);EvidenceRefresh(reloaded);ShelbyNpc(reloaded,10);
        Check(!Fenced(reloaded) && reloaded.VirtuePoints==10000 && reloaded.Inventory.Count==2 && ShelbyReceipts(reloaded)==2 && HasAchievement(reloaded,10110),"reconnect restores Meteors and achievement but never inherits an outstanding offer");
        ShelbyNpc(reloaded,9);ShelbyNpc(reloaded,10);
        Check(!Fenced(reloaded) && reloaded.VirtuePoints==5000 && reloaded.Inventory.Count==3 && EntityTable.SaveEntity(reloaded) && SavedVirtue(reloaded)==5000,"fresh session can deliberately exchange again without refunding prior fees");

    }
}
