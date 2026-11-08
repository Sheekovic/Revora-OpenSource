using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Revora.Client;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using S=Revora.Game.Features.FamilyFeudStore;
using W=Revora.Game.Features.FamilyFeudScenes;
namespace Revora.Game.Features
{
    internal static class FamilyFeudQuest
    {
        internal const uint Felix=4705,Guide=4706;
        internal static bool Enabled {get;private set;}
        private sealed class Watch {internal int Stage;}
        private static readonly ConditionalWeakTable<GameState,Watch> watches=new ConditionalWeakTable<GameState,Watch>();
        private static DateTime nextTick;
        internal static void Initialize(){
            Map map;if(!Kernel.Maps.TryGetValue(1015,out map))throw new InvalidOperationException("Family Feud map missing.");
            Revora.Interfaces.INpc npc;
            if(map.Npcs.TryGetValue(Guide,out npc)){
                if(npc.Mesh/10!=462 || !(npc.X==636 && npc.Y==656 || npc.X==669 && npc.Y==780))throw new InvalidOperationException("Family Feud Guide changed.");
                if(npc.X==636)map.RemoveNpc(npc);
            }
            if(map.Npcs.TryGetValue(Felix,out npc) && (npc.X!=686 || npc.Y!=547 || npc.Mesh/10!=461))throw new InvalidOperationException("Family Feud Felix changed.");
            S.Initialize();S.InitializeVisits();W.ValidateTerrain();W.RecoverInterrupted();
            CityGuardSystem.SpawnTalker(Felix,1015,686,547,4610,"Felix",0);CityGuardSystem.SpawnTalker(Guide,1015,669,780,4620,"GuideZhang",0);
            for(uint id=1819;id<=1827;id++)QuestInfo.Register((QuestID)id,QuestType.EquipmentBonus);Enabled=true;
        }
        private static bool Near(GameState p,uint npc){return Enabled && S.Eligible(p) && Q.Near(p,npc,npc==AzurePillQuest.Chang?(ushort)1002:(ushort)1015);}
        private static string Hint(S.Run run){switch(run.Stage){
            case 0:case 1:return "Felix waits on Bird Island, at 686,547. Hear his story.";
            case 2:return "Defeat fifty Bandits L97, then seek their leader near 362,469. Bring me his Red Token.";
            case 3:return "Bandits L98 near 362,469 carry the Indigo Token. Bring me one.";
            case 4:return "Defeat ten Banditti near 525,345, then find their courier there. I need his Black Token.";
            case 5:return "Defeat ten Bandit Deliverers near 525,345 for the Pitch Token.";
            case 6:return "Senior Bandits near 570,416 carry the Scarlet Token.";
            case 7:return "Take the Golden Token to Guide Zhang at 669,780 within ninety seconds. Return here if the disguise fades.";
            case 8:return "Tell Felix what Kerry revealed.";
            case 9:return "Chang has your reward. Leave three places in your bag.";
            default:return "The truth is out. May our families finally find peace.";
        }}
        private static void Disguise(GameState p,S.Run run){
            if(run.Stage!=7 || !run.DisguiseUntil.HasValue || run.DisguiseUntil.Value<=S.Clock() || p.Entity.MapID!=1015 || p.Entity.TransformationID!=0)return;
            QuestAppearance.ApplyBandit(p,run.DisguiseUntil.Value,()=>Enabled && S.Eligible(p));
        }
        internal static bool HandleNpc(GameState p,NpcRequest request){
            if(!Enabled || p==null || request==null)return false;uint npc=p.ActiveNpc;bool chang=npc==AzurePillQuest.Chang,inside=npc==W.Kerry || npc>=W.MarkerStart && npc<W.MarkerStart+5;
            if(!inside && npc!=Felix && npc!=Guide && (!chang || request.OptionID<160 || request.OptionID>162))return false;
            if(!S.Eligible(p) || request.NpcID!=0 && request.NpcID!=npc || !inside && !Near(p,npc) || !SessionInstall.TryEnter(p.Entity.UID))return true;
            W.Scene reserved=null;
            try{lock(p.ItemSyncRoot){
                if(inside){var scene=W.Find(p);if(scene==null || !W.Registered(scene,p) || !Q.Near(p,npc,scene.Map.ID))return true;
                    if(npc!=W.Kerry){p.Send("Jump beside the eyes: blue 50,60; green 54,64; red 58,64; yellow 60,60; black 58,60.");return true;}
                    Func<bool> context=()=>Enabled && S.AtKerry(p,scene);
                    if(request.OptionID==1)S.StartPuzzle(p,scene,context);
                    else if(request.OptionID==2){if(S.FinishPuzzle(p,scene,context)){W.Exit(scene,p);Refresh(p);return true;}}
                    else if(request.OptionID==3){W.Exit(scene,p);Refresh(p);return true;}
                    var d=new Npcs(p);d.Text("Prove your resolve. Visit blue, green, red, yellow, then black within ten seconds. Return to me when done.");d.Option("Begin the trial.",1);d.Option("I have visited all five eyes.",2);d.Option("Let me leave.",3);d.Option("Farewell.",255);d.Send();Refresh(p);return true;
                }
                Func<bool> near=()=>Near(p,npc);var run=S.Load(p);string note=null;
                if(request.OptionID==(chang?161:1)){
                    if(chang && run.Stage==0)S.Advance(p,0,near);
                    else if(npc==Felix && run.Stage>=1 && run.Stage<=6)S.Advance(p,run.Stage,near);
                    else if(npc==Felix && run.Stage==7)S.RenewDisguise(p,near);
                    else if(npc==Felix && run.Stage==8)S.Report(p,near);
                    else if(chang && run.Stage==9)S.Claim(p,near);
                    else if(npc==Guide && run.Stage==7){reserved=W.TryCreate(p);if(reserved==null)note="The chamber is occupied. Return shortly.";
                        else if(W.Enter(reserved,p,near)){reserved=null;QuestAppearance.Tick();Refresh(p);return true;}
                        else{W.Exit(reserved,p);reserved=null;note="Your disguise has faded, or your Golden Token is missing. Speak to Felix.";}}
                }else if(request.OptionID==(chang?162:2))note=S.Recover(p,near)?"Keep this token safe.":"Check your bag and storage first.";
                run=S.Load(p);Disguise(p,run);var dialog=new Npcs(p);dialog.Text(note??Hint(run));dialog.Option(npc==Guide?"Enter the chamber.":chang && run.Stage==9?"Claim my reward.":run.Stage==7?"Renew my disguise.":"About the bandits.",(byte)(chang?161:1));
                if(run.Stage>=2 && run.Stage<=7)dialog.Option("I lost my token.",(byte)(chang?162:2));dialog.Option("Farewell.",255);dialog.Send();Refresh(p);
            }}catch(Exception ex){
                try{if(reserved!=null && !reserved.Released)lock(p.ItemSyncRoot){if(Q.Player(p,false))W.Exit(reserved,p);else if(p.Entity.MapID!=reserved.Map.ID)W.Release(reserved);}}catch(Exception cleanup){Q.Fail(p,"family-entry-cleanup",cleanup);}
                Q.Fail(p,"family-npc",ex);
            }finally{SessionInstall.Exit(p.Entity.UID);}return true;
        }
        internal static bool TryUse(ConquerItem item,GameState p){if(!Enabled || item==null || item.ID<710562 || item.ID>710567)return false;
            if(!S.Eligible(p) || !SessionInstall.TryEnter(p.Entity.UID))return true;
            try{lock(p.ItemSyncRoot){ConquerItem owned;if(p.Inventory.TryGetItem(item.UID,out owned) && ReferenceEquals(owned,item))p.Send(Hint(S.Load(p)));}}
            catch(Exception ex){Q.Fail(p,"family-token-use",ex);}finally{SessionInstall.Exit(p.Entity.UID);}return true;
        }
        internal static void OnDeath(GameState p,Entity body){
            if(!Enabled || !S.Eligible(p) || body==null || body.MapID!=1015 || body.MonsterInfo==null || !new uint[]{55,79,84}.Contains(body.MonsterInfo.ID) || !SessionInstall.TryEnter(p.Entity.UID))return;
            try{lock(p.ItemSyncRoot){if(S.Hunt(p,body,()=>Enabled && S.Eligible(p)))Refresh(p);}}
            catch(Exception ex){Q.Fail(p,"family-hunt",ex);}finally{SessionInstall.Exit(p.Entity.UID);}
        }
        internal static void Arrived(GameState p,Map map,ushort x,ushort y,TravelProgress.Arrival arrival){
            if(!Enabled || !S.Eligible(p) || arrival!=TravelProgress.Arrival.Walk && arrival!=TravelProgress.Arrival.Jump || map==null || !W.Contains(map.ID)
                || !ReferenceEquals(p.Map,map) || p.Entity.X!=x || p.Entity.Y!=y || !SessionInstall.TryEnter(p.Entity.UID))return;
            try{lock(p.ItemSyncRoot){var scene=W.Find(p);if(scene!=null && S.ObservePuzzle(p,scene,()=>Enabled && ReferenceEquals(p.Map,map) && p.Entity.X==x && p.Entity.Y==y))p.Send("The five eyes are quiet. Return to Kerry.");}}
            catch(Exception ex){Q.Fail(p,"family-puzzle",ex);}finally{SessionInstall.Exit(p.Entity.UID);}
        }
        internal static void Tick(){if(!Enabled)return;W.Tick();FamilyFeudEncounters.Tick();QuestAppearance.Tick();var now=S.Clock();if(now<nextTick)return;nextTick=now.AddSeconds(3);
            foreach(var p in Kernel.GamePool.Values){Watch watch;if(!S.Eligible(p) || p.Entity.MapID!=1015 || !watches.TryGetValue(p,out watch) || !(watch.Stage==2 || watch.Stage>=4 && watch.Stage<=6) || FamilyFeudEncounters.HasOwner(p.Entity.UID))continue;
                ushort x=(ushort)(watch.Stage==2?362:watch.Stage==6?570:525),y=(ushort)(watch.Stage==2?469:watch.Stage==6?416:345);
                if(Kernel.GetDistance(p.Entity.X,p.Entity.Y,x,y)>8)continue;try{FamilyFeudEncounters.Start(p);}catch(Exception ex){Q.Fail(p,"family-spawn",ex);}
            }
        }
        internal static bool Protect(uint id){return Enabled && id>=1819 && id<=1827;}
        internal static void OnLogin(GameState p){if(!Enabled || !Q.Player(p,false) || !SessionInstall.TryEnter(p.Entity.UID))return;
            try{W.RecoverLogin(p);W.Tick();W.RecoverLogin(p);FamilyFeudEncounters.Tick();QuestAppearance.Tick();lock(p.ItemSyncRoot){Disguise(p,S.Load(p));Refresh(p);}}
            catch(Exception ex){Q.Fail(p,"family-login",ex);}finally{SessionInstall.Exit(p.Entity.UID);}}
        internal static void Refresh(GameState p){if(Enabled && Q.Player(p,false) && p.Quests!=null)p.Quests.SendFullGUI();}
        internal static void Fill(GameState p,List<MsgTaskStatus.QuestData> list,HashSet<uint> seen){
            if(!Enabled)return;for(uint id=1819;id<=1827;id++)seen.Add(id);if(!Q.Player(p,false) || p.Entity.Level<95 || p.Entity.Reborn>2)return;
            lock(p.ItemSyncRoot)try{var run=S.Load(p);watches.GetValue(p,key=>new Watch()).Stage=run.Stage;
                for(uint id=1819;id<=1827;id++){int step=(int)id-1818;var status=run.Stage>step?MsgTaskStatus.QuestData.QuestStatus.Finished:run.Stage==step?MsgTaskStatus.QuestData.QuestStatus.Accepted:MsgTaskStatus.QuestData.QuestStatus.Available;list.Add(MsgTaskStatus.QuestData.Create((QuestID)id,status,0));}
            }catch(Exception ex){Q.Fail(p,"family-journal",ex);}
        }
    }
}
