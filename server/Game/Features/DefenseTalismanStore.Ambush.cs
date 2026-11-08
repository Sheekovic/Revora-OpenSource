using System;
using Revora.Client;
using Revora.Database;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static partial class DefenseTalismanStore
    {
        private static bool AtInvocation(GameState p,DefenseTalismanScenes.Scene scene){return scene!=null && scene.Entered && !scene.Completed
            && scene.Map.BaseID==1783 && DefenseTalismanScenes.Registered(scene,p) && p.Entity.MapID==scene.Map.ID && Clock()<scene.Until
            && Kernel.GetDistance(p.Entity.X,p.Entity.Y,52,50)<=3;}
        internal static bool ClaimInvocation(GameState p,Func<bool> context){
            Ready(p,context);var scene=DefenseTalismanScenes.Find(p);if(!AtInvocation(p,scene))return false;
            Revora.Network.GamePackets.ConquerItem invocation;ulong silver;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);if(Read(c,t,p).Stage!=7 || p.Inventory.Count>=40 || Materials(c,t,p,721786).Count!=0)return false;
                using(var q=Q.Command(c,t,"SELECT COUNT(*) FROM revora_tower_house_kills WHERE uid=@uid AND visit=@visit","uid",(long)p.Entity.UID,"visit",scene.Visit))if(Convert.ToInt32(q.ExecuteScalar())!=5)return false;
                SilverBalance.AssertSaved(c,t,p);silver=checked(p.Entity.Money+100);if(silver>Entity.MaximumSilver)return false;
                invocation=Q.New(721786,true);Track(c,t,p,invocation);SilverBalance.Persist(c,t,p,silver);
                if(Q.Execute(c,t,"UPDATE revora_defense_talisman SET stage=8 WHERE uid=@uid AND stage=7","uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Tower invocation report changed.");
                Ready(p,()=>context() && AtInvocation(p,scene));t.Commit();
            }}Q.Install(p,new uint[0],new[]{invocation});SilverBalance.ApplyCommitted(p,silver);scene.Completed=true;return true;
        }
    }
}
