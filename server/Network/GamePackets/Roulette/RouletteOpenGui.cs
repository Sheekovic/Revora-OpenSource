// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Linq;
using System.Text;
using Revora.Network;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Revora.Roulette
{
    public unsafe class MsgRouletteOpenGui : Interfaces.IPacket
    {
        public byte[] packet;
        public static MsgRouletteOpenGui Create()
        {
            MsgRouletteOpenGui packet = new MsgRouletteOpenGui();
            packet.Length = 10;
            packet.PacketID = Network.GamePackets.PacketID.RouletteOpenGui;
            return packet;
        }
        public enum Color : byte
        {
            Blue = 0,
            Yellow = 1,
            Green = 2,
            Mauve = 3,
            Gray = 4,
            None = 5,
            Watch = 99//not sure
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
        public MsgRouletteTable.TableType Type
        {
            set { Writer.WriteUint((uint)value, 4, packet); }
        }
        public Color PlayerColor
        {
            set { Writer.Byte((byte)value, 8, packet); }
        }
        public byte TimerStamp
        {
            set { Writer.Byte((byte)value, 9, packet); }
        }
        public void FinalizePacket(Client.GameState user, Database.Roulettes.RouletteTable.Member[] Members)
        {
            ushort Size = (ushort)(10 + 8 + Members.Length * 25);
            byte[] data = new byte[Size];
            packet.CopyTo(data, 0);
            Writer.WriteUshort((ushort)(Size - 8), 0, data);
            data[10] = (byte)Members.Length;
            for (int x = 0; x < Members.Length; x++)
            {
                var elemenet = Members[x];
                int offset = 11 + (x * 25);
                Writer.WriteUint(elemenet.Owner.Entity.UID, offset, data);
                offset += 4;
                Writer.WriteUint(elemenet.Owner.Entity.Mesh, offset, data);
                offset += 4;
                Writer.WriteByte((byte)elemenet.Color, offset, data);
                offset += 1;
                Writer.WriteString(elemenet.Owner.Entity.Name, offset, data);
            }
            user.Send(data);
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



