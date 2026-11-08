using System;
using System.Collections.Generic;
using Revora.Client;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using S=Revora.Game.Features.MysticRingStore;
namespace Revora.Game.Features
{
    internal static class MysticRingQuest
    {
        internal const uint Minstrel=4500,Shen=4501,Junior=900071742;
        internal static bool Enabled {get;private set;}
        internal static void Initialize(){Map map;if(!Kernel.Maps.TryGetValue(1011,out map))throw new InvalidOperationException("Mystic map unavailable.");
            CheckNpc(map,Minstrel,184,282,675);CheckNpc(map,Shen,779,477,676);CheckNpc(map,Junior,920,552,677);S.Initialize();
            CityGuardSystem.SpawnTalker(Minstrel,1011,184,282,6750,"Minstrel",0);CityGuardSystem.SpawnTalker(Shen,1011,779,477,6760,"DealerShen",0);
            CityGuardSystem.SpawnTalker(Junior,1011,920,552,6770,"ShenJunior",0);
            for(uint id=1738;id<=1744;id++)QuestInfo.Register((QuestID)id,QuestType.EquipmentBonus);Enabled=true;
        }
        private static void CheckNpc(Map map,uint id,ushort x,ushort y,ushort model){Revora.Interfaces.INpc n;if(map.Npcs.TryGetValue(id,out n) && (n.X!=x || n.Y!=y || n.Mesh/10!=model))throw new InvalidOperationException("Mystic NPC placement changed.");}
        private static bool Near(GameState p,uint npc){return Enabled && S.Eligible(p) && Q.Near(p,npc,npc==AzurePillQuest.Chang?(ushort)1002:(ushort)1011);}
        private static string Hint(S.Run run){switch(run.Stage){
            case 0:return "The Minstrel seeks an old ring. Will you hear his story?";
            case 1:return "Find the Minstrel at 184,282 in Phoenix Castle.";
            case 2:return "Dealer Shen may know the ring. Find him at 779,477.";
            case 3:return "Shen's family needs twelve Stanchers and twelve Agrypnotics.";
            case 4:return "Defeat twenty Ratlings or FireRatL38 near the village. You have defeated "+run.Kills+".";
            case 5:return "Take Shen's Castle Gate to Shen Junior at 920,552.";
            case 6:return "Return to Dealer Shen with the Antique Ring.";
            case 7:return "Bring the Antique Ring to the Minstrel at 184,282.";
            case 8:return "Use the Mystic Ring beside the Minstrel. Wait for any other disguise to end.";
            case 9:return "Chang has your reward in Twin City. Leave two free places.";
            default:return "The ring's secret is yours, and so is your reward.";
        }}
        internal static bool HandleNpc(GameState p,NpcRequest request){
            if(p==null || request==null)return false;bool chang=p.ActiveNpc==AzurePillQuest.Chang;
            if(p.ActiveNpc!=Minstrel && p.ActiveNpc!=Shen && p.ActiveNpc!=Junior && (!chang || request.OptionID<80 || request.OptionID>82))return false;
            uint npc=p.ActiveNpc;if(!Near(p,npc) || request.NpcID!=0 && request.NpcID!=npc || !SessionInstall.TryEnter(p.Entity.UID))return true;
            try{lock(p.ItemSyncRoot){if(!Near(p,npc))return true;var run=S.Load(p);Func<bool> context=()=>Near(p,npc);string note=null;
                if(request.OptionID==(chang?81:1)){
                    if(chang && run.Stage==9)S.Claim(p,context);
                    else{int action=chang && run.Stage==0?0:npc==Minstrel && run.Stage==1?1:npc==Shen && run.Stage==2?2:npc==Shen && run.Stage==3?3:
                            npc==Shen && run.Stage==4?4:npc==Junior && run.Stage==5?5:npc==Shen && run.Stage==6?6:npc==Minstrel && run.Stage==7?7:-1;
                        if(action>=0){bool changed=action<4?S.Advance(p,action,context):S.Progress(p,action,context);if(!changed)note="Bring the requested belongings and leave room in your bag. "+Hint(run);}
                    }
                }else if(request.OptionID==(chang?82:2))note=S.Recover(p,context)?"Your lost belonging is replaced. Continue your errand.":"Check your bag and storage. I cannot replace something you still own.";
                run=S.Load(p);var d=new Npcs(p);d.Text(note??Hint(run));d.Option(chang && run.Stage==9?"Claim my reward.":"About the ring.",(byte)(chang?81:1));
                if(run.Stage>=5 && run.Stage<=8)d.Option("I lost the scroll or ring.",(byte)(chang?82:2));d.Option("Farewell.",255);d.Send();Refresh(p);
            }}catch(Exception ex){Q.Fail(p,"mystic-npc",ex);}finally{SessionInstall.Exit(p.Entity.UID);}return true;
        }
        private static bool BesideMinstrel(GameState p){Revora.Interfaces.INpc npc;return Enabled && S.Eligible(p) && p.Entity.MapID==1011 && p.Map.Npcs.TryGetValue(Minstrel,out npc) && Kernel.GetDistance(p.Entity.X,p.Entity.Y,npc.X,npc.Y)<=6;}
        internal static bool TryUse(ConquerItem item,GameState p){
            if(item==null || item.ID!=721708)return false;
            if(!BesideMinstrel(p) || !SessionInstall.TryEnter(p.Entity.UID))return true;
            try{lock(p.ItemSyncRoot){ConquerItem owned;if(!BesideMinstrel(p) || !p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(owned,item))return true;
                if(!S.UseRing(p,item.UID,()=>BesideMinstrel(p)))p.Send("Keep your earned ring and wait for any other disguise to end.");Refresh(p);
            }}catch(Exception ex){Q.Fail(p,"mystic-ring-use",ex);}finally{SessionInstall.Exit(p.Entity.UID);}return true;
        }
        internal static void OnDeath(GameState p,Entity body){
            if(!Enabled || !S.Eligible(p) || body==null || body.MapID!=1011 || body.MonsterInfo==null || body.MonsterInfo.ID!=8 && body.MonsterInfo.ID!=67 || !SessionInstall.TryEnter(p.Entity.UID))return;
            try{lock(p.ItemSyncRoot)S.Hunt(p,body,()=>Enabled && S.Eligible(p));}
            catch(Exception ex){Q.Fail(p,"mystic-hunt",ex);}finally{SessionInstall.Exit(p.Entity.UID);}
        }
        internal static bool Protect(uint id){return Enabled && id>=1738 && id<=1744;}
        internal static void Refresh(GameState p){if(Enabled && Q.Player(p,false) && p.Quests!=null)p.Quests.SendFullGUI();}
        internal static void Fill(GameState p,List<MsgTaskStatus.QuestData> list,HashSet<uint> seen){
            if(!Enabled)return;for(uint id=1738;id<=1744;id++)seen.Add(id);if(!Q.Player(p,false) || p.Entity.Level<48 || p.Entity.Reborn>2)return;
            lock(p.ItemSyncRoot)try{var run=S.Load(p);int current=run.Stage<=4?run.Stage:run.Stage<=6?5:run.Stage<=8?6:run.Stage==9?7:8;
                for(uint id=1738;id<=1744;id++){int step=(int)id-1737;var status=current>step?MsgTaskStatus.QuestData.QuestStatus.Finished:current==step?MsgTaskStatus.QuestData.QuestStatus.Accepted:MsgTaskStatus.QuestData.QuestStatus.Available;
                    list.Add(MsgTaskStatus.QuestData.Create((QuestID)id,status,0));}
            }catch(Exception ex){Q.Fail(p,"mystic-journal",ex);}
        }
    }
}
