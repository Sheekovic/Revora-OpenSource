/*using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Slayer.Network.GamePackets
{
    public class LastLogin : Writer, Interfaces.IPacket
    {
        private byte[] buffer;

        public LastLogin()
        {
            buffer = new byte[12 + 8];
            WriteUInt16(12, 0, buffer);
            WriteUInt16(2078, 2, buffer);
        }

        public uint TotalSeconds
        {
            get
            {
                return BitConverter.ToUInt32(buffer, 4);
            }
            set
            {
                WriteUInt32(value, 4, buffer);
            }
        }

        public bool DifferentCity
        {
            get
            {
                return buffer[8] == 1;
            }
            set
            {
                buffer[8] = value ? (byte)1 : (byte)0;
            }
        }

        public bool DifferentPlace
        {
            get
            {
                return buffer[9] == 1;
            }
            set
            {
                buffer[9] = value ? (byte)1 : (byte)0;
            }
        }

        public void Deserialize(byte[] data)
        {
            throw new NotImplementedException();
        }

        public byte[] ToArray()
        {
            return buffer;
        }

        public void Send(Client.GameState client)
        {
            client.Send(buffer);
        }
    }
}*/