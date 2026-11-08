using System;
using System.Linq;
using Revora.Client;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static partial class DesertTreasureStore
    {
        internal static int EncounterKind(GameState p){
            if(!Eligible(p))return -1;lock(p.ItemSyncRoot)using(var c=Database.DataHolder.MySqlConnection){c.Open();
                return Read(c,null,p).Stage==6 && Materials(c,null,p,721872).Count==0 && Materials(c,null,p,721874).Any(id=>Material(c,null,p,id,721874))?0:-1;
            }
        }
        internal static bool CaptureRelic(GameState p,Entity body,Guid attempt,uint bottle,Func<bool> context){
            Ready(p,context);if(!DesertTreasureEncounter.Owns(p,body,attempt,0) || !body.Dead || !ReferenceEquals(body.Killer,p.Entity))return false;
            ConquerItem relic;
            using(var c=Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);if(Read(c,t,p).Stage!=6 || Materials(c,t,p,721872).Count>0 || !Material(c,t,p,bottle,721874))return false;
                if(Q.Execute(c,t,"INSERT INTO revora_desert_treasure_captures(attempt,uid,life,body) VALUES(@attempt,@uid,@life,@body) ON CONFLICT DO NOTHING","attempt",attempt,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"body",(long)body.UID)!=1)return false;
                Consume(c,t,p,bottle,721874);relic=Q.New(721872,true);Track(c,t,p,relic);
                Ready(p,()=>context() && DesertTreasureEncounter.Owns(p,body,attempt,0));t.Commit();
            }}Q.Install(p,new[]{bottle},new[]{relic});return true;
        }
    }
}
