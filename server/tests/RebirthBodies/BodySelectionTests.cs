using System;
using System.Reflection;
internal static class BodySelectionTests {
    private static int Main(string[] args) {
        var rules=Assembly.LoadFrom(args[0]).GetType("Revora.Game.Features.RebirthRules",true);
        var select=rules.GetMethod("SelectedBody",BindingFlags.Static|BindingFlags.NonPublic);
        int checks=0;
        foreach(ushort body in new ushort[]{1003,1004,1006,2001,2002,2005,0,1005,2006,3000,65535})
        foreach(bool sturdy in new[]{false,true}) {
            ushort expected=body==1003 || body==1004 || body==1006 ? (ushort)(sturdy?1004:1003)
                :body==2001 || body==2002 || body==2005 ? (ushort)(sturdy?2002:2001):(ushort)0;
            ushort actual=(ushort)select.Invoke(null,new object[]{body,sturdy});
            if(actual!=expected)throw new Exception("Unexpected rebirth body for "+body+" sturdy="+sturdy);
            checks++;
        }
        Console.WriteLine("Rebirth body compatibility checks passed: "+checks);return 0;
    }
}
