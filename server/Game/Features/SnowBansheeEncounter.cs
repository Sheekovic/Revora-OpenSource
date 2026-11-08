using System;
using System.Linq;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using S=Revora.Game.Features.SnowBansheeStore;

namespace Revora.Game.Features
{
    internal static class SnowBansheeEncounter
    {
        internal const uint Family=9916805;
        internal static readonly object Gate=S.Gate;
        internal sealed class Actor {
            internal S.Run Run;internal Map Map;internal Entity Body;internal bool Released;
            internal long Sequence;internal DateTime Checkpoint;
        }
        internal static Actor Active;
        private static bool enabled;
        private static DateTime nextSpawn;
        internal static bool IsEncounter(Entity e) {return e!=null && e.EntityFlag==EntityFlag.Monster && e.MonsterInfo!=null && e.MonsterInfo.ID==Family;}
        internal static bool Current(Actor a) {
            Entity body;return enabled && a!=null && !a.Released && ReferenceEquals(Active,a) && S.Site(a.Map,a.Body.X,a.Body.Y)
                && a.Body.MapID==S.MapId && !a.Body.Dead && IsEncounter(a.Body) && ReferenceEquals(a.Body.MonsterInfo.Owner,a.Body)
                && a.Map.Entities.TryGetValue(a.Body.UID,out body) && ReferenceEquals(body,a.Body);
        }
        internal static bool Near(Entity first,Entity second,int radius=19) {
            long dx=(long)first.X-second.X,dy=(long)first.Y-second.Y;return dx*dx+dy*dy<=(long)radius*radius;
        }
        internal static bool Eligible(Actor a,GameState p,bool alive) {
            return Current(a) && Q.Player(p,alive) && p.Entity.MapID==S.MapId && ReferenceEquals(p.Map,a.Map)
                && p.Trade!=null && !p.Trade.InTrade && p.Booth==null && p.Entity.PokerTable==0 && Near(p.Entity,a.Body);
        }
        internal static GameState Fighter(Actor a,Entity entity) {
            if(entity==null || entity.Dead || entity.MapID!=S.MapId || !Eligible(a,entity.Owner,true) || !Near(entity,a.Body))return null;
            var p=entity.Owner;if(ReferenceEquals(p.Entity,entity))return p;Entity registered;
            return entity.Companion && ReferenceEquals(p.Companion,entity) && a.Map.Companions.TryGetValue(entity.UID,out registered) && ReferenceEquals(entity,registered)?p:null;
        }
        internal static bool BlockAttack(Entity attacker,Entity target) {
            var body=IsEncounter(attacker)?attacker:IsEncounter(target)?target:null;
            return body!=null && (Active==null || !ReferenceEquals(Active.Body,body) || Fighter(Active,ReferenceEquals(body,attacker)?target:attacker)==null);
        }
        private sealed class Lease:IDisposable {public void Dispose(){Monitor.Exit(Gate);}}
        internal static IDisposable DamageLease(Entity attacker,Entity target) {
            if(!IsEncounter(attacker) && !IsEncounter(target))return null;
            return Monitor.TryEnter(Gate)?new Lease():null;
        }
        internal static bool BlockDamage(Entity attacker,Entity target) {
            return BlockAttack(attacker,target) || (IsEncounter(attacker) || IsEncounter(target)) && !Monitor.IsEntered(Gate);
        }
        internal static void Initialize() {
            lock(Gate) {
                if(enabled)return;MonsterInformation info;Map map;
                if(!MonsterInformation.MonsterInformations.TryGetValue(4171,out info) || info.Name!="IceQueen" || info.Mesh!=951
                    || MonsterInformation.MonsterInformations.ContainsKey(Family) || !Kernel.Maps.TryGetValue(S.MapId,out map))
                    throw new InvalidOperationException("Snow Banshee catalog/world changed.");
                S.Initialize(map);SnowBansheeRewards.Initialize();SnowBansheeCombat.Initialize();enabled=true;nextSpawn=DateTime.MinValue;
                EnsureActor();
            }
        }
        private static bool Cell(Map map,ushort x,ushort y) {
            return S.Site(map,x,y) && map.Floor[x,y,MapObjectType.Monster]
                && map.Npcs.Values.All(n=>Math.Abs((int)n.X-x)>1 || Math.Abs((int)n.Y-y)>1)
                && map.Entities.Values.All(e=>Math.Abs((int)e.X-x)>1 || Math.Abs((int)e.Y-y)>1)
                && Kernel.GamePool.Values.All(p=>!Q.Player(p,false) || p.Entity.MapID!=S.MapId || Math.Abs((int)p.Entity.X-x)>1 || Math.Abs((int)p.Entity.Y-y)>1);
        }
        private static void EnsureActor() {
            if(Active!=null || S.Clock()<nextSpawn)return;nextSpawn=S.Clock().AddSeconds(5);Map map;
            if(!Kernel.Maps.TryGetValue(S.MapId,out map) || !S.Site(map,S.SpawnX,S.SpawnY))return;
            var run=S.Acquire(map,Guid.NewGuid(),()=>enabled && Active==null);if(run==null)return;
            ushort x=run.X,y=run.Y;bool found=Cell(map,x,y);
            for(int r=1;r<=8 && !found;r++)for(int dx=-r;dx<=r && !found;dx++)for(int dy=-r;dy<=r && !found;dy++) {
                if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=r)continue;int tx=run.X+dx,ty=run.Y+dy;
                if(tx>=0 && ty>=0 && tx<=ushort.MaxValue && ty<=ushort.MaxValue && Cell(map,(ushort)tx,(ushort)ty)){x=(ushort)tx;y=(ushort)ty;found=true;}
            }
            if(!found)return;
            if(!S.Checkpoint(map,run,1,run.Hp,x,y,()=>Active==null && Cell(map,x,y)))return;
            var info=MonsterInformation.MonsterInformations[4171].Copy();info.ID=Family;info.Name="SnowBanshee";info.Boss=true;info.Guard=info.Reviver=false;
            info.Hitpoints=S.Health;info.Level=138;info.SpellID=0;info.AttackType=1;info.ViewRange=19;info.AttackRange=19;
            info.MinAttack=info.MaxAttack=10000;info.Defence=40000;info.AttackSpeed=1500;info.RespawnTime=int.MaxValue/2;
            info.BoundX=x;info.BoundY=y;info.BoundCX=info.BoundCY=1;
            var body=new Entity(EntityFlag.Monster,false){UID=map.EntityUIDCounter.Next,MonsterInfo=info,MapID=S.MapId,X=x,Y=y,Name=info.Name,
                Body=951,Level=138,MinAttack=10000,MaxAttack=10000,MagicAttack=7500,Defence=40000,MagicDefence=200,MapObjType=MapObjectType.Monster};
            while(map.Entities.ContainsKey(body.UID) || map.Npcs.ContainsKey(body.UID) || map.Companions.ContainsKey(body.UID) || Kernel.GamePool.ContainsKey(body.UID))body.UID=map.EntityUIDCounter.Next;
            info.Owner=body;body.MaxHitpoints=S.Health;body.Hitpoints=run.Hp;body.ApplyMonsterBossHud();body.SendUpdates=true;
            var a=new Actor{Run=run,Map=map,Body=body,Sequence=1,Checkpoint=S.Clock().AddSeconds(5)};Active=a;
            try {
                map.AddEntity2(body);foreach(var p in Kernel.GamePool.Values)if(Q.Player(p,false) && p.Entity.MapID==S.MapId && Near(p.Entity,body))body.SendSpawn(p,false);
                Kernel.SendWorldMessage(new Message("Snow Banshee stirs in Frozen Grotto 2 near ("+x+","+y+"). Rally your allies!",System.Drawing.Color.White,Message.Center),Server.GamePool);
            }
            catch {Release(a);throw;}
        }
        internal static void Release(Actor a) {
            if(!Monitor.IsEntered(Gate))throw new InvalidOperationException("Snow Banshee cleanup requires lifecycle ownership.");
            if(a==null || a.Released)return;a.Released=true;if(ReferenceEquals(Active,a))Active=null;
            Entity body;if(a.Map.Entities.TryGetValue(a.Body.UID,out body) && ReferenceEquals(body,a.Body))a.Map.RemoveEntity(a.Body);
            a.Body.MonsterInfo.SendScreen(new Data(true){UID=a.Body.UID,ID=Data.RemoveEntity});SnowBansheeCombat.Clear(a);
        }
        private static uint[] Party(Actor a,GameState p) {
            GameState member;
            var members=p.Team==null?new[]{p}:p.Team.Teammates.Concat(new[]{p}).ToArray();
            return members.Where(m=>Eligible(a,m,false) && (ReferenceEquals(m,p) || ReferenceEquals(m.Team,p.Team)
                && p.Team.TryGetMember(m.Entity.UID,out member) && ReferenceEquals(member,m)))
                .Select(m=>m.Entity.UID).Distinct().OrderBy(uid=>uid).Take(5).ToArray();
        }
        internal static bool TryDie(Entity body,Entity killer) {
            if(!IsEncounter(body))return false;var p=killer==null?null:killer.Owner;
            if(p==null || p.ItemSyncRoot==null || !Monitor.TryEnter(p.ItemSyncRoot))return true;
            try {string spirit=null;lock(Gate) {
                var a=Active;Func<bool> proof=()=>a!=null && ReferenceEquals(a.Body,body) && Current(a) && ReferenceEquals(Fighter(a,killer),p);
                if(!proof())return true;var party=Party(a,p);
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    SkillTable.LockOwner(c,t,p);
                    if(!S.Victory(c,t,a.Map,a.Run,p.Entity.UID,party,body.X,body.Y,proof))return true;
                    SnowBansheeRewards.Record(c,t,a.Run.Id,p,party);spirit=DailySoulBall.RecordSnowBanshee(c,t,p,a);
                    ProgressionEvidence.Complete(c,t,p.Entity.UID,10527,"snow-banshee-kill-v1",a.Run.Id.ToString("N"),1);
                    EquipmentAchievements.RecordUnarmedVictory(c,t,p,body,killer,"snow:"+a.Run.Id.ToString("N"));
                    if(!proof() || !party.SequenceEqual(Party(a,p)))throw new InvalidOperationException("Snow Banshee victory participants changed.");t.Commit();
                }}
                Release(a);
            }
                body.DieCore(p.Entity);Q.Refresh(p);SnowBansheeRewards.Tick(true);if(spirit!=null)p.Send(spirit);p.Send("The Banshee falls. Claim the spoils before they fade.");
            }catch(Exception ex){Q.Fail(p,"snow-banshee-victory",ex);}finally{Monitor.Exit(p.ItemSyncRoot);}return true;
        }
        internal static void Tick() {
            if(!enabled || !Monitor.TryEnter(Gate))return;
            try {
                var a=Active;
                if(a!=null) {
                    if(!Current(a))Release(a);
                    else {
                        if(S.Clock()>=a.Checkpoint) {
                            if(!S.Checkpoint(a.Map,a.Run,++a.Sequence,a.Body.Hitpoints,a.Body.X,a.Body.Y,()=>Current(a))){Release(a);return;}
                            a.Checkpoint=S.Clock().AddSeconds(5);
                        }
                        SnowBansheeCombat.Tick(a);
                    }
                }
                EnsureActor();
            }catch(Exception ex){if(Active!=null)Release(Active);Console.WriteLine("[snow-banshee] lifecycle error="+ex.GetType().Name);}
            finally{Monitor.Exit(Gate);}
            SnowBansheeRewards.Tick();
        }
    }
}
