using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle
{
    static bool dragonExperienceFixture;
    static GameState DragonExperiencePlayer(uint uid){nextUid=uid;var p=Player(Kernel.Maps[1036],true);p.LoadItemStats();Require(p.Map.ID==1036 && !p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));return p;}
    static string DragonExperienceSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object('base',@base::json,
'last',(SELECT lastdragonballuse FROM entities WHERE uid=@uid),
'exchanges',(SELECT json_agg(e ORDER BY day) FROM revora_dragon_exp_uses e WHERE e.uid=@uid))::text","base",EnlightClaimSnapshot(p),"uid",(long)p.Entity.UID));}
    static void DragonExperienceProcess(bool reload){
        dragonExperienceFixture=true;const uint uid=4294913401;var service=Feature("DragonBallExperience");var enlightenment=Feature("EnlightenmentProgress");
        DateTime now=DateTime.Today.AddHours(12).ToUniversalTime();service.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));enlightenment.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        if((bool)Sql("SELECT to_regclass('revora_dragon_exp_probe') IS NULL")){
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN @a AND @b","a",(long)uid,"b",(long)uid+6)==0);
            nextUid=uid;byte[] levels={100,120,130,135,120,120,100};for(int n=0;n<7;n++){
                var p=Player(Kernel.Maps[1036]);Sql("UPDATE entities SET level=@level,reborn=0,mapid=1036,enlightenpoints=0,enlightsreceived=0,enlightmentwait=0,lastdragonballuse=0 WHERE uid=@uid","level",(int)levels[n],"uid",(long)p.Entity.UID);
            }
            Sql("CREATE TABLE revora_dragon_exp_probe(uid bigint PRIMARY KEY,snapshot text)");
        }
        if((long)Sql("SELECT count(*) FROM revora_dragon_exp_probe")==0){
            Require(!reload);
            for(int n=0;n<3;n++){
                var p=DragonExperiencePlayer(uid+(uint)n);int price=n==0?1:n==1?2:10;
                for(int i=0;i<price;i++)RefineryClaimGive(p,1088000);
                string before=DragonExperienceSnapshot(p);
                // An option alone, a distant actor and an expired quote cannot spend.
                p.ActiveNpc=3825;Call(service,"Handle",p,(byte)1);Require(before==DragonExperienceSnapshot(p));
                HeartTalk(p,3825,0);p.Entity.X=1;p.Entity.Y=1;Call(service,"Handle",p,(byte)1);Require(before==DragonExperienceSnapshot(p));
                HeartTalk(p,3825,0);now=now.AddMinutes(3);Call(service,"Handle",p,(byte)1);Require(before==DragonExperienceSnapshot(p));
                ushort points=p.Entity.EnlightenPoints;ulong xp=p.Entity.Experience;byte level=p.Entity.Level;
                HeartTalk(p,3825,0);System.Threading.Tasks.Parallel.For(0,8,_=>Npcs.GetDialog(new NpcRequest(true){NpcID=3825,OptionID=1},p));
                Require(!PirateFenced(p) && p.Inventory.Count==0 && p.Entity.EnlightenPoints==points+price*50 && (p.Entity.Level>level || p.Entity.Experience>xp));
                Require((long)Sql("SELECT count(*) FROM revora_dragon_exp_uses WHERE uid=@uid AND paid=@paid AND points=@points","uid",(long)p.Entity.UID,"paid",price,"points",price*50)==1);
                var extra=RefineryClaimGive(p,1088000);before=DragonExperienceSnapshot(p);HeartTalk(p,3825,0);HeartTalk(p,3825,1);Require(before==DragonExperienceSnapshot(p) && p.Inventory.ContainsUID(extra.UID));
            }
            var capped=DragonExperiencePlayer(uid+3);RefineryClaimGive(capped,1088000);string exact=DragonExperienceSnapshot(capped);HeartTalk(capped,3825,0);HeartTalk(capped,3825,1);Require(exact==DragonExperienceSnapshot(capped));
            var rollback=DragonExperiencePlayer(uid+4);RefineryClaimGive(rollback,1088000);HeartTalk(rollback,3825,0);exact=DragonExperienceSnapshot(rollback);HeartTalk(rollback,3825,1);Require(exact==DragonExperienceSnapshot(rollback));
            RefineryClaimGive(rollback,1088000);exact=DragonExperienceSnapshot(rollback);
            enlightenment.GetField("BeforeCommit",Hidden).SetValue(null,(Action<string>)(kind=>{if(kind=="credit")throw new InvalidOperationException("Injected DragonBall rollback");}));
            try{HeartTalk(rollback,3825,1);}finally{enlightenment.GetField("BeforeCommit",Hidden).SetValue(null,null);}
            Require(PirateFenced(rollback) && exact==DragonExperienceSnapshot(rollback));rollback=DragonExperiencePlayer(uid+4);Require(rollback.Inventory.Count==2);
            var interrupted=DragonExperiencePlayer(uid+5);RefineryClaimGive(interrupted,1088000);RefineryClaimGive(interrupted,1088000);ushort beforePoints=interrupted.Entity.EnlightenPoints;HeartTalk(interrupted,3825,0);
            enlightenment.GetField("BeforeInstall",Hidden).SetValue(null,(Action<string>)(kind=>{if(kind=="credit")throw new InvalidOperationException("Injected DragonBall install interruption");}));
            try{HeartTalk(interrupted,3825,1);}finally{enlightenment.GetField("BeforeInstall",Hidden).SetValue(null,null);}
            Require(PirateFenced(interrupted) && !EntityTable.SaveEntity(interrupted));interrupted=DragonExperiencePlayer(uid+5);Require(interrupted.Inventory.Count==0 && interrupted.Entity.EnlightenPoints==beforePoints+100);
            // A previous-year legacy timestamp must not block the same calendar date this year.
            Sql("UPDATE entities SET lastdragonballuse=@last WHERE uid=@uid","last",now.ToLocalTime().AddYears(-1).Ticks,"uid",(long)uid+6);
            var legacy=DragonExperiencePlayer(uid+6);var stack=RefineryClaimGive(legacy,1088000);
            Sql("UPDATE items SET stacksize=3 WHERE uid=@uid","uid",(long)stack.UID);legacy=DragonExperiencePlayer(uid+6);stack=legacy.Inventory.Objects.Single();
            HeartTalk(legacy,3825,0);HeartTalk(legacy,3825,1);Require(!PirateFenced(legacy) && stack.StackSize==2 && legacy.Inventory.Count==1);
            for(uint id=uid;id<=uid+6;id++){var p=DragonExperiencePlayer(id);Require(EntityTable.SaveEntity(p));Sql("INSERT INTO revora_dragon_exp_probe VALUES(@uid,@snapshot)","uid",(long)id,"snapshot",DragonExperienceSnapshot(p));}
        }
        for(uint id=uid;id<=uid+6;id++){var p=DragonExperiencePlayer(id);Require(DragonExperienceSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_dragon_exp_probe WHERE uid=@uid","uid",(long)id)));}
        var replay=DragonExperiencePlayer(uid);string saved=DragonExperienceSnapshot(replay);HeartTalk(replay,3825,0);HeartTalk(replay,3825,1);Require(saved==DragonExperienceSnapshot(replay));
        Require((long)Sql("SELECT count(*) FROM revora_dragon_exp_uses WHERE uid BETWEEN @a AND @b","a",(long)uid,"b",(long)uid+6)==5);
        System.Console.WriteLine("[career-process] career-dragon-experience-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("DragonBall experience did not close.");
    }
}
