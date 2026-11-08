// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading;
using Revora.Network;
using Revora.Game.Features;
using System.Collections.Generic;
using Revora.Network.GamePackets;
using System.Runtime.CompilerServices;

namespace Revora.Game.ConquerStructures.Society
{
    public unsafe struct RankingList
    {
        public uint EntityID;
        public string Name;
        public ulong silverdoantion;
        public ulong guidedoantion;
        public ulong cpdoantion;
        public ulong arsenaldoantion;
        public ulong totaldoantion;
        public ulong pkdoantion;
        public ulong rosedoantion;
        public ulong liliesdoantion;
        public ulong orchidesdoantion;
        public ulong tuplisedoantion;
        public Enums.GuildMemberRank postion;
        public int rank;
    }
    public unsafe class Guild
    {
        public class ClassRequirements
        {
            public const uint
            Trojan = 1,
            Warrior = 2,
            Taoist = 4,
            Archer = 8,
            Ninja = 16,
            Monk = 32,
            Pirate = 64,
            LeeLong = 80;
        }
        public uint cp_donaion;
        public uint Points;
        public uint CTFdonationCPs;
        public uint CTFdonationSilver;
        public uint CTFdonationCPsold;
        public uint CTFdonationSilverold;
        public uint CrossGuildCPsold;
        public uint CrossGuildMoneyold;
        public uint CrossGuildCPs;
        public uint CrossGuildMoney;
        public List<uint> BlackList = new List<uint>();
        public static SafeDictionary<uint, Guild> GuildWinners = new SafeDictionary<uint, Guild>(10000);
        public static SafeDictionary<uint, Guild> Scores = new SafeDictionary<uint, Guild>(10000);
        public static Counter GuildCounter;
        public DateTime adend
        {
            get { return DateTime.FromBinary((long)adEnd); }
            set { adEnd = (long)value.Ticks; }
        }
        public bool armor = false;
        public bool head = false;
        public bool ring = false;
        public bool necklace = false;
        public bool boot = false;
        public long adEnd;
        public bool weapone = false;
        public bool tower = false;
        public bool fan = false;
        public bool staticlevel = false;
        public bool rings1 = false;
        public bool rings2 = false;
        public bool rings3 = false;
        public bool armors1 = false;
        public bool armors2 = false;
        public bool armors3 = false;
        public bool heads1 = false;
        public bool heads2 = false;
        public bool heads3 = false;
        public bool necklaces1 = false;
        public bool necklaces2 = false;
        public bool necklaces3 = false;
        public bool boots1 = false;
        public bool boots2 = false;
        public bool boots3 = false;
        public bool weapones1 = false;
        public bool weapones2 = false;
        public bool weapones3 = false;
        public bool towers1 = false;
        public bool towers2 = false;
        public bool towers3 = false;
        public bool fans1 = false;
        public bool fans2 = false;
        public bool fans3 = false;
        public bool getbp = false;
        public uint maxenchant = 0;

        public Guild.Member[] RankSilversDonations = new Guild.Member[0];
        public Guild.Member[] RankArsenalDonations = new Guild.Member[0];
        public Guild.Member[] RankCPDonations = new Guild.Member[0];
        public Guild.Member[] RankPkDonations = new Guild.Member[0];
        public Guild.Member[] RankLiliesDonations = new Guild.Member[0];
        public Guild.Member[] RankOrchidsDonations = new Guild.Member[0];
        public Guild.Member[] RankRosseDonations = new Guild.Member[0];
        public Guild.Member[] RankTulipsDonations = new Guild.Member[0];
        public Guild.Member[] RankGuideDonations = new Guild.Member[0];
        public Guild.Member[] RankTotalDonations = new Guild.Member[0];

        public int[] RanksCounts = new int[1001];
        public static void Process(byte[] Packet, Client.GameState client)
        {
            // A profile request displays progress; it cannot mint treasury funds
            // or assign guild office. Promotions use the authorized rank service.
            if(Packet==null || Packet.Length<68 || Packet.Length>128
                || BitConverter.ToUInt16(Packet,0)!=Packet.Length-8
                || BitConverter.ToUInt16(Packet,2)!=Revora.Network.GamePackets.PacketID.TopGuild
                || BitConverter.ToUInt64(Packet,Packet.Length-8)!=Revora.Network.PacketHandler.ClientSeal
                || !Features.QuestInventory.Player(client,false))return;
            lock(client.ItemSyncRoot) {
                var guild=client.Guild;Member member;Guild current;
                if(!Features.QuestInventory.Player(client,false) || guild==null || client.Entity.GuildID!=guild.ID
                    || !Kernel.Guilds.TryGetValue(guild.ID,out current) || !ReferenceEquals(current,guild)
                    || !guild.Members.TryGetValue(client.Entity.UID,out member) || !ReferenceEquals(member,client.AsMember) || member.GuildID!=guild.ID)return;
                var p=new GuildProfilePacket();p.Deserialize(Packet);
                p.Silver=(uint)Math.Min(uint.MaxValue,member.SilverDonation);
                p.Cps=(uint)Math.Min(uint.MaxValue,member.ConquerPointDonation);
                p.Guide=(uint)Math.Min(uint.MaxValue,member.GuideDoantion);
                p.Pk=client.Entity.PKPoints;
                p.Arsenal=(uint)Math.Min(uint.MaxValue,member.Arsenal_Donation);
                p.Rose=client.Entity.Flowers==null?0:(uint)client.Entity.Flowers.RedRoses;
                p.Lily=client.Entity.Flowers==null?0:(uint)client.Entity.Flowers.Lilies;
                p.Orchid=client.Entity.Flowers==null?0:(uint)client.Entity.Flowers.Orchads;
                p.Tulip=client.Entity.Flowers==null?0:(uint)client.Entity.Flowers.Tulips;
                p.Exploits=(uint)member.Exploits;p.HistoryCps=0;p.HistoryGuide=0;p.HistoryPk=0;
                guild.SendGuild(client);client.Send(Packet);
            }
        }
        public class Member : Interfaces.IKnownPerson
        {
            public uint TotalDonation
            {
                get { return (uint)Math.Min(uint.MaxValue,DonationScore); }
            }
            public Member(uint GuildID)
            {
                this.GuildID = GuildID;
            }
            public uint ID
            {
                get;
                set;
            }
            public uint VirtutePointes;
            public uint Lilies;
            public uint Rouses;
            public uint Orchids;
            public uint Tulips;
            public uint ArsenalDonation;
            public uint PkDonation;
            public uint pkp_donation = 0;
            public uint ExploitsRank;
            public string Spouse;
            public uint Exploits;
            public uint MembersExploits;
            public uint CTFSilverReward;
            public uint CTFCpsReward;
            public uint CrossCpsReward;
            public uint CrossSilverReward;
            public uint WarScore;
            public string Name
            {
                get;
                set;
            }
            public bool IsOnline
            {
                get
                {
                    return Kernel.GamePool.ContainsKey(ID);
                }
            }
            public Client.GameState client;
            public Client.GameState Client
            {
                get
                {
                    return Kernel.GamePool[ID];
                }
            }
            public List<RankingList> SilversList = new List<RankingList>();
            public List<RankingList> CPSList = new List<RankingList>();
            public List<RankingList> TotalDonationsList = new List<RankingList>();
            public List<RankingList> ArsenalsList = new List<RankingList>();
            public List<RankingList> PKSList = new List<RankingList>();
            public List<RankingList> GuidesList = new List<RankingList>();
            public List<RankingList> TulipsList = new List<RankingList>();
            public List<RankingList> OrchidsList = new List<RankingList>();
            public List<RankingList> RosesList = new List<RankingList>();
            public List<RankingList> LiliesList = new List<RankingList>();
            public ulong TotalDoantion
            {
                get
                {
                    return (ulong)Math.Min(ulong.MaxValue,DonationScore);
                }

            }
            // Currency counters retain paid amounts. Rankings use contribution points.
            internal decimal DonationScore {
                get { return SilverDoantionMath+(decimal)ConquerPointDonation*20+Arsenal_Donation+PKDoantion+GuideDoantion; }
            }
            public ulong _Arsenal_Donation;
            public ulong Arsenal_Donation
            {
                get
                {
                    return _Arsenal_Donation;
                }
                set
                {
                    _Arsenal_Donation = value;

                }
            }
            private ulong _SilverDoantion;
            public ulong SilverDoantion
            {
                get
                {
                    return _SilverDoantion;
                }
                set
                {
                    _SilverDoantion = value;

                }
            }
            private ulong _PKDoantion;
            public ulong PKDoantion
            {
                get
                {
                    return _PKDoantion;
                }
                set
                {
                    _PKDoantion = value;

                }
            }
            private ulong _GuideDoantion;
            public ulong GuideDoantion
            {
                get
                {
                    return _GuideDoantion;
                }
                set
                {
                    _GuideDoantion = value;

                }
            }
            private ulong _RoseDonation;
            public ulong RoseDonation
            {
                get
                {
                    return _RoseDonation;
                }
                set
                {
                    _RoseDonation = value;

                }
            }
            private ulong _OrchidesDonation;
            public ulong OrchidesDonation
            {
                get
                {
                    return _OrchidesDonation;
                }
                set
                {
                    _OrchidesDonation = value;

                }
            }
            private ulong _TulipDonation;
            public ulong TulipDonation
            {
                get
                {
                    return _TulipDonation;
                }
                set
                {
                    _TulipDonation = value;

                }
            }
            private ulong _LiliesDonation;
            public ulong LiliesDonation
            {
                get
                {
                    return _LiliesDonation;
                }
                set
                {
                    _LiliesDonation = value;

                }
            }
            public ulong _ConquerPointDonation;
            public ulong ConquerPointDonation
            {
                get
                {
                    return _ConquerPointDonation;
                }
                set
                {
                    _ConquerPointDonation = value;

                }
            }
            public ulong SilverDoantionMath
            {
                get
                {
                    const ulong threshold=300000UL*10000UL;
                    return SilverDoantion<=threshold ? SilverDoantion/10000UL
                        : 300000UL+(SilverDoantion-threshold)/100000UL;
                }
            }
            public ulong ConquerPointDonationMath
            {
                get
                {
                    return (ulong)Math.Min(ulong.MaxValue,(decimal)ConquerPointDonation*20);
                }
            }
            public uint cpdoantion;
            public uint honor_donation = 0;
            public ulong SilverDonation
            {
                get;
                set;
            }
            private uint _GuildID;
            public ulong LastLogin = 0;
            public uint GuildID
            {
                get { return _GuildID; }
                set { _GuildID = value; }
            }
            public Guild Guild
            {
                get { return Kernel.Guilds[GuildID]; }
            }
            public Enums.GuildMemberRank _Rank;
            public Enums.GuildMemberRank Rank
            {
                get { return _Rank; }
                set { _Rank = value; }
            }
            public byte Level
            {
                get;
                set;
            }
            public uint Class
            {
                get;
                set;
            }
            public NobilityRank NobilityRank
            {
                get;
                set;
            }
            public byte Gender
            {
                get;
                set;
            }
        }
        private byte[] Buffer;
        public uint CTFFlagScore;
        public uint CrossFlagScore;
        public uint EWarScore;
        public uint PTScore;
        public uint WarScore;
        public uint MembersExploits = 0;
        public uint PointsGuild = 0;
        public uint Exploits = 0;
        public uint CTFReward = 0;
        public uint CTFPoints;
        public bool PoleKeeper
        {
            get { return GuildWar.Pole.Name == Name; }
        }
        public bool PoleKeeper1
        {
            get { return SuperGuildWar.Pole.Name == Name; }
        }
        public bool PoleKeeper2
        {
            get
            {
                return EliteGuildWar.Poles.Name == Name;
            }
        }
        public bool PoleKeeper3
        {
            get
            {
                return PoleTwin.Pole.Name == Name;
            }
        } 
        public Guild(string leadername)
        {
            Buffer = new byte[100];
            LeaderName = leadername;
            Points = 0;
            CTFdonationCPs = 0;
            CTFdonationSilver = 0;
            CTFdonationCPsold = 0;
            CTFdonationSilverold = 0;
            Writer.WriteUInt16(92, 0, Buffer);
            Writer.WriteUInt16(1106, 2, Buffer);
            Buffer[48] = 0x2;
            Buffer[49] = 0x1;
            Buffer[75] = 0x1;
            Buffer[87] = 0x20;
            LevelRequirement = 1;
            Writer.WriteByte(Level, 60, Buffer);
            Writer.WriteUInt32((uint)EnrollmentDate, 67, this.Buffer);
            Members = new SafeDictionary<uint, Member>(1000);
            Ally = new SafeDictionary<uint, Guild>(1000);
            Enemy = new SafeDictionary<uint, Guild>(1000);
            A_Packet = new ArsenalPacket();
            Arsenal = new Features.Arsenal(0);
        }
        public uint ID
        {
            get { return BitConverter.ToUInt32(Buffer, 4); }
            set { Writer.WriteUInt32(value, 4, Buffer); }
        }
        public ulong SilverFund
        {
            get { return BitConverter.ToUInt64(Buffer, 12); }
            set { Writer.WriteUInt64(value, 12, Buffer); }
        }
        public uint ConquerPointFund
        {
            get { return BitConverter.ToUInt32(Buffer, 20); }
            set { Writer.WriteUInt32(value, 20, Buffer); }
        }
        public uint adLevelRequirement;
        public uint adRebornRequirement;
        public uint adClassRequirement;
        public uint AutoRecruit;
        public ulong ad_rate;
        public uint MaleFemale;
        public string adv;
        public uint MemberCount
        {
            get { return BitConverter.ToUInt32(Buffer, 24); }
            set { Writer.WriteUInt32(value, 24, Buffer); }
        }
        public uint LevelRequirement
        {
            get { return BitConverter.ToUInt32(Buffer, 48); }
            set { Writer.WriteUInt32(value, 48, Buffer); }
        }
        public uint RebornRequirement
        {
            get { return BitConverter.ToUInt32(Buffer, 52); }
            set { Writer.WriteUInt32(value, 52, Buffer); }
        }
        public uint ClassRequirement
        {
            get { return BitConverter.ToUInt32(Buffer, 56); }
            set { Writer.WriteUInt32(value, 56, Buffer); }
        }
        public bool AllowTrojans
        {
            get { return ((ClassRequirement & ClassRequirements.Trojan) != ClassRequirements.Trojan); }
            set { ClassRequirement ^= ClassRequirements.Trojan; }
        }
        public bool AllowWarriors
        {
            get { return ((ClassRequirement & ClassRequirements.Warrior) != ClassRequirements.Warrior); }
            set { ClassRequirement ^= ClassRequirements.Warrior; }
        }
        public bool AllowTaoists
        {
            get { return ((ClassRequirement & ClassRequirements.Taoist) != ClassRequirements.Taoist); }
            set { ClassRequirement ^= ClassRequirements.Taoist; }
        }
        public bool AllowArchers
        {
            get { return ((ClassRequirement & ClassRequirements.Archer) != ClassRequirements.Archer); }
            set { ClassRequirement ^= ClassRequirements.Archer; }
        }
        public bool AllowNinjas
        {
            get { return ((ClassRequirement & ClassRequirements.Ninja) != ClassRequirements.Ninja); }
            set { ClassRequirement ^= ClassRequirements.Ninja; }
        }
        public bool AllowMonks
        {
            get { return ((ClassRequirement & ClassRequirements.Monk) != ClassRequirements.Monk); }
            set { ClassRequirement ^= ClassRequirements.Monk; }
        }
        public bool AllowPirates
        {
            get { return ((ClassRequirement & ClassRequirements.Pirate) != ClassRequirements.Pirate); }
            set { ClassRequirement ^= ClassRequirements.Pirate; }
        }
        public bool AllowLeeLong
        {
            get { return ((ClassRequirement & ClassRequirements.LeeLong) != ClassRequirements.LeeLong); }
            set { ClassRequirement ^= ClassRequirements.LeeLong; }
        }
        public bool Load = false;
        public byte Level
        {
            get { return Buffer[60]; }
            set { Buffer[60] = value; }
        }
        public string Name;
        public string Spouse;
        public SafeDictionary<uint, Member> Members;
        public List<Member> ListMember = new List<Member>();
        public SafeDictionary<uint, Guild> Ally, Enemy;
        public ArsenalPacket A_Packet;
        public Arsenal Arsenal;
        public uint Wins;
        public uint WinsWar;
        public uint Losts;
        public uint money_donation = 0;
        public uint honor_donation = 0;
        public uint pkp_donation = 0;
        public uint rose_donation = 0;
        public uint tuil_donation = 0;
        public uint orchid_donation = 0;
        public uint lilies_donation = 0;
        public string Bulletin;
        public Member Leader;
        private string leaderName;
        public string LeaderName
        {
            get { return leaderName; }
            set
            {
                leaderName = value;
                Writer.WriteString(value, 32, Buffer);
            }
        }
        public static Boolean CheckNameExist(String Name)
        {
            foreach (Guild guilds in Kernel.Guilds.Values)
            {
                if (guilds.Name == Name)
                    return true;
            }
            return false;
        }
        public bool Create(string name, Client.GameState c)
        {
            if (name.Length < 16)
            {
                Name = name;
                SilverFund = 500000;
                ad_rate = 50000;
                Level = 1;
                EnrollmentDate = Emulator.Core.Common.TimeGet(Emulator.Core.TimeType.Day);
                BulletinDate = EnrollmentDate;
                if (string.IsNullOrEmpty(Bulletin))
                    Bulletin = "This is a new guild!";
                Members.Add(Leader.ID, Leader);
                try
                {
                    Database.GuildTable.Create(this);
                }
                catch (Exception e)
                {
                    Server.SaveException(e);
                    return false;
                }
                Kernel.Guilds.Add(ID, this);
                Message message = null;
                Database.EntityTable.SaveGuildID(c, ID);
                message = new Message("Congratulations, " + leaderName + " has created the Guild " + name + " successfully!", System.Drawing.Color.White, Message.World);
                foreach (Client.GameState client in Kernel.GamePool.Values)
                {
                    client.Send(message);
                }
                return true;
            }
            return false;
        }
        public void AddMember(Client.GameState client)
        {
            Revora.Game.Features.GuildAdmission.Join(client,this);
        }

        public void SendMembers(Client.GameState client, ushort page)
        {
            ulong timernow = (ulong)DateTime.Now.Ticks;
            MemoryStream strm = new MemoryStream();
            BinaryWriter wtr = new BinaryWriter(strm);
            wtr.Write((ushort)0);
            wtr.Write((ushort)2102);
            wtr.Write((uint)0);
            wtr.Write((uint)page);
            int left = (int)MemberCount - page;
            if (left > 12) left = 12;
            if (left < 0) left = 0;
            wtr.Write((uint)left);
            int count = 0;
            int maxmem = page + 12;
            int minmem = page;
            List<Member> online = new List<Member>(250);
            List<Member> offline = new List<Member>(250);
            foreach (Member member in Members.Values)
            {
                if (member.IsOnline)
                    online.Add(member);
                else
                    offline.Add(member);
            }
            online.OrderByDescending((mem) => mem.Rank);
            var unite = online.Union<Member>(offline);
            byte[] name = null;
            foreach (Member member in unite)
            {
                if (count >= minmem && count < maxmem)
                {
                    wtr.Write((uint)0);
                    name = System.Text.Encoding.Default.GetBytes(member.Name);

                    for (int j = 0; j < 16; j++)
                    {
                        if (name.Length > j) wtr.Write(name[j]);
                        else wtr.Write((byte)0);
                    }
                    wtr.Write((uint)(member.IsOnline ? 1 : 0));
                    wtr.Write((uint)member.NobilityRank);
                    if (member.Gender == 3)
                        wtr.Write((uint)(member.Gender - 2));
                    else
                        wtr.Write((uint)(member.Gender + 1));
                    wtr.Write((uint)member.Level);
                    wtr.Write((uint)member.Rank);
                    wtr.Write((uint)0); // EXP
                    wtr.Write((uint)member.ArsenalDonation);
                    wtr.Write((uint)0);
                    wtr.Write((uint)0);
                    wtr.Write((uint)member.Class);
                    wtr.Write((uint)(((timernow - member.LastLogin) / 10000000)));
                    wtr.Write((uint)client.Entity.Mesh);
                }
                count++;
            }

            int packetlength = (int)strm.Length;
            strm.Position = 0;
            wtr.Write((ushort)packetlength);
            strm.Position = strm.Length;
            wtr.Write(Encoding.Default.GetBytes("TQServer"));
            strm.Position = 0;
            byte[] buf = new byte[strm.Length];
            strm.Read(buf, 0, buf.Length);
            wtr.Close();
            strm.Close();
            client.Send(buf);
        }
        //public void SendMembers(Client.GameState client, ushort page)
        //{
        //    ulong timernow = (ulong)DateTime.Now.Ticks;
        //    MemoryStream strm = new MemoryStream();
        //    BinaryWriter wtr = new BinaryWriter(strm);
        //    wtr.Write((ushort)0);
        //    wtr.Write((ushort)2102);
        //    wtr.Write((uint)0);
        //    wtr.Write((uint)page);
        //    int left = (int)MemberCount - page;
        //    if (left > 12)
        //        left = 12;
        //    if (left < 0)
        //        left = 0;
        //    wtr.Write((uint)left);
        //    int count = 0;
        //    int maxmem = page + 12;
        //    int minmem = page;
        //    List<Member> online = new List<Member>(250);
        //    List<Member> offline = new List<Member>(250);
        //    foreach (Member member in Members.Values)
        //    {
        //        if (member.IsOnline)
        //            online.Add(member);
        //        else
        //            offline.Add(member);
        //    }
        //    var unite = online.Union<Member>(offline);
        //    foreach (Member member in unite)
        //    {
        //        if (count >= minmem && count < maxmem)
        //        {
        //            for (int i = 0; i < 16; i++)
        //            {
        //                if (i < member.Name.Length)
        //                {
        //                    wtr.Write((byte)member.Name[i]);
        //                }
        //                else
        //                    wtr.Write((byte)0);
        //            }
        //            wtr.Write((uint)member.NobilityRank);
        //            if (member.Gender == 3) wtr.Write((uint)(member.Gender - 2));
        //            else wtr.Write((uint)(member.Gender + 1));
        //            wtr.Write((uint)member.Level);
        //            wtr.Write((uint)member.Rank);
        //            wtr.Write((uint)0);
        //            wtr.Write((uint)member.TotalDoantion);
        //            wtr.Write((uint)(member.IsOnline ? 1 : 0));
        //            wtr.Write((uint)0);
        //            wtr.Write((uint)member.Class);
        //            wtr.Write((uint)(((timernow - member.LastLogin) / 10000000))); // Offline time (in secoudes)
        //            //wtr.Write((uint)(GeneralData.UnixTimestamp - member.LastLogin));
        //            wtr.Write((uint)0);
        //        }
        //        count++;
        //    }
        //    int packetlength = (int)strm.Length;
        //    strm.Position = 0;
        //    wtr.Write((ushort)packetlength);
        //    strm.Position = strm.Length;
        //    wtr.Write(ASCIIEncoding.ASCII.GetBytes("TQServer"));
        //    strm.Position = 0;
        //    byte[] buf = new byte[strm.Length];
        //    strm.Read(buf, 0, buf.Length);
        //    wtr.Close();
        //    strm.Close();
        //    client.Send(buf);
        //}
        public void SendGuildMessage(Interfaces.IPacket message)
        {
            foreach (Member member in Members.Values)
            {
                if (member.IsOnline)
                {
                    member.Client.Send(message);
                }
            }
        }
        public Member GetMemberByName(string membername)
        {
            foreach (Member member in Members.Values)
            {
                if (member.Name == membername)
                {
                    return member;
                }
            }
            return null;
        }
        public void ExpelMember(string membername, bool ownquit)
        {
            lock(Revora.Game.Features.GuildLeadership.Gate(this)) {
            Member member = GetMemberByName(membername);
            if (member != null)
            {
                if (ownquit)
                    SendGuildMessage(new Message(member.Name + " has left the Guild.", System.Drawing.Color.Black, Message.Guild));
                else
                    SendGuildMessage(new Message(member.Name + " has been expelled from the clan.", System.Drawing.Color.Black, Message.Guild));
                uint uid = member.ID;

                if (member.IsOnline)
                {
                    GuildCommand command = new GuildCommand(true);
                    command.Type = GuildCommand.Disband;
                    command.dwParam = ID;
                    member.Client.Send(command);
                    member.Client.AsMember = null;
                    member.Client.Guild = null;
                    member.Client.Entity.GuildID = (ushort)0;
                    member.Client.Entity.GuildRank = (ushort)0;
                    member.Client.Screen.FullWipe();
                    member.Client.Screen.Reload(null);
                }
                else
                {
                    member.GuildID = 0;
                }
                Database.EntityTable.UpdateOffline(uid, "GuildRank", 0);
                Database.EntityTable.UpdateOffline(uid, "GuildID", 0);
                MemberCount--;
                Members.Remove(uid);
            }
            }
        }
        public void Disband()
        {
            lock(Revora.Game.Features.GuildLeadership.Gate(this)) {
            if (UnionID != 0)
            {
                Client.GameState leader;
                if(Leader!=null && Kernel.GamePool.TryGetValue(Leader.ID,out leader))leader.Send("Withdraw your guild from the union before dissolving it.");
                return;
            }
            var members = Members.Values.ToArray();
            foreach (Member member in members)
            {
                uint uid = member.ID;
                if (member.IsOnline)
                {
                    GuildCommand command = new GuildCommand(true);
                    command.Type = GuildCommand.Disband;
                    command.dwParam = ID;
                    member.Client.Entity.GuildID = 0;
                    member.Client.Entity.GuildRank = 0;
                    member.Client.Send(command);
                    member.Client.Screen.FullWipe();
                    member.Client.Screen.Reload(null);
                    member.Client.AsMember = null;
                    member.Client.Guild = null;
                    Message message = null;
                    message = new Message("The Guild " + Name + " has been deleted!", System.Drawing.Color.White, Message.World);
                    foreach (Client.GameState client in Kernel.GamePool.Values)
                    {
                        client.Send(message);
                    }
                }
                else
                {
                    member.GuildID = 0;
                }
                MemberCount--;
                Members.Remove(uid);
            }
            var ally_ = Ally.Values.ToArray();
            foreach (Guild ally in ally_)
            {
                RemoveAlly(ally.Name);
                ally.RemoveAlly(Name);
            }
            Database.GuildTable.Disband(this);
            // GamePool is keyed by character id. A guild id is not a session.
            }
        }
        public void CreateMembersRank()
        {
            lock (this)
            {
                //remove all ranks
                foreach (Member memb in Members.Values)
                {
                    if ((ushort)memb.Rank < 920)
                    {
                        if (RanksCounts[(ushort)memb.Rank] > 0)
                            RanksCounts[(ushort)memb.Rank]--;
                        memb.Rank = Enums.GuildMemberRank.Member;
                        RanksCounts[(ushort)memb.Rank]++;
                    }
                }
                Member[] Poll = null;
                //calculate manager`s
                const byte MaxMannager = 5;//0,1,2,3,4
                const byte MaxHonorManager = 2;//5,6,
                const byte MaxSupervisor = 2;//7,8,
                const byte MaxSteward = 4;//9,10,11,12
                const byte MaxArsFollower = 2;//13,14
                byte amount = 0;//8
                Poll = (from memb in Members.Values orderby memb.ArsenalDonation descending select memb).ToArray();
                for (byte x = 0; x < Poll.Length; x++)
                {
                    Member membru = Poll[x];
                    if (membru.Rank > Enums.GuildMemberRank.Manager)
                        continue;
                    if (amount < MaxMannager)
                    {
                        if (RanksCounts[(ushort)membru.Rank] > 0)
                            RanksCounts[(ushort)membru.Rank]--;
                        membru.Rank = Enums.GuildMemberRank.Manager;
                        RanksCounts[(ushort)membru.Rank]++;
                        amount++;
                    }
                    else if (amount < MaxMannager + MaxHonorManager)
                    {
                        if (RanksCounts[(ushort)membru.Rank] > 0)
                            RanksCounts[(ushort)membru.Rank]--;
                        membru.Rank = Enums.GuildMemberRank.HonoraryManager;
                        RanksCounts[(ushort)membru.Rank]++;
                        amount++;
                    }
                    else if (amount < MaxHonorManager + MaxMannager + MaxSupervisor)
                    {
                        if (RanksCounts[(ushort)membru.Rank] > 0)
                            RanksCounts[(ushort)membru.Rank]--;
                        membru.Rank = Enums.GuildMemberRank.Supervisor;
                        RanksCounts[(ushort)membru.Rank]++;
                        amount++;
                    }
                    else if (amount < MaxHonorManager + MaxMannager + MaxSupervisor + MaxSteward)
                    {
                        if (membru.Rank > Enums.GuildMemberRank.Steward)
                            continue;
                        if (RanksCounts[(ushort)membru.Rank] > 0)
                            RanksCounts[(ushort)membru.Rank]--;
                        membru.Rank = Enums.GuildMemberRank.Steward;
                        RanksCounts[(ushort)membru.Rank]++;
                        amount++;
                    }
                    else if (amount < MaxHonorManager + MaxMannager + MaxSupervisor + MaxSteward + MaxArsFollower)
                    {
                        if (membru.Rank > Enums.GuildMemberRank.ArsFollower)
                            continue;
                        if (RanksCounts[(ushort)membru.Rank] > 0)
                            RanksCounts[(ushort)membru.Rank]--;
                        membru.Rank = Enums.GuildMemberRank.ArsFollower;
                        RanksCounts[(ushort)membru.Rank]++;
                        amount++;
                    }
                    else
                        break;
                }
                RankArsenalDonations = Poll.ToArray();
                //calculate rank cps
                const byte MaxCPSupervisor = 3;//0,1,2
                const byte MaxCpAgent = 2;//3,4
                const byte MaxCpFollower = 2;//5,6
                amount = 0;//3
                Poll = (from memb in Members.Values orderby memb.ConquerPointDonation descending select memb).ToArray();
                for (byte x = 0; x < Poll.Length; x++)
                {
                    Member membru = Poll[x];
                    if (membru.Rank > Enums.GuildMemberRank.CPSupervisor)
                        continue;
                    if (amount < MaxCPSupervisor)
                    {
                        if (RanksCounts[(ushort)membru.Rank] > 0)
                            RanksCounts[(ushort)membru.Rank]--;
                        membru.Rank = Enums.GuildMemberRank.CPSupervisor;
                        RanksCounts[(ushort)membru.Rank]++;
                        amount++;
                    }
                    else if (amount < MaxCPSupervisor + MaxCpAgent)
                    {
                        if (membru.Rank > Enums.GuildMemberRank.CPAgent)
                            continue;
                        if (RanksCounts[(ushort)membru.Rank] > 0)
                            RanksCounts[(ushort)membru.Rank]--;
                        membru.Rank = Enums.GuildMemberRank.CPAgent;
                        RanksCounts[(ushort)membru.Rank]++;
                        amount++;
                    }
                    else if (amount < MaxCPSupervisor + MaxCpAgent + MaxCpFollower)
                    {
                        if (membru.Rank > Enums.GuildMemberRank.CPFollower)
                            continue;
                        if (RanksCounts[(ushort)membru.Rank] > 0)
                            RanksCounts[(ushort)membru.Rank]--;
                        membru.Rank = Enums.GuildMemberRank.CPFollower;
                        RanksCounts[(ushort)membru.Rank]++;
                        amount++;
                    }
                    else
                        break;
                }
                RankCPDonations = Poll.ToArray();
                //calculate pk ranks
                const byte MaxPkSupervisor = 3;//0,1,2
                const byte MaxPkAgent = 2;//3,4,
                const byte MaxPkFollower = 2;//5,6
                amount = 0;//3
                Poll = (from memb in Members.Values orderby memb.PkDonation descending select memb).ToArray();
                for (byte x = 0; x < Poll.Length; x++)
                {
                    Member membru = Poll[x];
                    if (membru.Rank > Enums.GuildMemberRank.PKSupervisor)
                        continue;
                    if (amount < MaxPkSupervisor)
                    {
                        if (RanksCounts[(ushort)membru.Rank] > 0)
                            RanksCounts[(ushort)membru.Rank]--;
                        membru.Rank = Enums.GuildMemberRank.PKSupervisor;
                        RanksCounts[(ushort)membru.Rank]++;
                        amount++;
                    }
                    else if (amount < MaxPkSupervisor + MaxPkAgent)
                    {
                        if (membru.Rank > Enums.GuildMemberRank.PKAgent)
                            continue;
                        if (RanksCounts[(ushort)membru.Rank] > 0)
                            RanksCounts[(ushort)membru.Rank]--;
                        membru.Rank = Enums.GuildMemberRank.PKAgent;
                        RanksCounts[(ushort)membru.Rank]++;
                        amount++;
                    }
                    else if (amount < MaxPkSupervisor + MaxPkAgent + MaxPkFollower)
                    {
                        if (membru.Rank > Enums.GuildMemberRank.PKFollower)
                            continue;
                        if (RanksCounts[(ushort)membru.Rank] > 0)
                            RanksCounts[(ushort)membru.Rank]--;
                        membru.Rank = Enums.GuildMemberRank.PKFollower;
                        RanksCounts[(ushort)membru.Rank]++;
                        amount++;
                    }
                    else
                        break;
                }
                RankPkDonations = Poll.ToArray();
                //calculate RoseSupervisor
                const byte MaxRoseSupervisor = 3;//0,1,2
                const byte MaxRoseAgent = 2;//3,4
                const byte MaxRoseFollower = 2;//5,6
                amount = 0;//3
                Poll = (from memb in Members.Values orderby memb.Rouses descending select memb).ToArray();
                for (byte x = 0; x < Poll.Length; x++)
                {
                    Member membru = Poll[x];
                    if (membru.Rank > Enums.GuildMemberRank.RoseSupervisor)
                        continue;
                    if (amount < MaxRoseSupervisor)
                    {
                        if (RanksCounts[(ushort)membru.Rank] > 0)
                            RanksCounts[(ushort)membru.Rank]--;
                        membru.Rank = Enums.GuildMemberRank.RoseSupervisor;
                        RanksCounts[(ushort)membru.Rank]++;
                        amount++;
                    }
                    else if (amount < MaxRoseSupervisor + MaxRoseAgent)
                    {
                        if (membru.Rank > Enums.GuildMemberRank.RoseAgent)
                            continue;
                        if (RanksCounts[(ushort)membru.Rank] > 0)
                            RanksCounts[(ushort)membru.Rank]--;
                        membru.Rank = Enums.GuildMemberRank.RoseAgent;
                        RanksCounts[(ushort)membru.Rank]++;
                        amount++;
                    }
                    else if (amount < MaxRoseSupervisor + MaxRoseAgent + MaxRoseFollower)
                    {
                        if (membru.Rank > Enums.GuildMemberRank.RoseFollower)
                            continue;
                        if (RanksCounts[(ushort)membru.Rank] > 0)
                            RanksCounts[(ushort)membru.Rank]--;
                        membru.Rank = Enums.GuildMemberRank.RoseFollower;
                        RanksCounts[(ushort)membru.Rank]++;
                        amount++;
                    }
                    else
                        break;
                }
                RankRosseDonations = Poll.ToArray();
                //calculate LilySupervisor
                const byte MaxLilySupervisor = 3;
                const byte MaxLilyAgent = 2;
                const byte MaxLilyFollower = 2;
                amount = 0;//3
                Poll = (from memb in Members.Values orderby memb.Lilies descending select memb).ToArray();
                for (byte x = 0; x < Poll.Length; x++)
                {
                    Member membru = Poll[x];
                    if (membru.Rank > Enums.GuildMemberRank.LilySupervisor)
                        continue;
                    if (amount < MaxLilySupervisor)
                    {
                        if (RanksCounts[(ushort)membru.Rank] > 0)
                            RanksCounts[(ushort)membru.Rank]--;
                        membru.Rank = Enums.GuildMemberRank.LilySupervisor;
                        RanksCounts[(ushort)membru.Rank]++;
                        amount++;
                    }
                    else if (amount < MaxLilySupervisor + MaxLilyAgent)
                    {
                        if (membru.Rank > Enums.GuildMemberRank.LilyAgent)
                            continue;
                        if (RanksCounts[(ushort)membru.Rank] > 0)
                            RanksCounts[(ushort)membru.Rank]--;
                        membru.Rank = Enums.GuildMemberRank.LilyAgent;
                        RanksCounts[(ushort)membru.Rank]++;
                        amount++;
                    }
                    else if (amount < MaxLilySupervisor + MaxLilyAgent + MaxLilyFollower)
                    {
                        if (membru.Rank > Enums.GuildMemberRank.LilyFollower)
                            continue;
                        if (RanksCounts[(ushort)membru.Rank] > 0)
                            RanksCounts[(ushort)membru.Rank]--;
                        membru.Rank = Enums.GuildMemberRank.LilyFollower;
                        RanksCounts[(ushort)membru.Rank]++;
                        amount++;
                    }
                    else
                        break;
                }
                RankLiliesDonations = Poll.ToArray();
                //calculate TulipAgent
                const byte MaxTSupervisor = 3;
                const byte MaxTulipAgent = 2;
                const byte MaxTulupFollower = 2;
                amount = 0;//3
                Poll = (from memb in Members.Values orderby memb.Tulips descending select memb).ToArray();
                for (byte x = 0; x < Poll.Length; x++)
                {
                    Member membru = Poll[x];
                    if (membru.Rank > Enums.GuildMemberRank.TSupervisor)
                        continue;
                    if (amount < MaxTSupervisor)
                    {
                        if (RanksCounts[(ushort)membru.Rank] > 0)
                            RanksCounts[(ushort)membru.Rank]--;
                        membru.Rank = Enums.GuildMemberRank.TSupervisor;
                        RanksCounts[(ushort)membru.Rank]++;
                        amount++;
                    }
                    else if (amount < MaxTSupervisor + MaxTulipAgent)
                    {
                        if (membru.Rank > Enums.GuildMemberRank.TulipAgent)
                            continue;
                        if (RanksCounts[(ushort)membru.Rank] > 0)
                            RanksCounts[(ushort)membru.Rank]--;
                        membru.Rank = Enums.GuildMemberRank.TulipAgent;
                        RanksCounts[(ushort)membru.Rank]++;
                        amount++;
                    }
                    else if (amount < MaxTSupervisor + MaxTulipAgent + MaxTulupFollower)
                    {
                        if (membru.Rank > Enums.GuildMemberRank.TulipFollower)
                            continue;
                        if (RanksCounts[(ushort)membru.Rank] > 0)
                            RanksCounts[(ushort)membru.Rank]--;
                        membru.Rank = Enums.GuildMemberRank.TulipFollower;
                        RanksCounts[(ushort)membru.Rank]++;
                        amount++;
                    }
                    else
                        break;
                }
                RankTulipsDonations = Poll.ToArray();
                // calculate OrchidAgent
                const byte MaxOSupervisor = 3;
                const byte MaxOrchidAgent = 2;
                const byte MaxOrchidFollower = 2;
                amount = 0;//3
                Poll = (from memb in Members.Values orderby memb.Tulips descending select memb).ToArray();
                for (byte x = 0; x < Poll.Length; x++)
                {
                    Member membru = Poll[x];
                    if (membru.Rank > Enums.GuildMemberRank.OSupervisor)
                        continue;
                    if (amount < MaxOSupervisor)
                    {
                        if (RanksCounts[(ushort)membru.Rank] > 0)
                            RanksCounts[(ushort)membru.Rank]--;
                        membru.Rank = Enums.GuildMemberRank.OSupervisor;
                        RanksCounts[(ushort)membru.Rank]++;
                        amount++;
                    }
                    else if (amount < MaxOSupervisor + MaxOrchidAgent)
                    {
                        if (membru.Rank > Enums.GuildMemberRank.OrchidAgent)
                            continue;
                        if (RanksCounts[(ushort)membru.Rank] > 0)
                            RanksCounts[(ushort)membru.Rank]--;
                        membru.Rank = Enums.GuildMemberRank.OrchidAgent;
                        RanksCounts[(ushort)membru.Rank]++;
                        amount++;
                    }
                    else if (amount < MaxOSupervisor + MaxOrchidFollower + MaxOrchidAgent)
                    {
                        if (membru.Rank > Enums.GuildMemberRank.OrchidFollower)
                            continue;
                        if (RanksCounts[(ushort)membru.Rank] > 0)
                            RanksCounts[(ushort)membru.Rank]--;
                        membru.Rank = Enums.GuildMemberRank.OrchidFollower;
                        RanksCounts[(ushort)membru.Rank]++;
                        amount++;
                    }
                    else
                        break;
                }
                RankOrchidsDonations = Poll.ToArray();
                Poll = (from memb in Members.Values orderby memb.TotalDonation descending select memb).ToArray();
                const byte HDeputyLeader = 2;//0,1
                const byte MaxHonorarySteward = 2;//2,3
                amount = 0;//20
                for (byte x = 0; x < Poll.Length; x++)
                {
                    Member membru = Poll[x];
                    if (membru.Rank > Enums.GuildMemberRank.HDeputyLeader)
                        continue;
                    if (amount < HDeputyLeader)
                    {
                        if (RanksCounts[(ushort)membru.Rank] > 0)
                            RanksCounts[(ushort)membru.Rank]--;
                        membru.Rank = Enums.GuildMemberRank.HDeputyLeader;
                        RanksCounts[(ushort)membru.Rank]++;
                        amount++;
                    }
                    else if (amount < HDeputyLeader + MaxHonorarySteward)
                    {
                        if (membru.Rank > Enums.GuildMemberRank.HonorarySteward)
                            continue;
                        if (RanksCounts[(ushort)membru.Rank] > 0)
                            RanksCounts[(ushort)membru.Rank]--;
                        membru.Rank = Enums.GuildMemberRank.HonorarySteward;
                        RanksCounts[(ushort)membru.Rank]++;
                        amount++;
                    }
                    else
                        break;
                }
                RankTotalDonations = Poll.ToArray();
                const byte SSupervisor = 5;//0,1,2,3
                const byte MaxSilverAgent = 2;//4,5
                const byte MaxSilverFollowr = 2;//6,7
                amount = 0;//20
                Poll = (from memb in Members.Values
                        orderby memb.SilverDonation descending
                        select memb).ToArray();
                for (byte x = 0; x < Poll.Length; x++)
                {
                    Member membru = Poll[x];
                    if (membru.Rank > Enums.GuildMemberRank.SSupervisor)
                        continue;
                    if (amount < SSupervisor)
                    {
                        if (RanksCounts[(ushort)membru.Rank] > 0)
                            RanksCounts[(ushort)membru.Rank]--;
                        membru.Rank = Enums.GuildMemberRank.SSupervisor;
                        RanksCounts[(ushort)membru.Rank]++;
                        amount++;
                    }
                    else if (amount < SSupervisor + MaxSilverAgent)
                    {
                        if (membru.Rank > Enums.GuildMemberRank.SilverAgent)
                            continue;
                        if (RanksCounts[(ushort)membru.Rank] > 0)
                            RanksCounts[(ushort)membru.Rank]--;
                        membru.Rank = Enums.GuildMemberRank.SilverAgent;
                        RanksCounts[(ushort)membru.Rank]++;
                        amount++;
                    }
                    else if (amount < SSupervisor + MaxSilverAgent + MaxSilverFollowr)
                    {
                        if (membru.Rank > Enums.GuildMemberRank.SilverFollower)
                            continue;
                        if (RanksCounts[(ushort)membru.Rank] > 0)
                            RanksCounts[(ushort)membru.Rank]--;
                        membru.Rank = Enums.GuildMemberRank.SilverFollower;
                        RanksCounts[(ushort)membru.Rank]++;
                        amount++;
                    }
                    else
                        break;
                }
                RankSilversDonations = Poll.ToArray();
                const byte GSupervisor = 3;//0,1,2
                const byte MaxGAgent = 2;//3,4
                const byte MaxGFollower = 2;//5,6
                amount = 0;//20
                Poll = (from memb in Members.Values orderby memb.VirtutePointes descending select memb).ToArray();
                for (byte x = 0; x < Poll.Length; x++)
                {
                    Member membru = Poll[x];
                    if (membru.Rank > Enums.GuildMemberRank.GSupervisor)
                        continue;
                    if (amount < GSupervisor)
                    {
                        if (RanksCounts[(ushort)membru.Rank] > 0)
                            RanksCounts[(ushort)membru.Rank]--;
                        membru.Rank = Enums.GuildMemberRank.GSupervisor;
                        RanksCounts[(ushort)membru.Rank]++;
                        amount++;
                    }
                    else if (amount < GSupervisor + MaxGAgent)
                    {
                        if (membru.Rank > Enums.GuildMemberRank.GuideAgent)
                            continue;
                        if (RanksCounts[(ushort)membru.Rank] > 0)
                            RanksCounts[(ushort)membru.Rank]--;
                        membru.Rank = Enums.GuildMemberRank.GuideAgent;
                        RanksCounts[(ushort)membru.Rank]++;
                        amount++;
                    }
                    else if (amount < GSupervisor + MaxGAgent + MaxGFollower)
                    {
                        if (membru.Rank > Enums.GuildMemberRank.GuideFollower)
                            continue;
                        if (RanksCounts[(ushort)membru.Rank] > 0)
                            RanksCounts[(ushort)membru.Rank]--;
                        membru.Rank = Enums.GuildMemberRank.GuideFollower;
                        RanksCounts[(ushort)membru.Rank]++;
                        amount++;
                    }
                    else
                        break;
                }
                RankGuideDonations = Poll.ToArray();
            }
        }
        public void CalculateCTFRank(bool create_players_reward = false)
        {
            var rank_ctf = Members.Values.Where(p => p.Exploits != 0).OrderByDescending(p => p.Exploits).ToArray();
            for (ushort x = 0; x < rank_ctf.Length; x++)
            {
                var a_mem = rank_ctf[x];
                var mem = Members[a_mem.ID];
                mem.ExploitsRank = (uint)(x + 1);
                if (create_players_reward)
                {
                    uint[] RewardCTF = CalculateRewardCTF(mem.ExploitsRank);
                    mem.CTFSilverReward = RewardCTF[0];
                    mem.CTFCpsReward = RewardCTF[1];
                }
            }
        }
        private uint[] CalculateRewardCTF(uint Rank)
        {
            uint[] rew = new uint[2];
            rew[0] = (CTFdonationSilverold / (Rank + 1));
            rew[1] = (CTFdonationCPsold / (Rank + 1));
            return rew;
        }
        public bool GetMember(string name, out Guild.Member getmem)
        {
            foreach (Guild.Member current in this.Members.Values)
            {
                if (current.Name == name)
                {
                    getmem = current;
                    return true;
                }
            }
            getmem = null;
            return false;
        }
        public uint UnionID;
        public void AddAlly(string name)
        {
            foreach (Guild guild in Kernel.Guilds.Values)
            {
                if (guild.Name == name)
                {
                    if (Enemy.ContainsKey(guild.ID))
                        RemoveEnemy(guild.Name);
                    if (!Ally.ContainsKey(guild.ID))
                    {
                        Database.GuildTable.AddAlly(this, guild.ID);
                        Ally.Add(guild.ID, guild);
                        _String stringPacket = new _String(true);
                        stringPacket.UID = guild.ID;
                        stringPacket.Type = _String.GuildAllies;
                        stringPacket.Texts.Add(guild.Name + " " + guild.LeaderName + " " + guild.Level + " " + guild.MemberCount);
                        SendGuildMessage(stringPacket);
                    }
                    return;
                }
            }
        }
        public void RemoveAlly(string name)
        {
            foreach (Guild guild in Ally.Values)
            {
                if (guild.Name == name)
                {
                    GuildCommand cmd = new GuildCommand(true);
                    cmd.Type = GuildCommand.Neutral1;
                    cmd.dwParam = guild.ID;
                    SendGuildMessage(cmd);
                    Database.GuildTable.RemoveAlly(this, guild.ID);
                    Ally.Remove(guild.ID);
                    return;
                }
            }
        }
        public void AddEnemy(string name)
        {
            foreach (Guild guild in Kernel.Guilds.Values)
            {
                if (guild.Name == name)
                {
                    if (Ally.ContainsKey(guild.ID))
                    {
                        RemoveAlly(guild.Name);
                        guild.RemoveAlly(Name);
                    }
                    Enemy.Add(guild.ID, guild);
                    _String stringPacket = new _String(true);
                    stringPacket.UID = guild.ID;
                    stringPacket.Type = _String.GuildEnemies;
                    stringPacket.Texts.Add(guild.Name + " " + guild.LeaderName + " " + guild.Level + " " + guild.MemberCount);
                    SendGuildMessage(stringPacket);
                    SendGuildMessage(stringPacket);
                    Database.GuildTable.AddEnemy(this, guild.ID);
                    return;
                }
            }
        }
        public void RemoveEnemy(string name)
        {
            foreach (Guild guild in Enemy.Values)
            {
                if (guild.Name == name)
                {
                    GuildCommand cmd = new GuildCommand(true);
                    cmd.Type = GuildCommand.Neutral2;
                    cmd.dwParam = guild.ID;
                    SendGuildMessage(cmd);
                    SendGuildMessage(cmd);
                    Database.GuildTable.RemoveEnemy(this, guild.ID);
                    Enemy.Remove(guild.ID);
                    return;
                }
            }
        }
        public void SendName(Client.GameState client)
        {
            _String stringPacket = new _String(true);
            stringPacket.UID = ID;
            stringPacket.Type = _String.GuildName;
            stringPacket.Texts.Add(Name + " " + LeaderName + " 0 " + MemberCount);
            client.Send(stringPacket);
        }
        public void SendGuild(Client.GameState client)
        {
            if (Members.ContainsKey(client.Entity.UID))
            {
                if (Bulletin == null)
                    Bulletin = "This is a new guild!";
                client.Send(new GuildCommand((uint)Bulletin.Length) { Type = GuildCommand.Bulletin, dwParam = BulletinDate, BulletinTime = Bulletin });
                Writer.WriteUInt32(uint.Parse(DateTime.Now.ToString("yyyymmdd")), 67, Buffer);
                Writer.WriteUInt32(client.AsMember.TotalDonation, 8, Buffer);
                Writer.WriteUInt32((ushort)client.AsMember.Rank, 28, Buffer);
                Writer.WriteString(LeaderName, 32, Buffer);
                Writer.WriteByte(Level, 60, Buffer);
                Writer.WriteUInt32((uint)EnrollmentDate, 67, Buffer);
                Writer.WriteUInt32(LevelRequirement, 48, Buffer);
                Writer.WriteUInt32(RebornRequirement, 52, Buffer);
                Writer.WriteUInt32(ClassRequirement, 56, Buffer);
                client.Send(Buffer);
                client.AsMember.Guild.SendAllyAndEnemy(client);
            }
        }
        public void SendAllyAndEnemy(Client.GameState client)
        {
            foreach (Guild guild in Enemy.Values)
            {
                _String stringPacket = new _String(true);
                stringPacket.UID = guild.ID;
                stringPacket.Type = _String.GuildEnemies;
                stringPacket.Texts.Add(guild.Name + " " + guild.LeaderName + " " + guild.Level + " " + guild.MemberCount);
                client.Send(stringPacket);
                client.Send(stringPacket);
            }
            foreach (Guild guild in Ally.Values)
            {
                _String stringPacket = new _String(true);
                stringPacket.UID = guild.ID;
                stringPacket.Type = _String.GuildAllies;
                stringPacket.Texts.Add(guild.Name + " " + guild.LeaderName + " " + guild.Level + " " + guild.MemberCount);
                client.Send(stringPacket);
                client.Send(stringPacket);
            }
        }
        public static bool ValidName(string Name)
        {
            if (Name.Length < 4 && Name.Length > 15) return false;
            else if (Name.IndexOfAny(new char[20] { ' ', '#', '%', '^', '&', '*', '(', ')', ';', ':', '\'', '\"', '/', '\\', ',', '.', '{', '}', '[', ']' }) > 0) return false;
            else return true;
        }
        #region Time Guild
        public uint EnrollmentDate;
        public uint BulletinDate;
        public void CreateBuletinTime(uint Time = 0)
        {
            if (Time == 0)
            {
                var timers = DateTime.Now;
                Time = GetTime((uint)timers.Year, (uint)timers.Month, (uint)timers.Day);
                Database.GuildTable.UpdateBulletin(this);
            }
            BulletinDate = Time;
        }
        public uint GetTime(uint year, uint month, uint day)
        {
            uint Timer = year * 10000 + month * 100 + day;
            return Timer;
        }
        #endregion
    }
}
