using System.Collections.Generic;
using System.Linq;
using System.Text;
using Revora.Game;
using Revora.Client;

namespace Revora.Network.GamePackets
{
    public unsafe class ElitePKBrackets : Writer, Interfaces.IPacket
    {
        public const byte
            InitialList = 0,
            StaticUpdate = 1,
            GUIEdit = 2,
            UpdateList = 3,
            RequestInformation = 4,
            StopWagers = 5,
            EPK_State = 6;

        private byte[] Buffer;

        public ElitePKBrackets(bool create, int matches = 0)
        {
            if (create)
            {
                Buffer = new byte[136 + 112 * matches + 8];
                WriteUInt16((ushort)(Buffer.Length - 8), 0, Buffer);
                WriteUInt16(2219, 2, Buffer);
            }
        }

        public byte Type
        {
            get { return Buffer[4]; }
            set { Buffer[4] = value; }
        }

        public byte Page
        {
            get { return Buffer[6]; }
            set { Buffer[6] = value; }
        }

        public byte Unknown
        {
            get { return Buffer[8]; }
            set { Buffer[8] = value; }
        }

        public uint MatchCount
        {
            get { return BitConverter.ToUInt32(Buffer, 10); }
            set { WriteUInt32(value, 10, Buffer); }
        }

        public ushort Group
        {
            get { return BitConverter.ToUInt16(Buffer, 14); }
            set { WriteUInt16(value, 14, Buffer); }
        }

        public ushort GUIType
        {
            get { return BitConverter.ToUInt16(Buffer, 16); }
            set { WriteUInt16(value, 16, Buffer); }
        }

        public ushort TimeLeft
        {
            get { return BitConverter.ToUInt16(Buffer, 18); }
            set { WriteUInt16(value, 18, Buffer); }
        }

        public uint TotalMatches
        {
            get { return BitConverter.ToUInt16(Buffer, 20); }
            set { WriteUInt32(value, 20, Buffer); }
        }

        public bool OnGoing
        {
            get { return Buffer[20] == 1; }
            set { WriteBoolean(value, 20, Buffer); }
        }

        private int offset = 24;

        public void Append(ElitePK.Match match)
        {
            if (Type != GUIEdit)
            {
                WriteUInt32(match.ID, offset, Buffer);
                if (match.Flag == ElitePK.Match.StatusFlag.FinalMatch)
                    match.Flag = ElitePK.Match.StatusFlag.AcceptingWagers;
            }
            else if (GUIType >= ElitePK.States.GUI_Top4Qualifier)
                match.Flag = ElitePK.Match.StatusFlag.FinalMatch;

            if (GUIType >= ElitePK.States.GUI_Top4Qualifier)
                if (match.Flag == ElitePK.Match.StatusFlag.OK)
                    match.Flag = ElitePK.Match.StatusFlag.Watchable;

            offset += 4;
            WriteUInt16((ushort)match.Players.Length, offset, Buffer);
            offset += 2;
            WriteUInt16((ushort)match.Index, offset, Buffer);
            offset += 2;
            if (match.Players.Length == 1)
                WriteUInt16((ushort)ElitePK.Match.StatusFlag.OK, offset, Buffer);
            else
                WriteUInt16((ushort)match.Flag, offset, Buffer);
            offset += 2;
            for (int i = 0; i < match.Players.Length; i++)
            {
                AppendPlayer(match.MatchStats[i], offset);
                offset += 34;
            }

            if (match.Players.Length == 2) offset += 34;
            if (match.Players.Length == 1) offset += 68;

        }

        private void AppendPlayer(ElitePK.FighterStats stats, int _offset)
        {
            WriteUInt32(stats.UID, _offset, Buffer);
            _offset += 4;
            WriteUInt32(stats.Mesh, _offset, Buffer);
            _offset += 4;
            WriteString(stats.Name, _offset, Buffer);
            _offset += 16;
            WriteInt32(0, _offset, Buffer);
            _offset += 4;
            WriteInt32((int)stats.Flag, _offset, Buffer);
            _offset += 4;
            WriteBoolean(stats.Advance, _offset, Buffer);
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