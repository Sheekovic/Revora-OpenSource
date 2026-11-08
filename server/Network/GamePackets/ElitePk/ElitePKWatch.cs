using System.Collections.Generic;
using System.Linq;
using System.Text;
using Revora.Game;
using Revora.Client;

namespace Revora.Network.GamePackets
{
    public unsafe class ElitePKWatch : Writer, Interfaces.IPacket
    {
        public const byte
            RequestView = 0,
            Watchers = 2,
            Leave = 3,
            Fighters = 4;

        private byte[] Buffer;

        public ElitePKWatch(bool create, int watchers = 0)
        {
            if (create)
            {
                Buffer = new byte[26 + watchers * 36 + 8];
                WriteUInt16((ushort)(Buffer.Length - 8), 0, Buffer);
                WriteUInt16(2211, 2, Buffer);
                WatcherCount = 2;
            }
        }

        public byte Type
        {
            get { return Buffer[4]; }
            set { Buffer[4] = value; }
        }

        public uint dwParam
        {
            get { return BitConverter.ToUInt32(Buffer, 6); }
            set { WriteUInt32(value, 6, Buffer); }
        }

        public uint ID
        {
            get { return BitConverter.ToUInt32(Buffer, 10); }
            set { WriteUInt32(value, 10, Buffer); }
        }

        public uint WatcherCount
        {
            get { return BitConverter.ToUInt32(Buffer, 14); }
            set { WriteUInt32(value, 14, Buffer); }
        }

        public uint dwCheers1
        {
            get { return BitConverter.ToUInt32(Buffer, 18); }
            set { WriteUInt32(value, 18, Buffer); }
        }

        public uint dwCheers2
        {
            get { return BitConverter.ToUInt32(Buffer, 22); }
            set { WriteUInt32(value, 22, Buffer); }
        }

        private int index = 0;

        public void Append(string name)
        {
            int offset = 26 + index * 32;
            WriteString(name, offset, Buffer);
            index++;
        }

        public void Append(uint mesh, string name)
        {
            int offset = 26 + index * 36;
            WriteUInt32(mesh, offset, Buffer);
            offset += 4;
            WriteString(name, offset, Buffer);
            index++;
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