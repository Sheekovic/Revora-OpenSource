// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Revora.Client;

namespace Revora.Network.GamePackets
{
    public unsafe class Guild_AdvertiseRecruit : Writer, Revora.Interfaces.IPacket
    {
        byte[] buffer;
        public Guild_AdvertiseRecruit()
        {
            buffer = new byte[292];
            WriteUInt16(284, 0, buffer);
            WriteUInt16(2225, 2, buffer);
        }
        public uint GuildID
        {
            get { return BitConverter.ToUInt32(buffer, 4); }
            set { Writer.WriteUInt32(value, 4, buffer); }
        }
        public ulong AD_rate
        {
            get { return BitConverter.ToUInt64(buffer, 264); }
            set { Writer.WriteUInt64(value, 264, buffer); }
        }
        public uint AutoRecruit
        {
            get { return BitConverter.ToUInt32(buffer, 272); }
            set { Writer.WriteUInt32(value, 272, buffer); }
        }
        public uint ReqLevel
        {
            get { return BitConverter.ToUInt32(buffer, 274); }
            set { Writer.WriteUInt32(value, 274, buffer); }
        }
        public uint ReqReborn
        {
            get { return BitConverter.ToUInt32(buffer, 276); }
            set { Writer.WriteUInt32(value, 276, buffer); }
        }
        public uint ReqClass
        {
            get { return BitConverter.ToUInt32(buffer, 278); }
            set { Writer.WriteUInt32(value, 278, buffer); }
        }
        public uint MaleFemale
        {
            get { return BitConverter.ToUInt32(buffer, 280); }
            set { Writer.WriteUInt32(value, 280, buffer); }
        }
        public byte[] ToArray()
        {
            return buffer;
        }
        public void Send(GameState client)
        {
            client.Send(buffer);
        }
        public void Deserialize(byte[] buffr)
        {
            buffer = buffr;
        }
        public static void Process(byte[] packet, Client.GameState client)
        {
            // No verified publishing/fee transaction exists for this legacy adapter.
            if (Revora.Game.Features.QuestInventory.Player(client,false))
                client.Send("Guild advertisements are not available yet.");
        }
    }
}
