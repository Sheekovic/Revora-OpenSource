// * Programador C++ Chavarro
// * Copyright © 2014 - 2016
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Linq;
using System.Collections.Generic;

namespace Revora.Network.GamePackets
{
    public unsafe class JiangHu : Writer
    {
        public const byte
        IconBar = 0,
        InfoStauts = 7,
        UpdateTalent = 5,
        UpdateStar = 11,
        OpenStage = 12,
        UpdateTime = 13,
        SetName = 14;

        public byte Mode = 0;
        private byte[] packet;
        public List<string> Texts;
        public JiangHu()
        {
            Texts = new List<string>();
        }
        public void Clear()
        {
            Texts.Clear();
        }
        public void CreateArray()
        {
            packet = new byte[Leng(Texts.ToArray()) + 7 + 8];
            WriteUInt16((ushort)(packet.Length - 8), 0, packet);
            WriteUInt16(2700, 2, packet);
            WriteByte(Mode, 4, packet);
            WriteByte((byte)Texts.Count, 5, packet);
            ushort position = 6;
            for (ushort x = 0; x < Texts.Count; x++)
            {
                string text = Texts[x];
                WriteByte((byte)text.Length, position, packet);
                WriteString(text, (ushort)(position + 1), packet);
                position += (ushort)(text.Length + 1);
            }
        }
        private uint Leng(string[] dat)
        {
            uint len = 0;
            foreach (string line in dat)
                len += (byte)line.Length;
            return (uint)(len + dat.Length);
        }
        public void Send(Client.GameState client)
        {
            if (packet != null)
            {
                client.Send(packet.ToArray());
            }
        }
    }
}