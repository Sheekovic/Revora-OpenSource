using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using E=Revora.Game.Features.ExorcismQuest;

namespace Revora.Game.Features
{
    internal static class ExorcismWorld
    {
        internal const uint Earth=900000132,Family=9916200;
        internal const int Capacity=16;
        internal static readonly object Gate=new object();
        internal sealed class Hunt {
            internal Guid Id;internal uint Owner;internal uint[] Party,Removed;internal int Grade;internal string State;internal DateTime Until;
            internal Map Map;internal Entity Body;internal bool Released;
            internal readonly ConcurrentDictionary<uint,GameState> Connections=new ConcurrentDictionary<uint,GameState>();
        }
        internal static readonly ConcurrentDictionary<ushort,Hunt> Maps=new ConcurrentDictionary<ushort,Hunt>();
        [ThreadStatic] private static HashSet<uint> transfers;
        private static DateTime nextSweep;
        internal static void Initialize() {
            ExorcismCombat.Initialize();
            var info=MonsterInformation.MonsterInformations[4212];if(info==null || info.Name!="ThrillingSpook" || info.Mesh!=952)throw new InvalidOperationException("Thrilling Spook template changed.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_exorcism_hunts(
id uuid PRIMARY KEY,uid bigint NOT NULL,party bigint[] NOT NULL,removed bigint[] NOT NULL DEFAULT '{}',grade integer NOT NULL CHECK(grade BETWEEN 1 AND 3),
state varchar(10) NOT NULL CHECK(state IN('waiting','active','victory','expired')),until_at timestamptz NOT NULL,killer bigint NOT NULL DEFAULT 0,
created_at timestamptz NOT NULL,defeated_at timestamptz);
CREATE UNIQUE INDEX IF NOT EXISTS revora_exorcism_hunt_owner ON revora_exorcism_hunts(uid) WHERE state IN('waiting','active');
CREATE TABLE IF NOT EXISTS revora_exorcism_members(run uuid NOT NULL REFERENCES revora_exorcism_hunts(id),uid bigint NOT NULL,
daily_id uuid REFERENCES revora_exorcism_daily(id),active boolean NOT NULL DEFAULT true,joined_at timestamptz NOT NULL,PRIMARY KEY(run,uid));
CREATE UNIQUE INDEX IF NOT EXISTS revora_exorcism_admitted ON revora_exorcism_members(uid) WHERE active;");}
        }
        internal static bool HasHunt(NpgsqlConnection c,NpgsqlTransaction t,uint uid,DateTime now) {
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_exorcism_hunts WHERE @uid=ANY(party) AND NOT(@uid=ANY(removed)) AND state IN('waiting','active') AND until_at>@now)","uid",(long)uid,"now",now))return Equals(q.ExecuteScalar(),true);
        }
        internal static bool HasAdmission(NpgsqlConnection c,NpgsqlTransaction t,uint uid,DateTime now) {
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_exorcism_members m JOIN revora_exorcism_hunts h ON h.id=m.run WHERE m.uid=@uid AND m.active AND h.state IN('waiting','active') AND h.until_at>@now)","uid",(long)uid,"now",now))return Equals(q.ExecuteScalar(),true);
        }
        private static Hunt Read(NpgsqlConnection c,NpgsqlTransaction t,uint uid,DateTime now,Guid? run=null) {
            using(var q=Q.Command(c,t,@"SELECT id,uid,party,removed,grade,state,until_at FROM revora_exorcism_hunts
WHERE @uid=ANY(party) AND NOT(@uid=ANY(removed)) AND state IN('waiting','active') AND until_at>@now AND (CAST(@run AS uuid) IS NULL OR id=@run) ORDER BY created_at LIMIT 1 FOR UPDATE","uid",(long)uid,"now",now,"run",run.HasValue?(object)run.Value:DBNull.Value))
            using(var r=q.ExecuteReader())if(r.Read())return new Hunt{Id=r.GetGuid(0),Owner=Convert.ToUInt32(r[1]),Party=((long[])r[2]).Select(i=>checked((uint)i)).ToArray(),Removed=((long[])r[3]).Select(i=>checked((uint)i)).ToArray(),
                Grade=r.GetInt32(4),State=r.GetString(5),Until=r.GetFieldValue<DateTimeOffset>(6).UtcDateTime};return null;
        }
        private static bool Group(Hunt h,GameState p) {
            if(p==null || !h.Party.Contains(p.Entity.UID) || h.Removed.Contains(p.Entity.UID))return false;
            GameState owner;if(!Kernel.GamePool.TryGetValue(h.Owner,out owner) || !Q.Player(owner,false))return false;
            if(h.Party.Length==1)return p.Entity.UID==h.Owner;
            GameState member;var team=owner.Team;return team!=null && ReferenceEquals(p.Team,team)
                && team.TryGetMember(p.Entity.UID,out member) && ReferenceEquals(member,p) && team.TryGetMember(h.Owner,out member) && ReferenceEquals(member,owner);
        }
        internal static bool Eligible(Hunt h,GameState p,bool alive) {
            GameState member;return !h.Released && Q.Player(p,alive) && Group(h,p) && h.Connections.TryGetValue(p.Entity.UID,out member) && ReferenceEquals(member,p)
                && p.Entity.MapID==h.Map.ID && ReferenceEquals(p.Map,h.Map);
        }
        private static uint[] Party(GameState p) {
            var members=p.Team==null?new[]{p}:p.Team.Teammates.Concat(new[]{p});GameState member;
            return members.Where(m=>E.Ready(m) && m.Entity.Level>=Config.DailyExorcismMinLevel && m.Entity.MapID==1036 && Kernel.GetDistance(m.Entity.X,m.Entity.Y,p.Entity.X,p.Entity.Y)<=Constants.pScreenDistance
                && (ReferenceEquals(m,p) || ReferenceEquals(m.Team,p.Team) && p.Team.TryGetMember(m.Entity.UID,out member) && ReferenceEquals(member,m)))
                .Select(m=>m.Entity.UID).Distinct().OrderBy(u=>u).Take(5).ToArray();
        }
        private static void ExpireDue(NpgsqlConnection c,NpgsqlTransaction t,DateTime now) {
            Q.Execute(c,t,"UPDATE revora_exorcism_hunts SET state='expired' WHERE state IN('waiting','active') AND until_at<=@now","now",now);
            Q.Execute(c,t,"UPDATE revora_exorcism_members m SET active=false FROM revora_exorcism_hunts h WHERE m.run=h.id AND m.active AND h.state NOT IN('waiting','active')");
        }
        internal static string Enter(GameState p) {
            if(!E.Near(p))return "Speak with Heavenly Master in the Market.";
            lock(Gate) {
                Hunt h;E.Daily daily;DateTime now=E.Clock();
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    E.Owner(c,t,p);ExpireDue(c,t,now);daily=E.Read(c,t,p);h=Read(c,t,p.Entity.UID,now);
                    if(h==null) {
                        if(!E.Pending(daily,now))return daily!=null && daily.State=="victory"?"Claim your recorded victory first.":"Choose your Spook before entering, or join a companion's hunt.";
                        if(Maps.Count>=Capacity)return "The seals are occupied. Return shortly.";
                        var party=Party(p);if(!party.Contains(p.Entity.UID))return "Gather your companions beside me.";
                        foreach(uint uid in party)if(HasHunt(c,t,uid,now))return "A companion already belongs to another hunt. Finish or leave that hunt first.";
                        h=new Hunt{Id=Guid.NewGuid(),Owner=p.Entity.UID,Party=party,Removed=new uint[0],Grade=daily.Grade,State="waiting",Until=new DateTime(now.AddHours(1).Ticks/10*10,DateTimeKind.Utc)};
                        if(Q.Execute(c,t,"INSERT INTO revora_exorcism_hunts(id,uid,party,grade,state,until_at,created_at) VALUES(@id,@uid,@party,@grade,'waiting',@until,@now)",
                            "id",h.Id,"uid",(long)h.Owner,"party",party.Select(i=>(long)i).ToArray(),"grade",h.Grade,"until",h.Until,"now",now)!=1)throw new InvalidOperationException("Exorcism hunt was not saved.");
                    }
                    if(!Group(h,p))return "Return with the companions who opened this seal, or leave that hunt.";
                    if(p.Entity.UID!=h.Owner) {
                        var live=Maps.Values.FirstOrDefault(i=>i.Id==h.Id);GameState owner;
                        if(live==null || !Kernel.GamePool.TryGetValue(h.Owner,out owner) || !Eligible(live,owner,false))return "Let the summoner enter first.";
                    }
                    bool pending=E.Pending(daily,now);
                    if(pending && daily.Grade!=h.Grade)return "Choose the same Spook as your summoner before entering.";
                    if(!pending && (daily==null || daily.Completed=="" || string.CompareOrdinal(daily.Completed,E.Day(now))<0))return "Choose your Spook before entering this hunt.";
                    using(var q=Q.Command(c,t,"SELECT daily_id FROM revora_exorcism_members WHERE run=@run AND uid=@uid","run",h.Id,"uid",(long)p.Entity.UID)) {
                        object prior=q.ExecuteScalar();if(prior!=null && prior!=DBNull.Value && (!pending || (Guid)prior!=daily.Id))return "Your entry belongs to a different daily commission.";
                    }
                    if(Q.Execute(c,t,@"INSERT INTO revora_exorcism_members(run,uid,daily_id,active,joined_at) VALUES(@run,@uid,@daily,true,@now)
ON CONFLICT(run,uid) DO UPDATE SET active=true WHERE revora_exorcism_members.daily_id IS NOT DISTINCT FROM EXCLUDED.daily_id",
                        "run",h.Id,"uid",(long)p.Entity.UID,"daily",pending?(object)daily.Id:DBNull.Value,"now",now)!=1)throw new InvalidOperationException("Exorcism admission changed.");
                    if(!E.Near(p))throw new InvalidOperationException("Exorcism entry session changed.");t.Commit();
                }}
                var instance=Maps.Values.FirstOrDefault(i=>i.Id==h.Id);if(instance==null)instance=Create(h);
                instance.Connections[p.Entity.UID]=p;Travel(p,instance.Map,36,27);
                return "Earth Master waits beside the seal. Leave the hunt before your hour is spent.";
            }
        }
        private static Hunt Create(Hunt h) {
            if(Maps.Count>=Capacity)throw new InvalidOperationException("Exorcism instance capacity reached.");
            using(var sha=SHA256.Create())using(var file=File.OpenRead(Path.Combine(Constants.DMapsPath,"maps","1013.map")))
                if(System.BitConverter.ToString(sha.ComputeHash(file)).Replace("-","")!="E689BAA83C58952C16352A10E4C5DF2D2E8BC86FFC3BE728C4F74574183B4868")throw new InvalidOperationException("Exorcism terrain changed.");
            ushort id=0;
            Map map=null;
            try {
                map=Map.CreateDynamic(1013,"maps\\1013.map",true);id=map.ID;h.Map=map;
                if(!ReferenceEquals(Kernel.Maps[id],map) || map.Entities.Count!=0 || map.Npcs.Count!=0)throw new InvalidOperationException("Exorcism map ownership collision.");
                foreach(var point in new[]{new[]{36,24},new[]{36,27},new[]{25,30},new[]{52,49}})if(!map.Floor[point[0],point[1],MapObjectType.Player])throw new InvalidOperationException("Exorcism landing blocked.");
                map.Portals.Clear();MapsTable.MapInformations[id]=new MapsTable.MapInformation{ID=id,BaseID=1013,Status=2};
                map.AddNpc(new NpcSpawn{UID=Earth,MapID=id,X=36,Y=24,Mesh=7610,Name="EarthMaster",Type=Enums.NpcType.Talker});
                map.AddNpc(new NpcSpawn{UID=8308,MapID=id,X=25,Y=30,Mesh=2507,Name="AlchemistDan",Type=Enums.NpcType.Talker});
                if(!Maps.TryAdd(id,h))throw new InvalidOperationException("Exorcism instance identity collision.");if(h.State=="active")Spawn(h);return h;
            }catch{
                if(map!=null)Release(h);
                throw;
            }
        }
        private static void Travel(GameState p,Map map,ushort x,ushort y) {
            if(transfers==null)transfers=new HashSet<uint>();transfers.Add(p.Entity.UID);
            try{p.Entity.Teleport(map.BaseID,map.ID,x,y);if(p.Entity.MapID!=map.ID)throw new InvalidOperationException("Exorcism travel did not arrive.");}
            finally{transfers.Remove(p.Entity.UID);}
        }
        private static void Home(GameState p) {
            var map=Kernel.Maps[1036];if(p.Entity.Dead)p.Entity.BringToLife();Travel(p,map,249,152);
        }
        internal static bool AllowTeleport(GameState p,ushort destination,ushort x,ushort y) {
            Hunt h;if(!Maps.TryGetValue(destination,out h))return true;
            if(p==null || p.Entity==null || h.Released || E.Clock()>=h.Until)return false;
            if(transfers!=null && transfers.Contains(p.Entity.UID))return h.Connections.ContainsKey(p.Entity.UID) && Group(h,p);
            if(ExorcismCombat.IsStunned(p) || !Eligible(h,p,false) || !h.Map.Floor[x,y,MapObjectType.Player])return false;
            long dx=(long)x-p.Entity.X,dy=(long)y-p.Entity.Y;return dx*dx+dy*dy<441;
        }
        private static void Spawn(Hunt h) {
            if(h.Body!=null)return;var info=MonsterInformation.MonsterInformations[4212].Copy();info.ID=Family+(uint)h.Grade;info.Name=h.Grade==1?"ThrillingSpook":"ThrillingSpook"+h.Grade;
            info.Boss=true;info.Guard=info.Reviver=false;info.Level=new byte[]{90,120,140}[h.Grade-1];info.Hitpoints=new uint[]{10000000,32000000,55000000}[h.Grade-1];
            info.MinAttack=20000;info.MaxAttack=30000;info.Defence=2328;info.SpellID=0;info.AttackType=1;info.AttackRange=20;info.ViewRange=18;
            info.AttackSpeed=1500;info.MoveSpeed=1000;info.RunSpeed=500;info.RespawnTime=int.MaxValue/2;info.BoundX=52;info.BoundY=49;info.BoundCX=info.BoundCY=1;
            var body=new Entity(EntityFlag.Monster,false){UID=h.Map.EntityUIDCounter.Next,MapID=h.Map.ID,X=52,Y=49,MonsterInfo=info,MapObjType=MapObjectType.Monster,
                Name=info.Name,Body=info.Mesh,Level=info.Level,MinAttack=info.MinAttack,MaxAttack=info.MaxAttack,MagicAttack=info.MaxAttack,Defence=info.Defence};
            while(h.Map.Entities.ContainsKey(body.UID) || h.Map.Npcs.ContainsKey(body.UID) || h.Map.Companions.ContainsKey(body.UID) || Kernel.GamePool.ContainsKey(body.UID))body.UID=h.Map.EntityUIDCounter.Next;
            info.Owner=body;body.MaxHitpoints=body.Hitpoints=info.Hitpoints;body.ApplyMonsterBossHud();body.SendUpdates=true;h.Body=body;h.Map.AddEntity2(body);
            foreach(var p in h.Connections.Values)if(Eligible(h,p,false) && Kernel.GetDistance(p.Entity.X,p.Entity.Y,body.X,body.Y)<Constants.nScreenDistance)body.SendSpawn(p,false);
        }
        internal static bool HandleNpc(GameState p,NpcRequest req) {
            if(p==null || req==null || p.ActiveNpc!=Earth)return false;if(!E.Ready(p))return true;
            lock(p.ItemSyncRoot)lock(Gate)try {
                Hunt h;if(!Maps.TryGetValue(p.Entity.MapID,out h) || !Eligible(h,p,true) || E.Clock()>=h.Until || !Q.Near(p,Earth,h.Map.ID) || req.NpcID!=0 && req.NpcID!=Earth)return true;
                if(req.OptionID==2){Leave(p,h);return true;}var d=new Npcs(p);d.Avatar(67);
                if(req.OptionID==3) {
                    if(h.State=="waiting")using(var c=DataHolder.MySqlConnection){c.Open();if(Q.Execute(c,null,"UPDATE revora_exorcism_hunts SET state='active' WHERE id=@id AND state='waiting' AND until_at>@now","id",h.Id,"now",E.Clock())!=1)throw new InvalidOperationException("Exorcism summon changed.");h.State="active";}
                    if(h.State=="active"){Spawn(h);d.Text("The seal is broken. The Spook waits at (52,49).");}else d.Text("The Spook is defeated. Heavenly Master holds your reward.");
                }else{d.Text(h.State=="victory"?"The seal is quiet. Return to Heavenly Master for your reward.":"Your companions may enter through Heavenly Master. Break the seal when everyone is ready.");if(h.State=="waiting")d.Option("Release the Spook.",3);}
                d.Option("Return to Market.",2);d.Option("Stay here.",255);d.Send();
            }catch(Exception ex){Q.Fail(p,"exorcism-seal",ex);}return true;
        }
        internal static string Leave(GameState p){return Leave(p,null);}
        private static string Leave(GameState p,Hunt expected) {
            lock(Gate) {
                Hunt h=expected;bool departed=false;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    var saved=Read(c,t,p.Entity.UID,E.Clock(),expected==null?(Guid?)null:expected.Id);
                    if(h==null)h=saved==null?Maps.Values.FirstOrDefault(i=>i.Connections.ContainsKey(p.Entity.UID)):Maps.Values.FirstOrDefault(i=>i.Id==saved.Id);
                    if(saved!=null) {
                        if(saved.Owner==p.Entity.UID) {
                            if(Q.Execute(c,t,"UPDATE revora_exorcism_hunts SET state='expired' WHERE id=@id AND state IN('waiting','active')","id",saved.Id)!=1)throw new InvalidOperationException("Exorcism departure changed.");
                            Q.Execute(c,t,"UPDATE revora_exorcism_members SET active=false WHERE run=@run","run",saved.Id);
                        }else {
                            if(Q.Execute(c,t,"UPDATE revora_exorcism_hunts SET removed=array_append(removed,@uid) WHERE id=@id AND NOT(@uid=ANY(removed))","uid",(long)p.Entity.UID,"id",saved.Id)!=1)throw new InvalidOperationException("Exorcism membership changed.");
                            Q.Execute(c,t,"UPDATE revora_exorcism_members SET active=false WHERE run=@run AND uid=@uid","run",saved.Id,"uid",(long)p.Entity.UID);
                            departed=true;
                        }
                    }t.Commit();
                }}
                if(h!=null && departed)h.Removed=h.Removed.Concat(new[]{p.Entity.UID}).Distinct().ToArray();
                if(h!=null){if(h.Owner==p.Entity.UID)Release(h);else{GameState ignored;h.Connections.TryRemove(p.Entity.UID,out ignored);if(p.Entity.MapID==h.Map.ID)Home(p);}}
                return "The passage is closed behind you. Your daily commission remains with Heavenly Master.";
            }
        }
        internal static bool IsEncounter(Entity e){return e!=null && e.MonsterInfo!=null && e.MonsterInfo.ID>Family && e.MonsterInfo.ID<=Family+3;}
        private static GameState Fighter(Hunt h,Entity actor) {
            if(actor==null || actor.Dead || !Eligible(h,actor.Owner,true))return null;var p=actor.Owner;if(ReferenceEquals(p.Entity,actor))return p;Entity current;
            return actor.Companion && ReferenceEquals(p.Companion,actor) && actor.MapID==h.Map.ID && h.Map.Companions.TryGetValue(actor.UID,out current) && ReferenceEquals(current,actor)?p:null;
        }
        internal static bool BlockAttack(Entity attacker,Entity target) {
            Entity mob=IsEncounter(attacker)?attacker:IsEncounter(target)?target:null;Hunt h;
            if(mob!=null)return !Maps.TryGetValue(mob.MapID,out h) || !ReferenceEquals(h.Body,mob) || h.State!="active" || E.Clock()>=h.Until || Fighter(h,ReferenceEquals(mob,attacker)?target:attacker)==null;
            return attacker!=null && target!=null && (Maps.ContainsKey(attacker.MapID) || Maps.ContainsKey(target.MapID));
        }
        internal static bool TryDie(Entity mob,Entity killer) {
            if(!IsEncounter(mob))return false;Hunt h;if(!Maps.TryGetValue(mob.MapID,out h) || killer==null || killer.Owner==null)return true;
            var locked=new List<object>();var members=h.Connections.Values.Concat(new[]{killer.Owner}).Distinct().OrderBy(p=>p.Entity.UID).ToArray();
            try {
                foreach(var p in members){if(p.ItemSyncRoot==null || !Monitor.TryEnter(p.ItemSyncRoot))return true;locked.Add(p.ItemSyncRoot);}
                GameState winner;string spirit=null;var credited=new List<GameState>();
                lock(Gate) {
                    Entity current;if(h.Released || h.State!="active" || !ReferenceEquals(h.Body,mob) || mob.Dead || E.Clock()>=h.Until || !h.Map.Entities.TryGetValue(mob.UID,out current) || !ReferenceEquals(current,mob)
                        || (winner=Fighter(h,killer))==null || Kernel.GetDistance(winner.Entity.X,winner.Entity.Y,mob.X,mob.Y)>Constants.pScreenDistance)return true;
                    var eligible=members.Where(p=>Eligible(h,p,false) && Kernel.GetDistance(p.Entity.X,p.Entity.Y,mob.X,mob.Y)<=Constants.pScreenDistance).ToArray();
                    try{using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                        foreach(var p in eligible)E.Owner(c,t,p);
                        if(Q.Execute(c,t,"UPDATE revora_exorcism_hunts SET state='victory',killer=@killer,defeated_at=@now WHERE id=@id AND state='active' AND until_at>@now","killer",(long)winner.Entity.UID,"now",E.Clock(),"id",h.Id)!=1)throw new InvalidOperationException("Exorcism victory changed.");
                        foreach(var p in eligible) {
                            var daily=E.Read(c,t,p);if(!E.Pending(daily,E.Clock()) || daily.Grade!=h.Grade)continue;
                            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_exorcism_members WHERE run=@run AND uid=@uid AND daily_id=@daily AND active)","run",h.Id,"uid",(long)p.Entity.UID,"daily",daily.Id))if(!Equals(q.ExecuteScalar(),true))continue;
                            if(Q.Execute(c,t,"UPDATE revora_exorcism_daily SET state='victory',source_run=@run,victory_at=@now WHERE id=@id AND state='accepted' AND completed_day=''","run",h.Id,"now",E.Clock(),"id",daily.Id)!=1)throw new InvalidOperationException("Exorcism daily victory was not saved.");credited.Add(p);
                        }
                        int admitted;using(var q=Q.Command(c,t,"SELECT count(*) FROM revora_exorcism_members WHERE run=@run AND active","run",h.Id))admitted=Convert.ToInt32(q.ExecuteScalar());
                        if(admitted==0)throw new InvalidOperationException("Exorcism admission receipt missing.");
                        if(credited.Contains(winner))spirit=DailySoulBall.RecordSpook(c,t,winner,h);
                        ProgressionEvidence.Complete(c,t,winner.Entity.UID,10529,"spook-kill-v1",h.Id.ToString("N"),1);
                        EquipmentAchievements.RecordUnarmedVictory(c,t,winner,mob,killer,"spook:"+h.Id.ToString("N"));
                        using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10529)","uid",(long)winner.Entity.UID))
                            if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Spook kill achievement was not saved.");
                        if(Q.Execute(c,t,"UPDATE revora_exorcism_members SET active=false WHERE run=@run AND active","run",h.Id)!=admitted)throw new InvalidOperationException("Exorcism participant receipt changed.");
                        if(!Eligible(h,winner,true) || credited.Any(p=>!Eligible(h,p,false)))throw new InvalidOperationException("Exorcism victory session changed.");
                        t.Commit();
                    }}}catch(Exception ex){Q.Fail(winner,"exorcism-victory",ex);return true;}
                    h.State="victory";RemoveBoss(h);
                }
                mob.DieCore(winner.Entity);Q.Refresh(winner);foreach(var p in credited){E.Refresh(p);p.Send("The Spook is defeated. Claim your reward from Heavenly Master.");}if(spirit!=null)winner.Send(spirit);return true;
            }finally{for(int n=locked.Count-1;n>=0;n--)Monitor.Exit(locked[n]);}
        }
        private static void RemoveBoss(Hunt h){if(h.Body==null)return;h.Body.MonsterInfo.SendScreen(new Data(true){UID=h.Body.UID,ID=Data.RemoveEntity});h.Map.RemoveEntity(h.Body);h.Body=null;}
        internal static void Release(Hunt h) {
            lock(Gate){if(h.Released)return;h.Released=true;
                foreach(var p in h.Connections.Values.ToArray())if(Q.Player(p,false) && p.Entity.MapID==h.Map.ID)Home(p);
                RemoveBoss(h);Hunt ignored;Maps.TryRemove(h.Map.ID,out ignored);h.Map.Npcs.Clear();MapsTable.MapInformations.Remove(h.Map.ID);h.Map.Dispose();}
        }
        internal static void Tick() {
            var now=E.Clock();if(now<nextSweep && (nextSweep-now).TotalSeconds<=2)return;nextSweep=now.AddSeconds(2);
            foreach(var h in Maps.Values.ToArray())try {
                lock(Gate) {
                    if(h.Released)continue;
                    GameState owner;if(!Kernel.GamePool.TryGetValue(h.Owner,out owner) || !Q.Player(owner,false)) {Release(h);continue;}
                    if(E.Clock()>=h.Until){using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){ExpireDue(c,t,E.Clock());t.Commit();}}Release(h);continue;}
                    if(owner.Entity.MapID!=h.Map.ID || !Group(h,owner)){Leave(owner,h);continue;}
                    foreach(var p in h.Connections.Values.Where(p=>p.Entity.UID!=h.Owner).ToArray()) {
                        if(!Q.Player(p,false)){GameState ignored;h.Connections.TryRemove(p.Entity.UID,out ignored);continue;}
                        if(p.Entity.MapID!=h.Map.ID || !Group(h,p))Leave(p,h);
                    }
                    if(h.Body!=null) {var target=h.Connections.Values.Where(p=>Eligible(h,p,true)).OrderBy(p=>Kernel.GetDistance(p.Entity.X,p.Entity.Y,h.Body.X,h.Body.Y)).FirstOrDefault();h.Body.MonsterInfo.InSight=target==null?0:target.Entity.UID;}
                }
            }catch(Exception ex){Console.WriteLine("[quest] source=exorcism-tick error="+ex.GetType().Name);}
            foreach(var h in Maps.Values.ToArray())try{ExorcismCombat.Tick(h);}
                catch(Exception ex){Console.WriteLine("[quest] source=exorcism-combat error="+ex.GetType().Name);}
        }
    }
}
