using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using S=Revora.Game.Features.PurpleBansheeAdmission;

namespace Revora.Game.Features
{
    internal static class PurpleBansheeEncounter
    {
        internal const uint Family=9916806,Health=54000000;
        internal static readonly object Gate=new object();
        internal sealed class Actor {
            internal S.Run Run;internal GameState Owner;internal Map Map;internal Entity Body;internal DateTime Until;internal bool Released;
        }
        internal static readonly ConcurrentDictionary<ushort,Actor> Actors=new ConcurrentDictionary<ushort,Actor>();
        private sealed class Offer {internal ConquerItem Item;internal uint Uid;internal ushort Stack;internal bool Bound;internal Map Map;internal DateTime Until;}
        private static readonly ConditionalWeakTable<GameState,Offer> offers=new ConditionalWeakTable<GameState,Offer>();
        private static bool enabled;
        internal static void Initialize() {
            lock(Gate) {
                if(enabled)return;
                MonsterInformation info;
                if(!MonsterInformation.MonsterInformations.TryGetValue(7565,out info) || info.Name!="PurpleBanshee" || info.Mesh!=951
                    || MonsterInformation.MonsterInformations.ContainsKey(Family))throw new InvalidOperationException("Purple Banshee catalog changed.");
                SwordMasterAdmission.Initialize();S.Initialize();PurpleBansheeRewards.Initialize();PurpleBansheeCombat.Initialize();RecoverInterrupted();enabled=true;
            }
        }
        // Revora policy: startup interruption forfeits the paid daily attempt.
        internal static void RecoverInterrupted() {
            lock(Gate) {
                if(Actors.Count!=0)throw new InvalidOperationException("Cannot recover Purple attempts while actors are running.");
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    Q.Execute(c,t,"UPDATE revora_purple_banshee_runs SET state='failed',ended_at=@now WHERE state='active'","now",S.Clock());
                    using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_purple_banshee_runs WHERE state='active')"))
                        if(Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Interrupted Purple attempts were not closed.");t.Commit();
                }}
            }
        }
        internal static bool IsEncounter(Entity e) {return e!=null && e.EntityFlag==EntityFlag.Monster && e.MonsterInfo!=null && e.MonsterInfo.ID==Family;}
        internal static bool Current(Actor a) {
            Actor registered;Entity body;Map map;MapsTable.MapInformation info;
            return a!=null && !a.Released && a.Run.State=="active" && S.Clock()<a.Until && Q.Player(a.Owner,false)
                && a.Owner.Entity.MapID==a.Map.ID && ReferenceEquals(a.Owner.Map,a.Map)
                && Actors.TryGetValue(a.Map.ID,out registered) && ReferenceEquals(registered,a)
                && Kernel.Maps.TryGetValue(a.Map.ID,out map) && ReferenceEquals(map,a.Map)
                && MapsTable.MapInformations.TryGetValue(a.Map.ID,out info) && info.ID==a.Map.ID && info.Owner==a.Run.Owner && info.HouseLevel==a.Run.HouseLevel
                && info.BaseID==a.Run.Terrain && a.Map.BaseID==a.Run.Terrain
                && a.Map.Entities.TryGetValue(a.Body.UID,out body) && ReferenceEquals(body,a.Body) && ReferenceEquals(body.MonsterInfo.Owner,body) && !body.Dead;
        }
        internal static bool Eligible(Actor a,GameState p,bool alive) {
            if(!Current(a) || !Q.Player(p,alive) || p.Trade==null || p.Trade.InTrade || p.Booth!=null || p.Entity.PokerTable!=0 || p.Entity.MapID!=a.Map.ID || !ReferenceEquals(p.Map,a.Map) || !a.Run.Party.Contains(p.Entity.UID))return false;
            if(ReferenceEquals(p,a.Owner))return true;
            GameState member;var team=a.Owner.Team;
            return team!=null && ReferenceEquals(p.Team,team) && team.TryGetMember(p.Entity.UID,out member) && ReferenceEquals(member,p)
                && team.TryGetMember(a.Owner.Entity.UID,out member) && ReferenceEquals(member,a.Owner);
        }
        private static bool Near(Entity first,Entity second) {
            long dx=(long)first.X-second.X,dy=(long)first.Y-second.Y;
            return dx*dx+dy*dy<=(long)Constants.pScreenDistance*Constants.pScreenDistance;
        }
        internal static GameState Fighter(Actor a,Entity entity) {
            if(entity==null || entity.Dead || entity.MapID!=a.Map.ID || !Eligible(a,entity.Owner,true))return null;
            var p=entity.Owner;if(!Near(p.Entity,a.Body))return null;
            if(ReferenceEquals(entity,p.Entity))return p;Entity current;
            return entity.Companion && ReferenceEquals(p.Companion,entity) && a.Map.Companions.TryGetValue(entity.UID,out current) && ReferenceEquals(entity,current)?p:null;
        }
        internal static bool BlockAttack(Entity attacker,Entity target) {
            var body=IsEncounter(attacker)?attacker:IsEncounter(target)?target:null;if(body==null)return false;Actor a;
            return !Actors.TryGetValue(body.MapID,out a) || !ReferenceEquals(a.Body,body) || !Current(a) || Fighter(a,ReferenceEquals(body,attacker)?target:attacker)==null;
        }
        private sealed class Lease:IDisposable {private readonly object gate;internal Lease(object g){gate=g;}public void Dispose(){Monitor.Exit(gate);}}
        internal static IDisposable DamageLease(Entity attacker,Entity target) {
            var body=IsEncounter(attacker)?attacker:IsEncounter(target)?target:null;Actor a;
            if(body==null || !Actors.TryGetValue(body.MapID,out a))return null;
            return Monitor.TryEnter(a.Owner.ItemSyncRoot)?new Lease(a.Owner.ItemSyncRoot):null;
        }
        internal static bool BlockDamage(Entity attacker,Entity target) {
            if(BlockAttack(attacker,target))return true;var body=IsEncounter(attacker)?attacker:IsEncounter(target)?target:null;Actor a;
            return body!=null && (!Actors.TryGetValue(body.MapID,out a) || !Monitor.IsEntered(a.Owner.ItemSyncRoot));
        }
        private static bool Landing(GameState p,out ushort x,out ushort y) {
            for(int r=3;r<=12;r++)for(int dx=-r;dx<=r;dx++)for(int dy=-r;dy<=r;dy++) {
                if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=r)continue;int tx=p.Entity.X+dx,ty=p.Entity.Y+dy;
                if(tx>=0 && ty>=0 && tx<=ushort.MaxValue && ty<=ushort.MaxValue && S.Cell(p.Map,(ushort)tx,(ushort)ty)) {x=(ushort)tx;y=(ushort)ty;return true;}
            }x=y=0;return false;
        }
        private static void Spawn(GameState p,S.Run run) {
            var info=MonsterInformation.MonsterInformations[7565].Copy();info.ID=Family;info.Name="PurpleBanshee";info.Boss=true;info.Guard=info.Reviver=false;
            // Revora raw combat stats; official numerical parity is not established.
            info.Hitpoints=Health;info.Level=138;info.MinAttack=10000;info.MaxAttack=10000;info.Defence=40000;info.SpellID=0;info.AttackType=1;
            info.ViewRange=18;info.AttackRange=12;info.AttackSpeed=1500;info.MoveSpeed=800;info.RunSpeed=600;info.RespawnTime=int.MaxValue/2;
            info.BoundX=run.X;info.BoundY=run.Y;info.BoundCX=info.BoundCY=1;info.InSight=p.Entity.UID;
            var body=new Entity(EntityFlag.Monster,false){UID=p.Map.EntityUIDCounter.Next,MonsterInfo=info,MapID=run.Map,X=run.X,Y=run.Y,Name=info.Name,
                Body=951,Level=138,MinAttack=10000,MaxAttack=10000,MagicAttack=7500,Defence=40000,MagicDefence=200,MapObjType=MapObjectType.Monster};
            while(p.Map.Entities.ContainsKey(body.UID) || p.Map.Npcs.ContainsKey(body.UID) || p.Map.Companions.ContainsKey(body.UID) || Kernel.GamePool.ContainsKey(body.UID))body.UID=p.Map.EntityUIDCounter.Next;
            info.Owner=body;body.MaxHitpoints=body.Hitpoints=Health;body.ApplyMonsterBossHud();body.SendUpdates=true;
            var a=new Actor{Run=run,Owner=p,Map=p.Map,Body=body,Until=S.Clock().AddMinutes(30)};
            if(!Actors.TryAdd(a.Map.ID,a))throw new InvalidOperationException("Purple Banshee house collision.");
            try {a.Map.AddEntity2(body);foreach(var member in Kernel.GamePool.Values)if(Q.Player(member,false) && member.Entity.MapID==a.Map.ID)body.SendSpawn(member,false);}
            catch {Release(a);throw;}
        }
        internal static void Release(Actor a) {
            if(!Monitor.IsEntered(Gate))throw new InvalidOperationException("Purple cleanup requires encounter ownership.");
            if(a.Released)return;a.Released=true;Actor current;
            if(Actors.TryGetValue(a.Map.ID,out current) && ReferenceEquals(current,a))Actors.TryRemove(a.Map.ID,out current);
            Entity registered;if(a.Map.Entities.TryGetValue(a.Body.UID,out registered) && ReferenceEquals(registered,a.Body))a.Map.RemoveEntity(a.Body);PurpleBansheeCombat.Clear(a);a.Body.MonsterInfo.SendScreen(new Data(true){UID=a.Body.UID,ID=Data.RemoveEntity});
        }
        internal static bool TryUse(ConquerItem item,GameState p) {
            if(item==null || item.ID!=S.Soul)return false;if(p==null || p.ItemSyncRoot==null)return true;
            lock(p.ItemSyncRoot)try {
                offers.Remove(p);p.ActiveNpc=0;
                if(!enabled){p.Send("The Banshee Spirit is dormant. Keep it until the summoning rite returns.");return true;}
                if(!S.Ready(p,p.Map) || !S.Material(p,item)){p.Send("Bring the Banshee Spirit into your own house.");return true;}
                offers.Add(p,new Offer{Item=item,Uid=item.UID,Stack=item.StackSize,Bound=item.Bound,Map=p.Map,Until=S.Clock().AddMinutes(2)});
                p.ActiveNpc=S.Soul;var d=new Npcs(p);d.Text("Awaken the Purple Banshee here? Gather your team first. One Banshee Spirit opens today's challenge. Leaving ends the hunt.");
                d.Option("Awaken her.",1);d.Option("Not yet.",255);d.Send();
            }catch(Exception ex){offers.Remove(p);Q.Fail(p,"purple-banshee-offer",ex);}return true;
        }
        internal static void Confirm(GameState p,NpcRequest req) {
            if(p==null || p.ItemSyncRoot==null)return;
            lock(p.ItemSyncRoot)try {
                p.ActiveNpc=0;Offer o;if(!offers.TryGetValue(p,out o))return;offers.Remove(p);
                if(!enabled || req==null || req.OptionID!=1 || req.NpcID!=0 && req.NpcID!=S.Soul || S.Clock()>=o.Until
                    || !ReferenceEquals(p.Map,o.Map) || o.Item.UID!=o.Uid || o.Item.StackSize!=o.Stack || o.Item.Bound!=o.Bound || !S.Ready(p,o.Map) || !S.Material(p,o.Item))return;
                lock(Gate) {
                    ushort x,y;if(Actors.Count>=128 || Actors.ContainsKey(o.Map.ID) || !Landing(p,out x,out y)){p.Send("There is no room for another challenge here.");return;}
                    var run=S.Admit(p,o.Item,o.Map,x,y,()=>enabled && Actors.Count<128 && !Actors.ContainsKey(o.Map.ID));
                    if(run==null){p.Send("The Banshee Spirit has answered today's challenge already.");return;}
                    try{Spawn(p,run);}catch{S.Fail(run.Owner,run.Id);throw;}
                    p.Send("The Purple Banshee has arrived. Defeat her within thirty minutes!");
                }
            }catch(Exception ex){Q.Fail(p,"purple-banshee-summon",ex);}
        }
        internal static bool TryDie(Entity body,Entity killer) {
            if(!IsEncounter(body))return false;Actor a;
            if(!Actors.TryGetValue(body.MapID,out a) || !ReferenceEquals(a.Body,body) || killer==null || killer.Owner==null)return true;
            var p=killer.Owner;if(!Monitor.TryEnter(a.Owner.ItemSyncRoot))return true;
            try {
                if(!Monitor.TryEnter(p.ItemSyncRoot))return true;
                try {lock(Gate) {
                    Func<bool> proof=()=>Current(a) && Eligible(a,a.Owner,false) && ReferenceEquals(Fighter(a,killer),p) && Near(killer,body);
                    if(!proof())return true;
                    Func<uint[]> snapshot=()=>a.Run.Party.Select(uid=>{GameState member;Kernel.GamePool.TryGetValue(uid,out member);return member;})
                        .Where(member=>Eligible(a,member,false) && (ReferenceEquals(member,a.Owner) || Near(member.Entity,body))).Select(member=>member.Entity.UID).ToArray();
                    var party=snapshot();
                    using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                        SkillTable.LockOwner(c,t,p);
                        if(Q.Execute(c,t,"UPDATE revora_purple_banshee_runs SET state='victory',killer=@killer,x=@x,y=@y,ended_at=@now WHERE id=@id AND uid=@owner AND state='active'",
                            "killer",(long)p.Entity.UID,"x",(int)body.X,"y",(int)body.Y,"now",S.Clock(),"id",a.Run.Id,"owner",(long)a.Run.Owner)!=1)return true;
                        PurpleBansheeRewards.Record(c,t,a.Run.Id,p,party);
                        EquipmentAchievements.RecordUnarmedVictory(c,t,p,body,killer,"purple:"+a.Run.Id.ToString("N"));
                        if(!proof() || !snapshot().SequenceEqual(party))throw new InvalidOperationException("Purple victory context changed.");t.Commit();
                    }}
                    a.Run.State="victory";Release(a);
                }
                body.DieCore(p.Entity);Q.Refresh(p);PurpleBansheeRewards.Tick(true);p.Send("The Purple Banshee falls. Claim the spoils before they fade.");
                }catch(Exception ex){Q.Fail(p,"purple-banshee-victory",ex);}finally{Monitor.Exit(p.ItemSyncRoot);}
            }finally{Monitor.Exit(a.Owner.ItemSyncRoot);}return true;
        }
        private static DateTime nextOrphanSweep;
        private static void SweepOrphans() {
            lock(Gate) {
                if(S.Clock()<nextOrphanSweep)return;nextOrphanSweep=S.Clock().AddSeconds(5);
                // Admission, spawn and victory also hold Gate: no in-flight summon is reaped.
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    Q.Execute(c,t,@"WITH lost AS (SELECT id FROM revora_purple_banshee_runs WHERE state='active' AND NOT(id=ANY(@live))
ORDER BY started_at LIMIT 128 FOR UPDATE SKIP LOCKED)
UPDATE revora_purple_banshee_runs r SET state='failed',ended_at=@now FROM lost WHERE r.id=lost.id",
                        "live",Actors.Values.Select(a=>a.Run.Id).ToArray(),"now",S.Clock());t.Commit();
                }}
            }
        }
        internal static void Tick() {
            if(!enabled)return;
            foreach(var a in Actors.Values.ToArray()) {
                if(!Monitor.TryEnter(a.Owner.ItemSyncRoot))continue;
                try {lock(Gate) {
                    if(a.Released)continue;
                    var target=a.Run.Party.Select(uid=>{GameState p;Kernel.GamePool.TryGetValue(uid,out p);return p;})
                        .Where(p=>Eligible(a,p,true)).OrderBy(p=>Kernel.GetDistance(p.Entity.X,p.Entity.Y,a.Body.X,a.Body.Y)).FirstOrDefault();
                    if(!Current(a) || target==null) {
                        bool closed=S.Fail(a.Run.Owner,a.Run.Id);
                        if(!closed)using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT state FROM revora_purple_banshee_runs WHERE id=@run AND uid=@uid",
                            "run",a.Run.Id,"uid",(long)a.Run.Owner)){var state=q.ExecuteScalar() as string;closed=state=="failed" || state=="victory";}}
                        if(closed){a.Run.State="failed";Release(a);}continue;
                    }
                    a.Body.MonsterInfo.InSight=target.Entity.UID;
                }}catch(Exception ex){Q.Fail(a.Owner,"purple-banshee-cleanup",ex);}finally{Monitor.Exit(a.Owner.ItemSyncRoot);}
                PurpleBansheeCombat.Tick(a);
            }
            SweepOrphans();PurpleBansheeRewards.Tick();
        }
    }
}
