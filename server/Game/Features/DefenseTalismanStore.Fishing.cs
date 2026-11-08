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
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static int EncounterKind(GameState p){
            if(!Eligible(p))return -1;lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();
                var run=Read(c,null,p);
                if(run.Stage==6 && run.DesertStep<2 && Materials(c,null,p,721777).Any(id=>Material(c,null,p,id,721777)))return 3+run.DesertStep;
                if(run.Stage==4 && run.SpearRequested && Materials(c,null,p,721781).Count==0)return 2;
                return run.Stage==3 && Materials(c,null,p,721778).Count<3 && Materials(c,null,p,721780).Count==0
                    && Materials(c,null,p,721779).Any(id=>Material(c,null,p,id,721779))?0:-1;
            }
        }
        internal static bool FishKill(GameState p,Entity body,Guid attempt,Func<bool> context){
            Ready(p,context);
            if(!DefenseTalismanEncounters.Owns(p,body,attempt,0) || !body.Dead || !ReferenceEquals(body.Killer,p.Entity))return false;
            uint net;ConquerItem fish;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);if(Read(c,t,p).Stage!=3 || Materials(c,t,p,721778).Count>=3 || Materials(c,t,p,721780).Count!=0)return false;
                net=Materials(c,t,p,721779).FirstOrDefault(id=>Material(c,t,p,id,721779));if(net==0)return false;
                if(Q.Execute(c,t,"INSERT INTO revora_defense_talisman_fishing(attempt,uid,body,net) VALUES(@attempt,@uid,@body,@net) ON CONFLICT DO NOTHING",
                    "attempt",attempt,"uid",(long)p.Entity.UID,"body",(long)body.UID,"net",(long)net)!=1)return false;
                Consume(c,t,p,net,721779);fish=Q.New(721778,true);Track(c,t,p,fish);
                Ready(p,()=>context() && DefenseTalismanEncounters.Owns(p,body,attempt,0));t.Commit();
            }}Q.Install(p,new[]{net},new[]{fish});return true;
        }
    }
}
