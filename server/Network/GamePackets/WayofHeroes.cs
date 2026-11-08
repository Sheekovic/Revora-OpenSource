// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.IO;
using System.Linq;
using Revora.Client;
using Revora.Database;
using System.Collections.Generic;

namespace Revora.Network.GamePackets
{
    public unsafe class WayofHeroes : Writer, Interfaces.IPacket
    {
        byte[] buffer;
        int offset = 11;
        public WayofHeroes(uint page)
        {
            buffer = new byte[11 + 8];
            Writer.Ushort((ushort)(buffer.Length - 8), 0, buffer);
            Writer.Ushort(2831, 2, buffer);
            Page = page;
        }
        private uint Page
        {
            get { return BitConverter.ReadUint(buffer, 4); }
            set { WriteUint(value, 4, buffer); }
        }
        private bool AllDone
        {
            get { return buffer[8] == 1; }
            set { buffer[8] = value == true ? (byte)1 : (byte)0; }
        }
        private ushort Count
        {
            get { return BitConverter.ReadUshort(buffer, 9); }
            set { Ushort(value, 9, buffer); }
        }
        public struct Stage
        {
            public uint UID;
            public uint Progress;
            public uint[] Prize;
        }
        public class StageInfo
        {
            public uint UID;
            public bool Done;
            public SafeDictionary<uint, SubStageInfo> SubStages = new SafeDictionary<uint, SubStageInfo>();
            public void WriteItem(BinaryWriter writer)
            {
                writer.Write(UID); //= reader.ReadUInt32();
                writer.Write(Done);
                writer.Write((byte)SubStages.Count);
                foreach (var prize in SubStages.Values)
                    prize.WriteItem(writer);
            }
            public StageInfo ReadItem(BinaryReader reader)
            {
                UID = reader.ReadUInt32();//4
                Done = reader.ReadBoolean();//8
                int count = reader.ReadByte();
                for (uint x = 0; x < count; x++)
                {
                    SubStageInfo item = new SubStageInfo();
                    item = item.ReadItem(reader);
                    SubStages.Add(item.UID, item);
                }
                return this;
            }
        }
        public class SubStageInfo
        {
            public uint UID;
            public bool Done;
            public uint points;
            public void WriteItem(BinaryWriter writer)
            {
                writer.Write(UID);
                writer.Write(Done);
                writer.Write(points);
            }
            public SubStageInfo ReadItem(BinaryReader reader)
            {
                UID = reader.ReadUInt32();
                Done = reader.ReadBoolean();
                points = reader.ReadUInt32();
                return this;
            }
        }
        public static void Load()
        {
            Console.WriteLine("Cargando StageGoal information!");
            Stages = new Dictionary<uint, Stage>();
            string[] text = File.ReadAllLines("database\\Slayer\\stagegoal.txt");
            Stage Stage = new Stage();
            for (int x = 0; x < text.Length; x++)
            {
                //System.Console.Write("\b{0}", Cargando.NextChar());
                string line = text[x];
                string[] split = line.Split('=');
                if (line.StartsWith("[") && line.EndsWith("]") && !line.Contains("StageGoal"))
                {
                    string[] numbers = line.Replace("[", "").Replace("]", "").Split('-');
                    uint id = uint.Parse(numbers[0]) * 100;
                    if (numbers.Length > 1)
                        id += uint.Parse(numbers[1]);
                    if (Stages.ContainsKey(id))
                        Stage = Stages[uint.Parse(split[1])];
                    else
                    {
                        Stage = new Stage();
                        Stage.UID = id;
                        Stage.Prize = new uint[3];
                        Stages.Add(id, Stage);
                    }
                }
                else
                {
                    if (split[0] == "Progress")
                        Stage.Progress = uint.Parse(split[1]);
                    else if (split[0] == "Prize1")
                    {
                        string[] PrizeLine = split[1].Split(new string[] { "@@" }, StringSplitOptions.RemoveEmptyEntries);
                        for (int i = 0; i < PrizeLine.Length; i++)
                        {
                            Stage.Prize[i] = uint.Parse(PrizeLine[i]);
                        }

                    }
                }
            }
            //Console.WriteLine("Done!");
        }
        public void check(StageInfo stages)
        {
            if (stages.SubStages.Count > 0)
            {
                byte[] bytes = new byte[11 + 8 + stages.SubStages.Count * 9];
                buffer.CopyTo(bytes, 0);
                WriteUInt16((ushort)(bytes.Length - 8), 0, bytes);
                buffer = bytes;
                Count = (ushort)stages.SubStages.Count;
                AllDone = stages.Done;
                var items = stages.SubStages.Values.ToArray();
                for (uint i = 0; i < items.Length; i++)
                {
                    Uint(items[i].UID, offset, buffer); offset += 8;
                    buffer[offset++] = items[i].Done == true ? (byte)1 : (byte)0;
                }
            }
        }
        public static void Load(GameState client)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT))
            {
                cmd.Select("wayofheroes").Where("UID", client.Entity.UID);
                using (MySqlReader rdr = new MySqlReader(cmd))
                {
                    if (rdr.Read())
                    {
                        byte[] data = rdr.ReadBlob("wayofheroes");
                        if (data.Length > 0)
                        {
                            using (var stream = new MemoryStream(data))
                            using (var reader = new BinaryReader(stream))
                            {
                                int count = reader.ReadByte();
                                for (uint x = 0; x < count; x++)
                                {
                                    StageInfo item = new StageInfo();
                                    item = item.ReadItem(reader);
                                    client.Way2Heroes.Add(item.UID, item);
                                }
                            }
                        }
                    }
                    else
                    {
                        using (var command = new MySqlCommand(MySqlCommandType.INSERT))
                        {
                            command.Insert("WayofHeroes").Insert("UID", client.Entity.UID).Insert("Name", client.Entity.Name);
                            command.Execute();
                        }
                    }
                }
            }
            Revora.Game.Features.WayOfHeroesClaims.Restore(client);
        }
        public static void Save(GameState client)
        {
            Revora.Game.Features.WayOfHeroesClaims.Save(client);
        }
        public static Dictionary<uint, Stage> Stages;
        public static void SendDone(GameState client)
        {
            byte[] Buffer = new byte[106 + 8];
            WriteUInt16((ushort)(Buffer.Length - 8), 0, Buffer);
            WriteUInt16(2832, 2, Buffer);
            Ushort(10, 4, Buffer);//count
            int offset = 6;
            for (uint i = 0; i < 10; i++)
            {
                Uint(i + 1, offset, Buffer); offset += 5;//id
                Buffer[offset++] = 0;//has something to claim
                Uint(4, offset, Buffer); offset += 4;//items done
            }
            client.Send(Buffer);
        }
        public byte[] ToArray()
        {
            return buffer;
        }
        public void Send(GameState client)
        {
            client.Send(this.ToArray());
        }
        public void Deserialize(byte[] Data)
        {
            buffer = Data;
        }
        public static void Process(byte[] packet, GameState client)
        {
            if (!Revora.Game.Features.WayOfHeroesClaims.ValidPacket(packet, client)) return;
            byte type = packet[4];
            if (type != 0)
            {
                Revora.Game.Features.WayOfHeroesClaims.Claim(packet, client);
                return;
            }
            uint stage = BitConverter.ReadUint(packet, 5);
            if (stage < 1 || stage > 10) return;
            lock (client.ItemSyncRoot)
            {
                if (!Revora.Game.Features.WayOfHeroesClaims.ValidPacket(packet, client)) return;
                WayofHeroes Way2Heroes = new WayofHeroes(stage);
                if (client.Way2Heroes.ContainsKey(stage)) Way2Heroes.check(client.Way2Heroes[stage]);
                client.Send(Way2Heroes);
            }
        }
    }
}