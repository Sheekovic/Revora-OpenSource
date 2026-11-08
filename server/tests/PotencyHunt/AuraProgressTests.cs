using System;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
internal static partial class PotencyHuntTests {
    static void VerifyAuraProgress(string runtime,Map town){
        Query("DROP TABLE entities;CREATE TABLE entities AS TABLE public.entities WITH NO DATA;CREATE TABLE titles (LIKE public.titles INCLUDING ALL)");
        foreach(string table in new[]{"teamarena","championarena","quests","status","reward","rewardclameid","merchant","dialyquests","activeness","house","pkexploits","achievement","itemadding"})
            Query("CREATE TABLE IF NOT EXISTS "+table+" AS TABLE public."+table+" WITH NO DATA");
        Query("CREATE TABLE prizes(uid bigint PRIMARY KEY,name text,prizes bytea)");
        StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
        RunesSystem.LoadRunesEXP();SpellTable.Load();Call(EvidenceType,null,"Initialize");
        foreach(ushort id in new ushort[]{1011,1020,1000,1004})World(runtime,id);
        ConquerItem.ItemUID=new Counter(7900000);
        Server.GamePool=new GameState[0];

        Func<GameState> player=()=>{var q=FullRunePlayer(town,65);q.Entity.DailySignDays=new byte[0];q.Entity.Mana=q.Entity.MaxMana=10000;q.Entity.Stamina=100;EntityTable.SaveEntity(q);return q;};
        Action<GameState,ushort> learn=(p,id)=>p.Spells[id]=new Spell(true){ID=id,Level=0};
        Action<GameState,ushort> cast=(p,id)=>{p.Entity.AttackStamp=default(Time32);new Revora.Game.Attacking.Handle(new Attack(true){AttackType=Attack.Magic,Decoded=true,Damage=id,Attacker=p.Entity.UID,Attacked=p.Entity.UID,X=p.Entity.X,Y=p.Entity.Y},p.Entity,p.Entity);};
        foreach(ushort id in new ushort[]{10395,10410,10420,10421,10422,10423,10424}){
            var p=player();learn(p,id);cast(p,id);
            Check(!Fenced(p) && p.Entity.Aura_isActive && p.Entity.ContainsFlag(p.Entity.Aura_actType) && HasAchievement(p,10919),"actual decoded Aura activation earns caster credit: "+id);
            cast(p,id);Check(!p.Entity.Aura_isActive && Scalar("SELECT count(*) FROM revora_aura_credit WHERE uid=@uid",p.Entity.UID)==1,"toggle-off creates no additional Aura receipt: "+id);
            p=ReloadBall(p);p.Entity.MyAchievement.Send();Check(HasAchievement(p,10919),"Aura achievement survives full login: "+id);
        }
        foreach(string invalid in new[]{"unlearned","dead","fake","no-mana"}){
            var p=player();learn(p,10420);if(invalid=="unlearned")p.Spells.Remove(10420);if(invalid=="dead")p.Entity.Hitpoints=0;if(invalid=="fake")p.Fake=true;if(invalid=="no-mana")p.Entity.Mana=0;
            cast(p,10420);p.Fake=false;Check(!p.Entity.Aura_isActive && !HasAchievement(p,10919),"rejected Aura grants no credit: "+invalid);
        }
        Call(typeof(Revora.Server).Assembly.GetType("Revora.Game.Features.MonkTraining",true),null,"Initialize");
        NpcSpawn instructor;
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT cellx,celly,lookface FROM public.npcs WHERE id=4271 AND mapid=1004",c))
        using(var r=q.ExecuteReader()){if(!r.Read())throw new Exception("Monk trainer missing.");instructor=new NpcSpawn{UID=4271,MapID=1004,X=Convert.ToUInt16(r[0]),Y=Convert.ToUInt16(r[1]),Mesh=Convert.ToUInt16(r[2]),Type=Enums.NpcType.Talker};}}
        Kernel.Maps[1004].AddNpc(instructor);
        Action<GameState> approach=p=>{p.BindWorldMap(1004);p.Entity.X=(ushort)(instructor.X-1);p.Entity.Y=instructor.Y;p.ActiveNpc=4271;};
        foreach(var row in new[]{new[]{13,10395,20},new[]{14,10410,20},new[]{15,10420,100},new[]{16,10421,100},new[]{17,10422,100},new[]{18,10423,100},new[]{19,10424,100}}){
            var novice=ReloadBall(player());approach(novice);novice.Entity.Level=(byte)(row[2]-1);SoulWire(novice,(byte)row[0],4271);
            Check(!Fenced(novice) && !novice.Spells.ContainsKey((ushort)row[1]),"Aura lesson rejects below minimum level: "+row[1]);
            novice.Entity.Level=(byte)row[2];SoulWire(novice,(byte)row[0],4271);
            Check(!Fenced(novice) && novice.Spells.ContainsKey((ushort)row[1]),"Aura lesson accepts exact minimum level: "+row[1]);
        }
        foreach(string invalid in new[]{"class","remote","wrong-npc","dead"}){
            var denied=ReloadBall(player());approach(denied);
            if(invalid=="class")denied.Entity.Class=15;if(invalid=="remote")denied.Entity.X+=30;if(invalid=="dead")denied.Entity.Hitpoints=0;
            SoulWire(denied,15,invalid=="wrong-npc"?1u:4271u);
            Check(!denied.Spells.ContainsKey(10420) && Scalar("SELECT count(*) FROM revora_monk_training WHERE uid=@uid",denied.Entity.UID)==0,"Aura trainer rejects invalid access: "+invalid);
        }
        var apprentice=ReloadBall(player());approach(apprentice);
        foreach(byte option in new byte[]{13,14,15,16,17,18,19})SoulWire(apprentice,option,4271);
        Check(!Fenced(apprentice) && apprentice.Account.State==AccountTable.AccountState.Player && apprentice.Spells.ContainsKey(10424),"ordinary Monk learns Auras through actual NPC dispatch without scrolls");
        apprentice=ReloadBall(apprentice);
        foreach(ushort id in new ushort[]{10395,10410,10420,10421,10422,10423,10424})Check(apprentice.Spells.ContainsKey(id),"trainer Aura survives full login: "+id);
        approach(apprentice);SoulWire(apprentice,15,4271);
        Check(Scalar("SELECT count(*) FROM revora_monk_training WHERE uid=@uid",apprentice.Entity.UID)==7,"repeated Aura lesson creates no duplicate receipt");
        apprentice.Entity.Mana=10000;cast(apprentice,10420);Check(HasAchievement(apprentice,10919),"trainer to reconnect to actual Aura cast earns achievement");
        var learner=ReloadBall(player());approach(learner);
        Query("CREATE FUNCTION suppress_aura_lesson() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
        Query("CREATE TRIGGER suppress_aura_lesson BEFORE INSERT ON skills FOR EACH ROW WHEN (NEW.id=10420) EXECUTE FUNCTION suppress_aura_lesson()");
        try{SoulWire(learner,15,4271);}finally{Query("DROP TRIGGER suppress_aura_lesson ON skills;DROP FUNCTION suppress_aura_lesson()");}
        Check(Fenced(learner) && !learner.Spells.ContainsKey(10420) && Scalar("SELECT count(*) FROM revora_monk_training WHERE uid=@uid",learner.Entity.UID)==0,"failed skill insert rolls back lesson and exposes no unsaved Aura");
        learner=ReloadBall(learner);approach(learner);SoulWire(learner,15,4271);
        Check(!Fenced(learner) && learner.Spells.ContainsKey(10420),"failed Aura lesson can be retried after full login");
        var switcher=player();learn(switcher,10420);learn(switcher,10421);cast(switcher,10420);ulong original=switcher.Entity.Aura_actType;switcher.Entity.Mana=0;cast(switcher,10421);
        Check(switcher.Entity.Aura_isActive && switcher.Entity.Aura_actType==original && switcher.Entity.ContainsFlag(original),"failed Aura switch preserves the already active Aura");
        var concurrent=player();learn(concurrent,10420);int resistance=concurrent.Entity.MetalResistance;
        Parallel.For(0,20,n=>Revora.Game.Attacking.Handle.HandleAuraMonk(concurrent.Entity,SpellTable.GetSpell(10420,0)));
        Check(!Fenced(concurrent) && !concurrent.Entity.Aura_isActive && concurrent.Entity.MetalResistance==resistance
            && Scalar("SELECT count(*) FROM revora_aura_credit WHERE uid=@uid",concurrent.Entity.UID)==1,
            "concurrent Aura toggles serialize without stacking bonuses or duplicate receipts");
        var failed=player();learn(failed,10420);
        Query("CREATE FUNCTION suppress_aura() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
        Query("CREATE TRIGGER suppress_aura BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW WHEN (NEW.achievement_id=10919) EXECUTE FUNCTION suppress_aura()");
        try{cast(failed,10420);}finally{Query("DROP TRIGGER suppress_aura ON revora_achievement_evidence;DROP FUNCTION suppress_aura()");}
        Check(Fenced(failed) && failed.Entity.Aura_isActive && Scalar("SELECT count(*) FROM revora_aura_credit WHERE uid=@uid AND completed_at IS NULL",failed.Entity.UID)==1,
            "failed Aura achievement write preserves a saved successful-cast receipt");
        failed=ReloadBall(failed);failed.Entity.MyAchievement.Send();Check(!Fenced(failed) && HasAchievement(failed,10919),"full login recovers saved Aura credit without recasting");
    }
}
