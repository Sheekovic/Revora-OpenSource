using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshAdvancedSnapshot(GameState p) {return Convert.ToString(Sql(@"SELECT json_build_object('career',@career::json,'dragon',(SELECT json_agg(r ORDER BY day) FROM revora_dragon_exp_uses r WHERE uid=@uid),'balls',(SELECT row_to_json(r) FROM revora_exp_ball_days r WHERE uid=@uid))::text","career",FreshMoonSnapshot(p),"uid",(long)p.Entity.UID));}
    static void FreshAdvancedSave(GameState p,string stage,int kills,int hits) {Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_advanced_probe SET stage=@stage,kills=@kills,hits=@hits,snapshot=@snapshot","stage",stage,"kills",kills,"hits",hits,"snapshot",FreshAdvancedSnapshot(p));}
    static void FreshAdvancedProcess(bool reload) {
        var p=FreshLoadSaved();uint uid=p.Entity.UID;Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_fresh_advanced_probe') IS NULL")) {
            Require(!reload && FreshMoonSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_moon_probe")) && p.Inventory.Contains(721090,1));
            Sql("CREATE TABLE revora_fresh_advanced_probe(uid bigint PRIMARY KEY,stage text,kills integer,hits integer,snapshot text,box bigint);INSERT INTO revora_fresh_advanced_probe VALUES(@uid,'exchange',0,0,@snapshot,@box)","uid",(long)uid,"snapshot",FreshAdvancedSnapshot(p),"box",(long)p.Inventory.Objects.Single(i=>i.ID==721090).UID);
        }
        Require(FreshAdvancedSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_advanced_probe")));
        int kills=Convert.ToInt32(Sql("SELECT kills FROM revora_fresh_advanced_probe")),hits=Convert.ToInt32(Sql("SELECT hits FROM revora_fresh_advanced_probe"));string stage=Convert.ToString(Sql("SELECT stage FROM revora_fresh_advanced_probe"));
        if(!reload && stage=="exchange") {
            Require((long)Sql("SELECT count(*) FROM revora_dragon_exp_uses WHERE uid=@uid","uid",(long)uid)==0);
            CareerBuy(p,1088000);var db=p.Inventory.Objects.Single(i=>i.ID==1088000);byte level=p.Entity.Level;ulong xp=p.Entity.Experience;
            p.Entity.Teleport(1036,200,200);HeartTalk(p,3825,0);HeartTalk(p,3825,1);
            Require(!p.Inventory.ContainsUID(db.UID) && (p.Entity.Level>level || p.Entity.Experience>xp));
            Require((long)Sql("SELECT count(*) FROM revora_dragon_exp_uses WHERE uid=@uid AND paid=1 AND @item=ANY(item_uids)","uid",(long)uid,"item",(long)db.UID)==1);
            FreshAdvancedSave(p,"training",kills,hits);stage="training";
            string once=FreshAdvancedSnapshot(p);HeartTalk(p,3825,1);Require(once==FreshAdvancedSnapshot(p));
            Console.WriteLine("[fresh-advanced] dragonball-consumed="+db.UID+" level="+p.Entity.Level+" exp="+p.Entity.Experience);
        }
        if(!reload && stage=="training") {
            p.Attackable=true;p.CantAttack=Time32.Now.AddSeconds(-1);var timer=System.Diagnostics.Stopwatch.StartNew();int batch=0;
            while(p.Entity.Level<120 && batch<2500 && timer.Elapsed.TotalSeconds<150) {
                var body=new ushort[]{1002,1011,1020,1000,1015}.SelectMany(id=>Kernel.Maps[id].Entities.Values).Where(e=>e.MonsterInfo!=null && !e.Dead && e.MonsterInfo.Level<=p.Entity.Level+5 && e.MonsterInfo.Level>=80 && e.MaxHitpoints<100000 && e.MonsterInfo.ID>=1 && e.MonsterInfo.ID<=100 && !e.MonsterInfo.Boss && !e.MonsterInfo.Guard).OrderByDescending(e=>e.MonsterInfo.Level).ThenBy(e=>e.UID).FirstOrDefault();

                if(body==null) {System.Threading.Thread.Sleep(500);continue;}
                if(p.Entity.MapID!=body.MapID)p.Entity.Teleport(body.MapID,(ushort)(body.X+1),body.Y);else {p.Entity.X=(ushort)(body.X+1);p.Entity.Y=body.Y;}
                p.Screen.Add(body);uint hp=body.Hitpoints;int strikes=0;byte before=p.Entity.Level;
                while(!body.Dead && strikes<500) {p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);Attack.HandleAttack(new Attack(true){Attacker=uid,Attacked=body.UID,X=body.X,Y=body.Y,AttackType=Attack.Melee},p);strikes++;hits++;if(strikes==1)Require(body.Hitpoints<hp);}
                Require(body.Dead && !p.Entity.Dead && !PirateFenced(p));kills++;batch++;FreshAdvancedSave(p,"training",kills,hits);
                if(before!=p.Entity.Level || kills%250==0)Console.WriteLine("[fresh-advanced] kills="+kills+" family="+body.MonsterInfo.ID+" hp="+hp+" level="+p.Entity.Level+" exp="+p.Entity.Experience);
            }
            if(p.Entity.Level>=120) {FreshAdvancedSave(p,"trained",kills,hits);stage="trained";}
        }
        if(!reload && p.Entity.Level>=110 && p.Entity.Class==14) {
            uint box=Convert.ToUInt32(Sql("SELECT box FROM revora_fresh_advanced_probe"));Require(p.Inventory.ContainsUID(box) && p.Entity.Class==14);uint cps=p.Entity.ConquerPoints;
            p.Entity.Teleport(1004,50,50);HeartTalk(p,10022,1);HeartTalk(p,10022,3);Require(p.Entity.Class==15 && !p.Inventory.ContainsUID(box) && p.Entity.ConquerPoints==cps && !PirateFenced(p));
            if(stage=="trained")stage="promoted";FreshAdvancedSave(p,stage,kills,hits);
        }
        if(!reload && stage=="trained" && p.Entity.Class==15) {stage="promoted";FreshAdvancedSave(p,stage,kills,hits);}
        if(!reload && stage=="promoted") {
            int count=p.Inventory.Count;uint cps=p.Entity.ConquerPoints;ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==727990),p);
            Require(p.Inventory.Count==count+1 && p.Inventory.Objects.Single(i=>i.ID==3006233).Bound && p.Entity.ConquerPoints==cps && !PirateFenced(p));
            FreshAdvancedSave(p,"claimed",kills,hits);stage="claimed";
        }
        Require(FreshAdvancedSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_advanced_probe")));
        if(reload || stage=="claimed") {
            Require(stage=="claimed" && p.Entity.Level>=120 && p.Entity.Class==15);
            Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10823 AND source='class-promotion-v1'","uid",(long)uid)==1);
            Require((long)Sql("SELECT count(*) FROM revora_custom_starter_claims r JOIN items i ON i.uid=ANY(r.rewards) WHERE r.uid=@uid AND r.reborn=0 AND r.milestone=120 AND i.entityid=r.uid AND i.id=3006233 AND i.bound=1","uid",(long)uid)==1);
            if(p.Entity.MapID!=1004) {p.Entity.Teleport(1004,50,50);HeartTalk(p,10022,0);FreshAdvancedSave(p,stage,kills,hits);}
            string once=FreshAdvancedSnapshot(p);ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==727990),p);HeartTalk(p,10022,3);Require(once==FreshAdvancedSnapshot(p));
        }
        Console.WriteLine("[career-process] career-fresh-advanced-"+(reload?"reload":"earn")+"-passed stage="+stage+" uid="+uid+" level="+p.Entity.Level+" class="+p.Entity.Class+" kills="+kills+" hits="+hits+" cps="+p.Entity.ConquerPoints);
        Server.CommandsAI("@close");throw new InvalidOperationException("Fresh advanced did not close.");
    }
}
