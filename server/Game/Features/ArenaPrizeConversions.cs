using System;
using System.Collections.Generic;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static class ArenaPrizeConversions
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        private static bool enabled;
        private sealed class Debit { internal ConquerItem Item;internal ushort Stack;internal bool Bound;internal int Used; }
        private static uint Result(uint type) {switch(type){case 3009002:return 3009003;case 3004243:return 3004247;case 3004244:return 3004248;case 3004245:return 3004249;default:return 0;}}
        internal static void Initialize() {
            foreach(uint type in new uint[]{3009002,3004243,3004244,3004245})
                if(!ConquerItemInformation.BaseInformations.ContainsKey(type) || !ConquerItemInformation.BaseInformations.ContainsKey(Result(type)))throw new InvalidOperationException("Arena material catalog changed.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_arena_material_conversions(
id uuid PRIMARY KEY,uid bigint NOT NULL,source_type integer NOT NULL,source_uids bigint[] NOT NULL,source_stacks integer[] NOT NULL,
source_used integer[] NOT NULL,source_bound boolean[] NOT NULL,result_uid bigint NOT NULL UNIQUE,result_type integer NOT NULL,
bound boolean NOT NULL,converted_at timestamptz NOT NULL);
CREATE INDEX IF NOT EXISTS revora_arena_material_recent ON revora_arena_material_conversions(uid,source_type,converted_at);");}enabled=true;
        }
        private static bool Ready(GameState p){return enabled && Q.Player(p) && p.Trade!=null && !p.Trade.InTrade && p.Booth==null;}
        private static bool Owns(GameState p,ConquerItem i) {ConquerItem current;return i!=null && i.Position==0 && i.Warehouse==0 && i.Lock==0 && !i.Suspicious && !i.InWardrobe
            && i.Days==0 && i.TimeLeftInMinutes==0 && p.Inventory.TryGetItem(i.UID,out current) && ReferenceEquals(i,current);}
        internal static bool TryUse(ConquerItem item,GameState p) {
            if(item==null || Result(item.ID)==0)return false;if(!Ready(p))return true;
            lock(p.ItemSyncRoot)try {
                if(!Ready(p) || !Owns(p,item))return true;
                uint type=item.ID,result=Result(type);int needed=type==3009002?10:15;var debits=new List<Debit>();
                foreach(var source in p.Inventory.Objects.Where(i=>i.ID==type && Owns(p,i)).OrderBy(i=>i.UID==item.UID?0:1).ThenBy(i=>i.UID)) {
                    int use=Math.Min(needed,Math.Max(1,(int)source.StackSize));
                    debits.Add(new Debit{Item=source,Stack=source.StackSize,Bound=source.Bound,Used=use});needed-=use;if(needed==0)break;
                }
                if(needed!=0){p.Send(type==3009002?"Gather 10 usable stones first.":"Gather 15 usable scraps first.");return true;}
                var removed=debits.Where(d=>d.Used==Math.Max(1,(int)d.Stack)).Select(d=>d.Item.UID).ToArray();
                if(p.Inventory.Count-removed.Length+1>40){p.Send("Make room for the completed item.");return true;}
                var now=Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Arena conversion requires UTC.");
                var reward=Q.New(result,debits.Any(d=>d.Bound));
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    SkillTable.LockOwner(c,t,p);
                    using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_arena_material_conversions WHERE uid=@uid AND source_type=@type AND converted_at>@now-interval '1 second')",
                        "uid",(long)p.Entity.UID,"type",(int)type,"now",now))if(Equals(q.ExecuteScalar(),true))return true;
                    var saved=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader())while(r.Read())saved.Add(Convert.ToUInt32(r[0]));
                    if(!saved.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Arena conversion inventory requires reload.");
                    foreach(var d in debits) {
                        string sql=removed.Contains(d.Item.UID)?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-@used";
                        if(Q.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=@type AND stacksize=@stack AND bound=@bound
AND position=0 AND warehouse=0 AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0'",
                            "used",d.Used,"item",(long)d.Item.UID,"uid",(long)p.Entity.UID,"type",(int)type,"stack",(int)d.Stack,"bound",d.Bound?1:0)!=1)
                            throw new InvalidOperationException("Arena conversion material changed.");
                    }
                    Q.Insert(c,t,p.Entity.UID,reward);
                    if(Q.Execute(c,t,@"INSERT INTO revora_arena_material_conversions(id,uid,source_type,source_uids,source_stacks,source_used,source_bound,result_uid,result_type,bound,converted_at)
VALUES(@id,@uid,@type,@sources,@stacks,@used,@bindings,@gift,@result,@bound,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"type",(int)type,
                        "sources",debits.Select(d=>(long)d.Item.UID).ToArray(),"stacks",debits.Select(d=>(int)d.Stack).ToArray(),"used",debits.Select(d=>d.Used).ToArray(),
                        "bindings",debits.Select(d=>d.Bound).ToArray(),"gift",(long)reward.UID,"result",(int)result,"bound",reward.Bound,"now",now)!=1)
                        throw new InvalidOperationException("Arena conversion receipt was not saved.");
                    if(!Ready(p) || debits.Any(d=>!Owns(p,d.Item) || d.Item.ID!=type || d.Item.StackSize!=d.Stack || d.Item.Bound!=d.Bound))throw new InvalidOperationException("Arena conversion context changed.");t.Commit();
                }}
                Q.Install(p,removed,new[]{reward});
                foreach(var d in debits.Where(d=>!removed.Contains(d.Item.UID))){d.Item.StackSize=(ushort)(d.Stack-d.Used);d.Item.Mode=Enums.ItemMode.Update;d.Item.Send(p);d.Item.Mode=Enums.ItemMode.Default;}Q.Refresh(p);
            }catch(Exception ex){Q.Fail(p,"arena-material-conversion",ex);}return true;
        }
    }
}
