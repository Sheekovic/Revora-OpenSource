// * Programador C++ Chavarro
// * Copyright © 2014 - 2016
// * Slayer [ Private Conquer Server ] - Revora

using System;
using Revora.Network;

namespace Revora.Network.GamePackets
{
    public unsafe class JiangHuUpdate : Writer
    {
        private byte[] packet = new byte[28];
        public JiangHuUpdate()
        {
            packet = new byte[28];
            WriteUInt16(20, 0, packet);
            WriteUInt16(2702, 2, packet);
        }
        public uint FreeCourse
        {
            get { return BitConverter.ToUInt32(packet, 4); }
            set { WriteUInt32(value, 4, packet); }
        }
        public byte Star
        {
            get { return packet[10]; }
            set { WriteByte(value, 10, packet); }
        }
        public byte Stage
        {
            get { return packet[11]; }
            set { WriteByte(value, 11, packet); }
        }
        public ushort Atribute
        {
            get { return BitConverter.ToUInt16(packet, 13); }
            set { WriteUInt16(value, 13, packet); }
        }
        public byte FreeTimeTodeyUsed
        {
            get { return packet[15]; }
            set { WriteByte(value, 15, packet); }
        }
        public uint RoundBuyPoints
        {
            get { return BitConverter.ToUInt32(packet, 16); }
            set { WriteUInt32(value, 16, packet); }
        }
        public byte[] ToArray()
        {
            return packet;
        }
    }
}