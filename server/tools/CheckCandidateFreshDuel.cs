using System;
using System.Linq;
using System.Reflection;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshDuelSnapshot(GameState p,GameState[] players){return Convert.ToString(Sql(@"SELECT json_build_object('prior',@prior::json,'players',@players::json,'results',(SELECT json_agg(r ORDER BY place) FROM revora_duel_results r WHERE cycle='204104211115'),'admissions',(SELECT json_agg(r ORDER BY uid) FROM revora_duel_admissions r WHERE cycle='204104211115'),'vaults',(SELECT json_agg(r ORDER BY entityid) FROM variablevault r WHERE entityid=ANY(@uids)))::text","prior",FreshRaceSnapshot(p),"players","["+string.Join(",",players.Select(w=>FreshStarterSnapshot(w)))+"]","uids",players.Select(w=>(long)w.Entity.UID).ToArray()));}
    static void FreshDuelJourney(string phase){
        bool prepare=phase.EndsWith("prepare"),earn=phase.EndsWith("earn"),claim=phase.EndsWith("claim");var p=FreshLoadSaved(true,true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);
        if(prepare)Require(FreshRaceSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_race_probe WHERE stage=3")) && (bool)Sql("SELECT to_regclass('revora_fresh_duel_probe') IS NULL"));
        var list=new System.Collections.Generic.List<GameState>();foreach(int slot in new[]{1,2,3,0}){uint uid=Convert.ToUInt32(Sql("SELECT uid FROM revora_fresh_contributors WHERE slot=@slot","slot",slot));list.Add(FreshContributor(uid,Convert.ToString(Sql("SELECT name FROM revora_fresh_contributors WHERE slot=@slot","slot",slot))));}list.Add(p);var players=list.ToArray();Server.GamePool=players;Require(players.All(w=>!w.Fake && w.Account.State==AccountTable.AccountState.Player && !PirateFenced(w) && !w.Entity.Dead));
        var service=Feature("DuelPitSystem");var arena=(Map)service.GetField("EventMap",Hidden).GetValue(null);Require(arena!=null && arena.BaseID==700 && arena.ID!=700);
        if(prepare){
            FreshClassicTalk(p,1020,10028,0);var bank=p.Warehouses[Revora.Game.ConquerStructures.Warehouse.WarehouseID.ApeCity];int store=Math.Max(0,p.Inventory.Count-38);Require(bank.Count+store<=60);
            foreach(var item in p.Inventory.Objects.Where(i=>i.Days==0).Take(store).ToArray()){var frame=new byte[172];Revora.Network.Writer.WriteUInt16(164,0,frame);Revora.Network.Writer.WriteUInt16(1102,2,frame);Revora.Network.Writer.WriteUInt32(10028,4,frame);frame[12]=1;frame[13]=10;Revora.Network.Writer.WriteUInt32(item.UID,18,frame);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,164,frame);Warehouse.Process(frame,p);Require(!p.Inventory.ContainsUID(item.UID) && bank.ContainsUID(item.UID));}Require(p.Inventory.Count<=38);
            foreach(var who in players){FreshClassicTalk(who,1002,900000011,0);Require(EntityTable.SaveEntity(who));Console.WriteLine("[fresh-duel] uid="+who.Entity.UID+" level="+who.Entity.Level+" hp="+who.Entity.Hitpoints+" attack="+who.Entity.MinAttack+"-"+who.Entity.MaxAttack+" defence="+who.Entity.Defence);}
            Sql("CREATE TABLE revora_fresh_duel_probe(stage int NOT NULL,snapshot text NOT NULL);INSERT INTO revora_fresh_duel_probe VALUES(1,@snapshot)","snapshot",FreshDuelSnapshot(p,players));
        }else{
            Require(FreshDuelSnapshot(p,players)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_duel_probe")));
            if(earn){
                Require((int)Sql("SELECT stage FROM revora_fresh_duel_probe")==1);service.GetField("CycleStart",Hidden).SetValue(null,DateTime.Now.AddMinutes(1));Revora.Game.Features.DuelPitSystem.Tick();
                for(int n=0;n<players.Length;n++){players[n].Entity.PKMode=(Enums.PKMode)(n%3);HeartTalk(players[n],900000011,1);}Require(((System.Collections.Generic.HashSet<uint>)service.GetField("Registered",Hidden).GetValue(null)).Count==5);
                service.GetField("CycleStart",Hidden).SetValue(null,new DateTime(2041,4,21,11,15,0));Call(service,"StartFight");Require((bool)service.GetField("Running",Hidden).GetValue(null));
                int total=0;for(int round=0;round<4;round++){
                    uint king=(uint)service.GetField("King",Hidden).GetValue(null),challenger=(uint)service.GetField("Challenger",Hidden).GetValue(null);var a=players.Single(w=>w.Entity.UID==king);var b=players.Single(w=>w.Entity.UID==challenger);var winner=round==0?a:b;var loser=round==0?b:a;
                    Require(winner.Entity.MapID==arena.ID && loser.Entity.MapID==arena.ID);service.GetField("PeaceUntil",Hidden).SetValue(null,Time32.Now.AddSeconds(-1));winner.CantAttack=loser.CantAttack=Time32.Now.AddSeconds(-1);winner.Attackable=loser.Attackable=true;winner.Entity.X=loser.Entity.X;winner.Entity.Y=(ushort)(loser.Entity.Y+1);winner.Screen.Add(loser.Entity);int hits=0;var timer=System.Diagnostics.Stopwatch.StartNew();
                    while((int)service.GetField("Round",Hidden).GetValue(null)==round && hits<15000 && timer.Elapsed.TotalSeconds<90){winner.Entity.AttackStamp=Time32.Now.AddSeconds(-2);uint hp=loser.Entity.Hitpoints;Attack.HandleAttack(new Attack(true){Attacker=winner.Entity.UID,Attacked=loser.Entity.UID,X=loser.Entity.X,Y=loser.Entity.Y,AttackType=Attack.Melee},winner);hits++;if(hits==1)Require(loser.Entity.Hitpoints<hp || (int)service.GetField("Round",Hidden).GetValue(null)>round);}
                    Require((int)service.GetField("Round",Hidden).GetValue(null)==round+1 && !winner.Entity.Dead);total+=hits;Console.WriteLine("[fresh-duel] round="+(round+1)+" winner="+winner.Entity.UID+" calculated-hits="+hits);
                }
                Call(service,"Pulse",((DateTime)service.GetField("WaitUntil",Hidden).GetValue(null)).AddSeconds(1));Require((long)Sql("SELECT count(*) FROM revora_duel_results WHERE cycle='204104211115' AND score=1 AND NOT imported")==4 && (long)Sql("SELECT count(*) FROM revora_duel_results WHERE cycle='204104211115' AND place=1 AND uid=@uid","uid",(long)p.Entity.UID)==1);
                Require((long)Sql("SELECT count(*) FROM revora_duel_admissions WHERE cycle='204104211115' AND ended AND returned")==5);for(int n=0;n<players.Length;n++)Require(players[n].Entity.MapID==1002 && players[n].Entity.PKMode==(Enums.PKMode)(n%3));Console.WriteLine("[fresh-duel] total-calculated-hits="+total);
            }else{
                Require((int)Sql("SELECT stage FROM revora_fresh_duel_probe")== (claim?2:3));foreach(var who in players){Revora.Game.Features.DuelPitSystem.OnLogin(who);HeartTalk(who,900000011,0);int place=Convert.ToInt32(Sql("SELECT coalesce((SELECT place FROM revora_duel_results WHERE cycle='204104211115' AND uid=@uid),0)","uid",(long)who.Entity.UID));uint cp=who.Entity.ConquerPoints,rp=who.Entity.RevoraPoint;ulong money=who.Entity.Money;var prior=who.Inventory.Objects.Select(i=>i.UID).ToArray();HeartTalk(who,900000011,2);
                    if(claim && place>0){string suffix=new[]{"First","Second","Third","Fourth"}[place-1];uint gc=(uint)typeof(Config).GetField("DuelPit"+suffix+"Cp").GetValue(null),gr=(uint)typeof(Config).GetField("DuelPit"+suffix+"RevoraPoints").GetValue(null);ulong gm=(ulong)typeof(Config).GetField("DuelPit"+suffix+"Gold").GetValue(null);var added=who.Inventory.Objects.Where(i=>!prior.Contains(i.UID)).ToArray();Require(added.Length==1 && added[0].Bound && ConquerItemInformation.BaseInformations[added[0].ID].PurificationLevel==7 && prior.All(id=>who.Inventory.ContainsUID(id)) && who.Entity.ConquerPoints==cp+gc && who.Entity.RevoraPoint==rp+gr && who.Entity.Money==money+gm);Console.WriteLine("[fresh-duel] place="+place+" cp="+gc+" rvp="+gr+" silver="+gm);}
                    else Require(who.Entity.ConquerPoints==cp && who.Entity.RevoraPoint==rp && who.Entity.Money==money && prior.OrderBy(id=>id).SequenceEqual(who.Inventory.Objects.Select(i=>i.UID).OrderBy(id=>id)));
                    string paid=FreshDuelSnapshot(p,players);HeartTalk(who,900000011,2);Require(paid==FreshDuelSnapshot(p,players) && (uint)who[Revora.Game.Features.DuelPitSystem.ClaimKey]==0);
                }
                Require((long)Sql("SELECT count(*) FROM revora_duel_results WHERE cycle='204104211115' AND imported")==4);
            }
            foreach(var who in players)Require(EntityTable.SaveEntity(who));if(earn || claim)Sql("UPDATE revora_fresh_duel_probe SET stage=stage+1,snapshot=@snapshot","snapshot",FreshDuelSnapshot(p,players));else Require(FreshDuelSnapshot(p,players)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_duel_probe")));
        }
        if(phase.EndsWith("reload"))foreach(var who in players){var saved=(long[])Sql("SELECT coalesce(array_agg(uid ORDER BY uid),ARRAY[]::bigint[]) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND coalesce(inwardrobe,0)=0","uid",(long)who.Entity.UID);Require(saved.SequenceEqual(who.Inventory.Objects.Select(i=>(long)i.UID).OrderBy(id=>id)) && who.Entity.MapID==1002 && !who.Entity.Dead);}
        Require(players.All(w=>!PirateFenced(w)));Console.WriteLine("[career-process] "+phase+"-passed");Console.Out.Flush();if(prepare || earn || claim){Environment.Exit(0);return;}Server.CommandsAI("@close");throw new InvalidOperationException("Fresh Duel did not close.");
    }
}
