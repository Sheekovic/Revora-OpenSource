using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;

internal static partial class PotencyHuntTests
{
    static readonly Type Cosmetics = typeof(Entity).Assembly.GetType("Revora.Game.Features.CosmeticOwnership");
    static object Cosmetic(GameState p,uint action,uint first,uint second) { return Call(Cosmetics,null,"Operate",p,action,first,second); }
    static object CosmeticField(object value,string field) { return value.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(value); }
    static ulong Charge(object value) { return (ulong)CosmeticField(value,"Charged"); }
    static GameState CosmeticPlayer(Map map) {
        var p=AppearancePlayer(map);p.Entity.Class=15;p.Entity.Money=50000;EntityTable.UpdateMoney(p);return p;
    }
    static Array CosmeticsSnapshot(GameState p) { return (Array)Call(Cosmetics,null,"Snapshot",p); }
    static void VerifyCosmeticOwnership(Map map) {
        Query("DROP TABLE entities; CREATE TABLE entities AS TABLE public.entities WITH NO DATA");
        typeof(Entity).Assembly.GetType("Revora.Database.AvaterFaces").GetMethod("Load",BindingFlags.Static|BindingFlags.Public).Invoke(null,null);
        Call(Cosmetics,null,"Initialize");Call(Cosmetics,null,"Initialize");Call(EvidenceType,null,"Initialize");
        Query("CREATE TABLE titles (LIKE public.titles INCLUDING ALL)");StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
        var p=CosmeticPlayer(map);
        Check(p.Account.State==AccountTable.AccountState.Player && CosmeticsSnapshot(p).Length==0,"ordinary existing appearance does not fabricate paid ownership");
        Check(Cosmetic(p,3,0,3)==null && Cosmetic(p,6,2,1)==null && Cosmetic(p,7,2,1)==null && p.Entity.Money==50000,"selection and colour purchase require that character's saved style");
        var bought=Cosmetic(p,4,0,3);
        Check(bought!=null && Charge(bought)==500 && p.Entity.Money==49500 && p.Entity.HairStyle==2
            && Scalar("SELECT count(*) FROM revora_cosmetic_ownership WHERE uid=@uid AND kind=0 AND id=3",p.Entity.UID)==1,"purchase saves one entitlement and deducts installed silver price without applying appearance");
        Check(Charge(Cosmetic(p,4,0,3))==0 && p.Entity.Money==49500 && Scalar("SELECT count(*) FROM revora_cosmetic_receipts WHERE uid=@uid",p.Entity.UID)==1,"replayed acquisition neither charges nor appends a second receipt");
        Check(Cosmetic(p,3,0,3)!=null && p.Entity.HairStyle==3 && Scalar("SELECT hairstyle FROM entities WHERE uid=@uid",p.Entity.UID)==3 && EntityTable.SaveEntity(p),"owned hairstyle selection persists immediately and survives the next character save");
        Check(!(bool)CosmeticField(Cosmetic(p,3,0,3),"Changed") && Scalar("SELECT count(*) FROM revora_cosmetic_receipts WHERE uid=@uid",p.Entity.UID)==2,"replayed selection does not create a new change");
        Check(Cosmetic(p,6,3,6)==null && p.Entity.HairStyle==3,"unowned colour cannot be selected");
        Check(Charge(Cosmetic(p,7,3,6))==1000 && p.Entity.Money==48500 && p.Entity.HairStyle==3,"colour purchase spends 1000 silver and leaves selection to the separate request");
        Check(Charge(Cosmetic(p,7,3,6))==0 && p.Entity.Money==48500,"repeated colour purchase is free and idempotent");
        Check(Cosmetic(p,6,3,6)!=null && p.Entity.HairStyle==6003 && Scalar("SELECT hairstyle FROM entities WHERE uid=@uid",p.Entity.UID)==6003,"selected colour uses native colour times 1000 plus style encoding");
        LoadAppearance(p);ReloadSilver(p);
        var snapshot=CosmeticsSnapshot(p);var owned=snapshot.GetValue(0);
        Check(snapshot.Length==1 && (uint)CosmeticField(owned,"Colour")==6 && (uint)CosmeticField(owned,"Colours")==65 && (bool)CosmeticField(owned,"Active"),"fresh durable snapshot retains selected colour, ownership mask and current style");
        Check(Charge(Cosmetic(p,4,0,3))==0 && Charge(Cosmetic(p,7,3,6))==0 && p.Entity.Money==48500,"reload preserves replay protection without an ownership cache");
        Check(Cosmetic(p,6,3,0)!=null && p.Entity.HairStyle==3 && p.Entity.Money==48500,"default colour zero can be reselected free of charge");
        var foreign=CosmeticPlayer(map);
        Check(Cosmetic(foreign,3,0,3)==null && Cosmetic(foreign,7,3,6)==null && CosmeticsSnapshot(foreign).Length==0,"another character cannot borrow purchased styles or colours");
        foreach(uint action in new uint[]{0,1,2,5,8,9,10,uint.MaxValue})Check(Cosmetic(p,action,0,3)==null,"transaction service rejects unsupported action "+action);
        foreach(uint[] args in new[]{new uint[]{4,2,3},new uint[]{4,0,0},new uint[]{4,0,1000},new uint[]{4,1,65536},
            new uint[]{7,3,0},new uint[]{7,3,7},new uint[]{6,3,uint.MaxValue},new uint[]{4,0,42},new uint[]{4,0,28}})
            Check(Cosmetic(p,args[0],args[1],args[2])==null,"malformed, missing catalog and event-only requests make no mutation");
        p.Entity.VIPLevel=0;Check(Cosmetic(p,4,0,8)==null,"VIP hairstyle purchase enforces the installed level-four requirement");
        p.Entity.VIPLevel=4;Check(Charge(Cosmetic(p,4,0,8))==500,"eligible VIP buys the same permanent entitlement");
        p.Entity.VIPLevel=0;Check(Cosmetic(p,3,0,8)!=null && Charge(Cosmetic(p,4,0,8))==0,"previously bought style remains selectable after VIP expiry");
        Check(Cosmetic(p,4,1,154)==null,"pirate avatar cannot be purchased by another profession");
        p.Entity.Class=75;Check(Charge(Cosmetic(p,4,1,154))==500 && Cosmetic(p,3,1,154)!=null && p.Entity.Face==154,"eligible ordinary male pirate buys and selects his avatar");
        Check(Cosmetic(p,4,1,345)==null,"male character cannot buy the female-only pirate avatar");
        p.Entity.Body=2001;Check(Charge(Cosmetic(p,4,1,345))==500 && Cosmetic(p,3,1,345)!=null && p.Entity.Face==345 && EntityTable.SaveEntity(p),"female pirate selection preserves legitimate pending body change");
        Check(Cosmetic(p,3,1,154)==null,"stored opposite-sex avatar cannot be selected after body change");
        p.Entity.Class=15;Check(Cosmetic(p,3,1,345)==null,"stored profession-specific avatar cannot bypass changed profession");
        var poor=CosmeticPlayer(map);poor.Entity.Money=499;EntityTable.UpdateMoney(poor);
        Check(Cosmetic(poor,4,0,3)==null && CosmeticsSnapshot(poor).Length==0 && poor.Entity.Money==499,"insufficient silver creates no entitlement or receipt");
        poor.Entity.Money=500;EntityTable.UpdateMoney(poor);Cosmetic(poor,4,0,3);
        Check(poor.Entity.Money==0 && Cosmetic(poor,7,3,1)==null,"exact style payment succeeds and insufficient dye payment does not grant a colour");
        var inactive=CosmeticPlayer(map);Cosmetic(inactive,4,0,3);Cosmetic(inactive,7,3,4);Cosmetic(inactive,6,3,4);
        Check(inactive.Entity.HairStyle==2 && Cosmetic(inactive,3,0,3)!=null && inactive.Entity.HairStyle==4003,"editing an unworn stored style applies its remembered colour when selected");
        var noColours=CosmeticPlayer(map);noColours.Entity.VIPLevel=4;Cosmetic(noColours,4,0,43);
        Check(Cosmetic(noColours,7,43,1)==null && Cosmetic(noColours,6,43,0)!=null,"style without colour catalog accepts its default but no invented paid colours");
        foreach(string target in new[]{"revora_cosmetic_ownership","revora_cosmetic_receipts"}) {
            var fault=CosmeticPlayer(map);
            Query("CREATE FUNCTION suppress_cosmetic_insert() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER suppress_cosmetic_insert BEFORE INSERT ON "+target+" FOR EACH ROW EXECUTE FUNCTION suppress_cosmetic_insert()");
            bool failed=false;try{Cosmetic(fault,4,0,3);}catch(InvalidOperationException){failed=true;}
            finally{Query("DROP TRIGGER suppress_cosmetic_insert ON "+target+"; DROP FUNCTION suppress_cosmetic_insert()");}
            Check(failed && fault.Entity.Money==50000 && CosmeticsSnapshot(fault).Length==0 && Scalar("SELECT money FROM entities WHERE uid=@uid",fault.Entity.UID)==50000 && EntityTable.SaveEntity(fault),"suppressed "+target+" insert rolls back ownership and payment without poisoning retry");
            Check(Charge(Cosmetic(fault,4,0,3))==500,"rolled-back acquisition retries once with its unchanged preimage");
        }
        var colourFault=CosmeticPlayer(map);Cosmetic(colourFault,4,0,3);
        Query("CREATE FUNCTION suppress_cosmetic_update() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER suppress_cosmetic_update BEFORE UPDATE ON revora_cosmetic_ownership FOR EACH ROW EXECUTE FUNCTION suppress_cosmetic_update()");
        bool colourFailed=false;try{Cosmetic(colourFault,7,3,1);}catch(InvalidOperationException){colourFailed=true;}
        finally{Query("DROP TRIGGER suppress_cosmetic_update ON revora_cosmetic_ownership; DROP FUNCTION suppress_cosmetic_update()");}
        Check(colourFailed && colourFault.Entity.Money==49500 && (uint)CosmeticField(CosmeticsSnapshot(colourFault).GetValue(0),"Colours")==1,"suppressed colour update cannot debit silver");
        Cosmetic(colourFault,7,3,1);
        Query("CREATE FUNCTION suppress_cosmetic_receipt() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER suppress_cosmetic_receipt BEFORE INSERT ON revora_cosmetic_receipts FOR EACH ROW EXECUTE FUNCTION suppress_cosmetic_receipt()");
        bool selectionFailed=false;try{Cosmetic(colourFault,3,0,3);}catch(InvalidOperationException){selectionFailed=true;}
        finally{Query("DROP TRIGGER suppress_cosmetic_receipt ON revora_cosmetic_receipts; DROP FUNCTION suppress_cosmetic_receipt()");}
        Check(selectionFailed && colourFault.Entity.HairStyle==2 && Scalar("SELECT hairstyle FROM entities WHERE uid=@uid",colourFault.Entity.UID)==2 && EntityTable.SaveEntity(colourFault),"selection receipt failure rolls back appearance with no cache projection");
        Query("CREATE FUNCTION suppress_cosmetic_receipt() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER suppress_cosmetic_receipt BEFORE INSERT ON revora_cosmetic_receipts FOR EACH ROW EXECUTE FUNCTION suppress_cosmetic_receipt()");
        bool dyeFailed=false;try{Cosmetic(colourFault,7,3,6);}catch(InvalidOperationException){dyeFailed=true;}
        finally{Query("DROP TRIGGER suppress_cosmetic_receipt ON revora_cosmetic_receipts; DROP FUNCTION suppress_cosmetic_receipt()");}
        Check(dyeFailed && colourFault.Entity.Money==48500 && (uint)CosmeticField(CosmeticsSnapshot(colourFault).GetValue(0),"Colours")==3
            && Scalar("SELECT money FROM entities WHERE uid=@uid",colourFault.Entity.UID)==48500,"failed dye receipt rolls back both colour mask and silver debit");
        foreach(string column in new[]{"money","body","face","hairstyle"}) {
            var stale=CosmeticPlayer(map);Query("UPDATE entities SET "+column+"=77 WHERE uid=@uid","uid",(long)stale.Entity.UID);
            bool rejected=false;try{Cosmetic(stale,4,0,3);}catch(InvalidOperationException){rejected=true;}
            Check(rejected && stale.Entity.Money==50000 && Scalar("SELECT count(*) FROM revora_cosmetic_ownership WHERE uid=@uid",stale.Entity.UID)==0 && Scalar("SELECT "+column+" FROM entities WHERE uid=@uid",stale.Entity.UID)==77,"stale "+column+" preimage prevents a paid ownership mutation");
        }
        var race=CosmeticPlayer(map);
        Parallel.Invoke(()=>{for(int i=0;i<20;i++)Cosmetic(race,4,0,3);},()=>{for(int i=0;i<20;i++)if(!EntityTable.SaveEntity(race))throw new Exception("Cosmetic save race failed");});
        Check(race.Entity.Money==49500 && Scalar("SELECT count(*) FROM revora_cosmetic_receipts WHERE uid=@uid",race.Entity.UID)==1,"concurrent duplicate purchases and character saves charge exactly once");
        Parallel.Invoke(()=>{for(int i=0;i<20;i++)Cosmetic(race,7,3,6);},()=>{for(int i=0;i<20;i++)if(!EntityTable.SaveEntity(race))throw new Exception("Colour save race failed");});
        Check(race.Entity.Money==48500 && Scalar("SELECT count(*) FROM revora_cosmetic_receipts WHERE uid=@uid",race.Entity.UID)==2,"concurrent colour purchases and character saves charge exactly once");
        Parallel.Invoke(()=>{for(int i=0;i<20;i++){Cosmetic(race,3,0,3);Cosmetic(race,6,3,(uint)(i%2==0?0:6));}},
            ()=>{for(int i=0;i<20;i++)if(!EntityTable.SaveEntity(race))throw new Exception("Selection save race failed");});
        Check(race.Entity.HairStyle==6003 && Scalar("SELECT hairstyle FROM entities WHERE uid=@uid",race.Entity.UID)==6003 && EntityTable.SaveEntity(race),"interleaved selections and full character saves retain the final committed appearance");
        var fenced=CosmeticPlayer(map);typeof(Entity).GetField("PersistenceRequiresReload",Instance).SetValue(fenced.Entity,true);
        Check(Cosmetic(fenced,4,0,3)==null && CosmeticsSnapshot(fenced).Length==0,"fenced player cannot use cosmetic persistence");
        var fake=CosmeticPlayer(map);fake.Fake=true;Check(Cosmetic(fake,4,0,3)==null,"synthetic adventurer cannot buy player cosmetics");
        var trading=CosmeticPlayer(map);trading.Trade.InTrade=true;Check(Cosmetic(trading,4,0,3)==null,"active trade prevents cosmetic payment");
        Check(Scalar("SELECT count(*) FROM revora_cosmetic_receipts WHERE charged<0 OR charged NOT IN(0,500,1000,10000)",p.Entity.UID)==0,"all committed receipt prices follow installed silver amounts");
        VerifyCosmeticHandler(map);VerifyRetiredDyes(map);VerifyRetiredTulips(map);VerifyArmorDyes(map);
    }
}
