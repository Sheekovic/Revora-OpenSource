// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using Revora.Game;
using Revora.Client;

namespace Revora.Network.GamePackets
{
    public unsafe class RacePotion : Writer, Interfaces.IPacket
    {
        private byte[] buffer;
        public RacePotion(bool create)
        {
            if (create)
            {
                buffer = new byte[24];
                WriteUInt16(16, 0, buffer);
                WriteUInt16(2072, 2, buffer);
            }
        }
        public ushort Amount
        {
            get { return BitConverter.ToUInt16(buffer, 4); }
            set { WriteUInt16(value, 4, buffer); }
        }
        public Enums.RaceItemType PotionType
        {
            get { return (Enums.RaceItemType)BitConverter.ToUInt16(buffer, 6); }
            set { WriteUInt16((ushort)value, 6, buffer); }
        }
        public int Location
        {
            get { return BitConverter.ToInt32(buffer, 8); }
            set { WriteInt32(value, 8, buffer); }
        }
        public uint dwParam
        {
            get { return BitConverter.ToUInt32(buffer, 12); }
            set { WriteUInt32(value, 12, buffer); }
        }
        public void Deserialize(byte[] buffer)
        {
            this.buffer = buffer;
        }
        public void Send(GameState client)
        {
            client.Send(buffer);
        }
        public byte[] ToArray()
        {
            return buffer;
        }
        public static void Process(byte[] packet, GameState client)
        {
            RacePotion pot = new RacePotion(false);
            pot.Deserialize(packet);
            int i = pot.Location - 1;
            if (i < 0 || i > 4) return;
            if (client.Potions[i] == null) return;
            var potion = client.Potions[i];
            potion.Count--;
            pot.PotionType = potion.Type;
            pot.Amount = (ushort)potion.Count;
            client.Send(pot);
            client.ApplyRacePotion(potion.Type, pot.dwParam);
            if (potion.Count == 0)
                client.Potions[i] = null;
        }
    }
}