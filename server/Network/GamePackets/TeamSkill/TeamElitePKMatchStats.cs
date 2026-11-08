// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using Revora.Client;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Revora.Network.GamePackets
{
    public unsafe class TeamElitePKMatchStats : Writer
    {
        byte[] packet;
        public TeamElitePKMatchStats(Game.Features.Tournaments.TeamElitePk.GamePackets ID)
        {
            packet = new byte[128 + 8];
            WriteUInt16(128, 0, packet);
            if (ID == Game.Features.Tournaments.TeamElitePk.GamePackets.SkillElitePkBrackets)
                WriteUInt16((ushort)Game.Features.Tournaments.TeamElitePk.GamePackets.SkillElitePKMatchStats, 2, packet);
            else
                WriteUInt16((ushort)Game.Features.Tournaments.TeamElitePk.GamePackets.TeamElitePKMatchStats, 2, packet);

        }
        public void Append(Game.Features.Tournaments.TeamElitePk.Match match)
        {
            ushort offset = 4;
            var array = match.MatchStats;
            AppendPlayer(array[0], offset); 
            offset += 60;
            AppendPlayer(array[1], offset);
        }
        public uint val1 = 0;
        public uint val2 = 0;
        public uint val3 = 0;
        public uint val4 = 0;

        private void AppendPlayer(Game.Features.Tournaments.TeamElitePk.FighterStats team, ushort offset)
        {
            WriteUInt32(team.LeaderUID, offset, packet); offset += 4;
            WriteUInt32(team.Team.UID, offset, packet); offset += 4;
            WriteString(team.Name, offset, packet); offset += 32;
            WriteUInt32(val1, offset, packet);
            offset += 4;
            WriteUInt32(val2, offset, packet);
            offset += 4;
            WriteUInt32(val3, offset, packet);
            offset += 4;
            WriteUInt32(val4, offset, packet);
            offset += 4;
            WriteUInt32(team.Points, offset, packet);
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
