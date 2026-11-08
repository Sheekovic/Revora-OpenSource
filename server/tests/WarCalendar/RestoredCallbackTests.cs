using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

internal static class RestoredCallbackTests
{
    const BindingFlags Hidden = BindingFlags.Static | BindingFlags.NonPublic;
    static int checks;
    static void Check(bool value, string name)
    {
        if (!value) throw new InvalidOperationException(name);
        checks++;
    }
    static void VerifyPulse()
    {
        var assembly = typeof(Revora.Thread).Assembly;
        var thread = typeof(Revora.Thread);
        var super = assembly.GetType("Revora.Game.SuperGuildWar", true);
        var elite = assembly.GetType("Revora.Game.EliteGuildWar", true);
        var clan = assembly.GetType("Revora.Game.Features.ClanWarResults", true);
        var pulse = thread.GetMethod("TickRestoredEvents", Hidden);
        var saved = new List<Tuple<FieldInfo, object>>();
        Action<Type, string, object> set = (type, name, value) => {
            var field = type.GetField(name, Hidden);
            saved.Add(Tuple.Create(field, field.GetValue(null)));
            field.SetValue(null, value);
        };
        var output = System.Console.Out;
        var log = new StringWriter();
        var local = new DateTime(2035, 1, 1, 12, 0, 0, DateTimeKind.Local);
        var now = local.ToUniversalTime();
        int superCalls = 0, eliteCalls = 0, lastCalls = 0;
        bool fail = true;
        try
        {
            set(super, "ledgerReady", true);
            set(super, "nextSchedulePulse", DateTime.MaxValue);
            set(super, "Clock", (Func<DateTime>)(() => {
                superCalls++;
                if (fail) throw new InvalidOperationException("synthetic-pulse-secret");
                return now;
            }));
            set(elite, "ledgerReady", true);
            set(elite, "nextSchedulePulse", DateTime.MaxValue);
            set(elite, "Clock", (Func<DateTime>)(() => { eliteCalls++; return now; }));
            set(clan, "Clock", (Func<DateTime>)(() => { lastCalls++; return now; }));
            set(thread, "restoredTickReported", 0);
            System.Console.SetOut(log);
            pulse.Invoke(null, new object[] { local });
            Check(superCalls == 1 && eliteCalls == 1 && lastCalls == 1,
                "actual pulse attempts later war and final Clan callback after injected Super failure");
            Check(log.ToString().Contains("name=SuperGuildWar error=InvalidOperationException")
                && !log.ToString().Contains("synthetic-pulse-secret")
                && !log.ToString().Contains("restored-event-pulse complete"),
                "failed actual pulse logs redacted failure and does not report completion");
            Check((int)thread.GetField("restoredTickOwner", Hidden).GetValue(null) == 0,
                "failed pulse releases its owner");
            fail = false;
            pulse.Invoke(null, new object[] { local });
            Check(superCalls == 2 && eliteCalls == 2 && lastCalls == 2
                && (int)thread.GetField("restoredTickReported", Hidden).GetValue(null) == 1,
                "next actual pulse recovers and completes");
            string recovered = log.ToString();
            Check(recovered.Split(new[] { "restored-event-pulse complete" }, StringSplitOptions.None).Length == 2,
                "recovered pulse emits one completion marker");
            using (var entered = new System.Threading.ManualResetEventSlim())
            using (var release = new System.Threading.ManualResetEventSlim())
            {
                set(super, "Clock", (Func<DateTime>)(() => {
                    superCalls++; entered.Set();
                    if (!release.Wait(5000)) throw new TimeoutException("fixture owner release");
                    return now;
                }));
                var owner = System.Threading.Tasks.Task.Run(() => pulse.Invoke(null, new object[] { local }));
                try
                {
                    Check(entered.Wait(5000), "owning pulse reaches blocked callback");
                    pulse.Invoke(null, new object[] { local });
                    Check(superCalls == 3 && eliteCalls == 2 && lastCalls == 2,
                        "overlapping pulse cannot enter callbacks while another owns them");
                }
                finally { release.Set(); }
                Check(owner.Wait(5000) && eliteCalls == 3 && lastCalls == 3,
                    "owning pulse resumes remaining callbacks after release");
            }
            Check(log.ToString() == recovered && (int)thread.GetField("restoredTickOwner", Hidden).GetValue(null) == 0,
                "successful repeated pulse adds no failures or completion spam and releases ownership");
        }
        finally
        {
            System.Console.SetOut(output);
            for (int i = saved.Count - 1; i >= 0; i--) saved[i].Item1.SetValue(null, saved[i].Item2);
            ((Dictionary<string, DateTime>)thread.GetField("restoredTickFailures", Hidden).GetValue(null)).Remove("SuperGuildWar");
            log.Dispose();
        }
    }
    static int Main()
    {
        var type = typeof(Revora.Thread);
        var method = type.GetMethod("RunRestoredTick", Hidden);
        var failures = (Dictionary<string, DateTime>)type.GetField("restoredTickFailures", Hidden).GetValue(null);
        var output = System.Console.Out;
        var captured = new StringWriter();
        Func<string, Action, bool> run = (name, action) => (bool)method.Invoke(null, new object[] { name, action });
        const string first = "fixture-first", second = "fixture-second";
        const string sensitive = "synthetic-secret-must-not-be-logged";
        int calls = 0;
        Action fail = () => { calls++; throw new InvalidOperationException(sensitive); };
        try
        {
            System.Console.SetOut(captured);
            Check(!run(first, fail) && calls == 1, "callback failure is contained and reported false");
            var initial = captured.ToString();
            Check(initial.Contains("name=" + first + " error=InvalidOperationException")
                && !initial.Contains(sensitive) && !initial.Contains(" at "), "failure log contains identity/type but no exception message or stack");
            var firstStamp = failures[first];
            for (int i = 0; i < 100; i++) Check(!run(first, fail), "repeated failure remains contained");
            Check(calls == 101 && captured.ToString() == initial && failures[first] == firstStamp,
                "throttling suppresses logs without skipping callbacks or moving the retry timestamp");
            Check(!run(second, fail) && captured.ToString().Length > initial.Length,
                "another service reports its failure independently");
            int before = captured.ToString().Length;
            var expired = DateTime.UtcNow.AddSeconds(-31);
            failures[first] = expired;
            Check(!run(first, fail) && captured.ToString().Length > before && failures[first] >= expired.AddSeconds(30),
                "expired failure throttle permits another report");
            before = captured.ToString().Length;
            Check(run(first, () => { calls++; }) && captured.ToString().Length == before,
                "recovered callback succeeds without another failure log");
            int laterCalls = 0;
            bool healthy = run(first, fail);
            healthy = run("fixture-later", () => { laterCalls++; }) && healthy;
            Check(!healthy && laterCalls == 1, "production aggregation order attempts later callback after a failure");
        }
        finally
        {
            System.Console.SetOut(output);
            failures.Remove(first);
            failures.Remove(second);
            captured.Dispose();
        }
        VerifyPulse();
        System.Console.WriteLine("PASS restored callback and pulse failure containment: " + checks + " assertions");
        return 0;
    }
}
