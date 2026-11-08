using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static partial class CheckCandidateArenaLifecycle {
    static GameState FreshLoadSaved(bool townWarehouse=true,bool phoenixWarehouse=false,bool apeWarehouse=false,bool desertWarehouse=false,bool birdWarehouse=false) {
        var p=FreshStarterSession();uint uid=Convert.ToUInt32(Sql("SELECT uid FROM revora_fresh_starter_probe"));p.Account.EntityID=uid;
        if(townWarehouse)p.Warehouses.Add(Revora.Game.ConquerStructures.Warehouse.WarehouseID.TwinCity,new Revora.Game.ConquerStructures.Warehouse(p,Revora.Game.ConquerStructures.Warehouse.WarehouseID.TwinCity));
        if(phoenixWarehouse)p.Warehouses.Add(Revora.Game.ConquerStructures.Warehouse.WarehouseID.PhoenixCity,new Revora.Game.ConquerStructures.Warehouse(p,Revora.Game.ConquerStructures.Warehouse.WarehouseID.PhoenixCity));
        if(apeWarehouse)p.Warehouses.Add(Revora.Game.ConquerStructures.Warehouse.WarehouseID.ApeCity,new Revora.Game.ConquerStructures.Warehouse(p,Revora.Game.ConquerStructures.Warehouse.WarehouseID.ApeCity));
        if(desertWarehouse)p.Warehouses.Add(Revora.Game.ConquerStructures.Warehouse.WarehouseID.DesertCity,new Revora.Game.ConquerStructures.Warehouse(p,Revora.Game.ConquerStructures.Warehouse.WarehouseID.DesertCity));
        if(birdWarehouse)p.Warehouses.Add(Revora.Game.ConquerStructures.Warehouse.WarehouseID.BirdCity,new Revora.Game.ConquerStructures.Warehouse(p,Revora.Game.ConquerStructures.Warehouse.WarehouseID.BirdCity));
        p.Entity=new Entity(EntityFlag.Player,false){Owner=p,UID=uid};Require((bool)Call(Feature("SessionInstall"),"TryClaim",uid,p));
        p.Quests=new Quests(p);Require((bool)Call(typeof(EntityTable),"LoadEntityState",p));p.Action=2;p.Entity.FullyLoaded=true;p.BindWorldMap(p.Entity.MapID);ConquerItemTable.LoadItems(p);
        SkillTable.LoadSpells(p);SkillTable.LoadProficiencies(p);p.LoadItemStats();return p;
    }
    static void FreshMilestoneSave(GameState p,string stage,int kills,int hits) {
        Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_milestone_probe SET stage=@stage,kills=@kills,hits=@hits,snapshot=@snapshot","stage",stage,"kills",kills,"hits",hits,"snapshot",FreshTrainingSnapshot(p));
    }
    static void FreshMilestoneProcess(bool reload) {
        var p=FreshLoadSaved();uint uid=p.Entity.UID;Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_fresh_milestone_probe') IS NULL")) {
            Require(!reload && p.Entity.Level==15 && p.Entity.Class==11 && FreshTrainingSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_training_probe")));
            Sql("CREATE TABLE revora_fresh_milestone_probe(uid bigint PRIMARY KEY,stage text,kills integer,hits integer,snapshot text);INSERT INTO revora_fresh_milestone_probe VALUES(@uid,'training',0,0,@snapshot)","uid",(long)uid,"snapshot",FreshTrainingSnapshot(p));
        }
        Require(FreshTrainingSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_milestone_probe")));
        int kills=Convert.ToInt32(Sql("SELECT kills FROM revora_fresh_milestone_probe")),hits=Convert.ToInt32(Sql("SELECT hits FROM revora_fresh_milestone_probe"));
        string stage=Convert.ToString(Sql("SELECT stage FROM revora_fresh_milestone_probe"));
        if(!reload && stage=="training") {
            p.Attackable=true;p.CantAttack=Time32.Now.AddSeconds(-1);
            while(p.Entity.Level<40 && kills<2000) {
                var body=new ushort[]{1002,1011}.SelectMany(id=>Kernel.Maps[id].Entities.Values).Where(e=>e.MonsterInfo!=null && !e.Dead && e.MonsterInfo.Level<=p.Entity.Level+5 && e.MonsterInfo.Level>=10 && e.MaxHitpoints<100000
                    && new[]{"Robin","TurtleDove","Turtledove","FlyingRooster","Apparition","WingedSnake","Bandit"}.Contains(e.MonsterInfo.Name)).OrderByDescending(e=>e.MonsterInfo.Level).ThenBy(e=>e.UID).FirstOrDefault();Require(body!=null);
                if(p.Entity.MapID!=body.MapID)p.Entity.Teleport(body.MapID,(ushort)(body.X+1),body.Y);else {p.Entity.X=(ushort)(body.X+1);p.Entity.Y=body.Y;}
                p.Screen.Add(body);uint hp=body.Hitpoints;int strikes=0;byte before=p.Entity.Level;
                while(!body.Dead && strikes<500) {p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);Attack.HandleAttack(new Attack(true){Attacker=uid,Attacked=body.UID,X=body.X,Y=body.Y,AttackType=Attack.Melee},p);strikes++;hits++;if(strikes==1)Require(body.Hitpoints<hp);}
                Require(body.Dead && !p.Entity.Dead && !PirateFenced(p));kills++;
                FreshMilestoneSave(p,"training",kills,hits);
                if(before!=p.Entity.Level || kills%50==0)Console.WriteLine("[fresh-milestone] kills="+kills+" family="+body.MonsterInfo.ID+" name="+body.MonsterInfo.Name+" hp="+hp+" strikes="+strikes+" level="+p.Entity.Level+" exp="+p.Entity.Experience);
            }
            Require(p.Entity.Level>=40 && p.Entity.Class==11);
            p.Entity.Teleport(1004,50,50);HeartTalk(p,10022,1);HeartTalk(p,10022,3);Require(p.Entity.Class==12 && !PirateFenced(p));
            FreshMilestoneSave(p,"promoted",kills,hits);stage="promoted";
        }
        if(!reload && stage=="promoted") {
            uint cps=p.Entity.ConquerPoints,bound=p.Entity.BoundCps;ulong silver=p.Entity.Money;int count=p.Inventory.Count;
            ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==727990),p);
            Require(!PirateFenced(p) && p.Inventory.Count==count+1 && p.Entity.ConquerPoints==cps && p.Entity.BoundCps==bound && p.Entity.Money==silver);
            var mount=p.Inventory.Objects.Single(i=>i.ID==300000);Require(mount.Bound && mount.Plus==12 && mount.SocketOne==0 && mount.SocketTwo==0 && new uint[]{0xff9600,0x00ff96,0x9600ff}.Contains((uint)mount.SocketProgress));
            FreshMilestoneSave(p,"claimed",kills,hits);
        }
        Require(Convert.ToString(Sql("SELECT stage FROM revora_fresh_milestone_probe"))=="claimed" && p.Entity.Level>=40 && p.Entity.Class==12);
        Require(FreshTrainingSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_milestone_probe")));
        Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10820 AND source='class-promotion-v1'","uid",(long)uid)==1);
        Require((long)Sql("SELECT count(*) FROM revora_custom_starter_claims r JOIN items i ON i.uid=ANY(r.rewards) AND i.entityid=r.uid WHERE r.uid=@uid AND r.reborn=0 AND r.milestone=40 AND i.id=300000 AND i.bound=1 AND i.plus=12 AND i.bless=0 AND i.socketone=0 AND i.sockettwo=0","uid",(long)uid)==1);
        string once=FreshTrainingSnapshot(p);ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==727990),p);HeartTalk(p,10022,3);Require(!PirateFenced(p) && once==FreshTrainingSnapshot(p));
        Console.WriteLine("[career-process] career-fresh-milestone-"+(reload?"reload":"earn")+"-passed uid="+uid+" level="+p.Entity.Level+" class="+p.Entity.Class+" kills="+kills+" hits="+hits+" cps="+p.Entity.ConquerPoints);
        Server.CommandsAI("@close");throw new InvalidOperationException("Fresh milestone did not close.");
    }
}
