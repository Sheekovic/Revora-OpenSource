// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using Revora.Interfaces;

namespace Revora.Network.GamePackets
{
    public unsafe class ClanAnnouncement : Writer, IPacket
    {
        private Byte[] mData;
        public ClanAnnouncement(Clan clan)
        {
            if (clan.Announcement == null) clan.Announcement = "";
            mData = new Byte[85 + clan.Announcement.Length + 8];
            WriteUInt16((UInt16)(mData.Length - 8), 0, mData);
            WriteUInt16(1312, 2, mData);
            WriteByte((Byte)24, 4, mData);
            WriteUInt16((UInt16)clan.ID, 8, mData);
            WriteByte((Byte)1, 16, mData);
            WriteStringWithLength(clan.Announcement, 17, mData);
        }
        public byte[] ToArray()
        {
            return mData;
        }
        public void Deserialize(byte[] buffer)
        {
            mData = buffer;
        }
        public void Send(Client.GameState client)
        {
            client.Send(mData);
        }
    }
}