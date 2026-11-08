// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Text;
using Revora.Interfaces;
using ProtoBuf;

namespace Revora.Network.GamePackets
{
    [ProtoContract]
    public class CMsgNpc
    {
        [ProtoMember(1, IsRequired = true)]
        public ulong TimeStamp;
        [ProtoMember(2, IsRequired = true)]
        public ulong UID;
        [ProtoMember(3, IsRequired = true)]
        public ulong NpcEffect;
        [ProtoMember(4, IsRequired = true)]
        public ulong OptionID;
        [ProtoMember(5, IsRequired = true)]
        public ulong interAct;

    }
    public unsafe class NpcRequest : Writer, Interfaces.IPacket
    {
        private byte[] Buffer;
        public NpcRequest(bool Create)
        {
            if (Create)
            {
                Buffer = new byte[28];
                Writer.WriteUInt16(20, 0, Buffer);
                Writer.WriteUInt16(2032, 2, Buffer);
                Writer.WriteUInt32((uint)Time32.timeGetTime().GetHashCode(), 4, Buffer);
            }
        }
        public uint NpcID
        {
            get { return BitConverter.ToUInt32(Buffer, 8); }
            set { WriteUInt32(value, 8, Buffer); }
        }
        public byte OptionID
        {
            get { return Buffer[14]; }
            set { Buffer[14] = value; }
        }
        public byte InteractType
        {
            get { return Buffer[15]; }
        }
        // 255 closes a dialogue, but confirms a message box; journal requests
        // use interaction 102 and are not dialogue choices.
        internal bool IsDialogueClose { get { return OptionID == 255 && InteractType != NpcReply.MessageBox && InteractType != 102; } }
        public string Input
        {
            get { return Encoding.Default.GetString(Buffer, 18, Buffer[17]); }
        }
        public void Deserialize(byte[] buffer)
        {
            Buffer = buffer;
        }
        public byte[] ToArray()
        {
            throw new NotImplementedException();
        }
        public void Send(Client.GameState client)
        {
            client.Send(Buffer);
        }
    }
}