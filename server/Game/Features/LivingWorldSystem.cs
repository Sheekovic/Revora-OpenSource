using System;
using System.Collections.Generic;
using System.Drawing;
using Revora.Client;
using Revora.Database;
using Revora.Interfaces;
using Revora.Network.GamePackets;
using Revora.Game.ConquerStructures;

namespace Revora.Game.Features
{
    public static partial class LivingWorldSystem
    {
        public const int RegionalCap = 4;
        private const uint FirstUid = 1200001;
        private static readonly object Gate = new object();
        private static readonly object TickGate = new object();
        private static readonly List<Wanderer> Actors = new List<Wanderer>();
        private static readonly Dictionary<uint, Wanderer> ByUid = new Dictionary<uint, Wanderer>();
        private static readonly Dictionary<uint, Wanderer> HiredBy = new Dictionary<uint, Wanderer>();
        private static readonly HashSet<string> UsedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private const byte DutyWander = 0;
        private const byte DutyFollow = 1;
        private const byte DutyWait = 2;
        private static uint NextUid = FirstUid;
        private static volatile bool Ready;

        private static readonly CityHub[] Cities = new CityHub[]
        {
            new CityHub(1002, 408, 330, 4, 36),
            new CityHub(1000, 500, 645, 2, 24),
            new CityHub(1011, 195, 260, 2, 24),
            new CityHub(1015, 717, 573, 2, 24),
            new CityHub(1020, 566, 563, 2, 24),
            new CityHub(1036, 292, 236, 2, 18)
        };

        private static readonly string[] GivenNames = new string[]
        {
            "Alden", "Briar", "Cyrus", "Della", "Ewan", "Fawn", "Gideon", "Hazel",
            "Ivo", "Juno", "Keir", "Lark", "Milo", "Nyla", "Orin", "Piper",
            "Quill", "Rhea", "Silas", "Tess", "Ulric", "Vesper", "Willa", "Xander",
            "Yule", "Zinnia", "Ash", "Bex", "Cal", "Dove", "Ember", "Flint"
        };

        private static readonly string[] Lines = new string[]
        {
            "Safe travels.",
            "The roads were kind this morning.",
            "I am only passing through.",
            "Keep your blade oiled.",
            "A bowl of stew and I am myself again.",
            "Watch the alleys after dusk.",
            "If you hunt, hunt with friends.",
            "The city still stands. That is enough."
        };

        private static readonly string[] HireAck = new string[]
        {
            "I will walk with you. Whisper wait or dismiss when you need me still.",
            "Lead on. I keep to your shadow.",
            "Hired. I follow, I fight nearby pests. I do not carry your loot."
        };

        private static readonly ClassKit[] Kits = new ClassKit[]
        {
            new ClassKit(15, 118309, 120269, 130309, 410439, 420439, 150269, 160249),
            new ClassKit(25, 111309, 120269, 131309, 480439, 900309, 150269, 160249),
            new ClassKit(45, 113309, 120269, 133309, 500429, 1050002, 150269, 160249),
            new ClassKit(55, 123309, 120269, 135309, 601439, 601439, 150269, 160249),
            new ClassKit(65, 143309, 120269, 136309, 610439, 610439, 150269, 160249),
            new ClassKit(75, 144309, 120269, 139309, 611439, 612439, 150269, 160249),
            new ClassKit(135, 114309, 121269, 134309, 421439, 619439, 152279, 160249),
            new ClassKit(145, 114309, 121269, 134309, 561439, 619439, 152279, 160249)
        };

        private struct CityHub
        {
            public readonly ushort MapId;
            public readonly ushort X;
            public readonly ushort Y;
            public readonly byte Count;
            public readonly byte Radius;

            public CityHub(ushort mapId, ushort x, ushort y, byte count, byte radius)
            {
                MapId = mapId;
                X = x;
                Y = y;
                Count = count;
                Radius = radius;
            }
        }

        private struct ClassKit
        {
            public readonly byte Class;
            public readonly uint Head;
            public readonly uint Necklace;
            public readonly uint Armor;
            public readonly uint Weapon;
            public readonly uint Shield;
            public readonly uint Ring;
            public readonly uint Boots;

            public ClassKit(byte cls, uint head, uint necklace, uint armor, uint weapon, uint shield, uint ring, uint boots)
            {
                Class = cls;
                Head = head;
                Necklace = necklace;
                Armor = armor;
                Weapon = weapon;
                Shield = shield;
                Ring = ring;
                Boots = boots;
            }
        }

        private sealed class Wanderer
        {
            public readonly object Sync = new object();
            public readonly CompanionNavigation Navigation = new CompanionNavigation();
            public CompanionStance Stance;
            public ushort HoldX, HoldY;
            public uint TargetUid;
            public uint AggressorUid;
            public ushort FightMap, FightX, FightY;
            public DateTime AngerUntil, NextReaction, NextEvasion;
            public volatile AssistedTarget OwnerTarget;
            public DateTime NextCombat, LastResource, SeparatedSince;
            public DateTime LastCommand;
            public bool Retreating, StayBack, Repositioned;
            public string Name;
            public byte Class;
            public byte Level;
            public byte Reborn;
            public ushort MapId;
            public ushort HomeMapId;
            public ushort HomeX;
            public ushort HomeY;
            public ushort DestX;
            public ushort DestY;
            public uint OwnerUid;
            public byte Duty;
            public ushort Body;
            public ushort Face;
            public ushort Hair;
            public uint Head;
            public uint Necklace;
            public uint Armor;
            public uint Weapon;
            public uint Shield;
            public uint Ring;
            public uint Boots;
            public GameState State;
            public DateTime LastAct;
            public DateTime LastGreet;
            public DateTime RestUntil;
            public DateTime ReviveAt;
            public uint LastPlayer;
            public bool Sitting;
            public ushort CombatSkill;
            public short CombatRange;
        }

        private sealed class AssistedTarget
        {
            public uint Uid;
            public ushort MapId;
            public DateTime Until;
        }

        public static void Create()
        {
            // The world timer is already running during server initialization.
            // Population must share its owner before readiness becomes visible.
            lock (TickGate)
            {
                if (Ready) return;
                FillCities();
                Ready = true;
                Console.WriteLine("LivingWorld adventurers: " + Actors.Count);
            }
        }

        public static void Tick()
        {
            if (!Ready) return;
            // Timer callbacks must not overlap. Network commands lock only their actor.
            if (!System.Threading.Monitor.TryEnter(TickGate)) return;
            try
            {
                FillCities();
                Wanderer[] actors;
                lock (Gate) actors = Actors.ToArray();
                DateTime now = DateTime.UtcNow;
                foreach (Wanderer actor in actors)
                    lock (actor.Sync) Drive(actor, now);
            }
            finally { System.Threading.Monitor.Exit(TickGate); }
        }

        public static bool IsAdventurer(Entity entity)
        {
            if (entity == null) return false;
            lock (Gate)
            {
                if (ByUid.ContainsKey(entity.UID))
                    return true;
            }
            return IsAdventurerName(entity.Name);
        }

        public static bool IsAdventurerName(string name)
        {
            return name != null && name.StartsWith("[Adv]");
        }

        public static void OnPlayerLogout(uint uid)
        {
            Wanderer actor;
            lock (Gate)
            {
                if (!HiredBy.TryGetValue(uid, out actor))
                    return;
            }
            lock (actor.Sync)
                if (actor.OwnerUid == uid) Release(actor, null);
        }

        // Called only by the existing accepted-damage path; never trusts a raw client target.
        public static void ObservePlayerAttack(Entity attacker, Entity target, uint damage)
        {
            if (damage == 0 || attacker == null || attacker.Owner == null || attacker.Owner.Fake
                || target == null || target.EntityFlag != EntityFlag.Monster) return;
            lock (Gate)
            {
                Wanderer actor;
                if (HiredBy.TryGetValue(attacker.UID, out actor))
                    actor.OwnerTarget = new AssistedTarget { Uid = target.UID, MapId = target.MapID,
                        Until = DateTime.UtcNow.AddSeconds(6) };
            }
        }

        public static bool TryHandleWhisper(GameState from, Message message)
        {
            if (from == null || from.Entity == null || from.Fake || message == null || string.IsNullOrEmpty(message._To))
                return false;
            Wanderer actor = FindByName(message._To.Trim());
            if (actor == null) return false;
            lock (actor.Sync)
            {
                DateTime now = DateTime.UtcNow;
                if (actor.OwnerUid == 0 || actor.OwnerUid == from.Entity.UID)
                {
                    if (now - actor.LastCommand < TimeSpan.FromMilliseconds(500)) return true;
                    actor.LastCommand = now;
                }
                string text = message.__Message == null ? "" : message.__Message.Trim();
                string cmd = text.ToLowerInvariant();
                if (cmd == "hire" || cmd == "follow" || cmd == "follow me" || cmd == "come" || cmd == "come with me" || cmd == "join" || cmd == "join me")
                    Hire(from, actor);
                else if (cmd == "wait" || cmd == "stay" || cmd == "stop" || cmd == "hold" || cmd == "hold here")
                    Wait(from, actor);
                else if (cmd == "assist" || cmd == "protect" || cmd == "protect me" || cmd == "passive"
                    || cmd == "stay back" || cmd == "retreat")
                    SetTactics(from, actor, cmd);
                else if (cmd == "dismiss" || cmd == "leave" || cmd == "go" || cmd == "bye" || cmd == "goodbye" || cmd == "go away")
                    Dismiss(from, actor);
                else if (cmd == "help" || cmd == "?" || cmd == "commands")
                    Reply(from, actor, "Commands: hire, follow, assist, protect me, passive, hold here, stay back, retreat, dismiss. One companion per player; no loot collection.");
                else
                    Reply(from, actor, Lines[Kernel.Random.Next(0, Lines.Length)] + " Whisper hire if you want company.");
                return true;
            }
        }

        public static void OnAdventurerKilled(Entity body, Entity killer)
        {
            Wanderer actor;
            lock (Gate)
            {
                if (body == null || !ByUid.TryGetValue(body.UID, out actor)) return;
            }
            lock (actor.Sync)
            {
                if (body.Hitpoints > 0) body.Hitpoints = 0;
                ClearIntent(actor);
                Data die = new Data(true);
                die.UID = body.UID;
                die.ID = Data.Die;
                Broadcast(body, die);
                actor.ReviveAt = DateTime.UtcNow.AddSeconds(10);
            }
        }

        private static void FillCities()
        {
            for (int i = 0; i < Cities.Length; i++)
            {
                CityHub hub = Cities[i];
                if (!Kernel.Maps.ContainsKey(hub.MapId)) continue;
                int have = 0;
                for (int n = 0; n < Actors.Count; n++)
                {
                    if (Actors[n].MapId == hub.MapId && Actors[n].OwnerUid == 0)
                        have++;
                }
                while (have < hub.Count)
                {
                    // Hired actors still count toward their region's capacity.
                    int regional = 0;
                    for (int n = 0; n < Actors.Count; n++)
                        if (Actors[n].HomeMapId == hub.MapId) regional++;
                    if (regional >= RegionalCap || Actors.Count >= Cities.Length * RegionalCap) break;
                    if (!SpawnOne(hub))
                        break;
                    have++;
                }
            }
        }

        private static bool SpawnOne(CityHub hub)
        {
            Map map = Kernel.Maps[hub.MapId];
            ushort x;
            ushort y;
            if (!FindTile(map, hub.X, hub.Y, hub.Radius, out x, out y))
            {
                x = hub.X;
                y = hub.Y;
            }
            ClassKit kit = Kits[Kernel.Random.Next(0, Kits.Length)];
            bool female = Kernel.Random.Next(0, 2) == 1;
            Wanderer actor = new Wanderer();
            actor.Name = NextName();
            actor.Class = kit.Class;
            actor.Level = 140;
            actor.Reborn = 2;
            actor.MapId = hub.MapId;
            actor.HomeMapId = hub.MapId;
            actor.HomeX = x;
            actor.HomeY = y;
            actor.OwnerUid = 0;
            actor.Duty = DutyWander;
            actor.DestX = x;
            actor.DestY = y;
            actor.Body = female ? (ushort)2001 : (ushort)1003;
            actor.Face = female ? (ushort)Kernel.Random.Next(201, 221) : (ushort)Kernel.Random.Next(21, 36);
            actor.Hair = (ushort)Kernel.Random.Next(300, 321);
            actor.Head = kit.Head;
            actor.Necklace = kit.Necklace;
            actor.Armor = kit.Armor;
            actor.Weapon = kit.Weapon;
            actor.Shield = kit.Shield;
            actor.Ring = kit.Ring;
            actor.Boots = kit.Boots;
            AssignCombat(actor);
            actor.LastAct = DateTime.UtcNow;
            actor.LastGreet = DateTime.MinValue;
            lock (Gate)
                Actors.Add(actor);
            return Materialize(actor);
        }

        private static string NextName()
        {
            for (int i = 0; i < GivenNames.Length; i++)
            {
                int pick = Kernel.Random.Next(0, GivenNames.Length);
                string candidate = "[Adv] " + GivenNames[pick];
                if (UsedNames.Add(candidate))
                    return candidate;
            }
            int serial = UsedNames.Count + 1;
            string fallback = "[Adv] W" + serial;
            while (!UsedNames.Add(fallback))
            {
                serial++;
                fallback = "[Adv] W" + serial;
            }
            return fallback;
        }

        private static bool Materialize(Wanderer actor)
        {
            if (!Kernel.Maps.ContainsKey(actor.MapId)) return false;
            uint uid;
            lock (Gate) uid = NextUid++;
            GameState gs = new GameState(null);
            gs.Fake = true;
            gs.Account = new AccountTable(null);
            gs.Account.EntityID = uid;
            gs.ReadyToPlay();
            gs.Entity = new Entity(EntityFlag.Player, false);
            gs.Entity.Owner = gs;
            gs.Entity.SendUpdates = false;
            gs.Entity.FullyLoaded = true;
            gs.Entity.UID = uid;
            gs.Entity.Name = actor.Name;
            gs.Entity.Spouse = "Adventurer";
            gs.Entity.ClanName = "Wanderer";
            gs.Entity.Face = actor.Face;
            gs.Entity.Body = actor.Body;
            gs.Entity.HairStyle = actor.Hair;
            gs.Entity.Action = Enums.ConquerAction.None;
            gs.Entity.Facing = Enums.ConquerAngle.South;
            gs.Entity.Level = actor.Level;
            gs.Entity.Reborn = actor.Reborn;
            gs.Entity.Class = actor.Class;
            gs.Entity.FirstRebornClass = actor.Reborn >= 1 ? actor.Class : (byte)0;
            gs.Entity.SecondRebornClass = 0;
            gs.Entity.PKMode = Enums.PKMode.Peace;
            gs.Entity.MaxHitpoints = (uint)(3500 + actor.Level * 30);
            gs.Entity.Hitpoints = gs.Entity.MaxHitpoints;
            gs.Entity.MinAttack = (uint)(180 + actor.Level * 3);
            gs.Entity.MaxAttack = (uint)(320 + actor.Level * 5);
            gs.Entity.Defence = (ushort)(300 + actor.Level * 4);
            gs.Entity.MagicDefence = (ushort)(180 + actor.Level * 2);
            gs.Entity.Stamina = 100;
            gs.Entity.MaxMana = 800;
            gs.Entity.Mana = 800;
            gs.Entity.NobilityRank = ConquerStructures.NobilityRank.Serf;
            gs.Attackable = true;
            gs.Variables = new VariableVault();
            gs.Friends = new SafeDictionary<uint, ConquerStructures.Society.Friend>();
            gs.Enemy = new SafeDictionary<uint, ConquerStructures.Society.Enemy>();
            gs.Spells = new SafeDictionary<ushort, ISkill>();
            gs.Proficiencies = new SafeDictionary<ushort, IProf>();
            gs.ChiPowers = new List<ConquerStructures.ChiPowerStructure>();
            gs.Retretead_ChiPowers = new List<ConquerStructures.ChiPowerStructure>();
            gs.ChiData = new ChiTable.ChiData() { Name = actor.Name, UID = uid, Powers = gs.ChiPowers };
            gs.NobilityInformation = new ConquerStructures.NobilityInformation();
            gs.NobilityInformation.EntityUID = uid;
            gs.NobilityInformation.Name = actor.Name;
            gs.NobilityInformation.Rank = ConquerStructures.NobilityRank.Serf;
            gs.NobilityInformation.Position = -1;
            gs.NobilityInformation.Gender = (byte)(actor.Body >= 2000 ? 0 : 1);
            gs.NobilityInformation.Mesh = gs.Entity.Mesh;
            gs.TeamArenaStatistic = new TeamQualifierStatistic(true);
            gs.TeamArenaStatistic.EntityID = uid;
            gs.TeamArenaStatistic.Name = actor.Name;
            gs.TeamArenaStatistic.Level = actor.Level;
            gs.TeamArenaStatistic.Class = actor.Class;
            gs.TeamArenaStatistic.Model = gs.Entity.Mesh;
            gs.TeamArenaStatistic.Status = TeamQualifierStatistic.NotSignedUp;
            gs.ArenaStatistic = new QualifierStatistic(true);
            gs.ArenaStatistic.EntityID = uid;
            gs.ArenaStatistic.Name = actor.Name;
            gs.ArenaStatistic.Level = actor.Level;
            gs.ArenaStatistic.Class = actor.Class;
            gs.ArenaStatistic.Model = gs.Entity.Mesh;
            gs.ArenaStatistic.Status = QualifierStatistic.NotSignedUp;
            gs.ChampionStats = new ChampionStatistic(true);
            gs.ChampionStats.UID = uid;
            gs.ChampionStats.Name = actor.Name;
            gs.ChampionStats.Level = actor.Level;
            gs.ChampionStats.Class = actor.Class;
            gs.ChampionStats.Model = gs.Entity.Mesh;
            Equip(gs, ConquerItem.Head, actor.Head);
            Equip(gs, ConquerItem.Necklace, actor.Necklace);
            Equip(gs, ConquerItem.Armor, actor.Armor);
            Equip(gs, ConquerItem.RightWeapon, actor.Weapon);
            if (actor.Shield != 0)
                Equip(gs, ConquerItem.LeftWeapon, actor.Shield);
            Equip(gs, ConquerItem.Ring, actor.Ring);
            Equip(gs, ConquerItem.Boots, actor.Boots);
            Equip(gs, ConquerItem.Fan, 201009);
            Equip(gs, ConquerItem.Tower, 202009);
            Equip(gs, ConquerItem.Steed, 300000);
            Equip(gs, ConquerItem.SteedCrop, 203009);
            Equip(gs, ConquerItem.Wing, 204009);
            GiveChi(gs);
            GiveJiangHu(gs, actor);
            GiveClassSkills(gs, actor);
            try
            {
                gs.LoadItemStats();
            }
            catch
            {
            }
            PaintLook(gs, actor);
            gs.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Dead | (ulong)FlagsUpdate.Flags.Ghost);
            // Revival retains a valid position on the current map, including a held anchor.
            ushort spawnX = actor.State == null ? actor.HomeX : actor.State.Entity.X;
            ushort spawnY = actor.State == null ? actor.HomeY : actor.State.Entity.Y;
            Map spawnMap;
            if (!Kernel.Maps.TryGetValue(actor.MapId, out spawnMap)
                || !FindTile(spawnMap, spawnX, spawnY, 2, out spawnX, out spawnY)) return false;
            gs.Entity.Hitpoints = gs.Entity.MaxHitpoints;
            gs.Entity.MaxMana = (ushort)Math.Max(800, (int)gs.Entity.MaxMana);
            gs.Entity.Mana = gs.Entity.MaxMana;
            gs.Entity.X = spawnX;
            gs.Entity.Y = spawnY;
            actor.Navigation.Reset();
            actor.TargetUid = 0;
            actor.OwnerTarget = null;
            actor.SeparatedSince = DateTime.MinValue;
            if (actor.Duty != DutyWait)
            {
                actor.HoldX = gs.Entity.X;
                actor.HoldY = gs.Entity.Y;
            }
            gs.Entity.MapObjType = MapObjectType.Player;
            gs.BindWorldMap(actor.MapId);
            gs.Entity.SendUpdates = true;
            PaintLook(gs, actor);
            Entity previous = null;
            GameState previousState = null;
            lock (Gate)
            {
                if (actor.State != null && actor.State.Entity != null)
                {
                    previous = actor.State.Entity;
                    previousState = actor.State;
                    ByUid.Remove(previous.UID);
                }
            }
            if (previousState != null)
                SessionInstall.TryRemoveFake(previous.UID, previousState);
            if (!SessionInstall.TryInstallFake(uid, gs)) return false;
            lock (Gate)
            {
                ByUid[uid] = actor;
                actor.State = gs;
            }
            if (previous != null)
            {
                if (previous.Owner != null && previous.Owner.Map != null)
                    previous.Owner.Map.RemoveEntity(previous);
                Data remove = new Data(true);
                remove.UID = previous.UID;
                remove.ID = Data.RemoveEntity;
                Broadcast(previous, remove);
            }
            if (gs.Map != null)
                gs.Map.AddEntity(gs.Entity);
            BroadcastSpawn(gs.Entity);
            return true;
        }

        private static void Equip(GameState gs, byte position, uint itemId)
        {
            if (itemId == 0) return;
            try
            {
                if (!ConquerItemInformation.BaseInformations.ContainsKey(itemId))
                    return;
                ConquerItem item = new ConquerItem(true);
                item.ID = itemId;
                item.UID = ConquerItem.ItemUID.Next;
                item.Position = position;
                bool arrow = ItemHandler.IsArrow(itemId);
                bool talisman = position == ConquerItem.Fan || position == ConquerItem.Tower || position == ConquerItem.Wing;
                item.Plus = (byte)(arrow ? 0 : 12);
                item.Bless = (byte)(talisman ? 1 : 0);
                item.Color = Enums.Color.Black;
                item.SocketOne = Enums.Gem.NoSocket;
                item.SocketTwo = Enums.Gem.NoSocket;
                if (position == ConquerItem.Fan || position == ConquerItem.Tower || position == ConquerItem.Wing)
                {
                    item.SocketOne = Enums.Gem.SuperThunderGem;
                    item.SocketTwo = Enums.Gem.SuperGloryGem;
                }
                item.Durability = 1000;
                item.MaximDurability = 1000;
                ConquerItemBaseInformation info;
                if (ConquerItemInformation.BaseInformations.TryGetValue(itemId, out info) && info != null && info.Durability > 0)
                {
                    item.Durability = info.Durability;
                    item.MaximDurability = info.Durability;
                }
                uint soul = arrow ? 0 : SoulFor(position, itemId, gs.Entity.Class);
                if (soul != 0)
                {
                    ItemAdding.Purification_ purify = new ItemAdding.Purification_();
                    purify.Available = true;
                    purify.ItemUID = item.UID;
                    purify.PurificationItemID = soul;
                    purify.PurificationLevel = 6;
                    purify.PurificationDuration = 0;
                    purify.AddedOn = DateTime.Now;
                    item.Purification = purify;
                }
                item.Mode = Enums.ItemMode.Default;
                item.IsWorn = true;
                gs.Equipment.Add(item, Enums.ItemUse.None);
                gs.Equipment.UpdateItemview(item);
            }
            catch
            {
            }
        }
        private static uint SoulFor(byte position, uint itemId, byte cls)
        {
            bool taoist = cls == 135 || cls == 145;
            switch (position)
            {
                case ConquerItem.Head:
                    return taoist ? 820072u : 820071u;
                case ConquerItem.Armor:
                    return taoist ? 822055u : 822053u;
                case ConquerItem.Necklace:
                    return taoist ? 821032u : 821031u;
                case ConquerItem.Ring:
                    return taoist ? 823056u : 823055u;
                case ConquerItem.Boots:
                    return 824017u;
                case ConquerItem.LeftWeapon:
                    uint left = itemId / 1000;
                    if (left == 900) return 800421u;
                    if (left == 619) return 801103u;
                    if (left == 612) return 800804u;
                    if (left == 601) return 800017u;
                    if (left == 610) return 800722u;
                    if (left == 420) return 800000u;
                    return 0;
                case ConquerItem.RightWeapon:
                    uint type = itemId / 1000;
                    if (type == 410 || type == 420) return 800000u;
                    if (type == 421) return 800513u;
                    if (type == 480) return 800110u;
                    if (type == 500) return 800616u;
                    if (type == 560 || type == 561) return 800320u;
                    if (type == 601) return 800017u;
                    if (type == 610) return 800722u;
                    if (type == 611) return 800809u;
                    if (type == 612) return 800804u;
                    if (type == 619) return 801103u;
                    return taoist ? 800513u : 800000u;
                default:
                    return 0;
            }
        }

        private static void PaintLook(GameState gs, Wanderer actor)
        {
            bool female = actor.Body >= 2000;
            gs.Entity.CharBuffer._Mesh = female ? 2562005u : 371006u;
            gs.Entity.CharBuffer._HairStyle = actor.Hair;
            gs.Entity.CharBuffer._Level = actor.Level;
            gs.Entity.CharBuffer._MonsterLevel = actor.Level;
            gs.Entity.CharBuffer._Head = actor.Head;
            gs.Entity.CharBuffer._Armor = actor.Armor;
            gs.Entity.CharBuffer._RightWeapon = actor.Weapon;
            gs.Entity.CharBuffer._LeftWeapon = actor.Shield;
            gs.Entity.CharBuffer._Garment = 0;
            gs.Entity.CharBuffer._HeadSoul = 0;
            gs.Entity.CharBuffer._ArmorSoul = 0;
            gs.Entity.CharBuffer._LeftWeaponSoul = 0;
            gs.Entity.CharBuffer._RightWeaponSoul = 0;
            gs.Entity.CharBuffer._UID = gs.Entity.UID;
            gs.Entity.CharBuffer._X = gs.Entity.X;
            gs.Entity.CharBuffer._Y = gs.Entity.Y;
            gs.Entity.CharBuffer._Action = Enums.ConquerAction.None;
            gs.Entity.CharBuffer._Facing = (byte)Enums.ConquerAngle.South;
            gs.Entity.CharBuffer._Wing = 204009;
            gs.Entity.CharBuffer._WingPlus = 12;
            gs.Entity.CharBuffer._JiangHuActive = 1;
            gs.Entity.CharBuffer._JiangHuTalen = 5;
            gs.Entity.CharBuffer._CountryCode = 0;
            gs.Entity.CharBuffer._Steed = 300000;
            gs.Entity.CharBuffer._SteedPlus = 12;
            gs.Entity.CharBuffer._Boss = 0;
        }

        private static void GiveChi(GameState gs)
        {
            try
            {
                gs.ChiPowers.Clear();
                gs.ChiPowers.Add(MakeChi(Enums.ChiPowerType.Dragon, Enums.ChiAttribute.CriticalStrike, Enums.ChiAttribute.AddAttack, Enums.ChiAttribute.HPAdd, Enums.ChiAttribute.Breakthrough));
                gs.ChiPowers.Add(MakeChi(Enums.ChiPowerType.Phoenix, Enums.ChiAttribute.SkillCriticalStrike, Enums.ChiAttribute.AddMagicAttack, Enums.ChiAttribute.Immunity, Enums.ChiAttribute.MagicDamageIncrease));
                gs.ChiPowers.Add(MakeChi(Enums.ChiPowerType.Tiger, Enums.ChiAttribute.PhysicalDamageIncrease, Enums.ChiAttribute.AddAttack, Enums.ChiAttribute.Counteraction, Enums.ChiAttribute.HPAdd));
                gs.ChiPowers.Add(MakeChi(Enums.ChiPowerType.Turtle, Enums.ChiAttribute.PhysicalDamageDecrease, Enums.ChiAttribute.MagicDamageDecrease, Enums.ChiAttribute.Immunity, Enums.ChiAttribute.AddMagicDefense));
                gs.ChiData.Powers = gs.ChiPowers;
            }
            catch
            {
            }
        }

        private static ConquerStructures.ChiPowerStructure MakeChi(Enums.ChiPowerType power, Enums.ChiAttribute a, Enums.ChiAttribute b, Enums.ChiAttribute c, Enums.ChiAttribute d)
        {
            ConquerStructures.ChiPowerStructure chi = new ConquerStructures.ChiPowerStructure(power);
            chi.Attributes[0] = new ConquerStructures.ChiAttribute(a, (ushort)Enums.ChiMaxValues(a));
            chi.Attributes[1] = new ConquerStructures.ChiAttribute(b, (ushort)Enums.ChiMaxValues(b));
            chi.Attributes[2] = new ConquerStructures.ChiAttribute(c, (ushort)Enums.ChiMaxValues(c));
            chi.Attributes[3] = new ConquerStructures.ChiAttribute(d, (ushort)Enums.ChiMaxValues(d));
            chi.CalculatePoints();
            return chi;
        }

        private static void GiveJiangHu(GameState gs, Wanderer actor)
        {
            try
            {
                Revora.Game.JiangHu jiang = new Revora.Game.JiangHu(0);
                jiang.UID = gs.Entity.UID;
                jiang.OwnName = actor.Name;
                jiang.JiangName = actor.Name;
                jiang.Level = 9;
                jiang.Talent = 5;
                jiang.Stage = 9;
                jiang.Star = 9;
                jiang.OnJiangHu = true;
                jiang.FreeCourse = 10000000;
                for (byte stage = 1; stage <= 9; stage++)
                {
                    Revora.Game.JiangHu.JiangStages stager = jiang.Stagers[stage - 1];
                    stager.Activate = true;
                    List<byte> types = null;
                    if (!Revora.Database.JiangHu.CultivateStatus.TryGetValue(stage, out types) || types == null || types.Count == 0)
                        continue;
                    for (int star = 0; star < 9; star++)
                    {
                        byte typ = types[star % types.Count];
                        byte level = 6;
                        ushort uid = Revora.Database.JiangHu.ValueToRoll(typ, level);
                        if (!Revora.Database.JiangHu.Attributes.ContainsKey(uid))
                        {
                            level = 5;
                            uid = Revora.Database.JiangHu.ValueToRoll(typ, level);
                        }
                        if (!Revora.Database.JiangHu.Attributes.ContainsKey(uid))
                            continue;
                        stager.Stars[star].Activate = true;
                        stager.Stars[star].Level = level;
                        stager.Stars[star].Typ = (Revora.Game.JiangHu.JiangStages.AtributesType)typ;
                        stager.Stars[star].UID = uid;
                    }
                }
                gs.Entity.MyJiang = jiang;
                gs.Entity.TalentStaus = 5;
                gs.Entity.JiangActive = true;
            }
            catch
            {
            }
        }

        private static void Drive(Wanderer actor, DateTime now)
        {
            if (actor.State == null || actor.State.Entity == null)
            {
                if (now >= actor.ReviveAt)
                    Materialize(actor);
                return;
            }
            Entity body = actor.State.Entity;
            if (body.Dead)
            {
                if (now >= actor.ReviveAt)
                    Materialize(actor);
                return;
            }
            if (now - actor.LastAct < TimeSpan.FromMilliseconds(420)) return;
            actor.LastAct = now;
            if (now - actor.LastResource >= TimeSpan.FromSeconds(2))
            {
                actor.LastResource = now;
                body.Mana = (ushort)Math.Min(body.MaxMana, body.Mana + 40);
                body.Stamina = (byte)Math.Min(100, body.Stamina + 10);
                if (body.Hitpoints < body.MaxHitpoints && now > actor.NextCombat.AddSeconds(10) && !HasThreat(body))
                    body.Hitpoints += Math.Min(Math.Max(1, body.MaxHitpoints / 100), body.MaxHitpoints - body.Hitpoints);
            }
            if (actor.AggressorUid != 0) return; // The fast retaliation timer owns this fight.
            if (actor.OwnerUid != 0)
            {
                DriveHired(actor, body, now);
                return;
            }
            Entity pest = CityGuardSystem.FindNearbyMonster(actor.MapId, body.X, body.Y, 12);
            if (pest != null && Kernel.GetDistance(actor.HomeX, actor.HomeY, pest.X, pest.Y) <= RadiusOf(actor.MapId) + 6)
            {
                if (actor.Sitting)
                    Stand(actor, body);
                short dist = Kernel.GetDistance(body.X, body.Y, pest.X, pest.Y);
                if (dist <= actor.CombatRange)
                    Strike(actor, body, pest);
                else
                    MoveToward(body, pest.X, pest.Y);
                return;
            }
            if (actor.Sitting)
            {
                if (now < actor.RestUntil) return;
                Stand(actor, body);
                return;
            }
            if (Greet(actor, body, now))
                return;
            int humans = HumansOn(actor.MapId);
            if (humans >= 18 && Kernel.Random.Next(0, 100) < 40)
            {
                Sit(actor, body, now);
                return;
            }
            if (body.X == actor.DestX && body.Y == actor.DestY)
            {
                int roll = Kernel.Random.Next(0, 100);
                if (roll < 22)
                {
                    Sit(actor, body, now);
                    return;
                }
                if (roll < 35)
                    return;
                PickDestination(actor);
                return;
            }
            MoveToward(body, actor.DestX, actor.DestY);
        }

        private static void AssignCombat(Wanderer actor)
        {
            switch (actor.Class)
            {
                case 15:
                    actor.CombatSkill = 0;
                    actor.CombatRange = 2;
                    break;
                case 25:
                    actor.CombatSkill = 0;
                    actor.CombatRange = 2;
                    break;
                case 45:
                    actor.CombatSkill = 0;
                    actor.CombatRange = 8;
                    break;
                case 55:
                    actor.CombatSkill = 0;
                    actor.CombatRange = 2;
                    break;
                case 65:
                    actor.CombatSkill = 0;
                    actor.CombatRange = 2;
                    break;
                case 75:
                    actor.CombatSkill = 0;
                    actor.CombatRange = 2;
                    break;
                case 135:
                    actor.CombatSkill = 1000;
                    actor.CombatRange = 9;
                    break;
                case 145:
                    actor.CombatSkill = 1002;
                    actor.CombatRange = 9;
                    break;
                default:
                    actor.CombatSkill = 0;
                    actor.CombatRange = 2;
                    break;
            }
        }

        private static void GiveClassSkills(GameState gs, Wanderer actor)
        {
            try
            {
                GiveSkill(gs, 9876);
                switch (actor.Class)
                {
                    case 15:
                        GiveSkills(gs, 1110, 1015, 1115, 1270, 1190, 3050, 10315);
                        GiveProficiency(gs, 410);
                        GiveProficiency(gs, 420);
                        break;
                    case 25:
                        GiveSkills(gs, 1020, 1025, 1040, 1050, 1051, 1260, 10470, 3060, 10311);
                        GiveProficiency(gs, 480);
                        GiveProficiency(gs, 900);
                        break;
                    case 45:
                        GiveSkills(gs, 8000, 8001, 8002, 8003, 8030, 9000, 10313);
                        GiveProficiency(gs, 500);
                        break;
                    case 55:
                        GiveSkills(gs, 6000, 6001, 6002, 6003, 6004, 6010, 6011);
                        GiveProficiency(gs, 601);
                        break;
                    case 65:
                        GiveSkills(gs, 10490, 10390, 10415, 10381, 10400, 10425, 10430, 10395, 10410, 10420, 10421, 10422, 10423, 10424, 10405);
                        GiveProficiency(gs, 610);
                        break;
                    case 75:
                        GiveSkills(gs, 11030, 11040, 11050, 11060, 11070, 11100, 11110, 11120, 11130, 11140, 11170, 11180, 11230, 3050);
                        GiveProficiency(gs, 611);
                        GiveProficiency(gs, 612);
                        break;
                    case 135:
                        GiveSkills(gs, 1000, 1005, 1010, 1050, 1055, 1075, 1085, 1090, 1095, 1100, 1170, 1175, 1195, 4000, 4010, 4020, 4050, 4060, 4070, 10309, 30000, 3090, 5001);
                        GiveProficiency(gs, 421);
                        break;
                    case 145:
                        GiveSkills(gs, 1000, 1002, 1005, 1010, 1120, 1125, 1150, 1160, 1165, 1180, 5001, 10309, 10310, 3080);
                        GiveProficiency(gs, 561);
                        break;
                }
                if (actor.CombatSkill != 0)
                    GiveSkill(gs, actor.CombatSkill);
            }
            catch
            {
            }
        }

        private static void GiveSkills(GameState gs, params ushort[] ids)
        {
            for (int i = 0; i < ids.Length; i++)
                GiveSkill(gs, ids[i]);
        }

        private static void GiveSkill(GameState gs, ushort id)
        {
            if (id == 0) return;
            if (SpellTable.SpellInformations == null || !SpellTable.SpellInformations.ContainsKey(id))
                return;
            Spell spell = new Spell(true);
            spell.ID = id;
            byte max = 0;
            foreach (byte level in SpellTable.SpellInformations[id].Keys)
            {
                if (level > max)
                    max = level;
            }
            spell.Level = max;
            gs.AddSpell(spell);
        }

        private static void GiveProficiency(GameState gs, ushort id)
        {
            Proficiency prof = new Proficiency(true);
            prof.ID = id;
            prof.Level = 20;
            gs.AddProficiency(prof);
        }

        private static void Strike(Wanderer actor, Entity body, Entity target)
        {
            DateTime now = DateTime.UtcNow;
            if (now < actor.NextCombat || !CityGuardSystem.IsHostileMonster(target)
                || body.MapID != target.MapID || !HasSight(body, target.X, target.Y)) return;
            SpellInformation spell = KnownSpell(body, actor.CombatSkill);
            if (actor.CombatSkill != 0 && spell == null) return;
            uint claimed = target.MonsterInfo.InSight;
            if (claimed != 0 && claimed != body.UID && claimed != actor.OwnerUid) return;
            int range = spell == null ? actor.CombatRange : Math.Min(actor.CombatRange, spell.Distance);
            if (Kernel.GetDistance(body.X, body.Y, target.X, target.Y) > range
                || !Attacking.Handle.CanAttack(body, target, spell, actor.CombatRange <= 2)) return;
            if (spell != null && !CanCast(body, spell)) return;
            Attack atk = new Attack(true);
            atk.Attacker = body.UID;
            atk.Attacked = target.UID;
            atk.AttackType = actor.Class == 45 ? Attack.Ranged : Attack.Melee;
            atk.X = target.X;
            atk.Y = target.Y;
            uint damage;
            if (spell != null)
            {
                SpendSpell(body, spell);
                damage = Attacking.Calculate.Magico(body, target, spell, ref atk);
                if (spell.ID == 1000) damage /= 2;
            }
            else if (actor.Class == 45)
                damage = Attacking.Calculate.Ranged(body, target, ref atk);
            else
                damage = Attacking.Calculate.Melee(body, target, ref atk);
            actor.NextCombat = now.AddMilliseconds(Math.Max(1000, spell == null ? 0 : spell.ColdTime));
            damage = Math.Min(damage, target.Hitpoints);
            // Use the existing world-actor death path: no fake-player XP, quest or loot awards.
            if (damage > 0)
            {
                if (target.MonsterInfo.InSight == 0 || actor.Class == 25)
                    target.MonsterInfo.InSight = body.UID;
                if (target.Hitpoints <= damage) target.Die(body);
                else target.Hitpoints -= damage;
            }
            if (spell != null)
            {
                SendSpell(body, target, spell, damage, atk);
                return;
            }
            atk.Damage = damage;
            Broadcast(body, atk);
        }

        private static SpellInformation KnownSpell(Entity body, ushort id)
        {
            ISkill known;
            SafeDictionary<byte, SpellInformation> levels;
            SpellInformation spell;
            if (id == 0 || !body.Owner.Spells.TryGetValue(id, out known) || known == null
                || !SpellTable.SpellInformations.TryGetValue(id, out levels)
                || !levels.TryGetValue(known.Level, out spell)) return null;
            return spell;
        }

        private static bool CanCast(Entity body, SpellInformation spell)
        {
            return body.Mana >= spell.UseMana && body.Stamina >= spell.UseStamina
                && spell.NeedXP != 1
                && (spell.ID == 1000 || spell.ID == 1002 || spell.ID == 1005)
                && Attacking.Handle.CanUseSpell(spell, body.Owner);
        }

        private static void SpendSpell(Entity body, SpellInformation spell)
        {
            body.Mana -= spell.UseMana;
            body.Stamina -= spell.UseStamina;
        }

        private static void SendSpell(Entity body, Entity target, SpellInformation spell, uint amount, Attack attack)
        {
            SpellUse use = new SpellUse(true);
            use.Attacker = body.UID;
            use.SpellID = spell.ID;
            use.SpellLevel = spell.Level;
            use.X = target.X;
            use.Y = target.Y;
            if (attack != null) use.Effect = attack.Effect;
            use.AddTarget(target.UID, amount, attack);
            Broadcast(body, use);
        }

        private static bool TryHeal(Wanderer actor, Entity body, Entity owner, DateTime now)
        {
            using(var health=HealthMutationLease.Acquire(body,owner))
                return health.Allowed && TryHealOwned(actor,body,owner,now);
        }
        private static bool TryHealOwned(Wanderer actor, Entity body, Entity owner, DateTime now)
        {
            if (actor.Class != 135 || actor.Stance == CompanionStance.Passive || now < actor.NextCombat) return false;
            Entity target = (ulong)owner.Hitpoints * 100 < (ulong)owner.MaxHitpoints * 70 ? owner : body;
            if (target.Dead || target.MapID != body.MapID || target.MaxHitpoints <= target.Hitpoints
                || (ulong)target.Hitpoints * 100 >= (ulong)target.MaxHitpoints * 70
                || WaveDefenseSystem.BlocksHealSpell(target.Owner, 1005)) return false;
            SpellInformation spell = KnownSpell(body, 1005);
            if (spell == null || Kernel.GetDistance(body.X, body.Y, target.X, target.Y) > spell.Distance
                || !HasSight(body, target.X, target.Y) || !CanCast(body, spell)) return false;
            uint healed = Math.Min(spell.Power, target.MaxHitpoints - target.Hitpoints);
            SpendSpell(body, spell);
            actor.NextCombat = now.AddMilliseconds(Math.Max(2000, spell.ColdTime));
            target.Hitpoints += healed;
            SendSpell(body, target, spell, healed, null);
            return true;
        }

        private static void PickDestination(Wanderer actor)
        {
            if (!Kernel.Maps.ContainsKey(actor.MapId)) return;
            Map map = Kernel.Maps[actor.MapId];
            ushort x;
            ushort y;
            if (FindTile(map, actor.HomeX, actor.HomeY, RadiusOf(actor.MapId), out x, out y))
            {
                actor.DestX = x;
                actor.DestY = y;
            }
        }

        private static byte RadiusOf(ushort mapId)
        {
            for (int i = 0; i < Cities.Length; i++)
            {
                if (Cities[i].MapId == mapId)
                    return Cities[i].Radius;
            }
            return 20;
        }

        private static bool FindTile(Map map, ushort cx, ushort cy, int radius, out ushort x, out ushort y)
        {
            x = cx;
            y = cy;
            if (map == null || map.Floor == null) return false;
            for (int i = 0; i < 40; i++)
            {
                int nx = cx + Kernel.Random.Next(-radius, radius + 1);
                int ny = cy + Kernel.Random.Next(-radius, radius + 1);
                if (nx < 1 || ny < 1) continue;
                if (map.Floor[nx, ny, MapObjectType.Player, null])
                {
                    x = (ushort)nx;
                    y = (ushort)ny;
                    return true;
                }
            }
            return map.Floor[cx, cy, MapObjectType.Player, null];
        }

        private static void MoveToward(Entity body, ushort x, ushort y)
        {
            Wanderer actor;
            lock (Gate)
                if (!ByUid.TryGetValue(body.UID, out actor)) return;
            Map map = body.Owner.Map;
            if (map == null || map.Floor == null) return;
            int distance = Kernel.GetDistance(body.X, body.Y, x, y);
            if (distance >= 5)
            {
                int span = Math.Min(6, distance);
                ushort jumpX = (ushort)(body.X + (x - body.X) * span / distance);
                ushort jumpY = (ushort)(body.Y + (y - body.Y) * span / distance);
                if (WorldActorMotion.JumpClear(body, jumpX, jumpY))
                {
                    actor.Navigation.Reset();
                    return;
                }
            }
            // Close approach and obstructed jumps use at most two routed walking steps.
            for (int i = 0; i < 2; i++)
            {
                Point next;
                if (!actor.Navigation.Next(map, new Point(body.X, body.Y), new Point(x, y), DateTime.UtcNow,
                    (tx, ty) => map.Floor[tx, ty, MapObjectType.Player, null], out next)) break;
                if (!WorldActorMotion.Walk(body, (ushort)next.X, (ushort)next.Y, false)) break;
            }
        }

        private static bool HasSight(Entity body, ushort x, ushort y)
        {
            Map map = body.Owner.Map;
            return map != null && map.Floor != null && CompanionNavigation.HasSight(
                new Point(body.X, body.Y), new Point(x, y),
                (tx, ty) => map.Floor[tx, ty, MapObjectType.Player, null]);
        }

        private static void WalkToward(Entity body, ushort x, ushort y, bool run)
        {
            WorldActorMotion.Walk(body, x, y, run);
        }

        private static void JumpToward(Entity body, ushort x, ushort y)
        {
            WorldActorMotion.Jump(body, x, y);
        }

        private static void Sit(Wanderer actor, Entity body, DateTime now)
        {
            actor.Sitting = true;
            actor.RestUntil = now.AddSeconds(Kernel.Random.Next(6, 16));
            body.Action = Enums.ConquerAction.Sit;
            Data act = new Data(true);
            act.ID = Data.ChangeAction;
            act.UID = body.UID;
            act.dwParam = Enums.ConquerAction.Sit;
            act.wParam1 = body.X;
            act.wParam2 = body.Y;
            Broadcast(body, act);
        }

        private static void Stand(Wanderer actor, Entity body)
        {
            actor.Sitting = false;
            body.Action = Enums.ConquerAction.None;
            Data act = new Data(true);
            act.ID = Data.ChangeAction;
            act.UID = body.UID;
            act.dwParam = Enums.ConquerAction.None;
            act.wParam1 = body.X;
            act.wParam2 = body.Y;
            Broadcast(body, act);
            PickDestination(actor);
        }

        private static bool Greet(Wanderer actor, Entity body, DateTime now)
        {
            if (now - actor.LastGreet < TimeSpan.FromSeconds(22)) return false;
            foreach (GameState client in Kernel.GamePool.Values)
            {
                if (client == null || client.Fake || client.Entity == null) continue;
                if (client.Entity.MapID != actor.MapId) continue;
                if (Kernel.GetDistance(body.X, body.Y, client.Entity.X, client.Entity.Y) > 4) continue;
                if (actor.LastPlayer == client.Entity.UID) continue;
                actor.LastPlayer = client.Entity.UID;
                actor.LastGreet = now;
                string line = Lines[Kernel.Random.Next(0, Lines.Length)];
                client.Send(new Message(body.Name + ": " + line, Color.SkyBlue, Message.Talk));
                Data wave = new Data(true);
                wave.ID = Data.ChangeAction;
                wave.UID = body.UID;
                wave.dwParam = Enums.ConquerAction.Wave;
                wave.wParam1 = body.X;
                wave.wParam2 = body.Y;
                Broadcast(body, wave);
                body.Action = Enums.ConquerAction.None;
                return true;
            }
            return false;
        }

        private static int HumansOn(ushort mapId)
        {
            int count = 0;
            foreach (GameState client in Kernel.GamePool.Values)
            {
                if (client == null || client.Fake || client.Entity == null) continue;
                if (client.Entity.MapID == mapId)
                    count++;
            }
            return count;
        }

        private static Wanderer FindByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            lock (Gate)
            {
                for (int i = 0; i < Actors.Count; i++)
                {
                    if (Actors[i] != null && string.Equals(Actors[i].Name, name, StringComparison.OrdinalIgnoreCase))
                        return Actors[i];
                }
            }
            return null;
        }

        private static void Hire(GameState from, Wanderer actor)
        {
            if (actor.AggressorUid != 0)
            {
                Reply(from, actor, "Stand aside. This is not finished.");
                return;
            }

            if (actor.State == null || actor.State.Entity == null || actor.State.Entity.Dead)
            {
                Reply(from, actor, "Give me a moment. I am not on my feet.");
                return;
            }
            if (actor.OwnerUid != 0 && actor.OwnerUid != from.Entity.UID)
            {
                Reply(from, actor, "I already walk with another.");
                return;
            }
            Wanderer existing;
            lock (Gate)
            {
                if (HiredBy.TryGetValue(from.Entity.UID, out existing) && existing != actor)
                {
                    Reply(from, actor, "You already have " + existing.Name + " with you. Dismiss them first.");
                    return;
                }
            }
            if (from.Entity.MapID != actor.MapId || Kernel.GetDistance(from.Entity.X, from.Entity.Y, actor.State.Entity.X, actor.State.Entity.Y) > 18)
            {
                Reply(from, actor, "Come closer. I will not shout across the city.");
                return;
            }
            lock (Gate)
            {
                // Recheck both sides in the same transaction; two players may hire at once.
                if (actor.OwnerUid != 0 && actor.OwnerUid != from.Entity.UID)
                {
                    Reply(from, actor, "I already walk with another.");
                    return;
                }
                if (HiredBy.TryGetValue(from.Entity.UID, out existing) && existing != actor)
                {
                    Reply(from, actor, "Dismiss your current companion first.");
                    return;
                }
                actor.OwnerUid = from.Entity.UID;
                actor.Duty = DutyFollow;
                HiredBy[from.Entity.UID] = actor;
            }
            actor.Stance = CompanionStance.Assist;
            actor.StayBack = false;
            actor.Retreating = false;
            ClearIntent(actor);
            if (actor.Sitting)
                Stand(actor, actor.State.Entity);
            Reply(from, actor, HireAck[Kernel.Random.Next(0, HireAck.Length)]);
        }

        private static void Wait(GameState from, Wanderer actor)
        {
            if (actor.OwnerUid != from.Entity.UID)
            {
                Reply(from, actor, "Hire me first.");
                return;
            }
            actor.Duty = DutyWait;
            actor.HoldX = actor.State.Entity.X;
            actor.HoldY = actor.State.Entity.Y;
            ClearIntent(actor);
            Reply(from, actor, "I will hold here.");
        }

        private static void ClearIntent(Wanderer actor)
        {
            ClearRetaliation(actor);
            actor.TargetUid = 0;
            actor.OwnerTarget = null;
            actor.Navigation.Reset();
            actor.SeparatedSince = DateTime.MinValue;
            actor.Repositioned = false;
        }

        private static void SetTactics(GameState from, Wanderer actor, string command)
        {
            if (actor.OwnerUid != from.Entity.UID)
            {
                Reply(from, actor, "Hire me first.");
                return;
            }
            ClearIntent(actor);
            if (command == "stay back")
            {
                actor.StayBack = true;
                actor.Duty = DutyFollow;
                Reply(from, actor, "I will keep four paces from you.");
            }
            else if (command == "retreat")
            {
                actor.Duty = DutyFollow;
                actor.Stance = CompanionStance.Passive;
                actor.StayBack = true;
                Reply(from, actor, "Falling back to you. Whisper assist or protect me to resume fighting.");
            }
            else
            {
                actor.Stance = command == "passive" ? CompanionStance.Passive
                    : command == "assist" ? CompanionStance.Assist : CompanionStance.Protect;
                Reply(from, actor, actor.Stance == CompanionStance.Passive ? "I will not attack or cast."
                    : actor.Stance == CompanionStance.Protect ? "I will defend us from attacking monsters."
                    : "I will assist your attacks and defend us.");
            }
        }

        private static void Dismiss(GameState from, Wanderer actor)
        {
            if (actor.OwnerUid != from.Entity.UID)
            {
                Reply(from, actor, "I do not walk with you.");
                return;
            }
            Release(actor, "Safe roads. Whisper hire if we meet again.");
        }

        private static void Release(Wanderer actor, string farewell)
        {
            uint ownerUid = actor.OwnerUid;
            GameState owner = null;
            if (ownerUid != 0)
                Kernel.GamePool.TryGetValue(ownerUid, out owner);
            lock (Gate)
            {
                if (ownerUid != 0)
                    HiredBy.Remove(ownerUid);
                actor.OwnerUid = 0;
                actor.Duty = DutyWander;
                ClearIntent(actor);
                actor.Stance = CompanionStance.Assist;
                actor.Retreating = false;
                actor.StayBack = false;
            }
            if (farewell != null && owner != null && !owner.Fake && owner.Entity != null)
                Reply(owner, actor, farewell);
            ReturnHome(actor);
        }

        private static void ReturnHome(Wanderer actor)
        {
            if (actor.State == null || actor.State.Entity == null) return;
            Entity body = actor.State.Entity;
            ushort mapId = actor.HomeMapId != 0 ? actor.HomeMapId : actor.MapId;
            if (!Kernel.Maps.ContainsKey(mapId)) return;
            Map map = Kernel.Maps[mapId];
            ushort x = actor.HomeX;
            ushort y = actor.HomeY;
            if (!FindTile(map, actor.HomeX, actor.HomeY, 4, out x, out y)) return;
            MoveActor(actor, body, mapId, x, y);
            actor.DestX = x;
            actor.DestY = y;
        }

        private static void DriveHired(Wanderer actor, Entity body, DateTime now)
        {
            GameState owner;
            if (!Kernel.GamePool.TryGetValue(actor.OwnerUid, out owner) || owner == null || owner.Fake || owner.Entity == null)
            {
                Release(actor, null);
                return;
            }
            Entity leader = owner.Entity;
            // Event/dynamic maps need their own companion eligibility rules. Do not bypass entry gates.
            if (owner.Map == null || owner.Map.IsDynamic() || owner.IsWatching() || owner.InQualifier()
                || WaveDefenseSystem.InEvent(leader) || BossRushSystem.InEvent(leader)
                || BeastTrialSystem.InEvent(leader) || LastManStandingSystem.InEvent(leader)
                || NobleWarSystem.InEvent(leader) || DuelPitSystem.InEvent(leader)
                || RelicRushSystem.InEvent(leader) || SteedRaceSystem.InEvent(leader))
            {
                Release(actor, "I will wait for you back in town after this event.");
                return;
            }
            if (leader.MapID != body.MapID)
            {
                ClearIntent(actor);
                if (actor.Duty != DutyWait && !leader.Dead) SnapTo(actor, body, owner);
                return;
            }
            if (leader.Dead)
            {
                ClearIntent(actor);
                return;
            }
            if (actor.Sitting) Stand(actor, body);
            int follow = Kernel.GetDistance(body.X, body.Y, leader.X, leader.Y);
            if (follow > 12 && actor.Duty != DutyWait)
            {
                actor.TargetUid = 0;
                if (actor.SeparatedSince == DateTime.MinValue) actor.SeparatedSince = now;
                // Walk first. Recovery is delayed and only allowed outside combat.
                if (now - actor.SeparatedSince >= TimeSpan.FromSeconds(8)
                    && !CompanionTactics.Retreat(false, body.Hitpoints, body.MaxHitpoints)
                    && !HasThreat(body) && !HasThreat(leader))
                    SnapTo(actor, body, owner);
                else MoveToward(body, leader.X, leader.Y);
                return;
            }
            actor.SeparatedSince = DateTime.MinValue;
            if (actor.Duty == DutyWait && (body.X != actor.HoldX || body.Y != actor.HoldY))
            {
                MoveToward(body, actor.HoldX, actor.HoldY);
                return;
            }
            if (TryHeal(actor, body, leader, now)) return;
            actor.Retreating = CompanionTactics.Retreat(actor.Retreating, body.Hitpoints, body.MaxHitpoints);
            Entity pest = actor.Retreating ? null : SelectTarget(actor, body, leader, now);
            actor.TargetUid = pest == null ? 0 : pest.UID;
            if (pest != null)
            {
                int dist = Kernel.GetDistance(body.X, body.Y, pest.X, pest.Y);
                if (actor.Duty != DutyWait && actor.CombatRange > 2 && dist < 3
                    && !actor.Repositioned && StepAway(body, pest, leader, 10))
                {
                    // Alternate repositioning and firing so a pursuer cannot suppress all attacks.
                    actor.Repositioned = true;
                    return;
                }
                actor.Repositioned = false;
                if (dist <= actor.CombatRange && HasSight(body, pest.X, pest.Y)) Strike(actor, body, pest);
                else if (actor.Duty != DutyWait) MoveToward(body, pest.X, pest.Y);
                return;
            }
            if (actor.Duty == DutyWait) return;
            if (actor.Retreating)
            {
                Entity threat = CityGuardSystem.FindNearbyMonster(body.MapID, body.X, body.Y, 4);
                if (threat != null && StepAway(body, threat, leader, 8)) return;
            }
            int spacing = actor.StayBack || actor.Retreating ? 4 : 2;
            if (follow < spacing && actor.StayBack)
            {
                StepAway(body, leader, leader, spacing);
                return;
            }
            if (follow > spacing)
            {
                // Stable per-companion offset keeps the owner tile clear.
                int sx = (body.UID & 1) == 0 ? spacing : -spacing;
                int sy = (body.UID & 2) == 0 ? spacing : -spacing;
                int x = leader.X + sx, y = leader.Y + sy;
                if (x > 0 && y > 0 && owner.Map.Floor[x, y, MapObjectType.Player, null])
                    MoveToward(body, (ushort)x, (ushort)y);
                else MoveToward(body, leader.X, leader.Y);
            }
        }

        private static Entity SelectTarget(Wanderer actor, Entity body, Entity owner, DateTime now)
        {
            if (actor.Stance == CompanionStance.Passive) return null;
            AssistedTarget assisted = actor.OwnerTarget;
            Entity best = null;
            int bestScore = 0;
            foreach (Entity target in body.Owner.Map.Entities.Values)
            {
                Entity anchor = actor.Duty == DutyWait ? body : owner;
                if (target == null || target.MapID != body.MapID
                    || Kernel.GetDistance(anchor.X, anchor.Y, target.X, target.Y) > 10
                    || !CityGuardSystem.IsHostileMonster(target)) continue;
                int dist = Kernel.GetDistance(body.X, body.Y, target.X, target.Y);
                if (dist > 12 || (actor.Duty == DutyWait &&
                    (dist > actor.CombatRange || !HasSight(body, target.X, target.Y)))) continue;
                uint threat = target.MonsterInfo.InSight;
                int score = CompanionTactics.Score(actor.Stance,
                    assisted != null && assisted.Until > now && assisted.MapId == body.MapID && assisted.Uid == target.UID,
                    threat == owner.UID, threat == body.UID,
                    threat != 0 && threat != owner.UID && threat != body.UID,
                    actor.TargetUid == target.UID, dist);
                if (score > bestScore || (score > 0 && score == bestScore && best != null && target.UID < best.UID))
                {
                    bestScore = score;
                    best = target;
                }
            }
            return best;
        }

        private static bool HasThreat(Entity body)
        {
            foreach (Entity target in body.Owner.Map.Entities.Values)
                if (target != null && target.MapID == body.MapID && target.MonsterInfo != null
                    && !target.Dead && target.MonsterInfo.InSight == body.UID
                    && Kernel.GetDistance(body.X, body.Y, target.X, target.Y) <= 16) return true;
            return false;
        }

        private static bool StepAway(Entity body, Entity threat, Entity owner, int leash)
        {
            Point start = new Point(body.X, body.Y);
            Point best = start;
            int distance = Kernel.GetDistance(body.X, body.Y, threat.X, threat.Y);
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    Point tile = new Point(start.X + dx, start.Y + dy);
                    if (CompanionNavigation.Distance(tile, new Point(owner.X, owner.Y)) > leash
                        || !CompanionNavigation.CanStep(start, tile,
                            (x, y) => body.Owner.Map.Floor[x, y, MapObjectType.Player, null])) continue;
                    int score = CompanionNavigation.Distance(tile, new Point(threat.X, threat.Y));
                    if (score > distance) { distance = score; best = tile; }
                }
            return best != start && WorldActorMotion.Walk(body, (ushort)best.X, (ushort)best.Y, false);
        }

        private static void SnapTo(Wanderer actor, Entity body, GameState owner)
        {
            if (owner == null || owner.Entity == null) return;
            ushort mapId = owner.Entity.MapID;
            if (!Kernel.Maps.ContainsKey(mapId)) return;
            Map map = Kernel.Maps[mapId];
            ushort x;
            ushort y;
            if (!FindTile(map, owner.Entity.X, owner.Entity.Y, 2, out x, out y))
                return;
            ClearIntent(actor);
            MoveActor(actor, body, mapId, x, y);
        }

        private static void MoveActor(Wanderer actor, Entity body, ushort mapId, ushort x, ushort y)
        {
            actor.Navigation.Reset();
            actor.TargetUid = 0;
            if (body.Owner != null && body.Owner.Map != null)
                body.Owner.Map.RemoveEntity(body);
            Data remove = new Data(true);
            remove.UID = body.UID;
            remove.ID = Data.RemoveEntity;
            Broadcast(body, remove);
            actor.MapId = mapId;
            body.Owner.BindWorldMap(mapId);
            body.X = x;
            body.Y = y;
            PaintLook(body.Owner, actor);
            if (body.Owner.Map != null)
                body.Owner.Map.AddEntity(body);
            BroadcastSpawn(body);
        }

        private static void Reply(GameState player, Wanderer actor, string line)
        {
            if (player == null || player.Fake || actor == null || string.IsNullOrEmpty(line)) return;
            Message whisper = new Message(line, player.Entity.Name, actor.Name, Color.White, Message.Whisper);
            if (actor.State != null && actor.State.Entity != null)
                whisper.Mesh = actor.State.Entity.Mesh;
            player.Send(whisper);
        }

        private static void BroadcastSpawn(Entity body)
        {
            foreach (GameState client in Kernel.GamePool.Values)
            {
                if (client == null || client.Fake || client.Entity == null) continue;
                if (client.Entity.MapID != body.MapID) continue;
                if (Kernel.GetDistance(client.Entity.X, client.Entity.Y, body.X, body.Y) > Constants.pScreenDistance)
                    continue;
                body.SendSpawn(client, false);
            }
        }

        private static void Broadcast(Entity body, Interfaces.IPacket packet)
        {
            foreach (GameState client in Kernel.GamePool.Values)
            {
                if (client == null || client.Fake || client.Entity == null) continue;
                if (client.Entity.MapID != body.MapID) continue;
                if (Kernel.GetDistance(client.Entity.X, client.Entity.Y, body.X, body.Y) > Constants.pScreenDistance)
                    continue;
                client.Send(packet);
            }
        }
    }
}
