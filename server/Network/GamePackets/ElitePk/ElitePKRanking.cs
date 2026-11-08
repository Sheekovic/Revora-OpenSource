using System.Collections.Generic;
using System.Linq;
using System.Text;
using Revora.Game;

namespace Revora.Network.GamePackets
{
    public unsafe class ElitePKRanking : Writer, Interfaces.IPacket
    {
        public const byte Top8 = 0, Top3 = 2;

        private byte[] buffer;

        public ElitePKRanking(bool create)
        {
            if (create)
            {
                buffer = new byte[420 + 8];
                WriteUInt16(420, 0, buffer);
                WriteUInt16(2223, 2, buffer);
            }
        }

        public uint Type
        {
            get { return BitConverter.ToUInt32(buffer, 4); }
            set { WriteUInt32(value, 4, buffer); }
        }

        public uint Group
        {
            get { return BitConverter.ToUInt32(buffer, 8); }
            set { WriteUInt32(value, 8, buffer); }
        }

        public uint GroupStatus
        {
            get { return BitConverter.ToUInt32(buffer, 12); }
            set { WriteUInt32(value, 12, buffer); }
        }

        public uint Count
        {
            get { return BitConverter.ToUInt32(buffer, 16); }
            set { WriteUInt32(value, 16, buffer); }
        }

        public uint UID
        {
            get { return BitConverter.ToUInt32(buffer, 20); }
            set { WriteUInt32(value, 20, buffer); }
        }

        private int Index = 0;
        public void Append(ElitePK.FighterStats stats, int rank)
        {
            int offset = 36 + Index * 44; Index++;
            WriteInt32(rank, offset, buffer); offset += 4;
            WriteString(stats.Name, offset, buffer); offset += 16;
            WriteUInt32(stats.Mesh, offset, buffer); offset += 4;
            WriteUInt32(0, offset, buffer); offset += 4;
            WriteUInt32(0, offset, buffer); offset += 4;
            WriteUInt32(stats.UID, offset, buffer);
        }

        public byte[] ToArray()
        {
            return buffer;
        }

        public void Send(Client.GameState client)
        {
            client.Send(ToArray());
        }


        public void Deserialize(byte[] buffer)
        {
            this.buffer = buffer;
        }
    }
}