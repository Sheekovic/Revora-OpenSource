using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static partial class CheckCandidateArenaLifecycle {
    static void FreshThirdShieldSave(GameState p,int kills,int hits) {
        Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_third_shield_probe SET kills=@kills,hits=@hits,snapshot=@snapshot","kills",kills,"hits",hits,"snapshot",FreshSecondRiteSnapshot(p));
    }
    static void FreshThirdShieldProcess(bool reload) {
        var p=FreshLoadSaved();uint uid=p.Entity.UID;SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && p.Entity.Reborn==2 && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_fresh_third_shield_probe') IS NULL")) {
            Require(!reload && p.Entity.Level==15 && p.Entity.Class==21 && FreshSecondRiteSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_second_rite_probe")));
            Sql("CREATE TABLE revora_fresh_third_shield_probe(uid bigint PRIMARY KEY,kills integer,hits integer,snapshot text);INSERT INTO revora_fresh_third_shield_probe VALUES(@uid,0,0,@snapshot)","uid",(long)uid,"snapshot",FreshSecondRiteSnapshot(p));
        }
        Require(FreshSecondRiteSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_third_shield_probe")));
        if(!(bool)Sql("SELECT to_regclass('revora_fresh_second_boost_probe') IS NULL"))Require(p.Entity.DoubleExperienceTime>0 && (long)Sql("SELECT count(*) FROM revora_exp_potion_uses r JOIN revora_fresh_second_boost_probe b ON b.uid=r.uid AND b.item=r.item_uid WHERE r.uid=@uid AND r.item_type=723017 AND r.seconds=3600","uid",(long)uid)==1);
        int kills=Convert.ToInt32(Sql("SELECT kills FROM revora_fresh_third_shield_probe")),hits=Convert.ToInt32(Sql("SELECT hits FROM revora_fresh_third_shield_probe"));
        if(!reload) {
            p.Attackable=true;p.CantAttack=Time32.Now.AddSeconds(-1);var timer=System.Diagnostics.Stopwatch.StartNew();int batch=0;
            while(p.Entity.Level<40 && batch<2500 && timer.Elapsed.TotalSeconds<150) {
                var body=new ushort[]{1002,1011,1020,1000,1015}.SelectMany(id=>Kernel.Maps[id].Entities.Values).Where(e=>e.MonsterInfo!=null && !e.Dead && e.MonsterInfo.Level<=p.Entity.Level+5 && e.MaxHitpoints<100000 && e.MonsterInfo.ID>=1 && e.MonsterInfo.ID<=100 && !e.MonsterInfo.Boss && !e.MonsterInfo.Guard).OrderByDescending(e=>e.MonsterInfo.Level).ThenBy(e=>e.UID).FirstOrDefault();
                if(body==null){System.Threading.Thread.Sleep(500);continue;}
                if(p.Entity.MapID!=body.MapID)p.Entity.Teleport(body.MapID,(ushort)(body.X+1),body.Y);else {p.Entity.X=(ushort)(body.X+1);p.Entity.Y=body.Y;}
                p.Screen.Add(body);uint hp=body.Hitpoints;int strikes=0;byte level=p.Entity.Level;
                while(!body.Dead && strikes<500) {p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);Attack.HandleAttack(new Attack(true){Attacker=uid,Attacked=body.UID,X=body.X,Y=body.Y,AttackType=Attack.Melee},p);strikes++;hits++;if(strikes==1)Require(body.Hitpoints<hp);}
                Require(body.Dead && !p.Entity.Dead && !PirateFenced(p));kills++;batch++;FreshThirdShieldSave(p,kills,hits);
                if(level!=p.Entity.Level || kills%250==0)Console.WriteLine("[fresh-third-shield] kills="+kills+" family="+body.MonsterInfo.ID+" level="+p.Entity.Level+" exp="+p.Entity.Experience);

            }
        }
        var rewardIds=(long[])Sql("SELECT rewards FROM revora_custom_starter_claims WHERE uid=@uid AND reborn=2 AND milestone=1","uid",(long)uid);
        var shield=p.Inventory.Objects.Concat(p.Equipment.GetItems()).Single(i=>i.ID==900009 && rewardIds.Contains((long)i.UID));
        if(!reload && p.Entity.Level>=40 && shield.Position==0) {
            Require(ItemHandler.Equipable(shield,p));p.Filtering=true;var frame=new ItemUsage(true){UID=shield.UID,dwParam=5,Action=ItemUsage.Mode.EquipItem}.ToArray();Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,frame.Length-8,frame);ChoiceWireSend(p,frame);
            Require(shield.Position==5 && p.Equipment.GetItems().Any(i=>i.UID==shield.UID) && !PirateFenced(p));FreshThirdShieldSave(p,kills,hits);
        }
        Require(p.Entity.Level>=40 && shield.Position==5 && shield.Bound && shield.Plus==12 && shield.Bless==7 && shield.SocketOne==Enums.Gem.SuperDragonGem && shield.SocketTwo==Enums.Gem.SuperDragonGem);
        Require(p.Equipment.GetItems().Count(i=>rewardIds.Contains((long)i.UID))==7);
        Require(FreshSecondRiteSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_third_shield_probe")) && !PirateFenced(p));
        if(reload)Require(p.Entity.Level>=40 && p.Entity.Class==21);
        Require((long)Sql("SELECT count(*) FROM revora_custom_starter_claims WHERE uid=@uid AND reborn=2","uid",(long)uid)==1);
        Console.WriteLine("[career-process] career-fresh-third-shield-"+(reload?"reload":"train")+"-passed uid="+uid+" level="+p.Entity.Level+" class="+p.Entity.Class+" kills="+kills+" hits="+hits+" cps="+p.Entity.ConquerPoints);
        Server.CommandsAI("@close");throw new InvalidOperationException("Fresh second life did not close.");
    }
}
