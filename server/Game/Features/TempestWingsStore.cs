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
    // Internal earning state. Public admission remains disabled until owned combat is wired.
    internal static partial class TempestWingsStore
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal sealed class Run { internal int Stage; internal uint Soul; }
        internal static bool Eligible(GameState p){return Q.Player(p) && ReferenceEquals(p.Entity.Owner,p)
            && CharacterProgression.Managed(p) && (p.Entity.Level>=100 || p.Entity.Reborn>0) && p.Entity.Reborn<=2
            && p.Trade!=null && !p.Trade.InTrade && p.Booth==null && p.Entity.PokerTable==0;}
        private static void Ready(GameState p,Func<bool> context){
            if(!Eligible(p) || !Monitor.IsEntered(p.ItemSyncRoot) || context==null || !context())throw new InvalidOperationException("Tempest context changed.");
        }
        private static bool Near(GameState p,ushort map,ushort x,ushort y){return p.Entity.MapID==map && Kernel.GetDistance(p.Entity.X,p.Entity.Y,x,y)<=6;}
        internal static void Initialize(){
            foreach(uint id in new uint[]{204008,3006392,720027})
                if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Tempest catalog missing.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_tempest_wings(
uid bigint PRIMARY KEY,stage smallint NOT NULL CHECK(stage BETWEEN 1 AND 4),soul bigint NOT NULL,rewards bigint[] NOT NULL DEFAULT '{}');
CREATE TABLE IF NOT EXISTS revora_tempest_permits(uid bigint PRIMARY KEY REFERENCES revora_tempest_wings(uid),
scrolls smallint NOT NULL CHECK(scrolls=5));
CREATE TABLE IF NOT EXISTS revora_tempest_payments(uid bigint NOT NULL REFERENCES revora_tempest_permits(uid),
item_uid bigint NOT NULL,stack_before integer NOT NULL CHECK(stack_before>0),spent smallint NOT NULL CHECK(spent BETWEEN 1 AND 5),
bound boolean NOT NULL,PRIMARY KEY(uid,item_uid),CHECK(spent<=stack_before));");}
        }
        private static Run Read(NpgsqlConnection c,NpgsqlTransaction t,GameState p){
            using(var q=Q.Command(c,t,"SELECT stage,soul FROM revora_tempest_wings WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())return r.Read()?new Run{Stage=r.GetInt16(0),Soul=checked((uint)r.GetInt64(1))}:new Run();
        }
        internal static Run Load(GameState p){if(!Q.Player(p,false))return null;lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();return Read(c,null,p);}}
        private static void Owner(NpgsqlConnection c,NpgsqlTransaction t,GameState p){
            SkillTable.LockOwner(c,t,p);
            using(var q=Q.Command(c,t,"SELECT reborn,level FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))using(var r=q.ExecuteReader())
                if(!r.Read() || Convert.ToInt32(r[0])!=p.Entity.Reborn || Convert.ToInt32(r[1])!=p.Entity.Level)throw new InvalidOperationException("Tempest saved owner changed.");
            var saved=new List<uint>();
            using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid FOR UPDATE","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())while(r.Read())saved.Add(checked((uint)r.GetInt64(0)));
            if(!saved.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Tempest inventory changed.");
        }
        internal static bool Accept(GameState p,Func<bool> context){
            Ready(p,context);if(!Near(p,1004,51,49))return false;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);if(Read(c,t,p).Stage!=0)return false;
                if(Q.Execute(c,t,"INSERT INTO revora_tempest_wings(uid,stage,soul) VALUES(@uid,1,@soul)","uid",(long)p.Entity.UID,"soul",(long)QuestSoulReward.Create().ID)!=1)throw new InvalidOperationException("Tempest acceptance changed.");
                Ready(p,()=>context() && Near(p,1004,51,49));t.Commit();return true;
            }}
        }
        private static bool Usable(ConquerItem item){return item.ID==720027 && item.Position==0 && item.StackSize>0
            && item.Lock==0 && !item.Suspicious && !item.InWardrobe && item.Days==0 && item.TimeLeftInMinutes==0;}
        // Initial admission consumes exactly five scrolls; stage2 reentry reuses the saved permit.
        internal static bool Permit(GameState p,Func<bool> context){
            Ready(p,context);if(!Near(p,1011,212,196))return false;
            var debits=new List<Tuple<ConquerItem,int,int>>();
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);
                if(run.Stage==2){
                    using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_tempest_permits r WHERE r.uid=@uid AND r.scrolls=5
AND (SELECT SUM(spent) FROM revora_tempest_payments p WHERE p.uid=r.uid)=5)","uid",(long)p.Entity.UID))
                        if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Tempest paid permit missing.");
                    Ready(p,()=>context() && Near(p,1011,212,196));t.Commit();return true;
                }
                if(run.Stage!=1)return false;
                int remaining=5;
                foreach(var item in p.Inventory.Objects.Where(Usable).OrderBy(i=>i.UID)){
                    int take=Math.Min(remaining,item.StackSize);debits.Add(Tuple.Create(item,(int)item.StackSize,take));remaining-=take;if(remaining==0)break;
                }
                if(remaining!=0)return false;
                if(Q.Execute(c,t,"INSERT INTO revora_tempest_permits(uid,scrolls) VALUES(@uid,5)","uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Tempest permit receipt missing.");
                foreach(var debit in debits){var item=debit.Item1;
                    string sql=debit.Item2==debit.Item3?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-@spent";
                    if(Q.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=720027 AND position=0 AND warehouse=0
AND stacksize=@before AND bound=@bound AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0
AND COALESCE(NULLIF(days,''),'0')='0'","item",(long)item.UID,"uid",(long)p.Entity.UID,"before",debit.Item2,"spent",debit.Item3,"bound",item.Bound?1:0)!=1)
                        throw new InvalidOperationException("Tempest scroll ownership changed.");
                    if(Q.Execute(c,t,"INSERT INTO revora_tempest_payments(uid,item_uid,stack_before,spent,bound) VALUES(@uid,@item,@before,@spent,@bound)",
                        "uid",(long)p.Entity.UID,"item",(long)item.UID,"before",debit.Item2,"spent",debit.Item3,"bound",item.Bound)!=1)throw new InvalidOperationException("Tempest payment receipt missing.");
                }
                if(Q.Execute(c,t,"UPDATE revora_tempest_wings SET stage=2 WHERE uid=@uid AND stage=1","uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Tempest payment stage changed.");
                Ready(p,()=>context() && Near(p,1011,212,196));t.Commit();
            }}
            Q.Install(p,debits.Where(d=>d.Item2==d.Item3).Select(d=>d.Item1.UID),new ConquerItem[0]);
            foreach(var debit in debits.Where(d=>d.Item2> d.Item3)){
                var item=debit.Item1;item.StackSize=checked((ushort)(debit.Item2-debit.Item3));item.Mode=Enums.ItemMode.Update;item.Send(p);item.Mode=Enums.ItemMode.Default;
            }
            return true;
        }
    }
}
