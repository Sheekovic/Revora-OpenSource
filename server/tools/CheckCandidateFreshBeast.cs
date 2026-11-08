using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshBeastSnapshot(GameState p) {
        return Convert.ToString(Sql(@"SELECT json_build_object('career',@career::json,
'results',(SELECT json_agg(r ORDER BY run) FROM revora_beast_results r WHERE uid=@uid),
'admissions',(SELECT json_agg(r ORDER BY cycle) FROM revora_beast_admissions r WHERE uid=@uid))::text",
            "career",FreshGuildHuntSnapshot(p),"uid",(long)p.Entity.UID));
    }
    static void FreshBeastProcess(string phase) {
        bool earn=phase.EndsWith("earn"),claim=phase.EndsWith("claim");
        var p=FreshLoadSaved(true,true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);Server.GamePool=new[]{p};Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));var feature=Feature("BeastTrialSystem");
        Console.WriteLine("[fresh-beast] loaded map="+p.Entity.MapID+" bag="+p.Inventory.Count+" ape="+p.Warehouses.ContainsKey(Revora.Game.ConquerStructures.Warehouse.WarehouseID.ApeCity));
        if(earn) {
            Require((bool)Sql("SELECT to_regclass('revora_fresh_beast_probe') IS NULL"));
            string expected=Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_guild_hunt_probe")),actual=FreshGuildHuntSnapshot(p);
            if(expected!=actual){
                // Reviewed interrupted fixture: only1038(56,58) ->1020(576,544).
                // Exact hashes cover all nested possessions, balances and event rows.
                Func<string,string> hash=value=>{using(var sha=System.Security.Cryptography.SHA256.Create())return System.BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant();};
                Require(hash(expected)=="f8fc77a1ae2a5e9068f860c900ab1db31d353d4ce307673d1cf22021c3192b83" && hash(actual)=="8659ed0f8723e6e9b77a3cc37851262c4c5f21f8f615a3a2c956c35f403f386e");Console.WriteLine("[fresh-beast] reviewed coordinate-only approach recovery");
            }

            Console.WriteLine("[fresh-beast] prior snapshot verified");
            FreshClassicTalk(p,1020,10028,0);var bank=p.Warehouses[Revora.Game.ConquerStructures.Warehouse.WarehouseID.ApeCity];Require(bank.Count<60 && p.Inventory.Count==40);var item=p.Inventory.Objects.First(i=>i.Days==0);var frame=new byte[172];Revora.Network.Writer.WriteUInt16(164,0,frame);Revora.Network.Writer.WriteUInt16(1102,2,frame);Revora.Network.Writer.WriteUInt32(10028,4,frame);frame[12]=1;frame[13]=10;Revora.Network.Writer.WriteUInt32(item.UID,18,frame);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,164,frame);Warehouse.Process(frame,p);Require(!p.Inventory.ContainsUID(item.UID) && bank.ContainsUID(item.UID) && p.Inventory.Count==39);
            Console.WriteLine("[fresh-beast] storage complete");
            p.Entity.Teleport(1002,408,330);feature.GetField("CycleStart",Hidden).SetValue(null,DateTime.Now.AddMinutes(1));Revora.Game.Features.BeastTrialSystem.Tick();HeartTalk(p,900000009,1);
            Console.WriteLine("[fresh-beast] admission requested");
            var fight=Call(feature,"FindByPlayer",p.Entity.UID);Require(fight!=null);var arena=(Map)fight.GetType().GetField("Arena").GetValue(fight);Require(p.Entity.MapID==arena.ID && arena.ID!=700);
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
                        Attack.HandleAttack(new Attack(true){Attacker=p.Entity.UID,Attacked=boss.UID,X=boss.X,Y=boss.Y,AttackType=Attack.Melee},p);attempts++;hits++;waveHits++;
                        if(attempts==10)Require(boss.Hitpoints<initial);
                    }
                    Require(boss.Dead && !p.Entity.Dead && !PirateFenced(p));kills++;
                }
                Console.WriteLine("[fresh-beast] wave="+wave+" kills="+alive.Length+" hits="+waveHits);Call(feature,"TickInstance",fight,DateTime.Now);
            }
            Require(p.Entity.MapID==1002 && (bool)fight.GetType().GetField("Closed").GetValue(fight));
            Require((long)Sql("SELECT count(*) FROM revora_beast_results WHERE uid=@uid AND kind=1 AND NOT imported","uid",(long)p.Entity.UID)==1);
            Require((long)Sql("SELECT count(*) FROM revora_beast_admissions WHERE uid=@uid AND ended AND returned","uid",(long)p.Entity.UID)==1);
            Require(EntityTable.SaveEntity(p));Sql("CREATE TABLE revora_fresh_beast_probe(step int,kills int,hits int,snapshot text);INSERT INTO revora_fresh_beast_probe VALUES(1,@kills,@hits,@snapshot)","kills",kills,"hits",hits,"snapshot",FreshBeastSnapshot(p));
        } else {
            Require(FreshBeastSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_beast_probe")));Revora.Game.Features.BeastTrialSystem.OnLogin(p);Require(p.Entity.MapID==1002);
            if(claim) {
                Require((int)Sql("SELECT step FROM revora_fresh_beast_probe")==1);uint cp=p.Entity.ConquerPoints,rvp=p.Entity.RevoraPoint;ulong gold=p.Entity.Money;var before=p.Inventory.Objects.Select(i=>i.UID).ToArray();
                HeartTalk(p,900000009,2);var added=p.Inventory.Objects.Where(i=>!before.Contains(i.UID)).ToArray();
                Require(p.Entity.ConquerPoints==cp+Config.BeastTrialWinCp && p.Entity.RevoraPoint==rvp+Config.BeastTrialWinRevoraPoints && p.Entity.Money==gold+Config.BeastTrialWinGold);
                Require(added.Length==1 && added[0].Bound && ConquerItemInformation.BaseInformations[added[0].ID].PurificationLevel==7);
                Require((long)Sql("SELECT count(*) FROM revora_beast_results WHERE uid=@uid AND kind=1 AND imported","uid",(long)p.Entity.UID)==1 && (uint)p[Revora.Game.Features.BeastTrialSystem.ClaimKey]==0);
                Console.WriteLine("[fresh-beast] prize cp="+Config.BeastTrialWinCp+" rvp="+Config.BeastTrialWinRevoraPoints+" gold="+Config.BeastTrialWinGold);
                Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_beast_probe SET step=2,snapshot=@snapshot","snapshot",FreshBeastSnapshot(p));
            }
            Require((int)Sql("SELECT step FROM revora_fresh_beast_probe")==2);string once=FreshBeastSnapshot(p);HeartTalk(p,900000009,2);Revora.Game.Features.BeastTrialSystem.OnLogin(p);Require(once==FreshBeastSnapshot(p) && !PirateFenced(p));
        }
        Console.WriteLine("[career-process] "+phase+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Fresh Beast did not close.");
    }
}
