// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

namespace Revora.Network.GamePackets
{
    public unsafe class SkillEliteSetTeamName : Writer
    {
        byte[] packet;
        public const byte
        Apprend = 0,
        SuccessfulName = 1,
        RenameWasSuccessfulName = 2,
        Remove = 3;

        public SkillEliteSetTeamName(Game.Features.Tournaments.TeamElitePk.GamePackets ID)
        {
            packet = new byte[56];
            WriteUInt16(48, 0, packet);
            if (ID == Game.Features.Tournaments.TeamElitePk.GamePackets.SkillElitePkBrackets)
                WriteUInt16((ushort)Game.Features.Tournaments.TeamElitePk.GamePackets.SkillEliteSetTeamName, 2, packet);
            else
                WriteUInt16((ushort)Game.Features.Tournaments.TeamElitePk.GamePackets.TeamEliteSetTeamName, 2, packet);
        }
        public uint Type
        {
            get { return BitConverter.ToUInt32(packet, 4); }
            set { WriteUInt32(value, 4, packet); }
        }
        public uint TeamID
        {
            get { return BitConverter.ToUInt32(packet, 8); }
            set { WriteUInt32(value, 8, packet); }
        }
        public string TeamName
        {
            get { return GeneralData.ReadString(packet, 12, 32); }
            set { WriteString(value, 12, packet); }
        }
        public byte[] ToArray()
        {
            return packet;
        }
    }
}