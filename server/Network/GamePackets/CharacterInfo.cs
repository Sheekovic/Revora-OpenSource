using System;

namespace Revora.Network.GamePackets
{
    public class CharacterInfo : Writer, Interfaces.IPacket
    {
        Client.GameState client;
        public CharacterInfo(Client.GameState _client)
        {
            client = _client;
        }
        public void Deserialize(byte[] buffer)
        {
            throw new NotImplementedException();
        }
        public byte[] ToArray()
        {
            if (client == null || client.Entity == null || client.Entity.UID == 0)
                throw new InvalidOperationException("CharacterInfo requires an initialized character identity.");
            string name = client.Entity.Name ?? "";
            string spouse = client.Entity.Spouse ?? "None";
            byte[] level = new byte[153 + spouse.Length + name.Length + 8];
            WriteUInt16((ushort)(level.Length - 8), 0, level);
            WriteUInt16(1006, 2, level);
            WriteUInt32(client.Entity.UID, 8, level);
            WriteUInt32(client.Entity.Mesh, 14, level);
            WriteUInt32(client.Entity.HairStyle, 18, level);
            WriteUInt64(client.Entity.Money, 22, level);
            WriteUInt32(client.Entity.ConquerPoints, 30, level);
            WriteUInt64(client.Entity.Experience, 34, level);
            WriteUInt16(client.Entity.Strength, 62, level);
            WriteUInt16(client.Entity.Agility, 64, level);
            WriteUInt16(client.Entity.Vitality, 66, level);
            WriteUInt16(client.Entity.Spirit, 68, level);
            WriteUInt16(client.Entity.Atributes, 70, level);
            // Exact 6711 receiver layout; HP occupies all four bytes before mana.
            WriteUInt32(client.Entity.Hitpoints, 72, level);
            WriteUInt16(client.Entity.Mana, 76, level);
            WriteUInt16(client.Entity.PKPoints, 78, level);
            level[80] = client.Entity.Level;
            level[81] = client.Entity.Class;
            level[82] = client.Entity.FirstRebornClass;
            level[83] = client.Entity.SecondRebornClass;
            level[84] = (byte)client.Entity.NobilityRank;
            level[85] = client.Entity.Reborn;
            WriteUInt32(client.Entity.QuizPoints, 87, level);
            WriteUInt16(client.Entity.Windwalker, 91, level);
            WriteUInt16(0, 93, level);
            WriteUInt16(client.Entity.VIPLevel, 103, level);
            WriteUInt16((ushort)client.Entity.MyTitle, 107, level);
            WriteUInt32(client.Entity.BoundCps, 109, level);
            WriteByte(client.Entity.SubClasses.Active, 113, level);
            WriteUInt32(client.Entity.SubClasses.GetHashPoint(), 114, level);
            //racepoint
            WriteUInt16((ushort)client.Entity.CountryID, 126, level);
            //132 Domino Point 
            WriteUInt64(client.Entity.RevoraPoint, 132, level);           
            // Stable display identity is distinct from the Domino currency above.
            WriteUInt64(client.Entity.UID, 140, level);

            WriteByte(3, 148, level);
            level[149] = (byte)name.Length;
            WriteString(name, 150, level);
            WriteByte((byte)spouse.Length, 151 + name.Length, level);
            WriteString(spouse, 152 + name.Length, level);
            byte[] numArray = level;
            return numArray;
        }
        public void Send(Client.GameState client)
        {
            client.Send(ToArray());
        }
    }
}
