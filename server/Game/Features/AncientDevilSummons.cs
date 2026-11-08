using System;
using System.Collections.Generic;
using System.Linq;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using S=Revora.Game.Features.AncientDevilStore;
using R=Revora.Game.Features.AncientDevilRules;

namespace Revora.Game.Features
{
    // Staged payment/resume boundary. No NPC, boss or achievement is enabled.
    internal static class AncientDevilSummons
    {
        internal const uint Switch=300017;
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal sealed class Run {
            internal Guid Id;internal string Day,State;internal DateTime Until;
            internal bool Created;internal uint[] Removed=new uint[0];
        }
        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Q.Execute(c,t,@"CREATE TABLE IF NOT EXISTS revora_ancient_summons(
id uuid PRIMARY KEY,uid bigint NOT NULL REFERENCES entities(uid),day varchar(8) NOT NULL,
state varchar(12) NOT NULL CHECK(state IN('pending','victory','claimed','expired')),
created_at timestamptz NOT NULL,until_at timestamptz NOT NULL CHECK(until_at>created_at),UNIQUE(uid,day));
ALTER TABLE revora_ancient_summons ADD COLUMN IF NOT EXISTS valuable_reward boolean NOT NULL DEFAULT false;
CREATE UNIQUE INDEX IF NOT EXISTS revora_ancient_pending_summon ON revora_ancient_summons(uid) WHERE state IN('pending','victory');
CREATE TABLE IF NOT EXISTS revora_ancient_offerings(
summon uuid NOT NULL REFERENCES revora_ancient_summons(id),item_uid bigint PRIMARY KEY CHECK(item_uid BETWEEN 1 AND 4294967295),
item_type integer NOT NULL,origin_trial uuid NOT NULL REFERENCES revora_ancient_trials(id),
UNIQUE(summon,item_type),UNIQUE(origin_trial));");t.Commit();}}
        }
        private static Run Read(NpgsqlDataReader r) {
            return new Run{Id=r.GetGuid(0),Day=r.GetString(1),State=r.GetString(2),Until=r.GetFieldValue<DateTimeOffset>(3).UtcDateTime};
        }
        internal static Run Inspect(NpgsqlConnection c,NpgsqlTransaction t,GameState p) {
            S.Owner(c,t,p);
            using(var q=Q.Command(c,t,@"SELECT id,day,state,until_at FROM revora_ancient_summons
WHERE uid=@uid AND (state IN('pending','victory') OR day=@day)
ORDER BY CASE state WHEN 'victory' THEN 0 WHEN 'pending' THEN 1 ELSE 2 END LIMIT 1",
                "uid",(long)p.Entity.UID,"day",SummerHunt.Day(Clock())))
            using(var r=q.ExecuteReader())return r.Read()?Read(r):null;
        }
        internal static bool Usable(ConquerItem item) {
            return item!=null && item.UID!=0 && item.Position==0 && item.Warehouse==0 && !item.Bound && item.StackSize==1
                && item.Lock==0 && !item.Suspicious && !item.InWardrobe && item.Days==0 && item.TimeLeftInMinutes==0;
        }
        // Caller retains the character lock through commit and Q.Install. A
        // returned Created=false run authorizes no inventory removal whatsoever.
        internal static Run Reserve(NpgsqlConnection c,NpgsqlTransaction t,GameState p,uint[] selected) {
            S.Owner(c,t,p);
            if(p.Entity.Level<50 || p.Entity.MapID!=1052 || p.Map.BaseID!=1082 || !Q.Near(p,Switch,1052)
                || p.Map.Npcs[Switch].MapID!=1052 || p.Map.Npcs[Switch].X!=218 || p.Map.Npcs[Switch].Y!=208)
                throw new InvalidOperationException("Ancient seal is not nearby.");
            DateTime now=Clock();string day=SummerHunt.Day(now);Run pending=null;
            using(var q=Q.Command(c,t,"SELECT id,day,state,until_at FROM revora_ancient_summons WHERE uid=@uid AND state IN('pending','victory') FOR UPDATE","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())if(r.Read())pending=Read(r);
            if(pending!=null) {
                if(pending.State=="victory")return null; // Claim outstanding victory before paying again.
                if(now<pending.Until)return pending;
                if(Q.Execute(c,t,"UPDATE revora_ancient_summons SET state='expired' WHERE id=@id AND state='pending'","id",pending.Id)!=1)
                    throw new InvalidOperationException("Ancient summon expiry was not saved.");
            }
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_ancient_summons WHERE uid=@uid AND day=@day)","uid",(long)p.Entity.UID,"day",day))
                if(Equals(q.ExecuteScalar(),true))return null;
            if(selected==null || selected.Length!=R.RequiredAmulets)throw new InvalidOperationException("Choose nine distinct amulets.");
            var items=new List<ConquerItem>();var offered=new R.Offering[selected.Length];
            for(int i=0;i<selected.Length;i++) {
                ConquerItem item;if(!p.Inventory.TryGetItem(selected[i],out item) || !Usable(item))throw new InvalidOperationException("Ancient offering is unavailable.");
                items.Add(item);offered[i]=new R.Offering(item.UID,item.ID);
            }
            if(!R.ValidOffering(offered))throw new InvalidOperationException("Ancient offering classes must be distinct.");
            var ordered=items.OrderBy(i=>i.UID).ToArray();var origins=new List<Guid>();
            foreach(var item in ordered)using(var q=Q.Command(c,t,@"SELECT r.id FROM revora_ancient_trials r JOIN items i ON i.uid=r.amulet_uid
WHERE r.state='exchanged' AND r.amulet=@type AND i.id=@type AND i.uid=@item AND i.entityid=@uid
AND i.position=0 AND i.warehouse=0 AND i.bound=0 AND i.stacksize=1 AND i.locked=0
AND COALESCE(i.suspicious,0)=0 AND COALESCE(i.inwardrobe,0)=0 AND COALESCE(NULLIF(i.days,''),'0')='0' FOR UPDATE OF i",
                "type",(int)item.ID,"item",(long)item.UID,"uid",(long)p.Entity.UID)) {
                var origin=q.ExecuteScalar();if(origin==null)throw new InvalidOperationException("Ancient amulet earning or ownership changed.");origins.Add((Guid)origin);
            }
            var run=new Run{Id=Guid.NewGuid(),Day=day,State="pending",Until=now.AddMinutes(30),Created=true,Removed=ordered.Select(i=>i.UID).ToArray()};
            if(Q.Execute(c,t,"INSERT INTO revora_ancient_summons(id,uid,day,state,created_at,until_at,valuable_reward) VALUES(@id,@uid,@day,'pending',@now,@until,true)",
                "id",run.Id,"uid",(long)p.Entity.UID,"day",day,"now",now,"until",run.Until)!=1)throw new InvalidOperationException("Ancient summon was not saved.");
            for(int i=0;i<ordered.Length;i++) {
                var item=ordered[i];
                if(Q.Execute(c,t,"INSERT INTO revora_ancient_offerings(summon,item_uid,item_type,origin_trial) VALUES(@summon,@item,@type,@origin)",
                    "summon",run.Id,"item",(long)item.UID,"type",(int)item.ID,"origin",origins[i])!=1)throw new InvalidOperationException("Ancient offering receipt was not saved.");
                Q.Consume(c,t,p,item.UID,item.ID,false);
            }
            if(!Q.Near(p,Switch,1052))throw new InvalidOperationException("Ancient summoner moved during payment.");
            return run;
        }
    }
}
