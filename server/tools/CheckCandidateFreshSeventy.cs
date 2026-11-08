using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static partial class CheckCandidateArenaLifecycle {
    static void FreshSeventySave(GameState p,string stage,int kills,int hits) {
        Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_seventy_probe SET stage=@stage,kills=@kills,hits=@hits,snapshot=@snapshot","stage",stage,"kills",kills,"hits",hits,"snapshot",FreshTrainingSnapshot(p));
    }
    static void FreshSeventyProcess(bool reload) {
        var p=FreshLoadSaved();uint uid=p.Entity.UID;Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_fresh_seventy_probe') IS NULL")) {
            Require(!reload && p.Entity.Level==40 && p.Entity.Class==12 && FreshTrainingSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_milestone_probe")));
            Sql("CREATE TABLE revora_fresh_seventy_probe(uid bigint PRIMARY KEY,stage text,kills integer,hits integer,snapshot text);INSERT INTO revora_fresh_seventy_probe VALUES(@uid,'training',0,0,@snapshot)","uid",(long)uid,"snapshot",FreshTrainingSnapshot(p));
        }
        Require(FreshTrainingSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_seventy_probe")));
        int kills=Convert.ToInt32(Sql("SELECT kills FROM revora_fresh_seventy_probe")),hits=Convert.ToInt32(Sql("SELECT hits FROM revora_fresh_seventy_probe"));
        string stage=Convert.ToString(Sql("SELECT stage FROM revora_fresh_seventy_probe"));
        if(!reload && stage=="training") {
            p.Attackable=true;p.CantAttack=Time32.Now.AddSeconds(-1);
            while(p.Entity.Level<70 && kills<3000) {
                var body=new ushort[]{1002,1011,1020,1000,1015}.SelectMany(id=>Kernel.Maps[id].Entities.Values).Where(e=>e.MonsterInfo!=null && !e.Dead && e.MonsterInfo.Level<=p.Entity.Level+5 && e.MonsterInfo.Level>=30 && e.MaxHitpoints<100000
                    && e.MonsterInfo.ID>=1 && e.MonsterInfo.ID<=30 && !e.MonsterInfo.Boss && !e.MonsterInfo.Guard).OrderByDescending(e=>e.MonsterInfo.Level).ThenBy(e=>e.UID).FirstOrDefault();Require(body!=null);
                if(p.Entity.MapID!=body.MapID)p.Entity.Teleport(body.MapID,(ushort)(body.X+1),body.Y);else {p.Entity.X=(ushort)(body.X+1);p.Entity.Y=body.Y;}
                p.Screen.Add(body);uint hp=body.Hitpoints;int strikes=0;byte before=p.Entity.Level;
                while(!body.Dead && strikes<500) {p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);Attack.HandleAttack(new Attack(true){Attacker=uid,Attacked=body.UID,X=body.X,Y=body.Y,AttackType=Attack.Melee},p);strikes++;hits++;if(strikes==1)Require(body.Hitpoints<hp);}
                Require(body.Dead && !p.Entity.Dead && !PirateFenced(p));kills++;
                FreshSeventySave(p,"training",kills,hits);
                if(before!=p.Entity.Level || kills%50==0)Console.WriteLine("[fresh-seventy] kills="+kills+" family="+body.MonsterInfo.ID+" name="+body.MonsterInfo.Name+" hp="+hp+" strikes="+strikes+" level="+p.Entity.Level+" exp="+p.Entity.Experience);
            }
            Require(p.Entity.Level>=70 && p.Entity.Class==12);
            FreshSeventySave(p,"trained",kills,hits);stage="trained";
        }
        if(!reload && stage=="trained") {
            Require(!p.Inventory.Contains(1080001,1));uint before=p.Entity.ConquerPoints;CareerBuy(p,1080001);
            var material=p.Inventory.Objects.Single(i=>i.ID==1080001);Require(material.StackSize==1 && !material.Bound);
            Sql("ALTER TABLE revora_fresh_seventy_probe ADD COLUMN emerald bigint,ADD COLUMN price bigint;UPDATE revora_fresh_seventy_probe SET emerald=@item,price=@price","item",(long)material.UID,"price",(long)(before-p.Entity.ConquerPoints));
            FreshSeventySave(p,"material",kills,hits);stage="material";
        }
        if(!reload && stage=="material") {
            uint emerald=Convert.ToUInt32(Sql("SELECT emerald FROM revora_fresh_seventy_probe"));Require(p.Inventory.ContainsUID(emerald));uint cp=p.Entity.ConquerPoints;
            p.Entity.Teleport(1004,50,50);HeartTalk(p,10022,1);HeartTalk(p,10022,3);Require(p.Entity.Class==13 && !PirateFenced(p) && !p.Inventory.ContainsUID(emerald) && p.Entity.ConquerPoints==cp);
            FreshSeventySave(p,"promoted",kills,hits);stage="promoted";
        }
        if(!reload && stage=="promoted") {
            uint cps=p.Entity.ConquerPoints,bound=p.Entity.BoundCps;ulong silver=p.Entity.Money;int count=p.Inventory.Count;
            ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==727990),p);
            Require(!PirateFenced(p) && p.Inventory.Count==count+1 && p.Entity.ConquerPoints==cps && p.Entity.BoundCps==bound && p.Entity.Money==silver);
            var crop=p.Inventory.Objects.Single(i=>i.ID==203009);Require(crop.Bound && crop.Plus==12 && crop.Bless==0 && crop.SocketOne==0 && crop.SocketTwo==0);
            FreshSeventySave(p,"claimed",kills,hits);
        }
        Require(Convert.ToString(Sql("SELECT stage FROM revora_fresh_seventy_probe"))=="claimed" && p.Entity.Level>=70 && p.Entity.Class==13);
        Require(FreshTrainingSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_seventy_probe")));
        Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10821 AND source='class-promotion-v1'","uid",(long)uid)==1);
        Require((long)Sql("SELECT count(*) FROM revora_custom_starter_claims r JOIN items i ON i.uid=ANY(r.rewards) AND i.entityid=r.uid WHERE r.uid=@uid AND r.reborn=0 AND r.milestone=70 AND i.id=203009 AND i.bound=1 AND i.plus=12 AND i.bless=0 AND i.socketone=0 AND i.sockettwo=0","uid",(long)uid)==1);
        string once=FreshTrainingSnapshot(p);ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==727990),p);HeartTalk(p,10022,3);Require(!PirateFenced(p) && once==FreshTrainingSnapshot(p));
        Console.WriteLine("[career-process] career-fresh-seventy-"+(reload?"reload":"earn")+"-passed uid="+uid+" level="+p.Entity.Level+" class="+p.Entity.Class+" kills="+kills+" hits="+hits+" cps="+p.Entity.ConquerPoints);
        Server.CommandsAI("@close");throw new InvalidOperationException("Fresh milestone did not close.");
    }
}
