using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features {
    internal static class AzurePillQuest {
        internal const uint Chang=900071727,Wu=900071728,Mu=900071729,Bian=900071730;
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static Func<bool> Drop=()=>Kernel.Random.Next(100)<50;
        private static bool enabled;
        private static readonly ConditionalWeakTable<Entity,object> deaths=new ConditionalWeakTable<Entity,object>();
        private sealed class Run {internal int Stage;internal uint Soul,Eye,Pill;internal uint[] Meat,Herbs;internal DateTime? Ready;}
        private static bool Ready(GameState p){return enabled && Q.Player(p) && p.Entity.Level>=40 && p.Entity.Reborn<=2 && CharacterProgression.Managed(p)
            && p.Spells!=null && p.Booth==null && p.Trade!=null && !p.Trade.InTrade;}
        private static bool Near(GameState p){return Ready(p) && Q.Near(p,p.ActiveNpc,p.ActiveNpc==Wu || p.ActiveNpc==Mu?(ushort)1011:(ushort)1002);}
        internal static void Initialize(){
            foreach(uint id in new uint[]{721695,721696,721697,721698,728238})if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Azure Pill catalog unavailable.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_azure_pill(
uid bigint NOT NULL,life smallint NOT NULL CHECK(life BETWEEN 0 AND 2),stage smallint NOT NULL CHECK(stage BETWEEN 1 AND 7),
soul_type bigint NOT NULL,eye bigint NOT NULL DEFAULT 0,meat bigint[] NOT NULL DEFAULT '{}',herbs bigint[] NOT NULL DEFAULT '{}',
pill bigint NOT NULL DEFAULT 0,ready_at timestamptz,rewards bigint[] NOT NULL DEFAULT '{}',PRIMARY KEY(uid,life));");}for(uint mission=1727;mission<=1732;mission++)QuestInfo.Register((QuestID)mission,QuestType.EquipmentBonus);enabled=true;
        }
        internal static bool Protect(uint mission){return enabled && mission>=1727 && mission<=1732;}
        internal static void Refresh(GameState p){if(enabled && Q.Player(p,false) && p.Quests!=null)p.Quests.SendFullGUI();}
        internal static void Fill(GameState p,List<MsgTaskStatus.QuestData> list,HashSet<uint> seen){
            if(!enabled)return;
            for(uint mission=1727;mission<=1732;mission++)seen.Add(mission);
            if(!Q.Player(p,false) || p.Entity.Level<40 || p.Entity.Reborn>2)return;
            lock(p.ItemSyncRoot)try{
                int stage=0;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,
                    "SELECT stage FROM revora_azure_pill WHERE uid=@uid AND life=@life","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn)){
                    object value=q.ExecuteScalar();if(value!=null && value!=DBNull.Value)stage=Convert.ToInt32(value);
                }}
                for(uint mission=1727;mission<=1732;mission++){
                    int step=(int)mission-1726;
                    var status=stage>step?MsgTaskStatus.QuestData.QuestStatus.Finished:stage==step?MsgTaskStatus.QuestData.QuestStatus.Accepted:MsgTaskStatus.QuestData.QuestStatus.Available;
                    list.Add(MsgTaskStatus.QuestData.Create((QuestID)mission,status,0));
                }
            }catch(Exception ex){Q.Fail(p,"azure-pill-journal",ex);}
        }
        internal static void Create(){Initialize();
            CityGuardSystem.SpawnTalker(Chang,1002,473,300,12090,"TaskmasterChang",0);
            CityGuardSystem.SpawnTalker(Wu,1011,214,231,7570,"ScholarWu",0);
            CityGuardSystem.SpawnTalker(Mu,1011,182,241,7580,"PharmacistMuMu",0);
            CityGuardSystem.SpawnTalker(Bian,1002,425,331,7590,"BianQing",0);
        }
        private static Run Read(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,GameState p){
            using(var q=Q.Command(c,t,"SELECT stage,soul_type,eye,meat,herbs,pill,ready_at FROM revora_azure_pill WHERE uid=@uid AND life=@life FOR UPDATE","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn))
            using(var r=q.ExecuteReader()){if(!r.Read())return null;return new Run{Stage=r.GetInt16(0),Soul=checked((uint)r.GetInt64(1)),Eye=checked((uint)r.GetInt64(2)),
                Meat=r.GetFieldValue<long[]>(3).Select(x=>checked((uint)x)).ToArray(),Herbs=r.GetFieldValue<long[]>(4).Select(x=>checked((uint)x)).ToArray(),
                Pill=checked((uint)r.GetInt64(5)),Ready=r.IsDBNull(6)?(DateTime?)null:r.GetFieldValue<DateTimeOffset>(6).UtcDateTime};}
        }
        private static void Bag(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,GameState p){
            var saved=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())while(r.Read())saved.Add(checked((uint)r.GetInt64(0)));
            if(!saved.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Azure Pill inventory changed.");
        }
        private static void Save(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,GameState p,Run r,int before,IEnumerable<ConquerItem> reward){
            if(Q.Execute(c,t,@"UPDATE revora_azure_pill SET stage=@stage,eye=@eye,meat=@meat,herbs=@herbs,pill=@pill,ready_at=@ready,rewards=@rewards
WHERE uid=@uid AND life=@life AND stage=@before","stage",r.Stage,"eye",(long)r.Eye,"meat",r.Meat.Select(x=>(long)x).ToArray(),"herbs",r.Herbs.Select(x=>(long)x).ToArray(),
                "pill",(long)r.Pill,"ready",r.Ready.HasValue?(object)r.Ready.Value:DBNull.Value,"rewards",r.Stage==7?reward.Select(i=>(long)i.UID).ToArray():new long[0],
                "uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"before",before)!=1)throw new InvalidOperationException("Azure Pill progress changed.");
        }
        internal static bool HandleNpc(GameState p,NpcRequest request){
            if(p==null || request==null || p.ActiveNpc<Chang || p.ActiveNpc>Bian)return false;
            if(!Near(p) || request.NpcID!=0 && request.NpcID!=p.ActiveNpc)return true;
            var d=new Npcs(p);
            if(request.OptionID==0){d.Text(p.ActiveNpc==Chang?"Scholar Wu has fallen ill. Will you carry word between us?":p.ActiveNpc==Wu?"Mu Mu knows the cure. Please, help me recover.":p.ActiveNpc==Mu?"A ratling eye, three cuts of meat and two herb packs. That is the cure.":"Mu Mu sent you? I keep herbs for his remedies.");d.Option("About the Azure Pill",1);if(p.ActiveNpc==Chang && ConnectorQuest.Enabled)d.Option("A message for the thieves",50);if(p.ActiveNpc==Chang && SpunGoldQuest.Enabled && p.Entity.Level>=30)d.Option("Hong's missing armor",60);if(p.ActiveNpc==Chang && ArtisanFeatherQuest.Enabled && p.Entity.Level>=45)d.Option("Luo's missing father",70);if(p.ActiveNpc==Chang && MysticRingQuest.Enabled && p.Entity.Level>=48)d.Option("The Minstrel's ring",80);if(p.ActiveNpc==Chang && FranticMonkeysQuest.Enabled && p.Entity.Level>=50)d.Option("Trouble in Ape City",90);if(p.ActiveNpc==Chang && SoulGeneralQuest.Enabled && p.Entity.Level>=55)d.Option("General Amber's wishes",100);if(p.ActiveNpc==Chang && TomboyQuest.Enabled && p.Entity.Level>=78)d.Option("Lynn and his daughter",130);if(p.ActiveNpc==Chang && FamilyFeudQuest.Enabled && p.Entity.Level>=95)d.Option("Felix and the bandits",160);if(p.ActiveNpc==Chang && DesertTreasureQuest.Enabled && p.Entity.Level>=98)d.Option("The desert treasures",170);if(p.ActiveNpc==Chang && DesertExpeditionQuest.Enabled && p.Entity.Level>=105)d.Option("Mo Mo and the bulls",180);if(p.ActiveNpc==Chang && HeroWingsQuest.Enabled && p.Entity.Level>=90)d.Option("Xiao needs a healer",150);if(p.ActiveNpc==Chang && DesertBloodQuest.Enabled && p.Entity.Level>=85)d.Option("Harvey and the Crypt",140);if(p.ActiveNpc==Chang && GhostCityQuest.Enabled && p.Entity.Level>=75)d.Option("The missing child",120);if(p.ActiveNpc==Chang && MadManQuest.Enabled && p.Entity.Level>=60)d.Option("Du San's illness",110);if(p.ActiveNpc==Chang && PowerBookQuest.Enabled && p.Entity.Level>=115)d.Option("About the Power Book",40);}
            else if(request.OptionID==1){d.Text(Act(p));Refresh(p);}
            d.Option("Farewell.",255);d.Send();return true;
        }
        private static string Act(GameState p){
            if(!Near(p) || !SessionInstall.TryEnter(p.Entity.UID))return "Come back when you are ready.";
            try{lock(p.ItemSyncRoot)lock(MentorExperience.Sync){
                if(!Near(p))return "Come closer.";
                var now=Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Azure Pill clock requires UTC.");
                var added=new List<ConquerItem>();var removed=new List<uint>();ulong silver=p.Entity.Money;bool paidSilver=false;ExperienceRewardPlan plan=null;ExperienceRewards.Effects effects=null;ulong auto=p.Entity.AutoHuntEXP;
                string message;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    SilverBalance.AssertSaved(c,t,p);var r=Read(c,t,p);Bag(c,t,p);
                    if(r==null){
                        if(p.ActiveNpc!=Chang)return "Taskmaster Chang will tell you who needs help.";
                        uint soul=QuestSoulReward.Create().ID;
                        if(Q.Execute(c,t,"INSERT INTO revora_azure_pill(uid,life,stage,soul_type) VALUES(@uid,@life,1,@soul)","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"soul",(long)soul)!=1)throw new InvalidOperationException("Azure Pill acceptance failed.");
                        if(!Near(p) || p.ActiveNpc!=Chang)throw new InvalidOperationException("Azure Pill acceptance owner changed.");
                        t.Commit();return "Find Scholar Wu in Phoenix Castle at 214,231.";
                    }
                    if(r.Stage==7)return "You have already helped Scholar Wu in this life.";
                    int before=r.Stage;uint coin=0;
                    if(r.Stage==1 && p.ActiveNpc==Wu){r.Stage=2;coin=100;message="Ask Mu Mu at 182,241 for an Azure Pill.";}
                    else if(r.Stage==2 && p.ActiveNpc==Mu){r.Stage=3;coin=100;message="Hunt level-38 Fire Rats for an eye and three cuts of meat. Bian Qing in Twin City has the herbs.";}
                    else if(r.Stage==3 && p.ActiveNpc==Bian){
                        if(r.Herbs.Length!=0)return "I have already given you the herbs. Take them to Mu Mu.";
                        if(p.Inventory.Count>38)return "Leave two places for the herbs.";
                        added.Add(Q.New(721697,true));added.Add(Q.New(721697,true));r.Herbs=added.Select(i=>i.UID).ToArray();message="Take these two herb packs to Mu Mu.";
                    }
                    else if(r.Stage==3 && p.ActiveNpc==Mu){
                        if(r.Eye==0 || r.Meat.Length!=3 || r.Herbs.Length!=2)return "Bring one eye, three cuts of meat and two herb packs.";
                        Q.Consume(c,t,p,r.Eye,721695);removed.Add(r.Eye);
                        foreach(uint id in r.Meat){Q.Consume(c,t,p,id,721696);removed.Add(id);}foreach(uint id in r.Herbs){Q.Consume(c,t,p,id,721697);removed.Add(id);}
                        r.Stage=4;r.Ready=now.AddMinutes(2);coin=100;message="The medicine needs two minutes over the fire.";
                    }
                    else if(r.Stage==4 && p.ActiveNpc==Mu){
                        if(!r.Ready.HasValue || now<r.Ready.Value)return "The medicine is still simmering.";
                        if(p.Inventory.Count>=40)return "Leave a place for the medicine.";
                        var pill=Q.New(721698,true);added.Add(pill);r.Pill=pill.UID;r.Stage=5;coin=100;message="Take this Azure Pill to Scholar Wu.";
                    }
                    else if(r.Stage==5 && p.ActiveNpc==Wu){
                        Q.Consume(c,t,p,r.Pill,721698);removed.Add(r.Pill);r.Stage=6;coin=1500;
                        ulong xp=p.Entity.Level>=Config.LevelCap?0:p.ExpBall/3;
                        plan=new ExperienceRewardPlan(CharacterProgression.Capture(p),xp,Config.LevelCap,Config.AttributePointsPerLevel,ReincarnationRecovery.Snapshot(p));
                        CharacterProgression.Persist(c,t,p,plan.After);effects=ExperienceRewards.Persist(c,t,p,plan);ReincarnationRecovery.Consume(c,t,p,plan.Restored);
                        auto=p.Entity.InAutoHunt?checked(p.Entity.AutoHuntEXP+xp/Math.Max(1u,Config.AutoHuntExperienceDivisor)):p.Entity.AutoHuntEXP;
                        message="I can breathe again. Tell Chang the cure worked.";
                    }
                    else if(r.Stage==6 && p.ActiveNpc==Chang){
                        if(p.Inventory.Count>38)return "Leave two places for your ring pack and bound P7 soul.";
                        ConquerItemBaseInformation info;if(!ConquerItemInformation.BaseInformations.TryGetValue(r.Soul,out info) || r.Soul<800000 || r.Soul>=900000 || info.PurificationLevel!=7)throw new InvalidOperationException("Azure Pill prize changed.");
                        added.Add(Q.New(728238,true));added.Add(Q.New(r.Soul,true));r.Stage=7;message="Well done. This ring pack and bound P7 soul are yours.";
                    }
                    else return "Finish the errand you were given, then return.";
                    silver=checked(silver+coin);if(silver>Entity.MaximumSilver)return "Make room in your purse first.";
                    if(coin!=0){SilverBalance.Persist(c,t,p,silver);paidSilver=true;}
                    foreach(var item in added)Q.Insert(c,t,p.Entity.UID,item);
                    Save(c,t,p,r,before,added);if(r.Stage==7)ProgressionEvidence.RecordEquipmentQuestAggregate(c,t,p.Entity.UID);if(!Near(p))throw new InvalidOperationException("Azure Pill owner changed.");t.Commit();
                }}
                Q.Install(p,removed,added);if(paidSilver)SilverBalance.ApplyCommitted(p,silver);
                if(plan!=null){ReincarnationRecovery.Committed(p,plan.Restored);MentorExperience.Apply(p,effects.MentorCredit);p.ApplyExperienceReward(plan,effects,auto,true);}
                return message;
            }}catch(Exception ex){Q.Fail(p,"azure-pill",ex);return "Reconnect before we continue.";}finally{SessionInstall.Exit(p.Entity.UID);}
        }
        internal static void Revived(Entity m){if(m!=null && !m.Dead)lock(m)deaths.Remove(m);}
        internal static void OnDeath(GameState p,Entity m){
            if(!Ready(p) || m==null || m.MapID!=1011 || m.MonsterInfo==null || m.MonsterInfo.ID!=67 || !SessionInstall.TryEnter(p.Entity.UID))return;
            try{lock(p.ItemSyncRoot)lock(m){
                object used;if(!Ready(p) || !DailySoulBall.WildernessDeath(m,p.Entity) || Kernel.GetDistance(p.Entity.X,p.Entity.Y,m.X,m.Y)>Constants.pScreenDistance || deaths.TryGetValue(m,out used))return;
                deaths.Add(m,new object());if(!Drop() || p.Inventory.Count>=40)return;
                ConquerItem item;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    SkillTable.LockOwner(c,t,p);var r=Read(c,t,p);if(r==null || r.Stage!=3 || r.Eye!=0 && r.Meat.Length>=3)return;Bag(c,t,p);
                    item=Q.New(r.Eye==0?721695u:721696u,true);Q.Insert(c,t,p.Entity.UID,item);
                    if(r.Eye==0)r.Eye=item.UID;else r.Meat=r.Meat.Concat(new[]{item.UID}).ToArray();Save(c,t,p,r,3,new ConquerItem[0]);
                    if(!Ready(p) || !DailySoulBall.WildernessDeath(m,p.Entity))throw new InvalidOperationException("Azure Pill hunter changed.");t.Commit();
                }}
                Q.Install(p,new uint[0],new[]{item});
            }}catch(Exception ex){Q.Fail(p,"azure-pill-hunt",ex);}finally{SessionInstall.Exit(p.Entity.UID);}
        }
    }
}
