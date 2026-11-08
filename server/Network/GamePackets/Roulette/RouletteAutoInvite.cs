// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Linq;
using System.Text;
using Revora.Network;

namespace Revora.Network.GamePackets
{
    public unsafe class MsgRouletteAutoInvite : Interfaces.IPacket
    {
        public byte[] packet;
        public static MsgRouletteAutoInvite Create()
        {
            MsgRouletteAutoInvite packet = new MsgRouletteAutoInvite();
            packet.Lenght = 36;
            packet.ID = PacketID.RouletteInvite;
            return packet;
        }
        public ushort Lenght
        {
            set
            {
                packet = new byte[value + 8];
                Writer.WriteUshort((ushort)(packet.Length - 8), 0, packet);
            }
        }
        public ushort ID
        {
            set { Writer.WriteUshort(value, 2, packet); }
        }
        public uint UID
        {
            get { return BitConverter.ReadUint(packet, 4); }
            set { Writer.WriteUint(value, 4, packet); }
        }
        public uint TableID
        {
            get { return BitConverter.ReadUint(packet, 12); }
            set { Writer.WriteUint(value, 12, packet); }
        }
        public uint MoneyType
        {
            get { return BitConverter.ReadUint(packet, 16); }
            set { Writer.WriteUint((uint)value, 16, packet); }
        }
        public string Name
        {
            set { Writer.WriteString(value, 20, packet); }
        }
        public byte[] ToArray()
        {
            return packet;
        }
        public void Deserialize(byte[] buffer)
        {
            throw new NotImplementedException();
        }
        public void Send(Client.GameState client)
        {
            client.Send(ToArray());
        }
    }
}