using System;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static partial class DefenseTalismanStore
    {
        // The invocation remains an owned entrance key until reconstruction consumes it.
        internal static bool PrepareDungeon(GameState p,Func<bool> context){
            Ready(p,context);if(!Near(p,1000,85,313))return false;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=8 || !Materials(c,t,p,721786).Any(id=>Material(c,t,p,id,721786)))return false;
                if(run.DungeonFloor==0 && Q.Execute(c,t,"UPDATE revora_defense_talisman SET dungeon_floor=1 WHERE uid=@uid AND stage=8 AND dungeon_floor=0","uid",(long)p.Entity.UID)!=1)
                    throw new InvalidOperationException("Tower dungeon admission changed.");
                Ready(p,()=>context() && Near(p,1000,85,313));t.Commit();return true;
            }}
        }
    }
}
