using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static partial class CheckCandidateArenaLifecycle {
    static string RetainedBeastSnapshot(GameState p,GameState recipient,string eliteRun) {
        return Convert.ToString(Sql(@"SELECT json_build_object('career',@career::json,
'results',(SELECT json_agg(r ORDER BY run) FROM revora_beast_results r WHERE uid=@uid),
'admissions',(SELECT json_agg(r ORDER BY cycle) FROM revora_beast_admissions r WHERE uid=@uid))::text",
            "career",RetainedExtractionSnapshot(p,recipient,eliteRun),"uid",(long)CareerUid));
    }
    static void RetainedBeastProcess(string phase) {
        bool earn=phase.EndsWith("earn"),claim=phase.EndsWith("claim");
        careerRitesFixture=true;combinedCollectionFixture=true;nextUid=CareerUid;
        var p=Player(Kernel.Maps[1002],true,1003,true,true);combinedCollectionFixture=false;
        SkillTable.LoadSpells(p);SkillTable.LoadProficiencies(p);SubClassTable.Load(p.Entity);Revora.Database.KnownPersons.LoadMentor(p);
        new CoatStorage().Login(p);new TitleStorage().Login(p);p.LoadItemStats();ChiTable.Load(p);WayofHeroes.Load(p);EntityVariableTable.Load(p);Call(typeof(Revora.Database.JiangHu),"LoadClient",p);
        nextUid=4294918104;var recipient=Player(Kernel.Maps[1002],true);recipient.LoadItemStats();ChiTable.Load(recipient);WayofHeroes.Load(recipient);
        nextUid=4294918206;var storage=Player(Kernel.Maps[1002],true);storage.LoadItemStats();
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));


        string eliteRun=Convert.ToString(Sql("SELECT run FROM revora_retained_elite_combat_probe"));var feature=Feature("BeastTrialSystem");
        if(earn) {
            Require((bool)Sql("SELECT to_regclass('revora_retained_beast_probe') IS NULL"));
            Require(RetainedExtractionSnapshot(p,recipient,eliteRun)==Convert.ToString(Sql("SELECT snapshot FROM revora_retained_extraction_probe")));
            p.Entity.Teleport(1002,408,330);feature.GetField("CycleStart",Hidden).SetValue(null,DateTime.Now.AddMinutes(1));Revora.Game.Features.BeastTrialSystem.Tick();HeartTalk(p,900000009,1);
            var fight=Call(feature,"FindByPlayer",CareerUid);Require(fight!=null);var arena=(Map)fight.GetType().GetField("Arena").GetValue(fight);Require(p.Entity.MapID==arena.ID && arena.ID!=700);
            int kills=0,hits=0;
            for(int wave=1;wave<=8;wave++) {
                Require((int)fight.GetType().GetField("Wave").GetValue(fight)==wave);
                var alive=arena.Entities.Values.Where(e=>e.EntityFlag==EntityFlag.Monster && !e.Dead).ToArray();Require(alive.Length>0);
                Call(feature,"TickInstance",fight,DateTime.Now);Require((int)fight.GetType().GetField("Wave").GetValue(fight)==wave);
                int waveHits=0;
                foreach(var boss in alive) {
                    p.Entity.X=(ushort)(boss.X+1);p.Entity.Y=boss.Y;p.Screen.Add(boss);p.Attackable=true;p.CantAttack=Time32.Now.AddSeconds(-1);uint initial=boss.Hitpoints;int attempts=0;
                    while(!boss.Dead && attempts<20000) {
                        p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);
                        Attack.HandleAttack(new Attack(true){Attacker=CareerUid,Attacked=boss.UID,X=boss.X,Y=boss.Y,AttackType=Attack.Melee},p);attempts++;hits++;waveHits++;
                        if(attempts==1)Require(boss.Hitpoints<initial);
                    }
                    Require(boss.Dead && !PirateFenced(p));kills++;
                }
                Console.WriteLine("[retained-beast] wave="+wave+" kills="+alive.Length+" hits="+waveHits);Call(feature,"TickInstance",fight,DateTime.Now);
            }
            Require(p.Entity.MapID==1002 && (bool)fight.GetType().GetField("Closed").GetValue(fight));
            Require((long)Sql("SELECT count(*) FROM revora_beast_results WHERE uid=@uid AND kind=1 AND NOT imported","uid",(long)CareerUid)==1);
            Require((long)Sql("SELECT count(*) FROM revora_beast_admissions WHERE uid=@uid AND ended AND returned","uid",(long)CareerUid)==1);
            Require(EntityTable.SaveEntity(p));Sql("CREATE TABLE revora_retained_beast_probe(step int,kills int,hits int,snapshot text);INSERT INTO revora_retained_beast_probe VALUES(1,@kills,@hits,@snapshot)","kills",kills,"hits",hits,"snapshot",RetainedBeastSnapshot(p,recipient,eliteRun));
        } else {
            Require(RetainedBeastSnapshot(p,recipient,eliteRun)==Convert.ToString(Sql("SELECT snapshot FROM revora_retained_beast_probe")));Revora.Game.Features.BeastTrialSystem.OnLogin(p);Require(p.Entity.MapID==1002);
            if(claim) {
                Require((int)Sql("SELECT step FROM revora_retained_beast_probe")==1);uint cp=p.Entity.ConquerPoints,rvp=p.Entity.RevoraPoint;ulong gold=p.Entity.Money;var before=p.Inventory.Objects.Select(i=>i.UID).ToArray();
                HeartTalk(p,900000009,2);var added=p.Inventory.Objects.Where(i=>!before.Contains(i.UID)).ToArray();
                Require(p.Entity.ConquerPoints==cp+Config.BeastTrialWinCp && p.Entity.RevoraPoint==rvp+Config.BeastTrialWinRevoraPoints && p.Entity.Money==gold+Config.BeastTrialWinGold);
                Require(added.Length==1 && added[0].Bound && ConquerItemInformation.BaseInformations[added[0].ID].PurificationLevel==7);
                Require((long)Sql("SELECT count(*) FROM revora_beast_results WHERE uid=@uid AND kind=1 AND imported","uid",(long)CareerUid)==1 && (uint)p[Revora.Game.Features.BeastTrialSystem.ClaimKey]==0);
                Console.WriteLine("[retained-beast] prize cp="+Config.BeastTrialWinCp+" rvp="+Config.BeastTrialWinRevoraPoints+" gold="+Config.BeastTrialWinGold);
                Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_retained_beast_probe SET step=2,snapshot=@snapshot","snapshot",RetainedBeastSnapshot(p,recipient,eliteRun));
            }
            Require((int)Sql("SELECT step FROM revora_retained_beast_probe")==2);string once=RetainedBeastSnapshot(p,recipient,eliteRun);HeartTalk(p,900000009,2);Revora.Game.Features.BeastTrialSystem.OnLogin(p);Require(once==RetainedBeastSnapshot(p,recipient,eliteRun) && !PirateFenced(p));
        }
        Console.WriteLine("[career-process] "+phase+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Retained Beast did not close.");
    }
}
