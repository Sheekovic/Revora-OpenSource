using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using Status=Revora.Network.GamePackets.MsgTaskStatus.QuestData.QuestStatus;

namespace Revora.Game.Features
{
    internal static class PirateEpicJournal
    {
        internal const uint Mission=6868;
        private static bool enabled;
        internal sealed class View {
            internal bool Attempted,Heart;
            internal Status Status {get{return Heart?Status.Finished:Attempted?Status.Accepted:Status.Available;}}
        }
        private static readonly ConditionalWeakTable<GameState,View> views=new ConditionalWeakTable<GameState,View>();
        // Called only after the private quest store schemas have initialized.
        internal static void Initialize(){QuestInfo.Register((QuestID)Mission,QuestType.Nezha_Feud);enabled=true;}
        internal static bool Protect(uint mission){return enabled && mission==Mission;}
        internal static void Refresh(GameState p,bool send=true) {
            if(!enabled || !Q.Player(p,false))return;
            lock(p.ItemSyncRoot)try {
                if(!Q.Player(p,false))return;
                var view=new View();
                using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,@"SELECT
EXISTS(SELECT 1 FROM revora_pirate_map_purchases WHERE uid=@uid) OR
EXISTS(SELECT 1 FROM revora_pirate_flowers WHERE uid=@uid) OR
EXISTS(SELECT 1 FROM revora_pirate_searches WHERE uid=@uid),
EXISTS(SELECT 1 FROM revora_pirate_searches WHERE uid=@uid AND input_type=3307447 AND 3307449=ANY(reward_types))",
                    "uid",(long)p.Entity.UID))using(var r=q.ExecuteReader()){
                    if(!r.Read())throw new InvalidOperationException("Pirate journal evidence missing.");
                    view.Attempted=r.GetBoolean(0);view.Heart=r.GetBoolean(1);
                }}
                views.Remove(p);views.Add(p,view);if(send && p.Quests!=null)p.Quests.SendFullGUI();
            }catch(Exception ex){views.Remove(p);Q.Fail(p,"pirate-journal",ex);}
        }
        internal static View Snapshot(GameState p) {
            if(!enabled || !Q.Player(p,false))return null;
            lock(p.ItemSyncRoot){
                View view;if(!views.TryGetValue(p,out view)){Refresh(p,false);if(!views.TryGetValue(p,out view))return null;}
                return view.Attempted || PirateEpicRules.Eligible(p.Entity.Class,p.Entity.Reborn,p.Entity.Level)?view:null;
            }
        }
        internal static void Fill(GameState p,List<MsgTaskStatus.QuestData> list,HashSet<uint> seen) {
            if(!enabled)return;seen.Add(Mission);
            var view=Snapshot(p);if(view!=null)list.Add(MsgTaskStatus.QuestData.Create((QuestID)Mission,view.Status,0));
        }
    }
}
