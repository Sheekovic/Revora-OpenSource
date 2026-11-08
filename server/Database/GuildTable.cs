using System;
using System.Collections.Generic;
using SubSonic;
using System.Linq;
using Conquer.Database;
using Revora.Game.ConquerStructures.Society;

namespace Revora.Database
{
    public unsafe class GuildTable
    {
        public static void Load()
        {
            Revora.Game.Features.GuildArsenalPurchases.Initialize();
            Console.WriteLine("Loading Guild information!");
            using (MySqlCommand command = new MySqlCommand(MySqlCommandType.SELECT).Select("entities").Where("guildid", 0L, true))
            {
                using (MySqlReader reader = new MySqlReader(command))
                {
                    while (reader.Read())
                    {
                        Guild.Member member = new Guild.Member(reader.ReadUInt16("guildid"))
                        {
                            ID = reader.ReadUInt32("uid"),
                            Name = reader.ReadString("name"),
                            Level = reader.ReadByte("level"),
                            Spouse = reader.ReadString("Spouse")
                        };
                    }
                }
            }
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("guilds"))
            using (var reader = new MySqlReader(cmd))
            {
                while (reader.Read())
                {
                    Guild guild = Materialize(reader);
                    Kernel.Guilds.Add(guild.ID, guild);
                    if (DateTime.Now < guild.adend)
                        Kernel.Guilds_adv.Add(guild.ID, guild);
                }
                //Console.WriteLine("Done!");
            }
            // Ally and enemy rows name both guilds, so they are resolved after every guild is registered.
            LoadAllyEnemy();
        }
        internal static Guild Read(MySqlReader reader)
        {
            Guild guild = new Guild(reader.ReadString("LeaderName"));
            guild.UnionID = reader.ReadUInt32("UnionID");
            guild.ID = reader.ReadUInt32("Id");
            guild.Name = reader.ReadString("Name");
            guild.Wins = reader.ReadUInt32("Wins");
            guild.Losts = reader.ReadUInt32("Losts");
            guild.Bulletin = reader.ReadString("Bulletin");
            guild.SilverFund = reader.ReadUInt64("SilverFund");
            guild.ConquerPointFund = reader.ReadUInt32("ConquerPointFund");
            guild.EnrollmentDate = reader.ReadUInt32("EnrollmentDate");
            guild.BulletinDate = reader.ReadUInt32("BulletinDate");
            guild.LevelRequirement = reader.ReadUInt32("LevelRequirement");
            guild.RebornRequirement = reader.ReadUInt32("RebornRequirement");
            guild.adLevelRequirement = reader.ReadUInt32("adLevelRequirement");
            guild.adRebornRequirement = reader.ReadUInt32("adRebornRequirement");
            guild.adClassRequirement = reader.ReadUInt32("adClassRequirement");
            guild.ClassRequirement = reader.ReadUInt32("ClassRequirement");
            guild.Points = reader.ReadUInt32("CTFPoints");
            guild.CTFReward = reader.ReadUInt32("CTFReward");
            guild.CTFdonationCPs = reader.ReadUInt32("CTFdonationCPs");
            guild.CTFdonationSilver = reader.ReadUInt32("CTFdonationSilver");
            guild.CTFdonationSilverold = reader.ReadUInt32("CTFdonationSilverold");
            guild.CTFdonationCPsold = reader.ReadUInt32("CTFdonationCPsold");
            guild.AutoRecruit = reader.ReadUInt32("AutoRecruit");
            guild.MaleFemale = reader.ReadUInt32("MaleFemale");
            guild.adv = reader.ReadString("adv");
            guild.ad_rate = reader.ReadUInt32("ad_rate");
            guild.adend = DateTime.FromBinary(reader.ReadInt64("adend"));
            guild.maxenchant = reader.ReadUInt32("maxenchant");
            return guild;
        }
        internal static Guild LoadRecord(uint id)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("guilds").Where("Id", (long)id))
            using (var reader = new MySqlReader(cmd))
            {
                if (!reader.Read()) return null;
                return Materialize(reader);
            }
        }
        // Read-only materialization shared by startup and the disconnected recovery prerequisite.
        // Saved offices survive reload; derived donation offices require their own durable ranking pass.
        private static Guild Materialize(MySqlReader reader)
        {
            Guild guild = Read(reader);
            guild.Load = true;
            LoadMembers(guild);
            guild.CalculateCTFRank(false);
            guild.MemberCount = (uint)guild.Members.Count;
            ArsenalTable.LoadArsenal(guild);
            ArsenalTable.LoadArsenal2(guild);
            RebuildLevel(guild);
            ApplySpouseRanks(guild);
            Revora.Game.Features.GuildLeadership.Recount(guild);
            BuildDonationRankings(guild);
            return guild;
        }
        // Call under the guild gate, or before publishing a materialized guild.
        // Revora refreshes immediately; original maintenance timing is not emulated.
        internal static void RebuildLevel(Guild guild)
        {
            guild.Level=(byte)(1+guild.Arsenal.Arsenals.Count(pair=>
                (byte)pair.Key>=1 && (byte)pair.Key<=8 && pair.Value.ID==pair.Key
                && pair.Value.Unlocked && pair.Value.Donation>=5000000));
        }
        private static void ApplySpouseRanks(Guild guild)
        {
            foreach (var member in guild.Members.Values)
            {
                if (member.Spouse == "None" || member.Spouse == "No"
                    || member.Rank > Game.Enums.GuildMemberRank.DLeaderSpouse) continue;
                foreach (var spouse in guild.Members.Values)
                {
                    if (member.Spouse != spouse.Name) continue;
                    if (spouse.Rank == Game.Enums.GuildMemberRank.GuildLeader)
                    {
                        member.Rank = Game.Enums.GuildMemberRank.LeaderSpouse;
                        break;
                    }
                    if (spouse.Rank == Game.Enums.GuildMemberRank.DeputyLeader)
                    {
                        if (member.Rank <= Game.Enums.GuildMemberRank.DLeaderSpouse)
                            member.Rank = Game.Enums.GuildMemberRank.DLeaderSpouse;
                        break;
                    }
                }
            }
        }
        public static void LoadAllyEnemy()
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("guildenemy"))
            using (var reader = new MySqlReader(cmd))
            {
                while (reader.Read())
                {
                    ushort guildID = reader.ReadUInt16("guildid");
                    ushort enemyID = reader.ReadUInt16("enemyid");
                    if (Kernel.Guilds.ContainsKey(guildID))
                        if (Kernel.Guilds.ContainsKey(enemyID))
                            Kernel.Guilds[guildID].Enemy.Add(enemyID, Kernel.Guilds[enemyID]);
                }
            }
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("guildally"))
            using (var reader = new MySqlReader(cmd))
            {
                while (reader.Read())
                {
                    ushort guildID = reader.ReadUInt16("guildid");
                    ushort allyID = reader.ReadUInt16("allyid");
                    if (Kernel.Guilds.ContainsKey(guildID))
                        if (Kernel.Guilds.ContainsKey(allyID))
                            Kernel.Guilds[guildID].Ally.Add(allyID, Kernel.Guilds[allyID]);
                }
            }
        }
        private static void LoadMembers(Guild guild)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("entities").Where("GuildID", guild.ID))
            using (var reader = new MySqlReader(cmd))
            while (reader.Read())
            {
                Guild.Member member = new Guild.Member(guild.ID);
                member.ID = reader.ReadUInt32("UID");
                member.Name = reader.ReadString("Name");
                member.Level = reader.ReadByte("Level");

                member.LastLogin = (ulong)CharacterActivity.Decode(reader.ReadNullableInt64Strict("LastLogin")).Ticks;
                member.Class = reader.ReadByte("Class");
                member.Spouse = reader.ReadString("Spouse");

                if (Game.ConquerStructures.Nobility.Board.ContainsKey(member.ID))
                {
                    member.NobilityRank = Game.ConquerStructures.Nobility.Board[member.ID].Rank;
                    member.Gender = Game.ConquerStructures.Nobility.Board[member.ID].Gender;
                }
                member.Rank = (Game.Enums.GuildMemberRank)reader.ReadUInt32("GuildRank");
                if (member.Rank == Game.Enums.GuildMemberRank.GuildLeader)
                {
                    if (guild.LeaderName != member.Name)
                    {
                        member.Rank = Game.Enums.GuildMemberRank.DeputyLeader;
                    }
                }
                if (member.Rank == Game.Enums.GuildMemberRank.GuildLeader)
                    guild.Leader = member;

                member.SilverDoantion = reader.ReadUInt64("GuildSilverDonation");
                member.SilverDonation = reader.ReadUInt64("GuildSilverDonation");
                member.ConquerPointDonation = reader.ReadUInt64("GuildConquerPointDonation");
                member.Arsenal_Donation = reader.ReadUInt64("ArsenalDonation");
                member.PkDonation = reader.ReadUInt32("GuildPkDonation");
                member.PKDoantion = member.PkDonation;
                member.Exploits = reader.ReadUInt32("Exploits");
                member.CTFCpsReward = reader.ReadUInt32("CTFCpsReward");
                member.CTFSilverReward = reader.ReadUInt32("CTFSilverReward");
                // No durable guild Guide donation producer exists yet. CP donations
                // must not be counted twice or masquerade as earned Guide credit.
                member.GuideDoantion = 0;
                member.Rouses = reader.ReadUInt32("GuildRouses");
                member.Lilies = reader.ReadUInt32("GuildLilies");
                member.Tulips = reader.ReadUInt32("GuildTulips");
                member.Orchids = reader.ReadUInt32("GuildOrchids");
                
                guild.Members.Add(member.ID, member);
                guild.ListMember.Add(member);

            }
            BuildDonationRankings(guild);
        }
        internal static void BuildDonationRankings(Guild guild)
        {
            var members=guild.Members.Values.OrderBy(member=>member.ID).ToArray();
            var rows=members.Select(entry=>new RankingList {
                EntityID=entry.ID,Name=entry.Name,postion=entry.Rank,
                silverdoantion=entry.SilverDoantionMath,cpdoantion=entry.ConquerPointDonationMath,
                arsenaldoantion=entry.Arsenal_Donation,pkdoantion=entry.PKDoantion,
                guidedoantion=entry.GuideDoantion,totaldoantion=entry.TotalDoantion,
                rosedoantion=entry.Rouses,liliesdoantion=entry.Lilies,
                orchidesdoantion=entry.Orchids,tuplisedoantion=entry.Tulips
            }).ToArray();
            var silver=DonationRanking(rows,r=>r.silverdoantion,true);
            var total=DonationRanking(rows,r=>r.totaldoantion,true);
            var cp=DonationRanking(rows,r=>r.cpdoantion,false);
            var pk=DonationRanking(rows,r=>r.pkdoantion,false);
            var guide=DonationRanking(rows,r=>r.guidedoantion,false);
            var arsenal=DonationRanking(rows,r=>r.arsenaldoantion,false);
            var rose=DonationRanking(rows,r=>r.rosedoantion,false);
            var lily=DonationRanking(rows,r=>r.liliesdoantion,false);
            var orchid=DonationRanking(rows,r=>r.orchidesdoantion,false);
            var tulip=DonationRanking(rows,r=>r.tuplisedoantion,false);
            foreach(var member in members) {
                member.SilversList=new List<RankingList>(silver);member.TotalDonationsList=new List<RankingList>(total);
                member.CPSList=new List<RankingList>(cp);member.PKSList=new List<RankingList>(pk);
                member.GuidesList=new List<RankingList>(guide);member.ArsenalsList=new List<RankingList>(arsenal);
                member.RosesList=new List<RankingList>(rose);member.LiliesList=new List<RankingList>(lily);
                member.OrchidsList=new List<RankingList>(orchid);member.TulipsList=new List<RankingList>(tulip);
            }
            guild.ListMember=new List<Guild.Member>(members);
        }
        private static RankingList[] DonationRanking(RankingList[] rows,Func<RankingList,ulong> score,bool positiveOnly)
        {
            return rows.Where(row=>!positiveOnly || score(row)>0).OrderByDescending(score).ThenBy(row=>row.EntityID)
                .Select((row,index)=>{
                    row.rank=index;
                    row.silverdoantion=Math.Min(uint.MaxValue,row.silverdoantion);row.cpdoantion=Math.Min(uint.MaxValue,row.cpdoantion);
                    row.guidedoantion=Math.Min(uint.MaxValue,row.guidedoantion);row.pkdoantion=Math.Min(uint.MaxValue,row.pkdoantion);
                    row.arsenaldoantion=Math.Min(uint.MaxValue,row.arsenaldoantion);row.totaldoantion=Math.Min(uint.MaxValue,row.totaldoantion);
                    return row;
                }).ToArray();
        }
        public static void UpdateGuildAdvertise(Guild guild)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE))
                cmd.Update("guilds")
                .Set("adv", guild.adv)
                .Set("ad_rate", guild.ad_rate)
                .Set("AutoRecruit", guild.AutoRecruit)
                .Set("adLevelRequirement", guild.adLevelRequirement)
                .Set("adRebornRequirement", guild.adRebornRequirement)
                .Set("adClassRequirement", guild.adClassRequirement)
                .Set("MaleFemale", guild.MaleFemale)
                .Set("adend", guild.adend.ToBinary())
                .Where("Id", guild.ID)
                .Execute();
        }
        public static void UpdateEnchant(Guild guild)
        {
            using (var command = new MySqlCommand(MySqlCommandType.UPDATE))
                command.Update("guilds").Set("maxenchant", guild.maxenchant).Where("ID", guild.ID).Execute();
        }
        public static void UpdateUnionID(Guild guild)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE).Update("guilds")
                .Set("UnionID", guild.UnionID)
                .Where("ID", guild.ID))
                cmd.Execute();
        }
        public static void UpdateBulletin(Guild guild)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE).Update("guilds")
                .Set("Bulletin", guild.Bulletin)
                .Set("BulletinDate", guild.BulletinDate = Emulator.Core.Common.TimeGet(Emulator.Core.TimeType.Day))
                .Where("ID", guild.ID))
                cmd.Execute();
        }
        public static void SaveFunds(Guild guild)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE)
                .Update("guilds")
                .Set("ConquerPointFund", guild.ConquerPointFund)
                .Set("SilverFund", guild.SilverFund)
                .Set("CTFdonationCPsold", guild.CTFdonationCPsold)
                .Set("CTFdonationSilverold", guild.CTFdonationSilverold)
                .Set("CTFdonationCPs", guild.CTFdonationCPs)
                .Set("CTFdonationSilver", guild.CTFdonationSilver)
                .Where("ID", guild.ID))
                cmd.Execute();
        }
        public static void SaveCTFReward(Guild guild)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE)
                .Update("guilds")
                .Set("CTFReward", guild.CTFReward)
                .Set("CTFPoints", guild.Points)
                .Where("ID", guild.ID))
                cmd.Execute();
        }
        public static void SaveCTFPoins(Guild guild)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE)
                .Update("guilds")
                .Set("CTFPoints", guild.Points)
                .Where("ID", guild.ID))
                cmd.Execute();
        }
        public static void Disband(Guild guild)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE)
                .Update("entities")
                .Set("guildid", 0)
                .Where("guildid", guild.ID))
                cmd.Execute();
            using (var cmd = new MySqlCommand(MySqlCommandType.DELETE)
                .Delete("guilds", "id", guild.ID))
                cmd.Execute();
            using (var del = new MySqlCommand(MySqlCommandType.DELETE).Delete("pt_arsenal", "syn_id", guild.ID))
                del.Execute();
        }
        public static void Create(Guild guild)
        {
            while (true)
            {
                using (var cmd = new MySqlCommand(MySqlCommandType.SELECT)
                    .Select("guilds")
                    .Where("id", guild.ID))
                using (var reader = cmd.CreateReader())
                {
                    if (reader.Read())
                        guild.ID = Guild.GuildCounter.Next;
                    else
                        break;
                }
            }
            uint day = guild.EnrollmentDate != 0
                ? guild.EnrollmentDate
                : Emulator.Core.Common.TimeGet(Emulator.Core.TimeType.Day);
            if (guild.BulletinDate == 0)
                guild.BulletinDate = day;
            using (var cmd = new MySqlCommand(MySqlCommandType.INSERT).Insert("guilds")
               .Insert("ID", guild.ID)
               .Insert("name", guild.Name)
               .Insert("Bulletin", guild.Bulletin ?? "")
               .Insert("SilverFund", guild.SilverFund)
               .Insert("EnrollmentDate", day)
               .Insert("BulletinDate", guild.BulletinDate)
               .Insert("LeaderName", guild.LeaderName))
                cmd.Execute();
        }
        public static void SaveLeader(Guild guild)
        {
            using (MySqlCommand cmd = new MySqlCommand(MySqlCommandType.UPDATE)
                .Update("guilds")
                .Set("LeaderName", guild.LeaderName)
                .Where("id", guild.ID))
                cmd.Execute();
        }
        public static void AddEnemy(Guild guild, uint enemy)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.INSERT)
                .Insert("guildenemy")
                .Insert("guildID", guild.ID)
                .Insert("enemyID", enemy))
                cmd.Execute();
        }
        public static void AddAlly(Guild guild, uint ally)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.INSERT)
                .Insert("guildally")
                .Insert("guildID", guild.ID)
                .Insert("allyID", ally))
                cmd.Execute();
        }
        public static void RemoveEnemy(Guild guild, uint enemy)
        {
            using (var command = new MySqlCommand(MySqlCommandType.DELETE))
                command.Delete("guildenemy", "GuildID", guild.ID).And("EnemyID", enemy)
                    .Execute();
        }
        public static void RemoveAlly(Guild guild, uint ally)
        {
            using (var command = new MySqlCommand(MySqlCommandType.DELETE))
                command.Delete("guildally", "GuildID", guild.ID).And("AllyID", ally)
                    .Execute();
            using (var command = new MySqlCommand(MySqlCommandType.DELETE))
                command.Delete("guildally", "GuildID", ally).And("AllyID", guild.ID)
                    .Execute();
        }
        public static void UpdateGuildWarStats(Guild guild)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE)
                .Update("guilds")
                .Set("wins", guild.Wins)
                .Set("losts", guild.Losts)
                .Where("id", guild.ID))
                cmd.Execute();
        }
        internal static void SaveRequirements(Guild guild)
        {
            using (var command = new MySqlCommand(MySqlCommandType.UPDATE))
                command.Update("guilds")
                .Set("LevelRequirement", guild.LevelRequirement)
                .Set("RebornRequirement", guild.RebornRequirement)
                .Set("ClassRequirement", guild.ClassRequirement)
                .Where("ID", guild.ID)
                .Execute();
        }
    }
}
