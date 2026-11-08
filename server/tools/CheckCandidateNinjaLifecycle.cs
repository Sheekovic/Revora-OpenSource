using System;
using System.Linq;
using System.Reflection;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static void NinjaAttempt(GameState p,uint npc) { HeartTalk(p,npc,0);HeartTalk(p,npc,1);HeartTalk(p,npc,20); }
    static void NinjaCapture() {
        Player(Kernel.Maps[1002]);Sql("UPDATE entities SET class=55,reborn=2 WHERE uid=@uid","uid",(long)FirstUid);nextUid=FirstUid;
        var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();
        var daily=Feature("DailyWorldBosses");DateTime now=DateTime.Now.Date.AddDays(1).ToUniversalTime();
        daily.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));Call(daily,"Tick");
        now=DateTime.SpecifyKind((DateTime)Sql("SELECT min(planned) FROM revora_daily_boss_runs WHERE planned>=@now AND state=0","now",now),DateTimeKind.Utc).AddSeconds(1);
        Call(daily,"Tick");var active=daily.GetField("Active",Hidden).GetValue(null);Require(active!=null);
        var boss=(Entity)active.GetType().GetField("Entity",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(active);
        p.BindWorldMap(boss.MapID);EliminationHit(p,boss);Require(p.Entity.ConquerPoints==1000000);
        p.BindWorldMap(1002);uint price=ConquerItemInformation.BaseInformations[601003].GoldWorth;
        var silver=p.Entity.Money;
        var shop=p.Map.Npcs[5];p.Entity.X=shop.X;p.Entity.Y=(ushort)(shop.Y+2);
        var purchase=new ItemUsage(true){UID=5,dwParam=601003,dwExtraInfo=1,Action=ItemUsage.Mode.BuyFromNPC}.ToArray();
        Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,purchase.Length-8,purchase);
        ItemUsage.Process(purchase,p);
        Require(p.Inventory.Objects.Count(i=>i.ID==601003)==1);
        Require((long)Sql("SELECT count(*) FROM revora_weapon_book_purchases WHERE uid=@uid AND shop=5 AND item_type=601003 AND cost=@cost","uid",(long)p.Entity.UID,"cost",(long)price)==1);
        var blade=p.Inventory.Objects.Single(i=>i.ID==601003);Require(p.Entity.Money==silver-price);
        ItemHandler.EquipItem(new ItemUsage(true){UID=blade.UID,dwParam=4,Action=ItemUsage.Mode.EquipItem},p);
        Require(p.Equipment.TryGetItem(4)!=null && p.Equipment.TryGetItem(4).UID==blade.UID);
        p.BindWorldMap(1004);HeartTalk(p,4720,86);Require(p.Entity.MapID==3849);
        var store=Feature("NinjaEpicStore");store.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(n=>99));
        NinjaAttempt(p,199464);Require(p.Entity.ConquerPoints==999973 && !p.Inventory.Objects.Any(i=>i.ID==3004462));
        HeartTalk(p,199464,20);Require(p.Entity.ConquerPoints==999973);
        store.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(n=>0));
        NinjaAttempt(p,199464);Require(p.Entity.ConquerPoints==999946 && p.Inventory.Objects.Count(i=>i.ID==3004462)==1);
        HeartTalk(p,199464,3);Require(p.Entity.MapID==3850);NinjaAttempt(p,199465);
        Require(!p.Inventory.Objects.Any(i=>i.ID==3004462) && p.Inventory.Objects.Count(i=>i.ID==3004463)==1);
        HeartTalk(p,199465,3);Require(p.Entity.MapID==3851);NinjaAttempt(p,199466);
        var eye=p.Inventory.Objects.Single(i=>i.ID==3004464);Require(!p.Inventory.Objects.Any(i=>i.ID==3004463));
        HeartTalk(p,199446,0);HeartTalk(p,199446,4); // Interrupt an unconfirmed awakening offer.
        EntityTable.SaveEntity(p);
        Sql("CREATE TABLE revora_ninja_process_probe(weapon bigint,eye bigint,silver bigint,fingerprint text); INSERT INTO revora_ninja_process_probe(weapon,eye,silver) VALUES(@weapon,@eye,@silver)","weapon",(long)blade.UID,"eye",(long)eye.UID,"silver",(long)p.Entity.Money);
        System.Console.WriteLine("[ninja-process] ninja-capture-passed");Environment.Exit(0);
    }
    static void NinjaFinish() {
        var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();Call(Feature("NinjaEpicQuest"),"OnLogin",p);
        Require(p.Entity.ConquerPoints==999946 && p.Entity.MapID==3851);
        var blade=p.Equipment.TryGetItem(4);Require(blade!=null && blade.ID==601003 && blade.UID==(long)Sql("SELECT weapon FROM revora_ninja_process_probe"));
        HeartTalk(p,199446,20);Require(blade.ID==601003 && p.Inventory.Objects.Any(i=>i.ID==3004464));
        HeartTalk(p,199446,4);HeartTalk(p,199446,20);HeartTalk(p,199446,20);
        Require(p.Equipment.TryGetItem(4).ID==616003 && !p.Inventory.Objects.Any(i=>i.ID==3004464));
        Require((long)Sql("SELECT count(*) FROM revora_ninja_epic_operations WHERE uid=@uid AND kind=7","uid",(long)p.Entity.UID)==1);
        Require((long)Sql("SELECT count(*) FROM revora_ninja_epic_rewards r JOIN items i ON i.uid=r.item_uid WHERE i.entityid=@uid AND i.bound=1 AND r.bound=true","uid",(long)p.Entity.UID)==1);
        Sql("UPDATE revora_ninja_process_probe SET fingerprint=(SELECT string_agg(uid::text||':'||id::text,',' ORDER BY uid) FROM items WHERE entityid=@uid)","uid",(long)p.Entity.UID);
        System.Console.WriteLine("[ninja-process] ninja-finish-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Ninja finish did not close.");
    }
    static void NinjaVerify() {
        var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();Call(Feature("NinjaEpicQuest"),"OnLogin",p);
        var soulId=Convert.ToUInt32(Sql("SELECT r.item_uid FROM revora_ninja_epic_rewards r JOIN revora_ninja_epic_operations o ON o.id=r.operation WHERE o.uid=@uid AND o.kind=7 AND r.bound=true","uid",(long)p.Entity.UID));
        var soul=p.Inventory.Objects.Single(i=>i.UID==soulId);
        Require(soul.Bound && ConquerItemInformation.BaseInformations[soul.ID].PurificationLevel==7);
        Require(p.Entity.ConquerPoints==999946 && p.Entity.Money==Convert.ToUInt64(Sql("SELECT silver FROM revora_ninja_process_probe")));
        Require(p.Equipment.TryGetItem(4).ID==616003 && p.Equipment.TryGetItem(4).UID==(long)Sql("SELECT weapon FROM revora_ninja_process_probe"));
        Require(Equals(Sql("SELECT fingerprint FROM revora_ninja_process_probe"),Sql("SELECT string_agg(uid::text||':'||id::text,',' ORDER BY uid) FROM items WHERE entityid=@uid","uid",(long)p.Entity.UID)));
        Require((long)Sql("SELECT count(*) FROM revora_ninja_epic_operations WHERE uid=@uid","uid",(long)p.Entity.UID)==5);
        Require((long)Sql("SELECT count(*) FROM items WHERE uid=(SELECT eye FROM revora_ninja_process_probe)")==0);
        System.Console.WriteLine("[ninja-process] ninja-verify-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Ninja verify did not close.");
    }
}
