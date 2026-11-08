using System;
using System.IO;
using System.Collections.Generic;
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
    static GameState MailPlayer(Map map) {
        var p=LoadoutPlayer(map);ReloadSilver(p);p.Prizes=new SafeDictionary<uint,Mailbox.PrizeInfo>();MailboxTable.Load(p);return p;
    }
    static byte[] MailRequest(uint id,ushort action) {
        var packet=new byte[18];Writer.WriteUInt16(10,0,packet);Writer.WriteUInt16(1045,2,packet);Writer.WriteUInt16(action,4,packet);Writer.WriteUInt32(id,6,packet);return packet;
    }
    static void MailRequest(GameState p,uint id,ushort action){Mailbox.Process(MailRequest(id,action),p);}
    static void StoredMail(GameState p,uint id,uint item=0,uint gold=0,uint cp=0,uint type=0) {
        byte[] bytes;using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream)) {
            w.Write((byte)1);w.Write(id);w.Write(600u);w.Write("Fixture courier");w.Write("Parcel");w.Write("Your parcel is ready.");w.Write(gold);w.Write(cp);w.Write(type);w.Write(item);bytes=stream.ToArray();
        }
        Query("UPDATE prizes SET prizes=@data WHERE uid=@uid","data",bytes,"uid",(long)p.Entity.UID);MailboxTable.Load(p);
    }
    static ConquerItem MailEscrow(GameState p,uint id) {
        var item=Pack(p,id,1);p.Inventory.Remove(item.UID,Enums.ItemUse.None,false);Query("UPDATE items SET entityid=0 WHERE uid=@uid","uid",(long)item.UID);return item;
    }
    static void VerifyMail(Map map) {
        Query("ALTER TABLE entities ADD COLUMN level integer DEFAULT 140,ADD COLUMN my_title integer DEFAULT 0,ADD COLUMN money bigint DEFAULT 0,ADD COLUMN conquerpoints bigint DEFAULT 0");
        Query("CREATE TABLE titles (LIKE public.titles INCLUDING ALL);CREATE TABLE itemadding (LIKE public.itemadding INCLUDING ALL);CREATE TABLE prizes (LIKE public.prizes INCLUDING ALL)");
        Query("CREATE TABLE refinery (LIKE public.refinery INCLUDING ALL);INSERT INTO refinery SELECT * FROM public.refinery;CREATE TABLE refineryboxes (LIKE public.refineryboxes INCLUDING ALL);INSERT INTO refineryboxes SELECT * FROM public.refineryboxes");
        Refinery.LoadItems();StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);ConquerItem.ItemUID=new Counter(8200000);
        var p=MailPlayer(map);const uint id=0x11223344;var item=MailEscrow(p,410009);long created=DateTime.Now.Ticks;
        Query("UPDATE items SET plus=12,perfectionlevel=54,perfectionprogress=777,perfectionowner='Fixture',perfectionownerguid=@owner,refineryitem=3004137,refinerytime=@expires WHERE uid=@uid",
            "owner",(long)p.Entity.UID,"expires",DateTime.Now.AddDays(7).Ticks,"uid",(long)item.UID);
        Query("INSERT INTO itemadding(uid,addingtype,addingid,addinglevel,addingpercent,duration,addedon,stabilizationpoints) VALUES(@uid,1,724409,5,6,0,@time,270),(@uid,1,3004137,6,7,604800,@time,270)","uid",(long)item.UID,"time",created);
        StoredMail(p,id,item.UID,100,20,1088001);
        Check(p.Account.State==AccountTable.AccountState.Player && p.Prizes[id].ItemPrize.UID==item.UID,"ordinary player loads a genuine persisted item parcel");
        Check(p.Prizes[id].ItemPrize.PerfectionLevel==54 && p.Prizes[id].ItemPrize.Plus==12 && p.Prizes[id].ItemPrize.RefineItem==3004137,"parcel loader preserves equipment details and active refinery");
        MailRequest(p,id,2);Check(p.Prizes.ContainsKey(id),"letter with unclaimed rewards cannot be discarded");
        for(int length=0;length<18;length++)Mailbox.Process(MailRequest(id,3).Take(length).ToArray(),p);
        foreach(ushort action in new ushort[]{0,7,256,259,65535})MailRequest(p,id,action);
        var bad=MailRequest(id,3);bad[0]--;Mailbox.Process(bad,p);
        Check(p.Entity.Money==0 && p.Prizes[id].goldprize==100,"short, malformed and unknown mailbox actions cannot credit a purse");
        p.Fake=true;MailRequest(p,id,3);p.Fake=false;p.Entity.FullyLoaded=false;MailRequest(p,id,3);p.Entity.FullyLoaded=true;
        Check(p.Entity.Money==0,"fake and unloaded sessions cannot claim mail");
        p.Entity.Money=Entity.MaximumSilver;MailRequest(p,id,3);
        Check(!Fenced(p) && p.Entity.Money==Entity.MaximumSilver && p.Prizes[id].goldprize==100,
            "full durable-range silver purse preserves an unclaimed mail reward");p.Entity.Money=0;
        Parallel.For(0,12,i=>MailRequest(p,id,3));
        Check(!Fenced(p) && p.Entity.Money==100 && Scalar("SELECT money FROM entities WHERE uid=@uid",p.Entity.UID)==100 && p.Prizes[id].goldprize==0,"nonzero low mail-ID bits are decoded correctly and concurrent gold claims commit once");
        Query("ALTER TABLE entities ADD CONSTRAINT mail_cp_failure CHECK(conquerpoints=0) NOT VALID");MailRequest(p,id,4);
        Check(Fenced(p) && p.Entity.ConquerPoints==0 && p.Prizes[id].cpsprize==20,"failed CP credit preserves the mail reward and live purse");
        Query("ALTER TABLE entities DROP CONSTRAINT mail_cp_failure");DemonReset(p);Parallel.For(0,12,i=>MailRequest(p,id,4));
        Check(!Fenced(p) && p.Entity.ConquerPoints==20 && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==20 && p.Prizes[id].cpsprize==0,"CP reward commits once with an immediate live balance update");
        var filling=Enumerable.Range(0,40).Select(i=>Pack(p,1088001,1)).ToArray();MailRequest(p,id,5);MailRequest(p,id,6);
        Check(p.Inventory.Count==40 && p.Prizes[id].ItemPrize!=null && p.Prizes[id].Attackment==1088001,"full inventory preserves both concrete and item-type attachments");
        foreach(var filler in filling){Query("DELETE FROM items WHERE uid=@uid","uid",(long)filler.UID);p.Inventory.Remove(filler.UID,Enums.ItemUse.None,false);}
        Query("ALTER TABLE prizes ADD CONSTRAINT mail_claim_failure CHECK(false) NOT VALID");MailRequest(p,id,5);
        Check(Fenced(p) && p.Inventory.Count==0 && SavedRune(item,"entityid")==0 && p.Prizes[id].ItemPrize!=null,"failed mail revision update rolls back item ownership transfer");
        Query("ALTER TABLE prizes DROP CONSTRAINT mail_claim_failure");DemonReset(p);Parallel.For(0,12,i=>MailRequest(p,id,5));
        var delivered=p.Inventory.Objects.Single();
        Check(!Fenced(p) && delivered.UID==item.UID && SavedRune(item,"entityid")==p.Entity.UID && p.Prizes[id].ItemPrize==null,"successful item claim preserves the original UID and transfers escrow once");
        Check(delivered.Plus==12 && delivered.PerfectionLevel==54 && delivered.PerfectionProgress==777 && delivered.PerfectionOwner=="Fixture"
            && RefineRows(delivered,"duration=0 AND stabilizationpoints=270")==1 && RefineRows(delivered,"duration>0 AND stabilizationpoints=270")==1,"parcel delivery preserves all equipment data, permanent fallback and paid stabilization credit");
        Parallel.For(0,12,i=>MailRequest(p,id,6));Check(p.Inventory.Count==2 && p.Inventory.Objects.Count(i=>i.ID==1088001)==1 && p.Prizes[id].Attackment==0,"item-type attachment is created once in the same mail claim transaction");
        MailboxTable.Load(p);MailRequest(p,id,3);MailRequest(p,id,4);MailRequest(p,id,5);MailRequest(p,id,6);
        Check(p.Entity.Money==100 && p.Entity.ConquerPoints==20 && p.Inventory.Count==2,"production mailbox reload cannot replay previously claimed rewards");
        MailRequest(p,id,2);MailboxTable.Load(p);Check(p.Prizes.Count==0,"empty letter deletion persists across reload");
        var outsider=MailPlayer(map);var foreign=Pack(outsider,410009,1);StoredMail(p,id,foreign.UID);MailRequest(p,id,5);
        Check(Fenced(p) && SavedRune(foreign,"entityid")==outsider.Entity.UID && p.Prizes[id].ItemPrize!=null,"a stale parcel pointing to another player's item cannot seize it");DemonReset(p);
        var usage=new ItemUsage(true){UID=foreign.UID,Action=ItemUsage.Mode.ItemsReturned};ItemUsage.Process(usage.ToArray(),p);
        Check(SavedRune(foreign,"entityid")==outsider.Entity.UID,"legacy returned-item action cannot bypass parcel escrow ownership");DemonReset(p);
        StoredMail(outsider,id+1);var escrow=MailEscrow(outsider,410009);usage=new ItemUsage(true){UID=escrow.UID,Action=ItemUsage.Mode.ItemsReturned};ItemUsage.Process(usage.ToArray(),p);
        Check(SavedRune(escrow,"entityid")==0 && !p.Inventory.Objects.Any(i=>i.UID==escrow.UID),"arbitrary ownerless item UID is not a mail entitlement");
        StoredMail(p,id,escrow.UID);ItemUsage.Process(usage.ToArray(),p);
        Check(!Fenced(p) && p.Inventory.Objects.Any(i=>i.UID==escrow.UID) && p.Prizes[id].ItemPrize==null,"returned-item action can deliver a genuinely entitled parcel through the same transaction");
        StoredMail(p,id,gold:25);Query("UPDATE prizes SET prizes=@empty WHERE uid=@uid","empty",new byte[]{0},"uid",(long)p.Entity.UID);MailRequest(p,id,3);
        Check(Fenced(p) && p.Entity.Money==100,"an externally changed mailbox revision cannot pay from stale cache");DemonReset(p);MailboxTable.Load(p);
        Mailbox.AddPrize(p,"Courier","Supplies","A new parcel.",10,5,600,1088001);uint issued=p.Prizes.Keys.Single();MailboxTable.Load(p);
        Check(p.Prizes.ContainsKey(issued) && p.Prizes[issued].goldprize==10,"server-issued mail is saved before its arrival notification");
        MailRequest(p,issued,3);p.Prizes[issued].goldprize=10;MailboxTable.Save(p);
        Check(Fenced(p) && p.Entity.Money==110,"background save rejects resurrection of an already claimed attachment");DemonReset(p);MailboxTable.Load(p);
        Check(p.Prizes[issued].goldprize==0,"reloading after rejected stale save preserves the durable claim");
        StoredMail(p,id,uint.MaxValue);MailboxTable.Save(p);MailboxTable.Load(p);MailRequest(p,id,5);
        Check(!Fenced(p) && p.Prizes[id].ItemPrize==null && p.Prizes.Count==1,"a missing parcel remains referenced for repair rather than being silently erased");
        var hidden=MailEscrow(outsider,410009);ConquerItemTable.ClearNulledItems();Check(SavedRune(hidden,"entityid")==0,"startup no longer deletes ownerless escrow without evidence of consumption");
        StoredMail(p,0,gold:7);Parallel.For(0,12,i=>MailRequest(p,0,3));MailboxTable.Load(p);MailRequest(p,0,3);
        Check(!Fenced(p) && p.Entity.Money==117 && p.Prizes[0].goldprize==0,"legacy zero-ID letters load and pay once across concurrent claims and reload");
        MailRequest(p,0,2);MailboxTable.Load(p);Check(p.Prizes.Count==0,"legacy zero-ID letters can be discarded after claiming rewards");
        EntityTable.UpdateMoney(p);Check(!Fenced(p) && Scalar("SELECT money FROM entities WHERE uid=@uid",p.Entity.UID)==117,"mail payout advances its durable silver preimage for later saves");
        StoredMail(p,0);var listing=new Mailbox();listing.Check(p,0);listing.Check(p,0);
        Check(System.BitConverter.ToUInt32(listing.ToArray(),5)==1,"reusing a mailbox list resets its record offset");
        foreach(uint page in new uint[]{1,2147483648,uint.MaxValue}){listing.Check(p,page);Check(System.BitConverter.ToUInt32(listing.ToArray(),5)==0,"out-of-range mail page returns an empty bounded list");}
        StoredMail(p,0,gold:10);Query("UPDATE entities SET money=money+1 WHERE uid=@uid","uid",(long)p.Entity.UID);MailRequest(p,0,3);
        Check(Fenced(p) && p.Prizes[0].goldprize==10 && Scalar("SELECT money FROM entities WHERE uid=@uid",p.Entity.UID)==118,
            "stale silver fences mail claims while preserving the reward and newer database credit");
    }
}
