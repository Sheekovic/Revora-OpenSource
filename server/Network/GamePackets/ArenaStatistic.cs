// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Linq;
using System.Text;
using Revora.Client;
using System.Collections.Generic;

namespace Revora.Network.GamePackets
{
    public unsafe class QualifierStatistic : Writer, Interfaces.IPacket
    {
        public const uint
        NotSignedUp = 0,
        WaitingForOpponent = 1,
        WaitingInactive = 2;

        byte[] Buffer;
        public uint UID;
        public int BattlePower
        {
            get
            {
                if (Kernel.GamePool.ContainsKey(UID))
                    return Kernel.GamePool[UID].Entity.BattlePower;
                return 0;
            }
        }
        public QualifierStatistic(bool Create)
        {
            Buffer = new byte[60];
            WriteUInt16(52, 0, Buffer);
            WriteUInt16(2209, 2, Buffer);
        }
        public uint Rank
        {
            get { return BitConverter.ToUInt32(Buffer, 4); }
            set { WriteUInt32(value, 4, Buffer); }
        }
        public uint Status
        {
            get { return BitConverter.ToUInt32(Buffer, 12); }
            set { WriteUInt32(value, 12, Buffer); }
        }
        public uint TotalWin
        {
            get { return BitConverter.ToUInt32(Buffer, 16); }
            set { WriteUInt32(value, 16, Buffer); }
        }
        public uint TotalLose
        {
            get { return BitConverter.ToUInt32(Buffer, 20); }
            set { WriteUInt32(value, 20, Buffer); }
        }
        public uint TodayWin
        {
            get { return BitConverter.ToUInt32(Buffer, 24); }
            set { WriteUInt32(value, 24, Buffer); }
        }
        public uint TodayBattles
        {
            get { return BitConverter.ToUInt32(Buffer, 28); }
            set { WriteUInt32(value, 28, Buffer); }
        }
        public uint HistoryHonor
        {
            get { return BitConverter.ToUInt32(Buffer, 32); }
            set { WriteUInt32(value, 32, Buffer); }
        }
        public uint CurrentHonor
        {
            get { return BitConverter.ToUInt32(Buffer, 36); }
            set { WriteUInt32(value, 36, Buffer); }
        }
        public uint ArenaPoints
        {
            get { return BitConverter.ToUInt32(Buffer, 40); }
            set { WriteUInt32(value, 40, Buffer); }
        }
        public void Send(GameState client)
        {
            client.Send(ToArray());
        }
        public byte[] ToArray()
        {
            return Buffer;
        }
        public void Deserialize(byte[] buffer)
        {
            Buffer = buffer;
        }
        public byte Level
        {
            get;
            set;
        }
        public byte Class
        {
            get;
            set;
        }
        public uint Model
        {
            get;
            set;
        }
        public uint EntityID
        {
            get;
            set;
        }
        public uint LastSeasonRank
        {
            get;
            set;
        }
        public string Name
        {
            get;
            set;
        }
        public DateTime LastArenaPointFill
        {
            get;
            set;
        }
        public Time32 AcceptBoxShow
        {
            get;
            set;
        }
        public bool AcceptBox
        {
            get;
            set;
        }
        public GameState PlayWith
        {
            get;
            set;
        }
        public uint LastSeasonArenaPoints
        {
            get;
            set;
        }
        public uint LastSeasonWin
        {
            get;
            set;
        }
        public uint LastSeasonLose
        {
            get;
            set;
        }
        public uint YesterdayTotal
        {
            get
            {
                return LastSeasonWin + LastSeasonLose;
            }
        }
    }
}