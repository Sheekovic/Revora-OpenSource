using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    // Callers own the session lease, ItemSyncRoot, world context and failure fence.
    // No NPC, item-use or death route is enabled by this persistence component.
    internal static class SpunGoldStore
    {
        internal const uint Family=9921723;
        internal static Func<int,int> Roll=max=>Kernel.Random.Next(max);
        internal sealed class Run {internal int Stage;internal uint Item,Type,Soul;internal Guid? Attempt;}
        private static readonly ConditionalWeakTable<Entity,object> deaths=new ConditionalWeakTable<Entity,object>();
        internal static void Initialize()
        {
            foreach(uint id in new uint[]{728237,729971,729972,729973,729974})if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Spun Gold catalog missing.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_spun_gold(
uid bigint NOT NULL,life smallint NOT NULL CHECK(life BETWEEN 0 AND 2),stage smallint NOT NULL CHECK(stage BETWEEN 1 AND 8),
item_uid bigint NOT NULL DEFAULT 0,item_type bigint NOT NULL DEFAULT 0,soul bigint NOT NULL,attempt uuid,
rewards bigint[] NOT NULL DEFAULT '{}',PRIMARY KEY(uid,life));
CREATE TABLE IF NOT EXISTS revora_spun_gold_attempts(
id uuid PRIMARY KEY,uid bigint NOT NULL,life smallint NOT NULL,body bigint NOT NULL,map integer NOT NULL,
state smallint NOT NULL DEFAULT 0 CHECK(state BETWEEN 0 AND 2),until_at timestamptz NOT NULL,
FOREIGN KEY(uid,life) REFERENCES revora_spun_gold(uid,life));
CREATE TABLE IF NOT EXISTS revora_spun_gold_recoveries(
id uuid PRIMARY KEY,uid bigint NOT NULL,life smallint NOT NULL,old_uid bigint NOT NULL,new_uid bigint NOT NULL,type bigint NOT NULL,
FOREIGN KEY(uid,life) REFERENCES revora_spun_gold(uid,life));
CREATE UNIQUE INDEX IF NOT EXISTS revora_spun_gold_recovery_old ON revora_spun_gold_recoveries(uid,life,old_uid) WHERE old_uid>0;");}
        }
        internal static bool Eligible(GameState p){return Q.Player(p) && ReferenceEquals(p.Entity.Owner,p) && CharacterProgression.Managed(p) && p.Entity.Level>=30 && p.Entity.Reborn<=2
            && p.Trade!=null && !p.Trade.InTrade && p.Booth==null && p.Entity.PokerTable==0;}
        private static void Ready(GameState p,Func<bool> context){if(!Eligible(p) || !Monitor.IsEntered(p.ItemSyncRoot) || context==null || !context())throw new InvalidOperationException("Spun Gold owner or context changed.");}
        private static Run Read(NpgsqlConnection c,NpgsqlTransaction t,GameState p)
        {
            using(var q=Q.Command(c,t,"SELECT stage,item_uid,item_type,soul,attempt FROM revora_spun_gold WHERE uid=@uid AND life=@life FOR UPDATE","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn))
            using(var r=q.ExecuteReader())return r.Read()?new Run{Stage=r.GetInt16(0),Item=checked((uint)r.GetInt64(1)),Type=checked((uint)r.GetInt64(2)),Soul=checked((uint)r.GetInt64(3)),Attempt=r.IsDBNull(4)?(Guid?)null:r.GetGuid(4)}:new Run();
        }
        internal static Run Load(GameState p){if(!Q.Player(p,false))return null;lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();return Read(c,null,p);}}
        private static void Owner(NpgsqlConnection c,NpgsqlTransaction t,GameState p)
        {
            SkillTable.LockOwner(c,t,p);
            using(var q=Q.Command(c,t,"SELECT reborn,level FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))using(var r=q.ExecuteReader())
                if(!r.Read() || Convert.ToInt32(r[0])!=p.Entity.Reborn || Convert.ToInt32(r[1])!=p.Entity.Level)throw new InvalidOperationException("Spun Gold saved owner changed.");
            var ids=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid FOR UPDATE","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())while(r.Read())ids.Add(checked((uint)r.GetInt64(0)));
            if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Spun Gold bag changed.");
        }
        private static bool Material(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Run r,uint type)
        {
            ConquerItem item;if(r.Item==0 || r.Type!=type || !p.Inventory.TryGetItem(r.Item,out item) || item.Lock!=0 || item.Suspicious || item.InWardrobe
                || item.Days!=0 || item.TimeLeftInMinutes!=0 || !Q.Owns(c,t,p,r.Item,type))return false;
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM items WHERE uid=@id AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0')","id",(long)r.Item))return Equals(q.ExecuteScalar(),true);
        }
        private static void Save(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Run r,int before,IEnumerable<ConquerItem> outputs)
        {
            if(Q.Execute(c,t,"UPDATE revora_spun_gold SET stage=@stage,item_uid=@item,item_type=@type,attempt=@attempt,rewards=@rewards WHERE uid=@uid AND life=@life AND stage=@before",
                "stage",r.Stage,"item",(long)r.Item,"type",(long)r.Type,"attempt",r.Attempt.HasValue?(object)r.Attempt.Value:DBNull.Value,
                "rewards",r.Stage==8?outputs.Select(i=>(long)i.UID).ToArray():new long[0],"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"before",before)!=1)
                throw new InvalidOperationException("Spun Gold progress changed.");
        }
        // 0 accept; 1 Hong referral; 2 invitation exchange; 3 box use;
        // 4 armor return; 5 final Chang claim. Context must name the actual actor/item.
        internal static bool Act(GameState p,int action,uint supplied,Func<bool> context)
        {
            Ready(p,context);lock(MentorExperience.Sync){
                var added=new List<ConquerItem>();var removed=new List<uint>();uint coin=0;ulong silver=p.Entity.Money,auto=p.Entity.AutoHuntEXP;
                ExperienceRewardPlan plan=null;ExperienceRewards.Effects effects=null;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Owner(c,t,p);var r=Read(c,t,p);int before=r.Stage;
                    if(action==0 && before==0){
                        r.Stage=1;r.Soul=QuestSoulReward.Create().ID;
                        if(Q.Execute(c,t,"INSERT INTO revora_spun_gold(uid,life,stage,soul) VALUES(@uid,@life,1,@soul)","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"soul",(long)r.Soul)!=1)throw new InvalidOperationException("Spun Gold acceptance failed.");
                    }
                    else if(action==1 && before==1){r.Stage=2;coin=100;}
                    else if(action==2 && before==2 || action==3 && before==5){
                        uint input=action==2?729971u:729973u,output=action==2?729972u:729974u;
                        if(action==3 && supplied!=r.Item || !Material(c,t,p,r,input))return false;
                        Q.Consume(c,t,p,r.Item,input);removed.Add(r.Item);var item=Q.New(output,true);added.Add(item);r.Item=item.UID;r.Type=output;r.Stage=action==2?3:6;if(action==2)coin=100;
                    }
                    else if(action==4 && before==6){
                        if(!Material(c,t,p,r,729974))return false;Q.Consume(c,t,p,r.Item,729974);removed.Add(r.Item);r.Item=r.Type=0;r.Stage=7;coin=1000;
                        ulong xp=p.Entity.Level>=Config.LevelCap?0:p.ExpBall/6;
                        plan=new ExperienceRewardPlan(CharacterProgression.Capture(p),xp,Config.LevelCap,Config.AttributePointsPerLevel,ReincarnationRecovery.Snapshot(p));
                        CharacterProgression.Persist(c,t,p,plan.After);effects=ExperienceRewards.Persist(c,t,p,plan);ReincarnationRecovery.Consume(c,t,p,plan.Restored);
                        auto=p.Entity.InAutoHunt?checked(p.Entity.AutoHuntEXP+xp/Math.Max(1u,Config.AutoHuntExperienceDivisor)):p.Entity.AutoHuntEXP;
                    }
                    else if(action==5 && before==7){
                        if(p.Inventory.Count>38 || FixedOutputPacks.SpunGoldArmorFor(p.Entity.Class)==0)return false;
                        ConquerItemBaseInformation soul;if(!ConquerItemInformation.BaseInformations.TryGetValue(r.Soul,out soul) || soul.PurificationLevel!=7 || r.Soul<800000 || r.Soul>=900000)throw new InvalidOperationException("Spun Gold reward changed.");
                        added.Add(Q.New(728237,true));added.Add(Q.New(r.Soul,true));r.Stage=8;
                    }else return false;
                    if(coin!=0){SilverBalance.AssertSaved(c,t,p);silver=checked(silver+coin);if(silver>Entity.MaximumSilver)return false;SilverBalance.Persist(c,t,p,silver);}
                    foreach(var item in added)Q.Insert(c,t,p.Entity.UID,item);
                    if(before!=0)Save(c,t,p,r,before,added);if(r.Stage==8)ProgressionEvidence.RecordEquipmentQuestAggregate(c,t,p.Entity.UID);Ready(p,context);t.Commit();
                }}
                Q.Install(p,removed,added);if(coin!=0)SilverBalance.ApplyCommitted(p,silver);
                if(plan!=null){ReincarnationRecovery.Committed(p,plan.Restored);MentorExperience.Apply(p,effects.MentorCredit);p.ApplyExperienceReward(plan,effects,auto,true);}return true;
            }
        }
        internal static void Revived(Entity body){if(body!=null && !body.Dead)lock(body)deaths.Remove(body);}
        internal static bool Invitation(GameState p,Entity body,Func<bool> context)
        {
            Ready(p,context);if(body==null)return false;
            lock(body){object used;if(body.MapID!=1011 || body.MonsterInfo==null || body.MonsterInfo.ID!=7 && body.MonsterInfo.ID!=66
                || !DailySoulBall.WildernessDeath(body,p.Entity) || !ReferenceEquals(body.Killer,p.Entity) || Kernel.GetDistance(p.Entity.X,p.Entity.Y,body.X,body.Y)>Constants.pScreenDistance || deaths.TryGetValue(body,out used))return false;
                deaths.Add(body,new object());int roll=Roll(100);if(roll<0 || roll>=100)throw new InvalidOperationException("Spun Gold drop range changed.");if(roll>=50 || p.Inventory.Count>=40)return false;
                ConquerItem item;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Owner(c,t,p);var r=Read(c,t,p);if(r.Stage!=2 || r.Item!=0)return false;
                    item=Q.New(729971,true);Q.Insert(c,t,p.Entity.UID,item);r.Item=item.UID;r.Type=item.ID;Save(c,t,p,r,2,new ConquerItem[0]);
                    Ready(p,()=>context() && DailySoulBall.WildernessDeath(body,p.Entity) && ReferenceEquals(body.Killer,p.Entity));t.Commit();
                }}Q.Install(p,new uint[0],new[]{item});return true;
            }
        }
        internal static Guid? Reserve(GameState p,Guid disguise,uint bodyUid,Func<bool> context)
        {
            Func<bool> valid=()=>context!=null && context() && p.Entity.MapID==1011 && p.Map.BaseID==1011 && Kernel.GetDistance(p.Entity.X,p.Entity.Y,100,430)<=3 && SpunGoldAppearance.Owns(p,disguise);
            Ready(p,valid);if(bodyUid==0 || Kernel.GamePool.ContainsKey(bodyUid) || p.Map.Npcs.ContainsKey(bodyUid))return null;
            DateTime now=SpunGoldAppearance.Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Spun Gold requires UTC.");
            Guid id=Guid.NewGuid();uint offering;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var r=Read(c,t,p);if(r.Stage!=3 || !Material(c,t,p,r,729972))return null;
                offering=r.Item;Q.Consume(c,t,p,offering,729972);r.Item=r.Type=0;r.Stage=4;r.Attempt=id;
                if(Q.Execute(c,t,"INSERT INTO revora_spun_gold_attempts(id,uid,life,body,map,until_at) VALUES(@id,@uid,@life,@body,1011,@until)","id",id,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"body",(long)bodyUid,"until",now.AddMinutes(5))!=1)throw new InvalidOperationException("Spun Gold attempt not saved.");
                Save(c,t,p,r,3,new ConquerItem[0]);Ready(p,valid);t.Commit();
            }}Q.Install(p,new[]{offering},new ConquerItem[0]);return id;
        }
        internal static bool Win(GameState p,Guid attempt,Entity body,Func<bool> context)
        {
            Entity current;
            Func<bool> valid=()=>context!=null && context() && body!=null && body.Dead && body.MapID==1011 && p.Entity.MapID==1011 && ReferenceEquals(body.Killer,p.Entity)
                && body.EntityFlag==EntityFlag.Monster && !body.Companion && p.Map.BaseID==1011
                && Kernel.GetDistance(p.Entity.X,p.Entity.Y,body.X,body.Y)<=Constants.pScreenDistance
                && body.MonsterInfo!=null && !body.MonsterInfo.Guard && !body.MonsterInfo.Reviver && !body.MonsterInfo.Boss
                && body.MonsterInfo.ID==Family && ReferenceEquals(body.MonsterInfo.Owner,body) && p.Map.Entities.TryGetValue(body.UID,out current) && ReferenceEquals(current,body);
            Ready(p,valid);ConquerItem box=null;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var r=Read(c,t,p);if(r.Stage!=4 || r.Attempt!=attempt)return false;
                if(Q.Execute(c,t,"UPDATE revora_spun_gold_attempts SET state=1 WHERE id=@id AND uid=@uid AND life=@life AND state=0 AND body=@body AND map=1011 AND until_at>@now",
                    "id",attempt,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"body",(long)body.UID,"now",SpunGoldAppearance.Clock())!=1)return false;
                r.Stage=5;r.Type=729973;if(p.Inventory.Count<40){box=Q.New(729973,true);Q.Insert(c,t,p.Entity.UID,box);r.Item=box.UID;}
                Save(c,t,p,r,4,new ConquerItem[0]);Ready(p,valid);t.Commit();
            }}if(box!=null)Q.Install(p,new uint[0],new[]{box});return true;
        }
        internal static bool Abandon(GameState p,Guid attempt,Func<bool> context)
        {
            Ready(p,context);using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var r=Read(c,t,p);if(r.Stage!=4 || r.Attempt!=attempt)return false;
                if(Q.Execute(c,t,"UPDATE revora_spun_gold_attempts SET state=2 WHERE id=@id AND uid=@uid AND life=@life AND state=0","id",attempt,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn)!=1)return false;
                r.Stage=3;r.Item=r.Type=0;Save(c,t,p,r,4,new ConquerItem[0]);Ready(p,context);t.Commit();return true;
            }}
        }
        internal static bool Recover(GameState p,Func<bool> context)
        {
            Ready(p,context);ConquerItem item=null;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var r=Read(c,t,p);uint type=r.Stage==2?729971u:r.Stage==3?729972u:r.Stage==5?729973u:r.Stage==6?729974u:0;
                if(type==0)return false;uint old=r.Item;
                if(old!=0){using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE uid=@uid FOR UPDATE","uid",(long)old))if(q.ExecuteScalar()!=null)return false;}
                if(r.Stage==2){if(old==0)return false;r.Item=r.Type=0;}
                else{if(p.Inventory.Count>=40)return false;item=Q.New(type,true);Q.Insert(c,t,p.Entity.UID,item);r.Item=item.UID;r.Type=type;}
                if(Q.Execute(c,t,"INSERT INTO revora_spun_gold_recoveries(id,uid,life,old_uid,new_uid,type) VALUES(@id,@uid,@life,@old,@new,@type)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"old",(long)old,"new",(long)r.Item,"type",(long)type)!=1)throw new InvalidOperationException("Spun Gold recovery receipt failed.");
                Save(c,t,p,r,r.Stage,new ConquerItem[0]);Ready(p,context);t.Commit();
            }}if(item!=null)Q.Install(p,new uint[0],new[]{item});return true;
        }
    }
}
