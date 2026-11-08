using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using S=Revora.Game.Features.DesertExpeditionStore;
namespace Revora.Game.Features
{
    internal static class DesertExpeditionQuest
    {
        internal const uint MoMo=4702,TrapStart=9918352;
        internal static bool Enabled {get;private set;}
        private static readonly ushort[,] Sites={{172,399},{102,292},{109,369}};
        private sealed class Watch {internal int Stage;internal DateTime? Due;}
        private static readonly ConditionalWeakTable<GameState,Watch> watches=new ConditionalWeakTable<GameState,Watch>();
        private static DateTime nextTick;
        private static void Npc(uint id,ushort mapId,ushort x,ushort y,ushort mesh,string name){
            Map map;if(!Kernel.Maps.TryGetValue(mapId,out map))throw new InvalidOperationException("Desert Expedition map missing.");
            foreach(var other in Kernel.Maps.Values){Revora.Interfaces.INpc npc;if(other.Npcs.TryGetValue(id,out npc) && (other.ID!=mapId || npc.X!=x || npc.Y!=y || npc.Mesh/10!=mesh/10))throw new InvalidOperationException("Desert Expedition NPC conflict.");}
            CityGuardSystem.SpawnTalker(id,mapId,x,y,mesh,name,0);
        }
        internal static void Initialize(){
            uint[] maps={1000,1001};string[] hashes={"c874aa5659420dfcf03545f1bfad67bea0197ae55fbc8140fcd7e85ffbae33f1","89bdcb2d247add85d8d7b888c8f90d7d4d5127e51499479ca988a0c9884624fc"};
            for(int i=0;i<maps.Length;i++)using(var sha=System.Security.Cryptography.SHA256.Create())using(var file=System.IO.File.OpenRead(System.IO.Path.Combine(Constants.DMapsPath,"maps",maps[i]+".map")))
                if(System.BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant()!=hashes[i])throw new InvalidOperationException("Desert Expedition terrain changed.");
            S.Initialize();Npc(MoMo,1000,520,589,4750,"MasterMoMo");
            for(int site=0;site<3;site++)Npc(TrapStart+(uint)site,1001,Sites[site,0],(ushort)(Sites[site,1]+2),3830,"SpiritStone");
            for(uint id=1835;id<=1839;id++)QuestInfo.Register((QuestID)id,QuestType.EquipmentBonus);Enabled=true;
        }

        private static bool Near(GameState p,uint npc){return Enabled && S.Eligible(p) && Q.Near(p,npc,npc==AzurePillQuest.Chang?(ushort)1002:npc==MoMo?(ushort)1000:(ushort)1001);}
        private static string Hint(S.Run run){switch(run.Stage){
            case 0:case 1:return "Master Mo Mo needs help with the bulls. Find him in Desert City at 520,589.";
            case 2:return "Use the Fervent Pill and hunt Bull Monsters. When the fever fades, bring me ten Burnt Bullhorns.";
            case 3:return "Use the mirror at 172,399, 102,292 and 109,369. Lure a Mad Bull into each trap, then slay it.";
            case 4:return "The Bull Master waits near 186,359. Bring me its heart.";
            case 5:return "The desert is safer. Taskmaster Chang holds your reward.";
            default:return "You have served the desert well. Farewell.";
        }}
        internal static bool HandleNpc(GameState p,NpcRequest request){
            if(!Enabled || p==null || request==null)return false;uint npc=p.ActiveNpc;bool chang=npc==AzurePillQuest.Chang,stone=npc>=TrapStart && npc<TrapStart+3;
            if(npc!=MoMo && !stone && (!chang || request.OptionID<180 || request.OptionID>182))return false;
            if(!Near(p,npc) || request.NpcID!=0 && request.NpcID!=npc || !SessionInstall.TryEnter(p.Entity.UID))return true;
            try{lock(p.ItemSyncRoot){Func<bool> near=()=>Near(p,npc);var run=S.Load(p);byte option=request.OptionID;string note=null;
                if(stone){int site=(int)(npc-TrapStart);var stoneDialog=new Npcs(p);stoneDialog.Text("Set the mirror at "+Sites[site,0]+","+Sites[site,1]+". A bull must fall inside your trap.");stoneDialog.Option("I understand.",255);stoneDialog.Send();return true;}
                if(run.Stage==2)S.SettlePill(p,near);
                if(option==(chang?181:1)){
                    if(chang && run.Stage==0)S.Advance(p,0,near);else if(chang && run.Stage==5)S.Claim(p,near);
                    else if(npc==MoMo && run.Stage>=1 && run.Stage<=3)S.Advance(p,run.Stage,near);
                    else if(npc==MoMo && run.Stage==4)S.Report(p,near);
                }else if(!chang && option>=30 && option<=34){uint[] types={721920,721921,710259,710261,710260};note=S.Recover(p,types[option-30],near)?"Here is your lost item. Keep it safe.":"Check your pack and storage. I can only replace what is truly lost.";}
                run=S.Load(p);var d=new Npcs(p);d.Text(note??Hint(run));d.Option(chang && run.Stage==5?"Claim my reward.":"I am ready.",(byte)(chang?181:1));
                if(!chang && run.Stage>=2 && run.Stage<=4){if(option==20){string[] names={"Fervent Pill","Ghost Mirror","Burnt Bullhorn","Bull Ghost","Bull Heart"};for(int i=0;i<names.Length;i++)d.Option("Lost "+names[i]+".",(byte)(30+i));}else d.Option("I lost a quest item.",20);}
                d.Option("Farewell.",255);d.Send();Refresh(p);
            }}catch(Exception ex){Q.Fail(p,"expedition-npc",ex);}finally{SessionInstall.Exit(p.Entity.UID);}return true;
        }
        internal static bool TryUse(ConquerItem item,GameState p){
            if(!Enabled || item==null || item.ID!=721920 && item.ID!=721921 && (item.ID<710259 || item.ID>710261))return false;
            if(!S.Eligible(p) || !SessionInstall.TryEnter(p.Entity.UID))return true;
            try{lock(p.ItemSyncRoot){ConquerItem owned;if(!p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(owned,item))return true;
                Func<bool> ready=()=>Enabled && S.Eligible(p);bool changed=false;
                if(item.ID==721920){S.SettlePill(p,ready);changed=S.ActivatePill(p,item.UID,ready);if(changed)p.Send("The fever burns for one minute. Each ten seconds takes a tenth of your health.");}
                else if(item.ID==721921){for(int site=0;site<3;site++)if(Kernel.GetDistance(p.Entity.X,p.Entity.Y,Sites[site,0],Sites[site,1])<=3){changed=S.PlaceTrap(p,item.UID,site,ready);break;}
                    if(changed){p.Send("The trap is set for five minutes. Draw the bull inside before striking it down.");DesertExpeditionEncounters.Start(p);}}
                if(!changed)p.Send(Hint(S.Load(p)));Refresh(p);
            }}catch(Exception ex){Q.Fail(p,"expedition-item",ex);}finally{SessionInstall.Exit(p.Entity.UID);}return true;
        }
        internal static void OnDeath(GameState p,Entity body){
            if(!Enabled || !S.Eligible(p) || body==null || body.MapID!=1001 || body.MonsterInfo==null || body.MonsterInfo.ID!=57 && body.MonsterInfo.ID!=82 || !SessionInstall.TryEnter(p.Entity.UID))return;
            try{lock(p.ItemSyncRoot){S.SettlePill(p,()=>Enabled && S.Eligible(p));if(S.Hunt(p,body,()=>Enabled && S.Eligible(p)))Refresh(p);}}
            catch(Exception ex){Q.Fail(p,"expedition-hunt",ex);}finally{SessionInstall.Exit(p.Entity.UID);}
        }
        internal static void Tick(){if(!Enabled)return;DesertExpeditionEncounters.Tick();var now=S.Clock();if(now<nextTick)return;nextTick=now.AddSeconds(1);
            foreach(var p in Kernel.GamePool.Values){Watch watch;if(!S.Eligible(p) || !watches.TryGetValue(p,out watch) || !SessionInstall.TryEnter(p.Entity.UID))continue;
                try{lock(p.ItemSyncRoot){if(watch.Due.HasValue && now>=watch.Due.Value){S.SettlePill(p,()=>Enabled && S.Eligible(p));Refresh(p);}
                    if(p.Entity.MapID!=1001 || DesertExpeditionEncounters.HasOwner(p.Entity.UID))continue;
                    bool near=watch.Stage==4 && Kernel.GetDistance(p.Entity.X,p.Entity.Y,186,359)<=8;
                    if(watch.Stage==3)for(int i=0;i<3;i++)near|=Kernel.GetDistance(p.Entity.X,p.Entity.Y,Sites[i,0],Sites[i,1])<=8;
                    if(near)DesertExpeditionEncounters.Start(p);
                }}catch(Exception ex){Q.Fail(p,"expedition-tick",ex);}finally{SessionInstall.Exit(p.Entity.UID);}}
        }
        internal static bool Protect(uint id){return Enabled && id>=1835 && id<=1839;}
        internal static void OnLogin(GameState p){if(!Enabled || !Q.Player(p,false) || !SessionInstall.TryEnter(p.Entity.UID))return;
            try{DesertExpeditionEncounters.Tick();lock(p.ItemSyncRoot){S.Load(p);if(S.Eligible(p))S.SettlePill(p,()=>Enabled && S.Eligible(p));Refresh(p);}}
            catch(Exception ex){Q.Fail(p,"expedition-login",ex);}finally{SessionInstall.Exit(p.Entity.UID);}}
        internal static void Refresh(GameState p){if(Enabled && Q.Player(p,false) && p.Quests!=null)p.Quests.SendFullGUI();}
        internal static void Fill(GameState p,List<MsgTaskStatus.QuestData> list,HashSet<uint> seen){
            if(!Enabled)return;for(uint id=1835;id<=1839;id++)seen.Add(id);if(!Q.Player(p,false) || p.Entity.Level<105 || p.Entity.Reborn>2)return;
            lock(p.ItemSyncRoot)try{var run=S.Load(p);var watch=watches.GetValue(p,key=>new Watch());watch.Stage=run.Stage;
                watch.Due=run.Stage==2 && run.PillUntil.HasValue && run.PillPaid<S.PillTicks?run.PillUntil.Value.AddSeconds(-S.PillSeconds+(run.PillPaid+1)*S.PillInterval):(DateTime?)null;
                for(uint id=1835;id<=1839;id++){int step=(int)id-1834;var status=run.Stage>step?MsgTaskStatus.QuestData.QuestStatus.Finished:run.Stage==step?MsgTaskStatus.QuestData.QuestStatus.Accepted:MsgTaskStatus.QuestData.QuestStatus.Available;list.Add(MsgTaskStatus.QuestData.Create((QuestID)id,status,0));}
            }catch(Exception ex){Q.Fail(p,"expedition-journal",ex);}
        }
    }
}
