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
    public unsafe class TeamElitePkBrackets
    {
        public const byte
        InitialList = 0,
        StaticUpdate = 1,
        GUIEdit = 2,
        UpdateList = 3,
        RequestInformation = 4,
        EPK_State = 5;

        byte[] packet;
        public TeamElitePkBrackets(Game.Features.Tournaments.TeamElitePk.GamePackets ID, int extraLength = 0)
        {
            if (extraLength > 5)
                return;
            packet = new byte[(int)((ushort)(190 + extraLength * 154))];
            Writer.WriteUInt16((ushort)(packet.Length - 8), 0, packet);
            Writer.WriteUInt16((ushort)ID, 2, packet);
        }
        public byte Type
        {
            get { return packet[4]; }
            set { Writer.WriteByte(value, 4, packet); }
        }
        public byte Page
        {
            get { return packet[6]; }
            set { Writer.WriteByte(value, 6, packet); }
        }
        public byte ListCount
        {
            get { return packet[8]; }
            set { Writer.WriteByte(value, 8, packet); }
        }
        public uint MatchCount
        {
            get { return BitConverter.ToUInt32(packet, 10); }
            set { Writer.WriteUInt32(value, 10, packet); }
        }
        public ushort Group
        {
            get { return BitConverter.ToUInt16(packet, 14); }
            set { Writer.WriteUInt16(value, 14, packet); }
        }
        public ushort GUIType
        {
            get { return BitConverter.ToUInt16(packet, 16); }
            set { Writer.WriteUInt16(value, 16, packet); }
        }
        public ushort TimeLeft
        {
            get { return BitConverter.ToUInt16(packet, 18); }
            set { Writer.WriteUInt16(value, 18, packet); }
        }
        public uint TotalMatchesOnRoom
        {
            get { return BitConverter.ToUInt32(packet, 20); }
            set { Writer.WriteUInt32(value, 20, packet); }
        }
        public bool OnGoing
        {
            get { return packet[20] == 1; }
            set { Writer.WriteByte((byte)(value ? 1 : 0), 20, packet); }
        }
        private ushort offset = 24;
        public void Append(TeamElitePk.Match match)
        {
            try
            {
                if (Type != GUIEdit)
                    Writer.WriteUInt32(match.ID, offset, packet);

                offset += 4;
                Writer.WriteUInt16((ushort)match.MatchStats.Length, offset, packet); offset += 2;
                Writer.WriteUInt32((ushort)match.Index, offset, packet); offset += 2;
                if (match.MatchStats.Length == 1)
                    Writer.WriteUInt32((ushort)TeamElitePk.Match.StatusFlag.OK, offset, packet);
                else
                    Writer.WriteUInt32((ushort)match.Flag, offset, packet);

                offset += 2;

                for (int i = 0; i < match.MatchStats.Length; i++)
                {
                    AppendPlayer(match.MatchStats[i], offset);
                    offset += 48;
                }
                if (match.MatchStats.Length == 2) offset += 48;
                if (match.MatchStats.Length == 1) offset += 96;
            }
            catch (Exception e) { Console.WriteLine(e.ToString()); }
        }
        private void AppendPlayer(TeamElitePk.FighterStats stats, ushort _offset)
        {
            Writer.WriteUInt32(stats.Team.UID, _offset, packet); _offset += 4;
            Writer.WriteUInt32(stats.LeaderUID, _offset, packet); _offset += 4;
            Writer.WriteUInt32(stats.LeaderMesh, _offset, packet); _offset += 4;
            Writer.WriteString(stats.Name, _offset, packet); _offset += 32;
            Writer.WriteUInt32((uint)stats.Flag, _offset, packet); _offset += 4;
        }
        public byte[] ToArray()
        {
            return packet;
        }
        public void Send(GameState client)
        {
            client.Send(packet);
        }
        public void Deserialize(byte[] buffer)
        {
            packet = buffer;
        }
        public static void TeamPk(byte[] packet, Client.GameState client)
        {
            client.Send(packet);
            if (packet[4] == GamePackets.TeamElitePkBrackets.UpdateList ||
                packet[4] == GamePackets.TeamElitePkBrackets.RequestInformation ||
                packet[4] == GamePackets.TeamElitePkBrackets.InitialList)
                Game.Features.Tournaments.TeamElitePk.TeamTournament.Tournaments[packet[14]].Update(client, packet[6]);
        }
        public static void SkillTeamPk(byte[] packet, Client.GameState client)
        {
            client.Send(packet);
            if (packet[4] == GamePackets.TeamElitePkBrackets.UpdateList
                || packet[4] == GamePackets.TeamElitePkBrackets.RequestInformation ||
                packet[4] == GamePackets.TeamElitePkBrackets.InitialList)
                Game.Features.Tournaments.TeamElitePk.SkillTeamTournament.Tournaments[packet[14]].Update(client, packet[6]);
        }
    }
}