using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type Snake=typeof(Entity).Assembly.GetType("Revora.Game.Features.SnakeIslandsQuest");
    static DateTime snakeNow;
    static readonly ushort[,] SnakeRoute={
        {417,367},{405,356},{390,370},{370,373},{350,378},{330,377},{322,358},{326,338},{330,318},{333,298},{338,278},{335,258},
        {318,250},{298,253},{278,258},{258,264},{238,266},{218,263},{210,246},{215,226},{218,206},{222,186},{226,166},{224,147},
        {206,138},{186,140},{166,146},{146,150},{126,154},{106,152},{98,134},{94,122},{90,110},{86,98},{79,82},{84,64}
    };
    static GameState SnakePlayer(Map bird) {var p=MoonPlayer(bird);var npc=bird.Npcs[30115];p.Entity.X=(ushort)(npc.X+2);p.Entity.Y=(ushort)(npc.Y+2);return p;}
    static void SnakeNpc(GameState p,uint npc,byte option) {SkyNear(p,npc,6);Npcs.GetDialog(new NpcRequest(true){NpcID=npc,OptionID=option},p);}
    static long SnakeCount(GameState p,string where) {return Scalar("SELECT count(*) FROM revora_snake_hunts WHERE uid=@uid AND "+where,p.Entity.UID);}
    static Entity SnakeMob(GameState p,uint type=3102,ushort x=84,ushort y=64) {
        var mob=(Entity)typeof(CompanionTests).GetMethod("Monster",Hidden).Invoke(null,new object[]{p.Map,x,y,1u});mob.MonsterInfo.ID=type;
        mob.MonsterInfo.Level=MonsterInformation.MonsterInformations[type].Level;mob.MonsterInfo.Name=mob.Name=MonsterInformation.MonsterInformations[type].Name;mob.IsDropped=true;return mob;
    }
    static void SnakeCross(GameState p) {
        if(p.Entity.MapID!=1063 || p.Entity.X!=SnakeRoute[0,0] || p.Entity.Y!=SnakeRoute[0,1])throw new Exception("Ferry did not reach the reviewed route origin.");
        for(int i=1;i<SnakeRoute.GetLength(0);i++) {
            if(!p.Map.Floor[SnakeRoute[i,0],SnakeRoute[i,1],MapObjectType.Player] || Kernel.GetDistance(p.Entity.X,p.Entity.Y,SnakeRoute[i,0],SnakeRoute[i,1])>20)throw new Exception("Snake route contains an invalid production jump.");
            SkyJump(p,SnakeRoute[i,0],SnakeRoute[i,1]);
        }
    }
    static void VerifySnake(string runtime,Map town) {
        Query("ALTER TABLE entities ADD COLUMN previousmapid integer DEFAULT 0,ADD COLUMN previousx integer DEFAULT 0,ADD COLUMN previousy integer DEFAULT 0,ADD COLUMN my_title integer DEFAULT 0,ADD COLUMN level integer DEFAULT 140,ADD COLUMN mapid integer DEFAULT 1002,ADD COLUMN x integer DEFAULT 0,ADD COLUMN y integer DEFAULT 0");
        Query("CREATE TABLE titles (LIKE public.titles INCLUDING ALL)");Query("CREATE TABLE itemadding (LIKE public.itemadding INCLUDING ALL)");
        Query("CREATE TABLE npcs (LIKE public.npcs INCLUDING ALL)");Query("INSERT INTO npcs SELECT * FROM public.npcs WHERE id IN (30115,30125)");
        StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);ConquerItem.ItemUID=new Counter(7100000);
        var bird=World(runtime,1015);var island=World(runtime,1063);Server.GamePool=new GameState[0];
        snakeNow=new DateTime(2026,9,10,20,0,0,DateTimeKind.Utc);Snake.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>snakeNow));
        Call(Snake,null,"Initialize");Call(Snake,null,"PlaceNpcs");
        Check(bird.Npcs.ContainsKey(30115) && island.Npcs.ContainsKey(30125) && new[]{3100u,3101u,3102u,3103u}.All(MonsterInformation.MonsterInformations.ContainsKey),"native ferrymen and all four installed snake families are available");
        var p=SnakePlayer(bird);p.Entity.Level=89;SnakeNpc(p,30115,1);Check(p.Entity.MapID==1015 && SnakeCount(p,"true")==0,"level 89 cannot board the island ferry");p.Entity.Level=90;
        p.Fake=true;SnakeNpc(p,30115,1);p.Fake=false;p.ActiveNpc=30115;ushort px=p.Entity.X,py=p.Entity.Y;p.Entity.X=1;p.Entity.Y=1;Npcs.GetDialog(new NpcRequest(true){NpcID=30115,OptionID=1},p);p.Entity.X=px;p.Entity.Y=py;
        Check(SnakeCount(p,"true")==0,"fake and remote ferry requests create no admission");
        p.Entity.Teleport(1063,84,64);p.Entity.TeleportHouse(1063,84,64);p.Entity.Teleport(1063,1063,84,64);
        Check(p.Entity.MapID==1015,"ordinary, house and dynamic teleport overloads cannot bypass ferry entry");
        SnakeNpc(p,30115,1);Check(p.Entity.MapID==1063 && p.Entity.X==417 && p.Entity.Y==367 && SnakeCount(p,"entered AND state='hunting'")==1,"ordinary ferry admission persists one attempt and reaches the far inhabited island");
        p.Entity.Teleport(84,64);p.Entity.Teleport(1063,84,64);p.Entity.Teleport(1063,1063,84,64);p.Entity.TeleportHouse(1063,84,64);
        Check(p.Entity.X==417 && p.Entity.Y==367,"large in-map teleports cannot bypass the island crossing");
        p.Entity.Teleport(405,356);Check(p.Entity.X==405 && p.Entity.Y==356,"valid bounded combat movement remains allowed on the islands");p.Entity.Teleport(417,367);
        var armed=Pack(p,410009,1);Query("UPDATE items SET position=4 WHERE uid=@uid","uid",(long)armed.UID);
        var distant=SnakeMob(p);distant.Die(p.Entity);SnakeMob(p,3101,417,367).Die(p.Entity);
        Check(!HasAchievement(p,11018) && SnakeCount(p,"state='victory'")==0,"distant boss and lesser snake kills cannot complete the island quest");
        SnakeCross(p);Check(p.Entity.X==84 && p.Entity.Y==64 && skyJumps==35,"ordinary player crosses the installed island chain through 35 production jumps");
        p.Fake=true;SnakeMob(p).Die(p.Entity);p.Fake=false;var unowned=SnakeMob(p);island.Entities.Remove(unowned.UID);unowned.Die(p.Entity);
        Check(!HasAchievement(p,11018),"fake killer and an unowned boss entity supply no victory evidence");
        Query("DELETE FROM items WHERE uid=@uid","uid",(long)armed.UID);p.Inventory.Remove(armed.UID,Enums.ItemUse.None,false);
        var retry=SnakeMob(p);Check(retry.MonsterInfo.Level==120 && !HasAchievement(p,10525),"loaded Snake King reaches unarmed threshold without earlier armed credit");
        Query("ALTER TABLE revora_snake_attempts ADD CONSTRAINT snake_victory_failure CHECK(reason<>'victory') NOT VALID");retry.Die(p.Entity);
        Check(!Fenced(p) && !retry.Dead && SnakeCount(p,"state='victory'")==0 && !HasAchievement(p,11018) && !HasAchievement(p,10525),"failed victory keeps boss alive and rolls back both achievements without fencing player");
        Query("ALTER TABLE revora_snake_attempts DROP CONSTRAINT snake_victory_failure");
        Query("CREATE FUNCTION reject_snake_unarmed() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER reject_snake_unarmed BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW WHEN(NEW.achievement_id=10525) EXECUTE FUNCTION reject_snake_unarmed()");
        retry.Die(p.Entity);
        Check(!retry.Dead && SnakeCount(p,"state='victory'")==0 && !HasAchievement(p,11018) && !HasAchievement(p,10525),"suppressed unarmed insert rolls back quest victory and leaves same boss retryable");
        Query("DROP TRIGGER reject_snake_unarmed ON revora_achievement_evidence;DROP FUNCTION reject_snake_unarmed()");
        var victory=typeof(Entity).Assembly.GetType("Revora.Game.Features.WildMonsterVictory");
        victory.GetField("AfterCommit",Hidden).SetValue(null,(Action)(()=>{throw new System.IO.IOException("fixture acknowledgement lost");}));
        try{retry.Die(p.Entity);}finally{victory.GetField("AfterCommit",Hidden).SetValue(null,null);}
        Check(!retry.Dead && SnakeCount(p,"state='victory'")==1 && Scalar("SELECT count(*) FROM revora_unarmed_wild_victories WHERE uid=@uid",p.Entity.UID)==1,"lost acknowledgement preserves one durable victory while boss waits for retry");
        Parallel.For(0,12,i=>retry.Die(p.Entity));
        Check(retry.Dead && HasAchievement(p,11018) && HasAchievement(p,10525) && SnakeCount(p,"state='victory'")==1 && Scalar("SELECT count(*) FROM revora_snake_attempts WHERE uid=@uid AND reason='victory'",p.Entity.UID)==1
            && Scalar("SELECT count(*) FROM revora_unarmed_wild_victories WHERE uid=@uid",p.Entity.UID)==1,"concurrent retry resolves same life with immediate quest and unarmed credit exactly once");
        var full=Enumerable.Range(0,38).Select(i=>Pack(p,1088001,1)).ToArray();SnakeNpc(p,30125,3);
        Check(p.Inventory.Count==38 && SnakeCount(p,"state='victory'")==1,"insufficient bag space preserves an earned island reward");
        foreach(var item in full){Query("DELETE FROM items WHERE uid=@id","id",(long)item.UID);p.Inventory.Remove(item.UID,Enums.ItemUse.None,false);}
        Query("ALTER TABLE revora_snake_hunts ADD CONSTRAINT snake_claim_failure CHECK(state<>'claimed') NOT VALID");SnakeNpc(p,30125,3);
        Check(Fenced(p) && p.Inventory.Count==0 && Scalar("SELECT count(*) FROM items WHERE entityid=@uid",p.Entity.UID)==0 && SnakeCount(p,"state='victory'")==1,"failed reward claim rolls back both Meteors and preserves the victory entitlement");
        Query("ALTER TABLE revora_snake_hunts DROP CONSTRAINT snake_claim_failure");DemonReset(p);Call(Snake,null,"OnLogin",p);snakeNow=snakeNow.AddDays(1);
        SkyNear(p,30115,6);Parallel.For(0,12,i=>Npcs.GetDialog(new NpcRequest(true){NpcID=30115,OptionID=3},p));
        Check(p.Inventory.Count==3 && p.Inventory.Objects.Count(i=>i.ID==1088001 && i.Bound)==2 && SnakeCount(p,"state='claimed'")==1,"saved previous-day victory pays two bound Meteors once through ordinary concurrent ferry claims");
        SnakeNpc(p,30115,3);foreach(var item in p.Inventory.Objects)p.Inventory.Remove(item.UID,Enums.ItemUse.None,false);ConquerItemTable.LoadItems(p);Call(Snake,null,"OnLogin",p);EvidenceRefresh(p);
        VerifyDailyIslandRune(p,"revora_snake_hunts");
        Check(p.Inventory.Count==3 && HasAchievement(p,11018) && HasAchievement(p,10525),"inventory and achievement reload preserve exactly the earned reward and completion");
        SnakeNpc(p,30115,1);Check(SnakeCount(p,"true")==2,"the next Pacific quest day permits a fresh ferry admission");
        p.Entity.Die(SnakeMob(p,3100,417,367));Call(Snake,null,"Tick");
        Check(p.Entity.MapID==1015 && !p.Entity.Dead && !p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Dead) && !p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Ghost) && SnakeCount(p,"entered")==0,"actual snake-inflicted defeat closes the active attempt and returns a player free of both death flags");
        p.Entity.AddFlag((ulong)FlagsUpdate.Flags.Dead,60,false);p.Entity.AddFlag((ulong)FlagsUpdate.Flags.Ghost,60,false);
        p.Entity.BringToLifeLotus();
        Check(!p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Dead) && !p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Ghost),"Lotus revival also removes the separate death and ghost flag indices");
        SnakeNpc(p,30115,1);SnakeCross(p);var late=SnakeMob(p);
        var attemptType=Snake.GetNestedType("Attempt",System.Reflection.BindingFlags.NonPublic);
        var stale=Activator.CreateInstance(attemptType,true);attemptType.GetField("Id",Instance).SetValue(stale,Guid.NewGuid());attemptType.GetField("Day",Instance).SetValue(stale,"20260911");
        Call(Snake,null,"Close",p,stale,true,false,"leave");
        Check(p.Entity.MapID==1063 && SnakeCount(p,"entered")==1,"stale cleanup cannot eject or remove a replacement attempt");
        Snake.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>{
            Snake.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>snakeNow));SnakeNpc(p,30125,2);SnakeNpc(p,30115,1);SnakeCross(p);return snakeNow;
        }));late.Die(p.Entity);
        Check(SnakeCount(p,"state='victory'")==0 && SnakeCount(p,"entered")==1,"late old-attempt boss death cannot complete a replacement ferry admission");
        snakeNow=new DateTime(2026,9,12,7,0,0,DateTimeKind.Utc);SnakeMob(p).Die(p.Entity);Call(Snake,null,"Tick");
        Check(p.Entity.MapID==1015 && SnakeCount(p,"entered")==0 && SnakeCount(p,"state='victory'")==0,"Pacific midnight closes an unfinished day and rejects its late boss death");
        var old=SnakePlayer(bird);SnakeNpc(old,30115,1);
        Query("UPDATE revora_snake_hunts SET valuable_reward=true,rune_reward=false WHERE uid=@uid","uid",(long)old.Entity.UID);
        Call(Snake,null,"OnLogin",old);SnakeNpc(old,30115,1);SnakeCross(old);
        var oldWeapon=Pack(old,410009,1);Query("UPDATE items SET position=4 WHERE uid=@uid","uid",(long)oldWeapon.UID);
        SnakeMob(old).Die(old.Entity);
        Check(HasAchievement(old,11018) && !HasAchievement(old,10525) && SnakeCount(old,"state='victory'")==1,"armed ordinary killer retains quest victory without unarmed achievement");
        Query("DELETE FROM items WHERE uid=@uid","uid",(long)oldWeapon.UID);old.Inventory.Remove(oldWeapon.UID,Enums.ItemUse.None,false);SnakeNpc(old,30125,3);
        Check(!Fenced(old) && old.Inventory.Count==2 && old.Inventory.Objects.All(i=>i.ID==1088001 && i.Bound),"restarted old Snake contract retains original reward without adding a soul");
        Call(Snake,null,"Initialize");VerifyDailyIslandRune(p,"revora_snake_hunts");
        var bypass=SnakePlayer(bird);bypass.BindWorldMap(1063);bypass.Entity.X=84;bypass.Entity.Y=64;SnakeMob(bypass).Die(bypass.Entity);
        Check(!HasAchievement(bypass,11018) && SnakeCount(bypass,"true")==0,"even an invalid externally placed character cannot gain victory without owned admission");
    }
}
