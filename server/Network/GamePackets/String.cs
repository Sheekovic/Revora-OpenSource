// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Text;
using System.Collections.Generic;

namespace Revora.Network.GamePackets
{
    public unsafe class _String : Writer, Interfaces.IPacket
    {
        public enum Mode : byte
        {
            None = 0,
            Fireworks = 1,
            CreateSyn = 2,
            GuildName = 3,
            ChangeTitle = 4,
            DeleteRole = 5,
            Spouse = 6,
            QueryNPC = 7,
            Wanted = 8,
            MapEffect = 9,
            Effect = 10,
            GuildList = 11,
            KickOut_SynMem = 12,
            QueryWanted = 13,
            QueryPoliceWanted = 14,
            PoliceWanted = 15,
            QuerySpouse = 16,
            BeginGamble = 17,
            EndGamble = 18,
            BonusGamble = 19,
            Sound = 20,
            GuildAllies = 21,
            GuildEnemies = 22,
            WhisperDetails = 26,
            ServerName = 60,
            ServerName2 = 61
        }
        public const byte
        None = 0,
        Fireworks = 1,
        CreateSyn = 2,
        GuildName = 3,
        ChangeTitle = 4,
        DeleteRole = 5,
        Spouse = 6,
        QueryNPC = 7,
        Wanted = 8,
        MapEffect = 9,
        Effect = 10,
        GuildList = 11,
        KickOut_SynMem = 12,
        QueryWanted = 13,
        QueryPoliceWanted = 14,
        PoliceWanted = 15,
        ViewEquipSpouse = 16,
        BeginGamble = 17,
        EndGamble = 18,
        BonusGamble = 19,
        Sound = 20,
        GuildAllies = 21,
        GuildEnemies = 22,
        WhisperDetails = 26,
        ServerName = 60,
        RemoveGuildColor = 61;

        byte[] Buffer;
        public _String(bool Create)
        {
            if (Create)
            {
                Buffer = new byte[108];
                WriteUInt16(100, 0, Buffer);
                WriteUInt16(1015, 2, Buffer);
                WriteUInt32((uint)Time32.timeGetTime().GetHashCode(), 4, Buffer);
                Texts = new List<string>();
            }
        }
        public Mode Action
        {
            get { return (Mode)Buffer[12]; }
            set { Buffer[12] = (byte)value; }
        }
        public uint UID
        {
            get { return BitConverter.ToUInt32(Buffer, 8); }
            set { WriteUInt32(value, 8, Buffer); }
        }
        public byte Type
        {
            get { return Buffer[12]; }
            set { Buffer[12] = value; }
        }
        public byte TextsCount
        {
            get { return Buffer[13]; }
            set { Buffer[13] = value; }
        }
        public List<string> Texts;
        public void Send(Client.GameState client)
        {
            client.Send(Buffer);
        }
        public byte[] ToArray()
        {
            ushort entirelength = 23;
            foreach (string list in Texts)
                entirelength += (ushort)list.Length;
            byte[] buffer = new byte[entirelength];
            WriteUInt16((ushort)(entirelength - 8), 0, buffer);
            WriteUInt16(1015, 2, buffer);
            WriteUInt32(UID, 8, buffer);
            WriteUInt32((uint)Time32.timeGetTime().GetHashCode(), 4, buffer);
            buffer[12] = Type;
            Buffer = buffer;
            WriteStringList(Texts, 13, Buffer);
            return Buffer;
        }
        public void Deserialize(byte[] buffer)
        {
            Buffer = buffer;
            Texts = new List<string>(buffer[13]);
            ushort offset = 14;
            byte count = 0;
            while (count != TextsCount)
            {
                ushort textlength = buffer[offset]; offset++;
                string text = Server.Encoding.GetString(buffer, offset, textlength); offset += textlength;
                Texts.Add(text);
                count++;
            }
        }
        public static void Process(byte[] packet, Client.GameState client)
        {
            if (client.Action != 2) return;
            _String stringpacket = new _String(false);
            stringpacket.Deserialize(packet);
            switch (stringpacket.Type)
            {
                case _String.WhisperDetails:
                    {
                        if (stringpacket.Texts.Count > 0)
                        {
                            foreach (var pClient in Kernel.GamePool.Values)
                            {
                                if (pClient.Entity.Name == stringpacket.Texts[0])
                                {
                                    //stringpacket = new _String(true);
                                    string otherstring = "";
                                    otherstring += pClient.Entity.UID + " ";
                                    otherstring += pClient.Entity.Level + " ";
                                    otherstring += pClient.Entity.BattlePower + " #";
                                    if (pClient.Entity.GuildID != 0)
                                        otherstring += pClient.Guild.Name + " # ";
                                    else
                                        otherstring += "None # ";
                                    otherstring += pClient.Entity.Spouse + " ";
                                    otherstring += (byte)(pClient.Entity.NobilityRank) + " ";
                                    if (pClient.Entity.Body % 10 < 3)
                                        otherstring += "2 ";
                                    else
                                        otherstring += "1 ";
                                    stringpacket.Texts.Add(otherstring);
                                    client.Send(stringpacket);
                                }
                            }
                        }
                        break;
                    }
                //default: Console.WriteLine("Unknown 1015 packet type " + stringpacket.Type); break;
            }
        }
    }
}