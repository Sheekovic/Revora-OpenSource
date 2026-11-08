//using ProtoBuf;
//using System;
//using System.Runtime.InteropServices;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Collections.Concurrent;
//using System.Text;
//using Revora;
//using Revora.Database;
//using System.Threading.Tasks;

//namespace Revora.Network.GamePackets
//{
//    public class MsgRankMemberShow
//    {
//        public MsgRankMemberShow() { }
//        public MsgRankMemberShowProto Info;
//        [ProtoContract]
//        public class MsgRankMemberShowProto
//        {
//            [ProtoMember(1, IsRequired = true)]
//            public uint ActionId;
//            [ProtoMember(2, IsRequired = true)]
//            public MsgRankMemberShowProtoSer proto;
//        }
//        [ProtoContract]
//        public class MsgRankMemberShowProtoSer
//        {
//            [ProtoMember(1, IsRequired = true)]
//            public uint dwParam;
//            [ProtoMember(2, IsRequired = true)]
//            public uint Count;
//            [ProtoMember(3, IsRequired = true)]
//            public uint UID;
//            [ProtoMember(4, IsRequired = true)]
//            public string Name = "";
//            [ProtoMember(5, IsRequired = true)]
//            public string Guild = "";
//            [ProtoMember(6, IsRequired = true)]
//            public uint Mesh;
//            [ProtoMember(7, IsRequired = true)]
//            public uint HairStyle;
//            [ProtoMember(8, IsRequired = true)]
//            public uint Hat;
//            [ProtoMember(9, IsRequired = true)]
//            public uint Garment;
//            [ProtoMember(10, IsRequired = true)]
//            public uint LeftWep;
//            [ProtoMember(11, IsRequired = true)]
//            public uint LeftWepAcc;
//            [ProtoMember(12, IsRequired = true)]
//            public uint RightWep;
//            [ProtoMember(13, IsRequired = true)]
//            public uint RightWepAcc;
//            [ProtoMember(14, IsRequired = true)]
//            public uint MountArmor;
//            [ProtoMember(15, IsRequired = true)]
//            public uint Armor;
//            [ProtoMember(16, IsRequired = true)]
//            public uint Wing;
//            [ProtoMember(17, IsRequired = true)]
//            public uint WingLevel;
//            [ProtoMember(18, IsRequired = true)]
//            public uint Title;
//            [ProtoMember(19, IsRequired = true)]
//            public uint Flag;
//        }
//        public void UpdateBestPlayer()
//        {
//            try
//            {
//                Stuff.Clear();
//                BestPlayer = null;
//                Stuff = new Dictionary<string, uint>();
//                BestPlayer = CMsgUserAbilityScore.RankingList.OrderByDescending(i => i.TotalPerfectionScore).ThenByDescending(i => i.Level).ThenBy(i => i.UID).FirstOrDefault();
//                using (var cmd = new Database.MySqlCommand(Database.MySqlCommandType.SELECT).Select("items").Where("EntityID", BestPlayer.UID).And("Position", 0, true))
//                using (var reader = new Database.MySqlReader(cmd))
//                {
//                    try
//                    {
//                        while (reader.Read())
//                        {
//                            if (reader.ReadByte("Position") == 1) Stuff.Add(GetPositionName(1), reader.ReadUInt32("ID"));
//                            if (reader.ReadByte("Position") == 3) Stuff.Add(GetPositionName(3), reader.ReadUInt32("ID"));
//                            if (reader.ReadByte("Position") == 5) Stuff.Add(GetPositionName(5), reader.ReadUInt32("ID"));
//                            if (reader.ReadByte("Position") == 9) Stuff.Add(GetPositionName(9), reader.ReadUInt32("ID"));
//                            if (reader.ReadByte("Position") == 16) Stuff.Add(GetPositionName(16), reader.ReadUInt32("ID"));
//                            if (reader.ReadByte("Position") == 17) Stuff.Add(GetPositionName(17), reader.ReadUInt32("ID"));
//                            if (reader.ReadByte("Position") == 4) Stuff.Add(GetPositionName(4), reader.ReadUInt32("ID"));
//                            if (reader.ReadByte("Position") == 15) Stuff.Add(GetPositionName(15), reader.ReadUInt32("ID"));
//                            if (reader.ReadByte("Position") == 19)
//                            {
//                                Stuff.Add(GetPositionName(19), reader.ReadUInt32("ID"));
//                                Stuff.Add(GetPositionName(255), reader.ReadByte("Plus"));
//                            }
//                        }
//                        if (!Stuff.Keys.Contains("MountArmor"))
//                        {
//                            using (var cmd2 = new Database.MySqlCommand(Database.MySqlCommandType.SELECT).Select("items").Where("EntityID", BestPlayer.UID).And("Position", 0, true))
//                            using (var reader2 = new Database.MySqlReader(cmd2))
//                            {
//                                while (reader2.Read())
//                                {
//                                    if (reader2.ReadByte("Position") == 12) Stuff.Add(GetPositionName(17), reader2.ReadUInt32("ID"));
//                                }
//                            }
//                        }
//                    }
//                    catch { }
//                }
//            }
//            catch { }
//        }
//        public static string GetPositionName(byte Position)
//        {
//            if (Position == 1) return "Hat";
//            if (Position == 3) return "Armor";
//            if (Position == 5) return "LeftWep";
//            if (Position == 9) return "Garment";
//            if (Position == 16) return "LeftWepAcc";
//            if (Position == 17) return "MountArmor";
//            if (Position == 4) return "RightWep";
//            if (Position == 15) return "RightWepAcc";
//            if (Position == 19) return "Wing";
//            if (Position == 255) return "WingLevel";
//            return "Unknwon";
//        }
//        public static Dictionary<string, uint> Stuff = new Dictionary<string, uint>();
//        public static Game.Entity BestPlayer;
//        public void Handle(Client.GameState client)
//        {
//            switch (Info.ActionId)
//            {
//                case 0:
//                    {
//                        try
//                        {
//                            new CMsgUserAbilityScore().GetRankingList();
//                            new MsgRankMemberShow().UpdateBestPlayer();
//                            new CMsgEquipRefineRank().UpdateRanking();
//                            PrestigeRank.Load();
//                            if (PrestigeRank.S != null)
//                                client.Send(PrestigeRank.S);
//                        }
//                        catch { }
//                        break;
//                    }
//                case 1:
//                    {
//                        if (Kernel.GamePool.ContainsKey(Info.proto == null ? 0 : Info.proto.dwParam))
//                        {
//                            GeneralData.ObserveEquipment(new MsgAction(true) { ID = MsgAction.Mode.ObserveEquipment, UID = client.Player.UID, dwParam = Info.proto == null ? 0 : Info.proto.dwParam }, client);
//                        }
//                        else
//                        {
//                            client.Send(Kernel.FinalizeProtoBuf(Info, 3257));
//                        }
//                        break;
//                    }
//            }

//        }
//        public bool Read(byte[] packet)
//        {
//            var mypkt = new byte[packet.Length - 4];
//            Array.Copy(packet, 4, mypkt, 0, mypkt.Length);
//            try
//            {
//                using (var memoryStream = new MemoryStream(packet))
//                {
//                    Info = Serializer.DeserializeWithLengthPrefix<MsgRankMemberShowProto>(memoryStream, PrefixStyle.Fixed32);
//                }
//            }
//            catch (Exception e)
//            {
//                Console.WriteLine(e);
//                return false;
//            }
//            return true;
//        }
//    }
//}