using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static partial class DesertExpeditionStore
    {
        internal static Func<int,int> HuntRoll=max=>Kernel.Random.Next(max);
        private static readonly ConditionalWeakTable<Entity,object> deaths=new ConditionalWeakTable<Entity,object>();
        internal static void Revived(Entity body){if(body!=null && !body.Dead)lock(body)deaths.Remove(body);}
        private static bool Death(GameState p,Entity body){return body!=null && body.MapID==1001 && DailySoulBall.WildernessDeath(body,p.Entity)
            && ReferenceEquals(body.Killer,p.Entity) && Kernel.GetDistance(p.Entity.X,p.Entity.Y,body.X,body.Y)<=Constants.pScreenDistance;}
        internal static bool Hunt(GameState p,Entity body,Func<bool> context){
            Ready(p,context);if(body==null)return false;lock(body){object used;
                if(!Death(p,body) || deaths.TryGetValue(body,out used))return false;deaths.Add(body,new object());
                ConquerItem added;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Owner(c,t,p);var run=Read(c,t,p);uint family=body.MonsterInfo.ID;uint type=710259;
                    if(run.Stage!=2 || (family!=57 && family!=82) || !run.PillUntil.HasValue)return false;
                    var now=PillNow();
                    if(now>=run.PillUntil.Value || PillDue(run.PillUntil.Value,now,run.PillPaid)!=0)return false;
                    if(Materials(c,t,p,type).Count>=10 || !Materials(c,t,p,721920).Any(id=>Material(c,t,p,id,721920)))return false;
                    if(p.Inventory.Count>=40)return false;
                    int roll=HuntRoll(100);if(roll<0 || roll>=100)throw new InvalidOperationException("Desert Expedition hunt roll outside range.");
                    if(roll>=50)return false; // Documented Revora odds, once per death.
                    added=Q.New(type,true);Track(c,t,p,added);
                    var committedAt=PillNow();if(committedAt>=run.PillUntil.Value || PillDue(run.PillUntil.Value,committedAt,run.PillPaid)!=0)return false;
                    Ready(p,()=>context() && Death(p,body));t.Commit();
                }}Q.Install(p,new uint[0],new[]{added});return true;
            }
        }
    }
}
