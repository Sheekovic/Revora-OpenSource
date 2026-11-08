// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Linq;
using System.Text;
using Revora.Network;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Revora.Roulette
{
    public unsafe class MsgRouletteScreen : Interfaces.IPacket
    {
        public byte[] packet;
        public static unsafe MsgRouletteScreen Create()
        {
            MsgRouletteScreen ptr = new MsgRouletteScreen();
            ptr.Length = 9;
            ptr.UnKnow = 1;
            ptr.PacketID = Network.GamePackets.PacketID.RouletteScreen;
            return ptr;
        }
        public ushort Length
        {
            set
            {
                packet = new byte[value + 8];
                Writer.WriteUshort((ushort)(packet.Length - 8), 0, packet);
            }
        }
        public ushort PacketID
        {
            set { Writer.WriteUshort(value, 2, packet); }
        }
        public byte UnKnow
        {
            set { Writer.Byte((byte)value, 4, packet); }
        }
        public uint UID
        {
            set { Writer.WriteUint((uint)value, 5, packet); }
        }
        public byte[] ToArray()
        {
            return packet;
        }
        public void Send(Client.GameState client)
        {
            client.Send(ToArray());
        }
        public void Deserialize(byte[] buffer)
        {
            throw new NotImplementedException();
        }
    }
}