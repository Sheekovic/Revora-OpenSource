using System;
using Revora.Client;
using Revora.Database;
using Q=Revora.Game.Features.QuestInventory;
using W=Revora.Game.Features.DesertBloodScenes;
namespace Revora.Game.Features
{
    internal static partial class DesertBloodStore
    {
        internal static void InitializeVisits(){
            W.InitializeTravel();
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_desert_column(
uid bigint NOT NULL,life smallint NOT NULL,visit uuid NOT NULL REFERENCES revora_desert_visits(visit),
PRIMARY KEY(uid,life),FOREIGN KEY(uid,life) REFERENCES revora_desert_blood(uid,life));");}
        }
        private static bool AtGate(GameState p){return p.Entity.MapID==1000 && Kernel.GetDistance(p.Entity.X,p.Entity.Y,386,103)<=3;}
        internal static bool BeginVisit(GameState p,W.Scene scene,Func<bool> context){
            Ready(p,context);if(!W.Registered(scene,p) || !AtGate(p) || W.Clock()>=scene.Until)return false;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(!run.SpellRead || (run.Stage!=2 && run.Stage!=3 && run.Stage!=4 && run.Stage!=6) || Owned(c,t,p,721843)==0)return false;
                if(run.Stage==6 && Owned(c,t,p,721841)==0)return false;
                using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_desert_visits WHERE uid=@uid AND active)","uid",(long)p.Entity.UID))if(Equals(q.ExecuteScalar(),true))return false;
                if(Q.Execute(c,t,"INSERT INTO revora_desert_visits(uid,visit,life,map,active,until_at) VALUES(@uid,@visit,@life,@map,true,@until)","uid",(long)p.Entity.UID,"visit",scene.Visit,"life",(int)p.Entity.Reborn,"map",(int)scene.Map.ID,"until",scene.Until)!=1)throw new InvalidOperationException("Desert Blood admission was not saved.");
                Ready(p,()=>context() && W.Registered(scene,p) && AtGate(p) && W.Clock()<scene.Until);t.Commit();return true;
            }}
        }
        internal static bool AtActor(GameState p,W.Scene scene,int index){
            if(index<0 || index>1 || !W.Registered(scene,p) || !scene.Entered || W.Clock()>=scene.Until || p.Entity.MapID!=scene.Map.ID || !ReferenceEquals(p.Map,scene.Map))return false;
            var expected=scene.Actors[index];Revora.Interfaces.INpc actor;return p.ActiveNpc==expected.UID && scene.Map.Npcs.TryGetValue(expected.UID,out actor) && ReferenceEquals(actor,expected) && Kernel.GetDistance(p.Entity.X,p.Entity.Y,actor.X,actor.Y)<=3;
        }
        private static bool Visit(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,GameState p,W.Scene scene){
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_desert_visits WHERE uid=@uid AND visit=@visit AND life=@life AND map=@map AND active AND until_at>@now)","uid",(long)p.Entity.UID,"visit",scene.Visit,"life",(int)p.Entity.Reborn,"map",(int)scene.Map.ID,"now",W.Clock()))return Equals(q.ExecuteScalar(),true);
        }
        internal static bool Warrior(GameState p,W.Scene scene,Func<bool> context){
            Ready(p,context);if(!AtActor(p,scene,0))return false;
            using(var c=DataHolder.MySqlConnection){c.Open();if(!Visit(c,null,p,scene))return false;}
            int stage=Load(p).Stage;if(stage!=2 && stage!=3 && stage!=4 && stage!=6)return false;
            return Advance(p,stage,()=>context() && AtActor(p,scene,0));
        }
        // Revora rule: one column blessing per rebirth, never one per entry.
        internal static bool ColumnExperience(GameState p,W.Scene scene,Func<bool> context){
            Ready(p,context);if(!AtActor(p,scene,1))return false;
            lock(MentorExperience.Sync){ExperienceRewardPlan plan;ExperienceRewards.Effects effects;ulong auto;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Owner(c,t,p);var run=Read(c,t,p);if(run.Stage<3 || run.Stage>7 || !Visit(c,t,p,scene) || Owned(c,t,p,721843)==0)return false;
                    if(Q.Execute(c,t,"INSERT INTO revora_desert_column(uid,life,visit) VALUES(@uid,@life,@visit) ON CONFLICT(uid,life) DO NOTHING","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"visit",scene.Visit)!=1)return false;
                    ulong xp=p.Entity.Level>=Config.LevelCap?0:p.ExpBall/12;
                    plan=new ExperienceRewardPlan(CharacterProgression.Capture(p),xp,Config.LevelCap,Config.AttributePointsPerLevel,ReincarnationRecovery.Snapshot(p));
                    CharacterProgression.Persist(c,t,p,plan.After);effects=ExperienceRewards.Persist(c,t,p,plan);ReincarnationRecovery.Consume(c,t,p,plan.Restored);
                    auto=p.Entity.InAutoHunt?checked(p.Entity.AutoHuntEXP+xp/Math.Max(1u,Config.AutoHuntExperienceDivisor)):p.Entity.AutoHuntEXP;
                    Ready(p,()=>context() && AtActor(p,scene,1));t.Commit();
                }}ReincarnationRecovery.Committed(p,plan.Restored);MentorExperience.Apply(p,effects.MentorCredit);p.ApplyExperienceReward(plan,effects,auto,true);return true;
            }
        }
    }
}
