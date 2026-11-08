// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Linq;
using System.Text;
using Revora.Network;
using System.Collections.Generic;
using Revora.Network.GamePackets;
using System.Runtime.InteropServices;

namespace Revora.Roulette
{
    public unsafe struct MsgRouletteSignUp : Interfaces.IPacket
    {
        public byte[] packet;
        public static MsgRouletteSignUp Create()
        {
            MsgRouletteSignUp ptr = new MsgRouletteSignUp();
            ptr.Length = 9;
            ptr.PacketID = Network.GamePackets.PacketID.RouletteSignUp;
            return ptr;
        }
        public MsgRouletteSignUp(byte[] stream)
        {
            packet = stream;
        }
        public enum ActionJoin : byte
        {
            Join = 0,
            Invite = 1,
            TelePort = 2,
            CancelInvite = 3,
            Watch = 4,
            InfoTable = 5,
            Quit = 7
        }
        public ushort Length
        {
            set
            {
                packet = new byte[value + 8];
                Writer.WriteUshort((ushort)(packet.Length - 8), 0, packet);
            }
        }
        public ushort PacketID
        {
            set { Writer.WriteUshort(value, 2, packet); }
        }
        public ActionJoin Action
        {
            get { return (ActionJoin)packet[4]; }
            set { packet[4] = (byte)value; }
        }
        public uint UID 
        { 
            get { return Revora.BitConverter.ReadUint(packet, 5); }
            set { Writer.WriteUint(value, 5, packet); }
        }
        public byte[] ToArray()
        {
            return packet;
        }
        public void Send(Client.GameState client)
        {
            client.Send(ToArray());
        }
        public void Deserialize(byte[] buffer)
        {
            throw new NotImplementedException();
        }
        public static void Poroces(Client.GameState user, byte[] stream)
        {
            MsgRouletteSignUp Info = new MsgRouletteSignUp(stream);
            switch (Info.Action)
            {
                case ActionJoin.InfoTable:
                    {
                        Database.Roulettes.RouletteTable Table;
                        if (Database.Roulettes.RoulettesPoll.TryGetValue(Info.UID, out Table))
                        {
                            Table.SpawnPacket.PlayersCount = (byte)Table.RegistredPlayers.Count;
                            user.Send(Table.SpawnPacket);
                        }
                        break;
                    }
                case ActionJoin.Invite:
                    {
                        foreach (Game.ConquerStructures.Society.Friend friend in user.Friends.Values)
                        {
                            Client.GameState Client = Kernel.GamePool[friend.ID];
                            if (Client.Entity.MapID == 1858)
                            {
                                user.Send(new Message("This Player Cant Receive Invitaion On The Current Map. Please Try Again Later", Message.System));
                                return;
                            }
                            if (Client.Entity.SendRouletteAutoInvite)
                            {
                                user.Send(new Message("You Have Sent Invitaion To This Player. Please Try Again Later", Message.System));
                                return;
                            }
                            if (friend.IsOnline)
                            {
                                MsgRouletteAutoInvite Send = MsgRouletteAutoInvite.Create();
                                Send.TableID = 1;
                                Send.Name = user.Entity.Name;
                                Send.UID = friend.ID;
                                Send.MoneyType = 1;
                                Client.Entity.SendRouletteAutoInvite = true;
                                Client.Send(Send);
                            }
                        }
                        break;
                    }
                case ActionJoin.TelePort:
                    {
                        foreach (Game.ConquerStructures.Society.Friend friend in user.Friends.Values)
                        {
                            Client.GameState Client = Kernel.GamePool[friend.ID];
                            if (friend.IsOnline)
                            {
                                user.Entity.Teleport(Client.Entity.MapID, (ushort)(Client.Entity.X - 3), (ushort)(Client.Entity.Y - 3));
                            }
                        }
                        break;
                    }
                case ActionJoin.CancelInvite:
                    {
                        user.Entity.SendRouletteAutoInvite = false;
                        break;
                    }
                case ActionJoin.Join:
                    {
                        Database.Roulettes.RouletteTable Table;
                        if (Database.Roulettes.RoulettesPoll.TryGetValue(Info.UID, out Table))
                        {
                            switch (Table.SpawnPacket.MoneyType)
                            {
                                case MsgRouletteTable.TableType.ConquerPoints:
                                    {
                                        if (user.Entity.ConquerPoints >= 5)
                                        {
                                            Table.AddPlayer(user);
                                        }
                                        else user.Send(new Message("Sorry, you should have at least 5 CPs to join in the Roulette.", Message.System));
                                        break;
                                    }
                                case MsgRouletteTable.TableType.Money:
                                    {
                                        if (user.Entity.Money >= 5000)
                                        {
                                            Table.AddPlayer(user);
                                        }
                                        else user.Send(new Message("Sorry, you should have at least 5,000 Silvers to join in the Roulette.", Message.System));
                                        break;
                                    }
                            }
                        }
                        break;
                    }
                case ActionJoin.Watch:
                    {
                        Database.Roulettes.RouletteTable Table;
                        if (Database.Roulettes.RoulettesPoll.TryGetValue(Info.UID, out Table))
                        {
                            Table.AddWatch(user);
                        }
                        break;
                    }
                default: Console.WriteLine("MsgRouletteSignUp Unknown Action " + Info.Action); break;
            }
        }
    }
}