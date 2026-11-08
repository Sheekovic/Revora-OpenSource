using System;
using System.Collections.Generic;
using System.Linq;

namespace Revora.Game.Features
{
    // Pure tournament rules. Callers must supply committed contest results;
    // these functions do not establish participation or award anything.
    internal static class GuildPkRules
    {
        internal sealed class Score
        {
            internal readonly uint Guild;
            internal readonly long Points;
            internal readonly DateTime Reached;
            internal Score(uint guild,long points,DateTime reached) {
                if(guild==0 || guild>ushort.MaxValue || points<=0 || reached.Kind!=DateTimeKind.Utc)
                    throw new ArgumentException("Invalid guild contest score.");
                Guild=guild;Points=points;Reached=reached;
            }
        }
        internal sealed class Pair
        {
            internal readonly int Round,Group;
            internal readonly uint First,Second;
            internal Pair(int round,int group,uint first,uint second) {Round=round;Group=group;First=first;Second=second;}
        }
        internal sealed class Result
        {
            internal readonly Pair Match;
            internal readonly uint Winner;
            internal readonly bool Played;
            internal Result(Pair match,uint winner,bool played) {
                if(match==null || winner!=0 && winner!=match.First && winner!=match.Second || !played && winner!=0)
                    throw new ArgumentException("Invalid Guild PK result.");
                Match=match;Winner=winner;Played=played;
            }
        }
        internal sealed class Standing
        {
            internal readonly uint Guild;
            internal readonly int Group,Seed,Points;
            internal Standing(uint guild,int group,int seed,int points){Guild=guild;Group=group;Seed=seed;Points=points;}
        }
        internal static uint[] Qualify(IEnumerable<Score> scores) {
            if(scores==null)throw new ArgumentNullException("scores");
            var entries=scores.ToArray();
            if(entries.Any(s=>s==null) || entries.Select(s=>s.Guild).Distinct().Count()!=entries.Length)
                throw new ArgumentException("Guild contest scores must have unique owners.");
            // Exact timestamp ties use guild ID as a documented Revora fallback.
            return entries.OrderByDescending(s=>s.Points).ThenBy(s=>s.Reached).ThenBy(s=>s.Guild).Take(8).Select(s=>s.Guild).ToArray();
        }
        internal static Pair[] GroupMatches(uint[] seeds) {
            if(seeds==null || seeds.Length>8 || seeds.Any(s=>s==0 || s>ushort.MaxValue) || seeds.Distinct().Count()!=seeds.Length)
                throw new ArgumentException("Guild PK seeds must be distinct qualified guilds.");
            var result=new List<Pair>();
            // Odd ranks form group A; even ranks form group B. Each pair meets
            // once and every guild has at most one opponent in each round.
            int[,] opponents={{0,3,1,2},{0,2,3,1},{0,1,2,3}};
            for(int round=0;round<3;round++)for(int group=0;group<2;group++)for(int match=0;match<2;match++) {
                int first=opponents[round,match*2]*2+group,second=opponents[round,match*2+1]*2+group;
                // Vacant seeds produce no match, entry credit or synthetic win.
                if(first<seeds.Length && second<seeds.Length)result.Add(new Pair(round+1,group,seeds[first],seeds[second]));
            }
            return result.ToArray();
        }
        internal static Standing[] GroupStandings(uint[] seeds,IEnumerable<Result> results) {
            var expected=GroupMatches(seeds); // Also validates the frozen seeds.
            if(results==null)throw new ArgumentNullException("results");
            var supplied=results.ToArray();
            if(supplied.Length!=expected.Length)throw new ArgumentException("Every scheduled group match requires a terminal result.");
            var points=seeds.ToDictionary(g=>g,g=>0);
            var used=new HashSet<string>();
            foreach(var result in supplied) {
                if(result==null)throw new ArgumentException("Missing Guild PK result.");
                var match=result.Match;
                if(!expected.Any(p=>p.Round==match.Round && p.Group==match.Group && p.First==match.First && p.Second==match.Second)
                    || !used.Add(match.Round+":"+match.Group+":"+match.First+":"+match.Second))
                    throw new ArgumentException("Unexpected or duplicate Guild PK match.");
                if(!result.Played)continue; // Cancelled matches award no points.
                if(result.Winner==0){points[match.First]++;points[match.Second]++;}
                else points[result.Winner]+=3;
            }
            // The historical guide does not settle equal group points. Revora
            // retains the stronger original qualifying seed as its tie-break.
            return seeds.Select((guild,index)=>new Standing(guild,index%2,index+1,points[guild]))
                .OrderBy(s=>s.Group).ThenByDescending(s=>s.Points).ThenBy(s=>s.Seed).ToArray();
        }
        internal static Pair[] FinalMatches(uint[] seeds,IEnumerable<Result> results) {
            var standings=GroupStandings(seeds,results);
            var first=standings.Where(s=>s.Group==0).ToArray();var second=standings.Where(s=>s.Group==1).ToArray();
            var pairs=new List<Pair>();
            for(int place=0;place<Math.Min(first.Length,second.Length);place++)
                pairs.Add(new Pair(4,place,first[place].Guild,second[place].Guild));
            // Missing opposite-group ranks produce no fictional match or win.
            return pairs.ToArray();
        }
        internal sealed class Placement {
            internal readonly uint Guild;internal readonly int Rank;internal readonly Pair Match;
            internal Placement(uint guild,int rank,Pair match){Guild=guild;Rank=rank;Match=match;}
        }
        internal static Placement[] FinalPlacements(uint[] seeds,IEnumerable<Result> groups,IEnumerable<Result> finals) {
            var expected=FinalMatches(seeds,groups);
            if(finals==null)throw new ArgumentNullException("finals");var results=finals.ToArray();
            if(results.Length!=expected.Length)throw new ArgumentException("Every final requires a terminal result.");
            var used=new HashSet<int>();var places=new List<Placement>();
            foreach(var result in results) {
                if(result==null || !expected.Any(p=>p.Round==result.Match.Round && p.Group==result.Match.Group && p.First==result.Match.First && p.Second==result.Match.Second)
                    || !used.Add(result.Match.Group))throw new ArgumentException("Unexpected or duplicate Guild PK final.");
                if(!result.Played)continue;
                var pair=result.Match;uint winner=result.Winner;
                // Revora final-draw tie-break: the stronger original qualifier seed.
                if(winner==0)winner=Array.IndexOf(seeds,pair.First)<Array.IndexOf(seeds,pair.Second)?pair.First:pair.Second;
                places.Add(new Placement(winner,2*pair.Group+1,pair));
                places.Add(new Placement(winner==pair.First?pair.Second:pair.First,2*pair.Group+2,pair));
            }
            return places.OrderBy(p=>p.Rank).ToArray();
        }
        internal sealed class Decision {
            internal readonly int Side;internal readonly string Reason;
            internal Decision(int side,string reason){Side=side;Reason=reason;}
        }
        // Side 0 is a draw, 1/2 the winning guild, 3 an empty cancellation.
        internal static Decision Decide(long first,long second,int firstRemaining,int secondRemaining,TimeSpan elapsed,bool expired) {
            if(first<0 || second<0 || firstRemaining<0 || secondRemaining<0)throw new ArgumentException("Invalid Guild PK match totals.");
            if(elapsed<TimeSpan.Zero)return null;
            if(firstRemaining==0 && secondRemaining==0)return new Decision(3,"empty");
            if(firstRemaining==0 || secondRemaining==0)return new Decision(firstRemaining==0?2:1,"elimination");
            if(expired)return new Decision(first==second?0:first>second?1:2,"time");
            if(elapsed>=TimeSpan.FromMinutes(30)) {
                // Decimal arithmetic avoids overflow and preserves the exact 50% boundary.
                if(first>0 && 2m*first>=3m*second)return new Decision(1,"lead");
                if(second>0 && 2m*second>=3m*first)return new Decision(2,"lead");
            }
            return null;
        }
        internal static DateTime MatchStart(DateTime qualifierStartUtc,int round,TimeZoneInfo zone) {
            if(qualifierStartUtc.Kind!=DateTimeKind.Utc || zone==null || round<1 || round>4)
                throw new ArgumentException("Invalid Guild PK schedule.");
            var monday=TimeZoneInfo.ConvertTimeFromUtc(qualifierStartUtc,zone).Date;
            if(monday.DayOfWeek!=DayOfWeek.Monday)throw new ArgumentException("Guild PK requires a Monday qualifier.");
            var local=monday.AddDays(round==4?5:round).AddHours(round==4?21:20).AddMinutes(30);
            return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local,DateTimeKind.Unspecified),zone);
        }
        internal const long PoolLimit=1000000000;
        internal static Dictionary<uint,long> DividePool(long amount,IDictionary<uint,long> weights) {
            if(amount<0 || amount>PoolLimit || weights==null || weights.Count>4096 || weights.Any(p=>p.Key==0 || p.Value<=0))throw new ArgumentException("Invalid Guild PK reward pool.");
            var shares=new Dictionary<uint,long>();if(weights.Count==0)return shares;
            decimal total=weights.Sum(p=>(decimal)p.Value);long allocated=0;
            foreach(var pair in weights){long share=(long)decimal.Floor(amount*(decimal)pair.Value/total);shares.Add(pair.Key,share);allocated+=share;}
            foreach(var pair in weights.OrderByDescending(p=>amount*(decimal)p.Value%total).ThenBy(p=>p.Key).Take(checked((int)(amount-allocated))))shares[pair.Key]++;
            return shares;
        }
        internal static long FinalSilver(int rank) {
            long[] amounts={120000000,100000000,80000000,65000000,50000000,40000000,30000000,20000000};
            if(rank<1 || rank>8)throw new ArgumentOutOfRangeException("rank");return amounts[rank-1];
        }
        // Revora resolves the guide's missing equal/lower-BP cases as one exploit.
        internal static int KillExploits(int killerPower,int victimPower) {
            if(killerPower<0 || victimPower<0)throw new ArgumentOutOfRangeException();
            long difference=(long)victimPower-killerPower;
            return difference>20?3:difference>0?2:1;
        }
        internal static uint FinalCps(int rank) {
            uint[] amounts={3000,2000,1000,600,500,400,300,200};
            if(rank<1 || rank>8)throw new ArgumentOutOfRangeException("rank");return amounts[rank-1];
        }
        internal static bool PrizeWindow(DateTime qualifier,TimeZoneInfo zone,DateTime utc) {
            if(utc.Kind!=DateTimeKind.Utc)throw new ArgumentException("Guild PK prize clock requires UTC.");
            var opens=MatchStart(qualifier,4,zone).AddHours(2);return utc>=opens && utc<opens.AddMinutes(30);
        }
        internal static long ContestPrize(int rank) {
            switch(rank) {
                case 1:return 60000000;case 2:return 50000000;case 3:return 40000000;case 4:return 35000000;
                case 5:return 30000000;case 6:return 25000000;case 7:return 20000000;case 8:return 15000000;
                default:throw new ArgumentOutOfRangeException("rank");
            }
        }
        internal static int ContestBand(byte level) {
            if(level==0)throw new ArgumentOutOfRangeException("level");
            return level<100?0:level<120?1:level<130?2:3;
        }
    }
}
