using System;
using System.Reflection;
using Revora;
using Revora.Client;
using Revora.Game;
using Revora.Game.ConquerStructures;
using Revora.Network.GamePackets;
internal static class WearChecks {
 static int count;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;}
 static bool Can(GameState p,ConquerItem item){return (bool)typeof(CoatStorage).GetMethod("CanPrepareWear",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{p,item});}
 static void Main(){
  var p=new GameState(null);p.Equipment=new Equipment(p);p.Inventory=new Inventory(p);p.Trade=new Revora.Game.ConquerStructures.Trade();p.ItemSyncRoot=new object();
  p.Entity=new Entity(EntityFlag.Player,false);p.Entity.Owner=p;p.Entity.SendUpdates=false;p.Entity.UID=900001;p.Entity.Hitpoints=100;p.Entity.FullyLoaded=true;p.Fake=false;p.Action=2;Kernel.GamePool[p.Entity.UID]=p;
  var garment=new ConquerItem(true){UID=900002,ID=189065,InWardrobe=true,StackSize=0};
  Check(Can(p,garment),"stored legacy garment accepted");
  garment.StackSize=1;Check(Can(p,garment),"stored new garment accepted");
  var mount=new ConquerItem(true){UID=900003,ID=200524,InWardrobe=true,StackSize=0};Check(Can(p,mount),"stored legacy mount accepted");
  var gear=(ConquerItem[])typeof(Equipment).GetField("objects",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(p.Equipment);
  gear[8]=new ConquerItem(true){UID=900004,ID=188915,Position=9,InWardrobe=true};
  typeof(Inventory).GetField("objects",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(p.Inventory,new ConquerItem[40]);
  Check(Can(p,garment),"full bag permits swapping stored cosmetics");
  gear[8].InWardrobe=false;Check(!Can(p,garment),"full bag cannot receive displaced non-wardrobe cosmetic");
  garment.InWardrobe=false;Check(Can(p,garment),"deposit frees displaced item slot");
  garment.StackSize=2;Check(!Can(p,garment),"actual stacks rejected");garment.StackSize=1;
  garment.Warehouse=8;Check(!Can(p,garment),"warehouse item rejected");garment.Warehouse=0;
  var selector=typeof(CoatStorage).GetMethod("MatchesWearSelector",BindingFlags.NonPublic|BindingFlags.Static);
  Check((bool)selector.Invoke(null,new object[]{garment,9}),"native garment Wear slot selector");
  Check((bool)selector.Invoke(null,new object[]{mount,17}),"native mount Wear slot selector");
  Check(!(bool)selector.Invoke(null,new object[]{garment,17}),"wrong slot rejected");
  Check(!(bool)selector.Invoke(null,new object[]{mount,9}),"wrong mount slot rejected");
  Check((bool)selector.Invoke(null,new object[]{garment,(int)garment.ID}),"legacy item ID selector accepted");
  Check((bool)selector.Invoke(null,new object[]{garment,0}),"unspecified selector accepted");
  Check(!(bool)selector.Invoke(null,new object[]{garment,12345}),"unrelated selector rejected");
  p.Action=0;Check(!Can(p,garment),"unready player rejected");
  System.Console.WriteLine(count+" wardrobe wear checks passed; no database connection or writes.");
 }
}
