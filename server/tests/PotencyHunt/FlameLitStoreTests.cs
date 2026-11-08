using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type FlameStore=typeof(Entity).Assembly.GetType("Revora.Game.Features.FlameLitStore");
    static readonly Type FlameRules=typeof(Entity).Assembly.GetType("Revora.Game.Features.FlameLitRules");
    static object FlameAccess(){return Call(GuildSeason,null,"AcquireFlame");}
    static void FlameLegacyLife(GameState p) {
        p.Entity.Level=139;p.Entity.Reborn=2;p.Entity.SecondRebornLevel=139;p.Entity.Experience=5000000000UL;
        EntityTable.SaveEntity(p);ReincarnationTable.NewReincarnated(p.Entity);
        Kernel.ReincarnatedCharacters[p.Entity.UID]=new Revora.Game.Features.Reincarnation.ReincarnateInfo{UID=p.Entity.UID,Level=139,Experience=5000000000UL};
        p.Entity.Level=109;p.Entity.Experience=DataHolder.LevelExperience(109)-1;p.Entity.Atributes=30;EntityTable.SaveEntity(p);
    }
    static void FlameGateApproach(GameState p) {
        var gate=GuildWar.LeftGate;
        for(int radius=3;radius<=17;radius++)for(int dx=-radius;dx<=radius;dx++)for(int dy=1;dy<=radius;dy++) {
            if(Math.Max(Math.Abs(dx),dy)!=radius)continue;
            ushort x=(ushort)(gate.X+dx),y=(ushort)(gate.Y+dy);
            if(!p.Map.Floor[x,y,MapObjectType.Player])continue;
            try{AdventureWalk(p,x,y,true);return;}
            catch(Exception ex){if(!ex.Message.StartsWith("No ordinary walking path:"))throw;}
        }
        throw new Exception("No exterior approach within normal Guild gate attack range.");
    }
    static void FlameGateIsolation() {
        var left=GuildWar.LeftGate;var right=GuildWar.RightGate;
        var superLeft=SuperGuildWar.LeftGate;var superRight=SuperGuildWar.RightGate;
        try {
            GuildWar.LeftGate=new SobNpcSpawn{X=160,Y=210,Mesh=251};GuildWar.RightGate=new SobNpcSpawn{X=223,Y=180,Mesh=281};
            SuperGuildWar.LeftGate=new SobNpcSpawn{X=160,Y=210,Mesh=241};SuperGuildWar.RightGate=new SobNpcSpawn{X=223,Y=180,Mesh=271};
            Func<ushort,ushort,ushort,ushort,ushort,bool> blocked=(map,ox,oy,x,y)=>(bool)Call(typeof(Calculations),null,"GuildGateBlocks",map,ox,oy,x,y);
            Check(!blocked(1038,224,180,222,180) && !blocked(1038,160,211,160,209),"destroyed Guild War gates ignore closed Super Guild War gates");
            Check(blocked(1234,224,180,222,180) && blocked(1234,160,211,160,209),"Super Guild War retains its own closed gate boundaries");
            GuildWar.LeftGate.Mesh=241;GuildWar.RightGate.Mesh=271;SuperGuildWar.LeftGate.Mesh=251;SuperGuildWar.RightGate.Mesh=281;
            Check(blocked(1038,224,180,222,180) && blocked(1038,160,211,160,209),"regular closed gates still reject inward crossings");
            Check(!blocked(1234,224,180,222,180) && !blocked(1234,160,211,160,209),"destroyed Super Guild War gates ignore regular closed gates");
            Check(!blocked(1038,222,180,224,180) && !blocked(1038,160,209,160,211),"closed gates preserve outward movement policy");
            Check(!blocked(1002,224,180,222,180),"Guild gate boundaries do not apply to Twin City");
            SuperGuildWar.LeftGate=null;SuperGuildWar.RightGate=null;GuildWar.LeftGate.Mesh=251;GuildWar.RightGate.Mesh=281;
            Check(!blocked(1038,224,180,222,180),"uninitialized other battlefield does not crash regular Guild movement");
            GuildWar.LeftGate=null;Check(blocked(1038,224,180,222,180),"missing own battlefield gates fail closed");
        } finally {GuildWar.LeftGate=left;GuildWar.RightGate=right;SuperGuildWar.LeftGate=superLeft;SuperGuildWar.RightGate=superRight;}
    }
    static object FlameClaim(GameState p,uint rune) {
        var mentor=typeof(Entity).Assembly.GetType("Revora.Game.Features.MentorExperience");
        lock(p.ItemSyncRoot)lock(mentor.GetField("Sync",Hidden).GetValue(null)) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                var reward=Call(FlameStore,null,"Claim",c,t,p,rune);t.Commit();
                Call(FlameStore,null,"ApplyReward",p,reward);return reward;
            }}
        }
    }
    static object FlameWrite(GameState p,string method,params object[] args) {
        lock(p.ItemSyncRoot)using(var access=(IDisposable)FlameAccess()) {
            if(access==null)return null;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                var change=Call(FlameStore,null,method,new object[]{c,t,p,access}.Concat(args).ToArray());t.Commit();
                if(change!=null){uint removed=(uint)AbyssField(change,"Removed");var item=(ConquerItem)AbyssField(change,"Added");
                    Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.QuestInventory"),null,"Install",p,removed==0?new uint[0]:new[]{removed},new[]{item});}
                return change;
            }}
        }
    }
    static void FlameAt(GameState p,int stage) {
        ushort[] maps={1002,1038,1038,1038,1038,1038,1038,1038,1002,1038};
        ushort[] xs={350,317,236,194,115,316,136,38,350,62},ys={327,270,291,168,53,378,182,94,321,59};
        p.BindWorldMap(stage<0?(ushort)1002:maps[stage]);p.Entity.X=stage<0?(ushort)353:xs[stage];p.Entity.Y=stage<0?(ushort)323:(ushort)(ys[stage]-2);
        p.ActiveNpc=stage<0?4452u:(uint)(4453+stage);if(!EntityTable.SaveEntity(p))throw new Exception("Flame fixture location save failed.");
    }
    static void VerifyFlameStore(string runtime,Map town) {
        FlameGateIsolation();InitializeHousingFixture(runtime,town);DataHolder.ReadStats();var field=World(runtime,1038);Server.GamePool=new GameState[0];
        Query(@"CREATE TABLE apprentice (LIKE public.apprentice INCLUDING ALL);
CREATE TABLE reincarnation(id bigserial PRIMARY KEY,uid bigint NOT NULL,level bigint NOT NULL,experience bigint NOT NULL);");
        Query("CREATE TABLE guilds(id bigint PRIMARY KEY,wins bigint NOT NULL,losts bigint NOT NULL);INSERT INTO sobnpcs SELECT * FROM public.sobnpcs WHERE mapid=1038");
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new NpgsqlCommand("SELECT id,cellx,celly,lookface,type,life,maxlife,name FROM sobnpcs WHERE id IN(810,516074,516075) ORDER BY id",c))
        using(var r=q.ExecuteReader())while(r.Read())field.AddNpc(new SobNpcSpawn{UID=Convert.ToUInt32(r[0]),MapID=1038,X=Convert.ToUInt16(r[1]),Y=Convert.ToUInt16(r[2]),Mesh=Convert.ToUInt16(r[3]),
            Type=(Enums.NpcType)Convert.ToByte(r[4]),Hitpoints=Convert.ToUInt32(r[5]),MaxHitpoints=Convert.ToUInt32(r[6]),Name=Convert.ToString(r[7])});}
        guildNow=GuildLocal(9,20,15);GuildSeason.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>guildNow));Call(GuildSeason,null,"Initialize");
        GuildWar.IsWar=true;Check(FlameAccess()==null,"calendar and loose war flag cannot create Flame authority");GuildWar.IsWar=false;
        GuildWar.Start();Call(FlameStore,null,"Initialize");Call(FlameStore,null,"Initialize");
        using(var access=(IDisposable)FlameAccess())Check(access!=null,"real scheduled Guild War supplies bounded Flame authority");
        var original=GuildWar.Pole;GuildWar.Pole=new SobNpcSpawn{UID=original.UID,MapID=1038,MaxHitpoints=original.MaxHitpoints};
        Check(FlameAccess()==null,"replaced battlefield structure invalidates Flame authority");GuildWar.Pole=original;
        using(var ready=new ManualResetEventSlim())using(var release=new ManualResetEventSlim()){
            var held=Task.Run(()=>{using(var access=(IDisposable)FlameAccess()){ready.Set();release.Wait();}});ready.Wait();
            try{Check(FlameAccess()==null,"busy battlefield defers Flame work without blocking");}finally{release.Set();held.Wait();}
        }
        town.AddNpc(new NpcSpawn{UID=4452,MapID=1002,X=353,Y=325,Mesh=6810,Type=Enums.NpcType.Talker});
        ushort[] maps={1002,1038,1038,1038,1038,1038,1038,1038,1002,1038},xs={350,317,236,194,115,316,136,38,350,62},ys={327,270,291,168,53,378,182,94,321,59};
        for(int i=0;i<10;i++)Kernel.Maps[maps[i]].AddNpc(new NpcSpawn{UID=(uint)(4453+i),MapID=maps[i],X=xs[i],Y=ys[i],Mesh=(ushort)(6570+i*10),Type=Enums.NpcType.Talker});
        var quest=typeof(Entity).Assembly.GetType("Revora.Game.Features.FlameLitQuest");Call(quest,null,"Initialize");Call(quest,null,"Initialize");
        Check(town.Npcs[4452].X==353 && town.Npcs[4453].Y==327 && town.Npcs[4461].Y==321 && field.Npcs[4462].X==62,
            "quest initialization restores intended flame actors idempotently");
        var p=HousePlayer(town);uint wardrobe=StoredQuestGarment(p);FlameAt(p,-1);Check(p.Account.State==AccountTable.AccountState.Player,"Flame store accepts ordinary non-GM owner");
        var disposed=(IDisposable)FlameAccess();disposed.Dispose();
        Check(AbyssRejected(()=>{lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction())Call(FlameStore,null,"Begin",c,t,p,disposed);}}),
            "disposed battlefield access cannot authorize a later transaction");
        Query("UPDATE revora_guild_battles SET state='cancelled' WHERE day='2026-09-20'");
        Check(AbyssRejected(()=>FlameWrite(p,"Begin")),"runtime flag cannot override a cancelled durable battlefield");
        Query("UPDATE revora_guild_battles SET state='active' WHERE day='2026-09-20'");
        var full=HousePlayer(town);FlameAt(full,-1);for(int i=0;i<40;i++)Pack(full,1088001,1);
        Check(AbyssRejected(()=>FlameWrite(full,"Begin")) && Scalar("SELECT count(*) FROM revora_flame_runs WHERE uid=@uid",full.Entity.UID)==0,"full inventory cannot leave a partial weekly entry");
        p.Entity.X=370;Check(FlameWrite(p,"Begin")==null,"distant Taoist cannot issue rune");p.Entity.X=353;
        p.Trade.InTrade=true;Check(AbyssRejected(()=>FlameWrite(p,"Begin")),"trade excludes Flame admission");p.Trade.InTrade=false;
        Parallel.For(0,8,i=>FlameWrite(p,"Begin"));var rune=p.Inventory.Objects.Single();uint first=rune.UID;
        Check(rune.ID==729960 && rune.Bound && Scalar("SELECT count(*) FROM revora_flame_runs WHERE uid=@uid",p.Entity.UID)==1,"concurrent admission creates one owned Jade Rune and weekly run");
        FlameAt(p,1);Check(FlameWrite(p,"Advance",first)==null,"second stone cannot skip first rune transition");FlameAt(p,0);
        Query("UPDATE items SET locked=1 WHERE uid=@uid","uid",(long)first);
        Check(AbyssRejected(()=>FlameWrite(p,"Advance",first)) && p.Inventory.ContainsUID(first),"stale saved rune restrictions cannot be bypassed by cached inventory");
        Query("UPDATE items SET locked=0 WHERE uid=@uid","uid",(long)first);
        var stranger=HousePlayer(town);FlameAt(stranger,0);Check(FlameWrite(stranger,"Advance",first)==null,"foreign character cannot advance another run");
        Query("CREATE FUNCTION suppress_flame_step() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF NEW.stage=1 THEN RETURN NULL; END IF; RETURN NEW; END';CREATE TRIGGER suppress_flame_step BEFORE INSERT ON revora_flame_steps FOR EACH ROW EXECUTE FUNCTION suppress_flame_step()");
        try{Check(AbyssRejected(()=>FlameWrite(p,"Advance",first)),"failed step receipt aborts replacement");}
        finally{Query("DROP TRIGGER suppress_flame_step ON revora_flame_steps;DROP FUNCTION suppress_flame_step()");}
        Check(p.Inventory.ContainsUID(first) && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0",p.Entity.UID)==1
            && Scalar("SELECT stage FROM revora_flame_runs WHERE uid=@uid",p.Entity.UID)==0,"failed replacement restores old rune and progress");
        var filler=new System.Collections.Generic.List<uint>();while(p.Inventory.Count<40)filler.Add(Pack(p,1088001,1).UID);
        Parallel.For(0,8,i=>FlameWrite(p,"Advance",first));
        Check(p.Inventory.Count==40,"rune replacement works atomically in an otherwise full bag");
        foreach(uint item in filler)RemoveFixtureItem(p,item);rune=p.Inventory.Objects.Single();
        Check(rune.ID==729961 && !p.Inventory.ContainsUID(first) && Scalar("SELECT stage FROM revora_flame_runs WHERE uid=@uid",p.Entity.UID)==1,"concurrent first lighting replaces the exact rune once");
        p=ReloadBall(p);FlameAt(p,1);uint lost=rune.UID;rune=p.Inventory.Objects.Single();
        Check(rune.UID==lost && rune.ID==729961,"production reload retains exact current rune");
        // Model a committed drop/loss, not a claim or an earned replacement.
        RemoveFixtureItem(p,lost);FlameAt(p,-1);var restarted=FlameWrite(p,"Begin");
        Check((int)AbyssField(restarted,"Generation")==2 && p.Inventory.Objects.Single().ID==729960,"lost rune restarts same weekly run at first stone with new generation");
        FlameAt(p,1);Check(FlameWrite(p,"Advance",lost)==null,"lost-generation rune UID cannot advance replacement attempt");
        for(int stage=0;stage<9;stage++) {FlameAt(p,stage);rune=p.Inventory.Objects.Single();Check(FlameWrite(p,"Advance",rune.UID)!=null,"owned ordered lighting persists stage "+(stage+1));}
        FlameAt(p,9);rune=p.Inventory.Objects.Single();Check(rune.ID==729969 && FlameWrite(p,"Advance",rune.UID)==null,"tenth flame stays closed before last half-hour");
        guildNow=GuildLocal(9,20,15,30);uint last=rune.UID;
        Query("CREATE FUNCTION suppress_flame_achievement() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF NEW.achievement_id=11016 THEN RETURN NULL; END IF; RETURN NEW; END';CREATE TRIGGER suppress_flame_achievement BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW EXECUTE FUNCTION suppress_flame_achievement()");
        try{Check(AbyssRejected(()=>FlameWrite(p,"Advance",last)),"failed Dragon Rune achievement rolls back final transition");}
        finally{Query("DROP TRIGGER suppress_flame_achievement ON revora_achievement_evidence;DROP FUNCTION suppress_flame_achievement()");}
        Check(p.Inventory.ContainsUID(last) && Scalar("SELECT stage FROM revora_flame_runs WHERE uid=@uid",p.Entity.UID)==9,
            "achievement failure retains ninth rune and stage for retry");
        Check(FlameWrite(p,"Advance",last)!=null && p.Inventory.Objects.Single().ID==729970,"tenth flame opens exactly at final half-hour and yields Dragon Rune");
        Check(FlameWrite(p,"Advance",last)==null && FlameWrite(p,"Advance",p.Inventory.Objects.Single().UID)==null,"old final rune and Dragon Rune replays cannot create another transition");
        p=ReloadBall(p);Check(p.Inventory.Objects.Single().ID==729970 && Scalar("SELECT stage FROM revora_flame_runs WHERE uid=@uid",p.Entity.UID)==10,"Dragon Rune and completed progression survive reload");
        var partial=HousePlayer(town);FlameAt(partial,-1);FlameWrite(partial,"Begin");FlameAt(partial,0);
        FlameWrite(partial,"Advance",partial.Inventory.Objects.Single().UID);uint partialRune=partial.Inventory.Objects.Single().UID;
        partial.Entity.Level=69;partial.Entity.Experience=DataHolder.LevelExperience(69)-1;FlameAt(partial,-1);partial=ReloadBall(partial);FlameAt(partial,-1);
        var partialExpected=HousePlayer(town);partialExpected.Entity.Level=69;partialExpected.Entity.Experience=partial.Entity.Experience;
        EntityTable.SaveEntity(partialExpected);partialExpected=ReloadBall(partialExpected);partialExpected.IncreaseExperience(partialExpected.ExpBall,false);
        var unlit=HousePlayer(town);FlameAt(unlit,-1);FlameWrite(unlit,"Begin");uint jade=unlit.Inventory.Objects.Single().UID;
        var capped=HousePlayer(town);FlameAt(capped,-1);FlameWrite(capped,"Begin");FlameAt(capped,0);FlameWrite(capped,"Advance",capped.Inventory.Objects.Single().UID);
        uint cappedRune=capped.Inventory.Objects.Single().UID;capped.Entity.Level=Config.LevelCap;capped.Entity.Experience=123;FlameAt(capped,-1);capped=ReloadBall(capped);FlameAt(capped,-1);
        uint dragon=p.Inventory.Objects.Single().UID;p.Entity.Level=69;p.Entity.Experience=DataHolder.LevelExperience(69)-1;FlameAt(p,-1);
        var mentor=HousePlayer(town);MentorLink(mentor,p,100);
        var recovery=HousePlayer(town);FlameAt(recovery,-1);FlameWrite(recovery,"Begin");
        for(int stage=0;stage<9;stage++){FlameAt(recovery,stage);FlameWrite(recovery,"Advance",recovery.Inventory.Objects.Single().UID);}
        uint recoveryRune=recovery.Inventory.Objects.Single().UID;FlameLegacyLife(recovery);FlameAt(recovery,-1);
        var recoveryExpected=HousePlayer(town);FlameLegacyLife(recoveryExpected);
        var flow=HousePlayer(town);FlameAt(flow,-1);AncientDialog(flow,4452,1);
        Check(flow.Inventory.Count==0,"unsolicited Taoist option cannot issue a rune");
        AncientDialog(flow,4452,0);AncientDialog(flow,4452,1);
        Check(flow.Inventory.Objects.Single().ID==729960,"ordinary sealed NPC dialogue issues owned Jade Rune");
        var stale=flow.Inventory.Objects.Single();ItemHandler.UseItem(stale,flow);
        // Taoist is within stone-one range, so right-click legitimately lights it.
        Check(flow.Inventory.Objects.Single().ID==729961,"owned rune right-click lights nearby first stone");
        ItemHandler.UseItem(stale,flow);Check(flow.Inventory.Objects.Single().ID==729961,"stale consumed item reference cannot advance again");
        MiningNpc(town,380);AdventureNear(flow,380,true);AncientDialog(flow,380,0);AncientDialog(flow,380,6);
        Check(flow.Entity.MapID==1038 && !Fenced(flow),"ordinary Guild Controller dialogue enters the Flame hunting map");
        var routeGuild=new Revora.Game.ConquerStructures.Society.Guild(flow.Entity.Name){ID=9876,Name="FlameRouteGuild"};
        Kernel.Guilds[routeGuild.ID]=routeGuild;Query("INSERT INTO guilds(id,wins,losts) VALUES(9876,0,0)");flow.Guild=routeGuild;flow.Entity.CharBuffer._GuildID=routeGuild.ID;
        flow.AsMember=new Revora.Game.ConquerStructures.Society.Guild.Member(routeGuild.ID){ID=flow.Entity.UID,Rank=Enums.GuildMemberRank.Member};routeGuild.Members[flow.Entity.UID]=flow.AsMember;
        for(int stage=1;stage<10;stage++) {
            if(stage==3){FlameGateApproach(flow);
                GuildHit(flow,GuildWar.LeftGate,GuildWar.LeftGate.Hitpoints);
                Check(GuildWar.LeftGate.Hitpoints==0 && GuildWar.LeftGate.Mesh/10==25,"normal guild-member attack destroys gate before entering inner flame route");}
            if(stage==8){ItemHandler.UseItem(Pack(flow,1060020,1),flow);Check(flow.Entity.MapID==1002,"normal Twin City scroll returns from Guild map for ninth stone");}
            if(stage==9){AdventureNear(flow,380,true);AncientDialog(flow,380,0);AncientDialog(flow,380,6);}
            AdventureNear(flow,(uint)(4453+stage),true);EntityTable.SaveEntity(flow);
            if(stage%2==0){AncientDialog(flow,(uint)(4453+stage),0);AncientDialog(flow,(uint)(4453+stage),1);}
            else ItemHandler.UseItem(flow.Inventory.Objects.Single(),flow);
        }
        Check(flow.Inventory.Objects.Single().ID==729970 && HasAchievement(flow,11016),"ordinary NPC and right-click route earns Dragon Rune and immediate achievement");
        Query("UPDATE revora_flame_runs SET valuable_reward=false WHERE uid=@uid","uid",(long)p.Entity.UID);
        Check(FlameClaim(p,dragon)==null && p.Inventory.ContainsUID(dragon),"Dragon Rune cannot be claimed before scheduled war end");
        guildNow=GuildLocal(9,20,16);Check(FlameAccess()==null,"exact Guild War end revokes progression authority");
        new TitleStorage().Login(p);Check(HasAchievement(p,11016),"owned Dragon Rune achievement survives production title reload");
        ulong before=p.Entity.Experience;byte level=p.Entity.Level;
        Query("CREATE FUNCTION suppress_flame_claim() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_flame_claim BEFORE INSERT ON revora_flame_claims FOR EACH ROW EXECUTE FUNCTION suppress_flame_claim()");
        try{Check(AbyssRejected(()=>FlameClaim(p,dragon)),"failed claim receipt rolls back reward");
            Check(AbyssRejected(()=>FlameClaim(recovery,recoveryRune)),"failed claim receipt rolls back legacy reincarnation recovery");}
        finally{Query("DROP TRIGGER suppress_flame_claim ON revora_flame_claims;DROP FUNCTION suppress_flame_claim()");}
        Check(p.Inventory.ContainsUID(dragon) && p.Entity.Experience==before && Scalar("SELECT experience FROM entities WHERE uid=@uid",p.Entity.UID)==(long)before
            && Scalar("SELECT count(*) FROM revora_flame_runs WHERE uid=@uid AND claimed",p.Entity.UID)==0,"claim failure preserves rune, EXP and weekly entitlement");
        Check(MentorBalance(p)==100 && mentor.PrizeExperience==100 && p.AsApprentice.Actual_Experience==100,
            "failed Flame claim preserves durable and visible mentor credit");
        Check(PendingRows(recovery)==1 && Kernel.ReincarnatedCharacters.ContainsKey(recovery.Entity.UID) && recovery.Entity.Level==109
            && recovery.Inventory.ContainsUID(recoveryRune),"failed Flame recovery preserves pending life and owned rune");
        var expected=HousePlayer(town);expected.Entity.Level=level;expected.Entity.Experience=before;EntityTable.SaveEntity(expected);expected=ReloadBall(expected);
        for(int i=0;i<12;i++)expected.IncreaseExperience(expected.ExpBall,false);
        Parallel.For(0,8,i=>FlameClaim(p,dragon));
        Check(!p.Inventory.ContainsUID(dragon) && Scalar("SELECT count(*) FROM revora_flame_claims c JOIN revora_flame_runs f ON f.id=c.run WHERE f.uid=@uid",p.Entity.UID)==1,
            "concurrent claims consume one Dragon Rune and record one reward");
        Check(p.Entity.Level==expected.Entity.Level && p.Entity.Level>70 && p.Entity.Experience==expected.Entity.Experience
            && p.Entity.Strength==expected.Entity.Strength && p.Entity.Vitality==expected.Entity.Vitality,
            "atomic final reward matches twelve legacy sequential EXP units across multiple levels");
        long mentorUnits=100;for(int earned=level+1;earned<=p.Entity.Level;earned++)mentorUnits+=earned*2;
        Check(MentorBalance(p)==mentorUnits && mentor.PrizeExperience==(ulong)mentorUnits && p.AsApprentice.Actual_Experience==(ulong)mentorUnits,
            "concurrent Flame claims grant one mentor credit for each earned level");
        Revora.Database.KnownPersons.LoadMentor(mentor);Check(mentor.PrizeExperience==(ulong)mentorUnits,"Flame mentor credit survives production relationship reload");
        for(int unit=0;unit<9;unit++)recoveryExpected.IncreaseExperience(recoveryExpected.ExpBall,false);
        Parallel.For(0,8,i=>FlameClaim(recovery,recoveryRune));
        Check(recovery.Entity.Level==recoveryExpected.Entity.Level && recovery.Entity.Experience==recoveryExpected.Entity.Experience
            && recovery.Entity.Atributes==recoveryExpected.Entity.Atributes && PendingRows(recovery)==0 && !Kernel.ReincarnatedCharacters.ContainsKey(recovery.Entity.UID),
            "Flame partial reward restores legacy life once and matches sequential EXP after recovery");
        recovery=ReloadBall(recovery);FlameAt(recovery,-1);
        Check(recovery.Entity.Level==recoveryExpected.Entity.Level && recovery.Entity.Experience==recoveryExpected.Entity.Experience
            && !recovery.Inventory.ContainsUID(recoveryRune) && FlameClaim(recovery,recoveryRune)==null,"recovered Flame reward survives reconnect without replay");
        p=ReloadBall(p);FlameAt(p,-1);
        Check(p.Entity.Level==expected.Entity.Level && p.Entity.Experience==expected.Entity.Experience && !p.Inventory.ContainsUID(dragon)
            && FlameClaim(p,dragon)==null,"claimed EXP and consumed rune survive production reload without replay");
        Check(Scalar("SELECT count(*) FROM items WHERE uid=@uid AND position=0 AND warehouse=0 AND inwardrobe=1",wardrobe)==1,"Flame lighting, rune replacement, reward and production reload preserve stored wardrobe design");
        Check(!p.Inventory.Objects.Any(i=>i.ID>=800000 && i.ID<900000),"legacy full Flame contract retains original reward without retroactive P7");
        Check(FlameClaim(unlit,jade)==null && unlit.Inventory.ContainsUID(jade),"unlit Jade Rune is not an earned EXP claim");
        var cappedReward=FlameClaim(capped,cappedRune);
        Check(cappedReward!=null && (ulong)AbyssField(cappedReward,"Experience")==0 && capped.Entity.Level==Config.LevelCap
            && capped.Entity.Experience==123 && !capped.Inventory.ContainsUID(cappedRune),"level-cap claim records completion without inventing overflow experience");
        guildNow=GuildLocal(9,27,16);Query("UPDATE revora_guild_battles SET state='cancelled' WHERE day='2026-09-20'");
        Query("UPDATE items SET locked=1 WHERE uid=@uid","uid",(long)partialRune);
        Check(AbyssRejected(()=>FlameClaim(partial,partialRune)) && partial.Inventory.ContainsUID(partialRune),"claim rechecks saved rune restrictions before awarding EXP");
        Query("UPDATE items SET locked=0 WHERE uid=@uid","uid",(long)partialRune);
        while(partial.Inventory.Count<40)Pack(partial,1088001,1);
        uint cpBefore=partial.Entity.ConquerPoints,rvpBefore=partial.Entity.RevoraPoint;ulong silverBefore=partial.Entity.Money;
        var partialReward=FlameClaim(partial,partialRune);
        Check(partialReward!=null && (int)AbyssField(partialReward,"Units")==1 && partial.Inventory.Count==39,
            "delayed partial reward survives cancelled war and needs no free bag slot");
        Check(partial.Entity.Level==partialExpected.Entity.Level && partial.Entity.Level>=70 && partial.Entity.Experience==partialExpected.Entity.Experience
            && partial.ArenaStatistic!=null && partial.ArenaStatistic.ArenaPoints==partialExpected.ArenaStatistic.ArenaPoints,
            "partial reward preserves level-up and arena admission effects");
        Check(partial.Entity.ConquerPoints==cpBefore && partial.Entity.RevoraPoint==rvpBefore && partial.Entity.Money==silverBefore,
            "Flame EXP claim neither charges nor grants unrelated currencies");
        partial=ReloadBall(partial);FlameAt(partial,-1);
        Check(partial.Entity.Level==partialExpected.Entity.Level && partial.Entity.Experience==partialExpected.Entity.Experience
            && !partial.Inventory.ContainsUID(partialRune) && FlameClaim(partial,partialRune)==null,"partial level-up reward survives reload without replay");
        ItemHandler.UseItem(Pack(flow,1060020,1),flow);AdventureNear(flow,4452,true);EntityTable.SaveEntity(flow);
        ulong flowBefore=flow.Entity.Experience;AncientDialog(flow,4452,0);AncientDialog(flow,4452,2);
        Check(flow.Inventory.Count==1 && flow.Inventory.Objects.All(i=>i.Bound && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7) && flow.Entity.Experience>flowBefore,"ordinary sealed Taoist claim delivers earned reward");
        ulong flowAfter=flow.Entity.Experience;AncientDialog(flow,4452,2);Check(flow.Entity.Experience==flowAfter,"replayed Taoist option does not duplicate EXP");
        flow=ReloadBall(flow);new TitleStorage().Login(flow);
        Check(flow.Inventory.Count==1 && flow.Entity.Experience==flowAfter && HasAchievement(flow,11016),"full dialogue route reward and achievement survive reconnect");
    }
}
