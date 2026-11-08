// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace Revora.Network.GamePackets
{
    public unsafe class SummonGuard : Writer, Interfaces.IPacket
    {
        byte[] buffer;
        public SummonGuard()
        {
            buffer = new byte[108];
            WriteUInt16(100, 0, buffer);
            WriteUInt16(2035, 2, buffer);
        }
        public uint Action
        {
            get { return BitConverter.ToUInt32(buffer, 4); }
            set { Writer.WriteUInt32(value, 4, buffer); }
        }
        public uint UID
        {
            get { return BitConverter.ToUInt32(buffer, 8); }
            set { Writer.WriteUInt32(value, 8, buffer); }
        }
        public uint LookFace
        {
            get { return BitConverter.ToUInt32(buffer, 16); }
            set { Writer.WriteUInt32(value, 16, buffer); }
        }
        public uint Unknown
        {
            get { return BitConverter.ToUInt32(buffer, 20); }
            set { Writer.WriteUInt32(value, 20, buffer); }
        }
        public uint Unknown1
        {
            get { return BitConverter.ToUInt32(buffer, 24); }
            set { Writer.WriteUInt32(value, 24, buffer); }
        }
        public string Name
        {
            set { Writer.WriteStringWithLength(value, 26, buffer); }
        }
        public byte[] ToArray()
        {
            return buffer;
        }
        public void Send(Client.GameState client)
        {
            client.Send(buffer);
        }
        public void Deserialize(byte[] buffr)
        {
            buffer = buffr;
        }
    }
}
