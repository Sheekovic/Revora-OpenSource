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
    internal static class ConnectorQuest
    {
        internal const uint Wong=900071718,Chen=4437;
        internal static bool Enabled {get;private set;}
        private static readonly ConditionalWeakTable<Entity,object> deaths=new ConditionalWeakTable<Entity,object>();
        private sealed class Run {internal int Stage,Kills;internal uint Badge,Letter,Soul;}
        private static bool Ready(GameState p){return Enabled && Q.Player(p) && p.Entity.Level>=22 && p.Entity.Reborn<=2 && CharacterProgression.Managed(p)
            && ReferenceEquals(p.Entity.Owner,p) && p.Booth==null && p.Trade!=null && !p.Trade.InTrade && p.Entity.PokerTable==0;}
        private static bool Near(GameState p,uint npc){return Ready(p) && Q.Near(p,npc,npc==Chen?(ushort)1011:(ushort)1002);}
        internal static void Create()
        {
            foreach(uint id in new uint[]{721715,729942,728236,723911})if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Connector catalog missing.");
            MonsterInformation bandit;if(!MonsterInformation.MonsterInformations.TryGetValue(2140,out bandit) || bandit.Name!="CastleBandit" || bandit.Level!=22 || bandit.Boss || bandit.Guard)
                throw new InvalidOperationException("Connector bandit catalog changed.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_connector(
uid bigint NOT NULL,life smallint NOT NULL CHECK(life BETWEEN 0 AND 2),stage smallint NOT NULL CHECK(stage BETWEEN 1 AND 6),
kills smallint NOT NULL DEFAULT 0 CHECK(kills BETWEEN 0 AND 5),badge bigint NOT NULL UNIQUE,letter bigint NOT NULL DEFAULT 0,soul bigint NOT NULL,
rewards bigint[] NOT NULL DEFAULT '{}',PRIMARY KEY(uid,life));
CREATE TABLE IF NOT EXISTS revora_connector_recoveries(
uid bigint NOT NULL,life smallint NOT NULL,old_uid bigint NOT NULL,new_uid bigint NOT NULL,type bigint NOT NULL,
PRIMARY KEY(uid,life,old_uid),FOREIGN KEY(uid,life) REFERENCES revora_connector(uid,life));");}
            Map map;Revora.Interfaces.INpc actor;
            if(!Kernel.Maps.TryGetValue(1011,out map) || !Kernel.Maps.ContainsKey(1002))throw new InvalidOperationException("Connector maps unavailable.");
            if(map.Npcs.TryGetValue(Chen,out actor) && (actor.X!=33 || actor.Y!=398 || actor.Mesh!=5820))throw new InvalidOperationException("Connector Chen placement changed.");
            CityGuardSystem.SpawnTalker(Wong,1002,440,339,5810,"ThiefWong",0);
            CityGuardSystem.SpawnTalker(Chen,1011,33,398,5820,"ThiefChen",0);
            for(uint id=1718;id<=1722;id++)QuestInfo.Register((QuestID)id,QuestType.EquipmentBonus);Enabled=true;
        }
        private static Run Read(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,GameState p)
        {
            using(var q=Q.Command(c,t,"SELECT stage,kills,badge,letter,soul FROM revora_connector WHERE uid=@uid AND life=@life FOR UPDATE","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn))
            using(var r=q.ExecuteReader())return r.Read()?new Run{Stage=r.GetInt16(0),Kills=r.GetInt16(1),Badge=checked((uint)r.GetInt64(2)),Letter=checked((uint)r.GetInt64(3)),Soul=checked((uint)r.GetInt64(4))}:new Run();
        }
        private static void Owner(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,GameState p)
        {
            SkillTable.LockOwner(c,t,p);
            using(var q=Q.Command(c,t,"SELECT reborn,level FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))using(var r=q.ExecuteReader())
                if(!r.Read() || Convert.ToInt32(r[0])!=p.Entity.Reborn || Convert.ToInt32(r[1])!=p.Entity.Level)throw new InvalidOperationException("Connector saved owner changed.");
            var saved=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid FOR UPDATE","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())while(r.Read())saved.Add(checked((uint)r.GetInt64(0)));
            if(!saved.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Connector bag changed.");
        }
        private static bool Usable(GameState p,uint uid)
        {
            ConquerItem item;return p.Inventory.TryGetItem(uid,out item) && item.Bound && item.StackSize==1 && item.Lock==0 && !item.Suspicious && !item.InWardrobe
                && item.Position==0 && item.Warehouse==0 && item.Days==0 && item.TimeLeftInMinutes==0;
        }
        internal static bool HandleNpc(GameState p,NpcRequest request)
        {
            if(p==null || request==null)return false;
            bool chang=p.ActiveNpc==AzurePillQuest.Chang;
            if(p.ActiveNpc!=Wong && p.ActiveNpc!=Chen && (!chang || request.OptionID!=50 && request.OptionID!=51 && request.OptionID!=52 && !(request.OptionID==0 && p.Entity!=null && p.Entity.Level<40)))return false;
            uint npc=p.ActiveNpc;if(!Near(p,npc) || request.NpcID!=0 && request.NpcID!=npc)return true;
            var d=new Npcs(p);
            if(request.OptionID==2 || chang && request.OptionID==52)d.Text(Recover(p,npc));
            else if(request.OptionID==1 || chang && request.OptionID==51)d.Text(Act(p,npc));
            else d.Text(chang?"Two thieves need a trustworthy messenger. Will you hear them out?":npc==Wong?"Chen waits in Maple Forest. I need someone he can trust.":"Words are easy. Deal with five Castle Bandits, then we will talk.");
            if(chang && SpunGoldQuest.Enabled && p.Entity.Level>=30)d.Option("Hong's missing armor",60);d.Option(chang?"I will help.":"About the message.",(byte)(chang?51:1));d.Option("I lost my badge or letter.",(byte)(chang?52:2));d.Option("Farewell.",255);d.Send();Refresh(p);return true;
        }
        internal static bool Protect(uint id){return Enabled && id>=1718 && id<=1722;}
        internal static void Refresh(GameState p){if(Enabled && Q.Player(p,false) && p.Quests!=null)p.Quests.SendFullGUI();}
        internal static void Fill(GameState p,List<MsgTaskStatus.QuestData> list,HashSet<uint> seen)
        {
            if(!Enabled)return;for(uint id=1718;id<=1722;id++)seen.Add(id);
            if(!Q.Player(p,false) || p.Entity.Level<22 || p.Entity.Reborn>2)return;
            lock(p.ItemSyncRoot)try{
                Run run;using(var c=DataHolder.MySqlConnection){c.Open();run=Read(c,null,p);}
                for(uint id=1718;id<=1722;id++){
                    int step=(int)id-1717;
                    var status=run.Stage>step?MsgTaskStatus.QuestData.QuestStatus.Finished:run.Stage==step?MsgTaskStatus.QuestData.QuestStatus.Accepted:MsgTaskStatus.QuestData.QuestStatus.Available;
                    list.Add(MsgTaskStatus.QuestData.Create((QuestID)id,status,0));
                }
            }catch(Exception ex){Q.Fail(p,"connector-journal",ex);}
        }
        private static bool Missing(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,uint uid)
        {
            if(uid==0)return false;
            using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE uid=@uid FOR UPDATE","uid",(long)uid))return q.ExecuteScalar()==null;
        }
        private static string Recover(GameState p,uint npc)
        {
            if(!Near(p,npc) || !SessionInstall.TryEnter(p.Entity.UID))return "Come back when you are ready.";
            try{lock(p.ItemSyncRoot){
                if(!Near(p,npc))return "Come closer.";var added=new List<ConquerItem>();
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Owner(c,t,p);var run=Read(c,t,p);if(run.Stage==0 || run.Stage==6)return "There is nothing to replace in this life.";
                    uint oldBadge=run.Badge,oldLetter=run.Letter;
                    bool badge=Missing(c,t,oldBadge),letter=run.Stage==4 && Missing(c,t,oldLetter);
                    if(!badge && !letter)return "Your belongings still exist. Check your bag and storage.";
                    if(p.Inventory.Count+(badge?1:0)+(letter?1:0)>40)return "Make room for your belongings first.";
                    if(badge){var item=Q.New(721715,true);added.Add(item);run.Badge=item.UID;}
                    if(letter){var item=Q.New(729942,true);added.Add(item);run.Letter=item.UID;}
                    foreach(var item in added){
                        Q.Insert(c,t,p.Entity.UID,item);
                        if(Q.Execute(c,t,"INSERT INTO revora_connector_recoveries(uid,life,old_uid,new_uid,type) VALUES(@uid,@life,@old,@new,@type)",
                            "uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"old",(long)(item.ID==721715?oldBadge:oldLetter),"new",(long)item.UID,"type",(long)item.ID)!=1)
                            throw new InvalidOperationException("Connector recovery receipt failed.");
                    }
                    if(Q.Execute(c,t,"UPDATE revora_connector SET badge=@badge,letter=@letter WHERE uid=@uid AND life=@life AND stage=@stage AND badge=@oldbadge AND letter=@oldletter",
                        "badge",(long)run.Badge,"letter",(long)run.Letter,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"stage",run.Stage,"oldbadge",(long)oldBadge,"oldletter",(long)oldLetter)!=1)
                        throw new InvalidOperationException("Connector recovery progress changed.");
                    if(!Near(p,npc))throw new InvalidOperationException("Connector recovery NPC changed.");t.Commit();
                }}
                Q.Install(p,new uint[0],added);return "Here are your lost belongings. Carry on with your errand.";
            }}catch(Exception ex){Q.Fail(p,"connector-recovery",ex);return "Reconnect before we continue.";}finally{SessionInstall.Exit(p.Entity.UID);}
        }
        private static string Act(GameState p,uint npc)
        {
            if(!Near(p,npc) || !SessionInstall.TryEnter(p.Entity.UID))return "Come back when you are ready.";
            try{lock(p.ItemSyncRoot)lock(MentorExperience.Sync){
                if(!Near(p,npc))return "Come closer.";
                var added=new List<ConquerItem>();var removed=new List<uint>();ulong silver=p.Entity.Money,auto=p.Entity.AutoHuntEXP;uint coin=0;
                ExperienceRewardPlan plan=null;ExperienceRewards.Effects effects=null;string message;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Owner(c,t,p);var run=Read(c,t,p);int before=run.Stage;
                    if(before==0){
                        if(npc!=AzurePillQuest.Chang)return "Speak to Chang in Twin City first.";
                        if(p.Inventory.Count>=40)return "Leave a place for your badge.";
                        var badge=Q.New(721715,true);added.Add(badge);run.Badge=badge.UID;run.Soul=QuestSoulReward.Create().ID;run.Stage=1;
                        Q.Execute(c,t,"INSERT INTO revora_connector(uid,life,stage,badge,soul) VALUES(@uid,@life,1,@badge,@soul)","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"badge",(long)run.Badge,"soul",(long)run.Soul);
                        message="Find Thief Wong at 440,339. Show him this badge.";
                    }
                    else if(before==1 && npc==Wong){if(!Usable(p,run.Badge) || !Q.Owns(c,t,p,run.Badge,721715))return "Bring Chang's badge.";run.Stage=2;coin=100;message="Find Chen in Phoenix Castle at 33,398.";}
                    else if(before==2 && npc==Chen){run.Stage=3;coin=100;message="Defeat five Castle Bandits nearby, then return.";}
                    else if(before==3 && npc==Chen){
                        if(run.Kills<5)return "Defeat five Castle Bandits. You have defeated "+run.Kills+".";
                        if(p.Inventory.Count>=40)return "Leave a place for the letter.";
                        var letter=Q.New(729942,true);added.Add(letter);run.Letter=letter.UID;run.Stage=4;coin=100;message="Take this letter to Wong at 440,339.";
                    }
                    else if(before==4 && npc==Wong){
                        if(!Usable(p,run.Letter))return "Bring Chen's letter.";
                        Q.Consume(c,t,p,run.Letter,729942);removed.Add(run.Letter);run.Stage=5;coin=1000;
                        ulong xp=p.Entity.Level>=Config.LevelCap?0:p.ExpBall/6;
                        plan=new ExperienceRewardPlan(CharacterProgression.Capture(p),xp,Config.LevelCap,Config.AttributePointsPerLevel,ReincarnationRecovery.Snapshot(p));
                        CharacterProgression.Persist(c,t,p,plan.After);effects=ExperienceRewards.Persist(c,t,p,plan);ReincarnationRecovery.Consume(c,t,p,plan.Restored);
                        auto=p.Entity.InAutoHunt?checked(p.Entity.AutoHuntEXP+xp/Math.Max(1u,Config.AutoHuntExperienceDivisor)):p.Entity.AutoHuntEXP;
                        message="My thanks. Chang has your reward.";
                    }
                    else if(before==5 && npc==AzurePillQuest.Chang){
                        if(!Usable(p,run.Badge))return "Bring your training badge.";
                        if(p.Inventory.Count>38)return "Leave two free places for your rewards.";
                        ConquerItemBaseInformation soul;if(!ConquerItemInformation.BaseInformations.TryGetValue(run.Soul,out soul) || run.Soul<800000 || run.Soul>=900000 || soul.PurificationLevel!=7)throw new InvalidOperationException("Connector reward changed.");
                        Q.Consume(c,t,p,run.Badge,721715);removed.Add(run.Badge);added.Add(Q.New(728236,true));added.Add(Q.New(723911,true));added.Add(Q.New(run.Soul,true));run.Stage=6;
                        message="Well done. Take this necklace pack, EXP Ball and bound P7 soul.";
                    }
                    else return before==6?"You have already carried this message in this life.":"Finish the errand you were given, then return.";
                    if(coin!=0){SilverBalance.AssertSaved(c,t,p);silver=checked(silver+coin);if(silver>Entity.MaximumSilver)return "Make room in your purse.";SilverBalance.Persist(c,t,p,silver);}
                    foreach(var item in added)Q.Insert(c,t,p.Entity.UID,item);
                    if(before!=0 && Q.Execute(c,t,"UPDATE revora_connector SET stage=@stage,letter=@letter,rewards=@rewards WHERE uid=@uid AND life=@life AND stage=@before",
                        "stage",run.Stage,"letter",(long)run.Letter,"rewards",run.Stage==6?added.Select(i=>(long)i.UID).ToArray():new long[0],"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"before",before)!=1)throw new InvalidOperationException("Connector stage changed.");
                    if(run.Stage==6)ProgressionEvidence.RecordEquipmentQuestAggregate(c,t,p.Entity.UID);
                    if(!Near(p,npc))throw new InvalidOperationException("Connector NPC changed.");t.Commit();
                }}
                Q.Install(p,removed,added);if(coin!=0)SilverBalance.ApplyCommitted(p,silver);
                if(plan!=null){ReincarnationRecovery.Committed(p,plan.Restored);MentorExperience.Apply(p,effects.MentorCredit);p.ApplyExperienceReward(plan,effects,auto,true);}
                return message;
            }}catch(Exception ex){Q.Fail(p,"connector-npc",ex);return "Reconnect before we continue.";}finally{SessionInstall.Exit(p.Entity.UID);}
        }
        internal static void Revived(Entity body){if(body!=null && !body.Dead)lock(body)deaths.Remove(body);}
        internal static void OnDeath(GameState p,Entity body)
        {
            if(!Ready(p) || body==null || body.MapID!=1011 || body.MonsterInfo==null || body.MonsterInfo.ID!=2140 || !SessionInstall.TryEnter(p.Entity.UID))return;
            try{lock(p.ItemSyncRoot)lock(body){
                object credited;if(!Ready(p) || !DailySoulBall.WildernessDeath(body,p.Entity) || !ReferenceEquals(body.Killer,p.Entity)
                    || Kernel.GetDistance(p.Entity.X,p.Entity.Y,body.X,body.Y)>Constants.pScreenDistance || deaths.TryGetValue(body,out credited))return;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=3 || run.Kills>=5){deaths.Add(body,new object());return;}
                    if(Q.Execute(c,t,"UPDATE revora_connector SET kills=kills+1 WHERE uid=@uid AND life=@life AND stage=3 AND kills=@before","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"before",run.Kills)!=1)throw new InvalidOperationException("Connector kills changed.");
                    if(!Ready(p) || !DailySoulBall.WildernessDeath(body,p.Entity) || !ReferenceEquals(body.Killer,p.Entity))throw new InvalidOperationException("Connector killer changed.");t.Commit();
                }}deaths.Add(body,new object());
            }}catch(Exception ex){Q.Fail(p,"connector-hunt",ex);}finally{SessionInstall.Exit(p.Entity.UID);}
        }
    }
}
