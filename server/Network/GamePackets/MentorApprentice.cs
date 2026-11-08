// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;

namespace Revora.Network.GamePackets
{
    public unsafe class MentorApprentice : Writer, Interfaces.IPacket
    {
        public const byte
        RequestApprentice = 1,
        RequestMentor = 2,
        LeaveMentor = 3,
        ExpellApprentice = 4,
        AcceptRequestApprentice = 8,
        AcceptRequestMentor = 9,
        DumpApprentice = 18,
        DumpMentor = 19;

        byte[] Buffer;
        public MentorApprentice(bool Create)
        {
            if (Create)
            {
                Buffer = new byte[32];
                WriteUInt16(24, 0, Buffer);
                WriteUInt16(2065, 2, Buffer);
            }
        }
        public uint Type
        {
            get { return BitConverter.ToUInt32(Buffer, 4); }
            set { WriteUInt32(value, 4, Buffer); }
        }
        public uint UID
        {
            get { return BitConverter.ToUInt32(Buffer, 8); }
            set { WriteUInt32(value, 8, Buffer); }
        }
        public uint dwParam
        {
            get { return BitConverter.ToUInt32(Buffer, 12); }
            set { WriteUInt32(value, 12, Buffer); }
        }
        public byte Dynamic
        {
            get { return Buffer[16]; }
            set { Buffer[16] = value; }
        }
        public bool Online
        {
            get { return Buffer[20] == 1; }
            set { Buffer[20] = value == true ? (byte)1 : (byte)0; }
        }
        public string Name
        {
            get
            {
                return System.Text.Encoding.Default.GetString(Buffer, 22, Buffer[21]);
            }
            set
            {
                byte[] encoded = new byte[16]; char[] chars = (value ?? "").ToCharArray(); int usedChars, usedBytes; bool complete;
                System.Text.Encoding.Default.GetEncoder().Convert(chars, 0, chars.Length, encoded, 0, encoded.Length, true,
                    out usedChars, out usedBytes, out complete);
                byte[] newBuffer = new byte[32 + usedBytes];
                Array.Copy(Buffer, newBuffer, Math.Min(Buffer.Length, 24));
                WriteUInt16((ushort)(24 + usedBytes), 0, newBuffer); newBuffer[21] = (byte)usedBytes;
                Array.Copy(encoded, 0, newBuffer, 22, usedBytes);
                Buffer = newBuffer;
            }
        }
        public void Deserialize(byte[] buffer)
        {
            Buffer = buffer;
        }
        public byte[] ToArray()
        {
            return Buffer;
        }
        public void Send(Client.GameState client)
        {
           // client.Send(Buffer);
        }
        public static void MentorInformation(byte[] packet, Revora.Client.GameState client)
        {
            MentorInformation info = new MentorInformation(false);
            info.Deserialize(packet);
            if (info.Mentor_Type == 1)
            {
                client.ReviewMentor();
            }
        }
        public static void MentorApprentices(byte[] packet, Revora.Client.GameState client)
        {
            if (!Game.Features.MentorAdmissions.Frame(packet) || !Game.Features.QuestInventory.Player(client)) return;
            uint type = BitConverter.ToUInt32(packet, 4);
            if (type == RequestApprentice || type == RequestMentor || type == AcceptRequestApprentice || type == AcceptRequestMentor)
            { Game.Features.MentorAdmissions.Process(packet, client); return; }
            Game.Features.MentorDepartures.Process(packet, client);
        }
    }
}
