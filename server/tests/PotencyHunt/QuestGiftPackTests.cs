using System;
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
    static readonly Type QuestGifts=typeof(Entity).Assembly.GetType("Revora.Game.Features.QuestGiftPacks");
    static DateTime giftNow=DateTime.UtcNow;
    static long GiftReceipts(GameState p){return Scalar("SELECT count(*) FROM revora_quest_gift_openings WHERE uid=@uid",p.Entity.UID);}
    static void OpenGift(GameState p,ConquerItem item){ItemUsage.Process(PrayerPacket(item.UID),p);}
    static void VerifyCustomPackBoundaries(Map town) {
        var historical=BallPlayer(town);
        Query("INSERT INTO revora_quest_gift_openings(id,uid,item_uid,item_type,before_stack,bound,rewards,silver,opened_at,cps,rvps) VALUES(@id,@uid,1,721269,1,false,'{}',0,@now,5000,1000)","id",Guid.NewGuid(),"uid",(long)historical.Entity.UID,"now",DateTime.UtcNow);
        Call(QuestGifts,null,"Initialize");
        Check(GiftReceipts(historical)==1 && historical.Inventory.Count==0 && Scalar("SELECT cardinality(rewards) FROM revora_quest_gift_openings WHERE uid=@uid",historical.Entity.UID)==0,"restoring custom P7 rewards preserves historical currency-only receipts without retroactive gifts");

        QuestGifts.GetField("SoulRoll",Hidden).SetValue(null,(Func<int,int>)(max=>0));
        foreach(uint type in new uint[]{721269,720957})foreach(bool bound in new[]{false,true})foreach(ushort stack in new ushort[]{0,3}) {
            var p=BallPlayer(town);var pack=NestedPack(p,type,stack,bound);uint cp=p.Entity.ConquerPoints,rvp=p.Entity.RevoraPoint;
            Parallel.For(0,12,i=>OpenGift(p,pack));
            Check(!Fenced(p) && GiftReceipts(p)==1 && p.Entity.ConquerPoints==cp+5000 && p.Entity.RevoraPoint==rvp+1000
                && (stack==0?!p.Inventory.ContainsUID(pack.UID):pack.StackSize==2)
                && p.Inventory.Objects.Count(i=>i.ID>=800000 && i.ID<900000 && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7 && i.Bound)==1,
                "custom pack stack/binding/concurrent opening grants exact currencies and one P7");
        }
        foreach(uint type in new uint[]{721269,720957}) {
            var p=BallPlayer(town);var pack=NestedPack(p,type,2,false);for(int i=0;i<39;i++)NestedPack(p,1088001,1,false);
            OpenGift(p,pack);Check(!Fenced(p) && p.Inventory.Count==40 && pack.StackSize==2 && GiftReceipts(p)==0,"full bag refuses stacked custom pack without payment");
            p=BallPlayer(town);pack=NestedPack(p,type,1,false);
            Query("UPDATE entities SET conquerpoints=@value WHERE uid=@uid","value",(long)uint.MaxValue-4999,"uid",(long)p.Entity.UID);p=ReloadBall(p);pack=p.Inventory.Objects.Single();
            OpenGift(p,pack);Check(!Fenced(p) && p.Inventory.ContainsUID(pack.UID) && GiftReceipts(p)==0,"custom pack refuses CP overflow without consuming source");
            Query("UPDATE entities SET conquerpoints=0,revorapoint=@value WHERE uid=@uid","value",(long)uint.MaxValue-999,"uid",(long)p.Entity.UID);p=ReloadBall(p);pack=p.Inventory.Objects.Single();
            OpenGift(p,pack);Check(!Fenced(p) && p.Inventory.ContainsUID(pack.UID) && GiftReceipts(p)==0,"custom pack refuses RVP overflow without consuming source");
        }
    }
    static void VerifyQuestVariableSaves(Map town) {
        Query("CREATE TABLE IF NOT EXISTS variablevault(entityid bigint PRIMARY KEY,data bytea NOT NULL)");
        foreach(bool sharedConnection in new[]{false,true}) {
            var p=BallPlayer(town);p.Variables=new VariableVault();p.Variables["soul-trial-bird"]=49u;
            Query("INSERT INTO variablevault(entityid,data) VALUES(@uid,@data)","uid",(long)p.Entity.UID,"data",new VariableVault().Serialize());
            Action save=()=> {
                if(!sharedConnection) {EntityVariableTable.Save(p);return;}
                using(var c=DataHolder.MySqlConnection) {
                    c.Open();typeof(EntityVariableTable).GetMethod("Save",Hidden,null,new[]{typeof(GameState),typeof(Npgsql.NpgsqlConnection)},null).Invoke(null,new object[]{p,c});
                }
            };
            foreach(string body in new[]{"RETURN NULL;","RAISE EXCEPTION ''fixture variable write failure'';"}) {
                Query("CREATE FUNCTION suppress_quest_vars() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN "+body+" END';CREATE TRIGGER suppress_quest_vars BEFORE UPDATE ON variablevault FOR EACH ROW EXECUTE FUNCTION suppress_quest_vars()");
                bool failed=false;
                try {save();}catch(Exception){failed=true;}
                finally {Query("DROP TRIGGER suppress_quest_vars ON variablevault;DROP FUNCTION suppress_quest_vars()");}
                Check(failed && p.Variables.Changed,"failed variable write retains pending progress: shared="+sharedConnection);
                save();VariableVault read;EntityVariableTable.Load(p.Entity.UID,out read);
                Check(!p.Variables.Changed && (uint)read["soul-trial-bird"]==49,"variable retry durably retains progress: shared="+sharedConnection);
                p.Variables["soul-trial-bird"]=49u;
            }
            Parallel.For(0,40,i=> {p.Variables["fixture-progress-"+i]=(uint)(i+1);save();});
            save();VariableVault restored;EntityVariableTable.Load(p.Entity.UID,out restored);
            Check(!p.Variables.Changed && Enumerable.Range(0,40).All(i=>(uint)restored["fixture-progress-"+i]==(uint)(i+1)),
                "concurrent variable writers and saves retain every progress entry: shared="+sharedConnection);
            Query("DELETE FROM variablevault WHERE entityid=@uid","uid",(long)p.Entity.UID);p.Variables["soul-trial-bird"]=50u;
            bool missing=false;try {save();}catch(Exception){missing=true;}
            Check(missing && p.Variables.Changed,"missing variable owner row cannot acknowledge unsaved progress: shared="+sharedConnection);
        }
        var vault=new VariableVault();
        Parallel.For(0,100,i=> {
            vault["snapshot-"+i]=(uint)i;byte[] image=vault.Serialize();var copy=new VariableVault();copy.Deserialize(image);
            if(image.Length!=4+17*System.BitConverter.ToInt32(image,0) || (uint)copy["snapshot-"+i]!=(uint)i)
                throw new InvalidOperationException("Variable snapshot did not round trip.");
        });
        Check(System.BitConverter.ToInt32(vault.Serialize(),0)==100,"concurrent insertion produces complete readable variable snapshots");
    }
    static void VerifyStarterSoulCatalog() {
        foreach(var type in new[]{typeof(Revora.Game.Features.CourierLoopSystem),typeof(Revora.Game.Features.SoulTrialSystem)}) {
            var pool=new System.Collections.Generic.List<uint>();var item=ConquerItemInformation.BaseInformations[800020];var grade=item.PurificationLevel;
            try {
                Call(type,null,"Offer",pool,800020u);item.PurificationLevel=6;Call(type,null,"Offer",pool,800020u);Call(type,null,"Offer",pool,uint.MaxValue);
                Check(pool.SequenceEqual(new uint[]{800020}),type.Name+" rejects a catalog downgrade and missing candidate instead of granting a non-P7 soul");
            } finally {item.PurificationLevel=grade;}
            foreach(byte profession in new byte[]{15,25,45,55,65,75,85,100,135,145,165}) {
                uint id=(uint)Call(type,null,"PickClassSoul",profession);
                Check(id!=0 && ConquerItemInformation.BaseInformations[id].PurificationLevel==7,
                    type.Name+" retains a loaded P7 selection for class "+profession);
            }
        }
    }
    static void VerifyQuestGifts(string runtime,Map town){
        VerifyFirstRebirth(runtime,town);Call(QuestGifts,null,"Initialize");Call(QuestGifts,null,"Initialize");
        QuestGifts.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>giftNow));
        VerifyStarterSoulCatalog();
        VerifyQuestVariableSaves(town);
        VerifyCustomPackBoundaries(town);
        VerifyLotteryDraws(runtime,town);
        foreach(uint type in new uint[]{720027,723714})foreach(bool bound in new[]{false,true})foreach(ushort stack in new ushort[]{0,1,3}){
            var p=BallPlayer(town);var pack=NestedPack(p,type,stack,bound);ulong money=p.Entity.Money;
            Parallel.For(0,24,n=>{if(n%3==0)EntityTable.SaveEntity(p);else OpenGift(p,pack);});
            Check(!Fenced(p) && GiftReceipts(p)==1 && p.Account.State==AccountTable.AccountState.Player && p.Entity.Money==money+(type==723714?800000u:0)
                && (stack>1?pack.StackSize==stack-1:!p.Inventory.ContainsUID(pack.UID)) && p.Inventory.Objects.Count(i=>i.ID==1088001)==(type==720027?10:0)
                && p.Inventory.Objects.Where(i=>i.ID==1088001).All(i=>i.Bound==bound && i.StackSize==1 && i.Durability==ConquerItemInformation.BaseInformations[1088001].Durability),
                "ordinary packet use and autosave grant exact catalog gift once: "+type+"/"+bound+"/"+stack);
            ItemHandler.UseItem(pack,p);Check(GiftReceipts(p)==1,"item-use and direct dispatch share singleton replay and stacked debounce");
            p=ReloadBall(p);
            Check(!Fenced(p) && GiftReceipts(p)==1 && p.Entity.Money==money+(type==723714?800000u:0)
                && p.Inventory.Objects.Count(i=>i.ID==1088001)==(type==720027?10:0) && p.Inventory.Objects.Where(i=>i.ID==1088001).All(i=>i.Bound==bound),
                "production reload retains entire gift, binding and silver without CP or quest rewards");
            if(stack>1){pack=p.Inventory.Objects.Single(i=>i.UID==pack.UID);giftNow=giftNow.AddSeconds(1);OpenGift(p,pack);giftNow=giftNow.AddSeconds(1);OpenGift(p,pack);
                Check(!Fenced(p) && GiftReceipts(p)==3 && !p.Inventory.ContainsUID(pack.UID) && p.Entity.Money==money+(type==723714?2400000u:0)
                    && p.Inventory.Objects.Count(i=>i.ID==1088001)==(type==720027?30:0),"separate intentional uses consume exactly the remaining stacked units");}
            else {OpenGift(p,pack);Check(GiftReceipts(p)==1,"spent source UID cannot reopen after reconnect");}
        }
        foreach(ushort stack in new ushort[]{1,2}){
            var p=BallPlayer(town);var scroll=Pack(p,720027,stack);while(p.Inventory.Count<(stack==1?32:31))Pack(p,1000000,1);OpenGift(p,scroll);
            Check(!Fenced(p) && GiftReceipts(p)==0 && p.Inventory.ContainsUID(scroll.UID),"insufficient complete-scroll capacity preserves source: stack="+stack);
            DisRemove(p,p.Inventory.Objects.First(i=>i.ID==1000000));OpenGift(p,scroll);
            Check(!Fenced(p) && GiftReceipts(p)==1 && p.Inventory.Count==40 && p.Inventory.Objects.Count(i=>i.ID==1088001)==10,"exact scroll capacity includes only an actually consumed source slot");
        }
        var full=BallPlayer(town);var bag=Pack(full,723714,1);while(full.Inventory.Count<40)Pack(full,1000000,1);OpenGift(full,bag);
        Check(!Fenced(full) && GiftReceipts(full)==1 && full.Inventory.Count==39 && full.Entity.Money==800000,"money bag requires no extra inventory space");
        var pending=BallPlayer(town);pending.Entity.Money=100;EntityTable.UpdateMoney(pending);pending.Entity.AdjustMoney(25);bag=Pack(pending,723714,1);OpenGift(pending,bag);EntityTable.SaveEntity(pending);pending=ReloadBall(pending);
        Check(!Fenced(pending) && pending.Entity.Money==800125 && GiftReceipts(pending)==1,"opening preserves pending owner silver gains and advances the autosave preimage");
        var nullable=BallPlayer(town);Query("UPDATE entities SET money=NULL WHERE uid=@uid","uid",(long)nullable.Entity.UID);nullable=ReloadBall(nullable);bag=Pack(nullable,723714,1);OpenGift(nullable,bag);
        Check(!Fenced(nullable) && GiftReceipts(nullable)==1 && nullable.Entity.Money==800000,"legacy nullable silver retains loader zero semantics");
        var overflow=BallPlayer(town);overflow.Entity.Money=(ulong)long.MaxValue-799999;EntityTable.UpdateMoney(overflow);bag=Pack(overflow,723714,1);OpenGift(overflow,bag);
        Check(!Fenced(overflow) && GiftReceipts(overflow)==0 && overflow.Inventory.ContainsUID(bag.UID),"database silver overflow preserves bag and balance");
        foreach(string fault in new[]{"delete","stack","late-item","balance","receipt-scroll","receipt-bag"}){
            bool silver=fault=="balance" || fault=="receipt-bag";var p=BallPlayer(town);var source=Pack(p,silver?723714u:720027u,(ushort)(fault=="stack"?2:1));
            string table=fault=="balance"?"entities":fault.StartsWith("receipt-")?"revora_quest_gift_openings":"items";
            string verb=fault=="delete"?"DELETE":fault=="stack" || fault=="balance"?"UPDATE":"INSERT";
            string body=fault=="late-item"?"IF NEW.id=1088001 AND (SELECT count(*) FROM items WHERE entityid=NEW.entityid AND id=1088001)>=5 THEN RETURN NULL; END IF; RETURN NEW;":"RETURN NULL;";
            Query("CREATE FUNCTION suppress_quest_gift() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN "+body+" END';CREATE TRIGGER suppress_quest_gift BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_quest_gift()");
            OpenGift(p,source);
            Check(Fenced(p) && GiftReceipts(p)==0 && p.Entity.Money==0 && p.Inventory.Count==1 && p.Inventory.ContainsUID(source.UID)
                && SavedRune(source,"stacksize")==source.StackSize && Scalar("SELECT count(*) FROM items WHERE entityid=@uid",p.Entity.UID)==1
                && Scalar("SELECT money FROM entities WHERE uid=@uid",p.Entity.UID)==0,"suppressed "+fault+" rolls back source, all ten items/silver and receipt");
            Query("DROP TRIGGER suppress_quest_gift ON "+table+";DROP FUNCTION suppress_quest_gift()");p=ReloadBall(p);source=p.Inventory.Objects.Single();OpenGift(p,source);
            Check(!Fenced(p) && GiftReceipts(p)==1 && (silver?p.Entity.Money==800000:p.Inventory.Objects.Count(i=>i.ID==1088001)==10),"production reload safely retries the preserved gift after "+fault);
        }
        foreach(string guard in new[]{"dead","fake","trade","locked","suspicious","wardrobe","position","warehouse","expired","substitute","stale-bag","stale-source","stale-silver","replaced"}){
            var p=BallPlayer(town);var source=Pack(p,guard=="stale-silver"?723714u:720027u,1);
            if(guard=="dead")p.Entity.Hitpoints=0;if(guard=="fake")p.Fake=true;if(guard=="trade")p.Trade.InTrade=true;if(guard=="locked")source.Lock=1;
            if(guard=="suspicious")source.Suspicious=true;if(guard=="wardrobe")source.InWardrobe=true;if(guard=="position")source.Position=4;if(guard=="warehouse")source.Warehouse=8;
            if(guard=="expired"){source.Days=1;source.DayStamp=giftNow.ToLocalTime().AddDays(-2);}
            if(guard=="stale-bag"){var extra=Pack(p,1000000,1);Query("DELETE FROM items WHERE uid=@uid","uid",(long)extra.UID);}
            if(guard=="stale-source")Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)source.UID);
            if(guard=="stale-silver")Query("UPDATE entities SET money=100 WHERE uid=@uid","uid",(long)p.Entity.UID);
            if(guard=="replaced")ReloadBall(p);
            if(guard=="substitute")ItemHandler.UseItem(new ConquerItem(true){ID=source.ID,UID=source.UID},p);else OpenGift(p,source);
            Check(GiftReceipts(p)==0 && p.Entity.Money==0 && p.Inventory.ContainsUID(source.UID),"unusable gift preserves its exact source and reward: "+guard);
            if(guard.StartsWith("stale-"))Check(Fenced(p),"stale "+guard+" requires production reload");
        }
        foreach(uint type in new uint[]{720027,723714}){
            var p=BallPlayer(town);var source=Pack(p,type,1);
            foreach(string bad in new[]{"short","long","length","type","seal"}){
                byte[] frame=bad=="short"?new byte[31]:bad=="long"?new byte[129]:PrayerPacket(source.UID);
                Writer.WriteUInt16((ushort)(frame.Length-8),0,frame);Writer.WriteUInt16(1009,2,frame);Writer.WriteUInt32(source.UID,8,frame);Writer.WriteUInt16(4,20,frame);
                Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);
                if(bad=="length")Writer.WriteUInt16(1,0,frame);if(bad=="type")Writer.WriteUInt16(1008,2,frame);if(bad=="seal")frame[frame.Length-1]^=1;
                ItemUsage.Process(frame,p);Check(!Fenced(p) && GiftReceipts(p)==0 && p.Inventory.ContainsUID(source.UID),"bounded gift use rejects "+bad+" frame: "+type);
            }
            OpenGift(p,source);Check(!Fenced(p) && GiftReceipts(p)==1,"normal gift use works after malformed frames: "+type);
        }
        var timed=BallPlayer(town);var valid=Pack(timed,720027,1);valid.Days=1;valid.DayStamp=giftNow.ToLocalTime().AddMinutes(-1);
        Query("UPDATE items SET days='1',daystamp=@stamp WHERE uid=@uid","stamp",valid.DayStamp.ToBinary(),"uid",(long)valid.UID);OpenGift(timed,valid);
        Check(!Fenced(timed) && GiftReceipts(timed)==1,"unexpired timed source keeps its exact expiry preimage and may be opened");
        VerifyPenitenceAmulets(town);
        VerifyLotteryExchange(runtime);
    }
    static void LotteryExchange(GameState p){p.ActiveNpc=923;Npcs.GetDialog(new NpcRequest(true){NpcID=923,OptionID=2},p);}
    static void VerifyLotteryExchange(string runtime){
        ushort mapId;
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT mapid FROM public.npcs WHERE id=923",c)){
            var value=q.ExecuteScalar();if(value==null)throw new Exception("Lady Luck placement missing");mapId=Convert.ToUInt16(value);}}
        Check(mapId==1002 || mapId==1036,"installed Lady Luck is in an admitted public town");
        var market=World(runtime,mapId);MiningNpc(market,923);
        System.Console.WriteLine("Lady Luck fixture uses installed map="+mapId+" x="+market.Npcs[923].X+" y="+market.Npcs[923].Y);
        foreach(bool bound in new[]{false,true})foreach(ushort stack in new ushort[]{0,1,3}){
            var p=BallPlayer(market);AdventureNear(p,923);var source=NestedPack(p,710212,stack,bound);
            ulong money=p.Entity.Money;uint cps=p.Entity.ConquerPoints;
            Parallel.For(0,12,n=>LotteryExchange(p));
            Check(!Fenced(p) && GiftReceipts(p)==1 && p.Account.State==AccountTable.AccountState.Player
                && p.Inventory.Objects.Count(i=>i.ID==711504)==3 && p.Inventory.Objects.Where(i=>i.ID==711504).All(i=>i.Bound==bound && i.StackSize==1)
                && (stack>1?source.StackSize==stack-1:!p.Inventory.ContainsUID(source.UID)) && p.Entity.Money==money && p.Entity.ConquerPoints==cps,
                "ordinary Lady Luck exchange grants three tickets once with original binding: "+bound+"/"+stack);
            EntityTable.SaveEntity(p);p=ReloadBall(p);AdventureNear(p,923);
            Check(!Fenced(p) && GiftReceipts(p)==1 && p.Inventory.Objects.Count(i=>i.ID==711504 && i.Bound==bound)==3,
                "production reload preserves paid ticket exchange and receipt");
            if(stack>1){giftNow=giftNow.AddSeconds(1);LotteryExchange(p);
                Check(!Fenced(p) && GiftReceipts(p)==2 && p.Inventory.Objects.Count(i=>i.ID==711504)==6,"next intentional exchange consumes one remaining old ticket");}
            else{LotteryExchange(p);Check(GiftReceipts(p)==1,"spent ticket cannot exchange again after reconnect");}
        }
        var multiple=BallPlayer(market);AdventureNear(multiple,923);Pack(multiple,710212,1);Pack(multiple,710212,1);
        Parallel.For(0,12,n=>LotteryExchange(multiple));
        Check(!Fenced(multiple) && GiftReceipts(multiple)==1 && multiple.Inventory.Objects.Count(i=>i.ID==710212)==1,
            "duplicate NPC requests cannot consume the next separate ticket");
        Call(QuestGifts,null,"Initialize");
        Check(GiftReceipts(multiple)==1,"repeat schema initialization preserves completed exchange receipts");
        foreach(string guard in new[]{"remote","no-npc","direct-use","locked","suspicious","wardrobe","warehouse","expired","dead","trade"}){
            var p=BallPlayer(market);AdventureNear(p,923);var source=Pack(p,710212,1);
            if(guard=="remote"){p.Entity.X=10;p.Entity.Y=10;}if(guard=="no-npc")p.ActiveNpc=924;
            if(guard=="locked")source.Lock=1;if(guard=="suspicious")source.Suspicious=true;if(guard=="wardrobe")source.InWardrobe=true;
            if(guard=="warehouse")source.Warehouse=8;if(guard=="expired"){source.Days=1;source.DayStamp=giftNow.ToLocalTime().AddDays(-2);}
            if(guard=="dead")p.Entity.Hitpoints=0;if(guard=="trade")p.Trade.InTrade=true;
            if(guard=="direct-use"){OpenGift(p,source);ItemHandler.UseItem(source,p);}
            else if(guard=="no-npc")Call(QuestGifts,null,"ExchangeLottery",p);else LotteryExchange(p);
            Check(GiftReceipts(p)==0 && p.Inventory.ContainsUID(source.UID) && p.Inventory.Objects.All(i=>i.ID!=711504),"ticket exchange rejects "+guard);
        }
        foreach(ushort stack in new ushort[]{1,2}){
            var p=BallPlayer(market);AdventureNear(p,923);var source=Pack(p,710212,stack);
            while(p.Inventory.Count<(stack==1?39:38))Pack(p,1000000,1);LotteryExchange(p);
            Check(!Fenced(p) && GiftReceipts(p)==0 && p.Inventory.ContainsUID(source.UID),"full reward capacity is required before ticket exchange");
            DisRemove(p,p.Inventory.Objects.First(i=>i.ID==1000000));LotteryExchange(p);
            Check(!Fenced(p) && GiftReceipts(p)==1 && p.Inventory.Count==40,"exact ticket exchange capacity credits only a removed source slot");
        }
        foreach(string fault in new[]{"source","stack","third-ticket","receipt","stale-source"}){
            var p=BallPlayer(market);AdventureNear(p,923);var source=Pack(p,710212,(ushort)(fault=="stack"?2:1));
            string table=fault=="receipt"?"revora_quest_gift_openings":"items";
            string verb=fault=="source"?"DELETE":fault=="stack"?"UPDATE":"INSERT";
            if(fault=="stale-source")Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)source.UID);
            else{string body=fault=="third-ticket"?"IF NEW.id=711504 AND (SELECT count(*) FROM items WHERE entityid=NEW.entityid AND id=711504)=2 THEN RETURN NULL; END IF; RETURN NEW;":"RETURN NULL;";
                Query("CREATE FUNCTION suppress_ticket_exchange() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN "+body+" END';CREATE TRIGGER suppress_ticket_exchange BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_ticket_exchange()");}
            LotteryExchange(p);
            Check(Fenced(p) && GiftReceipts(p)==0 && p.Inventory.ContainsUID(source.UID)
                && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id=711504",p.Entity.UID)==0,"failed ticket exchange rolls back all output: "+fault);
            if(fault!="stale-source")Query("DROP TRIGGER suppress_ticket_exchange ON "+table+";DROP FUNCTION suppress_ticket_exchange()");
            p=ReloadBall(p);AdventureNear(p,923);LotteryExchange(p);
            Check(!Fenced(p) && GiftReceipts(p)==1 && p.Inventory.Objects.Count(i=>i.ID==711504)==3,"reloaded ticket exchange retries preserved source: "+fault);
        }
    }
    static readonly Type Draws=typeof(Entity).Assembly.GetType("Revora.Game.Features.LotteryDraws");
    static byte[] LotteryFrame(ushort action){var frame=new byte[34];Writer.WriteUInt16(26,0,frame);Writer.WriteUInt16(1314,2,frame);
        Writer.WriteUInt16(action,4,frame);Writer.WriteUInt64(PacketHandler.ClientSeal,26,frame);return frame;}
    static void LotteryAction(GameState p,ushort action){PacketHandler.HandlePacket(LotteryFrame(action),p);}
    static long DrawCount(GameState p){return Scalar("SELECT count(*) FROM revora_lottery_draws WHERE uid=@uid",p.Entity.UID);}
    static void OpenBox(GameState p,uint box){p.ActiveNpc=box;Npcs.GetDialog(new NpcRequest(true){NpcID=box,OptionID=1},p);}
    static void PrepareLotteryFixture(){
        Query("CREATE TABLE IF NOT EXISTS lottery AS TABLE public.lottery");
        LotteryTable.LotteryItems.Clear();LotteryTable.Load();
        System.Console.WriteLine("Lottery audited rows: normal="+LotteryTable.LotteryItems.Count);
        foreach(var row in LotteryTable.LotteryItems.Where(x=>x.Rank<=0 || x.Rank>=35 || x.Chance<=0 || x.Plus>12 || x.Sockets>2 || !ConquerItemInformation.BaseInformations.ContainsKey(x.ID)))
            System.Console.WriteLine("Invalid normal lottery row: id="+row.ID+" rank="+row.Rank+" chance="+row.Chance+" plus="+row.Plus+" sockets="+row.Sockets);
        Call(Draws,null,"Initialize");Call(Draws,null,"Initialize");
        Draws.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>giftNow));Draws.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>0));
    }
    static void VerifyLotteryDraws(string runtime,Map town){
        PrepareLotteryFixture();
        var room=World(runtime,700);foreach(uint box in new uint[]{925,926,927,928,929})MiningNpc(room,box);
        foreach(bool bound in new[]{false,true}){
            var p=BallPlayer(town);MiningNpc(town,923);AdventureNear(p,923);Pack(p,710212,1).Bound=bound;
            var old=p.Inventory.Objects.Single(i=>i.ID==710212);Query("UPDATE items SET bound=@bound WHERE uid=@uid","bound",bound?1:0,"uid",(long)old.UID);
            LotteryExchange(p);p.ActiveNpc=923;Npcs.GetDialog(new NpcRequest(true){NpcID=923,OptionID=1},p);AdventureNear(p,925);
            Parallel.For(0,12,n=>OpenBox(p,925));
            Check(!Fenced(p) && DrawCount(p)==1 && p.Inventory.Objects.All(i=>i.ID!=711504)
                && Scalar("SELECT count(*) FROM revora_lottery_steps s JOIN revora_lottery_draws d ON d.id=s.draw_id WHERE d.uid=@uid",p.Entity.UID)==1,
                "ordinary exchange and Lady Luck travel fund one durable draw despite concurrent repeats");
            p=ReloadBall(p);AdventureNear(p,925);OpenBox(p,925);
            Check(!Fenced(p) && DrawCount(p)==1,"reconnect restores the paid result without requiring more tickets");
            Check(!HasAchievement(p,10118),"a paid lottery preview is not a received treasure achievement");
            LotteryAction(p,0);
            Check(HasAchievement(p,10118),"committed lottery claim immediately projects its earned achievement");
            LotteryAction(p,0);LotteryAction(p,0);EntityTable.SaveEntity(p);p=ReloadBall(p);new TitleStorage().Login(p);
            uint won=LotteryTable.LotteryItems[0].ID;
            Check(!Fenced(p) && p.Inventory.Objects.Count(i=>i.ID==won && i.Bound==bound)==1
                && Scalar("SELECT count(*) FROM revora_lottery_draws WHERE uid=@uid AND claimed AND item_uid>0",p.Entity.UID)==1 && HasAchievement(p,10118),
                "claim delivers exactly one saved prize with source binding after production reload");
        }
        VerifyLotteryRecovery(room);
        VerifyLotteryPools(runtime,room);
        var wireType=typeof(Entity).Assembly.GetType("Revora.Network.GamePackets.Lottery");var wire=Activator.CreateInstance(wireType,true);
        foreach(var pair in new[]{Tuple.Create("Plus",8),Tuple.Create("SocketTwo",255),Tuple.Create("SocketOne",255),Tuple.Create("AddJadeChances",2)})
            wireType.GetProperty(pair.Item1).SetValue(wire,(byte)pair.Item2,null);
        wireType.GetProperty("Color").SetValue(wire,Enums.Color.Blue,null);
        var bytes=(byte[])wireType.GetMethod("ToArray").Invoke(wire,null);
        Check(bytes[7]==255 && bytes[8]==255 && bytes[9]==8 && bytes[10]==(byte)Enums.Color.Blue && bytes[11]==2,
            "lottery response fields do not overwrite adjacent plus, sockets or rerolls");
    }
    static GameState DrawPlayer(Map room,uint box=925){var p=BallPlayer(room);p.Entity.X=42;p.Entity.Y=50;AdventureNear(p,box);return p;}
    static void VerifyLotteryRecovery(Map room){
        var p=DrawPlayer(room);var tickets=NestedPack(p,711504,5,false);OpenBox(p,925);
        Check(!Fenced(p) && tickets.StackSize==2,"initial lottery payment consumes exactly three units of a stack");
        giftNow=giftNow.AddSeconds(1);var bound=NestedPack(p,711504,1,true);
        // Force the bound ticket into the next payment without changing its identity.
        tickets.Lock=1;LotteryAction(p,1);tickets.Lock=0;
        Check(!Fenced(p) && !p.Inventory.ContainsUID(bound.UID) && tickets.StackSize==2
            && Scalar("SELECT step FROM revora_lottery_draws WHERE uid=@uid AND NOT claimed",p.Entity.UID)==1
            && Scalar("SELECT count(*) FROM revora_lottery_draws WHERE uid=@uid AND bound",p.Entity.UID)==1,
            "bound reroll payment binds the pending prize without consuming locked tickets");
        giftNow=giftNow.AddSeconds(1);Parallel.For(0,12,n=>LotteryAction(p,1));giftNow=giftNow.AddSeconds(1);LotteryAction(p,1);
        Check(!Fenced(p) && tickets.StackSize==1 && Scalar("SELECT step FROM revora_lottery_draws WHERE uid=@uid AND NOT claimed",p.Entity.UID)==2,
            "two-reroll cap and concurrent-request debounce preserve the remaining ticket");
        p=ReloadBall(p);AdventureNear(p,925);OpenBox(p,925);LotteryAction(p,0);p=ReloadBall(p);
        Check(p.Inventory.Objects.Any(i=>i.ID==LotteryTable.LotteryItems[0].ID && i.Bound),"later unbound reroll cannot remove binding from a recovered prize");
        foreach(string fault in new[]{"debit","draw","step","reroll","claim-item","claim-receipt","claim-achievement"}){
            var f=DrawPlayer(room);NestedPack(f,711504,5,false);bool claim=fault.StartsWith("claim-");bool reroll=fault=="reroll";
            if(claim || reroll){OpenBox(f,925);giftNow=giftNow.AddSeconds(1);}
            string table=fault=="claim-achievement"?"revora_achievement_evidence":fault=="debit" || fault=="claim-item"?"items":fault=="step"?"revora_lottery_steps":"revora_lottery_draws";
            string verb=fault=="debit" || fault=="reroll" || fault=="claim-receipt"?"UPDATE":"INSERT";
            Query("CREATE FUNCTION suppress_lottery_write() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_lottery_write BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_lottery_write()");
            if(claim)LotteryAction(f,0);else if(reroll)LotteryAction(f,1);else OpenBox(f,925);
            Check(Fenced(f) && Scalar("SELECT count(*) FROM revora_lottery_draws WHERE uid=@uid AND claimed",f.Entity.UID)==0
                && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10118",f.Entity.UID)==0
                && Scalar("SELECT COALESCE(sum(stacksize),0) FROM items WHERE entityid=@uid AND id=711504",f.Entity.UID)==(claim || reroll?2:5)
                && Scalar("SELECT count(*) FROM revora_lottery_draws WHERE uid=@uid AND NOT claimed AND step=0",f.Entity.UID)==(claim || reroll?1:0),
                "suppressed "+fault+" preserves payment, prior result and unclaimed ownership");
            Query("DROP TRIGGER suppress_lottery_write ON "+table+";DROP FUNCTION suppress_lottery_write()");
            f=ReloadBall(f);AdventureNear(f,925);OpenBox(f,925);if(reroll)LotteryAction(f,1);LotteryAction(f,0);f=ReloadBall(f);new TitleStorage().Login(f);
            Check(!Fenced(f) && Scalar("SELECT count(*) FROM revora_lottery_draws WHERE uid=@uid AND claimed",f.Entity.UID)==1 && HasAchievement(f,10118),
                "reconnect retries preserved lottery state after "+fault);
        }
        var full=DrawPlayer(room);NestedPack(full,711504,3,false);OpenBox(full,925);while(full.Inventory.Count<40)Pack(full,1000000,1);LotteryAction(full,0);
        Check(!Fenced(full) && Scalar("SELECT count(*) FROM revora_lottery_draws WHERE uid=@uid AND NOT claimed",full.Entity.UID)==1,"full inventory preserves an already paid lottery prize");
        DisRemove(full,full.Inventory.Objects.First());LotteryAction(full,0);Check(full.Inventory.Count==40 && Scalar("SELECT count(*) FROM revora_lottery_draws WHERE uid=@uid AND claimed",full.Entity.UID)==1,"freeing one slot claims the saved prize without another payment");
        foreach(string bad in new[]{"short","long","length","seal","id","action","remote","fake","trade","dead","stale-source","stale-bag"}){
            var f=DrawPlayer(room);var source=NestedPack(f,711504,3,false);f.ActiveNpc=925;var frame=LotteryFrame(2);
            if(bad=="short")Array.Resize(ref frame,13);if(bad=="long")Array.Resize(ref frame,129);
            if(bad=="length")Writer.WriteUInt16(1,0,frame);if(bad=="seal")frame[33]^=1;if(bad=="id")Writer.WriteUInt16(1315,2,frame);if(bad=="action")Writer.WriteUInt16(259,4,frame);
            if(bad=="remote"){f.Entity.X=1;f.Entity.Y=1;}if(bad=="fake")f.Fake=true;if(bad=="trade")f.Trade.InTrade=true;if(bad=="dead")f.Entity.Hitpoints=0;
            if(bad=="stale-source")Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)source.UID);
            if(bad=="stale-bag")Query("DELETE FROM items WHERE uid=@uid","uid",(long)source.UID);
            Revora.Game.Lottery.Process(frame,f);
            Check(DrawCount(f)==0 && f.Inventory.ContainsUID(source.UID),"lottery rejects "+bad+" without creating or charging a draw");
        }
        var exhausted=DrawPlayer(room);NestedPack(exhausted,711504,3,false);
        Draws.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>max));OpenBox(exhausted,925);
        Check(Fenced(exhausted) && DrawCount(exhausted)==0,"out-of-range random output never charges a ticket");
        int rejectIndex=LotteryTable.LotteryItems.FindIndex(x=>x.Chance>x.Rank),drawCalls=0;
        Check(rejectIndex>=0,"configured lottery contains a row with a testable rejection branch");
        var limited=DrawPlayer(room);NestedPack(limited,711504,3,false);
        Draws.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>drawCalls++%2==0?rejectIndex:max-1));OpenBox(limited,925);
        Check(Fenced(limited) && DrawCount(limited)==0 && drawCalls==8192 && limited.Inventory.Objects.Single().StackSize==3,
            "selection stops after 4096 rejected candidates without consuming tickets");
        foreach(string invalid in new[]{"rank-zero","rank-high","chance","plus","sockets","catalog"}){
            var row=LotteryTable.LotteryItems[0];if(invalid=="rank-zero")row.Rank=0;if(invalid=="rank-high")row.Rank=35;if(invalid=="chance")row.Chance=0;
            if(invalid=="plus")row.Plus=13;if(invalid=="sockets")row.Sockets=3;if(invalid=="catalog")row.ID=uint.MaxValue;
            bool rejected=false;try{Call(Draws,null,"ValidatePool",(object)new[]{row});}catch(InvalidOperationException){rejected=true;}
            Check(rejected,"invalid lottery configuration fails before accepting payment: "+invalid);
        }
        var unpaid=DrawPlayer(room);unpaid.ActiveNpc=925;unpaid.Entity.LotteryItemID=1088000;
        unpaid.Entity.LotteryPrize=new ConquerItem(true){ID=1088000};LotteryAction(unpaid,0);
        Check(DrawCount(unpaid)==0 && unpaid.Inventory.Count==0,"volatile legacy prize fields cannot authorize a new lottery claim");
        Draws.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>0));
        var compact=DrawPlayer(room);NestedPack(compact,711504,3,false);compact.ActiveNpc=925;
        var compactFrame=new byte[14];Writer.WriteUInt16(6,0,compactFrame);Writer.WriteUInt16(1314,2,compactFrame);
        Writer.WriteUInt16(2,4,compactFrame);Writer.WriteUInt64(PacketHandler.ClientSeal,6,compactFrame);PacketHandler.HandlePacket(compactFrame,compact);
        Check(!Fenced(compact) && DrawCount(compact)==1,"request admission does not require the longer outgoing prize-display layout");
    }
    static void VerifyLotteryPools(string runtime,Map room){
        var p=DrawPlayer(room);var normal=LotteryTable.LotteryItems.ToArray();
        for(int index=0;index<normal.Length;index++){
            var expected=normal[index];int selected=index,call=0;Draws.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>call++==0?selected:0));
            giftNow=giftNow.AddSeconds(1);NestedPack(p,711504,3,false);OpenBox(p,925);LotteryAction(p,0);
            var prize=p.Inventory.Objects.Single();
            Check(!Fenced(p) && prize.ID==expected.ID && prize.Plus==expected.Plus && prize.SocketOne==(expected.Sockets>0?Enums.Gem.EmptySocket:Enums.Gem.NoSocket)
                && prize.SocketTwo==(expected.Sockets>1?Enums.Gem.EmptySocket:Enums.Gem.NoSocket),"configured normal lottery outcome retains exact metadata: "+index);
            EntityTable.SaveEntity(p);p=ReloadBall(p);AdventureNear(p,925);prize=p.Inventory.Objects.Single();
            Check(prize.ID==expected.ID && prize.Plus==expected.Plus && (byte)prize.SocketTwo==(expected.Sockets>1?255:0),"configured outcome survives item reload: "+index);DisRemove(p,prize);
        }
        Check(Scalar("SELECT count(*) FROM public.npcs WHERE id BETWEEN 900020 AND 900025 AND mapid IN(1002,701)",0)==0
            && Enumerable.Range(900020,6).All(id=>Revora.Game.Features.TwinCityPlaza.IsClosed((uint)id)),
            "Anniversary boxes remain absent and explicitly closed; normal lottery does not reopen their campaign");
        Draws.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>0));
    }
    static readonly Type Amulets = typeof(Entity).Assembly.GetType("Revora.Game.Features.PenitenceAmulets");
    static long AmuletUses(GameState p) { return Scalar("SELECT count(*) FROM revora_penitence_uses WHERE uid=@uid",p.Entity.UID); }
    static GameState AmuletPlayer(Map map, ushort pk=120) {
        var p=BallPlayer(map); p.Entity.PKPoints=pk;p.Entity.VIPLevel=0;
        if(!EntityTable.SaveEntity(p))throw new Exception("Amulet fixture PK save failed");return p;
    }
    static void VerifyPenitenceAmulets(Map town) {
        Call(Amulets,null,"Initialize");Call(Amulets,null,"Initialize");
        Amulets.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>giftNow));
        foreach(uint type in new uint[]{720128,723727})foreach(bool bound in new[]{false,true})foreach(ushort stack in new ushort[]{0,1,3}) {
            var p=AmuletPlayer(town); var item=NestedPack(p,type,stack,bound);
            Parallel.For(0,16,n=>{if(n%3==0)EntityTable.SaveEntity(p);else OpenGift(p,item);});
            Check(!Fenced(p) && p.Account.State==AccountTable.AccountState.Player && p.Entity.PKPoints==90 && AmuletUses(p)==1
                && (stack>1?item.StackSize==2:!p.Inventory.ContainsUID(item.UID)),"ordinary concurrent amulet use consumes one unit and reduces exactly thirty PK: "+type+"/"+bound+"/"+stack);
            p=ReloadBall(p);var alternate=Pack(p,type==720128?723727u:720128u,1);OpenGift(p,alternate);
            Check(!Fenced(p) && p.Entity.PKPoints==90 && AmuletUses(p)==1 && p.Inventory.ContainsUID(alternate.UID),"alias and reconnect share durable amulet cooldown");
            // Revora login grants minimum VIP4. Explicitly exercise a lower
            // current tier to verify the catalog cooldown independently.
            p.Entity.VIPLevel=0;
            giftNow=giftNow.AddSeconds(3599);OpenGift(p,alternate);Check(AmuletUses(p)==1,"amulet remains unavailable one second before the hour");
            giftNow=giftNow.AddSeconds(1);OpenGift(p,alternate);Check(p.Entity.PKPoints==60 && AmuletUses(p)==2,"amulet works at the exact one-hour boundary");
        }
        foreach(ushort pk in new ushort[]{0,29,30,99,100}) {
            var p=AmuletPlayer(town,pk);var item=Pack(p,720128,1);OpenGift(p,item);
            Check(!Fenced(p) && p.Entity.PKPoints==(pk<30?pk:pk-30) && AmuletUses(p)==(pk<30?0:1)
                && p.Inventory.ContainsUID(item.UID)==(pk<30),"amulet admission and exact reduction at PK boundary "+pk);
        }
        var vip=AmuletPlayer(town);vip.Entity.VIPLevel=3;var vipItem=Pack(vip,720128,3);OpenGift(vip,vipItem);OpenGift(vip,vipItem);
        Check(AmuletUses(vip)==1,"VIP exemption retains bounded duplicate-request protection");giftNow=giftNow.AddSeconds(1);OpenGift(vip,vipItem);
        Check(vip.Entity.PKPoints==60 && AmuletUses(vip)==2,"VIP three can intentionally reuse after request debounce");
        vip.Entity.VIPLevel=2;giftNow=giftNow.AddSeconds(1);OpenGift(vip,vipItem);Check(AmuletUses(vip)==2,"VIP expiry reinstates ordinary cooldown from latest successful use");
        giftNow=giftNow.AddHours(-2);OpenGift(vip,vipItem);Check(AmuletUses(vip)==2,"backward clock cannot reset amulet cooldown");giftNow=giftNow.AddHours(3);
        Map deity; if(!Kernel.Maps.TryGetValue(10250,out deity))deity=new Map(10250,10250,"fixture",false);
        var hunter=AmuletPlayer(deity);var deityItem=Pack(hunter,720128,3);OpenGift(hunter,deityItem);
        giftNow=giftNow.AddSeconds(1799);OpenGift(hunter,deityItem);Check(AmuletUses(hunter)==1,"Deityland cooldown remains active before thirty minutes");
        giftNow=giftNow.AddSeconds(1);OpenGift(hunter,deityItem);Check(hunter.Entity.PKPoints==60 && AmuletUses(hunter)==2,"installed Deityland base map selects the thirty-minute cooldown");
        hunter.Entity.VIPLevel=3;giftNow=giftNow.AddSeconds(1);OpenGift(hunter,deityItem);
        Check(hunter.Entity.PKPoints==30 && AmuletUses(hunter)==3,"documented VIP exemption interpretation also applies in Deityland");
        var full=AmuletPlayer(town);var fullItem=Pack(full,720128,1);while(full.Inventory.Count<40)Pack(full,1000000,1);OpenGift(full,fullItem);
        Check(full.Inventory.Count==39 && full.Entity.PKPoints==90 && AmuletUses(full)==1,"full bag permits amulet consumption without needing an output slot");
        var timed=AmuletPlayer(town);var timedItem=Pack(timed,723727,1);timedItem.Days=1;timedItem.DayStamp=giftNow.ToLocalTime().AddMinutes(-1);
        Query("UPDATE items SET days='1',daystamp=@stamp WHERE uid=@uid","stamp",timedItem.DayStamp.ToBinary(),"uid",(long)timedItem.UID);OpenGift(timed,timedItem);
        Check(!Fenced(timed) && timed.Entity.PKPoints==90 && AmuletUses(timed)==1,"unexpired timed amulet retains exact expiry ownership checks");
        foreach(string fault in new[]{"delete","stack","balance","cooldown","receipt"}) {
            var p=AmuletPlayer(town);var item=Pack(p,720128,(ushort)(fault=="stack"?2:1));
            string table=fault=="balance"?"entities":fault=="cooldown"?"revora_penitence_cooldowns":fault=="receipt"?"revora_penitence_uses":"items";
            string verb=fault=="delete"?"DELETE":fault=="stack" || fault=="balance"?"UPDATE":"INSERT";
            Query("CREATE FUNCTION suppress_amulet() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_amulet BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_amulet()");
            try { OpenGift(p,item); } finally { Query("DROP TRIGGER suppress_amulet ON "+table+";DROP FUNCTION suppress_amulet()"); }
            Check(Fenced(p) && p.Entity.PKPoints==120 && AmuletUses(p)==0 && p.Inventory.ContainsUID(item.UID)
                && SavedRune(item,"stacksize")==item.StackSize && Scalar("SELECT pkpoints FROM entities WHERE uid=@uid",p.Entity.UID)==120
                && Scalar("SELECT count(*) FROM revora_penitence_cooldowns WHERE uid=@uid",p.Entity.UID)==0,"amulet "+fault+" failure rolls back source, PK, cooldown and receipt");
            p=ReloadBall(p);item=p.Inventory.Objects.Single();OpenGift(p,item);Check(!Fenced(p) && p.Entity.PKPoints==90 && AmuletUses(p)==1,"reload retries preserved amulet after "+fault);
        }
        foreach(string guard in new[]{"dead","fake","trade","locked","suspicious","wardrobe","position","warehouse","expired","stale-pk","stale-item"}) {
            var p=AmuletPlayer(town);var item=Pack(p,723727,1);
            if(guard=="dead")p.Entity.Hitpoints=0;if(guard=="fake")p.Fake=true;if(guard=="trade")p.Trade.InTrade=true;
            if(guard=="locked")item.Lock=1;if(guard=="suspicious")item.Suspicious=true;if(guard=="wardrobe")item.InWardrobe=true;
            if(guard=="position")item.Position=4;if(guard=="warehouse")item.Warehouse=8;
            if(guard=="expired"){item.Days=1;item.DayStamp=giftNow.ToLocalTime().AddDays(-2);}
            if(guard=="stale-pk")Query("UPDATE entities SET pkpoints=130 WHERE uid=@uid","uid",(long)p.Entity.UID);
            if(guard=="stale-item")Query("UPDATE items SET stacksize=2 WHERE uid=@uid","uid",(long)item.UID);
            OpenGift(p,item);Check(AmuletUses(p)==0 && p.Entity.PKPoints==120 && p.Inventory.ContainsUID(item.UID),"amulet rejects "+guard+" without consuming or granting");
        }
        var malformed=AmuletPlayer(town);var source=Pack(malformed,720128,1);
        foreach(string bad in new[]{"length","seal","action","short"}) {
            var frame=PrayerPacket(source.UID);
            if(bad=="length")Writer.WriteUInt16(1,0,frame);if(bad=="seal")frame[frame.Length-1]^=1;
            if(bad=="action")Writer.WriteUInt16(260,20,frame);if(bad=="short")Array.Resize(ref frame,22);
            ItemUsage.Process(frame,malformed);Check(AmuletUses(malformed)==0 && malformed.Inventory.ContainsUID(source.UID),"amulet rejects malformed "+bad+" use frame");
        }
    }
}
