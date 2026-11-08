using Revora;
using Revora.Client;
using Revora.Interfaces;
using Revora.Network;
using Revora.Network.GamePackets;
using System;

public unsafe class MsgAuctionItem : Writer, IPacket
{
    byte[] Packet;
    public MsgAuctionItem(bool Create)
    {
        if (Create)
        {
            Packet = new byte[128];
            WriteUInt16((ushort)(Packet.Length-8), 0, Packet);
            WriteUInt16((ushort)1321, 2, Packet);
            WriteInt32(Time32.Now.AllMilliseconds(), 4, Packet);
        }
    }
    public ushort PageListings
    {
        set { WriteUInt16(value, 46, Packet); }
    }
    public ushort TotalListings
    {
        set { WriteUInt16(value, 48, Packet); }
    }
    public ushort QueryPage
    {
        get { return System.BitConverter.ToUInt16(Packet, 50); }
    }
    public byte[] ToArray()
    {
        return Packet;
    }
    public void Send(GameState client)
    {
        client.Send(Packet);
    }
    public void Deserialize(byte[] Pack)
    {
        Packet = Pack;
    }
    public static byte[] ToArray(ConquerItem item, uint type, GameState client)
    { return Auction.AuctionQuery(item,type,client); }
    public static void Process(byte[] packet, GameState client)
    { Revora.Game.Features.AuctionGateway.Query(packet,client,false); }
}
