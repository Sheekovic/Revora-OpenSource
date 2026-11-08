using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static partial class CheckCandidateArenaLifecycle {
    static void FreshSecondLifeSave(GameState p,int kills,int hits) {
        Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_second_life_probe SET kills=@kills,hits=@hits,snapshot=@snapshot","kills",kills,"hits",hits,"snapshot",FreshRebirthSnapshot(p));
    }
    static void FreshSecondLifeProcess(bool reload) {
        var p=FreshLoadSaved();uint uid=p.Entity.UID;SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && p.Entity.Reborn==1 && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_fresh_second_life_probe') IS NULL")) {
            Require(!reload && p.Entity.Level==15 && p.Entity.Class==11 && FreshRebirthSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_rebirth_probe")));
            Sql("CREATE TABLE revora_fresh_second_life_probe(uid bigint PRIMARY KEY,kills integer,hits integer,snapshot text);INSERT INTO revora_fresh_second_life_probe VALUES(@uid,0,0,@snapshot)","uid",(long)uid,"snapshot",FreshRebirthSnapshot(p));
        }
        Require(FreshRebirthSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_second_life_probe")));
        int kills=Convert.ToInt32(Sql("SELECT kills FROM revora_fresh_second_life_probe")),hits=Convert.ToInt32(Sql("SELECT hits FROM revora_fresh_second_life_probe"));
        if(!reload) {
            p.Attackable=true;p.CantAttack=Time32.Now.AddSeconds(-1);var timer=System.Diagnostics.Stopwatch.StartNew();int batch=0;
            while(p.Entity.Level<110 && batch<2500 && timer.Elapsed.TotalSeconds<150) {
                var body=new ushort[]{1002,1011,1020,1000,1015}.SelectMany(id=>Kernel.Maps[id].Entities.Values).Where(e=>e.MonsterInfo!=null && !e.Dead && e.MonsterInfo.Level<=p.Entity.Level+5 && e.MaxHitpoints<100000 && e.MonsterInfo.ID>=1 && e.MonsterInfo.ID<=100 && !e.MonsterInfo.Boss && !e.MonsterInfo.Guard).OrderByDescending(e=>e.MonsterInfo.Level).ThenBy(e=>e.UID).FirstOrDefault();
                if(body==null){System.Threading.Thread.Sleep(500);continue;}
                if(p.Entity.MapID!=body.MapID)p.Entity.Teleport(body.MapID,(ushort)(body.X+1),body.Y);else {p.Entity.X=(ushort)(body.X+1);p.Entity.Y=body.Y;}
                p.Screen.Add(body);uint hp=body.Hitpoints;int strikes=0;byte level=p.Entity.Level;
                while(!body.Dead && strikes<500) {p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);Attack.HandleAttack(new Attack(true){Attacker=uid,Attacked=body.UID,X=body.X,Y=body.Y,AttackType=Attack.Melee},p);strikes++;hits++;if(strikes==1)Require(body.Hitpoints<hp);}
                Require(body.Dead && !p.Entity.Dead && !PirateFenced(p));kills++;batch++;FreshSecondLifeSave(p,kills,hits);
                if(level!=p.Entity.Level || kills%250==0)Console.WriteLine("[fresh-second-life] kills="+kills+" family="+body.MonsterInfo.ID+" level="+p.Entity.Level+" exp="+p.Entity.Experience);
                if(p.Entity.Class<14 && p.Entity.Level>=p.PromoteLevelNeed) {
                    byte before=p.Entity.Class;uint material=p.PromoteItemNeed;uint cps=p.Entity.ConquerPoints;uint item=0;
                    if(material!=0){CareerBuy(p,material);item=p.Inventory.Objects.Single(i=>i.ID==material).UID;}
                    p.Entity.Teleport(1004,50,50);HeartTalk(p,10022,1);HeartTalk(p,10022,3);
                    Require(!PirateFenced(p) && p.Entity.Class==before+1 && (item==0 || !p.Inventory.ContainsUID(item)) && p.Entity.ConquerPoints==cps-(material==0?0:ConquerItemInformation.BaseInformations[material].ConquerPointsWorth));
                    FreshSecondLifeSave(p,kills,hits);
                    string once=FreshRebirthSnapshot(p);HeartTalk(p,10022,3);ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==727990),p);Require(once==FreshRebirthSnapshot(p));
                    Console.WriteLine("[fresh-second-life] promoted="+p.Entity.Class+" material="+material);
                }
            }
        }
        Require(FreshRebirthSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_second_life_probe")) && !PirateFenced(p));
        if(reload)Require(p.Entity.Level>=110 && p.Entity.Class==14);
        Require((long)Sql("SELECT count(*) FROM revora_custom_starter_claims WHERE uid=@uid AND reborn=1","uid",(long)uid)==1);
        Console.WriteLine("[career-process] career-fresh-second-life-"+(reload?"reload":"train")+"-passed uid="+uid+" level="+p.Entity.Level+" class="+p.Entity.Class+" kills="+kills+" hits="+hits+" cps="+p.Entity.ConquerPoints);
        Server.CommandsAI("@close");throw new InvalidOperationException("Fresh second life did not close.");
    }
}
