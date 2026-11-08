using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class EliteNestedPacks
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static Func<int,int> Roll=RandomIndex;
        private static bool enabled;
        private static uint[] accessories;
        private static readonly uint[] refiners={724404,724409,724414,724424,724453,724458,724463};
        private sealed class Recent {internal readonly Dictionary<uint,DateTime> Until=new Dictionary<uint,DateTime>();}
        private static readonly ConditionalWeakTable<GameState,Recent> recent=new ConditionalWeakTable<GameState,Recent>();
        internal static void Initialize() {
            // Supplemental server-only SpeedSlayer has no definition in either reviewed 6711 client catalog.
            var pool=ConquerItemInformation.BaseInformations.Values.Where(i=>i.ID>=350001 && i.ID<=380030 && i.ID!=360204).OrderBy(i=>i.ID).ToArray();
            if(pool.Length==0 || pool.Any(i=>!new uint[]{0,7200,10080,43200}.Contains(i.TimeTillRemovInMinutes)))
                throw new InvalidOperationException("Elite accessory catalog requires review.");
            foreach(uint id in refiners.Concat(new uint[]{300000,720049,720731,729626}))
                if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Elite nested prize catalog missing.");
            uint[] positions={500,4,5,421,4,5,500};
            for(int n=0;n<refiners.Length;n++) {
                Refinery.RefineryItem r;
                if(!Kernel.DatabaseRefinery.TryGetValue(refiners[n],out r) || r.Level!=5 || r.Position!=positions[n] || r.Untradable)
                    throw new InvalidOperationException("Elite Super weapon refinery catalog changed.");
            }
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_epk_nested_openings(
id uuid PRIMARY KEY,uid bigint NOT NULL,pack_uid bigint NOT NULL,pack_type integer NOT NULL CHECK(pack_type IN(720049,720731,729626)),
before_stack integer NOT NULL CHECK(before_stack>=0),profession integer NOT NULL,result_uid bigint NOT NULL UNIQUE,result_type integer NOT NULL,
bound boolean NOT NULL,plus integer NOT NULL,effect integer NOT NULL,color bigint NOT NULL,days integer NOT NULL,daystamp bigint NOT NULL,opened_at timestamptz NOT NULL);");}
            accessories=pool.Select(i=>i.ID).ToArray();enabled=true;
        }
        private static int RandomIndex(int count) {
            var bytes=new byte[4];uint n,limit=uint.MaxValue-uint.MaxValue%(uint)count;
            using(var rng=RandomNumberGenerator.Create())do{rng.GetBytes(bytes);n=BitConverter.ToUInt32(bytes,0);}while(n>=limit);
            return (int)(n%(uint)count);
        }
        private static int Draw(int count){int n=Roll(count);if(n<0 || n>=count)throw new InvalidOperationException("Elite prize draw outside pool.");return n;}
        private static uint[] Refiners(byte job) {
            if(job>=20 && job<=25)return new uint[]{724409,724414,724453,724458};
            if(job>=40 && job<=45)return new uint[]{724404,724463};
            if(job==100 || job==101 || job>=132 && job<=135 || job>=142 && job<=145)return new uint[]{724424};
            if(job>=10 && job<=15 || job>=50 && job<=55 || job>=60 && job<=65 || job>=70 && job<=75 || job>=80 && job<=85 || job>=160 && job<=165)
                return new uint[]{724409,724453};
            return new uint[0];
        }
        private static bool Ready(GameState p){return enabled && Q.Player(p) && p.Booth==null && p.Trade!=null && !p.Trade.InTrade;}
        private static bool Usable(ConquerItem i,DateTime now){return i.Position==0 && i.Warehouse==0 && i.Lock==0 && !i.Suspicious && !i.InWardrobe
            && (i.Days==0?i.TimeLeftInMinutes==0:now.ToLocalTime()<i.DayStamp.AddDays(i.Days));}
        internal static bool TryUse(ConquerItem item,GameState p) {
            if(item==null || item.ID!=720049 && item.ID!=720731 && item.ID!=729626)return false;
            if(!Ready(p))return true;
            lock(p.ItemSyncRoot)try {
                ConquerItem owned;DateTime now=Clock();
                if(!Ready(p) || !p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(item,owned) || !Usable(item,now))return true;
                var history=recent.GetValue(p,_=>new Recent());foreach(uint id in history.Until.Where(e=>e.Value<=now).Select(e=>e.Key).ToArray())history.Until.Remove(id);
                if(history.Until.ContainsKey(item.UID))return true;
                if(p.Inventory.Count+(item.StackSize<=1?0:1)>40){p.Send("Make room for this prize before opening the pack.");return true;}
                byte job=p.Entity.Class;uint[] pool=item.ID==729626?Refiners(job):accessories;
                if(item.ID==729626 && pool.Length==0){p.Send("Keep this pack until you have chosen your calling.");return true;}
                ConquerItem reward;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    using(var q=Q.Command(c,t,"SELECT class FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        {object saved=q.ExecuteScalar();if(saved==null || Convert.ToInt32(saved)!=job)throw new InvalidOperationException("Elite prize owner or profession changed.");}
                    var bag=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader())while(r.Read()){if(bag.Count>=40)throw new InvalidOperationException("Elite prize bag over capacity.");bag.Add(Convert.ToUInt32(r[0]));}
                    if(!bag.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Elite prize bag changed.");
                    string debit=item.StackSize<=1?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
                    if(Q.Execute(c,t,debit+@" WHERE uid=@item AND id=@type AND entityid=@uid AND position=0 AND warehouse=0 AND stacksize=@stack AND bound=@bound
AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                        "item",(long)item.UID,"type",(int)item.ID,"uid",(long)p.Entity.UID,"stack",(int)item.StackSize,"bound",item.Bound?1:0,
                        "days",item.Days.ToString(CultureInfo.InvariantCulture),"permanent",item.Days==0,"stamp",item.DayStamp.ToBinary())!=1)
                        throw new InvalidOperationException("Elite nested pack ownership changed.");
                    reward=Q.New(item.ID==720049?300000u:pool[Draw(pool.Length)],item.Bound);
                    if(item.ID==720049){int color=Draw(4);reward.Plus=6;reward.Effect=Enums.ItemEffect.Horse;
                        reward.SocketProgress=color<2?150u<<8|255u<<16:color==2?150u|255u<<8:255u|150u<<16;}
                    uint minutes=ConquerItemInformation.BaseInformations[reward.ID].TimeTillRemovInMinutes;
                    if(minutes!=0){reward.Days=checked((byte)(minutes/1440));reward.DayStamp=now.ToLocalTime();reward.TimeLeftInMinutes=checked(minutes*60);}
                    if(Q.Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability,plus,effect,socketprogress,days,daystamp)
VALUES(@item,@type,@uid,0,0,1,@bound,@dur,@max,@plus,@effect,@color,@days,@stamp)","item",(long)reward.UID,"type",(int)reward.ID,"uid",(long)p.Entity.UID,
                        "bound",reward.Bound?1:0,"dur",(int)reward.Durability,"max",(int)reward.MaximDurability,"plus",(int)reward.Plus,"effect",(int)reward.Effect,
                        "color",(long)reward.SocketProgress,"days",reward.Days.ToString(CultureInfo.InvariantCulture),"stamp",reward.DayStamp.ToBinary())!=1)
                        throw new InvalidOperationException("Elite nested gift was not saved.");
                    if(Q.Execute(c,t,@"INSERT INTO revora_epk_nested_openings(id,uid,pack_uid,pack_type,before_stack,profession,result_uid,result_type,bound,plus,effect,color,days,daystamp,opened_at)
VALUES(@id,@uid,@pack,@type,@stack,@job,@gift,@kind,@bound,@plus,@effect,@color,@days,@stamp,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,
                        "pack",(long)item.UID,"type",(int)item.ID,"stack",(int)item.StackSize,"job",(int)job,"gift",(long)reward.UID,"kind",(int)reward.ID,"bound",reward.Bound,
                        "plus",(int)reward.Plus,"effect",(int)reward.Effect,"color",(long)reward.SocketProgress,"days",(int)reward.Days,"stamp",reward.DayStamp.ToBinary(),"now",now)!=1)
                        throw new InvalidOperationException("Elite nested opening receipt was not saved.");
                    if(!Ready(p) || p.Entity.Class!=job || !Usable(item,Clock()))throw new InvalidOperationException("Elite nested prize session changed.");t.Commit();
                }}
                if(history.Until.Count>=64)history.Until.Remove(history.Until.OrderBy(e=>e.Value).First().Key);history.Until[item.UID]=now.AddSeconds(1);
                Q.Install(p,item.StackSize<=1?new[]{item.UID}:new uint[0],new[]{reward});
                if(item.StackSize>1){item.StackSize--;item.Mode=Enums.ItemMode.Update;item.Send(p);item.Mode=Enums.ItemMode.Default;}
            }catch(Exception ex){Q.Fail(p,"elite-nested-pack",ex);}return true;
        }
    }
}
