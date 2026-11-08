using System;
using System.IO;
using System.Linq;
using Revora.Client;
using System.Collections.Generic;
using Revora.Network.GamePackets;
using System.Drawing;

namespace Revora.Game.ConquerStructures
{
    public unsafe class Nobility : Network.Writer
    {
        public const ulong CpSilverValue = 50000UL;
        internal const ulong MinimumRankDonation = 30000000UL;
        internal const int KingCount = 5, PrinceEnd = 55;
        public static ulong MaxDonation = 0;
        public static ulong MaxDonation1 = 0;
        public static ulong MaxDonation2 = 0;
        public static SafeDictionary<uint, NobilityInformation> Board = new SafeDictionary<uint, NobilityInformation>(10000);
        public static List<NobilityInformation> BoardList = new List<NobilityInformation>(10000);
        internal static readonly object ResetGate = new object();
        internal static Func<DateTime> Clock = () => DateTime.Now;
        private static DateTime LastWeeklyReset = DateTime.MinValue;

        public static void Handle(NobilityInfo information, Client.GameState client)
        {
            switch (information.Type)
            {
                case NobilityInfo.Donate:
                    {
                        if (client.Trade != null && client.Trade.InTrade)
                            return;
                        if (client.Entity.MapID == 3031)
                            return;
                        ulong silvers = information.NoblezaOro;
                        if (silvers == 0 || silvers > (ulong)long.MaxValue || information.MoneyType > 1
                            || client.NobilityInformation == null || client.NobilityInformation.Donation > (ulong)long.MaxValue - silvers)
                            return;
                        Features.NobilityDonations.Pay(client, silvers, information.MoneyType);
                        break;
                    }
                case NobilityInfo.NextRank:
                    {
                        try
                        {
                            ulong[] Contributions = new ulong[13];
                            lock (ResetGate) {
                                Contributions[7] = RequiredDonation(client.NobilityInformation, NobilityRank.Duke);
                                Contributions[9] = RequiredDonation(client.NobilityInformation, NobilityRank.Prince);
                                Contributions[12] = RequiredDonation(client.NobilityInformation, NobilityRank.King);
                            }
                            MemoryStream strm = new MemoryStream();
                            BinaryWriter wtr = new BinaryWriter(strm);
                            wtr.Write((ushort)0);
                            wtr.Write((ushort)2064);
                            wtr.Write((uint)4);
                            byte[] Ranks = { 1, 3, 5, 7, 9, 12 };
                            foreach (var Rank in Ranks)
                            {
                                wtr.Write((ulong)Contributions[Rank]);
                                wtr.Write((uint)4294967295);
                                wtr.Write((uint)Rank);
                            }
                            int packetlength = (int)strm.Length;
                            strm.Position = 0;
                            wtr.Write((ushort)packetlength);
                            strm.Position = strm.Length;
                            wtr.Write(Server.Encoding.GetBytes("TQServer"));
                            strm.Position = 0;
                            byte[] buf = new byte[strm.Length];
                            strm.Read(buf, 0, buf.Length);
                            client.Send(buf);
                            wtr.Close();
                            strm.Close();
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                            Server.SaveException(e);
                        }
                        break;
                    }
                case NobilityInfo.List:
                    {
                        byte[] packet = new byte[600 + 8];
                        Network.Writer.WriteUInt16(600, 0, packet);
                        Network.Writer.WriteUInt16(2064, 2, packet);
                        Network.Writer.WriteUInt16(2, 4, packet);
                        Network.Writer.WriteUInt16(5, 10, packet);
                        Network.Writer.WriteUInt16(10, 12, packet);
                        int offset = 120;
                        for (int i = (int)(information.wParam1 * 10); i < information.wParam1 * 10 + 10 && i < BoardList.Count; i++)
                        {
                            var nob = BoardList[i];
                            Network.Writer.WriteUInt32(nob.EntityUID, offset, packet);
                            offset += 4;
                            if (Kernel.GamePool.ContainsKey(nob.EntityUID))
                            {
                                Network.Writer.WriteUInt32(1, offset, packet);
                                offset += 4;
                                Network.Writer.WriteUInt32(nob.Mesh, offset, packet);
                                offset += 4;
                            }
                            else
                            {
                                offset += 8;
                            }
                            Network.Writer.WriteString(nob.Name, offset, packet);
                            offset += 20;
                            Network.Writer.WriteUInt64(nob.Donation, offset, packet);
                            offset += 8;
                            Network.Writer.WriteUInt32((uint)nob.Rank, offset, packet);
                            offset += 4;
                            Network.Writer.WriteUInt32((uint)i, offset, packet);
                            offset += 4;
                        }
                        client.Send(packet);
                        break;
                    }
            }
        }
        public static void Donate(ulong silvers, Client.GameState client)
        {
            Features.NobilityDonations.Pay(client, silvers, 0);
        }

        public static void Pulse()
        {
            DateTime week = WeekStart(Clock());
            lock (ResetGate)
            {
                if (LastWeeklyReset >= week) return;
                bool reset = Features.NobilityReset.Advance(week, ReadLastReset());
                Features.NobilityReset.Project();
                LastWeeklyReset = week;
                if (reset)
                {
                    Kernel.SendWorldMessage(new Message("Nobility has been cleared for the new week. Donate again to claim a title.", Color.Yellow, Message.Center), Server.GamePool);
                    Console.WriteLine("Nobility weekly reset " + week.ToString("yyyy-MM-dd"));
                }
            }
        }

        private static DateTime WeekStart(DateTime now)
        {
            return now.Date.AddDays(-(int)now.DayOfWeek);
        }

        private static string ResetFile
        {
            get { return Path.Combine(Revora.Constants.DataHolderPath, "NobilityReset.txt"); }
        }

        private static DateTime ReadLastReset()
        {
            try
            {
                string path = ResetFile;
                if (!File.Exists(path))
                    return DateTime.MinValue;
                string text = File.ReadAllText(path).Trim();
                DateTime stamp;
                if (DateTime.TryParse(text, out stamp))
                    return stamp.Date;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[nobility] reset-read " + ex.Message);
            }
            return DateTime.MinValue;
        }

        public static void Sort(uint updateUID)
        {
            lock (ResetGate) SortCore(updateUID);
        }
        internal static ulong RequiredDonation(NobilityInformation donor, NobilityRank rank) {
            if (rank == NobilityRank.Duke) return 0;
            ulong target = MinimumRankDonation;
            int slots = rank == NobilityRank.King ? KingCount : rank == NobilityRank.Prince ? PrinceEnd : 0;
            if (slots != 0) {
                var rivals = Board.Values.Where(p => p.EntityUID != donor.EntityUID && p.Donation >= MinimumRankDonation)
                    .OrderByDescending(p => p.Donation).ThenBy(p => p.EntityUID).Take(slots).ToArray();
                if (rivals.Length == slots) {
                    var last = rivals[slots - 1];
                    target = Math.Max(target, last.Donation + (donor.EntityUID > last.EntityUID ? 1UL : 0UL));
                }
            }
            return donor.Donation >= target ? 0 : target - donor.Donation;
        }
        private static void SortCore(uint updateUID)
        {
            SafeDictionary<uint, NobilityInformation> sortedBoard = new SafeDictionary<uint, NobilityInformation>();

            int Place = 0;
            MaxDonation = MaxDonation1 = MaxDonation2 = 0;
            foreach (NobilityInformation entry in Board.Values.OrderByDescending(p => p.Donation).ThenBy(p => p.EntityUID))
            {
                Client.GameState client = null;
                try
                {
                    int previousPlace = entry.Position;
                    entry.Position = Place;
                    NobilityRank Rank = NobilityRank.Duke;

                    if (entry.Donation >= MinimumRankDonation) {
                        Rank = Place < KingCount ? NobilityRank.King : Place < PrinceEnd ? NobilityRank.Prince : NobilityRank.Duke;
                        if (Rank == NobilityRank.King) MaxDonation = entry.Donation;
                        else if (Rank == NobilityRank.Prince) MaxDonation1 = entry.Donation;
                        else MaxDonation2 = entry.Donation;
                    }
                    var oldRank = entry.Rank;
                    entry.Rank = Rank;
                    if (Kernel.GamePool.TryGetValue(entry.EntityUID, out client))
                    {
                        bool updateTheClient = false;
                        if (oldRank != Rank)
                        {
                            updateTheClient = true;
                            if (Rank == NobilityRank.Baron)
                            {
                                Kernel.SendWorldMessage(new Message("Congratulations! " + client.Entity.Name + " you get the title of Baron.", System.Drawing.Color.White, Message.TopLeft), Server.GamePool);
                            }
                            if (Rank == NobilityRank.Earl)
                            {
                                Kernel.SendWorldMessage(new Message("Congratulations! " + client.Entity.Name + " you get the title of Count.", System.Drawing.Color.White, Message.TopLeft), Server.GamePool);
                            }
                            if (Rank == NobilityRank.Duke)
                            {
                                Kernel.SendWorldMessage(new Message("Congratulations! " + client.Entity.Name + " you have earned the rank of Duke.", System.Drawing.Color.White, Message.Center), Server.GamePool);
                            }
                            if (Rank == NobilityRank.Prince)
                            {
                                Kernel.SendWorldMessage(new Message("Congratulations! " + client.Entity.Name + " you become a new prince.", System.Drawing.Color.White, Message.Center), Server.GamePool);
                            }
                            if (Rank == NobilityRank.King)
                            {
                                Kernel.SendWorldMessage(new Message("Congratulations! " + client.Entity.Name + " Become the new King / Queen.", System.Drawing.Color.White, Message.Center), Server.GamePool);
                            }
                            if (Rank == NobilityRank.Knight)
                            {
                                Kernel.SendWorldMessage(new Message("Congratulations! " + client.Entity.Name + " become a lord.", System.Drawing.Color.White, Message.TopLeft), Server.GamePool);
                            }
                        }
                        else
                        {
                            if (previousPlace != Place)
                            {
                                updateTheClient = true;
                            }
                        }
                        if (updateTheClient || client.Entity.UID == updateUID)
                        {
                            NobilityInfo update = new NobilityInfo(true);
                            update.Type = NobilityInfo.Icon;
                            update.dwParam = entry.EntityUID;
                            update.UpdateString(entry);
                            client.SendScreen(update, true);
                            client.Entity.NobilityRank = entry.Rank;
                        }
                    }
                    sortedBoard.Add(entry.EntityUID, entry);
                    Place++;
                }
                catch { }
            }
            Board = sortedBoard;
            lock (BoardList)
                BoardList = Board.Values.ToList();
        }
    }
    public class NobilityInformation
    {
        public string Name;
        public uint EntityUID;
        public uint Mesh;
        public ulong Donation;
        public byte Gender;
        public int Position;
        public NobilityRank Rank = NobilityRank.Duke;
        public bool KingTitle = false;
        public int Rey;
    }

    public enum NobilityRank : byte
    {
        Serf = 0,
        Knight = 1,
        Baron = 3,
        Earl = 5,
        Duke = 7,
        Prince = 9,
        King = 12
    }
}
