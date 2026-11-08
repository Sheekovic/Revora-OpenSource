using System;
using System.Diagnostics;
using System.Reflection;
using Npgsql;
using Revora;
using Revora.Database;

internal static class AccountLookupTests
{
    static int checks;
    static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        checks++;
        System.Console.WriteLine("PASS " + label);
        System.Console.Out.Flush();
    }

    static void Execute(string sql, string name = null, string password = null)
    {
        using (var connection = DataHolder.MySqlConnection)
        using (var command = new NpgsqlCommand(sql, connection))
        {
            if (name != null) command.Parameters.AddWithValue("name", name);
            if (password != null) command.Parameters.AddWithValue("password", password);
            connection.Open();
            command.ExecuteNonQuery();
        }
    }

    static object Scalar(string sql, string name)
    {
        using (var c = DataHolder.MySqlConnection)
        using (var command = new NpgsqlCommand(sql, c))
        { c.Open(); command.Parameters.AddWithValue("name", name); return command.ExecuteScalar(); }
    }
    static bool Authenticate(AccountTable account, string password)
    {
        var exchange = account.BeginSrp(account.Username);
        byte[] a;
        byte[] proof = SrpVerifierTests.Proof(exchange.CreateChallenge(), account.Username, password, out a);
        return exchange.TryVerify(a, proof);
    }
    static void Activity(AccountTable account)
    {
        var type=typeof(AccountTable).Assembly.GetType("Revora.Database.AccountActivity");
        var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;
        type.GetMethod("Initialize",flags).Invoke(null,null);
        var observe=type.GetMethod("Authenticated",flags);
        Check(Convert.ToInt64(Scalar("SELECT count(*) FROM revora_account_activity WHERE account_name=lower(@name)",account.Username))==0,
            "unknown account activity has no inferred historical record");
        observe.Invoke(null,new object[]{account});
        Check((bool)Scalar("SELECT auth_count=1 AND last_auth>=first_auth FROM revora_account_activity WHERE account_name=lower(@name)",account.Username),
            "successful authentication creates a durable UTC observation");
        System.Threading.Tasks.Parallel.Invoke(()=>observe.Invoke(null,new object[]{account}),
            ()=>observe.Invoke(null,new object[]{new AccountTable(account.Username.ToUpperInvariant())}));
        Check(Convert.ToInt64(Scalar("SELECT auth_count FROM revora_account_activity WHERE account_name=lower(@name)",account.Username))==3,
            "concurrent case variants share one account observation without lost increments");
        Execute("UPDATE revora_account_activity SET last_auth='2099-01-01T00:00:00Z' WHERE account_name=lower(@name)",account.Username);
        observe.Invoke(null,new object[]{account});
        Check((bool)Scalar("SELECT last_auth='2099-01-01T00:00:00Z'::timestamptz FROM revora_account_activity WHERE account_name=lower(@name)",account.Username),
            "backward database clock cannot reduce observed account activity");
        Execute("CREATE FUNCTION reject_activity() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER reject_activity BEFORE UPDATE ON revora_account_activity FOR EACH ROW EXECUTE FUNCTION reject_activity()");
        bool failed=false;
        try{observe.Invoke(null,new object[]{account});}catch(System.Reflection.TargetInvocationException e){failed=e.InnerException is InvalidOperationException;}
        finally{Execute("DROP TRIGGER reject_activity ON revora_account_activity; DROP FUNCTION reject_activity()");}
        Check(failed && Convert.ToInt64(Scalar("SELECT auth_count FROM revora_account_activity WHERE account_name=lower(@name)",account.Username))==4,
            "suppressed account observation fails rather than claiming saved activity");
        uint uid=account.EntityID;account.EntityID=uid+1;failed=false;
        try{observe.Invoke(null,new object[]{account});}catch(System.Reflection.TargetInvocationException e){failed=e.InnerException is InvalidOperationException;}
        finally{account.EntityID=uid;}
        Check(failed,"stale account-to-character identity cannot create trusted activity");
        var open=type.GetMethod("SessionOpened",flags);
        var close=type.GetMethod("SessionClosed",flags);
        Guid oldSession=Guid.NewGuid(),newSession=Guid.NewGuid();
        open.Invoke(null,new object[]{account,oldSession});
        open.Invoke(null,new object[]{account,newSession});
        Check(Convert.ToInt64(Scalar("SELECT count(*) FROM revora_account_sessions WHERE account_name=lower(@name) AND closed_at IS NULL",account.Username))==2,
            "overlapping logins have independent durable presence");
        close.Invoke(null,new object[]{oldSession});
        Check(Convert.ToInt64(Scalar("SELECT count(*) FROM revora_account_sessions WHERE account_name=lower(@name) AND closed_at IS NULL",account.Username))==1,
            "old disconnect does not close newer session");
        var ended=Scalar("SELECT closed_at FROM revora_account_sessions WHERE session_id=CAST(@name AS uuid)",oldSession.ToString());
        close.Invoke(null,new object[]{oldSession});
        Check(object.Equals(ended,Scalar("SELECT closed_at FROM revora_account_sessions WHERE session_id=CAST(@name AS uuid)",oldSession.ToString())),
            "repeated disconnect preserves original closure");
        type.GetMethod("Initialize",flags).Invoke(null,null);
        Check(Convert.ToInt64(Scalar("SELECT count(*) FROM revora_account_sessions WHERE account_name=lower(@name) AND closed_at IS NULL",account.Username))==1,
            "same-process initialization keeps this process's open session");
        Guid orphan=Guid.NewGuid();
        open.Invoke(null,new object[]{account,orphan});
        Execute("UPDATE revora_account_sessions SET owner='11111111-1111-1111-1111-111111111111' WHERE session_id=CAST(@name AS uuid)",orphan.ToString());
        type.GetMethod("Initialize",flags).Invoke(null,null);
        Check(Convert.ToInt64(Scalar("SELECT count(*) FROM revora_account_sessions WHERE session_id=CAST(@name AS uuid) AND closed_at IS NOT NULL AND closed_at>=opened_at",orphan.ToString()))==1,
            "previous-process open session is closed on recovery");
        Check(Convert.ToInt64(Scalar("SELECT count(*) FROM revora_account_sessions WHERE account_name=lower(@name) AND closed_at IS NULL",account.Username))==1,
            "recovery preserves this process's live session");
        Guid unknown=Guid.NewGuid();
        open.Invoke(null,new object[]{account,unknown});
        Execute("UPDATE revora_account_sessions SET owner=NULL WHERE session_id=CAST(@name AS uuid)",unknown.ToString());
        type.GetMethod("Initialize",flags).Invoke(null,null);
        Check(Convert.ToInt64(Scalar("SELECT count(*) FROM revora_account_sessions WHERE session_id=CAST(@name AS uuid) AND closed_at IS NOT NULL",unknown.ToString()))==1,
            "legacy sessions without process ownership are closed on recovery");
        Guid blocked=Guid.NewGuid();
        open.Invoke(null,new object[]{account,blocked});
        Execute("UPDATE revora_account_sessions SET owner='22222222-2222-2222-2222-222222222222' WHERE session_id=CAST(@name AS uuid)",blocked.ToString());
        Execute("CREATE FUNCTION reject_recovery() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER reject_recovery BEFORE UPDATE ON revora_account_sessions FOR EACH ROW EXECUTE FUNCTION reject_recovery()");
        failed=false;
        try{type.GetMethod("Initialize",flags).Invoke(null,null);}catch(System.Reflection.TargetInvocationException e){failed=e.InnerException is InvalidOperationException;}
        finally{Execute("DROP TRIGGER reject_recovery ON revora_account_sessions; DROP FUNCTION reject_recovery()");}
        Check(failed && Convert.ToInt64(Scalar("SELECT count(*) FROM revora_account_sessions WHERE session_id=CAST(@name AS uuid) AND closed_at IS NULL",blocked.ToString()))==1,
            "failed recovery retains interrupted presence for retry");
        type.GetMethod("Initialize",flags).Invoke(null,null);
        Check(Convert.ToInt64(Scalar("SELECT count(*) FROM revora_account_sessions WHERE session_id=CAST(@name AS uuid) AND closed_at IS NOT NULL",blocked.ToString()))==1,
            "recovery retries after a failed write");
        Execute("CREATE FUNCTION reject_presence() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER reject_presence BEFORE UPDATE ON revora_account_sessions FOR EACH ROW EXECUTE FUNCTION reject_presence()");
        failed=false;
        try{close.Invoke(null,new object[]{newSession});}catch(System.Reflection.TargetInvocationException e){failed=e.InnerException is InvalidOperationException;}
        finally{Execute("DROP TRIGGER reject_presence ON revora_account_sessions; DROP FUNCTION reject_presence()");}
        Check(failed && Convert.ToInt64(Scalar("SELECT count(*) FROM revora_account_sessions WHERE account_name=lower(@name) AND closed_at IS NULL",account.Username))==1,
            "failed closure retains open presence rather than false inactivity");
        Execute("UPDATE revora_account_sessions SET opened_at='2099-01-01T00:00:00Z' WHERE session_id=CAST(@name AS uuid)",newSession.ToString());
        close.Invoke(null,new object[]{newSession});
        Check((bool)Scalar("SELECT closed_at>=opened_at FROM revora_account_sessions WHERE session_id=CAST(@name AS uuid)",newSession.ToString()),
            "clock rollback cannot close session before opening");
        account.EntityID=uid+1;failed=false;
        try{open.Invoke(null,new object[]{account,Guid.NewGuid()});}catch(System.Reflection.TargetInvocationException e){failed=e.InnerException is InvalidOperationException;}
        finally{account.EntityID=uid;}
        Check(failed,"stale character identity cannot open presence");
        var player=(Revora.Client.GameState)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Revora.Client.GameState));
        player.Account=account;
        player.Entity=new Revora.Game.Entity(Revora.Game.EntityFlag.Player,false) { UID=account.EntityID };
        var enter=type.GetMethod("Open",flags);var leave=type.GetMethod("Close",flags);
        enter.Invoke(null,new object[]{player});enter.Invoke(null,new object[]{player});
        Check(Convert.ToInt64(Scalar("SELECT count(*) FROM revora_account_sessions WHERE account_name=lower(@name) AND closed_at IS NULL",account.Username))==1,
            "same GameState opens only one durable session");
        leave.Invoke(null,new object[]{player});leave.Invoke(null,new object[]{player});
        Check(Convert.ToInt64(Scalar("SELECT count(*) FROM revora_account_sessions WHERE account_name=lower(@name) AND closed_at IS NULL",account.Username))==0,
            "GameState closure is idempotent");
        failed=false;
        try{enter.Invoke(null,new object[]{player});}catch(System.Reflection.TargetInvocationException e){failed=e.InnerException is InvalidOperationException;}
        Check(failed,"closed GameState cannot reopen presence");
        ReturnEvidence(account,type,flags);
        ReturnRules(type,flags);



    }
    static void ReturnRules(Type type,System.Reflection.BindingFlags flags)
    {
        var select=type.GetMethod("ReturnTitle",flags);
        DateTime returned=new DateTime(2030,1,2,0,0,0,DateTimeKind.Utc);
        long reset=new DateTime(2030,1,1).Ticks;
        Func<int,int,int,int,long,string> profile=(level,reborn,vip,days,ticks)=>string.Format(System.Globalization.CultureInfo.InvariantCulture,
            @"{{""version"":1,""level"":{0},""reborn"":{1},""vip_level"":{2},""vip_days"":{3},""last_reset"":{4},""timezone"":""UTC""}}",level,reborn,vip,days,ticks);
        for(int vip=0;vip<=7;vip++) {
            int expected=vip==0?20282028:vip<=4?20302030:vip<=6?20292029:20312031;
            Check((int)select.Invoke(null,new object[]{profile(120,0,vip,0,reset),returned})==expected,
                "return title follows frozen raw VIP 0 then login-floor tiers "+vip);
        }
        Check((int)select.Invoke(null,new object[]{profile(119,0,7,0,reset),returned})==0,
            "unreborn level 119 cannot earn return title");
        Check((int)select.Invoke(null,new object[]{profile(1,1,7,0,reset),returned})==20312031,
            "reborn character qualifies below level 120");
        Check((int)select.Invoke(null,new object[]{profile(120,3,7,0,reset),returned})==0 &&
            (int)select.Invoke(null,new object[]{profile(120,0,8,0,reset),returned})==0,
            "invalid rebirth or VIP snapshot cannot earn title");
        Check((int)select.Invoke(null,new object[]{profile(120,0,7,1,reset),returned.AddTicks(-1)})==20312031 &&
            (int)select.Invoke(null,new object[]{profile(120,0,7,1,reset),returned})==20292029,
            "last VIP day changes effective tier exactly at login reset boundary");
        Check((int)select.Invoke(null,new object[]{null,returned})==0 &&
            (int)select.Invoke(null,new object[]{"{}",returned})==0,
            "unknown profile cannot manufacture a return title");
    }

    static void ReturnEvidence(AccountTable account, Type type, System.Reflection.BindingFlags flags)
    {
        var observe=type.GetMethod("Authenticated",flags);
        var open=type.GetMethod("SessionOpened",flags);
        var close=type.GetMethod("SessionClosed",flags);
        string name=account.Username;
        const string count="SELECT count(*) FROM revora_account_returns WHERE account_name=lower(@name)";
        Execute("UPDATE revora_account_sessions SET opened_at=clock_timestamp()-interval '40 days',closed_at=clock_timestamp()-interval '31 days' WHERE account_name=lower(@name)",name);
        Execute("UPDATE revora_account_activity SET first_auth=clock_timestamp()-interval '40 days',last_auth=clock_timestamp()-interval '29 days' WHERE account_name=lower(@name)",name);
        observe.Invoke(null,new object[]{account});
        Check(Convert.ToInt64(Scalar(count,name))==0,"less than thirty days does not create return evidence");
        Execute("UPDATE revora_account_activity SET last_auth=clock_timestamp()-interval '31 days' WHERE account_name=lower(@name)",name);
        System.Threading.Tasks.Parallel.Invoke(()=>observe.Invoke(null,new object[]{account}),()=>observe.Invoke(null,new object[]{account}));
        Check(Convert.ToInt64(Scalar(count,name))==1,"concurrent first return creates one receipt before advancing authentication");
        Check((bool)Scalar("SELECT returned_at-inactive_since>=interval '30 days' AND character_uid=900001 FROM revora_account_returns WHERE account_name=lower(@name)",name),
            "return receipt freezes duration and character identity");
        Check((bool)Scalar("SELECT character_snapshot->>'version'='1' AND character_snapshot->>'level'='120' AND character_snapshot->>'reborn'='0' AND character_snapshot->>'vip_level'='7' AND character_snapshot->>'vip_days'='1' AND character_snapshot->>'last_reset'='638900000000000000' AND expires_at-returned_at=interval '720 hours' FROM revora_account_returns WHERE account_name=lower(@name)",name),
            "qualifying return freezes raw character VIP expiry inputs and thirty-day UTC term");
        Check(Convert.ToInt64(Scalar("SELECT title_key FROM revora_account_returns WHERE account_name=lower(@name)",name))==20292029,
            "expiring last VIP day selects the effective login tier at return");
        Execute("UPDATE entities SET viplevel=4,level=140,reborn=2 WHERE uid=900001");
        observe.Invoke(null,new object[]{account});
        Check((bool)Scalar("SELECT title_key=20292029 AND character_snapshot->>'vip_level'='7' AND character_snapshot->>'level'='120' AND character_snapshot->>'reborn'='0' AND expires_at-returned_at=interval '720 hours' FROM revora_account_returns WHERE account_name=lower(@name)",name),
            "later character changes and repeated login cannot rewrite original snapshot or term");

        Guid active=Guid.NewGuid();open.Invoke(null,new object[]{account,active});
        Execute("UPDATE revora_account_activity SET last_auth=clock_timestamp()-interval '31 days' WHERE account_name=lower(@name)",name);
        observe.Invoke(null,new object[]{account});
        Check(Convert.ToInt64(Scalar(count,name))==1,"open session prevents return evidence despite old authentication");
        close.Invoke(null,new object[]{active});
        Execute("UPDATE revora_account_activity SET last_auth=clock_timestamp()-interval '31 days' WHERE account_name=lower(@name)",name);
        observe.Invoke(null,new object[]{account});
        Check(Convert.ToInt64(Scalar(count,name))==1,"recent session closure prevents return evidence");
        Execute("UPDATE revora_account_sessions SET opened_at=clock_timestamp()-interval '40 days',closed_at=clock_timestamp()-interval '31 days' WHERE account_name=lower(@name)",name);
        Execute("UPDATE revora_account_activity SET first_auth=clock_timestamp()-interval '1 day',last_auth=clock_timestamp()-interval '31 days' WHERE account_name=lower(@name)",name);
        observe.Invoke(null,new object[]{account});
        Check(Convert.ToInt64(Scalar(count,name))==1,"insufficient observation history cannot qualify");
        Execute("UPDATE revora_account_activity SET first_auth=clock_timestamp()-interval '40 days',last_auth=clock_timestamp()-interval '31 days' WHERE account_name=lower(@name)",name);
        object prior=Scalar("SELECT last_auth FROM revora_account_activity WHERE account_name=lower(@name)",name);
        Execute("CREATE FUNCTION reject_snapshot() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER reject_snapshot BEFORE UPDATE ON revora_account_returns FOR EACH ROW EXECUTE FUNCTION reject_snapshot()");
        bool snapshotFailed=false;
        try{observe.Invoke(null,new object[]{account});}catch(System.Reflection.TargetInvocationException e){snapshotFailed=e.InnerException is InvalidOperationException;}
        finally{Execute("DROP TRIGGER reject_snapshot ON revora_account_returns; DROP FUNCTION reject_snapshot()");}
        Check(snapshotFailed && Convert.ToInt64(Scalar(count,name))==1 && object.Equals(prior,Scalar("SELECT last_auth FROM revora_account_activity WHERE account_name=lower(@name)",name)),
            "failed tier snapshot rolls back receipt and authentication timestamp together");
        Execute("CREATE FUNCTION reject_return() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER reject_return BEFORE INSERT ON revora_account_returns FOR EACH ROW EXECUTE FUNCTION reject_return()");
        bool failed=false;
        try{observe.Invoke(null,new object[]{account});}catch(System.Reflection.TargetInvocationException e){failed=e.InnerException is InvalidOperationException;}
        finally{Execute("DROP TRIGGER reject_return ON revora_account_returns; DROP FUNCTION reject_return()");}
        Check(failed && object.Equals(prior,Scalar("SELECT last_auth FROM revora_account_activity WHERE account_name=lower(@name)",name)),
            "failed receipt preserves prior activity for retry");
        Execute("CREATE FUNCTION reject_return_auth() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER reject_return_auth BEFORE UPDATE ON revora_account_activity FOR EACH ROW EXECUTE FUNCTION reject_return_auth()");
        failed=false;
        try{observe.Invoke(null,new object[]{account});}catch(System.Reflection.TargetInvocationException e){failed=e.InnerException is InvalidOperationException;}
        finally{Execute("DROP TRIGGER reject_return_auth ON revora_account_activity; DROP FUNCTION reject_return_auth()");}
        Check(failed && Convert.ToInt64(Scalar(count,name))==1,"failed authentication observation rolls back new return receipt");
        observe.Invoke(null,new object[]{new AccountTable(name.ToUpperInvariant())});
        Check(Convert.ToInt64(Scalar(count,name))==2,"later distinct inactivity interval saves another receipt after retry");
        Check(Convert.ToInt64(Scalar("SELECT count(*) FROM revora_account_returns WHERE account_name=lower(@name) AND character_snapshot->>'vip_level'='4' AND character_snapshot->>'level'='140' AND character_snapshot->>'reborn'='2'",name))==1,
            "new absence period snapshots its own contemporaneous profile");
        Check(Convert.ToInt64(Scalar("SELECT count(*) FROM revora_account_returns WHERE account_name=lower(@name) AND title_key=20302030",name))==1,
            "later return has its own frozen Sky Knight tier");
        Execute("UPDATE revora_account_returns SET character_snapshot=NULL,expires_at=NULL WHERE account_name=lower(@name)",name);
        type.GetMethod("Initialize",flags).Invoke(null,null);
        Check(Convert.ToInt64(Scalar("SELECT count(*) FROM revora_account_returns WHERE account_name=lower(@name) AND character_snapshot IS NULL AND expires_at IS NULL",name))==2,
            "schema initialization does not invent missing historical snapshots");
        Execute("DELETE FROM entities WHERE uid=900001");
        Execute("UPDATE revora_account_activity SET last_auth=clock_timestamp()-interval '31 days' WHERE account_name=lower(@name)",name);
        observe.Invoke(null,new object[]{account});
        Check(Convert.ToInt64(Scalar("SELECT count(*) FROM revora_account_returns WHERE account_name=lower(@name) AND character_snapshot IS NULL AND expires_at IS NOT NULL",name))==1,
            "account without a current character records no fabricated eligibility profile");
        Check(Convert.ToInt64(Scalar("SELECT count(*) FROM revora_account_returns WHERE account_name=lower(@name) AND title_key=0",name))==1,
            "missing return profile has no eligible title");
        Execute("INSERT INTO entities VALUES(900001,120,0,7,'1',7,638900000000000000)");
        Guid leftover=Guid.NewGuid();
        open.Invoke(null,new object[]{account,leftover});
        Execute("UPDATE revora_account_sessions SET owner='33333333-3333-3333-3333-333333333333',opened_at=clock_timestamp()-interval '40 days' WHERE session_id=CAST(@name AS uuid)",leftover.ToString());
        Execute("UPDATE revora_account_activity SET first_auth=clock_timestamp()-interval '40 days',last_auth=clock_timestamp()-interval '31 days' WHERE account_name=lower(@name)",name);
        object priorReturns=Scalar(count,name);
        observe.Invoke(null,new object[]{account});
        Check(object.Equals(priorReturns,Scalar(count,name)),"unrecovered previous-process presence still blocks return evidence");
        type.GetMethod("Initialize",flags).Invoke(null,null);
        Check(Convert.ToInt64(Scalar("SELECT count(*) FROM revora_account_sessions WHERE session_id=CAST(@name AS uuid) AND closed_at IS NOT NULL",leftover.ToString()))==1,
            "startup recovery closes the blocking previous-process session");
        Execute("UPDATE revora_account_sessions SET opened_at=clock_timestamp()-interval '40 days',closed_at=clock_timestamp()-interval '31 days' WHERE session_id=CAST(@name AS uuid)",leftover.ToString());
        Execute("UPDATE revora_account_activity SET last_auth=clock_timestamp()-interval '31 days' WHERE account_name=lower(@name)",name);
        observe.Invoke(null,new object[]{account});
        Check(Convert.ToInt64(Scalar(count,name))==Convert.ToInt64(priorReturns)+1,
            "recovered previous-process session can qualify after thirty days");
        VerifyProcessLease(account,type,flags);
        VerifyLeaseLoss(account,type,flags);
        VerifyHeldLeaseLoss(account,type,flags);
        VerifyClosedConnectionFence(account,type,flags);


    }

    static void VerifierAccounts()
    {
        var created = new AccountTable("VerifierPlayer") { Password = "Synthetic;Only-1", State = AccountTable.AccountState.Player };
        created.Insert();
        Check(created.HasSrpVerifier && created.Password == "", "new account clears transient password");
        Check((string)Scalar("SELECT password FROM accounts WHERE username=@name", created.Username) == "!revora-srp6711-v1!", "only format marker persisted in legacy field");
        Check(Convert.ToInt64(Scalar("SELECT count(*) FROM revora_account_srp WHERE username=@name AND octet_length(salt)=32 AND octet_length(verifier)=32", created.Username)) == 1, "one bounded verifier persisted");
        var loaded = new AccountTable("verifierplayer");
        Check(loaded.exists && loaded.State == AccountTable.AccountState.Player && loaded.Password == "" && loaded.HasSrpVerifier, "ordinary verifier account reload");
        Check(Authenticate(loaded, "Synthetic;Only-1") && !Authenticate(loaded, "wrong"), "reloaded credential proves correct password only");
        loaded.IP = "127.0.0.1"; loaded.EntityID = 900001; loaded.Save();
        loaded = new AccountTable("VERIFIERPLAYER");
        Check(loaded.EntityID == 900001 && Authenticate(loaded, "Synthetic;Only-1"), "profile save preserves credential");
        Activity(loaded);
        loaded.Password = "Synthetic;Only-2"; loaded.Save();
        loaded = new AccountTable("VerifierPlayer");
        Check(Authenticate(loaded, "Synthetic;Only-2") && !Authenticate(loaded, "Synthetic;Only-1"), "password reset replaces verifier without plaintext storage");
        Execute("INSERT INTO accounts(username,password) VALUES(@name,@password)", "LegacyUpgrade", "SyntheticLegacy");
        var legacy = new AccountTable("LegacyUpgrade");
        legacy.IP = "127.0.0.1"; legacy.Save();
        Check((string)Scalar("SELECT password FROM accounts WHERE username=@name", legacy.Username) == "SyntheticLegacy", "profile save does not silently migrate legacy identity");
        legacy.Password = "SyntheticReplacement"; legacy.Save();
        Check(new AccountTable("LegacyUpgrade").HasSrpVerifier && Authenticate(new AccountTable("LegacyUpgrade"), "SyntheticReplacement"), "explicit legacy password reset converts to verifier");
        bool duplicate = false;
        try { new AccountTable("VerifierPlayer") { Username = "verifierplayer", Password = "Duplicate", State = AccountTable.AccountState.Player }.Insert(); }
        catch (InvalidOperationException) { duplicate = true; }
        Check(duplicate && Convert.ToInt64(Scalar("SELECT count(*) FROM accounts WHERE lower(username)=lower(@name)", "VerifierPlayer")) == 1, "case-folded duplicate creation rejected");
        Execute("CREATE FUNCTION suppress_verifier() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER suppress_verifier BEFORE INSERT ON revora_account_srp FOR EACH ROW WHEN (NEW.username='SuppressedVerifier') EXECUTE FUNCTION suppress_verifier()");
        bool failed = false;
        try { new AccountTable("SuppressedVerifier") { Password = "SyntheticRollback", State = AccountTable.AccountState.Player }.Insert(); }
        catch (InvalidOperationException) { failed = true; }
        Check(failed && Convert.ToInt64(Scalar("SELECT count(*) FROM accounts WHERE username=@name", "SuppressedVerifier")) == 0, "suppressed verifier insert rolls back account row");
        Execute("DROP TRIGGER suppress_verifier ON revora_account_srp; DROP FUNCTION suppress_verifier()");
        Execute("UPDATE revora_account_srp SET verifier=decode(repeat('00',32),'hex') WHERE username=@name", "VerifierPlayer");
        Check(!new AccountTable("VerifierPlayer").exists, "zero verifier fails closed");
        Execute("DELETE FROM revora_account_srp WHERE username=@name", "VerifierPlayer");
        Check(!new AccountTable("VerifierPlayer").exists, "missing verifier does not fall back to empty password");
    }

    static int HolderChild()
    {
        try
        {
            string connection = System.Console.ReadLine();
            if (string.IsNullOrEmpty(connection) || connection.Length > 8192) return 2;
            var builder = new NpgsqlConnectionStringBuilder(connection);
            if (builder.SearchPath != "revora_login_validation") return 2;
            DataHolder.ConnectionString = builder.ConnectionString;
            var type = typeof(AccountTable).Assembly.GetType("Revora.Database.AccountActivity");
            var flags = BindingFlags.Static | BindingFlags.NonPublic;
            type.GetMethod("Initialize", flags).Invoke(null, null);
            var account = new AccountTable("VerifierPlayer");
            if (!account.exists || account.EntityID != 900001) return 2;
            Guid session = Guid.NewGuid();
            type.GetMethod("SessionOpened", flags).Invoke(null, new object[] { account, session });
            int pid = (int)type.GetMethod("BackendPid", flags).Invoke(null, null);
            System.Console.WriteLine("HOLDER_READY " + session.ToString("D") + " " + pid);
            System.Console.Out.Flush();
            for (;;)
            {
                string command = System.Console.ReadLine();
                if (command == null || command == "QUIT") return 0;
                if (command == "PROBE")
                {
                    bool serving = false;
                    try { type.GetMethod("SessionOpened", flags).Invoke(null, new object[] { account, Guid.NewGuid() }); serving = true; }
                    catch (TargetInvocationException) { }
                    try { type.GetMethod("Authenticated", flags).Invoke(null, new object[] { account }); serving = true; }
                    catch (TargetInvocationException) { }
                    try { type.GetMethod("SessionClosed", flags).Invoke(null, new object[] { session }); }
                    catch (TargetInvocationException) { serving = true; }
                    System.Console.WriteLine(serving ? "HOLDER_SERVING" : "HOLDER_FENCED");
                    System.Console.Out.Flush();
                }
                else if (command == "GAP")
                {
                    var hook = type.GetField("AfterLeaseHeld", flags);
                    hook.SetValue(null, new Action<NpgsqlConnection>(c =>
                    {
                        System.Console.WriteLine("HOLDER_HOLDING");
                        System.Console.Out.Flush();
                        System.Console.ReadLine();
                        hook.SetValue(null, null);
                        c.Close();
                    }));
                    bool recovered = false;
                    try { type.GetMethod("Initialize", flags).Invoke(null, null); recovered = true; }
                    catch (TargetInvocationException) { }
                    try { hook.SetValue(null, null); } catch { }
                    bool fencedNow = (bool)type.GetMethod("IsFenced", flags).Invoke(null, null);
                    System.Console.WriteLine(recovered || !fencedNow ? "HOLDER_RECOVERED" : "HOLDER_FENCED");
                    System.Console.Out.Flush();
                }
                else if (command == "FENCE_CHECK")
                {
                    bool acquired = false;
                    try { type.GetMethod("Initialize", flags).Invoke(null, null); acquired = true; }
                    catch (TargetInvocationException) { }
                    System.Console.WriteLine(acquired ? "HOLDER_REACQUIRED" : "HOLDER_STILL_FENCED");
                    System.Console.Out.Flush();
                }
                else if (command == "REINIT")
                {
                    bool acquired = false;
                    try { type.GetMethod("Relinquish", flags).Invoke(null, null); type.GetMethod("Initialize", flags).Invoke(null, null); acquired = true; }
                    catch (TargetInvocationException) { }
                    System.Console.WriteLine(acquired ? "HOLDER_REACQUIRED" : "HOLDER_STILL_FENCED");
                    System.Console.Out.Flush();
                }
            }
        }
        catch
        {
            System.Console.WriteLine("HOLDER_FAILED");
            return 1;
        }
    }

    static int ChallengerChild()
    {
        try
        {
            string connection = System.Console.ReadLine();
            if (string.IsNullOrEmpty(connection) || connection.Length > 8192) return 2;
            var builder = new NpgsqlConnectionStringBuilder(connection);
            if (builder.SearchPath != "revora_login_validation") return 2;
            DataHolder.ConnectionString = builder.ConnectionString;
            var type = typeof(AccountTable).Assembly.GetType("Revora.Database.AccountActivity");
            type.GetMethod("Initialize", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            System.Console.WriteLine("CHALLENGER_ACQUIRED");
            return 1;
        }
        catch (TargetInvocationException ex)
        {
            System.Console.WriteLine(ex.InnerException is InvalidOperationException ? "CHALLENGER_DENIED" : "CHALLENGER_FAILED");
            return ex.InnerException is InvalidOperationException ? 0 : 1;
        }
        catch
        {
            System.Console.WriteLine("CHALLENGER_FAILED");
            return 1;
        }
    }

    static void VerifyProcessLease(AccountTable account, Type type, BindingFlags flags)
    {
        type.GetMethod("Relinquish", flags).Invoke(null, null);
        var holderStart = new ProcessStartInfo {
            FileName = typeof(AccountLookupTests).Assembly.Location, Arguments = "--homecoming-holder",
            WorkingDirectory = Environment.CurrentDirectory, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        using (var holder = Process.Start(holderStart))
        {
            holder.StandardInput.WriteLine(DataHolder.ConnectionString);
            holder.StandardInput.Flush();
            var readyTask = holder.StandardOutput.ReadLineAsync();
            if (!readyTask.Wait(15000) || holder.HasExited)
            {
                try { holder.Kill(); holder.WaitForExit(); } catch { }
                throw new InvalidOperationException("Homecoming holder process did not become ready.");
            }
            string ready = readyTask.Result;
            Guid held;
            int ignoredPid;
            if (!TryParseHolderReady(ready, out held, out ignoredPid))
                throw new InvalidOperationException("Homecoming holder process failed to publish its session.");
            bool denied = false;
            try { type.GetMethod("Initialize", flags).Invoke(null, null); }
            catch (TargetInvocationException e) { denied = e.InnerException is InvalidOperationException; }
            Check(denied && Convert.ToInt64(Scalar("SELECT count(*) FROM revora_account_sessions WHERE session_id=CAST(@name AS uuid) AND closed_at IS NULL", held.ToString())) == 1,
                "a live Homecoming holder keeps its session while this process cannot take the lease");
            var challengeStart = new ProcessStartInfo {
                FileName = typeof(AccountLookupTests).Assembly.Location, Arguments = "--homecoming-challenger",
                WorkingDirectory = Environment.CurrentDirectory, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using (var challenger = Process.Start(challengeStart))
            {
                var challengeOut = challenger.StandardOutput.ReadToEndAsync();
                var challengeErr = challenger.StandardError.ReadToEndAsync();
                challenger.StandardInput.WriteLine(DataHolder.ConnectionString);
                challenger.StandardInput.Close();
                if (!challenger.WaitForExit(15000)) { challenger.Kill(); challenger.WaitForExit(); throw new InvalidOperationException("Homecoming challenger timed out."); }
                string challenge = challengeOut.GetAwaiter().GetResult();
                challengeErr.GetAwaiter().GetResult();
                Check(challenger.ExitCode == 0 && challenge.IndexOf("CHALLENGER_DENIED", StringComparison.Ordinal) >= 0
                    && Convert.ToInt64(Scalar("SELECT count(*) FROM revora_account_sessions WHERE session_id=CAST(@name AS uuid) AND closed_at IS NULL", held.ToString())) == 1,
                    "a second process cannot recover the live holder's sessions");
            }
            holder.StandardInput.Close();
            if (!holder.WaitForExit(15000)) { holder.Kill(); holder.WaitForExit(); throw new InvalidOperationException("Homecoming holder did not exit."); }
            type.GetMethod("Initialize", flags).Invoke(null, null);
            Check(Convert.ToInt64(Scalar("SELECT count(*) FROM revora_account_sessions WHERE session_id=CAST(@name AS uuid) AND closed_at IS NOT NULL", held.ToString())) == 1,
                "takeover after the holder process exits recovers its interrupted session");
        }
    }

    static bool TryParseHolderReady(string ready, out Guid session, out int pid)
    {
        session = Guid.Empty;
        pid = 0;
        if (ready == null || !ready.StartsWith("HOLDER_READY ", StringComparison.Ordinal)) return false;
        string[] parts = ready.Substring("HOLDER_READY ".Length).Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && Guid.TryParse(parts[0], out session) && int.TryParse(parts[1], out pid) && pid > 0;
    }

    static string HolderLine(Process holder, string what)
    {
        var task = holder.StandardOutput.ReadLineAsync();
        if (!task.Wait(15000) || holder.HasExited)
        {
            try { holder.Kill(); holder.WaitForExit(); } catch { }
            throw new InvalidOperationException("Homecoming holder did not report " + what + ".");
        }
        return task.Result;
    }

    static bool TerminateBackend(int pid)
    {
        using (var c = DataHolder.MySqlConnection)
        using (var command = new NpgsqlCommand("SELECT pg_terminate_backend(@pid)", c))
        {
            c.Open();
            command.Parameters.AddWithValue("pid", pid);
            return Equals(command.ExecuteScalar(), true);
        }
    }

    static bool BackendAlive(int pid)
    {
        using (var c = DataHolder.MySqlConnection)
        using (var command = new NpgsqlCommand("SELECT count(*) FROM pg_stat_activity WHERE pid=@pid", c))
        {
            c.Open();
            command.Parameters.AddWithValue("pid", pid);
            return Convert.ToInt64(command.ExecuteScalar()) > 0;
        }
    }

    static void VerifyLeaseLoss(AccountTable account, Type type, BindingFlags flags)
    {
        type.GetMethod("Relinquish", flags).Invoke(null, null);
        Execute("UPDATE revora_account_activity SET first_auth=clock_timestamp()-interval '40 days',last_auth=clock_timestamp()-interval '31 days' WHERE account_name=lower(@name)", account.Username);
        object returnsBefore = Scalar("SELECT count(*) FROM revora_account_returns WHERE account_name=lower(@name)", account.Username);
        var holderStart = new ProcessStartInfo {
            FileName = typeof(AccountLookupTests).Assembly.Location, Arguments = "--homecoming-holder",
            WorkingDirectory = Environment.CurrentDirectory, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        using (var holder = Process.Start(holderStart))
        {
            holder.StandardInput.WriteLine(DataHolder.ConnectionString);
            holder.StandardInput.Flush();
            Guid held;
            int pid;
            if (!TryParseHolderReady(HolderLine(holder, "ready"), out held, out pid))
            {
                try { holder.Kill(); holder.WaitForExit(); } catch { }
                throw new InvalidOperationException("Homecoming holder process failed to publish its lease backend.");
            }
            Check(Convert.ToInt64(Scalar("SELECT count(*) FROM revora_account_sessions WHERE session_id=CAST(@name AS uuid) AND closed_at IS NULL", held.ToString())) == 1,
                "lease-loss holder published an open session before backend death");
            Check(TerminateBackend(pid), "the test role can terminate the holder lease backend");
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (BackendAlive(pid) && DateTime.UtcNow < deadline) System.Threading.Thread.Sleep(50);
            Check(!BackendAlive(pid), "holder lease backend is gone while the holder process remains alive");
            Check(!holder.HasExited, "holder process stays alive after its lease backend is terminated");
            holder.StandardInput.WriteLine("PROBE");
            holder.StandardInput.Flush();
            Check(HolderLine(holder, "probe") == "HOLDER_FENCED",
                "fenced holder cannot open sessions or authenticate after lease loss");
            Check(object.Equals(returnsBefore, Scalar("SELECT count(*) FROM revora_account_returns WHERE account_name=lower(@name)", account.Username)),
                "fenced holder cannot qualify a Homecoming return after lease loss");
            Check(Convert.ToInt64(Scalar("SELECT count(*) FROM revora_account_sessions WHERE session_id=CAST(@name AS uuid) AND closed_at IS NULL", held.ToString())) == 1,
                "uncertain closure is retained until a living owner recovers the session");
            type.GetMethod("Initialize", flags).Invoke(null, null);
            Check(Convert.ToInt64(Scalar("SELECT count(*) FROM revora_account_sessions WHERE session_id=CAST(@name AS uuid) AND closed_at IS NOT NULL", held.ToString())) == 1,
                "takeover while the fenced holder process is still alive recovers its interrupted session");
            holder.StandardInput.WriteLine("REINIT");
            holder.StandardInput.Flush();
            Check(HolderLine(holder, "reinit") == "HOLDER_STILL_FENCED",
                "fenced holder cannot silently reacquire Homecoming ownership");
            holder.StandardInput.WriteLine("QUIT");
            holder.StandardInput.Close();
            if (!holder.WaitForExit(15000)) { holder.Kill(); holder.WaitForExit(); throw new InvalidOperationException("Homecoming holder did not exit after lease loss."); }
        }
    }

    static void VerifyHeldLeaseLoss(AccountTable account, Type type, BindingFlags flags)
    {
        type.GetMethod("Relinquish", flags).Invoke(null, null);
        var holderStart = new ProcessStartInfo {
            FileName = typeof(AccountLookupTests).Assembly.Location, Arguments = "--homecoming-holder",
            WorkingDirectory = Environment.CurrentDirectory, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        using (var holder = Process.Start(holderStart))
        {
            holder.StandardInput.WriteLine(DataHolder.ConnectionString);
            holder.StandardInput.Flush();
            Guid held;
            int pid;
            if (!TryParseHolderReady(HolderLine(holder, "ready"), out held, out pid))
            {
                try { holder.Kill(); holder.WaitForExit(); } catch { }
                throw new InvalidOperationException("Homecoming holder process failed to publish its lease backend.");
            }
            holder.StandardInput.WriteLine("GAP");
            holder.StandardInput.Flush();
            Check(HolderLine(holder, "holding") == "HOLDER_HOLDING",
                "holder reached recovery after the lease was confirmed held");
            Check(TerminateBackend(pid), "the test role can terminate the holder lease during recovery");
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (BackendAlive(pid) && DateTime.UtcNow < deadline) System.Threading.Thread.Sleep(50);
            Check(!BackendAlive(pid) && !holder.HasExited,
                "holder process stays alive after lease death between hold and recovery");
            holder.StandardInput.WriteLine("PROCEED");
            holder.StandardInput.Flush();
            Check(HolderLine(holder, "gap") == "HOLDER_FENCED",
                "lease death during recovery fences the holder immediately");
            holder.StandardInput.WriteLine("FENCE_CHECK");
            holder.StandardInput.Flush();
            Check(HolderLine(holder, "fence-check") == "HOLDER_STILL_FENCED",
                "Initialize without Relinquish stays fenced after Recover lease death");
            holder.StandardInput.WriteLine("REINIT");
            holder.StandardInput.Flush();
            Check(HolderLine(holder, "reinit-after-gap") == "HOLDER_STILL_FENCED",
                "Relinquish cannot unfence a process that lost the lease while holding it");
            type.GetMethod("Initialize", flags).Invoke(null, null);
            Check(Convert.ToInt64(Scalar("SELECT count(*) FROM revora_account_sessions WHERE session_id=CAST(@name AS uuid) AND closed_at IS NOT NULL", held.ToString())) == 1,
                "successor recovers the session after mid-hold lease loss");
            holder.StandardInput.WriteLine("QUIT");
            holder.StandardInput.Close();
            if (!holder.WaitForExit(15000)) { holder.Kill(); holder.WaitForExit(); throw new InvalidOperationException("Homecoming holder did not exit after mid-hold lease loss."); }
        }
    }

    static int BreakChild(string mode)
    {
        try
        {
            string connection = System.Console.ReadLine();
            if (string.IsNullOrEmpty(connection) || connection.Length > 8192) return 2;
            var builder = new NpgsqlConnectionStringBuilder(connection);
            if (builder.SearchPath != "revora_login_validation") return 2;
            DataHolder.ConnectionString = builder.ConnectionString;
            var type = typeof(AccountTable).Assembly.GetType("Revora.Database.AccountActivity");
            var flags = BindingFlags.Static | BindingFlags.NonPublic;
            type.GetMethod("Initialize", flags).Invoke(null, null);
            var account = new AccountTable("VerifierPlayer");
            if (!account.exists || account.EntityID != 900001) return 2;
            var hook = type.GetField("AfterLeaseHeld", flags);
            if (mode == "close")
            {
                Guid session = Guid.NewGuid();
                type.GetMethod("SessionOpened", flags).Invoke(null, new object[] { account, session });
                hook.SetValue(null, new Action<NpgsqlConnection>(c => { hook.SetValue(null, null); c.Close(); }));
                type.GetMethod("SessionClosed", flags).Invoke(null, new object[] { session });
            }
            else
            {
                hook.SetValue(null, new Action<NpgsqlConnection>(c => { hook.SetValue(null, null); c.Close(); }));
                try
                {
                    if (mode == "recover") type.GetMethod("Initialize", flags).Invoke(null, null);
                    else if (mode == "open") type.GetMethod("SessionOpened", flags).Invoke(null, new object[] { account, Guid.NewGuid() });
                    else if (mode == "auth") type.GetMethod("Authenticated", flags).Invoke(null, new object[] { account });
                    else return 2;
                }
                catch (TargetInvocationException) { }
            }
            bool fenced = (bool)type.GetMethod("IsFenced", flags).Invoke(null, null);
            bool acquired = false;
            try { type.GetMethod("Initialize", flags).Invoke(null, null); acquired = true; }
            catch (TargetInvocationException) { }
            System.Console.WriteLine(fenced && !acquired ? "BREAK_FENCED" : "BREAK_OPEN");
            return fenced && !acquired ? 0 : 1;
        }
        catch
        {
            System.Console.WriteLine("BREAK_FAILED");
            return 1;
        }
    }

    static void VerifyClosedConnectionFence(AccountTable account, Type type, BindingFlags flags)
    {
        foreach (string mode in new[] { "recover", "open", "auth", "close" })
        {
            type.GetMethod("Relinquish", flags).Invoke(null, null);
            var start = new ProcessStartInfo {
                FileName = typeof(AccountLookupTests).Assembly.Location, Arguments = "--homecoming-break " + mode,
                WorkingDirectory = Environment.CurrentDirectory, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using (var child = Process.Start(start))
            {
                var output = child.StandardOutput.ReadLineAsync();
                child.StandardInput.WriteLine(DataHolder.ConnectionString);
                child.StandardInput.Close();
                if (!child.WaitForExit(20000)) { child.Kill(); child.WaitForExit(); throw new InvalidOperationException("Homecoming break child timed out."); }
                string line = output.Wait(5000) ? output.Result : null;
                Check(child.ExitCode == 0 && line == "BREAK_FENCED",
                    "closed lease connection during " + mode + " fences immediately without Relinquish");
            }
        }
        type.GetMethod("Initialize", flags).Invoke(null, null);
    }

    static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--homecoming-holder") return HolderChild();
        if (args.Length == 1 && args[0] == "--homecoming-challenger") return ChallengerChild();
        if (args.Length == 2 && args[0] == "--homecoming-break") return BreakChild(args[1]);

        string original = null;
        bool created = false;
        try
        {
            var ini = new IniFile("Configuracion.ini");
            string host = ini.ReadString("MySql", "Host"), port = ini.ReadString("MySql", "Port");
            if (!string.IsNullOrEmpty(port) && host.IndexOf("Port=", StringComparison.OrdinalIgnoreCase) < 0)
                host = host.Trim().TrimEnd(';') + ";Port=" + port;
            DataHolder.CreateConnection(ini.ReadString("MySql", "Username"), ini.ReadString("MySql", "Password"),
                ini.ReadString("MySql", "Database"), host);
            original = DataHolder.ConnectionString;
            // No IF NOT EXISTS: never reuse or remove someone else's fixture.
            Execute("CREATE SCHEMA revora_login_validation");
            created = true;
            var builder = new NpgsqlConnectionStringBuilder(original);
            builder.SearchPath = "revora_login_validation";
            DataHolder.ConnectionString = builder.ConnectionString;
            Execute("CREATE TABLE accounts(username text NOT NULL PRIMARY KEY,password text NOT NULL," +
                "ip text DEFAULT '',entityid bigint DEFAULT 0,lastcheck bigint DEFAULT 0," +
                "state smallint DEFAULT 2,email text DEFAULT '',idreferido bigint DEFAULT 0)");
            Execute("CREATE TABLE entities(uid bigint PRIMARY KEY,level integer,reborn integer,viplevel integer,vipdays text,vipleveldays integer,lastresettime bigint)");
            Execute("INSERT INTO entities VALUES(900001,120,0,7,'1',7,638900000000000000)");
            const string secret = "SyntheticOnly7!";
            Execute("INSERT INTO accounts(username,password) VALUES(@name,@password)", "MixedPlayer", secret);
            foreach (string spelling in new[] { "MixedPlayer", "mixedplayer", "MIXEDPLAYER", "mIxEdPlAyEr" })
            {
                var account = new AccountTable(spelling);
                Check(account.exists && account.State == AccountTable.AccountState.Player, "ordinary account lookup " + spelling);
                Check(account.Username == "MixedPlayer", "canonical spelling preserved " + spelling);
                Check(account.Password == secret && account.Password != secret.ToLowerInvariant(), "password remains exact " + spelling);
            }
            Check(!new AccountTable("missingplayer").exists, "unknown account rejected");
            Check(!new AccountTable("' OR true --").exists, "SQL input stays a parameter");
            Check(!new AccountTable(null).exists && !new AccountTable("").exists, "empty account rejected");
            Check(!new AccountTable(" MixedPlayer").exists, "no extra whitespace normalization");
            Execute("UPDATE accounts SET state=1 WHERE username=@name", "MixedPlayer");
            Check(new AccountTable("mixedplayer").State == AccountTable.AccountState.Banned, "ban state preserved");
            Execute("INSERT INTO accounts(username,password) VALUES(@name,@password)", "mixedplayer", "OtherSynthetic9!");
            foreach (string spelling in new[] { "MixedPlayer", "mixedplayer", "MIXEDPLAYER" })
            {
                var account = new AccountTable(spelling);
                Check(!account.exists && account.Password == "" && account.State == AccountTable.AccountState.DoesntExist,
                    "ambiguous legacy identity refused " + spelling);
            }
            VerifierAccounts();
            System.Console.WriteLine("Account lookup checks passed: " + checks);
            return 0;
        }
        catch (Exception error)
        {
            // Database exception text can include SQL/configuration; do not print it.
            string fail = error.GetType().Name;
            for (var inner = error.InnerException; inner != null; inner = inner.InnerException)
                fail += "/" + inner.GetType().Name;
            System.Console.WriteLine("Account lookup tests failed: " + fail);
            System.Console.Out.Flush();
            return 1;
        }
        finally
        {
            if (original != null) DataHolder.ConnectionString = original;
            if (created) Execute("DROP SCHEMA revora_login_validation CASCADE");
        }
    }
}
