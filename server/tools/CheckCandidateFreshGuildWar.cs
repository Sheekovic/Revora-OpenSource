using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshGuildWarSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object('prior',@prior::json,'battle',(SELECT row_to_json(b) FROM revora_guild_battles b WHERE day='2041-03-10'),'captures',(SELECT json_agg(c ORDER BY number) FROM revora_guild_captures c WHERE day='2041-03-10'),'contributors',(SELECT json_agg(c ORDER BY number,uid,guild_id) FROM revora_guild_contributors c WHERE day='2041-03-10'),'purse',(SELECT row_to_json(r) FROM revora_guild_purses r WHERE day='2041-03-10' AND uid=@uid))::text","prior",FreshTerritoriesSnapshot(p),"uid",(long)p.Entity.UID));}
    static void FreshGuildWarProcess(string phase){
        DateTime now=new DateTime(2041,3,10,15,0,0,DateTimeKind.Local).ToUniversalTime();Require(now.ToLocalTime().DayOfWeek==DayOfWeek.Sunday);var service=Feature("GuildSeasonWar");service.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));Feature("GuildWarPurses").GetField("nextPoll",Hidden).SetValue(null,DateTime.MaxValue);
        var p=FreshLoadSaved(true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);Server.GamePool=new[]{p};Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p) && p.Guild!=null && p.Guild.Name=="FreshBanner" && p.Entity.GuildRank==1000);
        if((bool)Sql("SELECT to_regclass('revora_fresh_guild_war_probe') IS NULL")){
            Require(phase.EndsWith("war") && FreshTerritoriesSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_territories_probe")) && (long)Sql("SELECT count(*) FROM revora_guild_battles WHERE day='2041-03-10'")==0);Sql("CREATE TABLE revora_fresh_guild_war_probe(stage text,hits integer,snapshot text);INSERT INTO revora_fresh_guild_war_probe VALUES('new',0,@snapshot)","snapshot",FreshGuildWarSnapshot(p));
        }
        Require(FreshGuildWarSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_guild_war_probe")));string stage=Convert.ToString(Sql("SELECT stage FROM revora_fresh_guild_war_probe"));
        if(phase.EndsWith("war")){
            Require(stage=="new");GuildWar.Start();Require(GuildWar.IsWar);FreshClassicTalk(p,1002,380,6);Require(p.Entity.MapID==1038);var pole=GuildWar.Pole;p.Entity.X=(ushort)(pole.X+1);p.Entity.Y=pole.Y;p.Screen.Add(pole);p.Entity.PKMode=Enums.PKMode.PK;p.Attackable=true;p.CantAttack=Time32.Now.AddSeconds(-1);
            int hits=0;uint initial=pole.Hitpoints;while(GuildWar.PoleKeeper!=p.Guild && hits<10000){p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);Attack.HandleAttack(new Attack(true){Attacker=p.Entity.UID,Attacked=pole.UID,X=pole.X,Y=pole.Y,AttackType=Attack.Melee},p);hits++;if(hits==10)Require(pole.Hitpoints<initial || GuildWar.PoleKeeper==p.Guild);}
            Require(GuildWar.PoleKeeper==p.Guild && !p.Entity.Dead && !PirateFenced(p));now=now.AddHours(1);GuildWar.End();Require(!GuildWar.IsWar);new TitleStorage().Login(p);Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_guild_war_probe SET stage='won',hits=@hits,snapshot=@snapshot","hits",hits,"snapshot",FreshGuildWarSnapshot(p));System.Console.WriteLine("[fresh-guild-war] calculated melee hits="+hits);
        }else Require(phase.EndsWith("reload") && stage=="won");
        Require((long)Sql("SELECT count(*) FROM revora_guild_battles WHERE day='2041-03-10' AND state='won' AND keeper=@guild AND captures=1 AND purses_frozen","guild",(long)p.Guild.ID)==1 && (long)Sql("SELECT count(*) FROM revora_guild_purses WHERE day='2041-03-10' AND uid=@uid AND claimed_at IS NULL","uid",(long)p.Entity.UID)==1 && (long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10302 AND source='guild-war-occupation-v1' AND source_key='2041-03-10'","uid",(long)p.Entity.UID)==1);
        string exact=FreshGuildWarSnapshot(p);GuildWar.Start();Require(!GuildWar.IsWar);GuildWar.End();Require(exact==FreshGuildWarSnapshot(p) && exact==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_guild_war_probe")));System.Console.WriteLine("[career-process] "+phase+"-passed unpaid purse preserved");Server.CommandsAI("@close");throw new InvalidOperationException("Fresh guild war did not close.");
    }
}
