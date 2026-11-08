using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using S=Revora.Game.Features.AttackTalismanStore;
using Scenes=Revora.Game.Features.AttackTalismanScenes;
namespace Revora.Game.Features
{
    internal static class AttackTalismanQuest
    {
        internal static bool Enabled {get;private set;}
        internal const uint Shore=9910430;
        private sealed class Watch {internal int Stage,Site;}
        private static readonly ConditionalWeakTable<GameState,Watch> watches=new ConditionalWeakTable<GameState,Watch>();
        private static DateTime nextTick;
        private static readonly uint[] Materials={710218,721800,710220,721798,721803,721806,721802,721804,721801,721799,710219,710216,710217,710221,710222};
        private static readonly string[] Names={"Jadeite","Bamboo Wine","Snake Meat","Tang Silk","Poison Fang","Bee Kettle","Kunlun Liquid","Sacred Water","Poem Silk","Incense","Hairpin","Purple Eye","Jade Scepter","quest fan","Poem Book"};
        private static void Npc(uint id,ushort map,ushort x,ushort y,ushort mesh,string name){
            Map world;if(!Kernel.Maps.TryGetValue(map,out world))throw new InvalidOperationException("Fan public map missing.");Revora.Interfaces.INpc existing;
            if(world.Npcs.TryGetValue(id,out existing)){if(existing.X!=x || existing.Y!=y || existing.Mesh/10!=mesh/10)throw new InvalidOperationException("Fan NPC conflict.");return;}
            if(!world.Floor[x,y,MapObjectType.Player])throw new InvalidOperationException("Fan NPC terrain blocked.");CityGuardSystem.SpawnTalker(id,map,x,y,mesh,name,(byte)(mesh%10));
        }
        private static void RetirePillar(uint id,ushort x,ushort y,ushort mesh){Map map;if(!Kernel.Maps.TryGetValue(1015,out map))throw new InvalidOperationException("Fan pillar map missing.");Revora.Interfaces.INpc npc;
            if(!map.Npcs.TryGetValue(id,out npc))return;if(npc.X!=x || npc.Y!=y || npc.Mesh/10!=mesh/10)throw new InvalidOperationException("Fan legacy pillar changed.");map.RemoveNpc(npc);
        }
        internal static void Initialize(){
            if(Enabled || !DefenseTalismanQuest.Enabled)return;
            foreach(int id in new[]{1025,1011,3937})if(!File.Exists(Path.Combine(Constants.DMapsPath,"maps",id+".map"))){Console.WriteLine("[quest] Attack Talisman pending reviewed scenes.");return;}
            Scenes.ValidateProfiles();S.Initialize();Scenes.InitializeTravel();Scenes.RecoverInterrupted();
            Npc(4582,1011,788,456,3600,"VillagerChou");Npc(4581,1011,182,280,3600,"WineKiddo");Npc(4583,1000,490,618,3694,"KunlunWanderer");Npc(Shore,1011,469,840,3694,"LakeKeeper");
            RetirePillar(4587,653,700,3500);RetirePillar(4588,664,691,3510);RetirePillar(4591,656,693,3540);
            for(int i=0;i<4;i++)Npc(S.PillarStart+(uint)i,1015,S.PillarPoints[i,0],S.PillarPoints[i,1],3500,new[]{"SparrowPillar","DragonPillar","TigerPillar","LeopardPillar"}[i]);Npc(S.Platform,1015,686,723,3540,"GardenPlatform");S.InstallPillars();
            for(uint id=400;id<=409;id++)QuestInfo.Register((QuestID)id,QuestType.EquipmentBonus);Enabled=true;
        }
        private static bool OwnedNpc(uint npc){return npc==4581 || npc==4582 || npc==4583 || npc==Shore || npc==Scenes.Herbalist || npc>=S.PillarStart && npc<=S.Platform || npc>=S.BoxStart && npc<=S.Loneliness;}
        private static bool Near(GameState p,uint npc){
            if(!Enabled || !S.Eligible(p))return false;
            if(npc==Scenes.Herbalist || npc>=S.BoxStart && npc<=S.Loneliness){var scene=Scenes.Find(p);return scene!=null && scene.Entered && !scene.Completed && Scenes.Registered(scene,p) && S.Clock()<scene.Until && Q.Near(p,npc,scene.Map.ID);}
            return Q.Near(p,npc,npc==4583?(ushort)1000:npc==4565 || npc>=S.PillarStart && npc<=S.Platform?(ushort)1015:(ushort)1011);
        }
        private static string Hint(S.Run r){switch(r.Stage){
            case 0:return "Help my family recover the Star Tower first. Then we can seek the Heaven Fan.";
            case 1:return "Show Villager Chou your Jadeite. He waits at Phoenix Castle, 788,456.";
            case 2:return "Chou wants Bamboo Wine and five pieces of Winged Snake meat. Wine Kiddo sells wine at 182,280 for 10,000 silver.";
            case 3:return "Bring the Tang Silk to Sugar Tang on Bird Island, 685,599.";
            case 4:return "Find Kunlun Wanderer in Desert City, 490,618.";
            case 5:return r.Alchemy==0?"The cave permit costs 10,000 silver. Slay its Viper and Brigand, use the Bee Kettle to return, then ask Kunlun for liquid.":r.Alchemy==1?"Use the Poison Fang or Kunlun Liquid to mix Sacred Water.":"Use Sacred Water to reveal the writing on Tang Silk.";
            case 6:return "Show the Poem Silk to Villager Chou at 788,456.";
            case 7:return "Defeat the five Crimson Claws near 788,460, then speak to Chou.";
            case 8:return r.LakeStep==3?"Bring the Purple Eye back to Chou.":"Speak to the Lake Keeper at 469,840. Burn incense at 475,844 inside, speak to the spirit at 475,840, and defeat the Viper.";
            case 9:return r.Pillars==15?"The four pillars are awake. Speak to the Garden Platform at 686,723.":"Find Jade Courtesan near "+S.CourtesanSites[Math.Max(0,r.CourtesanSite),0]+","+S.CourtesanSites[Math.Max(0,r.CourtesanSite),1]+". Offer your four gems to the pillars around 686,723.";
            case 10:return r.BookGiven?"Bring the Poem Book to Sugar Tang. Leave one free space for your reward.":r.PendingGhost>=0?"Defeat the ghost before opening another chest.":r.FanTaken?"Bring the fan to Mr. Loneliness at 60,64.":"Choose a treasure box. Beware the ghosts hidden inside.";
            default:return "The Heaven Fan is yours. May it serve you well.";
        }}
        private static void Visit(GameState p,int kind,Func<bool> context){if(Scenes.HasOwner(p.Entity.UID))return;var scene=kind==5?Scenes.Create(p):kind==8?Scenes.CreateLake(p):Scenes.CreateGarden(p);
            try{if(!Scenes.Enter(scene,p,context))Scenes.Exit(scene,p);}catch{if(!scene.Released)Scenes.Exit(scene,p);throw;}
        }
        private static void FinishScene(GameState p){var scene=Scenes.Find(p);if(scene!=null && scene.Completed)Scenes.Exit(scene,p);}
        internal static bool HandleNpc(GameState p,NpcRequest request){
            if(p==null || request==null)return false;uint npc=p.ActiveNpc;bool sugar=npc==4565;
            if(!OwnedNpc(npc) && !(sugar && request.OptionID>=80 && request.OptionID<=114))return false;
            if(!Enabled || !Near(p,npc) || request.NpcID!=0 && request.NpcID!=npc || !SessionInstall.TryEnter(p.Entity.UID))return true;
            try{lock(p.ItemSyncRoot){Func<bool> near=()=>Near(p,npc);var r=S.Load(p);byte option=request.OptionID;string note=null;
                if(option==(sugar?81:1)){
                    if(sugar){if(r.Stage==0)S.Accept(p,near);else if(r.Stage==3)S.Referral(p,3,near);else if(r.Stage==10)S.Claim(p,near);}
                    else if(npc==4582){if(r.Stage==1)S.Referral(p,1,near);else if(r.Stage==2)S.ExchangeMeal(p,near);else if(r.Stage==6)S.ReportPoem(p,near);else if(r.Stage==7)S.ClaimIncense(p,near);else if(r.Stage==8)S.ReportLake(p,near);}
                    else if(npc==4581)S.BuyWine(p,near);
                    else if(npc==4583){if(r.Stage==4)S.Referral(p,4,near);else if(r.Stage==5)S.ClaimLiquid(p,near);}
                    else if(npc==Shore && r.Stage==8 && r.LakeStep<3)Visit(p,8,near);
                    else if(npc==Scenes.Herbalist)S.SpeakHerbalist(p,near);
                    else if(npc>=S.PillarStart && npc<S.Platform)S.OfferPillar(p,(int)(npc-S.PillarStart),near);
                    else if(npc==S.Platform && (r.Stage==9 || r.Stage==10) && r.Pillars==15 && !r.BookGiven)Visit(p,10,near);
                    else if(npc>=S.BoxStart && npc<S.Loneliness)S.OpenBox(p,(int)(npc-S.BoxStart),near);
                    else if(npc==S.Loneliness)S.ExchangeBook(p,near);
                }else if(option==2 && npc==4583 && r.Stage==5)Visit(p,5,near);
                else if(option==2 && (npc==Scenes.Herbalist || npc>=S.BoxStart && npc<=S.Loneliness)){Scenes.Exit(Scenes.Find(p),p);Refresh(p);return true;}
                else if(option>=100 && option<100+Materials.Length)note=S.Recover(p,Materials[option-100],near)?"Keep it safe this time.":"Check your pack and storage. I can only replace a lost item.";
                FinishScene(p);Refresh(p);if(!Near(p,npc))return true;r=S.Load(p);var d=new Npcs(p);d.Text(note??Hint(r));
                d.Option(npc==4581?"Buy wine: 10,000 silver.":r.BookGiven && sugar?"Claim my reward.":"I am ready.",(byte)(sugar?81:1));
                if(npc==4583 && r.Stage==5)d.Option(r.CavePaid?"Return to the cave.":"Enter cave: 10,000 silver.",2);
                if(npc==Scenes.Herbalist || npc>=S.BoxStart && npc<=S.Loneliness)d.Option("Let me leave.",2);
                if(option==90 || option==91){int start=option==90?0:8;for(int i=start;i<Math.Min(start+8,Materials.Length);i++)d.Option("Lost "+Names[i]+".",(byte)(100+i));d.Option("Other lost items.",(byte)(option==90?91:90));}
                else if(r.Stage>0 && r.Stage<11)d.Option("I lost a quest item.",90);
                d.Option("Farewell.",255);d.Send();
            }}catch(Exception ex){Q.Fail(p,"fan-npc",ex);}finally{SessionInstall.Exit(p.Entity.UID);}return true;
        }
        internal static bool TryUse(ConquerItem item,GameState p){if(item==null || !Materials.Contains(item.ID))return false;if(!Enabled || !S.Eligible(p) || !SessionInstall.TryEnter(p.Entity.UID))return true;
            try{lock(p.ItemSyncRoot){ConquerItem owned;if(!p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(owned,item))return true;Func<bool> context=()=>Enabled && S.Eligible(p);
                if(item.ID==721806)S.UseKettle(p,context);else if(item.ID==721802 || item.ID==721803 || item.ID==721804)S.Transform(p,item.UID,context);else if(item.ID==721799)S.BurnIncense(p,item.UID,context);
                FinishScene(p);Refresh(p);p.Send(Hint(S.Load(p)));
            }}catch(Exception ex){Q.Fail(p,"fan-item",ex);}finally{SessionInstall.Exit(p.Entity.UID);}return true;
        }
        internal static void OnDeath(GameState p,Entity body){if(!Enabled || !S.Eligible(p) || body==null || body.MonsterInfo==null || (body.MonsterInfo.ID!=6 && body.MonsterInfo.ID!=65) || !SessionInstall.TryEnter(p.Entity.UID))return;
            try{lock(p.ItemSyncRoot){S.Hunt(p,body,()=>Enabled && S.Eligible(p));Refresh(p);}}catch(Exception ex){Q.Fail(p,"fan-hunt",ex);}finally{SessionInstall.Exit(p.Entity.UID);}
        }
        internal static bool Protect(uint id){return id>=400 && id<=409;}
        internal static void Refresh(GameState p){if(Enabled && Q.Player(p,false)){var run=S.Load(p);var watch=watches.GetValue(p,_=>new Watch());watch.Stage=run.Stage;watch.Site=run.CourtesanSite;if(p.Quests!=null)p.Quests.SendFullGUI();}}
        internal static void OnLogin(GameState p){if(!Enabled || !Q.Player(p,false) || !SessionInstall.TryEnter(p.Entity.UID))return;try{lock(p.ItemSyncRoot){Scenes.RecoverLogin(p);Refresh(p);}}catch(Exception ex){Q.Fail(p,"fan-login",ex);}finally{SessionInstall.Exit(p.Entity.UID);}}
        internal static void Tick(){if(!Enabled)return;var now=S.Clock();if(now<nextTick)return;nextTick=now.AddSeconds(1);Scenes.Tick();AttackTalismanEncounters.Tick();
            foreach(var p in Kernel.GamePool.Values){Watch w;if(!S.Eligible(p) || !watches.TryGetValue(p,out w) || w.Stage==0 || w.Stage>=11 || AttackTalismanEncounters.HasOwner(p.Entity.UID))continue;
                bool near=Scenes.Contains(p.Entity.MapID) || w.Stage==7 && p.Entity.MapID==1011 && Kernel.GetDistance(p.Entity.X,p.Entity.Y,788,460)<=8 || w.Stage==9 && w.Site>=0 && w.Site<4 && p.Entity.MapID==1015 && Kernel.GetDistance(p.Entity.X,p.Entity.Y,S.CourtesanSites[w.Site,0],S.CourtesanSites[w.Site,1])<=8;
                if(near)try{AttackTalismanEncounters.Start(p);}catch(Exception ex){Q.Fail(p,"fan-spawn",ex);}
            }
        }
        internal static void Fill(GameState p,List<MsgTaskStatus.QuestData> list,HashSet<uint> seen){for(uint id=400;id<=409;id++)seen.Add(id);if(!Enabled || !Q.Player(p,false) || p.Entity.Level<100 || p.Entity.Reborn>2)return;
            lock(p.ItemSyncRoot)try{var run=S.Load(p);var w=watches.GetValue(p,_=>new Watch());w.Stage=run.Stage;w.Site=run.CourtesanSite;
                for(uint id=400;id<=409;id++){int step=(int)id-399;list.Add(MsgTaskStatus.QuestData.Create((QuestID)id,run.Stage>step?MsgTaskStatus.QuestData.QuestStatus.Finished:run.Stage==step?MsgTaskStatus.QuestData.QuestStatus.Accepted:MsgTaskStatus.QuestData.QuestStatus.Available,0));}
            }catch(Exception ex){Q.Fail(p,"fan-journal",ex);}
        }
    }
}
