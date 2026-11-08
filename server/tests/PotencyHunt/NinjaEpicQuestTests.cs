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
    static readonly Type NinjaQuest=typeof(Entity).Assembly.GetType("Revora.Game.Features.NinjaEpicQuest");
    static readonly Type NinjaStore=typeof(Entity).Assembly.GetType("Revora.Game.Features.NinjaEpicStore");
    static DateTime ninjaNow=new DateTime(2032,4,1,0,0,0,DateTimeKind.Utc);
    static void NinjaRoll(int roll){NinjaStore.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>roll));}
    static long EpicReceipts(GameState p){return Scalar("SELECT count(*) FROM revora_ninja_epic_operations WHERE uid=@uid",p.Entity.UID);}
    static long EpicAmount(GameState p,uint type){return p.Inventory.Objects.Where(i=>i.ID==type).Sum(i=>Math.Max(1,(int)i.StackSize));}
    static GameState EpicPlayer(Map job) {
        var p=PromotionPlayer(job,55,130,4720);p.Entity.Reborn=2;p.Entity.FirstRebornClass=55;p.Entity.SecondRebornClass=55;
        p.Entity.FirstRebornLevel=130;p.Entity.SecondRebornLevel=130;p.Entity.ConquerPoints=1000;p.Entity.X=38;p.Entity.Y=42;
        EntityTable.SaveEntity(p);return p;
    }
    static void EpicNpc(GameState p,uint npc,byte option) {
        if(Kernel.GetDistance(p.Entity.X,p.Entity.Y,p.Map.Npcs[npc].X,p.Map.Npcs[npc].Y)>6)AdventureNear(p,npc);
        AbyssOpen(p,npc);SoulWire(p,option,npc);
    }
    static void EpicEnter(GameState p){EpicNpc(p,4720,86);if(p.Entity.MapID!=3849 || Fenced(p))throw new Exception("Ninja quest entry failed.");}
    static void EpicConfirm(GameState p){SoulWire(p,20,p.ActiveNpc);}
    static void EpicSeal(GameState p){NinjaRoll(0);EpicNpc(p,199464,1);EpicConfirm(p);}
    static void EpicJade(GameState p){EpicSeal(p);EpicNpc(p,199464,3);EpicNpc(p,199465,1);EpicConfirm(p);}
    static void EpicWheel(GameState p,int roll){EpicJade(p);EpicNpc(p,199465,3);NinjaRoll(roll);EpicNpc(p,199466,1);EpicConfirm(p);}
    static void VerifyNinjaEpicQuest(string runtime) {
        VerifyNinjaTraining(runtime);VerifyNinjaEpicRules(runtime);PrepareNinjaFlowers(runtime);var job=Kernel.Maps[1004];
        Call(NinjaQuest,null,"Initialize");Call(NinjaQuest,null,"Initialize");
        NinjaStore.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>ninjaNow));
        var p=EpicPlayer(job);EpicEnter(p);
        Check(!Fenced(p) && p.Account.State==AccountTable.AccountState.Player && p.Entity.X==29 && p.Entity.Y==28
            && p.Map.Floor[p.Entity.X,p.Entity.Y,MapObjectType.Player],"ordinary Ninja enters on an actual walkable tile");
        NinjaRoll(99);EpicNpc(p,199464,1);EpicConfirm(p);
        Check(p.Entity.ConquerPoints==973 && EpicReceipts(p)==1 && EpicAmount(p,3004462)==0,"failed first trial charges exactly once and does not invent a seal");
        Parallel.For(0,12,_=>SoulWire(p,20,199464));Check(p.Entity.ConquerPoints==973 && EpicReceipts(p)==1,"concurrent confirmation replay cannot repeat a paid trial");
        p.Entity.VIPLevel=3;NinjaStore.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>0));EpicNpc(p,199464,2);EpicConfirm(p);
        Check(!Fenced(p) && p.Entity.ConquerPoints==703 && EpicAmount(p,3004462)==10 && EpicReceipts(p)==2,"VIP bulk payment commits ten independent successful seals and exactly 270 CPs");
        foreach(var i in p.Inventory.Objects.Where(i=>i.ID==3004462).ToArray())DisRemove(p,i);
        var red=NestedPack(p,722732,4,true);var white=NestedPack(p,722736,4,false);NinjaRoll(0);EpicNpc(p,199464,4);EpicConfirm(p);
        Check(!Fenced(p) && EpicAmount(p,3004462)==1 && p.Inventory.Objects.Single(i=>i.ID==3004462).Bound && !p.Inventory.ContainsUID(red.UID)
            && !p.Inventory.ContainsUID(white.UID) && p.Entity.ConquerPoints==703,"flower alternative consumes four of each and carries mixed binding into its seal");
        EpicNpc(p,199464,3);Check(p.Entity.MapID==3850 && p.Map.Floor[p.Entity.X,p.Entity.Y,MapObjectType.Player],"seal holder enters the actual reachable second trial");
        NinjaRoll(99);EpicNpc(p,199465,1);EpicConfirm(p);
        Check(!Fenced(p) && p.Entity.MapID==3849 && EpicAmount(p,3004462)==0 && EpicAmount(p,3004465)==1
            && p.Inventory.Objects.Single(i=>i.ID==3004465).Bound,"Elemental failure consumes its seal, awards one bound Stardust and returns safely");
        var learned=EpicPlayer(job);EpicEnter(learned);
        foreach(byte scroll in new byte[]{1,2,3}) {
            EpicWheel(learned,99);EpicWheel(learned,99);
            Check(!Fenced(learned) && EpicAmount(learned,3004465)==10 && learned.Entity.MapID==3849,"two actual Wheel failures earn ten Stardust");
            EpicNpc(learned,199546,scroll);EpicConfirm(learned);uint item=3004458u+scroll;
            Check(!Fenced(learned) && EpicAmount(learned,item)==1 && EpicAmount(learned,3004465)==0,"earned Stardust buys the selected scroll: "+item);
            SoulWire(learned,254,199546);Check(learned.Entity.MapID==1004 && learned.Map.Floor[learned.Entity.X,learned.Entity.Y,MapObjectType.Player],"trial return reaches a legal Job Center tile");
            byte learn=scroll==1?(byte)81:scroll==2?(byte)80:(byte)83;ushort spell=scroll==1?(ushort)12090:scroll==2?(ushort)12070:(ushort)12110;
            EpicNpc(learned,4720,learn);
            Check(!Fenced(learned) && learned.Spells.ContainsKey(spell) && EpicAmount(learned,item)==0,"trial-earned scroll reaches the ordinary durable instructor: "+spell);
            EpicEnter(learned);
        }
        EpicWheel(learned,0);Check(!Fenced(learned) && EpicAmount(learned,3004464)==1 && learned.Entity.MapID==3851,"complete paid trial graph earns Life's Eye");
        var gear=RebirthGear(learned,4,601439);gear.Plus=6;gear.Bless=3;gear.Enchant=100;gear.SocketOne=Enums.Gem.SuperDragonGem;gear.SocketTwo=Enums.Gem.SuperPhoenixGem;
        gear.PerfectionLevel=3;gear.PerfectionProgress=27;gear.PerfectionOwnerGuid=learned.Entity.UID;gear.PerfectionOwner=learned.Entity.Name;gear.Signature="Earned blade";
        Query("UPDATE items SET plus=6,bless=3,enchant=100,socketone=@one,sockettwo=@two,perfectionlevel=3,perfectionprogress=27,perfectionownerguid=@owner,perfectionowner=@name,signature=@signature WHERE uid=@uid",
            "one",(int)gear.SocketOne,"two",(int)gear.SocketTwo,"owner",(long)learned.Entity.UID,"name",learned.Entity.Name,"signature",gear.Signature,"uid",(long)gear.UID);
        EpicNpc(learned,199446,4);EpicConfirm(learned);
        Check(!Fenced(learned) && gear.ID==616439 && gear.Plus==6 && gear.Bless==3 && gear.Enchant==100 && EpicAmount(learned,3004464)==0
            && SavedRune(gear,"id")==616439,"earned Life's Eye awakens the equipped katana without replacing its enhancements");
        VerifyEpicSoul(learned,"ninja",1);var loaded=ReloadBall(learned);VerifyEpicSoul(loaded,"ninja",1);var reloaded=loaded.Equipment.TryGetItem(4);
        Check(!Fenced(loaded) && reloaded.ID==616439 && reloaded.Plus==6 && reloaded.Bless==3 && reloaded.Enchant==100
            && reloaded.SocketOne==Enums.Gem.SuperDragonGem && reloaded.SocketTwo==Enums.Gem.SuperPhoenixGem && reloaded.PerfectionProgress==27
            && reloaded.PerfectionOwnerGuid==loaded.Entity.UID && reloaded.Signature=="Earned blade"
            && new ushort[]{12070,12090,12110}.All(id=>loaded.Spells.ContainsKey(id)),"complete trial-to-three-skills-and-epic journey survives production reload with item identity and metadata");
        VerifyNinjaEpicFailures(job);VerifyNinjaEyeItems(job);VerifyNinjaEpicRecovery(job);
    }
    static void VerifyNinjaEpicFailures(Map job) {
        foreach(string mode in new[]{"no-offer","expired","cancel","balance","bag","trade","wrong-echo","short","far"}) {
            var p=EpicPlayer(job);EpicEnter(p);NinjaRoll(0);EpicNpc(p,199464,1);
            if(mode=="no-offer")AbyssOpen(p,199464);if(mode=="expired")ninjaNow=ninjaNow.AddMinutes(2);if(mode=="cancel")SoulWire(p,255,199464);
            if(mode=="balance")p.Entity.ConquerPoints--;if(mode=="bag")Pack(p,1088001,1);if(mode=="trade")p.Trade.InTrade=true;if(mode=="far")p.Entity.X=1;
            SoulWire(p,20,mode=="wrong-echo"?1u:199464u,mode=="short"?18:20);
            Check(EpicReceipts(p)==0 && EpicAmount(p,3004462)==0 && p.Entity.ConquerPoints==(mode=="balance"?999:1000),"invalid trial confirmation cannot charge: "+mode);
        }
        foreach(string table in new[]{"entities","items","revora_ninja_epic_operations","revora_ninja_epic_rewards"}) {
            var p=EpicPlayer(job);EpicEnter(p);NinjaRoll(0);EpicNpc(p,199464,1);
            Query("CREATE FUNCTION suppress_epic() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_epic BEFORE INSERT OR UPDATE ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_epic()");
            try{EpicConfirm(p);}finally{Query("DROP TRIGGER suppress_epic ON "+table+";DROP FUNCTION suppress_epic()");}
            Check(Fenced(p) && EpicReceipts(p)==0 && p.Entity.ConquerPoints==1000 && EpicAmount(p,3004462)==0
                && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==1000,"failed paid-trial write rolls back currency, reward and receipt: "+table);
        }
        foreach(string table in new[]{"items","revora_ninja_epic_materials","revora_ninja_epic_rewards"}) {
            var p=EpicPlayer(job);EpicEnter(p);var dust=NestedPack(p,3004465,10,true);EpicNpc(p,199546,1);
            Query("CREATE FUNCTION suppress_epic() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_epic BEFORE INSERT OR UPDATE OR DELETE ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_epic()");
            try{EpicConfirm(p);}finally{Query("DROP TRIGGER suppress_epic ON "+table+";DROP FUNCTION suppress_epic()");}
            Check(Fenced(p) && EpicReceipts(p)==0 && dust.StackSize==10 && SavedRune(dust,"stacksize")==10 && EpicAmount(p,3004459)==0,"failed scroll exchange preserves its exact bound Stardust: "+table);
        }
        var race=EpicPlayer(job);EpicEnter(race);EpicNpc(race,199464,1);
        NinjaStore.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>{race.Trade.InTrade=true;return 0;}));EpicConfirm(race);
        Check(Fenced(race) && EpicReceipts(race)==0 && race.Entity.ConquerPoints==1000,"eligibility lost during trial rolls back before commit");NinjaRoll(0);
        var mismatch=EpicPlayer(job);EpicEnter(mismatch);EpicNpc(mismatch,199464,1);Query("UPDATE entities SET class=15 WHERE uid=@uid","uid",(long)mismatch.Entity.UID);EpicConfirm(mismatch);
        Check(Fenced(mismatch) && EpicReceipts(mismatch)==0 && mismatch.Entity.ConquerPoints==1000,"external saved-class conflict cannot pay or grant a quest reward");
    }
    static void VerifyNinjaEyeItems(Map job) {
        var packed=EpicPlayer(job);RebirthGear(packed,4,601439);var stacked=NestedPack(packed,3004464,2,true);
        while(packed.Inventory.Count<40)Pack(packed,1088001,1);
        ItemHandler.UseItem(stacked,packed);Check(EpicReceipts(packed)==0 && stacked.StackSize==2,"full bag rejects awakening when the Eye stack cannot free a soul slot");
        DisRemove(packed,packed.Inventory.Objects.First(i=>i.ID==1088001));ItemHandler.UseItem(stacked,packed);VerifyEpicSoul(packed,"ninja",1);

        var p=EpicPlayer(job);var right=RebirthGear(p,4,601439);var left=RebirthGear(p,5,601439);var eye=NestedPack(p,3004464,2,true);
        ItemHandler.UseItem(eye,p);ItemHandler.UseItem(eye,p);
        VerifyEpicSoul(p,"ninja",1);
        Check(!Fenced(p) && right.ID==616439 && right.Bound && left.ID==601439 && eye.StackSize==1 && EpicReceipts(p)==1,"item-use replay cannot spend a second Eye or awaken the other hand immediately");
        p=ReloadBall(p);VerifyEpicSoul(p,"ninja",1);p.LoadItemStats();p.CalculateStatBonus();p.CalculateHPBonus();p.GemAlgorithm();
        right=p.Equipment.TryGetItem(4);left=p.Equipment.TryGetItem(5);eye=p.Inventory.Objects.Single(i=>i.ID==3004464);ItemHandler.UseItem(eye,p);
        Check(!Fenced(p) && left.ID==601439 && eye.StackSize==1 && EpicReceipts(p)==1,"durable Eye replay protection survives production reconnect");
        ninjaNow=ninjaNow.AddSeconds(2);ItemHandler.UseItem(eye,p);
        Check(!Fenced(p) && left.ID==616439 && left.Bound && !p.Inventory.ContainsUID(eye.UID) && EpicReceipts(p)==2,"later deliberate item use awakens the remaining katana and preserves binding");
        var invalid=EpicPlayer(job);var sword=RebirthGear(invalid,4,410339);var kept=NestedPack(invalid,3004464,1,false);ItemHandler.UseItem(kept,invalid);
        Check(!Fenced(invalid) && sword.ID==410339 && invalid.Inventory.ContainsUID(kept.UID) && EpicReceipts(invalid)==0,"Life's Eye cannot mint an epic or replace a non-katana weapon");
        foreach(string mode in new[]{"gear-write","material-write","reward-write","saved-metadata","stale-object"}) {
            var owner=EpicPlayer(job);var blade=RebirthGear(owner,4,601439);var material=NestedPack(owner,3004464,1,false);
            if(mode=="saved-metadata")Query("UPDATE items SET plus=7 WHERE uid=@uid","uid",(long)blade.UID);
            if(mode=="stale-object"){ItemHandler.UseItem(new ConquerItem(true){UID=material.UID,ID=material.ID},owner);}
            else {
                if(mode=="gear-write" || mode=="material-write" || mode=="reward-write")Query("CREATE FUNCTION suppress_eye() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_eye BEFORE "+(mode=="gear-write"?"UPDATE":mode=="reward-write"?"INSERT":"DELETE")+" ON items FOR EACH ROW EXECUTE FUNCTION suppress_eye()");
                try{ItemHandler.UseItem(material,owner);}finally{if(mode=="gear-write" || mode=="material-write" || mode=="reward-write")Query("DROP TRIGGER suppress_eye ON items;DROP FUNCTION suppress_eye()");}
            }
            Check(!owner.Inventory.Objects.Any(i=>i.ID>=800000 && i.ID<900000) && blade.ID==601439 && SavedRune(blade,"id")==601439 && owner.Inventory.ContainsUID(material.UID) && SavedRune(material,"id")==3004464
                && EpicReceipts(owner)==0 && (mode=="stale-object"?!Fenced(owner):Fenced(owner)),"invalid Eye conversion preserves material and weapon: "+mode);
        }
    }
    static void VerifyNinjaEpicRecovery(Map job) {
        foreach(string mode in new[]{"full-bag","vip","class","level","rebirth","invalid-roll"}) {
            var p=EpicPlayer(job);EpicEnter(p);NinjaRoll(mode=="invalid-roll"?100:0);
            if(mode=="full-bag")for(int i=0;i<40;i++)Pack(p,1088001,1);
            if(mode=="class")p.Entity.Class=45;if(mode=="level")p.Entity.Level=69;if(mode=="rebirth")p.Entity.Reborn=1;
            EpicNpc(p,199464,mode=="vip"?(byte)2:(byte)1);EpicConfirm(p);
            Check(EpicReceipts(p)==0 && p.Entity.ConquerPoints==1000 && EpicAmount(p,3004462)==0,"trial admission or draw cannot spend value: "+mode);
        }
        var stranded=EpicPlayer(job);EpicEnter(stranded);EpicSeal(stranded);EpicNpc(stranded,199464,3);NinjaRoll(99);EpicNpc(stranded,199465,1);
        var floor=Kernel.Maps[3849].Floor;floor[29,28,MapObjectType.InvalidCast]=false;
        try{EpicConfirm(stranded);}finally{floor[29,28,MapObjectType.InvalidCast]=true;}
        Check(Fenced(stranded) && stranded.Entity.MapID==3850 && EpicAmount(stranded,3004465)==1 && EpicReceipts(stranded)==2
            && Scalar("SELECT count(*) FROM revora_ninja_epic_operations WHERE uid=@uid AND return_map=3849 AND returned_at IS NULL",stranded.Entity.UID)==1,
            "interrupted failure return retains committed reward and durable pending travel");
        stranded=ReloadBall(stranded);Call(NinjaQuest,null,"OnLogin",stranded);Call(NinjaQuest,null,"OnLogin",stranded);
        Check(!Fenced(stranded) && stranded.Entity.MapID==3849 && EpicAmount(stranded,3004465)==1 && EpicReceipts(stranded)==2
            && Scalar("SELECT count(*) FROM revora_ninja_epic_operations WHERE uid=@uid AND return_map=3849 AND returned_at IS NULL",stranded.Entity.UID)==0,
            "login resumes the free return without a second charge, draw or Stardust award");
    }
}
