using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshWarriorSnapshot(GameState p) {return Convert.ToString(Sql(@"SELECT json_build_object('career',@career::json,
'lessons',(SELECT json_agg(r ORDER BY spell,id) FROM revora_warrior_training r WHERE uid=@uid))::text","career",FreshSecondRiteSnapshot(p),"uid",(long)p.Entity.UID));}
    static void FreshWarriorSave(GameState p,int kills,int hits) {
        Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_warrior_probe SET kills=@kills,hits=@hits,snapshot=@snapshot","kills",kills,"hits",hits,"snapshot",FreshWarriorSnapshot(p));
    }
    static void FreshWarriorProcess(string phase) {
        bool reload=phase.EndsWith("reload"),advanced=phase.Contains("advance");
        byte targetLevel=(byte)(advanced?110:70),targetClass=(byte)(advanced?24:23);
        var p=FreshLoadSaved();uint uid=p.Entity.UID;SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && p.Entity.Reborn==2 && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_fresh_warrior_probe') IS NULL")) {
            Require(phase=="career-fresh-warrior-train" && p.Entity.Level==40 && p.Entity.Class==21 && FreshSecondRiteSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_third_shield_probe")));
            Sql("CREATE TABLE revora_fresh_warrior_probe(uid bigint PRIMARY KEY,kills integer,hits integer,snapshot text);INSERT INTO revora_fresh_warrior_probe VALUES(@uid,0,0,@snapshot)","uid",(long)uid,"snapshot",FreshWarriorSnapshot(p));
        }
        string checkpoint=Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_warrior_probe")),current=FreshWarriorSnapshot(p);
        if(current!=checkpoint) {
            // Recover only the failed initial positioning assertion, never profile or reward changes.
            Require(!reload && p.Entity.Level==40 && p.Entity.Class==21 && p.Entity.MapID==1004 && (int)Sql("SELECT kills FROM revora_fresh_warrior_probe")==0);
            Require((bool)Sql("SELECT (@current::jsonb #- '{career,career,career,career,career,character,starter,profile,mapid}' #- '{career,career,career,career,career,character,starter,profile,x}' #- '{career,career,career,career,career,character,starter,profile,y}') = (@prior::jsonb #- '{career,career,career,career,career,character,starter,profile,mapid}' #- '{career,career,career,career,career,character,starter,profile,x}' #- '{career,career,career,career,career,character,starter,profile,y}')","current",current,"prior",checkpoint));
            Sql("UPDATE revora_fresh_warrior_probe SET snapshot=@snapshot","snapshot",current);
            Console.WriteLine("[fresh-warrior] recovered initial position-only fixture movement");
        }
        Require(FreshWarriorSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_warrior_probe")));
        if(!reload && p.Entity.Class==21) {
            p.Entity.Teleport(1004,50,50);HeartTalk(p,10001,0);string before=FreshWarriorSnapshot(p);HeartTalk(p,10001,10);HeartTalk(p,10001,11);
            Require(before==FreshWarriorSnapshot(p) && !p.Spells.ContainsKey(11160) && !p.Spells.ContainsKey(11200));FreshWarriorSave(p,0,0);
        }
        int kills=Convert.ToInt32(Sql("SELECT kills FROM revora_fresh_warrior_probe")),hits=Convert.ToInt32(Sql("SELECT hits FROM revora_fresh_warrior_probe"));
        if(!reload) {
            p.Attackable=true;p.CantAttack=Time32.Now.AddSeconds(-1);var timer=System.Diagnostics.Stopwatch.StartNew();int batch=0;
            while(p.Entity.Level<targetLevel && batch<2500 && timer.Elapsed.TotalSeconds<150) {
                var body=new ushort[]{1002,1011,1020,1000,1015}.SelectMany(id=>Kernel.Maps[id].Entities.Values).Where(e=>e.MonsterInfo!=null && !e.Dead && e.MonsterInfo.Level<=p.Entity.Level+5 && e.MaxHitpoints<100000 && e.MonsterInfo.ID>=1 && e.MonsterInfo.ID<=100 && !e.MonsterInfo.Boss && !e.MonsterInfo.Guard).OrderByDescending(e=>e.MonsterInfo.Level).ThenBy(e=>e.UID).FirstOrDefault();
                if(body==null){System.Threading.Thread.Sleep(500);continue;}
                if(p.Entity.MapID!=body.MapID)p.Entity.Teleport(body.MapID,(ushort)(body.X+1),body.Y);else {p.Entity.X=(ushort)(body.X+1);p.Entity.Y=body.Y;}
                p.Screen.Add(body);uint hp=body.Hitpoints;int strikes=0;byte level=p.Entity.Level;
                while(!body.Dead && strikes<500) {p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);Attack.HandleAttack(new Attack(true){Attacker=uid,Attacked=body.UID,X=body.X,Y=body.Y,AttackType=Attack.Melee},p);strikes++;hits++;if(strikes==1)Require(body.Hitpoints<hp);}
                Require(body.Dead && !p.Entity.Dead && !PirateFenced(p));kills++;batch++;FreshWarriorSave(p,kills,hits);
                if(level!=p.Entity.Level || kills%250==0)Console.WriteLine("[fresh-warrior] kills="+kills+" family="+body.MonsterInfo.ID+" level="+p.Entity.Level+" exp="+p.Entity.Experience);
                if(p.Entity.Class<targetClass && p.Entity.Level>=p.PromoteLevelNeed) {
                    byte before=p.Entity.Class;uint material=p.PromoteItemNeed;uint cps=p.Entity.ConquerPoints;uint item=0;
                    if(material!=0){CareerBuy(p,material);item=p.Inventory.Objects.Single(i=>i.ID==material).UID;}
                    p.Entity.Teleport(1004,50,50);HeartTalk(p,10001,1);HeartTalk(p,10001,3);
                    Require(!PirateFenced(p) && p.Entity.Class==before+1 && (item==0 || !p.Inventory.ContainsUID(item)) && p.Entity.ConquerPoints==cps-(material==0?0:ConquerItemInformation.BaseInformations[material].ConquerPointsWorth));
                    FreshWarriorSave(p,kills,hits);
                    string once=FreshWarriorSnapshot(p);HeartTalk(p,10001,3);Require(once==FreshWarriorSnapshot(p));
                    Console.WriteLine("[fresh-warrior] promoted="+p.Entity.Class+" material="+material);
                }
            }
        }
        if(!reload && p.Entity.Level>=70 && p.Entity.Class>=23) {
            p.Entity.Teleport(1004,50,50);
            foreach(byte option in new byte[]{9,10,11})HeartTalk(p,10001,option);
            Require(!PirateFenced(p) && p.Spells.ContainsKey(10470) && p.Spells.ContainsKey(11160) && p.Spells.ContainsKey(11200));
            Require((long)Sql("SELECT count(*) FROM revora_warrior_training WHERE uid=@uid AND spell IN(10470,11160,11200)","uid",(long)uid)==3);
            FreshWarriorSave(p,kills,hits);
        }
        if(reload) {
            Require(p.Spells.ContainsKey(10470) && p.Spells.ContainsKey(11160) && p.Spells.ContainsKey(11200));
            string once=FreshWarriorSnapshot(p);foreach(byte option in new byte[]{9,10,11})HeartTalk(p,10001,option);Require(once==FreshWarriorSnapshot(p));
        }
        Require(FreshWarriorSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_warrior_probe")) && !PirateFenced(p));
        if(reload)Require(p.Entity.Level>=targetLevel && p.Entity.Class==targetClass);
        Require((long)Sql("SELECT count(*) FROM revora_custom_starter_claims WHERE uid=@uid AND reborn=2","uid",(long)uid)==1);
        Console.WriteLine("[career-process] "+phase+"-passed uid="+uid+" level="+p.Entity.Level+" class="+p.Entity.Class+" kills="+kills+" hits="+hits+" cps="+p.Entity.ConquerPoints);
        Server.CommandsAI("@close");throw new InvalidOperationException("Fresh Warrior journey did not close.");
    }
}
