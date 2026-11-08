using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network;
using Revora.Network.GamePackets;
using FriendPacket=Revora.Network.GamePackets.KnownPersons;

internal static partial class CheckCandidateArenaLifecycle
{
    static bool friendClaimFixture;
    static string FriendClaimSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object(
'base',@snapshot::json,'friends',(SELECT json_agg(f ORDER BY entityid,friendid) FROM friends f WHERE entityid=@uid OR friendid=@uid),
'consents',(SELECT json_agg(a ORDER BY id) FROM revora_friend_consents a WHERE requester=@uid OR receiver=@uid))::text",
        "snapshot",CompositionClaimSnapshot(p),"uid",(long)p.Entity.UID));}
    static void FriendClaimSend(GameState p,GameState peer,byte kind){
        var frame=new FriendPacket(true){UID=peer.Entity.UID,Type=kind}.ToArray();
        Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);FriendPacket.Process(frame,p);
        Require(!PirateFenced(p) && !PirateFenced(peer));
    }
    static void FriendClaimProcess(bool reload){
        friendClaimFixture=true;const uint uid=4294910901;
        if((bool)Sql("SELECT to_regclass('revora_friend_claim_probe') IS NULL")){
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN @uid AND @last","uid",(long)uid,"last",(long)uid+5)==0);
            nextUid=uid;for(int n=0;n<6;n++)Player(Kernel.Maps[1002]);
            Sql("UPDATE entities SET level=100,reborn=0 WHERE uid BETWEEN @uid AND @last","uid",(long)uid,"last",(long)uid+5);
            Sql("CREATE TABLE revora_friend_claim_probe(snapshot text);INSERT INTO revora_friend_claim_probe VALUES(NULL)");
        }
        nextUid=uid;var players=new GameState[6];
        for(int n=0;n<6;n++){players[n]=Player(Kernel.Maps[1002],true);Revora.Database.KnownPersons.LoaderFriends(players[n]);}
        var p=players[0];ChiTable.Load(p);WayofHeroes.Load(p);
        Require(players.All(x=>!x.Fake && x.Account.State==AccountTable.AccountState.Player && !PirateFenced(x)));
        if(Sql("SELECT snapshot FROM revora_friend_claim_probe")==DBNull.Value){
            Require(!reload);
            var clock=Feature("Friendships").GetField("Clock",Hidden);object old=clock.GetValue(null);DateTime now=DateTime.UtcNow;
            try{clock.SetValue(null,(Func<DateTime>)(()=>now));
                if(p.Friends.Count==0){Require(p.Inventory.Count==0);
                for(int n=1;n<=5;n++){
                    now=now.AddSeconds(2);FriendClaimSend(p,players[n],FriendPacket.FriendApply);
                    NobilityClaimPacket(p,304);Require(p.Inventory.Count==0);
                    FriendClaimSend(players[n],p,FriendPacket.FriendAccept);
                    Require(p.Friends.Count==n && players[n].Friends.ContainsKey(uid));
                }
                }
                Require(p.Friends.Count==5);System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,304));var stone=p.Inventory.Objects.Single();
                Require(stone.ID==730001 && stone.Plus==1 && stone.Bound && stone.StackSize==1);
                uint bootsType=ConquerItemInformation.BaseInformations.Values.Where(i=>i.ID/1000==160 && i.ID%10==5 && i.Level<=15).OrderBy(i=>i.ID).First().ID;
                var boots=RefineryClaimGive(p,bootsType);CompositionClaimCompose(p,boots,stone);
                Require(boots.Plus==0 && boots.PlusProgress==10);
                FriendClaimSend(p,players[5],FriendPacket.RemovePerson);Require(p.Friends.Count==4 && !players[5].Friends.ContainsKey(uid));
                string removed=FriendClaimSnapshot(p);NobilityClaimPacket(p,304);Require(removed==FriendClaimSnapshot(p));
            }finally{clock.SetValue(null,old);}
            foreach(var player in players)Require(EntityTable.SaveEntity(player));
            Sql("UPDATE revora_friend_claim_probe SET snapshot=@snapshot","snapshot",FriendClaimSnapshot(p));
        }
        Require(p.Friends.Count==4 && players[5].Friends.Count==0 && p.Inventory.Count==1);
        Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=304 AND NOT legacy","uid",(long)uid)==1);
        Require((long)Sql("SELECT count(*) FROM revora_friend_consents WHERE requester=@uid AND removed_at IS NULL","uid",(long)uid)==4);
        string exact=FriendClaimSnapshot(p);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_friend_claim_probe")));
        NobilityClaimPacket(p,304);Require(exact==FriendClaimSnapshot(p) && !PirateFenced(p));
        System.Console.WriteLine("[career-process] career-friend-claim-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Friend claim did not close.");
    }
}
