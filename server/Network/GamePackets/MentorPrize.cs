// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Text;
using System.Drawing;
using System.Collections.Generic;

namespace Revora.Network.GamePackets
{
    public unsafe class MentorPrize : Writer, Interfaces.IPacket
    {
        public const byte
        Show = 0,
        ClaimExperience = 1,
        ClaimSomeExperience = 2,
        ClaimHeavenBlessing = 3,
        ClaimPlus = 4,
        Unknown = 5;

        private byte[] Buffer;
        public MentorPrize(bool create)
        {
            if (!create)
            {
                Buffer = new byte[48];
                WriteUInt16((ushort)(Buffer.Length - 8), 0, Buffer);
                WriteUInt16(2067, 2, Buffer);
            }
        }
        public uint Prize_Type
        {
            get { return BitConverter.ToUInt32(Buffer, 4); }
            set { WriteUInt32(value, 4, Buffer); }
        }
        public uint Mentor_ID
        {
            get { return BitConverter.ToUInt32(Buffer, 8); }
            set { WriteUInt32(value, 8, Buffer); }
        }
        public ulong Prize_Experience
        {
            get { return BitConverter.ToUInt64(Buffer, 24); }
            set { WriteUInt64(value, 24, Buffer); }
        }
        public ushort Prize_HeavensBlessing
        {
            get { return BitConverter.ToUInt16(Buffer, 32); }
            set { WriteUInt16(value, 32, Buffer); }
        }
        public ushort Prize_PlusStone
        {
            get { return BitConverter.ToUInt16(Buffer, 34); }
            set { WriteUInt16(value, 34, Buffer); }
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
        public static void Process(byte[] packet, Client.GameState client)
        {
            if (packet == null || packet.Length < 44 || packet.Length > ushort.MaxValue + 8
                || BitConverter.ToUInt16(packet, 0) + 8 != packet.Length || BitConverter.ToUInt16(packet, 2) != 2067
                || BitConverter.ToUInt64(packet, packet.Length - 8) != PacketHandler.ClientSeal
                || !Game.Features.QuestInventory.Player(client)) return;
            lock (client.ItemSyncRoot)
            lock (Game.Features.MentorExperience.Sync)
            {
                if (!Game.Features.QuestInventory.Player(client)) return;
                try
                {
                    Game.Features.MentorDepartures.ClaimFees(client);
                    if (!Game.Features.QuestInventory.Player(client)) return;
                    Game.Features.MentorStoneClaims.Refresh(client);
                    Game.Features.MentorBlessingClaims.Refresh(client);
                }
                catch (Exception ex) { Game.Features.QuestInventory.Fail(client, "mentor-prize", ex); return; }
                ProcessOwned(packet, client);
            }
        }
        private static void ProcessOwned(byte[] packet, Client.GameState client)
        {
            MentorPrize prize = new MentorPrize(false);
            prize.Deserialize(packet);
            switch (prize.Prize_Type)
            {
                case MentorPrize.ClaimExperience:
                case MentorPrize.ClaimSomeExperience:
                    {
                        Game.Features.MentorExperience.Claim(client);
                        if (!Game.Features.QuestInventory.Player(client)) return;
                        prize.Mentor_ID = client.Entity.UID;
                        prize.Prize_Type = MentorPrize.Show;
                        prize.Prize_Experience = client.PrizeExperience;
                        prize.Prize_HeavensBlessing = client.PrizeHeavenBlessing;
                        prize.Prize_PlusStone = client.PrizePlusStone;
                        client.Send(prize);
                        break;
                    }
                case MentorPrize.ClaimHeavenBlessing:
                    {
                        Game.Features.MentorBlessingClaims.Claim(client);
                        if (!Game.Features.QuestInventory.Player(client)) return;
                        prize.Mentor_ID = client.Entity.UID;
                        prize.Prize_Type = MentorPrize.Show;
                        prize.Prize_Experience = client.PrizeExperience;
                        prize.Prize_HeavensBlessing = client.PrizeHeavenBlessing;
                        prize.Prize_PlusStone = client.PrizePlusStone;
                        client.Send(prize);
                        break;
                    }
                case MentorPrize.ClaimPlus:
                    {
                        Game.Features.MentorStoneClaims.Claim(client);
                        if (!Game.Features.QuestInventory.Player(client)) return;
                        prize.Mentor_ID = client.Entity.UID;
                        prize.Prize_Type = MentorPrize.Show;
                        prize.Prize_Experience = client.PrizeExperience;
                        prize.Prize_HeavensBlessing = client.PrizeHeavenBlessing;
                        prize.Prize_PlusStone = client.PrizePlusStone;
                        client.Send(prize);
                        break;
                    }
                default:
                    {
                        prize.Mentor_ID = client.Entity.UID;
                        prize.Prize_Experience = client.PrizeExperience;
                        prize.Prize_HeavensBlessing = client.PrizeHeavenBlessing;
                        prize.Prize_PlusStone = client.PrizePlusStone;
                        client.Send(prize);
                        break;
                    }
            }
        }
    }
}
