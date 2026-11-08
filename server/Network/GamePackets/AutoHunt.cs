// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

namespace Revora.Network.GamePackets
{
    public unsafe class AutoHunt
    {
        public enum Mode : byte
        {
            None,
            Start,
            EndAuto,
            EXPGained,
            Unknown4,
            Unknown5,
            KilledBy,
            ChangedMap
        }
        byte[] Packet;
        public AutoHunt()
        {
            Packet = new byte[100];
            fixed (byte* Pack = Packet)
            {
                *(ushort*)(Pack + (Packet.Length - 8)) = 0;
                *((ushort*)(Pack + 2)) = PacketID.AutoHunt;
            }
        }
        public Mode Action
        {
            get { return (Mode)Packet[4]; }
            set { fixed (byte* Pack = Packet) *((byte*)(Pack + 4)) = (byte)value; }
        }
        public ushort Icon
        {
            get { return BitConverter.ToUInt16(Packet, 6); }
            set { fixed (byte* Pack = Packet) *((ushort*)(Pack + 6)) = value; }
        }
        public ulong EXPGained
        {
            get { return BitConverter.ToUInt64(Packet, 14); }
            set { fixed (byte* Pack = Packet) *((ulong*)(Pack + 14)) = value; }
        }
        public uint Unknown
        {
            get { return BitConverter.ReadUint(Packet, 22); }
            set { fixed (byte* Pack = Packet) *((uint*)(Pack + 22)) = value; }
        }
        public string KilledName
        {
            set { Writer.WriteStringWithLength(value, 23, Packet); }
        }
        public byte[] ToArray()
        {
            fixed (void* Pack = Packet)
                return Packet;
        }
        public void Deserialize(byte[] Pack)
        {
            Packet = Pack;
        }
        public static void Process(byte[] packet, Client.GameState client)

        {
            if ( client.Entity.VIPLevel < 1)
            {
                client.Send(new Network.GamePackets.NpcReply(6, "Auto-hunt and automatic gold pickup require VIP 1 or higher."));
                return;
            }
            AutoHunt AutoHunt = new AutoHunt();
            AutoHunt.Deserialize(packet);
            switch (AutoHunt.Action)
            {

                case AutoHunt.Mode.Start:
                    {
                        AutoHunt.Action = AutoHunt.Mode.Start;
                        client.Entity.AddFlag((ulong)FlagsUpdate.Flags.AutoHunting,Game.StatusFlagsBigVector32.PermanentFlag,false);
                        client.Entity.AutoHuntEXP = 0;
                        client.Entity.InAutoHunt = true;
                        break;
                    }
                case AutoHunt.Mode.EndAuto:
                    {
                        AutoHunt.Action = AutoHunt.Mode.EndAuto;
                        AutoHunt.EXPGained = client.Entity.AutoHuntEXP;
                        client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.AutoHunting);
                        client.Entity.AutoHuntEXP = 0;
                        client.Entity.InAutoHunt = false;
                        break;
                    }
                case AutoHunt.Mode.EXPGained:
                    {
                        AutoHunt.Action = AutoHunt.Mode.EXPGained;
                        AutoHunt.EXPGained = client.Entity.AutoHuntEXP;
                        client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.AutoHunting);
                        client.Entity.AutoHuntEXP = 0;
                        client.Entity.InAutoHunt = false;
                        break;
                    }
                default: System.Console.WriteLine("[AutoHunt] Unknown Action: " + AutoHunt.Action + ""); break;
            }
            client.Send(AutoHunt.ToArray());
        }
    }
}
