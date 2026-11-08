using System;
using System.Collections.Generic;
using System.Linq;
using Revora.Client;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using S=Revora.Game.Features.HeroWingsStore;
namespace Revora.Game.Features
{
    internal static class HeroWingsQuest
    {
        internal const uint Xiao=4700,Doctor=4701;
        internal static bool Enabled {get;private set;}
        internal static void Initialize(){
            CheckNpc(Xiao,725,541,421);CheckNpc(Doctor,701,595,422);S.Initialize();
            CityGuardSystem.SpawnTalker(Xiao,1015,725,541,4210,"PoorXiao",0);CityGuardSystem.SpawnTalker(Doctor,1015,701,595,4220,"DoctorKnowitAll",0);
            for(uint id=1808;id<=1818;id++)QuestInfo.Register((QuestID)id,QuestType.EquipmentBonus);Enabled=true;
        }
        private static void CheckNpc(uint id,ushort x,ushort y,ushort model){Map map;Revora.Interfaces.INpc npc;
            if(!Kernel.Maps.TryGetValue(1015,out map))throw new InvalidOperationException("Hero Wings NPC map unavailable.");
            if(map.Npcs.TryGetValue(id,out npc) && (npc.X!=x || npc.Y!=y || npc.Mesh/10!=model))throw new InvalidOperationException("Hero Wings NPC placement changed.");
        }
        private static bool Near(GameState p,uint npc){return Enabled && S.Eligible(p) && Q.Near(p,npc,npc==AzurePillQuest.Chang?(ushort)1002:(ushort)1015);}
        private static string Hint(S.Run run){switch(run.Stage){
            case 0:case 1:return "Poor Xiao waits at 725,541 in Bird Island. He needs a healer.";
            case 2:case 9:return "Doctor Know-it-All waits at 701,595. Tell him about Xiao.";
            case 3:return "Tell Xiao the doctor needs a Meteor.";
            case 4:return "Bring one Meteor to Doctor Know-it-All.";
            case 5:return "Bring the doctor ten samples of Bird Blood from Birdmen near 800,675.";
            case 6:return "Read your Ginseng Map. Take a Gallbladder from a HawKing to the marked place, then bring the root to the doctor.";
            case 7:return "Hunt HawkL93 near 451,676. Each pair killed within one minute yields a claw. Bring five to the doctor.";
            case 8:return "Take the doctor's Peel Pill to Xiao.";
            case 10:return "Tell Xiao what the doctor discovered.";
            case 11:return "Chang has your reward. Leave three places in your bag.";
            default:return "Xiao's future looks brighter. Your reward is yours.";
        }}
        internal static bool HandleNpc(GameState p,NpcRequest request){
            if(!Enabled || p==null || request==null)return false;uint npc=p.ActiveNpc;bool chang=npc==AzurePillQuest.Chang;
            if(npc!=Xiao && npc!=Doctor && (!chang || request.OptionID<150 || request.OptionID>152))return false;
            if(!Near(p,npc) || request.NpcID!=0 && request.NpcID!=npc || !SessionInstall.TryEnter(p.Entity.UID))return true;
            try{lock(p.ItemSyncRoot){if(!Near(p,npc))return true;Func<bool> context=()=>Near(p,npc);var run=S.Load(p);string note=null;
                if(request.OptionID==(chang?151:1)){
                    if(chang && run.Stage==0)S.Advance(p,0,context);
                    else if(npc==Xiao && (run.Stage==1 || run.Stage==3 || run.Stage==8))S.Advance(p,run.Stage,context);
                    else if(npc==Doctor && (run.Stage==2 || run.Stage==5 || run.Stage==6 || run.Stage==7 || run.Stage==9))S.Advance(p,run.Stage,context);
                    else if(npc==Doctor && run.Stage==4){var meteor=p.Inventory.Objects.FirstOrDefault(i=>i.ID==1088001 && i.Lock==0 && !i.Suspicious && !i.InWardrobe && i.Days==0 && i.TimeLeftInMinutes==0);if(meteor!=null)S.PayMeteor(p,meteor.UID,context);}
                    else if(npc==Xiao && run.Stage==10)S.Report(p,context);
                    else if(chang && run.Stage==11)S.Claim(p,context);
                }else if(request.OptionID==(chang?152:2))note=S.Recover(p,context)?"Your lost belongings are accounted for. Gather raw ingredients again.":"Check your bag and storage first.";
                run=S.Load(p);var d=new Npcs(p);d.Text(note??Hint(run));d.Option(chang && run.Stage==11?"Claim my reward.":"About Xiao's illness.",(byte)(chang?151:1));
                if(run.Stage>=5 && run.Stage<=8)d.Option("I lost my quest materials.",(byte)(chang?152:2));d.Option("Farewell.",255);d.Send();Refresh(p);
            }}catch(Exception ex){Q.Fail(p,"hero-wings-npc",ex);}finally{SessionInstall.Exit(p.Entity.UID);}return true;
        }
        internal static bool TryUse(ConquerItem item,GameState p){
            if(!Enabled || item==null || !(item.ID==721906 || item.ID>=721908 && item.ID<=721912))return false;
            if(!S.Eligible(p) || !SessionInstall.TryEnter(p.Entity.UID))return true;
            try{lock(p.ItemSyncRoot){ConquerItem owned;if(!p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(item,owned))return true;
                var run=S.Load(p);
                if(item.ID==721909){if(S.Gather(p,item.UID,()=>Enabled && S.Eligible(p)))p.Send("A bitter root rises from the earth. Bring it to the doctor.");else p.Send("Read your Ginseng Map and bring the earned Gallbladder to its marked spot.");}
                else if(item.ID==721912 && run.Stage==6)p.Send("The map marks Bird Island "+S.Sites[run.Site,0]+","+S.Sites[run.Site,1]+". Use your Gallbladder there.");
                else p.Send(Hint(run));Refresh(p);
            }}catch(Exception ex){Q.Fail(p,"hero-wings-item",ex);}finally{SessionInstall.Exit(p.Entity.UID);}return true;
        }
        internal static void OnDeath(GameState p,Entity body){
            if(!Enabled || !S.Eligible(p) || body==null || body.MapID!=1015 || body.MonsterInfo==null || !new uint[]{18,19,77,78}.Contains(body.MonsterInfo.ID) || !SessionInstall.TryEnter(p.Entity.UID))return;
            try{lock(p.ItemSyncRoot)if(S.Hunt(p,body,()=>Enabled && S.Eligible(p)))Refresh(p);}catch(Exception ex){Q.Fail(p,"hero-wings-hunt",ex);}finally{SessionInstall.Exit(p.Entity.UID);}
        }
        internal static bool Protect(uint id){return Enabled && id>=1808 && id<=1818;}
        internal static void OnLogin(GameState p){Refresh(p);}
        internal static void Refresh(GameState p){if(Enabled && Q.Player(p,false) && p.Quests!=null)p.Quests.SendFullGUI();}
        internal static void Fill(GameState p,List<MsgTaskStatus.QuestData> list,HashSet<uint> seen){
            if(!Enabled)return;for(uint id=1808;id<=1818;id++)seen.Add(id);if(!Q.Player(p,false) || p.Entity.Level<90 || p.Entity.Reborn>2)return;
            lock(p.ItemSyncRoot)try{var run=S.Load(p);
                for(uint id=1808;id<=1818;id++){int step=(int)id-1807;var status=run.Stage>step?MsgTaskStatus.QuestData.QuestStatus.Finished:run.Stage==step?MsgTaskStatus.QuestData.QuestStatus.Accepted:MsgTaskStatus.QuestData.QuestStatus.Available;
                    list.Add(MsgTaskStatus.QuestData.Create((QuestID)id,status,0));}
            }catch(Exception ex){Q.Fail(p,"hero-wings-journal",ex);}
        }
    }
}
