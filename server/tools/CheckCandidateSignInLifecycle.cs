using System;
using Console=System.Console;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static DateTime signNow;
    static void SignClock(DateTime now) {
        signNow=now;
        typeof(MsgSignIn).GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>signNow));
    }
    static void SignRequest(GameState p,MsgSignIn.Actions action) {
        var packet=new MsgSignIn{Type=action}.ToArray();
        Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,packet.Length-8,packet);
        MsgSignIn.Handle(p,packet);
    }
    static long SignReceipts(GameState p) {return (long)Sql("SELECT count(*) FROM revora_sign_in_claims WHERE uid=@uid","uid",(long)p.Entity.UID);}
    static string SignSnapshot(GameState p) {return Convert.ToString(Sql(@"SELECT json_build_object(
'owner',(SELECT json_build_array(revorapoint,dailysigneddays,dailysignrewards,dailysignvipchances) FROM entities WHERE uid=@uid),
'state',(SELECT row_to_json(s) FROM revora_sign_in_state s WHERE uid=@uid),
'receipts',(SELECT json_agg(c ORDER BY month,day) FROM revora_sign_in_claims c WHERE uid=@uid),
'items',(SELECT json_agg(i ORDER BY uid) FROM (SELECT uid,id,stacksize,bound,days,daystamp,inwardrobe FROM items WHERE entityid=@uid) i))::text","uid",(long)p.Entity.UID));}
    static void SignEligibility(GameState p) {p.Entity.Level=140;p.Entity.Reborn=2;p.Entity.VIPLevel=7;p.Entity.DailySignDays=new byte[31];}
    static void SignEarn() {
        Player(Kernel.Maps[1002]);nextUid=FirstUid;
        var p=Player(Kernel.Maps[1002],true); SignEligibility(p);p.LoadItemStats();
        var daily=Feature("DailyWorldBosses");DateTime bossNow=DateTime.Now.Date.AddDays(1).ToUniversalTime();
        daily.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>bossNow));Call(daily,"Tick");
        bossNow=DateTime.SpecifyKind((DateTime)Sql("SELECT min(planned) FROM revora_daily_boss_runs WHERE planned>=@now AND state=0","now",bossNow),DateTimeKind.Utc).AddSeconds(1);
        Call(daily,"Tick");var active=daily.GetField("Active",Hidden).GetValue(null);Require(active!=null);
        var boss=(Entity)active.GetType().GetField("Entity",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(active);
        p.BindWorldMap(boss.MapID);EliminationHit(p,boss);p.BindWorldMap(1002);Require(p.Entity.RevoraPoint==50000);
        var start=new DateTime(DateTime.Now.Year,DateTime.Now.Month,1).AddMonths(1);
        for(int day=1;day<=7;day++) {
            SignClock(start.AddDays(day-1));
            uint before=p.Entity.RevoraPoint;
            Parallel.For(0,8,n=>SignRequest(p,MsgSignIn.Actions.Sign));
            Require(p.Entity.RevoraPoint==before-4000 && SignReceipts(p)==day && p.Entity.DailySignDays.Take(day).All(x=>x==1));
        }
        Require(p.Entity.RevoraPoint==22000 && p.Entity.DailySignRewards==7);
        Require(p.Inventory.Objects.Count(i=>i.ID==3100011)==7 && p.Inventory.Objects.Count(i=>i.ID==3100013)==1);
        var seven=p.Inventory.Objects.Single(i=>i.ID==3100014);Require(seven.Days==2 && !seven.Bound);
        ItemHandler.UseItem(seven,p);Require(!p.Inventory.ContainsUID(seven.UID));
        var easter=p.Inventory.Objects.Single(i=>i.ID==720121);
        Feature("EasterGoldPacks").GetField("Roll",Hidden).SetValue(null,(Func<int,int,int>)((min,max)=>28));
        ItemHandler.UseItem(easter,p);ItemHandler.UseItem(easter,p);
        var mount=p.Inventory.Objects.Single(i=>i.ID==200481);Require(!mount.Bound && mount.Days==0);
        new CoatStorage{Info=new CoatStorageProto{ActionId=CoatStorage.Action.Addtowardrobe,ItemGuid=mount.UID,ItemId=(int)mount.ID}}.Handle(p);
        Require(mount.InWardrobe && p.Entity.StorageItems.ContainsKey(mount.UID));
        Require((long)Sql("SELECT count(*) FROM revora_easter_gold_pack_uses WHERE uid=@uid","uid",(long)p.Entity.UID)==1);
        Sql("CREATE TABLE revora_sign_process_probe(stamp bigint NOT NULL,snapshot text NOT NULL); INSERT INTO revora_sign_process_probe VALUES(@stamp,@snapshot)","stamp",signNow.ToBinary(),"snapshot",SignSnapshot(p));
        SignFaults(start);
        Console.WriteLine("[sign-process] sign-earn-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Sign-in earning did not close.");
    }
    static GameState SignFaultPlayer() {
        uint uid=nextUid;Player(Kernel.Maps[1002]);nextUid=uid;
        return Player(Kernel.Maps[1002],true);
    }
    static void SignFaults(DateTime start) {
        var full=SignFaultPlayer();SignEligibility(full);full.Entity.AddRevoraPoints(20000);SignClock(start);
        SignRequest(full,MsgSignIn.Actions.Sign);Require(SignReceipts(full)==1);
        while(full.Inventory.Count<39)SuppliedMountItem(full,1000000,1,false);
        SignClock(start.AddDays(1));string before=SignSnapshot(full);
        SignRequest(full,MsgSignIn.Actions.Sign);Require(SignSnapshot(full)==before && full.Entity.RevoraPoint==16000);
        var filler=full.Inventory.Objects.First(i=>i.ID==1000000);Require(full.Inventory.Remove(filler,Enums.ItemUse.Remove));
        SignRequest(full,MsgSignIn.Actions.Sign);Require(SignReceipts(full)==2 && full.Inventory.Count==40 && full.Entity.RevoraPoint==12000);

        var failed=SignFaultPlayer();SignEligibility(failed);failed.Entity.AddRevoraPoints(20000);SignClock(start);
        before=SignSnapshot(failed);
        Sql("CREATE FUNCTION sign_probe_refuse() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER sign_probe_refuse BEFORE INSERT ON revora_sign_in_claims FOR EACH ROW EXECUTE FUNCTION sign_probe_refuse()");
        try {SignRequest(failed,MsgSignIn.Actions.Sign);} finally {Sql("DROP TRIGGER sign_probe_refuse ON revora_sign_in_claims; DROP FUNCTION sign_probe_refuse()");}
        Require(SignSnapshot(failed)==before && failed.Inventory.Count==0 && SignReceipts(failed)==0);
        Require((bool)typeof(Entity).GetField("PersistenceRequiresReload",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(failed.Entity));

        var late=SignFaultPlayer();SignEligibility(late);late.Entity.AddRevoraPoints(9000);
        // Supplied legacy chances isolate refusal/consumption, not earned rewards.
        late.Entity.DailySignVIPChances=2;Sql("UPDATE entities SET dailysignvipchances=2 WHERE uid=@uid","uid",(long)late.Entity.UID);
        before=SignSnapshot(late);SignRequest(late,MsgSignIn.Actions.LateSignIn);
        Require(SignSnapshot(late)==before && late.Entity.DailySignVIPChances==2);
        late.Entity.AddRevoraPoints(1000);SignRequest(late,MsgSignIn.Actions.LateSignIn);
        Require(late.Entity.RevoraPoint==0 && late.Entity.DailySignVIPChances==1 && SignReceipts(late)==1);
        before=SignSnapshot(late);SignRequest(late,MsgSignIn.Actions.LateSignIn);Require(SignSnapshot(late)==before);
        foreach(var bytes in new[]{new byte[0],new byte[4],new byte[65]})MsgSignIn.Handle(late,bytes);
        Require(SignSnapshot(late)==before);
        Console.WriteLine("[sign-process] full bag, payment refusal, rollback, session fencing and replay passed");
    }
    static void SignReload() {
        SignClock(DateTime.FromBinary((long)Sql("SELECT stamp FROM revora_sign_process_probe")));
        var p=Player(Kernel.Maps[1002],true);new CoatStorage().Login(p);
        Require(SignSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_sign_process_probe")));
        MsgSignIn.Show(p);Require(p.Entity.DailySignRewards==7 && p.Entity.DailySignDays.Take(7).All(x=>x==1));
        string before=SignSnapshot(p);Parallel.For(0,8,n=>SignRequest(p,MsgSignIn.Actions.Sign));Require(SignSnapshot(p)==before);
        Require(p.Entity.StorageItems.Values.Count(i=>i.ID==200481 && i.Days==0)==1 && p.Entity.RevoraPoint==22000);
        SignClock(new DateTime(signNow.Year,signNow.Month,1).AddMonths(1));
        MsgSignIn.Show(p);Require(p.Entity.DailySignDays.All(x=>x==0) && p.Entity.DailySignRewards==0 && p.Entity.DailySignVIPChances==10);
        SignRequest(p,MsgSignIn.Actions.Sign);Require(p.Entity.RevoraPoint==18000 && SignReceipts(p)==8 && p.Entity.DailySignDays[0]==1);
        Sql("UPDATE revora_sign_process_probe SET stamp=@stamp,snapshot=@snapshot","stamp",signNow.ToBinary(),"snapshot",SignSnapshot(p));
        Console.WriteLine("[sign-process] sign-reload-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Sign-in reload did not close.");
    }
    static void SignVerify() {
        SignClock(DateTime.FromBinary((long)Sql("SELECT stamp FROM revora_sign_process_probe")));
        var p=Player(Kernel.Maps[1002],true);new CoatStorage().Login(p);
        Require(SignSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_sign_process_probe")));
        MsgSignIn.Show(p);Require(p.Entity.RevoraPoint==18000 && p.Entity.DailySignDays.Count(x=>x==1)==1 && p.Entity.DailySignRewards==0 && p.Entity.DailySignVIPChances==10);
        string before=SignSnapshot(p);SignRequest(p,MsgSignIn.Actions.Sign);Require(SignSnapshot(p)==before);
        SignClock(signNow.AddDays(1));
        foreach(string bad in new[]{"short","long","length","type","seal","action"}) {
            var packet=new MsgSignIn{Type=MsgSignIn.Actions.Sign}.ToArray();
            Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,packet.Length-8,packet);
            if(bad=="short")Array.Resize(ref packet,19);
            if(bad=="long")Array.Resize(ref packet,65);
            if(bad=="length")packet[0]=1;
            if(bad=="type")packet[2]^=1;
            if(bad=="seal")packet[packet.Length-1]^=1;
            if(bad=="action")packet[4]=255;
            MsgSignIn.Handle(p,packet);Require(SignSnapshot(p)==before);
        }
        SignRequest(p,MsgSignIn.Actions.Sign);Require(p.Entity.RevoraPoint==14000 && SignReceipts(p)==9 && p.Entity.DailySignRewards==2);
        before=SignSnapshot(p);
        SignClock(signNow.AddMonths(-1));SignRequest(p,MsgSignIn.Actions.Sign);Require(SignSnapshot(p)==before);
        Console.WriteLine("[sign-process] sign-verify-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Sign-in verify did not close.");
    }
}
