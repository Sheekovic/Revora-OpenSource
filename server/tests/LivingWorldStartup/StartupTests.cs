using System;
using System.Reflection;
using System.Threading;
using Revora.Game.Features;

internal static class StartupTests
{
    static int Main()
    {
        // Empty, isolated world: this test must never connect to a database or spawn actors.
        if(Revora.Kernel.Maps.Count!=0 || !string.IsNullOrEmpty(Revora.Database.DataHolder.ConnectionString))
            throw new InvalidOperationException("Startup fixture requires a fresh empty process.");
        var flags=BindingFlags.Static|BindingFlags.NonPublic;
        var gate=typeof(LivingWorldSystem).GetField("TickGate",flags).GetValue(null);
        var ready=typeof(LivingWorldSystem).GetField("Ready",flags);
        Exception failure=null;
        using(var started=new ManualResetEvent(false))
        using(var finished=new ManualResetEvent(false))
        {
            var worker=new Thread(()=>{
                started.Set();
                try{LivingWorldSystem.Create();}catch(Exception error){failure=error;}
                finally{finished.Set();}
            }){IsBackground=true};
            bool completedWhileTickOwned=false,readyWhileTickOwned=false;
            Monitor.Enter(gate);
            try{
                worker.Start();
                if(!started.WaitOne(5000))throw new TimeoutException("Initializer did not start.");
                completedWhileTickOwned=finished.WaitOne(500);
                readyWhileTickOwned=(bool)ready.GetValue(null);
            }finally{Monitor.Exit(gate);}
            if(!worker.Join(5000))throw new TimeoutException("Initializer did not finish.");
            if(failure!=null)throw new InvalidOperationException("Initializer failed.",failure);
            if(completedWhileTickOwned || readyWhileTickOwned){
                Console.WriteLine("FAIL: initialization overlapped the timer owner or published readiness early.");return 1;
            }
            if(!(bool)ready.GetValue(null))throw new InvalidOperationException("Successful initialization did not publish readiness.");
            LivingWorldSystem.Tick();
            LivingWorldSystem.Create();
            Console.WriteLine("PASS: initialization waits for the timer owner and publishes readiness only after completion.");return 0;
        }
    }
}
