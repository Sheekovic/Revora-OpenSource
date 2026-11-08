using System;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static partial class DesertTreasureStore
    {
        // Revora questions recall the completed quest; original wording is unverified.
        internal static readonly string[] QuizQuestions={"How many bottles of Bat Essence reveal the portrait?","How long does the revealed portrait last?","When must the Pure Bottle be used?"};
        internal static readonly string[][] QuizChoices={new[]{"Three.","Five.","Ten."},new[]{"Ten seconds.","Five minutes.","One minute."},new[]{"Before the fight.","While Jinx's corpse remains.","After leaving the castle."}};
        private static bool AtLouis(GameState p){return p.Entity.MapID==1000 && Kernel.GetDistance(p.Entity.X,p.Entity.Y,466,658)<=3;}
        private static int QuizStep(NpgsqlConnection c,NpgsqlTransaction t,GameState p){
            using(var q=Q.Command(c,t,"SELECT step FROM revora_desert_treasure_quiz WHERE uid=@uid AND life=@life FOR UPDATE","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn)){
                object step=q.ExecuteScalar();return step==null?0:Convert.ToInt32(step);
            }
        }
        internal static int QuizProgress(GameState p){if(!Eligible(p))return -1;lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();return Read(c,null,p).Stage>=7?QuizStep(c,null,p):-1;}}
        internal static bool AnswerQuiz(GameState p,int question,int answer,Func<bool> context){
            Ready(p,context);if(!AtLouis(p) || question<0 || question>2 || answer<0 || answer>2)return false;
            lock(MentorExperience.Sync){ExperienceRewardPlan plan=null;ExperienceRewards.Effects effects=null;ulong auto=0;bool correct;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Owner(c,t,p);if(Read(c,t,p).Stage<7)return false;int before=QuizStep(c,t,p);if(before!=question)return false;
                    correct=answer==new[]{1,2,1}[question];int after=correct?before+1:0;
                    if(after==3){ulong xp=p.Entity.Level>=Config.LevelCap?0:p.ExpBall/2;
                        plan=new ExperienceRewardPlan(CharacterProgression.Capture(p),xp,Config.LevelCap,Config.AttributePointsPerLevel,ReincarnationRecovery.Snapshot(p));
                        CharacterProgression.Persist(c,t,p,plan.After);effects=ExperienceRewards.Persist(c,t,p,plan);ReincarnationRecovery.Consume(c,t,p,plan.Restored);
                        auto=p.Entity.InAutoHunt?checked(p.Entity.AutoHuntEXP+xp/Math.Max(1u,Config.AutoHuntExperienceDivisor)):p.Entity.AutoHuntEXP;
                    }
                    if(Q.Execute(c,t,@"INSERT INTO revora_desert_treasure_quiz(uid,life,step) VALUES(@uid,@life,@after)
ON CONFLICT(uid,life) DO UPDATE SET step=@after WHERE revora_desert_treasure_quiz.step=@before",
                        "uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"after",after,"before",before)!=1)throw new InvalidOperationException("Desert Treasure quiz changed.");
                    Ready(p,()=>context() && AtLouis(p));t.Commit();
                }}
                if(plan!=null){ReincarnationRecovery.Committed(p,plan.Restored);MentorExperience.Apply(p,effects.MentorCredit);p.ApplyExperienceReward(plan,effects,auto,true);}return correct;
            }
        }
    }
}
