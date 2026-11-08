using System;

namespace COServer.Network.GamePackets
{
    public unsafe class CharacterInfo
    {
        byte[] Packet;
        public CharacterInfo(Client.GameState client)
        {
            Packet = new byte[138 + client.Entity.Spouse.Length + client.Entity.Name.Length];
            fixed (byte* Pack = Packet)
            {
                *(ushort*)(Pack + (Packet.Length - 8)) = 0;
                *((ushort*)(Pack + 2)) = 1006;
                *((uint*)(Pack + 4)) = (uint)Time32.timeGetTime().GetHashCode();
                *((uint*)(Pack + 8)) = client.Entity.UID;
                *((uint*)(Pack + 14)) = client.Entity.Mesh;
                *((ushort*)(Pack + 18)) = client.Entity.HairStyle;
                *((uint*)(Pack + 20)) = client.Entity.Money;
                *((uint*)(Pack + 28)) = client.Entity.ConquerPoints;
                *((ulong*)(Pack + 32)) = client.Entity.Experience;
                *((ushort*)(Pack + 60)) = client.Entity.Strength;
                *((ushort*)(Pack + 62)) = client.Entity.Agility;
                *((ushort*)(Pack + 64)) = client.Entity.Vitality;
                *((ushort*)(Pack + 66)) = client.Entity.Spirit;
                *((ushort*)(Pack + 68)) = client.Entity.Atributes;
                *((ushort*)(Pack + 70)) = (ushort)client.Entity.Hitpoints;
                *((ushort*)(Pack + 72)) = client.Entity.Mana;
                *((ushort*)(Pack + 74)) = client.Entity.PKPoints;
                *((byte*)(Pack + 78)) = client.Entity.Level;
                *((byte*)(Pack + 79)) = client.Entity.Class;
                *((byte*)(Pack + 80)) = client.Entity.FirstRebornClass;
                *((byte*)(Pack + 81)) = client.Entity.SecondRebornClass;
                *((byte*)(Pack + 83)) = client.Entity.Reborn;
                *((uint*)(Pack + 85)) = client.Entity.QuizPoints;
                if (client.Entity.FirstCreditPoints >= 5000)
                {
                    *((uint*)(Pack + 85)) = 1;
                }
                if (client.Entity.ClaimedFirstCredit)
                {
                    *((uint*)(Pack + 85)) = 7;
                }
                *((ushort*)(Pack + 93)) = client.Entity.EnlightenPoints;
                *((ushort*)(Pack + 94)) = client.Entity.VIPLevel;
                *((ushort*)(Pack + 105)) = (ushort)client.Entity.MyTitle;
                *((uint*)(Pack + 107)) = (uint)client.Entity.BoundCps;
                *((byte*)(Pack + 123)) = 1;
                *((ushort*)(Pack + 128)) = (ushort)client.Entity.CountryID;
                *((byte*)(Pack + 130)) = 3;
                *((byte*)(Pack + 131)) = (byte)client.Entity.Name.Length;
                Writer.WriteString(client.Entity.Name, 132, Packet);
                *((byte*)(Pack + 133 + client.Entity.Name.Length)) = (byte)client.Entity.Spouse.Length;
                Writer.WriteString(client.Entity.Spouse, 134 + client.Entity.Name.Length, Packet);
            }
        }
        public byte[] ToArray()
        {
            fixed (void* Pack = Packet)
                return Packet;
        }
    }
}