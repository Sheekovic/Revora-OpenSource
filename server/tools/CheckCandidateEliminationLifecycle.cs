using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.Attacking;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static void PrepareElimination() {
        var type=Feature("EliminationDenWorld");
        foreach(string path in new[]{"maps/1021.map","map/map/mine.dmap","MAP/MAP/MINE.DMAP"})Require((bool)Call(type,"MatchesDenIndex",path));
        foreach(string path in new[]{"maps/1020.map","map/map/other.dmap","../maps/1021.map","C:/maps/1021.map","map/map/mine.7z",""})Require(!(bool)Call(type,"MatchesDenIndex",path));
        Feature("EliminationLifecycle").GetField("ReleaseReady",Hidden).SetValue(null,true);
    }
    static void EliminationTalk(GameState p,uint npc,byte option) {
        var actor=Kernel.Maps[1011].Npcs[npc];p.BindWorldMap(1011);
        p.Entity.X=actor.X;p.Entity.Y=(ushort)(actor.Y+2);p.ActiveNpc=npc;
        Npcs.GetDialog(new NpcRequest(true){NpcID=npc,OptionID=option},p);
    }
    static string EliminationWireResult() {
        return (string)Sql(@"SELECT e.level::text||':'||e.experience::text||':'||e.studypoints::text||':'||c.rune_uid::text||':'||c.claimed_at::text||':'||i.id::text||':'||i.bound::text
FROM entities e JOIN revora_elimination_runs r ON r.uid=e.uid JOIN revora_elimination_claims c ON c.run=r.id JOIN items i ON i.uid=c.rune_uid AND i.entityid=e.uid WHERE e.uid=@uid","uid",(long)FirstUid);
    }
    static void EliminationWire(bool reload,bool claimed=false) {
        if(!reload){Player(Kernel.Maps[1002]);Sql("UPDATE entities SET reborn=1 WHERE uid=@uid","uid",(long)FirstUid);}
        string password=Guid.NewGuid().ToString("N");var account=new AccountTable("TeamProbe0");
        if(!account.exists){account.Password=password;account.State=AccountTable.AccountState.Player;account.EntityID=FirstUid;account.Insert();}account.Password=password;account.Save();Require(account.HasSrpVerifier && account.EntityID==FirstUid);
        if(!reload)Sql("UPDATE entities SET owner='TeamProbe0',mapid=1011,x=423,y=57 WHERE uid=@uid","uid",(long)FirstUid);
        Kernel.GamePool.Remove(FirstUid);
        var start=new System.Diagnostics.ProcessStartInfo(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"CandidateTeamWireProbe.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Environment.CurrentDirectory};
        start.EnvironmentVariables["REVORA_ELIMINATION_WIRE"]="1";start.EnvironmentVariables["REVORA_TEAM_WIRE_ROLE"]="0";start.EnvironmentVariables["REVORA_CANDIDATE_ACCOUNT_PASSWORD"]=password;start.EnvironmentVariables.Remove("REVORA_DATABASE_PASSWORD");
        using(var child=System.Diagnostics.Process.Start(start)) {
            start.EnvironmentVariables.Remove("REVORA_CANDIDATE_ACCOUNT_PASSWORD");password=null;var errors=child.StandardError.ReadToEndAsync();
            try {
                TeamWireRead(child,"[team-wire] ready");var p=Kernel.GamePool[FirstUid];Require(p.Account.State==AccountTable.AccountState.Player && !p.Fake && p.Entity.Reborn==1);
                Action<uint,byte> talk=(npc,option)=>{var actor=p.Map.Npcs[npc];p.Entity.X=actor.X;p.Entity.Y=(ushort)(actor.Y+2);TeamWireSend(child,"pirate-dialog:"+npc+":"+option);};
                if(!claimed) {
                TeamWireSend(child,"elimination-state:1011:0");
                if(!reload) {
                    talk(300020,0);talk(300020,1);
                    for(uint npc=300021;npc<=300032;npc++){talk(npc,0);talk(npc,1);talk(npc,1);}
                    Require(p.Inventory.Count==12 && p.Inventory.Objects.All(i=>i.Bound));TeamWireSend(child,"elimination-state:1011:12");
                    talk(300020,0);talk(300020,2);Require(p.Inventory.Count==1);var spice=p.Inventory.Objects.Single();
                    TeamWireSend(child,"pirate-check:"+p.Entity.ConquerPoints+":"+spice.UID+","+spice.ID+",1:");
                    Sql("CREATE TABLE revora_elimination_wire_probe(spice bigint,cp bigint,gold numeric); INSERT INTO revora_elimination_wire_probe VALUES(@spice,@cp,@gold)","spice",(long)spice.UID,"cp",(long)p.Entity.ConquerPoints,"gold",(decimal)p.Entity.Money);
                    p.Entity.X=311;p.Entity.Y=256;TeamWireSend(child,"pirate-use:"+spice.UID);TeamWireSend(child,"pirate-use:"+spice.UID);
                } else {talk(300020,0);talk(300020,9);}
                Require(p.Entity.MapID!=1011 && p.Inventory.Count==0);TeamWireSend(child,"elimination-state:"+p.Map.BaseID+":0");
                Require((long)Sql("SELECT count(*) FROM revora_elimination_runs WHERE uid=@uid","uid",(long)FirstUid)==1
                    && (long)Sql("SELECT count(*) FROM revora_elimination_rescues")==1
                    && (long)Sql("SELECT count(*) FROM revora_elimination_claims")==0
                    && (long)Sql("SELECT count(*) FROM items WHERE uid=(SELECT spice FROM revora_elimination_wire_probe)")==0);
                Require(p.Entity.ConquerPoints==Convert.ToUInt32(Sql("SELECT cp FROM revora_elimination_wire_probe")) && p.Entity.Money==Convert.ToUInt64(Sql("SELECT gold FROM revora_elimination_wire_probe")));
                if(reload) {
                    Action<Entity> fight=body=>{
                        for(int hit=0;hit<100 && !body.Dead && !p.Entity.Dead;hit++) {
                            p.Entity.X=body.X;p.Entity.Y=body.Y;p.Screen.Add(body);
                            TeamWireSend(child,"elimination-hit:"+body.UID);System.Threading.Thread.Sleep(550);
                        }
                        System.Console.WriteLine("[elimination-wire] fight="+body.Name+" remaining="+body.Hitpoints+" player="+p.Entity.Hitpoints);
                        Require(body.Dead && !p.Entity.Dead);
                    };
                    fight(EliminationBody(p));talk(300033,0);talk(300033,1);talk(300020,0);talk(300020,5);
                    var spirits=p.Map.Entities.Values.Where(e=>e.MonsterInfo!=null && e.MonsterInfo.ID==68 && !e.Dead).Take(10).ToArray();Require(spirits.Length==10);
                    foreach(var body in spirits)fight(body);
                    TeamWireSend(child,"elimination-state:1011:10");talk(300020,0);talk(300020,6);talk(300020,0);talk(300020,7);
                    for(int slot=0;slot<7;slot++)fight(EliminationBody(p));
                    talk(300020,0);talk(300020,8);talk(300020,8);
                    Require(p.Inventory.Count==1 && p.Inventory.Objects.Single().ID==4030101 && p.Inventory.Objects.Single().Bound && p.Entity.SubClasses.StudyPoints==50);
                    TeamWireSend(child,"elimination-state:1011:1");
                    Require((long)Sql("SELECT count(*) FROM revora_elimination_claims")==1);
                    Require((long)Sql("SELECT count(*) FROM revora_elimination_den_kills")==7 && (long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11010","uid",(long)FirstUid)==1);
                    Sql("ALTER TABLE revora_elimination_wire_probe ADD COLUMN result text; UPDATE revora_elimination_wire_probe SET result=@result","result",EliminationWireResult());
                }
                } else {
                    Require(EliminationWireResult()==(string)Sql("SELECT result FROM revora_elimination_wire_probe"));
                    Require(p.Entity.SubClasses.StudyPoints==50 && p.Inventory.Count==1 && p.Inventory.Objects.Single().ID==4030101);
                    talk(300020,0);talk(300020,8);talk(300020,8);
                    Require(EliminationWireResult()==(string)Sql("SELECT result FROM revora_elimination_wire_probe"));
                }
                if(reload) {var rune=p.Inventory.Objects.Single();TeamWireSend(child,"pirate-check:"+p.Entity.ConquerPoints+":"+rune.UID+","+rune.ID+",1:");}
                Require(EntityTable.SaveEntity(p));child.StandardInput.WriteLine("stop");child.StandardInput.Flush();TeamWireRead(child,"[team-wire] stopped");Require(child.WaitForExit(5000) && child.ExitCode==0);
            }finally{if(!child.HasExited){child.Kill();child.WaitForExit(5000);}if(errors.Wait(1000) && !string.IsNullOrEmpty(errors.Result))System.Console.Error.WriteLine(errors.Result);}
        }
        System.Console.WriteLine("[elimination-process] elimination-wire"+(claimed?"-claimed":reload?"-reload":"")+"-passed");Server.CommandsAI("@close");throw new Exception("Elimination wire did not close.");
    }
    static void EliminationCapture() {
        Require((bool)Feature("EliminationLifecycle").GetField("ReleaseReady",Hidden).GetValue(null));
        Require((long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)FirstUid)==0);
        Player(Kernel.Maps[1002]);
        // Supply eligible starting rebirth, then use the normal profile loader.
        Sql("UPDATE entities SET reborn=1 WHERE uid=@uid","uid",(long)FirstUid);
        nextUid=FirstUid;var p=Player(Kernel.Maps[1002],true);p.CalculateStatBonus();p.CalculateHPBonus();
        Require(EntityTable.SaveEntity(p));
        EliminationTalk(p,300020,0);EliminationTalk(p,300020,1);
        Require((long)Sql("SELECT count(*) FROM revora_elimination_runs WHERE uid=@uid AND stage=0","uid",(long)FirstUid)==1);
        for(uint npc=300021;npc<=300032;npc++){EliminationTalk(p,npc,0);EliminationTalk(p,npc,1);}
        Require(p.Inventory.Count==12);
        EliminationTalk(p,300020,0);EliminationTalk(p,300020,2);
        Require(p.Inventory.Count==1);var spice=p.Inventory.Objects.Single();
        Require(spice.Bound);
        p.Entity.X=311;p.Entity.Y=256;Require(EntityTable.SaveEntity(p));
        ItemHandler.UseItem(spice,p);
        Require(p.Entity.MapID!=1011 && p.Inventory.Count==0);
        Require((bool)Sql(@"SELECT EXISTS(SELECT 1 FROM revora_elimination_runs r
JOIN revora_elimination_rescue_bindings b ON b.run=r.id
JOIN entities e ON e.uid=r.uid WHERE r.uid=@uid AND b.active AND b.map=e.mapid)","uid",(long)FirstUid));
        Sql("CREATE TABLE revora_elimination_process_probe(uid bigint PRIMARY KEY,spice bigint NOT NULL); INSERT INTO revora_elimination_process_probe VALUES(@uid,@spice)","uid",(long)FirstUid,"spice",(long)spice.UID);
        System.Console.WriteLine("[elimination-process] elimination-capture-passed");
        Environment.Exit(0);
    }
    static void EliminationVerify() {
        Require((bool)Sql(@"SELECT EXISTS(SELECT 1 FROM revora_elimination_process_probe p
JOIN entities e ON e.uid=p.uid WHERE e.mapid=1011 AND e.x=309 AND e.y=256)
AND NOT EXISTS(SELECT 1 FROM revora_elimination_rescue_bindings WHERE active)
AND NOT EXISTS(SELECT 1 FROM revora_elimination_rescue_kills)
AND NOT EXISTS(SELECT 1 FROM items i JOIN revora_elimination_process_probe p ON p.spice=i.uid)"));
        var p=Player(Kernel.Maps[1011],true);p.CalculateStatBonus();p.CalculateHPBonus();
        Require(p.Entity.MapID==1011 && p.Entity.Reborn==1 && p.Inventory.Count==0);
        EliminationTalk(p,300020,0);EliminationTalk(p,300020,9);
        Require(p.Entity.MapID!=1011 && p.Inventory.Count==0);
        Require((bool)Sql(@"SELECT (SELECT count(*) FROM revora_elimination_runs WHERE uid=@uid)=1
AND (SELECT count(*) FROM revora_elimination_rescues r JOIN revora_elimination_runs q ON q.id=r.run WHERE q.uid=@uid)=1
AND EXISTS(SELECT 1 FROM revora_elimination_runs r JOIN revora_elimination_rescue_bindings b ON b.run=r.id
JOIN entities e ON e.uid=r.uid WHERE r.uid=@uid AND b.active AND b.map=e.mapid)
AND NOT EXISTS(SELECT 1 FROM revora_elimination_rescue_kills)
AND NOT EXISTS(SELECT 1 FROM items WHERE entityid=@uid)","uid",(long)FirstUid));
        System.Console.WriteLine("[elimination-process] elimination-verify-passed");
        Server.CommandsAI("@close");throw new InvalidOperationException("Elimination process did not close normally.");
    }
    static GameState EliminationPlayer() {
        var p=Player(Kernel.Maps[1011],true);p.CalculateStatBonus();p.CalculateHPBonus();
        Require(p.Entity.MapID==1011 && p.Entity.MaxHitpoints>0);return p;
    }
    static void EliminationHit(GameState p,Entity target) {
        p.Entity.X=target.X;p.Entity.Y=(ushort)(target.Y+2);
        // Actual world ticks may briefly own the encounter's nonblocking damage
        // lease. Retry ordinary attacks, never force a death or grant progress.
        var until=DateTime.UtcNow.AddSeconds(2);
        do {
            uint damage=target.Hitpoints;
            Handle.ReceiveAttack(p.Entity,target,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=target.UID},ref damage,null);
            if(target.Dead)break;
            System.Threading.Thread.Sleep(10);
        } while(DateTime.UtcNow<until);
        if(!target.Dead){
            System.Console.Error.WriteLine("[process-combat] unresolved family="+(target.MonsterInfo==null?0:target.MonsterInfo.ID)+" hp="+target.Hitpoints+" player-hp="+p.Entity.Hitpoints+" player-map="+p.Entity.MapID+" body-map="+target.MapID+" registered="+p.Map.Entities.ContainsKey(target.UID));
            foreach(string name in new[]{"DefenseTalismanEncounters","PowerBookCombat","DesertExpeditionEncounters","FamilyFeudEncounters","SoulGeneralPhantom"})
                System.Console.Error.WriteLine("[process-combat] "+name+" blocked="+Call(Feature(name),"BlockAttack",p.Entity,target));
            if((bool)Call(Feature("DefenseTalismanEncounters"),"IsEncounter",target)) {
                var type=Feature("DefenseTalismanEncounters");var registry=type.GetField("bodies",Hidden).GetValue(null);object[] args={target,null};
                bool tracked=(bool)registry.GetType().GetMethod("TryGetValue").Invoke(registry,args);
                System.Console.Error.WriteLine("[process-combat] tower tracked="+tracked+" eligible="+Call(Feature("DefenseTalismanStore"),"Eligible",p)+" world="+Call(type,"World",p.Map)+" same-body="+ReferenceEquals(p.Map.Entities[target.UID],target));
                if(tracked) {
                    var f=args[1];var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                    foreach(string field in new[]{"Kind","Life","Until","Recorded"})System.Console.Error.WriteLine("[process-combat] tower "+field+"="+f.GetType().GetField(field,flags).GetValue(f));
                    System.Console.Error.WriteLine("[process-combat] tower current="+Call(type,"Current",f)+" fighter="+Call(type,"Fighter",f,p.Entity)+" now="+towerNow.ToString("O"));
                }
            }
        }
        Require(target.Dead);
    }
    static Entity EliminationBody(GameState p) {return p.Map.Entities.Values.Single(e=>e.MonsterInfo!=null && !e.Dead);}
    static void EliminationCombat() {
        var p=EliminationPlayer();EliminationTalk(p,300020,0);EliminationTalk(p,300020,9);
        Require(p.Entity.MapID!=1011);EliminationHit(p,EliminationBody(p));
        var landy=p.Map.Npcs[300033];p.Entity.X=landy.X;p.Entity.Y=(ushort)(landy.Y+2);p.ActiveNpc=300033;
        Npcs.GetDialog(new NpcRequest(true){NpcID=300033,OptionID=0},p);
        Npcs.GetDialog(new NpcRequest(true){NpcID=300033,OptionID=1},p);
        Require(p.Entity.MapID==1011);EliminationTalk(p,300020,0);EliminationTalk(p,300020,5);
        var spirits=p.Map.Entities.Values.Where(e=>e.MonsterInfo!=null && e.MonsterInfo.ID==68 && !e.Dead).Take(10).ToArray();
        Require(spirits.Length==10);
        foreach(var spirit in spirits)EliminationHit(p,spirit);
        Require(p.Inventory.Objects.Count(i=>i.ID==711041 && i.Bound)==10);
        EliminationTalk(p,300020,0);EliminationTalk(p,300020,6);
        Require(p.Inventory.Count==0);EliminationTalk(p,300020,0);EliminationTalk(p,300020,7);
        Require(p.Map.BaseID==1021);
        for(int n=0;n<3;n++){var body=EliminationBody(p);Require(body.Name=="RockLackey");EliminationHit(p,body);}
        Require((long)Sql("SELECT count(*) FROM revora_elimination_den_kills")==3);
        Require((long)Sql("SELECT count(*) FROM revora_elimination_claims")==0);
        Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11010","uid",(long)FirstUid)==0);
        System.Console.WriteLine("[elimination-process] elimination-combat-passed");Environment.Exit(0);
    }
    static void EliminationFinish() {
        Require((long)Sql("SELECT count(*) FROM revora_elimination_den_bindings WHERE active")==0);
        var p=EliminationPlayer();bool paid=(long)Sql("SELECT count(*) FROM revora_elimination_claims")==1;
        if(!paid) {
            Require((long)Sql("SELECT count(*) FROM revora_elimination_den_kills")==3);
            EliminationTalk(p,300020,0);EliminationTalk(p,300020,7);Require(p.Map.BaseID==1021);
            for(int slot=3;slot<=6;slot++) {
                var body=EliminationBody(p);Require(body.Name==(slot==6?"RockLeader":"RockLackey"));EliminationHit(p,body);
            }
            Require(p.Entity.MapID==1011);
            ulong experience=(ulong)Call(Feature("ExperienceMath"),"Ball",p.Entity.Level);
            Sql("ALTER TABLE revora_elimination_process_probe ADD COLUMN expected_exp bigint; ALTER TABLE revora_elimination_process_probe ADD COLUMN final_fingerprint text; UPDATE revora_elimination_process_probe SET expected_exp=@exp","exp",(long)experience);
        }
        if(!paid) {
            EliminationTalk(p,300020,0);
            byte oldLevel=p.Entity.Level;ulong oldExp=p.Entity.Experience;ushort oldStudy=p.Entity.SubClasses.StudyPoints;
            PirateLoseCommit(()=>Npcs.GetDialog(new NpcRequest(true){NpcID=300020,OptionID=8},p),"revora_elimination_claims");
            Require(PirateFenced(p) && p.Inventory.Count==0 && p.Entity.Level==oldLevel
                && p.Entity.Experience==oldExp && p.Entity.SubClasses.StudyPoints==oldStudy);
            Require((long)Sql("SELECT count(*) FROM revora_elimination_claims")==1);
            string committed=TaoistFingerprint(p);
            System.Threading.Tasks.Parallel.For(0,8,_=>Npcs.GetDialog(new NpcRequest(true){NpcID=300020,OptionID=8},p));
            Require(!EntityTable.SaveEntity(p) && TaoistFingerprint(p)==committed);
            nextUid=FirstUid;p=EliminationPlayer();
            Require(!PirateFenced(p) && TaoistFingerprint(p)==committed);
            System.Console.WriteLine("[elimination-process] claim-commit-reply-loss-recovered");
        }
        EliminationTalk(p,300020,0);EliminationTalk(p,300020,8);
        byte level=p.Entity.Level;ulong experienceAfter=p.Entity.Experience;ushort study=p.Entity.SubClasses.StudyPoints;
        EliminationTalk(p,300020,0);EliminationTalk(p,300020,8);
        Require(p.Entity.Level==level && p.Entity.Experience==experienceAfter && p.Entity.SubClasses.StudyPoints==study);
        Require(p.Inventory.Count==1 && p.Inventory.Objects.Single().ID==4030101 && p.Inventory.Objects.Single().Bound);
        Require(p.Entity.SubClasses.StudyPoints==50);
        Require((bool)Sql(@"SELECT (SELECT count(*) FROM revora_elimination_den_kills)=7
AND (SELECT count(*) FROM revora_elimination_claims)=1
AND EXISTS(SELECT 1 FROM revora_elimination_claims c JOIN revora_elimination_runs r ON r.id=c.run
JOIN revora_elimination_process_probe p ON p.uid=r.uid
JOIN items i ON i.uid=c.rune_uid AND i.entityid=r.uid AND i.id=4030101 AND i.bound=1
WHERE c.study=50 AND c.experience=p.expected_exp AND r.stage=6)
AND (SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11010)=1","uid",(long)FirstUid));
        string fingerprint=(string)Sql(@"SELECT md5(e.level::text||':'||e.experience::text||':'||e.studypoints::text||':'||c.rune_uid::text||':'||c.claimed_at::text)
FROM entities e JOIN revora_elimination_runs r ON r.uid=e.uid JOIN revora_elimination_claims c ON c.run=r.id WHERE e.uid=@uid","uid",(long)FirstUid);
        object previous=Sql("SELECT final_fingerprint FROM revora_elimination_process_probe");
        if(previous==DBNull.Value)Sql("UPDATE revora_elimination_process_probe SET final_fingerprint=@value","value",fingerprint);
        else Require((string)previous==fingerprint);
        System.Console.WriteLine("[elimination-process] elimination-finish-passed");
        Server.CommandsAI("@close");throw new InvalidOperationException("Elimination final process did not close.");
    }
}
