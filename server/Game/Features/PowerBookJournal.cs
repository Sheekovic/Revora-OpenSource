using System;
using System.Collections.Generic;
using Revora.Client;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static class PowerBookJournal
    {
        internal static void Initialize(){for(uint id=1840;id<=1852;id++)QuestInfo.Register((QuestID)id,QuestType.EquipmentBonus);}
        internal static bool Protect(uint id){return PowerBookQuest.Enabled && id>=1840 && id<=1852;}
        internal static void Refresh(GameState p){if(PowerBookQuest.Enabled && Q.Player(p,false) && p.Quests!=null)p.Quests.SendFullGUI();}
        internal static void Fill(GameState p,List<MsgTaskStatus.QuestData> list,HashSet<uint> seen)
        {
            if(!PowerBookQuest.Enabled)return;
            for(uint id=1840;id<=1852;id++)seen.Add(id);
            if(!Q.Player(p,false) || p.Entity.Level<115 || p.Entity.Reborn>2)return;
            lock(p.ItemSyncRoot)try{
                var run=PowerBookStore.Load(p);if(run==null)return;
                for(uint id=1840;id<=1852;id++){
                    int step=(int)id-1839;
                    var status=run.Stage>step?MsgTaskStatus.QuestData.QuestStatus.Finished:run.Stage==step?MsgTaskStatus.QuestData.QuestStatus.Accepted:MsgTaskStatus.QuestData.QuestStatus.Available;
                    list.Add(MsgTaskStatus.QuestData.Create((QuestID)id,status,0));
                }
            }catch(Exception ex){Q.Fail(p,"power-book-journal",ex);}
        }
    }
}
