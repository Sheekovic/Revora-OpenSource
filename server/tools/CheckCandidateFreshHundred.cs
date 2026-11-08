using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static partial class CheckCandidateArenaLifecycle {
    static void FreshHundredSave(GameState p,string stage,int kills,int hits) {
        Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_hundred_probe SET stage=@stage,kills=@kills,hits=@hits,snapshot=@snapshot","stage",stage,"kills",kills,"hits",hits,"snapshot",FreshTrainingSnapshot(p));
    }
    static void FreshHundredProcess(bool reload) {
        var p=FreshLoadSaved();uint uid=p.Entity.UID;Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_fresh_hundred_probe') IS NULL")) {
            Require(!reload && p.Entity.Level==70 && p.Entity.Class==13 && FreshTrainingSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_seventy_probe")));
            Sql("CREATE TABLE revora_fresh_hundred_probe(uid bigint PRIMARY KEY,stage text,kills integer,hits integer,snapshot text,balls integer NOT NULL DEFAULT 0,ball_day text);INSERT INTO revora_fresh_hundred_probe (uid,stage,kills,hits,snapshot) VALUES(@uid,'training',0,0,@snapshot)","uid",(long)uid,"snapshot",FreshTrainingSnapshot(p));
        }
        Require(FreshTrainingSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_hundred_probe")));
        int kills=Convert.ToInt32(Sql("SELECT kills FROM revora_fresh_hundred_probe")),hits=Convert.ToInt32(Sql("SELECT hits FROM revora_fresh_hundred_probe"));
        string stage=Convert.ToString(Sql("SELECT stage FROM revora_fresh_hundred_probe"));
        if(!reload && stage=="training") {
            int balls=Convert.ToInt32(Sql("SELECT balls FROM revora_fresh_hundred_probe"));
            while(p.Entity.Level<100 && balls<10) {
                var ball=p.Inventory.Objects.FirstOrDefault(i=>i.ID==723700);if(ball==null){CareerBuy(p,723700);ball=p.Inventory.Objects.Single(i=>i.ID==723700);}
                byte level=p.Entity.Level;ulong exp=p.Entity.Experience;ItemHandler.UseItem(ball,p);
                Require(!p.Inventory.ContainsUID(ball.UID) && (p.Entity.Level>level || p.Entity.Experience>exp) && !PirateFenced(p));balls++;
                Sql("UPDATE revora_fresh_hundred_probe SET balls=@balls,ball_day=(SELECT row_to_json(d)::text FROM revora_exp_ball_days d WHERE uid=@uid)","balls",balls,"uid",(long)uid);
                FreshHundredSave(p,"training",kills,hits);
                string onceBall=FreshTrainingSnapshot(p);ItemHandler.UseItem(ball,p);Require(onceBall==FreshTrainingSnapshot(p));
                Console.WriteLine("[fresh-hundred] purchased-ball="+balls+" level="+p.Entity.Level+" exp="+p.Entity.Experience);
            }
            FreshEquip(p,420009,5);FreshHundredSave(p,"training",kills,hits);
            p.Attackable=true;p.CantAttack=Time32.Now.AddSeconds(-1);
            while(p.Entity.Level<100 && kills<4000) {
                var body=new ushort[]{1002,1011,1020,1000,1015}.SelectMany(id=>Kernel.Maps[id].Entities.Values).Where(e=>e.MonsterInfo!=null && !e.Dead && e.MonsterInfo.Level<=p.Entity.Level+5 && e.MonsterInfo.Level>=65 && e.MaxHitpoints<100000
                    && e.MonsterInfo.ID>=1 && e.MonsterInfo.ID<=30 && !e.MonsterInfo.Boss && !e.MonsterInfo.Guard).OrderByDescending(e=>e.MonsterInfo.Level).ThenBy(e=>e.UID).FirstOrDefault();Require(body!=null);
                if(p.Entity.MapID!=body.MapID)p.Entity.Teleport(body.MapID,(ushort)(body.X+1),body.Y);else {p.Entity.X=(ushort)(body.X+1);p.Entity.Y=body.Y;}
                p.Screen.Add(body);uint hp=body.Hitpoints;int strikes=0;byte before=p.Entity.Level;
                while(!body.Dead && strikes<500) {p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);Attack.HandleAttack(new Attack(true){Attacker=uid,Attacked=body.UID,X=body.X,Y=body.Y,AttackType=Attack.Melee},p);strikes++;hits++;if(strikes==1)Require(body.Hitpoints<hp);}
                Require(body.Dead && !p.Entity.Dead && !PirateFenced(p));kills++;
                FreshHundredSave(p,"training",kills,hits);
                if(before!=p.Entity.Level || kills%50==0)Console.WriteLine("[fresh-hundred] kills="+kills+" family="+body.MonsterInfo.ID+" name="+body.MonsterInfo.Name+" hp="+hp+" strikes="+strikes+" level="+p.Entity.Level+" exp="+p.Entity.Experience);
            }
            Require(p.Entity.Level>=100 && p.Entity.Class==13);
            FreshHundredSave(p,"trained",kills,hits);stage="trained";
        }
        if(!reload && stage=="trained") {
            Require(!p.Inventory.Contains(1088001,1));uint before=p.Entity.ConquerPoints;CareerBuy(p,1088001);
            var material=p.Inventory.Objects.Single(i=>i.ID==1088001);Require(material.StackSize==1 && !material.Bound);
            Sql("ALTER TABLE revora_fresh_hundred_probe ADD COLUMN meteor bigint,ADD COLUMN price bigint;UPDATE revora_fresh_hundred_probe SET meteor=@item,price=@price","item",(long)material.UID,"price",(long)(before-p.Entity.ConquerPoints));
            FreshHundredSave(p,"material",kills,hits);stage="material";
        }
        if(!reload && stage=="material") {
            uint meteor=Convert.ToUInt32(Sql("SELECT meteor FROM revora_fresh_hundred_probe"));Require(p.Inventory.ContainsUID(meteor));uint cp=p.Entity.ConquerPoints;
            p.Entity.Teleport(1004,50,50);HeartTalk(p,10022,1);HeartTalk(p,10022,3);Require(p.Entity.Class==14 && !PirateFenced(p) && !p.Inventory.ContainsUID(meteor) && p.Entity.ConquerPoints==cp);
            FreshHundredSave(p,"promoted",kills,hits);stage="promoted";
        }
        if(!reload && stage=="promoted") {
            uint cps=p.Entity.ConquerPoints,bound=p.Entity.BoundCps;ulong silver=p.Entity.Money;int count=p.Inventory.Count;
            ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==727990),p);
            Require(!PirateFenced(p) && p.Inventory.Count==count+3 && p.Entity.ConquerPoints==cps && p.Entity.BoundCps==bound && p.Entity.Money==silver);
            foreach(uint type in new uint[]{204009,201009,202009}) {var reward=p.Inventory.Objects.Single(i=>i.ID==type);Require(reward.Bound && reward.Plus==12 && reward.Bless==(type==204009?0:1));}
            Require(p.Inventory.Objects.Single(i=>i.ID==201009).SocketOne==Enums.Gem.SuperThunderGem && p.Inventory.Objects.Single(i=>i.ID==201009).SocketTwo==Enums.Gem.SuperThunderGem);
            Require(p.Inventory.Objects.Single(i=>i.ID==202009).SocketOne==Enums.Gem.SuperGloryGem && p.Inventory.Objects.Single(i=>i.ID==202009).SocketTwo==Enums.Gem.SuperGloryGem);
            Require(p.Inventory.Objects.Single(i=>i.ID==204009).SocketOne==Enums.Gem.SuperThunderGem && p.Inventory.Objects.Single(i=>i.ID==204009).SocketTwo==Enums.Gem.SuperGloryGem);
            FreshHundredSave(p,"claimed",kills,hits);
        }
        Require(Convert.ToString(Sql("SELECT stage FROM revora_fresh_hundred_probe"))=="claimed" && p.Entity.Level>=100 && p.Entity.Class==14);
        Require(FreshTrainingSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_hundred_probe")));
        Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10822 AND source='class-promotion-v1'","uid",(long)uid)==1);
        Require((long)Sql("SELECT count(*) FROM revora_custom_starter_claims r JOIN items i ON i.uid=ANY(r.rewards) AND i.entityid=r.uid WHERE r.uid=@uid AND r.reborn=0 AND r.milestone=100 AND i.id IN(204009,201009,202009) AND i.bound=1 AND i.plus=12","uid",(long)uid)==3);
        Require(Convert.ToString(Sql("SELECT ball_day FROM revora_fresh_hundred_probe"))==Convert.ToString(Sql("SELECT row_to_json(d)::text FROM revora_exp_ball_days d WHERE uid=@uid","uid",(long)uid)));
        string once=FreshTrainingSnapshot(p);ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==727990),p);HeartTalk(p,10022,3);Require(!PirateFenced(p) && once==FreshTrainingSnapshot(p));
        Console.WriteLine("[career-process] career-fresh-hundred-"+(reload?"reload":"earn")+"-passed uid="+uid+" level="+p.Entity.Level+" class="+p.Entity.Class+" kills="+kills+" hits="+hits+" cps="+p.Entity.ConquerPoints);
        Server.CommandsAI("@close");throw new InvalidOperationException("Fresh milestone did not close.");
    }
}
