using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Revora.Network.GamePackets;
using Revora.Interfaces;

namespace Revora.Game
{
    class EventoGMQuest
    {
        //public static ushort X = 148, Y = 51;
        //public static ushort X1 = 148, Y1 = 61;
        //public static ushort X2 = 148, Y2 = 71;
        //public static ushort X3 = 148, Y3 = 81;
        //public static ushort X4 = 148, Y4 = 91;
        //public static ushort X5 = 148, Y5 = 101;
        //public static ushort X6 = 148, Y6 = 111;
        //public static ushort X7 = 148, Y7 = 121;
        //public static ushort X8 = 148, Y8 = 131;
        //public static ushort X9 = 148, Y9 = 141;
        //public static ushort X10 = 148, Y10 = 151;
        //public static ushort X11 = 148, Y11 = 161;
        //public static ushort X12 = 148, Y12 = 171;
        //public static ushort X13 = 148, Y13 = 189;
        //public static ushort X14 = 151, Y14 = 221;
        //public static ushort X15 = 157, Y15 = 222;
        //public static ushort X16 = 161, Y16 = 259;
        public static ushort X = 301, Y = 266;
        public static ushort X1 = 300, Y1 = 275;
        public static ushort X2 = 301, Y2 = 284;
        public static ushort X3 = 313, Y3 = 292;
        public static ushort X4 = 329, Y4 = 290;
        public static ushort X5 = 326, Y5 = 279;
        public static ushort X6 = 324, Y6 = 264;
        public static ushort X7 = 345, Y7 = 253;
        public static ushort X8 = 358, Y8 = 253;
        public static ushort X9 = 382, Y9 = 253;
        public static ushort X10 = 420, Y10 = 245;
        public static ushort X11 = 399, Y11 = 231;
        public static ushort X12 = 381, Y12 = 218;
        public static ushort X13 = 357, Y13 = 210;
        public static ushort X14 = 344, Y14 = 229;
        public static ushort X15 = 325, Y15 = 236;
        public static ushort X16 = 301, Y16 = 266;
        public static void Load()
        {
            if (Kernel.Maps.ContainsKey(1002))
            {

                #region CPBag
                uint ItemID = 0;
                byte type1 = 3;
                for (int i = 0; i < 3; i++)
                {
                    type1 = (byte)Kernel.Random.Next(1, 1);
                    switch (type1)
                    {
                        case 1:
                            ItemID = 720159;// algunas ID usted puede cambiarlas
                            break;
                    }
                    //uint ItemID = 720159;
                    INpc npc = new Network.GamePackets.NpcSpawn();
                    npc.UID = 1305;
                    npc.Mesh = 13050;
                    npc.Type = Enums.NpcType.Talker;
                    npc.X = (ushort)(X - 1);
                    npc.Y = (ushort)(Y - 1);
                    npc.MapID = 1002;

                    Game.Map Map = Kernel.Maps[1002];
                    if (Map.SelectCoordonates(ref X, ref Y))
                    {
                        Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                        floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.BoundCps;
                        //floorItem.Value = 5000;
                        floorItem.Value = 5000;
                        floorItem.ItemID = ItemID;
                        floorItem.MapID = 1002;
                        floorItem.MapObjType = Game.MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Y = Y;
                        floorItem.Type = Network.GamePackets.FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                        {
                            floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        }
                        Map.AddFloorItem(floorItem);
                        foreach (Client.GameState C in Kernel.GamePool.Values)
                        {
                            if (C.Entity.MapID == 1002)
                            {
                                C.SendScreenSpawn(floorItem, true);
                                npc.SendSpawn(C);
                            }
                        }
                    }
                }


                #endregion
            }
        }
        public static void Load2()
        {
            if (Kernel.Maps.ContainsKey(1002))
            {
                #region CPBag

                uint ItemID = 0;
                byte type1 = 3;
                for (int i = 0; i < 3; i++)
                {
                    type1 = (byte)Kernel.Random.Next(1, 1);
                    switch (type1)
                    {
                        case 1:
                            ItemID = 720159;// algunas ID usted puede cambiarlas
                            break;
                    }
                    ushort X = X1, Y = Y1;
                    INpc npc = new Network.GamePackets.NpcSpawn();
                    npc.UID = 1305;
                    npc.Mesh = 13050;
                    npc.Type = Enums.NpcType.Talker;
                    npc.X = (ushort)(X - 1);
                    npc.Y = (ushort)(Y - 1);
                    npc.MapID = 1002;

                    Game.Map Map = Kernel.Maps[1002];
                    if (Map.SelectCoordonates(ref X, ref Y))
                    {
                        Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                        floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.BoundCps;
                        //floorItem.Value = 5000;
                        floorItem.Value = 5000;
                        floorItem.ItemID = ItemID;
                        floorItem.MapID = 1002;
                        floorItem.MapObjType = Game.MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Y = Y;
                        floorItem.Type = Network.GamePackets.FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        foreach (Client.GameState C in Kernel.GamePool.Values)
                        {
                            if (C.Entity.MapID == 1002)
                            {
                                C.SendScreenSpawn(floorItem, true);
                                npc.SendSpawn(C);

                            }
                        }
                    }
                }

                #endregion


            }
        }
        public static void Load3()
        {
            if (Kernel.Maps.ContainsKey(1002))
            {
                #region CPBag

                uint ItemID = 0;
                byte type1 = 3;
                for (int i = 0; i < 3; i++)
                {
                    type1 = (byte)Kernel.Random.Next(1, 1);
                    switch (type1)
                    {
                        case 1:
                            ItemID = 720159;// 
                            break;
                    }
                    ushort X = X2, Y = Y2;
                    INpc npc = new Network.GamePackets.NpcSpawn();
                    npc.UID = 1305;
                    npc.Mesh = 13050;
                    npc.Type = Enums.NpcType.Talker;
                    npc.X = (ushort)(X - 1);
                    npc.Y = (ushort)(Y - 1);
                    npc.MapID = 1002;


                    Game.Map Map = Kernel.Maps[1002];
                    if (Map.SelectCoordonates(ref X, ref Y))
                    {
                        Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                        floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.BoundCps;
                        floorItem.Value = 5000;
                        floorItem.ItemID = ItemID;
                        floorItem.MapID = 1002;
                        floorItem.MapObjType = Game.MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Y = Y;
                        floorItem.Type = Network.GamePackets.FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        foreach (Client.GameState C in Kernel.GamePool.Values)
                        {
                            if (C.Entity.MapID == 1002)
                            {
                                C.SendScreenSpawn(floorItem, true);
                                npc.SendSpawn(C);
                            }
                        }

                    }
                }
                #endregion
            }
        }
        public static void Load4()
        {
            if (Kernel.Maps.ContainsKey(1002))
            {
                #region CPBag

                uint ItemID = 0;
                byte type1 = 3;
                for (int i = 0; i < 3; i++)
                {
                    type1 = (byte)Kernel.Random.Next(1, 1);
                    switch (type1)
                    {
                        case 1:
                            ItemID = 720159;// algunas ID usted puede cambiarlas
                            break;
                    }
                    ushort X = X3, Y = Y3;
                    INpc npc = new Network.GamePackets.NpcSpawn();
                    npc.UID = 1305;
                    npc.Mesh = 13050;
                    npc.Type = Enums.NpcType.Talker;
                    npc.X = (ushort)(X - 1);
                    npc.Y = (ushort)(Y - 1);
                    npc.MapID = 1002;

                    Game.Map Map = Kernel.Maps[1002];
                    if (Map.SelectCoordonates(ref X, ref Y))
                    {
                        Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                        floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.BoundCps;
                        floorItem.Value = 5000;
                        floorItem.ItemID = ItemID;
                        floorItem.MapID = 1002;
                        floorItem.MapObjType = Game.MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Y = Y;
                        floorItem.Type = Network.GamePackets.FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        foreach (Client.GameState C in Kernel.GamePool.Values)
                        {
                            if (C.Entity.MapID == 1002)
                            {
                                C.SendScreenSpawn(floorItem, true);
                                npc.SendSpawn(C);
                            }
                        }
                    }
                }
                #endregion


            }
        }
        public static void Load5()
        {
            if (Kernel.Maps.ContainsKey(1002))
            {
                #region CPBag

                uint ItemID = 0;
                byte type1 = 3;
                for (int i = 0; i < 3; i++)
                {
                    type1 = (byte)Kernel.Random.Next(1, 1);
                    switch (type1)
                    {
                        case 1:
                            ItemID = 720159;// algunas ID usted puede cambiarlas
                            break;
                    }
                    ushort X = X4, Y = Y4;
                    INpc npc = new Network.GamePackets.NpcSpawn();
                    npc.UID = 1305;
                    npc.Mesh = 13050;
                    npc.Type = Enums.NpcType.Talker;
                    npc.X = (ushort)(X - 1);
                    npc.Y = (ushort)(Y - 1);
                    npc.MapID = 1002;

                    Game.Map Map = Kernel.Maps[1002];
                    if (Map.SelectCoordonates(ref X, ref Y))
                    {
                        Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                        floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.BoundCps;
                        floorItem.Value = 5000;
                        floorItem.ItemID = ItemID;
                        floorItem.MapID = 1002;
                        floorItem.MapObjType = Game.MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Y = Y;
                        floorItem.Type = Network.GamePackets.FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        foreach (Client.GameState C in Kernel.GamePool.Values)
                        {
                            if (C.Entity.MapID == 1002)
                            {
                                C.SendScreenSpawn(floorItem, true);
                                npc.SendSpawn(C);
                            }
                        }
                    }
                }
                #endregion

            }
        }
        public static void Load6()
        {
            if (Kernel.Maps.ContainsKey(1002))
            {
                #region CPBag

                uint ItemID = 0;
                byte type1 = 3;
                for (int i = 0; i < 3; i++)
                {
                    type1 = (byte)Kernel.Random.Next(1, 1);
                    switch (type1)
                    {
                        case 1:
                            ItemID = 720159;// algunas ID usted puede cambiarlas
                            break;
                    }
                    ushort X = X5, Y = Y5;
                    INpc npc = new Network.GamePackets.NpcSpawn();
                    npc.UID = 1305;
                    npc.Mesh = 13050;
                    npc.Type = Enums.NpcType.Talker;
                    npc.X = (ushort)(X - 1);
                    npc.Y = (ushort)(Y - 1);
                    npc.MapID = 1002;

                    Game.Map Map = Kernel.Maps[1002];
                    if (Map.SelectCoordonates(ref X, ref Y))
                    {
                        Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                        floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.BoundCps;
                        floorItem.Value = 5000;
                        floorItem.ItemID = ItemID;
                        floorItem.MapID = 1002;
                        floorItem.MapObjType = Game.MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Y = Y;
                        floorItem.Type = Network.GamePackets.FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        foreach (Client.GameState C in Kernel.GamePool.Values)
                        {
                            if (C.Entity.MapID == 1002)
                            {
                                C.SendScreenSpawn(floorItem, true);
                                npc.SendSpawn(C);
                            }
                        }
                    }
                }
                #endregion

            }
        }
        public static void Load7()
        {
            if (Kernel.Maps.ContainsKey(1002))
            {
                #region CPBag

                uint ItemID = 0;
                byte type1 = 3;
                for (int i = 0; i < 3; i++)
                {
                    type1 = (byte)Kernel.Random.Next(1, 1);
                    switch (type1)
                    {
                        case 1:
                            ItemID = 720159;// algunas ID usted puede cambiarlas
                            break;
                    }
                    ushort X = X6, Y = Y6;
                    INpc npc = new Network.GamePackets.NpcSpawn();
                    npc.UID = 1305;
                    npc.Mesh = 13050;
                    npc.Type = Enums.NpcType.Talker;
                    npc.X = (ushort)(X - 1);
                    npc.Y = (ushort)(Y - 1);
                    npc.MapID = 1002;

                    Game.Map Map = Kernel.Maps[1002];
                    if (Map.SelectCoordonates(ref X, ref Y))
                    {
                        Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                        floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.BoundCps;
                        floorItem.Value = 5000;
                        floorItem.ItemID = ItemID;
                        floorItem.MapID = 1002;
                        floorItem.MapObjType = Game.MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Y = Y;
                        floorItem.Type = Network.GamePackets.FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        foreach (Client.GameState C in Kernel.GamePool.Values)
                        {
                            if (C.Entity.MapID == 1002)
                            {
                                C.SendScreenSpawn(floorItem, true);
                                npc.SendSpawn(C);
                            }
                        }


                    }
                }

                #endregion
            }

        }
        public static void Load8()
        {
            if (Kernel.Maps.ContainsKey(1002))
            {
                #region CPBag

                uint ItemID = 0;
                byte type1 = 3;
                for (int i = 0; i < 3; i++)
                {
                    type1 = (byte)Kernel.Random.Next(1, 1);
                    switch (type1)
                    {
                        case 1:
                            ItemID = 720159;// algunas ID usted puede cambiarlas
                            break;
                    }
                    ushort X = X7, Y = Y7;
                    INpc npc = new Network.GamePackets.NpcSpawn();
                    npc.UID = 1305;
                    npc.Mesh = 13050;
                    npc.Type = Enums.NpcType.Talker;
                    npc.X = (ushort)(X - 1);
                    npc.Y = (ushort)(Y - 1);
                    npc.MapID = 1002;

                    Game.Map Map = Kernel.Maps[1002];
                    if (Map.SelectCoordonates(ref X, ref Y))
                    {
                        Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                        floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.BoundCps;
                        floorItem.Value = 5000;
                        floorItem.ItemID = ItemID;
                        floorItem.MapID = 1002;
                        floorItem.MapObjType = Game.MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Y = Y;
                        floorItem.Type = Network.GamePackets.FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        foreach (Client.GameState C in Kernel.GamePool.Values)
                        {
                            if (C.Entity.MapID == 1002)
                            {
                                C.SendScreenSpawn(floorItem, true);
                                npc.SendSpawn(C);
                            }
                        }
                    }
                }
                #endregion

            }
        }
        public static void Load9()
        {
            if (Kernel.Maps.ContainsKey(1002))
            {
                #region CPBag

                uint ItemID = 0;
                byte type1 = 3;
                for (int i = 0; i < 3; i++)
                {
                    type1 = (byte)Kernel.Random.Next(1, 1);
                    switch (type1)
                    {
                        case 1:
                            ItemID = 720159;// algunas ID usted puede cambiarlas
                            break;
                    }
                    ushort X = X8, Y = Y8;
                    INpc npc = new Network.GamePackets.NpcSpawn();
                    npc.UID = 1305;
                    npc.Mesh = 13050;
                    npc.Type = Enums.NpcType.Talker;
                    npc.X = (ushort)(X - 1);
                    npc.Y = (ushort)(Y - 1);
                    npc.MapID = 1002;

                    Game.Map Map = Kernel.Maps[1002];
                    if (Map.SelectCoordonates(ref X, ref Y))
                    {
                        Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                        floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.BoundCps;
                        floorItem.Value = 5000;
                        floorItem.ItemID = ItemID;
                        floorItem.MapID = 1002;
                        floorItem.MapObjType = Game.MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Y = Y;
                        floorItem.Type = Network.GamePackets.FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        foreach (Client.GameState C in Kernel.GamePool.Values)
                        {
                            if (C.Entity.MapID == 1002)
                            {
                                C.SendScreenSpawn(floorItem, true);
                                npc.SendSpawn(C);
                            }
                        }
                    }
                }
                #endregion

            }
        }
        public static void Load10()
        {
            if (Kernel.Maps.ContainsKey(1002))
            {
                #region CPBag

                uint ItemID = 0;
                byte type1 = 3;
                for (int i = 0; i < 3; i++)
                {
                    type1 = (byte)Kernel.Random.Next(1, 1);
                    switch (type1)
                    {
                        case 1:
                            ItemID = 720159;// algunas ID usted puede cambiarlas
                            break;
                    }
                    ushort X = X9, Y = Y9;
                    INpc npc = new Network.GamePackets.NpcSpawn();
                    npc.UID = 1305;
                    npc.Mesh = 13050;
                    npc.Type = Enums.NpcType.Talker;
                    npc.X = (ushort)(X - 1);
                    npc.Y = (ushort)(Y - 1);
                    npc.MapID = 1002;

                    Game.Map Map = Kernel.Maps[1002];
                    if (Map.SelectCoordonates(ref X, ref Y))
                    {
                        Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                        floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.BoundCps;
                        floorItem.Value = 5000;
                        floorItem.ItemID = ItemID;
                        floorItem.MapID = 1002;
                        floorItem.MapObjType = Game.MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Y = Y;
                        floorItem.Type = Network.GamePackets.FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        foreach (Client.GameState C in Kernel.GamePool.Values)
                        {
                            if (C.Entity.MapID == 1002)
                            {
                                C.SendScreenSpawn(floorItem, true);
                                npc.SendSpawn(C);
                            }
                        }
                    }
                }
                #endregion

            }
        }
        public static void Load11()
        {
            if (Kernel.Maps.ContainsKey(1002))
            {
                #region CPBag

                uint ItemID = 0;
                byte type1 = 3;
                for (int i = 0; i < 3; i++)
                {
                    type1 = (byte)Kernel.Random.Next(1, 1);
                    switch (type1)
                    {
                        case 1:
                            ItemID = 720159;// algunas ID usted puede cambiarlas
                            break;
                    }
                    ushort X = X10, Y = Y10;
                    INpc npc = new Network.GamePackets.NpcSpawn();
                    npc.UID = 1305;
                    npc.Mesh = 13050;
                    npc.Type = Enums.NpcType.Talker;
                    npc.X = (ushort)(X - 1);
                    npc.Y = (ushort)(Y - 1);
                    npc.MapID = 1002;

                    Game.Map Map = Kernel.Maps[1002];
                    if (Map.SelectCoordonates(ref X, ref Y))
                    {
                        Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                        floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.BoundCps;
                        floorItem.Value = 5000;
                        floorItem.ItemID = ItemID;
                        floorItem.MapID = 1002;
                        floorItem.MapObjType = Game.MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Y = Y;
                        floorItem.Type = Network.GamePackets.FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        foreach (Client.GameState C in Kernel.GamePool.Values)
                        {
                            if (C.Entity.MapID == 1002)
                            {
                                C.SendScreenSpawn(floorItem, true);
                                npc.SendSpawn(C);
                            }
                        }
                    }
                }
                #endregion

            }
        }
        public static void Load12()
        {
            if (Kernel.Maps.ContainsKey(1002))
            {
                #region CPBag

                uint ItemID = 0;
                byte type1 = 3;
                for (int i = 0; i < 3; i++)
                {
                    type1 = (byte)Kernel.Random.Next(1, 1);
                    switch (type1)
                    {
                        case 1:
                            ItemID = 720159;// algunas ID usted puede cambiarlas
                            break;
                    }
                    ushort X = X11, Y = Y11;
                    INpc npc = new Network.GamePackets.NpcSpawn();
                    npc.UID = 1305;
                    npc.Mesh = 13050;
                    npc.Type = Enums.NpcType.Talker;
                    npc.X = (ushort)(X - 1);
                    npc.Y = (ushort)(Y - 1);
                    npc.MapID = 1002;

                    Game.Map Map = Kernel.Maps[1002];
                    if (Map.SelectCoordonates(ref X, ref Y))
                    {
                        Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                        floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.BoundCps;
                        floorItem.Value = 5000;
                        floorItem.ItemID = ItemID;
                        floorItem.MapID = 1002;
                        floorItem.MapObjType = Game.MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Y = Y;
                        floorItem.Type = Network.GamePackets.FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        foreach (Client.GameState C in Kernel.GamePool.Values)
                        {
                            if (C.Entity.MapID == 1002)
                            {
                                C.SendScreenSpawn(floorItem, true);
                                npc.SendSpawn(C);
                            }
                        }
                    }
                }
                #endregion

            }
        }
        public static void Load13()
        {
            if (Kernel.Maps.ContainsKey(1002))
            {
                #region CPBag

                uint ItemID = 0;
                byte type1 = 3;
                for (int i = 0; i < 3; i++)
                {
                    type1 = (byte)Kernel.Random.Next(1, 1);
                    switch (type1)
                    {
                        case 1:
                            ItemID = 720159;// algunas ID usted puede cambiarlas
                            break;
                    }
                    ushort X = X12, Y = Y12;
                    INpc npc = new Network.GamePackets.NpcSpawn();
                    npc.UID = 1305;
                    npc.Mesh = 13050;
                    npc.Type = Enums.NpcType.Talker;
                    npc.X = (ushort)(X - 1);
                    npc.Y = (ushort)(Y - 1);
                    npc.MapID = 1002;

                    Game.Map Map = Kernel.Maps[1002];
                    if (Map.SelectCoordonates(ref X, ref Y))
                    {
                        Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                        floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.BoundCps;
                        floorItem.Value = 5000;
                        floorItem.ItemID = ItemID;
                        floorItem.MapID = 1002;
                        floorItem.MapObjType = Game.MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Y = Y;
                        floorItem.Type = Network.GamePackets.FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        foreach (Client.GameState C in Kernel.GamePool.Values)
                        {
                            if (C.Entity.MapID == 1002)
                            {
                                C.SendScreenSpawn(floorItem, true);
                                npc.SendSpawn(C);
                            }
                        }
                    }
                }
                #endregion

            }
        }
        public static void Load14()
        {
            if (Kernel.Maps.ContainsKey(1002))
            {
                #region CPBag

                uint ItemID = 0;
                byte type1 = 3;
                for (int i = 0; i < 3; i++)
                {
                    type1 = (byte)Kernel.Random.Next(1, 1);
                    switch (type1)
                    {
                        case 1:
                            ItemID = 720159;// algunas ID usted puede cambiarlas
                            break;
                    }
                    ushort X = X13, Y = Y13;
                    INpc npc = new Network.GamePackets.NpcSpawn();
                    npc.UID = 1305;
                    npc.Mesh = 13050;
                    npc.Type = Enums.NpcType.Talker;
                    npc.X = (ushort)(X - 1);
                    npc.Y = (ushort)(Y - 1);
                    npc.MapID = 1002;

                    Game.Map Map = Kernel.Maps[1002];
                    if (Map.SelectCoordonates(ref X, ref Y))
                    {
                        Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                        floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.BoundCps;
                        floorItem.Value = 5000;
                        floorItem.ItemID = ItemID;
                        floorItem.MapID = 1002;
                        floorItem.MapObjType = Game.MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Y = Y;
                        floorItem.Type = Network.GamePackets.FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        foreach (Client.GameState C in Kernel.GamePool.Values)
                        {
                            if (C.Entity.MapID == 1002)
                            {
                                C.SendScreenSpawn(floorItem, true);
                                npc.SendSpawn(C);
                            }
                        }
                    }
                }
                #endregion

            }
        }
        public static void Load15()
        {
            if (Kernel.Maps.ContainsKey(1002))
            {
                #region CPBag

                uint ItemID = 0;
                byte type1 = 3;
                for (int i = 0; i < 3; i++)
                {
                    type1 = (byte)Kernel.Random.Next(1, 1);
                    switch (type1)
                    {
                        case 1:
                            ItemID = 720159;// algunas ID usted puede cambiarlas
                            break;
                    }
                    ushort X = X14, Y = Y14;
                    INpc npc = new Network.GamePackets.NpcSpawn();
                    npc.UID = 1305;
                    npc.Mesh = 13050;
                    npc.Type = Enums.NpcType.Talker;
                    npc.X = (ushort)(X - 1);
                    npc.Y = (ushort)(Y - 1);
                    npc.MapID = 1002;

                    Game.Map Map = Kernel.Maps[1002];
                    if (Map.SelectCoordonates(ref X, ref Y))
                    {
                        Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                        floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.BoundCps;
                        floorItem.Value = 5000;
                        floorItem.ItemID = ItemID;
                        floorItem.MapID = 1002;
                        floorItem.MapObjType = Game.MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Y = Y;
                        floorItem.Type = Network.GamePackets.FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        foreach (Client.GameState C in Kernel.GamePool.Values)
                        {
                            if (C.Entity.MapID == 1002)
                            {
                                C.SendScreenSpawn(floorItem, true);
                                npc.SendSpawn(C);
                            }
                        }
                    }
                }
                #endregion

            }
        }
        public static void Load16()
        {
            if (Kernel.Maps.ContainsKey(1002))
            {
                #region CPBag

                uint ItemID = 0;
                byte type1 = 3;
                for (int i = 0; i < 3; i++)
                {
                    type1 = (byte)Kernel.Random.Next(1, 1);
                    switch (type1)
                    {
                        case 1:
                            ItemID = 720159;// algunas ID usted puede cambiarlas
                            break;
                    }
                    ushort X = X15, Y = Y15;
                    INpc npc = new Network.GamePackets.NpcSpawn();
                    npc.UID = 1305;
                    npc.Mesh = 13050;
                    npc.Type = Enums.NpcType.Talker;
                    npc.X = (ushort)(X - 1);
                    npc.Y = (ushort)(Y - 1);
                    npc.MapID = 1002;

                    Game.Map Map = Kernel.Maps[1002];
                    if (Map.SelectCoordonates(ref X, ref Y))
                    {
                        Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                        floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.BoundCps;
                        floorItem.Value = 5000;
                        floorItem.ItemID = ItemID;
                        floorItem.MapID = 1002;
                        floorItem.MapObjType = Game.MapObjectType.Item;
                        floorItem.X = X;
                        floorItem.Y = Y;
                        floorItem.Type = Network.GamePackets.FloorItem.Drop;
                        floorItem.OnFloor = Time32.Now;
                        floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        while (Map.Npcs.ContainsKey(floorItem.UID))
                            floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                        Map.AddFloorItem(floorItem);
                        foreach (Client.GameState C in Kernel.GamePool.Values)
                        {
                            if (C.Entity.MapID == 1002)
                            {
                                C.SendScreenSpawn(floorItem, true);
                                npc.SendSpawn(C);
                            }
                        }
                    }
                }
                #endregion

            }
        }
        public static void Load17()
        {
            if (Kernel.Maps.ContainsKey(1002))
            {
                #region CPBag

                uint ItemID = 720159;
                ushort X = X16, Y = Y16;
                INpc npc = new Network.GamePackets.NpcSpawn();
                npc.UID = 1305;
                npc.Mesh = 13050;
                npc.Type = Enums.NpcType.Talker;
                npc.X = (ushort)(X - 1);
                npc.Y = (ushort)(Y - 1);
                npc.MapID = 1002;

                Game.Map Map = Kernel.Maps[1002];
                if (Map.SelectCoordonates(ref X, ref Y))
                {
                    Network.GamePackets.FloorItem floorItem = new Network.GamePackets.FloorItem(true);
                    floorItem.ValueType = Network.GamePackets.FloorItem.FloorValueType.BoundCps;
                    floorItem.Value = 5000;
                    floorItem.ItemID = ItemID;
                    floorItem.MapID = 1002;
                    floorItem.MapObjType = Game.MapObjectType.Item;
                    floorItem.X = X;
                    floorItem.Y = Y;
                    floorItem.Type = Network.GamePackets.FloorItem.Drop;
                    floorItem.OnFloor = Time32.Now;
                    floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                    while (Map.Npcs.ContainsKey(floorItem.UID))
                        floorItem.UID = Network.GamePackets.FloorItem.FloorUID.Next;
                    Map.AddFloorItem(floorItem);
                    foreach (Client.GameState C in Kernel.GamePool.Values)
                    {
                        if (C.Entity.MapID == 1002)
                        {
                            C.SendScreenSpawn(floorItem, true);
                            npc.SendSpawn(C);
                        }
                    }
                }
            }
                #endregion

        }


    }

}
