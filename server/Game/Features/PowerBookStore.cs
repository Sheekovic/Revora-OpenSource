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
    // The coordinator must hold the owner lock, verify the registered NPC/scene
    // on both context checks and fence any failed installation after commit.
    // No player-facing route is enabled by this store alone.
    internal static partial class PowerBookStore
    {
        internal const uint Badge=721715,Book=721924,Meteor=1088001;
        internal sealed class Run {internal int Stage,CountA,CountB;internal DateTime? Window,LastKill;internal uint BadgeUid,BookUid,Soul,MaterialUid,MaterialType;}
        internal static void Initialize()
        {
            foreach(uint id in new[]{Badge,Book,Meteor,721926u,721928u,721929u,721931u})if(!ConquerItemInformation.BaseInformations.ContainsKey(id))
                throw new InvalidOperationException("Power Book item catalog missing.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"
CREATE TABLE IF NOT EXISTS revora_power_book(
uid bigint NOT NULL,life smallint NOT NULL CHECK(life BETWEEN 0 AND 2),stage smallint NOT NULL CHECK(stage BETWEEN 1 AND 14),
badge_uid bigint NOT NULL UNIQUE CHECK(badge_uid>0),book_uid bigint NOT NULL DEFAULT 0,
soul_type bigint NOT NULL CHECK(soul_type BETWEEN 800000 AND 899999),accepted_at timestamptz NOT NULL DEFAULT now(),
PRIMARY KEY(uid,life));
CREATE TABLE IF NOT EXISTS revora_power_book_purchases(
uid bigint NOT NULL,life smallint NOT NULL,meteor_uid bigint NOT NULL,stack_before integer NOT NULL CHECK(stack_before BETWEEN 0 AND 65535),
bound boolean NOT NULL,book_uid bigint NOT NULL UNIQUE CHECK(book_uid>0),created_at timestamptz NOT NULL DEFAULT now(),
PRIMARY KEY(uid,life),FOREIGN KEY(uid,life) REFERENCES revora_power_book(uid,life));
ALTER TABLE revora_power_book ADD COLUMN IF NOT EXISTS count_a integer NOT NULL DEFAULT 0 CHECK(count_a BETWEEN 0 AND 20);
ALTER TABLE revora_power_book ADD COLUMN IF NOT EXISTS count_b integer NOT NULL DEFAULT 0 CHECK(count_b BETWEEN 0 AND 15);
ALTER TABLE revora_power_book ADD COLUMN IF NOT EXISTS kill_window timestamptz;
ALTER TABLE revora_power_book ADD COLUMN IF NOT EXISTS last_kill timestamptz;
CREATE TABLE IF NOT EXISTS revora_power_book_lives(
id uuid PRIMARY KEY,uid bigint NOT NULL,life smallint NOT NULL,stage smallint NOT NULL,map integer NOT NULL,body bigint NOT NULL,
kind integer NOT NULL CHECK(kind BETWEEN 0 AND 6),consumed boolean NOT NULL DEFAULT false,
FOREIGN KEY(uid,life) REFERENCES revora_power_book(uid,life));
ALTER TABLE revora_power_book ADD COLUMN IF NOT EXISTS material_uid bigint NOT NULL DEFAULT 0;
ALTER TABLE revora_power_book ADD COLUMN IF NOT EXISTS material_type bigint NOT NULL DEFAULT 0;
ALTER TABLE revora_power_book_lives ADD COLUMN IF NOT EXISTS dead_at timestamptz;
ALTER TABLE revora_power_book_lives ADD COLUMN IF NOT EXISTS dead_x integer;
ALTER TABLE revora_power_book_lives ADD COLUMN IF NOT EXISTS dead_y integer;
ALTER TABLE revora_power_book_lives ADD COLUMN IF NOT EXISTS burned boolean NOT NULL DEFAULT false;
CREATE TABLE IF NOT EXISTS revora_power_book_rewards(
uid bigint NOT NULL,life smallint NOT NULL,pack_uid bigint NOT NULL UNIQUE,soul_uid bigint NOT NULL UNIQUE,
soul_type bigint NOT NULL,PRIMARY KEY(uid,life),FOREIGN KEY(uid,life) REFERENCES revora_power_book(uid,life));
CREATE TABLE IF NOT EXISTS revora_power_book_recoveries(
uid bigint NOT NULL,life smallint NOT NULL,old_uid bigint NOT NULL,new_uid bigint NOT NULL,type bigint NOT NULL,
recovered_at timestamptz NOT NULL DEFAULT now(),PRIMARY KEY(uid,life,old_uid),
FOREIGN KEY(uid,life) REFERENCES revora_power_book(uid,life));");}
        }
        internal static bool Eligible(GameState p){return Q.Player(p) && ReferenceEquals(p.Entity.Owner,p) && CharacterProgression.Managed(p)
            && p.Entity.Level>=115 && p.Entity.Reborn<=2 && p.Trade!=null && !p.Trade.InTrade && p.Booth==null && p.Entity.PokerTable==0;}
        private static void Ready(GameState p,Func<bool> context)
        {
            if(!Eligible(p) || !Monitor.IsEntered(p.ItemSyncRoot) || context==null || !context())
                throw new InvalidOperationException("Power Book owner or world changed.");
        }
        private static Run Read(NpgsqlConnection c,NpgsqlTransaction t,GameState p)
        {
            using(var q=Q.Command(c,t,"SELECT stage,badge_uid,book_uid,soul_type,count_a,count_b,kill_window,last_kill,material_uid,material_type FROM revora_power_book WHERE uid=@uid AND life=@life FOR UPDATE","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn))
            using(var r=q.ExecuteReader())return r.Read()?new Run{Stage=r.GetInt16(0),BadgeUid=checked((uint)r.GetInt64(1)),BookUid=checked((uint)r.GetInt64(2)),Soul=checked((uint)r.GetInt64(3)),CountA=r.GetInt32(4),CountB=r.GetInt32(5),Window=r.IsDBNull(6)?(DateTime?)null:r.GetFieldValue<DateTimeOffset>(6).UtcDateTime,LastKill=r.IsDBNull(7)?(DateTime?)null:r.GetFieldValue<DateTimeOffset>(7).UtcDateTime,MaterialUid=checked((uint)r.GetInt64(8)),MaterialType=checked((uint)r.GetInt64(9))}:new Run();
        }
        internal static Run Load(GameState p)
        {
            if(!Q.Player(p,false))return null;
            lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();return Read(c,null,p);}
        }
        private static void Owner(NpgsqlConnection c,NpgsqlTransaction t,GameState p)
        {
            SkillTable.LockOwner(c,t,p);
            using(var q=Q.Command(c,t,"SELECT reborn,level FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())if(!r.Read() || Convert.ToInt32(r[0])!=p.Entity.Reborn || Convert.ToInt32(r[1])!=p.Entity.Level)
                throw new InvalidOperationException("Power Book saved character changed.");
            var saved=new List<uint>();
            using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid FOR UPDATE","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())while(r.Read())saved.Add(checked((uint)r.GetInt64(0)));
            if(!saved.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Power Book inventory requires reload.");
        }
        internal static bool Begin(GameState p,Func<bool> context)
        {
            Ready(p,context);ConquerItem badge;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);if(Read(c,t,p).Stage!=0 || p.Inventory.Count>=40)return false;
                badge=Q.New(Badge,true);uint soul=QuestSoulReward.Create().ID;
                if(Q.Execute(c,t,"INSERT INTO revora_power_book(uid,life,stage,badge_uid,soul_type) VALUES(@uid,@life,1,@badge,@soul)",
                    "uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"badge",(long)badge.UID,"soul",(long)soul)!=1)
                    throw new InvalidOperationException("Power Book acceptance was not saved.");
                Q.Insert(c,t,p.Entity.UID,badge);Ready(p,context);t.Commit();
            }}
            Q.Install(p,new uint[0],new[]{badge});return true;
        }
        private static bool Missing(NpgsqlConnection c,NpgsqlTransaction t,uint uid)
        {
            if(uid==0)return false;
            // Any surviving row, regardless of owner or storage, prevents a copy.
            using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE uid=@uid FOR UPDATE","uid",(long)uid))return q.ExecuteScalar()==null;
        }
        internal static string Recover(GameState p,Func<bool> context)
        {
            Ready(p,context);var added=new List<ConquerItem>();bool material=false;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);
                if(run.Stage<1 || run.Stage>=14)return "There is nothing to replace in this life.";
                uint oldBadge=run.BadgeUid,oldBook=run.BookUid,oldMaterial=run.MaterialUid;
                bool badge=Missing(c,t,oldBadge),book=Missing(c,t,oldBook);
                material=Missing(c,t,oldMaterial);
                if(!badge && !book && !material)return "Your belongings still exist. Check your bag and storage.";
                if(p.Inventory.Count+(badge?1:0)+(book?1:0)>40)return "Make room for your badge and book first.";
                if(badge){var item=Q.New(Badge,true);added.Add(item);run.BadgeUid=item.UID;}
                if(book){var item=Q.New(Book,true);added.Add(item);run.BookUid=item.UID;}
                foreach(var item in added){
                    Q.Insert(c,t,p.Entity.UID,item);
                    if(Q.Execute(c,t,"INSERT INTO revora_power_book_recoveries(uid,life,old_uid,new_uid,type) VALUES(@uid,@life,@old,@new,@type)",
                        "uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"old",(long)(item.ID==Badge?oldBadge:oldBook),"new",(long)item.UID,"type",(long)item.ID)!=1)
                        throw new InvalidOperationException("Power Book replacement receipt failed.");
                }
                if(material){
                    if(Q.Execute(c,t,"INSERT INTO revora_power_book_recoveries(uid,life,old_uid,new_uid,type) VALUES(@uid,@life,@old,0,@type)",
                        "uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"old",(long)oldMaterial,"type",(long)run.MaterialType)!=1)
                        throw new InvalidOperationException("Power Book lost material receipt failed.");
                    run.MaterialUid=run.MaterialType=0;
                }
                if(Q.Execute(c,t,@"UPDATE revora_power_book SET badge_uid=@badge,book_uid=@book,material_uid=@material,material_type=@type
WHERE uid=@uid AND life=@life AND stage=@stage AND badge_uid=@oldbadge AND book_uid=@oldbook AND material_uid=@oldmaterial",
                    "badge",(long)run.BadgeUid,"book",(long)run.BookUid,"material",(long)run.MaterialUid,"type",(long)run.MaterialType,
                    "uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"stage",run.Stage,"oldbadge",(long)oldBadge,"oldbook",(long)oldBook,"oldmaterial",(long)oldMaterial)!=1)
                    throw new InvalidOperationException("Power Book recovery progress changed.");
                Ready(p,context);t.Commit();
            }}
            Q.Install(p,new uint[0],added);
            return material?"Your trial is safe. Hunt for the lost material again.":"Here are your lost belongings. Your trial continues.";
        }
        private static bool Usable(ConquerItem item){return item!=null && item.Position==0 && item.Warehouse==0 && item.Lock==0
            && !item.Suspicious && !item.InWardrobe && item.Days==0 && item.TimeLeftInMinutes==0;}
        internal static bool BuyBook(GameState p,uint meteorUid,Func<bool> context)
        {
            Ready(p,context);ConquerItem meteor,book;ushort before;ulong silver;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=1)return false;
                ConquerItem badge;
                if(!p.Inventory.TryGetItem(run.BadgeUid,out badge) || !Usable(badge) || !Q.Owns(c,t,p,run.BadgeUid,Badge))return false;
                if(!p.Inventory.TryGetItem(meteorUid,out meteor) || meteor.ID!=Meteor || !Usable(meteor))return false;
                before=meteor.StackSize;if(p.Inventory.Count-(before<=1?1:0)>=40)return false;
                if(p.Entity.Money>Entity.MaximumSilver-100)return false;
                SilverBalance.AssertSaved(c,t,p);silver=p.Entity.Money+100;
                string debit=before<=1?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
                if(Q.Execute(c,t,debit+@" WHERE uid=@item AND entityid=@uid AND id=1088001 AND position=0 AND warehouse=0
AND stacksize=@before AND bound=@bound AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0
AND COALESCE(NULLIF(days,''),'0')='0'", "item",(long)meteorUid,"uid",(long)p.Entity.UID,"before",(int)before,"bound",meteor.Bound?1:0)!=1)
                    throw new InvalidOperationException("Power Book Meteor ownership changed.");
                book=Q.New(Book,true);Q.Insert(c,t,p.Entity.UID,book);SilverBalance.Persist(c,t,p,silver);
                if(Q.Execute(c,t,"UPDATE revora_power_book SET stage=2,book_uid=@book WHERE uid=@uid AND life=@life AND stage=1",
                    "book",(long)book.UID,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn)!=1)
                    throw new InvalidOperationException("Power Book purchase stage changed.");
                if(Q.Execute(c,t,@"INSERT INTO revora_power_book_purchases(uid,life,meteor_uid,stack_before,bound,book_uid)
VALUES(@uid,@life,@meteor,@before,@bound,@book)","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"meteor",(long)meteorUid,
                    "before",(int)before,"bound",meteor.Bound,"book",(long)book.UID)!=1)throw new InvalidOperationException("Power Book payment receipt missing.");
                Ready(p,context);t.Commit();
            }}
            Q.Install(p,before<=1?new[]{meteorUid}:new uint[0],new[]{book});
            if(before>1){meteor.StackSize=(ushort)(before-1);meteor.Mode=Enums.ItemMode.Update;meteor.Send(p);meteor.Mode=Enums.ItemMode.Default;}
            SilverBalance.ApplyCommitted(p,silver);return true;
        }
    }
}
