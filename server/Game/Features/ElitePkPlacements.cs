using System;
using System.Collections.Generic;
using System.Linq;
using Npgsql;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Verified tournament results, not a claim inferred from owning a prize pack.
    internal static class ElitePkPlacements
    {
        private sealed class Bout {
            internal int Round,Slot;internal uint Left,Right,Winner;internal long LeftDamage,RightDamage;internal string Kind,Reason;
            internal bool Contested {get{return Right!=0 && Winner!=0 && (Reason=="defeat" || Reason=="timeout") && LeftDamage+RightDamage>0;}}
            internal IEnumerable<uint> Fighters {get{return new[]{Left,Right}.Where(u=>u!=0);}}
            internal IEnumerable<uint> Losers {get{return Fighters.Where(u=>u!=Winner);}}
        }
        internal static void Initialize(NpgsqlConnection c,NpgsqlTransaction t) {
            Q.Execute(c,t,@"ALTER TABLE revora_epk_matches ADD COLUMN IF NOT EXISTS kind varchar(12) NOT NULL DEFAULT 'elimination';
CREATE TABLE IF NOT EXISTS revora_epk_placements(
run varchar(32) NOT NULL,uid bigint NOT NULL,bracket integer NOT NULL CHECK(bracket BETWEEN 0 AND 3),signup_level integer NOT NULL,
place integer NOT NULL CHECK(place BETWEEN 1 AND 5),pack_type integer NOT NULL CHECK(pack_type BETWEEN 720714 AND 720729),
earned_at timestamptz NOT NULL,expires_at timestamptz NOT NULL,PRIMARY KEY(run,uid),
FOREIGN KEY(run,uid) REFERENCES revora_epk_entries(run,uid));");
        }
        internal static bool Record(NpgsqlConnection c,NpgsqlTransaction t,string run,uint winner,DateTime utc) {
            int bracket;DateTime start,end;bool wardrobe;
            using(var q=Q.Command(c,t,"SELECT bracket,starts_at,deadline,local_wardrobe FROM revora_epk_runs WHERE run=@run","run",run))
            using(var d=q.ExecuteReader()){if(!d.Read())throw new InvalidOperationException("Elite prize run missing.");bracket=d.GetInt32(0);start=d.GetFieldValue<DateTimeOffset>(1).UtcDateTime;end=d.GetFieldValue<DateTimeOffset>(2).UtcDateTime;wardrobe=d.GetBoolean(3);}
            if(utc<start || utc>=end)throw new InvalidOperationException("Elite result outside its window.");
            var entrants=new Dictionary<uint,ElitePkResults.Entry>();
            using(var q=Q.Command(c,t,"SELECT uid,level,power,seed FROM revora_epk_entries WHERE run=@run","run",run))
            using(var d=q.ExecuteReader())while(d.Read()){
                uint uid=Convert.ToUInt32(d[0]);int level=d.GetInt32(1);
                if(level<1 || level>byte.MaxValue || ElitePkEvent.Bracket(level)!=bracket)throw new InvalidOperationException("Elite entrant bracket changed.");
                entrants.Add(uid,new ElitePkResults.Entry{Uid=uid,Level=level,Power=d.GetInt32(2),Seed=d.GetGuid(3)});
            }
            if(entrants.Count<2 || winner==0)return false;
            if(entrants.Count>32 || !entrants.ContainsKey(winner))throw new InvalidOperationException("Elite prize roster invalid.");
            var bouts=new List<Bout>();
            using(var q=Q.Command(c,t,"SELECT round,slot,left_uid,right_uid,winner,left_damage,right_damage,outcome,kind FROM revora_epk_matches WHERE run=@run ORDER BY round,slot","run",run))
            using(var d=q.ExecuteReader())while(d.Read()){
                if(d.IsDBNull(4))throw new InvalidOperationException("Elite prize has unresolved bouts.");
                bouts.Add(new Bout{Round=d.GetInt32(0),Slot=d.GetInt32(1),Left=Convert.ToUInt32(d[2]),Right=Convert.ToUInt32(d[3]),Winner=Convert.ToUInt32(d[4]),
                    LeftDamage=d.GetInt64(5),RightDamage=d.GetInt64(6),Reason=d.GetString(7),Kind=d.GetString(8)});
            }
            if(bouts.Count==0)return false;
            foreach(var b in bouts){
                if(b.Kind!="elimination" && b.Kind!="bronze" || b.Left==0 || b.Left==b.Right || b.Fighters.Any(u=>!entrants.ContainsKey(u))
                    || b.LeftDamage<0 || b.RightDamage<0 || b.LeftDamage>long.MaxValue-b.RightDamage || b.Winner!=0 && !b.Fighters.Contains(b.Winner))
                    throw new InvalidOperationException("Elite prize bout identity invalid.");
                if(b.Right==0){if(b.Reason!="bye" || b.LeftDamage!=0 || b.RightDamage!=0)throw new InvalidOperationException("Elite bye invalid.");}
                else if(b.Reason=="idle"){if(b.Winner!=0 || b.LeftDamage+b.RightDamage!=0)throw new InvalidOperationException("Elite idle bout invalid.");}
                else if(b.Reason=="no-show"){if(b.LeftDamage+b.RightDamage!=0)throw new InvalidOperationException("Elite absent bout has damage.");}
                else if(b.Reason=="defeat" || b.Reason=="timeout"){
                    if(!b.Contested)throw new InvalidOperationException("Elite contested bout has no combat.");
                    if(b.Reason=="timeout"){
                        var x=entrants[b.Left];var y=entrants[b.Right];
                        uint expected=b.LeftDamage!=b.RightDamage?(b.LeftDamage>b.RightDamage?x.Uid:y.Uid):x.Power!=y.Power?(x.Power>y.Power?x.Uid:y.Uid)
                            :x.Level!=y.Level?(x.Level>y.Level?x.Uid:y.Uid):x.Seed.CompareTo(y.Seed)<0?x.Uid:y.Uid;
                        if(expected!=b.Winner)throw new InvalidOperationException("Elite timeout winner contradicts scores.");
                    }
                }else if(b.Reason!="forfeit")throw new InvalidOperationException("Elite bout outcome invalid.");
            }
            var elimination=bouts.Where(b=>b.Kind=="elimination").ToArray();
            var alive=new HashSet<uint>(entrants.Keys);int round=0;
            foreach(var group in elimination.GroupBy(b=>b.Round)){
                if(group.Key!=++round || alive.Count<2)throw new InvalidOperationException("Elite round order invalid.");
                int size=1;while(size<alive.Count)size*=2;var matches=group.ToArray();var fighters=matches.SelectMany(b=>b.Fighters).ToArray();
                if(matches.Length!=size/2 || !matches.Select(b=>b.Slot).SequenceEqual(Enumerable.Range(0,matches.Length))
                    || fighters.Length!=alive.Count || fighters.Distinct().Count()!=fighters.Length || !alive.SetEquals(fighters)
                    || matches.Count(b=>b.Right==0)!=size-alive.Count)throw new InvalidOperationException("Elite advancement graph invalid.");
                alive=new HashSet<uint>(matches.Where(b=>b.Winner!=0).Select(b=>b.Winner));
            }
            var final=elimination.Where(b=>b.Round==round).ToArray();
            if(final.Length!=1 || final[0].Winner!=winner || !alive.SetEquals(new[]{winner}))return false;
            bool championEarned=elimination.Any(b=>b.Winner==winner && b.Contested);
            var semis=elimination.Where(b=>b.Round==round-1).ToArray();var bronze=bouts.Where(b=>b.Kind=="bronze").ToArray();
            var semiLosers=new HashSet<uint>(semis.SelectMany(b=>b.Losers));
            var bronzeEligible=new HashSet<uint>(semis.Where(b=>b.Contested).SelectMany(b=>b.Losers));
            if(bronze.Length>1 || bronze.Any(b=>b.Round!=round || b.Slot!=1 || b.Fighters.Any(u=>!bronzeEligible.Contains(u))))
                throw new InvalidOperationException("Elite bronze qualification invalid.");
            var participated=new HashSet<uint>(elimination.Where(b=>b.Contested).SelectMany(b=>b.Fighters));
            var places=new Dictionary<uint,int>();if(championEarned)places.Add(winner,1);
            Action<uint,int> place=(uid,n)=>{if(uid!=0 && participated.Contains(uid) && !places.ContainsKey(uid))places.Add(uid,n);};
            foreach(uint uid in final[0].Losers)place(uid,2);
            if(bronze.Length==1){place(bronze[0].Winner,3);foreach(uint uid in bronze[0].Losers)place(uid,4);}
            foreach(uint uid in semiLosers)place(uid,4);
            foreach(uint uid in elimination.Where(b=>b.Round==round-2).SelectMany(b=>b.Losers))place(uid,5);
            if(places.Count>8)throw new InvalidOperationException("Elite prize field exceeds top eight.");
            var zone=TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
            DateTime expiry=TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(start,zone).AddDays(7),DateTimeKind.Unspecified),zone);
            foreach(var p in places){
                int pack=720714+bracket*4+(p.Value==1?3:p.Value==2?2:p.Value==3?1:0);
                if(Q.Execute(c,t,@"INSERT INTO revora_epk_placements(run,uid,bracket,signup_level,place,pack_type,earned_at,expires_at)
VALUES(@run,@uid,@bracket,@level,@place,@pack,@now,@expiry)","run",run,"uid",(long)p.Key,"bracket",bracket,"level",entrants[p.Key].Level,
                    "place",p.Value,"pack",pack,"now",utc,"expiry",expiry)!=1)throw new InvalidOperationException("Elite placement was not saved.");
                if(wardrobe && bracket==3 && championEarned) {
                    int key=p.Value==1?20352035:p.Value==2?20362036:p.Value==3?20372037:20382038;
                    if(!ProgressionEvidence.Grant(c,t,p.Key,"elite-pk-local-wardrobe-v1",run+":"+key,key,utc))
                        throw new InvalidOperationException("Elite wardrobe reward was not saved.");
                }
            }
            return championEarned;
        }
    }
}
