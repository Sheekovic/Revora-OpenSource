using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static partial class HeartCampaign
    {
        private sealed class Visit
        {
            internal string Weekend,Faction;
            internal uint Npc;
            internal readonly List<Tuple<string,byte>> Menu=new List<Tuple<string,byte>>();
        }
        private static readonly ConditionalWeakTable<GameState,Visit> Visits=new ConditionalWeakTable<GameState,Visit>();
        private static void Choice(Visit v,string text,byte option){v.Menu.Add(Tuple.Create(text,option));}
        private static void Home(Visit v)
        {
            v.Menu.Clear();v.Faction=null;
            Choice(v,"Join the couples.",161);Choice(v,"Join the singles.",162);
            Choice(v,"Enter my sortie.",164);Choice(v,"Deliver the cache.",165);
            Choice(v,"Campaign standings.",166);Choice(v,"Claim my prizes.",167);
            Choice(v,"Campaign rules.",168);Choice(v,"Farewell.",255);
        }
        internal static List<Tuple<string,byte>> LastMenu(GameState p)
        {
            Visit v;return p!=null && Visits.TryGetValue(p,out v)?v.Menu.ToList():new List<Tuple<string,byte>>();
        }
        internal static bool HandleNpc(GameState p,NpcRequest request)
        {
            if(p==null || request==null)return false;
            bool cache=p.ActiveNpc==HeartEncounter.CacheNpc;
            if(!cache && (p.ActiveNpc!=380 || request.OptionID<160 || request.OptionID>168))
            {Visits.Remove(p);return false;}
            try
            {
                lock(p.ItemSyncRoot)
                {
                    string text=Page(p,request.OptionID);if(text==null)return true;
                    var dialog=new Npcs(p);dialog.Text(text);
                    foreach(var choice in LastMenu(p))dialog.Option(choice.Item1,choice.Item2);
                    dialog.Send();
                }
            }
            catch(Exception ex){Q.Fail(p,"heart-dialogue",ex);}
            return true;
        }
        internal static string Page(GameState p,byte option)
        {
            if(!Ready(p) || p.Map==null)return null;
            lock(p.ItemSyncRoot)
            {
                bool cache=p.ActiveNpc==HeartEncounter.CacheNpc;
                if(!Q.Near(p,cache?HeartEncounter.CacheNpc:380,cache?p.Entity.MapID:(ushort)1002))
                {Visits.Remove(p);return null;}
                Visit v=Visits.GetValue(p,_=>new Visit());
                var window=PlanetWeekend.Current(Clock());string weekend=window==null?null:window.Key;
                if(cache && option==0)
                {
                    v.Menu.Clear();v.Npc=p.ActiveNpc;v.Weekend=weekend;
                    Choice(v,"Take the cache home.",1);Choice(v,"Farewell.",255);
                    return "Defeat the raiders and their guardian, then bring this cache to the City Commander.";
                }
                if(!cache && option==160)
                {
                    v.Npc=380;v.Weekend=weekend;Home(v);
                    return "Couples and singles rally each weekend. Which banner will you carry?";
                }
                if(v.Npc!=p.ActiveNpc || !v.Menu.Any(c=>c.Item2==option))return null;
                v.Menu.Clear();
                if(v.Weekend!=weekend){v.Faction=null;return "The campaign has changed. Speak to me again.";}
                if(cache)
                {
                    Visits.Remove(p);
                    return option==1 && HeartEncounter.Collect(p)?"The cache is safe. Deliver it to the City Commander.":"The guardian still protects this cache.";
                }
                if(option==161 || option==162)
                {
                    if(window==null){Home(v);return "Return on Saturday. The campaign ends at Pacific midnight on Monday.";}
                    v.Faction=option==161?"couples":"singles";
                    Choice(v,"Carry this banner.",163);Choice(v,"Choose again.",160);Choice(v,"Farewell.",255);
                    return "Join the "+v.Faction+"? Your banner stays fixed for this weekend. Entry is free.";
                }
                if(option==163)
                {
                    string faction=v.Faction;v.Faction=null;
                    var joined=Enroll(p,faction);Home(v);
                    return joined!=null?"Your banner is ready. Enter when you are prepared.":"Your banner is already fixed, or your three sorties are complete.";
                }
                if(option==164)
                {
                    var run=Resume(p);
                    if(run!=null && run.Delivered)run=Enroll(p,run.Faction);
                    bool entered=run!=null && HeartEncounter.Enter(p);
                    if(entered){Visits.Remove(p);return null;}
                    Home(v);return "Choose a banner first. Deliver any recovered cache before entering again. Three sorties are allowed each weekend.";
                }
                if(option==165)
                {
                    var run=Resume(p);bool delivered=run!=null && Deliver(p,run.Id);Home(v);
                    return delivered?"A fine delivery! Your banner gains 100 support.":"I have no new cache to receive from you.";
                }
                if(option==166){Home(v);return Standings(p,weekend);}
                if(option==167){RecoverPrizes(p);if(!Ready(p))return null;Home(v);return "Any prizes ready for you have been delivered. Judging takes place after the weekend.";}
                if(option==168)
                {
                    Home(v);return "Three sorties each weekend: defeat ten raiders, their guardian, and recover each cache within fifteen minutes. Deliver all three for Heart wings lasting thirty days after judging. The winning banner also earns 5,000 CPs and 1,000 RVPs; ties reward both. Marriage is not required.";
                }
                return null;
            }
        }
        private static string Standings(GameState p,string weekend)
        {
            if(weekend==null)return "The next campaign opens Saturday, Pacific time. Prizes from finished campaigns can still be claimed.";
            long couples=0,singles=0,delivered=0;
            using(var c=Database.DataHolder.MySqlConnection)
            {
                c.Open();using(var q=Q.Command(c,null,@"SELECT e.faction,COUNT(*)*100,COUNT(*) FILTER(WHERE r.uid=@uid)
FROM revora_heart_runs r JOIN revora_heart_entries e USING(weekend,uid)
WHERE r.weekend=@weekend AND r.delivered_at IS NOT NULL GROUP BY e.faction","uid",(long)p.Entity.UID,"weekend",weekend))
                using(var r=q.ExecuteReader())while(r.Read())
                {if(r.GetString(0)=="couples")couples=r.GetInt64(1);else singles=r.GetInt64(1);delivered+=r.GetInt64(2);}
            }
            return "Couples: "+couples+". Singles: "+singles+". Your deliveries: "+delivered+"/3.";
        }
    }
}
