using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Npgsql;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.ConquerStructures.Society;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type GuildSeason=typeof(Entity).Assembly.GetType("Revora.Game.Features.GuildSeasonWar");
    static DateTime guildNow;
    static DateTime GuildLocal(int month,int day,int hour,int minute=0) {return new DateTime(2026,month,day,hour,minute,0,DateTimeKind.Local).ToUniversalTime();}
    static GameState GuildFighter(Map map,uint id) {
        var p=Player(map);p.Entity.FullyLoaded=true;p.Attackable=true;
        Guild g;if(!Kernel.Guilds.TryGetValue(id,out g)){g=new Guild(p.Entity.Name){ID=id,Name="SeasonGuild"+id};Kernel.Guilds[id]=g;Query("INSERT INTO guilds(id,wins,losts) VALUES(@id,0,0)","id",(long)id);}
        p.Guild=g;p.Entity.CharBuffer._GuildID=id;p.AsMember=new Guild.Member(id){ID=p.Entity.UID,Rank=Enums.GuildMemberRank.Member};g.Members[p.Entity.UID]=p.AsMember;
        Query("UPDATE entities SET guildid=@guild WHERE uid=@uid","guild",(long)id,"uid",(long)p.Entity.UID);
        p.Entity.X=(ushort)(GuildWar.Pole.X+1);p.Entity.Y=GuildWar.Pole.Y;return p;
    }
    static void GuildHit(GameState p,SobNpcSpawn pole,uint damage) {
        Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,pole,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=pole.UID},damage,null);
    }
    static void GuildRestart() {
        GuildSeason.GetField("<Enabled>k__BackingField",Hidden).SetValue(null,false);Call(GuildSeason,null,"Initialize");
    }
    static void VerifyGuildSeason(string runtime) {
        Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.Tournaments.LocalTeamPkLedger",true),null,"Initialize");
        Query("ALTER TABLE entities ADD COLUMN conquerpoints bigint NOT NULL DEFAULT 0,ADD COLUMN revorapoint bigint NOT NULL DEFAULT 0,ADD COLUMN money bigint NOT NULL DEFAULT 0,ADD COLUMN x integer NOT NULL DEFAULT 0,ADD COLUMN y integer NOT NULL DEFAULT 0,ADD COLUMN mapid integer NOT NULL DEFAULT 1038,ADD COLUMN guildid bigint NOT NULL DEFAULT 0");
        Query("CREATE TABLE sobnpcs (LIKE public.sobnpcs INCLUDING ALL);INSERT INTO sobnpcs SELECT * FROM public.sobnpcs WHERE mapid=1038");
        Query("CREATE TABLE guilds(id bigint PRIMARY KEY,wins bigint NOT NULL,losts bigint NOT NULL)");
        var map=World(runtime,1038);Server.GamePool=new GameState[0];
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new NpgsqlCommand("SELECT id,cellx,celly,lookface,type,life,maxlife,name FROM sobnpcs WHERE id IN(810,516074,516075) ORDER BY id",c))
        using(var r=q.ExecuteReader())while(r.Read())map.AddNpc(new SobNpcSpawn{UID=Convert.ToUInt32(r[0]),MapID=1038,X=Convert.ToUInt16(r[1]),Y=Convert.ToUInt16(r[2]),
            Mesh=Convert.ToUInt16(r[3]),Type=(Enums.NpcType)Convert.ToByte(r[4]),Hitpoints=Convert.ToUInt32(r[5]),MaxHitpoints=Convert.ToUInt32(r[6]),Name=Convert.ToString(r[7])});}
        Check(map.Npcs.Count==3 && map.Npcs.Values.Cast<SobNpcSpawn>().All(n=>n.MaxHitpoints>0),"weekly guild battlefield uses the three installed structures and their real HP");
        guildNow=GuildLocal(9,20,14,59);GuildSeason.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>guildNow));Call(GuildSeason,null,"Initialize");
        var pole=GuildWar.Pole;var a=GuildFighter(map,201);var b=GuildFighter(map,202);var ally=GuildFighter(map,201);
        Check(a.Account.State==AccountTable.AccountState.Player && !a.Fake,"ordinary non-GM guild members enter the season combat fixture");
        GuildWar.PoleKeeper=b.Guild;GuildWar.Pole.Name=b.Guild.Name;GuildWar.Pole.Hitpoints=1;
        GuildWar.Start();Check(!GuildWar.IsWar && Scalar("SELECT count(*) FROM revora_guild_battles",0)==0,"early start cannot mint a scheduled guild result");
        guildNow=GuildLocal(9,20,15);Parallel.For(0,8,i=>GuildWar.Start());
        Check(GuildWar.IsWar && GuildWar.PoleKeeper==null && pole.Hitpoints==pole.MaxHitpoints && pole.Name=="Guild Pole"
            && Scalar("SELECT count(*) FROM revora_guild_battles",0)==1,"concurrent scheduled start creates one battle and clears previous ownership and damage");
        Check(a.Entity.StrResID==10515 && b.Entity.StrResID==10515,"managed scheduled start retains the existing ordinary-player guild invitation");
        Check(Scalar("SELECT round FROM revora_guild_battles WHERE day='2026-09-20'",0)==1,"first Sunday is round one of the recorded season");
        Call(GuildSeason,null,"Initialize");Check(GuildWar.IsWar,"repeated initialization cannot cancel the current owned battlefield");
        GuildWar.End();Check(GuildWar.IsWar && Scalar("SELECT count(*) FROM revora_guild_battles WHERE state='active'",0)==1,"premature finalization cannot record a winner");
        uint full=pole.MaxHitpoints;
        a.Fake=true;GuildHit(a,pole,full);a.Fake=false;Check(pole.Hitpoints==full,"adventurers cannot contribute seasonal guild damage");
        a.Entity.Hitpoints=0;GuildHit(a,pole,full);a.Entity.Hitpoints=a.Entity.MaxHitpoints;Check(pole.Hitpoints==full,"fallen players cannot capture a guild pole");
        var current=a.AsMember;a.AsMember=new Guild.Member(a.Guild.ID){ID=a.Entity.UID};GuildHit(a,pole,full);a.AsMember=current;
        Check(pole.Hitpoints==full,"replaced guild membership cannot claim a seasonal hit");
        Kernel.GamePool[a.Entity.UID]=b;GuildHit(a,pole,full);Kernel.GamePool[a.Entity.UID]=a;
        Check(pole.Hitpoints==full,"replaced player session cannot claim a seasonal hit");
        ushort x=a.Entity.X;a.Entity.X=0;GuildHit(a,pole,full);a.Entity.X=x;Check(pole.Hitpoints==full,"out-of-view pole attacks are rejected");
        var forged=new SobNpcSpawn{UID=pole.UID,MapID=1038,X=pole.X,Y=pole.Y,MaxHitpoints=full,Hitpoints=full,Name="Clone"};
        GuildHit(a,forged,full);Check(forged.Hitpoints==full && pole.Hitpoints==full,"same-ID forged structure cannot enter the seasonal ledger");
        GuildHit(a,pole,0);GuildWar.AddScore(full,a.Guild);GuildWar.FinishRound();GuildWar.Reset();
        Check(pole.Hitpoints==full && GuildWar.PoleKeeper==null,"legacy direct score and reset callbacks cannot manufacture a seasonal capture");
        Check(Revora.Game.Attacking.Handle.CanAttack(a.Entity,pole,null),"native combat eligibility admits the canonical guild fighter");
        guildNow=GuildLocal(9,20,14,59);GuildHit(a,pole,1);guildNow=GuildLocal(9,20,15);
        Check(pole.Hitpoints==full && Scalar("SELECT count(*) FROM revora_guild_pole_hits WHERE uid=@uid",a.Entity.UID)==0,"clock rollback before battle start cannot earn pillar credit or deal damage");
        Query("UPDATE entities SET guildid=0 WHERE uid=@uid","uid",(long)a.Entity.UID);GuildHit(a,pole,1);
        Check(pole.Hitpoints==full && Scalar("SELECT count(*) FROM revora_guild_pole_hits WHERE uid=@uid",a.Entity.UID)==0,"stale saved guild membership cannot earn a first pillar receipt");
        Query("UPDATE entities SET guildid=@guild WHERE uid=@uid","guild",(long)a.Guild.ID,"uid",(long)a.Entity.UID);
        lock(a.ItemSyncRoot){var busy=Task.Run(()=>GuildHit(a,pole,1));Check(busy.Wait(TimeSpan.FromSeconds(5)) && pole.Hitpoints==full,"busy owner lock rejects a pillar hit promptly without taking the battlefield gate");}
        foreach(string table in new[]{"revora_guild_pole_hits","revora_achievement_evidence"}){
            Query("CREATE FUNCTION suppress_pillar() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
            Query("CREATE TRIGGER suppress_pillar BEFORE INSERT ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_pillar()");
            try{GuildHit(a,pole,1);}finally{Query("DROP TRIGGER suppress_pillar ON "+table+";DROP FUNCTION suppress_pillar()");}
            Check(pole.Hitpoints==full && Scalar("SELECT count(*) FROM revora_guild_pole_hits WHERE uid=@uid",a.Entity.UID)==0
                && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10232",a.Entity.UID)==0,"failed first-hit persistence rolls back receipt and achievement without applying damage: "+table);
        }
        GuildHit(a,pole,full*3/4-1);GuildHit(a,pole,1);Check(pole.Hitpoints==full-full*3/4,"accepted damage is applied once through the real SobNpc dispatcher");
        Check(Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10232",a.Entity.UID)==1,"first accepted nonlethal pole hit earns durable pillar credit before any capture");
        Check(Scalar("SELECT count(*) FROM revora_guild_pole_hits WHERE uid=@uid",a.Entity.UID)==1 && Scalar("SELECT damage FROM revora_guild_pole_hits WHERE uid=@uid",a.Entity.UID)==full*3/4-1,"later accepted damage keeps one receipt describing the actual first hit");
        Query("CREATE FUNCTION suppress_guild_capture() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_guild_capture BEFORE INSERT ON revora_guild_captures FOR EACH ROW EXECUTE FUNCTION suppress_guild_capture()");
        try{GuildHit(b,pole,uint.MaxValue);}finally{Query("DROP TRIGGER suppress_guild_capture ON revora_guild_captures;DROP FUNCTION suppress_guild_capture()");}
        Check(GuildWar.PoleKeeper==null && pole.Hitpoints==full-full*3/4 && Scalar("SELECT captures FROM revora_guild_battles WHERE day='2026-09-20'",0)==0,
            "suppressed capture receipt rolls back ownership and retains the unaccepted final strike");
        Query("CREATE FUNCTION suppress_guild_member() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_guild_member BEFORE INSERT ON revora_guild_contributors FOR EACH ROW EXECUTE FUNCTION suppress_guild_member()");
        try{GuildHit(b,pole,uint.MaxValue);}finally{Query("DROP TRIGGER suppress_guild_member ON revora_guild_contributors;DROP FUNCTION suppress_guild_member()");}
        Check(GuildWar.PoleKeeper==null && Scalar("SELECT count(*) FROM revora_guild_captures",0)==0,"suppressed contributor receipt rolls back the entire capture");
        Check(Scalar("SELECT count(*) FROM revora_guild_pole_hits WHERE uid=@uid",b.Entity.UID)==0,"rejected lethal strikes cannot leave a pillar receipt outside the failed capture transaction");
        GuildHit(b,pole,uint.MaxValue);
        Check(GuildWar.PoleKeeper==a.Guild && pole.Hitpoints==full && Scalar("SELECT count(*) FROM revora_guild_captures",0)==1,"highest accepted guild damage wins capture and restores full pole HP without a second decrement");
        Check(Scalar("SELECT sum(damage) FROM revora_guild_contributors",0)==full && Scalar("SELECT count(*) FROM revora_guild_contributors",0)==2,"capture evidence includes actual contributors and caps overkill at remaining HP");
        Check(Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10232",b.Entity.UID)==1,"accepted final strike earns pillar credit even when another guild wins the damage contest");
        var battle=GuildSeason.GetField("active",Hidden).GetValue(null);
        battle.GetType().GetField("Captures",Instance).SetValue(battle,0);battle.GetType().GetField("Keeper",Instance).SetValue(battle,0u);GuildWar.PoleKeeper=null;
        GuildHit(b,pole,full);
        Check(GuildWar.PoleKeeper==a.Guild && pole.Hitpoints==full && Scalar("SELECT count(*) FROM revora_guild_captures",0)==1,
            "committed capture with a lost runtime projection recovers its saved winner instead of recording another capture");
        Check(a.Guild.Wins==1 && Scalar("SELECT wins FROM guilds WHERE id=201",0)==1,"capture recovery preserves one durable legacy win-stat update");
        GuildHit(a,pole,full);GuildHit(ally,pole,full);Check(Scalar("SELECT count(*) FROM revora_guild_captures",0)==1,"holding guild and its allies cannot farm their own pole");
        Check(!Revora.Game.Attacking.Handle.CanAttack(a.Entity,pole,null),"native combat gate rejects the current owning guild");
        Query("CREATE FUNCTION suppress_guild_stats() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.id=202 THEN RETURN NULL; END IF; RETURN NEW; END $$;CREATE TRIGGER suppress_guild_stats BEFORE UPDATE ON guilds FOR EACH ROW EXECUTE FUNCTION suppress_guild_stats()");
        try{GuildHit(b,pole,full);}finally{Query("DROP TRIGGER suppress_guild_stats ON guilds;DROP FUNCTION suppress_guild_stats()");}
        Check(GuildWar.PoleKeeper==a.Guild && pole.Hitpoints==full && Scalar("SELECT count(*) FROM revora_guild_captures",0)==1
            && Scalar("SELECT wins FROM guilds WHERE id=201",0)==1 && Scalar("SELECT wins FROM guilds WHERE id=202",0)==0,
            "failed winner statistics roll back the preceding owner update and every capture receipt");
        Parallel.For(0,10,i=>GuildHit(b,pole,full));
        Check(GuildWar.PoleKeeper==b.Guild && Scalar("SELECT count(*) FROM revora_guild_captures",0)==2,"concurrent finishing blows capture once before same-guild attacks close");
        Check(a.Guild.Wins==0 && b.Guild.Wins==1 && Scalar("SELECT wins FROM guilds WHERE id=202",0)==1,"recapture preserves the existing winner/previous-owner statistics without duplicate callbacks");
        var gate=GuildWar.LeftGate;ally.Entity.X=(ushort)(gate.X+1);ally.Entity.Y=gate.Y;GuildHit(ally,gate,uint.MaxValue);
        Check(Scalar("SELECT count(*) FROM revora_guild_pole_hits WHERE uid=@uid",ally.Entity.UID)==0,"positive gate damage does not stand in for hitting the guild pillar");
        Check(gate.Hitpoints==0 && gate.Mesh/10==25,"real gate destruction opens its existing native mesh");
        a.Entity.X=(ushort)(pole.X+1);a.Entity.Y=pole.Y;
        var late=GuildFighter(map,201);guildNow=GuildLocal(9,20,16);GuildHit(late,pole,1);
        Check(Scalar("SELECT count(*) FROM revora_guild_pole_hits WHERE uid=@uid",late.Entity.UID)==0,"a new fighter cannot earn a first hit at the exact battle deadline");
        guildNow=GuildLocal(9,20,16);GuildHit(a,pole,full);Check(GuildWar.PoleKeeper==b.Guild,"hit at the exact deadline cannot steal seasonal ownership");
        // Keep the legacy online purse loop out of this ledger fixture; it is separately pending migration.
        a.Guild=null;b.Guild=null;ally.Guild=null;
        Query("CREATE FUNCTION suppress_guild_finish() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.state='won' THEN RETURN NULL; END IF; RETURN NEW; END $$;CREATE TRIGGER suppress_guild_finish BEFORE UPDATE ON revora_guild_battles FOR EACH ROW EXECUTE FUNCTION suppress_guild_finish()");
        try{GuildWar.End();}finally{Query("DROP TRIGGER suppress_guild_finish ON revora_guild_battles;DROP FUNCTION suppress_guild_finish()");}
        Check(GuildWar.IsWar && Scalar("SELECT count(*) FROM revora_guild_battles WHERE state='won'",0)==0,"failed final result remains retryable without publishing a winner");
        Query("UPDATE revora_guild_battles SET state='won' WHERE day='2026-09-20'");
        Parallel.For(0,8,i=>GuildWar.End());
        Check(!GuildWar.IsWar && Scalar("SELECT keeper FROM revora_guild_battles WHERE day='2026-09-20' AND state='won'",0)==202,"deadline recovers a committed final result with missing runtime acknowledgment exactly once");
        GuildRestart();guildNow=GuildLocal(9,20,15,30);GuildWar.Start();Check(!GuildWar.IsWar
            && Scalar("SELECT keeper FROM revora_guild_battles WHERE day='2026-09-20' AND state='won'",0)==202,
            "restart retains the saved winner and clock rollback cannot replay a completed weekly battle");
        guildNow=GuildLocal(9,27,15);GuildWar.Start();Check(GuildWar.IsWar && GuildWar.PoleKeeper==null,"next week starts with no inherited owner");
        GuildRestart();GuildWar.Start();Check(!GuildWar.IsWar && Scalar("SELECT count(*) FROM revora_guild_battles WHERE state='cancelled'",0)==1,"world restart cancels an interrupted battle without fabricating its winner or replaying it");
        guildNow=GuildLocal(10,4,15);GuildWar.Start();guildNow=GuildLocal(10,4,16);GuildWar.End();
        Check(Scalar("SELECT count(*) FROM revora_guild_battles WHERE day='2026-10-04' AND state='empty' AND keeper=0",0)==1,"uncontested empty battle produces no inherited victory");
        guildNow=GuildLocal(10,11,15);GuildWar.Start();
        var third=GuildFighter(map,203);var fourth=GuildFighter(map,204);
        GuildHit(third,pole,full/2);GuildHit(fourth,pole,full/2);
        Check(GuildWar.PoleKeeper==third.Guild,"equal accepted damage uses stable guild ID ordering instead of callback order");
        Kernel.Guilds.Remove(third.Guild.ID);third.Guild=null;fourth.Guild=null;guildNow=guildNow.AddHours(1);GuildWar.End();
        Check(Scalar("SELECT count(*) FROM revora_guild_battles WHERE day='2026-10-11' AND state='empty' AND keeper=0",0)==1,"dissolved pole-holding guild cannot become an eligible season winner");
        foreach(var date in new[]{GuildLocal(10,18,15),GuildLocal(10,25,15),GuildLocal(11,1,15)}) {
            guildNow=date;GuildWar.Start();
            if(date==GuildLocal(10,18,15)){var winner=GuildFighter(map,205);GuildHit(winner,pole,full);winner.Guild=null;}
            guildNow=date.AddHours(1);GuildWar.End();
        }
        Check(Scalar("SELECT keeper FROM revora_guild_battles WHERE day='2026-10-18' AND state='won'",0)==205,"ordinary capture followed by the deadline commits a real winning result");
        Check(Scalar("SELECT count(*) FROM revora_guild_battles WHERE season='2026-09-20'",0)==5
            && Scalar("SELECT round FROM revora_guild_battles WHERE day='2026-10-25'",0)==1,"five scheduled Sundays make one season and the sixth opens the next");
        using(var connection=DataHolder.MySqlConnection){connection.Open();using(var q=new NpgsqlCommand("SELECT starts_at,ends_at FROM revora_guild_battles WHERE day='2026-11-01'",connection))
        using(var r=q.ExecuteReader()){Check(r.Read() && r.GetFieldValue<DateTimeOffset>(0).UtcDateTime==GuildLocal(11,1,15)
            && r.GetFieldValue<DateTimeOffset>(1).UtcDateTime==GuildLocal(11,1,16),"fall daylight-saving transition preserves the local guild hour and absolute UTC deadlines");}}
        guildNow=GuildLocal(11,4,20);GuildWar.Start();Check(!GuildWar.IsWar,"Wednesday's separate war cannot mint a Sunday season round");
        VerifyGuildClaims();
        VerifyGuildCupStats();
        VerifyHolyGrail();
        VerifyGuildPurses();
        VerifyCharacterActivity();
        VerifyGuildDonations();
        VerifyGuildBeastWorld();
        VerifyGuildBombs();
        VerifySuperGuildLifecycle(runtime);VerifyEliteGuildCombat(runtime);
        GuildSeason.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow));
    }
}
