using System;
using Revora.Network.GamePackets;
using System.Collections.Concurrent;

namespace Revora.Game
{
    public unsafe class Statue : Network.Writer
    {
        public static ConcurrentDictionary<uint, Statue> Statues = new ConcurrentDictionary<uint, Statue>();
        public byte[] SpawnPacket = null;
        public uint UID;
        public Statue(byte[] array)
        {
            UID = 105175;
            SpawnPacket = new byte[array.Length];
            for (ushort x = 0; x < array.Length; x++)
                SpawnPacket[x] = array[x];
            WriteByte(0, SpawnEntity.Facing, SpawnPacket);
            WriteUInt32(0, SpawnEntity.Action, SpawnPacket);
            WriteUInt16(1000, SpawnEntity.GuildRank, SpawnPacket);
            WriteUInt32(105175, SpawnEntity.UID, SpawnPacket);
            WriteUInt64(0, SpawnEntity.StatusFlag, SpawnPacket);
            WriteUInt64(0, SpawnEntity.StatusFlag2, SpawnPacket);
            WriteUInt64(0, SpawnEntity.StatusFlag3, SpawnPacket);
            WriteUInt32(0, SpawnEntity.Hitpoints, SpawnPacket);
            WriteUInt32(0, SpawnEntity.GuildID, SpawnPacket);
            X = 310;
            Y = 277;
            if (array.Length > 200)
            {
                if (!Statues.ContainsKey(105175))
                    Statues.TryAdd(105175, this);
                else
                {
                    Statues[105175] = this;
                }
            }
            foreach (var client in Kernel.GamePool.Values)
            {
                if (Kernel.GetDistance(X, Y, client.Entity.X, client.Entity.Y) < 16 && client.Entity.MapID == 1002)
                {
                    client.Send(this.SpawnPacket);
                }
            }
        }
        ushort _x, _y;
        public ushort X
        {
            get { return _x; }
            set
            {
                _x = value;
                WriteUInt16(value, SpawnEntity.X, SpawnPacket);
            }
        }
        public ushort Y
        {
            get { return _y; }
            set
            {
                _y = value;
                WriteUInt16(value, SpawnEntity.Y, SpawnPacket);
            }
        }
        public static bool operator >(Statue statue, Client.GameState client)
        {
            if (!client.Screen.Statue.ContainsKey(statue.UID))
            {
                if (Kernel.GetDistance(statue.X, statue.Y, client.Entity.X, client.Entity.Y) < 16 && client.Entity.MapID == 1002)
                {
                    if (statue.SpawnPacket.Length > 200)
                    {
                        client.Send(statue.SpawnPacket);
                        return true;
                    }
                }
            }
            return false;
        }
        public static bool operator <(Statue statue, Client.GameState client)
        {
            if (Kernel.GetDistance(statue.X, statue.Y, client.Entity.X, client.Entity.Y) >= 16 && client.Entity.MapID == 1002)
            {
                return true;
            }
            return false;
        }
    }
}