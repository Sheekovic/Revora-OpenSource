using System;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static partial class AttackTalismanStore
    {
        internal static bool ClaimLiquid(GameState p,Func<bool> context){
            Ready(p,context);if(!Near(p,1000,490,618))return false;ConquerItem liquid;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var r=Read(c,t,p);if(r.Stage!=5 || r.Alchemy!=0 || p.Inventory.Count>=40 || !Materials(c,t,p,721803).Any(id=>Material(c,t,p,id,721803)) || !Materials(c,t,p,721798).Any(id=>Material(c,t,p,id,721798)))return false;
                liquid=Q.New(721802,true);Track(c,t,p,liquid);AlchemyStep(c,t,p,0,1);Ready(p,()=>context() && Near(p,1000,490,618));t.Commit();
            }}Q.Install(p,new uint[0],new[]{liquid});return true;
        }
        private static void AlchemyStep(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,GameState p,int before,int after){
            if(Q.Execute(c,t,"UPDATE revora_attack_talisman SET alchemy=@after WHERE uid=@uid AND stage=5 AND alchemy=@before","uid",(long)p.Entity.UID,"before",before,"after",after)!=1)throw new InvalidOperationException("Fan alchemy changed.");
        }
        internal static bool Transform(GameState p,uint used,Func<bool> context){
            Ready(p,context);ConquerItem input;if(!p.Inventory.TryGetItem(used,out input))return false;
            bool mixing=input.ID==721802 || input.ID==721803;if(!mixing && input.ID!=721804)return false;
            uint first,second;ConquerItem output;ulong silver=p.Entity.Money;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var r=Read(c,t,p);if(r.Stage!=5 || r.Alchemy!=(mixing?1:2) || !Material(c,t,p,used,input.ID))return false;
                uint a=mixing?721803u:721804u,b=mixing?721802u:721798u;
                first=Materials(c,t,p,a).FirstOrDefault(id=>Material(c,t,p,id,a));second=Materials(c,t,p,b).FirstOrDefault(id=>Material(c,t,p,id,b));if(first==0 || second==0)return false;
                if(!mixing){SilverBalance.AssertSaved(c,t,p);silver=checked(p.Entity.Money+100);if(silver>Entity.MaximumSilver)return false;}
                Consume(c,t,p,first,a);Consume(c,t,p,second,b);output=Q.New(mixing?721804u:721801u,true);Track(c,t,p,output);AlchemyStep(c,t,p,mixing?1:2,mixing?2:3);
                if(!mixing){SilverBalance.Persist(c,t,p,silver);Save(c,t,p,5,6);}Ready(p,context);t.Commit();
            }}Q.Install(p,new[]{first,second},new[]{output});if(!mixing)SilverBalance.ApplyCommitted(p,silver);return true;
        }
        internal static bool ReportPoem(GameState p,Func<bool> context){
            Ready(p,context);if(!Near(p,1011,788,456))return false;ulong silver;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var r=Read(c,t,p);if(r.Stage!=6 || r.Alchemy!=3 || !Materials(c,t,p,721801).Any(id=>Material(c,t,p,id,721801)) || !Materials(c,t,p,710218).Any(id=>Material(c,t,p,id,710218)))return false;
                SilverBalance.AssertSaved(c,t,p);silver=checked(p.Entity.Money+100);if(silver>Entity.MaximumSilver)return false;SilverBalance.Persist(c,t,p,silver);Save(c,t,p,6,7);
                Ready(p,()=>context() && Near(p,1011,788,456));t.Commit();
            }}SilverBalance.ApplyCommitted(p,silver);return true;
        }
    }
}
