using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Revora.Network.GamePackets
{
    public unsafe class GenericRanking : Writer, Interfaces.IPacket
    {
        public const uint
        Ranking = 1,
        QueryCount = 2,
        InformationRequest = 5;
        public const uint
        RoseFairy = 30000002,
        LilyFairy = 30000102,
        OrchidFairy = 30000202,
        TulipFairy = 30000302,
        Kiss = 30000402,
        Love = 30000502,
        Tins = 30000602,
        Jade = 30000702,
        Chi = 60000000,
        DragonChi = 60000001,
        PhoenixChi = 60000002,
        TigerChi = 60000003,
        TurtleChi = 60000004,
        InnerPower = 70000000,
        Prestige = 80000000;
        byte[] Buffer;
        int current;

        public GenericRanking(bool Create, uint entries = 1)
        {
            if (Create)
            {
                Buffer = new byte[744 + 8];
                WriteUInt16((ushort)(Buffer.Length - 8), 0, Buffer);
                WriteUInt16(1151, 2, Buffer);
            }
        }
        internal static GenericRanking FlowerPage(uint entries)
        {
            if (entries > 10) throw new ArgumentOutOfRangeException("entries");
            var packet = new GenericRanking(true);
            packet.Buffer = new byte[Math.Max(752, 24 + (int)entries * 80 + 8)];
            WriteUInt16((ushort)(packet.Buffer.Length - 8), 0, packet.Buffer);
            WriteUInt16(1151, 2, packet.Buffer);
            return packet;
        }
        public uint Mode
        {
            get { return BitConverter.ToUInt32(Buffer, 4); }
            set { WriteUInt32(value, 4, Buffer); }
        }
        public uint RankingType
        {
            get { return BitConverter.ToUInt32(Buffer, 8); }
            set { WriteUInt32(value, 8, Buffer); }
        }
        public ushort RegisteredCount
        {
            get { return BitConverter.ToUInt16(Buffer, 12); }
            set { WriteUInt16(value, 12, Buffer); }
        }
        public ushort Page
        {
            get { return BitConverter.ToUInt16(Buffer, 14); }
            set { WriteUInt16(value, 14, Buffer); }
        }
        public uint Count
        {
            get { return BitConverter.ToUInt32(Buffer, 16); }
            set { WriteUInt32(value, 16, Buffer); }
        }
      /*  public void Append(uint rank, uint amount, uint uid, string name)
        {
            int offset = current * 72 + 24;
            if (offset + 56 <= Buffer.Length)
            {
                current++;
                Count = (uint)current;
                WriteUInt32(rank, offset, Buffer); offset += 8;
                WriteUInt32(amount, offset, Buffer); offset += 8;
                WriteUInt32(uid, offset, Buffer); offset += 4;
                WriteUInt32(uid, offset, Buffer); offset += 4;
                WriteString(name, offset, Buffer); offset += 16;
                WriteString(name, offset, Buffer); offset += 16;
            }
        }*/
        public void Append(uint rank, uint amount, uint uid, string name, byte level = 0, byte Class = 0, uint mesh = 0)
        {
            int offset = current * 80 + 24;
            if (offset + 80  <= Buffer.Length)
            {
                current++;
                Count = (uint)current;
                WriteUInt32(rank, offset, Buffer); offset += 8;
                WriteUInt32(amount, offset, Buffer); offset += 8;
                WriteUInt32(uid, offset, Buffer); offset += 4;
                WriteUInt32(uid, offset, Buffer); offset += 4;
                WriteString(name, offset, Buffer); offset += 16;
                WriteString(name, offset, Buffer); offset += 16;
                WriteUInt32(level, offset, Buffer); offset += 4;
                WriteUInt32(Class, offset, Buffer); offset += 4;
                WriteUlong(mesh, offset, Buffer); offset += 8;
            }
        }
        public void Reset()
        {
            current = 0;
        }
        public void Send(Client.GameState client)
        {
            client.Send(Buffer);
        }
        public byte[] ToArray()
        {
            return Buffer;
        }
        public void Deserialize(byte[] _buffer)
        {
            Buffer = _buffer;
            if (Count == 0)
            {
                byte[] buffer = new byte[112];
                Buffer.CopyTo(buffer, 0);
                WriteUInt16(104, 0, buffer);
                Buffer = buffer;
            }
        }
    }
}
