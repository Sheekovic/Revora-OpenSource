using System;
using System.Linq;
using System.Reflection;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static bool arenaClaimFixture;
    static string ArenaClaimSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object(
'base',@snapshot::json,'profile',(SELECT json_build_array(heavenblessingtime,experience,expballs) FROM entities WHERE uid=@uid),
'blessing',(SELECT json_agg(r ORDER BY id) FROM revora_praying_stone_uses r WHERE uid=@uid),
'exp',(SELECT row_to_json(r) FROM revora_exp_ball_days r WHERE uid=@uid),
'matches',(SELECT json_agg(r ORDER BY id) FROM revora_arena_matches r WHERE player1=@uid OR player2=@uid),
'results',(SELECT json_agg(r ORDER BY result_order) FROM revora_arena_result_statistics r WHERE uid=@uid),
'commits',(SELECT json_agg(c ORDER BY run) FROM revora_arena_result_commits c JOIN revora_arena_matches m ON m.id=c.run WHERE m.player1=@uid OR m.player2=@uid))::text",
        "snapshot",NobilityClaimSnapshot(p),"uid",(long)p.Entity.UID));}
    static void ArenaClaimRequest(GameState p,uint dialog,uint button){
        var frame=new Qualifier.ArenaSignup{DialogID=dialog,OptionID=button}.BuildPacket();
        Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);PacketHandler.HandlePacket(frame,p);Require(!PirateFenced(p));
    }
    static void ArenaClaimProcess(bool reload){
        arenaClaimFixture=true;const uint uid=4294911001;
        if((bool)Sql("SELECT to_regclass('revora_arena_claim_probe') IS NULL")){
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN @uid AND @last","uid",(long)uid,"last",(long)uid+1)==0);
            nextUid=uid;Player(Kernel.Maps[1002]);Player(Kernel.Maps[1002]);
            Sql("UPDATE entities SET level=130,reborn=1 WHERE uid BETWEEN @uid AND @last","uid",(long)uid,"last",(long)uid+1);
            Sql("CREATE TABLE revora_arena_claim_probe(step int,snapshot text,peer_snapshot text);INSERT INTO revora_arena_claim_probe VALUES(0,NULL,NULL)");
        }
        nextUid=uid;var p=Player(Kernel.Maps[1002],true);var peer=Player(Kernel.Maps[1002],true);
        foreach(var player in new[]{p,peer}){ChiTable.Load(player);WayofHeroes.Load(player);player.LoadItemStats();Require(!player.Fake && player.Account.State==AccountTable.AccountState.Player && !PirateFenced(player));}
        int step=(int)Sql("SELECT step FROM revora_arena_claim_probe");
        if(Sql("SELECT snapshot FROM revora_arena_claim_probe")==DBNull.Value){
            Require(!reload);var clock=Feature("ArenaMatchLedger").GetField("Clock",Hidden);object old=clock.GetValue(null);
            DateTime now=new DateTime(2036,4,1,20,0,0,DateTimeKind.Utc);
            try{clock.SetValue(null,(Func<DateTime>)(()=>now));
                for(;step<20;step++){
                    now=new DateTime(2036,4,1,20,0,0,DateTimeKind.Utc).AddMinutes(step);
                    if(peer.Entity.Dead){peer.Entity.DeathStamp=Time32.Now.AddSeconds(-20);GeneralData.Revive(new Data(true){dwParam=0},peer);}
                    Require(!peer.Entity.Dead && !p.Entity.Dead && p.QualifierGroup==null && peer.QualifierGroup==null);
                    p.Entity.Teleport(1002,408,330);peer.Entity.Teleport(1002,410,330);
                    string pre=ArenaClaimSnapshot(p);NobilityClaimPacket(p,506);Require(pre==ArenaClaimSnapshot(p));
                    if(step==0){NobilityClaimPacket(p,204);Require(pre==ArenaClaimSnapshot(p));}
                    ArenaClaimRequest(p,0,0);ArenaClaimRequest(peer,0,0);Qualifier.EngagePlayers();Require(ReferenceEquals(p.ArenaStatistic.PlayWith,peer));
                    ArenaClaimRequest(p,3,1);ArenaClaimRequest(peer,3,1);Qualifier.VerifyAwaitingPeople();
                    var group=p.QualifierGroup;Require(group!=null && ReferenceEquals(peer.QualifierGroup,group) && p.Map.BaseID==700);
                    if(step==0){string entered=ArenaClaimSnapshot(p);NobilityClaimPacket(p,204);Require(entered==ArenaClaimSnapshot(p));}
                    group.ImportTime=group.CreateTime=Time32.Now.AddSeconds(-13);p.Attackable=peer.Attackable=true;p.CantAttack=peer.CantAttack=Time32.Now.AddSeconds(-1);
                    p.Entity.MinAttack=p.Entity.MaxAttack=10000;peer.Entity.Defence=0;peer.Entity.Hitpoints=1000;
                    int hits=0;while(!group.Done && hits++<100){p.Entity.X=peer.Entity.X;p.Entity.Y=(ushort)(peer.Entity.Y+1);p.Screen.Add(peer.Entity);peer.Screen.Add(p.Entity);
                        p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);Attack.HandleAttack(new Attack(true){Attacker=p.Entity.UID,Attacked=peer.Entity.UID,X=peer.Entity.X,Y=peer.Entity.Y,AttackType=Attack.Melee},p);}
                    Require(group.Done);group.DoneStamp=Time32.Now.AddSeconds(-5);var limit=DateTime.UtcNow.AddSeconds(10);
                    do{Qualifier.CheckGroups();if(p.QualifierGroup==null && peer.QualifierGroup==null)break;System.Threading.Thread.Sleep(50);}while(DateTime.UtcNow<limit);
                    Require(p.QualifierGroup==null && peer.QualifierGroup==null);
                    Require((long)Sql("SELECT count(*) FROM revora_arena_matches WHERE winner=@uid AND state='finished'","uid",(long)uid)==step+1);
                    Require(EntityTable.SaveEntity(p) && EntityTable.SaveEntity(peer));Sql("UPDATE revora_arena_claim_probe SET step=@step","step",step+1);
                    if(step==0){System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,204));var stone=p.Inventory.Objects.Single(i=>i.ID==1200000);
                        Require(stone.Bound && stone.StackSize==1);uint before=p.Entity.HeavenBlessing;SoulClaimUse(p,stone);Require(p.Entity.HeavenBlessing==before+259200);}
                    System.Console.WriteLine("[arena-claims] committed-wins="+(step+1));
                }
                string lost=ArenaClaimSnapshot(peer);NobilityClaimPacket(peer,204);NobilityClaimPacket(peer,506);Require(lost==ArenaClaimSnapshot(peer));
                System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,506));
                long rewardUid=Convert.ToInt64(Sql("SELECT uid FROM items WHERE entityid=@uid AND id=723700 AND position=0 AND bound=1 ORDER BY uid DESC LIMIT 1","uid",(long)uid));
                var ball=p.Inventory.Objects.Single(i=>i.UID==(uint)rewardUid);ulong exp=p.Entity.Experience;SoulClaimUse(p,ball);Require(p.Entity.Experience>exp);
            }finally{clock.SetValue(null,old);}
            Require(EntityTable.SaveEntity(p) && EntityTable.SaveEntity(peer));
            Sql("UPDATE revora_arena_claim_probe SET snapshot=@snapshot,peer_snapshot=@peer","snapshot",ArenaClaimSnapshot(p),"peer",ArenaClaimSnapshot(peer));
        }
        Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid IN(204,506) AND NOT legacy","uid",(long)uid)==2);
        Require((long)Sql("SELECT count(*) FROM revora_praying_stone_uses WHERE uid=@uid AND item_type=1200000 AND seconds_after-seconds_before=259200","uid",(long)uid)==1);
        Require((long)Sql("SELECT count(*) FROM revora_arena_matches WHERE winner=@uid AND state='finished'","uid",(long)uid)==20);
        string exact=ArenaClaimSnapshot(p),peerExact=ArenaClaimSnapshot(peer);
        Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_arena_claim_probe")) && peerExact==Convert.ToString(Sql("SELECT peer_snapshot FROM revora_arena_claim_probe")));
        foreach(var player in new[]{p,peer}){NobilityClaimPacket(player,204);NobilityClaimPacket(player,506);Require(!PirateFenced(player));}
        Require(exact==ArenaClaimSnapshot(p) && peerExact==ArenaClaimSnapshot(peer));
        System.Console.WriteLine("[career-process] career-arena-claims-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Arena claims did not close.");
    }
}
