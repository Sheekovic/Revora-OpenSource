using System;
using System.Reflection;

internal static class CheckAbyssStartup
{
    static int Main(){
        var type=typeof(Revora.Server).Assembly.GetType("Revora.Game.Features.AbyssTrialQuest",true);
        const BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic;
        var requested=type.GetMethod("StartupRequested",flags);
        var configure=type.GetMethod("ConfigureStartup",flags);
        var enabled=type.GetProperty("Enabled",flags);
        int checks=0;
        foreach(string value in new[]{null,""," ","0","false"," FALSE "}){
            if((bool)requested.Invoke(null,new object[]{value}))return 1;
            configure.Invoke(null,new object[]{value});if((bool)enabled.GetValue(null,null))return 2;checks++;
        }
        foreach(string value in new[]{"1","true"," TRUE "}){
            if(!(bool)requested.Invoke(null,new object[]{value}))return 3;checks++;
        }
        foreach(string value in new[]{"yes","enabled","2","true false","-1"}){
            bool rejected=false;
            try{configure.Invoke(null,new object[]{value});}
            catch(TargetInvocationException ex){if(!(ex.InnerException is ArgumentException))throw;rejected=true;}
            if(!rejected || (bool)enabled.GetValue(null,null))return 4;checks++;
        }
        Console.WriteLine("Abyss fail-closed startup checks passed: "+checks);return 0;
    }
}
