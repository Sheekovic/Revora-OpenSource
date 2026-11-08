// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

namespace Revora.Network.GamePackets
{
    public unsafe class MentorPremio
    {
        public const byte
        Show = 0,
        Training = 1,
        Hunting = 2;

        byte[] Packet;
        public MentorPremio()
        {
            Packet = new byte[28];
            fixed (byte* Pack = Packet)
            {
                *(ushort*)(Pack + (Packet.Length - 8)) = 0;
                *((ushort*)(Pack + 2)) = PacketID.MentorPremio;
            }
        }
        public byte Action
        {
            get { return (byte)Packet[8]; }
            set { fixed (byte* Pack = Packet) *((byte*)(Pack + 8)) = value; }
        }
        public uint OnlineTraining
        {
            get { return BitConverter.ToUInt16(Packet, 14); }
            set { fixed (byte* Pack = Packet) *((uint*)(Pack + 14)) = value; }
        }
        public uint BlessedHunting
        {
            get { return BitConverter.ToUInt16(Packet, 18); }
            set { fixed (byte* Pack = Packet) *((uint*)(Pack + 18)) = value; }
        }
        public byte[] ToArray()
        {
            fixed (void* Pack = Packet)
                return Packet;
        }
        public void Deserialize(byte[] buffer)
        {
            Packet = buffer;
        }
        public static void Process(byte[] packet, Client.GameState client)
        {
            MentorPremio Premio = new MentorPremio();
            Premio.Deserialize(packet);
            switch (Premio.Action)
            {
                case MentorPremio.Show:
                    {
                        Premio.OnlineTraining = client.Entity.OnlineTraining;
                        Premio.BlessedHunting = client.Entity.BlessedHunting;
                        client.Send(Premio.ToArray());
                        break;
                    }
                case MentorPremio.Training:
                    {
                        client.IncreaseExperience(Revora.Game.Features.ExperienceMath.Scale(client.ExpBall,client.Entity.OnlineTraining,606), false);
                        client.Entity.OnlineTraining = 0;
                        Premio.Action = MentorPremio.Show;
                        Premio.OnlineTraining = client.Entity.OnlineTraining;
                        Premio.BlessedHunting = client.Entity.BlessedHunting;
                        client.Send(Premio.ToArray());
                        break;
                    }
                case MentorPremio.Hunting:
                    {
                        client.IncreaseExperience(Revora.Game.Features.ExperienceMath.Scale(client.ExpBall,client.Entity.BlessedHunting,600), false);
                        client.Entity.BlessedHunting = 0;
                        Premio.Action = MentorPremio.Show;
                        Premio.OnlineTraining = client.Entity.OnlineTraining;
                        Premio.BlessedHunting = client.Entity.BlessedHunting;
                        client.Send(Premio.ToArray());
                        break;
                    }
                default: System.Console.WriteLine("[MentorPremio] Unknown Action " + Premio.Action); break;
            }
        }
    }
}