using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using P=Revora.Game.Features.AncientDevilSummons;
using R=Revora.Game.Features.AncientDevilRules;
using B=Revora.Game.Features.AncientDevilBattles;
using W=Revora.Game.Features.AncientDevilBossWorld;

namespace Revora.Game.Features
{
    internal static class AncientDevilSeal
    {
        private sealed class Offer {
            internal byte Option,Profession;internal DateTime Until;internal Guid Run;
            internal uint[] Items;internal uint Spare;
        }
        private static readonly ConditionalWeakTable<GameState,Offer> offers=new ConditionalWeakTable<GameState,Offer>();
        private static bool Near(GameState p) {
            return Q.Near(p,P.Switch,1052) && p.Map.BaseID==1082 && p.Map.Npcs[P.Switch].X==218 && p.Map.Npcs[P.Switch].Y==208;
        }
        private static P.Run Pending(GameState p) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                var run=P.Inspect(c,t,p);t.Commit();return run;
            }}
        }
        private static ConquerItem[] Candidates(GameState p) {
            var earned=new HashSet<uint>();
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,@"SELECT i.uid FROM items i
JOIN revora_ancient_trials r ON r.amulet_uid=i.uid AND r.amulet=i.id
WHERE i.entityid=@uid AND r.state='exchanged' AND i.position=0 AND i.warehouse=0","uid",(long)p.Entity.UID))
            using(var reader=q.ExecuteReader())while(reader.Read())earned.Add(checked((uint)Convert.ToInt64(reader[0])));}
            return p.Inventory.Objects.Where(i=>earned.Contains(i.UID) && P.Usable(i) && R.AmuletName(i.ID)!=null)
                .OrderBy(i=>i.UID).GroupBy(i=>i.ID).Select(g=>g.First()).OrderBy(i=>i.ID).ToArray();
        }
        private static void Menu(GameState p,string message=null,uint spare=0) {
            offers.Remove(p);var dialog=new Npcs(p);Offer offer=null;var run=Pending(p);
            if(p.Entity.Level<50)dialog.Text("The seal will answer you at level 50.");
            else if(run!=null && run.State=="victory") {
                dialog.Text(message??"The ancient evil is vanquished. Take what you have earned. New summons also award a bound P7 soul.");dialog.Option("Claim my reward.",3);
                offer=new Offer{Option=3,Run=run.Id};
            }else if(run!=null && run.State=="pending" && P.Clock()<run.Until) {
                dialog.Text("Your offering still holds the passage open. Return before the seal closes.");dialog.Option("Return to the battle.",2);
                offer=new Offer{Option=2,Run=run.Id};
            }else if(run!=null && run.Day==SummerHunt.Day(P.Clock()))dialog.Text("The seal has answered you today. Return tomorrow.");
            else {
                var candidates=Candidates(p);
                if(candidates.Length<9)dialog.Text(message??"Bring nine different class amulets. Earn yours from a guard, then trade with other paths.");
                else {
                    if(candidates.Length>9 && !candidates.Any(i=>i.ID==spare))spare=candidates.Last().ID;
                    var selected=candidates.Where(i=>candidates.Length==9 || i.ID!=spare).Take(9).ToArray();
                    if(message!=null)dialog.Text(message+" ");
                    dialog.Text("Offer these nine amulets: "+String.Join(", ",selected.Select(i=>R.AmuletName(i.ID)))+". They will be consumed. You have one attempt today and thirty minutes inside.");
                    dialog.Option("Offer them and enter.",1);
                    if(candidates.Length>9){dialog.Text("Keep the "+R.AmuletName(spare)+" amulet.");dialog.Option("Keep a different amulet.",4);}
                    offer=new Offer{Option=1,Items=selected.Select(i=>i.UID).ToArray(),Spare=candidates.Length>9?spare:0};
                }
            }
            if(offer!=null){offer.Profession=p.Entity.Class;offer.Until=P.Clock().AddSeconds(60);offers.Add(p,offer);}
            dialog.Option("Leave the seal alone.",255);dialog.Send();
        }
        internal static bool Handle(GameState p,NpcRequest request) {
            if(p.ItemSyncRoot==null)return true;
            lock(p.ItemSyncRoot)try {
                if(!Near(p))return true;
                if(request.OptionID==255){offers.Remove(p);p.ActiveNpc=0;return true;}
                if(request.OptionID==0){Menu(p);return true;}
                Offer offer;
                if(!offers.TryGetValue(p,out offer) || offer.Profession!=p.Entity.Class || P.Clock()>=offer.Until
                    || request.OptionID!=offer.Option && !(request.OptionID==4 && offer.Option==1 && offer.Spare!=0))return true;
                offers.Remove(p);
                if(request.OptionID==4) {
                    var candidates=Candidates(p);int index=Array.FindIndex(candidates,i=>i.ID==offer.Spare);
                    Menu(p,null,candidates.Length==0?0:candidates[(index+1)%candidates.Length].ID);return true;
                }
                var pending=Pending(p);
                if(offer.Option==3) {
                    if(pending==null || pending.Id!=offer.Run || pending.State!="victory"){Menu(p);return true;}
                    ConquerItem[] items;
                    using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                        items=B.Claim(c,t,p,offer.Run);t.Commit();
                    }}
                    if(items!=null)Q.Install(p,new uint[0],items);
                    Menu(p,items==null?"Make room in your bag. Your reward will wait.":null);return true;
                }
                if(offer.Option==2) {
                    if(pending==null || pending.Id!=offer.Run || pending.State!="pending" || P.Clock()>=pending.Until){Menu(p);return true;}
                }else {
                    if(pending!=null && (pending.State=="victory" || pending.Day==SummerHunt.Day(P.Clock()) || pending.State=="pending" && P.Clock()<pending.Until)){Menu(p);return true;}
                    var available=new HashSet<uint>(Candidates(p).Select(i=>i.UID));
                    if(offer.Items==null || !offer.Items.All(available.Contains)){Menu(p,"Your offering has changed. Choose again.");return true;}
                }
                if(W.Start(p,offer.Option==1?offer.Items:null)==null)Menu(p,"The passage is crowded. Return shortly.");else p.ActiveNpc=0;
            }catch(Exception ex){Q.Fail(p,"ancient-seal",ex);}
            return true;
        }
    }
}
