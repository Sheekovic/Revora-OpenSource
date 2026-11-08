// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Linq;
using System.Text;
using System.Drawing;
using Revora.Client;
using Revora.Network;
using Revora.Database;
using Revora.Interfaces;
using System.Collections.Generic;
using Revora.Network.GamePackets;
using Revora.Game.Features.Tournaments;

namespace Revora
{
    public unsafe class Clan : Writer, IPacket
    {
        private Byte[] mData;
        private Int32 Position = 17;
        private UInt32 mLeader, mFund, mAllyRequest;
        private String mName, mInfo, mAnnouncement;
        private Byte mLevel, mBPTower;
        public Client.GameState client;
        public bool
        TwinCityClan = false,
        WindPlainClan = false,
        DesertCityClan = false,
        DesertClan = false,
        BirdCityClan = false,
        BirdIslandClan = false,
        ApeCityClan = false,
        LoveCanyonClan = false,
        PhoenixCastleClan = false,
        MapleForestClan = false,
        Claimed = false,
        ClaimedExp = false;

        private System.Collections.Concurrent.ConcurrentDictionary<UInt32, ClanMember> mMembers;
        private System.Collections.Concurrent.ConcurrentDictionary<UInt32, Clan> mAllies, mEnemies;
        public Clan(UInt32 leaderid, UInt32 clanid, String clanname, String leadername)
        {
            LeaderId = leaderid;

            mMembers = new System.Collections.Concurrent.ConcurrentDictionary<UInt32, ClanMember>();
            mAllies = new System.Collections.Concurrent.ConcurrentDictionary<UInt32, Clan>();
            mEnemies = new System.Collections.Concurrent.ConcurrentDictionary<UInt32, Clan>();

            mData = new byte[141 + (Byte)(clanname.Length + leadername.Length) + 8];
            WriteUInt16((UInt16)(mData.Length - 8), 0, mData);
            WriteUInt16((UInt16)1312, 2, mData);

            ID = clanid;
            Name = clanname;
        }
        public Clan()
        {
            mData = new byte[149];
            WriteUInt16((UInt16)(mData.Length - 8), 0, mData);
            WriteUInt16((UInt16)1312, 2, mData);
        }
        public UInt32 LeaderId
        {
            get { return mLeader; }
            set { mLeader = value; }
        }
        public Types Type
        {
            get { return (Types)BitConverter.ToUInt32(mData, 4); }
            set { WriteByte((Byte)value, 4, mData); }
        }
        public UInt32 ID
        {
            get { return BitConverter.ToUInt32(mData, 8); }
            set { WriteUInt32((UInt32)value, 8, mData); }
        }
        public Byte Offset16
        {
            get { return mData[16]; }
            set { mData[16] = value; }
        }
        public Byte Offset17
        {
            get { return mData[17]; }
            set { mData[17] = value; }
        }
        public String Offset18String
        {
            get { return Encoding.Default.GetString(mData, 18, mData[17]).Trim(new Char[] { '\0' }); }
            set { WriteString(value, 18, mData); }
        }
        public String Name
        {
            get { return mName; }
            set { mName = value; }
        }
        private string leaderName;
        public string LeaderName
        {
            get { return leaderName; }
            set
            {
                leaderName = value;
                Writer.WriteString(value, 32, mData);
            }
        }
        public UInt32 Fund
        {
            get { return mFund; }
            set { mFund = value; }
        }
        public Byte Level
        {
            get { return mLevel; }
            set { mLevel = value; }
        }
        public Byte BPTower
        {
            get { return mBPTower; }
            set { mBPTower = value; }
        }
        public String Announcement
        {
            get { return mAnnouncement; }
            set { mAnnouncement = value; }
        }
        public String Info
        {
            get { return mInfo; }
            set { mInfo = value; }
        }
        public UInt32 AllyRequest
        {
            get { return mAllyRequest; }
            set { mAllyRequest = value; }
        }
        public void Build(GameState c, Types type)
        {
            this.Type = type;
            switch (type)
            {
                case Types.Info:
                    {
                        ClanMember member;
                        if (Members.TryGetValue(c.Entity.UID, out member))
                        {
                            Info = String.Format("{0} {1} {2} {3} {4} {5} {6} {7} {8} {9} {10} {11}"
                                , ID, mMembers.Count(), 1, Fund, Level, Convert.ToByte(c.Entity.ClanRank), 2, BPTower, 3, 4, 5, member.Donation);

                            Offset16 = 6;

                            WriteStringWithLength(Info, Position, mData);
                            Position += Info.Length;
                            Position++;

                            WriteStringWithLength(Name, Position, mData);
                            Position += Name.Length;
                            Position++;

                            WriteStringWithLength(LeaderName, Position, mData);
                            Position += LeaderName.Length;
                            Position++;

                            String OwnedCity = "0 0 0 0 0 0 0";
                            WriteStringWithLength(OwnedCity, Position, mData);

                            ClanWar.ClientWar war;
                            if (ClanWar.GetMyWar(ID, out war))
                            {
                                string domination_map = war.DominationMap;
                                Position += OwnedCity.Length;
                                Position++;
                                WriteStringWithLength(domination_map, Position, mData);

                                string CurentMap = war.CurentMap;
                                Position += domination_map.Length;
                                Position++;
                                WriteStringWithLength(CurentMap, Position, mData);
                            }
                            Position = 17;
                        }
                        break;
                    }
                case Types.MyClan:
                    {
                        string datainfo = "0 0 0 0 0 0 0";
                        ClanWar.ClientWar war;
                        if (ClanWar.GetMyWar(ID, out war))
                        {
                            datainfo = "1 " + war.OccupationDays + " "
                                + war.Reward + " " + war.NextReward + " 0 0 0";
                        }
                        Offset16 = 1;
                        Offset17 = (byte)datainfo.Length;
                        WriteString(datainfo, 18, mData);
                        break;
                    }
            }
        }
        private static void UpdateData(Client.GameState client, string column, object value)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE))
                cmd.Update("clans").Set(column, value).Where("identifier", client.Entity.ClanId)
                    .Execute();
        }
        public void Save(Client.GameState client, String row, UInt16 value)
        {
            UpdateData(client, row, value);
        }
        public void Save(Client.GameState client, String row, Byte value)
        {
            UpdateData(client, row, value);
        }
        public void Save(Client.GameState client, String row, String value)
        {
            UpdateData(client, row, value);
        }
        public void Save(Client.GameState client, String row, UInt32 value)
        {
            UpdateData(client, row, value);
        }
        public UInt32 GetClanId(String name)
        {
            lock (Kernel.Clans)
            {
                foreach (Clan clans in Kernel.Clans.Values)
                {
                    if (clans.Name == name)
                        return clans.ID;
                }
            }
            return 0;
        }
        public System.Collections.Concurrent.ConcurrentDictionary<UInt32, ClanMember> Members
        {
            get { return this.mMembers; }
        }
        public System.Collections.Concurrent.ConcurrentDictionary<UInt32, Clan> Allies
        {
            get { return this.mAllies; }
        }
        public System.Collections.Concurrent.ConcurrentDictionary<UInt32, Clan> Enemies
        {
            get { return this.mEnemies; }
        }
        public void InfoToMembers()
        {
            GameState mem;
            foreach (ClanMember member in this.Members.Values)
            {
                if (Kernel.GamePool.TryGetValue(member.Identifier, out mem))
                {
                    mem.Entity.GetClan.Build(mem, Types.Info);
                    mem.Send(mem.Entity.GetClan.ToArray());
                    mem.Send(new ClanMembers(mem.Entity.GetClan).ToArray());
                }
            }
        }
        public void SendMessage(IPacket packet)
        {
            GameState mem;
            foreach (ClanMember member in this.Members.Values)
            {
                if (Kernel.GamePool.TryGetValue(member.Identifier, out mem))
                    mem.Send(packet);
            }
        }
        public static UInt32 NextClanId
        {
            get
            {
                UInt32 start = 600;
                while (Kernel.Clans.ContainsKey(start))
                    start++;
                return start;
            }
        }
        public static Boolean ValidName(String name)
        {
            if (name.Length < 1 || name.Length > 35) return false;
            foreach (Clan clans in Kernel.Clans.Values)
                if (clans.Name == name)
                    return false;
            return true;
        }
        public void AddRelation(UInt32 Relative, ClanRelations.RelationTypes type)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.INSERT).Insert("clanrelation"))
                cmd.Insert("id", ID).Insert("clanid", ID).Insert("associatedid", Relative).Insert("type", (byte)type).Execute();
        }
        public void DeleteRelation(UInt32 Relative, ClanRelations.RelationTypes type)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.DELETE))
                cmd.Delete("clanrelation", "clanid", ID).And("associatedid", Relative)
                    .And("type", (byte)type).Execute();
        }
        public void Join(GameState c)
        {
            c.Entity.ClanId = ID;
            if (c.Entity.Spouse == this.leaderName)
            {
                c.Entity.ClanRank = Clan.Ranks.Spouse;
            }
            else
            {
                c.Entity.ClanRank = Clan.Ranks.Member;
            }
            c.Entity.ClanName = Name;
            Database.EntityTable.UpdateClanID(c);
            Database.EntityTable.UpdateClanRank(c);

            Members.Add(c.Entity.UID, new ClanMember()
            {
                Class = c.Entity.Class,
                Donation = 0,
                Identifier = c.Entity.UID,
                Level = c.Entity.Level,
                Name = c.Entity.Name,
                Rank = c.Entity.ClanRank
            });

            Build(c, Types.Info);
            c.Send(this);

            c.Entity.Teleport(c.Entity.MapID, c.Entity.X, c.Entity.Y);

            SendMessage(new Message(System.String.Format("{0} Has Joined the Clan!", c.Entity.Name), Color.Red, Message.Clan));
        }
        public static bool CreateClan(GameState c, String cname)
        {
            return Game.Features.ClanFounding.Create(c, cname);
        }
        public static void LoadClans()
        {
            Dictionary<uint, Dictionary<uint, ClanMember>> dict = new Dictionary<uint, Dictionary<uint, ClanMember>>();
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("entities").Where("clanid", 0, true))
            using (var reader = cmd.CreateReader())
            {
                while (reader.Read())
                {
                    uint clanId = reader.ReadUInt32("clanid");
                    ClanMember mem = new ClanMember()
                    {
                        UID = reader.ReadUInt32("Uid"),
                        Identifier = reader.ReadUInt32("Uid"),
                        Class = reader.ReadByte("Class"),
                        Donation = reader.ReadUInt32("ClanDonation"),
                        Level = reader.ReadByte("Level"),
                        Name = reader.ReadString("Name"),
                        Rank = (Ranks)reader.ReadByte("ClanRank"),
                    };
                    if (!dict.ContainsKey(clanId)) dict.Add(clanId, new Dictionary<uint, ClanMember>());
                    dict[clanId].Add(mem.UID, mem);
                }
            }
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("clans"))
            using (var reader = cmd.CreateReader())
            {
                while (reader.Read())
                {
                    UInt32 HeroId = reader.ReadUInt32("LeaderId");
                    Clan c = new Clan(HeroId, reader.ReadUInt32("Identifier"), reader.ReadString("Name"), reader.ReadString("LeaderName"));
                    {
                        c.Announcement = reader.ReadString("Announcement");
                        c.BPTower = reader.ReadByte("BPTower");
                        c.ID = reader.ReadUInt32("Identifier");
                        c.Fund = reader.ReadUInt32("Fund");
                        c.LeaderId = reader.ReadUInt32("LeaderId");
                        c.Level = reader.ReadByte("Level");
                        c.LeaderName = reader.ReadString("LeaderName");
                        c.Claimed = reader.ReadBoolean("Claimed");
                    }
                    Kernel.Clans.Add(c.ID, c);
                    if (dict.ContainsKey(c.ID))
                        c.mMembers = new System.Collections.Concurrent.ConcurrentDictionary<uint, ClanMember>(dict[c.ID]);
                    else
                        c.mMembers = new System.Collections.Concurrent.ConcurrentDictionary<uint, ClanMember>();
                }
            }
            foreach (Clan c in Kernel.Clans.Values)
            {
                c.LoadAssociates();
            }
        }
        public static void DisbandClan(GameState c)
        {
            Game.Features.ClanDisbanding.Confirm(c);
        }
        public static void SaveClan(Clan clan)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE))
                cmd.Update("Clans").Set("Fund", clan.Fund).Set("Level", clan.Level).Set("BPTower", clan.BPTower)
                    .Set("Announcement", clan.Announcement).Set("LeaderName", clan.leaderName).Set("Claimed", clan.Claimed).Where("Identifier", clan.ID).Execute();
        }
        private void LoadAssociates()
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("clanrelation").Where("clanid", this.ID))
            using (var reader = cmd.CreateReader())
            {
                while (reader.Read())
                {
                    UInt32 AssociateId = reader.ReadUInt32("AssociatedId");
                    ClanRelations.RelationTypes Type = (ClanRelations.RelationTypes)(reader.ReadUInt32("Type"));
                    Clan c;
                    if (Kernel.Clans.TryGetValue(AssociateId, out c))
                    {
                        if (Type == ClanRelations.RelationTypes.Allies)
                            this.Allies.Add(AssociateId, c);
                        else
                            this.Enemies.Add(AssociateId, c);
                    }
                }
            }
        }
        public enum Types : byte
        {
            Info = 1,
            Members = 4,
            Recruit = 9,
            AcceptRecruit = 10,
            Join = 11,
            AcceptJoinRequest = 12,
            AddEnemy = 14,
            DeleteEnemy = 15,
            AddAlly = 17,
            AcceptAlliance = 18,
            DeleteAlly = 20,
            TransferLeader = 21,
            Kick = 22,
            Quit = 23,
            Announce = 24,
            SetAnnouncement = 25,
            Dedicate = 26,
            MyClan = 29
        }
        public enum Ranks : ushort
        {
            ClanLeader = 100,
            Spouse = 11,
            Member = 10,
            None = 0
        }
        public void Send(GameState client)
        {
            client.Send(mData);
        }
        public byte[] ToArray()
        {
            return mData;
        }
        public void Deserialize(byte[] buffer)
        {
            mData = buffer;
        }
        public void SendClanShareBP(uint source, uint share, GameState client)
        {
            // The entity already owns the full-width scalar status projection.
            if (client != null && client.Entity != null) client.Entity.ClanSharedBp = share;
        }
        internal static readonly object RelationshipSync = new object();
        public static void Process(byte[] packet, GameState client)
        {
            if (packet != null && packet.Length >= 5 && (packet[4] == 14 || packet[4] == 15 || packet[4] == 17 || packet[4] == 18 || packet[4] == 20))
            { lock (RelationshipSync) ProcessCore(packet, client); }
            else ProcessCore(packet, client);
        }
        private static void ProcessCore(byte[] packet, GameState client)
        {
            if (Game.Features.ClanMembership.Handle(packet, client)) return;
            if (Game.Features.ClanDepartures.Handle(packet, client)) return;
            if (Game.Features.ClanLeadership.Handle(packet, client)) return;
            Clan clanp = new Clan();
            clanp.Deserialize(packet);
            switch (clanp.Type)
            {
                case Clan.Types.Announce:
                    {
                        Clan clan = client.Entity.GetClan;
                        if (clan != null)
                        {
                            clan.Build(client, Clan.Types.Info);
                            client.Send(clan);
                            clan.Build(client, Clan.Types.MyClan);
                            client.Send(clan);
                            client.Send(new ClanAnnouncement(clan));
                        }
                        break;
                    }
                case Clan.Types.Kick:
                    {
                        var clan = client.Entity.GetClan;
                        var kickedmem = Server.GamePool.SingleOrDefault(x => x.Entity.Name == clanp.Offset18String);
                        if (kickedmem != null)
                        {
                            byte[] Packet = new byte[90];
                            Writer.WriteUInt16(82, 0, Packet);
                            Writer.WriteUInt16(1312, 2, Packet);
                            Writer.WriteUInt32(22, 4, Packet);
                            Writer.WriteUInt32(kickedmem.Entity.UID, 8, Packet);
                            kickedmem.Send(Packet);

                            kickedmem.Entity.ClanName = "";

                            client.Entity.Save("clanrank", 0);
                            client.Entity.Save("clanid", 0);
                            client.Entity.Save("clandonation", 0);
                            kickedmem.SendScreenSpawn(kickedmem.Entity, true);
                        }
                        if (clan.Members.Remove(clan.Members.SingleOrDefault(x => x.Value.Name == clanp.Offset18String).Key))
                        {
                            clan.InfoToMembers();
                            Database.EntityTable.RemoveClanMember(clanp.Offset18String);
                            Kernel.SendWorldMessage(new Message(String.Format("{0} has kicked {1} form his Clan!", client.Entity.Name, clanp.Offset18String), Color.Red, Message.TopLeft));
                        }
                        break;
                    }
                case Clan.Types.MyClan:
                    {
                        Clan clan = client.Entity.GetClan;
                        if (clan != null)
                        {
                            clan.Build(client, Clan.Types.MyClan);
                            client.Send(clan);
                            clan.Build(client, Clan.Types.Info);
                            client.Send(clan);
                        }
                        break;
                    }
                case Clan.Types.Members:
                    {
                        Clan clan = client.Entity.GetClan;
                        if (clan != null)
                        {
                            ClanMembers memclanp = new ClanMembers(clan);
                            memclanp.Type = clanp.Type;
                            client.Send(memclanp);
                        }
                        break;
                    }
                case Clan.Types.Dedicate:
                    {
                        Clan clan = client.Entity.GetClan;
                        if (clan != null)
                        {
                            if (client.Entity.Money >= clanp.ID)
                            {
                                client.Entity.AdjustMoney(-(decimal)(clanp.ID));
                                clan.Fund += clanp.ID;
                                ClanMember member;
                                if (clan.Members.TryGetValue(client.Entity.UID, out member))
                                {
                                    member.Donation += clanp.ID;
                                    client.Entity.Save("clandonation", member.Donation);
                                }
                                clan.Save(client, "fund", clan.Fund);
                                clan.InfoToMembers();
                                clan.SendMessage(new Message((client.Entity.Name + " dedicated " + clanp.ID + " to the Clan Fund."), Color.Red, Message.Clan));
                                client.Send(clan.ToArray());
                            }
                        }
                        break;
                    }
                case Clan.Types.Join:
                    {
                        uint lider = BitConverter.ToUInt32(packet, 8);
                        if (Kernel.GamePool.ContainsKey(lider))
                        {
                            packet[4] = 11;
                            Writer.WriteUInt32(client.Entity.UID, 8, packet);

                            packet[16] = 1;
                            packet[17] = (byte)client.Entity.Name.Length;
                            for (int i = 0; i < client.Entity.Name.Length; i++)
                            {
                                try
                                {
                                    packet[18 + i] = Convert.ToByte(client.Entity.Name[i]);
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine(e);
                                }
                            }
                            Kernel.GamePool[lider].Send(packet);
                        }
                        break;
                    }
                case Clan.Types.Recruit:
                    {
                        uint lider = BitConverter.ToUInt32(packet, 8);
                        Clan getClan = client.Entity.GetClan;
                        if (getClan.Members.Count >= 12)
                        {
                            client.Entity.SendSysMesage("Sorry, you clan use the limit of members");
                            return;
                        }
                        byte[] pack = new byte[89 + client.Entity.ClanName.Length + client.Entity.Name.Length];
                        Writer.WriteUInt16((ushort)(81 + client.Entity.Name.Length + client.Entity.ClanName.Length), 0, pack);
                        Writer.WriteUInt16(1312, 2, pack);
                        pack[4] = 9;

                        Writer.WriteUInt32(client.Entity.UID, 8, pack);
                        Writer.WriteStringList(new List<string>() { client.Entity.ClanName, client.Entity.Name }, 16, pack);
                        Kernel.GamePool[lider].Send(pack);
                        break;
                    }
                case Clan.Types.AcceptRecruit:
                    {
                        uint lider = BitConverter.ToUInt32(packet, 8);
                        var Client = Kernel.GamePool[lider];
                        if (Client == null) break;
                        if (packet[16] == 1)
                        {
                            Clan clientClan = Client.Entity.GetClan;
                            if (Client.Entity.ClanRank == Clan.Ranks.ClanLeader)
                            {
                                if (clientClan != null)
                                {
                                    if (clientClan.Members.Count < 12)
                                    {
                                        clientClan.Join(client);
                                    }
                                }
                            }
                        }
                        Database.EntityTable.UpdateClanID(Client);
                        Database.EntityTable.UpdateClanRank(Client);
                        break;
                    }
                case Clan.Types.AcceptJoinRequest:
                    {
                        if (packet[16] == 1)
                        {
                            Clan clientClan = client.Entity.GetClan;
                            if (client.Entity.ClanRank == Clan.Ranks.ClanLeader)
                            {
                                if (clientClan != null)
                                {
                                    if (clientClan.Members.Count < 12)
                                    {
                                        GameState member = null;
                                        if (Kernel.GamePool.TryGetValue(BitConverter.ToUInt32(packet, 8), out member))
                                        {
                                            clientClan.Join(member);
                                        }
                                    }
                                }
                            }
                        }
                        break;
                    }
                case Clan.Types.Quit:
                    {
                        Clan clan = client.Entity.GetClan;
                        if (clan != null)
                        {
                            if (client.Entity.ClanRank != Clan.Ranks.ClanLeader)
                            {
                                clan.Members.Remove(client.Entity.UID);

                                client.Entity.ClanRank = Clan.Ranks.None;
                                client.Entity.ClanName = "";
                                client.Entity.ClanId = 0;

                                client.Entity.Save("clanrank", 0);
                                client.Entity.Save("clanid", 0);
                                client.Entity.Save("clandonation", 0);

                                clan.InfoToMembers();
                                client.Send(client.Entity.CharBuffer.ToConquerPacket(10014));

                                clan.SendMessage(new Message(String.Format("{0} Has Left the Clan ", client.Entity.Name), Color.Red, Message.Clan));

                                client.Send(clanp);
                            }
                        }
                        break;
                    }
                case Clan.Types.AddAlly:
                    {
                        Clan getClan = client.Entity.GetClan;
                        if (getClan == null)
                        {
                            return;
                        }
                        if (client.Entity.ClanRank != Clan.Ranks.ClanLeader)
                        {
                            client.Send(new Message("You are not the clan leader.", Color.Red, 2012));
                            return;
                        }
                        if (getClan.Allies.Count<KeyValuePair<uint, Clan>>() >= 5)
                        {
                            client.Send(new Message("The Ammount of Allies You can Have has exceeded", Color.Red, 2012));
                            return;
                        }
                        GameState GameState14;
                        if (!Kernel.GamePool.TryGetValue(clanp.ID, out GameState14))
                        {
                            client.Send(new Message("Can not find target.", Color.Red, 2012));
                            return;
                        }
                        Clan getClan8 = GameState14.Entity.GetClan;
                        if (getClan8 == null)
                        {
                            return;
                        }
                        if (GameState14.Entity.ClanRank != Clan.Ranks.ClanLeader)
                        {
                            client.Send(new Message("This target is not the clan leader.", Color.Red, 2012));
                            return;
                        }
                        if (getClan8.Allies.Count<KeyValuePair<uint, Clan>>() >= 5)
                        {
                            client.Send(new Message("The Ammount of Allies the Target Clan can Have has exceeded", Color.Red, 2012));
                            return;
                        }
                        if (getClan.Allies.ContainsKey(getClan8.ID))
                        {
                            return;
                        }
                        if (!getClan.Enemies.ContainsKey(getClan8.ID))
                        {
                            getClan8.AllyRequest = getClan.ID;
                            Writer.WriteUInt32(client.Entity.UID, 8, packet);
                            Writer.WriteByte(2, 16, packet);
                            Writer.WriteByte((byte)getClan.Name.Length, 17, packet);
                            Writer.WriteString(getClan.Name, 18, packet);
                            ushort num64 = (ushort)(getClan.Name.Length + 18);
                            Writer.WriteByte((byte)getClan.LeaderName.Length, (int)num64, packet);
                            num64 += 1;
                            Writer.WriteString(getClan.LeaderName, (int)num64, packet);
                            GameState14.Send(packet.ToArray<byte>());
                            return;
                        }
                        client.Send(new Message("That clan is Your Enemy.", Color.Red, 2012));
                        return;
                    }
                case Clan.Types.AcceptAlliance:
                    {
                        if (packet[8] != 1)
                        {
                            return;
                        }
                        Clan getClan9 = client.Entity.GetClan;
                        if (getClan9 == null)
                        {
                            return;
                        }
                        if (client.Entity.ClanRank != Clan.Ranks.ClanLeader)
                        {
                            client.Send(new Message("You are not the clan leader.", Color.Red, 2012));
                            return;
                        }
                        Clan clan3;
                        if (!Kernel.Clans.TryGetValue(getClan9.AllyRequest, out clan3))
                        {
                            client.Send(new Message("Can not find target", Color.Red, 2012));
                            return;
                        }
                        if (clan3 == null)
                        {
                            return;
                        }
                        if (clan3.Enemies.ContainsKey(getClan9.ID))
                        {
                            client.Send(new Message("This Clan Has Enemied You!.", Color.Red, 2012));
                            return;
                        }
                        if (!getClan9.Enemies.ContainsKey(clan3.ID))
                        {
                            if (!getClan9.Allies.ContainsKey(clan3.ID))
                            {
                                getClan9.Allies.Add(clan3.ID, clan3);
                            }
                            clan3.Allies.Add(getClan9.ID, getClan9);
                            getClan9.SendMessage(new ClanRelations(getClan9, ClanRelations.RelationTypes.Allies));
                            clan3.SendMessage(new ClanRelations(clan3, ClanRelations.RelationTypes.Allies));
                            getClan9.SendMessage(new Message(string.Format("Our Clan has Allianced with {0}", clan3.Name), Color.Red, 2006));
                            clan3.SendMessage(new Message(string.Format("Our Clan has Allianced with {0}", getClan9.Name), Color.Red, 2006));
                            getClan9.AddRelation(clan3.ID, ClanRelations.RelationTypes.Allies);
                            clan3.AddRelation(getClan9.ID, ClanRelations.RelationTypes.Allies);
                            Clan arg_9788_0 = getClan9;
                            clan3.AllyRequest = 0;
                            arg_9788_0.AllyRequest = 0;
                            return;
                        }
                        client.Send(new Message("This Clan is Your Enemy.", Color.Red, 2012));
                        return;
                    }
                case Clan.Types.DeleteEnemy:
                    {
                        Clan clan = client.Entity.GetClan;
                        if (clan != null)
                        {
                            if (client.Entity.ClanRank == Clan.Ranks.ClanLeader)
                            {
                                String EnemyTarget = clanp.Offset18String;
                                UInt32 ClanId = clan.GetClanId(EnemyTarget);

                                Clan tclan;
                                if (Kernel.Clans.TryGetValue(ClanId, out tclan))
                                {
                                    clan.Enemies.Remove(ClanId);

                                    clan.DeleteRelation(ClanId, ClanRelations.RelationTypes.Enemies);

                                    clan.SendMessage(new ClanRelations(clan, ClanRelations.RelationTypes.Enemies));

                                    clan.SendMessage(new Message(String.Format("We are no longer Enemies With {0}", tclan.Name), Color.Red, Message.Clan));

                                    client.Send(clanp);
                                }
                            }
                            else client.Send(new Message("You are not authorized to continue with this action", Color.Red, Message.TopLeft));
                        }
                        break;
                    }
                case Clan.Types.DeleteAlly:
                    {
                        Clan clan = client.Entity.GetClan;
                        if (clan != null)
                        {
                            if (client.Entity.ClanRank == Clan.Ranks.ClanLeader)
                            {
                                String AlliedTarget = clanp.Offset18String;
                                UInt32 ClanId = clan.GetClanId(AlliedTarget);

                                Clan tclan;
                                if (clan.Allies.TryGetValue(ClanId, out tclan))
                                {
                                    clan.Allies.Remove(ClanId);
                                    tclan.Allies.Remove(clan.ID);

                                    clan.DeleteRelation(ClanId, ClanRelations.RelationTypes.Allies);
                                    tclan.DeleteRelation(clan.ID, ClanRelations.RelationTypes.Allies);

                                    clan.SendMessage(new ClanRelations(clan, ClanRelations.RelationTypes.Allies));
                                    tclan.SendMessage(new ClanRelations(tclan, ClanRelations.RelationTypes.Allies));

                                    clan.SendMessage(new Message(String.Format("We are no longer allied with {0}", tclan.Name), Color.Red, Message.Clan));
                                    tclan.SendMessage(new Message(String.Format("We are no longer allied with {0}", clan.Name), Color.Red, Message.Clan));
                                    client.Send(clanp);
                                    client.Screen.FullWipe();
                                    client.Screen.Reload(null);

                                }
                            }
                            else client.Send(new Message("You are not authorized to continue with this action", Color.Red, Message.TopLeft));
                        }
                        break;
                    }
                case Clan.Types.AddEnemy:
                    {
                        Clan clan = client.Entity.GetClan;
                        if (clan != null)
                        {
                            if (client.Entity.ClanRank == Clan.Ranks.ClanLeader)
                            {
                                String Enemy = clanp.Offset18String;
                                UInt32 ClanId = clan.GetClanId(Enemy);
                                if (!clan.Enemies.ContainsKey(ClanId))
                                {
                                    if (!clan.Allies.ContainsKey(ClanId))
                                    {
                                        if (clan.Enemies.Count() >= 5) { client.Send(new Message("The Ammount of Enemies You can Have has exceeded", Color.Red, Message.TopLeft)); break; }

                                        Clan tclan;
                                        if (Kernel.Clans.TryGetValue(ClanId, out tclan))
                                        {
                                            if (!clan.Enemies.ContainsKey(tclan.ID))
                                                clan.Enemies.Add(tclan.ID, tclan);
                                            clan.AddRelation(ClanId, ClanRelations.RelationTypes.Enemies);

                                            clan.SendMessage(new ClanRelations(clan, ClanRelations.RelationTypes.Enemies));

                                            clan.SendMessage(new Message(String.Format("We Have Enemied the clan {0}", tclan.Name), Color.Red, Message.Clan));
                                            tclan.SendMessage(new Message(String.Format("The Clan {0} Has Made us their Enemy!", clan.Name), Color.Red, Message.Clan));
                                        }
                                    }
                                    else client.Send(new Message("This clan is one of your alliance, What has gone wrong?", Color.Red, Message.TopLeft));
                                }
                                else client.Send(new Message("This clan is Already One of Your Enemies", Color.Red, Message.TopLeft));
                            }
                        }
                        break;
                    }
                case Clan.Types.SetAnnouncement:
                    {
                        Clan clan = client.Entity.GetClan;
                        if (clan != null)
                        {
                            if (client.Entity.ClanRank == Clan.Ranks.ClanLeader)
                            {
                                clan.Announcement = clanp.Offset18String;

                                clan.SendMessage(new ClanAnnouncement(clan));
                                clan.Save(client, "announcement", clan.Announcement);
                            }
                            else client.Send(new Message("You are not authorized to continue with this action", Color.Red, Message.TopLeft));
                        }
                        break;
                    }
                default: Console.WriteLine(String.Format("Un Handled Clan clanp Type {0}", clanp.Type)); break;
            }
        }
    }
}