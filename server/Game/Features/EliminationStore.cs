using System;
using System.Collections.Generic;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using R=Revora.Game.Features.EliminationRules;

namespace Revora.Game.Features
{
    // Staged service: caller commits, then installs under ItemSyncRoot; lifecycle release gate remains closed.
    internal static partial class EliminationStore
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal sealed class Change {
            internal uint[] Removed=new uint[0];internal ConquerItem Added;
            internal Guid RescueRun;internal DateTime RescueUntil;internal bool RescueWon;
            internal Guid DenRun;internal DateTime DenUntil;
        }
        private sealed class Run {internal Guid Id;internal int Stage;internal bool RuneReward;internal ushort Mask;internal uint Spice;}
        internal static void Initialize() {
            ConquerItemBaseInformation rune;
            if(!ConquerItemInformation.BaseInformations.TryGetValue(4030101,out rune) || rune.Class!=0)
                throw new InvalidOperationException("Elimination yellow rune catalog missing or class restricted.");
            foreach(uint item in new[]{R.BeautyHerb,R.FirePill,R.FancySpice})
                if(!ConquerItemInformation.BaseInformations.ContainsKey(item))throw new InvalidOperationException("Elimination catalog missing.");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Q.Execute(c,t,@"CREATE TABLE IF NOT EXISTS revora_elimination_runs(
 id uuid PRIMARY KEY,uid bigint NOT NULL REFERENCES entities(uid),day varchar(8) NOT NULL,
 stage integer NOT NULL DEFAULT 0 CHECK(stage BETWEEN 0 AND 6),herbs integer NOT NULL DEFAULT 0 CHECK(herbs BETWEEN 0 AND 4095),
 spice_uid bigint UNIQUE,started_at timestamptz NOT NULL,UNIQUE(uid,day));
CREATE TABLE IF NOT EXISTS revora_elimination_herbs(
 run uuid NOT NULL REFERENCES revora_elimination_runs(id),npc bigint NOT NULL CHECK(npc BETWEEN 300021 AND 300032),
 item_uid bigint NOT NULL UNIQUE CHECK(item_uid BETWEEN 1 AND 4294967295),gathered_at timestamptz NOT NULL,
 PRIMARY KEY(run,npc));
CREATE TABLE IF NOT EXISTS revora_elimination_replacements(
 run uuid NOT NULL REFERENCES revora_elimination_runs(id),old_uid bigint PRIMARY KEY CHECK(old_uid BETWEEN 1 AND 4294967295),
 new_uid bigint NOT NULL UNIQUE CHECK(new_uid BETWEEN 1 AND 4294967295),
 item_type integer NOT NULL CHECK(item_type IN(711042,723019)),replaced_at timestamptz NOT NULL,
 CHECK(old_uid<>new_uid));
CREATE TABLE IF NOT EXISTS revora_elimination_cleanup(
 item_uid bigint PRIMARY KEY CHECK(item_uid BETWEEN 1 AND 4294967295),uid bigint NOT NULL REFERENCES entities(uid),
 item_type integer NOT NULL CHECK(item_type IN(711042,723019)),cleaned_at timestamptz NOT NULL);
CREATE TABLE IF NOT EXISTS revora_elimination_rescues(
 run uuid PRIMARY KEY REFERENCES revora_elimination_runs(id),spice_uid bigint NOT NULL UNIQUE CHECK(spice_uid BETWEEN 1 AND 4294967295),
 state varchar(12) NOT NULL DEFAULT 'pending' CHECK(state IN('pending','rescued')),
 started_at timestamptz NOT NULL,until_at timestamptz NOT NULL CHECK(until_at>started_at));
CREATE TABLE IF NOT EXISTS revora_elimination_pills(
 run uuid NOT NULL REFERENCES revora_elimination_runs(id),slot integer NOT NULL CHECK(slot BETWEEN 0 AND 9),
 life uuid NOT NULL UNIQUE,monster_uid bigint NOT NULL CHECK(monster_uid BETWEEN 1 AND 4294967295),
 item_uid bigint NOT NULL UNIQUE CHECK(item_uid BETWEEN 1 AND 4294967295),earned_at timestamptz NOT NULL,PRIMARY KEY(run,slot));
CREATE TABLE IF NOT EXISTS revora_elimination_cures(
 run uuid PRIMARY KEY REFERENCES revora_elimination_runs(id),items bigint[] NOT NULL CHECK(cardinality(items)=10),cured_at timestamptz NOT NULL);
CREATE TABLE IF NOT EXISTS revora_elimination_dens(
 run uuid PRIMARY KEY REFERENCES revora_elimination_cures(run),entered_at timestamptz NOT NULL,
 until_at timestamptz NOT NULL CHECK(until_at>entered_at));
CREATE TABLE IF NOT EXISTS revora_elimination_claims(
 run uuid PRIMARY KEY REFERENCES revora_elimination_dens(run),experience bigint NOT NULL CHECK(experience>=0),
 study integer NOT NULL CHECK(study=50),claimed_at timestamptz NOT NULL);
ALTER TABLE revora_elimination_runs ADD COLUMN IF NOT EXISTS valuable_reward boolean NOT NULL DEFAULT false;
ALTER TABLE revora_elimination_claims ADD COLUMN IF NOT EXISTS soul_uid bigint UNIQUE;
ALTER TABLE revora_elimination_runs ADD COLUMN IF NOT EXISTS rune_reward boolean NOT NULL DEFAULT false;
ALTER TABLE revora_elimination_claims ADD COLUMN IF NOT EXISTS rune_uid bigint UNIQUE CHECK(rune_uid>0);
ALTER TABLE revora_elimination_replacements DROP CONSTRAINT IF EXISTS revora_elimination_replacements_item_type_check;
ALTER TABLE revora_elimination_replacements ADD CONSTRAINT revora_elimination_replacements_item_type_check CHECK(item_type IN(711041,711042,723019));
ALTER TABLE revora_elimination_cleanup DROP CONSTRAINT IF EXISTS revora_elimination_cleanup_item_type_check;
ALTER TABLE revora_elimination_cleanup ADD CONSTRAINT revora_elimination_cleanup_item_type_check CHECK(item_type IN(711041,711042,723019));");t.Commit();}}
        }
        private static void Owner(NpgsqlConnection c,NpgsqlTransaction t,GameState p) {
            if(t==null || t.Connection!=c || !Q.Player(p) || !ReferenceEquals(p.Entity.Owner,p)
                || !Monitor.IsEntered(p.ItemSyncRoot) || p.Trade==null || p.Trade.InTrade || p.Booth!=null || p.Entity.PokerTable!=0
                || p.Map==null || p.Map.ID!=R.Forest || p.Map.BaseID!=R.Forest || p.Entity.MapID!=R.Forest)
                throw new InvalidOperationException("Elimination requires its live owner in Maple Forest.");
            SkillTable.LockOwner(c,t,p);
            using(var q=Q.Command(c,t,"SELECT level,reborn,mapid FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())if(!r.Read() || Convert.ToInt32(r[0])!=p.Entity.Level || Convert.ToInt32(r[1])!=p.Entity.Reborn
                || Convert.ToInt32(r[2])!=R.Forest)throw new InvalidOperationException("Elimination owner requires reload.");
        }
        private static bool Site(GameState p,uint npc,ushort x,ushort y,ushort mesh) {
            Revora.Interfaces.INpc actor;
            return p.ActiveNpc==npc && p.Map.Npcs.TryGetValue(npc,out actor) && actor.MapID==R.Forest
                && actor.X==x && actor.Y==y && actor.Mesh==mesh && Kernel.GetDistance(p.Entity.X,p.Entity.Y,x,y)<=6;
        }
        private static Run Read(NpgsqlConnection c,NpgsqlTransaction t,GameState p,string day) {
            using(var q=Q.Command(c,t,"SELECT id,stage,herbs,spice_uid,rune_reward FROM revora_elimination_runs WHERE uid=@uid AND day=@day FOR UPDATE",
                "uid",(long)p.Entity.UID,"day",day))using(var r=q.ExecuteReader())
                return r.Read()?new Run{Id=r.GetGuid(0),Stage=r.GetInt32(1),RuneReward=r.GetBoolean(4),Mask=checked((ushort)r.GetInt32(2)),Spice=r.IsDBNull(3)?0:checked((uint)r.GetInt64(3))}:null;
        }
        private static void Bag(NpgsqlConnection c,NpgsqlTransaction t,GameState p,bool add) {
            using(var q=Q.Command(c,t,"SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0","uid",(long)p.Entity.UID)){
                long count=(long)q.ExecuteScalar();if(count!=p.Inventory.Count || add && count>=40)throw new InvalidOperationException("Elimination inventory unavailable.");
            }
        }
        private static void SameDay(string day) {
            if(SummerHunt.Day(Clock())!=day)throw new InvalidOperationException("Elimination day changed during transaction.");
        }
        internal static bool Begin(NpgsqlConnection c,NpgsqlTransaction t,GameState p) {
            Owner(c,t,p);var now=Clock();string day=SummerHunt.Day(now);
            if(!R.Eligible(p.Entity.Level,p.Entity.Reborn) || !Site(p,R.Georgia,R.GeorgiaX,R.GeorgiaY,4680) || Read(c,t,p,day)!=null)return false;
            if(Q.Execute(c,t,"INSERT INTO revora_elimination_runs(id,uid,day,started_at,valuable_reward,rune_reward) VALUES(@id,@uid,@day,@now,false,true)",
                "id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"day",day,"now",now)!=1)throw new InvalidOperationException("Elimination admission was not saved.");
            SameDay(day);return true;
        }
        internal static Change Gather(NpgsqlConnection c,NpgsqlTransaction t,GameState p,uint npc) {
            Owner(c,t,p);string day=SummerHunt.Day(Clock());var run=Read(c,t,p,day);var site=R.Herb(npc);ushort next;
            if(run==null || run.Stage!=0 || !R.Eligible(p.Entity.Level,p.Entity.Reborn) || site==null
                || !Site(p,npc,site.X,site.Y,4700) || !R.TryGather(npc,R.Forest,p.Entity.X,p.Entity.Y,run.Mask,out next))return null;
            Bag(c,t,p,true);var change=new Change{Added=Q.New(R.BeautyHerb)};
            Q.Insert(c,t,p.Entity.UID,change.Added);
            if(Q.Execute(c,t,"INSERT INTO revora_elimination_herbs(run,npc,item_uid,gathered_at) VALUES(@run,@npc,@item,@now)",
                "run",run.Id,"npc",(long)npc,"item",(long)change.Added.UID,"now",Clock())!=1)
                throw new InvalidOperationException("Elimination herb receipt was not saved.");
            if(Q.Execute(c,t,"UPDATE revora_elimination_runs SET herbs=@next WHERE id=@run AND herbs=@before AND stage=0",
                "next",(int)next,"run",run.Id,"before",(int)run.Mask)!=1)throw new InvalidOperationException("Elimination herb progress was not saved.");
            SameDay(day);return change;
        }
        internal static Change Spice(NpgsqlConnection c,NpgsqlTransaction t,GameState p) {
            Owner(c,t,p);string day=SummerHunt.Day(Clock());var run=Read(c,t,p,day);
            if(run==null || run.Stage!=0 || run.Mask!=4095 || !R.Eligible(p.Entity.Level,p.Entity.Reborn)
                || !Site(p,R.Georgia,R.GeorgiaX,R.GeorgiaY,4680))return null;
            var herbs=new List<uint>();
            using(var q=Q.Command(c,t,"SELECT item_uid FROM revora_elimination_herbs WHERE run=@run ORDER BY npc FOR UPDATE","run",run.Id))
            using(var r=q.ExecuteReader())while(r.Read())herbs.Add(checked((uint)r.GetInt64(0)));
            if(herbs.Count!=R.HerbsRequired)throw new InvalidOperationException("Elimination herb receipts incomplete.");
            Bag(c,t,p,false);
            foreach(uint uid in herbs) {
                ConquerItem item;
                if(!p.Inventory.TryGetItem(uid,out item) || item.ID!=R.BeautyHerb || !item.Bound || item.Position!=0 || item.Warehouse!=0
                    || item.StackSize!=1 || item.Lock!=0 || item.Suspicious || item.InWardrobe || item.Days!=0 || item.TimeLeftInMinutes!=0)return null;
                using(var q=Q.Command(c,t,@"SELECT uid FROM items WHERE uid=@item AND entityid=@uid AND id=@type AND position=0 AND warehouse=0
AND bound=1 AND stacksize=1 AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0' FOR UPDATE",
                    "item",(long)uid,"uid",(long)p.Entity.UID,"type",(int)R.BeautyHerb))
                    if(q.ExecuteScalar()==null)throw new InvalidOperationException("Elimination herb ownership changed.");
            }
            var change=new Change{Removed=herbs.ToArray(),Added=Q.New(R.FancySpice)};
            foreach(uint uid in herbs)Q.Consume(c,t,p,uid,R.BeautyHerb);
            Q.Insert(c,t,p.Entity.UID,change.Added);
            if(Q.Execute(c,t,"UPDATE revora_elimination_runs SET stage=1,spice_uid=@item WHERE id=@run AND stage=0 AND herbs=4095",
                "item",(long)change.Added.UID,"run",run.Id)!=1)throw new InvalidOperationException("Elimination spice was not saved.");
            SameDay(day);return change;
        }
        internal static Change Recover(NpgsqlConnection c,NpgsqlTransaction t,GameState p,uint missing) {
            Owner(c,t,p);string day=SummerHunt.Day(Clock());var run=Read(c,t,p,day);
            if(run==null || (run.Stage>1 && run.Stage!=4) || !R.Eligible(p.Entity.Level,p.Entity.Reborn)
                || !Site(p,R.Georgia,R.GeorgiaX,R.GeorgiaY,4680) || missing==0)return null;
            uint type=run.Stage==0?R.BeautyHerb:run.Stage==4?R.FirePill:R.FancySpice;
            // Only the current receipt is recoverable. An old request cannot
            // replace a replacement, nor resurrect herbs consumed into spice.
            string proof=run.Stage==0
                ? "SELECT EXISTS(SELECT 1 FROM revora_elimination_herbs WHERE run=@run AND item_uid=@item)"
                : run.Stage==4?"SELECT EXISTS(SELECT 1 FROM revora_elimination_pills WHERE run=@run AND item_uid=@item)"
                : "SELECT EXISTS(SELECT 1 FROM revora_elimination_runs WHERE id=@run AND spice_uid=@item AND stage=1)";
            using(var q=Q.Command(c,t,proof,"run",run.Id,"item",(long)missing))if(!Equals(q.ExecuteScalar(),true))return null;
            // These materials are bound: normal discard destroys them rather
            // than putting them on the floor. Saved storage/other ownership is
            // not loss, regardless of what the player's current bag contains.
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM items WHERE uid=@item)","item",(long)missing))
                if(Equals(q.ExecuteScalar(),true))return null;
            if(p.Inventory.ContainsUID(missing))throw new InvalidOperationException("Elimination inventory requires reload before recovery.");
            Bag(c,t,p,true);var change=new Change{Added=Q.New(type)};Q.Insert(c,t,p.Entity.UID,change.Added);
            string update=run.Stage==0
                ? "UPDATE revora_elimination_herbs SET item_uid=@next WHERE run=@run AND item_uid=@before"
                : run.Stage==4?"UPDATE revora_elimination_pills SET item_uid=@next WHERE run=@run AND item_uid=@before"
                : "UPDATE revora_elimination_runs SET spice_uid=@next WHERE id=@run AND spice_uid=@before AND stage=1";
            if(Q.Execute(c,t,update,"next",(long)change.Added.UID,"run",run.Id,"before",(long)missing)!=1)
                throw new InvalidOperationException("Elimination replacement ownership was not saved.");
            if(Q.Execute(c,t,@"INSERT INTO revora_elimination_replacements(run,old_uid,new_uid,item_type,replaced_at)
VALUES(@run,@old,@next,@type,@now)","run",run.Id,"old",(long)missing,"next",(long)change.Added.UID,"type",(int)type,"now",Clock())!=1)
                throw new InvalidOperationException("Elimination replacement receipt was not saved.");
            SameDay(day);return change;
        }
        internal static Change Cleanup(NpgsqlConnection c,NpgsqlTransaction t,GameState p) {
            Owner(c,t,p);string day=SummerHunt.Day(Clock());
            if(!Site(p,R.Georgia,R.GeorgiaX,R.GeorgiaY,4680))return null;
            Bag(c,t,p,false);var expired=new List<KeyValuePair<uint,uint>>();
            // Proof is by exact run/item ownership, never by item type alone.
            // Warehouse items are deferred until returned to the owner's bag.
            using(var q=Q.Command(c,t,@"WITH proof AS (
 SELECT h.item_uid,711042 AS item_type FROM revora_elimination_herbs h
 JOIN revora_elimination_runs r ON r.id=h.run WHERE r.uid=@uid AND r.day<@day
 UNION ALL SELECT spice_uid,723019 FROM revora_elimination_runs WHERE uid=@uid AND day<@day AND spice_uid IS NOT NULL
 UNION ALL SELECT p.item_uid,711041 FROM revora_elimination_pills p
 JOIN revora_elimination_runs r ON r.id=p.run WHERE r.uid=@uid AND r.day<@day)
SELECT i.uid,i.id FROM items i JOIN proof p ON p.item_uid=i.uid AND p.item_type=i.id
WHERE i.entityid=@uid AND i.position=0 AND i.warehouse=0 AND i.bound=1 AND i.stacksize=1
AND i.locked=0 AND COALESCE(i.suspicious,0)=0 AND COALESCE(i.inwardrobe,0)=0 AND COALESCE(NULLIF(i.days,''),'0')='0'
ORDER BY i.uid FOR UPDATE OF i","uid",(long)p.Entity.UID,"day",day))using(var r=q.ExecuteReader())
                while(r.Read())expired.Add(new KeyValuePair<uint,uint>(Convert.ToUInt32(r[0]),Convert.ToUInt32(r[1])));
            var removed=new List<uint>();
            foreach(var entry in expired) {
                ConquerItem item;
                if(!p.Inventory.TryGetItem(entry.Key,out item) || item.ID!=entry.Value || !item.Bound || item.Position!=0 || item.Warehouse!=0
                    || item.StackSize!=1 || item.Lock!=0 || item.Suspicious || item.InWardrobe || item.Days!=0 || item.TimeLeftInMinutes!=0)
                    throw new InvalidOperationException("Expired Elimination inventory requires reload.");
                Q.Consume(c,t,p,entry.Key,entry.Value);
                if(Q.Execute(c,t,"INSERT INTO revora_elimination_cleanup(item_uid,uid,item_type,cleaned_at) VALUES(@item,@uid,@type,@now)",
                    "item",(long)entry.Key,"uid",(long)p.Entity.UID,"type",(int)entry.Value,"now",Clock())!=1)
                    throw new InvalidOperationException("Elimination cleanup receipt was not saved.");
                removed.Add(entry.Key);
            }
            SameDay(day);return removed.Count==0?null:new Change{Removed=removed.ToArray()};
        }
        // Called only after the world coordinator has reserved an encounter.
        // No item-use registration exists while the encounter is unfinished.
        internal static Change Summon(NpgsqlConnection c,NpgsqlTransaction t,GameState p,uint spice) {
            Owner(c,t,p);var now=Clock();string day=SummerHunt.Day(now);var run=Read(c,t,p,day);
            if(run==null || run.Stage!=1 || spice==0 || run.Spice!=spice || !R.Eligible(p.Entity.Level,p.Entity.Reborn)
                || !R.AtSpiceSite(p.Entity.MapID,p.Entity.X,p.Entity.Y))return null;
            ConquerItem item;
            if(!p.Inventory.TryGetItem(spice,out item) || item.ID!=R.FancySpice || !item.Bound || item.Position!=0 || item.Warehouse!=0
                || item.StackSize!=1 || item.Lock!=0 || item.Suspicious || item.InWardrobe || item.Days!=0 || item.TimeLeftInMinutes!=0)return null;
            Bag(c,t,p,false);
            using(var q=Q.Command(c,t,@"SELECT uid FROM items WHERE uid=@item AND entityid=@uid AND id=@type AND position=0 AND warehouse=0
AND bound=1 AND stacksize=1 AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0' FOR UPDATE",
                "item",(long)spice,"uid",(long)p.Entity.UID,"type",(int)R.FancySpice))
                if(q.ExecuteScalar()==null)throw new InvalidOperationException("Elimination spice ownership changed.");
            var change=new Change{Removed=new[]{spice},RescueRun=run.Id,RescueUntil=SummerHunt.Midnight(now,1)};
            Q.Consume(c,t,p,spice,R.FancySpice);
            if(Q.Execute(c,t,"INSERT INTO revora_elimination_rescues(run,spice_uid,started_at,until_at) VALUES(@run,@item,@now,@until)",
                "run",run.Id,"item",(long)spice,"now",now,"until",change.RescueUntil)!=1)
                throw new InvalidOperationException("Elimination rescue admission was not saved.");
            if(Q.Execute(c,t,"UPDATE revora_elimination_runs SET stage=2 WHERE id=@run AND stage=1 AND spice_uid=@item",
                "run",run.Id,"item",(long)spice)!=1)throw new InvalidOperationException("Elimination rescue progress was not saved.");
            SameDay(day);return change;
        }
        internal static Change ResumeRescue(NpgsqlConnection c,NpgsqlTransaction t,GameState p) {
            Owner(c,t,p);var now=Clock();string day=SummerHunt.Day(now);var run=Read(c,t,p,day);
            if(run==null || run.Stage!=2 || !R.Eligible(p.Entity.Level,p.Entity.Reborn)
                || (!R.AtSpiceSite(p.Entity.MapID,p.Entity.X,p.Entity.Y) && !Site(p,R.Georgia,R.GeorgiaX,R.GeorgiaY,4680)))return null;
            Change change=null;
            using(var q=Q.Command(c,t,"SELECT until_at,state FROM revora_elimination_rescues WHERE run=@run AND spice_uid=@spice AND state IN('pending','rescued') AND until_at>@now FOR UPDATE",
                "run",run.Id,"spice",(long)run.Spice,"now",now))using(var r=q.ExecuteReader())
                if(r.Read())change=new Change{RescueRun=run.Id,RescueUntil=r.GetFieldValue<DateTimeOffset>(0).UtcDateTime,RescueWon=r.GetString(1)=="rescued"};
            SameDay(day);return change;
        }
        internal static void Apply(GameState p,Change change) {
            if(change!=null)Q.Install(p,change.Removed,change.Added==null?new ConquerItem[0]:new[]{change.Added});
        }
        // Staged reservation only: call after reserving the verified private den.
        // No NPC option or travel hook calls this until the world route exists.
        internal static Change AdmitDen(NpgsqlConnection c,NpgsqlTransaction t,GameState p) {
            Owner(c,t,p);var now=Clock();string day=SummerHunt.Day(now);var run=Read(c,t,p,day);
            if(run==null || run.Stage!=5 || !R.Eligible(p.Entity.Level,p.Entity.Reborn)
                || !Site(p,R.Georgia,R.GeorgiaX,R.GeorgiaY,4680))return null;
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_elimination_cures WHERE run=@run)","run",run.Id))
                if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Elimination cure proof missing.");
            DateTime until=SummerHunt.Midnight(now,1);bool exists=false;
            using(var q=Q.Command(c,t,"SELECT until_at FROM revora_elimination_dens WHERE run=@run FOR UPDATE","run",run.Id))
            using(var r=q.ExecuteReader())if(r.Read()){until=r.GetFieldValue<DateTimeOffset>(0).UtcDateTime;exists=true;}
            if(until<=now)return null;
            if(!exists && Q.Execute(c,t,"INSERT INTO revora_elimination_dens(run,entered_at,until_at) VALUES(@run,@now,@until)",
                "run",run.Id,"now",now,"until",until)!=1)throw new InvalidOperationException("Elimination den admission was not saved.");
            SameDay(day);if(Clock()>=until)throw new InvalidOperationException("Elimination den admission expired.");
            return new Change{DenRun=run.Id,DenUntil=until};
        }
        internal static bool Report(NpgsqlConnection c,NpgsqlTransaction t,GameState p) {
            Owner(c,t,p);string day=SummerHunt.Day(Clock());var run=Read(c,t,p,day);
            if(run==null || run.Stage!=3 || !R.Eligible(p.Entity.Level,p.Entity.Reborn) || !Site(p,R.Georgia,R.GeorgiaX,R.GeorgiaY,4680))return false;
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_elimination_landy WHERE run=@run)","run",run.Id))
                if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Elimination Landy report missing.");
            if(Q.Execute(c,t,"UPDATE revora_elimination_runs SET stage=4 WHERE id=@run AND stage=3","run",run.Id)!=1)
                throw new InvalidOperationException("Elimination cure request was not saved.");
            SameDay(day);return true;
        }
        internal static Change EarnPill(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Guid life,uint monster) {
            Owner(c,t,p);string day=SummerHunt.Day(Clock());var run=Read(c,t,p,day);
            if(run==null || run.Stage!=4 || life==Guid.Empty || monster==0 || !R.Eligible(p.Entity.Level,p.Entity.Reborn))return null;
            int count;using(var q=Q.Command(c,t,"SELECT count(*) FROM revora_elimination_pills WHERE run=@run","run",run.Id))count=Convert.ToInt32(q.ExecuteScalar());
            if(count>=R.PillsRequired)return null;
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_elimination_pills WHERE life=@life)","life",life))if(Equals(q.ExecuteScalar(),true))return null;
            Bag(c,t,p,true);var change=new Change{Added=Q.New(R.FirePill)};Q.Insert(c,t,p.Entity.UID,change.Added);
            if(Q.Execute(c,t,"INSERT INTO revora_elimination_pills(run,slot,life,monster_uid,item_uid,earned_at) VALUES(@run,@slot,@life,@monster,@item,@now)",
                "run",run.Id,"slot",count,"life",life,"monster",(long)monster,"item",(long)change.Added.UID,"now",Clock())!=1)
                throw new InvalidOperationException("Elimination Fire Pill evidence was not saved.");
            SameDay(day);return change;
        }
        internal static Change Cure(NpgsqlConnection c,NpgsqlTransaction t,GameState p) {
            Owner(c,t,p);string day=SummerHunt.Day(Clock());var run=Read(c,t,p,day);
            if(run==null || run.Stage!=4 || !R.Eligible(p.Entity.Level,p.Entity.Reborn) || !Site(p,R.Georgia,R.GeorgiaX,R.GeorgiaY,4680))return null;
            var pills=new List<uint>();
            using(var q=Q.Command(c,t,"SELECT item_uid FROM revora_elimination_pills WHERE run=@run ORDER BY slot FOR UPDATE","run",run.Id))
            using(var r=q.ExecuteReader())while(r.Read())pills.Add(checked((uint)r.GetInt64(0)));
            if(pills.Count!=R.PillsRequired)return null;Bag(c,t,p,false);
            foreach(uint uid in pills) {
                ConquerItem item;
                if(!p.Inventory.TryGetItem(uid,out item) || item.ID!=R.FirePill || !item.Bound || item.Position!=0 || item.Warehouse!=0
                    || item.StackSize!=1 || item.Lock!=0 || item.Suspicious || item.InWardrobe || item.Days!=0 || item.TimeLeftInMinutes!=0)return null;
                using(var q=Q.Command(c,t,@"SELECT uid FROM items WHERE uid=@item AND entityid=@uid AND id=@type AND position=0 AND warehouse=0
AND bound=1 AND stacksize=1 AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0' FOR UPDATE",
                    "item",(long)uid,"uid",(long)p.Entity.UID,"type",(int)R.FirePill))
                    if(q.ExecuteScalar()==null)throw new InvalidOperationException("Elimination Fire Pill ownership changed.");
            }
            foreach(uint uid in pills)Q.Consume(c,t,p,uid,R.FirePill);
            var saved=new long[pills.Count];for(int i=0;i<pills.Count;i++)saved[i]=pills[i];
            if(Q.Execute(c,t,"INSERT INTO revora_elimination_cures(run,items,cured_at) VALUES(@run,@items,@now)","run",run.Id,"items",saved,"now",Clock())!=1
                || Q.Execute(c,t,"UPDATE revora_elimination_runs SET stage=5 WHERE id=@run AND stage=4","run",run.Id)!=1)
                throw new InvalidOperationException("Elimination cure was not saved.");
            SameDay(day);return new Change{Removed=pills.ToArray()};
        }
    }
}
