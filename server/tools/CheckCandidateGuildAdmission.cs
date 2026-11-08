using System;
using System.Linq;
using Npgsql;
using Revora;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static void GuildPkCoordinatorProcess(bool reload) {
        GuildPkInterruptedReturnCheck();
        var parent=Feature("GuildContestCoordinator");var coordinator=Feature("GuildPkCoordinator");
        lock(parent.GetField("Gate",Hidden).GetValue(null))lock(coordinator.GetField("Gate",Hidden).GetValue(null)) {
            Guid qualifier=new Guid("f6067100-0000-0000-0000-000000000053");
            DateTime local=new DateTime(2086,3,11,20,30,0,DateTimeKind.Local);while(local.DayOfWeek!=DayOfWeek.Monday)local=local.AddDays(1);
            DateTime start=local.ToUniversalTime(),now=start;parent.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));Require((bool)parent.GetField("prepared",Hidden).GetValue(null) && (bool)parent.GetField("enabled",Hidden).GetValue(null));Require(Kernel.Maps[1002].Npcs.ContainsKey(706070) && Kernel.Maps[1002].Npcs.ContainsKey(706072));
            nextUid=4294916801;var p=Player(Kernel.Maps[1002],true);nextUid=4294916901;var offline=Player(Kernel.Maps[1002],true);var peer=Player(Kernel.Maps[1002],true);var leaver=Player(Kernel.Maps[1002],true);
            foreach(var who in new[]{p,offline,peer,leaver}) {SubClassTable.Load(who.Entity);ChiTable.Load(who);WayofHeroes.Load(who);Revora.Database.KnownPersons.LoadPartner(who);Revora.Database.KnownPersons.LoaderFriends(who);Revora.Database.KnownPersons.LoadMentor(who);Revora.Database.KnownPersons.LoadEnemy(who);who.LoadItemStats();Require(!who.Fake && who.Account.State==AccountTable.AccountState.Player);}
            uint rival=Convert.ToUInt32(Sql("SELECT guildid FROM entities WHERE uid=4294912301"));uint middle=Kernel.Guilds.Keys.First(g=>g!=p.Guild.ID && g!=rival);
            if(leaver.Guild.ID!=rival) {
                Require(!reload);nextUid=Kernel.Guilds[rival].Leader.ID;var rivalLeader=Player(Kernel.Maps[1002],true);
                SubClassTable.Load(rivalLeader.Entity);ChiTable.Load(rivalLeader);WayofHeroes.Load(rivalLeader);Revora.Database.KnownPersons.LoadPartner(rivalLeader);Revora.Database.KnownPersons.LoaderFriends(rivalLeader);Revora.Database.KnownPersons.LoadMentor(rivalLeader);Revora.Database.KnownPersons.LoadEnemy(rivalLeader);rivalLeader.LoadItemStats();
                HeartTalk(rivalLeader,706072,0);HeartTalk(leaver,706072,0);
                GuildClaimPacket(leaver,GuildCommand.Quit,leaver.Guild.ID);Require(leaver.Guild==null);
                GuildClaimPacket(rivalLeader,GuildCommand.InviteRequest,leaver.Entity.UID);GuildClaimPacket(leaver,GuildCommand.JoinRequest,rivalLeader.Entity.UID);
                Require(leaver.Guild!=null && leaver.Guild.ID==rival);
            }

            var ledger=GameAssembly.GetType("Revora.Database.GuildContestTable",true);var schedule=GameAssembly.GetType("Revora.Database.GuildPkSchedule",true);
            if((long)Sql("SELECT count(*) FROM revora_guild_contests WHERE id=@run","run",qualifier)==0) {
                Require(!reload);Sql("INSERT INTO revora_guild_contests(id,starts_at,ends_at,state) VALUES(@run,@start,@end,'frozen')","run",qualifier,"start",new DateTimeOffset(start),"end",new DateTimeOffset(start.AddHours(2)));
                uint[] seeds={p.Guild.ID,middle,rival};for(int i=0;i<3;i++) {
                    Sql("INSERT INTO revora_guild_contest_seeds VALUES(@run,@rank,@guild,@points,@now)","run",qualifier,"rank",i+1,"guild",(long)seeds[i],"points",3L-i,"now",new DateTimeOffset(start));BindContestFixtureGuild(ledger,qualifier,seeds[i]);
                }
            }
            Call(schedule,"Create",qualifier);Guid match=(Guid)Sql("SELECT id FROM revora_guild_pk_matches WHERE contest=@run AND round<4","run",qualifier);
            DateTime begins=new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc).AddSeconds(Convert.ToInt64(Sql("SELECT extract(epoch from starts_at)::bigint FROM revora_guild_pk_matches WHERE id=@run","run",match)));
            if(Convert.ToString(Sql("SELECT state FROM revora_guild_pk_matches WHERE id=@run","run",match))=="scheduled") {
                Require(!reload);now=begins.AddMinutes(-6);Call(parent,"Tick");HeartTalk(p,706072,1);Require(p.Entity.MapID==1002);
                now=begins.AddMinutes(-4);Call(parent,"Tick");GuildPkFundingProbe(match,p,peer,offline,now);
                foreach(var who in new[]{p,offline,peer,leaver}){HeartTalk(who,706072,0);HeartTalk(who,706072,1);Require(who.Map.BaseID==700 && who.Entity.MapID!=1002);}
                var map=p.Map;Require(ReferenceEquals(peer.Map,map) && ReferenceEquals(offline.Map,map));
                Require(ReferenceEquals(leaver.Map,map) && leaver.Guild.ID!=p.Guild.ID);
                Require(Kernel.GetDistance(p.Entity.X,p.Entity.Y,leaver.Entity.X,leaver.Entity.Y)>=20);
                var admission=Feature("GuildPkAdmission");object[] firstCamp={match,map,p.Guild.ID,(ushort)0,(ushort)0};object[] secondCamp={match,map,rival,(ushort)0,(ushort)0};
                Require((bool)admission.GetMethod("Camp",Hidden).Invoke(null,firstCamp) && (bool)admission.GetMethod("Camp",Hidden).Invoke(null,secondCamp));
                Require(p.Entity.X==(ushort)firstCamp[3] && p.Entity.Y==(ushort)firstCamp[4] && leaver.Entity.X==(ushort)secondCamp[3] && leaver.Entity.Y==(ushort)secondCamp[4]);
                object[] wrongCamp={match,map,middle,(ushort)0,(ushort)0};Require(!(bool)admission.GetMethod("Camp",Hidden).Invoke(null,wrongCamp));

                Require(!map.Npcs.ContainsKey(706074));
                foreach(var who in new[]{p,peer,leaver}){who.Attackable=true;who.Entity.PKMode=Enums.PKMode.PK;}
                GuildPkRejectDamage(p.Entity,leaver.Entity);p.Entity.X=50;p.Entity.Y=59;
                now=begins;Call(parent,"Tick");Require(Convert.ToString(Sql("SELECT state FROM revora_guild_pk_matches WHERE id=@run","run",match))=="active");
                GuildPkPlayerCombatProbe(p,peer,leaver);GuildPkPoisonProbe(match,p,peer,leaver);GuildPkRuneSupportProbe(p,peer,leaver);
                GuildPkBannerCombatProbe(match,p,leaver,begins,value=>now=value);
                GuildPkDefeatProbe(match,leaver,peer,offline,begins,value=>now=value);
                HeartTalk(leaver,706073,0);HeartTalk(leaver,706073,1);Require(leaver.Entity.MapID==1002);
                HeartTalk(leaver,706072,1);Require(leaver.Entity.MapID==1002); // One match entry only.

                Kernel.GamePool.Remove(offline.Entity.UID);
                now=begins.AddSeconds(170);
                Sql(@"CREATE OR REPLACE FUNCTION revora_test_pk_result_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;
CREATE TRIGGER revora_test_pk_result_failure BEFORE INSERT ON revora_guild_pk_results FOR EACH ROW EXECUTE FUNCTION revora_test_pk_result_failure();");
                try {
                    Call(parent,"Tick");Require(Kernel.Maps.ContainsKey(map.ID) && p.Entity.MapID==map.ID);
                    Require(Convert.ToString(Sql("SELECT state FROM revora_guild_pk_matches WHERE id=@run","run",match))=="active");
                    Require((long)Sql("SELECT count(*) FROM revora_guild_pk_results WHERE run=@run","run",match)==0);
                }finally{Sql("DROP TRIGGER IF EXISTS revora_test_pk_result_failure ON revora_guild_pk_results;DROP FUNCTION IF EXISTS revora_test_pk_result_failure()");}
                Sql(@"CREATE OR REPLACE FUNCTION revora_test_pk_share_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;
CREATE TRIGGER revora_test_pk_share_failure BEFORE INSERT ON revora_guild_pk_payouts FOR EACH ROW EXECUTE FUNCTION revora_test_pk_share_failure();");
                try{now=begins.AddSeconds(172);Call(parent,"Tick");Require((long)Sql("SELECT count(*) FROM revora_guild_pk_payouts WHERE run=@run","run",match)==0);Require(Convert.ToString(Sql("SELECT state FROM revora_guild_pk_pools WHERE run=@run","run",match))=="funded");}
                finally{Sql("DROP TRIGGER IF EXISTS revora_test_pk_share_failure ON revora_guild_pk_payouts;DROP FUNCTION IF EXISTS revora_test_pk_share_failure()");}
            }
            if(!reload && (long)Sql("SELECT count(*) FROM revora_guild_pk_payouts WHERE run=@run AND paid_at IS NULL","run",match)>0 || !reload && Convert.ToString(Sql("SELECT state FROM revora_guild_pk_pools WHERE run=@run","run",match))=="funded") {
                Kernel.GamePool.Remove(offline.Entity.UID);now=begins.AddSeconds(174);GuildPkPayoutFaultProbe(match,p,offline,now);
                var projection=coordinator.GetField("RewardProbe",Hidden);projection.SetValue(null,new Action<string,uint>((stage,uid)=>{if(stage=="paid-commit" && uid==p.Entity.UID)throw new InvalidOperationException("Fixture lost projection.");}));
                try{Call(coordinator,"RewardPulse",now);}finally{projection.SetValue(null,null);}
                Require(PirateFenced(p) && !EntityTable.SaveEntity(p));
                Require((long)Sql("SELECT count(*) FROM revora_guild_pk_payouts WHERE run=@run AND paid_at IS NOT NULL","run",match)==2);
                Require(Convert.ToDecimal(Sql("SELECT money FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))==Convert.ToDecimal(Sql("SELECT online_before+952381 FROM revora_guild_pk_pool_probe WHERE run=@run","run",match)));
                Call(coordinator,"RewardPulse",now.AddSeconds(2));
                System.Console.WriteLine("[career-process] guild-pk-paid-projection-loss-preserved");
                System.Console.WriteLine("[career-process] career-guild-pk-coordinator-earn-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Guild PK projection fixture did not close.");
            }
            Require(Convert.ToString(Sql("SELECT state FROM revora_guild_pk_matches WHERE id=@run","run",match))=="complete");
            Require((long)Sql("SELECT count(*) FROM revora_guild_pk_results WHERE run=@run AND reason='elimination' AND first_score=152 AND second_score=10","run",match)==1);
            Require((long)Sql("SELECT count(*) FROM revora_guild_pk_entries WHERE run=@run AND NOT active","run",match)==4);
            Require((long)Sql("SELECT count(*) FROM revora_guild_pk_transfers WHERE run=@run AND state<>'returned'","run",match)==0);
            Require((long)Sql("SELECT count(*) FROM revora_guild_pk_matches WHERE id=@run AND winner=first_guild AND first_score=152 AND second_score=10","run",match)==1);
            Require((long)Sql("SELECT count(*) FROM revora_guild_pk_defeats WHERE run=@run","run",match)==20);
            Require(Convert.ToInt32(Sql("SELECT deaths FROM revora_guild_pk_entries WHERE run=@run AND uid=4294916902","run",match))==20);
            var bannerLedger=GameAssembly.GetType("Revora.Database.GuildPkBannerTable",true);
            Call(bannerLedger,"Pulse",match,begins.AddHours(3)); // Complete matches cannot accrue more points.
            System.Threading.Tasks.Parallel.For(0,8,_=>Call(bannerLedger,"Settle",match,begins.AddHours(3)));
            Require((long)Sql("SELECT count(*) FROM revora_guild_pk_results WHERE run=@run","run",match)==1);
            Require((long)Sql("SELECT first_score+second_score FROM revora_guild_pk_matches WHERE id=@run","run",match)==162);
            Require((long)Sql("SELECT count(*) FROM revora_guild_pk_captures WHERE run=@run","run",match)==3);

            Call(schedule,"Advance",qualifier,begins.AddSeconds(172));
            Require((long)Sql("SELECT count(*) FROM revora_guild_pk_standings WHERE contest=@run","run",qualifier)==3);
            Require((long)Sql("SELECT count(*) FROM revora_guild_pk_matches WHERE contest=@run AND round=4","run",qualifier)==1);
            Require(Convert.ToInt32(Sql("SELECT points FROM revora_guild_pk_standings WHERE contest=@run AND guild_id=@guild","run",qualifier,"guild",(long)p.Guild.ID))==3);
            Require((long)Sql("SELECT count(*) FROM revora_guild_pk_defeats WHERE run=@run AND exploits=CASE WHEN victim_bp-killer_bp>20 THEN 3 WHEN victim_bp>killer_bp THEN 2 ELSE 1 END","run",match)==20);
            Require((long)Sql("SELECT count(*) FROM revora_guild_pk_banner_hits WHERE run=@run","run",match)==4);
            Require(Convert.ToInt64(Sql("SELECT damage FROM revora_guild_pk_banner_contributions WHERE run=@run AND uid=@uid","run",match,"uid",(long)p.Entity.UID))==2000000);
            Require(Convert.ToInt64(Sql("SELECT exploits FROM revora_guild_pk_banner_contributions WHERE run=@run AND uid=@uid","run",match,"uid",(long)p.Entity.UID))==200);
            Require(Convert.ToInt64(Sql("SELECT damage FROM revora_guild_pk_banner_contributions WHERE run=@run AND uid=@uid","run",match,"uid",(long)leaver.Entity.UID))==1000000);
            Require(Convert.ToInt64(Sql("SELECT exploits FROM revora_guild_pk_banner_contributions WHERE run=@run AND uid=@uid","run",match,"uid",(long)leaver.Entity.UID))==100);
            Require((long)Sql("SELECT count(*) FROM revora_guild_pk_revivals WHERE run=@run AND reviver IS NULL","run",match)==18);
            Require((long)Sql("SELECT count(*) FROM revora_guild_pk_revivals WHERE run=@run AND reviver=@uid","run",match,"uid",(long)offline.Entity.UID)==1);
            Require(Convert.ToInt64(Sql("SELECT exploits FROM revora_guild_pk_revival_contributions WHERE run=@run AND uid=@uid","run",match,"uid",(long)offline.Entity.UID))==10);
            GuildPkPoolPaymentProbe(match,p,offline,reload);
            GuildPkFinalScheduleProbe(reload);GuildPkMissedScheduleProbe(reload);GuildPkPlayedFinalProbe(reload,p,leaver);GuildPkPoolRefundProbe(reload,p,leaver);GuildPkEarnedPathProbe(reload);GuildPkConcurrentDeadlineProbe(reload);
            System.Console.WriteLine("[career-process] career-guild-pk-coordinator-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Guild PK coordinator fixture did not close.");
        }
    }
    static bool guildConcurrentFixture;
    static void GuildPkConcurrentDeadlineProbe(bool reload) {
        guildConcurrentFixture=true;const uint firstUid=4294917601;Guid run=new Guid("f6067100-0000-0000-0000-000000000052");
        var players=new Revora.Client.GameState[8];
        for(int i=0;i<8;i++) {
            uint uid=firstUid+(uint)i;
            if((long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==0){Require(!reload);nextUid=uid;Player(Kernel.Maps[1002]);Sql("UPDATE entities SET level=130,reborn=2,conquerpoints=2000000,money=5000000 WHERE uid=@uid","uid",(long)uid);}
            nextUid=uid;var p=players[i]=Player(Kernel.Maps[1002],true);SubClassTable.Load(p.Entity);ChiTable.Load(p);WayofHeroes.Load(p);
            Revora.Database.KnownPersons.LoadPartner(p);Revora.Database.KnownPersons.LoaderFriends(p);Revora.Database.KnownPersons.LoadMentor(p);Revora.Database.KnownPersons.LoadEnemy(p);p.LoadItemStats();
            if(p.Guild==null) {
                Require(!reload);HeartTalk(p,10003,9);var name=System.Text.Encoding.ASCII.GetBytes("ArenaGuild"+(char)('A'+i));var b=new byte[28+name.Length];
                Revora.Network.Writer.WriteUInt16((ushort)(b.Length-8),0,b);Revora.Network.Writer.WriteUInt16(2032,2,b);Revora.Network.Writer.WriteUInt32(10003,8,b);b[14]=2;b[17]=(byte)name.Length;name.CopyTo(b,18);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,b.Length-8,b);Revora.Network.PacketHandler.HandlePacket(b,p);
            }
            Require(p.Guild!=null && !p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        }
        Require(players.Select(p=>p.Guild.ID).Distinct().Count()==8);
        var parent=Feature("GuildContestCoordinator");var schedule=GameAssembly.GetType("Revora.Database.GuildPkSchedule",true);var ledger=GameAssembly.GetType("Revora.Database.GuildContestTable",true);
        DateTime local=new DateTime(2084,1,1,20,30,0,DateTimeKind.Local);while(local.DayOfWeek!=DayOfWeek.Monday)local=local.AddDays(1);
        DateTime start=local.ToUniversalTime(),begins=local.AddDays(1).ToUniversalTime(),ends=begins.AddHours(2),now=start;
        parent.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        if((long)Sql("SELECT count(*) FROM revora_guild_contests WHERE id=@run","run",run)==0) {
            Require(!reload);Sql("INSERT INTO revora_guild_contests(id,starts_at,ends_at,state) VALUES(@run,@start,@end,'frozen')","run",run,"start",new DateTimeOffset(start),"end",new DateTimeOffset(start.AddHours(2)));
            for(int i=0;i<8;i++){Sql("INSERT INTO revora_guild_contest_seeds VALUES(@run,@rank,@guild,@points,@now)","run",run,"rank",i+1,"guild",(long)players[i].Guild.ID,"points",8L-i,"now",new DateTimeOffset(start));BindContestFixtureGuild(ledger,run,players[i].Guild.ID);}
            Call(schedule,"Create",run);
        }
        if((long)Sql("SELECT count(*) FROM revora_guild_pk_results r JOIN revora_guild_pk_matches m ON m.id=r.run WHERE m.contest=@run AND m.round=1","run",run)==0) {
            Require(!reload);now=begins.AddMinutes(-4);Call(parent,"Tick");
            Require((long)Sql("SELECT count(*) FROM revora_guild_pk_maps f JOIN revora_guild_pk_matches m ON m.id=f.run WHERE m.contest=@run AND m.round=1","run",run)==4);
            foreach(var p in players){HeartTalk(p,706072,1);Require(p.Map.BaseID==700 && p.Entity.MapID!=1002);}
            var maps=players.Select(p=>p.Map).Distinct().OrderBy(m=>m.ID).ToArray();Require(maps.Length==4 && maps.All(m=>players.Count(p=>ReferenceEquals(p.Map,m))==2));
            now=begins;Call(parent,"Tick");
            GuildPkRejectDamage(players[0].Entity,players.First(p=>p.Map!=players[0].Map).Entity);
            var pairs=maps.Select(m=>players.Where(p=>ReferenceEquals(p.Map,m)).ToArray()).ToArray();
            now=begins.AddSeconds(10);GuildPkReflectionProbe(run,pairs[0][0],pairs[0][1],pairs[1][0]);
            now=begins.AddSeconds(15);Call(parent,"Tick");Require(!pairs[0][0].Entity.Dead && pairs[0][0].Entity.Hitpoints==pairs[0][0].Entity.MaxHitpoints);
            var outsider=Kernel.GamePool[4294917504];outsider.Entity.Teleport(maps[0].BaseID,maps[0].ID,50,55);Require(ReferenceEquals(outsider.Map,maps[0]));
            Action<Revora.Client.GameState> capture=p=> {
                var banner=(SobNpcSpawn)p.Map.Npcs[706074];p.Entity.X=(ushort)(banner.X-1);p.Entity.Y=banner.Y;p.Screen.Reload(null);
                p.Entity.MinAttack=p.Entity.MaxAttack=p.Entity.BaseMinAttack=p.Entity.BaseMaxAttack=10000000;
                var attack=new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=banner.UID};uint damage=Revora.Game.Attacking.Calculate.Melee(p.Entity,banner,ref attack);Require(damage>=banner.Hitpoints);
                Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,banner,attack,damage,null);
            };
            now=ends.AddSeconds(-20);for(int i=1;i<4;i++)capture(pairs[i][0]);
            now=ends.AddSeconds(-10);capture(pairs[1][1]);now=ends.AddSeconds(-5);capture(pairs[3][1]);
            now=ends;Call(parent,"Tick");
            Require(players.All(p=>p.Entity.MapID==1002) && outsider.Entity.MapID==1002 && outsider.Entity.X==327 && outsider.Entity.Y==303);
            Require(maps.All(m=>!Kernel.Maps.ContainsKey(m.ID)));
            foreach(var p in players)Require(EntityTable.SaveEntity(p));
        }
        // Dynamic map IDs are reused after restart. Scope retained map receipts to this contest.
        Require(players.Select(p=>p.Guild.ID).SequenceEqual(players.Select(p=>p.Guild.ID).OrderBy(id=>id)));
        for(int i=0;i<4;i++) {
            Guid match=(Guid)Sql("SELECT m.id FROM revora_guild_pk_maps f JOIN revora_guild_pk_matches m ON m.id=f.run WHERE m.contest=@run AND m.round=1 ORDER BY f.map OFFSET @offset LIMIT 1","run",run,"offset",i);
            long first=i==0?0:i==1?10:i==2?20:15,second=i==1?10:i==3?5:0;
            Require((long)Sql("SELECT count(*) FROM revora_guild_pk_matches m JOIN revora_guild_pk_results r ON r.run=m.id WHERE m.id=@run AND m.state='complete' AND r.reason='time' AND CASE WHEN m.first_guild<m.second_guild THEN r.first_score=@first AND r.second_score=@second ELSE r.first_score=@second AND r.second_score=@first END AND COALESCE(r.winner,0)=CASE WHEN @draw THEN 0 ELSE LEAST(m.first_guild,m.second_guild) END","run",match,"first",first,"second",second,"draw",i<2)==1);
        }
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_results r JOIN revora_guild_pk_matches m ON m.id=r.run WHERE m.contest=@run AND m.round=1 AND r.reason='time'","run",run)==4);
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_results r JOIN revora_guild_pk_matches m ON m.id=r.run WHERE m.contest=@run AND m.round=1 AND r.winner IS NULL","run",run)==2);
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_transfers f JOIN revora_guild_pk_matches m ON m.id=f.run WHERE m.contest=@run AND f.state='returned'","run",run)==8);
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_participation WHERE uid BETWEEN @first AND @last","first",(long)firstUid,"last",(long)firstUid+7)==8);
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_participation WHERE uid=4294917504")==0);
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_defeats d JOIN revora_guild_pk_matches m ON m.id=d.run WHERE m.contest=@run","run",run)==1);
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_revivals r JOIN revora_guild_pk_matches m ON m.id=r.run WHERE m.contest=@run AND r.reviver IS NULL","run",run)==1);
        Require(players.All(p=>p.Entity.MapID==1002));
        System.Console.WriteLine("[career-process] guild-pk-four-arenas-deadlines-cleanup-passed");
    }
    static void GuildPkReflectionProbe(Guid contest,Revora.Client.GameState attacker,Revora.Client.GameState defender,Revora.Client.GameState outsider) {
        var type=typeof(Revora.Game.Attacking.Calculate);var chance=type.GetField("ResponseChance",Hidden);var prior=chance.GetValue(null);
        bool supplied=!defender.Spells.ContainsKey(3060);if(supplied)defender.Spells.Add(3060,new Spell(true){ID=3060,Level=0,Available=true});
        var callback=type.GetMethod("AutoRespone",Hidden);
        Action<Entity,Entity> reflect=(a,b)=>{object[] args={a,b,1000};callback.Invoke(null,args);};
        try {
            chance.SetValue(null,(Func<int,bool>)(percent=>true));
            attacker.Entity.X=50;attacker.Entity.Y=59;defender.Entity.X=51;defender.Entity.Y=59;
            attacker.Entity.ClearFlags();defender.Entity.ClearFlags();defender.Entity.CounterKillSwitch=false;
            attacker.Entity.Hitpoints=500;uint outsideHp=outsider.Entity.Hitpoints;reflect(outsider.Entity,defender.Entity);Require(outsider.Entity.Hitpoints==outsideHp);
            reflect(attacker.Entity,defender.Entity);Require(attacker.Entity.Hitpoints==400 && !attacker.Entity.Dead);
            attacker.Entity.Hitpoints=1;reflect(attacker.Entity,defender.Entity);Require(attacker.Entity.Dead && attacker.Entity.Hitpoints==0 && attacker.Map==defender.Map);
            Require((long)Sql("SELECT count(*) FROM revora_guild_pk_defeats d JOIN revora_guild_pk_matches m ON m.id=d.run WHERE m.contest=@run AND d.uid=@victim AND d.killer=@killer AND d.number=1","run",contest,"victim",(long)attacker.Entity.UID,"killer",(long)defender.Entity.UID)==1);
            reflect(attacker.Entity,defender.Entity);Require((long)Sql("SELECT count(*) FROM revora_guild_pk_defeats d JOIN revora_guild_pk_matches m ON m.id=d.run WHERE m.contest=@run","run",contest)==1);
            System.Console.WriteLine("[career-process] guild-pk-reflection-owned-defeat-passed");
        }finally{chance.SetValue(null,prior);if(supplied)defender.Spells.Remove(3060);}
    }
    static bool guildPathFixture;
    static void GuildPkEarnedPathProbe(bool reload) {
        var parent=Feature("GuildContestCoordinator");guildPathFixture=true;const uint firstUid=4294917701;
        if((bool)Sql("SELECT to_regclass('revora_guild_pk_path_probe_v2') IS NULL")) {
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN @first AND @last","first",(long)firstUid,"last",(long)firstUid+2)==0);
            Sql("CREATE TABLE revora_guild_pk_path_probe_v2(id integer PRIMARY KEY,run uuid,complete boolean NOT NULL DEFAULT false);INSERT INTO revora_guild_pk_path_probe_v2(id) VALUES(1)");
        }
        var players=new Revora.Client.GameState[3];
        for(int i=0;i<3;i++) {
            uint uid=firstUid+(uint)i;
            if((long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==0){Require(!reload);nextUid=uid;Player(Kernel.Maps[1002]);Sql("UPDATE entities SET level=130,reborn=2,conquerpoints=2000000,money=5000000 WHERE uid=@uid","uid",(long)uid);}
            nextUid=uid;var p=players[i]=Player(Kernel.Maps[1002],true);SubClassTable.Load(p.Entity);ChiTable.Load(p);WayofHeroes.Load(p);
            Revora.Database.KnownPersons.LoadPartner(p);Revora.Database.KnownPersons.LoaderFriends(p);Revora.Database.KnownPersons.LoadMentor(p);Revora.Database.KnownPersons.LoadEnemy(p);p.LoadItemStats();
            Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player);
            if(p.Guild==null) {
                Require(!reload);HeartTalk(p,10003,9);var name=System.Text.Encoding.ASCII.GetBytes("PathLive"+(char)('A'+i));var b=new byte[28+name.Length];
                Revora.Network.Writer.WriteUInt16((ushort)(b.Length-8),0,b);Revora.Network.Writer.WriteUInt16(2032,2,b);Revora.Network.Writer.WriteUInt32(10003,8,b);b[14]=2;b[17]=(byte)name.Length;name.CopyTo(b,18);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,b.Length-8,b);Revora.Network.PacketHandler.HandlePacket(b,p);
            }
            Require(p.Guild!=null && p.AsMember.Rank==Enums.GuildMemberRank.GuildLeader && !PirateFenced(p));
        }
        DateTime local=new DateTime(2087,1,1,20,30,0,DateTimeKind.Local);while(local.DayOfWeek!=DayOfWeek.Monday)local=local.AddDays(1);
        DateTime starts=local.ToUniversalTime(),now=starts.AddMinutes(-4);parent.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        Guid run;
        if(Sql("SELECT run FROM revora_guild_pk_path_probe_v2 WHERE id=1")==DBNull.Value) {
            Require(!reload);Call(parent,"Tick");run=(Guid)Sql("SELECT id FROM revora_guild_contests WHERE starts_at=@start","start",new DateTimeOffset(starts));
            Sql("UPDATE revora_guild_pk_path_probe_v2 SET run=@run WHERE id=1","run",run);
            foreach(var p in players){HeartTalk(p,706070,1);Require(p.Map.BaseID==700);}
            now=starts;Call(parent,"Tick");
            for(int i=0;i<3;i++)for(int n=0;n<3-i;n++) {
                var p=players[i];var monster=p.Map.Entities.Values.First(m=>!m.Dead);p.Entity.X=monster.X;p.Entity.Y=monster.Y;p.Screen.Add(monster);
                p.Entity.MinAttack=p.Entity.MaxAttack=p.Entity.BaseMinAttack=p.Entity.BaseMaxAttack=10000000;
                var attack=new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=monster.UID};uint damage=Revora.Game.Attacking.Calculate.Melee(p.Entity,monster,ref attack);Require(damage>=monster.Hitpoints);
                Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,monster,attack,ref damage,null);Require(monster.Dead);
                now=now.AddSeconds(2);Call(parent,"Tick");
            }
            now=starts.AddHours(2);Call(parent,"Tick");Require(players.All(p=>p.Entity.MapID==1002));
            foreach(var p in players){HeartTalk(p,706070,0);HeartTalk(p,706070,2);}
        }else run=(Guid)Sql("SELECT run FROM revora_guild_pk_path_probe_v2 WHERE id=1");
        for(int i=0;i<3;i++)Require((long)Sql("SELECT points FROM revora_guild_contest_seeds WHERE run=@run AND guild_id=@guild AND rank=@rank","run",run,"guild",(long)players[i].Guild.ID,"rank",i+1)==3-i);
        Require((long)Sql("SELECT count(*) FROM revora_guild_contest_kills WHERE run=@run","run",run)==6);
        Require((long)Sql("SELECT count(*) FROM revora_guild_contest_prizes WHERE run=@run","run",run)==3);
        Action<Guid,Revora.Client.GameState,Revora.Client.GameState,DateTime> play=(match,a,b,begins)=> {
            if(Convert.ToString(Sql("SELECT state FROM revora_guild_pk_matches WHERE id=@run","run",match))=="complete")return;
            Require(!reload);now=begins.AddMinutes(-4);Call(parent,"Tick");
            HeartTalk(a,706072,3);HeartTalk(a,706072,6);
            foreach(var p in new[]{a,b}){HeartTalk(p,706072,1);Require(p.Map.BaseID==700);}
            var map=a.Map;Require(ReferenceEquals(map,b.Map));now=begins;Call(parent,"Tick");
            var banner=(SobNpcSpawn)map.Npcs[706074];now=begins.AddSeconds(10);a.Entity.X=(ushort)(banner.X-1);a.Entity.Y=banner.Y;a.Screen.Reload(null);
            a.Entity.MinAttack=a.Entity.MaxAttack=a.Entity.BaseMinAttack=a.Entity.BaseMaxAttack=10000000;
            var packet=new Attack(true){AttackType=Attack.Melee,Attacker=a.Entity.UID,Attacked=banner.UID};uint hit=Revora.Game.Attacking.Calculate.Melee(a.Entity,banner,ref packet);Require(hit>=banner.Hitpoints);
            Revora.Game.Attacking.Handle.ReceiveAttack(a.Entity,banner,packet,hit,null);
            now=begins.AddMinutes(30);Call(parent,"Tick");now=now.AddSeconds(32);Call(parent,"Tick");
            Require(a.Entity.MapID==1002 && b.Entity.MapID==1002 && !Kernel.Maps.ContainsKey(map.ID));
            Require(Convert.ToUInt32(Sql("SELECT winner FROM revora_guild_pk_results WHERE run=@run","run",match))==a.Guild.ID);
            Require((long)Sql("SELECT count(*) FROM revora_guild_pk_payouts WHERE run=@run AND uid=@uid AND amount=1000000 AND paid_at IS NOT NULL","run",match,"uid",(long)a.Entity.UID)==1);
        };
        Guid group=(Guid)Sql("SELECT id FROM revora_guild_pk_matches WHERE contest=@run AND round<4","run",run);
        play(group,players[0],players[2],local.AddDays(3).ToUniversalTime());
        Guid final=(Guid)Sql("SELECT id FROM revora_guild_pk_matches WHERE contest=@run AND round=4","run",run);
        play(final,players[1],players[0],local.AddDays(5).AddHours(1).ToUniversalTime());
        Require(Convert.ToString(Sql("SELECT phase FROM revora_guild_pk_weeks WHERE contest=@run","run",run))=="complete");
        Require(Convert.ToUInt32(Sql("SELECT guild_id FROM revora_guild_pk_placements WHERE contest=@run AND rank=1","run",run))==players[1].Guild.ID);
        Require(Convert.ToUInt32(Sql("SELECT guild_id FROM revora_guild_pk_placements WHERE contest=@run AND rank=2","run",run))==players[0].Guild.ID);
        now=local.AddDays(5).AddHours(3).ToUniversalTime();
        foreach(var p in players.Take(2)){HeartTalk(p,706072,0);HeartTalk(p,706072,2);Require(!PirateFenced(p));}
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_prizes WHERE contest=@run","run",run)==2);
        long[] funds={159500000,169500000,40500000};uint[] cps={2000,3000,0};
        for(int i=0;i<3;i++) {
            Require(players[i].Guild.SilverFund==(ulong)funds[i] && players[i].Guild.ConquerPointFund==cps[i]);
            Require(Convert.ToInt64(Sql("SELECT silverfund FROM guilds WHERE id=@guild","guild",(long)players[i].Guild.ID))==funds[i]);
            Require((long)Sql("SELECT silver FROM revora_guild_contest_prizes WHERE run=@run AND guild_id=@guild","run",run,"guild",(long)players[i].Guild.ID)==60000000-i*10000000);
        }
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_payouts p JOIN revora_guild_pk_matches m ON m.id=p.run WHERE m.contest=@run AND p.amount=1000000 AND p.paid_at IS NOT NULL","run",run)==2);
        foreach(var p in players)GuildPkParticipationClaimProbe(reload,p);
        Sql("UPDATE revora_guild_pk_path_probe_v2 SET complete=true WHERE id=1");
        System.Console.WriteLine("[career-process] guild-pk-earned-qualifier-group-final-passed");
        GuildPkInterruptedReturnProbe(reload,players[0],players[2]);
        GuildPkDisbandProbe(reload,players[1]);
    }
    static void GuildPkDisbandProbe(bool reload,Revora.Client.GameState other) {
        const uint uid=4294917504;Guid run=new Guid("f6067100-0000-0000-0000-000000000045");
        Sql("CREATE TABLE IF NOT EXISTS revora_guild_pk_disband_probe(id integer PRIMARY KEY,old_guild bigint,new_guild bigint,complete boolean NOT NULL DEFAULT false);INSERT INTO revora_guild_pk_disband_probe(id) VALUES(1) ON CONFLICT DO NOTHING");
        if((long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==0){Require(!reload);nextUid=uid;Player(Kernel.Maps[1002]);Sql("UPDATE entities SET level=130,reborn=2,conquerpoints=2000000,money=5000000 WHERE uid=@uid","uid",(long)uid);}
        nextUid=uid;var p=Player(Kernel.Maps[1002],true);SubClassTable.Load(p.Entity);ChiTable.Load(p);WayofHeroes.Load(p);
        Revora.Database.KnownPersons.LoadPartner(p);Revora.Database.KnownPersons.LoaderFriends(p);Revora.Database.KnownPersons.LoadMentor(p);Revora.Database.KnownPersons.LoadEnemy(p);p.LoadItemStats();
        Action found=()=> {
            HeartTalk(p,10003,9);var name=System.Text.Encoding.ASCII.GetBytes("LifeGuild");var b=new byte[28+name.Length];
            Revora.Network.Writer.WriteUInt16((ushort)(b.Length-8),0,b);Revora.Network.Writer.WriteUInt16(2032,2,b);Revora.Network.Writer.WriteUInt32(10003,8,b);b[14]=2;b[17]=(byte)name.Length;name.CopyTo(b,18);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,b.Length-8,b);Revora.Network.PacketHandler.HandlePacket(b,p);
            Require(p.Guild!=null && !PirateFenced(p) && p.AsMember.Rank==Enums.GuildMemberRank.GuildLeader);
        };
        if(Sql("SELECT old_guild FROM revora_guild_pk_disband_probe WHERE id=1")==DBNull.Value){Require(!reload);found();Sql("UPDATE revora_guild_pk_disband_probe SET old_guild=@guild WHERE id=1","guild",(long)p.Guild.ID);}
        uint old=Convert.ToUInt32(Sql("SELECT old_guild FROM revora_guild_pk_disband_probe WHERE id=1"));
        var parent=Feature("GuildContestCoordinator");var schedule=GameAssembly.GetType("Revora.Database.GuildPkSchedule",true);var ledger=GameAssembly.GetType("Revora.Database.GuildContestTable",true);
        DateTime local=new DateTime(2077,1,1,20,30,0,DateTimeKind.Local);while(local.DayOfWeek!=DayOfWeek.Monday)local=local.AddDays(1);
        DateTime start=local.ToUniversalTime(),begins=local.AddDays(5).AddHours(1).ToUniversalTime(),now=start;
        parent.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        if((long)Sql("SELECT count(*) FROM revora_guild_contests WHERE id=@run","run",run)==0) {
            Require(!reload);Sql("INSERT INTO revora_guild_contests(id,starts_at,ends_at,state) VALUES(@run,@start,@end,'frozen')","run",run,"start",new DateTimeOffset(start),"end",new DateTimeOffset(start.AddHours(2)));
            uint[] guilds={old,other.Guild.ID};for(int i=0;i<2;i++){Sql("INSERT INTO revora_guild_contest_seeds VALUES(@run,@rank,@guild,@points,@now)","run",run,"rank",i+1,"guild",(long)guilds[i],"points",2L-i,"now",new DateTimeOffset(start));BindContestFixtureGuild(ledger,run,guilds[i]);}
            Call(schedule,"Create",run);Call(schedule,"Advance",run,start.AddHours(3));
        }
        Guid match=(Guid)Sql("SELECT id FROM revora_guild_pk_matches WHERE contest=@run","run",run);
        if(!(bool)Sql("SELECT complete FROM revora_guild_pk_disband_probe WHERE id=1")) {
            Require(!reload);now=begins.AddMinutes(-4);Call(parent,"Tick");
            if(p.Guild!=null && p.Guild.ID==old) {
                if((long)Sql("SELECT count(*) FROM revora_guild_pk_funding WHERE run=@run","run",match)==0) {
                    GuildClaimPacket(p,GuildCommand.DonateSilvers,1000000);Require(p.Guild.SilverFund==1500000);
                    HeartTalk(p,706072,3);HeartTalk(p,706072,6);Require(p.Guild.SilverFund==500000);
                }
                HeartTalk(p,706070,0); // Retain the old guild's qualifier prize offer.
                HeartTalk(p,10003,6);HeartTalk(p,10003,7);
                Require(p.Guild==null && p.AsMember==null && p.Entity.GuildID==0 && !PirateFenced(p));
                Require((long)Sql("SELECT count(*) FROM guilds WHERE id=@guild","guild",(long)old)==0);
                Require(!(bool)Sql("SELECT eligible FROM revora_guild_contest_owners WHERE run=@run AND guild_id=@guild","run",run,"guild",(long)old));
            }
            if(p.Guild==null)found();Require(p.Guild.ID!=old && p.Guild.Name=="LifeGuild");
            Sql("UPDATE revora_guild_pk_disband_probe SET new_guild=@guild WHERE id=1","guild",(long)p.Guild.ID);
            HeartTalk(p,706070,2);HeartTalk(p,706072,1);Require(p.Entity.MapID==1002);
            now=begins;Call(parent,"Tick");Call(Feature("GuildPkCoordinator"),"RewardPulse",now.AddSeconds(2));
            Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_guild_pk_disband_probe SET complete=true WHERE id=1");
        }
        Require(p.Guild!=null && p.Guild.ID==Convert.ToUInt32(Sql("SELECT new_guild FROM revora_guild_pk_disband_probe WHERE id=1")) && p.Guild.ID!=old);
        Require(p.Guild.SilverFund==500000 && p.Guild.ConquerPointFund==0);
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_pools WHERE run=@run AND guild_id=@guild AND state='orphaned' AND amount=1000000 AND refund=0","run",match,"guild",(long)old)==1);
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_funding WHERE run=@run","run",match)==1);
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_payouts WHERE run=@run","run",match)==0);
        Require((long)Sql("SELECT count(*) FROM revora_guild_contest_prizes WHERE run=@run","run",run)==0);
        Require(!(bool)Sql("SELECT eligible FROM revora_guild_contest_owners WHERE run=@run AND guild_id=@guild","run",run,"guild",(long)old));
        System.Console.WriteLine("[career-process] guild-pk-disband-refound-ownership-passed");
    }
    static void GuildPkInterruptedReturnCheck() {
        if((bool)Sql("SELECT to_regclass('revora_guild_pk_interruption_probe') IS NULL"))return;
        if((long)Sql("SELECT count(*) FROM revora_guild_pk_interruption_probe WHERE NOT checked")==0)return;
        Require((long)Sql(@"SELECT count(*) FROM revora_guild_pk_interruption_probe p
JOIN revora_guild_pk_matches m ON m.id=p.run JOIN entities e ON e.uid=p.uid
JOIN revora_guild_pk_transfers f ON f.run=p.run AND f.uid=p.uid
JOIN revora_guild_pk_entries a ON a.run=p.run AND a.uid=p.uid
WHERE NOT p.checked AND m.state='cancelled' AND f.state='returned' AND NOT a.active
AND e.mapid=1002 AND e.x=f.return_x AND e.y=f.return_y AND e.hitpoints=p.expected_hp")==2);
        var run=(Guid)Sql("SELECT run FROM revora_guild_pk_interruption_probe LIMIT 1");
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_defeats WHERE run=@run","run",run)==1);
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_revivals WHERE run=@run","run",run)==0);
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_results WHERE run=@run","run",run)==0);
        foreach(uint uid in new[]{4294917501u,4294917503u})Require((bool)Call(Feature("GuildPkAdmission"),"ReturnOffline",run,uid));
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_interruption_probe p JOIN entities e ON e.uid=p.uid WHERE e.hitpoints=p.expected_hp AND e.mapid=1002")==2);
        Sql("UPDATE revora_guild_pk_interruption_probe SET checked=true");
        System.Console.WriteLine("[career-process] guild-pk-interrupted-downed-return-passed");
    }
    static void GuildPkInterruptedReturnProbe(bool reload,Revora.Client.GameState killer,Revora.Client.GameState victim) {
        if(!(bool)Sql("SELECT to_regclass('revora_guild_pk_interruption_probe') IS NULL"))return;
        Require(!reload);
        var parent=Feature("GuildContestCoordinator");var schedule=GameAssembly.GetType("Revora.Database.GuildPkSchedule",true);
        var ledger=GameAssembly.GetType("Revora.Database.GuildContestTable",true);Guid run=new Guid("f6067100-0000-0000-0000-000000000044");
        DateTime local=new DateTime(2076,1,1,20,30,0,DateTimeKind.Local);while(local.DayOfWeek!=DayOfWeek.Monday)local=local.AddDays(1);
        DateTime start=local.ToUniversalTime(),begins=local.AddDays(5).AddHours(1).ToUniversalTime(),now=start;
        parent.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        Sql("INSERT INTO revora_guild_contests(id,starts_at,ends_at,state) VALUES(@run,@start,@end,'frozen')","run",run,"start",new DateTimeOffset(start),"end",new DateTimeOffset(start.AddHours(2)));
        uint[] guilds={killer.Guild.ID,victim.Guild.ID};for(int i=0;i<2;i++) {
            Sql("INSERT INTO revora_guild_contest_seeds VALUES(@run,@rank,@guild,@points,@now)","run",run,"rank",i+1,"guild",(long)guilds[i],"points",2L-i,"now",new DateTimeOffset(start));BindContestFixtureGuild(ledger,run,guilds[i]);
        }
        Call(schedule,"Create",run);Call(schedule,"Advance",run,start.AddHours(3));
        Guid match=(Guid)Sql("SELECT id FROM revora_guild_pk_matches WHERE contest=@run AND round=4","run",run);
        now=begins.AddMinutes(-4);Call(parent,"Tick");foreach(var p in new[]{killer,victim}){HeartTalk(p,706072,1);Require(p.Map.BaseID==700);}
        now=begins;Call(parent,"Tick");now=begins.AddSeconds(10);
        killer.Entity.X=50;killer.Entity.Y=59;victim.Entity.X=51;victim.Entity.Y=59;
        killer.Attackable=victim.Attackable=true;killer.Entity.PKMode=victim.Entity.PKMode=Enums.PKMode.PK;
        victim.Entity.ClearFlags();victim.Entity.Hitpoints=1;killer.Entity.MinAttack=killer.Entity.MaxAttack=killer.Entity.BaseMinAttack=killer.Entity.BaseMaxAttack=10000000;
        Require(Revora.Game.Attacking.Handle.CanAttack(killer.Entity,victim.Entity,null,true));
        var packet=new Attack(true){AttackType=Attack.Melee,Attacker=killer.Entity.UID,Attacked=victim.Entity.UID};
        uint damage=Revora.Game.Attacking.Calculate.Melee(killer.Entity,victim.Entity,ref packet);Require(damage>=victim.Entity.Hitpoints);
        Revora.Game.Attacking.Handle.ReceiveAttack(killer.Entity,victim.Entity,packet,ref damage,null);
        Require(victim.Entity.Dead && victim.Entity.Hitpoints==0 && killer.Entity.Hitpoints>1);
        foreach(var p in new[]{killer,victim})Require(EntityTable.SaveEntity(p));
        Require(Convert.ToInt32(Sql("SELECT hitpoints FROM entities WHERE uid=@uid","uid",(long)victim.Entity.UID))==0);
        Sql("CREATE TABLE revora_guild_pk_interruption_probe(uid bigint PRIMARY KEY,run uuid NOT NULL,expected_hp bigint NOT NULL,checked boolean NOT NULL DEFAULT false)");
        foreach(var p in new[]{killer,victim}) {
            Sql("INSERT INTO revora_guild_pk_interruption_probe(uid,run,expected_hp) VALUES(@uid,@run,@hp)","uid",(long)p.Entity.UID,"run",match,"hp",(long)Math.Max(1u,p.Entity.Hitpoints));
            Kernel.GamePool.Remove(p.Entity.UID);
        }
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_transfers WHERE run=@run AND state<>'returned'","run",match)==2);
        System.Console.WriteLine("[career-process] guild-pk-downed-interruption-saved");
    }
    static void GuildPkPayoutFaultProbe(Guid match,Revora.Client.GameState online,Revora.Client.GameState offline,DateTime now) {
        var table=GameAssembly.GetType("Revora.Database.GuildPkRewardTable",true);uint guild=online.Guild.ID;
        using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){object[] args={c,t,match,guild,now,null};Require((bool)table.GetMethod("Allocate",Hidden).Invoke(null,args));t.Commit();}}
        var session=Feature("SessionInstall");Require((bool)Call(session,"TryEnter",offline.Entity.UID));
        try {
            decimal before=Convert.ToDecimal(Sql("SELECT money FROM entities WHERE uid=@uid","uid",(long)offline.Entity.UID));
            Func<NpgsqlConnection,NpgsqlTransaction,bool> pay=(c,t)=>{object[] args={c,t,match,guild,offline.Entity.UID,null,now,0UL};return (bool)table.GetMethod("Pay",Hidden).Invoke(null,args);};
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                using(var q=new NpgsqlCommand("UPDATE entities SET money=@amount WHERE uid=@uid",c,t)){q.Parameters.AddWithValue("amount",long.MaxValue);q.Parameters.AddWithValue("uid",(long)offline.Entity.UID);Require(q.ExecuteNonQuery()==1);}
                Require(!pay(c,t));t.Rollback();
            }}
            Sql(@"CREATE OR REPLACE FUNCTION revora_test_pk_pay_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.paid_at IS NOT NULL THEN RETURN NULL; END IF; RETURN NEW; END $$;
CREATE TRIGGER revora_test_pk_pay_failure BEFORE UPDATE ON revora_guild_pk_payouts FOR EACH ROW EXECUTE FUNCTION revora_test_pk_pay_failure();");
            try {using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){bool failed=false;try{pay(c,t);}catch(System.Reflection.TargetInvocationException ex){failed=ex.InnerException is InvalidOperationException;}Require(failed);t.Rollback();}}}
            finally{Sql("DROP TRIGGER IF EXISTS revora_test_pk_pay_failure ON revora_guild_pk_payouts;DROP FUNCTION IF EXISTS revora_test_pk_pay_failure()");}
            Require(Convert.ToDecimal(Sql("SELECT money FROM entities WHERE uid=@uid","uid",(long)offline.Entity.UID))==before);
            Require((long)Sql("SELECT count(*) FROM revora_guild_pk_payouts WHERE run=@run AND paid_at IS NULL","run",match)==2);
        }finally{Call(session,"Exit",offline.Entity.UID);}
        System.Console.WriteLine("[career-process] guild-pk-purse-cap-payment-rollback-passed");
    }
    static void GuildPkFundingProbe(Guid match,Revora.Client.GameState leader,Revora.Client.GameState member,Revora.Client.GameState offline,DateTime now) {
        var table=GameAssembly.GetType("Revora.Database.GuildPkRewardTable",true);uint guild=leader.Guild.ID;
        Func<NpgsqlConnection,NpgsqlTransaction,uint,DateTime,int> fund=(c,t,uid,at)=>{object[] args={c,t,Guid.NewGuid(),match,guild,uid,1000000L,at,0UL};return (int)table.GetMethod("Fund",Hidden).Invoke(null,args);};
        decimal before=Convert.ToDecimal(Sql("SELECT silverfund FROM guilds WHERE id=@guild","guild",(long)guild));
        using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){Require(fund(c,t,member.Entity.UID,now)==0);Require(fund(c,t,leader.Entity.UID,now.AddMinutes(4))==0);t.Rollback();}}
        Sql(@"CREATE OR REPLACE FUNCTION revora_test_pk_fund_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.silverfund<OLD.silverfund THEN RETURN NULL; END IF; RETURN NEW; END $$;
CREATE TRIGGER revora_test_pk_fund_failure BEFORE UPDATE ON guilds FOR EACH ROW EXECUTE FUNCTION revora_test_pk_fund_failure();");
        try {using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){bool failed=false;try{fund(c,t,leader.Entity.UID,now);}catch(System.Reflection.TargetInvocationException ex){failed=ex.InnerException is InvalidOperationException;}Require(failed);t.Rollback();}}}
        finally{Sql("DROP TRIGGER IF EXISTS revora_test_pk_fund_failure ON guilds;DROP FUNCTION IF EXISTS revora_test_pk_fund_failure()");}
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_pools WHERE run=@run","run",match)==0);
        HeartTalk(member,706072,3);HeartTalk(member,706072,6);Require((long)Sql("SELECT count(*) FROM revora_guild_pk_pools WHERE run=@run","run",match)==0);
        Sql("CREATE TABLE IF NOT EXISTS revora_guild_pk_pool_probe(run uuid PRIMARY KEY,online_uid bigint,offline_uid bigint,online_before numeric,offline_before numeric,fund_before numeric)");
        Sql("INSERT INTO revora_guild_pk_pool_probe VALUES(@run,@online,@offline,@money,@other,@fund)","run",match,"online",(long)leader.Entity.UID,"offline",(long)offline.Entity.UID,"money",(decimal)leader.Entity.Money,"other",(decimal)offline.Entity.Money,"fund",before);
        HeartTalk(leader,706072,3);HeartTalk(leader,706072,6);HeartTalk(leader,706072,6);
        Require(!PirateFenced(leader) && (decimal)leader.Guild.SilverFund==before-1000000);
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_funding WHERE run=@run","run",match)==1);
    }
    static void GuildPkPoolPaymentProbe(Guid match,Revora.Client.GameState online,Revora.Client.GameState offline,bool reload) {
        Require(Convert.ToString(Sql("SELECT state FROM revora_guild_pk_pools WHERE run=@run","run",match))=="allocated");
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_payouts WHERE run=@run AND paid_at IS NOT NULL","run",match)==2);
        Require(Convert.ToInt64(Sql("SELECT sum(amount) FROM revora_guild_pk_payouts WHERE run=@run","run",match))==1000000);
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_payouts WHERE run=@run AND ((uid=@online AND points=200 AND amount=952381) OR (uid=@offline AND points=10 AND amount=47619))","run",match,"online",(long)online.Entity.UID,"offline",(long)offline.Entity.UID)==2);
        Require((decimal)online.Entity.Money==Convert.ToDecimal(Sql("SELECT online_before+952381 FROM revora_guild_pk_pool_probe WHERE run=@run","run",match)));
        Require(Convert.ToDecimal(Sql("SELECT money FROM entities WHERE uid=@uid","uid",(long)offline.Entity.UID))==Convert.ToDecimal(Sql("SELECT offline_before+47619 FROM revora_guild_pk_pool_probe WHERE run=@run","run",match)));
        if(reload)Require((decimal)offline.Entity.Money==Convert.ToDecimal(Sql("SELECT offline_before+47619 FROM revora_guild_pk_pool_probe WHERE run=@run","run",match)));
        Require(EntityTable.SaveEntity(online)); // An ordinary subsequent save must preserve the credited purse.
        System.Console.WriteLine("[career-process] guild-pk-pool-online-offline-payout-passed");
    }
    static void GuildPkRefundGuardProbe(Guid match,uint guild,DateTime now) {
        var table=GameAssembly.GetType("Revora.Database.GuildPkRewardTable",true);
        for(int mode=0;mode<2;mode++)using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
            using(var q=new NpgsqlCommand("UPDATE revora_guild_pk_matches SET state='cancelled' WHERE id=@run",c,t)){q.Parameters.AddWithValue("run",match);Require(q.ExecuteNonQuery()==1);}
            string mutation=mode==0?"UPDATE guilds SET silverfund=@maximum WHERE id=@guild":"UPDATE revora_guild_contest_owners SET eligible=false WHERE guild_id=@guild AND run=(SELECT contest FROM revora_guild_pk_matches WHERE id=@run)";
            using(var q=new NpgsqlCommand(mutation,c,t)){q.Parameters.AddWithValue("run",match);q.Parameters.AddWithValue("guild",(long)guild);q.Parameters.AddWithValue("maximum",long.MaxValue);Require(q.ExecuteNonQuery()==1);}
            object[] args={c,t,match,guild,now,null};Require((bool)table.GetMethod("Allocate",Hidden).Invoke(null,args)==(mode==1));Require(args[5]==null);
            using(var q=new NpgsqlCommand("SELECT state FROM revora_guild_pk_pools WHERE run=@run AND guild_id=@guild",c,t)){q.Parameters.AddWithValue("run",match);q.Parameters.AddWithValue("guild",(long)guild);Require(Convert.ToString(q.ExecuteScalar())==(mode==0?"funded":"orphaned"));}
            using(var q=new NpgsqlCommand("SELECT count(*) FROM revora_guild_pk_payouts WHERE run=@run",c,t)){q.Parameters.AddWithValue("run",match);Require((long)q.ExecuteScalar()==0);}
            t.Rollback(); // Capacity and invalidated ownership are isolated fault states, not player progress.
        }}
        Require(Convert.ToString(Sql("SELECT state FROM revora_guild_pk_pools WHERE run=@run","run",match))=="funded");
        System.Console.WriteLine("[career-process] guild-pk-refund-cap-ownership-guards-passed");
    }
    static void GuildPkPoolRefundProbe(bool reload,Revora.Client.GameState leader,Revora.Client.GameState other) {
        var parent=Feature("GuildContestCoordinator");var schedule=GameAssembly.GetType("Revora.Database.GuildPkSchedule",true);var ledger=GameAssembly.GetType("Revora.Database.GuildContestTable",true);
        for(int mode=0;mode<2;mode++) {
            Guid run=new Guid(mode==0?"f6067100-0000-0000-0000-000000000042":"f6067100-0000-0000-0000-000000000043");
            DateTime local=new DateTime(2070+mode,1,1,20,30,0,DateTimeKind.Local);while(local.DayOfWeek!=DayOfWeek.Monday)local=local.AddDays(1);
            DateTime starts=local.ToUniversalTime(),begins=local.AddDays(5).AddHours(1).ToUniversalTime(),now=starts;
            parent.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
            if((long)Sql("SELECT count(*) FROM revora_guild_contests WHERE id=@run","run",run)==0) {
                Require(!reload);Sql("INSERT INTO revora_guild_contests(id,starts_at,ends_at,state) VALUES(@run,@start,@end,'frozen')","run",run,"start",new DateTimeOffset(starts),"end",new DateTimeOffset(starts.AddHours(2)));
                uint[] guilds={leader.Guild.ID,other.Guild.ID};for(int i=0;i<2;i++){Sql("INSERT INTO revora_guild_contest_seeds VALUES(@run,@rank,@guild,@points,@now)","run",run,"rank",i+1,"guild",(long)guilds[i],"points",2L-i,"now",new DateTimeOffset(starts));BindContestFixtureGuild(ledger,run,guilds[i]);}
                Call(schedule,"Create",run);Call(schedule,"Advance",run,starts.AddHours(3));
            }
            Guid match=(Guid)Sql("SELECT id FROM revora_guild_pk_matches WHERE contest=@run","run",run);
            if(Convert.ToString(Sql("SELECT state FROM revora_guild_pk_matches WHERE id=@run","run",match))=="scheduled") {
                Require(!reload);now=begins.AddMinutes(-4);Call(parent,"Tick");ulong before=leader.Guild.SilverFund;
                HeartTalk(leader,706072,3);HeartTalk(leader,706072,6);HeartTalk(leader,706072,4);HeartTalk(leader,706072,6);Require(leader.Guild.SilverFund==before-6000000);
                GuildPkRefundGuardProbe(match,leader.Guild.ID,begins);
                if(mode==1){HeartTalk(leader,706072,1);Require(leader.Map.BaseID==700);}
                now=begins;Call(parent,"Tick");Require(leader.Entity.MapID==1002 && leader.Guild.SilverFund==before);
            }
            Require(Convert.ToString(Sql("SELECT state FROM revora_guild_pk_matches WHERE id=@run","run",match))==(mode==0?"cancelled":"complete"));
            Require((long)Sql("SELECT count(*) FROM revora_guild_pk_pools WHERE run=@run AND state='refunded' AND amount=6000000 AND refund=6000000","run",match)==1);
            Require((long)Sql("SELECT count(*) FROM revora_guild_pk_funding WHERE run=@run","run",match)==2);
            Require((long)Sql("SELECT count(*) FROM revora_guild_pk_payouts WHERE run=@run","run",match)==0);
            ulong saved=leader.Guild.SilverFund;Call(Feature("GuildPkCoordinator"),"RewardPulse",begins.AddSeconds(2));Require(leader.Guild.SilverFund==saved);
        }
        System.Console.WriteLine("[career-process] guild-pk-cancelled-scoreless-refunds-passed");
    }
    static void GuildPkPlayedFinalProbe(bool reload,Revora.Client.GameState first,Revora.Client.GameState second) {
        var parent=Feature("GuildContestCoordinator");var schedule=GameAssembly.GetType("Revora.Database.GuildPkSchedule",true);
        var ledger=GameAssembly.GetType("Revora.Database.GuildContestTable",true);Guid run=new Guid("f6067100-0000-0000-0000-000000000029");
        DateTime local=new DateTime(2058,1,1,20,30,0,DateTimeKind.Local);while(local.DayOfWeek!=DayOfWeek.Monday)local=local.AddDays(1);
        DateTime start=local.ToUniversalTime(),begins=local.AddDays(5).AddHours(1).ToUniversalTime(),now=start;
        parent.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        if((long)Sql("SELECT count(*) FROM revora_guild_contests WHERE id=@run","run",run)==0) {
            Require(!reload);Sql("INSERT INTO revora_guild_contests(id,starts_at,ends_at,state) VALUES(@run,@start,@end,'frozen')","run",run,"start",new DateTimeOffset(start),"end",new DateTimeOffset(start.AddHours(2)));
            uint[] guilds={first.Guild.ID,second.Guild.ID};for(int i=0;i<2;i++) {
                Sql("INSERT INTO revora_guild_contest_seeds VALUES(@run,@rank,@guild,@points,@now)","run",run,"rank",i+1,"guild",(long)guilds[i],"points",2L-i,"now",new DateTimeOffset(start));BindContestFixtureGuild(ledger,run,guilds[i]);
            }
            Call(schedule,"Create",run);Call(schedule,"Advance",run,start.AddHours(3));
        }
        Guid match=(Guid)Sql("SELECT id FROM revora_guild_pk_matches WHERE contest=@run AND round=4","run",run);
        if(Convert.ToString(Sql("SELECT state FROM revora_guild_pk_matches WHERE id=@run","run",match))=="scheduled") {
            Require(!reload);now=begins.AddMinutes(-6);Call(parent,"Tick");HeartTalk(first,706072,1);Require(first.Entity.MapID==1002);
            now=begins.AddMinutes(-4);Call(parent,"Tick");foreach(var p in new[]{first,second}) {HeartTalk(p,706072,0);HeartTalk(p,706072,1);Require(p.Map.BaseID==700 && p.Entity.MapID!=1002);}
            var map=first.Map;Require(ReferenceEquals(second.Map,map));
            foreach(var actor in new[]{first,second}) {string before=NobilityClaimSnapshot(actor);NobilityClaimPacket(actor,607);Require(before==NobilityClaimSnapshot(actor));}
            Require((long)Sql("SELECT count(*) FROM revora_guild_pk_participation WHERE run=@run","run",match)==0);
            Sql(@"CREATE OR REPLACE FUNCTION revora_test_pk_participation_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;
CREATE TRIGGER revora_test_pk_participation_failure BEFORE INSERT ON revora_guild_pk_participation FOR EACH ROW EXECUTE FUNCTION revora_test_pk_participation_failure();");
            try {now=begins;Call(parent,"Tick");Require((long)Sql("SELECT count(*) FROM revora_guild_pk_participation WHERE run=@run","run",match)==0);}
            finally{Sql("DROP TRIGGER IF EXISTS revora_test_pk_participation_failure ON revora_guild_pk_participation;DROP FUNCTION IF EXISTS revora_test_pk_participation_failure()");}
            Call(Feature("GuildPkBanner"),"Pulse",map);
            Require((long)Sql("SELECT count(*) FROM revora_guild_pk_participation WHERE run=@run","run",match)==2);
            var banner=(SobNpcSpawn)map.Npcs[706074];
            now=begins.AddSeconds(10);first.Entity.X=(ushort)(banner.X-1);first.Entity.Y=banner.Y;first.Screen.Reload(null);
            first.Entity.MinAttack=first.Entity.MaxAttack=first.Entity.BaseMinAttack=first.Entity.BaseMaxAttack=10000000;
            var packet=new Attack(true){AttackType=Attack.Melee,Attacker=first.Entity.UID,Attacked=banner.UID};
            Require(Revora.Game.Attacking.Handle.CanAttack(first.Entity,banner,null));uint damage=Revora.Game.Attacking.Calculate.Melee(first.Entity,banner,ref packet);Require(damage>=banner.Hitpoints);
            Revora.Game.Attacking.Handle.ReceiveAttack(first.Entity,banner,packet,damage,null);
            now=begins.AddMinutes(30).AddMilliseconds(-1);Call(parent,"Tick");Require(Convert.ToString(Sql("SELECT state FROM revora_guild_pk_matches WHERE id=@run","run",match))=="active");
            now=begins.AddMinutes(30);Call(Feature("GuildPkBanner"),"Pulse",map);
            Require(Convert.ToString(Sql("SELECT state FROM revora_guild_pk_matches WHERE id=@run","run",match))=="complete");
            now=begins.AddMinutes(30).AddSeconds(2);Call(parent,"Tick");
            Require(first.Entity.MapID==1002 && second.Entity.MapID==1002 && !Kernel.Maps.ContainsKey(map.ID));
            Sql(@"CREATE OR REPLACE FUNCTION revora_test_pk_placement_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.rank=2 THEN RETURN NULL; END IF; RETURN NEW; END $$;
CREATE TRIGGER revora_test_pk_placement_failure BEFORE INSERT ON revora_guild_pk_placements FOR EACH ROW EXECUTE FUNCTION revora_test_pk_placement_failure();");
            try {
                bool failed=false;try{Call(schedule,"Advance",run,now);}catch(System.Reflection.TargetInvocationException ex){failed=ex.InnerException is InvalidOperationException;}Require(failed);
                Require((long)Sql("SELECT count(*) FROM revora_guild_pk_placements WHERE contest=@run","run",run)==0);
                Require(Convert.ToString(Sql("SELECT phase FROM revora_guild_pk_weeks WHERE contest=@run","run",run))=="finals");
            }finally{Sql("DROP TRIGGER IF EXISTS revora_test_pk_placement_failure ON revora_guild_pk_placements;DROP FUNCTION IF EXISTS revora_test_pk_placement_failure()");}
            System.Threading.Tasks.Parallel.For(0,8,_=>Call(schedule,"Advance",run,now));
        }
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_results WHERE run=@run AND reason='lead' AND winner=@guild AND first_score=1790 AND second_score=0","run",match,"guild",(long)first.Guild.ID)==1);
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_placements WHERE contest=@run AND match=@match","run",run,"match",match)==2);
        Require(Convert.ToUInt32(Sql("SELECT guild_id FROM revora_guild_pk_placements WHERE contest=@run AND rank=1","run",run))==first.Guild.ID);
        Require(Convert.ToUInt32(Sql("SELECT guild_id FROM revora_guild_pk_placements WHERE contest=@run AND rank=2","run",run))==second.Guild.ID);
        Require(Convert.ToString(Sql("SELECT phase FROM revora_guild_pk_weeks WHERE contest=@run","run",run))=="complete");
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_entries WHERE run=@run AND NOT active","run",match)==2);
        Require((long)Sql(@"SELECT count(*) FROM revora_guild_pk_participation p JOIN revora_guild_pk_entries e ON e.run=p.run AND e.uid=p.uid AND e.admission=p.admission AND e.guild_id=p.guild_id
JOIN revora_guild_pk_entries o ON o.run=p.run AND o.uid=p.opponent AND o.admission=p.opponent_admission AND o.guild_id<>p.guild_id
JOIN revora_guild_pk_matches m ON m.id=p.run WHERE p.run=@run AND p.accepted_at>=m.starts_at AND p.accepted_at<m.ends_at AND p.entered_at=e.entered_at","run",match)==2);
        GuildPkParticipationClaimProbe(reload,first);GuildPkParticipationClaimProbe(reload,second);
        now=begins.AddHours(2);GuildPkPrizeProbe(reload,run,first,now,value=>now=value);
        System.Console.WriteLine("[career-process] guild-pk-played-final-placements-passed");
    }
    static void GuildPkParticipationClaimProbe(bool reload,Revora.Client.GameState actor) {
        Sql("CREATE TABLE IF NOT EXISTS revora_guild_pk_claim_probe(uid bigint PRIMARY KEY,snapshot text NOT NULL,experience numeric NOT NULL,balls integer NOT NULL);ALTER TABLE revora_guild_pk_claim_probe ADD COLUMN IF NOT EXISTS pool_credit numeric NOT NULL DEFAULT 0");
        long uid=actor.Entity.UID;
        if(actor.Entity.Reborn==0 || actor.Entity.Reborn==1 && actor.Entity.Level<101) {
            Require((long)Sql("SELECT count(*) FROM revora_guild_pk_participation WHERE uid=@uid","uid",uid)==1);
            string before=NobilityClaimSnapshot(actor);NobilityClaimPacket(actor,607);Require(before==NobilityClaimSnapshot(actor) && !PirateFenced(actor));
            Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=607","uid",uid)==0);
            System.Console.WriteLine("[career-process] guild-pk-participation-stage-gate-passed uid="+uid);return;
        }
        if((long)Sql("SELECT count(*) FROM revora_guild_pk_claim_probe WHERE uid=@uid","uid",uid)==0) {
            Require(!reload);var old=actor.Inventory.Objects.Select(i=>i.UID).ToArray();
            System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(actor,607));
            Require(!PirateFenced(actor) && actor.Way2Heroes[6].SubStages[607].Done && actor.Way2Heroes[6].SubStages[607].points==1);
            var reward=actor.Inventory.Objects.Single(i=>!old.Contains(i.UID));Require(reward.ID==723700 && reward.Bound && reward.StackSize==1);
            ulong experience=actor.Entity.Experience;byte level=actor.Entity.Level;var balls=actor.ExpBalls;
            SoulClaimUse(actor,reward);Require(!actor.Inventory.Objects.Any(i=>i.UID==reward.UID) && !PirateFenced(actor));
            Require(actor.Entity.Level>level || actor.Entity.Experience>experience);Require(actor.ExpBalls==balls+1);
            Require(EntityTable.SaveEntity(actor));
            Sql("INSERT INTO revora_guild_pk_claim_probe(uid,snapshot,experience,balls,pool_credit) VALUES(@uid,@snapshot,@experience,@balls,COALESCE((SELECT sum(amount) FROM revora_guild_pk_payouts WHERE uid=@uid AND paid_at IS NOT NULL),0))","uid",uid,"snapshot",NobilityClaimSnapshot(actor),"experience",(decimal)actor.Entity.Experience,"balls",(int)actor.ExpBalls);
        }
        Require(actor.Way2Heroes[6].SubStages[607].Done && actor.Way2Heroes[6].SubStages[607].points==1);
        Require(actor.Entity.Experience==Convert.ToUInt64(Sql("SELECT experience FROM revora_guild_pk_claim_probe WHERE uid=@uid","uid",uid)));
        DateTime claimed=((DateTime)Sql("SELECT claimed_at FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=607","uid",uid)).ToLocalTime();
        string exact=NobilityClaimSnapshot(actor);
        if(claimed.Date==DateTime.Today) {
            Require(actor.ExpBalls==Convert.ToInt32(Sql("SELECT balls FROM revora_guild_pk_claim_probe WHERE uid=@uid","uid",uid)));
            Require((bool)Sql("SELECT CAST(@current AS jsonb)=jsonb_set(CAST(snapshot AS jsonb),'{profile,2}',to_jsonb((CAST(snapshot AS jsonb)#>>'{profile,2}')::numeric+COALESCE((SELECT sum(amount) FROM revora_guild_pk_payouts WHERE uid=@uid AND paid_at IS NOT NULL),0)-pool_credit)) FROM revora_guild_pk_claim_probe WHERE uid=@uid","uid",uid,"current",exact));
        }else {
            // Daily resets legitimately reset EXP Ball usage and add Chi. Preserve the
            // historical reward snapshot; validate its unrelated fields and today's replay.
            Require(actor.ExpBalls==0);
            Require((bool)Sql("SELECT (jsonb_set(CAST(snapshot AS jsonb),'{profile,2}',to_jsonb((CAST(snapshot AS jsonb)#>>'{profile,2}')::numeric+COALESCE((SELECT sum(amount) FROM revora_guild_pk_payouts WHERE uid=@uid AND paid_at IS NOT NULL),0)-pool_credit))-'chi')=(CAST(@current AS jsonb)-'chi') FROM revora_guild_pk_claim_probe WHERE uid=@uid","uid",uid,"current",exact));
        }
        NobilityClaimPacket(actor,607);Require(exact==NobilityClaimSnapshot(actor) && !PirateFenced(actor));
        Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=607","uid",uid)==1);
        System.Console.WriteLine("[career-process] guild-pk-participation-claim-passed uid="+uid);
    }
    static void GuildPkPrizeProbe(bool reload,Guid run,Revora.Client.GameState leader,DateTime opens,Action<DateTime> clock) {
        var schedule=GameAssembly.GetType("Revora.Database.GuildPkSchedule",true);uint guild=leader.Guild.ID;
        Func<NpgsqlConnection,NpgsqlTransaction,uint,DateTime,int> claim=(c,t,uid,now)=> {
            object[] args={c,t,run,uid,guild,now,0UL,0U};return (int)schedule.GetMethod("Prize",Hidden).Invoke(null,args);
        };
        Sql("CREATE TABLE IF NOT EXISTS revora_guild_pk_prize_probe(contest uuid PRIMARY KEY,guild_id bigint NOT NULL,silver numeric NOT NULL,cps bigint NOT NULL)");
        if((long)Sql("SELECT count(*) FROM revora_guild_pk_prize_probe WHERE contest=@run","run",run)==0) {
            Require(!reload);decimal silver=Convert.ToDecimal(Sql("SELECT silverfund FROM guilds WHERE id=@guild","guild",(long)guild));long cps=Convert.ToInt64(Sql("SELECT conquerpointfund FROM guilds WHERE id=@guild","guild",(long)guild));
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Require(claim(c,t,4294916902u,opens)==0);Require(claim(c,t,leader.Entity.UID,opens.AddTicks(-1))==0);Require(claim(c,t,leader.Entity.UID,opens.AddMinutes(30))==0);t.Rollback();
            }}
            Sql(@"CREATE OR REPLACE FUNCTION revora_test_pk_prize_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.silverfund>OLD.silverfund THEN RETURN NULL; END IF; RETURN NEW; END $$;
CREATE TRIGGER revora_test_pk_prize_failure BEFORE UPDATE ON guilds FOR EACH ROW EXECUTE FUNCTION revora_test_pk_prize_failure();");
            try {
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    bool failed=false;try{claim(c,t,leader.Entity.UID,opens);}catch(System.Reflection.TargetInvocationException ex){failed=ex.InnerException is InvalidOperationException;}Require(failed);t.Rollback();
                }}
            }finally{Sql("DROP TRIGGER IF EXISTS revora_test_pk_prize_failure ON guilds;DROP FUNCTION IF EXISTS revora_test_pk_prize_failure()");}
            Require((long)Sql("SELECT count(*) FROM revora_guild_pk_prizes WHERE contest=@run","run",run)==0);
            Require(Convert.ToDecimal(Sql("SELECT silverfund FROM guilds WHERE id=@guild","guild",(long)guild))==silver && Convert.ToInt64(Sql("SELECT conquerpointfund FROM guilds WHERE id=@guild","guild",(long)guild))==cps);
            clock(opens.AddTicks(-1));HeartTalk(leader,706072,0);HeartTalk(leader,706072,2);Require((long)Sql("SELECT count(*) FROM revora_guild_pk_prizes WHERE contest=@run","run",run)==0);
            clock(opens);HeartTalk(leader,706072,0);HeartTalk(leader,706072,2);
            System.Console.WriteLine("[guild-pk-prize-probe] officer="+Call(Feature("GuildContestCoordinator"),"OfficerAt",leader,706072u)+" map="+leader.Entity.MapID+" rank="+leader.Entity.GuildRank+" member="+leader.AsMember.Rank+" receipts="+Sql("SELECT count(*) FROM revora_guild_pk_prizes WHERE contest=@run","run",run)+" silver="+leader.Guild.SilverFund+" expected="+(silver+120000000)+" cps="+leader.Guild.ConquerPointFund+" expected-cps="+(cps+3000));
            Require((decimal)leader.Guild.SilverFund==silver+120000000 && leader.Guild.ConquerPointFund==cps+3000);
            Sql("INSERT INTO revora_guild_pk_prize_probe VALUES(@run,@guild,@silver,@cps)","run",run,"guild",(long)guild,"silver",silver+120000000,"cps",cps+3000);
        }
        clock(opens);HeartTalk(leader,706072,0);HeartTalk(leader,706072,2);
        System.Threading.Tasks.Parallel.For(0,8,i=> {using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
            Require(claim(c,t,i%2==0?leader.Entity.UID:4294916901u,opens)==2);t.Commit();
        }}});
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_prizes WHERE contest=@run AND silver=120000000 AND cps=3000 AND rank=1","run",run)==1);
        Require((decimal)leader.Guild.SilverFund==Convert.ToDecimal(Sql("SELECT silver-COALESCE((SELECT sum(amount) FROM revora_guild_pk_funding WHERE guild_id=p.guild_id),0)+COALESCE((SELECT sum(refund) FROM revora_guild_pk_pools WHERE guild_id=p.guild_id),0) FROM revora_guild_pk_prize_probe p WHERE contest=@run","run",run)));
        Require(leader.Guild.ConquerPointFund==Convert.ToInt64(Sql("SELECT cps FROM revora_guild_pk_prize_probe WHERE contest=@run","run",run)));
        Require((long)Sql("SELECT count(*) FROM guilds g JOIN revora_guild_pk_prize_probe p ON p.guild_id=g.id WHERE p.contest=@run AND g.silverfund=p.silver-COALESCE((SELECT sum(amount) FROM revora_guild_pk_funding WHERE guild_id=g.id),0)+COALESCE((SELECT sum(refund) FROM revora_guild_pk_pools WHERE guild_id=g.id),0) AND g.conquerpointfund=p.cps","run",run)==1);
        System.Console.WriteLine("[career-process] guild-pk-treasury-prize-passed");
    }
    static void GuildPkMissedScheduleProbe(bool reload) {
        var schedule=GameAssembly.GetType("Revora.Database.GuildPkSchedule",true);Guid run=new Guid("f6067100-0000-0000-0000-000000000027");
        DateTime local=new DateTime(2056,1,1,20,30,0,DateTimeKind.Local);while(local.DayOfWeek!=DayOfWeek.Monday)local=local.AddDays(1);
        var start=local.ToUniversalTime();
        if((long)Sql("SELECT count(*) FROM revora_guild_contests WHERE id=@run","run",run)==0) {
            Require(!reload);Sql("INSERT INTO revora_guild_contests(id,starts_at,ends_at,state) VALUES(@run,@start,@end,'frozen')","run",run,"start",new DateTimeOffset(start),"end",new DateTimeOffset(start.AddHours(2)));
            for(int i=1;i<=8;i++) {
                Sql("INSERT INTO revora_guild_contest_seeds VALUES(@run,@rank,@guild,@points,@now)","run",run,"rank",i,"guild",64000L+i,"points",100L-i,"now",new DateTimeOffset(start));
                Sql("INSERT INTO revora_guild_contest_owners(run,guild_id) VALUES(@run,@guild)","run",run,"guild",64000L+i);
            }
            Call(schedule,"Create",run);
            Require(!(bool)Call(schedule,"Advance",run,local.AddDays(1).AddTicks(-1).ToUniversalTime()));
            Require((long)Sql("SELECT count(*) FROM revora_guild_pk_matches WHERE contest=@run AND state='scheduled'","run",run)==12);
            Require(!(bool)Call(schedule,"Advance",run,local.AddDays(1).ToUniversalTime()));
            Require((long)Sql("SELECT count(*) FROM revora_guild_pk_matches WHERE contest=@run AND state='cancelled'","run",run)==4);
            Require((bool)Call(schedule,"Advance",run,local.AddDays(3).ToUniversalTime()));
            var final=local.AddDays(5).AddHours(1).ToUniversalTime();Call(schedule,"Advance",run,final.AddTicks(-1));
            Require((long)Sql("SELECT count(*) FROM revora_guild_pk_matches WHERE contest=@run AND round=4 AND state='scheduled'","run",run)==4);
            Call(schedule,"Advance",run,final);
        }
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_matches WHERE contest=@run AND state='cancelled' AND winner IS NULL","run",run)==16);
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_standings WHERE contest=@run AND points=0","run",run)==8);
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_results r JOIN revora_guild_pk_matches m ON m.id=r.run WHERE m.contest=@run","run",run)==0);
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_entries e JOIN revora_guild_pk_matches m ON m.id=e.run WHERE m.contest=@run","run",run)==0);
        Call(schedule,"Advance",run,local.AddDays(5).AddHours(1).ToUniversalTime());
        Require(Convert.ToString(Sql("SELECT phase FROM revora_guild_pk_weeks WHERE contest=@run","run",run))=="complete");
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_placements WHERE contest=@run","run",run)==0);
        System.Console.WriteLine("[career-process] guild-pk-missed-windows-passed");
    }
    static void GuildPkFinalScheduleProbe(bool reload) {
        var schedule=GameAssembly.GetType("Revora.Database.GuildPkSchedule",true);
        Guid run=new Guid("f6067100-0000-0000-0000-000000000026");
        DateTime local=new DateTime(2054,1,1,20,30,0,DateTimeKind.Local);while(local.DayOfWeek!=DayOfWeek.Monday)local=local.AddDays(1);
        DateTime start=local.ToUniversalTime(),now=start.AddDays(3).AddHours(3);
        if((long)Sql("SELECT count(*) FROM revora_guild_contests WHERE id=@run","run",run)==0) {
            Require(!reload);Sql("INSERT INTO revora_guild_contests(id,starts_at,ends_at,state) VALUES(@run,@start,@end,'frozen')","run",run,"start",new DateTimeOffset(start),"end",new DateTimeOffset(start.AddHours(2)));
            for(int i=1;i<=8;i++) {
                Sql("INSERT INTO revora_guild_contest_seeds VALUES(@run,@rank,@guild,@points,@now)","run",run,"rank",i,"guild",64000L+i,"points",100L-i,"now",new DateTimeOffset(start));
                Sql("INSERT INTO revora_guild_contest_owners(run,guild_id) VALUES(@run,@guild)","run",run,"guild",64000L+i);
            }
        }
        Call(schedule,"Create",run);
        if(Convert.ToString(Sql("SELECT phase FROM revora_guild_pk_weeks WHERE contest=@run","run",run))=="groups") {
            Require(!reload);
            // Synthetic result inputs validate bracket persistence, not match earning.
            Sql("UPDATE revora_guild_pk_matches SET state='complete',winner=first_guild,first_score=10,second_score=1 WHERE contest=@run","run",run);
            bool missing=false;try{Call(schedule,"Advance",run,now);}catch(System.Reflection.TargetInvocationException ex){missing=ex.InnerException is InvalidOperationException;}Require(missing);
            Sql(@"INSERT INTO revora_guild_pk_results(run,winner,reason,first_score,second_score,settled_at)
SELECT id,winner,'time',first_score,second_score,ends_at FROM revora_guild_pk_matches WHERE contest=@run ON CONFLICT DO NOTHING","run",run);
            Guid last=(Guid)Sql("SELECT id FROM revora_guild_pk_matches WHERE contest=@run ORDER BY round DESC,id LIMIT 1","run",run);
            Sql("UPDATE revora_guild_pk_matches SET state='active' WHERE id=@id","id",last);
            Require(!(bool)Call(schedule,"Advance",run,now));
            Require((long)Sql("SELECT count(*) FROM revora_guild_pk_standings WHERE contest=@run","run",run)==0);
            Sql("UPDATE revora_guild_pk_matches SET state='complete' WHERE id=@id","id",last);
            Sql(@"CREATE OR REPLACE FUNCTION revora_test_pk_final_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.round=4 THEN RETURN NULL; END IF; RETURN NEW; END $$;
CREATE TRIGGER revora_test_pk_final_failure BEFORE INSERT ON revora_guild_pk_matches FOR EACH ROW EXECUTE FUNCTION revora_test_pk_final_failure();");
            try {
                bool failed=false;try{Call(schedule,"Advance",run,now);}catch(System.Reflection.TargetInvocationException ex){failed=ex.InnerException is InvalidOperationException;}Require(failed);
                Require((long)Sql("SELECT count(*) FROM revora_guild_pk_standings WHERE contest=@run","run",run)==0);
                Require((long)Sql("SELECT count(*) FROM revora_guild_pk_matches WHERE contest=@run AND round=4","run",run)==0);
            }finally{Sql("DROP TRIGGER IF EXISTS revora_test_pk_final_failure ON revora_guild_pk_matches;DROP FUNCTION IF EXISTS revora_test_pk_final_failure()");}
            System.Threading.Tasks.Parallel.For(0,8,_=>Call(schedule,"Advance",run,now));
        }
        Require(Convert.ToString(Sql("SELECT phase FROM revora_guild_pk_weeks WHERE contest=@run","run",run))=="finals");
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_standings WHERE contest=@run","run",run)==8);
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_slots WHERE contest=@run AND round=4","run",run)==8);
        Require((long)Sql(@"SELECT count(*) FROM revora_guild_pk_matches m
JOIN revora_guild_pk_standings a ON a.contest=m.contest AND a.guild_id=m.first_guild AND a.bracket=0 AND a.place=m.bracket+1
JOIN revora_guild_pk_standings b ON b.contest=m.contest AND b.guild_id=m.second_guild AND b.bracket=1 AND b.place=m.bracket+1
WHERE m.contest=@run AND m.round=4 AND m.state='scheduled' AND m.winner IS NULL AND m.starts_at=@start AND m.ends_at=@end",
            "run",run,"start",new DateTimeOffset(local.AddDays(5).AddHours(1).ToUniversalTime()),"end",new DateTimeOffset(local.AddDays(5).AddHours(3).ToUniversalTime()))==4);
        Sql("CREATE TABLE IF NOT EXISTS revora_guild_pk_final_probe(run uuid PRIMARY KEY,snapshot text NOT NULL)");
        string snapshot=Convert.ToString(Sql("SELECT json_agg(row_to_json(m) ORDER BY bracket)::text FROM revora_guild_pk_matches m WHERE contest=@run AND round=4","run",run));
        Sql("INSERT INTO revora_guild_pk_final_probe VALUES(@run,@snapshot) ON CONFLICT DO NOTHING","run",run,"snapshot",snapshot);
        Require(snapshot==Convert.ToString(Sql("SELECT snapshot FROM revora_guild_pk_final_probe WHERE run=@run","run",run)));
        System.Console.WriteLine("[career-process] guild-pk-saved-finals-passed");
    }
    static void GuildPkDefeatProbe(Guid match,Revora.Client.GameState killer,Revora.Client.GameState victim,Revora.Client.GameState reviver,DateTime start,Action<DateTime> clock) {
        var map=victim.Map;uint pk=killer.Entity.PKPoints;var itemCount=victim.Inventory.Objects.Count();
        long money=Convert.ToInt64(Sql("SELECT money FROM entities WHERE uid=@uid","uid",(long)victim.Entity.UID));
        var ledger=GameAssembly.GetType("Revora.Database.GuildPkDefeatTable",true);
        Guid admission=(Guid)Sql("SELECT admission FROM revora_guild_pk_entries WHERE run=@run AND uid=@uid","run",match,"uid",(long)victim.Entity.UID);
        Guid other=(Guid)Sql("SELECT admission FROM revora_guild_pk_entries WHERE run=@run AND uid=@uid","run",match,"uid",(long)killer.Entity.UID);
        int killerPower=killer.Entity.BattlePower,victimPower=victim.Entity.BattlePower;
        int expected= victimPower-killerPower>20?3:victimPower>killerPower?2:1;
        Action<int> hit=number=> {
            clock(start.AddSeconds(40+number*6));killer.Entity.X=50;killer.Entity.Y=59;victim.Entity.X=51;victim.Entity.Y=59;
            victim.Attackable=killer.Attackable=true;victim.Entity.ClearFlags();victim.Entity.Hitpoints=1;killer.Entity.MinAttack=killer.Entity.MaxAttack=killer.Entity.BaseMinAttack=killer.Entity.BaseMaxAttack=10000000;
            Require(Revora.Game.Attacking.Handle.CanAttack(killer.Entity,victim.Entity,null,true));
            var packet=new Attack(true){AttackType=Attack.Melee,Attacker=killer.Entity.UID,Attacked=victim.Entity.UID};
            uint damage=Revora.Game.Attacking.Calculate.Melee(killer.Entity,victim.Entity,ref packet);
            System.Console.WriteLine("[guild-pk-defeat-probe] number="+number+" damage="+damage+" hp="+victim.Entity.Hitpoints);Require(damage>=victim.Entity.Hitpoints);
            Revora.Game.Attacking.Handle.ReceiveAttack(killer.Entity,victim.Entity,packet,ref damage,null);
        };
        for(int i=1;i<=20;i++) {
            if(i==1) {
                Sql(@"CREATE OR REPLACE FUNCTION revora_test_pk_defeat_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;
CREATE TRIGGER revora_test_pk_defeat_failure BEFORE INSERT ON revora_guild_pk_defeats FOR EACH ROW EXECUTE FUNCTION revora_test_pk_defeat_failure();");
                try {
                    hit(i);Require((long)Sql("SELECT count(*) FROM revora_guild_pk_defeats WHERE run=@run","run",match)==0);
                    Require(Convert.ToInt32(Sql("SELECT deaths FROM revora_guild_pk_entries WHERE run=@run AND uid=@uid","run",match,"uid",(long)victim.Entity.UID))==0);
                    Require(victim.Entity.X==51 && victim.Entity.Y==59 && victim.Entity.Hitpoints==1 && !victim.Entity.Dead);
                    GuildPkRejectDamage(killer.Entity,victim.Entity);
                }finally{Sql("DROP TRIGGER IF EXISTS revora_test_pk_defeat_failure ON revora_guild_pk_defeats;DROP FUNCTION IF EXISTS revora_test_pk_defeat_failure()");}
                Call(Feature("GuildPkBanner"),"Pulse",map);
            }else hit(i);
            Require((long)Sql("SELECT count(*) FROM revora_guild_pk_defeats WHERE run=@run","run",match)==i);
            if(i<20) {
                Require(victim.Entity.Dead);
                if(i==1) {
                    clock(start.AddSeconds(47));GuildPkRuneDownedProbe(match,reviver,victim);GuildPkLotusProbe(match,reviver,killer,victim);
                }else {
                    clock(start.AddSeconds(40+i*6+4).AddMilliseconds(999));Call(Feature("GuildPkBanner"),"Pulse",map);Require(victim.Entity.Dead);
                    clock(start.AddSeconds(40+i*6+5));Call(Feature("GuildPkBanner"),"Pulse",map);
                }
            }
            Require(!victim.Entity.Dead && victim.Entity.Hitpoints==victim.Entity.MaxHitpoints);
            if(i<20) {
                object[] camp={match,map,victim.Guild.ID,(ushort)0,(ushort)0};Require((bool)Feature("GuildPkAdmission").GetMethod("Camp",Hidden).Invoke(null,camp));
                Require(ReferenceEquals(victim.Map,map) && victim.Entity.X==(ushort)camp[3] && victim.Entity.Y==(ushort)camp[4]);
            }else Require(victim.Entity.MapID==1002);
            Require((bool)Call(ledger,"Record",match,victim.Entity.UID,killer.Entity.UID,admission,other,i,killerPower,victimPower,start.AddSeconds(40+i*6)));
            Require(!(bool)Call(ledger,"Record",match,victim.Entity.UID,killer.Entity.UID,admission,other,i,killerPower+1,victimPower,start.AddSeconds(40+i*6)));
            Require(Convert.ToInt32(Sql("SELECT sum(exploits) FROM revora_guild_pk_defeats WHERE run=@run","run",match))==i*expected);
            Require((long)Sql("SELECT count(*) FROM revora_guild_pk_defeats WHERE run=@run AND killer_bp=@killer AND victim_bp=@victim","run",match,"killer",killerPower,"victim",victimPower)==i);
        }
        Require(!(bool)Call(ledger,"Record",match,victim.Entity.UID,killer.Entity.UID,admission,other,21,killerPower,victimPower,start.AddSeconds(170)));
        Require(killer.Entity.PKPoints==pk && victim.Inventory.Objects.Count()==itemCount);
        Require(Convert.ToInt64(Sql("SELECT money FROM entities WHERE uid=@uid","uid",(long)victim.Entity.UID))==money);
        System.Console.WriteLine("[career-process] guild-pk-twenty-defeats-passed");
    }
    static void GuildPkFineRainCast(Revora.Client.GameState caster,Revora.Client.GameState target) {
        var spell=new SpellInformation{ID=14500,Power=target.Entity.MaxHitpoints,Damage1=5,Distance=18,UseMana=7,UseStamina=3};
        bool supplied=!caster.Spells.ContainsKey(14500);if(supplied)caster.Spells.Add(14500,new Spell(true){ID=14500,Level=0,Available=true});
        uint master=caster.Entity.MPMaster;
        try {
            caster.Entity.X=(ushort)(target.Entity.X-1);caster.Entity.Y=target.Entity.Y;caster.Entity.Mana=100;caster.Entity.Stamina=100;caster.Entity.MPMaster=0;
            RunesSystem.HandleRedBlueRunesSkills(new Attack(true){AttackType=Attack.Magic},caster.Entity,target.Entity,spell,target.Entity.X,target.Entity.Y);
            Require(caster.Entity.Mana==93 && caster.Entity.Stamina==97);
        }finally{caster.Entity.MPMaster=master;if(supplied)caster.Spells.Remove(14500);}
    }
    static void GuildPkRuneSupportProbe(Revora.Client.GameState caster,Revora.Client.GameState ally,Revora.Client.GameState enemy) {
        ally.Entity.Hitpoints=1;GuildPkFineRainCast(caster,ally);Require(ally.Entity.Hitpoints==ally.Entity.MaxHitpoints);
        enemy.Entity.Hitpoints=1;GuildPkFineRainCast(caster,enemy);Require(enemy.Entity.Hitpoints==1);enemy.Entity.Hitpoints=enemy.Entity.MaxHitpoints;
        int calls=0;Action pulse=()=>calls++;var type=typeof(RunesSystem);var map=caster.Map;var life=caster.Entity.DeathStamp;
        Require((bool)Call(type,"FineRainPulse",caster.Entity,map,life,pulse) && calls==1);
        Require(!(bool)Call(type,"FineRainPulse",caster.Entity,Kernel.Maps[1002],life,pulse) && calls==1);
        Require(!(bool)Call(type,"FineRainPulse",caster.Entity,map,life.AddSeconds(1),pulse) && calls==1);
        Kernel.GamePool.Remove(caster.Entity.UID);
        try{Require(!(bool)Call(type,"FineRainPulse",caster.Entity,map,life,pulse) && calls==1);}
        finally{Kernel.GamePool[caster.Entity.UID]=caster;}
        System.Console.WriteLine("[career-process] guild-pk-rune-heal-cost-context-passed");
    }
    static void GuildPkRuneDownedProbe(Guid match,Revora.Client.GameState caster,Revora.Client.GameState target) {
        Require(target.Entity.Dead && target.Entity.Hitpoints==0);GuildPkFineRainCast(caster,target);
        RunesSystem.HandleRedBlueRunesSkills(new Attack(true){AttackType=Attack.Magic},target.Entity,target.Entity,new SpellInformation{ID=14410,Duration=10},target.Entity.X,target.Entity.Y);
        bool ran=false;Require(!(bool)Call(typeof(RunesSystem),"FineRainPulse",target.Entity,target.Map,target.Entity.DeathStamp,(Action)(()=>ran=true)) && !ran);
        Require(target.Entity.Dead && target.Entity.Hitpoints==0 && (long)Sql("SELECT count(*) FROM revora_guild_pk_revivals WHERE run=@run","run",match)==0);
        System.Console.WriteLine("[career-process] guild-pk-runes-cannot-bypass-downed-passed");
    }
    static void GuildPkPoisonProbe(Guid match,Revora.Client.GameState caster,Revora.Client.GameState ally,Revora.Client.GameState target) {
        var poison=Feature("TeamArenaPoison");var spell=new SpellInformation{ID=10010,PowerPercent=0.5f,Duration=40};
        caster.Entity.X=50;caster.Entity.Y=59;target.Entity.X=51;target.Entity.Y=59;
        Require(!(bool)Call(poison,"Apply",caster.Entity,ally.Entity,spell));
        uint hp=target.Entity.Hitpoints;target.Entity.Hitpoints=1000;
        Require((bool)Call(poison,"Apply",caster.Entity,target.Entity,spell));var stamp=target.Entity.ToxicFogStamp;
        Require((bool)Call(poison,"Tick",target,stamp.AddMilliseconds(1999)) && target.Entity.Hitpoints==1000);
        Require((bool)Call(poison,"Tick",target,stamp.AddSeconds(2)) && target.Entity.Hitpoints==500);
        Require((bool)Call(poison,"Tick",target,stamp.AddSeconds(2)) && target.Entity.Hitpoints==500);
        target.Entity.Hitpoints=1000;target.Entity.AzureShieldDefence=1000;target.Entity.AddFlag((ulong)FlagsUpdate.Flags.AzureShield,20,true);
        Require((bool)Call(poison,"Tick",target,stamp.AddSeconds(4)) && target.Entity.Hitpoints==1000 && target.Entity.AzureShieldDefence==500);
        target.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.AzureShield);target.Entity.AzureShieldDefence=0;target.Entity.ToxicFogPercent=2f;
        Require((bool)Call(poison,"Tick",target,stamp.AddSeconds(6)) && target.Entity.Hitpoints==1 && !target.Entity.Dead);
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_defeats WHERE run=@run","run",match)==0);
        Kernel.GamePool.Remove(caster.Entity.UID);
        try{Require((bool)Call(poison,"Tick",target,stamp.AddSeconds(8)) && target.Entity.ToxicFogLeft==0 && !target.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Poisoned));}
        finally{Kernel.GamePool[caster.Entity.UID]=caster;}
        Require((bool)Call(poison,"Apply",caster.Entity,target.Entity,spell));stamp=target.Entity.ToxicFogStamp;
        Require((bool)Call(poison,"Tick",target,stamp.AddSeconds(41)) && target.Entity.ToxicFogLeft==0);
        target.Entity.Hitpoints=hp;
        System.Console.WriteLine("[career-process] guild-pk-poison-ownership-nonlethal-passed");
    }
    static void GuildPkLotusProbe(Guid match,Revora.Client.GameState caster,Revora.Client.GameState enemy,Revora.Client.GameState target) {
        Action<Revora.Client.GameState> cast=p=> {
            var weapons=p.Weapons;p.Weapons=Tuple.Create(new ConquerItem(true){ID=620005},(ConquerItem)null);
            bool supplied=!p.Spells.ContainsKey(12370);if(supplied)p.Spells.Add(12370,new Spell(true){ID=12370,Level=0,Available=true});
            try {
                p.Entity.X=(ushort)(target.Entity.X-1);p.Entity.Y=target.Entity.Y;p.Screen.Reload(null);p.Screen.Add(target.Entity);
                p.Entity.Mana=10000;p.Entity.Stamina=150;p.Entity.Energy=200;p.Entity.AttackStamp=p.Entity.SpellStamp=Time32.Now.AddSeconds(-10);p.Spells[12370].ColdTime=Time32.Now.AddSeconds(-10);
                new Revora.Game.Attacking.Handle(new Attack(true){AttackType=Attack.Magic,Decoded=true,Damage=12370,Attacker=p.Entity.UID,Attacked=p.Entity.UID,X=target.Entity.X,Y=target.Entity.Y},p.Entity,p.Entity);
                Require(p.Entity.FloorSpells.ContainsKey(12370));var floors=p.Entity.FloorSpells[12370];Require(floors.Spells.Count==1 && p.Entity.Energy==0);
                var stamp=floors.Spells[0].Stamp.AddMilliseconds(1);
                var wrapper=typeof(Revora.Network.Sockets.ClientWrapper);var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                wrapper.GetField("SendQueue",flags).SetValue(p.Socket,new System.Collections.Generic.Queue<byte[]>());wrapper.GetField("SendSyncRoot",flags).SetValue(p.Socket,new object());p.Socket.Alive=true;
                try{Call(typeof(Screen),"FloorSpellsCallBack",p,unchecked((int)stamp.Value));}finally{p.Socket.Alive=false;}
                Require(!p.Entity.FloorSpells.ContainsKey(12370));
            }finally{p.Weapons=weapons;if(supplied)p.Spells.Remove(12370);}
        };
        caster.Entity.X=(ushort)(target.Entity.X+1);caster.Entity.Y=target.Entity.Y;caster.Entity.Hitpoints=caster.Entity.MaxHitpoints-7;uint injured=caster.Entity.Hitpoints;
        cast(enemy);Require(caster.Entity.Hitpoints==injured);Require(target.Entity.Dead && (long)Sql("SELECT count(*) FROM revora_guild_pk_revivals WHERE run=@run","run",match)==0);
        Sql(@"CREATE OR REPLACE FUNCTION revora_test_pk_revival_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;
CREATE TRIGGER revora_test_pk_revival_failure BEFORE INSERT ON revora_guild_pk_revivals FOR EACH ROW EXECUTE FUNCTION revora_test_pk_revival_failure();");
        try{cast(caster);Require(target.Entity.Dead && (long)Sql("SELECT count(*) FROM revora_guild_pk_revivals WHERE run=@run","run",match)==0);}
        finally{Sql("DROP TRIGGER IF EXISTS revora_test_pk_revival_failure ON revora_guild_pk_revivals;DROP FUNCTION IF EXISTS revora_test_pk_revival_failure()");}
        Require((bool)Call(Feature("PlayerRevival"),"HandleLotus",caster.Entity,target.Entity,(ushort)1002) && target.Entity.Dead);
        cast(caster);Require(!target.Entity.Dead && target.Entity.Hitpoints==target.Entity.MaxHitpoints && !PirateFenced(caster));
        cast(caster);Require((long)Sql("SELECT count(*) FROM revora_guild_pk_revivals WHERE run=@run AND reviver=@uid","run",match,"uid",(long)caster.Entity.UID)==1);
        Require(Convert.ToInt64(Sql("SELECT exploits FROM revora_guild_pk_revival_contributions WHERE run=@run AND uid=@uid","run",match,"uid",(long)caster.Entity.UID))==10);
        System.Console.WriteLine("[career-process] guild-pk-actual-lotus-floor-revival-passed");
    }
    static void GuildPkRevivalProbe(Guid match,Revora.Client.GameState caster,Revora.Client.GameState enemy,Revora.Client.GameState target) {
        // Revival skill availability is a supplied combat prerequisite, not an earned training claim.
        bool grantXp=false;Require(SpellTable.SpellInformations[1050][0].NeedXP==1);
        Action<Revora.Client.GameState> cast=p=>{
            bool supplied=!p.Spells.ContainsKey(1050);if(supplied)p.Spells.Add(1050,new Spell(true){ID=1050,Level=0,Available=true});
            try {
                p.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.XPList);
                if(grantXp)p.Entity.AddFlag((ulong)FlagsUpdate.Flags.XPList,20,true);
                bool wasDead=target.Entity.Dead;
                p.Entity.X=(ushort)(target.Entity.X-1);p.Entity.Y=target.Entity.Y;p.Screen.Reload(null);
                p.Entity.Mana=10000;p.Entity.Stamina=150;p.Entity.AttackStamp=p.Entity.SpellStamp=Time32.Now.AddSeconds(-10);p.Spells[1050].ColdTime=Time32.Now.AddSeconds(-10);
                Entity seen;using(var probe=(IDisposable)Call(Feature("PlayerRevival"),"Acquire",p.Entity,target.Entity))System.Console.WriteLine("[guild-pk-revive-probe] caster="+p.Entity.UID+" lease="+(probe!=null)+" screen="+p.Screen.TryGetValue(target.Entity.UID,out seen)+" mana="+p.Entity.Mana+" sort="+SpellTable.SpellInformations[1050][0].Sort+" distance="+SpellTable.SpellInformations[1050][0].Distance);
                new Revora.Game.Attacking.Handle(new Attack(true){AttackType=Attack.Magic,Decoded=true,Damage=1050,Attacker=p.Entity.UID,Attacked=target.Entity.UID,X=target.Entity.X,Y=target.Entity.Y},p.Entity,target.Entity);
                if(wasDead && grantXp)Require(p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.XPList)==target.Entity.Dead);
            }finally{p.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.XPList);if(supplied)p.Spells.Remove(1050);}
        };
        cast(caster);Require(target.Entity.Dead && (long)Sql("SELECT count(*) FROM revora_guild_pk_revivals WHERE run=@run","run",match)==0);
        grantXp=true;cast(enemy);Require(target.Entity.Dead);
        Sql(@"CREATE OR REPLACE FUNCTION revora_test_pk_revival_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;
CREATE TRIGGER revora_test_pk_revival_failure BEFORE INSERT ON revora_guild_pk_revivals FOR EACH ROW EXECUTE FUNCTION revora_test_pk_revival_failure();");
        try{cast(caster);Require(target.Entity.Dead && (long)Sql("SELECT count(*) FROM revora_guild_pk_revivals WHERE run=@run","run",match)==0);}
        finally{Sql("DROP TRIGGER IF EXISTS revora_test_pk_revival_failure ON revora_guild_pk_revivals;DROP FUNCTION IF EXISTS revora_test_pk_revival_failure()");}
        cast(caster);Require(!target.Entity.Dead && target.Entity.Hitpoints==target.Entity.MaxHitpoints && !PirateFenced(caster));
        cast(caster);Require((long)Sql("SELECT count(*) FROM revora_guild_pk_revivals WHERE run=@run AND reviver=@uid","run",match,"uid",(long)caster.Entity.UID)==1);
        var ledger=GameAssembly.GetType("Revora.Database.GuildPkDefeatTable",true);
        Guid admission=(Guid)Sql("SELECT admission FROM revora_guild_pk_entries WHERE run=@run AND uid=@uid","run",match,"uid",(long)caster.Entity.UID);
        DateTime at=((DateTime)Sql("SELECT revived_at FROM revora_guild_pk_revivals WHERE run=@run AND uid=@uid AND number=1","run",match,"uid",(long)target.Entity.UID)).ToUniversalTime();
        System.Threading.Tasks.Parallel.For(0,8,_=>Require((bool)Call(ledger,"Restore",match,target.Entity.UID,1,caster.Entity.UID,admission,at)));
        Require(!(bool)Call(ledger,"Restore",match,target.Entity.UID,1,enemy.Entity.UID,admission,at));
        Require(!(bool)Call(ledger,"Restore",match,target.Entity.UID,1,caster.Entity.UID,Guid.NewGuid(),at));
        Require(Convert.ToInt64(Sql("SELECT exploits FROM revora_guild_pk_revival_contributions WHERE run=@run AND uid=@uid","run",match,"uid",(long)caster.Entity.UID))==10);
        System.Console.WriteLine("[career-process] guild-pk-actual-xp-revive-spell-passed");
    }
    static void GuildPkRejectDamage(Entity a,Entity b) {
        Require(!Revora.Game.Attacking.Handle.CanAttack(a,b,null,true));uint hp=b.Hitpoints,damage=100;
        Revora.Game.Attacking.Handle.ReceiveAttack(a,b,new Attack(true){AttackType=Attack.Melee,Attacker=a.UID,Attacked=b.UID},ref damage,null);
        Require(damage==0 && b.Hitpoints==hp);
    }
    static void GuildPkPlayerCombatProbe(Revora.Client.GameState first,Revora.Client.GameState ally,Revora.Client.GameState rival) {
        first.Entity.X=50;first.Entity.Y=59;rival.Entity.X=51;rival.Entity.Y=59;
        GuildPkRejectDamage(first.Entity,ally.Entity);
        nextUid=4294916802;var outsider=Player(Kernel.Maps[1002],true);outsider.Attackable=true;outsider.Entity.PKMode=Enums.PKMode.PK;
        GuildPkRejectDamage(first.Entity,outsider.Entity);GuildPkRejectDamage(outsider.Entity,first.Entity);
        outsider.Entity.Teleport(first.Map.BaseID,first.Map.ID,52,59);Require(ReferenceEquals(outsider.Map,first.Map));
        GuildPkRejectDamage(first.Entity,outsider.Entity);GuildPkRejectDamage(outsider.Entity,first.Entity);
        outsider.Entity.Teleport(1002,327,303);Require(outsider.Entity.MapID==1002);
        Require(Revora.Game.Attacking.Handle.CanAttack(first.Entity,rival.Entity,null,true));
        uint hp=rival.Entity.Hitpoints;Require(hp>100);
        first.Entity.MinAttack=first.Entity.MaxAttack=first.Entity.BaseMinAttack=first.Entity.BaseMaxAttack=1000;
        var packet=new Attack(true){AttackType=Attack.Melee,Attacker=first.Entity.UID,Attacked=rival.Entity.UID};
        uint damage=Revora.Game.Attacking.Calculate.Melee(first.Entity,rival.Entity,ref packet);
        Require(damage>0 && damage<hp);Revora.Game.Attacking.Handle.ReceiveAttack(first.Entity,rival.Entity,packet,ref damage,null);
        Require(rival.Entity.Hitpoints<hp && !rival.Entity.Dead);
        System.Console.WriteLine("[career-process] guild-pk-opposing-player-combat-passed");
    }
    static void GuildPkBannerCombatProbe(Guid match,Revora.Client.GameState first,Revora.Client.GameState second,DateTime start,Action<DateTime> clock) {
        var banner=(SobNpcSpawn)first.Map.Npcs[706074];SobNpcSpawn visible;
        Require(first.Screen.TryGetSob(706074,out visible) && ReferenceEquals(visible,banner));
        Require(banner.Hitpoints==1000000 && banner.MaxHitpoints==1000000);
        Action<Revora.Client.GameState> approach=p=>{p.Entity.X=(ushort)(banner.X-1);p.Entity.Y=banner.Y;p.Screen.Reload(null);Require(p.Screen.TryGetSob(706074,out visible) && ReferenceEquals(visible,banner));};
        Action<Revora.Client.GameState,bool> hit=(p,lethal)=>{
            approach(p);Require(Revora.Game.Attacking.Handle.CanAttack(p.Entity,banner,null));
            p.Entity.MinAttack=p.Entity.MaxAttack=p.Entity.BaseMinAttack=p.Entity.BaseMaxAttack=lethal?10000000u:1000u;
            var packet=new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=banner.UID};
            uint damage=Revora.Game.Attacking.Calculate.Melee(p.Entity,banner,ref packet);
            Require(lethal?damage>=banner.Hitpoints:damage>0 && damage<banner.Hitpoints);
            Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,banner,packet,damage,null);
        };
        Sql(@"CREATE OR REPLACE FUNCTION revora_test_pk_hit_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;
CREATE TRIGGER revora_test_pk_hit_failure BEFORE INSERT ON revora_guild_pk_banner_hits FOR EACH ROW EXECUTE FUNCTION revora_test_pk_hit_failure();");
        try {hit(first,false);Require(banner.Hitpoints==1000000);Require((long)Sql("SELECT count(*) FROM revora_guild_pk_banner_hits WHERE run=@run","run",match)==0);}
        finally{Sql("DROP TRIGGER IF EXISTS revora_test_pk_hit_failure ON revora_guild_pk_banner_hits;DROP FUNCTION IF EXISTS revora_test_pk_hit_failure()");}
        hit(first,false);Require(banner.Hitpoints<1000000 && banner.Hitpoints>0);
        Require(Convert.ToUInt32(Sql("SELECT hitpoints FROM revora_guild_pk_banners WHERE run=@run","run",match))==banner.Hitpoints);
        Require(Convert.ToUInt32(Sql("SELECT damage FROM revora_guild_pk_banner_hits WHERE run=@run AND number=1","run",match))==1000000-banner.Hitpoints);
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_captures WHERE run=@run","run",match)==0);
        var copy=new SobNpcSpawn{UID=706074,MapID=banner.MapID,X=banner.X,Y=banner.Y,Hitpoints=1000000};
        Require(!Revora.Game.Attacking.Handle.CanAttack(first.Entity,copy,null));uint saved=banner.Hitpoints;
        Revora.Game.Attacking.Handle.ReceiveAttack(first.Entity,copy,new Attack(true){AttackType=Attack.Melee},1000000,null);Require(banner.Hitpoints==saved);
        ushort neutralX=banner.X,neutralY=banner.Y;clock(start.AddSeconds(10));hit(first,true);
        Require(banner.Hitpoints==1000000 && (banner.X!=neutralX || banner.Y!=neutralY));
        approach(first);Require(!Revora.Game.Attacking.Handle.CanAttack(first.Entity,banner,null));
        Require(first.Screen.TryGetSob(706074,out visible) && ReferenceEquals(visible,banner));
        approach(first);second.Entity.X=(ushort)(banner.X+8);second.Entity.Y=banner.Y;
        Require(!(bool)Call(Feature("TeamArenaPoison"),"Apply",second.Entity,first.Entity,new SpellInformation{ID=10010,PowerPercent=0.5f,Duration=40}));
        GuildPkRejectDamage(second.Entity,first.Entity);GuildPkRejectDamage(first.Entity,second.Entity);
        clock(start.AddSeconds(25));hit(second,true);approach(second);Require(!Revora.Game.Attacking.Handle.CanAttack(second.Entity,banner,null));
        clock(start.AddSeconds(35));saved=banner.Hitpoints;ushort savedX=banner.X,savedY=banner.Y;
        Sql(@"CREATE OR REPLACE FUNCTION revora_test_pk_capture_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;
CREATE TRIGGER revora_test_pk_capture_failure BEFORE INSERT ON revora_guild_pk_captures FOR EACH ROW EXECUTE FUNCTION revora_test_pk_capture_failure();");
        try {
            hit(first,true);Require(banner.Hitpoints==saved && banner.X==savedX && banner.Y==savedY);
            Require(Convert.ToInt32(Sql("SELECT capture FROM revora_guild_pk_banners WHERE run=@run","run",match))==2);
            Require(Convert.ToUInt32(Sql("SELECT keeper FROM revora_guild_pk_banners WHERE run=@run","run",match))==second.Guild.ID);
        }finally{Sql("DROP TRIGGER IF EXISTS revora_test_pk_capture_failure ON revora_guild_pk_captures;DROP FUNCTION IF EXISTS revora_test_pk_capture_failure()");}
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_banner_hits WHERE run=@run","run",match)==3);
        Sql(@"CREATE OR REPLACE FUNCTION revora_test_pk_hit_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;
CREATE TRIGGER revora_test_pk_hit_failure BEFORE INSERT ON revora_guild_pk_banner_hits FOR EACH ROW EXECUTE FUNCTION revora_test_pk_hit_failure();");
        try {hit(first,true);Require(banner.Hitpoints==saved);Require(Convert.ToInt32(Sql("SELECT capture FROM revora_guild_pk_banners WHERE run=@run","run",match))==2);}
        finally{Sql("DROP TRIGGER IF EXISTS revora_test_pk_hit_failure ON revora_guild_pk_banner_hits;DROP FUNCTION IF EXISTS revora_test_pk_hit_failure()");}
        hit(first,true);Require(banner.Hitpoints==1000000);
        var ledger=GameAssembly.GetType("Revora.Database.GuildPkBannerTable",true);
        Guid admission=(Guid)Sql("SELECT admission FROM revora_guild_pk_entries WHERE run=@run AND uid=@uid","run",match,"uid",(long)first.Entity.UID);
        System.Threading.Tasks.Parallel.For(0,8,_=>Require((bool)Call(ledger,"Strike",match,4L,first.Entity.UID,first.Guild.ID,admission,1000000u,1000000u,start.AddSeconds(35))));
        Require(!(bool)Call(ledger,"Strike",match,4L,first.Entity.UID,first.Guild.ID,admission,999999u,1000000u,start.AddSeconds(35)));
        Require(!(bool)Call(ledger,"Strike",match,5L,first.Entity.UID,first.Guild.ID,admission,1000000u,1000000u,start.AddSeconds(36)));
        Require(Convert.ToUInt32(Sql("SELECT keeper FROM revora_guild_pk_banners WHERE run=@run","run",match))==first.Guild.ID);
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_captures WHERE run=@run","run",match)==3);
        System.Console.WriteLine("[career-process] guild-pk-banner-calculated-combat-passed");
    }
    static void GuildPkBannerLedgerProbe(Guid match,Revora.Client.GameState first,Revora.Client.GameState second,DateTime start) {
        var ledger=GameAssembly.GetType("Revora.Database.GuildPkBannerTable",true);Call(ledger,"Ensure",match);
        Guid a=(Guid)Sql("SELECT admission FROM revora_guild_pk_entries WHERE run=@run AND uid=@uid","run",match,"uid",(long)first.Entity.UID);
        Guid b=(Guid)Sql("SELECT admission FROM revora_guild_pk_entries WHERE run=@run AND uid=@uid","run",match,"uid",(long)second.Entity.UID);
        Func<int,Revora.Client.GameState,Guid,int,bool> capture=(number,p,admission,seconds)=>(bool)Call(ledger,"Capture",match,number,p.Entity.UID,p.Guild.ID,admission,start.AddSeconds(seconds));
        Require(!capture(1,first,a,-1) && !capture(1,first,Guid.NewGuid(),10));
        System.Threading.Tasks.Parallel.For(0,8,_=>Require(capture(1,first,a,10)));
        Require(!capture(1,second,b,10) && !capture(2,first,a,11));
        Call(ledger,"Pulse",match,start.AddSeconds(20));
        Require((long)Sql("SELECT first_score FROM revora_guild_pk_matches WHERE id=@run","run",match)==10);
        Require(capture(2,second,b,25));Call(ledger,"Pulse",match,start.AddSeconds(32));Call(ledger,"Pulse",match,start.AddSeconds(32));
        Require((long)Sql("SELECT first_score FROM revora_guild_pk_matches WHERE id=@run","run",match)==15);
        Require((long)Sql("SELECT second_score FROM revora_guild_pk_matches WHERE id=@run","run",match)==7);
        Sql(@"CREATE OR REPLACE FUNCTION revora_test_pk_capture_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;
CREATE TRIGGER revora_test_pk_capture_failure BEFORE INSERT ON revora_guild_pk_captures FOR EACH ROW EXECUTE FUNCTION revora_test_pk_capture_failure();");
        try {
            bool failed=false;try{capture(3,first,a,35);}catch(System.Reflection.TargetInvocationException ex){failed=ex.InnerException is InvalidOperationException;}
            Require(failed && (long)Sql("SELECT second_score FROM revora_guild_pk_matches WHERE id=@run","run",match)==7);
            Require(Convert.ToInt32(Sql("SELECT capture FROM revora_guild_pk_banners WHERE run=@run","run",match))==2);
        }finally{Sql("DROP TRIGGER IF EXISTS revora_test_pk_capture_failure ON revora_guild_pk_captures;DROP FUNCTION IF EXISTS revora_test_pk_capture_failure()");}
        Require(capture(3,first,a,35));
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_captures WHERE run=@run","run",match)==3);
        Require((long)Sql("SELECT second_score FROM revora_guild_pk_matches WHERE id=@run","run",match)==10);
    }
    static void GuildPkEntryProcess(bool reload) {
        var admission=Feature("GuildPkAdmission");var schedule=GameAssembly.GetType("Revora.Database.GuildPkSchedule",true);
        var ledger=GameAssembly.GetType("Revora.Database.GuildContestTable",true);
        Guid qualifier=new Guid("f6067100-0000-0000-0000-000000000014");
        DateTime local=new DateTime(2051,1,8,20,30,0,DateTimeKind.Local);while(local.DayOfWeek!=DayOfWeek.Monday)local=local.AddDays(1);
        var start=local.ToUniversalTime();DateTime now=start;
        admission.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        nextUid=4294916801;var p=Player(Kernel.Maps[1002],true);nextUid=4294916902;var pending=Player(Kernel.Maps[1002],true);
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && p.Entity.MapID==1002 && pending.Entity.MapID==1002);
        uint rival=Convert.ToUInt32(Sql("SELECT guildid FROM entities WHERE uid=4294912301"));
        uint middle=Kernel.Guilds.Keys.First(g=>g!=p.Guild.ID && g!=rival);
        if((long)Sql("SELECT count(*) FROM revora_guild_contests WHERE id=@run","run",qualifier)==0) {
            Require(!reload);
            Sql("INSERT INTO revora_guild_contests(id,starts_at,ends_at,state) VALUES(@run,@start,@end,'frozen')","run",qualifier,"start",new DateTimeOffset(start),"end",new DateTimeOffset(start.AddHours(2)));
            uint[] seeds={p.Guild.ID,middle,rival};
            for(int i=0;i<3;i++) {
                Sql("INSERT INTO revora_guild_contest_seeds VALUES(@run,@rank,@guild,@points,@now)","run",qualifier,"rank",i+1,"guild",(long)seeds[i],"points",3L-i,"now",new DateTimeOffset(start));
                BindContestFixtureGuild(ledger,qualifier,seeds[i]);
            }
        }
        Call(schedule,"Create",qualifier);
        Guid match=(Guid)Sql("SELECT id FROM revora_guild_pk_matches WHERE contest=@run","run",qualifier);
        var begins=new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc).AddSeconds(Convert.ToInt64(Sql("SELECT extract(epoch from starts_at)::bigint FROM revora_guild_pk_matches WHERE id=@run","run",match)));
        Func<uint,string> state=uid=>Convert.ToString(Sql("SELECT state FROM revora_guild_pk_transfers WHERE run=@run AND uid=@uid","run",match,"uid",(long)uid));
        if(state(p.Entity.UID)=="") {
            Require(!reload);var map=Kernel.Maps[700].MakeDynamicMap();
            now=begins.AddMinutes(-6);Require(!(bool)Call(admission,"BindMap",match,map));
            now=begins.AddMinutes(-2);Require((bool)Call(admission,"BindMap",match,map));
            Require((bool)Call(admission,"BindMap",match,map));
            var unrelated=Kernel.Maps[700].MakeDynamicMap();Require(!(bool)Call(admission,"Enter",p,match,unrelated));unrelated.Dispose();
            // Exercise the exact seven-day boundary with a scoped private timestamp;
            // the ordinary character's real admission identity/time is restored.
            long joinedTicks=Convert.ToInt64(Sql("SELECT (extract(epoch from joined_at)*1000000)::bigint FROM revora_guild_admissions WHERE uid=@uid","uid",(long)p.Entity.UID));
            try {
                Sql("UPDATE revora_guild_admissions SET joined_at=@joined WHERE uid=@uid","joined",new DateTimeOffset(now.AddDays(-7).AddMilliseconds(1)),"uid",(long)p.Entity.UID);
                Require(!(bool)Call(admission,"Enter",p,match,map) && state(p.Entity.UID)=="");
                Sql("UPDATE revora_guild_admissions SET joined_at=@joined WHERE uid=@uid","joined",new DateTimeOffset(now.AddDays(-7)),"uid",(long)p.Entity.UID);
                Require((bool)Call(admission,"Enter",p,match,map) && ReferenceEquals(p.Map,map) && state(p.Entity.UID)=="entered");
            }finally{Sql("UPDATE revora_guild_admissions SET joined_at=TIMESTAMPTZ 'epoch'+@micros*INTERVAL '1 microsecond' WHERE uid=@uid","micros",joinedTicks,"uid",(long)p.Entity.UID);}
            Require(joinedTicks==Convert.ToInt64(Sql("SELECT (extract(epoch from joined_at)*1000000)::bigint FROM revora_guild_admissions WHERE uid=@uid","uid",(long)p.Entity.UID)));
            Require(!(bool)Call(admission,"Enter",p,match,map));
            var sessions=Feature("SessionInstall");Require((bool)Call(sessions,"TryEnter",p.Entity.UID));
            try{lock(p.ItemSyncRoot)Call(admission,"Leave",p,match);}finally{Call(sessions,"Exit",p.Entity.UID);}
            Require(p.Entity.MapID==1002 && state(p.Entity.UID)=="returned");
            Require((bool)Call(sessions,"TryEnter",pending.Entity.UID));
            try{lock(pending.ItemSyncRoot) {
                Require((bool)Call(admission,"Reserve",pending,match,map));
                Require(!(bool)Call(admission,"Confirm",pending,match,map)); // Reservation while still in town is not entry.
                pending.Entity.Teleport(700,map.ID,40,40);Require(ReferenceEquals(pending.Map,map));
                Guid original=(Guid)Sql("SELECT admission FROM revora_guild_admissions WHERE uid=@uid","uid",(long)pending.Entity.UID);
                try {
                    Sql("UPDATE revora_guild_admissions SET admission=@admission WHERE uid=@uid","admission",Guid.NewGuid(),"uid",(long)pending.Entity.UID);
                    Require(!(bool)Call(admission,"Confirm",pending,match,map));
                }finally{Sql("UPDATE revora_guild_admissions SET admission=@admission WHERE uid=@uid","admission",original,"uid",(long)pending.Entity.UID);}
            }}finally{Call(sessions,"Exit",pending.Entity.UID);}
            Require(state(pending.Entity.UID)=="reserved"); // Leave for cold recovery.
        } else {
            Require(state(p.Entity.UID)=="returned" && state(pending.Entity.UID)=="returned");
            Require(Convert.ToString(Sql("SELECT state FROM revora_guild_pk_matches WHERE id=@run","run",match))=="cancelled");
        }
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_entries WHERE run=@run AND uid=@uid AND NOT active","run",match,"uid",(long)p.Entity.UID)==1);
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_entries WHERE run=@run AND uid=@uid","run",match,"uid",(long)pending.Entity.UID)==0);
        System.Console.WriteLine("[career-process] career-guild-pk-entry-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Guild PK entry fixture did not close.");
    }
    static bool guildCoordinatorFixture;
    static void GuildCoordinatorProcess(bool reload) {
        var coordinator=Feature("GuildContestCoordinator");
        lock(coordinator.GetField("Gate",Hidden).GetValue(null)) {
        DateTime local=new DateTime(2039,3,1,20,25,0,DateTimeKind.Local);while(local.DayOfWeek!=DayOfWeek.Monday)local=local.AddDays(1);
        DateTime now=local.ToUniversalTime(),start=now.AddMinutes(5);coordinator.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        Call(coordinator,"Activate");
        var timerMap=Kernel.Maps[700].MakeDynamicMap();var timerField=typeof(Map).GetField("Timer",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
        var ensureTimer=typeof(Map).GetMethod("EnsureMonsterTimer",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
        ensureTimer.Invoke(timerMap,null);Require(timerField.GetValue(timerMap)!=null);timerMap.Dispose();
        Require(timerField.GetValue(timerMap)==null);ensureTimer.Invoke(timerMap,null);Require(timerField.GetValue(timerMap)==null);
        if((bool)Sql("SELECT to_regclass('revora_guild_coordinator_v4_probe') IS NULL")) {
            Require(!reload);guildCoordinatorFixture=true;nextUid=4294916901;
            for(int i=0;i<3;i++){uint uid=4294916901u+(uint)i;if((long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==0){nextUid=uid;var p=Player(Kernel.Maps[1002]);Sql("UPDATE entities SET level=@level WHERE uid=@uid","level",new[]{99,119,129}[i],"uid",(long)p.Entity.UID);}}
            Sql("CREATE TABLE revora_guild_coordinator_v4_probe(run uuid PRIMARY KEY,points bigint NOT NULL)");
        }
        if((long)Sql("SELECT count(*) FROM revora_guild_coordinator_v4_probe")==0) {
            Require(!reload);
            for(int tries=0;(long)Sql("SELECT count(*) FROM revora_guild_contests WHERE starts_at=@start","start",new DateTimeOffset(start))!=0;tries++){Require(tries<12);local=local.AddDays(7);now=local.ToUniversalTime();start=now.AddMinutes(5);}
            object[] window={now,DateTime.MinValue,DateTime.MinValue};Require((bool)coordinator.GetMethod("Window",Hidden).Invoke(null,window) && (DateTime)window[1]==start);
            guildTenureFixture=true;nextUid=4294916801;var leader=Player(Kernel.Maps[1002],true);var offline=Player(Kernel.Maps[1002],true);
            nextUid=4294916901;var players=new[]{Player(Kernel.Maps[1002],true),Player(Kernel.Maps[1002],true),Player(Kernel.Maps[1002],true),leader};
            Call(coordinator,"Tick");
            foreach(var p in players.Concat(new[]{offline})){SubClassTable.Load(p.Entity);ChiTable.Load(p);WayofHeroes.Load(p);Revora.Database.KnownPersons.LoadPartner(p);Revora.Database.KnownPersons.LoaderFriends(p);Revora.Database.KnownPersons.LoadMentor(p);Revora.Database.KnownPersons.LoadEnemy(p);p.LoadItemStats();HeartTalk(p,706070,0);}
            foreach(var p in players.Take(3))if(p.Guild==null){GuildClaimPacket(leader,GuildCommand.InviteRequest,p.Entity.UID);GuildClaimPacket(p,GuildCommand.JoinRequest,leader.Entity.UID);Require(ReferenceEquals(p.Guild,leader.Guild));}
            foreach(var p in players.Concat(new[]{offline})){HeartTalk(p,706070,1);Require(p.Map.ID!=1002 && p.Map.BaseID==700);}
            Guid run=(Guid)Sql("SELECT id FROM revora_guild_contests WHERE starts_at=@start","start",new DateTimeOffset(start));
            var maps=players.Select(p=>p.Map).ToArray();Require(maps.Select(m=>m.ID).Distinct().Count()==4);
            for(int i=0;i<4;i++){now=start.AddSeconds(i*2);Call(coordinator,"Tick");}
            foreach(var map in maps)Require(map.Entities.Values.Count(m=>!m.Dead)==16);
            foreach(var p in players) {
                var monster=p.Map.Entities.Values.First(m=>!m.Dead);p.Entity.X=monster.X;p.Entity.Y=monster.Y;p.Screen.Add(monster);
                p.Entity.MinAttack=p.Entity.MaxAttack=p.Entity.BaseMinAttack=p.Entity.BaseMaxAttack=10000000;
                var attack=new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=monster.UID};uint damage=Revora.Game.Attacking.Calculate.Melee(p.Entity,monster,ref attack);Require(damage>=monster.Hitpoints);
                Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,monster,attack,ref damage,null);Require(monster.Dead);
            }
            Require((long)Sql("SELECT count(*) FROM revora_guild_contest_kills WHERE run=@run","run",run)==4);
            var liveLines=(string[])Call(coordinator,"ScoreLines",run,start.AddSeconds(10),start.AddHours(2));
            Require(liveLines.Length==2 && liveLines[0]=="Guild Contest - 120 min remaining" && liveLines[1].EndsWith(" - 4 points"));

            now=start.AddSeconds(10);Call(coordinator,"Tick");foreach(var map in maps)Require(map.Entities.Values.Count(m=>!m.Dead)==16);
            Kernel.GamePool.Remove(offline.Entity.UID);
            now=start.AddHours(2);Call(coordinator,"Tick");
            foreach(var p in players)Require(p.Entity.MapID==1002);
            Require((long)Sql("SELECT count(*) FROM revora_guild_contest_transfers WHERE run=@run AND state<>'returned'","run",run)==0);
            Require(Convert.ToInt32(Sql("SELECT mapid FROM entities WHERE uid=@uid","uid",(long)offline.Entity.UID))==1002);
            foreach(var map in maps)Require(!Kernel.Maps.ContainsKey(map.ID) && typeof(Map).GetField("Timer",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(map)==null);
            Require((long)Sql("SELECT points FROM revora_guild_contest_seeds WHERE run=@run AND rank=1","run",run)==4);
            Sql("INSERT INTO revora_guild_coordinator_v4_probe VALUES(@run,4)","run",run);
        }
        Require((long)Sql("SELECT count(*) FROM revora_guild_coordinator_v4_probe p JOIN revora_guild_contests r ON r.id=p.run JOIN revora_guild_contest_seeds s ON s.run=r.id AND s.rank=1 WHERE r.state='frozen' AND s.points=p.points")==1);
        Guid frozenRun=(Guid)Sql("SELECT run FROM revora_guild_coordinator_v4_probe");
        var frozenLines=(string[])Call(coordinator,"ScoreLines",frozenRun,start.AddHours(2),start.AddHours(2));
        Require(frozenLines.Length==2 && frozenLines[0]=="Guild Contest - 0 min remaining" && frozenLines[1].EndsWith(" - 4 points"));
        var ledger=GameAssembly.GetType("Revora.Database.GuildContestTable",true);
        Guid cancelled=new Guid("f6067100-0000-0000-0000-000000000010");
        Sql("INSERT INTO revora_guild_contests(id,starts_at,ends_at,state) VALUES(@run,'2042-01-06 20:30 UTC','2042-01-06 22:30 UTC','cancelled') ON CONFLICT(id) DO NOTHING","run",cancelled);
        bool refused=false;try {Call(ledger,"Freeze",cancelled,new DateTime(2043,1,1,0,0,0,DateTimeKind.Utc));}
        catch(System.Reflection.TargetInvocationException ex){refused=ex.InnerException is InvalidOperationException && ex.InnerException.Message=="Guild contest was cancelled.";}
        Require(refused && ((Array)Call(ledger,"Scores",cancelled)).Length==0);
        Require((long)Sql("SELECT count(*) FROM revora_guild_contest_seeds WHERE run=@run","run",cancelled)==0);
        now=new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc).AddSeconds(Convert.ToInt64(Sql("SELECT extract(epoch from ends_at)::bigint FROM revora_guild_contests WHERE id=@run","run",frozenRun)));
        Func<uint,Revora.Client.GameState> restore=uid=> {
            Revora.Client.GameState p;if(Kernel.GamePool.TryGetValue(uid,out p))return p;
            nextUid=uid;p=Player(Kernel.Maps[1002],true);SubClassTable.Load(p.Entity);ChiTable.Load(p);WayofHeroes.Load(p);
            Revora.Database.KnownPersons.LoadPartner(p);Revora.Database.KnownPersons.LoaderFriends(p);Revora.Database.KnownPersons.LoadMentor(p);Revora.Database.KnownPersons.LoadEnemy(p);p.LoadItemStats();return p;
        };
        var prizeLeader=restore(4294916801u);var deputy=restore(4294916901u);var ordinary=restore(4294916902u);
        Require(!deputy.Fake && deputy.Account.State==AccountTable.AccountState.Player && !PirateFenced(deputy));
        Sql("CREATE TABLE IF NOT EXISTS revora_guild_prize_probe(run uuid PRIMARY KEY,guild_id bigint NOT NULL,before_funds numeric NOT NULL,after_funds numeric NOT NULL)");
        if((long)Sql("SELECT count(*) FROM revora_guild_prize_probe WHERE run=@run","run",frozenRun)==0) {
            Require(!reload);
            if(deputy.AsMember.Rank!=Revora.Game.Enums.GuildMemberRank.DeputyLeader)
                Require((bool)Call(Feature("GuildLeadership"),"Appoint",prizeLeader,deputy.AsMember,Revora.Game.Enums.GuildMemberRank.DeputyLeader));
            ulong before=prizeLeader.Guild.SilverFund;
            Sql("INSERT INTO revora_guild_prize_probe VALUES(@run,@guild,@before,@after)","run",frozenRun,"guild",(long)prizeLeader.Guild.ID,"before",(decimal)before,"after",(decimal)before+60000000m);
            HeartTalk(ordinary,706070,0);HeartTalk(ordinary,706070,2);Require(prizeLeader.Guild.SilverFund==before);
            HeartTalk(deputy,706070,2);Require(prizeLeader.Guild.SilverFund==before); // No offered claim yet.
            HeartTalk(prizeLeader,706070,0);HeartTalk(deputy,706070,0);
            HeartTalk(deputy,706070,2);
            Require(prizeLeader.Guild.SilverFund==before+60000000UL && !PirateFenced(deputy));
            for(int i=0;i<8;i++){HeartTalk(prizeLeader,706070,2);HeartTalk(deputy,706070,2);}
            Require(prizeLeader.Guild.SilverFund==before+60000000UL);
        }
        Require(Convert.ToDecimal(Sql("SELECT silverfund FROM guilds WHERE id=@guild","guild",(long)prizeLeader.Guild.ID))==Convert.ToDecimal(Sql("SELECT after_funds FROM revora_guild_prize_probe WHERE run=@run","run",frozenRun)));
        Require((long)Sql("SELECT count(*) FROM revora_guild_contest_prizes WHERE run=@run AND uid=4294916901 AND rank=1 AND silver=60000000","run",frozenRun)==1);
        Require((decimal)prizeLeader.Guild.SilverFund==Convert.ToDecimal(Sql("SELECT after_funds FROM revora_guild_prize_probe WHERE run=@run","run",frozenRun)));
        // A cold retry through the actual NPC cannot add another treasury payment.
        HeartTalk(prizeLeader,706070,0);HeartTalk(prizeLeader,706070,2);
        Require((decimal)prizeLeader.Guild.SilverFund==Convert.ToDecimal(Sql("SELECT after_funds FROM revora_guild_prize_probe WHERE run=@run","run",frozenRun)));
        GuildContestPrizeTransactions(ledger,frozenRun,prizeLeader.Entity.UID,deputy.Entity.UID,ordinary.Entity.UID,prizeLeader.Guild.ID,now);
        GuildPkScheduleProcess(frozenRun);
        System.Console.WriteLine("[career-process] career-guild-coordinator-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Guild coordinator fixture did not close.");
        }
    }
    static void GuildPkScheduleProcess(Guid earnedQualifier) {
        var schedule=GameAssembly.GetType("Revora.Database.GuildPkSchedule",true);
        Call(schedule,"Create",earnedQualifier);
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_weeks WHERE contest=@run","run",earnedQualifier)==1);
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_matches WHERE contest=@run","run",earnedQualifier)==0); // Only one guild actually qualified.
        // Synthetic frozen eight-guild input tests scheduling, not earning or match participation.
        Guid run=new Guid("f6067100-0000-0000-0000-000000000012");
        DateTime local=new DateTime(2050,1,8,20,30,0,DateTimeKind.Local);while(local.DayOfWeek!=DayOfWeek.Monday)local=local.AddDays(1);
        DateTime start=local.ToUniversalTime();
        Sql("INSERT INTO revora_guild_contests(id,starts_at,ends_at,state) VALUES(@run,@start,@end,'frozen') ON CONFLICT(id) DO NOTHING","run",run,"start",new DateTimeOffset(start),"end",new DateTimeOffset(start.AddHours(2)));
        for(int rank=1;rank<=8;rank++) {
            Sql("INSERT INTO revora_guild_contest_seeds(run,rank,guild_id,points,reached_at) VALUES(@run,@rank,@guild,@points,@now) ON CONFLICT DO NOTHING","run",run,"rank",rank,"guild",64000L+rank,"points",100L-rank,"now",new DateTimeOffset(start));
            Sql("INSERT INTO revora_guild_contest_owners(run,guild_id) VALUES(@run,@guild) ON CONFLICT DO NOTHING","run",run,"guild",64000L+rank);
        }
        // Simulate qualification committed before process loss: recovery must create
        // the same owned schedule before concurrent retries can duplicate it.
        Call(schedule,"Recover",start.AddHours(3));
        System.Threading.Tasks.Parallel.For(0,8,_=>Call(schedule,"Create",run));
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_matches WHERE contest=@run AND state='scheduled' AND winner IS NULL AND first_score=0 AND second_score=0","run",run)==12);
        Require((long)Sql("SELECT count(*) FROM revora_guild_pk_slots WHERE contest=@run","run",run)==24);
        Require((long)Sql("SELECT count(*) FROM (SELECT guild_id FROM revora_guild_pk_slots WHERE contest=@run GROUP BY guild_id HAVING count(*)=3) s","run",run)==8);
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new NpgsqlCommand("SELECT round,starts_at,ends_at FROM revora_guild_pk_matches WHERE contest=@run",c)) {
            q.Parameters.AddWithValue("run",run);using(var r=q.ExecuteReader())while(r.Read()) {
                int round=Convert.ToInt32(r[0]);DateTime begins=r.GetFieldValue<DateTimeOffset>(1).UtcDateTime;
                Require(begins==local.AddDays(round).ToUniversalTime() && r.GetFieldValue<DateTimeOffset>(2).UtcDateTime==begins.AddHours(2));
            }
        }}
        string snapshot=Convert.ToString(Sql("SELECT json_agg(row_to_json(m) ORDER BY round,bracket,first_guild)::text FROM revora_guild_pk_matches m WHERE contest=@run","run",run));
        Sql("CREATE TABLE IF NOT EXISTS revora_guild_pk_schedule_probe(run uuid PRIMARY KEY,snapshot text NOT NULL)");
        Sql("INSERT INTO revora_guild_pk_schedule_probe VALUES(@run,@snapshot) ON CONFLICT DO NOTHING","run",run,"snapshot",snapshot);
        Require(snapshot==Convert.ToString(Sql("SELECT snapshot FROM revora_guild_pk_schedule_probe WHERE run=@run","run",run)));
        bool refused=false;try{Call(schedule,"Create",new Guid("f6067100-0000-0000-0000-000000000010"));}
        catch(System.Reflection.TargetInvocationException ex){refused=ex.InnerException is InvalidOperationException;}
        Require(refused);
    }
    static void BindContestFixtureGuild(Type ledger,Guid run,uint guild) {
        using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
            Require((bool)Call(ledger,"BindGuild",c,t,run,guild));t.Commit();
        }}
    }
    static void GuildContestPrizeTransactions(Type ledger,Guid run,uint leader,uint deputy,uint ordinary,uint guild,DateTime now) {
        Func<NpgsqlConnection,NpgsqlTransaction,uint,int> claim=(c,t,uid)=> {
            object[] args={c,t,run,uid,guild,now,0UL};return (int)ledger.GetMethod("Prize",Hidden).Invoke(null,args);
        };
        System.Threading.Tasks.Parallel.For(0,8,i=> {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){Require(claim(c,t,i%2==0?leader:deputy)==2);t.Commit();}}
        });
        using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
            Require(claim(c,t,ordinary)==0);t.Rollback();
        }}
        decimal balance=Convert.ToDecimal(Sql("SELECT silverfund FROM guilds WHERE id=@guild","guild",(long)guild));
        using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
            Action<string> query=sql=>{using(var q=new NpgsqlCommand(sql,c,t)){q.Parameters.AddWithValue("run",run);q.Parameters.AddWithValue("guild",(long)guild);q.ExecuteNonQuery();}};
            query("DELETE FROM revora_guild_contest_prizes WHERE run=@run AND guild_id=@guild");
            query(@"CREATE OR REPLACE FUNCTION revora_test_contest_prize_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
IF NEW.silverfund>OLD.silverfund AND EXISTS(SELECT 1 FROM revora_guild_prize_probe WHERE guild_id=OLD.id) THEN RETURN NULL; END IF; RETURN NEW; END $$;
CREATE TRIGGER revora_test_contest_prize_failure BEFORE UPDATE OF silverfund ON guilds FOR EACH ROW EXECUTE FUNCTION revora_test_contest_prize_failure();");
            bool failed=false;try{claim(c,t,deputy);}catch(System.Reflection.TargetInvocationException ex){failed=ex.InnerException is InvalidOperationException && ex.InnerException.Message=="Guild contest treasury prize was not saved.";}
            Require(failed);t.Rollback(); // Receipt and attempted treasury update both disappear.
        }}
        Require((long)Sql("SELECT count(*) FROM revora_guild_contest_prizes WHERE run=@run","run",run)==1);
        Require(Convert.ToDecimal(Sql("SELECT silverfund FROM guilds WHERE id=@guild","guild",(long)guild))==balance);
        using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
            Func<string,object> query=sql=>{using(var q=new NpgsqlCommand(sql,c,t)){q.Parameters.AddWithValue("run",run);return q.ExecuteScalar();}};
            Require((long)query("SELECT count(*) FROM guilds WHERE id IN(64990,64991)")==0);
            const string clone="INSERT INTO guilds SELECT (jsonb_populate_record(NULL::guilds,to_jsonb(g)||jsonb_build_object('id',64990,'name','PrizeLifeProbe','leadername','PrizeLifeProbe'))).* FROM guilds g ORDER BY id LIMIT 1";
            query(clone);Require((bool)Call(ledger,"BindGuild",c,t,run,64990u));
            query("UPDATE guilds SET id=64991 WHERE id=64990");
            Require(!(bool)query("SELECT eligible FROM revora_guild_contest_owners WHERE run=@run AND guild_id=64990"));
            query(clone);Require(!(bool)Call(ledger,"BindGuild",c,t,run,64990u));
            Require((bool)Call(ledger,"BindGuild",c,t,run,64991u));query("DELETE FROM guilds WHERE id=64991");
            Require(!(bool)query("SELECT eligible FROM revora_guild_contest_owners WHERE run=@run AND guild_id=64991"));
            t.Rollback();
        }}
        Require((long)Sql("SELECT count(*) FROM guilds WHERE id IN(64990,64991)")==0);
    }
    static void GuildContestEntryProcess(bool reload) {
        var ledger=GameAssembly.GetType("Revora.Database.GuildContestTable",true);Call(ledger,"Initialize");var admission=Feature("GuildContestAdmission");
        Guid run=new Guid("f6067100-0000-0000-0000-000000000007"),pending=new Guid("f6067100-0000-0000-0000-000000000008");
        DateTime start=new DateTime(2038,2,1,20,30,0,DateTimeKind.Utc),now=start.AddMinutes(-2);
        admission.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));guildTenureFixture=true;
        nextUid=4294916801;var p=Player(Kernel.Maps[1002],true);
        Func<Guid,string> state=id=>Convert.ToString(Sql("SELECT state FROM revora_guild_contest_transfers WHERE run=@run AND uid=@uid","run",id,"uid",(long)p.Entity.UID));
        if(!reload && state(pending)=="") {
            Require(p.Entity.MapID==1002);var map=Kernel.Maps[700].MakeDynamicMap();
            foreach(var id in new[]{run,pending}) {
                DateTime begins=id==run?start:start.AddDays(7);
                Sql("INSERT INTO revora_guild_contests(id,starts_at,ends_at,state) VALUES(@run,@start,@end,'open');INSERT INTO revora_guild_contest_maps(run,band,map) VALUES(@run,3,@map)",
                    "run",id,"start",new DateTimeOffset(begins),"end",new DateTimeOffset(begins.AddHours(2)),"map",(long)map.ID);
            }
            now=start.AddMinutes(-6);Require(!(bool)Call(admission,"Enter",p,run,map) && p.Entity.MapID==1002);
            now=start.AddMinutes(-2);Require((bool)Call(admission,"Enter",p,run,map) && ReferenceEquals(p.Map,map) && state(run)=="entered");
            Require(!(bool)Call(admission,"Enter",p,run,map));
            var sessions=Feature("SessionInstall");Require((bool)Call(sessions,"TryEnter",p.Entity.UID));
            try {lock(p.ItemSyncRoot)Call(admission,"Leave",p,run);}finally{Call(sessions,"Exit",p.Entity.UID);}
            Require(p.Entity.MapID==1002 && state(run)=="returned" && !(bool)Sql("SELECT active FROM revora_guild_contest_entries WHERE run=@run AND uid=@uid","run",run,"uid",(long)p.Entity.UID));
            now=start.AddDays(7).AddMinutes(-2);Require((bool)Call(sessions,"TryEnter",p.Entity.UID));
            try {lock(p.ItemSyncRoot) {
                Require((bool)Call(admission,"Reserve",p,pending,map));
                Require(!(bool)Call(admission,"Confirm",p,pending,map)); // Still in town.
                p.Entity.Teleport(700,map.ID,40,40);Require(ReferenceEquals(p.Map,map));
            }}finally{Call(sessions,"Exit",p.Entity.UID);}
            Require(state(pending)=="reserved" && (long)Sql("SELECT count(*) FROM revora_guild_contest_entries WHERE run=@run","run",pending)==0);
            // Leave the durable reservation and location for the next cold process.
        } else {
            Require(state(pending)=="returned" && p.Entity.MapID==1002);
            Require((long)Sql("SELECT count(*) FROM revora_guild_contest_entries WHERE run=@run","run",pending)==0);
        }
        Guid rejectedLife=new Guid("f6067100-0000-0000-0000-000000000009");
        Sql("INSERT INTO revora_guild_contest_lives(run,life,band,map) SELECT run,@life,band,map FROM revora_guild_contest_maps WHERE run=@run ON CONFLICT DO NOTHING","run",run,"life",rejectedLife);
        Require(!(bool)Call(ledger,"Kill",run,rejectedLife,p.Entity.UID,p.Guild.ID,start.AddMinutes(1)));
        System.Console.WriteLine("[career-process] career-guild-entry-"+(reload?"reload":"earn")+"-passed");
        Server.CommandsAI("@close");throw new InvalidOperationException("Guild entry fixture did not close.");
    }
    static void GuildContestCombatProcess(bool reload) {
        var ledger=GameAssembly.GetType("Revora.Database.GuildContestTable",true);Call(ledger,"Initialize");var combat=Feature("GuildContestCombat");
        Guid run=new Guid("f6067100-0000-0000-0000-000000000004");DateTime now=new DateTime(2037,1,12,21,0,0,DateTimeKind.Utc);
        combat.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        Func<long> count=()=>(long)Sql("SELECT count(*) FROM revora_guild_contest_kills WHERE run=@run","run",run);
        if((long)Sql("SELECT count(*) FROM revora_guild_contests WHERE id=@run","run",run)==0) {
            Require(!reload);Sql("INSERT INTO revora_guild_contests(id,starts_at,ends_at,state) VALUES(@run,@start,@end,'open')","run",run,"start",new DateTimeOffset(now.AddMinutes(-30)),"end",new DateTimeOffset(now.AddHours(1)));
        }
        if((string)Sql("SELECT state FROM revora_guild_contests WHERE id=@run","run",run)=="open") {
            Require(!reload && count()==0);guildTenureFixture=true;nextUid=4294916801;var p=Player(Kernel.Maps[1002],true);var outsider=Player(Kernel.Maps[1002],true);
            BindContestFixtureGuild(ledger,run,p.Guild.ID);
            var map=Kernel.Maps[700].MakeDynamicMap();
            try {
                foreach(var who in new[]{p,outsider}){who.Entity.Teleport(700,map.ID,40,40);Require(ReferenceEquals(who.Map,map));}
                Sql("INSERT INTO revora_guild_contest_entries(run,uid,guild_id,band,map,entered_at) VALUES(@run,@uid,@guild,3,@map,@now)","run",run,"uid",(long)p.Entity.UID,"guild",(long)p.Guild.ID,"map",(long)map.ID,"now",new DateTimeOffset(now));
                Func<Entity> monster=()=> {
                    var info=new MonsterInformation{ID=1,Name="GuildContestProbe",Level=1,Hitpoints=10};
                    var m=new Entity(EntityFlag.Monster,false){UID=map.EntityUIDCounter.Next,MapID=map.ID,X=40,Y=40,MonsterInfo=info,Name=info.Name,MaxHitpoints=10,MapObjType=MapObjectType.Monster,IsDropped=true};
                    info.Owner=m;map.Entities[m.UID]=m;m.Hitpoints=10;p.Screen.Add(m);outsider.Screen.Add(m);return m;
                };
                Action<Revora.Client.GameState,Entity> hit=(who,m)=> {
                    who.Entity.MinAttack=who.Entity.MaxAttack=who.Entity.BaseMinAttack=who.Entity.BaseMaxAttack=1000;
                    var attack=new Attack(true){AttackType=Attack.Melee,Attacker=who.Entity.UID,Attacked=m.UID};uint damage=Revora.Game.Attacking.Calculate.Melee(who.Entity,m,ref attack);Require(damage>=10);
                    Revora.Game.Attacking.Handle.ReceiveAttack(who.Entity,m,attack,ref damage,null);
                };
                var ordinary=monster();hit(p,ordinary);Require(ordinary.Dead && count()==0);
                var target=monster();Require((bool)Call(combat,"Register",run,target,3));Require(!(bool)Call(combat,"Register",run,target,3));
                hit(outsider,target);Require(!target.Dead && count()==0);
                Sql(@"CREATE OR REPLACE FUNCTION revora_test_guild_score_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
IF NEW.run='f6067100-0000-0000-0000-000000000004'::uuid THEN RETURN NULL; END IF; RETURN NEW; END $$;
CREATE TRIGGER revora_test_guild_score_failure BEFORE INSERT ON revora_guild_contest_kills FOR EACH ROW EXECUTE FUNCTION revora_test_guild_score_failure();");
                try {hit(p,target);Require(!target.Dead && count()==0);}
                finally {Sql("DROP TRIGGER revora_test_guild_score_failure ON revora_guild_contest_kills;DROP FUNCTION revora_test_guild_score_failure()");}
                hit(p,target);Require(target.Dead && count()==1);target.Die(p.Entity);Require(count()==1);
                target.Hitpoints=10;Require((bool)Call(combat,"Register",run,target,3));hit(p,target);Require(target.Dead && count()==2);
                var guard=monster();guard.MonsterInfo.Guard=true;Require(!(bool)Call(combat,"Register",run,guard,3));
                var seeds=(uint[])Call(ledger,"Freeze",run,now.AddHours(1));Require(seeds.Length==1 && seeds[0]==p.Guild.ID);
            } finally {
                foreach(var who in new[]{p,outsider})who.Entity.Teleport(1002,408,330);
                map.Dispose();
            }
        }
        Require(count()==2 && (long)Sql("SELECT points FROM revora_guild_contest_seeds WHERE run=@run AND rank=1","run",run)==2);
        System.Console.WriteLine("[career-process] career-guild-combat-"+(reload?"reload":"earn")+"-passed");
        Server.CommandsAI("@close");throw new InvalidOperationException("Guild combat fixture did not close.");
    }
    static void GuildContestLedgerProcess(bool reload) {
        var ledger=GameAssembly.GetType("Revora.Database.GuildContestTable",true);Call(ledger,"Initialize");
        Guid run=new Guid("f6067100-0000-0000-0000-000000000002");
        DateTime start=new DateTime(2036,1,7,20,30,0,DateTimeKind.Utc),end=start.AddHours(2);
        uint[] owners={4294916801u,4294912301u};uint[] guilds=owners.Select(uid=>Convert.ToUInt32(Sql("SELECT guildid FROM entities WHERE uid=@uid","uid",(long)uid))).ToArray();
        Require(guilds.All(g=>g>0) && guilds.Distinct().Count()==2);
        Func<int,Guid> life=n=>new Guid("f6067100-0000-0000-0000-"+n.ToString("D12"));
        Func<int,uint,uint,DateTime,bool> kill=(n,uid,guild,now)=>(bool)Call(ledger,"Kill",run,life(n),uid,guild,now);
        Func<uint[]> freeze=()=>(uint[])Call(ledger,"Freeze",run,end);
        if((long)Sql("SELECT count(*) FROM revora_guild_contests WHERE id=@run","run",run)==0) {
            Require(!reload);
            Sql("INSERT INTO revora_guild_contests(id,starts_at,ends_at,state) VALUES(@run,@start,@end,'open')","run",run,"start",new DateTimeOffset(start),"end",new DateTimeOffset(end));
            for(int i=0;i<2;i++)Sql("INSERT INTO revora_guild_contest_entries(run,uid,guild_id,band,map,entered_at) VALUES(@run,@uid,@guild,3,700,@now)","run",run,"uid",(long)owners[i],"guild",(long)guilds[i],"now",new DateTimeOffset(start));
            foreach(var guild in guilds)BindContestFixtureGuild(ledger,run,guild);
            for(int n=1;n<=11;n++)Sql("INSERT INTO revora_guild_contest_lives(run,life,band,map) VALUES(@run,@life,@band,700)","run",run,"life",life(n),"band",n==11?2:3);
        }
        if((string)Sql("SELECT state FROM revora_guild_contests WHERE id=@run","run",run)=="open") {
            Require(!reload);
            bool early=false;try {Call(ledger,"Freeze",run,end.AddSeconds(-1));}catch(System.Reflection.TargetInvocationException ex){early=ex.InnerException is InvalidOperationException;}Require(early);
            Require(!kill(1,owners[0],guilds[0],start.AddSeconds(-1)) && !kill(1,owners[0],guilds[0],end));
            Require(!kill(11,owners[0],guilds[0],start) && !kill(12,owners[0],guilds[0],start));
            Require(!kill(1,4294916802u,guilds[0],start));
            System.Threading.Tasks.Parallel.For(0,8,_=>Require(kill(1,owners[0],guilds[0],start)));
            Require(!kill(1,owners[1],guilds[1],start));
            for(int n=2;n<=10;n++){int owner=n<=6?0:1;Require(kill(n,owners[owner],guilds[owner],start.AddSeconds(n)));}
            Require((long)Sql("SELECT count(*) FROM revora_guild_contest_kills WHERE run=@run","run",run)==10);
            System.Threading.Tasks.Parallel.For(0,4,_=>Require(freeze().SequenceEqual(guilds)));
        }
        Require(freeze().SequenceEqual(guilds));
        Require((long)Sql("SELECT points FROM revora_guild_contest_seeds WHERE run=@run AND rank=1","run",run)==6);
        Require((long)Sql("SELECT points FROM revora_guild_contest_seeds WHERE run=@run AND rank=2","run",run)==4);
        Require(kill(1,owners[0],guilds[0],end.AddDays(1)) && !kill(1,owners[1],guilds[1],end.AddDays(1)));
        Require(!kill(11,owners[0],guilds[0],start.AddMinutes(1)));
        Require((long)Sql("SELECT count(*) FROM revora_guild_contest_kills WHERE run=@run","run",run)==10);
        System.Console.WriteLine("[career-process] career-guild-ledger-"+(reload?"reload":"earn")+"-passed");
        Server.CommandsAI("@close");throw new InvalidOperationException("Guild ledger fixture did not close.");
    }
    static bool guildTenureFixture;
    static string GuildTenureSnapshot(uint uid) {
        return Convert.ToString(Sql(@"SELECT json_build_object('admission',(SELECT row_to_json(a) FROM revora_guild_admissions a WHERE uid=@uid),
'membership',(SELECT json_build_array(guildid,guildrank,conquerpoints) FROM entities WHERE uid=@uid))::text","uid",(long)uid));
    }
    static void GuildTenureProcess(bool reload) {
        guildTenureFixture=true;const uint first=4294916801;
        if((bool)Sql("SELECT to_regclass('revora_guild_tenure_probe') IS NULL")) {
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN @uid AND @last","uid",(long)first,"last",(long)first+1)==0);
            nextUid=first;Player(Kernel.Maps[1002]);Player(Kernel.Maps[1002]);
            Sql("UPDATE entities SET level=130,reborn=2,conquerpoints=2000000 WHERE uid BETWEEN @uid AND @last","uid",(long)first,"last",(long)first+1);
            Sql("CREATE TABLE revora_guild_tenure_probe(uid bigint PRIMARY KEY,snapshot text NOT NULL)");
        }
        nextUid=first;var leader=Player(Kernel.Maps[1002],true);var member=Player(Kernel.Maps[1002],true);
        foreach(var p in new[]{leader,member}) {
            SubClassTable.Load(p.Entity);ChiTable.Load(p);WayofHeroes.Load(p);
            Revora.Database.KnownPersons.LoadPartner(p);Revora.Database.KnownPersons.LoaderFriends(p);Revora.Database.KnownPersons.LoadMentor(p);Revora.Database.KnownPersons.LoadEnemy(p);p.LoadItemStats();
            Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        }
        if((long)Sql("SELECT count(*) FROM revora_guild_tenure_probe")==0) {
            Require(!reload);
            if(leader.Guild==null) {
                HeartTalk(leader,10003,9);var name=System.Text.Encoding.ASCII.GetBytes("TenureProbe6801");var b=new byte[28+name.Length];
                Revora.Network.Writer.WriteUInt16((ushort)(b.Length-8),0,b);Revora.Network.Writer.WriteUInt16(2032,2,b);Revora.Network.Writer.WriteUInt32(10003,8,b);
                b[14]=2;b[17]=(byte)name.Length;name.CopyTo(b,18);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,b.Length-8,b);Revora.Network.PacketHandler.HandlePacket(b,leader);
            }
            Require(leader.Guild!=null);
            Action join=()=>{GuildClaimPacket(leader,GuildCommand.InviteRequest,member.Entity.UID);GuildClaimPacket(member,GuildCommand.JoinRequest,first);Require(ReferenceEquals(member.Guild,leader.Guild));};
            if(member.Guild==null)join();
            Guid before=(Guid)Sql("SELECT admission FROM revora_guild_admissions WHERE uid=@uid","uid",(long)member.Entity.UID);
            GuildClaimPacket(member,GuildCommand.Quit,leader.Guild.ID);
            Require(member.Guild==null && member.Entity.GuildID==0 && (long)Sql("SELECT count(*) FROM revora_guild_admissions WHERE uid=@uid","uid",(long)member.Entity.UID)==0);
            join();Require(before!=(Guid)Sql("SELECT admission FROM revora_guild_admissions WHERE uid=@uid","uid",(long)member.Entity.UID));
            foreach(var p in new[]{leader,member}) {Require(EntityTable.SaveEntity(p));Sql("INSERT INTO revora_guild_tenure_probe(uid,snapshot) VALUES(@uid,@snapshot)","uid",(long)p.Entity.UID,"snapshot",GuildTenureSnapshot(p.Entity.UID));}
        }
        foreach(var p in new[]{leader,member}) {
            Require(p.Guild!=null && GuildTenureSnapshot(p.Entity.UID)==Convert.ToString(Sql("SELECT snapshot FROM revora_guild_tenure_probe WHERE uid=@uid","uid",(long)p.Entity.UID)));
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                DateTime joined;using(var q=new NpgsqlCommand("SELECT joined_at FROM revora_guild_admissions WHERE uid=@uid",c,t)){q.Parameters.AddWithValue("uid",(long)p.Entity.UID);using(var r=q.ExecuteReader()){Require(r.Read());joined=r.GetFieldValue<DateTimeOffset>(0).UtcDateTime;}}
                Func<uint,DateTime,bool> eligible=(guild,now)=>(bool)Call(Feature("WayOfHeroesClaims"),"SevenDayGuildMembership",c,t,p.Entity.UID,guild,now);
                Require(!eligible(p.Guild.ID,joined) && !eligible(p.Guild.ID,joined.AddDays(7).AddMilliseconds(-1)));
                Require(eligible(p.Guild.ID,joined.AddDays(7)) && eligible(p.Guild.ID,joined.AddDays(8)));
                Require(!eligible(0,joined.AddDays(8)) && !eligible(p.Guild.ID+1,joined.AddDays(8)));
                using(var contender=DataHolder.MySqlConnection) {
                    contender.Open();bool locked=false;
                    using(var q=new NpgsqlCommand("SELECT uid FROM entities WHERE uid=@uid FOR UPDATE NOWAIT",contender)) {
                        q.Parameters.AddWithValue("uid",(long)p.Entity.UID);
                        try {q.ExecuteScalar();}catch(PostgresException ex){if(ex.SqlState!="55P03")throw;locked=true;}
                    }
                    Require(locked);
                }
                t.Rollback();
            }}
        }
        Require((long)Sql("SELECT count(*) FROM revora_guild_admissions WHERE uid=4294912301")==0);
        uint legacyGuild=Convert.ToUInt32(Sql("SELECT guildid FROM entities WHERE uid=4294912301"));Require(legacyGuild>0);
        using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
            Require(!(bool)Call(Feature("WayOfHeroesClaims"),"SevenDayGuildMembership",c,t,4294912301u,legacyGuild,DateTime.UtcNow.AddYears(10)));
            t.Rollback();
        }}
        System.Console.WriteLine("[career-process] career-guild-tenure-"+(reload?"reload":"earn")+"-passed");
        Server.CommandsAI("@close");throw new InvalidOperationException("Guild tenure fixture did not close.");
    }
    // Transaction-level prerequisite coverage only; this is not tournament entry.
    static void GuildAdmissionRecordProcess() {
        const long uid=4294916201; // Retained ordinary XP fixture, never committed below.
        string before=Convert.ToString(Sql("SELECT row_to_json(r)::text FROM revora_guild_admissions r WHERE uid=@uid","uid",uid));
        using(var c=DataHolder.MySqlConnection) {c.Open();using(var t=c.BeginTransaction()) {
            Func<string,object> query=sql=>{using(var q=new NpgsqlCommand(sql,c,t)){q.Parameters.AddWithValue("uid",uid);return q.ExecuteScalar();}};
            Require((long)query("SELECT count(*) FROM entities WHERE uid=@uid")==1);
            query("SELECT uid FROM entities WHERE uid=@uid FOR UPDATE");
            var method=Feature("WayOfHeroesClaims").GetMethod("RecordGuildMembership",Hidden);
            Action<uint> join=guild=>method.Invoke(null,new object[]{c,t,(uint)uid,guild,false});
            query("UPDATE entities SET guildid=51002,guildrank=200 WHERE uid=@uid");
            join(51002);
            string achievement=Convert.ToString(query("SELECT row_to_json(r)::text FROM revora_guild_earning r WHERE uid=@uid"));
            Guid first=(Guid)query("SELECT admission FROM revora_guild_admissions WHERE uid=@uid");
            query("UPDATE revora_guild_admissions SET joined_at='2000-01-01 UTC' WHERE uid=@uid");
            join(51002); // Rejoining the same guild must not inherit its old clock.
            Guid second=(Guid)query("SELECT admission FROM revora_guild_admissions WHERE uid=@uid");
            Require(second!=first && (bool)query("SELECT joined_at>='2026-01-01 UTC' FROM revora_guild_admissions WHERE uid=@uid"));
            query("SAVEPOINT departure");
            query("UPDATE entities SET guildrank=0 WHERE uid=@uid");
            Require((long)query("SELECT count(*) FROM revora_guild_admissions WHERE uid=@uid")==0);
            query("ROLLBACK TO SAVEPOINT departure");
            Require((Guid)query("SELECT admission FROM revora_guild_admissions WHERE uid=@uid")==second);
            query("UPDATE entities SET guildid=guildid,guildrank=200 WHERE uid=@uid");
            Require((Guid)query("SELECT admission FROM revora_guild_admissions WHERE uid=@uid")==second);
            query("UPDATE entities SET guildid=0 WHERE uid=@uid");
            Require((long)query("SELECT count(*) FROM revora_guild_admissions WHERE uid=@uid")==0);
            bool mismatch=false;
            try {join(51003);}catch(System.Reflection.TargetInvocationException ex){mismatch=ex.InnerException is InvalidOperationException;}
            Require(mismatch);
            query("UPDATE entities SET guildid=51003 WHERE uid=@uid");
            join(51003);
            Require((long)query("SELECT guild_id FROM revora_guild_admissions WHERE uid=@uid")==51003);
            Require((Guid)query("SELECT admission FROM revora_guild_admissions WHERE uid=@uid")!=second);
            Require(achievement==Convert.ToString(query("SELECT row_to_json(r)::text FROM revora_guild_earning r WHERE uid=@uid")));
            Require((long)query("SELECT count(*) FROM guilds WHERE id IN(64990,64991)")==0);
            query("INSERT INTO guilds SELECT (jsonb_populate_record(NULL::guilds,to_jsonb(g)||jsonb_build_object('id',64990,'name','TenureProbe','leadername','TenureProbe'))).* FROM guilds g ORDER BY id LIMIT 1");
            Require((long)query("SELECT count(*) FROM guilds WHERE id=64990")==1);
            query("UPDATE entities SET guildid=64990 WHERE uid=@uid");join(64990);
            Guid dissolution=(Guid)query("SELECT admission FROM revora_guild_admissions WHERE uid=@uid");
            query("SAVEPOINT dissolution");query("DELETE FROM guilds WHERE id=64990");
            Require((long)query("SELECT count(*) FROM revora_guild_admissions WHERE uid=@uid")==0);
            query("ROLLBACK TO SAVEPOINT dissolution");
            Require((Guid)query("SELECT admission FROM revora_guild_admissions WHERE uid=@uid")==dissolution);
            query("UPDATE guilds SET id=64991 WHERE id=64990");
            Require((long)query("SELECT count(*) FROM revora_guild_admissions WHERE uid=@uid")==0);
            bool refused=false;
            try {method.Invoke(null,new object[]{c,null,(uint)uid,51003u,false});}
            catch(System.Reflection.TargetInvocationException ex){refused=ex.InnerException is InvalidOperationException;}
            Require(refused);t.Rollback();
        }}
        Require(before==Convert.ToString(Sql("SELECT row_to_json(r)::text FROM revora_guild_admissions r WHERE uid=@uid","uid",uid)));
        System.Console.WriteLine("[career-process] career-guild-admission-passed");
        Server.CommandsAI("@close");throw new InvalidOperationException("Guild admission fixture did not close.");
    }
}
