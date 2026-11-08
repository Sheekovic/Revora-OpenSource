// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace Revora.Game.ConquerStructures
{
    public unsafe class Team
    {
        public ConcurrentDictionary<uint, Member> Members;
        private readonly object MembershipSync = new object();
        private sealed class CommandOwnership : IDisposable
        {
            internal object Gate;
            internal Client.GameState[] Actors;
            internal int Acquired;
            internal bool HasGate;
            public void Dispose()
            {
                if (HasGate) { System.Threading.Monitor.Exit(Gate); HasGate = false; }
                while (Acquired > 0) System.Threading.Monitor.Exit(Actors[--Acquired].ItemSyncRoot);
            }
        }
        internal IDisposable OwnCommand(Client.GameState sender, Client.GameState target, bool leaderRequired)
        {
            if (sender == null || target == null || sender.Entity == null || target.Entity == null
                || !leaderRequired && !ReferenceEquals(sender, target)) return null;
            var owned = new CommandOwnership { Gate = MembershipSync,
                Actors = (ReferenceEquals(sender, target) ? new[] { sender } : new[] { sender, target })
                    .OrderBy(p => p.Entity.UID).ToArray() };
            bool accepted = false;
            try
            {
                foreach (var actor in owned.Actors)
                {
                    if (!System.Threading.Monitor.TryEnter(actor.ItemSyncRoot)) return null;
                    owned.Acquired++;
                }
                if (!System.Threading.Monitor.TryEnter(MembershipSync)) return null;
                owned.HasGate = true;
                Member source, destination;
                if (!Features.QuestInventory.Player(sender, false) || !ReferenceEquals(sender.Team, this)
                    || !ReferenceEquals(target.Team, this) || leaderRequired && !ReferenceEquals(Lider, sender)
                    || !Members.TryGetValue(sender.Entity.UID, out source) || !ReferenceEquals(source.entry, sender)
                    || !Members.TryGetValue(target.Entity.UID, out destination) || !ReferenceEquals(destination.entry, target)) return null;
                accepted = true; return owned;
            }
            finally { if (!accepted) owned.Dispose(); }
        }
        public static Counter TeamCounter = new Counter(1);
        public Features.Tournaments.TeamElitePk.FighterStats EliteFighterStats;
        public Features.Tournaments.TeamElitePk.Match EliteMatch;
        public class Member : IDisposable
        {
            public Client.GameState entry;
            public bool Lider = false;
            internal Team OwnerTeam;

            public byte[] Create()
            {
                Network.GamePackets.AddToTeam temate = new Network.GamePackets.AddToTeam();
                temate.UID = entry.Entity.UID;
                temate.Hitpoints = (ushort)entry.Entity.Hitpoints;
                temate.MaxHitpoints = (ushort)entry.Entity.MaxHitpoints;
                temate.Mesh = entry.Entity.Mesh;
                temate.Name = entry.Entity.Name;

                return temate.ToArray();
            }
            public void Dispose()
            {
                GC.SuppressFinalize(this);
                lock (entry.ItemSyncRoot)
                    if (ReferenceEquals(entry.Team, OwnerTeam)) entry.Team = null;
            }
        }
        public bool OnPopulates
        {
            get { return Temates.Length > 0; }
        }
        public void SetEliteFighterStats(bool Create)
        {
            if (Create)
                EliteFighterStats = new Game.Features.Tournaments.TeamElitePk.FighterStats(Lider.Entity.UID, Lider.Entity.Name + "`s team", Lider.Entity.Mesh, this);
            else
            {
                if (EliteFighterStats != null)
                {
                    EliteFighterStats.LeaderUID = Lider.Entity.UID;
                    EliteFighterStats.LeaderMesh = Lider.Entity.Mesh;
                    EliteFighterStats.Name = Lider.Entity.Name + "`s team";
                }
            }
        }
        public byte LowestLevel;
        public uint LowestLevelsUID;
        public Map SpookMap;
        public bool ForbidJoin = false;
        public bool PickupMoney = true;
        public bool PickupItems = false;
        public bool AutoInvite = false;
        public bool TeamLeader = false;
        public uint UID;
        public bool InSpookMap
        {
            get
            {
                bool result = false;
                foreach (Client.GameState client in this.Players)
                {
                    if (this.SpookMap != null && client.Entity.MapID == this.SpookMap.ID)
                    {
                        result = true;
                    }
                }
                return result;
            }
        }
        public Client.GameState Lider;
        public bool TeamLider(Client.GameState client)
        {
            return client.Entity.UID == Lider.Entity.UID;
        }
        public Member[] Temates 
        {
            get
            {
                return Members.Values.ToArray();
            }
        }
        public Client.GameState[] Teammates
        {
            get
            {
                return Players.ToArray();
            }
        }
        public void GetClanShareBp(Client.GameState target) { Features.BattlePowerSharing.RefreshTeam(this, target); }
        public uint ProcentClanSharedBp(uint level) { return level <= 4 ? 30 + 10 * level : 0; }
        public bool RightTeamElite()
        {
            if (Lider == null)
                return false;
            foreach (var player in Players)
            {
                if (GetEliteGroup(player.Entity.Level) != GetEliteGroup(Lider.Entity.Level))
                    return false;
            }
            return true;
        }
        public byte GetEliteGroup(byte level)
        {
            return Features.Tournaments.ElitePkStatus.LevelGroup(level);
        }
        public IEnumerable<Client.GameState> Players
        {
            get
            {
                foreach (var mem in Members.Values)
                    yield return mem.entry;
            }
        }
        public bool Alive
        {
            get
            {
                bool alive = false;
                foreach (var player in Players)
                    if (player.Entity.Hitpoints > 0)
                        alive = true;

                return alive;
            }
        }
        public bool Contain(uint UID)
        {
            foreach (Member memb in Temates)
                if (UID == memb.entry.Entity.UID)
                    return true;

            return false;
        }
        public bool Active;
        public Team(Client.GameState owner)
        {
            lock (owner.ItemSyncRoot)
            {
            if (owner.Team != null) throw new InvalidOperationException("Player already belongs to a team.");
            UID = TeamCounter.Next;
            Lider = owner;
            Members = new ConcurrentDictionary<uint, Member>();
            Member member = new Member();
            member.entry = owner;
            member.OwnerTeam = this;
            Members.TryAdd(owner.Entity.UID, member);
            owner.Team = this;
            LowestLevel = owner.Entity.Level; LowestLevelsUID = owner.Entity.UID;
            AddLider();
            Active = false;
            TeamLeader = true;
            // Quest instances are allocated only by their owning encounter coordinator.
            }
        }
        public bool TryGetMember(uint UID, out Client.GameState client)
        {
            Member memb = null;
            if (!Members.TryGetValue(UID, out memb))
            {
                client = null;
                return false;
            }
            client = memb.entry;
            return true;
        }
        public void Add(Client.GameState client)
        {
            if (client == null || client.Entity == null) return;
            bool added;
            lock (client.ItemSyncRoot) lock (MembershipSync)
            {
                if (client.Team != null && !ReferenceEquals(client.Team, this)) return;
                if (Members.ContainsKey(client.Entity.UID)) return;
                AddOwned(client);
                added = ReferenceEquals(client.Team, this);
            }
            if (added) GetClanShareBp(client);
        }
        private void AddOwned(Client.GameState client)
        {
            if (AllowADD())
            {
                if (LowestLevel == 0)
                {
                    LowestLevel = client.Entity.Level;
                    LowestLevelsUID = client.Entity.UID;
                }
                else
                {
                    if (client.Entity.Level < LowestLevel)
                    {
                        LowestLevel = client.Entity.Level;
                        LowestLevelsUID = client.Entity.UID;
                    }
                }
                Member member = new Member();
                member.entry = client;
                member.OwnerTeam = this;
                if (!Members.TryAdd(client.Entity.UID, member)) return;
                member.entry.Team = this;
                Network.GamePackets.Leadership lider = new Network.GamePackets.Leadership();
                lider.Type = Network.GamePackets.Leadership.Teammate;
                client.Send(lider.ToArray());

                lider.Type = Network.GamePackets.Leadership.Leader;
                lider.UID = client.Entity.UID;
                lider.LeaderUID = Lider.Entity.UID;
                lider.Count = (ushort)(Members.Count);
                client.Send(lider.ToArray());

                lider.UID = lider.LeaderUID = Lider.Entity.UID;
                Lider.Send(lider.ToArray());
                client.Send(lider.ToArray());

                Network.GamePackets.AddToTeam addme = new Network.GamePackets.AddToTeam();
                addme.UID = client.Entity.UID;
                addme.Hitpoints = (ushort)client.Entity.Hitpoints;
                addme.Mesh = client.Entity.Mesh;
                addme.Name = client.Entity.Name;
                addme.MaxHitpoints = (ushort)client.Entity.MaxHitpoints;
                foreach (var noob in Temates)
                {
                    noob.entry.Send(addme.ToArray());
                    client.Send(noob.Create());
                }
            }
        }
        public void Remove(Client.GameState client, bool mode = true)
        {
            if (client == null || client.Entity == null) return;
            lock (client.ItemSyncRoot) lock (MembershipSync)
            {
                Member member;
                if (!ReferenceEquals(client.Team, this) || !Members.TryGetValue(client.Entity.UID, out member)
                    || !ReferenceEquals(member.entry, client)) return;
                RemoveOwned(client, mode);
            }
            GetClanShareBp(client);
        }
        private void RemoveOwned(Client.GameState client, bool mode)
        {
            Member memb = null;
            if (Members.TryGetValue(client.Entity.UID, out memb))
            {
                foreach (var noob in Temates)
                {
                    Network.GamePackets.Team tem = new Network.GamePackets.Team();
                    tem.UID = memb.entry.Entity.UID;
                    if (mode)
                        tem.Type = Network.GamePackets.Team.ExitTeam;
                    else
                        tem.Type = Network.GamePackets.Team.Kick;
                    noob.entry.Send(tem.ToArray());
                    noob.entry.Entity.SpookMap = null;

                    //if (memb.entry.Player.Aura_isActive)
                    //{
                    //    if (noob.entry.Player.ContainsFlag2((ulong)Network.GamePackets.FlagsUpdate.Flags.EarthAura))
                    //        noob.entry.Player.RemoveFlag2((ulong)Network.GamePackets.FlagsUpdate.Flags.EarthAura);
                    //    if (noob.entry.Player.ContainsFlag2((ulong)Network.GamePackets.FlagsUpdate.Flags.FireAura))
                    //        noob.entry.Player.RemoveFlag2((ulong)Network.GamePackets.FlagsUpdate.Flags.FireAura);
                    //    if (noob.entry.Player.ContainsFlag2((ulong)Network.GamePackets.FlagsUpdate.Flags.WaterAura))
                    //        noob.entry.Player.RemoveFlag2((ulong)Network.GamePackets.FlagsUpdate.Flags.WaterAura);
                    //    if (noob.entry.Player.ContainsFlag2((ulong)Network.GamePackets.FlagsUpdate.Flags.WoodAura))
                    //        noob.entry.Player.RemoveFlag2((ulong)Network.GamePackets.FlagsUpdate.Flags.WoodAura);
                    //    if (noob.entry.Player.ContainsFlag2((ulong)Network.GamePackets.FlagsUpdate.Flags.MetalAura))
                    //        noob.entry.Player.RemoveFlag2((ulong)Network.GamePackets.FlagsUpdate.Flags.MetalAura);
                    //    if (noob.entry.Player.ContainsFlag2((ulong)Network.GamePackets.FlagsUpdate.Flags.FendAura))
                    //        noob.entry.Player.RemoveFlag2((ulong)Network.GamePackets.FlagsUpdate.Flags.FendAura);
                    //    if (noob.entry.Player.ContainsFlag2((ulong)Network.GamePackets.FlagsUpdate.Flags.TyrantAura))
                    //        noob.entry.Player.RemoveFlag2((ulong)Network.GamePackets.FlagsUpdate.Flags.TyrantAura);

                    //    if (noob.entry.Player.ContainsFlag2((ulong)Network.GamePackets.FlagsUpdate.Flags.EarthAuraIcon))
                    //        noob.entry.Player.RemoveFlag2((ulong)Network.GamePackets.FlagsUpdate.Flags.EarthAuraIcon);
                    //    if (noob.entry.Player.ContainsFlag2((ulong)Network.GamePackets.FlagsUpdate.Flags.FireAuraIcon))
                    //        noob.entry.Player.RemoveFlag2((ulong)Network.GamePackets.FlagsUpdate.Flags.FireAuraIcon);
                    //    if (noob.entry.Player.ContainsFlag2((ulong)Network.GamePackets.FlagsUpdate.Flags.WaterAuraIcon))
                    //        noob.entry.Player.RemoveFlag2((ulong)Network.GamePackets.FlagsUpdate.Flags.WaterAuraIcon);
                    //    if (noob.entry.Player.ContainsFlag2((ulong)Network.GamePackets.FlagsUpdate.Flags.WoodAuraIcon))
                    //        noob.entry.Player.RemoveFlag2((ulong)Network.GamePackets.FlagsUpdate.Flags.WoodAuraIcon);
                    //    if (noob.entry.Player.ContainsFlag2((ulong)Network.GamePackets.FlagsUpdate.Flags.MetalAuraIcon))
                    //        noob.entry.Player.RemoveFlag2((ulong)Network.GamePackets.FlagsUpdate.Flags.MetalAuraIcon);
                    //    if (noob.entry.Player.ContainsFlag2((ulong)Network.GamePackets.FlagsUpdate.Flags.FendAuraIcon))
                    //        noob.entry.Player.RemoveFlag2((ulong)Network.GamePackets.FlagsUpdate.Flags.FendAuraIcon);
                    //    if (noob.entry.Player.ContainsFlag2((ulong)Network.GamePackets.FlagsUpdate.Flags.TyrantAuraIcon))
                    //        noob.entry.Player.RemoveFlag2((ulong)Network.GamePackets.FlagsUpdate.Flags.TyrantAuraIcon);

                    //    noob.entry.Player.Aura_isActive = false;
                    //    noob.entry.Player.Aura_actType = 0;
                    //    noob.entry.Player.Aura_actPower = 0;

                    //    noob.entry.TeamAura = false;
                    //    noob.entry.TeamAuraStatusFlag = 0;
                    //    noob.entry.TeamAuraPower = 0;

                    //    noob.entry.LoadItemStats();
                    //}
                    if (client.TeamAura)
                    {
                        if (client.Entity.ContainsFlag((ulong)Network.GamePackets.FlagsUpdate.Flags.EarthAura))
                            client.Entity.RemoveFlag((ulong)Network.GamePackets.FlagsUpdate.Flags.EarthAura);
                        if (client.Entity.ContainsFlag((ulong)Network.GamePackets.FlagsUpdate.Flags.FireAura))
                            client.Entity.RemoveFlag((ulong)Network.GamePackets.FlagsUpdate.Flags.FireAura);
                        if (client.Entity.ContainsFlag((ulong)Network.GamePackets.FlagsUpdate.Flags.WaterAura))
                            client.Entity.RemoveFlag((ulong)Network.GamePackets.FlagsUpdate.Flags.WaterAura);
                        if (client.Entity.ContainsFlag((ulong)Network.GamePackets.FlagsUpdate.Flags.WoodAura))
                            client.Entity.RemoveFlag((ulong)Network.GamePackets.FlagsUpdate.Flags.WoodAura);
                        if (client.Entity.ContainsFlag((ulong)Network.GamePackets.FlagsUpdate.Flags.MetalAura))
                            client.Entity.RemoveFlag((ulong)Network.GamePackets.FlagsUpdate.Flags.MetalAura);
                        if (client.Entity.ContainsFlag((ulong)Network.GamePackets.FlagsUpdate.Flags.FendAura))
                            client.Entity.RemoveFlag((ulong)Network.GamePackets.FlagsUpdate.Flags.FendAura);
                        if (client.Entity.ContainsFlag((ulong)Network.GamePackets.FlagsUpdate.Flags.TyrantAura))
                            client.Entity.RemoveFlag((ulong)Network.GamePackets.FlagsUpdate.Flags.TyrantAura);

                        client.TeamAura = false;
                        client.TeamAuraStatusFlag = 0;
                        client.TeamAuraPower = 0;
                        client.LoadItemStats();
                    }
                }
                if (!Members.TryRemove(client.Entity.UID, out memb)) return;
                LowestLevel = 0; LowestLevelsUID = 0; SearchForLowest();
                Network.GamePackets.Leadership lider = new Network.GamePackets.Leadership();
                lider.UID = memb.entry.Entity.UID;
                lider.Count = (ushort)Members.Count;
                lider.Type = Network.GamePackets.Leadership.Leader;
                memb.entry.Send(lider.ToArray());
                if (memb.Lider)
                {
                    memb.entry.Entity.RemoveFlag((ulong)Network.GamePackets.FlagsUpdate.Flags.TeamLeader);
                    AddLider();
                }
                memb.Dispose();
            }
        }
        public void AddLider()
        {
            lock (MembershipSync) AddLeaderOwned();
        }
        private void AddLeaderOwned()
        {
            if (Temates.Length >= 1)
            {
                Member mem;
                if (Lider == null || !Members.TryGetValue(Lider.Entity.UID, out mem) || !ReferenceEquals(mem.entry, Lider))
                    mem = Members.Values.OrderBy(p => p.entry.Entity.UID).First();
                if (mem.entry != null)
                {
                    Lider = mem.entry;
                    foreach (var entry in Members.Values)
                    {
                        entry.Lider = ReferenceEquals(entry, mem);
                        if (!entry.Lider) entry.entry.Entity.RemoveFlag((ulong)Network.GamePackets.FlagsUpdate.Flags.TeamLeader);
                    }
                    TeamLeader = true;
                    mem.entry.Entity.AddFlag((ulong)Network.GamePackets.FlagsUpdate.Flags.TeamLeader, StatusFlagsBigVector32.PermanentFlag, false);
                    Network.GamePackets.Leadership lider = new Network.GamePackets.Leadership();
                    lider.UID = lider.LeaderUID = mem.entry.Entity.UID;
                    lider.Count = (ushort)Members.Count;
                    lider.Type = Network.GamePackets.Leadership.Leader;
                    foreach (var amem in Temates)
                    {
                        if (amem.entry != null)
                        {
                            amem.entry.Send(amem.Create());
                            amem.entry.Send(lider.ToArray());
                        }
                    }
                    SetEliteFighterStats(false);
                }
            }
            else { TeamLeader = false; Lider = null; }
        }
        public bool SpouseWarFull
        {
            get
            {
                if (Teammates != null)
                {
                    if (Teammates.Length != 2) return false;
                    if (Teammates[0].Entity.Spouse == Teammates[1].Entity.Name)
                        return true;
                }
                return false;
            }
        }
        public void SearchForLowest()
        {
            foreach (Client.GameState client in Players)
            {
                if (LowestLevel == 0)
                {
                    LowestLevel = client.Entity.Level;
                    LowestLevelsUID = client.Entity.UID;
                }
                else
                {
                    if (client.Entity.Level < LowestLevel)
                    {
                        LowestLevel = client.Entity.Level;
                        LowestLevelsUID = client.Entity.UID;
                    }
                }
            }
        }
        public bool CanGetNoobExperience(Client.GameState Teammate)
        {
            return Teammate.Entity.Level > LowestLevel && LowestLevel < 70;
        }
        public bool IsTeammate(uint UID)
        {
            return Members.ContainsKey(UID);
        }
        public void SendTeam(byte[] packet, bool me)
        {
            foreach (Member memb in Temates)
            {
                if (me && TeamLider(memb.entry))
                    continue;
                memb.entry.Send(packet);
            }
        }
        public void SendMesageTeam(byte[] packet, uint mee)
        {
            foreach (Member memb in Temates)
            {
                if (memb.entry.Entity.UID == mee)
                    continue;
                memb.entry.Send(packet);
            }
        }
        public void SendMessage(byte[] data)
        {
            foreach (var teammate in Temates)
                teammate.entry.Send(data);
        }
        public bool AllowADD()
        {
            if (Game.Features.Tournaments.ElitePkStatus.InSkillTeam(GetEliteGroup(Lider.Entity.Level), this))
                return false;
            return Members.Count < 5;
        }
        public void SendMessage(Interfaces.IPacket message)
        {
            foreach (var teammate in Teammates)
                teammate.Send(message);
        }
    }
}
