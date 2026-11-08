using System;
using System.Collections.Generic;
using System.Linq;
using Revora.Client;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using S=Revora.Game.Features.MadManStore;
using W=Revora.Game.Features.MadManScenes;
namespace Revora.Game.Features
{
    internal static class MadManQuest
    {
        internal const uint DuSan=4468,Judd=1611,Dong=4470;
        internal static bool Enabled {get;private set;}
        internal static void Initialize(){
            CheckNpc(1020,DuSan,552,601,552);CheckNpc(1002,Judd,345,251,699);CheckNpc(1002,Dong,431,331,554);
            S.Initialize();W.InitializeTravel();W.ValidateTerrain();W.RecoverInterrupted();
            CityGuardSystem.SpawnTalker(DuSan,1020,552,601,5520,"DuSan",0);CityGuardSystem.SpawnTalker(Judd,1002,345,251,6990,"GeneralJudd",0);CityGuardSystem.SpawnTalker(Dong,1002,431,331,5540,"PharmacistDong",0);
            for(uint id=1781;id<=1786;id++)QuestInfo.Register((QuestID)id,QuestType.EquipmentBonus);Enabled=true;
        }
        private static void CheckNpc(ushort mapId,uint id,ushort x,ushort y,ushort model){Map map;Revora.Interfaces.INpc npc;
            if(!Kernel.Maps.TryGetValue(mapId,out map))throw new InvalidOperationException("Mad Man NPC map unavailable.");
            if(map.Npcs.TryGetValue(id,out npc) && (npc.X!=x || npc.Y!=y || npc.Mesh/10!=model))throw new InvalidOperationException("Mad Man NPC placement changed.");
        }
        private static bool Near(GameState p,uint npc){return Enabled && S.Eligible(p) && Q.Near(p,npc,npc==DuSan?(ushort)1020:(ushort)1002);}
        private static bool BesideDong(GameState p){Revora.Interfaces.INpc npc;return Enabled && S.Eligible(p) && p.Entity.MapID==1002 && p.Entity.OnDeath==null && !p.InQualifier() && !p.InTeamQualifier()
            && p.Map.Npcs.TryGetValue(Dong,out npc) && npc.X==431 && npc.Y==331 && npc.Mesh/10==554 && Kernel.GetDistance(p.Entity.X,p.Entity.Y,npc.X,npc.Y)<=6;}
        internal static void Tick(){if(Enabled)W.Tick();}
        internal static void OnLogin(GameState p){
            if(!Enabled || !Q.Player(p,false) || !SessionInstall.TryEnter(p.Entity.UID))return;
            try{W.RecoverLogin(p);W.Tick();W.RecoverLogin(p);lock(p.ItemSyncRoot){if(S.Eligible(p))Pending(p);}}
            catch(Exception ex){Q.Fail(p,"mad-login",ex);}finally{SessionInstall.Exit(p.Entity.UID);}
        }
        private static void Pending(GameState p){foreach(var receipt in S.PendingEffects(p))S.ApplyMixture(p,receipt,()=>Enabled && S.Eligible(p));}
        private static string Hint(S.Run run){switch(run.Stage){
            case 0:return "Du San has fallen ill. Will you visit him near Ape City?";
            case 1:return "Du San waits at 552,601 in Ape City.";
            case 2:return "Ask General Judd at 345,251 in Twin City about Du San's illness.";
            case 3:return "Hunt Snakemen west of Ape City for a Venomous Fang. Show it to Judd.";
            case 4:return "Take the fang to Pharmacist Dong at 431,331. Leave two free places.";
            case 5:return "Use Blue Toxin, then Green, beside Dong. Other mixtures last twenty seconds; bring a fresh fang to replace spent toxins.";
            case 6:return "Touch all seven stones before five minutes pass, then use one powder to combine them.";
            case 7:return "Bring the Rainbow Powder to Du San at 552,601.";
            case 8:return "Du San is well again. Chang has your reward; leave three free places.";
            default:return "Du San owes you his life. Your reward is yours.";
        }}
        internal static bool HandleNpc(GameState p,NpcRequest request){
            if(!Enabled || p==null || request==null)return false;uint npc=p.ActiveNpc;bool chang=npc==AzurePillQuest.Chang,stone=npc>=W.StoneStart && npc<W.StoneStart+7;
            if(!stone && npc!=DuSan && npc!=Judd && npc!=Dong && (!chang || request.OptionID<110 || request.OptionID>112))return false;
            if(!S.Eligible(p) || request.NpcID!=0 && request.NpcID!=npc || !stone && !Near(p,npc) || !SessionInstall.TryEnter(p.Entity.UID))return true;
            try{lock(p.ItemSyncRoot){
                if(stone){var scene=W.Find(p);if(scene!=null && (request.OptionID==0 || request.OptionID==1)){
                    if(S.Collect(p,scene,npc,()=>Enabled && S.Eligible(p)))p.Send("You gather a coloured powder. Collect all seven, then use one to combine them.");Refresh(p);
                }return true;}
                if(!Near(p,npc))return true;Func<bool> context=()=>Near(p,npc);if(npc==Dong)Pending(p);var run=S.Load(p);string note=null;
                if(request.OptionID==(chang?111:1)){
                    bool changed=false;
                    if(chang && run.Stage==0)changed=S.Advance(p,0,context);
                    else if(npc==DuSan && run.Stage==1)changed=S.Advance(p,1,context);
                    else if(npc==Judd && (run.Stage==2 || run.Stage==3))changed=S.Advance(p,run.Stage,context);
                    else if(npc==Dong && run.Stage==4)changed=S.Advance(p,4,context);
                    else if(npc==Dong && run.Stage==5)changed=S.Replenish(p,context);
                    else if(npc==DuSan && run.Stage==7)changed=S.HandInPowder(p,context);
                    else if(chang && run.Stage==8)changed=S.Claim(p,context);
                    if(!changed)note=Hint(run);
                }else if(request.OptionID==(chang?112:2)){
                    W.RecoverLogin(p);bool recovered=S.Recover(p,context);note=recovered?"Your errand can continue. Keep these belongings close.":"Check your bag and storage. I cannot replace belongings you still own.";
                }else if(npc==Dong && request.OptionID==3){S.CancelSelection(p,context);note="Start again with Blue Toxin, then Green.";}
                run=S.Load(p);var d=new Npcs(p);d.Text(note??Hint(run));d.Option(chang && run.Stage==8?"Claim my reward.":npc==Dong && run.Stage==5?"Extract this fresh fang.":"About Du San.",(byte)(chang?111:1));
                if(run.Stage>=3 && run.Stage<=7)d.Option("I lost my quest belongings.",(byte)(chang?112:2));if(npc==Dong && run.Stage==5)d.Option("Cancel my toxin selection.",3);d.Option("Farewell.",255);d.Send();Refresh(p);
            }}catch(Exception ex){Q.Fail(p,"mad-npc",ex);}finally{SessionInstall.Exit(p.Entity.UID);}return true;
        }
        internal static bool TryUse(ConquerItem item,GameState p){
            if(!Enabled || item==null || item.ID<729978 || item.ID>729989)return false;
            if(!S.Eligible(p) || !SessionInstall.TryEnter(p.Entity.UID))return true;
            W.Scene reserved=null;
            try{lock(p.ItemSyncRoot){ConquerItem owned;if(!p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(owned,item))return true;
                if(item.ID>=729983){var scene=W.Find(p);if(scene!=null && S.Combine(p,scene,item.UID,()=>Enabled && S.Eligible(p)))W.Exit(scene,p);else p.Send("Gather all seven colours before the five minutes end.");}
                else if(item.ID>=729979 && item.ID<=729981){
                    if(!BesideDong(p)){p.Send("Use your earned toxins beside Pharmacist Dong at 431,331.");return true;}
                    Pending(p);if(S.SelectedToxin(p)==729981 && item.ID==729980){reserved=W.TryCreate(p);if(reserved==null){p.Send("The mountain paths are busy. Try again shortly.");return true;}}
                    var result=S.UseToxin(p,item.UID,reserved,()=>BesideDong(p));
                    if(result.Outcome==S.Mixture.KunLun){W.Enter(reserved,p,()=>Enabled && S.Eligible(p) && W.Registered(reserved,p));reserved=null;p.Send("Five minutes remain. Touch the seven stones, then use one of their powders.");}
                    else{if(reserved!=null){W.Release(reserved);reserved=null;}if(result.Outcome==S.Mixture.Selected)p.Send("The first toxin is ready. Use the second, or ask Dong to cancel.");else if(result.Receipt!=Guid.Empty)S.ApplyMixture(p,result.Receipt,()=>Enabled && S.Eligible(p));}
                }else p.Send(Hint(S.Load(p)));Refresh(p);
            }}catch(Exception ex){
                try{if(reserved!=null && !reserved.Released){lock(p.ItemSyncRoot){if(Q.Player(p,false))W.Exit(reserved,p);else if(p.Entity.MapID!=reserved.Map.ID)W.Release(reserved);}}}catch(Exception cleanup){Q.Fail(p,"mad-admission-cleanup",cleanup);}
                Q.Fail(p,"mad-item",ex);
            }finally{SessionInstall.Exit(p.Entity.UID);}return true;
        }
        internal static void OnDeath(GameState p,Entity body){
            if(!Enabled || !S.Eligible(p) || body==null || body.MapID!=1020 || body.MonsterInfo==null || body.MonsterInfo.ID!=13 || !SessionInstall.TryEnter(p.Entity.UID))return;
            try{lock(p.ItemSyncRoot)if(S.Hunt(p,body,()=>Enabled && S.Eligible(p)))Refresh(p);}catch(Exception ex){Q.Fail(p,"mad-hunt",ex);}finally{SessionInstall.Exit(p.Entity.UID);}
        }
        internal static bool Protect(uint id){return Enabled && id>=1781 && id<=1786;}
        internal static void Refresh(GameState p){if(Enabled && Q.Player(p,false) && p.Quests!=null)p.Quests.SendFullGUI();}
        internal static void Fill(GameState p,List<MsgTaskStatus.QuestData> list,HashSet<uint> seen){
            if(!Enabled)return;for(uint id=1781;id<=1786;id++)seen.Add(id);if(!Q.Player(p,false) || p.Entity.Level<60 || p.Entity.Reborn>2)return;
            lock(p.ItemSyncRoot)try{var run=S.Load(p);int current=run.Stage<=4?run.Stage:run.Stage<=7?5:run.Stage==8?6:7;
                for(uint id=1781;id<=1786;id++){int step=(int)id-1780;var status=current>step?MsgTaskStatus.QuestData.QuestStatus.Finished:current==step?MsgTaskStatus.QuestData.QuestStatus.Accepted:MsgTaskStatus.QuestData.QuestStatus.Available;
                    list.Add(MsgTaskStatus.QuestData.Create((QuestID)id,status,0));}
            }catch(Exception ex){Q.Fail(p,"mad-journal",ex);}
        }
    }
}
