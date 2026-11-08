// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using Revora.Interfaces;

namespace Revora.Network.GamePackets
{
    public unsafe class GameSubClass : Writer, IPacket
    {
        private Byte[] mData;
        public enum Types : ushort
        {
            Switch = 0,
            Activate = 1,
            Upgrade = 2,
            ConfirmUpgrade = 3,
            Learn = 4,
            MartialPromoted = 5,
            Show = 6,
            Info = 7,
            SetStudyPoint = 8,
            RequestJoin = 9,
            RequestPromote = 10
        }
        public enum ID : byte
        {
            None = 0,
            MartialArtist = 1,
            Warlock = 2,
            ChiMaster = 3,
            Sage = 4,
            Apothecary = 5,
            Performer = 6,
            Wrangler = 9
        }
        public GameSubClass()
        {
            this.mData = new Byte[40 + 8];
            WriteUInt16((UInt16)(mData.Length - 8), 0, mData);
            WriteUInt16((UInt16)2320, 2, mData);
        }
        public Types Type
        {
            get { return (Types)BitConverter.ToUInt16(mData, 4); }
            set { WriteUInt16((Byte)value, 4, mData); }
        }
        public ID ClassId
        {
            get { return (ID)mData[6]; }
            set { mData[6] = (Byte)value; }
        }
        public Byte Phase
        {
            get { return mData[7]; }
            set { mData[7] = value; }
        }
        public byte[] ToArray()
        {
            return mData;
        }
        public void Deserialize(byte[] buffer)
        {
            this.mData = buffer;
        }
        public void Send(Client.GameState client)
        {
            client.Send(mData);
        }
    }
    public unsafe class SendSubClass : Writer, IPacket
    {
        byte Type;
        byte[] Buffer;
        Game.Entity Owner = null;
        public SendSubClass(Game.Entity entity)
        {
            Owner = entity;
            Type = 7;
        }
        public byte[] ToArray()
        {
            Buffer = new byte[8 + 30 + (Owner.SubClasses.Classes.Count * 3)];
            WriteUInt16((ushort)(Buffer.Length - 8), 0, Buffer);
            WriteUInt16(2320, 2, Buffer);
            Writer.WriteUInt32((uint)Time32.timeGetTime().GetHashCode(), 4, Buffer);
            WriteUInt16((ushort)Type, 8, Buffer);
            WriteUInt16(Owner.SubClasses.StudyPoints, 10, Buffer);
            WriteUInt16((ushort)Owner.SubClasses.Classes.Count, 26, Buffer);
            int Position = 30;
            if (Owner.SubClasses.Classes.Count > 0)
            {
                Game.SubClass[] Classes = new Game.SubClass[Owner.SubClasses.Classes.Count];
                Owner.SubClasses.Classes.Values.CopyTo(Classes, 0);
                foreach (Game.SubClass Class in Classes)
                {
                    WriteByte(Class.ID, Position, Buffer); Position++;
                    WriteByte(Class.Phase, Position, Buffer); Position++;
                    WriteByte(Class.Level, Position, Buffer); Position++;
                }
            }
            Owner.Owner.LoadItemStats();
            WriteString("TQServer", (Buffer.Length - 8), Buffer);
            return Buffer;
        }
        public void Deserialize(byte[] buffer)
        {
            this.Buffer = buffer;
        }
        public void Send(Client.GameState client)
        {
            client.Send(Buffer);
        }
        public static void Process(byte[] packet, Client.GameState client)
        {
            Revora.Game.Features.SubclassProgression.Process(packet, client);
        }
    }

    public unsafe class SubClassShowFull : Writer, IPacket
    {
        byte[] Buffer;
        public SubClassShowFull(ushort entry = 0)
        {
            Buffer = new byte[(38 + entry * 3)];
            WriteUInt16((ushort)(Buffer.Length - 8), 0, Buffer);
            WriteUInt16(2320, 2, Buffer);
            WriteUInt32((uint)Time32.timeGetTime().GetHashCode(), 4, Buffer);
            if (entry != 0)
            {
                this.Count = entry;
            }
        }
        public ushort ID
        {
            get { return BitConverter.ToUInt16(Buffer, 8); }
            set { WriteUInt16(value, 8, Buffer); }
        }
        public byte Class
        {
            get { return Buffer[10]; }
            set { Buffer[10] = value; }
        }
        public ushort Study
        {
            get { return BitConverter.ToUInt16(Buffer, 10); }
            set { WriteUInt16(value, 10, Buffer); }
        }
        public byte Level
        {
            get { return Buffer[11]; }
            set { Buffer[11] = value; }
        }
        public ushort StudyReceive
        {
            get { return BitConverter.ToUInt16(Buffer, 18); }
            set { WriteUInt16(value, 18, Buffer); }
        }
        public ushort Count
        {
            get { return BitConverter.ToUInt16(Buffer, 26); }
            set { WriteUInt16(value, 26, Buffer); }
        }
        public byte[] ToArray()
        {
            return Buffer;
        }
        public void Deserialize(byte[] buffer)
        {
            this.Buffer = buffer;
        }
        public void Send(Client.GameState client)
        {
            client.Send(Buffer);
        }
    }
}