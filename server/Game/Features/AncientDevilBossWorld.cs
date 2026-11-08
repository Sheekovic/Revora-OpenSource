using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using B=Revora.Game.Features.AncientDevilBattles;
using P=Revora.Game.Features.AncientDevilSummons;

namespace Revora.Game.Features
{
    // Staged guarded encounter. No production startup until the full quest is wired.
    internal static class AncientDevilBossWorld
    {
        internal const uint FamilyOffset=9916600;
        internal const int Capacity=16;
        internal sealed class Hunt {
            internal GameState Player;internal P.Run Run;internal B.Battle Battle;
            internal Map Map;internal Entity[] Bodies;internal DateTime Until;
            internal bool Released,Won,Persisted;
        }
        private static readonly object gate=new object();
        internal static readonly ConcurrentDictionary<ushort,Hunt> Hunts=new ConcurrentDictionary<ushort,Hunt>();
        [ThreadStatic] private static Hunt transfer;
        private static bool enabled;
        private static DateTime Clock(){return P.Clock();}
        internal static void Initialize() {
            if(Hunts.Count!=0)throw new InvalidOperationException("Ancient battles are already active.");
            for(int slot=0;slot<5;slot++)Profile(slot);
            P.Initialize();B.Initialize();B.RecoverStartup();enabled=true;
        }
        internal static MonsterInformation Profile(int slot) {
            if(slot<0 || slot>4)throw new ArgumentOutOfRangeException("slot");
            MonsterInformation original;
            if(!MonsterInformation.MonsterInformations.TryGetValue(7000,out original) || original.Name!="DevilGuard"
                || original.Mesh!=205 || original.Hitpoints!=6590)throw new InvalidOperationException("Ancient guard profile changed.");
            var info=original.Copy();
            info.ID=FamilyOffset+(uint)slot+1;
            info.Boss=info.Guard=info.Reviver=false;info.Level=100;
            info.MinAttack=700;info.MaxAttack=1000;info.Defence=200;info.SpellID=0;info.AttackType=1;
            info.AttackRange=2;info.ViewRange=18;info.AttackSpeed=1500;info.MoveSpeed=800;info.RunSpeed=500;
            info.RespawnTime=int.MaxValue/2;info.OwnItemID=info.OwnItemRate=0;
            if(slot==4) {
                // Revora combat policy. Mesh 205 is the installed AncientDevil
                // NPC motion model; monster presentation still needs native acceptance.
                info.Name="AncientDevil";info.Hitpoints=65535;info.Level=120;info.Boss=true;
                info.MinAttack=1000;info.MaxAttack=1500;info.Defence=300;
            }
            return info;
        }
        internal static bool IsEncounter(Entity body) {
            return body!=null && body.EntityFlag==EntityFlag.Monster && body.MonsterInfo!=null
                && body.MonsterInfo.ID>FamilyOffset && body.MonsterInfo.ID<=FamilyOffset+5;
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
            if(!Owner(h) || h.Won || Clock()>=h.Until || h.Map.BaseID!=1082)return false;
            for(int slot=0;slot<5;slot++) {
                if((h.Battle.Defeated&(1<<slot))!=0)continue;
                var body=h.Bodies[slot];Entity actual;
                if(body.Dead || body.MapID!=h.Map.ID || body.MonsterInfo.ID!=FamilyOffset+(uint)slot+1
                    || !ReferenceEquals(body.MonsterInfo.Owner,body)
                    || !h.Map.Entities.TryGetValue(body.UID,out actual) || !ReferenceEquals(actual,body))return false;
            }
            return true;
        }
        private static int Slot(Hunt h,Entity body) {
            if(h==null)return -1;
            for(int i=0;i<h.Bodies.Length;i++)if(ReferenceEquals(h.Bodies[i],body))return i;
            return -1;
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
            Hunt h;if(!enabled || !Hunts.TryGetValue(body.MapID,out h) || !Current(h))return true;
            int slot=Slot(h,body);
            return slot<0 || (h.Battle.Defeated&(1<<slot))!=0 || slot==4 && h.Battle.Defeated!=15
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
                if(p.Entity.MapID!=map.ID || p.Entity.X!=x || p.Entity.Y!=y || !EntityTable.SaveEntity(p))throw new InvalidOperationException("Ancient battle travel did not persist.");
            }finally{transfer=null;}
        }
        internal static bool AllowTeleport(GameState p,ushort destination,ushort x,ushort y) {
            Hunt h;if(!Hunts.TryGetValue(destination,out h))return true;
            if(!Registered(h) || !ReferenceEquals(h.Player,p) || !Q.Player(p,false) || Clock()>=h.Until)return false;
            if(ReferenceEquals(transfer,h))return h.Map.Floor[x,y,MapObjectType.Player];
            if(!Owner(h,false) || !h.Map.Floor[x,y,MapObjectType.Player])return false;
            long dx=(long)x-p.Entity.X,dy=(long)y-p.Entity.Y;return dx*dx+dy*dy<441;
        }
        internal static Hunt Start(GameState p,uint[] offering) {
            if(!enabled || !Q.Player(p) || !Monitor.IsEntered(p.ItemSyncRoot))throw new InvalidOperationException("Ancient battle admission requires its live owner lock.");
            lock(gate) {
                if(Hunts.Count>=Capacity || Hunts.Values.Any(hunt=>hunt.Player.Entity.UID==p.Entity.UID))return null;
                var h=new Hunt{Player=p};
                try {
                    h.Map=Map.CreateDynamic(1082,"maps\\1082.map",false);
                    if(h.Map.Entities.Count!=0 || h.Map.Npcs.Count!=0)throw new InvalidOperationException("Ancient battle map is occupied.");
                    h.Map.Portals.Clear();h.Map.AddNpc(AncientDevilQuest.ReturnNpc(h.Map.ID));
                    MapsTable.MapInformations.Add(h.Map.ID,new MapsTable.MapInformation{ID=h.Map.ID,BaseID=1082,Owner=p.Entity.UID,Status=8192});
                    if(!h.Map.Floor[216,208,MapObjectType.Player])throw new InvalidOperationException("Ancient battle landing blocked.");
                    h.Bodies=new Entity[5];
                    for(int slot=0;slot<5;slot++) {
                        ushort x=(ushort)(220+(slot%3)*2),y=(ushort)(208+(slot/3)*2);
                        if(!h.Map.Floor[x,y,MapObjectType.Monster])throw new InvalidOperationException("Ancient battle spawn blocked.");
                        var info=Profile(slot);info.BoundX=x;info.BoundY=y;info.BoundCX=info.BoundCY=1;
                        var actor=new Entity(EntityFlag.Monster,false){UID=h.Map.EntityUIDCounter.Next,MapID=h.Map.ID,X=x,Y=y,
                            MonsterInfo=info,MapObjType=MapObjectType.Monster,Name=info.Name,Body=info.Mesh,Level=info.Level,
                            MinAttack=info.MinAttack,MaxAttack=info.MaxAttack,MagicAttack=info.MaxAttack,Defence=info.Defence};
                        info.Owner=actor;actor.MaxHitpoints=actor.Hitpoints=info.Hitpoints;actor.ApplyMonsterBossHud();actor.SendUpdates=true;h.Bodies[slot]=actor;
                    }
                    using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                        h.Run=P.Reserve(c,t,p,offering);if(h.Run==null)throw new InvalidOperationException("Ancient paid battle unavailable.");
                        h.Battle=B.Bind(c,t,p,h.Run.Id,h.Map.ID,h.Bodies.Select(actor=>actor.UID).ToArray());h.Until=h.Battle.Until;
                        if(!Hunts.TryAdd(h.Map.ID,h))throw new InvalidOperationException("Ancient battle map identity collision.");
                        t.Commit();h.Persisted=true;
                    }}
                    Q.Install(p,h.Run.Removed,new ConquerItem[0]);
                    for(int slot=0;slot<5;slot++)if((h.Battle.Defeated&(1<<slot))==0)h.Map.AddEntity2(h.Bodies[slot]);
                    Travel(p,h.Map,216,208,h);return h;
                }catch(Exception ex) {
                    if(h.Persisted) {
                        Q.Fail(p,"ancient-battle-admission",ex);
                        // Keep registered ownership if cleanup fails; Tick retries it.
                        Leave(h);
                    }else if(h.Map!=null) {
                        Hunt removed;Hunts.TryRemove(h.Map.ID,out removed);MapsTable.MapInformations.Remove(h.Map.ID);h.Map.Dispose();
                    }
                    throw;
                }
            }
        }
        private static void ReturnSaved(Hunt h) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                int saved;
                using(var q=Q.Command(c,t,"SELECT mapid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)h.Player.Entity.UID)) {
                    var value=q.ExecuteScalar();if(value==null)throw new InvalidOperationException("Ancient summoner disappeared.");saved=Convert.ToInt32(value);
                }
                // No refund or deadline extension for leaving or technical loss.
                bool expire;
                using(var q=Q.Command(c,t,"SELECT state,until_at FROM revora_ancient_summons WHERE id=@id AND uid=@uid FOR UPDATE",
                    "id",h.Run.Id,"uid",(long)h.Player.Entity.UID))using(var r=q.ExecuteReader()) {
                    if(!r.Read())throw new InvalidOperationException("Ancient paid run disappeared.");
                    expire=r.GetString(0)=="pending" && Clock()>=r.GetFieldValue<DateTimeOffset>(1).UtcDateTime;
                }
                if(expire && Q.Execute(c,t,"UPDATE revora_ancient_summons SET state='expired' WHERE id=@id AND state='pending'",
                    "id",h.Run.Id)!=1)throw new InvalidOperationException("Ancient battle expiry was not saved.");
                if(saved==h.Map.ID && Q.Execute(c,t,"UPDATE entities SET mapid=1052,x=216,y=208 WHERE uid=@uid AND mapid=@map",
                    "uid",(long)h.Player.Entity.UID,"map",saved)!=1)throw new InvalidOperationException("Ancient battle return was not saved.");
                t.Commit();
            }}
        }
        internal static void Leave(Hunt h) {
            if(h==null || !Monitor.IsEntered(h.Player.ItemSyncRoot))throw new InvalidOperationException("Ancient battle cleanup requires its owner lock.");
            lock(gate){if(!Registered(h))return;ReturnSaved(h);
                GameState traveler;
                if(Kernel.GamePool.TryGetValue(h.Player.Entity.UID,out traveler) && Q.Player(traveler,false) && traveler.Entity.MapID==h.Map.ID) {
                    if(!Monitor.TryEnter(traveler.ItemSyncRoot))throw new InvalidOperationException("Ancient returning session is busy.");
                    try{if(Q.Player(traveler,false) && traveler.Entity.MapID==h.Map.ID){
                        if(traveler.Entity.Dead)traveler.Entity.BringToLife();Travel(traveler,Kernel.Maps[1052],216,208);
                    }}finally{Monitor.Exit(traveler.ItemSyncRoot);}
                }
                h.Released=true;
                foreach(var actor in h.Bodies){Entity actual;if(h.Map.Entities.TryGetValue(actor.UID,out actual) && ReferenceEquals(actor,actual))h.Map.RemoveEntity(actor);}
                Hunt removed;Hunts.TryRemove(h.Map.ID,out removed);MapsTable.MapInformations.Remove(h.Map.ID);h.Map.Dispose();
            }
        }
        internal static void Tick() {
            foreach(var h in Hunts.Values.ToArray()) {
                if(!Monitor.TryEnter(h.Player.ItemSyncRoot))continue;
                try{lock(gate){if(!Registered(h))continue;
                    if(!Current(h)){Leave(h);continue;}
                    for(int slot=0;slot<5;slot++)if((h.Battle.Defeated&(1<<slot))==0 && (slot!=4 || h.Battle.Defeated==15))h.Bodies[slot].MonsterInfo.InSight=h.Player.Entity.UID;
                }}catch(Exception ex){Q.Fail(h.Player,"ancient-battle-cleanup",ex);}
                finally{Monitor.Exit(h.Player.ItemSyncRoot);}
            }
        }
        internal static bool TryDie(Entity body,Entity killer) {
            if(!IsEncounter(body))return false;Hunt h;
            if(!enabled || !Hunts.TryGetValue(body.MapID,out h) || !Monitor.TryEnter(h.Player.ItemSyncRoot))return true;
            var p=h.Player;
            try{lock(gate){int slot=Slot(h,body);
                if(slot<0 || BlockAttack(killer,body) || Kernel.GetDistance(p.Entity.X,p.Entity.Y,body.X,body.Y)>Constants.pScreenDistance)return true;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    if(!B.Defeat(c,t,p,h.Run.Id,h.Battle.Generation,slot,body.UID))return true;
                    EquipmentAchievements.RecordUnarmedVictory(c,t,p,body,killer,"ancient:"+h.Run.Id.ToString("N")+":"+slot);
                    if(!Current(h) || !Fighter(h,killer))throw new InvalidOperationException("Ancient battle combat context changed.");t.Commit();
                }}
                h.Battle.Defeated|=1<<slot;h.Won=slot==4;body.DieCore(p.Entity);h.Map.RemoveEntity(body);Q.Refresh(p);
                if(h.Won)p.Send("The ancient evil has fallen. Return to the seal for your reward.");
            }}catch(Exception ex){Q.Fail(p,"ancient-battle-victory",ex);}
            finally{Monitor.Exit(p.ItemSyncRoot);}return true;
        }
    }
}
