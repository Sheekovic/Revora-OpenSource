using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Interfaces;
using Revora.Network.GamePackets;
using Revora.Network.Sockets;

namespace Revora.Game.Features
{
    public static class CityGuardSystem
    {
        public const uint CommanderNpcId = 91001;
        public const string CommanderName = "City Commander";
        public const ushort CommanderMap = 1002;
        public const ushort CommanderX = 442;
        public const ushort CommanderY = 339;
        public const uint UniformGarment = 187405; // ArmorofHolyLight
        public const int MaxGuardsPerOffender = 2;
        public const int AcquisitionTiles = 18;
        public static readonly TimeSpan BlueWarning = TimeSpan.FromSeconds(5);
        public static readonly TimeSpan HiddenEscape = TimeSpan.FromSeconds(60);
        public static readonly TimeSpan ReacquireGrace = TimeSpan.FromSeconds(30);
        public static readonly TimeSpan IncidentLimit = TimeSpan.FromMinutes(5);

        private static readonly Dictionary<uint, Incident> Incidents = new Dictionary<uint, Incident>();
        private static readonly Dictionary<uint, DateTime> GraceUntil = new Dictionary<uint, DateTime>();
        private static readonly Dictionary<ulong, DateTime> BountyPairUsed = new Dictionary<ulong, DateTime>();
        private static readonly List<GuardUnit> Units = new List<GuardUnit>();
        private static readonly Dictionary<uint, GuardUnit> ByUid = new Dictionary<uint, GuardUnit>();
        private static readonly List<PendingPost> Pending = new List<PendingPost>();
        private static readonly HashSet<string> UsedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static Map ArenaMap;
        private static GuardUnit ArenaElite;
        private static readonly object Gate = new object();
        private static uint NextUid = 1100001;
        private static bool Ready;

        private static readonly string[] MaleOfficerNames = new string[]
        {
            "OfficerLong", "OfficerYun", "OfficerFeng", "OfficerChen", "OfficerWei",
            "OfficerHao", "OfficerJun", "OfficerLei", "OfficerMing", "OfficerZhen",
            "OfficerBao", "OfficerCheng", "OfficerGang", "OfficerPeng", "OfficerQiang",
            "OfficerSheng", "OfficerTian", "OfficerWu", "OfficerXin", "OfficerYang",
            "OfficerZhao", "OfficerTiger", "OfficerHawk", "OfficerStone", "OfficerIron",
            "OfficerFlint", "OfficerRidge", "OfficerStorm", "OfficerBlade", "OfficerSpear",
            "OfficerCross", "OfficerNorth", "OfficerMark", "OfficerPaul", "OfficerSean",
            "OfficerTroy", "OfficerFinn", "OfficerHugo", "OfficerLars", "OfficerOtto",
            "OfficerAxel", "OfficerDirk", "OfficerKurt", "OfficerNash", "OfficerReed",
            "OfficerColt", "OfficerGarr", "OfficerHolt", "OfficerBram", "OfficerSoren",
            "OfficerTomas", "OfficerOwen", "OfficerLeif", "OfficerRolf", "OfficerIvor",
            "OfficerEmil", "OfficerPike", "OfficerStein"
        };

        private static readonly string[] FemaleOfficerNames = new string[]
        {
            "OfficerMei", "OfficerLing", "OfficerXue", "OfficerYan", "OfficerHua",
            "OfficerFang", "OfficerJing", "OfficerXia", "OfficerQing", "OfficerLan",
            "OfficerYu", "OfficerLian", "OfficerShu", "OfficerNing", "OfficerMoon",
            "OfficerLotus", "OfficerPearl", "OfficerSilk", "OfficerSnow", "OfficerStar",
            "OfficerPhoenix", "OfficerTalia", "OfficerLyra", "OfficerMira", "OfficerYara",
            "OfficerSera", "OfficerNessa", "OfficerKira", "OfficerRina", "OfficerLana",
            "OfficerVera", "OfficerFaye", "OfficerIris", "OfficerLena", "OfficerCora",
            "OfficerHana", "OfficerJade", "OfficerRosa", "OfficerThea", "OfficerZara",
            "OfficerDawn", "OfficerHope", "OfficerMyra", "OfficerCleo", "OfficerAsha",
            "OfficerPia", "OfficerIria", "OfficerElin"
        };

        // Level-140 Super kits from this tree's itemtype (class-correct hats/armor).
        // 118xxx is TrojanCoronet only. Taoists use 114xxx DistinctCap.
        private static readonly ClassKit[] Kits = new ClassKit[]
        {
            // Left hand: dual 1H, shield, arrows, or hossu. Never pair a 2H (5xxxxx / 421) with a shield.
            new ClassKit("Warrior", 25, 111309, 120269, 131309, 480439, 900309, 150269, 160249, 1004, 28, 303, 1260, 2),
            new ClassKit("Trojan", 15, 118309, 120269, 130309, 410439, 420439, 150269, 160249, 1003, 21, 310, 1115, 2),
            new ClassKit("Archer", 45, 113309, 120269, 133309, 500429, 1050002, 150269, 160249, 1004, 32, 302, 8001, 8),
            new ClassKit("Ninja", 55, 123309, 120269, 135309, 601439, 601439, 150269, 160249, 1003, 21, 304, 6000, 2),
            new ClassKit("Monk", 65, 143309, 120269, 136309, 610439, 610439, 150269, 160249, 1004, 22, 305, 10490, 2),
            new ClassKit("Pirate", 75, 144309, 120269, 139309, 611439, 612439, 150269, 160249, 1003, 35, 306, 11140, 4),
            new ClassKit("WaterTaoist", 135, 114309, 121269, 134309, 421439, 619439, 152279, 160249, 1004, 18, 305, 1000, 9),
            new ClassKit("FireTaoist", 145, 114309, 121269, 134309, 561439, 619439, 152279, 160249, 1004, 18, 307, 1165, 9)
        };

        private static readonly ClassKit[] FemaleKits = new ClassKit[]
        {
            new ClassKit("Warrior", 25, 111309, 120269, 131309, 480439, 900309, 150269, 160249, 2001, 210, 420, 1260, 2),
            new ClassKit("Trojan", 15, 118309, 120269, 130309, 410439, 420439, 150269, 160249, 2002, 201, 430, 1115, 2),
            new ClassKit("Archer", 45, 113309, 120269, 133309, 500429, 1050002, 150269, 160249, 2001, 215, 410, 8001, 8),
            new ClassKit("Ninja", 55, 123309, 120269, 135309, 601439, 601439, 150269, 160249, 2002, 220, 440, 6000, 2),
            new ClassKit("Monk", 65, 143309, 120269, 136309, 610439, 610439, 150269, 160249, 2001, 205, 425, 10490, 2),
            new ClassKit("Pirate", 75, 144309, 120269, 139309, 611439, 612439, 150269, 160249, 2002, 218, 435, 11140, 4),
            new ClassKit("WaterTaoist", 135, 114309, 121269, 134309, 421439, 619439, 152279, 160249, 2001, 208, 415, 1000, 9),
            new ClassKit("FireTaoist", 145, 114309, 121269, 134309, 561439, 619439, 152279, 160249, 2002, 212, 445, 1165, 9)
        };

        private struct ClassKit
        {
            public readonly string ClassName;
            public readonly byte Class;
            public readonly uint Head;
            public readonly uint Necklace;
            public readonly uint Armor;
            public readonly uint Weapon;
            public readonly uint Shield;
            public readonly uint Ring;
            public readonly uint Boots;
            public readonly ushort Body;
            public readonly ushort Face;
            public readonly ushort Hair;
            public readonly ushort CombatSkill;
            public readonly short CombatRange;

            public ClassKit(string className, byte cls, uint head, uint necklace, uint armor, uint weapon, uint shield, uint ring, uint boots, ushort body, ushort face, ushort hair, ushort combatSkill, short combatRange)
            {
                ClassName = className;
                Class = cls;
                Head = head;
                Necklace = necklace;
                Armor = armor;
                Weapon = weapon;
                Shield = shield;
                Ring = ring;
                Boots = boots;
                Body = body;
                Face = face;
                Hair = hair;
                CombatSkill = combatSkill;
                CombatRange = combatRange;
            }
        }

        private struct PendingPost
        {
            public ushort MapId;
            public ushort X;
            public ushort Y;
        }

        private sealed class Incident
        {
            public uint Offender;
            public DateTime Started;
            public DateTime Deadline;
            public DateTime? WarningUntil;
            public DateTime? HiddenSince;
            public string Phase;
            public readonly List<uint> Guards = new List<uint>();
            public bool Arena;
        }

        private sealed class GuardUnit
        {
            public string Name;
            public string ClassName;
            public byte Class;
            public byte Level;
            public byte Reborn;
            public ushort MapId;
            public ushort PostX;
            public ushort PostY;
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
            public uint Garment;
            public ushort CombatSkill;
            public short CombatRange;
            public bool Elite;
            public bool Desk;
            public GameState State;
            public uint Sight;
            public DateTime LastAct;
            public DateTime ReviveAt;
        }

        public static void Create()
        {
            SpawnTalker(CommanderNpcId, CommanderMap, CommanderX, CommanderY, 50000, CommanderName, (byte)Enums.ConquerAngle.South);
            Map source = null;
            if (Kernel.Maps.ContainsKey(700))
                source = Kernel.Maps[700];
            else if (Kernel.Maps.ContainsKey(1005))
                source = Kernel.Maps[1005];
            else if (Kernel.Maps.ContainsKey(1002))
                source = Kernel.Maps[1002];
            if (source != null)
            {
                ArenaMap = source.MakeDynamicMap();
                Constants.PKFreeMaps.Add(ArenaMap.ID);
                SpawnTalker(CommanderNpcId, ArenaMap.ID, 48, 48, 50000, CommanderName, (byte)Enums.ConquerAngle.South);
            }
            SpawnCaptains();
            Ready = true;
            lock (Gate)
            {
                for (int i = 0; i < Pending.Count; i++)
                    SpawnOfficer(Pending[i].MapId, Pending[i].X, Pending[i].Y, false);
                Pending.Clear();
            }
        }

        public static void AdoptSpawn(ushort mapId, ushort x, ushort y)
        {
            if (mapId == 0) return;
            lock (Gate)
            {
                if (!Ready)
                {
                    PendingPost post = new PendingPost();
                    post.MapId = mapId;
                    post.X = x;
                    post.Y = y;
                    Pending.Add(post);
                    return;
                }
            }
            SpawnOfficer(mapId, x, y, false);
        }

        public static bool IsCityGuard(Entity entity)
        {
            return entity != null && ByUid.ContainsKey(entity.UID);
        }

        internal static bool OwnsCombatBody(Entity entity)
        {
            GuardUnit unit;
            lock (Gate) return entity != null && ByUid.TryGetValue(entity.UID,out unit)
                && unit.State != null && unit.State.Fake && ReferenceEquals(unit.State.Entity,entity);
        }

        // Equipment and cultivation recalculation must not erase guard durability.
        internal static bool RestoreCombatProfile(Entity body)
        {
            if (body == null || body.Owner == null || !body.Owner.Fake) return false;
            GuardUnit unit;
            lock (Gate)
            {
                if (!ByUid.TryGetValue(body.UID, out unit) || unit.State == null
                    || !ReferenceEquals(unit.State.Entity, body)) return false;
            }
            ApplyCombatProfile(body, unit.Elite);
            return true;
        }

        private static void ApplyCombatProfile(Entity body, bool elite)
        {
            body.MaxHitpoints = Math.Max(body.MaxHitpoints, elite ? 3000000u : 1000000u);
            body.Defence = (ushort)Math.Max((int)body.Defence, elite ? 24000 : 16000);
            body.MagicDefence = (ushort)Math.Max((int)body.MagicDefence, elite ? 12000 : 8000);
            body.MagicDefencePercent = (ushort)Math.Max((int)body.MagicDefencePercent, elite ? 55 : 40);
            body.Immunity = Math.Max(body.Immunity, elite ? 8000 : 6000);
            body.Counteraction = (ushort)Math.Max((int)body.Counteraction, elite ? 800 : 600);
            body.Block = Math.Max(body.Block, elite ? 3500 : 2500);
        }

        public static bool TryHandleWhisper(GameState from, Message message)
        {
            if (from == null || from.Entity == null || message == null || string.IsNullOrEmpty(message._To))
                return false;
            string to = message._To.Trim();
            bool found = false;
            lock (Gate)
            {
                for (int i = 0; i < Units.Count; i++)
                {
                    if (Units[i] != null && string.Equals(Units[i].Name, to, StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }
            }
            if (!found) return false;
            from.Send(new Message("I keep the watch. I do not hire out.", from.Entity.Name, to, Color.White, Message.Whisper));
            return true;
        }

        public static bool IsWorldActor(Entity entity)
        {
            return IsCityGuard(entity) || LivingWorldSystem.IsAdventurer(entity);
        }

        public static bool IsHostileMonster(Entity monster)
        {
            if (monster == null || monster.Dead || monster.Killed) return false;
            if (monster.EntityFlag != EntityFlag.Monster) return false;
            if (monster.Companion) return false;
            if (monster.MonsterInfo == null) return false;
            if (monster.MonsterInfo.Guard || monster.MonsterInfo.Reviver || monster.MonsterInfo.Boss) return false;
            if (monster.Name != null && monster.Name.IndexOf("Guard", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (IsWorldActor(monster)) return false;
            return true;
        }

        public static Entity FindNearbyMonster(ushort mapId, ushort x, ushort y, int range)
        {
            Map map;
            if (!Kernel.Maps.TryGetValue(mapId, out map) || map.Entities == null) return null;
            Entity best = null;
            int bestDist = range + 1;
            foreach (Entity entity in map.Entities.Values)
            {
                if (!IsHostileMonster(entity)) continue;
                int dist = Kernel.GetDistance(x, y, entity.X, entity.Y);
                if (dist <= range && dist < bestDist)
                {
                    best = entity;
                    bestDist = dist;
                }
            }
            return best;
        }

        public static GameState NearestWorldActor(Entity from, int range)
        {
            if (from == null) return null;
            GameState best = null;
            int bestDist = range + 1;
            foreach (GameState client in Kernel.GamePool.Values)
            {
                if (client == null || !client.Fake || client.Entity == null || client.Entity.Dead) continue;
                if (client.Entity.MapID != from.MapID) continue;
                if (!IsWorldActor(client.Entity)) continue;
                int dist = Kernel.GetDistance(from.X, from.Y, client.Entity.X, client.Entity.Y);
                if (dist <= range && dist < bestDist)
                {
                    best = client;
                    bestDist = dist;
                }
            }
            return best;
        }

        public static void SpawnTalker(uint uid, ushort mapId, ushort x, ushort y, ushort mesh, string name)
        {
            SpawnTalker(uid, mapId, x, y, mesh, name, (byte)(mesh % 10));
        }

        public static void SpawnTalker(uint uid, ushort mapId, ushort x, ushort y, ushort mesh, string name, byte facing)
        {
            if (!Kernel.Maps.ContainsKey(mapId)) return;
            Map map = Kernel.Maps[mapId];
            if (map.Npcs.ContainsKey(uid)) return;
            NpcSpawn npc = new NpcSpawn();
            npc.UID = uid;
            npc.Mesh = (ushort)(mesh - (mesh % 10) + (facing % 10));
            npc.Type = Enums.NpcType.Talker;
            npc.X = x;
            npc.Y = y;
            npc.MapID = mapId;
            npc.Name = name;
            map.AddNpc(npc);
        }

        public static bool HandleNpc(GameState client, NpcRequest request)
        {
            if (client == null || request == null || client.ActiveNpc != CommanderNpcId)
                return false;
            Npcs dialog = new Npcs(client);
            int ransom = Tribute(client);
            switch (request.OptionID)
            {
                case 0:
                    dialog.Text("City Guards are named officers. Each one has a class, level 140, second reborn, and full equipment. They look and fight like players, but they serve the city.");
                    dialog.Text("Blue names get a 5 second warning. Red and black names are pursued on sight. Strike an officer and you have five seconds to leave. Kill one and face Darius in the Arena.");
                    dialog.Text("Honor: " + Honor(client) + ". Arena ransom: " + ransom + " silver.");
                    if (CourierLoopSystem.AwaitingCommander(client))
                        CourierLoopSystem.OfferCommander(dialog);
                    dialog.Option("Pay Arena ransom", 1);
                    dialog.Option("Redeem " + Revora.Config.CityCommanderHonorCost + " Honor for " + Revora.Config.CityCommanderHonorGold + " gold", 2);
                    dialog.Option("Just looking.", 255);
                    dialog.Send();
                    return true;
                case 1:
                    if (PayRansom(client))
                        dialog.Text("The ransom is paid. You are released.");
                    else
                        dialog.Text("You have no Arena sentence, or you cannot afford " + ransom + " silver.");
                    dialog.Option("Understood.", 255);
                    dialog.Send();
                    return true;
                case CourierLoopSystem.CommanderOption:
                    return CourierLoopSystem.HandleCommander(client, dialog);
                case 2:
                    if (SpendHonor(client, Revora.Config.CityCommanderHonorCost))
                    {
                        client.Entity.AdjustMoney((decimal)(Revora.Config.CityCommanderHonorGold));
                        client.Entity.AddRevoraPoints(Revora.Config.CityCommanderHonorRevoraPoints);
                        dialog.Text(Revora.Config.CityCommanderHonorCost + " Honor redeemed for " + Revora.Config.CityCommanderHonorGold + " gold and " + Revora.Config.CityCommanderHonorRevoraPoints + " Revora Points.");
                    }
                    else
                        dialog.Text("You need " + Revora.Config.CityCommanderHonorCost + " Honor.");
                    dialog.Option("Thanks.", 255);
                    dialog.Send();
                    return true;
            }
            return true;
        }

        public static uint Honor(GameState client)
        {
            return client["cityguard-honor"];
        }

        public static void AddHonor(GameState client, uint amount)
        {
            client["cityguard-honor"] = Honor(client) + amount;
        }

        public static bool SpendHonor(GameState client, uint cost)
        {
            uint current = Honor(client);
            if (current < cost) return false;
            client["cityguard-honor"] = current - cost;
            return true;
        }

        public static int Tribute(GameState client)
        {
            long progression = 10000L + client.Entity.Level * 2000L + client.Entity.Reborn * 100000L;
            long afford = (long)(client.Entity.Money / 20);
            long value = Math.Min(progression, afford);
            if (value < 10000) value = 10000;
            if (value > 1000000) value = 1000000;
            return (int)value;
        }

        public static bool IsGuardTarget(GameState client, Entity guard)
        {
            if (client == null || client.Entity == null || client.Entity.Dead || client.Fake) return false;
            if (guard == null || guard.Dead) return false;
            if (client.Socket == null || !client.Socket.Alive) return false;
            if (guard.MapID != client.Entity.MapID || !client.Attackable || client.IsWatching()) return false;
            if (Kernel.GetDistance(guard.X, guard.Y, client.Entity.X, client.Entity.Y) > AcquisitionTiles)
                return false;
            lock (Gate)
            {
                Incident incident;
                if (Incidents.TryGetValue(client.Entity.UID, out incident))
                {
                    if (incident.Arena)
                    {
                        GuardUnit unit;
                        return InArena(client) && ByUid.TryGetValue(guard.UID, out unit) && unit.Elite
                            && DateTime.Now < incident.Deadline
                            && (!incident.WarningUntil.HasValue || DateTime.Now >= incident.WarningUntil.Value);
                    }
                    if (InArena(client)) return false;
                    if (DateTime.Now >= incident.Deadline)
                    {
                        Close(client.Entity.UID);
                        return false;
                    }
                    if (incident.Phase == "escaped") return false;
                    if (incident.WarningUntil.HasValue && DateTime.Now < incident.WarningUntil.Value)
                        return false;
                    return Assign(incident, guard.UID);
                }
                if (InArena(client)) return false;
                DateTime grace;
                if (GraceUntil.TryGetValue(client.Entity.UID, out grace) && DateTime.Now < grace)
                    return false;
                if (client.Entity.PKPoints >= 30)
                    return Begin(client, guard.UID, false);
                if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.FlashingName))
                    return Begin(client, guard.UID, true);
                return false;
            }
        }

        public static void OnInnocentKill(Entity killer, Entity victim)
        {
            if (killer == null || killer.Owner == null || victim == null) return;
            if (IsCityGuard(killer) || IsCityGuard(victim)) return;
            if (killer.EntityFlag != EntityFlag.Player || victim.EntityFlag != EntityFlag.Player) return;
            if (ExpeditionEvent.SuppressCrime(killer, victim)) return;
            if (RelicRushSystem.SuppressCrime(killer, victim)) return;
            if (DuelPitSystem.SuppressCrime(killer, victim)) return;
            lock (Gate)
            {
                GraceUntil.Remove(killer.UID);
                Incident incident;
                if (Incidents.TryGetValue(killer.UID, out incident) && !incident.Arena)
                    return;
                Begin(killer.Owner, 0, killer.PKPoints < 30);
            }
        }

        public static void OnBlackKilled(Entity killer, Entity wanted)
        {
            if (killer == null || killer.Owner == null || wanted == null || wanted.Owner == null) return;
            if (IsCityGuard(killer) || IsCityGuard(wanted)) return;
            if (wanted.PKPoints < 100) return;
            if (ExpeditionEvent.SuppressCrime(killer, wanted)) return;
            if (RelicRushSystem.SuppressCrime(killer, wanted)) return;
            if (DuelPitSystem.SuppressCrime(killer, wanted)) return;
            if (killer.Owner.Account.EntityID == wanted.Owner.Account.EntityID) return;
            ulong pair = ((ulong)killer.Owner.Account.EntityID << 32) | wanted.Owner.Account.EntityID;
            lock (Gate)
            {
                DateTime last;
                if (BountyPairUsed.TryGetValue(pair, out last) && DateTime.Now - last < TimeSpan.FromHours(24))
                    return;
                BountyPairUsed[pair] = DateTime.Now;
            }
            uint points = 1;
            if (wanted.PKPoints >= 600) points = 5;
            else if (wanted.PKPoints >= 400) points = 4;
            else if (wanted.PKPoints >= 250) points = 3;
            else if (wanted.PKPoints >= 150) points = 2;
            AddHonor(killer.Owner, points);
            killer.Owner.Send(new Message("You received " + points + " Honor for capturing a wanted offender.", Color.Yellow, Message.TopLeft));
        }

        // Accepted hits reach this path for melee, ranged and spell attacks alike.
        public static bool ReceivePlayerHit(Entity attacker, Entity guard, ref uint damage)
        {
            GuardUnit unit;
            lock (Gate)
            {
                if (guard == null || !ByUid.TryGetValue(guard.UID, out unit)
                    || unit.State == null || !unit.State.Fake || !ReferenceEquals(unit.State.Entity, guard)) return false;
            }
            if (attacker == null || attacker.Owner == null || attacker.Owner.Fake) return false;
            lock (unit)
            {
                GameState session;
                if (attacker.EntityFlag != EntityFlag.Player || attacker.Dead || guard.Dead
                    || !attacker.Owner.Attackable || attacker.Owner.IsWatching()
                    || attacker.Owner.Socket == null || !attacker.Owner.Socket.Alive
                    || !Kernel.GamePool.TryGetValue(attacker.UID, out session) || !ReferenceEquals(session, attacker.Owner)
                    || attacker.MapID != guard.MapID || !ReferenceEquals(attacker.Owner.Map, unit.State.Map)
                    || Time32.Now < attacker.Owner.CantAttack
                    || (attacker.PKMode != Enums.PKMode.PK && attacker.PKMode != Enums.PKMode.Team)
                    || Kernel.GetDistance(attacker.X, attacker.Y, guard.X, guard.Y) > AcquisitionTiles
                    || damage == 0)
                { damage = 0; return true; }
                if (unit.Elite)
                {
                    lock (Gate)
                    {
                        Incident sentence;
                        if (!InArena(attacker.Owner) || !Incidents.TryGetValue(attacker.UID, out sentence)
                            || !sentence.Arena || DateTime.Now >= sentence.Deadline)
                        { damage = 0; return true; }
                    }
                }
                OnGuardAttacked(attacker.Owner, guard);
                damage = Math.Min(damage, guard.Hitpoints);
                if (damage >= guard.Hitpoints) guard.Die(attacker);
                else guard.Hitpoints -= damage;
                return true;
            }
        }

        public static void OnGuardAttacked(GameState client, Entity guard)
        {
            if (client == null || client.Entity == null || client.Fake || InArena(client)) return;
            lock (Gate)
            {
                GraceUntil.Remove(client.Entity.UID);
                Incident incident;
                if (Incidents.TryGetValue(client.Entity.UID, out incident) && DateTime.Now < incident.Deadline)
                    return; // Repeated hits never postpone the warning or interrupt pursuit.
                GuardUnit unit;
                uint uid = guard != null && ByUid.TryGetValue(guard.UID, out unit) && !unit.Desk ? guard.UID : 0;
                Begin(client, uid, client.Entity.PKPoints < 30);
            }
        }

        private static void SendToArena(GameState client, Entity guard)
        {
            if (client == null || client.Entity == null || client.Fake) return;
            if (InArena(client)) return;
            lock (Gate)
            {
                Incident incident;
                if (Incidents.TryGetValue(client.Entity.UID, out incident) && incident.Arena)
                    return;
                incident = new Incident();
                incident.Offender = client.Entity.UID;
                incident.Started = DateTime.Now;
                incident.Deadline = DateTime.Now.AddHours(1);
                incident.WarningUntil = DateTime.Now.Add(BlueWarning);
                incident.Phase = "arena";
                incident.Arena = true;
                Incidents[client.Entity.UID] = incident;
            }
            ushort map = ArenaMap != null ? ArenaMap.ID : (ushort)1005;
            client.Entity.Teleport(map, 50, 50);
            string who = guard != null && !string.IsNullOrEmpty(guard.Name) ? guard.Name : "a City Guard";
            client.Send(new Message("You killed " + who + ". Defeat Darius in the Arena or pay the ransom.", Color.Red, Message.Center));
            int cost = Tribute(client);
            client.MessageBox("Pay " + cost + " silver ransom to leave the Arena?",
                p => { PayRansom(p); }, null, 60);
            EnsureArenaElite();
        }

        public static void OnGuardKilled(Entity guard, Entity killer)
        {
            GuardUnit unit;
            if (guard == null || !ByUid.TryGetValue(guard.UID, out unit)) return;
            if (guard.Hitpoints > 0)
                guard.Hitpoints = 0;
            Data die = new Data(true);
            die.UID = guard.UID;
            die.ID = Data.Die;
            Broadcast(guard, die);
            if (killer != null && killer.Owner != null && !killer.Owner.Fake)
            {
                if (unit.Elite) ReleaseArenaVictory(killer.Owner);
                else SendToArena(killer.Owner, guard);
            }
            unit.ReviveAt = DateTime.Now.AddSeconds(unit.Elite ? 5 : 12);
        }

        public static void Tick()
        {
            DateTime now = DateTime.Now;
            lock (Gate)
            {
                List<uint> close = new List<uint>();
                foreach (KeyValuePair<uint, Incident> pair in Incidents)
                {
                    Incident incident = pair.Value;
                    if (incident.Arena)
                    {
                        GameState arenaClient;
                        if (Kernel.GamePool.TryGetValue(pair.Key, out arenaClient) && arenaClient.Entity != null
                            && ArenaMap != null && arenaClient.Entity.MapID != ArenaMap.ID && now < incident.Deadline)
                            arenaClient.Entity.Teleport(ArenaMap.ID, 50, 50);
                        continue;
                    }
                    if (now >= incident.Deadline)
                    {
                        close.Add(pair.Key);
                        continue;
                    }
                    GameState client;
                    if (!Kernel.GamePool.TryGetValue(pair.Key, out client) || client.Entity == null || client.Entity.Dead)
                        close.Add(pair.Key);
                    else if (incident.WarningUntil.HasValue && now >= incident.WarningUntil.Value)
                    {
                        incident.WarningUntil = null;
                        incident.Phase = "pursuing";
                    }
                }
                for (int i = 0; i < close.Count; i++)
                    Close(close[i]);
            }
            for (int i = 0; i < Units.Count; i++)
                DriveGuard(Units[i], now);
        }

        public static void NoteHidden(GameState client, bool hidden)
        {
            if (client == null || client.Entity == null) return;
            lock (Gate)
            {
                Incident incident;
                if (!Incidents.TryGetValue(client.Entity.UID, out incident) || incident.Arena) return;
                if (hidden)
                {
                    if (!incident.HiddenSince.HasValue) incident.HiddenSince = DateTime.Now;
                    if (DateTime.Now - incident.HiddenSince.Value >= HiddenEscape)
                    {
                        incident.Phase = "escaped";
                        GraceUntil[client.Entity.UID] = DateTime.Now.Add(ReacquireGrace);
                        Incidents.Remove(client.Entity.UID);
                        client.Send(new Message("You escaped the City Guard pursuit.", Color.LightGreen, Message.TopLeft));
                    }
                }
                else incident.HiddenSince = null;
            }
        }

        public static bool PayRansom(GameState client)
        {
            Incident incident;
            lock (Gate)
            {
                if (!Incidents.TryGetValue(client.Entity.UID, out incident) || !incident.Arena)
                    return false;
            }
            int cost = Tribute(client);
            if (client.Entity.Money < (uint)cost) return false;
            client.Entity.AdjustMoney(-(decimal)((uint)cost));
            lock (Gate) Incidents.Remove(client.Entity.UID);
            client.Entity.Teleport(1002, 408, 330);
            client.Send(new Message("Ransom of " + cost + " silver paid. You are released.", Color.Yellow, Message.TopLeft));
            return true;
        }

        public static void ReleaseArenaVictory(GameState client)
        {
            if (client == null || client.Entity == null) return;
            lock (Gate) Incidents.Remove(client.Entity.UID);
            client.Entity.Teleport(1002, 408, 330);
            client.Send(new Message("You defeated Darius and are released.", Color.Yellow, Message.TopLeft));
        }

        public static bool InArena(GameState client)
        {
            if (client == null || client.Entity == null || ArenaMap == null) return false;
            return client.Entity.MapID == ArenaMap.ID;
        }

        private static void SpawnCaptains()
        {
            // The City Commander is the talker NPC created by Create(), not a guard actor.
            SeedStreetOfficer(1002, 288, 250);
            SeedStreetOfficer(1002, 312, 250);
            SeedStreetOfficer(1002, 275, 290);
            SeedStreetOfficer(1002, 325, 290);
            SeedStreetOfficer(1002, 210, 260);
            SeedStreetOfficer(1002, 400, 260);
            SeedStreetOfficer(1002, 408, 370);
            SeedStreetOfficer(1002, 430, 380);
            SeedStreetOfficer(1011, 195, 260);
            SeedStreetOfficer(1020, 566, 563);
            SeedStreetOfficer(1000, 500, 645);
            SeedStreetOfficer(1015, 717, 573);
            for (int i = 0; i < Units.Count; i++)
                Materialize(Units[i]);
        }

        private static void SpawnOfficer(ushort mapId, ushort x, ushort y, bool elite)
        {
            GuardUnit unit;
            lock (Gate)
            {
                if (NearExisting(mapId, x, y, 3)) return;
                bool female = Kernel.Random.Next(0, 2) == 1;
                ClassKit kit = KitFor(female);
                string name = NextUniqueName(female);
                AddNamed(name, kit, mapId, x, y, elite);
                unit = Units[Units.Count - 1];
            }
            Materialize(unit);
        }

        private static bool NearExisting(ushort mapId, ushort x, ushort y, int tiles)
        {
            for (int i = 0; i < Units.Count; i++)
            {
                GuardUnit unit = Units[i];
                if (unit.MapId != mapId) continue;
                if (Kernel.GetDistance(unit.PostX, unit.PostY, x, y) <= tiles)
                    return true;
            }
            return false;
        }

        private static void SeedStreetOfficer(ushort map, ushort x, ushort y)
        {
            bool female = Kernel.Random.Next(0, 2) == 1;
            AddNamed(NextUniqueName(female), KitFor(female), map, x, y, false);
        }

        private static ClassKit RandomKit()
        {
            return KitFor(false);
        }

        private static ClassKit KitFor(bool female)
        {
            ClassKit[] pool = female ? FemaleKits : Kits;
            return pool[Kernel.Random.Next(0, pool.Length)];
        }

        private static string NextUniqueName(bool female)
        {
            string[] pool = female ? FemaleOfficerNames : MaleOfficerNames;
            List<string> free = new List<string>();
            for (int i = 0; i < pool.Length; i++)
            {
                if (!UsedNames.Contains(pool[i]))
                    free.Add(pool[i]);
            }
            if (free.Count > 0)
            {
                string chosen = free[Kernel.Random.Next(0, free.Count)];
                UsedNames.Add(chosen);
                return chosen;
            }
            string prefix = female ? "OfficerSister" : "OfficerBrother";
            int serial = UsedNames.Count + 1;
            string fallback = prefix + serial;
            while (!UsedNames.Add(fallback))
            {
                serial++;
                fallback = prefix + serial;
            }
            return fallback;
        }

        private static void AddNamed(string name, ClassKit kit, ushort map, ushort x, ushort y, bool elite)
        {
            UsedNames.Add(name);
            GuardUnit unit = new GuardUnit();
            unit.Name = name;
            unit.ClassName = kit.ClassName;
            unit.Class = kit.Class;
            unit.Level = 140;
            unit.Reborn = 2;
            unit.MapId = map;
            unit.PostX = x;
            unit.PostY = y;
            unit.Body = kit.Body;
            unit.Face = kit.Face;
            unit.Hair = kit.Hair;
            unit.Head = kit.Head;
            unit.Necklace = kit.Necklace;
            unit.Armor = kit.Armor;
            unit.Weapon = kit.Weapon;
            unit.Shield = kit.Shield;
            unit.Ring = kit.Ring;
            unit.Boots = kit.Boots;
            unit.Garment = UniformGarment;
            unit.CombatSkill = kit.CombatSkill;
            unit.CombatRange = kit.CombatRange;
            unit.Elite = elite;
            Units.Add(unit);
        }

        private static void EnsureArenaElite()
        {
            if (ArenaMap == null) return;
            if (ArenaElite == null)
            {
                AddNamed("Darius", RandomKit(), ArenaMap.ID, 50, 52, true);
                ArenaElite = Units[Units.Count - 1];
            }
            ArenaElite.MapId = ArenaMap.ID;
            ArenaElite.PostX = 50;
            ArenaElite.PostY = 52;
            if (ArenaElite.State == null || ArenaElite.State.Entity == null || ArenaElite.State.Entity.Dead)
                Materialize(ArenaElite);
            else
            {
                ArenaElite.State.Entity.X = 50;
                ArenaElite.State.Entity.Y = 52;
                ArenaElite.State.Entity.MapID = ArenaMap.ID;
            }
        }

        private static void Materialize(GuardUnit unit)
        {
            if (!Kernel.Maps.ContainsKey(unit.MapId)) return;
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
            gs.Entity.UID = uid;
            gs.Entity.Name = unit.Name;
            gs.Entity.Spouse = "CityGuard";
            gs.Entity.ClanName = "CityGuard";
            gs.Entity.Face = unit.Face;
            gs.Entity.Body = unit.Body;
            gs.Entity.HairStyle = unit.Hair;
            gs.Entity.Action = 100;
            gs.Entity.Facing = Enums.ConquerAngle.South;
            gs.Entity.Level = unit.Level;
            gs.Entity.Reborn = unit.Reborn;
            gs.Entity.Class = unit.Class;
            gs.Entity.FirstRebornClass = unit.Class;
            gs.Entity.SecondRebornClass = unit.Class;
            gs.Entity.PKMode = unit.Desk ? Enums.PKMode.Peace : Enums.PKMode.PK;
            gs.Entity.MaxHitpoints = unit.Elite ? 250000u : 65000u;
            gs.Entity.Hitpoints = gs.Entity.MaxHitpoints;
            gs.Entity.MinAttack = unit.Elite ? 12000u : 6000u;
            gs.Entity.MaxAttack = unit.Elite ? 18000u : 9000u;
            gs.Entity.Defence = unit.Elite ? (ushort)8000 : (ushort)4000;
            gs.Entity.MagicDefence = unit.Elite ? (ushort)5000 : (ushort)2500;
            gs.Entity.Stamina = 150;
            gs.Entity.Mana = 8000;
            gs.Entity.NobilityRank = ConquerStructures.NobilityRank.Knight;
            gs.Attackable = !unit.Desk;
            gs.Variables = new VariableVault();
            gs.Friends = new SafeDictionary<uint, ConquerStructures.Society.Friend>();
            gs.Enemy = new SafeDictionary<uint, ConquerStructures.Society.Enemy>();
            gs.Spells = new SafeDictionary<ushort, ISkill>();
            gs.Proficiencies = new SafeDictionary<ushort, IProf>();
            gs.ChiPowers = new List<ConquerStructures.ChiPowerStructure>();
            gs.Retretead_ChiPowers = new List<ConquerStructures.ChiPowerStructure>();
            gs.ChiData = new ChiTable.ChiData() { Name = unit.Name, UID = uid, Powers = gs.ChiPowers };
            gs.NobilityInformation = new ConquerStructures.NobilityInformation();
            gs.NobilityInformation.EntityUID = uid;
            gs.NobilityInformation.Name = unit.Name;
            gs.NobilityInformation.Rank = ConquerStructures.NobilityRank.Knight;
            gs.NobilityInformation.Position = -1;
            gs.NobilityInformation.Gender = (byte)(unit.Body % 10 >= 3 ? 0 : 1);
            gs.NobilityInformation.Mesh = gs.Entity.Mesh;
            gs.TeamArenaStatistic = new TeamQualifierStatistic(true);
            gs.TeamArenaStatistic.EntityID = uid;
            gs.TeamArenaStatistic.Name = unit.Name;
            gs.TeamArenaStatistic.Level = unit.Level;
            gs.TeamArenaStatistic.Class = unit.Class;
            gs.TeamArenaStatistic.Model = gs.Entity.Mesh;
            gs.TeamArenaStatistic.Status = TeamQualifierStatistic.NotSignedUp;
            gs.ArenaStatistic = new QualifierStatistic(true);
            gs.ArenaStatistic.EntityID = uid;
            gs.ArenaStatistic.Name = unit.Name;
            gs.ArenaStatistic.Level = unit.Level;
            gs.ArenaStatistic.Class = unit.Class;
            gs.ArenaStatistic.Model = gs.Entity.Mesh;
            gs.ArenaStatistic.Status = QualifierStatistic.NotSignedUp;
            gs.ChampionStats = new ChampionStatistic(true);
            gs.ChampionStats.UID = uid;
            gs.ChampionStats.Name = unit.Name;
            gs.ChampionStats.Level = unit.Level;
            gs.ChampionStats.Class = unit.Class;
            gs.ChampionStats.Model = gs.Entity.Mesh;
            Equip(gs, ConquerItem.Head, unit.Head);
            Equip(gs, ConquerItem.Necklace, unit.Necklace);
            Equip(gs, ConquerItem.Armor, unit.Armor);
            Equip(gs, ConquerItem.RightWeapon, unit.Weapon);
            if (unit.Shield != 0)
                Equip(gs, ConquerItem.LeftWeapon, unit.Shield);
            Equip(gs, ConquerItem.Ring, unit.Ring);
            Equip(gs, ConquerItem.Boots, unit.Boots);
            Equip(gs, ConquerItem.Garment, unit.Garment);
            Equip(gs, ConquerItem.Fan, 201009);
            Equip(gs, ConquerItem.Tower, 202009);
            Equip(gs, ConquerItem.Bottle, 2100025);
            Equip(gs, ConquerItem.Steed, 300000);
            Equip(gs, ConquerItem.SteedCrop, 203009);
            GiveChi(gs);
            GiveJiangHu(gs, unit);
            gs.Entity.JiangActive = false;
            GiveClassSkills(gs, unit);
            try
            {
                gs.LoadItemStats();
            }
            catch
            {
            }
            ApplyCombatProfile(gs.Entity, unit.Elite);
            gs.Entity.Hitpoints = gs.Entity.MaxHitpoints;
            if (gs.Entity.MaxAttack < 4000)
            {
                gs.Entity.MinAttack = unit.Elite ? 12000u : 6000u;
                gs.Entity.MaxAttack = unit.Elite ? 18000u : 9000u;
            }
            gs.Entity.CharBuffer._BattlePower = (ulong)gs.Entity.BattlePower;
            try
            {
                gs.Equipment.UpdateEntityPacket();
            }
            catch
            {
            }
            PaintLook(gs, unit);
            gs.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Dead | (ulong)FlagsUpdate.Flags.Ghost);
            gs.Entity.X = unit.PostX;
            gs.Entity.Y = unit.PostY;
            gs.Entity.MapObjType = Revora.Game.MapObjectType.Player;
            gs.BindWorldMap(unit.MapId);
            gs.Entity.SendUpdates = true;
            PaintLook(gs, unit);
            Entity previous = null;
            GameState previousState = null;
            lock (Gate)
            {
                if (unit.State != null && unit.State.Entity != null)
                {
                    previous = unit.State.Entity;
                    previousState = unit.State;
                    ByUid.Remove(previous.UID);
                }
            }
            if (previousState != null)
                SessionInstall.TryRemoveFake(previous.UID, previousState);
            if (!SessionInstall.TryInstallFake(uid, gs)) return;
            lock (Gate)
            {
                ByUid[uid] = unit;
                unit.State = gs;
                unit.Sight = 0;
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
        }

        private static void Equip(GameState gs, byte position, uint itemId)
        {
            if (itemId == 0) return;
            try
            {
                ConquerItem item = new ConquerItem(true);
                item.ID = itemId;
                item.UID = ConquerItem.ItemUID.Next;
                item.Position = position;
                bool arrow = ItemHandler.IsArrow(itemId);
                bool garment = position == ConquerItem.Garment;
                item.Plus = (byte)(arrow || garment ? 0 : 12);
                item.Bless = (byte)(arrow || garment ? 0 : 7);
                item.Color = Enums.Color.Orange;
                if (!arrow && !garment)
                {
                    uint type = itemId / 1000;
                    if (type == 203)
                    {
                        item.SocketOne = Enums.Gem.NoSocket;
                        item.SocketTwo = Enums.Gem.NoSocket;
                    }
                    else if (type == 201)
                    {
                        item.SocketOne = Enums.Gem.SuperThunderGem;
                        item.SocketTwo = Enums.Gem.SuperThunderGem;
                    }
                    else if (type == 202)
                    {
                        item.SocketOne = Enums.Gem.SuperGloryGem;
                        item.SocketTwo = Enums.Gem.SuperGloryGem;
                    }
                    else if (type == 204)
                    {
                        item.SocketOne = Enums.Gem.SuperThunderGem;
                        item.SocketTwo = Enums.Gem.SuperGloryGem;
                    }
                    else
                    {
                        Enums.Gem gem = (gs.Entity.Class == 135 || gs.Entity.Class == 145)
                            ? Enums.Gem.SuperPhoenixGem
                            : Enums.Gem.SuperDragonGem;
                        item.SocketOne = gem;
                        item.SocketTwo = gem;
                    }
                }
                item.Durability = 10000;
                item.MaximDurability = 10000;
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
                    purify.PurificationLevel = 7;
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
                    return taoist ? 820076u : 820073u;
                case ConquerItem.Armor:
                    return taoist ? 822072u : 822071u;
                case ConquerItem.Necklace:
                    return taoist ? 821034u : 821033u;
                case ConquerItem.Ring:
                    return taoist ? 823060u : 823058u;
                case ConquerItem.Boots:
                    return 824018u;
                case ConquerItem.LeftWeapon:
                    uint left = itemId / 1000;
                    if (left == 900) return 800422u;
                    if (left == 619) return 801104u;
                    if (left == 612) return 800810u;
                    if (left == 601) return 800142u;
                    if (left == 610) return 800725u;
                    if (left == 420) return 800020u;
                    return 0;
                case ConquerItem.RightWeapon:
                    uint type = itemId / 1000;
                    if (type == 410 || type == 420) return 800020u;
                    if (type == 421) return 800522u;
                    if (type == 480) return 800111u;
                    if (type == 500) return 800618u;
                    if (type == 560 || type == 561) return 800215u;
                    if (type == 601) return 800142u;
                    if (type == 610) return 800725u;
                    if (type == 611) return 800811u;
                    if (type == 612) return 800810u;
                    if (type == 613) return 800917u;
                    return taoist ? 800522u : 800020u;
                default:
                    return 0;
            }
        }

        private static void PaintLook(GameState gs, GuardUnit unit)
        {
            // Genuine 6711 DX9 only draws these two player meshes. Do not use Face/Body setters here.
            bool female = unit.Body >= 2000;
            gs.Entity.CharBuffer._Mesh = female ? 2562005u : 371006u;
            gs.Entity.CharBuffer._HairStyle = unit.Hair;
            gs.Entity.CharBuffer._Level = unit.Level;
            gs.Entity.CharBuffer._MonsterLevel = unit.Level;
            gs.Entity.CharBuffer._Head = unit.Head;
            gs.Entity.CharBuffer._Armor = unit.Armor;
            gs.Entity.CharBuffer._RightWeapon = unit.Weapon;
            gs.Entity.CharBuffer._LeftWeapon = unit.Shield;
            gs.Entity.CharBuffer._Garment = unit.Garment;
            gs.Entity.CharBuffer._HeadColor = (byte)Enums.Color.Orange;
            gs.Entity.CharBuffer._ArmorColor = (byte)Enums.Color.Orange;
            // P7 souls stay on inspect items. 6711 world look cannot load those overlays.
            gs.Entity.CharBuffer._HeadSoul = 0;
            gs.Entity.CharBuffer._ArmorSoul = 0;
            gs.Entity.CharBuffer._LeftWeaponSoul = 0;
            gs.Entity.CharBuffer._RightWeaponSoul = 0;
            gs.Entity.CharBuffer._UID = gs.Entity.UID;
            gs.Entity.CharBuffer._X = gs.Entity.X;
            gs.Entity.CharBuffer._Y = gs.Entity.Y;
            gs.Entity.CharBuffer._Action = 100;
            gs.Entity.CharBuffer._Facing = (byte)Enums.ConquerAngle.South;
            gs.Entity.CharBuffer._JiangHuActive = 0;
            gs.Entity.CharBuffer._JiangHuTalen = 0;
            gs.Entity.CharBuffer._CountryCode = 0;
            gs.Entity.CharBuffer._Steed = 0;
            gs.Entity.CharBuffer._SteedPlus = 0;
            gs.Entity.CharBuffer._Boss = 0;
            if (gs.Entity.BitVector != null && gs.Entity.BitVector.bits != null)
            {
                gs.Entity.CharBuffer._StatusFlag = new ulong[7];
                int n = Math.Min(7, gs.Entity.BitVector.bits.Length);
                for (int i = 0; i < n; i++)
                    gs.Entity.CharBuffer._StatusFlag[i] = gs.Entity.BitVector.bits[i];
            }
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

        private static void GiveJiangHu(GameState gs, GuardUnit unit)
        {
            try
            {
                Revora.Game.JiangHu jiang = new Revora.Game.JiangHu(0);
                jiang.UID = gs.Entity.UID;
                jiang.OwnName = unit.Name;
                jiang.JiangName = unit.Name;
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

        private static void GiveClassSkills(GameState gs, GuardUnit unit)
        {
            try
            {
                GiveSkill(gs, 9876);
                switch (unit.Class)
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
                        GiveSkills(gs, 1000, 1010, 1050, 1055, 1075, 1085, 1090, 1095, 1100, 1170, 1175, 1195, 4000, 4010, 4020, 4050, 4060, 4070, 10309, 30000, 3090, 5001);
                        GiveProficiency(gs, 421);
                        break;
                    case 145:
                        GiveSkills(gs, 1000, 1002, 1005, 1010, 1120, 1125, 1150, 1160, 1165, 1180, 5001, 10309, 10310, 3080);
                        GiveProficiency(gs, 561);
                        break;
                }
                if (unit.CombatSkill != 0)
                    GiveSkill(gs, unit.CombatSkill);
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
            byte max = SpellTable.SpellInformations[id].Keys.Max();
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

        private static void DriveGuard(GuardUnit unit, DateTime now)
        {
            if (unit.State == null || unit.State.Entity == null)
            {
                if (now >= unit.ReviveAt)
                    Materialize(unit);
                return;
            }
            Entity guard = unit.State.Entity;
            if (guard.Dead)
            {
                if (now >= unit.ReviveAt)
                    Materialize(unit);
                return;
            }
            if (now - unit.LastAct < TimeSpan.FromMilliseconds(700)) return;
            unit.LastAct = now;
            if (unit.Desk)
            {
                ReturnToPost(unit, guard);
                return;
            }
            GameState wanted = FindTarget(unit, guard);
            if (wanted != null && wanted.Entity != null && !wanted.Entity.Dead)
            {
                unit.Sight = wanted.Entity.UID;
                short pkDist = Kernel.GetDistance(guard.X, guard.Y, wanted.Entity.X, wanted.Entity.Y);
                if (pkDist <= unit.CombatRange)
                    Strike(unit, guard, wanted.Entity);
                else if (pkDist <= AcquisitionTiles)
                    StepToward(guard, wanted.Entity.X, wanted.Entity.Y);
                else
                {
                    unit.Sight = 0;
                    ReturnToPost(unit, guard);
                }
                return;
            }
            Entity pest = FindNearbyMonster(guard.MapID, guard.X, guard.Y, AcquisitionTiles);
            if (pest != null)
            {
                unit.Sight = pest.UID;
                short dist = Kernel.GetDistance(guard.X, guard.Y, pest.X, pest.Y);
                if (dist <= unit.CombatRange)
                    Strike(unit, guard, pest);
                else
                    StepToward(guard, pest.X, pest.Y);
                return;
            }
            unit.Sight = 0;
            ReturnToPost(unit, guard);
        }

        private static GameState FindTarget(GuardUnit unit, Entity guard)
        {
            GameState best = null;
            foreach (GameState client in Kernel.GamePool.Values)
            {
                if (client == null || client.Fake || client.Entity == null) continue;
                if (client.Entity.MapID != guard.MapID) continue;
                if (!IsGuardTarget(client, guard)) continue;
                if (best == null)
                    best = client;
                else if (client.Entity.PKPoints > best.Entity.PKPoints)
                    best = client;
            }
            return best;
        }

        private static void ReturnToPost(GuardUnit unit, Entity guard)
        {
            if (guard.X == unit.PostX && guard.Y == unit.PostY) return;
            StepToward(guard, unit.PostX, unit.PostY);
        }

        private static void StepToward(Entity guard, ushort x, ushort y)
        {
            WorldActorMotion.Approach(guard, x, y);
        }

        private static void Strike(GuardUnit unit, Entity guard, Entity target)
        {
            using(var health=HealthMutationLease.Acquire(guard,target))
                if(health.Allowed)StrikeOwned(unit,guard,target);
        }
        private static void StrikeOwned(GuardUnit unit, Entity guard, Entity target)
        {
            if (target == null || target.Dead) return;
            bool monster = target.EntityFlag == EntityFlag.Monster;
            if (!monster && (target.Owner == null || !target.Owner.Attackable)) return;
            if (monster && target.MonsterInfo != null)
                target.MonsterInfo.InSight = guard.UID;
            uint damage = (uint)Kernel.Random.Next((int)Math.Max(1, guard.MinAttack), (int)Math.Max(guard.MinAttack + 1, guard.MaxAttack));
            WarriorModes.Counter(guard,target,new Attack(true){AttackType=Attack.Melee},damage,Time32.Now);
            // A lethal reaction can sentence or release the player. The old-map
            // strike must not follow that player into the new destination.
            if (guard.MapID != target.MapID) return;
            if (target.Hitpoints <= damage)
            {
                try
                {
                    target.Die(guard);
                }
                catch
                {
                    target.Hitpoints = 0;
                }
            }
            else
                target.Hitpoints -= damage;
            if (unit.CombatSkill != 0)
            {
                SpellUse use = new SpellUse(true);
                use.Attacker = guard.UID;
                use.SpellID = unit.CombatSkill;
                byte level = 0;
                ISkill known;
                if (guard.Owner != null && guard.Owner.Spells != null && guard.Owner.Spells.TryGetValue(unit.CombatSkill, out known) && known != null)
                    level = known.Level;
                use.SpellLevel = level;
                use.X = target.X;
                use.Y = target.Y;
                use.AddTarget(target.UID, damage, null);
                Broadcast(guard, use);
                return;
            }
            Attack atk = new Attack(true);
            atk.Attacker = guard.UID;
            atk.Attacked = target.UID;
            atk.AttackType = Attack.Melee;
            atk.X = target.X;
            atk.Y = target.Y;
            atk.Damage = damage;
            if (!monster && target.Owner != null && !target.Owner.Fake)
                target.Owner.SendScreen(atk, true);
            else
                Broadcast(guard, atk);
        }

        private static void BroadcastSpawn(Entity guard)
        {
            foreach (GameState client in Kernel.GamePool.Values)
            {
                if (client == null || client.Fake || client.Entity == null) continue;
                if (client.Entity.MapID != guard.MapID) continue;
                if (Kernel.GetDistance(client.Entity.X, client.Entity.Y, guard.X, guard.Y) > Constants.pScreenDistance)
                    continue;
                guard.SendSpawn(client, false);
            }
        }

        private static void Broadcast(Entity guard, Interfaces.IPacket packet)
        {
            foreach (GameState client in Kernel.GamePool.Values)
            {
                if (client == null || client.Fake || client.Entity == null) continue;
                if (client.Entity.MapID != guard.MapID) continue;
                if (Kernel.GetDistance(client.Entity.X, client.Entity.Y, guard.X, guard.Y) > Constants.pScreenDistance)
                    continue;
                client.Send(packet);
            }
        }

        private static bool Begin(GameState client, uint guardUid, bool warning)
        {
            Incident incident = new Incident();
            incident.Offender = client.Entity.UID;
            incident.Started = DateTime.Now;
            incident.Deadline = DateTime.Now.Add(IncidentLimit);
            incident.Phase = warning ? "warning" : "pursuing";
            if (warning) incident.WarningUntil = DateTime.Now.Add(BlueWarning);
            if (guardUid != 0) incident.Guards.Add(guardUid);
            Incidents[client.Entity.UID] = incident;
            if (warning)
                client.Send(new Message("A City Guard warns you. Leave or you will be pursued.", Color.Orange, Message.TopLeft));
            return !warning && guardUid != 0;
        }

        private static bool Assign(Incident incident, uint guardUid)
        {
            GameState offender;
            if (!Kernel.GamePool.TryGetValue(incident.Offender, out offender) || offender.Entity == null) return false;
            for (int i = incident.Guards.Count - 1; i >= 0; i--)
            {
                GuardUnit assigned;
                if (!ByUid.TryGetValue(incident.Guards[i], out assigned) || assigned.State == null
                    || assigned.State.Entity == null || assigned.State.Entity.Dead || assigned.Desk
                    || assigned.State.Entity.MapID != offender.Entity.MapID
                    || Kernel.GetDistance(assigned.State.Entity.X, assigned.State.Entity.Y,
                        offender.Entity.X, offender.Entity.Y) > AcquisitionTiles)
                    incident.Guards.RemoveAt(i);
            }
            if (incident.Guards.Contains(guardUid)) return true;
            if (incident.Guards.Count >= MaxGuardsPerOffender) return false;
            incident.Guards.Add(guardUid);
            return true;
        }

        private static void Close(uint offender)
        {
            Incidents.Remove(offender);
        }
    }
}
