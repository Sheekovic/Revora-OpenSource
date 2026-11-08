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
    static readonly Type Welcome=typeof(Entity).Assembly.GetType("Revora.Game.Features.WelcomePacks");
    static DateTime welcomeNow=DateTime.UtcNow;
    static long WelcomeUses(GameState p){return Scalar("SELECT count(*) FROM revora_welcome_pack_uses WHERE uid=@uid",p.Entity.UID);}
    static int CountId(GameState p,uint id){return p.Inventory.Objects.Count(i=>i.ID==id);}
    static int CountPlus(GameState p,uint id,byte plus){return p.Inventory.Objects.Count(i=>i.ID==id && i.Plus==plus);}
    static GameState WelcomePlayer(Map town,byte level,byte reborn,byte cls){
        var p=BallPlayer(town);p.Entity.Level=level;p.Entity.Reborn=reborn;p.Entity.Class=cls;
        if(!EntityTable.SaveEntity(p))throw new Exception("Welcome fixture save failed");return p;
    }
    static void OpenWelcome(GameState p,ConquerItem item){ItemUsage.Process(PrayerPacket(item.UID),p);}
    static void VerifyWelcomePacks(string runtime,Map town){
        Call(Welcome,null,"Initialize");Call(Welcome,null,"Initialize");
        Welcome.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>welcomeNow));
        // Creation delivery is exercised through DoLogin and durable item reload
        // by REVIEW-293/295 in ConquerPointBagTests, including rollback and replay.
        var other=WelcomePlayer(town,1,0,15);var side=Pack(other,727026,1);
        Check(!(bool)Call(Welcome,null,"TryUse",side,other) && other.Inventory.ContainsUID(side.UID) && WelcomeUses(other)==0,"727026 stays outside the 723753 welcome service");
        foreach(bool bound in new[]{false,true})foreach(ushort stack in new ushort[]{0,1,2}){
            var p=WelcomePlayer(town,1,0,15);var pack=NestedPack(p,723753,stack,bound);uint cp=p.Entity.ConquerPoints;
            Parallel.For(0,24,n=>{if(n%3==0)EntityTable.SaveEntity(p);else OpenWelcome(p,pack);});
            int expected=stack>1?2:1;
            Check(!Fenced(p) && WelcomeUses(p)==expected && p.Entity.ConquerPoints==cp+10u*(uint)expected && CountId(p,1000000)==3*expected && CountId(p,1001000)==3*expected && CountId(p,723755)==expected
                && CountId(p,723753)==0,"L1 welcome pack pays 10 CP, six ores and the L10 successor: "+bound+"/"+stack);
            ItemHandler.UseItem(pack,p);p=ReloadBall(p);
            Check(!Fenced(p) && WelcomeUses(p)==expected && CountId(p,723755)==expected && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==cp+10L*expected,"L1 welcome reload retains ores, successor and CP");
        }
        var l10=WelcomePlayer(town,9,0,15);var tooSoon=Pack(l10,723755,1);OpenWelcome(l10,tooSoon);
        Check(!Fenced(l10) && WelcomeUses(l10)==0 && l10.Inventory.ContainsUID(tooSoon.UID),"L10 pack refuses below level 10");
        l10.Entity.Level=10;EntityTable.SaveEntity(l10);OpenWelcome(l10,tooSoon);
        Check(!Fenced(l10) && WelcomeUses(l10)==1 && CountId(l10,723700)==10 && CountId(l10,723017)==3 && CountId(l10,723767)==1,"L10 pack pays ten EXP Balls, three potions and the L70 successor");
        var potion=l10.Inventory.Objects.First(i=>i.ID==723017);Call(ExpPotions,null,"Initialize");l10.Entity.DoubleExperienceTime=0;Drink(l10,potion);
        Check(!Fenced(l10) && l10.Entity.DoubleExperienceTime==3600,"welcome EXP Potion remains usable through ExperiencePotions");
        var ball=l10.Inventory.Objects.First(i=>i.ID==723700);Call(ExpItems,null,"Initialize");ulong xp=l10.Entity.Experience;ItemHandler.UseItem(ball,l10);
        Check(!Fenced(l10) && (l10.Entity.Experience>xp || l10.ExpBalls>0 || !l10.Inventory.ContainsUID(ball.UID) || ball.StackSize==0),"welcome EXP Ball remains usable through ExperienceConsumables");
        var l70=WelcomePlayer(town,69,0,15);var wait=Pack(l70,723767,1);OpenWelcome(l70,wait);Check(WelcomeUses(l70)==0 && l70.Inventory.ContainsUID(wait.UID),"L70 pack refuses below level 70");
        l70.Entity.Level=70;EntityTable.SaveEntity(l70);uint l70cp=l70.Entity.ConquerPoints;OpenWelcome(l70,wait);
        Check(!Fenced(l70) && WelcomeUses(l70)==1 && l70.Entity.ConquerPoints==l70cp+20 && CountId(l70,723017)==5 && CountId(l70,1080001)==1 && CountId(l70,723772)==1,"L70 pack pays 20 CP, five potions, an emerald and the L100 successor");
        foreach(var row in new[]{new object[]{(byte)15,800010u},new object[]{(byte)21,800410u},new object[]{(byte)41,800610u},new object[]{(byte)51,800010u},new object[]{(byte)61,800010u},new object[]{(byte)100,800508u}}){
            byte cls=(byte)row[0];uint gem=(uint)row[1];
            var p=WelcomePlayer(town,99,0,cls);var pack=Pack(p,723772,1);OpenWelcome(p,pack);Check(WelcomeUses(p)==0,"L100 pack refuses below level 100 for class "+cls);
            p.Entity.Level=100;EntityTable.SaveEntity(p);uint cp=p.Entity.ConquerPoints;OpenWelcome(p,pack);
            Check(!Fenced(p) && WelcomeUses(p)==1 && p.Entity.ConquerPoints==cp+1000000 && CountId(p,723017)==10 && CountId(p,723774)==1 && CountId(p,gem)==1,"L100 pack pays 1,000,000 CP and class gem "+cls);
        }
        foreach(var row in new[]{new object[]{(byte)15,614439u,2,true},new object[]{(byte)25,560439u,1,false},new object[]{(byte)45,613429u,2,false},new object[]{(byte)55,616439u,2,true},new object[]{(byte)65,160249u,1,false}}){
            byte cls=(byte)row[0];uint weapon=(uint)row[1];int weapons=(int)row[2];bool boundWeapon=(bool)row[3];
            var p=WelcomePlayer(town,109,0,cls);var pack=Pack(p,723774,1);OpenWelcome(p,pack);Check(WelcomeUses(p)==0,"L110 pack refuses below level 110 for class "+cls);
            p.Entity.Level=110;EntityTable.SaveEntity(p);uint cp=p.Entity.ConquerPoints;OpenWelcome(p,pack);
            int jewelry=cls<=55?2:1;
            Check(!Fenced(p) && WelcomeUses(p)==1 && p.Entity.ConquerPoints==cp+30 && CountId(p,723776)==1 && CountId(p,721090)==1 && CountId(p,723017)==10
                && p.Inventory.Objects.Count(i=>i.ID==weapon && i.Plus==(weapon==160249?(byte)8:(byte)8) && i.Bound==boundWeapon)==weapons
                && CountPlus(p,120269,8)==jewelry && CountPlus(p,150269,8)==jewelry,"L110 pack keeps class gear and duplicate jewelry: "+cls);
            p=ReloadBall(p);Check(CountId(p,721090)==1 && CountPlus(p,120269,8)==jewelry,"L110 Moon Box and jewelry survive reload: "+cls);
        }
        var late=WelcomePlayer(town,140,1,15);var last=Pack(late,723776,1);OpenWelcome(late,last);Check(WelcomeUses(late)==0,"L120 pack refuses 140/rebirth 1");
        late=WelcomePlayer(town,139,2,15);last=Pack(late,723776,1);OpenWelcome(late,last);Check(WelcomeUses(late)==0,"L120 pack refuses 139/rebirth 2");
        late=WelcomePlayer(town,140,2,15);last=Pack(late,723776,1);ulong silver=late.Entity.Money;OpenWelcome(late,last);
        Check(!Fenced(late) && WelcomeUses(late)==1 && late.Entity.Money==silver+5000000 && CountId(late,3009003)==5 && late.Inventory.Objects.Where(i=>i.ID==3009003).All(i=>i.Bound),
            "L120 pack pays 5,000,000 silver and five bound 3009003");
        late=ReloadBall(late);Check(Scalar("SELECT money FROM entities WHERE uid=@uid",late.Entity.UID)==(long)(silver+5000000) && CountId(late,3009003)==5,"L120 silver and bound items survive reload");
        var silverFault=WelcomePlayer(town,140,2,15);var silverPack=Pack(silverFault,723776,1);ulong silverBefore=silverFault.Entity.Money;
        Query("CREATE FUNCTION suppress_welcome_silver() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_welcome_silver BEFORE UPDATE ON entities FOR EACH ROW EXECUTE FUNCTION suppress_welcome_silver()");OpenWelcome(silverFault,silverPack);
        Check(Fenced(silverFault) && WelcomeUses(silverFault)==0 && silverFault.Inventory.ContainsUID(silverPack.UID) && CountId(silverFault,3009003)==0
            && silverFault.Entity.Money==silverBefore && Scalar("SELECT money FROM entities WHERE uid=@uid",silverFault.Entity.UID)==(long)silverBefore,"suppressed silver preserves final welcome source, outputs and purse");
        Query("DROP TRIGGER suppress_welcome_silver ON entities;DROP FUNCTION suppress_welcome_silver()");silverFault=ReloadBall(silverFault);OpenWelcome(silverFault,silverFault.Inventory.Objects.Single(i=>i.ID==723776));
        Check(!Fenced(silverFault) && WelcomeUses(silverFault)==1 && CountId(silverFault,3009003)==5 && silverFault.Entity.Money==silverBefore+5000000,"production reload retries the final welcome pack after silver rollback");
        var staleSilver=WelcomePlayer(town,140,2,15);var staleSilverPack=Pack(staleSilver,723776,1);ulong staleSilverBefore=staleSilver.Entity.Money;
        Query("UPDATE entities SET money=money+1 WHERE uid=@uid","uid",(long)staleSilver.Entity.UID);OpenWelcome(staleSilver,staleSilverPack);
        Check(Fenced(staleSilver) && WelcomeUses(staleSilver)==0 && staleSilver.Inventory.ContainsUID(staleSilverPack.UID) && CountId(staleSilver,3009003)==0
            && Scalar("SELECT money FROM entities WHERE uid=@uid",staleSilver.Entity.UID)==(long)staleSilverBefore+1,"stale silver preimage cannot open the final welcome pack");
        var full=WelcomePlayer(town,1,0,15);var starter=Pack(full,723753,1);while(full.Inventory.Count<40)Pack(full,1000000,1);OpenWelcome(full,starter);
        Check(!Fenced(full) && WelcomeUses(full)==0 && full.Inventory.ContainsUID(starter.UID),"exact L1 outputs refuse a full bag");
        var room=WelcomePlayer(town,1,0,15);starter=Pack(room,723753,1);while(room.Inventory.Count<34)Pack(room,1088001,1);OpenWelcome(room,starter);
        Check(!Fenced(room) && WelcomeUses(room)==1 && CountId(room,723755)==1,"L1 pack credits the source slot and fits exact outputs");
        var stacked=WelcomePlayer(town,1,0,15);var pile=Pack(stacked,723753,2);OpenWelcome(stacked,pile);
        Check(!Fenced(stacked) && WelcomeUses(stacked)==1 && pile.StackSize==1 && CountId(stacked,723755)==1,"welcome pack keeps a remaining stacked source");
        var overflow=WelcomePlayer(town,100,0,15);Query("UPDATE entities SET conquerpoints=4294967295 WHERE uid=@uid","uid",(long)overflow.Entity.UID);overflow=ReloadBall(overflow);var rich=Pack(overflow,723772,1);OpenWelcome(overflow,rich);
        Check(!Fenced(overflow) && WelcomeUses(overflow)==0 && overflow.Inventory.ContainsUID(rich.UID) && overflow.Entity.ConquerPoints==uint.MaxValue,"L100 CP overflow preserves the pack");
        foreach(string fault in new[]{"source","output","cp","receipt"}){
            var p=WelcomePlayer(town,1,0,15);var item=Pack(p,723753,1);uint cp=p.Entity.ConquerPoints;
            string table=fault=="cp"?"entities":fault=="receipt"?"revora_welcome_pack_uses":"items";
            string verb=fault=="source"?"DELETE":fault=="cp"?"UPDATE":"INSERT";
            Query("CREATE FUNCTION suppress_welcome() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_welcome BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_welcome()");OpenWelcome(p,item);
            Check(Fenced(p) && WelcomeUses(p)==0 && p.Inventory.ContainsUID(item.UID) && CountId(p,723755)==0 && p.Entity.ConquerPoints==cp
                && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==cp,"suppressed "+fault+" preserves welcome source, outputs and CP");
            Query("DROP TRIGGER suppress_welcome ON "+table+";DROP FUNCTION suppress_welcome()");p=ReloadBall(p);OpenWelcome(p,p.Inventory.Objects.Single(i=>i.ID==723753));
            Check(!Fenced(p) && WelcomeUses(p)==1 && CountId(p,723755)==1,"production reload retries the full welcome pack after "+fault+" rollback");
        }
        var stale=WelcomePlayer(town,1,0,15);var supply=Pack(stale,723753,1);Query("UPDATE entities SET conquerpoints=conquerpoints+1 WHERE uid=@uid","uid",(long)stale.Entity.UID);uint before=stale.Entity.ConquerPoints;OpenWelcome(stale,supply);
        Check(Fenced(stale) && WelcomeUses(stale)==0 && stale.Inventory.ContainsUID(supply.UID) && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",stale.Entity.UID)==before+1,"stale CP preimage cannot open a welcome pack");
        foreach(string guard in new[]{"dead","fake","trade","locked","suspicious","wardrobe","expired","substituted"}){
            var p=WelcomePlayer(town,1,0,15);var item=Pack(p,723753,1);uint cp=p.Entity.ConquerPoints;
            if(guard=="dead")p.Entity.Hitpoints=0;if(guard=="fake")p.Fake=true;if(guard=="trade")p.Trade.InTrade=true;if(guard=="locked")item.Lock=1;
            if(guard=="suspicious")item.Suspicious=true;if(guard=="wardrobe")item.InWardrobe=true;if(guard=="expired"){item.Days=1;item.DayStamp=welcomeNow.ToLocalTime().AddDays(-2);}
            if(guard=="substituted")ItemHandler.UseItem(new ConquerItem(true){ID=item.ID,UID=item.UID,StackSize=1},p);else OpenWelcome(p,item);
            Check(WelcomeUses(p)==0 && CountId(p,723755)==0 && p.Entity.ConquerPoints==cp && p.Inventory.ContainsUID(item.UID),"ineligible welcome pack leaves source unchanged: "+guard);
        }
        var frames=WelcomePlayer(town,1,0,15);var wire=Pack(frames,723753,1);
        foreach(string bad in new[]{"short","long","length","type","seal"}){
            byte[] frame=bad=="short"?new byte[31]:bad=="long"?new byte[129]:PrayerPacket(wire.UID);
            Writer.WriteUInt16((ushort)(frame.Length-8),0,frame);Writer.WriteUInt16(1009,2,frame);Writer.WriteUInt32(wire.UID,8,frame);Writer.WriteUInt16(4,20,frame);Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);
            if(bad=="length")Writer.WriteUInt16(1,0,frame);if(bad=="type")Writer.WriteUInt16(1008,2,frame);if(bad=="seal")frame[frame.Length-1]^=1;
            ItemUsage.Process(frame,frames);Check(!Fenced(frames) && WelcomeUses(frames)==0 && frames.Inventory.ContainsUID(wire.UID),"bounded welcome pack rejects "+bad+" frame");
        }OpenWelcome(frames,wire);Check(!Fenced(frames) && WelcomeUses(frames)==1 && CountId(frames,723755)==1,"ordinary welcome pack works after malformed frames");
        var replay=WelcomePlayer(town,1,0,15);var once=Pack(replay,723753,1);uint uid=once.UID;OpenWelcome(replay,once);
        ItemHandler.UseItem(once,replay);Check(WelcomeUses(replay)==1 && !replay.Inventory.ContainsUID(uid),"consumed welcome pack UID cannot replay");
    }
}
