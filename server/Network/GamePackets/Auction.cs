// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using Revora.Client;
using Revora.Interfaces;

namespace Revora.Network.GamePackets
{
    public unsafe class Auction : Writer, IPacket
    {
        public enum Mode : byte
        {
            NewListing,
            ListingCancel,
            ListingBid,
            ListingBuyout
        }
        Byte[] Packet;
        public ConquerItem item;
        public Auction(Boolean Create)
        {
            if (Create)
            {
                Packet = new Byte[38 + 8];
                WriteByte(38, 0, Packet);
                WriteUInt16(1320, 2, Packet);
                WriteInt32(Time32.Now.AllMilliseconds(), 4, Packet);
            }
        }
        public Mode Action
        {
            get { return (Mode)Packet[8]; }
            set { WriteByte((Byte)value, 8, Packet); }
        }
        public UInt32 ItemUID
        {
            get { return BitConverter.ToUInt32(Packet, 12); }
            set { WriteUInt32(value, 12, Packet); }
        }
        public Byte MoneyType
        {
            get { return Packet[16]; }
            set { WriteByte(value, 16, Packet); }
        }
        public UInt32 BasePrice
        {
            get { return BitConverter.ToUInt32(Packet, 20); }
            set { WriteUInt32(value, 20, Packet); }
        }
        public UInt32 FixedPrice
        {
            get { return BitConverter.ToUInt32(Packet, 24); }
            set { WriteUInt32(value, 24, Packet); }
        }
        public Byte Time
        {
            get { return Packet[28]; }
            set { WriteByte(value, 28, Packet); }
        }
        public Int32 Margin
        {
            get { return Time == 12 ? 500 : Time == 24 ? 1100 : Time >= 48 ? 2250 : 1000000; }
        }
        public Auction Confirm(Boolean Value)
        {
            WriteBoolean(Value, 32, Packet);
            return this;
        }
        public DateTime EndTime;
        public Int32 Times = 0;
        public UInt32 SellerUID = 0;
        public UInt32 BidderUID = 0;
        public String BidderName = "";
        public String SellerName = "";
        public Byte[] ToArray()
        {
            return Packet;
        }
        public void Send(GameState client)
        {
            client.Send(Packet);
        }
        public void Deserialize(Byte[] packet)
        {
            Packet = packet;
        }
        public static void ProcesoInglesCompletoSubasta(byte[] packet, GameState client)
        { Revora.Game.Features.AuctionGateway.Process(packet, client); }
        public static void ProcessAuctionInfo(byte[] packet, GameState client)
        { Revora.Game.Features.AuctionGateway.Query(packet, client, true); }
        public static void ProcessAuctionQuery(byte[] packet, GameState client)
        { Revora.Game.Features.AuctionGateway.Query(packet, client, false); }
        private static void WriteName(string value, int offset, byte[] target)
        {
            int charsUsed, bytesUsed; bool completed;
            var chars = (value ?? string.Empty).ToCharArray();
            Server.Encoding.GetEncoder().Convert(chars, 0, chars.Length, target, offset, 16, true, out charsUsed, out bytesUsed, out completed);
        }
        public static byte[] AuctionInfo(ConquerItem item, uint type, Client.GameState client)
        {
            var Buffer = new byte[8 + 20 + 68];
            Writer.WriteUInt16((ushort)(Buffer.Length - 8), 0, Buffer);
            Writer.WriteUInt16(1322, 2, Buffer);
            Writer.WriteInt32(Time32.timeGetTime().GetHashCode(), 4, Buffer);
            Writer.WriteUInt32(type, 8, Buffer);
            Writer.WriteUInt32(1, 12, Buffer);
            int offset = 20;
            Writer.WriteUInt32(item.UID, offset, Buffer);
            item.SendExtras(client);
            offset += 8;
            Writer.WriteUInt32(item.AuctionInfo.SellerUID, offset, Buffer);
            offset += 4;
            WriteName(item.AuctionInfo.SellerName, offset, Buffer);
            offset += 16;
            Writer.WriteUInt32(item.AuctionInfo.MoneyType, offset, Buffer);
            offset += 4;
            Writer.WriteUInt32(item.AuctionInfo.BasePrice, offset, Buffer);
            offset += 4;
            Writer.WriteUInt32(item.AuctionInfo.FixedPrice, offset, Buffer);
            offset += 4;
            Writer.WriteUInt32(0, offset, Buffer);
            offset += 4;
            Writer.WriteUInt32(item.AuctionInfo.Time, offset, Buffer);
            offset += 4;
            Writer.WriteUInt32(item.AuctionInfo.BidderUID, offset, Buffer);
            offset += 4;
            WriteName(item.AuctionInfo.BidderName, offset, Buffer);
            offset += 16;
            return Buffer;
        }
        public static byte[] AuctionQuery(ConquerItem item, uint type, Client.GameState client)
        {
            var Buffer = new byte[60 + 68];
            Writer.WriteUInt16((ushort)(Buffer.Length - 8), 0, Buffer);
            Writer.WriteUInt16(1321, 2, Buffer);
            Writer.WriteUInt32((uint)Time32.timeGetTime().GetHashCode(), 4, Buffer);
            Writer.WriteUInt32(type, 8, Buffer);
            Writer.WriteUInt16(1, 46, Buffer);
            Writer.WriteUInt16(1, 48, Buffer);
            int offset = 52;
            Writer.WriteUInt32(item.UID, offset, Buffer);
            item.SendExtras(client);
            offset += 8;
            Writer.WriteUInt32(item.AuctionInfo.SellerUID, offset, Buffer);
            offset += 4;
            WriteName(item.AuctionInfo.SellerName, offset, Buffer);
            offset += 16;
            Writer.WriteUInt32(item.AuctionInfo.MoneyType, offset, Buffer);
            offset += 4;
            Writer.WriteUInt32(item.AuctionInfo.BasePrice, offset, Buffer);
            offset += 4;
            Writer.WriteUInt32(item.AuctionInfo.FixedPrice, offset, Buffer);
            offset += 4;
            Writer.WriteUInt32(0, offset, Buffer);
            offset += 4;
            Writer.WriteUInt32(item.AuctionInfo.Time, offset, Buffer);
            offset += 4;
            Writer.WriteUInt32(item.AuctionInfo.BidderUID, offset, Buffer);
            offset += 4;
            WriteName(item.AuctionInfo.BidderName, offset, Buffer);
            offset += 16;
            return Buffer;
        }
    }
}