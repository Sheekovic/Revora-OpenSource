using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Q=Revora.Game.Features.QuestInventory;
using S=Revora.Game.Features.EliminationStore;
using R=Revora.Game.Features.EliminationRules;
namespace Revora.Game.Features {
    internal static class EliminationDenBindings {
        internal const uint Family=9916702;
        internal sealed class Binding {internal Guid Run,Generation;internal ushort Map;internal uint Actor;internal int Slot;}
        private sealed class Scene {internal Entity Body;internal Binding Binding;}
        private static readonly ConditionalWeakTable<Map,Scene> scenes=new ConditionalWeakTable<Map,Scene>();
        internal static MonsterInformation Profile(int slot) {
            // Revora tuning; leader display level/HP are corroborated by catalogs.
            return new MonsterInformation{ID=Family,Name=slot==6?"RockLeader":"RockLackey",Mesh=105,Level=42,Hitpoints=(uint)(slot==6?3656:1219),
                MinAttack=300,MaxAttack=450,Defence=100,AttackRange=2,ViewRange=18,AttackType=1,
                AttackSpeed=1500,MoveSpeed=800,RunSpeed=500,RespawnTime=int.MaxValue/2};
        }
        internal static void Initialize() {
            if(MonsterInformation.MonsterInformations.ContainsKey(Family))throw new InvalidOperationException("Elimination den family collision.");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Q.Execute(c,t,@"CREATE TABLE IF NOT EXISTS revora_elimination_den_bindings(
 run uuid PRIMARY KEY REFERENCES revora_elimination_dens(run),generation uuid NOT NULL UNIQUE,
 map integer NOT NULL CHECK(map BETWEEN 11000 AND 59999),actor bigint NOT NULL,slot integer NOT NULL CHECK(slot BETWEEN 0 AND 6));
CREATE TABLE IF NOT EXISTS revora_elimination_den_kills(
 run uuid NOT NULL REFERENCES revora_elimination_dens(run),slot integer NOT NULL CHECK(slot BETWEEN 0 AND 6),
 generation uuid NOT NULL,actor bigint NOT NULL,killed_at timestamptz NOT NULL,PRIMARY KEY(run,slot));");Q.Execute(c,t,"ALTER TABLE revora_elimination_den_bindings ADD COLUMN IF NOT EXISTS active boolean NOT NULL DEFAULT true;");t.Commit();}}
        }
        internal static int Next(NpgsqlConnection c,NpgsqlTransaction t,Guid run) {
            byte mask=0;bool leader=false;
            using(var q=Q.Command(c,t,"SELECT slot FROM revora_elimination_den_kills WHERE run=@run ORDER BY slot","run",run))
            using(var r=q.ExecuteReader())while(r.Read()){int slot=r.GetInt32(0);if(slot==6)leader=true;else mask|=(byte)(1<<slot);}
            if(leader){if(!R.DenLeaderReady(mask))throw new InvalidOperationException("Elimination den kill history is inconsistent.");return 7;}
            for(int i=0;i<R.DenLackeys;i++)if((mask & (1<<i))==0)return i;return 6;
        }
        internal static Binding Bind(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Map map,Entity body,Guid run) {
            if(t==null || t.Connection!=c || !Q.Player(p) || !Monitor.IsEntered(p.ItemSyncRoot))throw new InvalidOperationException("Elimination den owner lock required.");
            SkillTable.LockOwner(c,t,p);int slot=Next(c,t,run);if(slot>6)return null;
            Map canonical;Entity actor;MapsTable.MapInformation metadata;var profile=Profile(slot);
            if(map==null || map.ID<11000 || map.ID>59999 || map.BaseID!=R.DenBase || !Kernel.Maps.TryGetValue(map.ID,out canonical) || !ReferenceEquals(map,canonical)
                || !MapsTable.MapInformations.TryGetValue(map.ID,out metadata) || metadata.Owner!=p.Entity.UID || metadata.BaseID!=R.DenBase
                || body==null || !map.Entities.TryGetValue(body.UID,out actor) || !ReferenceEquals(body,actor) || body.EntityFlag!=EntityFlag.Monster
                || body.MapID!=map.ID || body.Dead || body.MonsterInfo==null || !ReferenceEquals(body.MonsterInfo.Owner,body)
                || body.MonsterInfo.ID!=Family || body.Name!=profile.Name || body.Body!=profile.Mesh || body.Level!=profile.Level
                || body.Hitpoints!=profile.Hitpoints || body.MaxHitpoints!=profile.Hitpoints || map.Entities.Count!=1)
                throw new InvalidOperationException("Elimination den scene is not canonical.");
            var now=S.Clock();
            using(var q=Q.Command(c,t,@"SELECT d.run FROM revora_elimination_dens d JOIN revora_elimination_runs r ON r.id=d.run
WHERE d.run=@run AND r.uid=@uid AND r.stage=5 AND r.day=@day AND d.until_at>@now FOR UPDATE OF d,r",
                "run",run,"uid",(long)p.Entity.UID,"day",SummerHunt.Day(now),"now",now))if(q.ExecuteScalar()==null)return null;
            using(var q=Q.Command(c,t,"SELECT map FROM revora_elimination_den_bindings WHERE run=@run AND active FOR UPDATE","run",run)){
                var value=q.ExecuteScalar();if(value!=null && Convert.ToInt32(value)!=map.ID && Kernel.Maps.ContainsKey(checked((ushort)Convert.ToInt32(value))))
                    throw new InvalidOperationException("Previous Elimination den is still registered.");}
            var binding=new Binding{Run=run,Generation=Guid.NewGuid(),Map=map.ID,Actor=body.UID,Slot=slot};
            if(Q.Execute(c,t,@"INSERT INTO revora_elimination_den_bindings(run,generation,map,actor,slot) VALUES(@run,@gen,@map,@actor,@slot)
ON CONFLICT(run) DO UPDATE SET generation=EXCLUDED.generation,map=EXCLUDED.map,actor=EXCLUDED.actor,slot=EXCLUDED.slot,active=true",
                "run",run,"gen",binding.Generation,"map",(int)map.ID,"actor",(long)body.UID,"slot",slot)!=1)throw new InvalidOperationException("Den binding not saved.");
            scenes.Remove(map);scenes.Add(map,new Scene{Body=body,Binding=binding});return binding;
        }
        internal static bool Defeat(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Binding binding,Entity body) {
            if(t==null || t.Connection!=c || binding==null || !Q.Player(p) || !Monitor.IsEntered(p.ItemSyncRoot)
                || p.Map==null || p.Map.ID!=binding.Map || p.Map.BaseID!=R.DenBase || p.Entity.MapID!=binding.Map
                || p.Trade==null || p.Trade.InTrade || p.Booth!=null || p.Entity.PokerTable!=0 || !R.Eligible(p.Entity.Level,p.Entity.Reborn))return false;
            Scene scene;Entity actual;MapsTable.MapInformation metadata;
            if(body==null || !scenes.TryGetValue(p.Map,out scene) || !ReferenceEquals(scene.Body,body) || scene.Binding.Generation!=binding.Generation
                || body.UID!=binding.Actor || !p.Map.Entities.TryGetValue(body.UID,out actual) || !ReferenceEquals(actual,body)
                || !MapsTable.MapInformations.TryGetValue(binding.Map,out metadata) || metadata.Owner!=p.Entity.UID)return false;
            SkillTable.LockOwner(c,t,p);
            using(var q=Q.Command(c,t,"SELECT mapid,level,reborn FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))using(var r=q.ExecuteReader())
                if(!r.Read() || Convert.ToInt32(r[0])!=binding.Map || Convert.ToInt32(r[1])!=p.Entity.Level || Convert.ToInt32(r[2])!=p.Entity.Reborn)
                    throw new InvalidOperationException("Den combat owner requires reload.");
            var now=S.Clock();DateTime until;
            using(var q=Q.Command(c,t,@"SELECT d.until_at FROM revora_elimination_dens d JOIN revora_elimination_runs r ON r.id=d.run
JOIN revora_elimination_den_bindings b ON b.run=r.id WHERE r.id=@run AND r.uid=@uid AND r.stage=5 AND r.day=@day
AND d.until_at>@now AND b.active AND b.generation=@gen AND b.map=@map AND b.actor=@actor AND b.slot=@slot FOR UPDATE OF r,d,b",
                "run",binding.Run,"uid",(long)p.Entity.UID,"day",SummerHunt.Day(now),"now",now,"gen",binding.Generation,
                "map",(int)binding.Map,"actor",(long)binding.Actor,"slot",binding.Slot))using(var r=q.ExecuteReader()){
                    if(!r.Read())return false;until=r.GetFieldValue<DateTimeOffset>(0).UtcDateTime;}
            if(Next(c,t,binding.Run)!=binding.Slot)return false;
            if(Q.Execute(c,t,"INSERT INTO revora_elimination_den_kills(run,slot,generation,actor,killed_at) VALUES(@run,@slot,@gen,@actor,@now)",
                "run",binding.Run,"slot",binding.Slot,"gen",binding.Generation,"actor",(long)binding.Actor,"now",now)!=1)throw new InvalidOperationException("Den kill receipt not saved.");
            if(binding.Slot==6 && Q.Execute(c,t,"UPDATE revora_elimination_runs SET stage=6 WHERE id=@run AND stage=5","run",binding.Run)!=1)
                throw new InvalidOperationException("Den victory not saved.");
            if(S.Clock()>=until)throw new InvalidOperationException("Den expired during kill.");return true;
        }
    }
}
