using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace COServer.Network.GamePackets
{
    public sealed class TitleStorage
    {
        public TitleStorage() { }
        public bool Read(byte[] packet)
        {
            using (var memoryStream = new MemoryStream(packet))
            {
                Info = Serializer.DeserializeWithLengthPrefix<TitleStorageProto>(memoryStream, PrefixStyle.Fixed32);
            }
            return true;
        }
        public void Handle(Client.GameState client)
        {
            switch (Info.ActionId)
            {
                case Action.Unequip:
                    if (StorageManager.Wing<bool>(Info.Type, Info.Id))
                    {
                        client.Player.EquippedWing = 0;
                        client.Player.WTitles.Update((short)Info.Type, (short)Info.Id, false);
                    }
                    else if (StorageManager.Title<bool>(Info.Type, Info.Id))
                    {
                        client.Player.EquippedTitle = 0;
                        client.Player.WTitles.Update((short)Info.Type, (short)Info.Id, false);
                    }
                    client.Send(FinalizeProtoBuf(Info));
                    break;
                case Action.Equip:
                    if (StorageManager.Wing<bool>(Info.Type, Info.Id))
                    {
                        client.Player.EquippedWing = StorageManager.Wing<int>(Info.Type, Info.Id);
                        client.Player.WTitles.Update((short)Info.Type, (short)Info.Id, true);
                    }
                    else if (StorageManager.Title<bool>(Info.Type, Info.Id))
                    {
                        client.Player.EquippedTitle = StorageManager.Title<int>(Info.Type, Info.Id);
                        client.Player.WTitles.Update((short)Info.Type, (short)Info.Id, true);
                    }

                    Info = new TitleStorageProto()
                    {
                        ActionId = Action.Equip,
                        Points = client.Player.WTitles.Points,
                        Type = Info.Type,
                        Id = Info.Id,
                        Value = new TitleValue()
                        {
                            Type = Info.Type,
                            Id = Info.Id,
                            Equipped = false
                        }
                    };

                    client.Send(FinalizeProtoBuf(Info));
                    break;
            }
        }
        public void AddTitle(Client.GameState client, short _type, short _id, bool equipped = false)
        {

            if (client.Player.WTitles == null)
            {
                if (!StorageManager.Data.TryGetValue(client.Player.UID, out client.Player.WTitles))
                {
                    client.Player.WTitles = new WardrobeTitles();
                    client.Player.WTitles.Id = client.Player.UID;
                    client.Player.WTitles.Data = new byte[1];
                    client.Player.WTitles.Create();
                }
            }

            var pkt = new TitleStorageProto()
            {
                ActionId = Action.Update,
                Points = client.Player.WTitles.Points,
                Type = _type,
                Id = _id,
                Value = new TitleValue()
                {
                    Type = _type,
                    Id = _id,
                    Equipped = equipped
                }
            };

            client.Send(FinalizeProtoBuf(pkt));

            client.Player.WTitles.Points += StorageManager.GetTitlePoints(_type, _id);
            client.Player.UTitlePoints = client.Player.WTitles.Points;

            client.Player.WTitles.AddTitle(_type, _id, equipped);

            if (!StorageManager.Data.ContainsKey(client.Player.WTitles.Id))
                StorageManager.Data.Add(client.Player.WTitles.Id, client.Player.WTitles);
            else StorageManager.Data[client.Player.WTitles.Id] = client.Player.WTitles;

            if (equipped)
                if (StorageManager.Wing<bool>(_type, _id))
                    client.Player.EquippedWing = StorageManager.Wing<int>(_type, _id);
                else if (StorageManager.Title<bool>(_type, _id))
                    client.Player.EquippedTitle = StorageManager.Title<int>(_type, _id);
        }
        public void RemoveTitle(Client.GameState client, short _type, short _id, bool equipped = false)
        {
            client.Player.WTitles.Update((short)_type, (short)_id, false);
            var pkt = new TitleStorageProto()
            {
                ActionId = Action.RemoveTitle,
                Points = client.Player.WTitles.Points,
                Type = _type,
                Id = _id,
                Value = new TitleValue()
                {
                    Type = _type,
                    Id = _id,
                    Equipped = equipped
                }
            };

            client.Send(FinalizeProtoBuf(pkt));
        }
        public void Login(Client.GameState client)
        {
            if (client.Player.WTitles == null)
            {
                client.Player.WTitles = StorageManager.Find(t => t.Id == client.Player.UID);
            }
            if (client.Player.WTitles == null || client.Player.WTitles.Data == null || client.Player.WTitles.Data.Length == 0) return;
            var myPacketReader = new BinaryReader(new MemoryStream(client.Player.WTitles.Data));
            var _count = myPacketReader.ReadByte();
            for (var i = 0; i < _count; i++)
            {
                var _type = myPacketReader.ReadInt16();
                var _id = myPacketReader.ReadInt16();
                var _equipped = myPacketReader.ReadBoolean();
                var pkt = new TitleStorageProto()
                {
                    ActionId = Action.Update,
                    Points = client.Player.WTitles.Points,
                    Type = _type,
                    Id = _id,
                    Value = new TitleValue()
                    {
                        Type = _type,
                        Id = _id,
                        Equipped = _equipped
                    }
                };
                client.Player.WTitles.Points += StorageManager.GetTitlePoints(_type, _id);

                if (_equipped)
                    if (StorageManager.Wing<bool>(_type, _id))
                        client.Player.EquippedWing = StorageManager.Wing<int>(_type, _id);
                    else if (StorageManager.Title<bool>(_type, _id))
                        client.Player.EquippedTitle = StorageManager.Title<int>(_type, _id);

                client.Send(FinalizeProtoBuf(pkt));
            }
        }

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