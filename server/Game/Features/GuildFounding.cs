using System;
using System.Linq;
using System.Text;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Game.ConquerStructures.Society;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class GuildFounding
    {
        private static readonly object gate = new object();
        private static bool Ready(GameState p, uint payment)
        {
            return Q.Near(p, 10003, 1002) && p.Map.BaseID == 1002
                && p.Guild == null && p.AsMember == null && p.Entity.GuildID == 0
                && p.UnionID == 0 && p.Union == null
                && p.Entity.Level >= Config.GuildCreateMinLevel && p.Entity.PokerTable == 0
                && CharacterProgression.Managed(p) && PurseBalance.Managed(p)
                && (payment == 1 || payment == 2) && (uint)p["guild-create-pay"] == payment;
        }
        internal static bool Create(GameState p, string input)
        {
            string name = (input ?? "").Trim();
            if (p == null || p.Entity == null || p.ItemSyncRoot == null || p.Variables == null
                || name.Length < 4 || name.Length > 15 || name.Any(char.IsControl)
                || Encoding.Default.GetByteCount(name) > 15
                || Encoding.Default.GetString(Encoding.Default.GetBytes(name)) != name
                || !SessionInstall.TryEnter(p.Entity.UID)) return false;
            bool owner = false, held = false;
            try
            {
                if (!(owner = Monitor.TryEnter(p.ItemSyncRoot)) || !(held = Monitor.TryEnter(gate))) return false;
                uint payment = (uint)p["guild-create-pay"];
                if (!Ready(p, payment)) return false;
                uint price = checked((uint)(payment == 1 ? Config.GuildCreateRevoraPoints : Config.GuildCreateConquerPoints));
                uint balance = payment == 1 ? p.Entity.RevoraPoint : p.Entity.ConquerPoints;
                if (balance < price) return false;
                uint next = balance - price, id;
                var profile = CharacterProgression.Capture(p);
                uint day = Emulator.Core.Common.TimeGet(Emulator.Core.TimeType.Day);
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open();
                    using (var t = c.BeginTransaction())
                    {
                        Q.Execute(c, t, "SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s'");
                        SkillTable.LockOwner(c, t, p);
                        CharacterProgression.Persist(c, t, p, profile);
                        Q.Execute(c, t, "SELECT pg_advisory_xact_lock(6711,10003)");
                        long counter;
                        using (var q = Q.Command(c, t, "SELECT guildid FROM configuration WHERE server=@server FOR UPDATE", "server", Constants.ServerName))
                        {
                            object value = q.ExecuteScalar();
                            if (value == null || value == DBNull.Value) throw new InvalidOperationException("Guild counter is missing.");
                            counter = Convert.ToInt64(value);
                        }
                        using (var q = Q.Command(c, t, "SELECT EXISTS(SELECT 1 FROM guilds WHERE lower(name)=lower(@name))", "name", name))
                            if (Equals(q.ExecuteScalar(), true)) return false;
                        using (var q = Q.Command(c, t, "SELECT COALESCE(MAX(id),0) FROM guilds"))
                            counter = Math.Max(counter, Convert.ToInt64(q.ExecuteScalar()));
                        counter = Math.Max(counter, Kernel.Guilds.Keys.DefaultIfEmpty(0u).Max());
                        if (counter >= ushort.MaxValue) return false;
                        id = checked((uint)(counter + 1));
                        if (Q.Execute(c, t, @"INSERT INTO guilds(id,name,bulletin,silverfund,enrollmentdate,bulletindate,leadername,ad_rate)
VALUES(@id,@name,'This is a new guild!',500000,@day,@day,@leader,50000)",
                            "id", (long)id, "name", name, "day", (long)day, "leader", p.Entity.Name) != 1)
                            throw new InvalidOperationException("Guild was not saved.");
                        if (Q.Execute(c, t, "UPDATE entities SET guildid=@guild,guildrank=1000 WHERE uid=@uid AND COALESCE(guildid,0)=0",
                            "guild", (long)id, "uid", (long)p.Entity.UID) != 1)
                            throw new InvalidOperationException("Founder membership changed.");
                        string sql = payment == 1
                            ? "UPDATE entities SET revorapoint=@next WHERE uid=@uid AND COALESCE(revorapoint,0)=@old"
                            : "UPDATE entities SET conquerpoints=@next WHERE uid=@uid AND COALESCE(conquerpoints,0)=@old";
                        if (Q.Execute(c, t, sql, "next", (long)next, "uid", (long)p.Entity.UID,
                            "old", payment == 1 ? PurseBalance.PreimageRvp(p) : PurseBalance.PreimageCp(p)) != 1)
                            throw new InvalidOperationException("Founder payment changed.");
                        if (Q.Execute(c, t, "UPDATE configuration SET guildid=@id WHERE server=@server", "id", (long)id, "server", Constants.ServerName) != 1)
                            throw new InvalidOperationException("Guild counter was not saved.");
                        if (!Ready(p, payment) || p.Entity.Level != profile.Values[0]
                            || (payment == 1 ? p.Entity.RevoraPoint : p.Entity.ConquerPoints) != balance)
                            throw new InvalidOperationException("Founder context changed.");
                        WayOfHeroesClaims.RecordGuildMembership(c,t,p.Entity.UID,id,true);
                        t.Commit();
                    }
                }
                CharacterProgression.Committed(p, profile);
                if (payment == 1) p.Entity.ApplyCommittedRevoraPoints(next); else p.Entity.ApplyCommittedConquerPoints(next);
                // Read the committed member fields through the production loader, including prior donations.
                Guild guild = GuildTable.LoadRecord(id);
                if (guild == null || !guild.Members.ContainsKey(p.Entity.UID))
                    throw new InvalidOperationException("Founded guild requires reload.");
                Kernel.Guilds.Add(id, guild);
                Guild registered;
                if (!Kernel.Guilds.TryGetValue(id, out registered) || !ReferenceEquals(registered, guild))
                    throw new InvalidOperationException("Founded guild registry requires reload.");
                p.Guild = guild; p.AsMember = guild.Members[p.Entity.UID];
                p.Entity.GuildID = (ushort)id; p.Entity.GuildRank = (ushort)p.AsMember.Rank;
                p["guild-create-pay"] = 0u;
                guild.SendGuild(p); guild.SendName(p); p.SendScreenSpawn(p.Entity, true);
                return true;
            }
            catch (Exception ex) { Q.Fail(p, "guild-founder", ex); return false; }
            finally
            {
                if (held) Monitor.Exit(gate);
                if (owner) Monitor.Exit(p.ItemSyncRoot);
                SessionInstall.Exit(p.Entity.UID);
            }
        }
    }
}
