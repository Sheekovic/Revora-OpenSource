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
        private static bool Near(GameState p,ushort map,ushort x,ushort y){return p.Entity.MapID==map && Kernel.GetDistance(p.Entity.X,p.Entity.Y,x,y)<=6;}
        internal static bool PlumReferral(GameState p,Func<bool> context){
            Ready(p,context);if(!Near(p,1000,472,578))return false;ulong silver;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);if(Read(c,t,p).Stage!=5 || !Materials(c,t,p,721777).Any(id=>Material(c,t,p,id,721777)))return false;
                SilverBalance.AssertSaved(c,t,p);silver=checked(p.Entity.Money+100);if(silver>Entity.MaximumSilver)return false;
                SilverBalance.Persist(c,t,p,silver);
                if(Q.Execute(c,t,"UPDATE revora_defense_talisman SET stage=6 WHERE uid=@uid AND stage=5","uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Tower Plum referral changed.");
                Ready(p,()=>context() && Near(p,1000,472,578));t.Commit();
            }}SilverBalance.ApplyCommitted(p,silver);return true;
        }
        internal static bool DesertKill(GameState p,Entity body,Guid attempt,int kind,Func<bool> context){
            Ready(p,context);if(kind<3 || kind>4 || !DefenseTalismanEncounters.Owns(p,body,attempt,kind) || !body.Dead || !ReferenceEquals(body.Killer,p.Entity))return false;
            uint splinter=0;ConquerItem cloth=null;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=6 || run.DesertStep!=kind-3)return false;
                uint proof=Materials(c,t,p,721777).FirstOrDefault(id=>Material(c,t,p,id,721777));if(proof==0)return false;
                if(Q.Execute(c,t,"INSERT INTO revora_tower_desert_kills(attempt,uid,kind,body) VALUES(@attempt,@uid,@kind,@body) ON CONFLICT DO NOTHING",
                    "attempt",attempt,"uid",(long)p.Entity.UID,"kind",kind,"body",(long)body.UID)!=1)return false;
                if(kind==4){Consume(c,t,p,proof,721777);splinter=proof;cloth=Q.New(721782,true);Track(c,t,p,cloth);}
                if(Q.Execute(c,t,"UPDATE revora_defense_talisman SET desert_step=@next WHERE uid=@uid AND stage=6 AND desert_step=@before",
                    "next",kind-2,"uid",(long)p.Entity.UID,"before",kind-3)!=1)throw new InvalidOperationException("Tower desert progress changed.");
                Ready(p,()=>context() && DefenseTalismanEncounters.Owns(p,body,attempt,kind));t.Commit();
            }}if(cloth!=null)Q.Install(p,new[]{splinter},new[]{cloth});return true;
        }
        internal static bool ReportHandkerchief(GameState p,Func<bool> context){
            Ready(p,context);if(!Near(p,1015,732,516))return false;uint cloth;ulong silver;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=6 || run.DesertStep!=2)return false;
                cloth=Materials(c,t,p,721782).FirstOrDefault(id=>Material(c,t,p,id,721782));if(cloth==0)return false;
                SilverBalance.AssertSaved(c,t,p);silver=checked(p.Entity.Money+100);if(silver>Entity.MaximumSilver)return false;
                Consume(c,t,p,cloth,721782);SilverBalance.Persist(c,t,p,silver);
                if(Q.Execute(c,t,"UPDATE revora_defense_talisman SET stage=7 WHERE uid=@uid AND stage=6","uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Tower handkerchief report changed.");
                Ready(p,()=>context() && Near(p,1015,732,516));t.Commit();
            }}Q.Install(p,new[]{cloth},new ConquerItem[0]);SilverBalance.ApplyCommitted(p,silver);return true;
        }
    }
}
