// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

namespace Revora.Network.GamePackets
{
    public unsafe class CurrentFlags : Writer, Interfaces.IPacket
    {
        byte[] buffer;
        public CurrentFlags(uint heroID, Game.Enums.CountryID countryID)
        {
            buffer = new byte[18];
            WriteUInt16(10, 0, buffer);
            WriteUInt16(2430, 2, buffer);
            WriteUInt32(heroID, 4, buffer);
            WriteUInt16((ushort)countryID, 8, buffer);
        }
        public CurrentFlags(byte[] buffer)
        {
            this.buffer = buffer;
        }
        public uint HeroID
        {
            get { return BitConverter.ToUInt32(buffer, 4); }
            set { WriteUInt32(value, 4, buffer); }
        }
        public Game.Enums.CountryID CountryID
        {
            get { return (Game.Enums.CountryID)BitConverter.ToUInt32(buffer, 8); }
            set { WriteUInt16((ushort)value, 8, buffer); }
        }
        public byte[] ToArray()
        {
            return buffer;
        }
        public void Deserialize(byte[] buffer)
        {
            this.buffer = buffer;
        }
        public void Send(Client.GameState client)
        {
            client.Send(buffer);
        }
        public static void Process(byte[] packet, Client.GameState client)
        {
            CurrentFlags cur = new CurrentFlags(packet);
            client.Entity.CountryID = cur.CountryID;
            cur.HeroID = client.Entity.UID;
            client.SendScreen(cur, true);
        }
    }
}