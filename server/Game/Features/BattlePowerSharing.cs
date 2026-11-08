using System;
using System.Linq;
using Revora.Client;
using Revora.Game.ConquerStructures;
using Revora.Network.GamePackets;
using Team = Revora.Game.ConquerStructures.Team;

namespace Revora.Game.Features
{
    internal static class BattlePowerSharing
    {
        internal static uint MentorAmount(Entity child, Entity mentor)
        {
            if (child == null || mentor == null || child.Level >= 135) return 0;
            int gap = mentor.SourceBattlePower - child.SourceBattlePower;
            if (gap <= 0) return 0;
            // Retain the inherited Revora ratio; the original exact share is not published.
            uint amount = (uint)(gap / 3.3F);
            if (child.Level >= 125) amount = amount * (uint)(135 - child.Level) / 10;
            return Math.Min(200u, amount);
        }
        internal static GameState MentorSource(GameState p)
        {
            GameState parent; Game.ConquerStructures.Society.Apprentice child;
            if (!QuestInventory.Player(p, false) || p.AmbiguousMentor || p.Mentor == null || p.AsApprentice == null
                || !Kernel.GamePool.TryGetValue(p.Mentor.ID, out parent) || !QuestInventory.Player(parent, false)
                || ReferenceEquals(parent, p) || parent.Apprentices == null || !parent.Apprentices.TryGetValue(p.Entity.UID, out child)
                || child == null || child.ID != p.Entity.UID || child.EnroleDate != p.Mentor.EnroleDate
                || p.AsApprentice.ID != p.Entity.UID || p.AsApprentice.EnroleDate != p.Mentor.EnroleDate) return null;
            return parent;
        }
        private static bool Member(GameState p, Team team, Clan clan)
        {
            Team.Member member; ClanMember clanMember;
            return QuestInventory.Player(p, false) && ReferenceEquals(p.Team, team) && p.Entity.ClanId == clan.ID
                && team.Members != null && team.Members.TryGetValue(p.Entity.UID, out member) && member != null && ReferenceEquals(member.entry, p)
                && clan.Members != null && clan.Members.TryGetValue(p.Entity.UID, out clanMember) && clanMember != null && clanMember.Identifier == p.Entity.UID;
        }
        internal static uint ClanAmount(GameState p, out uint source)
        {
            source = 0;
            if (!QuestInventory.Player(p, false) || p.Team == null || p.Entity.ClanId == 0) return 0;
            var team = p.Team; var clan = p.Entity.GetClan;
            if (clan == null || clan.BPTower > 4 || !Member(p, team, clan)) return 0;
            int own = p.Entity.SourceBattlePower, best = own;
            foreach (var entry in team.Members.Values.ToArray())
            {
                var peer = entry == null ? null : entry.entry;
                if (!Member(peer, team, clan) || peer.Entity.MapID != p.Entity.MapID) continue;
                int power = peer.Entity.SourceBattlePower;
                if (power > best || power == best && source != 0 && peer.Entity.UID < source) { best = power; source = peer.Entity.UID; }
            }
            return (uint)((best - own) * (30 + 10 * clan.BPTower) / 100);
        }
        internal static ushort Total(Entity e)
        {
            if (e == null || e.Owner == null) return 0;
            lock (MentorExperience.Sync)
            {
                if (ReferenceEquals(e.Owner.Entity, e) && Tournaments.LocalTeamPkCombat.SkillStats(e.Owner))
                    return e.SkillBattlePower;
                int power = e.SourceBattlePower; uint source;
                if (ReferenceEquals(e.Owner.Entity, e))
                {
                    var mentor = MentorSource(e.Owner);
                    uint mentorShare = mentor == null ? 0 : MentorAmount(e, mentor.Entity);
                    uint clanShare = ClanAmount(e.Owner, out source);
                    // Installed 6711 StatusTips.ini [44]: only the higher shared value applies.
                    power += (int)Math.Max(mentorShare, clanShare);
                }
                return (ushort)Math.Min(ushort.MaxValue, power);
            }
        }
        internal static void RefreshClan(GameState p)
        {
            if (p == null || p.Entity == null) return;
            lock (MentorExperience.Sync)
            {
                uint source; uint amount = ClanAmount(p, out source);
                if (p.Entity.ClanSharedBp != amount) p.Entity.ClanSharedBp = amount;
            }
        }
        internal static void RefreshMentor(GameState p)
        {
            if (p == null || p.Entity == null) return;
            lock (MentorExperience.Sync)
            {
                RefreshOne(p); RefreshTeam(p.Team, p);
                if (!QuestInventory.Player(p, false) || p.Apprentices == null) return;
                foreach (uint uid in p.Apprentices.Keys.ToArray())
                {
                    GameState child;
                    if (Kernel.GamePool.TryGetValue(uid, out child) && child.Mentor != null && child.Mentor.ID == p.Entity.UID) RefreshOne(child);
                }
            }
        }
        internal static void RefreshTeam(Team team, GameState changed)
        {
            lock (MentorExperience.Sync)
            {
                RefreshClan(changed);
                if (team == null || team.Members == null) return;
                foreach (var member in team.Members.Values.ToArray())
                    if (member != null && !ReferenceEquals(member.entry, changed)) RefreshClan(member.entry);
            }
        }
        private static void RefreshOne(GameState p)
        {
            var parent = MentorSource(p); uint amount = parent == null ? 0 : MentorAmount(p.Entity, parent.Entity);
            if (p.Entity.MentorBattlePower == amount) return;
            p.Entity.MentorBattlePower = amount;
            if (parent == null) return;
            var e = parent.Entity;
            p.Send(new MentorInformation(true) { Mentor_Type=1, Mentor_ID=e.UID, Apprentice_ID=p.Entity.UID,
                Enrole_Date=p.Mentor.EnroleDate, Mentor_Level=e.Level, Mentor_Class=e.Class, Mentor_PkPoints=e.PKPoints,
                Mentor_Mesh=e.Mesh, Mentor_Online=true, Shared_Battle_Power=amount, String_Count=3,
                Mentor_Name=e.Name, Apprentice_Name=p.Entity.Name, Mentor_Spouse_Name=e.Spouse });
        }
    }
}
