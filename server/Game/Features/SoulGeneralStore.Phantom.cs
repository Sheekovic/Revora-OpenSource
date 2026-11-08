using System;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static partial class SoulGeneralStore
    {
        private static DateTime Now(){var now=SoulGeneralPhantom.Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Soul phantom clock must be UTC.");return now;}
        internal sealed class PhantomAttempt {internal Guid ID;internal int Kills,State;internal DateTime NextUse,Until;}
        private static PhantomAttempt ReadPhantom(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Guid id){
            using(var q=Q.Command(c,t,"SELECT kills,state,next_use,until_at FROM revora_soul_phantoms WHERE id=@id AND uid=@uid AND life=@life FOR UPDATE","id",id,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn))
            using(var r=q.ExecuteReader())return r.Read()?new PhantomAttempt{ID=id,Kills=r.GetInt16(0),State=r.GetInt16(1),NextUse=r.GetDateTime(2).ToUniversalTime(),Until=r.GetDateTime(3).ToUniversalTime()}:null;
        }
        internal static PhantomAttempt OpenPhantom(GameState p){
            if(!Q.Player(p,false))return null;lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();
                using(var q=Q.Command(c,null,"SELECT id FROM revora_soul_phantoms WHERE uid=@uid AND life=@life AND state IN(0,1)","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn)){
                    object id=q.ExecuteScalar();return id==null?null:ReadPhantom(c,null,p,(Guid)id);
                }
            }
        }
        internal static bool ReservePhantom(GameState p,Guid attempt,Func<bool> context){
            Ready(p,context);if(attempt==Guid.Empty || p.Entity.MapID!=1020 || p.Map.ID!=1020 || Kernel.GetDistance(p.Entity.X,p.Entity.Y,865,540)>6)return false;var now=Now();
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=9)return false;
                using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_soul_phantoms WHERE uid=@uid AND life=@life AND (state IN(0,1) OR next_use>@now))","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"now",now))if(Equals(q.ExecuteScalar(),true))return false;
                if(Q.Execute(c,t,"INSERT INTO revora_soul_phantoms(id,uid,life,next_use,until_at) VALUES(@id,@uid,@life,@next,@until)","id",attempt,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"next",now.AddSeconds(60),"until",now.AddMinutes(5))!=1)throw new InvalidOperationException("Soul General ape reservation failed.");
                Ready(p,context);t.Commit();return true;
            }}
        }
        private static bool PhantomBody(GameState p,Entity body){Entity registered;
            return body!=null && body.Dead && body.MapID==1020 && p.Entity.MapID==1020 && p.Map.ID==1020 && body.MonsterInfo!=null
                && body.MonsterInfo.ID==SoulGeneralPhantom.Family
                && ReferenceEquals(body.Killer,p.Entity) && p.Map.Entities.TryGetValue(body.UID,out registered) && ReferenceEquals(registered,body)
                && Kernel.GetDistance(p.Entity.X,p.Entity.Y,body.X,body.Y)<=Constants.pScreenDistance;
        }
        // Runtime context must prove this exact actor belongs to the owner's registered attempt.
        internal static bool PhantomDeath(GameState p,Guid attempt,Entity body,Func<bool> context){
            Ready(p,context);if(!PhantomBody(p,body))return false;
            lock(body){object seen;if(deaths.TryGetValue(body,out seen))return false;deaths.Add(body,new object());
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Owner(c,t,p);var run=Read(c,t,p);var fight=ReadPhantom(c,t,p,attempt);
                    if(run.Stage!=9 || fight==null || fight.State!=0 || Now()>=fight.Until || fight.Kills!=0)return false;
                    if(Q.Execute(c,t,"INSERT INTO revora_soul_phantom_kills(attempt,body) VALUES(@attempt,@body) ON CONFLICT DO NOTHING","attempt",attempt,"body",(long)body.UID)!=1)return false;
                    if(Q.Execute(c,t,"UPDATE revora_soul_phantoms SET kills=kills+1,state=@state WHERE id=@id AND state=0 AND kills=@kills","state",1,"id",attempt,"kills",fight.Kills)!=1)throw new InvalidOperationException("Soul General ape kill changed.");
                    Ready(p,()=>context() && PhantomBody(p,body));t.Commit();return true;
                }}
            }
        }
        // Only after the runtime owner has removed the abandoned encounter's actors.
        internal static bool AbandonPhantom(GameState p,Guid attempt,Func<bool> context){
            Ready(p,context);using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);var fight=ReadPhantom(c,t,p,attempt);if(run.Stage!=9 || fight==null || fight.State!=0)return false;
                if(Q.Execute(c,t,"UPDATE revora_soul_phantoms SET state=2 WHERE id=@id AND state=0","id",attempt)!=1)throw new InvalidOperationException("Soul General ape abandonment changed.");
                Ready(p,context);t.Commit();return true;
            }}
        }
        // The saved victory survives a full bag or a process interruption.
        internal static bool DeliverPaper(GameState p,Guid attempt,Func<bool> context){
            Ready(p,context);ConquerItem paper;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);var fight=ReadPhantom(c,t,p,attempt);
                if(run.Stage!=9 || fight==null || fight.State!=1 || fight.Kills!=1 || p.Inventory.Count>=40)return false;
                paper=Q.New(721706,true);Q.Insert(c,t,p.Entity.UID,paper);
                run.Stage=10;run.Item=paper.UID;run.Type=paper.ID;Save(c,t,p,run,9);
                if(Q.Execute(c,t,"UPDATE revora_soul_phantoms SET state=3 WHERE id=@id AND state=1","id",attempt)!=1)throw new InvalidOperationException("Soul paper delivery changed.");
                Ready(p,context);t.Commit();
            }}Q.Install(p,new uint[0],new[]{paper});return true;
        }
    }
}
