using System;
using System.Collections.Generic;

namespace Revora.Network.GamePackets
{
    public unsafe class MsgName : Writer, Interfaces.IPacket
    {
        byte[] Packet;
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
        public MsgName(bool Create)
        {
            if (Create)
            {
                Packet = new byte[23];//23
                WriteUInt16((ushort)(Packet.Length - 8), 0, Packet);
                WriteUInt16((ushort)PacketID.MsgName, 2, Packet);
                WriteUInt32((uint)Time32.timeGetTime().GetHashCode(), 4, Packet);
                Texts = new List<string>();
            }
        }
        public ushort PositionX
        {
            get { return (ushort)UID; }
            set { UID = (uint)((PositionY << 16) | value); }//
        }

        public ushort PositionY
        {
            get { return (ushort)(UID >> 16); }//
            set { UID = (uint)((value << 16) | PositionX); }
        }
        public uint UID
        {
            get { return BitConverter.ToUInt32(Packet, 8); }//
            set { WriteUInt32(value, 8, Packet); }
        }
        public Mode Action
        {
            get { return (Mode)Packet[12]; }//
            set { Packet[12] = (byte)value; }
        }
        public byte TextsCount
        {
            get { return Packet[13]; }//
            set { Packet[13] = value; }
        }
        public List<string> Texts;
        public byte[] ToArray()
        {
            ushort entirelength = 23;//23
            foreach (string list in Texts)
                entirelength += (ushort)list.Length;
            byte[] buffer = new byte[entirelength];
            WriteUInt16((ushort)(entirelength - 8), 0, buffer);
            WriteUInt16((ushort)PacketID.MsgName, 2, buffer);
            WriteUInt32(UID, 8, buffer);
            WriteUInt32((uint)Time32.timeGetTime().GetHashCode(), 4, buffer);
            buffer[12] = Packet[12];
            Packet = buffer;
            WriteStringList(Texts, 13, Packet);
            return Packet;
        }
        public void Deserialize(byte[] buffer)
        {
            Packet = buffer;
            Texts = new List<string>(buffer[13]);
            ushort offset = 14;
            byte count = 0;
            while (count != TextsCount)
            {
                ushort textlength = buffer[offset]; offset++;
                string text = Server.Encoding.GetString(buffer, offset, textlength);
                offset += textlength;
                Texts.Add(text);
                count++;
            }
        }
        public ushort X
        {
            get { return BitConverter.ToUInt16(Packet, 8); }
            set { Write(value, 8, Packet); }
        }
        public ushort Y
        {
            get { return BitConverter.ToUInt16(Packet, 10); }
            set { Write(value, 8, Packet); }
        }
        public int Type;
        public void Send(Client.GameState client)
        {
            client.Send(Packet);
        }
        
    }
}
