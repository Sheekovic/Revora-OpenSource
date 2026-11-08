using System;
using System.Reflection;
using Revora.Network;
using Revora.Network.GamePackets;
class PickupFrameChecks {
 static void Main() {
  var method=typeof(FloorItem).GetMethod("ValidPickupFrame",BindingFlags.Static|BindingFlags.NonPublic);
  Func<byte[],bool> valid=b=>(bool)method.Invoke(null,new object[]{b});
  int checks=0;
  if(valid(null))throw new Exception("Null accepted");checks++;
  for(int size=0;size<=200;size++) {
   var b=new byte[size];
   if(size>=12){Array.Copy(BitConverter.GetBytes((ushort)(size-8)),0,b,0,2);Array.Copy(BitConverter.GetBytes((ushort)1101),0,b,2,2);Array.Copy(BitConverter.GetBytes(PacketHandler.ClientSeal),0,b,size-8,8);}
   bool expected=size>=32 && (size<=128 || size==137);
   if(valid(b)!=expected)throw new Exception("Length "+size);checks++;
   if(!expected)continue;
   b[0]^=1;if(valid(b))throw new Exception("Wrong declared length accepted");b[0]^=1;checks++;
   b[2]^=1;if(valid(b))throw new Exception("Wrong packet ID accepted");b[2]^=1;checks++;
   b[size-1]^=1;if(valid(b))throw new Exception("Wrong seal accepted");checks++;
  }
  Console.WriteLine(checks+" pickup frame checks passed.");
 }
}
