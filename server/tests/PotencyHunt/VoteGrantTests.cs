using System;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.Features;

internal static partial class PotencyHuntTests
{
    static readonly Type VoteGrants=typeof(Entity).Assembly.GetType("Revora.Game.Features.VoteGrantDelivery");
    static long VoteRows(GameState p,string status){return Scalar("SELECT count(*) FROM xtremetop_votes WHERE entity_uid=@uid AND status='"+status+"'",p.Entity.UID);}
    static GameState VotePlayer(Map town,string name,uint cp,uint rvp,ulong gold){
        var p=WelcomePlayer(town,1,0,15);p.Account.Username=name;p.Entity.FullyLoaded=true;p.Action=2;Kernel.GamePool[p.Entity.UID]=p;
        Query("UPDATE entities SET conquerpoints=@cp,revorapoint=@rvp,money=@gold,name=@name WHERE uid=@uid","cp",(long)cp,"rvp",(long)rvp,"gold",checked((long)gold),"name",name,"uid",(long)p.Entity.UID);
        Call(typeof(Entity),p.Entity,"ApplyCommittedConquerPoints",cp);Call(typeof(Entity),p.Entity,"ApplyCommittedRevoraPoints",rvp);p.Entity.Money=gold;
        Query("INSERT INTO accounts(username,entityid) VALUES(@name,@uid)","name",name,"uid",(long)p.Entity.UID);
        p=ReloadBall(p);ArmVote(p);
        return p;
    }
    static Guid VoteRow(GameState p,string status,int cp,int rvp,long gold){
        Guid id=Guid.NewGuid();
        Query("INSERT INTO xtremetop_votes(vote_id,token,account_id,username,entity_uid,entity_name,status,cp_amount,rvp_amount,gold_amount,voted_at) VALUES(@id,@token,@account,@name,@uid,@ename,@status,@cp,@rvp,@gold,@voted)",
            "id",id,"token","ABCDEF0123456789ABCDEF0123456789","account",Guid.NewGuid(),"name",p.Account.Username,"uid",(long)p.Entity.UID,"ename",p.Entity.Name,"status",status,"cp",cp,"rvp",rvp,"gold",gold,"voted",status=="voted"?(object)DateTime.UtcNow:DBNull.Value);
        return id;
    }
    static void ArmVote(GameState p){p.Account.Username=p.Entity.Name;p.Entity.FullyLoaded=true;p.Action=2;Kernel.GamePool[p.Entity.UID]=p;}
    static void VerifyVoteGrants(string runtime,Map town){
        Query("ALTER TABLE entities ADD COLUMN IF NOT EXISTS conquerpoints bigint NOT NULL DEFAULT 0, ADD COLUMN IF NOT EXISTS revorapoint bigint NOT NULL DEFAULT 0, ADD COLUMN IF NOT EXISTS money bigint NOT NULL DEFAULT 0");
        Query("CREATE TABLE accounts(username text PRIMARY KEY, entityid bigint NOT NULL)");
        Query("CREATE TABLE xtremetop_votes(vote_id uuid PRIMARY KEY, token text, account_id uuid, username text NOT NULL, entity_uid bigint NOT NULL, entity_name text, status text NOT NULL, voting_ip text, cp_amount integer NOT NULL, rvp_amount integer NOT NULL, gold_amount bigint NOT NULL, created_at timestamptz NOT NULL DEFAULT now(), voted_at timestamptz, delivered_at timestamptz)");
        VoteGrants.GetField("Relation",Hidden).SetValue(null,"xtremetop_votes");
        string service=System.IO.File.ReadAllText(System.IO.Path.GetFullPath(System.IO.Path.Combine(runtime,"../../Game/Features/VoteGrantDelivery.cs")));
        Check(service.IndexOf("identity.xtremetop_votes")>=0 && service.IndexOf("status='delivered'")>=0 && service.IndexOf("FOR UPDATE")>=0,
            "production vote delivery still targets identity.xtremetop_votes and locks the grant");
        string heard="";VoteGrants.GetField("Spoken",Hidden).SetValue(null,(Action<string>)(m=>{heard=heard+m+"|";}));
        var payer=VotePlayer(town,"Voter",10,3,1000);VoteRow(payer,"voted",2500,50,50000);
        VoteGrantDelivery.ApplyFor(payer);
        Check(!Fenced(payer) && payer.Entity.ConquerPoints==2510 && payer.Entity.RevoraPoint==53 && payer.Entity.Money==51000
            && VoteRows(payer,"delivered")==1 && VoteRows(payer,"voted")==0 && heard.IndexOf("You received 2500 CPs, 50 Revora Points, and 50000 gold.")>=0,
            "ordinary ApplyFor pays the stored 2500/50/50000 vote exactly once");
        VoteGrantDelivery.ApplyFor(payer);Check(VoteRows(payer,"delivered")==1 && payer.Entity.ConquerPoints==2510,"a delivered vote cannot pay again");
        payer=ReloadBall(payer);ArmVote(payer);VoteGrantDelivery.Pulse();
        Check(!Fenced(payer) && payer.Entity.ConquerPoints==2510 && payer.Entity.RevoraPoint==53 && payer.Entity.Money==51000 && VoteRows(payer,"delivered")==1,
            "production reload keeps one vote award and Pulse does not repeat it");
        var counted=VotePlayer(town,"Counted",10,3,1000);Guid countedId=VoteRow(counted,"voted",2500,50,50000);
        Query("UPDATE xtremetop_votes SET voting_ip=NULL, cp_amount=2500, rvp_amount=50, gold_amount=50000 WHERE vote_id=@id","id",countedId);
        VoteGrantDelivery.ApplyFor(counted);
        Check(!Fenced(counted) && counted.Entity.ConquerPoints==2510 && counted.Entity.RevoraPoint==53 && counted.Entity.Money==51000
            && VoteRows(counted,"delivered")==1 && VoteRows(counted,"voted")==0,
            "a public-count row with no voter address pays the ordinary non-GM the stored 2500/50/50000 once");
        counted=ReloadBall(counted);ArmVote(counted);VoteGrantDelivery.ApplyFor(counted);VoteGrantDelivery.Pulse();
        Check(!Fenced(counted) && counted.Entity.ConquerPoints==2510 && counted.Entity.RevoraPoint==53 && counted.Entity.Money==51000 && VoteRows(counted,"delivered")==1,
            "reload does not repeat a public-count vote");
        var unflushed=VotePlayer(town,"Unflushed",10,3,1000);unflushed=ReloadBall(unflushed);ArmVote(unflushed);
        unflushed.Entity.AdjustMoney(17);VoteRow(unflushed,"voted",2500,50,50000);VoteGrantDelivery.ApplyFor(unflushed);
        Check(!Fenced(unflushed) && unflushed.Entity.Money==51017 && VoteRows(unflushed,"delivered")==1,
            "ordinary unsaved silver gains are preserved with the vote");
        EntityTable.SaveEntity(unflushed);unflushed=ReloadBall(unflushed);ArmVote(unflushed);
        Check(unflushed.Entity.Money==51017 && unflushed.Entity.RevoraPoint==53,"autosave and production reload retain the gain and vote");
        var spent=VotePlayer(town,"Spent",10,3,1000);spent=ReloadBall(spent);ArmVote(spent);
        spent.Entity.AdjustMoney(-17);VoteRow(spent,"voted",2500,50,50000);VoteGrantDelivery.ApplyFor(spent);
        Check(!Fenced(spent) && spent.Entity.Money==50983 && VoteRows(spent,"delivered")==1,"vote does not restore silver already spent in the session");
        var unflushedRvp=VotePlayer(town,"UnflushedRvp",10,3,1000);unflushedRvp=ReloadBall(unflushedRvp);ArmVote(unflushedRvp);
        unflushedRvp.Entity.AddConquerPoints(4);unflushedRvp.Entity.AddRevoraPoints(7);VoteRow(unflushedRvp,"voted",2500,50,50000);heard="";VoteGrantDelivery.ApplyFor(unflushedRvp);
        Check(!Fenced(unflushedRvp) && unflushedRvp.Entity.ConquerPoints==2514 && unflushedRvp.Entity.RevoraPoint==60 && VoteRows(unflushedRvp,"delivered")==1 && heard.IndexOf("You received 2500 CPs, 50 Revora Points, and 50000 gold.")>=0,
            "unsaved CP and RVP gains commit with the vote instead of being fenced or deferred");
        Check(EntityTable.SaveEntity(unflushedRvp),"the combined purse remains saveable");unflushedRvp=ReloadBall(unflushedRvp);ArmVote(unflushedRvp);
        Check(!Fenced(unflushedRvp) && unflushedRvp.Entity.ConquerPoints==2514 && unflushedRvp.Entity.RevoraPoint==60,"production reload retains unsaved gains and the vote");
        var external=VotePlayer(town,"ExternalRvp",10,3,1000);external=ReloadBall(external);ArmVote(external);
        external.Entity.AddRevoraPoints(7);Query("UPDATE entities SET revorapoint=revorapoint+1 WHERE uid=@uid","uid",(long)external.Entity.UID);VoteRow(external,"voted",2500,50,50000);
        VoteGrantDelivery.ApplyFor(external);
        Check(Fenced(external) && VoteRows(external,"voted")==1 && Scalar("SELECT revorapoint FROM entities WHERE uid=@uid",external.Entity.UID)==11,"an external Revora Point write fences the vote and keeps the saved gain plus that write");
        var nullPurse=VotePlayer(town,"NullPurse",10,3,1000);
        Query("UPDATE entities SET conquerpoints=NULL,revorapoint=NULL WHERE uid=@uid","uid",(long)nullPurse.Entity.UID);
        nullPurse=ReloadBall(nullPurse);ArmVote(nullPurse);VoteRow(nullPurse,"voted",2500,50,50000);heard="";
        VoteGrantDelivery.ApplyFor(nullPurse);
        Check(!Fenced(nullPurse) && nullPurse.Entity.ConquerPoints==2500 && nullPurse.Entity.RevoraPoint==50 && nullPurse.Entity.Money==51000 && VoteRows(nullPurse,"delivered")==1 && heard.IndexOf("You received 2500 CPs, 50 Revora Points, and 50000 gold.")>=0,"a null saved purse pays the stored vote once");
        Check(EntityTable.SaveEntity(nullPurse),"a vote paid from a null purse remains saveable");nullPurse=ReloadBall(nullPurse);ArmVote(nullPurse);
        Check(!Fenced(nullPurse) && nullPurse.Entity.ConquerPoints==2500 && nullPurse.Entity.RevoraPoint==50,"reload keeps the award paid from a null purse");
        var loading=VotePlayer(town,"LoginResume",0,0,0);VoteRow(loading,"voted",2500,50,50000);loading.Entity.FullyLoaded=false;
        VoteGrantDelivery.ApplyFor(loading);Check(VoteRows(loading,"voted")==1,"login-phase delivery defers");
        loading.Entity.FullyLoaded=true;VoteGrantDelivery.Pulse();Check(VoteRows(loading,"delivered")==1 && loading.Entity.ConquerPoints==2500,"ordinary admission resumes deferred login delivery");
        var changed=VotePlayer(town,"Changed",0,0,0);VoteRow(changed,"voted",2500,50,50000);
        Query("UPDATE xtremetop_votes SET cp_amount=100,rvp_amount=2,gold_amount=7 WHERE entity_uid=@uid","uid",(long)changed.Entity.UID);
        VoteGrantDelivery.ApplyFor(changed);
        Check(!Fenced(changed) && changed.Entity.ConquerPoints==100 && changed.Entity.RevoraPoint==2 && changed.Entity.Money==7,
            "the locked vote row supplies the amount, not an earlier listing");
        foreach(string status in new[]{"pending","expired","delivered"}){
            var p=VotePlayer(town,"State"+status,4,4,4);VoteRow(p,status,2500,50,50000);VoteGrantDelivery.ApplyFor(p);
            Check(!Fenced(p) && p.Entity.ConquerPoints==4 && VoteRows(p,"voted")==0,"status "+status+" is not a payable vote");
        }
        var otherName=VotePlayer(town,"Owner",1,1,1);VoteRow(otherName,"voted",2500,50,50000);
        Query("UPDATE xtremetop_votes SET username='someoneelse' WHERE entity_uid=@uid","uid",(long)otherName.Entity.UID);
        VoteGrantDelivery.ApplyFor(otherName);
        Check(!Fenced(otherName) && otherName.Entity.ConquerPoints==1 && VoteRows(otherName,"voted")==1,"a different account username cannot collect the vote");
        var otherUid=VotePlayer(town,"Mapped",1,1,1);VoteRow(otherUid,"voted",2500,50,50000);
        Query("UPDATE accounts SET entityid=entityid+1 WHERE username=@name","name",otherUid.Account.Username);
        VoteGrantDelivery.ApplyFor(otherUid);
        Check(!Fenced(otherUid) && VoteRows(otherUid,"voted")==1 && otherUid.Entity.ConquerPoints==1,"accounts.entityid must name the same character");
        foreach(string guard in new[]{"fake","loading","fenced","session","idle"}){
            var p=VotePlayer(town,"Guard"+guard,2,2,2);VoteRow(p,"voted",2500,50,50000);
            if(guard=="fake")p.Fake=true;if(guard=="loading")p.Entity.FullyLoaded=false;
            if(guard=="fenced")typeof(Entity).GetField("PersistenceRequiresReload",Instance).SetValue(p.Entity,true);
            if(guard=="session")Kernel.GamePool[p.Entity.UID]=VotePlayer(town,"Other"+guard,0,0,0);
            if(guard=="idle")p.Action=1;
            VoteGrantDelivery.ApplyFor(p);VoteGrantDelivery.Pulse();
            Check(VoteRows(p,"voted")==1 && p.Entity.ConquerPoints==2,"ineligible session keeps the voted row: "+guard);
        }
        var offline=VotePlayer(town,"Offline",0,0,0);VoteRow(offline,"voted",2500,50,50000);GameState removed;Kernel.GamePool.TryRemove(offline.Entity.UID,out removed);
        var online=VotePlayer(town,"Online",0,0,0);Query("UPDATE xtremetop_votes SET voted_at=now()-interval '1 hour' WHERE entity_uid=@uid","uid",(long)offline.Entity.UID);VoteRow(online,"voted",2500,50,50000);
        VoteGrantDelivery.Pulse();
        Check(VoteRows(offline,"voted")==1 && VoteRows(online,"delivered")==1 && online.Entity.ConquerPoints==2500,"an older offline vote does not block an online player");
        heard="";var fullCp=VotePlayer(town,"FullCp",uint.MaxValue-2499u,0,0);VoteRow(fullCp,"voted",2500,50,50000);VoteGrantDelivery.ApplyFor(fullCp);
        Check(!Fenced(fullCp) && fullCp.Entity.ConquerPoints==uint.MaxValue-2499u && fullCp.Entity.RevoraPoint==0 && VoteRows(fullCp,"voted")==1 && heard=="",
            "one purse overflowing preserves the entire undelivered vote");
        foreach(string purse in new[]{"rvp","gold"}){
            var p=VotePlayer(town,"Overflow"+purse,0,purse=="rvp"?uint.MaxValue-49u:0,purse=="gold"?Entity.MaximumSilver-49999ul:0);
            VoteRow(p,"voted",2500,50,50000);heard="";VoteGrantDelivery.ApplyFor(p);
            Check(!Fenced(p) && p.Entity.ConquerPoints==0 && VoteRows(p,"voted")==1 && heard=="",purse+" overflow preserves the complete entitlement");
        }
        var staleSilver=VotePlayer(town,"StaleSilver",0,0,10);staleSilver=ReloadBall(staleSilver);ArmVote(staleSilver);
        VoteRow(staleSilver,"voted",2500,50,50000);Query("UPDATE entities SET money=11 WHERE uid=@uid","uid",(long)staleSilver.Entity.UID);
        VoteGrantDelivery.ApplyFor(staleSilver);
        Check(Fenced(staleSilver) && VoteRows(staleSilver,"voted")==1 && Scalar("SELECT money FROM entities WHERE uid=@uid",staleSilver.Entity.UID)==11,
            "an independently changed saved silver preimage still fences without payment");
        var edge=VotePlayer(town,"EdgeCp",uint.MaxValue-2500u,uint.MaxValue-50u,Entity.MaximumSilver-50000ul);VoteRow(edge,"voted",2500,50,50000);VoteGrantDelivery.ApplyFor(edge);
        Check(!Fenced(edge) && edge.Entity.ConquerPoints==uint.MaxValue && edge.Entity.RevoraPoint==uint.MaxValue && edge.Entity.Money==Entity.MaximumSilver,
            "exact CP, Revora Point and silver limits accept the catalog vote");
        var stale=VotePlayer(town,"Stale",20,5,9);VoteRow(stale,"voted",2500,50,50000);
        Query("UPDATE entities SET conquerpoints=21,revorapoint=6,money=10 WHERE uid=@uid","uid",(long)stale.Entity.UID);
        VoteGrantDelivery.ApplyFor(stale);
        Check(Fenced(stale) && VoteRows(stale,"voted")==1 && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",stale.Entity.UID)==21,"stale durable purses fence without delivering");
        stale=ReloadBall(stale);ArmVote(stale);VoteGrantDelivery.ApplyFor(stale);
        Check(!Fenced(stale) && stale.Entity.ConquerPoints==2521 && stale.Entity.RevoraPoint==56 && stale.Entity.Money==50010,"reload retries the vote from the durable purses");
        foreach(string fault in new[]{"purse","claim"}){
            var p=VotePlayer(town,"Fault"+fault,8,8,8);VoteRow(p,"voted",2500,50,50000);
            string table=fault=="purse"?"entities":"xtremetop_votes";
            Query("CREATE FUNCTION suppress_vote() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_vote BEFORE UPDATE ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_vote()");
            try{VoteGrantDelivery.ApplyFor(p);Check(Fenced(p) && VoteRows(p,"voted")==1 && p.Entity.ConquerPoints==8 && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==8,"silent "+fault+" rolls the vote back");}
            finally{Query("DROP TRIGGER suppress_vote ON "+table+";DROP FUNCTION suppress_vote()");}
            p=ReloadBall(p);ArmVote(p);VoteGrantDelivery.ApplyFor(p);
            Check(!Fenced(p) && VoteRows(p,"delivered")==1 && p.Entity.ConquerPoints==2508,"reload retries after "+fault+" rollback");
        }
        var race=VotePlayer(town,"Race",0,0,0);VoteRow(race,"voted",2500,50,50000);
        Parallel.For(0,12,n=>{if(n%2==0)VoteGrantDelivery.Pulse();else VoteGrantDelivery.ApplyFor(race);});
        Check(!Fenced(race) && race.Entity.ConquerPoints==2500 && VoteRows(race,"delivered")==1,"parallel Pulse and ApplyFor deliver one vote");
        var install=VotePlayer(town,"Install",6,1,2);VoteRow(install,"voted",2500,50,50000);
        VoteGrants.GetField("BeforeInstall",Hidden).SetValue(null,(Action<GameState>)(g=>{throw new InvalidOperationException("installation");}));
        try{heard="";VoteGrantDelivery.ApplyFor(install);EntityTable.SaveEntity(install);
            Check(Fenced(install) && install.Entity.ConquerPoints==6 && VoteRows(install,"delivered")==1 && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",install.Entity.UID)==2506 && heard.IndexOf("Thanks for voting")<0,
                "post-commit projection failure fences, keeps one delivered vote, and does not announce it");}
        finally{VoteGrants.GetField("BeforeInstall",Hidden).SetValue(null,null);}
        install=ReloadBall(install);ArmVote(install);
        Check(!Fenced(install) && install.Entity.ConquerPoints==2506 && install.Entity.RevoraPoint==51 && install.Entity.Money==50002,"reload recovers the committed vote purses");
        EntityTable.SaveEntity(install);install=ReloadBall(install);ArmVote(install);VoteGrantDelivery.ApplyFor(install);
        Check(!Fenced(install) && install.Entity.ConquerPoints==2506 && VoteRows(install,"delivered")==1,"a later save cannot duplicate or erase the vote award");
        var racePurse=VotePlayer(town,"RacePurse",10,3,1000);racePurse=ReloadBall(racePurse);ArmVote(racePurse);VoteRow(racePurse,"voted",2500,50,50000);
        int added=0;Parallel.For(0,8,n=>{if(n%2==0){racePurse.Entity.AddRevoraPoints(1);System.Threading.Interlocked.Increment(ref added);}else if(n==1)VoteGrantDelivery.ApplyFor(racePurse);else EntityTable.SaveEntity(racePurse);});
        Check(!Fenced(racePurse) && VoteRows(racePurse,"delivered")==1,"concurrent reward, vote and save deliver the vote once");
        uint expectRvp=(uint)(3+added+50);Check(racePurse.Entity.RevoraPoint==expectRvp,"concurrent RVP credits survive beside the vote: "+added);
        Check(EntityTable.SaveEntity(racePurse),"concurrent purse remains saveable");racePurse=ReloadBall(racePurse);ArmVote(racePurse);
        Check(!Fenced(racePurse) && racePurse.Entity.RevoraPoint==expectRvp && racePurse.Entity.ConquerPoints==2510,"reload keeps the concurrent purse and one vote");
        var mixed=ReloadBall(VotePlayer(town,"MixedPurse",1000,1000,1000));ArmVote(mixed);VoteRow(mixed,"voted",2500,50,50000);
        int spentCp=0,spentRvp=0;
        Parallel.For(0,48,n=>{
            switch(n%6){
                case 0:mixed.Entity.AddConquerPoints(4);break;
                case 1:mixed.Entity.AddRevoraPoints(7);break;
                case 2:if(mixed.Entity.TrySpendConquerPoints(1))System.Threading.Interlocked.Increment(ref spentCp);break;
                case 3:if(mixed.Entity.TrySpendRevoraPoints(2))System.Threading.Interlocked.Increment(ref spentRvp);break;
                case 4:VoteGrantDelivery.ApplyFor(mixed);break;
                case 5:EntityTable.SaveEntity(mixed);break;
            }
        });
        Check(!Fenced(mixed) && spentCp==8 && spentRvp==8 && mixed.Entity.ConquerPoints==3524 && mixed.Entity.RevoraPoint==1090
            && mixed.Entity.Money==51000 && VoteRows(mixed,"delivered")==1 && EntityTable.SaveEntity(mixed),
            "simultaneous CP and RVP gains, spends, vote and save preserve exact purses");
        mixed=ReloadBall(mixed);ArmVote(mixed);VoteGrantDelivery.ApplyFor(mixed);
        Check(!Fenced(mixed) && mixed.Entity.ConquerPoints==3524 && mixed.Entity.RevoraPoint==1090 && mixed.Entity.Money==51000
            && VoteRows(mixed,"delivered")==1,"mixed gain and spending reload retains one vote and every delta");
        VerifyWebConfirmedHandoff(town);
        VerifyPublicCountHandoff(town);
        VoteGrants.GetField("Relation",Hidden).SetValue(null,"identity.xtremetop_votes");
        VoteGrants.GetField("Spoken",Hidden).SetValue(null,null);
    }
    static void VerifyWebConfirmedHandoff(Map town){
        string project=System.Environment.GetEnvironmentVariable("REVORA_WEB_TESTS");
        if(string.IsNullOrEmpty(project) || !System.IO.File.Exists(project))
            throw new InvalidOperationException("Set REVORA_WEB_TESTS to the validated Revora.WebTests.csproj before running the vote handoff suite.");
        string name="Web"+Guid.NewGuid().ToString("N").Substring(0,8);
        var player=VotePlayer(town,name,10,3,1000);
        string schema="votehandoff"+Guid.NewGuid().ToString("N").Substring(0,12);
        Query("CREATE SCHEMA "+schema);
        Query("CREATE TABLE "+schema+".xtremetop_votes(vote_id uuid PRIMARY KEY, token text NOT NULL, account_id uuid NOT NULL, username text NOT NULL, entity_uid bigint NOT NULL, entity_name text NOT NULL, status text NOT NULL, voting_ip inet, cp_amount integer NOT NULL, rvp_amount integer NOT NULL, gold_amount bigint NOT NULL, created_at timestamptz NOT NULL DEFAULT clock_timestamp(), voted_at timestamptz, delivered_at timestamptz, confirmation_source text)");
        try{
            var start=new System.Diagnostics.ProcessStartInfo();
            start.FileName="dotnet";
            start.Arguments="run --project \""+project+"\" -c Release --no-launch-profile --no-build --no-restore -- --confirm-handoff "+schema+" "+player.Entity.UID.ToString()+" "+player.Account.Username;
            start.UseShellExecute=false;
            start.RedirectStandardOutput=true;
            start.RedirectStandardError=true;
            start.CreateNoWindow=true;
            using(var process=System.Diagnostics.Process.Start(start)){
                var stdout=process.StandardOutput.ReadToEndAsync();
                var stderr=process.StandardError.ReadToEndAsync();
                if(!process.WaitForExit(120000)){
                    process.Kill();
                    throw new InvalidOperationException("The disposable website handoff exceeded its two-minute deadline.");
                }
                Check(process.ExitCode==0,"the website confirm handoff failed");
            }
            VoteGrants.GetField("Relation",Hidden).SetValue(null,schema+".xtremetop_votes");
            player=ReloadBall(player);ArmVote(player);
            uint cp=player.Entity.ConquerPoints;uint rvp=player.Entity.RevoraPoint;ulong gold=player.Entity.Money;
            VoteGrantDelivery.ApplyFor(player);
            Check(!Fenced(player) && player.Entity.ConquerPoints==cp+2500 && player.Entity.RevoraPoint==rvp+50 && player.Entity.Money==gold+50000
                && Scalar("SELECT count(*) FROM "+schema+".xtremetop_votes WHERE entity_uid=@uid AND status='delivered'",player.Entity.UID)==1
                && Scalar("SELECT count(*) FROM "+schema+".xtremetop_votes WHERE entity_uid=@uid AND cp_amount=2500 AND rvp_amount=50 AND gold_amount=50000",player.Entity.UID)==1,
                "a website-confirmed vote did not pay the ordinary non-GM 2500/50/50000 once");
            player=ReloadBall(player);ArmVote(player);
            VoteGrantDelivery.ApplyFor(player);VoteGrantDelivery.Pulse();
            Check(!Fenced(player) && player.Entity.ConquerPoints==cp+2500 && player.Entity.RevoraPoint==rvp+50 && player.Entity.Money==gold+50000
                && Scalar("SELECT count(*) FROM "+schema+".xtremetop_votes WHERE entity_uid=@uid AND status='delivered'",player.Entity.UID)==1,
                "reload repeated a website-confirmed vote");
        }finally{
            VoteGrants.GetField("Relation",Hidden).SetValue(null,"xtremetop_votes");
            Query("DROP SCHEMA IF EXISTS "+schema+" CASCADE");
        }
    }
    static void VerifyPublicCountHandoff(Map town){
        string project=System.Environment.GetEnvironmentVariable("REVORA_WEB_TESTS");
        if(string.IsNullOrEmpty(project) || !System.IO.File.Exists(project))
            throw new InvalidOperationException("Set REVORA_WEB_TESTS to the validated Revora.WebTests.csproj before running the vote handoff suite.");
        string name="Count"+Guid.NewGuid().ToString("N").Substring(0,8);
        var player=VotePlayer(town,name,10,3,1000);
        string schema="votehandoff"+Guid.NewGuid().ToString("N").Substring(0,12);
        Query("CREATE SCHEMA "+schema);
        Query("CREATE TABLE "+schema+".xtremetop_votes(vote_id uuid PRIMARY KEY, token text NOT NULL, account_id uuid NOT NULL, username text NOT NULL, entity_uid bigint NOT NULL, entity_name text NOT NULL, status text NOT NULL, voting_ip inet, cp_amount integer NOT NULL, rvp_amount integer NOT NULL, gold_amount bigint NOT NULL, created_at timestamptz NOT NULL DEFAULT clock_timestamp(), voted_at timestamptz, delivered_at timestamptz, confirmation_source text)");
        try{
            var start=new System.Diagnostics.ProcessStartInfo();
            start.FileName="dotnet";
            start.Arguments="run --project \""+project+"\" -c Release --no-launch-profile --no-build --no-restore -- --confirm-count-handoff "+schema+" "+player.Entity.UID.ToString()+" "+player.Account.Username;
            start.UseShellExecute=false;
            start.RedirectStandardOutput=true;
            start.RedirectStandardError=true;
            start.CreateNoWindow=true;
            using(var process=System.Diagnostics.Process.Start(start)){
                var stdout=process.StandardOutput.ReadToEndAsync();
                var stderr=process.StandardError.ReadToEndAsync();
                if(!process.WaitForExit(120000)){
                    process.Kill();
                    throw new InvalidOperationException("The disposable public-count handoff exceeded its two-minute deadline.");
                }
                Check(process.ExitCode==0,"the website public-count handoff failed");
            }
            VoteGrants.GetField("Relation",Hidden).SetValue(null,schema+".xtremetop_votes");
            player=ReloadBall(player);ArmVote(player);
            uint cp=player.Entity.ConquerPoints;uint rvp=player.Entity.RevoraPoint;ulong gold=player.Entity.Money;
            VoteGrantDelivery.ApplyFor(player);
            Check(!Fenced(player) && player.Entity.ConquerPoints==cp+2500 && player.Entity.RevoraPoint==rvp+50 && player.Entity.Money==gold+50000
                && Scalar("SELECT count(*) FROM "+schema+".xtremetop_votes WHERE entity_uid=@uid AND status='delivered' AND confirmation_source='public-count' AND voting_ip IS NULL AND cp_amount=2500 AND rvp_amount=50 AND gold_amount=50000",player.Entity.UID)==1,
                "a public-count confirmed vote did not pay the ordinary non-GM 2500/50/50000 once");
            player=ReloadBall(player);ArmVote(player);
            VoteGrantDelivery.ApplyFor(player);VoteGrantDelivery.Pulse();
            Check(!Fenced(player) && player.Entity.ConquerPoints==cp+2500 && player.Entity.RevoraPoint==rvp+50 && player.Entity.Money==gold+50000
                && Scalar("SELECT count(*) FROM "+schema+".xtremetop_votes WHERE entity_uid=@uid AND status='delivered'",player.Entity.UID)==1,
                "reload repeated a public-count confirmed vote");
        }finally{
            VoteGrants.GetField("Relation",Hidden).SetValue(null,"xtremetop_votes");
            Query("DROP SCHEMA IF EXISTS "+schema+" CASCADE");
        }
    }
}
