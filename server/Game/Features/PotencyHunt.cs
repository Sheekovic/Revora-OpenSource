using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Revora.Database;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    // A map owns one bounded population. Death, loot and replenishment share its gate.
    internal sealed class PotencyHunt
    {
        internal const ushort MapId = 1217;
        internal const uint FamilyId = 9901217;
        internal const int Population = 1000;
        internal const int RespawnSeconds = 1;
        internal const int Radius = 100;
        private readonly Map map;
        private readonly MonsterInformation template;
        private readonly object gate = new object();
        private readonly Random random = new Random();
        private readonly List<Point> cells;
        private readonly Dictionary<uint, Slot> slots = new Dictionary<uint, Slot>();
        private Time32 nextStatus;
        private sealed class Slot
        {
            internal Entity Monster;
            internal bool DeathHandled, LootRolled;
        }

        internal static void Create()
        {
            Map field;
            if (!Kernel.Maps.TryGetValue(MapId, out field)) field = new Map(MapId, "");
            if (field.PotencyPopulation != null) return;
            MonsterInformation baseline;
            if (!MonsterInformation.MonsterInformations.TryGetValue(1413, out baseline)
                || baseline.Name != "AlienSerpent") throw new InvalidOperationException("Potency Hunt requires AlienSerpent 1413");
            var population = new PotencyHunt(field, baseline);
            field.PotencyPopulation = population;
            population.Tick(Time32.Now);
            if (population.slots.Count != Population) throw new InvalidOperationException("Potency Hunt could not place " + Population + " monsters");
            Console.WriteLine("[potency-hunt] map=1217 population=" + population.slots.Count
                + " hp=" + population.template.Hitpoints + " attack=" + population.template.MinAttack + "-" + population.template.MaxAttack
                + " defence=" + population.template.Defence + " respawn_seconds=1 drops=10/5/1 no_drop=84");
        }

        internal PotencyHunt(Map field, MonsterInformation baseline)
        {
            if (field == null || field.ID != MapId || baseline == null) throw new ArgumentException("Invalid Potency Hunt map or baseline");
            map = field;
            template = baseline.Copy();
            template.ID = FamilyId;
            template.Name = "PotencySerpent";
            template.Hitpoints = Scale(baseline.Hitpoints, 125);
            template.MinAttack = Scale(baseline.MinAttack, 115);
            template.MaxAttack = Scale(baseline.MaxAttack, 115);
            template.Defence = (ushort)Math.Min(ushort.MaxValue, Scale(baseline.Defence, 110));
            template.RespawnTime = RespawnSeconds;
            template.Boss = template.Guard = template.Reviver = false;
            template.BoundX = 461; template.BoundY = 449;
            template.BoundCX = template.BoundCY = Radius * 2 + 1;
            cells = SpawnCells(field.Floor, 561, 549, Radius);
            if (cells.Count < Population) throw new InvalidOperationException("Not enough reachable Potency Hunt tiles");
        }

        private static uint Scale(uint value, uint percent)
        {
            return (uint)Math.Min(uint.MaxValue, ((ulong)value * percent + 99) / 100);
        }

        internal static uint RollPack(int roll)
        {
            if (roll < 0 || roll >= 10000) throw new ArgumentOutOfRangeException("roll");
            return roll < 1000 ? 3007108u : roll < 1500 ? 3007109u : roll < 1600 ? 3007110u : 0;
        }

        internal static List<Point> SpawnCells(Floor floor, int centerX, int centerY, int radius)
        {
            if (radius < 1 || radius > Radius) throw new ArgumentOutOfRangeException("radius");
            var result = new List<Point>();
            var pending = new Queue<Point>();
            var seen = new HashSet<Point>();
            var start = new Point(centerX, centerY);
            if (!floor[start.X, start.Y, MapObjectType.Player]) return result;
            seen.Add(start); pending.Enqueue(start);
            int[] dx = { -1, 1, 0, 0 }, dy = { 0, 0, -1, 1 };
            while (pending.Count != 0)
            {
                Point cell = pending.Dequeue();
                // Leave space around the owner's reference point and distribute the population.
                if (Math.Max(Math.Abs(cell.X-centerX), Math.Abs(cell.Y-centerY)) >= 5
                    && cell.X % 3 == 0 && cell.Y % 3 == 0) result.Add(cell);
                for (int direction = 0; direction < 4; direction++)
                {
                    var next = new Point(cell.X + dx[direction], cell.Y + dy[direction]);
                    if (Math.Abs(next.X-centerX) > radius || Math.Abs(next.Y-centerY) > radius
                        || !floor[next.X, next.Y, MapObjectType.Player] || !seen.Add(next)) continue;
                    pending.Enqueue(next);
                }
            }
            return result;
        }

        private bool FindCell(out Point cell)
        {
            int offset = random.Next(cells.Count);
            for (int i = 0; i < cells.Count; i++)
            {
                cell = cells[(offset+i) % cells.Count];
                if (!map.Floor[cell.X, cell.Y, MapObjectType.Monster]) continue;
                Point candidate = cell;
                bool occupied = Kernel.GamePool.Values.Any(p => p != null && p.Entity != null && p.Entity.MapID == map.ID
                    && Kernel.GetDistance(p.Entity.X, p.Entity.Y, (ushort)candidate.X, (ushort)candidate.Y) < 3);
                if (!occupied) return true;
            }
            cell = default(Point); return false;
        }

        internal bool Contains(Entity entity)
        {
            lock (gate) { Slot slot; return entity != null && slots.TryGetValue(entity.UID, out slot) && ReferenceEquals(slot.Monster, entity); }
        }

        internal bool Die(Entity monster, Entity killer)
        {
            lock (gate)
            {
                Slot slot;
                if (!slots.TryGetValue(monster.UID, out slot) || !ReferenceEquals(slot.Monster, monster)) return false;
                if (slot.DeathHandled) return true;
                slot.DeathHandled = true;
                monster.DieCore(killer);
                return true;
            }
        }

        internal void Tick(Time32 now)
        {
            lock (gate)
            {
                foreach (var pair in slots.ToArray())
                {
                    Slot slot = pair.Value;
                    Entity registered;
                    if (!map.Entities.TryGetValue(pair.Key, out registered) || !ReferenceEquals(registered, slot.Monster))
                    { slots.Remove(pair.Key); continue; }
                    if (!slot.Monster.Dead || !slot.DeathHandled || now < slot.Monster.DeathStamp.AddSeconds(RespawnSeconds)) continue;
                    Point cell;
                    if (!FindCell(out cell)) continue;
                    slot.Monster.X = (ushort)cell.X; slot.Monster.Y = (ushort)cell.Y;
                    slot.Monster.ClearFlagsMobs();
                    slot.Monster.CauseOfDeathIsMagic = false;
                    slot.Monster.ToxicFogLeft = 0;
                    slot.Monster.Hitpoints = slot.Monster.MaxHitpoints;
                    slot.DeathHandled = slot.LootRolled = false;
                    map.Floor[cell.X, cell.Y, MapObjectType.Monster, slot.Monster] = false;
                    SendSpawn(slot.Monster);
                }
                while (slots.Count < Population)
                {
                    Point cell;
                    if (!FindCell(out cell)) break;
                    var monster = new Entity(EntityFlag.Monster, false);
                    monster.MapObjType = MapObjectType.Monster;
                    monster.MonsterInfo = template.Copy(); monster.MonsterInfo.Owner = monster;
                    monster.Name = template.Name; monster.Body = template.Mesh; monster.Level = template.Level;
                    monster.MinAttack = template.MinAttack;
                    monster.MaxAttack = monster.MagicAttack = template.MaxAttack;
                    monster.Defence = template.Defence;
                    monster.Hitpoints = monster.MaxHitpoints = template.Hitpoints;
                    monster.ApplyMonsterBossHud();
                    monster.UID = map.EntityUIDCounter.Next; monster.MapID = map.ID;
                    monster.X = (ushort)cell.X; monster.Y = (ushort)cell.Y;
                    monster.SendUpdates = true;
                    map.AddEntity2(monster);
                    slots.Add(monster.UID, new Slot { Monster = monster });
                    SendSpawn(monster);
                }
                if (now >= nextStatus)
                {
                    nextStatus = now.AddSeconds(10);
                    string status = "{\"map\":1217,\"target\":" + Population + ",\"tracked\":" + slots.Count
                        + ",\"alive\":" + slots.Values.Count(s => !s.Monster.Dead)
                        + ",\"utc\":\"" + DateTime.UtcNow.ToString("o") + "\"}";
                    try
                    {
                        const string path = "database/potency-hunt-status.json";
                        System.IO.File.WriteAllText(path + ".pending", status);
                        if (System.IO.File.Exists(path)) System.IO.File.Replace(path + ".pending", path, null);
                        else System.IO.File.Move(path + ".pending", path);
                    }
                    catch (System.IO.IOException error) { Console.WriteLine("[potency-hunt] status_write=" + error.GetType().Name); }
                }
            }
        }

        private void SendSpawn(Entity monster)
        {
            foreach (var player in Kernel.GamePool.Values)
                if (player != null && player.Entity != null && player.Entity.MapID == map.ID
                    && Kernel.GetDistance(player.Entity.X, player.Entity.Y, monster.X, monster.Y) < Constants.nScreenDistance)
                    monster.SendSpawn(player, false);
        }

        internal bool Drop(Entity monster, Entity killer)
        {
            lock (gate)
            {
                Slot slot;
                if (!slots.TryGetValue(monster.UID, out slot) || !ReferenceEquals(slot.Monster, monster)) return false;
                if (slot.LootRolled) return true;
                slot.LootRolled = true;
                if (killer == null || killer.Owner == null || killer.Owner.Fake || killer.EntityFlag != EntityFlag.Player
                    || killer.MapID != map.ID || killer.Owner.Action != 2) return true;
                uint id = RollPack(random.Next(10000));
                if (id == 0) return true;
                ConquerItemBaseInformation info;
                if (!ConquerItemInformation.BaseInformations.TryGetValue(id, out info)) throw new InvalidOperationException("Missing Exploit Pack catalog entry");
                ushort x = monster.X, y = monster.Y;
                if (!map.SelectCoordonates(ref x, ref y)) return true;
                var item = new ConquerItem(true) { UID = ConquerItem.ItemUID.Next, ID = id, StackSize = 1, Durability = info.Durability,
                    MaximDurability = info.Durability, MobDropped = true, Color = Enums.Color.Orange };
                var drop = new FloorItem(true) { Item = item, ItemID = id, UID = FloorItem.FloorUID.Next,
                    Owner = killer.Owner, OwnerUID = killer.UID, MapID = map.ID, X = x, Y = y,
                    MapObjType = MapObjectType.Item, ValueType = FloorItem.FloorValueType.Item,
                    Type = FloorItem.Drop, OnFloor = Time32.Now, ItemColor = item.Color };
                map.AddFloorItem(drop);
                monster.MonsterInfo.SendScreenSpawn(drop);
                return true;
            }
        }
    }
}
