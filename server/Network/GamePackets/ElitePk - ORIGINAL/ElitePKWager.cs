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
    public unsafe class ElitePKWager : Writer, Interfaces.IPacket
    {
        public const byte
            SetWager = 2;

        byte[] Buffer;

        public ElitePKWager()
        {
        }

        public byte Type
        {
            get { return Buffer[4]; }
            set { Buffer[4] = value; }
        }

        public uint dwParam
        {
            get { return BitConverter.ToUInt32(Buffer, 8); }
            set { WriteUInt32(value, 8, Buffer); }
        }

        public uint WagedUID
        {
            get { return BitConverter.ToUInt32(Buffer, 12); }
            set { WriteUInt32(value, 12, Buffer); }
        }

        public uint Wager
        {
            get { return BitConverter.ToUInt32(Buffer, 20); }
            set { WriteUInt32(value, 20, Buffer); }
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
