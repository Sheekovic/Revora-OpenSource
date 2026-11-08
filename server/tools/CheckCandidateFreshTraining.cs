using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshTrainingSnapshot(GameState p) {
        return Convert.ToString(Sql(@"SELECT json_build_object('starter',@starter::json,'experience',(SELECT experience FROM entities WHERE uid=@uid),'promotion',(SELECT json_agg(r ORDER BY achievement_id) FROM revora_achievement_evidence r WHERE uid=@uid))::text","starter",FreshStarterSnapshot(p),"uid",(long)p.Entity.UID));
    }
    static void FreshEquip(GameState p,uint type,byte slot) {
        var item=p.Inventory.Objects.SingleOrDefault(i=>i.ID==type);if(item==null)return;
        ItemHandler.EquipItem(new ItemUsage(true){UID=item.UID,dwParam=slot,Action=ItemUsage.Mode.EquipItem},p);
    }
    static void FreshTrainingProcess(bool reload) {
        var p=FreshStarterSession();uint uid=Convert.ToUInt32(Sql("SELECT uid FROM revora_fresh_starter_probe"));p.Account.EntityID=uid;
        p.Entity=new Entity(EntityFlag.Player,false){Owner=p,UID=uid};Require((bool)Call(Feature("SessionInstall"),"TryClaim",uid,p));
        p.Quests=new Quests(p);Require((bool)Call(typeof(EntityTable),"LoadEntityState",p));p.Action=2;p.Entity.FullyLoaded=true;p.BindWorldMap(p.Entity.MapID);ConquerItemTable.LoadItems(p);
        SkillTable.LoadSpells(p);SkillTable.LoadProficiencies(p);p.LoadItemStats();
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if(!reload) {
            Require(p.Entity.Level==1 && p.Entity.Class==10 && FreshStarterSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_starter_probe")));
            Require((bool)Sql("SELECT to_regclass('revora_fresh_training_probe') IS NULL"));
            foreach(var entry in new[]{Tuple.Create(410009u,(byte)4),Tuple.Create(120009u,(byte)2),Tuple.Create(160019u,(byte)6),Tuple.Create(118009u,(byte)1),Tuple.Create(130009u,(byte)3)}) {
                int count=p.Inventory.Count;FreshEquip(p,entry.Item1,entry.Item2);Require(p.Inventory.Count==count && p.Equipment.TryGetItem(entry.Item2)==null);
            }
            FreshEquip(p,150009,6); // Position mapping is checked below; ring is slot6 in this client.
            Require(p.Equipment.GetItems().Any(i=>i.ID==150009));
            int kills=0,hits=0;p.Attackable=true;p.CantAttack=Time32.Now.AddSeconds(-1);
            while(p.Entity.Level<15 && kills<1000) {
                var body=p.Map.Entities.Values.Where(e=>e.MonsterInfo!=null && e.MonsterInfo.Name=="Pheasant" && !e.Dead).OrderBy(e=>e.UID).FirstOrDefault();Require(body!=null);
                p.Entity.X=(ushort)(body.X+1);p.Entity.Y=body.Y;p.Screen.Add(body);uint hp=body.Hitpoints;int strikes=0;
                while(!body.Dead && strikes<500) {p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);Attack.HandleAttack(new Attack(true){Attacker=uid,Attacked=body.UID,X=body.X,Y=body.Y,AttackType=Attack.Melee},p);strikes++;hits++;if(strikes==1)Require(body.Hitpoints<hp);}
                Require(body.Dead && !p.Entity.Dead && !PirateFenced(p));kills++;
                Console.WriteLine("[fresh-training] kill="+kills+" family="+body.MonsterInfo.ID+" hp="+hp+" strikes="+strikes+" level="+p.Entity.Level+" exp="+p.Entity.Experience);
                if(p.Entity.Level>=5)FreshEquip(p,410009,4);
                if(p.Entity.Level>=7)FreshEquip(p,120009,2);
                if(p.Entity.Level>=10)FreshEquip(p,160019,8);
            }
            Require(p.Entity.Level>=15 && p.Entity.Class==10);Require(EntityTable.SaveEntity(p));
            p.Entity.Teleport(1004,50,50);HeartTalk(p,10022,1);HeartTalk(p,10022,3);Require(p.Entity.Class==11 && !PirateFenced(p));
            FreshEquip(p,118009,1);FreshEquip(p,130009,3);Require(p.Equipment.GetItems().Any(i=>i.ID==118009) && p.Equipment.GetItems().Any(i=>i.ID==130009));
            Require(EntityTable.SaveEntity(p));Sql("CREATE TABLE revora_fresh_training_probe(uid bigint PRIMARY KEY,kills integer,hits integer,snapshot text);INSERT INTO revora_fresh_training_probe VALUES(@uid,@kills,@hits,@snapshot)","uid",(long)uid,"kills",kills,"hits",hits,"snapshot",FreshTrainingSnapshot(p));
        }
        Require(p.Entity.Class==11 && p.Entity.Level>=15 && FreshTrainingSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_training_probe")));
        Console.WriteLine("[fresh-training] equipment="+string.Join(",",p.Equipment.GetItems().Select(i=>i.ID+":"+i.Position))+" cps="+p.Entity.ConquerPoints+" bound="+p.Entity.BoundCps+" silver="+p.Entity.Money);
        Require(p.Equipment.GetItems().Where(i=>i.Position<20).Select(i=>i.ID).OrderBy(i=>i).SequenceEqual(new uint[]{118009,130009,120009,150009,160019,410009}.OrderBy(i=>i)));
        int killsSaved=Convert.ToInt32(Sql("SELECT kills FROM revora_fresh_training_probe"));
        ulong earned=p.Entity.ConquerPoints-10000UL;
        Require(Revora.Config.KillConquerPointsEnabled && p.Entity.VIPLevel>=Revora.Config.KillConquerPointsMinVip);
        Require(earned>0 && earned>=(ulong)killsSaved*Revora.Config.KillConquerPointsAlways
            && earned<=(ulong)killsSaved*((ulong)Revora.Config.KillConquerPointsAlways+Revora.Config.KillConquerPointsAmount5+Revora.Config.KillConquerPointsAmount50+Revora.Config.KillConquerPointsAmount250));
        Require(p.Entity.BoundCps==50000 && p.Entity.Money==1100000);
        string before=FreshTrainingSnapshot(p);HeartTalk(p,10022,3);Require(before==FreshTrainingSnapshot(p));
        Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10819 AND source='class-promotion-v1'","uid",(long)uid)==1);
        Console.WriteLine("[career-process] career-fresh-training-"+(reload?"reload":"earn")+"-passed uid="+uid+" level="+p.Entity.Level+" class="+p.Entity.Class);
        Server.CommandsAI("@close");throw new InvalidOperationException("Fresh training did not close.");
    }
}
