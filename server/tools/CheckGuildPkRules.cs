using System;
using System.Linq;
using Revora.Game.Features;

internal static class CheckGuildPkRules
{
    static void Require(bool value){if(!value)throw new InvalidOperationException("Guild PK rule assertion failed.");}
    static void Reject(Action action){bool rejected=false;try{action();}catch(ArgumentException){rejected=true;}Require(rejected);}
    static void Main() {
        var weights=new System.Collections.Generic.Dictionary<uint,long>{{1,1},{2,1},{3,1}};
        var shares=GuildPkRules.DividePool(10,weights);Require(shares[1]==4 && shares[2]==3 && shares[3]==3 && shares.Values.Sum()==10);
        shares=GuildPkRules.DividePool(1,weights);Require(shares[1]==1 && shares[2]==0 && shares[3]==0);
        weights=new System.Collections.Generic.Dictionary<uint,long>{{1,210},{2,60}};shares=GuildPkRules.DividePool(1000000,weights);Require(shares[1]==777778 && shares[2]==222222);
        weights=new System.Collections.Generic.Dictionary<uint,long>{{1,long.MaxValue},{2,long.MaxValue}};shares=GuildPkRules.DividePool(GuildPkRules.PoolLimit,weights);Require(shares[1]==500000000 && shares[2]==500000000);
        Require(GuildPkRules.DividePool(1000000,new System.Collections.Generic.Dictionary<uint,long>()).Count==0);
        Reject(()=>GuildPkRules.DividePool(-1,weights));Reject(()=>GuildPkRules.DividePool(GuildPkRules.PoolLimit+1,weights));
        Reject(()=>GuildPkRules.DividePool(1,new System.Collections.Generic.Dictionary<uint,long>{{1,0}}));

        Require(GuildPkRules.KillExploits(100,0)==1 && GuildPkRules.KillExploits(100,80)==1 && GuildPkRules.KillExploits(100,99)==1 && GuildPkRules.KillExploits(100,100)==1);
        Require(GuildPkRules.KillExploits(100,101)==2 && GuildPkRules.KillExploits(100,120)==2 && GuildPkRules.KillExploits(100,121)==3);
        Require(GuildPkRules.KillExploits(0,int.MaxValue)==3 && GuildPkRules.KillExploits(int.MaxValue,0)==1);
        Reject(()=>GuildPkRules.KillExploits(-1,100));Reject(()=>GuildPkRules.KillExploits(100,-1));

        var half=TimeSpan.FromMinutes(30);
        Require(GuildPkRules.Decide(150,100,1,1,half-TimeSpan.FromTicks(1),false)==null);
        Require(GuildPkRules.Decide(149,100,1,1,half,false)==null);
        Require(GuildPkRules.Decide(150,100,1,1,half,false).Side==1);
        Require(GuildPkRules.Decide(100,150,1,1,half,false).Side==2);
        Require(GuildPkRules.Decide(0,0,1,1,half,false)==null);
        Require(GuildPkRules.Decide(1,0,1,1,half,false).Side==1);
        Require(GuildPkRules.Decide(0,1,1,1,half,false).Side==2);
        Require(GuildPkRules.Decide(long.MaxValue,long.MaxValue,1,1,half,false)==null);
        Require(GuildPkRules.Decide(5,4,1,1,half,true).Side==1);
        Require(GuildPkRules.Decide(4,5,1,1,half,true).Side==2);
        Require(GuildPkRules.Decide(5,5,1,1,half,true).Side==0);
        Require(GuildPkRules.Decide(999,0,0,1,TimeSpan.Zero,false).Side==2);
        Require(GuildPkRules.Decide(0,999,1,0,TimeSpan.Zero,false).Side==1);
        Require(GuildPkRules.Decide(0,0,0,0,TimeSpan.Zero,false).Side==3);
        Require(GuildPkRules.Decide(0,0,0,0,TimeSpan.FromTicks(-1),false)==null);
        Reject(()=>GuildPkRules.Decide(-1,0,1,1,half,false));
        Reject(()=>GuildPkRules.Decide(0,0,-1,1,half,false));
        var time=new DateTime(2030,1,1,0,0,0,DateTimeKind.Utc);
        var scores=Enumerable.Range(1,10).Select(i=>new GuildPkRules.Score((uint)i,i==10?11:10,time.AddSeconds(i==9?0:i))).ToArray();
        uint[] expected={10,9,1,2,3,4,5,6};
        Require(GuildPkRules.Qualify(scores).SequenceEqual(expected));
        Require(GuildPkRules.Qualify(scores.Reverse()).SequenceEqual(expected));
        Require(GuildPkRules.Qualify(new[]{new GuildPkRules.Score(2,1,time),new GuildPkRules.Score(1,1,time)}).SequenceEqual(new uint[]{1,2}));
        Reject(()=>GuildPkRules.Qualify(new[]{scores[0],scores[0]}));Reject(()=>GuildPkRules.Qualify(null));
        Reject(()=>new GuildPkRules.Score(0,1,time));Reject(()=>new GuildPkRules.Score(1,0,time));Reject(()=>new GuildPkRules.Score(1,1,DateTime.SpecifyKind(time,DateTimeKind.Local)));
        for(int n=0;n<=8;n++) {
            uint[] seeds=Enumerable.Range(1,n).Select(i=>(uint)i).ToArray();var pairs=GuildPkRules.GroupMatches(seeds);
            int odd=(n+1)/2,even=n/2;Require(pairs.Length==odd*(odd-1)/2+even*(even-1)/2);
            foreach(var a in seeds)foreach(var b in seeds.Where(b=>b>a))
                Require(pairs.Count(p=>p.First==a && p.Second==b || p.First==b && p.Second==a)==(a%2==b%2?1:0));
            foreach(var p in pairs)Require(p.First!=p.Second && p.Round>=1 && p.Round<=3 && p.Group==(p.First%2==1?0:1));
            for(int round=1;round<=3;round++)foreach(var guild in seeds)Require(pairs.Count(p=>p.Round==round && (p.First==guild || p.Second==guild))<=1);
            var draws=pairs.Select(p=>new GuildPkRules.Result(p,0,true)).ToArray();
            var table=GuildPkRules.GroupStandings(seeds,draws);
            foreach(var entry in table)Require(entry.Points==(entry.Group==0?odd:even)-1 && entry.Seed==Array.IndexOf(seeds,entry.Guild)+1);
            Require(GuildPkRules.GroupStandings(seeds,draws.Reverse()).Select(e=>e.Guild).SequenceEqual(table.Select(e=>e.Guild)));
            var finals=GuildPkRules.FinalMatches(seeds,draws);Require(finals.Length==Math.Min(odd,even));
            for(int i=0;i<finals.Length;i++)Require(finals[i].Round==4 && finals[i].Group==i && finals[i].First==seeds[2*i] && finals[i].Second==seeds[2*i+1]);
            var finalDraws=finals.Select(p=>new GuildPkRules.Result(p,0,true)).ToArray();
            var finalPlaces=GuildPkRules.FinalPlacements(seeds,draws,finalDraws);
            Require(finalPlaces.Length==2*finals.Length && finalPlaces.Select(p=>p.Rank).SequenceEqual(Enumerable.Range(1,finalPlaces.Length)));
            foreach(var place in finalPlaces)Require(place.Guild==seeds[place.Rank-1]);
            Require(GuildPkRules.FinalPlacements(seeds,draws,finals.Select(p=>new GuildPkRules.Result(p,0,false))).Length==0);
            if(finals.Length>0) {
                Reject(()=>GuildPkRules.FinalPlacements(seeds,draws,finalDraws.Skip(1)));
                Reject(()=>GuildPkRules.FinalPlacements(seeds,draws,finalDraws.Concat(new[]{finalDraws[0]})));
                var reverse=GuildPkRules.FinalPlacements(seeds,draws,finals.Select(p=>new GuildPkRules.Result(p,p.Second,true)));
                foreach(var place in reverse)Require(place.Guild==(place.Rank%2==1?seeds[place.Rank]:seeds[place.Rank-2]));
            }
            var cancelled=pairs.Select(p=>new GuildPkRules.Result(p,0,false)).ToArray();Require(GuildPkRules.GroupStandings(seeds,cancelled).All(e=>e.Points==0));
            if(pairs.Length>0) {
                Reject(()=>GuildPkRules.GroupStandings(seeds,draws.Skip(1)));
                Reject(()=>GuildPkRules.GroupStandings(seeds,draws.Concat(new[]{draws[0]})));
                var altered=(GuildPkRules.Result[])draws.Clone();altered[0]=new GuildPkRules.Result(new GuildPkRules.Pair(4,0,pairs[0].First,pairs[0].Second),0,true);
                Reject(()=>GuildPkRules.GroupStandings(seeds,altered));
                var wins=pairs.Select(p=>new GuildPkRules.Result(p,Math.Max(p.First,p.Second),true)).ToArray();
                var ranked=GuildPkRules.GroupStandings(seeds,wins);
                foreach(var e in ranked)Require(e.Points==pairs.Count(p=>Math.Max(p.First,p.Second)==e.Guild)*3);
                var finalWins=GuildPkRules.FinalMatches(seeds,wins);
                if(finalWins.Length>0)Require(finalWins[0].First==seeds.Where(g=>g%2==1).Max() && finalWins[0].Second==seeds.Where(g=>g%2==0).Max());
            }

        }
        Reject(()=>GuildPkRules.GroupMatches(new uint[]{1,1}));Reject(()=>GuildPkRules.GroupMatches(new uint[]{0}));
        Reject(()=>GuildPkRules.GroupMatches(Enumerable.Range(1,9).Select(i=>(uint)i).ToArray()));
        Require(GuildPkRules.ContestBand(1)==0 && GuildPkRules.ContestBand(99)==0 && GuildPkRules.ContestBand(100)==1 && GuildPkRules.ContestBand(119)==1
            && GuildPkRules.ContestBand(120)==2 && GuildPkRules.ContestBand(129)==2 && GuildPkRules.ContestBand(130)==3 && GuildPkRules.ContestBand(140)==3);
        Reject(()=>GuildPkRules.ContestBand(0));
        long[] prizes={60000000,50000000,40000000,35000000,30000000,25000000,20000000,15000000};
        for(int rank=1;rank<=8;rank++)Require(GuildPkRules.ContestPrize(rank)==prizes[rank-1]);
        Reject(()=>GuildPkRules.ContestPrize(0));Reject(()=>GuildPkRules.ContestPrize(9));
        long[] finalSilver={120000000,100000000,80000000,65000000,50000000,40000000,30000000,20000000};
        uint[] finalCps={3000,2000,1000,600,500,400,300,200};
        for(int rank=1;rank<=8;rank++)Require(GuildPkRules.FinalSilver(rank)==finalSilver[rank-1] && GuildPkRules.FinalCps(rank)==finalCps[rank-1]);
        Reject(()=>GuildPkRules.FinalSilver(0));Reject(()=>GuildPkRules.FinalCps(9));


        var monday=new DateTime(2039,3,7,20,30,0,DateTimeKind.Utc); // UTC Monday, avoiding machine-local assumptions.
        Require(monday.DayOfWeek==DayOfWeek.Monday);
        var claimOpens=GuildPkRules.MatchStart(monday,4,TimeZoneInfo.Utc).AddHours(2);
        Require(!GuildPkRules.PrizeWindow(monday,TimeZoneInfo.Utc,claimOpens.AddTicks(-1)));
        Require(GuildPkRules.PrizeWindow(monday,TimeZoneInfo.Utc,claimOpens));
        Require(GuildPkRules.PrizeWindow(monday,TimeZoneInfo.Utc,claimOpens.AddMinutes(30).AddTicks(-1)));
        Require(!GuildPkRules.PrizeWindow(monday,TimeZoneInfo.Utc,claimOpens.AddMinutes(30)));

        for(int round=1;round<=4;round++) {
            var start=GuildPkRules.MatchStart(monday,round,TimeZoneInfo.Utc);
            Require(start==monday.AddDays(round==4?5:round).AddHours(round==4?1:0) && start.Kind==DateTimeKind.Utc);
        }
        Reject(()=>GuildPkRules.MatchStart(monday,0,TimeZoneInfo.Utc));Reject(()=>GuildPkRules.MatchStart(monday.AddDays(1),1,TimeZoneInfo.Utc));
        Reject(()=>new GuildPkRules.Result(new GuildPkRules.Pair(1,0,1,3),2,true));
        Reject(()=>new GuildPkRules.Result(new GuildPkRules.Pair(1,0,1,3),1,false));
        Console.WriteLine("Guild PK qualification, schedule, prizes, complete standings and 0-8 guild group/final pairing checks passed.");
    }
}
