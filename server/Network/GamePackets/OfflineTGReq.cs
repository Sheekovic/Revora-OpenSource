// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace Revora.Network.GamePackets
{
    public unsafe class OfflineTGRequest : Writer, Interfaces.IPacket
    {
        public const byte
        OnTrainingTimeRequested = 0,
        OnConfirmation = 1,
        FindConfirmation = 2,
        ReplyToConfirmation = 3,
        ClaimExperience = 4,
        CantTakeConfirmation = 5;

        private byte[] Buffer;
        public OfflineTGRequest(bool create)
        {
            if (create)
            {
                Buffer = new byte[20];
                WriteUInt16((ushort)(Buffer.Length - 8), 0, Buffer);
                WriteUInt16(2044, 2, Buffer);
            }
        }
        public byte ID
        {
            get { return Buffer[4]; }
            set { Buffer[4] = value; }
        }
        public ushort Minutes
        {
            get { return BitConverter.ToUInt16(Buffer, 8); }
            set { WriteUInt16(value, 8, Buffer); }
        }
        public byte[] ToArray()
        {
            return Buffer;
        }
        public void Deserialize(byte[] source)
        {
            Buffer = source;
        }
        public void Send(Client.GameState client)
        {
            client.Send(Buffer);
        }
        public static void Process(byte[] packet, Client.GameState client)
        {
            OfflineTGRequest otgr = new OfflineTGRequest(false);
            otgr.Deserialize(packet);
            switch (otgr.ID)
            {
                case OfflineTGRequest.OnTrainingTimeRequested:
                    {
                        if (client.Map.BaseID == 6001
                            || client.Map.BaseID == 6000
                            || client.Map.BaseID == 1038)
                        {
                            otgr.ID = 5;
                            client.Send(otgr);
                            return;
                        }
                        otgr.Minutes = 900;
                        client.Send(otgr);
                        break;
                    }
                case OfflineTGRequest.OnConfirmation:
                    {
                        client.Entity.PreviousMapID = client.Entity.MapID;
                        client.Entity.PrevX = client.Entity.X;
                        client.Entity.PrevY = client.Entity.Y;
                        client.Entity.MapID = 601;
                        client.Entity.X = 64;
                        client.Entity.Y = 56;
                        client.OfflineTGEnterTime = DateTime.Now;
                        client.Disconnect();
                        break;
                    }
                case OfflineTGRequest.ClaimExperience:
                    {
                        ushort minutes = Revora.Game.Features.ExperienceMath.TrainingMinutes(client.OfflineTGEnterTime,DateTime.Now);
                        int remainingMinutes = minutes;
                        while (remainingMinutes >= 300)
                        {
                            remainingMinutes -= 300;
                            client.IncreaseExperience(client.ExpBall, false);
                        }
                        if (remainingMinutes != 0)
                            client.IncreaseExperience(Revora.Game.Features.ExperienceMath.Scale(client.ExpBall,(ulong)remainingMinutes,300), false);

                        client.Entity.SetLocation(client.Entity.PreviousMapID, client.Entity.PrevX, client.Entity.PrevY);
                        if (client.Map.ID == 1036 || client.Map.ID == 1039)

                            client.Entity.Teleport(1002, 408, 330);
                        else
                        {
                            switch (client.Map.ID)
                            {
                                default:
                                    {
                                        client.Entity.Teleport(1002, 408, 330);
                                        break;
                                    }
                                case 1000:
                                    {
                                        client.Entity.Teleport(1000, 500, 650);
                                        break;
                                    }
                                case 1020:
                                    {
                                        client.Entity.Teleport(1020, 565, 562);
                                        break;
                                    }
                                case 1011:
                                    {
                                        client.Entity.Teleport(1011, 188, 264);
                                        break;
                                    }
                                case 1015:
                                    {
                                        client.Entity.Teleport(1015, 717, 571);
                                        break;
                                    }
                            }
                        }
                        client.OfflineTGEnterTime = DateTime.Now;
                        break;
                    }
            }
        }
    }
}