using System;
using System.IO;
using System.Linq;
using Revora.Client;
using Revora.Database;
using System.Collections.Generic;

namespace Revora.Network.GamePackets
{
    public unsafe class Quests
    {
        public class QuestItem
        {
            public MsgTaskStatus.QuestData QItem;

            public uint Kills = 0;
            public uint Kills1 = 0;
            public uint Kills2 = 0;
            public string Mob;
            public static QuestItem Create(MsgTaskStatus.QuestData Q_Item, uint Kills, string Mob = "")
            {
                QuestItem retn = new QuestItem();
                retn.QItem = Q_Item;
                retn.Mob = Mob;
                retn.Kills = Kills;
                return retn;
            }
            public void WriteItem(BinaryWriter writer)
            {
                writer.Write((uint)QItem.UID);
                writer.Write((uint)QItem.Status);
                writer.Write(QItem.Time);
                writer.Write(Kills);
                writer.Write(Mob);
            }
            public QuestItem ReadItem(BinaryReader reader)
            {
                QItem = new MsgTaskStatus.QuestData();
                QItem.UID = (QuestID)reader.ReadUInt32();
                QItem.Status = (MsgTaskStatus.QuestData.QuestStatus)reader.ReadUInt32();
                QItem.Time = reader.ReadUInt32();
                Kills = reader.ReadUInt32();
                Mob = reader.ReadString();
                return this;
            }
        }
        public void AshesOfAnger(QuestID UID, uint Kills1)
        {
            if (src.ContainsKey(UID))
            {
                var item = src[UID];
                item.Kills += Kills1;
                src[UID] = item;
                MsgTaskDetailInfo dataq = new MsgTaskDetailInfo(true);
                dataq.UID = UID;
                dataq.AshesOfAnger = item.Kills;
                Player.Send(dataq);
            }
        }

        public DateTime LastResetTime;
        public GameState Player;
        private SafeDictionary<QuestID, QuestItem> src;
        public Quests(GameState _owner)
        {
            Player = _owner;
            src = new SafeDictionary<QuestID, QuestItem>();
        }
        public void Load()
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT))
            {
                cmd.Select("quests").Where("UID", Player.Entity.UID);
                using (MySqlReader rdr = new MySqlReader(cmd))
                {
                    if (rdr.Read())
                    {
                        byte[] data = rdr.ReadBlob("quests");
                        if (data.Length > 0)
                        {
                            using (var stream = new MemoryStream(data))
                            using (var reader = new BinaryReader(stream))
                            {
                                int count = reader.ReadByte();
                                for (uint x = 0; x < count; x++)
                                {
                                    QuestItem item = new QuestItem();
                                    item = item.ReadItem(reader);
                                    src.Add(item.QItem.UID, item);
                                }
                            }
                        }
                        LastResetTime = DateTime.FromBinary(rdr.ReadInt64("LastResetTime"));
                        if (DateTime.Now.DayOfYear != LastResetTime.DayOfYear)
                        {
                            var array = src.Values.Where(p => QuestInfo.CheckType((QuestID)p.QItem.UID) == QuestType.DailyQuest && p.QItem.Status == MsgTaskStatus.QuestData.QuestStatus.Finished).ToArray();
                            for (int i = 0; i < array.Length; i++)
                            {
                                src.Remove(array[i].QItem.UID);
                            }
                            array = null;
                            LastResetTime = DateTime.Now;
                        }
                    }
                    else
                    {
                        using (var command = new MySqlCommand(MySqlCommandType.INSERT))
                        {
                            command.Insert("quests").Insert("UID", Player.Entity.UID).Insert("Name", Player.Entity.Name);
                            command.Execute();
                        }
                    }
                }
            }
        }
        public void Save()
        {
            MemoryStream stream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stream);
            writer.Write((byte)src.Count);
            foreach (var quest in src.Values)
                quest.WriteItem(writer);
            string SQL = "UPDATE quests SET quests=@quests,LastResetTime=@rest WHERE uid=@uid";
            byte[] rawData = stream.ToArray();
            using (var conn = DataHolder.MySqlConnection)
            {
                conn.Open();
                using (var cmd = new Npgsql.NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = Revora.Database.DataHolder.ToPostgres(SQL);
                    DataHolder.AddParameter(cmd, "@uid", (long)Player.Entity.UID);
                    DataHolder.AddParameter(cmd, "@rest", LastResetTime.Ticks);
                    DataHolder.AddParameter(cmd, "@quests", rawData);
                    cmd.ExecuteNonQuery();
                }
            }
        }
        #region Realm
        public void ThunderStrike(QuestID UID, uint Kills)
        {
            if (src.ContainsKey(UID) && IsActiveQuest(UID))
            {
                var item = src[UID];
                item.Kills += Kills;
                src[UID] = item;

                MsgTaskDetailInfo dataq = new MsgTaskDetailInfo(true);
                dataq.UID = UID;
                dataq.ThunderStrike = item.Kills;
                Player.Send(dataq);
            }
        }
        public void CrystalBounty(QuestID UID, uint Kills)
        {
            if (src.ContainsKey(UID) && IsActiveQuest(UID))
            {
                var item = src[UID];
                item.Kills += Kills;
                src[UID] = item;

                MsgTaskDetailInfo dataq = new MsgTaskDetailInfo(true);
                dataq.UID = UID;
                dataq.CrystalBounty = item.Kills;
                Player.Send(dataq);
            }
        }
        #endregion
        public void DoneTowerMystery()
        {
            if (src.ContainsKey(QuestID.TowerOfMystery) && IsActiveQuest(QuestID.TowerOfMystery))
            {
                var item = src[QuestID.TowerOfMystery];
                item.Kills = 1;
                src[QuestID.TowerOfMystery] = item;

                MsgTaskDetailInfo dataq = new MsgTaskDetailInfo(true);
                dataq.UID = QuestID.TowerOfMystery;
                dataq[1] = 1;
                dataq[2] = 1;
                Player.Send(dataq);
            }
        }
        public void IncreaseQuestKills5(QuestID UID, uint Kills)
        {
            if (src.ContainsKey(UID) && IsActiveQuest(UID))
            {
                var item = src[UID];
                item.Kills += Kills;
                src[UID] = item;

                MsgTaskDetailInfo dataq = new MsgTaskDetailInfo(true);
                dataq.UID = UID;
                dataq[1] = item.Kills;
                if (item.Kills == 15)
                {
                    dataq.Bright = 1;
                }
                Player.Send(dataq);
            }
        }
        public void IncreaseQuestKills2(QuestID UID, uint item1)
        {
            if (src.ContainsKey(UID) && IsActiveQuest(UID))
            {

                var item = src[UID];
                item.Kills += item1;
                src[UID] = item;
                uint kills2 = item.Kills;
                MsgTaskDetailInfo dataq = new MsgTaskDetailInfo(true);
                dataq.UID = UID;
                dataq[2] = dataq[2] + item1;
                while (kills2 >= 8)
                {
                    dataq[1] += 1;
                    dataq[2] = 0;
                    kills2 -= 8;
                }
                Player.Send(dataq);
            }
        }
        public void IncreaseQuestKills(QuestID UID, uint Kills)
        {
            if (src.ContainsKey(UID) && IsActiveQuest(UID))
            {
                var item = src[UID];
                item.Kills += Kills;
                src[UID] = item;
                MsgTaskDetailInfo dataq = new MsgTaskDetailInfo(true);
                dataq.UID = UID;
                dataq[1] = Kills;
                dataq[2] = item.Kills;
                Player.Send(dataq);
            }
        }
        private bool IsActiveQuest(QuestID UID)
        {
            return CheckQuest(UID, MsgTaskStatus.QuestData.QuestStatus.Accepted);
        }
        public bool CheckQuest(QuestID UID, MsgTaskStatus.QuestData.QuestStatus status)
        {
            return src.Values.Where((p) => p.QItem.UID == UID && p.QItem.Status == status).Count() == 1;
        }
        public bool HasQuest(QuestID UID)
        {
            if (src.ContainsKey(UID))
                return true;
            return false;
        }
        public MsgTaskStatus.QuestData.QuestStatus CheckQuest(QuestID UID)
        {
            return src[UID].QItem.Status;
        }
        public QuestItem GetQuest(QuestID UID)
        {
            return src[UID];
        }
        public bool FinishQuest(QuestID UID)
        {
            if (src.ContainsKey(UID))
            {
                var item = src[UID];
                if (item.QItem.Status != MsgTaskStatus.QuestData.QuestStatus.Finished)
                {
                    item.QItem.Status = MsgTaskStatus.QuestData.QuestStatus.Finished;
                    src[UID] = item;
                    SendSinglePacket(item.QItem, MsgTaskStatus.QuestAction.Complete);
                    return true;
                }
            }
            return false;
        }
        public QuestItem Accept(QuestID UID, uint time = 0)
        {
            if (!src.ContainsKey(UID))
            {
                var n_quest = QuestItem.Create(MsgTaskStatus.QuestData.Create(UID, MsgTaskStatus.QuestData.QuestStatus.Accepted, time), 0);
                src.Add(n_quest.QItem.UID, n_quest);
                SendSinglePacket(n_quest.QItem, MsgTaskStatus.QuestAction.Begin);
                return n_quest;
            }
            return new QuestItem();
        }
        private void SendSinglePacket(MsgTaskStatus.QuestData data, MsgTaskStatus.QuestAction mode)
        {
            MsgTaskStatus Packet = new MsgTaskStatus(true, 1);
            Packet.Action = mode;
            Packet.Apend(data);
            Player.Send(Packet);
        }
        private int AcceptQuestsCount()
        {
            return src.Values.Where((p) => p.QItem.Status == MsgTaskStatus.QuestData.QuestStatus.Accepted).Count();
        }
        public System.Collections.Generic.IEnumerable<QuestItem> Items
        {
            get { return src.Values; }
        }
        public void ClearQuest(QuestID UID)
        {
            src.Remove(UID);
        }
        public bool AllowAccept()
        {
            return AcceptQuestsCount() < 20;
        }
        public GameState client;
        public void SendPathFinding(string Text, ushort map, ushort x, ushort y, uint NpcUid)
        {
            Player.MessageBox(Text, p =>
            {
                Data datapacket = new Data(true);
                datapacket.UID = p.Entity.UID;
                datapacket.ID = Data.PathFinding;
                datapacket.TimeStamp2 = NpcUid;
                datapacket.X = x;
                datapacket.Y = y;
                p.Send(datapacket);
            }, null, 0);
        }
        public void SendFullGUI()
        {
            var list = new List<MsgTaskStatus.QuestData>();
            Revora.Game.Features.QuestListSystem.Fill(Player, list);
            if (list.Count == 0)
            {
                MsgTaskStatus empty = new MsgTaskStatus(true, 0);
                empty.Action = MsgTaskStatus.QuestAction.List;
                Player.Send(empty);
                return;
            }
            for (int offset = 0; offset < list.Count; offset += 80)
            {
                int n = list.Count - offset;
                if (n > 80)
                    n = 80;
                MsgTaskStatus Packet = new MsgTaskStatus(true, n);
                Packet.Action = MsgTaskStatus.QuestAction.List;
                for (int i = 0; i < n; i++)
                    Packet.Apend(list[offset + i]);
                Player.Send(Packet);
            }
            Revora.Game.Features.AbyssTrialJournal.SendDetails(Player);
            Revora.Game.Features.MonkEpicJournal.SendDetails(Player);
            Revora.Game.Features.TaoistEpicJournal.SendDetails(Player);
            Revora.Game.Features.TrojanEpicJournal.SendDetails(Player);
        }
        public bool QuitQuest(QuestID UID)
        {
            if (src.ContainsKey(UID))
            {
                var item = src[UID];
                if (item.QItem.Status == MsgTaskStatus.QuestData.QuestStatus.Accepted)
                {
                    item.QItem.Status = MsgTaskStatus.QuestData.QuestStatus.Available;
                    src[UID] = item;
                    SendSinglePacket(item.QItem, MsgTaskStatus.QuestAction.QuitQuest);
                    src.Remove(UID);
                    return true;
                }
            }
            return false;
        }

    }
    public unsafe class QuestInfo
    {
        public struct Info
        {
            public QuestID MissionId;
            public QuestType TypeId;
        }
        public static uint ActionBase;
        public static SafeDictionary<QuestID, Info> AllQuests = new SafeDictionary<QuestID, Info>();
        public static void Load()
        {
            string[] text = File.ReadAllLines("database\\Questinfo.ini");
            Info info = new Info();
            for (int x = 0; x < text.Length; x++)
            {
                string line = text[x];
                string[] split = line.Split('=');
                if (split[0] == "ActionBase")
                    ActionBase = uint.Parse(split[1]);
                else if (split[0] == "TotalMission")
                    AllQuests = new SafeDictionary<QuestID, Info>(int.Parse(split[1]));
                else if (split[0] == "MissionId")
                {
                    var id = (QuestID)uint.Parse(split[1]);
                    info.MissionId = id;
                    AllQuests[id] = info;
                }
                else if (split[0] == "TypeId")
                {
                    info.TypeId = (QuestType)byte.Parse(split[1]);
                    if (info.MissionId != 0)
                        AllQuests[info.MissionId] = info;
                }
            }
            Revora.Game.Features.QuestListSystem.EnsureCatalog();
        }
        public static void Register(QuestID id, QuestType type)
        {
            Info info = new Info();
            info.MissionId = id;
            info.TypeId = type;
            AllQuests[id] = info;
        }
        public static QuestType CheckType(QuestID UID)
        {
            if (!AllQuests.ContainsKey(UID))
                return QuestType.RegionQuests;
            return AllQuests[UID].TypeId;
        }
    }
    public enum QuestID : uint
    {
        Message = 804,
        Solar_Blade_1 = 3269,
        Solar_Blade_2 = 3278,
        Evil_Array_1 = 3275,
        Evil_Array_2 = 3270,
        Ghost_Reavers = 3271,
        The_Traitor_1 = 3277,
        The_Traitor_2 = 3272,
        Solar_Essences_1 = 3273,
        Solar_Essences_2 = 3279,
        Devastator_1 = 3280,
        Secret_in_the_Chest = 3679,
        Weird_Formation = 3680,
        Mind_of_Evil = 3682,
        Evil_Array_3 = 3276,
        WorshipLeaders = 6329,
        Heaven_Treasury = 6390,
        Secound_Quest = 2414,
        Secound_Quest2 = 2415,
        Secound_Quest3 = 2416,
        Secound_Quest4 = 2417,
        Tower_of_Mystery = 6126,
        Secound_Quest5 = 2418,
        Secound_Quest6 = 2419,
        Undercover = 30001,
        Struggle_for_Ores = 35006,
        Evil_Invasion = 35007,
        Doom_of_Beast = 35008,
        The_Riddle_of_Origins = 6435,
        The_Armorer = 6425,
        The_Storekeeper = 812,
        NightmareCaptain = 3303,
        SorrowofDesolation = 3644,
        FireForce = 3647,
        GloryOfThePast = 3637,
        SecretOfBright = 3638,
        HealingTheDying = 3642,
        PathToFlameTemple = 3643,
        TheWayofTommorow = 3631,
        UnexceptedDiscovery = 3639,
        BloodVengeance = 3634,
        MetalForce = 3646,
        MysteriousMetal = 3640,
        EvilRoot = 3635,
        ForgeFurance = 3636,
        EndHomelessness = 3649,
        SacrificetotheDead = 3641,
        WaitingForMiracle = 3648,
        UnknownDangers = 3632,
        WheelofNature = 3645,
        ScrambleforJustice = 35034,
        CrystalBounty = 35028,
        AshesOfAnger = 35024,
        BeastsOfLegend = 35025,
        DisCity1 = 6472,
        MonsterSage = 3633,
        Discity2 = 6473,
        DisCity3 = 6474,
        DisCity4 = 6475,
        ThunderStrike = 35007,
        TowerOfMystery = 6126,
        EvilLabyrinth = 6467,
        HeavenTreasury = 6390,
        TempestWing = 200,
        EveryThingHasAPrice = 6245,
        Release_the_souls = 6049,
        RareMaterials = 6366,
        SecondQuestStageOne = 2414,
        SkyPass = 6350,
        SecondQuestStageTwo = 2416,
        SecondQuestStageThree = 2418,
        SecondQuestStageFour = 2419,
        GoldBox = 3301,
        The_Pharmacist = 813,
        Create_a_Team = 802,
        The_Barber = 815,
        The_Warehouseman = 817,
        Upgrade_Equipment_1 = 818,
        First_Stab_1 = 823,
        First_Stab_2 = 824,
        Job_promotion_1 = 6426,
        Letter_Delivery_1 = 1006,
        Letter_Delivery_2 = 1007,
        Letter_Delivery_3 = 1008,
        Robin_Elimination = 1009,
        Feather_Collection = 1010,
        Love_Confession_1 = 1011,
        Love_Confession_2 = 1012,
        Love_Confession_3 = 1013,
        Love_Confession_4 = 1014,
        Love_Confession_5 = 1015,
        Capture_Spirit_I = 1016,
        Capture_Spirit_II = 1017,
        Ghostly_Secrets_I = 1018,
        Ghostly_Secrets_II = 1019,
        Ghostly_Secrets_III = 1020,
        Ghostly_Secrets_IV = 1021,
        Battlefield_Heroes = 2376,
        TalkToKungFu = 6435,
        TalkToArmorer = 1560,
        TalkToStoreKeeper = 812,
        TalkToPharmacise = 659,
        TalkToTheBarbr = 815,
        TalkToWareHouse = 817,
        TalkToBlackSmith = 818,
        TalkToTCCaptin = 823,
        Kill10Pheasent = 824,
        TalkToKungFuKing = 6426,
        TalkToKungFuKing2 = 6189,
        TalkToTrojanStar = 825,
        TalkToTrojanStar2 = TalkToTrojanStar + 6,
        TalkToWarriorGod = 826,
        TalkToWarriorGod2 = TalkToWarriorGod + 6,
        TalkToArcherGod = 827,
        TalkToArcherGod2 = TalkToArcherGod + 6,
        TalkToWindSage = 828,
        TalkToWindSage2 = TalkToWindSage + 6,
        TalkToHeadAboat = 829,
        TalkToHeadAboat2 = TalkToHeadAboat + 6,
        TalkToTaoistMoon = 830,
        TalkToTaoistMoon2 = TalkToTaoistMoon + 6,
        TalkToWindWalker = 3795,
        Sudden_Attack = 3797,
        Cry_for_Help = 3798,
        TalkToPirateLord = 2172,
        TalkToPirateLord2 = TalkToPirateLord + 6,
        ExperienceXPSkill = 2365,
        LetterHome = 1023,
        RevoraPathHunt = 90001,
        RevoraCourierLoop = 90002,
        RevoraSoulTrial = 90003,
        RevoraDailyEnvoy = 90004,
    }
    public enum QuestType : uint
    {
        RecruitQuest = 1,
        TutorialQuest = 2,
        DailyQuest = 3,
        EquipmentBonus = 4,
        Event = 5,
        Nezha_Feud = 6,
        RegionQuests = 7,
        Festival = 8,
        CrossServer = 9,
        KingdomWar = 10,
        ScrambleRealm = 11
    }
    public unsafe class MsgTaskDetailInfo : Writer, Interfaces.IPacket
    {
        byte[] Buffer;
        public MsgTaskDetailInfo(bool Create)
        {
            if (true)
            {
                Buffer = new byte[60 + 16 + 8];
                WriteUInt16((ushort)(Buffer.Length - 8), 0, Buffer);
                WriteUInt16(1135, 2, Buffer);
                WriteUInt16(1, 6, Buffer);
            }
        }
        public QuestID UID
        {
            get { return (QuestID)BitConverter.ToUInt32(Buffer, 8); }
            set { WriteUInt32((uint)value, 8, Buffer); }
        }
        public uint this[int index]
        {
            get { return BitConverter.ToUInt32(Buffer, 8 + 4 * index); }
            set { WriteUInt32(value, 8 + 4 * index, Buffer); }
        }
        public uint Bright
        {
            get { return BitConverter.ToUInt32(Buffer, 16); }
            set { Write(value, 16, Buffer); }
        }
        public uint AshesOfAnger
        {
            get { return BitConverter.ToUInt32(Buffer, 16); }
            set { Write(value, 16, Buffer); }
        }
        public uint CrystalBounty
        {
            get { return BitConverter.ToUInt32(Buffer, 12); }
            set { Write(value, 12, Buffer); }
        }
        public uint ThunderStrike
        {
            get { return BitConverter.ToUInt32(Buffer, 32); }
            set { Write(value, 32, Buffer); }
        }
        public byte[] ToArray()
        {
            return Buffer;
        }
        public void Deserialize(byte[] buffer)
        {
            Buffer = buffer;
        }
        public void Send(GameState client)
        {
            client.Send(Buffer);
        }
    }
    public unsafe class MsgTaskStatus : Writer, Interfaces.IPacket
    {
        public enum QuestAction : ushort
        {
            Begin = 1,
            QuitQuest = 2,
            List = 3,
            Complete = 4
        }
        public struct QuestData
        {
            public enum QuestStatus : uint
            {
                Accepted = 0,
                Finished = 1,
                Available = 2
            }
            public QuestID UID;
            public QuestStatus Status;
            public uint Time;
            public static QuestData Create(QuestID _uid, QuestStatus _status, uint _time)
            {
                QuestData qItem = new QuestData();
                qItem.UID = _uid;
                qItem.Status = _status;
                qItem.Time = _time;
                return qItem;
            }
        }
        byte[] Buffer;
        public MsgTaskStatus(bool Create, int count = 0)
        {
            if (Create)
            {
                if (count < 0 || count > 80) throw new ArgumentOutOfRangeException("count");
                Buffer = new byte[16 + 12 * count];
                WriteUInt16((ushort)(Buffer.Length - 8), 0, Buffer);
                WriteUInt16(1134, 2, Buffer);
                Amount = (ushort)count;
            }
        }
        ushort offset = 8;
        public void Apend(QuestData quest)
        {
            if (Buffer == null || offset + 12 > Buffer.Length - 8)
                throw new InvalidOperationException("Quest list capacity exceeded.");
            Uint((uint)quest.UID, offset, Buffer);
            offset += 4;
            Uint((uint)quest.Status, offset, Buffer);
            offset += 4;
            Uint(quest.Time, offset, Buffer);
            offset += 4;
        }
        public QuestAction Action
        {
            get { return (QuestAction)BitConverter.ToUInt16(Buffer, 4); }
            set { WriteUInt16((ushort)value, 4, Buffer); }
        }

        public ushort Amount
        {
            get { return BitConverter.ToUInt16(Buffer, 6); }
            set { WriteUInt16(value, 6, Buffer); }
        }
        public QuestData this[int index]
        {
            get
            {
                if (index < 0 || index >= Amount || 8L + 12L * (index + 1) > Buffer.Length)
                    throw new ArgumentOutOfRangeException("index");
                QuestData data = new QuestData();
                data.UID = (QuestID)BitConverter.ToUInt32(Buffer, 8 + 12 * index);
                data.Status = (QuestData.QuestStatus)BitConverter.ToUInt32(Buffer, 8 + 12 * index + 4);
                data.Time = BitConverter.ToUInt32(Buffer, 8 + 12 * index + 8);
                return data;
            }
            set
            {
                if (index < 0 || index >= Amount || 8L + 12L * (index + 1) > Buffer.Length - 8)
                    throw new ArgumentOutOfRangeException("index");
                WriteUInt32((uint)value.UID, 8 + 12 * index, Buffer);
                WriteUInt32((uint)value.Status, 8 + 12 * index + 4, Buffer);
                WriteUInt32(value.Time, 8 + 12 * index + 8, Buffer);
            }
        }
        public byte[] ToArray()
        {
            return Buffer;
        }
        public void Deserialize(byte[] buffer)
        {
            Buffer = buffer;
        }
        public void Send(GameState client)
        {
            client.Send(Buffer);
        }
        public static void Process(byte[] packet, GameState client)
        {
            if (packet == null || packet.Length < 8 || packet.Length > 976
                || BitConverter.ToUInt16(packet, 2) != 1134
                || !Revora.Game.Features.QuestInventory.Player(client, false) || client.Quests == null) return;
            int declared = BitConverter.ToUInt16(packet, 0);
            if (declared < 8 || (declared != packet.Length && declared != packet.Length - 8)) return;
            MsgTaskStatus quest = new MsgTaskStatus(false);
            quest.Deserialize(packet);
            switch (quest.Action)
            {
                case QuestAction.Begin:
                    {
                        if (quest.Amount != 1 || declared != 20 || !QuestInfo.AllQuests.ContainsKey(quest[0].UID)) return;
                        if (Revora.Game.Features.TrojanEpicJournal.Protect((uint)quest[0].UID)) {
                            if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.TrojanEpicJournal.Refresh(client);
                            return;
                        }
                        if (Revora.Game.Features.NewcomerIntroduction.Protect((uint)quest[0].UID)) {
                            if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.NewcomerIntroduction.Refresh(client);
                            return;
                        }
                        if (Revora.Game.Features.TomboyQuest.Protect((uint)quest[0].UID)) {
                            if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.TomboyQuest.Refresh(client);
                            return;
                        }
                        if (Revora.Game.Features.DesertTreasureQuest.Protect((uint)quest[0].UID)) {
                            if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.DesertTreasureQuest.Refresh(client);
                            return;
                        }
                        if (Revora.Game.Features.DefenseTalismanQuest.Protect((uint)quest[0].UID)) {
                            if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.DefenseTalismanQuest.Refresh(client);
                            return;
                        }
                        if (Revora.Game.Features.AttackTalismanQuest.Protect((uint)quest[0].UID)) {
                            if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.AttackTalismanQuest.Refresh(client);
                            return;
                        }
                        if (Revora.Game.Features.TempestWingsQuest.Protect((uint)quest[0].UID)) {
                            if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.TempestWingsQuest.Refresh(client);
                            return;
                        }
                        if (Revora.Game.Features.DesertExpeditionQuest.Protect((uint)quest[0].UID)) {
                            if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.DesertExpeditionQuest.Refresh(client);
                            return;
                        }
                        if (Revora.Game.Features.FamilyFeudQuest.Protect((uint)quest[0].UID)) {
                            if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.FamilyFeudQuest.Refresh(client);
                            return;
                        }
                        if (Revora.Game.Features.HeroWingsQuest.Protect((uint)quest[0].UID)) {
                            if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.HeroWingsQuest.Refresh(client);
                            return;
                        }
                        if (Revora.Game.Features.DesertBloodQuest.Protect((uint)quest[0].UID)) {
                            if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.DesertBloodQuest.Refresh(client);
                            return;
                        }
                        if (Revora.Game.Features.GhostCityQuest.Protect((uint)quest[0].UID)) {
                            if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.GhostCityQuest.Refresh(client);
                            return;
                        }
                        if (Revora.Game.Features.MadManQuest.Protect((uint)quest[0].UID)) {
                            if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.MadManQuest.Refresh(client);
                            return;
                        }
                        if (Revora.Game.Features.SoulGeneralQuest.Protect((uint)quest[0].UID)) {
                            if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.SoulGeneralQuest.Refresh(client);
                            return;
                        }
                        if (Revora.Game.Features.FranticMonkeysQuest.Protect((uint)quest[0].UID)) {
                            if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.FranticMonkeysQuest.Refresh(client);
                            return;
                        }
                        if (Revora.Game.Features.MysticRingQuest.Protect((uint)quest[0].UID)) {
                            if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.MysticRingQuest.Refresh(client);
                            return;
                        }
                        if (Revora.Game.Features.ArtisanFeatherQuest.Protect((uint)quest[0].UID)) {
                            if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.ArtisanFeatherQuest.Refresh(client);
                            return;
                        }
                        if (Revora.Game.Features.SpunGoldQuest.Protect((uint)quest[0].UID)) {
                            if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.SpunGoldQuest.Refresh(client);
                            return;
                        }
                        if (Revora.Game.Features.ConnectorQuest.Protect((uint)quest[0].UID)) {
                            if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.ConnectorQuest.Refresh(client);
                            return;
                        }
                        if (Revora.Game.Features.PowerBookJournal.Protect((uint)quest[0].UID)) {
                            if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.PowerBookJournal.Refresh(client);
                            return;
                        }
                        if (Revora.Game.Features.AzurePillQuest.Protect((uint)quest[0].UID)) {
                            if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.AzurePillQuest.Refresh(client);
                            return;
                        }
                        if (Revora.Game.Features.WarriorEpicJournal.Protect((uint)quest[0].UID)) {
                            if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.WarriorEpicJournal.Refresh(client);
                            return;
                        }
                        if (Revora.Game.Features.MonkEpicJournal.Protect((uint)quest[0].UID)) {
                            if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.MonkEpicJournal.Refresh(client);
                            return;
                        }
                        if (Revora.Game.Features.PirateEpicJournal.Protect((uint)quest[0].UID)) {
                            if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.PirateEpicJournal.Refresh(client);
                            return;
                        }
                        if (Revora.Game.Features.TaoistEpicJournal.Protect((uint)quest[0].UID)) {
                            if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.TaoistEpicJournal.Refresh(client);
                            return;
                        }
                        if (Revora.Game.Features.AbyssTrialJournal.Protect(client, (uint)quest[0].UID)) {
                            if (TitleRequirements.AllowRequest(client)) Revora.Game.Features.AbyssTrialJournal.Refresh(client);
                            return;
                        }
                        if ((uint)quest[0].UID >= 6472 && (uint)quest[0].UID <= 6475) {
                            if (!TitleRequirements.AllowRequest(client)) return;
                            Revora.Game.Features.DisCityQuest.Refresh(client);
                            client.Quests.SendFullGUI(); return;
                        }
                        if ((uint)quest[0].UID == Revora.Game.Features.SkyPassQuest.Mission) {
                            if (!TitleRequirements.AllowRequest(client)) return;
                            Revora.Game.Features.SkyPassQuest.Refresh(client);
                            client.Quests.SendFullGUI(); return;
                        }
                        if ((uint)quest[0].UID == Revora.Game.Features.ReleaseSoulsQuest.Mission) {
                            if (!TitleRequirements.AllowRequest(client)) return;
                            Revora.Game.Features.ReleaseSoulsQuest.Refresh(client);
                            client.Quests.SendFullGUI(); return;
                        }
                        if (client.Quests.AllowAccept())
                            client.Quests.Accept(quest[0].UID, 0);
                        break;
                    }
                case QuestAction.List:
                    {
                        if (quest.Amount < 80 && TitleRequirements.AllowRequest(client))
                            client.Quests.SendFullGUI();
                        break;
                    }
            }
        }
    }
}
