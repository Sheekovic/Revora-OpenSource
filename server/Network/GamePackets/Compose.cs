namespace Revora.Network.GamePackets
{
    public unsafe class Compose : Writer, Interfaces.IPacket
    {
        public const byte
        Plus = 0,
        CurrentSteed = 2,
        NewSteed = 3,
        QuickCompose = 4,
        ChanceUpgrade = 5,
        MeteorUpgrade = 6,
        DragonBallUpgrade = 7;

        byte[] Buffer;
        public Compose(bool Create)
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
        public byte Count
        {
            get { return Buffer[5]; }
            set { Buffer[5] = value; }
        }
        public byte Countx
        {
            get { return Buffer[8 + 4]; }
            set { Buffer[8 + 4] = value; }
        }
        public uint this[int index]
        {
            get { return BitConverter.ToUInt32(Buffer, 8 + 4 * index); }
            set { WriteUInt32(value, 8 + 4 * index, Buffer); }
        }
        public uint GetItem(int index)
        {
            return BitConverter.ToUInt32(Buffer, 12 + 4 * index);
        }
        public byte[] ToArray()
        {
            return Buffer;
        }
        public void Deserialize(byte[] buffer)
        {
            Buffer = buffer;
        }
        public void Send(Client.GameState client)
        {
            client.Send(Buffer);
        }
    }
}