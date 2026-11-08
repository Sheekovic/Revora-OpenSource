using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Revora.Network.GamePackets
{
    public class Chi : Writer, Interfaces.IPacket
    {
        public const byte
            Unlock = 0,
            QueryInfo = 1,
            Study = 2,
            BuyStrength = 3;

        byte[] Buffer;

        public Chi(bool Create)
        {
            if (Create)
            {
                Buffer = new byte[8 + 16];
                Writer.WriteUInt16(16, 0, Buffer);
                Writer.WriteUInt16(2533, 2, Buffer);
            }
        }

        public uint UID
        {
            get { return BitConverter.ToUInt32(Buffer, 4); }
            set { WriteUInt32(value, 4, Buffer); }
        }

        public ushort Type
        {
            get { return BitConverter.ToUInt16(Buffer, 8); }
            set { WriteUInt16(value, 8, Buffer); }
        }

        public Game.Enums.ChiPowerType Mode
        {
            get { return (Game.Enums.ChiPowerType)Buffer[10]; }
            set { Buffer[10] = (byte)value; }
        }

        public uint dwParam
        {
            get { return BitConverter.ToUInt32(Buffer, 11); }
            set { WriteUInt32(value, 11, Buffer); }
        }

        public void Send(Client.GameState client)
        {
            client.Send(Buffer);
        }

        public byte[] ToArray()
        {
            return Buffer;
        }

        public void Deserialize(byte[] buffer)
        {
            Buffer = buffer;
        }
    }
}
