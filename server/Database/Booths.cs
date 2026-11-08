using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Revora.Network.GamePackets;
using Revora.Game.ConquerStructures;
using Revora.Client;
using Revora.Game;

namespace Revora
{
    public class Booths
    {
        public enum BoothType
        {
            Npc = 0,
            Entity = 1
        }
        public class booth
        {
            public uint UID;
            public ushort Mesh = 100;
            public string Name;
            public ushort Map;
            public ushort X;
            public ushort Y;
            public List<string> Items;
            public BoothType Type;
            public string BotMessage = "Selling Items.[Boothing AI]";
            public uint Garment = 194300;
            public uint Head = 112259;
            public uint WeaponR = 601439;
            public uint WeaponL = 601439;
            public uint Armor = 135259;
        }
        public static SafeDictionary<uint, booth> Boooths = new SafeDictionary<uint, booth>();
        public static void Load()
        {
            string[] text = File.ReadAllLines(Constants.DataHolderPath + "/Booths.txt");
            booth booth = new booth();
            for (int x = 0; x < text.Length; x++)
            {
                string line = text[x];
                string[] split = line.Split('=');
                if (split[0] == "ID")
                {
                    if (booth.UID == 0)
                        booth.UID = uint.Parse(split[1]);
                    else
                    {
                        if (!Boooths.ContainsKey(booth.UID))
                        {
                            Boooths.Add(booth.UID, booth);
                            booth = new booth();
                            booth.UID = uint.Parse(split[1]);
                        }
                    }
                }
                else if (split[0] == "Type")
                {
                    booth.Type = (BoothType)byte.Parse(split[1]);
                }
                else if (split[0] == "Name")
                {
                    booth.Name = split[1];
                }

                else if (split[0] == "BotMessage")
                {
                    booth.BotMessage = split[1];
                }
                else if (split[0] == "Garment")
                {
                    booth.Garment = uint.Parse(split[1]);
                }
                else if (split[0] == "Head")
                {
                    booth.Head = uint.Parse(split[1]);
                }
                else if (split[0] == "WeaponR")
                {
                    booth.WeaponR = uint.Parse(split[1]);
                }
                else if (split[0] == "WeaponL")
                {
                    booth.WeaponL = uint.Parse(split[1]);
                }
                else if (split[0] == "Armor")
                {
                    booth.Armor = uint.Parse(split[1]);
                }


                else if (split[0] == "Mesh")
                {
                    booth.Mesh = ushort.Parse(split[1]);
                }
                else if (split[0] == "Map")
                {
                    booth.Map = ushort.Parse(split[1]);
                }
                else if (split[0] == "X")
                {
                    booth.X = ushort.Parse(split[1]);
                }
                else if (split[0] == "Y")
                {
                    booth.Y = ushort.Parse(split[1]);
                }
                else if (split[0] == "ItemAmount")
                {
                    booth.Items = new List<string>(ushort.Parse(split[1]));
                }
                else if (split[0].Contains("Item") && split[0] != "ItemAmount")
                {
                    string name = split[1];
                    booth.Items.Add(name);
                }
            }
            if (!Boooths.ContainsKey(booth.UID))
                Boooths.Add(booth.UID, booth);
            CreateBooths();
        }
        public static void UpdateCoordonatesForAngle(ref ushort X, ref ushort Y, Enums.ConquerAngle angle)
        {
            sbyte xi = 0, yi = 0;
            switch (angle)
            {
                case Enums.ConquerAngle.North: xi = 1; yi = 1; break;
                case Enums.ConquerAngle.South: xi = -1; yi = -1; break;
                case Enums.ConquerAngle.East: xi = -1; yi = 1; break;
                case Enums.ConquerAngle.West: xi = 1; yi = -1; break;
                case Enums.ConquerAngle.NorthWest: xi = 1; break;
                case Enums.ConquerAngle.SouthWest: yi = -1; break;
                case Enums.ConquerAngle.NorthEast: yi = 1; break;
                case Enums.ConquerAngle.SouthEast: xi = -1; break;
            }
            X = (ushort)(X + xi);
            Y = (ushort)(Y + yi);
        }

        public static void CreateBooths()
        {
            foreach (var bo in Boooths.Values)
            {


                Game.ConquerStructures.Booth booth = new Game.ConquerStructures.Booth();

                SobNpcSpawn Base = new SobNpcSpawn();
                Base.UID = bo.UID;


                if (Booth.Booths2.ContainsKey(Base.UID))
                    Booth.Booths2.Remove(Base.UID);
                Booth.Booths2.Add(Base.UID, booth);

                //if (Booth.Booths2.ContainsKey(Base.UID)) 
                //    Booth.Booths2.Remove(Base.UID); 
                //Booth.Booths2.Add(Base.UID, booth); 
                Base.Mesh = bo.Mesh;
                // Base.Mesh = 400; 
                Base.Type = Game.Enums.NpcType.Booth;
                Base.ShowName = true;
                Base.Name = bo.Name;
                Base.MapID = bo.Map;
                Base.X = bo.X;
                Base.Y = bo.Y;
                booth.Base = Base;

                if (bo.Type == BoothType.Entity)
                {
                    var c = new GameState(null);
                    c.FakeLoad2(bo.UID, bo.Name);


                    #region Equip

                    uint WeaponR = bo.WeaponR;
                    uint WeaponL = bo.WeaponL;
                    uint Armor = bo.Armor;
                    uint Head = bo.Head;
                    uint Garment = bo.Garment;

                    ConquerItem item7 = null;
                    ClientEquip equip = null;
                    if (WeaponR > 0)
                    {
                        Database.ConquerItemBaseInformation CIBI = Database.ConquerItemInformation.BaseInformations[WeaponR];
                        if (CIBI == null) return;
                        item7 = new ConquerItem(true);
                        item7.ID = WeaponR;
                        item7.UID = AuthClient.nextID;
                        AuthClient.nextID++;
                        item7.Position = 4;
                        item7.Durability = CIBI.Durability;
                        item7.MaximDurability = CIBI.Durability;
                        c.Equipment.Remove(4);
                        if (c.Equipment.Objects[3] != null)
                        {
                            c.Equipment.Objects[3] = null;
                        }
                        c.Equipment.Add(item7);
                        item7.Mode = Enums.ItemMode.Update;
                        item7.Send(c);
                        equip = new ClientEquip();
                        equip.DoEquips(c);
                        c.Send(equip);
                        c.Equipment.UpdateEntityPacket();

                    }
                    if (WeaponL > 0)
                    {
                        Database.ConquerItemBaseInformation CIBI = Database.ConquerItemInformation.BaseInformations[WeaponL];
                        if (CIBI == null) return;
                        item7 = new ConquerItem(true);
                        item7.ID = WeaponL;
                        item7.UID = AuthClient.nextID;
                        AuthClient.nextID++;
                        item7.Position = 5;
                        item7.Durability = CIBI.Durability;
                        item7.MaximDurability = CIBI.Durability;
                        c.Equipment.Remove(5);
                        if (c.Equipment.Objects[4] != null)
                        {
                            c.Equipment.Objects[4] = null;
                        }
                        c.Equipment.Add(item7);
                        item7.Mode = Enums.ItemMode.Update;
                        item7.Send(c);
                        equip = new ClientEquip();
                        equip.DoEquips(c);
                        c.Send(equip);
                        c.Equipment.UpdateEntityPacket();
                    }

                    if (Armor > 0)
                    {
                        Database.ConquerItemBaseInformation CIBI = Database.ConquerItemInformation.BaseInformations[Armor];
                        if (CIBI == null) return;
                        item7 = new ConquerItem(true);
                        item7.ID = Armor;
                        item7.UID = AuthClient.nextID;
                        AuthClient.nextID++;
                        item7.Position = 3;
                        item7.Durability = CIBI.Durability;
                        item7.MaximDurability = CIBI.Durability;
                        c.Equipment.Remove(3);
                        if (c.Equipment.Objects[2] != null)
                        {
                            c.Equipment.Objects[2] = null;
                        }
                        c.Equipment.Add(item7);
                        item7.Mode = Enums.ItemMode.Update;
                        item7.Send(c);
                        equip = new ClientEquip();
                        equip.DoEquips(c);
                        c.Send(equip);
                        c.Equipment.UpdateEntityPacket();

                    }

                    if (Head > 0)
                    {
                        Database.ConquerItemBaseInformation CIBI = Database.ConquerItemInformation.BaseInformations[Head];
                        if (CIBI == null) return;
                        item7 = new ConquerItem(true);
                        item7.ID = Head;
                        item7.UID = AuthClient.nextID;
                        AuthClient.nextID++;
                        item7.Position = 1;
                        item7.Durability = CIBI.Durability;
                        item7.MaximDurability = CIBI.Durability;
                        c.Equipment.Remove(1);
                        if (c.Equipment.Objects[0] != null)
                        {
                            c.Equipment.Objects[0] = null;
                        }
                        c.Equipment.Add(item7);
                        item7.Mode = Enums.ItemMode.Update;
                        item7.Send(c);
                        equip = new ClientEquip();
                        equip.DoEquips(c);
                        c.Send(equip);
                        c.Equipment.UpdateEntityPacket();

                    }

                    if (Garment > 0)
                    {
                        Database.ConquerItemBaseInformation CIBI = Database.ConquerItemInformation.BaseInformations[Garment];
                        if (CIBI == null) return;
                        item7 = new ConquerItem(true);
                        item7.ID = Garment;
                        item7.UID = AuthClient.nextID;
                        AuthClient.nextID++;
                        item7.Position = 9;
                        item7.Durability = CIBI.Durability;
                        item7.MaximDurability = CIBI.Durability;
                        c.Equipment.Remove(9);
                        if (c.Equipment.Objects[8] != null)
                        {
                            c.Equipment.Objects[8] = null;
                        }
                        c.Equipment.Add(item7);
                        item7.Mode = Enums.ItemMode.Update;
                        item7.Send(c);
                        equip = new ClientEquip();
                        equip.DoEquips(c);
                        c.Send(equip);
                        c.Equipment.UpdateEntityPacket();
                    }

                    #endregion Equip

                    c.Entity.Facing = (Enums.ConquerAngle)(bo.Mesh % 10);
                    UpdateCoordonatesForAngle(ref bo.X, ref bo.Y, c.Entity.Facing);
                    c.Entity.X = bo.X;
                    c.Entity.Y = bo.Y;
                    c.Entity.MapID = bo.Map;
                    c.Booth = booth;
                    c.Booth.HawkMessage = new Message(bo.BotMessage, Message.HawkMessage);
                    c.Entity.Action = Enums.ConquerAction.Sit;
                    c.Send(new Data(true) { ID = Data.ChangeAction, UID = c.Entity.UID, dwParam = 0 });

                    var data = new Data(true);
                    data.UID = c.Entity.UID;
                    data.dwParam = Base.UID;
                    data.wParam1 = Base.X;
                    data.wParam2 = Base.Y;
                    data.ID = Data.OwnBooth;
                    c.Send(data);
                    Base.Owner = c;
                }
                else
                {
                    if (Kernel.Maps[bo.Map].Npcs.ContainsKey(Base.UID))
                        Kernel.Maps[bo.Map].Npcs.Remove(Base.UID);
                    Kernel.Maps[bo.Map].Npcs.Add(Base.UID, Base);
                }

                for (int i = 0; i < bo.Items.Count; i++)
                {
                    var line = bo.Items[i].Split(new string[] { "@@", "@" }, StringSplitOptions.RemoveEmptyEntries);
                    #region booth
                    Game.ConquerStructures.BoothItem item = new Game.ConquerStructures.BoothItem();

                    item.Item = new ConquerItem(true);
                    item.Item.UID = AuthClient.nextID;
                    AuthClient.nextID++;
                    item.Item.ID = uint.Parse(line[0]);
                    if (line.Length >= 2)
                        item.Cost = uint.Parse(line[1]);
                    if (line.Length >= 3)
                        item.Item.Plus = byte.Parse(line[2]);
                    if (line.Length >= 4)
                        item.Item.Enchant = byte.Parse(line[3]);
                    if (line.Length >= 5)
                        item.Item.Bless = byte.Parse(line[4]);
                    if (line.Length >= 6)
                        item.Item.SocketOne = (Enums.Gem)byte.Parse(line[5]);
                    if (line.Length >= 7)
                        item.Item.SocketTwo = (Enums.Gem)byte.Parse(line[6]);
                    if (line.Length >= 8)
                        item.Item.StackSize = ushort.Parse(line[7]);

                    Database.ConquerItemBaseInformation CIBI = null;
                    CIBI = Database.ConquerItemInformation.BaseInformations[item.Item.ID];
                    if (CIBI == null)
                        break;
                    item.Item.Durability = CIBI.Durability;
                    item.Item.MaximDurability = CIBI.Durability;
                    item.Cost_Type = Game.ConquerStructures.BoothItem.CostType.ConquerPoints;
                    booth.ItemList.Add(item.Item.UID, item);
                    #endregion
                }

            }
            Console.WriteLine("" + Booth.Booths2.Count + " New Booths Loaded!");
        }
    }
}