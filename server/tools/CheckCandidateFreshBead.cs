using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshBeadSnapshot(GameState p) { return Convert.ToString(Sql(@"SELECT json_build_object('prior',@prior::json,'beads',(SELECT json_agg(r ORDER BY day) FROM revora_soul_ball_runs r WHERE uid=@uid),'bless',@bless)::text","prior",FreshWorshipSnapshot(p),"uid",(long)p.Entity.UID,"bless",(long)p.Entity.HeavenBlessing)); }
    static void FreshBeadSave(GameState p,string stage,int kills,int hits) { Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_bead_probe SET stage=@stage,kills=@kills,hits=@hits,snapshot=@snapshot","stage",stage,"kills",kills,"hits",hits,"snapshot",FreshBeadSnapshot(p)); }
    static void FreshBeadProcess(string phase) {
        var p=FreshLoadSaved();EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();DialyQuestTable.Load(p);var service=Feature("DailySoulBall");Call(service,"Refresh",p);
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_fresh_bead_probe') IS NULL")) {
            Require(phase.EndsWith("hunt") && FreshWorshipSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_worship_probe")) && (long)Sql("SELECT count(*) FROM revora_soul_ball_runs WHERE uid=@uid","uid",(long)p.Entity.UID)==0);
            Sql("CREATE TABLE revora_fresh_bead_probe(stage text,kills integer,hits integer,snapshot text);INSERT INTO revora_fresh_bead_probe VALUES('new',0,0,@snapshot)","snapshot",FreshBeadSnapshot(p));
        }
        Require(FreshBeadSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_bead_probe")));
        string stage=Convert.ToString(Sql("SELECT stage FROM revora_fresh_bead_probe"));int kills=Convert.ToInt32(Sql("SELECT kills FROM revora_fresh_bead_probe")),hits=Convert.ToInt32(Sql("SELECT hits FROM revora_fresh_bead_probe"));
        if(phase.EndsWith("hunt")) {
            if(stage=="new") {
                p.Entity.Teleport(1002,Config.DailyEnvoyX,Config.DailyEnvoyY);uint cp=p.Entity.ConquerPoints;HeartTalk(p,79882,7);HeartTalk(p,79882,9);
                Require(p.Entity.ConquerPoints==cp-Config.DailySpiritBeadSuperCp && p.Inventory.Objects.Count(i=>i.ID==Config.DailySpiritBeadSuper)==1 && !PirateFenced(p));
                var bead=p.Inventory.Objects.Single(i=>i.ID==Config.DailySpiritBeadSuper);string before=FreshBeadSnapshot(p);ItemHandler.UseItem(bead,p);Require(before==FreshBeadSnapshot(p));
                FreshBeadSave(p,"hunt",kills,hits);
            }
            p.Entity.Teleport(1015,717,576);p.Attackable=true;p.CantAttack=Time32.Now.AddSeconds(-1);var timer=System.Diagnostics.Stopwatch.StartNew();
            while(Convert.ToUInt32(Sql("SELECT spirits FROM revora_soul_ball_runs WHERE uid=@uid","uid",(long)p.Entity.UID))<Config.DailySpiritBeadSuperNeed && kills<1000 && timer.Elapsed.TotalSeconds<150) {
                var body=p.Map.Entities.Values.Where(e=>e.MonsterInfo!=null && !e.Dead && !e.MonsterInfo.Boss && !e.MonsterInfo.Guard && !e.MonsterInfo.Reviver && (e.MonsterInfo.Name=="Birdman" || e.MonsterInfo.Name=="BirdmanL78" || e.MonsterInfo.Name=="BanditL97" || e.MonsterInfo.Name=="BanditL98")).OrderBy(e=>e.UID).FirstOrDefault();Require(body!=null);
                uint before=Convert.ToUInt32(Sql("SELECT spirits FROM revora_soul_ball_runs WHERE uid=@uid","uid",(long)p.Entity.UID));p.Entity.X=(ushort)(body.X+1);p.Entity.Y=body.Y;p.Screen.Add(body);uint hp=body.Hitpoints;int strikes=0;
                while(!body.Dead && strikes<500) {p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);Attack.HandleAttack(new Attack(true){Attacker=p.Entity.UID,Attacked=body.UID,X=body.X,Y=body.Y,AttackType=Attack.Melee},p);hits++;strikes++;if(strikes==1)Require(body.Hitpoints<hp);}
                Require(body.Dead && !p.Entity.Dead && !PirateFenced(p));kills++;uint gain=body.MonsterInfo.Level<70?1u:body.MonsterInfo.Level<100?2u:body.MonsterInfo.Level<120?3u:4u;
                Require(Convert.ToUInt32(Sql("SELECT spirits FROM revora_soul_ball_runs WHERE uid=@uid","uid",(long)p.Entity.UID))==Math.Min(Config.DailySpiritBeadSuperNeed,before+gain));
                if(kills%25==0)FreshBeadSave(p,"hunt",kills,hits);
            }
            Require(Convert.ToUInt32(Sql("SELECT spirits FROM revora_soul_ball_runs WHERE uid=@uid","uid",(long)p.Entity.UID))==Config.DailySpiritBeadSuperNeed);FreshBeadSave(p,"ready",kills,hits);
        } else if(phase.EndsWith("claim")) {
            Require(stage=="ready");var bead=p.Inventory.Objects.Single(i=>i.ID==Config.DailySpiritBeadSuper);uint cp=p.Entity.ConquerPoints,rvp=p.Entity.RevoraPoint,bless=p.Entity.HeavenBlessing;ushort study=p.Entity.SubClasses.StudyPoints;var prior=p.Inventory.Objects.Select(i=>i.UID).ToArray();
            ItemHandler.UseItem(bead,p);Require(!PirateFenced(p) && !p.Inventory.ContainsUID(bead.UID) && p.Entity.ConquerPoints==cp+Config.DailyQuestCp && p.Entity.RevoraPoint==rvp+Config.DailyQuestRevoraPoints && p.Entity.SubClasses.StudyPoints==study+Config.DailySpiritBeadSuperStudy && p.Entity.HeavenBlessing==bless+172800);
            var rewards=p.Inventory.Objects.Where(i=>!prior.Contains(i.UID)).ToArray();Require(rewards.Length==2 && rewards.Any(i=>i.ID==Config.DailyQuestChiToken) && rewards.Any(i=>i.ID==4030101 && i.Bound));
            Require((long)Sql("SELECT count(*) FROM revora_soul_ball_runs WHERE uid=@uid AND completed_day<>'' AND spirits=need AND paid_complete=0 AND NOT paid_finish AND paid_issue=@fee AND rune_uid IS NOT NULL AND soul_uid IS NULL","uid",(long)p.Entity.UID,"fee",(long)Config.DailySpiritBeadSuperCp)==1);FreshBeadSave(p,"paid",kills,hits);
            string before=FreshBeadSnapshot(p);ItemHandler.UseItem(bead,p);Require(before==FreshBeadSnapshot(p));
        } else Require(phase.EndsWith("reload") && stage=="paid");
        Require(FreshBeadSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_bead_probe")));
        System.Console.WriteLine("[career-process] "+phase+"-passed uid="+p.Entity.UID+" kills="+kills+" hits="+hits+" cps="+p.Entity.ConquerPoints+" rvp="+p.Entity.RevoraPoint+" study="+p.Entity.SubClasses.StudyPoints+" blessing="+p.Entity.HeavenBlessing);
        Server.CommandsAI("@close");throw new InvalidOperationException("Fresh bead did not close.");
    }
}
