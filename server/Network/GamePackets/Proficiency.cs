using System;

namespace Revora.Network.GamePackets
{
    public unsafe class Proficiency : Writer, Interfaces.IPacket, Interfaces.IProf
    {
        public byte[] Buffer { get; set; }
        private byte _PreviousLevel;
        private bool _Available;
        public Proficiency(bool Create)
        {
            Buffer = new byte[28];
            WriteUInt16(20, 0, Buffer);
            WriteUInt16(1025, 2, Buffer);
            _Available = false;
        }
        public ushort ID
        {
            get { return (ushort)BitConverter.ToUInt16(Buffer, 4); }
            set { WriteUInt16(value, 4, Buffer); }
        }
        public byte Level
        {
            get { return (byte)BitConverter.ToUInt16(Buffer, 8); }
            set { WriteUInt16(value, 8, Buffer); }
        }
        public byte PreviousLevel
        {
            get { return _PreviousLevel; }
            set { _PreviousLevel = value; }
        }
        public uint Experience
        {
            get { return BitConverter.ToUInt32(Buffer, 12); }
            set { WriteUInt32(value, 12, Buffer); }
        }
        public byte TempLevel
        {
            get { return (byte)BitConverter.ToUInt16(this.Buffer, 16); }
            set { Writer.WriteUInt16(value, 16, this.Buffer); }
        }
        public uint NeededExperience
        {
            get { return BitConverter.ToUInt32(Buffer, 16); }
            set { WriteUInt32(value, 16, Buffer); }
        }
        public bool Available
        {
            get { return _Available; }
            set { _Available = value; }
        }
        public byte[] ToArray()
        {
            return Buffer;
        }
        public void Deserialize(byte[] buffer)
        {
            throw new NotImplementedException();
        }
        public void Send(Client.GameState client)
        {
            client.Send(Buffer);
        }
    }
}