// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

namespace Revora.Network.GamePackets
{
    public unsafe class Purification : Writer, Interfaces.IPacket
    {
        public const byte
        Purify = 1,
        ItemArtifact = 0,
        Stabilaze3 = 3;

        byte[] Buffer;
        public Purification(bool Create)
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
        public uint AddUID
        {
            get { return BitConverter.ToUInt32(Buffer, 12); }
            set { WriteUInt32(value, 12, Buffer); }
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
            if (packet == null || packet.Length < 24 || packet.Length > 4096 || BitConverter.ToUInt16(packet, 0) != packet.Length - 8 || BitConverter.ToUInt16(packet, 2) != 2076) return;
            Purification ps = new Purification(false);
            ps.Deserialize(packet);
            switch (ps.Mode)
            {
                case Purification.ItemArtifact:
                    {
                        ItemHandler.PurifyRefinery(ps, client); break;
                    }
                case Purification.Purify:
                    {
                        ItemHandler.PurifyItem(ps, client); break;
                    }
            }
        }
    }
}