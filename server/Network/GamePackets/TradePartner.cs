// * Authorized for personal/commerical use
// * Open Source
// * 
// *[ Private Conquer Server ] - Revora

using System;

namespace Revora.Network.GamePackets
{
    public unsafe class TradePartner : Writer, Interfaces.IPacket
    {
        
        public const byte
        RequestPartnership = 0,
        RejectRequest = 1,
        BreakPartnership = 4,
        AddPartner = 5;

        byte[] Buffer;
        public TradePartner(bool Create)
        {
            if (Create)
            {
                Buffer = new byte[40];
                WriteUInt16(32, 0, Buffer);
                WriteUInt16(2046, 2, Buffer);
            }
        }
        public uint UID
        {
            get { return BitConverter.ToUInt32(Buffer, 4); }
            set { WriteUInt32(value, 4, Buffer); }
        }
        public byte Type
        {
            get { return Buffer[8]; }
            set { Buffer[8] = value; }
        }
        public bool Online
        {
            get { return Buffer[9] == 1; }
            set { Buffer[9] = value == true ? (byte)1 : (byte)0; }
        }
        public int MinutesLeft
        {
            get { return BitConverter.ToInt32(Buffer, 10); }
            set { WriteInt32(Math.Max(0, value), 10, Buffer); }
        }
        public int HoursLeft
        {
            get { return MinutesLeft / 60; }
            set { MinutesLeft = (int)Math.Min(int.MaxValue, Math.Max(0L, (long)value * 60)); }
        }
        public string Name
        {
            get
            {
                return System.Text.Encoding.Default.GetString(Buffer, 16, 16);
            }
            set
            {
                Array.Clear(Buffer, 16, 16);
                char[] chars = (value ?? "").ToCharArray(); int usedChars, usedBytes; bool complete;
                System.Text.Encoding.Default.GetEncoder().Convert(chars, 0, chars.Length, Buffer, 16, 16, true,
                    out usedChars, out usedBytes, out complete);
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
            client.Send(Buffer);
        }
        public static void Process(byte[] packet, Client.GameState client)
        { Game.Features.TradePartnerships.Process(packet, client); }
    }
}
