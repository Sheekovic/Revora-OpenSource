// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Linq;
using System.Text;
using Revora.Network;
using System.Collections.Generic;

namespace Revora.Roulette
{
    public unsafe class MsgRouletteNoWinner : Interfaces.IPacket
    {
        public byte[] packet;
        public static MsgRouletteNoWinner Create()
        {
            MsgRouletteNoWinner packet = new MsgRouletteNoWinner();
            packet.Length = 5;
            packet.PacketID = Network.GamePackets.PacketID.RouletteNoWinner;
            return packet;
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
        public byte Number
        {
            set { Writer.Byte(value, 4, packet); }
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
