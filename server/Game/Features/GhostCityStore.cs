using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    // NPC/item coordinator supplies ordinary proximity and session ownership.
    internal static partial class GhostCityStore
    {
        internal sealed class Run {internal int Stage,Sparks;internal uint Item,Type,Charm,Soul;}
        internal static readonly ushort[,] FireworkPoints={{440,308},{443,344},{465,356}};
        internal static bool Eligible(GameState p){return Q.Player(p) && ReferenceEquals(p.Entity.Owner,p) && CharacterProgression.Managed(p)
            && p.Entity.Level>=75 && p.Entity.Reborn<=2 && p.Trade!=null && !p.Trade.InTrade && p.Booth==null && p.Entity.PokerTable==0;}
        private static void Ready(GameState p,Func<bool> context){if(!Eligible(p) || !Monitor.IsEntered(p.ItemSyncRoot) || context==null || !context())throw new InvalidOperationException("Ghost City context changed.");}
        internal static void Initialize(){
            foreach(uint id in new uint[]{728246,721717,721712,721713,721714,723911})if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Ghost City item catalog missing.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_ghost_city(
uid bigint NOT NULL,life smallint NOT NULL CHECK(life BETWEEN 0 AND 2),stage smallint NOT NULL CHECK(stage BETWEEN 1 AND 11),
sparks smallint NOT NULL DEFAULT 0 CHECK(sparks BETWEEN 0 AND 3),item_uid bigint NOT NULL DEFAULT 0,item_type bigint NOT NULL DEFAULT 0,
charm_uid bigint NOT NULL DEFAULT 0,soul bigint NOT NULL,rewards bigint[] NOT NULL DEFAULT '{}',PRIMARY KEY(uid,life));
CREATE TABLE IF NOT EXISTS revora_ghost_recoveries(
uid bigint NOT NULL,life smallint NOT NULL,old_uid bigint NOT NULL,new_uid bigint NOT NULL,type bigint NOT NULL,
PRIMARY KEY(uid,life,old_uid),FOREIGN KEY(uid,life) REFERENCES revora_ghost_city(uid,life));
CREATE TABLE IF NOT EXISTS revora_ghost_combat(uid bigint NOT NULL,life smallint NOT NULL,
 kills smallint NOT NULL DEFAULT 0 CHECK(kills BETWEEN 0 AND 102),PRIMARY KEY(uid,life),
 FOREIGN KEY(uid,life) REFERENCES revora_ghost_city(uid,life));
CREATE TABLE IF NOT EXISTS revora_ghost_kills(visit uuid NOT NULL,body bigint NOT NULL,
 uid bigint NOT NULL,life smallint NOT NULL,kind smallint NOT NULL CHECK(kind BETWEEN 0 AND 3),
 PRIMARY KEY(visit,body),FOREIGN KEY(uid,life) REFERENCES revora_ghost_city(uid,life));
CREATE TABLE IF NOT EXISTS revora_ghost_children(uid bigint NOT NULL,life smallint NOT NULL,
 child smallint NOT NULL CHECK(child BETWEEN 0 AND 4),PRIMARY KEY(uid,life,child),
 FOREIGN KEY(uid,life) REFERENCES revora_ghost_city(uid,life));");}
        }
        private static Run Read(NpgsqlConnection c,NpgsqlTransaction t,GameState p){
            using(var q=Q.Command(c,t,"SELECT stage,sparks,item_uid,item_type,charm_uid,soul FROM revora_ghost_city WHERE uid=@uid AND life=@life FOR UPDATE","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn))
            using(var r=q.ExecuteReader())return r.Read()?new Run{Stage=r.GetInt16(0),Sparks=r.GetInt16(1),Item=checked((uint)r.GetInt64(2)),Type=checked((uint)r.GetInt64(3)),Charm=checked((uint)r.GetInt64(4)),Soul=checked((uint)r.GetInt64(5))}:new Run();
        }
        internal static Run Load(GameState p){if(!Q.Player(p,false))return null;lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();return Read(c,null,p);}}
        private static void Owner(NpgsqlConnection c,NpgsqlTransaction t,GameState p){
            SkillTable.LockOwner(c,t,p);
            using(var q=Q.Command(c,t,"SELECT reborn,level FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))using(var r=q.ExecuteReader())
                if(!r.Read() || Convert.ToInt32(r[0])!=p.Entity.Reborn || Convert.ToInt32(r[1])!=p.Entity.Level)throw new InvalidOperationException("Ghost City saved owner changed.");
            var saved=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid FOR UPDATE","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())while(r.Read())saved.Add(checked((uint)r.GetInt64(0)));
            if(!saved.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Ghost City bag changed.");
        }
        private static bool Material(NpgsqlConnection c,NpgsqlTransaction t,GameState p,uint uid,uint type){ConquerItem item;
            if(uid==0 || !p.Inventory.TryGetItem(uid,out item) || item.Lock!=0 || item.Suspicious || item.InWardrobe || item.Days!=0 || item.TimeLeftInMinutes!=0 || !Q.Owns(c,t,p,uid,type))return false;
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM items WHERE uid=@id AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0')","id",(long)uid))return Equals(q.ExecuteScalar(),true);
        }
        private static void Save(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Run run,int before){
            if(Q.Execute(c,t,"UPDATE revora_ghost_city SET stage=@stage,sparks=@sparks,item_uid=@item,item_type=@type,charm_uid=@charm WHERE uid=@uid AND life=@life AND stage=@before",
                "stage",run.Stage,"sparks",run.Sparks,"item",(long)run.Item,"type",(long)run.Type,"charm",(long)run.Charm,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"before",before)!=1)throw new InvalidOperationException("Ghost City progress changed.");
        }
        // Chang acceptance, Aunt referral, Wang's firework, report three spots, Shine's charm.
        internal static bool Advance(GameState p,int action,Func<bool> context){
            Ready(p,context);uint removed=0;ConquerItem added=null;ulong silver=p.Entity.Money;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(action<0 || action>4 || run.Stage!=action)return false;
                if(action==0){
                    if(Q.Execute(c,t,"INSERT INTO revora_ghost_city(uid,life,stage,soul) VALUES(@uid,@life,1,@soul)","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"soul",(long)QuestSoulReward.Create().ID)!=1)throw new InvalidOperationException("Ghost City acceptance failed.");
                }else{
                    if(action==3){if(run.Sparks!=3 || run.Type!=721717 || !Material(c,t,p,run.Item,721717))return false;removed=run.Item;Q.Consume(c,t,p,removed,721717);run.Item=run.Type=0;}
                    if(action==2 || action==4){if(p.Inventory.Count>=40)return false;added=Q.New(action==2?721717u:721713u,true);Q.Insert(c,t,p.Entity.UID,added);
                        if(action==2){run.Item=added.UID;run.Type=added.ID;}else run.Charm=added.UID;}
                    SilverBalance.AssertSaved(c,t,p);silver=checked(silver+100);if(silver>Entity.MaximumSilver)return false;SilverBalance.Persist(c,t,p,silver);
                    run.Stage++;Save(c,t,p,run,action);
                }Ready(p,context);t.Commit();
            }}Q.Install(p,removed==0?new uint[0]:new[]{removed},added==null?new ConquerItem[0]:new[]{added});if(action>0)SilverBalance.ApplyCommitted(p,silver);return true;
        }
        // The installed 6711 journal marks these three positions in this order.
        internal static bool Firework(GameState p,uint item,Func<bool> context){
            Ready(p,context);using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=3 || run.Sparks>=3 || run.Item!=item || run.Type!=721717 || !Material(c,t,p,item,721717))return false;
                int next=run.Sparks;if(p.Entity.MapID!=1002 || Kernel.GetDistance(p.Entity.X,p.Entity.Y,FireworkPoints[next,0],FireworkPoints[next,1])>3)return false;
                run.Sparks++;Save(c,t,p,run,3);Ready(p,()=>context() && p.Entity.MapID==1002 && Kernel.GetDistance(p.Entity.X,p.Entity.Y,FireworkPoints[next,0],FireworkPoints[next,1])<=3);t.Commit();return true;
            }}
        }
        internal static bool Recover(GameState p,Func<bool> context){
            Ready(p,context);ConquerItem added;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(p.Inventory.Count>=40)return false;
                var candidates=new List<Tuple<uint,uint>>();
                if(run.Stage==3 && run.Type==721717)candidates.Add(Tuple.Create(run.Item,721717u));
                if(run.Stage>=5 && run.Stage<=8){
                    if(run.Stage>=6 && run.Type==721712)candidates.Add(Tuple.Create(run.Item,721712u));
                    if(run.Stage==8 && run.Type==721714)candidates.Add(Tuple.Create(run.Item,721714u));
                    candidates.Add(Tuple.Create(run.Charm,721713u));
                }
                uint old=0,type=0;foreach(var candidate in candidates){if(candidate.Item1==0)continue;
                    using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE uid=@uid FOR UPDATE","uid",(long)candidate.Item1))if(q.ExecuteScalar()!=null)continue;
                    old=candidate.Item1;type=candidate.Item2;break;
                }
                if(old==0)return false;
                if(type==721712){using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_ghost_visits WHERE uid=@uid AND life=@life AND powder_uid>0)","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn))if(!Equals(q.ExecuteScalar(),true))return false;}
                if(type==721714){using(var q=Q.Command(c,t,"SELECT kills FROM revora_ghost_combat WHERE uid=@uid AND life=@life","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn))if(Convert.ToInt32(q.ExecuteScalar())!=102)return false;}
                added=Q.New(type,true);Q.Insert(c,t,p.Entity.UID,added);if(type==721713)run.Charm=added.UID;else run.Item=added.UID;
                if(Q.Execute(c,t,"INSERT INTO revora_ghost_recoveries(uid,life,old_uid,new_uid,type) VALUES(@uid,@life,@old,@new,@type)","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"old",(long)old,"new",(long)added.UID,"type",(long)type)!=1)throw new InvalidOperationException("Ghost City recovery receipt failed.");
                Save(c,t,p,run,run.Stage);Ready(p,context);t.Commit();
            }}Q.Install(p,new uint[0],new[]{added});return true;
        }
    }
}
