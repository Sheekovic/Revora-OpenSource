using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type Sky=typeof(Entity).Assembly.GetType("Revora.Game.Features.SkyPassQuest");
    static readonly Type SkyWorld=typeof(Entity).Assembly.GetType("Revora.Game.Features.SkyPassWorld");
    static DateTime skyNow;static int skyRoll;static int skyJumps;
    static long SkyValue(GameState p,string expression) {return Scalar("SELECT "+expression+" FROM revora_sky_runs WHERE uid=@uid",p.Entity.UID);}
    static object SkyChallenge(int stage,int route) {return Call(SkyWorld,null,"At",stage,route);}
    static uint SkyId(int stage,int route,string field) {return Convert.ToUInt32(Field(SkyChallenge(stage,route),field));}
    static GameState SkyPlayer(Map home) {var p=MoonPlayer(home);p.Quests=new Quests(p);p.Quests.Load();p.Entity.X=156;p.Entity.Y=166;return p;}
    static void SkyJump(GameState p,ushort x,ushort y) {
        p.lastJumpDistance=0;
        var packet=new Data(true){UID=p.Entity.UID,ID=Data.Jump,wParam1=p.Entity.X,wParam2=p.Entity.Y,Xt1=x,Yt1=y,TimeStamp=Time32.Now};
        GeneralData.PlayerJump(packet,p);
        if(p.Entity.X!=x || p.Entity.Y!=y)throw new Exception("Sky jump failed to reach its reviewed landing.");skyJumps++;
    }
    // Exercise the production landing/distance rule; only advance the fixture's
    // old movement timestamps, avoiding real-time sleeps between geometry checks.
    static void SkyNear(GameState p,uint npc,int reach=12) {
        var actor=p.Map.Npcs[npc];int width=p.Map.Floor.Bounds.Width,height=p.Map.Floor.Bounds.Height;
        int start=p.Entity.Y*width+p.Entity.X;var parent=Enumerable.Repeat(-1,width*height).ToArray();parent[start]=start;
        var queue=new Queue<int>();queue.Enqueue(start);int end=-1,expanded=0;
        while(queue.Count>0 && expanded++<20000) {
            int at=queue.Dequeue(),x=at%width,y=at/width;
            if(Kernel.GetDistance((ushort)x,(ushort)y,actor.X,actor.Y)<=reach){end=at;break;}
            for(int dy=-20;dy<=20;dy++)for(int dx=-20;dx<=20;dx++) {
                int nx=x+dx,ny=y+dy;if(nx<0 || ny<0 || nx>=width || ny>=height)continue;int next=ny*width+nx;
                if(parent[next]!=-1 || !p.Map.Floor[nx,ny,MapObjectType.Player] || Kernel.GetDistance((ushort)x,(ushort)y,(ushort)nx,(ushort)ny)>20)continue;
                parent[next]=at;queue.Enqueue(next);
            }
        }
        if(end<0)throw new Exception("No bounded player jump route reaches Sky NPC "+npc+" from "+p.Entity.X+","+p.Entity.Y+" toward "+actor.X+","+actor.Y+" on "+p.Entity.MapID+" expanded "+expanded);
        var path=new Stack<int>();while(end!=start){path.Push(end);end=parent[end];}
        while(path.Count>0) {
            int step=path.Pop();ushort x=(ushort)(step%width),y=(ushort)(step/width);
            SkyJump(p,x,y);
        }
        p.ActiveNpc=npc;
    }
    static void SkyNpc(GameState p,uint npc,byte option) {SkyNear(p,npc);Npcs.GetDialog(new NpcRequest(true){NpcID=npc,OptionID=option},p);}
    static Entity SkyMob(GameState p,int stage,int route) {
        var challenge=SkyChallenge(stage,route);ushort x=(ushort)Field(challenge,"X"),y=(ushort)Field(challenge,"Y");
        var mob=(Entity)typeof(CompanionTests).GetMethod("Monster",Hidden).Invoke(null,new object[]{p.Map,x,y,1u});
        mob.MonsterInfo.ID=SkyId(stage,route,"Monster");mob.MonsterInfo.Name=mob.Name=MonsterInformation.MonsterInformations[mob.MonsterInfo.ID].Name;mob.IsDropped=true;return mob;
    }
    static void SkyFight(GameState p,int stage,int route) {
        var mob=SkyMob(p,stage,route);mob.Die(p.Entity);p.Map.Entities.Remove(mob.UID);
        if(stage==5 && !p.Inventory.Contains((uint)(721108-route),1))throw new Exception("Final Sky pass does not match its monster's level tier.");
        SkyNpc(p,SkyId(stage,route,"Guard"),1);
    }
    static void SkyClimb(GameState p,int finalRoute) {
        SkyNpc(p,30055,1);for(int stage=1;stage<=4;stage++)SkyNpc(p,SkyId(stage,skyRoll,"Gate"),1);
        SkyNpc(p,SkyId(5,finalRoute,"Gate"),1);SkyFight(p,5,finalRoute);
    }
    static void VerifyIslandSoul(GameState p,string table) {
        var souls=p.Inventory.Objects.Where(i=>i.ID>=800000 && i.ID<900000 && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7).ToArray();
        Check(!Fenced(p) && souls.Length==1 && souls[0].Bound && souls[0].StackSize==1
            && Scalar("SELECT count(*) FROM "+table+" r JOIN items i ON i.uid=ANY(r.rewards) WHERE r.uid=@uid AND i.entityid=r.uid AND i.id BETWEEN 800000 AND 899999",p.Entity.UID)==1
            && SavedRune(souls[0],"bound")==1,"earned island reward has one durable bound P7 soul with receipt UID");
    }
    static void VerifyDailyIslandRune(GameState p,string table) {
        var rune=p.Inventory.Objects.Single(i=>i.ID==4030101);
        Check(!Fenced(p) && rune.Bound && rune.StackSize==1 && SavedRune(rune,"bound")==1
            && Scalar("SELECT count(*) FROM "+table+" r JOIN items i ON i.uid=ANY(r.rewards) WHERE r.uid=@uid AND i.entityid=r.uid AND i.id=4030101 AND i.bound=1",p.Entity.UID)==1,
            "earned daily island reward preserves its exact bound rune receipt");
        Check(!p.Inventory.Objects.Any(i=>i.ID>=800000 && i.ID<900000)
            && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id BETWEEN 800000 AND 899999",p.Entity.UID)==0,"daily island reward grants no P7 soul");
    }
    static void VerifySky(string runtime,Map town) {
        Query("ALTER TABLE entities ADD COLUMN my_title integer DEFAULT 0,ADD COLUMN level integer DEFAULT 140,ADD COLUMN mapid integer DEFAULT 1002,ADD COLUMN x integer DEFAULT 0,ADD COLUMN y integer DEFAULT 0");
        Query("CREATE TABLE titles (LIKE public.titles INCLUDING ALL)");Query("CREATE TABLE itemadding (LIKE public.itemadding INCLUDING ALL)");Query("CREATE TABLE quests (LIKE public.quests INCLUDING ALL)");
        Query("CREATE TABLE npcs (LIKE public.npcs INCLUDING ALL)");Query("INSERT INTO npcs SELECT * FROM public.npcs WHERE id BETWEEN 30001 AND 30055");
        Query("INSERT INTO monsterspawns SELECT * FROM public.monsterspawns WHERE mapid=1040 AND npctype BETWEEN 3000 AND 3024");
        StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);ConquerItem.ItemUID=new Counter(6900000);
        var home=World(runtime,1012);var pass=World(runtime,1040);Server.GamePool=new GameState[0];
        skyNow=new DateTime(2026,9,10,20,0,0,DateTimeKind.Utc);skyRoll=4;
        Sky.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>skyNow));Sky.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(n=>skyRoll));
        Call(Sky,null,"Initialize");Call(SkyWorld,null,"PlaceNpcs");Revora.Game.Features.QuestListSystem.EnsureCatalog();
        Check(home.Npcs.ContainsKey(30055) && pass.Npcs.Count==51,"all five gate and guard groups and God Cloud are restored with the installed Daniel");
        var p=SkyPlayer(home);p.Fake=true;SkyNpc(p,30055,1);p.Fake=false;p.ActiveNpc=30055;p.Entity.X=1;p.Entity.Y=1;
        Npcs.GetDialog(new NpcRequest(true){NpcID=30055,OptionID=1},p);
        Check(Scalar("SELECT count(*) FROM revora_sky_runs WHERE uid=@uid",p.Entity.UID)==0,"fake and remote Sky enrollment cannot create a run");p.Entity.X=156;p.Entity.Y=166;
        var forged=new MsgTaskStatus(true,1){Action=MsgTaskStatus.QuestAction.Begin};forged.Apend(MsgTaskStatus.QuestData.Create((QuestID)6350,MsgTaskStatus.QuestData.QuestStatus.Finished,0));MsgTaskStatus.Process(forged.ToArray(),p);
        Check(Scalar("SELECT count(*) FROM revora_sky_runs WHERE uid=@uid",p.Entity.UID)==0,"native quest Begin cannot fabricate an earned climb");
        SkyNpc(p,30055,1);Check(p.Entity.MapID==1040 && SkyValue(p,"stage")==1 && p.Quests.CheckQuest((QuestID)6350)==MsgTaskStatus.QuestData.QuestStatus.Accepted,"ordinary Daniel entry commits a run and native mission projection");
        p.ActiveNpc=30054;Npcs.GetDialog(new NpcRequest(true){NpcID=30054,OptionID=1},p);Check(!HasAchievement(p,11019),"forged summit NPC cannot grant the achievement");
        SkyNpc(p,SkyId(1,0,"Gate"),1);Check(SkyValue(p,"CASE WHEN phase='battle' THEN 1 ELSE 0 END")==1 && SkyValue(p,"route")==0,"wrong sealed gate selects its real battle family");
        ushort landingX=p.Entity.X,landingY=p.Entity.Y;
        bool jumped=false;for(int dx=-8;dx<=8 && !jumped;dx++)for(int dy=-8;dy<=8 && !jumped;dy++)
            if((dx!=0 || dy!=0) && p.Map.Floor[landingX+dx,landingY+dy,MapObjectType.Player]){SkyJump(p,(ushort)(landingX+dx),(ushort)(landingY+dy));jumped=true;}
        SkyJump(p,landingX,landingY);Check(jumped,"production player jumps traverse valid battle-platform landings");
        var wrong=SkyMob(p,1,1);wrong.Die(p.Entity);var distant=SkyMob(p,1,0);distant.X=1;distant.Y=1;distant.Die(p.Entity);
        Check(p.Inventory.Count==0,"wrong family and distant monster deaths cannot issue a battle pass");
        var mob=SkyMob(p,1,0);Parallel.For(0,12,i=>mob.Die(p.Entity));
        Check(p.Inventory.Count==1 && p.Inventory.Objects[0].ID==721100 && p.Inventory.Objects[0].Bound,"one genuine monster life issues one owned bound stage pass under concurrent death callbacks");
        var passItem=p.Inventory.Objects[0];var foreign=SkyPlayer(home);
        Query("UPDATE items SET entityid=@owner WHERE uid=@item","owner",(long)foreign.Entity.UID,"item",(long)passItem.UID);SkyNpc(p,SkyId(1,0,"Guard"),1);
        Check(Fenced(p) && Scalar("SELECT entityid FROM items WHERE uid=@uid",passItem.UID)==foreign.Entity.UID && SkyValue(p,"CASE WHEN phase='token' THEN 1 ELSE 0 END")==1,"foreign persisted pass ownership blocks consumption and advancement");
        Query("UPDATE items SET entityid=@owner WHERE uid=@item","owner",(long)p.Entity.UID,"item",(long)passItem.UID);DemonReset(p);
        SkyNear(p,SkyId(1,0,"Guard"));Query("ALTER TABLE revora_sky_steps ADD CONSTRAINT sky_transition_failure CHECK(action<>'turn-in-pass') NOT VALID");
        Npcs.GetDialog(new NpcRequest(true){NpcID=p.ActiveNpc,OptionID=1},p);
        Check(Fenced(p) && p.Inventory.Count==1 && SkyValue(p,"CASE WHEN phase='token' THEN 1 ELSE 0 END")==1,"failed guard transition rolls back pass consumption and phase");
        Query("ALTER TABLE revora_sky_steps DROP CONSTRAINT sky_transition_failure");DemonReset(p);SkyNpc(p,SkyId(1,0,"Guard"),1);
        Check(p.Inventory.Count==0 && SkyValue(p,"stage")==1 && SkyValue(p,"gates[1]")==4,"battle pass returns to the same sealed stage without rerolling the open gate");
        for(int stage=1;stage<=4;stage++)SkyNpc(p,SkyId(stage,4,"Gate"),1);
        Check(SkyValue(p,"stage")==5 && !HasAchievement(p,11019),"four saved open gates reach the final stage without skipping its required fight");
        SkyNpc(p,SkyId(5,0,"Gate"),1);var filler=Enumerable.Range(0,40).Select(i=>Pack(p,1088001,1)).ToArray();SkyMob(p,5,0).Die(p.Entity);
        Check(SkyValue(p,"CASE WHEN phase='cleared' THEN 1 ELSE 0 END")==1 && p.Inventory.Count==40,"full bag retains the earned final battle credit");
        foreach(var item in filler){Query("DELETE FROM items WHERE uid=@id","id",(long)item.UID);p.Inventory.Remove(item.UID,Enums.ItemUse.None,false);}
        SkyNpc(p,SkyId(5,0,"Guard"),1);Check(p.Inventory.Count==1 && p.Inventory.Objects[0].ID==721108,"stage guard delivers the saved level-120 pass after room is made");
        SkyNpc(p,SkyId(5,0,"Guard"),1);Check(p.Inventory.Count==1 && p.Inventory.Objects[0].ID==721109 && SkyValue(p,"CASE WHEN phase='prize' THEN 1 ELSE 0 END")==1,"final pass exchanges atomically for the real Sky Prize Token");
        Call(Sky,null,"Initialize");Call(Sky,null,"OnLogin",p);Check(p.Entity.MapID==1012 && p.Inventory.Contains(721109,1),"reconnect returns home while preserving an already-earned prize");SkyNpc(p,30055,1);
        Check(p.Entity.MapID==1040 && Kernel.GetDistance(p.Entity.X,p.Entity.Y,83,184)<=8,"Daniel restores access to God Cloud for a saved prize entitlement");
        var almostFull=Enumerable.Range(0,36).Select(i=>Pack(p,1088001,1)).ToArray();SkyNpc(p,30054,1);
        Check(p.Inventory.Count==37 && !HasAchievement(p,11019),"insufficient final reward slots preserve the prize token and achievement state");
        foreach(var item in almostFull){Query("DELETE FROM items WHERE uid=@id","id",(long)item.UID);p.Inventory.Remove(item.UID,Enums.ItemUse.None,false);}
        Query("ALTER TABLE revora_sky_claims ADD CONSTRAINT sky_claim_failure CHECK(false) NOT VALID");SkyNpc(p,30054,1);
        Check(Fenced(p) && p.Inventory.Contains(721109,1) && !HasAchievement(p,11019) && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id BETWEEN 800000 AND 899999",p.Entity.UID)==0,"failed final claim rolls back four Meteors, pass consumption and achievement");
        Query("ALTER TABLE revora_sky_claims DROP CONSTRAINT sky_claim_failure");DemonReset(p);SkyNear(p,30054);Parallel.For(0,12,i=>Npcs.GetDialog(new NpcRequest(true){NpcID=30054,OptionID=1},p));
        Check(p.Entity.MapID==1012 && p.Inventory.Count==5 && p.Inventory.Objects.Count(i=>i.ID==1088001 && i.Bound)==4
            && HasAchievement(p,11019) && Scalar("SELECT count(*) FROM revora_sky_claims WHERE uid=@uid",p.Entity.UID)==1,"ordinary concurrent summit claim pays four bound Meteors and one achievement exactly once");
        SkyNpc(p,30055,1);Check(p.Entity.MapID==1012,"daily completion blocks another same-day reward run");
        foreach(var item in p.Inventory.Objects)p.Inventory.Remove(item.UID,Enums.ItemUse.None,false);ConquerItemTable.LoadItems(p);Call(Sky,null,"OnLogin",p);EvidenceRefresh(p);
        VerifyDailyIslandRune(p,"revora_sky_claims");
        Check(p.Inventory.Count==5 && HasAchievement(p,11019),"ordinary item and achievement reload preserves the final reward");
        skyNow=skyNow.AddDays(1);SkyNpc(p,30055,1);SkyNpc(p,SkyId(1,0,"Gate"),1);SkyMob(p,1,0).Die(p.Entity);
        p.Entity.Die(SkyMob(p,1,0));Call(Sky,null,"Tick");
        Check(p.Entity.MapID==1012 && !p.Entity.Dead && SkyValue(p,"CASE WHEN phase='abandoned' THEN 1 ELSE 0 END")==1 && !p.Inventory.Contains(721100,1),"actual defeat abandons unfinished stages, removes their pass and returns the revived player home");
        SkyNpc(p,30055,1);SkyNpc(p,SkyId(1,4,"Gate"),1);Call(Sky,null,"Initialize");Call(Sky,null,"OnLogin",p);
        Check(p.Entity.MapID==1012 && SkyValue(p,"CASE WHEN phase='abandoned' THEN 1 ELSE 0 END")==1,"restart and reconnect cannot preserve unfinished stage position");
        SkyNpc(p,30055,1);SkyNpc(p,SkyId(1,0,"Gate"),1);var late=SkyMob(p,1,0);
        // Deterministically pause this death at its clock seam while the owner
        // leaves, starts a new run and selects the same family at the same time.
        Sky.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>{
            Sky.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>skyNow));
            SkyNpc(p,SkyId(1,0,"Guard"),9);SkyNpc(p,30055,1);SkyNpc(p,SkyId(1,0,"Gate"),1);return skyNow;
        }));late.Die(p.Entity);
        Check(!p.Inventory.Contains(721100,1) && SkyValue(p,"CASE WHEN phase='battle' THEN 1 ELSE 0 END")==1,"late monster death from an abandoned run cannot credit its replacement at an equal timestamp");
        p.Entity.Teleport(1012,156,166);skyNow=skyNow.AddSeconds(3);Call(Sky,null,"Tick");
        Check(SkyValue(p,"CASE WHEN phase='abandoned' THEN 1 ELSE 0 END")==1,"leaving through ordinary travel abandons unfinished progress");
        // Vary the sealed route to exercise every actual monster/gate/guard pair.
        var fought=new HashSet<uint>();
        for(int winner=0;winner<5;winner++) {
            skyRoll=winner;var runner=SkyPlayer(home);SkyNpc(runner,30055,1);
            if(winner==0)Query("UPDATE revora_sky_runs SET valuable_reward=true,rune_reward=false WHERE uid=@uid","uid",(long)runner.Entity.UID);
            for(int stage=1;stage<=4;stage++) {
                for(int route=0;route<5;route++)if(route!=winner){SkyNpc(runner,SkyId(stage,route,"Gate"),1);SkyFight(runner,stage,route);fought.Add(SkyId(stage,route,"Monster"));}
                SkyNpc(runner,SkyId(stage,winner,"Gate"),1);
            }
            SkyNpc(runner,SkyId(5,winner,"Gate"),1);SkyFight(runner,5,winner);fought.Add(SkyId(5,winner,"Monster"));SkyNpc(runner,30054,1);
            Check(HasAchievement(runner,11019) && runner.Inventory.Count==(winner==0?4:5),"every final tier retains its frozen reward contract through the complete route");
            if(winner!=0)VerifyDailyIslandRune(runner,"revora_sky_claims");
            else Check(runner.Inventory.Objects.All(i=>i.ID==1088001),"old Sky contract keeps its original four Meteors");
        }
        Check(fought.Count==25 && skyJumps>0,"all 25 monster/guard associations and real bounded player jumps work on the installed map");
    }
}
