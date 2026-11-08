using System;
using System.Linq;
using Revora;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static string AdventureAliasSnapshot(uint uid,int visit) {
        return Convert.ToString(Sql(@"SELECT json_build_object(
'purse',(SELECT money FROM entities WHERE uid=@uid),
'passage',(SELECT row_to_json(r) FROM revora_adventure_passages r WHERE uid=@uid),
'evidence',(SELECT json_agg(r ORDER BY achievement_id) FROM revora_achievement_evidence r WHERE uid=@uid AND achievement_id IN(10117,@visit,10715,10716,10717,10718)))::text","uid",(long)uid,"visit",visit));
    }
    static void AdventureIslandPortal(Revora.Client.GameState p,int index,ushort x,ushort y) {
        var portal=p.Map.Portals[index];p.Entity.X=x;p.Entity.Y=y;
        Require(ReferenceEquals(p.Map.Portals.First(i=>Kernel.GetDistance(i.CurrentX,i.CurrentY,x,y)<=4),portal));
        // Proximity is supplied; one actual accepted step records the origin
        // region before the production portal action. This is not a walk route.
        bool moved=false;
        for(byte direction=0;direction<8 && !moved;direction++) {
            ushort nx=(ushort)(x+Entity.XDir[direction]),ny=(ushort)(y+Entity.YDir[direction]);
            if(!p.Map.Floor[nx,ny,MapObjectType.Player] || !ReferenceEquals(p.Map.Portals.FirstOrDefault(i=>Kernel.GetDistance(i.CurrentX,i.CurrentY,nx,ny)<=4),portal))continue;
            moved=p.Entity.Move((Enums.ConquerAngle)direction,1);
        }
        Require(moved && Kernel.GetDistance(portal.CurrentX,portal.CurrentY,p.Entity.X,p.Entity.Y)<=4);
        var frame=new Data(true){ID=Data.UsePortal,UID=p.Entity.UID,dwParam=(uint)(portal.CurrentX|portal.CurrentY<<16)}.ToArray();
        Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,frame.Length-8,frame);Data.Process(frame,p);
        Require(p.Entity.MapID==portal.DestinationMapID && p.Map.Floor[p.Entity.X,p.Entity.Y,MapObjectType.Player]);
    }
    static void AdventureIslands(Revora.Client.GameState p) {
        ulong silver=p.Entity.Money;
        HeartTalk(p,300655,1);Require(p.Entity.MapID==1210);
        AdventureIslandPortal(p,0,4,329);AdventureIslandPortal(p,0,3,400);
        Require(p.Entity.MapID==1212);
        foreach(var point in new[]{new[]{19,904,1045},new[]{15,898,895},new[]{12,865,848},new[]{0,780,837},new[]{2,583,826},
            new[]{22,478,692},new[]{24,349,515},new[]{5,394,391},new[]{6,312,175}})
            AdventureIslandPortal(p,point[0],(ushort)point[1],(ushort)point[2]);
        HeartTalk(p,300653,1);Require(p.Entity.MapID==1219 && p.Map.BaseID==1212);
        HeartTalk(p,300654,1);Require(p.Entity.MapID==1002 && p.Entity.Money==silver);
    }
    static void AdventureAliasProcess(bool reload) {
        var routes=new uint[][]{new uint[]{9886,1217,10719},new uint[]{9884,1216,10712},new uint[]{9885,1015,10631},new uint[]{9887,1001,10703}};
        for(int index=0;index<routes.Length;index++) {
        uint uid=FirstUid+(uint)index,npc=routes[index][0],target=routes[index][1];int visit=(int)routes[index][2];
        string file="adventure-passage-"+uid+".snapshot";nextUid=uid;
        if(!reload){Player(Kernel.Maps[1002]);Sql("UPDATE entities SET money=2000 WHERE uid=@uid","uid",(long)uid);nextUid=uid;}
        var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && p.Entity.GuildID==0);
        Require(Kernel.Maps[1217].BaseID==1075 && MapsTable.MapInformations[1217].BaseID==1075);
        if(!reload) {
            HeartTalk(p,380,1);Require(p.Entity.MapID==1038 && p.Entity.Money==2000);
            if(npc==9886) {
            var destination=Kernel.Maps[1217];destination.BaseID=1002;
            try{HeartTalk(p,9886,1);Require(p.Entity.MapID==1038 && p.Entity.Money==2000);
                Require((long)Sql("SELECT count(*) FROM revora_adventure_passages WHERE uid=@uid","uid",(long)uid)==0);}
            finally{destination.BaseID=1075;}
            }
            HeartTalk(p,npc,1);
            Require(p.Entity.MapID==target && p.Map.BaseID==(target==1217?1075:target) && p.Entity.Money==1000);
            Require((long)Sql("SELECT count(*) FROM revora_adventure_passages WHERE uid=@uid AND npc_id=@npc AND destination=@target AND NOT pending AND trips=1 AND silver_before=2000 AND silver_after=1000","uid",(long)uid,"npc",(long)npc,"target",(long)target)==1);
            if(npc==9884)AdventureIslands(p);
            Require(EntityTable.SaveEntity(p));System.IO.File.WriteAllText(file,AdventureAliasSnapshot(uid,visit));
        }else {
            Require(p.Entity.MapID==(npc==9884?1002:target) && p.Entity.Money==1000);
            Require(System.IO.File.ReadAllText(file)==AdventureAliasSnapshot(uid,visit));
            // A stale confirmation after departure has no origin authority.
            p.ActiveNpc=npc;Npcs.GetDialog(new Revora.Network.GamePackets.NpcRequest(true){NpcID=npc,OptionID=1},p);
            Require(System.IO.File.ReadAllText(file)==AdventureAliasSnapshot(uid,visit));
        }
        Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id IN(10117,@visit)","uid",(long)uid,"visit",visit)==2);
        if(npc==9884)Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id IN(10715,10716,10717,10718)","uid",(long)uid)==4);
        }
        System.Console.WriteLine("[adventure-process] adventure-alias"+(reload?"-reload":"")+"-passed");Server.CommandsAI("@close");throw new Exception("Adventure alias did not close.");
    }
}
