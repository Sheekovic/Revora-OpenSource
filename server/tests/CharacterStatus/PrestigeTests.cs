using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Revora;
using Revora.Client;
using Revora.Game;
using Revora.Network;
using Revora.Network.GamePackets;
using Console = System.Console;
using BitConverter = System.BitConverter;

internal static class PrestigeTests
{
    private static int checks;
    private static void Check(bool ok, string name)
    {
        if (!ok) throw new Exception("FAILED: " + name);
        checks++; Console.WriteLine("PASS: " + name);
    }
    private static PrestigeEntry Player(uint uid, byte level = 140, byte cls = 165, byte reborn = 2)
    {
        return new PrestigeEntry(new PrestigeProfile { UID = uid, Name = "Player" + uid,
            Level = level, Class = cls, Reborn = reborn, Attributes = 92, Mesh = 1003, HairStyle = 3047 });
    }
    private static uint U(byte[] p, int at) { return BitConverter.ToUInt32(p, at); }
    private static byte[] Request(params byte[] body)
    {
        var p = new byte[body.Length + 12];
        Writer.WriteUInt16((ushort)(p.Length - 8), 0, p); Writer.WriteUInt16(3257, 2, p);
        Array.Copy(body, 0, p, 4, body.Length); return p;
    }
    public static int Main()
    {
        try
        {
            // File tables only. No database credentials, sockets, or player mutation.
            string runtime = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "bin", "Debug"));
            var previous = Environment.CurrentDirectory;
            try { Environment.CurrentDirectory = runtime; PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore); Revora.Database.ConquerItemInformation.Load(); }
            finally { Environment.CurrentDirectory = previous; }
            var e = Player(1000001);
            Check(e.Total == 5960 && e.Abilities.Abilities.Sum(a => a.Value) == e.Total, "total uses status contributions, never a currency");
            var runeProfile=new PrestigeProfile{UID=42,Level=140,Class=15,Reborn=2};
            var emptyRuneTotal=new PrestigeEntry(runeProfile).Total;
            runeProfile.Runes.Add(new ConquerItem(true){UID=10,ID=4010001,Position=101});
            runeProfile.Runes.Add(new ConquerItem(true){UID=11,ID=4020127,Position=102});
            runeProfile.Runes.Add(new ConquerItem(true){UID=12,ID=4030109,Position=103});
            var scoredRunes=new PrestigeEntry(runeProfile);
            Check(scoredRunes.Total==emptyRuneTotal+45000,"red, max blue and max yellow contribute 3000, 31000 and 11000 native prestige");
            runeProfile.Runes.Add(new ConquerItem(true){UID=13,ID=4030209,Position=211});
            runeProfile.Runes.Add(new ConquerItem(true){UID=14,ID=4030309,Position=104,Warehouse=1});
            runeProfile.Runes.Add(new ConquerItem(true){UID=15,ID=4020101,Position=102});
            Check(new PrestigeEntry(runeProfile).Total==scoredRunes.Total-31000,"bag and warehouse runes are ignored; ambiguous duplicate slots earn no prestige");
            byte[] runePacket=new CMsgUserAbilityScore(scoredRunes.Abilities);
            using(var stream=new MemoryStream(runePacket,4,runePacket.Length-12)) {
                var decoded=ProtoBuf.Serializer.Deserialize<UserAbilityScoreProto>(stream);
                Check(decoded.Abilities.Length==23 && decoded.Abilities.Where(a=>(int)a.Sort>=21).Sum(a=>a.Value)==45000,
                    "status packet includes confirmed rune categories 21 through 23");
            }
            Check(Player(1, 140, 165, 0).Total == 3960 && Player(1, 140, 165, 1).Total == 4960,
                "zero and one rebirth are no longer hardcoded as two");
            var all = PrestigeRanking.Order(Enumerable.Range(1, 12).Select(i => Player((uint)i)), 80000000);
            Check(all[0].Profile.UID == 1 && all[11].Profile.UID == 12, "equal-score ties use stable UID order");
            var low = Player(99, 120);
            Check(PrestigeRanking.Order(new[] { low, e }, 80000000)[0] == e, "higher prestige ranks first");
            var page = PrestigePackets.Page(all, 80000000, 0);
            Check(page.Length == 832 && U(page, 16) == 10 && BitConverter.ToUInt16(page, 0) == 824,
                "ten complete 80-byte rows fit with footer space");
            for (int i = 0; i < 10; i++)
                Check(U(page, 24 + i * 80) == i + 1 && U(page, 40 + i * 80) == i + 1
                    && U(page, 32 + i * 80) == all[i].Total, "page row " + (i + 1) + " has correct rank, UID and prestige");
            var second = PrestigePackets.Page(all, 80000000, 1);
            Check(U(second, 16) == 2 && U(second, 24) == 11 && U(second, 104) == 12,
                "partial final page reports actual count and ranks");
            Check(U(PrestigePackets.Page(all, 80000000, ushort.MaxValue), 16) == 0,
                "out-of-range page is an empty valid response");
            var self = PrestigePackets.Self(all, all[4]);
            Check(U(self, 4) == 2 && U(self, 24) == 5 && U(self, 32) == all[4].Total && U(self, 40) == 5,
                "personal row agrees with ranking and status scores");
            var hundred = PrestigeRanking.Order(Enumerable.Range(1, 101).Select(i => Player((uint)i)), 80000000);
            Check(U(PrestigePackets.Self(hundred, hundred[100]), 24) == 0
                && U(PrestigePackets.Page(hundred, 80000000, 10), 16) == 0, "top-100 boundary never wraps rank or leaks extra rows");
            byte[] classes = { 15, 25, 45, 55, 65, 75, 85, 135, 145, 165 };
            var roster = PrestigeRanking.Order(classes.Select((c, i) => Player((uint)(i + 1), 140, c)), 80000000);
            for (int i = 0; i < classes.Length; i++)
                Check(PrestigeRanking.Order(roster, (uint)(80000001 + i)).Single().Profile.Class == classes[i],
                    "class ranking " + classes[i] + " selects its own profession");
            var wall = PrestigePackets.Winners(roster);
            Check(U(wall, 4) == 6 && U(wall, 8) == 0 && U(wall, 16) == 11 && wall.Length == 912,
                "wall sends overall winner and all ten class winners in confirmed mode");
            Check(U(wall, 40) == 80000000 && U(wall, 44) == roster[0].Profile.UID,
                "overall winner category and character UID are separate fields");
            Check(U(wall, 40 + 10 * 80) == 80000010 && U(wall, 84 + 10 * 80) == 165,
                "Windwalker winner reaches client profession group sixteen");
            var wind = PrestigeRanking.Order(roster, 80000010);
            Check(U(PrestigePackets.Self(wind, wind[0], 80000010), 8) == 80000010
                && U(PrestigePackets.Self(wind, wind[0], 80000010), 24) == 1, "class-specific self response carries class rank");
            var look = PrestigePackets.Appearance(e);
            using (var stream = new MemoryStream(look, 4, look.Length - 12))
            {
                var show = ProtoBuf.Serializer.Deserialize<PrestigePackets.Showcase>(stream);
                Check(show.Action == 0 && show.Members.Length == 1 && show.Members[0].Kind == 1
                    && show.Members[0].UID == e.Profile.UID && show.Members[0].Mesh == 1003
                    && show.Members[0].Hair == 3047, "central character message carries real identity and appearance");
            }
            var wingEntry = Player(902);
            wingEntry.Profile.Items.Add(new ConquerItem(true) { ID=204009,Position=ConquerItem.Wing,Plus=12 });
            Func<PrestigePackets.Member> shownWing = () => {
                var bytes=PrestigePackets.Appearance(wingEntry);
                using(var stream=new MemoryStream(bytes,4,bytes.Length-12))
                    return ProtoBuf.Serializer.Deserialize<PrestigePackets.Showcase>(stream).Members[0];
            };
            Check(shownWing().Wing==204009 && shownWing().WingPlus==12,"default wing identity and plus remain without a cosmetic selection");
            wingEntry.Profile.CosmeticWing=60030022;
            Check(shownWing().Wing==60030022 && shownWing().WingPlus==12,"selected cosmetic wing replaces showcase identity and preserves equipment plus");
            wingEntry.Profile.CosmeticWing=60040023;
            Check(shownWing().Wing==60040023,"changing selected wing changes the next showcase packet");
            wingEntry.Profile.CosmeticWing=0;
            Check(shownWing().Wing==204009,"unequipping the cosmetic returns to the actual equipment wing");
            wingEntry.Profile.Items.Clear();wingEntry.Profile.CosmeticWing=60030022;
            Check(shownWing().Wing==60030022 && shownWing().WingPlus==0,"cosmetic wing can display without inventing an equipped wing plus");
            wingEntry.Profile.CosmeticWing=0;
            Check(shownWing().Wing==0 && shownWing().WingPlus==0,"no equipment or cosmetic yields no wing");
            var savedWing=typeof(PrestigeProfile).GetMethod("SavedCosmeticWing",BindingFlags.Static|BindingFlags.NonPublic);
            Func<byte[],uint> decodeWing = bytes => (uint)savedWing.Invoke(null,new object[]{bytes});
            byte[] wardrobe;
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)) {
                writer.Write((byte)3);
                writer.Write((short)6003);writer.Write((short)22);writer.Write((byte)0);
                writer.Write((short)6004);writer.Write((short)23);writer.Write((byte)1);
                writer.Write((short)2001);writer.Write((short)2);writer.Write((byte)1);
                wardrobe=stream.ToArray();
            }
            Check(decodeWing(wardrobe)==60040023,"saved profile reads only the equipped wing, excluding unequipped wings and selected titles");
            Check(decodeWing(new byte[0])==0 && decodeWing(new byte[]{1,2})==0,"missing and truncated saved wardrobe cannot invent a wing");
            var readAward=typeof(PrestigeProfile).GetMethod("ReadWingAward",BindingFlags.Instance|BindingFlags.NonPublic);
            var visible=typeof(PrestigeProfile).GetMethod("VisibleCosmeticWing",BindingFlags.Instance|BindingFlags.NonPublic);
            DateTime clock=new DateTime(2040,1,1,0,0,0,DateTimeKind.Utc);
            var temporary=new PrestigeProfile{CosmeticWing=60096009};
            Func<DateTime,uint> viewAt=utc=>(uint)visible.Invoke(temporary,new object[]{utc});
            Action<string,DateTime,DateTime?> award=(source,earned,expiry)=>readAward.Invoke(temporary,new object[]{source,(int)temporary.CosmeticWing,earned,expiry,clock});
            Check(viewAt(clock)==0,"saved timed-wing selection alone cannot authorize display");
            award("planet-revora-v1",clock.AddDays(-30),clock);
            award("planet-revora-v1",clock.AddDays(1),clock.AddDays(31));
            award("unknown",clock.AddDays(-1),clock.AddDays(29));
            award("planet-revora-v1",clock.AddDays(-1),null);
            award("planet-revora-v1",clock.AddDays(-1),clock.AddDays(30));
            Check(viewAt(clock)==0,"expired, future, unknown-source and invalid-lifetime awards are rejected");
            award("planet-revora-v1",clock.AddDays(-1),clock.AddDays(29));
            Check(viewAt(clock)==60096009 && viewAt(clock.AddDays(29).AddTicks(-1))==60096009,"verified temporary wing remains visible until its deadline");
            Check(viewAt(clock.AddDays(29))==0,"cached offline appearance expires exactly at deadline without a login");
            temporary=new PrestigeProfile{CosmeticWing=60126012};
            award("mrms-2017-wing-3-v1",clock.AddDays(-1),clock.AddDays(179));
            award("mrms-2017-wing-1-v1",clock.AddDays(-1),null);
            award("mrms-2017-wing-2-v1",clock.AddDays(-1),clock.AddDays(359));
            Check(viewAt(clock.AddYears(5))==60126012,"permanent Romance edition survives overlapping temporary editions");
            wingEntry.Profile.CosmeticWing=60096009;
            wingEntry.Profile.Items.Add(new ConquerItem(true){ID=204009,Position=ConquerItem.Wing,Plus=12});
            Check(shownWing().Wing==204009 && shownWing().WingPlus==12,"unverified timed cosmetic falls back to actual wing equipment");
            uint action, target;
            Check(PrestigePackets.TryReadRequest(Request(8, 0), out action, out target) && action == 0,
                "minimal wall-open request accepted");
            Check(PrestigePackets.TryReadRequest(Request(8, 1, 18, 4, 8, 1, 16, 42), out action, out target)
                && action == 1 && target == 42, "inspect request extracts target without trusting observer UID");
            var unavailable = PrestigePackets.Unavailable();
            Check(unavailable.Length == 14 && unavailable[4] == 8 && unavailable[5] == 1,
                "offline inspection returns confirmed unavailable action instead of hanging");
            byte[][] malformed = { Request(8, 2), Request(8, 0, 8, 0), Request(8, 128), Request(8, 0, 18, 100),
                Request(8, 0, 18, 2, 18, 0), Request(8, 255, 255, 255, 255, 16), new byte[300], new byte[0] };
            for (int i = 0; i < malformed.Length; i++)
                Check(!PrestigePackets.TryReadRequest(malformed[i], out action, out target), "malformed wall request " + i + " rejected");
            var valid = Request(8, 0); valid[0]++;
            Check(!PrestigePackets.TryReadRequest(valid, out action, out target), "declared-length mismatch rejected");
            var scoreQuery = Request(8, 1, 16, 42);
            Writer.WriteUInt16(3253, 2, scoreQuery);
            var scoreReader = new CMsgUserAbilityScore(scoreQuery);
            Check(scoreReader.Read() && scoreReader.Data.Id == 42, "bounded ability request reads real target UID");
            Check(!new CMsgUserAbilityScore(new byte[0]).Read() && !new CMsgUserAbilityScore(new byte[300]).Read(),
                "short and oversized ability queries rejected");
            Check(U(PrestigePackets.Winners(new PrestigeEntry[0]), 16) == 0, "empty population produces no invented winners");
            var flags = BindingFlags.Static | BindingFlags.NonPublic;
            typeof(CompanionTests).GetMethod("Reset", flags).Invoke(null, null);
            var map = (Map)typeof(CompanionTests).GetMethod("World", flags).Invoke(null, new object[] { (ushort)1002 });
            var client = (GameState)typeof(CompanionTests).GetMethod("Player", flags).Invoke(null,
                new object[] { map, false, (ushort)20, (ushort)20, (byte)165 });
            var original = PrestigeRanking.Update(client);
            client.Entity.EquippedWing=60030022;
            Check(PrestigeProfile.FromClient(client).CosmeticWing==60030022,"online profile captures the player's selected cosmetic wing");
            Check(PrestigeRanking.Update(client).Total==original.Total,"cosmetic wing selection does not change prestige score");
            client.Entity.EquippedWing=60126012;
            client.Entity.MyAchievement=new Achievement(client.Entity);
            var awards=(System.Collections.IList)typeof(Achievement).GetField("awards",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(client.Entity.MyAchievement);
            var awardType=typeof(Entity).Assembly.GetType("Revora.Game.Features.ProgressionEvidence+Award");
            foreach(DateTime? expiry in new DateTime?[]{DateTime.UtcNow.AddDays(20),null}) {
                var row=Activator.CreateInstance(awardType,true);
                awardType.GetField("Key",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(row,60126012);
                awardType.GetField("EarnedUtc",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(row,DateTime.UtcNow.AddDays(-1));
                awardType.GetField("ExpiresUtc",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(row,expiry);
                awards.Add(row);
            }
            var onlineWing=PrestigeProfile.FromClient(client);
            Check((uint)visible.Invoke(onlineWing,new object[]{DateTime.UtcNow.AddYears(5)})==60126012,"online snapshot preserves permanent award despite overlapping rental");
            typeof(Entity).GetField("_conquerpoints", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(client.Entity, uint.MaxValue);
            Check(PrestigeRanking.Update(client).Total == original.Total, "ordinary player's CP balance cannot change prestige");
            client.Entity.RevoraPoint = uint.MaxValue;
            Check(PrestigeRanking.Update(client).Total == original.Total, "Domino currency cannot change prestige");
            client.Entity.FullyLoaded = true;
            Check(Enumerable.Range(0, 8).All(i => PrestigeRanking.AllowQuery(client)) && !PrestigeRanking.AllowQuery(client),
                "ordinary session query burst is bounded without a GM requirement");
            client.Fake = true;
            Check(PrestigeRanking.Update(client) == null, "synthetic adventurers excluded from rankings");
            Check(PrestigeRanking.Snapshot(null).Any(x => x.Profile.UID == client.Entity.UID), "saved ranking entry survives session leaving live updates");
            System.Threading.Tasks.Parallel.For(0, 20, i => {
                if (PrestigeRanking.Snapshot(null).Count(x => x.Profile.UID == client.Entity.UID) != 1)
                    throw new Exception("Concurrent snapshot lost or duplicated identity");
            });
            Check(true, "concurrent readers retain one entry per character");
            Console.WriteLine("Passed " + checks + " prestige regression checks.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
