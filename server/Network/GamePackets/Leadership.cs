// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using Revora.Game;

namespace Revora.Network.GamePackets
{
    public unsafe class Leadership : Writer, Interfaces.IPacket
    {
        public const ushort
        Leader = 1,
        Teammate = 2;

        byte[] Buffer;
        public Leadership()
        {
            Buffer = new byte[40];
            WriteUInt16(32, 0, Buffer);
            WriteUInt16(2045, 2, Buffer);
        }
        public uint Type
        {
            get { return BitConverter.ToUInt32(Buffer, 4); }
            set { WriteUInt32(value, 4, Buffer); }
        }
        public uint UID
        {
            get { return BitConverter.ToUInt32(Buffer, 8); }
            set { WriteUInt32(value, 8, Buffer); }
        }
        public uint LeaderUID
        {
            get { return BitConverter.ToUInt32(Buffer, 12); }
            set { WriteUInt32(value, 12, Buffer); }
        }
        public uint Count
        {
            get { return BitConverter.ToUInt32(Buffer, 16); }
            set { WriteUInt32(value, 16, Buffer); }
        }
        public void Deserialize(byte[] buffer)
        {
            Buffer = buffer;
        }
        public byte[] ToArray()
        {
            return Buffer;
        }
        public void Send(Client.GameState client)
        {
            client.Send(Buffer);
        }
    }
}