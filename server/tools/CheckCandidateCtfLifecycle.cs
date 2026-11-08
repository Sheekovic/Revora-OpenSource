using System;
using System.Linq;
using System.Reflection;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.ConquerStructures.Society;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static DateTime ctfProbeNow=new DateTime(2027,1,3,5,0,1,DateTimeKind.Utc);
    static string CtfItems(GameState p) {
        return (string)Sql("SELECT coalesce(string_agg(uid::text||':'||id::text||':'||bound::text||':'||stacksize::text,',' ORDER BY uid),'empty') FROM items WHERE entityid=@uid","uid",(long)p.Entity.UID);
    }
    static void CtfWalkTo(GameState p,ushort x,ushort y) {
        bool placed=false;
        for(int direction=0;direction<8;direction++) {
            int fromX=x-Entity.XDir[direction],fromY=y-Entity.YDir[direction];
            if(!p.Map.Floor[fromX,fromY,MapObjectType.Player] || !p.Map.Floor[x,fromY,MapObjectType.Player] || !p.Map.Floor[fromX,y,MapObjectType.Player])continue;
            p.Entity.X=(ushort)fromX;p.Entity.Y=(ushort)fromY;placed=true;break;
        }
        Require(placed);
        var frame=new GroundMovement(true){UID=p.Entity.UID,MapID=p.Entity.MapID,Direction=Kernel.GetAngle(p.Entity.X,p.Entity.Y,x,y),GroundMovementType=GroundMovement.Walk}.ToArray();
        Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,frame.Length-8,frame);
        Revora.Network.PacketHandler.HandlePacket(frame,p);
        Require(p.Entity.X==x && p.Entity.Y==y);
    }
    static void CtfWardrobe(GameState p,bool leader,bool restoring) {
        GameAssembly.GetType("Revora.TitleRequirements",true).GetField("Clock",Hidden).SetValue(null,
            (Func<DateTime>)(()=>new DateTime(2027,1,3,5,31,0,DateTimeKind.Utc)));
        new TitleStorage().Login(p);
        var owned=(System.Collections.IDictionary)p.Entity.WTitles.GetType().GetMethod("Records",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p.Entity.WTitles,null);
        Require(owned.Contains(20020003)==leader && owned.Contains(20030004)==leader && owned.Contains(60020021)==leader);
        if(restoring)Require(p.Entity.EquippedTitle==(leader?20030004:0) && p.Entity.EquippedWing==(leader?60020021:0));
        foreach(int key in new[]{20020003,20030004,60020021}) {
            new TitleStorage{Info=new TitleStorageProto{Type=key/10000,Id=key%10000,ActionId=TitleStorage.Action.Equip,Value=new TitleValue{Type=key/10000,Id=key%10000}}}.Handle(p);
            Require(p.Entity.EquippedTitle==(leader?(key==20020003?20020003:20030004):0));
        }
        Require(p.Entity.EquippedWing==(leader?60020021:0));
        Require((long)Sql("SELECT count(*) FROM revora_event_awards WHERE uid=@uid AND source='ctf-local-wardrobe-v1' AND expires_at IS NULL","uid",(long)p.Entity.UID)==(leader?2:0));
    }
    static void CtfCapture() {
        for(int i=0;i<4;i++)Player(Kernel.Maps[1002]);nextUid=FirstUid;
        var players=Enumerable.Range(0,4).Select(i=>Player(Kernel.Maps[1002],true)).ToArray();
        // Affiliation is supplied; capture, delivery, result and prizes are earned below.
        for(int group=0;group<2;group++) {
            var leader=players[group==0?0:2];
            var guild=new Guild(leader.Entity.Name){ID=(uint)(50000+group),Name="CtfProbe"+group};
            foreach(var p in group==0?new[]{players[0],players[1],players[3]}:new[]{players[2]}) {
                p.Guild=guild;p.Entity.GuildID=(ushort)guild.ID;
                p.AsMember=new Guild.Member(guild.ID){ID=p.Entity.UID,Name=p.Entity.Name,Level=p.Entity.Level,Class=p.Entity.Class,Rank=p==leader?Enums.GuildMemberRank.GuildLeader:Enums.GuildMemberRank.Member};
                p.Entity.GuildRank=(ushort)p.AsMember.Rank;guild.Members[p.Entity.UID]=p.AsMember;
                Require(EntityTable.SaveEntity(p));p.LoadItemStats();
            }
            guild.Leader=leader.AsMember;guild.LeaderName=leader.Entity.Name;GuildTable.Create(guild);Kernel.Guilds[guild.ID]=guild;
        }
        var map=Kernel.Maps[2057];
        var ctf=(CaptureTheFlag)Activator.CreateInstance(typeof(CaptureTheFlag),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{map,(Func<DateTime>)(()=>ctfProbeNow)},null);
        Server.Thread.CTF=ctf;ctf.Tick();Require(CaptureTheFlag.IsWar);
        foreach(var p in players){HeartTalk(p,380,11);Require(p.Entity.MapID==2057);}
        var bases=map.Npcs.Values.OfType<SobNpcSpawn>().Where(n=>n.UID>=20571 && n.UID<=20573).OrderBy(n=>n.UID).ToArray();Require(bases.Length==3);
        for(int i=0;i<3;i++) {
            var p=players[i];var target=bases[i];p.Entity.X=(ushort)(target.X+1);p.Entity.Y=target.Y;p.Attackable=true;
            Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,target,new Attack(true){AttackType=Attack.Melee},target.MaxHitpoints,null);
        }
        var flag=map.StaticEntities.Values.First(f=>f.NpsEffect.Mesh==StaticEntity.FlagMesh);
        CtfWalkTo(players[0],flag.X,flag.Y);
        Require(players[0].Entity.ContainsFlag((ulong)FlagsUpdate.Flags.CarryingFlag));
        // Place only the approach; the production bounded jump performs delivery.
        var jumper=players[0];ushort landingX=0,landingY=0;bool approach=false;
        for(int dx=-8;dx<=8 && !approach;dx++)for(int dy=-8;dy<=8 && !approach;dy++) {
            ushort x=(ushort)(bases[0].X+dx),y=(ushort)(bases[0].Y+dy);
            if(!map.Floor[x,y,MapObjectType.Player] || Kernel.GetDistance(x,y,bases[0].X,bases[0].Y)>=9)continue;
            for(int d=0;d<8;d++) {
                ushort fromX=(ushort)(x+12*Entity.XDir[d]),fromY=(ushort)(y+12*Entity.YDir[d]);
                if(!map.Floor[fromX,fromY,MapObjectType.Player] || Kernel.GetDistance(fromX,fromY,bases[0].X,bases[0].Y)<9)continue;
                jumper.Entity.X=fromX;jumper.Entity.Y=fromY;landingX=x;landingY=y;approach=true;break;
            }
        }
        Require(approach);
        jumper.lastJumpDistance=0;
        GeneralData.PlayerJump(new Data(true){UID=jumper.Entity.UID,ID=Data.Jump,wParam1=jumper.Entity.X,wParam2=jumper.Entity.Y,Xt1=landingX,Yt1=landingY,TimeStamp=Time32.Now},jumper);
        Require(jumper.Entity.X==landingX && jumper.Entity.Y==landingY && !jumper.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.CarryingFlag));
        Require((long)Sql("SELECT count(*) FROM revora_ctf_deliveries WHERE run='ctf-20270102' AND guild_id=50000")==1);
        Sql(@"CREATE FUNCTION reject_ctf_wardrobe() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
IF NEW.source='ctf-local-wardrobe-v1' AND NEW.title_key=60020021 THEN RETURN NULL; END IF; RETURN NEW; END $$;
CREATE TRIGGER reject_ctf_wardrobe BEFORE INSERT ON revora_event_awards FOR EACH ROW EXECUTE FUNCTION reject_ctf_wardrobe();");
        ctfProbeNow=new DateTime(2027,1,3,5,30,0,DateTimeKind.Utc);ctf.Tick();Require(CaptureTheFlag.IsWar);
        Require((long)Sql("SELECT count(*) FROM revora_event_awards WHERE source='ctf-local-wardrobe-v1'")==0);
        Require((long)Sql("SELECT count(*) FROM revora_ctf_prizes WHERE run='ctf-20270102'")==0);
        Sql("DROP TRIGGER reject_ctf_wardrobe ON revora_event_awards;DROP FUNCTION reject_ctf_wardrobe();");
        ctfProbeNow=ctfProbeNow.AddSeconds(2);ctf.Tick();Require(!CaptureTheFlag.IsWar);
        Require((long)Sql("SELECT count(*) FROM revora_ctf_runs WHERE run='ctf-20270102' AND state='complete' AND winner=50000")==1);
        Require((long)Sql("SELECT count(*) FROM revora_ctf_prizes WHERE run='ctf-20270102' AND claimed_at IS NULL AND uid IN(@leader,@member)","leader",(long)FirstUid,"member",(long)FirstUid+1)==2);
        Require((long)Sql("SELECT count(*) FROM revora_ctf_prizes WHERE uid IN(@loser,@idle)","loser",(long)FirstUid+2,"idle",(long)FirstUid+3)==0);
        for(int i=0;i<players.Length;i++)CtfWardrobe(players[i],i==0,false);
        Sql("CREATE TABLE revora_ctf_process_probe(uid bigint PRIMARY KEY,items text,paid_at timestamptz)");
        foreach(var p in players){Require(p.Inventory.Count==0 && EntityTable.SaveEntity(p));Sql("INSERT INTO revora_ctf_process_probe(uid) VALUES(@uid)","uid",(long)p.Entity.UID);}
        System.Console.WriteLine("[ctf-process] ctf-capture-passed");Environment.Exit(0);
    }
    static void CtfClaim(bool replay) {
        for(int i=0;i<4;i++) {
            var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();p.BindWorldMap(1002);
            CtfWardrobe(p,i==0,true);
            if(replay)Require(CtfItems(p)==(string)Sql("SELECT items FROM revora_ctf_process_probe WHERE uid=@uid","uid",(long)p.Entity.UID));
            HeartTalk(p,380,12);HeartTalk(p,380,12);
            Require(p.Inventory.Count==(i<2?1:0) && p.Entity.ConquerPoints==0 && p.Entity.RevoraPoint==0);
            if(i<2) {
                var soul=p.Inventory.Objects.Single();Require(soul.Bound && soul.Days==0 && ConquerItemInformation.BaseInformations[soul.ID].PurificationLevel==7);
                Require((bool)Sql("SELECT item_uid=@item AND item_type=@type AND claimed_at IS NOT NULL FROM revora_ctf_prizes WHERE uid=@uid","uid",(long)p.Entity.UID,"item",(long)soul.UID,"type",(long)soul.ID));
                if(replay)Require((bool)Sql("SELECT p.claimed_at=f.paid_at FROM revora_ctf_prizes p JOIN revora_ctf_process_probe f USING(uid) WHERE p.uid=@uid","uid",(long)p.Entity.UID));
            }
            Require(EntityTable.SaveEntity(p));
            if(!replay)Sql("UPDATE revora_ctf_process_probe SET items=@items,paid_at=(SELECT claimed_at FROM revora_ctf_prizes WHERE uid=@uid) WHERE uid=@uid","uid",(long)p.Entity.UID,"items",CtfItems(p));
        }
        Require((long)Sql("SELECT count(*) FROM revora_ctf_prizes WHERE run='ctf-20270102'")==2);
        System.Console.WriteLine("[ctf-process] "+(replay?"ctf-verify":"ctf-claim")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("CTF process did not close.");
    }
}
