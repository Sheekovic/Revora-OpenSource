using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Interfaces;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using L=Revora.Game.Features.LabyrinthItems;

namespace Revora.Game.Features
{
    internal static class LabyrinthQuest
    {
        internal static bool Enabled;
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static Func<int,int> Roll=n=>Kernel.Random.Next(n);
        private sealed class Visit {internal Guid Id;internal int Floor;}
        private sealed class Offer {internal GameState Player;internal int Kind;internal DateTime Until;}
        private sealed class Death {internal GameState Player;internal Guid Visit;}
        private static readonly ConcurrentDictionary<uint,Visit> active=new ConcurrentDictionary<uint,Visit>();
        private static readonly ConcurrentDictionary<uint,Offer> offers=new ConcurrentDictionary<uint,Offer>();
        private static readonly ConcurrentDictionary<uint,Death> deaths=new ConcurrentDictionary<uint,Death>();
        [ThreadStatic] private static HashSet<uint> transfers;
        private static readonly ushort[,] entries={{18,127},{27,227},{25,268},{5,290}};
        private static readonly uint[][] families={new[]{3141u,3142u,3143u},new[]{3144u,3145u,3146u},new[]{3147u,3148u,3149u},new[]{3155u,3156u}};
        internal static bool IsMap(ushort map) {return map>=1351 && map<=1354;}
        private static Visit Read(NpgsqlConnection c,NpgsqlTransaction t,uint uid) {
            using(var q=Q.Command(c,t,"SELECT id,floor FROM revora_lab_visits WHERE uid=@uid AND active FOR UPDATE","uid",(long)uid))
            using(var r=q.ExecuteReader())return r.Read()?new Visit{Id=r.GetGuid(0),Floor=r.GetInt32(1)}:null;
        }
        internal static void Initialize() {
            foreach(uint id in Enumerable.Range(721533,8).Select(n=>(uint)n).Concat(L.Gems).Concat(new[]{1088000u,1088001u}))
                if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Labyrinth item catalog missing.");
            foreach(uint id in families.SelectMany(a=>a))if(!MonsterInformation.MonsterInformations.ContainsKey(id))throw new InvalidOperationException("Labyrinth monster catalog missing.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_lab_visits(uid bigint PRIMARY KEY,id uuid UNIQUE NOT NULL,floor integer NOT NULL CHECK(floor BETWEEN 0 AND 3),active boolean NOT NULL);
CREATE TABLE IF NOT EXISTS revora_lab_entries(id uuid PRIMARY KEY,uid bigint NOT NULL,fee integer NOT NULL,created_at timestamptz NOT NULL);
CREATE TABLE IF NOT EXISTS revora_lab_steps(visit uuid NOT NULL,floor integer NOT NULL,token_uid bigint NOT NULL,PRIMARY KEY(visit,floor));
CREATE TABLE IF NOT EXISTS revora_lab_loot(id uuid PRIMARY KEY,uid bigint NOT NULL,visit uuid NOT NULL,family bigint NOT NULL,type integer NOT NULL,claimed boolean NOT NULL DEFAULT false);
CREATE INDEX IF NOT EXISTS revora_lab_loot_owner ON revora_lab_loot(uid) WHERE NOT claimed;
CREATE TABLE IF NOT EXISTS revora_lab_exchanges(id uuid PRIMARY KEY,uid bigint NOT NULL,kind integer NOT NULL,rewards bigint[] NOT NULL);
ALTER TABLE revora_lab_exchanges ADD COLUMN IF NOT EXISTS reward_version integer NOT NULL DEFAULT 0;
CREATE TABLE IF NOT EXISTS revora_lab_boxes(box_uid bigint PRIMARY KEY,uid bigint NOT NULL,roll integer NOT NULL,rewards bigint[] NOT NULL);");}
            active.Clear();offers.Clear();deaths.Clear();Enabled=true;
        }
        internal static void PlaceNpcs() {
            var actors=new List<NpcSpawn>();using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT id,lookface,mapid,cellx,celly FROM npcs WHERE id BETWEEN 1152 AND 1156"))
            using(var r=q.ExecuteReader())while(r.Read())actors.Add(new NpcSpawn{UID=Convert.ToUInt32(r[0]),Mesh=Convert.ToUInt16(r[1]),MapID=Convert.ToUInt16(r[2]),X=Convert.ToUInt16(r[3]),Y=Convert.ToUInt16(r[4]),Type=Enums.NpcType.Talker});}
            if(actors.Count!=5)throw new InvalidOperationException("Labyrinth NPC catalog missing.");
            foreach(var npc in actors) {
                foreach(var map in Kernel.Maps.Values){INpc old;if(map.Npcs.TryGetValue(npc.UID,out old))map.RemoveNpc(old);}
                if(npc.UID==1152){npc.MapID=1002;npc.X=209;npc.Y=374;npc.Name="Simon";}else {npc.Name=new[]{"EastGeneral","WestGeneral","SouthGeneral","NorthGeneral"}[npc.UID-1153];if(npc.MapID!=1351+npc.UID-1153)throw new InvalidOperationException("Labyrinth general map mismatch.");}
                var destination=Kernel.Maps[npc.MapID];ushort x=npc.X,y=npc.Y;if(!PotencyGuide.FindLanding(destination,ref x,ref y))throw new InvalidOperationException("Labyrinth NPC landing blocked.");npc.X=x;npc.Y=y;destination.AddNpc(npc);
            }
        }
        internal static bool AllowTeleport(GameState p,ushort map,ushort x,ushort y) {
            if(!Enabled || p==null || p.Entity==null || !IsMap(map) || transfers!=null && transfers.Contains(p.Entity.UID))return true;
            if(p.Entity.MapID==map && p.Map!=null && p.Map.Floor[x,y,MapObjectType.Player] && Kernel.GetDistance(p.Entity.X,p.Entity.Y,x,y)<=20)return true;
            p.Send("Enter through Simon and let each general guide you to the next floor.");return false;
        }
        private static void Travel(GameState p,int floor,bool heal=false) {
            ushort map=floor<0?(ushort)1002:(ushort)(1351+floor),x,y;
            if(floor<0){var npc=Kernel.Maps[1002].Npcs[1152];x=(ushort)(npc.X+2);y=(ushort)(npc.Y+2);}else{x=entries[floor,0];y=entries[floor,1];}
            if(!PotencyGuide.FindLanding(Kernel.Maps[map],ref x,ref y))throw new InvalidOperationException("Labyrinth transfer landing blocked.");
            if(transfers==null)transfers=new HashSet<uint>();transfers.Add(p.Entity.UID);
            try{p.Entity.Teleport(map,x,y);if(p.Entity.MapID!=map || p.Entity.X!=x || p.Entity.Y!=y)throw new InvalidOperationException("Labyrinth transfer failed.");if(heal)p.Entity.BringToLife();}
            finally{transfers.Remove(p.Entity.UID);}
        }
        private static string Begin(GameState p) {
            if(p.Entity.OnDeath!=null || p.InQualifier() || p.InTeamQualifier())return "Finish your contest first.";
            Visit visit;
            lock(p.VirtueSyncRoot) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))if(q.ExecuteScalar()==null)throw new InvalidOperationException("Labyrinth character missing.");
                visit=Read(c,t,p.Entity.UID);uint balance=p.VirtuePoints;bool paid=false;
                if(visit==null) {
                    if(balance<2000)return "Earn 2,000 Virtue Points by helping younger companions train, then return.";
                    visit=new Visit{Id=Guid.NewGuid(),Floor=0};balance-=2000;
                    VirtueBalance.Persist(c,t,p,balance);paid=true;
                    Q.Execute(c,t,"INSERT INTO revora_lab_entries(id,uid,fee,created_at) VALUES(@id,@uid,2000,@now)","id",visit.Id,"uid",(long)p.Entity.UID,"now",Clock());
                    Q.Execute(c,t,"INSERT INTO revora_lab_visits(uid,id,floor,active) VALUES(@uid,@id,0,true) ON CONFLICT(uid) DO UPDATE SET id=excluded.id,floor=0,active=true","uid",(long)p.Entity.UID,"id",visit.Id);
                }
                t.Commit();if(paid)VirtueBalance.ApplyCommitted(p,balance);
            }}
            }
            active[p.Entity.UID]=visit;Travel(p,visit.Floor);return "Seek the general at the end of each floor. Bring back seventeen matching diamonds for a prize.";
        }
        private static string Advance(GameState p,int floor) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                var visit=Read(c,t,p.Entity.UID);if(visit==null || visit.Floor!=floor || floor>=3)return "This passage does not belong to your current expedition.";
                var takes=L.Take(p,(uint)(721537+floor),1);if(takes==null)return "Bring the token carried by this floor's lesser spirit.";
                L.Write(c,t,p,takes);Q.Execute(c,t,"UPDATE revora_lab_visits SET floor=@floor WHERE uid=@uid AND id=@id","floor",floor+1,"uid",(long)p.Entity.UID,"id",visit.Id);
                Q.Execute(c,t,"INSERT INTO revora_lab_steps(visit,floor,token_uid) VALUES(@id,@floor,@token)","id",visit.Id,"floor",floor,"token",(long)takes[0].Item.UID);
                t.Commit();L.Install(p,takes);active[p.Entity.UID]=new Visit{Id=visit.Id,Floor=floor+1};Travel(p,floor+1);return "The way is open. Keep your guard up.";
            }}
        }
        private static void Close(GameState p,Guid expected,bool home,bool heal) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                var visit=Read(c,t,p.Entity.UID);if(expected!=Guid.Empty && (visit==null || visit.Id!=expected))return;
                if(visit!=null)Q.Execute(c,t,"UPDATE revora_lab_visits SET active=false WHERE uid=@uid AND id=@id","uid",(long)p.Entity.UID,"id",visit.Id);
                t.Commit();Visit ignored;active.TryRemove(p.Entity.UID,out ignored);if(home)Travel(p,-1,heal);
            }}
        }
        private static string Collect(GameState p) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                var ids=new List<Guid>();uint type=0;
                using(var q=Q.Command(c,t,"SELECT id,type FROM revora_lab_loot WHERE uid=@uid AND NOT claimed ORDER BY type,id LIMIT 10000 FOR UPDATE","uid",(long)p.Entity.UID))
                using(var r=q.ExecuteReader())while(r.Read()){uint next=(uint)r.GetInt32(1);if(type!=0 && type!=next)break;type=next;ids.Add(r.GetGuid(0));}
                if(ids.Count==0)return "Your satchel is empty. Defeat the creatures within to find diamonds and passage tokens.";
                var change=L.Give(p,type,ids.Count);if(change==null)return "Make room for a stack in your bag. Your satchel will wait.";
                L.Write(c,t,p,new[]{change});Q.Execute(c,t,"UPDATE revora_lab_loot SET claimed=true WHERE uid=@uid AND id=ANY(@ids) AND NOT claimed","uid",(long)p.Entity.UID,"ids",ids.ToArray());
                t.Commit();L.Install(p,new[]{change});return "Your "+ConquerItemInformation.BaseInformations[type].Name+" are ready. Ask again if more kinds remain in your satchel.";
            }}
        }
        internal static bool HandleNpc(GameState p,NpcRequest request) {
            if(!Enabled || p==null || p.Entity==null || request==null || p.ActiveNpc<1152 || p.ActiveNpc>1156)return false;
            if(p.ItemSyncRoot==null)return true;
            lock(p.ItemSyncRoot) {
                bool simon=p.ActiveNpc==1152;int floor=(int)p.ActiveNpc-1153;
                if(!Q.Near(p,p.ActiveNpc,simon?(ushort)1002:(ushort)(1351+floor)) || deaths.ContainsKey(p.Entity.UID))return true;
                try {
                    var d=new Npcs(p);int option=request.OptionID;
                    if(option==255){Offer ignored;offers.TryRemove(p.Entity.UID,out ignored);p.ActiveNpc=0;return true;}
                    if(simon && (option==1 || option>=10 && option<=13)) {
                        int kind=option==1?-1:option-10;offers[p.Entity.UID]=new Offer{Player=p,Kind=kind,Until=Clock().AddMinutes(2)};
                        d.Text(kind<0?"A new expedition costs 2,000 Virtue Points. An unfinished expedition may resume without another fee.":"Seventeen matching bound diamonds buy "+new[]{"two Meteors","four Meteors","one normal gem","one Ancestor Box and a bound P7 soul"}[kind]+".");d.Option("Agreed.",230);
                    }else if(simon && option==230) {
                        Offer offer;if(!offers.TryRemove(p.Entity.UID,out offer) || offer.Player!=p || offer.Until<=Clock())d.Text("Choose your journey or exchange again.");
                        else d.Text(offer.Kind<0?Begin(p):L.Exchange(p,offer.Kind));
                    }else if(option==3)d.Text(Collect(p));
                    else if(!simon && option==1 && floor<3)d.Text(Advance(p,floor));
                    else if(!simon && option==2){Close(p,Guid.Empty,true,false);d.Text("Simon will see you safely home.");}
                    else {
                        d.Text(simon?"The labyrinth keeps its treasures well. Seek diamonds within, and I will reward your courage.":"I guard the passage. Gather your spoils before deciding your next step.");
                        if(simon){d.Option("Enter or resume.",1);for(int i=0;i<4;i++)d.Option(new[]{"Sun Diamond prize.","Moon Diamond prize.","Star Diamond prize.","Ancestor Box."}[i],(byte)(10+i));}
                        else{if(floor<3)d.Option("Take me deeper.",1);d.Option("Return to Simon.",2);}
                        d.Option("Collect my satchel.",3);
                    }
                    d.Option("Farewell.",255);d.Send();
                }catch(Exception ex){Q.Fail(p,"labyrinth-npc",ex);}
            }return true;
        }
        internal static void OnLogin(GameState p) {
            if(!Enabled || !Q.Player(p,false))return;
            lock(p.ItemSyncRoot)try {
                Offer offer;offers.TryRemove(p.Entity.UID,out offer);Death death;deaths.TryRemove(p.Entity.UID,out death);
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    var visit=Read(c,t,p.Entity.UID);t.Commit();
                    if(IsMap(p.Entity.MapID) && visit!=null && !p.Entity.Dead){active[p.Entity.UID]=visit;Travel(p,visit.Floor);}
                    else if(IsMap(p.Entity.MapID))Close(p,Guid.Empty,true,true);
                    else {Visit ignored;active.TryRemove(p.Entity.UID,out ignored);} // A paid transfer fenced after commit may resume from Simon.
                }}Q.Refresh(p);
            }catch(Exception ex){Q.Fail(p,"labyrinth-login",ex);}
        }
        internal static void OnDeath(GameState p) {Visit visit;if(Enabled && Q.Player(p,false) && IsMap(p.Entity.MapID) && active.TryGetValue(p.Entity.UID,out visit))deaths[p.Entity.UID]=new Death{Player=p,Visit=visit.Id};}
        internal static void Tick() {
            if(!Enabled)return;
            foreach(var pair in deaths.ToArray()) {
                Death d;if(!deaths.TryRemove(pair.Key,out d) || !Q.Player(d.Player,false))continue;
                lock(d.Player.ItemSyncRoot)try{Close(d.Player,d.Visit,IsMap(d.Player.Entity.MapID),true);}catch(Exception ex){Q.Fail(d.Player,"labyrinth-death",ex);}
            }
            foreach(var pair in active.ToArray()) {
                GameState p;if(!Kernel.GamePool.TryGetValue(pair.Key,out p) || !Q.Player(p,false)){Visit ignored;active.TryRemove(pair.Key,out ignored);continue;}
                if(IsMap(p.Entity.MapID))continue;
                lock(p.ItemSyncRoot)try{if(Q.Player(p,false) && !IsMap(p.Entity.MapID))Close(p,pair.Value.Id,false,false);}catch(Exception ex){Q.Fail(p,"labyrinth-leave",ex);}
            }
        }
        internal static bool SuppressLegacyQuestDrop(Entity mob) {return Enabled && mob!=null && mob.MonsterInfo!=null && IsMap(mob.MapID) && families[mob.MapID-1351].Contains(mob.MonsterInfo.ID);}
        internal static bool TryDie(Entity mob,Entity killer) {
            if(!SuppressLegacyQuestDrop(mob) || mob.EntityFlag!=EntityFlag.Monster || mob.Companion)return false;
            Map map;Entity owner;if(!Kernel.Maps.TryGetValue(mob.MapID,out map) || !map.Entities.TryGetValue(mob.UID,out owner) || !ReferenceEquals(owner,mob))return false;
            Visit visit=null;if(killer!=null)active.TryGetValue(killer.UID,out visit);ushort x,y,where;uint family;bool died;
            bool durable=mob.MonsterInfo.Level>=120,evaluated=false;uint loot=0;Guid life=Guid.Empty;
            lock(mob) {
                if(mob.Dead)return true;x=mob.X;y=mob.Y;where=mob.MapID;family=mob.MonsterInfo.ID;
                if(durable) {
                    if(!System.Threading.Monitor.TryEnter(mob.MonsterLifeSyncRoot))return true;
                    try {
                        life=WildMonsterVictory.ProgressIdentity(mob);
                        WildMonsterVictory.DieWithProgress(mob,killer,(c,t)=>{
                            evaluated=true;loot=CreditSaved(c,t,mob,killer,visit,where,x,y,family,life);return loot!=0;
                        });
                    }finally{System.Threading.Monitor.Exit(mob.MonsterLifeSyncRoot);}
                }else mob.DieCore(killer);
                died=mob.Dead;
            }
            if(died) {
                if(!durable && visit!=null)Credit(killer,visit,where,x,y,family);
                else if(durable && killer!=null && Q.Player(killer.Owner))try {
                    // An accepted-life retry skips the callback. Recover only
                    // this player's exact saved satchel entry, never roll again.
                    if(!evaluated)using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,
                        "SELECT type FROM revora_lab_loot WHERE id=@id AND uid=@uid","id",life,"uid",(long)killer.UID)) {
                        var value=q.ExecuteScalar();if(value!=null)loot=Convert.ToUInt32(value);
                    }}
                    if(loot!=0)killer.Owner.Send("A "+ConquerItemInformation.BaseInformations[loot].Name+" was placed in your labyrinth satchel. A general or Simon can hand it over.");
                }catch(Exception ex){Q.Fail(killer.Owner,"labyrinth-loot-refresh",ex);}
            }
            return true;
        }
        private static uint CreditSaved(NpgsqlConnection c,NpgsqlTransaction t,Entity mob,Entity killer,Visit visit,ushort map,ushort x,ushort y,uint family,Guid life) {
            if(visit==null || killer==null || killer.Owner==null || !ReferenceEquals(killer.Owner.Entity,killer))return 0;
            var p=killer.Owner;
            if(!Q.Player(p) || p.Entity.MapID!=map || Kernel.GetDistance(p.Entity.X,p.Entity.Y,x,y)>Constants.pScreenDistance || deaths.ContainsKey(p.Entity.UID))return 0;
            int index=Array.IndexOf(families[map-1351],family);if(index<0 || index==2 || map==1354 && index==1)return 0;
            int chance=index==0?(map==1354?5:10):new[]{20,15,10}[map-1351];
            var current=Read(c,t,p.Entity.UID);if(current==null || current.Id!=visit.Id || current.Floor!=map-1351)return 0;
            if(WildMonsterVictory.RollProgress(mob,()=>Roll(100))>=chance)return 0;
            uint type=index==0?(uint)(721533+map-1351):(uint)(721537+map-1351);
            if(Q.Execute(c,t,"INSERT INTO revora_lab_loot(id,uid,visit,family,type) VALUES(@id,@uid,@visit,@family,@type)",
                "id",life,"uid",(long)p.Entity.UID,"visit",visit.Id,"family",(long)family,"type",(int)type)!=1)throw new InvalidOperationException("Labyrinth loot was not saved.");
            return type;
        }
        private static void Credit(Entity killer,Visit visit,ushort map,ushort x,ushort y,uint family) {
            if(killer==null || killer.EntityFlag!=EntityFlag.Player || killer.Owner==null || !ReferenceEquals(killer.Owner.Entity,killer) || !Q.Player(killer.Owner))return;
            var p=killer.Owner;lock(p.ItemSyncRoot)try {
                if(!Q.Player(p) || p.Entity.MapID!=map || Kernel.GetDistance(p.Entity.X,p.Entity.Y,x,y)>Constants.pScreenDistance || deaths.ContainsKey(p.Entity.UID))return;
                int index=Array.IndexOf(families[map-1351],family);if(index<0 || index==2 || map==1354 && index==1)return;
                int chance=index==0?(map==1354?5:10):new[]{20,15,10}[map-1351];uint type=index==0?(uint)(721533+map-1351):(uint)(721537+map-1351);
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    var current=Read(c,t,p.Entity.UID);if(current==null || current.Id!=visit.Id || current.Floor!=map-1351 || Roll(100)>=chance)return;
                    Q.Execute(c,t,"INSERT INTO revora_lab_loot(id,uid,visit,family,type) VALUES(@id,@uid,@visit,@family,@type)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"visit",visit.Id,"family",(long)family,"type",(int)type);
                    t.Commit();p.Send("A "+ConquerItemInformation.BaseInformations[type].Name+" was placed in your labyrinth satchel. A general or Simon can hand it over.");
                }}
            }catch(Exception ex){Q.Fail(p,"labyrinth-loot",ex);}
        }
    }
}
