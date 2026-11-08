using System;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
internal static partial class PotencyHuntTests {
    static void VerifyPlayerRevival(string runtime,Map town) {
        Query("DROP TABLE entities;CREATE TABLE entities AS TABLE public.entities WITH NO DATA;CREATE TABLE titles (LIKE public.titles INCLUDING ALL)");
        foreach(string table in new[]{"teamarena","championarena","quests","status","reward","rewardclameid","merchant","dialyquests","activeness","house","pkexploits","achievement","itemadding","enemy"})
            Query("CREATE TABLE IF NOT EXISTS "+table+" AS TABLE public."+table+" WITH NO DATA");
        Query("CREATE TABLE prizes(uid bigint PRIMARY KEY,name text,prizes bytea)");
        StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
        RunesSystem.LoadRunesEXP();SpellTable.Load();Call(EvidenceType,null,"Initialize");
        foreach(ushort id in new ushort[]{1011,1020,1000,1004})World(runtime,id);
        ConquerItem.ItemUID=new Counter(7900000);
        Server.GamePool=new GameState[0];

        Func<GameState> player=()=>{var q=FullRunePlayer(town,135);q.Entity.DailySignDays=new byte[0];q.Entity.Mana=q.Entity.MaxMana=10000;EntityTable.SaveEntity(q);return q;};
        Action<GameState> dead=q=>{q.Entity.Hitpoints=0;q.Entity.AddFlag((ulong)FlagsUpdate.Flags.Dead,StatusFlagsBigVector32.PermanentFlag,true);q.Entity.AddFlag((ulong)FlagsUpdate.Flags.Ghost,StatusFlagsBigVector32.PermanentFlag,true);};
        Action<GameState,GameState,ushort> cast=(a,b,id)=>{a.Entity.AttackStamp=default(Time32);a.Screen.Add(b.Entity);
            new Revora.Game.Attacking.Handle(new Attack(true){AttackType=Attack.Magic,Decoded=true,Damage=id,Attacker=a.Entity.UID,Attacked=b.Entity.UID,X=b.Entity.X,Y=b.Entity.Y},a.Entity,b.Entity);};
        foreach(ushort id in new ushort[]{1050,1100}) {
            var a=player();var b=player();a.Spells[id]=new Spell(true){ID=id,Level=0};dead(b);a.Entity.AddFlag((ulong)FlagsUpdate.Flags.XPList,StatusFlagsBigVector32.PermanentFlag,true);cast(a,b,id);
            Check(!Fenced(a) && !b.Entity.Dead && b.Entity.Hitpoints==b.Entity.MaxHitpoints && HasAchievement(a,10902) && !HasAchievement(b,10902),
                "decoded revival spell restores another ordinary player and credits only caster: "+id);
            a=ReloadBall(a);a.Entity.MyAchievement.Send();Check(HasAchievement(a,10902),"revival credit survives full login: "+id);
        }
        foreach(string invalid in new[]{"alive","self","shackled","remote","fake","missing-skill","no-mana","dead-caster","other-map"}) {
            var a=player();var b=player();a.Spells[1100]=new Spell(true){ID=1100,Level=0};dead(b);
            if(invalid=="alive")b.Entity.BringToLife();if(invalid=="self"){b=a;dead(a);}if(invalid=="shackled")b.Entity.AddFlag((ulong)FlagsUpdate.Flags.SoulShackle,StatusFlagsBigVector32.PermanentFlag,true);
            if(invalid=="remote")b.Entity.X+=40;if(invalid=="fake")b.Fake=true;if(invalid=="missing-skill")a.Spells.Remove(1100);if(invalid=="no-mana")a.Entity.Mana=0;
            if(invalid=="dead-caster")dead(a);if(invalid=="other-map")b.BindWorldMap(1011);
            ushort mana=a.Entity.Mana;cast(a,b,1100);Check(!HasAchievement(a,10902) && (invalid=="alive" || b.Entity.Dead),"invalid revival cannot grant credit: "+invalid);
            Check(a.Entity.Mana==mana,"rejected revival does not consume mana: "+invalid);
        }
        var first=player();var second=player();var target=player();first.Spells[1100]=new Spell(true){ID=1100,Level=0};second.Spells[1100]=new Spell(true){ID=1100,Level=0};dead(target);
        Parallel.Invoke(()=>cast(first,target,1100),()=>cast(second,target,1100));
        Check(!target.Entity.Dead && (HasAchievement(first,10902)?1:0)+(HasAchievement(second,10902)?1:0)==1,
            "competing revival casts restore the target once and credit one caster");
        var xp=player();var fallen=player();xp.Spells[1050]=new Spell(true){ID=1050,Level=0};dead(fallen);cast(xp,fallen,1050);
        Check(fallen.Entity.Dead && !HasAchievement(xp,10902),"XP revival requires the actual XP circle");
        var failed=player();var restored=player();failed.Spells[1100]=new Spell(true){ID=1100,Level=0};dead(restored);
        Query("CREATE FUNCTION suppress_revival() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
        Query("CREATE TRIGGER suppress_revival BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW WHEN (NEW.achievement_id=10902) EXECUTE FUNCTION suppress_revival()");
        try{cast(failed,restored,1100);}finally{Query("DROP TRIGGER suppress_revival ON revora_achievement_evidence;DROP FUNCTION suppress_revival()");}
        Check(Fenced(failed) && !restored.Entity.Dead && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10902",failed.Entity.UID)==0,
            "failed credit insertion cannot fabricate persisted achievement or undo an already completed revival");
        Check(Scalar("SELECT count(*) FROM revora_player_revival_credit WHERE uid=@uid AND completed_at IS NULL",failed.Entity.UID)==1,
            "successful revival keeps its earned receipt when achievement insertion fails");
        failed=ReloadBall(failed);failed.Entity.MyAchievement.Send();
        Check(!Fenced(failed) && HasAchievement(failed,10902) && Scalar("SELECT count(*) FROM revora_player_revival_credit WHERE uid=@uid AND completed_at IS NOT NULL",failed.Entity.UID)==1,
            "full login recovers the saved revival credit without reviving another player");
        var killer=player();var victim=player();var healer=ReloadBall(player());
        NpcSpawn instructor;
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT cellx,celly,lookface FROM public.npcs WHERE id=10000 AND mapid=1004",c))
        using(var r=q.ExecuteReader()){if(!r.Read())throw new Exception("Taoist trainer missing.");instructor=new NpcSpawn{UID=10000,MapID=1004,X=Convert.ToUInt16(r[0]),Y=Convert.ToUInt16(r[1]),Mesh=Convert.ToUInt16(r[2]),Type=Enums.NpcType.Talker};}}
        Kernel.Maps[1004].AddNpc(instructor);healer.BindWorldMap(1004);healer.Entity.X=(ushort)(instructor.X-1);healer.Entity.Y=instructor.Y;healer.ActiveNpc=10000;
        SoulWire(healer,113,10000);SoulWire(healer,115,10000);
        Check(!Fenced(healer) && healer.Account.State==AccountTable.AccountState.Player && healer.Spells.ContainsKey(1100) && healer.Spells.ContainsKey(1050),
            "ordinary eligible Water Taoist learns both revival spells through trainer packet dispatch");
        healer=ReloadBall(healer);Check(healer.Spells.ContainsKey(1100) && healer.Spells.ContainsKey(1050),"trainer-learned revival spells survive production login");
        healer.BindWorldMap(town.ID);healer.Entity.X=victim.Entity.X;healer.Entity.Y=victim.Entity.Y;healer.Entity.Mana=10000;
        killer.Screen.Add(victim.Entity);victim.Screen.Add(killer.Entity);victim.Attackable=true;victim.ReviveStamp=Time32.Now.AddSeconds(-60);
        uint lethal=victim.Entity.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(killer.Entity,victim.Entity,new Attack(true){AttackType=Attack.Melee,Attacker=killer.Entity.UID,Attacked=victim.Entity.UID},ref lethal,null);
        Check(victim.Entity.Dead,"revival journey begins with an actual lethal player attack");
        cast(healer,victim,1100);Check(!Fenced(healer) && !victim.Entity.Dead && HasAchievement(healer,10902),
            "actual combat death followed by decoded revival earns durable caster credit");
        var learner=ReloadBall(player());learner.BindWorldMap(1004);learner.Entity.X=(ushort)(instructor.X-1);learner.Entity.Y=instructor.Y;learner.ActiveNpc=10000;
        Query("CREATE FUNCTION suppress_revival_lesson() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
        Query("CREATE TRIGGER suppress_revival_lesson BEFORE INSERT ON skills FOR EACH ROW WHEN (NEW.id=1100) EXECUTE FUNCTION suppress_revival_lesson()");
        try{SoulWire(learner,113,10000);}finally{Query("DROP TRIGGER suppress_revival_lesson ON skills;DROP FUNCTION suppress_revival_lesson()");}
        Check(Fenced(learner) && !learner.Spells.ContainsKey(1100) && Scalar("SELECT count(*) FROM skills WHERE entityid=@uid AND id=1100",learner.Entity.UID)==0,
            "failed trainer save cannot expose an unsaved revival spell");
        learner=ReloadBall(learner);learner.BindWorldMap(1004);learner.Entity.X=(ushort)(instructor.X-1);learner.Entity.Y=instructor.Y;learner.ActiveNpc=10000;
        SoulWire(learner,113,10000);SoulWire(learner,113,10000);
        Check(!Fenced(learner) && learner.Spells.ContainsKey(1100) && Scalar("SELECT count(*) FROM skills WHERE entityid=@uid AND id=1100",learner.Entity.UID)==1,
            "reloaded learner retries the trainer lesson once without duplicate skill rows");



    }
}
