using System;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    // Disposable-loopback-only relay: no payload logging, bounded frames and lifetime.
    // Background connections pass through; only the selected receipt's transaction
    // loses its successful COMMIT response. Preparation commits are not targeted.
    sealed class PirateCommitDrop : IDisposable {
        readonly TcpListener listener=new TcpListener(IPAddress.Loopback,0);
        readonly System.Collections.Concurrent.ConcurrentBag<TcpClient> clients=new System.Collections.Concurrent.ConcurrentBag<TcpClient>();
        readonly System.Collections.Concurrent.ConcurrentBag<Task> workers=new System.Collections.Concurrent.ConcurrentBag<Task>();
        readonly Task accept;
        readonly string receiptInsert;
        volatile bool stopping;
        internal volatile bool Dropped;
        internal int Port {get{return ((IPEndPoint)listener.LocalEndpoint).Port;}}
        static byte[] Read(Stream stream,int size) {
            if(size<0 || size>1024*1024)throw new InvalidDataException("Private relay frame exceeds bounds.");
            var data=new byte[size];for(int at=0;at<size;){int n=stream.Read(data,at,size-at);if(n==0)throw new EndOfStreamException();at+=n;}return data;
        }
        static int Length(byte[] data,int offset){return (data[offset]<<24)|(data[offset+1]<<16)|(data[offset+2]<<8)|data[offset+3];}
        internal PirateCommitDrop(string table) {
            if(!new[]{"revora_elimination_claims","revora_pirate_awakenings","revora_pirate_map_purchases","revora_pirate_flowers","revora_pirate_scroll_exchanges","revora_pirate_training","revora_pirate_searches"}.Contains(table))
                throw new InvalidOperationException("Unapproved private relay target.");
            receiptInsert="INSERT INTO "+table;
            listener.Start(16);
            accept=Task.Run(()=>{try{while(!stopping){var peer=listener.AcceptTcpClient();clients.Add(peer);workers.Add(Task.Run(()=>Relay(peer)));}}
                catch(SocketException){if(!stopping)throw;}catch(ObjectDisposedException){if(!stopping)throw;}});
        }
        void Relay(TcpClient peer) {
            Task outgoing=null;var backend=new TcpClient();clients.Add(backend);int armed=0;
            try {
                backend.Connect(IPAddress.Loopback,25439);var client=peer.GetStream();var server=backend.GetStream();
                client.ReadTimeout=server.ReadTimeout=5000;client.WriteTimeout=server.WriteTimeout=5000;
                outgoing=Task.Run(()=>{try{
                    var start=Read(client,4);var body=Read(client,Length(start,0)-4);server.Write(start,0,4);server.Write(body,0,body.Length);Array.Clear(body,0,body.Length);
                    while(!stopping){var head=Read(client,5);var payload=Read(client,Length(head,1)-4);
                        if((head[0]=='P' || head[0]=='Q') && System.Text.Encoding.UTF8.GetString(payload).Contains(receiptInsert))
                            System.Threading.Interlocked.Exchange(ref armed,1);
                        server.Write(head,0,head.Length);server.Write(payload,0,payload.Length);Array.Clear(payload,0,payload.Length);
                    }
                }catch(IOException){}catch(ObjectDisposedException){}});
                while(!stopping){var head=Read(server,5);var payload=Read(server,Length(head,1)-4);
                    if(System.Threading.Volatile.Read(ref armed)==1 && head[0]=='C' && payload.SequenceEqual(new byte[]{67,79,77,77,73,84,0})){
                        Dropped=true;peer.Client.LingerState=new LingerOption(true,0);break;
                    }
                    client.Write(head,0,head.Length);client.Write(payload,0,payload.Length);Array.Clear(payload,0,payload.Length);
                }
            }catch(IOException){}catch(ObjectDisposedException){}finally{
                peer.Close();backend.Close();if(outgoing!=null && !outgoing.Wait(5000))throw new TimeoutException("Private relay sender did not stop.");
            }
        }
        public void Dispose(){
            stopping=true;listener.Stop();foreach(var client in clients)client.Close();
            if(!accept.Wait(5000))throw new TimeoutException("Private relay accept did not stop.");
            foreach(var client in clients)client.Close();
            if(!Task.WaitAll(workers.ToArray(),5000))throw new TimeoutException("Private relay workers did not stop.");
        }
    }
    static void PirateLoseCommit(Action action,string table="revora_pirate_awakenings") {
        string original=DataHolder.ConnectionString;var config=new Npgsql.NpgsqlConnectionStringBuilder(original);
        if(config.Host!="127.0.0.1" || config.Port!=25439 || config.Database!="revora_candidate_arena_process" || config.Username!="revora_candidate_runtime")
            throw new InvalidOperationException("Pirate relay requires the disposable process database.");
        using(var relay=new PirateCommitDrop(table)) {
            config.Port=relay.Port;config.Pooling=false;config.SslMode=Npgsql.SslMode.Disable;config.Timeout=5;config.CommandTimeout=5;
            try{DataHolder.ConnectionString=config.ConnectionString;action();}finally{DataHolder.ConnectionString=original;}
            Require(relay.Dropped);
        }
    }
    static bool PirateFenced(GameState p){return (bool)typeof(Revora.Game.Entity).GetField("PersistenceRequiresReload",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(p.Entity);}
    static GameState PirateRecover(GameState old){
        nextUid=old.Entity.UID;var p=Player(Kernel.Maps[1002],true);SkillTable.LoadSpells(p);p.LoadItemStats();p.ActiveNpc=199590;
        Require(!PirateFenced(p));return p;
    }
    static void PirateJournalCheck(GameState p,MsgTaskStatus.QuestData.QuestStatus status) {
        var list=new System.Collections.Generic.List<MsgTaskStatus.QuestData>();Revora.Game.Features.QuestListSystem.Fill(p,list);
        Require(list.Single(i=>(uint)i.UID==6868).Status==status);
    }
    static void PirateJournalForge(GameState p) {
        var begin=new MsgTaskStatus(true,1){Action=MsgTaskStatus.QuestAction.Begin};begin.Apend(MsgTaskStatus.QuestData.Create((QuestID)6868,MsgTaskStatus.QuestData.QuestStatus.Finished,0));MsgTaskStatus.Process(begin.ToArray(),p);
        var quit=new byte[28];Revora.Network.Writer.WriteUInt16(20,0,quit);Revora.Network.Writer.WriteUInt16(2032,2,quit);
        Revora.Network.Writer.WriteUInt32(QuestInfo.ActionBase+6868,8,quit);quit[15]=102;Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,20,quit);
        Revora.Network.PacketHandler.HandlePacket(quit,p);
    }
    static object PirateOffer(GameState p) {
        var heart=p.Inventory.Objects.Where(i=>i.ID==3307449).OrderByDescending(i=>i.Bound).ThenBy(i=>i.UID).First();
        object[] args={p,heart.UID,null};
        var offer=Feature("PirateEpicStore").GetMethod("Prepare",Hidden).Invoke(null,args);
        if(offer==null)throw new Exception("Pirate offer rejected: "+args[2]);return offer;
    }
    static bool PirateRejected(GameState p,object offer,Func<bool> context) {
        try{lock(p.ItemSyncRoot)Call(Feature("PirateEpicStore"),"Commit",p,offer,context);return false;}
        catch(System.Reflection.TargetInvocationException ex){Require(ex.InnerException is InvalidOperationException);return true;}
    }
    static void PirateStore() {
        var store=Feature("PirateEpicStore");Require((bool)Feature("PirateEpicWorld").GetProperty("Enabled",Hidden).GetValue(null,null));
        Player(Kernel.Maps[1002]);Sql("UPDATE entities SET class=75,reborn=2,conquerpoints=1100 WHERE uid=@uid","uid",(long)FirstUid);nextUid=FirstUid;
        var p=Player(Kernel.Maps[1002],true);
        Require(p.Account.State==AccountTable.AccountState.Player && !p.Fake);
        p.Inventory.Add(3307449,0,1);p.Quests.Accept((QuestID)6868);p.Quests.IncreaseQuestKills((QuestID)6868,99);p.Quests.Save();
        PirateJournalCheck(p,MsgTaskStatus.QuestData.QuestStatus.Available);PirateJournalForge(p);PirateJournalCheck(p,MsgTaskStatus.QuestData.QuestStatus.Available);
        p.Inventory.Remove(p.Inventory.Objects.Single(i=>i.ID==3307449),Revora.Game.Enums.ItemUse.Remove);
        var world=Feature("PirateEpicWorld");
        Require(Kernel.Maps.ContainsKey(10271) && (bool)world.GetProperty("Enabled",Hidden).GetValue(null,null));
        p.BindWorldMap(1004);var lord=p.Map.Npcs[4272];System.Console.WriteLine("[pirate-trainer] 4272,"+lord.MapID+","+lord.X+","+lord.Y);p.Entity.X=lord.X;p.Entity.Y=(ushort)(lord.Y+2);p.ActiveNpc=4272;
        foreach(byte option in new byte[]{23,24,25})Npcs.GetDialog(new NpcRequest(true){NpcID=4272,OptionID=option},p);
        Require(!p.Spells.ContainsKey(14680) && !p.Spells.ContainsKey(14720) && !p.Spells.ContainsKey(14710));
        Npcs.GetDialog(new NpcRequest(true){NpcID=4272,OptionID=150},p);
        Require(p.Entity.MapID==10271 && p.Entity.X==114 && p.Entity.Y==188);
        Require(p.Map.Npcs.ContainsKey(199590) && p.Map.Npcs.ContainsKey(199591) && p.Map.Npcs.ContainsKey(199592));
        Call(world,"Return",p);Require(p.Entity.MapID==1004);p.BindWorldMap(1002);
        // Paid chart acquisition uses supplied CPs and the real transactional store.
        object[] buyArgs={p,null};object buy;
        lock(p.ItemSyncRoot)buy=store.GetMethod("PrepareMap",Hidden).Invoke(null,buyArgs);
        Require(buy!=null);
        Sql("CREATE FUNCTION revora_pirate_map_fail() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'owned Pirate map rollback'; END $$; CREATE TRIGGER revora_pirate_map_fail BEFORE INSERT ON revora_pirate_map_purchases FOR EACH ROW EXECUTE FUNCTION revora_pirate_map_fail()");
        bool mapFailed=false;
        try{lock(p.ItemSyncRoot)Call(store,"CommitMap",p,buy,(Func<bool>)(()=>true));}
        catch(System.Reflection.TargetInvocationException ex){Require(ex.InnerException is Npgsql.PostgresException);mapFailed=true;}
        Require(mapFailed && p.Entity.ConquerPoints==1100 && TaoistCount(p,3307443)==0 && Convert.ToUInt32(Sql("SELECT conquerpoints FROM entities WHERE uid=@uid","uid",(long)FirstUid))==1100);
        Sql("DROP TRIGGER revora_pirate_map_fail ON revora_pirate_map_purchases; DROP FUNCTION revora_pirate_map_fail()");
        p.BindWorldMap(10271);p.Entity.X=116;p.Entity.Y=189;p.ActiveNpc=199590;
        Npcs.GetDialog(new NpcRequest(true){NpcID=199590,OptionID=20},p);
        Require(p.Entity.ConquerPoints==1100 && TaoistCount(p,3307443)==0);
        Npcs.GetDialog(new NpcRequest(true){NpcID=199590,OptionID=1},p);
        p.Entity.X=1;p.Entity.Y=1;
        Npcs.GetDialog(new NpcRequest(true){NpcID=199590,OptionID=20},p);
        Require(p.Entity.ConquerPoints==1100 && TaoistCount(p,3307443)==0);
        p.Entity.X=116;p.Entity.Y=189;
        Npcs.GetDialog(new NpcRequest(true){NpcID=199590,OptionID=20},p);
        Require(p.Entity.ConquerPoints==1100);
        Npcs.GetDialog(new NpcRequest(true){NpcID=199590,OptionID=1},p);
        EntityTable.SaveEntity(p);
        PirateLoseCommit(()=>Npcs.GetDialog(new NpcRequest(true){NpcID=199590,OptionID=20},p),"revora_pirate_map_purchases");
        Require(PirateFenced(p) && p.Entity.ConquerPoints==1100 && TaoistCount(p,3307443)==0);
        Require(Convert.ToUInt32(Sql("SELECT conquerpoints FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))==1073
            && (long)Sql("SELECT count(*) FROM revora_pirate_map_purchases r JOIN items i ON i.uid=r.item_uid WHERE r.uid=@uid AND i.id=3307443","uid",(long)p.Entity.UID)==1);
        string paidFingerprint=TaoistFingerprint(p);
        System.Threading.Tasks.Parallel.For(0,8,_=>Npcs.GetDialog(new NpcRequest(true){NpcID=199590,OptionID=20},p));
        Require(TaoistFingerprint(p)==paidFingerprint && p.Entity.ConquerPoints==1100);
        p=PirateRecover(p);
        System.Threading.Tasks.Parallel.For(0,8,_=>Npcs.GetDialog(new NpcRequest(true){NpcID=199590,OptionID=20},p));
        Require(TaoistFingerprint(p)==paidFingerprint);
        Require(p.Entity.ConquerPoints==1073 && TaoistCount(p,3307443)==1 && (long)Sql("SELECT count(*) FROM revora_pirate_map_purchases")==1);
        System.Console.WriteLine("[pirate-process] paid-chart-commit-reply-loss-passed");
        Npcs.GetDialog(new NpcRequest(true){NpcID=199590,OptionID=254},p);Require(p.Entity.MapID==1004);p.BindWorldMap(1002);
        PirateJournalCheck(p,MsgTaskStatus.QuestData.QuestStatus.Accepted);
        // Supplied items/qualifications: this proves transaction behavior, not issuance.
        ulong silverBefore=p.Entity.Money,totalPrice=0;
        var weaponsmith=p.Map.Npcs[5];p.Entity.X=weaponsmith.X;p.Entity.Y=(ushort)(weaponsmith.Y+2);
        foreach(uint id in new uint[]{611004,612004}){
            uint price=ConquerItemInformation.BaseInformations[id].GoldWorth;Require(price>0);totalPrice+=price;
            var buyWeapon=new ItemUsage(true){UID=5,dwParam=id,dwExtraInfo=1,Action=ItemUsage.Mode.BuyFromNPC}.ToArray();
            Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,buyWeapon.Length-8,buyWeapon);ItemUsage.Process(buyWeapon,p);
            var weapon=p.Inventory.Objects.Single(i=>i.ID==id);
            ItemHandler.EquipItem(new ItemUsage(true){UID=weapon.UID,dwParam=id==611004?4u:5u,Action=ItemUsage.Mode.EquipItem},p);
            Require(p.Equipment.TryGetItem((byte)(id==611004?4:5)).UID==weapon.UID);
        }
        Require(p.Entity.Money==silverBefore-totalPrice && (long)Sql("SELECT count(*) FROM revora_weapon_book_purchases WHERE uid=@uid AND shop=5 AND item_type IN(611004,612004)","uid",(long)p.Entity.UID)==2);
        p.BindWorldMap(1004);HeartTalk(p,4272,151);Require(p.Entity.MapID==1700);
        Config.MoneyDropEnabled=false;Config.KillConquerPointsEnabled=false;
        var hunt=Feature("NinjaFlowerHunt");var roll=hunt.GetField("Roll",Hidden);var previousRoll=roll.GetValue(null);
        try{
            var sources=p.Map.Entities.Values.Where(e=>e.MonsterInfo!=null && e.MonsterInfo.ID>=3600 && e.MonsterInfo.ID<=3631 && !e.Dead).Take(12).ToArray();Require(sources.Length==12);
            for(int n=0;n<sources.Length;n++){
                int color=n%2;roll.SetValue(null,(Func<int,int>)(max=>color));var beforeDrops=p.Map.FloorItems.Keys.ToArray();EliminationHit(p,sources[n]);
                var drop=p.Map.FloorItems.Values.Single(i=>!beforeDrops.Contains(i.UID) && (i.ItemID==722732 || i.ItemID==722736));
                p.Entity.X=drop.X;p.Entity.Y=drop.Y;
                var frame=new FloorItem(true){UID=drop.UID,Type=FloorItem.Remove,X=drop.X,Y=drop.Y,ItemID=drop.ItemID}.ToArray();
                Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,frame.Length-8,frame);FloorItem.Process(frame,p);FloorItem.Process(frame,p);
                Require(drop.PickedUpAlready && p.Inventory.ContainsUID(drop.Item.UID));
            }
        }finally{roll.SetValue(null,previousRoll);}
        Require(TaoistCount(p,722732)==6 && TaoistCount(p,722736)==6 && (long)Sql("SELECT count(*) FROM revora_ninja_flower_pickups WHERE collector=@uid","uid",(long)p.Entity.UID)==12);
        HeartTalk(p,3604,90);Require(p.Entity.MapID==1004);HeartTalk(p,4272,150);Require(p.Entity.MapID==10271);
        HeartTalk(p,199590,254);Require(p.Entity.MapID==1004);p.BindWorldMap(1002);
        EntityTable.SaveEntity(p);
        Sql("UPDATE items SET plus=12,bless=7,enchant=255,socketone=73,sockettwo=73 WHERE entityid=@uid AND id IN(611004,612004)","uid",(long)FirstUid);
        Sql("UPDATE items SET bound=1 WHERE entityid=@uid AND id=722732","uid",(long)FirstUid);
        nextUid=FirstUid;p=Player(Kernel.Maps[1002],true);p.LoadItemStats();
        var right=p.Equipment.TryGetItem(4);var left=p.Equipment.TryGetItem(5);
        Require(right.ID==611004 && left.ID==612004 && TaoistCount(p,3307449)==0);
        p=PirateFlowers(p);right=p.Equipment.TryGetItem(4);left=p.Equipment.TryGetItem(5);
        p=PirateSearches(p);PirateWaterStacks(p);p=PirateTraining(p);right=p.Equipment.TryGetItem(4);left=p.Equipment.TryGetItem(5);
        PirateJournalCheck(p,MsgTaskStatus.QuestData.QuestStatus.Finished);
        // Hearts are nonstackable in the loaded catalog; corrupt stacks reject.
        var heart=p.Inventory.Objects.First(i=>i.ID==3307449);ushort stack=heart.StackSize;
        heart.StackSize=2;object[] invalid={p,heart.UID,null};
        lock(p.ItemSyncRoot)Require(store.GetMethod("Prepare",Hidden).Invoke(null,invalid)==null);
        heart.StackSize=stack;
        // Saved rental status must not be ignored when the equipment cache is stale.
        Sql("UPDATE items SET days='1' WHERE uid=@item","item",(long)right.UID);
        bool rejected=false;try{lock(p.ItemSyncRoot)PirateOffer(p);}
        catch(System.Reflection.TargetInvocationException ex){Require(ex.InnerException is InvalidOperationException);rejected=true;}
        Require(rejected);Sql("UPDATE items SET days='0' WHERE uid=@item","item",(long)right.UID);
        // Conversion consumes a singleton, making room for exactly one soul even at40.
        for(int n=p.Inventory.Count;n<40;n++)p.Inventory.Add(611004,0,1);
        Require(p.Inventory.Count==40);
        object[] fullMap={p,null};lock(p.ItemSyncRoot)Require(store.GetMethod("PrepareMap",Hidden).Invoke(null,fullMap)==null);
        string before=TaoistFingerprint(p);object offer;lock(p.ItemSyncRoot)offer=PirateOffer(p);
        var deadline=offer.GetType().GetField("Until",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);var until=(DateTime)deadline.GetValue(offer);
        deadline.SetValue(offer,DateTime.UtcNow.AddSeconds(-1));Require(PirateRejected(p,offer,()=>true));deadline.SetValue(offer,until);
        Require(PirateRejected(p,offer,()=>false));
        Sql("UPDATE items SET bless=6 WHERE uid=@item","item",(long)right.UID);
        Require(PirateRejected(p,offer,()=>true));Sql("UPDATE items SET bless=7 WHERE uid=@item","item",(long)right.UID);
        Require(TaoistFingerprint(p)==before);

        Sql("CREATE FUNCTION revora_pirate_probe_fail() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'owned Pirate rollback probe'; END $$; CREATE TRIGGER revora_pirate_probe_fail BEFORE INSERT ON revora_pirate_awakenings FOR EACH ROW EXECUTE FUNCTION revora_pirate_probe_fail()");
        bool failed=false;
        try{lock(p.ItemSyncRoot)Call(store,"Commit",p,offer,(Func<bool>)(()=>true));}
        catch(System.Reflection.TargetInvocationException ex){Require(ex.InnerException is Npgsql.PostgresException);failed=true;}
        Require(failed && TaoistFingerprint(p)==before && right.ID==611004 && left.ID==612004 && TaoistCount(p,3307449)==2);
        Sql("DROP TRIGGER revora_pirate_probe_fail ON revora_pirate_awakenings; DROP FUNCTION revora_pirate_probe_fail()");
        var usedHeart=p.Inventory.Objects.Where(i=>i.ID==3307449).OrderByDescending(i=>i.Bound).ThenBy(i=>i.UID).First();
        ItemHandler.UseItem(new ConquerItem(true){ID=3307449,UID=usedHeart.UID},p);Require(TaoistFingerprint(p)==before);
        var useHeart=MountUsePacket(usedHeart.UID);
        System.Threading.Tasks.Parallel.For(0,8,_=>ItemUsage.Process(useHeart,p));
        Require((long)Sql("SELECT count(*) FROM revora_pirate_awakenings")==1 && p.Inventory.Count==40);
        Require(right.ID==671004 && left.ID==612004 && TaoistCount(p,3307449)==1);
        failed=false;try{lock(p.ItemSyncRoot)Call(store,"Commit",p,offer,(Func<bool>)(()=>true));}
        catch(System.Reflection.TargetInvocationException ex){Require(ex.InnerException is InvalidOperationException);failed=true;}
        Require(failed && (long)Sql("SELECT count(*) FROM revora_pirate_awakenings")==1);
        p.BindWorldMap(10271);p.Entity.X=116;p.Entity.Y=189;p.ActiveNpc=199590;
        Npcs.GetDialog(new NpcRequest(true){NpcID=199590,OptionID=20},p);
        Require(left.ID==612004 && TaoistCount(p,3307449)==1);
        Npcs.GetDialog(new NpcRequest(true){NpcID=199590,OptionID=3},p);
        Require(left.ID==612004 && TaoistCount(p,3307449)==1);
        var finalHeart=p.Inventory.Objects.Single(i=>i.ID==3307449);uint leftUid=left.UID;
        EntityTable.SaveEntity(p);
        PirateLoseCommit(()=>Npcs.GetDialog(new NpcRequest(true){NpcID=199590,OptionID=20},p));
        Require(PirateFenced(p));
        Require(left.ID==612004 && p.Inventory.ContainsUID(finalHeart.UID));
        Require(Convert.ToUInt32(Sql("SELECT id FROM items WHERE uid=@uid","uid",(long)leftUid))==670004
            && (long)Sql("SELECT count(*) FROM items WHERE uid=@uid","uid",(long)finalHeart.UID)==0
            && (long)Sql("SELECT count(*) FROM revora_pirate_awakenings a JOIN items i ON i.uid=a.soul_uid WHERE a.weapon_uid=@uid AND i.bound=1","uid",(long)leftUid)==1);
        string committed=TaoistFingerprint(p);
        System.Threading.Tasks.Parallel.For(0,8,_=>Npcs.GetDialog(new NpcRequest(true){NpcID=199590,OptionID=20},p));
        Require(TaoistFingerprint(p)==committed && left.ID==612004 && p.Inventory.ContainsUID(finalHeart.UID));
        p=PirateRecover(p);
        right=p.Equipment.TryGetItem(4);left=p.Equipment.TryGetItem(5);
        Require(left.UID==leftUid && left.ID==670004 && !p.Inventory.ContainsUID(finalHeart.UID) && TaoistFingerprint(p)==committed);
        System.Threading.Tasks.Parallel.For(0,8,_=>ItemUsage.Process(MountUsePacket(finalHeart.UID),p));
        Require(TaoistFingerprint(p)==committed);
        p.BindWorldMap(10271);p.Entity.X=116;p.Entity.Y=189;p.ActiveNpc=199590;
        System.Console.WriteLine("[pirate-process] awakening-commit-reply-loss-passed");
        Require((long)Sql("SELECT count(*) FROM revora_pirate_awakenings")==2);
        Npcs.GetDialog(new NpcRequest(true){NpcID=199590,OptionID=254},p);Require(p.Entity.MapID==1004);p.BindWorldMap(1002);
        Require(right.ID==671004 && left.ID==670004 && TaoistCount(p,3307449)==0);
        Require(right.Bound && !left.Bound);
        Require((long)Sql(@"WITH RECURSIVE earned(item_uid,source,depth) AS (
(SELECT item_uid,'paid'::text,0 FROM revora_pirate_map_purchases WHERE uid=@uid
UNION ALL SELECT bottle_uid,'flowers'::text,0 FROM revora_pirate_flowers WHERE uid=@uid)
UNION ALL SELECT r.item_uid,e.source,e.depth+1 FROM earned e JOIN revora_pirate_searches s ON s.input_uid=e.item_uid AND s.uid=@uid
CROSS JOIN LATERAL unnest(s.reward_uids) AS r(item_uid) WHERE e.depth<16)
SELECT count(DISTINCT e.source) FROM earned e JOIN revora_pirate_awakenings a ON a.heart_uid=e.item_uid AND a.uid=@uid","uid",(long)p.Entity.UID)==2);
        Require((long)Sql("SELECT count(*) FROM revora_weapon_book_purchases WHERE uid=@uid AND shop=5 AND ((item_type=611004 AND @right=ANY(item_uids)) OR (item_type=612004 AND @left=ANY(item_uids)))","uid",(long)p.Entity.UID,"right",(long)right.UID,"left",(long)left.UID)==2);
        foreach(var weapon in new[]{right,left})Require(weapon.Plus==12 && weapon.Bless==7 && weapon.Enchant==255 && (int)weapon.SocketOne==73 && (int)weapon.SocketTwo==73);
        Require(p.Inventory.Objects.Count(i=>i.Bound && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)==2);
        p.BindWorldMap(10271);p.Entity.X=114;p.Entity.Y=188;
        var remainingChart=p.Inventory.Objects.Single(i=>i.ID==3307443);ItemUsage.Process(MountUsePacket(remainingChart.UID),p);
        Require(p.Entity.MapID==10266 && p.Inventory.ContainsUID(remainingChart.UID));
        EntityTable.SaveEntity(p);
        Sql("CREATE TABLE revora_pirate_probe(right_uid bigint,left_uid bigint,fingerprint text); INSERT INTO revora_pirate_probe VALUES(@right,@left,@fingerprint)",
            "right",(long)right.UID,"left",(long)left.UID,"fingerprint",TaoistFingerprint(p));
        System.Console.WriteLine("[pirate-process] pirate-store-passed");Environment.Exit(0);
    }
    static byte[] PirateStackPacket(uint uid,uint value,ItemUsage.Mode action) {
        var packet=new ItemUsage(true){UID=uid,dwParam=value,Action=action}.ToArray();
        Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,packet.Length-8,packet);return packet;
    }
    static void PirateWaterStacks(GameState p) {
        // These three bound units were earned by the preceding searches, not supplied.
        var water=p.Inventory.Objects.Where(i=>i.ID==3307450).OrderBy(i=>i.UID).ToArray();
        Require(water.Length==3 && water.All(i=>i.Bound && i.StackSize==1));
        var first=water[0];var second=water[1];var last=water[2];
        var merge=PirateStackPacket(first.UID,second.UID,ItemUsage.Mode.MergeStackableItems);
        string before=TaoistFingerprint(p);var world=Feature("PirateEpicWorld");
        foreach(int offset in new[]{0,2,merge.Length-1}) {
            var malformed=(byte[])merge.Clone();malformed[offset]^=1;
            Require((bool)Call(world,"TryStack",p,malformed));
            Require(TaoistFingerprint(p)==before && first.StackSize==1 && p.Inventory.ContainsUID(second.UID));
        }
        first.Lock=1;ItemUsage.Process(merge,p);first.Lock=0;
        Require(TaoistFingerprint(p)==before && first.StackSize==1 && p.Inventory.ContainsUID(second.UID));
        // A cache-only binding change cannot launder bound material through a merge.
        second.Bound=false;ItemUsage.Process(merge,p);second.Bound=true;
        Require(TaoistFingerprint(p)==before && first.StackSize==1 && p.Inventory.ContainsUID(second.UID));
        System.Threading.Tasks.Parallel.For(0,8,_=>ItemUsage.Process(merge,p));
        Require(first.StackSize==2 && first.Bound && !p.Inventory.ContainsUID(second.UID) && TaoistCount(p,3307450)==3);
        var split=PirateStackPacket(first.UID,1,ItemUsage.Mode.SplitStack);
        before=TaoistFingerprint(p);
        foreach(uint quantity in new uint[]{0,2,uint.MaxValue})ItemUsage.Process(PirateStackPacket(first.UID,quantity,ItemUsage.Mode.SplitStack),p);
        Require(TaoistFingerprint(p)==before && first.StackSize==2);
        System.Threading.Tasks.Parallel.For(0,8,_=>ItemUsage.Process(split,p));
        var separated=p.Inventory.Objects.Single(i=>i.ID==3307450 && i.UID!=first.UID && i.UID!=last.UID);
        Require(first.StackSize==1 && separated.StackSize==1 && separated.Bound && TaoistCount(p,3307450)==3);
        ItemUsage.Process(PirateStackPacket(first.UID,separated.UID,ItemUsage.Mode.MergeStackableItems),p);
        ItemUsage.Process(PirateStackPacket(first.UID,last.UID,ItemUsage.Mode.MergeStackableItems),p);
        Require(first.StackSize==3 && first.Bound && p.Inventory.Objects.Count(i=>i.ID==3307450)==1);
        Require(Convert.ToInt32(Sql("SELECT stacksize FROM items WHERE uid=@uid AND bound=1","uid",(long)first.UID))==3);
        System.Console.WriteLine("[pirate-process] earned-water-stacks-passed");
    }
    static GameState PirateTraining(GameState p) {
        var store=Feature("PirateEpicStore");Require(TaoistCount(p,3307450)==3);
        for(byte choice=1;choice<=4;choice++) {
            var random=store.GetField("SearchDraw",Hidden);var previous=random.GetValue(null);
            try{
                random.SetValue(null,(Func<int,int>)(maximum=>maximum-1));
                while(TaoistCount(p,3307450)<10){
                    var waterBeforeSearch=TaoistCount(p,3307450);uint cpBeforeSearch=p.Entity.ConquerPoints;
                    p.BindWorldMap(10271);HeartTalk(p,199590,1);HeartTalk(p,199590,20);
                    Require(p.Entity.ConquerPoints==cpBeforeSearch-27);
                    // A bound locked decoy must not hide the usable unbound chart.
                    ConquerItem decoy=null;
                    if(choice==1 && waterBeforeSearch==3){
                        var owned=p.Inventory.Objects.Select(i=>i.UID).ToArray();p.Inventory.Add(3307443,0,1);
                        decoy=p.Inventory.Objects.Single(i=>!owned.Contains(i.UID));decoy.Bound=true;decoy.Lock=1;
                        Sql("UPDATE items SET bound=1,locked=1 WHERE uid=@item","item",(long)decoy.UID);
                    }
                    HeartTalk(p,199592,1);HeartTalk(p,199592,20);
                    if(decoy!=null){Require(p.Inventory.ContainsUID(decoy.UID) && decoy.Lock==1 && decoy.Bound);p.Inventory.Remove(decoy,Revora.Game.Enums.ItemUse.Remove);}
                    Require(TaoistCount(p,3307450)==waterBeforeSearch+1);
                }
            }finally{random.SetValue(null,previous);}
            byte pick=choice==4?(byte)1:choice;object[] args={p,pick,null};object offer;
            lock(p.ItemSyncRoot)offer=store.GetMethod("PrepareExchange",Hidden).Invoke(null,args);Require(offer!=null);
            if(choice==1) {
                string before=TaoistFingerprint(p);
                Sql("CREATE FUNCTION revora_pirate_exchange_skip() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER revora_pirate_exchange_skip BEFORE INSERT ON revora_pirate_scroll_exchanges FOR EACH ROW EXECUTE FUNCTION revora_pirate_exchange_skip()");
                bool failed=false;try{lock(p.ItemSyncRoot)Call(store,"CommitExchange",p,offer,(Func<bool>)(()=>true));}
                catch(System.Reflection.TargetInvocationException ex){Require(ex.InnerException is InvalidOperationException);failed=true;}
                Require(failed && TaoistFingerprint(p)==before && TaoistCount(p,3307450)==10 && (long)Sql("SELECT count(*) FROM revora_pirate_scroll_exchanges")==0);
                Sql("DROP TRIGGER revora_pirate_exchange_skip ON revora_pirate_scroll_exchanges; DROP FUNCTION revora_pirate_exchange_skip()");
            }
            p.BindWorldMap(10271);p.Entity.X=126;p.Entity.Y=185;p.ActiveNpc=199591;
            var waterBefore=TaoistCount(p,3307450);
            Npcs.GetDialog(new NpcRequest(true){NpcID=199591,OptionID=pick},p);
            Require(TaoistCount(p,3307450)==waterBefore);
            if(choice==1){
                EntityTable.SaveEntity(p);
                PirateLoseCommit(()=>Npcs.GetDialog(new NpcRequest(true){NpcID=199591,OptionID=20},p),"revora_pirate_scroll_exchanges");
                Require(PirateFenced(p) && TaoistCount(p,3307450)==10 && TaoistCount(p,3307432)==0);
                Require((long)Sql("SELECT count(*) FROM items WHERE entityid=@uid AND id=3307450","uid",(long)p.Entity.UID)==0
                    && (long)Sql("SELECT count(*) FROM revora_pirate_scroll_exchanges r JOIN items i ON i.uid=r.scroll_uid WHERE r.uid=@uid AND r.water_count=10 AND i.id=3307432 AND i.bound=1","uid",(long)p.Entity.UID)==1);
                string committed=TaoistFingerprint(p);
                System.Threading.Tasks.Parallel.For(0,8,_=>Npcs.GetDialog(new NpcRequest(true){NpcID=199591,OptionID=20},p));
                Require(TaoistFingerprint(p)==committed && TaoistCount(p,3307450)==10);
                p=PirateRecover(p);p.ActiveNpc=199591;
                Require(TaoistFingerprint(p)==committed && TaoistCount(p,3307450)==0 && TaoistCount(p,3307432)==1);
                System.Console.WriteLine("[pirate-process] scroll-exchange-commit-reply-loss-passed");
            }
            System.Threading.Tasks.Parallel.For(0,8,_=>Npcs.GetDialog(new NpcRequest(true){NpcID=199591,OptionID=20},p));
            Require(TaoistCount(p,3307450)==waterBefore-10 && (long)Sql("SELECT count(*) FROM revora_pirate_scroll_exchanges")==choice);
            Npcs.GetDialog(new NpcRequest(true){NpcID=199591,OptionID=254},p);Require(p.Entity.MapID==1004);p.BindWorldMap(1002);
            var scroll=p.Inventory.Objects.Single(i=>i.ID==3307431u+pick);Require(scroll.Bound);
            if(choice==4){ItemUsage.Process(MountUsePacket(scroll.UID),p);Require(p.Inventory.Objects.Any(i=>i.UID==scroll.UID));continue;}
            if(choice==1) {
                p.Trade.InTrade=true;lock(p.ItemSyncRoot)Require(!(bool)Call(store,"LearnScroll",p,scroll.UID,(Func<bool>)(()=>true)));p.Trade.InTrade=false;
                string before=TaoistFingerprint(p);
                Sql("CREATE FUNCTION revora_pirate_training_fail() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER revora_pirate_training_fail BEFORE INSERT ON revora_pirate_training FOR EACH ROW EXECUTE FUNCTION revora_pirate_training_fail()");
                bool failed=false;try{lock(p.ItemSyncRoot)Call(store,"LearnScroll",p,scroll.UID,(Func<bool>)(()=>true));}
                catch(System.Reflection.TargetInvocationException ex){Require(ex.InnerException is InvalidOperationException);failed=true;}
                Require(failed && TaoistFingerprint(p)==before && !p.Spells.ContainsKey(14680));
                Sql("DROP TRIGGER revora_pirate_training_fail ON revora_pirate_training; DROP FUNCTION revora_pirate_training_fail()");
            }
            var packet=MountUsePacket(scroll.UID);
            if(choice==1){
                string beforeGuard=TaoistFingerprint(p);byte profession=p.Entity.Class,level=p.Entity.Level;var itemLock=scroll.Lock;
                Action unchanged=()=>Require(TaoistFingerprint(p)==beforeGuard && !p.Spells.ContainsKey(14680) && (long)Sql("SELECT count(*) FROM revora_pirate_training")==0 && (long)Sql("SELECT count(*) FROM skills WHERE entityid=@uid AND id=14680","uid",(long)p.Entity.UID)==0);
                try{
                    foreach(byte wrong in new byte[]{69,76}){p.Entity.Class=wrong;ItemUsage.Process(packet,p);unchanged();}
                    p.Entity.Class=profession;p.Entity.Level=39;ItemUsage.Process(packet,p);unchanged();p.Entity.Level=level;
                    scroll.Lock=1;ItemUsage.Process(packet,p);scroll.Lock=itemLock;unchanged();
                    p.Trade.InTrade=true;ItemUsage.Process(packet,p);unchanged();p.Trade.InTrade=false;
                    var shortFrame=packet.Take(24).ToArray();Require((bool)Call(Feature("PirateEpicWorld"),"TryHandle",p,shortFrame));unchanged();
                    var large=new byte[129];Array.Copy(packet,large,packet.Length);Revora.Network.Writer.WriteUInt16(121,0,large);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,121,large);
                    Require((bool)Call(Feature("PirateEpicWorld"),"TryHandle",p,large));unchanged();
                }finally{p.Entity.Class=profession;p.Entity.Level=level;scroll.Lock=itemLock;p.Trade.InTrade=false;}
            }
            foreach(int broken in new[]{0,2,packet.Length-1}){
                var bad=(byte[])packet.Clone();bad[broken]^=1;
                Require((bool)Call(Feature("PirateEpicWorld"),"TryHandle",p,bad));
                Require(p.Inventory.Objects.Any(i=>i.UID==scroll.UID) && (long)Sql("SELECT count(*) FROM revora_pirate_training")==choice-1);
            }
            if(choice==1){
                EntityTable.SaveEntity(p);
                PirateLoseCommit(()=>ItemUsage.Process(packet,p),"revora_pirate_training");
                Require(PirateFenced(p) && p.Inventory.ContainsUID(scroll.UID) && !p.Spells.ContainsKey(14680));
                Require((long)Sql("SELECT count(*) FROM items WHERE uid=@uid","uid",(long)scroll.UID)==0
                    && (long)Sql("SELECT count(*) FROM revora_pirate_training r JOIN skills s ON s.entityid=r.uid AND s.id=r.spell WHERE r.uid=@uid AND r.scroll_uid=@scroll AND r.spell=14680","uid",(long)p.Entity.UID,"scroll",(long)scroll.UID)==1);
                string committed=TaoistFingerprint(p);
                System.Threading.Tasks.Parallel.For(0,8,_=>ItemUsage.Process(packet,p));
                Require(TaoistFingerprint(p)==committed && p.Inventory.ContainsUID(scroll.UID) && !p.Spells.ContainsKey(14680));
                p=PirateRecover(p);
                Require(TaoistFingerprint(p)==committed && !p.Inventory.ContainsUID(scroll.UID) && p.Spells.ContainsKey(14680));
                System.Console.WriteLine("[pirate-process] scroll-learning-commit-reply-loss-passed");
            }
            System.Threading.Tasks.Parallel.For(0,8,_=>ItemUsage.Process(packet,p));
            Require(!p.Inventory.Objects.Any(i=>i.UID==scroll.UID) && (long)Sql("SELECT count(*) FROM revora_pirate_training")==choice);
        }
        Require(TaoistCount(p,3307450)==0 && TaoistCount(p,3307432)==1 && p.Spells.ContainsKey(14680) && p.Spells.ContainsKey(14720) && p.Spells.ContainsKey(14710));
        Require((long)Sql("SELECT count(*) FROM revora_pirate_training")==3 && (long)Sql("SELECT count(*) FROM revora_pirate_scroll_exchanges")==4);
        Require(p.Entity.ConquerPoints==20 && (long)Sql("SELECT count(*) FROM revora_pirate_map_purchases")==40);
        Require((long)Sql("SELECT count(*) FROM revora_pirate_searches s CROSS JOIN LATERAL unnest(s.reward_types) AS r(item_type) WHERE s.uid=@uid AND r.item_type=3307450","uid",(long)p.Entity.UID)==40);
        return p;
    }
    static GameState PirateSearches(GameState p) {
        var store=Feature("PirateEpicStore");var draw=store.GetField("SearchDraw",Hidden);var original=draw.GetValue(null);
        try {
            uint[] inputs={3307443,3307444,3307446,3307447,3307448,3307445,3307447};
            int[][] outcomes={new[]{0,1},new[]{0,2},new[]{0,1},new int[0],new[]{2},new[]{0,2},new int[0]};
            for(int step=0;step<inputs.Length;step++) {
                var rolls=new System.Collections.Generic.Queue<int>(outcomes[step]);draw.SetValue(null,(Func<int,int>)(n=>rolls.Dequeue()));
                var input=p.Inventory.Objects.First(i=>i.ID==inputs[step]);object[] args={p,input.UID,null};object offer;
                var existing=p.Inventory.Objects.Select(i=>i.UID).ToArray();uint[] fillers=new uint[0];
                if(step==3) {
                    for(int n=p.Inventory.Count;n<40;n++)p.Inventory.Add(611004,0,1);
                    fillers=p.Inventory.Objects.Where(i=>!existing.Contains(i.UID)).Select(i=>i.UID).ToArray();
                    string before=TaoistFingerprint(p);
                    lock(p.ItemSyncRoot)Require(store.GetMethod("PrepareSearch",Hidden).Invoke(null,args)==null);
                    Require(p.Inventory.Count==40 && TaoistFingerprint(p)==before && input.ID==3307447);
                    p.Inventory.Remove(p.Inventory.Objects.Single(i=>i.UID==fillers[0]),Revora.Game.Enums.ItemUse.Remove);
                    Require(p.Inventory.Count==39);
                }
                lock(p.ItemSyncRoot)offer=store.GetMethod("PrepareSearch",Hidden).Invoke(null,args);Require(offer!=null);
                if(step==0) {
                    string before=TaoistFingerprint(p);
                    Sql("CREATE FUNCTION revora_pirate_search_fail() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'owned Pirate search rollback'; END $$; CREATE TRIGGER revora_pirate_search_fail BEFORE INSERT ON revora_pirate_searches FOR EACH ROW EXECUTE FUNCTION revora_pirate_search_fail()");
                    bool failed=false;try{lock(p.ItemSyncRoot)Call(store,"CommitSearch",p,offer,(Func<bool>)(()=>true));}
                    catch(System.Reflection.TargetInvocationException ex){Require(ex.InnerException is Npgsql.PostgresException);failed=true;}
                    Require(failed && TaoistFingerprint(p)==before);
                    Sql("DROP TRIGGER revora_pirate_search_fail ON revora_pirate_searches; DROP FUNCTION revora_pirate_search_fail()");
                    rolls=new System.Collections.Generic.Queue<int>(outcomes[step]);
                }
                p.BindWorldMap(10271);p.Entity.X=105;p.Entity.Y=176;p.ActiveNpc=199592;
                byte searchOption=input.ID==3307448?(byte)6:(byte)(input.ID-3307443+1);
                if(input.ID!=3307448){
                    ushort[] xs={29,56,25,46,26},ys={56,78,42,56,45};int grade=(int)(input.ID-3307443);
                    ushort destination=(ushort)(10266+grade);uint guide=199600u+(uint)grade;
                    p.Entity.X=114;p.Entity.Y=188;var entry=MountUsePacket(input.UID);
                    foreach(int badOffset in new[]{0,2,entry.Length-1}){
                        var bad=(byte[])entry.Clone();bad[badOffset]^=1;Require((bool)Call(Feature("PirateEpicWorld"),"TryHandle",p,bad));
                        Require(p.Entity.MapID==10271 && p.Inventory.ContainsUID(input.UID));
                    }
                    ItemUsage.Process(entry,p);
                    Require(p.Entity.MapID==destination && p.Entity.X==xs[grade] && p.Entity.Y==ys[grade] && p.Inventory.ContainsUID(input.UID));
                    HeartTalk(p,guide,1);Require(p.Inventory.ContainsUID(input.UID));
                    HeartTalk(p,guide,254);Require(p.Entity.MapID==10271 && p.Inventory.ContainsUID(input.UID));
                    ItemUsage.Process(entry,p);Require(p.Entity.MapID==destination);
                    HeartTalk(p,guide,20);Require(p.Entity.MapID==destination && p.Inventory.ContainsUID(input.UID));
                    HeartTalk(p,guide,1);
                    if(step==0 || step==3){
                        EntityTable.SaveEntity(p);
                        PirateLoseCommit(()=>Npcs.GetDialog(new NpcRequest(true){NpcID=guide,OptionID=20},p),"revora_pirate_searches");
                        Require(PirateFenced(p) && p.Inventory.ContainsUID(input.UID) && p.Entity.MapID==destination && rolls.Count==0);
                        Require((long)Sql("SELECT count(*) FROM items WHERE uid=@uid","uid",(long)input.UID)==0
                            && (long)Sql("SELECT count(*) FROM revora_pirate_searches WHERE input_uid=@uid","uid",(long)input.UID)==1);
                        var rewardUids=(long[])Sql("SELECT reward_uids FROM revora_pirate_searches WHERE input_uid=@uid","uid",(long)input.UID);
                        var rewardTypes=(int[])Sql("SELECT reward_types FROM revora_pirate_searches WHERE input_uid=@uid","uid",(long)input.UID);
                        Require(rewardTypes.SequenceEqual(step==0?new[]{3307444}:new[]{3307449,3307450}));
                        string committed=TaoistFingerprint(p);
                        System.Threading.Tasks.Parallel.For(0,8,_=>Npcs.GetDialog(new NpcRequest(true){NpcID=guide,OptionID=20},p));
                        Require(TaoistFingerprint(p)==committed && p.Inventory.ContainsUID(input.UID));
                        p=PirateRecover(p);p.ActiveNpc=guide;
                        Require(p.Entity.MapID==destination && !p.Inventory.ContainsUID(input.UID) && TaoistFingerprint(p)==committed);
                        for(int reward=0;reward<rewardUids.Length;reward++){
                            var item=p.Inventory.Objects.Single(i=>i.UID==(uint)rewardUids[reward]);
                            Require(item.ID==(uint)rewardTypes[reward] && item.Bound==(item.ID==3307450));
                        }
                        System.Threading.Tasks.Parallel.For(0,8,_=>{Npcs.GetDialog(new NpcRequest(true){NpcID=guide,OptionID=1},p);Npcs.GetDialog(new NpcRequest(true){NpcID=guide,OptionID=20},p);});
                        Require(!PirateFenced(p) && TaoistFingerprint(p)==committed && rolls.Count==0);
                        HeartTalk(p,guide,254);
                        System.Console.WriteLine("[pirate-process] search-commit-reply-loss-passed step="+step);
                    }
                    System.Threading.Tasks.Parallel.For(0,8,_=>Npcs.GetDialog(new NpcRequest(true){NpcID=guide,OptionID=20},p));
                    Require(p.Entity.MapID==10271);
                }else if(input.ID==3307448){
                    var beforeBottle=TaoistFingerprint(p);ItemHandler.UseItem(new ConquerItem(true){ID=3307448,UID=input.UID},p);Require(TaoistFingerprint(p)==beforeBottle);
                    var packet=MountUsePacket(input.UID);
                    System.Threading.Tasks.Parallel.For(0,8,_=>ItemUsage.Process(packet,p));
                }else{
                Npcs.GetDialog(new NpcRequest(true){NpcID=199592,OptionID=searchOption},p);
                Require(p.Inventory.Objects.Any(i=>i.UID==input.UID));
                System.Threading.Tasks.Parallel.For(0,8,_=>Npcs.GetDialog(new NpcRequest(true){NpcID=199592,OptionID=20},p));
                }
                Require(rolls.Count==0 && !p.Inventory.Objects.Any(i=>i.UID==input.UID));
                p.ActiveNpc=199592;p.Entity.X=105;p.Entity.Y=176;
                Npcs.GetDialog(new NpcRequest(true){NpcID=199592,OptionID=254},p);Require(p.Entity.MapID==1004);p.BindWorldMap(1002);
                if(step==3) {
                    Require(p.Inventory.Count==40 && TaoistCount(p,3307449)==1 && TaoistCount(p,3307450)==1);
                    foreach(var item in p.Inventory.Objects.Where(i=>fillers.Contains(i.UID)).ToArray())p.Inventory.Remove(item,Revora.Game.Enums.ItemUse.Remove);
                }
                bool replay=false;try{lock(p.ItemSyncRoot)Call(store,"CommitSearch",p,offer,(Func<bool>)(()=>true));}
                catch(System.Reflection.TargetInvocationException ex){Require(ex.InnerException is InvalidOperationException);replay=true;}Require(replay);
            }
            Require(TaoistCount(p,3307449)==2 && TaoistCount(p,3307450)==2 && TaoistCount(p,3307448)==0 && (long)Sql("SELECT count(*) FROM revora_pirate_searches")==7);
            // Purchase both stack units and merge through the guarded item handler.
            p.BindWorldMap(10271);HeartTalk(p,199590,1);HeartTalk(p,199590,20);HeartTalk(p,199590,1);HeartTalk(p,199590,20);
            var charts=p.Inventory.Objects.Where(i=>i.ID==3307443).OrderBy(i=>i.UID).ToArray();Require(charts.Length==2);
            var stacked=charts[0];var minor=charts[1];string mergeBefore=TaoistFingerprint(p);
            Sql("CREATE FUNCTION revora_pirate_merge_fail() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'owned merge rollback'; END $$; CREATE TRIGGER revora_pirate_merge_fail BEFORE DELETE ON items FOR EACH ROW EXECUTE FUNCTION revora_pirate_merge_fail()");
            bool mergeFailed=false;try{lock(p.ItemSyncRoot)Call(store,"Merge",p,stacked.UID,minor.UID);}catch(System.Reflection.TargetInvocationException ex){Require(ex.InnerException is Npgsql.PostgresException);mergeFailed=true;}
            Require(mergeFailed && TaoistFingerprint(p)==mergeBefore && stacked.StackSize==1 && p.Inventory.ContainsUID(minor.UID));
            Sql("DROP TRIGGER revora_pirate_merge_fail ON items; DROP FUNCTION revora_pirate_merge_fail()");
            var merge=new ItemUsage(true){UID=stacked.UID,dwParam=minor.UID,Action=ItemUsage.Mode.MergeStackableItems}.ToArray();
            Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,merge.Length-8,merge);
            System.Threading.Tasks.Parallel.For(0,8,_=>ItemUsage.Process(merge,p));
            Require(stacked.StackSize==2 && !p.Inventory.ContainsUID(minor.UID));
            string splitBefore=TaoistFingerprint(p);
            Sql("CREATE FUNCTION revora_pirate_split_fail() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'owned split rollback'; END $$; CREATE TRIGGER revora_pirate_split_fail BEFORE INSERT ON items FOR EACH ROW EXECUTE FUNCTION revora_pirate_split_fail()");
            bool splitFailed=false;try{lock(p.ItemSyncRoot)Call(store,"Split",p,stacked.UID,1u);}catch(System.Reflection.TargetInvocationException ex){Require(ex.InnerException is Npgsql.PostgresException);splitFailed=true;}
            Require(splitFailed && TaoistFingerprint(p)==splitBefore && stacked.StackSize==2);
            Sql("DROP TRIGGER revora_pirate_split_fail ON items; DROP FUNCTION revora_pirate_split_fail()");
            var splitPacket=new ItemUsage(true){UID=stacked.UID,dwParam=1,Action=ItemUsage.Mode.SplitStack}.ToArray();
            Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,splitPacket.Length-8,splitPacket);
            System.Threading.Tasks.Parallel.For(0,8,_=>ItemUsage.Process(splitPacket,p));
            var separated=p.Inventory.Objects.Single(i=>i.ID==3307443 && i.UID!=stacked.UID);
            Require(stacked.StackSize==1 && separated.StackSize==1 && separated.Bound==stacked.Bound && separated.Durability==stacked.Durability);
            var rejoin=new ItemUsage(true){UID=stacked.UID,dwParam=separated.UID,Action=ItemUsage.Mode.MergeStackableItems}.ToArray();
            Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,rejoin.Length-8,rejoin);ItemUsage.Process(rejoin,p);
            Require(stacked.StackSize==2 && !p.Inventory.ContainsUID(separated.UID));
            var keep=p.Inventory.Objects.Select(i=>i.UID).ToArray();for(int n=p.Inventory.Count;n<40;n++)p.Inventory.Add(611004,0,1);
            var padding=p.Inventory.Objects.Where(i=>!keep.Contains(i.UID)).ToArray();object[] partial={p,stacked.UID,null};
            string fullBag=TaoistFingerprint(p);ItemUsage.Process(splitPacket,p);Require(TaoistFingerprint(p)==fullBag && stacked.StackSize==2 && p.Inventory.Count==40);
            lock(p.ItemSyncRoot)Require(store.GetMethod("PrepareSearch",Hidden).Invoke(null,partial)==null);
            Require(stacked.StackSize==2 && TaoistCount(p,3307450)==2);
            p.Inventory.Remove(padding[0],Revora.Game.Enums.ItemUse.Remove);
            draw.SetValue(null,(Func<int,int>)(n=>99));object part;
            lock(p.ItemSyncRoot){part=store.GetMethod("PrepareSearch",Hidden).Invoke(null,partial);Require(part!=null);Call(store,"CommitSearch",p,part,(Func<bool>)(()=>true));}
            Require(stacked.StackSize==1 && p.Inventory.Count==40 && TaoistCount(p,3307450)==3 && (long)Sql("SELECT count(*) FROM revora_pirate_searches")==8);
            foreach(var item in p.Inventory.Objects.Where(i=>padding.Any(a=>a.UID==i.UID)).ToArray())p.Inventory.Remove(item,Revora.Game.Enums.ItemUse.Remove);
            return p;
        } finally {draw.SetValue(null,original);}
    }
    static GameState PirateFlowers(GameState p) {
        var store=Feature("PirateEpicStore");var clock=store.GetField("Clock",Hidden);var original=clock.GetValue(null);
        DateTime now=new DateTime(2026,9,27,23,59,30,DateTimeKind.Utc);
        clock.SetValue(null,(Func<DateTime>)(()=>now));
        try {
            object[] args={p,null};object offer;lock(p.ItemSyncRoot)offer=store.GetMethod("PrepareFlowers",Hidden).Invoke(null,args);
            Require(offer!=null);string before=TaoistFingerprint(p);
            now=now.AddSeconds(31);bool rejected=false;
            try{lock(p.ItemSyncRoot)Call(store,"CommitFlowers",p,offer,(Func<bool>)(()=>true));}
            catch(System.Reflection.TargetInvocationException ex){Require(ex.InnerException is InvalidOperationException);rejected=true;}
            Require(rejected && TaoistFingerprint(p)==before);now=now.AddSeconds(-31);
            Sql("CREATE FUNCTION revora_pirate_flower_fail() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.id=3307448 THEN RAISE EXCEPTION 'owned Pirate bottle rollback'; END IF; RETURN NEW; END $$; CREATE TRIGGER revora_pirate_flower_fail BEFORE INSERT ON items FOR EACH ROW EXECUTE FUNCTION revora_pirate_flower_fail()");
            bool failed=false;
            try{lock(p.ItemSyncRoot)Call(store,"CommitFlowers",p,offer,(Func<bool>)(()=>true));}
            catch(System.Reflection.TargetInvocationException ex){Require(ex.InnerException is Npgsql.PostgresException);failed=true;}
            Require(failed && TaoistFingerprint(p)==before && (long)Sql("SELECT count(*) FROM revora_pirate_flowers")==0);
            Sql("DROP TRIGGER revora_pirate_flower_fail ON items; DROP FUNCTION revora_pirate_flower_fail()");
            p.BindWorldMap(10271);p.Entity.X=116;p.Entity.Y=189;p.ActiveNpc=199590;
            Npcs.GetDialog(new NpcRequest(true){NpcID=199590,OptionID=2},p);
            Require(TaoistFingerprint(p)==before);
            EntityTable.SaveEntity(p);
            PirateLoseCommit(()=>Npcs.GetDialog(new NpcRequest(true){NpcID=199590,OptionID=20},p),"revora_pirate_flowers");
            Require(PirateFenced(p) && TaoistCount(p,722732)==6 && TaoistCount(p,722736)==6 && TaoistCount(p,3307448)==0);
            Require((long)Sql("SELECT count(*) FROM revora_pirate_flowers r JOIN items i ON i.uid=r.bottle_uid WHERE r.uid=@uid AND i.id=3307448 AND i.bound=1","uid",(long)p.Entity.UID)==1
                && (long)Sql("SELECT count(*) FROM items WHERE entityid=@uid AND id IN(722732,722736)","uid",(long)p.Entity.UID)==6);
            string committed=TaoistFingerprint(p);
            System.Threading.Tasks.Parallel.For(0,8,_=>Npcs.GetDialog(new NpcRequest(true){NpcID=199590,OptionID=20},p));
            Require(TaoistFingerprint(p)==committed && TaoistCount(p,3307448)==0);
            p=PirateRecover(p);args[0]=p;
            System.Threading.Tasks.Parallel.For(0,8,_=>{Npcs.GetDialog(new NpcRequest(true){NpcID=199590,OptionID=2},p);Npcs.GetDialog(new NpcRequest(true){NpcID=199590,OptionID=20},p);});
            Require(TaoistFingerprint(p)==committed);
            Require(TaoistCount(p,722732)==3 && TaoistCount(p,722736)==3 && TaoistCount(p,3307448)==1 && p.Inventory.Objects.Single(i=>i.ID==3307448).Bound);
            System.Console.WriteLine("[pirate-process] daily-flowers-commit-reply-loss-passed");
            Npcs.GetDialog(new NpcRequest(true){NpcID=199590,OptionID=254},p);Require(p.Entity.MapID==1004);p.BindWorldMap(1002);
            lock(p.ItemSyncRoot)Require(store.GetMethod("PrepareFlowers",Hidden).Invoke(null,args)==null);
            now=now.AddDays(1);lock(p.ItemSyncRoot)Require(store.GetMethod("PrepareFlowers",Hidden).Invoke(null,args)!=null);
            Require(p.Entity.ConquerPoints==1073 && (long)Sql("SELECT count(*) FROM revora_pirate_flowers")==1);
            return p;
        } finally {clock.SetValue(null,original);}
    }
    static void PirateReload() {
        var world=Feature("PirateEpicWorld");Require((bool)world.GetProperty("Enabled",Hidden).GetValue(null,null) && Kernel.Maps.ContainsKey(10271));
        var p=Player(Kernel.Maps[1002],true);SkillTable.LoadSpells(p);p.LoadItemStats();
        Require(p.Entity.MapID==10266 && p.Entity.X==29 && p.Entity.Y==56 && TaoistCount(p,3307443)==1);
        HeartTalk(p,199600,254);Require(p.Entity.MapID==10271 && TaoistCount(p,3307443)==1);
        var right=p.Equipment.TryGetItem(4);var left=p.Equipment.TryGetItem(5);
        Require(right.UID==Convert.ToUInt32(Sql("SELECT right_uid FROM revora_pirate_probe")) && left.UID==Convert.ToUInt32(Sql("SELECT left_uid FROM revora_pirate_probe")));
        Require(right.ID==671004 && left.ID==670004 && TaoistCount(p,3307449)==0);
        Require(right.Bound && !left.Bound);
        Require((long)Sql(@"WITH RECURSIVE earned(item_uid,source,depth) AS (
(SELECT item_uid,'paid'::text,0 FROM revora_pirate_map_purchases WHERE uid=@uid
UNION ALL SELECT bottle_uid,'flowers'::text,0 FROM revora_pirate_flowers WHERE uid=@uid)
UNION ALL SELECT r.item_uid,e.source,e.depth+1 FROM earned e JOIN revora_pirate_searches s ON s.input_uid=e.item_uid AND s.uid=@uid
CROSS JOIN LATERAL unnest(s.reward_uids) AS r(item_uid) WHERE e.depth<16)
SELECT count(DISTINCT e.source) FROM earned e JOIN revora_pirate_awakenings a ON a.heart_uid=e.item_uid AND a.uid=@uid","uid",(long)p.Entity.UID)==2);
        Require((long)Sql("SELECT count(*) FROM revora_weapon_book_purchases WHERE uid=@uid AND shop=5 AND ((item_type=611004 AND @right=ANY(item_uids)) OR (item_type=612004 AND @left=ANY(item_uids)))","uid",(long)p.Entity.UID,"right",(long)right.UID,"left",(long)left.UID)==2);
        foreach(var weapon in new[]{right,left})Require(weapon.Plus==12 && weapon.Bless==7 && weapon.Enchant==255 && (int)weapon.SocketOne==73 && (int)weapon.SocketTwo==73);
        Require(TaoistFingerprint(p)==(string)Sql("SELECT fingerprint FROM revora_pirate_probe") && (long)Sql("SELECT count(*) FROM revora_pirate_awakenings")==2);
        Require(p.Inventory.Objects.Count(i=>i.Bound && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)==2);
        Require(p.Entity.ConquerPoints==20 && TaoistCount(p,3307443)==1 && (long)Sql("SELECT count(*) FROM revora_pirate_map_purchases")==40);
        Require(TaoistCount(p,3307448)==0 && TaoistCount(p,3307450)==0 && p.Inventory.Objects.Where(i=>i.ID==3307450).All(i=>i.Bound) && TaoistCount(p,722732)==3 && TaoistCount(p,722736)==3 && (long)Sql("SELECT count(*) FROM revora_pirate_searches")==45);
        Require(TaoistCount(p,3307432)==1 && p.Spells.ContainsKey(14680) && p.Spells.ContainsKey(14720) && p.Spells.ContainsKey(14710) && (long)Sql("SELECT count(*) FROM revora_pirate_training")==3);
        var store=Feature("PirateEpicStore");var clock=store.GetField("Clock",Hidden);var previous=clock.GetValue(null);
        try{clock.SetValue(null,(Func<DateTime>)(()=>new DateTime(2026,9,27,23,59,40,DateTimeKind.Utc)));object[] args={p,null};lock(p.ItemSyncRoot)Require(store.GetMethod("PrepareFlowers",Hidden).Invoke(null,args)==null);}
        finally{clock.SetValue(null,previous);}
        PirateJournalCheck(p,MsgTaskStatus.QuestData.QuestStatus.Finished);PirateJournalForge(p);PirateJournalCheck(p,MsgTaskStatus.QuestData.QuestStatus.Finished);
        Require(p.Quests.GetQuest((QuestID)6868).Kills==99);
        System.Console.WriteLine("[pirate-process] pirate-reload-passed");Server.CommandsAI("@close");throw new Exception("Pirate reload did not close.");
    }
    static void PirateCombatTraining(bool reload=false) {
        var p=Player(Kernel.Maps[1002],true);SkillTable.LoadSpells(p);SkillTable.LoadProficiencies(p);p.LoadItemStats();
        Require(p.Equipment.TryGetItem(4).ID/1000==671 && p.Equipment.TryGetItem(5).ID/1000==670);
        if(reload) {
            foreach(ushort family in new ushort[]{671,670}) {
                Require(p.Proficiencies.ContainsKey(family));var prof=p.Proficiencies[family];
                Require(prof.Level>=12 && (long)Sql("SELECT count(*) FROM revora_pirate_mastery_probe WHERE uid=@uid AND family=@family AND level=@level AND xp=@xp","uid",(long)p.Entity.UID,"family",(int)family,"level",(int)prof.Level,"xp",(long)prof.Experience)==1);
            }
            foreach(int id in new[]{11322,11323})Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=@id","uid",(long)p.Entity.UID,"id",id)==1);
            p.Entity.MyAchievement.Send();p.Entity.MyAchievement.Send();
            foreach(int id in new[]{11322,11323})Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=@id","uid",(long)p.Entity.UID,"id",id)==1);
            System.Console.WriteLine("[pirate-process] pirate-train-reload-passed");Server.CommandsAI("@close");throw new Exception("Pirate training reload did not close.");
        }
        ushort savedMap=p.Entity.MapID,savedX=p.Entity.X,savedY=p.Entity.Y;
        Config.MoneyDropEnabled=false;Config.KillConquerPointsEnabled=false;
        Feature("MonsterEquipmentDrops").GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>max-1));
        var targets=Kernel.Maps.Values.SelectMany(m=>m.Entities.Values).Where(e=>!e.Dead && e.MonsterInfo!=null && !e.MonsterInfo.Boss && !e.MonsterInfo.Guard && !e.MonsterInfo.Reviver && !e.Companion).OrderByDescending(e=>e.Hitpoints).Take(128).ToArray();
        Require(targets.Length>0);int swings=0,kills=0;var until=DateTime.UtcNow.AddSeconds(90);
        foreach(var target in targets) {
            p.BindWorldMap(target.MapID);p.Entity.X=target.X;p.Entity.Y=target.Y;p.Screen.Add(target);int stalled=0;
            while(!target.Dead && !p.Entity.Dead && DateTime.UtcNow<until && swings<50000) {
                if(new ushort[]{671,670}.All(id=>p.Proficiencies.ContainsKey(id) && p.Proficiencies[id].Level>=12))break;
                // Only the attack cooldown clock is advanced. Damage, XP rates,
                // weapon metadata, monster HP and proficiency levels are untouched.
                uint beforeHp=target.Hitpoints;
                p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);
                Attack.HandleAttack(new Attack(true){Attacker=p.Entity.UID,Attacked=target.UID,X=target.X,Y=target.Y,AttackType=Attack.Melee},p);swings++;
                if(target.Hitpoints==beforeHp)stalled++;else stalled=0;
                if(stalled>=32)break;
            }
            if(target.Dead)kills++;
            if(DateTime.UtcNow>=until || swings>=50000 || new ushort[]{671,670}.All(id=>p.Proficiencies.ContainsKey(id) && p.Proficiencies[id].Level>=12))break;
        }
        SkillTable.SaveProficiencies(p);
        foreach(ushort family in new ushort[]{671,670}) {
            Require(p.Proficiencies.ContainsKey(family));var prof=p.Proficiencies[family];
            System.Console.WriteLine("[pirate-training] family="+family+" level="+prof.Level+" xp="+prof.Experience+" swings="+swings+" kills="+kills);
        }
        p.BindWorldMap(savedMap);p.Entity.X=savedX;p.Entity.Y=savedY;Require(EntityTable.SaveEntity(p));
        Require(new ushort[]{671,670}.All(id=>p.Proficiencies[id].Level>=12));
        p.Entity.MyAchievement.Send();
        foreach(int id in new[]{11322,11323})Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=@id","uid",(long)p.Entity.UID,"id",id)==1);
        Sql("CREATE TABLE revora_pirate_mastery_probe(uid bigint NOT NULL,family integer PRIMARY KEY,level integer NOT NULL,xp bigint NOT NULL)");
        foreach(ushort family in new ushort[]{671,670})Sql("INSERT INTO revora_pirate_mastery_probe VALUES(@uid,@family,@level,@xp)","uid",(long)p.Entity.UID,"family",(int)family,"level",(int)p.Proficiencies[family].Level,"xp",(long)p.Proficiencies[family].Experience);
        System.Console.WriteLine("[pirate-process] pirate-train-passed");Server.CommandsAI("@close");throw new Exception("Pirate training did not close.");
    }
    static void PirateWire(bool reload=false,bool searched=false,bool full=false,bool flowers=false) {
        if(!reload) {
            Player(Kernel.Maps[1002]);Sql("UPDATE entities SET class=75,reborn=2,conquerpoints=100 WHERE uid=@uid","uid",(long)FirstUid);nextUid=FirstUid;
        }
        var p=Player(Kernel.Maps[1002],true);Require(p.Account.State==AccountTable.AccountState.Player && !p.Fake);
        if(full) {
            SkillTable.LoadSpells(p);
            if(!reload) {
                p.Entity.ConquerPoints=1000;var smith=Kernel.Maps[1002].Npcs[5];p.Entity.X=smith.X;p.Entity.Y=(ushort)(smith.Y+2);
                foreach(uint type in new uint[]{611004,612004}) {
                    var packet=new ItemUsage(true){UID=5,dwParam=type,dwExtraInfo=1,Action=ItemUsage.Mode.BuyFromNPC}.ToArray();
                    Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,packet.Length-8,packet);ItemUsage.Process(packet,p);
                    var item=p.Inventory.Objects.Single(i=>i.ID==type);ItemHandler.EquipItem(new ItemUsage(true){UID=item.UID,dwParam=type==611004?4u:5u,Action=ItemUsage.Mode.EquipItem},p);
                }
                Require((long)Sql("SELECT count(*) FROM revora_weapon_book_purchases WHERE uid=@uid AND shop=5","uid",(long)FirstUid)==2);Require(EntityTable.SaveEntity(p));
                if(flowers) {
                    Sql("UPDATE items SET plus=12,bless=7,socketone=73,sockettwo=73 WHERE entityid=@uid AND id IN(611004,612004)","uid",(long)FirstUid);
                    Require(p.Inventory.Add(1002050,0,30));
                }
            } else Require(TaoistFingerprint(p)==(string)Sql("SELECT fingerprint FROM revora_pirate_wire_probe"));
        }

        if(reload && !searched && !full)Require(p.Entity.ConquerPoints==73 && TaoistCount(p,3307443)==1 && (long)Sql("SELECT count(*) FROM revora_pirate_map_purchases WHERE uid=@uid","uid",(long)FirstUid)==1);
        string password=Guid.NewGuid().ToString("N");var account=new AccountTable("TeamProbe0");
        if(!account.exists){account.Password=password;account.State=AccountTable.AccountState.Player;account.EntityID=FirstUid;account.Insert();}
        account.Password=password;account.Save();Require(account.HasSrpVerifier && account.EntityID==FirstUid);
        var lord=Kernel.Maps[1004].Npcs[4272];
        Sql("UPDATE entities SET owner='TeamProbe0',mapid=1004,x=@x,y=@y WHERE uid=@uid","x",(int)lord.X,"y",(int)lord.Y+2,"uid",(long)FirstUid);
        if(searched)Sql("UPDATE entities SET mapid=10266,x=29,y=56 WHERE uid=@uid","uid",(long)FirstUid);
        Kernel.GamePool.Remove(FirstUid);
        var start=new System.Diagnostics.ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"CandidateTeamWireProbe.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Environment.CurrentDirectory};
        start.EnvironmentVariables["REVORA_PIRATE_WIRE"]="1";start.EnvironmentVariables["REVORA_TEAM_WIRE_ROLE"]="0";start.EnvironmentVariables["REVORA_CANDIDATE_ACCOUNT_PASSWORD"]=password;start.EnvironmentVariables.Remove("REVORA_DATABASE_PASSWORD");
        using(var child=System.Diagnostics.Process.Start(start)) {
            start.EnvironmentVariables.Remove("REVORA_CANDIDATE_ACCOUNT_PASSWORD");password=null;var errors=child.StandardError.ReadToEndAsync();
            try {
                TeamWireRead(child,"[team-wire] ready");
                if(full) {p=Kernel.GamePool[FirstUid];if(flowers)PirateFlowerWire(child,p,reload);else PirateFullWire(child,p,reload);}
                else {TeamWireSend(child,searched?"pirate-search-replay":reload?"pirate-resume":"pirate-buy");
                p=Kernel.GamePool[FirstUid];Require(p.Account.State==AccountTable.AccountState.Player && p.Entity.MapID==10266 && p.Entity.ConquerPoints==73);
                Require((long)Sql("SELECT count(*) FROM revora_pirate_map_purchases WHERE uid=@uid AND balance_before=100 AND balance_after=73","uid",(long)FirstUid)==1);
                if(!searched)Require(TaoistCount(p,3307443)==1);
                if(reload && !searched){TeamWireSend(child,"pirate-search");Require(p.Entity.MapID==10271);}
                if(reload) {
                    Require((long)Sql("SELECT count(*) FROM revora_pirate_searches WHERE uid=@uid AND input_uid=(SELECT item_uid FROM revora_pirate_map_purchases WHERE uid=@uid)","uid",(long)FirstUid)==1);
                    uint reward=Convert.ToUInt32(Sql("SELECT reward_uids[1] FROM revora_pirate_searches WHERE uid=@uid","uid",(long)FirstUid));
                    uint type=Convert.ToUInt32(Sql("SELECT reward_types[1] FROM revora_pirate_searches WHERE uid=@uid","uid",(long)FirstUid));
                    Require(p.Inventory.Objects.Count()==1 && p.Inventory.Objects.Single().UID==reward && p.Inventory.Objects.Single().ID==type);
                    Require(type!=3307450 || p.Inventory.Objects.Single().Bound);
                }
                }
                Require(EntityTable.SaveEntity(p));
                child.StandardInput.WriteLine("stop");child.StandardInput.Flush();TeamWireRead(child,"[team-wire] stopped");Require(child.WaitForExit(5000) && child.ExitCode==0);
            } finally {
                if(!child.HasExited){child.Kill();child.WaitForExit(5000);}
                if(errors.Wait(1000) && !string.IsNullOrEmpty(errors.Result))System.Console.Error.WriteLine(errors.Result);
            }
        }
        System.Console.WriteLine("[pirate-process] "+(flowers?(reload?"pirate-wire-flowers-reload":"pirate-wire-flowers"):full?(reload?"pirate-wire-full-reload":"pirate-wire-full"):searched?"pirate-wire-search-reload":reload?"pirate-wire-reload":"pirate-wire")+"-passed");Server.CommandsAI("@close");throw new Exception("Pirate wire did not close.");
    }

    static void PirateSocketTrade(System.Diagnostics.Process child,GameState p,uint actor,byte choice) {
        var npc=p.Map.Npcs[actor];p.Entity.X=npc.X;p.Entity.Y=(ushort)(npc.Y+2);
        TeamWireSend(child,"pirate-dialog:"+actor+":0");TeamWireSend(child,"pirate-dialog:"+actor+":"+choice);TeamWireSend(child,"pirate-dialog:"+actor+":20");
    }
    static void PirateSocketSnapshot(System.Diagnostics.Process child,GameState p) {
        var items=p.Inventory.Objects.Concat(new[]{p.Equipment.TryGetItem(4),p.Equipment.TryGetItem(5)}).ToArray();
        TeamWireSend(child,"pirate-check:"+p.Entity.ConquerPoints+":"+string.Join(";",items.Select(i=>i.UID+","+i.ID+","+(i.Bound?1:0)))+":"+string.Join(",",new ushort[]{14680,14720,14710}.Where(id=>p.Spells.ContainsKey(id))));
    }
    static void PirateFullWire(System.Diagnostics.Process child,GameState p,bool reload) {
        TeamWireSend(child,"pirate-enter");var store=Feature("PirateEpicStore");var draw=store.GetField("SearchDraw",Hidden);var previous=draw.GetValue(null);
        try {
            if(!reload) {
                uint right=p.Equipment.TryGetItem(4).UID,left=p.Equipment.TryGetItem(5).UID;
                for(int weapon=0;weapon<2;weapon++) {
                    draw.SetValue(null,(Func<int,int>)(max=>max==100?0:max-1));
                    PirateSocketTrade(child,p,199590,1);
                    foreach(byte stars in new byte[]{1,4,5})PirateSocketTrade(child,p,199592,stars);
                    var heart=p.Inventory.Objects.Single(i=>i.ID==3307449);
                    TeamWireSend(child,"pirate-use:"+heart.UID);TeamWireSend(child,"pirate-use:"+heart.UID);
                    Require(!p.Inventory.ContainsUID(heart.UID));PirateSocketSnapshot(child,p);
                }
                Require(p.Equipment.TryGetItem(4).UID==right && p.Equipment.TryGetItem(5).UID==left && p.Equipment.TryGetItem(4).ID==671004 && p.Equipment.TryGetItem(5).ID==670004);
                draw.SetValue(null,(Func<int,int>)(max=>max-1));
                for(byte choice=1;choice<=3;choice++) {
                    while(TaoistCount(p,3307450)<10){PirateSocketTrade(child,p,199590,1);PirateSocketTrade(child,p,199592,1);}
                    PirateSocketTrade(child,p,199591,choice);var scroll=p.Inventory.Objects.Single(i=>i.ID==3307431u+choice);Require(scroll.Bound);
                    TeamWireSend(child,"pirate-use:"+scroll.UID);TeamWireSend(child,"pirate-use:"+scroll.UID);Require(!p.Inventory.ContainsUID(scroll.UID));PirateSocketSnapshot(child,p);
                }
                Sql("CREATE TABLE revora_pirate_wire_probe(fingerprint text NOT NULL)");Sql("INSERT INTO revora_pirate_wire_probe VALUES(@value)","value",TaoistFingerprint(p));
            }
            Require(p.Entity.ConquerPoints==190 && TaoistCount(p,3307450)==0 && TaoistCount(p,3307449)==0);
            Require(p.Inventory.Objects.Count()==2 && p.Inventory.Objects.All(i=>i.Bound && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7));
            Require(new ushort[]{14680,14720,14710}.All(id=>p.Spells.ContainsKey(id)));
            Require((bool)Sql(@"SELECT (SELECT count(*) FROM revora_pirate_map_purchases WHERE uid=@uid)=30
AND (SELECT count(*) FROM revora_pirate_searches WHERE uid=@uid)=34
AND (SELECT count(*) FROM revora_pirate_awakenings WHERE uid=@uid)=2
AND (SELECT count(*) FROM revora_pirate_scroll_exchanges WHERE uid=@uid)=3
AND (SELECT count(*) FROM revora_pirate_training WHERE uid=@uid)=3","uid",(long)FirstUid));
            Require(TaoistFingerprint(p)==(string)Sql("SELECT fingerprint FROM revora_pirate_wire_probe"));PirateSocketSnapshot(child,p);
        } finally {draw.SetValue(null,previous);}
    }

    static void PirateFlowerWire(System.Diagnostics.Process child,GameState p,bool reload) {
        var store=Feature("PirateEpicStore");var clock=store.GetField("Clock",Hidden);var oldClock=clock.GetValue(null);
        var hunt=Feature("NinjaFlowerHunt");var roll=hunt.GetField("Roll",Hidden);var oldRoll=roll.GetValue(null);
        clock.SetValue(null,(Func<DateTime>)(()=>new DateTime(2026,9,27,12,0,0,DateTimeKind.Utc)));
        try {
            if(!reload) {
                Config.MoneyDropEnabled=false;Config.KillConquerPointsEnabled=false;
                Feature("MonsterEquipmentDrops").GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>max-1));
                TeamWireSend(child,"pirate-passage:4272:151:1700");
                var monsters=p.Map.Entities.Values.Where(e=>e.MonsterInfo!=null && (e.MonsterInfo.ID==3609 || e.MonsterInfo.ID==3613 || e.MonsterInfo.ID==3629)).OrderBy(e=>e.MonsterInfo.MaxAttack).ToArray();Require(monsters.Length>=9);
                for(int n=0;n<12;n++) {
                    var available=DateTime.UtcNow.AddSeconds(15);while(!monsters.Any(e=>!e.Dead) && DateTime.UtcNow<available)System.Threading.Thread.Sleep(500);
                    var monster=monsters.FirstOrDefault(e=>!e.Dead);Require(monster!=null);int color=n%2;roll.SetValue(null,(Func<int,int>)(max=>color));var before=p.Map.FloorItems.Keys.ToArray();
                    p.Entity.X=monster.X;p.Entity.Y=monster.Y;p.Screen.Add(monster);
                    System.Console.WriteLine("[pirate-flower-wire] family="+monster.MonsterInfo.ID+" hp="+monster.Hitpoints);
                    for(int swing=0;swing<50 && !monster.Dead;swing++){
                        System.Threading.Thread.Sleep(550);
                        if(p.Entity.Hitpoints<p.Entity.MaxHitpoints*3/4) {
                            var medicine=p.Inventory.Objects.FirstOrDefault(i=>i.ID==1002050);Require(medicine!=null);
                            System.Console.WriteLine("[pirate-flower-wire] healing-live-hp="+p.Entity.Hitpoints+" saved-hp="+Convert.ToInt64(Sql("SELECT hitpoints FROM entities WHERE uid=@uid","uid",(long)FirstUid)));
                            EventHandler<System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs> observe=(sender,args)=>{if(args.Exception is InvalidOperationException && args.Exception.Message=="Durable HP/MP changed; reload required.")System.Console.WriteLine("[pirate-flower-wire] confirmed-durable-health-drift");};
                            AppDomain.CurrentDomain.FirstChanceException+=observe;
                            try {TeamWireSend(child,"pirate-use:"+medicine.UID);}finally{AppDomain.CurrentDomain.FirstChanceException-=observe;}
                        }
                        TeamWireSend(child,"pirate-hit:"+monster.UID);
                        if(swing%10==0 || monster.Dead)System.Console.WriteLine("[pirate-flower-wire] swing="+swing+" target-hp="+monster.Hitpoints+" player-hp="+p.Entity.Hitpoints);
                    }
                    Require(monster.Dead && !p.Entity.Dead);
                    var drop=p.Map.FloorItems.Values.Single(i=>!before.Contains(i.UID) && (i.ItemID==722732 || i.ItemID==722736));
                    p.Entity.X=drop.X;p.Entity.Y=drop.Y;
                    TeamWireSend(child,"pirate-pick:"+drop.UID+":"+drop.Item.UID+":"+drop.ItemID+":"+drop.X+":"+drop.Y);
                    Require(drop.PickedUpAlready && p.Inventory.ContainsUID(drop.Item.UID));
                }
                Require(TaoistCount(p,722732)==6 && TaoistCount(p,722736)==6);
                var exit=p.Map.Npcs[3604];p.Entity.X=exit.X;p.Entity.Y=(ushort)(exit.Y+2);TeamWireSend(child,"pirate-passage:3604:90:1004");
                TeamWireSend(child,"pirate-enter");PirateSocketTrade(child,p,199590,2);
                Require(TaoistCount(p,722732)==3 && TaoistCount(p,722736)==3 && TaoistCount(p,3307448)==1);
                var bottle=p.Inventory.Objects.Single(i=>i.ID==3307448);TeamWireSend(child,"pirate-use:"+bottle.UID);TeamWireSend(child,"pirate-use:"+bottle.UID);
                Require(!p.Inventory.ContainsUID(bottle.UID));
                Sql("CREATE TABLE revora_pirate_wire_probe(fingerprint text NOT NULL)");Sql("INSERT INTO revora_pirate_wire_probe VALUES(@value)","value",TaoistFingerprint(p));
            } else TeamWireSend(child,"pirate-enter");
            string beforeOffer=TaoistFingerprint(p);PirateSocketTrade(child,p,199590,2);
            Require(TaoistFingerprint(p)==beforeOffer && beforeOffer==(string)Sql("SELECT fingerprint FROM revora_pirate_wire_probe"));
            Require(p.Entity.ConquerPoints==1000 && TaoistCount(p,722732)==3 && TaoistCount(p,722736)==3 && TaoistCount(p,3307448)==0);
            Require(p.Inventory.Objects.Count(i=>i.ID>=3307443 && i.ID<=3307445)==1);
            Require((bool)Sql(@"SELECT (SELECT count(*) FROM revora_ninja_flower_pickups WHERE collector=@uid)=12
AND (SELECT count(*) FROM revora_pirate_flowers WHERE uid=@uid)=1
AND (SELECT count(*) FROM revora_pirate_searches WHERE uid=@uid AND input_uid=(SELECT bottle_uid FROM revora_pirate_flowers WHERE uid=@uid))=1","uid",(long)FirstUid));
            PirateSocketSnapshot(child,p);
        } finally {roll.SetValue(null,oldRoll);clock.SetValue(null,oldClock);}
    }

    static void HealthPersistenceProbe() {
        Player(Kernel.Maps[1002]);nextUid=FirstUid;var p=Player(Kernel.Maps[1002],true);p.Entity.MaxHitpoints=10000;p.Entity.Hitpoints=10000;p.Entity.Mana=800;Require(EntityTable.SaveEntity(p));
        Require(p.Inventory.Add(1002050,0,3));var medicine=p.Inventory.Objects.Where(i=>i.ID==1002050).OrderBy(i=>i.UID).ToArray();Require(medicine.Length==3);
        p.Entity.Hitpoints=2000;p.Entity.Mana=123;
        uint expected=Math.Min(10000u,2000u+ConquerItemInformation.BaseInformations[1002050].ItemHP);
        Require(Convert.ToUInt32(Sql("SELECT hitpoints FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))==10000);
        Parallel.For(0,8,_=>ItemHandler.UseItem(medicine[0],p));
        Require(!PirateFenced(p) && p.Entity.Hitpoints==expected && p.Entity.Mana==123 && !p.Inventory.ContainsUID(medicine[0].UID));
        Require((long)Sql("SELECT count(*) FROM revora_healing_medicine_uses WHERE uid=@uid","uid",(long)p.Entity.UID)==1);
        Require((bool)Sql("SELECT hitpoints=@hp AND mana=123 FROM entities WHERE uid=@uid","hp",(long)expected,"uid",(long)p.Entity.UID));
        p.Entity.Hitpoints=1000;Require(EntityTable.SaveEntity(p));
        Sql("CREATE FUNCTION revora_health_probe_fail() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'owned health rollback'; END $$; CREATE TRIGGER revora_health_probe_fail BEFORE INSERT ON revora_healing_medicine_uses FOR EACH ROW EXECUTE FUNCTION revora_health_probe_fail()");
        ItemHandler.UseItem(medicine[1],p);Require(PirateFenced(p));
        Require((long)Sql("SELECT count(*) FROM revora_healing_medicine_uses WHERE uid=@uid","uid",(long)p.Entity.UID)==1 && (long)Sql("SELECT count(*) FROM items WHERE uid=@item","item",(long)medicine[1].UID)==1);
        Require(Convert.ToUInt32(Sql("SELECT hitpoints FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))==1000);
        Sql("DROP TRIGGER revora_health_probe_fail ON revora_healing_medicine_uses; DROP FUNCTION revora_health_probe_fail()");
        nextUid=FirstUid;p=Player(Kernel.Maps[1002],true);p.Entity.MaxHitpoints=10000;
        ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.UID==medicine[1].UID),p);Require(!PirateFenced(p));
        Require((long)Sql("SELECT count(*) FROM revora_healing_medicine_uses WHERE uid=@uid","uid",(long)p.Entity.UID)==2);
        Sql("UPDATE entities SET hitpoints=hitpoints+1 WHERE uid=@uid","uid",(long)p.Entity.UID);
        string before=TaoistFingerprint(p);ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.UID==medicine[2].UID),p);
        Require(PirateFenced(p) && TaoistFingerprint(p)==before && (long)Sql("SELECT count(*) FROM revora_healing_medicine_uses WHERE uid=@uid","uid",(long)p.Entity.UID)==2);
        nextUid=FirstUid;p=Player(Kernel.Maps[1002],true);uint savedHp=p.Entity.Hitpoints;
        Sql("UPDATE entities SET hitpoints=hitpoints+1 WHERE uid=@uid","uid",(long)p.Entity.UID);
        p.Entity.Hitpoints=42;Require(!EntityTable.SaveEntity(p) && PirateFenced(p));
        Require(Convert.ToUInt32(Sql("SELECT hitpoints FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))==savedHp+1);
        System.Console.WriteLine("[health-process] pending-combat-concurrency-rollback-stale-save-passed");Environment.Exit(0);
    }

}
