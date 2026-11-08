using System;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Q=Revora.Game.Features.QuestInventory;
using W=Revora.Game.Features.FamilyFeudScenes;
namespace Revora.Game.Features
{
    internal static partial class FamilyFeudStore
    {
        internal static void InitializeVisits(){W.InitializeTravel();}
        private static bool AtGate(GameState p){return p.Entity.MapID==1015 && Kernel.GetDistance(p.Entity.X,p.Entity.Y,669,780)<=3;}
        internal static bool BeginVisit(GameState p,W.Scene scene,Func<bool> context){
            Ready(p,context);if(!W.Registered(scene,p) || !AtGate(p) || W.Clock()>=scene.Until)return false;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=7 || !run.DisguiseUntil.HasValue || W.Clock()>run.DisguiseUntil.Value
                    || !Materials(c,t,p,710567).Any(id=>Material(c,t,p,id,710567)))return false;
                using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_family_visits WHERE uid=@uid AND active)","uid",(long)p.Entity.UID))if(Equals(q.ExecuteScalar(),true))return false;
                if(Q.Execute(c,t,"INSERT INTO revora_family_visits(uid,visit,life,map,active,until_at) VALUES(@uid,@visit,@life,@map,true,@until)",
                    "uid",(long)p.Entity.UID,"visit",scene.Visit,"life",(int)p.Entity.Reborn,"map",(int)scene.Map.ID,"until",scene.Until)!=1)throw new InvalidOperationException("Family Feud admission not saved.");
                Ready(p,()=>context() && W.Registered(scene,p) && AtGate(p) && W.Clock()<scene.Until && W.Clock()<=run.DisguiseUntil.Value);t.Commit();return true;
            }}
        }
        internal static bool AtKerry(GameState p,W.Scene scene){
            if(!W.Registered(scene,p) || !scene.Entered || W.Clock()>=scene.Until || p.Entity.MapID!=scene.Map.ID || !ReferenceEquals(p.Map,scene.Map))return false;
            var expected=scene.Actors[0];Revora.Interfaces.INpc actor;return p.ActiveNpc==expected.UID && scene.Map.Npcs.TryGetValue(expected.UID,out actor) && ReferenceEquals(actor,expected) && Kernel.GetDistance(p.Entity.X,p.Entity.Y,actor.X,actor.Y)<=3;
        }
        private static bool Visit(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,GameState p,W.Scene scene){
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_family_visits WHERE uid=@uid AND visit=@visit AND life=@life AND map=@map AND active AND until_at>@now)",
                "uid",(long)p.Entity.UID,"visit",scene.Visit,"life",(int)p.Entity.Reborn,"map",(int)scene.Map.ID,"now",W.Clock()))return Equals(q.ExecuteScalar(),true);
        }
        internal static bool StartPuzzle(GameState p,W.Scene scene,Func<bool> context){
            Ready(p,context);if(!AtKerry(p,scene))return false;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);if(Read(c,t,p).Stage!=7 || !Visit(c,t,p,scene))return false;
                if(Q.Execute(c,t,@"UPDATE revora_family_visits SET puzzle_step=0,puzzle_until=@until WHERE uid=@uid AND visit=@visit AND active
AND puzzle_step<5 AND (puzzle_until IS NULL OR puzzle_until<@now)","until",W.Clock().AddSeconds(10),"now",W.Clock(),"uid",(long)p.Entity.UID,"visit",scene.Visit)!=1)return false;
                Ready(p,()=>context() && AtKerry(p,scene));t.Commit();return true;
            }}
        }
        // Called only after the ordinary movement handler has accepted and installed a position.
        internal static bool ObservePuzzle(GameState p,W.Scene scene,Func<bool> context){
            Ready(p,context);if(!W.Registered(scene,p) || !scene.Entered || p.Entity.MapID!=scene.Map.ID || !ReferenceEquals(p.Map,scene.Map))return false;
            int point=-1;for(int i=0;i<W.Pads.GetLength(0);i++)if(p.Entity.X==W.Pads[i,0] && p.Entity.Y==W.Pads[i,1])point=i;
            if(point<0)return false;bool completed=false;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);if(Read(c,t,p).Stage!=7 || !Visit(c,t,p,scene))return false;
                int step;DateTime? until;
                using(var q=Q.Command(c,t,"SELECT puzzle_step,puzzle_until FROM revora_family_visits WHERE visit=@visit FOR UPDATE","visit",scene.Visit))using(var r=q.ExecuteReader()){
                    if(!r.Read())return false;step=r.GetInt16(0);until=r.IsDBNull(1)?(DateTime?)null:r.GetDateTime(1).ToUniversalTime();
                }if(!until.HasValue || step==5)return false;
                DateTime now=W.Clock();bool expired=now>until.Value || now<until.Value.AddSeconds(-10);
                if(!expired && point==step-1)return false;
                if(expired || point!=step){
                    if(Q.Execute(c,t,"UPDATE revora_family_visits SET puzzle_step=0,puzzle_until=NULL WHERE visit=@visit AND active","visit",scene.Visit)!=1)throw new InvalidOperationException("Family Feud puzzle reset failed.");
                }else{
                    completed=step==4;
                    if(Q.Execute(c,t,"UPDATE revora_family_visits SET puzzle_step=@next WHERE visit=@visit AND active AND puzzle_step=@before","next",step+1,"before",step,"visit",scene.Visit)!=1)throw new InvalidOperationException("Family Feud puzzle progress changed.");
                }
                Ready(p,()=>context() && W.Registered(scene,p) && p.Entity.MapID==scene.Map.ID && ReferenceEquals(p.Map,scene.Map));t.Commit();
            }}return completed;
        }
        internal static bool FinishPuzzle(GameState p,W.Scene scene,Func<bool> context){
            Ready(p,context);if(!AtKerry(p,scene))return false;uint token;ulong silver;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);if(Read(c,t,p).Stage!=7 || !Visit(c,t,p,scene))return false;
                using(var q=Q.Command(c,t,"SELECT puzzle_step FROM revora_family_visits WHERE visit=@visit FOR UPDATE","visit",scene.Visit))if(Convert.ToInt32(q.ExecuteScalar())!=5)return false;
                token=Materials(c,t,p,710567).FirstOrDefault(id=>Material(c,t,p,id,710567));if(token==0)return false;
                Consume(c,t,p,token,710567);SilverBalance.AssertSaved(c,t,p);silver=checked(p.Entity.Money+100);if(silver>Entity.MaximumSilver)return false;
                SilverBalance.Persist(c,t,p,silver);Save(c,t,p,7,8);Ready(p,()=>context() && AtKerry(p,scene));t.Commit();
            }}Q.Install(p,new[]{token},new Network.GamePackets.ConquerItem[0]);SilverBalance.ApplyCommitted(p,silver);scene.Completed=true;return true;
        }
    }
}
