
using System;
using Revora.Client;
using Revora.Game.Features;

namespace Revora.Network.GamePackets
{
    public unsafe class ArsenalPacket
    {
        public static byte[] GuildArsenal(GameState Client)
        {
            byte[] Buffer = new byte[252];
            Writer.WriteUInt16(244, 0, Buffer);
            Writer.WriteUInt16(2201, 2, Buffer);

            if (Client.Guild == null)
            {
                Writer.WriteString(Constants.ServerKey, (Buffer.Length - 8), Buffer);
                return Buffer;
            }
            #region Default
            Writer.WriteUInt32(Client.Guild.Arsenal.BattlePower, 8, Buffer);//Total Battle Power 
            Writer.WriteUInt32((uint)Client.AsMember.Arsenal_Donation, 12, Buffer);//My Donation 
            Writer.WriteUInt32(Client.Entity.GuildSharedBp, 16, Buffer);//Shared Battle Power 
            Writer.WriteByte(8, 20, Buffer);
            if (Client.Guild.Arsenal.Arsenals.ContainsKey(Arsenal_ID.Headgear))
            {
                Writer.WriteByte(Client.Guild.Arsenal.Arsenals[Arsenal_ID.Headgear].Potency, 28, Buffer);//Potency 
                Writer.WriteUInt32(Client.Guild.Arsenal.Arsenals[Arsenal_ID.Headgear].Enhancement, 32, Buffer);//Enchantment 
                Writer.WriteUInt64(Client.Guild.Arsenal.Arsenals[Arsenal_ID.Headgear].Donation, 36, Buffer);//Donation 
                Writer.WriteByte(1, 44, Buffer);//Allowed 
            }
            Writer.WriteByte(1, 48, Buffer);
            if (Client.Guild.Arsenal.Arsenals.ContainsKey(Arsenal_ID.Armor))
            {
                Writer.WriteByte(Client.Guild.Arsenal.Arsenals[Arsenal_ID.Armor].Potency, 52, Buffer);//Potency 
                Writer.WriteUInt32(Client.Guild.Arsenal.Arsenals[Arsenal_ID.Armor].Enhancement, 56, Buffer);//Enchantment 
                Writer.WriteUInt64(Client.Guild.Arsenal.Arsenals[Arsenal_ID.Armor].Donation, 60, Buffer);//Donation 
                Writer.WriteByte(1, 68, Buffer);//Allowed 
            }
            Writer.WriteByte(2, 72, Buffer);
            if (Client.Guild.Arsenal.Arsenals.ContainsKey(Arsenal_ID.Weapon))
            {
                Writer.WriteByte(Client.Guild.Arsenal.Arsenals[Arsenal_ID.Weapon].Potency, 76, Buffer);//Potency 
                Writer.WriteUInt32(Client.Guild.Arsenal.Arsenals[Arsenal_ID.Weapon].Enhancement, 80, Buffer);//Enchantment 
                Writer.WriteUInt64(Client.Guild.Arsenal.Arsenals[Arsenal_ID.Weapon].Donation, 84, Buffer);//Donation 
                Writer.WriteByte(1, 92, Buffer);//Allowed 
            }
            Writer.WriteByte(3, 96, Buffer);
            if (Client.Guild.Arsenal.Arsenals.ContainsKey(Arsenal_ID.Ring))
            {
                Writer.WriteByte((byte)Client.Guild.Arsenal.Arsenals[Arsenal_ID.Ring].Potency, 100, Buffer);//Potency 
                Writer.WriteUInt32(Client.Guild.Arsenal.Arsenals[Arsenal_ID.Ring].Enhancement, 104, Buffer);//Enchantment 
                Writer.WriteUInt64(Client.Guild.Arsenal.Arsenals[Arsenal_ID.Ring].Donation, 108, Buffer);//Donation 
                Writer.WriteByte(1, 116, Buffer);//Allowed 
            }
            Writer.WriteByte(4, 120, Buffer);

            if (Client.Guild.Arsenal.Arsenals.ContainsKey(Arsenal_ID.Boots))
            {
                Writer.WriteByte(Client.Guild.Arsenal.Arsenals[Arsenal_ID.Boots].Potency, 124, Buffer);//Potency 
                Writer.WriteUInt32(Client.Guild.Arsenal.Arsenals[Arsenal_ID.Boots].Enhancement, 128, Buffer);//Enchantment 
                Writer.WriteUInt64(Client.Guild.Arsenal.Arsenals[Arsenal_ID.Boots].Donation, 132, Buffer);//Donation 
                Writer.WriteByte(1, 140, Buffer);//Allowed 
            }
            Writer.WriteByte(5, 144, Buffer);
            if (Client.Guild.Arsenal.Arsenals.ContainsKey(Arsenal_ID.Necklace))
            {
                Writer.WriteByte(Client.Guild.Arsenal.Arsenals[Arsenal_ID.Necklace].Potency, 148, Buffer);//Potency 
                Writer.WriteUInt32(Client.Guild.Arsenal.Arsenals[Arsenal_ID.Necklace].Enhancement, 152, Buffer);//Enchantment 
                Writer.WriteUInt64(Client.Guild.Arsenal.Arsenals[Arsenal_ID.Necklace].Donation, 156, Buffer);//Donation 
                Writer.WriteByte(1, 164, Buffer);//Allowed 
            }
            Writer.WriteByte(6, 168, Buffer);
            if (Client.Guild.Arsenal.Arsenals.ContainsKey(Arsenal_ID.Fan))
            {
                Writer.WriteByte(Client.Guild.Arsenal.Arsenals[Arsenal_ID.Fan].Potency, 172, Buffer);//Potency 
                Writer.WriteUInt32(Client.Guild.Arsenal.Arsenals[Arsenal_ID.Fan].Enhancement, 176, Buffer);//Enchantment 
                Writer.WriteUInt64(Client.Guild.Arsenal.Arsenals[Arsenal_ID.Fan].Donation, 180, Buffer);//Donation 
                Writer.WriteByte(1, 188, Buffer);//Allowed 
            }
            Writer.WriteByte(7, 192, Buffer);
            if (Client.Guild.Arsenal.Arsenals.ContainsKey(Arsenal_ID.Tower))
            {
                Writer.WriteByte(Client.Guild.Arsenal.Arsenals[Arsenal_ID.Tower].Potency, 196, Buffer);//Potency 
                Writer.WriteUInt32(Client.Guild.Arsenal.Arsenals[Arsenal_ID.Tower].Enhancement, 200, Buffer);//Enchantment 
                Writer.WriteUInt64(Client.Guild.Arsenal.Arsenals[Arsenal_ID.Tower].Donation, 204, Buffer);//Donation 
                Writer.WriteByte(1, 212, Buffer);//Allowed 
            }
            #endregion
            Writer.WriteString(Constants.ServerKey, (Buffer.Length - 8), Buffer);
            return Buffer;
        }
        public static void ArsenalPackets(byte[] packet, GameState client)
        {
            if(packet==null || packet.Length<28 || packet.Length>128 || BitConverter.ToUInt16(packet,0)!=packet.Length-8
                || BitConverter.ToUInt16(packet,2)!=PacketID.ArsenalPackets || BitConverter.ToUInt64(packet,packet.Length-8)!=Revora.Network.PacketHandler.ClientSeal
                || !QuestInventory.Player(client,false) || client.Guild==null || client.AsMember==null || packet[8]>8)return;
            client.Send(ArsenalPacket.GuildArsenal(client));
            byte pType = packet[4];
            byte i_type = packet[8];
            uint i_Uid = BitConverter.ToUInt32(packet, 12);
            if (i_type == 0) i_type = 8;
            switch (pType)
            {
                case 0:
                    {
                        client.Guild.Arsenal.UnlockArsenal(client, (Arsenal_ID)i_type);
                        break;
                    }
                case 1:
                    {
                        ConquerItem Item = null;
                        if (client.Inventory.TryGetItem(i_Uid, out Item) || client.Equipment.TryGetItem(i_Uid, out Item))
                            client.Guild.Arsenal.InscribeItem(client, Item, (Arsenal_ID)i_type);
                        break;
                    }
                case 2:
                    {
                        GuildArsenalInscriptions.Change(client, client.Guild.Arsenal, i_Uid, (Arsenal_ID)i_type, true);
                        break;
                    }
                case 3:
                    {
                        client.Guild.Arsenal.Enchant(client, i_type, BitConverter.ToUInt32(packet,16));
                        return;
                    }
                case 4:
                    {
                        client.Send(ArsenalPacket.GuildArsenal(client));
                        break;
                    }
            }
        }  
        public static void FastInscribeItem(byte[] packet, GameState client)
        {
            if (QuestInventory.Player(client, false) && client.Guild != null)
                client.Guild.Arsenal.FastInscribeItem(packet, client, Arsenal_ID.None);
        }
        public static byte[] ArsenalInscribedPage(GameState Client, byte[] Data, Arsenal_ID ID)
        {
            byte[] Buffer = null;
            Arsenal_Client[] Inscribed = null;
            uint Page = (byte)(BitConverter.ToUInt32(Data, 12) / 5);
        Again:
            try
            {
                Inscribed = new Arsenal_Client[Client.Guild.Arsenal.Arsenals[ID].Inscribed.Count];
                Client.Guild.Arsenal.Arsenals[ID].Inscribed.Values.CopyTo(Inscribed, 0);
            }
            catch 
            { 
                goto Again;
            }
            int Position = 44;
            int count = Inscribed.Length;
            if (count > 8) count = 8;
            Buffer = new byte[84 + (count * 40) + 8];
            for (int i = 0; i < 20; i++)
                Buffer[i] = Data[i];

            Writer.WriteUInt16((ushort)(Buffer.Length - 8), 0, Buffer);
            Writer.WriteUInt32(0, 16, Buffer);
            Writer.WriteUInt32((uint)Inscribed.Length, 20, Buffer);
            if (Client.Guild.Arsenal.Arsenals.ContainsKey(Arsenal_ID.Headgear))
            {
                Writer.WriteByte(Client.Guild.Arsenal.Arsenals[Arsenal_ID.Headgear].Potency, 24, Buffer);//Potency
            }
            if (Client.Guild.Arsenal.Arsenals.ContainsKey(Arsenal_ID.Armor))
            {
                Writer.WriteByte(Client.Guild.Arsenal.Arsenals[Arsenal_ID.Armor].Potency, 24, Buffer);//Potency
            }
            if (Client.Guild.Arsenal.Arsenals.ContainsKey(Arsenal_ID.Boots))
            {
                Writer.WriteByte(Client.Guild.Arsenal.Arsenals[Arsenal_ID.Boots].Potency, 24, Buffer);//Potency
            }
            if (Client.Guild.Arsenal.Arsenals.ContainsKey(Arsenal_ID.Fan))
            {
                Writer.WriteByte(Client.Guild.Arsenal.Arsenals[Arsenal_ID.Fan].Potency, 24, Buffer);//Potency
            }
            if (Client.Guild.Arsenal.Arsenals.ContainsKey(Arsenal_ID.Necklace))
            {
                Writer.WriteByte(Client.Guild.Arsenal.Arsenals[Arsenal_ID.Necklace].Potency, 24, Buffer);//Potency
            }
            if (Client.Guild.Arsenal.Arsenals.ContainsKey(Arsenal_ID.Ring))
            {
                Writer.WriteByte(Client.Guild.Arsenal.Arsenals[Arsenal_ID.Ring].Potency, 24, Buffer);//Potency
            }
            if (Client.Guild.Arsenal.Arsenals.ContainsKey(Arsenal_ID.Tower))
            {
                Writer.WriteByte(Client.Guild.Arsenal.Arsenals[Arsenal_ID.Tower].Potency, 24, Buffer);//Potency
            }
            if (Client.Guild.Arsenal.Arsenals.ContainsKey(Arsenal_ID.Weapon))
            {
                Writer.WriteByte(Client.Guild.Arsenal.Arsenals[Arsenal_ID.Weapon].Potency, 24, Buffer);//Potency
            }
            Writer.WriteUInt32((uint)Client.Guild.Arsenal.Arsenals[ID].Donation, 36, Buffer);
            Writer.WriteUInt32((uint)Inscribed.Length, 40, Buffer);
            for (uint i = BitConverter.ToUInt32(Data, 8); i <= BitConverter.ToUInt32(Data, 12); i++)
            {
                if (i - 1 >= Inscribed.Length) break;
                Arsenal_Client ac = Inscribed[i - 1];
                ConquerItem Item = ac.Item;
                Writer.WriteUInt32(Item.UID, Position, Buffer); Position += 4;
                Writer.WriteUInt32((uint)(i), Position, Buffer); Position += 4;
                Writer.WriteString(ac.Name, Position, Buffer); Position += 16;
                Writer.WriteUInt32(Item.ID, Position, Buffer); Position += 4;
                Writer.WriteByte((byte)(Item.ID % 10), Position, Buffer); Position++;
                Writer.WriteByte(Item.Plus, Position, Buffer); Position++;
                Writer.WriteByte((byte)Item.SocketOne, Position, Buffer); Position++;
                Writer.WriteByte((byte)Item.SocketTwo, Position, Buffer); Position++;
                Writer.WriteByte((byte)Item.BattlePower, Position, Buffer); Position += 4;
                Writer.WriteUInt32((uint)ac.donation, Position, Buffer); Position += 4;
            }
            Writer.WriteString(Constants.ServerKey, (Buffer.Length - 8), Buffer);
            return Buffer;
        }
    }
}