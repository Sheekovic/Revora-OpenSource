using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type AttributeService = typeof(Entity).Assembly.GetType("Revora.Game.Features.CharacterAttributes");
    static byte[] AttributePacket(uint strength, uint agility = 0, uint vitality = 0, uint spirit = 0)
    {
        var packet = new byte[36]; Writer.WriteUInt16(28, 0, packet); Writer.WriteUInt16(1024, 2, packet);
        Writer.WriteUInt32(strength, 12, packet); Writer.WriteUInt32(agility, 16, packet);
        Writer.WriteUInt32(vitality, 20, packet); Writer.WriteUInt32(spirit, 24, packet);
        Writer.WriteUInt64(PacketHandler.ClientSeal, 28, packet); return packet;
    }
    static ulong PointTotal(GameState p)
    { return (ulong)p.Entity.Strength + p.Entity.Agility + p.Entity.Vitality + p.Entity.Spirit + p.Entity.Atributes; }
    static void VerifyAttributes(Map map)
    {
        var p = RunePlayer(map); p.Entity.FullyLoaded = false;
        p.Entity.Strength = p.Entity.Agility = p.Entity.Vitality = p.Entity.Spirit = 100;
        p.Entity.Atributes = 100; p.Entity.FullyLoaded = true;
        Check(p.Account.State == AccountTable.AccountState.Player && !p.Fake, "attribute requests use an ordinary authenticated player");
        PacketHandler.HandlePacket(AttributePacket(1, 2, 3, 4), p);
        Check(p.Entity.Strength == 101 && p.Entity.Agility == 102 && p.Entity.Vitality == 103
            && p.Entity.Spirit == 104 && p.Entity.Atributes == 90, "production allocation spends exactly the four requested deltas");
        foreach (var values in new uint[][] { new uint[] { uint.MaxValue, 1, 0, 0 },
            new uint[] { uint.MaxValue, uint.MaxValue, 2, 0 }, new uint[] { 65536, 0, 0, 0 },
            new uint[] { 91, 0, 0, 0 }, new uint[] { 0, 0, 0, 0 } })
        {
            PacketHandler.HandlePacket(AttributePacket(values[0], values[1], values[2], values[3]), p);
            Check(p.Entity.Atributes == 90 && p.Entity.Strength == 101 && PointTotal(p) == 500,
                "overflow, overspend or zero allocation is rejected: " + string.Join(",", values));
        }
        foreach (var property in new[] { "Strength", "Agility", "Vitality", "Spirit" })
        {
            var field = typeof(Entity).GetProperty(property); object before = field.GetValue(p.Entity, null);
            field.SetValue(p.Entity, ushort.MaxValue, null);
            var deltas = new uint[4]; deltas[Array.IndexOf(new[] { "Strength", "Agility", "Vitality", "Spirit" }, property)] = 1;
            PacketHandler.HandlePacket(AttributePacket(deltas[0], deltas[1], deltas[2], deltas[3]), p);
            Check((ushort)field.GetValue(p.Entity, null) == ushort.MaxValue && p.Entity.Atributes == 90,
                "allocation cannot wrap the target " + property);
            field.SetValue(p.Entity, before, null);
        }
        for (int length = 0; length < 36; length++)
        {
            var truncated = new byte[length];
            if (length >= 12) { Writer.WriteUInt16((ushort)(length - 8), 0, truncated); Writer.WriteUInt16(1024, 2, truncated); Writer.WriteUInt64(PacketHandler.ClientSeal, length - 8, truncated); }
            PacketHandler.HandlePacket(truncated, p);
        }
        Check(p.Entity.Atributes == 90 && PointTotal(p) == 500, "all truncated 1024 frames fail without treating transport bytes as stat fields");
        var malformed = AttributePacket(1); malformed[0]++;
        Call(AttributeService, null, "Allocate", p, malformed);
        malformed = AttributePacket(1); malformed[2]++;
        Call(AttributeService, null, "Allocate", p, malformed);
        malformed = AttributePacket(1); malformed[35]++;
        Call(AttributeService, null, "Allocate", p, malformed);
        Call(AttributeService, null, "Allocate", p, new byte[65544]);
        Check(p.Entity.Atributes == 90 && PointTotal(p) == 500, "allocation rejects wrong type, framing, seal and oversized payloads");
        p.Fake = true; PacketHandler.HandlePacket(AttributePacket(1), p); p.Fake = false;
        p.Entity.FullyLoaded = false; PacketHandler.HandlePacket(AttributePacket(1), p); p.Entity.FullyLoaded = true;
        p.Action = 0; PacketHandler.HandlePacket(AttributePacket(1), p); p.Action = 2;
        typeof(Entity).GetField("PersistenceRequiresReload", Instance).SetValue(p.Entity, true);
        PacketHandler.HandlePacket(AttributePacket(1), p); DemonReset(p);
        Check(p.Entity.Atributes == 90 && PointTotal(p) == 500, "fake, unloaded, inactive and fenced sessions cannot allocate points");
        GameState detached; Kernel.GamePool.TryRemove(p.Entity.UID, out detached);
        try { PacketHandler.HandlePacket(AttributePacket(1), p); }
        finally { Kernel.GamePool[p.Entity.UID] = p; }
        Check(p.Entity.Atributes == 90 && PointTotal(p) == 500, "a detached session cannot allocate points for an otherwise valid character");
        Parallel.For(0, 200, i => PacketHandler.HandlePacket(AttributePacket(1), p));
        Check(p.Entity.Atributes == 0 && p.Entity.Strength == 191 && PointTotal(p) == 500,
            "concurrent repeated requests spend only the available ninety points");
        p.Entity.Level = 135; p.Entity.Experience = DataHolder.LevelExperience(135) - 1;
        p.Spells[1110] = new Spell(true) { ID = 1110 };
        Parallel.Invoke(() => p.IncreaseExperience(1, false),
            () => { for (int i = 0; i < 20; i++) PacketHandler.HandlePacket(AttributePacket(1), p); });
        while (p.Entity.Atributes != 0) PacketHandler.HandlePacket(AttributePacket(1), p);
        Check(p.Entity.Level == 136 && PointTotal(p) == 500UL + Config.AttributePointsPerLevel,
            "level-up and allocation share ownership and conserve newly awarded free points");
        var started = new ManualResetEventSlim(); var completed = new ManualResetEventSlim(); Task waiting;
        lock (p.ItemSyncRoot)
        {
            waiting = Task.Run(() => { started.Set(); p.IncreaseExperience(1, false); completed.Set(); });
            started.Wait(); Check(!completed.Wait(100), "EXP writer waits for the same owner lock used by character saves");
        }
        Check(waiting.Wait(5000) && p.Entity.Experience == 1, "EXP writer resumes after owner lock release without losing its award");
        started.Dispose(); completed.Dispose();
        // Copy only production column types and this fixture's synthetic identities.
        // Unrelated account-creation constraints are not part of this save fixture.
        // This mode owns the isolated schema; no production character data is read.
        Query(@"CREATE TABLE entity_save_fixture AS TABLE public.entities WITH NO DATA;
INSERT INTO entity_save_fixture(uid,name) SELECT uid,name FROM entities;
ALTER TABLE entities RENAME TO minimal_entity_fixture;
ALTER TABLE entity_save_fixture RENAME TO entities;");
        PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
        p.Entity.DailySignDays = new byte[0]; p.Entity.Atributes = 100;
        ulong expectedTotal = PointTotal(p); bool saved = EntityTable.SaveEntity(p);
        Check(saved && Scalar("SELECT strength+agility+vitality+spirit+atributes FROM entities WHERE uid=@uid", p.Entity.UID) == (long)expectedTotal,
            "production character save persists all five allocation fields using production column types");
        var saveResults = new bool[20]; var consistent = new bool[20];
        Parallel.Invoke(
            () => { for (int i = 0; i < 100; i++) { PacketHandler.HandlePacket(AttributePacket(1), p); p.IncreaseExperience(1, false); } },
            () => { for (int i = 0; i < saveResults.Length; i++) saveResults[i] = EntityTable.SaveEntity(p); },
            () => { for (int i = 0; i < consistent.Length; i++) consistent[i] = Scalar("SELECT strength+agility+vitality+spirit+atributes FROM entities WHERE uid=@uid", p.Entity.UID) == (long)expectedTotal; });
        Check(saveResults.All(value => value) && consistent.All(value => value) && EntityTable.SaveEntity(p),
            "concurrent production saves never persist a partial point allocation");
        Check(p.Entity.Experience == 101 && p.Entity.Atributes == 0
            && Scalar("SELECT experience FROM entities WHERE uid=@uid", p.Entity.UID) == 101
            && Scalar("SELECT strength FROM entities WHERE uid=@uid", p.Entity.UID) == p.Entity.Strength
            && Scalar("SELECT atributes FROM entities WHERE uid=@uid", p.Entity.UID) == 0,
            "production save retains every concurrent EXP gain and the final allocated stat balance");
    }
}
