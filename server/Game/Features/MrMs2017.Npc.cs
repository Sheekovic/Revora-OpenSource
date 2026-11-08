using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static partial class MrMs2017
    {
        private sealed class Visit
        {
            internal string Season,Category,CandidateName;
            internal uint Candidate,Item,ItemType,After;
            internal int FlowerPage;
            internal Guid Request;
            internal List<Tuple<string,byte>> Menu=new List<Tuple<string,byte>>();
            internal uint[] Candidates=new uint[0],Flowers=new uint[0];
        }
        private static readonly ConditionalWeakTable<GameState,Visit> Visits=new ConditionalWeakTable<GameState,Visit>();
        private static void Choice(Visit v,string label,byte option){v.Menu.Add(Tuple.Create(label,option));}
        private static void Home(Visit v)
        {
            v.Menu.Clear();v.Request=Guid.Empty;
            Choice(v,"Enter this month.",241);Choice(v,"Standings.",242);
            Choice(v,"Support a gentleman.",243);Choice(v,"Support a lady.",244);
            Choice(v,"Contest rules.",248);Choice(v,"Farewell.",255);
        }
        internal static List<Tuple<string,byte>> LastMenu(GameState p)
        {
            Visit v;return p!=null && Visits.TryGetValue(p,out v)?v.Menu.ToList():new List<Tuple<string,byte>>();
        }
        internal static bool HandleNpc(GameState p,NpcRequest request)
        {
            if(p==null || request==null)return false;
            if(p.ActiveNpc!=380){Visits.Remove(p);return false;}
            byte option=request.OptionID;
            if(!((option>=180 && option<=199)||(option>=240 && option<=248))){Visits.Remove(p);return false;}
            if(!Ready(p))return true;
            try
            {
                lock(p.ItemSyncRoot)
                {
                    string text=Page(p,option);if(text==null)return true;
                    var dialog=new Npcs(p);dialog.Text(text);
                    foreach(var choice in LastMenu(p))dialog.Option(choice.Item1,choice.Item2);
                    dialog.Send();
                }
            }
            catch(Exception ex){Console.WriteLine("[mrms2017] dialogue_failure="+ex.GetType().Name);}
            return true;
        }
        internal static string Page(GameState p,byte option)
        {
            if(!Ready(p) || p.Map==null || !Q.Near(p,380,1002))return null;
            lock(p.ItemSyncRoot)
            {
                Visit v=Visits.GetValue(p,_=>new Visit());
                string season=SeasonOf(Clock());
                if(option==240){v.Season=season;Home(v);return "The monthly flower contest is open. Who will wear the Romance wings?";}
                if(!v.Menu.Any(c=>c.Item2==option))return null;
                if(v.Season!=season){v.Season=season;Home(v);return "A new month has begun. Choose again.";}
                // Consume the displayed action before any mutation, including failed writes.
                v.Menu.Clear();
                if(option==241){bool added=Register(p,season);Home(v);return added?"Your name is entered. May the flowers favor you!":"You are already entered, or cannot enter right now.";}
                if(option==248){Home(v);return "Free entry each month. Give unbound flower bundles; tulips count double. The top three in each contest earn titles and Romance wings. First-place wings last forever; second lasts 360 days, third 180. Judging is at Pacific midnight as the month ends. Titles last 360 days; champions also earn a one-year title.";}
                if(option==242){Home(v);return Standings(season);}
                if(option==243 || option==244){v.Category=option==243?"mr":"ms";v.After=0;return Candidates(p,v);}
                if(option==245)return Candidates(p,v);
                if(option==246){v.After=0;return Candidates(p,v);}
                if(option>=180 && option<=187)
                {
                    int index=option-180;if(index>=v.Candidates.Length)return null;
                    v.Candidate=v.Candidates[index];
                    using(var c=Database.DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT name FROM entities WHERE uid=@uid","uid",(long)v.Candidate))v.CandidateName=SafeName(q.ExecuteScalar() as string);}
                    v.FlowerPage=0;return Flowers(p,v);
                }
                if(option==198){v.FlowerPage++;return Flowers(p,v);}
                if(option==199){v.FlowerPage=Math.Max(0,v.FlowerPage-1);return Flowers(p,v);}
                if(option>=190 && option<=197)
                {
                    int index=option-190;if(index>=v.Flowers.Length)return null;
                    ConquerItem item;
                    if(!p.Inventory.TryGetItem(v.Flowers[index],out item)){Home(v);return "That flower bundle is no longer in your bag.";}
                    v.Item=item.UID;v.ItemType=item.ID;v.Request=Guid.NewGuid();
                    Choice(v,"Give this bundle.",247);Choice(v,"Choose again.",240);Choice(v,"Farewell.",255);
                    return "Give one "+FlowerLabel(item.ID)+" bundle to "+v.CandidateName+" for "+FlowerPoints(item.ID)+" support?";
                }
                if(option==247)
                {
                    Guid request=v.Request;v.Request=Guid.Empty;
                    ConquerItem item;
                    bool given=request!=Guid.Empty && p.Inventory.TryGetItem(v.Item,out item) && item.ID==v.ItemType && Donate(p,v.Season,v.Candidate,v.Item,request);
                    Home(v);return given?"Your flowers have been delivered. Thank you!":"The gift could not be delivered. Check your flowers and try again.";
                }
                return null;
            }
        }
        private static string SafeName(string name){return new string((name??"Hero").Where(char.IsLetterOrDigit).Take(16).ToArray());}
        private static string FlowerLabel(uint id)
        {
            string[] names={"rose","lily","orchid","tulip"};
            return (id%1000)+" "+names[id/1000-751];
        }
        private static string Candidates(GameState p,Visit v)
        {
            var rows=new List<Tuple<uint,string>>();
            using(var c=Database.DataHolder.MySqlConnection)
            {
                c.Open();using(var q=Q.Command(c,null,@"SELECT e.uid,COALESCE(n.name,'Hero') FROM revora_mrms2017_entrants e
JOIN entities n ON n.uid=e.uid WHERE e.season=@season AND e.contest=@contest AND e.uid>@after AND e.uid<>@self ORDER BY e.uid LIMIT 9",
                    "season",v.Season,"contest",v.Category,"after",(long)v.After,"self",(long)p.Entity.UID))
                using(var r=q.ExecuteReader())while(r.Read())rows.Add(Tuple.Create(checked((uint)r.GetInt64(0)),SafeName(r.GetString(1))));
            }
            v.Candidates=rows.Take(8).Select(r=>r.Item1).ToArray();
            for(int i=0;i<v.Candidates.Length;i++)Choice(v,rows[i].Item2,(byte)(180+i));
            if(rows.Count>8){v.After=v.Candidates.Last();Choice(v,"More contestants.",245);}
            Choice(v,"First contestants.",246);Choice(v,"Back.",240);Choice(v,"Farewell.",255);
            return rows.Count==0?"No contestants here yet.":"Whom will you support?";
        }
        private static string Flowers(GameState p,Visit v)
        {
            var items=p.Inventory.Objects.Where(i=>FlowerPoints(i.ID)>0 && !i.Bound && i.Position==0 && i.Warehouse==0 && i.Lock==0 && !i.Suspicious && !i.InWardrobe)
                .OrderBy(i=>i.UID).Skip(v.FlowerPage*8).Take(9).ToArray();
            v.Flowers=items.Take(8).Select(i=>i.UID).ToArray();
            for(int i=0;i<v.Flowers.Length;i++)Choice(v,FlowerLabel(items[i].ID)+" ("+FlowerPoints(items[i].ID)+" support)",(byte)(190+i));
            if(items.Length>8)Choice(v,"More flowers.",198);
            if(v.FlowerPage>0)Choice(v,"Previous flowers.",199);
            Choice(v,"Back.",240);Choice(v,"Farewell.",255);
            return items.Length==0?"Bring an unbound flower bundle from your bag.":"Choose one bundle for "+v.CandidateName+".";
        }
        private static string Standings(string season)
        {
            var lines=new List<string>();
            using(var c=Database.DataHolder.MySqlConnection)
            {
                c.Open();using(var q=Q.Command(c,null,@"WITH ranked AS (
SELECT e.uid,e.contest,SUM(d.points) AS support,row_number() OVER(PARTITION BY e.contest ORDER BY SUM(d.points) DESC,e.registered_at,e.uid) AS place
FROM revora_mrms2017_entrants e JOIN revora_mrms2017_donations d ON d.season=e.season AND d.candidate=e.uid
WHERE e.season=@season GROUP BY e.uid,e.contest,e.registered_at)
SELECT r.contest,r.place,COALESCE(n.name,'Hero'),r.support FROM ranked r LEFT JOIN entities n ON n.uid=r.uid WHERE r.place<=3 ORDER BY r.contest,r.place","season",season))
                using(var r=q.ExecuteReader())while(r.Read())lines.Add(r.GetString(0)+" #"+r.GetInt64(1)+" "+SafeName(r.GetString(2))+": "+r.GetInt64(3));
            }
            return lines.Count==0?"No flowers have been given this month.":string.Join(". ",lines)+".";
        }
    }
}
