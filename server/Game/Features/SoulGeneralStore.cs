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
    internal static partial class SoulGeneralStore
    {
        internal sealed class Run {internal int Stage,Snake,Rock,Ape;internal uint Item,Type,Soul;}
        private static readonly ConditionalWeakTable<Entity,object> deaths=new ConditionalWeakTable<Entity,object>();
        internal static bool Eligible(GameState p){return Q.Player(p) && ReferenceEquals(p.Entity.Owner,p) && CharacterProgression.Managed(p)
            && p.Entity.Level>=55 && p.Entity.Reborn<=2 && p.Trade!=null && !p.Trade.InTrade && p.Booth==null && p.Entity.PokerTable==0;}
        private static void Ready(GameState p,Func<bool> context){if(!Eligible(p) || !Monitor.IsEntered(p.ItemSyncRoot) || context==null || !context())throw new InvalidOperationException("Soul General context changed.");}
        internal static void Initialize(){
            foreach(uint id in new uint[]{728244,721703,721704,721705,721706})if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Soul General item catalog missing.");
            // Exact families are checked against the loaded server catalog before enabling hunts.
            uint[] ids={13,106,70};string[] names={"Snakeman","RockMonsterL42","GiantApeL53"};for(int i=0;i<ids.Length;i++){
                MonsterInformation m;if(!MonsterInformation.MonsterInformations.TryGetValue(ids[i],out m) || m.Name!=names[i] || m.Guard || m.Reviver || m.Boss)throw new InvalidOperationException("Soul General hunt catalog changed: "+ids[i]);}
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_soul_general(
uid bigint NOT NULL,life smallint NOT NULL CHECK(life BETWEEN 0 AND 2),stage smallint NOT NULL CHECK(stage BETWEEN 1 AND 12),
snake smallint NOT NULL DEFAULT 0 CHECK(snake BETWEEN 0 AND 1),rock smallint NOT NULL DEFAULT 0 CHECK(rock BETWEEN 0 AND 10),ape smallint NOT NULL DEFAULT 0 CHECK(ape BETWEEN 0 AND 10),
item_uid bigint NOT NULL DEFAULT 0,item_type bigint NOT NULL DEFAULT 0,soul bigint NOT NULL,rewards bigint[] NOT NULL DEFAULT '{}',PRIMARY KEY(uid,life));
CREATE TABLE IF NOT EXISTS revora_soul_general_recoveries(
uid bigint NOT NULL,life smallint NOT NULL,old_uid bigint NOT NULL,new_uid bigint NOT NULL,type bigint NOT NULL,
PRIMARY KEY(uid,life,old_uid),FOREIGN KEY(uid,life) REFERENCES revora_soul_general(uid,life));
CREATE TABLE IF NOT EXISTS revora_soul_phantoms(
id uuid PRIMARY KEY,uid bigint NOT NULL,life smallint NOT NULL,kills smallint NOT NULL DEFAULT 0 CHECK(kills BETWEEN 0 AND 1),
state smallint NOT NULL DEFAULT 0 CHECK(state BETWEEN 0 AND 3),next_use timestamptz NOT NULL,until_at timestamptz NOT NULL,
FOREIGN KEY(uid,life) REFERENCES revora_soul_general(uid,life));
CREATE UNIQUE INDEX IF NOT EXISTS revora_soul_phantom_open ON revora_soul_phantoms(uid,life) WHERE state IN(0,1);
CREATE TABLE IF NOT EXISTS revora_soul_phantom_kills(attempt uuid NOT NULL REFERENCES revora_soul_phantoms(id),body bigint NOT NULL,PRIMARY KEY(attempt,body));");}
        }
        private static Run Read(NpgsqlConnection c,NpgsqlTransaction t,GameState p){
            using(var q=Q.Command(c,t,"SELECT stage,snake,rock,ape,item_uid,item_type,soul FROM revora_soul_general WHERE uid=@uid AND life=@life FOR UPDATE","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn))
            using(var r=q.ExecuteReader())return r.Read()?new Run{Stage=r.GetInt16(0),Snake=r.GetInt16(1),Rock=r.GetInt16(2),Ape=r.GetInt16(3),Item=checked((uint)r.GetInt64(4)),Type=checked((uint)r.GetInt64(5)),Soul=checked((uint)r.GetInt64(6))}:new Run();
        }
        internal static Run Load(GameState p){if(!Q.Player(p,false))return null;lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();return Read(c,null,p);}}
        private static void Owner(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,GameState p)
        {
            SkillTable.LockOwner(c,t,p);
            using(var q=Q.Command(c,t,"SELECT reborn,level FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))using(var r=q.ExecuteReader())
                if(!r.Read() || Convert.ToInt32(r[0])!=p.Entity.Reborn || Convert.ToInt32(r[1])!=p.Entity.Level)throw new InvalidOperationException("Soul General saved owner changed.");
            var saved=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid FOR UPDATE","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())while(r.Read())saved.Add(checked((uint)r.GetInt64(0)));
            if(!saved.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Soul General bag changed.");
        }
        private static bool Material(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Run run,uint type){ConquerItem item;
            if(run.Item==0 || run.Type!=type || !p.Inventory.TryGetItem(run.Item,out item) || item.Lock!=0 || item.Suspicious || item.InWardrobe || item.Days!=0 || item.TimeLeftInMinutes!=0 || !Q.Owns(c,t,p,run.Item,type))return false;
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM items WHERE uid=@id AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0')","id",(long)run.Item))return Equals(q.ExecuteScalar(),true);
        }
        private static void Save(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Run run,int before){
            if(Q.Execute(c,t,"UPDATE revora_soul_general SET stage=@stage,item_uid=@item,item_type=@type,snake=@snake,rock=@rock,ape=@ape WHERE uid=@uid AND life=@life AND stage=@before",
                "snake",run.Snake,"rock",run.Rock,"ape",run.Ape,"stage",run.Stage,"item",(long)run.Item,"type",(long)run.Type,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"before",before)!=1)throw new InvalidOperationException("Soul General progress changed.");
        }
        // Stage/action: 0 Chang, 1 Amber, 2 Zhao, 3 Amber, 4 Amber scent hand-in,
        // 5 Painter, 6 Amber portrait, 7 Zhao illness, 8 Guard referral.
        internal static bool Advance(GameState p,int action,Func<bool> context){
            Ready(p,context);uint removed=0;ConquerItem added=null;ulong silver=p.Entity.Money;bool paid=false;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(action<0 || action>8 || run.Stage!=action)return false;
                if(action==0){
                    if(Q.Execute(c,t,"INSERT INTO revora_soul_general(uid,life,stage,soul) VALUES(@uid,@life,1,@soul)","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"soul",(long)QuestSoulReward.Create().ID)!=1)throw new InvalidOperationException("Soul General acceptance failed.");
                }else{
                    uint consume=action==3?721703u:action==4?721704u:action==6?721705u:0;
                    uint issue=action==2?721703u:action==3?721704u:action==5?721705u:0;
                    if(action==4 && (run.Snake!=1 || run.Rock!=10 || run.Ape!=10))return false;
                    if(consume!=0){if(!Material(c,t,p,run,consume))return false;removed=run.Item;Q.Consume(c,t,p,removed,consume);run.Item=run.Type=0;}
                    if(issue!=0){if(removed==0 && p.Inventory.Count>=40)return false;added=Q.New(issue,true);Q.Insert(c,t,p.Entity.UID,added);run.Item=added.UID;run.Type=added.ID;}
                    paid=action==1 || action==3 || action==4 || action==6 || action==8;
                    if(paid){SilverBalance.AssertSaved(c,t,p);silver=checked(silver+100);if(silver>Entity.MaximumSilver)return false;SilverBalance.Persist(c,t,p,silver);}
                    run.Stage++;Save(c,t,p,run,action);
                }Ready(p,context);t.Commit();
            }}Q.Install(p,removed==0?new uint[0]:new[]{removed},added==null?new ConquerItem[0]:new[]{added});if(paid)SilverBalance.ApplyCommitted(p,silver);return true;
        }
        internal static void Revived(Entity body){if(body!=null && !body.Dead)lock(body)deaths.Remove(body);}
        internal static bool Hunt(GameState p,Entity body,Func<bool> context){
            Ready(p,context);if(body==null)return false;
            lock(body){object used;uint family=body.MonsterInfo==null?0:body.MonsterInfo.ID;
                if((family!=13 || body.MapID!=1020) && (family!=106 || body.MapID!=1011) && (family!=70 || body.MapID!=1020))return false;
                if(!DailySoulBall.WildernessDeath(body,p.Entity) || !ReferenceEquals(body.Killer,p.Entity) || Kernel.GetDistance(p.Entity.X,p.Entity.Y,body.X,body.Y)>Constants.pScreenDistance || deaths.TryGetValue(body,out used))return false;
                deaths.Add(body,new object());
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=4 || !Material(c,t,p,run,721704))return false;
                    if(family==13){if(run.Snake>=1)return false;run.Snake++;}
                    else if(family==106){if(run.Rock>=10)return false;run.Rock++;}
                    else{if(run.Ape>=10)return false;run.Ape++;}
                    Save(c,t,p,run,4);Ready(p,()=>context() && DailySoulBall.WildernessDeath(body,p.Entity) && ReferenceEquals(body.Killer,p.Entity));t.Commit();
                }}return true;
            }
        }
        internal static bool Recover(GameState p,Func<bool> context){
            Ready(p,context);ConquerItem added;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);uint type=run.Stage==3?721703u:run.Stage==4?721704u:run.Stage==6?721705u:run.Stage==10?721706u:0;
                if(type==0 || run.Item==0 || run.Type!=type || p.Inventory.Count>=40)return false;uint old=run.Item;
                using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE uid=@uid FOR UPDATE","uid",(long)old))if(q.ExecuteScalar()!=null)return false;
                added=Q.New(type,true);Q.Insert(c,t,p.Entity.UID,added);run.Item=added.UID;
                if(Q.Execute(c,t,"INSERT INTO revora_soul_general_recoveries(uid,life,old_uid,new_uid,type) VALUES(@uid,@life,@old,@new,@type)","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"old",(long)old,"new",(long)added.UID,"type",(long)type)!=1)throw new InvalidOperationException("Soul General recovery receipt failed.");
                Save(c,t,p,run,run.Stage);Ready(p,context);t.Commit();
            }}Q.Install(p,new uint[0],new[]{added});return true;
        }
    }
}
