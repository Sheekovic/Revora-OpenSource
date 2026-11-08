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
    static readonly Type ExpPotions=typeof(Entity).Assembly.GetType("Revora.Game.Features.ExperiencePotions");
    static DateTime potionNow=DateTime.UtcNow;
    static long Potions(GameState p){return Scalar("SELECT count(*) FROM revora_exp_potion_uses WHERE uid=@uid",p.Entity.UID);}
    static void Drink(GameState p,ConquerItem item){ItemUsage.Process(PrayerPacket(item.UID),p);}
    static void PotionTick(GameState p,Time32 now){Call(typeof(Entity),p.Entity,"TickDoubleExperience",now);}
    static void VerifyExperiencePotions(string runtime,Map town){
        VerifyFirstRebirth(runtime,town);Call(ExpPotions,null,"Initialize");Call(ExpPotions,null,"Initialize");
        ExpPotions.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>potionNow));
        foreach(uint type in new uint[]{722700,723017})foreach(bool bound in new[]{false,true})foreach(ushort stack in new ushort[]{0,1,2}){
            var p=BallPlayer(town);var item=NestedPack(p,type,stack,bound);ushort seconds=type==722700?(ushort)600:(ushort)3600;
            Parallel.For(0,24,n=>{if(n%3==0)EntityTable.SaveEntity(p);else Drink(p,item);});
            Check(!Fenced(p) && Potions(p)==1 && p.Entity.DoubleExperienceTime==seconds && p.Entity.Experience==5000
                && (stack>1?item.StackSize==1:!p.Inventory.ContainsUID(item.UID)),"ordinary potion packet/autosave commits one full duration and one source unit: "+type+"/"+bound+"/"+stack);
            ItemHandler.UseItem(item,p);p=ReloadBall(p);
            Check(!Fenced(p) && Potions(p)==1 && p.Entity.DoubleExperienceTime==seconds && Scalar("SELECT doubleexptime FROM entities WHERE uid=@uid",p.Entity.UID)==seconds,
                "production reload and direct replay retain the exact potion duration");
            if(stack>1){item=p.Inventory.Objects.Single();Drink(p,item);Check(Potions(p)==1 && item.StackSize==1,"active bonus preserves a remaining potion instead of stacking or shortening it");
                p.Entity.DoubleExperienceTime=1;PotionTick(p,p.Entity.DoubleExpStamp.AddMilliseconds(1001));Drink(p,item);
                Check(!Fenced(p) && Potions(p)==2 && p.Entity.DoubleExperienceTime==seconds && !p.Inventory.ContainsUID(item.UID),"remaining potion becomes usable only after the previous bonus expires");}
        }
        var race=BallPlayer(town);var supply=Pack(race,722700,2);Drink(race,supply);var tick=race.Entity.DoubleExpStamp.AddMilliseconds(1001);
        Parallel.For(0,48,n=>{if(n%3==0)PotionTick(race,tick);else if(n%3==1)EntityTable.SaveEntity(race);else Drink(race,supply);});
        Check(!Fenced(race) && Potions(race)==1 && race.Entity.DoubleExperienceTime==599 && supply.StackSize==1,"same-second countdown, autosave and repeated potion use serialize without lost time or extra consumption");
        Check(EntityTable.SaveEntity(race),"countdown checkpoint saves against its last durable preimage");race=ReloadBall(race);
        Check(race.Entity.DoubleExperienceTime==599,"production reconnect preserves the checkpointed remaining seconds");
        Call(typeof(Entity),race.Entity,"RefreshDoubleExperience");Check(Scalar("SELECT doubleexptime FROM entities WHERE uid=@uid",race.Entity.UID)==599,"login timer refresh projects without writing or extending the duration");
        var hunt=BallPlayer(town);ulong before=hunt.Entity.Experience;hunt.IncreaseExperience(10000,true);ulong ordinary=hunt.Entity.Experience-before;
        supply=Pack(hunt,723017,1);Drink(hunt,supply);before=hunt.Entity.Experience;hunt.IncreaseExperience(10000,true);
        Check(!Fenced(hunt) && hunt.Entity.Experience-before==ordinary*Config.DoubleExperienceMultiplier,"potion timer activates the production kill EXP multiplier");
        before=hunt.Entity.Experience;hunt.IncreaseExperience(10000,false);Check(hunt.Entity.Experience-before==10000,"potion does not multiply non-kill quest/consumable EXP");
        hunt.Entity.DoubleExperienceTime=1;PotionTick(hunt,hunt.Entity.DoubleExpStamp.AddMilliseconds(1001));before=hunt.Entity.Experience;hunt.IncreaseExperience(10000,true);
        Check(hunt.Entity.DoubleExperienceTime==0 && hunt.Entity.Experience-before==ordinary,"expired potion returns production kill awards to the original multiplier");
        var full=BallPlayer(town);supply=Pack(full,723017,1);while(full.Inventory.Count<40)Pack(full,1000000,1);Drink(full,supply);
        Check(!Fenced(full) && Potions(full)==1 && full.Inventory.Count==39,"potion use needs no empty inventory slot");
        var longBonus=BallPlayer(town);longBonus.Entity.DoubleExperienceTime=7200;supply=Pack(longBonus,722700,1);Drink(longBonus,supply);
        Check(!Fenced(longBonus) && Potions(longBonus)==0 && longBonus.Entity.DoubleExperienceTime==7200 && longBonus.Inventory.ContainsUID(supply.UID),"short potion never overwrites a longer active bonus");
        foreach(string fault in new[]{"source","stack","timer","receipt"}){
            var p=BallPlayer(town);var item=Pack(p,723017,(ushort)(fault=="stack"?2:1));string table=fault=="timer"?"entities":fault=="receipt"?"revora_exp_potion_uses":"items";
            string verb=fault=="source"?"DELETE":fault=="receipt"?"INSERT":"UPDATE";
            Query("CREATE FUNCTION suppress_potion() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_potion BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_potion()");Drink(p,item);
            Check(Fenced(p) && Potions(p)==0 && p.Entity.DoubleExperienceTime==0 && p.Inventory.ContainsUID(item.UID) && SavedRune(item,"stacksize")==item.StackSize
                && Scalar("SELECT doubleexptime FROM entities WHERE uid=@uid",p.Entity.UID)==0,"suppressed "+fault+" preserves potion, timer and receipt together");
            Query("DROP TRIGGER suppress_potion ON "+table+";DROP FUNCTION suppress_potion()");p=ReloadBall(p);Drink(p,p.Inventory.Objects.Single());
            Check(!Fenced(p) && Potions(p)==1 && p.Entity.DoubleExperienceTime==3600,"production reload retries the full effect after "+fault+" rollback");
        }
        var stale=BallPlayer(town);supply=Pack(stale,723017,1);Query("UPDATE entities SET doubleexptime=600 WHERE uid=@uid","uid",(long)stale.Entity.UID);Drink(stale,supply);
        Check(Fenced(stale) && Potions(stale)==0 && stale.Inventory.ContainsUID(supply.UID) && Scalar("SELECT doubleexptime FROM entities WHERE uid=@uid",stale.Entity.UID)==600,
            "stale zero timer cannot overwrite a newer durable bonus or consume a potion");
        stale=ReloadBall(stale);supply=stale.Inventory.Objects.Single();Drink(stale,supply);Check(!Fenced(stale) && Potions(stale)==0 && stale.Entity.DoubleExperienceTime==600,"reload exposes existing bonus and preserves potion");
        var save=BallPlayer(town);save.Entity.DoubleExperienceTime=600;PotionTick(save,save.Entity.DoubleExpStamp.AddMilliseconds(1001));
        Query("UPDATE entities SET doubleexptime=900 WHERE uid=@uid","uid",(long)save.Entity.UID);
        Check(!EntityTable.SaveEntity(save) && Fenced(save) && Scalar("SELECT doubleexptime FROM entities WHERE uid=@uid",save.Entity.UID)==900,"stale autosave cannot overwrite newer durable EXP time with a pending countdown");
        save=ReloadBall(save);Check(save.Entity.DoubleExperienceTime==900 && EntityTable.SaveEntity(save),"reload restores timer ownership and permits subsequent saves");
        Query("UPDATE entities SET doubleexptime=1000 WHERE uid=@uid","uid",(long)save.Entity.UID);bool rejected=false;
        try{save.Entity.DoubleExperienceTime=1200;}catch(InvalidOperationException){rejected=true;}
        Check(rejected && Fenced(save) && save.Entity.DoubleExperienceTime==900 && Scalar("SELECT doubleexptime FROM entities WHERE uid=@uid",save.Entity.UID)==1000,"legacy immediate setter also honors the durable timer preimage");
        var nullable=BallPlayer(town);Query("UPDATE entities SET doubleexptime=NULL WHERE uid=@uid","uid",(long)nullable.Entity.UID);nullable=ReloadBall(nullable);supply=Pack(nullable,722700,1);Drink(nullable,supply);
        Check(!Fenced(nullable) && Potions(nullable)==1 && nullable.Entity.DoubleExperienceTime==600,"nullable historical timer loads as zero and consumes safely");
        foreach(string guard in new[]{"dead","fake","trade","locked","suspicious","wardrobe","expired","substituted","stale-item","stale-bag","replaced"}){
            var p=BallPlayer(town);var item=Pack(p,723017,1);
            if(guard=="dead")p.Entity.Hitpoints=0;if(guard=="fake")p.Fake=true;if(guard=="trade")p.Trade.InTrade=true;if(guard=="locked")item.Lock=1;
            if(guard=="suspicious")item.Suspicious=true;if(guard=="wardrobe")item.InWardrobe=true;if(guard=="expired"){item.Days=1;item.DayStamp=potionNow.ToLocalTime().AddDays(-2);}
            if(guard=="stale-item")Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)item.UID);
            if(guard=="stale-bag"){var extra=Pack(p,1000000,1);Query("DELETE FROM items WHERE uid=@uid","uid",(long)extra.UID);}
            if(guard=="replaced")ReloadBall(p);
            if(guard=="substituted")ItemHandler.UseItem(new ConquerItem(true){ID=item.ID,UID=item.UID},p);else Drink(p,item);
            Check(Potions(p)==0 && p.Entity.DoubleExperienceTime==0 && p.Inventory.ContainsUID(item.UID),"ineligible potion leaves source and effect unchanged: "+guard);
            if(guard.StartsWith("stale-"))Check(Fenced(p),"stale potion ownership requires reload: "+guard);
        }
        foreach(uint type in new uint[]{722700,723017}){
            var p=BallPlayer(town);var item=Pack(p,type,1);
            foreach(string bad in new[]{"short","long","length","type","seal"}){
                byte[] frame=bad=="short"?new byte[31]:bad=="long"?new byte[129]:PrayerPacket(item.UID);
                Writer.WriteUInt16((ushort)(frame.Length-8),0,frame);Writer.WriteUInt16(1009,2,frame);Writer.WriteUInt32(item.UID,8,frame);Writer.WriteUInt16(4,20,frame);Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);
                if(bad=="length")Writer.WriteUInt16(1,0,frame);if(bad=="type")Writer.WriteUInt16(1008,2,frame);if(bad=="seal")frame[frame.Length-1]^=1;
                ItemUsage.Process(frame,p);Check(!Fenced(p) && Potions(p)==0 && p.Entity.DoubleExperienceTime==0,"bounded potion use rejects "+bad+" frame: "+type);
            }Drink(p,item);Check(!Fenced(p) && Potions(p)==1,"ordinary potion works after malformed frames: "+type);
        }
        VerifyTournamentExpPacks(town);
    }
    static readonly Type ExpPacks=typeof(Entity).Assembly.GetType("Revora.Game.Features.TournamentExpPacks");
    static long PackUses(GameState p){return Scalar("SELECT count(*) FROM revora_exp_pack_uses WHERE uid=@uid",p.Entity.UID);}
    static ushort PackSeconds(uint type){return type==728201?(ushort)7200:(ushort)3600;}
    static void VerifyTournamentExpPacks(Map town){
        Call(ExpPacks,null,"Initialize");Call(ExpPacks,null,"Initialize");
        ExpPacks.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>potionNow));
        foreach(uint type in new uint[]{720793,720981,728201})foreach(bool bound in new[]{false,true})foreach(ushort stack in new ushort[]{0,1,2}){
            var p=BallPlayer(town);var item=NestedPack(p,type,stack,bound);ushort seconds=PackSeconds(type);
            Parallel.For(0,24,n=>{if(n%3==0)EntityTable.SaveEntity(p);else Drink(p,item);});
            Check(!Fenced(p) && PackUses(p)==1 && p.Entity.DoubleExperienceTime==seconds && p.Entity.Experience==5000
                && (stack>1?item.StackSize==1:!p.Inventory.ContainsUID(item.UID)),"ordinary EXP pack packet/autosave commits one full duration and one source unit: "+type+"/"+bound+"/"+stack);
            ItemHandler.UseItem(item,p);p=ReloadBall(p);
            Check(!Fenced(p) && PackUses(p)==1 && p.Entity.DoubleExperienceTime==seconds && Scalar("SELECT doubleexptime FROM entities WHERE uid=@uid",p.Entity.UID)==seconds,
                "production reload and direct replay retain the exact EXP pack duration: "+type);
            if(stack>1){item=p.Inventory.Objects.Single();Drink(p,item);Check(PackUses(p)==1 && item.StackSize==1,"active bonus preserves a remaining EXP pack instead of stacking or shortening it: "+type);
                p.Entity.DoubleExperienceTime=1;PotionTick(p,p.Entity.DoubleExpStamp.AddMilliseconds(1001));Drink(p,item);
                Check(!Fenced(p) && PackUses(p)==2 && p.Entity.DoubleExperienceTime==seconds && !p.Inventory.ContainsUID(item.UID),"remaining EXP pack becomes usable only after the previous bonus expires: "+type);}
        }
        var hunt=BallPlayer(town);ulong before=hunt.Entity.Experience;hunt.IncreaseExperience(10000,true);ulong ordinary=hunt.Entity.Experience-before;
        var supply=Pack(hunt,720793,1);Drink(hunt,supply);before=hunt.Entity.Experience;hunt.IncreaseExperience(10000,true);
        Check(!Fenced(hunt) && hunt.Entity.Experience-before==ordinary*Config.DoubleExperienceMultiplier,"tournament EXP pack activates the production kill EXP multiplier");
        before=hunt.Entity.Experience;hunt.IncreaseExperience(10000,false);Check(hunt.Entity.Experience-before==10000,"tournament EXP pack does not multiply non-kill quest/consumable EXP");
        var longBonus=BallPlayer(town);longBonus.Entity.DoubleExperienceTime=10800;supply=Pack(longBonus,728201,1);Drink(longBonus,supply);
        Check(!Fenced(longBonus) && PackUses(longBonus)==0 && longBonus.Entity.DoubleExperienceTime==10800 && longBonus.Inventory.ContainsUID(supply.UID),"two-hour pack never overwrites a longer active bonus");
        foreach(string fault in new[]{"source","stack","timer","receipt"}){
            var p=BallPlayer(town);var item=Pack(p,720981,(ushort)(fault=="stack"?2:1));string table=fault=="timer"?"entities":fault=="receipt"?"revora_exp_pack_uses":"items";
            string verb=fault=="source"?"DELETE":fault=="receipt"?"INSERT":"UPDATE";
            Query("CREATE FUNCTION suppress_pack() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_pack BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_pack()");Drink(p,item);
            Check(Fenced(p) && PackUses(p)==0 && p.Entity.DoubleExperienceTime==0 && p.Inventory.ContainsUID(item.UID) && SavedRune(item,"stacksize")==item.StackSize
                && Scalar("SELECT doubleexptime FROM entities WHERE uid=@uid",p.Entity.UID)==0,"suppressed "+fault+" preserves EXP pack, timer and receipt together");
            Query("DROP TRIGGER suppress_pack ON "+table+";DROP FUNCTION suppress_pack()");p=ReloadBall(p);Drink(p,p.Inventory.Objects.Single());
            Check(!Fenced(p) && PackUses(p)==1 && p.Entity.DoubleExperienceTime==3600,"production reload retries the full EXP pack effect after "+fault+" rollback");
        }
        var stale=BallPlayer(town);supply=Pack(stale,728201,1);Query("UPDATE entities SET doubleexptime=600 WHERE uid=@uid","uid",(long)stale.Entity.UID);Drink(stale,supply);
        Check(Fenced(stale) && PackUses(stale)==0 && stale.Inventory.ContainsUID(supply.UID) && Scalar("SELECT doubleexptime FROM entities WHERE uid=@uid",stale.Entity.UID)==600,
            "stale zero timer cannot overwrite a newer durable bonus or consume an EXP pack");
        foreach(string guard in new[]{"dead","fake","trade","locked","suspicious","wardrobe","expired"}){
            var p=BallPlayer(town);var item=Pack(p,720793,1);
            if(guard=="dead")p.Entity.Hitpoints=0;if(guard=="fake")p.Fake=true;if(guard=="trade")p.Trade.InTrade=true;if(guard=="locked")item.Lock=1;
            if(guard=="suspicious")item.Suspicious=true;if(guard=="wardrobe")item.InWardrobe=true;if(guard=="expired"){item.Days=1;item.DayStamp=potionNow.ToLocalTime().AddDays(-2);}
            Drink(p,item);
            Check(PackUses(p)==0 && p.Entity.DoubleExperienceTime==0 && p.Inventory.ContainsUID(item.UID),"ineligible EXP pack leaves source and effect unchanged: "+guard);
        }
        foreach(uint type in new uint[]{720793,720981,728201}){
            var p=BallPlayer(town);var item=Pack(p,type,1);
            foreach(string bad in new[]{"short","long","length","type","seal"}){
                byte[] frame=bad=="short"?new byte[31]:bad=="long"?new byte[129]:PrayerPacket(item.UID);
                Writer.WriteUInt16((ushort)(frame.Length-8),0,frame);Writer.WriteUInt16(1009,2,frame);Writer.WriteUInt32(item.UID,8,frame);Writer.WriteUInt16(4,20,frame);Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);
                if(bad=="length")Writer.WriteUInt16(1,0,frame);if(bad=="type")Writer.WriteUInt16(1008,2,frame);if(bad=="seal")frame[frame.Length-1]^=1;
                ItemUsage.Process(frame,p);Check(!Fenced(p) && PackUses(p)==0 && p.Entity.DoubleExperienceTime==0,"bounded EXP pack use rejects "+bad+" frame: "+type);
            }Drink(p,item);Check(!Fenced(p) && PackUses(p)==1 && p.Entity.DoubleExperienceTime==PackSeconds(type),"ordinary EXP pack works after malformed frames: "+type);
        }
    }
    static readonly Type ExpProtection=typeof(Entity).Assembly.GetType("Revora.Game.Features.ExperienceProtection");
    static long ProtectionUses(GameState p){return Scalar("SELECT count(*) FROM revora_exp_protection_uses WHERE uid=@uid",p.Entity.UID);}
    static void ProtectionTick(GameState p,Time32 now){Call(typeof(Entity),p.Entity,"TickExpProtection",now);}
    static bool HasHourglass(GameState p){return p.Inventory.Objects.Any(i=>i.ID==3007985 || i.ID==3007989 || i.ID==3007992);}
    static void VerifyTimeDropIssuanceClosed(Map town){
        Check(Revora.Game.Features.TwinCityPlaza.IsClosed(900035),"Prizewell 900035 is a closed Twin City desk");
        bool plazaParks=false;Revora.Game.Features.TwinCityPlaza.Apply((id,map,x,y)=>{if(id==900035)plazaParks=true;});
        Check(!plazaParks,"Prizewell is jailed by the dedicated ParkNpc, not TwinCityPlaza.Apply");
        bool inRange=true;int max=0;var rng=new Random(1);for(int i=0;i<10000;i++){int n=rng.Next(1,5);if(n<1 || n>4)inRange=false;if(n>max)max=n;}
        Check(inRange && max==4,"closed Prizewell switch Random.Next(1,5) never reaches super hourglass 3007992");
        foreach(byte option in new byte[]{0,101,102}){
            var p=BallPlayer(town);p.ActiveNpc=900035;p.Entity.VIPLevel=6;p.TimeDropSlayerToday=false;p.Entity.TimeDropSlayer=0;
            var req=new NpcRequest(true);req.NpcID=900035;req.OptionID=option;
            Check(Revora.Game.Features.TwinCityPlaza.HandleClosed(p,req) && p.Inventory.Count==0 && !HasHourglass(p)
                && !p.TimeDropSlayerToday && p.Entity.TimeDropSlayer==0,
                "HandleClosed rejects Prizewell option "+option+" without issuing hourglasses");
            Npcs.GetDialog(req,p);
            Check(p.Inventory.Count==0 && !HasHourglass(p) && !p.TimeDropSlayerToday && p.Entity.TimeDropSlayer==0,
                "ordinary Twin City GetDialog cannot claim TimeDrop hourglasses for option "+option);
        }
        if(town.Npcs.ContainsKey(900035)){
            var existing=town.Npcs[900035];
            var visitor=BallPlayer(town);visitor.ActiveNpc=900035;visitor.Entity.VIPLevel=6;visitor.Entity.X=existing.X;visitor.Entity.Y=existing.Y;
            var claim=new NpcRequest(true);claim.NpcID=900035;claim.OptionID=102;Npcs.GetDialog(claim,visitor);
            Check(visitor.Inventory.Count==0 && !HasHourglass(visitor) && !visitor.TimeDropSlayerToday,"a loaded Prizewell actor still cannot issue hourglasses");
        }
    }
    static void VerifyExperienceProtection(Map town){
        Call(ExpProtection,null,"Initialize");Call(ExpProtection,null,"Initialize");
        ExpProtection.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>potionNow));
        foreach(bool bound in new[]{false,true})foreach(ushort stack in new ushort[]{0,1,2}){
            var p=BallPlayer(town);var item=NestedPack(p,3002559,stack,bound);
            Parallel.For(0,24,n=>{if(n%3==0)EntityTable.SaveEntity(p);else Drink(p,item);});
            Check(!Fenced(p) && ProtectionUses(p)==1 && p.Entity.ExpProtectionTime==3600 && p.Entity.Experience==5000
                && (stack>1?item.StackSize==1:!p.Inventory.ContainsUID(item.UID)),"ordinary EXP protection packet/autosave commits one hour and one source unit: "+bound+"/"+stack);
            ItemHandler.UseItem(item,p);p=ReloadBall(p);
            Check(!Fenced(p) && ProtectionUses(p)==1 && p.Entity.ExpProtectionTime==3600 && Scalar("SELECT protectiontime FROM entities WHERE uid=@uid",p.Entity.UID)==3600,
                "production reload and direct replay retain the exact EXP protection duration");
            if(stack>1){item=p.Inventory.Objects.Single();Drink(p,item);Check(ProtectionUses(p)==1 && item.StackSize==1,"active protection preserves a remaining pill instead of stacking or shortening it");
                p.Entity.ExpProtectionTime=1;ProtectionTick(p,p.Entity.ProtectionStamp.AddMilliseconds(1001));Drink(p,item);
                Check(!Fenced(p) && ProtectionUses(p)==2 && p.Entity.ExpProtectionTime==3600 && !p.Inventory.ContainsUID(item.UID),"remaining pill becomes usable only after the previous protection expires");}
        }
        var race=BallPlayer(town);var supply=Pack(race,3002559,2);Drink(race,supply);var tick=race.Entity.ProtectionStamp.AddMilliseconds(1001);
        Parallel.For(0,48,n=>{if(n%3==0)ProtectionTick(race,tick);else if(n%3==1)EntityTable.SaveEntity(race);else Drink(race,supply);});
        Check(!Fenced(race) && ProtectionUses(race)==1 && race.Entity.ExpProtectionTime==3599 && supply.StackSize==1,"same-second countdown, autosave and repeated protection use serialize without lost time or extra consumption");
        Check(EntityTable.SaveEntity(race),"countdown checkpoint saves against its last durable protection preimage");race=ReloadBall(race);
        Check(race.Entity.ExpProtectionTime==3599,"production reconnect preserves the checkpointed remaining protection seconds");
        Call(typeof(Entity),race.Entity,"RefreshExpProtection");Check(Scalar("SELECT protectiontime FROM entities WHERE uid=@uid",race.Entity.UID)==3599,"login timer refresh projects without writing or extending protection");
        var death=BallPlayer(town);supply=Pack(death,3002559,1);Drink(death,supply);ulong kept=death.Entity.Experience;death.Entity.Hitpoints=0;death.Entity.ExpProtectionTime-=600;
        Check(!Fenced(death) && death.Entity.ExpProtectionTime==3000 && kept==5000 && ProtectionUses(death)==1
            && Scalar("SELECT protectiontime FROM entities WHERE uid=@uid",death.Entity.UID)==3000,"PK death subtracts ten committed minutes from protection without dropping EXP");
        death=ReloadBall(death);Check(death.Entity.ExpProtectionTime==3000,"death-reduced protection survives production reload");
        var longBonus=BallPlayer(town);longBonus.Entity.ExpProtectionTime=7200;supply=Pack(longBonus,3002559,1);Drink(longBonus,supply);
        Check(!Fenced(longBonus) && ProtectionUses(longBonus)==0 && longBonus.Entity.ExpProtectionTime==7200 && longBonus.Inventory.ContainsUID(supply.UID),"protection pill never overwrites a longer active shield");
        var full=BallPlayer(town);supply=Pack(full,3002559,1);while(full.Inventory.Count<40)Pack(full,1000000,1);Drink(full,supply);
        Check(!Fenced(full) && ProtectionUses(full)==1 && full.Inventory.Count==39,"protection use needs no empty inventory slot");
        foreach(string fault in new[]{"source","stack","timer","receipt"}){
            var p=BallPlayer(town);var item=Pack(p,3002559,(ushort)(fault=="stack"?2:1));string table=fault=="timer"?"entities":fault=="receipt"?"revora_exp_protection_uses":"items";
            string verb=fault=="source"?"DELETE":fault=="receipt"?"INSERT":"UPDATE";
            Query("CREATE FUNCTION suppress_protection() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_protection BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_protection()");Drink(p,item);
            Check(Fenced(p) && ProtectionUses(p)==0 && p.Entity.ExpProtectionTime==0 && p.Inventory.ContainsUID(item.UID) && SavedRune(item,"stacksize")==item.StackSize
                && Scalar("SELECT protectiontime FROM entities WHERE uid=@uid",p.Entity.UID)==0,"suppressed "+fault+" preserves protection pill, timer and receipt together");
            Query("DROP TRIGGER suppress_protection ON "+table+";DROP FUNCTION suppress_protection()");p=ReloadBall(p);Drink(p,p.Inventory.Objects.Single());
            Check(!Fenced(p) && ProtectionUses(p)==1 && p.Entity.ExpProtectionTime==3600,"production reload retries the full protection effect after "+fault+" rollback");
        }
        var stale=BallPlayer(town);supply=Pack(stale,3002559,1);Query("UPDATE entities SET protectiontime=600 WHERE uid=@uid","uid",(long)stale.Entity.UID);Drink(stale,supply);
        Check(Fenced(stale) && ProtectionUses(stale)==0 && stale.Inventory.ContainsUID(supply.UID) && Scalar("SELECT protectiontime FROM entities WHERE uid=@uid",stale.Entity.UID)==600,
            "stale zero protection cannot overwrite a newer durable shield or consume a pill");
        stale=ReloadBall(stale);supply=stale.Inventory.Objects.Single();Drink(stale,supply);Check(!Fenced(stale) && ProtectionUses(stale)==0 && stale.Entity.ExpProtectionTime==600,"reload exposes existing protection and preserves the pill");
        var save=BallPlayer(town);save.Entity.ExpProtectionTime=600;ProtectionTick(save,save.Entity.ProtectionStamp.AddMilliseconds(1001));
        Query("UPDATE entities SET protectiontime=900 WHERE uid=@uid","uid",(long)save.Entity.UID);
        Check(!EntityTable.SaveEntity(save) && Fenced(save) && Scalar("SELECT protectiontime FROM entities WHERE uid=@uid",save.Entity.UID)==900,"stale autosave cannot overwrite newer durable protection with a pending countdown");
        save=ReloadBall(save);Check(save.Entity.ExpProtectionTime==900 && EntityTable.SaveEntity(save),"reload restores protection ownership and permits subsequent saves");
        Query("UPDATE entities SET protectiontime=1000 WHERE uid=@uid","uid",(long)save.Entity.UID);bool rejected=false;
        try{save.Entity.ExpProtectionTime=1200;}catch(InvalidOperationException){rejected=true;}
        Check(rejected && Fenced(save) && save.Entity.ExpProtectionTime==900 && Scalar("SELECT protectiontime FROM entities WHERE uid=@uid",save.Entity.UID)==1000,"legacy immediate setter also honors the durable protection preimage");
        var nullable=BallPlayer(town);Query("UPDATE entities SET protectiontime=NULL WHERE uid=@uid","uid",(long)nullable.Entity.UID);nullable=ReloadBall(nullable);supply=Pack(nullable,3002559,1);Drink(nullable,supply);
        Check(!Fenced(nullable) && ProtectionUses(nullable)==1 && nullable.Entity.ExpProtectionTime==3600,"nullable historical protection loads as zero and consumes safely");
        foreach(string guard in new[]{"dead","fake","trade","locked","suspicious","wardrobe","expired","substituted","stale-item","stale-bag","replaced"}){
            var p=BallPlayer(town);var item=Pack(p,3002559,1);
            if(guard=="dead")p.Entity.Hitpoints=0;if(guard=="fake")p.Fake=true;if(guard=="trade")p.Trade.InTrade=true;if(guard=="locked")item.Lock=1;
            if(guard=="suspicious")item.Suspicious=true;if(guard=="wardrobe")item.InWardrobe=true;if(guard=="expired"){item.Days=1;item.DayStamp=potionNow.ToLocalTime().AddDays(-2);}
            if(guard=="stale-item")Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)item.UID);
            if(guard=="stale-bag"){var extra=Pack(p,1000000,1);Query("DELETE FROM items WHERE uid=@uid","uid",(long)extra.UID);}
            if(guard=="replaced")ReloadBall(p);
            if(guard=="substituted")ItemHandler.UseItem(new ConquerItem(true){ID=item.ID,UID=item.UID},p);else Drink(p,item);
            Check(ProtectionUses(p)==0 && p.Entity.ExpProtectionTime==0 && p.Inventory.ContainsUID(item.UID),"ineligible protection leaves source and effect unchanged: "+guard);
            if(guard.StartsWith("stale-"))Check(Fenced(p),"stale protection ownership requires reload: "+guard);
        }
        var frames=BallPlayer(town);var pill=Pack(frames,3002559,1);
        foreach(string bad in new[]{"short","long","length","type","seal"}){
            byte[] frame=bad=="short"?new byte[31]:bad=="long"?new byte[129]:PrayerPacket(pill.UID);
            Writer.WriteUInt16((ushort)(frame.Length-8),0,frame);Writer.WriteUInt16(1009,2,frame);Writer.WriteUInt32(pill.UID,8,frame);Writer.WriteUInt16(4,20,frame);Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);
            if(bad=="length")Writer.WriteUInt16(1,0,frame);if(bad=="type")Writer.WriteUInt16(1008,2,frame);if(bad=="seal")frame[frame.Length-1]^=1;
            ItemUsage.Process(frame,frames);Check(!Fenced(frames) && ProtectionUses(frames)==0 && frames.Entity.ExpProtectionTime==0,"bounded protection use rejects "+bad+" frame");
        }Drink(frames,pill);Check(!Fenced(frames) && ProtectionUses(frames)==1 && frames.Entity.ExpProtectionTime==3600,"ordinary protection works after malformed frames");
    }
    static readonly Type ExpCombine=typeof(Entity).Assembly.GetType("Revora.Game.Features.ExpCareCombine");
    static long Combines(GameState p){return Scalar("SELECT count(*) FROM revora_exp_care_combines WHERE uid=@uid",p.Entity.UID);}
    static int ScrapCount(GameState p){return p.Inventory.Objects.Where(i=>i.ID==3002560).Sum(i=>i.StackSize);}
    static int BoundPills(GameState p){return p.Inventory.Objects.Count(i=>i.ID==3002559 && i.Bound);}
    static void Combine(GameState p,ConquerItem item){ItemUsage.Process(PrayerPacket(item.UID),p);}
    static void VerifyExpCareCombine(Map town){
        Call(ExpCombine,null,"Initialize");Call(ExpCombine,null,"Initialize");
        ExpCombine.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>potionNow));
        foreach(ushort shortStack in new ushort[]{0,1,9}){
            var p=BallPlayer(town);var scrap=Pack(p,3002560,shortStack);uint rvp=p.Entity.RevoraPoint;Combine(p,scrap);
            Check(!Fenced(p) && Combines(p)==0 && BoundPills(p)==0 && p.Entity.RevoraPoint==rvp && p.Inventory.ContainsUID(scrap.UID) && scrap.StackSize==shortStack,
                "undersized scrap stack awards nothing: "+shortStack);
        }
        foreach(bool bound in new[]{false,true})foreach(ushort stack in new ushort[]{10,11,20}){
            var p=BallPlayer(town);var scrap=NestedPack(p,3002560,stack,bound);uint rvp=p.Entity.RevoraPoint;
            Parallel.For(0,24,n=>{if(n%3==0)EntityTable.SaveEntity(p);else Combine(p,scrap);});
            int expected=stack>=20?2:1;int remain=stack-expected*10;
            Check(!Fenced(p) && Combines(p)==expected && BoundPills(p)==expected && p.Entity.RevoraPoint==rvp+3000u*(uint)expected
                && (remain==0?!p.Inventory.ContainsUID(scrap.UID):scrap.StackSize==remain)
                && p.Inventory.Objects.Where(i=>i.ID==3002559).All(i=>i.Bound && i.Days==0),
                "ordinary combine packet/autosave pays one bound pill and 3000 RVP per ten scraps: "+bound+"/"+stack);
            ItemHandler.UseItem(scrap,p);p=ReloadBall(p);
            Check(!Fenced(p) && Combines(p)==expected && BoundPills(p)==expected && Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",p.Entity.UID)==rvp+3000L*expected
                && ScrapCount(p)==remain,"production reload retains remainder, bound pills and committed RVP: "+stack);
        }
        var full=BallPlayer(town);var ten=Pack(full,3002560,10);while(full.Inventory.Count<40)Pack(full,1000000,1);uint fullRvp=full.Entity.RevoraPoint;Combine(full,ten);
        Check(!Fenced(full) && Combines(full)==1 && BoundPills(full)==1 && full.Inventory.Count==40 && !full.Inventory.ContainsUID(ten.UID)
            && full.Entity.RevoraPoint==fullRvp+3000,"exact ten-stack combine needs no extra slot in a full bag");
        var packed=BallPlayer(town);var extra=Pack(packed,3002560,15);while(packed.Inventory.Count<40)Pack(packed,1000000,1);uint packedRvp=packed.Entity.RevoraPoint;Combine(packed,extra);
        Check(!Fenced(packed) && Combines(packed)==0 && BoundPills(packed)==0 && extra.StackSize==15 && packed.Entity.RevoraPoint==packedRvp
            && packed.Inventory.Count==40,"remaining source after ten scraps requires one free bag slot");
        var room=BallPlayer(town);var keep=Pack(room,3002560,15);while(room.Inventory.Count<39)Pack(room,1000000,1);Combine(room,keep);
        Check(!Fenced(room) && Combines(room)==1 && keep.StackSize==5 && BoundPills(room)==1 && room.Inventory.Count==40,"one free slot accepts the bound pill and keeps the remainder");
        foreach(string fault in new[]{"source","stack","rvp","pill","receipt"}){
            var p=BallPlayer(town);var item=Pack(p,3002560,(ushort)(fault=="stack"?15:10));uint rvp=p.Entity.RevoraPoint;
            string table=fault=="rvp"?"entities":fault=="receipt"?"revora_exp_care_combines":"items";
            string verb=fault=="source"?"DELETE":fault=="receipt" || fault=="pill"?"INSERT":"UPDATE";
            Query("CREATE FUNCTION suppress_combine() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_combine BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_combine()");Combine(p,item);
            Check(Fenced(p) && Combines(p)==0 && BoundPills(p)==0 && p.Inventory.ContainsUID(item.UID) && SavedRune(item,"stacksize")==item.StackSize
                && p.Entity.RevoraPoint==rvp && Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",p.Entity.UID)==rvp,"suppressed "+fault+" preserves scraps, RVP and pill together");
            Query("DROP TRIGGER suppress_combine ON "+table+";DROP FUNCTION suppress_combine()");p=ReloadBall(p);Combine(p,p.Inventory.Objects.Single(i=>i.ID==3002560));
            Check(!Fenced(p) && Combines(p)==1 && BoundPills(p)==1 && p.Entity.RevoraPoint==rvp+3000,"production reload retries the full combine after "+fault+" rollback");
        }
        var stale=BallPlayer(town);var supply=Pack(stale,3002560,10);Query("UPDATE entities SET revorapoint=revorapoint+1 WHERE uid=@uid","uid",(long)stale.Entity.UID);uint before=stale.Entity.RevoraPoint;Combine(stale,supply);
        Check(Fenced(stale) && Combines(stale)==0 && stale.Inventory.ContainsUID(supply.UID) && Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",stale.Entity.UID)==before+1,
            "stale Revora Point preimage cannot pay or consume scraps");
        foreach(string guard in new[]{"dead","fake","trade","locked","suspicious","wardrobe","expired","substituted"}){
            var p=BallPlayer(town);var item=Pack(p,3002560,10);uint rvp=p.Entity.RevoraPoint;
            if(guard=="dead")p.Entity.Hitpoints=0;if(guard=="fake")p.Fake=true;if(guard=="trade")p.Trade.InTrade=true;if(guard=="locked")item.Lock=1;
            if(guard=="suspicious")item.Suspicious=true;if(guard=="wardrobe")item.InWardrobe=true;if(guard=="expired"){item.Days=1;item.DayStamp=potionNow.ToLocalTime().AddDays(-2);}
            if(guard=="substituted")ItemHandler.UseItem(new ConquerItem(true){ID=item.ID,UID=item.UID,StackSize=10},p);else Combine(p,item);
            Check(Combines(p)==0 && BoundPills(p)==0 && p.Entity.RevoraPoint==rvp && p.Inventory.ContainsUID(item.UID),"ineligible combine leaves source and reward unchanged: "+guard);
        }
        var overflow=BallPlayer(town);Query("UPDATE entities SET revorapoint=4294967295 WHERE uid=@uid","uid",(long)overflow.Entity.UID);overflow=ReloadBall(overflow);var overflowScrap=Pack(overflow,3002560,10);Combine(overflow,overflowScrap);
        Check(!Fenced(overflow) && Combines(overflow)==0 && overflow.Inventory.ContainsUID(overflowScrap.UID) && overflow.Entity.RevoraPoint==uint.MaxValue,"RVP overflow preserves every scrap");
        var nullable=BallPlayer(town);Query("UPDATE entities SET revorapoint=NULL WHERE uid=@uid","uid",(long)nullable.Entity.UID);nullable=ReloadBall(nullable);var nullScrap=Pack(nullable,3002560,10);Combine(nullable,nullScrap);
        Check(!Fenced(nullable) && Combines(nullable)==1 && nullable.Entity.RevoraPoint==3000 && BoundPills(nullable)==1,"nullable historical RVP loads as zero and combines safely");
        var frames=BallPlayer(town);var wire=Pack(frames,3002560,10);
        foreach(string bad in new[]{"short","long","length","type","seal"}){
            byte[] frame=bad=="short"?new byte[31]:bad=="long"?new byte[129]:PrayerPacket(wire.UID);
            Writer.WriteUInt16((ushort)(frame.Length-8),0,frame);Writer.WriteUInt16(1009,2,frame);Writer.WriteUInt32(wire.UID,8,frame);Writer.WriteUInt16(4,20,frame);Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);
            if(bad=="length")Writer.WriteUInt16(1,0,frame);if(bad=="type")Writer.WriteUInt16(1008,2,frame);if(bad=="seal")frame[frame.Length-1]^=1;
            ItemUsage.Process(frame,frames);Check(!Fenced(frames) && Combines(frames)==0 && ScrapCount(frames)==10,"bounded combine rejects "+bad+" frame");
        }Combine(frames,wire);Check(!Fenced(frames) && Combines(frames)==1 && BoundPills(frames)==1 && frames.Entity.RevoraPoint>=3000,"ordinary combine works after malformed frames");
    }
    static readonly Type TimeDrops=typeof(Entity).Assembly.GetType("Revora.Game.Features.TimeDropHourglasses");
    static long TimeDropUses(GameState p){return Scalar("SELECT count(*) FROM revora_timedrop_uses WHERE uid=@uid",p.Entity.UID);}
    static ushort HourglassSeconds(uint type){return type==3007992?(ushort)7200:type==3007989?(ushort)3600:(ushort)1800;}
    static void TimeDropTick(GameState p,Time32 now){Call(typeof(Entity),p.Entity,"TickTimeDropSlayer",now);Call(typeof(Entity),p.Entity,"TickDoubleExperience",now);}
    static void TimeDropKill(GameState p){
        var info=MonsterInformation.MonsterInformations.Values.First(m=>m.Name=="Pheasant").Copy();info.OwnItemRate=0;
        var monster=new Entity(EntityFlag.Monster,false){UID=p.Map.EntityUIDCounter.Next,MapID=p.Map.ID,X=p.Entity.X,Y=p.Entity.Y,Name="Pheasant",MonsterInfo=info};
        info.Owner=monster;bool cps=Config.KillConquerPointsEnabled,money=Config.MoneyDropEnabled;
        Config.KillConquerPointsEnabled=false;Config.MoneyDropEnabled=false;
        try{info.Drop(p.Entity);}finally{Config.KillConquerPointsEnabled=cps;Config.MoneyDropEnabled=money;}
    }
    static void VerifyTimeDropHourglasses(Map town){
        Call(TimeDrops,null,"Initialize");Call(TimeDrops,null,"Initialize");
        TimeDrops.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>potionNow));
        foreach(uint type in new uint[]{3007985,3007989,3007992})foreach(bool bound in new[]{false,true})foreach(ushort stack in new ushort[]{0,1,2}){
            var p=BallPlayer(town);var item=NestedPack(p,type,stack,bound);ushort seconds=HourglassSeconds(type);
            Parallel.For(0,24,n=>{if(n%3==0)EntityTable.SaveEntity(p);else Drink(p,item);});
            Check(!Fenced(p) && TimeDropUses(p)==1 && p.Entity.TimeDropSlayer==seconds && p.Entity.DoubleExperienceTime==seconds
                && (stack>1?item.StackSize==1:!p.Inventory.ContainsUID(item.UID)),"ordinary TimeDrop packet/autosave commits both timers and one source unit: "+type+"/"+bound+"/"+stack);
            ItemHandler.UseItem(item,p);p=ReloadBall(p);
            Check(!Fenced(p) && TimeDropUses(p)==1 && p.Entity.TimeDropSlayer==seconds && p.Entity.DoubleExperienceTime==seconds
                && Scalar("SELECT timedropslayer FROM entities WHERE uid=@uid",p.Entity.UID)==seconds && Scalar("SELECT doubleexptime FROM entities WHERE uid=@uid",p.Entity.UID)==seconds,
                "production reload retains both committed TimeDrop durations: "+type);
            if(stack>1){item=p.Inventory.Objects.Single();Drink(p,item);Check(TimeDropUses(p)==1 && item.StackSize==1,"active TimeDrop preserves a remaining hourglass: "+type);
                p.Entity.TimeDropSlayer=1;p.Entity.DoubleExperienceTime=1;TimeDropTick(p,p.Entity.TimeDropSlayerStamp.AddMilliseconds(1001));Drink(p,item);
                Check(!Fenced(p) && TimeDropUses(p)==2 && p.Entity.TimeDropSlayer==seconds && p.Entity.DoubleExperienceTime==seconds && !p.Inventory.ContainsUID(item.UID),"remaining hourglass becomes usable only after both timers expire: "+type);}
        }
        var race=BallPlayer(town);var supply=Pack(race,3007989,2);Drink(race,supply);var tick=race.Entity.TimeDropSlayerStamp.AddMilliseconds(1001);
        Parallel.For(0,48,n=>{if(n%3==0)TimeDropTick(race,tick);else if(n%3==1)EntityTable.SaveEntity(race);else Drink(race,supply);});
        Check(!Fenced(race) && TimeDropUses(race)==1 && race.Entity.TimeDropSlayer==3599 && race.Entity.DoubleExperienceTime==3599 && supply.StackSize==1,"same-second countdown, autosave and repeated TimeDrop use serialize without lost time");
        Check(EntityTable.SaveEntity(race),"countdown checkpoint saves against last durable TimeDrop preimages");race=ReloadBall(race);
        Check(race.Entity.TimeDropSlayer==3599 && race.Entity.DoubleExperienceTime==3599,"production reconnect preserves checkpointed TimeDrop seconds");
        Call(typeof(Entity),race.Entity,"RefreshTimeDropSlayer");Call(typeof(Entity),race.Entity,"RefreshDoubleExperience");
        Check(Scalar("SELECT timedropslayer FROM entities WHERE uid=@uid",race.Entity.UID)==3599 && Scalar("SELECT doubleexptime FROM entities WHERE uid=@uid",race.Entity.UID)==3599,"login refresh projects TimeDrop without writing or extending it");
        var hunt=BallPlayer(town);hunt.Entity.VIPLevel=4;uint rvp=hunt.Entity.RevoraPoint;TimeDropKill(hunt);uint idle=hunt.Entity.RevoraPoint-rvp;
        ulong before=hunt.Entity.Experience;hunt.IncreaseExperience(10000,true);ulong ordinary=hunt.Entity.Experience-before;
        supply=Pack(hunt,3007985,1);Drink(hunt,supply);rvp=hunt.Entity.RevoraPoint;TimeDropKill(hunt);
        Check(!Fenced(hunt) && hunt.Entity.TimeDropSlayer==1800 && hunt.Entity.RevoraPoint>rvp && hunt.Entity.RevoraPoint-rvp>idle,"ordinary monster kill observes the committed TimeDrop timer");
        before=hunt.Entity.Experience;hunt.IncreaseExperience(10000,true);
        Check(hunt.Entity.Experience-before==ordinary*Config.DoubleExperienceMultiplier,"TimeDrop hourglass activates the production kill EXP multiplier");
        before=hunt.Entity.Experience;hunt.IncreaseExperience(10000,false);Check(hunt.Entity.Experience-before==10000,"TimeDrop hourglass does not multiply non-kill EXP");
        var blocked=BallPlayer(town);blocked.Entity.TimeDropSlayer=900;supply=Pack(blocked,3007992,1);Drink(blocked,supply);
        Check(!Fenced(blocked) && TimeDropUses(blocked)==0 && blocked.Entity.TimeDropSlayer==900 && blocked.Entity.DoubleExperienceTime==0 && blocked.Inventory.ContainsUID(supply.UID),"active TimeDrop timer is not overwritten or shortened");
        blocked=BallPlayer(town);blocked.Entity.DoubleExperienceTime=900;supply=Pack(blocked,3007989,1);Drink(blocked,supply);
        Check(!Fenced(blocked) && TimeDropUses(blocked)==0 && blocked.Entity.DoubleExperienceTime==900 && blocked.Entity.TimeDropSlayer==0 && blocked.Inventory.ContainsUID(supply.UID),"active double-EXP timer is not overwritten or shortened");
        var full=BallPlayer(town);supply=Pack(full,3007985,1);while(full.Inventory.Count<40)Pack(full,1000000,1);Drink(full,supply);
        Check(!Fenced(full) && TimeDropUses(full)==1 && full.Inventory.Count==39,"TimeDrop use needs no empty inventory slot");
        foreach(string fault in new[]{"source","stack","timer","receipt"}){
            var p=BallPlayer(town);var item=Pack(p,3007989,(ushort)(fault=="stack"?2:1));string table=fault=="timer"?"entities":fault=="receipt"?"revora_timedrop_uses":"items";
            string verb=fault=="source"?"DELETE":fault=="receipt"?"INSERT":"UPDATE";
            Query("CREATE FUNCTION suppress_timedrop() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_timedrop BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_timedrop()");Drink(p,item);
            Check(Fenced(p) && TimeDropUses(p)==0 && p.Entity.TimeDropSlayer==0 && p.Entity.DoubleExperienceTime==0 && p.Inventory.ContainsUID(item.UID) && SavedRune(item,"stacksize")==item.StackSize
                && Scalar("SELECT timedropslayer FROM entities WHERE uid=@uid",p.Entity.UID)==0 && Scalar("SELECT doubleexptime FROM entities WHERE uid=@uid",p.Entity.UID)==0,"suppressed "+fault+" preserves hourglass and both timers together");
            Query("DROP TRIGGER suppress_timedrop ON "+table+";DROP FUNCTION suppress_timedrop()");p=ReloadBall(p);Drink(p,p.Inventory.Objects.Single());
            Check(!Fenced(p) && TimeDropUses(p)==1 && p.Entity.TimeDropSlayer==3600 && p.Entity.DoubleExperienceTime==3600,"production reload retries the full TimeDrop effect after "+fault+" rollback");
        }
        var stale=BallPlayer(town);supply=Pack(stale,3007992,1);Query("UPDATE entities SET timedropslayer=600 WHERE uid=@uid","uid",(long)stale.Entity.UID);Drink(stale,supply);
        Check(Fenced(stale) && TimeDropUses(stale)==0 && stale.Inventory.ContainsUID(supply.UID) && Scalar("SELECT timedropslayer FROM entities WHERE uid=@uid",stale.Entity.UID)==600,"stale TimeDrop preimage cannot overwrite a newer durable timer");
        stale=BallPlayer(town);supply=Pack(stale,3007985,1);Query("UPDATE entities SET doubleexptime=600 WHERE uid=@uid","uid",(long)stale.Entity.UID);Drink(stale,supply);
        Check(Fenced(stale) && TimeDropUses(stale)==0 && stale.Inventory.ContainsUID(supply.UID) && Scalar("SELECT doubleexptime FROM entities WHERE uid=@uid",stale.Entity.UID)==600,"stale double-EXP preimage cannot be overwritten by a TimeDrop hourglass");
        var nullable=BallPlayer(town);Query("UPDATE entities SET timedropslayer=NULL,doubleexptime=NULL WHERE uid=@uid","uid",(long)nullable.Entity.UID);nullable=ReloadBall(nullable);supply=Pack(nullable,3007985,1);Drink(nullable,supply);
        Check(!Fenced(nullable) && TimeDropUses(nullable)==1 && nullable.Entity.TimeDropSlayer==1800 && nullable.Entity.DoubleExperienceTime==1800,"nullable historical TimeDrop timers load as zero and consume safely");
        foreach(string guard in new[]{"dead","fake","trade","locked","suspicious","wardrobe","expired"}){
            var p=BallPlayer(town);var item=Pack(p,3007989,1);
            if(guard=="dead")p.Entity.Hitpoints=0;if(guard=="fake")p.Fake=true;if(guard=="trade")p.Trade.InTrade=true;if(guard=="locked")item.Lock=1;
            if(guard=="suspicious")item.Suspicious=true;if(guard=="wardrobe")item.InWardrobe=true;if(guard=="expired"){item.Days=1;item.DayStamp=potionNow.ToLocalTime().AddDays(-2);}
            Drink(p,item);
            Check(TimeDropUses(p)==0 && p.Entity.TimeDropSlayer==0 && p.Entity.DoubleExperienceTime==0 && p.Inventory.ContainsUID(item.UID),"ineligible TimeDrop leaves source and both timers unchanged: "+guard);
        }
        foreach(uint type in new uint[]{3007985,3007989,3007992}){
            var p=BallPlayer(town);var item=Pack(p,type,1);
            foreach(string bad in new[]{"short","long","length","type","seal"}){
                byte[] frame=bad=="short"?new byte[31]:bad=="long"?new byte[129]:PrayerPacket(item.UID);
                Writer.WriteUInt16((ushort)(frame.Length-8),0,frame);Writer.WriteUInt16(1009,2,frame);Writer.WriteUInt32(item.UID,8,frame);Writer.WriteUInt16(4,20,frame);Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);
                if(bad=="length")Writer.WriteUInt16(1,0,frame);if(bad=="type")Writer.WriteUInt16(1008,2,frame);if(bad=="seal")frame[frame.Length-1]^=1;
                ItemUsage.Process(frame,p);Check(!Fenced(p) && TimeDropUses(p)==0 && p.Entity.TimeDropSlayer==0,"bounded TimeDrop use rejects "+bad+" frame: "+type);
            }Drink(p,item);Check(!Fenced(p) && TimeDropUses(p)==1 && p.Entity.TimeDropSlayer==HourglassSeconds(type) && p.Entity.DoubleExperienceTime==HourglassSeconds(type),"ordinary TimeDrop works after malformed frames: "+type);
        }
    }
}
