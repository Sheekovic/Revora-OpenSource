using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using S=Revora.Game.Features.AncientDevilStore;
using P=Revora.Game.Features.AncientDevilSummons;

namespace Revora.Game.Features
{
    // Staged battle persistence. Only the future owned world coordinator may
    // bind actual actors or report their canonical deaths; never packet input.
    internal static class AncientDevilBattles
    {
        internal static Func<int,int> Roll=Draw;
        private static int Draw(int count) {
            if(count<=0)throw new ArgumentOutOfRangeException("count");
            var bytes=new byte[4];uint value,limit=uint.MaxValue-uint.MaxValue%(uint)count;
            using(var rng=RandomNumberGenerator.Create())do{rng.GetBytes(bytes);value=BitConverter.ToUInt32(bytes,0);}while(value>=limit);
            return (int)(value%(uint)count);
        }
        internal sealed class Battle {
            internal Guid Generation;internal ushort Map;internal uint[] Actors;
            internal int Defeated;internal DateTime Until;
        }
        internal static void Initialize() {
            foreach(uint id in new[]{1088000u,1088001u})if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Ancient victory catalog missing.");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Q.Execute(c,t,@"CREATE TABLE IF NOT EXISTS revora_ancient_battles(
summon uuid PRIMARY KEY REFERENCES revora_ancient_summons(id),generation uuid NOT NULL UNIQUE,
map integer NOT NULL CHECK(map BETWEEN 11000 AND 59999),actors bigint[] NOT NULL CHECK(cardinality(actors)=5),
defeated integer NOT NULL DEFAULT 0 CHECK(defeated BETWEEN 0 AND 31));
CREATE TABLE IF NOT EXISTS revora_ancient_battle_kills(
summon uuid NOT NULL REFERENCES revora_ancient_summons(id),slot integer NOT NULL CHECK(slot BETWEEN 0 AND 4),
generation uuid NOT NULL,monster_uid bigint NOT NULL CHECK(monster_uid BETWEEN 1 AND 4294967295),
killed_at timestamptz NOT NULL,PRIMARY KEY(summon,slot));
CREATE TABLE IF NOT EXISTS revora_ancient_victories(
summon uuid PRIMARY KEY REFERENCES revora_ancient_summons(id),generation uuid NOT NULL,
item_type integer NOT NULL,quantity integer NOT NULL,won_at timestamptz NOT NULL,
claimed_items bigint[] NOT NULL DEFAULT '{}',claimed_at timestamptz,
CHECK((item_type=1088000 AND quantity=1) OR (item_type=1088001 AND quantity=6)),
CHECK((claimed_at IS NULL AND cardinality(claimed_items)=0) OR (claimed_at IS NOT NULL AND cardinality(claimed_items)=quantity)));
ALTER TABLE revora_ancient_victories ADD COLUMN IF NOT EXISTS soul_uid bigint UNIQUE;");t.Commit();}}
        }
        private static void Seal(GameState p) {
            if(p.Entity.MapID!=1052 || p.Map.BaseID!=1082 || !Q.Near(p,P.Switch,1052)
                || p.Map.Npcs[P.Switch].MapID!=1052 || p.Map.Npcs[P.Switch].X!=218 || p.Map.Npcs[P.Switch].Y!=208)
                throw new InvalidOperationException("Ancient seal is not nearby.");
        }
        // Startup only, before admitting sessions or publishing encounter maps.
        // Paid runs survive memory loss; time and earned victory do not reset.
        internal static void RecoverStartup() {
            DateTime now=P.Clock();
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                var rows=new List<Tuple<uint,Guid,string,DateTime,int,int>>();
                using(var q=Q.Command(c,t,@"SELECT s.uid,s.id,s.state,s.until_at,COALESCE(b.map,0),e.mapid
FROM revora_ancient_summons s JOIN entities e ON e.uid=s.uid
LEFT JOIN revora_ancient_battles b ON b.summon=s.id
WHERE s.state IN('pending','victory') ORDER BY s.uid FOR UPDATE OF e,s"))
                using(var r=q.ExecuteReader())while(r.Read())rows.Add(Tuple.Create(checked((uint)r.GetInt64(0)),r.GetGuid(1),r.GetString(2),
                    r.GetFieldValue<DateTimeOffset>(3).UtcDateTime,r.GetInt32(4),Convert.ToInt32(r[5])));
                foreach(var row in rows) {
                    GameState admitted;if(Kernel.GamePool.TryGetValue(row.Item1,out admitted))
                        throw new InvalidOperationException("Ancient battle startup recovery requires offline owners.");
                    if(row.Item3=="pending" && now>=row.Item4 && Q.Execute(c,t,
                        "UPDATE revora_ancient_summons SET state='expired' WHERE id=@id AND state='pending'","id",row.Item2)!=1)
                        throw new InvalidOperationException("Ancient startup expiry was not saved.");
                    if(row.Item5!=0) {
                        // Retain the guard mask and receipts but revoke old actors.
                        if(Q.Execute(c,t,"UPDATE revora_ancient_battles SET generation=@generation WHERE summon=@id",
                            "generation",Guid.NewGuid(),"id",row.Item2)!=1)throw new InvalidOperationException("Ancient startup generation was not saved.");
                        if(row.Item5==row.Item6 && Q.Execute(c,t,"UPDATE entities SET mapid=1052,x=216,y=208 WHERE uid=@uid AND mapid=@map",
                            "uid",(long)row.Item1,"map",row.Item5)!=1)throw new InvalidOperationException("Ancient startup return was not saved.");
                    }
                }
                t.Commit();
            }}
        }
        private static bool Pending(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Guid id,out DateTime until) {
            until=DateTime.MinValue;
            using(var q=Q.Command(c,t,"SELECT state,until_at FROM revora_ancient_summons WHERE id=@id AND uid=@uid FOR UPDATE","id",id,"uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())if(r.Read() && r.GetString(0)=="pending") {
                until=r.GetFieldValue<DateTimeOffset>(1).UtcDateTime;return P.Clock()<until;
            }
            return false;
        }
        private static Battle Read(NpgsqlConnection c,NpgsqlTransaction t,Guid id,DateTime until) {
            using(var q=Q.Command(c,t,"SELECT generation,map,actors,defeated FROM revora_ancient_battles WHERE summon=@id FOR UPDATE","id",id))
            using(var r=q.ExecuteReader())return !r.Read()?null:new Battle{Generation=r.GetGuid(0),Map=checked((ushort)r.GetInt32(1)),
                Actors=((long[])r[2]).Select(v=>checked((uint)v)).ToArray(),Defeated=r.GetInt32(3),Until=until};
        }
        internal static Battle Bind(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Guid id,ushort map,uint[] actors) {
            S.Owner(c,t,p);Seal(p);DateTime until;
            if(p.Entity.Level<50 || map<11000 || map>59999 || actors==null || actors.Length!=5 || actors.Any(i=>i==0)
                || actors.Distinct().Count()!=5 || !Pending(c,t,p,id,out until))throw new InvalidOperationException("Ancient paid encounter unavailable.");
            using(var q=Q.Command(c,t,"SELECT count(*) FROM revora_ancient_offerings WHERE summon=@id","id",id))
                if(Convert.ToInt32(q.ExecuteScalar())!=9)throw new InvalidOperationException("Ancient payment evidence incomplete.");
            var old=Read(c,t,id,until);Guid generation=Guid.NewGuid();
            if(old!=null && old.Defeated>=16)throw new InvalidOperationException("Ancient battle already complete.");
            if(Q.Execute(c,t,@"INSERT INTO revora_ancient_battles(summon,generation,map,actors) VALUES(@id,@generation,@map,@actors)
ON CONFLICT(summon) DO UPDATE SET generation=EXCLUDED.generation,map=EXCLUDED.map,actors=EXCLUDED.actors",
                "id",id,"generation",generation,"map",(int)map,"actors",actors.Select(i=>(long)i).ToArray())!=1)
                throw new InvalidOperationException("Ancient battle generation was not saved.");
            return Read(c,t,id,until);
        }
        internal static bool Defeat(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Guid id,Guid generation,int slot,uint monster) {
            S.Owner(c,t,p);DateTime until;
            if(slot<0 || slot>4 || !Pending(c,t,p,id,out until))return false;
            var battle=Read(c,t,id,until);
            if(battle==null || battle.Generation!=generation || battle.Map!=p.Entity.MapID || battle.Actors[slot]!=monster
                || (battle.Defeated&(1<<slot))!=0 || slot==4 && battle.Defeated!=15)return false;
            DateTime now=P.Clock();if(now>=until)return false;
            if(Q.Execute(c,t,"INSERT INTO revora_ancient_battle_kills(summon,slot,generation,monster_uid,killed_at) VALUES(@id,@slot,@generation,@monster,@now)",
                "id",id,"slot",slot,"generation",generation,"monster",(long)monster,"now",now)!=1)throw new InvalidOperationException("Ancient kill was not saved.");
            if(Q.Execute(c,t,"UPDATE revora_ancient_battles SET defeated=@next WHERE summon=@id AND generation=@generation AND defeated=@old",
                "next",battle.Defeated|(1<<slot),"id",id,"generation",generation,"old",battle.Defeated)!=1)throw new InvalidOperationException("Ancient guard progress was not saved.");
            if(slot==4) {
                int draw=Roll(100);if(draw<0 || draw>=100)throw new InvalidOperationException("Ancient victory draw outside its range.");
                uint type=draw<20?1088000u:1088001u;int quantity=draw<20?1:6;
                if(Q.Execute(c,t,"INSERT INTO revora_ancient_victories(summon,generation,item_type,quantity,won_at) VALUES(@id,@generation,@type,@quantity,@now)",
                    "id",id,"generation",generation,"type",(int)type,"quantity",quantity,"now",now)!=1)throw new InvalidOperationException("Ancient victory reward was not saved.");
                if(Q.Execute(c,t,"UPDATE revora_ancient_summons SET state='victory' WHERE id=@id AND uid=@uid AND state='pending'",
                    "id",id,"uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Ancient victory was not saved.");
            }
            return true;
        }
        internal static ConquerItem[] Claim(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Guid id) {
            S.Owner(c,t,p);Seal(p);
            bool valuable;
            using(var q=Q.Command(c,t,"SELECT state,valuable_reward FROM revora_ancient_summons WHERE id=@id AND uid=@uid FOR UPDATE","id",id,"uid",(long)p.Entity.UID))
            using(var row=q.ExecuteReader()){if(!row.Read() || row.GetString(0)!="victory")return null;valuable=row.GetBoolean(1);}
            uint type;int count;
            using(var q=Q.Command(c,t,"SELECT item_type,quantity,claimed_at FROM revora_ancient_victories WHERE summon=@id FOR UPDATE","id",id))
            using(var r=q.ExecuteReader()) {
                if(!r.Read() || !r.IsDBNull(2))throw new InvalidOperationException("Ancient reward receipt changed.");
                type=checked((uint)r.GetInt32(0));count=r.GetInt32(1);
            }
            if(type!=1088000 && type!=1088001 || count!=(type==1088000?1:6))throw new InvalidOperationException("Ancient reward recipe changed.");
            long saved;using(var q=Q.Command(c,t,"SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0","uid",(long)p.Entity.UID))saved=(long)q.ExecuteScalar();
            if(saved!=p.Inventory.Count)throw new InvalidOperationException("Ancient reward inventory requires reload.");
            if(saved+count+(valuable?1:0)>40)return null;
            var items=Enumerable.Range(0,count).Select(_=>Q.New(type,false)).ToArray();foreach(var item in items)Q.Insert(c,t,p.Entity.UID,item);
            var soul=valuable?QuestSoulReward.Create():null;if(soul!=null)Q.Insert(c,t,p.Entity.UID,soul);
            if(Q.Execute(c,t,"UPDATE revora_ancient_victories SET claimed_items=@items,claimed_at=@now,soul_uid=@soul WHERE summon=@id AND claimed_at IS NULL",
                "items",items.Select(i=>(long)i.UID).ToArray(),"now",P.Clock(),"id",id,"soul",soul==null?(object)DBNull.Value:(long)soul.UID)!=1)throw new InvalidOperationException("Ancient claim was not saved.");
            if(Q.Execute(c,t,"UPDATE revora_ancient_summons SET state='claimed' WHERE id=@id AND uid=@uid AND state='victory'",
                "id",id,"uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Ancient claim state was not saved.");
            return soul==null?items:items.Concat(new[]{soul}).ToArray();
        }
    }
}
