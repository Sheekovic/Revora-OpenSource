using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Revora.Network.GamePackets
{
    public class InnerPowerGui : Writer
    {
        private byte[] stream;

        public InnerPowerGui(Game.InnerPower.Stage.NeiGong[] gongs)
        {
            stream = new byte[15 + (ushort)(gongs != null ? (gongs.Length * 5) : 0)];
            WriteUInt16((ushort)(stream.Length - 8), 0, stream);
            WriteUInt16(2612, 2, stream);

            if (gongs != null)
            {
                WriteUInt16((ushort)gongs.Length, 4, stream);
                ushort position = 6;
                for (int x = 0; x < gongs.Length; x++)
                {
                    var element = gongs[x];
                    WriteByte(element.ID, position, stream); position += 1;
                    WriteUInt32(element.Score, position, stream); position += 4;
                }

            }
        }

        public byte[] ToArray()
        {
            return stream;
        }
    }
}
