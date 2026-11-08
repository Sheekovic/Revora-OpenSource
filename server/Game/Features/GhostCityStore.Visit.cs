using System;
using Revora.Client;
using Revora.Database;
using Q=Revora.Game.Features.QuestInventory;
using W=Revora.Game.Features.GhostCityScenes;
namespace Revora.Game.Features
{
    internal static partial class GhostCityStore
    {
        private static bool AtGate(GameState p){if(p.Entity.MapID!=1002)return false;for(int i=0;i<3;i++)if(Kernel.GetDistance(p.Entity.X,p.Entity.Y,FireworkPoints[i,0],FireworkPoints[i,1])<=3)return true;return false;}
        internal static bool BeginVisit(GameState p,W.Scene scene,Func<bool> context){
            Ready(p,context);if(!W.Registered(scene,p) || !AtGate(p) || W.Clock()>=scene.Until)return false;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if((scene.Dark?(run.Stage!=7 && run.Stage!=8):run.Stage!=5) || !Material(c,t,p,run.Charm,721713))return false;
                if(scene.Dark){uint required=run.Stage==7?721712u:721714u;if(run.Type!=required || !Material(c,t,p,run.Item,required))return false;}
                using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_ghost_visits WHERE uid=@uid AND active)","uid",(long)p.Entity.UID))if(Equals(q.ExecuteScalar(),true))return false;
                if(Q.Execute(c,t,"INSERT INTO revora_ghost_visits(uid,visit,life,map,active,until_at) VALUES(@uid,@visit,@life,@map,true,@until)","uid",(long)p.Entity.UID,"visit",scene.Visit,"life",(int)p.Entity.Reborn,"map",(int)scene.Map.ID,"until",scene.Until)!=1)throw new InvalidOperationException("Ghost City admission was not saved.");
                Ready(p,()=>context() && W.Registered(scene,p) && AtGate(p) && W.Clock()<scene.Until);t.Commit();return true;
            }}
        }
        private static bool AtActor(GameState p,W.Scene scene,int index){
            if(!W.Registered(scene,p) || !scene.Entered || W.Clock()>=scene.Until || p.Entity.MapID!=scene.Map.ID || !ReferenceEquals(p.Map,scene.Map))return false;
            var expected=scene.Actors[index];Revora.Interfaces.INpc actor;return p.ActiveNpc==expected.UID && scene.Map.Npcs.TryGetValue(expected.UID,out actor) && ReferenceEquals(actor,expected) && Kernel.GetDistance(p.Entity.X,p.Entity.Y,actor.X,actor.Y)<=3;
        }
        private static bool Visit(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,GameState p,W.Scene scene,bool met){
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_ghost_visits WHERE uid=@uid AND visit=@visit AND life=@life AND map=@map AND active AND until_at>@now AND (NOT @met OR met_momo))","uid",(long)p.Entity.UID,"visit",scene.Visit,"life",(int)p.Entity.Reborn,"map",(int)scene.Map.ID,"now",W.Clock(),"met",met))return Equals(q.ExecuteScalar(),true);
        }
        internal static bool MeetMoMo(GameState p,W.Scene scene,Func<bool> context){
            Ready(p,context);if(!AtActor(p,scene,0))return false;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=5 || !Material(c,t,p,run.Charm,721713) || !Visit(c,t,p,scene,false))return false;
                if(Q.Execute(c,t,"UPDATE revora_ghost_visits SET met_momo=true WHERE uid=@uid AND visit=@visit AND active AND NOT met_momo","uid",(long)p.Entity.UID,"visit",scene.Visit)!=1)return false;
                Ready(p,()=>context() && AtActor(p,scene,0));t.Commit();return true;
            }}
        }
        internal static bool CollectPowder(GameState p,W.Scene scene,Func<bool> context){
            Ready(p,context);if(!AtActor(p,scene,1))return false;Revora.Network.GamePackets.ConquerItem powder;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=5 || p.Inventory.Count>=40 || !Material(c,t,p,run.Charm,721713) || !Visit(c,t,p,scene,true))return false;
                powder=Q.New(721712,true);Q.Insert(c,t,p.Entity.UID,powder);run.Item=powder.UID;run.Type=powder.ID;run.Stage=6;Save(c,t,p,run,5);
                if(Q.Execute(c,t,"UPDATE revora_ghost_visits SET powder_uid=@item WHERE uid=@uid AND visit=@visit AND active AND met_momo AND powder_uid=0","item",(long)powder.UID,"uid",(long)p.Entity.UID,"visit",scene.Visit)!=1)throw new InvalidOperationException("Ghost City powder receipt changed.");
                Ready(p,()=>context() && AtActor(p,scene,1));t.Commit();
            }}scene.Completed=true;Q.Install(p,new uint[0],new[]{powder});return true;
        }
        internal static bool InspectPowder(GameState p,Func<bool> context){
            Ready(p,context);if(p.Entity.MapID!=1002 || W.HasOwner(p.Entity.UID))return false;ulong silver;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=6 || run.Type!=721712 || !Material(c,t,p,run.Item,721712))return false;
                using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_ghost_visits WHERE uid=@uid AND life=@life AND powder_uid>0) AND NOT EXISTS(SELECT 1 FROM revora_ghost_visits WHERE uid=@uid AND active)","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn))if(!Equals(q.ExecuteScalar(),true))return false;
                SilverBalance.AssertSaved(c,t,p);silver=checked(p.Entity.Money+100);if(silver>Entity.MaximumSilver)return false;SilverBalance.Persist(c,t,p,silver);run.Stage=7;Save(c,t,p,run,6);Ready(p,context);t.Commit();
            }}SilverBalance.ApplyCommitted(p,silver);return true;
        }
    }
}
