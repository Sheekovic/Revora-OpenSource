using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.ConquerStructures.Society;
using Revora.Network.GamePackets;
using Console=System.Console;

internal static partial class CheckCandidateArenaLifecycle {
    static bool guildInterruptionFixture;
    static DateTime guildInterruptSuper,guildInterruptElite;
    static string GuildInterruptAssets(GameState p) {
        return Convert.ToString(Sql(@"SELECT json_build_object(
'items',(SELECT json_agg(r ORDER BY uid) FROM items r WHERE entityid=@uid),
'purse',(SELECT json_build_array(conquerpoints,money,revorapoint) FROM entities WHERE uid=@uid),
'guild',(SELECT row_to_json(r) FROM guilds r WHERE id=51101),
'supercaptures',(SELECT json_agg(r ORDER BY number) FROM revora_super_guild_captures r WHERE battle=(SELECT super FROM revora_guild_interrupt_probe)),
'elitecaptures',(SELECT json_agg(r ORDER BY number) FROM revora_elite_guild_captures r WHERE battle=(SELECT elite FROM revora_guild_interrupt_probe)))::text","uid",(long)p.Entity.UID));
    }
    static string GuildInterruptResults() {
        return Convert.ToString(Sql(@"SELECT json_build_object(
'super',(SELECT json_agg(r ORDER BY id) FROM revora_super_guild_battles r WHERE id=(SELECT super FROM revora_guild_interrupt_probe)),
'elite',(SELECT json_agg(r ORDER BY id) FROM revora_elite_guild_battles r WHERE id=(SELECT elite FROM revora_guild_interrupt_probe)))::text"));
    }
    static void GuildInterruptionProcess(string phase) {
        guildInterruptionFixture=true;const uint uid=4294918701;bool capture=phase.EndsWith("capture");
        guildInterruptSuper=new DateTime(2034,1,11,20,0,5,DateTimeKind.Local).ToUniversalTime();
        guildInterruptElite=new DateTime(2034,1,9,21,1,5,DateTimeKind.Local).ToUniversalTime();
        typeof(SuperGuildWar).GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>guildInterruptSuper));
        typeof(EliteGuildWar).GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>guildInterruptElite));
        if(capture) {
            Require((bool)Sql("SELECT to_regclass('revora_guild_interrupt_probe') IS NULL"));
            Require((long)Sql("SELECT count(*) FROM guilds WHERE id=51101")==0);
            nextUid=uid;Player(Kernel.Maps[1002]);
            Sql(@"CREATE TABLE revora_guild_interrupt_probe(super uuid,elite uuid,super_keeper bigint,elite_keeper bigint,assets text,results text);
INSERT INTO revora_guild_interrupt_probe(super_keeper,elite_keeper) VALUES(
COALESCE((SELECT keeper FROM revora_super_guild_battles WHERE state='won' ORDER BY ended_at DESC,id LIMIT 1),0),
COALESCE((SELECT keeper FROM revora_elite_guild_battles WHERE state='won' ORDER BY ended_at DESC,id LIMIT 1),0))");
        }
        nextUid=uid;var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();Server.GamePool=new[]{p};
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if(capture) {
            // Only affiliation is supplied. Both captures use normal NPC admission and damage handling.
            var guild=new Guild(p.Entity.Name){ID=51101,Name="InterruptWar"};p.Guild=guild;p.Entity.GuildID=51101;
            p.AsMember=new Guild.Member(guild.ID){ID=uid,Name=p.Entity.Name,Level=p.Entity.Level,Class=p.Entity.Class,Rank=Enums.GuildMemberRank.GuildLeader};
            p.Entity.GuildRank=(ushort)p.AsMember.Rank;guild.Members[uid]=p.AsMember;guild.Leader=p.AsMember;guild.LeaderName=p.Entity.Name;
            Require(EntityTable.SaveEntity(p));GuildTable.Create(guild);Kernel.Guilds[guild.ID]=guild;
            Call(typeof(SuperGuildWar),"Tick");Require(SuperGuildWar.IsWar);
            Sql("UPDATE revora_guild_interrupt_probe SET super=@id","id",(Guid)typeof(SuperGuildWar).GetField("battleId",Hidden).GetValue(null));
            HeartTalk(p,380,23);Require(p.Entity.MapID==1234);var pole=SuperGuildWar.Pole;p.Entity.X=(ushort)(pole.X+1);p.Entity.Y=pole.Y;
            Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,pole,new Attack(true){AttackType=Attack.Melee,Attacker=uid,Attacked=pole.UID},pole.MaxHitpoints,null);
            Require(SuperGuildWar.PoleKeeper1==guild);
            p.Entity.Teleport(1002,408,330);Call(typeof(EliteGuildWar),"Tick");Require(EliteGuildWar.IsWar);
            Sql("UPDATE revora_guild_interrupt_probe SET elite=@id","id",(Guid)typeof(EliteGuildWar).GetField("battleId",Hidden).GetValue(null));
            HeartTalk(p,115522,1);Require(p.Entity.MapID==2071);pole=EliteGuildWar.Poles;p.Entity.X=(ushort)(pole.X+1);p.Entity.Y=pole.Y;
            Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,pole,new Attack(true){AttackType=Attack.Melee,Attacker=uid,Attacked=pole.UID},pole.MaxHitpoints,null);
            Require(EliteGuildWar.PoleKeeper==guild);
            Require((bool)Sql(@"SELECT s.state='active' AND s.captures=1 AND s.keeper=51101 AND NOT s.purses_frozen
AND e.state='active' AND e.captures=1 AND e.keeper=51101 AND e.soul_type=0
FROM revora_guild_interrupt_probe p JOIN revora_super_guild_battles s ON s.id=p.super JOIN revora_elite_guild_battles e ON e.id=p.elite"));
            Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_guild_interrupt_probe SET assets=@assets","assets",GuildInterruptAssets(p));
        } else {
            Require(!SuperGuildWar.IsWar && !EliteGuildWar.IsWar && p.Entity.GuildID==51101 && p.Guild!=null);
            Console.WriteLine("[guild-interrupt] restored="+Convert.ToString(Sql(@"SELECT json_build_object('super',json_build_array(s.state,s.captures,s.keeper,s.purses_frozen,s.ended_at),'elite',json_build_array(e.state,e.captures,e.keeper,e.soul_type,e.claimed_at,e.ended_at))::text FROM revora_guild_interrupt_probe p JOIN revora_super_guild_battles s ON s.id=p.super JOIN revora_elite_guild_battles e ON e.id=p.elite")));
            Require((bool)Sql(@"SELECT s.state='cancelled' AND s.captures=1 AND s.keeper=51101 AND s.purses_frozen AND s.ended_at IS NOT NULL
AND e.state='cancelled' AND e.captures=1 AND e.keeper=51101 AND e.soul_type=0 AND e.claimed_at IS NULL AND e.ended_at IS NOT NULL
FROM revora_guild_interrupt_probe p JOIN revora_super_guild_battles s ON s.id=p.super JOIN revora_elite_guild_battles e ON e.id=p.elite"));
            uint expected=Convert.ToUInt32(Sql("SELECT super_keeper FROM revora_guild_interrupt_probe"));
            Require((SuperGuildWar.PoleKeeper1==null?0:SuperGuildWar.PoleKeeper1.ID)==expected);
            expected=Convert.ToUInt32(Sql("SELECT elite_keeper FROM revora_guild_interrupt_probe"));
            Require((EliteGuildWar.PoleKeeper==null?0:EliteGuildWar.PoleKeeper.ID)==expected);
            Require(SuperGuildWar.Pole.Hitpoints==SuperGuildWar.Pole.MaxHitpoints && EliteGuildWar.Poles.Hitpoints==EliteGuildWar.Poles.MaxHitpoints);
            // Initialize seals all terminal battles, including cancelled ones, without creating purse rows.
            // Repeated opening ticks for the cancelled schedule must not restart or award the interrupted battle.
            for(int n=0;n<2;n++) {guildInterruptSuper=guildInterruptSuper.AddSeconds(4);guildInterruptElite=guildInterruptElite.AddSeconds(4);Call(typeof(SuperGuildWar),"Tick");Call(typeof(EliteGuildWar),"Tick");}
            Require(!SuperGuildWar.IsWar && !EliteGuildWar.IsWar);
            // Exercise the actual post-load login handler from both saved war maps.
            // Positions are controlled because earlier phases already returned this profile to town.
            foreach(ushort warMap in new ushort[]{1234,2071}) {
                p.Entity.Teleport(warMap,50,50);
                GeneralData.LoginMessages(p);
                Require(p.Entity.MapID==1002 && p.Entity.X==408 && p.Entity.Y==330 && !PirateFenced(p));
                Console.WriteLine("[guild-interrupt] login-return map="+warMap+" passed");
            }
            HeartTalk(p,380,23);Require(p.Entity.MapID==1002);HeartTalk(p,115522,1);Require(p.Entity.MapID==1002);HeartTalk(p,115522,5);HeartTalk(p,115522,5);
            Call(Feature("SuperGuildWarPurses"),"Claim",p);Call(Feature("SuperGuildWarPurses"),"Claim",p);
            Require(GuildInterruptAssets(p)==Convert.ToString(Sql("SELECT assets FROM revora_guild_interrupt_probe")));
            if(phase.EndsWith("recover"))Sql("UPDATE revora_guild_interrupt_probe SET results=@results","results",GuildInterruptResults());
            else Require(GuildInterruptResults()==Convert.ToString(Sql("SELECT results FROM revora_guild_interrupt_probe")));
            Require(EntityTable.SaveEntity(p));
        }
        Require((long)Sql("SELECT count(*) FROM revora_super_guild_purses WHERE battle=(SELECT super FROM revora_guild_interrupt_probe)")==0);
        Require(!PirateFenced(p));Console.WriteLine("[career-process] "+phase+"-passed");
        if(capture)Environment.Exit(0);
        Server.CommandsAI("@close");throw new InvalidOperationException("Guild interruption did not close.");
    }
}
