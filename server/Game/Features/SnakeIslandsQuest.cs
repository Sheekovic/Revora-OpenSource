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

namespace Revora.Game.Features
{
    internal static class SnakeIslandsQuest
    {
        internal const ushort Island=1063,Bird=1015;
        internal const uint Ferry=30115,Exit=30125,Boss=3102;
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        [ThreadStatic] private static HashSet<uint> transfers;
        private sealed class Attempt {internal Guid Id;internal string Day;}
        private sealed class Death {internal GameState Player;internal Attempt Attempt;}
        private sealed class Run {internal Guid Attempt;internal string Day,State;internal bool Entered;internal DateTime Started;}
        private static readonly ConcurrentDictionary<uint,Attempt> active=new ConcurrentDictionary<uint,Attempt>();
        private static readonly ConcurrentDictionary<uint,Death> deaths=new ConcurrentDictionary<uint,Death>();
        private static bool enabled;private static DateTime nextScan;
        private static string Day() {return SummerHunt.Day(Clock());}
        private static Run Read(NpgsqlConnection c,NpgsqlTransaction t,uint uid,string day) {
            using(var q=Q.Command(c,t,"SELECT attempt,day,state,entered,started_at FROM revora_snake_hunts WHERE uid=@uid AND day=@day FOR UPDATE","uid",(long)uid,"day",day))
            using(var r=q.ExecuteReader())return r.Read()?new Run{Attempt=r.GetGuid(0),Day=r.GetString(1),State=r.GetString(2),Entered=r.GetBoolean(3),Started=r.GetFieldValue<DateTimeOffset>(4).UtcDateTime}:null;
        }
        internal static void Initialize() {
            ConquerItemBaseInformation rune;
            if(!ConquerItemInformation.BaseInformations.TryGetValue(4030101,out rune) || rune.Class!=0)
                throw new InvalidOperationException("Daily island yellow rune catalog missing or class restricted.");
            foreach(uint type in new[]{3100u,3101u,Boss,3103u})if(!MonsterInformation.MonsterInformations.ContainsKey(type))throw new InvalidOperationException("Snake Island monster catalog missing.");
            if(MonsterInformation.MonsterInformations[Boss].Level<120)throw new InvalidOperationException("Snake King level does not support the reviewed victory route.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_snake_hunts(
uid bigint NOT NULL CHECK(uid>0),day varchar(8) NOT NULL,attempt uuid UNIQUE NOT NULL,state varchar(16) NOT NULL CHECK(state IN ('hunting','victory','claimed')),
entered boolean NOT NULL,started_at timestamptz NOT NULL,boss_uid bigint NOT NULL DEFAULT 0,rewards bigint[] NOT NULL DEFAULT '{}',PRIMARY KEY(uid,day));
ALTER TABLE revora_snake_hunts ADD COLUMN IF NOT EXISTS valuable_reward boolean NOT NULL DEFAULT false;
ALTER TABLE revora_snake_hunts ADD COLUMN IF NOT EXISTS rune_reward boolean NOT NULL DEFAULT false;
CREATE TABLE IF NOT EXISTS revora_snake_attempts(id uuid PRIMARY KEY,uid bigint NOT NULL,day varchar(8) NOT NULL,started_at timestamptz NOT NULL,
ended_at timestamptz,reason varchar(16));");}
            active.Clear();deaths.Clear();nextScan=DateTime.MinValue;enabled=true;
        }
        internal static void PlaceNpcs() {
            var actors=new List<NpcSpawn>();using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT id,lookface,mapid,cellx,celly FROM npcs WHERE id IN (30115,30125)"))
            using(var r=q.ExecuteReader())while(r.Read())actors.Add(new NpcSpawn{UID=Convert.ToUInt32(r[0]),Mesh=Convert.ToUInt16(r[1]),MapID=Convert.ToUInt16(r[2]),X=Convert.ToUInt16(r[3]),Y=Convert.ToUInt16(r[4]),Name="GreenSnake",Type=Enums.NpcType.Talker});}
            if(actors.Count!=2)throw new InvalidOperationException("Snake Island ferryman catalog missing.");
            foreach(var npc in actors) {
                if(npc.MapID!=(npc.UID==Ferry?Bird:Island))throw new InvalidOperationException("Snake Island ferryman map differs from the reviewed route.");
                foreach(var map in Kernel.Maps.Values){INpc old;if(map.Npcs.TryGetValue(npc.UID,out old))map.RemoveNpc(old);}
                // The parked database position lies in blocked terrain. The
                // official guide's Bird Island position is valid on this map.
                if(npc.UID==Ferry){npc.X=468;npc.Y=203;}
                var destination=Kernel.Maps[npc.MapID];ushort x=npc.X,y=npc.Y;
                if(!PotencyGuide.FindLanding(destination,ref x,ref y))throw new InvalidOperationException("Snake Island ferryman landing blocked.");npc.X=x;npc.Y=y;destination.AddNpc(npc);
            }
            ushort ex=417,ey=367;if(!PotencyGuide.FindLanding(Kernel.Maps[Island],ref ex,ref ey))throw new InvalidOperationException("Snake Island far shore blocked.");
        }
        internal static bool AllowTeleport(GameState p,ushort destination,ushort x,ushort y) {
            if(!enabled || p==null || p.Entity==null || destination!=Island || transfers!=null && transfers.Contains(p.Entity.UID))return true;
            if(p.Entity.MapID==Island && p.Map!=null && p.Map.Floor[x,y,MapObjectType.Player]) {
                long dx=(long)x-p.Entity.X,dy=(long)y-p.Entity.Y;
                if(dx*dx+dy*dy<441)return true; // Same rule as a valid short jump; includes position corrections and combat movement.
            }
            p.Send("Green Snake's ferry reaches the islands. Cross the isles by jumping between safe landings.");return false;
        }
        private static void Travel(GameState p,bool enter,bool heal=false) {
            ushort map=enter?Island:Bird,x=417,y=367;
            if(!enter){var actor=Kernel.Maps[Bird].Npcs[Ferry];x=(ushort)(actor.X+2);y=(ushort)(actor.Y+2);}
            if(!PotencyGuide.FindLanding(Kernel.Maps[map],ref x,ref y))throw new InvalidOperationException("Snake ferry destination blocked.");
            if(transfers==null)transfers=new HashSet<uint>();transfers.Add(p.Entity.UID);
            try{p.Entity.Teleport(map,x,y);if(p.Entity.MapID!=map || p.Entity.X!=x || p.Entity.Y!=y)throw new InvalidOperationException("Snake ferry did not arrive.");
                if(heal){p.Entity.BringToLife();p.Entity.Mana=p.Entity.MaxMana;}}
            finally{transfers.Remove(p.Entity.UID);}
        }
        private static void EndAttempt(NpgsqlConnection c,NpgsqlTransaction t,Guid id,string reason) {
            Q.Execute(c,t,"UPDATE revora_snake_attempts SET ended_at=@now,reason=@reason WHERE id=@id AND ended_at IS NULL","now",Clock(),"reason",reason,"id",id);
        }
        private static string Begin(GameState p) {
            if(p.Entity.Level<90)return "Reach level 90 before facing the island snakes.";
            if(p.Entity.OnDeath!=null || p.InQualifier() || p.InTeamQualifier())return "Finish your current contest before boarding my ferry.";
            var now=Clock();string day=SummerHunt.Day(now);
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))if(q.ExecuteScalar()==null)throw new InvalidOperationException("Snake character missing.");
                var old=Read(c,t,p.Entity.UID,day);
                if(old!=null && old.State!="hunting")return "You have beaten the king for this quest day. Ask me for any unclaimed reward, then return tomorrow.";
                if(old!=null)EndAttempt(c,t,old.Attempt,"retry");
                // End any previous-day unfinished ferry admission as well.
                Q.Execute(c,t,"UPDATE revora_snake_attempts SET ended_at=@now,reason='new-day' WHERE uid=@uid AND ended_at IS NULL","now",now,"uid",(long)p.Entity.UID);
                Q.Execute(c,t,"UPDATE revora_snake_hunts SET entered=false WHERE uid=@uid AND entered","uid",(long)p.Entity.UID);
                Guid id=Guid.NewGuid();Q.Execute(c,t,"INSERT INTO revora_snake_attempts(id,uid,day,started_at) VALUES(@id,@uid,@day,@now)","id",id,"uid",(long)p.Entity.UID,"day",day,"now",now);
                Q.Execute(c,t,@"INSERT INTO revora_snake_hunts(uid,day,attempt,state,entered,started_at,rune_reward) VALUES(@uid,@day,@id,'hunting',true,@now,true)
ON CONFLICT(uid,day) DO UPDATE SET attempt=excluded.attempt,entered=true,started_at=excluded.started_at",
                    "uid",(long)p.Entity.UID,"day",day,"id",id,"now",now);
                t.Commit();active[p.Entity.UID]=new Attempt{Id=id,Day=day};Travel(p,true);
                return "Jump from isle to isle toward the Snake King near (87,67). The lesser snakes are dangerous; you need only defeat their king.";
            }}
        }
        private static string Claim(GameState p) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                string day;bool valuable;using(var q=Q.Command(c,t,"SELECT day,rune_reward FROM revora_snake_hunts WHERE uid=@uid AND state='victory' ORDER BY day LIMIT 1 FOR UPDATE","uid",(long)p.Entity.UID)) {
                    using(var row=q.ExecuteReader()){if(!row.Read())return "No victory reward is waiting for you.";day=row.GetString(0);valuable=row.GetBoolean(1);}
                }
                if(p.Inventory.Count+(valuable?3:2)>40)return valuable?"Make three places for your Meteors and Tortoise Breaker rune.":"Make two places in your bag. Your victory reward will wait.";
                var rewards=new[]{Q.New(1088001),Q.New(1088001)}.Concat(valuable?new[]{Q.New(4030101,true)}:new ConquerItem[0]).ToArray();foreach(var item in rewards)Q.Insert(c,t,p.Entity.UID,item);
                if(Q.Execute(c,t,"UPDATE revora_snake_hunts SET state='claimed',rewards=@items WHERE uid=@uid AND day=@day AND state='victory'",
                    "items",rewards.Select(i=>(long)i.UID).ToArray(),"uid",(long)p.Entity.UID,"day",day)!=1)throw new InvalidOperationException("Snake victory claim was not saved.");
                t.Commit();Q.Install(p,new uint[0],rewards);return valuable?"Two bound Meteors and a bound Tortoise Breaker rune. Well fought.":"Two bound Meteors for defeating the king. Well fought.";
            }}
        }
        private static void Close(GameState p,Attempt attempt,bool home,bool heal,string reason) {
            if(!Q.Player(p,false))return;
            Attempt current;
            if(attempt!=null && (!active.TryGetValue(p.Entity.UID,out current) || current.Id!=attempt.Id))return;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                if(attempt!=null) {
                    var r=Read(c,t,p.Entity.UID,attempt.Day);
                    if(r!=null && r.Attempt==attempt.Id){Q.Execute(c,t,"UPDATE revora_snake_hunts SET entered=false WHERE uid=@uid AND day=@day AND attempt=@id","uid",(long)p.Entity.UID,"day",attempt.Day,"id",attempt.Id);EndAttempt(c,t,attempt.Id,reason);}
                }else {
                    Q.Execute(c,t,"UPDATE revora_snake_hunts SET entered=false WHERE uid=@uid AND entered","uid",(long)p.Entity.UID);
                    Q.Execute(c,t,"UPDATE revora_snake_attempts SET ended_at=@now,reason=@reason WHERE uid=@uid AND ended_at IS NULL","now",Clock(),"reason",reason,"uid",(long)p.Entity.UID);
                }
                t.Commit();Attempt ignored;active.TryRemove(p.Entity.UID,out ignored);if(home)Travel(p,false,heal);
            }}
        }
        internal static bool HandleNpc(GameState p,NpcRequest request) {
            if(!enabled || p==null || p.Entity==null || request==null || p.ActiveNpc!=Ferry && p.ActiveNpc!=Exit)return false;
            if(p.ItemSyncRoot==null)return true;
            lock(p.ItemSyncRoot) {
                if(!Q.Near(p,p.ActiveNpc,p.ActiveNpc==Ferry?Bird:Island) || deaths.ContainsKey(p.Entity.UID))return true;
                try {
                    var d=new Npcs(p);byte option=request.OptionID;
                    if(option==255){p.ActiveNpc=0;return true;}
                    if(option==1 && p.ActiveNpc==Ferry)d.Text(Begin(p));
                    else if(option==3)d.Text(Claim(p));
                    else if(option==2 && p.ActiveNpc==Exit){Attempt a;active.TryGetValue(p.Entity.UID,out a);Close(p,a,true,false,"ferry-home");d.Text("Back to Bird Island. The sea will be here when you return.");}
                    else {
                        d.Text(p.ActiveNpc==Ferry?"The island snakes strike hard. Reach their king across the scattered isles and defeat him. New daily hunts earn two bound Meteors and a bound Tortoise Breaker rune.":"The king's island lies just south. Defeat him, then ask me for your reward or passage home.");
                        if(p.ActiveNpc==Ferry)d.Option("Take me to the islands.",1);else d.Option("Return to Bird Island.",2);
                        d.Option("Claim my victory reward.",3);
                    }
                    d.Option("Farewell.",255);d.Send();
                }catch(Exception ex){Q.Fail(p,"snake-npc",ex);}
            }return true;
        }
        internal static void OnLogin(GameState p) {
            if(!enabled || !Q.Player(p,false))return;
            lock(p.ItemSyncRoot)try{Death ignored;deaths.TryRemove(p.Entity.UID,out ignored);Close(p,null,p.Entity.MapID==Island,true,"login");Q.Refresh(p);}
            catch(Exception ex){Q.Fail(p,"snake-login",ex);}
        }
        internal static void OnDeath(GameState p) {
            Attempt attempt;if(!enabled || !Q.Player(p,false) || p.Entity.MapID!=Island || !active.TryGetValue(p.Entity.UID,out attempt))return;
            deaths[p.Entity.UID]=new Death{Player=p,Attempt=attempt};
        }
        internal static void Tick() {
            if(!enabled)return;
            foreach(var entry in deaths.ToArray()) {
                Death death;if(!deaths.TryRemove(entry.Key,out death) || !Q.Player(death.Player,false))continue;var p=death.Player;
                lock(p.ItemSyncRoot)try{Attempt a;if(active.TryGetValue(p.Entity.UID,out a) && a.Id==death.Attempt.Id)Close(p,a,p.Entity.MapID==Island,true,"death");}
                catch(Exception ex){Q.Fail(p,"snake-death",ex);}
            }
            if(Clock()<nextScan)return;nextScan=Clock().AddSeconds(2);string day=Day();
            foreach(var entry in active.ToArray()) {
                GameState p;if(!Kernel.GamePool.TryGetValue(entry.Key,out p) || !Q.Player(p,false)){Attempt ignored;active.TryRemove(entry.Key,out ignored);continue;}
                if(p.Entity.MapID==Island && entry.Value.Day==day)continue;
                lock(p.ItemSyncRoot)try {
                    // Admission installs active before ferry travel under this
                    // lock. Recheck after waiting; the old map may be stale.
                    Attempt current;
                    if(!Q.Player(p,false) || !active.TryGetValue(entry.Key,out current) || current.Id!=entry.Value.Id)continue;
                    string currentDay=Day();
                    if(p.Entity.MapID==Island && current.Day==currentDay)continue;
                    Close(p,current,p.Entity.MapID==Island,false,current.Day==currentDay?"leave":"new-day");
                }
                catch(Exception ex){Q.Fail(p,"snake-leave",ex);}
            }
        }
        internal static bool TryDie(Entity mob,Entity killer) {
            if(!enabled || mob==null || mob.EntityFlag!=EntityFlag.Monster || mob.Companion || mob.MonsterInfo==null || mob.MapID!=Island || mob.MonsterInfo.ID!=Boss)return false;
            Map map;Entity owned;if(!Kernel.Maps.TryGetValue(Island,out map) || !map.Entities.TryGetValue(mob.UID,out owned) || !ReferenceEquals(owned,mob))return false;
            Attempt attempt=null;if(killer!=null)active.TryGetValue(killer.UID,out attempt);
            bool died;
            lock(mob) {
                if(mob.Dead)return true;
                ushort x=mob.X,y=mob.Y;DateTime at=Clock();
                WildMonsterVictory.DieWithProgress(mob,killer,(c,t)=>CreditSaved(c,t,killer,attempt,x,y,mob.UID,at));
                died=mob.Dead;
            }
            if(died && attempt!=null)RefreshVictory(killer,attempt);
            return true;
        }
        private static bool CreditSaved(NpgsqlConnection c,NpgsqlTransaction t,Entity killer,Attempt attempt,ushort x,ushort y,uint boss,DateTime at) {
            if(attempt==null || killer==null || killer.EntityFlag!=EntityFlag.Player || killer.Owner==null || !ReferenceEquals(killer.Owner.Entity,killer))return false;
            var p=killer.Owner;
            if(!Q.Player(p) || p.Entity.Level<90 || p.Entity.MapID!=Island || Kernel.GetDistance(p.Entity.X,p.Entity.Y,x,y)>Constants.pScreenDistance
                || deaths.ContainsKey(p.Entity.UID) || attempt.Day!=SummerHunt.Day(at) || attempt.Day!=Day())return false;
            var r=Read(c,t,p.Entity.UID,attempt.Day);
            if(r==null || !r.Entered || r.State!="hunting" || r.Attempt!=attempt.Id || r.Started>at)return false;
            if(Q.Execute(c,t,"UPDATE revora_snake_hunts SET state='victory',entered=false,boss_uid=@boss WHERE uid=@uid AND day=@day AND attempt=@attempt",
                "boss",(long)boss,"uid",(long)p.Entity.UID,"day",attempt.Day,"attempt",attempt.Id)!=1)throw new InvalidOperationException("Snake victory was not saved.");
            ProgressionEvidence.Complete(c,t,p.Entity.UID,11018,"snake-islands-v1",attempt.Id.ToString("N"),1);
            EndAttempt(c,t,attempt.Id,"victory");return true;
        }
        private static void RefreshVictory(Entity killer,Attempt attempt) {
            var p=killer==null?null:killer.Owner;if(!Q.Player(p) || !ReferenceEquals(p.Entity,killer))return;
            lock(p.ItemSyncRoot)try {
                // Read back even when the life callback was skipped on an
                // uncertain-commit retry. Never remove a replacement admission.
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    var r=Read(c,t,p.Entity.UID,attempt.Day);
                    if(r==null || r.Attempt!=attempt.Id || r.State!="victory")return;
                    t.Commit();
                }}
                Attempt current,ignored;
                if(active.TryGetValue(p.Entity.UID,out current) && current.Id==attempt.Id)active.TryRemove(p.Entity.UID,out ignored);
                Q.Refresh(p);p.Send("The Snake King is defeated. Green Snake has your victory reward.");
            }catch(Exception ex){Q.Fail(p,"snake-victory-refresh",ex);}
        }
    }
}
