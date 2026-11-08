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
    static string FreshRelicSnapshot(GameState p,GameState[] players){return Convert.ToString(Sql("SELECT json_build_object('prior',@prior::json,'placements',(SELECT json_agg(r ORDER BY cycle,place) FROM revora_relic_placements r WHERE uid=ANY(@uids)),'admissions',(SELECT json_agg(r ORDER BY cycle,uid) FROM revora_relic_admissions r WHERE uid=ANY(@uids)))::text","prior",FreshWaveSnapshot(p,players),"uids",players.Select(w=>(long)w.Entity.UID).ToArray()));}
    static void FreshRelicJourney(string phase){
        var main=FreshLoadSaved(true,true,true,true,true);EntityVariableTable.Load(main);SubClassTable.Load(main.Entity);ChiTable.Load(main);main.Quests.Load();Revora.Database.KnownPersons.LoadMentor(main);WayofHeroes.Load(main);
        var list=new System.Collections.Generic.List<GameState>();foreach(int slot in new[]{1,2,3,0}){uint uid=Convert.ToUInt32(Sql("SELECT uid FROM revora_fresh_contributors WHERE slot=@slot","slot",slot));list.Add(FreshContributor(uid,Convert.ToString(Sql("SELECT name FROM revora_fresh_contributors WHERE slot=@slot","slot",slot))));}list.Add(main);var careerPlayers=list.ToArray();Server.GamePool=careerPlayers;Call(typeof(Revora.Database.JiangHu),"LoadClient",main);Require(careerPlayers.All(w=>!w.Fake && w.Account.State==AccountTable.AccountState.Player && !PirateFenced(w)) && main.Entity.Class==25);
        var players=new[]{careerPlayers[0],careerPlayers[3],main,careerPlayers[2]};var ids=players.Select(w=>(long)w.Entity.UID).ToArray();Require(players.All(w=>!w.Entity.Dead && w.Inventory.Count<40));
        var feature=Feature("RelicRushSystem");
        if(phase.EndsWith("earn")){
            Require(FreshWaveSnapshot(main,careerPlayers)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_wave_probe WHERE stage=2")));
            DateTime start=(DateTime)feature.GetField("CycleStart",Hidden).GetValue(null),now=start.AddMinutes(-4);feature.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
            Revora.Game.Features.RelicRushSystem.Tick();foreach(var p in players){FreshClassicTalk(p,1002,900000005,1);HeartTalk(p,900000005,1);}
            Require(((System.Collections.Generic.HashSet<uint>)feature.GetField("Registered",Hidden).GetValue(null)).Count==4);
            now=start;Revora.Game.Features.RelicRushSystem.Tick();var arena=(Map)feature.GetField("EventMap",Hidden).GetValue(null);Require(players.All(p=>p.Entity.MapID==arena.ID));
            feature.GetField("PeaceUntil",Hidden).SetValue(null,Time32.Now.AddSeconds(-1));foreach(var who in players){who.Attackable=true;who.CantAttack=Time32.Now.AddSeconds(-1);}int hits=0;
            Action pulse=()=>{feature.GetField("NextPulse",Hidden).SetValue(null,Time32.Now.AddSeconds(-1));Revora.Game.Features.RelicRushSystem.Tick();};
            var scores=(System.Collections.Generic.Dictionary<uint,uint>)feature.GetField("Scores",Hidden).GetValue(null);
            for(int holder=0;holder<3;holder++){
                ushort x=(ushort)feature.GetField("RelicX",Hidden).GetValue(null),y=(ushort)feature.GetField("RelicY",Hidden).GetValue(null);
                foreach(var other in players.Skip(holder+1)){other.Entity.X=(ushort)(x>10?x-10:x+10);other.Entity.Y=y;}
                var p=players[holder];p.Entity.X=x;p.Entity.Y=y;pulse();Require((uint)feature.GetField("Holder",Hidden).GetValue(null)==p.Entity.UID);
                int ticks=holder==0?6:holder==1?3:5;if(holder==2)now=start.AddMinutes(7);
                for(int n=0;n<ticks;n++){now=now.AddSeconds(1);pulse();}
                Require(scores[p.Entity.UID]==(uint)(holder==2?10:ticks));
                if(holder<2){main.Entity.X=p.Entity.X;main.Entity.Y=(ushort)(p.Entity.Y+1);main.Screen.Add(p.Entity);p.Screen.Add(main.Entity);main.Entity.PKMode=Enums.PKMode.PK;
                    for(int n=0;n<300 && !p.Entity.Dead && p.Entity.MapID==arena.ID;n++){main.Entity.AttackStamp=Time32.Now.AddSeconds(-2);Attack.HandleAttack(new Attack(true){Attacker=main.Entity.UID,Attacked=p.Entity.UID,X=p.Entity.X,Y=p.Entity.Y,AttackType=Attack.Melee},main);hits++;}
                    Require((uint)feature.GetField("Holder",Hidden).GetValue(null)==0 && p.Entity.MapID==1002);if(p.Entity.Dead){p.Entity.DeathStamp=Time32.Now.AddSeconds(-20);GeneralData.Revive(new Data(true){UID=p.Entity.UID,ID=Data.Revive},p);}Require(!p.Entity.Dead);}

            }
            now=start.AddMinutes(8);pulse();Require(players.All(p=>p.Entity.MapID==1002));
            Require((long)Sql("SELECT count(*) FROM revora_relic_placements WHERE (uid=@first AND place=1 AND score=10 OR uid=@second AND place=2 AND score=6 OR uid=@third AND place=3 AND score=3) AND NOT imported","first",(long)players[2].Entity.UID,"second",(long)players[0].Entity.UID,"third",(long)players[1].Entity.UID)==3);
            Require((long)Sql("SELECT count(*) FROM revora_relic_admissions WHERE uid=ANY(@uids) AND ended AND returned","uids",ids)==4);
            foreach(var p in careerPlayers)Require(EntityTable.SaveEntity(p));SkillTable.SaveSpells(main);Sql("CREATE TABLE revora_fresh_relic_probe(done boolean NOT NULL,hits int,souls bigint[],snapshot text);INSERT INTO revora_fresh_relic_probe VALUES(false,@hits,ARRAY[]::bigint[],@snapshot)","hits",hits,"snapshot",FreshRelicSnapshot(main,careerPlayers));
            Console.WriteLine("[career-process] career-fresh-relic-earn-passed");Console.Out.Flush();Environment.Exit(0);return;
        }
        Require(FreshRelicSnapshot(main,careerPlayers)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_relic_probe")));foreach(var p in players){Revora.Game.Features.RelicRushSystem.OnLogin(p);Require(p.Entity.MapID==1002);}
        if(phase.EndsWith("claim")){
            var cps=new uint[]{Config.RelicRushSecondCp,Config.RelicRushThirdCp,Config.RelicRushFirstCp,0};var rvps=new uint[]{Config.RelicRushSecondRevoraPoints,Config.RelicRushThirdRevoraPoints,Config.RelicRushFirstRevoraPoints,0};var golds=new ulong[]{Config.RelicRushSecondGold,Config.RelicRushThirdGold,Config.RelicRushFirstGold,0};
            var souls=new System.Collections.Generic.List<long>();for(int i=0;i<4;i++){var p=players[i];uint cp=p.Entity.ConquerPoints,rvp=p.Entity.RevoraPoint;ulong gold=p.Entity.Money;var oldIds=p.Inventory.Objects.Select(v=>v.UID).ToArray();HeartTalk(p,900000005,2);
                Require(p.Entity.ConquerPoints==cp+cps[i] && p.Entity.RevoraPoint==rvp+rvps[i] && p.Entity.Money==gold+golds[i]);
                var added=p.Inventory.Objects.Where(v=>!oldIds.Contains(v.UID)).ToArray();Require(added.Length==(i<3?1:0) && added.All(v=>v.Bound && ConquerItemInformation.BaseInformations[v.ID].PurificationLevel==7));
                if(i<3)souls.Add(added.Single().UID);Console.WriteLine("[fresh-relic] place="+(i==0?2:i==1?3:i==2?1:0)+" cp="+cps[i]+" rvp="+rvps[i]+" gold="+golds[i]);Require((uint)p[Revora.Game.Features.RelicRushSystem.ClaimKey]==0);string after=FreshRelicSnapshot(main,careerPlayers);HeartTalk(p,900000005,2);Require(after==FreshRelicSnapshot(main,careerPlayers));Require(EntityTable.SaveEntity(p));}
            Require((long)Sql("SELECT count(*) FROM revora_relic_placements WHERE uid=ANY(@uids) AND imported","uids",ids)==3);Sql("UPDATE revora_fresh_relic_probe SET done=true,souls=@souls,snapshot=@snapshot","souls",souls.ToArray(),"snapshot",FreshRelicSnapshot(main,careerPlayers));
        }
        Require((bool)Sql("SELECT done FROM revora_fresh_relic_probe"));foreach(var p in players)HeartTalk(p,900000005,2);Require(FreshRelicSnapshot(main,careerPlayers)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_relic_probe")));
        var owned=(long[])Sql("SELECT souls FROM revora_fresh_relic_probe");Require(owned.Length==3);for(int n=0;n<3;n++){var soul=players[n].Inventory.Objects.Single(i=>i.UID==owned[n]);Require(soul.Bound && soul.Days==0 && ConquerItemInformation.BaseInformations[soul.ID].PurificationLevel==7);}
        Console.WriteLine("[career-process] "+phase+"-passed hits="+Sql("SELECT hits FROM revora_fresh_relic_probe"));Server.CommandsAI("@close");throw new InvalidOperationException("Relic process did not close.");
    }
}
