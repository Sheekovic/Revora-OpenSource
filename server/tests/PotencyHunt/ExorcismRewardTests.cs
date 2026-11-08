using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Revora.Network;

internal static partial class PotencyHuntTests
{
    static readonly Type ExorcismGifts=typeof(Entity).Assembly.GetType("Revora.Game.Features.ExorcismRewards");
    static DateTime exorcismNow=DateTime.UtcNow;
    static long ExorcismOpenings(GameState p){return Scalar("SELECT count(*) FROM revora_exorcism_openings WHERE uid=@uid",p.Entity.UID);}
    static long ExorcismExchanges(GameState p){return Scalar("SELECT count(*) FROM revora_exorcism_exchanges WHERE uid=@uid",p.Entity.UID);}
    static void ExorcismNpc(GameState p,byte option){p.ActiveNpc=12333;Npcs.GetDialog(new NpcRequest(true){NpcID=0,OptionID=option},p);}
    static void ExorcismStand(GameState p){var n=p.Map.Npcs[12333];p.Entity.X=n.X;p.Entity.Y=n.Y;}
    static GameState ExorcismPlayer(Map map){var p=BallPlayer(map);ExorcismStand(p);EntityTable.SaveEntity(p);return p;}
    static void ExorcismRoll(int prize,int bonus){ExorcismGifts.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>max==100?bonus:prize));}
    static void VerifyExorcismRewards(string runtime,Map town) {
        VerifyFirstRebirth(runtime,town);Call(ExorcismGifts,null,"Initialize");Call(ExorcismGifts,null,"Initialize");
        var market=World(runtime,1036);MiningNpc(market,12333);
        ExorcismGifts.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>exorcismNow));
        foreach(bool gold in new[]{false,true}) {
            var pool=(uint[])ExorcismGifts.GetField(gold?"golden":"standard",Hidden).GetValue(null);
            Check(pool.Length==(gold?45:86) && pool.Any(id=>ConquerItemInformation.BaseInformations[id].PurificationLevel==3)
                && pool.Any(id=>ConquerItemInformation.BaseInformations[id].PurificationLevel==(gold?6:4)),"both pack pools contain the reviewed soul grade endpoints");
            for(int slot=0;slot<pool.Length;slot++) {
                var p=ExorcismPlayer(market);p.Entity.BoundCps=17;var pack=Pack(p,gold?729594u:720948u,1);bool bound=slot%2==0;
                pack.Bound=bound;Query("UPDATE items SET bound=@bound WHERE uid=@uid","bound",bound?1:0,"uid",(long)pack.UID);
                ExorcismRoll(slot,99);ItemHandler.UseItem(pack,p);var gift=p.Inventory.Objects.Single();
                var info=ConquerItemInformation.BaseInformations[pool[slot]];byte days=info.TimeTillRemovInMinutes==10080?(byte)7:(byte)0;
                Check(!Fenced(p) && p.Account.State==AccountTable.AccountState.Player && ExorcismOpenings(p)==1 && gift.ID==pool[slot] && gift.Bound==bound
                    && gift.Days==days && p.Entity.BoundCps==(gold?67u:17u) && !p.Inventory.ContainsUID(pack.UID),"ordinary player receives exact pool slot, lifetime, binding and purse: "+gold+"/"+slot);
                ItemHandler.UseItem(pack,p);EntityTable.SaveEntity(p);p=ReloadBall(p);gift=p.Inventory.Objects.Single();
                Check(!Fenced(p) && ExorcismOpenings(p)==1 && gift.ID==pool[slot] && gift.Bound==bound && gift.Days==days
                    && (days==0?gift.TimeLeftInMinutes==0:gift.TimeLeftInMinutes>604000 && gift.DayStamp==exorcismNow.ToLocalTime())
                    && p.Entity.BoundCps==(gold?67u:17u),"every pool slot survives production reload and spent-pack replay: "+gold+"/"+slot);
            }
        }
        foreach(int bonus in new[]{0,19,20,99}) {
            var p=ExorcismPlayer(market);var pack=Pack(p,729594,1);ExorcismRoll(0,bonus);ItemHandler.UseItem(pack,p);
            Check(!Fenced(p) && ExorcismOpenings(p)==1 && p.Entity.BoundCps==50 && p.Inventory.Contains(Config.DailyQuestChiToken,1)==(bonus<20),"extra Chi has the exact twenty-percent boundary "+bonus);
        }
        ExorcismRoll(0,0);
        foreach(bool gold in new[]{false,true}) {
            var p=ExorcismPlayer(market);var tokens=Pack(p,711211,(ushort)(gold?18:6));ExorcismNpc(p,gold?(byte)9:(byte)8);
            Check(ExorcismExchanges(p)==0 && tokens.StackSize==(gold?18:6),"unreviewed choice cannot spend tokens");
            ExorcismNpc(p,6);Parallel.For(0,20,n=>{if(n%3==0)EntityTable.SaveEntity(p);else ExorcismNpc(p,gold?(byte)9:(byte)8);});
            var pack=p.Inventory.Objects.Single(i=>i.ID==(gold?729594u:720948u));
            Check(!Fenced(p) && ExorcismExchanges(p)==1 && tokens.StackSize==(gold?9:3) && !pack.Bound,"concurrent exchange consumes one reviewed three/nine-token price");
            p=ReloadBall(p);ExorcismStand(p);ExorcismNpc(p,gold?(byte)9:(byte)8);ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.UID==pack.UID),p);
            Check(!Fenced(p) && ExorcismExchanges(p)==1 && ExorcismOpenings(p)==1 && p.Entity.BoundCps==(gold?50u:0u),"exchanged pack can be opened after reload without replaying the exchange");
        }
        var boundPlayer=ExorcismPlayer(market);var free=Pack(boundPlayer,711211,9);var boundTokens=Pack(boundPlayer,711211,3);boundTokens.Bound=true;
        Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)boundTokens.UID);ExorcismNpc(boundPlayer,6);ExorcismNpc(boundPlayer,9);
        var boundPack=boundPlayer.Inventory.Objects.Single(i=>i.ID==729594);ItemHandler.UseItem(boundPack,boundPlayer);
        Check(!Fenced(boundPlayer) && free.StackSize==3 && !boundPlayer.Inventory.ContainsUID(boundTokens.UID)
            && boundPlayer.Inventory.Objects.Where(i=>i.ID!=711211).All(i=>i.Bound),"bound-first mixed token exchange preserves binding through all pack gifts");
        var zero=ExorcismPlayer(market);for(int n=0;n<3;n++)Pack(zero,711211,0);ExorcismNpc(zero,6);ExorcismNpc(zero,8);
        Check(!Fenced(zero) && ExorcismExchanges(zero)==1 && zero.Inventory.Count==1,"three legacy zero-stack tokens count as three singletons");
        var zeroPack=Pack(zero,729594,0);ItemHandler.UseItem(zeroPack,zero);ItemHandler.UseItem(zeroPack,zero);
        Check(!Fenced(zero) && ExorcismOpenings(zero)==1 && zero.Entity.BoundCps==50,"legacy zero-stack pack opens once");
        foreach(string invalid in new[]{"cancel","expired","remote","trade","dead","fake","unloaded","replaced","price-change","wrong-npc","locked","short"}) {
            var p=ExorcismPlayer(market);var tokens=Pack(p,711211,(ushort)(invalid=="short"?2:3));ExorcismNpc(p,6);
            if(invalid=="cancel")ExorcismNpc(p,255);if(invalid=="expired")exorcismNow=exorcismNow.AddMinutes(2);
            if(invalid=="remote")p.Entity.X=1;if(invalid=="trade")p.Trade.InTrade=true;if(invalid=="dead")p.Entity.Hitpoints=0;
            if(invalid=="fake")p.Fake=true;if(invalid=="unloaded")p.Entity.FullyLoaded=false;if(invalid=="replaced")Kernel.GamePool[p.Entity.UID]=new GameState(null);
            if(invalid=="locked")tokens.Lock=1;if(invalid=="price-change")Config.DailyExorcismPackTokens++;
            try {if(invalid=="wrong-npc")Npcs.GetDialog(new NpcRequest(true){NpcID=1,OptionID=8},p);else ExorcismNpc(p,8);}
            finally{if(invalid=="price-change")Config.DailyExorcismPackTokens--;if(invalid=="expired")exorcismNow=exorcismNow.AddMinutes(-2);}
            Check(!Fenced(p) && ExorcismExchanges(p)==0 && p.Inventory.ContainsUID(tokens.UID),"invalid exchange leaves tokens untouched: "+invalid);
        }
        foreach(bool gold in new[]{false,true})foreach(ushort stack in new ushort[]{1,2}) {
            var p=ExorcismPlayer(market);var pack=Pack(p,gold?729594u:720948u,stack);while(p.Inventory.Count<40)Pack(p,1088001,1);
            int draws=0;ExorcismGifts.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>{draws++;return 0;}));ItemHandler.UseItem(pack,p);
            bool fits=!gold && stack==1;
            Check(!Fenced(p) && ExorcismOpenings(p)==(fits?1:0) && (fits || draws==0 && pack.StackSize==stack),"capacity is checked before randomness and counts the released singleton slot: "+gold+"/"+stack);
            if(!fits){while(p.Inventory.Count>38)DisRemove(p,p.Inventory.Objects.First(i=>i.ID==1088001));ItemHandler.UseItem(pack,p);
                Check(!Fenced(p) && ExorcismOpenings(p)==1 && p.Inventory.Count==(stack==1?(gold?39:38):(gold?40:39)),"complete possible bundle fits after making space");}
        }
        ExorcismRoll(0,0);
        var full=ExorcismPlayer(market);var stackTokens=Pack(full,711211,6);while(full.Inventory.Count<40)Pack(full,1088001,1);ExorcismNpc(full,6);ExorcismNpc(full,8);
        Check(!Fenced(full) && ExorcismExchanges(full)==0 && stackTokens.StackSize==6,"full bag preserves a partially consumed token stack");
        DisRemove(full,stackTokens);Pack(full,711211,3);ExorcismNpc(full,6);ExorcismNpc(full,8);
        Check(!Fenced(full) && ExorcismExchanges(full)==1 && full.Inventory.Count==40,"fully consumed token stack supplies its pack slot");
        var race=ExorcismPlayer(market);var racePack=Pack(race,729594,3);
        Parallel.For(0,20,n=>{if(n%3==0)EntityTable.SaveEntity(race);else ItemHandler.UseItem(racePack,race);});
        Check(!Fenced(race) && ExorcismOpenings(race)==1 && racePack.StackSize==2 && race.Entity.BoundCps==50,"repeated simultaneous stack clicks consume one unit and one complete purse");
        exorcismNow=exorcismNow.AddSeconds(1);ItemHandler.UseItem(racePack,race);race=ReloadBall(race);ItemHandler.UseItem(race.Inventory.Objects.Single(i=>i.UID==racePack.UID),race);
        Check(!Fenced(race) && ExorcismOpenings(race)==3 && race.Entity.BoundCps==150 && !race.Inventory.ContainsUID(racePack.UID),"later intentional opening and reconnected remaining stack grant exactly three owned units");
        VerifyExorcismFailures(market);
        VerifyExorcismFrames(market);
    }
    static void ExorcismFrame(GameState p,byte option,uint npc=0) {
        var frame=new byte[28];Writer.WriteUInt16(20,0,frame);Writer.WriteUInt16(2032,2,frame);Writer.WriteUInt32(npc,8,frame);frame[14]=option;
        Writer.WriteUInt64(PacketHandler.ClientSeal,20,frame);PacketHandler.HandlePacket(frame,p);
    }
    static void VerifyExorcismFrames(Map market) {
        var p=ExorcismPlayer(market);var tokens=Pack(p,711211,12);p.ActiveNpc=12333;ExorcismFrame(p,6);
        foreach(string invalid in new[]{"short","oversize","trailing","input"}) {
            int length=invalid=="short"?18:invalid=="oversize"?121:20;var frame=new byte[length+8+(invalid=="trailing"?1:0)];
            Writer.WriteUInt16((ushort)length,0,frame);Writer.WriteUInt16(2032,2,frame);frame[14]=9;if(invalid=="input")frame[17]=20;
            Writer.WriteUInt64(PacketHandler.ClientSeal,length,frame);PacketHandler.HandlePacket(frame,p);
            Check(!Fenced(p) && ExorcismExchanges(p)==0 && tokens.StackSize==12,"bounded Exorcism continuation rejects malformed frame: "+invalid);
        }
        ExorcismFrame(p,9,1);Check(ExorcismExchanges(p)==0,"packet dispatch cannot rewrite a foreign NPC into a valid exchange");
        ExorcismFrame(p,255);ExorcismFrame(p,9);Check(ExorcismExchanges(p)==0 && tokens.StackSize==12,"native zero-ID dismissal cancels the exchange offer before generic dismissal");
        ExorcismFrame(p,6);ExorcismFrame(p,9);Check(!Fenced(p) && ExorcismExchanges(p)==1 && tokens.StackSize==3,"bounded native exchange continuation reaches the ordinary service");
        foreach(bool admin in new[]{false,true}) {
            var actor=ExorcismPlayer(market);actor.Entity.ThirlSpookLevel=1;actor.Account.State=admin?AccountTable.AccountState.Administrador:AccountTable.AccountState.Player;
            int maps=Kernel.Maps.Count;foreach(int length in new[]{4,12,20,64}) {
                var frame=new byte[length+8];Writer.WriteUInt16((ushort)length,0,frame);Writer.WriteUInt16(2400,2,frame);Writer.WriteUInt64(PacketHandler.ClientSeal,length,frame);
                PacketHandler.HandlePacket(frame,actor);
            }
            Check(actor.Entity.SpookMap==null && actor.MessageOK==null && actor.Entity.MapID==1036 && Kernel.Maps.Count==maps,
                "unconfirmed 2400 cannot prepare entry or allocate a map for any account: admin="+admin);
        }
    }
    static void VerifyExorcismFailures(Map market) {
        foreach(bool exchange in new[]{false,true})foreach(string fault in new[]{"delete","stack","item","receipt","wallet"}) {
            if(exchange && fault=="wallet")continue;
            var p=ExorcismPlayer(market);var item=Pack(p,exchange?711211u:729594u,(ushort)(exchange?(fault=="stack"?12:9):(fault=="stack"?2:1)));
            string table=fault=="receipt"?(exchange?"revora_exorcism_exchanges":"revora_exorcism_openings"):fault=="wallet"?"entities":"items";
            string verb=fault=="receipt" || fault=="item"?"INSERT":fault=="delete"?"DELETE":"UPDATE";
            Query("CREATE FUNCTION suppress_exorcism() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_exorcism BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_exorcism()");
            try{if(exchange){ExorcismNpc(p,6);ExorcismNpc(p,9);}else ItemHandler.UseItem(item,p);}
            finally{Query("DROP TRIGGER suppress_exorcism ON "+table+";DROP FUNCTION suppress_exorcism()");}
            Check(Fenced(p) && ExorcismExchanges(p)==0 && ExorcismOpenings(p)==0 && p.Entity.BoundCps==0 && p.Inventory.Count==1
                && SavedRune(item,"stacksize")==item.StackSize && Scalar("SELECT count(*) FROM items WHERE entityid=@uid",p.Entity.UID)==1
                && Scalar("SELECT COALESCE(boundcps,0) FROM entities WHERE uid=@uid",p.Entity.UID)==0,"suppressed write rolls back debit, all items, purse and receipt: "+exchange+"/"+fault);
            p=ReloadBall(p);ExorcismStand(p);if(exchange){ExorcismNpc(p,6);ExorcismNpc(p,9);}else ItemHandler.UseItem(p.Inventory.Objects.Single(),p);
            Check(!Fenced(p) && (exchange?ExorcismExchanges(p):ExorcismOpenings(p))==1,"production reload retries the complete operation after "+exchange+"/"+fault);
        }
        var late=ExorcismPlayer(market);var latePack=Pack(late,729594,1);
        Query("CREATE FUNCTION suppress_chi() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF NEW.id="+Config.DailyQuestChiToken+" THEN RETURN NULL; END IF; RETURN NEW; END';CREATE TRIGGER suppress_chi BEFORE INSERT ON items FOR EACH ROW EXECUTE FUNCTION suppress_chi()");
        try{ItemHandler.UseItem(latePack,late);}finally{Query("DROP TRIGGER suppress_chi ON items;DROP FUNCTION suppress_chi()");}
        Check(Fenced(late) && ExorcismOpenings(late)==0 && late.Inventory.Count==1 && SavedRune(latePack,"stacksize")==1
            && Scalar("SELECT count(*) FROM items WHERE entityid=@uid",late.Entity.UID)==1 && late.Entity.BoundCps==0,"failure of the second gift also rolls back the first gift and consumed pack");
        late=ReloadBall(late);ItemHandler.UseItem(late.Inventory.Objects.Single(),late);
        Check(!Fenced(late) && ExorcismOpenings(late)==1 && late.Inventory.Count==2 && late.Entity.BoundCps==50,"reload delivers both gifts after the second-gift failure");
        foreach(bool exchange in new[]{false,true})foreach(bool hidden in new[]{false,true}) {
            var p=ExorcismPlayer(market);var source=Pack(p,exchange?711211u:729594u,(ushort)(exchange?9:1));var other=Pack(p,1088001,1);
            if(hidden)p.Inventory.Remove(other.UID,Enums.ItemUse.None,false);else Query("DELETE FROM items WHERE uid=@uid","uid",(long)other.UID);
            if(exchange){ExorcismNpc(p,6);ExorcismNpc(p,9);}else ItemHandler.UseItem(source,p);
            Check(Fenced(p) && ExorcismExchanges(p)==0 && ExorcismOpenings(p)==0 && p.Inventory.ContainsUID(source.UID),"exact bag membership mismatch prevents all reward writes: "+exchange+"/"+hidden);
        }
        var p1=ExorcismPlayer(market);var golden=Pack(p1,729594,1);p1.Entity.BoundCps=uint.MaxValue-49;ItemHandler.UseItem(golden,p1);
        Check(!Fenced(p1) && ExorcismOpenings(p1)==0 && p1.Inventory.ContainsUID(golden.UID),"bound CP overflow preserves the pack");
        p1.Entity.BoundCps=uint.MaxValue-50;ItemHandler.UseItem(golden,p1);p1=ReloadBall(p1);
        Check(!Fenced(p1) && ExorcismOpenings(p1)==1 && p1.Entity.BoundCps==uint.MaxValue,"exact bound CP maximum persists without overflow");
        var stale=ExorcismPlayer(market);var stalePack=Pack(stale,729594,1);Query("UPDATE entities SET boundcps=99 WHERE uid=@uid","uid",(long)stale.Entity.UID);ItemHandler.UseItem(stalePack,stale);
        Check(Fenced(stale) && ExorcismOpenings(stale)==0 && stale.Inventory.Count==1 && Scalar("SELECT boundcps FROM entities WHERE uid=@uid",stale.Entity.UID)==99,"stale bound purse cannot overwrite the durable balance");
        stale=ReloadBall(stale);ItemHandler.UseItem(stale.Inventory.Objects.Single(),stale);
        Check(!Fenced(stale) && ExorcismOpenings(stale)==1 && stale.Entity.BoundCps==149,"reload incorporates the newer bound balance before retry");
        var nil=ExorcismPlayer(market);Query("UPDATE entities SET boundcps=NULL WHERE uid=@uid","uid",(long)nil.Entity.UID);nil=ReloadBall(nil);ItemHandler.UseItem(Pack(nil,729594,1),nil);
        Check(!Fenced(nil) && ExorcismOpenings(nil)==1 && nil.Entity.BoundCps==50,"nullable legacy bound purse is treated as zero");
        foreach(bool exchange in new[]{false,true})foreach(string change in new[]{"entityid=1","stacksize=12","bound=1","locked=1","warehouse=1","position=1","suspicious=1","inwardrobe=1","days='1'"}) {
            var p=ExorcismPlayer(market);var item=Pack(p,exchange?711211u:729594u,(ushort)(exchange?9:1));Query("UPDATE items SET "+change+" WHERE uid=@uid","uid",(long)item.UID);
            if(exchange){ExorcismNpc(p,6);ExorcismNpc(p,9);}else ItemHandler.UseItem(item,p);
            Check(Fenced(p) && ExorcismOpenings(p)==0 && ExorcismExchanges(p)==0 && p.Entity.BoundCps==0 && p.Inventory.ContainsUID(item.UID),"changed source preimage cannot produce rewards: "+exchange+"/"+change);
        }
        var guarded=ExorcismPlayer(market);var kept=Pack(guarded,729594,1);
        guarded.Trade.InTrade=true;ItemHandler.UseItem(kept,guarded);guarded.Trade.InTrade=false;
        guarded.Fake=true;ItemHandler.UseItem(kept,guarded);guarded.Fake=false;
        guarded.Entity.FullyLoaded=false;ItemHandler.UseItem(kept,guarded);guarded.Entity.FullyLoaded=true;
        kept.Lock=1;ItemHandler.UseItem(kept,guarded);kept.Lock=0;kept.Days=1;ItemHandler.UseItem(kept,guarded);kept.Days=0;
        kept.TimeLeftInMinutes=100;ItemHandler.UseItem(kept,guarded);kept.TimeLeftInMinutes=0;
        ItemHandler.UseItem(new ConquerItem(true){UID=kept.UID,ID=kept.ID},guarded);
        Check(!Fenced(guarded) && ExorcismOpenings(guarded)==0 && guarded.Inventory.ContainsUID(kept.UID),"pack eligibility and exact object identity are required");
        ItemHandler.UseItem(kept,guarded);Check(!Fenced(guarded) && ExorcismOpenings(guarded)==1,"restored ordinary eligibility can open the preserved pack");
        foreach(int invalid in new[]{-1,1000}) {
            var p=ExorcismPlayer(market);var pack=Pack(p,729594,1);ExorcismGifts.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>invalid));ItemHandler.UseItem(pack,p);
            Check(Fenced(p) && ExorcismOpenings(p)==0 && p.Inventory.Count==1 && p.Entity.BoundCps==0 && SavedRune(pack,"stacksize")==1,"invalid random outcome rolls back the whole opening");
        }
        ExorcismRoll(0,0);
    }
}
