using System;
using System.Collections.Generic;
using Revora.Client;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using S=Revora.Game.Features.SpunGoldStore;
namespace Revora.Game.Features
{
    internal static class SpunGoldQuest
    {
        internal const uint Hong=4464,Zhao=4465;
        internal static bool Enabled {get;private set;}
        internal static void Initialize(){
            Map map;if(!Kernel.Maps.TryGetValue(1011,out map))throw new InvalidOperationException("Spun Gold map unavailable.");
            CheckNpc(map,Hong,194,260,6830);CheckNpc(map,Zhao,200,325,6840);S.Initialize();
            CityGuardSystem.SpawnTalker(Hong,1011,194,260,6830,"VeteranHong",0);
            CityGuardSystem.SpawnTalker(Zhao,1011,200,325,6840,"ZhaoJian",0);
            for(uint id=1723;id<=1726;id++)QuestInfo.Register((QuestID)id,QuestType.EquipmentBonus);Enabled=true;
        }
        private static void CheckNpc(Map map,uint id,ushort x,ushort y,ushort mesh){Revora.Interfaces.INpc n;
            if(map.Npcs.TryGetValue(id,out n) && (n.X!=x || n.Y!=y || n.Mesh!=mesh))throw new InvalidOperationException("Spun Gold NPC placement changed.");}
        private static bool Near(GameState p,uint npc){return Enabled && S.Eligible(p) && Q.Near(p,npc,npc==AzurePillQuest.Chang?(ushort)1002:(ushort)1011);}
        private static string Hint(int stage){switch(stage){
            case 0:return "Veteran Hong in Maple Forest has lost his armor. Will you help him?";
            case 1:return "Speak to Veteran Hong at 194,260 in Maple Forest.";
            case 2:return "The Bandits carry invitations to Cloud's feast. Bring one to Hong.";
            case 3:return "Zhao Jian at 200,325 can disguise you. Use the Offering near the altar at 100,430.";
            case 4:return "Cloud has answered your Offering. Defeat him before he leaves.";
            case 5:return "Open Cloud's Wooden Box. Make room first if you have not received it.";
            case 6:return "Take the Spun Gold Armor back to Hong.";
            case 7:return "Chang awaits you in Twin City. Leave two spaces for your reward.";
            default:return "Hong's armor is safe again. Your reward is yours.";
        }}
        internal static bool HandleNpc(GameState p,NpcRequest request){
            if(p==null || request==null)return false;bool chang=p.ActiveNpc==AzurePillQuest.Chang;
            if(p.ActiveNpc!=Hong && p.ActiveNpc!=Zhao && (!chang || request.OptionID<60 || request.OptionID>62))return false;
            uint npc=p.ActiveNpc;if(!Near(p,npc) || request.NpcID!=0 && request.NpcID!=npc || !SessionInstall.TryEnter(p.Entity.UID))return true;
            try{lock(p.ItemSyncRoot){
                if(!Near(p,npc))return true;Func<bool> context=()=>Near(p,npc);var run=S.Load(p);string note=null;
                if(chang && request.OptionID==61)S.Act(p,run.Stage==0?0:5,0,context);
                else if(npc==Hong && request.OptionID==1){int action=run.Stage==1?1:run.Stage==2?2:run.Stage==6?4:-1;if(action>=0)S.Act(p,action,0,context);}
                else if(npc==Zhao && request.OptionID==1 && run.Stage==3){
                    Guid token=SpunGoldAppearance.Apply(p,()=>context() && S.Load(p).Stage==3);
                    note=token==Guid.Empty?"Give me a moment. I am helping another traveler.":"The disguise lasts five minutes. Take your Offering to the altar.";
                }
                else if(request.OptionID==(chang?62:2)){
                    SpunGoldCombat.RecoverAttempt(p,context);bool restored=S.Recover(p,context);
                    note=restored?"Your task can continue. Check your belongings.":"Check your bag and storage. If Cloud is still here, finish your encounter.";
                }
                run=S.Load(p);var d=new Npcs(p);d.Text(note??Hint(run.Stage));
                if(chang && (run.Stage==0 || run.Stage==7))d.Option(run.Stage==0?"I will help Hong.":"Claim my reward.",61);
                if(npc==Hong && (run.Stage==1 || run.Stage==2 || run.Stage==6))d.Option("About your armor.",1);
                if(npc==Zhao && run.Stage==3)d.Option("Disguise me.",1);
                if(run.Stage>=2 && run.Stage<7)d.Option("I lost my belongings.",(byte)(chang?62:2));
                d.Option("Farewell.",255);d.Send();Refresh(p);
            }}catch(Exception ex){Q.Fail(p,"spun-gold-npc",ex);}finally{SessionInstall.Exit(p.Entity.UID);}return true;
        }
        internal static bool TryUse(ConquerItem item,GameState p){
            if(item==null || item.ID!=729972 && item.ID!=729973)return false;
            if(!Enabled || !S.Eligible(p) || !SessionInstall.TryEnter(p.Entity.UID))return true;
            try{lock(p.ItemSyncRoot){ConquerItem owned;
                if(!p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(owned,item) || !item.Bound || item.StackSize!=1 || item.Lock!=0 || item.Suspicious
                    || item.InWardrobe || item.Days!=0 || item.TimeLeftInMinutes!=0 || item.Position!=0 || item.Warehouse!=0)return true;
                var run=S.Load(p);if(run.Item!=item.UID)return true;
                Func<bool> context=()=>S.Eligible(p) && p.Inventory.TryGetItem(item.UID,out owned) && ReferenceEquals(owned,item);
                if(item.ID==729973)S.Act(p,3,item.UID,context);
                else if(run.Stage==3){var token=SpunGoldAppearance.Token(p);
                    if(token==Guid.Empty)p.Send("Ask Zhao Jian to disguise you first.");
                    else if(SpunGoldCombat.Summon(p,token,context)==null)p.Send("Bring your Offering to the altar at 100,430. Wait if the altar is crowded.");
                }Refresh(p);
            }}catch(Exception ex){Q.Fail(p,"spun-gold-item",ex);}finally{SessionInstall.Exit(p.Entity.UID);}return true;
        }
        internal static void OnDeath(GameState p,Entity body){
            if(!Enabled || !S.Eligible(p) || body==null || body.MonsterInfo==null || body.MonsterInfo.ID!=7 && body.MonsterInfo.ID!=66 || !SessionInstall.TryEnter(p.Entity.UID))return;
            try{lock(p.ItemSyncRoot)if(S.Invitation(p,body,()=>S.Eligible(p)))p.Send("You found an invitation. Take it to Veteran Hong.");}
            catch(Exception ex){Q.Fail(p,"spun-gold-hunt",ex);}finally{SessionInstall.Exit(p.Entity.UID);}
        }
        internal static void Tick(){if(!Enabled)return;SpunGoldCombat.Tick();SpunGoldAppearance.Tick();}
        internal static void OnLogin(GameState p){if(!Enabled || !S.Eligible(p) || !SessionInstall.TryEnter(p.Entity.UID))return;
            try{lock(p.ItemSyncRoot)SpunGoldCombat.RecoverAttempt(p,()=>S.Eligible(p));}
            catch(Exception ex){Q.Fail(p,"spun-gold-login",ex);}finally{SessionInstall.Exit(p.Entity.UID);}}
        internal static bool Protect(uint id){return Enabled && id>=1723 && id<=1726;}
        internal static void Refresh(GameState p){if(Enabled && Q.Player(p,false) && p.Quests!=null)p.Quests.SendFullGUI();}
        internal static void Fill(GameState p,List<MsgTaskStatus.QuestData> list,HashSet<uint> seen){
            if(!Enabled)return;for(uint id=1723;id<=1726;id++)seen.Add(id);if(!Q.Player(p,false) || p.Entity.Level<30 || p.Entity.Reborn>2)return;
            lock(p.ItemSyncRoot)try{var run=S.Load(p);int step=run.Stage<3?run.Stage:run.Stage<7?3:run.Stage==7?4:5;
                for(uint id=1723;id<=1726;id++){int index=(int)id-1722;
                    var state=step>index?MsgTaskStatus.QuestData.QuestStatus.Finished:step==index?MsgTaskStatus.QuestData.QuestStatus.Accepted:MsgTaskStatus.QuestData.QuestStatus.Available;
                    list.Add(MsgTaskStatus.QuestData.Create((QuestID)id,state,0));}
            }catch(Exception ex){Q.Fail(p,"spun-gold-journal",ex);}
        }
    }
}
