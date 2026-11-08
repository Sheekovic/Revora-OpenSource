using System;
using Revora.Game.Features;
class RulesTests {
 static int checks;
 static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
 static int Main(){try{
  var start=new DateTime(2033,1,2,15,0,0,DateTimeKind.Utc);var end=start.AddHours(1);
  for(uint rune=729960;rune<729970;rune++){
   FlameLitRules.Step step;Check(FlameLitRules.TryStep(rune,out step),"known rune");
   Check(step.Next==rune+1 && step.Npc==4453+rune-729960,"ordered transition");
   foreach(var now in new[]{start.AddTicks(-1),start,start.AddMinutes(29).AddSeconds(59),start.AddMinutes(30),end.AddTicks(-1),end}){
    bool expected=now>=start && now<end && (rune!=729969 || now>=start.AddMinutes(30));
    Check(FlameLitRules.CanLight(rune,step.Map,step.X,step.Y,now,start,end,true)==expected,"one-hour war boundary");
   }
   Check(!FlameLitRules.CanLight(rune,step.Map,step.X,step.Y,end.AddTicks(-1),start,end,false),"calendar alone cannot authorize lighting");
   Check(!FlameLitRules.CanLight(rune,5001,step.X,step.Y,end.AddTicks(-1),start,end,true),"parked map rejected");
   Check(FlameLitRules.CanLight(rune,step.Map,step.X+6,step.Y,end.AddTicks(-1),start,end,true),"range edge");
   Check(!FlameLitRules.CanLight(rune,step.Map,step.X+7,step.Y,end.AddTicks(-1),start,end,true),"beyond range");
   Check(!FlameLitRules.CanLight(rune,step.Map,int.MinValue,int.MaxValue,end.AddTicks(-1),start,end,true),"coordinate overflow rejected");
  }
  FlameLitRules.Step ignored;foreach(uint rune in new uint[]{0,729959,729970,uint.MaxValue})Check(!FlameLitRules.TryStep(rune,out ignored),"not an advancing rune");
  var longEnd=start.AddHours(30);
  Check(!FlameLitRules.CanLight(729960,1002,350,327,longEnd.AddHours(-2).AddTicks(-1),start,longEnd,true),"long war excludes early hours");
  Check(FlameLitRules.CanLight(729960,1002,350,327,longEnd.AddHours(-2),start,longEnd,true),"long war final two hours");
  Check(!FlameLitRules.CanLight(729969,1038,62,59,longEnd.AddMinutes(-30).AddTicks(-1),start,longEnd,true),"last flame exact half-hour gate");
  Check(FlameLitRules.CanLight(729969,1038,62,59,longEnd.AddMinutes(-30),start,longEnd,true),"last flame opens");
  Check(!FlameLitRules.CanLight(729960,1002,350,327,start,start,start,true),"empty war interval");
  Check(!FlameLitRules.CanLight(729960,1002,350,327,DateTime.SpecifyKind(start,DateTimeKind.Unspecified),start,end,true),"ambiguous clock rejected");
  for(int i=0;i<=10;i++)Check(FlameLitRules.RewardUnits(i)==(i==10?12:i),"existing reward units");
  foreach(int i in new[]{-1,11}){bool rejected=false;try{FlameLitRules.RewardUnits(i);}catch(ArgumentOutOfRangeException){rejected=true;}Check(rejected,"invalid progress reward");}
  Console.WriteLine("Flame Lit rule checks passed: "+checks);return 0;
 }catch(Exception e){Console.Error.WriteLine(e.Message);return 1;}}
}
