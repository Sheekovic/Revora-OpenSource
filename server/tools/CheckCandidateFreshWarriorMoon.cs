using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static partial class CheckCandidateArenaLifecycle {
    static void FreshWarriorMoonSave(GameState p,int visits,int kills,int hits,int surrenders) {
        Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_warrior_moon_probe SET visits=@visits,kills=@kills,hits=@hits,surrenders=@surrenders,snapshot=@snapshot","visits",visits,"kills",kills,"hits",hits,"surrenders",surrenders,"snapshot",FreshWarriorSnapshot(p));
    }
    static void FreshWarriorMoonProcess(bool reload) {
        var p=FreshLoadSaved();uint uid=p.Entity.UID;Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_fresh_warrior_moon_probe') IS NULL")) {
            Require(!reload && p.Entity.Reborn==2 && p.Entity.Level>=110 && p.Entity.Class==24 && !p.Inventory.Contains(721090,1) && FreshWarriorSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_warrior_probe")));
            Sql("CREATE TABLE revora_fresh_warrior_moon_probe(uid bigint PRIMARY KEY,visits integer,kills integer,hits integer,surrenders integer,snapshot text);INSERT INTO revora_fresh_warrior_moon_probe VALUES(@uid,0,0,0,0,@snapshot)","uid",(long)uid,"snapshot",FreshWarriorSnapshot(p));
        }
        Require(FreshWarriorSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_warrior_moon_probe")));
        int visits=Convert.ToInt32(Sql("SELECT visits FROM revora_fresh_warrior_moon_probe")),kills=Convert.ToInt32(Sql("SELECT kills FROM revora_fresh_warrior_moon_probe")),hits=Convert.ToInt32(Sql("SELECT hits FROM revora_fresh_warrior_moon_probe")),surrenders=Convert.ToInt32(Sql("SELECT surrenders FROM revora_fresh_warrior_moon_probe"));
        if(!reload) {
            if(p.Entity.MapID!=1002 && !(p.Entity.MapID>=1042 && p.Entity.MapID<=1050))p.Entity.Teleport(1002,489,622);
            FreshWarriorMoonSave(p,visits,kills,hits,surrenders);
            p.Attackable=true;p.CantAttack=Time32.Now.AddSeconds(-1);
            while(!p.Inventory.Contains(721090,1) && visits<512) {
                if(p.Entity.MapID==1004)p.Entity.Teleport(1002,489,622);
                if(p.Entity.MapID==1002) {HeartTalk(p,600050,1);Require(p.Entity.MapID==1042);FreshWarriorMoonSave(p,visits,kills,hits,surrenders);}
                if(p.Entity.MapID==1042) {HeartTalk(p,600003,1);visits++;FreshWarriorMoonSave(p,visits,kills,hits,surrenders);Console.WriteLine("[fresh-warrior-moon] visit="+visits+" tactic="+p.Entity.MapID);}
                if(p.Entity.MapID>=1043 && p.Entity.MapID<=1048) {
                    int index=p.Entity.MapID-1043;uint token=721010u+(uint)index;int attempted=0;
                    while(!p.Inventory.Contains(token,1) && attempted<20) {
                        var body=p.Map.Entities.Values.Where(e=>e.MonsterInfo!=null && e.MonsterInfo.ID==6000+index && !e.Dead).OrderBy(e=>e.UID).FirstOrDefault();Require(body!=null);
                        p.Entity.X=(ushort)(body.X+1);p.Entity.Y=body.Y;p.Screen.Add(body);uint hp=body.Hitpoints;int strikes=0;
                        while(!body.Dead && strikes<500) {p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);Attack.HandleAttack(new Attack(true){Attacker=uid,Attacked=body.UID,X=body.X,Y=body.Y,AttackType=Attack.Melee},p);strikes++;hits++;if(strikes==1)Require(body.Hitpoints<hp);}
                        Require(body.Dead && !p.Entity.Dead && !PirateFenced(p));kills++;attempted++;FreshWarriorMoonSave(p,visits,kills,hits,surrenders);
                    }
                    Require(p.Inventory.Contains(token,1));HeartTalk(p,600005u+(uint)index*2,1);Require(p.Entity.MapID==1042);FreshWarriorMoonSave(p,visits,kills,hits,surrenders);
                } else if(p.Entity.MapID==1049) {
                    bool full=(bool)Sql("SELECT NOT (0=ANY(tokens)) FROM revora_moon_runs WHERE uid=@uid","uid",(long)uid);
                    if(full) {HeartTalk(p,600017,1);Require(p.Entity.MapID==1050);}
                    else {HeartTalk(p,600017,3);HeartTalk(p,600017,4);Require(p.Entity.MapID==1002 && !p.Inventory.Objects.Any(i=>i.ID>=721010 && i.ID<=721015));surrenders++;}
                    FreshWarriorMoonSave(p,visits,kills,hits,surrenders);
                }
                if(p.Entity.MapID==1050) {
                    foreach(uint ghost in Enumerable.Range(600018,25).Select(i=>(uint)i)) {HeartTalk(p,ghost,1);if(p.Entity.MapID==1002)break;Require(p.Entity.MapID==1050 && !PirateFenced(p));}
                    Require(p.Entity.MapID==1002 && p.Inventory.Contains(721090,1));FreshWarriorMoonSave(p,visits,kills,hits,surrenders);
                }
            }
        }
        Require(p.Entity.MapID==1002 && p.Inventory.Contains(721090,1) && !PirateFenced(p));
        Require(FreshWarriorSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_warrior_moon_probe")));
        Require((long)Sql("SELECT count(*) FROM revora_moon_results WHERE uid=@uid AND choice='moon-box'","uid",(long)uid)==3);
        Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11022 AND source='moon-box-v1'","uid",(long)uid)==1);
        var rewards=(long[])Sql("SELECT rewards FROM revora_moon_results WHERE uid=@uid AND choice='moon-box' ORDER BY completed_at DESC,run DESC LIMIT 1","uid",(long)uid);Require(rewards.Length==2);
        var items=p.Inventory.Objects.Where(i=>rewards.Contains((long)i.UID)).ToArray();Require(items.Length==2 && items.Any(i=>i.ID==721090 && !i.Bound) && items.Any(i=>i.Bound && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7));
        Console.WriteLine("[career-process] career-fresh-warrior-moon-"+(reload?"reload":"earn")+"-passed uid="+uid+" visits="+visits+" kills="+kills+" hits="+hits+" surrenders="+surrenders);
        Server.CommandsAI("@close");throw new InvalidOperationException("Fresh Moon did not close.");
    }
    static void FreshWarriorPromotionProcess(bool reload) {
        var p=FreshLoadSaved();uint uid=p.Entity.UID;SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && p.Entity.Reborn==2 && !PirateFenced(p));
        if(!reload) {
            Require(p.Entity.Level>=110 && p.Entity.Class==24 && FreshWarriorSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_warrior_moon_probe")) && (bool)Sql("SELECT to_regclass('revora_fresh_warrior_promotion_probe') IS NULL"));
            var box=p.Inventory.Objects.Single(i=>i.ID==721090);uint cps=p.Entity.ConquerPoints,bound=p.Entity.BoundCps;ulong silver=p.Entity.Money;
            Require((long)Sql("SELECT count(*) FROM revora_moon_results WHERE uid=@uid AND choice='moon-box' AND @box=ANY(rewards)","uid",(long)uid,"box",(long)box.UID)==1);
            p.Entity.Teleport(1004,50,50);HeartTalk(p,10001,1);HeartTalk(p,10001,3);
            Require(!PirateFenced(p) && p.Entity.Class==25 && !p.Inventory.ContainsUID(box.UID) && p.Entity.ConquerPoints==cps && p.Entity.BoundCps==bound && p.Entity.Money==silver);
            Require(EntityTable.SaveEntity(p));Sql("CREATE TABLE revora_fresh_warrior_promotion_probe(uid bigint PRIMARY KEY,box bigint,snapshot text);INSERT INTO revora_fresh_warrior_promotion_probe VALUES(@uid,@box,@snapshot)","uid",(long)uid,"box",(long)box.UID,"snapshot",FreshWarriorSnapshot(p));
        }
        Require(p.Entity.Level>=110 && p.Entity.Class==25 && FreshWarriorSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_warrior_promotion_probe")));
        string once=FreshWarriorSnapshot(p);HeartTalk(p,10001,3);Require(once==FreshWarriorSnapshot(p) && !PirateFenced(p));
        Require((long)Sql("SELECT count(*) FROM revora_custom_starter_claims WHERE uid=@uid AND reborn=2","uid",(long)uid)==1);
        Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10817 AND source='class-promotion-v1'","uid",(long)uid)==1);
        Console.WriteLine("[career-process] career-fresh-warrior-moon-promote"+(reload?"-reload":"")+"-passed uid="+uid+" level="+p.Entity.Level+" class="+p.Entity.Class+" cps="+p.Entity.ConquerPoints);
        Server.CommandsAI("@close");throw new InvalidOperationException("Fresh second promotion did not close.");
    }

}
