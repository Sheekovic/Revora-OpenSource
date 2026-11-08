using System.Collections.Generic;
using System.Linq;
using System.Text;
using Revora.Client;

namespace Revora.Network.GamePackets
{
    public unsafe class ElitePKMatchUI : Writer, Interfaces.IPacket
    {
        public const byte
            Information = 1,
            BeginMatch = 2,
            Effect = 3,
            EndMatch = 4;

        public const uint
            Effect_Win = 1,
            Effect_Lose = 0;

        private byte[] Buffer;

        public ElitePKMatchUI(bool create)
        {
            if (create)
            {
                Buffer = new byte[52 + 8];
                WriteUInt16(52, 0, Buffer);
                WriteUInt16(2218, 2, Buffer);
            }
        }

        public uint Type
        {
            get { return BitConverter.ToUInt32(Buffer, 4); }
            set { WriteUInt32(value, 4, Buffer); }
        }

        public uint dwParam
        {
            get { return BitConverter.ToUInt32(Buffer, 8); }
            set { WriteUInt32(value, 8, Buffer); }
        }

        public uint OpponentUID
        {
            get { return BitConverter.ToUInt32(Buffer, 12); }
            set { WriteUInt32(value, 12, Buffer); }
        }

        public string OpponentName
        {
            get { return Encoding.Default.GetString(Buffer, 16, 16).Trim(); }
            set { WriteString(value, 16, Buffer); }
        }

        public uint TimeLeft
        {
            get { return BitConverter.ToUInt32(Buffer, 44); }
            set { WriteUInt32(value, 44, Buffer); }
        }

        public void Append(GameState opponent)
        {
            OpponentUID = opponent.Entity.UID;
            OpponentName = opponent.Entity.Name;
        }

        public void Send(GameState client)
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