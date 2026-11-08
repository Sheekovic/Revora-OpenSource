using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Q=Revora.Game.Features.QuestInventory;
using S=Revora.Game.Features.EliminationStore;

namespace Revora.Game.Features
{
    // Staged encounter identity and defeat receipts; normal startup is disabled.
    internal static class EliminationRescueBindings
    {
        internal const uint Family=9916701;
        internal sealed class Binding {
            internal Guid Run,Generation;internal ushort Map;internal uint Actor;
        }
        private sealed class Scene {internal Entity Body;internal Binding Binding;}
        private static readonly ConditionalWeakTable<Map,Scene> scenes=new ConditionalWeakTable<Map,Scene>();
        internal static MonsterInformation Profile() {
            // Display name/level/HP match installed catalogs. RockMonster's body
            // and these combat values are explicit Revora encounter adaptations.
            return new MonsterInformation{ID=Family,Name="RockBehemoth",Mesh=105,Level=42,Hitpoints=3656,
                MinAttack=300,MaxAttack=450,Defence=100,AttackRange=2,ViewRange=18,AttackType=1,
                AttackSpeed=1500,MoveSpeed=800,RunSpeed=500,RespawnTime=int.MaxValue/2};
        }
        internal static void Initialize() {
            if(MonsterInformation.MonsterInformations.ContainsKey(Family))throw new InvalidOperationException("Elimination family collides with the loaded monster catalog.");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Q.Execute(c,t,@"CREATE TABLE IF NOT EXISTS revora_elimination_rescue_bindings(
 run uuid PRIMARY KEY REFERENCES revora_elimination_rescues(run),generation uuid NOT NULL UNIQUE,
 map integer NOT NULL CHECK(map BETWEEN 11000 AND 59999),actor bigint NOT NULL CHECK(actor BETWEEN 1 AND 4294967295));
CREATE TABLE IF NOT EXISTS revora_elimination_rescue_kills(
 run uuid PRIMARY KEY REFERENCES revora_elimination_rescues(run),generation uuid NOT NULL,
 actor bigint NOT NULL CHECK(actor BETWEEN 1 AND 4294967295),killed_at timestamptz NOT NULL);
CREATE TABLE IF NOT EXISTS revora_elimination_landy(
 run uuid PRIMARY KEY REFERENCES revora_elimination_rescue_kills(run),generation uuid NOT NULL,talked_at timestamptz NOT NULL);");Q.Execute(c,t,"ALTER TABLE revora_elimination_rescue_bindings ADD COLUMN IF NOT EXISTS active boolean NOT NULL DEFAULT true;");t.Commit();}}
        }
        private static Binding Read(NpgsqlConnection c,NpgsqlTransaction t,Guid run) {
            using(var q=Q.Command(c,t,"SELECT generation,map,actor FROM revora_elimination_rescue_bindings WHERE run=@run AND active FOR UPDATE","run",run))
            using(var r=q.ExecuteReader())return !r.Read()?null:new Binding{Run=run,Generation=r.GetGuid(0),Map=checked((ushort)r.GetInt32(1)),Actor=checked((uint)r.GetInt64(2))};
        }
        internal static Binding Bind(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Map map,Entity body) {
            // Resume validates the live public-forest owner, locks the character
            // and current daily run, and proves paid admission has not expired.
            var admission=S.ResumeRescue(c,t,p);if(admission==null || admission.RescueWon)return null;
            Map actual;Entity actor;MapsTable.MapInformation info;
            if(map==null || map.ID<11000 || map.ID>59999 || map.BaseID!=1011 || !Kernel.Maps.TryGetValue(map.ID,out actual) || !ReferenceEquals(actual,map)
                || !MapsTable.MapInformations.TryGetValue(map.ID,out info) || info.Owner!=p.Entity.UID || info.BaseID!=1011
                || body==null || body.EntityFlag!=EntityFlag.Monster || body.MapID!=map.ID || body.UID==0
                || !map.Entities.TryGetValue(body.UID,out actor) || !ReferenceEquals(actor,body) || body.Dead
                || body.MonsterInfo==null || !ReferenceEquals(body.MonsterInfo.Owner,body) || body.MonsterInfo.ID!=Family
                || body.Name!="RockBehemoth" || body.Body!=105 || body.Level!=42 || body.MaxHitpoints!=3656 || body.Hitpoints!=3656
                || map.Entities.Count!=1 || map.Companions.Count!=0 || map.FloorItems.Count!=0 || map.Npcs.Count!=0
                || !map.Floor[body.X,body.Y,MapObjectType.Player])throw new InvalidOperationException("Elimination rescue actor is not an owned fresh scene.");
            var old=Read(c,t,admission.RescueRun);
            if(old!=null) {
                Scene scene;
                if(old.Map==map.ID && scenes.TryGetValue(map,out scene) && scene.Binding.Generation==old.Generation) {
                    if(ReferenceEquals(scene.Body,body) && old.Actor==body.UID)return old;
                    throw new InvalidOperationException("Elimination rescue actor instance changed.");
                }
                if(old.Map!=map.ID && Kernel.Maps.ContainsKey(old.Map))throw new InvalidOperationException("Previous Elimination rescue scene is still registered.");
            }
            var binding=new Binding{Run=admission.RescueRun,Generation=Guid.NewGuid(),Map=map.ID,Actor=body.UID};
            if(Q.Execute(c,t,@"INSERT INTO revora_elimination_rescue_bindings(run,generation,map,actor) VALUES(@run,@generation,@map,@actor)
ON CONFLICT(run) DO UPDATE SET generation=EXCLUDED.generation,map=EXCLUDED.map,actor=EXCLUDED.actor,active=true",
                "run",binding.Run,"generation",binding.Generation,"map",(int)binding.Map,"actor",(long)binding.Actor)!=1)
                throw new InvalidOperationException("Elimination rescue binding was not saved.");
            if(S.Clock()>=admission.RescueUntil)throw new InvalidOperationException("Elimination rescue expired during binding.");
            scenes.Remove(map);scenes.Add(map,new Scene{Body=body,Binding=binding});
            return binding;
        }
        internal static bool Defeat(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Binding binding,Entity body) {
            if(t==null || t.Connection!=c || !Q.Player(p) || !Monitor.IsEntered(p.ItemSyncRoot) || binding==null
                || p.Trade==null || p.Trade.InTrade || p.Booth!=null || p.Entity.PokerTable!=0
                || p.Entity.MapID!=binding.Map || p.Map==null || p.Map.BaseID!=1011 || p.Map.ID!=binding.Map)
                throw new InvalidOperationException("Elimination defeat requires its live private owner.");
            Entity actual;Scene scene;MapsTable.MapInformation metadata;
            if(body==null || body.UID!=binding.Actor || body.MapID!=binding.Map || body.MonsterInfo==null || body.MonsterInfo.ID!=Family
                || !p.Map.Entities.TryGetValue(body.UID,out actual) || !ReferenceEquals(actual,body)
                || !scenes.TryGetValue(p.Map,out scene) || !ReferenceEquals(scene.Body,body) || scene.Binding.Generation!=binding.Generation
                || !MapsTable.MapInformations.TryGetValue(binding.Map,out metadata) || metadata.Owner!=p.Entity.UID)return false;
            SkillTable.LockOwner(c,t,p);
            using(var q=Q.Command(c,t,"SELECT level,reborn,mapid FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())if(!r.Read() || Convert.ToInt32(r[0])!=p.Entity.Level || Convert.ToInt32(r[1])!=p.Entity.Reborn
                || Convert.ToInt32(r[2])!=binding.Map)throw new InvalidOperationException("Elimination combat owner requires reload.");
            var now=S.Clock();DateTime until;
            using(var q=Q.Command(c,t,@"SELECT s.until_at FROM revora_elimination_rescues s
JOIN revora_elimination_runs r ON r.id=s.run JOIN revora_elimination_rescue_bindings b ON b.run=s.run
WHERE r.id=@run AND r.uid=@uid AND r.day=@day AND r.stage=2 AND s.state='pending' AND s.until_at>@now
AND b.active AND b.generation=@generation AND b.map=@map AND b.actor=@actor FOR UPDATE OF r,s,b",
                "run",binding.Run,"uid",(long)p.Entity.UID,"day",SummerHunt.Day(now),"now",now,
                "generation",binding.Generation,"map",(int)binding.Map,"actor",(long)binding.Actor))using(var r=q.ExecuteReader()) {
                if(!r.Read())return false;until=r.GetFieldValue<DateTimeOffset>(0).UtcDateTime;
            }
            if(Q.Execute(c,t,"INSERT INTO revora_elimination_rescue_kills(run,generation,actor,killed_at) VALUES(@run,@generation,@actor,@now)",
                "run",binding.Run,"generation",binding.Generation,"actor",(long)binding.Actor,"now",now)!=1)
                throw new InvalidOperationException("Elimination defeat receipt was not saved.");
            if(Q.Execute(c,t,"UPDATE revora_elimination_rescues SET state='rescued' WHERE run=@run AND state='pending'","run",binding.Run)!=1)
                throw new InvalidOperationException("Elimination rescue victory was not saved.");
            if(S.Clock()>=until)throw new InvalidOperationException("Elimination rescue expired during victory.");
            return true;
        }
        internal static Binding BindLandy(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Map map) {
            var admission=S.ResumeRescue(c,t,p);if(admission==null || !admission.RescueWon)return null;
            Map registered;MapsTable.MapInformation metadata;Revora.Interfaces.INpc npc;
            if(map==null || map.ID<11000 || map.ID>59999 || map.BaseID!=1011 || !Kernel.Maps.TryGetValue(map.ID,out registered) || !ReferenceEquals(registered,map)
                || !MapsTable.MapInformations.TryGetValue(map.ID,out metadata) || metadata.Owner!=p.Entity.UID || metadata.BaseID!=1011
                || map.Entities.Count!=0 || map.Npcs.Count!=1 || !map.Npcs.TryGetValue(EliminationRules.Landy,out npc)
                || npc.MapID!=map.ID || npc.Mesh!=4690 || npc.X!=311 || npc.Y!=256)throw new InvalidOperationException("Elimination rescued scene is not canonical.");
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_elimination_rescue_kills WHERE run=@run)","run",admission.RescueRun))
                if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Elimination victory evidence missing.");
            var old=Read(c,t,admission.RescueRun);
            if(old!=null && old.Map!=map.ID && Kernel.Maps.ContainsKey(old.Map))throw new InvalidOperationException("Previous Elimination scene is still registered.");
            var binding=new Binding{Run=admission.RescueRun,Generation=Guid.NewGuid(),Map=map.ID,Actor=EliminationRules.Landy};
            if(Q.Execute(c,t,@"UPDATE revora_elimination_rescue_bindings SET generation=@generation,map=@map,actor=@actor,active=true WHERE run=@run",
                "generation",binding.Generation,"map",(int)binding.Map,"actor",(long)binding.Actor,"run",binding.Run)!=1)
                throw new InvalidOperationException("Elimination rescued scene was not saved.");
            if(S.Clock()>=admission.RescueUntil)throw new InvalidOperationException("Elimination rescued scene expired.");return binding;
        }
        internal static bool TalkLandy(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Binding binding) {
            Revora.Interfaces.INpc npc;
            if(t==null || t.Connection!=c || binding==null || !Q.Player(p) || !Monitor.IsEntered(p.ItemSyncRoot)
                || p.Map==null || p.Map.ID!=binding.Map || p.Entity.MapID!=binding.Map || p.Map.BaseID!=1011
                || p.Trade==null || p.Trade.InTrade || p.Booth!=null || p.Entity.PokerTable!=0
                || !EliminationRules.Eligible(p.Entity.Level,p.Entity.Reborn))throw new InvalidOperationException("Elimination Landy requires her live rescuer.");
            if(p.ActiveNpc!=EliminationRules.Landy || !p.Map.Npcs.TryGetValue(EliminationRules.Landy,out npc) || npc.MapID!=binding.Map
                || npc.Mesh!=4690 || npc.X!=311 || npc.Y!=256 || Kernel.GetDistance(p.Entity.X,p.Entity.Y,npc.X,npc.Y)>6)return false;
            SkillTable.LockOwner(c,t,p);
            using(var q=Q.Command(c,t,"SELECT mapid,level,reborn FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())if(!r.Read() || Convert.ToInt32(r[0])!=binding.Map || Convert.ToInt32(r[1])!=p.Entity.Level || Convert.ToInt32(r[2])!=p.Entity.Reborn)
                throw new InvalidOperationException("Elimination Landy owner requires reload.");
            var now=S.Clock();DateTime until;
            using(var q=Q.Command(c,t,@"SELECT s.until_at FROM revora_elimination_runs r
JOIN revora_elimination_rescues s ON s.run=r.id JOIN revora_elimination_rescue_bindings b ON b.run=r.id
JOIN revora_elimination_rescue_kills k ON k.run=r.id
WHERE r.id=@run AND r.uid=@uid AND r.day=@day AND r.stage=2 AND s.state='rescued' AND s.until_at>@now
AND b.active AND b.generation=@generation AND b.map=@map AND b.actor=@actor FOR UPDATE OF r,s,b",
                "run",binding.Run,"uid",(long)p.Entity.UID,"day",SummerHunt.Day(now),"now",now,
                "generation",binding.Generation,"map",(int)binding.Map,"actor",(long)binding.Actor))using(var r=q.ExecuteReader()) {
                if(!r.Read())return false;until=r.GetFieldValue<DateTimeOffset>(0).UtcDateTime;
            }
            if(Q.Execute(c,t,"INSERT INTO revora_elimination_landy(run,generation,talked_at) VALUES(@run,@generation,@now)",
                "run",binding.Run,"generation",binding.Generation,"now",now)!=1)throw new InvalidOperationException("Elimination Landy receipt was not saved.");
            if(Q.Execute(c,t,"UPDATE revora_elimination_runs SET stage=3 WHERE id=@run AND stage=2","run",binding.Run)!=1)
                throw new InvalidOperationException("Elimination rescue report was not saved.");
            if(S.Clock()>=until)throw new InvalidOperationException("Elimination rescue report expired.");return true;
        }
    }
}
