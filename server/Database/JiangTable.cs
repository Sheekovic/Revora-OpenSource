using System;
using System.IO;
using System.Linq;
using Revora.Game;
using System.Collections.Generic;

namespace Revora.Database
{
    public unsafe static class JiangHu
    {
        public static Dictionary<ushort, Atribut> Attributes = new Dictionary<ushort, Atribut>();
        public static Dictionary<byte, List<byte>> CultivateStatus = new Dictionary<byte, List<byte>>();
        private static byte[] FreeCourse = new byte[] { 10, 20, 26, 31, 52 };
        private static ushort[] FreeCourseInCastle = new ushort[] { 294, 625, 833, 1000, 1562 };
        public static ushort[] Aligmentpower = new ushort[] { 0, 0, 10, 13, 15, 18, 21, 25, 30, 50 };
        private static ushort[] Points = new ushort[] { 100, 120, 150, 200, 300, 500 };
        private static double[] MinutesInCastle = new double[] { 31.25, 31.25, 32.0, 32.2, 30.0 };
        private static ushort[] MinutesOnTalent = new ushort[] { 1000, 500, 384, 322, 192 };
        public static bool fullload = false;
        public static ushort GetFreeCourse(byte Talent)
        {
            if (Talent == 0)
            {
                Talent = 1;
            }
            return FreeCourse[Math.Min(4, Talent - 1)];
        }
        public static ushort GetFreeCourseInCastle(byte Talent)
        {
            if (Talent == 0)
            {
                Talent = 1;
            }
            return FreeCourseInCastle[Math.Min(4, Talent - 1)];
        }
        public static double GetMinutesInCastle(byte Talent)
        {
            if (Talent == 0)
            {
                Talent = 1;
            }
            return MinutesInCastle[Math.Min(4, Talent - 1)];
        }
        public static ushort GetMinutesOnTalent(byte Talent)
        {
            if (Talent == 0)
            {
                Talent = 1;
            }
            return MinutesOnTalent[Math.Min(4, Talent - 1)];
        }
        public static ushort GetPower(ushort UID)
        {
            return Attributes[UID].Power;
        }
        public static ushort GetStatusPoints(byte Level)
        {
            if (Level == 0)
            {
                Level = 1;
            }
            return Points[Math.Min(5, Level - 1)];
        }
        public static void LoadJiangHu()
        {
            Console.WriteLine("Loading JiangHu information!");
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT))
            {
                cmd.Select("JiangHu");
                using (MySqlReader rdr = new MySqlReader(cmd))
                {
                    //System.Console.Write("\b{0}", Cargando.NextChar());
                    while (rdr.Read())
                    {
                        Game.JiangHu Array = new Game.JiangHu(0);
                        Array.Load(rdr.ReadLongString("data"));
                        Game.JiangHu.JiangHuClients.TryAdd(Array.UID, Array);
                        Game.JiangHu.JiangHuRanking.UpdateRank(Array);
                    }
                }
            }
            fullload = true;
            //Console.WriteLine("Done!");
        }
        public static void LoadStatus()
        {
            try
            {
                Console.WriteLine("Loading JiangHuStatus information!");
                Database.Read read;
                uint count;
                uint num2;
                string[] strArray;
                using (read = new Database.Read(@"database/Slayer/JianghuAttributes.txt"))
                {
                    //System.Console.Write("\b{0}", Cargando.NextChar());
                    if (read.Reader(true))
                    {
                        count = (uint)read.Count;
                        for (num2 = 0; num2 < count; num2++)
                        {
                            strArray = read.ReadString("").Split(new char[] { ' ' });
                            Atribut atribut = new Atribut
                            {
                                Type = byte.Parse(strArray[1]),
                                Level = byte.Parse(strArray[2]),
                                Power = ushort.Parse(strArray[3])
                            };
                            if (((atribut.Type == 10) || (atribut.Type == 11)) || (atribut.Type == 12))
                            {
                                atribut.Power = (ushort)(atribut.Power * 10);
                            }
                            ushort key = ValueToRoll(atribut.Type, atribut.Level);
                            Attributes.Add(key, atribut);
                        }
                    }
                }
                using (read = new Database.Read(@"database/Slayer/JingHuCultivateStatus.txt"))
                {
                    if (read.Reader(true))
                    {
                        count = (uint)read.Count;
                        for (num2 = 0; num2 < count; num2++)
                        {
                            strArray = read.ReadString("").Split(new char[] { ' ' });
                            byte num4 = byte.Parse(strArray[0]);
                            byte num5 = byte.Parse(strArray[1]);
                            List<byte> list = new List<byte>();
                            for (byte i = 0; i < num5; i = (byte)(i + 1))
                            {
                                list.Add(byte.Parse(strArray[2 + i]));
                            }
                            CultivateStatus.Add(num4, list);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.ToString());
            }
            //Console.WriteLine("Done!");
        }
        internal static void LoadClient(Client.GameState client)
        {
            Interfaces.IJiangHu registered;
            if (!Game.JiangHu.JiangHuClients.TryGetValue(client.Entity.UID, out registered)) return;
            var jiang = registered as Game.JiangHu;
            if (jiang == null) return;
            if (jiang.SaveBlocked) {
                string data;
                using (var connection = DataHolder.MySqlConnection) {
                    connection.Open();
                    using (var command = Game.Features.QuestInventory.Command(connection, null,
                        "SELECT data FROM jianghu WHERE uid=@uid", "uid", (long)client.Entity.UID)) {
                        object value = command.ExecuteScalar();
                        data = value == null || value == DBNull.Value ? null : (string)value;
                    }
                }
                if (string.IsNullOrEmpty(data)) {
                    Game.JiangHu.JiangHuClients.TryRemove(client.Entity.UID, out registered);
                    client.Entity.MyJiang = null; return;
                }
                var restored = new Game.JiangHu(0); restored.Load(data);
                if (!restored.HasSavedData || restored.UID != client.Entity.UID) throw new InvalidDataException("Jiang recovery data invalid.");
                Game.JiangHu.JiangHuClients[client.Entity.UID] = restored; jiang = restored;
            }
            client.Entity.MyJiang = jiang; jiang.TimerStamp = DateTime.Now;
        }
        internal static void CaptureBaseline(Game.JiangHu jiang)
        {
            using (var connection = DataHolder.MySqlConnection) {
                connection.Open();
                using (var command = Game.Features.QuestInventory.Command(connection, null,
                    "SELECT data FROM jianghu WHERE uid=@uid", "uid", (long)jiang.UID))
                using (var reader = command.ExecuteReader()) {
                    if (!reader.Read()) throw new InvalidOperationException("Jiang baseline row missing.");
                    jiang.SavedData = reader.IsDBNull(0) ? null : reader.GetString(0);
                    jiang.HasSavedData = true;
                }
            }
        }
        private static void SaveRow(Game.JiangHu jiang)
        {
            if (!jiang.HasSavedData || jiang.SaveBlocked) throw new InvalidOperationException("Jiang state requires reload.");
            string data = jiang.ToString();
            using (var connection = DataHolder.MySqlConnection) {
                connection.Open();
                using (var command = Game.Features.QuestInventory.Command(connection, null,
                    "UPDATE jianghu SET data=@data WHERE uid=@uid AND data IS NOT DISTINCT FROM CAST(@before AS text)",
                    "data", data, "uid", (long)jiang.UID, "before", (object)jiang.SavedData ?? DBNull.Value)) {
                    command.CommandTimeout = 5;
                    if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("Jiang saved state changed.");
                }
            }
            jiang.SavedData = data;
        }
        public static void SaveJiangHu()
        {
            if (!fullload) return;
            foreach (var registered in Game.JiangHu.JiangHuClients.Values.ToArray()) {
                Client.GameState online;
                if (Kernel.GamePool.TryGetValue(registered.UID, out online)) { SaveJiangHu(online); continue; }
                var jiang = registered as Game.JiangHu;
                if (jiang == null || jiang.SaveBlocked || !Game.Features.SessionInstall.TryEnter(jiang.UID)) continue;
                try {
                    Interfaces.IJiangHu current;
                    if (Kernel.GamePool.ContainsKey(jiang.UID) || !Game.JiangHu.JiangHuClients.TryGetValue(jiang.UID, out current)
                        || !ReferenceEquals(current, jiang)) continue;
                    lock (jiang.sync) SaveRow(jiang);
                } catch (Exception ex) {
                    jiang.SaveBlocked = true;
                    Console.WriteLine("[jiang-save] offline uid=" + jiang.UID + " error=" + ex.GetType().Name);
                } finally { Game.Features.SessionInstall.Exit(jiang.UID); }
            }
        }
        public static void SaveJiangHu(Client.GameState client)
        {
            if (client == null || client.Entity == null || client.ItemSyncRoot == null || client.Fake
                || client.Entity.PersistenceRequiresReload || !client.Entity.FullyLoaded) return;
            uint uid = client.Entity.UID;
            if (!Game.Features.SessionInstall.TryEnter(uid)) return;
            try {
                lock (client.ItemSyncRoot) {
                    if (client.Fake || client.Entity.PersistenceRequiresReload || Game.Features.SessionInstall.IsSuperseded(client)) return;
                    var jiang = client.Entity.MyJiang;
                    Interfaces.IJiangHu registered;
                    if (jiang == null || !Game.JiangHu.JiangHuClients.TryGetValue(uid, out registered)
                        || !ReferenceEquals(jiang, registered) || jiang.UID != uid) return;
                    lock (jiang.sync) SaveRow(jiang);
                }
            } catch (Exception ex) {
                if (client.Entity.MyJiang != null) client.Entity.MyJiang.SaveBlocked = true;
                Game.Features.QuestInventory.Fail(client, "jiang-save", ex);
            }
            finally { Game.Features.SessionInstall.Exit(uid); }
        }
        public static ushort ValueToRoll(byte typ, byte level)
        {
            return (ushort)(typ + (level * 256));
        }
        public class Atribut
        {
            public byte Level;
            public ushort Power;
            public byte Type;
        }

    }
}
        
    
