using System.Collections.Generic;

namespace Revora.Network.GamePackets
{
    public unsafe class ItemAddingStabilization : Writer, Interfaces.IPacket
    {
        public const byte
        Refinery = 0,
        Artifact = 1;

        byte[] Buffer;
        public ItemAddingStabilization(bool Create)
        {
            if (Create)
            {
                Buffer = null;
            }
        }
        public byte Mode
        {
            get { return Buffer[4]; }
            set { Buffer[4] = value; }
        }
        public uint ItemUID
        {
            get { return BitConverter.ToUInt32(Buffer, 8); }
            set { WriteUInt32(value, 8, Buffer); }
        }
        public uint PurificationItemCount
        {
            get { return BitConverter.ToUInt32(Buffer, 12); }
            set { WriteUInt32(value, 12, Buffer); }
        }
        public List<uint> PurificationItems
        {
            get
            {
                List<uint> items = new List<uint>();
                for (int count = 16; count <= 12 + PurificationItemCount * 4; count += 4)
                {
                    items.Add(BitConverter.ToUInt32(Buffer, count));
                }
                return items;
            }
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
            if (packet == null || packet.Length < 24 || packet.Length > 184 || BitConverter.ToUInt16(packet, 0) != packet.Length - 8 || BitConverter.ToUInt16(packet, 2) != PacketID.ItemAdding) return;
            uint count = BitConverter.ToUInt32(packet, 12);
            if (count > 40 || packet.Length != 24 + count * 4) return;
            ItemAddingStabilization ps = new ItemAddingStabilization(false);
            ps.Deserialize(packet);
            switch (ps.Mode)
            {
                case ItemAddingStabilization.Refinery:
                    {
                        ItemHandler.StabilazeRefinery(ps, client);
                        break;
                    }
                case ItemAddingStabilization.Artifact:
                    {
                        ItemHandler.StabilazeArtifact(ps, client);
                        break;
                    }
                default: System.Console.WriteLine("[ItemAddingStabilization] Unknown Mode: " + ps.Mode + ""); break;
            }
        }
    }
}