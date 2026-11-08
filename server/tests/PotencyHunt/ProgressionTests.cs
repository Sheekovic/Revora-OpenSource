using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network;
using Revora.Network.GamePackets;
using BitConverter = System.BitConverter;

internal static partial class PotencyHuntTests
{
    static PotencyHuntTests() {
        // Advisory locks are database-wide even when fixture tables use another schema.
        // Use the same isolated identity in parent and child test processes.
        typeof(Entity).Assembly.GetType("Revora.Database.AccountActivity")
            .GetField("LeaseObjectId",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)
            .SetValue(null,67116711);
    }
    static readonly Type CatalogType = typeof(Entity).Assembly.GetType("Revora.Game.Features.AchievementCatalog");
    static readonly Type EvidenceType = typeof(Entity).Assembly.GetType("Revora.Game.Features.ProgressionEvidence");

    static void EvidenceRefresh(GameState player)
    { Call(typeof(Achievement), player.Entity.MyAchievement, "Refresh", DateTime.UtcNow, true); }

    static GameState ProgressPlayer(Map map, string legacy)
    {
        var player = RunePlayer(map); player.Entity.Level = 1; player.Entity.FullyLoaded = true; Collection(player);
        player.Entity.MyAchievement = new Achievement(player.Entity); player.Entity.MyAchievement.Load(legacy);
        return player;
    }

    static bool CompleteEvidence(uint uid, int id, string sourceKey)
    {
        using (var connection = DataHolder.MySqlConnection) {
            connection.Open(); using (var transaction = connection.BeginTransaction()) {
                bool added = (bool)Call(EvidenceType, null, "Complete", connection, transaction, uid, id, "fixture", sourceKey, 1);
                transaction.Commit(); return added;
            }
        }
    }

    static bool GrantEvidence(uint uid, string source, string key, int reward, DateTime earned)
    {
        using (var connection = DataHolder.MySqlConnection) {
            connection.Open(); using (var transaction = connection.BeginTransaction()) {
                using (var timezone = new NpgsqlCommand("SET LOCAL TIME ZONE 'Pacific/Auckland'", connection, transaction)) timezone.ExecuteNonQuery();
                bool added = (bool)Call(EvidenceType, null, "Grant", connection, transaction, uid, source, key, reward, earned);
                transaction.Commit(); return added;
            }
        }
    }

    static bool HasAchievement(GameState player, int id)
    {
        int position = (id / 100 - 101) * 32 + id % 100 - 1;
        return (BitConverter.ToUInt32(player.Entity.MyAchievement.ToArray(), 16 + position / 32 * 4) & (1u << (position % 32))) != 0;
    }

    static byte[] AchievementRequest(uint uid, uint action, uint id)
    {
        var packet = new byte[28]; Writer.WriteUInt16(20, 0, packet); Writer.WriteUInt16(1136, 2, packet);
        Writer.WriteUInt32(action, 4, packet); Writer.WriteUInt32(uid, 8, packet); Writer.WriteUInt32(id, 12, packet);
        return packet;
    }

    static void VerifyProgression(Map map)
    {
        Query("ALTER TABLE entities ADD COLUMN level integer NOT NULL DEFAULT 1");
        Query("CREATE TABLE titles (LIKE public.titles INCLUDING ALL)");
        StorageManager.Load(); StorageItem.Load(); PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
        ConquerItem.ItemUID = new Counter(3800000);
        int[] ids = (int[])Call(CatalogType, null, "Ids");
        Check(ids.Length == 337 && ids.Distinct().Count() == 337, "337 unique 6711 achievement IDs");
        Check(ids.First() == 10101 && ids.Last() == 11323 && ids.Contains(11001) && ids.Contains(11309), "catalog spans real classic and Windwalker IDs");
        foreach (int id in new[] { int.MinValue, -1, 0, 10100, 10109, 10111, 10133, 10201, 11401, 20101, int.MaxValue })
            Check(!(bool)Call(CatalogType, null, "Known", id), "undefined/aliased achievement rejected: " + id);
        var fullWords = (uint[])Call(CatalogType, null, "Words", (object)ids);
        Check(fullWords.Length == 13 && fullWords[9] == 0xE42EBA1Fu && fullWords[12] == 0x007FFFFFu, "sparse groups retain exact installed positions");
        string legacy = string.Join("#", Enumerable.Repeat(uint.MaxValue.ToString(), 13)) + "#";
        Check((int)Call(CatalogType, null, "LegacyKnownCount", legacy) == 337, "79 undefined physical bits do not count as achievements");
        Check((int)Call(CatalogType, null, "LegacyKnownCount", "garbage#4294967296#-1#") == 0, "malformed legacy words are bounded without parsing exceptions");

        var player = ProgressPlayer(map, legacy);
        Check(!player.Fake && player.Account.State == AccountTable.AccountState.Player, "progression fixture is an ordinary non-GM character");
        Check(player.Entity.MyAchievement.CompletedCount == 0, "loading a legacy bitset does not establish verified completion");
        EvidenceRefresh(player);
        Check(player.Entity.MyAchievement.CompletedCount == 0 && !EligibleTitles(player).Contains(20010002), "legacy all-bits cannot unlock Rising Star");
        Check(Scalar("SELECT known_count FROM revora_achievement_backup_v1 WHERE uid=@uid", player.Entity.UID) == 337, "original legacy evidence is archived with known count");
        Check(Scalar("SELECT length(legacy_words) FROM revora_achievement_backup_v1 WHERE uid=@uid", player.Entity.UID) == legacy.Length, "original legacy text is preserved exactly in length");
        byte[] full = player.Entity.MyAchievement.ToArray();
        Check(full.Length == 76 && BitConverter.ToUInt16(full, 0) == 68 && BitConverter.ToUInt16(full, 2) == 1136
            && BitConverter.ToUInt32(full, 8) == player.Entity.UID && BitConverter.ToUInt32(full, 12) == 13, "existing achievement state framing and identity preserved");
        Check((bool)Call(typeof(Achievement), null, "IsReadRequest", AchievementRequest(player.Entity.UID, 1, 0)), "bounded read with transport footer accepted");
        Check((bool)Call(typeof(Achievement), null, "IsReadRequest", AchievementRequest(player.Entity.UID, 1, 0).Take(20).ToArray()), "dispatcher payload without footer accepted");
        var forged = AchievementRequest(player.Entity.UID, 2, 11001);
        for (int length = 0; length < forged.Length; length++) Achievement.Process(forged.Take(length).ToArray(), player);
        foreach (int id in ids) Achievement.Process(AchievementRequest(player.Entity.UID, 2, (uint)id), player);
        foreach (uint id in new[] { 0u, uint.MaxValue, 20101u }) Achievement.Process(AchievementRequest(player.Entity.UID + 1, 2, id), player);
        for (int length = 0; length < 100; length++) Achievement.Process(new byte[length], player);
        var read = AchievementRequest(player.Entity.UID, 1, 0); read[0] = 21;
        Check(!(bool)Call(typeof(Achievement), null, "IsReadRequest", read), "mismatched declared length rejected");
        read = AchievementRequest(player.Entity.UID, 1, 0); read[2] = 0;
        Check(!(bool)Call(typeof(Achievement), null, "IsReadRequest", read), "wrong packet family rejected");
        read = AchievementRequest(player.Entity.UID, 257, 0);
        Check(!(bool)Call(typeof(Achievement), null, "IsReadRequest", read), "whole action field checked instead of only its low byte");
        Check(player.Entity.MyAchievement.CompletedCount == 0 && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid", player.Entity.UID) == 0,
            "all forged/truncated/aliased requests leave memory and database unchanged");

        player.Entity.Money = 2999999; EvidenceRefresh(player);
        Check(player.Entity.MyAchievement.CompletedCount == 0, "silver threshold rejects 2999999");
        player.Entity.Money = 3000000; EvidenceRefresh(player);
        Check(player.Entity.MyAchievement.CompletedCount == 1 && HasAchievement(player, 10106), "actual three-million balance records and serializes achievement");
        player.Entity.Money = 0; EvidenceRefresh(player);
        Check(player.Entity.MyAchievement.CompletedCount == 1, "earned historical balance milestone survives spending");
        player.Equipment.Objects[11] = new ConquerItem(true) { UID = ConquerItem.ItemUID.Next, ID = 130009, Position = 12, Plus = 12 };
        EvidenceRefresh(player); Check(player.Entity.MyAchievement.CompletedCount == 1, "wrong item type in mount slot cannot earn mount achievements");
        var mount = new ConquerItem(true) { UID = ConquerItem.ItemUID.Next, ID = 300000, Position = 12, Plus = 11 };
        player.Equipment.Objects[11] = mount; EvidenceRefresh(player);
        Check(HasAchievement(player, 10107) && !HasAchievement(player, 10108), "real equipped mount earns only qualifying lineage milestone");
        mount.Plus = 12; EvidenceRefresh(player);
        Check(HasAchievement(player, 10108), "plus-twelve mount records lineage achievement");
        player.Equipment.Objects[11] = null;
        player.Guild = new Revora.Game.ConquerStructures.Society.Guild(player.Entity.Name) { ID = 55 };
        player.Guild.Leader = new Revora.Game.ConquerStructures.Society.Guild.Member(55) { ID = player.Entity.UID };
        EvidenceRefresh(player); Check(!HasAchievement(player, 10218), "guild references must agree with the character guild identity");
        player.Entity.CharBuffer._GuildID = 55; EvidenceRefresh(player);
        Check(HasAchievement(player, 10218) && HasAchievement(player, 10219) && player.Entity.MyAchievement.CompletedCount == 5,
            "actual guild membership and leadership earn separate milestones");
        player.Guild = null; player.Entity.CharBuffer._GuildID = 0;
        player.Entity.MyAchievement = new Achievement(player.Entity); player.Entity.MyAchievement.Load("replacement#"); EvidenceRefresh(player);
        Check(player.Entity.MyAchievement.CompletedCount == 5, "fresh session reloads verified completions without current prerequisites");
        Check(Scalar("SELECT length(legacy_words) FROM revora_achievement_backup_v1 WHERE uid=@uid", player.Entity.UID) == legacy.Length,
            "subsequent sessions cannot overwrite original legacy backup");
        int additions = 0;
        Parallel.For(0, 16, i => { if (CompleteEvidence(player.Entity.UID, 11001, "same-completion")) Interlocked.Increment(ref additions); });
        EvidenceRefresh(player);
        Check(additions == 1 && player.Entity.MyAchievement.CompletedCount == 6 && HasAchievement(player, 11001),
            "concurrent identical server completions commit once and reload in the client state");
        bool rejected = false; try { CompleteEvidence(player.Entity.UID, 10109, "unknown"); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "transaction API rejects undefined IDs");
        Query("INSERT INTO revora_achievement_evidence(uid,achievement_id,source,source_key,rule_version) VALUES(@uid,999999,'fixture','invalid-row',1)", "uid", (long)player.Entity.UID);
        EvidenceRefresh(player); Check(player.Entity.MyAchievement.CompletedCount == 6, "unknown database entries cannot inflate the verified count");

        DateTime now = DateTime.UtcNow.AddSeconds(-2);
        now = new DateTime(now.Ticks / 10 * 10, DateTimeKind.Utc); // PostgreSQL microsecond precision.
        using (var connection = DataHolder.MySqlConnection) {
            connection.Open(); using (var transaction = connection.BeginTransaction()) {
                Call(EvidenceType, null, "Complete", connection, transaction, player.Entity.UID, 11022, "fixture", "rolled-back", 1);
                Call(EvidenceType, null, "Grant", connection, transaction, player.Entity.UID, "summer-glow-2017", "rolled-back", 60136013, now);
                transaction.Rollback();
            }
        }
        EvidenceRefresh(player);
        Check(!HasAchievement(player, 11022) && !EligibleTitles(player).Contains(60136013), "rollback removes both completion and reward evidence");
        additions = 0;
        Parallel.For(0, 16, i => { if (GrantEvidence(player.Entity.UID, "summer-glow-2017", "craft-1", 60136013, now)) Interlocked.Increment(ref additions); });
        Check(additions == 1, "concurrent crafting result replay creates one award");
        Check(!GrantEvidence(player.Entity.UID, "summer-glow-2017", "craft-1", 60136013, now.AddDays(1)), "replayed award does not replace its original timestamp");
        Check(!GrantEvidence(player.Entity.UID, "summer-glow-2017", "craft-1", 60146014, now)
            && Scalar("SELECT title_key FROM revora_event_awards WHERE uid=@uid AND source='summer-glow-2017' AND source_key='craft-1'", player.Entity.UID) == 60136013,
            "a retry with a different random color cannot create a second result for one craft");
        GrantEvidence(player.Entity.UID, "elite-pk-local-v1", "week-1", 60010020, now);
        GrantEvidence(player.Entity.UID, "team-pk-2018", "run-1", 20592059, now);
        Check(!GrantEvidence(player.Entity.UID, "team-pk-2018", "run-1", 20622062, now), "one tournament result cannot accumulate another placement tier");
        GrantEvidence(player.Entity.UID, "summer-siren-2017", "future", 60176017, now.AddDays(1));
        rejected = false; try { GrantEvidence(player.Entity.UID, "summer-glow-2017", "wrong-reward", 40011000, now); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "a known source cannot grant unrelated Emperor wings");
        rejected = false; try { GrantEvidence(player.Entity.UID, "unknown", "unknown", 20572057, now); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "unknown historical award source remains disabled");
        EvidenceRefresh(player); new TitleStorage().Login(player);
        Check(OwnedTitles(player).Keys.OrderBy(x => x).SequenceEqual(new[] { 20592059, 60010020, 60136013 }), "only eligible committed current awards enter the wardrobe: " + string.Join(",", OwnedTitles(player).Keys));
        Check(player.Entity.WTitles.Points == 500 && !EligibleTitles(player).Contains(60176017), "exact title points and future-date exclusion preserved");
        TitleRequest(60136013, TitleStorage.Action.Equip).Handle(player);
        Check(player.Entity.EquippedWing == 60136013, "ordinary owner equips the persistently earned wing");
        player.Entity.MyAchievement = new Achievement(player.Entity); player.Entity.WTitles = null;
        StorageManager.Load(); new TitleStorage().Login(player);
        Check(player.Entity.EquippedWing == 60136013 && OwnedTitles(player).ContainsKey(60136013), "new session and storage reload preserve award ownership and equipped appearance");
        int[] beforeExpiry = (int[])Call(typeof(Achievement), player.Entity.MyAchievement, "AwardKeys", now.AddDays(7).AddTicks(-1));
        int[] atExpiry = (int[])Call(typeof(Achievement), player.Entity.MyAchievement, "AwardKeys", now.AddDays(7));
        Check(beforeExpiry.Contains(60010020) && !atExpiry.Contains(60010020) && atExpiry.Contains(60136013), "cached awards enforce the exact expiry boundary without waiting for refresh");
        int[] teamExpiry = (int[])Call(typeof(Achievement), player.Entity.MyAchievement, "AwardKeys", now.AddDays(14));
        Check(!teamExpiry.Contains(20592059) && teamExpiry.Contains(60136013), "fourteen-day award expires while permanent craft survives");
        Query("UPDATE revora_event_awards SET revoked=true WHERE uid=@uid AND title_key=60136013", "uid", (long)player.Entity.UID);
        EvidenceRefresh(player); RefreshTitles(player);
        Check(!OwnedTitles(player).ContainsKey(60136013) && player.Entity.EquippedWing == 0, "revocation clears persisted ownership and equipped appearance on refresh");
        var expired = ProgressPlayer(map, "");
        GrantEvidence(expired.Entity.UID, "elite-pk-local-v1", "old", 60010020, now.AddDays(-8)); EvidenceRefresh(expired);
        Check(!EligibleTitles(expired).Contains(60010020), "offline-expired award is never restored at login");

        var failed = ProgressPlayer(map, "malformed#"); failed.Entity.Money = 3000000;
        Query("ALTER TABLE revora_achievement_evidence ADD CONSTRAINT reject_progression_fixture CHECK(achievement_id<>10106) NOT VALID");
        rejected = false; try { EvidenceRefresh(failed); } catch (PostgresException) { rejected = true; }
        Check(rejected && failed.Entity.MyAchievement.CompletedCount == 0, "failed evidence write cannot install an in-memory achievement");
        Check(Scalar("SELECT count(*) FROM revora_achievement_backup_v1 WHERE uid=@uid", failed.Entity.UID) == 0, "failed first-load transaction also rolls back its archive write");
        Query("ALTER TABLE revora_achievement_evidence DROP CONSTRAINT reject_progression_fixture");
        EvidenceRefresh(failed); Check(failed.Entity.MyAchievement.CompletedCount == 1, "safe retry after failure commits the milestone once");
        var fake = ProgressPlayer(map, legacy); fake.Fake = true; fake.Entity.Money = 3000000; EvidenceRefresh(fake);
        Check(fake.Entity.MyAchievement.CompletedCount == 0 && Scalar("SELECT count(*) FROM revora_achievement_backup_v1 WHERE uid=@uid", fake.Entity.UID) == 0,
            "fake adventurers cannot produce achievement evidence");

        // Synthetic authoritative rows exercise the threshold; this is not a claim
        // that a real character can currently complete 320 gameplay predicates.
        var boundary = ProgressPlayer(map, legacy);
        using (var connection = DataHolder.MySqlConnection) {
            connection.Open(); using (var transaction = connection.BeginTransaction()) {
                foreach (int id in ids.Take(319)) Call(EvidenceType, null, "Complete", connection, transaction, boundary.Entity.UID, id, "fixture", "boundary", 1);
                transaction.Commit();
            }
        }
        EvidenceRefresh(boundary);
        Check(boundary.Entity.MyAchievement.CompletedCount == 319 && !EligibleTitles(boundary).Contains(20010002), "319 known verified completions remain below Rising Star");
        CompleteEvidence(boundary.Entity.UID, ids[319], "boundary"); EvidenceRefresh(boundary);
        Check(boundary.Entity.MyAchievement.CompletedCount == 320 && EligibleTitles(boundary).Contains(20010002), "320 distinct verified completions unlock Rising Star");
        var growth = ProgressPlayer(map, ""); growth.Entity.Level = 129; growth.VirtuePoints = 4999;
        EvidenceRefresh(growth);
        Check(!HasAchievement(growth,10310) && !HasAchievement(growth,10311) && !HasAchievement(growth,10317), "growth observations reject values below all three thresholds");
        growth.Entity.Level = 130; EvidenceRefresh(growth);
        Check(HasAchievement(growth,10310) && !HasAchievement(growth,10311), "level 130 earns its own milestone only");
        growth.Entity.Level = 139; EvidenceRefresh(growth);
        Check(!HasAchievement(growth,10311), "level 139 remains below the level-140 milestone");
        growth.Entity.Level = 140; Call(typeof(GameState),growth,"AddVirtuePoints",1u); EvidenceRefresh(growth);
        Check(HasAchievement(growth,10311) && HasAchievement(growth,10317), "level 140 and the production Virtue increment earn their milestones");
        Check(Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id IN(10310,10311,10317)",growth.Entity.UID)==3 && Scalar("SELECT level FROM entities WHERE uid=@uid",growth.Entity.UID)==140, "production level update and all three observed growth milestones persist");
        growth.Entity.Level = 1; growth.VirtuePoints = 0;
        growth.Entity.MyAchievement = new Achievement(growth.Entity); growth.Entity.MyAchievement.Load(""); EvidenceRefresh(growth);
        Check(new[]{10310,10311,10317}.All(id=>HasAchievement(growth,id)), "earned growth survives level reset, Virtue spending and evidence reload");
        Parallel.For(0,12,i=>EvidenceRefresh(growth));
        Check(Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id IN(10310,10311,10317)",growth.Entity.UID)==3, "concurrent growth reconciliation cannot duplicate evidence");
        var growing = ProgressPlayer(map, ""); growing.Entity.Level = 140; growing.VirtuePoints = 5000;
        Query("ALTER TABLE revora_achievement_evidence ADD CONSTRAINT growth_failure CHECK(achievement_id<>10317) NOT VALID");
        rejected=false;try {EvidenceRefresh(growing);}catch(PostgresException){rejected=true;}
        Check(rejected && growing.Entity.MyAchievement.CompletedCount==0 && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid",growing.Entity.UID)==0, "failed growth observation rolls back the complete threshold batch and cache");
        Query("ALTER TABLE revora_achievement_evidence DROP CONSTRAINT growth_failure"); EvidenceRefresh(growing);
        Check(growing.Entity.MyAchievement.CompletedCount==4 && HasAchievement(growing,10312), "growth and BP observations retry safely after storage recovery");
        var simulated=ProgressPlayer(map,"");simulated.Fake=true;simulated.Entity.Level=140;simulated.VirtuePoints=5000;EvidenceRefresh(simulated);
        Check(simulated.Entity.MyAchievement.CompletedCount==0, "simulated adventurers cannot earn growth evidence");
        simulated.Fake=false;typeof(Entity).GetField("PersistenceRequiresReload",Instance).SetValue(simulated.Entity,true);EvidenceRefresh(simulated);
        Check(simulated.Entity.MyAchievement.CompletedCount==0, "reload-fenced state cannot prove growth completion");
        VerifyClassicDances(Kernel.Maps[1002],map);
    }
    static byte[] DanceRequest(GameState p,uint action,uint? uid=null) {
        var bytes=new Data(true){ID=Data.ChangeAction,UID=uid??p.Entity.UID,dwParam=action,wParam1=1,wParam2=1}.ToArray();
        Writer.WriteUInt64(PacketHandler.ClientSeal,bytes.Length-8,bytes);return bytes;
    }
    static long DanceEvidence(GameState p){return Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10121",p.Entity.UID);}
    static GameState Dancer(Map town){var p=ProgressPlayer(town,"");p.Entity.X=400;p.Entity.Y=300;return p;}
    static void VerifyClassicDances(Map town,Map outside) {
        Query("ALTER TABLE entities ADD COLUMN IF NOT EXISTS x integer NOT NULL DEFAULT 0,ADD COLUMN IF NOT EXISTS y integer NOT NULL DEFAULT 0");
        var p=Dancer(town);p.Entity.Action=0;
        Achievement.Process(AchievementRequest(p.Entity.UID,2,10121),p);
        Check(DanceEvidence(p)==0,"client achievement assertion cannot attest to dancing");
        Parallel.For(0,12,n=>Data.Process(DanceRequest(p,1),p));
        Check(!Fenced(p) && p.Entity.Action==1 && HasAchievement(p,10121) && DanceEvidence(p)==1,
            "ordinary basic dance in Twin City immediately earns one durable achievement under concurrent repeats");
        p.Entity.MyAchievement=new Achievement(p.Entity);p.Entity.MyAchievement.Load("");new TitleStorage().Login(p);
        Check(HasAchievement(p,10121),"production title login reloads the dance evidence independently of legacy bits");
        Query("CREATE FUNCTION suppress_dance() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF NEW.achievement_id=10121 THEN RETURN NULL; END IF; RETURN NEW; END';CREATE TRIGGER suppress_dance BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW EXECUTE FUNCTION suppress_dance()");
        var failed=Dancer(town);failed.Entity.Action=0;
        try{Data.Process(DanceRequest(failed,1),failed);Data.Process(DanceRequest(p,1),p);}
        finally{Query("DROP TRIGGER suppress_dance ON revora_achievement_evidence;DROP FUNCTION suppress_dance()");}
        Check(Fenced(failed) && failed.Entity.Action==0 && DanceEvidence(failed)==0 && !HasAchievement(failed,10121),
            "missing dance evidence prevents accepted action and leaves no achievement projection");
        Check(!Fenced(p) && DanceEvidence(p)==1,"repeated dancing does not rewrite a saved lifetime achievement");
        DemonReset(failed);Data.Process(DanceRequest(failed,1),failed);
        Check(!Fenced(failed) && failed.Entity.Action==1 && HasAchievement(failed,10121),"dance retries after storage repair without a poisoned success cache");
        var elsewhere=ProgressPlayer(outside,"");Data.Process(DanceRequest(elsewhere,1),elsewhere);
        Check(!Fenced(elsewhere) && elsewhere.Entity.Action==1 && DanceEvidence(elsewhere)==0,
            "dancing outside Twin City remains available without earning its location achievement");
        for(uint action=2;action<=8;action++) {
            var learned=Dancer(town);learned.Entity.Action=0;ushort id=(ushort)(1380+(action-2)*5);
            Data.Process(DanceRequest(learned,action),learned);
            Check(learned.Entity.Action==0 && DanceEvidence(learned)==0,"unlearned classic dance is rejected: "+action);
            learned.Spells[id]=new Spell(true){ID=id,Level=0,Available=false};Data.Process(DanceRequest(learned,action),learned);
            Check(learned.Entity.Action==0 && DanceEvidence(learned)==0,"unavailable dance art is rejected: "+id);
            learned.Spells[id].Available=true;Data.Process(DanceRequest(learned,action),learned);
            Check(!Fenced(learned) && learned.Entity.Action==action && HasAchievement(learned,10121),"owned available classic dance accepted: "+id);
        }
        foreach(string bad in new[]{"foreign","alias","fake","dead","trade","unloaded","stale","ride","fly","transform","short","seal","length"}) {
            var invalid=Dancer(town);invalid.Entity.Action=0;var packet=DanceRequest(invalid,bad=="alias"?65537u:1u,bad=="foreign"?invalid.Entity.UID+1:invalid.Entity.UID);
            if(bad=="fake")invalid.Fake=true;if(bad=="dead")invalid.Entity.Hitpoints=0;if(bad=="trade")invalid.Trade.InTrade=true;if(bad=="unloaded")invalid.Entity.FullyLoaded=false;
            if(bad=="stale"){GameState discarded;Kernel.GamePool.TryRemove(invalid.Entity.UID,out discarded);}
            if(bad=="ride")invalid.Entity.AddFlag((ulong)FlagsUpdate.Flags.Ride,30,true);if(bad=="fly")invalid.Entity.AddFlag((ulong)FlagsUpdate.Flags.Fly,30,true);
            if(bad=="transform")invalid.Entity.TransformationID=1000;
            if(bad=="short")Array.Resize(ref packet,7);if(bad=="seal")packet[packet.Length-1]^=1;if(bad=="length")Writer.WriteUInt16(1,0,packet);
            Data.Process(packet,invalid);
            Check(invalid.Entity.Action==0 && DanceEvidence(invalid)==0,"invalid dance neither projects nor earns: "+bad);
        }
        var other=ProgressPlayer(town,"");Data.Process(DanceRequest(other,Enums.ConquerAction.Sit),other);
        Check(other.Entity.Action==Enums.ConquerAction.Sit && DanceEvidence(other)==0,"ordinary sitting retains its existing behavior without dance credit");
        foreach(var tile in new[]{new[]{348,209,1},new[]{507,433,1},new[]{347,300,0},new[]{508,300,0},new[]{400,208,0},new[]{400,434,0}}) {
            var edge=Dancer(town);edge.Entity.X=(ushort)tile[0];edge.Entity.Y=(ushort)tile[1];Data.Process(DanceRequest(edge,1),edge);
            Check(!Fenced(edge) && edge.Entity.Action==1 && DanceEvidence(edge)==tile[2],"dance uses reviewed Twin City rectangle at "+tile[0]+","+tile[1]);
        }
    }
}
