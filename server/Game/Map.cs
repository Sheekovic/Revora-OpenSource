// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using Revora.Interfaces;
using Revora.Network.GamePackets;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Generic;

namespace Revora.Game
{
    public unsafe class Map
    {
        public enum MapFlagType : byte
        {
            None = 0,
            Valid = 1 << 0,
            Monster = 1 << 1,
            Item = 1 << 2,
            Player = 1 << 3

        }
        public Size bounds;
        public MapFlagType[,] cells { get; set; }
        public bool IsFlagPresent(int x, int y, MapFlagType flag)
        {
            if (x > 0 && y > 0 && x < bounds.Width && y < bounds.Height)
                return (cells[x, y] & flag) == flag;
            return false;
        }
        public Counter CloneCounter = new Revora.Counter(0);
        public static Counter DynamicIDs = new Revora.Counter(11000) { Finish = 60000 };
        private static readonly object dynamicAllocation = new object();
        public static Enums.ConquerAngle[] Angles = new Enums.ConquerAngle[] 
        {
            Enums.ConquerAngle.SouthWest,
            Enums.ConquerAngle.West,
            Enums.ConquerAngle.NorthWest,
            Enums.ConquerAngle.North,
            Enums.ConquerAngle.NorthEast,
            Enums.ConquerAngle.East,
            Enums.ConquerAngle.SouthEast,
            Enums.ConquerAngle.South
        };
        public static Floor ArenaBaseFloor = null;
        public Counter EntityUIDCounter = new Revora.Counter(400000);
        public Counter EntityUIDCounter2 = new Revora.Counter(100000);
        public Counter EntityUIDCounter3 = new Revora.Counter(102);
        public List<Zoning.Zone> Zones = new List<Zoning.Zone>();
        public ushort ID;
        public ushort BaseID;
        public ushort Level;
        public bool WasPKFree = false;
        public Floor Floor;
        public string Path;
        public bool IsDynamic()
        {
            return BaseID != ID;
        }
        public SafeConcurrentDictionary<uint, Entity> Entities;
        public SafeConcurrentDictionary<uint, Entity> Companions;
        public SafeDictionary<uint, NpcSpawn> Flags;
        public Dictionary<uint, INpc> Dragon = new Dictionary<uint, INpc>();
        public static Dictionary<uint, INpc> WaitNpcs = new Dictionary<uint, INpc>();
        public static Dictionary<uint, INpc> SecoundNpc = new Dictionary<uint, INpc>();
        public static Dictionary<uint, Entity> SecoundEntities = new Dictionary<uint, Entity>();
        public Dictionary<uint, INpc> Statues = new Dictionary<uint, INpc>();
        public Dictionary<uint, INpc> Npcs;
        public Dictionary<uint, SobNpcSpawn> Npcs1;
        public ConcurrentDictionary<uint, FloorItem> FloorItems;
        public Dictionary<uint, WindWalker.ThunderCloud> Clouds = new Dictionary<uint, WindWalker.ThunderCloud>();
        public void AddCloud(WindWalker.ThunderCloud entity)
        {
            if (!this.Clouds.ContainsKey(entity.UID))
                this.Clouds.Add(entity.UID, entity);

        }
        public void AddPlayer(Entity entity)
        {
            if (this.Entities.TryAdd(entity.UID, entity))
            {
                this.Floor[(int)entity.X, (int)entity.Y, MapObjectType.Monster, entity] = false;
            }
        }
        public void AddPole(INpc npc)
        {
            Npcs[npc.UID] = npc;
            Floor[npc.X, npc.Y, MapObjectType.InvalidCast, npc] = false;
        }
        public void RemovePole(INpc npc)
        {
            Npcs.Remove(npc.UID);
            Floor[npc.X, npc.Y, MapObjectType.InvalidCast, null] = true;
        }
        public void AddNpc(INpc npc)
        {
            if (Npcs.ContainsKey(npc.UID) == false)
            {
                if (!Map.SecoundNpc.ContainsKey(npc.UID) && npc.MapID == 1002)
                {
                    Map.SecoundNpc.Add(npc.UID, npc);
                }
                Npcs.Add(npc.UID, npc);
                if(Features.FurnitureFootprint.Known(npc)){Features.FurnitureFootprint.Add(this,npc);return;}
                #region Setting the near coords invalid to avoid unpickable items.
                Floor[npc.X, npc.Y, MapObjectType.InvalidCast, npc] = false;
                if (npc.Mesh / 10 != 108 && (byte)npc.Type < 10)
                {
                    ushort X = npc.X, Y = npc.Y;
                    foreach (Enums.ConquerAngle angle in Angles)
                    {
                        ushort xX = X, yY = Y;
                        UpdateCoordonatesForAngle(ref xX, ref yY, angle);
                        Floor[xX, yY, MapObjectType.InvalidCast, null] = false;
                    }
                }
                #endregion
            }
        }
        public void AddNpc(INpc npc, bool addquery = false) 
        { 
            if (Npcs.ContainsKey(npc.UID) == false || addquery)
            {
                if (!addquery) Npcs.Add(npc.UID, npc);
                if(Features.FurnitureFootprint.Known(npc)){Features.FurnitureFootprint.Add(this,npc);return;}
                #region Setting the near coords invalid to avoid unpickable items.
                Floor[npc.X, npc.Y, MapObjectType.InvalidCast, npc] = false; 
                if (npc.Mesh / 10 != 108 && (byte)npc.Type < 10) 
                {
                    ushort X = npc.X, Y = npc.Y;
                    foreach (Enums.ConquerAngle angle in Angles) 
                    {
                        ushort xX = X, yY = Y;
                        UpdateCoordonatesForAngle(ref xX, ref yY, angle); 
                        Floor[xX, yY, MapObjectType.InvalidCast, null] = false; 
                    } 
                }
                #endregion
            } 
        }
        public void RemoveNpc(INpc npc, bool query = false) 
        { 
            if (Npcs.ContainsKey(npc.UID) || query)
            {
                if (!query) Npcs.Remove(npc.UID);
                if(Features.FurnitureFootprint.Known(npc)){Features.FurnitureFootprint.Remove(this,npc);return;}
                #region Setting the near coords invalid to avoid unpickable items.
                Floor[npc.X, npc.Y, MapObjectType.InvalidCast, null] = true;
                if (npc.Mesh / 10 != 108 && (byte)npc.Type < 10) 
                { 
                    ushort X = npc.X, Y = npc.Y;
                    foreach (Enums.ConquerAngle angle in Angles) 
                    { 
                        ushort xX = X, yY = Y; UpdateCoordonatesForAngle(ref xX, ref yY, angle); 
                        Floor[xX, yY, MapObjectType.InvalidCast, null] = true; 
                    } 
                } 
            #endregion 
        } 
        }
        public void RemoveNpc(INpc npc)
        {
            if (this.Npcs.ContainsKey(npc.UID))
            {
                this.Npcs.Remove(npc.UID);
                if(Features.FurnitureFootprint.Known(npc)){Features.FurnitureFootprint.Remove(this,npc);return;}
                this.Floor[(int)npc.X, (int)npc.Y, MapObjectType.InvalidCast, null] = true;
                if (npc.Mesh / 10 != 108 && npc.Type < Enums.NpcType.Pole)
                {
                    ushort x = npc.X;
                    ushort y = npc.Y;
                    Enums.ConquerAngle[] angles = Map.Angles;
                    for (int i = 0; i < angles.Length; i++)
                    {
                        Enums.ConquerAngle angle = angles[i];
                        ushort x2 = x;
                        ushort y2 = y;
                        Map.UpdateCoordonatesForAngle(ref x2, ref y2, angle);
                        this.Floor[(int)x2, (int)y2, MapObjectType.InvalidCast, null] = true;
                    }
                }
            }
        }
        public void AddEntity(Entity entity)
        {
            Features.AbyssTrialWorld.Normalize(entity);
            if (entity.UID >= 200000003 && entity.UID <= 200000008 && !Map.SecoundEntities.ContainsKey(entity.UID))
            {
                Map.SecoundEntities.Add(entity.UID, entity);
            }
            if (entity.UID < 800000)
            {
                if (Entities.TryAdd(entity.UID, entity))
                {
                    Floor[entity.X, entity.Y, MapObjectType.Monster, entity] = false;
                }
            }
            else
            {
                // Registration and its floor update share one writer boundary.
                // Concurrent readers may safely enumerate the actor registry.
                lock (Companions)
                {
                    if (Companions.TryAdd(entity.UID, entity))
                        Floor[entity.X, entity.Y, MapObjectType.Monster, entity] = false;
                }
            }
        }
        public void AddEntity2(Entity entity)
        {
            Features.AbyssTrialWorld.Normalize(entity);
            if (this.Entities.TryAdd(entity.UID, entity))
            {
                this.Floor[entity.X, entity.Y, MapObjectType.Monster, entity] = false;
            }
        }
        public void RemoveEntity(Entity entity)
        {
            if (((ICollection<KeyValuePair<uint,Entity>>)Entities).Remove(new KeyValuePair<uint,Entity>(entity.UID,entity)))
            {
                Floor[entity.X, entity.Y, MapObjectType.Monster, entity] = true;
            }
            lock (Companions)
            {
                if (((ICollection<KeyValuePair<uint, Entity>>)Companions).Remove(
                    new KeyValuePair<uint, Entity>(entity.UID, entity)))
                    Floor[entity.X, entity.Y, MapObjectType.Monster, entity] = true;
            }
        }
        public void AddFloorItem(Network.GamePackets.FloorItem floorItem)
        {
            FloorItems.Add(floorItem.UID, floorItem);
            Floor[floorItem.X, floorItem.Y, MapObjectType.Item, floorItem] = false;
        }
        public void RemoveFloorItem(Network.GamePackets.FloorItem floorItem)
        {
            if (Features.RebirthSkillRules.MonkEffect(floorItem) || Features.NinjaFlowerHunt.Managed(floorItem) || Features.MonsterEquipmentDrops.Managed(floorItem) || Features.GrottoKeyDrops.Managed(floorItem) || Features.MeteorDrops.Managed(floorItem) || Features.CleanWaterDrops.Managed(floorItem))
            {
                lock (floorItem)
                {
                    var drops = (ICollection<KeyValuePair<uint, FloorItem>>)FloorItems;
                    if (drops.Remove(new KeyValuePair<uint, FloorItem>(floorItem.UID, floorItem)))
                        Floor[floorItem.X, floorItem.Y, MapObjectType.Item, floorItem] = true;
                }
                return;
            }
            FloorItems.Remove(floorItem.UID);
            Floor[floorItem.X, floorItem.Y, MapObjectType.Item, floorItem] = true;
        }
        public void RemoveFlag(NpcSpawn npc)
        {
            if (this.Flags.ContainsKey(npc.UID))
            {
                this.Flags.Remove(npc.UID);
                this.Floor[npc.X, npc.Y, MapObjectType.Npc, npc] = true;
            }
        }
        public bool SelectCoordonates(ref ushort X, ref ushort Y)
        {
            if (Floor[X, Y, MapObjectType.Item, null])
            {
                bool can = true;
                if (Zones.Count != 0)
                {
                    foreach (Zoning.Zone z in Zones)
                    {
                        if (z.IsPartOfRectangle(new Point() { X = X, Y = Y }))
                        {
                            can = false;
                            break;
                        }
                    }
                }
                if (can)
                    return true;
            }

            foreach (Enums.ConquerAngle angle in Angles)
            {
                ushort xX = X, yY = Y;
                UpdateCoordonatesForAngle(ref xX, ref yY, angle);
                if (Floor[xX, yY, MapObjectType.Item, null])
                {
                    if (Zones.Count != 0)
                    {
                        bool can = true;
                        foreach (Zoning.Zone z in Zones)
                        {
                            if (z.IsPartOfRectangle(new Point() { X = xX, Y = yY })) { can = false; break; }
                        }
                        if (!can)
                            continue;
                    }
                    X = xX;
                    Y = yY;
                    return true;
                }
            }
            return false;
        }
        public static void UpdateCoordonatesForAngle(ref ushort X, ref ushort Y, Enums.ConquerAngle angle)
        {
            sbyte xi = 0, yi = 0;
            switch (angle)
            {
                case Enums.ConquerAngle.North: xi = -1; yi = -1; break;
                case Enums.ConquerAngle.South: xi = 1; yi = 1; break;
                case Enums.ConquerAngle.East: xi = 1; yi = -1; break;
                case Enums.ConquerAngle.West: xi = -1; yi = 1; break;
                case Enums.ConquerAngle.NorthWest: xi = -1; break;
                case Enums.ConquerAngle.SouthWest: yi = 1; break;
                case Enums.ConquerAngle.NorthEast: yi = -1; break;
                case Enums.ConquerAngle.SouthEast: xi = 1; break;
            }
            X = (ushort)(X + xi);
            Y = (ushort)(Y + yi);
        }
        public static void Pushback(ref ushort x, ref ushort y, Enums.ConquerAngle angle, int paces)
        {
            sbyte xi = 0, yi = 0;
            for (int i = 0; i < paces; i++)
            {
                switch (angle)
                {
                    case Enums.ConquerAngle.North: xi = -1; yi = -1; break;
                    case Enums.ConquerAngle.South: xi = 1; yi = 1; break;
                    case Enums.ConquerAngle.East: xi = 1; yi = -1; break;
                    case Enums.ConquerAngle.West: xi = -1; yi = 1; break;
                    case Enums.ConquerAngle.NorthWest: xi = -1; break;
                    case Enums.ConquerAngle.SouthWest: yi = 1; break;
                    case Enums.ConquerAngle.NorthEast: yi = -1; break;
                    case Enums.ConquerAngle.SouthEast: xi = 1; break;
                }
                x = (ushort)(x + xi);
                y = (ushort)(y + yi);
            }
        }
        #region Scenes
        private SceneFile[] Scenes;
        private static string NTString(string value)
        {
            value = value.Remove(value.IndexOf("\0"));
            return value;
        }
        private SceneFile CreateSceneFile(BinaryReader Reader)
        {
            SceneFile file = new SceneFile();
            file.SceneFileName = NTString(Encoding.Default.GetString(Reader.ReadBytes(260)));
            file.Location = new Point(Reader.ReadInt32(), Reader.ReadInt32());
            using (BinaryReader reader = new BinaryReader(new FileStream(Constants.DataHolderPath + file.SceneFileName, FileMode.Open)))
            {
                ScenePart[] partArray = new ScenePart[reader.ReadInt32()];
                for (int i = 0; i < partArray.Length; i++)
                {
                    reader.BaseStream.Seek(0x14cL, SeekOrigin.Current);
                    partArray[i].Size = new Size(reader.ReadInt32(), reader.ReadInt32());
                    reader.BaseStream.Seek(4L, SeekOrigin.Current);
                    partArray[i].StartPosition = new Point(reader.ReadInt32(), reader.ReadInt32());
                    reader.BaseStream.Seek(4L, SeekOrigin.Current);
                    partArray[i].NoAccess = new bool[partArray[i].Size.Width, partArray[i].Size.Height];
                    for (int j = 0; j < partArray[i].Size.Height; j++)
                    {
                        for (int k = 0; k < partArray[i].Size.Width; k++)
                        {
                            partArray[i].NoAccess[k, j] = reader.ReadInt32() == 0;
                            reader.BaseStream.Seek(8L, SeekOrigin.Current);
                        }
                    }
                }
                file.Parts = partArray;
            }
            return file;
        }
        public struct SceneFile
        {
            public string SceneFileName
            {
                get;
                set;
            }
            public Point Location
            {
                get;
                set;
            }
            public ScenePart[] Parts
            {
                get;
                set;
            }
        }
        public struct ScenePart
        {
            public string Animation;
            public string PartFile;
            public Point Offset;
            public int aniInterval;
            public System.Drawing.Size Size;
            public int Thickness;
            public Point StartPosition;
            public bool[,] NoAccess;
        }
        #endregion
        public Map(ushort id, string path)
        {
            if (!Kernel.Maps.ContainsKey(id))
                Kernel.Maps.Add(id, this);
            Npcs = new Dictionary<uint, INpc>();
            Entities = new SafeConcurrentDictionary<uint, Entity>();
            FloorItems = new ConcurrentDictionary<uint, FloorItem>();
            Floor = new Floor(0, 0, id);
            Companions = new SafeConcurrentDictionary<uint, Entity>();
            ID = id;
            BaseID = id;
            if (path == "") path = Database.DMaps.MapPaths[id];
            Path = path;
            #region Loading floor.
            if (File.Exists(Constants.DMapsPath + "\\maps\\" + id.ToString() + ".map"))
            {
                byte[] buff = File.ReadAllBytes(Constants.DMapsPath + "\\maps\\" + id.ToString() + ".map");
                MemoryStream FS = new MemoryStream(buff);
                BinaryReader BR = new BinaryReader(FS);
                int Width = BR.ReadInt32();
                int Height = BR.ReadInt32();
                Floor = new Game.Floor(Width, Height, ID);
                if (id == 700)
                    ArenaBaseFloor = new Game.Floor(Width, Height, ID);
                for (ushort y = 0; y < Height; y = (ushort)(y + 1))
                {
                    for (ushort x = 0; x < Width; x = (ushort)(x + 1))
                    {
                        bool walkable = !(BR.ReadByte() == 1 ? true : false);
                        Floor[x, y, MapObjectType.InvalidCast, null] = walkable;
                        if (id == 700)
                            ArenaBaseFloor[x, y, MapObjectType.InvalidCast, null] = walkable;
                    }
                }

                BR.Close();
                FS.Close();
            }
            else
            {
                if (File.Exists(Constants.DMapsPath + Path))
                {
                    byte[] buff = File.ReadAllBytes(Constants.DMapsPath + Path);
                    MemoryStream FS = new MemoryStream(buff);
                    BinaryReader BR = new BinaryReader(FS);
                    BR.ReadBytes(268);
                    int Width = BR.ReadInt32();
                    int Height = BR.ReadInt32();
                    Floor = new Game.Floor(Width, Height, ID);
                    if (id == 700)
                        ArenaBaseFloor = new Game.Floor(Width, Height, ID);
                    for (ushort y = 0; y < Height; y = (ushort)(y + 1))
                    {
                        for (ushort x = 0; x < Width; x = (ushort)(x + 1))
                        {
                            bool walkable = !Convert.ToBoolean(BR.ReadUInt16());
                            Floor[x, y, MapObjectType.InvalidCast, null] = walkable;
                            if (id == 700)
                                ArenaBaseFloor[x, y, MapObjectType.InvalidCast, null] = walkable;
                            BR.BaseStream.Seek(4L, SeekOrigin.Current);
                        }
                        BR.BaseStream.Seek(4L, SeekOrigin.Current);
                    }
                    uint amount = BR.ReadUInt32();
                    BR.BaseStream.Seek(amount * 12, SeekOrigin.Current);
                    LoadMapObjects(BR);
                    MergeSceneToTextureArea();

                    BR.Close();
                    FS.Close();
                    SaveMap();
                }
            }
            #endregion
            LoadNpcs();
            LoadMonsters();
            LoadPortals();
        }
        public Map(ushort id, ushort baseid, string path)
            : this(id, baseid, path, true)
        {
        }
        public Map(ushort id, ushort baseid, string path, bool loadCatalog)
            : this(id, baseid, path, loadCatalog, true)
        {
        }
        private Map(ushort id, ushort baseid, string path, bool loadCatalog, bool register, bool compactFloor=false)
        {
            if (register && !Kernel.Maps.ContainsKey(id))
                Kernel.Maps.Add(id, this);
            Npcs = new Dictionary<uint, INpc>();
            Entities = new SafeConcurrentDictionary<uint, Entity>();
            Companions = new SafeConcurrentDictionary<uint, Entity>();
            FloorItems = new ConcurrentDictionary<uint, FloorItem>();
            ID = id;
            BaseID = baseid;
            Path = path;
            if (String.IsNullOrEmpty(path))
                Path = path = Database.DMaps.MapPaths[baseid];
            Floor = new Floor(0, 0, id);

            #region Loading floor.
            if (compactFloor)
            {
                if (loadCatalog || register) throw new InvalidOperationException("Private compact floors require isolated dynamic allocation.");
                string root=System.IO.Path.GetFullPath(System.IO.Path.Combine(Constants.DMapsPath,"maps"));
                string target=System.IO.Path.GetFullPath(System.IO.Path.Combine(Constants.DMapsPath,path));
                if (!target.StartsWith(root+System.IO.Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)
                    || !target.EndsWith(".map",StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Private floor path is outside maps.");
                var length=new FileInfo(target).Length;
                if(length<8 || length>4194312)throw new InvalidDataException("Invalid private floor size.");
                byte[] bytes=File.ReadAllBytes(target);
                using(var reader=new BinaryReader(new MemoryStream(bytes)))
                {
                    int width=reader.ReadInt32(),height=reader.ReadInt32();
                    if(width<1 || height<1 || width>4096 || height>4096 || 8L+(long)width*height!=bytes.Length)
                        throw new InvalidDataException("Invalid private floor dimensions.");
                    for(int i=8;i<bytes.Length;i++)if(bytes[i]>1)throw new InvalidDataException("Invalid private blocked cell.");
                    Floor=new Floor(width,height,id);
                    for(int y=0;y<height;y++)for(int x=0;x<width;x++)Floor[x,y,MapObjectType.InvalidCast,null]=reader.ReadByte()==0;
                }
            }
            else if (id != baseid && baseid == 700 && ArenaBaseFloor != null)
            {
                Floor = new Game.Floor(ArenaBaseFloor.Bounds.Width, ArenaBaseFloor.Bounds.Height, ID);
                for (ushort y = 0; y < ArenaBaseFloor.Bounds.Height; y = (ushort)(y + 1))
                {
                    for (ushort x = 0; x < ArenaBaseFloor.Bounds.Width; x = (ushort)(x + 1))
                    {
                        Floor[x, y, MapObjectType.InvalidCast, null] = !ArenaBaseFloor[x, y, MapObjectType.InvalidCast, null];
                    }
                }
            }
            else
            {
                if (File.Exists(Constants.DMapsPath + "\\maps\\" + baseid.ToString() + ".map"))
                {
                    byte[] buff = File.ReadAllBytes(Constants.DMapsPath + "\\maps\\" + baseid.ToString() + ".map");
                    MemoryStream FS = new MemoryStream(buff);
                    BinaryReader BR = new BinaryReader(FS);
                    int Width = BR.ReadInt32();
                    int Height = BR.ReadInt32();

                    Floor = new Game.Floor(Width, Height, ID);

                    for (ushort y = 0; y < Height; y = (ushort)(y + 1))
                    {
                        for (ushort x = 0; x < Width; x = (ushort)(x + 1))
                        {
                            Floor[x, y, MapObjectType.InvalidCast, null] = !(BR.ReadByte() == 1 ? true : false);
                        }
                    }
                    BR.Close();
                    FS.Close();
                }
                else
                {
                    if (File.Exists(Constants.DMapsPath + Path))
                    {
                        FileStream FS = new FileStream(Constants.DMapsPath + Path, FileMode.Open);
                        BinaryReader BR = new BinaryReader(FS);
                        BR.ReadBytes(268);
                        int Width = BR.ReadInt32();
                        int Height = BR.ReadInt32();

                        Floor = new Game.Floor(Width, Height, ID);

                        for (ushort y = 0; y < Height; y = (ushort)(y + 1))
                        {
                            for (ushort x = 0; x < Width; x = (ushort)(x + 1))
                            {
                                Floor[x, y, MapObjectType.InvalidCast, null] = !Convert.ToBoolean(BR.ReadUInt16());

                                BR.BaseStream.Seek(4L, SeekOrigin.Current);
                            }
                            BR.BaseStream.Seek(4L, SeekOrigin.Current);
                        }
                        uint amount = BR.ReadUInt32();
                        BR.BaseStream.Seek(amount * 12, SeekOrigin.Current);

                        int num = BR.ReadInt32();
                        List<SceneFile> list = new List<SceneFile>();
                        for (int i = 0; i < num; i++)
                        {
                            switch (BR.ReadInt32())
                            {
                                case 10:
                                    BR.BaseStream.Seek(0x48L, SeekOrigin.Current);
                                    break;

                                case 15:
                                    BR.BaseStream.Seek(0x114L, SeekOrigin.Current);
                                    break;

                                case 1:
                                    list.Add(this.CreateSceneFile(BR));
                                    break;

                                case 4:
                                    BR.BaseStream.Seek(0x1a0L, SeekOrigin.Current);
                                    break;
                            }
                        }
                        Scenes = list.ToArray();

                        for (int i = 0; i < Scenes.Length; i++)
                        {
                            foreach (ScenePart part in Scenes[i].Parts)
                            {
                                for (int j = 0; j < part.Size.Width; j++)
                                {
                                    for (int k = 0; k < part.Size.Height; k++)
                                    {
                                        Point point = new Point();
                                        point.X = ((Scenes[i].Location.X + part.StartPosition.X) + j) - part.Size.Width;
                                        point.Y = ((Scenes[i].Location.Y + part.StartPosition.Y) + k) - part.Size.Height;
                                        Floor[(ushort)point.X, (ushort)point.Y, MapObjectType.InvalidCast, null] = part.NoAccess[j, k];
                                    }
                                }
                            }
                        }

                        BR.Close();
                        FS.Close();
                        SaveMap();
                    }
                }
            }
            #endregion
            if (loadCatalog)
            {
                LoadNpcs();
                LoadMonsters();
                LoadPortals();
            }
        }
        private void MergeSceneToTextureArea()
        {
            for (int i = 0; i < Scenes.Length; i++)
            {
                if (Scenes[i].Parts == null) return;
                foreach (ScenePart part in Scenes[i].Parts)
                {
                    for (int j = 0; j < part.Size.Width; j++)
                    {
                        for (int k = 0; k < part.Size.Height; k++)
                        {
                            Point point = new Point
                            {
                                X = ((Scenes[i].Location.X + part.StartPosition.X) - j),
                                Y = ((Scenes[i].Location.Y + part.StartPosition.Y) - k)
                            };
                            Floor[(ushort)point.X, (ushort)point.Y, MapObjectType.InvalidCast] = part.NoAccess[j, k];
                        }
                    }
                }
            }
        }
        private void LoadMapObjects(BinaryReader Reader)
        {
            int num = Reader.ReadInt32();
            List<SceneFile> list = new List<SceneFile>();
            for (int i = 0; i < num; i++)
            {
                int id = Reader.ReadInt32();
                id = (byte)id;
                switch (id)
                {
                    case 10:
                        Reader.BaseStream.Seek(0x48L, SeekOrigin.Current);
                        break;

                    case 15:
                        Reader.BaseStream.Seek(0x114L, SeekOrigin.Current);
                        break;

                    case 1:
                        list.Add(this.CreateSceneFile(Reader));
                        break;

                    case 4:
                        Reader.BaseStream.Seek(0x1a0L, SeekOrigin.Current);
                        break;
                }
            }
            Scenes = list.ToArray();
        }
        private void LoadPortals()
        {
            IniFile file = new Revora.IniFile(Constants.PortalsPath);
            ushort portalCount = file.ReadUInt16(BaseID.ToString(), "Count");

            for (int i = 0; i < portalCount; i++)
            {
                string _PortalEnter = file.ReadString(BaseID.ToString(), "PortalEnter" + i.ToString());
                string _PortalExit = file.ReadString(BaseID.ToString(), "PortalExit" + i.ToString());
                string[] PortalEnter = _PortalEnter.Split(' ');
                string[] PortalExit = _PortalExit.Split(' ');
                Game.Portal portal = new Game.Portal();
                portal.CurrentMapID = Convert.ToUInt16(PortalEnter[0]);
                portal.CurrentX = Convert.ToUInt16(PortalEnter[1]);
                portal.CurrentY = Convert.ToUInt16(PortalEnter[2]);
                if (PortalExit.Length == 3)
                {
                    portal.DestinationMapID = Convert.ToUInt16(PortalExit[0]);
                    portal.DestinationX = Convert.ToUInt16(PortalExit[1]);
                    portal.DestinationY = Convert.ToUInt16(PortalExit[2]);
                }
                Portals.Add(portal);
            }
        }
        public List<Game.Portal> Portals = new List<Game.Portal>();
        private IDisposable Timer;
        internal void EnsureMonsterTimer() { lock(this) { if(!disposed && Timer==null)Timer=MonsterTimers.Add(this); } }

        public static sbyte[] XDir = new sbyte[] 
        { 
            -1, -2, -2, -1, 1, 2, 2, 1,
             0, -2, -2, -2, 0, 2, 2, 2, 
            -1, -2, -2, -1, 1, 2, 2, 1,
             0, -1, -1, -1, 0, 1, 1, 1,
        };
        public static sbyte[] YDir = new sbyte[] 
        {
            2,  1, -1, -2, -2, -1, 1, 2,
            2,  2,  0, -2, -2, -2, 0, 2, 
            2,  1, -1, -2, -2, -1, 1, 2, 
            1,  1,  0, -1, -1, -1, 0, 1
        };
        public SafeConcurrentDictionary<uint, StaticEntity> StaticEntities = new SafeConcurrentDictionary<uint, StaticEntity>();
        public void AddStaticEntity(StaticEntity item)
        {
            Floor[item.X, item.Y, MapObjectType.StaticEntity, null] = false;
            StaticEntities[item.UID] = item;
        }
        public void RemoveStaticItem(StaticEntity item)
        {
            Floor[item.X, item.Y, MapObjectType.StaticEntity, null] = true;
            StaticEntities.Remove(item.UID);
        }
        private void SaveMap()
        {
            if (!File.Exists(Constants.DMapsPath + "\\maps\\" + BaseID.ToString() + ".map"))
            {
                FileStream stream = new FileStream(Constants.DMapsPath + "\\maps\\" + BaseID.ToString() + ".map", FileMode.Create);
                BinaryWriter writer = new BinaryWriter(stream);
                writer.Write((uint)Floor.Bounds.Width);
                writer.Write((uint)Floor.Bounds.Height);
                for (int y = 0; y < Floor.Bounds.Height; y++)
                {
                    for (int x = 0; x < Floor.Bounds.Width; x++)
                    {
                        writer.Write((byte)(Floor[x, y, MapObjectType.InvalidCast, null] == true ? 1 : 0));
                    }
                }
                writer.Close();
                stream.Close();
            }
        }
        private void LoadNpcs()
        {
            using (var command = new Database.MySqlCommand(Database.MySqlCommandType.SELECT))
            {
                command.Select("npcs").Where("mapid", ID);
                using (var reader = new Database.MySqlReader(command))
                {
                    while (reader.Read())
                    {
                        INpc npc = new NpcSpawn();
                        npc.UID = reader.ReadUInt32("id");
                        // The legacy Tempest route is retired by TempestWingsQuest.
                        // Its surrounding blocked cells overlap the journal's Mr. Wonder.
                        if (ID == 1011 && npc.UID == 600065) continue;
                        npc.Mesh = reader.ReadUInt16("lookface");
                        string npcName = reader.ReadString("name");
                        if (npc.UID == 59558)
                            npc.Name = npcName;
                        npc.Type = (Enums.NpcType)reader.ReadByte("type");
                        npc.X = reader.ReadUInt16("cellx"); ;
                        npc.Y = reader.ReadUInt16("celly");
                        npc.effect = reader.ReadString("effect");
                        npc.MapID = ID;
                        AddNpc(npc);
                    }
                }
            }
            using (var command = new Database.MySqlCommand(Database.MySqlCommandType.SELECT))
            {
                command.Select("sobnpcs").Where("mapid", ID);
                using (var reader = new Database.MySqlReader(command))
                {
                    while (reader.Read())
                    {
                        SobNpcSpawn npc = new SobNpcSpawn();
                        npc.UID = reader.ReadUInt32("id");
                        // Some catalogs store the retired Tempest talker as a SobNpc.
                        if (ID == 1011 && npc.UID == 600065) continue;
                        npc.Mesh = reader.ReadUInt16("lookface");
                        if (ID == 1039) npc.Mesh = (ushort)(npc.Mesh - npc.Mesh % 10 + 7);
                        npc.Type = (Enums.NpcType)reader.ReadByte("type");
                        npc.X = reader.ReadUInt16("cellx");
                        npc.Y = reader.ReadUInt16("celly");
                        npc.MapID = reader.ReadUInt16("mapid");
                        npc.Sort = reader.ReadUInt16("sort");
                        npc.ShowName = true;
                        npc.Name = reader.ReadString("name");
                        npc.Hitpoints = reader.ReadUInt32("life");
                        npc.MaxHitpoints = reader.ReadUInt32("maxlife");
                        npc._isprize = reader.ReadBoolean("prize");
                        npc.effect = reader.ReadString("effect");
                        AddNpc(npc);
                    }
                }
            }
            using (var command = new Database.MySqlCommand(Database.MySqlCommandType.SELECT))
            {
                command.Select("Furniture").Where("mapid", ID);
                using (var reader = new Database.MySqlReader(command))
                {
                    while (reader.Read())
                    {
                        INpc npc = new NpcSpawn();
                        npc.UID = reader.ReadUInt32("id");
                        npc.Mesh = reader.ReadUInt16("lookface");
                        npc.Type = (Enums.NpcType)reader.ReadByte("type");
                        npc.X = reader.ReadUInt16("cellx"); ;
                        npc.Y = reader.ReadUInt16("celly");
                        npc.MapID = ID;
                        AddNpc(npc);
                    }
                }
            }
        }
        public bool FreezeMonsters = false;
        internal Features.PotencyHunt PotencyPopulation;
        public void LoadMonsters()
        {
            Companions = new SafeConcurrentDictionary<uint, Entity>();
            using (var command = new Database.MySqlCommand(Database.MySqlCommandType.SELECT))
            {
                command.Select("monsterspawns").Where("mapid", ID);
                using (var reader = new Database.MySqlReader(command))
                {
                    int mycount = 0;
                    try
                    {
                        while (reader.Read())
                        {
                            uint monsterID = reader.ReadUInt32("npctype");
                            ushort CircleDiameter = reader.ReadUInt16("maxnpc");
                            ushort X = reader.ReadUInt16("bound_x");
                            ushort Y = reader.ReadUInt16("bound_y");
                            ushort XPlus = reader.ReadUInt16("bound_cx");
                            ushort YPlus = reader.ReadUInt16("bound_cy");
                            int Amount = reader.ReadUInt16("max_per_gen");
                            int respawn = reader.ReadInt32("rest_secs");
                            Features.FamilyFeudPopulation.Correct(this, monsterID, ref X, ref Y, ref XPlus, ref YPlus);
                            if (Database.MonsterInformation.MonsterInformations.ContainsKey(monsterID))
                            {
                                Database.MonsterInformation mt = Database.MonsterInformation.MonsterInformations[monsterID];
                                // Apply once to each configured flock; keep database counts as the baseline.
                                bool pheasant = mt.Name == "Pheasant";
                                if (pheasant) Amount *= 2;
                                int countBeforeSpawn = mycount;
                                mt.RespawnTime = respawn + 5;
                                mt.BoundX = X;
                                mt.BoundY = Y;
                                mt.BoundCX = XPlus;
                                mt.BoundCY = YPlus;

                                if (mt.Guard || (mt.Name != null && mt.Name.Contains("Guard1")))
                                {
                                    int spreadX = XPlus == 0 ? 1 : XPlus;
                                    int spreadY = YPlus == 0 ? 1 : YPlus;
                                    for (int count = 0; count < Amount; count++)
                                    {
                                        ushort gx = (ushort)(X + Kernel.Random.Next(0, spreadX));
                                        ushort gy = (ushort)(Y + Kernel.Random.Next(0, spreadY));
                                        bool placed = false;
                                        for (int count2 = 0; count2 < 50; count2++)
                                        {
                                            if (Floor[gx, gy, MapObjectType.Monster, null])
                                            {
                                                placed = true;
                                                break;
                                            }
                                            gx = (ushort)(X + Kernel.Random.Next(0, spreadX));
                                            gy = (ushort)(Y + Kernel.Random.Next(0, spreadY));
                                        }
                                        if (placed)
                                        {
                                            mycount++;
                                            Revora.Game.Features.CityGuardSystem.AdoptSpawn(ID, gx, gy);
                                        }
                                    }
                                    continue;
                                }

                                bool more = true;
                                for (int count = 0; count < Amount; count++)
                                {
                                    if (!more)
                                        break;
                                    Entity entity = new Entity(EntityFlag.Monster, false);
                                    entity.MapObjType = MapObjectType.Monster;
                                    entity.MonsterInfo = mt.Copy();
                                    entity.MonsterInfo.Owner = entity;
                                    entity.Name = mt.Name;
                                    entity.MinAttack = mt.MinAttack;
                                    entity.MaxAttack = entity.MagicAttack = mt.MaxAttack;
                                    entity.Hitpoints = entity.MaxHitpoints = mt.Hitpoints;
                                    entity.Defence = mt.Defence;
                                    entity.Body = mt.Mesh;
                                    entity.Level = mt.Level;
                                    entity.ApplyMonsterBossHud();
                                    entity.UID = EntityUIDCounter.Next;
                                    entity.MapID = ID;
                                    entity.SendUpdates = true;
                                    entity.X = (ushort)(X + Kernel.Random.Next(0, XPlus));
                                    entity.Y = (ushort)(Y + Kernel.Random.Next(0, YPlus));
                                    for (int count2 = 0; count2 < 50; count2++)
                                    {
                                        if (!Floor[entity.X, entity.Y, MapObjectType.Monster, null])
                                        {
                                            entity.X = (ushort)(X + Kernel.Random.Next(0, XPlus));
                                            entity.Y = (ushort)(Y + Kernel.Random.Next(0, YPlus));
                                            if (count2 == 50)
                                                more = false;
                                        }
                                        else
                                            break;
                                    }
                                    Features.DesertTreasurePopulation.Place(this, entity);
                                    if (more)
                                    {
                                        if (Floor[entity.X, entity.Y, MapObjectType.Monster, null])
                                        {
                                            mycount++;
                                            if (mt.Type == 2)
                                            {
                                                this.AddEntity2(entity);
                                            }
                                            else
                                            {
                                                this.AddEntity(entity);
                                            }

                                        }
                                    }
                                }
                                if (pheasant)
                                    Console.WriteLine("[pheasant-spawn] map=" + ID + " x=" + X + " y=" + Y
                                        + " target=" + Amount + " placed=" + (mycount - countBeforeSpawn));
                            }
                        }
                    }
                    catch (Exception e) { Server.SaveException(e); }
                    mycount += Features.SoulGeneralQuest.EnsureRockPopulation(this);
                    if (mycount != 0)
                        Timer = MonsterTimers.Add(this);
                }
            }
        }
        public Tuple<ushort, ushort> RandomCoordinates()
        {
            int times = 10000;
            int x = Kernel.Random.Next(Floor.Bounds.Width), y = Kernel.Random.Next(Floor.Bounds.Height);
            while (times-- > 0)
            {
                if (!Floor[x, y, MapObjectType.Player, null])
                {
                    x = Kernel.Random.Next(Floor.Bounds.Width);
                    y = Kernel.Random.Next(Floor.Bounds.Height);
                }
                else break;
            }
            return new Tuple<ushort, ushort>((ushort)x, (ushort)y);
        }
        public Tuple<ushort, ushort> RandomCoordinates(int _x, int _y, int radius)
        {
            int times = 10000;
            int x = _x + Kernel.Random.Sign() * Kernel.Random.Next(radius),
                y = _y + Kernel.Random.Sign() * Kernel.Random.Next(radius);
            while (times-- > 0)
            {
                if (!Floor[x, y, MapObjectType.Player, null])
                {
                    x = _x + Kernel.Random.Sign() * Kernel.Random.Next(radius);
                    y = _y + Kernel.Random.Sign() * Kernel.Random.Next(radius);
                }
                else break;
            }
            return new Tuple<ushort, ushort>((ushort)x, (ushort)y);
        }
        private static TimerRule<Map> MonsterTimers;
        public static void CreateTimerFactories()
        {
            MonsterTimers = new TimerRule<Map>(_timerCallBack, 500);
        }
        public Time32 LastReload = Time32.Now;
        internal static bool RespawnDue(Time32 now,Time32 death,int seconds)
        {
            // Millisecond deadlines overflow for coordinator-owned encounters'
            // long respawn delays. Compare wrap-safe elapsed time to a wide delay;
            // ordinary intervals also remain valid across the 32-bit clock wrap.
            int elapsed=unchecked(now.TotalMilliseconds-death.TotalMilliseconds);
            // A death recorded after this tick's snapshot is still in the future.
            return elapsed>=0 && (ulong)elapsed>(ulong)Math.Max(0,seconds)*1000UL;
        }
        private static void _timerCallBack(Map map, int time)
        {
            if(map.disposed)return;
            Time32 Now = new Time32(time);
            if (map.PotencyPopulation != null) map.PotencyPopulation.Tick(Now);
            foreach (Entity monster in map.Entities.Values)
            {
                if (monster.Dead)
                {
                    if (Features.GuildBeastEvent.Managed(monster)) continue;
                    if (Features.SpunGoldCombat.IsEncounter(monster)) continue;
                    if (Features.PowerBookCombat.IsEncounter(monster)) continue;
                    if (Features.DailyWorldBosses.IsDaily(monster)) continue;
                    if (Features.ExpeditionEvent.IsEncounter(monster)) continue;
                    if (map.PotencyPopulation != null && map.PotencyPopulation.Contains(monster)) continue;
                    if (RespawnDue(Now,monster.DeathStamp,monster.MonsterInfo.RespawnTime))
                    {
                        monster.X = (ushort)(monster.MonsterInfo.BoundX + Kernel.Random.Next(0, monster.MonsterInfo.BoundCX));
                        monster.Y = (ushort)(monster.MonsterInfo.BoundY + Kernel.Random.Next(0, monster.MonsterInfo.BoundCY));
                        for (int count = 0; count < monster.MonsterInfo.BoundCX * monster.MonsterInfo.BoundCY; count++)
                        {
                            if (!map.Floor[monster.X, monster.Y, MapObjectType.Monster, null])
                            {
                                monster.X = (ushort)(monster.MonsterInfo.BoundX + Kernel.Random.Next(0, monster.MonsterInfo.BoundCX));
                                monster.Y = (ushort)(monster.MonsterInfo.BoundY + Kernel.Random.Next(0, monster.MonsterInfo.BoundCY));
                            }
                            else
                                break;
                        }
                        if (map.Floor[monster.X, monster.Y, MapObjectType.Monster, null] || monster.X == monster.MonsterInfo.BoundX && monster.Y == monster.MonsterInfo.BoundY)
                        {
                            lock (monster)
                            {
                                monster.Hitpoints = monster.MonsterInfo.Hitpoints;
                                monster.ClearFlagsMobs();
                                Revora.Game.Features.DailySoulBall.Revived(monster);
                                Revora.Game.Features.SoulTrialSystem.Revived(monster);
                                Revora.Game.Features.CourierLoopSystem.Revived(monster);
                                Revora.Game.Features.PathHuntSystem.Revived(monster);
                                Revora.Game.Features.AzurePillQuest.Revived(monster);
                                Revora.Game.Features.ConnectorQuest.Revived(monster);
                                Revora.Game.Features.SpunGoldStore.Revived(monster);
                                Revora.Game.Features.ArtisanFeatherQuest.Revived(monster);
                                Revora.Game.Features.MysticRingStore.Revived(monster);
                                Revora.Game.Features.FranticMonkeysStore.Revived(monster);
                                Revora.Game.Features.SoulGeneralStore.Revived(monster);
                                Revora.Game.Features.MadManStore.Revived(monster);
                                Revora.Game.Features.TomboyStore.Revived(monster);
                                Revora.Game.Features.DesertTreasureStore.Revived(monster);
                                Revora.Game.Features.DesertExpeditionStore.Revived(monster);
                                Revora.Game.Features.AttackTalismanStore.Revived(monster);
                                Revora.Game.Features.FamilyFeudStore.Revived(monster);
                                Revora.Game.Features.HeroWingsStore.Revived(monster);
                                Revora.Game.Features.DesertBloodStore.Revived(monster);
                                Revora.Game.Features.AbyssTrialWorld.Revived(monster);
                                Revora.Game.Features.AnniversaryQuest.Revived(monster);
                            }
                            _String stringPacket = new _String(true);
                            stringPacket.UID = monster.UID;
                            stringPacket.Type = _String.Effect;
                            stringPacket.Texts.Add("MBStandard");
                            if (monster.Body == 275 && monster.MapID == 1000)
                            {
                                Kernel.SendWorldMessage(new Message("The Chaos Guardian has appeared in the Desert. Go slay it!", Color.White, Message.Center), Server.GamePool);
                                foreach (var pclint in Server.GamePool)
                                {
                                    pclint.MessageBox("The Chaos Guardian has appeared in the Desert. Do you wish to slay it?",
                                    (p) => { p.Entity.Teleport(1000, 559, 637); }, null, 5);
                                }
                            }
                            if (monster.Body == 4171 && monster.MapID == 1015)
                            {
                                Kernel.SendWorldMessage(new Revora.Network.GamePackets.Message("The Ice Queen has appeared in Bird City. Go slay her!", System.Drawing.Color.White, Revora.Network.GamePackets.Message.Center), Server.GamePool);
                                foreach (var pclint in Server.GamePool)
                                {
                                    pclint.MessageBox("The Ice Queen has appeared in Bird City. Do you wish to slay her?",
                                    (p) => { p.Entity.Teleport(1015, 799, 570); }, null, 5);
                                }
                            }
                            if (monster.Body == 2700 && monster.MapID == 1011)
                            {
                                Kernel.SendWorldMessage(new Revora.Network.GamePackets.Message("GoldenOctopus has appeared in Wind Fort. Go slay it!", System.Drawing.Color.White, Revora.Network.GamePackets.Message.Center), Server.GamePool);
                                foreach (var pclint in Server.GamePool)
                                {
                                    pclint.MessageBox("GoldenOctopus has appeared in Wind Fort. Do you wish to slay it?",
                                    (p) => { p.Entity.Teleport(1011, 216, 318); }, null, 5);
                                }
                            }
                            foreach (Client.GameState client in Server.GamePool)
                            {
                                if (client.Map.ID == map.ID)
                                {
                                    if (Kernel.GetDistance(client.Entity.X, client.Entity.Y, monster.X, monster.Y) < Constants.nScreenDistance)
                                    {
                                        monster.CauseOfDeathIsMagic = false;
                                        monster.SendSpawn(client, false);
                                        client.Send(stringPacket);
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (monster.ToxicFogLeft > 0)
                    {
                        if (monster.MonsterInfo.Boss)
                        {
                            monster.ToxicFogLeft = 0;
                            continue;
                        }
                        if (Now > monster.ToxicFogStamp.AddSeconds(2))
                        {
                            monster.ToxicFogLeft--;
                            monster.ToxicFogStamp = Now;
                            if (monster.Hitpoints > 1)
                            {
                                uint damage = Game.Attacking.Calculate.Percent(monster, monster.ToxicFogPercent);
                                monster.Hitpoints -= damage;
                                Network.GamePackets.SpellUse suse = new Network.GamePackets.SpellUse(true);
                                suse.Attacker = monster.UID;
                                suse.SpellID = 10010;
                                suse.AddTarget(monster.UID, damage, null);
                                monster.MonsterInfo.SendScreen(suse);
                            }
                        }
                    }
                }
            }
        }
        public void SpawnMonsterNearToHero(Database.MonsterInformation mt, Client.GameState client)
        {
            if (mt == null) return;
            mt.RespawnTime = 5;
            Revora.Game.Entity entity = new Revora.Game.Entity(EntityFlag.Monster, false);
            entity.MapObjType = Revora.Game.MapObjectType.Monster;
            entity.MonsterInfo = mt.Copy();
            entity.MonsterInfo.Owner = entity;
            entity.Name = mt.Name;
            entity.MinAttack = mt.MinAttack;
            entity.MaxAttack = entity.MagicAttack = mt.MaxAttack;
            entity.Hitpoints = entity.MaxHitpoints = mt.Hitpoints;
            entity.Body = mt.Mesh;
            entity.Level = mt.Level;
            entity.ApplyMonsterBossHud();
            entity.UID = EntityUIDCounter.Next;
            entity.MapID = ID;
            entity.SendUpdates = true;
            entity.X = (ushort)(client.Entity.X + Kernel.Random.Next(5));
            entity.Y = (ushort)(client.Entity.Y + Kernel.Random.Next(5));
            AddEntity(entity);
            entity.SendSpawn(client);
        }
        public void Spawnthis(Database.MonsterInformation mt, Client.GameState client, ushort ID, ushort x, ushort y)
        {
            if (mt == null) return;
            mt.RespawnTime = 5;
            Entity entity = new Entity(EntityFlag.Monster, false);
            entity.MapObjType = MapObjectType.Monster;
            entity.MonsterInfo = mt.Copy();
            entity.MonsterInfo.Owner = entity;
            entity.Name = mt.Name;
            entity.MinAttack = mt.MinAttack;
            entity.MaxAttack = entity.MagicAttack = mt.MaxAttack;
            entity.Hitpoints = entity.MaxHitpoints = mt.Hitpoints;
            entity.Body = mt.Mesh;
            entity.Level = mt.Level;
            entity.ApplyMonsterBossHud();
            entity.UID = EntityUIDCounter.Next;
            entity.MapID = ID;
            entity.X = x;
            entity.Y = y;
            entity.SendUpdates = true;
            AddEntity(entity);
            entity.SendSpawn(client);
        }
        public Map MakeDynamicMap()
        {
            return CreateDynamic(this.ID, this.Path, true);
        }
        internal static Map CreateDynamic(ushort baseId, string path, bool loadCatalog)
        { return CreateDynamicCore(baseId,path,loadCatalog,false); }
        internal static Map CreateDynamicCompact(ushort baseId,string path)
        { return CreateDynamicCore(baseId,path,false,true); }
        private static Map CreateDynamicCore(ushort baseId, string path, bool loadCatalog, bool compactFloor)
        {
            lock (dynamicAllocation)
            {
                for (int attempt = 0; attempt < 49000; attempt++)
                {
                    uint candidate = DynamicIDs.Next;
                    if (candidate < 11000 || candidate >= 60000)
                        throw new InvalidOperationException("Dynamic map counter is outside its reserved range.");
                    ushort id = (ushort)candidate;
                    if (Kernel.Maps.ContainsKey(id) || Database.DMaps.MapPaths.ContainsKey(id)
                        || Database.MapsTable.MapInformations.ContainsKey(id)) continue;
                    // Construct terrain before publication so failed reads leave no partial map.
                    if(compactFloor && loadCatalog)throw new InvalidOperationException("Private compact floors cannot inherit catalog actors.");
                    var map = new Map(id, baseId, path, false, false, compactFloor);
                    if (map.Floor == null || map.Floor.Bounds.Width <= 0 || map.Floor.Bounds.Height <= 0)
                        throw new InvalidOperationException("Dynamic map terrain is unavailable.");
                    Kernel.Maps.Add(id, map);
                    try
                    {
                        if (loadCatalog) { map.LoadNpcs(); map.LoadMonsters(); map.LoadPortals(); }
                        return map;
                    }
                    catch { map.Dispose(); throw; }
                }
                throw new InvalidOperationException("Dynamic map identifiers exhausted.");
            }
        }
        volatile bool disposed = false;
        public void Dispose()
        {
            IDisposable timer;
            lock (dynamicAllocation)
            {
                Map current;
                if (!disposed && Kernel.Maps.TryGetValue(ID, out current) && ReferenceEquals(current, this))
                    Kernel.Maps.Remove(ID);
                lock(this){disposed = true;timer=Timer;Timer=null;}
            }
            if(timer!=null)timer.Dispose();
        }
    }
    public class Floor
    {
        [Flags]
        public enum DMapPointFlag : byte
        {
            Invalid = 1,
            Monster = 2,
            Item = 4,
            RaceItem = 8,
            Furniture = 16
        }
        public class Size
        {
            public int Width, Height;
            public Size(int width, int height)
            {
                Width = width;
                Height = height;
            }
            public Size()
            {
                Width = 0;
                Height = 0;
            }
        }
        public Size Bounds;
        public DMapPointFlag[,] Locations;
        public uint FloorMapID;
        public Floor(int width, int height, uint mapID)
        {
            FloorMapID = mapID;
            Bounds = new Size(width, height);
            Locations = new DMapPointFlag[width, height];
        }
        public bool this[int x, int y, MapObjectType type, object obj = null]
        {
            get
            {
                if (y >= Bounds.Height || x >= Bounds.Width || x < 0 || y < 0)  return false;
                DMapPointFlag filltype = Locations[x, y];
                if (type == MapObjectType.InvalidCast) return (filltype & (DMapPointFlag.Invalid | DMapPointFlag.Furniture)) != 0;
                if ((filltype & (DMapPointFlag.Invalid | DMapPointFlag.Furniture)) != 0) return false;
                if (type == MapObjectType.Player) return true;
                else if (type == MapObjectType.Monster)
                    return (filltype & DMapPointFlag.Monster) != DMapPointFlag.Monster;
                else if (type == MapObjectType.Item)
                    return (filltype & DMapPointFlag.Item) != DMapPointFlag.Item;
                else if (type == MapObjectType.StaticEntity)
                    return (filltype & DMapPointFlag.RaceItem) != DMapPointFlag.RaceItem;
                return false;
            }
            set
            {
                if (y >= Bounds.Height || x >= Bounds.Width || x < 0 || y < 0)
                    return;
                DMapPointFlag filltype = Locations[x, y];

                if (value)
                {
                    if (type == MapObjectType.InvalidCast)
                        TakeFlag(x, y, DMapPointFlag.Invalid);
                    if (type == MapObjectType.Item)
                        TakeFlag(x, y, DMapPointFlag.Item);
                    if (type == MapObjectType.Monster)
                        TakeFlag(x, y, DMapPointFlag.Monster);
                    if (type == MapObjectType.StaticEntity)
                        TakeFlag(x, y, DMapPointFlag.RaceItem);
                }
                else
                {
                    if (type == MapObjectType.InvalidCast)
                        AddFlag(x, y, DMapPointFlag.Invalid);
                    if (type == MapObjectType.Item)
                        AddFlag(x, y, DMapPointFlag.Item);
                    if (type == MapObjectType.Monster)
                        AddFlag(x, y, DMapPointFlag.Monster);
                    if (type == MapObjectType.StaticEntity)
                        AddFlag(x, y, DMapPointFlag.RaceItem);
                }
            }
        }
        public DMapPointFlag AddFlag(int x, int y, DMapPointFlag extraFlag)
        {
            Locations[x, y] |= extraFlag;
            return Locations[x, y];
        }
        public DMapPointFlag TakeFlag(int x, int y, DMapPointFlag extraFlag)
        {
            Locations[x, y] &= ~extraFlag;
            return Locations[x, y];
        }
    }
    public enum MapObjectType
    {
        SobNpc,
        Npc,
        Item,
        Monster,
        Player,
        Nothing,
        InvalidCast,
        StaticEntity,
        FloorSpell
    }
    public class Portal
    {
        public Portal(ushort CurrentMapID, ushort CurrentX, ushort CurrentY, ushort DestinationMapID, ushort DestinationX, ushort DestinationY)
        {
            this.CurrentMapID = CurrentMapID;
            this.CurrentX = CurrentX;
            this.CurrentY = CurrentY;
            this.DestinationMapID = DestinationMapID;
            this.DestinationX = DestinationX;
            this.DestinationY = DestinationY;
        }
        public Portal()
        {

        }
        public ushort CurrentMapID
        {
            get;
            set;
        }
        public ushort CurrentX
        {
            get;
            set;
        }
        public ushort CurrentY
        {
            get;
            set;
        }
        public ushort DestinationMapID
        {
            get;
            set;
        }
        public ushort DestinationX
        {
            get;
            set;
        }
        public ushort DestinationY
        {
            get;
            set;
        }
    }
}
