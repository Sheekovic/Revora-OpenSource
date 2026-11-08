using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshRushSnapshot(GameState p) {
        return Convert.ToString(Sql(@"SELECT json_build_object('career',@career::json,
'progress',(SELECT json_agg(r ORDER BY run_id) FROM revora_boss_rush_progress r WHERE entityid=@uid),
'runs',(SELECT json_agg(r ORDER BY run_id) FROM revora_boss_rush_runs r WHERE entityid=@uid))::text",
"career",FreshExtractionSnapshot(p),"uid",(long)p.Entity.UID));
    }
    static void FreshRushProcess(string phase) {
        bool earn=phase.EndsWith("earn"),claim=phase.EndsWith("claim");
        var p=FreshLoadSaved(true,true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);Server.GamePool=new[]{p};Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        var feature=Feature("BossRushSystem");
        if(earn) {
            bool fresh=(bool)Sql("SELECT to_regclass('revora_fresh_rush_probe') IS NULL");
            if(!fresh)Require((int)Sql("SELECT step FROM revora_fresh_rush_probe")==0);
            Require(FreshExtractionSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_extraction_probe")));
            if(fresh)Sql("CREATE TABLE revora_fresh_rush_probe(step int,hits int,snapshot text);INSERT INTO revora_fresh_rush_probe VALUES(0,0,NULL)");
            FreshClassicTalk(p,1020,10028,0);var bank=p.Warehouses[Revora.Game.ConquerStructures.Warehouse.WarehouseID.ApeCity];int slots=Config.BossRushWinPerfectionStones+1;Require(bank!=null && bank.Count+slots<=60 && p.Inventory.Count==40);
            foreach(var item in p.Inventory.Objects.Where(i=>i.Days==0).Take(slots).ToArray()){var frame=new byte[172];Revora.Network.Writer.WriteUInt16(164,0,frame);Revora.Network.Writer.WriteUInt16(1102,2,frame);Revora.Network.Writer.WriteUInt32(10028,4,frame);frame[12]=1;frame[13]=10;Revora.Network.Writer.WriteUInt32(item.UID,18,frame);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,164,frame);Warehouse.Process(frame,p);Require(!p.Inventory.ContainsUID(item.UID) && bank.ContainsUID(item.UID));}Require(p.Inventory.Count==40-slots);p.Entity.Teleport(1002,408,330);
            feature.GetField("CycleStart",Hidden).SetValue(null,DateTime.Now.AddMinutes(1));Revora.Game.Features.BossRushSystem.Tick();HeartTalk(p,900000010,1);
            var fight=Call(feature,"FindByPlayer",p.Entity.UID);Require(fight!=null && p.Entity.MapID!=1002);
            var arena=(Map)fight.GetType().GetField("Arena").GetValue(fight);int hits=0;
            for(int wave=1;wave<=5;wave++) {
                var alive=arena.Entities.Values.Where(e=>e.EntityFlag==EntityFlag.Monster && !e.Dead).ToArray();Require(alive.Length==1 && (int)fight.GetType().GetField("Boss").GetValue(fight)==wave);
                var boss=alive[0];uint initial=boss.Hitpoints;p.Entity.X=(ushort)(boss.X+1);p.Entity.Y=boss.Y;p.Screen.Add(boss);p.Attackable=true;p.CantAttack=Time32.Now.AddSeconds(-1);
                int waveHits=0;
                while(!boss.Dead && waveHits<20000) {
                    p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);
                    Attack.HandleAttack(new Attack(true){Attacker=p.Entity.UID,Attacked=boss.UID,X=boss.X,Y=boss.Y,AttackType=Attack.Melee},p);
                    waveHits++;hits++;
                    if(waveHits==10)Require(boss.Hitpoints<initial);
                }
                Console.WriteLine("[fresh-rush] wave="+wave+" initial-hp="+initial+" hits="+waveHits+" remaining-hp="+boss.Hitpoints);
                Require(boss.Dead && !p.Entity.Dead && !PirateFenced(p));Call(feature,"TickInstance",fight,DateTime.Now);
            }
            Require((uint)p[Revora.Game.Features.BossRushSystem.ClaimKey]==1 && p.Entity.MapID==1002);
            Require((long)Sql("SELECT count(*) FROM revora_boss_rush_runs WHERE entityid=@uid AND kind=1 AND NOT consumed","uid",(long)p.Entity.UID)==1);
            Require(EntityTable.SaveEntity(p));
            Sql("UPDATE revora_fresh_rush_probe SET step=1,hits=@hits,snapshot=@snapshot","hits",hits,"snapshot",FreshRushSnapshot(p));
        } else {
            Require(FreshRushSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_rush_probe")));
            if(claim) {
                Require((int)Sql("SELECT step FROM revora_fresh_rush_probe")==1);
                Require((long)Sql("SELECT count(*) FROM items WHERE entityid=@uid AND position=0","uid",(long)p.Entity.UID)>40);
                Require((long)Sql("SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0","uid",(long)p.Entity.UID)==p.Inventory.Count);
                uint cp=p.Entity.ConquerPoints,rvp=p.Entity.RevoraPoint;ulong gold=p.Entity.Money;var before=p.Inventory.Objects.Select(i=>i.UID).ToArray();
                Console.WriteLine("[fresh-rush] before claim kind="+(uint)p[Revora.Game.Features.BossRushSystem.ClaimKey]+" cp="+cp+" rvp="+rvp+" gold="+gold+" bag="+p.Inventory.Count);
                Revora.Game.Features.BossRushSystem.RecoverPending(p);
                HeartTalk(p,900000010,2);
                Console.WriteLine("[fresh-rush] after claim kind="+(uint)p[Revora.Game.Features.BossRushSystem.ClaimKey]+" cp="+p.Entity.ConquerPoints+" rvp="+p.Entity.RevoraPoint+" gold="+p.Entity.Money+" bag="+p.Inventory.Count);

                var rewards=p.Inventory.Objects.Where(i=>!before.Contains(i.UID)).ToArray();
                Require(p.Entity.ConquerPoints==cp+Config.BossRushWinCp && p.Entity.RevoraPoint==rvp+Config.BossRushWinRevoraPoints && p.Entity.Money==gold+Config.BossRushWinGold);
                Require(rewards.Length==Config.BossRushWinPerfectionStones+1 && rewards.Count(i=>i.Bound && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)==1);
                Require((long)Sql("SELECT count(*) FROM revora_boss_rush_runs WHERE entityid=@uid AND kind=1 AND consumed","uid",(long)p.Entity.UID)==1);
                Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_rush_probe SET step=2,snapshot=@snapshot","snapshot",FreshRushSnapshot(p));
            }
            Require((int)Sql("SELECT step FROM revora_fresh_rush_probe")==2);
            string once=FreshRushSnapshot(p);HeartTalk(p,900000010,2);Revora.Game.Features.BossRushSystem.RecoverPending(p);
            Require(once==FreshRushSnapshot(p) && !PirateFenced(p));
        }
        Console.WriteLine("[career-process] "+phase+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Fresh Rush did not close.");
    }
}
