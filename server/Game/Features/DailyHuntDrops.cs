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
    internal static class DailyHuntDrops
    {
        private sealed class Source { internal uint Family, Item; internal int OwnRate; internal ushort Map; internal string Name; }
        private sealed class Life { internal Time32 Stamp; internal bool Rolled; }
        private sealed class Origin {
            internal Guid Id;
            internal uint Monster, Family, ItemUid, ItemType, FloorUid, Owner;
            internal ushort Map, X, Y, Durability;
            internal ConquerItem Item;
        }
        private static Dictionary<uint, Source> sources;
        private static readonly ConditionalWeakTable<Entity, Life> lives = new ConditionalWeakTable<Entity, Life>();
        private static readonly ConditionalWeakTable<FloorItem, Origin> origins = new ConditionalWeakTable<FloorItem, Origin>();
        internal static Func<int, int> Roll = max => Kernel.Random.Next(max);

        internal static void Initialize() {
            var found = new Dictionary<uint, Source>();
            Add(found, 1015, Config.DailyPriceBanditBlade, "BanditL97", "BanditL98");
            Add(found, 1020, Config.DailyPriceBloodyTeeth, "Macaque", "MacaqueL48");
            Add(found, 1000, Config.DailyPriceBatBlood, "SandMonster", "SandMonsterL68");
            Add(found, 1011, Config.DailyPriceBladeScrap, "Bandit", "BanditL33");
            // Preserve the installed HillMonster own-item rate in the owned pickup path.
            Add(found, 1000, 1080001, "HillMonster");
            using (var c = DataHolder.MySqlConnection) { c.Open(); Q.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_daily_hunt_pickups(
id uuid PRIMARY KEY,item_uid bigint NOT NULL UNIQUE CHECK(item_uid>0),item_type bigint NOT NULL,
monster_uid bigint NOT NULL,monster_type bigint NOT NULL,map integer NOT NULL,
collector bigint NOT NULL CHECK(collector>0),picked_at timestamptz NOT NULL DEFAULT now());"); }
            sources = found;
        }
        private static void Add(Dictionary<uint, Source> found, ushort map, uint item, params string[] names) {
            if (!ConquerItemInformation.BaseInformations.ContainsKey(item)) throw new InvalidOperationException("Daily hunt material is absent from the catalog.");
            foreach (string name in names) {
                var families = MonsterInformation.MonsterInformations.Values.Where(m => m.Name == name).ToArray();
                if (families.Length == 0) throw new InvalidOperationException("Daily hunt monster is absent from the catalog: " + name);
                foreach (var family in families) found.Add(family.ID, new Source { Family = family.ID, Item = item, Map = map, Name = name, OwnRate = family.OwnItemRate });
            }
        }
        internal static bool OwnsEmeraldDrop(Entity monster) {
            Source source;
            return sources!=null && monster!=null && monster.MonsterInfo!=null
                && monster.MonsterInfo.OwnItemID==1080001
                && sources.TryGetValue(monster.MonsterInfo.ID,out source) && source.Item==1080001
                && monster.MonsterInfo.Name==source.Name && monster.MapID==source.Map;
        }
        internal static void TryDrop(Entity monster, Entity killer) {
            Source source;
            if (sources == null || monster == null || monster.MonsterInfo == null || killer == null
                || !Q.Player(killer.Owner) || !ReferenceEquals(killer.Owner.Entity, killer)
                || !ReferenceEquals(monster.MonsterInfo.Owner, monster) || monster.EntityFlag != EntityFlag.Monster || !monster.Dead
                || monster.Companion || monster.MonsterInfo.Guard || monster.MonsterInfo.Reviver || monster.MonsterInfo.Boss
                || CityGuardSystem.IsCityGuard(monster) || LivingWorldSystem.IsAdventurer(monster)
                || !sources.TryGetValue(monster.MonsterInfo.ID, out source) || monster.MonsterInfo.Name != source.Name
                || monster.MapID != source.Map || monster.MapID != killer.MapID) return;
            var map = killer.Owner.Map; Entity current;
            if (map == null || map.ID != source.Map || map.BaseID != map.ID || map.Floor == null
                || !map.Entities.TryGetValue(monster.UID, out current) || !ReferenceEquals(current, monster)) return;
            lock (monster) {
                if (!monster.Dead) return;
                var life = lives.GetValue(monster, _ => new Life());
                if (life.Rolled && life.Stamp == monster.DeathStamp) return;
                life.Rolled = true; life.Stamp = monster.DeathStamp;
                int chance = source.Item == 1080001 ? Config.AdjustedItemDropRate(source.OwnRate) : Math.Min(100, (int)Config.DailyPriceHuntDropPercent);
                if (chance == 0 || Roll(100) >= chance) return;
                ushort x = monster.X, y = monster.Y;
                if (!map.SelectCoordonates(ref x, ref y)) return;
                var item = Q.New(source.Item, false);
                var drop = new FloorItem(true) { UID = FloorItem.FloorUID.Next, Item = item, ItemID = item.ID, MapID = map.ID,
                    X = x, Y = y, Owner = killer.Owner, OwnerUID = killer.UID, Type = FloorItem.Drop,
                    ValueType = FloorItem.FloorValueType.Item, OnFloor = Time32.Now, ItemColor = item.Color, MapObjType = MapObjectType.Item };
                while (map.Npcs.ContainsKey(drop.UID) || map.FloorItems.ContainsKey(drop.UID)) drop.UID = FloorItem.FloorUID.Next;
                origins.Add(drop, new Origin { Id = Guid.NewGuid(), Monster = monster.UID, Family = source.Family,
                    Item = item, ItemUid = item.UID, ItemType = item.ID, Durability = item.Durability,
                    FloorUid = drop.UID, Owner = killer.UID, Map = map.ID, X = x, Y = y });
                map.AddFloorItem(drop); monster.MonsterInfo.SendScreenSpawn(drop);
            }
        }
        internal static bool Managed(FloorItem drop) { Origin origin; return drop != null && origins.TryGetValue(drop, out origin); }
        internal static bool Pickup(GameState p, FloorItem drop) {
            Origin origin; if (drop == null || !origins.TryGetValue(drop, out origin)) return false;
            if (!Q.Player(p)) return true;
            lock (p.ItemSyncRoot) lock (drop) try {
                var map = p.Map; FloorItem current;
                if (!Q.Player(p) || p.Booth != null || p.Trade == null || p.Trade.InTrade || map == null
                    || map.ID != origin.Map || map.BaseID != map.ID || p.Entity.X != origin.X || p.Entity.Y != origin.Y
                    || drop.PickedUpAlready || drop.Type != FloorItem.Drop || Time32.Now >= drop.OnFloor.AddSeconds(Constants.FloorItemSeconds)
                    || !map.FloorItems.TryGetValue(origin.FloorUid, out current) || !ReferenceEquals(current, drop)) return true;
                if (origin.Owner != p.Entity.UID && Time32.Now < drop.OnFloor.AddSeconds(Constants.FloorItemAvailableAfter)
                    && !(p.Team != null && p.Team.PickupItems && p.Team.IsTeammate(origin.Owner))) { p.Send(Constants.FloorItemNotAvailable); return true; }
                var item = origin.Item;
                if (drop.UID != origin.FloorUid || drop.OwnerUID != origin.Owner || drop.MapID != origin.Map || drop.X != origin.X || drop.Y != origin.Y
                    || drop.ValueType != FloorItem.FloorValueType.Item || !ReferenceEquals(item, drop.Item) || item.UID != origin.ItemUid
                    || item.ID != origin.ItemType || drop.ItemID != item.ID || item.Bound || item.Plus != 0 || item.StackSize != 1
                    || item.Position != 0 || item.Warehouse != 0 || item.Lock != 0 || item.Suspicious || item.InWardrobe || item.Days != 0
                    || item.Durability != origin.Durability || item.MaximDurability != origin.Durability)
                    throw new InvalidOperationException("Daily hunt drop origin changed.");
                if (p.Inventory.Count >= 40) { p.Send(Constants.FullInventory); return true; }
                using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                    SkillTable.LockOwner(c, t, p);
                    using (var q = Q.Command(c, t, "SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0", "uid", (long)p.Entity.UID))
                        if (Convert.ToInt64(q.ExecuteScalar()) != p.Inventory.Count) throw new InvalidOperationException("Daily hunt inventory requires reload.");
                    if (Q.Execute(c, t, @"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability)
VALUES(@item,@type,@uid,0,0,1,0,@dur,@dur)", "item", (long)item.UID, "type", (int)item.ID, "uid", (long)p.Entity.UID, "dur", (int)item.Durability) != 1)
                        throw new InvalidOperationException("Daily hunt material was not saved.");
                    if (Q.Execute(c, t, @"INSERT INTO revora_daily_hunt_pickups(id,item_uid,item_type,monster_uid,monster_type,map,collector)
VALUES(@id,@item,@type,@monster,@family,@map,@uid)", "id", origin.Id, "item", (long)item.UID, "type", (long)item.ID,
                        "monster", (long)origin.Monster, "family", (long)origin.Family, "map", (int)origin.Map, "uid", (long)p.Entity.UID) != 1)
                        throw new InvalidOperationException("Daily hunt pickup was not recorded.");
                    if (!Q.Player(p)) throw new InvalidOperationException("Daily hunt session changed.");
                    t.Commit();
                }}
                drop.PickedUpAlready = true; drop.Type = FloorItem.Remove; map.RemoveFloorItem(drop);
                Q.Install(p, new uint[0], new[] { item }); p.RemoveScreenSpawn(drop, true);
                p.Send(Constants.PickupItem(ConquerItemInformation.BaseInformations[item.ID].Name));
            } catch (Exception ex) { Q.Fail(p, "daily-hunt-pickup", ex); }
            return true;
        }
    }
}
