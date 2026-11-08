using System;
using System.Reflection;
using Revora;
using Revora.Game;
internal static class PokerPolicyTests
{
    static int Main()
    {
        var policy=typeof(Entity).Assembly.GetType("Revora.Game.PokerPolicy",true);
        if((bool)policy.GetField("Enabled",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null))throw new Exception("Poker must remain disabled.");
        // Refusal happens before parsing or touching a player, including malformed input.
        foreach(var packet in new[]{new byte[0],new byte[1],new byte[64]})new HandlePoker(packet,null);
        new HandlePoker(null,null);
        int before=Kernel.PokerTables.Count;
        string old=Environment.CurrentDirectory;
        try {
            Environment.CurrentDirectory=System.IO.Path.GetTempPath();
            Revora.Database.PoketTables.LoadTables();
            if(Kernel.PokerTables.Count!=before)throw new Exception("Disabled loader installed tables.");
        } finally {Environment.CurrentDirectory=old;}
        System.Console.WriteLine("Poker disabled handler and loader checks passed.");return 0;
    }
}
