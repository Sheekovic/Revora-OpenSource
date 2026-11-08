using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static long BpSouls(GameState p) {
        long saved=Scalar("SELECT count(*) FROM revora_bp_claims c JOIN items i ON i.uid=c.soul_uid WHERE c.uid=@uid AND i.bound=1 AND i.id BETWEEN 800000 AND 899999",p.Entity.UID);
        return p.Inventory.Objects.Count(i=>i.Bound && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)==saved?saved:-1;
    }
    static readonly Type BpPk = typeof(Entity).Assembly.GetType("Revora.Game.Features.BattlePowerTournament");
    static readonly TimeZoneInfo bpZone=TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
    static GameState BpPlayer(Map town,int power) {
        var p=PkPlayer(town,140);p.Entity.FullyLoaded=false;p.Entity.Reborn=2;p.Entity.FullyLoaded=true;
        p.Entity.GuildSharedBp=15;p.Entity.ExtraBattlePower=(uint)(power-165);
        Query("UPDATE entities SET conquerpoints=0,revorapoint=0,money=0 WHERE uid=@uid","uid",(long)p.Entity.UID);
        ChiTable.Load(p);SetJadeChi(p,0);return p;
    }
    static void BpNpc(GameState p,byte option){MoonNpc(p,91050,option);}
    static long BpClaims(GameState p){return Scalar("SELECT count(*) FROM revora_bp_claims WHERE uid=@uid",p.Entity.UID);}
    static DateTime BpMonth(int offset) {
        var local=new DateTime(2031,1,1).AddMonths(offset);local=local.AddDays((7-(int)local.DayOfWeek)%7).AddHours(20);
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local,DateTimeKind.Unspecified),bpZone);
    }
    static DateTime BpWeek(DateTime start,int week) {
        var local=TimeZoneInfo.ConvertTimeFromUtc(start,bpZone).AddDays(week*7);
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local,DateTimeKind.Unspecified),bpZone);
    }
    static object BpWin(string runtime,Map town,GameState p,int kind,DateTime start,bool oldContract=false) {
        pkStart=pkNow=start;var e=NewPk(runtime);Call(Pk,e,"Tick");
        var other=BpPlayer(town,(int)p.Entity.BattlePower);BpNpc(p,(byte)(kind-19));BpNpc(other,(byte)(kind-19));
        if(oldContract)Query("UPDATE revora_pk_wars SET bp_valuable_reward=false WHERE run IN (SELECT run FROM revora_pk_entries WHERE uid=@uid)","uid",(long)p.Entity.UID);
        PkTick(e,10);
        if(p.Entity.MapID==1002 || other.Entity.MapID!=p.Entity.MapID)throw new Exception("BP fixture admission failed");
        EpkDamage(p,other,1);other.Entity.Die(p.Entity);PkTick(e,10.02);
        if(p.Entity.MapID!=1002 || Fenced(p))throw new Exception("BP fixture completion failed");
        return e;
    }
    static void VerifyBpPk(string runtime,Map town) {
        Query("DROP TABLE entities;CREATE TABLE entities AS TABLE public.entities WITH NO DATA;CREATE TABLE titles (LIKE public.titles INCLUDING ALL);CREATE TABLE status (LIKE public.status INCLUDING ALL);CREATE TABLE chi (LIKE public.chi INCLUDING ALL)");
        StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);Call(EvidenceType,null,"Initialize");
        ConquerItem.ItemUID=new Counter(8900000);Server.GamePool=new GameState[0];Server.Thread=(Revora.Thread)FormatterServices.GetUninitializedObject(typeof(Revora.Thread));
        Call(Pk,null,"PlaceGeneral");
        Check(town.Npcs[91050].Mesh==37270 && town.Npcs[91050].X==295 && town.Npcs[91050].Y==141 && !Revora.Game.Features.TwinCityPlaza.IsClosed(91050),
            "BP manager uses installed presentation and original coordinates through the real server floor convention");
        foreach(int[] row in new[]{new[]{249,-1},new[]{250,20},new[]{300,20},new[]{301,21},new[]{350,21},new[]{351,22},new[]{1000,22}})
            Check((int)Call(BpPk,null,"Kind",row[0])==row[1],"nonoverlapping BP division boundary "+row[0]);
        var fifth=TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026,11,29,20,0,0),bpZone);
        Check(!(bool)Call(Pk,null,"Scheduled",fifth,20) && (bool)Call(Pk,null,"Scheduled",BpMonth(0),20),"BP season offers its first four Sundays and skips a fifth");
        Check((DateTime)Call(BpPk,null,"SeasonEnd",BpMonth(0))==BpMonth(1),"season tokens expire at the following month's first Sunday trial");
        Check((bool)Call(Pk,null,"Scheduled",BpWeek(BpMonth(26),1),20)
            && (DateTime)Call(Pk,null,"Start",BpWeek(BpMonth(26),1))==BpWeek(BpMonth(26),1),
            "successive Sunday trials retain 20:00 Pacific across daylight saving changes");
        pkStart=pkNow=BpMonth(0);var e=NewPk(runtime);Call(Pk,e,"Tick");
        Check(CtfCount("SELECT count(*) FROM revora_pk_wars WHERE kind BETWEEN 20 AND 22")==3,"one Sunday opens exactly three simultaneous BP divisions");
        var winners=new GameState[3];var losers=new GameState[3];
        for(int i=0;i<3;i++) {
            winners[i]=BpPlayer(town,new[]{250,301,351}[i]);losers[i]=BpPlayer(town,new[]{300,350,360}[i]);
            BpNpc(winners[i],(byte)(i+1));BpNpc(losers[i],(byte)(i+1));
            Check(winners[i].Entity.MapID!=1002 && winners[i].Entity.MapID==losers[i].Entity.MapID && winners[i].Account.State==AccountTable.AccountState.Player,
                "ordinary BP pair enters its owned division "+i);
        }
        PkTick(e,.02);
        Check(winners.Select(p=>p.Entity.MapID).Distinct().Count()==3,"BP groups occupy different private arenas");
        Check(winners.All(p=>HasAchievement(p,11114) && !HasAchievement(p,11116) && !HasAchievement(p,11131)),"contested BP admission awards only its matching participation identity");
        Check((bool)Call(Pk,null,"BlockAttack",winners[0].Entity,losers[0].Entity),"BP registration is peaceful until 20:10");
        var intruder=BpPlayer(town,300);intruder.Entity.Teleport(winners[0].Entity.MapID,30,30);
        Check(intruder.Entity.MapID==1002,"external teleport cannot enter a BP arena");
        uint life=losers[0].Entity.Hitpoints;EpkDamage(intruder,losers[0],100);
        Check(losers[0].Entity.Hitpoints==life,"outside damage cannot influence BP results");
        PkTick(e,10);
        Check((bool)Call(Pk,null,"BlockAttack",winners[0].Entity,winners[1].Entity),"cross-division damage stays blocked after battle starts");
        for(int i=0;i<3;i++){EpkDamage(winners[i],losers[i],1);losers[i].Entity.Die(winners[i].Entity);}
        PkTick(e,10.02);
        Check(winners.Concat(losers).All(p=>p.Entity.MapID==1002 && p.Entity.OnDeath==null && !p.Entity.Dead),"BP finalization restores players after their owned deaths and results");
        Check(CtfCount("SELECT count(*) FROM revora_pk_wars WHERE kind BETWEEN 20 AND 22 AND winner<>0 AND expires_at=ended_at+INTERVAL '7 days'")==3,
            "BP champions retain exact seven-day honor entitlements while native halo identity remains pending");
        foreach(string table in new[]{"teamarena","championarena","quests","reward","rewardclameid","merchant","dialyquests","activeness","house","pkexploits","achievement","itemadding"})
            Query("CREATE TABLE IF NOT EXISTS "+table+" AS TABLE public."+table+" WITH NO DATA");
        Query("CREATE TABLE IF NOT EXISTS prizes(uid bigint PRIMARY KEY,name text,prizes bytea)");
        for(int i=0;i<3;i++) {
            var p=winners[i];SavePkProfileBeforeClaim(p);uint wardrobe=StoredQuestGarment(p);BpNpc(p,10);uint cp=new uint[]{300,500,1000}[i],chi=new uint[]{5000,10000,15000}[i];
            Check(!Fenced(p) && p.Entity.ConquerPoints==cp && p.ChiPoints==chi && JadeChi(p)==chi && BpClaims(p)==1
                && BpSouls(p)==1 && p.Inventory.Objects.Count(item=>item.ID==3006840+i && item.Bound && item.StackSize==1)==1,"champion claims original CP/Chi/token rewards for division "+i);
            Parallel.For(0,8,n=>BpNpc(p,10));ChiTable.Load(p);BpNpc(p,10);
            Check(p.Entity.ConquerPoints==cp && p.ChiPoints==chi && BpClaims(p)==1,"parallel and reconnect claim replay preserves division purse "+i);
            PkNpc(p,3);Check(p.Entity.ConquerPoints==cp,"BP results cannot be claimed as the larger classic PK purse "+i);
            Call(Pk,null,"SynchronizeHonors",p,pkNow);
            Check(!p.Entity.ContainsFlag(39) && !HasAchievement(p,11131),"unresolved BP halo never becomes an unrelated Class PK honor "+i);
            var rewardIds=p.Inventory.Objects.Select(item=>item.UID).OrderBy(id=>id).ToArray();
            p=ReloadBall(p);ChiTable.Load(p);Call(Pk,null,"OnLogin",p);BpNpc(p,10);
            Check(p.Account.State==AccountTable.AccountState.Player && p.Entity.ConquerPoints==cp && p.ChiPoints==chi && BpClaims(p)==1 && BpSouls(p)==1
                && p.Inventory.Objects.Select(item=>item.UID).OrderBy(id=>id).SequenceEqual(rewardIds),
                "fresh BP champion reload preserves exact bound soul/token and CP/Chi without another claim "+i);
            Check(Scalar("SELECT count(*) FROM items WHERE uid=@uid AND position=0 AND warehouse=0 AND inwardrobe=1",wardrobe)==1,
                "BP division reward claim and production reload preserve stored wardrobe design "+i);

        }
        var switched=losers[0];switched.Entity.ExtraBattlePower=180;BpNpc(switched,2);
        Check(switched.Entity.MapID==1002 && PkEntries(switched)==1,"saved BP reservation prevents changing groups after elimination");
        var late=BpPlayer(town,300);BpNpc(late,1);Check(PkEntries(late)==0,"closed registration cannot admit a late BP entrant");
        VerifyBpPkEligibility(runtime,town);
        VerifyBpPkRewards(runtime,town);
    }
    static void VerifyBpPkEligibility(string runtime,Map town) {
        pkStart=pkNow=BpMonth(1);var e=NewPk(runtime);Call(Pk,e,"Tick");
        var low=BpPlayer(town,249);BpNpc(low,1);Check(PkEntries(low)==0,"below-250 players cannot enter BP PK");
        var fake=BpPlayer(town,300);fake.Fake=true;BpNpc(fake,1);Check(PkEntries(fake)==0,"ambient fake actors cannot enter BP PK");fake.Fake=false;
        var wrong=BpPlayer(town,351);BpNpc(wrong,1);Check(PkEntries(wrong)==0,"high-BP players cannot select a lower division");
        var a=BpPlayer(town,300);var b=BpPlayer(town,300);BpNpc(a,1);BpNpc(b,1);PkTick(e,10);
        a.Entity.ExtraBattlePower=136;
        Check((bool)Call(Pk,null,"BlockAttack",a.Entity,b.Entity),"exceeding a saved BP ceiling blocks combat before the next timer");
        PkTick(e,10.02);Check(a.Entity.MapID==1002,"over-limit entrant forfeits the frozen division");
        a.Entity.ExtraBattlePower=135;BpNpc(a,1);Check(PkEntries(a)==1,"removing excess gear cannot undo a forfeit");
        Check((bool)Call(BpPk,null,"Eligible",20,240,true),"loss of a shared bonus does not disqualify an already admitted fighter");
        var solo=BpPlayer(town,351); // the original upper field has no opponent
        pkNow=pkStart.AddMinutes(1);BpNpc(solo,3);PkTick(e,11);
        Check(!HasAchievement(solo,11114) && BpClaims(solo)==0,"a solitary entrant earns no participation or champion purse");
        pkStart=pkNow=BpMonth(2);e=NewPk(runtime);Call(Pk,e,"Tick");
        a=BpPlayer(town,300);b=BpPlayer(town,300);BpNpc(a,1);BpNpc(b,1);PkTick(e,10);EpkDamage(a,b,1);PkTick(e,60);
        BpNpc(a,10);Check(BpClaims(a)==0 && a.Entity.MapID==1002,"unresolved deadline does not invent a BP champion");
        pkStart=pkNow=BpMonth(3);e=NewPk(runtime);Call(Pk,e,"Tick");a=BpPlayer(town,300);a.Entity.PKMode=Enums.PKMode.Capture;BpNpc(a,1);
        ushort vanished=a.Entity.MapID;NewPk(runtime);Call(Pk,LocalCurrent(),"Tick");
        Kernel.Maps.Remove(vanished);MapsTable.MapInformations.Remove(vanished);typeof(GameState).GetField("map",Instance).SetValue(a,null);
        using(var cmd=new Revora.Database.MySqlCommand(MySqlCommandType.SELECT).Select("entities").Where("uid",a.Entity.UID))
        using(var reader=new MySqlReader(cmd)){Check(reader.Read(),"saved BP location is available to the production entity reader");Call(BpPk,null,"LoadedLocation",a,reader.ReadUInt16("MapID"));}
        a.Entity.FullyLoaded=false;a.Entity.MapID=1002;a.Entity.FullyLoaded=true;
        Call(Pk,null,"OnLogin",a);
        Check(CtfCount("SELECT count(*) FROM revora_pk_wars WHERE state='open' AND kind BETWEEN 20 AND 22")==0 && BpClaims(a)==0
            && a.Entity.MapID==1002 && a.Entity.OnDeath==null && a.Entity.PKMode==Enums.PKMode.Capture,
            "restart recovers the saved arena and PK mode even after the loader normalizes a missing map");
    }
    static object LocalCurrent(){return Pk.GetField("Current",Hidden).GetValue(null);}
    static void VerifyBpPkRewards(string runtime,Map town) {
        int month=4;
        for(int kind=20;kind<=22;kind++)for(int count=2;count<=4;count++) {
            var p=BpPlayer(town,new[]{300,350,360}[kind-20]);var start=BpMonth(month++);
            for(int week=0;week<count;week++){BpWin(runtime,town,p,kind,BpWeek(start,week));BpNpc(p,10);}
            uint before=p.ChiPoints;uint[,] table={{5000,10000,20000},{10000,20000,30000},{20000,30000,50000}};
            byte option=(byte)(20+(kind-20)*3+count-2);Parallel.For(0,8,i=>BpNpc(p,option));
            Check(!Fenced(p) && p.ChiPoints==before+table[kind-20,count-2] && JadeChi(p)==p.ChiPoints && p.Inventory.Count==count && BpSouls(p)==count
                && Scalar("SELECT count(*) FROM revora_bp_exchanges WHERE uid=@uid",p.Entity.UID)==1,
                "earned same-season token exchange is atomic for kind "+kind+" count "+count);
            ChiTable.Load(p);BpNpc(p,option);Check(p.ChiPoints==before+table[kind-20,count-2],"exchange replay after Chi reload cannot recreate consumed tokens");
        }
        var counterfeit=BpPlayer(town,300);Pack(counterfeit,3006840,1);Pack(counterfeit,3006840,1);BpNpc(counterfeit,20);
        Check(counterfeit.ChiPoints==0 && counterfeit.Inventory.Count==2,"tokens without a saved champion origin cannot grant Chi");
        var full=BpPlayer(town,300);BpWin(runtime,town,full,20,BpMonth(month++));for(int i=0;i<39;i++)Pack(full,1088001,1);BpNpc(full,10);
        Check(BpClaims(full)==0 && full.ChiPoints==0 && full.Entity.ConquerPoints==0,"full bag preserves the complete unclaimed BP purse");
        var overflow=BpPlayer(town,300);BpWin(runtime,town,overflow,20,BpMonth(month++));SetJadeChi(overflow,uint.MaxValue);BpNpc(overflow,10);
        Check(BpClaims(overflow)==0 && overflow.Entity.ConquerPoints==0,"Chi overflow defers the whole BP purse");SetJadeChi(overflow,0);BpNpc(overflow,10);
        Check(BpClaims(overflow)==1 && overflow.ChiPoints==5000,"deferred Chi purse can be claimed after making room");
        var cpFull=BpPlayer(town,300);BpWin(runtime,town,cpFull,20,BpMonth(month++));cpFull.Entity.ConquerPoints=uint.MaxValue;BpNpc(cpFull,10);
        Check(BpClaims(cpFull)==0 && cpFull.ChiPoints==0 && cpFull.Inventory.Count==0 && !Fenced(cpFull),"CP overflow preserves Chi, token and the whole unpaid purse");
        var mixed=BpPlayer(town,300);var mixedStart=BpMonth(month++);BpWin(runtime,town,mixed,20,mixedStart);BpNpc(mixed,10);
        mixed.Entity.ExtraBattlePower=185;BpWin(runtime,town,mixed,21,BpWeek(mixedStart,1));BpNpc(mixed,10);uint mixedChi=mixed.ChiPoints;
        BpNpc(mixed,20);BpNpc(mixed,23);
        Check(!Fenced(mixed) && mixed.ChiPoints==mixedChi && mixed.Inventory.Count==4 && BpSouls(mixed)==2,"victories in different BP divisions cannot combine unlike tokens");
        var expired=BpPlayer(town,300);var old=BpMonth(month++);BpWin(runtime,town,expired,20,old);pkNow=(DateTime)Call(BpPk,null,"SeasonEnd",old);BpNpc(expired,10);
        Check(BpClaims(expired)==1 && expired.ChiPoints==5000 && expired.Entity.ConquerPoints==300 && expired.Inventory.Count==1 && BpSouls(expired)==1,
            "late CP and Chi remain claimable after the season token expires");
        foreach(string mode in new[]{"item","claim","origin","silent-item","silent-claim","silent-origin"}) {
            var p=BpPlayer(town,300);BpWin(runtime,town,p,20,BpMonth(month++));string table=mode.EndsWith("item")?"items":mode.EndsWith("claim")?"revora_bp_claims":"revora_bp_tokens";
            string body=mode.StartsWith("silent")?"RETURN NULL;":"RAISE EXCEPTION ''fixture BP fault'';";
            Query("CREATE FUNCTION fail_bp() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN "+body+" END';CREATE TRIGGER fail_bp BEFORE INSERT ON "+table+" FOR EACH ROW EXECUTE FUNCTION fail_bp()");
            BpNpc(p,10);
            Check(Fenced(p) && BpClaims(p)==0 && JadeChi(p)==0 && p.Entity.ConquerPoints==0 && p.Inventory.Count==0,"BP claim rolls back and fences on "+mode);
            Query("DROP TRIGGER fail_bp ON "+table+";DROP FUNCTION fail_bp()");DemonReset(p);BpNpc(p,10);
            Check(!Fenced(p) && BpClaims(p)==1 && p.ChiPoints==5000,"BP claim retry recovers once after "+mode);
        }
        var legacy=BpPlayer(town,300);BpWin(runtime,town,legacy,20,BpMonth(month++),true);NewPk(runtime);BpNpc(legacy,10);BpNpc(legacy,10);
        Check(legacy.Inventory.Count==1 && legacy.Inventory.Objects[0].ID==3006840 && BpSouls(legacy)==0 && BpClaims(legacy)==1
            && legacy.Entity.ConquerPoints==300 && legacy.ChiPoints==5000,
            "older BP contract earns its original token and CP/Chi once after coordinator restart: items="+legacy.Inventory.Count+" souls="+BpSouls(legacy)+" claims="+BpClaims(legacy)+" cp="+legacy.Entity.ConquerPoints+" chi="+legacy.ChiPoints+" flag="+Scalar("SELECT count(*) FROM revora_pk_wars WHERE winner=@uid AND bp_valuable_reward",legacy.Entity.UID));
        VerifyBpPkExchangeFailures(runtime,town,month);
    }
    static void VerifyBpPkExchangeFailures(string runtime,Map town,int month) {
        foreach(string mode in new[]{"locked","foreign","expired","chi-overflow","chi-preimage","receipt","silent-receipt","silent-delete"}) {
            var p=BpPlayer(town,300);var start=BpMonth(month++);
            for(int week=0;week<2;week++){BpWin(runtime,town,p,20,BpWeek(start,week));BpNpc(p,10);}
            uint before=p.ChiPoints;var item=p.Inventory.Objects.First(i=>i.ID==3006840);
            if(mode=="locked"){item.Lock=1;Query("UPDATE items SET locked=1 WHERE uid=@uid","uid",(long)item.UID);}
            if(mode=="foreign")Query("UPDATE items SET entityid=entityid+100000 WHERE uid=@uid","uid",(long)item.UID);
            if(mode=="expired")pkNow=(DateTime)Call(BpPk,null,"SeasonEnd",start);
            if(mode=="chi-overflow"){SetJadeChi(p,uint.MaxValue);before=uint.MaxValue;}
            if(mode=="chi-preimage")Query("UPDATE chi SET points=points+1 WHERE uid=@uid","uid",(long)p.Entity.UID);
            if(mode.Contains("receipt"))Query("CREATE FUNCTION fail_exchange() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN "+(mode.StartsWith("silent")?"RETURN NULL;":"RAISE EXCEPTION ''fixture exchange failure'';")+" END';CREATE TRIGGER fail_exchange BEFORE INSERT ON revora_bp_exchanges FOR EACH ROW EXECUTE FUNCTION fail_exchange()");
            if(mode=="silent-delete")Query("CREATE FUNCTION fail_exchange() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER fail_exchange BEFORE DELETE ON items FOR EACH ROW EXECUTE FUNCTION fail_exchange()");
            BpNpc(p,20);
            Check(p.ChiPoints==before && p.Inventory.Count==4 && BpSouls(p)==2 && Scalar("SELECT count(*) FROM revora_bp_tokens WHERE uid=@uid AND spent_at IS NOT NULL",p.Entity.UID)==0
                && Scalar("SELECT count(*) FROM revora_bp_exchanges WHERE uid=@uid",p.Entity.UID)==0,"rejected token exchange preserves rewards and receipts for "+mode);
            if(mode.Contains("receipt") || mode=="silent-delete") {
                Query("DROP TRIGGER fail_exchange ON "+(mode=="silent-delete"?"items":"revora_bp_exchanges")+";DROP FUNCTION fail_exchange()");DemonReset(p);BpNpc(p,20);
                Check(!Fenced(p) && p.ChiPoints==before+5000 && p.Inventory.Count==2 && BpSouls(p)==2,"exchange retry recovers the exact token reward after "+mode);
            }
        }
        pkStart=pkNow=BpMonth(month++);var e=NewPk(runtime);Call(Pk,e,"Tick");
        for(int i=0;i<64;i++)BpNpc(BpPlayer(town,300),1);
        var excess=BpPlayer(town,300);BpNpc(excess,1);
        Check(PkEntries(excess)==0 && CtfCount("SELECT count(*) FROM revora_bp_admissions WHERE round_start=(SELECT max(round_start) FROM revora_bp_admissions)")==64,
            "BP division capacity rejects a sixty-fifth reservation without consuming its entry");
        var pending=BpPlayer(town,350);
        Query("CREATE FUNCTION fail_admit_bp() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF NEW.admitted_at IS NOT NULL THEN RAISE EXCEPTION ''fixture BP admission failure''; END IF; RETURN NEW; END';CREATE TRIGGER fail_admit_bp BEFORE UPDATE ON revora_pk_entries FOR EACH ROW EXECUTE FUNCTION fail_admit_bp()");
        BpNpc(pending,2);Check(Fenced(pending) && pending.Entity.MapID!=1002,"failed post-travel admission leaves a saved recovery location");
        Query("DROP TRIGGER fail_admit_bp ON revora_pk_entries;DROP FUNCTION fail_admit_bp()");NewPk(runtime);DemonReset(pending);Call(Pk,null,"OnLogin",pending);
        Check(pending.Entity.MapID==1002 && pending.Entity.OnDeath==null && !HasAchievement(pending,11114),"restart recovers a transfer whose final admission never committed");
    }
}
