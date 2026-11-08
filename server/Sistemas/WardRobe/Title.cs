using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Revora.Network.GamePackets
{
    public sealed class TitleStorage
    {
        public TitleStorage() { }
        public bool Read(byte[] packet)
        {
            Info=null;
            if(packet==null || packet.Length<4 || packet.Length>256 || BitConverter.ToUInt16(packet,0)!=packet.Length || BitConverter.ToUInt16(packet,2)!=3301)return false;
            try {
                using(var stream=new MemoryStream(packet,4,packet.Length-4,false))Info=Serializer.Deserialize<TitleStorageProto>(stream);
                return Info!=null && (Info.ActionId==Action.Equip || Info.ActionId==Action.Unequip)
                    && StorageManager.Known(Info.Type,Info.Id)
                    && (Info.Value==null || Info.Value.Type==Info.Type && Info.Value.Id==Info.Id);
            }
            catch(ProtoException){Info=null;return false;}
            catch(EndOfStreamException){Info=null;return false;}
            catch(OverflowException){Info=null;return false;}
        }
        public void Handle(Client.GameState client)
        {
            if(Info==null || client==null || !TitleRequirements.AllowRequest(client) || !StorageManager.Known(Info.Type,Info.Id)
                || (Info.ActionId!=Action.Equip && Info.ActionId!=Action.Unequip))return;
            lock(client.ItemSyncRoot) {
                TitleRequirements.Reconcile(client,false);
                var wardrobe=client.Entity.WTitles;
                if(wardrobe==null)return;
                lock(wardrobe.Sync) {
                    int key=Info.Type*10000+Info.Id;
                    if(!wardrobe.Records().ContainsKey(key))return;
                    bool equip=Info.ActionId==Action.Equip;
                    wardrobe.Update((short)Info.Type,(short)Info.Id,equip);
                    TitleRequirements.ApplyVisuals(client);
                    Notify(client,Info.ActionId,key,equip);
                }
            }
        }
        internal void Notify(Client.GameState client,Action action,int key,bool equipped)
        {
            client.Send(FinalizeProtoBuf(new TitleStorageProto {
                ActionId=action,Points=client.Entity.WTitles.Points,Type=key/10000,Id=key%10000,
                Value=new TitleValue{Type=key/10000,Id=key%10000,Equipped=equipped}
            }));
        }
        // Legacy callers cannot bypass the same authoritative requirements.
        public void AddTitle(Client.GameState client,short type,short id,bool equipped=false)
        {
            TitleRequirements.Reconcile(client,false);
            if(equipped && client.Entity.WTitles!=null) {
                client.Entity.WTitles.Update(type,id,true);TitleRequirements.ApplyVisuals(client);
            }
        }
        public void RemoveTitle(Client.GameState client,short type,short id,bool equipped=false) { TitleRequirements.Reconcile(client,false); }
        public void Login(Client.GameState client) { TitleRequirements.Login(client); }

        public byte[] FinalizeProtoBuf(TitleStorageProto titleStorageProto)
        {
            using (var memoryStream = new MemoryStream())
            {
                Serializer.SerializeWithLengthPrefix(memoryStream, titleStorageProto, PrefixStyle.Fixed32);
                var pkt = new byte[8 + memoryStream.Length];
                memoryStream.ToArray().CopyTo(pkt, 0);
                Writer.WriteUshort((ushort)memoryStream.Length, 0, pkt);
                Writer.WriteUshort((ushort)3301, 2, pkt);

                return pkt;
            }
        }


        public TitleStorageProto Info;
        [Flags]
        public enum Action : int
        {
            Update = 0,
            UseTitle = 1,
            RemoveTitle = 3,
            Equip = 4,
            Unequip = 5,
        }
    }
    [ProtoContract]
    public class TitleStorageProto
    {
        [ProtoMember(1, IsRequired = true)]
        public TitleStorage.Action ActionId;
        [ProtoMember(2, IsRequired = true)]
        public int Points;
        [ProtoMember(3, IsRequired = true)]
        public int Type;
        [ProtoMember(4, IsRequired = true)]
        public int Id;
        [ProtoMember(5, IsRequired = true)]
        public TitleValue Value;
    }
    [ProtoContract]
    public class TitleValue
    {
        [ProtoMember(1, IsRequired = true)]
        public int Type;
        [ProtoMember(2, IsRequired = true)]
        public int Id;
        [ProtoMember(3, IsRequired = true)]
        public bool Equipped;
        [ProtoMember(4, IsRequired = true)]
        public int dwParam4;
    }
}
