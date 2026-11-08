// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace Revora.Roulette
{
    public unsafe class MsgRouletteRecord : Network.Writer
    {
        private byte[] Packet = null;
        public void ApplayUser(Database.Roulettes.RouletteTable.Member[] members, byte WinnerNumber)
        {

            Packet = new byte[24 * members.Length + 6 + 8];
            WriteUshort((ushort)(Packet.Length - 8), 0, Packet);
            WriteUshort(Network.GamePackets.PacketID.RouletteRecord, 2, Packet);
            WriteByte(WinnerNumber, 4, Packet);
            WriteByte((byte)members.Length, 5, Packet);
            for (int x = 0; x < members.Length; x++)
            {
                var element = members[x];
                int off = (x * 24) + 6;
                WriteUint(element.Betting, off, Packet);
                off += 4;
                WriteUint(element.Winning, off, Packet);
                off += 4;
                WriteString(element.Owner.Entity.Name, off, Packet);
            }
        }
        public void SendInfo(Client.GameState client)
        {
            if (Packet != null)
            {
                client.Send(Packet);
            }
        }
    }
}