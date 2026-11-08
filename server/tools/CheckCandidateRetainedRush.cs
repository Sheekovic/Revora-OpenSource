using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static partial class CheckCandidateArenaLifecycle {
    static string RetainedRushSnapshot(GameState p,GameState recipient,string eliteRun) {
        return Convert.ToString(Sql(@"SELECT json_build_object('career',@career::json,
'progress',(SELECT json_agg(r ORDER BY run_id) FROM revora_boss_rush_progress r WHERE entityid=@uid),
'runs',(SELECT json_agg(r ORDER BY run_id) FROM revora_boss_rush_runs r WHERE entityid=@uid))::text",
"career",RetainedEliteCombatSnapshot(p,recipient,eliteRun),"uid",(long)CareerUid));
    }
    static void RetainedRushProcess(string phase) {
        bool earn=phase.EndsWith("earn"),claim=phase.EndsWith("claim");
        careerRitesFixture=true;combinedCollectionFixture=true;nextUid=CareerUid;
        var p=Player(Kernel.Maps[1002],true,1003,true,true);combinedCollectionFixture=false;
        SkillTable.LoadSpells(p);SkillTable.LoadProficiencies(p);SubClassTable.Load(p.Entity);Revora.Database.KnownPersons.LoadMentor(p);
        new CoatStorage().Login(p);new TitleStorage().Login(p);p.LoadItemStats();ChiTable.Load(p);WayofHeroes.Load(p);EntityVariableTable.Load(p);Call(typeof(Revora.Database.JiangHu),"LoadClient",p);
        nextUid=4294918104;var recipient=Player(Kernel.Maps[1002],true);recipient.LoadItemStats();ChiTable.Load(recipient);WayofHeroes.Load(recipient);
        nextUid=4294918206;var storage=Player(Kernel.Maps[1002],true);storage.LoadItemStats();
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));

        string eliteRun=Convert.ToString(Sql("SELECT run FROM revora_retained_elite_combat_probe"));
        var feature=Feature("BossRushSystem");
        if(earn) {
            bool fresh=(bool)Sql("SELECT to_regclass('revora_retained_rush_probe') IS NULL");
            if(!fresh)Require((int)Sql("SELECT step FROM revora_retained_rush_probe")==0);
            Require(RetainedEliteCombatSnapshot(p,recipient,eliteRun)==Convert.ToString(Sql("SELECT snapshot FROM revora_retained_elite_combat_probe")));
            if(fresh)Sql("CREATE TABLE revora_retained_rush_probe(step int,hits int,snapshot text);INSERT INTO revora_retained_rush_probe VALUES(0,0,NULL)");
            Console.WriteLine("[retained-rush] bag="+p.Inventory.Count+" storage-bag="+storage.Inventory.Count);
            if(p.Inventory.Count==40) {
                p.Entity.Teleport(1002,408,330);storage.Entity.Teleport(1002,408,330);
                var gift=p.Inventory.Objects.First(i=>(bool)Call(Feature("AtomicTrade"),"Tradable",p,storage,i));
                CareerTradePacket(p,Trade.Request,storage.Entity.UID);CareerTradePacket(storage,Trade.Request,CareerUid);CareerTradePacket(p,Trade.AddItem,gift.UID);
                Require(p.Trade.Items.Count==1);CareerTradePacket(p,Trade.Accept,0);CareerTradePacket(storage,Trade.Accept,0);
                Require(!p.Inventory.ContainsUID(gift.UID) && storage.Inventory.ContainsUID(gift.UID));
                Require(EntityTable.SaveEntity(p) && EntityTable.SaveEntity(storage));
            }
            MainCareerRoom(p,storage,25);p.Entity.Teleport(1002,408,330);
            feature.GetField("CycleStart",Hidden).SetValue(null,DateTime.Now.AddMinutes(1));Revora.Game.Features.BossRushSystem.Tick();HeartTalk(p,900000010,1);
            var fight=Call(feature,"FindByPlayer",CareerUid);Require(fight!=null && p.Entity.MapID!=1002);
            var arena=(Map)fight.GetType().GetField("Arena").GetValue(fight);int hits=0;
            for(int wave=1;wave<=5;wave++) {
                var alive=arena.Entities.Values.Where(e=>e.EntityFlag==EntityFlag.Monster && !e.Dead).ToArray();Require(alive.Length==1 && (int)fight.GetType().GetField("Boss").GetValue(fight)==wave);
                var boss=alive[0];uint initial=boss.Hitpoints;p.Entity.X=(ushort)(boss.X+1);p.Entity.Y=boss.Y;p.Screen.Add(boss);p.Attackable=true;p.CantAttack=Time32.Now.AddSeconds(-1);
                int waveHits=0;
                while(!boss.Dead && waveHits<20000) {
                    p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);
                    Attack.HandleAttack(new Attack(true){Attacker=CareerUid,Attacked=boss.UID,X=boss.X,Y=boss.Y,AttackType=Attack.Melee},p);
                    waveHits++;hits++;
                    if(waveHits==1)Require(boss.Hitpoints<initial);
                }
                Console.WriteLine("[retained-rush] wave="+wave+" initial-hp="+initial+" hits="+waveHits+" remaining-hp="+boss.Hitpoints);
                Require(boss.Dead && !PirateFenced(p));Call(feature,"TickInstance",fight,DateTime.Now);
            }
            Require((uint)p[Revora.Game.Features.BossRushSystem.ClaimKey]==1 && p.Entity.MapID==1002);
            Require((long)Sql("SELECT count(*) FROM revora_boss_rush_runs WHERE entityid=@uid AND kind=1 AND NOT consumed","uid",(long)CareerUid)==1);
            Require(EntityTable.SaveEntity(p) && EntityTable.SaveEntity(storage));
            Sql("UPDATE revora_retained_rush_probe SET step=1,hits=@hits,snapshot=@snapshot","hits",hits,"snapshot",RetainedRushSnapshot(p,recipient,eliteRun));
        } else {
            Require(RetainedRushSnapshot(p,recipient,eliteRun)==Convert.ToString(Sql("SELECT snapshot FROM revora_retained_rush_probe")));
            if(claim) {
                Require((int)Sql("SELECT step FROM revora_retained_rush_probe")==1);
                Require((long)Sql("SELECT count(*) FROM items WHERE entityid=@uid AND position=0","uid",(long)CareerUid)>40);
                Require((long)Sql("SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0","uid",(long)CareerUid)==p.Inventory.Count);
                uint cp=p.Entity.ConquerPoints,rvp=p.Entity.RevoraPoint;ulong gold=p.Entity.Money;var before=p.Inventory.Objects.Select(i=>i.UID).ToArray();
                Console.WriteLine("[retained-rush] before claim kind="+(uint)p[Revora.Game.Features.BossRushSystem.ClaimKey]+" cp="+cp+" rvp="+rvp+" gold="+gold+" bag="+p.Inventory.Count);
                Revora.Game.Features.BossRushSystem.RecoverPending(p);
                HeartTalk(p,900000010,2);
                Console.WriteLine("[retained-rush] after claim kind="+(uint)p[Revora.Game.Features.BossRushSystem.ClaimKey]+" cp="+p.Entity.ConquerPoints+" rvp="+p.Entity.RevoraPoint+" gold="+p.Entity.Money+" bag="+p.Inventory.Count);

                var rewards=p.Inventory.Objects.Where(i=>!before.Contains(i.UID)).ToArray();
                Require(p.Entity.ConquerPoints==cp+Config.BossRushWinCp && p.Entity.RevoraPoint==rvp+Config.BossRushWinRevoraPoints && p.Entity.Money==gold+Config.BossRushWinGold);
                Require(rewards.Length==Config.BossRushWinPerfectionStones+1 && rewards.Count(i=>i.Bound && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)==1);
                Require((long)Sql("SELECT count(*) FROM revora_boss_rush_runs WHERE entityid=@uid AND kind=1 AND consumed","uid",(long)CareerUid)==1);
                Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_retained_rush_probe SET step=2,snapshot=@snapshot","snapshot",RetainedRushSnapshot(p,recipient,eliteRun));
            }
            Require((int)Sql("SELECT step FROM revora_retained_rush_probe")==2);
            string once=RetainedRushSnapshot(p,recipient,eliteRun);HeartTalk(p,900000010,2);Revora.Game.Features.BossRushSystem.RecoverPending(p);
            Require(once==RetainedRushSnapshot(p,recipient,eliteRun) && !PirateFenced(p));
        }
        Console.WriteLine("[career-process] "+phase+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Retained Rush did not close.");
    }
}
