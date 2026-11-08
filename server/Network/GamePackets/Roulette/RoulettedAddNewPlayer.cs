// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using Revora.Network;

namespace Revora.Roulette
{
    public unsafe class MsgRoulettedAddNewPlayer : Interfaces.IPacket
    {
        public byte[] packet;
        public static MsgRoulettedAddNewPlayer Create()
        {
            MsgRoulettedAddNewPlayer ptr = new MsgRoulettedAddNewPlayer();
            ptr.Length = 29;
            ptr.PacketID = Network.GamePackets.PacketID.RoulettedAddNewPlayer;
            return ptr;
        }
        public ushort Length
        {
            set
            {
                packet = new byte[value + 8];
                Writer.WriteUshort((ushort)(packet.Length - 8), 0, packet);
            }
        }
        public ushort PacketID
        {
            set { Writer.WriteUshort(value, 2, packet); }
        }
        public uint UID
        {
            set { Writer.WriteUint(value, 4, packet); }
        }
        public uint Mesh
        {
            set { Writer.WriteUint(value, 8, packet); }
        }
        public MsgRouletteOpenGui.Color Color
        {
            set { Writer.Byte((byte)value, 12, packet); }
        }
        public string Name
        {
            set { Writer.WriteString(value, 13, packet); }
        }
        public byte[] ToArray()
        {
            return packet;
        }
        public void Send(Client.GameState client)
        {
            client.Send(ToArray());
        }
        public void Deserialize(byte[] buffer)
        {
            throw new NotImplementedException();
        }
    }
}