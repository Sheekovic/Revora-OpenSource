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
    static string FreshWaveSnapshot(GameState p,GameState[] players){return Convert.ToString(Sql("SELECT json_build_object('prior',@prior::json,'results',(SELECT json_agg(r ORDER BY run,uid) FROM revora_wave_results r WHERE uid=ANY(@uids)),'admissions',(SELECT json_agg(r ORDER BY cycle,uid) FROM revora_wave_admissions r WHERE uid=ANY(@uids)))::text","prior",FreshCtfSnapshot(p,players),"uids",players.Select(w=>(long)w.Entity.UID).ToArray()));}
    static void FreshWaveSave(GameState p,GameState[] players,int stage){foreach(var who in players)Require(EntityTable.SaveEntity(who));SkillTable.SaveSpells(p);Sql("UPDATE revora_fresh_wave_probe SET stage=@stage,snapshot=@snapshot","stage",stage,"snapshot",FreshWaveSnapshot(p,players));}
    static void FreshWaveJourney(string phase){
        bool earn=phase.EndsWith("earn"),claim=phase.EndsWith("claim");
        var p=FreshLoadSaved(true,true,true,true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);
        var list=new System.Collections.Generic.List<GameState>();foreach(int slot in new[]{1,2,3,0}){uint uid=Convert.ToUInt32(Sql("SELECT uid FROM revora_fresh_contributors WHERE slot=@slot","slot",slot));list.Add(FreshContributor(uid,Convert.ToString(Sql("SELECT name FROM revora_fresh_contributors WHERE slot=@slot","slot",slot))));}list.Add(p);var players=list.ToArray();Server.GamePool=players;Call(typeof(Revora.Database.JiangHu),"LoadClient",p);Require(players.All(w=>!w.Fake && w.Account.State==AccountTable.AccountState.Player && !PirateFenced(w)) && p.Entity.Class==25);

        var fighters=new[]{p,players[0],players[2],players[3]};var ids=fighters.Select(w=>(long)w.Entity.UID).ToArray();var feature=Feature("WaveDefenseSystem");Require(fighters.All(w=>!w.Entity.Dead && w.Inventory.Count<40));
        Revora.Database.KnownPersons.LoadPartner(p);Revora.Database.KnownPersons.LoaderFriends(p);
        if(earn){
            Require((bool)Sql("SELECT to_regclass('revora_fresh_wave_probe') IS NULL") && FreshCtfSnapshot(p,players)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_ctf_probe WHERE stage=2")) && (long)Sql("SELECT count(*) FROM revora_wave_results WHERE uid=ANY(@uids)","uids",ids)==0);
            foreach(var who in fighters)FreshClassicTalk(who,1002,900000007,0);
            Action<GameState,uint,uint> team=(who,action,target)=>{var frame=new Team{Type=action,UID=target}.ToArray();Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,frame.Length-8,frame);Team.Process(frame,who);};
            team(p,Team.Create,0);foreach(var who in fighters.Skip(1)){team(p,Team.InviteRequest,who.Entity.UID);team(who,Team.AcceptInvitation,p.Entity.UID);}Require(p.Team.Teammates.Length==4 && fighters.All(w=>ReferenceEquals(w.Team,p.Team)));
            feature.GetField("CycleStart",Hidden).SetValue(null,DateTime.Now.AddMinutes(1));Revora.Game.Features.WaveDefenseSystem.Tick();HeartTalk(p,900000007,1);
            var fight=Call(feature,"FindByPlayer",p.Entity.UID);Require(fight!=null);var ft=fight.GetType();var arena=(Map)ft.GetField("Arena").GetValue(fight);Require(fighters.All(w=>w.Entity.MapID==arena.ID));int kills=0,hits=0;var now=DateTime.Now;
            for(int wave=1;wave<=8;wave++){
                Require((int)ft.GetField("Wave").GetValue(fight)==wave);var roster=(System.Collections.Generic.Dictionary<uint,uint>)ft.GetField("MonsterPost").GetValue(fight);var alive=arena.Entities.Values.Where(e=>roster.ContainsKey(e.UID) && !e.Dead).ToArray();Require(alive.Length>0);Call(feature,"TickInstance",fight,now);Require((int)ft.GetField("Wave").GetValue(fight)==wave);int waveHits=0;
                foreach(var mob in alive){p.Entity.X=(ushort)(mob.X+1);p.Entity.Y=mob.Y;p.Screen.Add(mob);p.Attackable=true;p.CantAttack=Time32.Now.AddSeconds(-1);uint hp=mob.Hitpoints;int attempts=0;
                    while(!mob.Dead && attempts<20000){p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);Attack.HandleAttack(new Attack(true){Attacker=p.Entity.UID,Attacked=mob.UID,X=mob.X,Y=mob.Y,AttackType=Attack.Melee},p);attempts++;hits++;waveHits++;if(attempts==10)Require(mob.Hitpoints<hp);}
                    Require(mob.Dead && !p.Entity.Dead && !PirateFenced(p));kills++;
                }
                Console.WriteLine("[fresh-wave] wave="+wave+" kills="+alive.Length+" hits="+waveHits);Call(feature,"TickInstance",fight,now);
                if(wave<8){Call(feature,"TickInstance",fight,now.AddSeconds(7));Require((int)ft.GetField("Wave").GetValue(fight)==wave);now=now.AddSeconds(8);Call(feature,"TickInstance",fight,now);}
            }
            Require(fighters.All(w=>w.Entity.MapID==1002 && !w.Entity.Dead) && (bool)ft.GetField("Closed").GetValue(fight));Require((long)Sql("SELECT count(*) FROM revora_wave_results WHERE uid=ANY(@uids) AND kind=1 AND NOT imported","uids",ids)==4 && (long)Sql("SELECT count(*) FROM revora_wave_admissions WHERE uid=ANY(@uids) AND ended AND returned","uids",ids)==4);
            Sql("CREATE TABLE revora_fresh_wave_probe(stage int,kills int,hits int,souls bigint[],snapshot text);INSERT INTO revora_fresh_wave_probe VALUES(1,@kills,@hits,ARRAY[]::bigint[],NULL)","kills",kills,"hits",hits);FreshWaveSave(p,players,1);
        }else{
            Require(FreshWaveSnapshot(p,players)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_wave_probe")) && (int)Sql("SELECT stage FROM revora_fresh_wave_probe")==(claim?1:2));
            foreach(var who in fighters){Revora.Game.Features.WaveDefenseSystem.OnLogin(who);Require(who.Entity.MapID==1002);}
            if(claim){var souls=new System.Collections.Generic.List<long>();foreach(var who in fighters){uint cp=who.Entity.ConquerPoints,rvp=who.Entity.RevoraPoint;ulong gold=who.Entity.Money;var before=who.Inventory.Objects.Select(i=>i.UID).ToArray();HeartTalk(who,900000007,2);var soul=who.Inventory.Objects.Single(i=>!before.Contains(i.UID));Require(who.Entity.ConquerPoints==cp+Config.WaveDefenseWinCp && who.Entity.RevoraPoint==rvp+Config.WaveDefenseWinRevoraPoints && who.Entity.Money==gold+Config.WaveDefenseWinGold && soul.Bound && soul.Days==0 && ConquerItemInformation.BaseInformations[soul.ID].PurificationLevel==7 && (uint)who[Revora.Game.Features.WaveDefenseSystem.ClaimKey]==0);souls.Add(soul.UID);string once=FreshWaveSnapshot(p,players);HeartTalk(who,900000007,2);Require(once==FreshWaveSnapshot(p,players));}
                Sql("UPDATE revora_fresh_wave_probe SET souls=@souls","souls",souls.ToArray());FreshWaveSave(p,players,2);Console.WriteLine("[fresh-wave] each cp="+Config.WaveDefenseWinCp+" rvp="+Config.WaveDefenseWinRevoraPoints+" gold="+Config.WaveDefenseWinGold);
            }
            var earned=(long[])Sql("SELECT souls FROM revora_fresh_wave_probe");Require(earned.Length==4);for(int n=0;n<4;n++){var soul=fighters[n].Inventory.Objects.Single(i=>i.UID==earned[n]);Require(soul.Bound && soul.Days==0 && ConquerItemInformation.BaseInformations[soul.ID].PurificationLevel==7);HeartTalk(fighters[n],900000007,2);}Require((long)Sql("SELECT count(*) FROM revora_wave_results WHERE uid=ANY(@uids) AND kind=1 AND imported","uids",ids)==4 && FreshWaveSnapshot(p,players)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_wave_probe")));
        }
        Console.WriteLine("[career-process] "+phase+"-passed kills="+Sql("SELECT kills FROM revora_fresh_wave_probe")+" hits="+Sql("SELECT hits FROM revora_fresh_wave_probe"));if(earn){Console.Out.Flush();Environment.Exit(0);return;}Server.CommandsAI("@close");throw new InvalidOperationException("Fresh Wave Defense did not close.");
    }
}
