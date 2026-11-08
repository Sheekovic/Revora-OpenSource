using System;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static partial class FranticMonkeysStore
    {
        internal sealed class ApeAttempt {internal Guid ID;internal int Kills,State;internal DateTime NextUse,Until;}
        private static ApeAttempt ReadApes(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Guid id){
            using(var q=Q.Command(c,t,"SELECT kills,state,next_use,until_at FROM revora_frantic_apes WHERE id=@id AND uid=@uid AND life=@life FOR UPDATE","id",id,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn))
            using(var r=q.ExecuteReader())return r.Read()?new ApeAttempt{ID=id,Kills=r.GetInt16(0),State=r.GetInt16(1),NextUse=r.GetDateTime(2).ToUniversalTime(),Until=r.GetDateTime(3).ToUniversalTime()}:null;
        }
        internal static ApeAttempt OpenApes(GameState p){
            if(!Q.Player(p,false))return null;lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();
                using(var q=Q.Command(c,null,"SELECT id FROM revora_frantic_apes WHERE uid=@uid AND life=@life AND state IN(0,1)","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn)){
                    object id=q.ExecuteScalar();return id==null?null:ReadApes(c,null,p,(Guid)id);
                }
            }
        }
        internal static bool ReserveApes(GameState p,uint vial,Guid attempt,Func<bool> context){
            Ready(p,context);if(attempt==Guid.Empty || p.Entity.MapID!=1020 || p.Map.ID!=1020 || Kernel.GetDistance(p.Entity.X,p.Entity.Y,795,471)>3)return false;var now=Now();
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=7 || run.Item!=vial || !Material(c,t,p,run,721701))return false;
                using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_frantic_apes WHERE uid=@uid AND life=@life AND (state IN(0,1) OR next_use>@now))","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"now",now))if(Equals(q.ExecuteScalar(),true))return false;
                if(Q.Execute(c,t,"INSERT INTO revora_frantic_apes(id,uid,life,next_use,until_at) VALUES(@id,@uid,@life,@next,@until)","id",attempt,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"next",now.AddSeconds(60),"until",now.AddMinutes(5))!=1)throw new InvalidOperationException("Frantic ape reservation failed.");
                Ready(p,context);t.Commit();return true;
            }}
        }
        private static bool ApeBody(GameState p,Entity body){Entity registered;
            return body!=null && body.Dead && body.MapID==1020 && p.Entity.MapID==1020 && p.Map.ID==1020 && body.MonsterInfo!=null
                && (body.MonsterInfo.ID==FranticMonkeysScenes.FamilyStart+1 || body.MonsterInfo.ID==FranticMonkeysScenes.FamilyStart+2)
                && ReferenceEquals(body.Killer,p.Entity) && p.Map.Entities.TryGetValue(body.UID,out registered) && ReferenceEquals(registered,body)
                && Kernel.GetDistance(p.Entity.X,p.Entity.Y,body.X,body.Y)<=Constants.pScreenDistance;
        }
        // Runtime context must prove this exact actor belongs to the owner's registered attempt.
        internal static bool ApeDeath(GameState p,Guid attempt,Entity body,Func<bool> context){
            Ready(p,context);if(!ApeBody(p,body))return false;
            lock(body){object seen;if(deaths.TryGetValue(body,out seen))return false;deaths.Add(body,new object());
                int kind=body.MonsterInfo.ID==FranticMonkeysScenes.FamilyStart+1?1:2;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Owner(c,t,p);var run=Read(c,t,p);var fight=ReadApes(c,t,p,attempt);
                    if(run.Stage!=7 || fight==null || fight.State!=0 || Now()>=fight.Until || (fight.Kills<2?kind!=1:kind!=2))return false;
                    if(Q.Execute(c,t,"INSERT INTO revora_frantic_ape_kills(attempt,body,kind) VALUES(@attempt,@body,@kind) ON CONFLICT DO NOTHING","attempt",attempt,"body",(long)body.UID,"kind",kind)!=1)return false;
                    if(Q.Execute(c,t,"UPDATE revora_frantic_apes SET kills=kills+1,state=@state WHERE id=@id AND state=0 AND kills=@kills","state",kind==2?1:0,"id",attempt,"kills",fight.Kills)!=1)throw new InvalidOperationException("Frantic ape kill changed.");
                    Ready(p,()=>context() && ApeBody(p,body));t.Commit();return true;
                }}
            }
        }
        // Only after the runtime owner has removed the abandoned encounter's actors.
        internal static bool AbandonApes(GameState p,Guid attempt,Func<bool> context){
            Ready(p,context);using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);var fight=ReadApes(c,t,p,attempt);if(run.Stage!=7 || fight==null || fight.State!=0)return false;
                if(Q.Execute(c,t,"UPDATE revora_frantic_apes SET state=2 WHERE id=@id AND state=0","id",attempt)!=1)throw new InvalidOperationException("Frantic ape abandonment changed.");
                Ready(p,context);t.Commit();return true;
            }}
        }
        // A won encounter survives storage/full-bag/reload interruptions. Exchange the exact vial.
        internal static bool DeliverSword(GameState p,Guid attempt,Func<bool> context){
            Ready(p,context);ConquerItem sword;uint removed;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);var fight=ReadApes(c,t,p,attempt);
                if(run.Stage!=7 || fight==null || fight.State!=1 || fight.Kills!=3 || !Material(c,t,p,run,721701))return false;
                removed=run.Item;Q.Consume(c,t,p,removed,721701);sword=Q.New(721702,true);Q.Insert(c,t,p.Entity.UID,sword);
                run.Stage=8;run.Item=sword.UID;run.Type=sword.ID;Save(c,t,p,run,7);
                if(Q.Execute(c,t,"UPDATE revora_frantic_apes SET state=3 WHERE id=@id AND state=1","id",attempt)!=1)throw new InvalidOperationException("Frantic sword delivery changed.");
                Ready(p,context);t.Commit();
            }}Q.Install(p,new[]{removed},new[]{sword});return true;
        }
    }
}
