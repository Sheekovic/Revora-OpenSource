using System;
using System.Linq;
using System.Reflection;
using Revora.Client;
using Revora.Game;
using Revora.Network.GamePackets;
using Console = System.Console;
using BitConverter = System.BitConverter;

internal static class CharacterStatusTests
{
    private static int checks;
    private static readonly BindingFlags Private = BindingFlags.Static | BindingFlags.NonPublic;
    private static object Fixture(string name, params object[] args)
    {
        return typeof(CompanionTests).GetMethod(name, Private).Invoke(null, args);
    }
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAILED: " + name);
        checks++; Console.WriteLine("PASS: " + name);
    }
    private static byte[] Encode(GameState client) { return new CharacterInfo(client).ToArray(); }
    private static void SetFixtureCps(Entity entity, uint value)
    {
        // The public currency setter persists immediately; packet fixtures must stay offline.
        typeof(Entity).GetField("_conquerpoints", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(entity, value);
    }
    private static bool SameOutside(byte[] a, byte[] b, int start, int length)
    {
        return a.Length == b.Length && Enumerable.Range(0, a.Length)
            .All(i => (i >= start && i < start + length) || a[i] == b[i]);
    }
    public static int Main()
    {
        try
        {
            Fixture("Reset");
            var map = (Map)Fixture("World", (ushort)1002);
            var client = (GameState)Fixture("Player", map, false, (ushort)20, (ushort)20, (byte)165);
            Entity e = client.Entity;
            e.Name = "StatFixture"; e.Spouse = "None";
            e.Strength = 19; e.Agility = 45; e.Vitality = 15; e.Spirit = 13; e.Atributes = 452;
            e.Reborn = 2; e.PKPoints = 17; e.HairStyle = 3047;
            e.Money = 0x0102030405060708UL;
            SetFixtureCps(e, 0x11223344);
            e.Experience = 0x2233445566778899UL;
            e.RevoraPoint = 0x12345678;
            e.BoundCps = 321;
            e.Hitpoints = 0x12345678; e.Mana = 0x4321;
            byte[] packet = Encode(client);
            Check(BitConverter.ToUInt16(packet, 2) == 1006 && BitConverter.ToUInt16(packet, 0) == packet.Length - 8,
                "existing CharacterInfo opcode and frame length preserved");
            Check(BitConverter.ToUInt32(packet, 18) == 3047, "hair occupies confirmed UInt32 field");
            Check(BitConverter.ToUInt64(packet, 22) == e.Money, "64-bit silver preserved");
            Check(BitConverter.ToUInt32(packet, 30) == e.ConquerPoints && BitConverter.ToUInt64(packet, 34) == e.Experience,
                "CP and experience occupy separate confirmed fields");
            Check(BitConverter.ToUInt16(packet, 62) == 19 && BitConverter.ToUInt16(packet, 64) == 45
                && BitConverter.ToUInt16(packet, 66) == 15 && BitConverter.ToUInt16(packet, 68) == 13
                && BitConverter.ToUInt16(packet, 70) == 452, "screenshot base attributes and unspent points remain authoritative");
            Check(BitConverter.ToUInt32(packet, 72) == 0x12345678 && BitConverter.ToUInt16(packet, 76) == 0x4321,
                "HP and mana match modern confirmed receiver offsets");
            Check(BitConverter.ToString(packet, 72, 6) == "78-56-34-12-21-43", "golden HP/mana bytes have correct widths and little-endian order");
            Check(BitConverter.ToUInt16(packet, 78) == 17 && packet[80] == 140 && packet[81] == 165 && packet[85] == 2,
                "adjacent PK points, level, class and rebirth remain intact");
            Check(BitConverter.ToUInt64(packet, 132) == e.RevoraPoint && BitConverter.ToUInt64(packet, 140) == e.UID,
                "Domino display code is populated separately from currency");
            Check(packet[148] == 3 && packet[149] == 11 && packet[161] == 0 && packet[162] == 4
                && System.Text.Encoding.ASCII.GetString(packet, 150, 11) == "StatFixture"
                && System.Text.Encoding.ASCII.GetString(packet, 163, 4) == "None", "name, empty string and spouse remain intact");
            foreach (uint hp in new uint[] { 0, 1, 65535, 65536, 100000, uint.MaxValue })
            {
                byte[] before = Encode(client); e.Hitpoints = hp; byte[] after = Encode(client);
                Check(BitConverter.ToUInt32(after, 72) == hp && BitConverter.ToUInt16(after, 76) == e.Mana
                    && SameOutside(before, after, 72, 4), "HP boundary " + hp + " never truncates or changes mana");
            }
            foreach (ushort mana in new ushort[] { 0, 1, 12345, ushort.MaxValue })
            {
                byte[] before = Encode(client); e.Mana = mana; byte[] after = Encode(client);
                Check(BitConverter.ToUInt16(after, 76) == mana && BitConverter.ToUInt32(after, 72) == e.Hitpoints
                    && SameOutside(before, after, 76, 2), "mana boundary " + mana + " never changes HP or PK points");
            }
            foreach (uint cp in new uint[] { 0, 1000, uint.MaxValue })
            {
                byte[] before = Encode(client); SetFixtureCps(e, cp); byte[] after = Encode(client);
                Check(BitConverter.ToUInt32(after, 30) == cp && SameOutside(before, after, 30, 4),
                    "CP boundary " + cp + " preserves experience and other fields");
            }
            foreach (uint identity in new uint[] { 1, 1000004, uint.MaxValue })
            {
                e.UID = identity;
                byte[] first = Encode(client), again = Encode(client);
                Check(BitConverter.ToUInt64(first, 140) == identity && BitConverter.ToUInt32(first, 8) == identity
                    && first.SequenceEqual(again) && BitConverter.ToUInt64(first, 132) == e.RevoraPoint,
                    "stable positive display identity " + identity + " leaves currency alone");
            }
            e.SendUpdates = true;
            e.Hitpoints = 123456;
            var update = (FlagsUpdate)typeof(Entity).GetField("update", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(e);
            Check(update.UID == e.UID && update.Buffer.Values.Count == 1 && update.Buffer.Values[0].Type == 0
                && update.Buffer.Values[0].value == BitConverter.ToUInt32(Encode(client), 72),
                "subsequent HP update agrees with initial CharacterInfo");
            e.Mana = 1234;
            update = (FlagsUpdate)typeof(Entity).GetField("update", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(e);
            Check(update.Buffer.Values[0].Type == 2 && update.Buffer.Values[0].value == BitConverter.ToUInt16(Encode(client), 76),
                "subsequent mana update agrees with initial CharacterInfo");
            foreach(uint balance in new uint[]{1000,250,0,1,uint.MaxValue,50}) {
                e.RevoraPoint=balance;
                update=(FlagsUpdate)typeof(Entity).GetField("update",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(e);
                Check(update.UID==e.UID && update.Buffer.Values.Count==1 && update.Buffer.Values[0].Type==91
                    && update.Buffer.Values[0].value==BitConverter.ToUInt64(Encode(client),132),
                    "RVP increase/deduction refresh agrees with initial inventory balance: "+balance);
                byte[] refresh=update.ToArray();
                using(var stream=new System.IO.MemoryStream(refresh,4,refresh.Length-12)) {
                    var decoded=ProtoBuf.Serializer.Deserialize<FlagsUpdate.CMsgUserAttrib>(stream);
                    Check(BitConverter.ToUInt16(refresh,2)==10017 && decoded.Values[0].Type==91 && decoded.Values[0].value==balance,
                        "RVP protobuf round-trip preserves zero/full unsigned balance: "+balance);
                }
            }
            e.SendUpdates = false;
            e.UID = 0;
            bool rejected = false;
            try { Encode(client); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "uninitialized identity cannot emit a Domino error code");
            Console.WriteLine("Passed " + checks + " character-status checks against the actual server assembly; no database or client used.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { Fixture("Reset"); }
    }
}
