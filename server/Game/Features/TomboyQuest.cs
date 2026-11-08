using System;
using System.Collections.Generic;
using System.Linq;
using Revora.Client;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using S=Revora.Game.Features.TomboyStore;
namespace Revora.Game.Features
{
    internal static class TomboyQuest
    {
        internal const uint Lynn=4718,Carolyn=4719;
        internal static bool Enabled {get;private set;}
        internal static void Initialize(){
            CheckNpc(Lynn,461,661,424);CheckNpc(Carolyn,460,667,425);S.Initialize();
            CityGuardSystem.SpawnTalker(Lynn,1000,461,661,4240,"FarmerLynn",0);CityGuardSystem.SpawnTalker(Carolyn,1000,460,667,4250,"Carolyn",0);
            for(uint id=1795;id<=1799;id++)QuestInfo.Register((QuestID)id,QuestType.EquipmentBonus);Enabled=true;
        }
        private static void CheckNpc(uint id,ushort x,ushort y,ushort model){Map map;Revora.Interfaces.INpc npc;
            if(!Kernel.Maps.TryGetValue(1000,out map))throw new InvalidOperationException("Tomboy NPC map unavailable.");
            if(map.Npcs.TryGetValue(id,out npc) && (npc.X!=x || npc.Y!=y || npc.Mesh/10!=model))throw new InvalidOperationException("Tomboy NPC placement changed.");
        }
        private static bool Near(GameState p,uint npc){return Enabled && S.Eligible(p) && Q.Near(p,npc,npc==AzurePillQuest.Chang?(ushort)1002:(ushort)1000);}
        private static string Hint(S.Run run){switch(run.Stage){
            case 0:return "Farmer Lynn could use a helping hand. You will find him at 461,661 in Desert City.";
            case 1:return "Speak with Farmer Lynn at 461,661 in Desert City.";
            case 2:return "Bring Lynn ten Marbles from the Rock Monsters near 356,358.";
            case 3:return "Hunt RockMonsterL78 near 265,264 for stones. Click Leaf with Maple for Rune Jade, Maple with Ocean for Moon Jade, and Ocean with Leaf for Sun Jade.";
            case 4:return "Carolyn waits at 460,667. Perhaps she will hear you out.";
            case 5:return "Chang has your reward. Leave three places in your bag.";
            default:return "Lynn is grateful for your help. Your reward is yours.";
        }}
        internal static bool HandleNpc(GameState p,NpcRequest request){
            if(!Enabled || p==null || request==null)return false;uint npc=p.ActiveNpc;bool chang=npc==AzurePillQuest.Chang;
            if(npc!=Lynn && npc!=Carolyn && (!chang || request.OptionID<130 || request.OptionID>132))return false;
            if(!Near(p,npc) || request.NpcID!=0 && request.NpcID!=npc || !SessionInstall.TryEnter(p.Entity.UID))return true;
            try{lock(p.ItemSyncRoot){if(!Near(p,npc))return true;Func<bool> context=()=>Near(p,npc);var run=S.Load(p);string note=null;
                if(request.OptionID==(chang?131:1)){
                    if(chang && run.Stage==0)S.Advance(p,0,context);
                    else if(npc==Lynn && run.Stage>=1 && run.Stage<=3)S.Advance(p,run.Stage,context);
                    else if(chang && run.Stage==5)S.Claim(p,context);
                }else if(npc==Carolyn && run.Stage==4 && request.OptionID>=11 && request.OptionID<=13){
                    if(!S.Answer(p,(byte)(request.OptionID-10),context))note="Think back to the stones you joined.";
                }else if(request.OptionID==(chang?132:2))note=S.Recover(p,context)?"Your lost materials are accounted for. Gather raw stones again; earned jade can be restored.":"Check your bag and storage. I cannot replace what you still own.";
                run=S.Load(p);var d=new Npcs(p);
                if(npc==Carolyn && run.Stage==4){d.Text(note??"Which stone must you click alongside Maple to form Rune Jade?");d.Option("Ocean Stone.",11);d.Option("Leaf Stone.",12);d.Option("Maple Stone.",13);}
                else{d.Text(note??Hint(run));d.Option(chang && run.Stage==5?"Claim my reward.":"I have come to help.",(byte)(chang?131:1));}
                if(run.Stage==2 || run.Stage==3)d.Option("I lost my quest materials.",(byte)(chang?132:2));d.Option("Farewell.",255);d.Send();Refresh(p);
            }}catch(Exception ex){Q.Fail(p,"tomboy-npc",ex);}finally{SessionInstall.Exit(p.Entity.UID);}return true;
        }
        internal static bool TryUse(ConquerItem item,GameState p){
            if(!Enabled || item==null || !(item.ID>=721951 && item.ID<=721953 || item.ID>=710581 && item.ID<=710584))return false;
            if(!S.Eligible(p) || !SessionInstall.TryEnter(p.Entity.UID))return true;
            try{lock(p.ItemSyncRoot){ConquerItem owned;if(!p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(item,owned))return true;
                if(item.ID>=721951 && item.ID<=721953){uint partner;uint result=S.Recipe(item.ID,out partner);int before=p.Inventory.Objects.Count(i=>i.ID==result);
                    if(S.Fuse(p,item.UID,()=>Enabled && S.Eligible(p)))p.Send(p.Inventory.Objects.Count(i=>i.ID==result)>before?"The stones join into jade.":"The stones crumble. Hunt more stones and try again.");else p.Send("Bring the matching earned stone. Check your bag and storage for jade you already made.");
                }else p.Send(Hint(S.Load(p)));Refresh(p);
            }}catch(Exception ex){Q.Fail(p,"tomboy-item",ex);}finally{SessionInstall.Exit(p.Entity.UID);}return true;
        }
        internal static void OnDeath(GameState p,Entity body){
            if(!Enabled || !S.Eligible(p) || body==null || body.MapID!=1000 || body.MonsterInfo==null || body.MonsterInfo.ID!=16 && body.MonsterInfo.ID!=75 || !SessionInstall.TryEnter(p.Entity.UID))return;
            try{lock(p.ItemSyncRoot)if(S.Hunt(p,body,()=>Enabled && S.Eligible(p)))Refresh(p);}catch(Exception ex){Q.Fail(p,"tomboy-hunt",ex);}finally{SessionInstall.Exit(p.Entity.UID);}
        }
        internal static bool Protect(uint id){return Enabled && id>=1795 && id<=1799;}
        internal static void OnLogin(GameState p){Refresh(p);}
        internal static void Refresh(GameState p){if(Enabled && Q.Player(p,false) && p.Quests!=null)p.Quests.SendFullGUI();}
        internal static void Fill(GameState p,List<MsgTaskStatus.QuestData> list,HashSet<uint> seen){
            if(!Enabled)return;for(uint id=1795;id<=1799;id++)seen.Add(id);if(!Q.Player(p,false) || p.Entity.Level<78 || p.Entity.Reborn>2)return;
            lock(p.ItemSyncRoot)try{var run=S.Load(p);
                for(uint id=1795;id<=1799;id++){int step=(int)id-1794;var status=run.Stage>step?MsgTaskStatus.QuestData.QuestStatus.Finished:run.Stage==step?MsgTaskStatus.QuestData.QuestStatus.Accepted:MsgTaskStatus.QuestData.QuestStatus.Available;
                    list.Add(MsgTaskStatus.QuestData.Create((QuestID)id,status,0));}
            }catch(Exception ex){Q.Fail(p,"tomboy-journal",ex);}
        }
    }
}
