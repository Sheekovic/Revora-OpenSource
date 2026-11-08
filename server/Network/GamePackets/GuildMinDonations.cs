// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using Revora.Game;
using Revora.Game.ConquerStructures.Society;

namespace Revora.Network.GamePackets
{
    public unsafe class GuildMinDonations : Writer
    {
        private byte[] packet;
        private ushort Position = 8;
        public GuildMinDonations(ushort counts = 0)
        {
            packet = new byte[(int)(16 + counts * 8)];
            Writer.WriteUInt16((ushort)(packet.Length - 8), 0, packet);
            Writer.WriteUInt16(1061, 2, packet);
            Writer.WriteUInt16(counts, 6, packet);
        }
        public void Aprend(Enums.GuildMemberRank Rank, uint amount)
        {
            Writer.WriteUInt32((uint)Rank, (int)Position, packet);
            Position += 4;
            Writer.WriteUInt32(amount, (int)Position, packet);
            Position += 4;
        }
        public void AprendGuild(Guild guild)
        {
            if (guild.RankArsenalDonations.Length >= 5)
            {
                Guild.Member member = guild.RankArsenalDonations[4];
                Aprend(Enums.GuildMemberRank.Manager, member.ArsenalDonation);
            }
            else
            {
                Aprend(Enums.GuildMemberRank.Manager, 0);
            }
            if (guild.RankArsenalDonations.Length >= 7)
            {
                Guild.Member member2 = guild.RankArsenalDonations[6];
                Aprend(Enums.GuildMemberRank.HonoraryManager, member2.ArsenalDonation);
            }
            else
            {
                Aprend(Enums.GuildMemberRank.HonoraryManager, 0);
            }
            if (guild.RankArsenalDonations.Length >= 8)
            {
                Guild.Member member3 = guild.RankArsenalDonations[7];
                Aprend(Enums.GuildMemberRank.Supervisor, member3.ArsenalDonation);
            }
            else
            {
                Aprend(Enums.GuildMemberRank.Supervisor, 0);
            }
            if (guild.RankArsenalDonations.Length >= 13)
            {
                Guild.Member member4 = guild.RankArsenalDonations[12];
                Aprend(Enums.GuildMemberRank.Steward, member4.ArsenalDonation);
            }
            else
            {
                Aprend(Enums.GuildMemberRank.Steward, 0);
            }
            if (guild.RankArsenalDonations.Length >= 15)
            {
                Guild.Member member5 = guild.RankArsenalDonations[14];
                Aprend(Enums.GuildMemberRank.ArsFollower, member5.ArsenalDonation);
            }
            else
            {
                Aprend(Enums.GuildMemberRank.ArsFollower, 0);
            }
            if (guild.RankCPDonations.Length >= 3)
            {
                Guild.Member member6 = guild.RankCPDonations[2];
                Aprend(Enums.GuildMemberRank.CPSupervisor, (uint)member6.ConquerPointDonation);
            }
            else
            {
                Aprend(Enums.GuildMemberRank.CPSupervisor, 0);
            }
            if (guild.RankCPDonations.Length >= 5)
            {
                Guild.Member member7 = guild.RankCPDonations[4];
                Aprend(Enums.GuildMemberRank.CPAgent, (uint)member7.ConquerPointDonation);
            }
            else
            {
                Aprend(Enums.GuildMemberRank.CPAgent, 0);
            }
            if (guild.RankCPDonations.Length >= 7)
            {
                Guild.Member member8 = guild.RankCPDonations[6];
                Aprend(Enums.GuildMemberRank.CPFollower, (uint)member8.ConquerPointDonation);
            }
            else
            {
                Aprend(Enums.GuildMemberRank.CPFollower, 0);
            }
            if (guild.RankPkDonations.Length >= 3)
            {
                Guild.Member member9 = guild.RankPkDonations[2];
                Aprend(Enums.GuildMemberRank.PKSupervisor, member9.PkDonation);
            }
            else
            {
                Aprend(Enums.GuildMemberRank.PKSupervisor, 0);
            }
            if (guild.RankPkDonations.Length >= 5)
            {
                Guild.Member member10 = guild.RankPkDonations[4];
                Aprend(Enums.GuildMemberRank.PKAgent, member10.PkDonation);
            }
            else
            {
                Aprend(Enums.GuildMemberRank.PKAgent, 0);
            }
            if (guild.RankPkDonations.Length >= 7)
            {
                Guild.Member member11 = guild.RankPkDonations[6];
                Aprend(Enums.GuildMemberRank.PKFollower, member11.PkDonation);
            }
            else
            {
                Aprend(Enums.GuildMemberRank.PKFollower, 0);
            }
            if (guild.RankRosseDonations.Length >= 3)
            {
                Guild.Member member12 = guild.RankRosseDonations[2];
                Aprend(Enums.GuildMemberRank.RoseSupervisor, member12.Rouses);
            }
            else
            {
                Aprend(Enums.GuildMemberRank.RoseSupervisor, 0);
            }
            if (guild.RankRosseDonations.Length >= 5)
            {
                Guild.Member member13 = guild.RankRosseDonations[4];
                Aprend(Enums.GuildMemberRank.RoseAgent, member13.Rouses);
            }
            else
            {
                Aprend(Enums.GuildMemberRank.RoseAgent, 0);
            }
            if (guild.RankRosseDonations.Length >= 7)
            {
                Guild.Member member14 = guild.RankRosseDonations[6];
                Aprend(Enums.GuildMemberRank.RoseFollower, member14.Rouses);
            }
            else
            {
                Aprend(Enums.GuildMemberRank.RoseFollower, 0);
            }
            if (guild.RankLiliesDonations.Length >= 3)
            {
                Guild.Member member15 = guild.RankLiliesDonations[2];
                Aprend(Enums.GuildMemberRank.LilySupervisor, member15.Lilies);
            }
            else
            {
                Aprend(Enums.GuildMemberRank.LilySupervisor, 0);
            }
            if (guild.RankLiliesDonations.Length >= 5)
            {
                Guild.Member member16 = guild.RankLiliesDonations[4];
                Aprend(Enums.GuildMemberRank.LilyAgent, member16.Lilies);
            }
            else
            {
                Aprend(Enums.GuildMemberRank.LilyAgent, 0);
            }
            if (guild.RankLiliesDonations.Length >= 7)
            {
                Guild.Member member17 = guild.RankLiliesDonations[6];
                Aprend(Enums.GuildMemberRank.LilyFollower, member17.Lilies);
            }
            else
            {
                Aprend(Enums.GuildMemberRank.LilyFollower, 0);
            }
            if (guild.RankTulipsDonations.Length >= 3)
            {
                Guild.Member member18 = guild.RankTulipsDonations[2];
                Aprend(Enums.GuildMemberRank.TSupervisor, member18.Tulips);
            }
            else
            {
                Aprend(Enums.GuildMemberRank.TSupervisor, 0);
            }
            if (guild.RankTulipsDonations.Length >= 5)
            {
                Guild.Member member19 = guild.RankTulipsDonations[4];
                Aprend(Enums.GuildMemberRank.TulipAgent, member19.Tulips);
            }
            else
            {
                Aprend(Enums.GuildMemberRank.TulipAgent, 0);
            }
            if (guild.RankTulipsDonations.Length >= 7)
            {
                Guild.Member member20 = guild.RankTulipsDonations[6];
                Aprend(Enums.GuildMemberRank.TulipFollower, member20.Tulips);
            }
            else
            {
                Aprend(Enums.GuildMemberRank.TulipFollower, 0);
            }
            if (guild.RankOrchidsDonations.Length >= 3)
            {
                Guild.Member member21 = guild.RankOrchidsDonations[2];
                Aprend(Enums.GuildMemberRank.OSupervisor, member21.Orchids);
            }
            else
            {
                Aprend(Enums.GuildMemberRank.OSupervisor, 0u);
            }
            if (guild.RankOrchidsDonations.Length >= 5)
            {
                Guild.Member member22 = guild.RankOrchidsDonations[4];
                Aprend(Enums.GuildMemberRank.OrchidAgent, member22.Orchids);
            }
            else
            {
                Aprend(Enums.GuildMemberRank.OrchidAgent, 0);
            }
            if (guild.RankOrchidsDonations.Length >= 7)
            {
                Guild.Member member23 = guild.RankOrchidsDonations[6];
                Aprend(Enums.GuildMemberRank.OrchidFollower, member23.Orchids);
            }
            else
            {
                Aprend(Enums.GuildMemberRank.OrchidFollower, 0);
            }
            if (guild.RankTotalDonations.Length >= 2)
            {
                Guild.Member member24 = guild.RankTotalDonations[1];
                Aprend(Enums.GuildMemberRank.HDeputyLeader, member24.TotalDonation);
            }
            else
            {
                Aprend(Enums.GuildMemberRank.HDeputyLeader, 0);
            }
            if (guild.RankTotalDonations.Length >= 4)
            {
                Guild.Member member25 = guild.RankTotalDonations[3];
                Aprend(Enums.GuildMemberRank.HonorarySteward, member25.TotalDonation);
            }
            else
            {
                Aprend(Enums.GuildMemberRank.HonorarySteward, 0);
            }
            if (guild.RankSilversDonations.Length >= 4)
            {
                Guild.Member member26 = guild.RankSilversDonations[3];
                Aprend(Enums.GuildMemberRank.SSupervisor, (uint)member26.SilverDonation);
            }
            else
            {
                Aprend(Enums.GuildMemberRank.SSupervisor, 0);
            }
            if (guild.RankSilversDonations.Length >= 6)
            {
                Guild.Member member27 = guild.RankSilversDonations[5];
                Aprend(Enums.GuildMemberRank.SilverAgent, (uint)member27.SilverDonation);
            }
            else
            {
                Aprend(Enums.GuildMemberRank.SilverAgent, 0);
            }
            if (guild.RankSilversDonations.Length >= 8)
            {
                Guild.Member member28 = guild.RankSilversDonations[7];
                Aprend(Enums.GuildMemberRank.SilverFollower, (uint)member28.SilverDonation);
            }
            else
            {
                Aprend(Enums.GuildMemberRank.SilverFollower, 0);
            }
            if (guild.RankGuideDonations.Length >= 3)
            {
                Guild.Member member29 = guild.RankGuideDonations[2];
                Aprend(Enums.GuildMemberRank.GSupervisor, member29.VirtutePointes);
            }
            else
            {
                Aprend(Enums.GuildMemberRank.GSupervisor, 0);
            }
            if (guild.RankGuideDonations.Length >= 5)
            {
                Guild.Member member30 = guild.RankGuideDonations[4];
                Aprend(Enums.GuildMemberRank.GuideAgent, member30.VirtutePointes);
            }
            else
            {
                Aprend(Enums.GuildMemberRank.GuideAgent, 0u);
            }
            if (guild.RankGuideDonations.Length >= 7)
            {
                Guild.Member member31 = guild.RankGuideDonations[6];
                Aprend(Enums.GuildMemberRank.GuideFollower, member31.VirtutePointes);
                return;
            }
            Aprend(Enums.GuildMemberRank.GuideFollower, 0);
        }
        public byte[] ToArray()
        {
            return packet;
        }
    }
}