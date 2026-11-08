using System;
using System.Collections.Generic;
using Revora.Client;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using S=Revora.Game.Features.GhostCityStore;
using W=Revora.Game.Features.GhostCityScenes;
namespace Revora.Game.Features
{
    internal static class GhostCityQuest
    {
        internal const uint Aunt=4503,Wang=4504,Shine=4505;
        internal static bool Enabled {get;private set;}
        internal static void Initialize(){
            CheckNpc(Aunt,454,540,562);CheckNpc(Wang,468,309,563);CheckNpc(Shine,440,334,564);
            S.Initialize();W.InitializeTravel();W.ValidateTerrain();W.RecoverInterrupted();
            CityGuardSystem.SpawnTalker(Aunt,1002,454,540,5620,"AuntZhang",0);
            CityGuardSystem.SpawnTalker(Wang,1002,468,309,5630,"JerkWang",0);
            CityGuardSystem.SpawnTalker(Shine,1002,440,334,5640,"TaoistShine",0);
            for(uint id=1787;id<=1794;id++)QuestInfo.Register((QuestID)id,QuestType.EquipmentBonus);Enabled=true;
        }
        private static void CheckNpc(uint id,ushort x,ushort y,ushort model){Map map;Revora.Interfaces.INpc npc;
            if(!Kernel.Maps.TryGetValue(1002,out map))throw new InvalidOperationException("Ghost City NPC map unavailable.");
            if(map.Npcs.TryGetValue(id,out npc) && (npc.X!=x || npc.Y!=y || npc.Mesh/10!=model))throw new InvalidOperationException("Ghost City NPC placement changed.");
        }
        private static bool Near(GameState p,uint npc){return Enabled && S.Eligible(p) && Q.Near(p,npc,1002);}
        internal static void Tick(){if(Enabled)W.Tick();}
        internal static void OnLogin(GameState p){
            if(!Enabled || !Q.Player(p,false) || !SessionInstall.TryEnter(p.Entity.UID))return;
            try{W.RecoverLogin(p);W.Tick();W.RecoverLogin(p);Refresh(p);}
            catch(Exception ex){Q.Fail(p,"ghost-login",ex);}finally{SessionInstall.Exit(p.Entity.UID);}
        }
        private static string Hint(S.Run run){switch(run.Stage){
            case 0:return "Aunt Zhang's child is missing. Will you look for him?";
            case 1:return "Aunt Zhang waits at 454,540 in Twin City.";
            case 2:return "Ask Jerk Wang at 468,309 about the missing child.";
            case 3:return "Light Wang's firework at 440,308, then 443,344, then 465,356. Return to Wang.";
            case 4:return "Taoist Shine at 440,334 knows these strange signs.";
            case 5:return "Use your Charm at a firework spot. Speak with MoMo, then inspect the pillar.";
            case 6:return "Bring the pillar's Powder to Taoist Shine.";
            case 7:return "Use your Charm at a firework spot. Slay 100 KidCatchers or KidRippers, then KidHunter and KidSiezer.";
            case 8:return "Speak with the lost children if you wish, then feed MoMo the Bezoar.";
            case 9:return "Tell Aunt Zhang her child is safe.";
            case 10:return "Chang has your reward. Leave three places in your bag.";
            default:return "The child is safe. Your kindness will not be forgotten.";
        }}
        internal static bool HandleNpc(GameState p,NpcRequest request){
            if(!Enabled || p==null || request==null)return false;uint npc=p.ActiveNpc;bool chang=npc==AzurePillQuest.Chang;
            bool child=npc>=9917930 && npc<9917935,inside=child || npc==W.MoMo || npc==W.Pillar;
            if(!inside && npc!=Aunt && npc!=Wang && npc!=Shine && (!chang || request.OptionID<120 || request.OptionID>122))return false;
            if(!S.Eligible(p) || request.NpcID!=0 && request.NpcID!=npc || !inside && !Near(p,npc) || !SessionInstall.TryEnter(p.Entity.UID))return true;
            try{lock(p.ItemSyncRoot){
                if(inside){var scene=W.Find(p);Revora.Interfaces.INpc actor;
                    if(scene==null || !ReferenceEquals(p.Map,scene.Map) || !scene.Map.Npcs.TryGetValue(npc,out actor) || Kernel.GetDistance(p.Entity.X,p.Entity.Y,actor.X,actor.Y)>3)return true;
                    bool changed=false;if(request.OptionID==1){
                        Func<bool> context=()=>Enabled && S.Eligible(p);
                        if(child)changed=S.RescueChild(p,scene,(int)(npc-9917930),context);
                        else if(npc==W.Pillar)changed=S.CollectPowder(p,scene,context);
                        else if(scene.Dark)changed=S.FeedMoMo(p,scene,context);else changed=S.MeetMoMo(p,scene,context);
                        if(scene.Completed){W.Exit(scene,p);Refresh(p);return true;}
                    }
                    var d=new Npcs(p);d.Text(changed?child?"Thank you! I can find my way home now.":"The pillar... something is wrong with it.":child?"Please, help me get home.":npc==W.Pillar?"A pale powder clings to the stone.":scene.Dark?"I feel so ill. Have you brought something to help?":"I cannot leave this place. Look at that strange pillar.");
                    d.Option(child?"Let me help you.":npc==W.Pillar?"Gather the powder.":scene.Dark?"Take this Bezoar.":"Tell me what happened.",1);d.Option("Farewell.",255);d.Send();Refresh(p);return true;
                }
                if(!Near(p,npc))return true;Func<bool> near=()=>Near(p,npc);var run=S.Load(p);string note=null;
                if(request.OptionID==(chang?121:1)){
                    if(chang && run.Stage==0)S.Advance(p,0,near);
                    else if(npc==Aunt && run.Stage==1)S.Advance(p,1,near);
                    else if(npc==Wang && (run.Stage==2 || run.Stage==3))S.Advance(p,run.Stage,near);
                    else if(npc==Shine && run.Stage==4)S.Advance(p,4,near);
                    else if(npc==Shine && run.Stage==6)S.InspectPowder(p,near);
                    else if(npc==Aunt && run.Stage==9)S.ReportRescue(p,near);
                    else if(chang && run.Stage==10)S.Claim(p,near);
                }else if(request.OptionID==(chang?122:2)){
                    W.RecoverLogin(p);W.Tick();note=S.Recover(p,near)?"Here. Keep your belongings close.":"Check your bag and storage. I cannot replace what you still own.";
                }
                run=S.Load(p);var dialog=new Npcs(p);dialog.Text(note??Hint(run));dialog.Option(chang && run.Stage==10?"Claim my reward.":"About the missing child.",(byte)(chang?121:1));
                if(run.Stage>=3 && run.Stage<=8)dialog.Option("I lost my quest belongings.",(byte)(chang?122:2));dialog.Option("Farewell.",255);dialog.Send();Refresh(p);
            }}catch(Exception ex){Q.Fail(p,"ghost-npc",ex);}finally{SessionInstall.Exit(p.Entity.UID);}return true;
        }
        internal static bool TryUse(ConquerItem item,GameState p){
            if(!Enabled || item==null || item.ID!=721717 && item.ID!=721713 && item.ID!=721712 && item.ID!=721714)return false;
            if(!S.Eligible(p) || !SessionInstall.TryEnter(p.Entity.UID))return true;W.Scene reserved=null;
            try{lock(p.ItemSyncRoot){ConquerItem owned;if(!p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(owned,item))return true;
                Func<bool> context=()=>Enabled && S.Eligible(p);
                if(item.ID==721717){if(!S.Firework(p,item.UID,context))p.Send(Hint(S.Load(p)));else p.Send("The firework burns brightly. Wang will want to hear about this.");}
                else if(item.ID==721713){var run=S.Load(p);
                    if(run.Charm!=item.UID || run.Stage!=5 && run.Stage!=7 && run.Stage!=8){p.Send(Hint(run));return true;}
                    reserved=W.TryCreate(p);if(reserved==null){p.Send("The path is occupied. Finish your visit or try again shortly.");return true;}
                    if(!W.Enter(reserved,p,context)){W.Exit(reserved,p);reserved=null;p.Send("Use your Charm at one of Wang's firework spots. Bring your quest belongings.");return true;}
                    if(reserved.Dark)S.CollectBezoar(p,reserved,context);reserved=null;p.Send(Hint(S.Load(p)));
                }else p.Send(Hint(S.Load(p)));Refresh(p);
            }}catch(Exception ex){
                try{if(reserved!=null && !reserved.Released){lock(p.ItemSyncRoot){if(Q.Player(p,false))W.Exit(reserved,p);else if(p.Entity.MapID!=reserved.Map.ID)W.Release(reserved);}}}catch(Exception cleanup){Q.Fail(p,"ghost-admission-cleanup",cleanup);}
                Q.Fail(p,"ghost-item",ex);
            }finally{SessionInstall.Exit(p.Entity.UID);}return true;
        }
        // Called only after an owned final boss kill has committed, with owner lock held.
        internal static void OnCombat(GameState p,W.Scene scene){if(!Enabled)return;
            if(S.CollectBezoar(p,scene,()=>Enabled && S.Eligible(p)))p.Send("You found a Bezoar. Help the lost children, then bring it to MoMo.");Refresh(p);
        }
        internal static bool Protect(uint id){return Enabled && id>=1787 && id<=1794;}
        internal static void Refresh(GameState p){if(Enabled && Q.Player(p,false) && p.Quests!=null)p.Quests.SendFullGUI();}
        internal static void Fill(GameState p,List<MsgTaskStatus.QuestData> list,HashSet<uint> seen){
            if(!Enabled)return;for(uint id=1787;id<=1794;id++)seen.Add(id);if(!Q.Player(p,false) || p.Entity.Level<75 || p.Entity.Reborn>2)return;
            lock(p.ItemSyncRoot)try{var run=S.Load(p);int current=run.Stage<=4?run.Stage:run.Stage<=6?5:run.Stage<=8?6:run.Stage-2;
                for(uint id=1787;id<=1794;id++){int step=(int)id-1786;var status=current>step?MsgTaskStatus.QuestData.QuestStatus.Finished:current==step?MsgTaskStatus.QuestData.QuestStatus.Accepted:MsgTaskStatus.QuestData.QuestStatus.Available;
                    list.Add(MsgTaskStatus.QuestData.Create((QuestID)id,status,0));}
            }catch(Exception ex){Q.Fail(p,"ghost-journal",ex);}
        }
    }
}
