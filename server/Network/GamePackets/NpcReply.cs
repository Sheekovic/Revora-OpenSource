// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Text;

namespace Revora.Network.GamePackets
{
    public unsafe class NpcReply : Writer, Interfaces.IPacket
    {
        public const byte
        Dialog = 1,
        Option = 2,
        Input = 3,
        Avatar = 4,
        MessageBox = 6,
        Finish = 100;

        private byte[] Buffer;
        public NpcReply()
        {
            Buffer = new byte[28];
            WriteUInt16((ushort)20, 0, Buffer);
            WriteUInt16(2032, 2, Buffer);
            WriteUInt32((uint)Time32.timeGetTime().GetHashCode(), 4, Buffer);
        }
        public NpcReply(byte interactType, string text)
        {
            Buffer = new byte[29];
            WriteUInt16((ushort)(21 + text.Length), 0, Buffer);
            WriteUInt16(2032, 2, Buffer);
            WriteUInt32((uint)Time32.timeGetTime().GetHashCode(), 4, Buffer);
            InteractType = interactType;
            OptionID = 255;
            DontDisplay = true;
            Text = text;
        }
        public void Reset()
        {
            OptionID = 255;
            DontDisplay = true;
            Text = "";
        }
        public ushort InputMaxLength
        {
            get { return BitConverter.ToUInt16(Buffer, 12); }
            set { WriteUInt16(value, 12, Buffer); }
        }
        public byte OptionID
        {
            get { return Buffer[14]; }
            set { Buffer[14] = value; }
        }
        public byte InteractType
        {
            get { return Buffer[15]; }
            set { Buffer[15] = value; }
        }   
        public bool DontDisplay
        {
            get { return (Buffer[16] == 1); }
            set { Buffer[16] = (byte)(value ? 1 : 0); }
        }
        public string Text
        {
            get { return Encoding.Default.GetString(Buffer, 18, Buffer[17]); }
            set
            {
                int realloc = value.Length + 12 + 17;
                if (realloc != Buffer.Length)
                {
                    byte[] new_Packet = new byte[realloc];
                    System.Buffer.BlockCopy(Buffer, 0, new_Packet, 0, 28);
                    Buffer = new_Packet;
                }
                WriteUInt16((ushort)(value.Length + 21), 0, Buffer);
                WriteStringWithLength(value, 17, Buffer);
            }
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