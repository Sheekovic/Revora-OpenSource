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
    public unsafe class MsgRouletteShareBetting : Writer
    {
        private byte[] Packet;
        public MsgRouletteShareBetting(int count, MsgRouletteOpenGui.Color color)
        {
            Packet = new byte[6 + count * 5 + 8];
            WriteUshort((ushort)(Packet.Length - 8), 0, Packet);
            WriteUshort(Network.GamePackets.PacketID.RouletteShareBetting, 2, Packet);
            WriteByte((byte)color, 4, Packet);
            WriteByte((byte)count, 5, Packet);
        }
        public void DisplayBettings(Database.Roulettes.RouletteTable.Member player)
        {
            ushort position = 6;
            var MyLuckNumbers = player.MyLuckNumber.Values.ToArray();
            var MyLuckExtras = player.MyLuckExtra.Values.ToArray();
            for (int x = 0; x < MyLuckNumbers.Length; x++)
            {
                var element = MyLuckNumbers[x];
                WriteByte(element.Number, position, Packet);
                WriteUint(element.BetPrice, position + 1, Packet);
                position += 5;
            }
            for (int x = 0; x < MyLuckExtras.Length; x++)
            {
                var element = MyLuckExtras[x];
                WriteByte(element.Number, position, Packet);
                WriteUint(element.BetPrice, position + 1, Packet);
                position += 5;
            }
        }
        public byte[] GetArray()
        {
            return Packet;
        }
    }
}