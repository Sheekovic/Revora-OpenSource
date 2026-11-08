using System;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Guild=Revora.Game.ConquerStructures.Society.Guild;
using Revora.Network.GamePackets;
internal static partial class PotencyHuntTests {
    static void VerifyPlayerDefeat(string runtime,Map town){
        Query("DROP TABLE entities;CREATE TABLE entities AS TABLE public.entities WITH NO DATA;CREATE TABLE titles (LIKE public.titles INCLUDING ALL)");
        foreach(string table in new[]{"teamarena","championarena","quests","status","reward","rewardclameid","merchant","dialyquests","activeness","house","pkexploits","achievement","itemadding","enemy"})
            Query("CREATE TABLE IF NOT EXISTS "+table+" AS TABLE public."+table+" WITH NO DATA");
        Query("CREATE TABLE prizes(uid bigint PRIMARY KEY,name text,prizes bytea)");
        StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
        RunesSystem.LoadRunesEXP();SpellTable.Load();Call(EvidenceType,null,"Initialize");
        foreach(ushort id in new ushort[]{1011,1020,1000,1004})World(runtime,id);
        ConquerItem.ItemUID=new Counter(7900000);
        Server.GamePool=new GameState[0];


        Func<GameState> player=()=>{var p=FullRunePlayer(town,15);p.Entity.DailySignDays=new byte[0];EntityTable.SaveEntity(p);return p;};
        Action<GameState,GameState,uint> hit=(a,b,damage)=>{a.Screen.Add(b.Entity);b.Screen.Add(a.Entity);b.Attackable=true;b.ReviveStamp=Time32.Now.AddSeconds(-60);
            Revora.Game.Attacking.Handle.ReceiveAttack(a.Entity,b.Entity,new Attack(true){AttackType=Attack.Melee,Attacker=a.Entity.UID,Attacked=b.Entity.UID},ref damage,null);};
        var service=typeof(Entity).Assembly.GetType("Revora.Game.Features.PlayerDefeatCredit",true);
        foreach(int kind in new[]{10605,10606,10607,10611}){
            var a=player();var b=player();
            if(kind==10605)b.Entity.AddFlag((ulong)FlagsUpdate.Flags.FlashingName,20,true);
            if(kind==10606)b.Entity.PKPoints=30;if(kind==10607)b.Entity.PKPoints=100;
            if(kind==10611){
                hit(b,a,a.Entity.Hitpoints);Check(a.Entity.Dead && a.Enemy.ContainsKey(b.Entity.UID),"actual defeat creates the victim's enemy relation");
                a.Entity.BringToLife();EntityTable.SaveEntity(a);a=ReloadBall(a);Revora.Database.KnownPersons.LoadEnemy(a);
                a.BindWorldMap(town.ID);a.Entity.X=b.Entity.X;a.Entity.Y=b.Entity.Y;
                Check(a.Enemy.ContainsKey(b.Entity.UID),"enemy relation reloads through the production social loader");}
            hit(a,b,1);Check(!HasAchievement(a,kind),"nonlethal damage earns no defeat credit: "+kind);
            var snapshot=Call(service,null,"Capture",b.Entity,a.Entity);
            hit(a,b,b.Entity.Hitpoints);Check(b.Entity.Dead && !Fenced(a) && HasAchievement(a,kind) && !HasAchievement(b,kind),"actual lethal combat credits only qualifying killer: "+kind);
            System.Threading.Tasks.Parallel.For(0,16,n=>Call(service,null,"Accepted",snapshot));
            Check(Scalar("SELECT count(*) FROM revora_player_defeat_credit WHERE uid=@uid AND kind="+kind,a.Entity.UID)==1,"replayed accepted death cannot duplicate credit: "+kind);
            if(kind==10611)Check(HasAchievement(a,10605),"revenge against a flashing attacker also earns blue-name credit");
            a=ReloadBall(a);a.Entity.MyAchievement.Send();Check(HasAchievement(a,kind),"defeat achievement survives full login: "+kind);
        }
        foreach(string invalid in new[]{"white","fake-victim","fake-killer","companion","self","dead","other-map"}){
            var a=player();var b=player();b.Entity.AddFlag((ulong)FlagsUpdate.Flags.FlashingName,20,true);
            if(invalid=="white")b.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.FlashingName);
            if(invalid=="fake-victim")b.Fake=true;if(invalid=="fake-killer")a.Fake=true;if(invalid=="companion")b.Entity.Companion=true;
            if(invalid=="self")b=a;if(invalid=="dead")b.Entity.Hitpoints=0;if(invalid=="other-map")b.BindWorldMap(1011);
            Check(Call(service,null,"Capture",b.Entity,a.Entity)==null,"ineligible defeat has no snapshot: "+invalid);
        }
        foreach(var flag in new[]{FlagsUpdate.Flags.FlashingName,FlagsUpdate.Flags.RedName,FlagsUpdate.Flags.BlackName}){
            var a=player();var b=player();b.Entity.AddFlag((ulong)flag,20,true);
            var state=b.Entity.BitVector.ArrayFlags[(int)flag];var deadline=state.Timer.AddSeconds(state.Secounds);
            Check((bool)Call(service,null,"Active",b.Entity,flag,deadline.AddTicks(-1)) && !(bool)Call(service,null,"Active",b.Entity,flag,deadline),"name flag ceases qualifying at its exact deadline: "+flag);
            state.Timer=DateTime.Now.AddMinutes(-1);
            Check(b.Entity.ContainsFlag((ulong)flag) && Call(service,null,"Capture",b.Entity,a.Entity)==null,"expired flag awaiting cleanup does not qualify: "+flag);
            hit(a,b,b.Entity.Hitpoints);
            Check(b.Entity.Dead && Scalar("SELECT count(*) FROM revora_player_defeat_credit WHERE uid=@uid",a.Entity.UID)==0,"actual lethal hit cannot earn stale name-state credit: "+flag);
        }
        var premature=player();var living=player();living.Entity.AddFlag((ulong)FlagsUpdate.Flags.FlashingName,20,true);
        Call(service,null,"Accepted",Call(service,null,"Capture",living.Entity,premature.Entity));
        Check(!HasAchievement(premature,10605) && Scalar("SELECT count(*) FROM revora_player_defeat_credit WHERE uid=@uid",premature.Entity.UID)==0,"a snapshot without an accepted death grants nothing");
        var unsaved=player();var lost=player();lost.Entity.AddFlag((ulong)FlagsUpdate.Flags.FlashingName,20,true);
        Query("CREATE FUNCTION suppress_defeat_receipt() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
        Query("CREATE TRIGGER suppress_defeat_receipt BEFORE INSERT ON revora_player_defeat_credit FOR EACH ROW EXECUTE FUNCTION suppress_defeat_receipt()");
        try{hit(unsaved,lost,lost.Entity.Hitpoints);}finally{Query("DROP TRIGGER suppress_defeat_receipt ON revora_player_defeat_credit;DROP FUNCTION suppress_defeat_receipt()");}
        Check(Fenced(unsaved) && lost.Entity.Dead && Scalar("SELECT count(*) FROM revora_player_defeat_credit WHERE uid=@uid",unsaved.Entity.UID)==0,"initial receipt failure fences killer without fabricating durable credit");
        unsaved=ReloadBall(unsaved);unsaved.Entity.MyAchievement.Send();Check(!HasAchievement(unsaved,10605),"login cannot invent credit after initial receipt failure");
        var failed=player();var victim=player();victim.Entity.AddFlag((ulong)FlagsUpdate.Flags.FlashingName,20,true);
        Query("CREATE FUNCTION suppress_defeat() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
        Query("CREATE TRIGGER suppress_defeat BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW WHEN (NEW.achievement_id=10605) EXECUTE FUNCTION suppress_defeat()");
        try{hit(failed,victim,victim.Entity.Hitpoints);}finally{Query("DROP TRIGGER suppress_defeat ON revora_achievement_evidence;DROP FUNCTION suppress_defeat()");}
        Check(Fenced(failed) && victim.Entity.Dead && Scalar("SELECT count(*) FROM revora_player_defeat_credit WHERE uid=@uid AND completed_at IS NULL",failed.Entity.UID)==1,"failed evidence write preserves accepted defeat receipt");
        failed=ReloadBall(failed);failed.Entity.MyAchievement.Send();Check(!Fenced(failed) && HasAchievement(failed,10605),"login recovers defeat credit without another kill");

        Query("CREATE TABLE guildenemy AS TABLE public.guildenemy WITH NO DATA");
        Query("CREATE TABLE guildally AS TABLE public.guildally WITH NO DATA");
        uint guildId=50000;
        Action<GameState> join=p=>{
            var g=new Guild(p.Entity.Name){ID=++guildId,Name="DefeatGuild"+guildId};Kernel.Guilds[g.ID]=g;
            p.Guild=g;p.Entity.CharBuffer._GuildID=g.ID;p.AsMember=new Guild.Member(g.ID){ID=p.Entity.UID,Rank=Revora.Game.Enums.GuildMemberRank.Member};
            g.Members[p.Entity.UID]=p.AsMember;EntityTable.UpdateGuildID(p);
            Check(Scalar("SELECT guildid FROM entities WHERE uid=@uid",p.Entity.UID)==g.ID,"ordinary guild membership saved");
        };
        Action<GameState,GameState> rival=(a,b)=>{a.Guild.Enemy.Add(b.Guild.ID,b.Guild);GuildTable.AddEnemy(a.Guild,b.Guild.ID);};
        foreach(string mode in new[]{"valid","reverse","unsaved-rivalry","saved-membership-mismatch","stale-member","removed-after-capture","projection-failure","receipt-failure"}){
            var a=player();var b=player();join(a);join(b);
            if(mode=="reverse")rival(b,a);else rival(a,b);
            if(mode=="valid"){
                a.Guild.Enemy.Clear();GuildTable.LoadAllyEnemy();
                Check(a.Guild.Enemy.ContainsKey(b.Guild.ID),"directed rivalry reloads through the production loader");
            }
            if(mode=="unsaved-rivalry")Query("DELETE FROM guildenemy WHERE guildid=@guild","guild",(long)a.Guild.ID);
            if(mode=="saved-membership-mismatch")Query("UPDATE entities SET guildid=0 WHERE uid=@uid","uid",(long)b.Entity.UID);
            if(mode=="stale-member")b.AsMember=new Guild.Member(b.Guild.ID){ID=b.Entity.UID};
            hit(a,b,1);Check(!HasAchievement(a,10227),"nonlethal guild combat grants nothing: "+mode);
            var snapshot=Call(service,null,"Capture",b.Entity,a.Entity);
            if(mode=="removed-after-capture"){a.Guild.Enemy.Remove(b.Guild.ID);GuildTable.RemoveEnemy(a.Guild,b.Guild.ID);}
            if(mode=="projection-failure"){
                Query("CREATE FUNCTION suppress_guild_defeat() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
                Query("CREATE TRIGGER suppress_guild_defeat BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW WHEN (NEW.achievement_id=10227) EXECUTE FUNCTION suppress_guild_defeat()");
            }
            if(mode=="receipt-failure"){
                Query("CREATE FUNCTION suppress_guild_receipt() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
                Query("CREATE TRIGGER suppress_guild_receipt BEFORE INSERT ON revora_enemy_guild_defeat FOR EACH ROW EXECUTE FUNCTION suppress_guild_receipt()");
            }
            try{hit(a,b,b.Entity.Hitpoints);}finally{
                if(mode=="projection-failure")Query("DROP TRIGGER suppress_guild_defeat ON revora_achievement_evidence;DROP FUNCTION suppress_guild_defeat()");
                if(mode=="receipt-failure")Query("DROP TRIGGER suppress_guild_receipt ON revora_enemy_guild_defeat;DROP FUNCTION suppress_guild_receipt()");
            }
            Check(b.Entity.Dead && !HasAchievement(b,10227),"accepted ordinary death never credits victim: "+mode);
            if(mode=="valid"){
                Check(!Fenced(a) && HasAchievement(a,10227) && !HasAchievement(a,10611),"enemy-guild kill is distinct from personal revenge");
                System.Threading.Tasks.Parallel.For(0,16,n=>Call(service,null,"Accepted",snapshot));
                Check(Scalar("SELECT count(*) FROM revora_enemy_guild_defeat WHERE uid=@uid",a.Entity.UID)==1,"guild defeat replay produces one receipt");
                a=ReloadBall(a);a.Entity.MyAchievement.Send();Check(HasAchievement(a,10227),"guild defeat survives ordinary login");
            }else if(mode=="projection-failure"){
                Check(Fenced(a) && Scalar("SELECT count(*) FROM revora_enemy_guild_defeat WHERE uid=@uid AND completed_at IS NULL",a.Entity.UID)==1,"guild projection failure retains receipt");
                a.Guild.Enemy.Remove(b.Guild.ID);GuildTable.RemoveEnemy(a.Guild,b.Guild.ID);
                a=ReloadBall(a);a.Entity.MyAchievement.Send();Check(!Fenced(a) && HasAchievement(a,10227),"guild defeat recovers without another kill or current rivalry");
            }else{
                Call(service,null,"Accepted",snapshot);
                Check(!HasAchievement(a,10227) && Scalar("SELECT count(*) FROM revora_enemy_guild_defeat WHERE uid=@uid",a.Entity.UID)==0,"ineligible or unsaved guild defeat grants nothing: "+mode);
                if(mode=="receipt-failure"){
                    Check(Fenced(a),"initial guild receipt failure fences killer");a=ReloadBall(a);a.Entity.MyAchievement.Send();
                    Check(!HasAchievement(a,10227),"login cannot invent a missing guild receipt");
                }
            }
        }
    }
}
