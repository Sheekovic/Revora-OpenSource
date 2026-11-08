using System;
using System.Reflection;
using System.Runtime.Serialization;
using Revora.Client;
using Revora.Network.GamePackets;
class NpcCloseChecks {
 static void Main() {
  var property=typeof(NpcRequest).GetProperty("IsDialogueClose",BindingFlags.Instance|BindingFlags.NonPublic);
  int checks=0;
  foreach(byte interaction in new byte[]{0,1,2,3,6,102})
   foreach(byte option in new byte[]{0,1,9,35,254,255}) {
    var data=new byte[28];data[14]=option;data[15]=interaction;
    var request=new NpcRequest(false);request.Deserialize(data);
    bool expected=option==255 && interaction!=6 && interaction!=102;
    if((bool)property.GetValue(request,null)!=expected)throw new Exception("Wrong close classification");
    checks++;
   }
  // No entity, map, VIP, or DB connection: closing must bypass all eligibility work.
  var player=(GameState)FormatterServices.GetUninitializedObject(typeof(GameState));
  var close=new NpcRequest(true);close.OptionID=255;
  player.ActiveNpc=900028;
  var handler=typeof(Revora.Npcs).Assembly.GetType("Revora.Game.Features.AnniversaryQuest").GetMethod("HandleNpc",BindingFlags.Static|BindingFlags.NonPublic);
  if(!(bool)handler.Invoke(null,new object[]{player,close}) || player.ActiveNpc!=0)throw new Exception("Anniversary close reentered menu");checks++;
  player.ActiveNpc=900028;Revora.Npcs.GetDialog(close,player);
  if(player.ActiveNpc!=0)throw new Exception("Shared close reentered menu");checks++;
  Console.WriteLine(checks+" dialogue close checks passed.");
 }
}
