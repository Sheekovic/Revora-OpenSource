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
using B=Revora.Game.Features.EliminationRescueBindings;

namespace Revora.Game.Features
{
    // Staged rescue lifecycle; normal startup remains disabled until quest completion.
    internal static class EliminationRescueWorld
    {
        internal sealed class Hunt {
            internal GameState Player;internal Map Map;internal Entity Body;
            internal B.Binding Binding;internal DateTime Until;internal bool Released,Won;
            internal NpcSpawn Landy;internal DateTime TalkUntil;
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
                h.Won=true;body.DieCore(h.Player.Entity);h.Map.RemoveEntity(body);PublishLandy(h);
                h.Player.Send("The Rock Behemoth falls. Landy is free.");
            }}catch(Exception ex){Q.Fail(h.Player,"elimination-rescue-victory",ex);}
            finally{Monitor.Exit(h.Player.ItemSyncRoot);}return true;
        }
        internal static void Initialize() {
            if(Hunts.Count!=0)throw new InvalidOperationException("Elimination rescue scenes are active.");
            using(var sha=SHA256.Create())using(var f=File.OpenRead(Path.Combine(Constants.DMapsPath,"maps","1011.map")))
                if(System.BitConverter.ToString(sha.ComputeHash(f)).Replace("-","")!="7E8F7A8262F8AE93A314699AF6D276F0324FFF6C32BCC6061BFFC2F336F1E817")
                    throw new InvalidOperationException("Elimination forest terrain changed.");
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
        internal static Hunt Start(GameState p,uint spice) {
            if(!enabled || !Q.Player(p) || !Monitor.IsEntered(p.ItemSyncRoot))throw new InvalidOperationException("Elimination scene requires its owner lock.");
            lock(gate) {
                if(Hunts.Count>=16 || Hunts.Values.Any(h=>h.Player.Entity.UID==p.Entity.UID))return null;
                var hunt=new Hunt{Player=p};
                try {
                    hunt.Map=Map.CreateDynamic(1011,"maps\\1011.map",false);hunt.Map.Portals.Clear();
                    MapsTable.MapInformations.Add(hunt.Map.ID,new MapsTable.MapInformation{ID=hunt.Map.ID,BaseID=1011,Owner=p.Entity.UID,Status=8192});
                    if(!hunt.Map.Floor[309,256,MapObjectType.Player] || !hunt.Map.Floor[311,256,MapObjectType.Monster])throw new InvalidOperationException("Elimination landing blocked.");
                    var info=B.Profile();info.BoundX=311;info.BoundY=256;info.BoundCX=info.BoundCY=1;
                    hunt.Body=new Entity(EntityFlag.Monster,false){UID=hunt.Map.EntityUIDCounter.Next,MapID=hunt.Map.ID,X=311,Y=256,
                        MonsterInfo=info,MapObjType=MapObjectType.Monster,Name=info.Name,Body=info.Mesh,Level=info.Level,
                        MinAttack=info.MinAttack,MaxAttack=info.MaxAttack,MagicAttack=info.MaxAttack,Defence=info.Defence};
                    info.Owner=hunt.Body;hunt.Body.MaxHitpoints=hunt.Body.Hitpoints=info.Hitpoints;hunt.Body.SendUpdates=true;hunt.Map.AddEntity2(hunt.Body);
                    S.Change admission;
                    using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                        admission=spice==0?S.ResumeRescue(c,t,p):S.Summon(c,t,p,spice);
                        if(admission==null){Dispose(hunt);return null;}
                        hunt.Until=admission.RescueUntil;hunt.Won=admission.RescueWon;
                        if(hunt.Won){hunt.Map.RemoveEntity(hunt.Body);hunt.Body=null;PublishLandy(hunt);hunt.Binding=B.BindLandy(c,t,p,hunt.Map);}
                        else hunt.Binding=B.Bind(c,t,p,hunt.Map,hunt.Body);
                        if(hunt.Binding==null || !Hunts.TryAdd(hunt.Map.ID,hunt))throw new InvalidOperationException("Elimination scene binding failed.");
                        t.Commit();
                    }}
                    S.Apply(p,admission);Travel(p,hunt.Map,309,256,hunt);if(hunt.Body!=null)hunt.Body.MonsterInfo.InSight=p.Entity.UID;return hunt;
                }catch {
                    if(hunt.Map!=null) {
                        // The paid receipt survives uncertain publication/travel.
                        // Return before releasing a map that a session may occupy.
                        if(p.Entity.MapID==hunt.Map.ID && Q.Player(p,false))Travel(p,Kernel.Maps[1011],309,256);
                        Dispose(hunt);
                    }
                    throw;
                }
            }
        }
        private static void PublishLandy(Hunt h) {
            if(h.Map.Npcs.ContainsKey(EliminationRules.Landy))throw new InvalidOperationException("Landy actor identity collision.");
            // Installed Cherry profile 469 is a documented Revora appearance
            // adaptation; no claim that it is the original retail Landy model.
            h.Landy=new NpcSpawn{UID=EliminationRules.Landy,Name="Landy",MapID=h.Map.ID,X=311,Y=256,Mesh=4690,Type=Enums.NpcType.Talker};
            h.Map.AddNpc(h.Landy);
            if(h.Player.Entity.MapID==h.Map.ID)h.Landy.SendSpawn(h.Player,true);
        }
        internal static bool HandleNpc(GameState p,NpcRequest request) {
            if(p==null || p.ActiveNpc!=EliminationRules.Landy)return false;
            if(!enabled || request==null || p.ItemSyncRoot==null)return true;
            if(request.NpcID!=0 && request.NpcID!=EliminationRules.Landy)return true;
            lock(p.ItemSyncRoot)try {lock(gate) {
                Hunt h;Revora.Interfaces.INpc npc;
                if(!Q.Player(p) || !Hunts.TryGetValue(p.Entity.MapID,out h) || !Registered(h) || !ReferenceEquals(h.Player,p)
                    || !h.Won || h.Landy==null || !ReferenceEquals(p.Map,h.Map) || !h.Map.Npcs.TryGetValue(EliminationRules.Landy,out npc) || !ReferenceEquals(npc,h.Landy)
                    || Kernel.GetDistance(p.Entity.X,p.Entity.Y,npc.X,npc.Y)>6 || S.Clock()>=h.Until
                    || p.Trade==null || p.Trade.InTrade || p.Booth!=null || p.Entity.PokerTable!=0)return true;
                if(request.OptionID==0) {
                    h.TalkUntil=S.Clock().AddSeconds(60);var d=new Npcs(p);
                    d.Text("You found me... but its poison burns. Please tell Georgia what happened.");d.Option("I will get help.",1);d.Option("Wait here.",255);d.Send();return true;
                }
                if(request.OptionID!=1 || S.Clock()>=h.TalkUntil)return true;h.TalkUntil=DateTime.MinValue;
                bool saved;using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){saved=B.TalkLandy(c,t,p,h.Binding);t.Commit();}}
                if(saved){Leave(h);p.ActiveNpc=0;p.Send("Return to Georgia. Landy needs her help.");}
            }}catch(Exception ex){Q.Fail(p,"elimination-landy",ex);}return true;
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
                    Q.Execute(c,t,"UPDATE revora_elimination_rescue_bindings SET active=false WHERE run=@run AND generation=@generation AND map=@map",
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
