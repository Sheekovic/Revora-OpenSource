// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using Revora.Game;
using Revora.Client;

namespace Revora.Network.GamePackets
{
    public class DialyQuestsEffect
    {

        public static void CheckSaqueo(GameState client)
        {
            client.Entity.OnCheckSaqueo = true;
            client.Entity.CheckSaqueoStamp = Time32.Now;
            // client.Player.ConquerPoints += 10000000;
            string EffectName = "Destapando Cofre";
            byte[] Packet = new byte[47 + 4 + EffectName.Length];
            Writer.WriteUInt16((ushort)(Packet.Length - 8), 0, Packet);
            Writer.WriteUInt16(10010, 2, Packet);
            Writer.WriteUInt32(client.Entity.UID, 8, Packet);
            Writer.WriteUInt32(220, 12, Packet);
            Writer.WriteUInt16(164, 24, Packet);
            Writer.WriteUInt16((ushort)Enums.ConquerAngle.West, 26, Packet);
            Writer.WriteUInt32(3, 36, Packet);
            Writer.WriteByte(1, 41, Packet);
            Writer.WriteByte((byte)(EffectName.Length), 42, Packet);
            Writer.WriteString(EffectName, 43, Packet);
            client.Send(Packet);
        }
        public static void CheckCancelSaqueo(GameState client)
        {
            client.Entity.OnCheckSaqueo = false;
            byte[] Packet = new byte[51];
            Writer.WriteUInt16((ushort)(Packet.Length - 8), 0, Packet);
            Writer.WriteUInt16(10010, 2, Packet);
            Writer.WriteUInt32(client.Entity.UID, 8, Packet);
            Writer.WriteUInt16(164, 24, Packet);
            Writer.WriteUInt16((ushort)Enums.ConquerAngle.North, 26, Packet);
            Writer.WriteUInt32(0, 36, Packet);
            Writer.WriteByte(1, 41, Packet);
            client.Send(Packet);
        }
        public static void CheckSaqueo(GameState client, string CheckName)
        {
            byte[] Packet = new byte[47 + 4 + CheckName.Length];
            Writer.WriteUInt16((ushort)(Packet.Length - 8), 0, Packet);
            Writer.WriteUInt16(10010, 2, Packet);
            Writer.WriteUInt32(client.Entity.UID, 8, Packet);
            Writer.WriteUInt32(220, 12, Packet);
            Writer.WriteUInt16(164, 24, Packet);
            Writer.WriteUInt16((ushort)Enums.ConquerAngle.West, 26, Packet);
            Writer.WriteUInt32(1, 36, Packet);
            Writer.WriteByte(1, 41, Packet);
            Writer.WriteByte((byte)(CheckName.Length), 42, Packet);
            Writer.WriteString(CheckName, 43, Packet);
            client.Send(Packet);
        }



        public static void CheckGuard(GameState client)
        {
            client.Entity.OnCheckGuard = true;
            client.Entity.CheckGuardStamp = Time32.Now;
            string EffectName = "Check";
            byte[] Packet = new byte[47 + 4 + EffectName.Length];
            Writer.WriteUInt16((ushort)(Packet.Length - 8), 0, Packet);
            Writer.WriteUInt16(10010, 2, Packet);
            Writer.WriteUInt32(client.Entity.UID, 8, Packet);
            Writer.WriteUInt32(220, 12, Packet);
            Writer.WriteUInt16(164, 24, Packet);
            Writer.WriteUInt16((ushort)Enums.ConquerAngle.West, 26, Packet);
            Writer.WriteUInt32(3, 36, Packet);
            Writer.WriteByte(1, 41, Packet);
            Writer.WriteByte((byte)(EffectName.Length), 42, Packet);
            Writer.WriteString(EffectName, 43, Packet);
            client.Send(Packet);
        }
        public static void CheckCancelGuard(GameState client)
        {
            client.Entity.OnCheckGuard = false;
            byte[] Packet = new byte[51];
            Writer.WriteUInt16((ushort)(Packet.Length - 8), 0, Packet);
            Writer.WriteUInt16(10010, 2, Packet);
            Writer.WriteUInt32(client.Entity.UID, 8, Packet);
            Writer.WriteUInt16(164, 24, Packet);
            Writer.WriteUInt16((ushort)Enums.ConquerAngle.North, 26, Packet);
            Writer.WriteUInt32(0, 36, Packet);
            Writer.WriteByte(1, 41, Packet);
            client.Send(Packet);
        }
        public static void Check(GameState client, string CheckName)
        {
            byte[] Packet = new byte[47 + 4 + CheckName.Length];
            Writer.WriteUInt16((ushort)(Packet.Length - 8), 0, Packet);
            Writer.WriteUInt16(10010, 2, Packet);
            Writer.WriteUInt32(client.Entity.UID, 8, Packet);
            Writer.WriteUInt32(220, 12, Packet);
            Writer.WriteUInt16(164, 24, Packet);
            Writer.WriteUInt16((ushort)Enums.ConquerAngle.West, 26, Packet);
            Writer.WriteUInt32(1, 36, Packet);
            Writer.WriteByte(1, 41, Packet);
            Writer.WriteByte((byte)(CheckName.Length), 42, Packet);
            Writer.WriteString(CheckName, 43, Packet);
            client.Send(Packet);
        }
        public static void CheckStudyPoint(GameState client, uint amount)
        {
            byte[] buffer = new byte[38];
            Writer.WriteUInt16(30, 0, buffer);
            Writer.WriteUInt16(2320, 2, buffer);
            Writer.WriteUInt32(8, 8, buffer);
            Writer.WriteUInt32(client.Entity.SubClasses.StudyPoints, 10, buffer);
            Writer.WriteUInt32(amount, 18, buffer);
            client.Send(buffer);
        }
    }
}