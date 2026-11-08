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
    static readonly Type LeePack=typeof(Entity).Assembly.GetType("Revora.Game.Features.MillionaireStonePacking");
    static DateTime leeNow=DateTime.UtcNow;
    static long LeePacks(GameState p){return Scalar("SELECT count(*) FROM revora_millionaire_stone_packs WHERE uid=@uid",p.Entity.UID);}
    static int StoneUnits(GameState p,uint id){return p.Inventory.Objects.Where(i=>i.ID==id).Sum(i=>i.StackSize<=1?1:i.StackSize);}
    static void ParkLee(Map town){town.Npcs[11275]=new NpcSpawn{UID=11275,MapID=1002,X=304,Y=433,Type=Enums.NpcType.Talker};}
    static void AskLee(GameState p,byte option,uint npc){p.ActiveNpc=11275;p.Entity.MapID=1002;p.Entity.X=304;p.Entity.Y=433;Npcs.GetDialog(new NpcRequest(true){NpcID=npc,OptionID=option},p);}
    static void VerifyMillionaireStonePacking(string runtime,Map town){
        Query("CREATE TABLE IF NOT EXISTS teamarena (LIKE public.teamarena INCLUDING ALL);CREATE TABLE IF NOT EXISTS itemadding (LIKE public.itemadding INCLUDING ALL)");
        Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.PlusOneStonePacks"),null,"Initialize");
        Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.HigherStonePacks"),null,"Initialize");
        Call(LeePack,null,"Initialize");Call(LeePack,null,"Initialize");
        LeePack.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>leeNow));
        LeePack.GetField("BeforeInstall",Hidden).SetValue(null,null);
        ParkLee(town);
        string npcs=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Npcs.cs")));
        string service=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/MillionaireStonePacking.cs")));
        string audit=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../docs/ITEM_USE_COMPOUND_AUDIT.md")));
        int lee=npcs.IndexOf("case 11275:"),sereth=npcs.IndexOf("case 59558:",lee);
        string body=npcs.Substring(lee,sereth-lee);
        Check(town.ID==1002 && audit.IndexOf("| live-split-write | 145 |")>=0 && audit.IndexOf("| owned-fail-closed | 275 |")>=0,
            "item-use census stays 154/266 while stone packing is an NPC producer");
        Check(npcs.IndexOf("MillionaireStonePacking.HandleNpc")>=0 && body.IndexOf("Millionaire stone packing must use the owned service.")>=0
            && body.IndexOf("Contains(730001, 5)")<0 && body.IndexOf("Add(723712")<0 && body.IndexOf("Add(729022")<0 && body.IndexOf("Add(729023")<0,
            "options 3/4/5 no longer split-write and fail closed if the service is bypassed");
        Check(body.IndexOf("Pack gems.")>=0 && body.IndexOf("case 6:")<0 && body.IndexOf("Add(720757")<0
            && body.IndexOf("Add(720027")<0 && body.IndexOf("Add(720028")<0
            && service.IndexOf("stone=Meteor;pack=MeteorPack;need=10")>=0
            && service.IndexOf("stone=Ball;pack=BallPack;need=10")>=0
            && service.IndexOf("stone=ExpBall;pack=ExpPack;need=10")>=0,
            "options 1/2/7 are owned packing and gem option 6 has no handler");
        Check(service.IndexOf("item.Plus")<0 && service.IndexOf("if(item.Bound)bound=true")>=0,
            "packing accepts any stone Plus and binds the pack when any consumed unit is bound");
        string heard="";LeePack.GetField("Spoken",Hidden).SetValue(null,(Action<string>)(m=>{heard=heard+m+"|";}));
        var few=BallPlayer(town);for(int n=0;n<4;n++)Pack(few,730001,1);heard="";AskLee(few,3,11275);
        Check(!Fenced(few) && LeePacks(few)==0 && StoneUnits(few,730001)==4 && CountId(few,723712)==0 && heard.IndexOf("you don't have 5 stones +1")>=0,
            "four +1 stones refuse without consume");
        var plain=BallPlayer(town);for(int n=0;n<5;n++){var s=Pack(plain,730001,1);s.Plus=0;}
        AskLee(plain,3,11275);
        var made=plain.Inventory.Objects.Single(i=>i.ID==723712);
        Check(!Fenced(plain) && LeePacks(plain)==1 && StoneUnits(plain,730001)==0 && !made.Bound && made.Plus==0,
            "five Plus-0 730001 stones pack into one unbound 723712");
        plain=ReloadBall(plain);made=plain.Inventory.Objects.Single(i=>i.ID==723712);
        ItemHandler.UseItem(made,plain);
        Check(!Fenced(plain) && StoneUnits(plain,730001)==5 && plain.Inventory.Objects.Where(i=>i.ID==730001).All(i=>!i.Bound && i.Plus==1),
            "packed 723712 unpacks back into five unbound Plus-1 stones");
        plain=ReloadBall(plain);
        Check(!Fenced(plain) && StoneUnits(plain,730001)==5 && plain.Inventory.Objects.Where(i=>i.ID==730001).All(i=>!i.Bound && i.Plus==1),
            "pack then unpack round trip survives production reload");
        var mixedPlus=BallPlayer(town);var low=Pack(mixedPlus,730001,1);low.Plus=0;var high=Pack(mixedPlus,730001,1);high.Plus=1;
        for(int n=0;n<3;n++)Pack(mixedPlus,730001,1);AskLee(mixedPlus,3,11275);
        Check(!Fenced(mixedPlus) && CountId(mixedPlus,723712)==1 && StoneUnits(mixedPlus,730001)==0,
            "Plus 0 and Plus 1 stones of ID 730001 are both eligible");
        var boundPay=BallPlayer(town);NestedPack(boundPay,730001,1,true);for(int n=0;n<4;n++)Pack(boundPay,730001,1);AskLee(boundPay,3,11275);
        var boundPack=boundPay.Inventory.Objects.Single(i=>i.ID==723712);
        Check(!Fenced(boundPay) && boundPack.Bound,"a bound stone in the payment binds 723712");
        ItemHandler.UseItem(boundPack,boundPay);
        Check(!Fenced(boundPay) && boundPay.Inventory.Objects.Where(i=>i.ID==730001).All(i=>i.Bound && i.Plus==1) && StoneUnits(boundPay,730001)==5,
            "bound 723712 unpacks into five bound Plus-1 stones");
        var spare=BallPlayer(town);for(int n=0;n<5;n++)Pack(spare,730001,1);var extra=NestedPack(spare,730001,1,true);AskLee(spare,3,11275);
        Check(!Fenced(spare) && !spare.Inventory.Objects.Single(i=>i.ID==723712).Bound && spare.Inventory.ContainsUID(extra.UID) && extra.Bound,
            "UID order leaves a later bound stone unpacked and the pack unbound");
        var split=BallPlayer(town);Pack(split,730001,2);Pack(split,730001,3);AskLee(split,3,11275);
        Check(!Fenced(split) && LeePacks(split)==1 && StoneUnits(split,730001)==0 && CountId(split,723712)==1,
            "split stacks of 2 and 3 pay exactly five stones");
        var zero=BallPlayer(town);for(int n=0;n<5;n++)Pack(zero,730001,0);AskLee(zero,3,11275);
        Check(!Fenced(zero) && CountId(zero,723712)==1 && StoneUnits(zero,730001)==0,"five zero-stack stones pack once");
        var pile=BallPlayer(town);var stack=Pack(pile,730002,12);AskLee(pile,4,11275);
        Check(!Fenced(pile) && stack.StackSize==2 && CountId(pile,729022)==1 && pile.Inventory.Objects.Single(i=>i.ID==729022).Plus==0,
            "a stack of 12 +2 stones pays ten and keeps remainder 2");
        pile=ReloadBall(pile);stack=pile.Inventory.Objects.Single(i=>i.ID==730002);var opened=pile.Inventory.Objects.Single(i=>i.ID==729022);
        Check(stack.StackSize==2 && !opened.Bound,"stacked remainder and unbound +2 pack survive reload");
        ItemHandler.UseItem(opened,pile);
        Check(!Fenced(pile) && pile.Inventory.Objects.Count(i=>i.ID==730002 && i.Plus==2)==10 && stack.StackSize==2,
            "729022 unpacks into ten unbound Plus-2 stones beside the remainder");
        var tight=BallPlayer(town);Pack(tight,730003,12);FillTo(tight,40);heard="";AskLee(tight,5,11275);
        Check(!Fenced(tight) && LeePacks(tight)==0 && tight.Inventory.Objects.Single(i=>i.ID==730003).StackSize==12 && heard.IndexOf("Your inventory is Full.")>=0,
            "a full bag refuses when the +3 remainder keeps its slot");
        var freed=BallPlayer(town);for(int n=0;n<10;n++)Pack(freed,730003,1);FillTo(freed,40);AskLee(freed,5,11275);
        Check(!Fenced(freed) && CountId(freed,729023)==1 && StoneUnits(freed,730003)==0,
            "a full bag packs when the ten consumed stones release their slots");
        var again=BallPlayer(town);for(int n=0;n<10;n++)Pack(again,730001,1);AskLee(again,3,11275);AskLee(again,3,11275);
        Check(!Fenced(again) && LeePacks(again)==2 && CountId(again,723712)==2 && StoneUnits(again,730001)==0,
            "two payments of five stones produce two packs");
        var far=BallPlayer(town);for(int n=0;n<5;n++)Pack(far,730001,1);far.Entity.X=400;far.Entity.Y=433;far.ActiveNpc=11275;far.Entity.MapID=1002;
        Npcs.GetDialog(new NpcRequest(true){NpcID=11275,OptionID=3},far);
        Check(!Fenced(far) && LeePacks(far)==0 && StoneUnits(far,730001)==5,"a remote clerk request does not pack");
        var bad=BallPlayer(town);for(int n=0;n<5;n++)Pack(bad,730001,1);bad.ActiveNpc=11275;bad.Entity.MapID=1002;bad.Entity.X=304;bad.Entity.Y=433;
        Npcs.GetDialog(new NpcRequest(true){NpcID=1,OptionID=3},bad);
        Check(!Fenced(bad) && LeePacks(bad)==0 && StoneUnits(bad,730001)==5,"a mismatched NPC id does not pack");
        var menu=BallPlayer(town);for(int n=0;n<5;n++)Pack(menu,730001,1);AskLee(menu,0,11275);
        Check(!Fenced(menu) && LeePacks(menu)==0 && StoneUnits(menu,730001)==5,"the clerk menu does not pack");
        var meteor=BallPlayer(town);AskLee(meteor,1,11275);
        Check(!Fenced(meteor) && CountId(meteor,720027)==0,"Meteor packing option 1 still does not invent a pack without materials");
        var busy=BallPlayer(town);for(int n=0;n<5;n++)Pack(busy,730001,1);busy.Trade.InTrade=true;AskLee(busy,3,11275);
        Check(!Fenced(busy) && LeePacks(busy)==0 && StoneUnits(busy,730001)==5,"a trade blocks stone packing");
        var dead=BallPlayer(town);for(int n=0;n<5;n++)Pack(dead,730001,1);dead.Entity.Hitpoints=0;AskLee(dead,3,11275);
        Check(!Fenced(dead) && LeePacks(dead)==0,"a dead player cannot pack stones");
        var locked=BallPlayer(town);for(int n=0;n<5;n++){var s=Pack(locked,730001,1);s.Lock=1;Query("UPDATE items SET locked=1 WHERE uid=@uid","uid",(long)s.UID);}
        heard="";AskLee(locked,3,11275);
        Check(!Fenced(locked) && LeePacks(locked)==0 && heard.IndexOf("you don't have 5 stones +1")>=0,"locked stones are not packing material");
        var stale=BallPlayer(town);var staleStack=Pack(stale,730002,10);Query("UPDATE items SET stacksize=9 WHERE uid=@uid","uid",(long)staleStack.UID);
        AskLee(stale,4,11275);
        Check(Fenced(stale) && LeePacks(stale)==0 && staleStack.StackSize==10 && CountId(stale,729022)==0,
            "a stale durable stack refuses without projecting a pack");
        var race=BallPlayer(town);for(int n=0;n<5;n++)Pack(race,730001,1);
        Parallel.For(0,16,n=>AskLee(race,3,11275));
        Check(!Fenced(race) && LeePacks(race)==1 && CountId(race,723712)==1 && StoneUnits(race,730001)==0,
            "parallel requests spend one payment of five stones");
        var deleted=BallPlayer(town);for(int n=0;n<5;n++)Pack(deleted,730001,1);
        Query("CREATE FUNCTION suppress_lee_delete() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_lee_delete BEFORE DELETE ON items FOR EACH ROW EXECUTE FUNCTION suppress_lee_delete()");
        try{AskLee(deleted,3,11275);
            Check(Fenced(deleted) && LeePacks(deleted)==0 && StoneUnits(deleted,730001)==5 && CountId(deleted,723712)==0,
                "silent zero-row source DELETE preserves every stone");}
        finally{Query("DROP TRIGGER suppress_lee_delete ON items;DROP FUNCTION suppress_lee_delete()");}
        deleted=ReloadBall(deleted);AskLee(deleted,3,11275);
        Check(!Fenced(deleted) && LeePacks(deleted)==1 && CountId(deleted,723712)==1,"production reload retries packing after source DELETE rollback");
        var partial=BallPlayer(town);Pack(partial,730001,3);Pack(partial,730001,3);
        Query("CREATE FUNCTION suppress_lee_update() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_lee_update BEFORE UPDATE ON items FOR EACH ROW EXECUTE FUNCTION suppress_lee_update()");
        try{AskLee(partial,3,11275);
            Check(Fenced(partial) && LeePacks(partial)==0 && StoneUnits(partial,730001)==6 && CountId(partial,723712)==0,
                "partial source update rolls back the earlier delete");}
        finally{Query("DROP TRIGGER suppress_lee_update ON items;DROP FUNCTION suppress_lee_update()");}
        partial=ReloadBall(partial);AskLee(partial,3,11275);
        Check(!Fenced(partial) && CountId(partial,723712)==1 && StoneUnits(partial,730001)==1,
            "production reload retries split packing and leaves the unpaid stone");
        var reward=BallPlayer(town);for(int n=0;n<5;n++)Pack(reward,730001,1);
        Query("CREATE FUNCTION suppress_lee_pack() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF NEW.id=723712 OR NEW.id=729022 OR NEW.id=729023 THEN RETURN NULL; END IF; RETURN NEW; END';CREATE TRIGGER suppress_lee_pack BEFORE INSERT ON items FOR EACH ROW EXECUTE FUNCTION suppress_lee_pack()");
        try{AskLee(reward,3,11275);
            Check(Fenced(reward) && LeePacks(reward)==0 && StoneUnits(reward,730001)==5,
                "silent zero-row pack insert preserves the stones");}
        finally{Query("DROP TRIGGER suppress_lee_pack ON items;DROP FUNCTION suppress_lee_pack()");}
        reward=ReloadBall(reward);AskLee(reward,3,11275);
        Check(!Fenced(reward) && CountId(reward,723712)==1,"production reload retries packing after pack-insert rollback");
        var receipt=BallPlayer(town);for(int n=0;n<10;n++)Pack(receipt,730003,1);
        Query("CREATE FUNCTION suppress_lee_receipt() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_lee_receipt BEFORE INSERT ON revora_millionaire_stone_packs FOR EACH ROW EXECUTE FUNCTION suppress_lee_receipt()");
        try{AskLee(receipt,5,11275);
            Check(Fenced(receipt) && LeePacks(receipt)==0 && StoneUnits(receipt,730003)==10 && CountId(receipt,729023)==0,
                "silent zero-row receipt insert preserves ten +3 stones");}
        finally{Query("DROP TRIGGER suppress_lee_receipt ON revora_millionaire_stone_packs;DROP FUNCTION suppress_lee_receipt()");}
        receipt=ReloadBall(receipt);AskLee(receipt,5,11275);
        Check(!Fenced(receipt) && CountId(receipt,729023)==1 && StoneUnits(receipt,730003)==0,
            "production reload retries +3 packing after receipt rollback");
        var install=BallPlayer(town);for(int n=0;n<5;n++)Pack(install,730001,1);
        LeePack.GetField("BeforeInstall",Hidden).SetValue(null,(Action<GameState>)(g=>{throw new InvalidOperationException("install");}));
        try{AskLee(install,3,11275);
            Check(Fenced(install) && LeePacks(install)==1 && CountId(install,723712)==0 && StoneUnits(install,730001)==5,
                "post-commit install failure fences without projecting the pack");
            AskLee(install,3,11275);
            Check(Fenced(install) && LeePacks(install)==1,"a fenced clerk does not pack a second time");}
        finally{LeePack.GetField("BeforeInstall",Hidden).SetValue(null,null);}
        install=ReloadBall(install);
        Check(!Fenced(install) && CountId(install,723712)==1 && StoneUnits(install,730001)==0 && install.Inventory.Objects.Single(i=>i.ID==723712).Plus==0,
            "production reload shows the committed unbound pack");
        // Exercise the restrictions through NPC dispatch, with enough units to pay
        // if the material filter is accidentally relaxed.
        foreach(string restriction in new[]{"suspicious","warehouse","equipped","wardrobe","expired","invalid-expiry"}){
            var restricted=BallPlayer(town);var material=Pack(restricted,730001,5);
            if(restriction=="suspicious")material.Suspicious=true;
            if(restriction=="warehouse")material.Warehouse=1;
            if(restriction=="equipped")material.Position=1;
            if(restriction=="wardrobe")material.InWardrobe=true;
            if(restriction=="expired"){material.Days=1;material.DayStamp=leeNow.ToLocalTime().AddDays(-2);}
            if(restriction=="invalid-expiry")material.TimeLeftInMinutes=1;
            AskLee(restricted,3,11275);
            Check(!Fenced(restricted) && LeePacks(restricted)==0 && StoneUnits(restricted,730001)==5
                && SavedRune(material,"stacksize")==5 && CountId(restricted,723712)==0,
                "restricted material cannot pay at MillionaireLee: "+restriction);
        }
        var changedBound=BallPlayer(town);var changedStone=Pack(changedBound,730002,10);
        Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)changedStone.UID);
        AskLee(changedBound,4,11275);
        Check(Fenced(changedBound) && LeePacks(changedBound)==0 && CountId(changedBound,729022)==0,
            "a stale unbound projection cannot launder durable bound stones");
        changedBound=ReloadBall(changedBound);AskLee(changedBound,4,11275);changedBound=ReloadBall(changedBound);
        Check(!Fenced(changedBound) && LeePacks(changedBound)==1 && StoneUnits(changedBound,730002)==0
            && changedBound.Inventory.Objects.Single(i=>i.ID==729022).Bound,
            "reload retries the changed payment and persists a bound +2 pack");
        var boundInstall=BallPlayer(town);NestedPack(boundInstall,730003,12,true);
        LeePack.GetField("BeforeInstall",Hidden).SetValue(null,(Action<GameState>)(g=>{throw new InvalidOperationException("install");}));
        try{
            AskLee(boundInstall,5,11275);AskLee(boundInstall,5,11275);
            Check(Fenced(boundInstall) && LeePacks(boundInstall)==1 && StoneUnits(boundInstall,730003)==12
                && CountId(boundInstall,729023)==0,"bound stacked +3 payment commits once while failed installation is fenced");
        }finally{LeePack.GetField("BeforeInstall",Hidden).SetValue(null,null);}
        boundInstall=ReloadBall(boundInstall);
        var boundRemainder=boundInstall.Inventory.Objects.Single(i=>i.ID==730003);
        var recoveredPack=boundInstall.Inventory.Objects.Single(i=>i.ID==729023);
        Check(!Fenced(boundInstall) && boundRemainder.StackSize==2 && boundRemainder.Bound && recoveredPack.Bound,
            "production reload recovers bound +3 pack and exact bound remainder");
        ItemHandler.UseItem(recoveredPack,boundInstall);boundInstall=ReloadBall(boundInstall);
        Check(!Fenced(boundInstall) && StoneUnits(boundInstall,730003)==12 && CountId(boundInstall,729023)==0
            && boundInstall.Inventory.Objects.Count(i=>i.ID==730003 && i.Plus==3 && i.StackSize==1 && i.Bound)==10
            && boundInstall.Inventory.Objects.Single(i=>i.UID==boundRemainder.UID).StackSize==2,
            "bound +3 packing recovery and unpack round trip preserve twelve exact units after reload");
        VerifyClerkScrollPacking(town);
        LeePack.GetField("Spoken",Hidden).SetValue(null,null);
    }
    static void VerifyClerkScrollPacking(Map town){
        long oldReceipts=Scalar("SELECT count(*) FROM revora_millionaire_stone_packs WHERE uid<>@uid",0);
        Query("ALTER TABLE revora_millionaire_stone_packs DROP CONSTRAINT revora_millionaire_stone_packs_option_check;ALTER TABLE revora_millionaire_stone_packs ADD CONSTRAINT revora_millionaire_stone_packs_option_check CHECK(option IN (3,4,5))");
        Call(LeePack,null,"Initialize");Call(LeePack,null,"Initialize");
        Check(oldReceipts>0 && Scalar("SELECT count(*) FROM revora_millionaire_stone_packs WHERE uid<>@uid",0)==oldReceipts,
            "receipt upgrade from stone-only options is repeatable and preserves historical payments");
        Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.QuestGiftPacks"),null,"Initialize");
        Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.DragonBallPacks"),null,"Initialize");
        Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.FixedOutputPacks"),null,"Initialize");
        string heard="";LeePack.GetField("Spoken",Hidden).SetValue(null,(Action<string>)(m=>{heard=heard+m+"|";}));
        byte[] options=new byte[]{1,2,7};
        uint[] sources=new uint[]{1088001u,1088000u,723700u};
        uint[] packs=new uint[]{720027u,720028u,720757u};
        string[] poor=new string[]{"Poor man! I can't help you.","Poor man! I can't help you.","you don't have 10 Exp Balls"};
        for(int n=0;n<options.Length;n++){
            heard="";var few=BallPlayer(town);for(int i=0;i<9;i++)Pack(few,sources[n],1);AskLee(few,options[n],11275);
            Check(!Fenced(few) && LeePacks(few)==0 && StoneUnits(few,sources[n])==9 && CountId(few,packs[n])==0 && heard.IndexOf(poor[n])>=0,
                "nine units refuse clerk option "+options[n]);
            var plain=BallPlayer(town);for(int i=0;i<10;i++)Pack(plain,sources[n],1);AskLee(plain,options[n],11275);
            var made=plain.Inventory.Objects.Single(i=>i.ID==packs[n]);
            Check(!Fenced(plain) && LeePacks(plain)==1 && !made.Bound && made.Plus==0 && StoneUnits(plain,sources[n])==0,
                "ten unbound units pack option "+options[n]);
            ItemUsage.Process(PrayerPacket(made.UID),plain);
            Check(!Fenced(plain) && CountId(plain,packs[n])==0 && CountId(plain,sources[n])==10
                && plain.Inventory.Objects.Where(i=>i.ID==sources[n]).All(i=>!i.Bound && i.Plus==0),
                "1009 unpack keeps unbound plus-0 outputs for "+packs[n]);
            plain=ReloadBall(plain);
            Check(CountId(plain,sources[n])==10 && plain.Inventory.Objects.Where(i=>i.ID==sources[n]).All(i=>!i.Bound),
                "unbound unpack survives reload for "+packs[n]);
            var boundPay=BallPlayer(town);NestedPack(boundPay,sources[n],1,true);for(int i=0;i<9;i++)Pack(boundPay,sources[n],1);
            AskLee(boundPay,options[n],11275);var boundPack=boundPay.Inventory.Objects.Single(i=>i.ID==packs[n]);
            Check(boundPack.Bound && StoneUnits(boundPay,sources[n])==0,"a bound unit binds pack "+packs[n]);
            ItemHandler.UseItem(boundPack,boundPay);boundPay=ReloadBall(boundPay);
            Check(!Fenced(boundPay) && CountId(boundPay,sources[n])==10 && boundPay.Inventory.Objects.Where(i=>i.ID==sources[n]).All(i=>i.Bound),
                "direct UseItem and reload keep bound outputs for "+packs[n]);
            var zero=BallPlayer(town);for(int i=0;i<10;i++)Pack(zero,sources[n],0);AskLee(zero,options[n],11275);
            Check(!Fenced(zero) && LeePacks(zero)==1 && CountId(zero,packs[n])==1 && StoneUnits(zero,sources[n])==0,
                "zero-stack singleton payments work for option "+options[n]);
            var split=BallPlayer(town);Pack(split,sources[n],6);var remainder=NestedPack(split,sources[n],6,true);AskLee(split,options[n],11275);
            split=ReloadBall(split);
            Check(!Fenced(split) && LeePacks(split)==1 && split.Inventory.Objects.Single(i=>i.ID==packs[n]).Bound
                && split.Inventory.Objects.Single(i=>i.UID==remainder.UID).StackSize==2 && StoneUnits(split,sources[n])==2,
                "mixed split payment persists a bound pack and exact remainder for option "+options[n]);
            foreach(string restriction in new[]{"locked","suspicious","warehouse","equipped","wardrobe","expired","invalid-expiry"}){
                var restricted=BallPlayer(town);var material=Pack(restricted,sources[n],10);
                if(restriction=="locked")material.Lock=1;
                if(restriction=="suspicious")material.Suspicious=true;
                if(restriction=="warehouse")material.Warehouse=1;
                if(restriction=="equipped")material.Position=1;
                if(restriction=="wardrobe")material.InWardrobe=true;
                if(restriction=="expired"){material.Days=1;material.DayStamp=leeNow.ToLocalTime().AddDays(-2);}
                if(restriction=="invalid-expiry")material.TimeLeftInMinutes=1;
                AskLee(restricted,options[n],11275);
                Check(!Fenced(restricted) && LeePacks(restricted)==0 && CountId(restricted,packs[n])==0 && SavedRune(material,"stacksize")==10,
                    "clerk option "+options[n]+" excludes "+restriction+" payment");
            }
            var stale=BallPlayer(town);var materialStale=Pack(stale,sources[n],10);
            Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)materialStale.UID);
            AskLee(stale,options[n],11275);
            Check(Fenced(stale) && LeePacks(stale)==0 && CountId(stale,packs[n])==0 && SavedRune(materialStale,"stacksize")==10,
                "stale unbound payment cannot launder durable binding for option "+options[n]);
            stale=ReloadBall(stale);AskLee(stale,options[n],11275);stale=ReloadBall(stale);
            Check(!Fenced(stale) && LeePacks(stale)==1 && stale.Inventory.Objects.Single(i=>i.ID==packs[n]).Bound,
                "reloaded bound payment produces one bound pack for option "+options[n]);
            var staleUse=BallPlayer(town);var stalePack=Pack(staleUse,packs[n],1);
            Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)stalePack.UID);
            if(n==1)ItemUsage.Process(PrayerPacket(stalePack.UID),staleUse);else ItemHandler.UseItem(stalePack,staleUse);
            Check(Fenced(staleUse) && CountId(staleUse,sources[n])==0 && SavedRune(stalePack,"bound")==1,
                "unpack refuses stale binding for pack "+packs[n]);
            staleUse=ReloadBall(staleUse);stalePack=staleUse.Inventory.Objects.Single(i=>i.ID==packs[n]);
            if(n==1)ItemUsage.Process(PrayerPacket(stalePack.UID),staleUse);else ItemHandler.UseItem(stalePack,staleUse);
            staleUse=ReloadBall(staleUse);
            Check(!Fenced(staleUse) && CountId(staleUse,packs[n])==0 && CountId(staleUse,sources[n])==10
                && staleUse.Inventory.Objects.Where(i=>i.ID==sources[n]).All(i=>i.Bound),
                "unpack reload retries with ten bound rewards for pack "+packs[n]);
        }
        var spare=BallPlayer(town);for(int i=0;i<10;i++)Pack(spare,1088001,1);var extra=NestedPack(spare,1088001,1,true);AskLee(spare,1,11275);
        Check(!spare.Inventory.Objects.Single(i=>i.ID==720027).Bound && spare.Inventory.ContainsUID(extra.UID),
            "UID order leaves a later bound Meteor unpacked");
        var pile=BallPlayer(town);var stack=Pack(pile,1088000,12);AskLee(pile,2,11275);
        Check(!Fenced(pile) && stack.StackSize==2 && CountId(pile,720028)==1,"a stack of 12 DragonBalls pays ten and keeps two");
        var tight=BallPlayer(town);Pack(tight,723700,12);FillTo(tight,40);heard="";AskLee(tight,7,11275);
        Check(!Fenced(tight) && LeePacks(tight)==0 && tight.Inventory.Objects.Single(i=>i.ID==723700).StackSize==12 && heard.IndexOf("Your inventory is Full.")>=0,
            "a full bag refuses when the EXP Ball remainder keeps its slot");
        var freed=BallPlayer(town);for(int i=0;i<10;i++)Pack(freed,723700,1);FillTo(freed,40);AskLee(freed,7,11275);
        Check(!Fenced(freed) && CountId(freed,720757)==1 && StoneUnits(freed,723700)==0,"a full bag packs when ten EXP Balls release their slots");
        var again=BallPlayer(town);for(int i=0;i<20;i++)Pack(again,1088001,1);AskLee(again,1,11275);AskLee(again,1,11275);
        Check(!Fenced(again) && CountId(again,720027)==2 && StoneUnits(again,1088001)==0,"two Meteor payments produce two scrolls");
        var race=BallPlayer(town);for(int i=0;i<10;i++)Pack(race,1088000,1);Parallel.For(0,16,k=>AskLee(race,2,11275));
        Check(!Fenced(race) && CountId(race,720028)==1 && StoneUnits(race,1088000)==0,"parallel DragonBall requests spend one payment");
        var far=BallPlayer(town);for(int i=0;i<10;i++)Pack(far,1088001,1);far.Entity.X=400;far.Entity.Y=433;far.ActiveNpc=11275;far.Entity.MapID=1002;
        Npcs.GetDialog(new NpcRequest(true){NpcID=11275,OptionID=1},far);
        Check(!Fenced(far) && CountId(far,720027)==0,"a remote Meteor request does not pack");
        var bad=BallPlayer(town);for(int i=0;i<10;i++)Pack(bad,1088000,1);AskLee(bad,2,1);
        Check(!Fenced(bad) && CountId(bad,720028)==0,"a mismatched NPC id does not pack DragonBalls");
        var busy=BallPlayer(town);for(int i=0;i<10;i++)Pack(busy,723700,1);busy.Trade.InTrade=true;AskLee(busy,7,11275);
        Check(!Fenced(busy) && CountId(busy,720757)==0,"a trade blocks EXP Ball packing");
        var dead=BallPlayer(town);for(int i=0;i<10;i++)Pack(dead,1088001,1);dead.Entity.Hitpoints=0;AskLee(dead,1,11275);
        Check(!Fenced(dead) && CountId(dead,720027)==0,"a dead player cannot pack Meteors");
        var booth=BallPlayer(town);for(int i=0;i<10;i++)Pack(booth,1088000,1);
        booth.Booth=(Revora.Game.ConquerStructures.Booth)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Revora.Game.ConquerStructures.Booth));
        AskLee(booth,2,11275);Check(!Fenced(booth) && CountId(booth,720028)==0,"a booth blocks DragonBall packing");
        var locked=BallPlayer(town);for(int i=0;i<10;i++){var s=Pack(locked,723700,1);s.Lock=1;Query("UPDATE items SET locked=1 WHERE uid=@uid","uid",(long)s.UID);}
        heard="";AskLee(locked,7,11275);Check(!Fenced(locked) && heard.IndexOf("10 Exp Balls")>=0,"locked EXP Balls are not packing material");
        var changed=BallPlayer(town);var changedStack=Pack(changed,1088001,10);Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)changedStack.UID);
        AskLee(changed,1,11275);Check(Fenced(changed) && CountId(changed,720027)==0,"a stale unbound projection cannot launder bound Meteors");
        changed=ReloadBall(changed);AskLee(changed,1,11275);var changedPack=changed.Inventory.Objects.Single(i=>i.ID==720027);
        Check(!Fenced(changed) && changedPack.Bound,"reload retries the Meteor payment as a bound scroll");
        ItemHandler.UseItem(changedPack,changed);Check(changed.Inventory.Objects.Where(i=>i.ID==1088001).All(i=>i.Bound) && CountId(changed,1088001)==10,
            "the recovered Meteor scroll unpacks ten bound Meteors");
        var deleted=BallPlayer(town);for(int i=0;i<10;i++)Pack(deleted,1088001,1);
        Query("CREATE FUNCTION suppress_scroll_delete() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_scroll_delete BEFORE DELETE ON items FOR EACH ROW EXECUTE FUNCTION suppress_scroll_delete()");
        try{AskLee(deleted,1,11275);Check(Fenced(deleted) && CountId(deleted,720027)==0 && StoneUnits(deleted,1088001)==10,"silent source DELETE preserves Meteors");}
        finally{Query("DROP TRIGGER suppress_scroll_delete ON items;DROP FUNCTION suppress_scroll_delete()");}
        deleted=ReloadBall(deleted);AskLee(deleted,1,11275);Check(!Fenced(deleted) && CountId(deleted,720027)==1,"reload retries Meteor packing after source DELETE rollback");
        var partial=BallPlayer(town);Pack(partial,1088000,6);Pack(partial,1088000,6);
        Query("CREATE FUNCTION suppress_scroll_update() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_scroll_update BEFORE UPDATE ON items FOR EACH ROW EXECUTE FUNCTION suppress_scroll_update()");
        try{AskLee(partial,2,11275);Check(Fenced(partial) && CountId(partial,720028)==0 && StoneUnits(partial,1088000)==12,"partial DragonBall update rolls back the earlier delete");}
        finally{Query("DROP TRIGGER suppress_scroll_update ON items;DROP FUNCTION suppress_scroll_update()");}
        partial=ReloadBall(partial);AskLee(partial,2,11275);Check(!Fenced(partial) && CountId(partial,720028)==1 && StoneUnits(partial,1088000)==2,
            "reload retries split DragonBall packing and leaves two unpaid balls");
        var reward=BallPlayer(town);for(int i=0;i<10;i++)Pack(reward,723700,1);
        Query("CREATE FUNCTION suppress_scroll_pack() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF NEW.id IN (720027,720028,720757) THEN RETURN NULL; END IF; RETURN NEW; END';CREATE TRIGGER suppress_scroll_pack BEFORE INSERT ON items FOR EACH ROW EXECUTE FUNCTION suppress_scroll_pack()");
        try{AskLee(reward,7,11275);Check(Fenced(reward) && CountId(reward,720757)==0 && StoneUnits(reward,723700)==10,"silent pack insert preserves EXP Balls");}
        finally{Query("DROP TRIGGER suppress_scroll_pack ON items;DROP FUNCTION suppress_scroll_pack()");}
        reward=ReloadBall(reward);AskLee(reward,7,11275);Check(!Fenced(reward) && CountId(reward,720757)==1,"reload retries EXP Ball packing after pack-insert rollback");
        var receipt=BallPlayer(town);for(int i=0;i<10;i++)Pack(receipt,1088001,1);
        Query("CREATE FUNCTION suppress_scroll_receipt() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_scroll_receipt BEFORE INSERT ON revora_millionaire_stone_packs FOR EACH ROW EXECUTE FUNCTION suppress_scroll_receipt()");
        try{AskLee(receipt,1,11275);Check(Fenced(receipt) && LeePacks(receipt)==0 && StoneUnits(receipt,1088001)==10,"silent receipt insert preserves Meteors");}
        finally{Query("DROP TRIGGER suppress_scroll_receipt ON revora_millionaire_stone_packs;DROP FUNCTION suppress_scroll_receipt()");}
        receipt=ReloadBall(receipt);AskLee(receipt,1,11275);Check(!Fenced(receipt) && CountId(receipt,720027)==1,"reload retries Meteor packing after receipt rollback");
        var install=BallPlayer(town);NestedPack(install,723700,12,true);
        LeePack.GetField("BeforeInstall",Hidden).SetValue(null,(Action<GameState>)(g=>{throw new InvalidOperationException("install");}));
        try{AskLee(install,7,11275);AskLee(install,7,11275);
            Check(Fenced(install) && LeePacks(install)==1 && CountId(install,720757)==0 && StoneUnits(install,723700)==12,
                "bound stacked EXP Ball payment commits once while failed installation is fenced");}
        finally{LeePack.GetField("BeforeInstall",Hidden).SetValue(null,null);}
        install=ReloadBall(install);var left=install.Inventory.Objects.Single(i=>i.ID==723700);var packed=install.Inventory.Objects.Single(i=>i.ID==720757);
        Check(!Fenced(install) && left.StackSize==2 && left.Bound && packed.Bound,"reload recovers the bound EXP Ball pack and remainder");
        ItemHandler.UseItem(packed,install);install=ReloadBall(install);
        Check(StoneUnits(install,723700)==12 && install.Inventory.Objects.Count(i=>i.ID==723700 && i.Bound && i.StackSize==1)==10
            && install.Inventory.Objects.Single(i=>i.UID==left.UID).StackSize==2,
            "bound EXP Ball pack unpacks ten bound balls and keeps the remainder");
    }
}
