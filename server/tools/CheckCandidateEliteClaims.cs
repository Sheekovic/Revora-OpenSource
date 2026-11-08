using System;
using System.Linq;
using System.Collections;
using System.Reflection;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static bool eliteClaimFixture;
    static string EliteClaimEventSnapshot(string run){return Convert.ToString(Sql(@"SELECT json_build_object(
'run',(SELECT row_to_json(r) FROM revora_epk_runs r WHERE r.run=@run),
'entries',(SELECT json_agg(e ORDER BY uid) FROM revora_epk_entries e WHERE e.run=@run),
'matches',(SELECT json_agg(m ORDER BY round,slot) FROM revora_epk_matches m WHERE m.run=@run),
'placements',(SELECT json_agg(p ORDER BY uid) FROM revora_epk_placements p WHERE p.run=@run))::text","run",run));}
    static void EliteClaimProcess(bool reload){
        eliteClaimFixture=true;const uint first=4294909801;
        bool absent=(bool)Sql("SELECT to_regclass('revora_elite_claim_probe') IS NULL");
        if(absent){Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN 4294909801 AND 4294909808")==0);
            for(uint uid=first;uid<first+8;uid++){nextUid=uid;Player(Kernel.Maps[1002]);Sql("UPDATE entities SET level=140,reborn=2 WHERE uid=@uid","uid",(long)uid);}
            Sql("CREATE TABLE revora_elite_claim_probe(run text,snapshot text);CREATE TABLE revora_elite_claim_players(uid bigint PRIMARY KEY,snapshot text)");
        }
        var players=new GameState[8];for(int n=0;n<8;n++){nextUid=first+(uint)n;var p=Player(Kernel.Maps[1002],true);ChiTable.Load(p);WayofHeroes.Load(p);p.LoadItemStats();players[n]=p;Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));}
        string run;
        if(absent){
            foreach(var p in players){foreach(uint stage in new uint[]{801,901,1002})NobilityClaimPacket(p,stage);Require(p.Inventory.Count==0 && !PirateFenced(p));}
            localEliteNow=new DateTime(2035,1,13,3,55,0,DateTimeKind.Utc);
            var type=Feature("ElitePkEvent");var arena=Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.NonPublic,null,
                new object[]{(Func<ushort,Map>)(id=>Kernel.Maps[id].MakeDynamicMap()),(Func<DateTime>)(()=>localEliteNow)},null);
            type.GetField("Current",Hidden).SetValue(null,arena);ElitePulse(arena,0);
            foreach(var p in players)HeartTalk(p,448582,3);
            var group=((IEnumerable)EliteField(arena,"groups")).Cast<object>().Single(g=>(int)EliteField(g,"Id")==3);run=(string)EliteField(group,"Run");
            Require((long)Sql("SELECT count(*) FROM revora_epk_entries WHERE run=@run","run",run)==8);
            ElitePulse(arena,300);
            for(int step=0;step<25 && !(bool)EliteField(group,"Done");step++){
                ElitePulse(arena,11);ElitePulse(arena,11);
                foreach(var match in ((IEnumerable)EliteField(group,"Matches")).Cast<object>().Where(m=>(bool)EliteField(m,"Imported") && EliteField(m,"Winner")==null).ToArray()){
                    var pair=EliteField(match,"Pair");var a=Kernel.GamePool[(uint)EliteField(pair,"Left")];var b=Kernel.GamePool[(uint)EliteField(pair,"Right")];
                    a.Attackable=b.Attackable=true;uint damage=100;
                    Revora.Game.Attacking.Handle.ReceiveAttack(a.Entity,b.Entity,new Attack(true){AttackType=Attack.Melee},ref damage,null);b.Entity.Die(a.Entity);
                }
                ElitePulse(arena,1);
            }
            Require((bool)EliteField(group,"Done") && (long)Sql("SELECT count(*) FROM revora_epk_placements WHERE run=@run","run",run)==8);
            Require((bool)Sql("SELECT state='complete' AND outcome='champion' FROM revora_epk_runs WHERE run=@run","run",run));
            Sql("INSERT INTO revora_elite_claim_probe VALUES(@run,@snapshot)","run",run,"snapshot",EliteClaimEventSnapshot(run));
        }else run=Convert.ToString(Sql("SELECT run FROM revora_elite_claim_probe"));
        foreach(var p in players){
            int place=Convert.ToInt32(Sql("SELECT place FROM revora_epk_placements WHERE run=@run AND uid=@uid","run",run,"uid",(long)p.Entity.UID));
            bool captured=(long)Sql("SELECT count(*) FROM revora_elite_claim_players WHERE uid=@uid","uid",(long)p.Entity.UID)==1;
            if(!captured){Require(!reload);
                uint[] stages={801,901,1002},prizes={3005127,3005128,3005129};int[] ranks={5,3,1};
                for(int n=0;n<3;n++){
                    uint stage=stages[n];System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,stage));Require(!PirateFenced(p));
                    Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=@stage AND NOT legacy","uid",(long)p.Entity.UID,"stage",(int)stage)==(place<=ranks[n]?1:0));
                    if(place<=ranks[n]){var reward=p.Inventory.Objects.Single();Require(reward.ID==prizes[n] && reward.Bound && reward.Plus==0 && reward.StackSize==1);SoulClaimUse(p,reward);}else Require(p.Inventory.Count==0);
                }
                Require(EntityTable.SaveEntity(p));Sql("INSERT INTO revora_elite_claim_players VALUES(@uid,@snapshot)","uid",(long)p.Entity.UID,"snapshot",NobilityClaimSnapshot(p));
            }
            Require(p.ChiPoints==(place==1?6000u:place<=3?3000u:1000u));
            string exact=NobilityClaimSnapshot(p);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_elite_claim_players WHERE uid=@uid","uid",(long)p.Entity.UID)));
            foreach(uint stage in new uint[]{801,901,1002})NobilityClaimPacket(p,stage);Require(exact==NobilityClaimSnapshot(p) && !PirateFenced(p));
        }
        Require(EliteClaimEventSnapshot(run)==Convert.ToString(Sql("SELECT snapshot FROM revora_elite_claim_probe")) && players.Sum(p=>(long)p.ChiPoints)==17000);
        System.Console.WriteLine("[career-process] career-elite-claims-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Elite claims did not close.");
    }
}
