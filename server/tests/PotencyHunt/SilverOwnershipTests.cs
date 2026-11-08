using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Revora.Database;
using Revora.Game;
using Revora;
using Revora.Client;

internal static partial class PotencyHuntTests
{
    static readonly Type SilverStore=typeof(Entity).Assembly.GetType("Revora.Game.Features.SilverBalance");
    static void ReloadSilver(GameState p)
    {
        DemonReset(p);
        using(var q=new MySqlCommand(MySqlCommandType.SELECT).Select("entities").Where("uid",p.Entity.UID))
        using(var r=new MySqlReader(q))
        {
            if(!r.Read())throw new Exception("Silver fixture missing.");
            p.Entity.Money=r.ReadUInt64("money");Call(SilverStore,null,"Loaded",p,r);
        }
    }
    static void VerifySilverPersistence(Map map)
    {
        var p=StoredProgressionPlayer(map);p.Entity.Money=100;EntityTable.UpdateMoney(p);
        p.Entity.AdjustMoney(25);
        Check(EntityTable.SaveEntity(p) && EntityTable.SaveEntity(p) && Scalar("SELECT money FROM entities WHERE uid=@uid",p.Entity.UID)==125,
            "pending silver saves advance the durable preimage and repeated saves remain valid");
        Query("UPDATE entities SET money=money+40 WHERE uid=@uid","uid",(long)p.Entity.UID);
        p.Entity.AdjustMoney(5);p.Entity.Strength=111;
        Check(!EntityTable.SaveEntity(p) && Fenced(p) && Scalar("SELECT money FROM entities WHERE uid=@uid",p.Entity.UID)==165
            && Scalar("SELECT strength FROM entities WHERE uid=@uid",p.Entity.UID)==100,
            "stale full save preserves external silver and rolls back unrelated pending fields");
        ReloadSilver(p);p.Entity.AdjustMoney(10);EntityTable.UpdateMoney(p);
        Check(p.Entity.Money==175 && EntityTable.SaveEntity(p),"production silver reload restores the durable preimage and spending can resume");
        Query("UPDATE entities SET money=176 WHERE uid=@uid","uid",(long)p.Entity.UID);
        bool rejected=false;try{EntityTable.UpdateMoney(p);}catch(InvalidOperationException){rejected=true;}
        Check(rejected && Fenced(p) && Scalar("SELECT money FROM entities WHERE uid=@uid",p.Entity.UID)==176,
            "explicit silver save cannot erase a newer database credit");

        var rollback=StoredProgressionPlayer(map);rollback.Entity.Money=200;EntityTable.UpdateMoney(rollback);
        lock(rollback.ItemSyncRoot)using(var c=DataHolder.MySqlConnection)
        {c.Open();using(var t=c.BeginTransaction()){Call(SilverStore,null,"Persist",c,t,rollback,150UL);t.Rollback();}}
        Check(rollback.Entity.Money==200 && Scalar("SELECT money FROM entities WHERE uid=@uid",rollback.Entity.UID)==200 && EntityTable.SaveEntity(rollback),
            "rolled-back silver transaction advances neither cached balance nor durable preimage");
        rollback.Entity.AdjustMoney(20);
        lock(rollback.ItemSyncRoot)using(var c=DataHolder.MySqlConnection)
        {c.Open();using(var t=c.BeginTransaction()){Call(SilverStore,null,"Persist",c,t,rollback,170UL);t.Commit();}Call(SilverStore,null,"ApplyCommitted",rollback,170UL);}
        Check(rollback.Entity.Money==170 && EntityTable.SaveEntity(rollback),"committed payment includes pending gains and advances the save preimage");

        var offline=StoredProgressionPlayer(map);offline.Entity.Money=(ulong)uint.MaxValue+100;EntityTable.UpdateMoney(offline);
        GameState removed;Kernel.GamePool.TryRemove(offline.Entity.UID,out removed);
        Parallel.For(0,40,i=>EntityTable.UpdateMoney(offline.Entity.UID,100));
        Check(Scalar("SELECT money FROM entities WHERE uid=@uid",offline.Entity.UID)==(long)uint.MaxValue+4100,
            "concurrent offline credits retain every delta above the old thirty-two-bit limit");
        Check(!EntityTable.SaveEntity(offline) && Fenced(offline),"an old disconnected session cannot overwrite offline credits");
        ReloadSilver(offline);Kernel.GamePool[offline.Entity.UID]=offline;
        offline.Entity.Money=100;EntityTable.UpdateMoney(offline);offline.Entity.AdjustMoney(25);
        Parallel.For(0,20,i=>EntityTable.UpdateMoney(offline.Entity.UID,5));
        Check(offline.Entity.Money==225 && Scalar("SELECT money FROM entities WHERE uid=@uid",offline.Entity.UID)==225 && EntityTable.SaveEntity(offline),
            "online auction credit serializes pending silver and immediately installs its committed balance");
        offline.Entity.Money=Entity.MaximumSilver;EntityTable.UpdateMoney(offline);Kernel.GamePool.TryRemove(offline.Entity.UID,out removed);
        rejected=false;try{EntityTable.UpdateMoney(offline.Entity.UID,1);}catch(InvalidOperationException){rejected=true;}
        Check(rejected && Scalar("SELECT money FROM entities WHERE uid=@uid",offline.Entity.UID)==long.MaxValue,
            "offline credit reports a full purse without wrapping or deleting silver");
        var firstSave=StoredProgressionPlayer(map);Call(SilverStore,null,"Forget",firstSave);
        Query("UPDATE entities SET money=25 WHERE uid=@uid","uid",(long)firstSave.Entity.UID);
        Check(!EntityTable.SaveEntity(firstSave) && Fenced(firstSave) && Scalar("SELECT money FROM entities WHERE uid=@uid",firstSave.Entity.UID)==25,
            "an uninitialized session cannot bootstrap a full save over an offline credit");
        var loginRace=StoredProgressionPlayer(map);Kernel.GamePool.TryRemove(loginRace.Entity.UID,out removed);
        using(var q=new MySqlCommand(MySqlCommandType.SELECT).Select("entities").Where("uid",loginRace.Entity.UID))
        using(var r=new MySqlReader(q))
        {
            if(!r.Read())throw new Exception("Login race fixture missing.");
            EntityTable.UpdateMoney(loginRace.Entity.UID,50);
            loginRace.Entity.Money=r.ReadUInt64("money");Call(SilverStore,null,"Loaded",loginRace,r);
        }
        Check(!EntityTable.SaveEntity(loginRace) && Fenced(loginRace) && Scalar("SELECT money FROM entities WHERE uid=@uid",loginRace.Entity.UID)==50,
            "a login snapshot read before an offline credit cannot overwrite that later credit");
        var nullable=StoredProgressionPlayer(map);Query("UPDATE entities SET money=NULL WHERE uid=@uid","uid",(long)nullable.Entity.UID);
        ReloadSilver(nullable);nullable.Entity.AdjustMoney(7);
        Check(EntityTable.SaveEntity(nullable) && Scalar("SELECT money FROM entities WHERE uid=@uid",nullable.Entity.UID)==7,
            "legacy null purse has an exact nullable preimage and can save its first credit");
        Query("UPDATE entities SET money=-1 WHERE uid=@uid","uid",(long)nullable.Entity.UID);
        rejected=false;
        using(var q=new MySqlCommand(MySqlCommandType.SELECT).Select("entities").Where("uid",nullable.Entity.UID))
        using(var r=new MySqlReader(q)){r.Read();try{Call(SilverStore,null,"Loaded",nullable,r);}catch(OverflowException){rejected=true;}}
        Check(rejected && Fenced(nullable),"negative stored silver is rejected and fenced during load");
        rejected=false;try{EntityTable.UpdateData(rollback,"Money",1);}catch(InvalidOperationException){rejected=true;}
        Check(rejected && Scalar("SELECT money FROM entities WHERE uid=@uid",rollback.Entity.UID)==170,
            "generic entity updates cannot bypass the silver persistence contract");
    }
    static void VerifySilverOwnership(Map map)
    {
        var p=StoredProgressionPlayer(map); p.Entity.Money=5000;
        Parallel.For(0,12,i=>{for(int n=0;n<100;n++)p.Entity.AdjustMoney(i<8 ? 1 : -1);});
        Check(p.Entity.Money==5400,"concurrent silver credits and deductions preserve every delta");
        p.Entity.Money=1000;
        Parallel.Invoke(()=>{for(int i=0;i<300;i++)p.Entity.AdjustMoney(1);},
            ()=>{for(int i=0;i<100;i++)p.Entity.AdjustMoney(-1);},
            ()=>{for(int i=0;i<10;i++)if(!EntityTable.SaveEntity(p))throw new Exception("Concurrent silver save failed.");},
            ()=>{for(int i=0;i<10;i++)EntityTable.UpdateMoney(p);});
        Check(EntityTable.SaveEntity(p) && p.Entity.Money==1200 && Scalar("SELECT money FROM entities WHERE uid=@uid",p.Entity.UID)==1200,
            "character saves serialize with silver deltas and preserve the final durable purse");
        using(var started=new ManualResetEventSlim())
        {
            Task blocked;
            lock(p.ItemSyncRoot)
            {
                blocked=Task.Run(()=>{started.Set();p.Entity.AdjustMoney(7);});
                if(!started.Wait(5000))throw new Exception("Silver ownership worker did not start.");
                Check(!blocked.Wait(50) && p.Entity.Money==1200,"a silver writer cannot enter a held purchase or save owner lock");
            }
            Check(blocked.Wait(5000) && p.Entity.Money==1207,"the queued silver delta applies after its owner transaction releases");
        }
        p.Entity.Money=100;
        foreach(decimal invalid in new[]{-101m,0.5m,decimal.MaxValue,decimal.MinValue})
        {
            bool rejected=false;try{p.Entity.AdjustMoney(invalid);}catch(OverflowException){rejected=true;}
            Check(rejected && p.Entity.Money==100,"invalid silver delta cannot wrap or partially mutate the purse: "+invalid);
        }
        bool unsavable=false;try{p.Entity.Money=ulong.MaxValue;}catch(OverflowException){unsavable=true;}
        Check(unsavable && p.Entity.Money==100,"absolute balance assignment rejects the unsigned range that bigint cannot persist");
        p.Entity.Money=Revora.Game.Entity.MaximumSilver;
        bool overflow=false;try{p.Entity.AdjustMoney(1);}catch(OverflowException){overflow=true;}
        Check(overflow && p.Entity.Money==Revora.Game.Entity.MaximumSilver,"maximum silver balance rejects overflow instead of wrapping to zero");
        p.Entity.AdjustMoney(-(decimal)Revora.Game.Entity.MaximumSilver);
        Check(p.Entity.Money==0,"wide silver deduction can exactly empty the entire supported purse");
        p.Entity.AdjustMoney((decimal)Revora.Game.Entity.MaximumSilver);
        Check(p.Entity.Money==Revora.Game.Entity.MaximumSilver,"wide silver credit retains the full signed-bigint balance range");
        Check(EntityTable.SaveEntity(p),"maximum supported silver purse passes the production character save");
        using(var command=new MySqlCommand(MySqlCommandType.SELECT).Select("entities").Where("UID",p.Entity.UID))
        using(var reader=new MySqlReader(command))
            Check(reader.Read() && reader.ReadUInt64("Money")==Revora.Game.Entity.MaximumSilver,"production database reader restores the maximum durable silver balance");
        p.Entity.Money=10000; ulong taken=0;
        Parallel.Invoke(()=>taken=p.Entity.TakeAllMoney(),()=>{for(int i=0;i<200;i++)p.Entity.AdjustMoney(1);});
        Check((decimal)taken+p.Entity.Money==10200,"all-in withdrawal cannot erase or duplicate a racing silver credit");
        typeof(Entity).GetField("PersistenceRequiresReload",Instance).SetValue(p.Entity,true);
        ulong before=p.Entity.Money;
        bool credit=false,absolute=false,withdraw=false;
        try{p.Entity.AdjustMoney(1);}catch(InvalidOperationException){credit=true;}
        try{p.Entity.Money=0;}catch(InvalidOperationException){absolute=true;}
        try{p.Entity.TakeAllMoney();}catch(InvalidOperationException){withdraw=true;}
        Check(credit && absolute && withdraw && p.Entity.Money==before,"fenced characters cannot credit, reset or withdraw their silver");
        DemonReset(p);p.Entity.AdjustMoney(1);
        Check(p.Entity.Money==before+1,"recovered owner can resume silver mutations");
        const ulong first=0x1234567887654321,second=0x6BCDEF0123456789;
        p.Entity.Money=first;
        Parallel.Invoke(()=>{for(int i=0;i<10000;i++)p.Entity.Money=i%2==0?first:second;},
            ()=>{for(int i=0;i<10000;i++){ulong observed=p.Entity.Money;if(observed!=first && observed!=second)throw new Exception("Torn silver balance read.");}});
        Check(p.Entity.Money==second,"x86 concurrent silver readers observe complete sixty-four-bit balances");
        VerifySilverPersistence(map);
    }
}
