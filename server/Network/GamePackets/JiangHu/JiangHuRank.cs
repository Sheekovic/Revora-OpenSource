// * Programador C++ Chavarro
// * Copyright © 2014 - 2016
// * Slayer [ Private Conquer Server ] - Revora

using System;

namespace Revora.Network.GamePackets
{
    public unsafe class JiangHuRank : Writer
    {
        private byte[] packet;
        private ushort Position = 8;
        public JiangHuRank(byte entry = 1)
        {
            packet = new byte[16 + entry * 41];
            WriteUInt16((ushort)(packet.Length - 8), 0, packet);
            WriteUInt16(2703, 2, packet);
            WriteByte(entry, 6, packet);
        }
        public void Appren(byte Rank, uint Inner_Strength, uint Level, string Name, string CustomizedName)
        {
            WriteByte(Rank, Position, packet); Position++;
            WriteUInt32(Inner_Strength, Position, packet); Position += 4;
            WriteUInt32(Level, Position, packet); Position += 4;
            WriteString(Name, Position, packet); Position += 16;
            WriteString(CustomizedName, Position, packet); Position += 16;
        }
        public byte Page
        {
            get { return packet[4]; }
            set { WriteByte(value, 4, packet); }
        }
        public byte RegisteredCount
        {
            get { return packet[7]; }
            set { WriteByte(value, 7, packet); }
        }
        public byte MyRank
        {
            get { return packet[5]; }
            set { WriteByte(value, 5, packet); }
        }
        public byte[] ToArray()
        {
            return packet;
        }
    }
}