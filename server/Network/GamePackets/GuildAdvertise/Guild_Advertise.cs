// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Revora.Network.GamePackets
{
    public unsafe class Guild_Advertise
    {
        public static bool PassJoinRequirements(Client.GameState client, Game.ConquerStructures.Society.Guild guild)
        {
            GuildCommand cmd = new GuildCommand(true);
            cmd.Type = GuildCommand.GuildRequirements;
            cmd.Level = guild.LevelRequirement;
            cmd.Reborn = guild.RebornRequirement;
            cmd.Class = guild.ClassRequirement;
            if (((client.Entity.Class >= 10 && client.Entity.Class <= 15) && !guild.AllowTrojans) ||
                ((client.Entity.Class >= 20 && client.Entity.Class <= 25) && !guild.AllowWarriors) ||
                ((client.Entity.Class >= 40 && client.Entity.Class <= 45) && !guild.AllowArchers) ||
                ((client.Entity.Class >= 50 && client.Entity.Class <= 55) && !guild.AllowNinjas) ||
                ((client.Entity.Class >= 60 && client.Entity.Class <= 65) && !guild.AllowMonks) ||
                ((client.Entity.Class >= 70 && client.Entity.Class <= 75) && !guild.AllowPirates) ||
                ((client.Entity.Class >= 80 && client.Entity.Class <= 85) && !guild.AllowLeeLong) ||
                ((client.Entity.Class >= 100 && client.Entity.Class <= 190) && !guild.AllowTaoists))
            {
                client.Send(cmd);
                return false;
            }
            if (client.Entity.Reborn < guild.RebornRequirement)
            {
                client.Send(cmd);
                return false;
            }
            if (client.Entity.Level < guild.LevelRequirement)
            {
                client.Send(cmd);
                return false;
            }
            return true;
        }
        public static void Process(Client.GameState c, byte id)
        {
            var array = Kernel.Guilds_adv.Values.Where(p => p.ad_rate != 0).OrderByDescending(p => p.ad_rate).ToArray();
            build(c, id, array, p => p.ad_rate);
        }
        public static void build(Client.GameState c, byte id, Game.ConquerStructures.Society.Guild[] array = null, Func<Game.ConquerStructures.Society.Guild, UInt64> select = null)
        {
            byte[] data = null;
            int start = id;
        lable_a7a:
            int dowrite = 0;
            data = new byte[32 + 2 * 344];
            Writer.WriteInt32(data.Length - 8, 0, data);
            Writer.WriteUInt16(2226, 2, data);
            Writer.WriteUInt16(id, 4, data);
            Writer.WriteInt32(array.Length, 12, data);
            if (start == id)
            {
                Writer.WriteInt32(1, 16, data);
            }
            for (int i = start; i < array.Length; i++)
            {

                if (start == start + 4)
                {
                    break;
                }
                if (i == (start + 2))
                {
                    if (dowrite == 0)
                    {
                        Writer.WriteUInt32(1, 8, data);
                    }
                    if (dowrite == 1)
                    {
                        Writer.WriteUInt32(2, 8, data);
                    }
                    start += 2;
                    c.Send(data);
                    goto lable_a7a;

                }
                int offset = 24 + dowrite * 344;
                Writer.WriteUInt32(array[i].ID, offset, data); offset += 4;
                Writer.WriteString(array[i].adv, offset, data); offset += 255;
                foreach (var g in Kernel.Guilds.Values)
                {
                    if (g.ID == array[i].ID)
                    {
                        Writer.WriteString(g.Name, offset, data); offset += 36;
                        Writer.WriteString(g.LeaderName, offset, data); offset += 17;
                        Writer.WriteUInt32(g.Level, offset, data); offset += 4;
                        Writer.WriteUInt32(g.MemberCount, offset, data); offset += 4;
                        Writer.WriteUInt64(g.SilverFund, offset, data); offset += 8;
                    }
                }
                Writer.WriteUInt32(array[i].AutoRecruit, offset, data); offset += 2;
                Writer.WriteUInt64(array[i].adLevelRequirement, offset, data); offset += 2;
                Writer.WriteUInt64(array[i].adRebornRequirement, offset, data); offset += 2;
                Writer.WriteUInt64(array[i].adClassRequirement, offset, data); offset += 2;
                Writer.WriteUInt64(array[i].MaleFemale, offset, data);
                dowrite = 1;
            }
            if (dowrite == 0)
            {
                Writer.WriteUInt32(1, 8, data);
            }
            if (dowrite == 1)
            {
                Writer.WriteUInt32(2, 8, data);
            }
            c.Send(data);
        }
    }
}

