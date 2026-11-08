using System;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.ConquerStructures;
using Revora.Game.ConquerStructures.Society;
using Revora.Network;
using Revora.Network.GamePackets;

internal static class GuildInvitationTests
{
    static int checks;
    static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        checks++;
        System.Console.WriteLine("PASS: " + label);
    }
    static GameState Player(uint uid)
    {
        var p = new GameState(null);
        p.Socket.Alive = false; // Offline packet adapter; no listener or database.
        p.Fake = false;
        p.Account = new AccountTable(null) { State = AccountTable.AccountState.Player };
        p.Entity = new Entity(EntityFlag.Player, false) { UID = uid, Owner = p, Name = "Invite" + uid };
        p.Inventory = new Inventory(p);
        p.Equipment = new Equipment(p);
        p.Weapons = new Tuple<ConquerItem, ConquerItem>(null, null);
        p.Friends = new System.Collections.Generic.SafeDictionary<uint, Friend>();
        p.Enemy = new System.Collections.Generic.SafeDictionary<uint, Enemy>();
        p.Partners = new System.Collections.Generic.SafeDictionary<uint, Revora.Game.ConquerStructures.Society.TradePartner>();
        p.Apprentices = new System.Collections.Generic.SafeDictionary<uint, Apprentice>();
        p.Action = 2;
        p.Entity.FullyLoaded = true;
        Kernel.GamePool[uid] = p;
        return p;
    }
    static byte[] Invite(GameState p, GameState target)
    {
        byte[] packet = new byte[36];
        Writer.WriteUInt16(28, 0, packet);
        Writer.WriteUInt16(1107, 2, packet);
        Writer.WriteUInt32(GuildCommand.InviteRequest, 4, packet);
        Writer.WriteUInt32(target.Entity.UID, 8, packet);
        Writer.WriteUInt64(PacketHandler.ClientSeal, 28, packet);
        GuildCommand.Process(packet, p);
        return packet;
    }
    static void Refused(GameState p, GameState target, string label)
    {
        byte[] packet = Invite(p, target);
        Check(p.GuildJoinTarget == 0 && target.GuildJoinTarget == 0
            && System.BitConverter.ToUInt32(packet, 4) == GuildCommand.InviteRequest, label);
    }
    static int Main()
    {
        try
        {
            var p = Player(991001);
            var target = Player(991002);
            Refused(p, target, "guildless ordinary player invitation does not throw or create consent");
            var g = new Guild(p.Entity.Name) { ID = 991, LeaderName = p.Entity.Name };
            var m = new Guild.Member(g.ID) { ID = p.Entity.UID, Name = p.Entity.Name, Rank = Enums.GuildMemberRank.GuildLeader };
            p.AsMember = m;
            Refused(p, target, "orphaned leader member without a guild cannot invite");
            p.Guild = g;
            p.Entity.FullyLoaded = false;
            p.Entity.GuildID = (ushort)g.ID;
            p.Entity.GuildRank = (ushort)m.Rank;
            p.Entity.FullyLoaded = true;
            Kernel.Guilds[g.ID] = g;
            g.Members[p.Entity.UID] = m;
            g.BlackList.Add(target.Entity.UID);
            string leaderName = g.LeaderName;
            g.LeaderName = "AnotherLeader";
            Refused(p, target, "old cached leader rank cannot bypass current guild leadership");
            g.LeaderName = leaderName;
            var replacement = new Guild(p.Entity.Name) { ID = g.ID };
            Kernel.Guilds[g.ID] = replacement;
            Refused(p, target, "replaced guild object cannot issue an invitation");
            Kernel.Guilds[g.ID] = g;
            p.AsMember = new Guild.Member(g.ID) { ID = m.ID, Name = m.Name, Rank = m.Rank };
            Refused(p, target, "detached member object cannot issue an invitation");
            p.AsMember = m;
            p.Entity.GuildRank = (ushort)Enums.GuildMemberRank.Member;
            Refused(p, target, "mismatched character and membership ranks cannot invite");
            p.Entity.GuildRank = (ushort)m.Rank;
            byte[] accepted = Invite(p, target);
            Check(System.BitConverter.ToUInt32(accepted, 4) == GuildCommand.SmsBlackList
                && p.GuildJoinTarget == 0 && target.GuildJoinTarget == 0,
                "canonical ordinary leader reaches the existing blacklist rule");
            Check(g.Members.Count == 1 && target.Entity.GuildID == 0 && target.Guild == null,
                "refused invitations preserve both characters and guild membership");
            g.BlackList.Remove(target.Entity.UID);
            byte[] offered = Invite(p, target);
            Check(p.GuildJoinTarget == target.Entity.UID && target.GuildJoinTarget == 0
                && System.BitConverter.ToUInt32(offered, 8) == p.Entity.UID
                && target.Entity.GuildID == 0 && g.Members.Count == 1,
                "canonical ordinary leader can offer an invitation without joining the unconsenting target");
            System.Console.WriteLine("Guild invitation checks passed: " + checks);
            return 0;
        }
        catch (Exception e) { System.Console.WriteLine(e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace); return 1; }
    }
}
