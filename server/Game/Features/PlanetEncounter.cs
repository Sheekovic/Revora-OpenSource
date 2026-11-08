using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class PlanetEncounter
    {
        internal const uint Family=9926100, CacheNpc=900000190;
        private const int Capacity=16;
        private static readonly object Gate=new object();
        internal sealed class Attempt {
            internal Guid Id,Run;
            internal uint Owner;
            internal GameState Player;
            internal Map Map;
            internal DateTime Until;
            internal int Defeated;
            internal bool Ended;
            internal readonly Entity[] Guards=new Entity[3];
        }
        internal static readonly ConcurrentDictionary<ushort,Attempt> Maps=new ConcurrentDictionary<ushort,Attempt>();
        [ThreadStatic] private static Guid? transfer;
        private static bool initialized;
        private static DateTime nextSweep;
        internal static void Initialize()
        {
            using(var c=DataHolder.MySqlConnection) {
                c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_planet_attempts(
id uuid PRIMARY KEY,run uuid NOT NULL REFERENCES revora_planet_runs(id),uid bigint NOT NULL CHECK(uid>0),
arena integer NOT NULL CHECK(arena BETWEEN 11000 AND 59999),started_at timestamptz NOT NULL,until_at timestamptz NOT NULL,
defeated integer NOT NULL DEFAULT 0 CHECK(defeated BETWEEN 0 AND 7),
state varchar(16) NOT NULL DEFAULT 'active' CHECK(state IN('active','victory','abandoned')),
returned boolean NOT NULL DEFAULT false,cache_at timestamptz,
CHECK(until_at>started_at),CHECK((state='victory' AND defeated=7 AND cache_at IS NOT NULL) OR (state<>'victory' AND cache_at IS NULL)));
CREATE UNIQUE INDEX IF NOT EXISTS revora_planet_pending_return ON revora_planet_attempts(uid) WHERE NOT returned;");
            }
            initialized=true;
        }
        internal static void RecoverInterrupted()
        {
            lock(Gate) {
                if(Maps.Count!=0)throw new InvalidOperationException("Cannot recover Planet attempts while encounters are running.");
                using(var c=DataHolder.MySqlConnection) {
                    c.Open();Q.Execute(c,null,"UPDATE revora_planet_attempts SET state='abandoned' WHERE state='active'");
                    using(var q=Q.Command(c,null,"SELECT EXISTS(SELECT 1 FROM revora_planet_attempts WHERE state='active')"))
                        if(Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Planet interrupted attempts were not closed.");
                }
            }
        }
        private static bool Eligible(Attempt a,GameState p,bool alive=true) {
            Attempt registered;
            return !a.Ended && Q.Player(p,alive) && ReferenceEquals(a.Player,p) && p.Entity.UID==a.Owner
                && p.Entity.MapID==a.Map.ID && ReferenceEquals(p.Map,a.Map) && PlanetWeekend.Clock()<a.Until
                && Maps.TryGetValue(a.Map.ID,out registered) && ReferenceEquals(a,registered);
        }
        private static GameState Fighter(Attempt a,Entity e) {
            if(e==null || e.Dead || !Eligible(a,e.Owner))return null;
            var p=e.Owner;Entity owned;
            if(ReferenceEquals(p.Entity,e))return p;
            return e.Companion && ReferenceEquals(p.Companion,e) && e.MapID==a.Map.ID
                && a.Map.Companions.TryGetValue(e.UID,out owned) && ReferenceEquals(owned,e)?p:null;
        }
        internal static bool IsEncounter(Entity e) {
            return e!=null && e.MonsterInfo!=null && e.MonsterInfo.ID>=Family && e.MonsterInfo.ID<Family+3;
        }
        internal static bool BlockAttack(Entity attacker,Entity target) {
            var mob=IsEncounter(attacker)?attacker:IsEncounter(target)?target:null;
            lock(Gate) {
                Attempt a;
                if(mob!=null) {
                    int slot=(int)(mob.MonsterInfo.ID-Family);Entity owned;
                    return !Maps.TryGetValue(mob.MapID,out a) || !ReferenceEquals(a.Guards[slot],mob) || mob.Dead
                        || (a.Defeated&(1<<slot))!=0 || !a.Map.Entities.TryGetValue(mob.UID,out owned) || !ReferenceEquals(owned,mob)
                        || Fighter(a,ReferenceEquals(mob,attacker)?target:attacker)==null;
                }
                return attacker!=null && target!=null && (Maps.ContainsKey(attacker.MapID) || Maps.ContainsKey(target.MapID));
            }
        }
        private sealed class DamageScope : IDisposable {
            private readonly object gate;
            internal DamageScope(object gate) {this.gate=gate;}
            public void Dispose() {Monitor.Exit(gate);}
        }
        internal static IDisposable DamageLease(Entity attacker,Entity target) {
            var mob=IsEncounter(attacker)?attacker:IsEncounter(target)?target:null;
            Attempt a;if(mob==null || !Maps.TryGetValue(mob.MapID,out a) || a.Player.ItemSyncRoot==null)return null;
            // Match lifecycle lock ordering: owner inventory precedes the encounter gate.
            var gate=a.Player.ItemSyncRoot;return Monitor.TryEnter(gate)?new DamageScope(gate):null;
        }
        internal static bool BlockDamage(Entity attacker,Entity target) {
            if(BlockAttack(attacker,target))return true;
            var mob=IsEncounter(attacker)?attacker:IsEncounter(target)?target:null;
            Attempt a;
            return mob!=null && (!Maps.TryGetValue(mob.MapID,out a) || !Monitor.IsEntered(a.Player.ItemSyncRoot));
        }
        internal static bool AllowTeleport(GameState p,ushort destination,ushort x,ushort y) {
            Attempt a;if(!Maps.TryGetValue(destination,out a))return true;
            lock(Gate) {
                if(a.Ended || !Q.Player(p,false) || !ReferenceEquals(a.Player,p) || PlanetWeekend.Clock()>=a.Until
                    || !a.Map.Floor[x,y,MapObjectType.Player])return false;
                return transfer==a.Id || Eligible(a,p,false) && Kernel.GetDistance(p.Entity.X,p.Entity.Y,x,y)<22;
            }
        }
        internal static bool Enter(GameState p)
        {
            if(!initialized || !Q.Player(p))return false;
            lock(p.ItemSyncRoot)lock(Gate) {
                if(!Q.Near(p,PathHuntSystem.NpcId,1002) || p.Map.ID!=1002 || Maps.Count>=Capacity)return false;
                var now=PlanetWeekend.Clock();var window=PlanetWeekend.Current(now);if(window==null)return false;
                Map source;if(!Kernel.Maps.TryGetValue(700,out source))throw new InvalidOperationException("Planet arena terrain is unavailable.");
                Attempt a=null;bool reserved=false;string phase="reserve";
                try {
                    using(var c=DataHolder.MySqlConnection) {
                        c.Open();using(var t=c.BeginTransaction()) {
                            using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                                if(q.ExecuteScalar()==null)throw new InvalidOperationException("Planet character is missing.");
                            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_planet_attempts WHERE uid=@uid AND NOT returned)","uid",(long)p.Entity.UID))
                                if(Equals(q.ExecuteScalar(),true))return false;
                            Guid run;DateTime end;
                            using(var q=Q.Command(c,t,"SELECT id,ends_at FROM revora_planet_runs WHERE uid=@uid AND weekend=@week AND stage='encounter' AND kills=100 AND accepted_at<=@now AND ends_at>@now FOR UPDATE",
                                "uid",(long)p.Entity.UID,"week",window.Key,"now",now))
                            using(var r=q.ExecuteReader()) {if(!r.Read())return false;run=r.GetGuid(0);end=r.GetFieldValue<DateTimeOffset>(1).UtcDateTime;}
                            a=new Attempt{Id=Guid.NewGuid(),Run=run,Owner=p.Entity.UID,Player=p,Until=end<now.AddMinutes(15)?end:now.AddMinutes(15)};
                            phase="terrain";a.Map=Map.CreateDynamic(700,source.Path,false);a.Map.Portals.Clear();
                            if(!Maps.TryAdd(a.Map.ID,a))throw new InvalidOperationException("Planet arena identity collision.");
                            foreach(var xy in new[]{new[]{48,41},new[]{44,48},new[]{52,48},new[]{48,53},new[]{48,48}})
                                if(!a.Map.Floor[xy[0],xy[1],MapObjectType.Player])throw new InvalidOperationException("Planet arena landing is blocked.");
                            phase="receipt";
                            if(Q.Execute(c,t,@"INSERT INTO revora_planet_attempts(id,run,uid,arena,started_at,until_at)
VALUES(@id,@run,@uid,@arena,@now,@until)","id",a.Id,"run",run,"uid",(long)a.Owner,"arena",(int)a.Map.ID,"now",now,"until",a.Until)!=1)
                                throw new InvalidOperationException("Planet encounter admission was not saved.");
                            t.Commit();reserved=true;
                        }
                    }
                    phase="spawn";
                    MapsTable.MapInformations[a.Map.ID]=new MapsTable.MapInformation{ID=a.Map.ID,BaseID=700,Status=2};
                    a.Map.AddNpc(new NpcSpawn{UID=CacheNpc,MapID=a.Map.ID,X=48,Y=48,Mesh=10,Name="GuardedCache",Type=Enums.NpcType.Talker});
                    for(int i=0;i<3;i++)Spawn(a,i);
                    phase="travel";transfer=a.Id;
                    try {p.Entity.Teleport(700,a.Map.ID,48,41);}finally{transfer=null;}
                    if(p.Entity.MapID!=a.Map.ID)throw new InvalidOperationException("Planet encounter transfer failed.");
                    p.Send("Defeat the three sentries, then recover the cache. You have fifteen minutes, until the weekend closes.");
                    return true;
                } catch(Exception ex) {
                    if(a!=null) {
                        if(reserved) {
                            try {Abandon(a);}catch(Exception closeError) {Console.WriteLine("[quest] source=planet-entry-close error="+closeError.GetType().Name);}
                        } else if(a.Map!=null) {a.Ended=true;Cleanup(a);}
                    }
                    Q.Fail(p,"planet-entry-"+phase,ex);return false;
                }
            }
        }
        private static void Spawn(Attempt a,int slot) {
            var info=MonsterInformation.MonsterInformations[1413].Copy();
            info.ID=Family+(uint)slot;info.Name=slot==2?"AstralCaptain":"AstralSentry";
            info.Boss=slot==2;info.Guard=info.Reviver=false;info.SpellID=0;info.AttackType=1;
            info.Hitpoints=slot==2?210300u:70100u;info.MinAttack=info.MaxAttack=slot==2?1800u:1200u;
            info.AttackRange=1;info.ViewRange=18;info.MoveSpeed=800;info.RunSpeed=500;info.AttackSpeed=1500;
            info.RespawnTime=int.MaxValue/2;info.BoundX=48;info.BoundY=48;info.BoundCX=info.BoundCY=16;
            var mob=new Entity(EntityFlag.Monster,false){UID=a.Map.EntityUIDCounter.Next,MapID=a.Map.ID,
                X=(ushort)(slot==0?44:slot==1?52:48),Y=(ushort)(slot==2?53:48),MonsterInfo=info,MapObjType=MapObjectType.Monster,
                Name=info.Name,Body=info.Mesh,Level=info.Level,MinAttack=info.MinAttack,MaxAttack=info.MaxAttack,MagicAttack=info.MaxAttack,Defence=info.Defence};
            while(a.Map.Entities.ContainsKey(mob.UID) || Kernel.GamePool.ContainsKey(mob.UID))mob.UID=a.Map.EntityUIDCounter.Next;
            info.Owner=mob;mob.Hitpoints=mob.MaxHitpoints=info.Hitpoints;mob.ApplyMonsterBossHud();mob.SendUpdates=true;
            a.Guards[slot]=mob;a.Map.AddEntity2(mob);
        }
        internal static bool TryDie(Entity mob,Entity killer) {
            if(!IsEncounter(mob))return false;
            Attempt a;if(!Maps.TryGetValue(mob.MapID,out a) || a.Player.ItemSyncRoot==null || !Monitor.TryEnter(a.Player.ItemSyncRoot))return true;
            try {lock(Gate) {
                if(BlockAttack(killer,mob) || Kernel.GetDistance(killer.X,killer.Y,mob.X,mob.Y)>Constants.pScreenDistance)return true;
                int slot=(int)(mob.MonsterInfo.ID-Family),bit=1<<slot;
                try {
                    using(var c=DataHolder.MySqlConnection) {
                        c.Open();using(var t=c.BeginTransaction()) {
                            if(Q.Execute(c,t,@"UPDATE revora_planet_attempts SET defeated=defeated | @bit
WHERE id=@id AND state='active' AND NOT returned AND until_at>@now AND (defeated & @bit)=0",
                                "id",a.Id,"bit",bit,"now",PlanetWeekend.Clock())!=1)throw new InvalidOperationException("Planet guard defeat was not saved.");
                            t.Commit();
                        }
                    }
                    a.Defeated|=bit;mob.Hitpoints=0;
                    mob.MonsterInfo.SendScreen(new Data(true){UID=mob.UID,ID=Data.RemoveEntity});a.Map.RemoveEntity(mob);
                    if(a.Defeated==7)a.Player.Send("The sentries are defeated. Recover the cache at 48,48.");
                }catch(Exception ex){Q.Fail(a.Player,"planet-guard",ex);}
            }}finally{Monitor.Exit(a.Player.ItemSyncRoot);}return true;
        }
        internal static bool Collect(GameState p) {
            if(!Q.Player(p))return false;
            lock(p.ItemSyncRoot)lock(Gate) {
                Attempt a;if(!Maps.TryGetValue(p.Entity.MapID,out a) || !Eligible(a,p) || a.Defeated!=7 || p.ActiveNpc!=CacheNpc
                    || Kernel.GetDistance(p.Entity.X,p.Entity.Y,48,48)>3 || p.Trade==null || p.Trade.InTrade || p.Booth!=null)return false;
                using(var c=DataHolder.MySqlConnection) {
                    c.Open();using(var t=c.BeginTransaction()) {
                        if(Q.Execute(c,t,@"UPDATE revora_planet_attempts SET state='victory',cache_at=@now
WHERE id=@id AND uid=@uid AND state='active' AND defeated=7 AND until_at>@now AND NOT returned",
                            "id",a.Id,"uid",(long)p.Entity.UID,"now",PlanetWeekend.Clock())!=1)throw new InvalidOperationException("Planet cache receipt was not saved.");
                        if(Q.Execute(c,t,"UPDATE revora_planet_runs SET stage='delivery' WHERE id=@run AND uid=@uid AND stage='encounter' AND ends_at>@now",
                            "run",a.Run,"uid",(long)p.Entity.UID,"now",PlanetWeekend.Clock())!=1)throw new InvalidOperationException("Planet delivery stage was not saved.");
                        t.Commit();
                    }
                }
                a.Ended=true;Return(p);Cleanup(a);return true;
            }
        }
        internal static bool HandleNpc(GameState p,NpcRequest request) {
            if(p==null || request==null || p.ActiveNpc!=CacheNpc)return false;
            if(!Q.Player(p))return true;
            lock(p.ItemSyncRoot)try {
                Attempt a;if(!Maps.TryGetValue(p.Entity.MapID,out a) || !Eligible(a,p) || Kernel.GetDistance(p.Entity.X,p.Entity.Y,48,48)>3)return true;
                var d=new Npcs(p);
                if(request.OptionID==1) {if(Collect(p))p.Send("The cache is yours. Deliver it to Rowan before the weekend ends.");return true;}
                d.Text(a.Defeated==7?"The sentries are gone. Rowan awaits this cache.":"The sentries still watch the cache.");
                if(a.Defeated==7)d.Option("Recover the cache.",1);d.Option("Leave it.",255);d.Send();
            }catch(Exception ex){Q.Fail(p,"planet-cache",ex);}return true;
        }
        private static void Abandon(Attempt a) {
            using(var c=DataHolder.MySqlConnection) {
                c.Open();Q.Execute(c,null,"UPDATE revora_planet_attempts SET state='abandoned' WHERE id=@id AND state='active'","id",a.Id);
                using(var q=Q.Command(c,null,"SELECT state FROM revora_planet_attempts WHERE id=@id","id",a.Id))
                    if(Equals(q.ExecuteScalar(),"active"))throw new InvalidOperationException("Planet encounter closure was not saved.");
            }
            a.Ended=true;
        }
        private static void Cleanup(Attempt a) {
            if(Kernel.GamePool.Values.Any(p=>p!=null && !p.Fake && p.Entity!=null && p.Entity.MapID==a.Map.ID))return;
            foreach(var mob in a.Guards)if(mob!=null)a.Map.RemoveEntity(mob);
            a.Map.Npcs.Clear();Attempt ignored;Maps.TryRemove(a.Map.ID,out ignored);
            MapsTable.MapInformations.Remove(a.Map.ID);a.Map.Dispose();
        }
        internal static bool Return(GameState p) {
            if(!initialized || !Q.Player(p,false))return false;
            lock(p.ItemSyncRoot) {
                try {
                    using(var c=DataHolder.MySqlConnection) {
                        c.Open();Guid id;
                        using(var q=Q.Command(c,null,"SELECT id FROM revora_planet_attempts WHERE uid=@uid AND NOT returned AND state<>'active'","uid",(long)p.Entity.UID)) {
                            object value=q.ExecuteScalar();if(value==null)return true;id=(Guid)value;
                        }
                        Map town;if(!Kernel.Maps.TryGetValue(1002,out town))throw new InvalidOperationException("Planet return town is unavailable.");
                        ushort x=321,y=299;bool landing=town.Floor[x,y,MapObjectType.Player];
                        for(int radius=2;radius<=5 && !landing;radius++)for(int dx=-radius;dx<=radius && !landing;dx++)for(int dy=-radius;dy<=radius && !landing;dy++) {
                            int tx=319+dx,ty=299+dy;if(!town.Floor[tx,ty,MapObjectType.Player])continue;
                            x=(ushort)tx;y=(ushort)ty;landing=true;
                        }
                        if(!landing)throw new InvalidOperationException("Planet safe return landing is blocked.");
                        p.Entity.Teleport(1002,x,y);
                        if(p.Entity.MapID!=1002 || p.Entity.X!=x || p.Entity.Y!=y)throw new InvalidOperationException("Planet return did not arrive.");
                        using(var t=c.BeginTransaction()) {
                            // Persist safe position before acknowledging return; never infer victory from position.
                            if(Q.Execute(c,t,"UPDATE entities SET mapid=1002,x=@x,y=@y WHERE uid=@uid","x",(int)x,"y",(int)y,"uid",(long)p.Entity.UID)!=1)
                                throw new InvalidOperationException("Planet return position was not saved.");
                            if(Q.Execute(c,t,"UPDATE revora_planet_attempts SET returned=true WHERE id=@id AND NOT returned AND state<>'active'","id",id)!=1)
                                throw new InvalidOperationException("Planet return acknowledgement was not saved.");
                            t.Commit();
                        }
                    }
                    return true;
                }catch(Exception ex){Q.Fail(p,"planet-return",ex);return false;}
            }
        }
        internal static void OnLogin(GameState p) {
            if(!initialized || !Q.Player(p,false))return;
            lock(p.ItemSyncRoot)try {
                lock(Gate) {
                    foreach(var a in Maps.Values.Where(a=>a.Owner==p.Entity.UID).ToArray()){Abandon(a);Cleanup(a);}
                    using(var c=DataHolder.MySqlConnection){
                        c.Open();Q.Execute(c,null,"UPDATE revora_planet_attempts SET state='abandoned' WHERE uid=@uid AND state='active'","uid",(long)p.Entity.UID);
                        using(var q=Q.Command(c,null,"SELECT EXISTS(SELECT 1 FROM revora_planet_attempts WHERE uid=@uid AND state='active')","uid",(long)p.Entity.UID))
                            if(Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Planet interrupted session was not closed.");
                    }
                }
                if(!Return(p))return;
                lock(Gate)foreach(var a in Maps.Values.Where(a=>a.Owner==p.Entity.UID && a.Ended).ToArray())Cleanup(a);
            }catch(Exception ex){Q.Fail(p,"planet-recovery",ex);}
        }
        internal static void Tick() {
            if(!initialized || PlanetWeekend.Clock()<nextSweep)return;nextSweep=PlanetWeekend.Clock().AddSeconds(1);
            foreach(var a in Maps.Values.ToArray()) {
                if(!Monitor.TryEnter(a.Player.ItemSyncRoot))continue;
                try {lock(Gate) {
                    if(!a.Ended && (!Eligible(a,a.Player) || a.Player.Entity.Dead))Abandon(a);
                    if(a.Ended) {if(Q.Player(a.Player,false))Return(a.Player);Cleanup(a);}
                }}catch(Exception ex){Console.WriteLine("[quest] source=planet-encounter error="+ex.GetType().Name);}
                finally {Monitor.Exit(a.Player.ItemSyncRoot);}
            }
        }
    }
}
