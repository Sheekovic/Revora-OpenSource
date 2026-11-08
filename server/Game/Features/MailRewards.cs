using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class MailRewards
    {
        internal sealed class Record {
            internal uint Id,Time,Gold,Cp,Type,Item;
            internal string Sender,Subject,Message;
        }
        private sealed class Revision {internal byte[] Bytes;}
        private static readonly ConditionalWeakTable<GameState,Revision> versions=new ConditionalWeakTable<GameState,Revision>();
        internal static List<Record> Decode(byte[] data) {
            var result=new List<Record>();if(data.Length==0)return result;
            if(data.Length>1048576)throw new InvalidOperationException("Mailbox exceeds its stored size limit.");
            using(var stream=new MemoryStream(data))using(var r=new BinaryReader(stream)) {
                int count=r.ReadByte();var ids=new HashSet<uint>();
                for(int i=0;i<count;i++) {
                    var item=new Record{Id=r.ReadUInt32(),Time=r.ReadUInt32(),Sender=r.ReadString(),Subject=r.ReadString(),Message=r.ReadString(),
                        Gold=r.ReadUInt32(),Cp=r.ReadUInt32(),Type=r.ReadUInt32(),Item=r.ReadUInt32()};
                    // Existing mail includes ID zero; identity is mailbox-local and need only be unique.
                    if(!ids.Add(item.Id) || item.Sender.Length>32768 || item.Subject.Length>32768 || item.Message.Length>32768)
                        throw new InvalidOperationException("Mailbox record requires repair.");
                    result.Add(item);
                }
                // Historical rows may be zero-padded. Other trailing content is not silently lost.
                while(stream.Position<stream.Length)if(r.ReadByte()!=0)throw new InvalidOperationException("Mailbox has unrecognized trailing data.");
            }
            return result;
        }
        internal static byte[] Encode(IEnumerable<Record> records) {
            var all=records.ToArray();if(all.Length>255)throw new InvalidOperationException("Mailbox count exceeds the legacy format.");
            using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream)) {
                w.Write((byte)all.Length);foreach(var i in all){w.Write(i.Id);w.Write(i.Time);w.Write(i.Sender);w.Write(i.Subject);w.Write(i.Message);w.Write(i.Gold);w.Write(i.Cp);w.Write(i.Type);w.Write(i.Item);}
                if(stream.Length>1048576)throw new InvalidOperationException("Mailbox exceeds its stored size limit.");return stream.ToArray();
            }
        }
        private static byte[] Snapshot(GameState p) {
            return Encode(p.Prizes.Values.Select(i=>new Record{Id=i.ID,Time=i.Time,Sender=i.Sender,Subject=i.Subject,Message=i.Message,
                Gold=i.goldprize,Cp=i.cpsprize,Type=i.Attackment,Item=i.ItemPrize==null?i.ItemReference:i.ItemPrize.UID}));
        }
        private static byte[] Read(NpgsqlConnection c,NpgsqlTransaction t,uint uid) {
            using(var q=Q.Command(c,t,"SELECT prizes FROM prizes WHERE uid=@uid FOR UPDATE","uid",(long)uid)) {
                var value=q.ExecuteScalar();if(value==null)throw new InvalidOperationException("Mailbox owner missing.");return value==DBNull.Value?new byte[0]:(byte[])value;
            }
        }
        private static void Write(NpgsqlConnection c,NpgsqlTransaction t,uint uid,byte[] before,byte[] after) {
            if(Q.Execute(c,t,"UPDATE prizes SET prizes=@after WHERE uid=@uid AND COALESCE(prizes,@empty)=@before",
                "uid",(long)uid,"after",after,"before",before,"empty",new byte[0])!=1)throw new InvalidOperationException("Mailbox revision changed.");
        }
        internal static void Load(GameState p) {
            lock(p.ItemSyncRoot) {
                versions.Remove(p);
                byte[] data;using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,"INSERT INTO prizes(uid,name,prizes) VALUES(@uid,@name,@empty) ON CONFLICT(uid) DO NOTHING",
                    "uid",(long)p.Entity.UID,"name",p.Entity.Name,"empty",new byte[]{0});data=Read(c,null,p.Entity.UID);}
                var records=Decode(data);p.Prizes.Clear();
                foreach(var r in records)p.Prizes.Add(r.Id,new Mailbox.PrizeInfo{ID=r.Id,Time=r.Time,Sender=r.Sender,Subject=r.Subject,Message=r.Message,
                    goldprize=r.Gold,cpsprize=r.Cp,Attackment=r.Type,ItemReference=r.Item,ItemPrize=r.Item==0?null:LoadParcel(r.Item)});
                versions.GetValue(p,_=>new Revision()).Bytes=data;
            }
        }
        private static ConquerItem LoadParcel(uint uid) {
            var item=ConquerItemTable.LoadItem(uid);return item==null || item.ID==0?null:item;
        }
        internal static bool Ready(GameState p) {Revision ignored;return p!=null && versions.TryGetValue(p,out ignored);}
        internal static void ReturnOwnedParcel(GameState p,uint uid) {
            if(!Q.Player(p) || p.Prizes==null)return;
            lock(p.ItemSyncRoot) {
                var mail=p.Prizes.Values.FirstOrDefault(i=>(i.ItemPrize==null?i.ItemReference:i.ItemPrize.UID)==uid);
                if(mail==null || uid==0)return;
                var packet=new byte[18];Writer.WriteUInt16(10,0,packet);Writer.WriteUInt16(1045,2,packet);Writer.WriteUInt16(5,4,packet);Writer.WriteUInt32(mail.ID,6,packet);
                Process(p,packet);
            }
        }
        internal static void Save(GameState p) {
            if(p==null || p.Entity==null || p.ItemSyncRoot==null || p.Prizes==null || p.Fake || p.Entity.PersistenceRequiresReload)return;
            lock(p.ItemSyncRoot) {
                GameState current;Revision revision;
                if(!Kernel.GamePool.TryGetValue(p.Entity.UID,out current) || !ReferenceEquals(current,p) || !versions.TryGetValue(p,out revision))return;
                var after=Snapshot(p);if(after.SequenceEqual(revision.Bytes))return;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    var before=Read(c,t,p.Entity.UID);if(!before.SequenceEqual(revision.Bytes))throw new InvalidOperationException("Mailbox must reload before saving.");
                    var previous=Decode(before);var proposed=Decode(after).ToDictionary(i=>i.Id);
                    foreach(var old in previous) {
                        Record next;
                        if(!proposed.TryGetValue(old.Id,out next) || old.Time!=next.Time || old.Sender!=next.Sender || old.Subject!=next.Subject || old.Message!=next.Message
                            || old.Gold!=next.Gold || old.Cp!=next.Cp || old.Type!=next.Type || old.Item!=next.Item)
                            throw new InvalidOperationException("Only the claim transaction may change an existing letter.");
                    }
                    Write(c,t,p.Entity.UID,before,after);t.Commit();revision.Bytes=after;
                }}
            }
        }
        internal static bool Frame(byte[] packet) {
            return packet!=null && packet.Length>=18 && packet.Length<=1024 && BitConverter.ToUInt16(packet,0)==packet.Length-8 && BitConverter.ToUInt16(packet,2)==1045;
        }
        internal static void Process(GameState p,byte[] packet) {
            if(!Frame(packet) || !Q.Player(p,false) || p.Prizes==null)return;
            ushort action=BitConverter.ToUInt16(packet,4);uint id=BitConverter.ToUInt32(packet,6);
            if(action<1 || action>6)return;
            lock(p.ItemSyncRoot)try {
                Mailbox.PrizeInfo mail;
                if(!Q.Player(p,false) || !p.Prizes.TryGetValue(id,out mail))return;
                if(action==1){var reply=new byte[274];Writer.WriteUInt16(266,0,reply);Writer.WriteUInt16(1048,2,reply);Writer.WriteUInt32(id,4,reply);Writer.WriteString(mail.Message,8,reply);p.Send(reply);return;}
                if(!Q.Player(p) || p.Booth!=null || p.Trade==null || p.Trade.InTrade)return;
                Revision revision;if(!versions.TryGetValue(p,out revision))return;
                // Load before acquiring the item row lock: the legacy timed-item loader
                // may remove an expired item through its own connection.
                ConquerItem reward=null;uint reference=mail.ItemPrize==null?mail.ItemReference:mail.ItemPrize.UID;
                if(action==5) {
                    if(reference==0)return;if(p.Inventory.Count>=40){p.Send("Make one place in your bag. Your parcel will wait.");return;}
                    reward=ConquerItemTable.LoadItem(reference);if(reward==null || reward.ID==0 || reward.Warehouse!=0 || reward.Position!=0 || reward.InWardrobe){p.Send("This parcel needs to be checked before delivery.");return;}
                }
                if(action==6) {
                    if(mail.Attackment==0 || !ConquerItemInformation.BaseInformations.ContainsKey(mail.Attackment) || p.Inventory.Count>=40)return;
                    reward=Q.New(mail.Attackment,false);
                }
                byte[] after;ulong gold=p.Entity.Money;uint cp=p.Entity.ConquerPoints;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    if(action==3)SilverBalance.AssertSaved(c,t,p);
                    using(var q=Q.Command(c,t,"SELECT money,conquerpoints FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))using(var r=q.ExecuteReader()) {
                        if(!r.Read())throw new InvalidOperationException("Mail recipient missing.");
                        if(action==3 && Convert.ToDecimal(r.GetValue(0))!=gold || action==4 && Convert.ToUInt32(r.GetValue(1))!=cp){p.Send("Your purse is still being saved. Try again shortly.");return;}
                    }
                    var before=Read(c,t,p.Entity.UID);if(!before.SequenceEqual(revision.Bytes))throw new InvalidOperationException("Mailbox revision changed before claim.");
                    var records=Decode(before);var record=records.SingleOrDefault(i=>i.Id==id);
                    if(record==null || record.Gold!=mail.goldprize || record.Cp!=mail.cpsprize || record.Type!=mail.Attackment || record.Item!=reference)
                        throw new InvalidOperationException("Mailbox attachment cache changed.");
                    if(action==2) {
                        if(record.Gold!=0 || record.Cp!=0 || record.Type!=0 || record.Item!=0){p.Send("Claim your parcel's rewards before discarding the letter.");return;}
                        records.Remove(record);
                    }else if(action==3) {
                        if(record.Gold==0 || Revora.Game.Entity.MaximumSilver-gold<record.Gold)return;gold+=record.Gold;record.Gold=0;
                        if(Q.Execute(c,t,"UPDATE entities SET money=@gold WHERE uid=@uid","gold",(decimal)gold,"uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Mail silver was not saved.");
                    }else if(action==4) {
                        if(record.Cp==0 || uint.MaxValue-cp<record.Cp)return;cp+=record.Cp;record.Cp=0;
                        Q.Execute(c,t,"UPDATE entities SET conquerpoints=@cp WHERE uid=@uid","cp",(long)cp,"uid",(long)p.Entity.UID);
                    }else if(action==5) {
                        if(Q.Execute(c,t,@"UPDATE items SET entityid=@owner WHERE uid=@uid AND id=@type AND entityid=0 AND warehouse=0 AND position=0
AND stacksize=@stack AND bound=@bound","owner",(long)p.Entity.UID,"uid",(long)reference,"type",(int)reward.ID,
                            "stack",(int)reward.StackSize,"bound",reward.Bound?1:0)!=1)throw new InvalidOperationException("Mail parcel is not owned escrow.");
                        record.Item=0;
                    }else {Q.Insert(c,t,p.Entity.UID,reward);record.Type=0;}
                    after=Encode(records);Write(c,t,p.Entity.UID,before,after);t.Commit();
                }}
                revision.Bytes=after;
                if(action==2)p.Prizes.Remove(id);
                else if(action==3){mail.goldprize=0;SilverBalance.ApplyCommitted(p,gold);}
                else if(action==4){mail.cpsprize=0;p.Entity.ApplyCommittedConquerPoints(cp);}
                else {
                    if(action==5){mail.ItemPrize=null;mail.ItemReference=0;}else mail.Attackment=0;
                    reward.Mode=Enums.ItemMode.Default;Q.Install(p,new uint[0],new[]{reward});
                }
                if(p.Prizes.Count==0)Mailbox.SendMailbox(p,false);p.Send(packet);
            }catch(Exception ex){Q.Fail(p,"mail-reward",ex);}
        }
    }
}
