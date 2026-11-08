using System;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static partial class DefenseTalismanStore
    {
        private static bool AtRuan(GameState p){var scene=DefenseTalismanScenes.Find(p);return scene!=null && scene.Map.BaseID==1784
            && scene.Entered && !scene.Completed && DefenseTalismanScenes.Registered(scene,p) && p.Entity.MapID==scene.Map.ID
            && Clock()<scene.Until && Kernel.GetDistance(p.Entity.X,p.Entity.Y,55,55)<=6;}
        internal static bool RequestSpear(GameState p,Func<bool> context){
            Ready(p,context);if(!AtRuan(p))return false;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=4 || run.SpearRequested)return false;
                if(Q.Execute(c,t,"UPDATE revora_defense_talisman SET spear_requested=true WHERE uid=@uid AND stage=4 AND NOT spear_requested","uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Tower spear request changed.");
                Ready(p,()=>context() && AtRuan(p));t.Commit();return true;
            }}
        }
        internal static bool ShawnKill(GameState p,Entity body,Guid attempt,Func<bool> context){
            Ready(p,context);if(!DefenseTalismanEncounters.Owns(p,body,attempt,2) || !body.Dead || !ReferenceEquals(body.Killer,p.Entity))return false;
            ConquerItem spear;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=4 || !run.SpearRequested || p.Inventory.Count>=40 || Materials(c,t,p,721781).Count!=0)return false;
                if(Q.Execute(c,t,"INSERT INTO revora_tower_shawn_kills(attempt,uid,body) VALUES(@attempt,@uid,@body) ON CONFLICT DO NOTHING","attempt",attempt,"uid",(long)p.Entity.UID,"body",(long)body.UID)!=1)return false;
                spear=Q.New(721781,true);Track(c,t,p,spear);Ready(p,()=>context() && DefenseTalismanEncounters.Owns(p,body,attempt,2));t.Commit();
            }}Q.Install(p,new uint[0],new[]{spear});return true;
        }
        internal static bool ExchangeSpear(GameState p,Func<bool> context){
            Ready(p,context);if(!AtRuan(p))return false;uint spear;ConquerItem splinter;ulong silver;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=4 || !run.SpearRequested)return false;
                spear=Materials(c,t,p,721781).FirstOrDefault(id=>Material(c,t,p,id,721781));if(spear==0)return false;
                SilverBalance.AssertSaved(c,t,p);silver=checked(p.Entity.Money+100);if(silver>Entity.MaximumSilver)return false;
                Consume(c,t,p,spear,721781);splinter=Q.New(721777,true);Track(c,t,p,splinter);SilverBalance.Persist(c,t,p,silver);
                if(Q.Execute(c,t,"UPDATE revora_defense_talisman SET stage=5 WHERE uid=@uid AND stage=4","uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Tower spear report changed.");
                Ready(p,()=>context() && AtRuan(p));t.Commit();
            }}Q.Install(p,new[]{spear},new[]{splinter});SilverBalance.ApplyCommitted(p,silver);DefenseTalismanScenes.Find(p).Completed=true;return true;
        }
    }
}
