// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using Revora.Network.GamePackets;

namespace Revora.Database
{
    public unsafe class ChampionTable
    {
        private sealed class Image { internal long? Points; internal long Revision; internal bool Invalid; internal Guid? Pending; }
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ChampionStatistic,Image> images = new System.Runtime.CompilerServices.ConditionalWeakTable<ChampionStatistic,Image>();
        private static ChampionStatistic Read(MySqlReader reader) {
            var stat=new ChampionStatistic(true);
            stat.UID=reader.ReadUInt32("EntityID");stat.Name=reader.ReadString("EntityName");
            stat.YesterdayRank=reader.ReadUInt32("YesterdayRank");stat.TotalMatches=reader.ReadByte("TotalMatches");stat.WinStreak=reader.ReadByte("WinStreak");
            stat.SignedUp=false;stat.Points=reader.ReadUInt32("Points");stat.TotalPoints=reader.ReadUInt32("TotalPoints");
            stat.TodayPoints=reader.ReadUInt32("TodayPoints");stat.YesterdayPoints=reader.ReadUInt32("YesterdayPoints");
            stat.Grade=(byte)stat.IsGrade;stat.Level=reader.ReadByte("Level");stat.Class=reader.ReadByte("Class");stat.Model=reader.ReadUInt32("Model");
            stat.LastReset=DateTime.FromBinary(reader.ReadInt64("LastReset"));
            long? points=reader.ReadNullableInt64Strict("points"),revision=reader.ReadNullableInt64Strict("revora_revision");
            if(points<0 || points>uint.MaxValue || !revision.HasValue || revision<0)throw new InvalidOperationException("Invalid Champion balance image.");
            images.Add(stat,new Image{Points=points,Revision=revision.Value});
            if(stat.LastReset.Date!=DateTime.Now.Date)Reset(stat);
            return stat;
        }
        public static void Load() {
            using(var c=DataHolder.MySqlConnection){c.Open();Game.Features.QuestInventory.Execute(c,null,"DO $revora_column$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_attribute WHERE attrelid='championarena'::regclass AND attname='revora_revision' AND NOT attisdropped) THEN ALTER TABLE championarena ADD COLUMN revora_revision bigint NOT NULL DEFAULT 0; END IF; END $revora_column$");}
            ChampionEntryLedger.InitializeAndRecover();
            Game.Features.ChampionPurchases.Initialize();
            Console.WriteLine("Loading Champion information!");
            using(var cmd=new MySqlCommand(MySqlCommandType.SELECT).Select("championarena"))
            using(var reader=new MySqlReader(cmd))while(reader.Read()){var stat=Read(reader);Game.Champion.ChampionStats.Add(stat.UID,stat);}
            Game.Champion.Sort();Game.Champion.YesterdaySort();
        }
        internal static void LoadClient(Client.GameState client) {
            ChampionStatistic stat;Image image;
            if(!Game.Champion.ChampionStats.TryGetValue(client.Entity.UID,out stat) || !images.TryGetValue(stat,out image) || image.Invalid) {
                using(var c=DataHolder.MySqlConnection){c.Open();Game.Features.QuestInventory.Execute(c,null,@"INSERT INTO championarena(entityid,entityname,points,level,class,model,lastreset)
VALUES(@uid,@name,0,@level,@class,@model,@reset) ON CONFLICT(entityid) DO NOTHING",
                    "uid",(long)client.Entity.UID,"name",client.Entity.Name,"level",(int)client.Entity.Level,"class",(int)client.Entity.Class,"model",(long)client.Entity.Mesh,"reset",DateTime.Now.Ticks);}
                using(var cmd=new MySqlCommand(MySqlCommandType.SELECT).Select("championarena").Where("EntityID",client.Entity.UID))
                using(var reader=new MySqlReader(cmd)){if(!reader.Read())throw new InvalidOperationException("Champion owner row is missing.");stat=Read(reader);}
                Game.Champion.ChampionStats[client.Entity.UID]=stat;
            }
            lock(stat) {
                Image pendingImage;if(images.TryGetValue(stat,out pendingImage) && pendingImage.Pending.HasValue)
                    throw new InvalidOperationException("Champion ending must settle before loading its owner.");
                if(stat.UID!=client.Entity.UID)throw new InvalidOperationException("Champion cache identity differs.");
                client.ChampionStats=stat;stat.Level=client.Entity.Level;stat.Class=client.Entity.Class;stat.Model=client.Entity.Mesh;stat.Name=client.Entity.Name;
                if(stat.LastReset.Date!=DateTime.Now.Date)Reset(stat);
            }
            Game.Champion.Clear(client);
        }
        internal static void Invalidate(ChampionStatistic stat,Exception ex) {
            Image image;if(images.TryGetValue(stat,out image))image.Invalid=true;
            Client.GameState p;
            if(Kernel.GamePool.TryGetValue(stat.UID,out p) && ReferenceEquals(p.ChampionStats,stat))Game.Features.QuestInventory.Fail(p,"champion-save",ex);
        }
        public static void SaveStatistics(ChampionStatistic stats,Npgsql.NpgsqlConnection conn) {
            if(stats==null)return;
            lock(stats)try {
                Image image;
                if(!images.TryGetValue(stats,out image) || image.Invalid)throw new InvalidOperationException("Champion statistic requires reload.");
                if(image.Pending.HasValue)return; // The saved ending owns this row until its award commits.
                long nextRevision=checked(image.Revision+1);uint points=stats.Points;
                if(Game.Features.QuestInventory.Execute(conn,null,@"UPDATE championarena SET yesterdayrank=@rank,points=@points,yesterdaypoints=@yesterday,
todaypoints=@today,totalpoints=@total,totalmatches=@matches,winstreak=@streak,signedup=@signed,
level=@level,class=@class,entityname=@name,lastreset=@reset,model=@model,revora_revision=@next
WHERE entityid=@uid AND revora_revision=@previous AND points IS NOT DISTINCT FROM @oldpoints",
                    "rank",(long)stats.YesterdayRank,"points",(long)points,"yesterday",(long)stats.YesterdayPoints,"today",(long)stats.TodayPoints,
                    "total",(long)stats.TotalPoints,"matches",(int)stats.TotalMatches,"streak",(int)stats.WinStreak,"signed",stats.SignedUp?1:0,
                    "level",(int)stats.Level,"class",(int)stats.Class,"name",stats.Name,"reset",stats.LastReset.Ticks,"model",(long)stats.Model,
                    "next",nextRevision,"uid",(long)stats.UID,"previous",image.Revision,"oldpoints",(object)image.Points??DBNull.Value)!=1)
                    throw new InvalidOperationException("Champion balance revision changed.");
                image.Points=points;image.Revision=nextRevision;
            }catch(Exception ex){Invalidate(stats,ex);throw;}
        }
        internal static long PersistPoints(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,ChampionStatistic stats,uint next) {
            Image image;
            if(t==null || !System.Threading.Monitor.IsEntered(stats) || !images.TryGetValue(stats,out image) || image.Invalid
                || image.Pending.HasValue || image.Points.GetValueOrDefault()!=stats.Points)throw new InvalidOperationException("Champion point debit requires its saved owner image.");
            long revision=checked(image.Revision+1);
            if(Game.Features.QuestInventory.Execute(c,t,@"UPDATE championarena SET points=@next,revora_revision=@revision
WHERE entityid=@uid AND revora_revision=@old AND points IS NOT DISTINCT FROM @points",
                "next",(long)next,"revision",revision,"uid",(long)stats.UID,"old",image.Revision,"points",(object)image.Points??DBNull.Value)!=1)
                throw new InvalidOperationException("Champion point debit preimage changed.");
            return revision;
        }
        internal static void PointsCommitted(ChampionStatistic stats,uint points,long revision) {
            Image image;
            if(!System.Threading.Monitor.IsEntered(stats) || !images.TryGetValue(stats,out image) || image.Invalid)
                throw new InvalidOperationException("Champion point install requires its owner image.");
            image.Points=points;image.Revision=revision;image.Pending=null;stats.Points=points;stats.Grade=(byte)stats.IsGrade;
        }
        internal sealed class ResultAward {
            internal ChampionStatistic Stats;
            internal uint Award,Extra,Points,Today,Total;
            internal byte Matches,Streak;
            internal long Revision;
            internal uint Uid,Yesterday,YesterdayRank;
            internal int Grade;
            internal long? Before;
            internal long ReservedRevision,Reset;
        }
        internal static Action ResultBeforeCommit,ResultBeforeInstall;
        internal static void InitializeResults() {
            using(var c=DataHolder.MySqlConnection){c.Open();Game.Features.QuestInventory.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_champion_results(
run uuid PRIMARY KEY REFERENCES revora_champion_entries(id),player1 bigint NOT NULL,player2 bigint NOT NULL,
winner bigint NOT NULL,created_at timestamptz NOT NULL,
CHECK(player1<>player2 AND (winner=0 OR winner=player1 OR winner=player2)));
CREATE TABLE IF NOT EXISTS revora_champion_result_awards(
run uuid NOT NULL REFERENCES revora_champion_results(run),uid bigint NOT NULL REFERENCES championarena(entityid),
grade integer NOT NULL CHECK(grade BETWEEN 1 AND 7),award integer NOT NULL CHECK(award BETWEEN 0 AND 130),
points_before bigint NOT NULL,points_after bigint NOT NULL CHECK(points_after BETWEEN 0 AND 304400),
today_after bigint NOT NULL,total_after bigint NOT NULL,matches_after integer NOT NULL,streak_after integer NOT NULL,
revision bigint NOT NULL,PRIMARY KEY(run,uid));
CREATE TABLE IF NOT EXISTS revora_champion_outcomes(
run uuid PRIMARY KEY REFERENCES revora_champion_entries(id),player1 bigint NOT NULL,player2 bigint NOT NULL,
winner bigint NOT NULL,observed_at timestamptz NOT NULL,
CHECK(player1<>player2 AND (winner=0 OR winner=player1 OR winner=player2)));
CREATE TABLE IF NOT EXISTS revora_champion_outcome_players(
run uuid NOT NULL REFERENCES revora_champion_outcomes(run),uid bigint NOT NULL REFERENCES championarena(entityid),
grade integer NOT NULL CHECK(grade BETWEEN 1 AND 7),award integer NOT NULL CHECK(award BETWEEN 0 AND 130),
points_before bigint CHECK(points_before BETWEEN 0 AND 4294967295),reserved_revision bigint NOT NULL CHECK(reserved_revision>0),
points_after bigint NOT NULL CHECK(points_after BETWEEN 0 AND 304400),
today_after bigint NOT NULL CHECK(today_after BETWEEN 0 AND 4294967295),total_after bigint NOT NULL CHECK(total_after BETWEEN 0 AND 4294967295),
matches_after integer NOT NULL CHECK(matches_after BETWEEN 0 AND 255),streak_after integer NOT NULL CHECK(streak_after BETWEEN 0 AND 255),
lastreset bigint NOT NULL,yesterday_points bigint NOT NULL,yesterday_rank bigint NOT NULL,PRIMARY KEY(run,uid));");}
        }
        private static uint AddBounded(uint value,uint increment,uint limit) { return (uint)Math.Min((ulong)limit,(ulong)value+increment); }
        private sealed class Outcome {
            internal uint First,Second,Winner;
            internal DateTime Observed;
            internal ResultAward[] Awards;
        }
        private static void RequireOwners(Game.Champion.QualifierList.QualifierGroup group) {
            foreach(var p in new[]{group.Player1,group.Player2})if(!System.Threading.Monitor.IsEntered(p.ItemSyncRoot)
                || !Game.Features.SessionInstall.Holds(p.Entity.UID) || !System.Threading.Monitor.IsEntered(p.ChampionStats))
                throw new InvalidOperationException("Champion ending requires locked owners.");
        }
        private static Outcome ReadOutcome(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,Guid run) {
            Outcome result;
            using(var q=Game.Features.QuestInventory.Command(c,t,"SELECT player1,player2,winner,observed_at AT TIME ZONE 'UTC' FROM revora_champion_outcomes WHERE run=@run FOR UPDATE","run",run))
            using(var r=q.ExecuteReader()) {
                if(!r.Read())return null;
                result=new Outcome{First=checked((uint)r.GetInt64(0)),Second=checked((uint)r.GetInt64(1)),Winner=checked((uint)r.GetInt64(2)),Observed=DateTime.SpecifyKind(r.GetDateTime(3),DateTimeKind.Utc)};
            }
            var awards=new System.Collections.Generic.List<ResultAward>();
            using(var q=Game.Features.QuestInventory.Command(c,t,@"SELECT uid,grade,award,points_before,reserved_revision,points_after,
today_after,total_after,matches_after,streak_after,lastreset,yesterday_points,yesterday_rank
FROM revora_champion_outcome_players WHERE run=@run ORDER BY uid","run",run))
            using(var r=q.ExecuteReader())while(r.Read()) {
                var a=new ResultAward{Uid=checked((uint)r.GetInt64(0)),Grade=r.GetInt32(1),Award=checked((uint)r.GetInt32(2)),
                    Before=r.IsDBNull(3)?(long?)null:r.GetInt64(3),ReservedRevision=r.GetInt64(4),Points=checked((uint)r.GetInt64(5)),
                    Today=checked((uint)r.GetInt64(6)),Total=checked((uint)r.GetInt64(7)),Matches=checked((byte)r.GetInt32(8)),Streak=checked((byte)r.GetInt32(9)),
                    Reset=r.GetInt64(10),Yesterday=checked((uint)r.GetInt64(11)),YesterdayRank=checked((uint)r.GetInt64(12))};
                a.Revision=checked(a.ReservedRevision+1);a.Extra=a.Uid==result.Winner?checked(a.Award-Game.Champion.ChampionPoints[a.Grade][1]):0;awards.Add(a);
            }
            if(awards.Count!=2 || !awards.Exists(a=>a.Uid==result.First) || !awards.Exists(a=>a.Uid==result.Second))
                throw new InvalidOperationException("Champion ending is missing its paired snapshots.");
            result.Awards=awards.ToArray();return result;
        }
        private static void SameOutcome(Outcome outcome,Game.Champion.QualifierList.QualifierGroup group) {
            if(outcome==null || outcome.First!=group.Player1.Entity.UID || outcome.Second!=group.Player2.Entity.UID
                || outcome.Winner!=(group.Winner==null?0:group.Winner.Entity.UID))throw new InvalidOperationException("Champion saved ending differs.");
        }
        internal static bool CaptureOutcome(Game.Champion.QualifierList.QualifierGroup group) {
            RequireOwners(group);bool committing=false;
            var players=new[]{group.Player1,group.Player2};
            try {
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    var existing=ReadOutcome(c,t,group.EntryKey);
                    if(existing!=null){SameOutcome(existing,group);return true;}
                    if(!group.Done || !group.EntryConfirmed || !group.OwnsImportedMap)return false;
                    if(!((group.Winner==null && group.Loser==null)
                        || (ReferenceEquals(group.Winner,players[0]) && ReferenceEquals(group.Loser,players[1]))
                        || (ReferenceEquals(group.Winner,players[1]) && ReferenceEquals(group.Loser,players[0]))))return false;
                    foreach(var p in System.Linq.Enumerable.OrderBy(players,p=>p.Entity.UID)) {
                        if(!Game.Features.QuestInventory.Player(p,false))return false;
                        SkillTable.LockOwner(c,t,p);
                    }
                    using(var q=Game.Features.QuestInventory.Command(c,t,@"SELECT count(*) FROM revora_champion_entries e
WHERE e.id=@run AND e.state='entered' AND e.closed_at IS NULL
AND (SELECT count(*) FROM revora_champion_entry_players p WHERE p.run=e.id AND p.released_at IS NULL AND p.uid IN(@first,@second))=2",
                        "run",group.EntryKey,"first",(long)players[0].Entity.UID,"second",(long)players[1].Entity.UID))
                        if(Convert.ToInt64(q.ExecuteScalar())!=1)throw new InvalidOperationException("Champion ending has no paired entry.");
                    Game.Features.QuestInventory.Execute(c,t,"INSERT INTO revora_champion_outcomes VALUES(@run,@first,@second,@winner,@now)",
                        "run",group.EntryKey,"first",(long)players[0].Entity.UID,"second",(long)players[1].Entity.UID,
                        "winner",group.Winner==null?0L:(long)group.Winner.Entity.UID,"now",DateTime.UtcNow);
                    var planned=new ResultAward[2];
                    for(int n=0;n<2;n++) {
                        var s=players[n].ChampionStats;Image saved;
                        if(!images.TryGetValue(s,out saved) || saved.Invalid || saved.Pending.HasValue || saved.Points.GetValueOrDefault()!=s.Points)
                            throw new InvalidOperationException("Champion ending preimage requires reload.");
                        bool won=ReferenceEquals(group.Winner,players[n]);int grade=n==0?group.FirstGrade:group.SecondGrade;
                        if(grade<1 || grade>7)throw new InvalidOperationException("Champion entry grade missing.");
                        byte streak=won?(byte)Math.Min(255,(int)s.WinStreak+1):(byte)0;
                        uint reward=Game.Champion.ChampionPoints[grade][won?Math.Min(5,(int)streak):0];
                        var a=new ResultAward{Stats=s,Uid=s.UID,Grade=grade,Award=reward,Before=saved.Points,ReservedRevision=checked(saved.Revision+1),
                            Points=AddBounded(s.Points,reward,304400),Today=AddBounded(s.TodayPoints,reward,uint.MaxValue),Total=AddBounded(s.TotalPoints,reward,uint.MaxValue),
                            Matches=(byte)Math.Min(255,(int)s.TotalMatches+1),Streak=streak,Reset=s.LastReset.Ticks,Yesterday=s.YesterdayPoints,YesterdayRank=s.YesterdayRank};
                        if(Game.Features.QuestInventory.Execute(c,t,@"UPDATE championarena SET revora_revision=@reserved
WHERE entityid=@uid AND revora_revision=@old AND points IS NOT DISTINCT FROM @points",
                            "reserved",a.ReservedRevision,"uid",(long)a.Uid,"old",saved.Revision,"points",(object)saved.Points??DBNull.Value)!=1)
                            throw new InvalidOperationException("Champion ending reservation changed.");
                        Game.Features.QuestInventory.Execute(c,t,@"INSERT INTO revora_champion_outcome_players VALUES(
@run,@uid,@grade,@award,@before,@reserved,@after,@today,@total,@matches,@streak,@reset,@yesterday,@rank)",
                            "run",group.EntryKey,"uid",(long)a.Uid,"grade",grade,"award",(int)reward,"before",(object)a.Before??DBNull.Value,"reserved",a.ReservedRevision,
                            "after",(long)a.Points,"today",(long)a.Today,"total",(long)a.Total,"matches",(int)a.Matches,"streak",(int)a.Streak,"reset",a.Reset,
                            "yesterday",(long)a.Yesterday,"rank",(long)a.YesterdayRank);planned[n]=a;
                    }
                    committing=true;t.Commit();
                    foreach(var a in planned){Image saved=images.GetValue(a.Stats,_=>new Image());saved.Revision=a.ReservedRevision;saved.Pending=group.EntryKey;}
                    return true;
                }}
            }catch(Exception ex) {
                if(committing)foreach(var p in players)Invalidate(p.ChampionStats,ex);
                Console.WriteLine("[champion] ending capture failed type="+ex.GetType().Name);return false;
            }
        }
        // Shared by live settlement and cold startup, without depending on an online actor.
        private static bool ApplyOutcome(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,Guid run,Outcome outcome) {
            using(var q=Game.Features.QuestInventory.Command(c,t,"SELECT player1,player2,winner FROM revora_champion_results WHERE run=@run","run",run))
            using(var r=q.ExecuteReader())if(r.Read()) {
                if(r.GetInt64(0)!=outcome.First || r.GetInt64(1)!=outcome.Second || r.GetInt64(2)!=outcome.Winner)throw new InvalidOperationException("Champion result receipt differs.");
                return false;
            }
            foreach(var a in outcome.Awards) {
                using(var q=Game.Features.QuestInventory.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)a.Uid))
                    if(q.ExecuteScalar()==null)throw new InvalidOperationException("Champion result owner missing.");
            }
            Game.Features.QuestInventory.Execute(c,t,"INSERT INTO revora_champion_results VALUES(@run,@first,@second,@winner,@now)",
                "run",run,"first",(long)outcome.First,"second",(long)outcome.Second,"winner",(long)outcome.Winner,"now",outcome.Observed);
            foreach(var a in outcome.Awards) {
                if(Game.Features.QuestInventory.Execute(c,t,@"UPDATE championarena SET points=@after,revora_revision=@revision,todaypoints=@today,totalpoints=@total,
totalmatches=@matches,winstreak=@streak,yesterdaypoints=@yesterday,yesterdayrank=@rank,lastreset=@reset
WHERE entityid=@uid AND revora_revision=@reserved AND points IS NOT DISTINCT FROM @before",
                    "after",(long)a.Points,"revision",a.Revision,"today",(long)a.Today,"total",(long)a.Total,"matches",(int)a.Matches,"streak",(int)a.Streak,
                    "yesterday",(long)a.Yesterday,"rank",(long)a.YesterdayRank,"reset",a.Reset,"uid",(long)a.Uid,"reserved",a.ReservedRevision,
                    "before",(object)a.Before??DBNull.Value)!=1)throw new InvalidOperationException("Champion saved ending preimage changed.");
                Game.Features.QuestInventory.Execute(c,t,@"INSERT INTO revora_champion_result_awards VALUES(
@run,@uid,@grade,@award,@before,@after,@today,@total,@matches,@streak,@revision)",
                    "run",run,"uid",(long)a.Uid,"grade",a.Grade,"award",(int)a.Award,"before",a.Before.GetValueOrDefault(),"after",(long)a.Points,
                    "today",(long)a.Today,"total",(long)a.Total,"matches",(int)a.Matches,"streak",(int)a.Streak,"revision",a.Revision);
            }
            return true;
        }
        internal static void RecoverOutcomes(Npgsql.NpgsqlConnection c) {
            if(Server.AuthServer!=null || Server.GameServer!=null || Kernel.GamePool.Count!=0)throw new InvalidOperationException("Champion outcome recovery requires cold startup.");
            var pending=new System.Collections.Generic.List<Guid>();
            using(var q=Game.Features.QuestInventory.Command(c,null,@"SELECT o.run FROM revora_champion_outcomes o
WHERE NOT EXISTS(SELECT 1 FROM revora_champion_results r WHERE r.run=o.run) ORDER BY o.observed_at,o.run LIMIT 4097"))
            using(var r=q.ExecuteReader())while(r.Read())pending.Add(r.GetGuid(0));
            if(pending.Count>4096)throw new InvalidOperationException("Champion outcome recovery exceeds its batch limit.");
            foreach(var run in pending)using(var t=c.BeginTransaction()) {
                var outcome=ReadOutcome(c,t,run);if(outcome==null)throw new InvalidOperationException("Champion ending disappeared.");
                ApplyOutcome(c,t,run,outcome);t.Commit();
            }
        }
        internal static bool Settle(Game.Champion.QualifierList.QualifierGroup group,out ResultAward[] awards) {
            awards=null;RequireOwners(group);bool committed=false,committing=false;
            try {
                if(!CaptureOutcome(group))return false;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    var outcome=ReadOutcome(c,t,group.EntryKey);SameOutcome(outcome,group);
                    bool applied=ApplyOutcome(c,t,group.EntryKey,outcome);
                    if(ResultBeforeCommit!=null)ResultBeforeCommit();committing=true;t.Commit();committed=true;
                    if(!applied)throw new InvalidOperationException("Champion committed result requires reload.");
                    if(ResultBeforeInstall!=null)ResultBeforeInstall();
                    foreach(var a in outcome.Awards) {
                        a.Stats=group.Player1.Entity.UID==a.Uid?group.Player1.ChampionStats:group.Player2.ChampionStats;
                        Image saved;
                        if(!images.TryGetValue(a.Stats,out saved) || saved.Invalid || saved.Pending!=group.EntryKey || saved.Revision!=a.ReservedRevision)
                            throw new InvalidOperationException("Champion saved ending cache requires reload.");
                        PointsCommitted(a.Stats,a.Points,a.Revision);a.Stats.TodayPoints=a.Today;a.Stats.TotalPoints=a.Total;
                        a.Stats.TotalMatches=a.Matches;a.Stats.WinStreak=a.Streak;a.Stats.LastReset=new DateTime(a.Reset);
                        a.Stats.YesterdayPoints=a.Yesterday;a.Stats.YesterdayRank=a.YesterdayRank;
                    }
                    awards=outcome.Awards;return true;
                }}
            }catch(Exception ex) {
                if(committing){Invalidate(group.Player1.ChampionStats,ex);Invalidate(group.Player2.ChampionStats,ex);}
                Console.WriteLine("[champion] result failed type="+ex.GetType().Name);return committed;
            }
        }

        public static void SaveStats(ChampionStatistic stats) {
            using(var conn=DataHolder.MySqlConnection){conn.Open();SaveStatistics(stats,conn);}
        }
        public static void InsertStatistic(Client.GameState client) {LoadClient(client);}
        public static void Reset(ChampionStatistic stat) {
            lock(stat){Image image;if(images.TryGetValue(stat,out image) && image.Pending.HasValue)return;stat.YesterdayPoints=stat.TodayPoints;stat.YesterdayRank=stat.Rank;stat.TodayPoints=0;
                stat.TotalMatches=0;stat.WinStreak=0;stat.Rank=0;stat.SignedUp=false;stat.LastReset=DateTime.Now;}
        }
    }
}

namespace Revora.Database
{
    // Entry journal only. Results and spendable points require a separate atomic settlement.
    internal static class ChampionEntryLedger
    {
        internal static void InitializeAndRecover() {
            if (Server.AuthServer != null || Server.GameServer != null || Kernel.GamePool.Count != 0)
                throw new InvalidOperationException("Champion entry recovery requires cold startup.");
            using (var c = DataHolder.MySqlConnection) { c.Open();
                Game.Features.QuestInventory.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_champion_entries(
 id uuid PRIMARY KEY,map integer NOT NULL CHECK(map>0 AND map<>700),
 state varchar(12) NOT NULL CHECK(state IN('reserved','entered','returned','aborted','interrupted')),
 created_at timestamptz NOT NULL,entered_at timestamptz,closed_at timestamptz,
 CHECK((state IN('reserved','entered') AND closed_at IS NULL) OR (state IN('returned','aborted','interrupted') AND closed_at IS NOT NULL)));
 CREATE TABLE IF NOT EXISTS revora_champion_entry_players(
 run uuid NOT NULL REFERENCES revora_champion_entries(id),uid bigint NOT NULL REFERENCES entities(uid),
 return_map integer NOT NULL,return_x integer NOT NULL,return_y integer NOT NULL,released_at timestamptz,
 PRIMARY KEY(run,uid));
 ALTER TABLE revora_champion_entry_players ADD COLUMN IF NOT EXISTS event_day date;
 ALTER TABLE revora_champion_entry_players ADD COLUMN IF NOT EXISTS round integer CHECK(round BETWEEN 1 AND 5);
 CREATE UNIQUE INDEX IF NOT EXISTS revora_champion_one_round ON revora_champion_entry_players(uid,event_day,round) WHERE event_day IS NOT NULL;
 CREATE UNIQUE INDEX IF NOT EXISTS revora_champion_one_entry ON revora_champion_entry_players(uid) WHERE released_at IS NULL;");
                ChampionTable.InitializeResults();ChampionTable.RecoverOutcomes(c);
                using (var t = c.BeginTransaction()) {
                    var pending = new System.Collections.Generic.List<object[]>();
                    using (var q = Game.Features.QuestInventory.Command(c,t,@"SELECT p.run,p.uid,p.return_map,p.return_x,p.return_y,e.map
FROM revora_champion_entry_players p JOIN revora_champion_entries e ON e.id=p.run
WHERE p.released_at IS NULL ORDER BY p.uid LIMIT 4097 FOR UPDATE OF p,e"))
                    using (var r=q.ExecuteReader()) while(r.Read()) pending.Add(new object[]{r.GetGuid(0),r.GetInt64(1),r.GetInt32(2),r.GetInt32(3),r.GetInt32(4),r.GetInt32(5)});
                    if(pending.Count>4096)throw new InvalidOperationException("Champion recovery exceeds its startup batch limit.");
                    var now=DateTime.UtcNow;
                    foreach(var row in pending) {
                        int map=(int)row[2],x=(int)row[3],y=(int)row[4];
                        if(map<=0 || map>65535 || !MapsTable.MapInformations.ContainsKey((ushort)map)) {map=1002;x=400;y=400;}
                        Game.Features.QuestInventory.Execute(c,t,@"UPDATE entities SET mapid=@destination,x=@x,y=@y WHERE uid=@uid AND mapid=@arena",
                            "destination",map,"x",x,"y",y,"uid",row[1],"arena",row[5]);
                        Game.Features.QuestInventory.Execute(c,t,"UPDATE revora_champion_entry_players SET released_at=@now WHERE run=@run AND uid=@uid AND released_at IS NULL",
                            "now",now,"run",row[0],"uid",row[1]);
                        Game.Features.QuestInventory.Execute(c,t,"UPDATE revora_champion_entries SET state='interrupted',closed_at=@now WHERE id=@run AND closed_at IS NULL","now",now,"run",row[0]);
                    }
                    t.Commit();
                }
            }
        }
        internal static void Reserve(Guid run,Client.GameState first,Client.GameState second,ushort map,Game.Map return1,ushort x1,ushort y1,Game.Map return2,ushort x2,ushort y2,DateTime day,int round) {
            if(day.TimeOfDay!=TimeSpan.Zero || round<1 || round>5)throw new InvalidOperationException("Champion reservation requires a scheduled round.");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                var players=new[]{first,second};Array.Sort(players,(a,b)=>a.Entity.UID.CompareTo(b.Entity.UID));
                foreach(var p in players) {
                    if(!System.Threading.Monitor.IsEntered(p.ItemSyncRoot) || !Game.Features.SessionInstall.Holds(p.Entity.UID) || !Game.Features.QuestInventory.Player(p))
                        throw new InvalidOperationException("Champion reservation requires canonical locked owners.");
                    SkillTable.LockOwner(c,t,p);
                    if(Game.Features.TeamArenaResultStatistics.Pending(c,t,p.Entity.UID) || Game.Features.ArenaMatchLedger.Pending(c,t,p.Entity.UID))
                        throw new InvalidOperationException("Champion owner has an unsettled arena result.");
                }
                Game.Features.QuestInventory.Execute(c,t,"INSERT INTO revora_champion_entries(id,map,state,created_at) VALUES(@run,@map,'reserved',@now)","run",run,"map",(int)map,"now",DateTime.UtcNow);
                AddPlayer(c,t,run,first,return1,x1,y1,day,round);AddPlayer(c,t,run,second,return2,x2,y2,day,round);t.Commit();
            }}
        }
        private static void AddPlayer(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,Guid run,Client.GameState p,Game.Map map,ushort x,ushort y,DateTime day,int round) {
            Game.Features.QuestInventory.Execute(c,t,@"INSERT INTO revora_champion_entry_players(run,uid,return_map,return_x,return_y,event_day,round)
VALUES(@run,@uid,@map,@x,@y,CAST(@day AS date),@round)","run",run,"uid",(long)p.Entity.UID,"map",(int)map.ID,"x",(int)x,"y",(int)y,"day",day.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture),"round",round);
        }
        internal static void Confirm(Guid run) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                if(Game.Features.QuestInventory.Execute(c,t,@"UPDATE revora_champion_entries SET state='entered',entered_at=@now
WHERE id=@run AND state='reserved' AND (SELECT count(*) FROM revora_champion_entry_players WHERE run=@run AND released_at IS NULL)=2",
                    "now",DateTime.UtcNow,"run",run)!=1)throw new InvalidOperationException("Champion paired arrival reservation changed.");
                t.Commit();
            }}
        }
        internal static void Close(Guid run,bool aborted) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                var now=DateTime.UtcNow;
                Game.Features.QuestInventory.Execute(c,t,"UPDATE revora_champion_entries SET state=@state,closed_at=@now WHERE id=@run AND closed_at IS NULL",
                    "state",aborted?"aborted":"returned","now",now,"run",run);
                Game.Features.QuestInventory.Execute(c,t,"UPDATE revora_champion_entry_players SET released_at=@now WHERE run=@run AND released_at IS NULL","now",now,"run",run);
                t.Commit();
            }}
        }
    }
}
