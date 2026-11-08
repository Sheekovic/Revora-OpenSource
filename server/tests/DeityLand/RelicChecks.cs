using System;
using System.Linq;
using System.Reflection;
using Revora.Network;
using Revora.Network.GamePackets;
class RelicChecks {
 static void Main(){
  var create=typeof(Relics).GetMethod("CreateHuntRelic",BindingFlags.Static|BindingFlags.NonPublic);
  int checks=0;
  foreach(uint id in new uint[]{4100001,4100002,4100003,4100004,4100005}){
   foreach(bool high in new[]{false,true}){
    Func<int,int> roll=n=>high?n-1:0;
    var item=(ConquerItem)create.Invoke(null,new object[]{id,roll});
    uint[] attrs={item.RelicAttrib1,item.RelicAttrib2,item.RelicAttrib3};
    if(item.ID!=id || item.Bound || item.StackSize!=1 || item.Durability!=10000)throw new Exception("Relic item mismatch");checks++;
    if(attrs.Select(a=>a%100).Distinct().Count()!=3 || attrs.Any(a=>a/1000==0 || a/1000>600) || item.RelicAttrib4!=0 || item.RelicAttrib5!=0)throw new Exception("Invalid attributes");checks++;
    if(attrs[0]%100!= (uint)Relics.RelicsItemsInfo[(Relics.RelicsID)id][0].type || attrs[0]/100%10!=2)throw new Exception("Featured attribute missing");checks++;
   }
  }
  try{create.Invoke(null,new object[]{4100001u,(Func<int,int>)(n=>n)});throw new Exception("Invalid roll accepted");}catch(TargetInvocationException e){if(!(e.InnerException is InvalidOperationException))throw;}checks++;
  Console.WriteLine(checks+" relic generation checks passed.");
 }
}
