using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Ordinary leftover HP/MP/LifeFruit medicine with leftover NoDrugsTime
    // refuse/expire. Welcome/Starter packs issue 1000000/1001000.
    internal static class HealingMedicine
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static Action<string> Spoken;
        private static bool enabled;
        internal static bool Enabled{get{return enabled;}}
        private enum Kind { Hp, Mp, Fruit }
        private sealed class Spec { internal Kind Use; }
        private static readonly Dictionary<uint,Spec> Catalog=new Dictionary<uint,Spec>{
            {1000000,new Spec{Use=Kind.Hp}},{1000010,new Spec{Use=Kind.Hp}},{1000020,new Spec{Use=Kind.Hp}},
            {1000030,new Spec{Use=Kind.Hp}},{1000040,new Spec{Use=Kind.Hp}},{1002000,new Spec{Use=Kind.Hp}},
            {1002010,new Spec{Use=Kind.Hp}},{1002020,new Spec{Use=Kind.Hp}},{1002050,new Spec{Use=Kind.Hp}},
            {725065,new Spec{Use=Kind.Hp}},
            {1001000,new Spec{Use=Kind.Mp}},{1001010,new Spec{Use=Kind.Mp}},{1001020,new Spec{Use=Kind.Mp}},
            {1001030,new Spec{Use=Kind.Mp}},{1001040,new Spec{Use=Kind.Mp}},{1002030,new Spec{Use=Kind.Mp}},
            {1002040,new Spec{Use=Kind.Mp}},{725066,new Spec{Use=Kind.Mp}},
            {723726,new Spec{Use=Kind.Fruit}}
        };
        private static readonly Dictionary<uint,uint> Bundles=new Dictionary<uint,uint>{
            {720010,1000030},{720011,1002000},{720012,1002010},{720013,1002020},
            {720014,1001030},{720015,1001040},{720016,1002030},{720017,1002040}
        };
        private static bool Handles(uint id){return Catalog.ContainsKey(id) || Bundles.ContainsKey(id);}
        internal static void Initialize(){
            using(var c=DataHolder.MySqlConnection){c.Open();
                Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_no_drugs_state(
uid bigint PRIMARY KEY CHECK(uid>0),seconds integer NOT NULL CHECK(seconds>=0),granted_at timestamptz,
poison_star boolean NOT NULL DEFAULT false,
CHECK((seconds=0 AND granted_at IS NULL AND poison_star=false) OR (seconds>0 AND granted_at IS NOT NULL)));");
                Q.Execute(c,null,"ALTER TABLE revora_no_drugs_state ADD COLUMN IF NOT EXISTS poison_star boolean");
                Q.Execute(c,null,"UPDATE revora_no_drugs_state SET poison_star=true WHERE seconds>0 AND poison_star IS NULL");
                Q.Execute(c,null,"UPDATE revora_no_drugs_state SET poison_star=false WHERE poison_star IS NULL");
                Q.Execute(c,null,"ALTER TABLE revora_no_drugs_state ALTER COLUMN poison_star SET DEFAULT false");
                Q.Execute(c,null,"ALTER TABLE revora_no_drugs_state ALTER COLUMN poison_star SET NOT NULL");
                Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_healing_medicine_uses(
id uuid PRIMARY KEY,uid bigint NOT NULL CHECK(uid>0),item_uid bigint NOT NULL,
item_type integer NOT NULL CHECK(item_type IN(1000000,1000010,1000020,1000030,1000040,1001000,1001010,1001020,1001030,1001040,1002000,1002010,1002020,1002030,1002040,1002050,725065,725066,723726)),
before_stack integer NOT NULL CHECK(before_stack>=0),bound boolean NOT NULL,
hitpoints bigint NOT NULL CHECK(hitpoints>=0),mana integer NOT NULL CHECK(mana>=0),used_at timestamptz NOT NULL);");
                Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_medicine_bundle_uses(
id uuid PRIMARY KEY,uid bigint NOT NULL CHECK(uid>0),item_uid bigint NOT NULL,
item_type integer NOT NULL CHECK(item_type BETWEEN 720010 AND 720017),
before_stack integer NOT NULL CHECK(before_stack>=0),bound boolean NOT NULL,
rewards bigint[] NOT NULL CHECK(cardinality(rewards)=1),used_at timestamptz NOT NULL);");
            }enabled=true;
        }
        private static bool Ready(GameState p){return enabled && Q.Player(p) && p.Booth==null && p.Trade!=null && !p.Trade.InTrade;}
        private static void Hear(GameState p,string text){p.Send(new Message(text,System.Drawing.Color.Red,Message.TopLeft));var s=Spoken;if(s!=null)s(text);}
        internal static void Project(GameState p,HealingMedicineBalance.Image image)
        {
            int left=HealingMedicineBalance.Remaining(image.Seconds,image.Granted,Clock());
            if(left>0)
            {
                p.Entity.NoDrugsStamp=Time32.Now;
                p.Entity.NoDrugsTime=(short)left;
                if(image.PoisonStar)
                {
                    if(!p.Entity.BitVector.UpdateFlag((int)FlagsUpdate.Flags.PoisonStar,left,true,left))
                        p.Entity.AddFlag((ulong)FlagsUpdate.Flags.PoisonStar,left,true);
                }
                else if(p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.PoisonStar))
                    p.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.PoisonStar);
            }
            else
            {
                p.Entity.NoDrugsTime=0;
                if(p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.PoisonStar))
                    p.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.PoisonStar);
            }
        }
        private static bool PersistNoDrugs(Entity target,int seconds,bool poisonStar,bool sendUi,string source)
        {
            if(target==null || target.Owner==null || target.EntityFlag!=EntityFlag.Player || seconds<0)return false;
            if(!enabled)return false;
            GameState p=target.Owner;
            lock(p.ItemSyncRoot ?? p)try{
                if(!Q.Player(p,false))throw new InvalidOperationException("NoDrugsTime requires a healthy owner.");
                DateTime now=Clock();
                var next=HealingMedicineBalance.Current(p);
                next.Seconds=seconds;next.Granted=seconds>0?HealingMedicineBalance.GrantStamp(now):(DateTime?)null;
                next.PoisonStar=seconds>0 && poisonStar;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("NoDrugsTime owner missing.");
                    HealingMedicineBalance.Persist(c,t,p,next);
                    t.Commit();
                }}
                HealingMedicineBalance.Committed(p,next);
                if(seconds>0 && next.PoisonStar)Project(p,next);
                else if(seconds>0)
                {
                    p.Entity.NoDrugsStamp=Time32.Now;
                    p.Entity.NoDrugsTime=(short)seconds;
                    if(p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.PoisonStar))
                        p.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.PoisonStar);
                }
                else
                {
                    p.Entity.NoDrugsTime=0;
                    if(poisonStar && p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.PoisonStar))
                        p.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.PoisonStar);
                }
                if(sendUi)p.Send(Constants.NoDrugs(seconds));
                return true;
            }catch(Exception ex){Q.Fail(p,source,ex);return false;}
        }
        internal static bool TryApplyPoisonStar(Entity attacked,int seconds)
        {
            return seconds>0 && PersistNoDrugs(attacked,seconds,true,true,"poison-star");
        }
        internal static bool TryClearNoDrugs(Entity target,bool removePoisonStar)
        {
            return PersistNoDrugs(target,0,removePoisonStar,false,"no-drugs-clear");
        }
        internal static bool TryResetNoDrugs(Entity target,int seconds)
        {
            return seconds>0 && PersistNoDrugs(target,seconds,false,false,"no-drugs-reset");
        }
        private static bool Blocked(GameState p,Kind kind)
        {
            if(p.Entity.MapID==1707 || p.Entity.MapID==1801)return true;
            if(kind!=Kind.Mp && WaveDefenseSystem.InEvent(p.Entity))return true;
            return false;
        }
        internal static bool TryHandle(GameState p,byte[] packet){
            ConquerItem item;
            if(p==null || p.Inventory==null || packet==null || packet.Length<12 || !p.Inventory.TryGetItem(BitConverter.ToUInt32(packet,8),out item) || !Handles(item.ID))return false;
            if(packet.Length<32 || packet.Length>128 || BitConverter.ToUInt16(packet,0)+8!=packet.Length || BitConverter.ToUInt16(packet,2)!=1009
                || BitConverter.ToUInt16(packet,20)!=4 || BitConverter.ToUInt64(packet,packet.Length-8)!=Revora.Network.PacketHandler.ClientSeal)return true;
            TryUse(item,p);return true;
        }
        internal static bool TryUse(ConquerItem item,GameState p){
            if(item==null || !Handles(item.ID))return false;if(!Ready(p))return true;
            if(Bundles.ContainsKey(item.ID))return TryBundle(item,p);
            using(var health=HealthMutationLease.Acquire(p.Entity,null)){
                if(!health.Allowed)return true;return TryUseOwned(item,p);
            }
        }
        private static bool TryBundle(ConquerItem item,GameState p){
            if(item==null || !Bundles.ContainsKey(item.ID))return false;if(!Ready(p))return true;
            lock(p.ItemSyncRoot)try{
                ConquerItem owned;DateTime now=Clock();
                if(!Ready(p) || !p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(item,owned) || item.Position!=0 || item.Warehouse!=0
                    || item.Lock!=0 || item.Suspicious || item.InWardrobe || (item.Days==0?item.TimeLeftInMinutes!=0:now.ToLocalTime()>=item.DayStamp.AddDays(item.Days)))return true;
                if(p.Inventory.Count>38){p.Send(Constants.FullInventory);return true;}
                uint type=Bundles[item.ID];ConquerItemBaseInformation info;
                if(!ConquerItemInformation.BaseInformations.TryGetValue(type,out info) || info.StackSize<3)
                    throw new InvalidOperationException("Medicine bundle output catalog changed.");
                // A fresh three-pill stack avoids modifying any existing owner's stack.
                var rewards=new[]{new ConquerItem(true){ID=type,UID=ConquerItem.ItemUID.Next,Plus=0,Bound=false,
                    StackSize=3,Durability=info.Durability,MaximDurability=info.Durability}};
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("Medicine bundle owner missing.");
                    var ids=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader())while(r.Read()){if(ids.Count>=40)throw new InvalidOperationException("Medicine bundle inventory exceeds capacity.");ids.Add(checked((uint)Convert.ToInt64(r[0])));}
                    if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Medicine bundle inventory changed.");
                    string sql=item.StackSize<=1?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
                    if(Q.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=@type AND warehouse=0 AND position=0 AND stacksize=@stack AND bound=@bound
AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                        "item",(long)item.UID,"uid",(long)p.Entity.UID,"type",(int)item.ID,"stack",(int)item.StackSize,"bound",item.Bound?1:0,
                        "days",item.Days.ToString(CultureInfo.InvariantCulture),"permanent",item.Days==0,"stamp",item.DayStamp.ToBinary())!=1)
                        throw new InvalidOperationException("Medicine bundle ownership changed.");
                    foreach(var reward in rewards)
                        if(Q.Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,plus,durability,maximdurability)
VALUES(@item,@type,@uid,0,0,@count,@bound,@plus,@dur,@max)","item",(long)reward.UID,"type",(int)reward.ID,"uid",(long)p.Entity.UID,
                            "count",(int)reward.StackSize,"bound",reward.Bound?1:0,"plus",(int)reward.Plus,"dur",(int)reward.Durability,"max",(int)reward.MaximDurability)!=1)
                            throw new InvalidOperationException("Medicine bundle item was not saved.");
                    if(Q.Execute(c,t,@"INSERT INTO revora_medicine_bundle_uses(id,uid,item_uid,item_type,before_stack,bound,rewards,used_at)
VALUES(@id,@uid,@item,@type,@stack,@bound,@rewards,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"item",(long)item.UID,
                        "type",(int)item.ID,"stack",(int)item.StackSize,"bound",item.Bound,"rewards",rewards.Select(r=>(long)r.UID).ToArray(),"now",now)!=1)
                        throw new InvalidOperationException("Medicine bundle receipt was not saved.");
                    if(!Ready(p))throw new InvalidOperationException("Medicine bundle session changed.");t.Commit();
                }}
                Q.Install(p,item.StackSize<=1?new[]{item.UID}:new uint[0],rewards);
                if(item.StackSize>1){item.StackSize--;item.Mode=Enums.ItemMode.Update;item.Send(p);item.Mode=Enums.ItemMode.Default;}
            }catch(Exception ex){Q.Fail(p,"medicine-bundle",ex);}return true;
        }
        private static bool TryUseOwned(ConquerItem item,GameState p){
            if(item==null || !Handles(item.ID))return false;if(!Ready(p))return true;
            lock(p.ItemSyncRoot)try{
                ConquerItem owned;DateTime now=Clock();
                if(!Ready(p) || !p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(item,owned) || item.Position!=0 || item.Warehouse!=0
                    || item.Lock!=0 || item.Suspicious || item.InWardrobe || (item.Days==0?item.TimeLeftInMinutes!=0:now.ToLocalTime()>=item.DayStamp.AddDays(item.Days)))return true;
                Spec spec=Catalog[item.ID];
                if(Blocked(p,spec.Use)){Hear(p,spec.Use==Kind.Mp?"No MP in this map":"You cannot use healing on this map.");return true;}
                var cool=HealingMedicineBalance.Current(p);
                int durable=HealingMedicineBalance.Remaining(cool.Seconds,cool.Granted,now);
                bool cacheActive=p.Entity.NoDrugsTime>0 && Time32.Now<=p.Entity.NoDrugsStamp.AddSeconds(p.Entity.NoDrugsTime);
                if(durable>0 || cacheActive)return true;
                if(!ConquerItemInformation.BaseInformations.ContainsKey(item.ID))return true;
                var info=ConquerItemInformation.BaseInformations[item.ID];
                uint nextHp=p.Entity.Hitpoints;ushort nextMp=p.Entity.Mana;
                if(spec.Use==Kind.Hp || spec.Use==Kind.Fruit)nextHp=Math.Min(p.Entity.Hitpoints+info.ItemHP,p.Entity.MaxHitpoints);
                if(spec.Use==Kind.Mp)nextMp=(ushort)Math.Min(p.Entity.Mana+info.ItemMP,p.Entity.MaxMana);
                if(spec.Use==Kind.Fruit)nextMp=p.Entity.MaxMana;
                cool.Seconds=0;cool.Granted=null;cool.PoisonStar=false;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    HealingMedicineBalance.AssertSaved(c,t,p);
                    var ids=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader())while(r.Read()){if(ids.Count>=40)throw new InvalidOperationException("Healing medicine inventory exceeds capacity.");ids.Add(checked((uint)Convert.ToInt64(r[0])));}
                    if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Healing medicine inventory changed.");
                    string sql=item.StackSize<=1?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
                    if(Q.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=@type AND warehouse=0 AND position=0 AND stacksize=@stack AND bound=@bound
AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                        "item",(long)item.UID,"uid",(long)p.Entity.UID,"type",(int)item.ID,"stack",(int)item.StackSize,"bound",item.Bound?1:0,
                        "days",item.Days.ToString(CultureInfo.InvariantCulture),"permanent",item.Days==0,"stamp",item.DayStamp.ToBinary())!=1)
                        throw new InvalidOperationException("Healing medicine ownership changed.");
                    HealthBalance.Persist(c,t,p,nextHp,nextMp);
                    HealingMedicineBalance.Persist(c,t,p,cool);
                    if(Q.Execute(c,t,@"INSERT INTO revora_healing_medicine_uses(id,uid,item_uid,item_type,before_stack,bound,hitpoints,mana,used_at)
VALUES(@id,@uid,@item,@type,@stack,@bound,@hp,@mp,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"item",(long)item.UID,
                        "type",(int)item.ID,"stack",(int)item.StackSize,"bound",item.Bound,"hp",(long)nextHp,"mp",(int)nextMp,"now",now)!=1)
                        throw new InvalidOperationException("Healing medicine receipt was not saved.");
                    if(!Ready(p))throw new InvalidOperationException("Healing medicine session changed.");t.Commit();
                }}
                Q.Install(p,item.StackSize<=1?new[]{item.UID}:new uint[0],new ConquerItem[0]);
                if(item.StackSize>1){item.StackSize--;item.Mode=Enums.ItemMode.Update;item.Send(p);item.Mode=Enums.ItemMode.Default;}
                HealingMedicineBalance.Committed(p,cool);
                HealthBalance.Committed(p,nextHp,nextMp);
                p.Entity.Hitpoints=nextHp;p.Entity.Mana=nextMp;p.Entity.NoDrugsTime=0;
            }catch(Exception ex){Q.Fail(p,"healing-medicine",ex);}return true;
        }
    }
}
