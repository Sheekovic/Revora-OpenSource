using System;
using System.Collections.Generic;
using System.IO;
using Revora.Client;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using S=Revora.Game.Features.TempestWingsStore;
using Scenes=Revora.Game.Features.TempestWingsScenes;
namespace Revora.Game.Features
{
    internal static class TempestWingsQuest
    {
        internal const uint Chang=8796,Wonder=10850;
        internal static bool Enabled {get;private set;}
        private static DateTime nextTick;
        private static void Npc(uint id,ushort map,ushort x,ushort y,ushort mesh,string name){
            Map world;if(!Kernel.Maps.TryGetValue(map,out world) || world.BaseID!=map)throw new InvalidOperationException("Tempest public map missing.");
            Revora.Interfaces.INpc existing;
            if(world.Npcs.TryGetValue(id,out existing)){
                if(existing.MapID!=map || existing.X!=x || existing.Y!=y || existing.Mesh/10!=mesh/10)throw new InvalidOperationException("Tempest NPC conflict.");return;
            }
            foreach(var actor in world.Npcs.Values)if(actor.X==x && actor.Y==y)throw new InvalidOperationException("Tempest NPC cell occupied.");
            if(!world.Floor[x,y,MapObjectType.Player])throw new InvalidOperationException("Tempest NPC terrain blocked.");
            CityGuardSystem.SpawnTalker(id,map,x,y,mesh,name,0);
        }
        internal static void Initialize(){
            if(Enabled)return;
            if(!File.Exists(Path.Combine(Constants.DMapsPath,"maps","tempest-1100.map"))){Console.WriteLine("[quest] Tempest Wings pending reviewed private terrain.");return;}
            Scenes.ValidateProfiles();TempestWingsEncounters.Profile();S.Initialize();Scenes.InitializeTravel();S.InitializeEncounter();Scenes.RecoverInterrupted();
            // Explicit Revora appearance reuse; original NPC meshes are not verified.
            Npc(Chang,1004,51,49,12090,"TaskmasterChang");Npc(Wonder,1011,212,196,3580,"MrWonder");
            QuestInfo.Register((QuestID)200,QuestType.EquipmentBonus);Enabled=true;
        }
        private static bool Near(GameState p,uint npc){
            if(!Enabled || !S.Eligible(p))return false;
            if(npc==Scenes.ExitNpc)return S.InScene(p) && Q.Near(p,npc,p.Entity.MapID);
            return Q.Near(p,npc,npc==Chang?(ushort)1004:(ushort)1011);
        }
        private static string Hint(int stage){switch(stage){
            case 0:return "Seek the Roc above Phoenix Castle. Mr. Wonder can send you skyward.";
            case 1:return "Bring five Meteor Scrolls to Mr. Wonder in Phoenix Castle, 212,196.";
            case 2:return "Your passage is paid. Defeat the Roc and bring me its feather.";
            case 3:return "The Roc has fallen. Bring its feather and leave one space for your wing and bound P7 soul.";
            default:return "The Tempest Wing is yours. May the winds carry you safely.";
        }}
        internal static bool HandleNpc(GameState p,NpcRequest request){
            if(p==null || request==null)return false;uint npc=p.ActiveNpc;
            // Retire the untracked legacy CP/random-reward route, even while staged.
            if(npc==600065 || npc==4482)return true;
            if(npc!=Chang && npc!=Wonder && npc!=Scenes.ExitNpc)return false;
            if(!Near(p,npc) || request.NpcID!=0 && request.NpcID!=npc || !SessionInstall.TryEnter(p.Entity.UID))return true;
            try{lock(p.ItemSyncRoot){Func<bool> near=()=>Near(p,npc);var run=S.Load(p);byte option=request.OptionID;
                if(npc==Scenes.ExitNpc){if(option==1){Scenes.Exit(Scenes.Find(p),p);Refresh(p);return true;}}
                else if(option==1 && npc==Chang && run.Stage==0)S.Accept(p,near);
                else if(option==1 && npc==Wonder && (run.Stage==1 || run.Stage==2)){
                    // Allocate only after the saved permit exists; failed travel never charges again.
                    if(!Scenes.HasOwner(p.Entity.UID) && S.Permit(p,near)){
                        var scene=Scenes.Create(p);try{if(!Scenes.Enter(scene,p,near))Scenes.Exit(scene,p);}catch{if(!scene.Released)Scenes.Exit(scene,p);throw;}
                    }
                }
                else if(npc==Wonder && run.Stage==3){if(option==2)S.Feather(p,near);else if(option==3)S.Claim(p,near);}
                Refresh(p);if(!Near(p,npc))return true;run=S.Load(p);var d=new Npcs(p);
                d.Text(npc==Scenes.ExitNpc?"The wind is turning. Shall I take you back to Phoenix Castle?":Hint(run.Stage));
                if(npc==Scenes.ExitNpc)d.Option("Take me back.",1);
                else if(npc==Chang && run.Stage==0)d.Option("I will seek the Roc.",1);
                else if(npc==Wonder && run.Stage==1)d.Option("Passage: five Meteor Scrolls.",1);
                else if(npc==Wonder && run.Stage==2)d.Option("Return to the sky.",1);
                else if(npc==Wonder && run.Stage==3){d.Option("Recover my earned feather.",2);d.Option("Claim my reward.",3);}
                d.Option("Farewell.",255);d.Send();
            }}catch(Exception ex){Q.Fail(p,"tempest-npc",ex);}finally{SessionInstall.Exit(p.Entity.UID);}return true;
        }
        internal static bool Portal(GameState p,Data packet){
            if(p==null || p.Entity==null || !Scenes.Contains(p.Entity.MapID))return false;
            if(!Enabled || !S.Eligible(p) || packet==null || !SessionInstall.TryEnter(p.Entity.UID))return true;
            try{lock(p.ItemSyncRoot){
                if(S.InScene(p) && (ushort)packet.dwParam==190 && (ushort)(packet.dwParam>>16)==162 && Kernel.GetDistance(p.Entity.X,p.Entity.Y,190,162)<=4){Scenes.Exit(Scenes.Find(p),p);Refresh(p);}
            }}catch(Exception ex){Q.Fail(p,"tempest-portal",ex);}finally{SessionInstall.Exit(p.Entity.UID);}return true;
        }
        internal static bool Protect(uint id){return id==200 || id==3429;}
        internal static void Refresh(GameState p){if(Enabled && Q.Player(p,false) && p.Quests!=null)p.Quests.SendFullGUI();}
        internal static void OnLogin(GameState p){if(!Enabled || !Q.Player(p,false) || !SessionInstall.TryEnter(p.Entity.UID))return;
            try{lock(p.ItemSyncRoot){Scenes.RecoverLogin(p);Refresh(p);}}catch(Exception ex){Q.Fail(p,"tempest-login",ex);}finally{SessionInstall.Exit(p.Entity.UID);}}
        internal static void Tick(){
            if(!Enabled)return;var now=S.Clock();if(now<nextTick)return;nextTick=now.AddSeconds(1);Scenes.Tick();TempestWingsEncounters.Tick();
            foreach(var scene in Scenes.Active()){
                var p=scene.Owner;if(!S.Eligible(p) || !scene.Entered || scene.Completed || TempestWingsEncounters.HasOwner(p.Entity.UID))continue;
                try{TempestWingsEncounters.Start(p);}catch(Exception ex){Q.Fail(p,"tempest-spawn",ex);}
            }
        }
        internal static void Fill(GameState p,List<MsgTaskStatus.QuestData> list,HashSet<uint> seen){
            seen.Add(200);seen.Add(3429);if(!Enabled || !Q.Player(p,false) || p.Entity.Reborn>2 || p.Entity.Level<100 && p.Entity.Reborn==0)return;
            lock(p.ItemSyncRoot)try{var run=S.Load(p);list.Add(MsgTaskStatus.QuestData.Create((QuestID)200,run.Stage==4?MsgTaskStatus.QuestData.QuestStatus.Finished:run.Stage>0?MsgTaskStatus.QuestData.QuestStatus.Accepted:MsgTaskStatus.QuestData.QuestStatus.Available,0));}
            catch(Exception ex){Q.Fail(p,"tempest-journal",ex);}
        }
    }
}
