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
    internal static class NinjaFlowerHunt
    {
        private sealed class Source { internal uint Family; internal ushort Map; internal string Name; }
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

        private static bool Eligible(Entity p){return NinjaEpicRules.Eligible(p.Class,p.Reborn,p.Level)
            || MonkEpicQuest.Enabled && MonkEpicRules.Eligible(p.Class,p.Reborn,p.Level)
            || TaoistEpicQuest.Enabled && TaoistEpicRules.Eligible(p.Class,p.Reborn,p.Level)
            || PirateEpicWorld.Enabled && PirateEpicRules.Eligible(p.Class,p.Reborn,p.Level);}
        internal static bool Owns(Entity body,Entity killer) {
            return sources!=null && body!=null && body.MapID==1700 && body.MonsterInfo!=null && sources.ContainsKey(body.MonsterInfo.ID)
                && killer!=null && killer.EntityFlag==EntityFlag.Player && Eligible(killer);
        }
        internal static bool Handles(uint npc){return sources!=null && npc==3604;}
        internal static void Initialize() {
            var map=Kernel.Maps[1700];Revora.Interfaces.INpc npc;
            if(map.ID!=1700 || map.BaseID!=1700 || !map.Npcs.TryGetValue(3604,out npc) || npc.MapID!=1700 || npc.X!=601 || npc.Y!=641
                || npc.Type!=Enums.NpcType.Talker || !map.Floor[603,641,MapObjectType.Player])throw new InvalidOperationException("Ninja flower passage differs from reviewed world.");
            using(var sha=System.Security.Cryptography.SHA256.Create())using(var file=System.IO.File.OpenRead(System.IO.Path.Combine(Constants.DMapsPath,"maps","1700.map")))
                if(System.BitConverter.ToString(sha.ComputeHash(file)).Replace("-","")!="8026193EC8C8CB7F1934CCF7C2E713931A0C65269C8D7A0B67209019BED17D1B")throw new InvalidOperationException("Ninja flower terrain changed.");
            foreach(uint id in new uint[]{722732,722736})if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Ninja flower catalog missing.");
            var found=new Dictionary<uint,Source>();
            for(uint family=3600;family<=3631;family++) {
                MonsterInformation info;if(!MonsterInformation.MonsterInformations.TryGetValue(family,out info))throw new InvalidOperationException("Ninja flower monster missing.");
                found.Add(family,new Source{Family=family,Map=1700,Name=info.Name});
            }
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_ninja_flower_pickups(
id uuid PRIMARY KEY,item_uid bigint NOT NULL UNIQUE CHECK(item_uid>0),item_type bigint NOT NULL CHECK(item_type IN (722732,722736)),
monster_uid bigint NOT NULL,monster_type bigint NOT NULL CHECK(monster_type BETWEEN 3600 AND 3631),map integer NOT NULL CHECK(map=1700),
collector bigint NOT NULL CHECK(collector>0),picked_at timestamptz NOT NULL DEFAULT now());");}
            sources=found;
        }
        internal static bool HandleNpc(GameState p,NpcRequest request) {
            if(sources==null || p==null || p.Entity==null || request==null || p.ItemSyncRoot==null)return false;
            bool taoist=TaoistEpicQuest.Enabled && p.ActiveNpc==10000;
            bool pirate=PirateEpicWorld.Enabled && p.ActiveNpc==4272;
            bool entering=((p.ActiveNpc==4720 || MonkEpicQuest.Enabled && p.ActiveNpc==4271 || taoist) && request.OptionID==87) || taoist && request.OptionID==154 || pirate && request.OptionID==151;
            bool home=p.ActiveNpc==3604 && (request.OptionID==90 || Eligible(p.Entity));
            if(!entering && !home)return false;
            lock(p.ItemSyncRoot) {
                if(request.NpcID!=0 && request.NpcID!=p.ActiveNpc || !Q.Near(p,p.ActiveNpc,entering?(ushort)1004:(ushort)1700)
                    || p.Map.ID!=p.Map.BaseID || p.Entity.PokerTable!=0)return true;
                try {
                    if(request.OptionID==255)return true;var d=new Npcs(p);
                    if(entering && !(pirate?PirateEpicStore.Eligible(p):taoist?TaoistEpicStore.Eligible(p):p.ActiveNpc==4271?MonkEpicStore.Eligible(p):NinjaEpicStore.Eligible(p))){d.Text("Return as a second-reborn "+(pirate?"Pirate":taoist?"Taoist":p.ActiveNpc==4271?"Monk":"Ninja")+" of level 70.");}
                    else if(entering || request.OptionID==90) {
                        if(!entering && PirateEpicWorld.Enabled && p.Entity.Class>=70 && p.Entity.Class<=75){PirateEpicWorld.Return(p);return true;}
                        if(!entering && TaoistEpicQuest.Enabled && p.Entity.Class>=100 && p.Entity.Class<=145){TaoistEpicQuest.Return(p);return true;}
                        ushort map=entering?(ushort)1700:(ushort)1004,x=entering?(ushort)603:p.Entity.Class>=60 && p.Entity.Class<=65?(ushort)78:(ushort)38,y=entering?(ushort)641:(ushort)42;
                        if(!Kernel.Maps[map].Floor[x,y,MapObjectType.Player])throw new InvalidOperationException("Ninja flower landing blocked.");
                        p.Entity.Teleport(map,x,y);return true;
                    } else {d.Text(PirateEpicWorld.Enabled && p.Entity.Class>=70 && p.Entity.Class<=75?"The Abyss demons bear Red and White Flowers. Bring three of each to Jack Sparrow.":TaoistEpicQuest.Enabled && p.Entity.Class>=100 && p.Entity.Class<=145?"The Abyss demons bear Red and White Flowers. Bring three of each to the Refined Pure Oven.":p.Entity.Class>=60 && p.Entity.Class<=65?"The Abyss demons bear Red and White Flowers. Bring three of each to Fortune Arhat.":"The Abyss demons bear Red and White Flowers. Bring four of each to the YinYang Stele.");d.Option("Return to Job Center.",90);}
                    d.Option("Farewell.",255);d.Send();
                }catch(Exception ex){Q.Fail(p,"ninja-flower-passage",ex);}
            }return true;
        }
        internal static void TryDrop(Entity monster, Entity killer) {
            Source source;
            if (!Owns(monster,killer) || monster == null || monster.MonsterInfo == null || killer == null
                || !Q.Player(killer.Owner) || !ReferenceEquals(killer.Owner.Entity, killer)
                || !ReferenceEquals(monster.MonsterInfo.Owner, monster) || monster.EntityFlag != EntityFlag.Monster || !monster.Dead
                || monster.Companion || monster.MonsterInfo.Guard || monster.MonsterInfo.Reviver
                || CityGuardSystem.IsCityGuard(monster) || LivingWorldSystem.IsAdventurer(monster)
                || !sources.TryGetValue(monster.MonsterInfo.ID, out source) || monster.MonsterInfo.Name != source.Name
                || monster.MapID != source.Map || monster.MapID != killer.MapID) return;
            var map = killer.Owner.Map; Entity current;
            if (map == null || map.ID != source.Map || map.BaseID != map.ID || map.Floor == null
                || Kernel.GetDistance(killer.X,killer.Y,monster.X,monster.Y)>Constants.pScreenDistance
                || !map.Entities.TryGetValue(monster.UID, out current) || !ReferenceEquals(current, monster)) return;
            lock (monster) {
                if (!monster.Dead) return;
                var life = lives.GetValue(monster, _ => new Life());
                if (life.Rolled && life.Stamp == monster.DeathStamp) return;
                life.Rolled = true; life.Stamp = monster.DeathStamp;
                // Revora policy: each flower has one of 24 outcomes, matching the common legacy free roll.
                int rolled=Roll(24);if(rolled<0 || rolled>=24)throw new InvalidOperationException("Ninja flower roll outside its range.");
                if(rolled>1)return;uint product=rolled==0?722732u:722736u;
                ushort x = monster.X, y = monster.Y;
                if (!map.SelectCoordonates(ref x, ref y)) return;
                var item = Q.New(product, false);
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
                    throw new InvalidOperationException("Ninja flower drop origin changed.");
                if (p.Inventory.Count >= 40) { p.Send(Constants.FullInventory); return true; }
                using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                    SkillTable.LockOwner(c, t, p);
                    using (var q = Q.Command(c, t, "SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0", "uid", (long)p.Entity.UID))
                        if (Convert.ToInt64(q.ExecuteScalar()) != p.Inventory.Count) throw new InvalidOperationException("Ninja flower inventory requires reload.");
                    if (Q.Execute(c, t, @"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability)
VALUES(@item,@type,@uid,0,0,1,0,@dur,@dur)", "item", (long)item.UID, "type", (int)item.ID, "uid", (long)p.Entity.UID, "dur", (int)item.Durability) != 1)
                        throw new InvalidOperationException("Ninja flower material was not saved.");
                    if (Q.Execute(c, t, @"INSERT INTO revora_ninja_flower_pickups(id,item_uid,item_type,monster_uid,monster_type,map,collector)
VALUES(@id,@item,@type,@monster,@family,@map,@uid)", "id", origin.Id, "item", (long)item.UID, "type", (long)item.ID,
                        "monster", (long)origin.Monster, "family", (long)origin.Family, "map", (int)origin.Map, "uid", (long)p.Entity.UID) != 1)
                        throw new InvalidOperationException("Ninja flower pickup was not recorded.");
                    if (!Q.Player(p) || p.Trade.InTrade || p.Booth!=null || p.Map!=map || p.Entity.X!=origin.X || p.Entity.Y!=origin.Y) throw new InvalidOperationException("Ninja flower session changed.");
                    t.Commit();
                }}
                drop.PickedUpAlready = true; drop.Type = FloorItem.Remove; map.RemoveFloorItem(drop);
                Q.Install(p, new uint[0], new[] { item }); p.RemoveScreenSpawn(drop, true);
                p.Send(Constants.PickupItem(ConquerItemInformation.BaseInformations[item.ID].Name));
            } catch (Exception ex) { Q.Fail(p, "ninja-flower-pickup", ex); }
            return true;
        }
    }
}
