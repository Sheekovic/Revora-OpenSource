using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using R=Revora.Game.Features.FlameLitRules;
using S=Revora.Game.Features.FlameLitStore;

namespace Revora.Game.Features
{
    internal static class FlameLitQuest
    {
        private static bool enabled;
        private sealed class Offer {internal uint Npc,Rune;internal byte Class;internal DateTime Until;}
        private static readonly ConditionalWeakTable<GameState,Offer> offers=new ConditionalWeakTable<GameState,Offer>();
        internal static bool Handles(uint npc){return npc>=4452 && npc<=4462;}
        internal static void Initialize() {
            if(!GuildSeasonWar.Enabled)throw new InvalidOperationException("Flame requires the Guild War coordinator.");
            var actors=new List<NpcSpawn>{new NpcSpawn{UID=R.Taoist,Name="Flame Taoist",MapID=1002,X=370,Y=346,Mesh=6810,Type=Enums.NpcType.Talker}};
            for(uint item=R.FirstRune;item<R.DragonRune;item++) {
                R.Step step;R.TryStep(item,out step);
                actors.Add(new NpcSpawn{UID=step.Npc,Name="Flame Stone",MapID=step.Map,X=step.X,Y=step.Y,Mesh=(ushort)(6570+step.Completed*10),Type=Enums.NpcType.Talker});
            }
            foreach(var actor in actors) {
                Map map;if(!Kernel.Maps.TryGetValue(actor.MapID,out map) || map.BaseID!=actor.MapID)throw new InvalidOperationException("Flame map missing.");
                bool approach=false;
                for(int dx=-6;dx<=6;dx++)for(int dy=-6;dy<=6;dy++)if(Math.Max(Math.Abs(dx),Math.Abs(dy))>1 && map.Floor[actor.X+dx,actor.Y+dy,MapObjectType.Player])approach=true;
                if(!approach)throw new InvalidOperationException("Flame stone approach blocked.");
                foreach(var priorMap in Kernel.Maps.Values) {
                    Revora.Interfaces.INpc prior;if(!priorMap.Npcs.TryGetValue(actor.UID,out prior))continue;
                    if(prior.Mesh!=actor.Mesh || priorMap.ID!=actor.MapID && priorMap.ID!=5001)
                        throw new InvalidOperationException("Flame actor identity collision.");
                }
            }
            S.Initialize();
            foreach(var actor in actors) {
                foreach(var map in Kernel.Maps.Values){Revora.Interfaces.INpc prior;if(map.Npcs.TryGetValue(actor.UID,out prior))map.RemoveNpc(prior);}
                Kernel.Maps[actor.MapID].AddNpc(actor);
            }
            enabled=true;
        }
        private static bool Near(GameState p,uint npc) {
            if(!Q.Player(p) || p.Trade==null || p.Trade.InTrade || p.Booth!=null || p.Entity.PokerTable!=0)return false;
            ushort map=1002,x=370,y=346;
            if(npc!=R.Taoist){R.Step step;if(!R.TryStep(R.FirstRune+npc-4453,out step))return false;map=step.Map;x=step.X;y=step.Y;}
            Revora.Interfaces.INpc actor;
            return p.Map!=null && p.Map.ID==map && p.Map.BaseID==map && p.Entity.MapID==map && p.Map.Npcs.TryGetValue(npc,out actor)
                && actor.MapID==map && actor.X==x && actor.Y==y && Kernel.GetDistance(p.Entity.X,p.Entity.Y,x,y)<=6;
        }
        private static uint Claimable(GameState p) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,@"SELECT f.rune_uid FROM revora_flame_runs f
JOIN revora_guild_battles b ON b.day=f.war_day JOIN items i ON i.uid=f.rune_uid AND i.entityid=f.uid
WHERE f.uid=@uid AND NOT f.claimed AND f.stage>0 AND b.ends_at<=@now AND i.position=0 AND i.warehouse=0
ORDER BY b.ends_at,f.id LIMIT 1","uid",(long)p.Entity.UID,"now",GuildSeasonWar.Clock())) {
                var value=q.ExecuteScalar();return value==null?0:checked((uint)Convert.ToInt64(value));
            }}
        }
        private static void Menu(GameState p,uint npc,string message=null) {
            offers.Remove(p);uint rune=npc==R.Taoist?Claimable(p):0;
            if(npc!=R.Taoist)using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,@"SELECT f.rune_uid FROM revora_flame_runs f
JOIN revora_guild_battles b ON b.day=f.war_day WHERE f.uid=@uid AND NOT f.claimed AND f.stage=@stage
AND b.state='active' AND b.starts_at<=@now AND b.ends_at>@now ORDER BY b.starts_at DESC LIMIT 1",
                "uid",(long)p.Entity.UID,"stage",(int)(npc-4453),"now",GuildSeasonWar.Clock())) {
                var value=q.ExecuteScalar();if(value!=null){uint current=checked((uint)Convert.ToInt64(value));if(p.Inventory.ContainsUID(current))rune=current;}
            }}
            offers.Add(p,new Offer{Npc=npc,Rune=rune,Class=p.Entity.Class,Until=GuildSeasonWar.Clock().AddSeconds(60)});
            var dialog=new Npcs(p);
            dialog.Text(message??(npc==R.Taoist?"Carry my rune to the ten flame stones during Guild War. Return after the battle for your reward.":"A faint flame waits within the stone."));
            if(npc==R.Taoist){dialog.Option("Give me a rune.",1);if(rune!=0)dialog.Option("Claim my reward.",2);}
            else if(rune!=0)dialog.Option("Light the stone.",1);
            dialog.Option("Farewell.",255);dialog.Send();
        }
        private static bool Light(GameState p,uint rune,bool begin) {
            using(var access=GuildSeasonWar.AcquireFlame()) {
                if(access==null)return false;
                S.Change change;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    change=begin?S.Begin(c,t,p,access):S.Advance(c,t,p,access,rune);t.Commit();
                }}
                if(change==null)return false;
                Q.Install(p,change.Removed==0?new uint[0]:new[]{change.Removed},new[]{change.Added});
                if(change.Stage==10)Q.Refresh(p);return true;
            }
        }
        internal static bool HandleNpc(GameState p,NpcRequest request) {
            if(p==null || !Handles(p.ActiveNpc))return false;
            if(!enabled || request==null || p.ItemSyncRoot==null)return true;
            uint npc=p.ActiveNpc;if(request.NpcID!=0 && request.NpcID!=npc)return true;
            lock(p.ItemSyncRoot)try {
                if(!Near(p,npc))return true;
                if(request.OptionID==255){offers.Remove(p);p.ActiveNpc=0;return true;}
                if(request.OptionID==0){Menu(p,npc);return true;}
                Offer offer;if(!offers.TryGetValue(p,out offer) || offer.Npc!=npc || offer.Class!=p.Entity.Class || GuildSeasonWar.Clock()>=offer.Until)return true;
                offers.Remove(p);string message="The flame is silent. Check your rune and return during Guild War.";
                if(request.OptionID==1) {
                    if(npc==R.Taoist && p.Inventory.Count>=40)message="Make room in your bag for my rune.";
                    else if(Light(p,offer.Rune,npc==R.Taoist))message=npc==R.Taoist?"Keep this rune safe. Begin at the nearby flame stone.":"The flame awakens. Your rune will guide you onward.";
                }else if(request.OptionID==2 && npc==R.Taoist && offer.Rune!=0) {
                    lock(MentorExperience.Sync){S.Reward reward;
                        using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){reward=S.Claim(c,t,p,offer.Rune);t.Commit();}}
                        S.ApplyReward(p,reward);message=reward==null?"Keep your rune safe. Your reward is not ready.":"You have kept the flame alive. Accept this reward.";
                    }
                }else return true;
                Menu(p,npc,message);
            }catch(Exception ex){Q.Fail(p,"flame-dialogue",ex);}return true;
        }
        internal static bool TryUse(ConquerItem item,GameState p) {
            if(item==null || item.ID<R.FirstRune || item.ID>R.DragonRune)return false;
            if(!enabled || p==null || p.ItemSyncRoot==null)return true;
            lock(p.ItemSyncRoot)try {
                ConquerItem owned;if(!Q.Player(p) || !p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(item,owned))return true;
                if(item.ID==R.DragonRune){p.Send("Take your Dragon Rune to Flame Taoist after Guild War.");return true;}
                R.Step step;R.TryStep(item.ID,out step);
                if(!Near(p,step.Npc) || !Light(p,item.UID,false))p.Send("Seek the stone named on your rune during Guild War. The final flame opens in the last half-hour.");
            }catch(Exception ex){Q.Fail(p,"flame-rune",ex);}return true;
        }
    }
}
