using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshMoonSnapshot(GameState p) {return Convert.ToString(Sql(@"SELECT json_build_object('character',@character::json,'moon',(SELECT row_to_json(r) FROM revora_moon_runs r WHERE uid=@uid),'results',(SELECT json_agg(r ORDER BY completed_at,run) FROM revora_moon_results r WHERE uid=@uid))::text","character",FreshTrainingSnapshot(p),"uid",(long)p.Entity.UID));}
    static void FreshMoonSave(GameState p,int visits,int kills,int hits,int surrenders) {
        Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_moon_probe SET visits=@visits,kills=@kills,hits=@hits,surrenders=@surrenders,snapshot=@snapshot","visits",visits,"kills",kills,"hits",hits,"surrenders",surrenders,"snapshot",FreshMoonSnapshot(p));
    }
    static void FreshMoonProcess(bool reload) {
        var p=FreshLoadSaved();uint uid=p.Entity.UID;Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_fresh_moon_probe') IS NULL")) {
            Require(!reload && FreshTrainingSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_hundred_probe")));
            Sql("CREATE TABLE revora_fresh_moon_probe(uid bigint PRIMARY KEY,visits integer,kills integer,hits integer,surrenders integer,snapshot text);INSERT INTO revora_fresh_moon_probe VALUES(@uid,0,0,0,0,@snapshot)","uid",(long)uid,"snapshot",FreshMoonSnapshot(p));
        }
        Require(FreshMoonSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_moon_probe")));
        int visits=Convert.ToInt32(Sql("SELECT visits FROM revora_fresh_moon_probe")),kills=Convert.ToInt32(Sql("SELECT kills FROM revora_fresh_moon_probe")),hits=Convert.ToInt32(Sql("SELECT hits FROM revora_fresh_moon_probe")),surrenders=Convert.ToInt32(Sql("SELECT surrenders FROM revora_fresh_moon_probe"));
        if(!reload) {
            foreach(var entry in new[]{Tuple.Create(201009u,(byte)10),Tuple.Create(202009u,(byte)11),Tuple.Create(204009u,(byte)19),Tuple.Create(300000u,(byte)12),Tuple.Create(203009u,(byte)18)}) {
                FreshEquip(p,entry.Item1,entry.Item2);Require(p.Equipment.GetItems().Any(i=>i.ID==entry.Item1 && i.Position==entry.Item2));
            }
            FreshMoonSave(p,visits,kills,hits,surrenders);
            p.Attackable=true;p.CantAttack=Time32.Now.AddSeconds(-1);
            while(!p.Inventory.Contains(721090,1) && visits<512) {
                if(p.Entity.MapID==1004)p.Entity.Teleport(1002,489,622);
                if(p.Entity.MapID==1002) {HeartTalk(p,600050,1);Require(p.Entity.MapID==1042);FreshMoonSave(p,visits,kills,hits,surrenders);}
                if(p.Entity.MapID==1042) {HeartTalk(p,600003,1);visits++;FreshMoonSave(p,visits,kills,hits,surrenders);Console.WriteLine("[fresh-moon] visit="+visits+" tactic="+p.Entity.MapID);}
                if(p.Entity.MapID>=1043 && p.Entity.MapID<=1048) {
                    int index=p.Entity.MapID-1043;uint token=721010u+(uint)index;int attempted=0;
                    while(!p.Inventory.Contains(token,1) && attempted<20) {
                        var body=p.Map.Entities.Values.Where(e=>e.MonsterInfo!=null && e.MonsterInfo.ID==6000+index && !e.Dead).OrderBy(e=>e.UID).FirstOrDefault();Require(body!=null);
                        p.Entity.X=(ushort)(body.X+1);p.Entity.Y=body.Y;p.Screen.Add(body);uint hp=body.Hitpoints;int strikes=0;
                        while(!body.Dead && strikes<500) {p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);Attack.HandleAttack(new Attack(true){Attacker=uid,Attacked=body.UID,X=body.X,Y=body.Y,AttackType=Attack.Melee},p);strikes++;hits++;if(strikes==1)Require(body.Hitpoints<hp);}
                        Require(body.Dead && !p.Entity.Dead && !PirateFenced(p));kills++;attempted++;FreshMoonSave(p,visits,kills,hits,surrenders);
                    }
                    Require(p.Inventory.Contains(token,1));HeartTalk(p,600005u+(uint)index*2,1);Require(p.Entity.MapID==1042);FreshMoonSave(p,visits,kills,hits,surrenders);
                } else if(p.Entity.MapID==1049) {
                    bool full=(bool)Sql("SELECT NOT (0=ANY(tokens)) FROM revora_moon_runs WHERE uid=@uid","uid",(long)uid);
                    if(full) {HeartTalk(p,600017,1);Require(p.Entity.MapID==1050);}
                    else {HeartTalk(p,600017,3);HeartTalk(p,600017,4);Require(p.Entity.MapID==1002 && !p.Inventory.Objects.Any(i=>i.ID>=721010 && i.ID<=721015));surrenders++;}
                    FreshMoonSave(p,visits,kills,hits,surrenders);
                }
                if(p.Entity.MapID==1050) {
                    foreach(uint ghost in Enumerable.Range(600018,25).Select(i=>(uint)i)) {HeartTalk(p,ghost,1);if(p.Entity.MapID==1002)break;Require(p.Entity.MapID==1050 && !PirateFenced(p));}
                    Require(p.Entity.MapID==1002 && p.Inventory.Contains(721090,1));FreshMoonSave(p,visits,kills,hits,surrenders);
                }
            }
        }
        Require(p.Entity.MapID==1002 && p.Inventory.Contains(721090,1) && !PirateFenced(p));
        Require(FreshMoonSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_moon_probe")));
        Require((long)Sql("SELECT count(*) FROM revora_moon_results WHERE uid=@uid AND choice='moon-box'","uid",(long)uid)==1);
        Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11022 AND source='moon-box-v1'","uid",(long)uid)==1);
        var rewards=(long[])Sql("SELECT rewards FROM revora_moon_results WHERE uid=@uid AND choice='moon-box'","uid",(long)uid);Require(rewards.Length==2);
        var items=p.Inventory.Objects.Where(i=>rewards.Contains((long)i.UID)).ToArray();Require(items.Length==2 && items.Any(i=>i.ID==721090 && !i.Bound) && items.Any(i=>i.Bound && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7));
        Console.WriteLine("[career-process] career-fresh-moon-"+(reload?"reload":"earn")+"-passed uid="+uid+" visits="+visits+" kills="+kills+" hits="+hits+" surrenders="+surrenders);
        Server.CommandsAI("@close");throw new InvalidOperationException("Fresh Moon did not close.");
    }
}
