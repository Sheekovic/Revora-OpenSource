// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using Revora.Client;
using Revora.Network;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Revora.Network.GamePackets
{
    public unsafe class TeamElitePKMatchUI : Writer
    {
        byte[] packet;

        public const byte
            BeginMatch = 2,
            Effect = 3,
            EndMatch = 4,
            Information = 7,
            Reward = 8;

        public const uint
            Effect_Win = 1,
            Effect_Lose = 0;

        public TeamElitePKMatchUI(Game.Features.Tournaments.TeamElitePk.GamePackets ID)
        {
            packet = new byte[60];
            WriteUInt16(52, 0, packet);
            if (ID == Game.Features.Tournaments.TeamElitePk.GamePackets.SkillElitePkBrackets)
                WriteUInt16((ushort)Game.Features.Tournaments.TeamElitePk.GamePackets.SkillElitePKMatchUI, 2, packet);
            else
                WriteUInt16((ushort)Game.Features.Tournaments.TeamElitePk.GamePackets.TeamElitePKMatchUI, 2, packet);

        }
        public uint Type
        {
            get { return BitConverter.ToUInt32(packet, 4); }
            set { WriteUInt32(value, 4, packet); }
        }

        public uint dwParam
        {
            get { return BitConverter.ToUInt32(packet, 8); }
            set { WriteUInt32(value, 8, packet); }
        }

        public uint OpponentUID
        {
            get { return BitConverter.ToUInt32(packet, 12); }
            set { WriteUInt32(value, 12, packet); }
        }

        public string OpponentName
        {
            get { return GeneralData.ReadString(packet, 16, 16); }
            set { WriteString(value, 16, packet); }
        }

        public uint TimeLeft
        {
            get { return BitConverter.ToUInt32(packet, 44); }
            set { WriteUInt32(value, 44, packet); }
        }

        public void Append(Game.ConquerStructures.Team opponent)
        {
            OpponentUID = opponent.Lider.Entity.UID;
            if (opponent.EliteFighterStats != null)
                OpponentName = opponent.EliteFighterStats.Name;
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
