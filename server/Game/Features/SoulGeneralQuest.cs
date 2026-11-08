using System;
using System.Collections.Generic;
using Revora.Client;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using S=Revora.Game.Features.SoulGeneralStore;
using W=Revora.Game.Features.SoulGeneralPhantom;
namespace Revora.Game.Features
{
    internal static class SoulGeneralQuest
    {
        internal const uint Amber=4487,Zhao=4488,Painter=4489,Guard=4490;
        internal static bool Enabled {get;private set;}
        internal static int EnsureRockPopulation(Map map){
            if(map==null || map.ID!=1011 || map.BaseID!=1011)return 0;
            foreach(var existing in map.Entities.Values)
                if(existing.MonsterInfo!=null && existing.MonsterInfo.ID==106)return 0;
            Revora.Database.MonsterInformation profile;
            if(!Revora.Database.MonsterInformation.MonsterInformations.TryGetValue(106,out profile)
                || profile.Name!="RockMonsterL42" || profile.Level!=42 || profile.Boss || profile.Guard || profile.Reviver)
                throw new InvalidOperationException("Soul General Phoenix rock population catalog changed.");
            using(var sha=System.Security.Cryptography.SHA256.Create())
            using(var file=System.IO.File.OpenRead(System.IO.Path.Combine(Constants.DMapsPath,"maps","1011.map")))
                if(System.BitConverter.ToString(sha.ComputeHash(file)).Replace("-","")!="7E8F7A8262F8AE93A314699AF6D276F0324FFF6C32BCC6061BFFC2F336F1E817")
                    throw new InvalidOperationException("Soul General Phoenix terrain changed.");
            // The legacy catalog only places this quest family on map 1025.
            // Supply the missing Phoenix flock without replacing configured spawns.
            int added=0;
            for(ushort y=541;y<=585 && added<20;y++)for(ushort x=901;x<=945 && added<20;x++){
                if(!map.Floor[x,y,MapObjectType.Monster,null])continue;
                var info=profile.Copy();info.BoundX=901;info.BoundY=541;info.BoundCX=info.BoundCY=45;info.RespawnTime=30;
                var body=new Entity(EntityFlag.Monster,false){MapObjType=MapObjectType.Monster,MonsterInfo=info,
                    Name=info.Name,MinAttack=info.MinAttack,MaxAttack=info.MaxAttack,MagicAttack=info.MaxAttack,
                    Hitpoints=info.Hitpoints,MaxHitpoints=info.Hitpoints,Defence=info.Defence,Body=info.Mesh,
                    Level=info.Level,UID=map.EntityUIDCounter.Next,MapID=map.ID,SendUpdates=true,X=x,Y=y};
                info.Owner=body;map.AddEntity(body);added++;
            }
            if(added!=20)throw new InvalidOperationException("Soul General Phoenix flock has insufficient open terrain.");
            return added;
        }
        internal static void Initialize(){
            CheckNpc(1020,Amber,865,540,670);CheckNpc(1002,Zhao,368,251,671);CheckNpc(1020,Painter,556,571,672);CheckNpc(1002,Guard,372,251,673);
            S.Initialize();FranticMonkeysScenes.ValidateTerrain();W.Profile();
            CityGuardSystem.SpawnTalker(Amber,1020,865,540,6700,"GeneralAmber",0);CityGuardSystem.SpawnTalker(Zhao,1002,368,251,6710,"GeneralZhao",0);
            CityGuardSystem.SpawnTalker(Painter,1020,556,571,6720,"PainterFengKang",0);CityGuardSystem.SpawnTalker(Guard,1002,372,251,6730,"GuardLi",0);
            for(uint id=1751;id<=1757;id++)QuestInfo.Register((QuestID)id,QuestType.EquipmentBonus);Enabled=true;
        }
        private static void CheckNpc(ushort id,uint npc,ushort x,ushort y,ushort model){Map map;Revora.Interfaces.INpc n;
            if(!Kernel.Maps.TryGetValue(id,out map))throw new InvalidOperationException("Soul General NPC map unavailable.");
            if(map.Npcs.TryGetValue(npc,out n) && (n.X!=x || n.Y!=y || n.Mesh/10!=model))throw new InvalidOperationException("Soul General NPC placement changed.");
        }
        private static bool Near(GameState p,uint npc){return Enabled && S.Eligible(p) && Q.Near(p,npc,npc==Amber || npc==Painter?(ushort)1020:(ushort)1002);}
        internal static void Tick(){if(Enabled)W.Tick();}
        internal static void OnLogin(GameState p){if(!Enabled)return;try{
            W.Tick();lock(p.ItemSyncRoot)W.Recover(p);
        }catch(Exception ex){Q.Fail(p,"soul-general-login",ex);}}
        private static string Hint(S.Run r){switch(r.Stage){
            case 0:return "General Amber has three wishes. Hear him out near Ape City.";
            case 1:return "Amber waits at 865,540 outside Ape City.";
            case 2:return "Ask General Zhao at 368,251 in Twin City for a strand of hair.";
            case 3:return "Take Zhao's hair back to Amber.";
            case 4:return "Keep the silk pack with you. Scents: Snakeman "+r.Snake+"/1, RockMonsterL42 "+r.Rock+"/10, GiantApeL53 "+r.Ape+"/10. Return to Amber when it is full.";
            case 5:return "Painter Feng Kang waits at 556,571 in Ape City. Ask for Zhao's portrait.";
            case 6:return "Bring the portrait to Amber.";
            case 7:return "Something is wrong. Find General Zhao in Twin City.";
            case 8:return "Guard Li at 372,251 knows what has happened to Zhao.";
            case 9:return "Confront Amber at 865,540. Defeat his phantom and bring the Paper Figure to Zhao.";
            case 10:return "Bring the Paper Figure to General Zhao.";
            case 11:return "The curse is broken. Chang has your reward; leave two free places.";
            default:return "Zhao has recovered. You have earned your reward.";
        }}
        internal static bool HandleNpc(GameState p,NpcRequest request){
            if(p==null || request==null)return false;bool chang=p.ActiveNpc==AzurePillQuest.Chang;
            if(p.ActiveNpc!=Amber && p.ActiveNpc!=Zhao && p.ActiveNpc!=Painter && p.ActiveNpc!=Guard && (!chang || request.OptionID<100 || request.OptionID>102))return false;
            uint npc=p.ActiveNpc;if(!Near(p,npc) || request.NpcID!=0 && request.NpcID!=npc || !SessionInstall.TryEnter(p.Entity.UID))return true;
            try{lock(p.ItemSyncRoot){if(!Near(p,npc))return true;var run=S.Load(p);Func<bool> context=()=>Near(p,npc);string note=null;
                if(request.OptionID==(chang?101:1)){
                    bool changed=false;
                    if(chang && run.Stage==0)changed=S.Advance(p,0,context);
                    else if(npc==Amber && (run.Stage==1 || run.Stage==3 || run.Stage==4 || run.Stage==6))changed=S.Advance(p,run.Stage,context);
                    else if(npc==Zhao && (run.Stage==2 || run.Stage==7))changed=S.Advance(p,run.Stage,context);
                    else if(npc==Painter && run.Stage==5)changed=S.Advance(p,5,context);
                    else if(npc==Guard && run.Stage==8)changed=S.Advance(p,8,context);
                    else if(npc==Amber && run.Stage==9){W.Recover(p);changed=W.Start(p);}
                    else if(npc==Zhao && run.Stage==10)changed=S.HandInPaper(p,context);
                    else if(chang && run.Stage==11)changed=S.Claim(p,context);
                    if(!changed)note=Hint(run);
                }else if(request.OptionID==(chang?102:2)){
                    bool recovered=W.Recover(p);recovered=S.Recover(p,context)||recovered;
                    note=recovered?"Your errand can continue. Keep your belongings close.":"Check your bag and storage. I cannot replace something you still own.";
                }
                run=S.Load(p);var d=new Npcs(p);d.Text(note??Hint(run));d.Option(chang && run.Stage==11?"Claim my reward.":"About General Zhao.",(byte)(chang?101:1));
                if(run.Stage>=2 && run.Stage<=10)d.Option("I lost my quest belongings.",(byte)(chang?102:2));d.Option("Farewell.",255);d.Send();Refresh(p);
            }}catch(Exception ex){Q.Fail(p,"soul-general-npc",ex);}finally{SessionInstall.Exit(p.Entity.UID);}return true;
        }
        internal static void OnDeath(GameState p,Entity body){
            if(!Enabled || !S.Eligible(p) || body==null || body.MonsterInfo==null || body.MonsterInfo.ID!=13 && body.MonsterInfo.ID!=106 && body.MonsterInfo.ID!=70 || !SessionInstall.TryEnter(p.Entity.UID))return;
            try{lock(p.ItemSyncRoot)if(S.Hunt(p,body,()=>Enabled && S.Eligible(p)))Refresh(p);}catch(Exception ex){Q.Fail(p,"soul-general-hunt",ex);}finally{SessionInstall.Exit(p.Entity.UID);}
        }
        internal static bool Protect(uint id){return Enabled && id>=1751 && id<=1757;}
        internal static void Refresh(GameState p){if(Enabled && Q.Player(p,false) && p.Quests!=null)p.Quests.SendFullGUI();}
        internal static void Fill(GameState p,List<MsgTaskStatus.QuestData> list,HashSet<uint> seen){
            if(!Enabled)return;for(uint id=1751;id<=1757;id++)seen.Add(id);if(!Q.Player(p,false) || p.Entity.Level<55 || p.Entity.Reborn>2)return;
            lock(p.ItemSyncRoot)try{var run=S.Load(p);int current=run.Stage==0?0:run.Stage==1?1:run.Stage<=3?2:run.Stage==4?3:run.Stage<=6?4:run.Stage<=8?5:run.Stage<=10?6:run.Stage==11?7:8;
                for(uint id=1751;id<=1757;id++){int step=(int)id-1750;var status=current>step?MsgTaskStatus.QuestData.QuestStatus.Finished:current==step?MsgTaskStatus.QuestData.QuestStatus.Accepted:MsgTaskStatus.QuestData.QuestStatus.Available;
                    list.Add(MsgTaskStatus.QuestData.Create((QuestID)id,status,0));}
            }catch(Exception ex){Q.Fail(p,"soul-general-journal",ex);}
        }
    }
}
