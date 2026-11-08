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
    static readonly Type ChiCash=typeof(Entity).Assembly.GetType("Revora.Game.Features.ChiTokenCash");
    static DateTime cashNow=DateTime.UtcNow;
    static long CashReceipts(GameState p){return Scalar("SELECT count(*) FROM revora_chi_token_cash WHERE uid=@uid",p.Entity.UID);}
    static int CashUnits(GameState p){return p.Inventory.Objects.Where(i=>i.ID==729304).Sum(i=>Math.Max(1,(int)i.StackSize));}
    static void CashUse(GameState p,ConquerItem item){ItemUsage.Process(PrayerPacket(item.UID),p);}
    static void VerifyChiCash(string runtime,Map town) {
        VerifySouls(runtime,town);Call(ChiCash,null,"Initialize");Call(ChiCash,null,"Initialize");
        ChiCash.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>cashNow));
        Check(Config.ChiTokenCashCp==1500 && Config.ChiTokenCashRevoraPoints==5,"Chi cash keeps the configured seven-token economy");
        foreach(bool bound in new[]{false,true})foreach(ushort stack in new ushort[]{0,1,7,8,14}) {
            var p=BallPlayer(town);var selected=NestedPack(p,729304,stack,bound);
            if(stack<=1)for(int i=0;i<6;i++)NestedPack(p,729304,stack,bound);
            int before=CashUnits(p);uint cp=p.Entity.ConquerPoints,rvp=p.Entity.RevoraPoint;
            Parallel.For(0,24,i=>{if(i%3==0)EntityTable.SaveEntity(p);else CashUse(p,selected);});
            Check(!Fenced(p) && CashReceipts(p)==1 && CashUnits(p)==before-7 && p.Entity.ConquerPoints==cp+1500 && p.Entity.RevoraPoint==rvp+5,
                "ordinary concurrent token use and autosave debit exactly seven: "+bound+"/"+stack);
            ItemHandler.UseItem(selected,p);Check(CashReceipts(p)==1,"direct use shares selected-item replay and session debounce");
            p=ReloadBall(p);Check(!Fenced(p) && CashUnits(p)==before-7 && p.Entity.ConquerPoints==cp+1500 && p.Entity.RevoraPoint==rvp+5,"cash and exact remaining units survive production reload");
            if(stack==14) {cashNow=cashNow.AddSeconds(1);CashUse(p,p.Inventory.Objects.Single());Check(!Fenced(p) && CashReceipts(p)==2 && CashUnits(p)==0 && p.Entity.ConquerPoints==cp+3000,"intentional second redemption consumes remaining seven");}
        }
        var mixed=BallPlayer(town);var first=NestedPack(mixed,729304,2,false);var second=NestedPack(mixed,729304,5,true);var chosen=NestedPack(mixed,729304,3,true);
        CashUse(mixed,chosen);Check(!Fenced(mixed) && CashUnits(mixed)==3 && !mixed.Inventory.ContainsUID(chosen.UID) && !mixed.Inventory.ContainsUID(first.UID)
            && mixed.Inventory.Objects.Single().UID==second.UID && second.StackSize==3,"clicked source is consumed first and mixed binding retains exact surplus");
        Check(Scalar("SELECT cardinality(source_uids) FROM revora_chi_token_cash WHERE uid=@uid",mixed.Entity.UID)==7
            && EpkCount("SELECT count(*) FROM revora_chi_token_cash WHERE uid=@uid AND source_uids[1]=@selected AND source_bound[1] AND NOT source_bound[4]","uid",(long)mixed.Entity.UID,"selected",(long)chosen.UID)==1,"receipt records all seven source units and their bindings");
        var full=BallPlayer(town);var token=Pack(full,729304,7);while(full.Inventory.Count<40)Pack(full,1000000,1);CashUse(full,token);
        Check(!Fenced(full) && full.Inventory.Count==39 && CashReceipts(full)==1,"full bag can exchange tokens without extra space");
        foreach(string fault in new[]{"first-delete","late-delete","stack","balance","receipt"}) {
            var p=BallPlayer(town);token=Pack(p,729304,(ushort)(fault=="stack"?8:1));if(fault!="stack")for(int n=0;n<6;n++)Pack(p,729304,1);
            string table=fault=="balance"?"entities":fault=="receipt"?"revora_chi_token_cash":"items";
            string op=fault=="stack" || fault=="balance"?"UPDATE":fault=="receipt"?"INSERT":"DELETE";
            string body=fault=="late-delete"?"IF (SELECT count(*) FROM items WHERE entityid=OLD.entityid AND id=729304)<=3 THEN RETURN NULL; END IF; RETURN OLD;":"RETURN NULL;";
            Query("CREATE FUNCTION suppress_cash() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN "+body+" END';CREATE TRIGGER suppress_cash BEFORE "+op+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_cash()");
            uint cp=p.Entity.ConquerPoints,rvp=p.Entity.RevoraPoint;int units=CashUnits(p);
            try{CashUse(p,token);}finally{Query("DROP TRIGGER suppress_cash ON "+table+";DROP FUNCTION suppress_cash()");}
            Check(Fenced(p) && CashReceipts(p)==0 && CashUnits(p)==units && Scalar("SELECT sum(GREATEST(stacksize,1)) FROM items WHERE entityid=@uid AND id=729304",p.Entity.UID)==units
                && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==cp && Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",p.Entity.UID)==rvp,"suppressed "+fault+" preserves every source and both balances");
            p=ReloadBall(p);CashUse(p,p.Inventory.Objects.First(i=>i.ID==729304));Check(!Fenced(p) && CashReceipts(p)==1 && CashUnits(p)==units-7,"production reload retries entire cash exchange: "+fault);
        }
        foreach(string field in new[]{"conquerpoints","revorapoint","stacksize","bound","locked","bag"}) {
            var p=BallPlayer(town);token=Pack(p,729304,8);
            if(field=="bag"){var hidden=Pack(p,1088001,1);p.Inventory.Remove(hidden.UID,Enums.ItemUse.None,false);}
            else if(field=="conquerpoints" || field=="revorapoint")Query("UPDATE entities SET "+field+"="+field+"+1 WHERE uid=@uid","uid",(long)p.Entity.UID);
            else Query("UPDATE items SET "+field+"="+field+"+1 WHERE uid=@uid","uid",(long)token.UID);
            CashUse(p,token);Check(Fenced(p) && CashReceipts(p)==0 && token.StackSize==8,"stale "+field+" cannot debit or pay token cash");
        }
        foreach(string state in new[]{"short","locked","expired","fake","dead","trade","replaced","substituted"}) {
            var p=BallPlayer(town);token=Pack(p,729304,(ushort)(state=="short"?6:7));
            if(state=="locked")token.Lock=1;if(state=="expired"){token.Days=1;token.DayStamp=cashNow.ToLocalTime().AddDays(-2);}if(state=="fake")p.Fake=true;
            if(state=="dead")p.Entity.Hitpoints=0;if(state=="trade")p.Trade.InTrade=true;
            if(state=="replaced"){var replacement=ReloadBall(p);Check(!ReferenceEquals(p,replacement),"token session replacement uses production loader");}
            if(state=="substituted")ItemHandler.UseItem(new ConquerItem(true){UID=token.UID,ID=token.ID,StackSize=7},p);else CashUse(p,token);
            Check(!Fenced(p) && CashReceipts(p)==0 && p.Inventory.ContainsUID(token.UID),"ineligible "+state+" preserves token exchange");
        }
        foreach(string currency in new[]{"conquerpoints","revorapoint"}) {
            var p=BallPlayer(town);Query("UPDATE entities SET "+currency+"=4294967295 WHERE uid=@uid","uid",(long)p.Entity.UID);p=ReloadBall(p);token=Pack(p,729304,7);CashUse(p,token);
            Check(!Fenced(p) && CashReceipts(p)==0 && CashUnits(p)==7,"cash overflow preserves every token: "+currency);
        }
        var nullable=BallPlayer(town);Query("UPDATE entities SET conquerpoints=NULL,revorapoint=NULL WHERE uid=@uid","uid",(long)nullable.Entity.UID);nullable=ReloadBall(nullable);token=Pack(nullable,729304,7);CashUse(nullable,token);
        Check(!Fenced(nullable) && CashReceipts(nullable)==1 && nullable.Entity.ConquerPoints==1500 && nullable.Entity.RevoraPoint==5,"legacy nullable balances use loaded zero semantics");
        var wire=BallPlayer(town);token=Pack(wire,729304,7);foreach(string shape in new[]{"short","large","length","seal","action","id"}) {
            var bytes=PrayerPacket(token.UID);if(shape=="short")Array.Resize(ref bytes,31);if(shape=="large")Array.Resize(ref bytes,129);
            if(shape=="length")bytes[0]++;if(shape=="seal")bytes[bytes.Length-1]^=1;if(shape=="action")Writer.WriteUInt16(5,20,bytes);if(shape=="id")Writer.WriteUInt16(1008,2,bytes);
            Call(ChiCash,null,"TryHandle",wire,bytes);Check(!Fenced(wire) && CashReceipts(wire)==0 && CashUnits(wire)==7,"malformed token frame is inert: "+shape);
        }
        CashUse(wire,token);Check(!Fenced(wire) && CashReceipts(wire)==1,"valid item packet still redeems after rejected frames");
        VerifyEarnedChiCash(town);
    }
    static void VerifyEarnedChiCash(Map town) {
        var p=SoulPlayer(town);SoulNpc(p,79882,3);
        for(int day=0;day<7;day++) {
            SoulWalkNpc(p,301,1);foreach(uint id in new uint[]{65822,65823,65824}) {SoulWalkNpc(p,id,1);soulNow=soulNow.AddSeconds(3);Call(Souls,null,"Tick");}
            SoulWalkNpc(p,301,3);p=SoulReload(p);Check(!Fenced(p) && CashUnits(p)==day+1,"earned separate daily Chi Token survives reconnect: day "+(day+1));soulNow=soulNow.AddDays(1);
        }
        var sources=p.Inventory.Objects.Where(i=>i.ID==729304).Select(i=>i.UID).ToArray();CashUse(p,p.Inventory.Objects.First(i=>i.ID==729304));p=ReloadBall(p);
        Check(!Fenced(p) && CashReceipts(p)==1 && CashUnits(p)==0 && sources.All(uid=>!p.Inventory.ContainsUID(uid)) && p.Inventory.Objects.Count(i=>i.ID==722136)==7
            && p.Entity.ConquerPoints==39000 && p.Entity.RevoraPoint==210,"seven complete ordinary daily routes redeem their actual gifts and persist cash exactly once");
    }
}
