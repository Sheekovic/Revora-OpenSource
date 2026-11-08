// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Drawing;
using Revora.Game;

namespace Revora.Network.GamePackets
{
    public unsafe class Team : Writer, Interfaces.IPacket
    {
        public const ushort
        Create = 0,
        JoinRequest = 1,
        ExitTeam = 2,
        AcceptInvitation = 3,
        InviteRequest = 4,
        AcceptJoinRequest = 5,
        Dismiss = 6,
        Kick = 7,
        ForbidJoining = 8,
        UnforbidJoining = 9,
        LootMoneyOff = 10,
        LootMoneyOn = 11,
        LootItemsOff = 12,
        LootItemsOn = 13;

        byte[] Buffer;
        public Team()
        {
            // 24-byte payload followed by the eight-byte server seal.
            Buffer = new byte[32];
            WriteUInt16(24, 0, Buffer);
            WriteUInt16(1023, 2, Buffer);
        }
        public uint Type
        {
            get { return BitConverter.ToUInt32(Buffer, 4); }
            set { WriteUInt32(value, 4, Buffer); }
        }
        public uint UID
        {
            get { return BitConverter.ToUInt32(Buffer, 8); }
            set { WriteUInt32(value, 8, Buffer); }
        }
        public byte[] ToArray()
        {
            return Buffer;
        }
        public void Deserialize(byte[] buffer)
        {
            Buffer = buffer;
        }
        public void Send(Client.GameState client)
        {
            client.Send(Buffer);
        }
        private static void NotifyArenaRemoval(Client.GameState player, Game.ConquerStructures.Team previousTeam)
        {
            if (ReferenceEquals(player.Team, previousTeam) || !Game.Features.ArenaStartupPreparation.Prepared) return;
            var group = player.TeamQualifierGroup;
            if (group != null && group.CompletionOwner != null)
                Game.TeamQualifier.QualifyEngine.DoQuit(player);
        }
        private static bool CurrentLeader(Client.GameState player)
        {
            if (!Game.Features.QuestInventory.Player(player)) return false;
            var team = player.Team;
            Game.ConquerStructures.Team.Member member;
            return team != null && ReferenceEquals(team.Lider, player)
                && team.Members.TryGetValue(player.Entity.UID, out member)
                && ReferenceEquals(member.entry, player);
        }
        private sealed class EntryOffer
        {
            internal Client.GameState Leader;
            internal Game.ConquerStructures.Team Team;
            internal Time32 Created;
        }
        private sealed class EntryRequests
        {
            internal EntryOffer Invitation, Application;
        }
        // Two bounded, transient slots per original applicant session. Reconnects
        // do not inherit consent; no global UID-indexed backlog is retained.
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Client.GameState, EntryRequests> EntryPending
            = new System.Runtime.CompilerServices.ConditionalWeakTable<Client.GameState, EntryRequests>();
        internal static Func<Time32> EntryClock = () => Time32.Now;
        private static bool RecordEntry(Client.GameState leader, Client.GameState applicant, bool invitation)
        {
            if (!CurrentLeader(leader) || !Game.Features.QuestInventory.Player(applicant)
                || applicant.Team != null || leader.Team.ForbidJoin || !leader.Team.AllowADD()) return false;
            var pending = EntryPending.GetValue(applicant, p => new EntryRequests());
            lock (pending)
            {
                var offer = new EntryOffer { Leader = leader, Team = leader.Team, Created = EntryClock() };
                if (invitation) pending.Invitation = offer; else pending.Application = offer;
                return true;
            }
        }
        private static bool AcceptEntry(Client.GameState leader, Client.GameState applicant, bool invitation)
        {
            EntryRequests pending;
            if (!EntryPending.TryGetValue(applicant, out pending)) return false;
            lock (pending)
            {
                var offer = invitation ? pending.Invitation : pending.Application;
                if (offer == null || !ReferenceEquals(offer.Leader, leader)
                    || unchecked(EntryClock().Value - offer.Created.Value) >= 60000) return false;
                if (!System.Threading.Monitor.TryEnter(applicant.ItemSyncRoot)) return false;
                bool leaderLocked = false;
                try
                {
                    leaderLocked = System.Threading.Monitor.TryEnter(leader.ItemSyncRoot);
                    if (!leaderLocked || !CurrentLeader(leader) || !Game.Features.QuestInventory.Player(applicant)
                        || !ReferenceEquals(leader.Team, offer.Team) || applicant.Team != null
                        || offer.Team.ForbidJoin || !offer.Team.AllowADD() || offer.Team.Members.ContainsKey(applicant.Entity.UID)) return false;
                    offer.Team.Add(applicant);
                    Game.ConquerStructures.Team.Member member;
                    if (!offer.Team.Members.TryGetValue(applicant.Entity.UID, out member) || !ReferenceEquals(member.entry, applicant)) return false;
                    pending.Invitation = null; pending.Application = null;
                    return Game.Features.WayOfHeroesClaims.RecordTeam(applicant,leader,invitation?(short)1:(short)2);
                }
                finally
                {
                    if (leaderLocked) System.Threading.Monitor.Exit(leader.ItemSyncRoot);
                    System.Threading.Monitor.Exit(applicant.ItemSyncRoot);
                }
            }
        }
        public static void Process(byte[] packet, Client.GameState client)
        {
            if (packet == null || packet.Length < 12 || !Game.Features.QuestInventory.Player(client, false)) return;
            Team team_pak = new Team();
            team_pak.Deserialize(packet);
            // The packet UID is a target, never proof of command authority.
            if (team_pak.Type == ExitTeam || team_pak.Type == Dismiss || team_pak.Type == Kick
                || (team_pak.Type >= ForbidJoining && team_pak.Type <= LootItemsOn))
            {
                var ownedTeam = client.Team;
                if (ownedTeam == null) return;
                var target = client;
                if (team_pak.Type == Kick && !ownedTeam.TryGetMember(team_pak.UID, out target)) return;
                using (var ownership = ownedTeam.OwnCommand(client, target, team_pak.Type != ExitTeam))
                {
                    if (ownership == null) return;
                    ProcessOwned(team_pak, client);
                }
                return;
            }
            ProcessOwned(team_pak, client);
        }
        private static void ProcessOwned(Team team_pak, Client.GameState client)
        {
            switch (team_pak.Type)
            {
                case Team.Create:
                    {
                        lock (client.ItemSyncRoot)
                        {
                        if (client.Entity.MaxHitpoints == 0 || client.Team != null || client.Entity.Dead) break;
                        client.Team = new Game.ConquerStructures.Team(client);
                        if(!Game.Features.WayOfHeroesClaims.RecordTeam(client,client,0))break;
                        team_pak.Type = 15;
                        client.Send(team_pak.ToArray());
                        team_pak.Type = 0;
                        client.Send(team_pak.ToArray());
                        client.Entity.SpookMap = client.Team.SpookMap;
                        }
                        break;
                    }
                case Team.AcceptJoinRequest:
                    {
                        if (client.Team != null && client.Entity.Hitpoints > 0)
                        {
                            if (CurrentLeader(client) && client.Team.AllowADD() && !client.Team.ForbidJoin)
                            {
                                Client.GameState NewTeammate;
                                if (!Kernel.GamePool.TryGetValue(team_pak.UID, out NewTeammate)
                                    || !Game.Features.QuestInventory.Player(NewTeammate) || NewTeammate.Team != null) return;
                                if (!AcceptEntry(client, NewTeammate, false)) return;
                                if (NewTeammate.Entity.SpookMap != null)
                                {
                                    NewTeammate.Entity.SpookMap.Dispose();
                                    NewTeammate.Entity.SpookMap = client.Team.SpookMap;
                                    return;
                                }
                                NewTeammate.Entity.SpookMap = client.Team.SpookMap;
                                AddToTeam teams = new AddToTeam();
                                AddToTeam AddMe = new AddToTeam();
                                AddToTeam AddYou = new AddToTeam();
                                if (client.Team.PickupMoney == true)
                                {
                                    NewTeammate.Send(new Message("Share Silver: On", Color.Yellow, Message.Team));
                                }
                                if (client.Team.PickupMoney == false)
                                {
                                    NewTeammate.Send(new Message("Share Silver: Off", Color.Yellow, Message.Team));
                                }
                                if (client.Team.PickupItems == true)
                                {
                                    NewTeammate.Send(new Message("Share Loots: On", Color.Yellow, Message.Team));
                                }
                                if (client.Team.PickupItems == false)
                                {
                                    NewTeammate.Send(new Message("Share Loots: Off", Color.Yellow, Message.Team));
                                }
                            }
                        }
                        break;
                    }
                case Team.AcceptInvitation:
                    {
                        if (client.Team == null && client.Entity.Hitpoints > 0)
                        {
                            Client.GameState Leader;
                            if (Kernel.GamePool.TryGetValue(team_pak.UID, out Leader))
                            {
                                if (CurrentLeader(Leader))
                                {
                                    if (!Leader.Team.AllowADD() || Leader.Team.ForbidJoin) return;
                                    if (!AcceptEntry(Leader, client, true)) return;
                                    if (client.Team.PickupMoney == true)
                                    {
                                        client.Send(new Message("Share Silver: On", Color.Yellow, Message.Team));
                                    }
                                    if (client.Team.PickupMoney == false)
                                    {
                                        client.Send(new Message("Share Silver: Off", Color.Yellow, Message.Team));
                                    }
                                    if (client.Team.PickupItems == true)
                                    {
                                        client.Send(new Message("Share Loots: On", Color.Yellow, Message.Team));
                                    }
                                    if (client.Team.PickupItems == false)
                                    {
                                        client.Send(new Message("Share Loots: Off", Color.Yellow, Message.Team));
                                    }
                                    if (client.Entity.SpookMap != null)
                                    {
                                        client.Entity.SpookMap.Dispose();
                                        client.Entity.SpookMap = client.Team.SpookMap;
                                        return;
                                    }
                                    client.Entity.SpookMap = client.Team.SpookMap;
                                    return;
                                }
                            }
                        }
                        break;
                    }
                case Team.InviteRequest:
                    {
                        if (client.Team != null)
                        {
                            if (client.Team.AllowADD() && client.Team.TeamLider(client))
                            {
                                Client.GameState Inv;
                                if (Kernel.GamePool.TryGetValue(team_pak.UID, out Inv))
                                {
                                    if (Inv.Team == null && RecordEntry(client, Inv, true))
                                    {
                                        RequestInformation inf = new RequestInformation();
                                        inf.Level = client.Entity.Level;
                                        inf.BattlePower = (uint)client.Entity.BattlePower;
                                        inf.Receiver = Inv.Entity.UID;
                                        inf.Requester = client.Entity.UID;
                                        if (client.Entity.Spouse == Inv.Entity.Name)//Spouse
                                        {
                                            inf.Spouse = RequestInformation.Request;
                                        }
                                        if (client.Friends.ContainsKey(Inv.Entity.UID))//Friends
                                        {
                                            inf.Friends = RequestInformation.Request;
                                        }
                                        if (client.Partners.ContainsKey(Inv.Entity.UID))//TradePartner
                                        {
                                            inf.TradePartner = RequestInformation.Request;
                                        }
                                        if (client.Apprentices.ContainsKey(Inv.Entity.UID))//Mentor
                                        {
                                            inf.Mentor = RequestInformation.Request;
                                        }
                                        if (client.Mentor != null)//Apprentice
                                        {
                                            if (client.Mentor.ID == Inv.Entity.UID)
                                                inf.Apprentice = RequestInformation.Request;
                                        }
                                        if (Inv.Mentor != null)
                                            if (Inv.Mentor.ID == client.Entity.UID)
                                                inf.Apprentice = RequestInformation.Request;
                                        if (client.Team != null && Inv.Team != null)//Team
                                        {
                                            if (client.Team.Contain(Inv.Entity.UID))
                                                if (Inv.Team.Contain(client.Entity.UID))
                                                    inf.Team = RequestInformation.Request;
                                        }
                                        if (client.Entity.GuildID == Inv.Entity.GuildID)//Guild
                                        {
                                            inf.GuildRank = RequestInformation.Request;
                                        }
                                        if (Inv.Enemy.ContainsKey(client.Entity.UID))//Enemy
                                        {
                                            inf.Enemy = RequestInformation.Request;
                                        }
                                        Inv.Send(inf.ToArray());
                                        team_pak.UID = client.Entity.UID;
                                        client.Send(new Message("[Team]The invitation has been send.", Message.System));
                                        Inv.Send(team_pak.ToArray());
                                    }
                                    else client.Send(new Message("[Team]Failed to invite. This player has already joined a team.", Message.System));
                                }
                            }
                        }
                        break;
                    }
                case Team.JoinRequest:
                    {
                        if (client.Team == null && client.Entity.Hitpoints > 0)
                        {
                            Client.GameState Leader;
                            if (Kernel.GamePool.TryGetValue(team_pak.UID, out Leader))
                            {
                                if (Leader.Team != null)
                                {
                                    if (RecordEntry(Leader, client, false))
                                    {
                                        GamePackets.RequestInformation inf = new GamePackets.RequestInformation();
                                        inf.Level = client.Entity.Level;
                                        inf.BattlePower = (uint)client.Entity.BattlePower;
                                        inf.Receiver = Leader.Entity.UID;
                                        inf.Requester = client.Entity.UID;
                                        if (client.Entity.Spouse == Leader.Entity.Name)//Spouse
                                        {
                                            inf.Spouse = RequestInformation.Request;
                                        }
                                        if (client.Friends.ContainsKey(Leader.Entity.UID))//Friends
                                        {
                                            inf.Friends = RequestInformation.Request;
                                        }
                                        if (client.Partners.ContainsKey(Leader.Entity.UID))//TradePartner
                                        {
                                            inf.TradePartner = RequestInformation.Request;
                                        }
                                        if (client.Apprentices.ContainsKey(Leader.Entity.UID))//Mentor
                                        {
                                            inf.Mentor = RequestInformation.Request;
                                        }
                                        if (client.Mentor != null)//Apprentice
                                        {
                                            if (client.Mentor.ID == Leader.Entity.UID)
                                                inf.Apprentice = RequestInformation.Request;
                                        }
                                        if (Leader.Mentor != null)
                                            if (Leader.Mentor.ID == client.Entity.UID)
                                                inf.Apprentice = RequestInformation.Request;
                                        if (client.Team != null && Leader.Team != null)//Team
                                        {
                                            if (client.Team.Contain(Leader.Entity.UID))
                                                if (Leader.Team.Contain(client.Entity.UID))
                                                    inf.Team = RequestInformation.Request;
                                        }
                                        if (client.Entity.GuildID == Leader.Entity.GuildID)//Guild
                                        {
                                            inf.GuildRank = RequestInformation.Request;
                                        }
                                        if (Leader.Enemy.ContainsKey(client.Entity.UID))//Enemy
                                        {
                                            inf.Enemy = RequestInformation.Request;
                                        }
                                        Leader.Send(inf.ToArray());
                                        team_pak.UID = client.Entity.UID;
                                        client.Send(new Message("[Team]The request has been send.", Message.System));
                                        Leader.Send(team_pak.ToArray());
                                    }
                                    else client.Send(new Message("[Team]This team is full.", Message.System));
                                }
                                else client.Send(new Message("[Team]This player has not created a team.", Message.System));
                            }
                        }
                        break;
                    }
                case Team.ExitTeam:
                    {
                        if (client.Team != null)
                        {
                            var previousTeam = client.Team;
                            previousTeam.Remove(client, true);
                            NotifyArenaRemoval(client, previousTeam);
                            client.Entity.SpookMap = null;
                        }
                        break;
                    }
                case Team.Dismiss:
                    {
                        var previousTeam = client.Team;
                        previousTeam.Remove(client, false);
                        NotifyArenaRemoval(client, previousTeam);
                        break;
                    }
                case Team.Kick:
                    {
                        if (client.Team == null) break;
                        Client.GameState remov;
                        if (client.Team.TryGetMember(team_pak.UID, out remov))
                        {
                            var previousTeam = client.Team;
                            previousTeam.Remove(remov, false);
                            NotifyArenaRemoval(remov, previousTeam);
                        }
                        break;
                    }
                case Team.ForbidJoining:
                    {
                        if (client.Team != null)
                        {
                            client.Team.SendTeam(team_pak.ToArray(), true);
                            client.Team.ForbidJoin = true;
                        }
                        break;
                    }
                case Team.UnforbidJoining:
                    {
                        if (client.Team != null)
                        {
                            client.Team.SendTeam(team_pak.ToArray(), true);
                            client.Team.ForbidJoin = false;
                        }
                        break;
                    }
                case Team.LootMoneyOff:
                    {
                        if (client.Team != null)
                        {
                            client.Team.SendTeam(team_pak.ToArray(), true);
                            client.Team.PickupMoney = false;
                        }
                        break;
                    }
                case Team.LootMoneyOn:
                    {
                        if (client.Team != null)
                        {
                            client.Team.SendTeam(team_pak.ToArray(), true);
                            client.Team.PickupMoney = true;
                        }
                        break;
                    }
                case Team.LootItemsOff:
                    {
                        if (client.Team != null)
                        {
                            client.Team.SendTeam(team_pak.ToArray(), true);
                            client.Team.PickupItems = false;
                        }
                        break;
                    }
                case Team.LootItemsOn:
                    {
                        if (client.Team != null)
                        {
                            client.Team.SendTeam(team_pak.ToArray(), true);
                            client.Team.PickupItems = true;
                        }
                        break;
                    }
            }
        }
    }
}
