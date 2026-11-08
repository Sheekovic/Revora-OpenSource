using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;

internal static partial class CheckCandidateArenaLifecycle
{
    static bool teamWireFixture;
    static void TeamWardrobeVerify(GameState p,bool restoring) {
        if(restoring)new Revora.Network.GamePackets.TitleStorage().Login(p);
        int key=Convert.ToInt32(Sql(@"SELECT a.title_key FROM revora_event_awards a
JOIN revora_local_team_pk_roster m ON m.uid=a.uid
JOIN revora_local_team_pk_runs r ON r.run=m.run
WHERE a.uid=@uid AND a.source='team-pk-local-wardrobe-v1' AND r.state='complete' AND r.kind=0 AND r.bracket=3 AND m.contested
AND a.title_key=CASE array_position(r.leaders,m.leader) WHEN 1 THEN 20592059 WHEN 2 THEN 20602060 WHEN 3 THEN 20612061 ELSE 20622062 END
AND a.earned_at=r.ended_at AND a.expires_at=r.ended_at+interval '14 days'","uid",(long)p.Entity.UID));
        Require(key>=20592059 && key<=20622062);
        var owned=(System.Collections.IDictionary)p.Entity.WTitles.GetType().GetMethod("Records",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(p.Entity.WTitles,null);
        Require(owned.Contains(key));
        if(restoring)Require(p.Entity.EquippedTitle==key);
        new Revora.Network.GamePackets.TitleStorage{Info=new Revora.Network.GamePackets.TitleStorageProto{Type=key/10000,Id=key%10000,ActionId=Revora.Network.GamePackets.TitleStorage.Action.Equip,Value=new Revora.Network.GamePackets.TitleValue{Type=key/10000,Id=key%10000}}}.Handle(p);
        Require(p.Entity.EquippedTitle==key);
        if(!restoring)Require(EntityTable.SaveEntity(p));
        if(restoring)Require((bool)Sql("SELECT fingerprint=(SELECT md5(string_agg(uid::text||':'||title_key::text||':'||earned_at::text||':'||expires_at::text,',' ORDER BY uid)) FROM revora_event_awards WHERE source='team-pk-local-wardrobe-v1') FROM revora_team_wardrobe_probe"));
    }
    static void TeamWireSnapshot() {
        Sql("CREATE TABLE revora_team_wire_probe(uid bigint PRIMARY KEY,items text NOT NULL,receipts text NOT NULL,cp bigint NOT NULL,rvp bigint NOT NULL,gold bigint NOT NULL)");
        foreach(var p in Enumerable.Range(0,16).Select(i=>Kernel.GamePool[FirstUid+(uint)i]))
            Sql("INSERT INTO revora_team_wire_probe VALUES(@uid,@items,@receipts,@cp,@rvp,@gold)","uid",(long)p.Entity.UID,"items",CtfItems(p),"receipts",TeamWireReceipts(p.Entity.UID),"cp",(long)p.Entity.ConquerPoints,"rvp",(long)p.Entity.RevoraPoint,"gold",(long)p.Entity.Money);
    }
    static string RegularTeamEntrySnapshot() {
        return (string)Sql("SELECT COALESCE(string_agg(run::text||':'||uid::text||':'||pkmode::text||':'||entry_earned::text||':'||COALESCE(entry_item::text,'pending'),';' ORDER BY uid),'empty') FROM revora_local_team_pk_admissions");
    }
    static string TeamWireRounds() {
        return (string)Sql("SELECT COALESCE(string_agg(row_to_json(r)::text,';' ORDER BY row_to_json(r)::text),'empty') FROM revora_skill_pk_round_rewards r");
    }
    static string TeamWireReceipts(uint uid) {
        return (string)Sql("SELECT COALESCE(string_agg(row_to_json(r)::text,';' ORDER BY row_to_json(r)::text),'empty') FROM revora_local_team_pk_claims r WHERE uid=@uid","uid",(long)uid);
    }
    static void TeamWireVerifyReward(GameState p) {
        Require(CtfItems(p)==(string)Sql("SELECT items FROM revora_team_wire_probe WHERE uid=@uid","uid",(long)p.Entity.UID));
        Require(TeamWireReceipts(p.Entity.UID)==(string)Sql("SELECT receipts FROM revora_team_wire_probe WHERE uid=@uid","uid",(long)p.Entity.UID));
        Require((bool)Sql("SELECT cp=@cp AND rvp=@rvp AND gold=@gold FROM revora_team_wire_probe WHERE uid=@uid","uid",(long)p.Entity.UID,"cp",(long)p.Entity.ConquerPoints,"rvp",(long)p.Entity.RevoraPoint,"gold",(long)p.Entity.Money));
        Require(p.Entity.MapID==1002 && !p.Entity.InSkillPk);
    }
    static void TeamWireReload(bool interrupted=false,bool regular=false,bool wardrobe=false) {
        teamWireFixture=true;
        for(int i=0;i<16;i++) {
            var p=Player(Kernel.Maps[1002],true);TeamWireVerifyReward(p);if(wardrobe)TeamWardrobeVerify(p,true);Kernel.GamePool.Remove(p.Entity.UID);
        }
        var pool=new Npgsql.NpgsqlConnectionStringBuilder(DataHolder.ConnectionString){Pooling=true,MaxPoolSize=12,MinPoolSize=0};
        DataHolder.ConnectionString=pool.ConnectionString;Npgsql.NpgsqlConnection.ClearAllPools();
        for(int role=0;role<(interrupted?16:1);role++) {
        uint returning=FirstUid+(uint)role;
        // Rotate only the disposable account's credential; character/reward state
        // comes entirely from the prior earned run and the restarted database.
        string password=Guid.NewGuid().ToString("N");var account=new AccountTable("TeamProbe"+role);
        Require(account.exists && account.EntityID==returning && account.State==AccountTable.AccountState.Player);
        account.Password=password;account.Save();
        var start=new ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"CandidateTeamWireProbe.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Environment.CurrentDirectory};
        start.EnvironmentVariables["REVORA_REGULAR_TEAM_WIRE"]=regular?"1":"0";start.EnvironmentVariables["REVORA_TEAM_WIRE_ROLE"]=role.ToString();start.EnvironmentVariables["REVORA_CANDIDATE_ACCOUNT_PASSWORD"]=password;start.EnvironmentVariables.Remove("REVORA_DATABASE_PASSWORD");
        using(var child=Process.Start(start)) {
            start.EnvironmentVariables.Remove("REVORA_CANDIDATE_ACCOUNT_PASSWORD");password=null;
            var errors=child.StandardError.ReadToEndAsync();
            try {
                TeamWireRead(child,"[team-wire] ready");var winner=Kernel.GamePool[returning];TeamWireVerifyReward(winner);
                winner.Entity.X=(ushort)(regular?291:302);winner.Entity.Y=146;
                TeamWireSend(child,"claim");TeamWireSend(child,"claim");TeamWireVerifyReward(winner);
                child.StandardInput.WriteLine("stop");child.StandardInput.Flush();TeamWireRead(child,"[team-wire] stopped");Require(child.WaitForExit(5000) && child.ExitCode==0);
            } finally {
                if(!child.HasExited){child.Kill();child.WaitForExit(5000);}
                if(errors.Wait(1000) && !string.IsNullOrEmpty(errors.Result))System.Console.Error.WriteLine(errors.Result);
            }
        }
        }
        if(interrupted) {
            Require((regular?RegularTeamEntrySnapshot():TeamWireRounds())==(string)Sql("SELECT rounds FROM revora_team_wire_crash_probe"));
            if(regular)Require((long)Sql("SELECT count(*) FROM revora_local_team_pk_runs WHERE kind=0 AND state='complete'")==0);
            Require((long)Sql("SELECT count(*) FROM revora_local_team_pk_claims")==0);
            Require((long)Sql("SELECT count(*) FROM revora_local_team_pk_runs WHERE state='active'")==0);
            Require((long)Sql("SELECT count(*) FROM revora_local_team_pk_admissions WHERE NOT returned")==0);
            System.Console.WriteLine("[team-wire-process] crash-recovery-passed");
        }
        System.Console.WriteLine("[team-wire-process] reload-passed");
        Server.CommandsAI("@close");throw new InvalidOperationException("Private team host did not stop.");
    }
    static void TeamWireRead(Process child,string marker) {
        for(int n=0;n<16;n++) {
            var line=child.StandardOutput.ReadLineAsync();Require(line.Wait(8000));
            if(line.Result==marker)return;
            if(line.Result!=null && line.Result.StartsWith("[wire-timing]",StringComparison.Ordinal))System.Console.WriteLine(line.Result);
            if(line.Result==null)throw new InvalidOperationException("Team wire child ended before "+marker);
        }
        throw new InvalidOperationException("Team wire marker missing.");
    }
    static void TeamWireSend(Process child,string command) {System.Console.WriteLine("[team-wire-process] sending "+command);child.StandardInput.WriteLine(command);child.StandardInput.Flush();TeamWireRead(child,"[team-wire] done "+command);}
    static void TeamWireUnrelatedReturn(GameState p) {
        Require((long)Sql("SELECT count(*) FROM revora_lms_admissions WHERE uid=@uid","uid",(long)p.Entity.UID)==0);
        for(int pending=0;pending<2;pending++) {
            if(pending==1)Sql("INSERT INTO revora_lms_admissions(cycle,uid,arena,pkmode,ended) VALUES('wire-lock-fixture',@uid,700,0,true)","uid",(long)p.Entity.UID);
        foreach(object gate in new[]{p.ItemSyncRoot,typeof(GameState).GetProperty("VirtueSyncRoot",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(p,null),typeof(GameState).GetProperty("PkSyncRoot",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(p,null)}) {
            using(var entered=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
                var holder=System.Threading.Tasks.Task.Run(()=>{lock(gate){entered.Set();release.Wait();}});
                try {Require(entered.Wait(3000));Require((bool)Call(Feature("LmsAdmissions"),"Return",p)==(pending==0));}
                finally{release.Set();Require(holder.Wait(3000));}
            }
        }
        }
        Require((bool)Call(Feature("LmsAdmissions"),"Return",p));
        Require((bool)Sql("SELECT returned FROM revora_lms_admissions WHERE cycle='wire-lock-fixture' AND uid=@uid","uid",(long)p.Entity.UID));
        Require((bool)Call(Feature("LmsAdmissions"),"Return",p));
        foreach(string system in new[]{"LastManStandingSystem","RelicRushSystem","WaveDefenseSystem","BeastTrialSystem","DuelPitSystem","NobleWarSystem"}) {
            using(var entered=new System.Threading.ManualResetEventSlim())using(var release=new System.Threading.ManualResetEventSlim()) {
                var holder=System.Threading.Tasks.Task.Run(()=>{lock(p.ItemSyncRoot){entered.Set();release.Wait();}});
                try {Require(entered.Wait(3000));Feature(system).GetMethod("OnLogin",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public).Invoke(null,new object[]{p});Require(!(bool)p.Entity.GetType().GetField("PersistenceRequiresReload",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(p.Entity));}
                finally{release.Set();Require(holder.Wait(3000));}
            }
        }
        System.Console.WriteLine("[team-wire-process] unrelated-lms-locks-passed");
    }
    static void TeamWireProcess(bool interruptCombat=false,bool regular=false,bool wardrobe=false) {
        teamWireFixture=true;
        // The disposable PostgreSQL instance allows20 connections, including admin.
        var pool=new Npgsql.NpgsqlConnectionStringBuilder(DataHolder.ConnectionString){Pooling=true,MaxPoolSize=12,MinPoolSize=0};
        DataHolder.ConnectionString=pool.ConnectionString;Npgsql.NpgsqlConnection.ClearAllPools();
        AppDomain.CurrentDomain.FirstChanceException+=(sender,args)=>{
            var failure=args.Exception as Npgsql.PostgresException;
            if(failure!=null && failure.SqlState!="42P01")System.Console.Error.WriteLine("[team-wire-db] sqlstate="+failure.SqlState+" table="+failure.TableName+" constraint="+failure.ConstraintName);
        };
        var children=new Process[16];var errors=new System.Threading.Tasks.Task<string>[16];
        try {
            for(int i=0;i<16;i++) {
                Player(Kernel.Maps[1002]);nextUid--;var p=Player(Kernel.Maps[1002],true);if(i<2)TeamWireUnrelatedReturn(p);string username="TeamProbe"+i,password=Guid.NewGuid().ToString("N");
                Require((long)Sql("SELECT count(*) FROM accounts WHERE username=@user","user",username)==0);
                var account=new AccountTable(username){Password=password,State=AccountTable.AccountState.Player,EntityID=p.Entity.UID};account.Insert();account.Save();Require(account.HasSrpVerifier);
                Require((long)Sql("SELECT entityid FROM accounts WHERE username=@user","user",username)==p.Entity.UID);
                Sql("UPDATE entities SET owner=@user,x=@x,y=146 WHERE uid=@uid","user",username,"uid",(long)p.Entity.UID,"x",regular?291:302);
                if(wardrobe)Sql("UPDATE entities SET level=140 WHERE uid=@uid","uid",(long)p.Entity.UID);
                Kernel.GamePool.Remove(p.Entity.UID);
                var start=new ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"CandidateTeamWireProbe.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Environment.CurrentDirectory};
                start.EnvironmentVariables["REVORA_REGULAR_TEAM_WIRE"]=regular?"1":"0";start.EnvironmentVariables["REVORA_TEAM_WIRE_ROLE"]=i.ToString();start.EnvironmentVariables["REVORA_CANDIDATE_ACCOUNT_PASSWORD"]=password;
                start.EnvironmentVariables.Remove("REVORA_DATABASE_PASSWORD");
                children[i]=Process.Start(start);start.EnvironmentVariables.Remove("REVORA_CANDIDATE_ACCOUNT_PASSWORD");password=null;
                errors[i]=children[i].StandardError.ReadToEndAsync();TeamWireRead(children[i],"[team-wire] ready");
            }
            var leader=Kernel.GamePool[FirstUid];var member=Kernel.GamePool[FirstUid+1];
            Require(leader.Account.State==AccountTable.AccountState.Player && member.Account.State==AccountTable.AccountState.Player && leader.Team==null && member.Team==null);
            TeamWireSend(children[0],"create");Require(leader.Team!=null && leader.Team.Members.Count==1);
            TeamWireSend(children[1],"accept");Require(member.Team==null && leader.Team.Members.Count==1);
            TeamWireSend(children[0],"invite");TeamWireSend(children[1],"accept");
            Require(ReferenceEquals(leader.Team,member.Team) && leader.Team.Members.Count==2 && ReferenceEquals(leader.Team.Lider,leader));
            TeamWireSend(children[1],"accept");Require(leader.Team.Members.Count==2);
            TeamWireSend(children[1],"kick");Require(ReferenceEquals(leader.Team,member.Team) && leader.Team.Members.Count==2);
            TeamWireSend(children[0],"kick");Require(member.Team==null && leader.Team.Members.Count==1);
            // Re-form through real invitation traffic, then enter individually.
            TeamWireSend(children[0],"invite");TeamWireSend(children[1],"accept");
            Require(ReferenceEquals(leader.Team,member.Team) && leader.Team.Members.Count==2);
            TeamWireSend(children[2],"create");TeamWireSend(children[2],"invite");TeamWireSend(children[3],"accept");
            var opponent=Kernel.GamePool[FirstUid+2];var opponentMember=Kernel.GamePool[FirstUid+3];
            Require(ReferenceEquals(opponent.Team,opponentMember.Team) && opponent.Team.Members.Count==2);
            for(int i=4;i<16;i+=2){TeamWireSend(children[i],"create");TeamWireSend(children[i],"invite");TeamWireSend(children[i+1],"accept");}
            DateTime opening=DateTime.Today.AddDays(7).AddHours(19).AddMinutes(45);
            while(opening.DayOfWeek!=DayOfWeek.Wednesday)opening=opening.AddDays(1);
            Require(regular?Revora.Game.Features.Tournaments.TeamElitePk.TeamTournament.Open():(bool)Call(typeof(Revora.Game.Features.Tournaments.TeamElitePk.SkillTeamTournament),"OpenAt",opening));
            foreach(var child in children)TeamWireSend(child,"enter");
            var bracket=regular?Revora.Game.Features.Tournaments.TeamElitePk.TeamTournament.Tournaments[wardrobe?3:1]:Revora.Game.Features.Tournaments.TeamElitePk.SkillTeamTournament.Tournaments[1];
            Require(leader.Entity.MapID==bracket.WaitingArea.ID && member.Entity.MapID==bracket.WaitingArea.ID && bracket.Teams.Count==8);
            Require((long)Sql("SELECT count(*) FROM revora_local_team_pk_admissions a JOIN revora_local_team_pk_runs r USING(run) WHERE a.uid IN(@leader,@member) AND r.kind=@kind AND NOT a.returned","leader",(long)FirstUid,"member",(long)(FirstUid+1),"kind",regular?0:1)==2);
            Require((long)Sql("SELECT count(*) FROM revora_skill_pk_round_rewards WHERE uid IN(@leader,@member)","leader",(long)FirstUid,"member",(long)(FirstUid+1))==0);
            System.Console.WriteLine("[team-wire-process] ordinary-team-admitted");
            foreach(var p in new[]{leader,member})Require(p.Inventory.Count==0 && p.Entity.ConquerPoints==0 && p.Entity.RevoraPoint==0);
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var bracketType=bracket.GetType();int scored=0;
            bool wardrobeRollback=false;
            if(wardrobe)Sql(@"CREATE FUNCTION reject_team_wardrobe() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
IF NEW.source='team-pk-local-wardrobe-v1' THEN RETURN NULL; END IF; RETURN NEW; END $$;
CREATE TRIGGER reject_team_wardrobe BEFORE INSERT ON revora_event_awards FOR EACH ROW EXECUTE FUNCTION reject_team_wardrobe();");
            for(int pulse=0;pulse<45 && bracket.State!=Revora.Game.Features.Tournaments.ElitePkStatus.States.GUI_ReconstructTop;pulse++) {
                bracketType.GetField("StarTimer",flags).SetValue(bracket,DateTime.Now.AddMinutes(-2));
                bracketType.GetField("pStamp",flags).SetValue(bracket,Time32.Now.AddSeconds(-90));
                foreach(var match in bracket.Matches.Values.Where(m=>m.Inside && !m.Done)) {
                    Require(match.MatchStats.Count(f=>f.Fighting)==2);
                    var fighters=match.MatchStats.Where(f=>f.Fighting).OrderBy(f=>f.LeaderUID).ToArray();
                    var attacker=fighters[0].Team.Lider;var target=fighters[1].Team.Lider;
                    var map=match.Map;ushort x=0,y=0;
                    for(int cx=30;cx<100 && x==0;cx++)for(int cy=30;cy<100 && x==0;cy++)
                        if(map.Floor[cx,cy,Revora.Game.MapObjectType.Player] && map.Floor[cx+1,cy,Revora.Game.MapObjectType.Player]){x=(ushort)cx;y=(ushort)cy;}
                    Require(x!=0 && attacker.Entity.MapID==map.ID && target.Entity.MapID==map.ID);
                    // Proximity and preparation time are controlled, not damage or score.
                    attacker.Entity.X=x;attacker.Entity.Y=y;target.Entity.X=(ushort)(x+1);target.Entity.Y=y;
                    attacker.CantAttack=Time32.Now.AddSeconds(-1);
                    // Import revives participants; let the normal player timer expire
                    // its five-second protection instead of changing attack eligibility.
                    Require(System.Threading.SpinWait.SpinUntil(()=>target.Attackable,7000));
                    uint before=attacker.Team.EliteFighterStats.Points;
                    // Ordinary melee can miss; keep real hit rolls and cooldowns.
                    for(int attempt=0;attempt<12 && attacker.Team.EliteFighterStats.Points==before;attempt++) {
                        System.Threading.Thread.Sleep(550);
                        TeamWireSend(children[attacker.Entity.UID-FirstUid],"attack:"+target.Entity.UID);
                    }
                    Revora.Game.Entity visible;
                    System.Console.WriteLine("[team-wire-process] attack-check action="+attacker.Action+" range="+attacker.Entity.AttackRange+" visible="+attacker.Screen.TryGetValue(target.Entity.UID,out visible)+" allowed="+Revora.Game.Attacking.Handle.CanAttack(attacker.Entity,target.Entity,null,true)+" target-attackable="+target.Attackable+" target-hp="+target.Entity.Hitpoints+" points="+attacker.Team.EliteFighterStats.Points);
                    Require(attacker.Team.EliteFighterStats.Points>before);scored++;
                    if(interruptCombat && scored==5) {
                        if(regular)Require((long)Sql("SELECT count(*) FROM revora_local_team_pk_admissions WHERE entry_earned AND entry_item IS NOT NULL")==16);
                        else Require((long)Sql("SELECT count(*) FROM revora_skill_pk_round_rewards")>0);
                        Require((long)Sql("SELECT count(*) FROM revora_local_team_pk_claims")==0);
                        TeamWireSnapshot();
                        Sql("CREATE TABLE revora_team_wire_crash_probe(rounds text NOT NULL)");
                        Sql("INSERT INTO revora_team_wire_crash_probe VALUES(@rounds)","rounds",regular?RegularTeamEntrySnapshot():TeamWireRounds());
                        System.Console.WriteLine("[team-wire-process] interrupted-after-earned-round");System.Console.Out.Flush();
                        // Abrupt game-process exit: no tournament close or player logout.
                        // Child probes receive EOF when this process closes their pipes.
                        Environment.Exit(0);
                    }
                    match.ImportTime=Time32.Now.AddSeconds(-1000);
                }
                bracketType.GetMethod("timerCallback",flags).Invoke(bracket,new object[]{0});
                Require(bracket.Alive);
                if(wardrobe && !wardrobeRollback && bracket.Top8!=null && bracket.Top8.Length==8 && bracket.Top8[0]!=null && bracket.Top8[1]!=null) {
                    Require((long)Sql("SELECT count(*) FROM revora_local_team_pk_runs WHERE kind=0 AND bracket=3 AND state='complete'")==0);
                    Require((long)Sql("SELECT count(*) FROM revora_event_awards WHERE source='team-pk-local-wardrobe-v1'")==0);
                    Require((long)Sql("SELECT count(*) FROM revora_local_team_pk_battles")>0);
                    foreach(var p in Enumerable.Range(0,16).Select(i=>Kernel.GamePool[FirstUid+(uint)i])) {
                        Call(GameAssembly.GetType("Revora.TitleRequirements",true),"Reconcile",p,true);
                        p.Entity.MyAchievement.GetType().GetField("nextRead",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(p.Entity.MyAchievement,DateTime.UtcNow.AddMinutes(5));
                    }
                    Sql("DROP TRIGGER reject_team_wardrobe ON revora_event_awards;DROP FUNCTION reject_team_wardrobe();");wardrobeRollback=true;
                }
            }
            if(wardrobe)Require(wardrobeRollback);
            Require(scored>0 && bracket.State==Revora.Game.Features.Tournaments.ElitePkStatus.States.GUI_ReconstructTop && bracket.Top8[0].LeaderUID==FirstUid);
            bracket.ConstructTop8=DateTime.Now.AddMinutes(-3);bracketType.GetMethod("timerCallback",flags).Invoke(bracket,new object[]{0});Require(!bracket.Alive);
            foreach(var p in Enumerable.Range(0,16).Select(i=>Kernel.GamePool[FirstUid+(uint)i]))Require(p.Entity.MapID==1002 && !p.Entity.InSkillPk);
            leader.Entity.X=(ushort)(regular?291:302);leader.Entity.Y=146;
            ulong beforePurse=leader.Entity.Money;
            TeamWireSend(children[0],"claim");TeamWireSend(children[0],"claim");
            Require((regular?leader.Entity.ConquerPoints==0 && leader.Entity.Money==beforePurse+1000000000:leader.Entity.ConquerPoints==10000) && leader.Inventory.Objects.Count(i=>i.Bound && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)==1);
            Require((long)Sql("SELECT count(*) FROM revora_local_team_pk_claims WHERE uid=@uid","uid",(long)FirstUid)==1);
            System.Console.WriteLine("[team-wire-process] combat-final-passed scored="+scored);
            if(wardrobe) {
                Require((long)Sql("SELECT count(*) FROM revora_local_team_pk_roster WHERE contested")==16);
                Require((long)Sql("SELECT count(*) FROM revora_event_awards WHERE source='team-pk-local-wardrobe-v1'")==16);
                Require((long)Sql("SELECT count(DISTINCT title_key) FROM revora_event_awards WHERE source='team-pk-local-wardrobe-v1'")==4);
                foreach(var p in Enumerable.Range(0,16).Select(i=>Kernel.GamePool[FirstUid+(uint)i]))TeamWardrobeVerify(p,false);
                Sql("CREATE TABLE revora_team_wardrobe_probe(fingerprint text)");
                Sql("INSERT INTO revora_team_wardrobe_probe SELECT md5(string_agg(uid::text||':'||title_key::text||':'||earned_at::text||':'||expires_at::text,',' ORDER BY uid)) FROM revora_event_awards WHERE source='team-pk-local-wardrobe-v1'");
            }
            TeamWireSnapshot();
            for(int i=0;i<16;i++){children[i].StandardInput.WriteLine("stop");children[i].StandardInput.Flush();TeamWireRead(children[i],"[team-wire] stopped");Require(children[i].WaitForExit(5000) && children[i].ExitCode==0);}
            System.Console.WriteLine("[team-wire-process] invitation-passed");
        } finally {
            for(int i=0;i<16;i++)if(children[i]!=null) {
                if(!children[i].HasExited){children[i].Kill();children[i].WaitForExit(5000);}
                if(errors[i]!=null && errors[i].Wait(1000) && !string.IsNullOrEmpty(errors[i].Result))System.Console.Error.WriteLine(errors[i].Result);
                children[i].Dispose();
            }
        }
        Server.CommandsAI("@close");throw new InvalidOperationException("Private team host did not stop.");
    }
}
