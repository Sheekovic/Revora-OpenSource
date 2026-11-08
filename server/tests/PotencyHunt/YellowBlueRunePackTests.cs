using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type YbRune=typeof(Entity).Assembly.GetType("Revora.Game.Features.YellowBlueRunePacks");
    static readonly DateTime ybNow=DateTime.UtcNow;
    static int ybPick=1,ybMax;
    static long YbCombines(GameState p){return Scalar("SELECT count(*) FROM revora_yb_rune_combines WHERE uid=@uid",p.Entity.UID);}
    static long YbOpens(GameState p){return Scalar("SELECT count(*) FROM revora_yb_rune_pack_uses WHERE uid=@uid",p.Entity.UID);}
    static void YbUse(GameState p,ConquerItem item){ItemUsage.Process(PrayerPacket(item.UID),p);}
    static void VerifyYellowBlueRunePacks(string runtime,Map town){
        Query("CREATE TABLE IF NOT EXISTS teamarena (LIKE public.teamarena INCLUDING ALL);CREATE TABLE IF NOT EXISTS itemadding (LIKE public.itemadding INCLUDING ALL)");
        Call(YbRune,null,"Initialize");Call(YbRune,null,"Initialize");
        YbRune.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>ybNow));
        YbRune.GetField("Roll",Hidden).SetValue(null,(Func<int,int,int>)((min,max)=>{Check(min==1 && max==ybMax,"legacy exclusive random bounds");return ybPick;}));
        YbRune.GetField("BeforeInstall",Hidden).SetValue(null,null);
        string handler=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemHandler.cs")));
        string usage=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemUsage.cs")));
        string thread=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Thread.cs")));
        string audit=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../docs/ITEM_USE_COMPOUND_AUDIT.md")));
        Check(handler.Contains("YellowBlueRunePacks.TryUse") && usage.Contains("YellowBlueRunePacks.TryHandle") && thread.Contains("YellowBlueRunePacks.Initialize"),"yellow/blue service is initialized and owns both dispatch paths");
        Check(handler.Split(new[]{"Yellow/blue rune item must use the owned service."},StringSplitOptions.None).Length==5,"all four leftover branches fail closed");
        Check(audit.Contains("| live-split-write | 145 |") && audit.Contains("| owned-fail-closed | 275 |"),"four newly owned cases give census 145/275/55/43");
        foreach(uint id in new uint[]{3306370,3306371,3306510,3306511})
            Check(audit.Contains("| "+id+" | owned-fail-closed | source+items |"),"audit owns yellow/blue item "+id);
        foreach(uint fragment in new uint[]{3306370,3306371}){
            uint pack=fragment==3306370?3306510u:3306511u;
            uint first=fragment==3306370?4030101u:4020101u;
            ybMax=fragment==3306370?15:10;ybPick=1;
            var poor=BallPlayer(town);var few=Pack(poor,fragment,19);YbUse(poor,few);
            Check(!Fenced(poor) && YbCombines(poor)==0 && StoneUnits(poor,fragment)==19,"19 fragments cannot pay for "+pack);
            foreach(ushort size in new ushort[]{0,1,20,21}){
                var p=BallPlayer(town);ConquerItem source=null;
                for(int i=0;i<(size<=1?20:1);i++)source=Pack(p,fragment,size);
                Check(p.Account.State==AccountTable.AccountState.Player,"ordinary player combines "+fragment);
                YbUse(p,source);
                Check(!Fenced(p) && YbCombines(p)==1 && StoneUnits(p,fragment)==(size==21?1:0) && CountId(p,pack)==1,"zero/single/exact/remainder payment "+fragment+" size="+size);
                var result=p.Inventory.Objects.Single(i=>i.ID==pack);
                Check(result.Bound && result.Plus==0,"unbound fragments still produce the legacy bound Plus-0 pack");
                p=ReloadBall(p);result=p.Inventory.Objects.Single(i=>i.ID==pack);ItemHandler.UseItem(result,p);p=ReloadBall(p);
                var rune=p.Inventory.Objects.Single(i=>i.ID==first);
                Check(!Fenced(p) && YbOpens(p)==1 && rune.Bound && rune.Plus==0 && rune.StackSize==1 && rune.RuneSkill==ConquerItemInformation.BaseInformations[first].subtype
                    && StoneUnits(p,fragment)==(size==21?1:0),"combine/open/reload preserves bound rune metadata and exact remainder");
            }
            var ordered=BallPlayer(town);var early=Pack(ordered,fragment,3);var big=NestedPack(ordered,fragment,22,true);var later=NestedPack(ordered,fragment,2,true);
            YbUse(ordered,later);
            Check(!Fenced(ordered) && !ordered.Inventory.ContainsUID(early.UID) && big.StackSize==5 && later.StackSize==2 && CountId(ordered,pack)==1,"UID order consumes 3 then 17, never preferring a later large stack or clicked UID");
            Check(Scalar("SELECT count(*) FROM revora_yb_rune_combines WHERE uid=@uid AND consumed=20 AND bound AND amounts=ARRAY[3,17]",ordered.Entity.UID)==1,"durable receipt records exact ordered amounts");
            ordered=ReloadBall(ordered);YbUse(ordered,ordered.Inventory.Objects.Single(i=>i.ID==pack));ordered=ReloadBall(ordered);
            Check(StoneUnits(ordered,fragment)==7 && ordered.Inventory.Objects.All(i=>i.Bound),"mixed fragments and unconsumed bound remainder cannot launder through pack/open/reload");
            foreach(ushort size in new ushort[]{20,21}){
                var p=BallPlayer(town);var source=Pack(p,fragment,size);FillTo(p,40);YbUse(p,source);
                Check(!Fenced(p) && YbCombines(p)==(size==20?1:0) && p.Inventory.Count==40,"full bag combine uses slots actually released size="+size);
            }
            var skipped=BallPlayer(town);var locked=Pack(skipped,fragment,20);locked.Lock=1;Query("UPDATE items SET locked=1 WHERE uid=@uid","uid",(long)locked.UID);
            var eligible=Pack(skipped,fragment,20);YbUse(skipped,eligible);
            Check(!Fenced(skipped) && locked.StackSize==20 && CountId(skipped,pack)==1,"ineligible early source is excluded from payment");
            for(int pick=1;pick<ybMax;pick++){
                ybPick=pick;uint expected=fragment==3306370?(uint)(4030001+Math.Min(pick,13)*100):(uint)(4020001+pick*100);
                var p=BallPlayer(town);var source=NestedPack(p,pack,1,pick%2==0);YbUse(p,source);p=ReloadBall(p);
                var rune=p.Inventory.Objects.Single(i=>i.ID==expected);
                Check(!Fenced(p) && YbOpens(p)==1 && rune.Bound && rune.Plus==0 && rune.Color==(Enums.Color)3 && rune.RuneSkill==ConquerItemInformation.BaseInformations[expected].subtype,"legacy reward pick "+pick+" for "+pack+" survives reload");
            }
            foreach(int pick in new[]{0,ybMax}){
                ybPick=pick;var p=BallPlayer(town);var source=Pack(p,pack,1);YbUse(p,source);
                Check(!Fenced(p) && YbOpens(p)==0 && p.Inventory.ContainsUID(source.UID),"exclusive/unmapped rune roll cannot consume a pack");
            }
            ybPick=1;
            foreach(ushort size in new ushort[]{0,1,2}){
                var p=BallPlayer(town);var source=Pack(p,pack,size);FillTo(p,40);YbUse(p,source);
                Check(!Fenced(p) && YbOpens(p)==(size<=1?1:0) && p.Inventory.Count==40,"full bag open requires released slot size="+size);
            }
            foreach(uint id in new[]{fragment,pack}){
                ushort amount=(ushort)(id==fragment?20:1);
                var race=BallPlayer(town);var source=Pack(race,id,amount);Parallel.For(0,12,n=>YbUse(race,source));ItemHandler.UseItem(source,race);
                Check(!Fenced(race) && YbCombines(race)+YbOpens(race)==1,"parallel and consumed-UID replay has one payment for "+id);
                var twice=BallPlayer(town);var stack=Pack(twice,id,(ushort)(amount*2));YbUse(twice,stack);YbUse(twice,stack);
                Check(!Fenced(twice) && YbCombines(twice)+YbOpens(twice)==2 && !twice.Inventory.ContainsUID(stack.UID),"two legitimate fresh uses of remaining stack succeed for "+id);
                VerifyYbGuards(town,id,amount);
                VerifyYbStale(town,id,amount);
                VerifyYbFaults(town,id,amount,id==fragment?pack:first);
                var install=BallPlayer(town);var bound=NestedPack(install,id,(ushort)(amount+2),true);
                YbRune.GetField("BeforeInstall",Hidden).SetValue(null,(Action<GameState>)(g=>{throw new InvalidOperationException("installation");}));
                try{YbUse(install,bound);YbUse(install,bound);Check(Fenced(install) && YbCombines(install)+YbOpens(install)==1 && bound.StackSize==amount+2,"post-commit failure fences stale projection and retry");}
                finally{YbRune.GetField("BeforeInstall",Hidden).SetValue(null,null);}
                install=ReloadBall(install);
                Check(!Fenced(install) && install.Inventory.Objects.Single(i=>i.UID==bound.UID).StackSize==2 && install.Inventory.Objects.All(i=>i.Bound)
                    && CountId(install,id==fragment?pack:first)==1,"reload recovers exactly one bound reward and two bound remainder units");
                var frames=BallPlayer(town);var wire=Pack(frames,id,amount);
                foreach(string fault in new[]{"length","type","action","seal","short","oversize"}){
                    var bytes=PrayerPacket(wire.UID);
                    if(fault=="length")bytes[0]++;if(fault=="type")bytes[2]++;if(fault=="action")bytes[20]=5;if(fault=="seal")bytes[bytes.Length-1]^=1;
                    if(fault=="short")Array.Resize(ref bytes,12);if(fault=="oversize")Array.Resize(ref bytes,129);
                    Check((bool)Call(YbRune,null,"TryHandle",frames,bytes) && YbCombines(frames)+YbOpens(frames)==0,"owned malformed frame refuses "+fault);
                }
                ItemHandler.UseItem(wire,frames);Check(!Fenced(frames) && YbCombines(frames)+YbOpens(frames)==1,"direct use remains functional after malformed frames");
            }
        }
        YbRune.GetField("Roll",Hidden).SetValue(null,(Func<int,int,int>)((min,max)=>new Random().Next(min,max)));
    }
    static void VerifyYbGuards(Map town,uint id,ushort amount){
        foreach(string guard in new[]{"locked","suspicious","position","warehouse","wardrobe","expired","timer","trade","booth","dead","fenced","session","fake","loading"}){
            var p=BallPlayer(town);var source=Pack(p,id,amount);
            if(guard=="locked")source.Lock=1;if(guard=="suspicious")source.Suspicious=true;if(guard=="position")source.Position=1;
            if(guard=="warehouse")source.Warehouse=8;if(guard=="wardrobe")source.InWardrobe=true;
            if(guard=="expired"){source.Days=1;source.DayStamp=ybNow.ToLocalTime().AddDays(-2);}if(guard=="timer")source.TimeLeftInMinutes=10;
            if(guard=="trade")p.Trade.InTrade=true;
            if(guard=="booth")p.Booth=(Revora.Game.ConquerStructures.Booth)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Revora.Game.ConquerStructures.Booth));
            if(guard=="dead")p.Entity.Hitpoints=0;if(guard=="fenced")typeof(Entity).GetField("PersistenceRequiresReload",Instance).SetValue(p.Entity,true);
            if(guard=="session")Kernel.GamePool[p.Entity.UID]=BallPlayer(town);if(guard=="fake")p.Fake=true;if(guard=="loading")p.Action=1;
            ItemHandler.UseItem(source,p);
            Check(YbCombines(p)+YbOpens(p)==0 && p.Inventory.ContainsUID(source.UID),"ordinary guard "+guard+" refuses "+id);
        }
        var timed=BallPlayer(town);var valid=Pack(timed,id,amount);valid.Days=1;valid.DayStamp=ybNow.ToLocalTime();
        Query("UPDATE items SET days='1',daystamp=@stamp WHERE uid=@uid","stamp",valid.DayStamp.ToBinary(),"uid",(long)valid.UID);YbUse(timed,valid);
        Check(!Fenced(timed) && YbCombines(timed)+YbOpens(timed)==1,"unexpired matching timed source is eligible");
        var owner=BallPlayer(town);var owned=Pack(owner,id,amount);var other=BallPlayer(town);ItemHandler.UseItem(owned,other);
        Check(YbCombines(owner)+YbOpens(owner)+YbCombines(other)+YbOpens(other)==0 && owner.Inventory.ContainsUID(owned.UID),"foreign direct item reference cannot be spent");
    }
    static void VerifyYbStale(Map town,uint id,ushort amount){
        foreach(string change in new[]{"stacksize=99","bound=1","locked=1","suspicious=1","position=1","warehouse=8","inwardrobe=1","days='1'","entityid=1","id=1088001"}){
            var p=BallPlayer(town);var source=Pack(p,id,amount);Query("UPDATE items SET "+change+" WHERE uid=@uid","uid",(long)source.UID);YbUse(p,source);
            Check(Fenced(p) && YbCombines(p)+YbOpens(p)==0 && p.Inventory.ContainsUID(source.UID),"stale durable preimage "+change+" fences "+id);
        }
        var stamp=BallPlayer(town);var timed=Pack(stamp,id,amount);timed.Days=1;timed.DayStamp=ybNow.ToLocalTime();
        Query("UPDATE items SET days='1',daystamp=@stamp WHERE uid=@uid","stamp",timed.DayStamp.AddHours(-1).ToBinary(),"uid",(long)timed.UID);YbUse(stamp,timed);
        Check(Fenced(stamp) && YbCombines(stamp)+YbOpens(stamp)==0,"stale timed source stamp fences");
        foreach(bool extra in new[]{false,true}){
            var p=BallPlayer(town);var source=Pack(p,id,amount);var filler=Pack(p,1088001,1);
            if(extra)p.Inventory.Remove(filler.UID,Enums.ItemUse.None,true);else Query("DELETE FROM items WHERE uid=@uid","uid",(long)filler.UID);
            YbUse(p,source);Check(Fenced(p) && YbCombines(p)+YbOpens(p)==0,"complete durable bag rejects "+(extra?"extra":"missing")+" row");
        }
        var bound=BallPlayer(town);var stale=Pack(bound,id,amount);Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)stale.UID);YbUse(bound,stale);bound=ReloadBall(bound);
        YbUse(bound,bound.Inventory.Objects.Single(i=>i.ID==id));bound=ReloadBall(bound);
        Check(!Fenced(bound) && YbCombines(bound)+YbOpens(bound)==1 && bound.Inventory.Objects.All(i=>i.Bound),"reload retries stale binding without laundering");
    }
    static void VerifyYbFaults(Map town,uint id,ushort amount,uint output){
        foreach(string fault in new[]{"delete","later-delete","update","insert","receipt"}){
            var p=BallPlayer(town);ConquerItem source;
            uint later=0;
            if(fault=="later-delete" && amount==20){source=Pack(p,id,10);later=Pack(p,id,10).UID;}
            else if(fault=="update" && amount==20){source=Pack(p,id,10);Pack(p,id,12);}else source=Pack(p,id,(ushort)(amount+(fault=="update"?2:0)));
            int before=StoneUnits(p,id);
            string table=fault=="receipt"?(amount==20?"revora_yb_rune_combines":"revora_yb_rune_pack_uses"):"items";
            string op=fault=="delete" || fault=="later-delete"?"DELETE":fault=="update"?"UPDATE":"INSERT";
            string when=later==0?"":" WHEN (OLD.uid="+later+")";
            Query("CREATE FUNCTION suppress_yb() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_yb BEFORE "+op+" ON "+table+" FOR EACH ROW"+when+" EXECUTE FUNCTION suppress_yb()");
            try{YbUse(p,source);Check(Fenced(p) && YbCombines(p)+YbOpens(p)==0 && StoneUnits(p,id)==before && CountId(p,output)==0,"silent "+fault+" fences without live reward for "+id);}
            finally{Query("DROP TRIGGER suppress_yb ON "+table+";DROP FUNCTION suppress_yb()");}
            p=ReloadBall(p);
            Check(StoneUnits(p,id)==before && CountId(p,output)==0,"reload proves rollback of all prior writes for "+fault);
            YbUse(p,p.Inventory.Objects.Where(i=>i.ID==id).OrderBy(i=>i.UID).First());p=ReloadBall(p);
            Check(!Fenced(p) && YbCombines(p)+YbOpens(p)==1 && StoneUnits(p,id)==before-amount && CountId(p,output)==1,"reload/retry commits one reward and exact remainder after "+fault);
        }
    }
}
