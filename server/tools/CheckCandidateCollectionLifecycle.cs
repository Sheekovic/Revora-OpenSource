using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Store=Revora.Game.ConquerStructures.Warehouse;
using Console=System.Console;

internal static partial class CheckCandidateArenaLifecycle
{
    static GameState CollectionPlayer() {
        var p=Player(Kernel.Maps[1002],true,1003,true,true);
        new CoatStorage().Login(p);p.LoadItemStats();return p;
    }
    static uint[] CollectionMounts() {return (uint[])Feature("CollectionAchievementCatalog").GetField("Mounts",Hidden).GetValue(null);}
    static int CollectionMountCount(GameState p) {return p.Entity.StorageItems.Values.Select(i=>i.ID).Distinct().Intersect(CollectionMounts()).Count();}
    static void CollectionDeposit(GameState p,ConquerItem item) {
        Require(item.Days==0 && item.StackSize==1);
        var add=new CoatStorage{Info=new CoatStorageProto{ActionId=CoatStorage.Action.Addtowardrobe,ItemGuid=item.UID,ItemId=(int)item.ID}};
        add.Handle(p);add.Handle(p);Require(p.Entity.StorageItems.ContainsKey(item.UID) && !p.Inventory.ContainsUID(item.UID));
    }
    static void CollectionStash(GameState p) {
        foreach(var item in p.Inventory.Objects.ToArray()) {
            Require(item.Days==0); // Timed packs must be opened on their earning date.
            uint id=(uint)p.Warehouses.Keys.OrderBy(k=>(uint)k).FirstOrDefault(k=>p.Warehouses[k].Count<p.Warehouses[k].MaxCount);
            Require(id!=0);
            ushort map=id==8?(ushort)1002:id==10012?(ushort)1011:(ushort)1020;
            Require(Kernel.Maps[map].Npcs.ContainsKey(id));var npc=Kernel.Maps[map].Npcs[id];
            p.Entity.Teleport(map,(ushort)(npc.X-1),npc.Y);
            var packet=new byte[172];Revora.Network.Writer.WriteUInt16(164,0,packet);Revora.Network.Writer.WriteUInt16(1102,2,packet);
            Revora.Network.Writer.WriteUInt32(id,4,packet);packet[12]=1;packet[13]=10;Revora.Network.Writer.WriteUInt32(item.UID,18,packet);
            Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,164,packet);Warehouse.Process(packet,p);
            Require(!p.Inventory.ContainsUID(item.UID));
        }
        p.BindWorldMap(1002);
    }
    static string CollectionSnapshot(GameState p) {return Convert.ToString(Sql(@"SELECT json_build_object(
'purse',(SELECT json_build_array(conquerpoints,revorapoint,money,boundcps) FROM entities WHERE uid=@uid),
'items',(SELECT json_agg(i ORDER BY uid) FROM (SELECT uid,id,bound,stacksize,warehouse,position,inwardrobe,days,bless FROM items WHERE entityid=@uid) i),
'signs',(SELECT json_agg(c ORDER BY month,day) FROM revora_sign_in_claims c WHERE uid=@uid),
'easter',(SELECT json_agg(c ORDER BY item_uid) FROM revora_easter_gold_pack_uses c WHERE uid=@uid),
'achievement',(SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11228))::text","uid",(long)p.Entity.UID));}
    static void CollectionMemory(GameState p) {
        var items=p.Inventory.Objects.Concat(p.Warehouses.Values.SelectMany(w=>w.Objects)).Concat(p.Entity.StorageItems.Values).OrderBy(i=>i.UID).ToArray();
        Require(items.Length==items.Select(i=>i.UID).Distinct().Count());
        string memory=string.Join(",",items.Select(i=>i.UID+":"+i.ID+":"+i.Warehouse+":"+(i.InWardrobe?1:0)+":"+(i.Bound?1:0)+":"+i.StackSize+":"+i.Bless));
        Require(memory==Convert.ToString(Sql("SELECT string_agg(uid::text||':'||id::text||':'||warehouse::text||':'||inwardrobe::text||':'||bound::text||':'||stacksize::text||':'||bless::text,',' ORDER BY uid) FROM items WHERE entityid=@uid","uid",(long)p.Entity.UID)));
        Require(items.All(i=>i.Days==0));
    }
    static void CollectionBuy() {
        var p=CollectionPlayer();SignEligibility(p);Require(CollectionMountCount(p)==9 && p.Entity.StorageItems.Count==19);
        int paid=Convert.ToInt32(Sql("SELECT count(*) FROM revora_daily_boss_runs WHERE killer=@uid AND state=2 AND paid","uid",(long)p.Entity.UID));
        Require(paid>=0 && paid<=3);
        uint baseCp=p.Entity.ConquerPoints-(uint)paid*1000000u;ulong baseGold=p.Entity.Money-(ulong)paid*10000000UL;
        var latest=Sql("SELECT max(planned) FROM revora_daily_boss_runs");
        DateTime previous=latest==DBNull.Value?planetNow:DateTime.SpecifyKind((DateTime)latest,DateTimeKind.Utc);
        if(previous<planetNow)previous=planetNow;
        var daily=Feature("DailyWorldBosses");DateTime now=previous.ToLocalTime().Date.AddDays(1).ToUniversalTime();
        daily.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        for(int fight=paid;fight<3;fight++) {
            Call(daily,"Tick");
            now=DateTime.SpecifyKind((DateTime)Sql("SELECT min(planned) FROM revora_daily_boss_runs WHERE planned>=@now AND state=0","now",now),DateTimeKind.Utc).AddSeconds(1);
            Call(daily,"Tick");var active=daily.GetField("Active",Hidden).GetValue(null);Require(active!=null);
            var boss=(Entity)active.GetType().GetField("Entity",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(active);
            p.BindWorldMap(boss.MapID);EliminationHit(p,boss);Require(p.Entity.RevoraPoint==(fight+1)*50000u);
            now=now.ToLocalTime().Date.AddDays(1).ToUniversalTime();
        }
        p.BindWorldMap(1002);Require(p.Entity.ConquerPoints==baseCp+3000000);
        var goods=(Array)Feature("RevoraPointShopSystem").GetField("Mounts",Hidden).GetValue(null);
        foreach(object good in goods) {
            var type=good.GetType();uint id=(uint)type.GetField("Id").GetValue(good);
            if(!CollectionMounts().Contains(id) || p.Entity.StorageItems.Values.Any(i=>i.ID==id))continue;
            byte option=(byte)type.GetField("Option").GetValue(good);uint price=(uint)type.GetField("Price").GetValue(good);
            uint before=p.Entity.RevoraPoint;HeartTalk(p,900000014,option);
            Require(p.Entity.RevoraPoint==before-price);CollectionDeposit(p,p.Inventory.Objects.Single(i=>i.ID==id));
        }
        Require(p.Entity.RevoraPoint==105000 && CollectionMountCount(p)==27);
        foreach(uint id in MountPacks) {
            uint before=p.Entity.ConquerPoints;
            ItemHandler.HandleBuyFromNPC(new ItemUsage(true){UID=2888,dwParam=id,dwExtraInfo=1},p);
            Require(p.Entity.ConquerPoints==before-2799);
            var pack=p.Inventory.Objects.Single(i=>i.ID==id);uint output=(uint)Call(Feature("FixedOutputPacks"),"MountFor",id);
            Parallel.For(0,4,n=>ItemHandler.UseItem(pack,p));CollectionDeposit(p,p.Inventory.Objects.Single(i=>i.ID==output));
        }
        p.Entity.MyAchievement.Send();Require(CollectionMountCount(p)==44 && MountUses(p.Entity.UID)==17 && p.Entity.ConquerPoints==baseCp+2952417);
        Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11228","uid",(long)p.Entity.UID)==0);
        CollectionMemory(p);
        DateTime first=new DateTime(now.Year,now.Month,1).AddMonths(1);
        Sql("CREATE TABLE revora_collection_process_probe(start bigint NOT NULL,completed integer NOT NULL,snapshot text NOT NULL,base_cp bigint NOT NULL,base_gold bigint NOT NULL); INSERT INTO revora_collection_process_probe VALUES(@start,0,@snapshot,@cp,@gold)","start",first.ToBinary(),"snapshot",CollectionSnapshot(p),"cp",(long)baseCp,"gold",(long)baseGold);
        Console.WriteLine("[collection-process] collection-buy-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Collection buy did not close.");
    }
    static void CollectionMonth() {
        var p=CollectionPlayer();int month=Convert.ToInt32(Sql("SELECT completed FROM revora_collection_process_probe"));Require(month>=0 && month<3);
        Require(CollectionSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_collection_process_probe")));CollectionMemory(p);
        DateTime first=DateTime.FromBinary((long)Sql("SELECT start FROM revora_collection_process_probe")).AddMonths(month);
        uint[] extra={200481,200499,200500,200543,200563,200570};
        var rewards=(uint[])Feature("EasterGoldPacks").GetField("Rewards",Hidden).GetValue(null);
        for(int day=1;day<=14;day++) {
            SignClock(first.AddDays(day-1));DateTime utc=signNow.ToUniversalTime();
            Feature("DailySignInPacks").GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>utc));
            Feature("EasterGoldPacks").GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>utc));
            uint before=p.Entity.RevoraPoint;Parallel.For(0,4,n=>SignRequest(p,MsgSignIn.Actions.Sign));
            Require(p.Entity.RevoraPoint==before-4000 && SignReceipts(p)==month*14+day);
            foreach(uint id in new uint[]{3100011,3100013,3100014,3100015}) {
                var pack=p.Inventory.Objects.SingleOrDefault(i=>i.ID==id);if(pack==null)continue;
                Require(pack.Days==2 && pack.DayStamp==signNow);ItemHandler.UseItem(pack,p);Require(!p.Inventory.ContainsUID(pack.UID));
            }
            var easter=p.Inventory.Objects.SingleOrDefault(i=>i.ID==720121);
            if(day==7 || day==14) {
                Require(easter!=null);uint design=extra[month*2+(day==14?1:0)];
                int choice=Array.IndexOf(rewards,design)+1;Require(choice>=1 && choice<=30);
                Feature("EasterGoldPacks").GetField("Roll",Hidden).SetValue(null,(Func<int,int,int>)((min,max)=>choice));
                ItemHandler.UseItem(easter,p);ItemHandler.UseItem(easter,p);
                CollectionDeposit(p,p.Inventory.Objects.Single(i=>i.ID==design));
                p.Entity.MyAchievement.Send();
                Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11228","uid",(long)p.Entity.UID)==(CollectionMountCount(p)>=50?1:0));
            } else Require(easter==null);
            CollectionStash(p);Require(p.Inventory.Count==0);
        }
        p.Entity.MyAchievement.Send();Require(CollectionMountCount(p)==46+month*2);
        Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11228","uid",(long)p.Entity.UID)==(month==2?1:0));
        CollectionMemory(p);Sql("UPDATE revora_collection_process_probe SET completed=completed+1,snapshot=@snapshot","snapshot",CollectionSnapshot(p));
        Console.WriteLine("[collection-process] collection-month-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Collection month did not close.");
    }
    static void CollectionVerify() {
        var p=CollectionPlayer();Require(Convert.ToInt32(Sql("SELECT completed FROM revora_collection_process_probe"))==3);
        Require(CollectionSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_collection_process_probe")));CollectionMemory(p);
        Require(CollectionMountCount(p)==50 && p.Entity.StorageItems.Count==60 && p.Warehouses.Values.Sum(w=>w.Count)==174);
        Require(p.Entity.RevoraPoint==21000 && p.Entity.ConquerPoints==482952417UL+(ulong)(long)Sql("SELECT base_cp FROM revora_collection_process_probe") && p.Entity.BoundCps==1500000 && p.Entity.Money==2430000000UL+(ulong)(long)Sql("SELECT base_gold FROM revora_collection_process_probe"));
        Require(SignReceipts(p)==42 && MountUses(p.Entity.UID)==17);
        Require((long)Sql("SELECT count(*) FROM revora_easter_gold_pack_uses WHERE uid=@uid","uid",(long)p.Entity.UID)==6);
        Require((long)Sql("SELECT count(*) FROM revora_daily_sign_in_pack_uses WHERE uid=@uid","uid",(long)p.Entity.UID)==51);
        Require((long)Sql("SELECT count(*) FROM revora_planet_completions WHERE uid=@uid","uid",(long)p.Entity.UID)==8);
        p.Entity.MyAchievement.Send();p.Entity.MyAchievement.Send();
        const int bit=(11228/100-101)*32+11228%100-1;
        Require((System.BitConverter.ToUInt32(p.Entity.MyAchievement.ToArray(),16+bit/32*4)&(1u<<(bit%32)))!=0);
        Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11228 AND source='wardrobe-permanent-v1' AND source_key='mounts:50'","uid",(long)p.Entity.UID)==1);
        Require(CollectionSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_collection_process_probe")));
        Console.WriteLine("[collection-process] collection-verify-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Collection verify did not close.");
    }
}
