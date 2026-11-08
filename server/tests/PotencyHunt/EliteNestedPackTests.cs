using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type NestedPrizes=typeof(Entity).Assembly.GetType("Revora.Game.Features.EliteNestedPacks");
    static DateTime nestedNow=DateTime.UtcNow;
    static int nestedRoll,nestedDraws;
    static long NestedReceipts(GameState p){return Scalar("SELECT count(*) FROM revora_epk_nested_openings WHERE uid=@uid",p.Entity.UID);}
    static ConquerItem NestedPack(GameState p,uint id,ushort stack=1,bool bound=false){var i=Pack(p,id,stack);i.Bound=bound;Query("UPDATE items SET bound=@bound WHERE uid=@uid","bound",bound?1:0,"uid",(long)i.UID);return i;}
    static void VerifyNestedPrizes(Map town){
        Query("CREATE TABLE IF NOT EXISTS refinery AS TABLE public.refinery;CREATE TABLE IF NOT EXISTS refineryboxes AS TABLE public.refineryboxes");
        Refinery.LoadItems();Call(NestedPrizes,null,"Initialize");Call(NestedPrizes,null,"Initialize");
        NestedPrizes.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>nestedNow));
        NestedPrizes.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>{nestedDraws++;return nestedRoll;}));
        var accessories=ConquerItemInformation.BaseInformations.Values.Where(i=>i.ID>=350001 && i.ID<=380030 && i.ID!=360204).OrderBy(i=>i.ID).ToArray();
        Check(accessories.Length==236 && accessories.Count(i=>i.TimeTillRemovInMinutes==0)==130 && accessories.Count(i=>i.TimeTillRemovInMinutes==7200)==5
            && accessories.Count(i=>i.TimeTillRemovInMinutes==10080)==57 && accessories.Count(i=>i.TimeTillRemovInMinutes==43200)==44,"reviewed accessory catalog includes permanent, five-, seven- and thirty-day gifts");
        for(int n=0;n<accessories.Length;n++)foreach(bool bound in new[]{false,true}){
            nestedRoll=n;var p=BallPlayer(town);var source=NestedPack(p,720731,1,bound);uint cp=p.Entity.ConquerPoints;ulong money=p.Entity.Money;
            ItemHandler.UseItem(source,p);var gift=p.Inventory.Objects.Single();uint uid=gift.UID;int days=(int)accessories[n].TimeTillRemovInMinutes/1440;
            Check(!Fenced(p) && NestedReceipts(p)==1 && gift.ID==accessories[n].ID && gift.Bound==bound && gift.Days==days && gift.StackSize==1
                && gift.Durability==accessories[n].Durability && (days==0 || gift.DayStamp==nestedNow.ToLocalTime()) && p.Entity.ConquerPoints==cp && p.Entity.Money==money,
                "accessory opening grants its exact bound/lifetime contract: "+gift.ID+"/"+bound);
            ItemHandler.UseItem(source,p);p=ReloadBall(p);gift=p.Inventory.Objects.Single();
            Check(!Fenced(p) && NestedReceipts(p)==1 && gift.UID==uid && gift.ID==accessories[n].ID && gift.Bound==bound && gift.Days==days
                && (days==0?gift.TimeLeftInMinutes==0:gift.TimeLeftInMinutes>0 && gift.TimeLeftInMinutes<=(uint)days*86400),"accessory survives production reload and spent-pack replay: "+gift.ID+"/"+bound);
        }
        for(nestedRoll=0;nestedRoll<4;nestedRoll++)foreach(bool bound in new[]{false,true}){
            var p=BallPlayer(town);var source=NestedPack(p,720049,1,bound);ItemHandler.UseItem(source,p);var horse=p.Inventory.Objects.Single();
            uint color=nestedRoll<2?16750080u:nestedRoll==2?65430u:9830655u;
            Check(!Fenced(p) && NestedReceipts(p)==1 && horse.ID==300000 && horse.Plus==6 && horse.Effect==Enums.ItemEffect.Horse && horse.SocketProgress==color && horse.Bound==bound,
                "steed retains +6 and the legacy 50/25/25 color slot: "+nestedRoll+"/"+bound);
            p=ReloadBall(p);horse=p.Inventory.Objects.Single();Check(!Fenced(p) && horse.Plus==6 && horse.Effect==Enums.ItemEffect.Horse && horse.SocketProgress==color && horse.Bound==bound,"steed attributes survive production reload");
        }
        foreach(byte job in new byte[]{10,15,20,25,40,45,50,55,60,65,70,75,80,85,100,101,132,135,142,145,160,165}){
            uint[] expected=job>=100 && job<160?new uint[]{724424}:job>=40 && job<=45?new uint[]{724404,724463}
                :job>=20 && job<=25?new uint[]{724409,724414,724453,724458}:new uint[]{724409,724453};
            for(int n=0;n<expected.Length;n++){
                nestedRoll=n;var p=BallPlayer(town);p.Entity.Class=job;EntityTable.SaveEntity(p);var source=NestedPack(p,729626,1,true);ItemHandler.UseItem(source,p);
                var gift=p.Inventory.Objects.Single();Check(!Fenced(p) && NestedReceipts(p)==1 && gift.ID==expected[n] && gift.Bound && Kernel.DatabaseRefinery[gift.ID].Level==5,
                    "class "+job+" receives a Super weapon material instead of a Superior pack or armor refinery: "+expected[n]);
                uint weaponId=job>=100 && job<160?421009u:job>=40 && job<=45?500009u:job>=50 && job<=55?601009u:job>=60 && job<=65?610009u
                    :job>=70 && job<=75?611009u:job>=80 && job<=85?617009u:job>=160?626009u:Kernel.DatabaseRefinery[gift.ID].Position==5?560009u:410009u;
                Check(ConquerItemInformation.BaseInformations.ContainsKey(weaponId) && (bool)Call(RefineProgress,null,"Fits",new ConquerItem(true){ID=weaponId},Kernel.DatabaseRefinery[gift.ID]),
                    "class "+job+" prize is applicable to its catalog weapon family "+weaponId/1000);
                p=ReloadBall(p);Check(!Fenced(p) && p.Inventory.Objects.Single().ID==expected[n] && p.Inventory.Objects.Single().Bound,"class-based refinery survives production reload");
            }
        }
        nestedRoll=0;
        foreach(uint id in new uint[]{720049,720731,729626}){
            var p=BallPlayer(town);var source=NestedPack(p,id);while(p.Inventory.Count<40)Pack(p,1000000,1);ItemHandler.UseItem(source,p);
            Check(!Fenced(p) && NestedReceipts(p)==1 && p.Inventory.Count==40 && !p.Inventory.ContainsUID(source.UID),"singleton uses its released bag slot: "+id);
            p=BallPlayer(town);source=NestedPack(p,id,2);while(p.Inventory.Count<40)Pack(p,1000000,1);nestedDraws=0;ItemHandler.UseItem(source,p);
            Check(!Fenced(p) && NestedReceipts(p)==0 && source.StackSize==2 && nestedDraws==0,"full bag preserves a stacked pack without drawing: "+id);
            var filler=p.Inventory.Objects.First(i=>i.ID==1000000);DisRemove(p,filler);ItemHandler.UseItem(source,p);
            Check(!Fenced(p) && NestedReceipts(p)==1 && source.StackSize==1 && p.Inventory.Count==40,"one free slot allows one stacked prize: "+id);
            p=BallPlayer(town);source=NestedPack(p,id,0);Parallel.For(0,12,i=>ItemHandler.UseItem(source,p));
            Check(!Fenced(p) && NestedReceipts(p)==1 && p.Inventory.Count==1 && !p.Inventory.ContainsUID(source.UID),"legacy zero-stack singleton and parallel duplicate use grant once: "+id);
        }
        var race=BallPlayer(town);var stack=NestedPack(race,720049,3);
        Parallel.For(0,24,i=>{if(i%3==0)EntityTable.SaveEntity(race);else ItemHandler.UseItem(stack,race);});
        Check(!Fenced(race) && NestedReceipts(race)==1 && stack.StackSize==2,"simultaneous stacked requests and autosave consume one intended unit");
        nestedNow=nestedNow.AddSeconds(1);ItemHandler.UseItem(stack,race);nestedNow=nestedNow.AddSeconds(1);ItemHandler.UseItem(stack,race);
        Check(!Fenced(race) && NestedReceipts(race)==3 && race.Inventory.Count==3 && !race.Inventory.ContainsUID(stack.UID),"later deliberate requests consume remaining units exactly once");
        foreach(string fault in new[]{"delete","stack","gift","receipt","random"}){
            var p=BallPlayer(town);var source=NestedPack(p,720049,(ushort)(fault=="stack"?2:1));int before=source.StackSize;
            string table=fault=="receipt"?"revora_epk_nested_openings":"items",verb=fault=="delete"?"DELETE":fault=="stack"?"UPDATE":"INSERT";
            if(fault!="random")Query("CREATE FUNCTION suppress_nested() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_nested BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_nested()");
            else nestedRoll=4;
            try{ItemHandler.UseItem(source,p);}finally{nestedRoll=0;if(fault!="random")Query("DROP TRIGGER suppress_nested ON "+table+";DROP FUNCTION suppress_nested()");}
            Check(Fenced(p) && NestedReceipts(p)==0 && p.Inventory.Count==1 && source.StackSize==before && Scalar("SELECT stacksize FROM items WHERE uid=@uid",source.UID)==before,"failed "+fault+" rolls back source, gift and receipt together");
            p=ReloadBall(p);ItemHandler.UseItem(p.Inventory.Objects.Single(),p);Check(!Fenced(p) && NestedReceipts(p)==1 && p.Inventory.Objects.Any(i=>i.ID==300000),"normal reconnect retries complete prize after "+fault);
        }
        foreach(string stale in new[]{"owner","type","stack","bound","locked","suspicious","wardrobe","days","class","hidden-bag","missing-bag"}){
            var p=BallPlayer(town);var source=NestedPack(p,729626);nestedDraws=0;
            if(stale=="class")Query("UPDATE entities SET class=45 WHERE uid=@uid","uid",(long)p.Entity.UID);
            else if(stale=="hidden-bag"){var hidden=Pack(p,1000000,1);p.Inventory.Remove(hidden.UID,Enums.ItemUse.None,false);}
            else if(stale=="missing-bag"){var missing=Pack(p,1000000,1);Query("DELETE FROM items WHERE uid=@uid","uid",(long)missing.UID);}
            else {string update=stale=="owner"?"entityid=1":stale=="type"?"id=720731":stale=="stack"?"stacksize=2":stale=="bound"?"bound=1":stale=="locked"?"locked=1":stale=="suspicious"?"suspicious=1":stale=="wardrobe"?"inwardrobe=1":"days='1'";Query("UPDATE items SET "+update+" WHERE uid=@uid","uid",(long)source.UID);}
            ItemHandler.UseItem(source,p);Check(Fenced(p) && NestedReceipts(p)==0 && p.Inventory.ContainsUID(source.UID) && nestedDraws==0,"stale "+stale+" is rejected before drawing or consuming a reward");
        }
        foreach(string invalid in new[]{"dead","trade","fake","unloaded","replaced","locked","suspicious","warehouse","position","expired","unknown-class","forged"}){
            var p=BallPlayer(town);var source=NestedPack(p,729626);nestedDraws=0;
            if(invalid=="dead")p.Entity.Hitpoints=0;if(invalid=="trade")p.Trade.InTrade=true;if(invalid=="fake")p.Fake=true;if(invalid=="unloaded")p.Entity.FullyLoaded=false;
            if(invalid=="replaced")Kernel.GamePool.Remove(p.Entity.UID);if(invalid=="locked")source.Lock=1;if(invalid=="suspicious")source.Suspicious=true;
            if(invalid=="warehouse")source.Warehouse=10;if(invalid=="position")source.Position=4;if(invalid=="expired"){source.Days=1;source.DayStamp=nestedNow.ToLocalTime().AddDays(-2);}
            if(invalid=="unknown-class")p.Entity.Class=30;
            ItemHandler.UseItem(invalid=="forged"?new ConquerItem(true){UID=source.UID,ID=source.ID}:source,p);
            Check(!Fenced(p) && NestedReceipts(p)==0 && p.Inventory.ContainsUID(source.UID) && nestedDraws==0,"ineligible "+invalid+" cannot spend or draw a prize");
        }
        foreach(string timing in new[]{"valid","expired","stale-stamp"}){
            var timed=BallPlayer(town);var source=NestedPack(timed,720049);source.Days=1;source.DayStamp=nestedNow.ToLocalTime();
            Query("UPDATE items SET days='1',daystamp=@stamp WHERE uid=@uid","stamp",source.DayStamp.ToBinary(),"uid",(long)source.UID);
            if(timing=="expired")nestedNow=nestedNow.AddDays(1);if(timing=="stale-stamp")source.DayStamp=source.DayStamp.AddHours(1);
            nestedDraws=0;ItemHandler.UseItem(source,timed);
            Check(timing=="valid"?!Fenced(timed) && NestedReceipts(timed)==1 && timed.Inventory.Objects.Single().ID==300000:
                NestedReceipts(timed)==0 && timed.Inventory.ContainsUID(source.UID) && nestedDraws==0 && Fenced(timed)==(timing=="stale-stamp"),"timed source pack respects its expiry and durable timestamp: "+timing);
            if(timing=="expired")nestedNow=nestedNow.AddDays(-1);
        }
        var chain=BallPlayer(town);var champion=NestedPack(chain,720729,1,true);ItemHandler.UseItem(champion,chain);chain=ReloadBall(chain);
        foreach(var nested in chain.Inventory.Objects.Where(i=>i.ID==720049 || i.ID==720731 || i.ID==729626).ToArray())ItemHandler.UseItem(nested,chain);
        chain=ReloadBall(chain);Check(!Fenced(chain) && PrizeReceipts(chain)==1 && NestedReceipts(chain)==11 && chain.Inventory.Count==23 && chain.Entity.ConquerPoints==5000000
            && chain.Inventory.Objects.All(i=>i.Bound) && chain.Inventory.Objects.Count(i=>i.ID==724409)==5,"champion bundle opens all eleven nested prizes and reloads with its purse and binding intact");
        var weapon=NestedPack(chain,410009,1,true);var material=chain.Inventory.Objects.First(i=>i.ID==724409);Refine(chain,weapon,material);chain=ReloadBall(chain);
        Check(!Fenced(chain) && !chain.Inventory.ContainsUID(material.UID) && chain.Inventory.Objects.Single(i=>i.UID==weapon.UID).RefineStats.Level==5,
            "nested refinery reward can refine an owned weapon and survive production reload");
        Check(Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid",chain.Entity.UID)==0,"opening supplied prize packs does not invent a tournament placement or honor");
    }
}
