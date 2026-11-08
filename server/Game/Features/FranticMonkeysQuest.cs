using System;
using System.Collections.Generic;
using Revora.Client;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using S=Revora.Game.Features.FranticMonkeysStore;
using W=Revora.Game.Features.FranticMonkeysScenes;
namespace Revora.Game.Features
{
    internal static class FranticMonkeysQuest
    {
        internal const uint Bao=4485,Doctor=4486;
        internal static bool Enabled {get;private set;}
        internal static void Initialize(){
            CheckNpc(1020,Bao,553,584,687);CheckNpc(1002,Doctor,440,343,688);
            S.Initialize();W.InitializeTravel();W.ValidateTerrain();for(int i=0;i<3;i++)W.Profile(i);W.RecoverInterrupted();
            CityGuardSystem.SpawnTalker(Bao,1020,553,584,6870,"OfficerBao",0);CityGuardSystem.SpawnTalker(Doctor,1002,440,343,6880,"RoyalDoctorLi",0);
            for(uint id=1745;id<=1750;id++)QuestInfo.Register((QuestID)id,QuestType.EquipmentBonus);Enabled=true;
        }
        private static void CheckNpc(ushort id,uint npc,ushort x,ushort y,ushort model){Map map;Revora.Interfaces.INpc n;
            if(!Kernel.Maps.TryGetValue(id,out map))throw new InvalidOperationException("Frantic NPC map unavailable.");
            if(map.Npcs.TryGetValue(npc,out n) && (n.X!=x || n.Y!=y || n.Mesh/10!=model))throw new InvalidOperationException("Frantic NPC placement changed.");
        }
        private static bool Near(GameState p,uint npc){return Enabled && S.Eligible(p) && Q.Near(p,npc,npc==Bao?(ushort)1020:(ushort)1002);}
        internal static void Tick(){if(Enabled){W.Tick();FranticMonkeysApes.Tick();}}
        internal static void OnLogin(GameState p){if(!Enabled)return;try{
            W.RecoverLogin(p);W.Tick();W.RecoverLogin(p);FranticMonkeysApes.Tick();
            lock(p.ItemSyncRoot)FranticMonkeysApes.Recover(p);
        }catch(Exception ex){Q.Fail(p,"frantic-login",ex);}}
        private static string Hint(S.Run r){switch(r.Stage){
            case 0:return "Officer Bao needs help with the monkeys near Ape City.";
            case 1:return "Find Officer Bao at 553,584 in Ape City.";
            case 2:return "Hunt Macaques near 668,673 for a Decayed Heart. Bring it to Bao.";
            case 3:return "Take the heart to Doctor Li at 440,343 in Twin City.";
            case 4:return "Use the poison beside Doctor Li. End any other transformation first.";
            case 5:return "Wait one minute for the poison to fade, then defeat five trial monkeys.";
            case 6:return "Doctor Li has a vial for you. Leave one free place in your bag.";
            case 7:return "Use the vial at 795,471 in Ape City. Defeat the apes and their leader.";
            case 8:return "Bring the Devilish Sword to Officer Bao.";
            case 9:return "Report to Chang in Twin City. Leave two places for your reward.";
            default:return "The monkeys are quiet again. You have earned your reward.";
        }}
        internal static bool HandleNpc(GameState p,NpcRequest request){
            if(p==null || request==null)return false;bool chang=p.ActiveNpc==AzurePillQuest.Chang;
            if(p.ActiveNpc!=Bao && p.ActiveNpc!=Doctor && (!chang || request.OptionID<90 || request.OptionID>92))return false;
            uint npc=p.ActiveNpc;if(!Near(p,npc) || request.NpcID!=0 && request.NpcID!=npc || !SessionInstall.TryEnter(p.Entity.UID))return true;
            try{lock(p.ItemSyncRoot){if(!Near(p,npc))return true;var run=S.Load(p);Func<bool> context=()=>Near(p,npc);string note=null;
                if(request.OptionID==(chang?91:1)){
                    bool changed=false;
                    if(chang && run.Stage==0)changed=S.Advance(p,0,context);
                    else if(npc==Bao && (run.Stage==1 || run.Stage==2))changed=S.Advance(p,run.Stage,context);
                    else if(npc==Doctor && run.Stage==3)changed=S.Advance(p,3,context);
                    else if(npc==Doctor && run.Stage==6)changed=S.TrialReward(p,context);
                    else if(npc==Bao && run.Stage==8)changed=S.HandInSword(p,context);
                    else if(chang && run.Stage==9)changed=S.Claim(p,context);
                    if(!changed)note=Hint(run);
                }else if(request.OptionID==(chang?92:2)){
                    W.RecoverLogin(p);bool recovered=FranticMonkeysApes.Recover(p);recovered=S.Recover(p,context)||recovered;
                    note=recovered?"Your errand can continue. Keep your belongings close.":"Check your bag and storage. I cannot replace something you still own.";
                }
                run=S.Load(p);var d=new Npcs(p);d.Text(note??Hint(run));d.Option(chang && run.Stage==9?"Claim my reward.":"About the monkeys.",(byte)(chang?91:1));
                if(run.Stage>=2 && run.Stage<=8)d.Option("I lost my quest belongings.",(byte)(chang?92:2));d.Option("Farewell.",255);d.Send();Refresh(p);
            }}catch(Exception ex){Q.Fail(p,"frantic-npc",ex);}finally{SessionInstall.Exit(p.Entity.UID);}return true;
        }
        internal static bool TryUse(ConquerItem item,GameState p){
            if(!Enabled || item==null || item.ID!=721700 && item.ID!=721701)return false;
            bool started=item.ID==721700?FranticMonkeysAdmission.Start(p,item):FranticMonkeysApes.Start(p,item);
            if(!started && Q.Player(p))p.Send(item.ID==721700?"Use your earned poison beside Doctor Li when you are ready.":"Use your earned vial at 795,471. Wait a minute between attempts.");Refresh(p);return true;
        }
        internal static void OnDeath(GameState p,Entity body){
            if(!Enabled || !S.Eligible(p) || body==null || body.MapID!=1020 || body.MonsterInfo==null || body.MonsterInfo.ID!=10 && body.MonsterInfo.ID!=69 || !SessionInstall.TryEnter(p.Entity.UID))return;
            try{lock(p.ItemSyncRoot)S.Hunt(p,body,()=>Enabled && S.Eligible(p));}catch(Exception ex){Q.Fail(p,"frantic-hunt",ex);}finally{SessionInstall.Exit(p.Entity.UID);}
        }
        internal static bool Protect(uint id){return Enabled && id>=1745 && id<=1750;}
        internal static void Refresh(GameState p){if(Enabled && Q.Player(p,false) && p.Quests!=null)p.Quests.SendFullGUI();}
        internal static void Fill(GameState p,List<MsgTaskStatus.QuestData> list,HashSet<uint> seen){
            if(!Enabled)return;for(uint id=1745;id<=1750;id++)seen.Add(id);if(!Q.Player(p,false) || p.Entity.Level<50 || p.Entity.Reborn>2)return;
            lock(p.ItemSyncRoot)try{var run=S.Load(p);int current=run.Stage<=3?run.Stage:run.Stage<=6?4:run.Stage<=8?5:run.Stage==9?6:7;
                for(uint id=1745;id<=1750;id++){int step=(int)id-1744;var status=current>step?MsgTaskStatus.QuestData.QuestStatus.Finished:current==step?MsgTaskStatus.QuestData.QuestStatus.Accepted:MsgTaskStatus.QuestData.QuestStatus.Available;
                    list.Add(MsgTaskStatus.QuestData.Create((QuestID)id,status,0));}
            }catch(Exception ex){Q.Fail(p,"frantic-journal",ex);}
        }
    }
}
