// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using Revora.Client;
using Revora.Game.Features.Tournaments;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Revora.Network.GamePackets
{
    public unsafe class TeamElitePkTop : Writer
    {
        byte[] packet;
        public const byte Top3 = 2, Top8 = 0;
        public TeamElitePkTop(Game.Features.Tournaments.TeamElitePk.GamePackets ID)
        {
            packet = new byte[428];
            WriteUInt16((ushort)(packet.Length - 8), 0, packet);
            if (ID == Game.Features.Tournaments.TeamElitePk.GamePackets.SkillElitePkBrackets)
                WriteUInt16((ushort)Game.Features.Tournaments.TeamElitePk.GamePackets.SkillElitePkTop, 2, packet);
            else
                WriteUInt16((ushort)Game.Features.Tournaments.TeamElitePk.GamePackets.TeamElitePkTop, 2, packet);

        }

        public uint Type
        {
            get { return BitConverter.ToUInt32(packet, 4); }
            set { WriteUInt32(value, 4, packet); }
        }//top 3, top 8
        public uint Group
        {
            get { return BitConverter.ToUInt32(packet, 8); }
            set { WriteUInt32(value, 8, packet); }
        }//8
        public uint GroupStatus
        {
            get { return BitConverter.ToUInt32(packet, 12); }
            set { WriteUInt32(value, 12, packet); }
        }
        public uint Count
        {
            get { return BitConverter.ToUInt32(packet, 16); }
            set { WriteUInt32(value, 16, packet); }
        }
        /*   public uint UID
           {
               get { return ReadUint(20); }
               set { WriteUint(value, 20); }
           }*/
        public uint ClientUID
        {
            get { return BitConverter.ToUInt32(packet, 20); }
            set { WriteUInt32(value, 20, packet); }
        }

        private int Index = 0;
        public void Append(TeamElitePk.FighterStats stats, int rank)
        {
            ushort offset = (ushort)(20 + Index * 44); Index++;
            WriteUInt32(stats.LeaderUID, offset, packet); offset += 4;
            WriteUInt32((uint)rank, offset, packet); offset += 4;
            WriteString(stats.Name, offset, packet); offset += 32;
            WriteUInt32(stats.LeaderMesh, offset, packet); offset += 4;
        }
        public void Send(GameState client)
        {
            client.Send(packet);
        }

        public byte[] ToArray()
        {
            return packet;
        }

        public void Deserialize(byte[] buffer)
        {
            packet = buffer;
        }
    }
}
