using System;
using System.Collections.Generic;
using System.Linq;
using Revora.Client;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using S=Revora.Game.Features.DesertBloodStore;
using W=Revora.Game.Features.DesertBloodScenes;
namespace Revora.Game.Features
{
    internal static class DesertBloodQuest
    {
        internal const uint Harvey=4621,Column=4622,Peach=4633;
        internal static bool Enabled {get;private set;}
        internal static void Initialize(){
            CheckNpc(Harvey,1000,495,579,380);CheckNpc(Column,1000,386,103,381);CheckNpc(Peach,1015,765,602,384);
            S.Initialize();S.InitializeVisits();W.ValidateTerrain();W.RecoverInterrupted();
            CityGuardSystem.SpawnTalker(Harvey,1000,495,579,3800,"Harvey",0);
            CityGuardSystem.SpawnTalker(Column,1000,386,103,3810,"StoneColumn",0);
            CityGuardSystem.SpawnTalker(Peach,1015,765,602,3840,"AuntPeach",0);
            for(uint id=1800;id<=1807;id++)QuestInfo.Register((QuestID)id,QuestType.EquipmentBonus);Enabled=true;
        }
        private static void CheckNpc(uint id,ushort mapId,ushort x,ushort y,ushort model){Map map;Revora.Interfaces.INpc npc;
            if(!Kernel.Maps.TryGetValue(mapId,out map))throw new InvalidOperationException("Desert Blood NPC map unavailable.");
            if(map.Npcs.TryGetValue(id,out npc) && (npc.X!=x || npc.Y!=y || npc.Mesh/10!=model))throw new InvalidOperationException("Desert Blood NPC placement changed.");
        }
        private static bool Near(GameState p,uint npc){return Enabled && S.Eligible(p) && Q.Near(p,npc,npc==AzurePillQuest.Chang?(ushort)1002:npc==Peach?(ushort)1015:(ushort)1000);}
        internal static void Tick(){if(Enabled)W.Tick();}
        private static string Hint(S.Run run){switch(run.Stage){
            case 0:case 1:return "Harvey waits at 495,579 in Desert City. He has a tale of the Crypt.";
            case 2:return "Seek a Stone Spell from BladeGhostL83. Read it, then visit the column at 386,103.";
            case 3:return "Defeat 200 Blade Ghosts and bring their Blunt Sword to the Crypt warrior.";
            case 4:return "The warrior needs five fangs from BladeGhostL83.";
            case 5:return "Tell Harvey what the warrior has asked.";
            case 6:return "Carry Harvey's letter to the Crypt warrior.";
            case 7:return "Take the Peach Bag to Aunt Peach at 765,602 in Bird Island.";
            case 8:return "Read Aunt Peach's note.";
            case 9:return "Speak to Aunt Peach once more.";
            case 10:return "Chang has your reward. Leave three places in your bag.";
            default:return "The warrior's wish is fulfilled. Farewell.";
        }}
        internal static bool HandleNpc(GameState p,NpcRequest request){
            if(!Enabled || p==null || request==null)return false;uint npc=p.ActiveNpc;bool chang=npc==AzurePillQuest.Chang,inside=npc==W.Warrior || npc==W.Pillar;
            if(!inside && npc!=Harvey && npc!=Column && npc!=Peach && (!chang || request.OptionID<140 || request.OptionID>142))return false;
            if(!S.Eligible(p) || request.NpcID!=0 && request.NpcID!=npc || !inside && !Near(p,npc) || !SessionInstall.TryEnter(p.Entity.UID))return true;
            W.Scene reserved=null;
            try{lock(p.ItemSyncRoot){
                if(inside){var scene=W.Find(p);int index=npc==W.Warrior?0:1;if(!S.AtActor(p,scene,index))return true;
                    Func<bool> context=()=>Enabled && S.Eligible(p) && S.AtActor(p,scene,index);
                    if(request.OptionID==1){if(index==0)S.Warrior(p,scene,context);else S.ColumnExperience(p,scene,context);}
                    else if(request.OptionID==2){W.Exit(scene,p);Refresh(p);return true;}
                    var d=new Npcs(p);d.Text(index==0?Hint(S.Load(p)):"The stone offers a small blessing once in each life. Shall I send you back?");d.Option(index==0?"I have come about your request.":"Receive the blessing.",1);d.Option("Return to Desert City.",2);d.Option("Farewell.",255);d.Send();Refresh(p);return true;
                }
                Func<bool> near=()=>Near(p,npc);var run=S.Load(p);string note=null;
                if(request.OptionID==(chang?141:1)){
                    if(chang && run.Stage==0)S.Advance(p,0,near);
                    else if(npc==Harvey && (run.Stage==1 || run.Stage==5))S.Advance(p,run.Stage,near);
                    else if(npc==Peach && run.Stage==7)S.Advance(p,7,near);
                    else if(npc==Peach && run.Stage==9)S.Report(p,near);
                    else if(chang && run.Stage==10)S.Claim(p,near);
                    else if(npc==Column){reserved=W.TryCreate(p);if(reserved==null)note="The path is occupied. Try again shortly.";
                        else if(W.Enter(reserved,p,near)){reserved=null;Refresh(p);return true;}
                        else{W.Exit(reserved,p);reserved=null;note="Bring your read Stone Spell and the warrior's quest belongings.";}}
                }else if(request.OptionID==(chang?142:2))note=S.Recover(p,near)?"Keep these belongings close. Lost fangs must be gathered again.":"Check your bag and storage first.";
                var dialog=new Npcs(p);dialog.Text(note??Hint(S.Load(p)));dialog.Option(npc==Column?"Enter the Crypt.":chang && S.Load(p).Stage==10?"Claim my reward.":"About the Crypt.",(byte)(chang?141:1));dialog.Option("I lost my quest belongings.",(byte)(chang?142:2));dialog.Option("Farewell.",255);dialog.Send();Refresh(p);
            }}catch(Exception ex){
                try{if(reserved!=null && !reserved.Released)lock(p.ItemSyncRoot){if(Q.Player(p,false))W.Exit(reserved,p);else if(p.Entity.MapID!=reserved.Map.ID)W.Release(reserved);}}catch(Exception cleanup){Q.Fail(p,"desert-entry-cleanup",cleanup);}
                Q.Fail(p,"desert-npc",ex);
            }finally{SessionInstall.Exit(p.Entity.UID);}return true;
        }
        internal static bool TryUse(ConquerItem item,GameState p){
            if(!Enabled || item==null || item.ID<721841 || item.ID>721846)return false;
            if(!S.Eligible(p) || !SessionInstall.TryEnter(p.Entity.UID))return true;
            try{lock(p.ItemSyncRoot){ConquerItem owned;if(!p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(item,owned))return true;
                if(item.ID==721843 || item.ID==721844)S.ReadItem(p,item.UID,()=>Enabled && S.Eligible(p));p.Send(Hint(S.Load(p)));Refresh(p);
            }}catch(Exception ex){Q.Fail(p,"desert-item",ex);}finally{SessionInstall.Exit(p.Entity.UID);}return true;
        }
        internal static void OnDeath(GameState p,Entity body){
            if(!Enabled || !S.Eligible(p) || body==null || body.MapID!=1000 || body.MonsterInfo==null || body.MonsterInfo.ID!=17 && body.MonsterInfo.ID!=76 || !SessionInstall.TryEnter(p.Entity.UID))return;
            try{lock(p.ItemSyncRoot)if(S.Hunt(p,body,()=>Enabled && S.Eligible(p)))Refresh(p);}catch(Exception ex){Q.Fail(p,"desert-hunt",ex);}finally{SessionInstall.Exit(p.Entity.UID);}
        }
        internal static bool Protect(uint id){return Enabled && id>=1800 && id<=1807;}
        internal static void OnLogin(GameState p){if(!Enabled || !Q.Player(p,false) || !SessionInstall.TryEnter(p.Entity.UID))return;
            try{W.RecoverLogin(p);W.Tick();W.RecoverLogin(p);Refresh(p);}catch(Exception ex){Q.Fail(p,"desert-login",ex);}finally{SessionInstall.Exit(p.Entity.UID);}}
        internal static void Refresh(GameState p){if(Enabled && Q.Player(p,false) && p.Quests!=null)p.Quests.SendFullGUI();}
        internal static void Fill(GameState p,List<MsgTaskStatus.QuestData> list,HashSet<uint> seen){
            if(!Enabled)return;for(uint id=1800;id<=1807;id++)seen.Add(id);if(!Q.Player(p,false) || p.Entity.Level<85 || p.Entity.Reborn>2)return;
            lock(p.ItemSyncRoot)try{var run=S.Load(p);int current=run.Stage==11?9:Math.Min(run.Stage,8);
                for(uint id=1800;id<=1807;id++){int step=(int)id-1799;var status=current>step?MsgTaskStatus.QuestData.QuestStatus.Finished:current==step?MsgTaskStatus.QuestData.QuestStatus.Accepted:MsgTaskStatus.QuestData.QuestStatus.Available;
                    list.Add(MsgTaskStatus.QuestData.Create((QuestID)id,status,0));}
            }catch(Exception ex){Q.Fail(p,"desert-journal",ex);}
        }
    }
}
