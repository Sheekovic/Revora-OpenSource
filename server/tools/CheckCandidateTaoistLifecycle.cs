using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static void TaoistInputs() {
        TaoistWorld();Player(Kernel.Maps[1002]);Sql("UPDATE entities SET class=135,reborn=2 WHERE uid=@uid","uid",(long)FirstUid);nextUid=FirstUid;
        var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();
        foreach(uint id in new uint[]{722734,729780,1072040})p.Inventory.Add(id,0,1);
        p.BindWorldMap(1004);string before=TaoistFingerprint(p);
        HeartTalk(p,10000,156);HeartTalk(p,10000,157);Require(TaoistFingerprint(p)==before && TaoistOperations()==0);
        foreach(var item in p.Inventory.Objects.ToArray())p.Inventory.Remove(item,Revora.Game.Enums.ItemUse.Remove);
        p.BindWorldMap(1002);var shop=p.Map.Npcs[5];p.Entity.X=shop.X;p.Entity.Y=(ushort)(shop.Y+2);
        var buy=new ItemUsage(true){UID=5,dwParam=421005,dwExtraInfo=1,Action=ItemUsage.Mode.BuyFromNPC}.ToArray();
        Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,buy.Length-8,buy);ItemUsage.Process(buy,p);
        var sword=p.Inventory.Objects.Single(i=>i.ID==421005);ItemHandler.EquipItem(new ItemUsage(true){UID=sword.UID,dwParam=4,Action=ItemUsage.Mode.EquipItem},p);
        p.Inventory.Add(3006016,0,1);var panacea=p.Inventory.Objects.Single(i=>i.ID==3006016);before=TaoistFingerprint(p);
        foreach(string mutation in new[]{"short","length","family","seal","oversized"}) {
            var packet=MountUsePacket(panacea.UID);
            if(mutation=="short")packet=packet.Take(24).ToArray();
            if(mutation=="length")Revora.Network.Writer.WriteUInt16(1,0,packet);
            if(mutation=="family")Revora.Network.Writer.WriteUInt16(1111,2,packet);
            if(mutation=="seal")Revora.Network.Writer.WriteUInt64(0,packet.Length-8,packet);
            if(mutation=="oversized"){var large=new byte[129];Array.Copy(packet,large,Math.Min(packet.Length,large.Length));packet=large;Revora.Network.Writer.WriteUInt16(121,0,packet);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,121,packet);}
            ItemUsage.Process(packet,p);Require(TaoistFingerprint(p)==before && sword.ID==421005 && TaoistOperations()==0);
        }
        ItemHandler.UseItem(new ConquerItem(true){ID=3006016,UID=panacea.UID},p);Require(TaoistFingerprint(p)==before && TaoistOperations()==0);
        var valid=MountUsePacket(panacea.UID);ItemUsage.Process(valid,p);ItemUsage.Process(valid,p);
        Require(sword.ID==620005 && TaoistCount(p,3006016)==0 && TaoistOperations()==1);
        p.Inventory.Add(3006020,0,1);var scroll=p.Inventory.Objects.Single(i=>i.ID==3006020);p.BindWorldMap(1004);before=TaoistFingerprint(p);byte level=p.Entity.Level;
        p.Entity.Level=51;HeartTalk(p,10000,162);p.Entity.Level=level;
        p.Trade.InTrade=true;HeartTalk(p,10000,162);p.Trade.InTrade=false;
        p.Entity.X=1;p.Entity.Y=1;p.ActiveNpc=10000;Npcs.GetDialog(new NpcRequest(true){NpcID=10000,OptionID=162},p);
        var moon=p.Map.Npcs[10000];p.Entity.X=moon.X;p.Entity.Y=(ushort)(moon.Y+2);
        Npcs.GetDialog(new NpcRequest(true){NpcID=999999,OptionID=162},p);
        scroll.Lock=1;HeartTalk(p,10000,162);scroll.Lock=0;
        scroll.Days=1;HeartTalk(p,10000,162);scroll.Days=0;
        ushort originalStack=scroll.StackSize;scroll.StackSize=2;HeartTalk(p,10000,162);scroll.StackSize=originalStack;
        Require(!p.Spells.ContainsKey(12390) && TaoistFingerprint(p)==before && (long)Sql("SELECT count(*) FROM revora_taoist_training WHERE uid=@uid","uid",(long)FirstUid)==0);
        HeartTalk(p,10000,162);Require(p.Spells.ContainsKey(12390) && TaoistCount(p,3006020)==0);
        var soul=p.Inventory.Objects.Single(i=>ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7);Require(soul.Bound);
        Sql("CREATE TABLE revora_taoist_input_probe(fingerprint text); INSERT INTO revora_taoist_input_probe VALUES(@fingerprint)","fingerprint",TaoistFingerprint(p));
        EntityTable.SaveEntity(p);System.Console.WriteLine("[taoist-process] taoist-inputs-passed");Environment.Exit(0);
    }
    static void TaoistInputsReload() {
        TaoistWorld();var p=Player(Kernel.Maps[1002],true);SkillTable.LoadSpells(p);
        Require(p.Equipment.TryGetItem(4).ID==620005 && TaoistCount(p,3006016)==0 && p.Spells.ContainsKey(12390) && TaoistCount(p,3006020)==0);
        Require(TaoistFingerprint(p)==(string)Sql("SELECT fingerprint FROM revora_taoist_input_probe") && TaoistOperations()==1
            && (long)Sql("SELECT count(*) FROM revora_taoist_training WHERE uid=@uid","uid",(long)FirstUid)==1);
        System.Console.WriteLine("[taoist-process] taoist-inputs-reload-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Taoist input reload did not close.");
    }
    static void TaoistJournalCheck(GameState p,MsgTaskStatus.QuestData.QuestStatus status,uint leaf,uint crystal,uint panacea) {
        var list=new System.Collections.Generic.List<MsgTaskStatus.QuestData>();Revora.Game.Features.QuestListSystem.Fill(p,list);
        Require(list.Single(i=>(uint)i.UID==3418).Status==status);
        var type=Feature("TaoistEpicJournal");var detail=(MsgTaskDetailInfo)Call(type,"Details",Call(type,"Snapshot",p));
        Require(detail.UID==(QuestID)3418 && detail[1]==leaf && detail[2]==crystal && detail[3]==panacea);
        var bytes=detail.ToArray();Require(System.BitConverter.ToUInt16(bytes,2)==1135 && System.BitConverter.ToUInt32(bytes,8)==3418);
    }
    static void TaoistJournalForge(GameState p) {
        p.ActiveNpc=0;
        var begin=new MsgTaskStatus(true,1){Action=MsgTaskStatus.QuestAction.Begin};begin.Apend(MsgTaskStatus.QuestData.Create((QuestID)3418,MsgTaskStatus.QuestData.QuestStatus.Finished,0));
        MsgTaskStatus.Process(begin.ToArray(),p);
        var quit=new byte[28];Revora.Network.Writer.WriteUInt16(20,0,quit);Revora.Network.Writer.WriteUInt16(2032,2,quit);
        Revora.Network.Writer.WriteUInt32(QuestInfo.ActionBase+3418,8,quit);quit[15]=102;Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,20,quit);
        Revora.Network.PacketHandler.HandlePacket(quit,p);
    }
    static void TaoistJournal() {
        TaoistWorld();Player(Kernel.Maps[1002]);Sql("UPDATE entities SET class=135,reborn=2,conquerpoints=1000 WHERE uid=@uid","uid",(long)FirstUid);nextUid=FirstUid;
        var p=Player(Kernel.Maps[1002],true);p.Inventory.Add(3006016,0,1);
        p.Quests.Accept((QuestID)3418);p.Quests.IncreaseQuestKills((QuestID)3418,99);p.Quests.Save();
        TaoistJournalCheck(p,MsgTaskStatus.QuestData.QuestStatus.Available,0,0,0);TaoistJournalForge(p);
        TaoistJournalCheck(p,MsgTaskStatus.QuestData.QuestStatus.Available,0,0,0);Require(p.Quests.GetQuest((QuestID)3418).Kills==99 && TaoistOperations()==0);
        p.Inventory.Remove(p.Inventory.Objects.Single(i=>i.ID==3006016),Revora.Game.Enums.ItemUse.Remove);
        TaoistAttempt(p,"Paid",99);TaoistJournalCheck(p,MsgTaskStatus.QuestData.QuestStatus.Accepted,0,0,0);
        TaoistAttempt(p,"Paid",0);TaoistJournalCheck(p,MsgTaskStatus.QuestData.QuestStatus.Accepted,1,0,0);
        TaoistAttempt(p,"Elite",0);TaoistJournalCheck(p,MsgTaskStatus.QuestData.QuestStatus.Accepted,1,1,0);
        TaoistAttempt(p,"Super",0);TaoistJournalCheck(p,MsgTaskStatus.QuestData.QuestStatus.Finished,1,1,1);
        p.Inventory.Remove(p.Inventory.Objects.Single(i=>i.ID==3006016),Revora.Game.Enums.ItemUse.Remove);
        TaoistJournalForge(p);TaoistJournalCheck(p,MsgTaskStatus.QuestData.QuestStatus.Finished,1,1,1);
        Require(TaoistCount(p,3006016)==0 && TaoistOperations()==4 && p.Entity.ConquerPoints==946 && p.Quests.GetQuest((QuestID)3418).Kills==99);
        EntityTable.SaveEntity(p);
        // A traded/supplied Crystal may earn the final reward without inventing earlier wins.
        nextUid=FirstUid+1;Player(Kernel.Maps[1002]);Sql("UPDATE entities SET class=135,reborn=2 WHERE uid=@uid","uid",(long)(FirstUid+1));nextUid=FirstUid+1;
        p=Player(Kernel.Maps[1002],true);p.Inventory.Add(3006014,0,1);TaoistAttempt(p,"Super",0);
        TaoistJournalCheck(p,MsgTaskStatus.QuestData.QuestStatus.Finished,0,0,1);EntityTable.SaveEntity(p);
        System.Console.WriteLine("[taoist-process] taoist-journal-passed");Environment.Exit(0);
    }
    static void TaoistJournalReload() {
        TaoistWorld();var p=Player(Kernel.Maps[1002],true);
        TaoistJournalCheck(p,MsgTaskStatus.QuestData.QuestStatus.Finished,1,1,1);TaoistJournalForge(p);
        TaoistJournalCheck(p,MsgTaskStatus.QuestData.QuestStatus.Finished,1,1,1);
        Require(TaoistCount(p,3006016)==0 && TaoistOperations()==4 && p.Entity.ConquerPoints==946 && p.Quests.GetQuest((QuestID)3418).Kills==99);
        p=Player(Kernel.Maps[1002],true);TaoistJournalCheck(p,MsgTaskStatus.QuestData.QuestStatus.Finished,0,0,1);
        System.Console.WriteLine("[taoist-process] taoist-journal-reload-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Taoist journal reload did not close.");
    }
    static GameState TaoistEdgeBag(uint item,int quantity,int fillers=0) {
        Sql("DELETE FROM items WHERE entityid=@uid","uid",(long)FirstUid);
        for(int n=0;n<=fillers;n++) {
            uint id=n==0?item:1088001u;var info=ConquerItemInformation.BaseInformations[id];
            Sql("INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability) VALUES(@item,@type,@owner,0,0,@quantity,1,@dur,@dur)",
                "item",(long)ConquerItem.ItemUID.Next,"type",(int)id,"owner",(long)FirstUid,"quantity",n==0?quantity:1,"dur",(int)info.Durability);
        }
        nextUid=FirstUid;return Player(Kernel.Maps[1002],true);
    }
    static object TaoistMaybePrepare(GameState p,string kind) {
        var type=Feature("TaoistEpicStore");var args=new object[]{p,Enum.Parse(type.GetNestedType("Kind",Hidden),kind),199583u,(byte)1,null,0u};
        lock(p.ItemSyncRoot)return type.GetMethod("Prepare",Hidden).Invoke(null,args);
    }
    static bool TaoistRejected(GameState p,object offer,Func<bool> context) {
        try{lock(p.ItemSyncRoot)Call(Feature("TaoistEpicStore"),"Commit",p,offer,context);return false;}
        catch(System.Reflection.TargetInvocationException e){if(!(e.InnerException is InvalidOperationException))throw;return true;}
    }
    static long TaoistOperations(){return (long)Sql("SELECT count(*) FROM revora_taoist_epic_operations WHERE uid=@uid","uid",(long)FirstUid);}
    static void TaoistEdges() {
        TaoistWorld();var store=Feature("TaoistEpicStore");var rules=Feature("TaoistEpicRules");int pairs=0,missing=0;
        foreach(uint id in ConquerItemInformation.BaseInformations.Keys.Where(i=>i/1000==421)) {
            uint target=(uint)Call(rules,"EpicBacksword",id,(Func<uint,bool>)(i=>ConquerItemInformation.BaseInformations.ContainsKey(i)));
            if(target==0){missing++;System.Console.WriteLine("[taoist-catalog] unsupported="+id);}
            else{Require(target/1000==620 && target%1000==id%1000 && ConquerItemInformation.BaseInformations.ContainsKey(target));pairs++;}
        }
        Require(pairs==280 && missing==5);System.Console.WriteLine("[taoist-catalog] supported=280 unsupported=5");
        Player(Kernel.Maps[1002]);Sql("UPDATE entities SET class=135,reborn=2,mapid=3933 WHERE uid=@uid","uid",(long)FirstUid);
        store.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(n=>99));
        var p=TaoistEdgeBag(3006014,2,39);string before=TaoistFingerprint(p);long count=TaoistOperations();
        Require(p.Inventory.Count==40 && TaoistMaybePrepare(p,"Super")==null && TaoistFingerprint(p)==before && TaoistOperations()==count);
        p=TaoistEdgeBag(3006014,1,39);TaoistAttempt(p,"Super",99);
        Require(p.Inventory.Count==40 && TaoistCount(p,3006014)==0 && TaoistCount(p,3006015)==3 && p.Inventory.Objects.Single(i=>i.ID==3006015).StackSize==3);
        p=TaoistEdgeBag(3006015,101);before=TaoistFingerprint(p);count=TaoistOperations();
        Require(TaoistMaybePrepare(p,"Hossu")==null && TaoistFingerprint(p)==before && TaoistOperations()==count);
        p=TaoistEdgeBag(3006014,2);var offer=TaoistMaybePrepare(p,"Super");Require(offer!=null);
        Sql("UPDATE items SET stacksize=3 WHERE entityid=@uid AND id=3006014","uid",(long)FirstUid);before=TaoistFingerprint(p);count=TaoistOperations();
        Require(TaoistRejected(p,offer,()=>true) && TaoistFingerprint(p)==before && TaoistOperations()==count);
        p=TaoistEdgeBag(3006014,1);offer=TaoistMaybePrepare(p,"Super");before=TaoistFingerprint(p);count=TaoistOperations();
        Require(TaoistRejected(p,offer,()=>false) && TaoistFingerprint(p)==before && TaoistOperations()==count);
        var now=new DateTime(2033,1,1,0,0,0,DateTimeKind.Utc);store.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        offer=TaoistMaybePrepare(p,"Super");now=now.AddMinutes(2);
        Require(TaoistRejected(p,offer,()=>true) && TaoistFingerprint(p)==before && TaoistOperations()==count);
        store.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow));
        p=TaoistEdgeBag(3006014,2);offer=TaoistMaybePrepare(p,"Super");count=TaoistOperations();int successes=0;
        System.Threading.Tasks.Parallel.For(0,8,_=>{if(!TaoistRejected(p,offer,()=>true))System.Threading.Interlocked.Increment(ref successes);});
        Require(successes==1 && TaoistOperations()==count+1 && TaoistCount(p,3006014)==1 && TaoistCount(p,3006015)==3);
        // Earn 51 Fish from supplied Crystals through real NPC offerings without manual merging.
        p=TaoistEdgeBag(3006014,17);count=TaoistOperations();
        for(int n=0;n<17;n++){NinjaAttempt(p,199583);Require(p.Inventory.Count<=18);}
        Require(TaoistCount(p,3006014)==0 && TaoistCount(p,3006015)==51 && p.Inventory.Count==17);
        HeartTalk(p,199584,0);HeartTalk(p,199584,4);HeartTalk(p,199584,20);HeartTalk(p,199584,20);
        Require(TaoistOperations()==count+18 && TaoistCount(p,3006015)==1 && TaoistCount(p,619439)==1 && p.Inventory.Count==2 && p.Inventory.Objects.All(i=>i.Bound));
        Sql("CREATE TABLE revora_taoist_edge_probe(fingerprint text,operations bigint); INSERT INTO revora_taoist_edge_probe VALUES(@fingerprint,@operations)","fingerprint",TaoistFingerprint(p),"operations",TaoistOperations());
        EntityTable.SaveEntity(p);System.Console.WriteLine("[taoist-process] taoist-edges-passed");Environment.Exit(0);
    }
    static void TaoistEdgesReload() {
        TaoistWorld();var p=Player(Kernel.Maps[1002],true);
        Require(p.Inventory.Count==2 && TaoistCount(p,3006015)==1 && TaoistCount(p,619439)==1 && p.Inventory.Objects.All(i=>i.Bound));
        Require(TaoistFingerprint(p)==(string)Sql("SELECT fingerprint FROM revora_taoist_edge_probe") && TaoistOperations()==(long)Sql("SELECT operations FROM revora_taoist_edge_probe"));
        HeartTalk(p,199584,20);Require(TaoistOperations()==(long)Sql("SELECT operations FROM revora_taoist_edge_probe"));
        System.Console.WriteLine("[taoist-process] taoist-edges-reload-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Taoist edge reload did not close.");
    }
    static void TaoistTraining() {
        TaoistWorld();
        Sql("CREATE TABLE revora_taoist_training_probe(uid bigint PRIMARY KEY,fingerprint text)");
        for(int n=0;n<2;n++) {
            uint uid=FirstUid+(uint)n;nextUid=uid;Player(Kernel.Maps[1002]);
            Sql("UPDATE entities SET class=@class,reborn=2 WHERE uid=@uid","class",n==0?135:145,"uid",(long)uid);
            nextUid=uid;var p=Player(Kernel.Maps[1002],true);p.Inventory.Add(3006015,0,1);EntityTable.SaveEntity(p);
            // Supplied Fish isolate exchange/learning; the hunted producer is a separate route suite.
            Sql("UPDATE items SET stacksize=100,bound=1 WHERE entityid=@uid AND id=3006015","uid",(long)uid);
            nextUid=uid;p=Player(Kernel.Maps[1002],true);p.BindWorldMap(3933);
            foreach(byte option in new byte[]{1,2,3,5,4}) {
                HeartTalk(p,199584,0);HeartTalk(p,199584,option);HeartTalk(p,199584,20);HeartTalk(p,199584,20);
            }
            Require(TaoistCount(p,3006015)==10 && p.Inventory.Objects.Single(i=>i.ID==619439).Bound);
            foreach(uint item in new uint[]{3006017,3006018,3006019,3006020})Require(TaoistCount(p,item)==1 && p.Inventory.Objects.Single(i=>i.ID==item).Bound);
            HeartTalk(p,199584,254);Require(p.Entity.MapID==1004);
            byte first=n==0?(byte)162:(byte)160,second=n==0?(byte)163:(byte)161;
            ushort firstSpell=n==0?(ushort)12390:(ushort)12400,secondSpell=n==0?(ushort)12370:(ushort)12380;
            uint firstScroll=n==0?3006020u:3006019u;
            foreach(byte option in n==0?new byte[]{160,161}:new byte[]{162,163})HeartTalk(p,10000,option);
            Require(p.Spells.Count==0 && TaoistCount(p,firstScroll)==1);
            if(n==0) {
                string before=TaoistFingerprint(p);
                Sql("CREATE FUNCTION revora_taoist_training_probe_fail() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'owned training rollback probe'; END $$; CREATE TRIGGER revora_taoist_training_probe_fail BEFORE INSERT ON revora_taoist_training FOR EACH ROW EXECUTE FUNCTION revora_taoist_training_probe_fail()");
                HeartTalk(p,10000,first);
                Require(TaoistFingerprint(p)==before && (long)Sql("SELECT count(*) FROM skills WHERE entityid=@uid AND id=@spell","uid",(long)uid,"spell",(int)firstSpell)==0
                    && (long)Sql("SELECT count(*) FROM revora_taoist_training WHERE uid=@uid","uid",(long)uid)==0);
                Sql("DROP TRIGGER revora_taoist_training_probe_fail ON revora_taoist_training; DROP FUNCTION revora_taoist_training_probe_fail()");
                nextUid=uid;p=Player(Kernel.Maps[1002],true);SkillTable.LoadSpells(p);
                Require(!p.Spells.ContainsKey(firstSpell) && TaoistCount(p,firstScroll)==1);
            }
            System.Threading.Tasks.Parallel.For(0,8,_=>HeartTalk(p,10000,first));HeartTalk(p,10000,second);
            Require(p.Spells.ContainsKey(firstSpell) && p.Spells.ContainsKey(secondSpell) && TaoistCount(p,firstScroll)==0);
            Require((long)Sql("SELECT count(*) FROM revora_taoist_training WHERE uid=@uid","uid",(long)uid)==2);
            HeartTalk(p,10000,151);HeartTalk(p,199584,0);HeartTalk(p,199584,n==0?(byte)5:(byte)3);HeartTalk(p,199584,20);
            HeartTalk(p,199584,254);HeartTalk(p,10000,first);
            Require(TaoistCount(p,firstScroll)==1 && TaoistCount(p,3006015)==0 && p.Entity.ConquerPoints==0);
            Sql("INSERT INTO revora_taoist_training_probe VALUES(@uid,@fingerprint)","uid",(long)uid,"fingerprint",TaoistFingerprint(p));EntityTable.SaveEntity(p);
        }
        System.Console.WriteLine("[taoist-process] taoist-training-passed");Environment.Exit(0);
    }
    static void TaoistTrainingReload() {
        TaoistWorld();
        for(int n=0;n<2;n++) {
            uint uid=FirstUid+(uint)n;nextUid=uid;var p=Player(Kernel.Maps[1002],true);SkillTable.LoadSpells(p);
            ushort first=n==0?(ushort)12390:(ushort)12400,second=n==0?(ushort)12370:(ushort)12380;
            Require(p.Spells.ContainsKey(first) && p.Spells.ContainsKey(second));
            HeartTalk(p,10000,n==0?(byte)162:(byte)160);HeartTalk(p,10000,n==0?(byte)163:(byte)161);
            Require((string)Sql("SELECT fingerprint FROM revora_taoist_training_probe WHERE uid=@uid","uid",(long)uid)==TaoistFingerprint(p));
            Require((long)Sql("SELECT count(*) FROM revora_taoist_training WHERE uid=@uid","uid",(long)uid)==2
                && (long)Sql("SELECT count(*) FROM revora_taoist_epic_operations WHERE uid=@uid","uid",(long)uid)==6);
            Require(TaoistCount(p,3006015)==0 && p.Entity.ConquerPoints==0 && p.Inventory.Objects.Single(i=>i.ID==619439).Bound);
        }
        System.Console.WriteLine("[taoist-process] taoist-training-reload-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Taoist training reload did not close.");
    }
    static void TaoistWorld() {
        var quest=Feature("TaoistEpicQuest");Map map;
        Require((bool)quest.GetProperty("Enabled",Hidden).GetValue(null,null));
        Require(Kernel.Maps.TryGetValue(3933,out map));
        Require(map.Npcs.ContainsKey(199580) && map.Npcs.ContainsKey(199584) && map.Floor[84,77,MapObjectType.Player]);
        var moon=Kernel.Maps[1004].Npcs[10000];System.Console.WriteLine("[taoist-world] moon="+moon.X+","+moon.Y);
    }
    static void TaoistHunt() {
        TaoistWorld();Player(Kernel.Maps[1002]);Sql("UPDATE entities SET class=135,reborn=2 WHERE uid=@uid","uid",(long)FirstUid);nextUid=FirstUid;
        var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();
        var shop=p.Map.Npcs[5];p.Entity.X=shop.X;p.Entity.Y=(ushort)(shop.Y+2);
        uint price=ConquerItemInformation.BaseInformations[421005].GoldWorth;
        var buy=new ItemUsage(true){UID=5,dwParam=421005,dwExtraInfo=1,Action=ItemUsage.Mode.BuyFromNPC}.ToArray();
        Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,buy.Length-8,buy);ItemUsage.Process(buy,p);
        var sword=p.Inventory.Objects.Single(i=>i.ID==421005);
        ItemHandler.EquipItem(new ItemUsage(true){UID=sword.UID,dwParam=4,Action=ItemUsage.Mode.EquipItem},p);
        Require(p.Equipment.TryGetItem(4)!=null && p.Equipment.TryGetItem(4).UID==sword.UID && p.Entity.Money==100000-price);
        p.BindWorldMap(1004);HeartTalk(p,10000,150);HeartTalk(p,10000,154);Require(p.Entity.MapID==1700);
        Config.MoneyDropEnabled=false;Config.KillConquerPointsEnabled=false;
        var sources=p.Map.Entities.Values.Where(e=>e.MonsterInfo!=null && e.MonsterInfo.ID>=3600 && e.MonsterInfo.ID<=3631 && !e.Dead).Take(6).ToArray();Require(sources.Length==6);
        for(int n=0;n<6;n++) {
            int color=n%2;Feature("NinjaFlowerHunt").GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>color));
            var before=p.Map.FloorItems.Keys.ToArray();EliminationHit(p,sources[n]);
            var drop=p.Map.FloorItems.Values.Single(i=>!before.Contains(i.UID) && (i.ItemID==722732 || i.ItemID==722736));
            p.Entity.X=drop.X;p.Entity.Y=drop.Y;
            var pickup=new FloorItem(true){UID=drop.UID,Type=FloorItem.Remove,X=drop.X,Y=drop.Y,ItemID=drop.ItemID}.ToArray();
            Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,pickup.Length-8,pickup);FloorItem.Process(pickup,p);FloorItem.Process(pickup,p);
            Require(drop.PickedUpAlready && p.Inventory.ContainsUID(drop.Item.UID));
        }
        Require(TaoistCount(p,722732)==3 && TaoistCount(p,722736)==3 && (long)Sql("SELECT count(*) FROM revora_ninja_flower_pickups WHERE collector=@uid","uid",(long)p.Entity.UID)==6);
        HeartTalk(p,3604,90);Require(p.Entity.MapID==1004);HeartTalk(p,10000,151);Require(p.Entity.MapID==3933 && p.Entity.X==84 && p.Entity.Y==77);
        Feature("TaoistEpicStore").GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(n=>0));
        foreach(uint npc in new uint[]{199581,199582,199583}){NinjaAttempt(p,npc);Require(!p.Map.Floor[p.Map.Npcs[npc].X,p.Map.Npcs[npc].Y,MapObjectType.Player]);}
        Require(TaoistCount(p,3006016)==1 && TaoistCount(p,722732)==0 && TaoistCount(p,722736)==0 && p.Entity.ConquerPoints==0);
        HeartTalk(p,199580,0);HeartTalk(p,199580,4);Require(sword.ID==421005);
        Sql("CREATE TABLE revora_taoist_route_probe(weapon bigint,panacea bigint,silver bigint,fingerprint text); INSERT INTO revora_taoist_route_probe(weapon,panacea,silver) VALUES(@weapon,@panacea,@silver)","weapon",(long)sword.UID,"panacea",(long)p.Inventory.Objects.Single(i=>i.ID==3006016).UID,"silver",(long)p.Entity.Money);
        EntityTable.SaveEntity(p);System.Console.WriteLine("[taoist-process] taoist-hunt-passed");Environment.Exit(0);
    }
    static void TaoistAwaken() {
        TaoistWorld();var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();Require(p.Entity.MapID==3933);
        var sword=p.Equipment.TryGetItem(4);Require(sword.ID==421005 && TaoistCount(p,3006016)==1);
        HeartTalk(p,199580,20);Require(sword.ID==421005 && TaoistCount(p,3006016)==1);
        HeartTalk(p,199580,0);HeartTalk(p,199580,4);HeartTalk(p,199580,20);HeartTalk(p,199580,20);
        Require(sword.ID==620005 && TaoistCount(p,3006016)==0 && p.Entity.ConquerPoints==0);
        Sql("UPDATE revora_taoist_route_probe SET fingerprint=@fingerprint","fingerprint",TaoistFingerprint(p));
        HeartTalk(p,199580,254);Require(p.Entity.MapID==1004);
        System.Console.WriteLine("[taoist-process] taoist-awaken-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Taoist awakening did not close.");
    }
    static void TaoistEarnedReload() {
        TaoistWorld();var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();Require(p.Entity.MapID==1004);
        var sword=p.Equipment.TryGetItem(4);Require(sword.ID==620005 && sword.UID==Convert.ToUInt32(Sql("SELECT weapon FROM revora_taoist_route_probe")));
        Require(p.Entity.ConquerPoints==0 && p.Entity.Money==Convert.ToUInt64(Sql("SELECT silver FROM revora_taoist_route_probe"))
            && TaoistFingerprint(p)==(string)Sql("SELECT fingerprint FROM revora_taoist_route_probe") && TaoistCount(p,3006016)==0);
        Require((long)Sql("SELECT count(*) FROM revora_taoist_epic_operations WHERE uid=@uid","uid",(long)p.Entity.UID)==4);
        var soul=p.Inventory.Objects.Single(i=>ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7);Require(soul.Bound);
        Require((long)Sql("SELECT count(*) FROM revora_taoist_epic_rewards WHERE item_uid=@item AND bound=true","item",(long)soul.UID)==1);
        System.Console.WriteLine("[taoist-process] taoist-earned-reload-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Taoist earned reload did not close.");
    }
    static object TaoistPrepare(GameState p,string kind) {
        var type=Feature("TaoistEpicStore");
        var args=new object[]{p,Enum.Parse(type.GetNestedType("Kind",Hidden),kind),199580u,(byte)1,null,0u};
        var offer=type.GetMethod("Prepare",Hidden).Invoke(null,args);
        if(offer==null){System.Console.WriteLine("[taoist-probe] rejected "+kind+": "+args[4]);throw new InvalidOperationException("Taoist offer rejected.");}return offer;
    }
    static void TaoistAttempt(GameState p,string kind,int roll) {
        Feature("TaoistEpicStore").GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(n=>roll));
        lock(p.ItemSyncRoot)Call(Feature("TaoistEpicStore"),"Commit",p,TaoistPrepare(p,kind),(Func<bool>)(()=>true));
    }
    static long TaoistCount(GameState p,uint id){return p.Inventory.Objects.Where(i=>i.ID==id).Sum(i=>(long)Math.Max(1,(int)i.StackSize));}
    static void TaoistFlowers(GameState p) {for(int n=0;n<3;n++){p.Inventory.Add(722732,0,1);p.Inventory.Add(722736,0,1);}}
    static string TaoistFingerprint(GameState p) {
        return (string)Sql("SELECT string_agg(uid::text||':'||id::text||':'||bound::text||':'||stacksize::text,',' ORDER BY uid) FROM items WHERE entityid=@uid","uid",(long)p.Entity.UID);
    }
    static void TaoistStore() {
        var store=Feature("TaoistEpicStore");Call(store,"Initialize");Call(store,"Initialize");
        Player(Kernel.Maps[1002]);Sql("UPDATE entities SET class=135,reborn=2,conquerpoints=1000 WHERE uid=@uid","uid",(long)FirstUid);nextUid=FirstUid;
        var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();
        Require((bool)Call(store,"Eligible",p) && p.Account.State==AccountTable.AccountState.Player && !p.Fake);
        // Qualifications, CPs and flowers are supplied fixtures. This is store coverage.
        TaoistFlowers(p);TaoistAttempt(p,"Flowers",99);
        Require(TaoistCount(p,722732)==0 && TaoistCount(p,722736)==0 && TaoistCount(p,3006013)==0 && p.Entity.ConquerPoints==1000);
        TaoistAttempt(p,"Paid",99);Require(p.Entity.ConquerPoints==973 && TaoistCount(p,3006013)==0);
        TaoistAttempt(p,"Paid",0);TaoistAttempt(p,"Elite",99);Require(TaoistCount(p,3006015)==1 && p.Entity.ConquerPoints==946);
        TaoistFlowers(p);TaoistAttempt(p,"Flowers",0);TaoistAttempt(p,"Elite",0);TaoistAttempt(p,"Super",99);
        Require(TaoistCount(p,3006015)==4 && TaoistCount(p,3006014)==0);
        TaoistFlowers(p);TaoistAttempt(p,"Flowers",0);TaoistAttempt(p,"Elite",0);TaoistAttempt(p,"Super",0);
        Require(TaoistCount(p,3006016)==1);
        var shop=p.Map.Npcs[5];p.Entity.X=shop.X;p.Entity.Y=(ushort)(shop.Y+2);
        uint price=ConquerItemInformation.BaseInformations[421005].GoldWorth;
        var buy=new ItemUsage(true){UID=5,dwParam=421005,dwExtraInfo=1,Action=ItemUsage.Mode.BuyFromNPC}.ToArray();
        Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,buy.Length-8,buy);ItemUsage.Process(buy,p);
        Require(TaoistCount(p,421005)==1);var sword=p.Inventory.Objects.Single(i=>i.ID==421005);
        ItemHandler.EquipItem(new ItemUsage(true){UID=sword.UID,dwParam=4,Action=ItemUsage.Mode.EquipItem},p);
        Require(p.Equipment.TryGetItem(4)!=null && p.Equipment.TryGetItem(4).UID==sword.UID && p.Entity.Money==100000-price);
        // Load supplied enhancements from persistence before observing the conversion.
        EntityTable.SaveEntity(p);
        Sql("UPDATE items SET plus=12,bless=7,enchant=255,socketone=73,sockettwo=73 WHERE uid=@item","item",(long)sword.UID);
        Sql("UPDATE items SET bound=1 WHERE entityid=@uid AND id=3006016","uid",(long)p.Entity.UID);
        nextUid=FirstUid;p=Player(Kernel.Maps[1002],true);p.LoadItemStats();
        sword=p.Equipment.TryGetItem(4);Require(sword.Plus==12 && sword.Bless==7);
        string before=TaoistFingerprint(p);long count=(long)Sql("SELECT count(*) FROM revora_taoist_epic_operations");
        object offer;lock(p.ItemSyncRoot)offer=TaoistPrepare(p,"Awaken");
        Sql("CREATE FUNCTION revora_taoist_probe_fail() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'owned Taoist rollback probe'; END $$; CREATE TRIGGER revora_taoist_probe_fail BEFORE INSERT ON revora_taoist_epic_rewards FOR EACH ROW EXECUTE FUNCTION revora_taoist_probe_fail()");
        bool failed=false;
        try{lock(p.ItemSyncRoot)Call(store,"Commit",p,offer,(Func<bool>)(()=>true));}catch(System.Reflection.TargetInvocationException){failed=true;}
        Require(failed && TaoistFingerprint(p)==before && TaoistCount(p,3006016)==1 && sword.ID==421005
            && (long)Sql("SELECT count(*) FROM revora_taoist_epic_operations")==count);
        Sql("DROP TRIGGER revora_taoist_probe_fail ON revora_taoist_epic_rewards; DROP FUNCTION revora_taoist_probe_fail()");
        lock(p.ItemSyncRoot)Call(store,"Commit",p,offer,(Func<bool>)(()=>true));
        Require(sword.ID==620005 && sword.Bound && sword.Plus==12 && sword.Bless==7 && sword.Enchant==255
            && (int)sword.SocketOne==73 && (int)sword.SocketTwo==73 && TaoistCount(p,3006016)==0);
        failed=false;try{lock(p.ItemSyncRoot)Call(store,"Commit",p,offer,(Func<bool>)(()=>true));}catch(System.Reflection.TargetInvocationException){failed=true;}
        Require(failed && (long)Sql("SELECT count(*) FROM revora_taoist_epic_operations")==count+1);
        var soul=p.Inventory.Objects.Single(i=>ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7);Require(soul.Bound);
        Sql("CREATE TABLE revora_taoist_process_probe(weapon bigint,soul bigint,cp bigint,silver bigint,fingerprint text); INSERT INTO revora_taoist_process_probe VALUES(@weapon,@soul,@cp,@silver,@fingerprint)",
            "weapon",(long)sword.UID,"soul",(long)soul.UID,"cp",(long)p.Entity.ConquerPoints,"silver",(long)p.Entity.Money,"fingerprint",TaoistFingerprint(p));
        EntityTable.SaveEntity(p);System.Console.WriteLine("[taoist-process] taoist-store-passed");Environment.Exit(0);
    }
    static void TaoistReload() {
        Call(Feature("TaoistEpicStore"),"Initialize");var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();
        var sword=p.Equipment.TryGetItem(4);
        Require(sword.UID==Convert.ToUInt32(Sql("SELECT weapon FROM revora_taoist_process_probe")) && sword.ID==620005 && sword.Bound
            && sword.Plus==12 && sword.Bless==7 && sword.Enchant==255 && (int)sword.SocketOne==73 && (int)sword.SocketTwo==73);
        Require(TaoistFingerprint(p)==(string)Sql("SELECT fingerprint FROM revora_taoist_process_probe") && TaoistCount(p,3006016)==0 && TaoistCount(p,3006015)==4);
        Require(p.Entity.ConquerPoints==Convert.ToUInt32(Sql("SELECT cp FROM revora_taoist_process_probe")) && p.Entity.Money==Convert.ToUInt64(Sql("SELECT silver FROM revora_taoist_process_probe")));
        uint soulUid=Convert.ToUInt32(Sql("SELECT soul FROM revora_taoist_process_probe"));var soul=p.Inventory.Objects.Single(i=>i.UID==soulUid);
        Require(soul.Bound && ConquerItemInformation.BaseInformations[soul.ID].PurificationLevel==7);
        Require((long)Sql("SELECT count(*) FROM revora_taoist_epic_operations WHERE kind=5")==1);
        System.Console.WriteLine("[taoist-process] taoist-reload-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Taoist reload did not close.");
    }
    static void TaoistCombatTraining(bool reload=false) {
        var p=Player(Kernel.Maps[1002],true);SkillTable.LoadSpells(p);SkillTable.LoadProficiencies(p);p.LoadItemStats();
        Require(p.Equipment.TryGetItem(4).ID/1000==620);
        if(reload) {
            foreach(ushort family in new ushort[]{620}) {
                Require(p.Proficiencies.ContainsKey(family));var prof=p.Proficiencies[family];
                Require(prof.Level>=12 && (long)Sql("SELECT count(*) FROM revora_taoist_mastery_probe WHERE uid=@uid AND family=@family AND level=@level AND xp=@xp","uid",(long)p.Entity.UID,"family",(int)family,"level",(int)prof.Level,"xp",(long)prof.Experience)==1);
            }
            foreach(int id in new[]{11222})Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=@id","uid",(long)p.Entity.UID,"id",id)==1);
            p.Entity.MyAchievement.Send();p.Entity.MyAchievement.Send();
            foreach(int id in new[]{11222})Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=@id","uid",(long)p.Entity.UID,"id",id)==1);
            System.Console.WriteLine("[taoist-process] taoist-train-reload-passed");Server.CommandsAI("@close");throw new Exception("Taoist training reload did not close.");
        }
        ushort savedMap=p.Entity.MapID,savedX=p.Entity.X,savedY=p.Entity.Y;
        Config.MoneyDropEnabled=false;Config.KillConquerPointsEnabled=false;
        Feature("MonsterEquipmentDrops").GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>max-1));
        var targets=Kernel.Maps.Values.SelectMany(m=>m.Entities.Values).Where(e=>!e.Dead && e.MonsterInfo!=null && !e.MonsterInfo.Boss && !e.MonsterInfo.Guard && !e.MonsterInfo.Reviver && !e.Companion).OrderByDescending(e=>e.Hitpoints).Take(128).ToArray();
        Require(targets.Length>0);int swings=0,kills=0;var until=DateTime.UtcNow.AddSeconds(90);
        foreach(var target in targets) {
            p.BindWorldMap(target.MapID);p.Entity.X=target.X;p.Entity.Y=target.Y;p.Screen.Add(target);int stalled=0;
            while(!target.Dead && !p.Entity.Dead && DateTime.UtcNow<until && swings<50000) {
                if(new ushort[]{620}.All(id=>p.Proficiencies.ContainsKey(id) && p.Proficiencies[id].Level>=12))break;
                // Only the attack cooldown clock is advanced. Damage, XP rates,
                // weapon metadata, monster HP and proficiency levels are untouched.
                uint beforeHp=target.Hitpoints;
                p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);
                Attack.HandleAttack(new Attack(true){Attacker=p.Entity.UID,Attacked=target.UID,X=target.X,Y=target.Y,AttackType=Attack.Melee},p);swings++;
                if(target.Hitpoints==beforeHp)stalled++;else stalled=0;
                if(stalled>=32)break;
            }
            if(target.Dead)kills++;
            if(DateTime.UtcNow>=until || swings>=50000 || new ushort[]{620}.All(id=>p.Proficiencies.ContainsKey(id) && p.Proficiencies[id].Level>=12))break;
        }
        SkillTable.SaveProficiencies(p);
        foreach(ushort family in new ushort[]{620}) {
            Require(p.Proficiencies.ContainsKey(family));var prof=p.Proficiencies[family];
            System.Console.WriteLine("[taoist-training] family="+family+" level="+prof.Level+" xp="+prof.Experience+" swings="+swings+" kills="+kills);
        }
        p.BindWorldMap(savedMap);p.Entity.X=savedX;p.Entity.Y=savedY;Require(EntityTable.SaveEntity(p));
        Require(new ushort[]{620}.All(id=>p.Proficiencies[id].Level>=12));
        p.Entity.MyAchievement.Send();
        foreach(int id in new[]{11222})Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=@id","uid",(long)p.Entity.UID,"id",id)==1);
        Sql("CREATE TABLE revora_taoist_mastery_probe(uid bigint NOT NULL,family integer PRIMARY KEY,level integer NOT NULL,xp bigint NOT NULL)");
        foreach(ushort family in new ushort[]{620})Sql("INSERT INTO revora_taoist_mastery_probe VALUES(@uid,@family,@level,@xp)","uid",(long)p.Entity.UID,"family",(int)family,"level",(int)p.Proficiencies[family].Level,"xp",(long)p.Proficiencies[family].Experience);
        System.Console.WriteLine("[taoist-process] taoist-train-passed");Server.CommandsAI("@close");throw new Exception("Taoist training did not close.");
    }
    static void TaoistFlowerSocketHunt(System.Diagnostics.Process child,GameState p) {
        var roll=Feature("NinjaFlowerHunt").GetField("Roll",Hidden);var old=roll.GetValue(null);
        try {
            Config.MoneyDropEnabled=false;Config.KillConquerPointsEnabled=false;
            Feature("MonsterEquipmentDrops").GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>max-1));
            TeamWireSend(child,"pirate-passage:10000:154:1700");
            var monsters=p.Map.Entities.Values.Where(e=>e.MonsterInfo!=null && (e.MonsterInfo.ID==3609 || e.MonsterInfo.ID==3613 || e.MonsterInfo.ID==3629)).OrderBy(e=>e.MonsterInfo.MaxAttack).ToArray();Require(monsters.Length>=6);
            for(int n=0;n<6;n++) {
                var monster=monsters.FirstOrDefault(e=>!e.Dead);Require(monster!=null);int color=n%2;
                roll.SetValue(null,(Func<int,int>)(max=>color));var before=p.Map.FloorItems.Keys.ToArray();
                p.Entity.X=monster.X;p.Entity.Y=monster.Y;p.Screen.Add(monster);
                for(int swing=0;swing<80 && !monster.Dead;swing++) {
                    System.Threading.Thread.Sleep(550);
                    if(p.Entity.Hitpoints<p.Entity.MaxHitpoints*3/4) {
                        var medicine=p.Inventory.Objects.FirstOrDefault(i=>i.ID==1002050);Require(medicine!=null);
                        TeamWireSend(child,"pirate-use:"+medicine.UID);
                    }
                    TeamWireSend(child,"pirate-hit:"+monster.UID);
                }
                Require(monster.Dead && !p.Entity.Dead);
                var drop=p.Map.FloorItems.Values.Single(i=>!before.Contains(i.UID) && (i.ItemID==722732 || i.ItemID==722736));
                p.Entity.X=drop.X;p.Entity.Y=drop.Y;
                TeamWireSend(child,"pirate-pick:"+drop.UID+":"+drop.Item.UID+":"+drop.ItemID+":"+drop.X+":"+drop.Y);
                Require(drop.PickedUpAlready && p.Inventory.ContainsUID(drop.Item.UID));
                System.Console.WriteLine("[taoist-flower-wire] earned pickup "+(n+1)+" family="+monster.MonsterInfo.ID);
            }
            Require(TaoistCount(p,722732)==3 && TaoistCount(p,722736)==3 && p.Entity.ConquerPoints==1000);
            var exit=p.Map.Npcs[3604];p.Entity.X=exit.X;p.Entity.Y=(ushort)(exit.Y+2);
            TeamWireSend(child,"pirate-passage:3604:90:1004");
        }finally{roll.SetValue(null,old);}
    }
    static void TaoistWire(bool fire=false,bool reload=false) {
        TaoistWorld();uint uid=FirstUid+(fire?1u:0u);nextUid=uid;
        if(!reload){Player(Kernel.Maps[1002]);Sql("UPDATE entities SET class=@class,reborn=2,conquerpoints=1000 WHERE uid=@uid","class",fire?145:135,"uid",(long)uid);}
        nextUid=uid;var p=Player(Kernel.Maps[1002],true);SkillTable.LoadSpells(p);
        if(!reload){
            var shop=p.Map.Npcs[5];p.Entity.X=shop.X;p.Entity.Y=(ushort)(shop.Y+2);
            var buy=new ItemUsage(true){UID=5,dwParam=421005,dwExtraInfo=1,Action=ItemUsage.Mode.BuyFromNPC}.ToArray();Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,buy.Length-8,buy);ItemUsage.Process(buy,p);
            var sword=p.Inventory.Objects.Single(i=>i.ID==421005);ItemHandler.EquipItem(new ItemUsage(true){UID=sword.UID,dwParam=4,Action=ItemUsage.Mode.EquipItem},p);
            Require(p.Equipment.TryGetItem(4).UID==sword.UID);if(!fire)Require(p.Inventory.Add(1002050,0,30));Require(EntityTable.SaveEntity(p));
        }
        string name="TeamProbe"+(fire?1:0),password=Guid.NewGuid().ToString("N");var account=new AccountTable(name);
        if(!account.exists){account.Password=password;account.State=AccountTable.AccountState.Player;account.EntityID=uid;account.Insert();}account.Password=password;account.Save();Require(account.HasSrpVerifier && account.EntityID==uid);
        var moon=Kernel.Maps[1004].Npcs[10000];Sql("UPDATE entities SET owner=@owner,mapid=1004,x=@x,y=@y WHERE uid=@uid","owner",name,"x",(int)moon.X,"y",(int)moon.Y+2,"uid",(long)uid);Kernel.GamePool.Remove(uid);
        var start=new System.Diagnostics.ProcessStartInfo(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"CandidateTeamWireProbe.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Environment.CurrentDirectory};
        start.EnvironmentVariables["REVORA_TAOIST_WIRE"]="1";start.EnvironmentVariables["REVORA_TEAM_WIRE_ROLE"]=fire?"1":"0";start.EnvironmentVariables["REVORA_CANDIDATE_ACCOUNT_PASSWORD"]=password;start.EnvironmentVariables.Remove("REVORA_DATABASE_PASSWORD");
        using(var child=System.Diagnostics.Process.Start(start)){
            start.EnvironmentVariables.Remove("REVORA_CANDIDATE_ACCOUNT_PASSWORD");password=null;var errors=child.StandardError.ReadToEndAsync();
            var roll=Feature("TaoistEpicStore").GetField("Roll",Hidden);var previous=roll.GetValue(null);
            try{
                TeamWireRead(child,"[team-wire] ready");p=Kernel.GamePool[uid];Require(p.Account.State==AccountTable.AccountState.Player && !p.Fake);
                if(!reload){
                    if(!fire)TaoistFlowerSocketHunt(child,p);
                    TeamWireSend(child,"taoist-enter");uint sword=p.Equipment.TryGetItem(4).UID;
                    for(int trial=0;trial<31;trial++){
                        roll.SetValue(null,(Func<int,int>)(max=>0));PirateSocketTrade(child,p,199581,!fire && trial==0?(byte)1:(byte)2);PirateSocketTrade(child,p,199582,1);
                        roll.SetValue(null,(Func<int,int>)(max=>trial==0?0:99));PirateSocketTrade(child,p,199583,1);
                        if(trial==0){var panacea=p.Inventory.Objects.Single(i=>i.ID==3006016);TeamWireSend(child,"pirate-use:"+panacea.UID);TeamWireSend(child,"pirate-use:"+panacea.UID);Require(!p.Inventory.ContainsUID(panacea.UID));}
                    }
                    Require(p.Entity.ConquerPoints==(fire?163u:190u) && TaoistCount(p,3006015)==90 && p.Equipment.TryGetItem(4).UID==sword && p.Equipment.TryGetItem(4).ID==620005);
                    foreach(byte choice in new byte[]{1,2,3,5,4})PirateSocketTrade(child,p,199584,choice);
                    Require(TaoistCount(p,3006015)==0 && TaoistCount(p,619439)==1);
                    var sun=p.Map.Npcs[199580];p.Entity.X=sun.X;p.Entity.Y=(ushort)(sun.Y+2);TeamWireSend(child,"taoist-return");
                    Sql("CREATE TABLE IF NOT EXISTS revora_taoist_wire_probe(uid bigint PRIMARY KEY,sword bigint,fingerprint text)");Sql("INSERT INTO revora_taoist_wire_probe(uid,sword) VALUES(@uid,@sword)","uid",(long)uid,"sword",(long)sword);
                }
                // Both branches attempt every art; the opposite path must keep its scrolls.
                TeamWireSend(child,"pirate-dialog:10000:0");foreach(byte option in new byte[]{160,161,162,163,160,161,162,163})TeamWireSend(child,"pirate-dialog:10000:"+option);
                ushort[] learned=fire?new ushort[]{12400,12380}:new ushort[]{12390,12370},denied=fire?new ushort[]{12390,12370}:new ushort[]{12400,12380};
                Require(learned.All(id=>p.Spells.ContainsKey(id)) && denied.All(id=>!p.Spells.ContainsKey(id)));
                foreach(uint type in fire?new uint[]{3006020,3006018}:new uint[]{3006019,3006017})Require(TaoistCount(p,type)==1);
                foreach(uint type in fire?new uint[]{3006019,3006017}:new uint[]{3006020,3006018})Require(TaoistCount(p,type)==0);
                Require((bool)Sql("SELECT count(*)=99 AND sum(cost)=@cost FROM revora_taoist_epic_operations WHERE uid=@uid","uid",(long)uid,"cost",fire?837:810));

                if(!fire) {
                    Require(TaoistCount(p,722732)==0 && TaoistCount(p,722736)==0);
                    Require((long)Sql("SELECT count(*) FROM revora_ninja_flower_pickups WHERE collector=@uid","uid",(long)uid)==6);
                }
                Require(p.Entity.ConquerPoints==(fire?163u:190u) && p.Equipment.TryGetItem(4).ID==620005 && p.Equipment.TryGetItem(4).UID==Convert.ToUInt32(Sql("SELECT sword FROM revora_taoist_wire_probe WHERE uid=@uid","uid",(long)uid)));
                Require(p.Inventory.Objects.Count(i=>i.Bound && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)==1 && TaoistCount(p,619439)==1);
                Require((long)Sql("SELECT count(*) FROM revora_taoist_training WHERE uid=@uid","uid",(long)uid)==2);
                if(!reload)Sql("UPDATE revora_taoist_wire_probe SET fingerprint=@value WHERE uid=@uid","value",TaoistFingerprint(p),"uid",(long)uid);
                Require(TaoistFingerprint(p)==(string)Sql("SELECT fingerprint FROM revora_taoist_wire_probe WHERE uid=@uid","uid",(long)uid));
                var items=p.Inventory.Objects.Concat(new[]{p.Equipment.TryGetItem(4)}).ToArray();
                TeamWireSend(child,"pirate-check:"+p.Entity.ConquerPoints+":"+string.Join(";",items.Select(i=>i.UID+","+i.ID+","+(i.Bound?1:0)))+":"+string.Join(",",learned));
                Require(EntityTable.SaveEntity(p));child.StandardInput.WriteLine("stop");child.StandardInput.Flush();TeamWireRead(child,"[team-wire] stopped");Require(child.WaitForExit(5000) && child.ExitCode==0);
            }finally{roll.SetValue(null,previous);if(!child.HasExited){child.Kill();child.WaitForExit(5000);}if(errors.Wait(1000) && !string.IsNullOrEmpty(errors.Result))System.Console.Error.WriteLine(errors.Result);}
        }
        System.Console.WriteLine("[taoist-process] taoist-wire-"+(fire?"fire":"water")+(reload?"-reload":"")+"-passed");Server.CommandsAI("@close");throw new Exception("Taoist wire did not close.");
    }

}
