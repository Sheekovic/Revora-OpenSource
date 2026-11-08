using System;
using System.Linq;
using Revora.Client;
using Q=Revora.Game.Features.QuestInventory;
using Revora.Database;
namespace Revora.Game.Features
{
    internal static partial class DefenseTalismanStore
    {
        // Revora puzzle: Time, Season, Dream. Exact original door-selection rules are unverified.
        internal static readonly ushort[,] Doors={{340,404},{354,402},{362,397}};
        private static bool NearDoor(GameState p,int site){return site>=0 && site<3 && p.Entity.MapID==1015
            && Kernel.GetDistance(p.Entity.X,p.Entity.Y,Doors[site,0],Doors[site,1])<=3;}
        internal static bool OpenDoor(GameState p,int site,Func<bool> context){
            Ready(p,context);if(!NearDoor(p,site))return false;uint consumed=0;ulong silver=p.Entity.Money;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=3 || run.DoorStep!=site)return false;
                uint poise=Materials(c,t,p,721780).FirstOrDefault(id=>Material(c,t,p,id,721780));if(poise==0)return false;
                if(site==2){
                    SilverBalance.AssertSaved(c,t,p);silver=checked(silver+100);if(silver>Entity.MaximumSilver)return false;
                    Consume(c,t,p,poise,721780);consumed=poise;SilverBalance.Persist(c,t,p,silver);
                }
                if(Q.Execute(c,t,"UPDATE revora_defense_talisman SET door_step=@next,stage=@stage WHERE uid=@uid AND stage=3 AND door_step=@before",
                    "next",site+1,"stage",site==2?4:3,"uid",(long)p.Entity.UID,"before",site)!=1)throw new InvalidOperationException("Tower door progress changed.");
                Ready(p,()=>context() && NearDoor(p,site));t.Commit();
            }}if(consumed!=0){Q.Install(p,new[]{consumed},new Revora.Network.GamePackets.ConquerItem[0]);SilverBalance.ApplyCommitted(p,silver);}return true;
        }
    }
}
