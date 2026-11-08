using System;
using Revora.Game;
using Revora.Database;
using CountryLookupProj;
using System.Collections.Generic;
using Revora.Network.GamePackets;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace Revora
{
    public unsafe class Kernel
    {
        #region GoldenTree
        public static Database.GoldenTreeTable.GoldenTreeCondition GoldenTree;
        public static uint GoldenTreeTimes = 0;
        public static uint RandomTree(Database.GoldenTreeTable.GoldenTreePool[] nums)
        {
            if (nums == null || nums.Length == 0) return 0;
            return nums[Random.Next(0, nums.Length)].ID;
        }
        #endregion
        #region Union
        public static uint ServerKingdom;
        public static Dictionary<uint, Revora.Network.GamePackets.Union.UnionClass> Unions = new Dictionary<uint, Revora.Network.GamePackets.Union.UnionClass>();
        #endregion
        #region Read7BitesPerfectionSystem
        public static byte[] FinalizeProtoBuf(object proto, ushort packetid)
        {
            byte[] protobuff;
            using (var ms = new System.IO.MemoryStream())
            {
                ProtoBuf.Serializer.Serialize(ms, proto);
                protobuff = ms.ToArray();
                byte[] buffer;
                buffer = new byte[12 + protobuff.Length];
                System.Buffer.BlockCopy(protobuff, 0, buffer, 4, protobuff.Length);
                Network.Writer.Write(buffer.Length - 8, 0, buffer);
                Network.Writer.Write(packetid, 2, buffer);
                return buffer;
            }
        }
        public static byte[] CreateProtocolBuffer(params uint[] values)
        {
            List<byte> ptr = new List<byte>();
            ptr.Add(8);
            for (int x = 0; x < values.Length; x++)
            {
                uint value = values[x];
                while (value > 0x7F)
                {
                    ptr.Add((byte)((value & 0x7F) | 0x80));
                    value >>= 7;
                }
                ptr.Add((byte)(value & 0x7F));
                ptr.Add((byte)(8 * (x + 2)));
                if (x + 1 == values.Length)
                    break;
            }
            return ptr.ToArray();
        }
        public static uint[] Read7BitEncodedInt(byte[] buffer)
        {
            List<uint> ptr2 = new List<uint>();

            for (int i = 0; i < buffer.Length; )
            {
                if (i + 2 <= buffer.Length)
                {
                    int tmp = buffer[i++];

                    if (tmp % 8 == 0)
                        while (true)
                        {
                            if (i + 1 > buffer.Length) break;
                            tmp = buffer[i++];
                            if (tmp < 128)
                            {
                                ptr2.Add((uint)tmp);
                                break;
                            }
                            else
                            {
                                int result = tmp & 0x7f;
                                if ((tmp = buffer[i++]) < 128)
                                {
                                    result |= tmp << 7;
                                    ptr2.Add((uint)result);
                                    break;
                                }
                                else
                                {
                                    result |= (tmp & 0x7f) << 7;
                                    if ((tmp = buffer[i++]) < 128)
                                    {
                                        result |= tmp << 14;
                                        ptr2.Add((uint)result);
                                        break;
                                    }
                                    else
                                    {
                                        result |= (tmp & 0x7f) << 14;
                                        if ((tmp = buffer[i++]) < 128)
                                        {
                                            result |= tmp << 21;
                                            ptr2.Add((uint)result);
                                            break;
                                        }
                                        else
                                        {
                                            result |= (tmp & 0x7f) << 21;
                                            result |= (tmp = buffer[i++]) << 28;
                                            ptr2.Add((uint)result);
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                }
                else break;
            }
            return ptr2.ToArray();
        }
        #endregion
        public static ConcurrentDictionary<uint, ConquerItem> AuctionItems = new ConcurrentDictionary<uint, ConquerItem>();
        public static CountryLookup Lookup = new CountryLookup(System.Windows.Forms.Application.StartupPath + "\\GeoIP.dat");
        public static readonly ConcurrentDictionary<uint, Game.Features.Reincarnation.ReincarnateInfo> ReincarnatedCharacters = new ConcurrentDictionary<uint, Game.Features.Reincarnation.ReincarnateInfo>();
        public static SafeDictionary<uint, Game.ConquerStructures.Society.Guild> Guilds_adv = new SafeDictionary<uint, Revora.Game.ConquerStructures.Society.Guild>();
        public static SafeDictionary<uint, Game.ConquerStructures.Society.Guild> Guilds = new SafeDictionary<uint, Revora.Game.ConquerStructures.Society.Guild>();
        public static Dictionary<UInt32, Refinery.RefineryBoxes> DatabaseRefineryBoxes = new Dictionary<UInt32, Refinery.RefineryBoxes>();
        public static ConcurrentDictionary<uint, Database.AccountTable> AwaitingPool = new ConcurrentDictionary<uint, Database.AccountTable>();
        public static ConcurrentDictionary<uint, Client.GameState> DisconnectPool = new ConcurrentDictionary<uint, Client.GameState>();
        public static Dictionary<UInt32, Refinery.RefineryItem> DatabaseRefinery = new Dictionary<UInt32, Refinery.RefineryItem>();
        public static ConcurrentDictionary<uint, Client.GameState> GamePool = new ConcurrentDictionary<uint, Client.GameState>();
        public static ConcurrentDictionary<uint, Game.Entity> BlackSpoted = new ConcurrentDictionary<uint, Game.Entity>();
        public static Dictionary<uint, Game.PokerTable> PokerTables = new Dictionary<uint, Game.PokerTable>(50);
        public static SafeDictionary<uint, ConquerItem> Dropitem = new SafeDictionary<uint, ConquerItem>();
        public static SafeDictionary<ushort, Game.Map> Maps = new SafeDictionary<ushort, Game.Map>(1000);
        public static Dictionary<uint, Clan> Clans = new Dictionary<uint, Clan>();
        public static BlackSpotPacket BlackSpotPacket = new BlackSpotPacket();
        public static Game.ConquerStructures.QuizShow QuizShow;
        public static FastRandom Random = new FastRandom();
        public static List<char> InvalidCharacters = new List<char>() { ' ', '[', '{', '}', '(', ')', ']', '#', '*', '\\', '/', '<', '>', '"', '|', '=', '' };
        public static List<string> Insults = new List<string>() { "coño", "singa", "puta", "mmg", "mamague", "guebo", "rapa", "chinga", "paja", "pajia", "maldit", "perra", "Perra", "hijueputa", "mama", "gonorrea", "payaso", "maldito", "chupemela", "pene", "gay" };
        public static int boundID = 45, boundIDEnd = 46;
        public static bool
        Spawn = false,
        FactionWar = false,
        SpawnDragon = false,
        SpawnNemesis = false,
        SpawnBanshee = false,
        SpawnNemesis2 = false,
        SpawnBanshee2 = false,
        TreasureinBlue = false,
        DevastatedCity = false,
        SpawnBansheeSoul = false,
        SpawnBansheeSoulKeep = false;
        public static bool SacarGuardian = false;
        public static bool SacarReina = false;
        public static bool SacarPulpo = false;
        public static bool SacarDragon = false;
        public static bool SacarNemesis = false;
        public static bool SacarNemesis1 = false;
        public static bool SacarGordo = false;
        public static bool SacarBruja = false;
        public static bool SacarTrueno = false;
        public static bool SacarOso = false;
        public static bool SoldadoSlayer = false;
        public static bool SoldadoSlayer1 = false;
        public static bool SacarFaisan = false;

        public static Time32 SpawnBansheeSoulStamp;
        public static short GetDistance(ushort X, ushort Y, ushort X2, ushort Y2)
        {
            return (short)Math.Sqrt((X - X2) * (X - X2) + (Y - Y2) * (Y - Y2));
        }
        public static double GetDDistance(ushort X, ushort Y, ushort X2, ushort Y2)
        {
            return Math.Sqrt((X - X2) * (X - X2) + (Y - Y2) * (Y - Y2));
        }
        public static int GetDegree(int X, int X2, int Y, int Y2)
        {
            int direction = 0;

            double AddX = X2 - X;
            double AddY = Y2 - Y;
            double r = (double)Math.Atan2(AddY, AddX);
            if (r < 0) r += (double)Math.PI * 2;

            direction = (int)(360 - (r * 180 / Math.PI));

            return direction;
        }
        public static bool ChanceSuccess(double Chance)
        {
            double num2 = (((double)Random.Next(0x989680)) / 10000000.0) * 100.0;
            return (Chance >= num2);
        }
        public static ulong Gettime()
        {
            ulong Year = (ulong)DateTime.Now.Year;
            Year = Year - 1900;
            ulong Month = (ulong)DateTime.Now.Month;
            Month = Month - 1;
            ulong DayOfYear = (ulong)DateTime.Now.DayOfYear;
            ulong Day = (ulong)DateTime.Now.Day;
            ulong Hour = (ulong)DateTime.Now.Hour;
            ulong Minute = (ulong)DateTime.Now.Minute;
            ulong Second = (ulong)DateTime.Now.Second;
            Second += 8;
            ulong value = Year;
            value = value * 100 + Month;
            value = value * 1000 + DayOfYear;
            value = value * 100 + Day;
            value = value * 100 + Hour;
            value = value * 100 + Minute;
            value = value * 100 + Second;
            return value;
        }
        public static ulong TqTimer(DateTime timer)
        {
            var year = (ulong)(10000000000000 * (ulong)(timer.Year - 1900));
            var month = (ulong)(100000000000 * (ulong)(timer.Month - 1));
            var dayofyear = (ulong)(100000000 * (ulong)(timer.DayOfYear - 1));
            var day = (ulong)(timer.Day * 1000000);
            var Hour = (ulong)(timer.Hour * 10000);
            var Minute = (ulong)(timer.Minute * 100);
            var Second = (ulong)(timer.Second);
            return (ulong)(year + month + dayofyear + day + Hour + Minute + Second);
        }
        public static UInt64 ToDateTimeInt(DateTime dt)
        {
            return UInt64.Parse(dt.ToString("yyyyMMddHHmmss"));
        }
        public static UInt32 ToDateTimeUInt(DateTime dt)
        {
            return UInt32.Parse(dt.ToString("yyyyMMddHH"));
        }
        public static DateTime FromDateTimeInt(UInt64 val)
        {
            return new DateTime((Int32)(val / 10000000000),
                (Int32)((val % 10000000000) / 100000000),
                (Int32)((val % 100000000) / 1000000),
                (Int32)((val % 1000000) / 10000),
                (Int32)((val % 10000) / 100),
                (Int32)(val % 100));
        }
        private static Int64 RandSeed = 3721;
        [DllImport("winmm", EntryPoint = "timeGetTime", ExactSpelling = true, CharSet = System.Runtime.InteropServices.CharSet.Ansi, SetLastError = true)]
        private static extern long timeGetTime();
        public static int RandGet(int nMax, bool bReset = false)
        {
            if (bReset)
                RandSeed = timeGetTime();

            Int64 x = 0xffffffff;
            double i;
            ulong final;

            RandSeed *= ((Int64)134775813);
            RandSeed += 1;
            RandSeed = RandSeed % x;
            i = ((double)RandSeed) / (double)0xffffffff;
            final = (ulong)(nMax * i);

            return (int)final;
        }
        public static Game.Enums.ConquerAngle GetAngle(ushort X, ushort Y, ushort X2, ushort Y2)
        {
            double direction = 0;

            double AddX = X2 - X;
            double AddY = Y2 - Y;
            double r = (double)Math.Atan2(AddY, AddX);

            if (r < 0) r += (double)Math.PI * 2;

            direction = 360 - (r * 180 / (double)Math.PI);

            byte Dir = (byte)((7 - (Math.Floor(direction) / 45 % 8)) - 1 % 8);
            return (Game.Enums.ConquerAngle)(byte)((int)Dir % 8);
        }
        public static Boolean ValidClanName(String name)
        {
            lock (Clans)
            {
                foreach (Clan clans in Clans.Values)
                {
                    if (clans.Name == name)
                        return false;
                }
            }
            return true;
        }
        public static void SendWorldMessage(Interfaces.IPacket packet)
        {
            foreach (Client.GameState client in Server.GamePool)
            {
                if (client != null)
                {
                    client.Send(packet);
                }
            }
        }
        public static void SendWorldMessage(Interfaces.IPacket message, Client.GameState[] to)
        {
            foreach (Client.GameState client in to)
            {
                if (client != null)
                {
                    client.Send(message);
                }
            }
        }
        public static void Execute(Action<Client.GameState> action)
        {
            foreach (Client.GameState client in Server.GamePool)
            {
                if (client != null)
                {
                    action(client);
                }
            }
        }
        public static void SendWorldMessage(Interfaces.IPacket message, Client.GameState[] to, uint exceptuid)
        {
            foreach (Client.GameState client in to)
            {
                if (client != null)
                {
                    if (client.Entity.UID != exceptuid)
                    {
                        client.Send(message);
                    }
                }
            }
        }
        public static void SendWorldMessage(Interfaces.IPacket message, Client.GameState[] to, ushort mapid)
        {
            foreach (Client.GameState client in to)
            {
                if (client != null)
                {
                    if (client.Map.ID == mapid)
                    {
                        client.Send(message);
                    }
                }
            }
        }
        public static void SendWorldMessage(Interfaces.IPacket message, Client.GameState[] to, ushort mapid, uint exceptuid)
        {
            foreach (Client.GameState client in to)
            {
                if (client != null)
                {
                    if (client.Map.ID == mapid)
                    {
                        if (client.Entity.UID != exceptuid)
                        {
                            client.Send(message);
                        }
                    }
                }
            }
        }
        public static void SendScreen(Interfaces.IMapObject obj, Interfaces.IPacket packet)
        {
            var Values = Server.GamePool;
            foreach (var pClient in Values)
            {
                if (pClient == null) continue;
                if (!pClient.Socket.Alive) continue;
                if (pClient.Entity.MapID != obj.MapID) continue;
                if (Kernel.GetDistance(pClient.Entity.X, pClient.Entity.Y, obj.X, obj.Y) > Constants.pScreenDistance) continue;
                pClient.Send(packet);
            }
        }
        public static Enums.ConquerAngle GetFacing(short angle)
        {
            sbyte c_angle = (sbyte)((angle / 46) - 1);
            return (c_angle == -1) ? Enums.ConquerAngle.South : (Enums.ConquerAngle)c_angle;
        }
        public static short GetAnglex(ushort X, ushort Y, ushort x2, ushort y2)
        {
            double r = Math.Atan2(y2 - Y, x2 - X);
            if (r < 0)
                r += Math.PI * 2;
            return (short)Math.Round(r * 180 / Math.PI);
        }
        public static bool ChanceSuccess(int percent)
        {
            if (percent == 0)
                return false;

            return (Random.Next(0, 100) < percent);
        }
        public static uint maxJumpTime(short distance)
        {
            uint x = 0;
            x = 400 * (uint)distance / 10;
            return x;
        }
        public static bool Rate(int value)
        {
            return value > Random.Next() % 100;
        }
        public static bool Rate(double percent)
        {
            if (percent == 0) return false;
            while ((int)percent > 0) percent /= 10f;
            int discriminant = 1;
            percent = Math.Round(percent, 4);
            while (percent != Math.Ceiling(percent))
            {
                percent *= 10;
                discriminant *= 10;
                percent = Math.Round(percent, 4);
            }
            return Kernel.Rate((int)percent, discriminant);
        }
        public static bool Rate(int value, int discriminant)
        {
            return value > Random.Next() % discriminant;
        }
        public static bool Rate(ulong value)
        {
            return Rate((int)value);
        }
        public static bool Rate2(int value)
        {
            return value > RandGet(100);
        }
        public static int RandFromGivingNums(params int[] nums)
        {
            return nums[Random.Next(0, nums.Length)];
        }
        internal static void SendSpawn(Game.StaticEntity item)
        {
            foreach (Client.GameState client in Server.GamePool)
                if (client != null)
                    if (client.Map.ID == item.MapID)
                        if (GetDistance(item.X, item.Y, client.Entity.X, client.Entity.Y) <= Constants.pScreenDistance)
                            item.SendSpawn(client);
        }
        public static void SendWorldMessage(byte[] p)
        {
            foreach (Client.GameState client in Server.GamePool)
            {
                if (client != null)
                {
                    client.Send(p);
                }
            }
        }
    }
}
