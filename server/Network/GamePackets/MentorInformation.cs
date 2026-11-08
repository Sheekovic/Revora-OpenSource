// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Text;
using System.Timers;

namespace Revora.Network.GamePackets
{
    public unsafe class MentorInformation : Writer, Interfaces.IPacket
    {
        private byte[] Buffer;
        private string m_MentorName = "", m_MentorSpouse = "", m_ApprenticeName = "";
        public MentorInformation(bool create)
        {
            if (create)
            {
                Buffer = new byte[158];
                WriteUInt16((ushort)(Buffer.Length - 8), 0, Buffer);
                WriteUInt16(2066, 2, Buffer);
                WriteUInt32(999999, 24, Buffer);
            }
        }
        public uint Mentor_Type
        {
            get { return BitConverter.ToUInt32(Buffer, 4); }
            set { WriteUInt32(value, 4, Buffer); }
        }
        public uint Mentor_ID
        {
            get { return BitConverter.ToUInt32(Buffer, 8); }
            set { WriteUInt32(value, 8, Buffer); }
        }
        public uint Apprentice_ID
        {
            get { return BitConverter.ToUInt32(Buffer, 12); }
            set { WriteUInt32(value, 12, Buffer); }
        }
        public uint Mentor_Mesh
        {
            get { return BitConverter.ToUInt32(Buffer, 16); }
            set { WriteUInt32(value, 16, Buffer); }
        }
        public uint Shared_Battle_Power
        {
            get { return BitConverter.ToUInt32(Buffer, 20); }
            set
            {
                if (value > 200)
                    value = 0;
                if (value < 0)
                    value = 0;
                WriteUInt32(value, 20, Buffer);
            }
        }
        public uint Enrole_Date
        {
            get { return BitConverter.ToUInt32(Buffer, 28); }
            set { WriteUInt32(value, 28, Buffer); }
        }
        public uint Mentor_Level
        {
            get { return BitConverter.ToUInt32(Buffer, 32); }
            set { WriteUInt32(value, 32, Buffer); }
        }
        public byte Mentor_Class
        {
            get { return Buffer[33]; }
            set { Buffer[33] = value; }
        }
        public ushort Mentor_PkPoints
        {
            get { return BitConverter.ToUInt16(Buffer, 34); }
            set { WriteUInt16(value, 34, Buffer); }
        }
        public bool Mentor_Online
        {
            get { return (Buffer[56] == 1); }
            set { Buffer[56] = (byte)(value ? 1 : 0); }
        }
        public uint Apprentice_Experience
        {
            get { return BitConverter.ToUInt32(Buffer, 64); }
            set { WriteUInt32(value, 64, Buffer); }
        }
        public ushort Apprentice_Blessing
        {
            get { return BitConverter.ToUInt16(Buffer, 72); }
            set { WriteUInt16(value, 72, Buffer); }
        }
        public ushort Apprentice_Composing
        {
            get { return BitConverter.ToUInt16(Buffer, 74); }
            set { WriteUInt16(value, 74, Buffer); }
        }
        public byte String_Count
        {
            get { return Buffer[76]; }
            set { Buffer[76] = value; }
        }
        public string Mentor_Name { get { return m_MentorName; } set { m_MentorName = value ?? ""; } }
        public string Mentor_Spouse_Name { get { return m_MentorSpouse; } set { m_MentorSpouse = value ?? ""; } }
        public string Apprentice_Name { get { return m_ApprenticeName; } set { m_ApprenticeName = value ?? ""; } }
        internal static byte[] EncodeName(string value)
        {
            var target = new byte[16]; var chars = (value ?? "").ToCharArray(); int usedChars, usedBytes; bool complete;
            Encoding.Default.GetEncoder().Convert(chars, 0, chars.Length, target, 0, 16, true, out usedChars, out usedBytes, out complete);
            Array.Resize(ref target, usedBytes); return target;
        }
        public void Null(int Offset)
        {
            Buffer[Offset] = 0;
        }
        public byte[] ToArray()
        {
            var mentor = EncodeName(Mentor_Name); var spouse = EncodeName(Mentor_Spouse_Name); var child = EncodeName(Apprentice_Name);
            Array.Clear(Buffer, 77, Buffer.Length - 8 - 77);
            Buffer[77] = (byte)mentor.Length; Array.Copy(mentor, 0, Buffer, 78, mentor.Length);
            int spouseOffset = 79 + mentor.Length; Buffer[spouseOffset] = (byte)spouse.Length; Array.Copy(spouse, 0, Buffer, spouseOffset + 1, spouse.Length);
            int childOffset = 81 + mentor.Length + spouse.Length; Buffer[childOffset] = (byte)child.Length; Array.Copy(child, 0, Buffer, childOffset + 1, child.Length);
            return Buffer;
        }
        public void Deserialize(byte[] buffer)
        {
            Buffer = buffer;
        }
        public void Send(Client.GameState client)
        {
           // client.Send(Buffer);
        }
    }
}