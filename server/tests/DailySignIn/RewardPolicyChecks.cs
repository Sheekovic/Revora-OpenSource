using System;
using System.Linq;
using System.Reflection;
using Revora.Network.GamePackets;
class RewardPolicyChecks {
 static void Main(){
  var type=typeof(ConquerItem).Assembly.GetType("Revora.Game.Features.DailySignInPacks",true);
  var cp=type.GetMethod("Cps",BindingFlags.Static|BindingFlags.NonPublic);
  var items=type.GetMethod("Items",BindingFlags.Static|BindingFlags.NonPublic);
  uint[] ids={3100011,3100013,3100014,3100015,3100016,3100017};
  uint[] expected={10000,20000,0,0,500000,0};
  int checks=0;
  for(int i=0;i<ids.Length;i++){
   if((uint)cp.Invoke(null,new object[]{ids[i]})!=expected[i])throw new Exception("Incorrect CP payout: "+ids[i]);checks++;
   var rewards=(uint[])items.Invoke(null,new object[]{ids[i]});
   if(rewards.Any(id=>id==3004247 || id==3004248 || id==3004249))throw new Exception("Daily P7 reward: "+ids[i]);checks++;
  }
  Console.WriteLine(checks+" compiled reward-policy checks passed; no database writes.");
 }
}
