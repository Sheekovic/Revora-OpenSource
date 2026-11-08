// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Linq;
using System.Text;
using Revora.Network;
using Revora.Game.Features;
using System.Collections.Generic;

namespace Revora.Network.GamePackets
{
    public unsafe class SendFlower : Writer
    {
        public const uint FlowerSender = 2;
        public const uint Flower = 3;
        private byte[] Packet;
        public SendFlower()
        {
            Packet = new byte[68];
            Writer.WriteUInt16(60, 0, Packet);
            Writer.WriteUInt16(1150, 2, Packet);
        }
        public void Apprend(Flowers flowers)
        {
            Writer.WriteUInt32(flowers.RedRoses, 16, Packet);
            Writer.WriteUInt32(flowers.RedRoses2day, 20, Packet);
            Writer.WriteUInt32(flowers.Lilies, 24, Packet);
            Writer.WriteUInt32(flowers.Lilies2day, 28, Packet);
            Writer.WriteUInt32(flowers.Orchads, 32, Packet);
            Writer.WriteUInt32(flowers.Orchads2day, 36, Packet);
            Writer.WriteUInt32(flowers.Tulips, 40, Packet);
            Writer.WriteUInt32(flowers.Tulips2day, 44, Packet);
        }
        public uint Typing
        {
            get { return BitConverter.ToUInt32(Packet, 4); }
            set { Writer.WriteUInt32(value, 4, Packet); }
        }
        public string SenderName
        {
            get { return Encoding.ASCII.GetString(Packet, 16, 16); }
            set { Writer.WriteString(value, 16, Packet); }
        }
        public string ReceiverName
        {
            get { return Encoding.ASCII.GetString(Packet, 32, 16); }
            set { Writer.WriteString(value, 32, Packet); }
        }
        public uint Amount
        {
            get { return BitConverter.ToUInt32(Packet, 48); }
            set { Writer.WriteUInt32(value, 48, Packet); }
        }
        public uint FType
        {
            get { return BitConverter.ToUInt32(Packet, 52); }
            set { Writer.WriteUInt32(value, 52, Packet); }
        }
        public uint Effect
        {
            get { return BitConverter.ToUInt32(Packet, 56); }
            set { Writer.WriteUInt32(value, 56, Packet); }
        }
        public byte[] ToArray()
        {
            return Packet;
        }
    }
}