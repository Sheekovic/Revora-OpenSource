
using Revora.Game;
using Revora.Interfaces;
using ProtoBuf;
using System;

namespace Revora.Network.GamePackets
{
    public unsafe class SobNpcSpawn : Writer, IPacket, INpc, ISobNpc, IMapObject
    {
        public byte[] SpawnPacket;
        public bool _isprize;
        [ProtoContract]
        public class CMsgNpcInfoEX
        {
            [ProtoMember(1, IsRequired = true)]
            public ulong UID;
            [ProtoMember(2, IsRequired = true)]
            public ulong UID2;
            [ProtoMember(3, IsRequired = true)]
            public ulong unkown;
            [ProtoMember(4, IsRequired = true)]
            public ulong MaxHitpoints;
            [ProtoMember(5, IsRequired = true)]
            public ulong Hitpoints;
            [ProtoMember(6, IsRequired = true)]
            public ulong X;
            [ProtoMember(7, IsRequired = true)]
            public ulong Y;
            [ProtoMember(8, IsRequired = true)]
            public ulong Mesh;
            [ProtoMember(9, IsRequired = true)]
            public ulong Type;
            [ProtoMember(10, IsRequired = true)]
            public ulong Sort;
            [ProtoMember(11, IsRequired = true)]
            public ulong ShowName;
            [ProtoMember(12, IsRequired = true)]
            public byte[] Name;
        }
        ushort packetid = 1109;
        private CMsgNpcInfoEX Buffer;
        public SobNpcSpawn()
        {
            Buffer = new CMsgNpcInfoEX();
            ShowName = false;
        }
        public uint UID
        {
            get { return (uint)Buffer.UID; }
            set { Buffer.UID = Buffer.UID2 = value; }
        }
        public uint MaxHitpoints
        {
            get { return (uint)Buffer.MaxHitpoints; }
            set { Buffer.MaxHitpoints = value; }
        }
        public uint Hitpoints
        {
            get { return (uint)Buffer.Hitpoints; }
            set { Buffer.Hitpoints = value; }
        }
        public ushort X
        {
            get { return (ushort)Buffer.X; }
            set { Buffer.X = value; }
        }
        public ushort Y
        {
            get { return (ushort)Buffer.Y; }
            set { Buffer.Y = value; }
        }
        public ushort Mesh
        {
            get { return (ushort)Buffer.Mesh; }
            set { Buffer.Mesh = value; }
        }
        public Enums.NpcType Type
        {
            get { return (Enums.NpcType)Buffer.Type; }
            set { Buffer.Type = (byte)value; }
        }
        public ushort Sort
        {
            get { return (ushort)Buffer.Sort; }
            set { Buffer.Sort = value; }
        }
        public bool ShowName
        {
            get { return Buffer.ShowName == 1; }
            set { Buffer.ShowName = value == true ? (byte)1 : (byte)0; }
        }
        public string LoweredName;
        private string _Name;
        public string Name
        {
            get
            {
                return _Name;
            }
            set
            {
                _Name = value;
                LoweredName = value.ToLower();
                Buffer.Name = System.Text.Encoding.Default.GetBytes(value);
            }
        }
        public Game.MapObjectType MapObjType
        {
            get
            {
                if (MaxHitpoints == 0)
                    return Game.MapObjectType.Npc;
                return Game.MapObjectType.SobNpc;
            }
        }
        public void Die(Game.Entity killer)
        {
            if (UID == 12345)
            {
                Hitpoints = 0;
                Server.Thread.PoleDomination.KillPole();
                return;
            }
            if (UID == 123456)
            {
                Hitpoints = 0;
                Server.Thread.PoleEmulator.KillPole();
                return;
            }
            // CTF base health and capture are serialized by its event owner.
            if (MapID == CaptureTheFlag.MapID) return;
            if (MapID == 1234)
            {
                if (UID != 811)
                {
                    if (Hitpoints != 0 || Mesh != 251 && Mesh != 281)
                    {
                        if (Mesh == 241)
                            Mesh = (ushort)(250 + Mesh % 10);
                        else
                            Mesh = (ushort)(280 + Mesh % 10);

                        FlagsUpdate upd = new FlagsUpdate(true);
                        upd.UID = UID;
                        upd.Append((byte)FlagsUpdate.DataType.Mesh, Mesh);
                        killer.Owner.SendScreen(upd, true);
                        Hitpoints = 0;
                    }
                    Attack attack = new Attack(true);
                    attack.Attacker = killer.UID;
                    attack.Attacked = UID;
                    attack.AttackType = Network.GamePackets.Attack.Kill;
                    attack.X = X;
                    attack.Y = Y;
                    killer.Owner.Send(attack);
                    killer.KOCount++;
                }
            }
            else if (MapID == 2072)
            {
                if (UID != 819)
                {
                    if (Hitpoints != 0 || Mesh != 251 && Mesh != 281)
                    {
                        if (Mesh == 241)
                            Mesh = (ushort)(250 + Mesh % 10);
                        else
                            Mesh = (ushort)(280 + Mesh % 10);

                        FlagsUpdate upd = new FlagsUpdate(true);
                        upd.UID = UID;
                        upd.Append((byte)FlagsUpdate.DataType.Mesh, Mesh);
                        killer.Owner.SendScreen(upd, true);
                        Hitpoints = 0;
                    }
                    Attack attack = new Attack(true);
                    attack.Attacker = killer.UID;
                    attack.Attacked = UID;
                    attack.AttackType = Network.GamePackets.Attack.Kill;
                    attack.X = X;
                    attack.Y = Y;
                    killer.Owner.Send(attack);
                    killer.KOCount++;
                }
            }
            else if (MapID == 1038)
            {
                if (UID != 810)
                {
                    if (Hitpoints != 0 || Mesh != 251 && Mesh != 281)
                    {
                        if (Mesh == 241)
                            Mesh = (ushort)(250 + Mesh % 10);
                        else
                            Mesh = (ushort)(280 + Mesh % 10);

                        FlagsUpdate upd = new FlagsUpdate(true);
                        upd.UID = UID;
                        upd.Append((byte)FlagsUpdate.DataType.Mesh, Mesh);
                        killer.Owner.SendScreen(upd, true);
                        Hitpoints = 0;
                    }
                    Attack attack = new Attack(true);
                    attack.Attacker = killer.UID;
                    attack.Attacked = UID;
                    attack.AttackType = Network.GamePackets.Attack.Kill;
                    attack.X = X;
                    attack.Y = Y;
                    killer.Owner.Send(attack);
                    killer.KOCount++;
                }
            }
            else
            {
                Attack attack = new Attack(true);
                attack.Attacker = killer.UID;
                attack.Attacked = UID;
                attack.AttackType = Network.GamePackets.Attack.Kill;
                attack.X = X;
                attack.Y = Y;
                killer.Owner.Send(attack);
                Hitpoints = MaxHitpoints;
                FlagsUpdate upd = new FlagsUpdate(true);
                upd.UID = UID;
                upd.Append((byte)FlagsUpdate.DataType.Hitpoints, MaxHitpoints);
                killer.Owner.SendScreen(upd, true);
            }
            if (Kernel.Maps[MapID].Statues.ContainsKey(UID))
            {
                Kernel.Maps[MapID].Statues.Remove(UID);
                Kernel.Maps[MapID].Npcs.Remove(UID);
                killer.Owner.Screen.FullWipe();
                killer.Owner.Screen.Reload(null);
            }
        }
        private Client.GameState owner_null = null;
        public Client.GameState Owner
        {
            get { return owner_null; }
            set { owner_null = value; }
        }
        public bool SpawnOnMinutes
        {
            get;
            set;
        }
        public byte BeforeHour
        {
            get;
            set;
        }
        public string effect
        {
            get;
            set;
        }
        public ushort MapID
        {
            get;
            set;
        }
        public void SendSpawn(Client.GameState client, bool checkScreen)
        {
            if (client.Screen.Add(this) || !checkScreen)
            {
                if (Kernel.Maps[MapID].Statues.ContainsKey(UID))
                {
                    WriteUshort(Mesh, SpawnEntity.Facing, SpawnPacket);
                    WriteUshort((ushort)Type, SpawnEntity.Action, SpawnPacket);
                    WriteUshort(X, SpawnEntity.X, SpawnPacket);
                    WriteUshort(Y, SpawnEntity.Y, SpawnPacket);
                    WriteUInt32(Hitpoints, SpawnEntity.Hitpoints, SpawnPacket);
                    WriteUInt32(UID, SpawnEntity.UID, SpawnPacket);
                    client.Send(SpawnPacket);
                    return;
                }
                client.Send(ToArray());
                if (effect != "" && effect != null)
                {
                    client.SendScreen(new _String(true) { UID = UID, TextsCount = 22, Type = 10, Texts = { effect } }, true);
                }
            }
        }
        public void SendSpawn(Client.GameState client)
        {
            SendSpawn(client, false);
        }
        public byte[] ToArray()
        {
            return Buffer.ToConquerPacket(packetid);
        }
        public void Deserialize(byte[] buffer)
        {
            Buffer = buffer.ConvertToProto<CMsgNpcInfoEX>();
        }
        public void Send(Client.GameState client)
        {
            SendSpawn(client, false);
        }
    }
}