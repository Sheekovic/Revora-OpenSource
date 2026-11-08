using System;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network;
using Revora.Network.GamePackets;
internal static partial class PotencyHuntTests {
    static void VerifyGuildBeastWorld() {
        var type=typeof(Entity).Assembly.GetType("Revora.Game.Features.GuildBeastEvent",true);var map=Kernel.Maps[1038];
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new NpgsqlCommand("SELECT id,cellx,celly,lookface,type,name FROM public.npcs WHERE id=8150 AND mapid=1038",c))
        using(var r=q.ExecuteReader()){Check(r.Read(),"installed Beast Guard catalog row exists");map.AddNpc(new NpcSpawn{UID=Convert.ToUInt32(r[0]),MapID=1038,X=Convert.ToUInt16(r[1]),Y=Convert.ToUInt16(r[2]),Mesh=Convert.ToUInt16(r[3]),Type=(Enums.NpcType)Convert.ToByte(r[4]),Name=Convert.ToString(r[5])});}}
        Map.CreateTimerFactories();Revora.Thread.GenericThreadPool=new System.Threading.StaticPool(1);
        var leader=PrizeLeader(270);EarnGuildToken(leader,1,15,2034);
        DateTime now=new DateTime(2034,1,16,12,0,0,DateTimeKind.Local).ToUniversalTime();type.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        Call(type,null,"Initialize");
        var approach=new object[]{map,(ushort)0,(ushort)0};Check((bool)Call(type,null,"Approach",approach),"installed Beast Guard has a legal reachable talking tile");
        ushort approachX=(ushort)approach[1],approachY=(ushort)approach[2];
        Action<GameState> near=p=>{p.BindWorldMap(1038);p.Entity.X=approachX;p.Entity.Y=approachY;p.ActiveNpc=8150;};
        Action<GameState,byte> talk=(p,option)=>{p.ActiveNpc=8150;Npcs.GetDialog(new NpcRequest(true){NpcID=8150,OptionID=option},p);};
        Func<Entity> beast=()=>map.Entities.Values.Single(m=>m.MonsterInfo!=null && m.MonsterInfo.ID==7006);
        Action<GameState,Entity> kill=(p,m)=>{p.Entity.X=m.X;p.Entity.Y=m.Y;p.Screen.Add(m);uint damage=m.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,m,new Attack(true){Attacker=p.Entity.UID,Attacked=m.UID,AttackType=Attack.Melee},ref damage,null);};
        near(leader);Query("UPDATE guilds SET silverfund=1500000 WHERE id=270");leader.Guild.SilverFund=1500000;
        var member=PurseMember(leader,Enums.GuildMemberRank.Member);near(member);talk(member,1);
        Check(Scalar("SELECT count(*) FROM revora_guild_beast_days",0)==0,"ordinary member cannot spend the winning guild's summon funds");
        talk(leader,0);talk(leader,1);talk(leader,1);
        Check(!Fenced(leader) && leader.Guild.SilverFund==1000000 && Scalar("SELECT count(*) FROM revora_guild_beast_days",0)==1,
            "ordinary winning leader reserves the real Beast Guard hunt once through NPC dispatch");
        Call(type,null,"Tick");Check(!map.Entities.Values.Any(m=>m.MonsterInfo!=null && m.MonsterInfo.ID==7006),"paid beast does not appear before its daily window");
        now=now.AddHours(9);Call(type,null,"Tick");var body=beast();
        Check(body.MonsterInfo.Level==120 && body.Hitpoints==MonsterInformation.MonsterInformations[7006].Hitpoints && body.Name.Replace(" ","")=="GuildBeast"
            && !map.Entities.Values.Any(m=>m.MonsterInfo!=null && m.MonsterInfo.ID==4211),"timed event spawns the actual Guild Beast instead of the legacy HollowBeast");
        var hunter=FullRunePlayer(Kernel.Maps[1002],15);hunter.Entity.DailySignDays=new byte[0];EntityTable.SaveEntity(hunter);
        hunter.Entity.X=317;hunter.Entity.Y=322;hunter.ActiveNpc=10003;
        Npcs.GetDialog(new NpcRequest(true){NpcID=10003,OptionID=89},hunter);
        Check(!Fenced(hunter) && hunter.Entity.MapID==1038 && hunter.Entity.X==approachX && hunter.Entity.Y==approachY
            && Scalar("SELECT mapid FROM entities WHERE uid=@uid",hunter.Entity.UID)==1038,"unguilded ordinary player reaches Beast Guard through the Guild Director with saved travel");
        near(hunter);
        Check(hunter.Account.State==AccountTable.AccountState.Player && hunter.Entity.GuildID==0,"public beast hunt accepts an ordinary unguilded player");
        hunter.Entity.X=body.X;hunter.Entity.Y=body.Y;hunter.Screen.Add(body);uint chip=1;
        Revora.Game.Attacking.Handle.ReceiveAttack(hunter.Entity,body,new Attack(true){Attacker=hunter.Entity.UID,Attacked=body.UID,AttackType=Attack.Melee},ref chip,null);
        Check(!HasAchievement(hunter,10303) && !HasAchievement(hunter,10525) && body.Hitpoints==body.MaxHitpoints-1,"nonlethal combat does not award the Guild Beast achievement");
        kill(hunter,body);
        Check(!Fenced(hunter) && body.Dead && !map.Entities.ContainsKey(body.UID) && HasAchievement(hunter,10303) && HasAchievement(hunter,10525),"normal lethal combat saves and projects achievement then retires the owned beast");
        uint promisedSoul=(uint)Scalar("SELECT soul_type FROM revora_guild_beast_days WHERE killer=@uid",hunter.Entity.UID);
        Check(promisedSoul>=800000 && promisedSoul<900000 && ConquerItemInformation.BaseInformations[promisedSoul].PurificationLevel==7,
            "Guild Beast kill freezes a catalog P7 soul with the earned result");
        Parallel.For(0,8,i=>body.Die(hunter.Entity));now=now.AddSeconds(6);Call(type,null,"Tick");
        Check(Scalar("SELECT count(*) FROM revora_guild_beast_days WHERE state='killed'",0)==1 && !map.Entities.Values.Any(m=>m.MonsterInfo!=null && m.MonsterInfo.ID==7006),"duplicate deaths and the world pulse cannot respawn a settled beast");
        Check(Scalar("SELECT soul_type FROM revora_guild_beast_days WHERE killer=@uid",hunter.Entity.UID)==promisedSoul,
            "Guild Beast repeated death cannot reroll the promised soul");
        near(hunter);talk(hunter,2);talk(hunter,2);
        Check(!Fenced(hunter) && hunter.Inventory.Objects.Count(i=>i.ID==1088000)==1 && Scalar("SELECT count(*) FROM revora_guild_beast_days WHERE claimed_uid=@uid",hunter.Entity.UID)==1,
            "killer receives exactly one Dragon Ball through ordinary NPC delivery");
        var soul=hunter.Inventory.Objects.Single(i=>i.ID==promisedSoul);uint soulUid=soul.UID;
        Check(soul.Bound && Scalar("SELECT soul_uid FROM revora_guild_beast_days WHERE claimed_uid=@uid",hunter.Entity.UID)==soulUid,
            "Guild Beast ordinary NPC claim stores exact bound soul receipt");
        Query("DELETE FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10303","uid",(long)hunter.Entity.UID);
        hunter=ReloadBall(hunter);hunter.Entity.MyAchievement.Send();
        Check(HasAchievement(hunter,10303) && HasAchievement(hunter,10525) && hunter.Inventory.Objects.Count(i=>i.ID==1088000)==1,"real character and item reload retain the earned achievement and prize");
        Check(hunter.Inventory.Objects.Count(i=>i.UID==soulUid && i.ID==promisedSoul && i.Bound)==1,
            "Guild Beast bound P7 identity survives real item reload");
        now=new DateTime(2034,1,17,12,0,0,DateTimeKind.Local).ToUniversalTime();near(leader);talk(leader,1);now=now.AddHours(9);Call(type,null,"Tick");body=beast();
        now=now.AddHours(1);Call(type,null,"Tick");
        Check(!map.Entities.ContainsKey(body.UID) && Scalar("SELECT count(*) FROM revora_guild_beast_days WHERE state='fallback'",0)==1,"undefeated beast disappears at the deadline and saves the fallback entitlement");
        near(member);talk(member,2);Check(!member.Inventory.Objects.Any(i=>i.ID==1088000),"ordinary member cannot collect the leadership fallback reward");
        var deputy=PurseMember(leader,Enums.GuildMemberRank.DeputyLeader);near(deputy);talk(deputy,2);talk(leader,2);
        Check(deputy.Inventory.Objects.Count(i=>i.ID==1088000)==1 && !HasAchievement(deputy,10303) && !leader.Inventory.Objects.Any(i=>i.ID==1088000),
            "first eligible deputy receives one fallback prize without invented kill credit or a second leader prize");
        Check(!deputy.Inventory.Objects.Any(i=>i.ID>=800000 && i.ID<900000),"Guild Beast no-kill fallback does not award a P7 soul");
        now=new DateTime(2034,1,18,12,0,0,DateTimeKind.Local).ToUniversalTime();near(leader);talk(leader,1);now=now.AddHours(9);Call(type,null,"Tick");body=beast();
        var stale=body;
        type.GetField("enabled",Hidden).SetValue(null,false);Call(type,null,"Initialize");Call(type,null,"Tick");body=beast();
        near(member);member.Entity.X=stale.X;member.Entity.Y=stale.Y;stale.Die(member.Entity);
        Check(!ReferenceEquals(stale,body) && !body.Dead && Scalar("SELECT count(*) FROM revora_guild_beast_days WHERE day='2034-01-18' AND state='active'",0)==1,
            "world reinitialization replaces the life and rejects stale actor death callbacks");
        var copyInfo=MonsterInformation.MonsterInformations[7006].Copy();
        var copy=new Entity(EntityFlag.Monster,false){UID=map.EntityUIDCounter.Next,MapID=1038,X=body.X,Y=body.Y,MonsterInfo=copyInfo,Hitpoints=10,MaxHitpoints=10,IsDropped=true};copyInfo.Owner=copy;
        map.AddEntity2(copy);copy.Die(member.Entity);map.RemoveEntity(copy);
        Check(Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10303",member.Entity.UID)==0 && Scalar("SELECT count(*) FROM revora_guild_beast_days WHERE day='2034-01-18' AND state='active'",0)==1,
            "a generic copy of family 7006 cannot settle the owned hunt or award its achievement");
        lock(member.ItemSyncRoot){var busy=Task.Run(()=>body.Die(member.Entity));Check(busy.Wait(TimeSpan.FromSeconds(5)) && !body.Dead,"busy killer ownership defers a lethal callback without deadlock");}
        kill(member,body);near(member);
        Query("CREATE FUNCTION refuse_beast_item() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");Query("CREATE TRIGGER refuse_beast_item BEFORE INSERT ON items FOR EACH ROW WHEN (NEW.id BETWEEN 800000 AND 899999) EXECUTE FUNCTION refuse_beast_item()");
        try{talk(member,2);}finally{Query("DROP TRIGGER refuse_beast_item ON items;DROP FUNCTION refuse_beast_item()");}
        Check(Fenced(member) && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id=1088000",member.Entity.UID)==0
            && Scalar("SELECT count(*) FROM revora_guild_beast_days WHERE killer=@uid AND claimed_at IS NULL AND soul_uid=0",member.Entity.UID)==1,
            "failed soul insert rolls back Dragon Ball and claim receipt together");
        member=ReloadBall(member);near(member);talk(member,2);
        Check(!Fenced(member) && member.Inventory.Objects.Count(i=>i.ID==1088000)==1,"ordinary reconnect retries delivery without another kill or fee");
        now=new DateTime(2034,1,19,12,0,0,DateTimeKind.Local).ToUniversalTime();near(leader);leader.Entity.Money=500000;EntityTable.SaveEntity(leader);
        GuildCommand.Process(DonationPacket(500000),leader);talk(leader,1);
        Check(!Fenced(leader) && leader.Guild.SilverFund==0 && Scalar("SELECT count(*) FROM revora_guild_beast_days",0)==4,
            "normal guild donation funds a later paid hunt without creating or losing silver");
        now=now.AddHours(9);Call(type,null,"Tick");body=beast();
        var fresh=FullRunePlayer(Kernel.Maps[1002],15);fresh.Entity.DailySignDays=new byte[0];EntityTable.SaveEntity(fresh);near(fresh);fresh.Entity.X=body.X;fresh.Entity.Y=body.Y;
        fresh.Fake=true;body.Die(fresh.Entity);fresh.Fake=false;fresh.BindWorldMap(1002);body.Die(fresh.Entity);fresh.BindWorldMap(1038);
        Kernel.GamePool.Remove(fresh.Entity.UID);body.Die(fresh.Entity);Kernel.GamePool.Add(fresh.Entity.UID,fresh);
        Check(!body.Dead && Scalar("SELECT count(*) FROM revora_guild_beast_days WHERE day='2034-01-19' AND state='active'",0)==1,
            "fake, remote and replaced sessions cannot settle a live hunt");
        Query("CREATE FUNCTION refuse_beast_world_credit() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
        Query("CREATE TRIGGER refuse_beast_world_credit BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW WHEN (NEW.achievement_id=10303) EXECUTE FUNCTION refuse_beast_world_credit()");
        try{kill(fresh,body);}finally{Query("DROP TRIGGER refuse_beast_world_credit ON revora_achievement_evidence;DROP FUNCTION refuse_beast_world_credit()");}
        Check(Fenced(fresh) && !body.Dead && Scalar("SELECT count(*) FROM revora_guild_beast_days WHERE day='2034-01-19' AND state='active' AND killer IS NULL",0)==1,
            "failed kill evidence leaves the owned beast alive and its day unsettled");
        fresh=ReloadBall(fresh);near(fresh);
        Query("CREATE FUNCTION refuse_beast_unarmed() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
        Query("CREATE TRIGGER refuse_beast_unarmed BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW WHEN (NEW.achievement_id=10525) EXECUTE FUNCTION refuse_beast_unarmed()");
        try{kill(fresh,body);}finally{Query("DROP TRIGGER refuse_beast_unarmed ON revora_achievement_evidence;DROP FUNCTION refuse_beast_unarmed()");}
        Check(Fenced(fresh) && !body.Dead && !HasAchievement(fresh,10525)
            && Scalar("SELECT count(*) FROM revora_guild_beast_days WHERE day='2034-01-19' AND state='active' AND killer IS NULL AND soul_type=0",0)==1,
            "missing unarmed evidence rolls back beast victory and promised soul while preserving the living boss");
        fresh=ReloadBall(fresh);near(fresh);kill(fresh,body);near(fresh);
        Check(HasAchievement(fresh,10525),"reconnected ordinary killer retries living beast and receives unarmed credit");
        for(int i=0;i<39;i++)Pack(fresh,1088001,1);talk(fresh,2);
        Check(!Fenced(fresh) && Scalar("SELECT count(*) FROM revora_guild_beast_days WHERE killer=@uid AND claimed_at IS NULL",fresh.Entity.UID)==1,
            "one free space preserves both rewards until two places are available");
        foreach(var item in fresh.Inventory.Objects.ToArray())DisRemove(fresh,item);
        now=new DateTime(2034,1,20,0,0,0,DateTimeKind.Local).ToUniversalTime();Call(type,null,"Tick");talk(fresh,2);
        Check(fresh.Inventory.Objects.Count(i=>i.ID==1088000)==1 && HasAchievement(fresh,10303),"a delayed killer prize remains deliverable after midnight");
        type.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow));
        var timer=(IDisposable)typeof(Map).GetField("Timer",Instance).GetValue(map);if(timer!=null)timer.Dispose();
    }
}
