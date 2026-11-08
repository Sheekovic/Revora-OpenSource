using System;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using S=Revora.Game.Features.PowerBookStore;
using W=Revora.Game.Features.PowerBookScenes;
namespace Revora.Game.Features
{
    internal static class PowerBookQuest
    {
        internal const uint Beggar=900071841;
        internal static bool Enabled {get;private set;}
        internal static void Initialize()
        {
            S.Initialize();W.InitializeTravel();
            for(int i=0;i<4;i++)W.ValidateTerrain(i);
            for(int i=0;i<7;i++)W.Profile(i);
            W.RecoverInterrupted();
            CityGuardSystem.SpawnTalker(Beggar,1002,466,331,4570,"OldBeggar",0);
            PowerBookJournal.Initialize();Enabled=true;
        }
        private static bool Near(GameState p,uint npc){return Enabled && S.Eligible(p) && Q.Near(p,npc,1002);}
        internal static void OnLogin(GameState p){if(Enabled)try{W.RecoverLogin(p);}catch(Exception ex){Q.Fail(p,"power-book-login",ex);}}
        internal static void Tick()
        {
            if(!Enabled)return;W.Tick();
            foreach(var scene in W.Active()){
                var p=scene.Owner;if(!SessionInstall.TryEnter(p.Entity.UID))continue;
                try{lock(p.ItemSyncRoot)PowerBookCombat.Populate(scene);}
                catch(Exception ex){Q.Fail(p,"power-book-population",ex);}
                finally{SessionInstall.Exit(p.Entity.UID);}
            }
        }
        internal static string Hint(S.Run run)
        {
            switch(run.Stage){
                case 0:return "The Old Beggar guards an ancient trial. Take my badge to him at 466,331.";
                case 1:return "Bring your badge and one Meteor to the Old Beggar at 466,331.";
                case 2:return "Defeat 20 Slingers. Read the book for your next task.";
                case 3:return "Lure 10 Gold Ghosts within three paces of 77,23, 102,136 or 146,104, then defeat them.";
                case 4:return "Defeat 15 Slingers and 15 Gold Ghosts. Read the book at 140,140 to continue.";
                case 5:return "Defeat two Bladelings within ten seconds.";
                case 6:return "Hunt Agile Rats for a fang, then use it. A broken fang means another hunt.";
                case 7:return "Defeat 10 Bladelings and 10 Agile Rats. Read the book at 140,140 to continue.";
                case 8:return "Defeat two Blue Birds within twenty seconds.";
                case 9:return "Hunt Fiend Bats for a wing. Use it at 100,66 to roast it, then eat it. Burnt wings must be replaced.";
                case 10:return "Defeat five Blue Birds and five Fiend Bats. Keep a Blue Bird plume for the last trial.";
                case 11:return run.MaterialUid==0?"Hunt Blue Birds for a plume, then read the book at 140,140.":"Defeat 10 Minotaurs. Use the plume within three paces and three seconds of each death.";
                case 12:return "The trials are over. Read the book at 77,27 to leave, then speak to the Old Beggar.";
                case 13:return "Return to Chang for your armor pack and bound P7 soul.";
                default:return "You have mastered the Power Book in this life.";
            }
        }
        internal static bool HandleNpc(GameState p,NpcRequest request)
        {
            if(p==null || request==null)return false;
            bool chang=p.ActiveNpc==AzurePillQuest.Chang;
            if(p.ActiveNpc!=Beggar && (!chang || request.OptionID<40 || request.OptionID>43))return false;
            uint npc=p.ActiveNpc;
            if(!Near(p,npc) || request.NpcID!=0 && request.NpcID!=npc || !SessionInstall.TryEnter(p.Entity.UID))return true;
            try{lock(p.ItemSyncRoot){
                if(!Near(p,npc))return true;
                Func<bool> context=()=>Near(p,npc);string recovery=null;
                if(chang && request.OptionID==41)S.Begin(p,context);
                else if(chang && request.OptionID==43)S.Claim(p,context);
                else if(!chang && request.OptionID==1){
                    var meteor=p.Inventory.Objects.FirstOrDefault(i=>i.ID==S.Meteor && i.Position==0 && i.Warehouse==0 && i.Lock==0 && !i.Suspicious && !i.InWardrobe && i.Days==0 && i.TimeLeftInMinutes==0);
                    if(meteor!=null)S.BuyBook(p,meteor.UID,context);
                }
                else if(!chang && request.OptionID==2){
                    var run=S.Load(p);int floor=S.FloorFor(run.Stage);if(run.Stage==11 && run.MaterialUid==0)floor=2;
                    if(floor>=0 && W.Find(p)==null){Travel(p,floor,context);p.Send(Hint(run)+" Read the book at 77,27 to leave. Each visit lasts thirty minutes.");return true;}
                }
                else if(!chang && request.OptionID==3)S.HandIn(p,context);
                else if(chang && request.OptionID==42 || !chang && request.OptionID==4)recovery=S.Recover(p,context);
                var saved=S.Load(p);var d=new Npcs(p);d.Text(recovery??Hint(saved));
                if(chang){if(saved.Stage==0)d.Option("I will face the trial.",41);if(saved.Stage==13)d.Option("Claim my reward.",43);}
                else{if(saved.Stage==1)d.Option("Buy the book: one Meteor.",1);if(S.FloorFor(saved.Stage)>=0)d.Option("Enter the trial.",2);if(saved.Stage==12)d.Option("My trial is complete.",3);}
                if(saved.Stage>=1 && saved.Stage<14)d.Option("I lost my trial belongings.",(byte)(chang?42:4));
                d.Option("Farewell.",255);d.Send();PowerBookJournal.Refresh(p);
            }}catch(Exception ex){Q.Fail(p,"power-book-npc",ex);}finally{SessionInstall.Exit(p.Entity.UID);}
            return true;
        }
        private static void Travel(GameState p,int floor,Func<bool> context)
        {
            var scene=W.Create(p,floor);
            // Failed entry preserves the visit receipt for login/startup recovery.
            W.Enter(scene,p,context);PowerBookCombat.Populate(scene);
        }
        internal static bool TryUse(ConquerItem item,GameState p)
        {
            if(item==null || item.ID!=S.Book && item.ID!=721926 && item.ID!=721928 && item.ID!=721929 && item.ID!=721931)return false;
            if(!Enabled || !S.Eligible(p) || !SessionInstall.TryEnter(p.Entity.UID))return true;
            try{lock(p.ItemSyncRoot){
                ConquerItem owned;
                if(!S.Eligible(p) || !p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(item,owned) || !item.Bound || item.StackSize!=1
                    || item.Position!=0 || item.Warehouse!=0 || item.Lock!=0 || item.Suspicious || item.InWardrobe || item.Days!=0 || item.TimeLeftInMinutes!=0)return true;
                var run=S.Load(p);var scene=W.Find(p);
                bool inside=scene!=null && scene.Entered && W.Registered(scene,p) && ReferenceEquals(p.Map,scene.Map) && S.Clock()<scene.Until;
                if(item.ID==S.Book){
                    if(run.BookUid!=item.UID)return true;
                    if(inside && Kernel.GetDistance(p.Entity.X,p.Entity.Y,W.ExitX,W.ExitY)<=2){W.Exit(scene,p);return true;}
                    if(inside && Kernel.GetDistance(p.Entity.X,p.Entity.Y,W.GateX,W.GateY)<=3){
                        int floor=S.FloorFor(run.Stage);if(run.Stage==11 && run.MaterialUid==0)floor=2;
                        if(floor>=0 && floor!=scene.Floor){W.Exit(scene,p);Travel(p,floor,()=>S.Eligible(p) && p.Entity.MapID==1002 && p.Entity.X==466 && p.Entity.Y==333);return true;}
                    }
                }
                else if(inside && run.MaterialUid==item.UID){
                    if(item.ID==721931){
                        foreach(var corpse in scene.Map.Entities.Values.Where(b=>b.Dead && b.MonsterInfo!=null && b.MonsterInfo.ID==W.FamilyStart+6 && Kernel.GetDistance(p.Entity.X,p.Entity.Y,b.X,b.Y)<=3).OrderBy(b=>Kernel.GetDistance(p.Entity.X,p.Entity.Y,b.X,b.Y)).ThenBy(b=>b.UID).ToArray())
                            if(S.Burn(p,corpse,item.UID))break;
                    }else S.UseMaterial(p,scene,item.UID);
                }
                p.Send(Hint(S.Load(p)));PowerBookJournal.Refresh(p);
            }}catch(Exception ex){Q.Fail(p,"power-book-item",ex);}finally{SessionInstall.Exit(p.Entity.UID);}
            return true;
        }
    }
}
