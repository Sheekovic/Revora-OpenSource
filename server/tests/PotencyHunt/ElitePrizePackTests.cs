using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type PrizePacks = typeof(Entity).Assembly.GetType("Revora.Game.Features.ElitePkPacks");
    static long PrizeReceipts(GameState p) { return Scalar("SELECT count(*) FROM revora_epk_pack_openings WHERE uid=@uid", p.Entity.UID); }
    static string PrizeContents(GameState p) {
        return string.Join(";", p.Inventory.Objects.GroupBy(i => i.ID).OrderBy(g => g.Key).Select(g => g.Key + ":" + g.Count()));
    }
    static void VerifyPrizePacks(string runtime, Map town) {
        VerifyFirstRebirth(runtime,town);
        VerifyNestedPrizes(town);
        string[] lower = {
            "723341:1;729626:1", "720731:1;723341:2;729626:1",
            "720731:2;723341:3;729626:1", "720731:3;723341:5;729626:1;1088000:1"
        };
        string[] upper = {
            "720598:1;720731:1;723341:1;723700:3;729626:1",
            "720598:1;720731:2;723342:1;723700:5;729626:2",
            "720598:2;720730:1;720731:3;723342:1;729626:3;1088000:1",
            "720049:1;720598:3;720730:3;720731:5;723342:3;729626:5;1088000:3"
        };
        ulong curve = DataHolder.LevelExperience(135);
        ulong[] exp = {curve / 100 * 15, curve / 10 * 3, curve / 2, curve};
        for (uint id = 720714; id <= 720729; id++) foreach (bool bound in new[] {false,true}) {
            var p = BallPlayer(town); var pack = Pack(p,id,1); pack.Bound = bound;
            Query("UPDATE items SET bound=@bound WHERE uid=@uid","bound",bound ? 1 : 0,"uid",(long)pack.UID);
            int rank = (int)(id-720714)%4; bool experience = id < 720726; uint cp = experience ? 0u : rank == 0 ? 3000000u : 5000000u;
            ItemHandler.UseItem(pack,p);
            Check(!Fenced(p) && PrizeReceipts(p) == 1 && p.Entity.ConquerPoints == cp && !p.Inventory.ContainsUID(pack.UID)
                && PrizeContents(p) == (experience ? lower : upper)[rank]
                && p.Inventory.Objects.All(i => i.Bound == (bound || !experience && (i.ID == 723700 || i.ID == 720730)))
                && p.Entity.Level == (experience && rank == 3 ? 136 : 135)
                && p.Entity.Experience == 5000 + (experience && rank != 3 ? exp[rank] : 0)
                && BallCount(p) == 0, "owned prize grants its exact legacy bundle, binding and EXP/CP amounts in one receipt: " + id + "/" + bound);
            ItemHandler.UseItem(pack,p); EntityTable.SaveEntity(p); p = ReloadBall(p);
            Check(!Fenced(p) && PrizeReceipts(p) == 1 && p.Entity.ConquerPoints == cp && PrizeContents(p) == (experience ? lower : upper)[rank]
                && Scalar("SELECT count(*) FROM items WHERE uid=@uid",pack.UID) == 0
                && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid",p.Entity.UID) == 0,
                "production reload preserves complete bundle and deleted pack replay cannot fabricate prizes or honors: " + id + "/" + bound);
        }
        foreach (uint id in new uint[] {720714,720729}) {
            int rewards = id == 720714 ? 2 : 23;
            var p = BallPlayer(town); var pack = Pack(p,id,1);
            while (p.Inventory.Count < 42-rewards) Pack(p,1000000,1);
            ItemHandler.UseItem(pack,p);
            Check(!Fenced(p) && PrizeReceipts(p) == 0 && p.Inventory.ContainsUID(pack.UID), "insufficient complete-bundle capacity preserves prize " + id);
            var filler = p.Inventory.Objects.First(i => i.ID == 1000000);
            p.Inventory.Remove(filler,Enums.ItemUse.Remove); ItemHandler.UseItem(pack,p);
            Check(!Fenced(p) && PrizeReceipts(p) == 1 && p.Inventory.Count == 40 && !p.Inventory.ContainsUID(pack.UID),
                "exact capacity counts the slot freed by the consumed singleton " + id);
        }
        var race = BallPlayer(town); var stack = Pack(race,720714,3);
        Parallel.For(0,36,i => { if (i % 3 == 0) EntityTable.SaveEntity(race); else ItemHandler.UseItem(stack,race); });
        Check(!Fenced(race) && PrizeReceipts(race) == 3 && race.Inventory.Count == 6 && !race.Inventory.ContainsUID(stack.UID)
            && race.Entity.Experience == 5000 + 3 * exp[0], "concurrent opening and autosave consume exactly three owned units and all three complete bundles");
        var money = BallPlayer(town); var cpPack = Pack(money,720729,1);
        Parallel.For(0,20,i => ItemHandler.UseItem(cpPack,money));
        Check(!Fenced(money) && PrizeReceipts(money) == 1 && money.Entity.ConquerPoints == 5000000 && money.Inventory.Count == 23,
            "concurrent CP prize replay grants its purse and 23 items once");
        foreach (string fault in new[] {"profile","cp","delete","stack","late-item","receipt","skills","recovery"}) {
            var p = fault == "recovery" ? PendingFirstLife(town) : BallPlayer(town);
            var pack = Pack(p,fault == "cp" ? 720729u : 720725u,(ushort)(fault == "stack" ? 2 : 1));
            ulong before = p.Entity.Experience; byte level = p.Entity.Level; int count = p.Inventory.Count;
            string table = fault == "profile" || fault == "cp" ? "entities" : fault == "receipt" ? "revora_epk_pack_openings"
                : fault == "skills" ? "skills" : fault == "recovery" ? "revora_first_rebirths" : "items";
            string verb = fault == "late-item" || fault == "receipt" || fault == "skills" ? "INSERT" : fault == "delete" ? "DELETE" : "UPDATE";
            string body = fault == "late-item" ? "IF NEW.id=729626 THEN RETURN NULL; END IF; RETURN NEW;" : "RETURN NULL;";
            Query("CREATE FUNCTION suppress_prize() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN " + body + " END';CREATE TRIGGER suppress_prize BEFORE " + verb + " ON " + table + " FOR EACH ROW EXECUTE FUNCTION suppress_prize()");
            ItemHandler.UseItem(pack,p);
            Check(Fenced(p) && PrizeReceipts(p) == 0 && p.Entity.Experience == before && p.Entity.Level == level
                && p.Entity.ConquerPoints == 0 && p.Inventory.Count == count && p.Inventory.ContainsUID(pack.UID)
                && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0",p.Entity.UID) == count
                && SavedRune(pack,"stacksize") == pack.StackSize && (fault != "recovery" || LifePending(p) == 1),
                "suppressed " + fault + " write preserves the entire original pack and rolls back every reward");
            Query("DROP TRIGGER suppress_prize ON " + table + ";DROP FUNCTION suppress_prize()");
            p = ReloadBall(p); ItemHandler.UseItem(p.Inventory.Objects.Single(i => i.UID == pack.UID),p);
            Check(!Fenced(p) && PrizeReceipts(p) == 1 && (fault != "recovery" || p.Entity.Level == 139 && LifePending(p) == 0),
                "production reload retries the preserved full prize after " + fault + " failure");
        }
        var overflow = BallPlayer(town); overflow.Entity.ConquerPoints = uint.MaxValue-100; var overflowPack = Pack(overflow,720729,1);
        ItemHandler.UseItem(overflowPack,overflow);
        Check(!Fenced(overflow) && PrizeReceipts(overflow) == 0 && overflow.Inventory.ContainsUID(overflowPack.UID)
            && overflow.Entity.ConquerPoints == uint.MaxValue-100, "CP overflow preserves the complete prize without partial items");
        var stale = BallPlayer(town); var stalePack = Pack(stale,720729,1);
        Query("UPDATE entities SET conquerpoints=99 WHERE uid=@uid","uid",(long)stale.Entity.UID); ItemHandler.UseItem(stalePack,stale);
        Check(Fenced(stale) && PrizeReceipts(stale) == 0 && stale.Inventory.Count == 1 && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",stale.Entity.UID) == 99,
            "stale CP purse cannot overwrite a newer balance or partially open a prize");
        foreach (bool hidden in new[] {true,false}) {
            var p = BallPlayer(town); var pack = Pack(p,720714,1); var other = Pack(p,1000000,1);
            if (hidden) p.Inventory.Remove(other.UID,Enums.ItemUse.None,false); else Query("DELETE FROM items WHERE uid=@uid","uid",(long)other.UID);
            ItemHandler.UseItem(pack,p);
            Check(Fenced(p) && PrizeReceipts(p) == 0 && p.Entity.Experience == 5000 && p.Inventory.ContainsUID(pack.UID),
                "durable bag membership mismatch fences before creating a partial reward: hidden=" + hidden);
        }
        foreach (string change in new[] {"entityid=1","stacksize=2","bound=1","locked=1","warehouse=1","position=1","suspicious=1","inwardrobe=1","days='1'"}) {
            var p = BallPlayer(town); var pack = Pack(p,720714,1);
            Query("UPDATE items SET " + change + " WHERE uid=@uid","uid",(long)pack.UID); ItemHandler.UseItem(pack,p);
            Check(Fenced(p) && PrizeReceipts(p) == 0 && p.Entity.Experience == 5000 && p.Inventory.ContainsUID(pack.UID), "changed pack preimage cannot grant rewards: " + change);
        }
        var cap = BallPlayer(town); cap.Entity.Level = Config.LevelCap; EntityTable.SaveEntity(cap); var capped = Pack(cap,720725,1);
        ItemHandler.UseItem(capped,cap);
        Check(!Fenced(cap) && PrizeReceipts(cap) == 1 && cap.Entity.Level == Config.LevelCap && cap.Entity.Experience == 5000 && cap.Inventory.Count == 10
            && Scalar("SELECT experience FROM revora_epk_pack_openings WHERE uid=@uid",cap.Entity.UID) == 0, "maximum-level character retains existing item reward entitlement without extra EXP");
        var guarded = BallPlayer(town); var kept = Pack(guarded,720714,1);
        guarded.Trade.InTrade = true; ItemHandler.UseItem(kept,guarded); guarded.Trade.InTrade = false;
        guarded.Fake = true; ItemHandler.UseItem(kept,guarded); guarded.Fake = false;
        guarded.Entity.FullyLoaded = false; ItemHandler.UseItem(kept,guarded); guarded.Entity.FullyLoaded = true;
        kept.Lock = 1; ItemHandler.UseItem(kept,guarded); kept.Lock = 0;
        kept.Days = 1; kept.DayStamp = DateTime.Now.AddDays(-2); ItemHandler.UseItem(kept,guarded); kept.Days = 0;
        ItemHandler.UseItem(new ConquerItem(true) {UID=kept.UID,ID=kept.ID},guarded);
        Check(!Fenced(guarded) && PrizeReceipts(guarded) == 0 && guarded.Entity.Experience == 5000 && guarded.Inventory.ContainsUID(kept.UID),
            "trade, fake actor, incomplete login, locks, expiry and substituted reference cannot spend a pack");
        ItemHandler.UseItem(kept,guarded); Check(!Fenced(guarded) && PrizeReceipts(guarded) == 1, "normal eligibility restored permits complete prize consumption");
        var legacy = BallPlayer(town); var zero = Pack(legacy,720714,0); ItemHandler.UseItem(zero,legacy);
        Check(!Fenced(legacy) && PrizeReceipts(legacy) == 1 && legacy.Inventory.Count == 2, "legacy zero-stack single prize opens once");
        Query("CREATE TABLE IF NOT EXISTS apprentice AS TABLE public.apprentice WITH NO DATA");
        var mentor = BallPlayer(town); var child = BallPlayer(town); MentorLink(mentor,child,100);
        var mentorPack = Pack(child,720725,1); ItemHandler.UseItem(mentorPack,child);
        Check(!Fenced(child) && PrizeReceipts(child) == 1 && MentorBalance(child) == 372 && child.Entity.Level == 136 && child.Inventory.Count == 10,
            "earned level from a complete prize credits the mentor in the same transaction");
        var failedChild = BallPlayer(town); MentorLink(mentor,failedChild,100); var failedPack = Pack(failedChild,720725,1);
        Query("CREATE FUNCTION suppress_prize() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_prize BEFORE UPDATE ON apprentice FOR EACH ROW EXECUTE FUNCTION suppress_prize()");
        ItemHandler.UseItem(failedPack,failedChild);
        Check(Fenced(failedChild) && PrizeReceipts(failedChild) == 0 && failedChild.Entity.Level == 135
            && failedChild.Entity.Experience == 5000 && MentorBalance(failedChild) == 100 && failedChild.Inventory.Count == 1
            && failedChild.Inventory.ContainsUID(failedPack.UID), "mentor failure rolls back the whole item bundle, source pack and progression");
        Query("DROP TRIGGER suppress_prize ON apprentice;DROP FUNCTION suppress_prize()");
        failedChild = ReloadBall(failedChild); Revora.Database.KnownPersons.LoadMentor(failedChild);
        ItemHandler.UseItem(failedChild.Inventory.Objects.Single(),failedChild);
        Check(!Fenced(failedChild) && PrizeReceipts(failedChild) == 1 && failedChild.Entity.Level == 136
            && MentorBalance(failedChild) == 372 && failedChild.Inventory.Count == 10, "mentor rollback can retry the same complete prize after production reload");

    }
}
