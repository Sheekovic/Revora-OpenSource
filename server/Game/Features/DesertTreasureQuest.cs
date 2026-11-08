using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using S=Revora.Game.Features.DesertTreasureStore;
namespace Revora.Game.Features
{
    internal static class DesertTreasureQuest
    {
        internal const uint Louis=4651,Shark=4652,Vault=9918281;
        internal static bool Enabled {get;private set;}
        private sealed class Watch {internal int Stage;}
        private static readonly ConditionalWeakTable<GameState,Watch> watches=new ConditionalWeakTable<GameState,Watch>();
        private static DateTime nextTick;
        private static void Npc(uint id,ushort mapId,ushort x,ushort y,ushort mesh,string name){
            Map map;if(!Kernel.Maps.TryGetValue(mapId,out map))throw new InvalidOperationException("Desert Treasure map missing.");
            foreach(var other in Kernel.Maps.Values){Revora.Interfaces.INpc npc;if(other.Npcs.TryGetValue(id,out npc) && (other.ID!=mapId || npc.X!=x || npc.Y!=y || npc.Mesh/10!=mesh/10))throw new InvalidOperationException("Desert Treasure NPC conflict.");}
            CityGuardSystem.SpawnTalker(id,mapId,x,y,mesh,name,0);
        }
        internal static void Initialize(){
            uint[] maps={1000,1001};string[] hashes={"c874aa5659420dfcf03545f1bfad67bea0197ae55fbc8140fcd7e85ffbae33f1","89bdcb2d247add85d8d7b888c8f90d7d4d5127e51499479ca988a0c9884624fc"};
            for(int i=0;i<maps.Length;i++)using(var sha=System.Security.Cryptography.SHA256.Create())using(var file=System.IO.File.OpenRead(System.IO.Path.Combine(Constants.DMapsPath,"maps",maps[i]+".map")))
                if(System.BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant()!=hashes[i])throw new InvalidOperationException("Desert Treasure terrain changed.");
            S.Initialize();Npc(Louis,1000,466,658,4080,"Louis");Npc(Shark,1001,310,631,4090,"Shark");Npc(Vault,1001,94,283,3830,"ScriptureVault");
            for(uint id=1828;id<=1834;id++)QuestInfo.Register((QuestID)id,QuestType.EquipmentBonus);Enabled=true;
        }
        private static bool Near(GameState p,uint npc){return Enabled && S.Eligible(p) && Q.Near(p,npc,npc==AzurePillQuest.Chang?(ushort)1002:npc==Louis?(ushort)1000:(ushort)1001);}
        private static string Hint(S.Run run){switch(run.Stage){
            case 0:case 1:return "Louis seeks three lost treasures. Find him in Desert City at 466,658.";
            case 2:return "Tomb Bats carry the key. Use the Reveal Vial near 91,283, step onto that spot, then open the vault beside it.";
            case 3:return "Shark has the portrait. Find him in Mystic Castle at 310,631.";
            case 4:return "Examine the portrait, then ask Shark why it is blank.";
            case 5:return "Gather five Bat Essences. Reveal the portrait beside Louis and give it to him within one minute.";
            case 6:return "Jinx waits near 728,676. Defeat it and use the Pure Bottle beside its corpse within three seconds.";
            case 7:return "The treasures are safe. Claim your reward from Taskmaster Chang.";
            default:return "You have recovered the desert's lost treasures. My thanks.";
        }}
        internal static bool HandleNpc(GameState p,NpcRequest request){
            if(!Enabled || p==null || request==null)return false;uint npc=p.ActiveNpc;bool chang=npc==AzurePillQuest.Chang;
            if(npc!=Louis && npc!=Shark && npc!=Vault && (!chang || request.OptionID<170 || request.OptionID>172))return false;
            if(!Near(p,npc) || request.NpcID!=0 && request.NpcID!=npc || !SessionInstall.TryEnter(p.Entity.UID))return true;
            try{lock(p.ItemSyncRoot){Func<bool> near=()=>Near(p,npc);var run=S.Load(p);byte option=request.OptionID;string note=null;
                if(npc==Vault){if(option==1)note=S.OpenVault(p,near)?"The compartment opens. Examine the box.":"The lock resists. Check your key, reveal the vault and step onto 91,283.";
                    var v=new Npcs(p);v.Text(note??"A sealed compartment lies beneath the stone.");v.Option("Turn the key.",1);v.Option("Leave it.",255);v.Send();Refresh(p);return true;}
                if(option==(chang?171:1)){
                    if(chang && run.Stage==0)S.Advance(p,0,near);else if(chang && run.Stage==7)S.Claim(p,near);
                    else if(npc==Louis && (run.Stage==1 || run.Stage==2 || run.Stage==5))S.Advance(p,run.Stage,near);
                    else if(npc==Louis && run.Stage==6)S.Report(p,near);else if(npc==Shark && run.Stage==4)S.Advance(p,4,near);
                }else if(npc==Shark && option>=10 && option<=12)note=S.Purchase(p,new[]{30000,60000,99999}[option-10],near)?"A fair bargain. The portrait is yours.":"No deal yet. Check your silver and pack, then wait a moment before bidding again.";
                else if(!chang && option>=30 && option<=38)note=S.Recover(p,721870u+(uint)(option-30),near)?"Here is your lost item. Keep it safe.":"Check your pack and storage. I can only replace what is truly lost.";
                if(npc==Louis && (option==50 || option>=60 && option<=68)){
                    if(option>=60)S.AnswerQuiz(p,(option-60)/3,(option-60)%3,near);
                    int q=S.QuizProgress(p);var quiz=new Npcs(p);quiz.Text(q<0?"First, bring me the relic.":q==3?"You remember well. May that knowledge serve you.":S.QuizQuestions[q]);
                    if(q>=0 && q<3)for(int a=0;a<3;a++)quiz.Option(S.QuizChoices[q][a],(byte)(60+q*3+a));quiz.Option("Farewell.",255);quiz.Send();Refresh(p);return true;
                }
                run=S.Load(p);var d=new Npcs(p);d.Text(note??Hint(run));d.Option(chang && run.Stage==7?"Claim my reward.":"About the treasures.",(byte)(chang?171:1));
                if(npc==Shark && run.Stage==3){d.Option("30,000 silver (30%).",10);d.Option("60,000 silver (60%).",11);d.Option("99,999 silver (certain).",12);}
                if(!chang && run.Stage>=2 && run.Stage<=6){if(option==20){string[] names={"Scripture","Real portrait","Relic","Reveal Vial","Pure Bottle","Scripture Box","Box Key","Bat Essence","Blank portrait"};for(int i=0;i<names.Length;i++)d.Option("Lost "+names[i]+".",(byte)(30+i));}else d.Option("I lost a quest item.",20);}
                if(npc==Louis && run.Stage>=7)d.Option("Test my memory.",50);d.Option("Farewell.",255);d.Send();Refresh(p);
            }}catch(Exception ex){Q.Fail(p,"treasure-npc",ex);}finally{SessionInstall.Exit(p.Entity.UID);}return true;
        }
        internal static bool TryUse(ConquerItem item,GameState p){
            if(!Enabled || item==null || item.ID<721870 || item.ID>721878)return false;if(!S.Eligible(p) || !SessionInstall.TryEnter(p.Entity.UID))return true;
            try{lock(p.ItemSyncRoot){ConquerItem owned;if(!p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(owned,item))return true;
                Func<bool> ready=()=>Enabled && S.Eligible(p);bool changed=false;var run=S.Load(p);
                if(item.ID==721873){changed=S.RevealVault(p,item.UID,ready);if(changed)p.Send("The seal stirs. Step onto 91,283, then open the vault beside it.");}
                else if(item.ID==721875)changed=S.OpenScripture(p,item.UID,ready);
                else if(item.ID==721878 && run.Stage==4)changed=S.InspectPortrait(p,item.UID,ready);
                else if(item.ID==721878 || item.ID==721871)changed=S.RevealPortrait(p,item.UID,ready);
                else if(item.ID==721874)changed=DesertTreasureEncounter.Capture(p,item.UID);
                p.Send(changed && (item.ID==721878 || item.ID==721871) && run.Stage==5?"The portrait is revealed. Give it to Louis within one minute.":Hint(S.Load(p)));Refresh(p);
            }}catch(Exception ex){Q.Fail(p,"treasure-item",ex);}finally{SessionInstall.Exit(p.Entity.UID);}return true;
        }
        internal static void OnDeath(GameState p,Entity body){
            if(!Enabled || !S.Eligible(p) || body==null || body.MapID!=1001 || body.MonsterInfo==null || !new uint[]{20,80,56,81,86}.Contains(body.MonsterInfo.ID) || !SessionInstall.TryEnter(p.Entity.UID))return;
            try{lock(p.ItemSyncRoot){if(S.Hunt(p,body,()=>Enabled && S.Eligible(p)))Refresh(p);}}catch(Exception ex){Q.Fail(p,"treasure-hunt",ex);}finally{SessionInstall.Exit(p.Entity.UID);}
        }
        internal static void Arrived(GameState p,Map map,ushort x,ushort y,TravelProgress.Arrival arrival){
            if(!Enabled || !S.Eligible(p) || arrival!=TravelProgress.Arrival.Walk && arrival!=TravelProgress.Arrival.Jump || map==null || map.ID!=1001 || x!=91 || y!=283
                || !ReferenceEquals(p.Map,map) || p.Entity.X!=x || p.Entity.Y!=y || !SessionInstall.TryEnter(p.Entity.UID))return;
            try{lock(p.ItemSyncRoot){if(S.StepVault(p,()=>Enabled && ReferenceEquals(p.Map,map) && p.Entity.X==x && p.Entity.Y==y))p.Send("The compartment is within reach. Open the vault beside you.");}}
            catch(Exception ex){Q.Fail(p,"treasure-vault-step",ex);}finally{SessionInstall.Exit(p.Entity.UID);}
        }
        internal static void Tick(){if(!Enabled)return;DesertTreasureEncounter.Tick();var now=S.Clock();if(now<nextTick)return;nextTick=now.AddSeconds(3);
            foreach(var p in Kernel.GamePool.Values){Watch watch;if(!S.Eligible(p) || p.Entity.MapID!=1001 || !watches.TryGetValue(p,out watch) || watch.Stage!=6 || DesertTreasureEncounter.HasOwner(p.Entity.UID) || Kernel.GetDistance(p.Entity.X,p.Entity.Y,728,676)>8)continue;
                try{DesertTreasureEncounter.Start(p);}catch(Exception ex){Q.Fail(p,"treasure-spawn",ex);}}
        }
        internal static bool Protect(uint id){return Enabled && id>=1828 && id<=1834;}
        internal static void OnLogin(GameState p){if(!Enabled || !Q.Player(p,false) || !SessionInstall.TryEnter(p.Entity.UID))return;
            try{DesertTreasureEncounter.Tick();lock(p.ItemSyncRoot)Refresh(p);}catch(Exception ex){Q.Fail(p,"treasure-login",ex);}finally{SessionInstall.Exit(p.Entity.UID);}}
        internal static void Refresh(GameState p){if(Enabled && Q.Player(p,false) && p.Quests!=null)p.Quests.SendFullGUI();}
        internal static void Fill(GameState p,List<MsgTaskStatus.QuestData> list,HashSet<uint> seen){
            if(!Enabled)return;for(uint id=1828;id<=1834;id++)seen.Add(id);if(!Q.Player(p,false) || p.Entity.Level<98 || p.Entity.Reborn>2)return;
            lock(p.ItemSyncRoot)try{var run=S.Load(p);watches.GetValue(p,key=>new Watch()).Stage=run.Stage;
                for(uint id=1828;id<=1834;id++){int step=(int)id-1827;var status=run.Stage>step?MsgTaskStatus.QuestData.QuestStatus.Finished:run.Stage==step?MsgTaskStatus.QuestData.QuestStatus.Accepted:MsgTaskStatus.QuestData.QuestStatus.Available;list.Add(MsgTaskStatus.QuestData.Create((QuestID)id,status,0));}
            }catch(Exception ex){Q.Fail(p,"treasure-journal",ex);}
        }
    }
}
