using System;
using System.Linq;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using R=Revora.Game.Features.FlameLitRules;

namespace Revora.Game.Features
{
    // Caller owns commit and post-commit projection under the player lock.
    internal static class FlameLitStore
    {
        internal sealed class Change {
            internal Guid Run;internal int Generation,Stage;internal uint Removed;internal ConquerItem Added;
        }
        internal sealed class Reward {
            internal ConquerItem Soul;internal uint Rune;internal int Units;internal ulong Experience,AutoHunt;
            internal ExperienceRewardPlan Plan;internal ExperienceRewards.Effects Effects;
        }
        private sealed class Run {internal Guid Id;internal int Generation,Stage;internal uint Rune;internal bool Claimed,Valuable;}
        internal static void Initialize() {
            for(uint item=R.FirstRune;item<=R.DragonRune;item++)if(!ConquerItemInformation.BaseInformations.ContainsKey(item))
                throw new InvalidOperationException("Flame rune catalog missing.");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Q.Execute(c,t,@"CREATE TABLE IF NOT EXISTS revora_flame_runs(
 id uuid PRIMARY KEY,uid bigint NOT NULL REFERENCES entities(uid),war_day varchar(10) NOT NULL REFERENCES revora_guild_battles(day),
 generation integer NOT NULL CHECK(generation>0),stage integer NOT NULL CHECK(stage BETWEEN 0 AND 10),
 rune_uid bigint NOT NULL UNIQUE CHECK(rune_uid BETWEEN 1 AND 4294967295),claimed boolean NOT NULL DEFAULT false,
 started_at timestamptz NOT NULL,UNIQUE(uid,war_day));
CREATE TABLE IF NOT EXISTS revora_flame_steps(
 run uuid NOT NULL REFERENCES revora_flame_runs(id),generation integer NOT NULL CHECK(generation>0),
 stage integer NOT NULL CHECK(stage BETWEEN 0 AND 10),before_item bigint NOT NULL,after_item bigint NOT NULL UNIQUE CHECK(after_item BETWEEN 1 AND 4294967295),
 npc bigint NOT NULL,lit_at timestamptz NOT NULL,PRIMARY KEY(run,generation,stage),
 CHECK((stage=0 AND before_item=0 AND npc=4452) OR (stage>0 AND before_item>0 AND npc=4452+stage)));
CREATE TABLE IF NOT EXISTS revora_flame_claims(
 run uuid PRIMARY KEY REFERENCES revora_flame_runs(id),generation integer NOT NULL CHECK(generation>0),
 rune_uid bigint NOT NULL UNIQUE,units integer NOT NULL CHECK(units BETWEEN 1 AND 12),
 experience bigint NOT NULL CHECK(experience>=0),claimed_at timestamptz NOT NULL);
ALTER TABLE revora_flame_runs ADD COLUMN IF NOT EXISTS valuable_reward boolean NOT NULL DEFAULT false;
ALTER TABLE revora_flame_claims ADD COLUMN IF NOT EXISTS soul_uid bigint UNIQUE;");t.Commit();}}
        }
        private static void Owner(NpgsqlConnection c,NpgsqlTransaction t,GameState p) {
            if(t==null || t.Connection!=c || !Q.Player(p) || !ReferenceEquals(p.Entity.Owner,p)
                || !Monitor.IsEntered(p.ItemSyncRoot) || p.Trade==null || p.Trade.InTrade || p.Booth!=null || p.Entity.PokerTable!=0
                || p.Map==null || p.Map.ID!=p.Entity.MapID)throw new InvalidOperationException("Flame progression requires its live owner and battlefield.");
            SkillTable.LockOwner(c,t,p);
            using(var q=Q.Command(c,t,"SELECT class,level,mapid FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())if(!r.Read() || Convert.ToInt32(r[0])!=p.Entity.Class || Convert.ToInt32(r[1])!=p.Entity.Level
                || Convert.ToInt32(r[2])!=p.Entity.MapID)throw new InvalidOperationException("Flame owner requires reload.");
        }
        private static void Owner(NpgsqlConnection c,NpgsqlTransaction t,GameState p,GuildSeasonWar.FlameAccess access) {
            if(access==null || !access.Valid())throw new InvalidOperationException("Flame battlefield access expired.");
            Owner(c,t,p);
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_guild_battles WHERE day=@day AND state='active' AND starts_at=@start AND ends_at=@end)",
                "day",access.Day,"start",access.Starts,"end",access.Ends))if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Flame battlefield receipt changed.");
        }
        private static Run Read(NpgsqlConnection c,NpgsqlTransaction t,uint uid,string day) {
            using(var q=Q.Command(c,t,"SELECT id,generation,stage,rune_uid,claimed FROM revora_flame_runs WHERE uid=@uid AND war_day=@day FOR UPDATE",
                "uid",(long)uid,"day",day))using(var r=q.ExecuteReader())return !r.Read()?null:new Run{Id=r.GetGuid(0),Generation=r.GetInt32(1),Stage=r.GetInt32(2),Rune=checked((uint)r.GetInt64(3)),Claimed=r.GetBoolean(4)};
        }
        private static bool Site(GameState p,uint npc,ushort map,ushort x,ushort y) {
            Revora.Interfaces.INpc actor;
            return p.Entity.MapID==map && p.Map.BaseID==map && p.Map.Npcs.TryGetValue(npc,out actor) && actor.MapID==map && actor.X==x && actor.Y==y
                && Kernel.GetDistance(p.Entity.X,p.Entity.Y,x,y)<=6;
        }
        private static void Bag(NpgsqlConnection c,NpgsqlTransaction t,GameState p,bool add) {
            using(var q=Q.Command(c,t,"SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0","uid",(long)p.Entity.UID)) {
                long count=(long)q.ExecuteScalar();if(count!=p.Inventory.Count || add && count>=40)throw new InvalidOperationException("Flame rune inventory unavailable.");
            }
        }
        private static void Receipt(NpgsqlConnection c,NpgsqlTransaction t,Change change,uint npc) {
            if(Q.Execute(c,t,@"INSERT INTO revora_flame_steps(run,generation,stage,before_item,after_item,npc,lit_at)
VALUES(@run,@generation,@stage,@before,@after,@npc,@now)","run",change.Run,"generation",change.Generation,"stage",change.Stage,
                "before",(long)change.Removed,"after",(long)change.Added.UID,"npc",(long)npc,"now",GuildSeasonWar.Clock())!=1)
                throw new InvalidOperationException("Flame step receipt was not saved.");
        }
        internal static Change Begin(NpgsqlConnection c,NpgsqlTransaction t,GameState p,GuildSeasonWar.FlameAccess access) {
            Owner(c,t,p,access);
            if(!Site(p,R.Taoist,1002,353,325) || p.ActiveNpc!=R.Taoist || !R.CanLight(R.FirstRune,1002,350,327,GuildSeasonWar.Clock(),access.Starts,access.Ends,true))return null;
            var run=Read(c,t,p.Entity.UID,access.Day);
            if(run!=null) {
                if(run.Claimed)return null;
                // Warehouse storage or transfer is not loss: any saved instance
                // of the current UID blocks generation of a replacement.
                using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM items WHERE uid=@item)","item",(long)run.Rune))if(Equals(q.ExecuteScalar(),true))return null;
            }
            Bag(c,t,p,true);
            var change=new Change{Run=run==null?Guid.NewGuid():run.Id,Generation=run==null?1:checked(run.Generation+1),Stage=0,Added=Q.New(R.FirstRune,true)};
            if(run==null) {
                if(Q.Execute(c,t,@"INSERT INTO revora_flame_runs(id,uid,war_day,generation,stage,rune_uid,started_at,valuable_reward)
VALUES(@id,@uid,@day,1,0,@rune,@now,true)","id",change.Run,"uid",(long)p.Entity.UID,"day",access.Day,"rune",(long)change.Added.UID,"now",GuildSeasonWar.Clock())!=1)
                    throw new InvalidOperationException("Flame run was not saved.");
            }else if(Q.Execute(c,t,"UPDATE revora_flame_runs SET generation=@generation,stage=0,rune_uid=@rune WHERE id=@id AND generation=@old AND NOT claimed",
                "generation",change.Generation,"rune",(long)change.Added.UID,"id",run.Id,"old",run.Generation)!=1)throw new InvalidOperationException("Flame restart was not saved.");
            Q.Insert(c,t,p.Entity.UID,change.Added);Receipt(c,t,change,R.Taoist);
            if(!access.Valid())throw new InvalidOperationException("Flame war ended during admission.");return change;
        }
        internal static Change Advance(NpgsqlConnection c,NpgsqlTransaction t,GameState p,GuildSeasonWar.FlameAccess access,uint rune) {
            Owner(c,t,p,access);var run=Read(c,t,p.Entity.UID,access.Day);
            if(run==null || run.Claimed || run.Rune!=rune || run.Stage>=10)return null;
            R.Step step;if(!R.TryStep(R.FirstRune+(uint)run.Stage,out step)
                || !Site(p,step.Npc,step.Map,step.X,step.Y)
                || !R.CanLight(step.Rune,p.Entity.MapID,p.Entity.X,p.Entity.Y,GuildSeasonWar.Clock(),access.Starts,access.Ends,true))return null;
            ConquerItem item;if(!p.Inventory.TryGetItem(rune,out item) || item.ID!=step.Rune || !item.Bound || item.Position!=0
                || item.Warehouse!=0 || item.StackSize!=1 || item.Lock!=0 || item.Suspicious || item.InWardrobe || item.Days!=0 || item.TimeLeftInMinutes!=0)return null;
            using(var q=Q.Command(c,t,@"SELECT uid FROM items WHERE uid=@item AND entityid=@uid AND id=@type AND position=0 AND warehouse=0
AND bound=1 AND stacksize=1 AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0' FOR UPDATE",
                "item",(long)rune,"uid",(long)p.Entity.UID,"type",(int)step.Rune))if(q.ExecuteScalar()==null)throw new InvalidOperationException("Flame rune ownership changed.");
            Bag(c,t,p,false);var change=new Change{Run=run.Id,Generation=run.Generation,Stage=run.Stage+1,Removed=rune,Added=Q.New(step.Next,true)};
            Q.Consume(c,t,p,rune,step.Rune);Q.Insert(c,t,p.Entity.UID,change.Added);Receipt(c,t,change,step.Npc);
            if(Q.Execute(c,t,"UPDATE revora_flame_runs SET stage=@stage,rune_uid=@next WHERE id=@id AND generation=@generation AND stage=@old AND rune_uid=@before AND NOT claimed",
                "stage",change.Stage,"next",(long)change.Added.UID,"id",run.Id,"generation",run.Generation,"old",run.Stage,"before",(long)rune)!=1)
                throw new InvalidOperationException("Flame progression was not saved.");
            if(change.Stage==10)ProgressionEvidence.Complete(c,t,p.Entity.UID,11016,"flame-lit-v1",run.Id.ToString("N"),1);
            if(!access.Valid())throw new InvalidOperationException("Flame war ended during lighting.");return change;
        }
        internal static Reward Claim(NpgsqlConnection c,NpgsqlTransaction t,GameState p,uint rune) {
            Owner(c,t,p);
            if(!Monitor.IsEntered(MentorExperience.Sync) || !CharacterProgression.Managed(p) || p.Spells==null)
                throw new InvalidOperationException("Flame experience owner is not ready.");
            if(!Site(p,R.Taoist,1002,353,325) || p.ActiveNpc!=R.Taoist
                || p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Cursed))return null;
            Run run=null;
            // Earned progress survives interruption. Claim only after the original
            // scheduled end, including cancelled wars and delayed weekly claims.
            using(var q=Q.Command(c,t,@"SELECT f.id,f.generation,f.stage,f.rune_uid,f.valuable_reward FROM revora_flame_runs f
JOIN revora_guild_battles b ON b.day=f.war_day
WHERE f.uid=@uid AND f.rune_uid=@rune AND NOT f.claimed AND f.stage>0 AND b.ends_at<=@now
FOR UPDATE OF f","uid",(long)p.Entity.UID,"rune",(long)rune,"now",GuildSeasonWar.Clock()))
            using(var r=q.ExecuteReader())if(r.Read())run=new Run{Id=r.GetGuid(0),Generation=r.GetInt32(1),Stage=r.GetInt32(2),Rune=checked((uint)r.GetInt64(3)),Valuable=r.GetBoolean(4)};
            if(run==null)return null;
            uint type=R.FirstRune+(uint)run.Stage;ConquerItem item;
            if(!p.Inventory.TryGetItem(rune,out item) || item.ID!=type || !item.Bound || item.Position!=0 || item.Warehouse!=0
                || item.StackSize!=1 || item.Lock!=0 || item.Suspicious || item.InWardrobe || item.Days!=0 || item.TimeLeftInMinutes!=0)return null;
            Bag(c,t,p,false);
            var reward=new Reward{Rune=rune,Units=R.RewardUnits(run.Stage),AutoHunt=p.Entity.AutoHuntEXP};
            var before=CharacterProgression.Capture(p);var recovery=ReincarnationRecovery.Snapshot(p);byte level=p.Entity.Level;
            // Recompute each ball at its earned level, but persist one final plan.
            // Replanning from the original profile includes recovery exactly once.
            for(int unit=0;unit<reward.Units;unit++) {
                ulong award=level>=Config.LevelCap?0:ExperienceMath.Ball(level);
                reward.Experience=checked(reward.Experience+award);
                if(p.Entity.InAutoHunt)reward.AutoHunt=checked(reward.AutoHunt+award/Math.Max(1u,Config.AutoHuntExperienceDivisor));
                reward.Plan=new ExperienceRewardPlan(before,reward.Experience,Config.LevelCap,Config.AttributePointsPerLevel,recovery);
                level=(byte)reward.Plan.After.Values[0];
            }
            // Check every restriction against the saved item as well as its cache.
            if(Q.Execute(c,t,@"DELETE FROM items WHERE uid=@rune AND entityid=@uid AND id=@type AND bound=1
AND position=0 AND warehouse=0 AND stacksize=1 AND locked=0 AND COALESCE(suspicious,0)=0
AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0'",
                "rune",(long)rune,"uid",(long)p.Entity.UID,"type",(int)type)!=1)throw new InvalidOperationException("Flame claim rune changed.");
            if(run.Valuable && run.Stage==10){reward.Soul=QuestSoulReward.Create();Q.Insert(c,t,p.Entity.UID,reward.Soul);}
            CharacterProgression.Persist(c,t,p,reward.Plan.After);
            reward.Effects=ExperienceRewards.Persist(c,t,p,reward.Plan);ReincarnationRecovery.Consume(c,t,p,reward.Plan.Restored);
            if(Q.Execute(c,t,@"INSERT INTO revora_flame_claims(run,generation,rune_uid,units,experience,claimed_at,soul_uid)
VALUES(@run,@generation,@rune,@units,@exp,@now,@soul)","run",run.Id,"generation",run.Generation,"rune",(long)rune,
                "units",reward.Units,"exp",checked((long)reward.Experience),"now",GuildSeasonWar.Clock(),"soul",reward.Soul==null?(object)DBNull.Value:(long)reward.Soul.UID)!=1)
                throw new InvalidOperationException("Flame reward receipt was not saved.");
            if(Q.Execute(c,t,"UPDATE revora_flame_runs SET claimed=true WHERE id=@run AND generation=@generation AND rune_uid=@rune AND NOT claimed",
                "run",run.Id,"generation",run.Generation,"rune",(long)rune)!=1)throw new InvalidOperationException("Flame reward completion was not saved.");
            return reward;
        }
        internal static void ApplyReward(GameState p,Reward reward) {
            if(reward==null)return;
            Q.Install(p,new[]{reward.Rune},reward.Soul==null?new ConquerItem[0]:new[]{reward.Soul});
            ReincarnationRecovery.Committed(p,reward.Plan.Restored);MentorExperience.Apply(p,reward.Effects.MentorCredit);
            p.ApplyExperienceReward(reward.Plan,reward.Effects,reward.AutoHunt,true);
        }
    }
}
