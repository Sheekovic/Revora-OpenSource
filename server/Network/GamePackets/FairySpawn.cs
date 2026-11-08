// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

namespace Revora.Network.GamePackets
{
    public unsafe class FairySpawn : Writer, Interfaces.IPacket
    {
        private byte[] buf;
        public FairySpawn(bool create)
        {
            if (create)
            {
                buf = new byte[28];
                WriteUInt16(20, 0, buf);
                WriteUInt16(2070, 2, buf);
            }
        }
        public uint SType
        {
            get { return BitConverter.ToUInt32(buf, 4); }
            set { WriteUInt32(value, 4, buf); }
        }
        public uint FairyType
        {
            get { return BitConverter.ToUInt32(buf, 12); }
            set { WriteUInt32(value, 12, buf); }
        }
        public uint UID
        {
            get { return BitConverter.ToUInt32(buf, 16); }
            set { WriteUInt32(value, 16, buf); }
        }
        public byte[] ToArray()
        {
            return buf;
        }
        public void Deserialize(byte[] buffer)
        {
            buf = buffer;
        }
        public void Send(Client.GameState client)
        {
            client.Send(buf);
        }
        public static void Process(byte[] packet, Client.GameState client)
        {
            FairySpawn FS = new FairySpawn(false);
            FS.Deserialize(packet);
            switch (FS.SType)
            {
                case 1:
                    {
                        if (client.IsFairy == false)
                        {
                            client.IsFairy = true;
                            client.FairyType = FS.FairyType;
                            client.SType = FS.SType;
                            FS.UID = client.Entity.UID;
                            client.SendScreen(FS, true);
                        }
                        else client.Send(new Message("Cancel The Other Transformer First", Message.System));
                        break;
                    }
                case 2:
                    {
                        if (client.IsFairy == false) return;
                        client.IsFairy = false;
                        client.FairyType = 0;
                        client.SType = 0;
                        FS.UID = client.Entity.UID;
                        client.SendScreen(FS, true);
                        break;
                    }
                default: System.Console.WriteLine("[FairySpawn] Unknown SType: " + FS.SType + ""); break;
            }
        }
    }
}