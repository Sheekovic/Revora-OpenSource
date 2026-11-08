// * Programador y editor de Sources Chavarro
// * Derechos Reservados © 2015 - 2016
// * Slayer [ Server Privado ] - Proyecto TQ

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using COServer.Client;
using COServer.Game;

namespace COServer.Network.GamePackets
{
    public unsafe class ElitePKMatchStats : Writer, Interfaces.IPacket
    {
        byte[] Buffer;

        public ElitePKMatchStats()
        {
            Buffer = new byte[56 + 8];
            WriteUInt16(56, 0, Buffer);
            WriteUInt16(2222, 2, Buffer);
        }

        public void Append(ElitePK.Match match)
        {
            int offset = 4;
            var array = match.FightersStats;
            if (array.Length >= 2)
            {
                AppendPlayer(array[0], offset); offset += 24;
                AppendPlayer(array[1], offset);
            }
        }

        private void AppendPlayer(ElitePK.FighterStats player, int offset)
        {
            WriteUInt32(player.UID, offset, Buffer); offset += 4;
            WriteString(player.Name, offset, Buffer); offset += 16;
            WriteUInt32(player.Points, offset, Buffer); 
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
