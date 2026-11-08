using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Team=Revora.Game.ConquerStructures.Team;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type Cpk=typeof(Entity).Assembly.GetType("Revora.Game.Features.CouplePkEvent");
    static readonly Type CpkStore=typeof(Entity).Assembly.GetType("Revora.Game.Features.CouplePkResults");
    static DateTime cpkNow,cpkStart;
    static object NewCpk(string runtime) {
        Func<ushort,Map> factory=id=>{var m=World(runtime,id);Kernel.Maps.Remove(id);m.BaseID=id;m.ID=epkMapId++;Kernel.Maps[m.ID]=m;return m;};
        var e=Activator.CreateInstance(Cpk,BindingFlags.NonPublic|BindingFlags.Instance,null,new object[]{factory,(Func<DateTime>)(()=>cpkNow)},null);
        Cpk.GetField("Current",Hidden).SetValue(null,e);return e;
    }
    static GameState[] Couple(Map town,bool reload=false) {
        var a=MoonPlayer(town);var b=MoonPlayer(town);a.Entity.Body=1006;b.Entity.Body=2005;a.Entity.Spouse=b.Entity.Spouse="None";
        Query("UPDATE entities SET spouse='None',body=@body WHERE uid=@uid","uid",(long)a.Entity.UID,"body",1006);
        Query("UPDATE entities SET spouse='None',body=@body WHERE uid=@uid","uid",(long)b.Entity.UID,"body",2005);
        MoonNear(a,2319);MoonNear(b,2319);
        if(reload){SavePkProfileBeforeClaim(a);SavePkProfileBeforeClaim(b);}
        MarriageSend(a,b,Attack.MarriageRequest);MarriageSend(b,a,Attack.MarriageAccept);
        Check(a.Entity.Spouse==b.Entity.Name && b.Entity.Spouse==a.Entity.Name && HasAchievement(a,10129) && HasAchievement(b,10129),
            "Couple PK spouses earn reciprocal marriage through ordinary proposals and acceptance");
        if(reload){a=ReloadBall(a);b=ReloadBall(b);foreach(var p in new[]{a,b}){p.CalculateStatBonus();p.CalculateHPBonus();}
            a.Entity.MyAchievement.Send();b.Entity.MyAchievement.Send();
            Check(a.Entity.Spouse==b.Entity.Name && b.Entity.Spouse==a.Entity.Name && HasAchievement(a,10129) && HasAchievement(b,10129),
                "both entrant spouse identities and achievements survive production login before tournament admission");}
        var team=(Team)FormatterServices.GetUninitializedObject(typeof(Team));team.Lider=a;team.TeamLeader=true;team.Members=new ConcurrentDictionary<uint,Team.Member>();
        team.Members[a.Entity.UID]=new Team.Member{entry=a,Lider=true};team.Members[b.Entity.UID]=new Team.Member{entry=b};a.Team=b.Team=team;
        MoonNear(a,2319);MoonNear(b,2319);return new[]{a,b};
    }
    static void CpkNpc(GameState p,byte option){MoonNpc(p,2319,option);}
    static byte CpkInvite(object e,GameState[] pair) {
        foreach(var p in pair)MoonNear(p,2319);CpkNpc(pair[0],1);var invites=(IDictionary)Field(e,"invites");
        if(!invites.Contains(pair[1].Entity.UID))throw new Exception("Expected a server-issued spouse invitation.");
        return (byte)Field(invites[pair[1].Entity.UID],"Code");
    }
    static void CpkJoin(object e,GameState[] pair){byte code=CpkInvite(e,pair);CpkNpc(pair[1],0);CpkNpc(pair[1],code);}
    static void CpkTick(object e,double minutes){cpkNow=cpkStart.AddMinutes(minutes);Call(Cpk,e,"Tick");}
    static bool CoupleSoul(GameState p) {
        var souls=p.Inventory.Objects.Where(i=>ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7).ToArray();
        return souls.Length==1 && souls[0].Bound && Scalar("SELECT count(*) FROM revora_cpk_claims c JOIN items i ON i.uid=ANY(c.rewards) WHERE c.uid=@uid AND i.id BETWEEN 800000 AND 899999 AND i.bound=1",p.Entity.UID)==1;
    }
    static void VerifyCouplePk(string runtime,Map town) {
        PreparePkReloadSchema();
        Query("CREATE TABLE titles (LIKE public.titles INCLUDING ALL); CREATE TABLE status (LIKE public.status INCLUDING ALL)");
        StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);ConquerItem.ItemUID=new Counter(7200000);
        Server.GamePool=new GameState[0];Server.Thread=(Revora.Thread)FormatterServices.GetUninitializedObject(typeof(Revora.Thread));
        cpkStart=cpkNow=new DateTime(2026,9,19,4,0,0,DateTimeKind.Utc);pkNow=cpkNow;NewPk(runtime);Call(Pk,null,"PlaceGeneral");var e=NewCpk(runtime);Call(Cpk,null,"PlaceGuide");
        var arena=(Map)Field(e,"arena");Check(town.Npcs.ContainsKey(2319) && arena.Npcs.ContainsKey(23140) && !Revora.Game.Features.TwinCityPlaza.IsClosed(2319),"ordinary Couple PK guide and owned awarder remain present after startup placement");
        Check((DateTime)Call(Cpk,null,"Window",cpkStart)==cpkStart && Call(Cpk,null,"Window",cpkStart.AddDays(1))==null,"Couple PK uses Friday's Pacific evening date");
        Check((DateTime)Call(Cpk,null,"Window",new DateTime(2026,12,19,5,0,0,DateTimeKind.Utc))==new DateTime(2026,12,19,5,0,0,DateTimeKind.Utc),"winter Couple PK follows Pacific standard time");
        Call(Cpk,e,"Tick");var a=Couple(town,true);var b=Couple(town,true);var outsider=MoonPlayer(town);var fakePair=Couple(town);
        fakePair[0].Fake=true;CpkNpc(fakePair[0],1);Check(((IDictionary)Field(e,"invites")).Count==0,"fake spouses cannot create an invitation");fakePair[0].Fake=false;
        CpkNpc(a[1],130);Check(CtfCount("SELECT count(*) FROM revora_cpk_pairs")==0,"unsolicited spouse confirmation cannot reserve a pair");
        a[1].Entity.Spouse="None";CpkNpc(a[0],1);Check(((IDictionary)Field(e,"invites")).Count==0,"one-way marriage cache cannot invite a spouse");a[1].Entity.Spouse=a[0].Entity.Name;
        Query("UPDATE entities SET spouse='None' WHERE uid=@uid","uid",(long)a[1].Entity.UID);byte rejected=CpkInvite(e,a);CpkNpc(a[1],rejected);
        Check(CtfCount("SELECT count(*) FROM revora_cpk_pairs")==0 && a.All(p=>p.Entity.MapID==1002),"cache-only marriage cannot bypass reciprocal durable spouse records");
        Query("UPDATE entities SET spouse=@spouse WHERE uid=@uid","uid",(long)a[1].Entity.UID,"spouse",a[0].Entity.Name);
        byte accepted=CpkInvite(e,a);CpkNpc(a[1],rejected);Check(CtfCount("SELECT count(*) FROM revora_cpk_pairs")==0,"an earlier confirmation code cannot accept a replacement invitation");
        CpkNpc(outsider,accepted);Check(CtfCount("SELECT count(*) FROM revora_cpk_pairs")==0,"another character cannot accept the spouse's confirmation code");
        MoonNear(a[1],2319);Parallel.For(0,8,i=>Npcs.GetDialog(new NpcRequest(true){NpcID=2319,OptionID=accepted},a[1]));
        Check(a.All(p=>p.Entity.MapID==arena.ID && p.Account.State==AccountTable.AccountState.Player) && CtfCount("SELECT count(*) FROM revora_cpk_pairs")==1,"concurrent spouse confirmations admit one real pair");
        var admittedPair=((IDictionary)Field(e,"members"))[a[0].Entity.UID];admittedPair.GetType().GetField("Admitted",Instance).SetValue(admittedPair,false);
        Call(Cpk,e,"Travel",Field(admittedPair,"Wife"),true);CpkTick(e,.02);
        Check(a.All(p=>p.Entity.MapID==arena.ID) && (bool)Field(admittedPair,"Admitted"),"a partial arena import retries while combat remains blocked: "+string.Join(";",a.Select(p=>p.Entity.Class+"/"+p.Entity.Hitpoints+"/"+p.Entity.MaxHitpoints+"/"+p.Entity.MapID+"/"+p.Entity.Dead)));
        Check(!HasAchievement(a[0],11115) && !HasAchievement(a[1],11115),"an uncontested pair has no participation evidence");
        byte expired=CpkInvite(e,b);CpkTick(e,1.6);CpkNpc(b[1],expired);Check(CtfCount("SELECT count(*) FROM revora_cpk_pairs")==1,"expired spouse consent cannot admit another pair");
        CpkJoin(e,b);CpkTick(e,1.7);
        Check(a.Concat(b).All(p=>HasAchievement(p,11115) && !HasAchievement(p,11121)),"two genuine admitted pairs earn participation for all four players");
        uint hp=b[0].Entity.Hitpoints;EpkDamage(a[0],b[0],100);Check(b[0].Entity.Hitpoints==hp,"new pair preparation blocks incoming damage");
        outsider.Entity.Teleport(arena.ID,a[0].Entity.X,a[0].Entity.Y);Check(outsider.Entity.MapID==1002,"outsider teleport cannot enter the owned Couple PK field");
        EpkDamage(outsider,a[0],100);Check(a[0].Entity.Hitpoints==a[0].Entity.MaxHitpoints,"direct outsider receive attack cannot damage an entrant");
        CpkTick(e,1.9);uint spouseHp=a[1].Entity.Hitpoints;EpkDamage(a[0],a[1],100);
        Check(a[1].Entity.Hitpoints==spouseHp && !Revora.Game.Attacking.Handle.CanAttack(a[0].Entity,a[1].Entity,null,true),"spouses cannot damage one another through normal or direct attack paths");
        LabNear(a[0],23140);Check(Kernel.GetDistance(a[0].Entity.X,a[0].Entity.Y,arena.Npcs[23140].X,arena.Npcs[23140].Y)<=6,"Couple PK awarder is reachable through production jumps");
        EpkDamage(a[0],b[0],100);EpkDamage(b[0],a[1],100);a[1].Entity.Die(b[0].Entity);CpkTick(e,2);
        Check(a[1].Entity.MapID==1002 && !a[1].Entity.Dead && (bool)Call(Cpk,null,"InSession",a[1]),"a defeated spouse retreats alive while reserving the pair's outcome");
        a[1].Entity.Teleport(arena.ID,a[0].Entity.X,a[0].Entity.Y);Revora.Game.Qualifier.QualifyEngine.DoSignup(a[1]);
        Check(a[1].Entity.MapID==1002 && a[1].QualifierGroup==null,"a retreated spouse cannot reenter or join another Arena match");
        EpkDamage(a[0],b[0],100);b[0].Entity.Die(a[0].Entity);EpkDamage(a[0],b[1],100);b[1].Entity.Die(a[0].Entity);CpkTick(e,2.1);
        Check(b.All(p=>p.Entity.MapID==1002 && p.Entity.OnDeath==null) && !HasAchievement(a[0],11121),"both defeated opponents leave without awarding an early winner");
        CpkJoin(e,b);Check(b.All(p=>p.Entity.MapID==1002) && Scalar("SELECT count(*) FROM revora_cpk_entries WHERE uid=@uid",b[0].Entity.UID)==1,"a defeated pair cannot reenter the same dated tournament");
        Query("ALTER TABLE revora_cpk_runs ADD CONSTRAINT couple_result_failure CHECK(winner IS NULL) NOT VALID");CpkTick(e,10);
        Check(CtfCount("SELECT count(*) FROM revora_cpk_runs WHERE winner IS NOT NULL")==0 && !HasAchievement(a[1],11121),"failed result transaction rolls back both spouse achievements");
        a[0].Entity.Teleport(1002,408,330);
        Query("ALTER TABLE revora_cpk_runs DROP CONSTRAINT couple_result_failure");CpkTick(e,61);
        foreach(var p in a)EvidenceRefresh(p);
        Check(a.All(p=>HasAchievement(p,11121) && p.Entity.OnDeath==null) && CtfCount("SELECT count(*) FROM revora_cpk_runs WHERE expires_at=ended_at+interval '7 days'")==1,
            "delayed finalization preserves captured standings after departure and credits both spouses with one dated result");
        foreach(var p in a)Call(Pk,null,"SynchronizeHonors",p,cpkStart.AddMinutes(10));Check(a.All(p=>p.Entity.ContainsFlag(51)),"both verified champions receive the native spouse halo");
        foreach(var owner in a)SavePkProfileBeforeClaim(owner);
        var filler=Enumerable.Range(0,38).Select(i=>Pack(a[0],1088001,1)).ToArray();CpkNpc(a[0],3);Check(CtfCount("SELECT count(*) FROM revora_cpk_claims")==0,"full bags preserve an individual spouse's saved purse");
        foreach(var item in filler){Query("DELETE FROM items WHERE uid=@id","id",(long)item.UID);a[0].Inventory.Remove(item.UID,Enums.ItemUse.None,false);}
        a[0].Entity.Money=Entity.MaximumSilver;CpkNpc(a[0],3);
        Check(!Fenced(a[0]) && CtfCount("SELECT count(*) FROM revora_cpk_claims")==0 && a[0].Inventory.Count==0,
            "couple champion purse waits when its silver would exceed durable storage");a[0].Entity.Money=0;
        Query("ALTER TABLE revora_cpk_claims ADD CONSTRAINT couple_claim_failure CHECK(uid=0) NOT VALID");CpkNpc(a[0],3);
        Check(Fenced(a[0]) && a[0].Entity.ConquerPoints==0 && a[0].Inventory.Count==0,"failed purse receipt rolls back currency and items");
        Query("ALTER TABLE revora_cpk_claims DROP CONSTRAINT couple_claim_failure");DemonReset(a[0]);foreach(var p in a)MoonNear(p,2319);
        Parallel.For(0,12,i=>Npcs.GetDialog(new NpcRequest(true){NpcID=2319,OptionID=3},a[i%2]));
        Check(a.All(p=>p.Entity.ConquerPoints==50000 && p.Entity.RevoraPoint==500 && p.Entity.Money==500000 && p.Inventory.Count==3) && CtfCount("SELECT count(*) FROM revora_cpk_claims")==2,
            "concurrent two-spouse claims pay each champion exactly once");
        Check(a.All(p=>p.Inventory.Objects.Count(item=>item.ID==722136 && item.Bound)==2 && CoupleSoul(p) && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==50000),"individual bound rewards and purses persist");
        foreach(var p in a)Call(Pk,null,"SynchronizeHonors",p,cpkStart.AddMinutes(10).AddDays(7));Check(a.All(p=>!p.Entity.ContainsFlag(51)),"both spouse halos expire at the original result deadline");
        // Failed admission and a replacement reservation in a later actual window.
        cpkStart=cpkNow=cpkStart.AddDays(7);Call(Cpk,e,"Tick");var retry=Couple(town);byte invitation=CpkInvite(e,retry);
        Query("ALTER TABLE revora_cpk_pairs ADD CONSTRAINT couple_admit_failure CHECK(admitted_at IS NULL) NOT VALID");CpkNpc(retry[1],invitation);
        Check(Fenced(retry[1]) && retry.All(p=>p.Entity.MapID==arena.ID) && Scalar("SELECT count(*) FROM revora_cpk_entries e JOIN revora_cpk_pairs p ON p.id=e.pair WHERE e.uid=@uid AND p.admitted_at IS NOT NULL",retry[1].Entity.UID)==0,
            "failed admission after both transfers creates no admitted pair or participation");
        Query("ALTER TABLE revora_cpk_pairs DROP CONSTRAINT couple_admit_failure");DemonReset(retry[1]);Call(Cpk,null,"OnLogin",retry[1]);CpkJoin(e,retry);CpkTick(e,.1);
        Check(retry.All(p=>p.Entity.MapID==arena.ID) && Scalar("SELECT count(*) FROM revora_cpk_entries WHERE uid=@uid",retry[1].Entity.UID)==1,
            "reconnect retries an unadmitted reservation without duplicate entries or stale eviction");
        var changing=Couple(town);CpkJoin(e,changing);CpkTick(e,.2);changing[1].Entity.Spouse="None";CpkTick(e,.3);
        Check(changing.All(p=>p.Entity.MapID==1002 && p.Entity.OnDeath==null),"breaking the marriage forfeits both field places");
        retry[1].Action=0;CpkTick(e,.4);Check(retry[0].Entity.MapID==1002 && retry[0].Entity.OnDeath==null,"a disconnected spouse forfeits the pair instead of leaving an eligible solo entrant");
        CpkTick(e,10);Check(CtfCount("SELECT count(*) FROM revora_cpk_runs WHERE winner IS NOT NULL")==1,"withdrawals and idle survival cannot manufacture another championship");
        // A changed team is a forfeit; two idle pairs cannot win at the deadline.
        cpkStart=cpkNow=cpkStart.AddDays(7);Call(Cpk,e,"Tick");var teamChange=Couple(town);var idle=Couple(town);var other=Couple(town);
        CpkJoin(e,teamChange);CpkJoin(e,idle);CpkJoin(e,other);CpkTick(e,.2);Team.Member removed;teamChange[0].Team.Members.TryRemove(teamChange[1].Entity.UID,out removed);CpkTick(e,.3);
        Check(teamChange.All(p=>p.Entity.MapID==1002 && p.Entity.OnDeath==null),"changing the two-person team forfeits both entrants");
        CpkTick(e,9);var late=Couple(town);CpkNpc(late[0],1);Check(!((IDictionary)Field(e,"invites")).Contains(late[1].Entity.UID),"joint registration closes at exactly 21:09 Pacific");
        CpkTick(e,60);Check(idle.Concat(other).All(p=>p.Entity.MapID==1002) && CtfCount("SELECT count(*) FROM revora_cpk_runs WHERE winner IS NOT NULL")==1,"two unresolved pairs leave at 22:00 without a champion");
        cpkStart=cpkNow=cpkStart.AddDays(7);Call(Cpk,e,"Tick");var interrupted=Couple(town);CpkJoin(e,interrupted);
        e=NewCpk(runtime);CpkNpc(a[0],3);Check(CtfCount("SELECT count(*) FROM revora_cpk_claims")==2 && a[0].Inventory.Count==3,"restart preserves saved claims and rejects reward replay");
        Check(CtfCount("SELECT count(*) FROM revora_cpk_runs WHERE state='open'")==0 && CtfCount("SELECT count(*) FROM revora_cpk_runs WHERE state='cancelled'")==1,"restart cancels an actual admitted unfinished pair without a new result");
        for(int index=0;index<a.Length;index++) {
            var before=a[index].Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i).ToArray();
            a[index]=ReloadBall(a[index]);Call(Cpk,null,"OnLogin",a[index]);CpkNpc(a[index],3);
            Check(CoupleSoul(a[index]) && a[index].Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i).SequenceEqual(before)
                && a[index].Entity.ConquerPoints==50000 && a[index].Entity.RevoraPoint==500 && a[index].Entity.Money==500000,
                "fresh ordinary spouse reload retains exact bound soul, items and purse without duplicate payment");
        }
        cpkStart=cpkNow=cpkStart.AddDays(7);Call(Cpk,e,"Tick");
        Query("UPDATE revora_cpk_runs SET valuable_reward=false WHERE state='open'");
        var old=Couple(town);var opponents=Couple(town);CpkJoin(e,old);CpkJoin(e,opponents);CpkTick(e,2);
        foreach(var rival in opponents){EpkDamage(old[0],rival,100);rival.Entity.Die(old[0].Entity);}CpkTick(e,10);
        e=NewCpk(runtime);
        foreach(var spouse in old){CpkNpc(spouse,3);CpkNpc(spouse,3);}
        Check(old.All(p=>p.Inventory.Count==2 && p.Inventory.Objects.All(i=>i.ID==722136 && i.Bound)
            && p.Entity.ConquerPoints==50000 && Scalar("SELECT count(*) FROM revora_cpk_claims WHERE uid=@uid AND cardinality(rewards)=2",p.Entity.UID)==1),
            "older earned pair result retains both original spouse purses through restart and replay");
        Cpk.GetField("Current",Hidden).SetValue(null,null);Pk.GetField("Current",Hidden).SetValue(null,null);
    }
}
