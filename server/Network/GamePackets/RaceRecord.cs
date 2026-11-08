// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using Revora.Client;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Revora.Network.GamePackets
{
    public enum RaceRecordTypes : int
    {
        BestTime = 0,
        EndTime = 1,
        AddRecord = 2,
        CloseRecords = 3,
    }
    public unsafe class RaceRecord : Writer, Interfaces.IPacket
    {
        private byte[] buffer;
        public RaceRecord()
        {
            buffer = new byte[44];
            WriteUInt16(36, 0, buffer);
            WriteUInt16(1071, 2, buffer);
        }

        public RaceRecordTypes Type
        {
            get { return (RaceRecordTypes)BitConverter.ToInt32(buffer, 4); }
            set { WriteInt32((int)value, 4, buffer); }
        }

        public int Rank
        {
            get { return BitConverter.ToInt32(buffer, 8); }
            set { WriteInt32(value, 8, buffer); }
        }

        public string Name
        {
            get { return Encoding.Default.GetString(buffer, 12, 16).Replace("\0", ""); }
            set { WriteString(value, 12, buffer); }
        }

        //For type EndTime
        public int dwParam
        {
            get { return BitConverter.ToInt32(buffer, 12); }
            set { WriteInt32(value, 12, buffer); }
        }
        
        //For type EndTime
        public int dwParam2
        {
            get { return BitConverter.ToInt32(buffer, 16); }
            set { WriteInt32(value, 16, buffer); }
        }

        public int Time
        {
            get { return BitConverter.ToInt32(buffer, 28); }
            set { WriteInt32(value, 28, buffer); }
        }

        public int Prize
        {
            get { return BitConverter.ToInt32(buffer, 32); }
            set { WriteInt32(value, 32, buffer); }
        }

        public void Deserialize(byte[] buffer)
        {
            throw new NotImplementedException();
        }

        public void Send(GameState client)
        {
            client.Send(buffer);
        }

        public byte[] ToArray()
        {
            return buffer;
        }
    }
}
