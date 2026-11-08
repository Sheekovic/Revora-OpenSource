using System;
using System.Linq;
using System.Security.Cryptography;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static class P7SoulPacks
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static Func<int,int> Roll=RandomIndex;
        private static bool enabled;
        private static readonly uint[] Weapons={800020,800111,800142,800215,800255,800422,800522,800618,800725,800810,800811,800917,801004,801104,801308,801216,801218};
        private static readonly uint[] Equipment={820073,820074,820075,820076,821033,821034,822071,822072,823058,823059,823060,824018,824019,824020,823061,823062};
        private static int RandomIndex(int count) {
            uint value,limit=uint.MaxValue-uint.MaxValue%(uint)count;var bytes=new byte[4];
            using(var rng=RandomNumberGenerator.Create())do{rng.GetBytes(bytes);value=BitConverter.ToUInt32(bytes,0);}while(value>=limit);
            return (int)(value%(uint)count);
        }
        internal static void Initialize() {
            if(new uint[]{3004247,3004248}.Any(id=>!ConquerItemInformation.BaseInformations.ContainsKey(id))
                || Weapons.Concat(Equipment).Any(id=>!ConquerItemInformation.BaseInformations.ContainsKey(id) || ConquerItemInformation.BaseInformations[id].PurificationLevel!=7))
                throw new InvalidOperationException("P7 soul pack catalog changed.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_p7_pack_openings(
id uuid PRIMARY KEY,uid bigint NOT NULL,pack_uid bigint NOT NULL,pack_type integer NOT NULL CHECK(pack_type IN(3004247,3004248)),
before_stack integer NOT NULL CHECK(before_stack BETWEEN 0 AND 65535),source_bound boolean NOT NULL,
result_uid bigint NOT NULL UNIQUE,result_type integer NOT NULL,bound boolean NOT NULL CHECK(bound),opened_at timestamptz NOT NULL);
CREATE INDEX IF NOT EXISTS revora_p7_pack_recent ON revora_p7_pack_openings(uid,pack_uid,opened_at);");}enabled=true;
        }
        private static bool Ready(GameState p){return enabled && Q.Player(p) && p.Trade!=null && !p.Trade.InTrade && p.Booth==null;}
        private static bool Owns(GameState p,ConquerItem item) {ConquerItem current;return item.Position==0 && item.Warehouse==0 && item.Lock==0 && !item.Suspicious
            && !item.InWardrobe && item.Days==0 && item.TimeLeftInMinutes==0 && p.Inventory.TryGetItem(item.UID,out current) && ReferenceEquals(item,current);}
        internal static bool TryUse(ConquerItem item,GameState p) {
            if(item==null || item.ID!=3004247 && item.ID!=3004248)return false;if(!Ready(p))return true;
            lock(p.ItemSyncRoot)try {
                if(!Ready(p) || !Owns(p,item))return true;
                ushort stack=item.StackSize;bool bound=item.Bound;uint type=item.ID;var now=Clock();
                if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("P7 pack clock requires UTC.");
                if(p.Inventory.Count+(stack<=1?0:1)>40){p.Send("Make room before opening this soul pack.");return true;}
                ConquerItem reward;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    SkillTable.LockOwner(c,t,p);
                    using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_p7_pack_openings WHERE uid=@uid AND pack_uid=@pack AND opened_at>@now-interval '1 second')",
                        "uid",(long)p.Entity.UID,"pack",(long)item.UID,"now",now))if(Equals(q.ExecuteScalar(),true))return true;
                    using(var q=Q.Command(c,t,"SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0","uid",(long)p.Entity.UID))
                        if(Convert.ToInt64(q.ExecuteScalar())!=p.Inventory.Count)throw new InvalidOperationException("P7 pack inventory requires reload.");
                    string debit=stack<=1?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
                    if(Q.Execute(c,t,debit+@" WHERE uid=@pack AND entityid=@uid AND id=@type AND stacksize=@stack AND bound=@bound
AND position=0 AND warehouse=0 AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0'",
                        "pack",(long)item.UID,"uid",(long)p.Entity.UID,"type",(int)type,"stack",(int)stack,"bound",bound?1:0)!=1)
                        throw new InvalidOperationException("P7 pack ownership changed.");
                    var pool=type==3004247?Weapons:Equipment;int draw=Roll(pool.Length);if(draw<0 || draw>=pool.Length)throw new InvalidOperationException("P7 draw outside pool.");
                    reward=Q.New(pool[draw],true);Q.Insert(c,t,p.Entity.UID,reward);
                    if(Q.Execute(c,t,@"INSERT INTO revora_p7_pack_openings(id,uid,pack_uid,pack_type,before_stack,source_bound,result_uid,result_type,bound,opened_at)
VALUES(@id,@uid,@pack,@type,@stack,@source,@gift,@kind,true,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,
                        "pack",(long)item.UID,"type",(int)type,"stack",(int)stack,"source",bound,"gift",(long)reward.UID,"kind",(int)reward.ID,"now",now)!=1)
                        throw new InvalidOperationException("P7 pack receipt was not saved.");
                    if(!Ready(p) || !Owns(p,item) || item.ID!=type || item.StackSize!=stack || item.Bound!=bound)throw new InvalidOperationException("P7 pack context changed.");t.Commit();
                }}
                Q.Install(p,stack<=1?new[]{item.UID}:new uint[0],new[]{reward});
                if(stack>1){item.StackSize--;item.Mode=Enums.ItemMode.Update;item.Send(p);item.Mode=Enums.ItemMode.Default;}Q.Refresh(p);
            }catch(Exception ex){Q.Fail(p,"p7-soul-pack",ex);}return true;
        }
    }
}
