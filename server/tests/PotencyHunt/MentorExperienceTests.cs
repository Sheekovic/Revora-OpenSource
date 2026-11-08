using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.ConquerStructures.Society;
using Revora.Network;
using Revora.Network.GamePackets;
using KnownPersons = Revora.Database.KnownPersons;

internal static partial class PotencyHuntTests
{
    static void MentorLink(GameState mentor, GameState child, long units)
    {
        Query(@"INSERT INTO apprentice(apprenticeid,mentorid,actual_experience,total_experience,
actual_plus,total_plus,actual_heavenblessing,total_heavenblessing,mentorname,apprenticename,enroledate)
VALUES(@uid,@mentor,@units,@units,0,0,0,0,@name,@child,0)",
            "uid", (long)child.Entity.UID, "mentor", (long)mentor.Entity.UID, "units", units,
            "name", mentor.Entity.Name, "child", child.Entity.Name);
        KnownPersons.LoadMentor(child); KnownPersons.LoadMentor(mentor);
    }
    static long MentorBalance(GameState child)
    { return Scalar("SELECT actual_experience FROM apprentice WHERE apprenticeid=@uid", child.Entity.UID); }
    static byte[] MentorClaimPacket()
    {
        var packet = new byte[48]; Writer.WriteUInt16(40, 0, packet); Writer.WriteUInt16(2067, 2, packet);
        Writer.WriteUInt32(1, 4, packet); Writer.WriteUInt32(uint.MaxValue, 8, packet);
        Writer.WriteUInt64(ulong.MaxValue, 24, packet); Writer.WriteUInt64(PacketHandler.ClientSeal, 40, packet);
        return packet;
    }
    static void ClaimMentor(GameState p) { MentorPrize.Process(MentorClaimPacket(), p); }
    static void VerifyMentorExperience(Map map)
    {
        Query("ALTER TABLE apprentice ADD COLUMN mentorname text, ADD COLUMN apprenticename text, ADD COLUMN enroledate bigint DEFAULT 0");
        var mentor = StoredProgressionPlayer(map); var child = StoredProgressionPlayer(map);
        MentorLink(mentor, child, 100);
        child.Entity.Experience = DataHolder.LevelExperience(135) - 1; EntityTable.SaveEntity(child);
        child.IncreaseExperience(1, false);
        Check(!Fenced(child) && child.Entity.Level == 136 && MentorBalance(child) == 372
            && mentor.PrizeExperience == 372 && child.AsApprentice.Actual_Experience == 372 && SkillRows(child) == 1,
            "ordinary apprentice level-up commits mentor EXP, character progression and learned skill together");
        KnownPersons.SaveApprenticeInfo(new Apprentice { ID = child.Entity.UID, Actual_Experience = 1, Total_Experience = 1 });
        Check(MentorBalance(child) == 372, "stale unrelated apprentice save cannot overwrite committed EXP credit");
        KnownPersons.LoadMentor(mentor); KnownPersons.LoadMentor(mentor);
        Check(mentor.PrizeExperience == 372, "repeated mentor loading does not double count contributions");
        mentor.Entity.FullyLoaded = false;
        child.Entity.Experience = DataHolder.LevelExperience(136) - 1; EntityTable.SaveEntity(child); child.IncreaseExperience(1, false);
        mentor.Entity.FullyLoaded = true; KnownPersons.LoadMentor(mentor);
        Check(MentorBalance(child) == 646 && mentor.PrizeExperience == 646,
            "apprentice credit survives an unavailable mentor cache and is recovered by the production loader");

        var failing = StoredProgressionPlayer(map); MentorLink(mentor, failing, 0);
        failing.Entity.Experience = DataHolder.LevelExperience(135) - 1; EntityTable.SaveEntity(failing);
        Query(@"CREATE FUNCTION reject_mentor_credit() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
RAISE EXCEPTION 'fixture mentor failure'; END $$;
CREATE TRIGGER reject_mentor_credit BEFORE UPDATE ON apprentice FOR EACH ROW EXECUTE FUNCTION reject_mentor_credit()");
        bool rejected = false;
        try { failing.IncreaseExperience(1, false); } catch (Npgsql.PostgresException) { rejected = true; }
        finally { Query("DROP TRIGGER reject_mentor_credit ON apprentice; DROP FUNCTION reject_mentor_credit()"); }
        Check(rejected && Fenced(failing) && failing.Entity.Level == 135 && SkillRows(failing) == 0 && MentorBalance(failing) == 0
            && Scalar("SELECT level FROM entities WHERE uid=@uid", failing.Entity.UID) == 135,
            "mentor credit failure rolls back profile and skill without projecting a level reward");

        var claimant = StoredProgressionPlayer(map); var donor = StoredProgressionPlayer(map); MentorLink(claimant, donor, 600);
        ulong before = claimant.Entity.Experience, ball = claimant.ExpBall;
        Parallel.Invoke(() => ClaimMentor(claimant), () => ClaimMentor(claimant));
        Check(!Fenced(claimant) && claimant.Entity.Experience == before + ball && MentorBalance(donor) == 0
            && claimant.PrizeExperience == 0 && donor.AsApprentice.Actual_Experience == 0
            && Scalar("SELECT total_experience FROM apprentice WHERE apprenticeid=@uid", donor.Entity.UID) == 600
            && Scalar("SELECT experience FROM entities WHERE uid=@uid", claimant.Entity.UID) == (long)(before + ball),
            "concurrent forged-balance claims grant stored EXP once and retain lifetime credit");

        var capped = StoredProgressionPlayer(map); var rich = StoredProgressionPlayer(map); MentorLink(capped, rich, 31000);
        capped.Apprentices = new SafeDictionary<uint, Apprentice>();
        ClaimMentor(capped);
        Check(!Fenced(capped) && MentorBalance(rich) == 700 && capped.PrizeExperience == 700,
            "visible claim cap consumes only 30300 units and displays the remaining database credit even with a missing cache row");

        var rollback = StoredProgressionPlayer(map); var source = StoredProgressionPlayer(map); MentorLink(rollback, source, 600);
        Query(@"CREATE FUNCTION reject_mentor_profile() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
RAISE EXCEPTION 'fixture profile failure'; END $$;
CREATE TRIGGER reject_mentor_profile BEFORE UPDATE ON entities FOR EACH ROW EXECUTE FUNCTION reject_mentor_profile()");
        rejected = false;
        try { ClaimMentor(rollback); } catch (Npgsql.PostgresException) { rejected = true; }
        finally { Query("DROP TRIGGER reject_mentor_profile ON entities; DROP FUNCTION reject_mentor_profile()"); }
        Check(rejected && Fenced(rollback) && MentorBalance(source) == 600 && rollback.Entity.Experience == 5000,
            "failed character grant rolls back mentor debit and leaves the cached award unchanged");
        DemonReset(rollback); LoadProgressionImage(rollback); ClaimMentor(rollback);
        Check(!Fenced(rollback) && MentorBalance(source) == 0 && rollback.Entity.Experience == 5000 + rollback.ExpBall,
            "reloaded mentor can claim the intact balance after a rolled-back attempt");

        var guarded = StoredProgressionPlayer(map); var reserve = StoredProgressionPlayer(map); MentorLink(guarded, reserve, 600);
        for (int length = 0; length < 44; length++) MentorPrize.Process(new byte[length], guarded);
        var invalid = MentorClaimPacket(); invalid[40] ^= 1; MentorPrize.Process(invalid, guarded);
        invalid = MentorClaimPacket(); invalid[0]--; MentorPrize.Process(invalid, guarded);
        Check(MentorBalance(reserve) == 600 && guarded.Entity.Experience == 5000 && !Fenced(guarded),
            "short, malformed and unsealed mentor claims cannot consume credit or grant EXP");
        guarded.Entity.Level = Config.LevelCap; ClaimMentor(guarded);
        Check(MentorBalance(reserve) == 600, "maximum-level claim preserves contributions when EXP cannot be awarded");

        var upstream = StoredProgressionPlayer(map); var middle = StoredProgressionPlayer(map); var bottom = StoredProgressionPlayer(map);
        MentorLink(upstream, middle, 0); MentorLink(middle, bottom, 600);
        middle.Entity.Experience = DataHolder.LevelExperience(135) - 1; EntityTable.SaveEntity(middle); ClaimMentor(middle);
        Check(!Fenced(middle) && middle.Entity.Level == 136 && MentorBalance(bottom) == 0 && MentorBalance(middle) == 0,
            "mentor prize level-up cannot recursively create upstream contribution credit");
        var excluded = StoredProgressionPlayer(map); MentorLink(upstream, excluded, 0);
        excluded.Entity.Experience = DataHolder.LevelExperience(135) - 1; EntityTable.SaveEntity(excluded);
        typeof(GameState).GetMethod("IncreaseExperience", Instance, null,
            new Type[] { typeof(ulong), typeof(bool), typeof(bool) }, null).Invoke(excluded, new object[] { 1UL, false, false });
        Check(!Fenced(excluded) && excluded.Entity.Level == 136 && MentorBalance(excluded) == 0 && SkillRows(excluded) == 1,
            "excluded EXP source preserves the complete level reward without creating mentor credit");
        var racingMentor = StoredProgressionPlayer(map); var racingChild = StoredProgressionPlayer(map);
        MentorLink(racingMentor, racingChild, 600);
        racingChild.Entity.Experience = DataHolder.LevelExperience(135) - 1; EntityTable.SaveEntity(racingChild);
        ulong raceBall = racingMentor.ExpBall;
        Parallel.Invoke(() => ClaimMentor(racingMentor), () => racingChild.IncreaseExperience(1, false));
        ulong remainder = (ulong)MentorBalance(racingChild);
        Check(!Fenced(racingMentor) && !Fenced(racingChild) && (remainder == 0 || remainder == 272)
            && racingMentor.Entity.Experience == 5000 + ExpScale(raceBall, 872 - remainder, 600)
            && racingMentor.PrizeExperience == remainder,
            "concurrent apprentice credit and mentor claim preserve every contribution in either valid ordering");
        for (int length = 0; length < 32; length++) MentorApprentice.MentorApprentices(new byte[length], racingChild);
        var request = new MentorApprentice(true); request.Name = new string('x', 100); request.Name = "Test";
        Check(racingChild.Mentor.ID == racingMentor.Entity.UID && request.Name == "Test" && request.ToArray().Length == 36,
            "short relationship frames are ignored and replacing a long request name preserves a bounded response");
        var duplicateMentor = StoredProgressionPlayer(map); var duplicateChild = StoredProgressionPlayer(map);
        MentorLink(duplicateMentor, duplicateChild, 600);
        Query("ALTER TABLE apprentice DROP CONSTRAINT apprentice_pkey");
        Query("INSERT INTO apprentice SELECT * FROM apprentice WHERE apprenticeid=@uid", "uid", (long)duplicateChild.Entity.UID);
        rejected = false;
        try { ClaimMentor(duplicateMentor); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && Fenced(duplicateMentor) && duplicateMentor.Entity.Experience == 5000
            && Scalar("SELECT sum(actual_experience) FROM apprentice WHERE apprenticeid=@uid", duplicateChild.Entity.UID) == 1200,
            "ambiguous duplicate historical relationships cannot be silently aggregated into a payable claim");
    }
}
