using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Revora;
using Revora.Database;
using Revora.Game;
using Revora.Client;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static readonly uint[] MountPacks = new uint[] {3000254,3000256,3000257,3000258,3000259,3000260,3000261,3000262,3000263,3000264,3000265,3000266,3000267,3000268,3000269,3000270,3000271};
    static long MountUses(uint uid) { return (long)Sql("SELECT count(*) FROM revora_fixed_output_pack_uses WHERE uid=@uid","uid",(long)uid); }
    static void MountPackBuy() {
        Player(Kernel.Maps[1002]);nextUid=FirstUid;
        var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();
        var daily=Feature("DailyWorldBosses");DateTime now=DateTime.Now.Date.AddDays(1).ToUniversalTime();
        daily.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));Call(daily,"Tick");
        now=DateTime.SpecifyKind((DateTime)Sql("SELECT min(planned) FROM revora_daily_boss_runs WHERE planned>=@now AND state=0","now",now),DateTimeKind.Utc).AddSeconds(1);
        Call(daily,"Tick");var active=daily.GetField("Active",Hidden).GetValue(null);Require(active!=null);
        var boss=(Entity)active.GetType().GetField("Entity",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(active);
        p.BindWorldMap(boss.MapID);EliminationHit(p,boss);Require(p.Entity.ConquerPoints==1000000);
        p.BindWorldMap(1002);
        foreach(uint id in MountPacks) {
            uint before=p.Entity.ConquerPoints;
            ItemHandler.HandleBuyFromNPC(new ItemUsage(true){UID=2888,dwParam=id,dwExtraInfo=1},p);
            Require(p.Entity.ConquerPoints==before-2799 && p.Inventory.Objects.Count(i=>i.ID==id)==1);
        }
        Require(p.Entity.ConquerPoints==952417 && MountUses(p.Entity.UID)==0);
        var pack=p.Inventory.Objects.Single(i=>i.ID==MountPacks[0]);
        Sql("CREATE FUNCTION mount_probe_refuse() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER mount_probe_refuse BEFORE INSERT ON revora_fixed_output_pack_uses FOR EACH ROW EXECUTE FUNCTION mount_probe_refuse()");
        try {ItemHandler.UseItem(pack,p);} finally {Sql("DROP TRIGGER mount_probe_refuse ON revora_fixed_output_pack_uses; DROP FUNCTION mount_probe_refuse()");}
        Require((bool)typeof(Entity).GetField("PersistenceRequiresReload",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(p.Entity));
        Require(MountUses(p.Entity.UID)==0 && (long)Sql("SELECT count(*) FROM items WHERE entityid=@uid AND id=200407","uid",(long)p.Entity.UID)==0);
        Require((long)Sql("SELECT count(*) FROM items WHERE uid=@uid","uid",(long)pack.UID)==1);
        System.Console.WriteLine("[mount-process] mount-buy-passed");
        Server.CommandsAI("@close");throw new InvalidOperationException("Mount purchase process did not close.");
    }
    static void MountPackOpen() {
        var p=Player(Kernel.Maps[1002],true);new CoatStorage().Login(p);
        Require(p.Entity.ConquerPoints==952417 && MountUses(p.Entity.UID)==0);
        foreach(uint id in MountPacks) {
            var pack=p.Inventory.Objects.Single(i=>i.ID==id);
            uint output=(uint)Call(Feature("FixedOutputPacks"),"MountFor",id);
            Parallel.For(0,8,n=>ItemHandler.UseItem(pack,p));
            Require(!p.Inventory.ContainsUID(pack.UID));
            var mount=p.Inventory.Objects.Single(i=>i.ID==output);
            Require(!mount.Bound && mount.Bless==1 && mount.Plus==0 && mount.Days==0 && mount.StackSize==1);
            var saved=ConquerItemTable.LoadItem(mount.UID);Require(saved.ID==output && saved.Bless==1 && !saved.Bound && saved.Days==0);
            var add=new CoatStorage{Info=new CoatStorageProto{ActionId=CoatStorage.Action.Addtowardrobe,ItemGuid=mount.UID,ItemId=(int)mount.ID}};
            add.Handle(p);add.Handle(p);Require(p.Entity.StorageItems.ContainsKey(mount.UID));
        }
        p.Entity.MyAchievement.Send();
        Require(MountUses(p.Entity.UID)==17 && p.Entity.StorageItems.Count==17 && p.Entity.ConquerPoints==952417);
        Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11226","uid",(long)p.Entity.UID)==1);
        Sql("CREATE TABLE revora_mount_process_probe(fingerprint text NOT NULL); INSERT INTO revora_mount_process_probe SELECT string_agg(uid::text||':'||id::text,',' ORDER BY uid) FROM items WHERE entityid=@uid AND inwardrobe=1","uid",(long)p.Entity.UID);
        System.Console.WriteLine("[mount-process] mount-open-passed");
        Server.CommandsAI("@close");throw new InvalidOperationException("Mount opening process did not close.");
    }
    static void MountPackVerify() {
        var p=Player(Kernel.Maps[1002],true);new CoatStorage().Login(p);
        Require(p.Entity.ConquerPoints==952417 && p.Entity.StorageItems.Count==17 && MountUses(p.Entity.UID)==17);
        Require(p.Entity.StorageItems.Values.All(i=>i.Bless==1 && !i.Bound && i.Days==0));
        Require(Equals(Sql("SELECT fingerprint FROM revora_mount_process_probe"),Sql("SELECT string_agg(uid::text||':'||id::text,',' ORDER BY uid) FROM items WHERE entityid=@uid AND inwardrobe=1","uid",(long)p.Entity.UID)));
        Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11226","uid",(long)p.Entity.UID)==1);
        MountPackBoundaries();
        System.Console.WriteLine("[mount-process] mount-verify-passed");
        Server.CommandsAI("@close");throw new InvalidOperationException("Mount verification process did not close.");
    }
    // Supplied inventories below are fault fixtures, separate from the earned character.
    static ConquerItem SuppliedMountItem(GameState p,uint id,ushort stack,bool bound) {
        var item=(ConquerItem)Call(Feature("QuestInventory"),"New",id,bound);item.StackSize=stack;
        Sql("INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability) VALUES(@item,@type,@uid,0,0,@stack,@bound,@dur,@dur)",
            "item",(long)item.UID,"type",(int)id,"uid",(long)p.Entity.UID,"stack",(int)stack,"bound",bound?1:0,"dur",(int)item.Durability);
        Require(p.Inventory.Add(item,Enums.ItemUse.None));return item;
    }
    static byte[] MountUsePacket(uint uid) {
        var frame=new ItemUsage(true){UID=uid,Action=ItemUsage.Mode.EquipItem}.ToArray();
        Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);return frame;
    }
    static void MountPackBoundaries() {
        var stackPlayer=Player(Kernel.Maps[1002]);
        var stack=SuppliedMountItem(stackPlayer,3000254,2,true);
        while(stackPlayer.Inventory.Count<40)SuppliedMountItem(stackPlayer,1000000,1,false);
        ItemUsage.Process(MountUsePacket(stack.UID),stackPlayer);
        Require(stackPlayer.Inventory.Count==40 && stack.StackSize==2 && MountUses(stackPlayer.Entity.UID)==0);
        Require(Convert.ToInt32(Sql("SELECT stacksize FROM items WHERE uid=@uid","uid",(long)stack.UID))==2);
        var filler=stackPlayer.Inventory.Objects.First(i=>i.ID==1000000);
        Require(stackPlayer.Inventory.Remove(filler,Enums.ItemUse.Remove));
        ItemUsage.Process(MountUsePacket(stack.UID),stackPlayer);
        Require(stack.StackSize==1 && stackPlayer.Inventory.Count==40 && MountUses(stackPlayer.Entity.UID)==1);
        ItemUsage.Process(MountUsePacket(stack.UID),stackPlayer);
        ItemHandler.UseItem(stack,stackPlayer);
        Require(!stackPlayer.Inventory.ContainsUID(stack.UID) && stackPlayer.Inventory.Count==40 && MountUses(stackPlayer.Entity.UID)==2);
        Require(stackPlayer.Inventory.Objects.Count(i=>i.ID==200407 && !i.Bound && i.Bless==1 && i.Days==0)==2);

        var singlePlayer=Player(Kernel.Maps[1002]);var singleton=SuppliedMountItem(singlePlayer,3000256,1,false);
        while(singlePlayer.Inventory.Count<40)SuppliedMountItem(singlePlayer,1000000,1,false);
        ItemUsage.Process(MountUsePacket(singleton.UID),singlePlayer);
        Require(singlePlayer.Inventory.Count==40 && !singlePlayer.Inventory.ContainsUID(singleton.UID) && MountUses(singlePlayer.Entity.UID)==1);
        Require(singlePlayer.Inventory.Objects.Count(i=>i.ID==200411 && i.Bless==1)==1);

        var wirePlayer=Player(Kernel.Maps[1002]);var wire=SuppliedMountItem(wirePlayer,3000257,1,false);
        foreach(string bad in new[]{"empty","tiny","short","long","length","type","action","seal"}) {
            byte[] frame=bad=="empty"?new byte[0]:bad=="tiny"?new byte[11]:bad=="short"?new byte[31]:bad=="long"?new byte[129]:MountUsePacket(wire.UID);
            if(frame.Length>=31) {
                Writer.WriteUInt16((ushort)(frame.Length-8),0,frame);Writer.WriteUInt16(1009,2,frame);Writer.WriteUInt32(wire.UID,8,frame);
                Writer.WriteUInt16(4,20,frame);Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);
                if(bad=="length")Writer.WriteUInt16(1,0,frame);if(bad=="type")Writer.WriteUInt16(1008,2,frame);
                if(bad=="action")Writer.WriteUInt16(5,20,frame);if(bad=="seal")frame[frame.Length-1]^=1;
            }
            Call(Feature("FixedOutputPacks"),"TryHandle",wirePlayer,frame);
            Require(MountUses(wirePlayer.Entity.UID)==0 && wirePlayer.Inventory.ContainsUID(wire.UID));
        }
        ItemHandler.UseItem(wire,singlePlayer);
        Require(MountUses(singlePlayer.Entity.UID)==1 && wirePlayer.Inventory.ContainsUID(wire.UID));
        Sql("UPDATE items SET stacksize=2 WHERE uid=@uid","uid",(long)wire.UID);
        ItemUsage.Process(MountUsePacket(wire.UID),wirePlayer);
        Require((bool)typeof(Entity).GetField("PersistenceRequiresReload",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(wirePlayer.Entity));
        Require(MountUses(wirePlayer.Entity.UID)==0 && Convert.ToInt32(Sql("SELECT stacksize FROM items WHERE uid=@uid","uid",(long)wire.UID))==2);
        Require((long)Sql("SELECT count(*) FROM items WHERE entityid=@uid AND id=200412","uid",(long)wirePlayer.Entity.UID)==0);
        System.Console.WriteLine("[mount-process] full bags, bound stack consumption, malformed frames and stale ownership passed");
    }
}
