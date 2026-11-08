using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Game;
using Revora.Game.ConquerStructures;
using Revora.Network.GamePackets;
using Console = System.Console;
using Trade = Revora.Game.ConquerStructures.Trade;
using Team = Revora.Game.ConquerStructures.Team;

internal static class GoldPickupTests
{
    private static int checks;
    private static uint uid = 7000;
    private static readonly BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    private static readonly MethodInfo Collect = typeof(Entity).Assembly.GetType("Revora.Game.Features.GoldPickup")
        .GetMethod("TryCollect", Static);
    private static void Check(bool passed, string name)
    {
        if (!passed) throw new Exception("FAILED: " + name);
        checks++; Console.WriteLine("PASS: " + name);
    }
    private static object Fixture(string name, params object[] args)
    {
        return typeof(CompanionTests).GetMethod(name, Static).Invoke(null, args);
    }
    private static bool Pick(GameState client, FloorItem item, bool automatic)
    {
        try { return (bool)Collect.Invoke(null, new object[] { client, item, automatic }); }
        catch (TargetInvocationException ex) { throw new Exception("Gold collection", ex.InnerException); }
    }
    private static GameState Player(Map map)
    {
        var client = (GameState)Fixture("Player", map, false, (ushort)20, (ushort)20, (byte)15);
        client.Action = 2;
        client.ItemSyncRoot = new object();
        client.Trade = new Trade();
        client.Entity.InAutoHunt = true;
        client.Entity.VIPLevel = 1;
        return client;
    }
    private static FloorItem Drop(Map map, GameState viewer, GameState owner, ushort x = 22, ushort y = 20)
    {
        var item = new FloorItem(true) { UID = uid++, MapID = map.ID, MapObjType = MapObjectType.Item,
            X = x, Y = y, Owner = owner, Value = 5000, ValueType = FloorItem.FloorValueType.Money,
            ItemID = ItemHandler.MoneyItemID(5000), Type = FloorItem.Drop, OnFloor = Time32.Now };
        map.AddFloorItem(item);
        viewer.Screen.Add(item);
        return item;
    }
    public static int Main()
    {
        try
        {
            Fixture("Reset");
            var map = (Map)Fixture("World", (ushort)1002);
            map.FloorItems = new ConcurrentDictionary<uint, FloorItem>();
            GameState player = Player(map), other = Player(map);
            FloorItem item = Drop(map, player, player);
            Check(Pick(player, item, true) && player.Entity.Money == 5000, "auto-hunt credits nearby owned gold");
            Check(item.PickedUpAlready && !map.FloorItems.ContainsKey(item.UID)
                && !player.Screen.Contains(item.UID), "collected pile leaves map and player screen");
            Check(!Pick(player, item, true) && player.Entity.Money == 5000, "duplicate request cannot credit twice");
            item = Drop(map, player, player);
            Check(!Pick(player, item, false), "manual pickup still requires exact tile");
            player.Entity.X = item.X;
            ItemHandler.PickupItem(item, player);
            Check(player.Entity.Money == 10000 && item.PickedUpAlready, "manual handler shares protected gold collection");
            player.Entity.X = 20;
            item = Drop(map, player, other);
            Check(!Pick(player, item, true), "another player's protected drop remains on ground");
            item.OnFloor = Time32.Now.AddSeconds(-16);
            Check(Pick(player, item, true), "ownership protection expires at existing timeout");
            item = Drop(map, player, null);
            Check(Pick(player, item, true), "public gold can be collected");
            item = Drop(map, player, player, 24);
            Check(!Pick(player, item, true), "auto-hunt cannot collect beyond three tiles");
            item = Drop(map, player, player);
            map.Floor.Locations[21, 20] = Floor.DMapPointFlag.Invalid;
            Check(!Pick(player, item, true), "gold behind a wall is not collected");
            map.Floor.Locations[21, 20] = 0;
            player.Entity.InAutoHunt = false;
            Check(!Pick(player, item, true), "stopped auto-hunt cannot collect automatically");
            player.Entity.InAutoHunt = true;
            player.Entity.VIPLevel = 0;
            Check(!Pick(player, item, true), "existing VIP eligibility remains enforced");
            player.Entity.VIPLevel = 1;
            player.Entity.Hitpoints = 0;
            Check(!Pick(player, item, true), "dead characters cannot collect");
            player.Entity.Hitpoints = player.Entity.MaxHitpoints;
            player.Trade.InTrade = true;
            Check(!Pick(player, item, true), "trading blocks automatic collection");
            player.Trade.InTrade = false;
            item.MapID = 1011;
            Check(!Pick(player, item, true), "cross-map pickup rejected");
            item.MapID = map.ID;
            item.ValueType = FloorItem.FloorValueType.ConquerPoints;
            Check(!Pick(player, item, true), "automatic gold collection does not take CPs");
            item.ValueType = FloorItem.FloorValueType.Item;
            Check(!Pick(player, item, true), "automatic gold collection does not take equipment");
            item.ValueType = FloorItem.FloorValueType.Money;
            item.OnFloor = Time32.Now.AddSeconds(-21);
            Check(!Pick(player, item, true), "expired piles cannot be credited from stale screens");
            item.OnFloor = Time32.Now;
            map.RemoveFloorItem(item);
            Check(!Pick(player, item, true), "removed piles cannot be credited from stale screens");
            item = Drop(map, player, null);
            ulong previous = player.Entity.Money;
            player.Entity.Money = Revora.Game.Entity.MaximumSilver - 100;
            Check(!Pick(player, item, true) && map.FloorItems.ContainsKey(item.UID), "balance overflow retains ground gold");
            player.Entity.Money = previous;
            other.Screen.Add(item);
            ulong before = player.Entity.Money + other.Entity.Money;
            Parallel.Invoke(() => Pick(player, item, true), () => Pick(other, item, true));
            Check(player.Entity.Money + other.Entity.Money == before + 5000, "two collectors credit a public pile exactly once");
            item = Drop(map, player, null, 20);
            before = player.Entity.Money;
            Parallel.Invoke(() => ItemHandler.PickupItem(item, player), () => Pick(player, item, true));
            Check(player.Entity.Money == before + 5000, "manual and automatic race credits once");
            var first = Drop(map, player, player);
            var second = Drop(map, player, player, 21);
            before = player.Entity.Money;
            Parallel.Invoke(() => Pick(player, first, true), () => Pick(player, second, true));
            Check(player.Entity.Money == before + 10000, "concurrent distinct piles do not lose balance updates");
            // Team construction normally creates an event map; only its membership is needed here.
            player.Team = (Team)FormatterServices.GetUninitializedObject(typeof(Team));
            player.Team.Members = new ConcurrentDictionary<uint, Team.Member>();
            player.Team.Members[other.Entity.UID] = new Team.Member();
            item = Drop(map, player, other);
            player.Team.PickupMoney = false;
            Check(!Pick(player, item, true), "team money restriction is respected");
            player.Team.PickupMoney = true;
            Check(Pick(player, item, true), "team money sharing permits protected teammate gold");
            Console.WriteLine("Passed " + checks + " gold pickup checks; no live database or sockets used.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { Fixture("Reset"); }
    }
}
