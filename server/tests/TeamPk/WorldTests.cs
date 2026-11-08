using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Npgsql;
using ElitePkStatus = Revora.Game.Features.Tournaments.ElitePkStatus;
using TP = Revora.Game.Features.Tournaments.TeamElitePk;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Interfaces;
using Revora.Network.GamePackets;
internal static partial class TeamPkLedgerTests {
    // Offline world/player setup follows the existing CompanionAI and PotencyHunt fixtures.
    private static uint nextUid=1260000;
    private static GameState WorldPlayer(Map map, bool fake, ushort x, ushort y, byte cls)
    {
        GameState state = new GameState(null);
        state.Account = new AccountTable(null);
        state.Account.EntityID = nextUid++;
        // Do not register production pet/screen timers in an offline fixture.
        state.Screen = (Screen)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Screen));
        GC.SuppressFinalize(state.Screen);
        state.Screen.Owner = state;
        typeof(Screen).GetField("_objects", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(state.Screen, new IMapObject[0]);
        typeof(Screen).GetField("_objectDictionary", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(state.Screen,
            new System.Collections.Concurrent.ConcurrentDictionary<uint, IMapObject>());
        state.Equipment = new Revora.Game.ConquerStructures.Equipment(state);
        state.Inventory = new Revora.Game.ConquerStructures.Inventory(state);
        state.Weapons = new Tuple<ConquerItem, ConquerItem>(null, null);
        state.ArenaStatistic = new QualifierStatistic(true);
        state.Variables = new VariableVault();
        state.Entity = new Entity(EntityFlag.Player, false);
        state.Entity.Owner = state;
        state.Entity.SendUpdates = false;
        state.Entity.UID = state.Account.EntityID;
        state.Entity.Name = fake ? "[Adv] Fixture" + state.Entity.UID : "Player" + state.Entity.UID;
        state.Entity.Body = 1003;
        state.Entity.Level = 140;
        state.Entity.Class = cls;
        state.Entity.MaxHitpoints = state.Entity.Hitpoints = 10000;
        state.Entity.MaxMana = state.Entity.Mana = 800;
        state.Entity.MinAttack = 1000;
        state.Entity.MaxAttack = 1200;
        state.Entity.MagicAttack = 1200;
        state.Entity.Stamina = 100;
        state.Entity.X = x; state.Entity.Y = y;
        state.Spells = new SafeDictionary<ushort, ISkill>();
        state.Proficiencies = new SafeDictionary<ushort, IProf>();
        state.Friends = new SafeDictionary<uint, Revora.Game.ConquerStructures.Society.Friend>();
        state.Enemy = new SafeDictionary<uint, Revora.Game.ConquerStructures.Society.Enemy>();
        state.BindWorldMap(map.ID);
        state.Socket.Alive = false;
        state.Fake = fake;
        if(!fake && Environment.GetEnvironmentVariable("REVORA_TEST_HEALTH_LEASE")=="1")
            typeof(Entity).GetField("HealthSynchronizationRequired",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(state.Entity,true);
        Kernel.GamePool[state.Entity.UID] = state;
        return state;
    }
    static Map TournamentMap(string runtime,ushort id) {
        var map=(Map)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Map));
        map.Path="maps/"+id+".map";DMaps.MapPaths[id]=map.Path;map.ID=map.BaseID=id;map.Entities=new System.Collections.Concurrent.SafeConcurrentDictionary<uint,Entity>();map.Companions=new System.Collections.Concurrent.SafeConcurrentDictionary<uint, Entity>();Kernel.Maps[id]=map;
        using(var r=new BinaryReader(File.OpenRead(Path.Combine(runtime,"database/maps/"+id+".map")))){
            int w=r.ReadInt32(),h=r.ReadInt32();map.Floor=new Floor(w,h,id);
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)map.Floor[x,y,MapObjectType.InvalidCast]=r.ReadByte()!=1;
        }
        map.Npcs=new Dictionary<uint,INpc>();map.Zones=new List<Zoning.Zone>();map.FloorItems=new ConcurrentDictionary<uint,FloorItem>();map.EntityUIDCounter=new Counter(400000);
        foreach(var f in typeof(Map).GetFields(BindingFlags.Public|BindingFlags.Instance))
            if(f.GetValue(map)==null && f.FieldType.IsGenericType && f.FieldType.GetConstructor(Type.EmptyTypes)!=null)f.SetValue(map,Activator.CreateInstance(f.FieldType));
        MapsTable.MapInformations[id]=new MapsTable.MapInformation{ID=id,BaseID=id,Status=2};return map;
    }
    static GameState TournamentPlayer(Map town) {
        var p=WorldPlayer(town,false,275,140,15);
        foreach(var f in typeof(Screen).GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance))
            if(f.GetValue(p.Screen)==null && f.FieldType.IsGenericType && f.FieldType.GetConstructor(Type.EmptyTypes)!=null)f.SetValue(p.Screen,Activator.CreateInstance(f.FieldType));
        p.Entity.Vitality=100;p.Entity.Strength=100;p.Entity.Agility=50;p.Entity.Spirit=50;
        p.Action=2;p.Account.State=AccountTable.AccountState.Player;p.Trade=new Revora.Game.ConquerStructures.Trade();
        p.InnerPower=new InnerPower(p.Entity.Name,p.Entity.UID);p.RunesItem=new SafeDictionary<uint,ConquerItem>();p.ChiData=new ChiTable.ChiData{Powers=p.ChiPowers};p.Entity.FullyLoaded=true;
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new NpgsqlCommand("INSERT INTO entities(uid,name,conquerpoints,revorapoint,money) VALUES(@uid,@name,0,0,0)",c)){q.Parameters.AddWithValue("uid",(long)p.Entity.UID);q.Parameters.AddWithValue("name",p.Entity.Name);q.ExecuteNonQuery();}}
        var armor=new ConquerItem(true){UID=ConquerItem.ItemUID.Next,ID=130009,Position=3,Durability=1000,MaximDurability=1000};WorldValue("INSERT INTO items(uid,id,entityid,position,warehouse) VALUES(@item,130009,@uid,3,0)","item",(long)armor.UID,"uid",(long)p.Entity.UID);
        p.Equipment.Add(armor,Enums.ItemUse.None);
        p.ActiveNpc=417171u;p.Entity.PKMode=Enums.PKMode.Capture;return p;
    }
    static object WorldValue(string sql,params object[] parameters) {
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new NpgsqlCommand(sql,c)){
            for(int i=0;i<parameters.Length;i+=2)q.Parameters.AddWithValue((string)parameters[i],parameters[i+1]);return q.ExecuteScalar();
        }}
    }
    static object Admission(string name,params object[] args) {
        try {return typeof(Revora.Game.Features.Tournaments.TeamElitePk).Assembly.GetType("Revora.Game.Features.Tournaments.LocalTeamPkAdmissions").GetMethod(name,Hidden).Invoke(null,args);}
        catch(TargetInvocationException e){throw e.InnerException;}
    }
    static GameState ReloadWorldPlayer(Map town,uint uid) {
        var p=WorldPlayer(town,false,327,303,15);uint placeholder=p.Entity.UID;
        foreach(var f in typeof(Screen).GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance))
            if(f.GetValue(p.Screen)==null && f.FieldType.IsGenericType && f.FieldType.GetConstructor(Type.EmptyTypes)!=null)f.SetValue(p.Screen,Activator.CreateInstance(f.FieldType));
        p.Trade=new Revora.Game.ConquerStructures.Trade();p.Account.EntityID=uid;p.Account.State=AccountTable.AccountState.Player;
        p.Account.Username="team_pk_world_fixture";p.Quests=new Quests(p);
        Check((bool)typeof(EntityTable).GetMethod("LoadEntityState",Hidden).Invoke(null,new object[]{p}),"production loader restores an interrupted entrant");
        Kernel.GamePool.Remove(placeholder);Kernel.GamePool[uid]=p;p.Action=2;p.Entity.FullyLoaded=true;
        p.InnerPower=new InnerPower(p.Entity.Name,p.Entity.UID);p.RunesItem=new SafeDictionary<uint,ConquerItem>();p.ChiData=new ChiTable.ChiData{Powers=p.ChiPowers};
        return p;
    }
    static void VerifyDamageRoute(Revora.Game.Features.Tournaments.TeamElitePk.Match match) {
        var combat=typeof(Revora.Game.Features.Tournaments.TeamElitePk).Assembly.GetType("Revora.Game.Features.Tournaments.LocalTeamPkCombat");
        var damageMethod=combat.GetMethod("Damage",Hidden);
        var sides=match.MatchStats.Where(f=>f.Fighting).Take(2).ToArray();
        var a=sides[0].Team.Players.First();var b=sides[1].Team.Players.First();
        uint hp=b.Entity.Hitpoints,before=sides[0].Points,damage=100;
        var packet=new Attack(true){Attacker=a.Entity.UID,Attacked=b.Entity.UID};
        Revora.Game.Attacking.Handle.ReceiveAttack(a.Entity,b.Entity,packet,ref damage,null);
        Check(b.Entity.Hitpoints==hp-100 && sides[0].Points==before+100,"actual nonlethal physical damage credits the attacking team exactly once");
        hp=b.Entity.Hitpoints;before=sides[0].Points;damage=100;
        Revora.Game.Attacking.Handle.ReceiveAttack(a.Entity,b.Entity,packet,ref damage,new SpellInformation{ID=1000});
        Check(b.Entity.Hitpoints==hp-100 && sides[0].Points==before+100,"actual nonlethal magic damage credits the attacking team exactly once");
        hp=b.Entity.Hitpoints;before=sides[0].Points;int accepted=0;
        System.Threading.Tasks.Parallel.For(0,32,i=>{
            uint hit=10;var hitPacket=new Attack(true){Attacker=a.Entity.UID,Attacked=b.Entity.UID};
            Revora.Game.Attacking.Handle.ReceiveAttack(a.Entity,b.Entity,hitPacket,ref hit,null);
            if(hit==10)System.Threading.Interlocked.Increment(ref accepted);
        });
        Check(accepted>0 && b.Entity.Hitpoints==hp-(uint)(accepted*10) && sides[0].Points==before+(uint)(accepted*10),"concurrent accepted hits keep HP loss and score equal; contention cannot mutate either");
        hp=b.Entity.Hitpoints;before=sides[0].Points;
        lock(match) {
            var denied=System.Threading.Tasks.Task.Run(()=>{
                uint hit=100;Revora.Game.Attacking.Handle.ReceiveAttack(a.Entity,b.Entity,new Attack(true){Attacker=a.Entity.UID,Attacked=b.Entity.UID},ref hit,null);return hit;
            });
            Check(denied.Wait(5000) && denied.Result==0 && b.Entity.Hitpoints==hp && sides[0].Points==before,"contended match ownership rejects a hit before HP or score mutation");
        }
        uint score=sides[0].Points;
        damageMethod.Invoke(null,new object[]{a.Entity,b.Entity,uint.MaxValue});
        Check(sides[0].Points==score+b.Entity.Hitpoints,"overkill scoring is capped to remaining target HP");
        score=sides[0].Points;
        damageMethod.Invoke(null,new object[]{a.Entity,a.Entity,100u});
        damageMethod.Invoke(null,new object[]{a.Entity,sides[0].Team.Players.Last().Entity,100u});
        Check(sides[0].Points==score,"self and same-team damage cannot score");
        var previous=sides[1].Team.EliteMatch;sides[1].Team.EliteMatch=null;
        damageMethod.Invoke(null,new object[]{a.Entity,b.Entity,100u});
        sides[1].Team.EliteMatch=previous;
        Check(sides[0].Points==score,"missing defender match is rejected without dereferencing it");
        sides[0].Points=uint.MaxValue-1;damageMethod.Invoke(null,new object[]{a.Entity,b.Entity,100u});
        Check(sides[0].Points==uint.MaxValue,"score overflow saturates instead of reversing the lead");
        sides[0].Points=before;b.Entity.Hitpoints=hp;
        var winner=sides.OrderByDescending(f=>f.LeaderUID).First();var loser=sides.Single(f=>!ReferenceEquals(f,winner));
        var killer=winner.Team.Players.First();var victims=loser.Team.Players.ToArray();
        for(int i=0;i<victims.Length;i++) {
            var victim=victims[i];uint remaining=victim.Entity.Hitpoints,priorScore=winner.Points;damage=remaining+1000;
            packet=new Attack(true){Attacker=killer.Entity.UID,Attacked=victim.Entity.UID};
            Revora.Game.Attacking.Handle.ReceiveAttack(killer.Entity,victim.Entity,packet,ref damage,i%2==0?null:new SpellInformation{ID=1000});
            Check(victim.Entity.Dead && winner.Points==priorScore+remaining,"actual lethal physical/magic damage scores remaining HP once for member "+i);
            Check(match.Done==(i==victims.Length-1),"match ends only after the final opposing member dies: "+i);
        }
        Check(winner.Winner && loser.Lost,"post-death combat route awards the surviving team the match");
        using(var attempting=new System.Threading.ManualResetEventSlim()) {
            System.Threading.Tasks.Task export;
            lock(match) {
                export=System.Threading.Tasks.Task.Run(()=>{attempting.Set();match.Export();});
                Check(attempting.Wait(5000) && !export.Wait(50),"timeout/export waits while the match lifecycle is owned");
            }
            Check(export.Wait(5000) && winner.Winner && loser.Lost,"export resumes after ownership release without changing the resolved winner");
        }

    }
    static void VerifyConcurrentOpening(bool skill=false) {
        var kind=skill?Revora.Game.Features.Tournaments.TeamElitePk.GamePackets.SkillElitePkBrackets:Revora.Game.Features.Tournaments.TeamElitePk.GamePackets.TeamElitePkBrackets;
        var opening=new DateTime(2026,9,30,19,45,35);
        var skillOpen=typeof(TP.SkillTeamTournament).GetMethod("OpenAt",BindingFlags.Static|BindingFlags.NonPublic);
        Func<bool> open=skill?(Func<bool>)(()=> (bool)skillOpen.Invoke(null,new object[]{opening})):TP.TeamTournament.Open;
        var brackets=Enumerable.Range(0,4).Select(i=>new Revora.Game.Features.Tournaments.TeamElitePk(i,kind)).ToArray();
        var priorPool=Revora.Thread.GenericThreadPool;
        // Register real callbacks without running wall-clock tournament ticks in this fixture.
        var pool=new System.Threading.StaticPool(1);Revora.Thread.GenericThreadPool=pool;
        try {
            if(skill)Revora.Game.Features.Tournaments.TeamElitePk.SkillTeamTournament.Tournaments=brackets;else Revora.Game.Features.Tournaments.TeamElitePk.TeamTournament.Tournaments=brackets;
            if(skill)Revora.Game.Features.Tournaments.TeamElitePk.SkillTeamTournament.Opened=false;else Revora.Game.Features.Tournaments.TeamElitePk.TeamTournament.Opened=false;
            var outcomes=new bool[8];
            System.Threading.Tasks.Parallel.For(0,outcomes.Length,i=>outcomes[i]=open());
            Check(outcomes.Count(x=>x)==1,"eight concurrent database-backed opens report one successful invitation trigger");
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var runs=brackets.Select(t=>(Guid)t.GetType().GetField("ledgerRun",flags).GetValue(t)).ToArray();
            Check(runs.Distinct().Count()==4 && Convert.ToInt64(Sql("SELECT count(*) FROM revora_local_team_pk_runs WHERE run=ANY(@runs) AND state='active'","runs",runs))==4,"one opening saves exactly four distinct active brackets");
            Check(brackets.All(t=>(bool)t.GetType().GetField("AllowJoin",flags).GetValue(t)
                && t.GetType().GetField("Subscriber",flags).GetValue(t)!=null),"each opened bracket owns registration and a real timer subscription");
            if(skill)Check(brackets.All(t=>(DateTime)t.GetType().GetField("StarTimer",flags).GetValue(t)==opening.Date.AddHours(20)),"all Skill brackets close exactly at 20:00 despite delayed opening");
            if(skill)Revora.Game.Features.Tournaments.TeamElitePk.SkillTeamTournament.Opened=false;else Revora.Game.Features.Tournaments.TeamElitePk.TeamTournament.Opened=false;
            Check(!open(),"same-date opening cannot restart existing bracket identities after the UI flag resets");
            foreach(var run in runs)Call("Cancel",run);
            if(skill) {
                var pulse=typeof(Revora.Thread).GetMethod("TickRestoredEvents",Hidden);
                pulse.Invoke(null,new object[]{opening.AddDays(7)});
                var scheduled=brackets.Select(t=>(Guid)t.GetType().GetField("ledgerRun",flags).GetValue(t)).ToArray();
                Check(TP.SkillTeamTournament.Opened&&scheduled.All(id=>!runs.Contains(id))
                    &&Convert.ToInt64(Sql("SELECT count(*) FROM revora_local_team_pk_runs WHERE run=ANY(@runs) AND state='active'","runs",scheduled))==4,
                    "actual restored-event pulse opens all Skill brackets on a due Wednesday");
                pulse.Invoke(null,new object[]{opening.AddDays(7).AddSeconds(1)});
                Check(brackets.Select(t=>(Guid)t.GetType().GetField("ledgerRun",flags).GetValue(t)).SequenceEqual(scheduled),
                    "repeated active-world pulse keeps the same Skill opening identities");
                foreach(var scheduledRun in scheduled)Call("Cancel",scheduledRun);
                TP.SkillTeamTournament.Opened=false;
                var manualStart=new DateTime(2026,9,28,11,0,0);
                Check(!(bool)skillOpen.Invoke(null,new object[]{manualStart}),"automatic Skill opening still refuses an off-calendar Monday");
                var manual=typeof(TP.SkillTeamTournament).GetMethod("OpenWindow",Hidden);
                Check((bool)manual.Invoke(null,new object[]{manualStart,manualStart.AddMinutes(15)}),"explicit operator registration window can open outside the recurring calendar");
                Check(brackets.All(t=>(DateTime)t.GetType().GetField("StarTimer",flags).GetValue(t)==manualStart.AddMinutes(15)),"manual Skill registration uses its own fifteen-minute deadline");
                var manualRuns=brackets.Select(t=>(Guid)t.GetType().GetField("ledgerRun",flags).GetValue(t)).ToArray();
                TP.SkillTeamTournament.Opened=false;
                Check(!(bool)manual.Invoke(null,new object[]{manualStart.AddMinutes(1),manualStart.AddMinutes(16)}),"manual start cannot bypass an already reserved date");
                foreach(var manualRun in manualRuns)Call("Cancel",manualRun);
            }
        } finally {
            foreach(var bracket in brackets) {
                var subscription=(IDisposable)bracket.GetType().GetField("Subscriber",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(bracket);
                if(subscription!=null)subscription.Dispose();
                bracket.Alive=false;bracket.WaitingArea.Dispose();
            }
            ((IDisposable)pool).Dispose();Revora.Thread.GenericThreadPool=priorPool;
            if(skill)Revora.Game.Features.Tournaments.TeamElitePk.SkillTeamTournament.Opened=false;else Revora.Game.Features.Tournaments.TeamElitePk.TeamTournament.Opened=false;
        }
    }
    static void VerifySkillImportRecovery(Map town,bool partial) {
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var bracket=new TP(3,TP.GamePackets.SkillElitePkBrackets);bracket.Alive=true;
        TP.SkillTeamTournament.Tournaments=new TP[4];TP.SkillTeamTournament.Tournaments[3]=bracket;
        var run=Guid.NewGuid();Call("Begin",run,TP.GamePackets.SkillElitePkBrackets,3,DateTime.UtcNow.AddDays(partial?-4:-3));
        typeof(TP).GetField("ledgerRun",flags).SetValue(bracket,run);
        var entrants=(ConcurrentDictionary<uint,Enums.PKMode>)typeof(TP).GetField("entrants",flags).GetValue(bracket);
        var players=new[]{TournamentPlayer(town),TournamentPlayer(town)};
        foreach(var p in players){
            p.Team=new Revora.Game.ConquerStructures.Team(p);p.Team.SetEliteFighterStats(true);bracket.Teams[p.Team.UID]=p.Team;
            Check((bool)Admission("Reserve",run,p),"Skill recovery fixture reserves ordinary participant");entrants[p.Entity.UID]=p.Entity.PKMode;
            var xy=bracket.WaitingArea.RandomCoordinates();p.Entity.Teleport(bracket.WaitingArea.ID,xy.Item1,xy.Item2);p.LoadItemStats();
        }
        uint normalHp=players[0].Entity.MaxHitpoints;
        var match=(TP.Match)typeof(TP).GetMethod("CreateOwnedMatch",flags).Invoke(bracket,new object[]{players.Select(p=>p.Team.EliteFighterStats).ToArray()});
        match.ID=400001;bracket.Matches[match.ID]=match;
        if(partial)players[1].Spells=null;else players[1].Equipment.Objects[2].ID=130999;
        match.Import();
        Check(!bracket.Alive&&!(bool)typeof(TP).GetProperty("ResultPayable",flags).GetValue(bracket,null),"failed Skill import fences bracket and forbids result payment");
        if(partial)Check(players[0].Entity.MapID==match.Map.ID&&players[0].Entity.InSkillPk,"fixture reaches a genuine partial import before second player fails");
        else Check(players.All(p=>p.Entity.MapID==bracket.WaitingArea.ID)&&Convert.ToInt32(typeof(TP.Match).GetField("Imports",flags).GetValue(match))==0,"whole participant preflight rejects invalid gear before any match import");
        players[1].Spells=new SafeDictionary<ushort,ISkill>();players[1].Equipment.Objects[2].ID=130009;
        if(partial) {
            Sql("CREATE FUNCTION reject_skill_cancel() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.state='cancelled' THEN RAISE EXCEPTION 'fixture'; END IF; RETURN NEW; END $$; CREATE TRIGGER reject_skill_cancel BEFORE UPDATE ON revora_local_team_pk_runs FOR EACH ROW EXECUTE PROCEDURE reject_skill_cancel();");
            typeof(TP).GetMethod("timerCallback",flags).Invoke(bracket,new object[]{0});
            Check(!bracket.Alive&&(bool)typeof(TP).GetField("importRecoveryPending",flags).GetValue(bracket)
                &&Equals(Sql("SELECT state FROM revora_local_team_pk_runs WHERE run=@run","run",run),"active"),"failed cancellation write retains fenced pending recovery for retry");
            Check(Convert.ToInt64(Sql("SELECT count(*) FROM revora_local_team_pk_admissions WHERE run=@run AND returned","run",run))==0,"failed cancellation cannot acknowledge returns or award a result");
            Sql("DROP TRIGGER reject_skill_cancel ON revora_local_team_pk_runs;DROP FUNCTION reject_skill_cancel();");
        }
        typeof(TP).GetMethod("timerCallback",flags).Invoke(bracket,new object[]{0});
        Check(Equals(Sql("SELECT state FROM revora_local_team_pk_runs WHERE run=@run","run",run),"cancelled")&&Convert.ToInt64(Sql("SELECT cardinality(leaders) FROM revora_local_team_pk_runs WHERE run=@run","run",run))==0,"recovery durably cancels the bracket without a winner");
        Check(players.All(p=>p.Entity.MapID==1002&&!p.Entity.InSkillPk)&&players[0].Entity.MaxHitpoints==normalHp,"recovery returns all ordinary entrants and restores normal character stats");
        Check(Convert.ToInt64(Sql("SELECT count(*) FROM revora_local_team_pk_admissions WHERE run=@run AND returned","run",run))==2&&!Kernel.Maps.ContainsKey(match.Map.ID),"return receipts are durable before owned arena disposal");
        typeof(TP).GetMethod("timerCallback",flags).Invoke(bracket,new object[]{0});
        Check(Convert.ToInt64(Sql("SELECT cardinality(leaders) FROM revora_local_team_pk_runs WHERE run=@run","run",run))==0,"recovery replay cannot create a winner");
        bracket.WaitingArea.Dispose();
    }
    static void VerifySkillOwnership(Map town) {
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var bracket=new TP(3,TP.GamePackets.SkillElitePkBrackets);bracket.Alive=true;
        TP.SkillTeamTournament.Tournaments=new TP[4];TP.SkillTeamTournament.Tournaments[3]=bracket;
        var run=Guid.NewGuid();Call("Begin",run,TP.GamePackets.SkillElitePkBrackets,3,DateTime.UtcNow.AddDays(-8));
        typeof(TP).GetField("ledgerRun",flags).SetValue(bracket,run);
        var players=new[]{TournamentPlayer(town),TournamentPlayer(town),TournamentPlayer(town)};
        var team=new Revora.Game.ConquerStructures.Team(players[0]);team.Add(players[1]);team.SetEliteFighterStats(true);
        var opponent=new Revora.Game.ConquerStructures.Team(players[2]);opponent.SetEliteFighterStats(true);
        bracket.Teams[team.UID]=team;bracket.Teams[opponent.UID]=opponent;
        foreach(var p in players){Check((bool)Admission("Reserve",run,p),"ownership fixture reserves an ordinary player");var xy=bracket.WaitingArea.RandomCoordinates();p.Entity.Teleport(bracket.WaitingArea.ID,xy.Item1,xy.Item2);}
        var match=(TP.Match)typeof(TP).GetMethod("CreateOwnedMatch",flags).Invoke(bracket,new object[]{new[]{team.EliteFighterStats,opponent.EliteFighterStats}});
        match.ID=400004;bracket.Matches[match.ID]=match;
        foreach(var p in players) {
            lock(p.ItemSyncRoot) {
                var importing=System.Threading.Tasks.Task.Run(()=>match.Import());
                Check(importing.Wait(5000)&&bracket.Alive&&!match.Inside
                    &&Convert.ToInt32(typeof(TP.Match).GetField("Imports",flags).GetValue(match))==0
                    &&players.All(actor=>actor.Entity.MapID==bracket.WaitingArea.ID&&!actor.Entity.InSkillPk),
                    "contended participant equipment ownership defers the entire Skill import without partial movement");
            }
        }
        match.Import();
        Check(players.All(p=>p.Entity.MapID==match.Map.ID&&p.Entity.InSkillPk),"deferred Skill import succeeds after all participant locks are released");
        var leaseType=typeof(TP).Assembly.GetType("Revora.Game.Features.Tournaments.LocalTeamPkCombatLease");
        var acquire=leaseType.GetMethod("Acquire",Hidden);var allowed=leaseType.GetProperty("Allowed",flags);
        System.Threading.Tasks.Task removing;
        using(var attempting=new System.Threading.ManualResetEventSlim()) {
            using(var lease=(IDisposable)acquire.Invoke(null,new object[]{players[1].Entity,players[2].Entity})) {
                Check(lease!=null&&(bool)allowed.GetValue(lease,null),"ordinary opposing Skill fighters acquire a combat lease");
                removing=System.Threading.Tasks.Task.Run(()=>{attempting.Set();team.Remove(players[1]);});
                Check(attempting.Wait(5000)&&!removing.Wait(50)&&ReferenceEquals(players[1].Team,team),
                    "actual team removal waits until the active hit releases participant ownership");
            }
            Check(removing.Wait(5000)&&players[1].Team==null&&!team.Members.ContainsKey(players[1].Entity.UID),
                "team removal completes after the hit releases its lease");
        }
        using(var lease=(IDisposable)acquire.Invoke(null,new object[]{players[1].Entity,players[2].Entity}))
            Check(lease!=null&&!(bool)allowed.GetValue(lease,null),"removed participant cannot acquire another Skill hit even while standing in its arena");
        Call("Cancel",run);bracket.Alive=false;
        foreach(var p in players)Check((bool)Admission("Return",p,true)&&p.Entity.MapID==1002&&!p.Entity.InSkillPk,
            "cancelled Skill return recovers ordinary participants including the removed member");
        match.Map.Dispose();bracket.WaitingArea.Dispose();
    }
    static void VerifySkillDisconnectRecovery(Map town) {
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var bracket=new TP(3,TP.GamePackets.SkillElitePkBrackets);bracket.Alive=true;
        TP.SkillTeamTournament.Tournaments=new TP[4];TP.SkillTeamTournament.Tournaments[3]=bracket;
        var run=Guid.NewGuid();Call("Begin",run,TP.GamePackets.SkillElitePkBrackets,3,DateTime.UtcNow.AddDays(-5));typeof(TP).GetField("ledgerRun",flags).SetValue(bracket,run);
        var entrants=(ConcurrentDictionary<uint,Enums.PKMode>)typeof(TP).GetField("entrants",flags).GetValue(bracket);
        var players=new[]{TournamentPlayer(town),TournamentPlayer(town)};var normalHp=new uint[2];var armorUid=new uint[2];
        for(int i=0;i<players.Length;i++){
            var p=players[i];p.Entity.DailySignDays=new byte[31];p.Entity.PKMode=Enums.PKMode.Peace;
            p.Entity.VIPLevel=4;p.Entity.NobilityRank=Revora.Game.ConquerStructures.NobilityRank.Duke;
            p.Team=new Revora.Game.ConquerStructures.Team(p);p.Team.SetEliteFighterStats(true);bracket.Teams[p.Team.UID]=p.Team;
            var armor=p.Equipment.Objects[2];armor.Plus=12;ConquerItemTable.UpdatePlus(armor);armorUid[i]=armor.UID;
            var necklace=new ConquerItem(true){UID=ConquerItem.ItemUID.Next,ID=120269,Position=2,Plus=12,Durability=1000,MaximDurability=1000};
            WorldValue("INSERT INTO items(uid,id,entityid,position,warehouse,plus) VALUES(@item,120269,@uid,2,0,12)","item",(long)necklace.UID,"uid",(long)p.Entity.UID);
            p.Equipment.Add(necklace,Enums.ItemUse.None);
            WorldValue("UPDATE items SET durability=1000,maximdurability=1000 WHERE uid=ANY(@ids)","ids",new long[]{armor.UID,necklace.UID});
            p.LoadItemStats();normalHp[i]=p.Entity.MaxHitpoints;
            Check((bool)Admission("Reserve",run,p),"Skill disconnect fixture reserves ordinary participant");entrants[p.Entity.UID]=p.Entity.PKMode;
            var xy=bracket.WaitingArea.RandomCoordinates();p.Entity.Teleport(bracket.WaitingArea.ID,xy.Item1,xy.Item2);
        }
        var match=(TP.Match)typeof(TP).GetMethod("CreateOwnedMatch",flags).Invoke(bracket,new object[]{players.Select(p=>p.Team.EliteFighterStats).ToArray()});match.ID=400002;bracket.Matches[match.ID]=match;
        match.Import();
        Check(bracket.Alive&&players.All(p=>p.Entity.MapID==match.Map.ID&&p.Entity.InSkillPk&&p.Entity.Hitpoints==p.Entity.MaxHitpoints),"successful Skill import admits both ordinary players at their effective full HP");
        Check(players.Select((p,i)=>p.Entity.MaxHitpoints<normalHp[i]).All(x=>x),"installed plus12 equipment produces genuinely reduced Skill HP");
        int imports=Convert.ToInt32(typeof(TP.Match).GetField("Imports",flags).GetValue(match));match.Import();
        Check(Convert.ToInt32(typeof(TP.Match).GetField("Imports",flags).GetValue(match))==imports,"repeated import of an active Skill match cannot reset participants");
        foreach(var p in players){Check(EntityTable.SaveEntity(p),"Skill arena character saves before session loss");Kernel.GamePool.Remove(p.Entity.UID);}
        bracket.Alive=false;TP.SkillTeamTournament.Tournaments=new TP[0];match.Map.Dispose();bracket.WaitingArea.Dispose();
        Ledger.GetField("ready",Hidden).SetValue(null,false);Call("Initialize");
        Check(Equals(Sql("SELECT state FROM revora_local_team_pk_runs WHERE run=@run","run",run),"cancelled")&&Convert.ToInt64(Sql("SELECT count(*) FROM revora_local_team_pk_admissions WHERE run=@run AND NOT returned","run",run))==2,"startup cancellation preserves both offline Skill return receipts");
        for(int i=0;i<players.Length;i++){
            var fresh=ReloadWorldPlayer(town,players[i].Entity.UID);ConquerItemTable.LoadItems(fresh);fresh.LoadItemStats();
            Check(fresh.Entity.MapID==1002&&fresh.Entity.X==327&&fresh.Entity.Y==303&&!fresh.Entity.InSkillPk&&fresh.Entity.PKMode==Enums.PKMode.Peace,"production login recovers destination and mode after the dynamic Skill arena is gone");
            Check(fresh.Entity.MaxHitpoints==normalHp[i]&&fresh.Equipment.Objects.Any(item=>item!=null&&item.UID==armorUid[i]&&item.Plus==12)&&fresh.Equipment.Objects[1].ID==120269&&fresh.Equipment.Objects[1].Plus==12,"production entity/item reload restores normal HP and original plus12 gear");
            Check((bool)Admission("Return",fresh,false)&&(bool)Admission("Return",fresh,false),"reconnected Skill return completes and replays safely");
        }
        Check(Convert.ToInt64(Sql("SELECT count(*) FROM revora_local_team_pk_admissions WHERE run=@run AND returned","run",run))==2&&Convert.ToInt64(Sql("SELECT cardinality(leaders) FROM revora_local_team_pk_runs WHERE run=@run","run",run))==0&&Convert.ToInt64(Sql("SELECT count(*) FROM revora_local_team_pk_claims WHERE run=@run","run",run))==0,"interrupted Skill match has durable returns and no invented winner or claim");
    }
    // One connection, fixed disposable PostgreSQL only. No payload logging.
    // Drop the server's successful COMMIT response before Npgsql receives it.
    sealed class CommitReplyDrop : IDisposable {
        readonly System.Net.Sockets.TcpListener listener;
        System.Net.Sockets.TcpClient peer,backend;
        readonly System.Threading.Tasks.Task worker;
        internal volatile bool Dropped;
        internal int Port {get{return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;}}
        static byte[] ReadExact(Stream stream,int size) {
            var bytes=new byte[size];int offset=0;
            while(offset<size){int count=stream.Read(bytes,offset,size-offset);if(count==0)throw new EndOfStreamException();offset+=count;}
            return bytes;
        }
        internal CommitReplyDrop() {
            listener=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0);listener.Start(1);
            worker=System.Threading.Tasks.Task.Run(()=> {
                System.Threading.Tasks.Task outbound=null;
                try {
                    peer=listener.AcceptTcpClient();backend=new System.Net.Sockets.TcpClient();backend.Connect(System.Net.IPAddress.Loopback,25439);
                    var client=peer.GetStream();var server=backend.GetStream();client.ReadTimeout=server.ReadTimeout=5000;client.WriteTimeout=server.WriteTimeout=5000;
                    outbound=System.Threading.Tasks.Task.Run(()=>{try{client.CopyTo(server);}catch(IOException){}catch(ObjectDisposedException){}});
                    while(true){
                        var header=ReadExact(server,5);int length=(header[1]<<24)|(header[2]<<16)|(header[3]<<8)|header[4];
                        if(length<4||length>1024*1024)throw new InvalidDataException("Private PostgreSQL response exceeds fixture bounds.");
                        var payload=ReadExact(server,length-4);
                        if(header[0]==(byte)'C'&&payload.SequenceEqual(new byte[]{67,79,77,77,73,84,0})){
                            Dropped=true;peer.Client.LingerState=new System.Net.Sockets.LingerOption(true,0);break;
                        }
                        client.Write(header,0,header.Length);client.Write(payload,0,payload.Length);
                    }
                } finally {
                    if(peer!=null)peer.Close();if(backend!=null)backend.Close();listener.Stop();
                    if(outbound!=null&&!outbound.Wait(5000))throw new TimeoutException("Private relay did not stop.");
                }
            });
        }
        public void Dispose() {
            listener.Stop();if(peer!=null)peer.Close();if(backend!=null)backend.Close();
            if(!worker.Wait(5000))throw new TimeoutException("Private COMMIT drop fixture did not stop.");
        }
    }
    static Exception LoseCommitReply(Action action) {
        string original=DataHolder.ConnectionString;
        var config=new NpgsqlConnectionStringBuilder(original);
        if(config.Host!="127.0.0.1"||config.Port!=25439||config.Database!="revora_candidate_game"||config.SearchPath!="local_team_pk_fixture")
            throw new InvalidOperationException("COMMIT drop fixture requires the isolated test schema.");
        using(var proxy=new CommitReplyDrop()) {
            config.Port=proxy.Port;config.Pooling=false;config.SslMode=SslMode.Disable;config.Timeout=5;config.CommandTimeout=5;
            Exception observed=null;
            try {DataHolder.ConnectionString=config.ConnectionString;action();}
            catch(TargetInvocationException error){observed=error.InnerException;}
            catch(NpgsqlException error){observed=error;}
            finally {DataHolder.ConnectionString=original;}
            Check(proxy.Dropped,"private relay observed a successful COMMIT and withheld its response");
            return observed;
        }
    }
    static void VerifySkillRoundRewards(Map town) {
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;var rounds=typeof(TP).Assembly.GetType("Revora.Game.Features.Tournaments.LocalSkillPkRounds");
        Func<GameState,bool> deliver=p=>(bool)rounds.GetMethod("Deliver",Hidden).Invoke(null,new object[]{p});
        var bracket=new TP(3,TP.GamePackets.SkillElitePkBrackets);bracket.Alive=true;TP.SkillTeamTournament.Tournaments=new TP[4];TP.SkillTeamTournament.Tournaments[3]=bracket;
        var run=Guid.NewGuid();Call("Begin",run,TP.GamePackets.SkillElitePkBrackets,3,DateTime.UtcNow.AddDays(-6));typeof(TP).GetField("ledgerRun",flags).SetValue(bracket,run);
        var players=new[]{TournamentPlayer(town),TournamentPlayer(town)};
        foreach(var p in players){p.Entity.DailySignDays=new byte[31];p.Team=new Revora.Game.ConquerStructures.Team(p);p.Team.SetEliteFighterStats(true);bracket.Teams[p.Team.UID]=p.Team;Check((bool)Admission("Reserve",run,p),"round reward participant is durably admitted");var xy=bracket.WaitingArea.RandomCoordinates();p.Entity.Teleport(bracket.WaitingArea.ID,xy.Item1,xy.Item2);}
        var match=(TP.Match)typeof(TP).GetMethod("CreateOwnedMatch",flags).Invoke(bracket,new object[]{players.Select(p=>p.Team.EliteFighterStats).ToArray()});match.ID=400003;bracket.Matches[match.ID]=match;match.Import();
        var bag=typeof(Revora.Game.ConquerStructures.Inventory).GetField("objects",flags);bag.SetValue(players[0].Inventory,new ConquerItem[40]);
        Sql("CREATE FUNCTION reject_skill_round() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'fixture'; END $$; CREATE TRIGGER reject_skill_round BEFORE INSERT ON revora_skill_pk_round_rewards FOR EACH ROW EXECUTE PROCEDURE reject_skill_round();");
        match.End(players[1].Team);
        Check(!match.Done&&players[1].Team.EliteFighterStats.Fighting&&Convert.ToInt64(Sql("SELECT count(*) FROM revora_skill_pk_rounds WHERE run=@run","run",run))==0,"entitlement insertion failure rolls back round and leaves match outcome retryable");
        Sql("DROP TRIGGER reject_skill_round ON revora_skill_pk_round_rewards;DROP FUNCTION reject_skill_round();");
        var pending=typeof(TP.Match).GetField("pendingRound",flags).GetValue(match);
        var record=rounds.GetMethod("Record",Hidden);
        var commitError=LoseCommitReply(()=>record.Invoke(null,new[]{pending}));
        Check(commitError is NpgsqlException,"round recorder sees a genuine transport failure after PostgreSQL commits");
        Check(!match.Done&&players[1].Team.EliteFighterStats.Fighting
            &&Convert.ToInt64(Sql("SELECT count(*) FROM revora_skill_pk_rounds WHERE run=@run","run",run))==1,
            "round commit can precede match advancement while the frozen outcome remains retryable");
        System.Threading.Tasks.Parallel.For(0,8,i=>record.Invoke(null,new[]{pending}));
        Check(Convert.ToInt64(Sql("SELECT count(*) FROM revora_skill_pk_round_rewards WHERE run=@run","run",run))==1,
            "concurrent retries after a committed round preserve one entitlement");
        foreach(string field in new[]{"Winner","Loser","Owners"}) {
            var altered=Activator.CreateInstance(pending.GetType(),true);
            foreach(var member in pending.GetType().GetFields(flags))member.SetValue(altered,member.GetValue(pending));
            var changed=altered.GetType().GetField(field,flags);
            if(field=="Owners")changed.SetValue(altered,new[]{(long)players[1].Entity.UID});
            else changed.SetValue(altered,(long)(field=="Winner"?players[0].Team.UID:players[1].Team.UID)+1000000L);
            bool refused=false;try{record.Invoke(null,new[]{altered});}
            catch(TargetInvocationException error){if(!(error.InnerException is InvalidOperationException))throw;refused=true;}
            Check(refused,"committed Skill round rejects changed "+field);
        }
        Check(Convert.ToInt64(Sql("SELECT winner FROM revora_skill_pk_rounds WHERE run=@run","run",run))==players[0].Team.UID
            &&Convert.ToInt64(Sql("SELECT loser FROM revora_skill_pk_rounds WHERE run=@run","run",run))==players[1].Team.UID
            &&((long[])Sql("SELECT owners FROM revora_skill_pk_rounds WHERE run=@run","run",run)).SequenceEqual(new[]{(long)players[0].Entity.UID}),
            "rejected outcome changes leave the original winner loser and reward owner intact");
        match.End(players[0].Team);match.End(players[1].Team);
        Check(players[1].Team.EliteFighterStats.Lost&&!players[0].Team.EliteFighterStats.Lost,
            "retry with the opposite requested loser follows the already frozen committed outcome");
        Check(match.Done&&Convert.ToInt64(Sql("SELECT count(*) FROM revora_skill_pk_rounds WHERE run=@run","run",run))==1&&Convert.ToInt64(Sql("SELECT count(*) FROM revora_skill_pk_round_rewards WHERE run=@run AND item_uid IS NULL","run",run))==1,"full bag retains one atomic round outcome and undelivered entitlement after repeated End");
        Check(deliver(players[1])&&!players[1].Inventory.Objects.Any(item=>item.ID==720981),"losing participant has no round reward");
        bag.SetValue(players[0].Inventory,new ConquerItem[0]);Call("Cancel",run);bracket.Alive=false;
        record.Invoke(null,new[]{pending});
        var newRound=Activator.CreateInstance(pending.GetType(),true);
        foreach(var member in pending.GetType().GetFields(flags))member.SetValue(newRound,member.GetValue(pending));
        pending.GetType().GetField("Round",flags).SetValue(newRound,2);
        bool cancelledRefused=false;try{record.Invoke(null,new[]{newRound});}
        catch(TargetInvocationException error){if(!(error.InnerException is InvalidOperationException))throw;cancelledRefused=true;}
        Check(cancelledRefused&&Convert.ToInt64(Sql("SELECT count(*) FROM revora_skill_pk_rounds WHERE run=@run","run",run))==1
            &&Convert.ToInt64(Sql("SELECT count(*) FROM revora_skill_pk_round_rewards WHERE run=@run","run",run))==1,
            "cancelled run permits exact earned-round replay but cannot create a new round or entitlement");
        Check((bool)Admission("Return",players[0],true)&&EntityTable.SaveEntity(players[0]),"earned round owner returns and saves before delivery interruption");
        Sql("CREATE FUNCTION reject_skill_item() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.id=720981 THEN RAISE EXCEPTION 'fixture'; END IF; RETURN NEW; END $$; CREATE TRIGGER reject_skill_item BEFORE INSERT ON items FOR EACH ROW EXECUTE PROCEDURE reject_skill_item();");
        Check(!deliver(players[0])&&players[0].Action==0&&Sql("SELECT item_uid FROM revora_skill_pk_round_rewards WHERE run=@run","run",run)==DBNull.Value,"failed item insert preserves pending receipt and fences stale session");
        Sql("DROP TRIGGER reject_skill_item ON items;DROP FUNCTION reject_skill_item();");
        var fresh=ReloadWorldPlayer(town,players[0].Entity.UID);ConquerItemTable.LoadItems(fresh);
        bool delivered=false;LoseCommitReply(()=>delivered=deliver(fresh));
        long committedItem=Convert.ToInt64(Sql("SELECT item_uid FROM revora_skill_pk_round_rewards WHERE run=@run","run",run));
        Check(!delivered&&fresh.Action==0&&committedItem>0&&!fresh.Inventory.Objects.Any(item=>item.ID==720981)
            &&Convert.ToInt64(Sql("SELECT count(*) FROM items WHERE uid=@item AND entityid=@uid AND id=720981","item",committedItem,"uid",(long)fresh.Entity.UID))==1,
            "lost delivery COMMIT response fences the stale session while preserving the exact committed item and receipt");
        fresh=ReloadWorldPlayer(town,players[0].Entity.UID);ConquerItemTable.LoadItems(fresh);
        Check(fresh.Inventory.Objects.Count(item=>item.ID==720981&&item.UID==(uint)committedItem)==1,
            "ordinary production reload recovers the item whose successful delivery reply was lost");
        var admissions=typeof(TP).Assembly.GetType("Revora.Game.Features.Tournaments.LocalTeamPkAdmissions");
        admissions.GetField("nextReturn",Hidden).SetValue(null,DateTime.MinValue);admissions.GetField("cursor",Hidden).SetValue(null,0L);
        Admission("Tick");
        Check(fresh.Inventory.Objects.Count(item=>item.ID==720981)==1,"ordinary background pulse preserves the reloaded committed reward after its delivery response was lost");
        System.Threading.Tasks.Parallel.For(0,8,i=>deliver(fresh));
        var prize=fresh.Inventory.Objects.Single(item=>item.ID==720981);
        Check(!prize.Bound&&prize.Days==0&&Convert.ToInt64(Sql("SELECT item_uid FROM revora_skill_pk_round_rewards WHERE run=@run","run",run))==prize.UID,"background delivery and concurrent replay after cancelled-run reload retain one original unbound pack and exact receipt");
        Check(deliver(fresh)&&fresh.Inventory.Objects.Count(item=>item.ID==720981)==1,"round reward delivery replay cannot duplicate the pack");
        foreach(var item in fresh.Inventory.Objects)fresh.Inventory.Remove(item.UID,Enums.ItemUse.None,false);fresh.Equipment=new Revora.Game.ConquerStructures.Equipment(fresh);ConquerItemTable.LoadItems(fresh);
        Check(fresh.Inventory.Objects.Count(item=>item.UID==prize.UID&&item.ID==720981)==1,"production inventory reload retains the exact round reward");
        Check((bool)Admission("Return",players[1],true),"loser returns without a reward");match.Map.Dispose();bracket.WaitingArea.Dispose();
    }
    static void VerifySkillFinals(Map town) {
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;var bracket=new TP(3,TP.GamePackets.SkillElitePkBrackets);bracket.Alive=true;
        TP.SkillTeamTournament.Tournaments=new TP[4];TP.SkillTeamTournament.Tournaments[3]=bracket;
        var run=Guid.NewGuid();Call("Begin",run,TP.GamePackets.SkillElitePkBrackets,3,DateTime.UtcNow.AddDays(-7));typeof(TP).GetField("ledgerRun",flags).SetValue(bracket,run);
        typeof(TP).GetField("AllowJoin",flags).SetValue(bracket,true);typeof(TP).GetField("StarTimer",flags).SetValue(bracket,DateTime.Now.AddMinutes(15));
        TP.SkillTeamTournament.Opened=true;var players=new List<GameState>();
        for(int i=0;i<33;i++){
            var leader=TournamentPlayer(town);leader.Team=new Revora.Game.ConquerStructures.Team(leader);var member=TournamentPlayer(town);leader.Team.Add(member);
            foreach(var p in new[]{leader,member}){
                players.Add(p);p.Entity.Teleport(1002,301,146);p.ActiveNpc=801717;
                Npcs.GetDialog(new NpcRequest(true){NpcID=801717,OptionID=2},p);
                Check(p.Entity.MapID==bracket.WaitingArea.ID,"ordinary Skill NPC admission reaches the waiting area");
            }
        }
        var priorPool=Server.GamePool;Server.GamePool=players.ToArray();bool threeWay=false,knockoutClock=false,finalClock=false;
        try {
            for(int pulse=0;pulse<50&&bracket.State!=ElitePkStatus.States.GUI_ReconstructTop;pulse++){
                typeof(TP).GetField("StarTimer",flags).SetValue(bracket,DateTime.Now.AddMinutes(-2));typeof(TP).GetField("pStamp",flags).SetValue(bracket,Time32.Now.AddSeconds(-90));
                foreach(var match in bracket.Matches.Values)if(match.Inside&&!match.Done){
                    if(match.MatchStats.Length==3)threeWay=true;
                    int minutes=(int)typeof(TP.Match).GetField("RoundMinutes",flags).GetValue(match);
                    bool finalStage=bracket.State==ElitePkStatus.States.GUI_Top4Qualifier||bracket.State==ElitePkStatus.States.GUI_Top2Qualifier||bracket.State==ElitePkStatus.States.GUI_Top3||bracket.State==ElitePkStatus.States.GUI_Top1;
                    if(minutes!=(finalStage?15:10))throw new InvalidOperationException("Actual Skill scheduler created a match with the wrong duration.");
                    if(finalStage)finalClock=true;else knockoutClock=true;
                    match.ImportTime=Time32.Now.AddSeconds(-1000);
                    foreach(var fighter in match.MatchStats.Where(f=>f.Fighting)){
                        if(fighter.Team.Players.Any(p=>p.Entity.MapID!=match.Map.ID))throw new InvalidOperationException("Skill finals participant failed import.");fighter.Points=fighter.LeaderUID;
                    }
                }
                typeof(TP).GetMethod("timerCallback",flags).Invoke(bracket,new object[]{0});
                if(!bracket.Alive)throw new InvalidOperationException("Skill finals bracket cancelled unexpectedly.");
            }
            Check(bracket.State==ElitePkStatus.States.GUI_ReconstructTop&&bracket.Top8.Length==8,"Skill series progresses from 33 ordinary teams through its complete top-eight final");
            Check(knockoutClock&&finalClock,"actual Skill bracket allocates ten-minute knockout and fifteen-minute top-eight clocks");
            Check(threeWay&&Convert.ToInt64(Sql("SELECT count(*) FROM revora_skill_pk_rounds WHERE run=@run AND round=2","run",run))>0,"three-team switch rounds have distinct persisted second-round identities");
            Check(Equals(Sql("SELECT state FROM revora_local_team_pk_runs WHERE run=@run","run",run),"complete")&&bracket.Top8[0].LeaderUID==players.Where(p=>p.Team.TeamLider(p)).Max(p=>p.Entity.UID),"complete Skill result freezes the highest scoring leader");
            Check(Convert.ToInt64(Sql("SELECT count(*) FROM revora_skill_pk_rounds WHERE run=@run AND cardinality(owners)<>2","run",run))==0&&Convert.ToInt64(Sql("SELECT count(*) FROM revora_skill_pk_round_rewards WHERE run=@run","run",run))==2*Convert.ToInt64(Sql("SELECT count(*) FROM revora_skill_pk_rounds WHERE run=@run","run",run)),"every earned round owns exactly one entitlement for each imported winning member");
            bracket.ConstructTop8=DateTime.Now.AddMinutes(-3);typeof(TP).GetMethod("timerCallback",flags).Invoke(bracket,new object[]{0});
            Check(!bracket.Alive&&players.All(p=>p.Entity.MapID==1002&&!p.Entity.InSkillPk)&&Convert.ToInt64(Sql("SELECT count(*) FROM revora_local_team_pk_admissions WHERE run=@run AND returned","run",run))==66,"Skill finals return all 66 ordinary participants durably");
            var winner=players.Single(p=>p.Entity.UID==bracket.Top8[0].LeaderUID);
            {
                winner.Entity.Teleport(1002,301,146);winner.ActiveNpc=801717;Npcs.GetDialog(new NpcRequest(true){NpcID=801717,OptionID=3},winner);
                Check(winner.Entity.ConquerPoints==10000&&winner.Inventory.Objects.Count(item=>item.Bound&&ConquerItemInformation.BaseInformations[item.ID].PurificationLevel==7)==1,"ordinary Skill final leader earns 10000 CP and one bound P7 through the claim service");
                Npcs.GetDialog(new NpcRequest(true){NpcID=801717,OptionID=3},winner);Check(winner.Entity.ConquerPoints==10000&&Convert.ToInt64(Sql("SELECT count(*) FROM revora_local_team_pk_claims WHERE run=@run","run",run))==1,"Skill final placement replay is receipt protected");
            }
            Check(Convert.ToInt64(Sql("SELECT count(*) FROM revora_skill_pk_round_rewards r JOIN items i ON i.uid=r.item_uid AND i.entityid=r.uid WHERE r.run=@run AND i.id=720981","run",run))==Convert.ToInt64(Sql("SELECT count(*) FROM revora_skill_pk_round_rewards WHERE run=@run","run",run)),"all round rewards retain exact persisted pack receipts through the final");
        } finally {Server.GamePool=priorPool;TP.SkillTeamTournament.Opened=false;bracket.Alive=false;bracket.WaitingArea.Dispose();}
    }
    static void VerifyWorldBracket() {
        const string runtime=@"C:\Revora\DisposablePostgres\moon-box-candidate\game";
        string prior=Environment.CurrentDirectory;
        try {
            Environment.CurrentDirectory=runtime;
            Sql("DROP TABLE entities;CREATE TABLE entities AS TABLE public.entities WITH NO DATA;CREATE TABLE skills AS TABLE public.skills WITH NO DATA;CREATE TABLE arena AS TABLE public.arena WITH NO DATA;CREATE TABLE titles (LIKE public.titles INCLUDING ALL);CREATE TABLE status (LIKE public.status INCLUDING ALL);CREATE TABLE chi (LIKE public.chi INCLUDING ALL);CREATE TABLE npcs AS TABLE public.npcs WITH NO DATA;CREATE TABLE sobnpcs AS TABLE public.sobnpcs WITH NO DATA;CREATE TABLE monsterspawns AS TABLE public.monsterspawns WITH NO DATA;CREATE TABLE furniture AS TABLE public.furniture WITH NO DATA;");
            StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
            Kernel.GamePool.Clear();Kernel.Maps.Clear();
            var town=TournamentMap(runtime,1002);TournamentMap(runtime,700);TournamentMap(runtime,2068);
            Call("PlaceNpc");Call("PlaceNpc");
            var parked=new Dictionary<uint,Tuple<ushort,ushort,ushort>>();
            Revora.Game.Features.TwinCityPlaza.Apply((id,map,x,y)=>parked[id]=Tuple.Create(map,x,y));
            Check(!parked.ContainsKey(417171)&&!parked.ContainsKey(801717)&&!Revora.Game.Features.TwinCityPlaza.IsClosed(801717)&&town.Npcs.ContainsKey(801717),"both tournament clerks survive repeated placement and plaza cleanup with Skill dialogue open");

            Check(town.Npcs.ContainsKey(417171) && town.Floor[293,146,MapObjectType.Player],"public clerk placement retains an open invitation landing");
            foreach(string table in new[]{"teamarena","championarena","quests","reward","rewardclameid","merchant","dialyquests","activeness","house","pkexploits","achievement"}) {
                // Fixed, source-controlled fixture table names; no player input.
                Sql("CREATE TABLE IF NOT EXISTS "+table+" AS TABLE public."+table+" WITH NO DATA");
            }
            Sql("CREATE TABLE IF NOT EXISTS prizes(uid bigint PRIMARY KEY,name text,prizes bytea)");
            Sql("CREATE TABLE IF NOT EXISTS itemadding (LIKE public.itemadding INCLUDING ALL)");
            VerifyConcurrentOpening();VerifyConcurrentOpening(true);
            VerifySkillImportRecovery(town,false);VerifySkillImportRecovery(town,true);VerifySkillDisconnectRecovery(town);VerifySkillOwnership(town);VerifySkillRoundRewards(town);VerifySkillFinals(town);
            var eventType=Revora.Game.Features.Tournaments.TeamElitePk.GamePackets.TeamElitePkBrackets;
            var tournament=new Revora.Game.Features.Tournaments.TeamElitePk(3,eventType);
            Revora.Game.Features.Tournaments.TeamElitePk.TeamTournament.Tournaments=new Revora.Game.Features.Tournaments.TeamElitePk[4];
            Revora.Game.Features.Tournaments.TeamElitePk.TeamTournament.Tournaments[3]=tournament;
            // Production Solo Elite PK uses an empty legacy bracket array.
            Revora.Game.Features.Tournaments.ElitePKTournament.Tournaments=new Revora.Game.ElitePK[0];
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var round=Guid.NewGuid();Call("Begin",round,eventType,3,DateTime.UtcNow.AddSeconds(-1));
            tournament.GetType().GetField("ledgerRun",flags).SetValue(tournament,round);
            tournament.GetType().GetField("AllowJoin",flags).SetValue(tournament,true);
            tournament.GetType().GetField("StarTimer",flags).SetValue(tournament,DateTime.Now.AddMinutes(1));
            Revora.Game.Features.Tournaments.TeamElitePk.TeamTournament.Opened=true;
            var players=new List<GameState>();
            for(int i=0;i<25;i++) {
                var p=TournamentPlayer(town);p.Team=new Revora.Game.ConquerStructures.Team(p);players.Add(p);
                var members=new List<GameState>();
                for(int slot=0;slot<4;slot++) {
                    var member=TournamentPlayer(town);member.Entity.PKMode=Enums.PKMode.Peace;p.Team.Add(member);players.Add(member);members.Add(member);
                }
                Check(p.Team.Members.Count==5 && members.All(member=>ReferenceEquals(member.Team,p.Team)),"ordinary team add retains all five members");
                Check(!Revora.Game.Features.Tournaments.TeamElitePk.TeamTournament.Join(p,3),"distant player cannot enter through a forged join");
                p.Entity.Teleport(1002,293,146);p.ActiveNpc=417171;
                Npcs.GetDialog(new NpcRequest(true){NpcID=417171,OptionID=2},p);
                Check(p.Entity.MapID==tournament.WaitingArea.ID,"ordinary NPC entry reaches its bracket waiting area");
                var fighter=p.Team.EliteFighterStats;
                foreach(var member in members) {
                    member.Entity.Teleport(1002,293,146);member.ActiveNpc=417171;
                    Npcs.GetDialog(new NpcRequest(true){NpcID=417171,OptionID=2},member);
                }
                Check(members.All(member=>member.Entity.MapID==tournament.WaitingArea.ID) && ReferenceEquals(p.Team.EliteFighterStats,fighter),"all five members enter without replacing registered fighter state");
            }
            Check(Convert.ToInt64(Sql("SELECT count(*) FROM revora_local_team_pk_admissions WHERE run=@run AND NOT returned","run",round))==125,"all ordinary entries have durable admissions before the bracket starts");
            Check(!(bool)Admission("Reserve",round,players[0]),"duplicate admission cannot replace the original return record");
            Check((bool)Admission("Return",players[0],false) && players[0].Entity.MapID==tournament.WaitingArea.ID,"recovery leaves an admitted current participant in the active bracket");
            var originalModes=players.ToDictionary(p=>p.Entity.UID,p=>p.Entity.PKMode);
            Server.GamePool=players.ToArray();bool combatChecked=false;
            for(int pulse=0;pulse<40 && tournament.State!=Revora.Game.Features.Tournaments.ElitePkStatus.States.GUI_ReconstructTop;pulse++) {
                tournament.GetType().GetField("StarTimer",flags).SetValue(tournament,DateTime.Now.AddMinutes(-2));
                tournament.GetType().GetField("pStamp",flags).SetValue(tournament,Time32.Now.AddSeconds(-90));
                foreach(var match in tournament.Matches.Values)if(match.Inside && !match.Done){
                    if(!combatChecked && match.MatchStats.Count(f=>f.Fighting)>=2){VerifyDamageRoute(match);combatChecked=true;}
                    match.ImportTime=Time32.Now.AddSeconds(-120);
                    foreach(var fighter in match.MatchStats.Where(f=>f.Fighting)) {
                        if(fighter.Team.Players.Any(p=>p.Entity.MapID!=match.Map.ID))throw new InvalidOperationException("match import did not reach its actual arena");
                        fighter.Points=fighter.LeaderUID;
                    }
                }
                tournament.GetType().GetMethod("timerCallback",flags).Invoke(tournament,new object[]{0});
            }
            Check(Convert.ToInt64(Sql("SELECT count(*) FROM revora_local_team_pk_admissions a JOIN items i ON i.uid=a.entry_item AND i.entityid=a.uid WHERE a.run=@run AND a.entry_earned AND i.id=720793 AND i.bound=0","run",round))==125,"registration awards exactly one persisted unbound participation item per ordinary entrant");
            Admission("EarnEntry",round,players.Select(p=>(long)p.Entity.UID).ToArray());
            foreach(var entrant in players)Admission("DeliverEntry",entrant);
            Check(Convert.ToInt64(Sql("SELECT count(*) FROM items WHERE id=720793"))==125,"registration award replay cannot duplicate participation items");
            Check(tournament.State==Revora.Game.Features.Tournaments.ElitePkStatus.States.GUI_ReconstructTop,"twenty-five-team bracket reaches its saved final through actual match import/export");
            Check(Convert.ToInt64(Sql("SELECT count(*) FROM revora_local_team_pk_runs WHERE run=@run AND state='complete' AND cardinality(leaders)=8","run",round))==1,"earned final has eight durable placements");
            var winner=players.Single(p=>p.Entity.UID==tournament.Top8[0].LeaderUID);
            Check(winner.Entity.UID==players.Where(p=>p.Team.TeamLider(p)).Max(p=>p.Entity.UID),"highest scoring ordinary team wins the complete bracket");
            var heldMap=tournament.Matches.Values.First().Map;
            // Replace an admitted member's session before final return. A new session
            // cannot be trusted to carry the old session's PrevPKMode field.
            var stale=players.First(p=>!p.Team.TeamLider(p));
            var replacement=TournamentPlayer(town);uint replacementPlaceholder=replacement.Entity.UID;
            Kernel.GamePool.Remove(replacementPlaceholder);
            replacement.Entity.UID=stale.Entity.UID;replacement.Account.EntityID=stale.Entity.UID;
            replacement.BindWorldMap(heldMap.ID);replacement.Entity.PKMode=Enums.PKMode.Team;
            replacement.Entity.PrevPKMode=Enums.PKMode.Capture;
            Kernel.GamePool[stale.Entity.UID]=replacement;
            players[players.IndexOf(stale)]=replacement;
            var spectator=WorldPlayer(heldMap,false,50,50,15);
            tournament.ConstructTop8=DateTime.Now.AddMinutes(-3);
            tournament.GetType().GetMethod("timerCallback",flags).Invoke(tournament,new object[]{0});
            Check(tournament.Alive && Kernel.Maps.ContainsKey(heldMap.ID),"occupied match map defers final cleanup without evicting a spectator");
            Check(replacement.Entity.MapID==1002 && replacement.Entity.X==327 && replacement.Entity.Y==303
                && replacement.Entity.PKMode==originalModes[stale.Entity.UID],"final return resolves the current session and restores the admission PK mode");
            Check(stale.Entity.MapID==tournament.WaitingArea.ID,"final return does not mutate the replaced session");
            spectator.BindWorldMap(1002);
            tournament.GetType().GetMethod("timerCallback",flags).Invoke(tournament,new object[]{0});
            Check(Kernel.Maps.Count==4 && Kernel.Maps.ContainsKey(700) && Kernel.Maps.ContainsKey(tournament.WaitingArea.ID),"final cleanup removes only owned battle maps and retains base/waiting maps");
            Check(!tournament.Alive && players.All(p=>p.Entity.MapID==1002 && p.Entity.X==327 && p.Entity.Y==303 && p.Entity.PKMode==originalModes[p.Entity.UID]),"completed bracket returns all ordinary entrants and restores their original PK mode: "+string.Join(";",players.Select(p=>p.Entity.UID+":"+p.Entity.MapID+":"+p.Entity.X+","+p.Entity.Y+":"+p.Entity.PKMode+":"+p.Entity.PrevPKMode)));
            Check(Convert.ToInt64(Sql("SELECT count(*) FROM revora_local_team_pk_admissions WHERE run=@run AND returned","run",round))==125,"final cleanup durably acknowledges every returned entrant");
            winner.Entity.Teleport(1002,293,146);winner.ActiveNpc=417171;
            Npcs.GetDialog(new NpcRequest(true){NpcID=417171,OptionID=3},winner);
            Check(winner.Entity.Money==1000000000 && winner.Inventory.Objects.Count(i=>i.Bound && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)==1,"bracket winner receives the durable silver and bound soul");
            var teammate=winner.Team.Players.First(p=>!ReferenceEquals(p,winner));
            teammate.Entity.Teleport(1002,293,146);teammate.ActiveNpc=417171;
            Npcs.GetDialog(new NpcRequest(true){NpcID=417171,OptionID=3},teammate);
            Check(teammate.Entity.Money==0 && !teammate.Inventory.Objects.Any(i=>ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)
                && Convert.ToInt64(Sql("SELECT count(*) FROM revora_local_team_pk_claims WHERE run=@run AND uid=@uid","run",round,"uid",(long)teammate.Entity.UID))==0,"winning member cannot claim the leader-only placement purse");
            var eliminated=players.First(p=>p.Team.TeamLider(p) && !tournament.Top8.Any(f=>f.LeaderUID==p.Entity.UID));
            eliminated.Entity.Teleport(1002,293,146);eliminated.ActiveNpc=417171;
            Npcs.GetDialog(new NpcRequest(true){NpcID=417171,OptionID=3},eliminated);
            Check(eliminated.Entity.Money==0 && !eliminated.Inventory.Objects.Any(i=>ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)
                && Convert.ToInt64(Sql("SELECT count(*) FROM revora_local_team_pk_claims WHERE run=@run AND uid=@uid","run",round,"uid",(long)eliminated.Entity.UID))==0,"leader eliminated before top eight has no placement purse");
            var soul=winner.Inventory.Objects.Single(i=>i.Bound && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7);
            Sql("CREATE TABLE IF NOT EXISTS itemadding (LIKE public.itemadding INCLUDING ALL)");
            foreach(var item in winner.Inventory.Objects)winner.Inventory.Remove(item.UID,Enums.ItemUse.None,false);
            winner.Equipment=new Revora.Game.ConquerStructures.Equipment(winner);
            ConquerItemTable.LoadItems(winner);
            Check(winner.Inventory.Objects.Count(i=>i.UID==soul.UID && i.ID==soul.ID && i.Bound && i.Days==0)==1,"production inventory reload preserves the exact earned bound permanent soul");
            winner.Entity.DailySignDays=new byte[31];
            Check(EntityTable.SaveEntity(winner),"earned team winner profile saves before reconnect");
            var fresh=WorldPlayer(town,false,327,303,15);uint placeholder=fresh.Entity.UID;
            foreach(var f in typeof(Screen).GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance))
                if(f.GetValue(fresh.Screen)==null && f.FieldType.IsGenericType && f.FieldType.GetConstructor(Type.EmptyTypes)!=null)f.SetValue(fresh.Screen,Activator.CreateInstance(f.FieldType));
            fresh.Trade=new Revora.Game.ConquerStructures.Trade();
            fresh.Account.EntityID=winner.Entity.UID;fresh.Account.State=AccountTable.AccountState.Player;fresh.Account.Username="team_pk_world_fixture";fresh.Quests=new Quests(fresh);
            Check((bool)typeof(EntityTable).GetMethod("LoadEntityState",Hidden).Invoke(null,new object[]{fresh}),"production entity state loader restores the winning character");
            Kernel.GamePool.Remove(placeholder);Kernel.GamePool[winner.Entity.UID]=fresh;fresh.Action=2;fresh.Entity.FullyLoaded=true;
            fresh.InnerPower=new InnerPower(fresh.Entity.Name,fresh.Entity.UID);fresh.RunesItem=new SafeDictionary<uint,ConquerItem>();fresh.ChiData=new ChiTable.ChiData{Powers=fresh.ChiPowers};
            ConquerItemTable.LoadItems(fresh);
            Check(fresh.Entity.ConquerPoints==0 && fresh.Entity.RevoraPoint==0 && fresh.Entity.Money==1000000000 && fresh.Inventory.Objects.Count(i=>i.UID==soul.UID && i.ID==soul.ID && i.Bound && i.Days==0)==1,"fresh character and inventory loaders retain earned balances and bound soul");
            var recoveryRun=Guid.NewGuid();Call("Begin",recoveryRun,eventType,3,DateTime.UtcNow);
            var offline=TournamentPlayer(town);offline.Entity.PKMode=Enums.PKMode.Peace;offline.Entity.DailySignDays=new byte[31];
            Check((bool)Admission("Reserve",recoveryRun,offline),"offline-recovery fixture uses normal durable admission");
            offline.Entity.Teleport(tournament.WaitingArea.ID,50,50);
            Check(EntityTable.SaveEntity(offline),"interrupted entrant location is saved before session loss");
            Admission("EarnEntry",recoveryRun,new[]{(long)offline.Entity.UID});
            var bagField=typeof(Revora.Game.ConquerStructures.Inventory).GetField("objects",BindingFlags.Instance|BindingFlags.NonPublic);
            bagField.SetValue(offline.Inventory,new ConquerItem[40]);
            Check(!(bool)Admission("DeliverEntry",offline) && Sql("SELECT entry_item FROM revora_local_team_pk_admissions WHERE run=@run","run",recoveryRun)==DBNull.Value,"full bag retains the earned participation reward without a receipt");
            bagField.SetValue(offline.Inventory,new ConquerItem[0]);
            Sql("CREATE FUNCTION reject_entry_item() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'fixture'; END $$; CREATE TRIGGER reject_entry_item BEFORE INSERT ON items FOR EACH ROW EXECUTE PROCEDURE reject_entry_item();");
            Check(!(bool)Admission("DeliverEntry",offline) && offline.Action==0
                && Sql("SELECT entry_item FROM revora_local_team_pk_admissions WHERE run=@run","run",recoveryRun)==DBNull.Value,"failed participation item insertion leaves the earned entitlement unpaid");
            Sql("DROP TRIGGER reject_entry_item ON items;DROP FUNCTION reject_entry_item();");
            Kernel.GamePool.Remove(offline.Entity.UID);
            Ledger.GetField("ready",Hidden).SetValue(null,false);Call("Initialize");
            Check(Equals(Sql("SELECT state FROM revora_local_team_pk_runs WHERE run=@run","run",recoveryRun),"cancelled")
                && Equals(Sql("SELECT returned FROM revora_local_team_pk_admissions WHERE run=@run","run",recoveryRun),false),"startup cancels the run without discarding an offline return");
            var recovered=ReloadWorldPlayer(town,offline.Entity.UID);
            Check(recovered.Entity.MapID==1002 && recovered.Entity.X==327 && recovered.Entity.Y==303 && recovered.Entity.PKMode==Enums.PKMode.Peace
                && Equals(Sql("SELECT returned FROM revora_local_team_pk_admissions WHERE run=@run","run",recoveryRun),false),"login projects safe destination and original mode without prematurely acknowledging recovery");
            Sql("CREATE FUNCTION reject_return() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.returned THEN RAISE EXCEPTION 'fixture'; END IF; RETURN NEW; END $$; CREATE TRIGGER reject_return BEFORE UPDATE ON revora_local_team_pk_admissions FOR EACH ROW EXECUTE PROCEDURE reject_return();");
            Check(!(bool)Admission("Return",recovered,false) && recovered.Action==0
                && Equals(Sql("SELECT returned FROM revora_local_team_pk_admissions WHERE run=@run","run",recoveryRun),false),"failed return commit preserves pending recovery and fences the session");
            Sql("DROP TRIGGER reject_return ON revora_local_team_pk_admissions;DROP FUNCTION reject_return();");
            recovered=ReloadWorldPlayer(town,offline.Entity.UID);
            Check((bool)Admission("Return",recovered,false)
                && Equals(Sql("SELECT returned FROM revora_local_team_pk_admissions WHERE run=@run","run",recoveryRun),true)
                && Convert.ToInt64(Sql("SELECT count(*) FROM entities WHERE uid=@uid AND mapid=1002 AND x=327 AND y=303","uid",(long)offline.Entity.UID))==1,"fresh session completes the pending return with a durable town destination");
            Check((bool)Admission("Return",recovered,false) && !(bool)Admission("Reserve",recoveryRun,recovered),"return replay is harmless and a cancelled run cannot readmit the character");
            System.Threading.Tasks.Parallel.For(0,8,i=>Admission("DeliverEntry",recovered));
            Check(recovered.Inventory.Objects.Count(i=>i.ID==720793 && !i.Bound)==1
                && Convert.ToInt64(Sql("SELECT count(*) FROM revora_local_team_pk_admissions a JOIN items i ON i.uid=a.entry_item AND i.entityid=a.uid WHERE a.run=@run AND a.returned AND a.entry_earned AND i.id=720793","run",recoveryRun))==1,"concurrent delivery after recovery pays a deferred participation reward once");
            foreach(var item in recovered.Inventory.Objects)recovered.Inventory.Remove(item.UID,Enums.ItemUse.None,false);
            recovered.Equipment=new Revora.Game.ConquerStructures.Equipment(recovered);ConquerItemTable.LoadItems(recovered);
            Check(recovered.Inventory.Objects.Count(i=>i.ID==720793 && !i.Bound)==1,"production item reload retains the participation reward");
            winner=fresh;
            winner.Entity.Teleport(1002,293,146);winner.ActiveNpc=417171;
            Npcs.GetDialog(new NpcRequest(true){NpcID=417171,OptionID=3},winner);
            Check(winner.Entity.Money==1000000000 && winner.Inventory.Objects.Count(i=>i.UID==soul.UID)==1,"ordinary NPC replay after item reload does not duplicate payment");
        } finally {Environment.CurrentDirectory=prior;}
    }
}
