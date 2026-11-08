// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using Revora.Interfaces;

namespace Revora.Network.GamePackets
{
    public unsafe class ConfirmMap : Writer, IPacket
    {
        private byte[] Buffer;
        public ConfirmMap()
        {
            Buffer = new byte[28];
            Writer.WriteUInt16(20, 0, Buffer);
            Writer.WriteUInt16(1044, 2, Buffer);
        }
        public ushort X
        {
            get { return BitConverter.ToUInt16(Buffer, 6); }
            set { Writer.WriteUInt16(value, 6, Buffer); }
        }
        public ushort Y
        {
            get { return BitConverter.ToUInt16(Buffer, 8); }
            set { Writer.WriteUInt16(value, 8, Buffer); }
        }
        public ushort MapID
        {
            get { return BitConverter.ToUInt16(Buffer, 12); }
            set { Writer.WriteUInt16(value, 12, Buffer); }
        }
        public byte[] ToArray()
        {
            return Buffer;
        }
        public void Deserialize(byte[] buffer)
        {
            Buffer = buffer;
        }
        public void Send(Client.GameState client)
        {
            client.Send(Buffer);
        }
        public static void Process(Client.GameState client)
        {
            ConfirmMap Map = new ConfirmMap();
            Map.X = client.Entity.X;
            Map.Y = client.Entity.Y;
            Map.MapID = client.Entity.MapID;
            Map.Send(client);
        }
    }
}