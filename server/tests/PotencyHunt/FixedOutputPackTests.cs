using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type FixedPacks=typeof(Entity).Assembly.GetType("Revora.Game.Features.FixedOutputPacks");
    static DateTime fixedNow=DateTime.UtcNow;
    static long FixedUses(GameState p){return Scalar("SELECT count(*) FROM revora_fixed_output_pack_uses WHERE uid=@uid",p.Entity.UID);}
    static void OpenFixed(GameState p,ConquerItem item){ItemUsage.Process(PrayerPacket(item.UID),p);}
    static void VerifyFixedOutputPacks(string runtime,Map town){
        Call(FixedPacks,null,"Initialize");Call(FixedPacks,null,"Initialize");
        FixedPacks.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>fixedNow));
        string handler=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemHandler.cs")));
        Check(handler.IndexOf("Fixed output pack must use the owned service.")>=0 && handler.IndexOf("case 720757:")>=0 && handler.IndexOf("case 723268:")>=0 && handler.IndexOf("case 723711:")>=0,
            "720757, 723268 and 723711 are fail-closed on the live handler");
        foreach(uint id in new uint[]{720757u,723268u,723711u,723700u,1088001u,1088002u})
            Check(ConquerItemInformation.BaseInformations.ContainsKey(id),"installed catalog contains fixed pack identity "+id);
        string heard="";FixedPacks.GetField("Spoken",Hidden).SetValue(null,(Action<string>)(m=>{heard=heard+m+"|";}));
        foreach(bool bound in new[]{false,true})foreach(ushort stack in new ushort[]{0,1,2}){
            var p=WelcomePlayer(town,1,0,15);var pack=NestedPack(p,720757,stack,bound);
            Parallel.For(0,24,n=>{if(n%3==0)EntityTable.SaveEntity(p);else OpenFixed(p,pack);});
            int expected=stack>1?2:1;
            Check(!Fenced(p) && FixedUses(p)==expected && CountId(p,723700)==10*expected && CountId(p,720757)==0
                && p.Inventory.Objects.Where(i=>i.ID==723700).All(i=>i.Bound==bound && i.Plus==0),
                "720757 pays ten plus-0 EXP Balls with the pack binding: "+bound+"/"+stack);
            p=ReloadBall(p);Check(!Fenced(p) && FixedUses(p)==expected && CountId(p,723700)==10*expected
                && p.Inventory.Objects.Where(i=>i.ID==723700).All(i=>i.Bound==bound),"720757 reload retains ten EXP Balls and their binding");
        }
        foreach(bool bound in new[]{false,true})foreach(ushort stack in new ushort[]{0,1,2}){
            var p=WelcomePlayer(town,1,0,15);var pack=NestedPack(p,723268,stack,bound);
            Parallel.For(0,24,n=>{if(n%3==0)EntityTable.SaveEntity(p);else OpenFixed(p,pack);});
            int expected=stack>1?2:1;
            Check(!Fenced(p) && FixedUses(p)==expected && CountId(p,1088001)==5*expected && CountId(p,723268)==0
                && p.Inventory.Objects.Where(i=>i.ID==1088001).All(i=>i.Bound==bound && i.Plus==0),
                "723268 pays five binding-preserving plus-0 Meteors: "+bound+"/"+stack);
            p=ReloadBall(p);Check(!Fenced(p) && FixedUses(p)==expected && CountId(p,1088001)==5*expected,"723268 reload retains five Meteors");
        }
        heard="";var ballFit=WelcomePlayer(town,1,0,15);var ballSrc=Pack(ballFit,720757,1);while(ballFit.Inventory.Count<31)Pack(ballFit,1000000,1);OpenFixed(ballFit,ballSrc);
        Check(!Fenced(ballFit) && FixedUses(ballFit)==1 && CountId(ballFit,723700)==10 && heard.IndexOf("Your inventory is Full.")<0,"720757 singleton fits ten outputs when Count is 31");
        heard="";var ballOver=WelcomePlayer(town,1,0,15);var ballOverSrc=Pack(ballOver,720757,1);while(ballOver.Inventory.Count<32)Pack(ballOver,1000000,1);OpenFixed(ballOver,ballOverSrc);
        Check(!Fenced(ballOver) && FixedUses(ballOver)==0 && ballOver.Inventory.ContainsUID(ballOverSrc.UID) && heard.IndexOf("Your inventory is Full.")>=0,"720757 singleton refuses at Count 32 instead of granting a partial ten");
        heard="";var ballStackFit=WelcomePlayer(town,1,0,15);var ballStack=NestedPack(ballStackFit,720757,2,false);while(ballStackFit.Inventory.Count<30)Pack(ballStackFit,1000000,1);OpenFixed(ballStackFit,ballStack);
        Check(!Fenced(ballStackFit) && FixedUses(ballStackFit)==1 && CountId(ballStackFit,723700)==10,"720757 stack remainder fits ten outputs when Count is 30");
        heard="";var ballStackOver=WelcomePlayer(town,1,0,15);var ballStackOverSrc=NestedPack(ballStackOver,720757,2,false);while(ballStackOver.Inventory.Count<31)Pack(ballStackOver,1000000,1);OpenFixed(ballStackOver,ballStackOverSrc);
        Check(!Fenced(ballStackOver) && FixedUses(ballStackOver)==0 && ballStackOver.Inventory.ContainsUID(ballStackOverSrc.UID) && heard.IndexOf("Your inventory is Full.")>=0,"720757 stack remainder refuses at Count 31");
        heard="";var meteorOk=WelcomePlayer(town,1,0,15);var meteorSrc=Pack(meteorOk,723268,1);while(meteorOk.Inventory.Count<30)Pack(meteorOk,1000000,1);OpenFixed(meteorOk,meteorSrc);
        Check(!Fenced(meteorOk) && FixedUses(meteorOk)==1 && CountId(meteorOk,1088001)==5 && heard.IndexOf("Your inventory is Full.")<0,"723268 pays at legacy Count 30");
        heard="";var meteorNo=WelcomePlayer(town,1,0,15);var meteorNoSrc=Pack(meteorNo,723268,1);while(meteorNo.Inventory.Count<31)Pack(meteorNo,1000000,1);OpenFixed(meteorNo,meteorNoSrc);
        Check(!Fenced(meteorNo) && FixedUses(meteorNo)==0 && meteorNo.Inventory.ContainsUID(meteorNoSrc.UID) && CountId(meteorNo,1088001)==0 && heard.IndexOf("Your inventory is Full.")>=0,"723268 refuses at Count 31 even though five outputs would physically fit");
        string service=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/FixedOutputPacks.cs")));
        Check(service.IndexOf("p.Send(Constants.FullInventory)")>=0 && service.IndexOf("You received")<0 && service.IndexOf("Congratulations")<0,
            "fixed packs send FullInventory on refusal and have no success message");
        var drink=WelcomePlayer(town,1,0,15);var drinkPack=Pack(drink,720757,1);OpenFixed(drink,drinkPack);
        var ball=drink.Inventory.Objects.First(i=>i.ID==723700);Call(ExpItems,null,"Initialize");ulong xp=drink.Entity.Experience;ItemHandler.UseItem(ball,drink);
        Check(!Fenced(drink) && (drink.Entity.Experience>xp || drink.ExpBalls>0 || !drink.Inventory.ContainsUID(ball.UID) || ball.StackSize==0),"issued EXP Ball remains usable through ExperienceConsumables");
        var meteor=WelcomePlayer(town,1,0,15);var meteorPack=Pack(meteor,723268,1);OpenFixed(meteor,meteorPack);
        var stone=meteor.Inventory.Objects.First(i=>i.ID==1088001);
        OpenFixed(meteor,stone);
        Check(!(bool)Call(FixedPacks,null,"TryUse",stone,meteor) && meteor.Inventory.ContainsUID(stone.UID)
            && File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Npcs.cs"))).IndexOf("Contains(1088001, 3)")>=0
            && File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Npcs.cs"))).IndexOf("Add(720027, 0, 1)")<0
            && File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/MillionaireStonePacking.cs"))).IndexOf("stone=Meteor;pack=MeteorPack;need=10")>=0
            && !Fenced(meteor) && CountId(meteor,1088001)==5 && FixedUses(meteor)==1,
            "1088001 remains the Meteor catalog identity for NPC upgrade/crafting sinks without an ordinary UseItem consumable");
        foreach(string fault in new[]{"source","output","receipt"}){
            var p=WelcomePlayer(town,1,0,15);var item=Pack(p,720757,1);
            string table=fault=="receipt"?"revora_fixed_output_pack_uses":"items";
            string verb=fault=="source"?"DELETE":"INSERT";
            Query("CREATE FUNCTION suppress_fixed_pack() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_fixed_pack BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_fixed_pack()");OpenFixed(p,item);
            Check(Fenced(p) && FixedUses(p)==0 && p.Inventory.ContainsUID(item.UID) && CountId(p,723700)==0,"suppressed "+fault+" preserves 720757 source and outputs");
            Query("DROP TRIGGER suppress_fixed_pack ON "+table+";DROP FUNCTION suppress_fixed_pack()");p=ReloadBall(p);OpenFixed(p,p.Inventory.Objects.Single(i=>i.ID==720757));
            Check(!Fenced(p) && FixedUses(p)==1 && CountId(p,723700)==10,"production reload retries 720757 after "+fault+" rollback");
        }
        foreach(string guard in new[]{"dead","fake","trade","booth","locked","suspicious","wardrobe","expired","substituted"}){
            var p=WelcomePlayer(town,1,0,15);var item=Pack(p,723268,1);
            if(guard=="dead")p.Entity.Hitpoints=0;if(guard=="fake")p.Fake=true;if(guard=="trade")p.Trade.InTrade=true;
            if(guard=="booth")p.Booth=(Revora.Game.ConquerStructures.Booth)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Revora.Game.ConquerStructures.Booth));if(guard=="locked")item.Lock=1;
            if(guard=="suspicious")item.Suspicious=true;if(guard=="wardrobe")item.InWardrobe=true;if(guard=="expired"){item.Days=1;item.DayStamp=fixedNow.ToLocalTime().AddDays(-2);}
            if(guard=="substituted")ItemHandler.UseItem(new ConquerItem(true){ID=item.ID,UID=item.UID,StackSize=1},p);else OpenFixed(p,item);
            Check(FixedUses(p)==0 && CountId(p,1088001)==0 && p.Inventory.ContainsUID(item.UID),"ineligible 723268 leaves source unchanged: "+guard);
        }
        var frames=WelcomePlayer(town,1,0,15);var wire=Pack(frames,720757,1);
        foreach(string bad in new[]{"short","long","length","type","seal"}){
            byte[] frame=bad=="short"?new byte[31]:bad=="long"?new byte[129]:PrayerPacket(wire.UID);
            Writer.WriteUInt16((ushort)(frame.Length-8),0,frame);Writer.WriteUInt16(1009,2,frame);Writer.WriteUInt32(wire.UID,8,frame);Writer.WriteUInt16(4,20,frame);Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);
            if(bad=="length")Writer.WriteUInt16(1,0,frame);if(bad=="type")Writer.WriteUInt16(1008,2,frame);if(bad=="seal")frame[frame.Length-1]^=1;
            ItemUsage.Process(frame,frames);Check(!Fenced(frames) && FixedUses(frames)==0 && frames.Inventory.ContainsUID(wire.UID),"bounded 720757 rejects "+bad+" frame");
        }OpenFixed(frames,wire);Check(!Fenced(frames) && FixedUses(frames)==1 && CountId(frames,723700)==10,"ordinary 720757 works after malformed frames");
        var replay=WelcomePlayer(town,1,0,15);var once=Pack(replay,723268,1);uint uid=once.UID;OpenFixed(replay,once);
        ItemHandler.UseItem(once,replay);Check(FixedUses(replay)==1 && !replay.Inventory.ContainsUID(uid),"consumed 723268 UID cannot replay");
        var direct=WelcomePlayer(town,1,0,15);var directPack=Pack(direct,720757,1);ItemHandler.UseItem(directPack,direct);
        Check(!Fenced(direct) && FixedUses(direct)==1 && CountId(direct,723700)==10,"direct UseItem opens 720757");
        VerifyMeteorTearPack(runtime,town);
        FixedPacks.GetField("BeforeInstall",Hidden).SetValue(null,null);
        FixedPacks.GetField("Spoken",Hidden).SetValue(null,null);
    }
    static void VerifyMeteorTearPack(string runtime,Map town){
        string handler=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemHandler.cs")));
        string npcs=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Npcs.cs")));
        string catalog=File.ReadAllText("database\\itemtype-new.txt");
        string sub=File.ReadAllText("database\\ItemtypeSub.txt");
        string service=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/FixedOutputPacks.cs")));
        Check(catalog.IndexOf("723711@@MeteorTearPack")>=0 && catalog.IndexOf("1088002@@MeteorTear")>=0 && catalog.IndexOf("721330@@")<0 && sub.IndexOf("721330@@")<0,
            "installed catalogs name MeteorTearPack and MeteorTear and omit 721330");
        Check(npcs.IndexOf("Add(723711")<0 && handler.IndexOf("Add(1088002, 0, 5)")<0 && handler.IndexOf("Add(1002050, 0, 3)")>=0
            && service.IndexOf("id==723711")>=0,
            "723711 has no new issuer; 721330 keeps its leftover three-1002050 branch; receipts accept the third pack");
        long preserved=Scalar("SELECT count(*) FROM revora_fixed_output_pack_uses WHERE uid<>@uid",0);
        Query("ALTER TABLE revora_fixed_output_pack_uses DROP CONSTRAINT revora_fixed_output_pack_uses_item_type_check;ALTER TABLE revora_fixed_output_pack_uses ADD CONSTRAINT revora_fixed_output_pack_uses_item_type_check CHECK(item_type IN (720757,723268))");
        bool rejected=false;
        try{Query("INSERT INTO revora_fixed_output_pack_uses(id,uid,item_uid,item_type,before_stack,bound,rewards,used_at) VALUES('11111111-1111-1111-1111-111111111111',1,1,723711,1,false,ARRAY[1]::bigint[],now())");}
        catch(Exception){rejected=true;}
        Check(rejected && preserved>0,"old two-ID receipt constraint rejects 723711 and keeps historical receipts");
        Call(FixedPacks,null,"Initialize");Call(FixedPacks,null,"Initialize");
        Check(Scalar("SELECT count(*) FROM revora_fixed_output_pack_uses WHERE uid<>@uid",0)==preserved,"repeated Initialize preserves historical fixed-pack receipts");
        foreach(bool bound in new[]{false,true})foreach(ushort stack in new ushort[]{0,1,2}){
            var p=WelcomePlayer(town,1,0,15);var pack=NestedPack(p,723711,stack,bound);
            Parallel.For(0,16,n=>OpenFixed(p,pack));
            int expected=stack>1?2:1;
            Check(!Fenced(p) && FixedUses(p)==expected && CountId(p,1088002)==5*expected && CountId(p,723711)==0
                && p.Inventory.Objects.Where(i=>i.ID==1088002).All(i=>i.Bound==bound && i.Plus==0),
                "723711 pays five plus-0 MeteorTears with the pack binding: "+bound+"/"+stack);
            p=ReloadBall(p);Check(!Fenced(p) && FixedUses(p)==expected && CountId(p,1088002)==5*expected
                && p.Inventory.Objects.Where(i=>i.ID==1088002).All(i=>i.Bound==bound),"723711 reload retains MeteorTear binding");
        }
        string heard="";FixedPacks.GetField("Spoken",Hidden).SetValue(null,(Action<string>)(m=>{heard=heard+m+"|";}));
        foreach(ushort size in new ushort[]{0,1}){
            heard="";var fit=WelcomePlayer(town,1,0,15);var src=NestedPack(fit,723711,size,false);while(fit.Inventory.Count<36)Pack(fit,1000000,1);OpenFixed(fit,src);
            Check(!Fenced(fit) && FixedUses(fit)==1 && CountId(fit,1088002)==5 && heard.IndexOf("Your inventory is Full.")<0,"723711 zero/single fits at Count 36 size="+size);
            heard="";var over=WelcomePlayer(town,1,0,15);var overSrc=NestedPack(over,723711,size,false);while(over.Inventory.Count<37)Pack(over,1000000,1);OpenFixed(over,overSrc);
            Check(!Fenced(over) && FixedUses(over)==0 && over.Inventory.ContainsUID(overSrc.UID) && heard.IndexOf("Your inventory is Full.")>=0,"723711 zero/single refuses at Count 37 size="+size);
            heard="";var full=WelcomePlayer(town,1,0,15);var fullSrc=NestedPack(full,723711,size,true);while(full.Inventory.Count<40)Pack(full,1000000,1);OpenFixed(full,fullSrc);
            Check(!Fenced(full) && FixedUses(full)==0 && full.Inventory.ContainsUID(fullSrc.UID) && fullSrc.Bound,"full bag without five free slots refuses a bound 723711 size="+size);
        }
        heard="";var stackFit=WelcomePlayer(town,1,0,15);var stackSrc=NestedPack(stackFit,723711,2,true);while(stackFit.Inventory.Count<35)Pack(stackFit,1000000,1);OpenFixed(stackFit,stackSrc);
        Check(!Fenced(stackFit) && FixedUses(stackFit)==1 && stackSrc.StackSize==1 && stackSrc.Bound && CountId(stackFit,1088002)==5
            && stackFit.Inventory.Objects.Where(i=>i.ID==1088002).All(i=>i.Bound),"stacked bound 723711 at Count 35 pays five bound tears and keeps a bound remainder");
        heard="";var stackOver=WelcomePlayer(town,1,0,15);var stackOverSrc=NestedPack(stackOver,723711,2,false);while(stackOver.Inventory.Count<36)Pack(stackOver,1000000,1);OpenFixed(stackOver,stackOverSrc);
        Check(!Fenced(stackOver) && FixedUses(stackOver)==0 && stackOverSrc.StackSize==2 && heard.IndexOf("Your inventory is Full.")>=0,"stacked 723711 at Count 36 refuses because the remainder keeps its slot");
        var twice=WelcomePlayer(town,1,0,15);var fresh=NestedPack(twice,723711,2,false);OpenFixed(twice,fresh);OpenFixed(twice,fresh);
        Check(!Fenced(twice) && FixedUses(twice)==2 && CountId(twice,1088002)==10 && !twice.Inventory.ContainsUID(fresh.UID),"two fresh uses of a 723711 stack pay ten unbound tears");
        var wire=WelcomePlayer(town,1,0,15);var wirePack=Pack(wire,723711,1);ItemHandler.UseItem(wirePack,wire);
        Check(!Fenced(wire) && FixedUses(wire)==1 && CountId(wire,1088002)==5,"direct UseItem opens 723711");
        var prayer=WelcomePlayer(town,1,0,15);var prayerPack=Pack(prayer,723711,1);OpenFixed(prayer,prayerPack);
        Check(!Fenced(prayer) && FixedUses(prayer)==1 && CountId(prayer,1088002)==5,"1009/action-4 opens 723711");
        var tear=prayer.Inventory.Objects.First(i=>i.ID==1088002);
        Check(!(bool)Call(FixedPacks,null,"TryUse",tear,prayer) && prayer.Inventory.ContainsUID(tear.UID) && tear.Plus==0 && handler.IndexOf("case 1088002:")>=0,
            "issued 1088002 stays a MeteorTear and is not consumed by the pack service");
        foreach(string guard in new[]{"locked","suspicious","position","warehouse","wardrobe","expired","timer","trade","booth","dead","fenced","session","fake","loading"}){
            var p=WelcomePlayer(town,1,0,15);var source=Pack(p,723711,1);
            if(guard=="locked")source.Lock=1;if(guard=="suspicious")source.Suspicious=true;if(guard=="position")source.Position=1;
            if(guard=="warehouse")source.Warehouse=8;if(guard=="wardrobe")source.InWardrobe=true;
            if(guard=="expired"){source.Days=1;source.DayStamp=fixedNow.ToLocalTime().AddDays(-2);}if(guard=="timer")source.TimeLeftInMinutes=10;
            if(guard=="trade")p.Trade.InTrade=true;
            if(guard=="booth")p.Booth=(Revora.Game.ConquerStructures.Booth)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Revora.Game.ConquerStructures.Booth));
            if(guard=="dead")p.Entity.Hitpoints=0;if(guard=="fenced")typeof(Entity).GetField("PersistenceRequiresReload",Instance).SetValue(p.Entity,true);
            if(guard=="session")Kernel.GamePool[p.Entity.UID]=WelcomePlayer(town,1,0,15);if(guard=="fake")p.Fake=true;if(guard=="loading")p.Action=1;
            ItemHandler.UseItem(source,p);
            Check(FixedUses(p)==0 && p.Inventory.ContainsUID(source.UID) && CountId(p,1088002)==0,"ordinary guard "+guard+" refuses 723711");
        }
        var foreign=WelcomePlayer(town,1,0,15);var owned=Pack(foreign,723711,1);var other=WelcomePlayer(town,1,0,15);ItemHandler.UseItem(owned,other);
        Check(FixedUses(foreign)==0 && FixedUses(other)==0 && foreign.Inventory.ContainsUID(owned.UID),"foreign direct 723711 reference cannot be spent");
        foreach(string bad in new[]{"short","long","length","type","seal"}){
            var frames=WelcomePlayer(town,1,0,15);var item=Pack(frames,723711,1);
            byte[] frame=bad=="short"?new byte[31]:bad=="long"?new byte[129]:PrayerPacket(item.UID);
            Writer.WriteUInt16((ushort)(frame.Length-8),0,frame);Writer.WriteUInt16(1009,2,frame);Writer.WriteUInt32(item.UID,8,frame);Writer.WriteUInt16(4,20,frame);Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);
            if(bad=="length")Writer.WriteUInt16(1,0,frame);if(bad=="type")Writer.WriteUInt16(1008,2,frame);if(bad=="seal")frame[frame.Length-1]^=1;
            ItemUsage.Process(frame,frames);Check(!Fenced(frames) && FixedUses(frames)==0 && frames.Inventory.ContainsUID(item.UID),"bounded 723711 rejects "+bad+" frame");
        }
        foreach(string change in new[]{"stacksize=99","bound=1","locked=1","suspicious=1","position=1","warehouse=8","inwardrobe=1","days='1'","entityid=1","id=1088001"}){
            var p=WelcomePlayer(town,1,0,15);var source=Pack(p,723711,1);Query("UPDATE items SET "+change+" WHERE uid=@uid","uid",(long)source.UID);OpenFixed(p,source);
            Check(Fenced(p) && FixedUses(p)==0 && p.Inventory.ContainsUID(source.UID) && CountId(p,1088002)==0,"stale durable preimage "+change+" fences 723711");
        }
        var stamp=WelcomePlayer(town,1,0,15);var timed=Pack(stamp,723711,1);timed.Days=1;timed.DayStamp=fixedNow.ToLocalTime();
        Query("UPDATE items SET days='1',daystamp=@stamp WHERE uid=@uid","stamp",timed.DayStamp.AddHours(-1).ToBinary(),"uid",(long)timed.UID);OpenFixed(stamp,timed);
        Check(Fenced(stamp) && FixedUses(stamp)==0,"stale timed 723711 stamp fences");
        foreach(bool extra in new[]{false,true}){
            var p=WelcomePlayer(town,1,0,15);var source=Pack(p,723711,1);var filler=Pack(p,1088001,1);
            if(extra)p.Inventory.Remove(filler.UID,Enums.ItemUse.None,true);else Query("DELETE FROM items WHERE uid=@uid","uid",(long)filler.UID);
            OpenFixed(p,source);Check(Fenced(p) && FixedUses(p)==0 && CountId(p,1088002)==0,"complete durable bag rejects "+(extra?"extra":"missing")+" 723711 row");
        }
        var rebound=WelcomePlayer(town,1,0,15);var staleBound=Pack(rebound,723711,1);Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)staleBound.UID);OpenFixed(rebound,staleBound);
        rebound=ReloadBall(rebound);OpenFixed(rebound,rebound.Inventory.Objects.Single(i=>i.ID==723711));rebound=ReloadBall(rebound);
        Check(!Fenced(rebound) && FixedUses(rebound)==1 && rebound.Inventory.Objects.Where(i=>i.ID==1088002).All(i=>i.Bound) && CountId(rebound,1088002)==5,
            "reload retries stale 723711 binding without laundering unbound tears");
        foreach(string fault in new[]{"delete","update","insert","later-insert","receipt"}){
            var p=WelcomePlayer(town,1,0,15);var source=Pack(p,723711,(ushort)(fault=="update"?2:1));
            string table=fault=="receipt"?"revora_fixed_output_pack_uses":"items";
            string op=fault=="delete"?"DELETE":fault=="update"?"UPDATE":"INSERT";
            string body=fault=="later-insert"
                ?"BEGIN IF NEW.id=1088002 AND (SELECT count(*) FROM items WHERE id=1088002 AND entityid=NEW.entityid)>=1 THEN RETURN NULL; END IF; RETURN NEW; END"
                :"BEGIN RETURN NULL; END";
            Query("CREATE FUNCTION suppress_tear() RETURNS trigger LANGUAGE plpgsql AS '"+body+"';CREATE TRIGGER suppress_tear BEFORE "+op+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_tear()");
            try{OpenFixed(p,source);Check(Fenced(p) && FixedUses(p)==0 && p.Inventory.ContainsUID(source.UID) && CountId(p,1088002)==0,"silent "+fault+" fences 723711 without a live reward");}
            finally{Query("DROP TRIGGER suppress_tear ON "+table+";DROP FUNCTION suppress_tear()");}
            p=ReloadBall(p);Check(CountId(p,1088002)==0 && p.Inventory.Objects.Any(i=>i.ID==723711),"reload proves 723711 rollback after "+fault);
            OpenFixed(p,p.Inventory.Objects.Single(i=>i.ID==723711));p=ReloadBall(p);
            int left=fault=="update"?1:0;
            Check(!Fenced(p) && FixedUses(p)==1 && CountId(p,1088002)==5 && CountId(p,723711)==(left==0?0:1),"reload/retry commits five MeteorTears after "+fault);
        }
        var install=WelcomePlayer(town,1,0,15);var boundStack=NestedPack(install,723711,3,true);
        FixedPacks.GetField("BeforeInstall",Hidden).SetValue(null,(Action<GameState>)(g=>{throw new InvalidOperationException("installation");}));
        try{OpenFixed(install,boundStack);OpenFixed(install,boundStack);
            Check(Fenced(install) && FixedUses(install)==1 && boundStack.StackSize==3,"post-commit 723711 failure fences the stale projection and does not add a second receipt");}
        finally{FixedPacks.GetField("BeforeInstall",Hidden).SetValue(null,null);}
        install=ReloadBall(install);
        var remainder=install.Inventory.Objects.Single(i=>i.UID==boundStack.UID);
        Check(!Fenced(install) && FixedUses(install)==1 && remainder.StackSize==2 && remainder.Bound && CountId(install,1088002)==5
            && install.Inventory.Objects.Where(i=>i.ID==1088002).All(i=>i.Bound && i.Plus==0),
            "reload recovers five bound MeteorTears and the exact bound 723711 remainder");
    }
}
