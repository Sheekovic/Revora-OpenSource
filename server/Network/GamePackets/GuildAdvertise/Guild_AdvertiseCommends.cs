// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using Revora.Client;

namespace Revora.Network.GamePackets
{
    public unsafe class Guild_AdvertiseCommends : Writer, Interfaces.IPacket
    {
        public const byte
        None = 0,
        Join = 1,
        Recruit = 2;

        byte[] buffer;
        public Guild_AdvertiseCommends()
        {

            buffer = new byte[38 + 8];
            WriteUInt16(38, 0, buffer);
            WriteUInt16(2227, 2, buffer);

        }
        public uint Action
        {
            get { return BitConverter.ToUInt32(buffer, 4); }
            set { Writer.WriteUInt32(value, 4, buffer); }
        }
        public uint GuildID
        {
            get { return BitConverter.ToUInt32(buffer, 8); }
            set { Writer.WriteUInt32(value, 8, buffer); }
        }
        public byte[] ToArray()
        {
            return buffer;
        }
        public void Send(GameState client)
        {
            client.Send(buffer);
        }
        public void Deserialize(byte[] buffr)
        {
            buffer = buffr;
        }
        public static void Process(byte[] packet, GameState client)
        {
            Guild_AdvertiseCommends adv = new Guild_AdvertiseCommends();
            adv.Deserialize(packet);
            switch (adv.Action)
            {
                case Guild_AdvertiseCommends.Join:
                    {
                        Game.ConquerStructures.Society.Guild G;
                        if (Kernel.Guilds.TryGetValue(adv.GuildID, out G))
                        {
                            if (client.Entity.GuildID == 0)
                            {
                                if (Guild_Advertise.PassJoinRequirements(client, G))
                                {

                                    G.AddMember(client);
                                }
                            }
                        }
                        break;
                    }
                case Guild_AdvertiseCommends.Recruit:
                    {
                        adv.GuildID = client.Entity.GuildID;
                        byte[] buf = new byte[288 + 8];
                        Writer.WriteUInt16(288, 0, buf);
                        Writer.WriteUInt16(2225, 2, buf);
                        client.Send(adv);
                        client.Send(buf);
                        break;
                    }
                default:
                    {
                        Console.WriteLine("GuildAdvertise: " + adv.Action);
                        break;
                    }
            }
            client.Send(packet);
            client.Send(adv);
        }
    }
}