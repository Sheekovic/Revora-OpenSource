using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using R=Revora.Game.Features.EliminationRules;
using S=Revora.Game.Features.EliminationStore;

namespace Revora.Game.Features
{
    // No normal startup registration until the entire rescue/den route exists.
    internal static class EliminationQuest
    {
        private static bool enabled;
        private sealed class Offer {
            internal uint Npc,Missing;internal int Options;internal byte Level,Reborn;
            internal string Day;internal DateTime Until;
        }
        private static readonly ConditionalWeakTable<GameState,Offer> offers=new ConditionalWeakTable<GameState,Offer>();
        internal static void Initialize() {
            Map map;if(!Kernel.Maps.TryGetValue(R.Forest,out map) || map.BaseID!=R.Forest)throw new InvalidOperationException("Elimination forest missing.");
            var actors=new List<NpcSpawn>{new NpcSpawn{UID=R.Georgia,Name="Georgia",MapID=R.Forest,X=R.GeorgiaX,Y=R.GeorgiaY,Mesh=4680,Type=Enums.NpcType.Talker}};
            for(uint npc=R.FirstHerb;npc<R.FirstHerb+12;npc++) {
                var site=R.Herb(npc);actors.Add(new NpcSpawn{UID=npc,Name="Beauty Herb",MapID=R.Forest,X=site.X,Y=site.Y,Mesh=4700,Type=Enums.NpcType.Talker});
            }
            foreach(var actor in actors) {
                bool approach=false;
                for(int dx=-6;dx<=6;dx++)for(int dy=-6;dy<=6;dy++)if(Math.Max(Math.Abs(dx),Math.Abs(dy))>1 && map.Floor[actor.X+dx,actor.Y+dy,MapObjectType.Player])approach=true;
                if(!approach)throw new InvalidOperationException("Elimination actor approach blocked.");
                foreach(var priorMap in Kernel.Maps.Values) {
                    Revora.Interfaces.INpc prior;if(!priorMap.Npcs.TryGetValue(actor.UID,out prior))continue;
                    if(priorMap.ID!=R.Forest || prior.MapID!=R.Forest || prior.Mesh!=actor.Mesh || prior.X!=actor.X || prior.Y!=actor.Y)
                        throw new InvalidOperationException("Elimination actor identity collision.");
                }
            }
            S.Initialize();EliminationPills.Initialize();foreach(var actor in actors)if(!map.Npcs.ContainsKey(actor.UID))map.AddNpc(actor);enabled=true;
        }
        private static bool Near(GameState p,uint npc) {
            if(!Q.Player(p) || p.Trade==null || p.Trade.InTrade || p.Booth!=null || p.Entity.PokerTable!=0
                || p.Map==null || p.Map.ID!=R.Forest || p.Map.BaseID!=R.Forest || p.Entity.MapID!=R.Forest)return false;
            var site=R.Herb(npc);ushort x=npc==R.Georgia?R.GeorgiaX:site.X,y=npc==R.Georgia?R.GeorgiaY:site.Y;
            Revora.Interfaces.INpc actor;
            return p.Map.Npcs.TryGetValue(npc,out actor) && actor.MapID==R.Forest && actor.X==x && actor.Y==y
                && actor.Mesh==(npc==R.Georgia?4680:4700) && Kernel.GetDistance(p.Entity.X,p.Entity.Y,x,y)<=6;
        }
        private static void Menu(GameState p,uint npc,string message=null) {
            offers.Remove(p);var now=S.Clock();string day=SummerHunt.Day(now);int stage=-1,mask=0;Guid run=Guid.Empty;uint missing=0;
            using(var c=DataHolder.MySqlConnection){c.Open();
                using(var q=Q.Command(c,null,"SELECT id,stage,herbs FROM revora_elimination_runs WHERE uid=@uid AND day=@day","uid",(long)p.Entity.UID,"day",day))
                using(var r=q.ExecuteReader())if(r.Read()){run=r.GetGuid(0);stage=r.GetInt32(1);mask=r.GetInt32(2);}
                if(npc==R.Georgia && (stage==0 || stage==1 || stage==4)) {
                    string sql=stage==0?@"SELECT h.item_uid FROM revora_elimination_herbs h WHERE h.run=@run
AND NOT EXISTS(SELECT 1 FROM items i WHERE i.uid=h.item_uid) ORDER BY h.npc LIMIT 1":stage==4?@"SELECT h.item_uid FROM revora_elimination_pills h WHERE h.run=@run
AND NOT EXISTS(SELECT 1 FROM items i WHERE i.uid=h.item_uid) ORDER BY h.slot LIMIT 1":@"SELECT r.spice_uid FROM revora_elimination_runs r
WHERE r.id=@run AND r.spice_uid IS NOT NULL AND NOT EXISTS(SELECT 1 FROM items i WHERE i.uid=r.spice_uid)";
                    using(var q=Q.Command(c,null,sql,"run",run)){var value=q.ExecuteScalar();if(value!=null)missing=Convert.ToUInt32(value);}
                }
            }
            var offer=new Offer{Npc=npc,Missing=missing,Level=p.Entity.Level,Reborn=p.Entity.Reborn,Day=day,Until=now.AddSeconds(60)};
            var d=new Npcs(p);bool eligible=R.Eligible(p.Entity.Level,p.Entity.Reborn);
            d.Text(message??(!eligible?"This forest is dangerous. Return after rebirth, once you reach level 70.":npc!=R.Georgia?"A fragrant herb grows among the leaves.":stage<0?
                "Landy has vanished. Bring me twelve Beauty Herbs from these woods; their scent may draw out her captor.":stage==0?
                "Gather twelve Beauty Herbs. I will blend them into a spice no Rock Behemoth can resist.":stage==1?
                "Take the spice to the clearing at (311,256). Find Landy.":stage==2?"Return to the clearing at (311,256). Landy is counting on you.":stage==4?"Bring ten Fire Pills from Fire Spirits L43. Landy is growing weaker.":stage==5?"The poison is gone. The Rock Leader must still answer for this.":"Landy needs a cure. We must act quickly."));
            if(eligible) {
                if(npc==R.Georgia) {
                    if(stage<0){d.Option("I will find her.",1);offer.Options|=1<<1;}
                    if(stage==0 && mask==4095){d.Option("Blend these herbs.",2);offer.Options|=1<<2;}
                    if(missing!=0){d.Option("I lost a quest item.",3);offer.Options|=1<<3;}
                    if(stage==2){d.Option("Take me back to Landy.",9);offer.Options|=1<<9;}
                    if(stage==3){d.Option("Landy has been poisoned.",5);offer.Options|=1<<5;}
                    if(stage==5){d.Option("Take me to the den.",7);offer.Options|=1<<7;}
                    if(stage==6){d.Option("Rock Leader has fallen.",8);offer.Options|=1<<8;}
                    if(stage==4){d.Option("Here are ten Fire Pills.",6);offer.Options|=1<<6;}
                }else if(stage==0 && (mask & (1<<(int)(npc-R.FirstHerb)))==0){d.Option("Gather the herb.",1);offer.Options|=1<<1;}
            }
            if(npc==R.Georgia){d.Option("Take my old supplies.",4);offer.Options|=1<<4;}
            d.Option("Farewell.",255);offers.Add(p,offer);d.Send();
        }
        internal static bool HandleNpc(GameState p,NpcRequest request) {
            if(p==null || p.ActiveNpc!=R.Georgia && R.Herb(p.ActiveNpc)==null)return false;
            if(!enabled || request==null || p.ItemSyncRoot==null)return true;
            uint npc=p.ActiveNpc;if(request.NpcID!=0 && request.NpcID!=npc)return true;
            lock(p.ItemSyncRoot)try {
                if(!Near(p,npc))return true;
                if(request.OptionID==255){offers.Remove(p);p.ActiveNpc=0;return true;}
                if(request.OptionID==0){Menu(p,npc);return true;}
                Offer offer;
                if(request.OptionID>9 || !offers.TryGetValue(p,out offer) || offer.Npc!=npc || offer.Level!=p.Entity.Level || offer.Reborn!=p.Entity.Reborn
                    || S.Clock()>=offer.Until || SummerHunt.Day(S.Clock())!=offer.Day || (offer.Options & (1<<request.OptionID))==0)return true;
                offers.Remove(p);
                if((request.OptionID==3 || npc!=R.Georgia) && p.Inventory.Count>=40){Menu(p,npc,"Make room in your bag first.");return true;}
                if(request.OptionID==9){if(EliminationRescueWorld.Start(p,0)==null)Menu(p,npc,"The trail has gone cold.");return true;}
                if(request.OptionID==7){if(EliminationDenWorld.Start(p)==null)Menu(p,npc,"The path is not ready. Return in a moment.");return true;}
                if(request.OptionID==8){lock(MentorExperience.Sync){S.Reward reward;
                    using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){reward=S.Claim(c,t,p);t.Commit();}}
                    S.ApplyReward(p,reward);Menu(p,npc,reward==null?"You have already done enough today.":"Landy is safe. Take this reward, with our thanks.");}return true;}
                S.Change change=null;bool began=false;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    if(npc!=R.Georgia)change=S.Gather(c,t,p,npc);
                    else if(request.OptionID==1)began=S.Begin(c,t,p);
                    else if(request.OptionID==2)change=S.Spice(c,t,p);
                    else if(request.OptionID==3)change=S.Recover(c,t,p,offer.Missing);
                    else if(request.OptionID==4)change=S.Cleanup(c,t,p);
                    else if(request.OptionID==5)began=S.Report(c,t,p);
                    else if(request.OptionID==6)change=S.Cure(c,t,p);
                    t.Commit();
                }}
                S.Apply(p,change);
                Menu(p,npc,began?(request.OptionID==5?"Bring me ten Fire Pills from Fire Spirits L43. They may save her.":"Search the herbs nearby. I will wait here."):change==null?"There is nothing I can do just yet.":npc!=R.Georgia?"You carefully gather the herb.":
                    request.OptionID==6?"This will draw out the poison. We must still face the Rock Leader.":
                    request.OptionID==2?"The spice is ready. Use it in the clearing at (311,256).":request.OptionID==3?"Here is a replacement. Keep it safe.":"I have taken the old supplies.");
            }catch(Exception ex){Q.Fail(p,"elimination-dialogue",ex);}return true;
        }
        internal static bool TryUse(ConquerItem item,GameState p) {
            if(item==null || item.ID!=R.FancySpice)return false;
            if(!enabled || p==null || p.ItemSyncRoot==null)return true;
            lock(p.ItemSyncRoot)try {
                ConquerItem owned;if(!Q.Player(p) || !p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(item,owned))return true;
                if(!R.AtSpiceSite(p.Entity.MapID,p.Entity.X,p.Entity.Y)){p.Send("Use the spice in the clearing at (311,256).");return true;}
                if(EliminationRescueWorld.Start(p,item.UID)==null)p.Send("The scent fades. Check your supplies and try again.");
            }catch(Exception ex){Q.Fail(p,"elimination-spice-use",ex);}return true;
        }
        internal static void Tick(){if(!enabled)return;EliminationRescueWorld.Tick();EliminationDenWorld.Tick();}
    }
}
