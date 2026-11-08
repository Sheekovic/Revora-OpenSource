using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static void MonkDraw(int roll) { Feature("MonkEpicStore").GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(n=>roll)); }
    static long MonkCount(GameState p,uint item) { return p.Inventory.Objects.Where(i=>i.ID==item).Sum(i=>(long)i.StackSize); }
    static void MonkCapture() {
        Player(Kernel.Maps[1002]);Sql("UPDATE entities SET class=65,reborn=2 WHERE uid=@uid","uid",(long)FirstUid);nextUid=FirstUid;
        var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();
        var shop=p.Map.Npcs[5];p.Entity.X=shop.X;p.Entity.Y=(ushort)(shop.Y+2);
        uint price=ConquerItemInformation.BaseInformations[610003].GoldWorth;
        var purchase=new ItemUsage(true){UID=5,dwParam=610003,dwExtraInfo=1,Action=ItemUsage.Mode.BuyFromNPC}.ToArray();
        Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,purchase.Length-8,purchase);ItemUsage.Process(purchase,p);
        Require(MonkCount(p,610003)==1 && p.Entity.Money==100000-price);
        var beads=p.Inventory.Objects.Single(i=>i.ID==610003);
        ItemHandler.EquipItem(new ItemUsage(true){UID=beads.UID,dwParam=4,Action=ItemUsage.Mode.EquipItem},p);
        Require(p.Equipment.TryGetItem(4)!=null && p.Equipment.TryGetItem(4).UID==beads.UID);
        p.BindWorldMap(1004);HeartTalk(p,4271,87);Require(p.Entity.MapID==1700);
        Config.MoneyDropEnabled=false;Config.KillConquerPointsEnabled=false;
        var sources=p.Map.Entities.Values.Where(e=>e.MonsterInfo!=null && e.MonsterInfo.ID>=3600 && e.MonsterInfo.ID<=3631 && !e.Dead).Take(6).ToArray();
        Require(sources.Length==6);
        for(int n=0;n<sources.Length;n++) {
            int color=n%2;Feature("NinjaFlowerHunt").GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>color));
            var before=p.Map.FloorItems.Keys.ToArray();EliminationHit(p,sources[n]);
            var drop=p.Map.FloorItems.Values.Single(i=>!before.Contains(i.UID) && (i.ItemID==722732 || i.ItemID==722736));
            p.Entity.X=drop.X;p.Entity.Y=drop.Y;
            var frame=new FloorItem(true){UID=drop.UID,Type=FloorItem.Remove,X=drop.X,Y=drop.Y,ItemID=drop.ItemID}.ToArray();
            Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,frame.Length-8,frame);FloorItem.Process(frame,p);FloorItem.Process(frame,p);
            Require(drop.PickedUpAlready && p.Inventory.ContainsUID(drop.Item.UID));
        }
        Require(MonkCount(p,722732)==3 && MonkCount(p,722736)==3);
        Require((long)Sql("SELECT count(*) FROM revora_ninja_flower_pickups WHERE collector=@uid","uid",(long)p.Entity.UID)==6);
        HeartTalk(p,3604,90);Require(p.Entity.MapID==1004);HeartTalk(p,4271,86);Require(p.Entity.MapID==3983);
        MonkDraw(0);NinjaAttempt(p,199484);Require(MonkCount(p,3007562)==1 && MonkCount(p,722732)==0 && MonkCount(p,722736)==0);
        NinjaAttempt(p,199485);Require(MonkCount(p,3007563)==1 && MonkCount(p,3007562)==0);
        MonkDraw(99);NinjaAttempt(p,199486);Require(MonkCount(p,3007563)==0 && MonkCount(p,3007562)==1 && MonkCount(p,3007565)==3);
        Require(p.Entity.ConquerPoints==0 && p.Entity.Money==100000-price);
        EntityTable.SaveEntity(p);
        Sql("CREATE TABLE revora_monk_process_probe(weapon bigint,silver bigint,gem bigint,fingerprint text); INSERT INTO revora_monk_process_probe(weapon,silver,gem) VALUES(@weapon,@silver,@gem)","weapon",(long)beads.UID,"silver",(long)p.Entity.Money,"gem",(long)p.Inventory.Objects.Single(i=>i.ID==3007562).UID);
        System.Console.WriteLine("[monk-process] monk-capture-passed");Environment.Exit(0);
    }
    static void MonkFinish() {
        var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();Require(p.Entity.MapID==3983);
        Require(MonkCount(p,3007562)==1 && MonkCount(p,3007565)==3 && MonkCount(p,3007564)==0);
        Require(p.Inventory.Objects.Single(i=>i.ID==3007562).UID==(long)Sql("SELECT gem FROM revora_monk_process_probe"));
        MonkDraw(0);NinjaAttempt(p,199485);NinjaAttempt(p,199486);Require(MonkCount(p,3007564)==1);
        HeartTalk(p,199483,0);HeartTalk(p,199483,4);HeartTalk(p,199483,20);HeartTalk(p,199483,20);
        Require(p.Equipment.TryGetItem(4).ID==622003 && MonkCount(p,3007564)==0 && MonkCount(p,3007565)==3);
        Sql("UPDATE revora_monk_process_probe SET fingerprint=(SELECT string_agg(uid::text||':'||id::text,',' ORDER BY uid) FROM items WHERE entityid=@uid)","uid",(long)p.Entity.UID);
        System.Console.WriteLine("[monk-process] monk-finish-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Monk finish did not close.");
    }
    static void MonkVerify() {
        var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();
        Require(p.Entity.ConquerPoints==0 && p.Entity.Money==Convert.ToUInt64(Sql("SELECT silver FROM revora_monk_process_probe")));
        Require(p.Equipment.TryGetItem(4).ID==622003 && p.Equipment.TryGetItem(4).UID==(long)Sql("SELECT weapon FROM revora_monk_process_probe"));
        Require(Equals(Sql("SELECT fingerprint FROM revora_monk_process_probe"),Sql("SELECT string_agg(uid::text||':'||id::text,',' ORDER BY uid) FROM items WHERE entityid=@uid","uid",(long)p.Entity.UID)));
        Require((long)Sql("SELECT count(*) FROM revora_monk_epic_operations WHERE uid=@uid","uid",(long)p.Entity.UID)==6);
        Require((long)Sql("SELECT count(*) FROM revora_monk_epic_operations WHERE uid=@uid AND kind=5","uid",(long)p.Entity.UID)==1);
        var soulUid=Convert.ToUInt32(Sql("SELECT r.item_uid FROM revora_monk_epic_rewards r JOIN revora_monk_epic_operations o ON o.id=r.operation WHERE o.uid=@uid AND o.kind=5 AND r.bound=true","uid",(long)p.Entity.UID));
        var soul=p.Inventory.Objects.Single(i=>i.UID==soulUid);Require(soul.Bound && ConquerItemInformation.BaseInformations[soul.ID].PurificationLevel==7);
        Require(MonkCount(p,3007565)==3 && MonkCount(p,3007562)==0 && MonkCount(p,3007563)==0 && MonkCount(p,3007564)==0);
        Require((long)Sql("SELECT count(*) FROM revora_weapon_book_purchases WHERE uid=@uid AND item_type=610003 AND shop=5","uid",(long)p.Entity.UID)==1);
        System.Console.WriteLine("[monk-process] monk-verify-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Monk verify did not close.");
    }
}
