// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using Revora.Client;

namespace Revora.Network.GamePackets
{
    public unsafe class ChampionStatistic : Writer, Interfaces.IPacket
    {
        public string Name;
        public uint UID, Model;
        public byte Class, Level;
        public int BattlePower
        {
            get
            {
                if (Kernel.GamePool.ContainsKey(UID))
                {
                    return Kernel.GamePool[UID].Entity.BattlePower;
                }
                return 0;
            }
        }
        public GameState Opponent;
        public bool SignedUp;
        public byte Status = 0;

        byte[] Buffer;
        public bool AcceptBox;
        public bool GivenUp;
        public uint TodayPoints;
        public Time32 AcceptBoxShow;
        public uint YesterdayRank;
        public uint YesterdayPoints;
        public DateTime LastReset;
        public Time32 SignUpTime;
        // Server-only event ownership; not part of the wire layout or saved signup flag.
        internal DateTime EventDay;
        internal int OfferedRound;
        internal bool Withdrawn;

        public ChampionStatistic(bool Create)
        {
            Buffer = new byte[24];
            WriteUInt16(16, 0, Buffer);
            WriteUInt16(2601, 2, Buffer);
            Grade = 1;
        }
        public byte Grade
        {
            get { return Buffer[4]; }
            set { Buffer[4] = value; }
        }
        public byte Rank
        {
            get { return Buffer[5]; }
            set { Buffer[5] = value; }
        }
        public byte TotalMatches
        {
            get { return Buffer[6]; }
            set { Buffer[6] = value; }
        }
        public byte WinStreak
        {
            get { return Buffer[7]; }
            set { Buffer[7] = value; }
        }
        public uint Points
        {
            get { return BitConverter.ToUInt32(Buffer, 12); }
            set { WriteUInt32(value, 12, Buffer); }
        }
        public uint TotalPoints
        {
            get { return BitConverter.ToUInt32(Buffer, 8); }
            set { WriteUInt32(value, 8, Buffer); }
        }
        public uint IsGrade
        {
            get
            {
                if (Points > 24400) return 7;
                if (Points > 9400) return 6;
                if (Points > 3400) return 5;
                if (Points > 1400) return 4;
                if (Points > 500) return 3;
                if (Points > 200) return 2;
                return 1;
            }
        }
        public int YesterdayGrade
        {
            get
            {
                if (YesterdayPoints > 24400) return 7;
                if (YesterdayPoints > 9400) return 6;
                if (YesterdayPoints > 3400) return 5;
                if (YesterdayPoints > 1400) return 4;
                if (YesterdayPoints > 500) return 3;
                if (YesterdayPoints > 200) return 2;
                return 1;
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
            client.Send(ToArray());
        }
    }
}
