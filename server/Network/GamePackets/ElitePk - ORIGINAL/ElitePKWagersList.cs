// * Programador y editor de Sources Chavarro
// * Derechos Reservados © 2015 - 2016
// * Slayer [ Server Privado ] - Proyecto TQ

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using COServer.Game;
using COServer.Client;

namespace COServer.Network.GamePackets
{
    public unsafe class ElitePKWagersList : Writer, Interfaces.IPacket
    {
        public const byte
            SendList = 3,
            RequestList = 4;

        byte[] Buffer;

        public ElitePKWagersList(bool create, int matchcount = 0)
        {
            if (create)
            {
                Buffer = new byte[12 + matchcount * 16 + 8];
                WriteUInt16((ushort)(Buffer.Length - 8), 0, Buffer);
                WriteUInt16(1065, 2, Buffer);
            }
        }

        public byte Group
        {
            get { return Buffer[4]; }
            set { Buffer[4] = value; }
        }

        public byte Page //not sure.
        {
            get { return Buffer[6]; }
            set { Buffer[6] = value; }
        }

        public uint TotalMatches
        {
            get { return BitConverter.ToUInt32(Buffer, 8); }
            set { WriteUInt32(value, 8, Buffer); }
        }

        public uint WagedUID
        {
            get { return BitConverter.ToUInt32(Buffer, 12); }
            set { WriteUInt32(value, 12, Buffer); }
        }

        public void Append(ElitePK.Match match, int index)
        {
            int offset = 12 + index * 16;
            var array = match.FightersStats;
            if (array.Length == 2)
            {
                WriteUInt32(array[0].UID, offset, Buffer); offset += 4;
                WriteUInt32(array[1].UID, offset, Buffer); offset += 4;
                WriteUInt32(array[0].Wager, offset, Buffer); offset += 4;
                WriteUInt32(array[1].Wager, offset, Buffer); offset += 4;
            }
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
