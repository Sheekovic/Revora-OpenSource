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
    // Persistence component. The NPC coordinator must own its session lease,
    // item lock, exact NPC context and post-commit reload fence.
    internal static partial class MysticRingStore
    {
        internal sealed class Run {internal int Stage,Kills;internal uint Item,Type,Soul;}
        internal static bool Eligible(GameState p){return Q.Player(p) && ReferenceEquals(p.Entity.Owner,p) && CharacterProgression.Managed(p)
            && p.Entity.Level>=48 && p.Entity.Reborn<=2 && p.Trade!=null && !p.Trade.InTrade && p.Booth==null && p.Entity.PokerTable==0;}
        private static void Ready(GameState p,Func<bool> context){if(!Eligible(p) || !Monitor.IsEntered(p.ItemSyncRoot) || context==null || !context())throw new InvalidOperationException("Mystic Ring context changed.");}
        internal static void Initialize(){
            foreach(uint id in new uint[]{728240,721707,721708,721710,1000000,1001000})if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Mystic Ring catalog missing.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_mystic_ring(
uid bigint NOT NULL,life smallint NOT NULL CHECK(life BETWEEN 0 AND 2),stage smallint NOT NULL CHECK(stage BETWEEN 1 AND 10),
kills smallint NOT NULL DEFAULT 0 CHECK(kills BETWEEN 0 AND 20),item_uid bigint NOT NULL DEFAULT 0,item_type bigint NOT NULL DEFAULT 0,
soul bigint NOT NULL,rewards bigint[] NOT NULL DEFAULT '{}',PRIMARY KEY(uid,life));
CREATE TABLE IF NOT EXISTS revora_mystic_medicine(
uid bigint NOT NULL,life smallint NOT NULL,item_uid bigint NOT NULL,item_type integer NOT NULL CHECK(item_type IN(1000000,1001000)),
before_stack integer NOT NULL CHECK(before_stack BETWEEN 0 AND 65535),taken integer NOT NULL CHECK(taken BETWEEN 1 AND 12 AND taken<=GREATEST(1,before_stack)),
bound boolean NOT NULL,PRIMARY KEY(uid,life,item_uid),FOREIGN KEY(uid,life) REFERENCES revora_mystic_ring(uid,life));
CREATE TABLE IF NOT EXISTS revora_mystic_recoveries(
uid bigint NOT NULL,life smallint NOT NULL,old_uid bigint NOT NULL,new_uid bigint NOT NULL,type bigint NOT NULL,
PRIMARY KEY(uid,life,old_uid),FOREIGN KEY(uid,life) REFERENCES revora_mystic_ring(uid,life));");}
        }
        private static Run Read(NpgsqlConnection c,NpgsqlTransaction t,GameState p){
            using(var q=Q.Command(c,t,"SELECT stage,kills,item_uid,item_type,soul FROM revora_mystic_ring WHERE uid=@uid AND life=@life FOR UPDATE","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn))
            using(var r=q.ExecuteReader())return r.Read()?new Run{Stage=r.GetInt16(0),Kills=r.GetInt16(1),Item=checked((uint)r.GetInt64(2)),Type=checked((uint)r.GetInt64(3)),Soul=checked((uint)r.GetInt64(4))}:new Run();
        }
        internal static Run Load(GameState p){if(!Q.Player(p,false))return null;lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();return Read(c,null,p);}}
        private static void Owner(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,GameState p)
        {
            SkillTable.LockOwner(c,t,p);
            using(var q=Q.Command(c,t,"SELECT reborn,level FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))using(var r=q.ExecuteReader())
                if(!r.Read() || Convert.ToInt32(r[0])!=p.Entity.Reborn || Convert.ToInt32(r[1])!=p.Entity.Level)throw new InvalidOperationException("Mystic Ring saved owner changed.");
            var saved=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid FOR UPDATE","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())while(r.Read())saved.Add(checked((uint)r.GetInt64(0)));
            if(!saved.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Mystic Ring bag changed.");
        }
        private sealed class Medicine {
            internal ConquerItem Item;internal uint Uid,Type;internal ushort Stack,Take;internal bool Bound;
            internal int After {get{return Math.Max(1,(int)Stack)-Take;}}
        }
        private static bool Usable(ConquerItem i){return i!=null && (i.ID==1000000 || i.ID==1001000) && i.Position==0 && i.Warehouse==0
            && i.Lock==0 && !i.Suspicious && !i.InWardrobe && i.Days==0 && i.TimeLeftInMinutes==0;}
        private static Medicine[] Select(GameState p){
            var parts=new List<Medicine>();foreach(uint type in new uint[]{1000000,1001000}){
                int need=12;foreach(var item in p.Inventory.Objects.Where(i=>i.ID==type && Usable(i)).OrderByDescending(i=>i.Bound).ThenBy(i=>i.UID)){
                    int take=Math.Min(need,Math.Max(1,(int)item.StackSize));parts.Add(new Medicine{Item=item,Uid=item.UID,Type=type,Stack=item.StackSize,Take=(ushort)take,Bound=item.Bound});need-=take;if(need==0)break;
                }if(need!=0)return null;
            }return parts.ToArray();
        }
        private static void Debit(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Medicine[] parts){
            foreach(var part in parts){ConquerItem item;
                if(!p.Inventory.TryGetItem(part.Uid,out item) || !ReferenceEquals(item,part.Item) || !Usable(item) || item.ID!=part.Type || item.StackSize!=part.Stack || item.Bound!=part.Bound)
                    throw new InvalidOperationException("Mystic medicine changed.");
                string sql=part.After==0?"DELETE FROM items":"UPDATE items SET stacksize=@after";
                if(Q.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=@type AND stacksize=@stack AND bound=@bound
AND position=0 AND warehouse=0 AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0'",
                    "after",part.After,"item",(long)part.Uid,"uid",(long)p.Entity.UID,"type",(int)part.Type,"stack",(int)part.Stack,"bound",part.Bound?1:0)!=1)throw new InvalidOperationException("Mystic medicine ownership changed.");
                if(Q.Execute(c,t,"INSERT INTO revora_mystic_medicine(uid,life,item_uid,item_type,before_stack,taken,bound) VALUES(@uid,@life,@item,@type,@stack,@take,@bound)",
                    "uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"item",(long)part.Uid,"type",(int)part.Type,"stack",(int)part.Stack,"take",(int)part.Take,"bound",part.Bound)!=1)throw new InvalidOperationException("Mystic medicine receipt failed.");
            }
        }
        // 0 acceptance, 1 Minstrel referral, 2 Shen referral, 3 medicine hand-in.
        // Caller context must identify the actual eligible NPC before each commit.
        internal static bool Advance(GameState p,int action,Func<bool> context){
            Ready(p,context);Medicine[] parts=null;ulong silver=p.Entity.Money;bool paid=false;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(action<0 || action>3 || run.Stage!=action)return false;
                if(action==0){
                    if(Q.Execute(c,t,"INSERT INTO revora_mystic_ring(uid,life,stage,soul) VALUES(@uid,@life,1,@soul)","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"soul",(long)QuestSoulReward.Create().ID)!=1)throw new InvalidOperationException("Mystic acceptance failed.");
                }else{
                    if(action==3){parts=Select(p);if(parts==null)return false;Debit(c,t,p,parts);}
                    SilverBalance.AssertSaved(c,t,p);silver=checked(silver+100);if(silver>Entity.MaximumSilver)return false;SilverBalance.Persist(c,t,p,silver);paid=true;
                    if(Q.Execute(c,t,"UPDATE revora_mystic_ring SET stage=stage+1 WHERE uid=@uid AND life=@life AND stage=@before","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"before",action)!=1)throw new InvalidOperationException("Mystic stage changed.");
                }Ready(p,context);t.Commit();
            }}
            if(parts!=null){
                Q.Install(p,parts.Where(x=>x.After==0).Select(x=>x.Uid),new ConquerItem[0]);
                foreach(var part in parts.Where(x=>x.After>0)){part.Item.StackSize=(ushort)part.After;part.Item.Mode=Enums.ItemMode.Update;part.Item.Send(p);part.Item.Mode=Enums.ItemMode.Default;}
            }if(paid)SilverBalance.ApplyCommitted(p,silver);return true;
        }
    }
}
