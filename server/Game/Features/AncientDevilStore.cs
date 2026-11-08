using System;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using R=Revora.Game.Features.AncientDevilRules;

namespace Revora.Game.Features
{
    // Staged transaction participants. The future world service owns NPC offers,
    // actual encounter registration/death verification and post-commit delivery.
    internal static class AncientDevilStore
    {
        internal sealed class Run {
            internal Guid Id; internal uint Owner,Guard,Family,Certificate,Amulet,CertificateUid;
            internal byte Profession; internal string State;
        }
        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Q.Execute(c,t,@"CREATE TABLE IF NOT EXISTS revora_ancient_trials(
id uuid PRIMARY KEY,uid bigint NOT NULL REFERENCES entities(uid),profession integer NOT NULL CHECK(profession BETWEEN 1 AND 255),
guard integer NOT NULL CHECK(guard BETWEEN 300011 AND 300015),family integer NOT NULL CHECK(family BETWEEN 7001 AND 7005),
certificate integer NOT NULL,amulet integer NOT NULL,state varchar(12) NOT NULL CHECK(state IN('hunting','victory','certificate','exchanged','abandoned')),
certificate_uid bigint UNIQUE,amulet_uid bigint UNIQUE,created_at timestamptz NOT NULL DEFAULT now(),
CHECK((certificate_uid IS NOT NULL)=(state IN('certificate','exchanged'))),CHECK((amulet_uid IS NOT NULL)=(state='exchanged')));
CREATE UNIQUE INDEX IF NOT EXISTS revora_ancient_active_owner ON revora_ancient_trials(uid) WHERE state IN('hunting','victory','certificate');
CREATE TABLE IF NOT EXISTS revora_ancient_lives(
id uuid PRIMARY KEY,trial uuid NOT NULL UNIQUE REFERENCES revora_ancient_trials(id),map integer NOT NULL CHECK(map BETWEEN 11000 AND 59999),
monster_uid bigint NOT NULL CHECK(monster_uid BETWEEN 1 AND 4294967295),defeated boolean NOT NULL DEFAULT false);");t.Commit();}}
        }
        internal static void Owner(NpgsqlConnection c,NpgsqlTransaction t,GameState p) {
            if(t==null || !Q.Player(p) || !ReferenceEquals(p.Entity.Owner,p) || !Monitor.IsEntered(p.ItemSyncRoot) || p.Trade==null || p.Trade.InTrade
                || p.Booth!=null || p.Entity.PokerTable!=0 || p.Map==null || p.Map.ID!=p.Entity.MapID)
                throw new InvalidOperationException("Ancient trial owner is not ready.");
            SkillTable.LockOwner(c,t,p);
            using(var q=Q.Command(c,t,"SELECT class,level,mapid FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())if(!r.Read() || Convert.ToInt32(r[0])!=p.Entity.Class || Convert.ToInt32(r[1])!=p.Entity.Level
                || Convert.ToInt32(r[2])!=p.Entity.MapID)throw new InvalidOperationException("Ancient trial owner requires reload.");
        }
        internal static Run Read(NpgsqlConnection c,NpgsqlTransaction t,uint uid,Guid id) {
            using(var q=Q.Command(c,t,@"SELECT id,profession,guard,family,certificate,amulet,state,COALESCE(certificate_uid,0)
FROM revora_ancient_trials WHERE uid=@uid AND id=@id FOR UPDATE","uid",(long)uid,"id",id))
            using(var r=q.ExecuteReader())return !r.Read()?null:new Run{Id=r.GetGuid(0),Owner=uid,Profession=checked((byte)r.GetInt32(1)),
                Guard=checked((uint)r.GetInt32(2)),Family=checked((uint)r.GetInt32(3)),Certificate=checked((uint)r.GetInt32(4)),
                Amulet=checked((uint)r.GetInt32(5)),State=r.GetString(6),CertificateUid=checked((uint)r.GetInt64(7))};
        }
        private static Run Require(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Guid id) {
            Owner(c,t,p);var run=Read(c,t,p.Entity.UID,id);R.Trial rule;
            if(run==null || !R.TryTrial(p.Entity.Class,p.Entity.Level,run.Guard,out rule)
                || rule.Certificate!=run.Certificate || rule.Amulet!=run.Amulet || rule.Family!=run.Family)
                throw new InvalidOperationException("Ancient trial does not belong to this profession.");
            return run;
        }
        private static void Site(GameState p,uint guard) {
            if(!Q.Near(p,guard,1052) || p.Map.BaseID!=1082 || p.Map.Npcs[guard].MapID!=1052)
                throw new InvalidOperationException("Ancient trial guard is not nearby.");
        }
        internal static Run Begin(NpgsqlConnection c,NpgsqlTransaction t,GameState p,uint guard) {
            Owner(c,t,p);Site(p,guard);R.Trial rule;
            if(!R.TryTrial(p.Entity.Class,p.Entity.Level,guard,out rule))throw new InvalidOperationException("Ancient trial is unavailable.");
            Guid previous=Guid.Empty;
            using(var q=Q.Command(c,t,"SELECT id FROM revora_ancient_trials WHERE uid=@uid AND state IN('hunting','victory','certificate')","uid",(long)p.Entity.UID)) {
                var value=q.ExecuteScalar();if(value!=null)previous=(Guid)value;
            }
            if(previous!=Guid.Empty){var old=Require(c,t,p,previous);if(old.Guard!=guard)throw new InvalidOperationException("Another guard's trial is outstanding.");return old;}
            var id=Guid.NewGuid();
            if(Q.Execute(c,t,@"INSERT INTO revora_ancient_trials(id,uid,profession,guard,family,certificate,amulet,state)
VALUES(@id,@uid,@class,@guard,@family,@cert,@amulet,'hunting')","id",id,"uid",(long)p.Entity.UID,"class",(int)p.Entity.Class,
                "guard",(int)guard,"family",(int)rule.Family,"cert",(int)rule.Certificate,"amulet",(int)rule.Amulet)!=1)
                throw new InvalidOperationException("Ancient trial was not saved.");
            return Read(c,t,p.Entity.UID,id);
        }
        internal static Guid Bind(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Guid id,ushort map,uint monster) {
            var run=Require(c,t,p,id);Site(p,run.Guard);
            if(run.State!="hunting" || map<11000 || map>59999 || monster==0)throw new InvalidOperationException("Ancient encounter is unavailable.");
            using(var q=Q.Command(c,t,"SELECT id,map,monster_uid FROM revora_ancient_lives WHERE trial=@id","id",id))
            using(var r=q.ExecuteReader())if(r.Read()) {
                if(r.GetInt32(1)!=map || r.GetInt64(2)!=monster)throw new InvalidOperationException("Ancient encounter identity changed.");
                return r.GetGuid(0);
            }
            Guid life=Guid.NewGuid();
            if(Q.Execute(c,t,"INSERT INTO revora_ancient_lives(id,trial,map,monster_uid) VALUES(@life,@trial,@map,@monster)",
                "life",life,"trial",id,"map",(int)map,"monster",(long)monster)!=1)throw new InvalidOperationException("Ancient encounter was not saved.");
            return life;
        }
        // Internal persistence boundary only. The world caller must verify the
        // canonical dying monster and its owned run before invoking this method.
        internal static bool Defeat(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Guid id,Guid life,uint family,uint monster) {
            var run=Require(c,t,p,id);
            if(run.State!="hunting" || run.Family!=family || life==Guid.Empty || monster==0)return false;
            int changed=Q.Execute(c,t,@"UPDATE revora_ancient_lives SET defeated=true
WHERE id=@life AND trial=@trial AND map=@map AND monster_uid=@monster AND NOT defeated",
                "life",life,"trial",id,"map",(int)p.Entity.MapID,"monster",(long)monster);
            if(changed==0)return false;
            if(changed!=1 || Q.Execute(c,t,"UPDATE revora_ancient_trials SET state='victory' WHERE id=@id AND state='hunting'","id",id)!=1)
                throw new InvalidOperationException("Ancient trial victory was not saved.");
            return true;
        }
        internal static bool Abandon(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Guid id) {
            Owner(c,t,p);var run=Read(c,t,p.Entity.UID,id);
            if(run==null || run.State!="hunting")return false;
            if(Q.Execute(c,t,"UPDATE revora_ancient_trials SET state='abandoned' WHERE id=@id AND state='hunting'","id",id)!=1)
                throw new InvalidOperationException("Ancient trial abandonment was not saved.");
            return true;
        }
        internal static ConquerItem Claim(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Guid id) {
            var run=Require(c,t,p,id);Site(p,run.Guard);
            if(run.State!="victory")return null;
            long saved;using(var q=Q.Command(c,t,"SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0","uid",(long)p.Entity.UID))saved=(long)q.ExecuteScalar();
            if(saved!=p.Inventory.Count)throw new InvalidOperationException("Ancient reward inventory requires reload.");
            if(saved>=40)return null;
            var item=Q.New(run.Certificate);Q.Insert(c,t,p.Entity.UID,item);
            if(Q.Execute(c,t,"UPDATE revora_ancient_trials SET state='certificate',certificate_uid=@item WHERE id=@id AND state='victory'",
                "item",(long)item.UID,"id",id)!=1)throw new InvalidOperationException("Ancient certificate was not saved.");
            return item;
        }
        internal static ConquerItem Exchange(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Guid id,uint certificate) {
            var run=Require(c,t,p,id);Site(p,run.Guard);
            if(run.State!="certificate" || run.CertificateUid!=certificate)return null;
            ConquerItem item;
            if(!p.Inventory.TryGetItem(certificate,out item) || item.ID!=run.Certificate || !item.Bound || item.Position!=0
                || item.Warehouse!=0 || item.StackSize!=1 || item.Lock!=0 || item.Suspicious || item.InWardrobe || item.Days!=0 || item.TimeLeftInMinutes!=0)return null;
            using(var q=Q.Command(c,t,@"SELECT uid FROM items WHERE uid=@item AND entityid=@uid AND id=@type AND position=0 AND warehouse=0
AND bound=1 AND stacksize=1 AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0
AND COALESCE(NULLIF(days,''),'0')='0' FOR UPDATE","item",(long)certificate,"uid",(long)p.Entity.UID,"type",(int)run.Certificate))
                if(q.ExecuteScalar()==null)throw new InvalidOperationException("Ancient certificate requires reload.");
            // This specific earned certificate intentionally yields a transferable
            // class amulet. No arbitrary bound item may use this conversion.
            var amulet=Q.New(run.Amulet,false);Q.Consume(c,t,p,certificate,run.Certificate);Q.Insert(c,t,p.Entity.UID,amulet);
            if(Q.Execute(c,t,"UPDATE revora_ancient_trials SET state='exchanged',amulet_uid=@item WHERE id=@id AND state='certificate'",
                "item",(long)amulet.UID,"id",id)!=1)throw new InvalidOperationException("Ancient amulet was not saved.");
            AncientDevilCollection.Record(c,t,p);return amulet;
        }
    }
}
