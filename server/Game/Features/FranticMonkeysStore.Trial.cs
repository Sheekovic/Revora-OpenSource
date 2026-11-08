using System;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using Scenes=Revora.Game.Features.FranticMonkeysScenes;
namespace Revora.Game.Features
{
    internal static partial class FranticMonkeysStore
    {
        internal sealed class Trial {internal Guid ID;internal ushort Map;internal DateTime ReadyAt,Until;internal int State;}
        private static DateTime Now(){var now=Scenes.Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Frantic trial requires UTC.");return now;}
        private static Trial ReadTrial(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Guid id){
            using(var q=Q.Command(c,t,"SELECT map,ready_at,until_at,state FROM revora_frantic_trials WHERE id=@id AND uid=@uid AND life=@life FOR UPDATE","id",id,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn))
            using(var r=q.ExecuteReader())return r.Read()?new Trial{ID=id,Map=checked((ushort)r.GetInt32(0)),ReadyAt=r.GetDateTime(1).ToUniversalTime(),Until=r.GetDateTime(2).ToUniversalTime(),State=r.GetInt16(3)}:null;
        }
        internal static Trial ActiveTrial(GameState p){
            if(!Q.Player(p,false))return null;lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();
                using(var q=Q.Command(c,null,"SELECT id FROM revora_frantic_trials WHERE uid=@uid AND life=@life AND state=0","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn)){
                    object id=q.ExecuteScalar();return id==null?null:ReadTrial(c,null,p,(Guid)id);
                }
            }
        }
        // Caller reserves the private scene and monkey appearance before consuming poison.
        // The context checks those in-memory reservations; it must not issue database reads.
        internal static bool BeginTrial(GameState p,uint poison,Scenes.Scene scene,Func<bool> context){return BeginTrialCore(p,poison,scene,context,false);}
        internal static bool BeginDisguisedTrial(GameState p,uint poison,Scenes.Scene scene,Func<bool> context){return BeginTrialCore(p,poison,scene,context,true);}
        private static bool BeginTrialCore(GameState p,uint poison,Scenes.Scene scene,Func<bool> context,bool disguise){
            Ready(p,context);if(!Scenes.Registered(scene,p))return false;var now=Now();
            if(scene.Until<=now.AddSeconds(60))return false;Guid token=Guid.Empty;bool committed=false;
            try{using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=4 || run.Item!=poison || !Material(c,t,p,run,721700))return false;
                if(disguise){token=QuestAppearance.ApplyMonkey(p,scene,now.AddSeconds(60),context);if(token==Guid.Empty)return false;scene.Appearance=token;}
                Q.Consume(c,t,p,poison,721700);
                if(Q.Execute(c,t,"INSERT INTO revora_frantic_trials(id,uid,life,map,ready_at,until_at) VALUES(@id,@uid,@life,@map,@ready,@until)",
                    "id",scene.Visit,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"map",(int)scene.Map.ID,"ready",now.AddSeconds(60),"until",scene.Until)!=1)throw new InvalidOperationException("Frantic trial reservation failed.");
                run.Stage=5;run.Item=run.Type=0;Save(c,t,p,run,4);
                if(Q.Execute(c,t,"UPDATE revora_frantic_monkeys SET kills=0 WHERE uid=@uid AND life=@life","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn)!=1)throw new InvalidOperationException("Frantic trial counter missing.");
                Ready(p,()=>context() && Scenes.Registered(scene,p) && (!disguise || QuestAppearance.Owns(p,token,QuestAppearance.MonkeyMesh)));t.Commit();committed=true;
            }}Q.Install(p,new[]{poison},new ConquerItem[0]);return true;
            }finally{if(!committed && token!=Guid.Empty){QuestAppearance.Clear(p,token,QuestAppearance.MonkeyMesh);scene.Appearance=Guid.Empty;}}
        }
        private static bool TrialBody(GameState p,Scenes.Scene scene,Entity body){Entity registered;
            return Scenes.Registered(scene,p) && ReferenceEquals(p.Map,scene.Map) && p.Entity.MapID==scene.Map.ID
                && body!=null && body.Dead && body.MapID==scene.Map.ID && body.MonsterInfo!=null && body.MonsterInfo.ID==Scenes.FamilyStart
                && ReferenceEquals(body.Killer,p.Entity) && scene.Map.Entities.TryGetValue(body.UID,out registered) && ReferenceEquals(registered,body)
                && Kernel.GetDistance(p.Entity.X,p.Entity.Y,body.X,body.Y)<=Constants.pScreenDistance;
        }
        internal static bool TrialKill(GameState p,Scenes.Scene scene,Entity body,Func<bool> context){
            Ready(p,context);if(!TrialBody(p,scene,body))return false;
            lock(body){object seen;if(deaths.TryGetValue(body,out seen))return false;deaths.Add(body,new object());
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Owner(c,t,p);var run=Read(c,t,p);var trial=ReadTrial(c,t,p,scene.Visit);var now=Now();
                    if(run.Stage!=5 || run.Kills>=5 || trial==null || trial.State!=0 || trial.Map!=scene.Map.ID || now<trial.ReadyAt || now>=trial.Until)return false;
                    if(Q.Execute(c,t,"INSERT INTO revora_frantic_trial_kills(attempt,body) VALUES(@attempt,@body) ON CONFLICT DO NOTHING","attempt",trial.ID,"body",(long)body.UID)!=1)return false;
                    if(Q.Execute(c,t,"UPDATE revora_frantic_monkeys SET kills=kills+1,stage=@stage WHERE uid=@uid AND life=@life AND stage=5 AND kills=@kills",
                        "stage",run.Kills==4?6:5,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"kills",run.Kills)!=1)throw new InvalidOperationException("Frantic trial progress changed.");
                    if(run.Kills==4 && Q.Execute(c,t,"UPDATE revora_frantic_trials SET state=1 WHERE id=@id AND state=0","id",trial.ID)!=1)throw new InvalidOperationException("Frantic trial completion changed.");
                    Ready(p,()=>context() && TrialBody(p,scene,body));t.Commit();return true;
                }}
            }
        }
        // The coordinator must evacuate and release a failed/interrupted scene first.
        internal static bool RecoverTrial(GameState p,Guid attempt,Func<bool> context){
            Ready(p,context);if(Scenes.HasOwner(p.Entity.UID) || p.Inventory.Count>=40)return false;ConquerItem poison;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);var trial=ReadTrial(c,t,p,attempt);if(run.Stage!=5 || trial==null || trial.State!=0)return false;
                poison=Q.New(721700,true);Q.Insert(c,t,p.Entity.UID,poison);run.Stage=4;run.Item=poison.UID;run.Type=poison.ID;Save(c,t,p,run,5);
                if(Q.Execute(c,t,"UPDATE revora_frantic_trials SET state=2 WHERE id=@id AND state=0","id",attempt)!=1)throw new InvalidOperationException("Frantic trial recovery changed.");
                if(Q.Execute(c,t,"UPDATE revora_frantic_monkeys SET kills=0 WHERE uid=@uid AND life=@life","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn)!=1)throw new InvalidOperationException("Frantic recovery counter missing.");
                Ready(p,()=>context() && !Scenes.HasOwner(p.Entity.UID));t.Commit();
            }}Q.Install(p,new uint[0],new[]{poison});return true;
        }
        // Doctor Li after automatic return. Completion survives a full bag or disconnect.
        internal static bool TrialReward(GameState p,Func<bool> context){
            Ready(p,context);if(p.Inventory.Count>=40)return false;ConquerItem vial;ulong silver=p.Entity.Money;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=6 || run.Kills!=5)return false;
                vial=Q.New(721701,true);Q.Insert(c,t,p.Entity.UID,vial);SilverBalance.AssertSaved(c,t,p);silver=checked(silver+100);if(silver>Entity.MaximumSilver)return false;SilverBalance.Persist(c,t,p,silver);
                run.Stage=7;run.Item=vial.UID;run.Type=vial.ID;Save(c,t,p,run,6);Ready(p,context);t.Commit();
            }}Q.Install(p,new uint[0],new[]{vial});SilverBalance.ApplyCommitted(p,silver);return true;
        }
    }
}
