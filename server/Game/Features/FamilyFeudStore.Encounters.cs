using System;
using Revora.Client;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static partial class FamilyFeudStore
    {
        internal static int EncounterKind(GameState p){
            if(!Eligible(p))return -1;lock(p.ItemSyncRoot)using(var c=Database.DataHolder.MySqlConnection){c.Open();var run=Read(c,null,p);
                int kind=run.Stage==2?0:run.Stage==4?1:run.Stage==5?2:run.Stage==6?3:-1;
                if(kind<0 || Materials(c,null,p,710560u+(uint)run.Stage).Count>0)return -1;
                if(kind==0 && Kills(c,null,p,2)<50 || kind==1 && Kills(c,null,p,4)<10)return -1;return kind;
            }
        }
        internal static bool EncounterKill(GameState p,Entity body,Guid attempt,int kind,Func<bool> context){
            Ready(p,context);if(!FamilyFeudEncounters.Owns(p,body,attempt,kind) || !body.Dead || !ReferenceEquals(body.Killer,p.Entity))return false;
            ConquerItem added=null;
            using(var c=Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);int expected=kind==0?2:kind==1?4:kind==2?5:kind==3?6:-1;
                if(run.Stage!=expected || Materials(c,t,p,710560u+(uint)run.Stage).Count>0)return false;
                if(kind==0 && Kills(c,t,p,2)<50 || kind==1 && Kills(c,t,p,4)<10)return false;
                int before=kind==2?Kills(c,t,p,5):0;if(kind==2 && before>=10)return false;
                bool won=kind!=2 || before==9;
                if(won && p.Inventory.Count>=40)return false;
                if(kind==3){int roll=Roll(100);if(roll<0 || roll>=100)throw new InvalidOperationException("Family Feud roll outside range.");won=roll<50;}
                if(Q.Execute(c,t,"INSERT INTO revora_family_feud_encounter_kills(attempt,uid,life,kind,body,won) VALUES(@attempt,@uid,@life,@kind,@body,@won) ON CONFLICT DO NOTHING",
                    "attempt",attempt,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"kind",kind,"body",(long)body.UID,"won",won)!=1)return false;
                if(kind==2 && Q.Execute(c,t,@"INSERT INTO revora_family_feud_kills(uid,life,stage,kills) VALUES(@uid,@life,5,@next)
ON CONFLICT(uid,life,stage) DO UPDATE SET kills=@next WHERE revora_family_feud_kills.kills=@before",
                    "uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"next",before+1,"before",before)!=1)throw new InvalidOperationException("Family Feud deliverer count changed.");
                if(won){added=Q.New(710560u+(uint)run.Stage,true);Track(c,t,p,added);}
                Ready(p,()=>context() && FamilyFeudEncounters.Owns(p,body,attempt,kind));t.Commit();
            }}if(added!=null)Q.Install(p,new uint[0],new[]{added});return true;
        }
    }
}
