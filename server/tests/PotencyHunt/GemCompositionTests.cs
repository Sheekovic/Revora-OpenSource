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
    static readonly Type Gems=typeof(Entity).Assembly.GetType("Revora.Game.Features.GemComposition");
    static byte[] GemRequest(uint type,bool bound=false,uint tick=1)
    {
        var b=new byte[103];Writer.WriteUInt16(95,0,b);Writer.WriteUInt16(1009,2,b);
        Writer.WriteUInt32(tick,4,b);Writer.WriteUInt32(type,8,b);Writer.WriteUInt32(bound?1u:0u,12,b);
        Writer.WriteUInt16(39,20,b);Writer.WriteUInt32(tick+10,22,b);Writer.WriteUInt64(PacketHandler.ClientSeal,95,b);return b;
    }
    static void GemStock(GameState p,uint type,int count,bool bound=false)
    {
        for(int i=0;i<count;i++){var item=Pack(p,type,1);item.Bound=bound;
            Query("UPDATE items SET bound=@bound WHERE uid=@uid","bound",bound?1:0,"uid",(long)item.UID);}
    }
    static long GemReceipts(GameState p){return Scalar("SELECT count(*) FROM revora_gem_compositions WHERE uid=@uid",p.Entity.UID);}
    static void VerifyGemComposition(Map town)
    {
        Query("DROP TABLE entities;CREATE TABLE entities AS TABLE public.entities WITH NO DATA;ALTER TABLE entities ADD PRIMARY KEY(uid);CREATE TABLE titles (LIKE public.titles INCLUDING ALL)");
        foreach(string table in new[]{"teamarena","championarena","quests","status","reward","rewardclameid","merchant","dialyquests","activeness","pkexploits","achievement","itemadding","profs","npcs","sobnpcs","house","furniture"})
            Query("CREATE TABLE IF NOT EXISTS "+table+" AS TABLE public."+table+" WITH NO DATA");
        Query("CREATE TABLE prizes(uid bigint PRIMARY KEY,name text,prizes bytea)");
        StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
        Call(Gems,null,"Initialize");Call(Gems,null,"Initialize");
        var now=new DateTime(2032,2,1,0,0,0,DateTimeKind.Utc);
        Gems.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>now));
        foreach(uint family in new uint[]{700000,700010,700020,700030,700040,700050,700060,700070,700100,700120})
        foreach(uint grade in new uint[]{1,2})foreach(bool bound in new[]{false,true}) {
            if(family==700070 && grade==1)continue;
            uint type=family+grade;ulong fee=grade==1?10000UL:type==700072?1000000UL:800000UL;
            var p=BookBuyer(town,fee);GemStock(p,type,15,bound);var request=GemRequest(type,bound);
            Parallel.For(0,8,i=>ItemUsage.Process(request,p));
            Check(!Fenced(p) && p.Entity.Money==0 && p.Inventory.Count==1 && p.Inventory.Objects.Single().ID==type+1
                && p.Inventory.Objects.Single().Bound==bound && GemReceipts(p)==1
                && Scalar("SELECT count(*) FROM revora_gem_materials m JOIN revora_gem_compositions c ON c.id=m.composition WHERE c.uid=@uid",p.Entity.UID)==15,
                "fifteen owned gems and exact fee compose once: "+type+" bound="+bound);
            p=ReloadBall(p);ItemUsage.Process(request,p);
            Check(!Fenced(p) && p.Entity.Money==0 && p.Inventory.Count==1 && p.Inventory.Objects.Single().ID==type+1
                && p.Inventory.Objects.Single().Bound==bound && GemReceipts(p)==1,"production reload retains composed gem and silver: "+type+"/"+bound);
        }
        var replay=BookBuyer(town,20000);GemStock(replay,700001,30);var same=GemRequest(700001);
        Parallel.For(0,12,i=>ItemUsage.Process(same,replay));replay=ReloadBall(replay);ItemUsage.Process(same,replay);
        Check(!Fenced(replay) && replay.Entity.Money==10000 && replay.Inventory.Objects.Count(i=>i.ID==700001)==15 && GemReceipts(replay)==1,
            "durable exact-request replay cannot consume another available batch after reconnect");
        Parallel.For(0,12,i=>ItemUsage.Process(GemRequest(700001,false,(uint)(i+2)),replay));
        Check(!Fenced(replay) && replay.Entity.Money==10000 && GemReceipts(replay)==1,"rapid clicks with different ticks cannot charge a second composition");
        now=now.AddSeconds(1);ItemUsage.Process(GemRequest(700001,false,100),replay);
        Check(!Fenced(replay) && replay.Entity.Money==0 && replay.Inventory.Objects.Count(i=>i.ID==700002)==2 && GemReceipts(replay)==2,
            "a new intentional request may compose the next owned batch");
        var full=BookBuyer(town,10000);GemStock(full,700001,15);for(int i=0;i<25;i++)Pack(full,1000000,1);
        ItemUsage.Process(GemRequest(700001),full);
        Check(!Fenced(full) && full.Inventory.Count==26 && full.Entity.Money==0,"full bag can compose using slots freed by consumed gems");
        foreach(string why in new[]{"short","mixed","locked","stored","equipped","suspicious","stacked","expired","poor","trade","dead","stale"}) {
            var p=BookBuyer(town,why=="poor"?9999UL:10000UL);GemStock(p,700001,why=="short"?14:15);
            var item=p.Inventory.Objects.First();
            if(why=="mixed")item.Bound=true;
            if(why=="locked")item.Lock=1;
            if(why=="stored")item.Warehouse=1;
            if(why=="equipped")item.Position=4;
            if(why=="suspicious")item.Suspicious=true;
            if(why=="stacked")item.StackSize=15;
            if(why=="expired")item.Days=1;
            if(why=="trade")p.Trade.InTrade=true;
            if(why=="dead")p.Entity.Hitpoints=0;
            if(why=="stale")Query("UPDATE items SET entityid=0 WHERE uid=@uid","uid",(long)item.UID);
            int count=p.Inventory.Count;ulong before=p.Entity.Money;ItemUsage.Process(GemRequest(700001),p);
            Check(p.Entity.Money==before && p.Inventory.Count==count && GemReceipts(p)==0
                && Scalar("SELECT money FROM entities WHERE uid=@uid",p.Entity.UID)==(long)before,"ineligible gem composition preserves payment and bag: "+why);
        }
        var malformed=BookBuyer(town,1000000);GemStock(malformed,700001,15);
        foreach(string why in new[]{"length","opcode","seal","binding","count","type","super","normal-tortoise","action-high","truncated","oversized"}) {
            byte[] b=GemRequest(700001);
            if(why=="length")Writer.WriteUInt16(94,0,b);
            if(why=="opcode")Writer.WriteUInt16(1008,2,b);
            if(why=="seal")b[102]^=1;
            if(why=="binding")Writer.WriteUInt32(2,12,b);
            if(why=="count")Writer.WriteUInt32(uint.MaxValue,26,b);
            if(why=="type")Writer.WriteUInt32(1088000,8,b);
            if(why=="super")Writer.WriteUInt32(700003,8,b);
            if(why=="normal-tortoise")Writer.WriteUInt32(700071,8,b);
            if(why=="action-high")Writer.WriteUInt16(295,20,b);
            if(why=="truncated")Array.Resize(ref b,22);
            if(why=="oversized")Array.Resize(ref b,10000);
            ItemUsage.Process(b,malformed);
            Check(!Fenced(malformed) && malformed.Entity.Money==1000000 && malformed.Inventory.Count==15 && GemReceipts(malformed)==0,
                "malformed gem request is rejected without mutation: "+why);
        }
        var mixed=new byte[131];Writer.WriteUInt16(123,0,mixed);Writer.WriteUInt16(1009,2,mixed);Writer.WriteUInt16(51,20,mixed);
        Writer.WriteUInt32(7,26,mixed);Writer.WriteUInt64(PacketHandler.ClientSeal,123,mixed);ItemUsage.Process(mixed,malformed);
        Check(malformed.Entity.Money==1000000 && malformed.Inventory.Count==15 && GemReceipts(malformed)==0,"unconfirmed mixed Tortoise recipe cannot reach the legacy grant");
        foreach(string participant in new[]{"entities","items","items-insert","revora_gem_compositions","revora_gem_materials"}) {
            var p=BookBuyer(town,10000);GemStock(p,700001,15);
            string table=participant=="items-insert"?"items":participant;
            string operation=participant=="entities"?"UPDATE":participant=="items"?"DELETE":"INSERT";
            Query("CREATE FUNCTION suppress_gem() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER suppress_gem BEFORE "+operation+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_gem()");
            ItemUsage.Process(GemRequest(700001),p);
            Query("DROP TRIGGER suppress_gem ON "+table+";DROP FUNCTION suppress_gem()");
            Check(Fenced(p) && p.Entity.Money==10000 && p.Inventory.Count==15 && GemReceipts(p)==0
                && Scalar("SELECT money FROM entities WHERE uid=@uid",p.Entity.UID)==10000
                && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id=700001",p.Entity.UID)==15,
                "suppressed gem transaction participant rolls back every debit: "+participant);
            p=ReloadBall(p);ItemUsage.Process(GemRequest(700001),p);
            Check(!Fenced(p) && p.Entity.Money==0 && p.Inventory.Count==1 && GemReceipts(p)==1,"failed gem transaction can retry after production reload: "+table);
        }
        Gems.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>DateTime.UtcNow));
    }
}
