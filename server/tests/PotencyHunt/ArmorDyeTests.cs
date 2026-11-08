using System;
using System.IO;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type ArmorDyes=typeof(Entity).Assembly.GetType("Revora.Game.Features.ArmorDyeShop");
    static void DyeNpc(GameState p,uint npc,byte option) {p.ActiveNpc=npc;Npcs.GetDialog(new NpcRequest(true){NpcID=npc,OptionID=option},p);}
    static ConquerItem DyeGear(GameState p,byte slot=3,byte colour=3) {
        var item=new ConquerItem(true){UID=ConquerItem.ItemUID.Next,ID=slot==3?130009u:111009u,Position=slot,StackSize=1,Color=(Enums.Color)colour,IsWorn=true};
        Query("INSERT INTO items(uid,id,entityid,position,stacksize,bound,color) VALUES(@uid,@id,@owner,@slot,1,0,@color)","uid",(long)item.UID,"id",(int)item.ID,"owner",(long)p.Entity.UID,"slot",(int)slot,"color",(int)colour);
        p.Equipment.Objects[slot-1]=item;return item;
    }
    static GameState DyeVisitor(Map town) {var p=CosmeticPlayer(town);p.Entity.X=429;p.Entity.Y=312;return p;}
    static void AtTinter(GameState p) {p.Entity.X=27;p.Entity.Y=17;p.ActiveNpc=10064;}
    static long DyeCount(GameState p) {return Scalar("SELECT count(*) FROM revora_armor_dyes WHERE uid=@uid",p.Entity.UID);}
    static void VerifyArmorDyes(Map town) {
        Query("CREATE TABLE itemadding AS TABLE public.itemadding WITH NO DATA");
        Call(ArmorDyes,null,"Initialize");Call(ArmorDyes,null,"Initialize");
        var shop=World(Directory.GetCurrentDirectory(),1008);
        town.Npcs[10063]=new NpcSpawn{UID=10063,MapID=1002,X=429,Y=312};shop.Npcs[10064]=new NpcSpawn{UID=10064,MapID=1008,X=27,Y=16};
        Check(shop.Floor[22,26,MapObjectType.Player,null],"installed dye shop landing is walkable");
        var p=DyeVisitor(town);var armor=DyeGear(p);var helmet=DyeGear(p,1);var meteor=Pack(p,1088001,3);
        DyeNpc(p,10063,1);
        Check(!Fenced(p) && p.Entity.MapID==1008 && meteor.StackSize==2 && SavedRune(meteor,"stacksize")==2
            && Scalar("SELECT count(*) FROM revora_dye_visits WHERE uid=@uid AND entered_at IS NOT NULL AND closed_at IS NULL",p.Entity.UID)==1,"real Shopboy route charges one Meteor and durably enters the shop");
        AtTinter(p);DyeNpc(p,10064,1);DyeNpc(p,10064,14);
        Check(!Fenced(p) && (byte)helmet.Color==4 && SavedRune(helmet,"color")==4 && DyeCount(p)==1
            && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10115",p.Entity.UID)==0,"helmet dye persists without inventing a clothing achievement");
        DyeNpc(p,10064,3);DyeNpc(p,10064,13);
        Check(DyeCount(p)==1 && (byte)armor.Color==3,"unchanged armor color creates no receipt or achievement");
        DyeNpc(p,10064,3);DyeNpc(p,10064,19);
        Check(!Fenced(p) && (byte)armor.Color==9 && SavedRune(armor,"color")==9 && meteor.StackSize==2 && HasAchievement(p,10115),"real Tinter armor change is free during the paid visit and immediately earns 10115");
        Parallel.For(0,12,i=>DyeNpc(p,10064,19));
        Check(DyeCount(p)==2 && meteor.StackSize==2,"duplicate color replies cannot mutate or charge again");
        DyeNpc(p,10064,3);DyeNpc(p,10064,15);DyeNpc(p,10064,3);DyeNpc(p,10064,16);
        Check((byte)armor.Color==6 && meteor.StackSize==2 && DyeCount(p)==4 && EntityTable.SaveEntity(p),"multiple deliberate dyes in one visit preserve one admission charge");
        // A fresh ordinary session can continue the same durable visit; no runtime selection is inherited.
        var reloaded=CosmeticPlayer(shop);uint placeholder=reloaded.Entity.UID;
        reloaded.Account.EntityID=p.Entity.UID;reloaded.Account.Username="dye_fixture";reloaded.Quests=new Quests(reloaded);
        Check((bool)Call(typeof(EntityTable),null,"LoadEntityState",reloaded),"production character loader restores a paid shop visitor");
        Kernel.GamePool.Remove(placeholder);Kernel.GamePool[p.Entity.UID]=reloaded;reloaded.Action=2;
        ConquerItemTable.LoadItems(reloaded);AtTinter(reloaded);
        Check((byte)reloaded.Equipment.TryGetItem(3).Color==6,"production item reload restores the exact saved armor color");
        DyeNpc(reloaded,10064,17);Check(DyeCount(reloaded)==4,"reconnected session must select its equipped item again");
        DyeNpc(reloaded,10064,3);DyeNpc(reloaded,10064,17);
        Check(!Fenced(reloaded) && (byte)reloaded.Equipment.TryGetItem(3).Color==7 && DyeCount(reloaded)==5,"reconnect retains free color retries for the paid visit");
        reloaded.Entity.Teleport(1002,429,312);
        Check(Scalar("SELECT count(*) FROM revora_dye_visits WHERE uid=@uid AND closed_at IS NULL",reloaded.Entity.UID)==0,"leaving the shop atomically closes the durable visit");
        reloaded.Entity.Teleport(1008,27,17);DyeNpc(reloaded,10064,3);DyeNpc(reloaded,10064,18);
        Check(DyeCount(reloaded)==5,"returning without paying cannot reuse a closed visit");
        reloaded.Entity.Teleport(1002,429,312);DyeNpc(reloaded,10063,1);
        Check(!Fenced(reloaded) && reloaded.Entity.MapID==1008 && Scalar("SELECT count(*) FROM revora_dye_visits WHERE uid=@uid",reloaded.Entity.UID)==2,"new shop visit requires and records a second Meteor");
        var black=DyeVisitor(town);var blackArmor=DyeGear(black);var balls=Pack(black,1088000,3);
        DyeNpc(black,10063,2);Check(balls.StackSize==3 && (byte)blackArmor.Color==3,"forged black confirmation without selected armor cannot charge");
        DyeNpc(black,10063,3);Parallel.For(0,10,i=>DyeNpc(black,10063,2));
        Check(!Fenced(black) && balls.StackSize==2 && SavedRune(balls,"stacksize")==2 && (byte)blackArmor.Color==2 && HasAchievement(black,10115),"black armor costs exactly one DragonBall with concurrent confirmations");
        DyeNpc(black,10063,3);DyeNpc(black,10063,2);Check(balls.StackSize==2 && DyeCount(black)==1,"already black armor is never charged again");
        var changed=DyeVisitor(town);var first=DyeGear(changed);var price=Pack(changed,1088000,1);DyeNpc(changed,10063,3);var replacement=DyeGear(changed);DyeNpc(changed,10063,2);
        Check(changed.Inventory.ContainsUID(price.UID) && (byte)replacement.Color==3 && SavedRune(first,"color")==3,"equipment replacement after selection cannot dye or charge a different physical item");
        foreach(string failure in new[]{"color","owner","receipt","achievement"}) {
            var f=DyeVisitor(town);var gear=DyeGear(f);var cost=Pack(f,1088000,1);DyeNpc(f,10063,3);
            if(failure=="color")Query("UPDATE items SET color=9 WHERE uid=@uid","uid",(long)gear.UID);
            if(failure=="owner")Query("UPDATE items SET entityid=999 WHERE uid=@uid","uid",(long)gear.UID);
            string table=failure=="receipt"?"revora_armor_dyes":"revora_achievement_evidence";
            if(failure=="receipt" || failure=="achievement")Query("CREATE FUNCTION suppress_armor_write() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER suppress_armor_write BEFORE INSERT ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_armor_write()");
            try{DyeNpc(f,10063,2);}finally{if(failure=="receipt" || failure=="achievement")Query("DROP TRIGGER suppress_armor_write ON "+table+"; DROP FUNCTION suppress_armor_write()");}
            Check(Fenced(f) && (byte)gear.Color==3 && f.Inventory.ContainsUID(cost.UID) && SavedRune(cost,"entityid")==f.Entity.UID && DyeCount(f)==0,"failed "+failure+" rolls back dye, payment and evidence");
        }
        var far=DyeVisitor(town);var farGear=DyeGear(far);var farPrice=Pack(far,1088000,1);DyeNpc(far,10063,3);far.Entity.X=100;DyeNpc(far,10063,2);
        Check(!Fenced(far) && far.Inventory.ContainsUID(farPrice.UID) && (byte)farGear.Color==3,"walking away from Shopboy prevents the paid operation");
        var trade=DyeVisitor(town);var tradeGear=DyeGear(trade);var tradePrice=Pack(trade,1088000,1);DyeNpc(trade,10063,3);trade.Trade.InTrade=true;DyeNpc(trade,10063,2);
        Check(!Fenced(trade) && trade.Inventory.ContainsUID(tradePrice.UID) && (byte)tradeGear.Color==3,"trade blocks a previously selected armor dye");
        var admission=DyeVisitor(town);var admissionFee=Pack(admission,1088001,1);
        Query("CREATE FUNCTION suppress_dye_admission() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER suppress_dye_admission BEFORE INSERT ON revora_dye_visits FOR EACH ROW EXECUTE FUNCTION suppress_dye_admission()");
        try{DyeNpc(admission,10063,1);}finally{Query("DROP TRIGGER suppress_dye_admission ON revora_dye_visits; DROP FUNCTION suppress_dye_admission()");}
        Check(Fenced(admission) && admission.Entity.MapID==1002 && admission.Inventory.ContainsUID(admissionFee.UID) && SavedRune(admissionFee,"entityid")==admission.Entity.UID,"missing admission receipt rolls back the Meteor before any teleport");
        var pending=DyeVisitor(town);var pendingFee=Pack(pending,1088001,3);
        Query("CREATE FUNCTION fail_dye_entry() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.mapid=1008 THEN RAISE EXCEPTION 'fixture entry failure'; END IF; RETURN NEW; END $$; CREATE TRIGGER fail_dye_entry BEFORE UPDATE OF mapid ON entities FOR EACH ROW EXECUTE FUNCTION fail_dye_entry()");
        try{DyeNpc(pending,10063,1);}finally{Query("DROP TRIGGER fail_dye_entry ON entities; DROP FUNCTION fail_dye_entry()");}
        Check(Fenced(pending) && pending.Entity.MapID==1002 && pendingFee.StackSize==2
            && Scalar("SELECT count(*) FROM revora_dye_visits WHERE uid=@uid AND entered_at IS NULL AND closed_at IS NULL",pending.Entity.UID)==1,"failed teleport retains the paid pending admission and original map");
        DemonReset(pending);pending.Entity.X=429;pending.Entity.Y=312;DyeNpc(pending,10063,1);
        Check(!Fenced(pending) && pending.Entity.MapID==1008 && pendingFee.StackSize==2
            && Scalar("SELECT count(*) FROM revora_dye_visits WHERE uid=@uid",pending.Entity.UID)==1,"retry after failed teleport resumes the paid admission without a second Meteor");
        AtTinter(pending);var pendingArmor=DyeGear(pending);DyeNpc(pending,10064,3);
        pending.Entity.Teleport(1002,429,312);DyeNpc(pending,10063,1);AtTinter(pending);DyeNpc(pending,10064,19);
        Check(pendingFee.StackSize==1 && (byte)pendingArmor.Color==3 && DyeCount(pending)==0,"a selection from a closed visit cannot be reused in a new paid visit");
        var unavailable=DyeVisitor(town);var unavailableFee=Pack(unavailable,1088001,1);var tinter=shop.Npcs[10064];shop.Npcs.Remove(10064);
        try{DyeNpc(unavailable,10063,1);}finally{shop.Npcs[10064]=tinter;}
        Check(!Fenced(unavailable) && unavailable.Entity.MapID==1002 && unavailable.Inventory.ContainsUID(unavailableFee.UID),"missing destination Tinter prevents any admission charge");
        var bound=DyeVisitor(town);var boundFee=Pack(bound,1088001,1);boundFee.Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)boundFee.UID);DyeNpc(bound,10063,1);
        Check(!Fenced(bound) && bound.Entity.MapID==1008 && bound.Inventory.Count==0
            && Scalar("SELECT count(*) FROM revora_dye_visits WHERE uid=@uid AND bound",bound.Entity.UID)==1,"bound Meteor buys an ordinary player's shop visit");
        var unworn=DyeVisitor(town);var unwornGear=DyeGear(unworn);unwornGear.IsWorn=false;var unwornFee=Pack(unworn,1088000,1);DyeNpc(unworn,10063,3);DyeNpc(unworn,10063,2);
        Check(!Fenced(unworn) && unworn.Inventory.ContainsUID(unwornFee.UID) && (byte)unwornGear.Color==3,"inactive equipment cannot earn a worn clothing achievement");
        var noFee=DyeVisitor(town);DyeNpc(noFee,10063,1);Check(noFee.Entity.MapID==1002 && Scalar("SELECT count(*) FROM revora_dye_visits WHERE uid=@uid",noFee.Entity.UID)==0,"no Meteor grants no admission");
    }
}
