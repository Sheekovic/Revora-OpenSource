using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static class ArtisanFeatherQuest
    {
        internal const uint Apprentice=4466,Artisan=4467;
        internal static bool Enabled {get;private set;}
        internal static Func<int,int> Roll=max=>Kernel.Random.Next(max);
        private sealed class Run {internal int Stage,Count;internal uint Feather,Soul;}
        private static readonly ConditionalWeakTable<Entity,object> deaths=new ConditionalWeakTable<Entity,object>();
        private static bool Ready(GameState p){return Enabled && Q.Player(p) && ReferenceEquals(p.Entity.Owner,p) && CharacterProgression.Managed(p)
            && p.Entity.Level>=45 && p.Entity.Reborn<=2 && p.Trade!=null && !p.Trade.InTrade && p.Booth==null && p.Entity.PokerTable==0;}
        private static bool Near(GameState p,uint npc){return Ready(p) && Q.Near(p,npc,1002);}
        internal static void Initialize(){
            foreach(uint id in new uint[]{728239,729976})if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Artisan catalog missing.");
            uint[] families={1,2,3,2143};string[] names={"Pheasant","Turtledove","Robin","Vulture"};
            for(int i=0;i<families.Length;i++){MonsterInformation m;if(!MonsterInformation.MonsterInformations.TryGetValue(families[i],out m) || m.Name!=names[i] || m.Guard || m.Reviver || m.Boss)throw new InvalidOperationException("Artisan hunt family changed.");}
            Map map;if(!Kernel.Maps.TryGetValue(1002,out map))throw new InvalidOperationException("Artisan town unavailable.");
            CheckNpc(map,Apprentice,459,331,5500);CheckNpc(map,Artisan,342,640,5510);
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_artisan_feather(
uid bigint NOT NULL,life smallint NOT NULL CHECK(life BETWEEN 0 AND 2),stage smallint NOT NULL CHECK(stage BETWEEN 1 AND 6),
feathers smallint NOT NULL DEFAULT 0 CHECK(feathers BETWEEN 0 AND 100),feather bigint NOT NULL DEFAULT 0,soul bigint NOT NULL,
rewards bigint[] NOT NULL DEFAULT '{}',PRIMARY KEY(uid,life));
CREATE TABLE IF NOT EXISTS revora_artisan_feather_recoveries(
uid bigint NOT NULL,life smallint NOT NULL,old_uid bigint NOT NULL,PRIMARY KEY(uid,life,old_uid),
FOREIGN KEY(uid,life) REFERENCES revora_artisan_feather(uid,life));");}
            CityGuardSystem.SpawnTalker(Apprentice,1002,459,331,5500,"ApprenticeLuo",0);CityGuardSystem.SpawnTalker(Artisan,1002,342,640,5510,"ArtisanLuo",0);
            for(uint id=1733;id<=1737;id++)QuestInfo.Register((QuestID)id,QuestType.EquipmentBonus);Enabled=true;
        }
        private static void CheckNpc(Map map,uint id,ushort x,ushort y,ushort mesh){Revora.Interfaces.INpc n;if(map.Npcs.TryGetValue(id,out n) && (n.X!=x || n.Y!=y || n.Mesh!=mesh))throw new InvalidOperationException("Artisan NPC placement changed.");}
        private static Run Read(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,GameState p){
            using(var q=Q.Command(c,t,"SELECT stage,feathers,feather,soul FROM revora_artisan_feather WHERE uid=@uid AND life=@life FOR UPDATE","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn))
            using(var r=q.ExecuteReader())return r.Read()?new Run{Stage=r.GetInt16(0),Count=r.GetInt16(1),Feather=checked((uint)r.GetInt64(2)),Soul=checked((uint)r.GetInt64(3))}:new Run();
        }
        private static void Owner(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,GameState p)
        {
            SkillTable.LockOwner(c,t,p);
            using(var q=Q.Command(c,t,"SELECT reborn,level FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))using(var r=q.ExecuteReader())
                if(!r.Read() || Convert.ToInt32(r[0])!=p.Entity.Reborn || Convert.ToInt32(r[1])!=p.Entity.Level)throw new InvalidOperationException("Artisan Feather saved owner changed.");
            var saved=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid FOR UPDATE","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())while(r.Read())saved.Add(checked((uint)r.GetInt64(0)));
            if(!saved.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Artisan Feather bag changed.");
        }
        private static bool Material(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,GameState p,Run run){ConquerItem item;
            if(run.Feather==0 || !p.Inventory.TryGetItem(run.Feather,out item) || item.Lock!=0 || item.Suspicious || item.InWardrobe || item.Days!=0 || item.TimeLeftInMinutes!=0 || !Q.Owns(c,t,p,run.Feather,729976))return false;
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM items WHERE uid=@id AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0')","id",(long)run.Feather))return Equals(q.ExecuteScalar(),true);
        }
        internal static bool HandleNpc(GameState p,NpcRequest request){
            if(p==null || request==null)return false;bool chang=p.ActiveNpc==AzurePillQuest.Chang;
            if(p.ActiveNpc!=Apprentice && p.ActiveNpc!=Artisan && (!chang || request.OptionID<70 || request.OptionID>72))return false;
            uint npc=p.ActiveNpc;if(!Near(p,npc) || request.NpcID!=0 && request.NpcID!=npc)return true;
            var d=new Npcs(p);
            if(request.OptionID==(chang?71:1))d.Text(Act(p,npc));
            else if(request.OptionID==(chang?72:2))d.Text(Recover(p,npc));
            else d.Text(chang?"Young Luo is worried about his father. Will you look into it?":npc==Apprentice?"Father has not returned. Please find him at 342,640.":"My wounds will heal, but I still need feathers for my arrows.");
            d.Option(chang?"About Luo's family.":"About the feathers.",(byte)(chang?71:1));
            d.Option("I lost my Vulture Feather.",(byte)(chang?72:2));d.Option("Farewell.",255);d.Send();Refresh(p);return true;
        }
        private static string Act(GameState p,uint npc){
            if(!Near(p,npc) || !SessionInstall.TryEnter(p.Entity.UID))return "Come closer when you are ready.";
            try{lock(p.ItemSyncRoot)lock(MentorExperience.Sync){
                if(!Near(p,npc))return "Come closer.";var added=new List<ConquerItem>();var removed=new List<uint>();uint coin=0;ulong silver=p.Entity.Money,auto=p.Entity.AutoHuntEXP;
                ExperienceRewardPlan plan=null;ExperienceRewards.Effects effects=null;string message;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Owner(c,t,p);var run=Read(c,t,p);int before=run.Stage;
                    if(before==0 && npc==AzurePillQuest.Chang){run.Stage=1;run.Soul=QuestSoulReward.Create().ID;
                        if(Q.Execute(c,t,"INSERT INTO revora_artisan_feather(uid,life,stage,soul) VALUES(@uid,@life,1,@soul)","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"soul",(long)run.Soul)!=1)throw new InvalidOperationException("Artisan acceptance failed.");message="Find Apprentice Luo at 459,331.";}
                    else if(before==1 && npc==Apprentice){run.Stage=2;coin=100;message="Find my father at 342,640. Tell him I am waiting.";}
                    else if(before==2 && npc==Artisan){run.Stage=3;coin=100;message="Collect 100 feathers from Pheasants, Turtledoves or Robins in Wind Plain.";}
                    else if(before==3 && npc==Artisan){if(run.Count<100)return "You have gathered "+run.Count+" of 100 feathers.";run.Stage=4;coin=100;message="Now bring me a Vulture Feather. Hunt Vultures near 713,678 or 866,552 in Ape City.";}
                    else if(before==4 && npc==Artisan){
                        if(!Material(c,t,p,run))return "Bring the Vulture Feather you found.";
                        Q.Consume(c,t,p,run.Feather,729976);removed.Add(run.Feather);run.Stage=5;coin=1500;
                        ulong xp=p.Entity.Level>=Config.LevelCap?0:p.ExpBall/3;
                        plan=new ExperienceRewardPlan(CharacterProgression.Capture(p),xp,Config.LevelCap,Config.AttributePointsPerLevel,ReincarnationRecovery.Snapshot(p));
                        CharacterProgression.Persist(c,t,p,plan.After);effects=ExperienceRewards.Persist(c,t,p,plan);ReincarnationRecovery.Consume(c,t,p,plan.Restored);
                        auto=p.Entity.InAutoHunt?checked(p.Entity.AutoHuntEXP+xp/Math.Max(1u,Config.AutoHuntExperienceDivisor)):p.Entity.AutoHuntEXP;message="These will make fine arrows. Chang has your reward.";
                    }
                    else if(before==5 && npc==AzurePillQuest.Chang){
                        if(p.Inventory.Count>38)return "Leave two free places for your rewards.";
                        if(FixedOutputPacks.ArtisanWeaponFor(p.Entity.Class)==0)return "I have no suitable weapon for your calling.";
                        ConquerItemBaseInformation soul;if(!ConquerItemInformation.BaseInformations.TryGetValue(run.Soul,out soul) || run.Soul<800000 || run.Soul>=900000 || soul.PurificationLevel!=7)throw new InvalidOperationException("Artisan reward changed.");
                        added.Add(Q.New(728239,true));added.Add(Q.New(run.Soul,true));run.Stage=6;message="Take this weapon pack and bound P7 soul. You have earned them.";
                    }else return before==6?"You have already helped Luo's family in this life.":"Finish your current errand first.";
                    if(coin!=0){SilverBalance.AssertSaved(c,t,p);silver=checked(silver+coin);if(silver>Entity.MaximumSilver)return "Make room in your purse.";SilverBalance.Persist(c,t,p,silver);}
                    foreach(var item in added)Q.Insert(c,t,p.Entity.UID,item);
                    if(before!=0 && Q.Execute(c,t,"UPDATE revora_artisan_feather SET stage=@stage,rewards=@rewards WHERE uid=@uid AND life=@life AND stage=@before",
                        "stage",run.Stage,"rewards",run.Stage==6?added.Select(i=>(long)i.UID).ToArray():new long[0],"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"before",before)!=1)throw new InvalidOperationException("Artisan stage changed.");
                    if(run.Stage==6)ProgressionEvidence.RecordEquipmentQuestAggregate(c,t,p.Entity.UID);
                    if(!Near(p,npc))throw new InvalidOperationException("Artisan NPC changed.");t.Commit();
                }}Q.Install(p,removed,added);if(coin!=0)SilverBalance.ApplyCommitted(p,silver);
                if(plan!=null){ReincarnationRecovery.Committed(p,plan.Restored);MentorExperience.Apply(p,effects.MentorCredit);p.ApplyExperienceReward(plan,effects,auto,true);}return message;
            }}catch(Exception ex){Q.Fail(p,"artisan-npc",ex);return "Reconnect before we continue.";}finally{SessionInstall.Exit(p.Entity.UID);}
        }
        private static string Recover(GameState p,uint npc){
            if(!Near(p,npc) || !SessionInstall.TryEnter(p.Entity.UID))return "Come closer when you are ready.";
            try{lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=4 || run.Feather==0)return "Keep hunting Vultures for a feather.";
                using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE uid=@uid FOR UPDATE","uid",(long)run.Feather))if(q.ExecuteScalar()!=null)return "Your feather still exists. Check your bag and storage.";
                Q.Execute(c,t,"INSERT INTO revora_artisan_feather_recoveries(uid,life,old_uid) VALUES(@uid,@life,@old)","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"old",(long)run.Feather);
                if(Q.Execute(c,t,"UPDATE revora_artisan_feather SET feather=0 WHERE uid=@uid AND life=@life AND stage=4 AND feather=@old","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"old",(long)run.Feather)!=1)throw new InvalidOperationException("Artisan recovery changed.");
                if(!Near(p,npc))throw new InvalidOperationException("Artisan recovery NPC changed.");t.Commit();return "Hunt another Vulture Feather. Your earlier work is safe.";
            }}}catch(Exception ex){Q.Fail(p,"artisan-recovery",ex);return "Reconnect before we continue.";}finally{SessionInstall.Exit(p.Entity.UID);}
        }
        internal static void Revived(Entity body){if(body!=null && !body.Dead)lock(body)deaths.Remove(body);}
        internal static void OnDeath(GameState p,Entity body){
            if(!Ready(p) || body==null || body.MonsterInfo==null)return;uint family=body.MonsterInfo.ID;
            bool bird=body.MapID==1002 && family>=1 && family<=3,vulture=body.MapID==1020 && family==2143;
            if(!bird && !vulture || !SessionInstall.TryEnter(p.Entity.UID))return;
            try{lock(p.ItemSyncRoot)lock(body){object used;
                if(!Ready(p) || !DailySoulBall.WildernessDeath(body,p.Entity) || !ReferenceEquals(body.Killer,p.Entity) || Kernel.GetDistance(p.Entity.X,p.Entity.Y,body.X,body.Y)>Constants.pScreenDistance || deaths.TryGetValue(body,out used))return;
                deaths.Add(body,new object());ConquerItem feather=null;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Owner(c,t,p);var run=Read(c,t,p);
                    if(bird && run.Stage==3 && run.Count<100){
                        if(Q.Execute(c,t,"UPDATE revora_artisan_feather SET feathers=feathers+1 WHERE uid=@uid AND life=@life AND stage=3 AND feathers=@old","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"old",run.Count)!=1)throw new InvalidOperationException("Artisan count changed.");
                    }else if(vulture && run.Stage==4 && run.Feather==0 && p.Inventory.Count<40){
                        int roll=Roll(100);if(roll<0 || roll>=100)throw new InvalidOperationException("Artisan drop range changed.");if(roll>=50)return;
                        feather=Q.New(729976,true);Q.Insert(c,t,p.Entity.UID,feather);
                        if(Q.Execute(c,t,"UPDATE revora_artisan_feather SET feather=@item WHERE uid=@uid AND life=@life AND stage=4 AND feather=0","item",(long)feather.UID,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn)!=1)throw new InvalidOperationException("Artisan feather changed.");
                    }else return;
                    if(!Ready(p) || !DailySoulBall.WildernessDeath(body,p.Entity) || !ReferenceEquals(body.Killer,p.Entity))throw new InvalidOperationException("Artisan killer changed.");t.Commit();
                }}if(feather!=null){Q.Install(p,new uint[0],new[]{feather});p.Send("You found a Vulture Feather. Take it to Artisan Luo.");}
            }}catch(Exception ex){Q.Fail(p,"artisan-hunt",ex);}finally{SessionInstall.Exit(p.Entity.UID);}
        }
        internal static bool Protect(uint id){return Enabled && id>=1733 && id<=1737;}
        internal static void Refresh(GameState p){if(Enabled && Q.Player(p,false) && p.Quests!=null)p.Quests.SendFullGUI();}
        internal static void Fill(GameState p,List<MsgTaskStatus.QuestData> list,HashSet<uint> seen){
            if(!Enabled)return;for(uint id=1733;id<=1737;id++)seen.Add(id);if(!Q.Player(p,false) || p.Entity.Level<45 || p.Entity.Reborn>2)return;
            lock(p.ItemSyncRoot)try{Run run;using(var c=DataHolder.MySqlConnection){c.Open();run=Read(c,null,p);}
                for(uint id=1733;id<=1737;id++){int step=(int)id-1732;var status=run.Stage>step?MsgTaskStatus.QuestData.QuestStatus.Finished:run.Stage==step?MsgTaskStatus.QuestData.QuestStatus.Accepted:MsgTaskStatus.QuestData.QuestStatus.Available;
                    list.Add(MsgTaskStatus.QuestData.Create((QuestID)id,status,0));}
            }catch(Exception ex){Q.Fail(p,"artisan-journal",ex);}
        }
    }
}
