using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Runtime.CompilerServices;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static partial class DesertExpeditionStore
    {
        internal sealed class Run {internal int Stage;internal uint Soul;internal DateTime? PillUntil;internal int PillPaid;}
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static bool Eligible(GameState p){return Q.Player(p) && ReferenceEquals(p.Entity.Owner,p) && CharacterProgression.Managed(p)
            && p.Entity.Level>=105 && p.Entity.Reborn<=2 && p.Trade!=null && !p.Trade.InTrade && p.Booth==null && p.Entity.PokerTable==0;}
        private static void Ready(GameState p,Func<bool> context){if(!Eligible(p) || !Monitor.IsEntered(p.ItemSyncRoot) || context==null || !context())throw new InvalidOperationException("DesertExpedition context changed.");}
        internal static void Initialize(){
            foreach(uint id in new uint[]{710259,710260,710261,721920,721921,728250,723911})
                if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Desert Expedition catalog missing.");
            uint[] families={57,82};string[] names={"BullMonster","BullMonsterL113"};byte[] levels={112,113};
            for(int i=0;i<families.Length;i++){MonsterInformation m;if(!MonsterInformation.MonsterInformations.TryGetValue(families[i],out m) || m.Name!=names[i] || m.Level!=levels[i] || m.Guard || m.Reviver || m.Boss)throw new InvalidOperationException("Desert Expedition family changed.");}
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_desert_expedition(
uid bigint NOT NULL,life smallint NOT NULL CHECK(life BETWEEN 0 AND 2),stage smallint NOT NULL CHECK(stage BETWEEN 1 AND 6),
soul bigint NOT NULL,pill_until timestamptz,rewards bigint[] NOT NULL DEFAULT '{}',PRIMARY KEY(uid,life));
ALTER TABLE revora_desert_expedition ADD COLUMN IF NOT EXISTS pill_paid smallint NOT NULL DEFAULT 0 CHECK(pill_paid BETWEEN 0 AND 6);
CREATE TABLE IF NOT EXISTS revora_desert_expedition_materials(item_uid bigint PRIMARY KEY,uid bigint NOT NULL,life smallint NOT NULL,
type integer NOT NULL CHECK(type IN(710259,710260,710261,721920,721921)),state smallint NOT NULL DEFAULT 0 CHECK(state BETWEEN 0 AND 2),
FOREIGN KEY(uid,life) REFERENCES revora_desert_expedition(uid,life));
CREATE TABLE IF NOT EXISTS revora_desert_expedition_recoveries(old_uid bigint PRIMARY KEY,new_uid bigint NOT NULL,uid bigint NOT NULL,life smallint NOT NULL,type integer NOT NULL,
FOREIGN KEY(uid,life) REFERENCES revora_desert_expedition(uid,life));
CREATE TABLE IF NOT EXISTS revora_desert_expedition_traps(uid bigint NOT NULL,life smallint NOT NULL,id uuid NOT NULL UNIQUE,
site smallint NOT NULL CHECK(site BETWEEN 0 AND 2),until_at timestamptz NOT NULL,used boolean NOT NULL DEFAULT false,PRIMARY KEY(uid,life),
FOREIGN KEY(uid,life) REFERENCES revora_desert_expedition(uid,life));
CREATE TABLE IF NOT EXISTS revora_desert_expedition_encounter_kills(attempt uuid PRIMARY KEY,uid bigint NOT NULL,life smallint NOT NULL,
kind smallint NOT NULL CHECK(kind BETWEEN 0 AND 1),body bigint NOT NULL,won boolean NOT NULL,site smallint NOT NULL CHECK(site BETWEEN -1 AND 2),
FOREIGN KEY(uid,life) REFERENCES revora_desert_expedition(uid,life));
CREATE TABLE IF NOT EXISTS revora_desert_expedition_pill_payments(uid bigint NOT NULL,life smallint NOT NULL,until_at timestamptz NOT NULL,
paid_before smallint NOT NULL CHECK(paid_before BETWEEN 0 AND 5),paid_after smallint NOT NULL CHECK(paid_after BETWEEN 1 AND 6 AND paid_after>paid_before),
hp_before bigint NOT NULL CHECK(hp_before>0),hp_after bigint NOT NULL CHECK(hp_after>0 AND hp_after<=hp_before),
PRIMARY KEY(uid,life,until_at,paid_after),FOREIGN KEY(uid,life) REFERENCES revora_desert_expedition(uid,life));");}
        }
        private static Run Read(NpgsqlConnection c,NpgsqlTransaction t,GameState p){
            using(var q=Q.Command(c,t,"SELECT stage,soul,pill_until,pill_paid FROM revora_desert_expedition WHERE uid=@uid AND life=@life FOR UPDATE","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn))
            using(var r=q.ExecuteReader())return r.Read()?new Run{Stage=r.GetInt16(0),Soul=checked((uint)r.GetInt64(1)),PillUntil=r.IsDBNull(2)?(DateTime?)null:r.GetDateTime(2).ToUniversalTime(),PillPaid=r.GetInt16(3)}:new Run();
        }
        internal static Run Load(GameState p){if(!Q.Player(p,false))return null;lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();var run=Read(c,null,p);p.Entity.HealthSynchronizationRequired=run.PillUntil.HasValue && run.PillPaid<PillTicks;return run;}}
        private static void Owner(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,GameState p)
        {
            SkillTable.LockOwner(c,t,p);
            using(var q=Q.Command(c,t,"SELECT reborn,level FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))using(var r=q.ExecuteReader())
                if(!r.Read() || Convert.ToInt32(r[0])!=p.Entity.Reborn || Convert.ToInt32(r[1])!=p.Entity.Level)throw new InvalidOperationException("DesertExpedition saved owner changed.");
            var saved=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid FOR UPDATE","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())while(r.Read())saved.Add(checked((uint)r.GetInt64(0)));
            if(!saved.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("DesertExpedition bag changed.");
        }

        private static bool Material(NpgsqlConnection c,NpgsqlTransaction t,GameState p,uint uid,uint type){ConquerItem item;
            if(uid==0 || !p.Inventory.TryGetItem(uid,out item) || item.Lock!=0 || item.Suspicious || item.InWardrobe || item.Days!=0 || item.TimeLeftInMinutes!=0 || !Q.Owns(c,t,p,uid,type))return false;
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_desert_expedition_materials m JOIN items i ON i.uid=m.item_uid WHERE m.item_uid=@id AND m.uid=@uid AND m.life=@life AND m.type=@type AND m.state=0 AND i.locked=0 AND COALESCE(i.suspicious,0)=0 AND COALESCE(i.inwardrobe,0)=0 AND COALESCE(NULLIF(i.days,''),'0')='0')","id",(long)uid,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"type",(int)type))return Equals(q.ExecuteScalar(),true);
        }
        private static List<uint> Materials(NpgsqlConnection c,NpgsqlTransaction t,GameState p,uint type){
            var ids=new List<uint>();using(var q=Q.Command(c,t,"SELECT item_uid FROM revora_desert_expedition_materials WHERE uid=@uid AND life=@life AND type=@type AND state=0 ORDER BY item_uid FOR UPDATE","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"type",(int)type))using(var r=q.ExecuteReader())while(r.Read())ids.Add(checked((uint)r.GetInt64(0)));return ids;
        }
        private static void Track(NpgsqlConnection c,NpgsqlTransaction t,GameState p,ConquerItem item){Q.Insert(c,t,p.Entity.UID,item);
            if(Q.Execute(c,t,"INSERT INTO revora_desert_expedition_materials(item_uid,uid,life,type) VALUES(@item,@uid,@life,@type)","item",(long)item.UID,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"type",(int)item.ID)!=1)throw new InvalidOperationException("DesertExpedition material tracking failed.");
        }
        private static void Consume(NpgsqlConnection c,NpgsqlTransaction t,GameState p,uint uid,uint type){
            if(!Material(c,t,p,uid,type))throw new InvalidOperationException("DesertExpedition material ownership changed.");Q.Consume(c,t,p,uid,type);
            if(Q.Execute(c,t,"UPDATE revora_desert_expedition_materials SET state=1 WHERE item_uid=@item AND uid=@uid AND life=@life AND state=0","item",(long)uid,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn)!=1)throw new InvalidOperationException("DesertExpedition consumption was not saved.");
        }
        private static void Save(NpgsqlConnection c,NpgsqlTransaction t,GameState p,int before,int after){
            if(Q.Execute(c,t,"UPDATE revora_desert_expedition SET stage=@after WHERE uid=@uid AND life=@life AND stage=@before","after",after,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"before",before)!=1)throw new InvalidOperationException("DesertExpedition stage changed.");
        }
        internal static bool Advance(GameState p,int action,Func<bool> context){
            Ready(p,context);var removed=new List<uint>();ConquerItem added=null;ulong silver=p.Entity.Money;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(action<0 || action>3 || run.Stage!=action)return false;
                if(action==2 && run.PillUntil.HasValue && run.PillPaid!=PillTicks)return false;
                if(action==0){
                    if(Q.Execute(c,t,"INSERT INTO revora_desert_expedition(uid,life,stage,soul) VALUES(@uid,@life,1,@soul)","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"soul",(long)QuestSoulReward.Create().ID)!=1)throw new InvalidOperationException("Desert Expedition acceptance failed.");
                }else{
                    if(action>=2){uint type=action==2?710259u:710261u,tool=action==2?721920u:721921u;int count=action==2?10:3;
                        var items=Materials(c,t,p,type).Where(id=>Material(c,t,p,id,type)).Take(count).ToArray();uint toolId=Materials(c,t,p,tool).FirstOrDefault(id=>Material(c,t,p,id,tool));
                        if(items.Length!=count || toolId==0)return false;
                        foreach(uint item in items){Consume(c,t,p,item,type);removed.Add(item);}Consume(c,t,p,toolId,tool);removed.Add(toolId);
                    }
                    uint grant=action==1?721920u:action==2?721921u:0;
                    if(grant!=0){if(p.Inventory.Count-removed.Count>=40)return false;added=Q.New(grant,true);Track(c,t,p,added);}
                    SilverBalance.AssertSaved(c,t,p);silver=checked(silver+100);if(silver>Entity.MaximumSilver)return false;
                    SilverBalance.Persist(c,t,p,silver);Save(c,t,p,action,action+1);
                }
                Ready(p,context);t.Commit();
            }}Q.Install(p,removed,added==null?new ConquerItem[0]:new[]{added});if(action>0)SilverBalance.ApplyCommitted(p,silver);return true;
        }
    }
}
