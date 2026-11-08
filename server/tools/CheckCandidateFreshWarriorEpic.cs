using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshWarriorEpicSnapshot(GameState p) {return Convert.ToString(Sql(@"SELECT json_build_object('prior',@prior::json,'quest',(SELECT row_to_json(r) FROM revora_warrior_epic r WHERE uid=@uid),'steps',(SELECT json_agg(r ORDER BY stage) FROM revora_warrior_epic_steps r WHERE uid=@uid),'encounters',(SELECT json_agg(r ORDER BY id) FROM revora_warrior_epic_encounters r WHERE uid=@uid),'rewards',(SELECT json_agg(r ORDER BY slot) FROM revora_warrior_epic_rewards r WHERE uid=@uid))::text","prior",FreshDailyClaimSnapshot(p),"uid",(long)p.Entity.UID));}
    static void FreshWarriorEpicSave(GameState p,string stage,int hits) {Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_warrior_epic_probe SET stage=@stage,hits=@hits,snapshot=@snapshot","stage",stage,"hits",hits,"snapshot",FreshWarriorEpicSnapshot(p));}
    static void FreshWarriorEpicProcess(string phase) {
        var p=FreshLoadSaved();EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_fresh_warrior_epic_probe') IS NULL")) {
            Require(phase.EndsWith("earn") && FreshDailyClaimSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_daily_claim_probe")) && WarriorStage(p)==0);
            Sql("CREATE TABLE revora_fresh_warrior_epic_probe(stage text,hits integer,snapshot text);INSERT INTO revora_fresh_warrior_epic_probe VALUES('new',0,@snapshot)","snapshot",FreshWarriorEpicSnapshot(p));
        }
        Require(FreshWarriorEpicSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_warrior_epic_probe")));
        string stage=Convert.ToString(Sql("SELECT stage FROM revora_fresh_warrior_epic_probe"));int hits=Convert.ToInt32(Sql("SELECT hits FROM revora_fresh_warrior_epic_probe"));
        if(phase.EndsWith("earn")) {
            Require(stage=="new" && p.Inventory.Count<=37);p.Entity.Teleport(1004,42,74);WarriorTalk(p,10001,86);WarriorTalk(p,10001,87);var jade=p.Inventory.Objects.Single(i=>i.ID==3200206);Require(jade.Bound && WarriorStage(p)==1);WarriorTalk(p,10001,87);Require(p.Inventory.Objects.Count(i=>i.ID==3200206)==1);
            ItemUsage.Process(MountUsePacket(jade.UID),p);Require(!p.Inventory.ContainsUID(jade.UID) && p.Map.BaseID==10009 && WarriorStage(p)==2);WarriorTalk(p,19253,1);Require(p.Map.BaseID==10010 && WarriorStage(p)==3);
            foreach(string name in new[]{"SealingStele","TinyMindofEvil"}) {
                if(name=="TinyMindofEvil"){Require(WarriorStage(p)==4 && p.Map.BaseID==10011);WarriorTalk(p,19254,1);Require(WarriorStage(p)==5);}
                var body=WarriorTarget(p,name);p.Attackable=true;p.CantAttack=Time32.Now.AddSeconds(-1);p.Entity.X=body.X;p.Entity.Y=(ushort)(body.Y+1);p.Screen.Add(body);int attacks=0;uint hp=body.Hitpoints;
                while(!body.Dead && attacks<1000) {p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);Attack.HandleAttack(new Attack(true){Attacker=p.Entity.UID,Attacked=body.UID,X=body.X,Y=body.Y,AttackType=Attack.Melee},p);attacks++;hits++;if(attacks==1)Require(body.Hitpoints<hp);}
                Require(body.Dead && !p.Entity.Dead && !PirateFenced(p));System.Console.WriteLine("[fresh-warrior-epic] target="+name+" attacks="+attacks);
            }
            Require(WarriorStage(p)==6 && (long)Sql("SELECT count(*) FROM revora_warrior_epic_encounters WHERE uid=@uid AND state='victory'","uid",(long)p.Entity.UID)==2);WarriorTalk(p,19259,254);Require(p.Entity.MapID==1004);WarriorTalk(p,10001,86);FreshWarriorEpicSave(p,"ready",hits);
        } else if(phase.EndsWith("claim")) {
            Require(stage=="ready" && WarriorStage(p)==6);uint cp=p.Entity.ConquerPoints,rvp=p.Entity.RevoraPoint;ulong silver=p.Entity.Money;var prior=p.Inventory.Objects.Select(i=>i.UID).ToArray();WarriorTalk(p,10001,88);Require(p.Map.BaseID==10011);WarriorTalk(p,19259,1);
            var rewards=p.Inventory.Objects.Where(i=>!prior.Contains(i.UID)).ToArray();Require(WarriorStage(p)==7 && rewards.Length==2 && rewards.All(i=>i.ID==624439 && !i.Bound && i.Plus==12 && i.Bless==7 && i.SocketOne==Revora.Game.Enums.Gem.EmptySocket && i.SocketTwo==Revora.Game.Enums.Gem.EmptySocket) && prior.All(uid=>p.Inventory.ContainsUID(uid)) && !PirateFenced(p));
            Require(p.Entity.ConquerPoints==cp && p.Entity.RevoraPoint==rvp && p.Entity.Money==silver);string before=FreshWarriorEpicSnapshot(p);WarriorTalk(p,19259,1);Require(before==FreshWarriorEpicSnapshot(p));WarriorTalk(p,19259,254);Require(p.Entity.MapID==1004);WarriorTalk(p,10001,86);FreshWarriorEpicSave(p,"paid",hits);
        } else Require(phase.EndsWith("reload") && stage=="paid" && WarriorStage(p)==7);
        if(!phase.EndsWith("earn")) {
            Require((long)Sql("SELECT count(*) FROM revora_warrior_epic_steps WHERE uid=@uid","uid",(long)p.Entity.UID)==7 && (long)Sql("SELECT count(*) FROM revora_warrior_epic_rewards WHERE uid=@uid AND item_type=624439 AND NOT bound","uid",(long)p.Entity.UID)==2);
            string before=FreshWarriorEpicSnapshot(p);WarriorTalk(p,10001,87);WarriorTalk(p,10001,88);WarriorTalk(p,10001,89);Require(before==FreshWarriorEpicSnapshot(p));
        }
        Require(FreshWarriorEpicSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_warrior_epic_probe")));System.Console.WriteLine("[career-process] "+phase+"-passed uid="+p.Entity.UID+" hits="+hits+" level="+p.Entity.Level);Server.CommandsAI("@close");throw new InvalidOperationException("Fresh Warrior epic did not close.");
    }
}
