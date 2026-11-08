using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.ConquerStructures;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type Jades=typeof(Entity).Assembly.GetType("Revora.Game.Features.DisCityJades");
    static GameState JadePlayer(Map map,uint points=0) {
        var p=LoadoutPlayer(map);p.ChiPowers=new List<ChiPowerStructure>();p.Retretead_ChiPowers=new List<ChiPowerStructure>();
        ChiTable.Load(p);SetJadeChi(p,points);return p;
    }
    static void SetJadeChi(GameState p,uint points) {Query("UPDATE chi SET points=@points WHERE uid=@uid","points",(long)points,"uid",(long)p.Entity.UID);ChiTable.Load(p);}
    static long JadeChi(GameState p) {return Scalar("SELECT points FROM chi WHERE uid=@uid",p.Entity.UID);}
    static long JadeUses(GameState p) {return Scalar("SELECT count(*) FROM revora_dis_jade_uses WHERE uid=@uid",p.Entity.UID);}
    static void UseJade(GameState p,ConquerItem item) {ItemHandler.UseItem(item,p);}
    static void VerifyJades(Map map) {
        Query("ALTER TABLE entities ADD COLUMN level integer DEFAULT 140,ADD COLUMN my_title integer DEFAULT 0,ADD COLUMN conquerpoints bigint DEFAULT 0");
        Query("CREATE TABLE titles (LIKE public.titles INCLUDING ALL);CREATE TABLE chi (LIKE public.chi INCLUDING ALL)");
        StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);ConquerItem.ItemUID=new Counter(8500000);
        Call(Jades,null,"Initialize");var now=DateTime.UtcNow;Jades.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        foreach(uint type in new uint[]{3008059,3008060}) {
            var p=JadePlayer(map);var single=Pack(p,type,1);Parallel.For(0,16,i=>UseJade(p,single));
            Check(!Fenced(p) && p.Account.State==AccountTable.AccountState.Player && p.ChiPoints==300 && JadeChi(p)==300 && p.Inventory.Count==0 && JadeUses(p)==1,"either single jade grants an ordinary player 300 Chi exactly once");
            ChiTable.Load(p);UseJade(p,single);Check(p.ChiPoints==300 && JadeUses(p)==1,"production Chi reload cannot replay a consumed jade");
        }
        var pair=JadePlayer(map,1000);var yin=Pack(pair,3008059,3);var yang=Pack(pair,3008060,3);
        Parallel.For(0,16,i=>UseJade(pair,i%2==0?yin:yang));
        Check(!Fenced(pair) && pair.ChiPoints==2200 && JadeChi(pair)==2200 && yin.StackSize==2 && yang.StackSize==2 && JadeUses(pair)==1,"opposite simultaneous requests consume one Yin/Yang pair and grant 1200 Chi");
        now=now.AddSeconds(1);UseJade(pair,yang);
        Check(pair.ChiPoints==3400 && yin.StackSize==1 && yang.StackSize==1,"a later deliberate Yang use consumes the next pair");
        now=now.AddSeconds(1);UseJade(pair,yin);
        Check(pair.ChiPoints==4600 && pair.Inventory.Count==0 && JadeUses(pair)==3,"last units of both stacks are removed together");
        foreach(uint before in new uint[]{0,4000,7999,8000,8001,uint.MaxValue})foreach(bool paired in new[]{false,true}) {
            var p=JadePlayer(map,before);var a=Pack(p,3008059,1);if(paired)Pack(p,3008060,1);UseJade(p,a);
            uint expected=before<8000?before+(paired?1200u:300u):before;
            Check(!Fenced(p) && p.ChiPoints==expected && JadeChi(p)==expected && p.Inventory.Count==(before<8000?0:paired?2:1),"below-8000 eligibility preserves the full jade reward at boundary "+before+" paired="+paired);
        }
        var full=JadePlayer(map);var fullJade=Pack(full,3008059,1);for(int i=0;i<39;i++)Pack(full,1088001,1);UseJade(full,fullJade);
        Check(full.ChiPoints==300 && full.Inventory.Count==39,"a full bag can consume a jade because no replacement item is created");
        var bound=JadePlayer(map);var boundYin=Pack(bound,3008059,1);boundYin.Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)boundYin.UID);Pack(bound,3008060,1);UseJade(bound,boundYin);
        Check(bound.ChiPoints==1200 && bound.Inventory.Count==0,"bound and unbound jades can combine into nontransferable Chi");
        var locked=JadePlayer(map);var lockedYin=Pack(locked,3008059,1);var lockedYang=Pack(locked,3008060,1);lockedYang.Lock=1;Query("UPDATE items SET locked=1 WHERE uid=@uid","uid",(long)lockedYang.UID);UseJade(locked,lockedYang);
        Check(locked.ChiPoints==0 && locked.Inventory.Count==2,"locked selected jade cannot be consumed");UseJade(locked,lockedYin);
        Check(locked.ChiPoints==300 && locked.Inventory.ContainsUID(lockedYang.UID),"an unusable opposite jade is preserved during single use");
        var denied=JadePlayer(map);var kept=Pack(denied,3008059,1);
        denied.Fake=true;UseJade(denied,kept);denied.Fake=false;denied.Entity.FullyLoaded=false;UseJade(denied,kept);denied.Entity.FullyLoaded=true;denied.Trade.InTrade=true;UseJade(denied,kept);denied.Trade.InTrade=false;
        UseJade(denied,new ConquerItem(true){UID=kept.UID,ID=kept.ID,StackSize=1});
        Check(denied.ChiPoints==0 && denied.Inventory.ContainsUID(kept.UID),"fake, unloaded, trading and forged-reference requests cannot spend a jade");
        var outsider=JadePlayer(map);UseJade(outsider,kept);Check(outsider.ChiPoints==0 && denied.Inventory.ContainsUID(kept.UID),"foreign jade references do not establish ownership");
        Query("UPDATE items SET entityid=@owner WHERE uid=@uid","owner",(long)outsider.Entity.UID,"uid",(long)kept.UID);UseJade(denied,kept);
        Check(Fenced(denied) && JadeChi(denied)==0 && JadeUses(denied)==0,"changed durable jade ownership rolls back the Chi credit");
        var rollback=JadePlayer(map);var ra=Pack(rollback,3008059,2);var rb=Pack(rollback,3008060,2);
        Query("ALTER TABLE revora_dis_jade_uses ADD CONSTRAINT jade_receipt_failure CHECK(false) NOT VALID");UseJade(rollback,ra);
        Check(Fenced(rollback) && JadeChi(rollback)==0 && ra.StackSize==2 && rb.StackSize==2 && SavedRune(ra,"stacksize")==2 && SavedRune(rb,"stacksize")==2,"receipt failure rolls back both consumed units and the wallet");
        Query("ALTER TABLE revora_dis_jade_uses DROP CONSTRAINT jade_receipt_failure");DemonReset(rollback);UseJade(rollback,ra);
        Check(!Fenced(rollback) && JadeChi(rollback)==1200 && JadeUses(rollback)==1,"failed pair use can be retried without duplicate payment");
        var dirty=JadePlayer(map);var pending=Pack(dirty,3008059,1);dirty.AdjustChiPoints(500);UseJade(dirty,pending);
        Check(!Fenced(dirty) && dirty.ChiPoints==500 && JadeChi(dirty)==0 && dirty.Inventory.ContainsUID(pending.UID),"unsaved legacy Chi gains defer jade use without overwriting either balance");
        ChiTable.Save(dirty);UseJade(dirty,pending);ChiTable.Save(dirty);ChiTable.Load(dirty);
        Check(!Fenced(dirty) && dirty.ChiPoints==800 && JadeChi(dirty)==800,"background Chi saves preserve committed jade credit across reload");
        var external=JadePlayer(map,500);var externalJade=Pack(external,3008059,1);Query("UPDATE chi SET points=700 WHERE uid=@uid","uid",(long)external.Entity.UID);UseJade(external,externalJade);
        Check(Fenced(external) && JadeChi(external)==700 && external.Inventory.ContainsUID(externalJade.UID),"external purse changes cannot be overwritten by a jade claim");
        DemonReset(external);ChiTable.Load(external);external.AdjustChiPoints(1);Query("UPDATE chi SET points=900 WHERE uid=@uid","uid",(long)external.Entity.UID);ChiTable.Save(external);
        Check(Fenced(external) && JadeChi(external)==900,"a stale heartbeat save cannot overwrite an external Chi revision");
        var parallel=JadePlayer(map);var parallelJade=Pack(parallel,3008059,1);Parallel.Invoke(()=>UseJade(parallel,parallelJade),()=>{for(int i=0;i<20;i++){parallel.AdjustChiPoints(1);ChiTable.Save(parallel);}});
        UseJade(parallel,parallelJade);ChiTable.Save(parallel);ChiTable.Load(parallel);
        Check(!Fenced(parallel) && parallel.ChiPoints==320 && JadeUses(parallel)==1,"concurrent atomic legacy increments, saves and jade use preserve every Chi point");
        var saved=JadePlayer(map);var power=new ChiPowerStructure(Enums.ChiPowerType.Dragon){Attributes=new[]{new ChiAttribute(Enums.ChiAttribute.HPAdd,3500),new ChiAttribute(Enums.ChiAttribute.AddMagicAttack,2500),new ChiAttribute(Enums.ChiAttribute.CriticalStrike,200),new ChiAttribute(Enums.ChiAttribute.Immunity,200)}};
        saved.ChiPowers.Add(power);power.Retreat_TimeLeft=DateTime.Now.AddDays(5);saved.Retretead_ChiPowers.Add(power);ChiTable.Save(saved);ChiTable.Load(saved);ChiTable.Load(saved);
        Check(!Fenced(saved) && saved.ChiPowers.Count==1 && saved.Retretead_ChiPowers.Count==1 && saved.Retretead_ChiPowers[0].Retreat_TimeLeft.Ticks==power.Retreat_TimeLeft.Ticks,"production Chi reload preserves saved powers and retreat time without duplicate entries or empty-list indexing");
        var unsaved=LoadoutPlayer(map);var unready=Pack(unsaved,3008059,1);UseJade(unsaved,unready);Check(unsaved.ChiPoints==0 && unsaved.Inventory.ContainsUID(unready.UID),"a session without a loaded Chi wallet cannot consume jade rewards");
        var stalePair=JadePlayer(map);var staleYin=Pack(stalePair,3008059,1);var staleYang=Pack(stalePair,3008060,1);
        Query("UPDATE items SET locked=1 WHERE uid=@uid","uid",(long)staleYang.UID);UseJade(stalePair,staleYin);
        Check(Fenced(stalePair) && JadeChi(stalePair)==0 && SavedRune(staleYin,"stacksize")==1 && stalePair.Inventory.Count==2,"a changed partner preimage rolls back the first jade's deletion and Chi credit");
        var expired=JadePlayer(map);var expiredJade=Pack(expired,3008059,1);expiredJade.Days=1;expiredJade.DayStamp=now.ToLocalTime().AddDays(-2);UseJade(expired,expiredJade);
        Check(expired.ChiPoints==0 && expired.Inventory.ContainsUID(expiredJade.UID),"an expired timed jade cannot award Chi before normal expiry cleanup");
        uint oldPrice=Config.ChiFullRefillCp;Config.ChiFullRefillCp=1000;
        try {
            foreach(var row in new uint[][]{new uint[]{0,1000,4000,0},new uint[]{0,999,0,999},new uint[]{2000,1000,4000,500},new uint[]{3500,125,4000,0},
                new uint[]{3996,1,4000,0},new uint[]{3997,1,4000,0},new uint[]{3999,0,3999,0},new uint[]{3999,1,4000,0},new uint[]{4000,1000,4000,1000},new uint[]{4001,1000,4001,1000},new uint[]{uint.MaxValue,uint.MaxValue,uint.MaxValue,uint.MaxValue}}) {
                var p=JadePlayer(map,row[0]);p.Entity.ConquerPoints=row[1];Parallel.For(0,12,i=>GeneralData.BuyStrengthChi(new Chi(true),p));ChiTable.Load(p);
                Check(!Fenced(p) && p.ChiPoints==row[2] && JadeChi(p)==row[2] && p.Entity.ConquerPoints==row[3] && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==row[3],"durable Chi refill price and duplicate protection at Chi="+row[0]+" CP="+row[1]);
            }
            var failed=JadePlayer(map);failed.Entity.ConquerPoints=1000;Query("ALTER TABLE chi ADD CONSTRAINT chi_refill_failure CHECK(points<>4000) NOT VALID");GeneralData.BuyStrengthChi(new Chi(true),failed);
            Check(Fenced(failed) && JadeChi(failed)==0 && failed.Entity.ConquerPoints==1000 && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",failed.Entity.UID)==1000,"failed Chi refill rolls back CP deduction before updating the player");
            Query("ALTER TABLE chi DROP CONSTRAINT chi_refill_failure");DemonReset(failed);GeneralData.BuyStrengthChi(new Chi(true),failed);
            Check(!Fenced(failed) && failed.ChiPoints==4000 && failed.Entity.ConquerPoints==0,"a failed refill can be retried once without a second charge");
            var mismatch=JadePlayer(map);mismatch.Entity.ConquerPoints=1000;Query("UPDATE entities SET conquerpoints=900 WHERE uid=@uid","uid",(long)mismatch.Entity.UID);GeneralData.BuyStrengthChi(new Chi(true),mismatch);
            Check(!Fenced(mismatch) && JadeChi(mismatch)==0 && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",mismatch.Entity.UID)==900,"mismatched CP purse defers refill without overwriting saved funds");
            var high=JadePlayer(map);Config.ChiFullRefillCp=uint.MaxValue;high.Entity.ConquerPoints=uint.MaxValue;GeneralData.BuyStrengthChi(new Chi(true),high);
            Check(!Fenced(high) && high.ChiPoints==4000 && high.Entity.ConquerPoints==0,"maximum configured refill price uses widened arithmetic");
            var walletPacket=new ChiPowers(true).Query(high).ToArray();Check(System.BitConverter.ToUInt32(walletPacket,10)==4000,"existing Chi update packet carries the committed wallet balance");
            Config.ChiFullRefillCp=1000;
            var staleRefill=JadePlayer(map);staleRefill.Entity.ConquerPoints=1000;Query("UPDATE chi SET points=200 WHERE uid=@uid","uid",(long)staleRefill.Entity.UID);GeneralData.BuyStrengthChi(new Chi(true),staleRefill);
            Check(Fenced(staleRefill) && JadeChi(staleRefill)==200 && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",staleRefill.Entity.UID)==1000,"an external Chi change rolls back a refill's tentative CP deduction");
            var dirtyRefill=JadePlayer(map);dirtyRefill.Entity.ConquerPoints=1000;dirtyRefill.AdjustChiPoints(500);GeneralData.BuyStrengthChi(new Chi(true),dirtyRefill);
            Check(!Fenced(dirtyRefill) && dirtyRefill.ChiPoints==500 && dirtyRefill.Entity.ConquerPoints==1000 && JadeChi(dirtyRefill)==0,"pending legacy Chi must save before a refill can charge CPs");
            dirtyRefill.Fake=true;GeneralData.BuyStrengthChi(new Chi(true),dirtyRefill);dirtyRefill.Fake=false;dirtyRefill.Trade.InTrade=true;GeneralData.BuyStrengthChi(new Chi(true),dirtyRefill);dirtyRefill.Trade.InTrade=false;
            Check(dirtyRefill.ChiPoints==500 && dirtyRefill.Entity.ConquerPoints==1000,"fake or trading sessions cannot purchase Chi refills");
        }finally{Config.ChiFullRefillCp=oldPrice;}
        VerifyChiPursuitAdmission(map);
        VerifyChiAtomicProgression(map);
        VerifyJiangPaymentAdmission(map);
        VerifyJiangReload(map);
        VerifyJiangSaveOwnership(map);
        VerifyJiangAtomicTraining(map);
        VerifyJiangAtomicCreation(map);
        VerifyJiangAtomicRestore(map);
    }
    static void VerifyChiPursuitAdmission(Map map) {
        Query("ALTER TABLE entities ADD COLUMN IF NOT EXISTS reborn integer DEFAULT 2");
        var p=JadePlayer(map,350);p.Entity.Level=110;p.Entity.ConquerPoints=1000;
        Action<Chi> dispatch=request=>{
            byte[] bytes=request.ToArray();Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,16,bytes);
            Revora.Network.PacketHandler.HandlePacket(bytes,p);
        };
        foreach(byte mode in new byte[]{0,5,255}) {
            dispatch(new Chi(true){Type=Chi.Unlock,Mode=(Enums.ChiPowerType)mode});
            Check(p.ChiPowers.Count==0 && JadeChi(p)==350,"invalid Chi unlock mode cannot spend or insert a power: "+mode);
        }
        dispatch(new Chi(true){Type=Chi.Unlock,Mode=Enums.ChiPowerType.Dragon});
        Check(p.Account.State==AccountTable.AccountState.Player && p.ChiPowers.Count==1 && JadeChi(p)==50,"ordinary level-110 Dragon unlock charges 300 durable Chi");
        dispatch(new Chi(true){Type=Chi.Study,Mode=Enums.ChiPowerType.Dragon});
        Check(p.ChiPoints==0 && JadeChi(p)==0 && p.Entity.ConquerPoints==1000 && p.ChiPowers[0].Points>0,
            "50-Chi pursuit rolls and saves instead of buying an unnecessary CP refill");
        string rolled=string.Join(",",p.ChiPowers[0].Attributes.Select(a=>((int)a).ToString()));ChiTable.Load(p);
        Check(p.ChiPoints==0 && string.Join(",",p.ChiPowers[0].Attributes.Select(a=>((int)a).ToString()))==rolled,
            "ordinary pursuit attributes and exact payment survive production Chi reload");
        foreach(byte mode in new byte[]{0,2,5,255}) {
            dispatch(new Chi(true){Type=Chi.Study,Mode=(Enums.ChiPowerType)mode});
            Check(p.Entity.ConquerPoints==1000 && JadeChi(p)==0,"invalid or locked pursuit mode cannot trigger CP refill: "+mode);
        }
        foreach(uint mask in new uint[]{15,16,31,uint.MaxValue}) {
            dispatch(new Chi(true){Type=Chi.Study,Mode=Enums.ChiPowerType.Dragon,dwParam=mask});
            Check(p.Entity.ConquerPoints==1000 && JadeChi(p)==0,"invalid or all-locked pursuit cannot trigger CP refill: "+mask);
        }
        for(uint mask=0;mask<15;mask++) {
            uint cost=50;for(int slot=0;slot<4;slot++)if((mask&(1u<<slot))!=0)cost+=50;
            SetJadeChi(p,cost);
            var before=p.ChiPowers[0].Attributes.ToArray();
            dispatch(new Chi(true){Type=Chi.Study,Mode=Enums.ChiPowerType.Dragon,dwParam=mask});
            Check(p.ChiPoints==0 && JadeChi(p)==0 && p.Entity.ConquerPoints==1000
                && Enumerable.Range(0,4).Where(slot=>(mask&(1u<<slot))!=0).All(slot=>(int)p.ChiPowers[0].Attributes[slot]==(int)before[slot]),
                "every legal lock mask charges exact Chi and preserves held attributes: "+mask);
        }
        foreach(ushort length in new ushort[]{4,8,12,15}) {
            var bytes=new byte[length+8];Writer.WriteUInt16(length,0,bytes);Writer.WriteUInt16(2533,2,bytes);
            Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,length,bytes);Revora.Network.PacketHandler.HandlePacket(bytes,p);
            Check(p.Entity.ConquerPoints==1000 && JadeChi(p)==0,"short Chi frame is rejected before field access: "+length);
        }
    }

    static void VerifyChiAtomicProgression(Map map) {
        var wallet=typeof(Entity).Assembly.GetType("Revora.Game.Features.ChiWallet");
        var beforeCommit=wallet.GetField("BeforePowerCommit",Hidden);var beforeInstall=wallet.GetField("BeforePowerInstall",Hidden);
        Func<uint,GameState> player=points=>{var created=JadePlayer(map,points);created.Entity.Level=110;return created;};
        var unlock=new Chi(true){Type=Chi.Unlock,Mode=Enums.ChiPowerType.Dragon};
        var study=new Chi(true){Type=Chi.Study,Mode=Enums.ChiPowerType.Dragon};
        var p=player(400);
        beforeCommit.SetValue(null,(Action<GameState>)(g=>{throw new InvalidOperationException("fixture rollback");}));
        try {GeneralData.UnlockChi(unlock,p);}
        finally {beforeCommit.SetValue(null,null);}
        Check(Fenced(p) && p.ChiPoints==400 && p.ChiPowers.Count==0 && JadeChi(p)==400,"failed unlock rolls back payment without installing a power");
        DemonReset(p);ChiTable.Load(p);GeneralData.UnlockChi(unlock,p);
        Check(!Fenced(p) && p.ChiPowers.Count==1 && JadeChi(p)==100,"reload and deliberate unlock retry charge once");
        Query("ALTER TABLE chi ADD CONSTRAINT chi_pursuit_failure CHECK(points<>50) NOT VALID");
        try {GeneralData.StudyChi(study,p);}
        finally {Query("ALTER TABLE chi DROP CONSTRAINT chi_pursuit_failure");}
        Check(Fenced(p) && p.ChiPoints==100 && JadeChi(p)==100 && p.ChiPowers[0].Points==0,
            "database failure leaves rolled attributes uninstalled and Chi unspent");
        DemonReset(p);ChiTable.Load(p);
        beforeInstall.SetValue(null,(Action<GameState>)(g=>{throw new InvalidOperationException("fixture install");}));
        try {GeneralData.StudyChi(study,p);ChiTable.Save(p);GeneralData.StudyChi(study,p);}
        finally {beforeInstall.SetValue(null,null);}
        Check(Fenced(p) && p.ChiPoints==100 && JadeChi(p)==50 && p.ChiPowers[0].Points==0,
            "post-commit failure fences old attributes and blocks background overwrite or repeated spend");
        DemonReset(p);ChiTable.Load(p);
        Check(!Fenced(p) && p.ChiPoints==50 && p.ChiPowers[0].Points>0,"Chi reload recovers the committed roll and its single charge");
        string restored=string.Join(",",p.ChiPowers[0].Attributes.Select(a=>((int)a).ToString()));ChiTable.Save(p);ChiTable.Load(p);
        Check(p.ChiPoints==50 && string.Join(",",p.ChiPowers[0].Attributes.Select(a=>((int)a).ToString()))==restored,
            "later save and reload preserve the recovered committed roll");
        var simultaneous=player(800);Parallel.For(0,16,n=>GeneralData.UnlockChi(unlock,simultaneous));
        Check(!Fenced(simultaneous) && simultaneous.ChiPowers.Count==1 && JadeChi(simultaneous)==500,"concurrent unlock requests create one power and one payment");
        int installed=0;beforeInstall.SetValue(null,(Action<GameState>)(g=>System.Threading.Interlocked.Increment(ref installed)));
        try {Parallel.For(0,16,n=>GeneralData.StudyChi(study,simultaneous));}
        finally {beforeInstall.SetValue(null,null);}
        Check(!Fenced(simultaneous) && installed>0 && installed<=10 && JadeChi(simultaneous)==500-50*installed && simultaneous.ChiPoints==JadeChi(simultaneous),
            "concurrent pursuits serialize each committed roll with exactly one charge");
        for(int n=installed;n<10;n++)GeneralData.StudyChi(study,simultaneous);
        GeneralData.StudyChi(study,simultaneous);ChiTable.Load(simultaneous);
        Check(!Fenced(simultaneous) && simultaneous.ChiPoints==0 && simultaneous.ChiPowers[0].Points>0,
            "ten affordable pursuits exhaust exactly 500 Chi and the eleventh cannot spend");
        var changed=player(400);GeneralData.UnlockChi(unlock,changed);
        Query("UPDATE chi SET chipowers=@powers WHERE uid=@uid","powers",new byte[]{0},"uid",(long)changed.Entity.UID);
        GeneralData.StudyChi(study,changed);
        Check(Fenced(changed) && JadeChi(changed)==100,"external power revision with unchanged purse rejects stale pursuit");
        DemonReset(changed);ChiTable.Load(changed);
        Check(changed.ChiPowers.Count==0 && changed.ChiPoints==100,"reload preserves the external power revision rather than the stale local list");
        var profile=player(400);Query("UPDATE entities SET level=109 WHERE uid=@uid","uid",(long)profile.Entity.UID);
        GeneralData.UnlockChi(unlock,profile);
        Check(Fenced(profile) && JadeChi(profile)==400 && profile.ChiPowers.Count==0,"changed saved eligibility cannot purchase an unlock with stale level");
        var pending=player(400);pending.AdjustChiPoints(50);GeneralData.UnlockChi(unlock,pending);
        Check(!Fenced(pending) && pending.ChiPowers.Count==0 && pending.ChiPoints==450 && JadeChi(pending)==400,
            "pending legacy Chi changes defer power mutation until saved");
        ChiTable.Save(pending);GeneralData.UnlockChi(unlock,pending);
        Check(!Fenced(pending) && pending.ChiPowers.Count==1 && JadeChi(pending)==150,"saved pending Chi can fund a later ordinary unlock");
        var contended=player(400);var sessions=typeof(Entity).Assembly.GetType("Revora.Game.Features.SessionInstall");
        using(var held=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
            var holder=Task.Run(()=>{Call(sessions,null,"Enter",contended.Entity.UID);try{held.Set();if(!release.Wait(5000))throw new Exception("lease wait");}finally{Call(sessions,null,"Exit",contended.Entity.UID);}});
            try {if(!held.Wait(5000))throw new Exception("lease holder missing");GeneralData.UnlockChi(unlock,contended);
                Check(!Fenced(contended) && contended.ChiPowers.Count==0 && JadeChi(contended)==400,"busy character lease defers unlock without charge or deadlock");}
            finally {release.Set();holder.GetAwaiter().GetResult();}
        }
        GeneralData.UnlockChi(unlock,contended);
        Check(!Fenced(contended) && contended.ChiPowers.Count==1 && JadeChi(contended)==100,"unlock succeeds once session lease is available");
        var disconnected=player(400);
        beforeCommit.SetValue(null,(Action<GameState>)(g=>{GameState removed;Kernel.GamePool.TryRemove(g.Entity.UID,out removed);}));
        try {GeneralData.UnlockChi(unlock,disconnected);}
        finally {beforeCommit.SetValue(null,null);Kernel.GamePool[disconnected.Entity.UID]=disconnected;}
        Check(Fenced(disconnected) && disconnected.ChiPowers.Count==0 && JadeChi(disconnected)==400,"session removal before commit rolls back the prospective unlock and payment");

    }

    static void VerifyJiangPaymentAdmission(Map map) {
        Query("CREATE TABLE IF NOT EXISTS jianghu (LIKE public.jianghu INCLUDING ALL);ALTER TABLE entities ADD COLUMN IF NOT EXISTS revorapoint bigint DEFAULT 0,ADD COLUMN IF NOT EXISTS studypoints integer DEFAULT 0");
        Revora.Database.JiangHu.LoadStatus();
        Func<GameState> player=()=>{
            var created=JadePlayer(map);created.Entity.ConquerPoints=100;
            using(var query=new MySqlCommand(MySqlCommandType.SELECT).Select("entities").Where("uid",created.Entity.UID))
            using(var reader=new MySqlReader(query)){if(!reader.Read())throw new Exception("Purse row missing");
                Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.PurseBalance"),null,"Loaded",created,reader);}

            created.Entity.MyJiang=new Revora.Game.JiangHu(created.Entity.UID){OwnName=created.Entity.Name,JiangName="Training"};created.Entity.MyJiang.Stagers[0].Activate=true;
            return created;
        };
        var packet=new byte[20];packet[9]=1;packet[10]=1;packet[11]=1;
        var p=player();Revora.Game.JiangHu.ExtraStudy(packet,p);
        Check(!Fenced(p) && p.Entity.ConquerPoints==90 && p.Entity.SubClasses.StudyPoints==20
            && p.Entity.MyJiang.RoundBuyPoints==1 && p.Entity.MyJiang.Stagers[0].Stars[0].Activate,
            "ordinary paid Jiang training charges 10 CP and grants 20 Study Points exactly once");
        Revora.Game.JiangHu.ExtraStudy(packet,p);
        Check(!Fenced(p) && p.Entity.ConquerPoints==70 && p.Entity.SubClasses.StudyPoints==40 && p.Entity.MyJiang.RoundBuyPoints==2,
            "second Jiang training follows its price ladder with one Study Point reward");
        var stale=player();Query("UPDATE entities SET conquerpoints=99 WHERE uid=@uid","uid",(long)stale.Entity.UID);
        string before=stale.Entity.MyJiang.ToString();Revora.Game.JiangHu.ExtraStudy(packet,stale);
        Check(Fenced(stale) && stale.Entity.MyJiang.ToString()==before && stale.Entity.SubClasses.StudyPoints==0
            && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",stale.Entity.UID)==99,
            "failed real CP purse debit cannot grant Jiang roll, counter, talent or Study Points");
        var overflow=player();overflow.Entity.SubClasses.StudyPoints=ushort.MaxValue-19;before=overflow.Entity.MyJiang.ToString();
        Revora.Game.JiangHu.ExtraStudy(packet,overflow);
        Check(!Fenced(overflow) && overflow.Entity.ConquerPoints==100 && overflow.Entity.SubClasses.StudyPoints==ushort.MaxValue-19
            && overflow.Entity.MyJiang.ToString()==before,"full Study Point purse defers paid training without wrapping or charging");
        overflow.Entity.SubClasses.StudyPoints=ushort.MaxValue-20;Query("UPDATE entities SET studypoints=@study WHERE uid=@uid","study",(int)(ushort.MaxValue-20),"uid",(long)overflow.Entity.UID);Revora.Game.JiangHu.ExtraStudy(packet,overflow);
        Check(!Fenced(overflow) && overflow.Entity.ConquerPoints==90 && overflow.Entity.SubClasses.StudyPoints==ushort.MaxValue,
            "exact Study Point capacity accepts one paid training reward");
        var restore=new byte[12];restore[4]=10;
        var restored=player();Revora.Game.JiangHu.ExtraStudy(packet,restored);ushort prior=restored.Entity.MyJiang.Stagers[0].Stars[0].UID;
        Revora.Game.JiangHu.ExtraStudy(packet,restored);Revora.Game.JiangHu.JiangHus(restore,restored);
        Check(!Fenced(restored) && restored.Entity.ConquerPoints==50 && restored.Entity.MyJiang.MyOldStar==null
            && restored.Entity.MyJiang.Stagers[0].Stars[0].UID==prior,"normal restoration still buys the prior paid-training attribute for 20 CPs");
        Revora.Game.JiangHu.JiangHus(restore,restored);
        Check(restored.Entity.ConquerPoints==50,"repeated restoration without a previous snapshot cannot charge again");

        Query("UPDATE entities SET conquerpoints=69 WHERE uid=@uid","uid",(long)p.Entity.UID);
        before=p.Entity.MyJiang.ToString();var snapshot=p.Entity.MyJiang.MyOldStar;
        Revora.Game.JiangHu.JiangHus(restore,p);
        Check(Fenced(p) && p.Entity.MyJiang.ToString()==before && ReferenceEquals(snapshot,p.Entity.MyJiang.MyOldStar)
            && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==69,
            "failed restoration CP debit preserves current attributes and previous-roll snapshot");
    }

    static void VerifyJiangReload(Map map) {
        var p=JadePlayer(map);var original=new Revora.Game.JiangHu(p.Entity.UID){OwnName=p.Entity.Name,JiangName="Reload",RoundBuyPoints=49,FreeCourse=123456};
        p.Entity.MyJiang=original;
        for(byte index=0;index<9;index++) {
            byte type=Revora.Database.JiangHu.CultivateStatus[(byte)(index+1)].First();
            ushort key=Revora.Database.JiangHu.ValueToRoll(type,6);
            original.Stagers[index].Activate=true;
            foreach(var star in original.Stagers[index].Stars){star.Activate=true;star.UID=key;star.Level=6;star.Typ=(Revora.Game.JiangHu.JiangStages.AtributesType)type;}
        }
        foreach(bool mode in new[]{false,true})foreach(uint used in new uint[]{0,9,300,uint.MaxValue}) {
            original.OnJiangHu=mode;original.FreeTimeTodeyUsed=used;Revora.Database.JiangHu.SaveJiangHu(p);
            string saved;
            using(var command=new MySqlCommand(MySqlCommandType.SELECT).Select("jianghu").Where("uid",p.Entity.UID))
            using(var reader=new MySqlReader(command)){if(!reader.Read())throw new Exception("Jiang row missing");saved=reader.ReadLongString("data");}
            var loaded=new Revora.Game.JiangHu(0);loaded.Load(saved);
            Check(loaded.UID==p.Entity.UID && loaded.OnJiangHu==mode && loaded.FreeTimeTodeyUsed==used && loaded.FreeTimeTodey==10
                && loaded.RoundBuyPoints==49 && loaded.FreeCourse==123456 && loaded.Inner_Strength==81000,
                "database Jiang reload preserves mode, UInt32 usage and full aligned score: "+mode+"/"+used);
            loaded.Load(saved);
            Check(loaded.Inner_Strength==81000 && loaded.Stagers.All(stage=>stage.Aligments.Count==1),
                "repeat Jiang load rebuilds alignment score without accumulation: "+mode+"/"+used);
        }
        original.Stagers[8].Activate=false;
        var inactive=new Revora.Game.JiangHu(0);inactive.Load(original.ToString());
        Check(inactive.Inner_Strength==72000,"inactive saved Jiang stage contributes no base or alignment score");
        original.Stagers[8].Activate=true;original.Stagers[8].Stars[8].Activate=false;
        inactive.Load(original.ToString());
        Check(inactive.Inner_Strength==79200,"saved inactive star breaks nine-star alignment on reload");
    }

    static void VerifyJiangSaveOwnership(Map map) {
        var p=JadePlayer(map);var original=new Revora.Game.JiangHu(p.Entity.UID){OwnName=p.Entity.Name,JiangName="Owner"};p.Entity.MyJiang=original;
        Func<string> stored=()=>{using(var command=new MySqlCommand(MySqlCommandType.SELECT).Select("jianghu").Where("uid",p.Entity.UID))
            using(var reader=new MySqlReader(command)){if(!reader.Read())throw new Exception("Missing Jiang row");return reader.ReadLongString("data");}};
        Revora.Database.JiangHu.SaveJiangHu(p);string saved=stored();
        Check(saved==original.ToString(),"healthy ordinary owner saves registered Jiang state");
        original.FreeCourse++;typeof(Entity).GetField("PersistenceRequiresReload",Instance).SetValue(p.Entity,true);Revora.Database.JiangHu.SaveJiangHu(p);
        Check(stored()==saved,"fenced player cannot overwrite saved Jiang state");
        typeof(Entity).GetField("PersistenceRequiresReload",Instance).SetValue(p.Entity,false);p.Fake=true;Revora.Database.JiangHu.SaveJiangHu(p);p.Fake=false;
        Check(stored()==saved,"synthetic player cannot write Jiang progression");
        var replacement=JadePlayer(map);
        Kernel.GamePool[p.Entity.UID]=replacement;
        try {Revora.Database.JiangHu.SaveJiangHu(p);Check(stored()==saved,"replaced session cannot write another session's Jiang state");}
        finally {Kernel.GamePool[p.Entity.UID]=p;}
        var other=new Revora.Game.JiangHu(0){UID=p.Entity.UID,OwnName=p.Entity.Name,JiangName="Other"};
        Revora.Game.JiangHu.JiangHuClients[p.Entity.UID]=other;
        try {Revora.Database.JiangHu.SaveJiangHu(p);Check(stored()==saved,"mismatched registered Jiang object cannot be saved through stale owner");}
        finally {Revora.Game.JiangHu.JiangHuClients[p.Entity.UID]=original;}
        bool loaded=Revora.Database.JiangHu.fullload;Revora.Database.JiangHu.fullload=true;
        GameState removed;Kernel.GamePool.TryRemove(p.Entity.UID,out removed);
        try {Revora.Database.JiangHu.SaveJiangHu();Check(stored()==original.ToString(),"offline administrator edits save only against their loaded database revision");saved=stored();}
        finally {Kernel.GamePool[p.Entity.UID]=p;Revora.Database.JiangHu.fullload=loaded;}
        Query("ALTER TABLE jianghu ADD CONSTRAINT jiang_save_failure CHECK(false) NOT VALID");
        try {Revora.Database.JiangHu.SaveJiangHu(p);}
        finally {Query("ALTER TABLE jianghu DROP CONSTRAINT jiang_save_failure");}
        Check(Fenced(p) && stored()==saved,"database save failure fences Jiang owner and preserves prior row");
        loaded=Revora.Database.JiangHu.fullload;Revora.Database.JiangHu.fullload=true;
        try {Revora.Database.JiangHu.SaveJiangHu();Check(stored()==saved,"later global save cannot overwrite Jiang state from a fenced owner");}
        finally {Revora.Database.JiangHu.fullload=loaded;}
        var external=JadePlayer(map);var offline=new Revora.Game.JiangHu(external.Entity.UID){OwnName=external.Entity.Name,JiangName="External"};external.Entity.MyJiang=offline;
        Revora.Database.JiangHu.SaveJiangHu(external);string newer=offline.ToString().Replace("External","Newer");
        Query("UPDATE jianghu SET data=@data WHERE uid=@uid","data",newer,"uid",(long)external.Entity.UID);
        Kernel.GamePool.TryRemove(external.Entity.UID,out removed);loaded=Revora.Database.JiangHu.fullload;Revora.Database.JiangHu.fullload=true;
        try {Revora.Database.JiangHu.SaveJiangHu();Revora.Database.JiangHu.SaveJiangHu();
            using(var command=new MySqlCommand(MySqlCommandType.SELECT).Select("jianghu").Where("uid",external.Entity.UID))
            using(var reader=new MySqlReader(command)){reader.Read();Check(reader.ReadLongString("data")==newer,"repeated offline saves cannot overwrite a newer durable Jiang revision");}}
        finally {Kernel.GamePool[external.Entity.UID]=external;Revora.Database.JiangHu.fullload=loaded;}
    }

    static void ReloadJiangTraining(GameState p) {
        DemonReset(p);
        using(var command=new MySqlCommand(MySqlCommandType.SELECT).Select("entities").Where("uid",p.Entity.UID))
        using(var reader=new MySqlReader(command)){if(!reader.Read())throw new Exception("Jiang purse missing");
            Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.PurseBalance"),null,"Loaded",p,reader);
            p.Entity.SubClasses.StudyPoints=reader.ReadUInt16("studypoints");}
        Call(typeof(Revora.Database.JiangHu),null,"LoadClient",p);
    }
    static void VerifyJiangAtomicTraining(Map map) {
        var training=typeof(Entity).Assembly.GetType("Revora.Game.Features.JiangTraining");
        var beforeCommit=training.GetField("BeforeCommit",Hidden);var beforeInstall=training.GetField("BeforeInstall",Hidden);
        Func<GameState> player=()=>{
            var created=JadePlayer(map);created.Entity.ConquerPoints=100;
            created.Entity.MyJiang=new Revora.Game.JiangHu(created.Entity.UID){OwnName=created.Entity.Name,JiangName="Atomic",JiangSetting=7};
            created.Entity.MyJiang.Stagers[0].Activate=true;Revora.Database.JiangHu.SaveJiangHu(created);ReloadJiangTraining(created);return created;
        };
        var paid=new byte[20];paid[9]=1;paid[10]=1;paid[11]=1;var free=(byte[])paid.Clone();free[9]=0;
        var p=player();string original=p.Entity.MyJiang.ToString();
        Query("ALTER TABLE jianghu ADD CONSTRAINT jiang_training_failure CHECK(false) NOT VALID");
        try {Revora.Game.JiangHu.ExtraStudy(paid,p);}
        finally {Query("ALTER TABLE jianghu DROP CONSTRAINT jiang_training_failure");}
        Check(Fenced(p) && p.Entity.ConquerPoints==100 && p.Entity.SubClasses.StudyPoints==0 && p.Entity.MyJiang.ToString()==original
            && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==100
            && Scalar("SELECT studypoints FROM entities WHERE uid=@uid",p.Entity.UID)==0,
            "failed Jiang row write rolls back tentative CP and Study Point changes without installing a roll");
        ReloadJiangTraining(p);Revora.Game.JiangHu.ExtraStudy(paid,p);
        Check(!Fenced(p) && p.Entity.ConquerPoints==90 && p.Entity.SubClasses.StudyPoints==20 && p.Entity.MyJiang.RoundBuyPoints==1,
            "normal blocked-state reload allows one deliberate paid-training retry");
        var timer=p.Entity.MyJiang.TimerStamp;var start=p.Entity.MyJiang.StartCountDwon;var end=p.Entity.MyJiang.CountDownEnd;
        p.Entity.MyJiang.JiangSetting=7;Revora.Game.JiangHu.ExtraStudy(free,p);
        Check(!Fenced(p) && p.Entity.ConquerPoints==90 && p.Entity.SubClasses.StudyPoints==10 && p.Entity.MyJiang.FreeCourse==10000
            && p.Entity.MyJiang.Talent==1 && p.Entity.MyJiang.JiangSetting==7 && p.Entity.MyJiang.TimerStamp==timer
            && p.Entity.MyJiang.StartCountDwon==start && p.Entity.MyJiang.CountDownEnd==end,
            "free training atomically consumes Study Points, talent and course without resetting timers or attack settings");
        Check(Scalar("SELECT studypoints FROM entities WHERE uid=@uid",p.Entity.UID)==10,
            "free training Study Point cost is durable immediately");
        beforeInstall.SetValue(null,(Action<GameState>)(g=>{throw new InvalidOperationException("fixture install");}));
        try {Revora.Game.JiangHu.ExtraStudy(paid,p);Revora.Database.JiangHu.SaveJiangHu(p);Revora.Game.JiangHu.ExtraStudy(paid,p);}
        finally {beforeInstall.SetValue(null,null);}
        Check(Fenced(p) && p.Entity.ConquerPoints==90 && p.Entity.SubClasses.StudyPoints==10
            && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==70
            && Scalar("SELECT studypoints FROM entities WHERE uid=@uid",p.Entity.UID)==30,
            "post-commit Jiang failure fences stale memory and prevents another charge or background overwrite");
        ReloadJiangTraining(p);
        Check(!Fenced(p) && p.Entity.ConquerPoints==70 && p.Entity.SubClasses.StudyPoints==30 && p.Entity.MyJiang.RoundBuyPoints==2
            && p.Entity.MyJiang.Stagers[0].Stars[0].Activate,
            "login recovery reloads committed Jiang roll, counter, CP and Study Points");
        Revora.Database.JiangHu.SaveJiangHu(p);ReloadJiangTraining(p);
        Check(!Fenced(p) && p.Entity.ConquerPoints==70 && p.Entity.SubClasses.StudyPoints==30 && p.Entity.MyJiang.RoundBuyPoints==2,
            "recovered Jiang state survives a later normal save and load");
        var parallel=player();Parallel.For(0,16,n=>Revora.Game.JiangHu.ExtraStudy(paid,parallel));
        int count=parallel.Entity.MyJiang.RoundBuyPoints;
        Check(!Fenced(parallel) && count>=1 && count<=4 && parallel.Entity.ConquerPoints==100-5*count*(count+1)
            && parallel.Entity.SubClasses.StudyPoints==20*count
            && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",parallel.Entity.UID)==parallel.Entity.ConquerPoints,
            "concurrent Jiang trainings serialize every accepted fee, counter and reward");
        for(int n=count;n<4;n++)Revora.Game.JiangHu.ExtraStudy(paid,parallel);Revora.Game.JiangHu.ExtraStudy(paid,parallel);
        Check(!Fenced(parallel) && parallel.Entity.ConquerPoints==0 && parallel.Entity.MyJiang.RoundBuyPoints==4 && parallel.Entity.SubClasses.StudyPoints==80,
            "four affordable Jiang trainings exhaust exactly 100 CPs; fifth grants nothing");
        var removed=player();beforeCommit.SetValue(null,(Action<GameState>)(g=>{GameState old;Kernel.GamePool.TryRemove(g.Entity.UID,out old);}));
        try {Revora.Game.JiangHu.ExtraStudy(paid,removed);}
        finally {beforeCommit.SetValue(null,null);Kernel.GamePool[removed.Entity.UID]=removed;}
        Check(Fenced(removed) && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",removed.Entity.UID)==100
            && Scalar("SELECT studypoints FROM entities WHERE uid=@uid",removed.Entity.UID)==0 && removed.Entity.MyJiang.RoundBuyPoints==0,
            "session removal before Jiang commit rolls back all progression and payment");
        var stale=player();Query("UPDATE entities SET studypoints=5 WHERE uid=@uid","uid",(long)stale.Entity.UID);
        Revora.Game.JiangHu.ExtraStudy(paid,stale);
        Check(Fenced(stale) && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",stale.Entity.UID)==100
            && Scalar("SELECT studypoints FROM entities WHERE uid=@uid",stale.Entity.UID)==5,
            "changed durable Study Point purse rejects Jiang training without overwriting it");
    }

    static byte[] JiangCreationPacket(string name) {
        int body=8+name.Length;var packet=new byte[body+8];
        Writer.WriteUInt16((ushort)body,0,packet);Writer.WriteUInt16(2700,2,packet);
        packet[4]=1;packet[5]=1;packet[6]=(byte)name.Length;Writer.WriteString(name,7,packet);
        Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,body,packet);return packet;
    }
    static void VerifyJiangAtomicCreation(Map map) {
        Action<GameState> create=actor=>Revora.Network.PacketHandler.HandlePacket(JiangCreationPacket("NewPath"),actor);
        var p=JadePlayer(map);p.Entity.ConquerPoints=100;create(p);
        Check(!Fenced(p) && p.Entity.MyJiang!=null && p.Entity.MyJiang.JiangName=="NewPath"
            && p.Entity.SubClasses.StudyPoints==100 && Scalar("SELECT studypoints FROM entities WHERE uid=@uid",p.Entity.UID)==100,
            "ordinary Jiang creation atomically persists its starting Study Points and named progression");
        create(p);var paid=new byte[20];paid[9]=1;paid[10]=1;paid[11]=1;Revora.Game.JiangHu.ExtraStudy(paid,p);
        Check(!Fenced(p) && p.Entity.ConquerPoints==90 && p.Entity.SubClasses.StudyPoints==120 && p.Entity.MyJiang.RoundBuyPoints==1,
            "repeated creation grants no extra gift and the first ordinary paid training succeeds");
        var parallel=JadePlayer(map);Parallel.For(0,16,n=>create(parallel));
        Check(!Fenced(parallel) && parallel.Entity.MyJiang!=null && parallel.Entity.SubClasses.StudyPoints==100
            && Scalar("SELECT count(*) FROM jianghu WHERE uid=@uid",parallel.Entity.UID)==1,
            "concurrent creation installs one Jiang row and one starting gift");
        var failed=JadePlayer(map);Query("ALTER TABLE jianghu ADD CONSTRAINT jiang_create_failure CHECK(false) NOT VALID");
        try {create(failed);}finally {Query("ALTER TABLE jianghu DROP CONSTRAINT jiang_create_failure");}
        Check(Fenced(failed) && Scalar("SELECT studypoints FROM entities WHERE uid=@uid",failed.Entity.UID)==0
            && Scalar("SELECT count(*) FROM jianghu WHERE uid=@uid",failed.Entity.UID)==0,
            "failed Jiang creation rolls back starting gift and progression together");
        ReloadJiangTraining(failed);create(failed);
        Check(!Fenced(failed) && failed.Entity.MyJiang!=null && failed.Entity.SubClasses.StudyPoints==100,
            "normal reload after rolled-back creation permits one deliberate retry");
        var committed=JadePlayer(map);var training=typeof(Entity).Assembly.GetType("Revora.Game.Features.JiangTraining");
        var hook=training.GetField("BeforeInstall",Hidden);hook.SetValue(null,(Action<GameState>)(g=>{throw new InvalidOperationException("fixture creation install");}));
        try {create(committed);Revora.Database.JiangHu.SaveJiangHu(committed);}finally {hook.SetValue(null,null);}
        Check(Fenced(committed) && committed.Entity.SubClasses.StudyPoints==0
            && Scalar("SELECT studypoints FROM entities WHERE uid=@uid",committed.Entity.UID)==100,
            "committed creation with failed installation fences stale memory and preserves durable gift");
        ReloadJiangTraining(committed);create(committed);
        Check(!Fenced(committed) && committed.Entity.MyJiang!=null && committed.Entity.MyJiang.JiangName=="NewPath"
            && committed.Entity.SubClasses.StudyPoints==100,
            "login recovery restores committed creation without granting its gift twice");
        var legacy=JadePlayer(map);legacy.Entity.SubClasses.StudyPoints=7;
        Query("UPDATE entities SET studypoints=7 WHERE uid=@uid","uid",(long)legacy.Entity.UID);
        Query("INSERT INTO jianghu(uid,data) VALUES(@uid,NULL)","uid",(long)legacy.Entity.UID);create(legacy);
        Check(!Fenced(legacy) && legacy.Entity.MyJiang!=null && legacy.Entity.SubClasses.StudyPoints==7
            && Scalar("SELECT studypoints FROM entities WHERE uid=@uid",legacy.Entity.UID)==7,
            "legacy blank Jiang row initializes without repeating an unproven starting gift");
    }

    static void VerifyJiangAtomicRestore(Map map) {
        var paid=new byte[20];paid[9]=1;paid[10]=1;paid[11]=1;
        var restore=new byte[12];restore[4]=10;
        Func<GameState> player=()=> {
            var actor=JadePlayer(map);actor.Entity.ConquerPoints=100;
            Revora.Network.PacketHandler.HandlePacket(JiangCreationPacket("Restore"),actor);
            Revora.Game.JiangHu.ExtraStudy(paid,actor);Revora.Game.JiangHu.ExtraStudy(paid,actor);return actor;
        };
        var failed=player();string original=failed.Entity.MyJiang.ToString();
        Query("ALTER TABLE jianghu ADD CONSTRAINT jiang_restore_failure CHECK(false) NOT VALID");
        try {Revora.Game.JiangHu.JiangHus(restore,failed);}finally {Query("ALTER TABLE jianghu DROP CONSTRAINT jiang_restore_failure");}
        Check(Fenced(failed) && failed.Entity.MyJiang.ToString()==original && failed.Entity.MyJiang.MyOldStar!=null
            && failed.Entity.ConquerPoints==70 && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",failed.Entity.UID)==70,
            "restore row failure rolls back its CP debit and preserves the current roll");
        var committed=player();ushort previous=committed.Entity.MyJiang.MyOldStar.Star.UID;
        uint course=committed.Entity.MyJiang.FreeCourse;byte talent=committed.Entity.MyJiang.Talent;
        var training=typeof(Entity).Assembly.GetType("Revora.Game.Features.JiangTraining");var hook=training.GetField("BeforeInstall",Hidden);
        hook.SetValue(null,(Action<GameState>)(g=>{throw new InvalidOperationException("fixture restore install");}));
        try {Revora.Game.JiangHu.JiangHus(restore,committed);Revora.Database.JiangHu.SaveJiangHu(committed);Revora.Game.JiangHu.JiangHus(restore,committed);}
        finally {hook.SetValue(null,null);}
        Check(Fenced(committed) && committed.Entity.ConquerPoints==70
            && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",committed.Entity.UID)==50,
            "post-commit restore failure blocks stale saves and repeated charges");
        ReloadJiangTraining(committed);Revora.Game.JiangHu.JiangHus(restore,committed);
        Check(!Fenced(committed) && committed.Entity.ConquerPoints==50 && committed.Entity.MyJiang.MyOldStar==null
            && committed.Entity.MyJiang.Stagers[0].Stars[0].UID==previous && committed.Entity.MyJiang.RoundBuyPoints==2
            && committed.Entity.SubClasses.StudyPoints==140 && committed.Entity.MyJiang.FreeCourse==course && committed.Entity.MyJiang.Talent==talent,
            "login recovers the restored attribute and single fee without changing training resources");
        var parallel=player();Parallel.For(0,16,n=>Revora.Game.JiangHu.JiangHus(restore,parallel));
        Check(!Fenced(parallel) && parallel.Entity.ConquerPoints==50 && parallel.Entity.MyJiang.MyOldStar==null
            && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",parallel.Entity.UID)==50,
            "concurrent restore requests consume one snapshot and charge exactly once");
        var poor=player();poor.Entity.ConquerPoints=19;original=poor.Entity.MyJiang.ToString();
        Revora.Game.JiangHu.JiangHus(restore,poor);
        Check(!Fenced(poor) && poor.Entity.ConquerPoints==19 && poor.Entity.MyJiang.ToString()==original && poor.Entity.MyJiang.MyOldStar!=null,
            "insufficient restore CPs preserve the previous-star option and current state");
        var invalid=player();var remote=(byte[])paid.Clone();remote[9]=2;original=invalid.Entity.MyJiang.ToString();
        Revora.Game.JiangHu.ExtraStudy(remote,invalid);
        Check(!Fenced(invalid) && invalid.Entity.ConquerPoints==70 && invalid.Entity.MyJiang.ToString()==original,
            "training action two cannot bypass the separate restore dispatcher");
    }

}
