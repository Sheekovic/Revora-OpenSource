using System;
using System.Linq;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using R=Revora.Game.Features.PirateEpicRules;

namespace Revora.Game.Features
{
    // Private transaction core; no production initialization or dispatch yet.
    internal static partial class PirateEpicStore
    {
        internal sealed class Offer {
            internal Guid Id;internal uint Owner,HeartUid,OldWeapon,NewWeapon;
            internal ConquerItem Heart,Weapon,Right,Left;
            internal ushort Stack;internal bool HeartBound,WeaponBound;
            internal string Cache,Rows;internal DateTime Until;
        }
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_pirate_awakenings(
id uuid PRIMARY KEY,uid bigint NOT NULL,heart_uid bigint NOT NULL,heart_stack integer NOT NULL,
weapon_uid bigint NOT NULL,weapon_before integer NOT NULL,weapon_after integer NOT NULL,
weapon_slot integer NOT NULL CHECK(weapon_slot IN(4,5)),bound boolean NOT NULL,
soul_uid bigint NOT NULL UNIQUE,soul_type integer NOT NULL,created_at timestamptz NOT NULL,
UNIQUE(uid,weapon_uid,weapon_before));
CREATE TABLE IF NOT EXISTS revora_pirate_map_purchases(
id uuid PRIMARY KEY,uid bigint NOT NULL,item_uid bigint NOT NULL UNIQUE,
balance_before bigint NOT NULL CHECK(balance_before>=27),balance_after bigint NOT NULL CHECK(balance_after=balance_before-27),
created_at timestamptz NOT NULL);
CREATE TABLE IF NOT EXISTS revora_pirate_flowers(
id uuid PRIMARY KEY,uid bigint NOT NULL,day date NOT NULL,bottle_uid bigint NOT NULL UNIQUE,bound boolean NOT NULL,
material_uids bigint[] NOT NULL,quantities integer[] NOT NULL,created_at timestamptz NOT NULL,
UNIQUE(uid,day),CHECK(cardinality(material_uids)=cardinality(quantities) AND cardinality(material_uids) BETWEEN 2 AND 6));
CREATE TABLE IF NOT EXISTS revora_pirate_searches(
id uuid PRIMARY KEY,uid bigint NOT NULL,input_uid bigint NOT NULL,input_type integer NOT NULL,stack_before integer NOT NULL,
reward_uids bigint[] NOT NULL,reward_types integer[] NOT NULL,created_at timestamptz NOT NULL,
CHECK(cardinality(reward_uids)=cardinality(reward_types) AND cardinality(reward_uids) BETWEEN 1 AND 2));
CREATE TABLE IF NOT EXISTS revora_pirate_scroll_exchanges(
id uuid PRIMARY KEY,uid bigint NOT NULL,scroll_uid bigint NOT NULL UNIQUE,scroll_type integer NOT NULL,
water_count integer NOT NULL CHECK(water_count=10),bound boolean NOT NULL,created_at timestamptz NOT NULL);
CREATE TABLE IF NOT EXISTS revora_pirate_training(
id uuid PRIMARY KEY,uid bigint NOT NULL,scroll_uid bigint NOT NULL,scroll_type integer NOT NULL,
spell integer NOT NULL,created_at timestamptz NOT NULL);");}
            PirateEpicJournal.Initialize();
        }
        internal static bool Eligible(GameState p) {
            return Q.Player(p) && p.Trade!=null && !p.Trade.InTrade && p.Booth==null
                && p.Entity.PokerTable==0 && CharacterProgression.Managed(p)
                && R.Eligible(p.Entity.Class,p.Entity.Reborn,p.Entity.Level);
        }
        private static bool Gear(ConquerItem i) {
            return i!=null && i.StackSize<=1 && i.Warehouse==0 && !i.Suspicious && !i.InWardrobe
                && i.Days==0 && i.TimeLeftInMinutes==0 && ConquerItemInformation.BaseInformations.ContainsKey(i.ID);
        }
        internal static bool Heart(ConquerItem i) {
            return i!=null && i.ID==R.Heart && i.Position==0 && i.Lock==0 && Gear(i);
        }
        private static void Bag(NpgsqlConnection c,NpgsqlTransaction t,GameState p) {
            var ids=new System.Collections.Generic.List<uint>();
            using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid FOR UPDATE","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())while(r.Read()){
                if(ids.Count>=40)throw new InvalidOperationException("Pirate bag exceeds capacity.");ids.Add(checked((uint)Convert.ToInt64(r[0])));
            }
            if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Pirate bag requires reload.");
        }
        private static void GearImage(NpgsqlConnection c,NpgsqlTransaction t,GameState p,ConquerItem i) {
            if(i==null)return;
            string[] columns={"id","position","plus","bless","enchant","socketone","sockettwo","durability","maximdurability",
                "plusprogress","socketprogress","effect","color","perfectionlevel","perfectionownerguid","perfectionprogress","refineryitem","runesexp",
                "relicattrib1","relicattrib2","relicattrib3","relicattrib4","relicattrib5","bound","locked","stacksize"};
            long[] values={i.ID,i.Position,i.Plus,i.Bless,i.Enchant,(long)i.SocketOne,(long)i.SocketTwo,i.Durability,i.MaximDurability,
                i.PlusProgress,i.SocketProgress,(long)i.Effect,(long)i.Color,i.PerfectionLevel,i.PerfectionOwnerGuid,i.PerfectionProgress,i.RefineItem,i.RunesEXP,
                i.RelicAttrib1,i.RelicAttrib2,i.RelicAttrib3,i.RelicAttrib4,i.RelicAttrib5,i.Bound?1:0,i.Lock,i.StackSize};
            using(var q=Q.Command(c,t,"SELECT "+string.Join(",",columns)+",perfectionowner,signature FROM items WHERE uid=@item AND entityid=@uid AND warehouse=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0' FOR UPDATE",
                "item",(long)i.UID,"uid",(long)p.Entity.UID))using(var r=q.ExecuteReader()) {
                if(!r.Read())throw new InvalidOperationException("Pirate equipment owner missing.");
                for(int field=0;field<columns.Length;field++)if((r.IsDBNull(field)?0:Convert.ToInt64(r[field]))!=values[field])throw new InvalidOperationException("Pirate equipment metadata requires reload.");
                if(Convert.ToString(r[columns.Length])!=(i.PerfectionOwner??"") || Convert.ToString(r[columns.Length+1])!=(i.Signature??""))throw new InvalidOperationException("Pirate equipment attribution requires reload.");
            }
        }
        internal static Offer Prepare(GameState p,uint heartUid,out string error) {
            if(p==null || !Monitor.IsEntered(p.ItemSyncRoot))throw new InvalidOperationException("Pirate conversion requires owner lock.");
            error=null;ConquerItem heart;
            if(!Eligible(p) || p.AlternateEquipment){error="This blessing awaits second-reborn Pirates of level 70.";return null;}
            if(!p.Inventory.TryGetItem(heartUid,out heart) || !Heart(heart)){error="Bring an unlocked, permanent Heart of the Sea.";return null;}
            var right=p.Equipment.TryGetItem(4);var left=p.Equipment.TryGetItem(5);
            byte slot=R.ConversionSlot(right==null?0:right.ID,left==null?0:left.ID,ConquerItemInformation.BaseInformations.ContainsKey);
            var weapon=slot==4?right:slot==5?left:null;
            if(slot==0 || !Gear(right) || !Gear(weapon) || right.Position!=4 || weapon.Position!=slot){error="Equip a rapier, or an epic rapier with a pistol, whose epic form is known.";return null;}
            var o=new Offer{Id=Guid.NewGuid(),Owner=p.Entity.UID,HeartUid=heartUid,Heart=heart,Stack=heart.StackSize,HeartBound=heart.Bound,
                Weapon=weapon,Right=right,Left=left,OldWeapon=weapon.ID,NewWeapon=R.EpicWeapon(weapon.ID,ConquerItemInformation.BaseInformations.ContainsKey),
                WeaponBound=weapon.Bound,Until=Clock().AddMinutes(2),Cache=FirstRebirth.Cache(p)};
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                SkillTable.LockOwner(c,t,p);Bag(c,t,p);GearImage(c,t,p,right);GearImage(c,t,p,left);GearImage(c,t,p,heart);
                o.Rows=FirstRebirth.Rows(c,t,p,"items");t.Commit();
            }}return o;
        }
        private static bool Same(GameState p,Offer o,Func<bool> context) {
            ConquerItem heart;
            return o!=null && context!=null && Eligible(p) && !p.AlternateEquipment && p.Entity.UID==o.Owner && Clock()<o.Until && context()
                && o.Cache==FirstRebirth.Cache(p) && p.Inventory.TryGetItem(o.HeartUid,out heart) && ReferenceEquals(heart,o.Heart) && Heart(heart)
                && ReferenceEquals(p.Equipment.TryGetItem(4),o.Right) && ReferenceEquals(p.Equipment.TryGetItem(5),o.Left)
                && R.ConversionSlot(o.Right.ID,o.Left==null?0:o.Left.ID,ConquerItemInformation.BaseInformations.ContainsKey)==o.Weapon.Position;
        }
        internal static void Commit(GameState p,Offer o,Func<bool> context) {
            if(p==null || !Monitor.IsEntered(p.ItemSyncRoot) || !Same(p,o,context))throw new InvalidOperationException("Pirate conversion offer changed.");
            var profile=CharacterProgression.Capture(p);var soul=QuestSoulReward.Create();
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                SkillTable.LockOwner(c,t,p);Bag(c,t,p);
                if(FirstRebirth.Rows(c,t,p,"items")!=o.Rows)throw new InvalidOperationException("Saved Pirate items changed.");
                CharacterProgression.Persist(c,t,p,profile);
                if(Q.Execute(c,t,@"DELETE FROM items WHERE uid=@item AND entityid=@uid AND id=@type AND position=0 AND warehouse=0
AND stacksize=@stack AND bound=@bound AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0'",
                    "item",(long)o.HeartUid,"uid",(long)o.Owner,"type",(int)R.Heart,"stack",(int)o.Stack,"bound",o.HeartBound?1:0)!=1)
                    throw new InvalidOperationException("Pirate Heart changed.");
                if(Q.Execute(c,t,@"UPDATE items SET id=@new,bound=@bound WHERE uid=@item AND entityid=@uid AND id=@old AND position=@slot
AND warehouse=0 AND COALESCE(bound,0)=@oldbound AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0",
                    "new",(int)o.NewWeapon,"bound",o.WeaponBound || o.HeartBound?1:0,"item",(long)o.Weapon.UID,"uid",(long)o.Owner,
                    "old",(int)o.OldWeapon,"slot",(int)o.Weapon.Position,"oldbound",o.WeaponBound?1:0)!=1)throw new InvalidOperationException("Pirate weapon changed.");
                Q.Insert(c,t,o.Owner,soul);
                if(Q.Execute(c,t,@"INSERT INTO revora_pirate_awakenings(id,uid,heart_uid,heart_stack,weapon_uid,weapon_before,weapon_after,weapon_slot,bound,soul_uid,soul_type,created_at)
VALUES(@id,@uid,@heart,@stack,@weapon,@old,@new,@slot,@bound,@soul,@type,@now)",
                    "id",o.Id,"uid",(long)o.Owner,"heart",(long)o.HeartUid,"stack",(int)o.Stack,"weapon",(long)o.Weapon.UID,
                    "old",(int)o.OldWeapon,"new",(int)o.NewWeapon,"slot",(int)o.Weapon.Position,"bound",o.WeaponBound || o.HeartBound,
                    "soul",(long)soul.UID,"type",(int)soul.ID,"now",Clock())!=1)throw new InvalidOperationException("Pirate conversion receipt missing.");
                if(!Same(p,o,context))throw new InvalidOperationException("Pirate conversion session changed before commit.");t.Commit();
            }}
            CharacterProgression.Committed(p,profile);Q.Install(p,new[]{o.HeartUid},new[]{soul});
            o.Weapon.ID=o.NewWeapon;o.Weapon.Bound=o.WeaponBound || o.HeartBound;o.Weapon.Mode=Enums.ItemMode.Update;
            o.Weapon.Send(p);o.Weapon.Mode=Enums.ItemMode.Default;
            p.LoadItemStats();p.CalculateStatBonus();p.CalculateHPBonus();p.GemAlgorithm();p.Equipment.UpdateEntityPacket();Q.Refresh(p);PirateEpicJournal.Refresh(p);
        }
    }
}
