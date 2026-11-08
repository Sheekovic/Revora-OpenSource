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
    // Durable quest state; ordinary interactions are coordinated by DefenseTalismanQuest.
    internal static partial class DefenseTalismanStore
    {
        internal const uint NetPrice=30000; // Explicit Revora silver price, not verified original pricing.
        internal sealed class Run {internal int Stage;internal uint Soul;internal int DoorStep;internal bool SpearRequested;internal int DesertStep;internal int DungeonFloor;}
        internal static bool Eligible(GameState p){return Q.Player(p) && ReferenceEquals(p.Entity.Owner,p) && CharacterProgression.Managed(p)
            && p.Entity.Level>=100 && p.Entity.Reborn<=2 && p.Trade!=null && !p.Trade.InTrade && p.Booth==null && p.Entity.PokerTable==0;}
        private static void Ready(GameState p,Func<bool> context){if(!Eligible(p) || !Monitor.IsEntered(p.ItemSyncRoot) || context==null || !context())throw new InvalidOperationException("Defense Talisman context changed.");}
        internal static void Initialize(){
            foreach(uint id in new uint[]{202008,721777,721778,721779,721780,721781,721782,721784,721785,721786,721788,721789,721790,721791,721792,721793,721794})
                if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Defense Talisman catalog missing.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_defense_talisman(
uid bigint PRIMARY KEY,stage smallint NOT NULL CHECK(stage BETWEEN 1 AND 10),soul bigint NOT NULL,rewards bigint[] NOT NULL DEFAULT '{}');
ALTER TABLE revora_defense_talisman ADD COLUMN IF NOT EXISTS door_step smallint NOT NULL DEFAULT 0 CHECK(door_step BETWEEN 0 AND 3);
ALTER TABLE revora_defense_talisman ADD COLUMN IF NOT EXISTS spear_requested boolean NOT NULL DEFAULT false;
ALTER TABLE revora_defense_talisman ADD COLUMN IF NOT EXISTS desert_step smallint NOT NULL DEFAULT 0 CHECK(desert_step BETWEEN 0 AND 2);
ALTER TABLE revora_defense_talisman ADD COLUMN IF NOT EXISTS dungeon_floor smallint NOT NULL DEFAULT 0 CHECK(dungeon_floor BETWEEN 0 AND 3);
CREATE TABLE IF NOT EXISTS revora_defense_talisman_materials(item_uid bigint PRIMARY KEY,uid bigint NOT NULL REFERENCES revora_defense_talisman(uid),
type integer NOT NULL CHECK(type IN(721777,721778,721779,721780,721781,721782,721784,721785,721786,721788,721789,721790,721791,721792,721793,721794)),
state smallint NOT NULL DEFAULT 0 CHECK(state BETWEEN 0 AND 2));
CREATE TABLE IF NOT EXISTS revora_defense_talisman_recoveries(old_uid bigint PRIMARY KEY,new_uid bigint NOT NULL UNIQUE,
uid bigint NOT NULL REFERENCES revora_defense_talisman(uid),type integer NOT NULL);
CREATE TABLE IF NOT EXISTS revora_defense_talisman_purchases(item_uid bigint PRIMARY KEY,uid bigint NOT NULL REFERENCES revora_defense_talisman(uid),
price bigint NOT NULL CHECK(price>0));
CREATE TABLE IF NOT EXISTS revora_defense_talisman_fishing(attempt uuid PRIMARY KEY,
uid bigint NOT NULL REFERENCES revora_defense_talisman(uid),body bigint NOT NULL,net bigint NOT NULL UNIQUE);
CREATE TABLE IF NOT EXISTS revora_tower_shawn_kills(attempt uuid PRIMARY KEY,uid bigint NOT NULL UNIQUE REFERENCES revora_defense_talisman(uid),body bigint NOT NULL);
CREATE TABLE IF NOT EXISTS revora_tower_desert_kills(attempt uuid PRIMARY KEY,uid bigint NOT NULL REFERENCES revora_defense_talisman(uid),
kind smallint NOT NULL CHECK(kind BETWEEN 3 AND 4),body bigint NOT NULL,UNIQUE(uid,kind));
CREATE TABLE IF NOT EXISTS revora_tower_general_kills(uid bigint NOT NULL REFERENCES revora_defense_talisman(uid),
slot smallint NOT NULL CHECK(slot BETWEEN 0 AND 3),attempt uuid NOT NULL UNIQUE,body bigint NOT NULL,PRIMARY KEY(uid,slot));
CREATE TABLE IF NOT EXISTS revora_tower_feng_kills(uid bigint PRIMARY KEY REFERENCES revora_defense_talisman(uid),attempt uuid NOT NULL UNIQUE,body bigint NOT NULL);");}
        }
        private static Run Read(NpgsqlConnection c,NpgsqlTransaction t,GameState p){
            using(var q=Q.Command(c,t,"SELECT stage,soul,door_step,spear_requested,desert_step,dungeon_floor FROM revora_defense_talisman WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())return r.Read()?new Run{Stage=r.GetInt16(0),Soul=checked((uint)r.GetInt64(1)),DoorStep=r.GetInt16(2),SpearRequested=r.GetBoolean(3),DesertStep=r.GetInt16(4),DungeonFloor=r.GetInt16(5)}:new Run();
        }
        internal static Run Load(GameState p){if(!Q.Player(p,false))return null;lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();return Read(c,null,p);}}
        private static void Owner(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,GameState p)
        {
            SkillTable.LockOwner(c,t,p);
            using(var q=Q.Command(c,t,"SELECT reborn,level FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))using(var r=q.ExecuteReader())
                if(!r.Read() || Convert.ToInt32(r[0])!=p.Entity.Reborn || Convert.ToInt32(r[1])!=p.Entity.Level)throw new InvalidOperationException("DefenseTalisman saved owner changed.");
            var saved=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid FOR UPDATE","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())while(r.Read())saved.Add(checked((uint)r.GetInt64(0)));
            if(!saved.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("DefenseTalisman bag changed.");
        }

        private static bool Material(NpgsqlConnection c,NpgsqlTransaction t,GameState p,uint uid,uint type){ConquerItem item;
            if(uid==0 || !p.Inventory.TryGetItem(uid,out item) || item.Lock!=0 || item.Suspicious || item.InWardrobe || item.Days!=0 || item.TimeLeftInMinutes!=0 || !Q.Owns(c,t,p,uid,type))return false;
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_defense_talisman_materials m JOIN items i ON i.uid=m.item_uid WHERE m.item_uid=@id AND m.uid=@uid AND m.type=@type AND m.state=0 AND i.locked=0 AND COALESCE(i.suspicious,0)=0 AND COALESCE(i.inwardrobe,0)=0 AND COALESCE(NULLIF(i.days,''),'0')='0')","id",(long)uid,"uid",(long)p.Entity.UID,"type",(int)type))return Equals(q.ExecuteScalar(),true);
        }
        private static List<uint> Materials(NpgsqlConnection c,NpgsqlTransaction t,GameState p,uint type){
            var ids=new List<uint>();using(var q=Q.Command(c,t,"SELECT item_uid FROM revora_defense_talisman_materials WHERE uid=@uid AND type=@type AND state=0 ORDER BY item_uid FOR UPDATE","uid",(long)p.Entity.UID,"type",(int)type))using(var r=q.ExecuteReader())while(r.Read())ids.Add(checked((uint)r.GetInt64(0)));return ids;
        }
        private static void Track(NpgsqlConnection c,NpgsqlTransaction t,GameState p,ConquerItem item){Q.Insert(c,t,p.Entity.UID,item);
            if(Q.Execute(c,t,"INSERT INTO revora_defense_talisman_materials(item_uid,uid,type) VALUES(@item,@uid,@type)","item",(long)item.UID,"uid",(long)p.Entity.UID,"type",(int)item.ID)!=1)throw new InvalidOperationException("DefenseTalisman material tracking failed.");
        }
        private static void Consume(NpgsqlConnection c,NpgsqlTransaction t,GameState p,uint uid,uint type){
            if(!Material(c,t,p,uid,type))throw new InvalidOperationException("DefenseTalisman material ownership changed.");Q.Consume(c,t,p,uid,type);
            if(Q.Execute(c,t,"UPDATE revora_defense_talisman_materials SET state=1 WHERE item_uid=@item AND uid=@uid AND state=0","item",(long)uid,"uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("DefenseTalisman consumption was not saved.");
        }
        internal static bool Accept(GameState p,Func<bool> context){
            Ready(p,context);
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);if(Read(c,t,p).Stage!=0)return false;
                if(Q.Execute(c,t,"INSERT INTO revora_defense_talisman(uid,stage,soul) VALUES(@uid,1,@soul)","uid",(long)p.Entity.UID,"soul",(long)QuestSoulReward.Create().ID)!=1)
                    throw new InvalidOperationException("Defense Talisman acceptance failed.");
                Ready(p,context);t.Commit();return true;
            }}
        }
        internal static bool BuyNet(GameState p,Func<bool> context){
            Ready(p,context);ConquerItem added;ulong silver;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);if(Read(c,t,p).Stage!=3 || p.Inventory.Count>=40 || p.Entity.Money<NetPrice
                    || Materials(c,t,p,721779).Count!=0 || Materials(c,t,p,721780).Count!=0 || Materials(c,t,p,721778).Count>=3)return false;
                SilverBalance.AssertSaved(c,t,p);silver=p.Entity.Money-NetPrice;added=Q.New(721779,true);Track(c,t,p,added);
                SilverBalance.Persist(c,t,p,silver);
                if(Q.Execute(c,t,"INSERT INTO revora_defense_talisman_purchases(item_uid,uid,price) VALUES(@item,@uid,@price)","item",(long)added.UID,"uid",(long)p.Entity.UID,"price",(long)NetPrice)!=1)
                    throw new InvalidOperationException("Defense Talisman net receipt failed.");
                Ready(p,context);t.Commit();
            }}Q.Install(p,new uint[0],new[]{added});SilverBalance.ApplyCommitted(p,silver);return true;
        }
        internal static bool ExchangeFish(GameState p,Func<bool> context){
            Ready(p,context);uint[] removed;ConquerItem added;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);if(Read(c,t,p).Stage!=3 || Materials(c,t,p,721780).Count!=0)return false;
                removed=Materials(c,t,p,721778).Where(id=>Material(c,t,p,id,721778)).Take(3).ToArray();
                if(removed.Length!=3)return false;
                foreach(uint id in removed)Consume(c,t,p,id,721778);
                added=Q.New(721780,true);Track(c,t,p,added);Ready(p,context);t.Commit();
            }}Q.Install(p,removed,new[]{added});return true;
        }
        internal static bool Recover(GameState p,uint type,Func<bool> context){
            Ready(p,context);ConquerItem added;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(p.Inventory.Count>=40)return false;
                bool allowed=run.Stage==3 && (type==721779 || type==721778 || type==721780)
                    || run.Stage==4 && type==721781 || (run.Stage==5 || run.Stage==6 && run.DesertStep<2) && type==721777
                    || run.Stage==6 && run.DesertStep==2 && type==721782 || run.Stage==8 && (type==721786 || type==721777 || GeneralWeapons.Contains(type) || Clinkers.Contains(type)) || run.Stage==9 && type==721785;
                if(!allowed)return false;uint old=0;
                using(var q=Q.Command(c,t,@"SELECT item_uid FROM revora_defense_talisman_materials WHERE uid=@uid AND type=@type AND state=0
AND NOT EXISTS(SELECT 1 FROM items WHERE uid=item_uid) ORDER BY item_uid LIMIT 1 FOR UPDATE",
                    "uid",(long)p.Entity.UID,"type",(int)type)){
                    object value=q.ExecuteScalar();if(value!=null)old=checked((uint)Convert.ToInt64(value));
                }if(old==0)return false;
                added=Q.New(type,true);Track(c,t,p,added);
                if(Q.Execute(c,t,"UPDATE revora_defense_talisman_materials SET state=2 WHERE item_uid=@old AND state=0","old",(long)old)!=1)throw new InvalidOperationException("Defense Talisman lost token changed.");
                if(Q.Execute(c,t,"INSERT INTO revora_defense_talisman_recoveries(old_uid,new_uid,uid,type) VALUES(@old,@new,@uid,@type)",
                    "old",(long)old,"new",(long)added.UID,"uid",(long)p.Entity.UID,"type",(int)type)!=1)throw new InvalidOperationException("Defense Talisman recovery receipt missing.");
                Ready(p,context);t.Commit();
            }}Q.Install(p,new uint[0],new[]{added});return true;
        }
    }
}
