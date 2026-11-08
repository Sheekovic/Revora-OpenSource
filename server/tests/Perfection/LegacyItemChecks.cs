using System;
using System.Reflection;
using Revora.Network.GamePackets;

internal static class LegacyItemChecks
{
    private static readonly Type Tempering = typeof(ConquerItem).Assembly.GetType("Revora.Game.Features.PerfectionTempering", true);
    private static int checks;
    private static object Call(string name, params object[] args) {
        return Tempering.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,args);
    }
    private static void Check(bool value) { if(!value)throw new Exception("Perfection regression check failed: "+checks);checks++; }
    private static void Main() {
        var target=new ConquerItem(true){ID=626439,Position=4,StackSize=0,Lock=1,Bound=true};
        var stone=new ConquerItem(true){ID=3009002,Position=0,StackSize=1};
        Check((bool)Call("Usable",target));
        Check((bool)Call("MaterialUsable",stone,target));
        Check((uint)Call("Value",stone,target)==1000);
        target.StackSize=1;Check((bool)Call("Usable",target));
        target.StackSize=2;Check(!(bool)Call("Usable",target));target.StackSize=0;
        target.Warehouse=1;Check(!(bool)Call("Usable",target));target.Warehouse=0;
        target.Suspicious=true;Check(!(bool)Call("Usable",target));target.Suspicious=false;
        stone.Lock=1;Check(!(bool)Call("MaterialUsable",stone,target));stone.Lock=0;
        stone.Bound=true;target.Bound=false;Check(!(bool)Call("MaterialUsable",stone,target));target.Bound=true;
        stone.StackSize=2;Check(!(bool)Call("MaterialUsable",stone,target));stone.StackSize=0;
        Check((bool)Call("MaterialUsable",stone,target));
        stone.Position=4;Check(!(bool)Call("MaterialUsable",stone,target));
        Console.WriteLine(checks+" Perfection legacy-item and material-protection checks passed; no database writes.");
    }
}
