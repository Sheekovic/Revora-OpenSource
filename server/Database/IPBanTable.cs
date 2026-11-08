// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace Revora.Database
{
    public unsafe class IPBan
    {
        public static void Load()
        {
            BannedIPs = new Dictionary<int, string>();
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("bannedips"))
            using (var reader = cmd.CreateReader())
            {
                //System.Console.Write("\b{0}", Cargando.NextChar());
                while (reader.Read())
                {
                    int asInt = reader.ReadInt32("ip_int");
                    BannedIPs.Add(asInt, reader.ReadString("ip"));
                }
            }
            //Console.WriteLine("Done!");
        }
        public static void Ban(string ip)
        {
            BannedIPs.Add(ip.GetHashCode(), ip);
            using (var cmd = new MySqlCommand(MySqlCommandType.INSERT))
                cmd.Insert("bannedips").Insert("ip_int", ip.GetHashCode())
                    .Insert("ip", ip).Execute();
        }
        public static void Unban(string ip)
        {
            BannedIPs.Remove(ip.GetHashCode());
            using (var cmd = new MySqlCommand(MySqlCommandType.DELETE))
                cmd.Delete("bannedips", "ip_int", ip.GetHashCode()).Execute();
        }
        public static bool IsBanned(string ip)
        {
            return BannedIPs.ContainsKey(ip.GetHashCode());
        }
        public static Dictionary<int, string> BannedIPs;
    }
}