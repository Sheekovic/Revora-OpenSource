using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class MonsterEquipmentDrops
    {
        private sealed class Life { internal Time32 Stamp; internal bool Rolled; }
        private sealed class Origin { internal Guid Id; internal uint Monster, Type, ItemUid, ItemId; internal ConquerItem Item; }
        private static readonly ConditionalWeakTable<Entity, Life> lives = new ConditionalWeakTable<Entity, Life>();
        private static readonly ConditionalWeakTable<FloorItem, Origin> origins = new ConditionalWeakTable<FloorItem, Origin>();
        private static readonly HashSet<uint> families = new HashSet<uint> {120,121,150,151,152,160,410,420,430,440,450,460,480,481,490,500,510,530,540,560,561,580};
        private static readonly uint[][] catalog = new uint[141][];
        internal static Func<int, int> Roll = max => Kernel.Random.Next(max);
        internal static void Initialize()
        {
            var common = ConquerItemInformation.BaseInformations.Values.Where(i => families.Contains(i.ID / 1000) && i.ID % 10 == 3 && i.Level <= 120 && i.Durability > 0).ToArray();
            for (int level = 1; level < catalog.Length; level++)
            {
                int cap = Math.Min(120, level); var available = common.Where(i => i.Level <= cap).ToArray();
                if (available.Length == 0) throw new InvalidOperationException("Common monster equipment catalog is missing.");
                int low = Math.Max(1, available.Max(i => (int)i.Level) - 9);
                catalog[level] = available.Where(i => i.Level >= low).Select(i => i.ID).OrderBy(i => i).ToArray();
            }
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); Q.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_monster_equipment_pickups(
id uuid PRIMARY KEY,item_uid bigint NOT NULL UNIQUE,item_type integer NOT NULL,monster_uid bigint NOT NULL,monster_type bigint NOT NULL,
map integer NOT NULL,collector bigint NOT NULL CHECK(collector>0),mentor bigint NOT NULL CHECK(mentor>=0),
points integer NOT NULL CHECK(points IN (0,1)),picked_at timestamptz NOT NULL DEFAULT now(),CHECK((mentor=0)=(points=0)));");
            }
        }
        private static bool Wilderness(ushort map) { return map == 1002 || map == 1011 || map == 1020 || map == 1000 || map == 1015; }
        internal static void TryDrop(Entity monster, Entity killer)
        {
            if (monster == null || monster.MonsterInfo == null || killer == null || !Q.Player(killer.Owner) || !ReferenceEquals(killer.Owner.Entity, killer)
                || !ReferenceEquals(monster.MonsterInfo.Owner, monster) || CityGuardSystem.IsCityGuard(monster) || LivingWorldSystem.IsAdventurer(monster)
                || monster.EntityFlag != EntityFlag.Monster || !monster.Dead || monster.Companion || monster.MonsterInfo.Boss || monster.MonsterInfo.Guard || monster.MonsterInfo.Reviver
                || monster.MapID != killer.MapID || !Wilderness(monster.MapID) || monster.MonsterInfo.Level == 0 || monster.MonsterInfo.Level > 140) return;
            var map = killer.Owner.Map; Entity current;
            if (map == null || map.BaseID != map.ID || !map.Entities.TryGetValue(monster.UID, out current) || !ReferenceEquals(current, monster)) return;
            lock (monster)
            {
                var life = lives.GetValue(monster, _ => new Life());
                if (life.Rolled && life.Stamp == monster.DeathStamp) return;
                life.Rolled = true; life.Stamp = monster.DeathStamp;
                int chance = Math.Max(0, Math.Min(10000, Config.MonsterPlusEquipmentBasisPoints));
                if (chance == 0 || Roll(10000) >= chance) return;
                var choices = catalog[monster.MonsterInfo.Level];
                if (choices == null || choices.Length == 0) throw new InvalidOperationException("Monster equipment catalog was not initialized.");
                ushort x = monster.X, y = monster.Y;
                if (!map.SelectCoordonates(ref x, ref y)) return;
                var item = Q.New(choices[Roll(choices.Length)], false); item.Plus = 1; item.MobDropped = true; item.Color = Enums.Color.Orange;
                var drop = new FloorItem(true) { UID = FloorItem.FloorUID.Next, Item = item, ItemID = item.ID, MapID = map.ID,
                    X = x, Y = y, Owner = killer.Owner, OwnerUID = killer.UID, Type = FloorItem.Drop, ValueType = FloorItem.FloorValueType.Item,
                    OnFloor = Time32.Now, ItemColor = item.Color, MapObjType = MapObjectType.Item };
                while (map.Npcs.ContainsKey(drop.UID) || map.FloorItems.ContainsKey(drop.UID)) drop.UID = FloorItem.FloorUID.Next;
                origins.Add(drop, new Origin { Id = Guid.NewGuid(), Monster = monster.UID, Type = monster.MonsterInfo.ID, Item = item, ItemUid = item.UID, ItemId = item.ID });
                map.AddFloorItem(drop); monster.MonsterInfo.SendScreenSpawn(drop);
            }
        }
        internal static bool Managed(FloorItem drop) { Origin origin; return drop != null && origins.TryGetValue(drop, out origin); }
        internal static bool Pickup(GameState p, FloorItem drop)
        {
            Origin origin;
            if (drop == null || !origins.TryGetValue(drop, out origin)) return false;
            if (!Q.Player(p)) return true;
            lock (p.ItemSyncRoot)
            lock (drop)
            lock (MentorExperience.Sync) try
            {
                FloorItem current; var map = p.Map;
                if (!Q.Player(p) || p.Booth != null || p.Trade == null || p.Trade.InTrade || map == null || map.ID != drop.MapID
                    || p.Entity.X != drop.X || p.Entity.Y != drop.Y || drop.PickedUpAlready || drop.Type != FloorItem.Drop
                    || Time32.Now >= drop.OnFloor.AddSeconds(Constants.FloorItemSeconds)
                    || !map.FloorItems.TryGetValue(drop.UID, out current) || !ReferenceEquals(current, drop)) return true;
                if (drop.OwnerUID != 0 && drop.OwnerUID != p.Entity.UID && Time32.Now < drop.OnFloor.AddSeconds(Constants.FloorItemAvailableAfter)
                    && !(p.Team != null && p.Team.PickupItems && p.Team.IsTeammate(drop.OwnerUID))) { p.Send(Constants.FloorItemNotAvailable); return true; }
                var item = origin.Item;
                if (!ReferenceEquals(item, drop.Item) || item.UID != origin.ItemUid || item.ID != origin.ItemId || item.Plus != 1 || item.Bound || item.StackSize != 1 || item.Position != 0 || item.Warehouse != 0 || drop.ItemID != item.ID)
                    throw new InvalidOperationException("Monster equipment origin changed.");
                if (p.Inventory.Count >= 40) { p.Send(Constants.FullInventory); return true; }
                uint mentor = 0; long actual = 0, total = 0;
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open(); using (var t = c.BeginTransaction())
                    {
                        SkillTable.LockOwner(c, t, p);
                        using (var q = Q.Command(c, t, "SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0", "uid", (long)p.Entity.UID))
                            if (Convert.ToInt64(q.ExecuteScalar()) != p.Inventory.Count) throw new InvalidOperationException("Monster pickup inventory requires reload.");
                        using (var q = Q.Command(c, t, "SELECT mentorid,COALESCE(actual_plus,0),COALESCE(total_plus,0) FROM apprentice WHERE apprenticeid=@uid FOR UPDATE", "uid", (long)p.Entity.UID))
                        using (var r = q.ExecuteReader()) if (r.Read())
                        {
                            mentor = checked((uint)r.GetInt64(0)); actual = r.GetInt64(1); total = r.GetInt64(2);
                            if (mentor == 0 || mentor == p.Entity.UID || actual < 0 || total < 0 || r.Read()) throw new InvalidOperationException("Ambiguous monster pickup membership.");
                        }
                        if ((p.Mentor == null ? 0 : p.Mentor.ID) != mentor) throw new InvalidOperationException("Monster pickup membership changed.");
                        if (mentor != 0 && Q.Execute(c, t, "UPDATE apprentice SET actual_plus=@actual,total_plus=@total WHERE mentorid=@mentor AND apprenticeid=@uid AND COALESCE(actual_plus,0)=@before AND COALESCE(total_plus,0)=@lifetime",
                            "actual", checked(actual + 1), "total", checked(total + 1), "mentor", (long)mentor, "uid", (long)p.Entity.UID, "before", actual, "lifetime", total) != 1)
                            throw new InvalidOperationException("Monster equipment contribution was not saved.");
                        if (Q.Execute(c, t, @"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,plus,durability,maximdurability,color)
VALUES(@item,@type,@uid,0,0,1,0,1,@dur,@max,@color)", "item", (long)item.UID, "type", (int)item.ID, "uid", (long)p.Entity.UID,
                            "dur", (int)item.Durability, "max", (int)item.MaximDurability, "color", (int)item.Color) != 1) throw new InvalidOperationException("Monster equipment was not saved.");
                        if (Q.Execute(c, t, @"INSERT INTO revora_monster_equipment_pickups(id,item_uid,item_type,monster_uid,monster_type,map,collector,mentor,points)
VALUES(@id,@item,@type,@monster,@family,@map,@uid,@mentor,@points)", "id", origin.Id, "item", (long)item.UID, "type", (int)item.ID,
                            "monster", (long)origin.Monster, "family", (long)origin.Type, "map", (int)map.ID, "uid", (long)p.Entity.UID, "mentor", (long)mentor, "points", mentor == 0 ? 0 : 1) != 1)
                            throw new InvalidOperationException("Monster pickup was not recorded.");
                        t.Commit();
                    }
                }
                drop.PickedUpAlready = true; drop.Type = FloorItem.Remove; map.RemoveFloorItem(drop); item.MobDropped = false;
                Q.Install(p, new uint[0], new[] { item });
                if (mentor != 0)
                {
                    ushort left = MentorExperience.ContributionView(actual + 1), lifetime = MentorExperience.ContributionView(total + 1);
                    if (p.AsApprentice != null) { p.AsApprentice.Actual_Plus = left; p.AsApprentice.Total_Plus = lifetime; }
                    GameState parent; Game.ConquerStructures.Society.Apprentice app;
                    if (Kernel.GamePool.TryGetValue(mentor, out parent) && Q.Player(parent, false))
                    {
                        if (parent.Apprentices.TryGetValue(p.Entity.UID, out app)) { app.Actual_Plus = left; app.Total_Plus = lifetime; }
                        MentorStoneClaims.Refresh(parent);
                    }
                }
                p.RemoveScreenSpawn(drop, true); p.Send(Constants.PickupItem(ConquerItemInformation.BaseInformations[item.ID].Name));
            }
            catch (Exception ex) { Q.Fail(p, "monster-equipment-pickup", ex); }
            return true;
        }
    }
}
