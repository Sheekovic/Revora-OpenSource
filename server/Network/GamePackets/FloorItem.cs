using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Revora.Network.GamePackets
{
    public class FloorItem : Writer, Interfaces.IPacket, Interfaces.IMapObject
    {
        internal string DailyBossRun;
        internal int DailyBossLootIndex;
        internal DateTime DailyBossExpiresUtc;
        internal bool Expired(int time)
        {
            if (Game.Features.TeratoRewards.Managed(this)) return Game.Features.TeratoRewards.Expired(this);
            if (Game.Features.SnowBansheeRewards.Managed(this)) return Game.Features.SnowBansheeRewards.Expired(this);
            if (Game.Features.PurpleBansheeRewards.Managed(this)) return Game.Features.PurpleBansheeRewards.Expired(this);
            if (Game.Features.SwordMasterRewards.Managed(this)) return Game.Features.SwordMasterRewards.Expired(this);
            return DailyBossRun != null ? DateTime.UtcNow >= DailyBossExpiresUtc
                : OnFloor.AddSeconds(Constants.FloorItemSeconds).Next(time: time);
        }
        public const byte Drop = 1,
            Remove = 2,
            Animation = 3,
            DropDetain = 4,
            Effect = 10,
            RemoveEffect = 12;
        public const uint
            FlameLotus = 940,
            AuroraLotus = 930,
            DaggerStorm = 1027,
            InfernalEcho = 1397,
              RageOfWarTrap = 1500,
             ShadowOfChaser = 1550,
            ActOfTarget = 1380,
            ActOfTarget2 = 1390,
            TwilightDance = 40,
            FuryofEgg = 41,
            ShacklingIce = 42,
            TideTrap = 1080,
            SeaBurial = 1850;
        public static Counter FloorUID = new Counter(0);
        byte[] Buffer;
        Client.GameState owner;
        ushort mapid;
        public Time32 OnFloor, UseTime;
        public bool PickedUpAlready = false;
        public uint Value;
        public FloorValueType ValueType;
        private ConquerItem item;
        public bool Shake = false;
        public bool Zoom = false;
        public uint TideTrapOnPlayer = 0;
        public uint TideTrapOnMonster = 0;
        public bool Darkness = false;
        #region WindWalker Floor
        public Game.Enums.ConquerAngle Angle;
        public byte Unknown37
        {
            get { return Buffer[37]; }
            set { Buffer[37] = value; }
        }

        public string Name2
        {
            get { return Server.Encoding.GetString(Buffer, 93, 16); }
            set { Writer.Write(value, 93, Buffer); }
        }
        public ushort X2
        {
            get { return BitConverter.ToUInt16(Buffer, 61); }
            set { Writer.Write(value, 61, Buffer); }
        }

        public ushort Y2
        {
            get { return BitConverter.ToUInt16(Buffer, 63); }
            set { Writer.Write(value, 63, Buffer); }
        }

        public Game.Entity ShadowofChaserAttacker, ShadowofChaserAttacked;
        public Attack Attack;
        public ushort nX
        {
            get { return BitConverter.ToUInt16(Buffer, 61); }
            set { WriteUInt16(value, 61, Buffer); }
        }
        public ushort nY
        {
            get { return BitConverter.ToUInt16(Buffer, 63); }
            set { WriteUInt16(value, 63, Buffer); }
        }
        public void AppendFlags()
        {
            WriteInt32((Shake ? 1 : 0) | (Zoom ? 2 : 0) | (Darkness ? 4 : 0), 8, Buffer);
        }
        #endregion
        public IDisposable TimerSubscriptions = null;
        public FloorItem(bool Create)
        {
            if (Create)
            {
                Buffer = new byte[109 + 8]; // 61 + 8
                WriteUInt16(109, 0, Buffer); // 61
                WriteUInt16(1101, 2, Buffer);
                WriteUInt32((uint)Time32.timeGetTime().GetHashCode(), 4, Buffer);
                Value = 0;
                ValueType = FloorValueType.Item;
            }
        }
        public ulong time
        {
            get { return BitConverter.ToUInt64(Buffer, 37 + 8); }
            set { WriteUInt64(value, 37 + 8, Buffer); }
        }
        public uint UID
        {
            get { return BitConverter.ToUInt32(Buffer, 8); }
            set { WriteUInt32(value, 8, Buffer); }
        }
        public uint ItemID
        {
            get { return BitConverter.ToUInt32(Buffer, 12); }
            set { WriteUInt32(value, 12, Buffer); }
        }
        public ushort X
        {
            get { return BitConverter.ToUInt16(Buffer, 16); }
            set { WriteUInt16(value, 16, Buffer); }
        }
        public ushort Y
        {
            get { return BitConverter.ToUInt16(Buffer, 18); }
            set { WriteUInt16(value, 18, Buffer); }
        }
        public Game.Enums.Color ItemColor
        {
            get { return (Game.Enums.Color)BitConverter.ToUInt16(Buffer, 20); }
            set { WriteUInt16((ushort)value, 20, Buffer); }
        }
        public byte Type
        {
            get { return Buffer[22]; }
            set { Buffer[22] = value; }
        }
        public ushort MaxLife
        {
            get { return BitConverter.ToUInt16(Buffer, 20); }
            set { WriteUInt16((ushort)value, 20, Buffer); }
        }
        public uint Life
        {
            get { return BitConverter.ToUInt32(Buffer, 23); }
            set { WriteUInt32(value, 23, Buffer); }
        }
        public byte mColor
        {
            get { return Buffer[27]; }
            set { Buffer[27] = value; }
        }
        public uint OwnerUID
        {
            get { return BitConverter.ToUInt32(Buffer, 28); }
            set { WriteUInt32(value, 28, Buffer); }
        }
        public uint OwnerGuildUID
        {
            get { return BitConverter.ToUInt32(Buffer, 32); }
            set { WriteUInt32(value, 32, Buffer); }
        }
        public byte FlowerType
        {
            get { return Buffer[36]; }
            set { Buffer[36] = value; }
        }
        public byte FlowerType2
        {
            get { return Buffer[37]; }
            set { Buffer[37] = value; }
        }

        public ulong Timer
        {
            get { return BitConverter.ToUInt64(Buffer, 45); }
            set { WriteUInt64(value, 45, Buffer); }
        }
        public ushort dwpram
        {
            get { return BitConverter.ToUInt16(Buffer, 53); }
            set { WriteUInt16(value, 53, Buffer); }
        }
        public string Name
        {
            get { return Server.Encoding.GetString(Buffer, 53, 16); } // 45
            set { WriteString(value, 53, Buffer); } // 45
        }
        public Game.MapObjectType MapObjType
        {
            get { return Game.MapObjectType.Item; }
            set { }
        }
        public void Send(Client.GameState client)
        {
            client.Send(Buffer);
        }
        public void SendSpawn(Client.GameState client)
        {
            SendSpawn(client, false);
        }
        public Client.GameState Owner
        {
            get { return owner; }
            set { owner = value; }
        }
        public ConquerItem Item
        {
            get { return item; }
            set { item = value; }
        }
        public ushort MapID
        {
            get { return mapid; }
            set { mapid = value; }
        }
        public void SendSpawn(Client.GameState client, bool checkScreen)
        {
            if (client.Screen.Add(this) || !checkScreen)
            {
                client.Send(Buffer);
            }
        }
        public byte[] ToArray()
        {
            return Buffer;
        }
        private static int pickupDiagnostics;
        private static void TracePickup(byte[] packet, Client.GameState client)
        {
            if (System.Threading.Interlocked.Increment(ref pickupDiagnostics)>40) return;
            FloorItem drop=null;
            uint uid=BitConverter.ToUInt32(packet,8);
            if(client.Map!=null)client.Map.FloorItems.TryGetValue(uid,out drop);
            FloorItem screen=null;
            bool onScreen=client.Screen!=null && client.Screen.TryGetFloorItem(uid,out screen);
            Console.WriteLine("[pickup] player="+client.Entity.UID+" map="+client.Entity.MapID
                +" request="+uid+" bytes="+packet.Length+" type="+(packet.Length>22?packet[22]:0)
                +" xy="+client.Entity.X+","+client.Entity.Y+" drop="+(drop==null?"missing":drop.X+","+drop.Y+"/"+drop.ValueType+"/"+drop.Type)
                +" screen="+onScreen+" inventory="+(client.Inventory==null?-1:client.Inventory.Count)
                +" dead="+client.Entity.Dead+" trade="+(client.Trade==null?"null":client.Trade.InTrade.ToString())
                +" booth="+(client.Booth!=null)+" reload="+client.Entity.PersistenceRequiresReload
                +" ready="+Game.Features.QuestInventory.Player(client));
        }
        internal static bool ValidPickupFrame(byte[] packet)
        {
            return packet!=null && packet.Length>=32 && (packet.Length<=128 || packet.Length==137)
                && BitConverter.ToUInt16(packet,0)+8==packet.Length
                && BitConverter.ToUInt16(packet,2)==1101
                && BitConverter.ToUInt64(packet,packet.Length-8)==PacketHandler.ClientSeal;
        }
        public static void Process(byte[] packet, Client.GameState client)
        {
            if (client == null || client.Entity == null || client.Action != 2 || packet == null || packet.Length < 12) return;
            TracePickup(packet,client);
            FloorItem current;
            if (client.Map != null && client.Map.FloorItems.TryGetValue(BitConverter.ToUInt32(packet, 8), out current)
                && (Game.Features.NinjaFlowerHunt.Managed(current) || Game.Features.MonsterEquipmentDrops.Managed(current) || Game.Features.GrottoKeyDrops.Managed(current) || Game.Features.MeteorDrops.Managed(current) || Game.Features.CleanWaterDrops.Managed(current) || Game.Features.DailyHuntDrops.Managed(current) || Game.Features.SnowBansheeRewards.Managed(current) || Game.Features.PurpleBansheeRewards.Managed(current) || Game.Features.SwordMasterRewards.Managed(current) || Game.Features.TeratoRewards.Managed(current)))
            {
                if (!ValidPickupFrame(packet)) return;
                if (!Game.Features.NinjaFlowerHunt.Pickup(client, current) && !Game.Features.DailyHuntDrops.Pickup(client, current) && !Game.Features.MeteorDrops.Pickup(client, current) && !Game.Features.CleanWaterDrops.Pickup(client, current) && !Game.Features.SnowBansheeRewards.Pickup(client, current) && !Game.Features.PurpleBansheeRewards.Pickup(client, current) && !Game.Features.SwordMasterRewards.Pickup(client, current) && !Game.Features.TeratoRewards.Pickup(client, current) && !Game.Features.GrottoKeyDrops.Pickup(client, current)) Game.Features.MonsterEquipmentDrops.Pickup(client, current);
                return;
            }
            FloorItem floorItem = new FloorItem(false);
            floorItem.Deserialize(packet);
            client.SpiltStack = true;
            ItemHandler.PickupItem(floorItem, client);
            client.SpiltStack = false;
        }
        public void Deserialize(byte[] buffer)
        {
            Buffer = buffer;
        }
        public enum FloorValueType { Item, Money, ConquerPoints, BoundCps }
    }
}
