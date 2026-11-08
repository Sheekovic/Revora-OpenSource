using System;
using System.Linq;
using System.Collections;
using System.Reflection;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;

internal static partial class CheckCandidateArenaLifecycle {
    static bool retainedEliteCombatFixture;
    static string RetainedEliteCombatSnapshot(GameState p,GameState recipient,string run) {
        return Convert.ToString(Sql(@"SELECT json_build_object('career',@career::json,'event',@event::json,
'claim',(SELECT row_to_json(r) FROM revora_epk_prize_claims r WHERE run=@run AND uid=@uid))::text",
"career",MainTenthSnapshot(p,recipient),"event",EliteClaimEventSnapshot(run),"run",run,"uid",(long)p.Entity.UID));
    }
    static void RetainedEliteCombatProcess(string phase) {
        bool win=phase.EndsWith("win"),claim=phase.EndsWith("claim");const uint opponentUid=4294918801;
        careerRitesFixture=true;retainedEliteCombatFixture=true;combinedCollectionFixture=true;nextUid=CareerUid;
        var p=Player(Kernel.Maps[1002],true,1003,true,true);combinedCollectionFixture=false;
        SkillTable.LoadSpells(p);SkillTable.LoadProficiencies(p);SubClassTable.Load(p.Entity);Revora.Database.KnownPersons.LoadMentor(p);
        new CoatStorage().Login(p);new TitleStorage().Login(p);p.LoadItemStats();ChiTable.Load(p);WayofHeroes.Load(p);EntityVariableTable.Load(p);Call(typeof(Revora.Database.JiangHu),"LoadClient",p);
        nextUid=4294918104;var recipient=Player(Kernel.Maps[1002],true);recipient.LoadItemStats();ChiTable.Load(recipient);WayofHeroes.Load(recipient);
        nextUid=4294918206;var storage=Player(Kernel.Maps[1002],true);storage.LoadItemStats();
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        bool absent=(bool)Sql("SELECT to_regclass('revora_retained_elite_combat_probe') IS NULL");
        if(absent) {
            Require(win && MainTenthSnapshot(p,recipient)==Convert.ToString(Sql("SELECT snapshot FROM revora_main_tenth_probe")));
            Require((long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)opponentUid)==0);
            nextUid=opponentUid;Player(Kernel.Maps[1002]);Sql("UPDATE entities SET level=140,reborn=2 WHERE uid=@uid","uid",(long)opponentUid);
            Sql("CREATE TABLE revora_retained_elite_combat_probe(run text,step int,snapshot text,cp_before bigint,hits int);INSERT INTO revora_retained_elite_combat_probe VALUES(NULL,0,NULL,NULL,0)");
        }
        nextUid=opponentUid;var opponent=Player(Kernel.Maps[1002],true);opponent.LoadItemStats();
        Require(!opponent.Fake && opponent.Account.State==AccountTable.AccountState.Player && p.Entity.Level>=130 && opponent.Entity.Level>=130);
        localEliteNow=new DateTime(2035,2,10,3,55,0,DateTimeKind.Utc);var type=Feature("ElitePkEvent");
        var arena=Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{(Func<ushort,Map>)(id=>Kernel.Maps[id].MakeDynamicMap()),(Func<DateTime>)(()=>localEliteNow)},null);
        type.GetField("Current",Hidden).SetValue(null,arena);
        string run=Convert.ToString(Sql("SELECT run FROM revora_retained_elite_combat_probe"));
        if(win) {
            if(!absent) {
                Require((int)Sql("SELECT step FROM revora_retained_elite_combat_probe")==0 && string.IsNullOrEmpty(run));
                Require((long)Sql("SELECT count(*) FROM revora_epk_entries WHERE cycle='20350202' AND uid=ANY(@uids)","uids",new long[]{CareerUid,opponentUid})==0);
                Require(p.Inventory.Count==16 && (long)Sql("SELECT count(*) FROM revora_trade_receipts WHERE id=ANY(@ids) AND left_uid=@left AND right_uid=@right AND left_gold=0 AND right_gold=0 AND left_cp=0 AND right_cp=0",
                    "ids",new[]{Guid.Parse("4340529b-370c-4613-90de-a99bc5ea2364"),Guid.Parse("51656094-f503-4214-8e01-ac39e595d8dd"),Guid.Parse("d157b93f-cdcb-4a2b-8ce9-2c72dce2e31a")},"left",(long)CareerUid,"right",4294918206L)==3);
            }
            MainCareerRoom(p,storage,16);p.Entity.Teleport(1002,408,330);opponent.Entity.Teleport(1002,408,330);ElitePulse(arena,0);
            HeartTalk(p,448582,3);HeartTalk(opponent,448582,3);
            var group=((IEnumerable)EliteField(arena,"groups")).Cast<object>().Single(g=>(int)EliteField(g,"Id")==3);run=(string)EliteField(group,"Run");
            Sql("UPDATE revora_retained_elite_combat_probe SET run=@run","run",run);
            Require((long)Sql("SELECT count(*) FROM revora_epk_entries WHERE run=@run","run",run)==2);
            ElitePulse(arena,300);int hits=0;
            for(int step=0;step<20 && !(bool)EliteField(group,"Done");step++) {
                ElitePulse(arena,11);ElitePulse(arena,11);
                foreach(var match in ((IEnumerable)EliteField(group,"Matches")).Cast<object>().Where(m=>(bool)EliteField(m,"Imported") && EliteField(m,"Winner")==null).ToArray()) {
                    Require(p.Entity.MapID==opponent.Entity.MapID);p.Attackable=opponent.Attackable=true;
                    p.CantAttack=Time32.Now.AddSeconds(-1);p.Entity.X=(ushort)(opponent.Entity.X+1);p.Entity.Y=opponent.Entity.Y;p.Screen.Add(opponent.Entity);
                    uint initial=opponent.Entity.Hitpoints;
                    for(int n=0;n<200 && !opponent.Entity.Dead && p.Entity.MapID==opponent.Entity.MapID;n++) {
                        p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);
                        Attack.HandleAttack(new Attack(true){Attacker=p.Entity.UID,Attacked=opponentUid,X=opponent.Entity.X,Y=opponent.Entity.Y,AttackType=Attack.Melee},p);hits++;
                    }
                    Console.WriteLine("[retained-elite] initial-hp="+initial+" remaining-hp="+opponent.Entity.Hitpoints+" hits="+hits);
                    Require(opponent.Entity.Dead || EliteField(match,"Winner")!=null);
                }
                ElitePulse(arena,1);
            }
            Require(hits>0 && (bool)EliteField(group,"Done"));
            Require((bool)Sql("SELECT state='complete' AND winner=@uid AND outcome='champion' FROM revora_epk_runs WHERE run=@run","run",run,"uid",(long)CareerUid));
            Require((long)Sql("SELECT count(*) FROM revora_epk_matches WHERE run=@run AND winner=@uid AND outcome='defeat' AND left_damage+right_damage>0","run",run,"uid",(long)CareerUid)>0);
            Require((long)Sql("SELECT count(*) FROM revora_epk_prize_claims WHERE run=@run","run",run)==0);
            Require(EntityTable.SaveEntity(p) && EntityTable.SaveEntity(opponent) && EntityTable.SaveEntity(storage));
            Sql("UPDATE revora_retained_elite_combat_probe SET step=1,hits=@hits,cp_before=@cp,snapshot=@snapshot","hits",hits,"cp",(long)p.Entity.ConquerPoints,"snapshot",RetainedEliteCombatSnapshot(p,recipient,run));
        } else {
            localEliteNow=localEliteNow.AddHours(1);
            Require(RetainedEliteCombatSnapshot(p,recipient,run)==Convert.ToString(Sql("SELECT snapshot FROM revora_retained_elite_combat_probe")));
            if(claim) {
                Require((int)Sql("SELECT step FROM revora_retained_elite_combat_probe")==1);
                uint[] before=p.Inventory.Objects.Select(i=>i.UID).ToArray();HeartTalk(p,448582,6);HeartTalk(p,448582,7);
                Require(p.Entity.ConquerPoints==Convert.ToUInt32(Sql("SELECT cp_before FROM revora_retained_elite_combat_probe"))+5000000u);
                var rewards=p.Inventory.Objects.Where(i=>!before.Contains(i.UID)).ToArray();
                Require(rewards.Length==24 && rewards.Count(i=>i.Bound && i.Days==0 && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)==1);
                var saved=(long[])Sql("SELECT rewards FROM revora_epk_prize_claims WHERE run=@run AND uid=@uid","run",run,"uid",(long)CareerUid);
                Require(saved.OrderBy(i=>i).SequenceEqual(rewards.Select(i=>(long)i.UID).OrderBy(i=>i)));
                Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_retained_elite_combat_probe SET step=2,snapshot=@snapshot","snapshot",RetainedEliteCombatSnapshot(p,recipient,run));
            }
            Require((int)Sql("SELECT step FROM revora_retained_elite_combat_probe")==2);
            string once=RetainedEliteCombatSnapshot(p,recipient,run);HeartTalk(p,448582,7);HeartTalk(p,448582,6);HeartTalk(p,448582,7);
            Require(once==RetainedEliteCombatSnapshot(p,recipient,run) && !PirateFenced(p));
        }
        Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11123","uid",(long)CareerUid)==1);
        Console.WriteLine("[career-process] "+phase+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Retained Elite combat did not close.");
    }
}
