using System;
using System.Collections.Concurrent;
using System.Linq;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using W=Revora.Game.Features.SkyPassWorld;

namespace Revora.Game.Features
{
    internal static class SkyPassQuest
    {
        internal const uint Mission=6350;
        internal const int DialogRange=12;
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static Func<int,int> Roll=max=>Kernel.Random.Next(max);
        private static bool enabled;
        private static DateTime nextScan;
        private static readonly ConcurrentDictionary<uint,Guid> active=new ConcurrentDictionary<uint,Guid>();
        private sealed class Death {internal GameState Player;internal Guid Run;}
        private static readonly ConcurrentDictionary<uint,Death> deaths=new ConcurrentDictionary<uint,Death>();
        private sealed class Run {internal Guid Id;internal string Day,Phase;internal int Stage,Route,Step;internal int[] Gates;internal uint Token;internal bool RuneReward;internal DateTime BattleAt;}
        private static Run Read(NpgsqlConnection c,NpgsqlTransaction t,uint uid) {
            using(var q=Q.Command(c,t,"SELECT run,day,phase,stage,route,step,gates,token,battle_at,rune_reward FROM revora_sky_runs WHERE uid=@uid FOR UPDATE","uid",(long)uid))
            using(var r=q.ExecuteReader())return r.Read()?new Run{Id=r.GetGuid(0),Day=r.GetString(1),Phase=r.GetString(2),Stage=r.GetInt32(3),Route=r.GetInt32(4),Step=r.GetInt32(5),Gates=r.GetFieldValue<int[]>(6),Token=Convert.ToUInt32(r[7]),BattleAt=r.GetFieldValue<DateTimeOffset>(8).UtcDateTime,RuneReward=r.GetBoolean(9)}:null;
        }
        private static bool Open(Run r) {return r!=null && r.Phase!="complete" && r.Phase!="abandoned";}
        private static void Save(NpgsqlConnection c,NpgsqlTransaction t,uint uid,Run r,string action,uint monster=0) {
            r.Step++;
            if(Q.Execute(c,t,@"INSERT INTO revora_sky_runs(uid,run,day,phase,stage,route,step,gates,token,battle_at,rune_reward)
VALUES(@uid,@run,@day,@phase,@stage,@route,@step,@gates,@token,@battle,@valuable)
ON CONFLICT(uid) DO UPDATE SET run=excluded.run,day=excluded.day,phase=excluded.phase,stage=excluded.stage,route=excluded.route,
step=excluded.step,gates=excluded.gates,token=excluded.token,battle_at=excluded.battle_at,rune_reward=excluded.rune_reward",
                "uid",(long)uid,"run",r.Id,"day",r.Day,"phase",r.Phase,"stage",r.Stage,"route",r.Route,"step",r.Step,"gates",r.Gates,"token",(long)r.Token,"battle",r.BattleAt,"valuable",r.RuneReward)!=1)throw new InvalidOperationException("Sky run was not saved.");
            if(Q.Execute(c,t,@"INSERT INTO revora_sky_steps(run,step,uid,day,action,stage,route,gates,token,monster_uid)
VALUES(@run,@step,@uid,@day,@action,@stage,@route,@gates,@token,@monster)","run",r.Id,"step",r.Step,"uid",(long)uid,"day",r.Day,
                "action",action,"stage",r.Stage,"route",r.Route,"gates",r.Gates,"token",(long)r.Token,"monster",(long)monster)!=1)throw new InvalidOperationException("Sky step was not saved.");
        }
        internal static void Initialize() {
            ConquerItemBaseInformation rune;
            if(!ConquerItemInformation.BaseInformations.TryGetValue(4030101,out rune) || rune.Class!=0)
                throw new InvalidOperationException("Daily island yellow rune catalog missing or class restricted.");
            W.Initialize();
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_sky_runs(
uid bigint PRIMARY KEY CHECK(uid>0),run uuid UNIQUE NOT NULL,day varchar(8) NOT NULL,
phase varchar(16) NOT NULL CHECK(phase IN ('gate','battle','cleared','token','prize','complete','abandoned')),
stage integer NOT NULL CHECK(stage BETWEEN 1 AND 5),route integer NOT NULL CHECK(route BETWEEN -1 AND 4),step integer NOT NULL CHECK(step>0),
gates integer[] NOT NULL CHECK(cardinality(gates)=4),token bigint NOT NULL DEFAULT 0,battle_at timestamptz NOT NULL);
ALTER TABLE revora_sky_runs ADD COLUMN IF NOT EXISTS valuable_reward boolean NOT NULL DEFAULT false;
ALTER TABLE revora_sky_runs ADD COLUMN IF NOT EXISTS rune_reward boolean NOT NULL DEFAULT false;
CREATE TABLE IF NOT EXISTS revora_sky_steps(run uuid NOT NULL,step integer NOT NULL,uid bigint NOT NULL,day varchar(8) NOT NULL,
action varchar(16) NOT NULL,stage integer NOT NULL,route integer NOT NULL,gates integer[] NOT NULL,token bigint NOT NULL,monster_uid bigint NOT NULL,
created_at timestamptz NOT NULL DEFAULT now(),PRIMARY KEY(run,step));
CREATE TABLE IF NOT EXISTS revora_sky_claims(uid bigint NOT NULL,day varchar(8) NOT NULL,run uuid UNIQUE NOT NULL,
rewards bigint[] NOT NULL,claimed_at timestamptz NOT NULL,PRIMARY KEY(uid,day));");}
            active.Clear();deaths.Clear();nextScan=DateTime.MinValue;enabled=true;
        }
        private static bool Claimed(NpgsqlConnection c,NpgsqlTransaction t,uint uid) {
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_sky_claims WHERE uid=@uid AND day=@day)","uid",(long)uid,"day",SummerHunt.Day(Clock())))return Equals(q.ExecuteScalar(),true);
        }
        private static void Projection(GameState p,Run r) {
            if(p.Quests==null)return;
            if(r!=null && r.Phase=="complete" && r.Day==SummerHunt.Day(Clock()))QuestListSystem.MarkCompleted(p,Mission);
            else if(Open(r)) {
                QuestListSystem.MarkAccepted(p,Mission);var detail=new MsgTaskDetailInfo(true){UID=(QuestID)Mission};
                detail[6]=r.Phase=="prize"?1u:0u;p.Send(detail);
            }else QuestListSystem.MarkAvailable(p,Mission);
        }
        internal static void Refresh(GameState p) {
            if(!enabled || !Q.Player(p,false))return;
            lock(p.ItemSyncRoot)try{using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){var r=Read(c,t,p.Entity.UID);t.Commit();Projection(p,r);}}}
            catch(Exception ex){Q.Fail(p,"sky-refresh",ex);}
        }
        private static string Begin(GameState p) {
            if(p.Entity.OnDeath!=null || p.InQualifier() || p.InTeamQualifier())return "Finish your current contest before climbing the pass.";
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))if(q.ExecuteScalar()==null)throw new InvalidOperationException("Sky character missing.");
                var r=Read(c,t,p.Entity.UID);
                if(Claimed(c,t,p.Entity.UID))return "God Cloud has rewarded you today. Return after the next Pacific midnight.";
                if(Open(r) && r.Phase=="prize"){t.Commit();active[p.Entity.UID]=r.Id;W.Travel(p,5,-1,true);Projection(p,r);return "Your prize is still waiting with God Cloud.";}
                if(Open(r))return "Your climb is already under way. Finish it or ask me to abandon it.";
                var gates=Enumerable.Range(0,4).Select(i=>Roll(5)).ToArray();if(gates.Any(g=>g<0 || g>=5))throw new InvalidOperationException("Sky gate draw invalid.");
                r=new Run{RuneReward=true,Id=Guid.NewGuid(),Day=SummerHunt.Day(Clock()),Phase="gate",Stage=1,Route=-1,Gates=gates,BattleAt=Clock()};
                Save(c,t,p.Entity.UID,r,"begin");t.Commit();active[p.Entity.UID]=r.Id;W.Travel(p,1,-1);Projection(p,r);
                return "One of these five paths leads upward. The others test you in battle. Bring a battle pass to its guard and try again.";
            }}
        }
        private static string Choose(GameState p,W.Challenge challenge) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                var r=Read(c,t,p.Entity.UID);if(r==null || r.Phase!="gate" || r.Stage!=challenge.Stage)return "This path is not your next challenge.";
                bool advance=r.Stage<5 && r.Gates[r.Stage-1]==challenge.Route;
                if(advance){r.Stage++;r.Route=-1;r.Phase="gate";}else {r.Route=challenge.Route;r.Phase="battle";r.BattleAt=Clock();}
                Save(c,t,p.Entity.UID,r,advance?"open-gate":"enter-battle");t.Commit();active[p.Entity.UID]=r.Id;W.Travel(p,r.Stage,r.Route);Projection(p,r);
                return advance?"The path opens. Continue your climb.":"Defeat "+MonsterInformation.MonsterInformations[challenge.Monster].Name+" and bring its pass to the Stage Guard.";
            }}
        }
        private static uint TokenType(Run r) {return r.Stage<5?(uint)(721099+r.Stage):(uint)(721108-r.Route);}
        private static ConquerItem Issue(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Run r) {
            if(p.Inventory.Count>=40)return null;var item=Q.New(TokenType(r));Q.Insert(c,t,p.Entity.UID,item);r.Token=item.UID;r.Phase="token";return item;
        }
        private static string Guard(GameState p,W.Challenge challenge) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                var r=Read(c,t,p.Entity.UID);if(r==null || r.Stage!=challenge.Stage || r.Route!=challenge.Route)return "Bring me the pass from my own arena.";
                if(r.Phase=="cleared") {
                    var issued=Issue(c,t,p,r);if(issued==null)return "Make one place in your bag for your earned pass.";
                    Save(c,t,p.Entity.UID,r,"collect-pass");t.Commit();Q.Install(p,new uint[0],new[]{issued});return "Here is your pass. Speak to me again when you are ready to leave.";
                }
                if(r.Phase!="token" || r.Token==0)return "Defeat your appointed monster before asking to leave this arena.";
                uint old=r.Token;Q.Consume(c,t,p,old,TokenType(r));ConquerItem prize=null;
                if(r.Stage==5){prize=Q.New(721109);Q.Insert(c,t,p.Entity.UID,prize);r.Token=prize.UID;r.Phase="prize";}
                else {r.Token=0;r.Route=-1;r.Phase="gate";}
                Save(c,t,p.Entity.UID,r,"turn-in-pass");t.Commit();Q.Install(p,new[]{old},prize==null?new ConquerItem[0]:new[]{prize});
                W.Travel(p,r.Stage,-1,r.Phase=="prize");Projection(p,r);return prize==null?"You have proved yourself here. Try another path upward.":"Take this prize pass to God Cloud.";
            }}
        }
        private static string Claim(GameState p) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                var r=Read(c,t,p.Entity.UID);if(r==null || r.Phase!="prize" || r.Token==0)return "Bring the prize pass earned at the summit.";
                if(Claimed(c,t,p.Entity.UID))return "Your daily reward has already been claimed. Keep the pass safe.";
                if(p.Inventory.Count+(r.RuneReward?4:3)>40)return r.RuneReward?"Make room for four Meteors and a Tortoise Breaker rune. Your pass is safe.":"Make room for four bound Meteors. Your prize pass is safe.";
                Q.Consume(c,t,p,r.Token,721109);var rewards=Enumerable.Range(0,4).Select(i=>Q.New(1088001)).Concat(r.RuneReward?new[]{Q.New(4030101,true)}:new ConquerItem[0]).ToArray();foreach(var item in rewards)Q.Insert(c,t,p.Entity.UID,item);
                string day=SummerHunt.Day(Clock());if(Q.Execute(c,t,"INSERT INTO revora_sky_claims(uid,day,run,rewards,claimed_at) VALUES(@uid,@day,@run,@items,@now)",
                    "uid",(long)p.Entity.UID,"day",day,"run",r.Id,"items",rewards.Select(i=>(long)i.UID).ToArray(),"now",Clock())!=1)throw new InvalidOperationException("Sky claim was not saved.");
                ProgressionEvidence.Complete(c,t,p.Entity.UID,11019,"sky-pass-v1",r.Id.ToString("N"),1);
                uint token=r.Token;r.Token=0;r.Phase="complete";r.Day=day;Save(c,t,p.Entity.UID,r,"claim");t.Commit();
                Q.Install(p,new[]{token},rewards);Guid ignored;active.TryRemove(p.Entity.UID,out ignored);Q.Refresh(p);Projection(p,r);W.Travel(p,0,-1);
                return r.RuneReward?"Four Meteors and a bound Tortoise Breaker rune. You have earned them.":"Four Meteors for a worthy climb. May the sky favor your next journey.";
            }}
        }
        private static void Abandon(GameState p,bool returnHome,bool heal) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                var r=Read(c,t,p.Entity.UID);uint removed=0;
                if(Open(r) && r.Phase!="prize") {
                    if(r.Token!=0 && Q.Owns(c,t,p,r.Token,TokenType(r))){removed=r.Token;Q.Consume(c,t,p,r.Token,TokenType(r));}
                    r.Token=0;r.Phase="abandoned";Save(c,t,p.Entity.UID,r,"abandon");
                }
                t.Commit();if(removed!=0)Q.Install(p,new[]{removed},new ConquerItem[0]);Guid ignored;active.TryRemove(p.Entity.UID,out ignored);
                Projection(p,r);if(returnHome)W.Travel(p,0,-1,false,heal);
            }}
        }
        internal static bool HandleNpc(GameState p,NpcRequest request) {
            if(!enabled || p==null || p.Entity==null || request==null || !W.Actor(p.ActiveNpc))return false;
            if(p.ItemSyncRoot==null)return true;
            lock(p.ItemSyncRoot) {
                Revora.Interfaces.INpc actor;
                // Native gate pedestals sit across narrow gaps. Keep interactions
                // within the visible group, using its actual installed coordinates.
                if(!Q.Player(p) || p.Entity.MapID!=(p.ActiveNpc==W.Daniel?W.Home:W.MapId) || p.Booth!=null || p.Trade==null || p.Trade.InTrade
                    || !p.Map.Npcs.TryGetValue(p.ActiveNpc,out actor) || Kernel.GetDistance(p.Entity.X,p.Entity.Y,actor.X,actor.Y)>DialogRange || deaths.ContainsKey(p.Entity.UID))return true;
                try {
                    var d=new Npcs(p);uint npc=p.ActiveNpc;byte option=request.OptionID;
                    if(option==255){p.ActiveNpc=0;return true;}
                    if(option==9){Abandon(p,true,false);d.Text("The sky will wait. Begin again when you are ready.");}
                    else if(npc==W.Daniel) {
                        if(option==1)d.Text(Begin(p));else {d.Text("Five stages lead to God Cloud. False paths demand a battle; the final stage always does. Death or leaving ends an unfinished climb. An earned prize stays yours.");d.Option("Climb the Sky Pass.",1);}
                    }else if(npc==W.Cloud) {
                        if(option==1)d.Text(Claim(p));else {using(var c=DataHolder.MySqlConnection){c.Open();var r=Read(c,null,p.Entity.UID);d.Text(r!=null && r.RuneReward?"Your prize pass earns four bound Meteors and a bound Tortoise Breaker rune.":"A prize pass earns four bound Meteors, once each Pacific day.");}d.Option("Claim my reward.",1);}
                    }else {
                        var choice=W.Challenges.FirstOrDefault(c=>c.Gate==npc);
                        if(choice!=null) {
                            if(option==1)d.Text(Choose(p,choice));else {var monster=MonsterInformation.MonsterInformations[choice.Monster];d.Text("This path may test you against "+monster.Name+", level "+monster.Level+". Step forward when ready.");d.Option("Take this path.",1);}
                        }else {var guard=W.Challenges.Single(c=>c.Guard==npc);if(option==1)d.Text(Guard(p,guard));else {d.Text("Show me the pass earned in this arena, and I will guide you onward.");d.Option("Here is my pass.",1);}}
                    }
                    d.Option("Leave the climb.",9);d.Option("Farewell.",255);d.Send();
                }catch(Exception ex){Q.Fail(p,"sky-npc",ex);}
            }return true;
        }
        internal static void OnLogin(GameState p) {
            if(!enabled || !Q.Player(p,false))return;
            lock(p.ItemSyncRoot)try{Death ignored;deaths.TryRemove(p.Entity.UID,out ignored);Abandon(p,p.Entity.MapID==W.MapId,true);}
            catch(Exception ex){Q.Fail(p,"sky-login",ex);}
        }
        internal static void OnDeath(GameState p) {
            Guid run;if(!enabled || !Q.Player(p,false) || p.Entity.MapID!=W.MapId || !active.TryGetValue(p.Entity.UID,out run))return;
            deaths[p.Entity.UID]=new Death{Player=p,Run=run};
        }
        internal static void Tick() {
            if(!enabled)return;
            foreach(var entry in deaths.ToArray()) {
                Death death;if(!deaths.TryRemove(entry.Key,out death) || !Q.Player(death.Player,false))continue;
                var p=death.Player;lock(p.ItemSyncRoot)try{Guid run;if(active.TryGetValue(p.Entity.UID,out run) && run==death.Run)Abandon(p,p.Entity.MapID==W.MapId,true);}
                catch(Exception ex){Q.Fail(p,"sky-death",ex);}
            }
            if(Clock()<nextScan)return;nextScan=Clock().AddSeconds(2);
            foreach(var entry in active.ToArray()) {
                GameState p;if(!Kernel.GamePool.TryGetValue(entry.Key,out p) || !Q.Player(p,false)){Guid ignored;active.TryRemove(entry.Key,out ignored);continue;}
                if(p.Entity.MapID==W.MapId)continue;
                lock(p.ItemSyncRoot)try {
                    // A pending admission may have arrived while this tick
                    // waited for its owner lock. Never abandon that arrival.
                    Guid current;
                    if(!Q.Player(p,false) || !active.TryGetValue(entry.Key,out current) || current!=entry.Value || p.Entity.MapID==W.MapId)continue;
                    Abandon(p,false,false);
                }catch(Exception ex){Q.Fail(p,"sky-leave",ex);}
            }
        }
        internal static bool TryDie(Entity mob,Entity killer) {
            if(!enabled || mob==null || mob.EntityFlag!=EntityFlag.Monster || mob.Companion || mob.MonsterInfo==null || mob.MapID!=W.MapId || mob.MonsterInfo.ID<3000 || mob.MonsterInfo.ID>3024)return false;
            Map map;Entity owned;if(!Kernel.Maps.TryGetValue(mob.MapID,out map) || !map.Entities.TryGetValue(mob.UID,out owned) || !ReferenceEquals(owned,mob))return false;
            ushort x,y;uint type;DateTime at;bool died;Guid run=Guid.Empty;
            if(killer!=null)active.TryGetValue(killer.UID,out run);
            lock(mob){if(mob.Dead)return true;x=mob.X;y=mob.Y;type=mob.MonsterInfo.ID;at=Clock();mob.DieCore(killer);died=mob.Dead;}
            if(died)Credit(killer,x,y,type,mob.UID,at,run);return true;
        }
        private static void Credit(Entity killer,ushort x,ushort y,uint type,uint monster,DateTime at,Guid run) {
            if(run==Guid.Empty || killer==null || killer.EntityFlag!=EntityFlag.Player || killer.Owner==null || !ReferenceEquals(killer.Owner.Entity,killer))return;
            var p=killer.Owner;if(!Q.Player(p) || p.Entity.MapID!=W.MapId || !active.ContainsKey(p.Entity.UID) || Kernel.GetDistance(p.Entity.X,p.Entity.Y,x,y)>Constants.pScreenDistance)return;
            lock(p.ItemSyncRoot)try {
                if(!Q.Player(p) || deaths.ContainsKey(p.Entity.UID))return;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    var r=Read(c,t,p.Entity.UID);if(r==null || r.Id!=run || r.Phase!="battle" || r.BattleAt>at || r.Route<0 || W.At(r.Stage,r.Route).Monster!=type)return;
                    r.Phase="cleared";var item=Issue(c,t,p,r);Save(c,t,p.Entity.UID,r,"defeat",monster);t.Commit();
                    if(item!=null)Q.Install(p,new uint[0],new[]{item});
                    p.Send(item==null?"Your arena is cleared. Make one space, then ask its Stage Guard for your earned pass.":"Take your earned pass to this arena's Stage Guard.");
                }}
            }catch(Exception ex){Q.Fail(p,"sky-defeat",ex);}
        }
    }
}
