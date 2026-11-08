using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Revora.Game.ConquerStructures.Society;
using Revora.GamePackets;
using Revora.Client;
using Revora.Database;
using Revora.Network;

namespace Revora.Network
{
    public class Advertise2
    {
        public static void Execute2(Client.GameState client, byte[] packet)
        {
            try
            {
                Advertise_client[] _clientArray = Advertise.Top8.Values.ToArray<Advertise_client>();
                _clientArray = (from guil in _clientArray orderby guil.Rank ascending select guil).ToArray();
                uint all_advertise = (ushort)_clientArray.Length;

                uint Page = BitConverter.ToUInt32(packet, 4);

                if (Page > all_advertise) return;
                ushort x_count = (ushort)(all_advertise - Page);

                ushort count = (ushort)Math.Min((int)x_count, 4);
                if (count <= 2)
                {
                    Advertise adv = new Advertise(count);
                    adv.AllRegistred = (ushort)_clientArray.Length;
                    adv.AtCount = (ushort)Page;
                    for (uint x = Page; x < Page + count; x++)
                    {
                        var element = _clientArray[x];
                        adv.Aprend(element);
                    }
                    client.Send(adv.ToArray());
                }
                else
                {

                    ushort Other_count = (ushort)(count - 2);
                    ushort countx = (ushort)(count - Other_count);
                    Advertise adv = new Advertise(countx);
                    adv.AllRegistred = (ushort)_clientArray.Length;
                    adv.AtCount = (ushort)Page;
                    for (uint x = Page; x < Page + countx; x++)
                    {
                        var element = _clientArray[x];
                        adv.Aprend(element);
                    }
                    client.Send(adv.ToArray());

                    adv = new Advertise(Other_count);
                    adv.AllRegistred = (ushort)_clientArray.Length;
                    adv.AtCount = (ushort)Page;
                    for (uint x = countx; x < Page + Other_count; x++)
                    {
                        var element = _clientArray[x];
                        adv.Aprend(element);
                    }
                    client.Send(adv.ToArray());

                }

            }
            catch (Exception) { }
        }
        public static void Execute(Client.GameState client, byte[] packet)
        {
            try
            {
                Advertise_client[] _clientArray = Advertise.Top8.Values.ToArray<Advertise_client>();
                _clientArray = (from guil in _clientArray orderby guil.Rank ascending select guil).ToArray();
                uint all_advertise = (ushort)_clientArray.Length;
                uint Receive_count = BitConverter.ToUInt32(packet, 4);
                if (Receive_count > all_advertise) return;
                ushort x_count = (ushort)(all_advertise - Receive_count);
                ushort count = (ushort)Math.Min((int)x_count, 4);
                uint num = Math.Max(0, Receive_count);
                if (count <= 2)
                {
                    Advertise adv = new Advertise(count);
                    adv.AllRegistred = (ushort)_clientArray.Length;
                    adv.AtCount = (ushort)Receive_count;
                    adv.First = 1;
                    for (ushort x = 0; x < count; x++)
                    {
                        var element = _clientArray[x + num];
                        adv.Aprend(element);
                    }
                    client.Send(adv.ToArray());
                }
                else
                {
                    ushort Other_count = (ushort)(count - 2);
                    ushort countx = (ushort)(count - Other_count);

                    Advertise adv = new Advertise(countx);
                    adv.AllRegistred = (ushort)_clientArray.Length;
                    adv.AtCount = (ushort)Receive_count;
                    adv.First = 1;
                    for (ushort x = 0; x < countx; x++)
                    {
                        var element = _clientArray[x + num];
                        adv.Aprend(element);
                    }
                    client.Send(adv.ToArray());

                    adv = new Advertise(Other_count);
                    adv.AllRegistred = (ushort)_clientArray.Length;
                    adv.AtCount = (ushort)Receive_count;
                    for (ushort x = 0; x < Other_count; x++)
                    {
                        var element = _clientArray[x + countx + num];
                        adv.Aprend(element);
                    }
                    client.Send(adv.ToArray());
                    return;
                }

            }
            catch (Exception) { }
        }
    }
}
namespace Revora.GamePackets
{
    public class Advertise_client
    {
        public uint Rank;
        public uint UID;
        public string Name;
        public string LeaderName;
        public string Buletin;
        public uint Level;
        public uint MemberCount;
        public ulong SilverFund;


        public Advertise_client(Guild client)
        {
            this.Rank = 0;
            this.UID = 0;
            this.Name = "";
            this.LeaderName = "";
            this.Buletin = "";
            this.Level = 0;
            this.MemberCount = 0;
            this.SilverFund = 0;

            this.UID = client.ID;
            this.Level = (uint)client.Level;
            this.MemberCount = client.MemberCount;
            this.Name = client.Name;
            this.LeaderName = client.LeaderName;
            this.Buletin = client.Bulletin;
            this.SilverFund = client.SilverFund;
        }

        public Advertise_client(uint _rank, uint _uid, uint _level, ushort _count, string _name, string _lname, string _bulletin, ulong _fund)
        {
            this.Rank = 0;
            this.UID = 0;
            this.Name = "";
            this.LeaderName = "";
            this.Buletin = "";
            this.Level = 0;
            this.MemberCount = 0;
            this.SilverFund = 0;
            this.Rank = _rank;
            this.UID = _uid;
            this.Level = _level;
            this.MemberCount = _count;
            this.Name = _name;
            this.LeaderName = _lname;
            this.Buletin = _bulletin;
            this.SilverFund = _fund;
        }
    }
    public class Advertise : Revora.Network.Writer, Interfaces.IPacket
    {
        public static Dictionary<uint, Advertise_client> Top8 = new Dictionary<uint, Advertise_client>(1000);

        public static Advertise_client[] AdvertiseRanks = new Advertise_client[0];

        //public static void LoadTop8()
        //{
        //    MySqlCommand cmd = new MySqlCommand(MySqlCommandType.SELECT);
        //    cmd.Select("adv");
        //    MySqlReader r = new MySqlReader(cmd);
        //    while (r.Read())
        //    {
        //        Advertise_client client = new Advertise_client(r.ReadUInt32("Rank"), r.ReadUInt32("UID"), r.ReadUInt32("Level"), r.ReadUInt16("MemberCount"), r.ReadString("Name"), r.ReadString("LeaderName"), r.ReadString("Buletin"), r.ReadUInt64("SilverFund"));
        //        if (Kernel.Guilds.ContainsKey(client.UID))
        //        {
        //            if (!Top8.ContainsKey(client.UID))
        //                Top8.Add(client.UID, client);
        //        }
        //    }
        //}

        public static void CalculateRanks()
        {
            lock (AdvertiseRanks)
            {
                Advertise_client[] array = Top8.Values.ToArray();
                array = (from guil in array orderby guil.Rank ascending select guil).ToArray();
                List<Advertise_client> listarray = new List<Advertise_client>();
                for (ushort x = 0; x < array.Length; x++)
                {
                    listarray.Add(array[x]);
                    if (x == 40) break;
                }
                AdvertiseRanks = listarray.ToArray();
            }
        }

        //public static void SaveTop8(Guild g)
        //{
        //    Advertise_client clients = new Advertise_client(
        //        (uint)(Top8.Count + 1)
        //        , (uint)g.ID
        //        , (uint)g.Level
        //        , (ushort)g.MemberCount
        //        , (string)g.Name
        //        , (string)g.LeaderName
        //        , (string)g.Bulletin
        //        , (ulong)g.SilverFund
        //            );
        //    if (!Top8.ContainsKey(clients.UID))
        //        Top8.Add(clients.UID, clients);
        //    MySqlCommand cmd = new MySqlCommand(MySqlCommandType.INSERT);
        //    cmd.Insert("adv")
        //        .Insert("Rank", clients.Rank).Insert("UID", clients.UID)
        //        .Insert("Level", clients.Level).Insert("MemberCount", clients.MemberCount)
        //        .Insert("Name", clients.Name).Insert("LeaderName", clients.LeaderName)
        //  .Insert("Buletin", clients.Buletin).Insert("SilverFund", clients.SilverFund);
        //    cmd.Execute();
        //}


        private Byte[] _buffer;
        public Advertise(ushort counts = 0)
        {
            ushort num = (ushort)(40 + (counts * 344));
            _buffer = new byte[num];
            Writer.Write((ushort)(num - 8), 0, _buffer);
            Writer.Write(2226, 2, _buffer);
            Writer.Write(counts, 8, _buffer);

        }
        public uint First { get { return BitConverter.ToUInt32(_buffer, 16); } set { Writer.Write(value, 16, _buffer); } }
        public ushort AtCount { get { return BitConverter.ToUInt16(_buffer, 4); } set { Writer.Write(value, 4, _buffer); } }
        public ushort AllRegistred { get { return BitConverter.ToUInt16(_buffer, 12); } set { Writer.Write(value, 12, _buffer); } }

        ushort Position = 24;
        public void Aprend(Advertise_client guild)
        {
            Writer.Write(guild.UID, Position, _buffer);
            Position += 4;
            Writer.Write(guild.Buletin, Position, _buffer);
            Position += 255;//9//255
            Writer.Write(guild.Name, Position, _buffer);
            Position += 36;//36
            Writer.Write(guild.LeaderName, Position, _buffer);
            Position += 17;
            Writer.Write(Kernel.Guilds[guild.UID].Level, Position, _buffer);
            Position += 4;
            Writer.Write((ushort)Kernel.Guilds[guild.UID].MemberCount, Position, _buffer);
            Position += 4;
            Writer.Write(Kernel.Guilds[guild.UID].SilverFund, Position, _buffer);
            Position += 8;
            Write(1, Position, _buffer);
            Position += 2;
            Writer.Write(1, Position, _buffer);
            Position += 14;//20, era 14

        }
        public void Send(Client.GameState client)
        {
            client.Send(this.ToArray());
        }

        public void Deserialize(byte[] Data)
        {
            _buffer = Data;
        }

        public byte[] ToArray()
        {
            return _buffer;
        }
    }
}
