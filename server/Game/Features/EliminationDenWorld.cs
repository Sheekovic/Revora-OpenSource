using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using S=Revora.Game.Features.EliminationStore;
using B=Revora.Game.Features.EliminationDenBindings;

namespace Revora.Game.Features
{
    // Staged den lifecycle; normal startup remains disabled until quest completion.
    internal static class EliminationDenWorld
    {
        internal sealed class Hunt {
            internal GameState Player;internal Map Map;internal Entity Body;
            internal B.Binding Binding;internal DateTime Until;internal bool Released,Won;
            internal NpcSpawn Exit;internal DateTime TalkUntil;internal Guid Run;
        }
        private static bool enabled;
        private static readonly object gate=new object();
        internal static readonly ConcurrentDictionary<ushort,Hunt> Hunts=new ConcurrentDictionary<ushort,Hunt>();
        [ThreadStatic] private static Hunt transfer;
        internal static bool IsEncounter(Entity body){return body!=null && body.EntityFlag==EntityFlag.Monster && body.MonsterInfo!=null && body.MonsterInfo.ID==B.Family;}
        private static bool Current(Hunt h) {
            Entity body;
            return Registered(h) && !h.Won && Q.Player(h.Player) && EliminationRules.Eligible(h.Player.Entity.Level,h.Player.Entity.Reborn)
                && h.Player.Entity.MapID==h.Map.ID && ReferenceEquals(h.Player.Map,h.Map) && h.Player.Trade!=null && !h.Player.Trade.InTrade
                && h.Player.Booth==null && h.Player.Entity.PokerTable==0 && S.Clock()<h.Until && !h.Body.Dead
                && h.Map.Entities.TryGetValue(h.Body.UID,out body) && ReferenceEquals(body,h.Body) && ReferenceEquals(body.MonsterInfo.Owner,body);
        }
        private static bool Fighter(Hunt h,Entity fighter) {
            if(fighter==null || fighter.Dead || fighter.MapID!=h.Map.ID)return false;
            if(ReferenceEquals(fighter,h.Player.Entity))return true;
            Entity actual;return fighter.Companion && ReferenceEquals(fighter.Owner,h.Player) && ReferenceEquals(h.Player.Companion,fighter)
                && h.Map.Companions.TryGetValue(fighter.UID,out actual) && ReferenceEquals(actual,fighter);
        }
        internal static bool BlockAttack(Entity a,Entity b) {
            Entity body=IsEncounter(a)?a:IsEncounter(b)?b:null;
            if(body==null)return a!=null && Hunts.ContainsKey(a.MapID) || b!=null && Hunts.ContainsKey(b.MapID);
            Hunt h;return !enabled || !Hunts.TryGetValue(body.MapID,out h) || !ReferenceEquals(body,h.Body) || !Current(h)
                || !Fighter(h,ReferenceEquals(a,body)?b:a);
        }
        private sealed class Lease:IDisposable {private readonly object value;internal Lease(object value){this.value=value;}public void Dispose(){Monitor.Exit(value);}}
        internal static IDisposable DamageLease(Entity a,Entity b) {
            Entity body=IsEncounter(a)?a:IsEncounter(b)?b:null;Hunt h;
            if(body==null || !Hunts.TryGetValue(body.MapID,out h))return null;
            return Monitor.TryEnter(h.Player.ItemSyncRoot)?new Lease(h.Player.ItemSyncRoot):null;
        }
        internal static bool BlockDamage(Entity a,Entity b) {
            if(BlockAttack(a,b))return true;Entity body=IsEncounter(a)?a:IsEncounter(b)?b:null;Hunt h;
            return body!=null && (!Hunts.TryGetValue(body.MapID,out h) || !Monitor.IsEntered(h.Player.ItemSyncRoot));
        }
        internal static bool TryDie(Entity body,Entity killer) {
            if(!IsEncounter(body))return false;Hunt h;
            if(!enabled || !Hunts.TryGetValue(body.MapID,out h) || !ReferenceEquals(h.Body,body) || !Monitor.TryEnter(h.Player.ItemSyncRoot))return true;
            try{lock(gate){
                if(!Current(h) || !Fighter(h,killer) || Kernel.GetDistance(h.Player.Entity.X,h.Player.Entity.Y,body.X,body.Y)>Constants.pScreenDistance)return true;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    if(!B.Defeat(c,t,h.Player,h.Binding,body))return true;
                    if(!Current(h) || !Fighter(h,killer))throw new InvalidOperationException("Elimination combat context changed.");t.Commit();
                }}
                h.Won=h.Binding.Slot==6;body.DieCore(h.Player.Entity);h.Map.RemoveEntity(body);
                if(h.Won){Leave(h);h.Player.Send("Rock Leader has fallen. Return to Georgia.");}
                else using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){Spawn(h,c,t);t.Commit();}}
            }}catch(Exception ex){Q.Fail(h.Player,"elimination-den-victory",ex);}
            finally{Monitor.Exit(h.Player.ItemSyncRoot);}return true;
        }
        internal static bool MatchesDenIndex(string path) {
            if(path==null)return false;
            string normalized=path.Replace('/', '\\');
            // DMaps.Load retains the client index name; Map loads the reviewed
            // maps/1021.map cache by base ID before considering that source path.
            return string.Equals(normalized,"maps\\1021.map",StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized,"map\\map\\mine.dmap",StringComparison.OrdinalIgnoreCase);
        }
        internal static void Initialize() {
            if(Hunts.Count!=0)throw new InvalidOperationException("Elimination den scenes are active.");
            using(var sha=SHA256.Create())using(var f=File.OpenRead(Path.Combine(Constants.DMapsPath,"maps","1021.map")))
                if(System.BitConverter.ToString(sha.ComputeHash(f)).Replace("-","")!="608DC4F689D0EFF861BCAF5E48A0585714E9BD254C03209A04DD92872E3F19D2")
                    throw new InvalidOperationException("Elimination den terrain changed.");
            string path;if(!DMaps.MapPaths.TryGetValue(EliminationRules.DenBase,out path) || !MatchesDenIndex(path))
                throw new InvalidOperationException("Elimination den base mapping changed.");
            if(Kernel.Maps.Values.Any(map=>map.Npcs.ContainsKey(300034)))throw new InvalidOperationException("Elimination den exit ID collision.");
            S.Initialize();B.Initialize();enabled=true;
        }
        private static bool Registered(Hunt h) {
            Hunt actual;Map map;
            return h!=null && !h.Released && Hunts.TryGetValue(h.Map.ID,out actual) && ReferenceEquals(actual,h)
                && Kernel.Maps.TryGetValue(h.Map.ID,out map) && ReferenceEquals(map,h.Map);
        }
        private static void Travel(GameState p,Map map,ushort x,ushort y,Hunt permit=null) {
            transfer=permit;
            try{p.Entity.Teleport(map.BaseID,map.ID,x,y);
                if(p.Entity.MapID!=map.ID || p.Entity.X!=x || p.Entity.Y!=y || !EntityTable.SaveEntity(p))throw new InvalidOperationException("Elimination travel did not persist.");
            }finally{transfer=null;}
        }
        internal static bool AllowTeleport(GameState p,ushort destination,ushort x,ushort y) {
            Hunt h;if(!Hunts.TryGetValue(destination,out h))return true;
            if(!Registered(h) || !ReferenceEquals(h.Player,p) || !Q.Player(p,false) || S.Clock()>=h.Until || !h.Map.Floor[x,y,MapObjectType.Player])return false;
            if(ReferenceEquals(transfer,h))return true;
            if(p.Entity.MapID!=h.Map.ID || !ReferenceEquals(p.Map,h.Map))return false;
            long dx=(long)x-p.Entity.X,dy=(long)y-p.Entity.Y;return dx*dx+dy*dy<441;
        }
        private static void Spawn(Hunt h,Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t) {
            int slot=B.Next(c,t,h.Run);if(slot>6)throw new InvalidOperationException("Den already completed.");
            var site=EliminationRules.DenLackey(slot);ushort x=slot==6?EliminationRules.DenLeaderX:site.X,y=slot==6?EliminationRules.DenLeaderY:site.Y;
            if(!h.Map.Floor[x,y,MapObjectType.Monster])throw new InvalidOperationException("Den actor site blocked.");
            var info=B.Profile(slot);info.BoundX=x;info.BoundY=y;info.BoundCX=info.BoundCY=1;
            h.Body=new Entity(EntityFlag.Monster,false){UID=h.Map.EntityUIDCounter.Next,MapID=h.Map.ID,X=x,Y=y,
                MonsterInfo=info,MapObjType=MapObjectType.Monster,Name=info.Name,Body=info.Mesh,Level=info.Level,
                MinAttack=info.MinAttack,MaxAttack=info.MaxAttack,MagicAttack=info.MaxAttack,Defence=info.Defence};
            info.Owner=h.Body;h.Body.MaxHitpoints=h.Body.Hitpoints=info.Hitpoints;h.Body.SendUpdates=true;h.Map.AddEntity2(h.Body);
            h.Binding=B.Bind(c,t,h.Player,h.Map,h.Body,h.Run);if(h.Binding==null)throw new InvalidOperationException("Den actor binding failed.");
            info.InSight=h.Player.Entity.UID;
        }
        internal static Hunt Start(GameState p) {
            if(!enabled || !Q.Player(p) || !Monitor.IsEntered(p.ItemSyncRoot))throw new InvalidOperationException("Den owner lock required.");
            lock(gate) {
                if(Hunts.Count>=16 || Hunts.Values.Any(existing=>existing.Player.Entity.UID==p.Entity.UID))return null;
                var h=new Hunt{Player=p};
                try {
                    h.Map=Map.CreateDynamic(EliminationRules.DenBase,"maps\\1021.map",false);h.Map.Portals.Clear();
                    MapsTable.MapInformations.Add(h.Map.ID,new MapsTable.MapInformation{ID=h.Map.ID,BaseID=EliminationRules.DenBase,Owner=p.Entity.UID,Status=8192});
                    h.Exit=new NpcSpawn{UID=300034,Name="Georgia",MapID=h.Map.ID,X=EliminationRules.DenExitX,Y=EliminationRules.DenExitY,Mesh=4680,Type=Enums.NpcType.Talker};h.Map.AddNpc(h.Exit);
                    using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                        var admission=S.AdmitDen(c,t,p);if(admission==null){Dispose(h);return null;}
                        h.Run=admission.DenRun;h.Until=admission.DenUntil;Spawn(h,c,t);
                        if(!Hunts.TryAdd(h.Map.ID,h))throw new InvalidOperationException("Den registry collision.");t.Commit();
                    }}
                    Travel(p,h.Map,EliminationRules.DenLandingX,EliminationRules.DenLandingY,h);return h;
                }catch {
                    if(h.Map!=null){if(p.Entity.MapID==h.Map.ID && Q.Player(p,false))Travel(p,Kernel.Maps[1011],309,256);Dispose(h);}throw;
                }
            }
        }
        internal static bool HandleNpc(GameState p,NpcRequest request) {
            if(p==null || p.ActiveNpc!=300034)return false;
            if(!enabled || request==null || p.ItemSyncRoot==null || request.NpcID!=0 && request.NpcID!=300034)return true;
            lock(p.ItemSyncRoot)try {lock(gate){Hunt h;Revora.Interfaces.INpc actor;
                if(!Q.Player(p) || !Hunts.TryGetValue(p.Entity.MapID,out h) || !Registered(h) || !ReferenceEquals(h.Player,p) || !ReferenceEquals(p.Map,h.Map)
                    || !h.Map.Npcs.TryGetValue(300034,out actor) || !ReferenceEquals(actor,h.Exit)
                    || Kernel.GetDistance(p.Entity.X,p.Entity.Y,actor.X,actor.Y)>6 || S.Clock()>=h.Until
                    || p.Trade==null || p.Trade.InTrade || p.Booth!=null || p.Entity.PokerTable!=0)return true;
                if(request.OptionID==0){h.TalkUntil=S.Clock().AddSeconds(60);var d=new Npcs(p);d.Text("Need a moment outside? I will guide you back.");d.Option("Take me outside.",1);d.Option("I will stay.",255);d.Send();}
                else if(request.OptionID==1 && S.Clock()<h.TalkUntil){h.TalkUntil=DateTime.MinValue;Leave(h);p.ActiveNpc=0;}
            }}catch(Exception ex){Q.Fail(p,"elimination-den-exit",ex);}return true;
        }
        private static void Dispose(Hunt h) {
            h.Released=true;Hunt actual,removed;
            if(Hunts.TryGetValue(h.Map.ID,out actual) && ReferenceEquals(actual,h))Hunts.TryRemove(h.Map.ID,out removed);
            MapsTable.MapInformations.Remove(h.Map.ID);h.Map.Dispose();
        }
        internal static void Leave(Hunt h) {
            if(h==null || !Monitor.IsEntered(h.Player.ItemSyncRoot))throw new InvalidOperationException("Elimination return requires the owner lock.");
            lock(gate) {
                if(!Registered(h))return;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    int saved;
                    using(var q=Q.Command(c,t,"SELECT mapid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)h.Player.Entity.UID))
                        {var value=q.ExecuteScalar();if(value==null)throw new InvalidOperationException("Elimination returning owner missing.");saved=Convert.ToInt32(value);}
                    if(saved==h.Map.ID && Q.Execute(c,t,"UPDATE entities SET mapid=1011,x=309,y=256 WHERE uid=@uid AND mapid=@map",
                        "uid",(long)h.Player.Entity.UID,"map",(int)h.Map.ID)!=1)throw new InvalidOperationException("Elimination return was not saved.");
                    Q.Execute(c,t,"UPDATE revora_elimination_den_bindings SET active=false WHERE run=@run AND generation=@generation AND map=@map",
                        "run",h.Binding.Run,"generation",h.Binding.Generation,"map",(int)h.Map.ID);
                    t.Commit();
                }}
                GameState current;
                if(Kernel.GamePool.TryGetValue(h.Player.Entity.UID,out current) && Q.Player(current,false) && current.Entity.MapID==h.Map.ID) {
                    if(!Monitor.TryEnter(current.ItemSyncRoot))throw new InvalidOperationException("Elimination replacement session is busy.");
                    try{if(current.Entity.Dead)current.Entity.BringToLife();Travel(current,Kernel.Maps[1011],309,256);}
                    finally{Monitor.Exit(current.ItemSyncRoot);}
                }
                Dispose(h);
            }
        }
        internal static void Tick() {
            foreach(var h in Hunts.Values.ToArray()) {
                if(!Monitor.TryEnter(h.Player.ItemSyncRoot))continue;
                try {
                    if(!Q.Player(h.Player) || h.Player.Entity.MapID!=h.Map.ID || !ReferenceEquals(h.Player.Map,h.Map) || S.Clock()>=h.Until)Leave(h);
                    else if(Current(h))h.Body.MonsterInfo.InSight=h.Player.Entity.UID;
                }catch(Exception ex){Q.Fail(h.Player,"elimination-scene-return",ex);}
                finally{Monitor.Exit(h.Player.ItemSyncRoot);}
            }
        }
    }
}
