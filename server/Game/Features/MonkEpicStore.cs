using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using R=Revora.Game.Features.MonkEpicRules;

namespace Revora.Game.Features
{
    internal static class MonkEpicStore
    {
        internal enum Kind { Flowers=1,Altar=2,Victory=3,Scroll=4,Awaken=5 }
        internal sealed class Part {
            internal ConquerItem Item;internal uint Uid,Type;internal ushort Before;internal int Count;internal bool Bound;
            internal int After {get{return Math.Max(1,(int)Before)-Count;}}
        }
        internal sealed class Offer {
            internal Guid Id;internal Kind Kind;internal uint Npc,BeforeCp,Product,HeartUid;internal readonly uint Cost=0;internal byte Vip;
            internal DateTime Until;internal string Cache,Rows;internal Part[] Parts;internal ConquerItem Weapon;
            internal uint OldWeapon,NewWeapon;internal byte Slot;internal bool WeaponBound;
            internal bool Bound {get{return Parts.Any(a=>a.Bound);}}
        }
        internal sealed class Outcome {internal int Count;internal uint Product;internal bool RetryGem,Duplicate;}
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        private static readonly System.Security.Cryptography.RandomNumberGenerator random=System.Security.Cryptography.RandomNumberGenerator.Create();
        private static int Draw(int maximum) {
            if(maximum!=100)throw new ArgumentOutOfRangeException("maximum");
            var sample=new byte[1];lock(random){do{random.GetBytes(sample);}while(sample[0]>=200);}return sample[0]%100;
        }
        internal static Func<int,int> Roll=Draw;
        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_monk_epic_operations(
id uuid PRIMARY KEY,uid bigint NOT NULL,kind integer NOT NULL CHECK(kind BETWEEN 1 AND 5),npc bigint NOT NULL,
cost bigint NOT NULL CHECK(cost>=0),balance_before bigint NOT NULL,balance_after bigint NOT NULL CHECK(balance_after=balance_before-cost AND balance_after>=0),
product integer NOT NULL,quantity integer NOT NULL CHECK(quantity BETWEEN 0 AND 10),bound boolean NOT NULL,
weapon_uid bigint NOT NULL DEFAULT 0,weapon_before integer NOT NULL DEFAULT 0,weapon_after integer NOT NULL DEFAULT 0,
heart_uid bigint NOT NULL DEFAULT 0,retry_gem boolean NOT NULL,created_at timestamptz NOT NULL);
CREATE INDEX IF NOT EXISTS revora_monk_epic_owner ON revora_monk_epic_operations(uid,created_at DESC);
CREATE TABLE IF NOT EXISTS revora_monk_epic_materials(operation uuid NOT NULL REFERENCES revora_monk_epic_operations(id),
item_uid bigint NOT NULL,item_type integer NOT NULL,stack_before integer NOT NULL,quantity integer NOT NULL CHECK(quantity>0),bound boolean NOT NULL,PRIMARY KEY(operation,item_uid));
CREATE TABLE IF NOT EXISTS revora_monk_epic_rewards(operation uuid NOT NULL REFERENCES revora_monk_epic_operations(id),
item_uid bigint NOT NULL UNIQUE,item_type integer NOT NULL,bound boolean NOT NULL,PRIMARY KEY(operation,item_uid));");}
        }
        internal static bool Eligible(GameState p) {
            return Q.Player(p) && p.Trade!=null && !p.Trade.InTrade && p.Booth==null && p.Entity.PokerTable==0
                && CharacterProgression.Managed(p) && R.Eligible(p.Entity.Class,p.Entity.Reborn,p.Entity.Level);
        }
        internal static bool Usable(ConquerItem i) {
            return i!=null && i.Position==0 && i.Warehouse==0 && i.Lock==0 && !i.Suspicious && !i.InWardrobe && i.Days==0 && i.TimeLeftInMinutes==0;
        }
        private static Part[] Select(GameState p,uint type,int count,uint exact=0) {
            var parts=new List<Part>();
            foreach(var i in p.Inventory.Objects.Where(i=>i.ID==type && Usable(i) && (exact==0 || i.UID==exact)).OrderByDescending(i=>i.Bound).ThenBy(i=>i.UID)) {
                int take=Math.Min(count,Math.Max(1,(int)i.StackSize));parts.Add(new Part{Item=i,Uid=i.UID,Type=i.ID,Before=i.StackSize,Bound=i.Bound,Count=take});count-=take;if(count==0)return parts.ToArray();
            }
            return null;
        }
        internal static bool Has(GameState p,uint type){return p.Inventory.Objects.Any(i=>i.ID==type && Usable(i));}
        private static bool Gear(GameState p,ConquerItem item) {
            return item!=null && (item.Position==ConquerItem.RightWeapon || item.Position==ConquerItem.LeftWeapon)
                && ReferenceEquals(p.Equipment.TryGetItem(item.Position),item) && item.Warehouse==0 && !item.Suspicious && !item.InWardrobe
                && item.Days==0 && item.TimeLeftInMinutes==0 && R.EpicBeads(item.ID,id=>ConquerItemInformation.BaseInformations.ContainsKey(id))!=0;
        }
        private static void Bag(NpgsqlConnection c,NpgsqlTransaction t,GameState p) {
            var ids=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())while(r.Read()){if(ids.Count>=40)throw new InvalidOperationException("Monk quest bag exceeds its bound.");ids.Add(checked((uint)Convert.ToInt64(r[0])));}
            if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Monk quest bag requires reload.");
        }
        private static void GearImage(NpgsqlConnection c,NpgsqlTransaction t,GameState p,ConquerItem i) {
            if(i==null)return;
            string[] columns={"id","position","plus","bless","enchant","socketone","sockettwo","durability","maximdurability",
                "plusprogress","socketprogress","effect","color","perfectionlevel","perfectionownerguid","perfectionprogress","refineryitem","runesexp",
                "relicattrib1","relicattrib2","relicattrib3","relicattrib4","relicattrib5","bound","locked","stacksize"};
            long[] values={i.ID,i.Position,i.Plus,i.Bless,i.Enchant,(long)i.SocketOne,(long)i.SocketTwo,i.Durability,i.MaximDurability,
                i.PlusProgress,i.SocketProgress,(long)i.Effect,(long)i.Color,i.PerfectionLevel,i.PerfectionOwnerGuid,i.PerfectionProgress,i.RefineItem,i.RunesEXP,
                i.RelicAttrib1,i.RelicAttrib2,i.RelicAttrib3,i.RelicAttrib4,i.RelicAttrib5,i.Bound?1:0,i.Lock,i.StackSize};
            using(var q=Q.Command(c,t,"SELECT "+string.Join(",",columns)+",perfectionowner,signature FROM items WHERE uid=@item AND entityid=@uid FOR UPDATE",
                "item",(long)i.UID,"uid",(long)p.Entity.UID))using(var r=q.ExecuteReader()) {
                if(!r.Read())throw new InvalidOperationException("Monk prayer beads owner missing.");
                for(int field=0;field<columns.Length;field++)if((r.IsDBNull(field)?0:Convert.ToInt64(r[field]))!=values[field])throw new InvalidOperationException("Monk prayer beads metadata requires reload.");
                if(Convert.ToString(r[columns.Length])!=(i.PerfectionOwner??"") || Convert.ToString(r[columns.Length+1])!=(i.Signature??""))throw new InvalidOperationException("Monk prayer beads attribution requires reload.");
            }
        }
        internal static Offer Prepare(GameState p,Kind kind,uint npc,byte choice,out string error,uint exactHeart=0) {
            if(!Monitor.IsEntered(p.ItemSyncRoot))throw new InvalidOperationException("Monk quest requires owner lock.");
            error=null;if(!Eligible(p)){error="This trial awaits second-reborn Monks of level 70.";return null;}
            var o=new Offer{Id=Guid.NewGuid(),Kind=kind,Npc=npc,BeforeCp=p.Entity.ConquerPoints,Vip=p.Entity.VIPLevel,Until=Clock().AddMinutes(2),Parts=new Part[0]};
            switch(kind) {
                case Kind.Flowers:
                    var red=Select(p,R.RedFlower,R.FlowersPerAttempt);var white=Select(p,R.WhiteFlower,R.FlowersPerAttempt);
                    o.Parts=red==null || white==null?null:red.Concat(white).ToArray();break;
                case Kind.Altar:o.Parts=Select(p,R.Gem,1);break;
                case Kind.Victory:o.Parts=Select(p,R.Necklace,1);break;
                case Kind.Scroll:o.Product=R.Scroll(choice);o.Parts=Select(p,R.Incense,R.IncensePerScroll);break;
                case Kind.Awaken:
                    o.Parts=Select(p,R.Heart,1,exactHeart);o.Weapon=p.Equipment.TryGetItem(choice==5?ConquerItem.LeftWeapon:ConquerItem.RightWeapon);
                    if(choice==0 && !Gear(p,o.Weapon))o.Weapon=p.Equipment.TryGetItem(ConquerItem.LeftWeapon);
                    if(!Gear(p,o.Weapon)){error="Equip a permanent prayer beads whose epic form is known.";return null;}
                    o.OldWeapon=o.Weapon.ID;o.NewWeapon=R.EpicBeads(o.OldWeapon,id=>ConquerItemInformation.BaseInformations.ContainsKey(id));o.Slot=o.Weapon.Position;o.WeaponBound=o.Weapon.Bound;break;
                default:throw new ArgumentOutOfRangeException("kind");
            }
            if(o.Parts==null){error="Bring the required unlocked materials in your bag.";return null;}
            if(o.Cost>o.BeforeCp){error="You need "+o.Cost+" CPs for this trial.";return null;}
            int room=kind==Kind.Victory?4:1;
            if(p.Inventory.Count-o.Parts.Count(a=>a.After==0)+room>40){error="Make room in your bag for the trial's gifts.";return null;}
            if(kind==Kind.Awaken)o.HeartUid=o.Parts[0].Uid;
            o.Cache=FirstRebirth.Cache(p);
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                SkillTable.LockOwner(c,t,p);Bag(c,t,p);GearImage(c,t,p,o.Weapon);o.Rows=FirstRebirth.Rows(c,t,p,"items");t.Commit();
            }}return o;
        }
        private static bool Same(GameState p,Offer o,Func<bool> context) {
            return Eligible(p) && context() && Clock()<o.Until && p.Entity.ConquerPoints==o.BeforeCp && p.Entity.VIPLevel==o.Vip
                && o.Cache==FirstRebirth.Cache(p) && o.Parts.All(a=>{ConquerItem owned;return p.Inventory.TryGetItem(a.Uid,out owned)
                    && ReferenceEquals(owned,a.Item) && Usable(owned) && owned.ID==a.Type && owned.StackSize==a.Before && owned.Bound==a.Bound;})
                && (o.Weapon==null || Gear(p,o.Weapon) && o.Weapon.ID==o.OldWeapon && o.Weapon.Position==o.Slot && o.Weapon.Bound==o.WeaponBound);
        }
        internal static Outcome Commit(GameState p,Offer o,Func<bool> context) {
            if(!Monitor.IsEntered(p.ItemSyncRoot) || o==null || !Same(p,o,context))throw new InvalidOperationException("Monk quest offer changed.");
            var profile=CharacterProgression.Capture(p);var result=new Outcome();var rewards=new List<ConquerItem>();DateTime now=Clock();
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                SkillTable.LockOwner(c,t,p);Bag(c,t,p);
                if(FirstRebirth.Rows(c,t,p,"items")!=o.Rows)throw new InvalidOperationException("Saved Monk quest items changed.");
                if(o.Kind==Kind.Awaken && o.Npc==0)using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_monk_epic_operations
WHERE uid=@uid AND kind=5 AND heart_uid=@eye AND created_at>@since)","uid",(long)p.Entity.UID,"eye",(long)o.HeartUid,"since",now.AddSeconds(-1)))
                    if(Equals(q.ExecuteScalar(),true)){result.Duplicate=true;t.Commit();return result;}
                CharacterProgression.Persist(c,t,p,profile);
                if(Q.Execute(c,t,"UPDATE entities SET conquerpoints=@after WHERE uid=@uid AND conquerpoints=@before AND mapid=@map",
                    "after",(long)(o.BeforeCp-o.Cost),"uid",(long)p.Entity.UID,"before",(long)o.BeforeCp,"map",(int)p.Entity.MapID)!=1)
                    throw new InvalidOperationException("Saved Monk quest owner changed.");
                if(o.Kind==Kind.Scroll){result.Product=o.Product;result.Count=1;}
                else if(o.Kind!=Kind.Awaken){var trial=o.Kind==Kind.Altar?R.Trial.Altar:o.Kind==Kind.Victory?R.Trial.Victory:R.Trial.Fortune;
                    var drawn=R.Resolve(trial,Roll(100));result.Product=drawn.Item;result.Count=drawn.Count;result.RetryGem=drawn.RetryGem;}
                if(Q.Execute(c,t,@"INSERT INTO revora_monk_epic_operations(id,uid,kind,npc,cost,balance_before,balance_after,product,quantity,bound,
weapon_uid,weapon_before,weapon_after,heart_uid,retry_gem,created_at)
VALUES(@id,@uid,@kind,@npc,@cost,@before,@after,@product,@count,@bound,@weapon,@old,@new,@eye,@retry,@now)",
                    "id",o.Id,"uid",(long)p.Entity.UID,"kind",(int)o.Kind,"npc",(long)o.Npc,"cost",(long)o.Cost,"before",(long)o.BeforeCp,"after",(long)(o.BeforeCp-o.Cost),
                    "product",(int)result.Product,"count",result.Count,"bound",o.Bound,"weapon",(long)(o.Weapon==null?0:o.Weapon.UID),
                    "old",(int)o.OldWeapon,"new",(int)o.NewWeapon,"eye",(long)o.HeartUid,"retry",result.RetryGem,"now",now)!=1)
                    throw new InvalidOperationException("Monk quest receipt not saved.");
                foreach(var a in o.Parts) {
                    string debit=a.After==0?"DELETE FROM items":"UPDATE items SET stacksize=@after";
                    if(Q.Execute(c,t,debit+@" WHERE uid=@item AND entityid=@uid AND id=@type AND position=0 AND warehouse=0
AND stacksize=@stack AND bound=@bound AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0'",
                        "after",a.After,"item",(long)a.Uid,"uid",(long)p.Entity.UID,"type",(int)a.Type,"stack",(int)a.Before,"bound",a.Bound?1:0)!=1)
                        throw new InvalidOperationException("Monk quest material changed.");
                    if(Q.Execute(c,t,"INSERT INTO revora_monk_epic_materials(operation,item_uid,item_type,stack_before,quantity,bound) VALUES(@op,@item,@type,@stack,@count,@bound)",
                        "op",o.Id,"item",(long)a.Uid,"type",(int)a.Type,"stack",(int)a.Before,"count",a.Count,"bound",a.Bound)!=1)throw new InvalidOperationException("Monk offering receipt not saved.");
                }
                for(int i=0;i<result.Count+(result.RetryGem?1:0);i++) {
                    var item=Q.New(i==result.Count?R.Gem:result.Product,o.Bound);rewards.Add(item);
                    if(Q.Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability)
VALUES(@item,@type,@uid,0,0,1,@bound,@dur,@max)","item",(long)item.UID,"type",(int)item.ID,"uid",(long)p.Entity.UID,"bound",item.Bound?1:0,"dur",(int)item.Durability,"max",(int)item.MaximDurability)!=1)
                        throw new InvalidOperationException("Monk quest reward not saved.");
                    if(Q.Execute(c,t,"INSERT INTO revora_monk_epic_rewards(operation,item_uid,item_type,bound) VALUES(@op,@item,@type,@bound)","op",o.Id,"item",(long)item.UID,"type",(int)item.ID,"bound",item.Bound)!=1)
                        throw new InvalidOperationException("Monk reward receipt not saved.");
                }
                if(o.Kind==Kind.Awaken) {
                    var soul=QuestSoulReward.Create();Q.Insert(c,t,p.Entity.UID,soul);rewards.Add(soul);
                    if(Q.Execute(c,t,"INSERT INTO revora_monk_epic_rewards(operation,item_uid,item_type,bound) VALUES(@op,@item,@type,true)",
                        "op",o.Id,"item",(long)soul.UID,"type",(int)soul.ID)!=1)throw new InvalidOperationException("Monk awakening soul receipt not saved.");
                }
                if(o.Weapon!=null && Q.Execute(c,t,@"UPDATE items SET id=@new,bound=@bound WHERE uid=@item AND entityid=@uid
AND id=@old AND position=@slot AND warehouse=0 AND COALESCE(bound,0)=@oldbound AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0",
                    "new",(int)o.NewWeapon,"bound",o.WeaponBound || o.Bound?1:0,"item",(long)o.Weapon.UID,"uid",(long)p.Entity.UID,
                    "old",(int)o.OldWeapon,"slot",(int)o.Slot,"oldbound",o.WeaponBound?1:0)!=1)throw new InvalidOperationException("Monk prayer beads changed.");
                if(!Same(p,o,context))throw new InvalidOperationException("Monk quest session changed before commit.");t.Commit();
            }}
            CharacterProgression.Committed(p,profile);if(o.Cost!=0)p.Entity.ApplyCommittedConquerPoints(o.BeforeCp-o.Cost);
            Q.Install(p,o.Parts.Where(a=>a.After==0).Select(a=>a.Uid),rewards);
            foreach(var a in o.Parts.Where(a=>a.After!=0)){a.Item.StackSize=checked((ushort)a.After);a.Item.Mode=Enums.ItemMode.Update;a.Item.Send(p);a.Item.Mode=Enums.ItemMode.Default;}
            if(o.Weapon!=null){o.Weapon.ID=o.NewWeapon;o.Weapon.Bound=o.WeaponBound || o.Bound;o.Weapon.Mode=Enums.ItemMode.Update;o.Weapon.Send(p);o.Weapon.Mode=Enums.ItemMode.Default;
                p.LoadItemStats();p.CalculateStatBonus();p.CalculateHPBonus();p.GemAlgorithm();p.Equipment.UpdateEntityPacket();}
            Q.Refresh(p);MonkEpicJournal.Refresh(p);return result;
        }
    }
}
