// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Linq;
using System.Text;
using Revora.Client;
using Revora.Database;
using System.Collections.Generic;
using Revora.Game.ConquerStructures;

namespace Revora.Network.GamePackets
{
    public unsafe class RetreatChi : Writer, Interfaces.IPacket
    {
        byte[] Buffer;
        public enum RetreatType
        {
            #region client
            Info = 0,
            RequestRetreat = 1,
            RequestRestore = 3,
            RequestExtend = 5,
            RequestPayoff = 7,
            RequestAbondan = 9,
            RequestUpdate = 11,
            RequestExtend2 = 13,
            #endregion
            #region Server
            Retreat = 2,
            Restore = 4,
            Extend = 6,
            Payoff = 8,
            Abondan = 10,
            Update = 12,
            Extend2 = 14,
            #endregion Server
        }
        public RetreatChi(bool Create)
        {
            if (Create)
            {
                Buffer = new byte[7 + 8];
                Writer.WriteUInt16(7, 0, Buffer);
                Writer.WriteUInt16(2536, 2, Buffer);
            }
        }
        public RetreatType Type
        {
            get { return (RetreatType)BitConverter.ToUInt16(Buffer, 4); }
            set { WriteUInt16((ushort)value, 4, Buffer); }
        }
        public Game.Enums.ChiPowerType Mode
        {
            get { return (Game.Enums.ChiPowerType)Buffer[6]; }
            set { Buffer[6] = (byte)value; }
        }
        public void Send(GameState client)
        {
            client.Send(Buffer);
        }
        public byte[] ToArray()
        {
            return Buffer;
        }
        public void Deserialize(byte[] buffer)
        {
            Buffer = buffer;
        }
        public static void Process(byte[] packet, GameState client)
        {
            RetreatChi chi = new RetreatChi(false);
            chi.Deserialize(packet);
            switch (chi.Type)
            {
                default:
                    {
                        GeneralData.PrintPacket(packet);
                        break;
                    }
                case Network.GamePackets.RetreatChi.RetreatType.Info:
                    {
                        var powers = client.Retretead_ChiPowers;
                        Network.GamePackets.RetreatChi2 RetreatChi = new Network.GamePackets.RetreatChi2(powers.Count);
                        RetreatChi.Append(powers);
                        client.Send(RetreatChi);
                        break;
                    }
                case Network.GamePackets.RetreatChi.RetreatType.RequestRetreat:
                    {
                        if (client.ChiPoints >= 4000)
                        {
                            var power = client.ChiPowers[(int)chi.Mode - 1];
                            if (!client.Retretead_ChiPowers.Contains(power))
                            {
                                client.AdjustChiPoints(-((long)4000));
                                client.Retretead_ChiPowers.Add(power);
                                client.Retretead_ChiPowers[(int)chi.Mode - 1].Retreat_TimeLeft = DateTime.Now.AddDays(5);
                                Network.GamePackets.RetreatChi RetreatChi = new Network.GamePackets.RetreatChi(true);
                                RetreatChi = chi;
                                RetreatChi.Type = Network.GamePackets.RetreatChi.RetreatType.Retreat;
                                RetreatChi.Send(client);
                                ChiTable.Save(client);
                            }
                        }
                        else
                            return;
                        break;
                    }
                case Network.GamePackets.RetreatChi.RetreatType.RequestRestore://Restore
                    {
                        var power = client.Retretead_ChiPowers[(int)chi.Mode - 1];
                        if (client.Retretead_ChiPowers.Contains(power))
                        {
                            client.ChiPowers[(int)chi.Mode - 1].Attributes = client.Retretead_ChiPowers[(int)chi.Mode - 1].Attributes;

                            var powers = client.ChiPowers[(int)chi.Mode - 1];
                            powers.CalculatePoints();
                            Database.ChiTable.Sort(chi.Mode);
                            powers.Power = chi.Mode;
                            client.Send(new ChiPowers(true).Query(client));
                            #region update ranking
                            Database.ChiTable.ChiData[] array = null;
                            switch (chi.Mode)
                            {
                                case Game.Enums.ChiPowerType.Dragon:
                                    array = Database.ChiTable.Dragon;
                                    break;

                                case Game.Enums.ChiPowerType.Phoenix:
                                    array = Database.ChiTable.Phoenix;
                                    break;

                                case Game.Enums.ChiPowerType.Tiger:
                                    array = Database.ChiTable.Tiger;
                                    break;

                                case Game.Enums.ChiPowerType.Turtle:
                                    array = Database.ChiTable.Turtle;
                                    break;
                            }
                            foreach (var chiData in array)
                            {
                                if (Kernel.GamePool.ContainsKey(chiData.UID))
                                {
                                    var pClient = Kernel.GamePool[chiData.UID];
                                    if (pClient == null) continue;
                                    if (pClient.ChiData == null) continue;
                                    //   PacketHandler.SendRankingQuery(new GenericRanking(true) { Mode = GenericRanking.QueryCount }, pClient, GenericRanking.Chi + (uint)chi.Mode, pClient.ChiData.SelectRank(chi.Mode), pClient.ChiData.SelectPoints(chi.Mode));
                                    // Include characters who just fell out of the top 50.
                                    pClient.LoadItemStats();
                                }
                            }
                            #endregion
                            Database.ChiTable.Save(client);

                            Network.GamePackets.RetreatChi RetreatChi = new Network.GamePackets.RetreatChi(true);
                            RetreatChi = chi;
                            RetreatChi.Type = Network.GamePackets.RetreatChi.RetreatType.Restore;//Restore
                            RetreatChi.Send(client);
                        }
                        break;
                    }
                case Network.GamePackets.RetreatChi.RetreatType.RequestExtend://Extend
                    {
                        if (client.ChiPoints >= 4000)
                        {
                            var power = client.Retretead_ChiPowers[(int)chi.Mode - 1];
                            if (client.Retretead_ChiPowers.Contains(power))
                            {
                                client.AdjustChiPoints(-((long)4000));
                                client.Retretead_ChiPowers[(int)chi.Mode - 1].Retreat_TimeLeft = client.Retretead_ChiPowers[(int)chi.Mode - 1].Retreat_TimeLeft.AddDays(5);
                                Network.GamePackets.RetreatChi RetreatChi = new Network.GamePackets.RetreatChi(true);
                                RetreatChi = chi;
                                RetreatChi.Type = Network.GamePackets.RetreatChi.RetreatType.Extend;//Extend
                                RetreatChi.Send(client);
                                ChiTable.Save(client);
                            }
                        }
                        else
                            return;
                        break;
                    }

                case Network.GamePackets.RetreatChi.RetreatType.RequestPayoff://Restore pay off
                    {
                        var power = client.Retretead_ChiPowers[(int)chi.Mode - 1];
                        if (client.Retretead_ChiPowers.Contains(power))
                        {
                            client.ChiPowers[(int)chi.Mode - 1].Attributes = client.Retretead_ChiPowers[(int)chi.Mode - 1].Attributes;

                            var powers = client.ChiPowers[(int)chi.Mode - 1];
                            powers.CalculatePoints();
                            Database.ChiTable.Sort(chi.Mode);
                            powers.Power = chi.Mode;
                            client.Send(new ChiPowers(true).Query(client));
                            #region update ranking
                            Database.ChiTable.ChiData[] array = null;
                            switch (chi.Mode)
                            {
                                case Game.Enums.ChiPowerType.Dragon:
                                    array = Database.ChiTable.Dragon;
                                    break;

                                case Game.Enums.ChiPowerType.Phoenix:
                                    array = Database.ChiTable.Phoenix;
                                    break;

                                case Game.Enums.ChiPowerType.Tiger:
                                    array = Database.ChiTable.Tiger;
                                    break;

                                case Game.Enums.ChiPowerType.Turtle:
                                    array = Database.ChiTable.Turtle;
                                    break;
                            }
                            foreach (var chiData in array)
                            {
                                if (Kernel.GamePool.ContainsKey(chiData.UID))
                                {
                                    var pClient = Kernel.GamePool[chiData.UID];
                                    if (pClient == null) continue;
                                    if (pClient.ChiData == null) continue;
                                    //   PacketHandler.SendRankingQuery(new GenericRanking(true) { Mode = GenericRanking.QueryCount }, pClient, GenericRanking.Chi + (uint)chi.Mode, pClient.ChiData.SelectRank(chi.Mode), pClient.ChiData.SelectPoints(chi.Mode));
                                    // Include characters who just fell out of the top 50.
                                    pClient.LoadItemStats();
                                }
                            }
                            #endregion
                            Database.ChiTable.Save(client);

                            Network.GamePackets.RetreatChi RetreatChi = new Network.GamePackets.RetreatChi(true);
                            RetreatChi = chi;
                            RetreatChi.Type = Network.GamePackets.RetreatChi.RetreatType.Payoff;//Restore pay off
                            RetreatChi.Send(client);
                        }
                        break;
                    }
                case Network.GamePackets.RetreatChi.RetreatType.RequestAbondan://Abondan 
                    {
                        var power = client.Retretead_ChiPowers[(int)chi.Mode - 1];
                        if (client.Retretead_ChiPowers.Contains(power))
                        {
                            client.Retretead_ChiPowers.Remove(power);
                            Network.GamePackets.RetreatChi RetreatChi = new Network.GamePackets.RetreatChi(true);
                            RetreatChi = chi;
                            RetreatChi.Type = Network.GamePackets.RetreatChi.RetreatType.Abondan;//Abondan 
                            RetreatChi.Send(client);
                            Database.ChiTable.Save(client);
                        }
                        break;
                    }
                case Network.GamePackets.RetreatChi.RetreatType.RequestUpdate://Update
                    {
                        var power = client.Retretead_ChiPowers[(int)chi.Mode - 1];
                        if (client.Retretead_ChiPowers.Contains(power))
                        {
                            client.Retretead_ChiPowers[(int)chi.Mode - 1].Attributes = client.ChiPowers[(int)chi.Mode - 1].Attributes;
                            Network.GamePackets.RetreatChi RetreatChi = new Network.GamePackets.RetreatChi(true);
                            RetreatChi = chi;
                            RetreatChi.Type = Network.GamePackets.RetreatChi.RetreatType.Update;//Update
                            RetreatChi.Send(client);
                            ChiTable.Save(client);
                        }
                        break;
                    }
                case Network.GamePackets.RetreatChi.RetreatType.RequestExtend2://Extend
                    {
                        if (client.ChiPoints >= 4000)
                        {
                            var power = client.Retretead_ChiPowers[(int)chi.Mode - 1];
                            if (client.Retretead_ChiPowers.Contains(power))
                            {
                                client.AdjustChiPoints(-((long)4000));
                                client.Retretead_ChiPowers[(int)chi.Mode - 1].Retreat_TimeLeft = client.Retretead_ChiPowers[(int)chi.Mode - 1].Retreat_TimeLeft.AddDays(5);
                                Network.GamePackets.RetreatChi RetreatChi = new Network.GamePackets.RetreatChi(true);
                                RetreatChi = chi;
                                RetreatChi.Type = Network.GamePackets.RetreatChi.RetreatType.Extend2;//Extend
                                RetreatChi.Send(client);
                                ChiTable.Save(client);
                            }
                        }
                        else
                            return;
                        break;
                    }

            }
        }
    }
    public unsafe class RetreatChi2 : Writer, Interfaces.IPacket
    {
        byte[] Buffer;
        int Offset = 8;
        public RetreatChi2(int count)
        {

            {
                Buffer = new byte[8 + 8 + 21 * count];
                Writer.WriteUInt16((ushort)(Buffer.Length - 8), 0, Buffer);
                Writer.WriteUInt16(2537, 2, Buffer);
                Count = (uint)count;
            }
        }
        private uint Count
        {
            get { return BitConverter.ToUInt32(Buffer, 4); }
            set { WriteUInt32(value, 4, Buffer); }
        }
        public void Append(List<ChiPowerStructure> powers)
        {
            if (powers == null) return;
            for (int i = 0; i < powers.Count; i++)
            {
                Append(powers[i]);
            }
        }
        private void Append(ChiPowerStructure power)
        {
            if (power == null) return;
            Buffer[Offset] = (byte)power.Power;
            Offset++;
            var now = power.Retreat_TimeLeft;
            uint secs = (uint)(now.Year % 100 * 100000000 +
                            (now.Month) * 1000000 +
                            now.Day * 10000 +
                            now.Hour * 100 +
                            now.Minute);
            Writer.Uint((uint)secs, Offset, Buffer);
            Offset += 4;
            var attributes = power.Attributes;
            foreach (var attribute in attributes)
            {
                Writer.WriteInt32(attribute, Offset, Buffer);
                Offset += 4;
            }
        }
        public void Send(GameState client)
        {
            client.Send(Buffer);
        }
        public byte[] ToArray()
        {
            return Buffer;
        }
        public void Deserialize(byte[] buffer)
        {
            Buffer = buffer;
        }
    }
}