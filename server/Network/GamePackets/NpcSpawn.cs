using Revora.Interfaces;
using ProtoBuf;

namespace Revora.Network.GamePackets
{
    public class NpcSpawn : Writer, IPacket, INpc, IMapObject
    {
        [ProtoContract]
        public class CMsgNpcInfo
        {
            [ProtoMember(1, IsRequired = true)]
            public uint TimeStamp;
            [ProtoMember(2, IsRequired = true)]
            public uint UID;
            [ProtoMember(3, IsRequired = true)]
            public ulong NpcEffect;
            [ProtoMember(4, IsRequired = true)]
            public ulong X;
            [ProtoMember(5, IsRequired = true)]
            public ulong Y;
            [ProtoMember(6, IsRequired = true)]
            public ulong Mesh;
            [ProtoMember(7, IsRequired = true)]
            public ulong Type;
            [ProtoMember(9, IsRequired = true)]
            public byte[] Name;
        }
        private CMsgNpcInfo Buffer;
        private ushort _MapID;
        ushort packetid = 2030;
        public NpcSpawn()
        {
            Buffer = new CMsgNpcInfo();
        }

        string _Name;
        public string Name
        {
            get
            {
                return _Name;
            }
            set
            {
                _Name = value;
                Buffer.Name = System.Text.Encoding.Default.GetBytes(value);
            }
        }

        public uint UID
        {
            get { return Buffer.UID; }
            set { Buffer.UID = value; }
        }
        public uint NpcEffect
        {
            get { return (uint)Buffer.NpcEffect; }
            set { Buffer.NpcEffect = value; }
        }
        public ushort X
        {
            get { return (ushort)Buffer.X; }
            set { Buffer.X = value; }
        }

        public ushort Y
        {
            get { return (ushort)Buffer.Y; }
            set { Buffer.Y = value; }
        }

        public ushort Mesh
        {
            get { return (ushort)Buffer.Mesh; }
            set { Buffer.Mesh = value; }
        }
        public string effect { get; set; }
        public Game.Enums.NpcType Type
        {
            get { return (Game.Enums.NpcType)Buffer.Type; }
            set { Buffer.Type = (byte)value; }
        }

        public ushort MapID { get { return _MapID; } set { _MapID = value; } }

        public Game.MapObjectType MapObjType { get { return Game.MapObjectType.Npc; } }

        public Client.GameState Owner { get { return null; } }

        public uint Hitpoints;
      
        public void SendSpawn(Client.GameState client, bool checkScreen)
        {
            if (client.Screen.Add(this) || !checkScreen)
            {
                Buffer.TimeStamp = Buffer.UID;
                client.Send(Buffer.ToConquerPacket(packetid));
                if (effect != "" && effect != null)
                {
                    _String str = new _String(true);
                    str.UID = UID;
                    str.TextsCount = 22;
                    str.Type = _String.Effect;
                    str.Texts.Add(effect);
                    client.SendScreen(str, true);
                }
            }
        }
        public void SendSpawn(Client.GameState client)
        {
            SendSpawn(client, false);
            if (effect != "" && effect != null)
            {
                _String str = new _String(true);
                str.UID = UID;
                str.TextsCount = 22;
                str.Type = _String.Effect;
                str.Texts.Add(effect);
                client.Send(str);
            }
        }
        public byte[] ToArray()
        {
            Buffer.TimeStamp = Buffer.UID;
            return Buffer.ToConquerPacket(packetid);
        }
        public void Deserialize(byte[] buffer)
        {
            Buffer = buffer.ConvertToProto<CMsgNpcInfo>();
        }
        public void Send(Client.GameState client)
        {
            SendSpawn(client, false);
        }
    }
}