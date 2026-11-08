using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.ConquerStructures.Society;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static DateTime beastProbeNow;
    static void BeastProbeClock(bool claiming){
        GuildProbeClock(false);
        beastProbeNow=new DateTime(2034,1,claiming?2:1,claiming?0:20,claiming?30:0,0,DateTimeKind.Local).ToUniversalTime();
        Feature("GuildBeastEvent").GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>beastProbeNow));
    }
    static void BeastHunt(){
        BeastProbeClock(false);var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();
        Require(p.Account.State==AccountTable.AccountState.Player && !p.Fake && p.Entity.GuildID==50000 && p.Entity.Money>=500000);
        Require((long)Sql("SELECT count(*) FROM revora_guild_battles WHERE day='2034-01-01' AND state='won' AND keeper=50000")==1
            && (long)Sql("SELECT count(*) FROM revora_guild_purses WHERE day='2034-01-01' AND uid=@uid AND claimed_at IS NOT NULL","uid",(long)p.Entity.UID)==1);
        ulong gold=p.Entity.Money,fund=p.Guild.SilverFund;
        lock(p.ItemSyncRoot)Require((bool)Call(Feature("GuildDonations"),"Donate",p,500000u,false));
        Require(p.Entity.Money==gold-500000 && p.Guild.SilverFund==fund+500000);
        HeartTalk(p,10003,89);Require(p.Entity.MapID==1038);HeartTalk(p,8150,1);HeartTalk(p,8150,1);
        Require(p.Guild.SilverFund==fund && (long)Sql("SELECT count(*) FROM revora_guild_beast_days WHERE day='2034-01-01' AND battle_day='2034-01-01' AND guild_id=50000 AND paid_silver=500000 AND state='funded'")==1);
        beastProbeNow=beastProbeNow.AddHours(1);Call(Feature("GuildBeastEvent"),"Tick");
        var body=Kernel.Maps[1038].Entities.Values.Single(e=>e.MonsterInfo!=null && e.MonsterInfo.ID==7006);
        Require(body.Hitpoints==MonsterInformation.MonsterInformations[7006].Hitpoints);
        string before=TaoistFingerprint(p);uint cp=p.Entity.ConquerPoints,rvp=p.Entity.RevoraPoint;
        EliminationHit(p,body);
        Require((long)Sql("SELECT count(*) FROM revora_guild_beast_days WHERE day='2034-01-01' AND state='killed' AND killer=@uid AND soul_type BETWEEN 800000 AND 899999 AND claimed_at IS NULL","uid",(long)p.Entity.UID)==1
            && (long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10303","uid",(long)p.Entity.UID)==1);
        Require(TaoistFingerprint(p)==before && p.Entity.ConquerPoints==cp && p.Entity.RevoraPoint==rvp && p.Entity.Money==gold-500000);
        Require(EntityTable.SaveEntity(p));
        Sql("CREATE TABLE revora_beast_process_probe(uid bigint PRIMARY KEY,before_items text,after_items text,cp bigint,rvp bigint,gold bigint,paid_at timestamptz); INSERT INTO revora_beast_process_probe(uid,before_items,cp,rvp,gold) VALUES(@uid,@items,@cp,@rvp,@gold)",
            "uid",(long)p.Entity.UID,"items",before,"cp",(long)cp,"rvp",(long)rvp,"gold",(long)p.Entity.Money);
        System.Console.WriteLine("[beast-process] beast-hunt-passed");Environment.Exit(0);
    }
    static void BeastClaim(){
        BeastProbeClock(true);var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();
        Require(TaoistFingerprint(p)==(string)Sql("SELECT before_items FROM revora_beast_process_probe"));
        var prior=p.Inventory.Objects.Select(i=>i.UID).ToArray();p.BindWorldMap(1002);HeartTalk(p,10003,89);HeartTalk(p,8150,2);HeartTalk(p,8150,2);
        var added=p.Inventory.Objects.Where(i=>!prior.Contains(i.UID)).ToArray();Require(added.Length==2);
        Require(added.Any(i=>i.ID==1088000 && !i.Bound && i.Days==0));
        var soul=added.Single(i=>ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7);Require(soul.Bound && soul.Days==0);
        Require((bool)Sql("SELECT claimed_uid=@uid AND soul_uid=@soul AND soul_type=@type AND reward_item=ANY(@items) AND claimed_at IS NOT NULL FROM revora_guild_beast_days WHERE day='2034-01-01'", "uid",(long)p.Entity.UID,"soul",(long)soul.UID,"type",(long)soul.ID,"items",added.Select(i=>(long)i.UID).ToArray()));
        Sql("UPDATE revora_beast_process_probe SET after_items=@items,paid_at=(SELECT claimed_at FROM revora_guild_beast_days WHERE day='2034-01-01')","items",TaoistFingerprint(p));
        System.Console.WriteLine("[beast-process] beast-claim-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Beast claim did not close.");
    }
    static void BeastVerify(){
        BeastProbeClock(true);var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();p.BindWorldMap(1002);HeartTalk(p,10003,89);HeartTalk(p,8150,2);
        Require(TaoistFingerprint(p)==(string)Sql("SELECT after_items FROM revora_beast_process_probe"));
        Require((bool)Sql("SELECT e.conquerpoints=p.cp AND e.revorapoint=p.rvp AND e.money=p.gold AND d.claimed_at=p.paid_at FROM revora_beast_process_probe p JOIN entities e ON e.uid=p.uid JOIN revora_guild_beast_days d ON d.killer=p.uid WHERE d.day='2034-01-01'"));
        Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10303","uid",(long)p.Entity.UID)==1);
        System.Console.WriteLine("[beast-process] beast-verify-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Beast verify did not close.");
    }
    static DateTime eliteProbeNow;
    static void EliteProbeClock(bool opening){
        eliteProbeNow=new DateTime(2034,1,2,opening?21:22,opening?1:2,5,DateTimeKind.Local).ToUniversalTime();
        typeof(EliteGuildWar).GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>eliteProbeNow));
    }
    static void EliteGuildCapture(){
        EliteProbeClock(true);Player(Kernel.Maps[1002]);nextUid=FirstUid;var p=Player(Kernel.Maps[1002],true);
        var guild=new Guild(p.Entity.Name){ID=50000,Name="ProbeEliteGuild"};p.Guild=guild;p.Entity.GuildID=50000;
        p.AsMember=new Guild.Member(guild.ID){ID=p.Entity.UID,Name=p.Entity.Name,Level=p.Entity.Level,Class=p.Entity.Class,Rank=Enums.GuildMemberRank.GuildLeader};
        p.Entity.GuildRank=(ushort)p.AsMember.Rank;guild.Members[p.Entity.UID]=p.AsMember;guild.Leader=p.AsMember;guild.LeaderName=p.Entity.Name;
        Require(EntityTable.SaveEntity(p));GuildTable.Create(guild);Kernel.Guilds[guild.ID]=guild;p.LoadItemStats();
        Call(typeof(EliteGuildWar),"Tick");Require(EliteGuildWar.IsWar);
        var battle=(Guid)typeof(EliteGuildWar).GetField("battleId",Hidden).GetValue(null);
        HeartTalk(p,115522,1);Require(p.Entity.MapID==2071);
        var pole=EliteGuildWar.Poles;p.Entity.X=(ushort)(pole.X+1);p.Entity.Y=pole.Y;
        Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,pole,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=pole.UID},pole.MaxHitpoints,null);
        Require(EliteGuildWar.PoleKeeper==guild && Convert.ToInt32(Sql("SELECT captures FROM revora_elite_guild_battles WHERE id=@id","id",battle))==1);
        EliteProbeClock(false);Call(typeof(EliteGuildWar),"Tick");Require(!EliteGuildWar.IsWar);
        // Supplied bag padding demonstrates pending-prize capacity across restart.
        for(int n=0;n<40;n++)p.Inventory.Add(611004,0,1);
        p.BindWorldMap(1002);HeartTalk(p,115522,5);
        Require(p.Inventory.Count==40 && p.Entity.Money==100000 && p.Entity.RevoraPoint==0
            && (bool)Sql("SELECT state='won' AND claimed_at IS NULL FROM revora_elite_guild_battles WHERE id=@id","id",battle));
        Require(EntityTable.SaveEntity(p));
        Sql("CREATE TABLE revora_elite_process_probe(uid bigint PRIMARY KEY,battle uuid,fingerprint text,soul bigint,paid_at timestamptz); INSERT INTO revora_elite_process_probe(uid,battle) VALUES(@uid,@battle)","uid",(long)p.Entity.UID,"battle",battle);
        System.Console.WriteLine("[elite-guild-process] elite-capture-passed");Environment.Exit(0);
    }
    static void EliteGuildFinish(){
        EliteProbeClock(false);var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();
        Require(p.Account.State==AccountTable.AccountState.Player && !p.Fake && p.Inventory.Count==40);
        p.BindWorldMap(1002);HeartTalk(p,115522,5);
        Require(p.Entity.Money==100000 && p.Entity.RevoraPoint==0 && (bool)Sql("SELECT claimed_at IS NULL FROM revora_elite_guild_battles WHERE id=(SELECT battle FROM revora_elite_process_probe)"));
        p.Inventory.Remove(p.Inventory.Objects.First(i=>i.ID==611004),Enums.ItemUse.Remove);
        HeartTalk(p,115522,5);HeartTalk(p,115522,5);
        var soul=p.Inventory.Objects.Single(i=>ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7);
        Require(p.Entity.Money==500000100000UL && p.Entity.RevoraPoint==200000 && p.Entity.ConquerPoints==0 && p.Inventory.Count==40 && soul.Bound && soul.Days==0);
        Require((bool)Sql("SELECT claimed_by=@uid AND soul_uid=@soul AND claimed_at IS NOT NULL FROM revora_elite_guild_battles WHERE id=(SELECT battle FROM revora_elite_process_probe)","uid",(long)p.Entity.UID,"soul",(long)soul.UID));
        Sql("UPDATE revora_elite_process_probe SET fingerprint=@fingerprint,soul=@soul,paid_at=(SELECT claimed_at FROM revora_elite_guild_battles WHERE id=battle)","fingerprint",TaoistFingerprint(p),"soul",(long)soul.UID);
        System.Console.WriteLine("[elite-guild-process] elite-finish-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Elite finish did not close.");
    }
    static void EliteGuildVerify(){
        EliteProbeClock(false);var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();p.BindWorldMap(1002);HeartTalk(p,115522,5);
        Require(p.Entity.Money==500000100000UL && p.Entity.RevoraPoint==200000 && p.Entity.ConquerPoints==0 && p.Inventory.Count==40
            && p.Entity.GuildID==50000 && p.AsMember.Rank==Enums.GuildMemberRank.GuildLeader);
        Require(TaoistFingerprint(p)==(string)Sql("SELECT fingerprint FROM revora_elite_process_probe"));
        Require((bool)Sql("SELECT b.claimed_at=p.paid_at AND b.soul_uid=p.soul AND b.claimed_by=p.uid AND b.state='won' AND b.keeper=50000 FROM revora_elite_process_probe p JOIN revora_elite_guild_battles b ON b.id=p.battle"));
        System.Console.WriteLine("[elite-guild-process] elite-verify-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Elite verify did not close.");
    }
    static DateTime superProbeNow;
    static void SuperProbeClock(bool opening){
        superProbeNow=new DateTime(2034,1,4,opening?20:21,opening?0:2,opening?1:0,DateTimeKind.Local).ToUniversalTime();
        Require(superProbeNow.ToLocalTime().DayOfWeek==DayOfWeek.Wednesday);
        typeof(SuperGuildWar).GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>superProbeNow));
        Feature("SuperGuildWarPurses").GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>superProbeNow));
        Feature("SuperGuildWarPurses").GetField("nextPoll",Hidden).SetValue(null,DateTime.MaxValue);
    }
    static void SuperGuildCapture(){
        SuperProbeClock(true);Player(Kernel.Maps[1002]);Player(Kernel.Maps[1002]);nextUid=FirstUid;
        var leader=Player(Kernel.Maps[1002],true);var member=Player(Kernel.Maps[1002],true);
        // Affiliation is supplied; entry, capture and frozen prizes are earned below.
        var guild=new Guild(leader.Entity.Name){ID=50000,Name="ProbeSuperGuild"};
        foreach(var p in new[]{leader,member}){
            p.Guild=guild;p.Entity.GuildID=(ushort)guild.ID;
            p.AsMember=new Guild.Member(guild.ID){ID=p.Entity.UID,Name=p.Entity.Name,Level=p.Entity.Level,Class=p.Entity.Class,Rank=p==leader?Enums.GuildMemberRank.GuildLeader:Enums.GuildMemberRank.Member};
            p.Entity.GuildRank=(ushort)p.AsMember.Rank;guild.Members[p.Entity.UID]=p.AsMember;Require(EntityTable.SaveEntity(p));p.LoadItemStats();
        }
        guild.Leader=leader.AsMember;guild.LeaderName=leader.Entity.Name;GuildTable.Create(guild);Kernel.Guilds[guild.ID]=guild;
        Call(typeof(SuperGuildWar),"Tick");Require(SuperGuildWar.IsWar);
        var battle=(Guid)typeof(SuperGuildWar).GetField("battleId",Hidden).GetValue(null);
        foreach(var p in new[]{leader,member}){HeartTalk(p,380,23);Require(p.Entity.MapID==1234);}
        var pole=SuperGuildWar.Pole;leader.Entity.X=(ushort)(pole.X+1);leader.Entity.Y=pole.Y;
        Revora.Game.Attacking.Handle.ReceiveAttack(leader.Entity,pole,new Attack(true){AttackType=Attack.Melee,Attacker=leader.Entity.UID,Attacked=pole.UID},pole.MaxHitpoints,null);
        Require(SuperGuildWar.PoleKeeper1==guild && Convert.ToInt32(Sql("SELECT captures FROM revora_super_guild_battles WHERE id=@id","id",battle))==1);
        SuperProbeClock(false);Call(typeof(SuperGuildWar),"Tick");Require(!SuperGuildWar.IsWar);
        Require((long)Sql("SELECT count(*) FROM revora_super_guild_purses WHERE battle=@id AND guild_id=50000 AND claimed_at IS NULL","id",battle)==2);
        Sql("CREATE TABLE revora_super_process_probe(uid bigint PRIMARY KEY,battle uuid NOT NULL,fingerprint text,cp bigint,rvp bigint,gold bigint,paid_at timestamptz)");
        foreach(var p in new[]{leader,member}){
            Require(p.Account.State==AccountTable.AccountState.Player && !p.Fake && p.Entity.ConquerPoints==0 && p.Inventory.Count==0);
            Require(EntityTable.SaveEntity(p));
            Sql("INSERT INTO revora_super_process_probe(uid,battle) VALUES(@uid,@battle)","uid",(long)p.Entity.UID,"battle",battle);
        }
        System.Console.WriteLine("[super-process] super-capture-passed");Environment.Exit(0);
    }
    static void SuperGuildFinish(){
        SuperProbeClock(false);
        for(int n=0;n<2;n++){
            var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();Require(p.Entity.ConquerPoints==0 && p.Inventory.Count==0);
            Call(Feature("SuperGuildWarPurses"),"Claim",p);Call(Feature("SuperGuildWarPurses"),"Claim",p);
            Require((bool)Sql(@"SELECT r.claimed_at IS NOT NULL AND e.conquerpoints=r.cp AND e.revorapoint=r.rvp AND e.money=100000+r.gold
FROM revora_super_process_probe p JOIN revora_super_guild_purses r ON r.uid=p.uid AND r.battle=p.battle JOIN entities e ON e.uid=p.uid WHERE p.uid=@uid","uid",(long)p.Entity.UID));
            Require(p.Entity.ConquerPoints==Convert.ToUInt32(Sql("SELECT cp FROM revora_super_guild_purses WHERE uid=@uid","uid",(long)p.Entity.UID))
                && p.Entity.RevoraPoint==Convert.ToUInt32(Sql("SELECT rvp FROM revora_super_guild_purses WHERE uid=@uid","uid",(long)p.Entity.UID))
                && p.Entity.Money==100000+Convert.ToUInt64(Sql("SELECT gold FROM revora_super_guild_purses WHERE uid=@uid","uid",(long)p.Entity.UID)));
            var soul=p.Inventory.Objects.Single(i=>ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7);Require(soul.Bound && soul.Days==0);
            Require(p.Inventory.Count==(n==0?6:2) && p.Inventory.Objects.Count(i=>i.ID==3302411)==1 && p.Inventory.Objects.Where(i=>i.ID==723467).Sum(i=>(int)i.StackSize)==(n==0?4:0));
            Require((bool)Sql("SELECT @soul=ANY(rewards) FROM revora_super_guild_purses WHERE uid=@uid","soul",(long)soul.UID,"uid",(long)p.Entity.UID));
            Sql(@"UPDATE revora_super_process_probe SET fingerprint=@fingerprint,cp=@cp,rvp=@rvp,gold=@gold,
paid_at=(SELECT claimed_at FROM revora_super_guild_purses WHERE uid=@uid) WHERE uid=@uid",
                "fingerprint",TaoistFingerprint(p),"cp",(long)p.Entity.ConquerPoints,"rvp",(long)p.Entity.RevoraPoint,"gold",(long)p.Entity.Money,"uid",(long)p.Entity.UID);
        }
        System.Console.WriteLine("[super-process] super-finish-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Super finish did not close.");
    }
    static void SuperGuildVerify(){
        SuperProbeClock(false);
        for(int n=0;n<2;n++){
            var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();Call(Feature("SuperGuildWarPurses"),"Claim",p);
            Require(TaoistFingerprint(p)==(string)Sql("SELECT fingerprint FROM revora_super_process_probe WHERE uid=@uid","uid",(long)p.Entity.UID));
            Require((bool)Sql(@"SELECT e.conquerpoints=p.cp AND e.revorapoint=p.rvp AND e.money=p.gold AND r.claimed_at=p.paid_at
FROM revora_super_process_probe p JOIN entities e ON e.uid=p.uid JOIN revora_super_guild_purses r ON r.uid=p.uid AND r.battle=p.battle WHERE p.uid=@uid","uid",(long)p.Entity.UID));
            Require(p.Entity.GuildID==50000 && p.Guild!=null && p.AsMember.Rank==(n==0?Enums.GuildMemberRank.GuildLeader:Enums.GuildMemberRank.Member));
        }
        Require((long)Sql("SELECT count(*) FROM revora_super_guild_battles WHERE id IN(SELECT battle FROM revora_super_process_probe) AND state='won' AND keeper=50000 AND purses_frozen")==1);
        System.Console.WriteLine("[super-process] super-verify-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Super verify did not close.");
    }
    static bool occupationFixture;
    static void GuildOccupation(bool reload) {
        occupationFixture=true;nextUid=4294907201;
        guildProbeNow=new DateTime(2035,1,7,15,0,0,DateTimeKind.Local).ToUniversalTime();
        Feature("GuildSeasonWar").GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>guildProbeNow));
        Feature("GuildWarPurses").GetField("nextPoll",Hidden).SetValue(null,DateTime.MaxValue);
        if(!reload)for(uint uid=4294907201;uid<=4294907203;uid++)if((long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==0){nextUid=uid;Player(Kernel.Maps[1002]);}
        nextUid=4294907201;var people=Enumerable.Range(0,3).Select(i=>Player(Kernel.Maps[1002],true)).ToArray();
        if(!reload){
            var guild=new Guild(people[0].Entity.Name){ID=51001,Name="OccupationProbe"};
            foreach(var p in people){p.Guild=guild;p.Entity.GuildID=(ushort)guild.ID;p.AsMember=new Guild.Member(guild.ID){ID=p.Entity.UID,Name=p.Entity.Name,Level=p.Entity.Level,Class=p.Entity.Class,Rank=p==people[0]?Enums.GuildMemberRank.GuildLeader:Enums.GuildMemberRank.Member};p.Entity.GuildRank=(ushort)p.AsMember.Rank;guild.Members[p.Entity.UID]=p.AsMember;Require(EntityTable.SaveEntity(p));p.LoadItemStats();}
            guild.Leader=people[0].AsMember;guild.LeaderName=people[0].Entity.Name;GuildTable.Create(guild);Kernel.Guilds[guild.ID]=guild;
            GuildWar.Start();Require(GuildWar.IsWar);
            foreach(var p in people.Take(2)){p.BindWorldMap(1038);p.Entity.X=(ushort)(GuildWar.Pole.X+1);p.Entity.Y=GuildWar.Pole.Y;}
            var leader=people[0];Revora.Game.Attacking.Handle.ReceiveAttack(leader.Entity,GuildWar.Pole,new Attack(true){AttackType=Attack.Melee,Attacker=leader.Entity.UID,Attacked=GuildWar.Pole.UID},GuildWar.Pole.MaxHitpoints,null);
            Require(GuildWar.PoleKeeper==guild);
            Sql("CREATE FUNCTION reject_occupation() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.achievement_id=10302 THEN RETURN NULL; END IF; RETURN NEW; END $$;CREATE TRIGGER reject_occupation BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW EXECUTE FUNCTION reject_occupation();");
            guildProbeNow=guildProbeNow.AddHours(1);GuildWar.End();Require(GuildWar.IsWar);
            Require((long)Sql("SELECT count(*) FROM revora_guild_purses WHERE day='2035-01-07'")==0);
            Require((bool)Sql("SELECT state='active' AND NOT purses_frozen FROM revora_guild_battles WHERE day='2035-01-07'"));
            Sql("DROP TRIGGER reject_occupation ON revora_achievement_evidence;DROP FUNCTION reject_occupation();");GuildWar.End();Require(!GuildWar.IsWar);
            GuildWar.End();foreach(var p in people)Require(EntityTable.SaveEntity(p));
        }
        Require((long)Sql("SELECT count(*) FROM revora_guild_purses WHERE day='2035-01-07' AND guild_id=51001 AND claimed_at IS NULL")==2);
        for(int i=0;i<3;i++){
            var p=people[i];Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10302 AND source='guild-war-occupation-v1' AND source_key='2035-01-07'","uid",(long)p.Entity.UID)==(i<2?1:0));
            p.Entity.MyAchievement.Send();int bit=(10302/100-101)*32+10302%100-1;Require(((System.BitConverter.ToUInt32(p.Entity.MyAchievement.ToArray(),16+bit/32*4)&(1u<<(bit%32)))!=0)==(i<2));
            Require(p.Entity.ConquerPoints==0 && p.Entity.RevoraPoint==0 && p.Inventory.Count==0);
        }
        Require((bool)Sql("SELECT state='won' AND keeper=51001 AND purses_frozen FROM revora_guild_battles WHERE day='2035-01-07'"));
        System.Console.WriteLine("[occupation] remaining=0");System.Console.WriteLine("[occupation] occupation-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Occupation probe did not close.");
    }
    static DateTime guildProbeNow;
    static void GuildProbeClock(bool capture) {
        guildProbeNow=capture?new DateTime(2034,1,1,15,0,0,DateTimeKind.Local).ToUniversalTime():new DateTime(2034,1,1,16,1,0,DateTimeKind.Local).ToUniversalTime();
        Feature("GuildSeasonWar").GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>guildProbeNow));
        Feature("GuildWarPurses").GetField("nextPoll",Hidden).SetValue(null,DateTime.MaxValue);
    }
    static void GuildWarCapture() {
        GuildProbeClock(true);Player(Kernel.Maps[1002]);Player(Kernel.Maps[1002]);nextUid=FirstUid;
        var leader=Player(Kernel.Maps[1002],true);var member=Player(Kernel.Maps[1002],true);
        // Supply guild affiliation/rank, not a victory or purse. Event earning starts below.
        var guild=new Guild(leader.Entity.Name){ID=50000,Name="ProbeWarGuild"};
        foreach(var p in new[]{leader,member}) {
            p.Guild=guild;p.Entity.GuildID=(ushort)guild.ID;
            p.AsMember=new Guild.Member(guild.ID){ID=p.Entity.UID,Name=p.Entity.Name,Level=p.Entity.Level,Class=p.Entity.Class,Rank=p==leader?Enums.GuildMemberRank.GuildLeader:Enums.GuildMemberRank.Member};
            p.Entity.GuildRank=(ushort)p.AsMember.Rank;guild.Members[p.Entity.UID]=p.AsMember;
            Require(EntityTable.SaveEntity(p));p.LoadItemStats();
        }
        guild.Leader=leader.AsMember;guild.LeaderName=leader.Entity.Name;
        GuildTable.Create(guild);Kernel.Guilds[guild.ID]=guild;
        GuildWar.Start();Require(GuildWar.IsWar);
        foreach(var p in new[]{leader,member}) {p.BindWorldMap(1038);p.Entity.X=(ushort)(GuildWar.Pole.X+1);p.Entity.Y=GuildWar.Pole.Y;}
        Revora.Game.Attacking.Handle.ReceiveAttack(leader.Entity,GuildWar.Pole,new Attack(true){AttackType=Attack.Melee,Attacker=leader.Entity.UID,Attacked=GuildWar.Pole.UID},GuildWar.Pole.MaxHitpoints,null);
        Require(GuildWar.PoleKeeper==guild && (long)Sql("SELECT count(*) FROM revora_guild_captures WHERE day='2034-01-01' AND guild_id=50000")==1);
        guildProbeNow=guildProbeNow.AddHours(1);GuildWar.End();Require(!GuildWar.IsWar);
        Require((long)Sql("SELECT count(*) FROM revora_guild_purses WHERE day='2034-01-01' AND guild_id=50000 AND claimed_at IS NULL")==2);
        Require(leader.Entity.ConquerPoints==0 && member.Entity.ConquerPoints==0 && leader.Inventory.Count==0 && member.Inventory.Count==0);
        foreach(var p in new[]{leader,member})Require(EntityTable.SaveEntity(p));
        Sql("CREATE TABLE revora_guild_process_probe(uid bigint PRIMARY KEY,fingerprint text,cp bigint,rvp bigint,gold bigint,paid_at timestamptz); INSERT INTO revora_guild_process_probe(uid) VALUES(@leader),(@member)","leader",(long)leader.Entity.UID,"member",(long)member.Entity.UID);
        System.Console.WriteLine("[guild-process] guild-capture-passed");Environment.Exit(0);
    }
    static void GuildWarFinish() {
        GuildProbeClock(false);
        for(int n=0;n<2;n++) {
            var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();Require(p.Entity.ConquerPoints==0);
            p.BindWorldMap(1002);HeartTalk(p,10003,0);HeartTalk(p,10003,88);HeartTalk(p,10003,88);
            Require((long)Sql("SELECT count(*) FROM revora_guild_purses WHERE uid=@uid AND day='2034-01-01' AND claimed_at IS NOT NULL","uid",(long)p.Entity.UID)==1);
            Require(p.Entity.ConquerPoints==Convert.ToUInt32(Sql("SELECT cp FROM revora_guild_purses WHERE uid=@uid AND day='2034-01-01'","uid",(long)p.Entity.UID)));
            Require(p.Entity.RevoraPoint==Convert.ToUInt32(Sql("SELECT rvp FROM revora_guild_purses WHERE uid=@uid AND day='2034-01-01'","uid",(long)p.Entity.UID)));
            Require(p.Entity.Money==100000+Convert.ToUInt64(Sql("SELECT gold FROM revora_guild_purses WHERE uid=@uid AND day='2034-01-01'","uid",(long)p.Entity.UID)));
            var soul=p.Inventory.Objects.Single(i=>i.ID>=800000 && i.ID<900000);Require(soul.Bound && ConquerItemInformation.BaseInformations[soul.ID].PurificationLevel==7);
            Require(p.Inventory.Objects.Count(i=>i.ID==3302411)==1 && p.Inventory.Objects.Where(i=>i.ID==723467).Sum(i=>(int)i.StackSize)==(n==0?4:0));
            Sql("UPDATE revora_guild_process_probe SET fingerprint=(SELECT string_agg(uid::text||':'||id::text,',' ORDER BY uid) FROM items WHERE entityid=@uid),cp=@cp,rvp=@rvp,gold=@gold,paid_at=(SELECT claimed_at FROM revora_guild_purses WHERE uid=@uid AND day='2034-01-01') WHERE uid=@uid","uid",(long)p.Entity.UID,"cp",(long)p.Entity.ConquerPoints,"rvp",(long)p.Entity.RevoraPoint,"gold",(long)p.Entity.Money);
        }
        System.Console.WriteLine("[guild-process] guild-finish-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Guild finish did not close.");
    }
    static void GuildWarVerify() {
        GuildProbeClock(false);
        for(int n=0;n<2;n++) {
            var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();p.BindWorldMap(1002);HeartTalk(p,10003,88);
            Require(Equals(Sql("SELECT fingerprint FROM revora_guild_process_probe WHERE uid=@uid","uid",(long)p.Entity.UID),Sql("SELECT string_agg(uid::text||':'||id::text,',' ORDER BY uid) FROM items WHERE entityid=@uid","uid",(long)p.Entity.UID)));
            Require((bool)Sql("SELECT e.conquerpoints=p.cp AND e.revorapoint=p.rvp AND e.money=p.gold AND r.claimed_at=p.paid_at FROM revora_guild_process_probe p JOIN entities e ON e.uid=p.uid JOIN revora_guild_purses r ON r.uid=p.uid AND r.day='2034-01-01' WHERE p.uid=@uid","uid",(long)p.Entity.UID));
            Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10228","uid",(long)p.Entity.UID)==1);
        }
        Require((long)Sql("SELECT count(*) FROM revora_guild_battles WHERE day='2034-01-01' AND state='won' AND keeper=50000 AND purses_frozen")==1);
        System.Console.WriteLine("[guild-process] guild-verify-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Guild verify did not close.");
    }
}
