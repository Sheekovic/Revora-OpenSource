using System;
using Revora.Client;
using Revora.Database;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features {
    internal static class AuraProgress {
        internal static void Initialize(){using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_aura_credit(
uid bigint PRIMARY KEY CHECK(uid>0),spell integer NOT NULL CHECK(spell IN(10395,10410,10420,10421,10422,10423,10424)),
receipt uuid NOT NULL UNIQUE,created_at timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,completed_at timestamptz)");}}
        internal static void Recover(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,uint uid){
            Guid key;using(var q=Q.Command(c,t,"SELECT receipt FROM revora_aura_credit WHERE uid=@uid AND completed_at IS NULL FOR UPDATE","uid",(long)uid)){
                object value=q.ExecuteScalar();if(value==null)return;key=(Guid)value;}
            ProgressionEvidence.Complete(c,t,uid,10919,"aura-cast-v1",key.ToString("N"),1);
            if(Q.Execute(c,t,"UPDATE revora_aura_credit SET completed_at=CURRENT_TIMESTAMP WHERE uid=@uid AND completed_at IS NULL","uid",(long)uid)!=1)
                throw new InvalidOperationException("Aura credit receipt was not completed.");
        }
        internal static void Record(GameState p,ushort spell){
            if(!Q.Player(p) || !p.Entity.Aura_isActive || !p.Entity.ContainsFlag(p.Entity.Aura_actType))return;
            if(!System.Threading.Monitor.IsEntered(p.ItemSyncRoot))throw new InvalidOperationException("Aura credit requires its caster lock.");
            try{using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                SkillTable.LockOwner(c,t,p);Q.Execute(c,t,"INSERT INTO revora_aura_credit(uid,spell,receipt) VALUES(@uid,@spell,@key) ON CONFLICT(uid) DO NOTHING","uid",(long)p.Entity.UID,"spell",(int)spell,"key",Guid.NewGuid());
                using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_aura_credit WHERE uid=@uid)","uid",(long)p.Entity.UID))
                    if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Aura credit receipt was not saved.");t.Commit();}
                using(var t=c.BeginTransaction()){Recover(c,t,p.Entity.UID);t.Commit();}}
                Q.Refresh(p);
            }catch(Exception ex){Q.Fail(p,"aura-credit",ex);}
        }
    }
}
