using System;
using Revora.Game;
using Revora.Network.GamePackets;
internal static class KoTimerTests {
 static int count;
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);count++;}
 static void Main(){
  foreach(ushort skill in new ushort[]{1110,1025}){
   var e=new Entity(EntityFlag.Player,false);e.SendUpdates=false;e.KOSpell=skill;
   var mode=skill==1110?FlagsUpdate.Flags.Cyclone:FlagsUpdate.Flags.Superman;
   e.AddFlag((ulong)mode,20,true);
   var flag=e.BitVector.GetFlags()[0];flag.Timer=DateTime.Now.AddSeconds(-30);
   if(skill==1110){e.CycloneStamp=Time32.Now.AddSeconds(-10);e.CycloneTime=20;}
   else {e.SupermanStamp=Time32.Now.AddSeconds(-10);e.SupermanTime=20;}
   e.KOSpellTime++;
   Check(!flag.Expire(DateTime.Now),"kill extension refreshes status deadline: "+skill);
   Check(flag.Secounds==e.KOSpellTime && flag.Secounds>0 && flag.Secounds<=20,"status and KO duration agree");
   Check(flag.RemoveOnDead,"death removal preserved");
   e.KOSpellTime=100;
   Check(e.KOSpellTime==20 && flag.Secounds==20,"extension capped at20");
   Check(flag.Expire(DateTime.Now.AddSeconds(21)),"extended effect still expires");
  }
  System.Console.WriteLine(count+" KO status timer checks passed.");
 }
}
