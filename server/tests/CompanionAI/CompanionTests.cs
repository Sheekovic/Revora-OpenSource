using System;
using System.Collections;
using System.Collections.Generic;
using Console = System.Console;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.Features;
using Revora.Interfaces;
using Revora.Network.GamePackets;

internal static class CompanionTests
{
    private static int checks;
    private static uint nextUid = 1250000;
    internal static void LoadBackfire(string path)
    {
        if (SpellTable.SpellInformations.ContainsKey(12680)) return;
        foreach (string line in File.ReadLines(path))
        {
            var fields=line.Split(new[]{"@@"," "},StringSplitOptions.RemoveEmptyEntries);
            if(fields.Length>2 && fields[1]=="12680")SpellTable.Parse(line);
        }
    }
    internal static void ArmBackfire(GameState p)
    {
        // Seed the offline character before enabling normal gameplay ownership.
        p.Entity.FullyLoaded=false;p.Entity.Class=25;p.Action=2;p.Entity.FullyLoaded=true;p.Entity.MaxHitpoints=p.Entity.Hitpoints=100000;p.Entity.Stamina=0;
        p.Spells[12680]=new Spell(true){ID=12680,Level=0,Available=true};
        foreach(byte slot in new[]{ConquerItem.RightWeapon,ConquerItem.LeftWeapon})
            if(p.Equipment.TryGetItem(slot)==null)p.Equipment.Add(new ConquerItem(true){ID=624000,UID=++nextUid,Position=slot,Durability=1000,MaximDurability=1000},Enums.ItemUse.None);
        typeof(Entity).Assembly.GetType("Revora.Game.Features.WarriorModes").GetMethod("Cast",Private).Invoke(null,new object[]{p,(ushort)12680});
        if(!p.Entity.OnBackFire())throw new Exception("Backfire fixture did not arm.");
    }
    internal static bool CounterBackfire(Entity from,Entity to)
    {
        return (bool)typeof(Entity).Assembly.GetType("Revora.Game.Features.WarriorModes").GetMethod("Counter",Private)
            .Invoke(null,new object[]{from,to,new Attack(true){AttackType=Attack.Melee},1u,Time32.Now});
    }
    private static readonly Type SystemType = typeof(LivingWorldSystem);
    private static readonly Type ActorType = SystemType.GetNestedType("Wanderer", BindingFlags.NonPublic);
    private static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Static;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAILED: " + name);
        checks++;
        Console.WriteLine("PASS: " + name);
    }
    private static object Call(string method, params object[] args)
    {
        try { return SystemType.GetMethod(method, Private).Invoke(null, args); }
        catch (TargetInvocationException ex) { throw new Exception(method, ex.InnerException); }
    }
    private static void Set(object actor, string field, object value) { ActorType.GetField(field).SetValue(actor, value); }
    private static T Get<T>(object actor, string field) { return (T)ActorType.GetField(field).GetValue(actor); }
    private static void Stance(object actor, string name)
    {
        FieldInfo field = ActorType.GetField("Stance");
        field.SetValue(actor, Enum.Parse(field.FieldType, name));
    }
    private static void Reset()
    {
        ((IDictionary)SystemType.GetField("ByUid", Private).GetValue(null)).Clear();
        ((IDictionary)SystemType.GetField("HiredBy", Private).GetValue(null)).Clear();
        ((IList)SystemType.GetField("Actors", Private).GetValue(null)).Clear();
        Kernel.GamePool.Clear();
        Kernel.Maps.Clear();
    }
    private static Map World(ushort id)
    {
        Map map = (Map)FormatterServices.GetUninitializedObject(typeof(Map));
        map.ID = map.BaseID = id;
        map.Floor = new Floor(80, 80, id);
        map.Entities = new System.Collections.Concurrent.SafeConcurrentDictionary<uint, Entity>();
        map.Companions = new System.Collections.Concurrent.SafeConcurrentDictionary<uint, Entity>();
        Kernel.Maps[id] = map;
        return map;
    }
    private static GameState Player(Map map, bool fake, ushort x, ushort y, byte cls)
    {
        GameState state = new GameState(null);
        state.Account = new AccountTable(null);
        state.Account.EntityID = nextUid++;
        // Do not register production pet/screen timers in an offline fixture.
        state.Screen = (Screen)FormatterServices.GetUninitializedObject(typeof(Screen));
        GC.SuppressFinalize(state.Screen);
        state.Screen.Owner = state;
        typeof(Screen).GetField("_objects", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(state.Screen, new IMapObject[0]);
        typeof(Screen).GetField("_objectDictionary", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(state.Screen,
            new System.Collections.Concurrent.ConcurrentDictionary<uint, IMapObject>());
        state.Equipment = new Revora.Game.ConquerStructures.Equipment(state);
        state.Inventory = new Revora.Game.ConquerStructures.Inventory(state);
        state.Weapons = new Tuple<ConquerItem, ConquerItem>(null, null);
        state.ArenaStatistic = new QualifierStatistic(true);
        state.Variables = new VariableVault();
        state.Entity = new Entity(EntityFlag.Player, false);
        state.Entity.Owner = state;
        state.Entity.SendUpdates = false;
        state.Entity.UID = state.Account.EntityID;
        state.Entity.Name = fake ? "[Adv] Fixture" + state.Entity.UID : "Player" + state.Entity.UID;
        state.Entity.Body = 1003;
        state.Entity.Level = 140;
        state.Entity.Class = cls;
        state.Entity.MaxHitpoints = state.Entity.Hitpoints = 10000;
        state.Entity.MaxMana = state.Entity.Mana = 800;
        state.Entity.MinAttack = 1000;
        state.Entity.MaxAttack = 1200;
        state.Entity.MagicAttack = 1200;
        state.Entity.Stamina = 100;
        state.Entity.X = x; state.Entity.Y = y;
        state.Spells = new SafeDictionary<ushort, ISkill>();
        state.Proficiencies = new SafeDictionary<ushort, IProf>();
        state.Friends = new SafeDictionary<uint, Revora.Game.ConquerStructures.Society.Friend>();
        state.Enemy = new SafeDictionary<uint, Revora.Game.ConquerStructures.Society.Enemy>();
        state.BindWorldMap(map.ID);
        state.Socket.Alive = false;
        state.Fake = fake;
        if(!fake && Environment.GetEnvironmentVariable("REVORA_TEST_HEALTH_LEASE")=="1")
            typeof(Entity).GetField("HealthSynchronizationRequired",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(state.Entity,true);
        Kernel.GamePool[state.Entity.UID] = state;
        return state;
    }
    private static object Actor(GameState state)
    {
        object actor = Activator.CreateInstance(ActorType, true);
        Set(actor, "State", state); Set(actor, "Class", state.Entity.Class);
        Set(actor, "Name", state.Entity.Name); Set(actor, "MapId", state.Entity.MapID);
        Set(actor, "HomeMapId", state.Entity.MapID);
        Set(actor, "HomeX", state.Entity.X); Set(actor, "HomeY", state.Entity.Y);
        Call("AssignCombat", actor);
        ((IDictionary)SystemType.GetField("ByUid", Private).GetValue(null))[state.Entity.UID] = actor;
        ((IList)SystemType.GetField("Actors", Private).GetValue(null)).Add(actor);
        return actor;
    }
    private static Entity Monster(Map map, ushort x, ushort y, uint threat)
    {
        Entity mob = new Entity(EntityFlag.Monster, false);
        mob.SendUpdates = false;
        mob.UID = nextUid++;
        mob.Name = "FixtureRat";
        mob.MapID = map.ID;
        mob.X = x; mob.Y = y; mob.Level = 140;
        mob.MonsterInfo = new MonsterInformation { Owner = mob, Name = mob.Name, Level = 140, InSight = threat, ID = 1 };
        mob.MaxHitpoints = mob.Hitpoints = 100000;
        mob.Defence = 50;
        map.Entities[mob.UID] = mob;
        return mob;
    }
    private static void Command(GameState owner, object actor, string command)
    {
        Set(actor, "LastCommand", DateTime.MinValue);
        var message = new Message(command, Get<string>(actor, "Name"), owner.Entity.Name, Color.White, Message.Whisper);
        if (!LivingWorldSystem.TryHandleWhisper(owner, message)) throw new Exception("Whisper not handled");
    }
    private static void Navigation()
    {
        var nav = new CompanionNavigation();
        object world = new object();
        DateTime now = DateTime.UtcNow;
        Func<int, int, bool> open = (x, y) => x > 0 && y > 0 && x < 24 && y < 24 && !(x == 8 && y < 12);
        Point start = new Point(5, 5), goal = new Point(12, 5), p = start, next;
        bool sawDetour = false;
        for (int i = 0; i < 80 && p != goal; i++)
        {
            if (nav.Next(world, p, goal, now.AddSeconds(i), open, out next))
            {
                if (!CompanionNavigation.CanStep(p, next, open)) throw new Exception("Route crossed a wall");
                p = next;
                if (p.Y >= 12) sawDetour = true;
            }
            if (nav.LastExpanded > CompanionNavigation.SearchBudget) throw new Exception("Unbounded search");
        }
        Check(p == goal && sawDetour, "cached route goes around wall and reaches destination");
        Check(!CompanionNavigation.HasSight(start, goal, open), "ranged attacks cannot see through wall");
        Check(!CompanionNavigation.CanStep(new Point(1, 1), new Point(2, 2), (x, y) => x == y), "no diagonal corner cutting");
        Check(!CompanionNavigation.CanStep(new Point(1, 1), new Point(0, 1), open), "map lower boundary rejected");
        nav.Reset();
        Check(!nav.Next(world, start, goal, now, (x, y) => x == 5 && y == 5, out next), "boxed actor stays still");
        int calls = 0;
        nav.Next(world, start, new Point(13, 5), now.AddMilliseconds(100), (x, y) => { calls++; return true; }, out next);
        Check(calls == 0, "moving destination cannot bypass failed-route backoff");
        Check(nav.Next(new object(), start, goal, now.AddMilliseconds(200), (x, y) => true, out next), "map change discards stale route and backoff");
        Check(nav.LastExpanded <= 256, "search expansion budget enforced");
        Check(CompanionTactics.Retreat(false, 20, 100) && CompanionTactics.Retreat(true, 50, 100)
            && !CompanionTactics.Retreat(true, 60, 100), "retreat thresholds have hysteresis");
    }
    private static void Integration()
    {
        Reset();
        Map map = World(1002);
        GameState owner = Player(map, false, 20, 20, 15);
        GameState other = Player(map, false, 21, 20, 15);
        GameState body = Player(map, true, 22, 20, 25);
        object actor = Actor(body);
        Command(owner, actor, "hire");
        Check(Get<uint>(actor, "OwnerUid") == owner.Entity.UID, "ordinary non-GM player can hire");
        Command(other, actor, "passive");
        Check(Get<object>(actor, "Stance").ToString() == "Assist", "other player cannot change companion stance");
        Entity idle = Monster(map, 23, 20, 0);
        Check(Call("SelectTarget", actor, body.Entity, owner.Entity, DateTime.UtcNow) == null, "assist does not attack nearby neutral monsters");
        uint ownerDamage = 100;
        Revora.Game.Attacking.Handle.ReceiveAttack(owner.Entity, idle,
            new Attack(true) { Attacker = owner.Entity.UID, Attacked = idle.UID, AttackType = Attack.Melee }, ref ownerDamage, null);
        Check(Call("SelectTarget", actor, body.Entity, owner.Entity, DateTime.UtcNow) == idle, "assist selects owner's accepted attack");
        Check(Call("SelectTarget", actor, body.Entity, owner.Entity, DateTime.UtcNow.AddSeconds(7)) == null,
            "old owner target expires instead of becoming permanent aggression");
        Entity threat = Monster(map, 24, 20, owner.Entity.UID);
        Command(owner, actor, "protect me");
        Check(Call("SelectTarget", actor, body.Entity, owner.Entity, DateTime.UtcNow) == threat, "protect selects threat to owner");
        threat.MonsterInfo.InSight = other.Entity.UID;
        Check(Call("SelectTarget", actor, body.Entity, owner.Entity, DateTime.UtcNow) == null, "ignores another player's engaged monster");
        threat.MonsterInfo.InSight = owner.Entity.UID;
        Command(owner, actor, "passive");
        uint hp = threat.Hitpoints;
        Call("DriveHired", actor, body.Entity, DateTime.UtcNow);
        Check(threat.Hitpoints == hp && Get<uint>(actor, "TargetUid") == 0, "passive neither acquires nor damages targets");
        Command(owner, actor, "protect");
        Command(owner, actor, "hold here");
        Point held = new Point(body.Entity.X, body.Entity.Y);
        threat.X = 28;
        Call("DriveHired", actor, body.Entity, DateTime.UtcNow);
        Check(new Point(body.Entity.X, body.Entity.Y) == held, "hold does not chase beyond weapon range");
        threat.X = 23;
        Call("DriveHired", actor, body.Entity, DateTime.UtcNow);
        Check(threat.Hitpoints < hp, "warrior deals calculated melee damage while holding");
        Check(threat.MonsterInfo.InSight == body.Entity.UID, "warrior intercepts a monster attacking owner");
        hp = threat.Hitpoints;
        Call("Strike", actor, body.Entity, threat);
        Check(threat.Hitpoints == hp, "attack cooldown blocks repeated damage");
        Command(owner, actor, "retreat");
        Check(Get<object>(actor, "Stance").ToString() == "Passive" && Get<byte>(actor, "Duty") == 1, "retreat cancels hold and combat");
        Command(owner, actor, "follow");
        owner.Entity.Hitpoints = 0;
        hp = threat.Hitpoints;
        Call("DriveHired", actor, body.Entity, DateTime.UtcNow);
        Check(threat.Hitpoints == hp, "dead owner cannot continue companion attacks");
        owner.Entity.Hitpoints = owner.Entity.MaxHitpoints;
        Map nextMap = World(1011);
        owner.BindWorldMap(nextMap.ID);
        Call("DriveHired", actor, body.Entity, DateTime.UtcNow);
        Check(body.Entity.MapID == nextMap.ID && Get<uint>(actor, "TargetUid") == 0, "map transition follows owner and clears target");
        Command(owner, actor, "hold");
        owner.BindWorldMap(map.ID);
        Call("DriveHired", actor, body.Entity, DateTime.UtcNow);
        Check(body.Entity.MapID == nextMap.ID, "hold remains on its original map");
        LivingWorldSystem.OnPlayerLogout(owner.Entity.UID);
        Check(Get<uint>(actor, "OwnerUid") == 0 && Get<uint>(actor, "TargetUid") == 0, "logout releases ownership and clears intent");
        // One player racing two different companions still gets exactly one.
        body.BindWorldMap(map.ID); body.Entity.X = 22; body.Entity.Y = 20; Set(actor, "MapId", map.ID);
        object second = Actor(Player(map, true, 22, 21, 15));
        Parallel.Invoke(() => Command(owner, actor, "hire"), () => Command(owner, second, "hire"));
        Check((Get<uint>(actor, "OwnerUid") == owner.Entity.UID ? 1 : 0)
            + (Get<uint>(second, "OwnerUid") == owner.Entity.UID ? 1 : 0) == 1, "concurrent hires preserve one companion per owner");
        LivingWorldSystem.OnPlayerLogout(owner.Entity.UID);
        Set(actor, "LastCommand", DateTime.MinValue);
        Parallel.Invoke(() => Command(owner, actor, "hire"), () => Command(other, actor, "hire"));
        Check(((IDictionary)SystemType.GetField("HiredBy", Private).GetValue(null)).Count == 1,
            "two players cannot simultaneously own one companion");
    }
    private static void Combat()
    {
        Reset();
        Map map = World(1002);
        GameState owner = Player(map, false, 20, 20, 15);
        GameState water = Player(map, true, 22, 20, 135);
        object actor = Actor(water);
        Call("GiveSkill", water, (ushort)1005);
        Call("GiveSkill", water, (ushort)1000);
        Command(owner, actor, "hire");
        owner.Entity.Hitpoints = 1000;
        Check((bool)Call("TryHeal", actor, water.Entity, owner.Entity, DateTime.UtcNow), "Water Tao actually heals an ordinary player");
        Check(owner.Entity.Hitpoints == 9000 && water.Entity.Mana == 670, "Cure applies table power and charges table mana");
        Check(!(bool)Call("TryHeal", actor, water.Entity, owner.Entity, DateTime.UtcNow), "healing respects shared action cooldown");
        Set(actor, "NextCombat", DateTime.MinValue);
        owner.Entity.Hitpoints = 1000; water.Entity.Mana = 0;
        Check(!(bool)Call("TryHeal", actor, water.Entity, owner.Entity, DateTime.UtcNow) && owner.Entity.Hitpoints == 1000,
            "no free healing when mana exhausted");
        water.Entity.Mana = 800;
        map.Floor.Locations[21, 20] = Floor.DMapPointFlag.Invalid;
        Check(!(bool)Call("TryHeal", actor, water.Entity, owner.Entity, DateTime.UtcNow), "healing cannot cross a wall");
        map.Floor.Locations[21, 20] = 0;
        Command(owner, actor, "passive");
        Check(!(bool)Call("TryHeal", actor, water.Entity, owner.Entity, DateTime.UtcNow), "passive suppresses healing too");
        Command(owner, actor, "assist");
        Entity mob = Monster(map, 25, 20, owner.Entity.UID);
        uint hp = mob.Hitpoints;
        Set(actor, "NextCombat", DateTime.MinValue);
        Call("Strike", actor, water.Entity, mob);
        Check(mob.Hitpoints < hp && water.Entity.Mana == 783, "Thunder applies calculated magic damage and costs 17 mana");
        GameState fire = Player(map, true, 22, 21, 145);
        object fireActor = Actor(fire);
        Call("GiveSkill", fire, (ushort)1002);
        Set(fireActor, "OwnerUid", owner.Entity.UID);
        hp = mob.Hitpoints;
        Call("Strike", fireActor, fire.Entity, mob);
        Check(mob.Hitpoints < hp && fire.Entity.Mana == 736, "Fire Tao casts Tornado with its actual mana cost");
        GameState archer = Player(map, true, 22, 22, 45);
        object bowActor = Actor(archer);
        Set(bowActor, "OwnerUid", owner.Entity.UID);
        hp = mob.Hitpoints;
        Call("Strike", bowActor, archer.Entity, mob);
        Check(mob.Hitpoints < hp, "archer uses ranged damage calculation");
        var equipment = (ConquerItem[])typeof(Revora.Game.ConquerStructures.Equipment)
            .GetField("objects", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(archer.Equipment);
        equipment[4] = new ConquerItem(true) { ID = 500429, Durability = 100, MaximDurability = 100 };
        var durability = typeof(Revora.Game.Attacking.Calculate).GetMethod("Durability", Private);
        for (int i = 0; i < 100; i++) durability.Invoke(null, new object[] { archer.Entity, mob, null });
        Check(equipment[4].Durability == 100, "synthetic equipment never enters durability persistence path");
        equipment[4] = null;
        Set(bowActor, "NextCombat", DateTime.MinValue);
        hp = owner.Entity.Hitpoints;
        Call("Strike", bowActor, archer.Entity, owner.Entity);
        Check(owner.Entity.Hitpoints == hp, "companion attack cannot damage players");
        mob.MonsterInfo.Boss = true;
        hp = mob.Hitpoints;
        Call("Strike", bowActor, archer.Entity, mob);
        Check(mob.Hitpoints == hp, "boss exclusion preserved");
        mob.MonsterInfo.Boss = false;
        mob.X = 23; mob.Y = 22;
        LivingWorldSystem.ObservePlayerAttack(owner.Entity, mob, 1);
        Stance(bowActor, "Protect");
        Set(bowActor, "Duty", (byte)1);
        hp = mob.Hitpoints;
        Call("DriveHired", bowActor, archer.Entity, DateTime.UtcNow);
        mob.X = (ushort)(archer.Entity.X + 1); mob.Y = archer.Entity.Y;
        Call("DriveHired", bowActor, archer.Entity, DateTime.UtcNow);
        Check(mob.Hitpoints < hp, "ranged companion fires between retreat steps while pursued");
        Set(bowActor, "NextCombat", DateTime.MinValue);
        mob.Hitpoints = 1;
        byte xp = archer.XPCount;
        Call("Strike", bowActor, archer.Entity, mob);
        Check(mob.Dead && archer.XPCount == xp, "finishing blow uses death cleanup without synthetic XP awards");
    }
    private static void Recovery()
    {
        Reset();
        Map map = World(1002);
        GameState owner = Player(map, false, 20, 20, 15);
        GameState body = Player(map, true, 22, 20, 45);
        object actor = Actor(body);
        Command(owner, actor, "hire");
        for (int y = 15; y <= 25; y++) map.Floor.Locations[26, y] = Floor.DMapPointFlag.Invalid;
        for (int step = 0; step < 40 && (body.Entity.X != 32 || body.Entity.Y != 20); step++)
        {
            Point previous = new Point(body.Entity.X, body.Entity.Y);
            Call("MoveToward", body.Entity, (ushort)32, (ushort)20);
            Point current = new Point(body.Entity.X, body.Entity.Y);
            int limit = body.Entity.Action == Enums.ConquerAction.Jump ? 6 : 2;
            if (CompanionNavigation.Distance(previous, current) > limit)
                throw new Exception("Movement exceeded walk/jump budget");
            if (body.Entity.Action == Enums.ConquerAction.Jump && !CompanionNavigation.HasSight(previous, current,
                (x, y) => map.Floor[x, y, MapObjectType.Player, null]))
                throw new Exception("Jump crossed blocked terrain");
            if (!map.Floor[body.Entity.X, body.Entity.Y, MapObjectType.Player, null])
                throw new Exception("Actor entered blocked terrain");
        }
        Check(body.Entity.X == 32 && body.Entity.Y == 20, "actual Entity.Move follows obstacle route within step budget");
        for (int y = 15; y <= 25; y++) map.Floor.Locations[26, y] = 0;
        owner.Entity.X = 60;
        Point before = new Point(body.Entity.X, body.Entity.Y);
        Call("DriveHired", actor, body.Entity, DateTime.UtcNow);
        Check(Kernel.GetDistance(body.Entity.X, body.Entity.Y, owner.Entity.X, owner.Entity.Y) > 2
            && CompanionNavigation.Distance(before, new Point(body.Entity.X, body.Entity.Y)) <= 6
            && body.Entity.Action == Enums.ConquerAction.Jump,
            "distant following jumps on clear ground before teleport recovery");
        Set(actor, "SeparatedSince", DateTime.UtcNow.AddSeconds(-9));
        Call("DriveHired", actor, body.Entity, DateTime.UtcNow);
        Check(Kernel.GetDistance(body.Entity.X, body.Entity.Y, owner.Entity.X, owner.Entity.Y) <= 2,
            "prolonged separation recovers near owner on valid terrain");
        body.Entity.X = 20; body.Entity.Y = 20;
        Entity chasing = Monster(map, 21, 20, body.Entity.UID);
        Set(actor, "SeparatedSince", DateTime.UtcNow.AddSeconds(-9));
        Call("DriveHired", actor, body.Entity, DateTime.UtcNow);
        Check(Kernel.GetDistance(body.Entity.X, body.Entity.Y, owner.Entity.X, owner.Entity.Y) > 2,
            "recovery cannot teleport out of monster combat");
        chasing.MonsterInfo.InSight = 0;
        owner.Entity.X = body.Entity.X; owner.Entity.Y = body.Entity.Y;
        Command(owner, actor, "hold");
        ushort anchorX = body.Entity.X, anchorY = body.Entity.Y;
        body.Entity.X += 2;
        Call("DriveHired", actor, body.Entity, DateTime.UtcNow);
        Check(body.Entity.X == anchorX && body.Entity.Y == anchorY, "displaced holder returns to saved anchor");
        Command(owner, actor, "follow");
        Map blocked = World(1011);
        owner.BindWorldMap(blocked.ID);
        owner.Entity.X = owner.Entity.Y = 40;
        for (int x = 38; x <= 42; x++)
            for (int y = 38; y <= 42; y++) blocked.Floor.Locations[x, y] = Floor.DMapPointFlag.Invalid;
        Call("DriveHired", actor, body.Entity, DateTime.UtcNow);
        Check(body.Entity.MapID == map.ID, "map transfer refuses an entirely blocked destination");
        blocked.BaseID = map.ID;
        Call("DriveHired", actor, body.Entity, DateTime.UtcNow);
        Check(Get<uint>(actor, "OwnerUid") == 0 && body.Entity.MapID == map.ID,
            "dynamic event entry releases companion without bypassing entry rules");
        owner.BindWorldMap(map.ID); owner.Entity.X = body.Entity.X; owner.Entity.Y = body.Entity.Y;
        Command(owner, actor, "hire");
        Set(actor, "TargetUid", chasing.UID);
        LivingWorldSystem.OnAdventurerKilled(body.Entity, chasing);
        Check(Get<uint>(actor, "TargetUid") == 0 && body.Entity.Hitpoints == 0
            && Get<DateTime>(actor, "ReviveAt") > DateTime.UtcNow.AddSeconds(8),
            "death clears intent and schedules bounded revival delay");
        LivingWorldSystem.OnPlayerLogout(owner.Entity.UID);
        Check(Get<uint>(actor, "OwnerUid") == 0, "logout also releases a dead companion");
    }
    public static int Main(string[] args)
    {
        try
        {
            foreach (string line in File.ReadLines(args[0]))
            {
                string[] fields = line.Split(new[] { "@@", " " }, StringSplitOptions.RemoveEmptyEntries);
                if (fields.Length > 2 && (fields[1] == "1000" || fields[1] == "1002" || fields[1] == "1005"))
                    SpellTable.Parse(line);
            }
            Navigation(); Integration(); Combat(); Recovery(); Jumping();
            Console.WriteLine("Passed " + checks + " companion checks; no live server or database used.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { Reset(); }
    }

    private static void Jumping()
    {
        Reset();
        Map map = World(1002);
        GameState body = Player(map, true, 20, 20, 15);
        Actor(body);
        // Production adventurers are FullyLoaded, but have no persisted character.
        // Leave the database unconfigured so any accidental movement write fails.
        body.Entity.FullyLoaded = true;
        Call("MoveToward", body.Entity, (ushort)40, (ushort)20);
        Check(body.Entity.X == 26 && body.Entity.Y == 20 && body.Entity.Action == Enums.ConquerAction.Jump
            && body.Entity.PX == 20 && body.Entity.PY == 20, "clear catch-up sends a six-tile jump with its old position");
        Call("MoveToward", body.Entity, (ushort)28, (ushort)20);
        Check(body.Entity.X == 28 && body.Entity.Action != Enums.ConquerAction.Jump,
            "close approach walks without overshooting");
        var jump = typeof(WorldActorMotion).GetMethod("JumpClear", Private);
        body.Entity.X = body.Entity.Y = 20;
        map.Floor.Locations[23, 20] = Floor.DMapPointFlag.Invalid;
        Check(!(bool)jump.Invoke(null, new object[] { body.Entity, (ushort)26, (ushort)20 }) && body.Entity.X == 20,
            "blocked jump is rejected before changing position");
        map.Floor.Locations[23, 20] = 0;
        map.Floor.Locations[21, 20] = Floor.DMapPointFlag.Invalid;
        Check(!(bool)jump.Invoke(null, new object[] { body.Entity, (ushort)25, (ushort)25 }),
            "diagonal jumps cannot cut blocked corners");
        map.Floor.Locations[21, 20] = 0;
        Check(!(bool)jump.Invoke(null, new object[] { body.Entity, (ushort)27, (ushort)20 }),
            "jump cannot exceed six-tile limit");
        body.Entity.Hitpoints = 0;
        Check(!(bool)jump.Invoke(null, new object[] { body.Entity, (ushort)26, (ushort)20 }),
            "dead companion cannot jump");
    }
}
