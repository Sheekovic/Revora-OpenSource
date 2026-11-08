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
    // Opening earning transactions; no public admission/initialization hook yet.
    internal static partial class AttackTalismanStore
    {
        internal const uint WinePrice=10000;
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal sealed class Run {internal int Stage;internal uint Soul;internal bool CavePaid;internal int Alchemy;internal int LakeStep;internal int CourtesanSite;internal int Pillars;internal int BoxWinner,Boxes,PendingGhost;internal bool FanTaken,BookGiven;}
        internal static bool Eligible(GameState p){return DefenseTalismanStore.Eligible(p);}
        private static void Ready(GameState p,Func<bool> context){if(!Eligible(p) || !Monitor.IsEntered(p.ItemSyncRoot) || context==null || !context())throw new InvalidOperationException("Attack Talisman context changed.");}
        private static bool Near(GameState p,ushort map,ushort x,ushort y){return p.Entity.MapID==map && Kernel.GetDistance(p.Entity.X,p.Entity.Y,x,y)<=6;}
        internal static void Initialize(){
            foreach(uint id in new uint[]{201009,710216,710217,710218,710219,710220,710221,710222,721798,721799,721800,721801,721802,721803,721804,721806})
                if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Fan catalog missing.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_attack_talisman(
uid bigint PRIMARY KEY REFERENCES revora_defense_talisman(uid),stage smallint NOT NULL CHECK(stage BETWEEN 1 AND 11),soul bigint NOT NULL,rewards bigint[] NOT NULL DEFAULT '{}');
ALTER TABLE revora_attack_talisman ADD COLUMN IF NOT EXISTS cave_paid boolean NOT NULL DEFAULT false;
ALTER TABLE revora_attack_talisman ADD COLUMN IF NOT EXISTS alchemy smallint NOT NULL DEFAULT 0 CHECK(alchemy BETWEEN 0 AND 3);
ALTER TABLE revora_attack_talisman ADD COLUMN IF NOT EXISTS lake_step smallint NOT NULL DEFAULT 0 CHECK(lake_step BETWEEN 0 AND 3);
ALTER TABLE revora_attack_talisman ADD COLUMN IF NOT EXISTS courtesan_site smallint NOT NULL DEFAULT -1 CHECK(courtesan_site BETWEEN -1 AND 3);
ALTER TABLE revora_attack_talisman ADD COLUMN IF NOT EXISTS pillars smallint NOT NULL DEFAULT 0 CHECK(pillars BETWEEN 0 AND 15);
ALTER TABLE revora_attack_talisman ADD COLUMN IF NOT EXISTS box_winner smallint NOT NULL DEFAULT -1 CHECK(box_winner BETWEEN -1 AND 2);
ALTER TABLE revora_attack_talisman ADD COLUMN IF NOT EXISTS boxes smallint NOT NULL DEFAULT 0 CHECK(boxes BETWEEN 0 AND 7);
ALTER TABLE revora_attack_talisman ADD COLUMN IF NOT EXISTS pending_ghost smallint NOT NULL DEFAULT -1 CHECK(pending_ghost BETWEEN -1 AND 2);
ALTER TABLE revora_attack_talisman ADD COLUMN IF NOT EXISTS fan_taken boolean NOT NULL DEFAULT false;
ALTER TABLE revora_attack_talisman ADD COLUMN IF NOT EXISTS book_given boolean NOT NULL DEFAULT false;
CREATE TABLE IF NOT EXISTS revora_fan_ghost_kills(uid bigint NOT NULL REFERENCES revora_attack_talisman(uid),box smallint NOT NULL CHECK(box BETWEEN 0 AND 2),attempt uuid NOT NULL UNIQUE,body bigint NOT NULL,PRIMARY KEY(uid,box));
CREATE TABLE IF NOT EXISTS revora_fan_courtesan_kills(uid bigint PRIMARY KEY REFERENCES revora_attack_talisman(uid),attempt uuid NOT NULL UNIQUE,body bigint NOT NULL,site smallint NOT NULL CHECK(site BETWEEN 0 AND 3));
CREATE TABLE IF NOT EXISTS revora_fan_lake_kills(uid bigint PRIMARY KEY REFERENCES revora_attack_talisman(uid),attempt uuid NOT NULL UNIQUE,body bigint NOT NULL);
CREATE TABLE IF NOT EXISTS revora_fan_ambush_kills(uid bigint NOT NULL REFERENCES revora_attack_talisman(uid),slot smallint NOT NULL CHECK(slot BETWEEN 0 AND 4),attempt uuid NOT NULL UNIQUE,body bigint NOT NULL,PRIMARY KEY(uid,slot));
CREATE TABLE IF NOT EXISTS revora_fan_cave_kills(uid bigint NOT NULL REFERENCES revora_attack_talisman(uid),kind smallint NOT NULL CHECK(kind BETWEEN 0 AND 1),attempt uuid NOT NULL UNIQUE,body bigint NOT NULL,PRIMARY KEY(uid,kind));
CREATE TABLE IF NOT EXISTS revora_attack_talisman_materials(item_uid bigint PRIMARY KEY,uid bigint NOT NULL REFERENCES revora_attack_talisman(uid),
type integer NOT NULL CHECK(type IN(710216,710217,710218,710219,710220,710221,710222,721798,721799,721800,721801,721802,721803,721804,721806)),state smallint NOT NULL DEFAULT 0 CHECK(state BETWEEN 0 AND 2));
CREATE TABLE IF NOT EXISTS revora_attack_talisman_recoveries(old_uid bigint PRIMARY KEY,new_uid bigint NOT NULL UNIQUE,uid bigint NOT NULL REFERENCES revora_attack_talisman(uid),type integer NOT NULL);
CREATE TABLE IF NOT EXISTS revora_attack_talisman_purchases(item_uid bigint PRIMARY KEY,uid bigint NOT NULL REFERENCES revora_attack_talisman(uid),price bigint NOT NULL CHECK(price>0));");}
        }
        private static Run Read(NpgsqlConnection c,NpgsqlTransaction t,GameState p){using(var q=Q.Command(c,t,"SELECT stage,soul,cave_paid,alchemy,lake_step,courtesan_site,pillars,box_winner,boxes,pending_ghost,fan_taken,book_given FROM revora_attack_talisman WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())return r.Read()?new Run{Stage=r.GetInt16(0),Soul=checked((uint)r.GetInt64(1)),CavePaid=r.GetBoolean(2),Alchemy=r.GetInt16(3),LakeStep=r.GetInt16(4),CourtesanSite=r.GetInt16(5),Pillars=r.GetInt16(6),BoxWinner=r.GetInt16(7),Boxes=r.GetInt16(8),PendingGhost=r.GetInt16(9),FanTaken=r.GetBoolean(10),BookGiven=r.GetBoolean(11)}:new Run();}
        internal static Run Load(GameState p){if(!Q.Player(p,false))return null;lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();return Read(c,null,p);}}
        private static void Owner(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,GameState p)
        {
            SkillTable.LockOwner(c,t,p);
            using(var q=Q.Command(c,t,"SELECT reborn,level FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))using(var r=q.ExecuteReader())
                if(!r.Read() || Convert.ToInt32(r[0])!=p.Entity.Reborn || Convert.ToInt32(r[1])!=p.Entity.Level)throw new InvalidOperationException("AttackTalisman saved owner changed.");
            var saved=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid FOR UPDATE","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())while(r.Read())saved.Add(checked((uint)r.GetInt64(0)));
            if(!saved.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("AttackTalisman bag changed.");
        }

        private static bool Material(NpgsqlConnection c,NpgsqlTransaction t,GameState p,uint uid,uint type){ConquerItem item;
            if(uid==0 || !p.Inventory.TryGetItem(uid,out item) || item.Lock!=0 || item.Suspicious || item.InWardrobe || item.Days!=0 || item.TimeLeftInMinutes!=0 || !Q.Owns(c,t,p,uid,type))return false;
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_attack_talisman_materials m JOIN items i ON i.uid=m.item_uid WHERE m.item_uid=@id AND m.uid=@uid AND m.type=@type AND m.state=0 AND i.locked=0 AND COALESCE(i.suspicious,0)=0 AND COALESCE(i.inwardrobe,0)=0 AND COALESCE(NULLIF(i.days,''),'0')='0')","id",(long)uid,"uid",(long)p.Entity.UID,"type",(int)type))return Equals(q.ExecuteScalar(),true);
        }
        private static List<uint> Materials(NpgsqlConnection c,NpgsqlTransaction t,GameState p,uint type){
            var ids=new List<uint>();using(var q=Q.Command(c,t,"SELECT item_uid FROM revora_attack_talisman_materials WHERE uid=@uid AND type=@type AND state=0 ORDER BY item_uid FOR UPDATE","uid",(long)p.Entity.UID,"type",(int)type))using(var r=q.ExecuteReader())while(r.Read())ids.Add(checked((uint)r.GetInt64(0)));return ids;
        }
        private static void Track(NpgsqlConnection c,NpgsqlTransaction t,GameState p,ConquerItem item){Q.Insert(c,t,p.Entity.UID,item);
            if(Q.Execute(c,t,"INSERT INTO revora_attack_talisman_materials(item_uid,uid,type) VALUES(@item,@uid,@type)","item",(long)item.UID,"uid",(long)p.Entity.UID,"type",(int)item.ID)!=1)throw new InvalidOperationException("AttackTalisman material tracking failed.");
        }
        private static void Consume(NpgsqlConnection c,NpgsqlTransaction t,GameState p,uint uid,uint type){
            if(!Material(c,t,p,uid,type))throw new InvalidOperationException("AttackTalisman material ownership changed.");Q.Consume(c,t,p,uid,type);
            if(Q.Execute(c,t,"UPDATE revora_attack_talisman_materials SET state=1 WHERE item_uid=@item AND uid=@uid AND state=0","item",(long)uid,"uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("AttackTalisman consumption was not saved.");
        }

        internal static bool Accept(GameState p,Func<bool> context){
            Ready(p,context);if(!Near(p,1015,685,599) || p.Inventory.Count>=40)return false;ConquerItem jade;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);if(Read(c,t,p).Stage!=0)return false;
                using(var q=Q.Command(c,t,"SELECT stage=10 AND COALESCE(array_length(rewards,1),0)=2 FROM revora_defense_talisman WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))if(!Equals(q.ExecuteScalar(),true))return false;
                if(Q.Execute(c,t,"INSERT INTO revora_attack_talisman(uid,stage,soul) VALUES(@uid,1,@soul)","uid",(long)p.Entity.UID,"soul",(long)QuestSoulReward.Create().ID)!=1)throw new InvalidOperationException("Fan admission changed.");
                jade=Q.New(710218,true);Track(c,t,p,jade);Ready(p,()=>context() && Near(p,1015,685,599));t.Commit();
            }}Q.Install(p,new uint[0],new[]{jade});return true;
        }
        internal static bool Referral(GameState p,int expected,Func<bool> context){
            Ready(p,context);ushort map=expected==1?(ushort)1011:expected==3?(ushort)1015:(ushort)1000;
            ushort x=expected==1?(ushort)788:expected==3?(ushort)685:(ushort)490,y=expected==1?(ushort)456:expected==3?(ushort)599:(ushort)618;
            if(expected!=1 && expected!=3 && expected!=4 || !Near(p,map,x,y))return false;ulong silver;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);if(Read(c,t,p).Stage!=expected || !Materials(c,t,p,710218).Any(id=>Material(c,t,p,id,710218))
                    || expected>=3 && !Materials(c,t,p,721798).Any(id=>Material(c,t,p,id,721798)))return false;
                SilverBalance.AssertSaved(c,t,p);silver=checked(p.Entity.Money+100);if(silver>Entity.MaximumSilver)return false;SilverBalance.Persist(c,t,p,silver);
                Save(c,t,p,expected,expected+1);Ready(p,()=>context() && Near(p,map,x,y));t.Commit();
            }}SilverBalance.ApplyCommitted(p,silver);return true;
        }
        private static void Save(NpgsqlConnection c,NpgsqlTransaction t,GameState p,int before,int after){
            if(Q.Execute(c,t,"UPDATE revora_attack_talisman SET stage=@after WHERE uid=@uid AND stage=@before","uid",(long)p.Entity.UID,"before",before,"after",after)!=1)throw new InvalidOperationException("Fan stage changed.");
        }
        internal static bool BuyWine(GameState p,Func<bool> context){
            Ready(p,context);if(!Near(p,1011,182,280))return false;ConquerItem wine;ulong silver;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);if(Read(c,t,p).Stage!=2 || p.Inventory.Count>=40 || p.Entity.Money<WinePrice || Materials(c,t,p,721800).Count!=0)return false;
                SilverBalance.AssertSaved(c,t,p);silver=p.Entity.Money-WinePrice;wine=Q.New(721800,true);Track(c,t,p,wine);SilverBalance.Persist(c,t,p,silver);
                Q.Execute(c,t,"INSERT INTO revora_attack_talisman_purchases(item_uid,uid,price) VALUES(@item,@uid,@price)","item",(long)wine.UID,"uid",(long)p.Entity.UID,"price",(long)WinePrice);
                Ready(p,()=>context() && Near(p,1011,182,280));t.Commit();
            }}Q.Install(p,new uint[0],new[]{wine});SilverBalance.ApplyCommitted(p,silver);return true;
        }
        internal static bool ExchangeMeal(GameState p,Func<bool> context){
            Ready(p,context);if(!Near(p,1011,788,456))return false;uint wine;uint[] meat;ConquerItem silk;ulong silver;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);if(Read(c,t,p).Stage!=2 || !Materials(c,t,p,710218).Any(id=>Material(c,t,p,id,710218)))return false;
                wine=Materials(c,t,p,721800).FirstOrDefault(id=>Material(c,t,p,id,721800));meat=Materials(c,t,p,710220).Where(id=>Material(c,t,p,id,710220)).Take(5).ToArray();if(wine==0 || meat.Length!=5)return false;
                SilverBalance.AssertSaved(c,t,p);silver=checked(p.Entity.Money+100);if(silver>Entity.MaximumSilver)return false;
                Consume(c,t,p,wine,721800);foreach(uint id in meat)Consume(c,t,p,id,710220);silk=Q.New(721798,true);Track(c,t,p,silk);
                SilverBalance.Persist(c,t,p,silver);Save(c,t,p,2,3);Ready(p,()=>context() && Near(p,1011,788,456));t.Commit();
            }}Q.Install(p,meat.Concat(new[]{wine}).ToArray(),new[]{silk});SilverBalance.ApplyCommitted(p,silver);return true;
        }
        internal static bool Recover(GameState p,uint type,Func<bool> context){
            Ready(p,context);ConquerItem added;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(p.Inventory.Count>=40)return false;
                bool allowed=run.Stage>=1 && run.Stage<=9 && type==710218 || run.Stage==2 && (type==721800 || type==710220)
                    || run.Stage>=3 && run.Stage<=5 && type==721798 || run.Stage==5 && (type==721806 || run.Alchemy<=1 && type==721803 || run.Alchemy==1 && type==721802 || run.Alchemy==2 && type==721804)
                    || (run.Stage==6 || run.Stage==7) && type==721801 || run.Stage==8 && run.LakeStep==0 && type==721799 || (run.Stage==8 || run.Stage==9) && (type==710219 || type==710216) || run.Stage==9 && type==710217 || run.Stage==10 && (run.FanTaken && !run.BookGiven && type==710221 || run.BookGiven && type==710222);
                if(!allowed)return false;uint old=0;
                using(var q=Q.Command(c,t,@"SELECT item_uid FROM revora_attack_talisman_materials WHERE uid=@uid AND type=@type AND state=0
AND NOT EXISTS(SELECT 1 FROM items WHERE uid=item_uid) ORDER BY item_uid LIMIT 1 FOR UPDATE",
                    "uid",(long)p.Entity.UID,"type",(int)type)){
                    object value=q.ExecuteScalar();if(value!=null)old=checked((uint)Convert.ToInt64(value));
                }if(old==0)return false;
                added=Q.New(type,true);Track(c,t,p,added);
                if(Q.Execute(c,t,"UPDATE revora_attack_talisman_materials SET state=2 WHERE item_uid=@old AND state=0","old",(long)old)!=1)throw new InvalidOperationException("Attack Talisman lost token changed.");
                if(Q.Execute(c,t,"INSERT INTO revora_attack_talisman_recoveries(old_uid,new_uid,uid,type) VALUES(@old,@new,@uid,@type)",
                    "old",(long)old,"new",(long)added.UID,"uid",(long)p.Entity.UID,"type",(int)type)!=1)throw new InvalidOperationException("Attack Talisman recovery receipt missing.");
                Ready(p,context);t.Commit();
            }}Q.Install(p,new uint[0],new[]{added});return true;
        }
    }
}
