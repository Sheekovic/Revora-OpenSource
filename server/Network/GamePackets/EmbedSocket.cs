using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Revora.Network.GamePackets
{
    public class EmbedSocket : Writer, Interfaces.IPacket
    {
        public const ushort Add = 0,
                            Remove = 1,
                            SlotOne = 1,
                            SlotTwo = 2;

        byte[] Buffer;

        public EmbedSocket(bool Create)
        {
            if (Create)
            {
                Buffer = null;
            }
        }

        public uint ItemUID
        {
            get { return BitConverter.ToUInt32(Buffer, 8 + 4); }
            set { WriteUInt32(value, 8 + 4, Buffer); }
        }

        public uint GemUID
        {
            get { return BitConverter.ToUInt32(Buffer, 12 + 4); }
            set { WriteUInt32(value, 12 + 4, Buffer); }
        }

        public ushort Slot
        {
            get { return BitConverter.ToUInt16(Buffer, 16 + 4); }
            set { WriteUInt16(value, 16 + 4, Buffer); }
        }

        public ushort Mode
        {
            get { return BitConverter.ToUInt16(Buffer, 18 + 4); }
            set { WriteUInt16(value, 18 + 4, Buffer); }
        }

        public void Send(Client.GameState client)
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
        public static void Process(byte[] packet, Client.GameState client)
        {
            EmbedSocket socket = new EmbedSocket(false);
            socket.Deserialize(packet);
            ItemHandler.SocketItem(socket, client);
        }
    }
}