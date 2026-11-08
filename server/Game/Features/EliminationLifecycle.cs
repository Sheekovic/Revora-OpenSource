using System;
using System.Collections.Generic;
using Revora.Client;
using Revora.Database;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features {
    internal static class EliminationLifecycle {
        // Global release gate. No account/GM allowlist; enable only after native acceptance.
        internal static readonly bool ReleaseReady=false;
        internal static void Initialize() {
            if(!ReleaseReady)return;
            EliminationRescueWorld.Initialize();EliminationDenWorld.Initialize();RecoverStartup();EliminationQuest.Initialize();
        }
        internal static void RecoverStartup() {
            if(EliminationRescueWorld.Hunts.Count!=0 || EliminationDenWorld.Hunts.Count!=0)
                throw new InvalidOperationException("Elimination startup recovery requires retired runtime scenes.");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                // Exact owner/map joins prevent old receipts moving unrelated characters.
                var rows=new List<Tuple<uint,int>>();
                using(var q=Q.Command(c,t,@"SELECT e.uid,e.mapid FROM entities e WHERE EXISTS(
SELECT 1 FROM revora_elimination_runs r JOIN revora_elimination_rescue_bindings b ON b.run=r.id
WHERE r.uid=e.uid AND b.active AND b.map=e.mapid) OR EXISTS(
SELECT 1 FROM revora_elimination_runs r JOIN revora_elimination_den_bindings b ON b.run=r.id
WHERE r.uid=e.uid AND b.active AND b.map=e.mapid) ORDER BY e.uid FOR UPDATE OF e"))
                using(var r=q.ExecuteReader())while(r.Read())rows.Add(Tuple.Create(checked((uint)r.GetInt64(0)),Convert.ToInt32(r[1])));
                foreach(var row in rows){GameState live;
                    if(Kernel.GamePool.TryGetValue(row.Item1,out live) && Q.Player(live,false))throw new InvalidOperationException("Elimination recovery cannot move an admitted player.");
                    if(Kernel.Maps.ContainsKey(checked((ushort)row.Item2)))throw new InvalidOperationException("Elimination recovery map is still registered.");
                    if(Q.Execute(c,t,"UPDATE entities SET mapid=1011,x=309,y=256 WHERE uid=@uid AND mapid=@map","uid",(long)row.Item1,"map",row.Item2)!=1)
                        throw new InvalidOperationException("Elimination startup return was not saved.");
                }
                Q.Execute(c,t,"UPDATE revora_elimination_rescue_bindings SET active=false WHERE active");
                Q.Execute(c,t,"UPDATE revora_elimination_den_bindings SET active=false WHERE active");t.Commit();
            }}
        }
    }
}
