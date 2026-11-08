using System.Collections.Generic;
using System.Linq;
using System.Text;
using Revora.Client;
using Revora.Game;

namespace Revora.Network.GamePackets
{
    public unsafe class ElitePKMatchStats : Writer, Interfaces.IPacket
    {
        private byte[] Buffer;

        public ElitePKMatchStats()
        {
            Buffer = new byte[64 + 8];
            WriteUInt16(64, 0, Buffer);
            WriteUInt16(2222, 2, Buffer);
        }

        public void Append(ElitePK.Match match)
        {
            int offset = 4;
            var array = match.FightersStats;
            if (array.Length >= 2)
            {
                AppendPlayer(array[0], offset);
                offset += 28;
                AppendPlayer(array[1], offset);
            }
        }

        private void AppendPlayer(ElitePK.FighterStats player, int offset)
        {
            WriteUInt32(player.UID, offset, Buffer);
            offset += 4;
            WriteString(player.Name, offset, Buffer);
            offset += 16;
            WriteUInt32(0, offset, Buffer);
            offset += 4;
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