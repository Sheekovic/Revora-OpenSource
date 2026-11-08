using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static bool teamArenaClaimFixture;
    static string TeamArenaClaimSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object(
'base',@snapshot::json,'results',(SELECT json_agg(r ORDER BY id) FROM revora_team_arena_results r JOIN revora_team_arena_statistics s ON s.run=r.id WHERE s.uid=@uid),
'statistics',(SELECT json_agg(s ORDER BY run) FROM revora_team_arena_statistics s WHERE uid=@uid),
'admissions',(SELECT json_agg(a ORDER BY run) FROM revora_team_arena_admission_players a WHERE uid=@uid))::text",
        "snapshot",ArenaClaimSnapshot(p),"uid",(long)p.Entity.UID));}
    static void TeamArenaClaimRequest(GameState p,uint dialog,uint button){
        var frame=new TeamQualifier.ArenaSignup{DialogID=dialog,OptionID=button}.BuildPacket();
        Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);PacketHandler.HandlePacket(frame,p);Require(!PirateFenced(p));
    }
    static void TeamArenaClaimProcess(bool reload){
        teamArenaClaimFixture=true;const uint uid=4294911101;
        if((bool)Sql("SELECT to_regclass('revora_team_arena_claim_probe') IS NULL")){
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN @uid AND @last","uid",(long)uid,"last",(long)uid+3)==0);
            nextUid=uid;for(int n=0;n<4;n++)Player(Kernel.Maps[1002]);
            Sql("UPDATE entities SET level=130,reborn=1 WHERE uid BETWEEN @uid AND @last","uid",(long)uid,"last",(long)uid+3);
            Sql("CREATE TABLE revora_team_arena_claim_probe(uid bigint PRIMARY KEY,snapshot text);CREATE TABLE revora_team_arena_claim_step(step int);INSERT INTO revora_team_arena_claim_step VALUES(0)");
        }
        nextUid=uid;var players=new GameState[4];for(int n=0;n<4;n++){
            var player=players[n]=Player(Kernel.Maps[1002],true);ChiTable.Load(player);WayofHeroes.Load(player);player.LoadItemStats();
            Require(!player.Fake && player.Account.State==AccountTable.AccountState.Player && !PirateFenced(player));}
        var p=players[0];var mate=players[1];var peer=players[2];var peerMate=players[3];var winners=new[]{p,mate};var losers=new[]{peer,peerMate};
        if((long)Sql("SELECT count(*) FROM revora_team_arena_claim_probe")==0){
            Require(!reload);p.Team=new Revora.Game.ConquerStructures.Team(p);p.Team.Add(mate);peer.Team=new Revora.Game.ConquerStructures.Team(peer);peer.Team.Add(peerMate);
            Require(ReferenceEquals(p.Team,mate.Team) && ReferenceEquals(peer.Team,peerMate.Team));
            var clock=Feature("ArenaMatchLedger").GetField("Clock",Hidden);object old=clock.GetValue(null);DateTime now=new DateTime(2036,4,2,20,0,0,DateTimeKind.Utc);
            bool accepting=TeamQualifier.AcceptingNewBattles;
            try{clock.SetValue(null,(Func<DateTime>)(()=>now));int step=(int)Sql("SELECT step FROM revora_team_arena_claim_step");
                for(;step<20;step++){
                    now=new DateTime(2036,4,2,20,0,0,DateTimeKind.Utc).AddMinutes(step);
                    foreach(var player in players){if(player.Entity.Dead){player.Entity.DeathStamp=Time32.Now.AddSeconds(-20);GeneralData.Revive(new Data(true){dwParam=0},player);}
                        Require(!player.Entity.Dead && player.TeamQualifierGroup==null);player.Entity.Teleport(1002,(ushort)(408+Array.IndexOf(players,player)),330);}
                    foreach(var player in winners){string pre=TeamArenaClaimSnapshot(player);NobilityClaimPacket(player,507);if(step==0)NobilityClaimPacket(player,305);Require(pre==TeamArenaClaimSnapshot(player));}
                    TeamQualifier.AcceptingNewBattles=true;TeamArenaClaimRequest(p,0,0);TeamArenaClaimRequest(peer,0,0);TeamQualifier.EngagePlayers();Require(ReferenceEquals(p.TeamArenaStatistic.PlayWith,peer));
                    TeamArenaClaimRequest(p,3,1);TeamArenaClaimRequest(peer,3,1);TeamQualifier.VerifyAwaitingPeople();
                    var group=p.TeamQualifierGroup;Require(group!=null && players.All(x=>ReferenceEquals(x.TeamQualifierGroup,group)));
                    if(step==0)foreach(var player in winners){string entered=TeamArenaClaimSnapshot(player);NobilityClaimPacket(player,305);Require(entered==TeamArenaClaimSnapshot(player));}
                    group.ImportTime=group.CreateTime=Time32.Now.AddSeconds(-13);foreach(var player in players){player.Attackable=true;player.CantAttack=Time32.Now.AddSeconds(-1);}
                    p.Entity.MinAttack=p.Entity.MaxAttack=10000;
                    foreach(var target in losers){target.Entity.Defence=0;target.Entity.Hitpoints=1000;int hits=0;
                        while(!target.Entity.Dead && !group.Done && hits++<100){p.Entity.X=target.Entity.X;p.Entity.Y=(ushort)(target.Entity.Y+1);p.Screen.Add(target.Entity);target.Screen.Add(p.Entity);
                            p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);Attack.HandleAttack(new Attack(true){Attacker=p.Entity.UID,Attacked=target.Entity.UID,X=target.Entity.X,Y=target.Entity.Y,AttackType=Attack.Melee},p);}}
                    Require(group.Done);group.DoneStamp=Time32.Now.AddSeconds(-6);var limit=DateTime.UtcNow.AddSeconds(10);
                    do{TeamQualifier.CheckGroups();if(players.All(x=>x.TeamQualifierGroup==null))break;System.Threading.Thread.Sleep(50);}while(DateTime.UtcNow<limit);
                    Require(players.All(x=>x.TeamQualifierGroup==null));
                    foreach(var player in players)Require(EntityTable.SaveEntity(player));
                    foreach(var player in winners)Require((long)Sql("SELECT count(*) FROM revora_team_arena_statistics WHERE uid=@uid AND won","uid",(long)player.Entity.UID)==step+1);
                    Sql("UPDATE revora_team_arena_claim_step SET step=@step","step",step+1);
                    if(step==0)foreach(var player in winners){var beforeItems=player.Inventory.Objects.Select(i=>i.UID).ToArray();System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(player,305));
                        var stone=player.Inventory.Objects.Single(i=>!beforeItems.Contains(i.UID));Require(stone.ID==1200001 && stone.Bound && stone.StackSize==1);
                        uint before=player.Entity.HeavenBlessing;SoulClaimUse(player,stone);Require(player.Entity.HeavenBlessing==before+604800);}
                    System.Console.WriteLine("[team-arena-claims] committed-wins="+(step+1));
                }
                foreach(var player in losers){string lost=TeamArenaClaimSnapshot(player);NobilityClaimPacket(player,305);NobilityClaimPacket(player,507);Require(lost==TeamArenaClaimSnapshot(player));}
                foreach(var player in winners){var beforeItems=player.Inventory.Objects.Select(i=>i.UID).ToArray();System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(player,507));
                    var ball=player.Inventory.Objects.Single(i=>!beforeItems.Contains(i.UID));Require(ball.ID==723700 && ball.Bound && ball.StackSize==1);
                    ulong exp=player.Entity.Experience;SoulClaimUse(player,ball);Require(player.Entity.Experience>exp);}
            }finally{clock.SetValue(null,old);TeamQualifier.AcceptingNewBattles=accepting;}
            foreach(var player in players){Require(EntityTable.SaveEntity(player));Sql("INSERT INTO revora_team_arena_claim_probe VALUES(@uid,@snapshot)","uid",(long)player.Entity.UID,"snapshot",TeamArenaClaimSnapshot(player));}
        }
        foreach(var player in winners){
            Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid IN(305,507) AND NOT legacy","uid",(long)player.Entity.UID)==2);
            Require((long)Sql("SELECT count(*) FROM revora_praying_stone_uses WHERE uid=@uid AND item_type=1200001 AND seconds_after-seconds_before=604800","uid",(long)player.Entity.UID)==1);
            Require((long)Sql("SELECT count(*) FROM revora_team_arena_statistics WHERE uid=@uid AND won","uid",(long)player.Entity.UID)==20);}
        foreach(var player in players){string exact=TeamArenaClaimSnapshot(player);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_team_arena_claim_probe WHERE uid=@uid","uid",(long)player.Entity.UID)));
            NobilityClaimPacket(player,305);NobilityClaimPacket(player,507);Require(exact==TeamArenaClaimSnapshot(player) && !PirateFenced(player));}
        System.Console.WriteLine("[career-process] career-team-arena-claims-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Team Arena claims did not close.");
    }
}
