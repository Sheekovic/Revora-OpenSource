using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using S=Revora.Game.Features.AncientDevilStore;

namespace Revora.Game.Features
{
    // Runtime trial engine; initialization and admission remain staged until the
    // complete quest coordinator exists. These families have no legacy loot.
    internal static class AncientDevilWorld
    {
        internal const uint FamilyOffset=9909500;
        internal const int Capacity=16;
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal sealed class Hunt {
            internal GameState Player;internal S.Run Run;internal Guid Life;
            internal Map Map;internal Entity Body;internal DateTime Until;internal bool Released,Won,Persisted;
        }
        private static readonly object gate=new object();
        internal static readonly ConcurrentDictionary<ushort,Hunt> Hunts=new ConcurrentDictionary<ushort,Hunt>();
        [ThreadStatic] private static Hunt transfer;
        private static bool enabled;
        internal static void Initialize() {
            if(Hunts.Count!=0)throw new InvalidOperationException("Ancient trials are already active.");
            using(var sha=SHA256.Create())using(var f=File.OpenRead(Path.Combine(Constants.DMapsPath,"maps","1082.map")))
                if(System.BitConverter.ToString(sha.ComputeHash(f)).Replace("-","")!="B1974128A5DE1EFFF2BE6FA46482AA75AB46E6D4123E01332CE9117C2EF2574F")throw new InvalidOperationException("Ancient terrain changed.");
            for(uint family=7001;family<=7005;family++)Profile(family);
            S.Initialize();RecoverStartup();enabled=true;
        }
        // Startup-only recovery, before players are admitted. An unfinished life
        // is abandoned; an earned pending certificate is retained.
        private static void RecoverStartup() {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                var rows=new List<Tuple<uint,Guid,string,int,int>>();
                using(var q=Q.Command(c,t,@"SELECT r.uid,r.id,r.state,COALESCE(l.map,0),e.mapid FROM revora_ancient_trials r
JOIN entities e ON e.uid=r.uid LEFT JOIN revora_ancient_lives l ON l.trial=r.id
WHERE r.state IN('hunting','victory') ORDER BY r.uid FOR UPDATE OF e,r"))
                using(var r=q.ExecuteReader())while(r.Read())rows.Add(Tuple.Create(checked((uint)r.GetInt64(0)),r.GetGuid(1),r.GetString(2),r.GetInt32(3),Convert.ToInt32(r[4])));
                foreach(var row in rows) {
                    GameState current;if(Kernel.GamePool.TryGetValue(row.Item1,out current) && Q.Player(current,false))
                        throw new InvalidOperationException("Ancient startup recovery cannot run against admitted players.");
                    if(row.Item3=="hunting" && Q.Execute(c,t,"UPDATE revora_ancient_trials SET state='abandoned' WHERE id=@id AND state='hunting'","id",row.Item2)!=1)
                        throw new InvalidOperationException("Ancient startup abandonment failed.");
                    if(row.Item4!=0 && row.Item4==row.Item5 && Q.Execute(c,t,"UPDATE entities SET mapid=1052,x=216,y=208 WHERE uid=@uid AND mapid=@map",
                        "uid",(long)row.Item1,"map",row.Item4)!=1)throw new InvalidOperationException("Ancient startup return failed.");
                }
                t.Commit();
            }}
        }
        internal static MonsterInformation Profile(uint family) {
            if(family<7001 || family>7005)throw new ArgumentException("Unknown Ancient trial family.");
            string[] names={"WarriorDevil","TrojanDevil","ArcherDevil","FireDevil","WaterDevil"};
            uint[] health={23588,9436,33964,38550,30000};MonsterInformation original;
            if(!MonsterInformation.MonsterInformations.TryGetValue(family,out original) || original.Mesh!=205
                || original.Name!=names[family-7001] || original.Hitpoints!=health[family-7001])throw new InvalidOperationException("Ancient trial profile changed.");
            var info=original.Copy();info.ID=FamilyOffset+family;info.Boss=info.Guard=info.Reviver=false;
            info.Level=70;info.MinAttack=350;info.MaxAttack=550;info.Defence=100;info.SpellID=0;info.AttackType=1;
            info.AttackRange=2;info.ViewRange=18;info.AttackSpeed=1500;info.MoveSpeed=800;info.RunSpeed=500;
            info.RespawnTime=int.MaxValue/2;info.OwnItemID=info.OwnItemRate=0;return info;
        }
        internal static bool IsEncounter(Entity body) {
            return body!=null && body.EntityFlag==EntityFlag.Monster && body.MonsterInfo!=null
                && body.MonsterInfo.ID>=FamilyOffset+7001 && body.MonsterInfo.ID<=FamilyOffset+7005;
        }
        private static bool Registered(Hunt h) {
            Hunt current;Map map;
            return h!=null && !h.Released && Hunts.TryGetValue(h.Map.ID,out current) && ReferenceEquals(current,h)
                && Kernel.Maps.TryGetValue(h.Map.ID,out map) && ReferenceEquals(map,h.Map);
        }
        private static bool Owner(Hunt h,bool alive=true) {
            return Registered(h) && Q.Player(h.Player,alive) && ReferenceEquals(h.Player.Entity.Owner,h.Player)
                && h.Player.Entity.MapID==h.Map.ID && ReferenceEquals(h.Player.Map,h.Map)
                && h.Player.Trade!=null && !h.Player.Trade.InTrade && h.Player.Booth==null && h.Player.Entity.PokerTable==0;
        }
        private static bool Current(Hunt h) {
            Entity body;AncientDevilRules.Trial rule;
            return Owner(h) && h.Map.BaseID==1082 && !h.Won && Clock()<h.Until && !h.Body.Dead
                && h.Body.MonsterInfo.ID==FamilyOffset+h.Run.Family
                && AncientDevilRules.TryTrial(h.Player.Entity.Class,h.Player.Entity.Level,h.Run.Guard,out rule)
                && rule.Certificate==h.Run.Certificate && h.Map.Entities.TryGetValue(h.Body.UID,out body) && ReferenceEquals(body,h.Body)
                && ReferenceEquals(h.Body.MonsterInfo.Owner,h.Body);
        }
        private static bool Fighter(Hunt h,Entity fighter) {
            if(fighter==null || fighter.Dead || fighter.MapID!=h.Map.ID)return false;
            if(ReferenceEquals(fighter,h.Player.Entity))return true;
            Entity actual;
            return fighter.Companion && ReferenceEquals(fighter.Owner,h.Player) && ReferenceEquals(h.Player.Companion,fighter)
                && h.Map.Companions.TryGetValue(fighter.UID,out actual) && ReferenceEquals(actual,fighter);
        }
        internal static bool BlockAttack(Entity attacker,Entity target) {
            Entity body=IsEncounter(attacker)?attacker:IsEncounter(target)?target:null;
            if(body==null)return attacker!=null && Hunts.ContainsKey(attacker.MapID) || target!=null && Hunts.ContainsKey(target.MapID);
            Hunt h;
            return !enabled || !Hunts.TryGetValue(body.MapID,out h) || !ReferenceEquals(h.Body,body) || !Current(h)
                || !Fighter(h,ReferenceEquals(attacker,body)?target:attacker);
        }
        private sealed class Lease:IDisposable {private readonly object value;internal Lease(object value){this.value=value;}public void Dispose(){Monitor.Exit(value);}}
        internal static IDisposable DamageLease(Entity attacker,Entity target) {
            Entity body=IsEncounter(attacker)?attacker:IsEncounter(target)?target:null;Hunt h;
            if(body==null || !Hunts.TryGetValue(body.MapID,out h))return null;
            return Monitor.TryEnter(h.Player.ItemSyncRoot)?new Lease(h.Player.ItemSyncRoot):null;
        }
        internal static bool BlockDamage(Entity attacker,Entity target) {
            if(BlockAttack(attacker,target))return true;
            Entity body=IsEncounter(attacker)?attacker:IsEncounter(target)?target:null;Hunt h;
            return body!=null && (!Hunts.TryGetValue(body.MapID,out h) || !Monitor.IsEntered(h.Player.ItemSyncRoot));
        }
        private static void Travel(GameState p,Map map,ushort x,ushort y,Hunt permit=null) {
            transfer=permit;
            try {
                p.Entity.Teleport(map.BaseID,map.ID,x,y);
                if(p.Entity.MapID!=map.ID || p.Entity.X!=x || p.Entity.Y!=y || !EntityTable.SaveEntity(p))throw new InvalidOperationException("Ancient trial travel did not persist.");
            }finally{transfer=null;}
        }
        internal static bool AllowTeleport(GameState p,ushort destination,ushort x,ushort y) {
            Hunt h;if(!Hunts.TryGetValue(destination,out h))return true;
            if(!Registered(h) || !ReferenceEquals(h.Player,p) || !Q.Player(p,false) || Clock()>=h.Until)return false;
            if(ReferenceEquals(transfer,h))return h.Map.Floor[x,y,MapObjectType.Player];
            if(!Owner(h,false) || !h.Map.Floor[x,y,MapObjectType.Player])return false;
            long dx=(long)x-p.Entity.X,dy=(long)y-p.Entity.Y;return dx*dx+dy*dy<441;
        }
        internal static Hunt Start(GameState p,uint guard) {
            if(!enabled || !Q.Player(p) || !Monitor.IsEntered(p.ItemSyncRoot))throw new InvalidOperationException("Ancient admission requires its live owner lock.");
            lock(gate) {
                if(Hunts.Count>=Capacity || Hunts.Values.Any(existing=>existing.Player.Entity.UID==p.Entity.UID))return null;
                var h=new Hunt{Player=p,Until=Clock().AddMinutes(20)};
                try {
                    using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                        h.Run=S.Begin(c,t,p,guard);if(h.Run.State!="hunting")throw new InvalidOperationException("Claim and exchange the outstanding certificate first.");
                        // No live scene exists for this owner. A persisted old life
                        // cannot be rebound after an uncertain commit or lost scene.
                        using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_ancient_lives WHERE trial=@id)","id",h.Run.Id))
                            if(Equals(q.ExecuteScalar(),true)){S.Abandon(c,t,p,h.Run.Id);h.Run=S.Begin(c,t,p,guard);}
                        h.Map=Map.CreateDynamic(1082,"maps\\1082.map",false);
                        if(h.Map.Entities.Count!=0 || h.Map.Npcs.Count!=0)throw new InvalidOperationException("Ancient private map is occupied.");
                        h.Map.Portals.Clear();h.Map.AddNpc(AncientDevilQuest.ReturnNpc(h.Map.ID));MapsTable.MapInformations.Add(h.Map.ID,new MapsTable.MapInformation{ID=h.Map.ID,BaseID=1082,Owner=p.Entity.UID,Status=8192});
                        if(!h.Map.Floor[216,208,MapObjectType.Player] || !h.Map.Floor[220,208,MapObjectType.Monster])throw new InvalidOperationException("Ancient trial landings blocked.");
                        var info=Profile(h.Run.Family);info.BoundX=220;info.BoundY=208;info.BoundCX=info.BoundCY=1;
                        var body=new Entity(EntityFlag.Monster,false){UID=h.Map.EntityUIDCounter.Next,MapID=h.Map.ID,X=220,Y=208,
                            MonsterInfo=info,MapObjType=MapObjectType.Monster,Name=info.Name,Body=info.Mesh,Level=info.Level,
                            MinAttack=info.MinAttack,MaxAttack=info.MaxAttack,MagicAttack=info.MaxAttack,Defence=info.Defence};
                        info.Owner=body;body.MaxHitpoints=body.Hitpoints=info.Hitpoints;body.SendUpdates=true;h.Body=body;
                        h.Life=S.Bind(c,t,p,h.Run.Id,h.Map.ID,body.UID);
                        if(!Hunts.TryAdd(h.Map.ID,h))throw new InvalidOperationException("Ancient scene identity collision.");
                        t.Commit();h.Persisted=true;
                    }}
                    h.Map.AddEntity2(h.Body);Travel(p,h.Map,216,208,h);h.Body.MonsterInfo.InSight=p.Entity.UID;
                    return h;
                }catch {
                    if(h.Map!=null){
                        if(h.Persisted)Cancel(h);
                        if(p.Entity.MapID==h.Map.ID && Q.Player(p,false))Travel(p,Kernel.Maps[1052],216,208);
                        Hunt actual,ignored;if(Hunts.TryGetValue(h.Map.ID,out actual) && ReferenceEquals(actual,h))Hunts.TryRemove(h.Map.ID,out ignored);
                        MapsTable.MapInformations.Remove(h.Map.ID);h.Map.Dispose();
                    }
                    throw;
                }
            }
        }
        private static void Cancel(Hunt h) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                int savedMap;
                using(var q=Q.Command(c,t,"SELECT mapid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)h.Player.Entity.UID)) {
                    var value=q.ExecuteScalar();if(value==null)throw new InvalidOperationException("Ancient owner missing during cleanup.");savedMap=Convert.ToInt32(value);
                }
                var run=S.Read(c,t,h.Player.Entity.UID,h.Run.Id);
                if(run==null)throw new InvalidOperationException("Ancient trial missing during cleanup.");
                if(run.State=="hunting" && Q.Execute(c,t,"UPDATE revora_ancient_trials SET state='abandoned' WHERE id=@id AND uid=@uid AND state='hunting'",
                    "id",h.Run.Id,"uid",(long)h.Player.Entity.UID)!=1)throw new InvalidOperationException("Ancient abandonment was not saved.");
                if(savedMap==h.Map.ID && Q.Execute(c,t,"UPDATE entities SET mapid=1052,x=216,y=208 WHERE uid=@uid AND mapid=@map",
                    "uid",(long)h.Player.Entity.UID,"map",savedMap)!=1)throw new InvalidOperationException("Ancient return was not saved.");
                t.Commit();
            }}
        }
        internal static void Leave(Hunt h) {
            if(h==null || !Monitor.IsEntered(h.Player.ItemSyncRoot))throw new InvalidOperationException("Ancient cleanup requires its owner lock.");
            lock(gate){if(!Registered(h))return;Cancel(h);
                GameState traveler;
                if(Kernel.GamePool.TryGetValue(h.Player.Entity.UID,out traveler) && Q.Player(traveler,false) && traveler.Entity.MapID==h.Map.ID) {
                    // A replacement session has a different item lock. Never wait
                    // for it while holding the retiring scene's owner/gate locks.
                    if(!Monitor.TryEnter(traveler.ItemSyncRoot))throw new InvalidOperationException("Ancient returning session is busy.");
                    try{if(Q.Player(traveler,false) && traveler.Entity.MapID==h.Map.ID){
                        if(traveler.Entity.Dead)traveler.Entity.BringToLife();Travel(traveler,Kernel.Maps[1052],216,208);
                    }}finally{Monitor.Exit(traveler.ItemSyncRoot);}
                }
                h.Released=true;Entity current;
                if(h.Map.Entities.TryGetValue(h.Body.UID,out current) && ReferenceEquals(current,h.Body))h.Map.RemoveEntity(h.Body);
                Hunt ignored;Hunts.TryRemove(h.Map.ID,out ignored);MapsTable.MapInformations.Remove(h.Map.ID);h.Map.Dispose();
            }
        }
        internal static void Tick() {
            foreach(var h in Hunts.Values.ToArray()) {
                if(!Monitor.TryEnter(h.Player.ItemSyncRoot))continue;
                try{lock(gate){if(!Registered(h))continue;
                    if(h.Won || !Current(h)){Leave(h);continue;}h.Body.MonsterInfo.InSight=h.Player.Entity.UID;
                }}catch(Exception ex){Q.Fail(h.Player,"ancient-trial-cleanup",ex);}
                finally{Monitor.Exit(h.Player.ItemSyncRoot);}
            }
        }
        internal static bool TryDie(Entity body,Entity killer) {
            if(!IsEncounter(body))return false;Hunt h;
            if(!enabled || !Hunts.TryGetValue(body.MapID,out h) || !ReferenceEquals(h.Body,body) || !Monitor.TryEnter(h.Player.ItemSyncRoot))return true;
            var p=h.Player;
            try{lock(gate){
                if(!Current(h) || !Fighter(h,killer) || Kernel.GetDistance(p.Entity.X,p.Entity.Y,body.X,body.Y)>Constants.pScreenDistance)return true;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    if(!S.Defeat(c,t,p,h.Run.Id,h.Life,h.Run.Family,body.UID))return true;
                    if(!Current(h) || !Fighter(h,killer))throw new InvalidOperationException("Ancient combat context changed.");t.Commit();
                }}
                h.Won=true;body.DieCore(p.Entity);h.Map.RemoveEntity(body);
                p.Send("The trial is won. Your guard holds your certificate.");
            }}catch(Exception ex){Q.Fail(p,"ancient-trial-victory",ex);}
            finally{Monitor.Exit(p.ItemSyncRoot);}return true;
        }
    }
}
