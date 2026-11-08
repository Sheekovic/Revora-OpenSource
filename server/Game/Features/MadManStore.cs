using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Runtime.CompilerServices;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    // Caller owns the session lease, item lock, NPC/world context and reload fence.
    internal static partial class MadManStore
    {
        internal sealed class Run {internal int Stage;internal uint Item,Type,Soul;}
        internal static Func<int,int> Roll=max=>Kernel.Random.Next(max);
        private static readonly ConditionalWeakTable<Entity,object> deaths=new ConditionalWeakTable<Entity,object>();
        internal static bool Eligible(GameState p){return Q.Player(p) && ReferenceEquals(p.Entity.Owner,p) && CharacterProgression.Managed(p)
            && p.Entity.Level>=60 && p.Entity.Reborn<=2 && p.Trade!=null && !p.Trade.InTrade && p.Booth==null && p.Entity.PokerTable==0;}
        private static void Ready(GameState p,Func<bool> context){if(!Eligible(p) || !Monitor.IsEntered(p.ItemSyncRoot) || context==null || !context())throw new InvalidOperationException("Mad Man context changed.");}
        internal static void Initialize(){
            foreach(uint id in new uint[]{723911,728245,729978,729979,729980,729981,729982,729983,729984,729985,729986,729987,729988,729989})if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Mad Man item catalog missing.");
            MonsterInformation m;if(!MonsterInformation.MonsterInformations.TryGetValue(13,out m) || m.Name!="Snakeman" || m.Guard || m.Reviver || m.Boss)throw new InvalidOperationException("Mad Man hunt catalog changed.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_mad_man(
uid bigint NOT NULL,life smallint NOT NULL CHECK(life BETWEEN 0 AND 2),stage smallint NOT NULL CHECK(stage BETWEEN 1 AND 10),
item_uid bigint NOT NULL DEFAULT 0,item_type bigint NOT NULL DEFAULT 0,soul bigint NOT NULL,rewards bigint[] NOT NULL DEFAULT '{}',PRIMARY KEY(uid,life));
CREATE TABLE IF NOT EXISTS revora_mad_recoveries(
uid bigint NOT NULL,life smallint NOT NULL,old_uid bigint NOT NULL,new_uid bigint NOT NULL,type bigint NOT NULL,
PRIMARY KEY(uid,life,old_uid),FOREIGN KEY(uid,life) REFERENCES revora_mad_man(uid,life));
CREATE TABLE IF NOT EXISTS revora_mad_toxins(
uid bigint NOT NULL,life smallint NOT NULL,type integer NOT NULL CHECK(type BETWEEN 729979 AND 729981),
item_uid bigint NOT NULL,state smallint NOT NULL DEFAULT 0 CHECK(state BETWEEN 0 AND 2),
PRIMARY KEY(uid,life,type),FOREIGN KEY(uid,life) REFERENCES revora_mad_man(uid,life));
CREATE UNIQUE INDEX IF NOT EXISTS revora_mad_selected_toxin ON revora_mad_toxins(uid,life) WHERE state=1;
CREATE TABLE IF NOT EXISTS revora_mad_mixes(
id uuid PRIMARY KEY,uid bigint NOT NULL,life smallint NOT NULL,first_type integer NOT NULL,second_type integer NOT NULL,
first_uid bigint NOT NULL,second_uid bigint NOT NULL,outcome smallint NOT NULL CHECK(outcome BETWEEN 2 AND 7),
mixed_at timestamptz NOT NULL,applied boolean NOT NULL DEFAULT false,FOREIGN KEY(uid,life) REFERENCES revora_mad_man(uid,life));
CREATE TABLE IF NOT EXISTS revora_mad_trials(
id uuid PRIMARY KEY,uid bigint NOT NULL,life smallint NOT NULL,map integer NOT NULL,started_at timestamptz NOT NULL,
until_at timestamptz NOT NULL,state smallint NOT NULL DEFAULT 0 CHECK(state BETWEEN 0 AND 2),mask smallint NOT NULL DEFAULT 0 CHECK(mask BETWEEN 0 AND 127),
CHECK(until_at>started_at),FOREIGN KEY(uid,life) REFERENCES revora_mad_man(uid,life));
CREATE UNIQUE INDEX IF NOT EXISTS revora_mad_trial_active ON revora_mad_trials(uid,life) WHERE state=0;
CREATE TABLE IF NOT EXISTS revora_mad_powders(attempt uuid NOT NULL REFERENCES revora_mad_trials(id),colour smallint NOT NULL CHECK(colour BETWEEN 0 AND 6),item_uid bigint NOT NULL,PRIMARY KEY(attempt,colour));");}
        }
        private static Run Read(NpgsqlConnection c,NpgsqlTransaction t,GameState p){
            using(var q=Q.Command(c,t,"SELECT stage,item_uid,item_type,soul FROM revora_mad_man WHERE uid=@uid AND life=@life FOR UPDATE","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn))
            using(var r=q.ExecuteReader())return r.Read()?new Run{Stage=r.GetInt16(0),Item=checked((uint)r.GetInt64(1)),Type=checked((uint)r.GetInt64(2)),Soul=checked((uint)r.GetInt64(3))}:new Run();
        }
        internal static Run Load(GameState p){if(!Q.Player(p,false))return null;lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();return Read(c,null,p);}}
        private static void Owner(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,GameState p)
        {
            SkillTable.LockOwner(c,t,p);
            using(var q=Q.Command(c,t,"SELECT reborn,level FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))using(var r=q.ExecuteReader())
                if(!r.Read() || Convert.ToInt32(r[0])!=p.Entity.Reborn || Convert.ToInt32(r[1])!=p.Entity.Level)throw new InvalidOperationException("Mad Man saved owner changed.");
            var saved=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid FOR UPDATE","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())while(r.Read())saved.Add(checked((uint)r.GetInt64(0)));
            if(!saved.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Mad Man bag changed.");
        }
        private static bool Material(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Run run,uint type){ConquerItem item;
            if(run.Item==0 || run.Type!=type || !p.Inventory.TryGetItem(run.Item,out item) || item.Lock!=0 || item.Suspicious || item.InWardrobe || item.Days!=0 || item.TimeLeftInMinutes!=0 || !Q.Owns(c,t,p,run.Item,type))return false;
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM items WHERE uid=@id AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0')","id",(long)run.Item))return Equals(q.ExecuteScalar(),true);
        }
        private static void Save(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Run run,int before){
            if(Q.Execute(c,t,"UPDATE revora_mad_man SET stage=@stage,item_uid=@item,item_type=@type WHERE uid=@uid AND life=@life AND stage=@before",
                "stage",run.Stage,"item",(long)run.Item,"type",(long)run.Type,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"before",before)!=1)throw new InvalidOperationException("Mad Man progress changed.");
        }
        // 0 Chang acceptance, 1 Du referral, 2 Judd referral, 3 Judd fang inspection,
        // 4 Dong extraction. The NPC coordinator supplies the exact context.
        internal static bool Advance(GameState p,int action,Func<bool> context){
            Ready(p,context);uint removed=0;var added=new List<ConquerItem>();ulong silver=p.Entity.Money;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(action<0 || action>4 || run.Stage!=action)return false;
                if(action==0){
                    if(Q.Execute(c,t,"INSERT INTO revora_mad_man(uid,life,stage,soul) VALUES(@uid,@life,1,@soul)","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"soul",(long)QuestSoulReward.Create().ID)!=1)throw new InvalidOperationException("Mad Man acceptance failed.");
                }else{
                    if(action>=3 && !Material(c,t,p,run,729978))return false;
                    if(action==4){
                        if(p.Inventory.Count>38)return false;removed=run.Item;Q.Consume(c,t,p,removed,729978);
                        foreach(uint type in new uint[]{729979,729980,729981}){
                            var item=Q.New(type,true);Q.Insert(c,t,p.Entity.UID,item);added.Add(item);
                            if(Q.Execute(c,t,"INSERT INTO revora_mad_toxins(uid,life,type,item_uid) VALUES(@uid,@life,@type,@item)","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"type",(int)type,"item",(long)item.UID)!=1)throw new InvalidOperationException("Mad Man toxin tracking failed.");
                        }run.Item=run.Type=0;
                    }
                    SilverBalance.AssertSaved(c,t,p);silver=checked(silver+100);if(silver>Entity.MaximumSilver)return false;SilverBalance.Persist(c,t,p,silver);
                    run.Stage++;Save(c,t,p,run,action);
                }Ready(p,context);t.Commit();
            }}Q.Install(p,removed==0?new uint[0]:new[]{removed},added);if(action>0)SilverBalance.ApplyCommitted(p,silver);return true;
        }
        internal static void Revived(Entity body){if(body!=null && !body.Dead)lock(body)deaths.Remove(body);}
        internal static bool Hunt(GameState p,Entity body,Func<bool> context){
            Ready(p,context);if(body==null)return false;
            lock(body){object used;if(body.MapID!=1020 || body.MonsterInfo==null || body.MonsterInfo.ID!=13
                || !DailySoulBall.WildernessDeath(body,p.Entity) || !ReferenceEquals(body.Killer,p.Entity) || Kernel.GetDistance(p.Entity.X,p.Entity.Y,body.X,body.Y)>Constants.pScreenDistance || deaths.TryGetValue(body,out used))return false;
                deaths.Add(body,new object());ConquerItem added;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Owner(c,t,p);var run=Read(c,t,p);if((run.Stage!=3 && run.Stage!=4 && run.Stage!=5) || run.Item!=0 || p.Inventory.Count>=40)return false;
                    if(run.Stage==5 && !MayReplenish(c,t,p))return false;
                    int roll=Roll(100);if(roll<0 || roll>=100)throw new InvalidOperationException("Mad Man roll outside range.");if(roll>=50)return false;
                    added=Q.New(729978,true);Q.Insert(c,t,p.Entity.UID,added);run.Item=added.UID;run.Type=added.ID;Save(c,t,p,run,run.Stage);
                    Ready(p,()=>context() && DailySoulBall.WildernessDeath(body,p.Entity) && ReferenceEquals(body.Killer,p.Entity));t.Commit();
                }}Q.Install(p,new uint[0],new[]{added});return true;
            }
        }
        internal static bool Recover(GameState p,Func<bool> context){
            Ready(p,context);ConquerItem added=null;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);uint type=0,old=0;
                if(run.Stage==3 || run.Stage==4 || run.Stage==5 && run.Type==729978){type=729978;old=run.Item;if(old==0 || run.Type!=type)return false;
                    using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE uid=@uid FOR UPDATE","uid",(long)old))if(q.ExecuteScalar()!=null)return false;
                    run.Item=run.Type=0;Save(c,t,p,run,run.Stage);
                }else if(run.Stage==5){
                    if(p.Inventory.Count>=40)return false;
                    using(var q=Q.Command(c,t,"SELECT type,item_uid FROM revora_mad_toxins WHERE uid=@uid AND life=@life AND state=0 AND NOT EXISTS(SELECT 1 FROM items WHERE uid=item_uid) ORDER BY type LIMIT 1 FOR UPDATE","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn))using(var r=q.ExecuteReader())if(r.Read()){type=checked((uint)r.GetInt32(0));old=checked((uint)r.GetInt64(1));}
                    if(old==0)return false;added=Q.New(type,true);Q.Insert(c,t,p.Entity.UID,added);
                    if(Q.Execute(c,t,"UPDATE revora_mad_toxins SET item_uid=@new WHERE uid=@uid AND life=@life AND type=@type AND item_uid=@old AND state=0","new",(long)added.UID,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"type",(int)type,"old",(long)old)!=1)throw new InvalidOperationException("Mad Man toxin recovery changed.");
                }else if(run.Stage==7){
                    type=729982;old=run.Item;if(old==0 || run.Type!=type || p.Inventory.Count>=40)return false;
                    using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE uid=@uid FOR UPDATE","uid",(long)old))if(q.ExecuteScalar()!=null)return false;
                    using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_mad_trials WHERE uid=@uid AND life=@life AND state=1 AND mask=127)","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn))if(!Equals(q.ExecuteScalar(),true))return false;
                    added=Q.New(type,true);Q.Insert(c,t,p.Entity.UID,added);run.Item=added.UID;Save(c,t,p,run,7);
                }else return false;
                if(Q.Execute(c,t,"INSERT INTO revora_mad_recoveries(uid,life,old_uid,new_uid,type) VALUES(@uid,@life,@old,@new,@type)","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"old",(long)old,"new",added==null?0L:(long)added.UID,"type",(long)type)!=1)throw new InvalidOperationException("Mad Man recovery receipt failed.");
                Ready(p,context);t.Commit();
            }}if(added!=null)Q.Install(p,new uint[0],new[]{added});return true;
        }
    }
}
