using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Revora.Game.ConquerStructures.Society;
using Revora.Interfaces;
using Revora.Client;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    public unsafe class Arsenal_State
    {
        public Arsenal_ID ID;
        public bool Unlocked;
        public uint Enhancement;
        public uint ars_bp = 0;
        public DateTime EnhancementExpDate;
        public ulong Donation
        {
            get
            {
                ulong Cal = 0;
                lock (Inscribed)
                    foreach (var insc in Inscribed)
                        Cal += insc.Value.donation;
                return Cal;
            }
        }
        public byte Potency;
        public Dictionary<uint, Arsenal_Client> Inscribed;
        public Arsenal_State()
        {
            Inscribed = new Dictionary<uint, Arsenal_Client>();
        }
    }
    public unsafe class Arsenal_Client
    {
        public ConquerItem Item;
        public string Name;
        public uint UID;
        public uint iUID;
        public uint donation;
    }
    public enum Arsenal_ID : byte
    {
        None = 0,
        Headgear = 8,
        Armor = 1,
        Weapon = 2,
        Ring = 3,
        Boots = 4,
        Necklace = 5,
        Fan = 6,
        Tower = 7,
    }
    public unsafe class Arsenal
    {
        ushort gOwner = 0;
        public Dictionary<Arsenal_ID, Arsenal_State> Arsenals = null;
        public byte BattlePower;
        internal long Version=1;
        public Arsenal(ushort g_owner)
        {
            gOwner = g_owner;
            Arsenals = new Dictionary<Arsenal_ID, Arsenal_State>();
        }
        public class MetoEditionars
        {
            public static void enchant(uint id)
            {
            }
            public static int ArsenalPosition(uint ID)
            {
                ushort pos = ItemHandler.ItemPosition(ID); switch (pos)
                {
                    case ConquerItem.Head: return 8;
                    case ConquerItem.Armor: return 1;
                    case ConquerItem.LeftWeapon:
                    case ConquerItem.RightWeapon: return 2;
                    case ConquerItem.Ring: return 3;
                    case ConquerItem.Boots: return 4;
                    case ConquerItem.Necklace: return 5;
                    case ConquerItem.Fan: return 6;
                    case ConquerItem.Tower: return 7;
                    default: return -1;
                }
            }
        }
        public void FastInscribeItem(byte[] packet, GameState Client, Arsenal_ID ID)
        {
            if (packet == null || packet.Length < 13 || packet[4] == 0 || packet[4] > 40
                || packet.Length != 13 + packet[4] * 4 || BitConverter.ToUInt16(packet, 0) != packet.Length - 8
                || BitConverter.ToUInt16(packet, 2) != PacketID.FastInscribeItem
                || BitConverter.ToUInt64(packet, packet.Length - 8) != Revora.Network.PacketHandler.ClientSeal
                || !QuestInventory.Player(Client, false)) return;
            var seen = new HashSet<uint>();
            for (int i = 0; i < packet[4]; i++)
            {
                uint uid = BitConverter.ReadUint(packet, 5 + i * 4);
                if (!seen.Add(uid)) continue;
                ConquerItem item;
                if (Client.Inventory.TryGetItem(uid, out item) || Client.Equipment.TryGetItem(uid, out item))
                    GuildArsenalInscriptions.Change(Client, this, uid, (Arsenal_ID)MetoEditionars.ArsenalPosition(item.ID), false);
            }
        }
        public void InscribeItem(GameState Client, ConquerItem Item, Arsenal_ID ID)
        {
            if (Item != null) GuildArsenalInscriptions.Change(Client, this, Item.UID, ID, false);
        }
        public void RemoveItem(GameState Client, ConquerItem Item, Arsenal_ID ID)
        {
            if (Item != null) GuildArsenalInscriptions.Change(Client, this, Item.UID, ID, true);
        }
        public void Enchant(GameState Client, uint ID,uint level)
        {
            if(ID>8)return;
            GuildArsenalPurchases.Enhance(Client,this,(Arsenal_ID)ID,level);
        }
        public void getbpEnchant(GameState Client, uint a_bp, Arsenal_ID ID)
        {
            GuildArsenalPurchases.Rebuild(Client.Guild);
        }
        public void UnlockArsenal(GameState Client, Arsenal_ID ID)
        {
            GuildArsenalPurchases.Unlock(Client,this,ID);
        }
        public uint getUnlockValue()
        {
            switch (Arsenals.Count)
            {
                case 0:
                case 1: return 5000000;
                case 2:
                case 3:
                case 4: return 10000000;
                case 5:
                case 6: return 15000000;
                case 7: return 20000000;
            }
            return 0;
        }
        public uint GetDonation(ConquerItem Item)
        {
            uint Return = 0;
            int id = (int)(Item.ID % 10);
            switch (id)
            {
                case 8: Return = 1000; break;
                case 9: Return = 16660; break;
            }
            if (Item.SocketOne > 0 && Item.SocketTwo == 0)
                Return += 33330;
            if (Item.SocketOne > 0 && Item.SocketTwo > 0)
                Return += 133330;
            switch (Item.Plus)
            {
                case 1: Return += 90; break;
                case 2: Return += 490; break;
                case 3: Return += 1350; break;
                case 4: Return += 4070; break;
                case 5: Return += 12340; break;
                case 6: Return += 37030; break;
                case 7: Return += 111110; break;
                case 8: Return += 333330; break;
                case 9: Return += 1000000; break;
                case 10: Return += 1033330; break;
                case 11: Return += 1101230; break;
                case 12: Return += 1212340; break;
                default: break;
            }
            return Return;
        }
    }
    public unsafe class ArsenalSystem
    {
        private IDisposable Subscriber;
        public ArsenalSystem()
        {
            Subscriber = Thread.Subscribe(work, 1000);
        }
        public void work(int time)
        {
            #region ArsenalSystem
            foreach (var Guilds in Kernel.Guilds)
            {
                var guildGate = GuildLeadership.Gate(Guilds.Value);
                if (!System.Threading.Monitor.TryEnter(guildGate)) continue;
                try
                {
                    Database.GuildTable.RebuildLevel(Guilds.Value);
                    GuildArsenalPurchases.Refresh(Guilds.Value);
                }
                catch (Exception e)
                {
                    Server.SaveException(e);
                }
                finally { System.Threading.Monitor.Exit(guildGate); }
            }
            foreach (var client in Kernel.GamePool.Values)
            {
                if (!QuestInventory.Player(client, false) || !System.Threading.Monitor.TryEnter(client.ItemSyncRoot)) continue;
                object memberGate = null;
                try
                {
                    if (!QuestInventory.Player(client, false)) continue;
                    if (client.Guild != null)
                    {
                        memberGate = GuildLeadership.Gate(client.Guild);
                        if (!System.Threading.Monitor.TryEnter(memberGate)) { memberGate = null; continue; }
                    }
                #region Rest Arsenal Doantion & BattlePowerGuild Shared with null
                if (client.Guild == null)
                {
                    if (client.Entity.GuildSharedBp != 0)
                    {
                        client.Entity.GuildSharedBp = 0;
                    }
                    if (client.AsMember != null)
                    {
                        if (client.AsMember.Arsenal_Donation != 0)
                        {
                            client.AsMember.Arsenal_Donation = 0;
                            Database.EntityTable.UpdateArsenalDonation(client);
                        }
                    }
                    #region AutoRemove
                    if (client.Guild != null)
                    {
                        foreach (var Equip in client.Equipment.Objects)
                        {
                            if (Equip != null)
                                if (Equip.Inscribed)
                                {
                                    Equip.Inscribed = false;
                                    Equip.Mode = Revora.Game.Enums.ItemMode.Update;
                                    Equip.Send(client);
                                    Equip.Mode = Revora.Game.Enums.ItemMode.Default;
                                    Database.ConquerItemTable.UpdateInscre2(Equip);
                                    Database.ArsenalTable.DeleteArsenalItem(client.Entity.UID, Equip.UID, Arsenal_ID.None);
                                }
                        }
                        foreach (var Equip in client.Inventory.Objects)
                        {
                            if (Equip != null)
                                if (Equip.Inscribed)
                                {
                                    Equip.Inscribed = false;
                                    Equip.Mode = Revora.Game.Enums.ItemMode.Update;
                                    Equip.Send(client);
                                    Equip.Mode = Revora.Game.Enums.ItemMode.Default;
                                    Database.ConquerItemTable.UpdateInscre2(Equip);
                                    Database.ArsenalTable.DeleteArsenalItem(client.Entity.UID, Equip.UID, Arsenal_ID.None);
                                }
                        }
                        foreach (var Ware in client.Warehouses.Values)
                        {
                            foreach (var Equip in Ware.Objects)
                            {
                                if (Equip != null)
                                    if (Equip.Inscribed)
                                    {
                                        Equip.Inscribed = false;
                                        Equip.Mode = Revora.Game.Enums.ItemMode.Update;
                                        Equip.Send(client);
                                        Equip.Mode = Revora.Game.Enums.ItemMode.Default;
                                        Database.ConquerItemTable.UpdateInscre2(Equip);
                                        Database.ArsenalTable.DeleteArsenalItem(client.Entity.UID, Equip.UID, Arsenal_ID.None);
                                    }
                            }
                        }
                    }
                    #endregion
                }
                #endregion
                #region AutoRemove
                if (client.Guild != null)
                {
                    foreach (var Equip in client.Equipment.Objects)
                    {
                        if (Equip != null)
                            if (Equip.Inscribed)
                            {
                                bool Entered = false;
                                foreach (var Arsenals in client.Guild.Arsenal.Arsenals)
                                {
                                    if (Arsenals.Value.Inscribed.ContainsKey(Equip.UID))
                                    {
                                        Entered = true;
                                    }
                                }
                                if (!Entered)
                                {
                                    Equip.Inscribed = false;
                                    Equip.Mode = Revora.Game.Enums.ItemMode.Update;
                                    Equip.Send(client);
                                    Equip.Mode = Revora.Game.Enums.ItemMode.Default;
                                    Database.ConquerItemTable.UpdateInscre2(Equip);
                                    Database.ArsenalTable.DeleteArsenalItem(client.Entity.UID, Equip.UID, Arsenal_ID.None);
                                }
                            }
                            else
                            {
                                bool Entered = false;
                                Arsenal_ID Ar = Arsenal_ID.None;
                                foreach (var Arsenals in client.Guild.Arsenal.Arsenals)
                                {
                                    if (Arsenals.Value.Inscribed.ContainsKey(Equip.UID))
                                    {
                                        Entered = true;
                                        Ar = Arsenals.Key;
                                    }
                                }
                                if (Entered)
                                {
                                    Equip.Inscribed = true;
                                    Equip.Mode = Revora.Game.Enums.ItemMode.Update;
                                    Equip.Send(client);
                                    Equip.Mode = Revora.Game.Enums.ItemMode.Default;
                                    Database.ConquerItemTable.UpdateInscre1(Equip);
                                }
                            }
                    }
                    foreach (var Equip in client.Inventory.Objects)
                    {
                        if (Equip != null)
                            if (Equip.Inscribed)
                            {
                                bool Entered = false;
                                foreach (var Arsenals in client.Guild.Arsenal.Arsenals)
                                {
                                    if (Arsenals.Value.Inscribed.ContainsKey(Equip.UID))
                                    {
                                        Entered = true;
                                    }
                                }
                                if (!Entered)
                                {
                                    Equip.Inscribed = false;
                                    Equip.Mode = Revora.Game.Enums.ItemMode.Update;
                                    Equip.Send(client);
                                    Equip.Mode = Revora.Game.Enums.ItemMode.Default;
                                    Database.ConquerItemTable.UpdateInscre2(Equip);
                                    Database.ArsenalTable.DeleteArsenalItem(client.Entity.UID, Equip.UID, Arsenal_ID.None);
                                }
                            }
                            else
                            {
                                bool Entered = false;
                                Arsenal_ID Ar = Arsenal_ID.None;
                                foreach (var Arsenals in client.Guild.Arsenal.Arsenals)
                                {
                                    if (Arsenals.Value.Inscribed.ContainsKey(Equip.UID))
                                    {
                                        Entered = true;
                                        Ar = Arsenals.Key;
                                    }
                                }
                                if (Entered)
                                {
                                    Equip.Inscribed = true;
                                    Equip.Mode = Revora.Game.Enums.ItemMode.Update;
                                    Equip.Send(client);
                                    Equip.Mode = Revora.Game.Enums.ItemMode.Default;
                                    Database.ConquerItemTable.UpdateInscre1(Equip);
                                }
                            }
                    }
                    foreach (var Ware in client.Warehouses.Values)
                    {
                        foreach (var Equip in Ware.Objects)
                        {
                            if (Equip != null)
                                if (Equip.Inscribed)
                                {
                                    bool Entered = false;
                                    foreach (var Arsenals in client.Guild.Arsenal.Arsenals)
                                    {
                                        if (Arsenals.Value.Inscribed.ContainsKey(Equip.UID))
                                        {
                                            Entered = true;
                                        }
                                    }
                                    if (!Entered)
                                    {
                                        Equip.Inscribed = false;
                                        Equip.Mode = Revora.Game.Enums.ItemMode.Update;
                                        Equip.Send(client);
                                        Equip.Mode = Revora.Game.Enums.ItemMode.Default;
                                        Database.ConquerItemTable.UpdateInscre2(Equip);
                                        Database.ArsenalTable.DeleteArsenalItem(client.Entity.UID, Equip.UID, Arsenal_ID.None);
                                    }
                                }
                                else
                                {
                                    bool Entered = false;
                                    Arsenal_ID Ar = Arsenal_ID.None;
                                    foreach (var Arsenals in client.Guild.Arsenal.Arsenals)
                                    {
                                        if (Arsenals.Value.Inscribed.ContainsKey(Equip.UID))
                                        {
                                            Entered = true;
                                            Ar = Arsenals.Key;
                                        }
                                    }
                                    if (Entered)
                                    {
                                        Equip.Inscribed = true;
                                        Equip.Mode = Revora.Game.Enums.ItemMode.Update;
                                        Equip.Send(client);
                                        Equip.Mode = Revora.Game.Enums.ItemMode.Default;
                                        Database.ConquerItemTable.UpdateInscre1(Equip);
                                    }
                                }
                        }
                    }
                }
                #endregion
                if (client.Guild != null)
                {
                    GuildArsenalPurchases.Publish(client);
                }
                }
                finally
                {
                    if (memberGate != null) System.Threading.Monitor.Exit(memberGate);
                    System.Threading.Monitor.Exit(client.ItemSyncRoot);
                }
            }
            #endregion
        }
    }
}