using System;
using System.Linq;
using System.Threading.Tasks;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.Features;
using Revora.Network;
using Revora.Network.GamePackets;
using Status = Revora.Network.GamePackets.MsgTaskStatus.QuestData.QuestStatus;

internal static partial class PotencyHuntTests
{
    static readonly Type TrojanStore = typeof(Entity).Assembly.GetType("Revora.Game.Features.TrojanEpicStore");
    static readonly Type TrojanJournal = typeof(Entity).Assembly.GetType("Revora.Game.Features.TrojanEpicJournal");
    static readonly uint[] TrojanMissions = { 3269, 3278, 3275, 3270, 3276, 3271, 3277, 3272, 3273, 3279, 3280, 3274 };
    static DateTime trojanNow;
    // Storage-level world proof only. Real NPC/death routes are separate unfinished work.
    static readonly Func<bool> TrojanContext = () => true;
    static object TrojanCall(GameState p, string method, params object[] args) {
        lock (p.ItemSyncRoot) return Call(TrojanStore, null, method, new object[] { p }.Concat(args).ToArray());
    }
    static object TrojanProgress(GameState p) { return TrojanCall(p, "Load"); }
    static int TrojanValue(GameState p, string name) { return Convert.ToInt32(Field(TrojanProgress(p), name)); }
    static GameState TrojanPlayer(Map map) { return PromotionPlayer(map, 15, 120, 10001); }
    static void TrojanJournalCheck(GameState p) {
        int stage = TrojanValue(p, "Stage");
        var list = new System.Collections.Generic.List<MsgTaskStatus.QuestData>(); QuestListSystem.Fill(p, list);
        var route = list.Where(x => TrojanMissions.Contains((uint)x.UID)).ToArray();
        int finished = stage == 14 ? 12 : Math.Max(0, Math.Min(stage - 1, 11));
        Check(route.Length == 12 && route.Select(x => (uint)x.UID).SequenceEqual(TrojanMissions)
            && route.Take(finished).All(x => x.Status == Status.Finished)
            && (stage == 0 || stage == 14 || route[finished].Status == Status.Accepted)
            && route.Skip(finished + (stage > 0 && stage < 14 ? 1 : 0)).All(x => x.Status == Status.Available),
            "journal projects twelve unique ordered missions from durable stage " + stage);
        var packets = (MsgTaskDetailInfo[])Call(TrojanJournal, null, "Details", TrojanProgress(p));
        Check(packets.Length == 2 && packets[0].UID == (QuestID)3272 && packets[1].UID == (QuestID)3274
            && BitConverter.ToUInt16(packets[0].ToArray(), 2) == 1135
            && BitConverter.ToUInt32(packets[0].ToArray(), 12) == (stage > 8 ? 1u : 0)
            && BitConverter.ToUInt32(packets[1].ToArray(), 12) == (stage >= 13 ? 1u : 0),
            "native traitor and final objective-one flags reflect recorded victories at stage " + stage);
    }
    static bool TrojanAdvance(GameState p) {
        int stage = TrojanValue(p, "Stage"), revision = TrojanValue(p, "Revision");
        Guid? encounter = null;
        if (stage == 6 || stage == 8 || stage == 12) {
            bool won = TrojanCombatAdvance(p); if (won) TrojanJournalCheck(p); return won;
        }
        bool advanced = (bool)TrojanCall(p, "Advance", revision, encounter, TrojanContext);
        if (advanced) TrojanJournalCheck(p);
        return advanced;
    }
    static void VerifyTrojanEpicStore(string runtime) {
        VerifyPromotions(runtime); var map = Revora.Kernel.Maps[1004];
        Call(TrojanStore, null, "Initialize"); Call(TrojanStore, null, "Initialize");
        TrojanCombatCatalog();
        Check(!(bool)Call(TrojanJournal, null, "Protect", 3269u), "Trojan journal remains disabled until explicitly initialized");
        Call(TrojanJournal, null, "Initialize"); Call(TrojanJournal, null, "Initialize");
        trojanNow = new DateTime(2035, 1, 1, 23, 59, 59, DateTimeKind.Utc);
        TrojanStore.GetField("Clock", Hidden).SetValue(null, new Func<DateTime>(() => trojanNow));
        try {
            var p = TrojanPlayer(map); uint cp = p.Entity.ConquerPoints; ulong silver = p.Entity.Money;
            p.Quests = new Quests(p); p.Quests.Load();
            foreach (uint mission in TrojanMissions) { p.Quests.Accept((QuestID)mission); p.Quests.IncreaseQuestKills((QuestID)mission, 9); }
            p.Quests.Save(); TrojanJournalCheck(p);
            foreach (uint mission in TrojanMissions) {
                var begin = new MsgTaskStatus(true, 1) { Action = MsgTaskStatus.QuestAction.Begin };
                begin.Apend(MsgTaskStatus.QuestData.Create((QuestID)mission, Status.Finished, 0));
                MsgTaskStatus.Process(begin.ToArray(), p); PacketHandler.HandlePacket(AbyssQuit(mission), p);
                Check(TrojanValue(p, "Stage") == 0 && p.Quests.GetQuest((QuestID)mission).Kills == 9
                    && p.Quests.CheckQuest((QuestID)mission) == Status.Accepted,
                    "client Begin and quit cannot author story progress or erase legacy data: " + mission);
            }
            Check(!(bool)Call(TrojanJournal, null, "Protect", 3268u) && !(bool)Call(TrojanJournal, null, "Protect", 3281u),
                "Trojan protection leaves neighboring missions unchanged");
            Check(p.Account.State == AccountTable.AccountState.Player && TrojanValue(p, "Stage") == 0,
                "ordinary Trojan starts without invented quest progress");
            Check(!TrojanAdvance(p), "unaccepted story cannot advance");
            Parallel.For(0, 12, _ => TrojanCall(p, "Begin", 0, TrojanContext));
            Check(TrojanValue(p, "Revision") == 1 && Scalar("SELECT count(*) FROM revora_trojan_epic_steps WHERE uid=@uid", p.Entity.UID) == 1,
                "concurrent acceptance creates one story and receipt");
            Parallel.For(0, 12, _ => TrojanCall(p, "Advance", 1, null, TrojanContext));
            Check(TrojanValue(p, "Energy") == 1 && TrojanValue(p, "Pulses") == 1,
                "concurrent repeated action spends exactly one daily energy");
            Check(!TrojanAdvance(p), "same-day request cannot consume tomorrow's energy");
            trojanNow = trojanNow.AddSeconds(1);
            Check(TrojanAdvance(p) && TrojanValue(p, "Energy") == 2, "UTC midnight permits the next energy");
            p = ReloadBall(p);
            p.Quests = new Quests(p); p.Quests.Load(); TrojanJournalCheck(p);
            Check(TrojanMissions.All(id => p.Quests.GetQuest((QuestID)id).Kills == 9), "journal projection preserves serialized historical objectives across reload");
            Check(TrojanValue(p, "Energy") == 2 && !TrojanAdvance(p), "fresh reload retains the daily limit without an extra character save");
            trojanNow = trojanNow.AddDays(-2);
            Check(!TrojanAdvance(p), "clock rollback cannot reopen a consumed day");
            trojanNow = trojanNow.AddDays(2);
            p.Entity.MyAchievement.Send();Check(!HasAchievement(p,10525),"Trojan NPC story and daily energy advances cannot invent unarmed kill credit");
            bool checkedEncounter = false, checkedLoan = false;
            var checkedScenes = new System.Collections.Generic.HashSet<int>();
            for (int guard = 0; TrojanValue(p, "Stage") != 13 && guard < 220; guard++) {
                int sceneStage = TrojanValue(p, "Stage");
                if ((sceneStage == 3 || sceneStage == 9 || sceneStage == 11) && checkedScenes.Add(sceneStage)) p = VerifyTrojanScene(p);
                if (TrojanValue(p, "Stage") == 11) {
                    while (p.Inventory.Count < 40) Pack(p, 1088001, 1);
                    Check(!TrojanAdvance(p) && TrojanValue(p, "Stage") == 11, "full bag preserves the unissued blade step");
                    foreach (var filler in p.Inventory.Objects.ToArray()) DisRemove(p, filler);
                    foreach (string table in new[] { "items", "revora_trojan_epic_blades", "revora_trojan_epic_steps" }) {
                        Query("CREATE FUNCTION trojan_loan_fail() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
                        Query("CREATE TRIGGER trojan_loan_fail BEFORE INSERT ON " + table + " FOR EACH ROW EXECUTE FUNCTION trojan_loan_fail()");
                        bool failed = false;
                        try { TrojanAdvance(p); } catch (InvalidOperationException) { failed = true; }
                        finally { Query("DROP TRIGGER trojan_loan_fail ON " + table); Query("DROP FUNCTION trojan_loan_fail()"); }
                        p = ReloadBall(p);
                        Check(failed && TrojanValue(p, "Stage") == 11 && p.Inventory.Count == 0
                            && Scalar("SELECT count(*) FROM revora_trojan_epic_blades WHERE uid=@uid", p.Entity.UID) == 0,
                            "failed borrowing rolls back item, loan receipt and story: " + table);
                    }
                }
                if (TrojanValue(p, "Stage") == 12 && !checkedLoan) {
                    p = ReloadBall(p); var loan = p.Inventory.Objects.Single(i => i.ID == 3003340);
                    Check(loan.Bound && loan.StackSize == 1, "borrowed Solar Blade persists as one bound owned quest item");
                    int rev = TrojanValue(p, "Revision");
                    var encounter = (Guid)TrojanCall(p, "OpenEncounter", rev, TrojanContext);
                    Query("UPDATE items SET warehouse=100 WHERE uid=@uid", "uid", (long)loan.UID); p = ReloadBall(p);
                    Check(!(bool)TrojanCall(p, "RecoverBlade", rev, TrojanContext), "warehouse blade cannot be duplicated through recovery");
                    Query("UPDATE items SET warehouse=0 WHERE uid=@uid", "uid", (long)loan.UID); p = ReloadBall(p);
                    DisRemove(p, p.Inventory.Objects.Single(i => i.UID == loan.UID));
                    var forged = Pack(p, 3003340, 1); forged.Bound = true;
                    Query("UPDATE items SET bound=1 WHERE uid=@uid", "uid", (long)forged.UID);
                    Check((Guid)TrojanCall(p, "OpenEncounter", rev, TrojanContext) == Guid.Empty
                        && !(bool)TrojanCall(p, "Advance", rev, encounter, TrojanContext),
                        "a matching item type cannot replace the issued blade for summoning or a pending victory");
                    DisRemove(p, forged);
                    foreach (string table in new[] { "items", "revora_trojan_epic_blades" }) {
                        Query("CREATE FUNCTION trojan_recover_fail() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
                        Query("CREATE TRIGGER trojan_recover_fail BEFORE INSERT OR UPDATE ON " + table + " FOR EACH ROW EXECUTE FUNCTION trojan_recover_fail()");
                        bool failed = false;
                        try { TrojanCall(p, "RecoverBlade", rev, TrojanContext); } catch (InvalidOperationException) { failed = true; }
                        finally { Query("DROP TRIGGER trojan_recover_fail ON " + table); Query("DROP FUNCTION trojan_recover_fail()"); }
                        p = ReloadBall(p);
                        Check(failed && p.Inventory.Count == 0 && TrojanValue(p, "Revision") == rev,
                            "failed recovery leaves the blade absent and progress unchanged: " + table);
                    }
                    Parallel.For(0, 12, _ => TrojanCall(p, "RecoverBlade", rev, TrojanContext));
                    p = ReloadBall(p);
                    Check(p.Inventory.Objects.Count(i => i.ID == 3003340 && i.UID == loan.UID && i.Bound) == 1
                        && Scalar("SELECT recoveries FROM revora_trojan_epic_blades WHERE uid=@uid", p.Entity.UID) == 1,
                        "concurrent recovery restores only the original blade identity once");
                    Check(!(bool)TrojanCall(p, "RecoverBlade", rev, TrojanContext), "present blade cannot be recovered again");
                    checkedLoan = true;
                }
                if (TrojanValue(p, "Stage") == 6 && !checkedEncounter) {
                    int rev = TrojanValue(p, "Revision");
                    var stale = (Guid)TrojanCall(p, "OpenEncounter", rev, TrojanContext);
                    var current = (Guid)TrojanCall(p, "OpenEncounter", rev, TrojanContext);
                    Check(stale != current && !(bool)TrojanCall(p, "Advance", rev, stale, TrojanContext)
                        && !(bool)TrojanCall(p, "Advance", rev, Guid.NewGuid(), TrojanContext)
                        && !(bool)TrojanCall(p, "Advance", rev, null, TrojanContext),
                        "retired, invented and absent encounters cannot award Ghost Reaver credit");
                    Query("CREATE FUNCTION trojan_combat_fail() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER trojan_combat_fail BEFORE INSERT ON revora_trojan_epic_steps FOR EACH ROW EXECUTE FUNCTION trojan_combat_fail()");
                    bool failed = false;
                    try { TrojanCall(p, "Advance", rev, current, TrojanContext); } catch (InvalidOperationException) { failed = true; }
                    finally { Query("DROP TRIGGER trojan_combat_fail ON revora_trojan_epic_steps;DROP FUNCTION trojan_combat_fail()"); }
                    Check(failed && TrojanValue(p, "Revision") == rev
                        && Scalar("SELECT count(*) FROM revora_trojan_epic_encounters WHERE uid=@uid AND state='victory'", p.Entity.UID) == 0,
                        "failed combat receipt rolls back both victory and story progress");
                    checkedEncounter = true;
                }
                if (!TrojanAdvance(p)) { trojanNow = trojanNow.AddDays(1); Check(TrojanAdvance(p), "next UTC day permits its pending task"); }
                int stage = TrojanValue(p, "Stage"), energy = TrojanValue(p, "Energy");
                if (stage == 2) Check(energy == 27, "27 scraps end the first stage at energy 27");
                if (stage == 5) Check(energy == 49, "23 overlapping array tasks end at energy 49");
                if (stage == 7) Check(energy == 66, "18 registered Ghost Reaver victories end at energy 66");
                if (stage == 10) Check(energy == 90, "25 essence tasks end at energy 90");
                if (stage == 4) Check((int)Call(TrojanStore, null, "Objective", TrojanProgress(p)) == TrojanValue(p, "Pulses") * 10000 / 23,
                    "array journal progress derives from durable tasks");
            }
            Check(TrojanValue(p, "Stage") == 13 && TrojanValue(p, "Energy") == 90
                && Scalar("SELECT count(*) FROM revora_trojan_epic_energy WHERE uid=@uid", p.Entity.UID) == 90,
                "full story requires exactly 90 distinct daily energy receipts");
            Check(Scalar("SELECT count(*) FROM revora_trojan_epic_encounters WHERE uid=@uid AND state='victory'", p.Entity.UID) == 20,
                "story requires 18 reavers, the traitor and the final Devastator");
            Check(!TrojanAdvance(p) && !(bool)TrojanCall(p, "Begin", TrojanValue(p, "Revision"), TrojanContext)
                && p.Entity.ConquerPoints == cp && p.Entity.Money == silver && p.Inventory.Count == 1,
                "victory retains the borrowed blade for an explicit reward claim, without an extra charge");
            p = ReloadBall(p);
            p.Entity.MyAchievement.Send();Check(HasAchievement(p,10525),"earned Trojan unarmed credit survives full production victory reload");
            Check(TrojanValue(p, "Stage") == 13 && TrojanValue(p, "Completed") == 0,
                "cold-reloaded victory is not misreported as a claimed completion");

            var finalLoan = p.Inventory.Objects.Single(i => i.ID == 3003340);
            DisRemove(p, finalLoan);
            while (p.Inventory.Count < 40) Pack(p, 1088001, 1);
            int victoryRevision = TrojanValue(p, "Revision");
            Check(!(bool)TrojanCall(p, "Claim", victoryRevision, TrojanContext)
                && !(bool)TrojanCall(p, "RecoverBlade", victoryRevision, TrojanContext),
                "missing blade cannot be returned and a full bag cannot recover it");
            DisRemove(p, p.Inventory.Objects.First());
            Check((bool)TrojanCall(p, "RecoverBlade", victoryRevision, TrojanContext)
                && p.Inventory.Objects.Any(i => i.UID == finalLoan.UID && i.ID == 3003340 && i.Bound),
                "post-victory recovery restores the same loan without repeating combat");
            foreach (var filler in p.Inventory.Objects.Where(i => i.ID != 3003340).ToArray()) DisRemove(p, filler);

            foreach (string table in new[] { "items", "revora_trojan_epic_rewards", "revora_trojan_epic_steps", "revora_trojan_epic", "revora_trojan_epic_blades" }) {
                Query("CREATE FUNCTION trojan_claim_fail() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
                Query("CREATE TRIGGER trojan_claim_fail BEFORE INSERT OR UPDATE ON " + table + " FOR EACH ROW EXECUTE FUNCTION trojan_claim_fail()");
                bool denied = false;
                try { TrojanCall(p, "Claim", TrojanValue(p, "Revision"), TrojanContext); } catch (InvalidOperationException) { denied = true; }
                finally { Query("DROP TRIGGER trojan_claim_fail ON " + table); Query("DROP FUNCTION trojan_claim_fail()"); }
                p = ReloadBall(p);
                Check(denied && TrojanValue(p, "Stage") == 13 && TrojanValue(p, "Completed") == 0
                    && p.Inventory.Objects.Count(i => i.ID == 3003340 && i.Bound) == 1
                    && Scalar("SELECT count(*) FROM revora_trojan_epic_rewards WHERE uid=@uid", p.Entity.UID) == 0,
                    "failed claim rolls back weapon and completion across cold reload: " + table);
            }
            Query("CREATE FUNCTION trojan_enhance_fail() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER trojan_enhance_fail BEFORE UPDATE ON items FOR EACH ROW EXECUTE FUNCTION trojan_enhance_fail()");
            bool enhanceFailed = false;
            try { TrojanCall(p, "Claim", TrojanValue(p, "Revision"), TrojanContext); } catch (InvalidOperationException) { enhanceFailed = true; }
            finally { Query("DROP TRIGGER trojan_enhance_fail ON items;DROP FUNCTION trojan_enhance_fail()"); }
            Check(enhanceFailed && TrojanValue(p, "Stage") == 13 && p.Inventory.Count == 1,
                "missing enhancement write cannot commit an underpowered reward");
            Query("CREATE FUNCTION trojan_return_fail() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER trojan_return_fail BEFORE DELETE ON items FOR EACH ROW EXECUTE FUNCTION trojan_return_fail()");
            bool returnFailed = false;
            try { TrojanCall(p, "Claim", TrojanValue(p, "Revision"), TrojanContext); } catch (InvalidOperationException) { returnFailed = true; }
            finally { Query("DROP TRIGGER trojan_return_fail ON items;DROP FUNCTION trojan_return_fail()"); }
            p = ReloadBall(p);
            Check(returnFailed && TrojanValue(p, "Stage") == 13 && p.Inventory.Objects.Single().ID == 3003340,
                "failed blade return cannot commit the weapon or completion");
            while (p.Inventory.Count < 40) Pack(p, 1088001, 1);
            int claimRevision = TrojanValue(p, "Revision");
            Parallel.For(0, 12, _ => TrojanCall(p, "Claim", claimRevision, TrojanContext));
            p = ReloadBall(p);
            Check(TrojanValue(p, "Stage") == 14 && TrojanValue(p, "Completed") == 1
                && p.Inventory.Objects.Count(i => i.ID == 614439 && !i.Bound && i.Plus == 12 && i.Bless == 7
                    && i.SocketOne == Revora.Game.Enums.Gem.EmptySocket && i.SocketTwo == Revora.Game.Enums.Gem.EmptySocket) == 1
                && Scalar("SELECT count(*) FROM revora_trojan_epic_rewards WHERE uid=@uid", p.Entity.UID) == 1
                && p.Inventory.Count == 40 && !p.Inventory.Objects.Any(i => i.ID == 3003340)
                && Scalar("SELECT count(*) FROM revora_trojan_epic_blades WHERE uid=@uid AND state='returned'", p.Entity.UID) == 1,
                "concurrent full-bag claims exchange the loan for exactly one persisted unbound level-140 +12 -7 two-socket saber");
            Check(!(bool)TrojanCall(p, "Claim", claimRevision, TrojanContext), "reloaded reward replay cannot grant another saber");
            TrojanJournalCheck(p);
            p.Entity.Class = 25; TrojanJournalCheck(p); p.Entity.Class = 15;
            Check(!(bool)TrojanCall(p, "RecoverBlade", TrojanValue(p, "Revision"), TrojanContext), "claimed cycle cannot recreate its returned blade");
            Check((bool)TrojanCall(p, "Begin", TrojanValue(p, "Revision"), TrojanContext) && !TrojanAdvance(p)
                && TrojanValue(p, "Run") == 2 && TrojanValue(p, "Completed") == 1,
                "repeat journey preserves completion history and cannot reuse the final day's free energy");
            trojanNow = trojanNow.AddDays(1);
            Check(TrojanAdvance(p) && TrojanValue(p, "Energy") == 1, "repeat journey starts earning next UTC day");

            foreach (string table in new[] { "revora_trojan_epic", "revora_trojan_epic_steps", "revora_trojan_epic_energy" }) {
                var failed = TrojanPlayer(map); TrojanCall(failed, "Begin", 0, TrojanContext);
                Query("CREATE FUNCTION trojan_suppress() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
                Query("CREATE TRIGGER trojan_suppress BEFORE INSERT OR UPDATE ON " + table + " FOR EACH ROW EXECUTE FUNCTION trojan_suppress()");
                bool denied = false;
                try { TrojanAdvance(failed); } catch (InvalidOperationException) { denied = true; }
                finally { Query("DROP TRIGGER trojan_suppress ON " + table); Query("DROP FUNCTION trojan_suppress()"); }
                failed = ReloadBall(failed);
                Check(denied && TrojanValue(failed, "Energy") == 0 && TrojanValue(failed, "Revision") == 1
                    && Scalar("SELECT count(*) FROM revora_trojan_epic_energy WHERE uid=@uid", failed.Entity.UID) == 0,
                    "suppressed write rolls back energy and story across reload: " + table);
                Check(TrojanAdvance(failed), "failed transaction leaves its daily energy available");
            }
            var changing = TrojanPlayer(map); int calls = 0; bool rejected = false;
            try { TrojanCall(changing, "Begin", 0, new Func<bool>(() => ++calls == 1)); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected && TrojanValue(changing, "Stage") == 0, "world context lost before commit rolls admission back");
            var wrong = PromotionPlayer(map, 25, 120, 10001); rejected = false;
            try { TrojanCall(wrong, "Begin", 0, TrojanContext); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected && TrojanValue(wrong, "Stage") == 0, "Warriors cannot start the Trojan story");
            wrong.Quests = new Quests(wrong); wrong.Quests.Load(); wrong.Quests.Accept((QuestID)3269);
            var hidden = new System.Collections.Generic.List<MsgTaskStatus.QuestData>(); QuestListSystem.Fill(wrong, hidden);
            Check(!hidden.Any(x => TrojanMissions.Contains((uint)x.UID)), "unadmitted non-Trojan cannot expose a forged legacy journal route");
            var low = PromotionPlayer(map, 10, 119, 10001); rejected = false;
            try { TrojanCall(low, "Begin", 0, TrojanContext); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "non-reborn Trojans below level 120 are ineligible");
            low.Entity.Reborn = 1; low.Entity.FirstRebornClass = 15; low.Entity.FirstRebornLevel = 120;
            EntityTable.SaveEntity(low);
            Check((bool)TrojanCall(low, "Begin", 0, TrojanContext), "reborn Trojan eligibility does not invent a second-rebirth restriction");
            var staleOwner = TrojanPlayer(map);
            Query("UPDATE entities SET class=25 WHERE uid=@uid", "uid", (long)staleOwner.Entity.UID);
            rejected = false;
            try { TrojanCall(staleOwner, "Begin", 0, TrojanContext); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected && TrojanValue(staleOwner, "Stage") == 0, "stale saved class cannot be overwritten to admit a Trojan quest");
            var replaced = TrojanPlayer(map); Revora.Kernel.GamePool[replaced.Entity.UID] = p;
            rejected = false;
            try { TrojanCall(replaced, "Begin", 0, TrojanContext); } catch (InvalidOperationException) { rejected = true; }
            finally { Revora.Kernel.GamePool[replaced.Entity.UID] = replaced; }
            Check(rejected && TrojanValue(replaced, "Stage") == 0, "replaced session cannot mutate another connection's story");
            VerifyTrojanPrivateNpcJourney(map, runtime);
            VerifyTrojanCapacity(map);
        } finally { TrojanStore.GetField("Clock", Hidden).SetValue(null, new Func<DateTime>(() => DateTime.UtcNow)); }
    }
}
