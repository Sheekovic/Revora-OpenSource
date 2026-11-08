using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Npgsql;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;

internal static partial class PotencyHuntTests
{
    static readonly Type Epk=typeof(Entity).Assembly.GetType("Revora.Game.Features.ElitePkEvent");
    static readonly Type EpkStore=typeof(Entity).Assembly.GetType("Revora.Game.Features.ElitePkResults");
    static DateTime epkNow;
    static ushort epkMapId=36000;
    static object Field(object value,string name) { return value.GetType().GetField(name,Instance).GetValue(value); }
    static IEnumerable<object> EpkGroups(object e) { return ((IEnumerable)Field(e,"groups")).Cast<object>(); }
    static IEnumerable<object> EpkMatches(object e) { return EpkGroups(e).SelectMany(g=>((IEnumerable)Field(g,"Matches")).Cast<object>()); }
    static object NewEpk(string runtime) {
        Func<ushort,Map> make=id=> {
            Map m=World(runtime,id);Kernel.Maps.Remove(id);m.BaseID=id;m.ID=epkMapId++;Kernel.Maps[m.ID]=m;
            MapsTable.MapInformations[m.ID]=new MapsTable.MapInformation{ID=m.ID,BaseID=id,Status=2};return m;
        };
        var e=Activator.CreateInstance(Epk,BindingFlags.NonPublic|BindingFlags.Instance,null,new object[]{make,(Func<DateTime>)(()=>epkNow)},null);
        Epk.GetField("Current",Hidden).SetValue(null,e);return e;
    }
    static GameState EpkPlayer(Map town,byte level) {
        var p=ProgressPlayer(town,"");p.Entity.Level=level;p.ActiveNpc=448582;
        p.Entity.X=town.Npcs[448582].X;p.Entity.Y=town.Npcs[448582].Y;
        p.Entity.Titles=new System.Collections.Concurrent.ConcurrentDictionary<TitlePacket.Titles,DateTime>();
        return p;
    }
    static void EpkTick(object e,int seconds) { epkNow=epkNow.AddSeconds(seconds);Call(Epk,e,"Tick"); }
    static void EpkJoin(GameState p) { Npcs.GetDialog(new NpcRequest(true){NpcID=448582,OptionID=3},p); }
    static GameState EpkFighter(object match,string side) { return Kernel.GamePool[(uint)Field(Field(match,"Pair"),side)]; }
    static void EpkDamage(GameState a,GameState b,uint damage) {
        a.Attackable=b.Attackable=true;
        Revora.Game.Attacking.Handle.ReceiveAttack(a.Entity,b.Entity,new Attack(true){AttackType=Attack.Melee},ref damage,null);
    }
    static long EpkCount(string sql,params object[] values){
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new NpgsqlCommand(sql,c)){
            for(int n=0;n<values.Length;n+=2)q.Parameters.AddWithValue((string)values[n],values[n+1]);return Convert.ToInt64(q.ExecuteScalar());
        }}
    }
    static void VerifyElitePlacementBrackets(string runtime,Map town) {
        var zone=TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        foreach(int count in new[]{3,4,8,16,32}){
            var day=TimeZoneInfo.ConvertTimeFromUtc(epkNow,zone).Date.AddDays(7);
            epkNow=TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(day.AddHours(19).AddMinutes(55),DateTimeKind.Unspecified),zone);
            var e=NewEpk(runtime);Call(Epk,e,"Tick");var players=Enumerable.Range(0,count).Select(_=>EpkPlayer(town,140)).ToArray();foreach(var p in players)EpkJoin(p);
            var g=EpkGroups(e).Single(x=>(int)Field(x,"Id")==3);string run=(string)Field(g,"Run");EpkTick(e,300);int guard=0;bool audited=false;
            while(!(bool)Field(g,"Done")){
                if(++guard>16)throw new Exception("Elite placement bracket did not settle.");EpkTick(e,11);EpkTick(e,11);
                foreach(var m in EpkMatches(e).Where(m=>(bool)Field(m,"Imported") && Field(m,"Winner")==null).ToArray()){
                    var a=EpkFighter(m,"Left");var b=EpkFighter(m,"Right");EpkDamage(a,b,100);b.Entity.Die(a.Entity);
                }
                EpkTick(e,1);
                if(count==4 && !audited && ((ICollection)Field(g,"Alive")).Count==1 && ((ICollection)Field(g,"Matches")).Count==0){
                    int round=(int)Field(g,"Round");uint right=(uint)EpkCount("SELECT right_uid FROM revora_epk_matches WHERE run=@run AND round=@round AND slot=0","run",run,"round",round);
                    foreach(string fault in new[]{"duplicate","score","kind","round","bronze"}){
                        string mutate=fault=="duplicate"?"right_uid=left_uid":fault=="score"?"right_damage=9999,outcome='timeout'":fault=="kind"?"kind='unknown'":fault=="round"?"round=round+1":"left_uid=(SELECT winner FROM revora_epk_matches WHERE run=@run AND round=@round AND slot=0),winner=(SELECT winner FROM revora_epk_matches WHERE run=@run AND round=@round AND slot=0)";
                        uint bronzeLeft=(uint)EpkCount("SELECT left_uid FROM revora_epk_matches WHERE run=@run AND kind='bronze'","run",run);
                        Query("UPDATE revora_epk_matches SET "+mutate+" WHERE run=@run AND round=@round AND slot=@slot","run",run,"round",round,"slot",fault=="bronze"?1:0);
                        EpkTick(e,1);
                        Check(!(bool)Field(g,"Done") && EpkCount("SELECT count(*) FROM revora_epk_placements WHERE run=@run","run",run)==0,
                            "invalid "+fault+" result cannot create any placement or finish the bracket");
                        if(fault=="bronze")Query("UPDATE revora_epk_matches SET left_uid=@left,winner=@left WHERE run=@run AND kind='bronze'","left",(long)bronzeLeft,"run",run);
                        else Query("UPDATE revora_epk_matches SET right_uid=@right,right_damage=0,outcome='defeat',kind='elimination',round=@round WHERE run=@run AND slot=0 AND round=@broken",
                            "right",(long)right,"round",round,"run",run,"broken",fault=="round"?round+1:round);
                    }audited=true;
                }
            }
            long earned=EpkCount("SELECT count(*) FROM revora_epk_placements WHERE run=@run","run",run);
            Check(earned==Math.Min(count,8),"owned "+count+"-player combat bracket grants only its earned top-eight placements");
            Check(EpkCount("SELECT count(*) FROM revora_epk_placements WHERE run=@run AND place=3","run",run)==1,
                "owned "+count+"-player bracket resolves third place, including the three-player bronze bye");
            Check(players.All(p=>p.Entity.MapID==1002),"all finalists and bronze candidates return home after "+count+"-player results");
            Check(EpkCount("SELECT count(*) FROM revora_epk_placements p JOIN revora_epk_entries e USING(run,uid) WHERE p.run=@run AND p.signup_level=e.level AND p.bracket=3","run",run)==earned,
                "placement provenance matches each registered ordinary entrant: "+count);
        }
        foreach(string outcome in new[]{"forfeit","idle","uncontested-champion"}){
            var day=TimeZoneInfo.ConvertTimeFromUtc(epkNow,zone).Date.AddDays(7);
            epkNow=TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(day.AddHours(19).AddMinutes(55),DateTimeKind.Unspecified),zone);
            var e=NewEpk(runtime);Call(Epk,e,"Tick");var players=Enumerable.Range(0,outcome=="uncontested-champion"?3:4).Select(_=>EpkPlayer(town,140)).ToArray();foreach(var p in players)EpkJoin(p);
            var g=EpkGroups(e).Single(x=>(int)Field(x,"Id")==3);string run=(string)Field(g,"Run");EpkTick(e,300);int guard=0;
            while(!(bool)Field(g,"Done")){
                if(++guard>12)throw new Exception("Elite bronze attendance fixture did not settle.");EpkTick(e,11);EpkTick(e,11);
                foreach(var m in EpkMatches(e).Where(m=>(bool)Field(m,"Imported") && Field(m,"Winner")==null).ToArray()){
                    if(outcome=="uncontested-champion" && (int)Field(g,"Round")==2 && !(bool)Field(Field(m,"Pair"),"Bronze")){
                        EpkFighter(m,"Right").Entity.Teleport(1002,408,330);
                    }else if((bool)Field(Field(m,"Pair"),"Bronze")){
                        if(outcome=="forfeit")EpkFighter(m,"Right").Entity.Teleport(1002,408,330);else epkNow=(DateTime)Field(m,"End");
                    }else{var a=EpkFighter(m,"Left");var b=EpkFighter(m,"Right");EpkDamage(a,b,100);b.Entity.Die(a.Entity);}
                }EpkTick(e,1);
            }
            if(outcome=="uncontested-champion"){
                Check(EpkCount("SELECT winner FROM revora_epk_runs WHERE run=@run","run",run)==0 && EpkCount("SELECT count(*) FROM revora_epk_placements WHERE run=@run AND place=1","run",run)==0,
                    "finalist advancing only by byes and forfeits receives neither champion honor nor first prize");
                Check(EpkCount("SELECT count(*) FROM revora_epk_placements WHERE run=@run AND place IN(2,3)","run",run)==2,
                    "an unearned champion does not erase runner-up and bronze placements earned through actual combat");
            }else{
            Check(EpkCount("SELECT count(*) FROM revora_epk_matches WHERE run=@run AND kind='bronze' AND outcome=@reason","run",run,"reason",outcome)==1,
                "owned bronze bout records actual "+outcome+" outcome");
            Check(EpkCount("SELECT count(*) FROM revora_epk_placements WHERE run=@run AND place=3","run",run)==(outcome=="idle"?0:1)
                && EpkCount("SELECT count(*) FROM revora_epk_placements WHERE run=@run","run",run)==4,"bronze "+outcome+" preserves earned top-four places without inventing an idle third-place winner");
            }
            Check(players.All(p=>p.Entity.MapID==1002),"bronze "+outcome+" returns all participants home");
        }
        foreach(var day in new[]{new DateTime(2027,3,12),new DateTime(2027,11,5)}){
            epkNow=TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(day.AddHours(19).AddMinutes(55),DateTimeKind.Unspecified),zone);
            var e=NewEpk(runtime);Call(Epk,e,"Tick");var a=EpkPlayer(town,140);var b=EpkPlayer(town,140);EpkJoin(a);EpkJoin(b);EpkTick(e,300);EpkTick(e,11);
            var m=EpkMatches(e).Single(x=>(bool)Field(x,"Imported"));var left=EpkFighter(m,"Left");var right=EpkFighter(m,"Right");EpkDamage(left,right,100);right.Entity.Die(left.Entity);EpkTick(e,1);EpkTick(e,1);
            string run=(string)Field(EpkGroups(e).Single(g=>(int)Field(g,"Id")==3),"Run");long hours=day.Month==11?169:167;
            Check(EpkCount("SELECT count(*) FROM revora_epk_placements p JOIN revora_epk_runs r USING(run) WHERE p.run=@run AND extract(epoch FROM(p.expires_at-r.starts_at))=@seconds","run",run,"seconds",hours*3600)==2,
                "next-week claim deadline follows Pacific clock across daylight-saving transition: "+day.ToString("yyyy-MM-dd"));
        }
    }
    static void VerifyElitePk(string runtime,Map town) {
        Query("ALTER TABLE entities ADD COLUMN x integer DEFAULT 0, ADD COLUMN y integer DEFAULT 0, ADD COLUMN mapid integer DEFAULT 1002, ADD COLUMN level integer DEFAULT 140, ADD COLUMN my_title integer DEFAULT 0");
        Query("CREATE TABLE titles (LIKE public.titles INCLUDING ALL)");StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
        ConquerItem.ItemUID=new Counter(3950000);
        Query("CREATE TABLE npcs (LIKE public.npcs INCLUDING ALL)");
        Query("INSERT INTO npcs SELECT * FROM public.npcs WHERE id=448582");
        Check(CtfCount("SELECT count(*) FROM npcs")==1,"installed Elite PK Envoy exists");
        Call(Epk,null,"PlaceEnvoy");
        Check(town.Npcs.ContainsKey(448582) && !Revora.Game.Features.TwinCityPlaza.IsClosed(448582),"restored Envoy is reachable in Twin City and its desk is open");
        foreach(var boundary in new[]{Tuple.Create(1,0),Tuple.Create(99,0),Tuple.Create(100,1),Tuple.Create(119,1),Tuple.Create(120,2),Tuple.Create(129,2),Tuple.Create(130,3),Tuple.Create(140,3)})
            Check((int)Call(Epk,null,"Bracket",boundary.Item1)==boundary.Item2,"6711 level bracket boundary "+boundary.Item1);
        for(int n=2;n<=32;n++) {
            var pairs=((IEnumerable)Call(Epk,null,"Pairings",Enumerable.Range(1,n).Select(i=>(uint)i).ToArray())).Cast<object>().ToArray();
            var ids=pairs.SelectMany(p=>new[]{(uint)Field(p,"Left"),(uint)Field(p,"Right")}).Where(i=>i!=0).ToArray();
            Check(ids.Length==n && ids.Distinct().Count()==n && (pairs.Length&(pairs.Length-1))==0,"seeded bracket includes every entrant once: "+n);
        }
        // Last Friday keeps real seven-day rewards within the current read clock.
        DateTime local=TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time")).Date;
        while(local.DayOfWeek!=DayOfWeek.Friday)local=local.AddDays(-1);
        epkNow=TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local.AddHours(19).AddMinutes(55),DateTimeKind.Unspecified),TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"));
        var e=NewEpk(runtime);Server.Thread=(Revora.Thread)FormatterServices.GetUninitializedObject(typeof(Revora.Thread));
        Call(Epk,e,"Tick");
        Check(CtfCount("SELECT count(*) FROM revora_epk_runs")==4,"one registration run opens for each level bracket");
        var opened=EpkGroups(e).First();string openedRun=(string)Field(opened,"Run");
        Check((bool)Call(EpkStore,null,"Open",openedRun,openedRun.Split('-')[1],(int)Field(opened,"Id"),(DateTime)Field(opened,"Start"),(DateTime)Field(opened,"Deadline")),
            "uncertain registration-window commit can be retried without losing the run");
        Parallel.For(0,8,i=>Call(Epk,e,"Tick"));Check(CtfCount("SELECT count(*) FROM revora_epk_runs")==4,"parallel ticks do not duplicate registration windows");
        var high=Enumerable.Range(0,5).Select(i=>EpkPlayer(town,140)).ToArray();var watch=EpkPlayer(town,140);
        high[0].Entity.X=20;high[0].Entity.Y=20;EpkJoin(high[0]);
        Check(high[0].Entity.MapID==1002,"forged distant NPC signup is rejected");
        high[0].Entity.X=town.Npcs[448582].X;high[0].Entity.Y=town.Npcs[448582].Y;
        high[0].Fake=true;EpkJoin(high[0]);Check(high[0].Entity.MapID==1002,"fake adventurer cannot enter Elite PK");high[0].Fake=false;
        foreach(var p in high)EpkJoin(p);
        Check(high.All(p=>p.Entity.MapID!=1002 && !p.Fake && p.Account.State==AccountTable.AccountState.Player),"five ordinary players register through the actual NPC dispatcher");
        EpkJoin(high[0]);Check(CtfCount("SELECT count(*) FROM revora_epk_entries")==5,"duplicate signup cannot add another bracket place");
        var lower=new[]{EpkPlayer(town,129),EpkPlayer(town,129)};
        var absent=new[]{EpkPlayer(town,119),EpkPlayer(town,119)};
        var idle=new[]{EpkPlayer(town,99),EpkPlayer(town,99)};
        foreach(var p in lower.Concat(absent).Concat(idle))EpkJoin(p);
        absent[1].Entity.Teleport(1002,408,330);
        EpkTick(e,300);
        var first=EpkMatches(e).First(m=>(bool)Field(m,"Imported"));var a=EpkFighter(first,"Left");var b=EpkFighter(first,"Right");
        Check(EpkMatches(e).Count(m=>(string)Field(m,"Reason")=="bye")==3,"five-player bracket grants three first-round byes");
        Check((bool)Call(Epk,null,"BlockAttack",a.Entity,b.Entity),"ten-second preparation blocks combat");
        first.GetType().GetField("Imported",Instance).SetValue(first,false);
        Call(Epk,e,"Travel",b,(Map)Field(e,"waiting"),false);EpkTick(e,1);
        Check(b.Entity.MapID==a.Entity.MapID && (bool)Field(first,"Imported") && Field(first,"Winner")==null,
            "partially completed server transfer retries without forfeiting a fighter");
        Npcs.GetDialog(new NpcRequest(true){NpcID=448582,OptionID=4},watch);
        Check(watch.Entity.MapID==a.Entity.MapID && (bool)Call(Epk,null,"BlockSpell",watch),"ordinary spectator enters a duel with spell use blocked");
        EpkTick(e,11);
        Check(!Revora.Game.Attacking.Handle.CanAttack(a.Entity,watch.Entity,null,true)
            && !Revora.Game.Attacking.Handle.CanAttack(watch.Entity,a.Entity,null,true),"spectators cannot attack or be attacked by fighters");
        uint hp=b.Entity.Hitpoints;EpkDamage(watch,b,500);
        Check(b.Entity.Hitpoints==hp,"direct receive path also rejects spectator damage");
        EpkDamage(a,b,100);
        Check(b.Entity.Hitpoints<hp && (long)Field(first,"LeftDamage")>0,"actual accepted combat updates HP and authoritative damage score");
        b.Entity.Hitpoints=200;long beforeLarge=(long)Field(first,"LeftDamage");EpkDamage(a,b,150);
        Check(b.Entity.Hitpoints==50 && (long)Field(first,"LeftDamage")==beforeLarge+150,"physical score uses damage dealt before HP subtraction");
        long score=(long)Field(first,"LeftDamage");
        Call(Epk,null,"Damage",watch.Entity,b.Entity,100u);
        Check((long)Field(first,"LeftDamage")==score,"spectator cannot forge a damage score");
        // Make finalization fail after a genuine server death. The frozen duel retries.
        b.Entity.Die(a.Entity);
        Query("ALTER TABLE revora_epk_matches ADD CONSTRAINT epk_test_failure CHECK(winner IS NULL) NOT VALID");
        EpkTick(e,1);
        Check(CtfCount("SELECT count(*) FROM revora_epk_matches WHERE right_uid<>0 AND winner IS NOT NULL")==0
            && (bool)Call(Epk,null,"BlockAttack",a.Entity,b.Entity),"failed match persistence freezes combat without installing a result");
        Query("ALTER TABLE revora_epk_matches DROP CONSTRAINT epk_test_failure");EpkTick(e,1);
        Check((bool)Field(first,"Exported") && b.Entity.MapID==1002 && watch.Entity.MapID==1002,"retry commits once and returns loser and spectator home");
        Query("ALTER TABLE revora_epk_runs ADD CONSTRAINT epk_final_failure CHECK(bracket<>3 OR state<>'complete') NOT VALID");
        int rounds=0;bool finalRollback=false;
        while(EpkGroups(e).Any(g=>(int)Field(g,"Id")==3 && !(bool)Field(g,"Done"))) {
            if(++rounds>64)throw new Exception("Elite PK bracket did not settle.");
            EpkTick(e,11);EpkTick(e,11);
            foreach(var match in EpkMatches(e).Where(m=>(bool)Field(m,"Imported") && Field(m,"Winner")==null).ToArray()) {
                var left=EpkFighter(match,"Left");var right=EpkFighter(match,"Right");EpkDamage(left,right,100);right.Entity.Die(left.Entity);
            }
            EpkTick(e,1);
            if(!finalRollback && CtfCount("SELECT count(*) FROM revora_epk_matches m JOIN revora_epk_runs r USING(run) WHERE r.bracket=3 AND m.round=3 AND m.kind='elimination' AND m.winner IS NOT NULL")==1) {
                EpkTick(e,1);
                Check(CtfCount("SELECT count(*) FROM revora_event_awards")==0 && CtfCount("SELECT count(*) FROM revora_epk_placements")==0 && CtfCount("SELECT winner FROM revora_epk_runs WHERE bracket=3")==0,
                    "failed championship transaction rolls back both winner and wing award");
                Query("ALTER TABLE revora_epk_runs DROP CONSTRAINT epk_final_failure");
                Query("CREATE FUNCTION suppress_epk_place() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_epk_place BEFORE INSERT ON revora_epk_placements FOR EACH ROW EXECUTE FUNCTION suppress_epk_place()");
                try{EpkTick(e,1);Check(CtfCount("SELECT count(*) FROM revora_epk_placements")==0 && CtfCount("SELECT count(*) FROM revora_event_awards")==0,"silent placement failure rolls back all ranks and champion honor");}
                finally{Query("DROP TRIGGER suppress_epk_place ON revora_epk_placements;DROP FUNCTION suppress_epk_place()");}
                Query("CREATE FUNCTION suppress_epk_finish() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF NEW.state=''complete'' THEN RETURN NULL; END IF; RETURN NEW; END';CREATE TRIGGER suppress_epk_finish BEFORE UPDATE ON revora_epk_runs FOR EACH ROW EXECUTE FUNCTION suppress_epk_finish()");
                try{EpkTick(e,1);Check(CtfCount("SELECT count(*) FROM revora_epk_placements")==0 && CtfCount("SELECT count(*) FROM revora_event_awards")==0,"silent final-state failure rolls back placements and honor together");}
                finally{Query("DROP TRIGGER suppress_epk_finish ON revora_epk_runs;DROP FUNCTION suppress_epk_finish()");}
                finalRollback=true;EpkTick(e,1);
            }
        }
        uint champion=(uint)CtfCount("SELECT winner FROM revora_epk_runs WHERE bracket=3");
        Check(CtfCount("SELECT count(*) FROM revora_epk_matches WHERE kind='bronze' AND outcome='defeat'")==1,"semifinal losers fight an owned bronze bout instead of both being sent home");
        Check(CtfCount("SELECT count(*) FROM revora_epk_placements WHERE bracket=3")==5
            && CtfCount("SELECT count(DISTINCT place) FROM revora_epk_placements WHERE bracket=3")==5,"five-player bracket saves champion, runner-up, bronze, fourth and top-eight placements");
        Check(CtfCount("SELECT count(*) FROM revora_epk_placements p JOIN revora_epk_matches m USING(run) WHERE p.place=3 AND m.kind='bronze' AND p.uid=m.winner")==1,"third-place entitlement belongs to the actual bronze winner");
        Check(CtfCount("SELECT count(*) FROM revora_epk_placements WHERE (place=1 AND pack_type=720729) OR (place=2 AND pack_type=720728) OR (place=3 AND pack_type=720727) OR (place IN(4,5) AND pack_type=720726)")==5,"each saved placement identifies its correct highest-bracket prize pack");
        var winner=Kernel.GamePool[champion];EvidenceRefresh(winner);RefreshTitles(winner);
        Check(champion!=0 && winner.Entity.MapID==1002,"full ordinary-player elimination bracket crowns one saved champion");
        Check(OwnedTitles(winner).ContainsKey(60010020) && winner.Entity.Titles.ContainsKey(TitlePacket.Titles.ElitePKChamption_High),"verified highest-bracket winner owns the wing and champion status");
        Check(high.Where(p=>p!=winner).All(p=>!EligibleTitles(p).Contains(60010020)),"losing players cannot claim champion wings");
        winner.Entity.WTitles=null;StorageManager.Load();new TitleStorage().Login(winner);
        Check(OwnedTitles(winner).ContainsKey(60010020),"champion wings survive wardrobe and evidence reload");
        TitleRequest(60010020,TitleStorage.Action.Equip).Handle(winner);
        Check(winner.Entity.EquippedWing==60010020,"ordinary champion can equip the earned wing");
        long expiry=Scalar("SELECT extract(epoch FROM expires_at)::bigint FROM revora_event_awards WHERE uid=@uid AND source='elite-pk-local-v1'",champion);
        winner.ActiveNpc=448582;winner.Entity.X=town.Npcs[448582].X;winner.Entity.Y=town.Npcs[448582].Y;
        for(int i=0;i<8;i++)Npcs.GetDialog(new NpcRequest(true){NpcID=448582,OptionID=5},winner);
        EpkTick(e,1);
        foreach(ushort id in new[]{PacketID.ElitePkRank,PacketID.ElitePKBrackets,PacketID.ElitePKWager,PacketID.ElitePKWagersList}) {
            var packet=new byte[12];Array.Copy(System.BitConverter.GetBytes((ushort)4),packet,2);Array.Copy(System.BitConverter.GetBytes(id),0,packet,2,2);
            Array.Copy(System.BitConverter.GetBytes(Revora.Network.PacketHandler.ClientSeal),0,packet,4,8);
            Revora.Network.PacketHandler.HandlePacket(packet,winner);
        }
        Check(CtfCount("SELECT count(*) FROM revora_event_awards")==1,"disabled native bracket and wager requests cannot mutate tournament rewards");
        Check(Scalar("SELECT extract(epoch FROM expires_at)::bigint FROM revora_event_awards WHERE uid=@uid AND source='elite-pk-local-v1'",champion)==expiry,"repeated NPC claims never extend the seven-day expiry");
        Check(CtfCount("SELECT count(*) FROM revora_event_awards WHERE expires_at=earned_at+interval '7 days'")==1,"reward duration is exactly seven days from the result");
        var group=EpkGroups(e).First(g=>(int)Field(g,"Id")==3);string run=(string)Field(group,"Run");
        Parallel.For(0,8,i=>Call(EpkStore,null,"Finish",run,champion,epkNow.AddDays(1),false));
        Check(Scalar("SELECT extract(epoch FROM expires_at)::bigint FROM revora_event_awards WHERE uid=@uid AND source='elite-pk-local-v1'",champion)==expiry,"duplicate finalization cannot replay or extend a reward");
        DateTime expiration=new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc).AddSeconds(expiry);
        Call(typeof(Achievement),winner.Entity.MyAchievement,"Refresh",expiration,true);Call(Epk,null,"SynchronizeChampion",winner,expiration);
        Check(!winner.Entity.Titles.ContainsKey(TitlePacket.Titles.ElitePKChamption_High)
            && ((int[])Call(typeof(Achievement),winner.Entity.MyAchievement,"AwardKeys",expiration)).Length==0,"champion status and wing entitlement expire together");
        RefreshTitles(winner);Check(winner.Entity.EquippedWing==0,"expired wing is removed from equipped appearance");
        epkNow=(DateTime)Call(Epk,null,"Start",epkNow,2);EpkTick(e,1);EpkTick(e,11);
        var lowerMatch=EpkMatches(e).Single(m=>(int)Field(Field(m,"Group"),"Id")==2);
        var lowerLeft=EpkFighter(lowerMatch,"Left");var lowerRight=EpkFighter(lowerMatch,"Right");
        EpkDamage(lowerLeft,lowerRight,120);EpkDamage(lowerRight,lowerLeft,40);
        epkNow=(DateTime)Field(lowerMatch,"End");Call(Epk,e,"Tick");EpkTick(e,1);
        Check(CtfCount("SELECT winner FROM revora_epk_runs WHERE bracket=2")==lowerLeft.Entity.UID,"timeout resolves using actual damage scores");
        Check(CtfCount("SELECT count(*) FROM revora_epk_placements WHERE bracket=2")==2
            && CtfCount("SELECT count(*) FROM revora_epk_placements WHERE bracket=2 AND signup_level=129 AND pack_type IN(720724,720725)")==2,
            "contested lower-bracket final saves two correctly leveled prize entitlements");
        Check(lower.All(p=>!EligibleTitles(p).Contains(60010020)),"a legitimate lower-bracket champion does not receive highest-bracket wings");
        Check(CtfCount("SELECT count(*) FROM revora_epk_matches m JOIN revora_epk_runs r USING(run) WHERE bracket=1 AND m.outcome='no-show'")==1
            && CtfCount("SELECT winner FROM revora_epk_runs WHERE bracket=1")==0,"leaving the hall produces a no-show and cannot create an uncontested champion");
        var idleMatch=EpkMatches(e).Single(m=>(int)Field(Field(m,"Group"),"Id")==0);
        epkNow=(DateTime)Field(idleMatch,"End");Call(Epk,e,"Tick");EpkTick(e,1);
        Check(CtfCount("SELECT count(*) FROM revora_epk_matches m JOIN revora_epk_runs r USING(run) WHERE bracket=0 AND m.outcome='idle'")==1
            && CtfCount("SELECT winner FROM revora_epk_runs WHERE bracket=0")==0,"two idle opponents cannot farm a title by waiting out the match");
        Check(CtfCount("SELECT count(*) FROM revora_epk_placements WHERE bracket IN(0,1)")==0,"idle and no-show-only runs do not create prize entitlements");
        Check(CtfCount("SELECT count(*) FROM revora_epk_placements WHERE expires_at<=earned_at OR signup_level<=0")==0,"placement receipts retain valid signup levels and future claim deadlines");
        EpkTick(e,1800);epkNow=epkNow.AddDays(7).Date; // Restart test uses the next actual Friday window below.
        epkNow=TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local.AddDays(7).AddHours(19).AddMinutes(55),DateTimeKind.Unspecified),TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"));
        Call(Epk,e,"Tick");var restartA=EpkPlayer(town,140);var restartB=EpkPlayer(town,140);EpkJoin(restartA);EpkJoin(restartB);EpkTick(e,300);
        Check(EpkMatches(e).Any(m=>(bool)Field(m,"Imported")),"restart fixture interrupts an actual in-progress duel");
        var restarted=NewEpk(runtime);Call(Epk,restarted,"Tick");
        Check(CtfCount("SELECT count(*) FROM revora_epk_placements")==7,"restart preserves completed placements without awarding interrupted runs");
        Check(CtfCount("SELECT count(*) FROM revora_epk_runs WHERE outcome='server-restart'")==4,"restart cancels registration and cannot reopen the same dated slots");
        VerifyElitePlacementBrackets(runtime,town);
    }
}
