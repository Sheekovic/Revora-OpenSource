using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Revora.Database;
using Revora.Network.GamePackets;

internal static class CheckSignInMilestones
{
    static void Require(bool condition, string message) {
        if (!condition) throw new InvalidOperationException(message);
    }
    static int Main() {
        SignInTable.Load();
        var next = typeof(MsgSignIn).GetMethod("NextMilestone", BindingFlags.Static | BindingFlags.NonPublic);
        Require(next != null, "Compiled sign-in milestone contract missing.");
        Func<byte, byte> milestone = claimed => (byte)next.Invoke(null, new object[] { claimed });
        foreach (int days in new[] { 28, 29, 30, 31 }) {
            byte claimed = 0;
            var awards = new List<byte>();
            for (int signed = 1; signed <= days; signed++) {
                byte threshold = milestone(claimed);
                if (threshold != 0 && signed >= threshold) {
                    Require(SignInTable.Prizes.ContainsKey(threshold), "Milestone has no loaded prize.");
                    awards.Add(threshold); claimed = threshold;
                }
            }
            byte[] expected = days < 30 ? new byte[] { 2, 7, 14, 21 } : new byte[] { 2, 7, 14, 21, 30 };
            Require(awards.SequenceEqual(expected), "Month length duplicates or skips a reward.");
        }
        Require(!SignInTable.Prizes.ContainsKey(28), "Review changed catalog before validating legacy 28-day state.");
        Require(milestone(28) == 0 && milestone(30) == 0, "Completed or legacy final state can claim twice.");
        Require(milestone(255) == 0, "Unknown saved state must not reopen the ladder.");
        Require(SignInTable.Prizes[7].PrizeItemID == 3100014 && SignInTable.Prizes[14].PrizeItemID == 3100015,
            "Easter supplier milestone identities changed.");
        var ready = typeof(MsgSignIn).GetMethod("CatalogReady", BindingFlags.Static | BindingFlags.NonPublic);
        Require((bool)ready.Invoke(null, null), "Reviewed catalog should admit sign-in.");
        foreach (byte day in new byte[] { 1, 2, 7, 14, 21, 30 }) {
            var saved = SignInTable.Prizes[day]; SignInTable.Prizes.Remove(day);
            Require(!(bool)ready.Invoke(null, null), "Missing prize must close paid sign-in.");
            SignInTable.Prizes[day] = saved;
        }
        SignInTable.Prizes = null;
        Require(!(bool)ready.Invoke(null, null), "Unloaded catalog must close paid sign-in.");
        Console.WriteLine("Sign-in 28/29/30/31-day month boundaries, terminal replay and loaded prize identities passed.");
        return 0;
    }
}
