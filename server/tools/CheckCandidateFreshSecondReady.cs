using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static partial class CheckCandidateArenaLifecycle {
    static void FreshSecondReadySave(GameState p,int kills,int hits) {
        Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_second_ready_probe SET kills=@kills,hits=@hits,snapshot=@snapshot","kills",kills,"hits",hits,"snapshot",FreshRebirthSnapshot(p));
    }
    static void FreshSecondReadyProcess(bool reload) {
        var p=FreshLoadSaved();uint uid=p.Entity.UID;SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && p.Entity.Reborn==1 && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_fresh_second_ready_probe') IS NULL")) {
            Require(!reload && p.Entity.Level==110 && p.Entity.Class==15 && FreshRebirthSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_second_promotion_probe")));
            Sql("CREATE TABLE revora_fresh_second_ready_probe(uid bigint PRIMARY KEY,kills integer,hits integer,snapshot text);INSERT INTO revora_fresh_second_ready_probe VALUES(@uid,0,0,@snapshot)","uid",(long)uid,"snapshot",FreshRebirthSnapshot(p));
        }
        Require(FreshRebirthSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_second_ready_probe")));
        if(!(bool)Sql("SELECT to_regclass('revora_fresh_second_boost_probe') IS NULL"))Require(p.Entity.DoubleExperienceTime>0 && (long)Sql("SELECT count(*) FROM revora_exp_potion_uses r JOIN revora_fresh_second_boost_probe b ON b.uid=r.uid AND b.item=r.item_uid WHERE r.uid=@uid AND r.item_type=723017 AND r.seconds=3600","uid",(long)uid)==1);
        int kills=Convert.ToInt32(Sql("SELECT kills FROM revora_fresh_second_ready_probe")),hits=Convert.ToInt32(Sql("SELECT hits FROM revora_fresh_second_ready_probe"));
        if(!reload) {
            p.Attackable=true;p.CantAttack=Time32.Now.AddSeconds(-1);var timer=System.Diagnostics.Stopwatch.StartNew();int batch=0;
            while(p.Entity.Level<120 && batch<2500 && timer.Elapsed.TotalSeconds<150) {
                var body=new ushort[]{1002,1011,1020,1000,1015}.SelectMany(id=>Kernel.Maps[id].Entities.Values).Where(e=>e.MonsterInfo!=null && !e.Dead && e.MonsterInfo.Level<=p.Entity.Level+5 && e.MaxHitpoints<100000 && e.MonsterInfo.ID>=1 && e.MonsterInfo.ID<=100 && !e.MonsterInfo.Boss && !e.MonsterInfo.Guard).OrderByDescending(e=>e.MonsterInfo.Level).ThenBy(e=>e.UID).FirstOrDefault();
                if(body==null){System.Threading.Thread.Sleep(500);continue;}
                if(p.Entity.MapID!=body.MapID)p.Entity.Teleport(body.MapID,(ushort)(body.X+1),body.Y);else {p.Entity.X=(ushort)(body.X+1);p.Entity.Y=body.Y;}
                p.Screen.Add(body);uint hp=body.Hitpoints;int strikes=0;byte level=p.Entity.Level;
                while(!body.Dead && strikes<500) {p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);Attack.HandleAttack(new Attack(true){Attacker=uid,Attacked=body.UID,X=body.X,Y=body.Y,AttackType=Attack.Melee},p);strikes++;hits++;if(strikes==1)Require(body.Hitpoints<hp);}
                Require(body.Dead && !p.Entity.Dead && !PirateFenced(p));kills++;batch++;FreshSecondReadySave(p,kills,hits);
                if(level!=p.Entity.Level || kills%250==0)Console.WriteLine("[fresh-second-ready] kills="+kills+" family="+body.MonsterInfo.ID+" level="+p.Entity.Level+" exp="+p.Entity.Experience);

            }
        }
        Require(FreshRebirthSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_second_ready_probe")) && !PirateFenced(p));
        if(reload)Require(p.Entity.Level>=120 && p.Entity.Class==15);
        Require((long)Sql("SELECT count(*) FROM revora_custom_starter_claims WHERE uid=@uid AND reborn=1","uid",(long)uid)==1);
        Console.WriteLine("[career-process] career-fresh-second-ready-"+(reload?"reload":"train")+"-passed uid="+uid+" level="+p.Entity.Level+" class="+p.Entity.Class+" kills="+kills+" hits="+hits+" cps="+p.Entity.ConquerPoints);
        Server.CommandsAI("@close");throw new InvalidOperationException("Fresh second life did not close.");
    }
    static void FreshSecondBoostProcess() {
        var p=FreshLoadSaved();uint uid=p.Entity.UID;Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && p.Entity.Reborn==1 && !PirateFenced(p));
        Require((bool)Sql("SELECT to_regclass('revora_fresh_second_boost_probe') IS NULL") && FreshRebirthSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_second_ready_probe")) && p.Entity.DoubleExperienceTime==0);
        uint cps=p.Entity.ConquerPoints;Require(!p.Inventory.Contains(723017,1));CareerBuy(p,723017);var potion=p.Inventory.Objects.Single(i=>i.ID==723017);uint price=cps-p.Entity.ConquerPoints;
        p.Filtering=true;ChoiceWireOpen(p,potion.UID);Require(!PirateFenced(p) && !p.Inventory.ContainsUID(potion.UID) && p.Entity.DoubleExperienceTime==3600);
        Require((long)Sql("SELECT count(*) FROM revora_exp_potion_uses WHERE uid=@uid AND item_uid=@item AND item_type=723017 AND seconds=3600","uid",(long)uid,"item",(long)potion.UID)==1);
        Require(Convert.ToUInt16(Sql("SELECT doubleexptime FROM entities WHERE uid=@uid","uid",(long)uid))==3600);
        string once=FreshRebirthSnapshot(p);ChoiceWireOpen(p,potion.UID);Require(once==FreshRebirthSnapshot(p) && p.Entity.DoubleExperienceTime==3600);
        Sql("CREATE TABLE revora_fresh_second_boost_probe(uid bigint PRIMARY KEY,item bigint,price bigint);INSERT INTO revora_fresh_second_boost_probe VALUES(@uid,@item,@price)","uid",(long)uid,"item",(long)potion.UID,"price",(long)price);
        FreshSecondReadySave(p,Convert.ToInt32(Sql("SELECT kills FROM revora_fresh_second_ready_probe")),Convert.ToInt32(Sql("SELECT hits FROM revora_fresh_second_ready_probe")));
        Console.WriteLine("[career-process] career-fresh-second-ready-boost-passed uid="+uid+" potion="+potion.UID+" price="+price+" seconds="+p.Entity.DoubleExperienceTime);
        Server.CommandsAI("@close");throw new InvalidOperationException("Fresh potion did not close.");
    }

}
