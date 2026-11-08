using System;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using W=Revora.Game.Features.GhostCityScenes;
namespace Revora.Game.Features
{
    internal static partial class GhostCityStore
    {
        internal static int CombatProgress(GameState p){
            if(!Q.Player(p,false))throw new InvalidOperationException("Dark City owner changed.");
            lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();
                using(var q=Q.Command(c,null,"SELECT kills FROM revora_ghost_combat WHERE uid=@uid AND life=@life","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn)){
                    object value=q.ExecuteScalar();return value==null?0:Convert.ToInt32(value);
                }
            }
        }
        private static bool CombatBody(GameState p,W.Scene scene,Entity body){Entity registered;
            return scene.Dark && scene.Entered && W.Registered(scene,p) && W.Clock()<scene.Until
                && p.Entity.MapID==scene.Map.ID && ReferenceEquals(p.Map,scene.Map)
                && body!=null && body.Dead && GhostCityCombat.IsEncounter(body) && GhostCityCombat.Owns(scene,body)
                && body.MapID==scene.Map.ID && ReferenceEquals(body.Killer,p.Entity)
                && scene.Map.Entities.TryGetValue(body.UID,out registered) && ReferenceEquals(registered,body)
                && Kernel.GetDistance(p.Entity.X,p.Entity.Y,body.X,body.Y)<=Constants.pScreenDistance;
        }
        internal static bool CombatKill(GameState p,W.Scene scene,Entity body,Func<bool> context){
            Ready(p,context);if(!CombatBody(p,scene,body))return false;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);
                if(run.Stage!=7 || !Visit(c,t,p,scene,false))return false;
                Q.Execute(c,t,"INSERT INTO revora_ghost_combat(uid,life) VALUES(@uid,@life) ON CONFLICT DO NOTHING","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn);
                int kills;using(var q=Q.Command(c,t,"SELECT kills FROM revora_ghost_combat WHERE uid=@uid AND life=@life FOR UPDATE","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn))kills=Convert.ToInt32(q.ExecuteScalar());
                int kind=checked((int)(body.MonsterInfo.ID-GhostCityCombat.FamilyStart));
                // Revora interpretation: 100 combined ordinary kills, then each boss once.
                if(kills>=102 || (kills<100?kind>1:kind!=kills-98))return false;
                if(Q.Execute(c,t,"INSERT INTO revora_ghost_kills(visit,body,uid,life,kind) VALUES(@visit,@body,@uid,@life,@kind) ON CONFLICT DO NOTHING",
                    "visit",scene.Visit,"body",(long)body.UID,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"kind",kind)!=1)return false;
                if(Q.Execute(c,t,"UPDATE revora_ghost_combat SET kills=kills+1 WHERE uid=@uid AND life=@life AND kills=@kills","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"kills",kills)!=1)
                    throw new InvalidOperationException("Dark City progress was not saved.");
                Ready(p,()=>context() && CombatBody(p,scene,body));t.Commit();return true;
            }}
        }
        // Durable entitlement survives timeout and full bags. The coordinator will
        // invoke this after combat; exchange the tracked powder for the earned item.
        internal static bool CollectBezoar(GameState p,W.Scene scene,Func<bool> context){
            Ready(p,context);if(!scene.Dark || !scene.Entered || !W.Registered(scene,p) || W.Clock()>=scene.Until
                || p.Entity.MapID!=scene.Map.ID || !ReferenceEquals(p.Map,scene.Map))return false;
            ConquerItem item;uint removed;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);
                if(run.Stage!=7 || run.Type!=721712 || !Material(c,t,p,run.Item,721712) || !Visit(c,t,p,scene,false))return false;
                using(var q=Q.Command(c,t,"SELECT kills FROM revora_ghost_combat WHERE uid=@uid AND life=@life FOR UPDATE","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn))if(Convert.ToInt32(q.ExecuteScalar())!=102)return false;
                removed=run.Item;Q.Consume(c,t,p,removed,721712);item=Q.New(721714,true);Q.Insert(c,t,p.Entity.UID,item);
                run.Item=item.UID;run.Type=item.ID;run.Stage=8;Save(c,t,p,run,7);
                Ready(p,()=>context() && W.Registered(scene,p) && W.Clock()<scene.Until && ReferenceEquals(p.Map,scene.Map));t.Commit();
            }}Q.Install(p,new[]{removed},new[]{item});return true;
        }
    }
}
