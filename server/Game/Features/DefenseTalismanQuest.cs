using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using S=Revora.Game.Features.DefenseTalismanStore;
using Scenes=Revora.Game.Features.DefenseTalismanScenes;
namespace Revora.Game.Features
{
    internal static class DefenseTalismanQuest
    {
        internal static bool Enabled {get;private set;}
        private sealed class Watch {internal int Stage;}
        private static readonly ConditionalWeakTable<GameState,Watch> watches=new ConditionalWeakTable<GameState,Watch>();
        private static DateTime nextTick;
        private static readonly uint[] Materials={721779,721778,721780,721781,721777,721782,721786,721784,721788,721789,721790,721793,721794,721792,721791,721785};
        private static readonly string[] MaterialNames={"Fishing Net","Fresh Fish","Iron Poise","Shawn's Spear","Tower Splinter","Handkerchief","Invocation","Glitter Sword","Annatto Blade","Wasp Spear","Infernal Axe","Metal Clinker","Wood Clinker","Earth Clinker","Fire Clinker","Star Tower"};
        private static void Npc(uint id,ushort map,ushort x,ushort y,ushort mesh,string name){
            Map world;if(!Kernel.Maps.TryGetValue(map,out world))throw new InvalidOperationException("Tower NPC map missing.");
            Revora.Interfaces.INpc existing;if(world.Npcs.TryGetValue(id,out existing)){
                if(existing.X!=x || existing.Y!=y || existing.Mesh/10!=mesh/10)throw new InvalidOperationException("Tower NPC conflict.");return;
            }
            if(!world.Floor[x,y,MapObjectType.Player])throw new InvalidOperationException("Tower NPC terrain blocked.");
            CityGuardSystem.SpawnTalker(id,map,x,y,mesh,name,(byte)(mesh%10));
        }
        private static void Relocate(uint id,ushort oldMap,ushort oldX,ushort oldY,ushort map,ushort x,ushort y,ushort mesh,string name){
            foreach(var m in Kernel.Maps.Values){Revora.Interfaces.INpc n;if(!m.Npcs.TryGetValue(id,out n))continue;
                if(m.ID==map && n.X==x && n.Y==y && n.Mesh/10==mesh/10)continue;
                if(m.ID!=oldMap || n.X!=oldX || n.Y!=oldY || n.Mesh/10!=mesh/10)throw new InvalidOperationException("Tower legacy NPC changed.");
                m.RemoveNpc(n);
            }Npc(id,map,x,y,mesh,name);
        }
        internal static void Initialize(){
            if(Enabled)return;
            for(int id=1782;id<=1784;id++)if(!File.Exists(Path.Combine(Constants.DMapsPath,"maps",id+".map"))){Console.WriteLine("[quest] Defense Talisman pending staged house scenes.");return;}
            Scenes.ValidateProfiles();S.Initialize();Scenes.InitializeTravel();Scenes.RecoverInterrupted();
            Npc(4565,1015,685,599,3654,"SugarTang");Npc(4566,1015,732,516,3654,"YangYun");
            Npc(4572,1000,472,578,3676,"Plum");Npc(4573,1000,85,313,3694,"GhoulKong");
            Relocate(4569,1015,192,212,1015,324,400,3686,"YeSheng");Relocate(4579,1015,189,183,1015,325,371,3786,"NetPeddler");
            Relocate(4575,1015,208,216,1015,340,404,3740,"TimeDoor");Relocate(4576,1015,222,214,1015,354,402,3750,"SeasonDoor");Relocate(4577,5000,150,95,1015,362,397,3760,"DreamDoor");
            for(uint id=520;id<=528;id++)QuestInfo.Register((QuestID)id,QuestType.EquipmentBonus);Enabled=true;
        }
        internal static void AddSceneNpcs(Scenes.Scene s){if(!Enabled)return;
            ushort b=s.Map.BaseID,m=s.Map.ID;
            if(b==1782)Npc(4567,m,51,70,3654,"YangYun");else if(b==1784)Npc(4568,m,55,55,3676,"RuanBrother");
            else if(b==1783)Npc(4574,m,52,50,3700,"InvocationStone");else if(b==1787)Npc(4578,m,48,37,3770,"Cauldron");
        }
        private static bool Near(GameState p,uint npc){
            if(!Enabled || !S.Eligible(p))return false;
            bool indoors=npc==4567 || npc==4568 || npc==4574 || npc==4578;
            if(indoors){var scene=Scenes.Find(p);return scene!=null && scene.Entered && !scene.Completed && Scenes.Registered(scene,p) && S.Clock()<scene.Until && Q.Near(p,npc,scene.Map.ID);}
            return Q.Near(p,npc,npc==4572 || npc==4573?(ushort)1000:(ushort)1015);
        }
        private static string Hint(S.Run r){switch(r.Stage){
            case 0:return "Bandits have stolen our family's tower. Will you help us?";
            case 1:return "Clear the five bandits, then speak to Yang Yun inside the house.";
            case 2:return "Speak to Yang Yun on Bird Island, at 732,516.";
            case 3:return "Net Peddler sells nets for 30,000 silver. Fish at 310,380; Ye Sheng needs three fish. Then visit Time, Season and Dream doors.";
            case 4:return "Ruan Brother wants Shawn's Spear. Shawn appears near 400,650 every ten minutes.";
            case 5:return "Plum in Desert City knows of another splinter. Find her at 472,578.";
            case 6:return r.DesertStep<2?"Hunt the Condor and Caprice Leader near 166,226 in the desert.":"Show the handkerchief to Yang Yun at 732,516.";
            case 7:return "Clear the five rats in Yang Yun's house. Collect the invocation at 52,50.";
            case 8:return r.DungeonFloor==0?"Ghoul Kong at 85,313 guards the dungeon. Show him the invocation.":r.DungeonFloor<3?"Defeat both generals. Use the invocation at "+(r.DungeonFloor==1?"70,56":"62,77")+" to descend. Elsewhere, it returns you safely.":"Defeat Yang Feng, then bring the weapons and splinters to the Cauldron at 48,37.";
            case 9:return "Bring the restored tower to Sugar Tang at 685,599. Leave one free space for your reward.";
            default:return "Our family's tower is safe again. You have my thanks.";
        }}
        private static void Visit(GameState p,int kind,Func<bool> context){
            if(Scenes.HasOwner(p.Entity.UID))return;
            var s=kind==1?Scenes.Create(p):kind==4?Scenes.CreateHut(p):kind==7?Scenes.CreateAmbush(p):Scenes.CreateDungeon(p);
            try{if(!Scenes.Enter(s,p,context))Scenes.Exit(s,p);}catch{if(!s.Released)Scenes.Exit(s,p);throw;}
        }
        private static void FinishScene(GameState p){var s=Scenes.Find(p);if(s!=null && s.Completed)Scenes.Exit(s,p);}
        internal static bool HandleNpc(GameState p,NpcRequest request){
            if(p==null || request==null || p.ActiveNpc<4565 || p.ActiveNpc>4579 || p.ActiveNpc==4570 || p.ActiveNpc==4571)return false;
            if(!Enabled)return true;uint npc=p.ActiveNpc;if(!Near(p,npc) || request.NpcID!=0 && request.NpcID!=npc || !SessionInstall.TryEnter(p.Entity.UID))return true;
            try{lock(p.ItemSyncRoot){Func<bool> near=()=>Near(p,npc);var r=S.Load(p);byte option=request.OptionID;string note=null;
                if(option==1){
                    if(npc==4565){if(r.Stage==0)S.Accept(p,near);else if(r.Stage==1)Visit(p,1,near);else if(r.Stage==9)S.Claim(p,near);}
                    else if(npc==4566){if(r.Stage==1)Visit(p,1,near);else if(r.Stage==2)S.FishingReferral(p,near);else if(r.Stage==6)S.ReportHandkerchief(p,near);else if(r.Stage==7)Visit(p,7,near);}
                    else if(npc==4567)S.HouseReport(p,near);
                    else if(npc==4568){if(!r.SpearRequested)S.RequestSpear(p,near);else S.ExchangeSpear(p,near);}
                    else if(npc==4569)S.ExchangeFish(p,near);else if(npc==4579)S.BuyNet(p,near);
                    else if(npc==4572)S.PlumReferral(p,near);else if(npc==4573 && S.PrepareDungeon(p,near))Visit(p,8,near);
                    else if(npc==4574)S.ClaimInvocation(p,near);else if(npc==4578)S.Reconstruct(p,near);
                    else if(npc>=4575 && npc<=4577){if(r.Stage==3)S.OpenDoor(p,(int)npc-4575,near);if(S.Load(p).Stage==4)Visit(p,4,near);}
                }else if(option==2 && (npc==4567 || npc==4568 || npc==4574 || npc==4578)){Scenes.Exit(Scenes.Find(p),p);Refresh(p);return true;}
                else if(npc==4578 && option>=10 && option<=13)S.Smelt(p,option-10,near);
                else if(option>=40 && option<40+Materials.Length)note=S.Recover(p,Materials[option-40],near)?"Keep it safe this time.":"Check your pack and storage. I can only replace a lost item.";
                FinishScene(p);Refresh(p);if(!Near(p,npc))return true;r=S.Load(p);var d=new Npcs(p);d.Text(note??Hint(r));
                if(npc==4579)d.Option("Buy a net: 30,000 silver.",1);else d.Option(r.Stage==9 && npc==4565?"Claim my reward.":"I am ready.",1);
                if(npc==4578){string[] names={"Glitter Sword","Annatto Blade","Wasp Spear","Infernal Axe"};for(int i=0;i<4;i++)d.Option("Smelt "+names[i]+".",(byte)(10+i));}
                if(npc==4567 || npc==4568 || npc==4574 || npc==4578)d.Option("Let me leave.",2);
                if(option==30 || option==31){int start=option==30?0:8;for(int i=start;i<Math.Min(start+8,Materials.Length);i++)d.Option("Lost "+MaterialNames[i]+".",(byte)(40+i));d.Option("Other lost items.",(byte)(option==30?31:30));}
                else if(r.Stage>0 && r.Stage<10)d.Option("I lost a quest item.",30);
                if(npc==4565 && AttackTalismanQuest.Enabled)d.Option("About the Heaven Fan.",80);
                d.Option("Farewell.",255);d.Send();
            }}catch(Exception ex){Q.Fail(p,"tower-npc",ex);}finally{SessionInstall.Exit(p.Entity.UID);}return true;
        }
        internal static bool TryUse(ConquerItem item,GameState p){
            if(item==null || !Materials.Contains(item.ID))return false;if(!Enabled || !S.Eligible(p) || !SessionInstall.TryEnter(p.Entity.UID))return true;
            try{lock(p.ItemSyncRoot){ConquerItem owned;if(!p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(owned,item))return true;
                if(item.ID==721779)DefenseTalismanEncounters.Start(p);
                else if(item.ID==721780){for(int site=0;site<3;site++)if(S.OpenDoor(p,site,()=>Enabled && S.Eligible(p)))break;}
                else if(item.ID==721786 && Scenes.Contains(p.Entity.MapID)){
                    // At a reviewed gate, advance; elsewhere this key returns its owner safely.
                    var scene=Scenes.Find(p);bool gate=scene!=null && (scene.Map.BaseID==1785 && Kernel.GetDistance(p.Entity.X,p.Entity.Y,70,56)<=6 || scene.Map.BaseID==1786 && Kernel.GetDistance(p.Entity.X,p.Entity.Y,62,77)<=6);
                    if(gate)Scenes.Descend(p,()=>Enabled && S.Eligible(p));else if(scene!=null)Scenes.Exit(scene,p);
                }
                Refresh(p);p.Send(Hint(S.Load(p)));
            }}catch(Exception ex){Q.Fail(p,"tower-item",ex);}finally{SessionInstall.Exit(p.Entity.UID);}return true;
        }
        internal static bool Protect(uint id){return id>=520 && id<=528;}
        internal static void Refresh(GameState p){if(Enabled && Q.Player(p,false)){var w=watches.GetValue(p,_=>new Watch());w.Stage=S.Load(p).Stage;if(p.Quests!=null)p.Quests.SendFullGUI();}}
        internal static void OnLogin(GameState p){if(!Enabled || !Q.Player(p,false) || !SessionInstall.TryEnter(p.Entity.UID))return;
            try{lock(p.ItemSyncRoot){Scenes.RecoverLogin(p);Refresh(p);}}catch(Exception ex){Q.Fail(p,"tower-login",ex);}finally{SessionInstall.Exit(p.Entity.UID);}}
        internal static void Tick(){if(!Enabled)return;var now=S.Clock();if(now<nextTick)return;nextTick=now.AddSeconds(1);Scenes.Tick();DefenseTalismanEncounters.Tick();
            foreach(var p in Kernel.GamePool.Values){Watch w;if(!S.Eligible(p) || !watches.TryGetValue(p,out w) || w.Stage==0 || w.Stage>=9 || DefenseTalismanEncounters.HasOwner(p.Entity.UID))continue;
                bool near=Scenes.Contains(p.Entity.MapID) || w.Stage==4 && p.Entity.MapID==1015 && Kernel.GetDistance(p.Entity.X,p.Entity.Y,400,650)<=8 || w.Stage==6 && p.Entity.MapID==1000 && Kernel.GetDistance(p.Entity.X,p.Entity.Y,166,226)<=8;
                if(!near)continue;try{DefenseTalismanEncounters.Start(p);}catch(Exception ex){Q.Fail(p,"tower-spawn",ex);}
            }
        }
        internal static void Fill(GameState p,List<MsgTaskStatus.QuestData> list,HashSet<uint> seen){for(uint id=520;id<=528;id++)seen.Add(id);if(!Enabled || !Q.Player(p,false) || p.Entity.Level<100 || p.Entity.Reborn>2)return;
            lock(p.ItemSyncRoot)try{var r=S.Load(p);watches.GetValue(p,_=>new Watch()).Stage=r.Stage;
                for(uint id=520;id<=528;id++){int step=(int)id-519;list.Add(MsgTaskStatus.QuestData.Create((QuestID)id,r.Stage>step?MsgTaskStatus.QuestData.QuestStatus.Finished:r.Stage==step?MsgTaskStatus.QuestData.QuestStatus.Accepted:MsgTaskStatus.QuestData.QuestStatus.Available,0));}
            }catch(Exception ex){Q.Fail(p,"tower-journal",ex);}
        }
    }
}
