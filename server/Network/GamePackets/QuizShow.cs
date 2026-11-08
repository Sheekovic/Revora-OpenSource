// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using Revora.Client;
using Revora.Network;
using Revora.Interfaces;

namespace Revora.Network.GamePackets
{
    public unsafe class QuizShowTypes
    {
        public const ushort
        Open = 1,
        SendQuestion = 2,
        ReceiveToping = 3,
        SendTop = 4,
        History = 5;
    }
    public unsafe class OpenQuiz : Writer, IPacket
    {
        byte[] Buffer;
        public OpenQuiz()
        {
            Buffer = new byte[52];
            Writer.WriteUInt16(44, 0, Buffer);
            Writer.WriteUInt16(2068, 2, Buffer);
        }
        public ushort Type
        {
            get { return BitConverter.ToUInt16(Buffer, 4); }
            set { Writer.WriteUInt16(value, 4, Buffer); }
        }
        public ushort StartInTimeSecouds
        {
            get { return BitConverter.ToUInt16(Buffer, 6); }
            set { WriteUInt16(value, 6, Buffer); }
        }
        public ushort AllQuestions
        {
            get { return BitConverter.ToUInt16(Buffer, 8); }
            set { WriteUInt16(value, 8, Buffer); }
        }
        public ushort FullTimeLimit
        {
            get { return BitConverter.ToUInt16(Buffer, 10); }
            set { WriteUInt16(value, 10, Buffer); }
        }
        public ushort ExpBallFirst
        {
            get { return BitConverter.ToUInt16(Buffer, 12); }
            set { WriteUInt16(value, 12, Buffer); }
        }
        public ushort ExpBall2nd
        {
            get { return BitConverter.ToUInt16(Buffer, 14); }
            set { WriteUInt16(value, 14, Buffer); }
        }
        public ushort ExpBall3rd
        {
            get { return BitConverter.ToUInt16(Buffer, 16); }
            set { WriteUInt16(value, 16, Buffer); }
        }
        public void Send(GameState client)
        {
            client.Send(Buffer);
        }
        public byte[] ToArray()
        {
            return Buffer;
        }
        public void Deserialize(byte[] buffer)
        {
            Buffer = buffer;
        }
        public static void Process(byte[] packet, Client.GameState client)
        {
            // Native answer sequencing is unverified. Keep 2068 gameplay disabled
            // globally; the replay-safe Revora edition uses normal NPC dialogue.
        }
    }

    public unsafe class QuizQuestions : Writer, IPacket
    {
        byte[] Buffer;
        private ushort GetLeng(string[] ques)
        {
            ushort set = 0;
            foreach (string add in ques)
                set += (ushort)add.Length;

            return set;
        }
        public QuizQuestions(string[] Questions)
        {
            Buffer = new byte[52 + 4 + GetLeng(Questions)];
            Writer.WriteUInt16((ushort)(44 + 4 + GetLeng(Questions)), 0, Buffer);
            Writer.WriteUInt16(2068, 2, Buffer);

            Buffer[24] = (byte)Questions.Length;

            int position = 25;
            for (ushort i = 0; i < (ushort)Questions.Length; i++)
            {
                Writer.WriteStringWithLength(Questions[i], position, Buffer);
                position += 1 + Questions[i].Length;
            }
        }
        public ushort Type
        {
            get { return BitConverter.ToUInt16(Buffer, 4); }
            set { Writer.WriteUInt16(value, 4, Buffer); }
        }
        public ushort NoQuestion
        {
            get { return BitConverter.ToUInt16(Buffer, 6); }
            set { Writer.WriteUInt16(value, 6, Buffer); }
        }
        public ushort Right
        {
            get { return BitConverter.ToUInt16(Buffer, 8); }
            set { Writer.WriteUInt16(value, 8, Buffer); }
        }
        public ushort AllQuestions
        {
            get { return BitConverter.ToUInt16(Buffer, 10); }
            set { Writer.WriteUInt16(value, 10, Buffer); }
        }
        public ushort FullTimeLimit
        {
            get { return BitConverter.ToUInt16(Buffer, 12); }
            set { Writer.WriteUInt16(value, 12, Buffer); }
        }
        public ushort MyPoints
        {
            get { return BitConverter.ToUInt16(Buffer, 14); }
            set { Writer.WriteUInt16(value, 14, Buffer); }
        }
        public void Send(GameState client)
        {
            client.Send(Buffer);
        }
        public byte[] ToArray()
        {
            return Buffer;
        }
        public void Deserialize(byte[] buffer)
        {
            Buffer = buffer;
        }
    }
    public unsafe class QuizRank : Writer, IPacket
    {
        byte[] Buffer;
        public QuizRank()
        {
            Buffer = new byte[92];
            Writer.WriteUInt16(84, 0, Buffer);
            Writer.WriteUInt16(2068, 2, Buffer);
        }
        public ushort Type
        {
            get { return BitConverter.ToUInt16(Buffer, 4); }
            set { Writer.WriteUInt16(value, 4, Buffer); }
        }
        public ushort MyPoints
        {
            get { return BitConverter.ToUInt16(Buffer, 6); }
            set { Writer.WriteUInt16(value, 6, Buffer); }
        }
        public ushort MyTime
        {
            get { return BitConverter.ToUInt16(Buffer, 8); }
            set { Writer.WriteUInt16(value, 8, Buffer); }
        }
        public byte MyRank
        {
            get { return Buffer[10]; }
            set { Buffer[10] = value; }
        }
        public byte GiveRight
        {
            get { return Buffer[11]; }
            set { Buffer[11] = value; }
        }
        public byte Count
        {
            get { return Buffer[20]; }
            set { Buffer[20] = value; }
        }
        ushort Position = 24;
        public void Aprend(string name, ushort BestScore, ushort BestTime)
        {
            if ((ushort)(Position + 20) <= (ushort)(Buffer.Length - 8))
            {
                Count++;
                WriteString(name, Position, Buffer);
                Position += 16;
                WriteUInt16(BestScore, Position, Buffer);
                Position += 2;
                WriteUInt16(BestTime, Position, Buffer);
                Position += 2;
            }
        }
        public void Send(GameState client)
        {
            client.Send(Buffer);
        }
        public byte[] ToArray()
        {
            return Buffer;
        }
        public void Deserialize(byte[] buffer)
        {
            Buffer = buffer;
        }
    }
    public unsafe class QuizHistory : Writer, IPacket
    {
        byte[] Buffer;
        public QuizHistory()
        {
            Buffer = new byte[68];
            WriteUInt16((ushort)(68 - 8), 0, Buffer);
            WriteUInt16((ushort)2068, 2, Buffer);
        }
        public ushort Type
        {
            get { return BitConverter.ToUInt16(Buffer, 4); }
            set { WriteUInt16(value, 4, Buffer); }
        }
        public byte MyRank
        {
            get { return Buffer[6]; }
            set { Buffer[6] = value; }
        }
        public ushort ExpBallsReceives
        {
            get { return BitConverter.ToUInt16(Buffer, 8); }
            set { WriteUInt16(value, 8, Buffer); }
        }
        public ushort MyTime
        {
            get { return BitConverter.ToUInt16(Buffer, 10); }
            set { WriteUInt16(value, 10, Buffer); }
        }
        public ushort MyPoints
        {
            get { return BitConverter.ToUInt16(Buffer, 12); }
            set { WriteUInt16(value, 12, Buffer); }
        }
        ushort Position = 24;
        public void Append(string name, ushort BestScore, ushort BestTime)
        {
            if ((ushort)(Position + 20) <= (ushort)(Buffer.Length - 8))
            {
                WriteString(name, Position, Buffer);
                Position += 16;
                WriteUInt16(BestScore, Position, Buffer);
                Position += 2;
                WriteUInt16(BestTime, Position, Buffer);
                Position += 2;
            }
        }
        public void Send(GameState client)
        {
            client.Send(Buffer);
        }
        public byte[] ToArray()
        {
            return Buffer;
        }
        public void Deserialize(byte[] buffer)
        {
            Buffer = buffer;
        }
    }
}