using System;
using Console = System.Console;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Npgsql;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.ConquerStructures.Society;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type CtfStore=typeof(Entity).Assembly.GetType("Revora.Game.Features.CtfResults");
    static DateTime ctfNow;
    static CaptureTheFlag NewCtf(Map map) {
        return (CaptureTheFlag)Activator.CreateInstance(typeof(CaptureTheFlag),BindingFlags.Instance|BindingFlags.NonPublic,
            null,new object[]{map,(Func<DateTime>)(()=>ctfNow)},null);
    }
    static GameState CtfPlayer(Map town,uint guildId) {
        var p=ProgressPlayer(town,"");
        Guild guild;
        if(!Kernel.Guilds.TryGetValue(guildId,out guild)) {
            guild=new Guild(p.Entity.Name){ID=guildId,Name="Guild"+guildId};Kernel.Guilds[guildId]=guild;
            guild.Leader=new Guild.Member(guildId){ID=p.Entity.UID};
        }
        p.Guild=guild;p.Entity.CharBuffer._GuildID=guildId;
        guild.Members[p.Entity.UID]=new Guild.Member(guildId){ID=p.Entity.UID};
        p.ActiveNpc=380;p.Entity.X=town.Npcs[380].X;p.Entity.Y=town.Npcs[380].Y;
        return p;
    }
    static void CtfPosition(GameState p,ushort x,ushort y) { p.Entity.X=x;p.Entity.Y=y;p.Attackable=true; }
    static void CaptureBase(CaptureTheFlag ctf,GameState p,SobNpcSpawn npc) {
        CtfPosition(p,(ushort)(npc.X+1),npc.Y);
        Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,npc,new Attack(true){AttackType=Attack.Melee},npc.MaxHitpoints,null);
    }
    static void Plant(CaptureTheFlag ctf,Map map,GameState p,SobNpcSpawn npc) {
        var flag=map.StaticEntities.Values.First(f=>f.NpsEffect.Mesh==StaticEntity.FlagMesh);
        CtfPosition(p,flag.X,flag.Y);Call(typeof(CaptureTheFlag),ctf,"Move",p);
        CtfPosition(p,(ushort)(npc.X+1),npc.Y);Call(typeof(CaptureTheFlag),ctf,"Move",p);
    }
    static long CtfCount(string sql) { return Scalar(sql,0); }
    static void VerifyCtf(string runtime,Map town) {
        PreparePkReloadSchema();
        Query("CREATE TABLE IF NOT EXISTS status AS TABLE public.status WITH NO DATA");
        Query("CREATE TABLE titles (LIKE public.titles INCLUDING ALL)");
        StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
        ConquerItem.ItemUID=new Counter(3900000);
        var map=World(runtime,2057);
        using(var c=DataHolder.MySqlConnection) { c.Open();
            using(var q=new NpgsqlCommand("SELECT id,cellx,celly,lookface,type,life,maxlife FROM public.sobnpcs WHERE mapid=2057 ORDER BY id",c))
            using(var r=q.ExecuteReader())while(r.Read()) {
                var npc=new SobNpcSpawn{UID=Convert.ToUInt32(r[0]),MapID=2057,X=Convert.ToUInt16(r[1]),Y=Convert.ToUInt16(r[2]),
                    Mesh=Convert.ToUInt16(r[3]),Type=(Enums.NpcType)Convert.ToByte(r[4]),Hitpoints=Convert.ToUInt32(r[5]),MaxHitpoints=Convert.ToUInt32(r[6]),Name="Flag Base",ShowName=true};
                map.AddNpc(npc);Console.WriteLine("CTF installed base: "+npc.UID+" at "+npc.X+","+npc.Y+" HP "+npc.MaxHitpoints);
            }
            using(var q=new NpgsqlCommand("SELECT cellx,celly,lookface,type FROM public.npcs WHERE id=380 AND mapid=1002",c))
            using(var r=q.ExecuteReader()) {
                Check(r.Read(),"Guild Controller 380 exists in the installed Twin City database");
                town.AddNpc(new NpcSpawn{UID=380,MapID=1002,X=Convert.ToUInt16(r[0]),Y=Convert.ToUInt16(r[1]),Mesh=Convert.ToUInt16(r[2]),Type=(Enums.NpcType)Convert.ToByte(r[3])});
            }
        }
        var bases=map.Npcs.Values.OfType<SobNpcSpawn>().OrderBy(n=>n.UID).ToArray();
        Check(bases.Length==3 && bases.Select(n=>n.UID).SequenceEqual(new uint[]{20571,20572,20573}) && bases.All(n=>n.MaxHitpoints>0),"all three actual CTF bases match the owned arena identities");
        object[] winter={new DateTime(2027,1,3,5,0,0,DateTimeKind.Utc),null,DateTime.MinValue};
        Check((bool)typeof(CaptureTheFlag).GetMethod("Window",Hidden).Invoke(null,winter)
            && (DateTime)winter[2]==new DateTime(2027,1,3,5,30,0,DateTimeKind.Utc),"Pacific winter schedule keeps its local hour with UTC persistence");
        ctfNow=new DateTime(2026,9,20,3,59,59,DateTimeKind.Utc);
        var ctf=NewCtf(map);
        Server.Thread=(Revora.Thread)FormatterServices.GetUninitializedObject(typeof(Revora.Thread));Server.Thread.CTF=ctf;
        var a=CtfPlayer(town,55);var b=CtfPlayer(town,56);var spectator=CtfPlayer(town,55);
        Check(!a.Fake && a.Account.State==AccountTable.AccountState.Player,"CTF participants are ordinary non-GM players");
        Check(!ctf.SignUp(a),"registration closed outside the Saturday Pacific window");
        ctfNow=ctfNow.AddSeconds(2);ctf.Tick();
        Check(CaptureTheFlag.IsWar && map.StaticEntities.Count==40,"scheduler opens once and creates forty flags on actual 2057 terrain");
        ctf.Tick();Check(CtfCount("SELECT count(*) FROM revora_ctf_runs")==1,"repeated scheduler ticks cannot duplicate a weekly run");
        a.Entity.X=20;a.Entity.Y=20;Check(!ctf.SignUp(a),"remote forged Guild Controller signup is rejected");
        CtfPosition(a,town.Npcs[380].X,town.Npcs[380].Y);a.Fake=true;
        Check(!ctf.SignUp(a),"adventurer cannot enter a player guild tournament");a.Fake=false;
        Check(ctf.SignUp(a),"real nearby guild member can enter through signup");
        Npcs.GetDialog(new NpcRequest(true){NpcID=380,OptionID=11},b);
        Npcs.GetDialog(new NpcRequest(true){NpcID=380,OptionID=11},spectator);
        Check(b.Entity.MapID==2057 && spectator.Entity.MapID==2057,"normal NPC dialogue dispatcher registers and teleports ordinary guild members");
        Kernel.GamePool[a.Entity.UID]=spectator;
        Check(!(bool)Call(typeof(CaptureTheFlag),ctf,"CanAttack",a,bases[0]),"stale replaced session cannot mutate a tournament");
        Kernel.GamePool[a.Entity.UID]=a;
        Check(a.Entity.PKMode==Enums.PKMode.Team,"entry selects guild combat mode");
        Check(a.Entity.MapID==2057 && !CaptureTheFlag.Attackable(a.Entity),"spawn camp protects registered players from combat");
        CaptureBase(ctf,a,bases[0]);CaptureBase(ctf,b,bases[1]);
        CtfPosition(b,a.Entity.X,a.Entity.Y);
        Check(Revora.Game.Attacking.Handle.CanAttack(a.Entity,b.Entity,null,true),"normal PvP attack gate permits rival guild combat outside camp");
        CtfPosition(spectator,a.Entity.X,a.Entity.Y);a.Entity.PKMode=Enums.PKMode.PK;
        Check(!Revora.Game.Attacking.Handle.CanAttack(a.Entity,spectator.Entity,null,true),"changing PK mode cannot bypass same-guild protection");
        a.Entity.PKMode=Enums.PKMode.Team;
        Check(bases[0].Name==a.Guild.Name && bases[1].Name==b.Guild.Name && bases.All(n=>n.Hitpoints==n.MaxHitpoints),"actual SobNpc attack path captures and restores base HP");
        Check(!(bool)Call(typeof(CaptureTheFlag),ctf,"CanAttack",a,bases[0]),"guild cannot attack its own base");
        Plant(ctf,map,a,bases[0]);EvidenceRefresh(a);
        Check(CtfCount("SELECT count(*) FROM revora_ctf_deliveries")==1 && HasAchievement(a,10230),"a real flag delivery commits score and planting achievement together");
        Parallel.For(0,8,i=>Call(typeof(CaptureTheFlag),ctf,"Move",a));
        Check(CtfCount("SELECT count(*) FROM revora_ctf_deliveries")==1,"repeated movement at the base cannot duplicate a delivery");
        var flag=map.StaticEntities.Values.First();CtfPosition(a,flag.X,flag.Y);CtfPosition(b,flag.X,flag.Y);
        Parallel.Invoke(()=>Call(typeof(CaptureTheFlag),ctf,"Move",a),()=>Call(typeof(CaptureTheFlag),ctf,"Move",b));
        var carriers=(IDictionary)typeof(CaptureTheFlag).GetField("carriers",Instance).GetValue(ctf);
        Check(carriers.Count==1,"simultaneous pickup gives one flag to exactly one player");
        var carrier=carriers.Contains(a.Entity.UID)?a:b;
        ctfNow=ctfNow.AddSeconds(60);Call(typeof(CaptureTheFlag),ctf,"Move",carrier);
        Check(carriers.Count==0 && CtfCount("SELECT count(*) FROM revora_ctf_deliveries")==1,"expired flag cannot score at the exact deadline");
        flag=map.StaticEntities.Values.First();CtfPosition(a,flag.X,flag.Y);Call(typeof(CaptureTheFlag),ctf,"Move",a);
        GameState removed;Kernel.GamePool.TryRemove(a.Entity.UID,out removed);ctfNow=ctfNow.AddSeconds(2);ctf.Tick();
        Check(carriers.Count==0,"disconnect discards possession without losing earned deliveries");Kernel.GamePool[a.Entity.UID]=a;
        flag=map.StaticEntities.Values.First();CtfPosition(a,flag.X,flag.Y);Call(typeof(CaptureTheFlag),ctf,"Move",a);
        CtfPosition(b,a.Entity.X,a.Entity.Y);Call(typeof(CaptureTheFlag),ctf,"Killed",a,b);
        Check(carriers.Count==1 && carriers.Contains(b.Entity.UID),"enemy kill transfers one existing flag without creating a second ground flag");
        Call(typeof(CaptureTheFlag),ctf,"Leave",b);
        // A failure in achievement insertion rolls the objective receipt back too.
        flag=map.StaticEntities.Values.First();CtfPosition(b,flag.X,flag.Y);Call(typeof(CaptureTheFlag),ctf,"Move",b);
        CtfPosition(b,(ushort)(bases[1].X+1),bases[1].Y);
        Query("ALTER TABLE revora_achievement_evidence ADD CONSTRAINT ctf_failure CHECK(achievement_id<>10230) NOT VALID");
        bool failed=false;try{Call(typeof(CaptureTheFlag),ctf,"Move",b);}catch(PostgresException){failed=true;}
        Check(failed && carriers.Contains(b.Entity.UID) && CtfCount("SELECT count(*) FROM revora_ctf_deliveries")==1,"database failure retains flag for retry and rolls back score");
        Query("ALTER TABLE revora_achievement_evidence DROP CONSTRAINT ctf_failure");
        Call(typeof(CaptureTheFlag),ctf,"Move",b);Plant(ctf,map,a,bases[0]);
        Check(CtfCount("SELECT count(*)*10 FROM revora_ctf_deliveries WHERE guild_id=55")==20,"score is exactly ten per successful delivery, independent of level");
        a.Entity.Teleport(482,367);a.ActiveNpc=900000060;
        Npcs.GetDialog(new NpcRequest(true){NpcID=900000060,OptionID=1},a);
        Check(a.Entity.MapID==1002,"Flag Marshal dialogue returns a participant to Twin City");
        a.ActiveNpc=380;a.Entity.Teleport(1002,town.Npcs[380].X,town.Npcs[380].Y);a.Guild=b.Guild;a.Entity.CharBuffer._GuildID=56;b.Guild.Members[a.Entity.UID]=new Guild.Member(56){ID=a.Entity.UID};
        Check(!ctf.SignUp(a),"changing guild cannot switch teams within the same round");
        a.Guild=Kernel.Guilds[55];a.Entity.CharBuffer._GuildID=55;
        Check(ctf.SignUp(a),"reconnecting original guild membership retains eligibility");
        ctfNow=new DateTime(2026,9,20,4,30,0,DateTimeKind.Utc);ctf.Tick();
        Check(!CaptureTheFlag.IsWar && CtfCount("SELECT winner FROM revora_ctf_runs")==55,"one persisted winner is finalized at the fixed end time");
        Check(a.Entity.PKMode==Enums.PKMode.PK && b.Entity.PKMode==Enums.PKMode.PK,"leaving restores each player original combat mode");
        Check(a.Entity.MapID==1002 && b.Entity.MapID==1002 && map.StaticEntities.Count==0,"finalization clears flags and returns participants home");
        EvidenceRefresh(a);EvidenceRefresh(b);EvidenceRefresh(spectator);
        Check(HasAchievement(a,10301) && !HasAchievement(b,10301) && !HasAchievement(spectator,10301),"winning contributors earn victory; losing members and idle spectators do not");
        Check(EligibleTitles(a).Contains(20020003) && !EligibleTitles(a).Contains(20030004) && !EligibleTitles(a).Contains(60020021),"local victory unlocks Victor without cross-server awards");
        int count=a.Entity.MyAchievement.CompletedCount;
        Parallel.For(0,8,i=>Call(CtfStore,null,"Finish","ctf-20260919",ctfNow));
        EvidenceRefresh(a);Check(a.Entity.MyAchievement.CompletedCount==count,"duplicate concurrent finalization cannot duplicate achievements");
        a.Entity.WTitles=null;StorageManager.Load();new TitleStorage().Login(a);
        Check(OwnedTitles(a).ContainsKey(20020003),"ordinary winner reloads the persisted Victor wardrobe entry");
        a.Guild.Leader=new Guild.Member(55){ID=spectator.Entity.UID};RefreshTitles(a);RefreshTitles(spectator);
        Check(!OwnedTitles(a).ContainsKey(20020003) && OwnedTitles(spectator).ContainsKey(20020003),"Victor follows current legitimate guild leadership");
        VerifyCtfSuppressedResults(a,b);
        VerifyCtfResults(a,b);
        ctfNow=new DateTime(2026,9,27,4,0,0,DateTimeKind.Utc);ctf.Tick();
        Check(CaptureTheFlag.IsWar,"next weekly window starts independently");
        var restarted=NewCtf(map);Server.Thread.CTF=restarted;restarted.Tick();
        Check(!CaptureTheFlag.IsWar && CtfCount("SELECT count(*) FROM revora_ctf_runs WHERE outcome='server-restart'")==1,"restart cancels unfinished run and cannot replay the same weekly slot");
        VerifyCtfPrizes(a,b,spectator,town);
    }
    static void CtfPrizeNpc(GameState p,Map town) {
        p.Entity.MapID=1002;p.ActiveNpc=380;
        CtfPosition(p,town.Npcs[380].X,town.Npcs[380].Y);
        Npcs.GetDialog(new NpcRequest(true){NpcID=380,OptionID=12},p);
    }
    static void VerifyCtfPrizes(GameState winner,GameState loser,GameState idle,Map town) {
        Check(Scalar("SELECT count(*) FROM revora_ctf_prizes WHERE run='ctf-20260919' AND uid=@uid AND claimed_at IS NULL",winner.Entity.UID)==1
            && Scalar("SELECT count(*) FROM revora_ctf_prizes WHERE run='ctf-20260919' AND uid=@uid",loser.Entity.UID)==0
            && Scalar("SELECT count(*) FROM revora_ctf_prizes WHERE uid=@uid",idle.Entity.UID)==0,
            "only winning contributors receive a saved CTF item entitlement");
        Check(CtfCount("SELECT count(*) FROM revora_ctf_prizes WHERE run IN ('tie','uncontested')")==0,
            "tied and uncontested CTF rounds create no item prizes");
        uint expected=(uint)Scalar("SELECT item_type FROM revora_ctf_prizes WHERE run='ctf-20260919' AND uid=@uid",winner.Entity.UID);
        Check(ConquerItemInformation.BaseInformations[expected].PurificationLevel==7,"CTF freezes a catalog-backed P7 soul at finalization");
        // Old admitted contracts do not acquire a new prize when finalized after migration.
        var end=ctfNow.AddMinutes(1);const string old="old-ctf-prize";
        Call(CtfStore,null,"Start",old,end);
        Query("UPDATE revora_ctf_runs SET valuable_reward=false WHERE run=@run","run",old);
        Call(CtfStore,null,"Register",old,winner.Entity.UID,55u,ctfNow);
        Call(CtfStore,null,"Register",old,loser.Entity.UID,56u,ctfNow);
        Call(CtfStore,null,"Contribute",old,loser.Entity.UID,56u,ctfNow);
        Call(CtfStore,null,"Deliver",old,Guid.NewGuid(),winner.Entity.UID,55u,ctfNow);
        Call(CtfStore,null,"Finish",old,end);
        Check(CtfCount("SELECT count(*) FROM revora_ctf_prizes WHERE run='old-ctf-prize'")==0,"old CTF contracts retain their original reward edition");
        winner.Entity.MapID=1002;winner.ActiveNpc=380;CtfPosition(winner,20,20);
        Call(CtfStore,null,"Claim",winner);
        Check(Scalar("SELECT count(*) FROM revora_ctf_prizes WHERE uid=@uid AND claimed_at IS NOT NULL",winner.Entity.UID)==0,"remote CTF claim cannot deliver a prize");
        winner.Fake=true;CtfPrizeNpc(winner,town);winner.Fake=false;
        Check(winner.Inventory.Count==0,"adventurers cannot claim player CTF prizes");
        CtfPrizeNpc(idle,town);Check(idle.Inventory.Count==0,"idle participant cannot claim another character's prize");
        var fillers=Enumerable.Range(0,40).Select(i=>Pack(winner,1088001,1)).ToArray();
        CtfPrizeNpc(winner,town);
        Check(!Fenced(winner) && winner.Inventory.Count==40
            && Scalar("SELECT count(*) FROM revora_ctf_prizes WHERE uid=@uid AND claimed_at IS NOT NULL",winner.Entity.UID)==0,
            "full CTF prize inventory preserves the unclaimed entitlement");
        foreach(var item in fillers) {Query("DELETE FROM items WHERE uid=@id","id",(long)item.UID);winner.Inventory.Remove(item.UID,Enums.ItemUse.None,false);}
        SavePkProfileBeforeClaim(winner);
        foreach(string table in new[]{"items","revora_ctf_prizes"}) {
            Query("CREATE FUNCTION ctf_claim_suppress() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
            Query("CREATE TRIGGER ctf_claim_suppress BEFORE INSERT OR UPDATE ON "+table+" FOR EACH ROW EXECUTE FUNCTION ctf_claim_suppress()");
            try { CtfPrizeNpc(winner,town); }
            finally { Query("DROP TRIGGER ctf_claim_suppress ON "+table);Query("DROP FUNCTION ctf_claim_suppress()"); }
            Check(Fenced(winner) && Scalar("SELECT count(*) FROM items WHERE entityid=@uid",winner.Entity.UID)==0
                && Scalar("SELECT count(*) FROM revora_ctf_prizes WHERE uid=@uid AND claimed_at IS NOT NULL",winner.Entity.UID)==0,
                "failed CTF item or receipt write rolls back both: "+table);
            winner=ReloadBall(winner);
            Check(winner.Inventory.Count==0 && !Fenced(winner),"CTF pending prize survives fresh-session reload: "+table);
        }
        // Guild departure does not revoke the contribution earned in the saved round.
        winner.Guild=null;winner.Entity.CharBuffer._GuildID=0;
        winner.Entity.MapID=1002;winner.ActiveNpc=380;CtfPosition(winner,town.Npcs[380].X,town.Npcs[380].Y);
        Parallel.For(0,8,i=>Call(CtfStore,null,"Claim",winner));
        var prize=winner.Inventory.Objects.Single();
        Check(prize.ID==expected && prize.Bound && prize.StackSize==1
            && Scalar("SELECT count(*) FROM revora_ctf_prizes WHERE uid=@uid AND claimed_at IS NOT NULL AND item_uid>0",winner.Entity.UID)==1,
            "concurrent ordinary claims award one frozen bound soul after guild departure");
        uint itemUid=prize.UID;winner=ReloadBall(winner);CtfPrizeNpc(winner,town);
        Check(winner.Inventory.Objects.Count()==1 && winner.Inventory.Objects.Single().UID==itemUid
            && winner.Inventory.Objects.Single().Bound,"CTF prize and replay protection survive production item reload");
    }
    static long CtfRunCount(string run) {
        using(var c=DataHolder.MySqlConnection) { c.Open();
            using(var q=new NpgsqlCommand("SELECT count(*) FROM revora_ctf_runs WHERE run=@run AND state='active' AND winner=0",c)) {
                q.Parameters.AddWithValue("run",run); return Convert.ToInt64(q.ExecuteScalar());
            }
        }
    }
    static void VerifyCtfSuppressedResults(GameState a,GameState b) {
        DateTime end=ctfNow.AddMinutes(1);
        foreach(string table in new[]{"revora_title_ctf","revora_ctf_runs","revora_ctf_prizes"}) {
            string run="suppressed-"+table;
            Call(CtfStore,null,"Start",run,end);
            Call(CtfStore,null,"Register",run,a.Entity.UID,55u,ctfNow);
            Call(CtfStore,null,"Contribute",run,a.Entity.UID,55u,ctfNow);
            Call(CtfStore,null,"Register",run,b.Entity.UID,56u,ctfNow);
            Call(CtfStore,null,"Deliver",run,Guid.NewGuid(),b.Entity.UID,56u,ctfNow);
            Query("CREATE FUNCTION ctf_suppress() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
            Query("CREATE TRIGGER ctf_suppress BEFORE INSERT OR UPDATE ON "+table+" FOR EACH ROW EXECUTE FUNCTION ctf_suppress()");
            bool failed=false;
            try { Call(CtfStore,null,"Finish",run,end); } catch(InvalidOperationException) { failed=true; }
            finally {
                Query("DROP TRIGGER ctf_suppress ON "+table);
                Query("DROP FUNCTION ctf_suppress()");
            }
            Check(failed && CtfRunCount(run)==1,
                "suppressed result write leaves the CTF run retryable: "+table);
            Check(CtfCount("SELECT guild_id FROM revora_title_ctf WHERE id=1")==55
                && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE achievement_id=10301 AND uid=@uid",b.Entity.UID)==0,
                "suppressed result write rolls back champion and winner evidence: "+table);
            var result=Call(CtfStore,null,"Finish",run,end);
            Check((uint)result.GetType().GetField("Winner",Instance).GetValue(result)==56u
                && CtfCount("SELECT guild_id FROM revora_title_ctf WHERE id=1")==56,
                "retry persists CTF champion after suppressed write: "+table);
            Call(CtfStore,null,"Finish",run,end);
            Check(Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE achievement_id=10301 AND uid=@uid",b.Entity.UID)==1,
                "CTF retry and replay retain exactly one victory identity: "+table);
            // Restore the earlier champion/evidence baseline for the next fault case.
            Query("UPDATE revora_title_ctf SET guild_id=55 WHERE id=1");
            Query("DELETE FROM revora_achievement_evidence WHERE achievement_id=10301 AND uid=@uid","uid",(long)b.Entity.UID);
        }
    }
    static void VerifyCtfResults(GameState a,GameState b) {
        DateTime end=ctfNow.AddMinutes(1);
        foreach(string name in new[]{"tie","uncontested","rollback"}) {
            Call(CtfStore,null,"Start",name,end);
            Call(CtfStore,null,"Register",name,a.Entity.UID,55u,ctfNow);
            Call(CtfStore,null,"Deliver",name,Guid.NewGuid(),a.Entity.UID,55u,ctfNow);
            if(name!="uncontested") {
                Call(CtfStore,null,"Register",name,b.Entity.UID,56u,ctfNow);
                Call(CtfStore,null,"Contribute",name,b.Entity.UID,56u,ctfNow);
            }
            if(name=="tie")Call(CtfStore,null,"Deliver",name,Guid.NewGuid(),b.Entity.UID,56u,ctfNow);
            if(name=="rollback") {
                Call(CtfStore,null,"Deliver",name,Guid.NewGuid(),b.Entity.UID,56u,ctfNow);
                Call(CtfStore,null,"Deliver",name,Guid.NewGuid(),b.Entity.UID,56u,ctfNow);
                Query("ALTER TABLE revora_ctf_runs ADD CONSTRAINT ctf_finish_failure CHECK(state<>'complete' OR run<>'rollback') NOT VALID");
                bool failed=false;try{Call(CtfStore,null,"Finish",name,end);}catch(PostgresException){failed=true;}
                Check(failed && CtfCount("SELECT count(*) FROM revora_ctf_runs WHERE run='rollback' AND state='active'")==1,"failed finalization transaction leaves the run retryable");
                Check(CtfCount("SELECT guild_id FROM revora_title_ctf WHERE id=1")==55
                    && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE achievement_id=10301 AND uid=@uid",b.Entity.UID)==0,
                    "failed result rolls back champion change and losing player victory evidence");
                Query("ALTER TABLE revora_ctf_runs DROP CONSTRAINT ctf_finish_failure");
            }
            var result=Call(CtfStore,null,"Finish",name,end);
            string outcome=(string)result.GetType().GetField("Outcome",Instance).GetValue(result);
            Check(outcome==(name=="rollback"?"winner":name),"saved result policy: "+name);
            Check(CtfCount("SELECT guild_id FROM revora_title_ctf WHERE id=1")== (name=="rollback"?56:55),"atomic champion policy for "+name);
        }
    }
}
