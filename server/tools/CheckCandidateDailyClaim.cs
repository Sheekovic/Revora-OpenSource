using System;
using System.Linq;
using Npgsql;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;

internal static partial class CheckCandidateArenaLifecycle {
    static string DailyClaimSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object(
'base',@snapshot::json,'blessing',(SELECT heavenblessingtime FROM entities WHERE uid=@uid),
'study',(SELECT studypoints FROM entities WHERE uid=@uid),'rvp',(SELECT revorapoint FROM entities WHERE uid=@uid),
'worship',(SELECT json_agg(r ORDER BY day) FROM revora_worship_claims r WHERE uid=@uid),
'price',(SELECT json_agg(r ORDER BY day) FROM revora_price_runs r WHERE uid=@uid),
'souls',(SELECT json_agg(r ORDER BY day) FROM revora_soul_runs r WHERE uid=@uid),
'rare',(SELECT json_agg(r ORDER BY day) FROM revora_rare_runs r WHERE uid=@uid),
'magnolia',(SELECT json_agg(r ORDER BY day) FROM revora_magnolia_runs r WHERE uid=@uid),
'exorcism',(SELECT json_agg(r ORDER BY id) FROM revora_exorcism_daily r WHERE uid=@uid),
'bead',(SELECT json_agg(r ORDER BY id) FROM revora_soul_ball_runs r WHERE uid=@uid),
'stones',(SELECT json_agg(r ORDER BY id) FROM revora_praying_stone_uses r WHERE uid=@uid))::text",
"snapshot",NobilityClaimSnapshot(p),"uid",(long)p.Entity.UID));}
    static void DailyPredicateChecks(uint uid){
        using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
            Func<bool> eligible=()=>(bool)Call(Feature("WayOfHeroesClaims"),"CompletedDailies",c,t,uid);
            Require(eligible());
            // Transaction-local copies of earned receipts isolate each missing/invalid leg.
            // No production or retained earning receipt is modified by these boundary checks.
            using(var q=new NpgsqlCommand(@"CREATE TEMP TABLE revora_worship_claims ON COMMIT DROP AS SELECT * FROM public.revora_worship_claims WHERE uid=@uid;
CREATE TEMP TABLE revora_price_runs ON COMMIT DROP AS SELECT * FROM public.revora_price_runs WHERE uid=@uid;
CREATE TEMP TABLE revora_soul_runs ON COMMIT DROP AS SELECT * FROM public.revora_soul_runs WHERE uid=@uid;
CREATE TEMP TABLE revora_rare_runs ON COMMIT DROP AS SELECT * FROM public.revora_rare_runs WHERE uid=@uid;
CREATE TEMP TABLE revora_magnolia_runs ON COMMIT DROP AS SELECT * FROM public.revora_magnolia_runs WHERE uid=@uid;
CREATE TEMP TABLE revora_exorcism_daily ON COMMIT DROP AS SELECT * FROM public.revora_exorcism_daily WHERE uid=@uid;
CREATE TEMP TABLE revora_soul_ball_runs ON COMMIT DROP AS SELECT * FROM public.revora_soul_ball_runs WHERE uid=@uid;",c,t)){q.Parameters.AddWithValue("uid",(long)uid);q.ExecuteNonQuery();}
            Require(eligible());
            foreach(string sql in new[]{
                "DELETE FROM pg_temp.revora_worship_claims", "DELETE FROM pg_temp.revora_price_runs", "DELETE FROM pg_temp.revora_soul_runs", "DELETE FROM pg_temp.revora_rare_runs", "DELETE FROM pg_temp.revora_magnolia_runs", "DELETE FROM pg_temp.revora_exorcism_daily", "DELETE FROM pg_temp.revora_soul_ball_runs",
                "UPDATE pg_temp.revora_worship_claims SET legacy=true", "UPDATE pg_temp.revora_price_runs SET legacy=true", "UPDATE pg_temp.revora_soul_runs SET state='active'", "UPDATE pg_temp.revora_rare_runs SET completed=false", "UPDATE pg_temp.revora_magnolia_runs SET planted_seed=NULL", "UPDATE pg_temp.revora_exorcism_daily SET state='victory'", "UPDATE pg_temp.revora_soul_ball_runs SET completed_day=''",
                "UPDATE pg_temp.revora_rare_runs SET legacy=true", "UPDATE pg_temp.revora_magnolia_runs SET legacy=true", "UPDATE pg_temp.revora_exorcism_daily SET legacy=true", "UPDATE pg_temp.revora_soul_ball_runs SET legacy=true"}){
                t.Save("boundary");using(var q=new NpgsqlCommand(sql,c,t))q.ExecuteNonQuery();Require(!eligible());t.Rollback("boundary");Require(eligible());
            }
            Require(!(bool)Call(Feature("WayOfHeroesClaims"),"CompletedDailies",c,t,4294917801u));t.Rollback();
        }}
    }
    static void DailyClaimProcess(string phase){
        bool reload=phase.EndsWith("reload");careerRitesFixture=true;combinedCollectionFixture=true;nextUid=CareerUid;
        var p=Player(Kernel.Maps[1002],true,1003,true,true);combinedCollectionFixture=false;
        SkillTable.LoadSpells(p);SkillTable.LoadProficiencies(p);SubClassTable.Load(p.Entity);EntityVariableTable.Load(p);Revora.Database.KnownPersons.LoadMentor(p);
        new CoatStorage().Login(p);new TitleStorage().Login(p);p.LoadItemStats();ChiTable.Load(p);WayofHeroes.Load(p);p.Quests.Load();
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_daily_claim_probe') IS NULL")){
            Require(!reload);Sql("CREATE TABLE revora_daily_claim_probe(snapshot text,reward bigint,blessing bigint);INSERT INTO revora_daily_claim_probe VALUES(NULL,NULL,NULL)");
        }
        var services=new[]{Feature("AncestorWorship"),Feature("DailyPriceQuest"),Feature("DailyRareQuest")};
        var clocks=services.Select(s=>s.GetField("Clock",Hidden)).ToArray();var old=clocks.Select(f=>f.GetValue(null)).ToArray();
        DateTime now=new DateTime(2088,1,5,20,0,0,DateTimeKind.Utc);
        try{foreach(var f in clocks)f.SetValue(null,(Func<DateTime>)(()=>now));
            if(Sql("SELECT snapshot FROM revora_daily_claim_probe")==DBNull.Value){
                Require(!reload);
                // Four other daily kinds were previously earned by this retained career.
                Action refuse=()=>{string before=DailyClaimSnapshot(p);NobilityClaimPacket(p,409);Require(before==DailyClaimSnapshot(p));};
                Console.WriteLine("[daily-claim] bag="+p.Inventory.Count+" level="+p.Entity.Level);
                // This long-lived career has filled all public warehouses. Use its existing
                // ordinary trade helper to retain items with the beginner fixture character.
                if(Sql("SELECT reward FROM revora_daily_claim_probe")==DBNull.Value && p.Inventory.Count>32)GrailMakeRoom(p,32,who=>Require(EntityTable.SaveEntity(who)),4294917801u);
                Require(EntityTable.SaveEntity(p));
                if((long)Sql("SELECT count(*) FROM revora_worship_claims WHERE uid=@uid AND NOT legacy","uid",(long)CareerUid)==0){
                    refuse();p.Entity.Teleport(1002,400,350);HeartTalk(p,79882,1);Require(p.Entity.MapID==3081);HeartTalk(p,98130,1);
                    Require((long)Sql("SELECT count(*) FROM revora_worship_claims WHERE uid=@uid AND NOT legacy","uid",(long)CareerUid)==1);Require(EntityTable.SaveEntity(p));
                }
                p.Entity.Teleport(1002,400,350);
                if((long)Sql("SELECT count(*) FROM revora_price_runs WHERE uid=@uid AND completed AND NOT legacy","uid",(long)CareerUid)==0){
                    refuse();HeartTalk(p,79882,2);refuse();uint before=p.Entity.ConquerPoints;HeartTalk(p,79882,13);
                    Require((long)Sql("SELECT count(*) FROM revora_price_runs WHERE uid=@uid AND completed AND NOT legacy AND skip_fee>0","uid",(long)CareerUid)==1);
                    long fee=Convert.ToInt64(Sql("SELECT skip_fee FROM revora_price_runs WHERE uid=@uid AND day='20880105'","uid",(long)CareerUid));
                    Require(p.Entity.ConquerPoints==before-fee+Config.DailyQuestCp);Require(EntityTable.SaveEntity(p));
                }
                if((long)Sql("SELECT count(*) FROM revora_rare_runs WHERE uid=@uid AND completed AND NOT legacy","uid",(long)CareerUid)==0){
                    refuse();HeartTalk(p,79882,19);refuse();uint before=p.Entity.ConquerPoints;HeartTalk(p,79882,30);
                    Require((long)Sql("SELECT count(*) FROM revora_rare_runs WHERE uid=@uid AND completed AND NOT legacy AND paid_cp>0","uid",(long)CareerUid)==1);
                    long fee=Convert.ToInt64(Sql("SELECT paid_cp FROM revora_rare_runs WHERE uid=@uid AND day='20880105'","uid",(long)CareerUid));
                    Require(p.Entity.ConquerPoints==before-fee+Config.DailyQuestCp);Require(EntityTable.SaveEntity(p));
                }
                DailyPredicateChecks(CareerUid);
                if(Sql("SELECT reward FROM revora_daily_claim_probe")==DBNull.Value){
                    var before=p.Inventory.Objects.Select(i=>i.UID).ToArray();System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,409));
                    var stone=p.Inventory.Objects.Single(i=>!before.Contains(i.UID));Require(stone.ID==1200001 && stone.Bound && stone.StackSize==1);
                    Sql("UPDATE revora_daily_claim_probe SET reward=@item,blessing=@seconds","item",(long)stone.UID,"seconds",(long)p.Entity.HeavenBlessing);
                }
                uint item=Convert.ToUInt32(Sql("SELECT reward FROM revora_daily_claim_probe"));var earned=p.Inventory.Objects.SingleOrDefault(i=>i.UID==item);
                if(earned!=null){SoulClaimUse(p,earned);SoulClaimUse(p,earned);}
                Require(p.Entity.HeavenBlessing==Convert.ToUInt32(Sql("SELECT blessing FROM revora_daily_claim_probe"))+604800 && !p.Inventory.ContainsUID(item));
                Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_daily_claim_probe SET snapshot=@snapshot","snapshot",DailyClaimSnapshot(p));
            }
            DailyPredicateChecks(CareerUid);
            Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=409 AND NOT legacy","uid",(long)CareerUid)==1);
            Require((long)Sql("SELECT count(*) FROM revora_praying_stone_uses WHERE uid=@uid AND item_uid=(SELECT reward FROM revora_daily_claim_probe) AND item_type=1200001 AND bound AND seconds_after-seconds_before=604800","uid",(long)CareerUid)==1);
            string exact=DailyClaimSnapshot(p);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_daily_claim_probe")));
            NobilityClaimPacket(p,409);Require(exact==DailyClaimSnapshot(p) && !PirateFenced(p));
        }finally{for(int i=0;i<clocks.Length;i++)clocks[i].SetValue(null,old[i]);}
        Console.WriteLine("[career-process] "+phase+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Daily claim did not close.");
    }
}
