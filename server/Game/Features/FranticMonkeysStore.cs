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
    internal static partial class FranticMonkeysStore
    {
        internal sealed class Run {internal int Stage,Kills;internal uint Item,Type,Soul;}
        internal static Func<int,int> Roll=max=>Kernel.Random.Next(max);
        private static readonly ConditionalWeakTable<Entity,object> deaths=new ConditionalWeakTable<Entity,object>();
        internal static bool Eligible(GameState p){return Q.Player(p) && ReferenceEquals(p.Entity.Owner,p) && CharacterProgression.Managed(p)
            && p.Entity.Level>=50 && p.Entity.Reborn<=2 && p.Trade!=null && !p.Trade.InTrade && p.Booth==null && p.Entity.PokerTable==0;}
        private static void Ready(GameState p,Func<bool> context){if(!Eligible(p) || !Monitor.IsEntered(p.ItemSyncRoot) || context==null || !context())throw new InvalidOperationException("Frantic context changed.");}
        internal static void Initialize(){
            foreach(uint id in new uint[]{728243,721699,721700,721701,721702})if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Frantic item catalog missing.");
            uint[] ids={10,69};string[] names={"Macaque","MacaqueL48"};for(int i=0;i<ids.Length;i++){
                MonsterInformation m;if(!MonsterInformation.MonsterInformations.TryGetValue(ids[i],out m) || m.Name!=names[i] || m.Guard || m.Reviver || m.Boss)throw new InvalidOperationException("Frantic hunt catalog changed.");}
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_frantic_monkeys(
uid bigint NOT NULL,life smallint NOT NULL CHECK(life BETWEEN 0 AND 2),stage smallint NOT NULL CHECK(stage BETWEEN 1 AND 10),
kills smallint NOT NULL DEFAULT 0 CHECK(kills BETWEEN 0 AND 5),item_uid bigint NOT NULL DEFAULT 0,item_type bigint NOT NULL DEFAULT 0,
soul bigint NOT NULL,rewards bigint[] NOT NULL DEFAULT '{}',PRIMARY KEY(uid,life));
CREATE TABLE IF NOT EXISTS revora_frantic_recoveries(
uid bigint NOT NULL,life smallint NOT NULL,old_uid bigint NOT NULL,new_uid bigint NOT NULL,type bigint NOT NULL,
PRIMARY KEY(uid,life,old_uid),FOREIGN KEY(uid,life) REFERENCES revora_frantic_monkeys(uid,life));
CREATE TABLE IF NOT EXISTS revora_frantic_trials(
id uuid PRIMARY KEY,uid bigint NOT NULL,life smallint NOT NULL,map integer NOT NULL,
ready_at timestamptz NOT NULL,until_at timestamptz NOT NULL,state smallint NOT NULL DEFAULT 0 CHECK(state BETWEEN 0 AND 2),
CHECK(until_at>ready_at),FOREIGN KEY(uid,life) REFERENCES revora_frantic_monkeys(uid,life));
CREATE UNIQUE INDEX IF NOT EXISTS revora_frantic_trial_active ON revora_frantic_trials(uid,life) WHERE state=0;
CREATE TABLE IF NOT EXISTS revora_frantic_trial_kills(
attempt uuid NOT NULL REFERENCES revora_frantic_trials(id),body bigint NOT NULL,PRIMARY KEY(attempt,body));
CREATE TABLE IF NOT EXISTS revora_frantic_apes(
id uuid PRIMARY KEY,uid bigint NOT NULL,life smallint NOT NULL,kills smallint NOT NULL DEFAULT 0 CHECK(kills BETWEEN 0 AND 3),
state smallint NOT NULL DEFAULT 0 CHECK(state BETWEEN 0 AND 3),next_use timestamptz NOT NULL,until_at timestamptz NOT NULL,
FOREIGN KEY(uid,life) REFERENCES revora_frantic_monkeys(uid,life));
CREATE UNIQUE INDEX IF NOT EXISTS revora_frantic_ape_open ON revora_frantic_apes(uid,life) WHERE state IN(0,1);
CREATE TABLE IF NOT EXISTS revora_frantic_ape_kills(
attempt uuid NOT NULL REFERENCES revora_frantic_apes(id),body bigint NOT NULL,kind smallint NOT NULL CHECK(kind IN(1,2)),PRIMARY KEY(attempt,body));");}
        }
        private static Run Read(NpgsqlConnection c,NpgsqlTransaction t,GameState p){
            using(var q=Q.Command(c,t,"SELECT stage,kills,item_uid,item_type,soul FROM revora_frantic_monkeys WHERE uid=@uid AND life=@life FOR UPDATE","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn))
            using(var r=q.ExecuteReader())return r.Read()?new Run{Stage=r.GetInt16(0),Kills=r.GetInt16(1),Item=checked((uint)r.GetInt64(2)),Type=checked((uint)r.GetInt64(3)),Soul=checked((uint)r.GetInt64(4))}:new Run();
        }
        internal static Run Load(GameState p){if(!Q.Player(p,false))return null;lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();return Read(c,null,p);}}
        private static void Owner(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,GameState p)
        {
            SkillTable.LockOwner(c,t,p);
            using(var q=Q.Command(c,t,"SELECT reborn,level FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))using(var r=q.ExecuteReader())
                if(!r.Read() || Convert.ToInt32(r[0])!=p.Entity.Reborn || Convert.ToInt32(r[1])!=p.Entity.Level)throw new InvalidOperationException("Frantic Monkeys saved owner changed.");
            var saved=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid FOR UPDATE","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())while(r.Read())saved.Add(checked((uint)r.GetInt64(0)));
            if(!saved.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Frantic Monkeys bag changed.");
        }
        private static bool Material(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Run run,uint type){ConquerItem item;
            if(run.Item==0 || run.Type!=type || !p.Inventory.TryGetItem(run.Item,out item) || item.Lock!=0 || item.Suspicious || item.InWardrobe || item.Days!=0 || item.TimeLeftInMinutes!=0 || !Q.Owns(c,t,p,run.Item,type))return false;
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM items WHERE uid=@id AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0')","id",(long)run.Item))return Equals(q.ExecuteScalar(),true);
        }
        private static void Save(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Run run,int before){
            if(Q.Execute(c,t,"UPDATE revora_frantic_monkeys SET stage=@stage,item_uid=@item,item_type=@type WHERE uid=@uid AND life=@life AND stage=@before",
                "stage",run.Stage,"item",(long)run.Item,"type",(long)run.Type,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"before",before)!=1)throw new InvalidOperationException("Frantic progress changed.");
        }
        // 0 Chang acceptance, 1 Bao referral, 2 Bao heart inspection, 3 Li poison exchange.
        internal static bool Advance(GameState p,int action,Func<bool> context){
            Ready(p,context);uint removed=0;ConquerItem added=null;ulong silver=p.Entity.Money;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(action<0 || action>3 || run.Stage!=action)return false;
                if(action==0){
                    if(Q.Execute(c,t,"INSERT INTO revora_frantic_monkeys(uid,life,stage,soul) VALUES(@uid,@life,1,@soul)","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"soul",(long)QuestSoulReward.Create().ID)!=1)throw new InvalidOperationException("Frantic acceptance failed.");
                }else{
                    if(action>=2 && !Material(c,t,p,run,721699))return false;
                    if(action==3){removed=run.Item;Q.Consume(c,t,p,removed,721699);added=Q.New(721700,true);Q.Insert(c,t,p.Entity.UID,added);run.Item=added.UID;run.Type=added.ID;}
                    SilverBalance.AssertSaved(c,t,p);silver=checked(silver+100);if(silver>Entity.MaximumSilver)return false;SilverBalance.Persist(c,t,p,silver);
                    run.Stage++;Save(c,t,p,run,action);
                }Ready(p,context);t.Commit();
            }}Q.Install(p,removed==0?new uint[0]:new[]{removed},added==null?new ConquerItem[0]:new[]{added});if(action>0)SilverBalance.ApplyCommitted(p,silver);return true;
        }
        internal static void Revived(Entity body){if(body!=null && !body.Dead)lock(body)deaths.Remove(body);}
        internal static bool Hunt(GameState p,Entity body,Func<bool> context){
            Ready(p,context);if(body==null)return false;
            lock(body){object used;if(body.MapID!=1020 || body.MonsterInfo==null || body.MonsterInfo.ID!=10 && body.MonsterInfo.ID!=69
                || !DailySoulBall.WildernessDeath(body,p.Entity) || !ReferenceEquals(body.Killer,p.Entity) || Kernel.GetDistance(p.Entity.X,p.Entity.Y,body.X,body.Y)>Constants.pScreenDistance || deaths.TryGetValue(body,out used))return false;
                deaths.Add(body,new object());ConquerItem added;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Owner(c,t,p);var run=Read(c,t,p);if((run.Stage!=2 && run.Stage!=3) || run.Item!=0 || p.Inventory.Count>=40)return false;
                    int roll=Roll(100);if(roll<0 || roll>=100)throw new InvalidOperationException("Frantic roll outside range.");if(roll>=50)return false;
                    added=Q.New(721699,true);Q.Insert(c,t,p.Entity.UID,added);run.Item=added.UID;run.Type=added.ID;Save(c,t,p,run,run.Stage);
                    Ready(p,()=>context() && DailySoulBall.WildernessDeath(body,p.Entity) && ReferenceEquals(body.Killer,p.Entity));t.Commit();
                }}Q.Install(p,new uint[0],new[]{added});return true;
            }
        }
        internal static bool Recover(GameState p,Func<bool> context){
            Ready(p,context);ConquerItem added=null;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);uint type=run.Stage==2 || run.Stage==3?721699u:run.Stage==4?721700u:run.Stage==7?721701u:run.Stage==8?721702u:0;
                if(type==0 || run.Item==0 || run.Type!=type)return false;uint old=run.Item;
                if(run.Stage==7)using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_frantic_apes WHERE uid=@uid AND life=@life AND state=0)","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn))if(Equals(q.ExecuteScalar(),true))return false;
                using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE uid=@uid FOR UPDATE","uid",(long)old))if(q.ExecuteScalar()!=null)return false;
                if(type!=721699){if(p.Inventory.Count>=40)return false;added=Q.New(type,true);Q.Insert(c,t,p.Entity.UID,added);run.Item=added.UID;}
                else run.Item=run.Type=0; // Missing hearts must be hunted again; Bao's inspection remains saved.
                if(Q.Execute(c,t,"INSERT INTO revora_frantic_recoveries(uid,life,old_uid,new_uid,type) VALUES(@uid,@life,@old,@new,@type)","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"old",(long)old,"new",added==null?0L:(long)added.UID,"type",(long)type)!=1)throw new InvalidOperationException("Frantic recovery receipt failed.");
                Save(c,t,p,run,run.Stage);Ready(p,context);t.Commit();
            }}if(added!=null)Q.Install(p,new uint[0],new[]{added});return true;
        }
    }
}
