// * Programador C++ Chavarro
// * Copyright © 2014 - 2016
// * Slayer [ Private Conquer Server ] - Revora

using System;
using Revora.Game;
using Revora.Network;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Revora.Network.GamePackets
{
    public class JiangHuStatus : Writer
    {
        byte[] packet;
        public JiangHuStatus(uint count = 1)
        {
            packet = new byte[56 + count * 18];
            WriteUInt16((ushort)(48 + count * 18), 0, packet);
            WriteUInt16((ushort)(2701), 2, packet);
            WriteUInt32(9999999, 39, packet);
        }
        public string Name
        {
            set { WriteString(value, 4, packet); }
        }
        public byte Stage
        {
            get { return packet[20]; }
            set { WriteByte(value, 20, packet); }
        }
        public byte Talent
        {
            get { return packet[21]; }
            set { WriteByte(value, 21, packet); }
        }
        public uint Timer
        {
            get { return BitConverter.ToUInt32(packet, 22); }
            set { WriteUInt32(value, 22, packet); }
        }
        public ulong StudyPoints
        {
            get { return BitConverter.ToUInt64(packet, 27); }
            set { WriteUInt64(value, 27, packet); }
        }
        public uint FreeTimeTodey
        {
            get { return BitConverter.ToUInt32(packet, 35); }
            set { WriteUInt32(value, 35, packet); }
        }
        public byte FreeTimeTodeyUsed
        {
            get { return packet[43]; }
            set { WriteByte(value, 43, packet); }
        }
        public uint RoundBuyPoints
        {
            get { return BitConverter.ToUInt32(packet, 44); }
            set { WriteUInt32(value, 44, packet); }
        }
        private ushort Position = 48;
        public void Apprend(ICollection<Game.JiangHu.JiangStages> val)
        {
            foreach (var obj in val)
            {
                if (!obj.Activate) break;
                for (byte x = 0; x < obj.Stars.Length; x++)
                {
                    if (packet.Length < Position + 2) break;
                    var star = obj.Stars[x];
                    if (!star.Activate) break;
                    WriteUInt16(star.UID, Position, packet);
                    Position += 2;
                }
            }
            FreeTimeTodeyUsed = 0;
        }
        public byte[] ToArray()
        {
            return packet;
        }
    }
}